using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full Agawang Base match world orchestrator: owns <see cref="SimBridge"/>, <see cref="MatchView"/>, <see cref="ShoulderCamera"/>,
    /// <see cref="MatchHud"/>, <see cref="AudioHub"/>, <see cref="AimSolver"/> + <see cref="AimIndicatorView"/>, and <see cref="Haptics"/>.
    /// Runs both interactively in PlayMode (via <see cref="MatchBoot"/>) and deterministically in EditMode (<c>KalyeahBuild.Screenshots</c>).
    /// </summary>
    public sealed class MatchWorld : IDisposable
    {
        public const int MaxTicksPerFrame = 5;

        public readonly DataSource Data;
        public readonly SimBridge Sim;
        public readonly MatchView View;
        public readonly ShoulderCamera Shoulder;
        public readonly Camera Camera;
        public readonly MatchHud Hud;
        public readonly AudioHub Audio;
        public readonly bool Interactive;

        readonly List<KidSnapshot> _previous = new List<KidSnapshot>(12);
        readonly bool _ownsAudio;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly PcCombatInput _pcInput;
        public PcCombatInput PcInput => _pcInput;
#endif
        bool _disposed;

        public MatchWorld(Transform root, DataSource data, long seed, string localCharacter, bool interactive, bool hudCamera)
            : this(
                root,
                data,
                new MatchSetup
                {
                    Seed = seed,
                    LocalCharacter = localCharacter,
                },
                interactive,
                hudCamera,
                null,
                null,
                null)
        {
        }

        public MatchWorld(
            Transform root,
            DataSource data,
            MatchSetup setup,
            bool interactive = true,
            bool hudCamera = false,
            AudioHub audio = null,
            Action onPlayAgain = null,
            Action onBackToHome = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Interactive = interactive;
            Sim = new SimBridge(data, setup);

            // Headless / screenshot captures start straight in Playing so 10 kids run & full HUD controls render crisp and active.
            if (!interactive)
                Sim.SkipIntro();

            CameraRulesData cameraRules = CameraRulesData.Load(data);
            MapLayoutData mapLayout = MapLayoutData.Load(data);

            if (audio != null)
            {
                Audio = audio;
                _ownsAudio = false;
            }
            else if (interactive)
            {
                Audio = new AudioHub(root, prewarm: Application.isPlaying);
                _ownsAudio = true;
                Audio.BindRules(data);
                SyncAudioSettings();
                if (Application.isPlaying)
                    Audio.PlayBgm("bgm");
            }

            View = new MatchView(root, mapLayout, cameraRules, Audio);
            Shoulder = new ShoulderCamera(cameraRules, Sim.LocalPlayerTeam, data, mapLayout)
            {
                ReduceMotion = SettingsStore.Current.ReduceMotion,
            };
            Shoulder.RegisterOccluders(View.Occluders);

            var cameraObject = new GameObject("ShoulderCamera", typeof(Camera));
            if (root != null) cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.Skybox;

            View.Fx.ViewCamera = Camera;

            Hud = new MatchHud(
                root,
                Sim,
                hudCamera ? Camera : null,
                interactive,
                Audio,
                onPlayAgain,
                onBackToHome)
            {
                WorldFeetOverride = View.WorldFeetLookup,
            };
            Hud.OverheadBars.KidViewScale = cameraRules.KidViewScale;

            BindSimEvents();
            if (interactive)
            {
                Haptics.Enabled = SettingsStore.Current.Haptics;
                SettingsStore.Current.Changed += OnSettingsChanged;
                MatchBoot.ApplyDeviceQualityAndFrameRate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _pcInput = new PcCombatInput();
#endif
            }

            Draw(1f, 0f);
        }

        /// <summary>
        /// Samples LookPad, VirtualJoystick / TiltInput, radial AbilityArc or CardHand tray, and updates world-space AimIndicatorView
        /// before pushing the combined <see cref="LocalInputFrame"/> into <see cref="SimBridge.SetLocalInput(LocalInputFrame)"/>.
        /// </summary>
        public void SampleInput(float screenHeight, TiltInput tilt = null)
        {
            if (!Interactive || _disposed || Hud == null) return;

            if (Hud.Overlays != null && Hud.Overlays.IsBlockingGameplayInput)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_pcInput != null)
                {
                    Vector2 blockedStick = Vector2.zero;
                    AbilityInputState blockedAbility = default;
                    _pcInput.Sample(Sim, Hud, Shoulder, Camera, View.Layout, screenHeight, Time.unscaledDeltaTime, ref blockedStick, ref blockedAbility);
                }
#endif
                Hud.CancelActiveTouches();
                View.AimIndicator.HideAim();
                Sim.SetLocalInput(default);
                return;
            }

            if (Hud.Look != null)
                Shoulder.Look(Hud.Look.TakeDrag(), Mathf.Max(1f, screenHeight));

            Vector2 stick = Hud.Joystick != null ? Hud.Joystick.Value : Vector2.zero;
            if (stick.sqrMagnitude < 0.0025f && tilt != null && tilt.Enabled && SettingsStore.Current.TiltToRun)
                stick = tilt.SampleDirection();

            AbilityInputState ability = Hud.SampleAbilityInput(Sim, Shoulder, Camera, View.Layout);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _pcInput?.Sample(Sim, Hud, Shoulder, Camera, View.Layout, screenHeight, Time.unscaledDeltaTime, ref stick, ref ability);
#endif
            SVector2 move = Shoulder.GroundWay(stick);
            UpdateAimTelegraph(in ability);

            LocalInputFrame frame = ability.ToInputFrame(move);
            Sim.SetLocalInput(frame);
        }

        public void StepTick()
        {
            _previous.Clear();
            IReadOnlyList<KidSnapshot> current = Sim.Kids;
            for (int i = 0; i < current.Count; i++)
                _previous.Add(current[i]);

            Sim.Step();
        }

        public void Draw(float alpha, float dt)
        {
            if (_disposed) return;

            float clampedAlpha = Mathf.Clamp01(alpha);
            float stepDt = Mathf.Max(0f, dt);

            Shoulder.ReduceMotion = SettingsStore.Current.ReduceMotion;

            KidSnapshot? me = Sim.LocalKid;
            if (me.HasValue)
            {
                KidSnapshot local = me.Value;
                SVector2 feet = local.Position;
                for (int i = 0; i < _previous.Count; i++)
                {
                    if (_previous[i].Id == local.Id)
                    {
                        feet = SVector2.Lerp(_previous[i].Position, local.Position, clampedAlpha);
                        break;
                    }
                }

                Shoulder.SyncLocalKid(
                    facing: local.Facing,
                    speed01: local.Tagged ? 0f : local.Speed01,
                    sprintingOrDashing: local.Sprinting || local.Dashing,
                    crouchingOrSliding: local.Crouching || local.Sliding,
                    airY: local.AirY,
                    dt: stepDt);
                Shoulder.Apply(Camera, feet, stepDt);
            }

            View.Sync(
                _previous.Count > 0 ? _previous : null,
                Sim,
                clampedAlpha,
                stepDt,
                hideLocalNearCamera: Shoulder.TooCloseToKid);

            Hud?.Refresh(Sim, Camera, Shoulder, View.Layout, stepDt);
        }

        void UpdateAimTelegraph(in AbilityInputState ability)
        {
            if (ability.HeldSlot < 0 || string.IsNullOrEmpty(ability.PreviewWeaponId))
            {
                View.AimIndicator.HideAim();
                return;
            }

            KidSnapshot? localOpt = Sim.LocalKid;
            if (!localOpt.HasValue || localOpt.Value.Tagged)
            {
                View.AimIndicator.HideAim();
                return;
            }

            KidSnapshot me = localOpt.Value;
            SVector2 rawAimPoint = ability.AimPoint;
            if (!ability.HasAimPoint)
            {
                WeaponInfo? wInfo = Sim.GetWeaponInfo(ability.PreviewWeaponId);
                float maxRange = wInfo.HasValue && wInfo.Value.MaxRange > 0.1f ? wInfo.Value.MaxRange : 16f;
                float originH = (me.Crouching || me.Sliding) ? AimSolver.CrouchedOriginHeight : AimSolver.DefaultOriginHeight;
                AimSolution sol = AimSolver.Solve(Camera, me.Position, me.Facing, maxRange, View.Layout, originH);
                rawAimPoint = sol.SimAimPoint;
            }

            View.AimIndicator.ShowAim(
                Sim,
                ability.PreviewWeaponId,
                rawAimPoint,
                isCancelHovered: ability.IsCancelHovered,
                manualDrag: ability.IsDraggingAim);
        }

        void OnSettingsChanged()
        {
            if (_disposed) return;
            Haptics.Enabled = SettingsStore.Current.Haptics;
            MatchBoot.ApplyDeviceQualityAndFrameRate();
            SyncAudioSettings();
        }

        void SyncAudioSettings()
        {
            if (Audio == null) return;
            SettingsStore s = SettingsStore.Current;
            Audio.BindVolumes(s.MusicVolume, s.SfxVolume, s.MusicEnabled, s.SoundsEnabled, s.MasterVolume);
        }

        // ---- Sim Event Audio & Haptics Hooks ------------------------------------------------------------------------------------

        void BindSimEvents()
        {
            Sim.WeaponCast += OnWeaponCast;
            Sim.WeaponHit += OnWeaponHit;
            Sim.Tagged += OnTagged;
            Sim.Rescued += OnRescued;
            Sim.PointScored += OnPointScored;
            Sim.MatchWon += OnMatchWon;
            Sim.PowerupTaken += OnPowerupTaken;
            Sim.StatusApplied += OnStatusApplied;
        }

        void UnbindSimEvents()
        {
            if (Sim == null) return;
            Sim.WeaponCast -= OnWeaponCast;
            Sim.WeaponHit -= OnWeaponHit;
            Sim.Tagged -= OnTagged;
            Sim.Rescued -= OnRescued;
            Sim.PointScored -= OnPointScored;
            Sim.MatchWon -= OnMatchWon;
            Sim.PowerupTaken -= OnPowerupTaken;
            Sim.StatusApplied -= OnStatusApplied;
        }

        void OnWeaponCast(int casterId, string weaponId)
        {
            Audio?.PlayWeaponCast(weaponId);
            if (casterId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnWeaponHit(int targetId, string weaponId)
        {
            Audio?.PlayHit(isTag: false, weaponId);
            if (targetId == Sim.LocalPlayerId)
                Haptics.HitConfirm();
        }

        void OnTagged(int victimId, int taggerId)
        {
            Audio?.PlayHit(isTag: true);
            Audio?.PlayObjectiveEvent("tagged");
            if (victimId == Sim.LocalPlayerId || taggerId == Sim.LocalPlayerId)
                Haptics.HeavyImpact();
        }

        void OnRescued(int rescuedId, int rescuerId)
        {
            Audio?.PlayObjectiveEvent("rescued");
            if (rescuedId == Sim.LocalPlayerId || rescuerId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnPointScored(int scoringTeam, int _)
        {
            Audio?.PlayObjectiveEvent("point");
            Haptics.HeavyImpact();
        }

        void OnMatchWon(int _)
        {
            Audio?.PlayObjectiveEvent("victory");
            Haptics.HeavyImpact();
        }

        void OnPowerupTaken(int playerId, int _, BuffKind __)
        {
            Audio?.PlayObjectiveEvent("merienda");
            if (playerId == Sim.LocalPlayerId)
                Haptics.LightTap();
        }

        void OnStatusApplied(int playerId, StatusKind kind)
        {
            if (playerId != Sim.LocalPlayerId) return;
            switch (kind)
            {
                case StatusKind.Stun:
                    Audio?.PlayStatusEffect("stun");
                    break;
                case StatusKind.Slow:
                    Audio?.PlayStatusEffect("slow");
                    break;
                case StatusKind.Tripped:
                    Audio?.PlayStatusEffect("tripped");
                    break;
                case StatusKind.Polymorph:
                    Audio?.PlayStatusEffect("polymorph");
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnbindSimEvents();
            Sim?.Dispose();
            if (Interactive)
                SettingsStore.Current.Changed -= OnSettingsChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _pcInput?.Dispose();
#endif

            Shoulder?.ClearOccluders();
            Hud?.Dispose();
            View?.Dispose();
            if (_ownsAudio)
                Audio?.Dispose();

            if (Camera != null)
                KalyeahResources.Kill(Camera.gameObject);
        }
    }

    /// <summary>
    /// Match scene entry point: loads persisted Settings/Session state, branches cleanly between <see cref="StreetMatchWorld"/>
    /// (Patintero, Tumbang Preso, Langit Lupa) and <see cref="MatchWorld"/> (5v5 Agawang Base / Training / Online / Hotspot),
    /// attaches <see cref="NetworkPauseGuard"/>, blends <see cref="TiltInput"/>, polls network sockets, and handles Play Again / Home / Back.
    /// </summary>
    public sealed class MatchBoot : MonoBehaviour
    {
        public const string SceneName = "Match";

        [SerializeField] string localCharacter = "junjun";
        [SerializeField] long seed = 0;

        DataSource _data;
        MatchWorld _world;
        StreetMatchWorld _streetWorld;
        TiltInput _tilt;
        NetworkPauseGuard _pauseGuard;
        float _carry;

        void Start()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            ScreenFitMetrics.EnsureEnhancedTouch();
            _data = KalyeahResources.Data();
            ApplyBootResourcesAndGraphics(_data);
            MenuStack.LoadPersistedState(AppSession.Current, SettingsStore.Current);
            ApplyDeviceQualityAndFrameRate();
            Ui.EnsureEventSystem();

            _tilt = new TiltInput(SettingsStore.Current.TiltToRun);
            SettingsStore.Current.Changed += OnSettingsChanged;

            BuildActiveWorld(seed);
            LoadingScreenView.CompleteAndHide();
        }

        void BuildActiveWorld(long matchSeed)
        {
            DisposeActiveWorld();
            _carry = 0f;

            long actualSeed = matchSeed != 0 ? matchSeed : DateTime.UtcNow.Ticks;
            AppSession session = AppSession.Current;

            if (session.TryGetActiveStreetKind(out StreetGameKind streetKind))
            {
                string chosenKid = !string.IsNullOrWhiteSpace(session.SelectedCharacter)
                    ? session.SelectedCharacter
                    : localCharacter;

                _streetWorld = new StreetMatchWorld(
                    transform,
                    _data,
                    streetKind,
                    actualSeed,
                    chosenKid,
                    interactive: true,
                    hudCamera: false,
                    onPlayAgain: HandlePlayAgain,
                    onHome: ReturnToTitle);
                _pauseGuard = NetworkPauseGuard.Attach(gameObject, street: _streetWorld.Sim);
            }
            else
            {
                MatchSetup setup = session.BuildMatchSetup(actualSeed);
                if (string.IsNullOrWhiteSpace(setup.LocalCharacter))
                    setup.LocalCharacter = localCharacter;

                _world = new MatchWorld(
                    transform,
                    _data,
                    setup,
                    interactive: true,
                    hudCamera: false,
                    audio: null,
                    onPlayAgain: HandlePlayAgain,
                    onBackToHome: ReturnToTitle);
                _pauseGuard = NetworkPauseGuard.Attach(gameObject, sim: _world.Sim);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (AppSession.Current.IsNetworkedMode)
                AppSession.Current.PollNetwork(dt);

            if (BackPressed())
            {
                if (_world != null)
                {
                    _world.Hud.HandleBack();
                }
                else if (_streetWorld != null)
                {
                    _streetWorld.Hud.HandleBack();
                }
            }

            float screenHeight = ScreenFitMetrics.ScreenSizePx.y;
            if (_streetWorld != null)
            {
                _streetWorld.UpdateFrame(dt, screenHeight);
                return;
            }

            if (_world == null) return;

            _world.SampleInput(screenHeight, _tilt);

            _carry += Mathf.Max(0f, dt);
            float tickDt = Mathf.Max(0.001f, _world.Sim.TickDt);
            int ticks = 0;
            while (_carry >= tickDt && ticks < MatchWorld.MaxTicksPerFrame)
            {
                _world.StepTick();
                _carry -= tickDt;
                ticks++;
            }
            if (ticks == MatchWorld.MaxTicksPerFrame)
                _carry = Mathf.Min(_carry, tickDt);

            if (_world.Sim.IsOnline)
                _world.Sim.SyncInterpolated();

            _world.Draw(_world.Sim.IsOnline ? 1f : _carry / tickDt, dt);
        }

        void HandlePlayAgain()
        {
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            if (AppSession.Current.IsNetworkedMode)
            {
                ReturnToTitle();
                return;
            }
            BuildActiveWorld(DateTime.UtcNow.Ticks);
        }

        void ReturnToTitle()
        {
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            AppSession session = AppSession.Current;
            if (session.Mode == GameMode.HotspotHost || session.Mode == GameMode.HotspotClient)
                AndroidLocalLink.LeaveLocalSession();
            if (session.IsNetworkedMode)
                session.StopAllNetwork();
            LoadingScreenView.LoadScene(
                TitleScreen.SceneName,
                _data,
                LoadingScreenView.ResolveHomeModeTitle());
        }

        void OnSettingsChanged()
        {
            _tilt?.SetEnabled(SettingsStore.Current.TiltToRun);
            ApplyDeviceQualityAndFrameRate();
        }

        public static void ApplyBootResourcesAndGraphics(DataSource data = null)
        {
            if (I18n.KeyCount == 0)
            {
                DataSource resolvedData = data ?? KalyeahResources.Data();
                if (resolvedData != null)
                    I18n.LoadFromData(resolvedData);
                if (I18n.KeyCount == 0)
                {
                    TextAsset csv = Resources.Load<TextAsset>("Data/i18n__strings");
                    if (csv != null && !string.IsNullOrEmpty(csv.text))
                        I18n.LoadCsv(csv.text);
                }
            }

            TextAsset stamp = Resources.Load<TextAsset>("Data/build_stamp");
            if (stamp != null && !string.IsNullOrWhiteSpace(stamp.text))
                AppSession.Current.ApplyBuildStampServerUrl(stamp.text);

            ApplyDeviceQualityAndFrameRate();
        }

        public static void ApplyDeviceQualityAndFrameRate()
        {
            Application.targetFrameRate = SettingsStore.Current.TargetFrameRate;
            var urp = (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                      ?? (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            if (urp != null)
                urp.renderScale = SettingsStore.Current.IsTurboGraphics ? SettingsStore.TurboRenderScale : 1f;
        }

        void DisposeActiveWorld()
        {
            if (_pauseGuard != null)
            {
                KalyeahResources.Kill(_pauseGuard);
                _pauseGuard = null;
            }
            if (_world != null)
            {
                _world.Dispose();
                _world = null;
            }
            if (_streetWorld != null)
            {
                _streetWorld.Dispose();
                _streetWorld = null;
            }
        }

        void OnDestroy()
        {
            SettingsStore.Current.Changed -= OnSettingsChanged;
            _tilt?.SetEnabled(false);
            DisposeActiveWorld();
        }

        public static bool BackPressed()
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            }
            catch
            {
                return false;
            }
        }
    }
}
