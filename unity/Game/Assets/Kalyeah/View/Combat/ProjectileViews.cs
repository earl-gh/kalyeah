using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Zero-GC pooled 3D combat renderer syncing <see cref="SimBridge.WorldEffects"/>
    /// (<c>Projectile</c>, <c>ExplosionZone</c>, <c>TrapArmed</c>, <c>TrapArming</c>, <c>SlowZone</c>,
    /// <c>FieldZone</c>, <c>ShieldWall</c>, <c>PendingCone</c>) plus instant cone flashes, <c>tirador</c> slingshot
    /// cast pops, and <c>green_cross</c> heal markers (<c>scripts/view/weapon_fx_view.gd</c>).
    /// Loads weapon models via <see cref="KalyeahResources.Model"/> (<c>"weapons"</c>, ...) with procedural mesh fallbacks.
    /// </summary>
    public sealed class ProjectileViews : IDisposable
    {
        public static readonly string[] AllWeaponModelIds =
        {
            "tirador",
            "slipper",
            "can",
            "trumpo",
            "paper",
            "crumpled_paper",
            "meteor",
            "scissor",
            "spikes",
            "marble",
            "green_cross",
            "soil_block",
        };

        const int DefaultModelPoolPerKind = 6;
        const int DefaultZoneSlotPool = 20;
        const int DefaultTransientPool = 10;
        const int JacksClustersPerField = 6;
        const int GroundCircleSegments = 36;
        const int ConeWedgeSegments = 14;
        const int CurvedShieldSegments = 10;

        // Visual geometry & animation constants matching scripts/view/fx/*.gd.
        const float ProjectileFlightHeight = 0.72f;
        const float SlipperSpinLightRev = 3.0f;
        const float SlipperSpinHeavyRev = 2.0f;
        const float SlipperTiltDeg = 25f;
        const float RockTumbleRev = 2.5f;
        const float TrumpoSpinRev = 6.0f;
        const float TrumpoDropSeconds = 0.08f;
        const float TrumpoWobbleDeg = 7.5f;
        const float BolaArcPeak = 1.15f;
        const float BolaHandHeight = 1.05f;
        const float CanHandHeight = 1.80f;
        const float CanPeakBase = 1.85f;
        const float CanSideBow = 0.95f;
        const float MeteorDropHeight = 7.6f;
        const float BagsakDropHeight = 9.8f;
        const float ShieldHeight = 2.60f;
        const float ShieldThickness = 0.28f;
        const float ShieldCurveBow = 0.30f;
        const float ShieldFadeSeconds = 0.50f;
        const float SoilWallHeight = 0.44f;
        const float SoilWallDepth = 0.62f;
        const float SoilRiseSeconds = 0.30f;
        const float SoilCollapseSeconds = 0.60f;
        const float GroundRibbonWidth = 0.12f;
        const float TelegraphHeight = 0.032f;

        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropLegacyColor = Shader.PropertyToID("_Color");
        static readonly int PropGltfBaseColor = Shader.PropertyToID("baseColorFactor");
        static readonly int PropTint = Shader.PropertyToID("_Tint");
        static readonly int PropInkColor = Shader.PropertyToID("_InkColor");
        static readonly int PropPhase = Shader.PropertyToID("_Phase");
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");
        static readonly int PropFresnel = Shader.PropertyToID("_FresnelSoftness");
        static readonly int PropSunlit = Shader.PropertyToID("_SunlitLift");
        static readonly int PropHardEdgeWidth = Shader.PropertyToID("_HardEdgeWidth");
        static readonly int PropDashCount = Shader.PropertyToID("_DashCount");
        static readonly int PropEdgeBoost = Shader.PropertyToID("_EdgeBoost");
        static readonly int PropInteriorAlpha = Shader.PropertyToID("_InteriorAlpha");
        static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int PropDistanceBoost = Shader.PropertyToID("_DistanceBoost");
        static readonly int PropHitFlash = Shader.PropertyToID("_HitFlash");
        const float WeaponAllyOutlineWidth = 0.013f;
        const float WeaponEnemyOutlineWidth = 0.017f;
        const float EnemyCircleDashCount = 18f;
        const float EnemyConeDashCount = 14f;
        const float EnemyRibbonDashCount = 10f;
        const float EnemyTrailDashCount = 6f;
        static readonly Color WeaponInkBase = new Color(0.14f, 0.11f, 0.12f, 0.94f);

        static readonly Color AllyTelegraphColor = new Color(0.24f, 0.58f, 1.00f, 0.92f);
        static readonly Color EnemyTelegraphColor = new Color(1.00f, 0.26f, 0.24f, 0.94f);
        static readonly Color BagsakGoldAccent = new Color(1.00f, 0.80f, 0.24f, 0.96f);
        static readonly Color HealGreenColor = new Color(0.28f, 0.92f, 0.54f, 0.96f);

        static readonly Color[] JacksClusterColors =
        {
            new Color(0.957f, 0.894f, 0.227f, 1f), // #F4E43A yellow
            new Color(1.000f, 0.310f, 0.604f, 1f), // #FF4F9A pink
            new Color(0.271f, 0.878f, 0.424f, 1f), // #45E06C green
            new Color(1.000f, 0.416f, 0.122f, 1f), // #FF6A1F orange
            new Color(0.239f, 0.545f, 1.000f, 1f), // #3D8BFF blue
            new Color(0.659f, 0.333f, 0.969f, 1f), // #A855F7 purple
        };

        sealed class ModelNode
        {
            public string ModelId;
            public GameObject Root;
            public Transform Tr;
            public Transform Tilt;
            public Transform Spin;
            public ScissorsRig Scissors;
            public Renderer[] Renderers;
            public Renderer[] OutlineRenderers;
            public MaterialPropertyBlock Block;
            public MaterialPropertyBlock OutlineBlock;
            public bool UsesFallbackMesh;
        }

        sealed class EffectSlot
        {
            public int Key;
            public WorldEffectSnapshot LastSnap;
            public WorldEffectKind BoundKind;
            public string BoundWeaponId;
            public string BoundModelId;

            public GameObject Root;
            public Transform Tr;
            public MeshFilter RimFilter;
            public MeshFilter FillFilter;
            public MeshRenderer RimRenderer;
            public MeshRenderer FillRenderer;
            public MeshRenderer TrailRenderer;
            public Transform RimTr;
            public Transform FillTr;
            public Transform TrailTr;
            public MaterialPropertyBlock RimBlock;
            public MaterialPropertyBlock FillBlock;
            public MaterialPropertyBlock TrailBlock;

            public ModelNode PrimaryModel;
            public readonly ModelNode[] SubModels = new ModelNode[JacksClustersPerField];
            public int SubModelCount;

            public SVector2 SpawnOriginSim;
            public bool HasSpawnOrigin;
            public float SpawnTime;
            public float TotalDuration;
            public float SpinAngleRad;
            public float SpinSign = 1f;
            public bool WasReturning;
            public int LastBounces;
            public float ReactUntil;
            public int LastSeenStamp;
        }

        sealed class TransientVisual
        {
            public GameObject Root;
            public Transform Tr;
            public MeshFilter GroundFilter;
            public MeshRenderer GroundRenderer;
            public Transform GroundTr;
            public MaterialPropertyBlock GroundBlock;
            public ModelNode Model;
            public float Elapsed;
            public float Duration;
            public float BaseModelSize;
            public Vector3 StartPos;
            public Vector3 Velocity;
            public bool IsCone;
            public bool IsScissors;
            public bool IsHeavyScissors;
        }

        readonly Transform _root;
        readonly Material _litMaterial;
        readonly Material _overlayMaterial;
        readonly bool _ownsLitMaterial;
        readonly bool _ownsOverlayMaterial;

        readonly Mesh _ringMesh;
        readonly Mesh _discMesh;
        readonly Mesh _wedgeRimMesh;
        readonly Mesh _wedgeFillMesh;
        readonly Mesh _ribbonQuadMesh;
        readonly Mesh _curvedShieldMesh;
        readonly Mesh _trailMesh;
        Mesh _scissorBladeAMesh;
        Mesh _scissorBladeBMesh;
        Material _scissorMaterial;
        Vector3 _scissorScrewPivot;
        float _scissorOpenSignA = 1f;
        float _scissorNaturalLength = 1f;
        float _scissorMinLengthY;
        bool _scissorUsesFallback = true;
        readonly Dictionary<string, Mesh> _fallbackMeshes = new Dictionary<string, Mesh>(16, StringComparer.Ordinal);
        readonly Dictionary<string, GameObject> _prefabTemplates = new Dictionary<string, GameObject>(16, StringComparer.Ordinal);
        readonly Dictionary<string, Vector3> _prefabNaturalSizes = new Dictionary<string, Vector3>(16, StringComparer.Ordinal);
        readonly Dictionary<string, Stack<ModelNode>> _modelPools = new Dictionary<string, Stack<ModelNode>>(16, StringComparer.Ordinal);

        readonly Stack<EffectSlot> _freeSlots;
        readonly Dictionary<int, EffectSlot> _activeSlots = new Dictionary<int, EffectSlot>(48);
        readonly List<int> _staleKeys = new List<int>(48);
        readonly Dictionary<int, SVector2> _prevSimPositions = new Dictionary<int, SVector2>(48);
        readonly Dictionary<int, SVector2> _recentCastOrigins = new Dictionary<int, SVector2>(16);

        readonly Stack<TransientVisual> _freeTransients;
        readonly List<TransientVisual> _liveTransients = new List<TransientVisual>(DefaultTransientPool);

        SimBridge _boundBridge;
        float _clock;
        int _syncStamp;
        bool _disposed;

        public Transform Root => _root;
        public FxPool Fx { get; set; }
        public AudioHub Audio { get; set; }
        public bool ReduceMotion { get; set; }
        public float ViewScale { get; set; } = 0.82f;
        public int LocalTeam { get; set; } = SimBridge.LocalTeam;
        public int LiveEffectCount => _activeSlots.Count;
        public int ActiveFlashCount => _liveTransients.Count;
        public float LastScissorsAngleDegrees { get; private set; }

        public int ActiveScissorsCount
        {
            get
            {
                int count = 0;
                foreach (var kv in _activeSlots)
                    if (kv.Value.PrimaryModel != null && kv.Value.PrimaryModel.Scissors != null) count++;
                for (int i = 0; i < _liveTransients.Count; i++)
                    if (_liveTransients[i].Model != null && _liveTransients[i].Model.Scissors != null) count++;
                return count;
            }
        }

        public int LiveProjectileCount
        {
            get
            {
                int count = 0;
                foreach (var kv in _activeSlots)
                    if (kv.Value.BoundKind == WorldEffectKind.Projectile) count++;
                return count;
            }
        }

        public ProjectileViews(
            Transform parent = null,
            FxPool fx = null,
            AudioHub audio = null,
            float viewScale = 0.82f,
            bool reduceMotion = false)
        {
            Fx = fx;
            Audio = audio;
            ViewScale = Mathf.Clamp(viewScale, 0.35f, 2.0f);
            ReduceMotion = reduceMotion;

            var rootGo = new GameObject("ProjectileViews");
            _root = rootGo.transform;
            if (parent != null) _root.SetParent(parent, false);

            _litMaterial = CreateLitMaterial(out _ownsLitMaterial);
            _overlayMaterial = CreateOverlayMaterial(out _ownsOverlayMaterial);

            _ringMesh = BuildAnnulusMesh("TelegraphRing", 0.90f, 1.00f, GroundCircleSegments);
            _discMesh = BuildDiscMesh("TelegraphDisc", 0.90f, GroundCircleSegments);
            _wedgeRimMesh = BuildCanonicalWedgeRimMesh("ConeWedgeRim", ConeWedgeSegments);
            _wedgeFillMesh = BuildCanonicalWedgeFillMesh("ConeWedgeFill", ConeWedgeSegments);
            _ribbonQuadMesh = BuildUnitQuadMesh("WallRibbonQuad");
            _curvedShieldMesh = BuildCurvedShieldMesh("CurvedPapelShield", CurvedShieldSegments);
            _trailMesh = BuildProjectileTrailMesh("ProjectileEdgeTrail");

            BuildFallbackMeshCatalog();
            PrewarmModelPools();

            _freeSlots = new Stack<EffectSlot>(DefaultZoneSlotPool);
            for (int i = 0; i < DefaultZoneSlotPool; i++)
                _freeSlots.Push(CreateEffectSlot(i));

            _freeTransients = new Stack<TransientVisual>(DefaultTransientPool);
            for (int i = 0; i < DefaultTransientPool; i++)
                _freeTransients.Push(CreateTransientSlot(i));
        }

        /// <summary>Maps any weapon ID + optional look tag onto one of the 12 canonical <c>models/weapons/*</c> keys.</summary>
        public static string ResolveModelKey(string weaponId, string look = null)
        {
            if (look == "soil" || weaponId == "langit_lupa") return "soil_block";
            if (string.IsNullOrEmpty(weaponId)) return "meteor";

            switch (weaponId)
            {
                case "tirador": return "tirador";
                case "tirador_bato": return "meteor";
                case "tsinelas_light":
                case "tsinelas_heavy":
                case "slipper": return "slipper";
                case "lata":
                case "can": return "can";
                case "trumpo": return "trumpo";
                case "papel_shield":
                case "paper": return "paper";
                case "papel_trap":
                case "crumpled_paper": return "crumpled_paper";
                case "bato_light":
                case "bato_heavy":
                case "ult_bagsak":
                case "meteor": return "meteor";
                case "gunting_light":
                case "gunting_heavy":
                case "scissor": return "scissor";
                case "jacks":
                case "spikes": return "spikes";
                case "bola":
                case "marble": return "marble";
                case "heal":
                case "green_cross": return "green_cross";
                case "soil_block": return "soil_block";
                default: return "meteor";
            }
        }

        /// <summary>Real-world unscaled reference size (metres) for a weapon's 3D object.</summary>
        public static float BaseModelMeters(string weaponId)
        {
            switch (weaponId)
            {
                case "tirador": return 0.34f;
                case "tirador_bato": return 0.34f;
                case "bato_light": return 0.44f;
                case "bato_heavy": return 1.05f;
                case "ult_bagsak": return 1.42f;
                case "tsinelas_light": return 0.52f;
                case "tsinelas_heavy": return 0.84f;
                case "lata": return 0.70f;
                case "bola": return 0.56f;
                case "papel_trap": return 0.54f;
                case "trumpo": return 1.12f;
                case "gunting_light": return 1.25f;
                case "gunting_heavy": return 1.95f;
                case "jacks": return 0.55f;
                case "heal": return 0.40f;
                default: return 0.52f;
            }
        }

        /// <summary>Subscribes to <see cref="SimBridge"/> discrete combat events (paired cleanly in <see cref="Unbind"/>).</summary>
        public void Bind(SimBridge bridge)
        {
            if (ReferenceEquals(_boundBridge, bridge)) return;
            Unbind();
            _boundBridge = bridge;
            if (_boundBridge == null) return;

            LocalTeam = _boundBridge.LocalPlayerTeam;
            _boundBridge.WeaponCast += OnWeaponCast;
            _boundBridge.UltimateCast += OnUltimateCast;
            _boundBridge.ConeTriggered += OnConeTriggered;
            _boundBridge.PlayerHealed += OnPlayerHealed;
            _boundBridge.PlayerDamaged += OnPlayerDamaged;
            _boundBridge.PlayerDashed += OnPlayerDashed;
            _boundBridge.PointReset += ClearAll;
        }

        public void Unbind()
        {
            if (_boundBridge == null) return;
            _boundBridge.WeaponCast -= OnWeaponCast;
            _boundBridge.UltimateCast -= OnUltimateCast;
            _boundBridge.ConeTriggered -= OnConeTriggered;
            _boundBridge.PlayerHealed -= OnPlayerHealed;
            _boundBridge.PlayerDamaged -= OnPlayerDamaged;
            _boundBridge.PlayerDashed -= OnPlayerDashed;
            _boundBridge.PointReset -= ClearAll;
            _boundBridge = null;
        }

        public void Sync(SimBridge bridge, float dt, float alpha = 1f)
        {
            if (bridge == null || _disposed) return;
            SettingsStore settings = SettingsStore.Current;
            if (settings != null) ReduceMotion = settings.ReduceMotion;

            LocalTeam = bridge.LocalPlayerTeam;
            Sync(null, bridge.WorldEffects, bridge.Kids, bridge.LocalPlayerTeam, alpha, dt);
        }

        public void Sync(
            IReadOnlyList<WorldEffectSnapshot> previousEffects,
            IReadOnlyList<WorldEffectSnapshot> currentEffects,
            IReadOnlyList<KidSnapshot> kids,
            int localTeam,
            float alpha,
            float dt)
        {
            if (_disposed) return;
            LocalTeam = localTeam;
            float step = Mathf.Max(0f, dt);
            _clock += step;
            _syncStamp++;

            _prevSimPositions.Clear();
            if (previousEffects != null)
            {
                for (int i = 0; i < previousEffects.Count; i++)
                {
                    WorldEffectSnapshot prev = previousEffects[i];
                    _prevSimPositions[EffectKey(prev)] = prev.Position;
                }
            }

            if (currentEffects != null)
            {
                float blend = Mathf.Clamp01(alpha);
                for (int i = 0; i < currentEffects.Count; i++)
                {
                    WorldEffectSnapshot snap = currentEffects[i];
                    int key = EffectKey(snap);

                    if (!_activeSlots.TryGetValue(key, out EffectSlot slot))
                    {
                        slot = AcquireSlot(key, snap, kids);
                        _activeSlots[key] = slot;
                    }
                    else if (slot.BoundKind != snap.Kind)
                    {
                        HandleKindTransition(slot, snap);
                    }

                    slot.LastSeenStamp = _syncStamp;
                    SVector2 simPos = _prevSimPositions.TryGetValue(key, out SVector2 fromPos)
                        ? SVector2.Lerp(fromPos, snap.Position, blend)
                        : snap.Position;

                    UpdateSlot(slot, snap, simPos, step);
                    slot.LastSnap = snap;
                }
            }

            _staleKeys.Clear();
            foreach (var kv in _activeSlots)
            {
                if (kv.Value.LastSeenStamp != _syncStamp)
                    _staleKeys.Add(kv.Key);
            }

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                int key = _staleKeys[i];
                EffectSlot removed = _activeSlots[key];
                _activeSlots.Remove(key);
                OnEffectRemoved(removed);
                ReleaseSlot(removed);
            }

            TickTransients(step);
        }

        public void ClearAll()
        {
            _staleKeys.Clear();
            foreach (var kv in _activeSlots)
                _staleKeys.Add(kv.Key);

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                EffectSlot slot = _activeSlots[_staleKeys[i]];
                ReleaseSlot(slot);
            }
            _activeSlots.Clear();
            _prevSimPositions.Clear();
            _recentCastOrigins.Clear();

            for (int i = _liveTransients.Count - 1; i >= 0; i--)
                RecycleTransientAt(i);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unbind();
            ClearAll();

            if (_root != null) KalyeahResources.Kill(_root.gameObject);
            KalyeahResources.Kill(_ringMesh);
            KalyeahResources.Kill(_discMesh);
            KalyeahResources.Kill(_wedgeRimMesh);
            KalyeahResources.Kill(_wedgeFillMesh);
            KalyeahResources.Kill(_ribbonQuadMesh);
            KalyeahResources.Kill(_curvedShieldMesh);
            KalyeahResources.Kill(_trailMesh);
            KalyeahResources.Kill(_scissorBladeAMesh);
            KalyeahResources.Kill(_scissorBladeBMesh);

            foreach (var kv in _fallbackMeshes)
                KalyeahResources.Kill(kv.Value);
            _fallbackMeshes.Clear();

            if (_ownsLitMaterial && _litMaterial != null) KalyeahResources.Kill(_litMaterial);
            if (_ownsOverlayMaterial && _overlayMaterial != null) KalyeahResources.Kill(_overlayMaterial);
        }

        static int EffectKey(WorldEffectSnapshot snap)
        {
            // Namespace by kind in high bits so Projectile/Zone/Shield/PendingCone IDs never collide.
            return ((int)snap.Kind << 24) ^ snap.Id;
        }

        EffectSlot AcquireSlot(int key, WorldEffectSnapshot snap, IReadOnlyList<KidSnapshot> kids)
        {
            EffectSlot slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : CreateEffectSlot(_activeSlots.Count + 64);
            slot.Key = key;
            slot.BoundKind = snap.Kind;
            slot.BoundWeaponId = snap.WeaponId ?? "";
            slot.BoundModelId = ResolveModelKey(slot.BoundWeaponId, snap.Look);
            slot.SpawnTime = _clock;
            slot.TotalDuration = EstimateTotalDuration(snap);
            slot.SpinAngleRad = 0f;
            slot.SpinSign = 1f;
            slot.WasReturning = snap.Returning;
            slot.LastBounces = snap.Bounces;
            slot.ReactUntil = -1f;
            slot.HasSpawnOrigin = TryResolveOrigin(snap, kids, out slot.SpawnOriginSim);

            ConfigureSlotVisuals(slot, snap);
            slot.Root.SetActive(true);
            return slot;
        }

        void ConfigureSlotVisuals(EffectSlot slot, WorldEffectSnapshot snap)
        {
            ReleaseSlotModels(slot);
            slot.RimRenderer.gameObject.SetActive(false);
            slot.FillRenderer.gameObject.SetActive(false);
            slot.TrailRenderer.gameObject.SetActive(false);
            slot.RimTr.localPosition = Vector3.zero;
            slot.RimTr.localRotation = Quaternion.identity;
            slot.RimTr.localScale = Vector3.one;
            slot.FillTr.localPosition = Vector3.zero;
            slot.FillTr.localRotation = Quaternion.identity;
            slot.FillTr.localScale = Vector3.one;
            slot.TrailTr.localPosition = Vector3.zero;
            slot.TrailTr.localRotation = Quaternion.identity;
            slot.TrailTr.localScale = Vector3.one;

            bool isEnemy = snap.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(snap.Team, snap.WeaponId);

            switch (snap.Kind)
            {
                case WorldEffectKind.Projectile:
                {
                    ModelNode m = AcquireModel(slot.BoundModelId);
                    AttachModel(slot.Tr, m);
                    slot.PrimaryModel = m;
                    SetWeaponOutlineAccent(m, teamCol, isEnemy, 0.58f);
                    TintModelIfFallback(m, FallbackTintFor(slot.BoundWeaponId, teamCol));
                    SetupProjectileTrail(slot, teamCol, isEnemy);

                    if (slot.BoundWeaponId == "tsinelas_light" || slot.BoundWeaponId == "tsinelas_heavy")
                        m.Tilt.localRotation = Quaternion.Euler(SlipperTiltDeg, 0f, 0f);

                    if (slot.BoundWeaponId == "bola")
                    {
                        float ringR = Mathf.Max(0.35f, snap.Radius);
                        slot.RimFilter.sharedMesh = _ringMesh;
                        slot.RimTr.localPosition = new Vector3(0f, TelegraphHeight, 0f);
                        slot.RimTr.localRotation = Quaternion.identity;
                        slot.RimTr.localScale = new Vector3(ringR, 1f, ringR);
                        ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.88f), isEnemy, EnemyCircleDashCount);
                        slot.RimRenderer.gameObject.SetActive(true);
                    }
                    break;
                }

                case WorldEffectKind.ExplosionZone:
                {
                    // Immediate <150 ms ground AoE hard-edge telegraph ring + clean progress fill from frame 0.
                    SetupGroundCircle(slot, Mathf.Max(0.45f, snap.Radius), teamCol, isEnemy, rimAlpha: 0.94f, fillAlpha: 0.18f);

                    if (slot.BoundWeaponId == "bato_heavy" || slot.BoundWeaponId == "ult_bagsak" || slot.BoundWeaponId == "lata")
                    {
                        string modelKey = slot.BoundWeaponId == "lata" ? "can" : "meteor";
                        ModelNode m = AcquireModel(modelKey);
                        AttachModel(slot.Tr, m);
                        slot.PrimaryModel = m;
                        SetWeaponOutlineAccent(m, teamCol, isEnemy, 0.65f);
                        TintModelIfFallback(m, FallbackTintFor(slot.BoundWeaponId, teamCol));
                        SetupProjectileTrail(slot, teamCol, isEnemy);
                    }
                    break;
                }

                case WorldEffectKind.TrapArming:
                case WorldEffectKind.TrapArmed:
                {
                    bool armed = snap.Kind == WorldEffectKind.TrapArmed;
                    SetupGroundCircle(slot, Mathf.Max(0.40f, snap.Radius), teamCol, isEnemy, rimAlpha: armed ? 0.92f : 0.76f, fillAlpha: armed ? 0.16f : 0.20f);

                    ModelNode ball = AcquireModel("crumpled_paper");
                    AttachModel(slot.Tr, ball);
                    slot.PrimaryModel = ball;
                    SetWeaponOutlineAccent(ball, teamCol, isEnemy, armed ? 0.62f : 0.35f);
                    TintModelIfFallback(ball, new Color(0.95f, 0.93f, 0.87f, 1f));
                    break;
                }

                case WorldEffectKind.SlowZone:
                {
                    SetupGroundCircle(slot, Mathf.Max(0.50f, snap.Radius), teamCol, isEnemy, rimAlpha: 0.92f, fillAlpha: 0.18f);
                    break;
                }

                case WorldEffectKind.FieldZone:
                {
                    float fieldR = Mathf.Max(0.60f, snap.Radius);
                    SetupGroundCircle(slot, fieldR, teamCol, isEnemy, rimAlpha: 0.92f, fillAlpha: 0.16f);

                    if (slot.BoundWeaponId == "jacks" || slot.BoundModelId == "spikes")
                    {
                        float spikeScale = BaseModelMeters("jacks") * ViewScale;
                        float room = Mathf.Max(0.15f, fieldR - spikeScale * 0.6f);
                        for (int c = 0; c < JacksClustersPerField; c++)
                        {
                            ModelNode jack = AcquireModel("spikes");
                            AttachModel(slot.Tr, jack);
                            float angle = Hash01(snap.Id, c, 11) * Mathf.PI * 2f;
                            float dist = Mathf.Sqrt(Hash01(snap.Id, c, 29)) * room;
                            float yaw = Hash01(snap.Id, c, 53) * 360f;
                            jack.Tr.localPosition = new Vector3(Mathf.Cos(angle) * dist, spikeScale * 0.32f, Mathf.Sin(angle) * dist);
                            jack.Tr.localScale = Vector3.one * spikeScale;
                            jack.Spin.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            SetWeaponOutlineAccent(jack, teamCol, isEnemy, 0.55f);
                            ApplyColorToModel(jack, JacksClusterColors[c % JacksClusterColors.Length], forceTint: true);
                            slot.SubModels[slot.SubModelCount++] = jack;
                        }
                    }
                    break;
                }

                case WorldEffectKind.ShieldWall:
                {
                    bool isSoil = snap.Look == "soil" || slot.BoundWeaponId == "langit_lupa";
                    slot.RimFilter.sharedMesh = _ribbonQuadMesh;
                    slot.RimTr.localPosition = new Vector3(0f, TelegraphHeight, 0f);
                    slot.RimTr.localRotation = Quaternion.identity;
                    ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.94f), isEnemy, EnemyRibbonDashCount);
                    slot.RimRenderer.gameObject.SetActive(true);

                    if (isSoil)
                    {
                        ModelNode soil = AcquireModel("soil_block");
                        AttachModel(slot.Tr, soil);
                        slot.PrimaryModel = soil;
                        SetWeaponOutlineAccent(soil, teamCol, isEnemy, 0.55f);
                        TintModelIfFallback(soil, new Color(0.54f, 0.39f, 0.25f, 1f));
                    }
                    else
                    {
                        // Curved papel_shield wall: smooth bowed paper arc + optional glTFast paper sheets along the curve.
                        slot.FillFilter.sharedMesh = _curvedShieldMesh;
                        slot.FillTr.localPosition = Vector3.zero;
                        slot.FillTr.localRotation = Quaternion.identity;
                        Color paperTint = Color.Lerp(new Color(0.97f, 0.95f, 0.90f, 0.95f), teamCol, 0.16f);
                        ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, paperTint);
                        slot.FillRenderer.gameObject.SetActive(true);

                        if (_prefabTemplates.ContainsKey("paper"))
                        {
                            ModelNode sheet = AcquireModel("paper");
                            AttachModel(slot.Tr, sheet);
                            slot.PrimaryModel = sheet;
                            SetWeaponOutlineAccent(sheet, teamCol, isEnemy, 0.55f);
                        }
                    }
                    break;
                }

                case WorldEffectKind.PendingCone:
                {
                    slot.RimFilter.sharedMesh = _wedgeRimMesh;
                    slot.FillFilter.sharedMesh = _wedgeFillMesh;
                    slot.RimTr.localPosition = new Vector3(0f, TelegraphHeight + 0.002f, 0f);
                    slot.FillTr.localPosition = new Vector3(0f, TelegraphHeight, 0f);
                    ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.94f), isEnemy, EnemyConeDashCount);
                    ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, WithAlpha(teamCol, 0.18f));
                    slot.RimRenderer.gameObject.SetActive(true);
                    slot.FillRenderer.gameObject.SetActive(true);

                    if (slot.BoundWeaponId == "gunting_heavy" || slot.BoundWeaponId == "gunting_light")
                    {
                        ModelNode sc = AcquireModel("scissor");
                        AttachModel(slot.Tr, sc);
                        slot.PrimaryModel = sc;
                        SetWeaponOutlineAccent(sc, teamCol, isEnemy, 0.65f);
                        TintModelIfFallback(sc, new Color(0.84f, 0.88f, 0.93f, 1f));
                    }
                    break;
                }
            }
        }

        void HandleKindTransition(EffectSlot slot, WorldEffectSnapshot snap)
        {
            if (slot.BoundKind == WorldEffectKind.TrapArming && snap.Kind == WorldEffectKind.TrapArmed)
            {
                slot.BoundKind = snap.Kind;
                bool isEnemy = snap.Team != LocalTeam;
                Color teamCol = TelegraphColorFor(snap.Team, snap.WeaponId);
                ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.94f), isEnemy, EnemyCircleDashCount);
                ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, WithAlpha(teamCol, 0.16f));
                float r = Mathf.Max(0.40f, snap.Radius);
                slot.FillTr.localScale = new Vector3(r, 1f, r);
                return;
            }

            if ((slot.BoundKind == WorldEffectKind.TrapArmed || slot.BoundKind == WorldEffectKind.TrapArming)
                && snap.Kind == WorldEffectKind.SlowZone)
            {
                Audio?.PlayWeaponImpact("papel_trap");
                Fx?.SpawnDustPuff(snap.Position, 1.05f);
            }

            slot.BoundKind = snap.Kind;
            slot.BoundWeaponId = snap.WeaponId ?? "";
            slot.BoundModelId = ResolveModelKey(slot.BoundWeaponId, snap.Look);
            slot.SpawnTime = _clock;
            slot.TotalDuration = EstimateTotalDuration(snap);
            ConfigureSlotVisuals(slot, snap);
        }

        void UpdateSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos, float dt)
        {
            switch (snap.Kind)
            {
                case WorldEffectKind.Projectile:
                    UpdateProjectileSlot(slot, snap, simPos, dt);
                    break;
                case WorldEffectKind.ExplosionZone:
                    UpdateExplosionZoneSlot(slot, snap, simPos);
                    break;
                case WorldEffectKind.TrapArming:
                case WorldEffectKind.TrapArmed:
                    UpdateTrapSlot(slot, snap, simPos);
                    break;
                case WorldEffectKind.SlowZone:
                case WorldEffectKind.FieldZone:
                    UpdateAreaZoneSlot(slot, snap, simPos);
                    break;
                case WorldEffectKind.ShieldWall:
                    UpdateShieldWallSlot(slot, snap, simPos);
                    break;
                case WorldEffectKind.PendingCone:
                    UpdatePendingConeSlot(slot, snap, simPos);
                    break;
            }
        }

        void UpdateProjectileSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos, float dt)
        {
            Vector3 worldGround = KalyeahResources.World(simPos, 0f);
            Vector3 worldDir = WorldDirection(snap.Direction);
            float scaleMeters = BaseModelMeters(slot.BoundWeaponId) * ViewScale;

            slot.Tr.position = worldGround;
            slot.Tr.rotation = worldDir.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(worldDir, Vector3.up) : Quaternion.identity;

            ModelNode m = slot.PrimaryModel;
            if (m == null) return;
            m.Tr.localScale = Vector3.one * scaleMeters;

            float age = _clock - slot.SpawnTime;

            if (slot.BoundWeaponId == "trumpo")
            {
                // Spinning wooden top on its tip: drops in quickly, spins at 6 rev/s with gentle street wobble.
                float dropY = age < TrumpoDropSeconds ? (1f - age / TrumpoDropSeconds) * 0.55f : 0f;
                m.Tr.localPosition = new Vector3(0f, dropY + scaleMeters * 0.5f, 0f);
                if (!ReduceMotion)
                {
                    slot.SpinAngleRad += dt * Mathf.PI * 2f * TrumpoSpinRev;
                    float wobble = Mathf.Sin(_clock * 11f + snap.Id) * TrumpoWobbleDeg;
                    m.Tilt.localRotation = Quaternion.Euler(wobble, 0f, wobble * 0.6f);
                    m.Spin.localRotation = Quaternion.Euler(0f, slot.SpinAngleRad * Mathf.Rad2Deg, 0f);
                }
                else
                {
                    m.Tilt.localRotation = Quaternion.identity;
                    m.Spin.localRotation = Quaternion.identity;
                }
            }
            else if (slot.BoundWeaponId == "bola")
            {
                // Bouncing glass marble: parabolic arcs across bounce segments + sound/dust on each bounce touchdown.
                if (snap.Bounces > slot.LastBounces)
                {
                    slot.LastBounces = snap.Bounces;
                    Audio?.PlayWeaponImpact("bola");
                    Fx?.SpawnDustPuff(simPos, 0.78f);
                }

                int segCount = 3;
                float segLen = Mathf.Max(1.2f, (snap.MaxRange > 0.2f ? snap.MaxRange : 9.0f) / segCount);
                float segProgress = Mathf.Clamp01((snap.Travelled - snap.Bounces * segLen) / segLen);
                float arcY = Mathf.Sin(segProgress * Mathf.PI) * BolaArcPeak * ViewScale;
                float startLift = snap.Bounces == 0 ? (1f - segProgress) * BolaHandHeight * ViewScale : 0f;
                float centerR = scaleMeters * 0.5f;
                m.Tr.localPosition = new Vector3(0f, centerR + arcY + startLift, 0f);

                // Place upcoming bounce ring ahead on street, hiding it after the final bounce.
                if (snap.Bounces >= segCount)
                {
                    if (slot.RimRenderer.gameObject.activeSelf)
                        slot.RimRenderer.gameObject.SetActive(false);
                }
                else if (slot.RimRenderer.gameObject.activeSelf && snap.MaxRange > 0.2f && snap.Direction.LengthSquared() > 1e-4f)
                {
                    SVector2 dirNorm = SVector2.Normalize(snap.Direction);
                    SVector2 origin = simPos - dirNorm * snap.Travelled;
                    int nextSeg = Mathf.Clamp(snap.Bounces + 1, 1, segCount);
                    SVector2 nextBounce = origin + dirNorm * (segLen * nextSeg);
                    slot.RimTr.position = KalyeahResources.World(nextBounce, TelegraphHeight);
                    slot.RimTr.rotation = Quaternion.identity;
                }

                if (!ReduceMotion)
                {
                    slot.SpinAngleRad += dt * 16f;
                    m.Spin.localRotation = Quaternion.Euler(slot.SpinAngleRad * Mathf.Rad2Deg, 0f, 0f);
                }
            }
            else if (slot.BoundWeaponId == "tsinelas_light" || slot.BoundWeaponId == "tsinelas_heavy")
            {
                // Boomerang slipper: reverses spin direction + brief hang squash when turning back toward thrower.
                if (snap.Returning && !slot.WasReturning)
                {
                    slot.WasReturning = true;
                    slot.SpinSign = -slot.SpinSign;
                    slot.ReactUntil = _clock + 0.10f;
                }

                m.Tr.localPosition = new Vector3(0f, ProjectileFlightHeight * ViewScale, 0f);
                if (!ReduceMotion)
                {
                    float rate = slot.BoundWeaponId == "tsinelas_light" ? SlipperSpinLightRev : SlipperSpinHeavyRev;
                    slot.SpinAngleRad += slot.SpinSign * dt * Mathf.PI * 2f * rate;
                    m.Spin.localRotation = Quaternion.Euler(0f, slot.SpinAngleRad * Mathf.Rad2Deg, 0f);
                    float hangScale = _clock < slot.ReactUntil ? 0.82f : 1f;
                    m.Tr.localScale = new Vector3(scaleMeters / hangScale, scaleMeters * hangScale, scaleMeters / hangScale);
                }
            }
            else if (m.Scissors != null)
            {
                // Gunting (Hati-Kalye / Gupit-Kidlat) projectile: counter-rotating twin blades snipping along aim.
                m.Tr.localPosition = new Vector3(0f, 0.90f * ViewScale, 0f);
                m.Spin.localRotation = Quaternion.identity;
                float bladeDeg = ScissorsRig.Closed;
                if (!ReduceMotion)
                {
                    slot.SpinAngleRad += dt * 9f;
                    float cycle = Mathf.Repeat(slot.SpinAngleRad, 1f);
                    bladeDeg = cycle < 0.35f
                        ? Mathf.Lerp(ScissorsRig.Closed, ScissorsRig.Wide, cycle / 0.35f)
                        : ScissorsRig.CutAngle((cycle - 0.35f) / 0.65f);
                }
                m.Scissors.SetOpen(bladeDeg);
                LastScissorsAngleDegrees = bladeDeg;
            }
            else
            {
                // Default projectile (tirador_bato, bato_light, etc.): crisp flight height + rock tumble.
                m.Tr.localPosition = new Vector3(0f, ProjectileFlightHeight * ViewScale, 0f);
                if (!ReduceMotion)
                {
                    slot.SpinAngleRad += dt * Mathf.PI * 2f * RockTumbleRev;
                    m.Spin.localRotation = Quaternion.Euler(slot.SpinAngleRad * Mathf.Rad2Deg, slot.SpinAngleRad * 32f, 0f);
                }
            }

            // Riot weapon/projectile clarity: crisp smoothstep highlight response + readable team-colored edge trail across all 4 tiers.
            bool isEnemy = snap.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(snap.Team, slot.BoundWeaponId);
            float highlightSignal = ReduceMotion ? 0.65f : (0.5f + 0.5f * Mathf.Sin(slot.SpinAngleRad * 2f));
            SetWeaponOutlineAccent(m, teamCol, isEnemy, highlightSignal);
            UpdateProjectileTrail(slot, snap, m.Tr.localPosition, scaleMeters, teamCol, isEnemy);
        }

        void UpdateExplosionZoneSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos)
        {
            float radius = Mathf.Max(0.45f, snap.Radius);
            slot.Tr.position = KalyeahResources.World(simPos, 0f);
            slot.Tr.rotation = Quaternion.identity;

            float progress = ZoneProgress01(slot, snap);
            float fillR = Mathf.Max(0.04f, radius * progress);
            slot.RimTr.localScale = new Vector3(radius, 1f, radius);
            slot.FillTr.localScale = new Vector3(fillR, 1f, fillR);

            ModelNode m = slot.PrimaryModel;
            if (m == null) return;

            bool isEnemy = snap.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(snap.Team, slot.BoundWeaponId);
            SetWeaponOutlineAccent(m, teamCol, isEnemy, Mathf.Lerp(0.35f, 1.0f, progress));

            if (slot.BoundWeaponId == "lata")
            {
                // Arcing tin can from caster hand onto landing circle.
                float canSize = BaseModelMeters("lata") * ViewScale;
                Vector3 toLocal = new Vector3(0f, canSize * 0.5f, 0f);
                Vector3 fromLocal = new Vector3(0f, CanHandHeight * ViewScale, 0f);
                float peak = CanPeakBase * ViewScale;
                Vector3 bowOffset = Vector3.zero;

                if (slot.HasSpawnOrigin)
                {
                    Vector3 originWorld = KalyeahResources.World(slot.SpawnOriginSim, 0f);
                    Vector3 delta = originWorld - slot.Tr.position;
                    fromLocal = new Vector3(delta.x, CanHandHeight * ViewScale, delta.z);
                    peak += delta.magnitude * 0.22f;
                    Vector3 flatDir = new Vector3(-delta.x, 0f, -delta.z);
                    if (flatDir.sqrMagnitude > 0.01f)
                        bowOffset = Vector3.Cross(flatDir.normalized, Vector3.up) * (CanSideBow * ViewScale * Mathf.Sin(Mathf.PI * progress));
                }

                Vector3 pos = Vector3.Lerp(fromLocal, toLocal, progress);
                pos.y += 4f * peak * progress * (1f - progress);
                Vector3 finalPos = pos + bowOffset;
                Vector3 tangent = (toLocal - fromLocal) + new Vector3(0f, 4f * peak * (1f - 2f * progress), 0f);
                m.Tr.localPosition = finalPos;
                m.Tr.localScale = Vector3.one * canSize;
                m.Spin.localRotation = ReduceMotion
                    ? Quaternion.identity
                    : Quaternion.Euler(progress * 540f, 0f, 0f);
                UpdateArcingTrail(slot, finalPos, tangent, canSize * 0.45f, Mathf.Lerp(0.4f, 1.8f, SmoothStep01(0.05f, 0.35f, progress)), teamCol, isEnemy);
                return;
            }

            // bato_heavy or ult_bagsak meteor drop: ease-in sky drop onto exact landing AoE circle.
            bool isBagsak = slot.BoundWeaponId == "ult_bagsak";
            float meteorSize = BaseModelMeters(isBagsak ? "ult_bagsak" : "bato_heavy") * (isBagsak ? 1f : ViewScale);
            float dropH = isBagsak ? BagsakDropHeight : MeteorDropHeight;
            float easeIn = progress * progress;
            float remain = 1f - easeIn;
            var skyOffset = new Vector3(-1.8f * remain, dropH * remain + meteorSize * 0.5f, 1.6f * remain);
            var dropTangent = new Vector3(1.8f, -dropH, -1.6f);
            m.Tr.localPosition = skyOffset;
            m.Tr.localScale = Vector3.one * meteorSize;
            m.Spin.localRotation = ReduceMotion
                ? Quaternion.identity
                : Quaternion.Euler(progress * 480f, progress * 210f, 0f);
            UpdateArcingTrail(slot, skyOffset, dropTangent, meteorSize * 0.52f, Mathf.Lerp(0.8f, isBagsak ? 3.4f : 2.5f, progress), teamCol, isEnemy);
        }

        void UpdateTrapSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos)
        {
            float radius = Mathf.Max(0.40f, snap.Radius);
            slot.Tr.position = KalyeahResources.World(simPos, 0f);
            slot.Tr.rotation = Quaternion.identity;
            slot.RimTr.localScale = new Vector3(radius, 1f, radius);

            if (snap.Kind == WorldEffectKind.TrapArming)
            {
                float p = ZoneProgress01(slot, snap);
                slot.FillTr.localScale = new Vector3(Mathf.Max(0.04f, radius * p), 1f, Mathf.Max(0.04f, radius * p));
            }
            else
            {
                slot.FillTr.localScale = new Vector3(radius, 1f, radius);
            }

            if (slot.PrimaryModel != null)
            {
                float ballSize = BaseModelMeters("papel_trap") * ViewScale;
                float bob = ReduceMotion ? 0f : Mathf.Sin(_clock * 3.5f + snap.Id) * 0.025f;
                slot.PrimaryModel.Tr.localPosition = new Vector3(0f, ballSize * 0.45f + bob, 0f);
                slot.PrimaryModel.Tr.localScale = Vector3.one * ballSize;
            }
        }

        void UpdateAreaZoneSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos)
        {
            float radius = Mathf.Max(0.50f, snap.Radius);
            slot.Tr.position = KalyeahResources.World(simPos, 0f);
            slot.Tr.rotation = Quaternion.identity;
            slot.RimTr.localScale = new Vector3(radius, 1f, radius);
            slot.FillTr.localScale = new Vector3(radius, 1f, radius);

            float fade = snap.TimeLeft > 0f && snap.TimeLeft < 0.35f ? Mathf.Clamp01(snap.TimeLeft / 0.35f) : 1f;
            bool isEnemy = snap.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(snap.Team, snap.WeaponId);
            ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.92f * fade), isEnemy, EnemyCircleDashCount);
            ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, WithAlpha(teamCol, 0.16f * fade));
        }

        void UpdateShieldWallSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos)
        {
            Vector3 fwd = WorldDirection(snap.Direction);
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;

            slot.Tr.position = KalyeahResources.World(simPos, 0f);
            slot.Tr.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            float width = Mathf.Max(0.8f, snap.HalfWidth * 2f);
            float wallScale = Mathf.Max(ViewScale, 0.56f);
            float age = _clock - slot.SpawnTime;
            bool isSoil = snap.Look == "soil" || slot.BoundWeaponId == "langit_lupa";
            bool isEnemy = snap.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(snap.Team, snap.WeaponId);

            slot.RimTr.localScale = new Vector3(width, 1f, GroundRibbonWidth);

            if (isSoil)
            {
                float rise = ReduceMotion ? Mathf.Clamp01(age / SoilRiseSeconds) : Overshoot01(age / SoilRiseSeconds, 1.12f);
                float fall = snap.TimeLeft > 0f ? Mathf.Clamp01(snap.TimeLeft / SoilCollapseSeconds) : 1f;
                float hFactor = fall < 1f ? Mathf.Min(rise, 1f) * (1f - (1f - fall) * (1f - fall)) : rise;
                float h = Mathf.Max(0.03f, SoilWallHeight * wallScale * hFactor);
                float d = SoilWallDepth * wallScale;

                if (slot.PrimaryModel != null)
                {
                    slot.PrimaryModel.Tr.localPosition = new Vector3(0f, h * 0.5f, 0f);
                    slot.PrimaryModel.Tr.localScale = new Vector3(width, h, d);
                }
                ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.94f * fall), isEnemy, EnemyRibbonDashCount);
                return;
            }

            // Curved papel_shield wall: unfolds briskly, flutters lightly, and fades over final 0.5 s.
            float unfold = Mathf.Clamp01(age / (ReduceMotion ? 0.10f : 0.18f));
            float height = ShieldHeight * wallScale * Mathf.Lerp(0.35f, 1f, unfold);
            float alpha = snap.TimeLeft > 0f ? Mathf.Clamp01(snap.TimeLeft / ShieldFadeSeconds) : 1f;
            float flutterDeg = ReduceMotion ? 0f : Mathf.Sin(_clock * Mathf.PI * 2f * 1.5f + snap.Id) * 2.0f;

            slot.FillTr.localScale = new Vector3(width, height, 1f);
            slot.FillTr.localRotation = Quaternion.Euler(flutterDeg, 0f, 0f);
            Color paperTint = Color.Lerp(new Color(0.97f, 0.95f, 0.90f, 1f), teamCol, 0.16f);
            ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, WithAlpha(paperTint, 0.94f * alpha));
            ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(teamCol, 0.94f * alpha), isEnemy, EnemyRibbonDashCount);

            if (slot.PrimaryModel != null)
            {
                slot.PrimaryModel.Tr.localPosition = new Vector3(0f, height * 0.5f, ShieldCurveBow * 0.65f);
                slot.PrimaryModel.Tr.localScale = new Vector3(width * 0.94f, height, ShieldThickness * wallScale);
                slot.PrimaryModel.Tr.localRotation = Quaternion.Euler(flutterDeg, 0f, 0f);
            }
        }

        void UpdatePendingConeSlot(EffectSlot slot, WorldEffectSnapshot snap, SVector2 simPos)
        {
            Vector3 fwd = WorldDirection(snap.Direction);
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;

            slot.Tr.position = KalyeahResources.World(simPos, 0f);
            slot.Tr.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            float reach = Mathf.Max(1.0f, snap.Radius);
            float halfRad = Mathf.Clamp(snap.AngleDegrees > 1f ? snap.AngleDegrees : 55f, 15f, 150f) * 0.5f * Mathf.Deg2Rad;
            // Canonical wedge mesh spans 60 deg (tan 30 deg = 0.57735f).
            float widthFactor = Mathf.Clamp(Mathf.Tan(halfRad) / 0.57735f, 0.25f, 2.6f);
            float lateral = reach * widthFactor;

            float progress = ZoneProgress01(slot, snap);
            float fillFactor = Mathf.Lerp(0.06f, 1.0f, progress);

            slot.RimTr.localScale = new Vector3(lateral, 1f, reach);
            slot.FillTr.localScale = new Vector3(lateral * fillFactor, 1f, reach * fillFactor);

            if (slot.PrimaryModel != null)
            {
                float scSize = BaseModelMeters(slot.BoundWeaponId) * ViewScale;
                float pullback = ReduceMotion ? 0f : -0.32f * (1f - (1f - progress) * (1f - progress));
                float forwardOffset = Mathf.Max(0.35f, Mathf.Min(reach * 0.44f, 1.20f) + pullback);
                slot.PrimaryModel.Tr.localPosition = new Vector3(0f, 0.95f * ViewScale, forwardOffset);
                slot.PrimaryModel.Tr.localScale = Vector3.one * scSize;
                slot.PrimaryModel.Spin.localRotation = Quaternion.identity;
                if (slot.PrimaryModel.Scissors != null)
                {
                    float windupDeg = ScissorsRig.WindupAngle(progress, _clock, snap.TimeLeft, ReduceMotion);
                    slot.PrimaryModel.Scissors.SetOpen(windupDeg);
                    LastScissorsAngleDegrees = windupDeg;
                }
            }
        }

        void OnEffectRemoved(EffectSlot slot)
        {
            WorldEffectSnapshot last = slot.LastSnap;
            bool isEnemy = last.Team != LocalTeam;
            Color teamCol = TelegraphColorFor(last.Team, slot.BoundWeaponId);

            if (slot.BoundKind == WorldEffectKind.ExplosionZone)
            {
                if (slot.BoundWeaponId == "ult_bagsak")
                {
                    Fx?.SpawnGroundShockwave(last.Position, Mathf.Max(2.5f, last.Radius), teamCol, isBagsak: true, isEnemy: isEnemy);
                    Audio?.PlayWeaponImpact("ult_bagsak");
                }
                else if (slot.BoundWeaponId == "bato_heavy")
                {
                    Fx?.SpawnGroundShockwave(last.Position, Mathf.Max(1.6f, last.Radius), teamCol, isBagsak: false, isEnemy: isEnemy);
                    Audio?.PlayWeaponImpact("bato_heavy");
                }
                else if (slot.BoundWeaponId == "lata")
                {
                    Fx?.SpawnPolymorphPuff(last.Position, teamCol);
                    Audio?.PlayWeaponImpact("lata");
                }
                return;
            }

            if (slot.BoundKind == WorldEffectKind.Projectile)
            {
                CheckShieldBlockReaction(last);
                if (!ReduceMotion && (slot.BoundWeaponId == "trumpo" || slot.BoundWeaponId == "bato_light"))
                    Fx?.SpawnDustPuff(last.Position, 0.72f);
            }
        }

        void CheckShieldBlockReaction(WorldEffectSnapshot proj)
        {
            foreach (var kv in _activeSlots)
            {
                EffectSlot wall = kv.Value;
                if (wall.BoundKind != WorldEffectKind.ShieldWall || wall.LastSnap.Team == proj.Team) continue;

                SVector2 fwd = wall.LastSnap.Direction;
                SVector2 along = new SVector2(-fwd.Y, fwd.X);
                float len = along.Length();
                if (len > 1e-4f) along /= len;

                SVector2 a = wall.LastSnap.Position - along * wall.LastSnap.HalfWidth;
                SVector2 b = wall.LastSnap.Position + along * wall.LastSnap.HalfWidth;
                float dist = DistancePointToSegment(proj.Position, a, b);
                if (dist <= 0.85f)
                {
                    bool isSoil = wall.LastSnap.Look == "soil" || wall.BoundWeaponId == "langit_lupa";
                    Audio?.PlayWeaponImpact(isSoil ? "langit_lupa" : "papel_shield");
                    Fx?.SpawnHitConfirm(KalyeahResources.World(proj.Position, 0.90f), TelegraphColorFor(wall.LastSnap.Team, wall.BoundWeaponId), heavy: false);
                    break;
                }
            }
        }

        void OnWeaponCast(int casterId, string weaponId)
        {
            if (_boundBridge == null || string.IsNullOrEmpty(weaponId)) return;
            // Cast SFX is owned by MatchBoot.OnWeaponCast so each cast does not burn 2 pooled voices.

            KidSnapshot? caster = _boundBridge.Find(casterId);
            if (!caster.HasValue) return;
            KidSnapshot kid = caster.Value;
            _recentCastOrigins[casterId] = kid.Position;

            Color teamCol = TelegraphColorFor(kid.Team, weaponId);
            if (weaponId == "tirador_bato")
            {
                Fx?.SpawnMuzzleSnap(kid.Position, kid.Facing, teamCol);
                SpawnTiradorCastPop(kid.Position, kid.Facing);
            }
            else if (weaponId == "heal")
            {
                Fx?.SpawnHealRing(kid.Position, 1.5f);
            }
        }

        void OnConeTriggered(int ownerId, string weaponId, SVector2 origin, SVector2 dir, bool isWindup)
        {
            if (isWindup) return;
            int team = LocalTeam;
            float range = 3.2f;
            float angleDeg = 55f;
            if (_boundBridge != null)
            {
                KidSnapshot? k = _boundBridge.Find(ownerId);
                if (k.HasValue) team = k.Value.Team;
                WeaponInfo? info = _boundBridge.GetWeaponInfo(weaponId);
                if (info.HasValue)
                {
                    if (info.Value.MaxRange > 0.2f) range = info.Value.MaxRange;
                    if (info.Value.AngleDegrees > 1f) angleDeg = info.Value.AngleDegrees;
                }
            }

            bool heavy = weaponId == "gunting_heavy";
            bool isEnemy = team != LocalTeam;
            Color teamCol = TelegraphColorFor(team, weaponId);
            Fx?.SpawnSlashArc(origin, dir, range, angleDeg, teamCol, heavy, isEnemy);
            Audio?.PlayWeaponImpact(weaponId);
            SpawnConeStrikeTransient(origin, dir, range, angleDeg, teamCol, weaponId, isEnemy);
        }

        void OnPlayerHealed(int playerId, int amount)
        {
            if (_boundBridge == null || amount <= 0) return;
            KidSnapshot? k = _boundBridge.Find(playerId);
            if (!k.HasValue) return;
            Fx?.SpawnHealRing(k.Value.Position, 1.5f);
            SpawnHealCrossTransient(k.Value.Position);
        }

        void OnPlayerDamaged(int playerId, int amount)
        {
            if (_boundBridge == null || amount <= 0) return;
            KidSnapshot? k = _boundBridge.Find(playerId);
            if (!k.HasValue) return;
            Fx?.SpawnHitConfirm(k.Value.Position, null, heavy: amount >= 28 || k.Value.Tagged);
        }

        void OnPlayerDashed(int playerId)
        {
            if (_boundBridge == null) return;
            KidSnapshot? k = _boundBridge.Find(playerId);
            if (!k.HasValue) return;
            Fx?.SpawnCleanseSpark(k.Value.Position, k.Value.DashDir);
        }

        void OnUltimateCast(int casterId)
        {
            if (_boundBridge == null) return;
            KidSnapshot? k = _boundBridge.Find(casterId);
            if (k.HasValue)
                _recentCastOrigins[casterId] = k.Value.Position;
        }

        void SpawnTiradorCastPop(SVector2 casterPos, SVector2 facing)
        {
            if (_freeTransients.Count == 0) return;
            TransientVisual tv = _freeTransients.Pop();
            Vector3 fwd = WorldDirection(facing);
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;

            tv.GroundRenderer.gameObject.SetActive(false);
            tv.Model = AcquireModel("tirador");
            AttachModel(tv.Tr, tv.Model);
            float sz = BaseModelMeters("tirador") * ViewScale;
            tv.Model.Tr.localPosition = Vector3.zero;
            tv.Model.Tr.localScale = Vector3.one * sz;
            SetWeaponOutlineAccent(tv.Model, WeaponInkBase);
            TintModelIfFallback(tv.Model, new Color(0.62f, 0.42f, 0.22f, 1f));

            tv.StartPos = KalyeahResources.World(casterPos, 0.95f * ViewScale) + fwd * 0.36f;
            tv.Velocity = Vector3.zero;
            tv.Tr.position = tv.StartPos;
            tv.Tr.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            tv.Elapsed = 0f;
            tv.Duration = 0.14f;
            tv.IsCone = false;
            tv.IsScissors = false;
            tv.Root.SetActive(true);
            _liveTransients.Add(tv);
        }

        void SpawnHealCrossTransient(SVector2 pos)
        {
            if (_freeTransients.Count == 0) return;
            TransientVisual tv = _freeTransients.Pop();
            tv.GroundRenderer.gameObject.SetActive(false);
            tv.Model = AcquireModel("green_cross");
            AttachModel(tv.Tr, tv.Model);
            float sz = BaseModelMeters("heal") * ViewScale;
            tv.Model.Tr.localPosition = Vector3.zero;
            tv.Model.Tr.localScale = Vector3.one * sz;
            SetWeaponOutlineAccent(tv.Model, HealGreenColor);
            TintModelIfFallback(tv.Model, HealGreenColor);

            tv.StartPos = KalyeahResources.World(pos, 1.35f * ViewScale);
            tv.Velocity = ReduceMotion ? Vector3.zero : new Vector3(0f, 1.25f, 0f);
            tv.Tr.position = tv.StartPos;
            tv.Tr.rotation = Quaternion.identity;
            tv.Elapsed = 0f;
            tv.Duration = 0.32f;
            tv.IsCone = false;
            tv.IsScissors = false;
            tv.Root.SetActive(true);
            _liveTransients.Add(tv);
        }

        void SpawnConeStrikeTransient(SVector2 origin, SVector2 dir, float range, float angleDeg, Color teamCol, string weaponId, bool isEnemy)
        {
            if (_freeTransients.Count == 0) return;
            TransientVisual tv = _freeTransients.Pop();
            Vector3 fwd = WorldDirection(dir);
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;

            float halfRad = Mathf.Clamp(angleDeg, 15f, 150f) * 0.5f * Mathf.Deg2Rad;
            float lateral = range * Mathf.Clamp(Mathf.Tan(halfRad) / 0.57735f, 0.25f, 2.6f);

            tv.GroundFilter.sharedMesh = _wedgeFillMesh;
            tv.GroundTr.localPosition = new Vector3(0f, TelegraphHeight + 0.004f, 0f);
            tv.GroundTr.localScale = new Vector3(lateral, 1f, range);
            ApplyTelegraphFill(tv.GroundRenderer, tv.GroundBlock, WithAlpha(teamCol, 0.26f));
            tv.GroundRenderer.gameObject.SetActive(true);

            bool hasScissors = weaponId == "gunting_light" || weaponId == "gunting_heavy";
            bool heavyScissors = weaponId == "gunting_heavy";
            float sz = BaseModelMeters(weaponId) * ViewScale;
            if (hasScissors)
            {
                tv.Model = AcquireModel("scissor");
                AttachModel(tv.Tr, tv.Model);
                tv.Model.Tr.localPosition = new Vector3(0f, (heavyScissors ? 0.86f : 0.92f) * ViewScale, Mathf.Min(range * 0.48f, 1.35f));
                tv.Model.Tr.localScale = Vector3.one * sz;
                SetWeaponOutlineAccent(tv.Model, teamCol, isEnemy, 0.85f);
                TintModelIfFallback(tv.Model, new Color(0.86f, 0.90f, 0.95f, 1f));
                float startOpen = ReduceMotion
                    ? ScissorsRig.Closed
                    : (heavyScissors ? ScissorsRig.WideHeavy : ScissorsRig.Wide);
                tv.Model.Scissors?.SetOpen(startOpen);
                LastScissorsAngleDegrees = startOpen;
            }

            tv.StartPos = KalyeahResources.World(origin, 0f);
            tv.Velocity = Vector3.zero;
            tv.BaseModelSize = sz;
            tv.Tr.position = tv.StartPos;
            tv.Tr.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            tv.Elapsed = 0f;
            tv.Duration = hasScissors ? (heavyScissors ? 0.30f : 0.36f) : 0.24f;
            tv.IsCone = true;
            tv.IsScissors = hasScissors;
            tv.IsHeavyScissors = heavyScissors;
            tv.Root.SetActive(true);
            _liveTransients.Add(tv);
        }

        void TickTransients(float dt)
        {
            for (int i = _liveTransients.Count - 1; i >= 0; i--)
            {
                TransientVisual tv = _liveTransients[i];
                tv.Elapsed += dt;
                if (tv.Elapsed >= tv.Duration)
                {
                    RecycleTransientAt(i);
                    continue;
                }

                float t = Mathf.Clamp01(tv.Elapsed / tv.Duration);
                tv.Tr.position = tv.StartPos + tv.Velocity * tv.Elapsed;
                if (tv.IsScissors && tv.Model != null)
                {
                    PoseTransientScissors(tv, t);
                }
            }
        }

        void PoseTransientScissors(TransientVisual tv, float t)
        {
            float baseSz = Mathf.Max(0.05f, tv.BaseModelSize);
            if (ReduceMotion)
            {
                tv.Model.Tr.localScale = Vector3.one * baseSz;
                if (tv.Model.Scissors != null)
                {
                    tv.Model.Scissors.SetOpen(ScissorsRig.Closed);
                    LastScissorsAngleDegrees = ScissorsRig.Closed;
                }
                return;
            }

            float bladeDeg;
            float sideX = 0f;
            float lungeZ = 0f;
            float liftY = 0f;
            float scaleMul = 1f;

            if (tv.IsHeavyScissors)
            {
                // Gupit-Kidlat (gunting_heavy): wide blades snap shut on strike with forward lunge, hold shut, then lift & shrink.
                if (t < 0.24f)
                {
                    float u = t / 0.24f;
                    bladeDeg = Mathf.Lerp(ScissorsRig.WideHeavy, ScissorsRig.Closed, u * u);
                    lungeZ = Mathf.Lerp(0f, 0.34f, 1f - (1f - u) * (1f - u));
                }
                else
                {
                    bladeDeg = ScissorsRig.Closed;
                    lungeZ = 0.34f;
                    if (t > 0.72f)
                    {
                        float exit = (t - 0.72f) / 0.28f;
                        scaleMul = Mathf.Max(0.02f, 1f - exit);
                        liftY = 0.24f * exit;
                    }
                }
            }
            else
            {
                // Hati-Kalye (gunting_light): alternating right/left shoulder twin-blade snip volley closing Wide -> Closed.
                if (t < 0.39f)
                {
                    float u = t / 0.39f;
                    bladeDeg = ScissorsRig.CutAngle(u);
                    sideX = Mathf.Lerp(0.18f, 0.03f, 1f - (1f - u) * (1f - u));
                    lungeZ = Mathf.Lerp(-0.22f, 0.08f, 1f - (1f - u) * (1f - u));
                }
                else if (t < 0.78f)
                {
                    float u = (t - 0.39f) / 0.39f;
                    bladeDeg = u < 0.22f
                        ? Mathf.Lerp(ScissorsRig.Closed, ScissorsRig.Wide, u / 0.22f)
                        : ScissorsRig.CutAngle((u - 0.22f) / 0.78f);
                    sideX = Mathf.Lerp(-0.18f, -0.03f, u);
                    lungeZ = Mathf.Lerp(-0.14f, 0.14f, u);
                }
                else
                {
                    bladeDeg = ScissorsRig.Closed;
                    lungeZ = 0.14f;
                    float exit = (t - 0.78f) / 0.22f;
                    scaleMul = Mathf.Max(0.02f, 1f - exit);
                    liftY = 0.22f * exit;
                }
            }

            float baseY = (tv.IsHeavyScissors ? 0.86f : 0.92f) * ViewScale;
            float baseZ = Mathf.Max(0.45f, tv.GroundTr.localScale.z * 0.48f);
            baseZ = Mathf.Min(baseZ, 1.35f);
            tv.Model.Tr.localPosition = new Vector3(sideX, baseY + liftY, baseZ + lungeZ);
            tv.Model.Tr.localScale = Vector3.one * (baseSz * scaleMul);
            if (tv.Model.Scissors != null)
            {
                tv.Model.Scissors.SetOpen(bladeDeg);
                LastScissorsAngleDegrees = bladeDeg;
            }
        }

        void RecycleTransientAt(int index)
        {
            TransientVisual tv = _liveTransients[index];
            int last = _liveTransients.Count - 1;
            _liveTransients[index] = _liveTransients[last];
            _liveTransients.RemoveAt(last);

            if (tv.Model != null)
            {
                ReleaseModel(tv.Model);
                tv.Model = null;
            }
            tv.GroundRenderer.gameObject.SetActive(false);
            tv.Root.SetActive(false);
            _freeTransients.Push(tv);
        }

        bool TryResolveOrigin(WorldEffectSnapshot snap, IReadOnlyList<KidSnapshot> kids, out SVector2 origin)
        {
            if (_recentCastOrigins.TryGetValue(snap.OwnerId, out origin))
                return true;
            if (kids != null)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (kids[i].Id == snap.OwnerId)
                    {
                        origin = kids[i].Position;
                        return true;
                    }
                }
            }
            origin = snap.Position;
            return false;
        }

        static float EstimateTotalDuration(WorldEffectSnapshot snap)
        {
            if (snap.Duration > 0.05f) return snap.Duration;
            if (snap.TimeLeft > 0.05f) return snap.TimeLeft;
            if (snap.WeaponId == "ult_bagsak") return 0.45f;
            if (snap.WeaponId == "bato_heavy") return 0.60f;
            if (snap.WeaponId == "lata") return 0.55f;
            if (snap.Kind == WorldEffectKind.TrapArming) return 0.50f;
            if (snap.Kind == WorldEffectKind.PendingCone) return 0.45f;
            return 0.40f;
        }

        float ZoneProgress01(EffectSlot slot, WorldEffectSnapshot snap)
        {
            float span = Mathf.Max(0.05f, slot.TotalDuration);
            if (snap.TimeLeft > 0f && snap.TimeLeft <= span)
                return Mathf.Clamp01((span - snap.TimeLeft) / span);
            return Mathf.Clamp01((_clock - slot.SpawnTime) / span);
        }

        Color TelegraphColorFor(int team, string weaponId)
        {
            Color baseCol = team == LocalTeam ? AllyTelegraphColor : ResolveEnemyTelegraphColor();
            return weaponId == "ult_bagsak" ? Color.Lerp(baseCol, BagsakGoldAccent, 0.55f) : baseCol;
        }

        static Color ResolveEnemyTelegraphColor()
        {
            SettingsStore settings = SettingsStore.Current;
            if (settings != null && settings.ResolvedEnemyHighlightMode != SettingsStore.EnemyHighlightRed)
            {
                Color c = KalyeahResources.ResolveEnemyHighlightColor(settings.ResolvedEnemyHighlightMode);
                return new Color(c.r, c.g, c.b, EnemyTelegraphColor.a);
            }
            return EnemyTelegraphColor;
        }

        static Color FallbackTintFor(string weaponId, Color teamCol)
        {
            switch (weaponId)
            {
                case "tirador_bato":
                case "bato_light":
                case "bato_heavy": return new Color(0.50f, 0.47f, 0.43f, 1f);
                case "ult_bagsak": return new Color(0.92f, 0.66f, 0.22f, 1f);
                case "tsinelas_light":
                case "tsinelas_heavy": return Color.Lerp(new Color(0.95f, 0.84f, 0.26f, 1f), teamCol, 0.45f);
                case "lata": return new Color(0.78f, 0.82f, 0.86f, 1f);
                case "trumpo": return new Color(0.72f, 0.46f, 0.24f, 1f);
                case "bola": return Color.Lerp(new Color(0.28f, 0.84f, 0.96f, 1f), teamCol, 0.35f);
                case "papel_trap": return new Color(0.95f, 0.93f, 0.87f, 1f);
                case "gunting_light":
                case "gunting_heavy": return new Color(0.84f, 0.88f, 0.93f, 1f);
                case "heal": return HealGreenColor;
                case "langit_lupa": return new Color(0.54f, 0.39f, 0.25f, 1f);
                default: return teamCol;
            }
        }

        void SetupGroundCircle(EffectSlot slot, float radius, Color color, bool isEnemy, float rimAlpha, float fillAlpha)
        {
            slot.RimFilter.sharedMesh = _ringMesh;
            slot.FillFilter.sharedMesh = _discMesh;
            slot.RimTr.localPosition = new Vector3(0f, TelegraphHeight + 0.002f, 0f);
            slot.FillTr.localPosition = new Vector3(0f, TelegraphHeight, 0f);
            slot.RimTr.localRotation = Quaternion.identity;
            slot.FillTr.localRotation = Quaternion.identity;
            slot.RimTr.localScale = new Vector3(radius, 1f, radius);
            slot.FillTr.localScale = new Vector3(radius, 1f, radius);
            ApplyTelegraphRim(slot.RimRenderer, slot.RimBlock, WithAlpha(color, rimAlpha), isEnemy, EnemyCircleDashCount);
            ApplyTelegraphFill(slot.FillRenderer, slot.FillBlock, WithAlpha(color, fillAlpha));
            slot.RimRenderer.gameObject.SetActive(true);
            slot.FillRenderer.gameObject.SetActive(true);
        }

        void SetupProjectileTrail(EffectSlot slot, Color teamCol, bool isEnemy)
        {
            if (slot.TrailRenderer == null) return;
            ApplyTrailStyle(slot.TrailRenderer, slot.TrailBlock, WithAlpha(teamCol, 0.92f), isEnemy);
            slot.TrailTr.localPosition = Vector3.zero;
            slot.TrailTr.localRotation = Quaternion.identity;
            slot.TrailTr.localScale = new Vector3(0.22f, 0.22f, 0.35f);
            slot.TrailRenderer.gameObject.SetActive(true);
        }

        void UpdateProjectileTrail(EffectSlot slot, WorldEffectSnapshot snap, Vector3 localModelPos, float scaleMeters, Color teamCol, bool isEnemy)
        {
            if (slot.TrailRenderer == null || !slot.TrailRenderer.gameObject.activeSelf) return;
            float maxLen = (slot.BoundWeaponId == "trumpo" || slot.BoundWeaponId == "bola") ? 1.55f : 2.35f;
            float targetLen = ReduceMotion
                ? Mathf.Min(0.95f, Mathf.Max(0.35f, snap.Travelled))
                : Mathf.Lerp(0.32f, maxLen, SmoothStep01(0.15f, maxLen, snap.Travelled));
            float width = Mathf.Clamp(scaleMeters * 0.56f, 0.16f, 0.52f);
            slot.TrailTr.localPosition = localModelPos;
            slot.TrailTr.localRotation = Quaternion.identity;
            slot.TrailTr.localScale = new Vector3(width, width, targetLen);
            ApplyTrailStyle(slot.TrailRenderer, slot.TrailBlock, WithAlpha(teamCol, 0.92f), isEnemy);
        }

        void UpdateArcingTrail(EffectSlot slot, Vector3 localModelPos, Vector3 localTangent, float width, float length, Color teamCol, bool isEnemy)
        {
            if (slot.TrailRenderer == null || !slot.TrailRenderer.gameObject.activeSelf) return;
            float clampedW = Mathf.Clamp(width, 0.18f, 0.72f);
            float clampedL = ReduceMotion ? Mathf.Min(length, 1.0f) : Mathf.Max(0.30f, length);
            slot.TrailTr.localPosition = localModelPos;
            if (localTangent.sqrMagnitude > 1e-5f)
            {
                Vector3 fwd = localTangent.normalized;
                Vector3 up = Mathf.Abs(fwd.y) > 0.96f ? Vector3.forward : Vector3.up;
                slot.TrailTr.localRotation = Quaternion.LookRotation(fwd, up);
            }
            slot.TrailTr.localScale = new Vector3(clampedW, clampedW, clampedL);
            ApplyTrailStyle(slot.TrailRenderer, slot.TrailBlock, WithAlpha(teamCol, 0.90f), isEnemy);
        }

        EffectSlot CreateEffectSlot(int index)
        {
            var go = new GameObject($"WorldEffect_{index:D2}");
            Transform tr = go.transform;
            tr.SetParent(_root, false);

            MeshRenderer rim = CreateGroundSubRenderer(tr, "Rim", _ringMesh, out MeshFilter rimFilter);
            MeshRenderer fill = CreateGroundSubRenderer(tr, "Fill", _discMesh, out MeshFilter fillFilter);
            MeshRenderer trail = CreateGroundSubRenderer(tr, "Trail", _trailMesh, out _);
            go.SetActive(false);

            return new EffectSlot
            {
                Root = go,
                Tr = tr,
                RimFilter = rimFilter,
                FillFilter = fillFilter,
                RimRenderer = rim,
                FillRenderer = fill,
                TrailRenderer = trail,
                RimTr = rim.transform,
                FillTr = fill.transform,
                TrailTr = trail.transform,
                RimBlock = new MaterialPropertyBlock(),
                FillBlock = new MaterialPropertyBlock(),
                TrailBlock = new MaterialPropertyBlock(),
            };
        }

        TransientVisual CreateTransientSlot(int index)
        {
            var go = new GameObject($"CombatFlash_{index:D2}");
            Transform tr = go.transform;
            tr.SetParent(_root, false);

            MeshRenderer ground = CreateGroundSubRenderer(tr, "FlashGround", _wedgeFillMesh, out MeshFilter groundFilter);
            go.SetActive(false);

            return new TransientVisual
            {
                Root = go,
                Tr = tr,
                GroundFilter = groundFilter,
                GroundRenderer = ground,
                GroundTr = ground.transform,
                GroundBlock = new MaterialPropertyBlock(),
            };
        }

        MeshRenderer CreateGroundSubRenderer(Transform parent, string name, Mesh initialMesh, out MeshFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = initialMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            if (_overlayMaterial != null) renderer.sharedMaterial = _overlayMaterial;
            go.SetActive(false);
            return renderer;
        }

        void ReleaseSlot(EffectSlot slot)
        {
            ReleaseSlotModels(slot);
            slot.RimRenderer.gameObject.SetActive(false);
            slot.FillRenderer.gameObject.SetActive(false);
            slot.TrailRenderer.gameObject.SetActive(false);
            slot.RimTr.localPosition = Vector3.zero;
            slot.RimTr.localRotation = Quaternion.identity;
            slot.RimTr.localScale = Vector3.one;
            slot.FillTr.localPosition = Vector3.zero;
            slot.FillTr.localRotation = Quaternion.identity;
            slot.FillTr.localScale = Vector3.one;
            slot.TrailTr.localPosition = Vector3.zero;
            slot.TrailTr.localRotation = Quaternion.identity;
            slot.TrailTr.localScale = Vector3.one;
            slot.Root.SetActive(false);
            _freeSlots.Push(slot);
        }

        void ReleaseSlotModels(EffectSlot slot)
        {
            if (slot.PrimaryModel != null)
            {
                ReleaseModel(slot.PrimaryModel);
                slot.PrimaryModel = null;
            }
            for (int i = 0; i < slot.SubModelCount; i++)
            {
                if (slot.SubModels[i] != null)
                {
                    ReleaseModel(slot.SubModels[i]);
                    slot.SubModels[i] = null;
                }
            }
            slot.SubModelCount = 0;
        }

        void PrewarmModelPools()
        {
            for (int k = 0; k < AllWeaponModelIds.Length; k++)
            {
                string modelId = AllWeaponModelIds[k];
                GameObject prefab = KalyeahResources.Model("weapons", modelId);
                if (prefab != null)
                {
                    _prefabTemplates[modelId] = prefab;
                    _prefabNaturalSizes[modelId] = MeasurePrefabSize(prefab);
                }
            }

            PrepareScissorsRigTemplate();

            for (int k = 0; k < AllWeaponModelIds.Length; k++)
            {
                string modelId = AllWeaponModelIds[k];
                var stack = new Stack<ModelNode>(DefaultModelPoolPerKind);
                _modelPools[modelId] = stack;
                for (int i = 0; i < DefaultModelPoolPerKind; i++)
                    stack.Push(CreateModelNode(modelId, i));
            }
        }

        void PrepareScissorsRigTemplate()
        {
            Mesh sourceMesh = null;
            Material bladeMat = _litMaterial;
            _scissorUsesFallback = true;

            if (_prefabTemplates.TryGetValue("scissor", out GameObject prefab) && prefab != null)
            {
                GameObject probe = Object.Instantiate(prefab, _root, false);
                KidView.SanitizeGltfMaterials(probe);
                MeshFilter mf = probe.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.vertexCount >= 6)
                {
                    sourceMesh = mf.sharedMesh;
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        bladeMat = mr.sharedMaterial;
                    _scissorUsesFallback = false;
                }
                KalyeahResources.Kill(probe);
            }

            ScissorsRig.SplitMesh(
                sourceMesh,
                out _scissorBladeAMesh,
                out _scissorBladeBMesh,
                out _scissorScrewPivot,
                out _scissorOpenSignA,
                out _scissorNaturalLength,
                out _scissorMinLengthY);
            _scissorMaterial = bladeMat;
        }

        Vector3 MeasurePrefabSize(GameObject prefab)
        {
            GameObject probe = Object.Instantiate(prefab, _root, false);
            probe.transform.localPosition = Vector3.zero;
            probe.transform.localRotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;
            Vector3 size = KalyeahResources.RenderBounds(probe).size;
            KalyeahResources.Kill(probe);
            return new Vector3(
                Mathf.Max(0.05f, size.x),
                Mathf.Max(0.05f, size.y),
                Mathf.Max(0.05f, size.z));
        }

        ModelNode AcquireModel(string modelId)
        {
            string key = _modelPools.ContainsKey(modelId) ? modelId : "meteor";
            Stack<ModelNode> pool = _modelPools[key];
            ModelNode node = pool.Count > 0 ? pool.Pop() : CreateModelNode(key, 99);
            node.Tr.localPosition = Vector3.zero;
            node.Tr.localRotation = Quaternion.identity;
            node.Tr.localScale = Vector3.one;
            node.Tilt.localRotation = Quaternion.identity;
            node.Spin.localRotation = Quaternion.identity;
            if (node.Scissors != null)
            {
                node.Scissors.SetOpen(ScissorsRig.OpenAuthored);
                LastScissorsAngleDegrees = ScissorsRig.OpenAuthored;
            }
            node.Root.SetActive(true);
            return node;
        }

        void AttachModel(Transform parent, ModelNode node)
        {
            node.Tr.SetParent(parent, false);
            node.Tr.localPosition = Vector3.zero;
            node.Tr.localRotation = Quaternion.identity;
        }

        void ReleaseModel(ModelNode node)
        {
            if (node == null || _disposed) return;
            if (!node.UsesFallbackMesh)
            {
                for (int i = 0; i < node.Renderers.Length; i++)
                    if (node.Renderers[i] != null) node.Renderers[i].SetPropertyBlock(null);
            }
            node.Root.SetActive(false);
            node.Tr.SetParent(_root, false);
            if (_modelPools.TryGetValue(node.ModelId, out Stack<ModelNode> pool))
                pool.Push(node);
        }

        ModelNode CreateModelNode(string modelId, int serial)
        {
            var holder = new GameObject($"Model_{modelId}_{serial:D2}");
            Transform holderTr = holder.transform;
            holderTr.SetParent(_root, false);

            var tiltGo = new GameObject("Tilt");
            Transform tiltTr = tiltGo.transform;
            tiltTr.SetParent(holderTr, false);

            var spinGo = new GameObject("Spin");
            Transform spinTr = spinGo.transform;
            spinTr.SetParent(tiltTr, false);

            ScissorsRig scissorsRig = null;
            bool usesFallback = true;
            if (modelId == "scissor" && _scissorBladeAMesh != null && _scissorBladeBMesh != null)
            {
                scissorsRig = new ScissorsRig(
                    spinTr,
                    _scissorBladeAMesh,
                    _scissorBladeBMesh,
                    _scissorMaterial,
                    _scissorScrewPivot,
                    _scissorOpenSignA,
                    _scissorNaturalLength,
                    _scissorMinLengthY);
                usesFallback = _scissorUsesFallback;
            }
            else if (_prefabTemplates.TryGetValue(modelId, out GameObject prefab) && prefab != null)
            {
                GameObject inst = Object.Instantiate(prefab, spinTr, false);
                KidView.SanitizeGltfMaterials(inst);
                Vector3 nat = _prefabNaturalSizes[modelId];
                bool stretchBox = modelId == "paper" || modelId == "soil_block";
                if (stretchBox)
                {
                    inst.transform.localScale = new Vector3(1f / nat.x, 1f / nat.y, 1f / nat.z);
                }
                else
                {
                    float longest = Mathf.Max(nat.x, Mathf.Max(nat.y, nat.z));
                    float norm = 1f / Mathf.Max(0.05f, longest);
                    inst.transform.localScale = Vector3.one * norm;
                }

                // Normalize glTFast -Z import axis (+180 deg yaw) and center bounds on spin pivot.
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Bounds b = KalyeahResources.RenderBounds(inst);
                inst.transform.localPosition -= spinTr.InverseTransformVector(b.center - spinTr.position);

                Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++) KalyeahResources.Kill(cols[c]);
                usesFallback = false;
            }
            else
            {
                var meshGo = new GameObject("FallbackMesh");
                meshGo.transform.SetParent(spinTr, false);
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _fallbackMeshes.TryGetValue(modelId, out Mesh fb) ? fb : _fallbackMeshes["meteor"];
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (_litMaterial != null) mr.sharedMaterial = _litMaterial;
            }

            Renderer[] renderers = holder.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].shadowCastingMode = ShadowCastingMode.Off;
                renderers[r].lightProbeUsage = LightProbeUsage.Off;
                renderers[r].reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            // Attach TeamOutlineHull AFTER sanitizing .glb materials and capturing base PBR renderers.
            Renderer[] outlineRenderers = KidView.AttachWeaponOutlines(renderers);
            var outlineBlock = new MaterialPropertyBlock();
            if (outlineRenderers.Length > 0)
            {
                outlineBlock.SetColor(PropOutlineColor, WeaponInkBase);
                outlineBlock.SetFloat(PropOutlineWidth, WeaponAllyOutlineWidth);
                for (int i = 0; i < outlineRenderers.Length; i++)
                    outlineRenderers[i].SetPropertyBlock(outlineBlock);
            }

            holder.SetActive(false);
            return new ModelNode
            {
                ModelId = modelId,
                Root = holder,
                Tr = holderTr,
                Tilt = tiltTr,
                Spin = spinTr,
                Scissors = scissorsRig,
                Renderers = renderers,
                OutlineRenderers = outlineRenderers,
                Block = new MaterialPropertyBlock(),
                OutlineBlock = outlineBlock,
                UsesFallbackMesh = usesFallback,
            };
        }

        /// <summary>
        /// Applies Riot VALORANT smoothstep weapon highlight response on thrown objects so fast-moving projectiles
        /// (<c>Bato</c>, <c>Tsinelas</c>, <c>Lata</c>, <c>Trumpo</c>, etc.) pop cleanly across all 4 quality tiers with zero GC.
        /// </summary>
        static void SetWeaponOutlineAccent(ModelNode node, Color accent, bool isEnemy = false, float highlightSignal01 = 0.55f)
        {
            if (node == null || node.OutlineRenderers == null || node.OutlineRenderers.Length == 0) return;
            float hi = SmoothStep01(0.15f, 0.85f, highlightSignal01);
            Color rim = Color.Lerp(accent, Color.white, hi * (isEnemy ? 0.34f : 0.24f));
            rim.a = 0.96f;
            node.OutlineBlock.Clear();
            node.OutlineBlock.SetColor(PropOutlineColor, rim);
            node.OutlineBlock.SetFloat(PropOutlineWidth, isEnemy ? WeaponEnemyOutlineWidth : WeaponAllyOutlineWidth);
            node.OutlineBlock.SetFloat(PropDistanceBoost, isEnemy ? 0.55f : 0.30f);
            node.OutlineBlock.SetFloat(PropHitFlash, hi * 0.22f);
            for (int i = 0; i < node.OutlineRenderers.Length; i++)
                if (node.OutlineRenderers[i] != null) node.OutlineRenderers[i].SetPropertyBlock(node.OutlineBlock);
        }

        static void TintModelIfFallback(ModelNode node, Color color)
        {
            if (node == null || !node.UsesFallbackMesh) return;
            ApplyColorToModel(node, color, forceTint: true);
        }

        static void ApplyColorToModel(ModelNode node, Color color, bool forceTint)
        {
            if (node == null || (!forceTint && !node.UsesFallbackMesh)) return;
            node.Block.Clear();
            node.Block.SetColor(PropBaseColor, color);
            node.Block.SetColor(PropLegacyColor, color);
            node.Block.SetColor(PropGltfBaseColor, color);
            for (int i = 0; i < node.Renderers.Length; i++)
                if (node.Renderers[i] != null) node.Renderers[i].SetPropertyBlock(node.Block);
        }

        static void ApplyTelegraphRim(MeshRenderer renderer, MaterialPropertyBlock block, Color color, bool isEnemy, float dashCount)
        {
            if (renderer == null || block == null) return;
            block.SetColor(PropBaseColor, color);
            block.SetColor(PropLegacyColor, color);
            block.SetColor(PropTint, Color.white);
            block.SetColor(PropInkColor, Color.clear);
            block.SetFloat(PropPhase, 0f);
            block.SetFloat(PropSoftFade, 1f);
            block.SetFloat(PropFresnel, 0f);
            block.SetFloat(PropSunlit, 0f);
            block.SetFloat(PropHardEdgeWidth, 1f);
            block.SetFloat(PropDashCount, isEnemy ? dashCount : 0f);
            block.SetFloat(PropEdgeBoost, 0.30f);
            block.SetFloat(PropInteriorAlpha, 1f);
            renderer.SetPropertyBlock(block);
        }

        static void ApplyTelegraphFill(MeshRenderer renderer, MaterialPropertyBlock block, Color color)
        {
            if (renderer == null || block == null) return;
            block.SetColor(PropBaseColor, color);
            block.SetColor(PropLegacyColor, color);
            block.SetColor(PropTint, Color.white);
            block.SetColor(PropInkColor, Color.clear);
            block.SetFloat(PropPhase, 0f);
            block.SetFloat(PropSoftFade, 1f);
            block.SetFloat(PropFresnel, 0f);
            block.SetFloat(PropSunlit, 0f);
            block.SetFloat(PropHardEdgeWidth, 0f);
            block.SetFloat(PropDashCount, 0f);
            block.SetFloat(PropEdgeBoost, 0f);
            block.SetFloat(PropInteriorAlpha, 1f);
            renderer.SetPropertyBlock(block);
        }

        static void ApplyTrailStyle(MeshRenderer renderer, MaterialPropertyBlock block, Color color, bool isEnemy)
        {
            if (renderer == null || block == null) return;
            block.SetColor(PropBaseColor, color);
            block.SetColor(PropLegacyColor, color);
            block.SetColor(PropTint, Color.white);
            block.SetColor(PropInkColor, Color.clear);
            block.SetFloat(PropPhase, 0f);
            block.SetFloat(PropSoftFade, 1f);
            block.SetFloat(PropFresnel, 0f);
            block.SetFloat(PropSunlit, 0f);
            block.SetFloat(PropHardEdgeWidth, 0.36f);
            block.SetFloat(PropDashCount, isEnemy ? EnemyTrailDashCount : 0f);
            block.SetFloat(PropEdgeBoost, 0.42f);
            block.SetFloat(PropInteriorAlpha, 0.32f);
            renderer.SetPropertyBlock(block);
        }

        static Material CreateLitMaterial(out bool owns)
        {
            Material loaded = KalyeahResources.Material("Ground", "Universal Render Pipeline/Lit");
            if (loaded != null)
            {
                owns = Resources.Load<Material>("KalyeahMaterials/Ground") == null;
                loaded.enableInstancing = true;
                return loaded;
            }
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (s != null)
            {
                owns = true;
                return new Material(s) { enableInstancing = true };
            }
            owns = false;
            return null;
        }

        static Material CreateOverlayMaterial(out bool owns)
        {
            Material arcane = KalyeahResources.Material("ArcaneFx", FxPool.ShaderName);
            if (arcane != null)
            {
                var mat = new Material(arcane) { name = "CombatTelegraphOverlay", enableInstancing = true };
                if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, Color.white);
                if (mat.HasProperty(PropTint)) mat.SetColor(PropTint, Color.white);
                if (mat.HasProperty(PropInkColor)) mat.SetColor(PropInkColor, Color.clear);
                if (mat.HasProperty(PropPhase)) mat.SetFloat(PropPhase, 0f);
                if (mat.HasProperty(PropSoftFade)) mat.SetFloat(PropSoftFade, 1f);
                if (mat.HasProperty(PropFresnel)) mat.SetFloat(PropFresnel, 0f);
                if (mat.HasProperty(PropSunlit)) mat.SetFloat(PropSunlit, 0f);
                if (mat.HasProperty(PropHardEdgeWidth)) mat.SetFloat(PropHardEdgeWidth, 1f);
                if (mat.HasProperty(PropDashCount)) mat.SetFloat(PropDashCount, 0f);
                if (mat.HasProperty(PropEdgeBoost)) mat.SetFloat(PropEdgeBoost, 0.30f);
                if (mat.HasProperty(PropInteriorAlpha)) mat.SetFloat(PropInteriorAlpha, 1f);
                mat.renderQueue = (int)RenderQueue.Transparent + 6;
                owns = true;
                return mat;
            }

            Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null)
            {
                var mat = new Material(s) { name = "CombatTelegraphOverlay", enableInstancing = true };
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.renderQueue = (int)RenderQueue.Transparent + 6;
                owns = true;
                return mat;
            }
            owns = false;
            return null;
        }

        // =============================================================================================================================
        // Procedural Fallback & Telegraph Mesh Builders (built once at match load; 0 B per-frame GC)
        // =============================================================================================================================

        void BuildFallbackMeshCatalog()
        {
            _fallbackMeshes["meteor"] = BuildSphereLikeMesh("Fb_Meteor", 8, 6, 0.50f, jitter: 0.08f);
            _fallbackMeshes["marble"] = BuildSphereLikeMesh("Fb_Marble", 12, 8, 0.50f, jitter: 0f);
            _fallbackMeshes["crumpled_paper"] = BuildSphereLikeMesh("Fb_CrumpledPaper", 7, 5, 0.48f, jitter: 0.14f);
            _fallbackMeshes["can"] = BuildCylinderMesh("Fb_Can", 12, 0.32f, 0.32f, 1.0f);
            _fallbackMeshes["trumpo"] = BuildCylinderMesh("Fb_Trumpo", 12, 0.05f, 0.38f, 1.0f);
            _fallbackMeshes["slipper"] = BuildBoxMesh("Fb_Slipper", 0.44f, 0.12f, 1.0f);
            _fallbackMeshes["paper"] = BuildBoxMesh("Fb_Paper", 1.0f, 1.0f, 1.0f);
            _fallbackMeshes["soil_block"] = BuildBeveledSoilMesh("Fb_SoilBlock");
            _fallbackMeshes["tirador"] = BuildYSlingshotMesh("Fb_Tirador");
            _fallbackMeshes["scissor"] = BuildScissorsMesh("Fb_Scissor");
            _fallbackMeshes["spikes"] = BuildJackstoneStarMesh("Fb_Spikes");
            _fallbackMeshes["green_cross"] = Build3DCrossMesh("Fb_GreenCross");
        }

        static Mesh BuildAnnulusMesh(string name, float innerR, float outerR, int segments)
        {
            // (segments + 1) ring gives seamless 0..1 perimeter uv.x for ArcaneFx solid/dashed hard edges.
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color32[vertCount];
            var tris = new int[segments * 6];
            var white = new Color32(255, 255, 255, 255);

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float a = u * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                int b = i * 2;
                verts[b] = new Vector3(c * innerR, 0f, s * innerR);
                verts[b + 1] = new Vector3(c * outerR, 0f, s * outerR);
                normals[b] = Vector3.up;
                normals[b + 1] = Vector3.up;
                uvs[b] = new Vector2(u, 1f);
                uvs[b + 1] = new Vector2(u, 1f);
                colors[b] = white;
                colors[b + 1] = white;
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                int t = i * 6;
                tris[t] = i0; tris[t + 1] = o0; tris[t + 2] = o1;
                tris[t + 3] = i0; tris[t + 4] = o1; tris[t + 5] = i1;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildDiscMesh(string name, float radius, int segments)
        {
            var verts = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var colors = new Color32[segments + 1];
            var tris = new int[segments * 3];
            var white = new Color32(255, 255, 255, 255);

            verts[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = Vector2.zero;
            colors[0] = white;

            for (int i = 0; i < segments; i++)
            {
                float a = (Mathf.PI * 2f * i) / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(i / (float)segments, 0f);
                colors[i + 1] = white;

                int next = (i + 1) % segments;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = next + 1;
                tris[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildCanonicalWedgeFillMesh(string name, int arcSteps)
        {
            // Canonical 60-degree wedge facing +Z (radius = 1.0).
            const float halfRad = 30f * Mathf.Deg2Rad;
            var verts = new Vector3[arcSteps + 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var colors = new Color32[verts.Length];
            var tris = new int[arcSteps * 3];
            var white = new Color32(255, 255, 255, 255);

            verts[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = Vector2.zero;
            colors[0] = white;

            for (int i = 0; i <= arcSteps; i++)
            {
                float a = -halfRad + (2f * halfRad * i) / arcSteps;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(i / (float)arcSteps, 0f);
                colors[i + 1] = white;
            }

            for (int i = 0; i < arcSteps; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildCanonicalWedgeRimMesh(string name, int arcSteps)
        {
            const float halfRad = 30f * Mathf.Deg2Rad;
            const float rim = 0.04f;
            var pts = new List<Vector3>(arcSteps + 3) { Vector3.zero };
            for (int i = 0; i <= arcSteps; i++)
            {
                float a = -halfRad + (2f * halfRad * i) / arcSteps;
                pts.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            pts.Add(Vector3.zero);

            float totalLen = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
                totalLen += Vector3.Distance(pts[i], pts[i + 1]);
            float invTotal = totalLen > 0.001f ? (1f / totalLen) : 1f;

            var verts = new List<Vector3>((arcSteps + 3) * 4);
            var uvs = new List<Vector2>((arcSteps + 3) * 4);
            var tris = new List<int>((arcSteps + 3) * 6);

            float accum = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float segLen = Vector3.Distance(pts[i], pts[i + 1]);
                float u0 = accum * invTotal;
                accum += segLen;
                float u1 = accum * invTotal;
                AddFlatQuadRibbon(verts, uvs, tris, pts[i], pts[i + 1], rim, u0, u1);
            }

            return FinalizeWhiteGroundMesh(name, verts, uvs, tris);
        }

        static void AddFlatQuadRibbon(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, float width, float u0, float u1)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector3 side = Vector3.Cross(Vector3.up, d.normalized) * (width * 0.5f);
            int v0 = verts.Count;
            verts.Add(a - side); uvs.Add(new Vector2(u0, 1f));
            verts.Add(a + side); uvs.Add(new Vector2(u0, 1f));
            verts.Add(b + side); uvs.Add(new Vector2(u1, 1f));
            verts.Add(b - side); uvs.Add(new Vector2(u1, 1f));
            tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 1);
            tris.Add(v0); tris.Add(v0 + 3); tris.Add(v0 + 2);
        }

        static Mesh FinalizeWhiteGroundMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var normals = new Vector3[verts.Count];
            var colors = new Color32[verts.Count];
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < verts.Count; i++)
            {
                normals[i] = Vector3.up;
                colors[i] = white;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.normals = normals;
            mesh.SetUVs(0, uvs);
            mesh.colors32 = colors;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildUnitQuadMesh(string name)
        {
            var verts = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uvs = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
            };
            var white = new Color32(255, 255, 255, 255);
            var colors = new[] { white, white, white, white };
            var tris = new[] { 0, 1, 2, 0, 2, 3 };
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildCurvedShieldMesh(string name, int segments)
        {
            // Unit width [-0.5..0.5], unit height [0..1], bowing gently toward +Z (ShieldCurveBow).
            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var colors = new Color32[verts.Length];
            var tris = new int[segments * 6];
            var white = new Color32(255, 255, 255, 255);
            var topTint = new Color32(245, 243, 236, 255);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = -0.5f + t;
                float z = Mathf.Sin(t * Mathf.PI) * ShieldCurveBow;
                verts[i * 2] = new Vector3(x, 0f, z);
                verts[i * 2 + 1] = new Vector3(x, 1f, z);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                colors[i * 2] = white;
                colors[i * 2 + 1] = topTint;
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = (i + 1) * 2, t1 = b1 + 1;
                int k = i * 6;
                tris[k] = b0; tris[k + 1] = t0; tris[k + 2] = t1;
                tris[k + 3] = b0; tris[k + 4] = t1; tris[k + 5] = b1;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Crossed dual-plane tapered projectile trail (z = 0 head -> z = -1 tail) with <c>uv.y = 1</c> outer rails and
        /// <c>uv.y = 0</c> bright core so <c>Kalyeah/ArcaneFx</c> renders razor-sharp team-colored edge trails on all 4 tiers.
        /// </summary>
        static Mesh BuildProjectileTrailMesh(string name)
        {
            const int segs = 6;
            const int slices = segs + 1;
            const int vertsPerPlane = slices * 3;
            var verts = new Vector3[vertsPerPlane * 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var colors = new Color32[verts.Length];
            var tris = new int[segs * 12 * 2];

            int triCursor = 0;
            for (int plane = 0; plane < 2; plane++)
            {
                int baseV = plane * vertsPerPlane;
                Vector3 nrm = plane == 0 ? Vector3.up : Vector3.right;
                for (int i = 0; i < slices; i++)
                {
                    float t = i / (float)segs; // 0 = tail (-Z), 1 = head (0)
                    float z = -(1f - t);
                    float halfW = Mathf.Lerp(0.10f, 0.50f, t);
                    float fade = SmoothStep01(0f, 0.35f, t);
                    byte edgeA = (byte)Mathf.RoundToInt(fade * 255f);
                    byte coreA = (byte)Mathf.RoundToInt(fade * 215f);
                    var edgeCol = new Color32(255, 255, 255, edgeA);
                    var coreCol = new Color32(255, 252, 244, coreA);

                    int row = baseV + i * 3;
                    if (plane == 0)
                    {
                        verts[row] = new Vector3(-halfW, 0f, z);
                        verts[row + 1] = new Vector3(0f, 0f, z);
                        verts[row + 2] = new Vector3(halfW, 0f, z);
                    }
                    else
                    {
                        verts[row] = new Vector3(0f, -halfW, z);
                        verts[row + 1] = new Vector3(0f, 0f, z);
                        verts[row + 2] = new Vector3(0f, halfW, z);
                    }

                    normals[row] = nrm;
                    normals[row + 1] = nrm;
                    normals[row + 2] = nrm;

                    uvs[row] = new Vector2(t, 1f);
                    uvs[row + 1] = new Vector2(t, 0f);
                    uvs[row + 2] = new Vector2(t, 1f);

                    colors[row] = edgeCol;
                    colors[row + 1] = coreCol;
                    colors[row + 2] = edgeCol;
                }

                for (int i = 0; i < segs; i++)
                {
                    int r0 = baseV + i * 3;
                    int r1 = r0 + 3;
                    tris[triCursor++] = r0; tris[triCursor++] = r1; tris[triCursor++] = r1 + 1;
                    tris[triCursor++] = r0; tris[triCursor++] = r1 + 1; tris[triCursor++] = r0 + 1;
                    tris[triCursor++] = r0 + 1; tris[triCursor++] = r1 + 1; tris[triCursor++] = r1 + 2;
                    tris[triCursor++] = r0 + 1; tris[triCursor++] = r1 + 2; tris[triCursor++] = r0 + 2;
                }
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Mesh BuildBoxMesh(string name, float sx, float sy, float sz)
        {
            float hx = sx * 0.5f, hy = sy * 0.5f, hz = sz * 0.5f;
            var verts = new List<Vector3>(24);
            var tris = new List<int>(36);
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(hx, hy, hz));
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildBeveledSoilMesh(string name)
        {
            var verts = new List<Vector3>(24);
            var tris = new List<int>(36);
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                if (v.y > 0f)
                {
                    v.x *= 0.86f;
                    v.z *= 0.78f;
                    verts[i] = v;
                }
            }
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildYSlingshotMesh(string name)
        {
            var verts = new List<Vector3>(72);
            var tris = new List<int>(108);
            AppendBoxToLists(verts, tris, new Vector3(0f, -0.22f, 0f), new Vector3(0.06f, 0.26f, 0.06f));
            AppendBoxToLists(verts, tris, new Vector3(-0.14f, 0.22f, 0f), new Vector3(0.05f, 0.24f, 0.05f));
            AppendBoxToLists(verts, tris, new Vector3(0.14f, 0.22f, 0f), new Vector3(0.05f, 0.24f, 0.05f));
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildScissorsMesh(string name)
        {
            var verts = new List<Vector3>(48);
            var tris = new List<int>(72);
            AppendRotatedBox(verts, tris, new Vector3(-0.08f, 0f, 0.05f), new Vector3(0.05f, 0.03f, 0.48f), Quaternion.Euler(0f, 16f, 0f));
            AppendRotatedBox(verts, tris, new Vector3(0.08f, 0f, 0.05f), new Vector3(0.05f, 0.03f, 0.48f), Quaternion.Euler(0f, -16f, 0f));
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildJackstoneStarMesh(string name)
        {
            var verts = new List<Vector3>(72);
            var tris = new List<int>(108);
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.50f, 0.09f, 0.09f));
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.09f, 0.50f, 0.09f));
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.09f, 0.09f, 0.50f));
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh Build3DCrossMesh(string name)
        {
            var verts = new List<Vector3>(48);
            var tris = new List<int>(72);
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.48f, 0.16f, 0.14f));
            AppendBoxToLists(verts, tris, Vector3.zero, new Vector3(0.16f, 0.48f, 0.14f));
            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildCylinderMesh(string name, int sides, float bottomR, float topR, float height)
        {
            float hy = height * 0.5f;
            var verts = new List<Vector3>((sides + 1) * 4);
            var tris = new List<int>(sides * 12);

            int sideStart = 0;
            for (int i = 0; i <= sides; i++)
            {
                float a = (Mathf.PI * 2f * (i % sides)) / sides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(new Vector3(c * bottomR, -hy, s * bottomR));
                verts.Add(new Vector3(c * topR, hy, s * topR));
            }
            for (int i = 0; i < sides; i++)
            {
                int b0 = sideStart + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.Add(b0); tris.Add(t0); tris.Add(t1);
                tris.Add(b0); tris.Add(t1); tris.Add(b1);
            }

            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, hy, 0f));
            for (int i = 0; i < sides; i++)
            {
                float a = (Mathf.PI * 2f * i) / sides;
                verts.Add(new Vector3(Mathf.Cos(a) * topR, hy, Mathf.Sin(a) * topR));
            }
            for (int i = 0; i < sides; i++)
            {
                tris.Add(topCenter);
                tris.Add(topCenter + 1 + ((i + 1) % sides));
                tris.Add(topCenter + 1 + i);
            }

            return FinalizeSolidMesh(name, verts, tris);
        }

        static Mesh BuildSphereLikeMesh(string name, int lon, int lat, float radius, float jitter)
        {
            var verts = new List<Vector3>((lon + 1) * (lat + 1));
            var tris = new List<int>(lon * lat * 6);

            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    int wrappedX = x % lon;
                    float u = (float)wrappedX / lon;
                    float theta = u * Mathf.PI * 2f;
                    float bump = jitter > 0f && y > 0 && y < lat ? (Hash01(wrappedX, y, 7) - 0.5f) * 2f * jitter : 0f;
                    float r = radius * (1f + bump);
                    verts.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * r,
                        Mathf.Cos(phi) * r,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * r));
                }
            }

            int stride = lon + 1;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int i0 = y * stride + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            return FinalizeSolidMesh(name, verts, tris);
        }

        static void AppendRotatedBox(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 halfExtents, Quaternion rot)
        {
            int start = verts.Count;
            AppendBoxToLists(verts, tris, Vector3.zero, halfExtents);
            for (int i = start; i < verts.Count; i++)
                verts[i] = center + rot * verts[i];
        }

        static void AppendBoxToLists(List<Vector3> verts, List<int> tris, Vector3 c, Vector3 h)
        {
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y,  h.z);
            Vector3 p010 = c + new Vector3(-h.x,  h.y, -h.z);
            Vector3 p011 = c + new Vector3(-h.x,  h.y,  h.z);
            Vector3 p100 = c + new Vector3( h.x, -h.y, -h.z);
            Vector3 p101 = c + new Vector3( h.x, -h.y,  h.z);
            Vector3 p110 = c + new Vector3( h.x,  h.y, -h.z);
            Vector3 p111 = c + new Vector3( h.x,  h.y,  h.z);

            AddFace(verts, tris, p001, p101, p111, p011); // +Z
            AddFace(verts, tris, p100, p000, p010, p110); // -Z
            AddFace(verts, tris, p010, p011, p111, p110); // +Y
            AddFace(verts, tris, p001, p000, p100, p101); // -Y
            AddFace(verts, tris, p101, p100, p110, p111); // +X
            AddFace(verts, tris, p000, p001, p011, p010); // -X
        }

        static void AddFace(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int v = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
        }

        static Mesh FinalizeSolidMesh(string name, List<Vector3> verts, List<int> tris)
        {
            var colors = new Color32[verts.Count];
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < colors.Length; i++) colors[i] = white;

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.colors32 = colors;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 WorldDirection(SVector2 simDir)
        {
            if (simDir.LengthSquared() < 1e-6f) return Vector3.zero;
            return new Vector3(simDir.X, 0f, -simDir.Y).normalized;
        }

        static float Overshoot01(float t, float peak)
        {
            float c = Mathf.Clamp01(t);
            if (c >= 1f) return 1f;
            return c < 0.65f
                ? Mathf.Lerp(0f, peak, c / 0.65f)
                : Mathf.Lerp(peak, 1f, (c - 0.65f) / 0.35f);
        }

        static float DistancePointToSegment(SVector2 p, SVector2 a, SVector2 b)
        {
            SVector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-6f) return SVector2.Distance(p, a);
            float t = Mathf.Clamp01(SVector2.Dot(p - a, ab) / lenSq);
            return SVector2.Distance(p, a + ab * t);
        }

        static float Hash01(int seed, int a, int b)
        {
            uint h = (uint)seed * 374761393u + (uint)a * 668265263u + (uint)b * 2147483647u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFu) / 1048576f;
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }

    /// <summary>
    /// Twin-blade articulated scissors rig (<c>scripts/view/art/scissors_rig.gd</c>) for <i>Gunting</i>
    /// (<i>Hati-Kalye</i> / <i>Gupit-Kidlat</i>). Splits <c>models/weapons/scissor.glb</c> once by triangle centroid
    /// around the pivot screw (<c>Pivot = (-0.005, 0.515)</c>, <c>PivotKeep = 0.03</c>) into Blade A (upper blade + opposite
    /// handle + screw) and Blade B (other blade + handle), offset by <c>±BladeGap</c> along the flat face normal so closed
    /// blades never z-fight, and counter-rotates <c>PivotA</c> (<c>+deg</c>) and <c>PivotB</c> (<c>-deg</c>) around the screw.
    /// </summary>
    public sealed class ScissorsRig
    {
        public const float OpenAuthored = 0f;
        public const float Closed = -13.2f;
        public const float Wide = 24.0f;
        public const float WideHeavy = 30.0f;
        public const float TipAngle = 13.2f;
        public const float PivotKeep = 0.03f;
        public const float BladeGap = 0.008f;
        public const float TrembleDegrees = 2.0f;
        public const float TrembleHz = 15.0f;

        public static readonly Vector2 Pivot = new Vector2(-0.005f, 0.515f);

        public Transform Root { get; }
        public Transform PivotA { get; }
        public Transform PivotB { get; }
        public MeshRenderer BladeA { get; }
        public MeshRenderer BladeB { get; }
        public float Angle { get; private set; }

        readonly float _openSignA;

        public ScissorsRig(
            Transform parent,
            Mesh bladeAMesh,
            Mesh bladeBMesh,
            Material material,
            Vector3 screwPivot,
            float openSignA,
            float naturalLength,
            float minLengthY = 0f)
        {
            _openSignA = Mathf.Abs(openSignA) < 0.01f ? 1f : Mathf.Sign(openSignA);
            float safeLen = Mathf.Max(0.05f, naturalLength);
            float midNormalized = (minLengthY + 0.5f * safeLen) / safeLen;

            var rootGo = new GameObject("ScissorsRig");
            Root = rootGo.transform;
            if (parent != null) Root.SetParent(parent, false);

            // Orient model frame (handles->tips along +Y, flat face +Z) so tips point along +Z aim and flat face turns +Y toward camera.
            var modelGo = new GameObject("Model");
            Transform modelTr = modelGo.transform;
            modelTr.SetParent(Root, false);
            modelTr.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            modelTr.localScale = Vector3.one * (1f / safeLen);
            modelTr.localPosition = new Vector3(0f, 0f, -midNormalized);

            float gap = BladeGap * safeLen;
            PivotA = CreateBladePivot(modelTr, "PivotA", bladeAMesh, material, screwPivot, gap, out MeshRenderer mrA);
            PivotB = CreateBladePivot(modelTr, "PivotB", bladeBMesh, material, screwPivot, -gap, out MeshRenderer mrB);
            BladeA = mrA;
            BladeB = mrB;

            SetOpen(OpenAuthored);
        }

        /// <summary>Counter-rotates Blade A (<c>+degrees</c>) and Blade B (<c>-degrees</c>) around the pivot screw.</summary>
        public void SetOpen(float degrees)
        {
            Angle = degrees;
            float zDeg = _openSignA * degrees;
            PivotA.localRotation = Quaternion.Euler(0f, 0f, zDeg);
            PivotB.localRotation = Quaternion.Euler(0f, 0f, -zDeg);
        }

        /// <summary>Ease-in snip cut curve (<c>fx_motion.gd cut_angle</c>): <see cref="Wide"/> at 0 to <see cref="Closed"/> at 1.</summary>
        public static float CutAngle(float t)
        {
            float x = Mathf.Clamp01(t);
            return Mathf.Lerp(Wide, Closed, x * x);
        }

        /// <summary>Delayed heavy cone wind-up blade curve (<c>fx_snips.gd _pose_heavy</c>): opens to <see cref="WideHeavy"/> with end tremble.</summary>
        public static float WindupAngle(float progress01, float clockSeconds, float timeLeftSeconds, bool reduceMotion)
        {
            if (reduceMotion) return WideHeavy;
            float p = Mathf.Clamp01(progress01);
            float deg = Mathf.Lerp(OpenAuthored, WideHeavy, p * p);
            if (timeLeftSeconds > 0f && timeLeftSeconds < 0.10f)
                deg += TrembleDegrees * Mathf.Sin(Mathf.PI * 2f * TrembleHz * clockSeconds);
            return deg;
        }

        /// <summary>
        /// Splits <paramref name="sourceMesh"/> into compacted <paramref name="bladeA"/> and <paramref name="bladeB"/> meshes
        /// preserving 100% of triangles, UVs, normals, tangents, and vertex colors (or builds a crisp procedural twin-blade pair
        /// when <paramref name="sourceMesh"/> is null or non-readable).
        /// </summary>
        public static void SplitMesh(
            Mesh sourceMesh,
            out Mesh bladeA,
            out Mesh bladeB,
            out Vector3 screwPivot,
            out float openSignA,
            out float naturalLength,
            out float minLengthY)
        {
            if (sourceMesh == null || !sourceMesh.isReadable || sourceMesh.vertexCount < 6)
            {
                screwPivot = new Vector3(Pivot.x, Pivot.y, 0f);
                openSignA = 1f;
                naturalLength = 1f;
                minLengthY = 0f;
                bladeA = BuildProceduralBladeHalfMesh("ScissorBladeA_Fb", side: -1f);
                bladeB = BuildProceduralBladeHalfMesh("ScissorBladeB_Fb", side: 1f);
                return;
            }

            Vector3[] verts = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Vector2[] uvs = sourceMesh.uv;
            Color32[] colors = sourceMesh.colors32;
            int[] indices = sourceMesh.triangles;

            Bounds b = sourceMesh.bounds;
            naturalLength = Mathf.Max(0.05f, b.size.y);
            minLengthY = b.min.y;
            float screwX = b.center.x;
            float screwY = Mathf.Lerp(b.min.y, b.max.y, Pivot.y);
            float screwZ = b.center.z;
            screwPivot = new Vector3(screwX, screwY, screwZ);

            // Detect whether glTFast's X-handedness flip placed the front (+Z) blade tip in dx > 0 or dx < 0.
            float frontTipDxSum = 0f;
            int frontTipCount = 0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                Vector3 c = (verts[indices[t]] + verts[indices[t + 1]] + verts[indices[t + 2]]) * (1f / 3f);
                float dy = c.y - screwY;
                if (dy > 0.08f * naturalLength && c.z >= screwZ)
                {
                    frontTipDxSum += c.x - screwX;
                    frontTipCount++;
                }
            }
            bool bladeAPositiveDiag = frontTipCount > 0 && frontTipDxSum > 0f;

            var trisA = new List<int>(indices.Length / 2 + 64);
            var trisB = new List<int>(indices.Length / 2 + 64);
            float keepR = PivotKeep * naturalLength;
            float keepSq = keepR * keepR;
            Vector3 tipSumA = Vector3.zero;
            int tipCountA = 0;

            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                Vector3 c = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                float dx = c.x - screwX;
                float dy = c.y - screwY;
                float distSq = dx * dx + dy * dy;
                bool onBladeADiagonal = bladeAPositiveDiag ? (dx * dy >= 0f) : (dx * dy < 0f);
                if (distSq < keepSq || onBladeADiagonal)
                {
                    trisA.Add(i0); trisA.Add(i1); trisA.Add(i2);
                    if (dy > 0.12f * naturalLength)
                    {
                        tipSumA += c;
                        tipCountA++;
                    }
                }
                else
                {
                    trisB.Add(i0); trisB.Add(i1); trisB.Add(i2);
                }
            }

            if (trisA.Count == 0 || trisB.Count == 0)
            {
                trisA.Clear();
                trisB.Clear();
                tipSumA = Vector3.zero;
                tipCountA = 0;
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                    Vector3 c = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                    if (c.x < screwX)
                    {
                        trisA.Add(i0); trisA.Add(i1); trisA.Add(i2);
                        if (c.y > screwY) { tipSumA += c; tipCountA++; }
                    }
                    else
                    {
                        trisB.Add(i0); trisB.Add(i1); trisB.Add(i2);
                    }
                }
            }

            openSignA = 1f;
            if (tipCountA > 0)
            {
                Vector3 tipOffset = (tipSumA / tipCountA) - screwPivot;
                Vector3 testPlus = Quaternion.Euler(0f, 0f, 1f) * tipOffset;
                openSignA = Mathf.Abs(testPlus.x) >= Mathf.Abs(tipOffset.x) ? 1f : -1f;
            }

            bladeA = CompactSubMesh("ScissorBladeA", verts, normals, tangents, uvs, colors, trisA);
            bladeB = CompactSubMesh("ScissorBladeB", verts, normals, tangents, uvs, colors, trisB);
        }

        static Transform CreateBladePivot(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Vector3 screwPivot,
            float zGap,
            out MeshRenderer renderer)
        {
            var pivotGo = new GameObject(name);
            Transform pivotTr = pivotGo.transform;
            pivotTr.SetParent(parent, false);
            pivotTr.localPosition = new Vector3(screwPivot.x, screwPivot.y, screwPivot.z + zGap);

            var bladeGo = new GameObject("Blade");
            Transform bladeTr = bladeGo.transform;
            bladeTr.SetParent(pivotTr, false);
            bladeTr.localPosition = -screwPivot;

            var mf = bladeGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            renderer = bladeGo.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (material != null) renderer.sharedMaterial = material;

            return pivotTr;
        }

        static Mesh CompactSubMesh(
            string name,
            Vector3[] verts,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            Color32[] colors,
            List<int> triIndices)
        {
            var remap = new Dictionary<int, int>(triIndices.Count / 2 + 16);
            var used = new List<int>(triIndices.Count / 2 + 16);
            var localTris = new int[triIndices.Count];

            for (int i = 0; i < triIndices.Count; i++)
            {
                int srcIdx = triIndices[i];
                if (!remap.TryGetValue(srcIdx, out int dstIdx))
                {
                    dstIdx = used.Count;
                    remap[srcIdx] = dstIdx;
                    used.Add(srcIdx);
                }
                localTris[i] = dstIdx;
            }

            int n = used.Count;
            var outVerts = new Vector3[n];
            bool hasNormals = normals != null && normals.Length == verts.Length;
            bool hasTangents = tangents != null && tangents.Length == verts.Length;
            bool hasUvs = uvs != null && uvs.Length == verts.Length;
            bool hasColors = colors != null && colors.Length == verts.Length;

            var outNormals = hasNormals ? new Vector3[n] : null;
            var outTangents = hasTangents ? new Vector4[n] : null;
            var outUvs = hasUvs ? new Vector2[n] : null;
            var outColors = new Color32[n];
            var white = new Color32(255, 255, 255, 255);

            for (int i = 0; i < n; i++)
            {
                int s = used[i];
                outVerts[i] = verts[s];
                if (hasNormals) outNormals[i] = normals[s];
                if (hasTangents) outTangents[i] = tangents[s];
                if (hasUvs) outUvs[i] = uvs[s];
                outColors[i] = hasColors ? colors[s] : white;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = outVerts;
            if (hasNormals) mesh.normals = outNormals;
            if (hasTangents) mesh.tangents = outTangents;
            if (hasUvs) mesh.uv = outUvs;
            mesh.colors32 = outColors;
            mesh.triangles = localTris;
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildProceduralBladeHalfMesh(string name, float side)
        {
            // Model frame [0..1] along +Y (handles at 0, screw at 0.515, tips at 1.0), flat face along +Z.
            var verts = new List<Vector3>(48);
            var colors = new List<Color32>(48);
            var tris = new List<int>(72);

            var steel = new Color32(214, 222, 232, 255);
            var handleRed = new Color32(226, 56, 50, 255);
            Vector3 pivot = new Vector3(Pivot.x, Pivot.y, 0f);
            Quaternion bladeRot = Quaternion.Euler(0f, 0f, -side * TipAngle);

            // Honed steel blade from tang (-0.06) to tip (+0.48)
            AppendColoredBox(verts, colors, tris, pivot + bladeRot * new Vector3(side * 0.012f, 0.21f, 0f), new Vector3(0.026f, 0.27f, 0.008f), bladeRot, steel);
            // Opposite-quadrant molded red handle neck + finger ring
            float hSide = -side;
            Quaternion neckRot = Quaternion.Euler(0f, 0f, hSide * 18f);
            AppendColoredBox(verts, colors, tris, new Vector3(Pivot.x + hSide * 0.095f, 0.28f, 0f), new Vector3(0.028f, 0.22f, 0.012f), neckRot, handleRed);
            AppendColoredBox(verts, colors, tris, new Vector3(Pivot.x + hSide * 0.155f, 0.12f, 0f), new Vector3(0.075f, 0.11f, 0.014f), Quaternion.identity, handleRed);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AppendColoredBox(
            List<Vector3> verts,
            List<Color32> colors,
            List<int> tris,
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rot,
            Color32 color)
        {
            Vector3 p000 = center + rot * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
            Vector3 p001 = center + rot * new Vector3(-halfExtents.x, -halfExtents.y,  halfExtents.z);
            Vector3 p010 = center + rot * new Vector3(-halfExtents.x,  halfExtents.y, -halfExtents.z);
            Vector3 p011 = center + rot * new Vector3(-halfExtents.x,  halfExtents.y,  halfExtents.z);
            Vector3 p100 = center + rot * new Vector3( halfExtents.x, -halfExtents.y, -halfExtents.z);
            Vector3 p101 = center + rot * new Vector3( halfExtents.x, -halfExtents.y,  halfExtents.z);
            Vector3 p110 = center + rot * new Vector3( halfExtents.x,  halfExtents.y, -halfExtents.z);
            Vector3 p111 = center + rot * new Vector3( halfExtents.x,  halfExtents.y,  halfExtents.z);

            AddColoredFace(verts, colors, tris, p001, p101, p111, p011, color);
            AddColoredFace(verts, colors, tris, p100, p000, p010, p110, color);
            AddColoredFace(verts, colors, tris, p010, p011, p111, p110, color);
            AddColoredFace(verts, colors, tris, p001, p000, p100, p101, color);
            AddColoredFace(verts, colors, tris, p101, p100, p110, p111, color);
            AddColoredFace(verts, colors, tris, p000, p001, p011, p010, color);
        }

        static void AddColoredFace(
            List<Vector3> verts,
            List<Color32> colors,
            List<int> tris,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Color32 col)
        {
            int v = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
        }
    }
}
