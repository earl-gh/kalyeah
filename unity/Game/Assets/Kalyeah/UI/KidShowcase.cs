using UnityEngine;

namespace Kalyeah.UI
{
    /// <summary>
    /// The main screen's 3D kid (title_screen.gd's hero stage): your kid's glTF model standing on a soft disc in front of the menu
    /// camera, idling, turned by a drag on the stage, swapped when you choose another kid. The camera sits a little to the right so
    /// the kid stands between the navigation rail and the play cluster. A missing model (assets not synced) shows nothing; the menus
    /// still work. Reduce motion stops the slow idle sway.
    /// </summary>
    public sealed class KidShowcase : MonoBehaviour
    {
        const float KidHeight = 1.65f;
        static readonly Vector3 CameraAt = new Vector3(0.55f, 1.05f, -3.9f);
        static readonly Vector3 LookAt = new Vector3(0.55f, 0.92f, 0f);

        public Camera Camera { get; private set; }
        Transform _stage;
        GameObject _model;
        string _shown = "";
        float _yaw = 180f;
        float _sway;

        public static KidShowcase Create(Transform parent)
        {
            var go = new GameObject("KidShowcase");
            go.transform.SetParent(parent, false);
            var showcase = go.AddComponent<KidShowcase>();
            showcase.Build();
            return showcase;
        }

        void Build()
        {
            var cameraObject = new GameObject("MenuCamera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = MenuKit.Night;
            Camera.fieldOfView = 34f;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 60f;
            Camera.transform.position = CameraAt;
            Camera.transform.LookAt(LookAt);

            var sun = new GameObject("MenuSun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.66f);

            _stage = new GameObject("Stage").transform;
            _stage.SetParent(transform, false);
            // a soft blue disc under the kid (the backlit floor glow)
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Floor";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            Renderer floor = disc.GetComponent<Renderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.SetColor("_BaseColor", MenuKit.Hex("2a5bb8"));
                floor.sharedMaterial = material;
            }
        }

        /// <summary>Shows kid `id` (no change when it is already shown).</summary>
        public void Show(string id)
        {
            if (id == _shown) return;
            _shown = id;
            if (_model != null) Destroy(_model);
            _model = null;
            GameObject prefab = Resources.Load<GameObject>("models/characters/" + id);
            if (prefab == null) return;
            _model = Instantiate(prefab, _stage, false);
            _model.name = "Kid_" + id;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            _model.transform.localScale = Vector3.one;
            foreach (Collider collider in _model.GetComponentsInChildren<Collider>()) Destroy(collider);
            Bounds bounds = Bounds(_model);
            if (bounds.size.y > 0.01f)
            {
                float scale = KidHeight / bounds.size.y;
                _model.transform.localScale = Vector3.one * scale;
                bounds = Bounds(_model);
                // feet on the floor, centred on the stage
                Vector3 shift = _stage.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                _model.transform.position += shift;
            }
            PlayIdle(_model);
        }

        /// <summary>A drag on the stage turns the kid (pixels of the 1280 x 720 base).</summary>
        public void Turn(float dragX) => _yaw -= dragX * 0.35f;

        public void SetVisible(bool visible)
        {
            if (_stage != null) _stage.gameObject.SetActive(visible);
        }

        void Update()
        {
            if (_stage == null) return;
            bool still = MenuServices.Settings != null && MenuServices.Settings.ReducedMotion;
            if (!still) _sway += Time.unscaledDeltaTime;
            float sway = still ? 0f : Mathf.Sin(_sway * 0.6f) * 8f;
            // glTF models face +Z; the camera looks along +Z, so a yaw of 180 turns the kid to face it
            _stage.localRotation = Quaternion.Euler(0f, _yaw + sway, 0f);
        }

        static Bounds Bounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>The model's idle clip on loop (glTFast imports legacy clips; any clip with "idle" in its name, else the first).</summary>
        static void PlayIdle(GameObject model)
        {
            Animation animation = model.GetComponentInChildren<Animation>();
            if (animation == null) return;
            string chosen = null;
            foreach (AnimationState state in animation)
            {
                if (chosen == null) chosen = state.name;
                if (state.name.ToLowerInvariant().Contains("idle"))
                {
                    chosen = state.name;
                    break;
                }
            }
            if (chosen == null) return;
            animation[chosen].wrapMode = WrapMode.Loop;
            animation.Play(chosen);
        }
    }
}
