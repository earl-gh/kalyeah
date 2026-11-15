using System;
using System.Collections.Generic;
using UnityEngine;
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
            string localCharacter = "junjun",
            bool interactive = true,
            bool hudCamera = false,
            Action onPlayAgain = null,
            Action onHome = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Kind = kind;
            Interactive = interactive;

            string charId = string.IsNullOrWhiteSpace(localCharacter) ? "junjun" : localCharacter.Trim();
            long actualSeed = seed != 0 ? seed : DateTime.UtcNow.Ticks;

            Kalyeah.Net.GameClient client = (interactive && AppSession.Current.IsNetworkedMode)
                ? AppSession.Current.Client
                : null;
            Sim = new StreetBridge(data, kind, actualSeed, charId, client);

            CameraRulesData cameraRules = CameraRulesData.Load(data);
            MapLayoutData mapLayout = MapLayoutData.Load(data);

            View = new StreetMatchView(root, mapLayout, cameraRules, Sim);
            Shoulder = new ShoulderCamera(cameraRules, Sim.LocalPlayerTeam, data, layout: null)
            {
                ReduceMotion = SettingsStore.Current.ReduceMotion,
            };
            Shoulder.RegisterOccluders(View.Occluders);

            var cameraObject = new GameObject("ShoulderCamera", typeof(Camera));
            if (root != null) cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.Skybox;

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
            _sampledMove = Shoulder.GroundWay(stick);

            if (Hud.ConsumeSecondaryPress())
                _pendingSecondaryPress = true;
            if (Hud.ConsumeCancelAim())
                _pendingCancelAim = true;

            bool holdingThrow = Hud.PrimaryHeld && !_pendingCancelAim;
            Vector2 aimDragPx = Hud.TakeAimDragPixels();

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
