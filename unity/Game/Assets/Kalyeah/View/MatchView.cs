using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Builds and syncs the 3D barangay match arena: TropicalSky + sunlit afternoon lights, Estero canal + 3 footbridges + landmarks,
    /// non-repeating BarangayGround + spaghetti wires + curbs/props + ObjectiveZonesView, placed barangay kit obstacles (recording
    /// camera occluders), pooled FxPool, ProjectileViews, AimIndicatorView, and 10 interpolated KidView puppets.
    /// </summary>
    public sealed class MatchView : IDisposable
    {
        static readonly Vector2 SunAngles = new Vector2(48f, 38f);
        static readonly Color SunColor = new Color(1f, 0.91f, 0.78f);
        const float SunIntensity = 1.25f;
        static readonly Vector2 FillAngles = new Vector2(34f, -142f);
        static readonly Color FillColor = new Color(0.62f, 0.72f, 0.95f);
        const float FillIntensity = 0.25f;
        const float HitFlashDuration = 0.18f;

        const int MaxWallSlots = 10;
        const int MaxTrapSlots = 16;
        const int MaxSentrySlots = 8;
        const int MaxTetherSlots = 6;
        const int MaxSkillZoneSlots = 24;
        const int MaxRevealMarkerSlots = 16;

        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropColor = Shader.PropertyToID("_Color");
        static readonly int PropTint = Shader.PropertyToID("_Tint");
        static readonly int PropInkColor = Shader.PropertyToID("_InkColor");
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");
        static readonly int PropHardEdgeWidth = Shader.PropertyToID("_HardEdgeWidth");
        static readonly int PropDashCount = Shader.PropertyToID("_DashCount");
        static readonly int PropEdgeBoost = Shader.PropertyToID("_EdgeBoost");
        static readonly int PropInteriorAlpha = Shader.PropertyToID("_InteriorAlpha");

        static readonly Color HeadshotBurstGold = new Color(1.00f, 0.85f, 0.22f, 1f);
        static readonly Color BoxCardboardColor = new Color(0.79f, 0.62f, 0.42f, 1f);
        static readonly Color BoxCrackColor = new Color(0.18f, 0.13f, 0.11f, 0.90f);
        static readonly Color HealZoneMint = new Color(0.24f, 0.90f, 0.56f, 0.95f);
        static readonly Color RevealGold = new Color(1.00f, 0.80f, 0.24f, 0.95f);
        static readonly Color TetherMint = new Color(0.28f, 0.92f, 0.94f, 0.95f);
        static readonly Color TetherTaut = new Color(1.00f, 0.44f, 0.26f, 0.98f);
        static readonly Color BurlapTan = new Color(0.66f, 0.52f, 0.34f, 1f);
        static readonly Color BambooGreen = new Color(0.46f, 0.68f, 0.30f, 1f);

        sealed class WallSlot
        {
            public GameObject Root;
            public Transform Tr;
            public Transform BodyTr;
            public MeshRenderer BodyRend;
            public Transform StripeTr;
            public MeshRenderer StripeRend;
            public Transform CrackTr;
            public MeshRenderer CrackRend;
            public Transform HpTrackTr;
            public Transform HpFillTr;
            public MeshRenderer HpFillRend;
            public MaterialPropertyBlock Block;
            public bool Active;
        }

        sealed class TrapSlot
        {
            public GameObject Root;
            public Transform Tr;
            public Transform PropTr;
            public MeshRenderer PropRend;
            public Transform RingTr;
            public MeshRenderer RingRend;
            public Transform RevealTr;
            public MeshRenderer RevealRend;
            public MaterialPropertyBlock Block;
            public bool Active;
        }

        sealed class SentrySlot
        {
            public GameObject Root;
            public Transform Tr;
            public Transform SpinTr;
            public MeshRenderer SpinRend;
            public Transform ScanRingTr;
            public MeshRenderer ScanRingRend;
            public Transform PingRingTr;
            public MeshRenderer PingRingRend;
            public MaterialPropertyBlock Block;
            public bool Active;
        }

        sealed class TetherSlot
        {
            public GameObject Root;
            public Transform Tr;
            public MeshRenderer BeamRend;
            public MaterialPropertyBlock Block;
            public bool Active;
        }

        sealed class SkillZoneSlot
        {
            public GameObject Root;
            public Transform Tr;
            public Transform RimTr;
            public MeshRenderer RimRend;
            public Transform FillTr;
            public MeshRenderer FillRend;
            public readonly Transform[] Extras = new Transform[3];
            public readonly MeshRenderer[] ExtraRends = new MeshRenderer[3];
            public readonly MeshFilter[] ExtraFilters = new MeshFilter[3];
            public MaterialPropertyBlock Block;
            public int LastZoneId = -1;
            public float InitialDelay;
            public bool Active;
        }

        sealed class RevealMarkerSlot
        {
            public GameObject Root;
            public Transform Tr;
            public Transform PillarTr;
            public MeshRenderer PillarRend;
            public Transform DiamondTr;
            public MeshRenderer DiamondRend;
            public Transform RingTr;
            public MeshRenderer RingRend;
            public MaterialPropertyBlock Block;
            public bool Active;
        }

        public readonly Transform Root;
        public readonly MapLayoutData Layout;
        public readonly BarangayEnvironment Environment;
        public readonly ObjectiveZonesView Zones;
        public readonly FxPool Fx;
        public readonly ProjectileViews Projectiles;
        public readonly AimIndicatorView AimIndicator;
        public readonly Func<int, Vector3?> WorldFeetLookup;

        public IReadOnlyList<GameObject> Occluders => _occluders;

        public AudioHub Audio
        {
            get => _audio;
            set
            {
                _audio = value;
                if (Projectiles != null) Projectiles.Audio = value;
            }
        }

        readonly CameraRulesData _camera;
        readonly Material _unlit;
        readonly GameObject _wiresRoot;
        readonly GameObject _curbsRoot;
        readonly List<GameObject> _occluders = new List<GameObject>(128);
        readonly Dictionary<string, Vector3> _naturalSizes = new Dictionary<string, Vector3>(24);
        readonly Dictionary<int, KidView> _kids = new Dictionary<int, KidView>(12);
        readonly Dictionary<int, SVector2> _previousPositions = new Dictionary<int, SVector2>(12);
        readonly Dictionary<int, Vector3> _worldFeet = new Dictionary<int, Vector3>(12);
        readonly Dictionary<int, float> _hitFlashLeft = new Dictionary<int, float>(12);
        readonly HashSet<int> _seenIds = new HashSet<int>();
        readonly List<int> _staleIds = new List<int>(12);
        readonly WallSlot[] _wallPool = new WallSlot[MaxWallSlots];
        readonly TrapSlot[] _trapPool = new TrapSlot[MaxTrapSlots];
        readonly SentrySlot[] _sentryPool = new SentrySlot[MaxSentrySlots];
        readonly TetherSlot[] _tetherPool = new TetherSlot[MaxTetherSlots];
        readonly SkillZoneSlot[] _zonePool = new SkillZoneSlot[MaxSkillZoneSlots];
        readonly RevealMarkerSlot[] _revealPool = new RevealMarkerSlot[MaxRevealMarkerSlots];
        readonly Mesh _combatBoxMesh;
        readonly Mesh _combatRingMesh;
        readonly Mesh _combatDiscMesh;
        readonly Mesh _combatDiamondMesh;
        readonly Material _combatSolidMat;
        readonly Material _combatOverlayMat;
        readonly Transform _combatEntitiesRoot;
        float _combatClock;
        int _missingKits;

        AudioHub _audio;
        SimBridge _boundSim;
        bool _disposed;

        public MatchView(Transform parent, MapLayoutData layout, CameraRulesData camera)
            : this(parent, layout, camera, null)
        {
        }

        public MatchView(Transform parent, MapLayoutData layout, CameraRulesData camera, AudioHub audio)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _audio = audio;

            Root = new GameObject("MatchView").transform;
            if (parent != null) Root.SetParent(parent, false);

            _unlit = KalyeahResources.Material("Unlit", "Universal Render Pipeline/Unlit");
            WorldFeetLookup = TryGetWorldFeet;

            BuildLights();

            Environment = BarangayEnvironmentBuilder.Build(Root, Layout);
            BarangayEnvironmentBuilder.BuildGround(Root, Layout, Environment);
            _wiresRoot = SpaghettiWiresBuilder.Build(Root, Layout, _occluders);
            _curbsRoot = BarangayCurbsAndProps.Build(Root, Layout, _occluders);
            Zones = new ObjectiveZonesView(Root, Layout, MapLayoutData.SideOwn);

            if (Environment.Occluders != null)
            {
                for (int i = 0; i < Environment.Occluders.Count; i++)
                {
                    GameObject occ = Environment.Occluders[i];
                    if (occ != null) _occluders.Add(occ);
                }
            }

            BuildKits();
            if (_missingKits > 0)
                Debug.LogWarning($"MatchView: {_missingKits} barangay kits are missing (run tools/unity_export/sync_assets.sh)");

            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            Fx = new FxPool(Root, FxPool.DefaultCapacity, reduceMotion);
            Projectiles = new ProjectileViews(Root, Fx, _audio, _camera.KidViewScale, reduceMotion);
            AimIndicator = new AimIndicatorView(Root, SettingsStore.Current.AimGuide, reduceMotion);

            _combatEntitiesRoot = new GameObject("CombatEntities").transform;
            _combatEntitiesRoot.SetParent(Root, false);
            _combatBoxMesh = BuildUnitBoxMesh();
            _combatRingMesh = BuildFlatAnnulusMesh(0.89f, 1.00f, 32);
            _combatDiscMesh = BuildFlatDiscMesh(0.89f, 32);
            _combatDiamondMesh = BuildDiamondMesh();
            _combatSolidMat = CreateCombatSolidMaterial();
            _combatOverlayMat = CreateCombatOverlayMaterial();
            PrewarmCombatEntityPools();
        }

        public static Color TeamColor(int team) => KomiksUi.TeamColor(team, SimBridge.LocalTeam, SettingsStore.Current.ColorblindMode);

        public Vector3? TryGetWorldFeet(int playerId)
        {
            return _worldFeet.TryGetValue(playerId, out Vector3 feet) ? feet : (Vector3?)null;
        }

        /// <summary>Subscribes to SimBridge combat events for kid cast animations, hit flashes, and projectile VFX.</summary>
        public void Bind(SimBridge sim)
        {
            if (ReferenceEquals(_boundSim, sim)) return;
            Unbind();
            _boundSim = sim;
            if (_boundSim == null) return;

            Projectiles.Bind(_boundSim);
            _boundSim.WeaponCast += OnWeaponCast;
            _boundSim.UltimateCast += OnUltimateCast;
            _boundSim.WeaponHit += OnWeaponHit;
            _boundSim.HitConfirmed += OnHitConfirmed;
            _boundSim.PlayerDamaged += OnPlayerDamaged;
            _boundSim.WallBroken += OnWallBroken;
            _boundSim.TrapTriggered += OnTrapTriggered;
            _boundSim.TiradorReloadStarted += OnTiradorReloadStarted;
            _boundSim.PointReset += OnPointReset;
        }

        public void Unbind()
        {
            Projectiles?.Unbind();
            if (_boundSim == null) return;
            _boundSim.WeaponCast -= OnWeaponCast;
            _boundSim.UltimateCast -= OnUltimateCast;
            _boundSim.WeaponHit -= OnWeaponHit;
            _boundSim.HitConfirmed -= OnHitConfirmed;
            _boundSim.PlayerDamaged -= OnPlayerDamaged;
            _boundSim.WallBroken -= OnWallBroken;
            _boundSim.TrapTriggered -= OnTrapTriggered;
            _boundSim.TiradorReloadStarted -= OnTiradorReloadStarted;
            _boundSim.PointReset -= OnPointReset;
            _boundSim = null;
        }

        /// <summary>Legacy snapshot-list sync preserved for standalone callers and tests.</summary>
        public void Sync(IReadOnlyList<KidSnapshot> previous, IReadOnlyList<KidSnapshot> current, float alpha, float dt)
        {
            if (_disposed || current == null) return;

            float stepDt = Mathf.Max(0f, dt);
            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            int cbMode = SettingsStore.Current.ColorblindMode;

            SyncKidsInternal(previous, current, SimBridge.LocalTeam, -1, cbMode, alpha, stepDt, false);

            Zones.Tick(stepDt, reduceMotion);
            Fx.ReduceMotion = reduceMotion;
            Fx.Tick(stepDt);
            AimIndicator.ReduceMotion = reduceMotion;
            AimIndicator.AimGuideEnabled = SettingsStore.Current.AimGuide;
            AimIndicator.TickMoveMarker(stepDt);
        }

        /// <summary>Full match sync driving objective zones, projectiles/zones, FX pool, aim move marker, and 10 KidView puppets.</summary>
        public void Sync(
            IReadOnlyList<KidSnapshot> previous,
            SimBridge sim,
            float alpha,
            float dt,
            bool hideLocalNearCamera = false)
        {
            if (_disposed || sim == null) return;
            Bind(sim);

            float stepDt = Mathf.Max(0f, dt);
            int localTeam = sim.LocalPlayerTeam;
            int localId = sim.LocalPlayerId;
            int ownSide = localTeam == 0 ? MapLayoutData.SideOwn : MapLayoutData.SideEnemy;
            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            int cbMode = SettingsStore.Current.ColorblindMode;

            SyncZones(sim, localTeam, ownSide, stepDt, reduceMotion);

            Projectiles.ReduceMotion = reduceMotion;
            Projectiles.Sync(sim, stepDt, alpha);

            SyncCombatEntities(sim, localTeam, cbMode, stepDt, reduceMotion);

            Fx.ReduceMotion = reduceMotion;
            Fx.Tick(stepDt);

            AimIndicator.ReduceMotion = reduceMotion;
            AimIndicator.AimGuideEnabled = SettingsStore.Current.AimGuide;
            AimIndicator.TickMoveMarker(stepDt, sim.LocalKid, sim.Phase);

            SyncKidsInternal(previous, sim.Kids, localTeam, localId, cbMode, alpha, stepDt, hideLocalNearCamera);
        }

        void SyncZones(SimBridge sim, int localTeam, int ownSide, float dt, bool reduceMotion)
        {
            Zones.SetOwnSide(ownSide);

            int ownIdx = Mathf.Clamp(localTeam, 0, 1);
            int enemyIdx = 1 - ownIdx;
            bool playing = sim.Phase == MatchPhaseKind.Playing;

            IReadOnlyList<BaseObjectiveSnapshot> bases = sim.Bases;
            float ownCap01 = (playing && bases != null && ownIdx < bases.Count) ? bases[ownIdx].CaptureProgress01 : 0f;
            bool ownContested = playing && bases != null && ownIdx < bases.Count && bases[ownIdx].Contested;
            float enemyCap01 = (playing && bases != null && enemyIdx < bases.Count) ? bases[enemyIdx].CaptureProgress01 : 0f;
            bool enemyContested = playing && bases != null && enemyIdx < bases.Count && bases[enemyIdx].Contested;
            Zones.SyncBases(ownCap01, ownContested, enemyCap01, enemyContested);

            // docs/SPEC.md §5: the jail at team 0's end (SideOwn) holds and is used by team 1, and the other way round
            IReadOnlyList<RescueObjectiveSnapshot> rescues = sim.Rescues;
            float sideOwnJailCd = (rescues != null && rescues.Count > 1) ? rescues[1].CooldownLeft : 0f;
            float sideEnemyJailCd = (rescues != null && rescues.Count > 0) ? rescues[0].CooldownLeft : 0f;
            Zones.SyncRescue(sideOwnJailCd, sideEnemyJailCd, hideCounts: !playing);

            IReadOnlyList<MeriendaStandSnapshot> stands = sim.MeriendaStands;
            if (stands != null)
            {
                for (int i = 0; i < stands.Count; i++)
                {
                    MeriendaStandSnapshot s = stands[i];
                    float respawn01 = s.Active
                        ? 1f
                        : (s.RespawnTotal > 0.01f ? Mathf.Clamp01(1f - s.RespawnLeft / s.RespawnTotal) : 0f);
                    Zones.SyncMeriendaAt(i, s.Position, s.Active, respawn01, (int)s.Item);
                }
            }

            Zones.Tick(dt, reduceMotion);
        }

        void SyncKidsInternal(
            IReadOnlyList<KidSnapshot> previous,
            IReadOnlyList<KidSnapshot> current,
            int localTeam,
            int localId,
            int colorblindMode,
            float alpha,
            float dt,
            bool hideLocalNearCamera)
        {
            DecayHitFlashes(dt);

            _previousPositions.Clear();
            if (previous != null)
            {
                for (int i = 0; i < previous.Count; i++)
                {
                    KidSnapshot p = previous[i];
                    _previousPositions[p.Id] = p.Position;
                }
            }

            float blend = Mathf.Clamp01(alpha);
            _seenIds.Clear();

            for (int i = 0; i < current.Count; i++)
            {
                KidSnapshot kid = current[i];
                _seenIds.Add(kid.Id);

                Color teamColor = KomiksUi.TeamColor(kid.Team, localTeam, colorblindMode);
                bool isLocal = localId >= 0 && kid.Id == localId;
                if (!_kids.TryGetValue(kid.Id, out KidView view))
                {
                    view = new KidView(Root, kid, _camera.KidViewScale, teamColor, _unlit);
                    view.IsLocalKid = isLocal;
                    if (isLocal)
                        view.ApplyGraphicsProfile(view.ActiveGraphicsProfile);
                    _kids[kid.Id] = view;
                }
                else
                {
                    if (view.IsLocalKid != isLocal)
                    {
                        view.IsLocalKid = isLocal;
                        view.ApplyGraphicsProfile(view.ActiveGraphicsProfile);
                    }
                    view.SetTeamColor(teamColor);
                }

                SVector2 pos = _previousPositions.TryGetValue(kid.Id, out SVector2 fromPos)
                    ? SVector2.Lerp(fromPos, kid.Position, blend)
                    : kid.Position;

                _worldFeet[kid.Id] = KalyeahResources.World(pos, 0f);

                float flash01 = _hitFlashLeft.TryGetValue(kid.Id, out float flashLeft) && flashLeft > 0f
                    ? Mathf.Clamp01(flashLeft / HitFlashDuration)
                    : 0f;

                bool isEnemy = kid.Team != localTeam;
                bool hiddenEnemy = isEnemy && kid.Invisible && kid.RevealedOutlineLeft <= 0.01f;
                bool hideNear = (hideLocalNearCamera && kid.Id == localId) || hiddenEnemy;

                if (isEnemy)
                {
                    Color hl = kid.RevealedOutlineLeft > 0.01f
                        ? Color.Lerp(KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.Current.ResolvedEnemyHighlightMode), RevealGold, 0.45f)
                        : KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.Current.ResolvedEnemyHighlightMode);
                    view.SetEnemyHighlight(true, hl);
                }
                else
                {
                    view.SetEnemyHighlight(false, teamColor);
                }

                float liftY = kid.AirY + (kid.OnStilts ? 0.85f : 0f);

                bool hasAim = !kid.Tagged && kid.Facing.LengthSquared() > 0.0001f;
                if (hasAim)
                {
                    float invLen = 1f / Mathf.Sqrt(kid.Facing.LengthSquared());
                    SVector2 aimGround = pos + kid.Facing * (invLen * 8f);
                    float aimHeight = ((kid.Crouching || kid.Sliding) ? 0.82f : 1.15f) * _camera.KidViewScale + Mathf.Max(0f, liftY);
                    view.SetAimTarget(KalyeahResources.World(aimGround, aimHeight), true);
                }
                else
                {
                    view.SetAimTarget(Vector3.zero, false);
                }

                float blind01 = !kid.Tagged && kid.BlurLeft > 0.05f ? Mathf.Clamp01(kid.BlurLeft * 0.65f) : 0f;
                float stealth01 = !kid.Tagged && kid.Invisible && !isEnemy ? 0.62f : 0f;
                float ultGlow = !kid.Tagged && !string.IsNullOrEmpty(kid.CastingWeaponId)
                    && kid.CastingWeaponId.StartsWith("ult", StringComparison.OrdinalIgnoreCase)
                    ? 1f
                    : (!kid.Tagged && kid.UltCharge >= 99.5f ? 0.50f : 0f);
                float revealPulse = !kid.Tagged && (kid.RevealedOutlineLeft > 0.01f || kid.CarryingFlag) ? 0.85f : 0f;
                Color decayTint = (kid.StatusKind == StatusKind.Slow || kid.SlowMagnitude > 0.05f)
                    ? new Color(0.46f, 0.36f, 0.68f, Mathf.Clamp(0.42f + kid.SlowMagnitude * 0.35f, 0.35f, 0.72f))
                    : (!kid.Tagged && kid.ExhaustLeft > 0.05f
                        ? new Color(0.55f, 0.48f, 0.38f, 0.28f)
                        : Color.clear);
                view.SetCombatVisualStates(flash01, blind01, stealth01, ultGlow, revealPulse, decayTint);
                view.SyncState(
                    ground: pos,
                    airY: liftY,
                    facing: kid.Facing,
                    speed01: kid.Tagged ? 0f : kid.Speed01,
                    crouching: kid.Crouching,
                    sliding: kid.Sliding,
                    dashing: kid.Dashing,
                    statusKind: (int)kid.StatusKind,
                    tagged: kid.Tagged,
                    hitFlash01: flash01,
                    buffId: kid.BuffId,
                    hideNearCamera: hideNear,
                    dt: dt);
            }

            _staleIds.Clear();
            foreach (var kv in _kids)
            {
                if (!_seenIds.Contains(kv.Key))
                    _staleIds.Add(kv.Key);
            }
            for (int i = 0; i < _staleIds.Count; i++)
            {
                int gone = _staleIds[i];
                KalyeahResources.Kill(_kids[gone].Root);
                _kids.Remove(gone);
                _worldFeet.Remove(gone);
                _hitFlashLeft.Remove(gone);
            }
        }

        void DecayHitFlashes(float dt)
        {
            if (dt <= 0f || _hitFlashLeft.Count == 0) return;
            _staleIds.Clear();
            foreach (var kv in _hitFlashLeft)
                _staleIds.Add(kv.Key);

            for (int i = 0; i < _staleIds.Count; i++)
            {
                int id = _staleIds[i];
                float next = _hitFlashLeft[id] - dt;
                if (next <= 0.001f) _hitFlashLeft.Remove(id);
                else _hitFlashLeft[id] = next;
            }
        }

        void OnWeaponCast(int casterId, string _)
        {
            if (!_kids.TryGetValue(casterId, out KidView view)) return;
            KidSnapshot? who = _boundSim?.Find(casterId);
            view.TriggerCast(who.HasValue && who.Value.Speed01 > 0.1f);
        }

        void OnUltimateCast(int casterId)
        {
            if (_kids.TryGetValue(casterId, out KidView view))
                view.TriggerCast(false);

            KidSnapshot? who = _boundSim?.Find(casterId);
            if (who.HasValue)
                Fx?.SpawnGroundShockwave(who.Value.Position, 2.2f, isBagsak: true);
        }

        void OnWeaponHit(int targetId, string _)
        {
            _hitFlashLeft[targetId] = HitFlashDuration;
        }

        void OnHitConfirmed(HitConfirmInfo info)
        {
            _hitFlashLeft[info.TargetId] = HitFlashDuration;
            if (Fx != null)
            {
                if (info.IsHeadshot)
                    Fx.SpawnHitConfirm(KalyeahResources.World(info.WorldPos, 1.42f), HeadshotBurstGold, heavy: true);
                else
                    Fx.SpawnHitConfirm(KalyeahResources.World(info.WorldPos, 0.92f), null, heavy: info.IsTag);
            }
        }

        void OnWallBroken(int _, SVector2 pos)
        {
            Fx?.SpawnGroundShockwave(pos, 2.2f, BoxCardboardColor, isBagsak: false);
            Fx?.SpawnDustPuff(pos, 1.4f);
            _audio?.PlayWallBreak();
        }

        void OnTrapTriggered(int _, SVector2 pos, bool isDisarmed)
        {
            Fx?.SpawnHitConfirm(KalyeahResources.World(pos, 0.35f), isDisarmed ? RevealGold : null, heavy: false);
            _audio?.PlayTrapSnap();
        }

        void OnTiradorReloadStarted(int playerId)
        {
            if (_kids.TryGetValue(playerId, out KidView view))
                view.TriggerUpperBodyAction(isReload: true, duration: 0.36f);
        }

        void OnPlayerDamaged(int targetId, int amount)
        {
            if (amount > 0)
                _hitFlashLeft[targetId] = HitFlashDuration;
        }

        void OnPointReset()
        {
            _hitFlashLeft.Clear();
            HideAllCombatEntities();
            AimIndicator?.HideAim();
            AimIndicator?.ClearMoveMarker();
        }

        // ---- Lights & Barangay Kit Placement ------------------------------------------------------------------------------------

        void BuildLights()
        {
            BarangayEnvironmentBuilder.ApplySky();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(SunAngles.x, SunAngles.y)));

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(Root, false);
            fill.type = LightType.Directional;
            fill.color = FillColor;
            fill.intensity = FillIntensity;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(FillAngles.x, FillAngles.y)));

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.80f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.72f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.48f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 76f;
            RenderSettings.fogEndDistance = 260f;
        }

        void BuildKits()
        {
            var parent = new GameObject("Barangay").transform;
            parent.SetParent(Root, false);
            foreach (KitPlacement kit in BarangayPlan.Build(Layout))
                PlaceKit(parent, kit);
        }

        void PlaceKit(Transform parent, KitPlacement kit)
        {
            GameObject prefab = KalyeahResources.Model("barangay", kit.Kit);
            if (prefab == null)
            {
                _missingKits++;
                GameObject block = GreyBoxKit.Build(parent, kit);
                if (block != null && kit.Occluder) _occluders.Add(block);
                return;
            }

            GameObject model = UnityEngine.Object.Instantiate(prefab, parent, false);
            model.name = kit.Kit;
            KalyeahResources.SanitizeModelMaterials(model);
            Transform t = model.transform;
            Vector3 natural = NaturalSize(kit.Kit, model);
            t.localScale = KalyeahResources.U(kit.ScaleFor(new SVector3(natural.x, natural.y, natural.z)));
            t.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);
            Vector3 spot = KalyeahResources.World(kit.Ground);
            t.position = spot;
            if (kit.Fit == KitFit.Footprint)
            {
                Bounds placed = KalyeahResources.RenderBounds(model);
                t.position += new Vector3(spot.x - placed.center.x, kit.Lift - placed.min.y, spot.z - placed.center.z);
            }

            if (kit.Occluder && model != null)
                _occluders.Add(model);
        }

        Vector3 NaturalSize(string id, GameObject model)
        {
            if (_naturalSizes.TryGetValue(id, out Vector3 size)) return size;
            Transform t = model.transform;
            t.localScale = Vector3.one;
            t.rotation = Quaternion.identity;
            t.position = Vector3.zero;
            size = KalyeahResources.RenderBounds(model).size;
            _naturalSizes[id] = size;
            return size;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Unbind();
            Projectiles?.Dispose();
            AimIndicator?.Dispose();
            Fx?.Dispose();
            Zones?.Dispose();
            SpaghettiWiresBuilder.Dispose(_wiresRoot);
            BarangayCurbsAndProps.Dispose(_curbsRoot);
            Environment?.Dispose();

            _kids.Clear();
            _worldFeet.Clear();
            _hitFlashLeft.Clear();
            _occluders.Clear();

            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
            if (_combatBoxMesh != null) KalyeahResources.Kill(_combatBoxMesh);
            if (_combatRingMesh != null) KalyeahResources.Kill(_combatRingMesh);
            if (_combatDiscMesh != null) KalyeahResources.Kill(_combatDiscMesh);
            if (_combatDiamondMesh != null) KalyeahResources.Kill(_combatDiamondMesh);
            if (_combatSolidMat != null) KalyeahResources.Kill(_combatSolidMat);
            if (_combatOverlayMat != null) KalyeahResources.Kill(_combatOverlayMat);
        }

        // ---- Zero-GC Pooled World Combat Entities (Walls, Hidden Traps, Sentries, Tethers, Skill Zones, Reveal Markers) --------

        void PrewarmCombatEntityPools()
        {
            for (int i = 0; i < MaxWallSlots; i++)
            {
                var root = new GameObject("Wall_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var body = MakePart("Body", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer bodyR);
                var stripe = MakePart("Stripe", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer stripeR);
                var crack = MakePart("Crack", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer crackR);
                var hpTrack = MakePart("HpTrack", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer hpTrackR);
                var hpFill = MakePart("HpFill", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer hpFillR);
                var block = new MaterialPropertyBlock();
                SetSolidColor(hpTrackR, block, new Color(0.06f, 0.08f, 0.13f, 0.92f));
                root.SetActive(false);
                _wallPool[i] = new WallSlot
                {
                    Root = root,
                    Tr = root.transform,
                    BodyTr = body,
                    BodyRend = bodyR,
                    StripeTr = stripe,
                    StripeRend = stripeR,
                    CrackTr = crack,
                    CrackRend = crackR,
                    HpTrackTr = hpTrack,
                    HpFillTr = hpFill,
                    HpFillRend = hpFillR,
                    Block = block,
                };
            }

            for (int i = 0; i < MaxTrapSlots; i++)
            {
                var root = new GameObject("Trap_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var prop = MakePart("Prop", root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer propR);
                var ring = MakePart("Ring", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer ringR);
                var reveal = MakePart("RevealHalo", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer revR);
                root.SetActive(false);
                _trapPool[i] = new TrapSlot
                {
                    Root = root,
                    Tr = root.transform,
                    PropTr = prop,
                    PropRend = propR,
                    RingTr = ring,
                    RingRend = ringR,
                    RevealTr = reveal,
                    RevealRend = revR,
                    Block = new MaterialPropertyBlock(),
                };
            }

            for (int i = 0; i < MaxSentrySlots; i++)
            {
                var root = new GameObject("Sentry_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var spin = MakePart("TopSpire", root.transform, _combatDiamondMesh, _combatSolidMat, out MeshRenderer spinR);
                var scan = MakePart("ScanRing", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer scanR);
                var ping = MakePart("PingRing", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer pingR);
                root.SetActive(false);
                _sentryPool[i] = new SentrySlot
                {
                    Root = root,
                    Tr = root.transform,
                    SpinTr = spin,
                    SpinRend = spinR,
                    ScanRingTr = scan,
                    ScanRingRend = scanR,
                    PingRingTr = ping,
                    PingRingRend = pingR,
                    Block = new MaterialPropertyBlock(),
                };
            }

            for (int i = 0; i < MaxTetherSlots; i++)
            {
                var root = new GameObject("Tether_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var beam = MakePart("Beam", root.transform, _combatBoxMesh, _combatOverlayMat, out MeshRenderer beamR);
                beam.localPosition = new Vector3(0f, -0.5f, 0f);
                root.SetActive(false);
                _tetherPool[i] = new TetherSlot
                {
                    Root = root,
                    Tr = root.transform,
                    BeamRend = beamR,
                    Block = new MaterialPropertyBlock(),
                };
            }

            for (int i = 0; i < MaxSkillZoneSlots; i++)
            {
                var root = new GameObject("SkillZone_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var rim = MakePart("Rim", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer rimR);
                var fill = MakePart("Fill", root.transform, _combatDiscMesh, _combatOverlayMat, out MeshRenderer fillR);
                var slot = new SkillZoneSlot
                {
                    Root = root,
                    Tr = root.transform,
                    RimTr = rim,
                    RimRend = rimR,
                    FillTr = fill,
                    FillRend = fillR,
                    Block = new MaterialPropertyBlock(),
                };
                for (int e = 0; e < 3; e++)
                {
                    Transform ex = MakePart("Extra_" + e, root.transform, _combatBoxMesh, _combatSolidMat, out MeshRenderer exR);
                    ex.gameObject.SetActive(false);
                    slot.Extras[e] = ex;
                    slot.ExtraRends[e] = exR;
                    slot.ExtraFilters[e] = ex.GetComponent<MeshFilter>();
                }
                root.SetActive(false);
                _zonePool[i] = slot;
            }

            for (int i = 0; i < MaxRevealMarkerSlots; i++)
            {
                var root = new GameObject("RevealMarker_" + i);
                root.transform.SetParent(_combatEntitiesRoot, false);
                var pillar = MakePart("Pillar", root.transform, _combatBoxMesh, _combatOverlayMat, out MeshRenderer pilR);
                var diamond = MakePart("Diamond", root.transform, _combatDiamondMesh, _combatSolidMat, out MeshRenderer diaR);
                var ring = MakePart("Ring", root.transform, _combatRingMesh, _combatOverlayMat, out MeshRenderer ringR);
                root.SetActive(false);
                _revealPool[i] = new RevealMarkerSlot
                {
                    Root = root,
                    Tr = root.transform,
                    PillarTr = pillar,
                    PillarRend = pilR,
                    DiamondTr = diamond,
                    DiamondRend = diaR,
                    RingTr = ring,
                    RingRend = ringR,
                    Block = new MaterialPropertyBlock(),
                };
            }
        }

        void HideAllCombatEntities()
        {
            for (int i = 0; i < MaxWallSlots; i++) if (_wallPool[i].Active) { _wallPool[i].Active = false; _wallPool[i].Root.SetActive(false); }
            for (int i = 0; i < MaxTrapSlots; i++) if (_trapPool[i].Active) { _trapPool[i].Active = false; _trapPool[i].Root.SetActive(false); }
            for (int i = 0; i < MaxSentrySlots; i++) if (_sentryPool[i].Active) { _sentryPool[i].Active = false; _sentryPool[i].Root.SetActive(false); }
            for (int i = 0; i < MaxTetherSlots; i++) if (_tetherPool[i].Active) { _tetherPool[i].Active = false; _tetherPool[i].Root.SetActive(false); }
            for (int i = 0; i < MaxSkillZoneSlots; i++)
            {
                if (_zonePool[i].Active)
                {
                    _zonePool[i].Active = false;
                    _zonePool[i].LastZoneId = -1;
                    _zonePool[i].InitialDelay = 0f;
                    _zonePool[i].Root.SetActive(false);
                }
            }
            for (int i = 0; i < MaxRevealMarkerSlots; i++) if (_revealPool[i].Active) { _revealPool[i].Active = false; _revealPool[i].Root.SetActive(false); }
        }

        void SyncCombatEntities(SimBridge sim, int localTeam, int cbMode, float dt, bool reduceMotion)
        {
            _combatClock += dt;
            SyncWalls(sim.Walls, localTeam, cbMode);
            SyncTraps(sim.Traps, localTeam, cbMode, reduceMotion);
            SyncSentries(sim.Sentries, localTeam, cbMode, reduceMotion);
            SyncTethers(sim.Tethers, reduceMotion);
            SyncSkillZones(sim.SkillZones, localTeam, cbMode, reduceMotion);
            SyncRevealMarkers(sim.RevealMarkers, reduceMotion);
        }

        void SyncWalls(IReadOnlyList<WallSnapshot> walls, int localTeam, int cbMode)
        {
            int count = walls != null ? Mathf.Min(walls.Count, MaxWallSlots) : 0;
            for (int i = 0; i < count; i++)
            {
                WallSnapshot w = walls[i];
                WallSlot slot = _wallPool[i];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }

                Vector3 center = KalyeahResources.World(w.Center, 0f);
                Vector3 alongU = KalyeahResources.World(w.Along, 0f);
                if (alongU.sqrMagnitude < 1e-4f) alongU = Vector3.right;
                else alongU.Normalize();
                Vector3 fwdU = new Vector3(-alongU.z, 0f, alongU.x);

                slot.Tr.position = center;
                slot.Tr.rotation = Quaternion.LookRotation(fwdU, Vector3.up);

                bool isTinik = string.Equals(w.SkillId, "tinik_wall", StringComparison.Ordinal);
                float width = Mathf.Max(0.8f, w.HalfWidth * 2f);
                float age = Mathf.Max(0f, w.Duration - w.TimeLeft);
                float rise = isTinik ? Mathf.Clamp01(age / 0.14f) : Mathf.Clamp01(age / 0.10f);
                float height = (isTinik ? 2.25f : 2.10f) * Mathf.Max(0.15f, rise);
                float hp01 = w.MaxHp > 0 ? Mathf.Clamp01((float)w.Hp / w.MaxHp) : 1f;
                float dmg01 = 1f - hp01;

                Color teamCol = KomiksUi.TeamColor(w.Team, localTeam, cbMode);
                Color bodyCol = isTinik
                    ? Color.Lerp(teamCol, Color.white, 0.22f)
                    : Color.Lerp(BoxCardboardColor, BoxCrackColor, dmg01 * 0.35f);

                slot.BodyTr.localPosition = Vector3.zero;
                slot.BodyTr.localScale = new Vector3(width, height, isTinik ? 0.36f : 0.62f);
                SetSolidColor(slot.BodyRend, slot.Block, bodyCol);

                slot.StripeTr.localPosition = new Vector3(0f, height - 0.18f, 0f);
                slot.StripeTr.localScale = new Vector3(width + 0.04f, 0.16f, isTinik ? 0.42f : 0.66f);
                SetSolidColor(slot.StripeRend, slot.Block, teamCol);

                bool showCrack = dmg01 > 0.12f;
                if (slot.CrackTr.gameObject.activeSelf != showCrack)
                    slot.CrackTr.gameObject.SetActive(showCrack);
                if (showCrack)
                {
                    slot.CrackTr.localPosition = new Vector3(0f, height * 0.42f, 0f);
                    slot.CrackTr.localScale = new Vector3(width * Mathf.Lerp(0.30f, 0.88f, dmg01), 0.12f + dmg01 * 0.22f, isTinik ? 0.44f : 0.68f);
                    SetSolidColor(slot.CrackRend, slot.Block, BoxCrackColor);
                }

                float barW = Mathf.Clamp(width * 0.48f, 1.4f, 2.6f);
                float barY = height + 0.28f;
                slot.HpTrackTr.localPosition = new Vector3(0f, barY, 0f);
                slot.HpTrackTr.localScale = new Vector3(barW, 0.13f, 0.08f);

                float fillW = Mathf.Max(0.04f, (barW - 0.06f) * hp01);
                slot.HpFillTr.localPosition = new Vector3(-(barW - 0.06f - fillW) * 0.5f, barY + 0.015f, 0f);
                slot.HpFillTr.localScale = new Vector3(fillW, 0.10f, 0.11f);
                Color hpCol = hp01 > 0.55f ? HealZoneMint : (hp01 > 0.25f ? RevealGold : TetherTaut);
                SetSolidColor(slot.HpFillRend, slot.Block, hpCol);
            }
            for (int i = count; i < MaxWallSlots; i++)
            {
                if (_wallPool[i].Active) { _wallPool[i].Active = false; _wallPool[i].Root.SetActive(false); }
            }
        }

        void SyncTraps(IReadOnlyList<TrapSnapshot> traps, int localTeam, int cbMode, bool reduceMotion)
        {
            int used = 0;
            int total = traps != null ? traps.Count : 0;
            for (int i = 0; i < total && used < MaxTrapSlots; i++)
            {
                TrapSnapshot t = traps[i];
                if (!t.VisibleToLocal) continue;

                TrapSlot slot = _trapPool[used++];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }

                bool isEnemy = t.Team != localTeam;
                bool isSako = string.Equals(t.SkillId, "sako_trap", StringComparison.Ordinal);
                Color teamCol = KomiksUi.TeamColor(t.Team, localTeam, cbMode);

                slot.Tr.position = KalyeahResources.World(t.Position, 0.02f);
                slot.PropTr.localPosition = new Vector3(0f, 0.01f, 0f);
                slot.PropTr.localRotation = Quaternion.Euler(0f, (t.Id * 47f) % 360f, isSako ? 0f : 12f);
                slot.PropTr.localScale = isSako ? new Vector3(0.74f, 0.14f, 0.62f) : new Vector3(0.46f, 0.08f, 0.34f);
                SetSolidColor(slot.PropRend, slot.Block, isSako ? BurlapTan : Color.Lerp(teamCol, Color.white, 0.35f));

                float r = Mathf.Max(0.45f, t.TriggerRadius);
                slot.RingTr.localPosition = new Vector3(0f, 0.025f, 0f);
                slot.RingTr.localScale = new Vector3(r, 1f, r);
                SetOverlayColor(slot.RingRend, slot.Block, new Color(teamCol.r, teamCol.g, teamCol.b, t.Armed ? 0.86f : 0.44f), isEnemy ? 10f : 0f, 0.24f, 0.45f);

                bool showReveal = t.Revealed;
                if (slot.RevealTr.gameObject.activeSelf != showReveal)
                    slot.RevealTr.gameObject.SetActive(showReveal);
                if (showReveal)
                {
                    float pulse = reduceMotion ? 1.15f : (1.10f + 0.18f * (0.5f + 0.5f * Mathf.Sin(_combatClock * 6f)));
                    slot.RevealTr.localPosition = new Vector3(0f, 0.045f, 0f);
                    slot.RevealTr.localScale = new Vector3(r * pulse, 1f, r * pulse);
                    SetOverlayColor(slot.RevealRend, slot.Block, RevealGold, 0f, 0.20f, 0.45f);
                }
            }
            for (int i = used; i < MaxTrapSlots; i++)
            {
                if (_trapPool[i].Active) { _trapPool[i].Active = false; _trapPool[i].Root.SetActive(false); }
            }
        }

        void SyncSentries(IReadOnlyList<SentrySnapshot> sentries, int localTeam, int cbMode, bool reduceMotion)
        {
            int count = sentries != null ? Mathf.Min(sentries.Count, MaxSentrySlots) : 0;
            for (int i = 0; i < count; i++)
            {
                SentrySnapshot s = sentries[i];
                SentrySlot slot = _sentryPool[i];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }

                bool isEnemy = s.Team != localTeam;
                Color teamCol = KomiksUi.TeamColor(s.Team, localTeam, cbMode);
                slot.Tr.position = KalyeahResources.World(s.Position, 0.02f);

                float spinDeg = reduceMotion ? 0f : Mathf.Repeat(_combatClock * 720f + s.Id * 37f, 360f);
                slot.SpinTr.localPosition = new Vector3(0f, 0.44f, 0f);
                slot.SpinTr.localRotation = Quaternion.Euler(reduceMotion ? 0f : 8f, spinDeg, 0f);
                slot.SpinTr.localScale = new Vector3(0.46f, 0.62f, 0.46f);
                SetSolidColor(slot.SpinRend, slot.Block, Color.Lerp(teamCol, RevealGold, 0.30f));

                float r = Mathf.Max(1.0f, s.Radius);
                slot.ScanRingTr.localPosition = new Vector3(0f, 0.03f, 0f);
                slot.ScanRingTr.localScale = new Vector3(r, 1f, r);
                SetOverlayColor(slot.ScanRingRend, slot.Block, new Color(teamCol.r, teamCol.g, teamCol.b, 0.78f), isEnemy ? 16f : 0f, 0.16f, 0.45f);

                float ping01 = Mathf.Clamp01(s.PingPulse01);
                bool showPing = ping01 > 0.01f && ping01 < 0.99f;
                if (slot.PingRingTr.gameObject.activeSelf != showPing)
                    slot.PingRingTr.gameObject.SetActive(showPing);
                if (showPing)
                {
                    float pr = Mathf.Lerp(0.4f, r, ping01);
                    slot.PingRingTr.localPosition = new Vector3(0f, 0.045f, 0f);
                    slot.PingRingTr.localScale = new Vector3(pr, 1f, pr);
                    SetOverlayColor(slot.PingRingRend, slot.Block, new Color(RevealGold.r, RevealGold.g, RevealGold.b, (1f - ping01) * 0.88f), 0f, 0.18f, 0.45f);
                }
            }
            for (int i = count; i < MaxSentrySlots; i++)
            {
                if (_sentryPool[i].Active) { _sentryPool[i].Active = false; _sentryPool[i].Root.SetActive(false); }
            }
        }

        void SyncTethers(IReadOnlyList<TetherSnapshot> tethers, bool reduceMotion)
        {
            int count = tethers != null ? Mathf.Min(tethers.Count, MaxTetherSlots) : 0;
            for (int i = 0; i < count; i++)
            {
                TetherSnapshot t = tethers[i];
                TetherSlot slot = _tetherPool[i];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }

                Vector3 a = KalyeahResources.World(t.OwnerPos, 0.95f);
                Vector3 b = KalyeahResources.World(t.AllyPos, 0.95f);
                Vector3 delta = b - a;
                float len = delta.magnitude;
                if (len < 0.05f)
                {
                    slot.Root.SetActive(false);
                    slot.Active = false;
                    continue;
                }

                float stretch = Mathf.Clamp01(t.Stretch01);
                float thickness = Mathf.Lerp(0.11f, 0.055f, stretch);
                slot.Tr.position = (a + b) * 0.5f;
                slot.Tr.rotation = Quaternion.LookRotation(delta / len, Vector3.up);
                slot.Tr.localScale = new Vector3(thickness, thickness, len);

                float pulse = (!reduceMotion && stretch > 0.72f) ? (0.5f + 0.5f * Mathf.Sin(_combatClock * 16f)) : 0f;
                Color col = Color.Lerp(TetherMint, TetherTaut, Mathf.Clamp01(stretch * 1.15f + pulse * 0.25f));
                SetOverlayColor(slot.BeamRend, slot.Block, col, 0f, 1.0f, 0.02f);
            }
            for (int i = count; i < MaxTetherSlots; i++)
            {
                if (_tetherPool[i].Active) { _tetherPool[i].Active = false; _tetherPool[i].Root.SetActive(false); }
            }
        }

        void SyncSkillZones(IReadOnlyList<SkillZoneSnapshot> zones, int localTeam, int cbMode, bool reduceMotion)
        {
            int count = zones != null ? Mathf.Min(zones.Count, MaxSkillZoneSlots) : 0;
            for (int i = 0; i < count; i++)
            {
                SkillZoneSnapshot z = zones[i];
                SkillZoneSlot slot = _zonePool[i];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }
                if (slot.LastZoneId != z.Id)
                {
                    slot.LastZoneId = z.Id;
                    slot.InitialDelay = Mathf.Max(0f, z.DelayLeft);
                }
                else if (z.DelayLeft > slot.InitialDelay)
                {
                    slot.InitialDelay = z.DelayLeft;
                }

                bool isEnemy = z.Team != localTeam;
                Color teamCol = KomiksUi.TeamColor(z.Team, localTeam, cbMode);
                Color zoneCol = z.Kind == SkillZoneVisualKind.PabitinHeal || z.Kind == SkillZoneVisualKind.LangitAura
                    ? (isEnemy ? teamCol : HealZoneMint)
                    : (z.Kind == SkillZoneVisualKind.SaranggolaKite || z.Kind == SkillZoneVisualKind.PikoGrid
                        ? Color.Lerp(teamCol, RevealGold, 0.35f)
                        : teamCol);

                Vector3 center = KalyeahResources.World(z.Position, 0.03f);
                slot.Tr.position = center;
                slot.Tr.rotation = Quaternion.identity;

                for (int e = 0; e < 3; e++)
                    if (slot.Extras[e].gameObject.activeSelf)
                        slot.Extras[e].gameObject.SetActive(false);

                float r = Mathf.Max(0.5f, z.Radius);
                bool showCircle = z.Kind != SkillZoneVisualKind.PatinteroLines && z.Kind != SkillZoneVisualKind.LuksongLubidRope;
                if (slot.RimTr.gameObject.activeSelf != showCircle) slot.RimTr.gameObject.SetActive(showCircle);
                if (slot.FillTr.gameObject.activeSelf != showCircle) slot.FillTr.gameObject.SetActive(showCircle);

                if (showCircle)
                {
                    slot.RimTr.localPosition = Vector3.zero;
                    slot.RimTr.localScale = new Vector3(r, 1f, r);
                    SetOverlayColor(slot.RimRend, slot.Block, new Color(zoneCol.r, zoneCol.g, zoneCol.b, 0.92f), isEnemy ? 16f : 0f, 0.20f, 0.45f);

                    // docs/GRAPHICS.md L56: delayed skills fill up smoothly 0 -> 1 to exact outer radius before landing
                    float fillScale = r;
                    float fillAlpha = isEnemy ? 0.20f : 0.16f;
                    float discHardEdge = 0.02f;
                    if (z.DelayLeft > 0.01f)
                    {
                        float delaySpan = Mathf.Max(slot.InitialDelay, 0.05f);
                        float fill01 = Mathf.Clamp01(1f - z.DelayLeft / delaySpan);
                        fillScale = Mathf.Max(0.12f, r * fill01);
                        fillAlpha = Mathf.Lerp(0.18f, 0.38f, fill01);
                        discHardEdge = 0.08f;
                    }
                    slot.FillTr.localPosition = new Vector3(0f, -0.004f, 0f);
                    slot.FillTr.localScale = new Vector3(fillScale, 1f, fillScale);
                    SetOverlayColor(slot.FillRend, slot.Block, new Color(zoneCol.r, zoneCol.g, zoneCol.b, fillAlpha), 0f, 1.0f, discHardEdge);
                }

                // Signature 3D street-game prop silhouettes per SkillZoneVisualKind
                switch (z.Kind)
                {
                    case SkillZoneVisualKind.PabitinHeal:
                    case SkillZoneVisualKind.PabitinSnare:
                    {
                        float rackY = z.Kind == SkillZoneVisualKind.PabitinSnare && z.DelayLeft > 0.01f
                            ? Mathf.Lerp(0.45f, 3.0f, Mathf.Clamp01(z.DelayLeft / 0.5f))
                            : 2.35f;
                        ShowZoneExtra(slot, 0, _combatBoxMesh, new Vector3(0f, rackY, 0f), Quaternion.identity, new Vector3(r * 1.15f, 0.12f, 0.16f), zoneCol);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, new Vector3(0f, rackY, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(r * 1.15f, 0.12f, 0.16f), zoneCol);
                        break;
                    }
                    case SkillZoneVisualKind.PaloseboPole:
                    {
                        ShowZoneExtra(slot, 0, _combatBoxMesh, new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(0.24f, 4.2f, 0.24f), BambooGreen);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, new Vector3(0.32f, 3.6f, 0f), Quaternion.identity, new Vector3(0.68f, 0.42f, 0.06f), teamCol);
                        break;
                    }
                    case SkillZoneVisualKind.SaranggolaKite:
                    {
                        float orbit = reduceMotion ? 0f : (_combatClock * 1.4f + z.Id);
                        Vector3 kitePos = new Vector3(Mathf.Cos(orbit) * Mathf.Min(2.4f, r * 0.28f), 5.2f, Mathf.Sin(orbit) * Mathf.Min(2.4f, r * 0.28f));
                        ShowZoneExtra(slot, 0, _combatDiamondMesh, kitePos, Quaternion.Euler(22f, orbit * Mathf.Rad2Deg, 0f), new Vector3(1.45f, 0.26f, 1.85f), RevealGold);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, new Vector3(kitePos.x * 0.5f, 0f, kitePos.z * 0.5f), Quaternion.identity, new Vector3(0.05f, 5.1f, 0.05f), new Color(1f, 0.96f, 0.84f, 0.55f));
                        break;
                    }
                    case SkillZoneVisualKind.PikoGrid:
                    {
                        float box = Mathf.Max(0.75f, r * 0.52f);
                        ShowZoneExtra(slot, 0, _combatBoxMesh, new Vector3(0f, 0.01f, -box * 0.7f), Quaternion.identity, new Vector3(box, 0.04f, box), zoneCol);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, new Vector3(-box * 0.55f, 0.01f, box * 0.35f), Quaternion.identity, new Vector3(box * 0.9f, 0.04f, box), zoneCol);
                        ShowZoneExtra(slot, 2, _combatBoxMesh, new Vector3(box * 0.55f, 0.01f, box * 0.35f), Quaternion.identity, new Vector3(box * 0.9f, 0.04f, box), zoneCol);
                        break;
                    }
                    case SkillZoneVisualKind.PatinteroLines:
                    {
                        Vector3 fwdU = KalyeahResources.World(z.Direction, 0f);
                        if (fwdU.sqrMagnitude < 1e-4f) fwdU = Vector3.forward;
                        else fwdU.Normalize();
                        Quaternion rot = Quaternion.LookRotation(fwdU, Vector3.up);
                        int lineCount = Mathf.Clamp(z.Count > 0 ? z.Count : 3, 1, 3);
                        float spacing = z.Spacing > 0.2f ? z.Spacing : 2.2f;
                        float lineW = Mathf.Max(2.0f, z.HalfWidth * 2f);
                        for (int k = 0; k < lineCount; k++)
                        {
                            float off = (k - (lineCount - 1) * 0.5f) * spacing;
                            ShowZoneExtra(slot, k, _combatBoxMesh, fwdU * off + new Vector3(0f, 0.015f, 0f), rot, new Vector3(lineW, 0.05f, 0.26f), zoneCol);
                        }
                        break;
                    }
                    case SkillZoneVisualKind.LuksongLubidRope:
                    {
                        Vector3 a = KalyeahResources.World(z.Position, 0.32f);
                        Vector3 b = KalyeahResources.World(z.EndPosition, 0.32f);
                        Vector3 delta = b - a;
                        float len = Mathf.Max(0.4f, delta.magnitude);
                        Quaternion rot = delta.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(delta / len, Vector3.up) : Quaternion.identity;
                        Vector3 localMid = ((a + b) * 0.5f) - center;
                        ShowZoneExtra(slot, 0, _combatBoxMesh, localMid, rot, new Vector3(0.10f, 0.10f, len), zoneCol);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, Vector3.zero, Quaternion.identity, new Vector3(0.18f, 0.55f, 0.18f), RevealGold);
                        ShowZoneExtra(slot, 2, _combatBoxMesh, b - center - new Vector3(0f, 0.30f, 0f), Quaternion.identity, new Vector3(0.18f, 0.55f, 0.18f), RevealGold);
                        break;
                    }
                    case SkillZoneVisualKind.GarterNet:
                    {
                        ShowZoneExtra(slot, 0, _combatBoxMesh, new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(r * 1.85f, 0.05f, 0.12f), zoneCol);
                        ShowZoneExtra(slot, 1, _combatBoxMesh, new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, -45f, 0f), new Vector3(r * 1.85f, 0.05f, 0.12f), zoneCol);
                        break;
                    }
                    case SkillZoneVisualKind.JackstoneCircle:
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            float ang = (k * 120f + z.Id * 29f) * Mathf.Deg2Rad;
                            float d = r * 0.45f;
                            ShowZoneExtra(slot, k, _combatDiamondMesh, new Vector3(Mathf.Cos(ang) * d, 0.14f, Mathf.Sin(ang) * d), Quaternion.identity, new Vector3(0.24f, 0.24f, 0.24f), RevealGold);
                        }
                        break;
                    }
                    case SkillZoneVisualKind.HolensSpillCone:
                    {
                        Vector3 fwdU = KalyeahResources.World(z.Direction, 0f);
                        if (fwdU.sqrMagnitude < 1e-4f) fwdU = Vector3.forward;
                        else fwdU.Normalize();
                        float halfAng = Mathf.Clamp(z.AngleDegrees > 5f ? z.AngleDegrees * 0.35f : 22f, 10f, 40f);
                        for (int k = 0; k < 3; k++)
                        {
                            float yaw = (k - 1) * halfAng;
                            Vector3 ray = Quaternion.Euler(0f, yaw, 0f) * fwdU;
                            float d = r * (k == 1 ? 0.62f : 0.48f);
                            ShowZoneExtra(slot, k, _combatDiamondMesh, ray * d + new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(0.22f, 0.22f, 0.22f), zoneCol);
                        }
                        break;
                    }
                }
            }
            for (int i = count; i < MaxSkillZoneSlots; i++)
            {
                if (_zonePool[i].Active)
                {
                    _zonePool[i].Active = false;
                    _zonePool[i].LastZoneId = -1;
                    _zonePool[i].InitialDelay = 0f;
                    _zonePool[i].Root.SetActive(false);
                }
            }
        }

        void SyncRevealMarkers(IReadOnlyList<RevealMarkerSnapshot> markers, bool reduceMotion)
        {
            int count = markers != null ? Mathf.Min(markers.Count, MaxRevealMarkerSlots) : 0;
            for (int i = 0; i < count; i++)
            {
                RevealMarkerSnapshot m = markers[i];
                RevealMarkerSlot slot = _revealPool[i];
                if (!slot.Active) { slot.Active = true; slot.Root.SetActive(true); }

                float fade = m.Duration > 0.01f ? Mathf.Clamp01(m.TimeLeft / m.Duration) : 1f;
                Color col = new Color(RevealGold.r, RevealGold.g, RevealGold.b, Mathf.Clamp01(0.35f + 0.65f * fade));
                slot.Tr.position = KalyeahResources.World(m.Position, 0.03f);

                slot.PillarTr.localPosition = Vector3.zero;
                slot.PillarTr.localScale = new Vector3(0.14f, 2.35f, 0.14f);
                SetOverlayColor(slot.PillarRend, slot.Block, new Color(col.r, col.g, col.b, col.a * 0.48f), 0f, 1.0f, 0.02f);

                float bob = reduceMotion ? 0f : Mathf.Sin(_combatClock * 5f + m.Id) * 0.12f;
                float spin = reduceMotion ? 0f : Mathf.Repeat(_combatClock * 110f + m.Id * 31f, 360f);
                slot.DiamondTr.localPosition = new Vector3(0f, 1.55f + bob, 0f);
                slot.DiamondTr.localRotation = Quaternion.Euler(0f, spin, 0f);
                slot.DiamondTr.localScale = new Vector3(0.46f, 0.68f, 0.46f);
                SetSolidColor(slot.DiamondRend, slot.Block, col);

                slot.RingTr.localPosition = new Vector3(0f, 0.02f, 0f);
                slot.RingTr.localScale = new Vector3(0.95f, 1f, 0.95f);
                SetOverlayColor(slot.RingRend, slot.Block, col, 0f, 0.22f, 0.45f);
            }
            for (int i = count; i < MaxRevealMarkerSlots; i++)
            {
                if (_revealPool[i].Active) { _revealPool[i].Active = false; _revealPool[i].Root.SetActive(false); }
            }
        }

        static void ShowZoneExtra(SkillZoneSlot slot, int index, Mesh mesh, Vector3 localPos, Quaternion localRot, Vector3 scale, Color color)
        {
            Transform tr = slot.Extras[index];
            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            if (slot.ExtraFilters[index].sharedMesh != mesh) slot.ExtraFilters[index].sharedMesh = mesh;
            tr.localPosition = localPos;
            tr.localRotation = localRot;
            tr.localScale = scale;
            SetSolidColor(slot.ExtraRends[index], slot.Block, color);
        }

        static Transform MakePart(string name, Transform parent, Mesh mesh, Material mat, out MeshRenderer rend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            rend = go.AddComponent<MeshRenderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (mat != null) rend.sharedMaterial = mat;
            return go.transform;
        }

        static void SetSolidColor(MeshRenderer rend, MaterialPropertyBlock block, Color c)
        {
            if (rend == null) return;
            block.Clear();
            block.SetColor(PropBaseColor, c);
            block.SetColor(PropColor, c);
            rend.SetPropertyBlock(block);
        }

        static void SetOverlayColor(
            MeshRenderer rend,
            MaterialPropertyBlock block,
            Color c,
            float dashCount,
            float interiorAlpha,
            float hardEdgeWidth)
        {
            if (rend == null) return;
            block.Clear();
            // ArcaneFx multiplies _Tint * _BaseColor; keep _Tint = white so RGB and alpha never square!
            block.SetColor(PropBaseColor, c);
            block.SetColor(PropColor, c);
            block.SetColor(PropTint, Color.white);
            block.SetFloat(PropSoftFade, 1f);
            block.SetFloat(PropDashCount, dashCount);
            block.SetFloat(PropHardEdgeWidth, hardEdgeWidth);
            block.SetFloat(PropEdgeBoost, hardEdgeWidth > 0.05f ? 0.24f : 0f);
            block.SetFloat(PropInteriorAlpha, interiorAlpha);
            rend.SetPropertyBlock(block);
        }

        static Material CreateCombatSolidMaterial()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            if (s == null) return null;
            var mat = new Material(s) { name = "KalyeahCombatSolid" };
            mat.enableInstancing = true;
            return mat;
        }

        static Material CreateCombatOverlayMaterial()
        {
            Material arcane = KalyeahResources.Material("ArcaneFx", FxPool.ShaderName);
            if (arcane != null)
            {
                var mat = new Material(arcane) { name = "KalyeahCombatOverlay" };
                if (mat.HasProperty(PropTint)) mat.SetColor(PropTint, Color.white);
                if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, Color.white);
                if (mat.HasProperty(PropInkColor)) mat.SetColor(PropInkColor, Color.clear);
                if (mat.HasProperty(PropSoftFade)) mat.SetFloat(PropSoftFade, 1f);
                mat.renderQueue = (int)RenderQueue.Transparent + 8;
                return mat;
            }
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (s == null) return null;
            var fb = new Material(s) { name = "KalyeahCombatOverlay" };
            if (fb.HasProperty("_Surface")) fb.SetFloat("_Surface", 1f);
            if (fb.HasProperty("_ZWrite")) fb.SetInt("_ZWrite", 0);
            if (fb.HasProperty("_Cull")) fb.SetInt("_Cull", (int)CullMode.Off);
            fb.renderQueue = (int)RenderQueue.Transparent + 8;
            return fb;
        }

        static Mesh BuildUnitBoxMesh()
        {
            var m = new Mesh { name = "KalyeahCombatBox" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, -0.5f), new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f), new Vector3(0.5f, 0f,  0.5f), new Vector3(0.5f, 1f,  0.5f), new Vector3(-0.5f, 1f,  0.5f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            };
            m.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 7, 3, 0, 4, 7,
                1, 2, 6, 1, 6, 5,
                3, 7, 6, 3, 6, 2,
                0, 1, 5, 0, 5, 4,
            };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFlatAnnulusMesh(float innerR, float outerR, int segs)
        {
            var m = new Mesh { name = "KalyeahCombatRing" };
            var verts = new Vector3[segs * 2];
            var uvs = new Vector2[segs * 2];
            var tris = new int[segs * 6];
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);
                uvs[i * 2] = new Vector2(i / (float)segs, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segs, 1f);
                int next = (i + 1) % segs;
                int t = i * 6;
                tris[t] = i * 2;
                tris[t + 1] = next * 2 + 1;
                tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2;
                tris[t + 4] = next * 2;
                tris[t + 5] = next * 2 + 1;
            }
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFlatDiscMesh(float radius, int segs)
        {
            var m = new Mesh { name = "KalyeahCombatDisc" };
            var verts = new Vector3[segs + 1];
            var uvs = new Vector2[segs + 1];
            var tris = new int[segs * 3];
            verts[0] = Vector3.zero;
            // Radial UVs (uv.y = 0 at center, 1 at perimeter) so ArcaneFx _HardEdgeWidth is radially symmetric
            uvs[0] = new Vector2(0.5f, 0f);
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                uvs[i + 1] = new Vector2(i / (float)segs, 1f);
                int next = (i + 1) % segs;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = next + 1;
                tris[i * 3 + 2] = i + 1;
            }
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildDiamondMesh()
        {
            var m = new Mesh { name = "KalyeahCombatDiamond" };
            m.vertices = new[]
            {
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0f, -0.5f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0f, 0f, -0.5f),
            };
            m.triangles = new[]
            {
                0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5,
                1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2,
            };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
