using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Self-contained 30 Hz step + interpolated draw orchestrator for Kalyeah's 3 Filipino Larong Kalye street mini-games
    /// (Patintero, Tumbang Preso, and Langit Lupa).
    /// Drives StreetBridge, StreetMatchView, ShoulderCamera, StreetHudView, FxPool, AudioHub, and TiltInput in PlayMode
    /// (via MatchBoot) and renders deterministically in EditMode (via KalyeahBuild.Screenshots) with hudCamera = true.
    /// </summary>
    public sealed class StreetMatchWorld : IDisposable
    {
        public const int MaxTicksPerFrame = 5;
        const float AirPivotShare = 0.65f;

        public readonly DataSource Data;
        public readonly StreetGameKind Kind;
        public readonly StreetBridge Sim;
        public readonly StreetMatchView View;
        public readonly ShoulderCamera Shoulder;
        public readonly Camera Camera;
        public readonly StreetHudView Hud;
        public readonly FxPool Fx;
        public readonly AudioHub Audio;
        public readonly bool Interactive;

        public StreetBridge Bridge => Sim;

        readonly TiltInput _tilt;
        readonly List<StreetKidSnapshot> _previous = new List<StreetKidSnapshot>(8);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly PcCombatInput _pcInput;
        public PcCombatInput PcInput => _pcInput;
#endif

        SVector2 _sampledMove;
        SVector2 _throwAimDir = new SVector2(0f, -1f);
        bool _sampledPrimaryHeld;
        bool _wasHoldingThrow;
        bool _pendingSecondaryPress;
        bool _pendingCancelAim;
        float _carry;
        bool _disposed;

        /// <summary>
        /// Session-driven constructor for MatchBoot: reads SelectedStreetGameKind, SelectedCharacter, and online GameClient
        /// (when IsNetworkedMode is active) directly from AppSession.Current.
        /// </summary>
        public StreetMatchWorld(
            Transform root,
            DataSource data,
            long seed,
            bool interactive = true,
            bool hudCamera = false,
            Action onPlayAgain = null,
            Action onHome = null)
            : this(
                root,
                data,
                AppSession.Current.SelectedStreetGameKind,
                seed,
                AppSession.Current.SelectedCharacter,
                interactive,
                hudCamera,
                onPlayAgain,
                onHome)
        {
        }

        /// <summary>
        /// Explicit StreetGameKind constructor usable directly by both MatchBoot and KalyeahBuild.Screenshots.
        /// </summary>
        public StreetMatchWorld(
            Transform root,
            DataSource data,
            StreetGameKind kind,
            long seed,
            string localCharacter = "narda",
            bool interactive = true,
            bool hudCamera = false,
            Action onPlayAgain = null,
            Action onHome = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Kind = kind;
            Interactive = interactive;

            string charId = string.IsNullOrWhiteSpace(localCharacter) ? "narda" : localCharacter.Trim();
            long actualSeed = seed != 0 ? seed : DateTime.UtcNow.Ticks;

            Kalyeah.Net.GameClient client = (interactive && AppSession.Current.IsNetworkedMode)
                ? AppSession.Current.Client
                : null;
            Sim = new StreetBridge(data, kind, actualSeed, charId, client);

            CameraRulesData cameraRules = CameraRulesData.Load(data);
            MapLayoutData mapLayout = MapLayoutData.Load(data);

            View = new StreetMatchView(root, mapLayout, cameraRules, Sim);
            MapLayoutData streetCamLayout = ConfigureStreetWorldCollidersAndLighting(View, Sim, mapLayout);

            Shoulder = new ShoulderCamera(cameraRules, Sim.LocalPlayerTeam, data, layout: streetCamLayout)
            {
                ReduceMotion = SettingsStore.Current.ReduceMotion,
            };
            Shoulder.RegisterOccluders(View.Occluders);

            var cameraObject = new GameObject("ShoulderCamera", typeof(Camera));
            if (root != null) cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.Skybox;

            // Ensure Street mode always has an active AudioListener so BGM & SFX play on Android and desktop.
            if (cameraObject.GetComponent<AudioListener>() == null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();

            Fx = new FxPool(root, FxPool.DefaultCapacity, SettingsStore.Current.ReduceMotion)
            {
                ViewCamera = Camera,
            };

            if (interactive)
            {
                Haptics.Enabled = SettingsStore.Current.Haptics;
                _tilt = new TiltInput(SettingsStore.Current.TiltToRun);
                Audio = new AudioHub(root, prewarm: Application.isPlaying);
                Audio.BindRules(data);
                SyncAudioSettings();
                if (Application.isPlaying)
                    Audio.PlayBgm("bgm");
                SettingsStore.Current.Changed += OnSettingsChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _pcInput = new PcCombatInput();
#endif
            }

            Hud = new StreetHudView(
                root,
                Sim,
                hudCamera ? Camera : null,
                interactive,
                onPlayAgain,
                onHome);
            if (interactive)
            {
                Hud.TumbangRollStepped += OnTumbangRollStepped;
                Hud.TumbangRollLanded += OnTumbangRollLanded;
            }

            BindSimEvents();
            Draw(1f, 0f);
        }

        /// <summary>Deterministic override for Langit Lupa Brownout visuals in EditMode screenshot captures.</summary>
        public void SetBrownoutPreview(bool? forceBrownout)
        {
            View.ForceBrownoutPreview = forceBrownout;
            if (Hud != null) Hud.ForceBrownoutPreview = forceBrownout;
            Draw(1f, 0f);
        }

        /// <summary>Handles Android Back / Escape by opening or dismissing the Street HUD Leave modal.</summary>
        public bool HandleBack() => Hud != null && Hud.HandleBack();

        /// <summary>
        /// Samples touch LookPad, VirtualJoystick / TiltInput, hold-to-charge Tumbang Preso throw aim, and latches edge-triggered
        /// Secondary / Cancel taps so fixed-rate 30 Hz StepTick() calls never drop inputs between ticks.
        /// </summary>
        public void SampleInput(float screenHeight)
        {
            if (!Interactive || Hud == null) return;

            if (Hud.IsBlockingGameplayInput)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_pcInput != null)
                {
                    Vector2 blockedStick = Vector2.zero;
                    bool blockedHold = false;
                    Vector2 blockedDrag = Vector2.zero;
                    bool blockedSec = false;
                    _pcInput.SampleStreet(
                        Sim,
                        Hud,
                        Shoulder,
                        screenHeight,
                        Time.unscaledDeltaTime,
                        ref blockedStick,
                        ref blockedHold,
                        ref blockedDrag,
                        ref blockedSec,
                        ref _pendingCancelAim);
                }
#endif
                Hud.CancelActiveTouches();
                _sampledMove = SVector2.Zero;
                if (_wasHoldingThrow)
                    _pendingCancelAim = true;
                _wasHoldingThrow = false;
                _sampledPrimaryHeld = false;
                return;
            }

            if (Hud.Look != null)
                Shoulder.Look(Hud.Look.TakeDrag(), Mathf.Max(1f, screenHeight));

            Vector2 stick = Hud.Joystick != null ? Hud.Joystick.Value : Vector2.zero;
            if (stick.sqrMagnitude < 0.0025f && _tilt != null && _tilt.Enabled)
                stick = _tilt.SampleDirection();

            if (Hud.ConsumeSecondaryPress())
                _pendingSecondaryPress = true;
            if (Hud.ConsumeCancelAim())
                _pendingCancelAim = true;

            bool holdingThrow = Hud.PrimaryHeld && !_pendingCancelAim;
            Vector2 aimDragPx = Hud.TakeAimDragPixels();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _pcInput?.SampleStreet(
                Sim,
                Hud,
                Shoulder,
                screenHeight,
                Time.unscaledDeltaTime,
                ref stick,
                ref holdingThrow,
                ref aimDragPx,
                ref _pendingSecondaryPress,
                ref _pendingCancelAim);
            if (_pendingCancelAim)
                holdingThrow = false;
#endif

            _sampledMove = Shoulder.GroundWay(stick);

            if (holdingThrow)
            {
                if (!_wasHoldingThrow)
                    _throwAimDir = DefaultCanAimDirection();

                if (Mathf.Abs(aimDragPx.x) > 0.001f)
                {
                    StreetKidSnapshot? local = Sim.LocalKid;
                    float charge01 = local.HasValue ? Mathf.Clamp01(local.Value.ThrowCharge01) : 0f;
                    float sensitivity = Mathf.Lerp(0.0018f, 0.006f, charge01);
                    _throwAimDir = RotateGround(_throwAimDir, aimDragPx.x * sensitivity);
                }
            }

            _wasHoldingThrow = holdingThrow;
            _sampledPrimaryHeld = holdingThrow;
        }

        /// <summary>Steps one fixed 30 Hz simulation tick and retains the previous snapshot for frame interpolation.</summary>
        public void StepTick()
        {
            _previous.Clear();
            IReadOnlyList<StreetKidSnapshot> current = Sim.Kids;
            for (int i = 0; i < current.Count; i++)
                _previous.Add(current[i]);

            if (Interactive)
            {
                bool cancel = _pendingCancelAim;
                bool secondary = _pendingSecondaryPress;
                bool hold = !cancel && _sampledPrimaryHeld;
                SVector2 aim = (hold || (_throwAimDir.LengthSquared() > 0.01f && Kind == StreetGameKind.TumbangPreso))
                    ? _throwAimDir
                    : SVector2.Zero;

                Sim.SetLocalInput(_sampledMove, aim, hold, secondary, cancel);
                _pendingSecondaryPress = false;
                _pendingCancelAim = false;
            }

            Sim.Step();
        }

        /// <summary>Renders interpolated street puppets, mode props, shoulder camera, FX bursts, and SafeArea HUD.</summary>
        public void Draw(float alpha, float dt)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            float stepDt = Mathf.Max(0f, dt);

            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            Shoulder.ReduceMotion = reduceMotion;
            if (Fx != null) Fx.ReduceMotion = reduceMotion;

            Vector3? localWorldSpotlight = null;
            StreetKidSnapshot? me = Sim.LocalKid;
            if (me.HasValue)
            {
                StreetKidSnapshot local = me.Value;
                SVector2 feet = local.Position;
                for (int i = 0; i < _previous.Count; i++)
                {
                    if (_previous[i].Id == local.Id)
                    {
                        feet = SVector2.Lerp(_previous[i].Position, local.Position, clampedAlpha);
                        break;
                    }
                }

                float platformLift = View.GetGroundElevation(feet);
                localWorldSpotlight = KalyeahResources.World(feet, platformLift + 0.35f);
                Shoulder.SyncLocalKid(
                    facing: local.Facing,
                    speed01: local.Jailed ? 0f : local.Speed01,
                    sprintingOrDashing: local.Diving,
                    crouchingOrSliding: false,
                    airY: platformLift / AirPivotShare,
                    dt: stepDt);
                Shoulder.Apply(Camera, feet, stepDt);
            }

            View.Sync(
                _previous.Count > 0 ? _previous : null,
                Sim,
                clampedAlpha,
                stepDt,
                Shoulder.TooCloseToKid);

            Fx?.Tick(stepDt);
            Hud?.Refresh(Sim, stepDt, Camera, localWorldSpotlight);
        }

        /// <summary>One-call PlayMode frame driver for MatchBoot: samples input, steps fixed 30 Hz accumulator, and draws.</summary>
        public void UpdateFrame(float deltaTime, float screenHeight)
        {
            SampleInput(screenHeight);

            _carry += Mathf.Max(0f, deltaTime);
            float tickDt = Mathf.Max(0.001f, Sim.TickDt);
            int ticks = 0;
            while (_carry >= tickDt && ticks < MaxTicksPerFrame)
            {
                StepTick();
                _carry -= tickDt;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame)
                _carry = Mathf.Min(_carry, tickDt);

            Draw(_carry / tickDt, deltaTime);
        }

        SVector2 DefaultCanAimDirection()
        {
            StreetKidSnapshot? me = Sim.LocalKid;
            if (!me.HasValue) return new SVector2(0f, -1f);

            SVector2 toCan = Sim.TumbangPresoState.CanPosition - me.Value.Position;
            if (toCan.LengthSquared() > 0.001f)
                return SVector2.Normalize(toCan);

            return me.Value.Facing.LengthSquared() > 0.001f
                ? SVector2.Normalize(me.Value.Facing)
                : new SVector2(0f, -1f);
        }

        static SVector2 RotateGround(SVector2 dir, float radians)
        {
            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            var rotated = new SVector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c);
            return rotated.LengthSquared() > 0.0001f ? SVector2.Normalize(rotated) : new SVector2(0f, -1f);
        }

        /// <summary>
        /// Configures Street mode for Option A1 crisp 10:30 AM sunlit Manila lighting, upgrades untextured storefront stand-ins
        /// to <c>Kalyeah/BarangayBuilding</c>, attaches Default-layer ground/sidewalk/wall <see cref="BoxCollider"/>s for Foot IK
        /// and camera whisker SphereCasts, and returns an analytic 4-wall <see cref="MapLayoutData"/> so Tumbang Preso / Patintero /
        /// Langit Lupa cameras never clip through street walls.
        /// </summary>
        static MapLayoutData ConfigureStreetWorldCollidersAndLighting(StreetMatchView view, StreetBridge sim, MapLayoutData baseLayout)
        {
            float halfW = ComputeStreetHalfWidth(sim);
            float halfL = ComputeStreetHalfLength(sim);

            if (view != null && view.Root != null)
            {
                // 1. Crisp 10:30 AM sunlit Manila lighting (disable dead 2nd Fill directional light + refresh GI probes)
                Light[] lights = view.Root.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    Light l = lights[i];
                    if (l.type != LightType.Directional) continue;
                    if (l.gameObject.name == "Fill")
                    {
                        l.enabled = false;
                        l.intensity = 0f;
                    }
                    else if (l.gameObject.name == "Sun")
                    {
                        l.color = new Color(1.00f, 0.957f, 0.871f, 1f);
                        l.intensity = 1.38f;
                        l.shadows = LightShadows.Soft;
                        l.shadowStrength = 0.56f;
                        l.shadowBias = 0.04f;
                        l.shadowNormalBias = 0.35f;
                        l.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(44f, 60f)));
                        RenderSettings.sun = l;
                    }
                }
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.384f, 0.596f, 0.847f, 1f);
                RenderSettings.ambientEquatorColor = new Color(0.796f, 0.749f, 0.663f, 1f);
                RenderSettings.ambientGroundColor = new Color(0.490f, 0.431f, 0.361f, 1f);
                RenderSettings.fogColor = new Color(0.84f, 0.89f, 0.93f, 1f);
                DynamicGI.UpdateEnvironment();

                // 2. Walkable street floor collider (Default layer) for 2-bone Foot IK
                var floorColGo = new GameObject("StreetGroundCollider");
                floorColGo.layer = 0;
                floorColGo.transform.SetParent(view.Root, false);
                var floorCol = floorColGo.AddComponent<BoxCollider>();
                floorCol.center = new Vector3(0f, -0.5f, 0f);
                floorCol.size = new Vector3(240f, 1f, 240f);
                floorCol.isTrigger = false;

                // 3. Upgrade untextured street storefront cubes to Kalyeah/BarangayBuilding & add sidewalk colliders
                Transform[] children = view.Root.GetComponentsInChildren<Transform>(true);
                int bldgIdx = 0;
                for (int i = 0; i < children.Length; i++)
                {
                    GameObject go = children[i].gameObject;
                    string n = go.name;
                    if ((n == "WestSidewalk" || n == "EastSidewalk") && go.GetComponent<Collider>() == null)
                    {
                        go.layer = 0;
                        go.AddComponent<BoxCollider>().isTrigger = false;
                        continue;
                    }

                    if (n.EndsWith("_StandIn", StringComparison.Ordinal)
                        && n.IndexOf("mango", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("coconut", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("banana", StringComparison.OrdinalIgnoreCase) < 0
                        && go.transform.lossyScale.y >= 2.0f)
                    {
                        MeshRenderer mr = go.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            Material bldgMat = GreyBoxKit.GetBarangayBuildingMaterial(GreyBoxKit.PaletteIndexFor(go.transform.position, bldgIdx));
                            if (bldgMat != null) mr.sharedMaterial = bldgMat;
                            bldgIdx++;
                        }
                    }
                }

                // 4. Unscaled Default-layer wall BoxColliders for all street buildings + continuous boundary facades (zero shear!)
                var wallsRoot = new GameObject("StreetBoundaryColliders").transform;
                wallsRoot.SetParent(view.Root, false);
                if (view.Occluders != null)
                {
                    for (int i = 0; i < view.Occluders.Count; i++)
                    {
                        GameObject occ = view.Occluders[i];
                        if (occ == null || occ.GetComponentInChildren<Collider>(true) != null) continue;
                        Bounds wb = KalyeahResources.RenderBounds(occ);
                        if (wb.size.y < 1.8f || (wb.size.x < 1.2f && wb.size.z < 1.2f)) continue;
                        AddStreetWallCollider(wallsRoot, "StreetOccluderCol_" + i, wb.center, wb.size);
                    }
                }

                // 5. Continuous enclosure wall colliders flush with storefront facades (closes alleys between cubes)
                float wallInnerX = halfW + 3.0f;
                float wallInnerZ = halfL + 3.6f;
                float sideSpanZ = (wallInnerZ + 8f) * 2f;
                float endSpanX = (wallInnerX + 8f) * 2f;
                AddStreetWallCollider(wallsRoot, "WestFacadeWall", new Vector3(-(wallInnerX + 1.5f), 4f, 0f), new Vector3(3f, 8f, sideSpanZ));
                AddStreetWallCollider(wallsRoot, "EastFacadeWall", new Vector3(wallInnerX + 1.5f, 4f, 0f), new Vector3(3f, 8f, sideSpanZ));
                AddStreetWallCollider(wallsRoot, "NorthFacadeWall", new Vector3(0f, 4f, wallInnerZ + 1.5f), new Vector3(endSpanX, 8f, 3f));
                AddStreetWallCollider(wallsRoot, "SouthFacadeWall", new Vector3(0f, 4f, -(wallInnerZ + 1.5f)), new Vector3(endSpanX, 8f, 3f));

                Physics.SyncTransforms();
            }

            // Analytic 4-wall layout (MapLayoutData mirrors Obstacles around origin -> West+North mirror to East+South).
            float camWallX = halfW + 2.95f;
            float camWallZ = halfL + 3.55f;
            return new MapLayoutData
            {
                ArenaRadius = 0f,
                BaseInset = baseLayout != null ? baseLayout.BaseInset : 0f,
                BaseRadius = baseLayout != null ? baseLayout.BaseRadius : 0f,
                BoundaryThickness = 1.2f,
                CoverHeight = baseLayout != null ? baseLayout.CoverHeight : 1.35f,
                Obstacles = new[]
                {
                    new GroundRect(-(camWallX + 12f), -(camWallZ + 12f), 12f, (camWallZ + 12f) * 2f),
                    new GroundRect(-(camWallX + 12f), -(camWallZ + 12f), (camWallX + 12f) * 2f, 12f),
                },
                ObstacleYawsDeg = Array.Empty<float>(),
                ObstacleScales = Array.Empty<float>(),
                ObstacleProps = Array.Empty<string>(),
                PowerupSpots = Array.Empty<SVector2>(),
            };
        }

        static void AddStreetWallCollider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = 0;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = false;
        }

        static float ComputeStreetHalfWidth(StreetBridge bridge)
        {
            if (bridge == null) return 12.5f;
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

        static float ComputeStreetHalfLength(StreetBridge bridge)
        {
            if (bridge == null) return 20f;
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

        void OnSettingsChanged()
        {
            Haptics.Enabled = SettingsStore.Current.Haptics;
            _tilt?.SetEnabled(Interactive && SettingsStore.Current.TiltToRun);
            SyncAudioSettings();
        }

        void SyncAudioSettings()
        {
            if (Audio == null) return;
            SettingsStore s = SettingsStore.Current;
            Audio.BindVolumes(s.MusicVolume, s.SfxVolume, s.MusicEnabled, s.SoundsEnabled, s.MasterVolume);
        }

        // ---- Sim Event Presentation Hooks (paired with -= in Dispose) -----------------------------------------------------------

        void BindSimEvents()
        {
            Sim.Tagged += OnTagged;
            Sim.Scored += OnScored;
            Sim.RolesSwapped += OnRolesSwapped;
            Sim.MatchWon += OnMatchWon;
            Sim.SlipperThrown += OnSlipperThrown;
            Sim.CanKicked += OnCanKicked;
            Sim.CanPlaced += OnCanPlaced;
            Sim.CanFlung += OnCanFlung;
            Sim.Dived += OnDived;
            Sim.MoundRaised += OnMoundRaised;
            Sim.PlatformBroke += OnPlatformBroke;
        }

        void UnbindSimEvents()
        {
            if (Sim == null) return;
            Sim.Tagged -= OnTagged;
            Sim.Scored -= OnScored;
            Sim.RolesSwapped -= OnRolesSwapped;
            Sim.MatchWon -= OnMatchWon;
            Sim.SlipperThrown -= OnSlipperThrown;
            Sim.CanKicked -= OnCanKicked;
            Sim.CanPlaced -= OnCanPlaced;
            Sim.CanFlung -= OnCanFlung;
            Sim.Dived -= OnDived;
            Sim.MoundRaised -= OnMoundRaised;
            Sim.PlatformBroke -= OnPlatformBroke;
        }

        void OnTagged(int taggerId, int targetId)
        {
            View.TriggerKidCast(taggerId, moving: true);
            StreetKidSnapshot? target = Sim.Find(targetId);
            if (target.HasValue)
                Fx?.SpawnHitConfirm(target.Value.Position, new Color(1.00f, 0.36f, 0.22f, 0.98f), heavy: true);

            Audio?.PlayStreetEvent("tag");
            if (taggerId == Sim.LocalPlayerId) Haptics.HitConfirm();
            else if (targetId == Sim.LocalPlayerId) Haptics.HeavyImpact();
        }

        void OnScored(int team, int scorerId)
        {
            StreetKidSnapshot? who = Sim.Find(scorerId);
            if (who.HasValue)
                Fx?.SpawnHealRing(who.Value.Position, 1.5f);

            Audio?.PlayStreetEvent("point");
            if (scorerId == Sim.LocalPlayerId || (!Sim.IsFreeForAll && team == Sim.LocalPlayerTeam))
                Haptics.HitConfirm();
        }

        void OnRolesSwapped(int newTayaId)
        {
            Audio?.PlayStreetEvent("taya_picked");
            if (newTayaId == Sim.LocalPlayerId)
                Haptics.HeavyImpact();
        }

        void OnMatchWon(int _)
        {
            Audio?.PlayStreetEvent("victory");
            Haptics.HeavyImpact();
        }

        void OnSlipperThrown(int throwerId)
        {
            StreetKidSnapshot? who = Sim.Find(throwerId);
            bool moving = who.HasValue && who.Value.Speed01 > 0.1f;
            View.TriggerKidCast(throwerId, moving);
            if (who.HasValue)
                Fx?.SpawnMuzzleSnap(who.Value.Position, who.Value.Facing);

            Audio?.PlayWeaponCast("tsinelas");
            if (throwerId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnCanKicked(int kickerId)
        {
            View.TriggerKidCast(kickerId, moving: true);
            Fx?.SpawnGroundShockwave(Sim.TumbangPresoState.CanPosition, 1.8f, new Color(1.00f, 0.82f, 0.28f, 0.96f));
            Audio?.PlayStreetEvent("kick");
            if (kickerId == Sim.LocalPlayerId)
                Haptics.HitConfirm();
        }

        void OnCanPlaced(int _)
        {
            Fx?.SpawnCleanseSpark(Sim.TumbangPresoState.CanSpot);
            Audio?.PlayStreetEvent("can_reset");
        }

        void OnCanFlung(SVector2 fromGround)
        {
            Fx?.SpawnGroundShockwave(fromGround, 2.2f, new Color(1.00f, 0.78f, 0.24f, 0.98f), isBagsak: true);
            Fx?.SpawnHitConfirm(fromGround, new Color(1.00f, 0.86f, 0.32f, 0.98f), heavy: true);
            Audio?.PlayStreetEvent("can_hit");
            Haptics.HeavyImpact();
        }

        void OnDived(int runnerId, SVector2 way)
        {
            StreetKidSnapshot? who = Sim.Find(runnerId);
            if (who.HasValue)
            {
                Vector3 worldFeet = KalyeahResources.World(who.Value.Position, 0f);
                Fx?.SpawnDustPuff(worldFeet, 1.05f);
                Fx?.SpawnMuzzleSnap(who.Value.Position, way);
            }
            Audio?.PlayUiTap("confirm");
            if (runnerId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnMoundRaised(int ownerId)
        {
            View.TriggerKidCast(ownerId, moving: false);
            StreetKidSnapshot? who = Sim.Find(ownerId);
            if (who.HasValue)
                Fx?.SpawnGroundShockwave(who.Value.Position, 1.7f, new Color(0.84f, 0.60f, 0.30f, 0.95f));

            Audio?.PlayStreetEvent("mound");
            if (ownerId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnPlatformBroke(int platformIndex)
        {
            IReadOnlyList<LangitPlatformSnapshot> plats = Sim.LangitLupaState.Platforms;
            if (plats != null && platformIndex >= 0 && platformIndex < plats.Count)
            {
                SVector2 center = plats[platformIndex].Bounds.Center;
                Fx?.SpawnGroundShockwave(center, 2.1f, new Color(1.00f, 0.44f, 0.24f, 0.96f));
                Fx?.SpawnDustPuff(KalyeahResources.World(center, 0.25f), 1.25f);
            }
            Audio?.PlayStreetEvent("platform_break");
            Haptics.HeavyImpact();
        }

        void OnTumbangRollStepped()
        {
            Audio?.PlayUiTap();
        }

        void OnTumbangRollLanded()
        {
            Audio?.PlayUiTap("ready");
            if (Sim != null && Sim.TumbangPresoState.TayaId == Sim.LocalPlayerId)
                Haptics.HeavyImpact();
            else
                Haptics.LightTap();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnbindSimEvents();
            if (Interactive)
                SettingsStore.Current.Changed -= OnSettingsChanged;

            _tilt?.SetEnabled(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _pcInput?.Dispose();
#endif
            Shoulder?.ClearOccluders();
            if (Hud != null)
            {
                if (Interactive)
                {
                    Hud.TumbangRollStepped -= OnTumbangRollStepped;
                    Hud.TumbangRollLanded -= OnTumbangRollLanded;
                }
                Hud.Dispose();
            }
            View?.Dispose();
            Fx?.Dispose();
            Audio?.Dispose();

            if (Camera != null)
                KalyeahResources.Kill(Camera.gameObject);
        }
    }
}
