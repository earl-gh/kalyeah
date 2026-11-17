using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kalyeah.Game
{
    /// <summary>
    /// The home screen's 3D hero: the selected kid's own model on a small lit stage far below the menu camera, rendered into a
    /// RenderTexture with a transparent background and shown full height behind the home UI (Wild Rift style lobby hero).
    /// View only: it never touches session or sim state.
    /// </summary>
    public sealed class HeroStage : IDisposable
    {
        const float KidHeight = 1.5f;
        const float TurnTowardUiDeg = -14f;
        const int TextureWidth = 768;
        const int TextureHeight = 1024;
        static readonly string[] IdleNames = { "Idle", "idle" };
        static int s_instances;

        readonly GameObject _root;
        readonly Camera _camera;
        readonly RenderTexture _texture;
        GameObject _model;
        GameObject _animRoot;
        AnimationClip _idle;
        string _kidId;
        float _time;

        public Texture Texture => _texture;

        /// <summary>Viewport height (0..1) of the kid's feet in the texture, so the UI can lay a floor shadow under them.</summary>
        public float FeetViewportY { get; private set; } = 0.05f;

        public HeroStage()
        {
            int index = s_instances++;
            _root = new GameObject("HeroStage");
            _root.transform.position = new Vector3(80f * index, -600f, 0f);

            _texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "HeroStage",
                antiAliasing = 2,
            };

            var cameraGo = new GameObject("HeroCamera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.enabled = true; // URP skips disabled cameras, even for an explicit render request
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = 26f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 20f;
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;
            _camera.transform.localPosition = new Vector3(0f, 1.0f, 4.3f);
            _camera.transform.LookAt(_root.transform.position + new Vector3(0f, 0.8f, 0f));

            AddLight("Key", new Color(1f, 0.94f, 0.86f), 1.25f, Quaternion.Euler(28f, 150f, 0f));
            AddLight("Rim", new Color(1f, 0.42f, 0.46f), 1.0f, Quaternion.Euler(16f, -32f, 0f));
            AddLight("Fill", new Color(0.56f, 0.66f, 0.86f), 0.45f, Quaternion.Euler(6f, 215f, 0f));
        }

        void AddLight(string name, Color color, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = rotation;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>Shows <paramref name="kidId"/> (falls back to junjun); a no-op when that kid is already on stage.</summary>
        public void SetKid(string kidId)
        {
            if (_model != null && string.Equals(kidId, _kidId, StringComparison.OrdinalIgnoreCase)) return;
            _kidId = kidId;
            if (_model != null) KalyeahResources.Kill(_model);
            _model = null;
            _animRoot = null;
            _idle = null;

            GameObject prefab = KalyeahResources.Model("characters", kidId) ?? KalyeahResources.Model("characters", "junjun");
            if (prefab == null) return;

            _model = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
            _model.name = "Kid";
            KalyeahResources.SanitizeModelMaterials(_model);

            Animation animation = _model.GetComponentInChildren<Animation>(true);
            if (animation != null)
            {
                animation.enabled = false;
                _animRoot = animation.gameObject;
                for (int i = 0; i < IdleNames.Length && _idle == null; i++)
                    _idle = animation.GetClip(IdleNames[i]);
            }
            Sample();

            float natural = Mathf.Max(KalyeahResources.RenderBounds(_model).size.y, 0.1f);
            _model.transform.localScale = Vector3.one * (KidHeight / natural);
            _model.transform.localRotation = Quaternion.Euler(0f, TurnTowardUiDeg, 0f) * FaceCamera(_model);

            Bounds placed = KalyeahResources.RenderBounds(_model);
            Vector3 origin = _root.transform.position;
            _model.transform.position += new Vector3(origin.x - placed.center.x, origin.y - placed.min.y, origin.z - placed.center.z);

            Vector3 feet = _camera.WorldToViewportPoint(origin);
            FeetViewportY = Mathf.Clamp01(feet.y);
        }

        /// <summary>The rotation that turns the model's face toward +Z (the stage camera), read from its head bones when it has them.</summary>
        static Quaternion FaceCamera(GameObject model)
        {
            Transform head = null;
            Transform front = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (front == null && n.IndexOf("headfront", StringComparison.OrdinalIgnoreCase) >= 0) front = t;
                else if (head == null && n.EndsWith("Head", StringComparison.Ordinal)) head = t;
            }
            if (head == null || front == null) return Quaternion.identity;
            Vector3 facing = front.position - head.position;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.FromToRotation(facing.normalized, Vector3.forward);
        }

        void Sample()
        {
            if (_idle == null || _animRoot == null) return;
            float length = Mathf.Max(_idle.length, 0.01f);
            _idle.SampleAnimation(_animRoot, _time % length);
        }

        /// <summary>Advances the idle loop (play mode: the enabled camera renders each frame by itself).</summary>
        public void Tick(float dt)
        {
            _time += Mathf.Max(0f, dt);
            Sample();
        }

        /// <summary>Renders one frame now (edit-mode screenshots, and the first frame after the kid changes).</summary>
        public void RenderNow()
        {
            if (_camera == null) return;
            if (!_texture.IsCreated()) _texture.Create();
#if UNITY_EDITOR
            // batchmode screenshots: compile the kid's shaders now instead of skipping it this frame
            bool prevAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            try { Submit(); }
            finally { UnityEditor.ShaderUtil.allowAsyncCompilation = prevAsync; }
#else
            Submit();
#endif
        }

        void Submit()
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _texture };
            if (RenderPipeline.SupportsRenderRequest(_camera, request)) RenderPipeline.SubmitRenderRequest(_camera, request);
            else _camera.Render();
        }

        /// <summary>One line of stage state for capture logs: kid, renderers, bounds and camera.</summary>
        public string Describe()
        {
            if (_model == null) return $"HeroStage kid={_kidId} model=null";
            Renderer[] renderers = _model.GetComponentsInChildren<Renderer>(true);
            Bounds b = KalyeahResources.RenderBounds(_model);
            Vector3 vp = _camera.WorldToViewportPoint(b.center);
            return $"HeroStage kid={_kidId} renderers={renderers.Length} bounds={b.center}/{b.size} scale={_model.transform.lossyScale.x:F3} "
                + $"cam={_camera.transform.position} fwd={_camera.transform.forward} centreViewport={vp} feet={FeetViewportY:F3} "
                + $"rt={_texture.width}x{_texture.height} created={_texture.IsCreated()} pipeline={GraphicsSettings.currentRenderPipeline}";
        }

        public void Dispose()
        {
            if (_root != null) KalyeahResources.Kill(_root);
            if (_texture != null)
            {
                _texture.Release();
                KalyeahResources.Kill(_texture);
            }
        }
    }
}
