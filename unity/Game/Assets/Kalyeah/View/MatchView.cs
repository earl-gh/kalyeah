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
            _boundSim.PlayerDamaged += OnPlayerDamaged;
            _boundSim.PointReset += OnPointReset;
        }

        public void Unbind()
        {
            Projectiles?.Unbind();
            if (_boundSim == null) return;
            _boundSim.WeaponCast -= OnWeaponCast;
            _boundSim.UltimateCast -= OnUltimateCast;
            _boundSim.WeaponHit -= OnWeaponHit;
            _boundSim.PlayerDamaged -= OnPlayerDamaged;
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

            IReadOnlyList<RescueObjectiveSnapshot> rescues = sim.Rescues;
            float ownRescueCd = (rescues != null && ownIdx < rescues.Count) ? rescues[ownIdx].CooldownLeft : 0f;
            float enemyRescueCd = (rescues != null && enemyIdx < rescues.Count) ? rescues[enemyIdx].CooldownLeft : 0f;
            Zones.SyncRescue(ownRescueCd, enemyRescueCd, hideCounts: !playing);

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
                if (!_kids.TryGetValue(kid.Id, out KidView view))
                {
                    view = new KidView(Root, kid, _camera.KidViewScale, teamColor, _unlit);
                    _kids[kid.Id] = view;
                }
                else
                {
                    view.SetTeamColor(teamColor);
                }

                SVector2 pos = _previousPositions.TryGetValue(kid.Id, out SVector2 fromPos)
                    ? SVector2.Lerp(fromPos, kid.Position, blend)
                    : kid.Position;

                _worldFeet[kid.Id] = KalyeahResources.World(pos, 0f);

                float flash01 = _hitFlashLeft.TryGetValue(kid.Id, out float flashLeft) && flashLeft > 0f
                    ? Mathf.Clamp01(flashLeft / HitFlashDuration)
                    : 0f;

                bool hideNear = hideLocalNearCamera && kid.Id == localId;
                view.SyncState(
                    ground: pos,
                    airY: kid.AirY,
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

        void OnPlayerDamaged(int targetId, int amount)
        {
            if (amount > 0)
                _hitFlashLeft[targetId] = HitFlashDuration;
        }

        void OnPointReset()
        {
            _hitFlashLeft.Clear();
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
        }
    }
}
