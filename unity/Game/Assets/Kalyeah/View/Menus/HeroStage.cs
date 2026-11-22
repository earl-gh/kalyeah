using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Lightweight non-Selectable drag handler that orbits a <see cref="HeroStage"/> horizontally in Play mode.
    /// Strictly inert when <c>!Application.isPlaying</c> so static EditMode screenshots always lock to the hero front pose.
    /// </summary>
    public sealed class HeroOrbitDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public HeroStage Stage;
        public float DegreesPerPixel = 0.45f;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Application.isPlaying || Stage == null) return;
            Stage.SetOrbitDragging(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Application.isPlaying || Stage == null) return;
            Stage.AddOrbitDelta(-eventData.delta.x * DegreesPerPixel);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Application.isPlaying || Stage == null) return;
            Stage.SetOrbitDragging(false);
        }

        void OnDisable()
        {
            if (Stage != null) Stage.SetOrbitDragging(false);
        }
    }

    /// <summary>
    /// The home, Kids and Pick screens' 3D hero showcase: the selected kid's own model on a studio-lit stage far below the menu
    /// camera (warm golden key, cool teal-slate rim, balanced fill and a soft grounded pedestal disc), rendered into a
    /// RenderTexture with a transparent background. Supports optional smooth touch drag-to-orbit in Play mode while snapping
    /// cleanly to the hero front pose in EditMode for deterministic captures. View only: never touches session or sim state.
    /// </summary>
    public sealed class HeroStage : IDisposable
    {
        const float KidHeight = 1.5f;
        const float TurnTowardUiDeg = -14f;
        const float PedestalRadius = 0.64f;
        const float OrbitDamping = 6.5f;
        const int TextureWidth = 768;
        const int TextureHeight = 1024;
        static readonly string[] IdleNames = { "Idle", "idle" };
        static int s_instances;

        readonly GameObject _root;
        readonly Camera _camera;
        readonly RenderTexture _texture;
        readonly GameObject _pedestalGo;
        readonly Mesh _pedestalMesh;
        readonly Material _pedestalMat;
        GameObject _model;
        GameObject _animRoot;
        AnimationClip _idle;
        FaceBlendShapeBinding _faceShapes = FaceBlendShapeBinding.Empty;
        float _blinkTimer = 2.8f;
        float _blinkPhase;
        int _blinkCycle;
        string _kidId;
        float _time;
        Quaternion _baseRotation = Quaternion.identity;
        float _orbitYaw;
        float _orbitVelocity;
        bool _isDragging;

        public Texture Texture => _texture;

        /// <summary>Root transform of this hero showcase stage (read-only).</summary>
        public Transform StageRoot => _root != null ? _root.transform : null;

        /// <summary>True when the current showcase kid mesh exposes supported glTF facial morph targets.</summary>
        public bool HasFaceBlendShapes => _faceShapes.HasAnyShape;

        /// <summary>Viewport height (0..1) of the kid's feet in the texture, so the UI can align a floor shadow or nameplate under them.</summary>
        public float FeetViewportY { get; private set; } = 0.05f;

        /// <summary>Current play-mode horizontal orbit offset in degrees (always 0 in EditMode).</summary>
        public float OrbitYaw => _orbitYaw;

        /// <summary>Toggles the 3D grounded shadow and luminous pedestal ring directly beneath the kid's feet.</summary>
        public bool ShowPedestal
        {
            get => _pedestalGo != null && _pedestalGo.activeSelf;
            set
            {
                if (_pedestalGo != null) _pedestalGo.SetActive(value);
            }
        }

        public HeroStage()
        {
            int index = s_instances++;
            _root = new GameObject("HeroStage");
            _root.transform.position = new Vector3(80f * index, -600f, 0f);

            _texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "HeroStage",
                antiAliasing = 1, // URP renders MSAA in its own target and resolves into this one; a mismatched count fails the final blit
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

            // URP-valid 3-point studio showcase rig (calibrated for Kalyeah/Kid LDR+HDR without highlight clipping):
            AddDirectionalKey("Key", new Color(1.00f, 0.96f, 0.88f), 1.02f, Quaternion.Euler(38f, 152f, 0f));
            AddSpotRim("Rim", new Color(0.62f, 0.84f, 1.00f), 2.40f, new Vector3(-1.35f, 1.95f, -1.75f), new Vector3(0f, 1.05f, 0f), range: 8.0f, spotAngle: 58f);
            AddPointFill("Fill", new Color(0.86f, 0.90f, 0.98f), 0.72f, new Vector3(1.85f, 0.95f, 1.85f), range: 8.0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.56f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.46f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.31f, 0.28f);
            DynamicGI.UpdateEnvironment();

            _pedestalGo = BuildPedestal(_root.transform, out _pedestalMesh, out _pedestalMat);
        }

        void AddDirectionalKey(string name, Color color, float intensity, Quaternion rotation)
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

        void AddSpotRim(string name, Color color, float intensity, Vector3 localPos, Vector3 localTarget, float range, float spotAngle)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation((localTarget - localPos).normalized, Vector3.up);
            Light light = go.GetComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.48f;
            light.shadows = LightShadows.None;
        }

        void AddPointFill(string name, Color color, float intensity, Vector3 localPos, float range)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = localPos;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static GameObject BuildPedestal(Transform parent, out Mesh mesh, out Material mat)
        {
            Material src = Resources.Load<Material>("KalyeahMaterials/TransparentUnlit")
                        ?? Resources.Load<Material>("KalyeahMaterials/Unlit");
            Shader fallback = src == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (src == null && fallback == null)
            {
                mesh = null;
                mat = null;
                return null;
            }

            mesh = new Mesh { name = "HeroPedestalQuad", hideFlags = HideFlags.HideAndDontSave };
            const float h = PedestalRadius;
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h),
                new Vector3(-h, 0f, h),
                new Vector3(h, 0f, h),
                new Vector3(h, 0f, -h),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            mat = src != null
                ? new Material(src) { name = "HeroPedestalMat", hideFlags = HideFlags.HideAndDontSave }
                : new Material(fallback) { name = "HeroPedestalMat", hideFlags = HideFlags.HideAndDontSave };

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;

            Texture2D floorTex = MenuArt.PedestalFloorTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", floorTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", floorTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            var go = new GameObject("Pedestal", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>Shows <paramref name="kidId"/> (falls back to narda); a no-op when that kid is already on stage.</summary>
        public void SetKid(string kidId)
        {
            if (_root == null) return;
            if (_model != null && string.Equals(kidId, _kidId, StringComparison.OrdinalIgnoreCase)) return;
            _kidId = kidId;
            if (_model != null) KalyeahResources.Kill(_model);
            _model = null;
            _animRoot = null;
            _idle = null;
            _faceShapes = FaceBlendShapeBinding.Empty;
            _blinkTimer = 2.8f;
            _blinkPhase = 0f;
            _blinkCycle = 0;
            _orbitYaw = 0f;
            _orbitVelocity = 0f;
            _isDragging = false;

            string resolvedKidId = string.IsNullOrWhiteSpace(kidId) ? "narda" : kidId.Trim().ToLowerInvariant();
            GameObject prefab = KalyeahResources.Model("characters", resolvedKidId);
            if (prefab == null)
            {
                resolvedKidId = "narda";
                prefab = KalyeahResources.Model("characters", resolvedKidId);
            }
            if (prefab == null) return;

            _model = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
            _model.name = "Kid";
            KalyeahResources.UpgradeKidMaterials(_model, resolvedKidId, KalyeahResources.CharacterRig(resolvedKidId));
            KidView.TryFindFaceBlendShapes(_model, out _faceShapes);

            Animation animation = _model.GetComponentInChildren<Animation>(true);
            if (animation != null)
            {
                animation.enabled = false;
                _animRoot = animation.gameObject;
                for (int i = 0; i < IdleNames.Length && _idle == null; i++)
                    _idle = animation.GetClip(IdleNames[i]);
            }
            Sample();
            TickFaceExpressions(0f);

            float natural = Mathf.Max(KalyeahResources.RenderBounds(_model).size.y, 0.1f);
            _model.transform.localScale = Vector3.one * (KidHeight / natural);
            _baseRotation = Quaternion.Euler(0f, TurnTowardUiDeg, 0f) * FaceCamera(_model);
            _model.transform.localRotation = _baseRotation;

            Bounds placed = KalyeahResources.RenderBounds(_model);
            Vector3 origin = _root.transform.position;
            _model.transform.position += new Vector3(origin.x - placed.center.x, origin.y - placed.min.y, origin.z - placed.center.z);

            Vector3 feet = _camera.WorldToViewportPoint(origin);
            FeetViewportY = Mathf.Clamp01(feet.y);
        }

        /// <summary>Rotates the hero horizontally in Play mode with smooth release inertia; no-op and snaps to front in EditMode.</summary>
        public void AddOrbitDelta(float deltaDegrees)
        {
            if (_root == null) return;
            if (!Application.isPlaying)
            {
                ResetOrbit();
                return;
            }
            _orbitYaw = WrapAngle(_orbitYaw + deltaDegrees);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            _orbitVelocity = Mathf.Clamp(deltaDegrees / dt, -540f, 540f);
            ApplyModelRotation();
        }

        /// <summary>Marks drag begin/end so Play-mode orbit coasts smoothly on release.</summary>
        public void SetOrbitDragging(bool dragging)
        {
            if (_root == null) return;
            if (!Application.isPlaying)
            {
                ResetOrbit();
                return;
            }
            _isDragging = dragging;
        }

        /// <summary>Snaps the hero cleanly back to the deterministic front-facing pose.</summary>
        public void ResetOrbit()
        {
            _orbitYaw = 0f;
            _orbitVelocity = 0f;
            _isDragging = false;
            ApplyModelRotation();
        }

        /// <summary>
        /// Attaches a non-Selectable <see cref="HeroOrbitDrag"/> handler to <paramref name="dragSurface"/> (e.g. the hero RawImage)
        /// so players can smoothly spin the 3D kid by dragging in Play mode.
        /// </summary>
        public HeroOrbitDrag AttachOrbitDrag(RectTransform dragSurface, float degreesPerPixel = 0.45f)
        {
            if (dragSurface == null) return null;
            Graphic g = dragSurface.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
            HeroOrbitDrag drag = dragSurface.GetComponent<HeroOrbitDrag>();
            if (drag == null) drag = dragSurface.gameObject.AddComponent<HeroOrbitDrag>();
            drag.Stage = this;
            drag.DegreesPerPixel = degreesPerPixel;
            return drag;
        }

        static float WrapAngle(float deg) => Mathf.Repeat(deg + 180f, 360f) - 180f;

        void ApplyModelRotation()
        {
            if (_model == null) return;
            _model.transform.localRotation = Mathf.Abs(_orbitYaw) > 0.001f
                ? Quaternion.Euler(0f, _orbitYaw, 0f) * _baseRotation
                : _baseRotation;
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

        /// <summary>Advances the idle loop, lobby facial expressions, and smooth Play-mode orbit inertia (snaps to front pose in EditMode).</summary>
        public void Tick(float dt)
        {
            float clampedDt = Mathf.Max(0f, dt);
            _time += clampedDt;
            Sample();
            TickFaceExpressions(clampedDt);

            if (!Application.isPlaying)
            {
                if (_orbitYaw != 0f || _orbitVelocity != 0f) ResetOrbit();
                return;
            }

            if (!_isDragging && Mathf.Abs(_orbitVelocity) > 0.1f)
            {
                _orbitYaw = WrapAngle(_orbitYaw + _orbitVelocity * clampedDt);
                _orbitVelocity *= Mathf.Exp(-OrbitDamping * clampedDt);
                if (Mathf.Abs(_orbitVelocity) <= 0.1f) _orbitVelocity = 0f;
                ApplyModelRotation();
            }
        }

        void TickFaceExpressions(float dt)
        {
            if (!_faceShapes.HasAnyShape) return;

            bool staticMode = !Application.isPlaying || SettingsStore.Current.ReduceMotion;
            float blinkW = 0f;
            if (!staticMode && dt > 0f && _faceShapes.HasBlink)
            {
                if (_blinkPhase > 0f)
                {
                    _blinkPhase += dt / 0.14f;
                    if (_blinkPhase >= 1f)
                    {
                        _blinkPhase = 0f;
                        _blinkCycle++;
                        _blinkTimer = 3.1f + ((_blinkCycle * 7) % 5) * 0.42f;
                    }
                    else
                    {
                        blinkW = Mathf.Sin(_blinkPhase * Mathf.PI) * 100f;
                    }
                }
                else
                {
                    _blinkTimer -= dt;
                    if (_blinkTimer <= 0f) _blinkPhase = 0.001f;
                }
            }
            else
            {
                _blinkPhase = 0f;
            }

            float smileW = staticMode ? 18f : 18f + 10f * Mathf.Sin(_time * 0.85f);
            float determinedW = staticMode ? 8f : 8f + 6f * Mathf.Cos(_time * 0.55f);

            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.Blink, blinkW);
            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.BlinkL, blinkW);
            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.BlinkR, blinkW);
            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.Smile, smileW);
            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.Determined, determinedW);
            KidView.SetFaceShapeWeightSafe(in _faceShapes, _faceShapes.TalkOpen, 0f);
        }

        /// <summary>Renders one frame now (edit-mode screenshots, and the first frame after the kid changes).</summary>
        public void RenderNow()
        {
            if (_camera == null) return;
            if (!Application.isPlaying && (_orbitYaw != 0f || _orbitVelocity != 0f)) ResetOrbit();
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
            if (_pedestalMat != null) KalyeahResources.Kill(_pedestalMat);
            if (_pedestalMesh != null) KalyeahResources.Kill(_pedestalMesh);
            if (_texture != null)
            {
                _texture.Release();
                KalyeahResources.Kill(_texture);
            }
        }
    }
}
