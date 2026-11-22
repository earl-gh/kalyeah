using System;
using System.Collections.Generic;
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
    /// Live 3D hero showcase stage shared by Home, Profile, Kids, and Pick screens.
    /// Supports two clean modes:
    /// (1) <see cref="ShowManilaStreetStage"/> = <c>true</c>: crisp 10:30 AM sunlit 3D Manila Barangay street corner
    ///     (<c>1280x720</c> 16:9 RenderTexture, <c>Kalyeah/TropicalSky</c> dome, sunlit concrete intersection + curbs,
    ///     real Filipino <c>.glb</c> storefronts/houses with <c>Kalyeah/BarangayBuilding</c> stucco stand-in fallbacks,
    ///     subtle utility poles, soft grounded contact shadow disc, and balanced non-clipping 3-point daylight).
    /// (2) <see cref="ShowManilaStreetStage"/> = <c>false</c>: transparent <c>768x1024</c> studio showcase with optional
    ///     grounded pedestal ring for Profile / Kids / Pick.
    /// Supports smooth Play-mode horizontal drag-to-orbit while snapping deterministically to front pose in EditMode.
    /// View only: never mutates session or simulation state.
    /// </summary>
    public sealed class HeroStage : IDisposable
    {
        const float KidHeight = 1.5f;
        const float TurnTowardUiDeg = -14f;
        const float StreetTurnDeg = -8f;
        const float PedestalRadius = 0.64f;
        const float ContactShadowRadius = 0.56f;
        const float OrbitDamping = 6.5f;

        public const int StudioTextureWidth = 768;
        public const int StudioTextureHeight = 1024;
        public const int StreetTextureWidth = 1280;
        public const int StreetTextureHeight = 720;

        static readonly Color StudioKeyColor = new Color(1.00f, 0.96f, 0.88f);
        static readonly Color StudioRimColor = new Color(0.62f, 0.84f, 1.00f);
        static readonly Color StudioFillColor = new Color(0.86f, 0.90f, 0.98f);
        static readonly Color StudioAmbientSky = new Color(0.44f, 0.56f, 0.72f);
        static readonly Color StudioAmbientEq = new Color(0.48f, 0.46f, 0.44f);
        static readonly Color StudioAmbientGround = new Color(0.34f, 0.31f, 0.28f);

        // Calibrated 10:30 AM Manila sunlit exposure: crisp daylight clarity without clipping Karding's white sando or face.
        static readonly Color StreetSkyClear = new Color(0.620f, 0.816f, 0.949f, 1f);        // #9ED0F2
        static readonly Color StreetSkyZenith = new Color(0.365f, 0.655f, 0.910f, 1f);       // #5DA7E8
        static readonly Color StreetSkyMid = new Color(0.659f, 0.831f, 0.961f, 1f);          // #A8D4F5
        static readonly Color StreetSkyHorizon = new Color(0.855f, 0.929f, 0.976f, 1f);      // #DAEDF9
        static readonly Color StreetSkyGlow = new Color(0.945f, 0.969f, 0.988f, 1f);         // #F1F7FC
        static readonly Color StreetKeyColor = new Color(1.00f, 0.965f, 0.902f, 1f);         // #FFF6E6
        static readonly Color StreetRimColor = new Color(0.66f, 0.86f, 1.00f, 1f);
        static readonly Color StreetFillColor = new Color(0.88f, 0.92f, 0.98f, 1f);
        static readonly Color StreetAmbientSky = new Color(0.361f, 0.549f, 0.784f, 1f);      // #5C8CC8
        static readonly Color StreetAmbientEq = new Color(0.596f, 0.596f, 0.580f, 1f);       // #989894
        static readonly Color StreetAmbientGround = new Color(0.420f, 0.384f, 0.345f, 1f);   // #6B6258
        static readonly Color ConcreteRoadColor = new Color(0.784f, 0.769f, 0.737f, 1f);     // #C8C4BC
        static readonly Color ConcreteCurbColor = new Color(0.863f, 0.847f, 0.812f, 1f);     // #DCD8CF
        static readonly Color ConcreteWalkColor = new Color(0.831f, 0.812f, 0.776f, 1f);     // #D4CFC6
        static readonly Color RoadLineColor = new Color(0.935f, 0.922f, 0.895f, 0.82f);
        static readonly Color CastShadowColor = new Color(0.28f, 0.38f, 0.52f, 0.28f);
        static readonly Color PoleConcreteColor = new Color(0.675f, 0.663f, 0.635f, 1f);
        static readonly Color PoleArmColor = new Color(0.46f, 0.40f, 0.34f, 1f);
        static readonly Color CompoundWallColor = new Color(0.835f, 0.796f, 0.733f, 1f);

        static readonly string[] IdleNames = { "Idle", "idle" };
        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropColor = Shader.PropertyToID("_Color");
        static readonly int PropSmoothness = Shader.PropertyToID("_Smoothness");
        static readonly int PropMetallic = Shader.PropertyToID("_Metallic");
        static readonly int PropSkyZenith = Shader.PropertyToID("_SkyZenith");
        static readonly int PropSkyMid = Shader.PropertyToID("_SkyMid");
        static readonly int PropSkyHorizon = Shader.PropertyToID("_SkyHorizon");
        static readonly int PropHorizonGlow = Shader.PropertyToID("_HorizonGlow");
        static readonly int PropGroundColor = Shader.PropertyToID("_GroundColor");
        static readonly int PropExposure = Shader.PropertyToID("_Exposure");
        static readonly int PropHorizonClarityBand = Shader.PropertyToID("_HorizonClarityBand");

        static int s_instances;
        static Texture2D s_contactShadowTex;

        readonly GameObject _root;
        readonly Camera _camera;
        readonly Light _keyLight;
        readonly Light _rimLight;
        readonly Light _fillLight;
        readonly GameObject _pedestalGo;
        readonly GameObject _contactShadowGo;
        readonly List<UnityEngine.Object> _ownedObjects = new List<UnityEngine.Object>(16);

        RenderTexture _texture;
        GameObject _streetStageRoot;
        bool _showManilaStreetStage;
        bool _showPedestalRequested = true;

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

        /// <summary>Toggles the 3D grounded studio pedestal ring directly beneath the kid's feet (hidden automatically when <see cref="ShowManilaStreetStage"/> is active).</summary>
        public bool ShowPedestal
        {
            get => _pedestalGo != null && _pedestalGo.activeSelf;
            set
            {
                _showPedestalRequested = value;
                SyncFloorShadowVisibility();
            }
        }

        /// <summary>
        /// Enables the crisp 10:30 AM sunlit 3D Manila Barangay street corner backdrop (<c>1280x720</c> 16:9 opaque RT)
        /// behind the hero for Main Lobby, or restores transparent <c>768x1024</c> studio showcase mode for Profile/Kids/Pick.
        /// </summary>
        public bool ShowManilaStreetStage
        {
            get => _showManilaStreetStage;
            set
            {
                if (_showManilaStreetStage == value) return;
                _showManilaStreetStage = value;
                ApplyStageMode();
            }
        }

        public HeroStage()
        {
            int index = s_instances++;
            _root = new GameObject("HeroStage");
            // Keep Y = 0 so world-space height shaders (Kalyeah/BarangayBuilding plinth/stucco/shutters) shade cleanly while isolated far off-map.
            _root.transform.position = new Vector3(600f + 80f * index, 0f, 600f);

            _texture = CreateRenderTexture(StudioTextureWidth, StudioTextureHeight, "HeroStage");

            var cameraGo = new GameObject("HeroCamera", typeof(Camera));
            cameraGo.transform.SetParent(_root.transform, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.enabled = true; // URP skips disabled cameras, even for an explicit render request
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;

            // URP-valid 3-point rig (strictly 1 Directional Key + 1 Spot Rim + 1 Point Fill).
            _keyLight = AddDirectionalKey("Key", StudioKeyColor, 1.02f, Quaternion.Euler(38f, 152f, 0f));
            _rimLight = AddSpotRim("Rim", StudioRimColor, 2.40f, new Vector3(-1.35f, 1.95f, -1.75f), new Vector3(0f, 1.05f, 0f), range: 8.0f, spotAngle: 58f);
            _fillLight = AddPointFill("Fill", StudioFillColor, 0.72f, new Vector3(1.85f, 0.95f, 1.85f), range: 8.0f);

            _pedestalGo = BuildFloorQuad("Pedestal", _root.transform, PedestalRadius, 0.004f, MenuArt.PedestalFloorTexture());
            _contactShadowGo = BuildFloorQuad("ContactShadow", _root.transform, ContactShadowRadius, 0.005f, ContactShadowTexture());

            ApplyStageMode();
        }

        static RenderTexture CreateRenderTexture(int width, int height, string name)
        {
            return new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 1, // URP resolves MSAA internally before blitting into single-sample RT; mismatched MSAA fails final blit
            };
        }

        void ApplyStageMode()
        {
            if (_root == null || _camera == null) return;

            int desiredW = _showManilaStreetStage ? StreetTextureWidth : StudioTextureWidth;
            int desiredH = _showManilaStreetStage ? StreetTextureHeight : StudioTextureHeight;
            if (_texture == null || _texture.width != desiredW || _texture.height != desiredH)
            {
                RenderTexture old = _texture;
                _texture = CreateRenderTexture(desiredW, desiredH, _showManilaStreetStage ? "HeroStage_ManilaStreet" : "HeroStage");
                _camera.targetTexture = _texture;
                if (old != null)
                {
                    old.Release();
                    KalyeahResources.Kill(old);
                }
            }

            UniversalAdditionalCameraData camData = _camera.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.antialiasing = _showManilaStreetStage
                    ? AntialiasingMode.FastApproximateAntialiasing
                    : AntialiasingMode.None;
            }

            if (_showManilaStreetStage)
            {
                if (_streetStageRoot == null)
                    _streetStageRoot = BuildManilaStreetCorner(_root.transform);
                if (_streetStageRoot != null)
                    _streetStageRoot.SetActive(true);

                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = StreetSkyClear;
                _camera.fieldOfView = 32f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 65f;
                _camera.transform.localPosition = new Vector3(0f, 1.06f, 4.35f);
                _camera.transform.LookAt(_root.transform.position + new Vector3(0f, 0.82f, 0f));

                ApplyLighting(
                    StreetKeyColor, 1.05f, Quaternion.Euler(44f, 146f, 0f),
                    StreetRimColor, 2.00f,
                    StreetFillColor, 0.60f,
                    StreetAmbientSky, StreetAmbientEq, StreetAmbientGround);
            }
            else
            {
                if (_streetStageRoot != null)
                    _streetStageRoot.SetActive(false);

                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _camera.fieldOfView = 26f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 20f;
                _camera.transform.localPosition = new Vector3(0f, 1.0f, 4.3f);
                _camera.transform.LookAt(_root.transform.position + new Vector3(0f, 0.8f, 0f));

                ApplyLighting(
                    StudioKeyColor, 1.02f, Quaternion.Euler(38f, 152f, 0f),
                    StudioRimColor, 2.40f,
                    StudioFillColor, 0.72f,
                    StudioAmbientSky, StudioAmbientEq, StudioAmbientGround);
            }

            SyncFloorShadowVisibility();

            if (_model != null)
            {
                _baseRotation = Quaternion.Euler(0f, _showManilaStreetStage ? StreetTurnDeg : TurnTowardUiDeg, 0f) * FaceCamera(_model);
                ApplyModelRotation();
            }

            Vector3 feet = _camera.WorldToViewportPoint(_root.transform.position);
            FeetViewportY = Mathf.Clamp01(feet.y);
        }

        void SyncFloorShadowVisibility()
        {
            if (_pedestalGo != null)
                _pedestalGo.SetActive(!_showManilaStreetStage && _showPedestalRequested);
            if (_contactShadowGo != null)
                _contactShadowGo.SetActive(_showManilaStreetStage);
        }

        void ApplyLighting(
            Color keyColor, float keyIntensity, Quaternion keyRot,
            Color rimColor, float rimIntensity,
            Color fillColor, float fillIntensity,
            Color ambSky, Color ambEq, Color ambGround)
        {
            if (_keyLight != null)
            {
                _keyLight.color = keyColor;
                _keyLight.intensity = keyIntensity;
                _keyLight.transform.rotation = keyRot;
            }
            if (_rimLight != null)
            {
                _rimLight.color = rimColor;
                _rimLight.intensity = rimIntensity;
            }
            if (_fillLight != null)
            {
                _fillLight.color = fillColor;
                _fillLight.intensity = fillIntensity;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;
            DynamicGI.UpdateEnvironment();
        }

        Light AddDirectionalKey(string name, Color color, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = rotation;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        Light AddSpotRim(string name, Color color, float intensity, Vector3 localPos, Vector3 localTarget, float range, float spotAngle)
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
            return light;
        }

        Light AddPointFill(string name, Color color, float intensity, Vector3 localPos, float range)
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
            return light;
        }

        GameObject BuildFloorQuad(string name, Transform parent, float halfSize, float yOffset, Texture2D tex)
        {
            Material mat = CreateTransparentUnlitMaterial(name + "Mat", tex, Color.white);
            if (mat == null) return null;

            Mesh mesh = new Mesh { name = name + "Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-halfSize, 0f, -halfSize),
                new Vector3(-halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, -halfSize),
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
            _ownedObjects.Add(mesh);

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        Material CreateTransparentUnlitMaterial(string name, Texture2D tex, Color color)
        {
            Material src = Resources.Load<Material>("KalyeahMaterials/TransparentUnlit")
                        ?? Resources.Load<Material>("KalyeahMaterials/Unlit");
            Shader fallback = src == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (src == null && fallback == null) return null;

            Material mat = src != null
                ? new Material(src) { name = name, hideFlags = HideFlags.HideAndDontSave }
                : new Material(fallback) { name = name, hideFlags = HideFlags.HideAndDontSave };

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, color);
            if (mat.HasProperty(PropColor)) mat.SetColor(PropColor, color);

            _ownedObjects.Add(mat);
            return mat;
        }

        Material CreateSunlitLitMaterial(string name, Color color, float smoothness = 0.08f, float metallic = 0f)
        {
            Material src = Resources.Load<Material>("KalyeahMaterials/Lit");
            Shader fallback = src == null
                ? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit"))
                : null;
            if (src == null && fallback == null) return null;

            Material mat = src != null
                ? new Material(src) { name = name, hideFlags = HideFlags.HideAndDontSave }
                : new Material(fallback) { name = name, hideFlags = HideFlags.HideAndDontSave };

            if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, color);
            if (mat.HasProperty(PropColor)) mat.SetColor(PropColor, color);
            if (mat.HasProperty(PropSmoothness)) mat.SetFloat(PropSmoothness, smoothness);
            if (mat.HasProperty(PropMetallic)) mat.SetFloat(PropMetallic, metallic);
            _ownedObjects.Add(mat);
            return mat;
        }

        /// <summary>Soft grounded contact shadow texture beneath the kid's feet on the sunlit street (strictly zero glowing ring per ART.md).</summary>
        static Texture2D ContactShadowTexture()
        {
            if (s_contactShadowTex != null) return s_contactShadowTex;

            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = ((y + 0.5f) / size * 2f - 1f) * 1.18f; // subtly elliptical shadow footprint
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float falloff = Mathf.Pow(1f - Mathf.Clamp01(r), 2.2f);
                    byte a = (byte)Mathf.RoundToInt(falloff * 0.52f * 255f);
                    pixels[y * size + x] = new Color32(24, 34, 48, a);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahHeroContactShadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            s_contactShadowTex = tex;
            return tex;
        }

        /// <summary>
        /// Constructs the crisp 10:30 AM sunlit 3D Manila Barangay street corner diorama behind the hero:
        /// TropicalSky dome, sunlit concrete intersection + angled sidewalks/curbs, real Filipino storefront/house
        /// <c>.glb</c> props (with <c>Kalyeah/BarangayBuilding</c> stucco fallbacks), compound walls, and utility poles.
        /// </summary>
        GameObject BuildManilaStreetCorner(Transform parent)
        {
            var stageRoot = new GameObject("ManilaStreetCorner");
            stageRoot.transform.SetParent(parent, false);

            // 1. 10:30 AM Pale-Blue Tropical Sky Dome (isolated mesh dome — never overwrites scene RenderSettings.skybox)
            BuildSkyDome(stageRoot.transform);

            // 2. Open sunlit concrete intersection ground, curbs, sidewalks, lane markings & architectural cast shadows
            Material roadMat = CreateSunlitLitMaterial("ManilaRoadMat", ConcreteRoadColor, 0.08f);
            Material curbMat = CreateSunlitLitMaterial("ManilaCurbMat", ConcreteCurbColor, 0.12f);
            Material walkMat = CreateSunlitLitMaterial("ManilaSidewalkMat", ConcreteWalkColor, 0.10f);
            Material lineMat = CreateTransparentUnlitMaterial("ManilaRoadLineMat", null, RoadLineColor);
            Material shadeMat = CreateTransparentUnlitMaterial("ManilaCastShadowMat", null, CastShadowColor);
            Material poleMat = CreateSunlitLitMaterial("ManilaPoleMat", PoleConcreteColor, 0.14f);
            Material armMat = CreateSunlitLitMaterial("ManilaPoleArmMat", PoleArmColor, 0.18f);
            Material wallMat = CreateSunlitLitMaterial("ManilaCompoundWallMat", CompoundWallColor, 0.08f);

            // Main concrete road plane (top strictly at Y = 0.0f)
            MakeBox("IntersectionGround", stageRoot.transform, new Vector3(0f, -0.10f, -6f), new Vector3(42f, 0.20f, 32f), 0f, roadMat);

            // Raised corner sidewalk aprons framing the V-shaped Manila street intersection behind Karding
            MakeBox("SidewalkLeft", stageRoot.transform, new Vector3(-6.8f, 0.07f, -6.2f), new Vector3(16.0f, 0.14f, 6.2f), 11f, walkMat);
            MakeBox("CurbLeft", stageRoot.transform, new Vector3(-6.4f, 0.075f, -3.15f), new Vector3(15.6f, 0.15f, 0.28f), 11f, curbMat);
            MakeBox("SidewalkRight", stageRoot.transform, new Vector3(6.8f, 0.07f, -6.2f), new Vector3(16.0f, 0.14f, 6.2f), -11f, walkMat);
            MakeBox("CurbRight", stageRoot.transform, new Vector3(6.4f, 0.075f, -3.15f), new Vector3(15.6f, 0.15f, 0.28f), -11f, curbMat);
            MakeBox("CornerApron", stageRoot.transform, new Vector3(0f, 0.07f, -5.0f), new Vector3(6.4f, 0.14f, 5.2f), 0f, walkMat);
            MakeBox("CornerCurbFront", stageRoot.transform, new Vector3(0f, 0.075f, -2.42f), new Vector3(5.6f, 0.15f, 0.28f), 0f, curbMat);

            // Clean sunlit road corner markings & soft 10:30 AM angled building cast-shadow bands on foreground street
            if (lineMat != null)
            {
                MakeBox("RoadStripeLeft", stageRoot.transform, new Vector3(-4.2f, 0.008f, -1.85f), new Vector3(6.5f, 0.006f, 0.09f), 11f, lineMat);
                MakeBox("RoadStripeRight", stageRoot.transform, new Vector3(4.2f, 0.008f, -1.85f), new Vector3(6.5f, 0.006f, 0.09f), -11f, lineMat);
            }
            if (shadeMat != null)
            {
                // Soft cool-blue architectural shadow cast from left foreground block across lower-left street corner
                MakeBox("StreetShadowLeft", stageRoot.transform, new Vector3(-2.65f, 0.007f, 0.25f), new Vector3(4.8f, 0.005f, 1.85f), 18f, shadeMat);
                // Crisp subtle shadow lip hugging the back curbs
                MakeBox("CurbShadowLeft", stageRoot.transform, new Vector3(-4.6f, 0.007f, -2.55f), new Vector3(9.4f, 0.005f, 0.55f), 11f, shadeMat);
                MakeBox("CurbShadowRight", stageRoot.transform, new Vector3(4.6f, 0.007f, -2.55f), new Vector3(9.4f, 0.005f, 0.55f), -11f, shadeMat);
            }

            // 3. Real Filipino .glb Barangay Buildings with Kalyeah/BarangayBuilding Stucco Stand-In Fallbacks
            PlaceStageBuilding(stageRoot.transform, "nena_store", "CornerSariSari", new Vector3(0.05f, 0.14f, -4.55f), new Vector3(3.8f, 3.05f, 3.0f), 10f, paletteIndex: 0);
            PlaceStageBuilding(stageRoot.transform, "house_1", "RightHouseMain", new Vector3(4.85f, 0.14f, -5.85f), new Vector3(4.0f, 4.10f, 3.4f), -15f, paletteIndex: 3);
            PlaceStageBuilding(stageRoot.transform, "house_3", "RightHouseAccent", new Vector3(9.15f, 0.14f, -7.35f), new Vector3(3.9f, 3.85f, 3.4f), -22f, paletteIndex: 4);
            PlaceStageBuilding(stageRoot.transform, "house_2", "LeftHouseMain", new Vector3(-4.95f, 0.14f, -5.85f), new Vector3(4.0f, 3.70f, 3.4f), 16f, paletteIndex: 0);
            PlaceStageBuilding(stageRoot.transform, "house_1", "LeftHouseFar", new Vector3(-9.25f, 0.14f, -7.35f), new Vector3(3.9f, 3.55f, 3.3f), 22f, paletteIndex: 1);

            // Low sunlit concrete garden walls linking the corner storefronts cleanly
            MakeBox("CompoundWallLeft", stageRoot.transform, new Vector3(-2.65f, 0.78f, -5.05f), new Vector3(1.65f, 1.28f, 0.24f), 12f, wallMat);
            MakeBox("CompoundWallRight", stageRoot.transform, new Vector3(2.55f, 0.78f, -5.05f), new Vector3(1.55f, 1.28f, 0.24f), -12f, wallMat);

            // 4. Clean Filipino Concrete Utility Poles (placed symmetrically off-center so hero head & L3/L4 sky stay clear)
            BuildUtilityPole(stageRoot.transform, "UtilityPoleLeft", new Vector3(-3.15f, 0.14f, -3.65f), 5.6f, 10f, poleMat, armMat);
            BuildUtilityPole(stageRoot.transform, "UtilityPoleRight", new Vector3(3.35f, 0.14f, -3.75f), 5.6f, -10f, poleMat, armMat);

            return stageRoot;
        }

        void BuildSkyDome(Transform parent)
        {
            Material src = Resources.Load<Material>("KalyeahMaterials/TropicalSky");
            Shader skyShader = src != null && src.shader != null && src.shader.isSupported
                ? src.shader
                : Shader.Find("Kalyeah/TropicalSky");
            if (skyShader == null || !skyShader.isSupported) return;

            Material skyMat = src != null
                ? new Material(src) { name = "HeroStageTropicalSky", hideFlags = HideFlags.HideAndDontSave }
                : new Material(skyShader) { name = "HeroStageTropicalSky", hideFlags = HideFlags.HideAndDontSave };

            if (skyMat.HasProperty(PropSkyZenith)) skyMat.SetColor(PropSkyZenith, StreetSkyZenith);
            if (skyMat.HasProperty(PropSkyMid)) skyMat.SetColor(PropSkyMid, StreetSkyMid);
            if (skyMat.HasProperty(PropSkyHorizon)) skyMat.SetColor(PropSkyHorizon, StreetSkyHorizon);
            if (skyMat.HasProperty(PropHorizonGlow)) skyMat.SetColor(PropHorizonGlow, StreetSkyGlow);
            if (skyMat.HasProperty(PropGroundColor)) skyMat.SetColor(PropGroundColor, ConcreteRoadColor);
            if (skyMat.HasProperty(PropExposure)) skyMat.SetFloat(PropExposure, 1.0f);
            if (skyMat.HasProperty(PropHorizonClarityBand)) skyMat.SetFloat(PropHorizonClarityBand, 0.28f);
            _ownedObjects.Add(skyMat);

            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "SkyDome";
            dome.transform.SetParent(parent, false);
            dome.transform.localPosition = Vector3.zero;
            dome.transform.localScale = Vector3.one * 88f; // radius 44 m (inside camera farClipPlane = 65 m)
            StripColliders(dome);
            MeshRenderer mr = dome.GetComponent<MeshRenderer>();
            mr.sharedMaterial = skyMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void PlaceStageBuilding(Transform parent, string propId, string fallbackName, Vector3 localPos, Vector3 targetSize, float yawDeg, int paletteIndex)
        {
            GameObject prefab = KalyeahResources.Model("props", propId);
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = fallbackName + "_" + propId;
                KalyeahResources.SanitizeModelMaterials(instance);
                StripColliders(instance);

                Transform t = instance.transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;

                Bounds nat = KalyeahResources.RenderBounds(instance);
                if (nat.size.y > 0.05f)
                {
                    float scaleY = targetSize.y / nat.size.y;
                    float maxHoriz = Mathf.Max(nat.size.x, nat.size.z);
                    float scaleXZ = maxHoriz > 0.05f ? (Mathf.Max(targetSize.x, targetSize.z) * 1.25f) / maxHoriz : scaleY;
                    float uniform = Mathf.Clamp(Mathf.Min(scaleY, scaleXZ), 0.2f, 8f);
                    t.localScale = Vector3.one * uniform;
                }
                t.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
                t.localPosition = localPos;

                Bounds placed = KalyeahResources.RenderBounds(instance);
                if (placed.size.sqrMagnitude > 0.0001f)
                {
                    Vector3 worldTarget = parent.TransformPoint(localPos);
                    t.position += new Vector3(worldTarget.x - placed.center.x, worldTarget.y - placed.min.y, worldTarget.z - placed.center.z);
                }
                return;
            }

            // Crisp stylized Manila BarangayBuilding stucco + yero roof + awning stand-in fallback when .glb is absent
            BuildStuccoFallback(parent, fallbackName, localPos, targetSize, yawDeg, paletteIndex);
        }

        static void BuildStuccoFallback(Transform parent, string name, Vector3 localPos, Vector3 size, float yawDeg, int paletteIndex)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPos;
            host.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            Material wallMat = GreyBoxKit.GetBarangayBuildingMaterial(paletteIndex);
            Material roofMat = GreyBoxKit.GetRoofYeroMaterial();
            Material awningMat = GreyBoxKit.GetAwningMaterial(paletteIndex);

            float wallH = Mathf.Max(1.6f, size.y - 0.34f);
            MakeBox("Body", host.transform, new Vector3(0f, wallH * 0.5f, 0f), new Vector3(size.x, wallH, size.z), 0f, wallMat);
            MakeBox("YeroRoof", host.transform, new Vector3(0f, wallH + 0.14f, 0f), new Vector3(size.x + 0.36f, 0.18f, size.z + 0.42f), 0f, roofMat);

            GameObject awning = MakeBox("Awning", host.transform, new Vector3(0f, Mathf.Min(2.15f, wallH * 0.72f), size.z * 0.5f + 0.28f), new Vector3(size.x * 0.78f, 0.10f, 0.68f), 0f, awningMat);
            if (awning != null) awning.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        }

        static void BuildUtilityPole(Transform parent, string name, Vector3 localPos, float height, float yawDeg, Material poleMat, Material armMat)
        {
            var poleRoot = new GameObject(name);
            poleRoot.transform.SetParent(parent, false);
            poleRoot.transform.localPosition = localPos;
            poleRoot.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(poleRoot.transform, false);
            shaft.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            shaft.transform.localScale = new Vector3(0.14f, height * 0.5f, 0.14f);
            StripColliders(shaft);
            MeshRenderer mr = shaft.GetComponent<MeshRenderer>();
            if (poleMat != null) mr.sharedMaterial = poleMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            MakeBox("CrossArm", poleRoot.transform, new Vector3(0f, height - 0.45f, 0f), new Vector3(1.15f, 0.08f, 0.09f), 0f, armMat);
        }

        static GameObject MakeBox(string name, Transform parent, Vector3 localPos, Vector3 size, float yawDeg, Material mat)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            box.transform.localScale = size;
            StripColliders(box);
            MeshRenderer mr = box.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return box;
        }

        static void StripColliders(GameObject go)
        {
            if (go == null) return;
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                KalyeahResources.Kill(cols[i]);
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
            _baseRotation = Quaternion.Euler(0f, _showManilaStreetStage ? StreetTurnDeg : TurnTowardUiDeg, 0f) * FaceCamera(_model);
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
            if (_model == null) return $"HeroStage kid={_kidId} street={_showManilaStreetStage} model=null";
            Renderer[] renderers = _model.GetComponentsInChildren<Renderer>(true);
            Bounds b = KalyeahResources.RenderBounds(_model);
            Vector3 vp = _camera.WorldToViewportPoint(b.center);
            return $"HeroStage kid={_kidId} street={_showManilaStreetStage} renderers={renderers.Length} bounds={b.center}/{b.size} scale={_model.transform.lossyScale.x:F3} "
                + $"cam={_camera.transform.position} fwd={_camera.transform.forward} centreViewport={vp} feet={FeetViewportY:F3} "
                + $"rt={_texture.width}x{_texture.height} created={_texture.IsCreated()} pipeline={GraphicsSettings.currentRenderPipeline}";
        }

        public void Dispose()
        {
            if (_root != null) KalyeahResources.Kill(_root);
            for (int i = 0; i < _ownedObjects.Count; i++)
            {
                if (_ownedObjects[i] != null) KalyeahResources.Kill(_ownedObjects[i]);
            }
            _ownedObjects.Clear();
            if (_texture != null)
            {
                _texture.Release();
                KalyeahResources.Kill(_texture);
                _texture = null;
            }
        }
    }
}
