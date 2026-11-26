using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Read-only 3D world and puppet view for Kalyeah's 3 Filipino Larong Kalye street mini-games:
    ///   - Patintero: Barangay street court, dashed chalk grid lines, sidewalk jail strips, and active guard lane highlights.
    ///   - Tumbang Preso: Chalk toe/goal lines, chalk can circle, 3D tin can (models/weapons/can) with fling tumble,
    ///     6 distinct-color 3D slippers (models/weapons/slipper), local pickup ring, and hold-to-charge throw aim guide.
    ///   - Langit Lupa: Elevated crate platforms that sink/redden with wear, animated rising/falling earth mounds,
    ///     and periodic Brownout ambient dim with glowing curbside Meralco streetlamp cones + local lantern light.
    /// Kids use KidView with role-colored TeamOutline silhouettes and ZERO ground rings under any kid's feet.
    /// </summary>
    public sealed class StreetMatchView : IDisposable
    {
        public const float PlatformHeight = 0.60f;
        public const float WornHeightShare = 0.55f;
        public const float MoundHeight = 0.45f;
        public const float MoundRiseSeconds = 0.25f;

        const float ChalkLiftY = 0.030f;
        const float GlowLiftY = 0.022f;
        const float ChalkWidth = 0.28f;
        const float CanTargetHeight = 0.46f;
        const float SlipperTargetLength = 0.44f;
        const float CarryCanHeight = 0.96f;
        const float FlingDuration = 0.52f;
        const float FlingPeakHeight = 1.45f;

        // Warm sunset like the main match (owner, 2026-11-25): low golden sun under the shared sunset sky.
        static readonly Vector2 SunAngles = new Vector2(20f, 38f);
        static readonly Color DaylightSunColor = new Color(1.00f, 0.68f, 0.42f, 1f);
        const float DaylightSunIntensity = 1.35f;
        static readonly Vector2 FillAngles = new Vector2(34f, -142f);
        static readonly Color DaylightFillColor = new Color(0.56f, 0.52f, 0.86f, 1f);
        const float DaylightFillIntensity = 0.25f;
        static readonly Color DaylightAmbient = new Color(0.90f, 0.72f, 0.66f, 1f) * 0.55f;
        static readonly Color DaylightFog = new Color(0.98f, 0.72f, 0.52f, 1f);

        static readonly Color BrownoutSunColor = new Color(0.66f, 0.74f, 0.96f, 1f);
        static readonly Color BrownoutAmbient = new Color(0.30f, 0.34f, 0.48f, 1f);
        static readonly Color BrownoutFog = new Color(0.20f, 0.24f, 0.38f, 1f);
        const float BrownoutSunIntensity = 0.36f;
        const float BrownoutFillIntensity = 0.22f;

        static readonly Color ChalkWhite = new Color(1.00f, 0.99f, 0.94f, 0.92f);
        static readonly Color ChalkWarmRim = new Color(0.96f, 0.90f, 0.76f, 0.76f);
        static readonly Color ChalkDim = new Color(1.00f, 0.98f, 0.90f, 0.52f);
        static readonly Color ChalkGold = new Color(1.00f, 0.82f, 0.24f, 0.95f);
        static readonly Color ChalkDanger = new Color(1.00f, 0.34f, 0.24f, 0.92f);
        static readonly Color GuardGlowIdle = new Color(1.00f, 0.52f, 0.20f, 0.20f);
        static readonly Color GuardGlowHot = new Color(1.00f, 0.30f, 0.18f, 0.44f);
        static readonly Color LampWarmColor = new Color(1.00f, 0.82f, 0.44f, 1f);
        static readonly Color LampConeTint = new Color(1.00f, 0.85f, 0.48f, 0.26f);
        static readonly Color LampPoolTint = new Color(1.00f, 0.80f, 0.38f, 0.34f);

        // Verified synced .glb kit filenames in Assets/Imported/Resources/models/barangay/ & models/props/
        static readonly string[] StreetRowBuildings =
        {
            "brgy_sari_sari",
            "brgy_house_two_storey",
            "brgy_shophouse_row",
            "brgy_palengke_stall",
            "brgy_bakery_oven",
            "brgy_toda_shed",
            "brgy_tabi_cluster_a",
            "brgy_tabi_cluster_b",
        };

        static readonly string[] StreetRowGreenery =
        {
            "brgy_mango_tree",
            "brgy_coconut_palm",
            "brgy_banana_clump",
        };

        static readonly string[] SidewalkMicroProps =
        {
            "brgy_water_drum",
            "brgy_sorbetes_cart",
            "brgy_tricycle",
            "brgy_merienda_stand",
            "bench",
            "crates",
            "sacks",
        };

        static readonly Color32[] BanderitaColors =
        {
            new Color32(247, 168, 36, 255),  // Mango Gold
            new Color32(227, 75, 56, 255),   // Fiesta Red
            new Color32(36, 158, 139, 255),  // Jade Teal
            new Color32(45, 138, 229, 255),  // Sky Blue
            new Color32(250, 242, 225, 255), // Sunlit Cream
        };

        // Authentic 6-player Tumbang Preso slipper colors from street_mode_view.gd SLIPPER_COLOURS.
        static readonly Color[] SlipperColors =
        {
            new Color(0.90f, 0.22f, 0.21f, 1f), // #e53935 red
            new Color(0.12f, 0.53f, 0.90f, 1f), // #1e88e5 blue
            new Color(0.99f, 0.85f, 0.21f, 1f), // #fdd835 yellow
            new Color(0.26f, 0.63f, 0.28f, 1f), // #43a047 green
            new Color(0.98f, 0.55f, 0.00f, 1f), // #fb8c00 orange
            new Color(0.56f, 0.14f, 0.67f, 1f), // #8e24aa purple
        };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");

        sealed class GuardLaneVisual
        {
            public Transform Root;
            public Transform ChalkBar;
            public Transform GlowBar;
            public MeshRenderer GlowRenderer;
        }

        sealed class SlipperVisual
        {
            public GameObject Root;
            public Transform ModelPivot;
            public readonly List<Renderer> Renderers = new List<Renderer>(4);
            public float SpinClock;
        }

        sealed class PlatformVisual
        {
            public GameObject Root;
            public Transform Holder;
            public readonly MeshRenderer[] RimRenderers = new MeshRenderer[4];
            public GroundRect Bounds;
        }

        sealed class MoundVisual
        {
            public int OwnerId;
            public GameObject Root;
            public Transform Holder;
            public SVector2 Position;
            public float Radius;
            public float Rise01;
            public bool TargetUp;
        }

        sealed class StreetlampVisual
        {
            public GameObject Root;
            public MeshRenderer BulbRenderer;
            public MeshRenderer ConeRenderer;
            public MeshRenderer PoolRenderer;
            public Light PointLight;
        }

        public readonly Transform Root;
        public readonly StreetGameKind Kind;
        public readonly MapLayoutData Layout;

        readonly StreetBridge _bridge;
        readonly CameraRulesData _cameraRules;
        readonly float _courtHalfWidth;
        readonly float _courtHalfLength;
        readonly List<GameObject> _occluders = new List<GameObject>(96);
        readonly Dictionary<int, KidView> _kids = new Dictionary<int, KidView>(8);
        readonly Dictionary<string, Vector3> _naturalSizes = new Dictionary<string, Vector3>(32, StringComparer.Ordinal);
        readonly List<Object> _ownedAssets = new List<Object>(48);
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        readonly Material _opaqueLit;
        readonly Material _transparentUnlit;
        readonly Material _vertexColorUnlit;
        readonly Mesh _quadMesh;
        readonly Mesh _ringMesh;
        readonly Mesh _coneMesh;
        readonly Mesh _poolDiscMesh;

        Light _sunLight;
        Light _fillLight;
        Light _localLanternLight;
        MeshRenderer _localLanternPool;

        // Patintero visuals
        Transform _patinteroRoot;
        readonly List<GuardLaneVisual> _guardLanes = new List<GuardLaneVisual>(4);

        // Tumbang Preso visuals
        Transform _tumbangRoot;
        Transform _canRoot;
        Transform _canModelPivot;
        MeshRenderer _canCircleRenderer;
        MeshRenderer _localSlipperRingRenderer;
        Transform _localSlipperRing;
        Transform _aimLineTransform;
        MeshRenderer _aimLineRenderer;
        Transform _aimTipRingTransform;
        MeshRenderer _aimTipRingRenderer;
        readonly Dictionary<int, SlipperVisual> _slippers = new Dictionary<int, SlipperVisual>(8);
        SVector2 _canFlingFrom;
        SVector2 _canFlingTo;
        float _canFlingElapsed = FlingDuration + 1f;
        bool _canWasUp = true;
        bool _canInitialized;

        // Langit Lupa & Brownout visuals
        Transform _langitRoot;
        readonly List<PlatformVisual> _platforms = new List<PlatformVisual>(12);
        readonly Dictionary<int, MoundVisual> _mounds = new Dictionary<int, MoundVisual>(8);
        readonly List<int> _moundKeysScratch = new List<int>(8);
        readonly HashSet<int> _seenMoundOwners = new HashSet<int>();
        readonly List<StreetlampVisual> _streetlamps = new List<StreetlampVisual>(6);
        float _brownoutBlend;
        float _clock;

        public IReadOnlyList<GameObject> Occluders => _occluders;
        public bool? ForceBrownoutPreview { get; set; }
        public float CurrentBrownoutBlend => _brownoutBlend;
        public int ActivePlatformCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _platforms.Count; i++)
                    if (_platforms[i].Root != null && _platforms[i].Root.activeSelf) n++;
                return n;
            }
        }

        public GameObject CanObject => _canRoot != null ? _canRoot.gameObject : null;
        public int ActiveGuardLaneCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _guardLanes.Count; i++)
                    if (_guardLanes[i].Root != null && _guardLanes[i].Root.gameObject.activeSelf) n++;
                return n;
            }
        }

        public StreetMatchView(
            Transform parent,
            MapLayoutData layout,
            CameraRulesData cameraRules,
            StreetBridge bridge)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (cameraRules == null) throw new ArgumentNullException(nameof(cameraRules));
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));

            _bridge = bridge;
            Kind = bridge.Kind;
            Layout = layout;
            _cameraRules = cameraRules;
            _courtHalfWidth = ComputeCourtHalfWidth(bridge);
            _courtHalfLength = ComputeCourtHalfLength(bridge);

            Root = new GameObject($"StreetMatchView_{Kind}").transform;
            if (parent != null) Root.SetParent(parent, false);

            _opaqueLit = CreateSafeMaterial("Ground", "Universal Render Pipeline/Lit", transparent: false);
            _transparentUnlit = CreateSafeMaterial("TransparentUnlit", "Universal Render Pipeline/Unlit", transparent: true);
            _vertexColorUnlit = CreateVertexColorMaterial();
            _quadMesh = BuildHorizontalQuadMesh();
            _ringMesh = BuildAnnulusMesh(36, 0.86f, 1.0f);
            _coneMesh = BuildLampConeMesh(20, 0.22f, 3.2f, 4.6f);
            _poolDiscMesh = BuildSoftPoolDiscMesh(28);

            BuildLights();
            BuildBackdrop();
            BuildStreetSidewalksAndNeighborhood();
            BuildStreetlamps();
            BuildOverheadWiresAndBanderitas();
            BuildModeCourt(bridge);
        }

        static float ComputeCourtHalfWidth(StreetBridge bridge)
        {
            if (bridge.Kind == StreetGameKind.Patintero)
            {
                PatinteroSnapshot pat = bridge.PatinteroState;
                float halfW = pat.LineSpan > 1f ? pat.LineSpan * 0.5f : 0f;
                if (pat.GuardLines != null)
                {
                    for (int i = 0; i < pat.GuardLines.Count; i++)
                    {
                        PatinteroLineSnapshot gl = pat.GuardLines[i];
                        halfW = gl.IsVertical
                            ? Mathf.Max(halfW, Mathf.Abs(gl.Coordinate) + 3.5f)
                            : Mathf.Max(halfW, gl.SpanHalfExtent);
                    }
                }
                return Mathf.Clamp(halfW > 1f ? halfW : 12.5f, 11f, 14f);
            }

            if (bridge.Kind == StreetGameKind.TumbangPreso)
                return 12.5f;

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            float maxX = 12f;
            if (ll.Platforms != null)
            {
                for (int i = 0; i < ll.Platforms.Count; i++)
                {
                    GroundRect b = ll.Platforms[i].Bounds;
                    maxX = Mathf.Max(maxX, Mathf.Max(Mathf.Abs(b.X), Mathf.Abs(b.End.X)) + 3.5f);
                }
            }
            return Mathf.Clamp(maxX, 12f, 15.5f);
        }

        static float ComputeCourtHalfLength(StreetBridge bridge)
        {
            if (bridge.Kind == StreetGameKind.Patintero)
            {
                PatinteroSnapshot pat = bridge.PatinteroState;
                float span = Mathf.Max(Mathf.Abs(pat.HomeZ), Mathf.Abs(pat.GoalZ));
                return Mathf.Clamp((span > 1f ? span : 18f) + 4f, 16f, 28f);
            }

            if (bridge.Kind == StreetGameKind.TumbangPreso)
            {
                TumbangPresoSnapshot tp = bridge.TumbangPresoState;
                float span = Mathf.Max(Mathf.Abs(tp.HomeZ), Mathf.Abs(tp.CanSpot.Y) + 6f);
                return Mathf.Clamp((span > 1f ? span : 16f) + 4f, 16f, 28f);
            }

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            float maxZ = 15f;
            if (ll.Platforms != null)
            {
                for (int i = 0; i < ll.Platforms.Count; i++)
                {
                    GroundRect b = ll.Platforms[i].Bounds;
                    maxZ = Mathf.Max(maxZ, Mathf.Max(Mathf.Abs(b.Y), Mathf.Abs(b.End.Y)) + 4.5f);
                }
            }
            return Mathf.Clamp(maxZ, 15f, 24f);
        }

        // ---- Lighting & Barangay Backdrop ---------------------------------------------------------------------------------------

        void BuildLights()
        {
            _sunLight = new GameObject("Sun").AddComponent<Light>();
            _sunLight.transform.SetParent(Root, false);
            _sunLight.type = LightType.Directional;
            _sunLight.color = DaylightSunColor;
            _sunLight.intensity = DaylightSunIntensity;
            _sunLight.shadows = LightShadows.Soft;
            _sunLight.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(SunAngles.x, SunAngles.y)));

            _fillLight = new GameObject("Fill").AddComponent<Light>();
            _fillLight.transform.SetParent(Root, false);
            _fillLight.type = LightType.Directional;
            _fillLight.color = DaylightFillColor;
            _fillLight.intensity = DaylightFillIntensity;
            _fillLight.shadows = LightShadows.None;
            _fillLight.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(FillAngles.x, FillAngles.y)));

            _localLanternLight = new GameObject("LocalLanternLight").AddComponent<Light>();
            _localLanternLight.transform.SetParent(Root, false);
            _localLanternLight.type = LightType.Point;
            _localLanternLight.color = new Color(1.00f, 0.86f, 0.58f, 1f);
            _localLanternLight.range = 10f;
            _localLanternLight.intensity = 0f;
            _localLanternLight.shadows = LightShadows.None;
            _localLanternLight.enabled = false;

            var lanternPoolGo = new GameObject("LocalLanternPool", typeof(MeshFilter), typeof(MeshRenderer));
            lanternPoolGo.transform.SetParent(Root, false);
            lanternPoolGo.GetComponent<MeshFilter>().sharedMesh = _poolDiscMesh;
            _localLanternPool = lanternPoolGo.GetComponent<MeshRenderer>();
            ConfigureUnlitRenderer(_localLanternPool, _vertexColorUnlit ?? _transparentUnlit, LampPoolTint);
            _localLanternPool.enabled = false;

            RenderSettings.sun = _sunLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = DaylightAmbient;
            BarangayEnvironmentBuilder.ApplySky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = DaylightFog;
            RenderSettings.fogStartDistance = 76f;
            RenderSettings.fogEndDistance = 260f;
        }

        void BuildBackdrop()
        {
            // Open street court ground: uses BarangayGround's warm 0.22-0.38 asphalt shader with carveCreekSlot = false
            // so the center court (CanSpot at 0,0 and Patintero center line) is solid barrio pavement.
            MapLayoutData courtLayout = CloneStreetCourtLayout(Layout);
            GameObject groundGo = BarangayEnvironmentBuilder.BuildGround(Root, courtLayout);
            if (groundGo != null)
            {
                var mr = groundGo.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    BarangayEnvironmentBuilder.ConfigureGroundMaterial(mr.sharedMaterial, courtLayout, carveCreekSlot: false, isCreekBed: false);
                }
            }

            var kitsParent = new GameObject("BarangayBackdrop").transform;
            kitsParent.SetParent(Root, false);

            float clearX = _courtHalfWidth + 13.5f;
            float clearZ = _courtHalfLength + 13.5f;

            foreach (KitPlacement kit in BarangayPlan.Build(Layout))
            {
                if (kit.Kit == BarangayPlan.PostBlue || kit.Kit == BarangayPlan.PostRed || kit.Kit == BarangayPlan.PowerupStand)
                    continue;
                if (Mathf.Abs(kit.Ground.X) < clearX && Mathf.Abs(kit.Ground.Y) < clearZ)
                    continue;

                GameObject placed = PlaceKit(kitsParent, kit);
                if (placed != null && kit.Occluder)
                    _occluders.Add(placed);
            }
        }

        static MapLayoutData CloneStreetCourtLayout(MapLayoutData src)
        {
            return new MapLayoutData
            {
                ArenaRadius = src.ArenaRadius,
                BaseInset = src.BaseInset,
                BaseRadius = src.BaseRadius,
                BaseCorner = src.BaseCorner,
                SpawnOffset = src.SpawnOffset,
                BoundaryThickness = src.BoundaryThickness,
                CoverHeight = src.CoverHeight,
                PostHeight = src.PostHeight,
                PostWidthShare = src.PostWidthShare,
                SceneryScale = src.SceneryScale,
                RescueSize = src.RescueSize,
                Obstacles = Array.Empty<GroundRect>(),
                ObstacleYawsDeg = Array.Empty<float>(),
                ObstacleScales = Array.Empty<float>(),
                ObstacleProps = Array.Empty<string>(),
                PowerupSpots = Array.Empty<SVector2>(),
            };
        }

        GameObject PlaceKit(Transform parent, KitPlacement kit)
        {
            GameObject prefab = KalyeahResources.Model("barangay", kit.Kit);
            if (prefab == null) return GreyBoxKit.Build(parent, kit);

            GameObject model = Object.Instantiate(prefab, parent, false);
            model.name = kit.Kit;
            SanitizeModelMaterials(model);
            Transform t = model.transform;
            Vector3 natural = MeasureNaturalSize("barangay/" + kit.Kit, model);
            t.localScale = KalyeahResources.U(kit.ScaleFor(new SVector3(natural.x, natural.y, natural.z)));
            t.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);
            Vector3 spot = KalyeahResources.World(kit.Ground);
            t.position = spot;
            if (kit.Fit == KitFit.Footprint)
            {
                Bounds placed = KalyeahResources.RenderBounds(model);
                t.position += new Vector3(spot.x - placed.center.x, kit.Lift - placed.min.y, spot.z - placed.center.z);
            }
            return model;
        }

        float[] GetStreetPoleZs()
        {
            float zOuter = Mathf.Max(11f, _courtHalfLength * 0.78f);
            float zInner = Mathf.Max(3.8f, _courtHalfLength * 0.26f);
            return new[] { -zOuter, -zInner, zInner, zOuter };
        }

        void BuildStreetSidewalksAndNeighborhood()
        {
            var enclosureRoot = new GameObject("StreetNeighborhoodEnclosure").transform;
            enclosureRoot.SetParent(Root, false);

            float curbInnerX = _courtHalfWidth + 1.8f;
            float sidewalkWidth = 4.6f;
            float sidewalkHeight = 0.14f;
            float streetHalfZ = _courtHalfLength + 6.5f;
            float sidewalkLen = streetHalfZ * 2f;

            Color sidewalkTopColor = new Color(0.47f, 0.45f, 0.41f, 1f);
            Color curbOchre = new Color(0.78f, 0.63f, 0.25f, 1f);
            Color curbCream = new Color(0.84f, 0.81f, 0.74f, 1f);

            // 1. Raised concrete sidewalks & alternating barangay yellow-ochre / white curb lips right at ±(_courtHalfWidth + 1.8f)
            for (int side = -1; side <= 1; side += 2)
            {
                float centerX = side * (curbInnerX + sidewalkWidth * 0.5f);
                GameObject slab = CreatePrimitiveNoCollider(PrimitiveType.Cube, side < 0 ? "WestSidewalk" : "EastSidewalk", enclosureRoot);
                slab.transform.position = KalyeahResources.World(new SVector2(centerX, 0f), sidewalkHeight * 0.5f);
                slab.transform.localScale = new Vector3(sidewalkWidth, sidewalkHeight, sidewalkLen);
                ApplyColor(slab.GetComponent<MeshRenderer>(), _opaqueLit, sidewalkTopColor);

                int stripeIndex = 0;
                for (float z = -streetHalfZ + 2.25f; z <= streetHalfZ - 2.25f; z += 4.5f, stripeIndex++)
                {
                    float noseX = side * (curbInnerX + 0.16f);
                    GameObject nose = CreatePrimitiveNoCollider(PrimitiveType.Cube, $"CurbNose_{side}_{stripeIndex}", enclosureRoot);
                    nose.transform.position = KalyeahResources.World(new SVector2(noseX, z), sidewalkHeight + 0.012f);
                    nose.transform.localScale = new Vector3(0.32f, 0.035f, 4.35f);
                    ApplyColor(nose.GetComponent<MeshRenderer>(), _opaqueLit, (stripeIndex & 1) == 0 ? curbOchre : curbCream);
                }
            }

            // 2. Close-in barangay storefronts & tropical foliage right at ±(_courtHalfWidth + 7.2f)
            float rowX = _courtHalfWidth + 7.2f;
            int rowCount = Mathf.Clamp(Mathf.RoundToInt((streetHalfZ * 2f) / 8.8f), 5, 8);
            float rowSpan = Mathf.Max(24f, (streetHalfZ - 2.5f) * 2f);
            float rowStep = rowCount > 1 ? rowSpan / (rowCount - 1) : 9f;

            for (int side = -1; side <= 1; side += 2)
            {
                float inwardGodotYaw = side < 0 ? (Mathf.PI * 0.5f) : (-Mathf.PI * 0.5f);
                for (int i = 0; i < rowCount; i++)
                {
                    string kitId = StreetRowBuildings[(i + (side > 0 ? 3 : 0)) % StreetRowBuildings.Length];
                    float z = -rowSpan * 0.5f + i * rowStep + (side > 0 ? -0.9f : 0.9f);
                    GameObject house = PlaceNeighborhoodKit(
                        enclosureRoot,
                        kitId,
                        new SVector2(side * rowX, z),
                        inwardGodotYaw,
                        new Vector3(8.2f, 6.4f, 8.2f),
                        side < 0 ? new Color(0.86f, 0.72f, 0.52f, 1f) : new Color(0.76f, 0.82f, 0.74f, 1f));
                    if (house != null)
                        _occluders.Add(house);

                    // Pocket street greenery between storefronts to soften roof silhouettes
                    if (i < rowCount - 1)
                    {
                        string plantId = StreetRowGreenery[(i + (side > 0 ? 1 : 0)) % StreetRowGreenery.Length];
                        float plantZ = z + rowStep * 0.5f;
                        PlaceNeighborhoodKit(
                            enclosureRoot,
                            plantId,
                            new SVector2(side * (curbInnerX + 3.8f), plantZ),
                            (i * 1.1f) + side,
                            new Vector3(3.8f, 5.6f, 3.8f),
                            new Color(0.28f, 0.52f, 0.26f, 1f));
                    }
                }
            }

            // Cross-street end-cap storefronts right at ±(_courtHalfLength + 7.5f) so camera horizon sees authentic barrio buildings
            float endZ = _courtHalfLength + 7.5f;
            float[] endXs = { -11.4f, -3.8f, 3.8f, 11.4f };
            for (int dir = -1; dir <= 1; dir += 2)
            {
                float endYaw = dir < 0 ? 0f : Mathf.PI;
                for (int i = 0; i < endXs.Length; i++)
                {
                    string kitId = StreetRowBuildings[(i * 2 + (dir > 0 ? 1 : 0)) % StreetRowBuildings.Length];
                    GameObject cap = PlaceNeighborhoodKit(
                        enclosureRoot,
                        kitId,
                        new SVector2(endXs[i], dir * endZ),
                        endYaw,
                        new Vector3(8.0f, 6.4f, 7.6f),
                        new Color(0.84f, 0.76f, 0.62f, 1f));
                    if (cap != null)
                        _occluders.Add(cap);
                }
            }

            // 3. Curbside barrio micro-props along the raised concrete sidewalks (strictly outside court lines)
            float propSpan = Mathf.Max(16f, _courtHalfLength * 0.82f);
            float[] propZs = { -propSpan, -propSpan * 0.45f, 0f, propSpan * 0.45f, propSpan };
            for (int side = -1; side <= 1; side += 2)
            {
                float inwardGodotYaw = side < 0 ? (Mathf.PI * 0.5f) : (-Mathf.PI * 0.5f);
                for (int i = 0; i < propZs.Length; i++)
                {
                    string propId = SidewalkMicroProps[(i + (side > 0 ? 2 : 0)) % SidewalkMicroProps.Length];
                    float px = side * (curbInnerX + 1.55f + (i % 2) * 0.55f);
                    PlaceNeighborhoodKit(
                        enclosureRoot,
                        propId,
                        new SVector2(px, propZs[i] + side * 1.4f),
                        inwardGodotYaw,
                        MicroPropTargetSize(propId),
                        MicroPropFallbackColor(propId),
                        liftY: sidewalkHeight);
                }
            }
        }

        GameObject PlaceNeighborhoodKit(
            Transform parent,
            string kitId,
            SVector2 groundPos,
            float godotYawRad,
            Vector3 targetFootprint,
            Color standInColor,
            float liftY = 0f)
        {
            string folder = "barangay";
            GameObject prefab = KalyeahResources.Model("barangay", kitId);
            if (prefab == null)
            {
                folder = "props";
                prefab = KalyeahResources.Model("props", kitId);
            }

            Vector3 worldSpot = KalyeahResources.World(groundPos, liftY);
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, parent, false);
                model.name = kitId;
                SanitizeModelMaterials(model);
                Vector3 nat = MeasureNaturalSize(folder + "/" + kitId, model);
                float sx = targetFootprint.x / Mathf.Max(nat.x, 0.25f);
                float sy = targetFootprint.y / Mathf.Max(nat.y, 0.25f);
                float sz = targetFootprint.z / Mathf.Max(nat.z, 0.25f);
                float uniform = Mathf.Clamp(Mathf.Min(sx, Mathf.Min(sy, sz)), 0.25f, 8.5f);
                model.transform.localScale = Vector3.one * uniform;
                model.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYawRad), 0f);
                model.transform.position = worldSpot;
                Bounds b = KalyeahResources.RenderBounds(model);
                model.transform.position += new Vector3(worldSpot.x - b.center.x, worldSpot.y - b.min.y, worldSpot.z - b.center.z);
                return model;
            }

            GameObject standIn = CreatePrimitiveNoCollider(PrimitiveType.Cube, kitId + "_StandIn", parent);
            standIn.transform.position = worldSpot + Vector3.up * (targetFootprint.y * 0.5f);
            standIn.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYawRad), 0f);
            standIn.transform.localScale = targetFootprint;
            ApplyColor(standIn.GetComponent<MeshRenderer>(), _opaqueLit, standInColor);
            return standIn;
        }

        static Vector3 MicroPropTargetSize(string propId)
        {
            switch (propId)
            {
                case "brgy_water_drum": return new Vector3(0.78f, 1.08f, 0.78f);
                case "brgy_sorbetes_cart": return new Vector3(1.65f, 1.55f, 1.15f);
                case "brgy_tricycle": return new Vector3(2.15f, 1.65f, 1.45f);
                case "brgy_merienda_stand": return new Vector3(1.75f, 1.85f, 1.25f);
                case "bench": return new Vector3(1.55f, 0.68f, 0.62f);
                case "sacks": return new Vector3(1.10f, 0.78f, 0.95f);
                default: return new Vector3(1.05f, 0.82f, 0.95f);
            }
        }

        static Color MicroPropFallbackColor(string propId)
        {
            switch (propId)
            {
                case "brgy_water_drum": return new Color(0.18f, 0.48f, 0.82f, 1f);
                case "brgy_sorbetes_cart": return new Color(0.92f, 0.68f, 0.20f, 1f);
                case "brgy_tricycle": return new Color(0.80f, 0.24f, 0.20f, 1f);
                case "brgy_merienda_stand": return new Color(0.76f, 0.52f, 0.28f, 1f);
                default: return new Color(0.66f, 0.49f, 0.30f, 1f);
            }
        }

        void BuildStreetlamps()
        {
            var lampsRoot = new GameObject("CurbsideStreetlamps").transform;
            lampsRoot.SetParent(Root, false);

            float lampX = _courtHalfWidth + 2.4f;
            float[] lampZs = GetStreetPoleZs();

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < lampZs.Length; i++)
                {
                    var pos = new SVector2(side * lampX, lampZs[i]);
                    StreetlampVisual vis = CreateStreetlamp(lampsRoot, pos, side < 0 ? 90f : -90f, _streetlamps.Count);
                    _streetlamps.Add(vis);
                    if (vis.Root != null)
                        _occluders.Add(vis.Root);
                }
            }
        }

        StreetlampVisual CreateStreetlamp(Transform parent, SVector2 groundPos, float armYawDeg, int index)
        {
            var lampRoot = new GameObject($"Streetlamp_{index}");
            lampRoot.transform.SetParent(parent, false);
            lampRoot.transform.position = KalyeahResources.World(groundPos, 0.14f);
            lampRoot.transform.rotation = Quaternion.Euler(0f, armYawDeg, 0f);

            const float poleHeight = 5.6f;
            GameObject polePrefab = KalyeahResources.Model("barangay", "brgy_meralco_post");
            if (polePrefab != null)
            {
                GameObject pole = Object.Instantiate(polePrefab, lampRoot.transform, false);
                pole.name = "MeralcoPole";
                SanitizeModelMaterials(pole);
                Vector3 nat = MeasureNaturalSize("barangay/brgy_meralco_post", pole);
                float s = poleHeight / Mathf.Max(nat.y, 0.2f);
                pole.transform.localScale = new Vector3(s, s, s);
                Bounds b = KalyeahResources.RenderBounds(pole);
                pole.transform.position += new Vector3(
                    lampRoot.transform.position.x - b.center.x,
                    lampRoot.transform.position.y - b.min.y,
                    lampRoot.transform.position.z - b.center.z);
            }
            else
            {
                GameObject post = CreatePrimitiveNoCollider(PrimitiveType.Cylinder, "PoleStandIn", lampRoot.transform);
                post.transform.localPosition = new Vector3(0f, poleHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.18f, poleHeight * 0.5f, 0.18f);
                ApplyColor(post.GetComponent<MeshRenderer>(), _opaqueLit, new Color(0.36f, 0.34f, 0.31f, 1f));
            }

            // Overhanging barrio capiz/metal lamp arm + bulb + volumetric cone + ground light pool
            GameObject arm = CreatePrimitiveNoCollider(PrimitiveType.Cube, "LampArm", lampRoot.transform);
            arm.transform.localPosition = new Vector3(0f, poleHeight - 0.35f, 0.85f);
            arm.transform.localScale = new Vector3(0.08f, 0.08f, 1.75f);
            ApplyColor(arm.GetComponent<MeshRenderer>(), _opaqueLit, new Color(0.24f, 0.25f, 0.28f, 1f));

            Vector3 headLocal = new Vector3(0f, poleHeight - 0.45f, 1.65f);
            GameObject bulb = CreatePrimitiveNoCollider(PrimitiveType.Sphere, "LampBulb", lampRoot.transform);
            bulb.transform.localPosition = headLocal;
            bulb.transform.localScale = new Vector3(0.34f, 0.24f, 0.34f);
            var bulbRenderer = bulb.GetComponent<MeshRenderer>();
            ApplyColor(bulbRenderer, _transparentUnlit, new Color(0.88f, 0.84f, 0.72f, 0.72f));

            var coneGo = new GameObject("StreetlampCone", typeof(MeshFilter), typeof(MeshRenderer));
            coneGo.transform.SetParent(lampRoot.transform, false);
            coneGo.transform.localPosition = headLocal;
            coneGo.GetComponent<MeshFilter>().sharedMesh = _coneMesh;
            var coneRenderer = coneGo.GetComponent<MeshRenderer>();
            ConfigureUnlitRenderer(coneRenderer, _vertexColorUnlit ?? _transparentUnlit, LampConeTint);
            coneRenderer.enabled = false;

            var poolGo = new GameObject("LampGroundPool", typeof(MeshFilter), typeof(MeshRenderer));
            poolGo.transform.SetParent(lampRoot.transform, false);
            poolGo.transform.localPosition = new Vector3(0f, -0.122f, 1.85f);
            poolGo.transform.localScale = new Vector3(9.5f, 1f, 9.5f);
            poolGo.GetComponent<MeshFilter>().sharedMesh = _poolDiscMesh;
            var poolRenderer = poolGo.GetComponent<MeshRenderer>();
            ConfigureUnlitRenderer(poolRenderer, _vertexColorUnlit ?? _transparentUnlit, LampPoolTint);
            poolRenderer.enabled = false;

            var pointLight = new GameObject("LampLight").AddComponent<Light>();
            pointLight.transform.SetParent(lampRoot.transform, false);
            pointLight.transform.localPosition = headLocal + Vector3.down * 0.25f;
            pointLight.type = LightType.Point;
            pointLight.color = LampWarmColor;
            pointLight.range = 16.5f;
            pointLight.intensity = 0f;
            pointLight.shadows = LightShadows.None;
            pointLight.enabled = false;

            return new StreetlampVisual
            {
                Root = lampRoot,
                BulbRenderer = bulbRenderer,
                ConeRenderer = coneRenderer,
                PoolRenderer = poolRenderer,
                PointLight = pointLight,
            };
        }

        void BuildOverheadWiresAndBanderitas()
        {
            var verts = new List<Vector3>(1024);
            var colors = new List<Color32>(1024);
            var tris = new List<int>(2048);

            // Anchor every longitudinal cable and cross-street banderita directly onto the visible Meralco poles at ±(_courtHalfWidth + 2.4f)
            float poleX = _courtHalfWidth + 2.4f;
            float[] poleZs = GetStreetPoleZs();
            Color32 wireInk = new Color32(34, 30, 29, 238);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int seg = 0; seg < poleZs.Length - 1; seg++)
                {
                    Vector3 a = KalyeahResources.World(new SVector2(side * poleX, poleZs[seg]), 5.28f);
                    Vector3 b = KalyeahResources.World(new SVector2(side * poleX, poleZs[seg + 1]), 5.28f);
                    AppendCatenaryRibbon(verts, colors, tris, a + Vector3.up * 0.20f, b + Vector3.up * 0.20f, 0.30f, 0.030f, wireInk, false);
                    AppendCatenaryRibbon(verts, colors, tris, a, b, 0.36f, 0.026f, wireInk, false);
                    AppendCatenaryRibbon(verts, colors, tris, a + Vector3.right * (side * 0.20f) - Vector3.up * 0.14f, b + Vector3.right * (side * 0.20f) - Vector3.up * 0.14f, 0.32f, 0.022f, wireInk, false);
                }
            }

            // Cross-street catenary utility lines + festive fiesta banderitas strung pole-to-pole across the court (y >= 4.9 m)
            for (int i = 0; i < poleZs.Length; i++)
            {
                Vector3 west = KalyeahResources.World(new SVector2(-poleX, poleZs[i]), 5.45f);
                Vector3 east = KalyeahResources.World(new SVector2(poleX, poleZs[i]), 5.45f);
                AppendCatenaryRibbon(verts, colors, tris, west, east, 0.48f, 0.026f, wireInk, true);
                if (i < poleZs.Length - 1)
                {
                    Vector3 diagEast = KalyeahResources.World(new SVector2(poleX, poleZs[i + 1]), 5.42f);
                    AppendCatenaryRibbon(verts, colors, tris, west + Vector3.up * 0.12f, diagEast + Vector3.up * 0.12f, 0.44f, 0.022f, wireInk, true);
                }
            }

            var mesh = new Mesh { name = "StreetOverheadWiresAndBanderitas" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);

            var go = new GameObject("SpaghettiWiresAndBanderitas", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(Root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (_vertexColorUnlit != null) mr.sharedMaterial = _vertexColorUnlit;
        }

        static void AppendCatenaryRibbon(
            List<Vector3> verts,
            List<Color32> colors,
            List<int> tris,
            Vector3 start,
            Vector3 end,
            float sag,
            float thickness,
            Color32 wireColor,
            bool withBanderitas)
        {
            const int steps = 16;
            Vector3 prev = start;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 cur = Vector3.Lerp(start, end, t);
                cur.y -= 4f * t * (1f - t) * sag;

                // Double-sided vertical + horizontal ribbon cross so thin cables read cleanly from shoulder view
                Vector3 halfUp = Vector3.up * (thickness * 0.5f);
                AddDoubleQuad(verts, colors, tris, prev - halfUp, prev + halfUp, cur + halfUp, cur - halfUp, wireColor);

                if (withBanderitas && i >= 2 && i <= steps - 1)
                {
                    Vector3 mid = (prev + cur) * 0.5f;
                    Vector3 tangent = (cur - prev).normalized;
                    Vector3 spanHalf = tangent * 0.24f;
                    Vector3 tip = mid + Vector3.down * 0.42f;
                    Color32 flagCol = BanderitaColors[(i + Mathf.Abs(Mathf.RoundToInt(start.z))) % BanderitaColors.Length];
                    AddDoubleTri(verts, colors, tris, mid - spanHalf, mid + spanHalf, tip, flagCol);
                }

                prev = cur;
            }
        }

        static void AddDoubleQuad(List<Vector3> verts, List<Color32> colors, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color32 col)
        {
            int b = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
        }

        static void AddDoubleTri(List<Vector3> verts, List<Color32> colors, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Color32 col)
        {
            int b = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2);
            colors.Add(col); colors.Add(col); colors.Add(col);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
        }

        // ---- Mode Court Builders ------------------------------------------------------------------------------------------------

        void BuildModeCourt(StreetBridge bridge)
        {
            if (Kind == StreetGameKind.Patintero)
                BuildPatinteroCourt(bridge.PatinteroState);
            else if (Kind == StreetGameKind.TumbangPreso)
                BuildTumbangPresoCourt(bridge);
            else if (Kind == StreetGameKind.LangitLupa)
                BuildLangitLupaCourt(bridge.LangitLupaState);
        }

        void BuildPatinteroCourt(PatinteroSnapshot pat)
        {
            _patinteroRoot = new GameObject("PatinteroCourt").transform;
            _patinteroRoot.SetParent(Root, false);

            float homeZ = Mathf.Abs(pat.HomeZ) > 0.1f ? pat.HomeZ : 21.5f;
            float goalZ = Mathf.Abs(pat.GoalZ) > 0.1f ? pat.GoalZ : -21.5f;
            float spanZ = Mathf.Abs(homeZ - goalZ);
            float midZ = (homeZ + goalZ) * 0.5f;
            float width = _courtHalfWidth * 2f;

            // Dashed chalk boundary lines (Home, Goal, Left/Right Sidelines, and Sidewalk Jail strips)
            BuildDashedChalkLine(_patinteroRoot, "HomeLine", new SVector2(0f, homeZ), width, vertical: false, ChalkWhite, 11);
            BuildDashedChalkLine(_patinteroRoot, "GoalLine", new SVector2(0f, goalZ), width, vertical: false, ChalkGold, 11);
            BuildDashedChalkLine(_patinteroRoot, "WestSideline", new SVector2(-_courtHalfWidth, midZ), spanZ, vertical: true, ChalkDim, 13);
            BuildDashedChalkLine(_patinteroRoot, "EastSideline", new SVector2(_courtHalfWidth, midZ), spanZ, vertical: true, ChalkDim, 13);

            float jailX = _courtHalfWidth + 2.7f;
            CreateFlatQuad(_patinteroRoot, "SidewalkJailWest", new SVector2(-jailX, 0f), new Vector2(1.6f, 10f), 0.14f + GlowLiftY, new Color(1f, 0.36f, 0.24f, 0.20f));
            CreateFlatQuad(_patinteroRoot, "SidewalkJailEast", new SVector2(jailX, 0f), new Vector2(1.6f, 10f), 0.14f + GlowLiftY, new Color(1f, 0.36f, 0.24f, 0.20f));

            // Preallocate up to 4 dynamic guard lanes synced from PatinteroState.GuardLines
            for (int i = 0; i < 4; i++)
            {
                var laneRoot = new GameObject($"GuardLane_{i}").transform;
                laneRoot.SetParent(_patinteroRoot, false);

                MeshRenderer glowMr = CreateFlatQuad(laneRoot, "ActiveLaneGlow", SVector2.Zero, Vector2.one, GlowLiftY, GuardGlowIdle);
                MeshRenderer chalkMr = CreateFlatQuad(laneRoot, "ChalkLine", SVector2.Zero, Vector2.one, ChalkLiftY, ChalkWhite);

                _guardLanes.Add(new GuardLaneVisual
                {
                    Root = laneRoot,
                    ChalkBar = chalkMr.transform,
                    GlowBar = glowMr.transform,
                    GlowRenderer = glowMr,
                });
                laneRoot.gameObject.SetActive(false);
            }
        }

        void BuildTumbangPresoCourt(StreetBridge bridge)
        {
            TumbangPresoSnapshot tp = bridge.TumbangPresoState;
            _tumbangRoot = new GameObject("TumbangPresoCourt").transform;
            _tumbangRoot.SetParent(Root, false);

            float maxCourtZ = Mathf.Max(8f, _courtHalfLength - 1.5f);
            float homeZ = Mathf.Clamp(Mathf.Abs(tp.HomeZ) > 0.1f ? tp.HomeZ : 16f, -maxCourtZ, maxCourtZ);
            float goalZ = Mathf.Clamp(Mathf.Abs(tp.GoalZ) > 0.1f ? tp.GoalZ : (tp.CanSpot.Y - 8f), -maxCourtZ, maxCourtZ);
            float width = _courtHalfWidth * 2f;

            // Toe-line (HomeZ) and far boundary (GoalZ)
            BuildDashedChalkLine(_tumbangRoot, "ThrowToeLine", new SVector2(0f, homeZ), width, vertical: false, ChalkWhite, 11);
            BuildDashedChalkLine(_tumbangRoot, "FarBackLine", new SVector2(0f, goalZ), width, vertical: false, ChalkDim, 11);

            // Chalk circle around CanSpot (where the lata tin can stands)
            float circleDiameter = Mathf.Max(0.8f, tp.CanHitRadius * 2f) * 2.1f;
            _canCircleRenderer = CreateRing(_tumbangRoot, "CanChalkCircle", tp.CanSpot, circleDiameter, ChalkLiftY, ChalkWhite);

            // 3D Tin Can (models/weapons/can)
            _canRoot = new GameObject("TinCan").transform;
            _canRoot.SetParent(_tumbangRoot, false);
            _canModelPivot = new GameObject("CanPivot").transform;
            _canModelPivot.SetParent(_canRoot, false);

            float scaledCanH = CanTargetHeight * Mathf.Max(0.5f, _cameraRules.KidViewScale);
            GameObject canPrefab = KalyeahResources.Model("weapons", "can");
            if (canPrefab != null)
            {
                GameObject canInst = Object.Instantiate(canPrefab, _canModelPivot, false);
                canInst.name = "CanModel";
                SanitizeModelMaterials(canInst);
                Vector3 nat = MeasureNaturalSize("weapons/can", canInst);
                float s = scaledCanH / Mathf.Max(nat.y, 0.05f);
                canInst.transform.localScale = Vector3.one * s;
                Bounds b = KalyeahResources.RenderBounds(canInst);
                canInst.transform.localPosition += Vector3.up * (_canModelPivot.position.y - b.min.y);
            }
            else
            {
                GameObject standIn = CreatePrimitiveNoCollider(PrimitiveType.Cylinder, "CanStandIn", _canModelPivot);
                standIn.transform.localScale = new Vector3(scaledCanH * 0.56f, scaledCanH * 0.5f, scaledCanH * 0.56f);
                standIn.transform.localPosition = new Vector3(0f, scaledCanH * 0.5f, 0f);
                ApplyColor(standIn.GetComponent<MeshRenderer>(), _opaqueLit, new Color(0.84f, 0.87f, 0.91f, 1f));
            }

            // Local slipper pickup ring (sits around the grounded slipper, never under a kid!)
            _localSlipperRingRenderer = CreateRing(_tumbangRoot, "OwnSlipperRing", SVector2.Zero, 1.35f, ChalkLiftY + 0.005f, ChalkWhite);
            _localSlipperRing = _localSlipperRingRenderer.transform;
            _localSlipperRing.gameObject.SetActive(false);

            // Local hold-to-charge throw aim strip + landing ring
            _aimLineRenderer = CreateFlatQuad(_tumbangRoot, "ThrowAimLine", SVector2.Zero, new Vector2(0.42f, 1f), ChalkLiftY + 0.008f, ChalkWhite);
            _aimLineTransform = _aimLineRenderer.transform;
            _aimLineTransform.gameObject.SetActive(false);

            _aimTipRingRenderer = CreateRing(_tumbangRoot, "ThrowAimTip", SVector2.Zero, 1.3f, ChalkLiftY + 0.010f, ChalkGold);
            _aimTipRingTransform = _aimTipRingRenderer.transform;
            _aimTipRingTransform.gameObject.SetActive(false);

            bridge.CanFlung += OnCanFlung;
        }

        void OnCanFlung(SVector2 from)
        {
            _canFlingFrom = from;
            _canFlingElapsed = 0f;
        }

        void BuildLangitLupaCourt(LangitLupaSnapshot ll)
        {
            _langitRoot = new GameObject("LangitLupaCourt").transform;
            _langitRoot.SetParent(Root, false);

            if (ll.Platforms == null) return;
            for (int i = 0; i < ll.Platforms.Count; i++)
            {
                EnsurePlatformVisual(i, ll.Platforms[i].Bounds);
            }
        }

        PlatformVisual EnsurePlatformVisual(int index, GroundRect bounds)
        {
            while (_platforms.Count <= index)
            {
                int idx = _platforms.Count;
                GroundRect rect = idx == index ? bounds : new GroundRect(-2.4f, -2.4f, 4.8f, 4.8f);
                var platRoot = new GameObject($"LangitPlatform_{idx}");
                platRoot.transform.SetParent(_langitRoot, false);
                platRoot.transform.position = KalyeahResources.World(rect.Center, 0f);

                var holder = new GameObject("Holder").transform;
                holder.SetParent(platRoot.transform, false);

                FitPropOrStandIn(
                    holder,
                    "crates",
                    new Vector3(Mathf.Max(0.8f, rect.W), PlatformHeight, Mathf.Max(0.8f, rect.H)),
                    PrimitiveType.Cube,
                    new Color(0.66f, 0.49f, 0.30f, 1f));

                // Hollow 4-strip 0.12 m warm-chalk perimeter border around platform base (never a solid white slab)
                const float rimWidth = 0.12f;
                const float rimGap = 0.08f;
                float halfW = rect.W * 0.5f + rimGap;
                float halfH = rect.H * 0.5f + rimGap;

                var created = new PlatformVisual
                {
                    Root = platRoot,
                    Holder = holder,
                    Bounds = rect,
                };
                created.RimRenderers[0] = CreateFlatQuad(platRoot.transform, "ChalkRimNorth", new SVector2(0f, -halfH), new Vector2(rect.W + (rimGap + rimWidth * 0.5f) * 2f, rimWidth), 0.018f, ChalkWarmRim, localSpace: true);
                created.RimRenderers[1] = CreateFlatQuad(platRoot.transform, "ChalkRimSouth", new SVector2(0f, halfH), new Vector2(rect.W + (rimGap + rimWidth * 0.5f) * 2f, rimWidth), 0.018f, ChalkWarmRim, localSpace: true);
                created.RimRenderers[2] = CreateFlatQuad(platRoot.transform, "ChalkRimWest", new SVector2(-halfW, 0f), new Vector2(rimWidth, rect.H + rimGap * 2f - rimWidth), 0.018f, ChalkWarmRim, localSpace: true);
                created.RimRenderers[3] = CreateFlatQuad(platRoot.transform, "ChalkRimEast", new SVector2(halfW, 0f), new Vector2(rimWidth, rect.H + rimGap * 2f - rimWidth), 0.018f, ChalkWarmRim, localSpace: true);

                _platforms.Add(created);
            }

            PlatformVisual pv = _platforms[index];
            pv.Bounds = bounds;
            pv.Root.transform.position = KalyeahResources.World(bounds.Center, 0f);
            return pv;
        }

        MoundVisual EnsureMoundVisual(int ownerId, float radius)
        {
            if (_mounds.TryGetValue(ownerId, out MoundVisual existing)) return existing;

            var moundRoot = new GameObject($"LupaMound_{ownerId}");
            moundRoot.transform.SetParent(_langitRoot != null ? _langitRoot : Root, false);

            var holder = new GameObject("Holder").transform;
            holder.SetParent(moundRoot.transform, false);

            float d = Mathf.Max(0.8f, radius * 2f);
            FitPropOrStandIn(
                holder,
                "sacks",
                new Vector3(d, MoundHeight, d),
                PrimitiveType.Cylinder,
                new Color(0.54f, 0.38f, 0.23f, 1f));

            var created = new MoundVisual
            {
                OwnerId = ownerId,
                Root = moundRoot,
                Holder = holder,
                Radius = radius,
                Rise01 = 0f,
                TargetUp = true,
            };
            _mounds[ownerId] = created;
            return created;
        }

        void FitPropOrStandIn(Transform holder, string propId, Vector3 targetSize, PrimitiveType fallbackShape, Color fallbackColor)
        {
            GameObject prefab = KalyeahResources.Model("props", propId);
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, holder, false);
                model.name = "Model";
                SanitizeModelMaterials(model);
                Vector3 nat = MeasureNaturalSize("props/" + propId, model);
                model.transform.localScale = new Vector3(
                    targetSize.x / Mathf.Max(nat.x, 0.1f),
                    targetSize.y / Mathf.Max(nat.y, 0.1f),
                    targetSize.z / Mathf.Max(nat.z, 0.1f));
                Bounds b = KalyeahResources.RenderBounds(model);
                model.transform.position += new Vector3(
                    holder.position.x - b.center.x,
                    holder.position.y - b.min.y,
                    holder.position.z - b.center.z);
            }
            else
            {
                GameObject standIn = CreatePrimitiveNoCollider(fallbackShape, "ModelStandIn", holder);
                float yScale = fallbackShape == PrimitiveType.Cylinder ? targetSize.y * 0.5f : targetSize.y;
                standIn.transform.localScale = new Vector3(targetSize.x, yScale, targetSize.z);
                standIn.transform.localPosition = new Vector3(0f, targetSize.y * 0.5f, 0f);
                ApplyColor(standIn.GetComponent<MeshRenderer>(), _opaqueLit, fallbackColor);
            }
        }

        // ---- Per-Frame Sync -----------------------------------------------------------------------------------------------------

        public void Sync(
            IReadOnlyList<StreetKidSnapshot> previous,
            StreetBridge bridge,
            float alpha,
            float dt,
            bool hideLocalNearCamera = false)
        {
            if (bridge == null) return;
            float clampedAlpha = Mathf.Clamp01(alpha);
            float stepDt = Mathf.Max(0f, dt);
            _clock += stepDt;

            if (Kind == StreetGameKind.Patintero)
                SyncPatintero(bridge.PatinteroState, bridge.Kids);
            else if (Kind == StreetGameKind.TumbangPreso)
                SyncTumbangPreso(bridge, stepDt);
            else if (Kind == StreetGameKind.LangitLupa)
                SyncLangitLupa(bridge, stepDt);

            SyncKids(previous, bridge, clampedAlpha, stepDt, hideLocalNearCamera);
        }

        public float GetGroundElevation(SVector2 groundPos)
        {
            if (Kind != StreetGameKind.LangitLupa) return 0f;

            for (int i = 0; i < _platforms.Count; i++)
            {
                PlatformVisual pv = _platforms[i];
                if (pv.Root != null && pv.Root.activeSelf && pv.Bounds.Contains(groundPos))
                    return pv.Holder.localScale.y * PlatformHeight;
            }

            foreach (var pair in _mounds)
            {
                MoundVisual mv = pair.Value;
                if (mv.Rise01 > 0.01f && SVector2.Distance(groundPos, mv.Position) <= mv.Radius + 0.15f)
                    return mv.Rise01 * MoundHeight;
            }
            return 0f;
        }

        public void TriggerKidCast(int kidId, bool moving = false)
        {
            if (_kids.TryGetValue(kidId, out KidView view))
                view.TriggerCast(moving, 0.34f);
        }

        void SyncKids(
            IReadOnlyList<StreetKidSnapshot> previous,
            StreetBridge bridge,
            float alpha,
            float dt,
            bool hideLocalNearCamera)
        {
            int localId = bridge.LocalPlayerId;
            IReadOnlyList<StreetKidSnapshot> current = bridge.Kids;
            bool colorblind = SettingsStore.Current.IsColorblind;

            for (int i = 0; i < current.Count; i++)
            {
                StreetKidSnapshot kid = current[i];
                Color roleColor = ResolveRoleOutlineColor(kid, localId, colorblind);

                if (!_kids.TryGetValue(kid.Id, out KidView view))
                {
                    var initSnap = new KidSnapshot
                    {
                        Id = kid.Id,
                        Team = kid.Team,
                        CharacterId = string.IsNullOrEmpty(kid.CharacterId) ? "narda" : kid.CharacterId,
                        Position = kid.Position,
                        Facing = kid.Facing,
                        Hp = 100,
                        Tagged = kid.Jailed,
                        Speed01 = kid.Speed01,
                    };
                    view = new KidView(Root, initSnap, _cameraRules.KidViewScale, roleColor, _transparentUnlit);
                    view.SetTiradorEnabled(false);
                    _kids[kid.Id] = view;
                }
                else
                {
                    view.SetTeamColor(roleColor);
                }

                SVector2 from = kid.Position;
                if (previous != null)
                {
                    for (int p = 0; p < previous.Count; p++)
                    {
                        if (previous[p].Id == kid.Id)
                        {
                            from = previous[p].Position;
                            break;
                        }
                    }
                }

                SVector2 interpGround = SVector2.Lerp(from, kid.Position, alpha);
                float elevation = GetGroundElevation(interpGround);
                int status = kid.Stumbling ? (int)StatusKind.Tripped : (int)StatusKind.None;
                string auraBuff = kid.SafeOnLangit ? "teks" : (kid.Diving ? "palamig" : "");
                bool hideModel = !kid.Visible || (kid.Id == localId && hideLocalNearCamera);

                view.SyncState(
                    ground: interpGround,
                    airY: 0f,
                    facing: kid.Facing,
                    speed01: kid.Jailed ? 0f : kid.Speed01,
                    crouching: false,
                    sliding: false,
                    dashing: kid.Diving,
                    statusKind: status,
                    tagged: kid.Jailed,
                    hitFlash01: kid.Stumbling ? 0.65f : 0f,
                    buffId: auraBuff,
                    hideNearCamera: hideModel,
                    dt: dt);

                if (elevation > 0.001f)
                {
                    Vector3 pos = view.Root.transform.position;
                    view.Root.transform.position = new Vector3(pos.x, pos.y + elevation, pos.z);
                }

                if (kid.Id == localId)
                {
                    if (_localLanternLight != null && _localLanternLight.enabled)
                    {
                        Vector3 worldFeet = KalyeahResources.World(interpGround, elevation + 2.5f);
                        _localLanternLight.transform.position = worldFeet;
                    }
                    if (_localLanternPool != null && _localLanternPool.enabled)
                    {
                        _localLanternPool.transform.position = KalyeahResources.World(interpGround, elevation + 0.022f);
                    }
                }
            }
        }

        public static Color ResolveRoleOutlineColor(StreetKidSnapshot kid, int localId, bool colorblind)
        {
            switch (kid.Role)
            {
                case StreetRoleKind.Taya:
                    return colorblind ? new Color(0.98f, 0.38f, 0.10f, 1f) : new Color(1.00f, 0.22f, 0.20f, 1f);
                case StreetRoleKind.Guard:
                    return colorblind ? new Color(0.96f, 0.44f, 0.12f, 1f) : new Color(1.00f, 0.34f, 0.20f, 1f);
                case StreetRoleKind.Thrower:
                    return kid.Id == localId
                        ? (colorblind ? new Color(0.18f, 0.80f, 1.00f, 1f) : new Color(0.22f, 0.60f, 1.00f, 1f))
                        : new Color(0.36f, 0.78f, 0.96f, 1f);
                default: // Runner
                    return kid.Id == localId
                        ? (colorblind ? new Color(0.16f, 0.82f, 0.98f, 1f) : new Color(0.20f, 0.56f, 1.00f, 1f))
                        : new Color(0.30f, 0.72f, 1.00f, 1f);
            }
        }

        void SyncPatintero(PatinteroSnapshot pat, IReadOnlyList<StreetKidSnapshot> kids)
        {
            IReadOnlyList<PatinteroLineSnapshot> lines = pat.GuardLines;
            int count = lines != null ? Mathf.Min(lines.Count, _guardLanes.Count) : 0;

            for (int i = 0; i < _guardLanes.Count; i++)
            {
                GuardLaneVisual vis = _guardLanes[i];
                if (i >= count)
                {
                    if (vis.Root.gameObject.activeSelf) vis.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!vis.Root.gameObject.activeSelf) vis.Root.gameObject.SetActive(true);
                PatinteroLineSnapshot line = lines[i];
                float length = Mathf.Max(2f, line.SpanHalfExtent * 2f);
                SVector2 center = line.IsVertical
                    ? new SVector2(line.Coordinate, 0f)
                    : new SVector2(0f, line.Coordinate);

                vis.Root.position = KalyeahResources.World(center, 0f);
                vis.ChalkBar.localPosition = new Vector3(0f, ChalkLiftY, 0f);
                vis.GlowBar.localPosition = new Vector3(0f, GlowLiftY, 0f);

                if (line.IsVertical)
                {
                    vis.ChalkBar.localScale = new Vector3(ChalkWidth, 1f, length);
                    vis.GlowBar.localScale = new Vector3(1.25f, 1f, length);
                }
                else
                {
                    vis.ChalkBar.localScale = new Vector3(length, 1f, ChalkWidth);
                    vis.GlowBar.localScale = new Vector3(length, 1f, 1.25f);
                }

                bool guardActive = false;
                for (int k = 0; k < kids.Count; k++)
                {
                    if (kids[k].Id == line.GuardId && kids[k].Speed01 > 0.08f)
                    {
                        guardActive = true;
                        break;
                    }
                }
                float pulse = 0.78f + 0.22f * Mathf.Sin(_clock * 5.5f + i);
                Color glow = Color.Lerp(GuardGlowIdle, GuardGlowHot, guardActive ? pulse : 0.35f);
                SetBlockColor(vis.GlowRenderer, glow);
            }
        }

        void SyncTumbangPreso(StreetBridge bridge, float dt)
        {
            TumbangPresoSnapshot tp = bridge.TumbangPresoState;
            int localId = bridge.LocalPlayerId;

            // 1. Chalk can circle at CanSpot (brighter gold pulse while knocked down waiting to be reset)
            if (_canCircleRenderer != null)
            {
                _canCircleRenderer.transform.position = KalyeahResources.World(tp.CanSpot, ChalkLiftY);
                float diameter = Mathf.Max(0.9f, tp.CanHitRadius * 4.2f);
                _canCircleRenderer.transform.localScale = new Vector3(diameter, 1f, diameter);
                Color ringCol = tp.CanUp
                    ? ChalkDim
                    : Color.Lerp(ChalkWhite, ChalkGold, 0.5f + 0.5f * Mathf.Sin(_clock * 6f));
                SetBlockColor(_canCircleRenderer, ringCol);
            }

            // 2. 3D Tin Can (carried by Taya, mid-fling parabolic tumble, standing upright, or knocked on its side)
            if (_canRoot != null)
            {
                float scaledCanH = CanTargetHeight * Mathf.Max(0.5f, _cameraRules.KidViewScale);
                if (!_canInitialized)
                {
                    _canInitialized = true;
                    _canWasUp = tp.CanUp;
                    _canFlingTo = tp.CanPosition;
                }
                else if (_canWasUp && !tp.CanUp && _canFlingElapsed > FlingDuration)
                {
                    _canFlingFrom = tp.CanSpot;
                    _canFlingTo = tp.CanPosition;
                    _canFlingElapsed = 0f;
                }
                _canWasUp = tp.CanUp;

                if (tp.CanCarrierId > 0)
                {
                    StreetKidSnapshot? carrier = bridge.Find(tp.CanCarrierId);
                    if (carrier.HasValue)
                    {
                        SVector2 fwd = carrier.Value.Facing.LengthSquared() > 0.001f
                            ? SVector2.Normalize(carrier.Value.Facing)
                            : new SVector2(0f, -1f);
                        SVector2 heldGround = carrier.Value.Position + fwd * 0.34f;
                        _canRoot.position = KalyeahResources.World(heldGround, CarryCanHeight * _cameraRules.KidViewScale);
                        _canModelPivot.localRotation = Quaternion.Euler(14f, 0f, 0f);
                    }
                }
                else if (_canFlingElapsed < FlingDuration && dt > 0f)
                {
                    _canFlingElapsed += dt;
                    _canFlingTo = tp.CanPosition;
                    float t = Mathf.Clamp01(_canFlingElapsed / FlingDuration);
                    SVector2 ground = SVector2.Lerp(_canFlingFrom, _canFlingTo, t);
                    float arcY = 4f * t * (1f - t) * FlingPeakHeight;
                    float lieY = scaledCanH * 0.28f;
                    _canRoot.position = KalyeahResources.World(ground, Mathf.Lerp(0f, lieY, t) + arcY);
                    _canModelPivot.localRotation = Quaternion.Euler(t * 450f, t * 140f, 0f);
                }
                else if (tp.CanUp)
                {
                    _canRoot.position = KalyeahResources.World(tp.CanPosition, 0f);
                    _canModelPivot.localRotation = Quaternion.identity;
                }
                else
                {
                    _canRoot.position = KalyeahResources.World(tp.CanPosition, scaledCanH * 0.28f);
                    _canModelPivot.localRotation = Quaternion.Euler(88f, 32f, 0f);
                }
            }

            // 3. Thrown & grounded 3D Slippers (tsinelas) + local slipper pickup ring
            IReadOnlyList<SlipperSnapshot> slippers = tp.Slippers;
            bool showOwnPickupRing = false;
            SVector2 ownSlipperPos = SVector2.Zero;

            foreach (var pair in _slippers)
            {
                if (pair.Value.Root != null) pair.Value.Root.SetActive(false);
            }

            if (slippers != null)
            {
                for (int i = 0; i < slippers.Count; i++)
                {
                    SlipperSnapshot s = slippers[i];
                    SlipperVisual vis = EnsureSlipperVisual(s.OwnerId);
                    vis.Root.SetActive(true);

                    float y = s.InFlight ? 0.36f : 0.045f;
                    vis.Root.transform.position = KalyeahResources.World(s.Position, y);

                    if (s.InFlight)
                    {
                        vis.SpinClock += dt;
                        vis.ModelPivot.localRotation = Quaternion.Euler(12f, vis.SpinClock * 1080f, 0f);
                    }
                    else
                    {
                        float yaw = s.Direction.LengthSquared() > 0.001f
                            ? GodotSpace.ModelYawDegrees(GodotSpace.KidYawRad(s.Direction))
                            : (s.OwnerId * 47f);
                        vis.ModelPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

                        if (s.OwnerId == localId)
                        {
                            showOwnPickupRing = true;
                            ownSlipperPos = s.Position;
                        }
                    }
                }
            }

            if (_localSlipperRing != null)
            {
                _localSlipperRing.gameObject.SetActive(showOwnPickupRing);
                if (showOwnPickupRing)
                {
                    float breath = 1.25f + 0.14f * Mathf.Sin(_clock * 4.5f);
                    _localSlipperRing.position = KalyeahResources.World(ownSlipperPos, ChalkLiftY + 0.005f);
                    _localSlipperRing.localScale = new Vector3(breath, 1f, breath);
                }
            }

            // 4. Local thrower's hold-to-charge aim line & landing ring
            StreetKidSnapshot? me = bridge.LocalKid;
            bool showAim = me.HasValue && me.Value.AimingThrow && me.Value.AimRange > 0.25f;
            if (_aimLineTransform != null && _aimTipRingTransform != null)
            {
                bool showLine = showAim && SettingsStore.Current.AimGuide;
                _aimLineTransform.gameObject.SetActive(showLine);
                _aimTipRingTransform.gameObject.SetActive(showAim);

                if (showAim)
                {
                    StreetKidSnapshot local = me.Value;
                    SVector2 dir = local.AimDirection.LengthSquared() > 0.001f
                        ? SVector2.Normalize(local.AimDirection)
                        : new SVector2(0f, -1f);
                    float range = local.AimRange;
                    SVector2 mid = local.Position + dir * (range * 0.5f);
                    SVector2 tip = local.Position + dir * range;

                    bool full = local.ThrowCharge01 >= 0.98f;
                    Color aimCol = full
                        ? Color.Lerp(ChalkGold, Color.white, 0.35f + 0.35f * Mathf.Sin(_clock * 10f))
                        : Color.Lerp(ChalkWhite, ChalkGold, local.ThrowCharge01 * 0.65f);

                    if (showLine)
                    {
                        _aimLineTransform.position = KalyeahResources.World(mid, ChalkLiftY + 0.008f);
                        float yaw = GodotSpace.ModelYawDegrees(GodotSpace.KidYawRad(dir));
                        _aimLineTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        _aimLineTransform.localScale = new Vector3(0.42f, 1f, range);
                        SetBlockColor(_aimLineRenderer, aimCol);
                    }

                    _aimTipRingTransform.position = KalyeahResources.World(tip, ChalkLiftY + 0.010f);
                    float tipScale = full ? (1.35f + 0.16f * Mathf.Sin(_clock * 10f)) : 1.25f;
                    _aimTipRingTransform.localScale = new Vector3(tipScale, 1f, tipScale);
                    SetBlockColor(_aimTipRingRenderer, aimCol);
                }
            }
        }

        SlipperVisual EnsureSlipperVisual(int ownerId)
        {
            if (_slippers.TryGetValue(ownerId, out SlipperVisual existing)) return existing;

            var rootGo = new GameObject($"Slipper_{ownerId}");
            rootGo.transform.SetParent(_tumbangRoot != null ? _tumbangRoot : Root, false);

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(rootGo.transform, false);

            var vis = new SlipperVisual { Root = rootGo, ModelPivot = pivot };
            GameObject prefab = KalyeahResources.Model("weapons", "slipper");
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, pivot, false);
                model.name = "SlipperModel";
                SanitizeModelMaterials(model);
                Vector3 nat = MeasureNaturalSize("weapons/slipper", model);
                float longest = Mathf.Max(Mathf.Max(nat.x, nat.z), 0.05f);
                model.transform.localScale = Vector3.one * (SlipperTargetLength / longest);
                Bounds b = KalyeahResources.RenderBounds(model);
                model.transform.localPosition += Vector3.up * (pivot.position.y - b.min.y);
                vis.Renderers.AddRange(model.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                GameObject sole = CreatePrimitiveNoCollider(PrimitiveType.Cube, "SoleStandIn", pivot);
                sole.transform.localScale = new Vector3(0.20f, 0.045f, SlipperTargetLength);
                sole.transform.localPosition = new Vector3(0f, 0.022f, 0f);
                var r = sole.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.sharedMaterial = _opaqueLit;
                    vis.Renderers.Add(r);
                }
            }

            Color tint = SlipperColors[Mathf.Abs(ownerId - 1) % SlipperColors.Length];
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, tint);
            _mpb.SetColor(LegacyColorId, tint);
            _mpb.SetColor(GltfBaseColorId, tint);
            for (int i = 0; i < vis.Renderers.Count; i++)
            {
                if (vis.Renderers[i] != null)
                    vis.Renderers[i].SetPropertyBlock(_mpb);
            }

            _slippers[ownerId] = vis;
            return vis;
        }

        void SyncLangitLupa(StreetBridge bridge, float dt)
        {
            LangitLupaSnapshot ll = bridge.LangitLupaState;

            // 1. Elevated Langit Platforms (sink & redden rim with Wear01; hide when broken)
            if (ll.Platforms != null)
            {
                for (int i = 0; i < ll.Platforms.Count; i++)
                {
                    LangitPlatformSnapshot snap = ll.Platforms[i];
                    PlatformVisual pv = EnsurePlatformVisual(i, snap.Bounds);
                    if (pv.Root.activeSelf != snap.Standing)
                        pv.Root.SetActive(snap.Standing);

                    if (snap.Standing)
                    {
                        float wear = Mathf.Clamp01(snap.Wear01);
                        float scaleY = Mathf.Lerp(1f, WornHeightShare, wear);
                        pv.Holder.localScale = new Vector3(1f, scaleY, 1f);
                        Color rimCol = Color.Lerp(ChalkWarmRim, ChalkDanger, wear);
                        for (int r = 0; r < pv.RimRenderers.Length; r++)
                            SetBlockColor(pv.RimRenderers[r], rimCol);
                    }
                }
            }

            // 2. Rising & falling Lupa Mounds
            _seenMoundOwners.Clear();
            if (ll.Mounds != null)
            {
                for (int i = 0; i < ll.Mounds.Count; i++)
                {
                    LangitMoundSnapshot ms = ll.Mounds[i];
                    _seenMoundOwners.Add(ms.OwnerId);
                    MoundVisual mv = EnsureMoundVisual(ms.OwnerId, ms.Radius);
                    mv.Position = ms.Position;
                    mv.Radius = ms.Radius;
                    mv.TargetUp = true;
                    mv.Root.transform.position = KalyeahResources.World(ms.Position, 0f);
                }
            }

            _moundKeysScratch.Clear();
            foreach (var pair in _mounds) _moundKeysScratch.Add(pair.Key);

            bool instant = !Application.isPlaying || dt <= 0f || SettingsStore.Current.ReduceMotion;
            float riseStep = instant ? 1f : (dt / MoundRiseSeconds);

            for (int i = 0; i < _moundKeysScratch.Count; i++)
            {
                int ownerId = _moundKeysScratch[i];
                MoundVisual mv = _mounds[ownerId];
                bool activeNow = _seenMoundOwners.Contains(ownerId);
                mv.TargetUp = activeNow;
                mv.Rise01 = Mathf.Clamp01(mv.Rise01 + (activeNow ? riseStep : -riseStep));

                bool show = mv.Rise01 > 0.005f;
                if (mv.Root.activeSelf != show)
                    mv.Root.SetActive(show);
                if (show)
                    mv.Holder.localScale = new Vector3(1f, Mathf.Max(0.02f, mv.Rise01), 1f);
            }

            // 3. Brownout ambient dim + curbside Meralco streetlamp cones + local lantern point light
            bool wantBrownout = ForceBrownoutPreview ?? (ll.IsBrownout && !bridge.IsMatchOver);
            if (instant)
                _brownoutBlend = wantBrownout ? 1f : 0f;
            else
                _brownoutBlend = Mathf.MoveTowards(_brownoutBlend, wantBrownout ? 1f : 0f, dt * 2.8f);

            ApplyBrownoutLighting(_brownoutBlend, Mathf.Max(3.5f, ll.BrownoutSight));
        }

        void ApplyBrownoutLighting(float blend01, float sightRadius)
        {
            float b = Mathf.Clamp01(blend01);
            if (_sunLight != null)
            {
                _sunLight.color = Color.Lerp(DaylightSunColor, BrownoutSunColor, b);
                _sunLight.intensity = Mathf.Lerp(DaylightSunIntensity, BrownoutSunIntensity, b);
            }
            if (_fillLight != null)
                _fillLight.intensity = Mathf.Lerp(DaylightFillIntensity, BrownoutFillIntensity, b);

            RenderSettings.ambientLight = Color.Lerp(DaylightAmbient, BrownoutAmbient, b);
            RenderSettings.fogColor = Color.Lerp(DaylightFog, BrownoutFog, b);
            RenderSettings.fogStartDistance = Mathf.Lerp(76f, 42f, b);
            RenderSettings.fogEndDistance = Mathf.Lerp(260f, 150f, b);

            bool lampsOn = b > 0.04f;
            float flicker = (SettingsStore.Current.ReduceMotion || b > 0.92f)
                ? 1f
                : (0.90f + 0.10f * Mathf.Sin(_clock * 19f));

            for (int i = 0; i < _streetlamps.Count; i++)
            {
                StreetlampVisual lamp = _streetlamps[i];
                if (lamp.ConeRenderer != null)
                {
                    lamp.ConeRenderer.enabled = lampsOn;
                    if (lampsOn)
                    {
                        Color coneCol = LampConeTint;
                        coneCol.a = LampConeTint.a * b * flicker;
                        SetBlockColor(lamp.ConeRenderer, coneCol);
                    }
                }
                if (lamp.PoolRenderer != null)
                {
                    lamp.PoolRenderer.enabled = lampsOn;
                    if (lampsOn)
                    {
                        Color poolCol = LampPoolTint;
                        poolCol.a = LampPoolTint.a * b * flicker;
                        SetBlockColor(lamp.PoolRenderer, poolCol);
                    }
                }
                if (lamp.BulbRenderer != null)
                {
                    Color bulbCol = Color.Lerp(new Color(0.88f, 0.84f, 0.72f, 0.72f), LampWarmColor, b);
                    SetBlockColor(lamp.BulbRenderer, bulbCol);
                }
                if (lamp.PointLight != null)
                {
                    lamp.PointLight.enabled = lampsOn;
                    lamp.PointLight.intensity = 2.45f * b * flicker;
                }
            }

            if (_localLanternLight != null)
            {
                _localLanternLight.enabled = lampsOn;
                _localLanternLight.range = sightRadius * 1.85f;
                _localLanternLight.intensity = 2.65f * b;
            }

            if (_localLanternPool != null)
            {
                _localLanternPool.enabled = lampsOn;
                if (lampsOn)
                {
                    float poolDia = Mathf.Max(5.5f, sightRadius * 2.0f);
                    _localLanternPool.transform.localScale = new Vector3(poolDia, 1f, poolDia);
                    Color lCol = LampPoolTint;
                    lCol.a = 0.28f * b;
                    SetBlockColor(_localLanternPool, lCol);
                }
            }
        }

        // ---- Low-Overhead Procedural Geometry & Material Helpers ---------------------------------------------------------------

        void BuildDashedChalkLine(Transform parent, string name, SVector2 center, float totalLength, bool vertical, Color color, int dashes)
        {
            var container = new GameObject(name).transform;
            container.SetParent(parent, false);
            container.position = KalyeahResources.World(center, ChalkLiftY);

            int count = Mathf.Max(1, dashes);
            float step = totalLength / count;
            float dashLen = step * 0.72f;
            float start = -totalLength * 0.5f + step * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float offset = start + i * step;
                var seg = new GameObject($"Dash_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                seg.transform.SetParent(container, false);
                seg.transform.localPosition = vertical ? new Vector3(0f, 0f, -offset) : new Vector3(offset, 0f, 0f);
                seg.transform.localScale = vertical ? new Vector3(ChalkWidth, 1f, dashLen) : new Vector3(dashLen, 1f, ChalkWidth);
                seg.GetComponent<MeshFilter>().sharedMesh = _quadMesh;
                ConfigureUnlitRenderer(seg.GetComponent<MeshRenderer>(), _transparentUnlit, color);
            }
        }

        MeshRenderer CreateFlatQuad(Transform parent, string name, SVector2 at, Vector2 sizeXZ, float y, Color color, bool localSpace = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            if (localSpace)
                go.transform.localPosition = new Vector3(at.X, y, -at.Y);
            else
                go.transform.position = KalyeahResources.World(at, y);
            go.transform.localScale = new Vector3(sizeXZ.x, 1f, sizeXZ.y);
            go.GetComponent<MeshFilter>().sharedMesh = _quadMesh;
            var mr = go.GetComponent<MeshRenderer>();
            ConfigureUnlitRenderer(mr, _transparentUnlit, color);
            return mr;
        }

        MeshRenderer CreateRing(Transform parent, string name, SVector2 at, float diameter, float y, Color color)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(at, y);
            go.transform.localScale = new Vector3(diameter, 1f, diameter);
            go.GetComponent<MeshFilter>().sharedMesh = _ringMesh;
            var mr = go.GetComponent<MeshRenderer>();
            ConfigureUnlitRenderer(mr, _transparentUnlit, color);
            return mr;
        }

        static GameObject CreatePrimitiveNoCollider(PrimitiveType type, string name, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            KalyeahResources.Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            return go;
        }

        void ApplyColor(MeshRenderer mr, Material sharedMat, Color color)
        {
            if (mr == null) return;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (sharedMat != null) mr.sharedMaterial = sharedMat;
            SetBlockColor(mr, color);
        }

        void ConfigureUnlitRenderer(MeshRenderer mr, Material sharedMat, Color color)
        {
            if (mr == null) return;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (sharedMat != null) mr.sharedMaterial = sharedMat;
            SetBlockColor(mr, color);
        }

        void SetBlockColor(Renderer r, Color color)
        {
            if (r == null) return;
            _mpb.Clear();
            _mpb.SetColor(GltfBaseColorId, color);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(LegacyColorId, color);
            r.SetPropertyBlock(_mpb);
        }

        Vector3 MeasureNaturalSize(string cacheKey, GameObject model)
        {
            if (_naturalSizes.TryGetValue(cacheKey, out Vector3 cached)) return cached;
            Transform t = model.transform;
            Vector3 prevScale = t.localScale;
            Quaternion prevRot = t.localRotation;
            Vector3 prevPos = t.localPosition;
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            t.localPosition = Vector3.zero;
            cached = KalyeahResources.RenderBounds(model).size;
            t.localScale = prevScale;
            t.localRotation = prevRot;
            t.localPosition = prevPos;
            _naturalSizes[cacheKey] = cached;
            return cached;
        }

        Material CreateSafeMaterial(string resourceName, string shaderName, bool transparent)
        {
            Material baseMat = KalyeahResources.Material(resourceName, shaderName);
            if (baseMat == null)
            {
                Shader fallback = Shader.Find(shaderName)
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default");
                if (fallback != null) baseMat = new Material(fallback);
            }
            if (baseMat == null) return null;

            var inst = new Material(baseMat) { name = resourceName + "_StreetInst" };
            if (transparent)
            {
                if (inst.HasProperty("_Surface")) inst.SetFloat("_Surface", 1f);
                if (inst.HasProperty("_Blend")) inst.SetFloat("_Blend", 0f);
                if (inst.HasProperty("_SrcBlend")) inst.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (inst.HasProperty("_DstBlend")) inst.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (inst.HasProperty("_ZWrite")) inst.SetFloat("_ZWrite", 0f);
                inst.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                inst.renderQueue = (int)RenderQueue.Transparent;
            }
            _ownedAssets.Add(inst);
            return inst;
        }

        Mesh BuildHorizontalQuadMesh()
        {
            var mesh = new Mesh { name = "StreetFlatQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);
            return mesh;
        }

        Mesh BuildAnnulusMesh(int segments, float innerRatio, float outerRatio)
        {
            int seg = Mathf.Max(12, segments);
            float rIn = innerRatio * 0.5f;
            float rOut = outerRatio * 0.5f;
            var verts = new Vector3[seg * 2];
            var norms = new Vector3[seg * 2];
            var tris = new int[seg * 6];

            for (int i = 0; i < seg; i++)
            {
                float a = (2f * Mathf.PI * i) / seg;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * rIn, 0f, s * rIn);
                verts[i * 2 + 1] = new Vector3(c * rOut, 0f, s * rOut);
                norms[i * 2] = Vector3.up;
                norms[i * 2 + 1] = Vector3.up;

                int next = (i + 1) % seg;
                int v0 = i * 2;
                int v1 = i * 2 + 1;
                int v2 = next * 2;
                int v3 = next * 2 + 1;
                int ti = i * 6;
                tris[ti] = v0; tris[ti + 1] = v3; tris[ti + 2] = v1;
                tris[ti + 3] = v0; tris[ti + 4] = v2; tris[ti + 5] = v3;
            }

            var mesh = new Mesh { name = "StreetChalkRing" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);
            return mesh;
        }

        Mesh BuildLampConeMesh(int segments, float topRadius, float bottomRadius, float dropHeight)
        {
            int seg = Mathf.Max(10, segments);
            var verts = new Vector3[seg * 2];
            var colors = new Color32[seg * 2];
            var tris = new int[seg * 12]; // double-sided soft cone

            for (int i = 0; i < seg; i++)
            {
                float a = (2f * Mathf.PI * i) / seg;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * topRadius, 0f, s * topRadius);
                verts[i * 2 + 1] = new Vector3(c * bottomRadius, -dropHeight, s * bottomRadius);
                colors[i * 2] = new Color32(255, 255, 255, 230);
                colors[i * 2 + 1] = new Color32(255, 255, 255, 0);

                int next = (i + 1) % seg;
                int v0 = i * 2;
                int v1 = i * 2 + 1;
                int v2 = next * 2;
                int v3 = next * 2 + 1;
                int ti = i * 12;
                tris[ti] = v0; tris[ti + 1] = v1; tris[ti + 2] = v3;
                tris[ti + 3] = v0; tris[ti + 4] = v3; tris[ti + 5] = v2;
                tris[ti + 6] = v0; tris[ti + 7] = v3; tris[ti + 8] = v1;
                tris[ti + 9] = v0; tris[ti + 10] = v2; tris[ti + 11] = v3;
            }

            var mesh = new Mesh { name = "StreetlampConeMesh" };
            mesh.vertices = verts;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);
            return mesh;
        }

        void SanitizeModelMaterials(GameObject root)
        {
            if (root == null) return;
            KalyeahResources.SanitizeModelMaterials(root);

            if (_opaqueLit == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer ren = renderers[r];
                if (ren == null) continue;
                Material[] shared = ren.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (!NeedsMaterialRepair(shared[i])) continue;
                    shared[i] = _opaqueLit;
                    changed = true;
                }

                if (changed)
                    ren.sharedMaterials = shared;
            }
        }

        static bool NeedsMaterialRepair(Material mat)
        {
            if (mat == null || mat.shader == null || !mat.shader.isSupported)
                return true;
            string sName = mat.shader.name ?? "";
            return sName.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0
                || sName.StartsWith("glTF/", StringComparison.Ordinal)
                || sName.StartsWith("Legacy Shaders/", StringComparison.Ordinal)
                || string.Equals(sName, "Standard", StringComparison.Ordinal);
        }

        Material CreateVertexColorMaterial()
        {
            Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) return _transparentUnlit;
            var mat = new Material(s) { name = "StreetVertexColorUnlit" };
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            _ownedAssets.Add(mat);
            return mat;
        }

        Mesh BuildSoftPoolDiscMesh(int segments)
        {
            int seg = Mathf.Max(14, segments);
            var verts = new Vector3[seg + 1];
            var colors = new Color32[seg + 1];
            var tris = new int[seg * 3];

            verts[0] = Vector3.zero;
            colors[0] = new Color32(255, 245, 215, 225);

            for (int i = 0; i < seg; i++)
            {
                float a = (2f * Mathf.PI * i) / seg;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
                colors[i + 1] = new Color32(255, 220, 140, 0);

                int next = ((i + 1) % seg) + 1;
                int ti = i * 3;
                tris[ti] = 0;
                tris[ti + 1] = next;
                tris[ti + 2] = i + 1;
            }

            var mesh = new Mesh { name = "StreetLightPoolDisc" };
            mesh.vertices = verts;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);
            return mesh;
        }

        public void Dispose()
        {
            if (_bridge != null)
                _bridge.CanFlung -= OnCanFlung;

            for (int i = 0; i < _ownedAssets.Count; i++)
            {
                if (_ownedAssets[i] != null)
                    KalyeahResources.Kill(_ownedAssets[i]);
            }
            _ownedAssets.Clear();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }
    }
}
