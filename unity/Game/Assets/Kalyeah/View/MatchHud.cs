using System;
using System.Collections.Generic;
using System.Globalization;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full Agawang Base match HUD on the 1280 x 720 landscape SafeArea canvas (Locked Option A1 / G3-G8):
    /// - Preserves <see cref="Canvas"/>, <see cref="Joystick"/>, <see cref="Look"/>, <c>SafeArea/Header</c>, and <c>SafeArea/OwnTeam</c>
    ///   bounds invariants verified by <c>ScreenFitBoundsTests</c> across 21:9..4:3 + LeftHanded + 180 deg flips.
    /// - Wires <see cref="OverheadBarsView"/> (enemy HP hidden + smooth amber drain), <see cref="CrosshairView"/> (A3 coral-gold confirm),
    ///   <see cref="BarangayMinimapView"/> (G3 circular Valorant architectural radar), <see cref="AbilityArcView"/> (G7 frosted titanium discs)
    ///   + <see cref="CardHandView"/>, <see cref="PingAndBanterView"/> (5-wedge radial ping wheel + Taglish banter strip),
    ///   and <see cref="MatchOverlaysView"/> (G5 frameless vitals, G4 5v5 bust wings, streak banners, MatchResultsView MVP screen).
    /// - Always builds visible HUD chrome even when <c>!interactive</c> (disabling <see cref="GraphicRaycaster"/> and hiding the intro scrim
    ///   so headless EditMode screenshots capture crisp gameplay + full HUD).
    /// </summary>
    public sealed class MatchHud : IDisposable
    {
        static readonly Color ScoreCharcoal = new Color(20f / 255f, 24f / 255f, 31f / 255f, 0.94f); // #14181F
        static readonly Color ScoreAllyDefault = new Color(41f / 255f, 182f / 255f, 246f / 255f, 1f); // #29B6F6
        static readonly Color ScoreEnemyDefault = MenuArt.SignalVermilion; // #FF4655
        static readonly string[] PrebuiltScores = BuildScoreTable(30);

        const float BanterTagCooldownSeconds = 8.0f;
        const float BanterRescueCooldownSeconds = 6.0f;

        public readonly Canvas Canvas;
        public readonly SafeArea SafeArea;
        public readonly RectTransform SafeRoot;
        public readonly VirtualJoystick Joystick;
        public readonly LookPad Look;
        public readonly AbilityArcView AbilityArc;
        public readonly CardHandView CardHand;
        public readonly CrosshairView Crosshair;
        public readonly OverheadBarsView OverheadBars;
        public readonly BarangayMinimapView Minimap;
        /// <summary>Aim down sight, under the minimap on the stick side (docs/SPEC.md §8).</summary>
        public readonly AdsButton Ads;
        public readonly PingAndBanterView PingAndBanter;
        public readonly MatchOverlaysView Overlays;
        public readonly RectTransform HeaderRect;
        public readonly RectTransform OwnTeamRect;
        public readonly RectTransform JoystickBaseRect;
        public readonly RectTransform HeroVitalsRect;
        public readonly RectTransform PauseButtonRect;
        public readonly RectTransform QuickChatButtonRect;
        public readonly RectTransform MapToggleButtonRect;
        public readonly Button MapToggleButton;
        public readonly bool Interactive;

        public bool UsingCardsTray { get; private set; }
        public Func<int, Vector3?> WorldFeetOverride { get; set; }

        readonly Image _allyScoreBg;
        readonly Image _enemyScoreBg;
        readonly Text _allyScoreText;
        readonly Text _enemyScoreText;
        readonly Text _clock;
        readonly Image _mapToggleRim;
        readonly Image _mapToggleDisc;
        readonly Image _mapToggleRingIcon;
        readonly Text _mapToggleLabel;
        readonly Transform _introOverlay;
        readonly AudioHub _audio;
        readonly List<RectTransform> _activeControlsScratch = new List<RectTransform>(16);
        readonly List<string> _banterAlliesScratch = new List<string>(5);
        readonly List<string> _banterEnemiesScratch = new List<string>(5);

        SimBridge _boundAudioSim;
        int _lastLocalDamageHp = -1;
        bool _appliedLeftHanded;
        bool _minimapLayoutAssigned;
        bool _lastMapVisible;
        bool _combatControlsHidden;
        int _lastMyScore = int.MinValue;
        int _lastTheirScore = int.MinValue;
        int _lastClockSeconds = int.MinValue;
        int _lastScoreColorblind = -1;
        MatchPhaseKind _lastHeaderPhase = (MatchPhaseKind)(-1);
        bool _lastOvertime;
        bool _lastTraining;
        bool _matchStartBanterShown;
        float _banterCooldownLeft;
        int _banterSeedCounter = 17;
        bool _disposed;

        static string[] BuildScoreTable(int count)
        {
            var table = new string[count];
            for (int i = 0; i < count; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture);
            return table;
        }

        public MatchHud(Transform parent, Camera camera, bool interactive)
            : this(parent, null, camera, interactive, null, null, null)
        {
        }

        public MatchHud(
            Transform parent,
            SimBridge sim,
            Camera camera,
            bool interactive,
            AudioHub audio = null,
            Action onPlayAgain = null,
            Action onBackToHome = null)
        {
            Interactive = interactive;
            _audio = audio;
            Canvas = Ui.MakeCanvas("MatchHud", parent, camera);

            var raycaster = Canvas.GetComponent<GraphicRaycaster>();
            if (!interactive && raycaster != null)
                raycaster.enabled = false;

            SafeRoot = Ui.Fill("SafeArea", Canvas.transform);
            SafeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
            SafeArea.Apply();

            bool leftHanded = SettingsStore.Current.LeftHanded;
            _appliedLeftHanded = leftHanded;

            // 1. World-projected 10-kid overhead plates (enemy HP strictly hidden) + center crosshair + the top-left minimap (on by default,
            //    docs/SPEC.md §8)
            OverheadBars = new OverheadBarsView(SafeRoot, Canvas);
            Crosshair = CrosshairView.Build(SafeRoot);
            Crosshair.Audio = audio;
            Minimap = new BarangayMinimapView(SafeRoot, null, Canvas, initialVisible: true);
            Minimap.SetLeftHanded(leftHanded);

            // 2. Locked G4 (A3): Minimalist split-color top-center scoreboard (200x46 nests cleanly in SquadWings' 212 px center gap)
            //    Flat Ally Blue (#29B6F6) | Dark Charcoal (#14181F) clock | Flat Enemy Red (#FF4655) — zero heavy enclosing card!
            RectTransform header = Ui.Panel(
                "Header",
                SafeRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(200f, 46f));
            HeaderRect = header;

            RectTransform allyBlock = Ui.Panel("AllyScoreBlock", header, new Vector2(0f, 0f), new Vector2(0.28f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _allyScoreBg = Ui.MakeImage("Bg", allyBlock, ScoreboardAllyColor(), round: false, raycast: false);
            StretchInset(_allyScoreBg.rectTransform, 0f);
            _allyScoreText = Ui.MakeText("Score", allyBlock, "0", 24, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            StretchInset(_allyScoreText.rectTransform, 0f);

            RectTransform clockBlock = Ui.Panel("ClockBlock", header, new Vector2(0.28f, 0f), new Vector2(0.72f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image clockBg = Ui.MakeImage("Bg", clockBlock, ScoreCharcoal, round: false, raycast: false);
            StretchInset(clockBg.rectTransform, 0f);
            Image clockBottomHairline = Ui.MakeImage("BottomHairline", clockBlock, new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.34f), round: false, raycast: false);
            clockBottomHairline.rectTransform.anchorMin = new Vector2(0f, 0f);
            clockBottomHairline.rectTransform.anchorMax = new Vector2(1f, 0f);
            clockBottomHairline.rectTransform.pivot = new Vector2(0.5f, 0f);
            clockBottomHairline.rectTransform.anchoredPosition = Vector2.zero;
            clockBottomHairline.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            _clock = Ui.MakeText("Clock", clockBlock, "3:00", 19, MenuArt.PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _clock.resizeTextForBestFit = true;
            _clock.resizeTextMinSize = 13;
            _clock.resizeTextMaxSize = 19;
            StretchInset(_clock.rectTransform, 2f);

            RectTransform enemyBlock = Ui.Panel("EnemyScoreBlock", header, new Vector2(0.72f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _enemyScoreBg = Ui.MakeImage("Bg", enemyBlock, ScoreboardEnemyColor(), round: false, raycast: false);
            StretchInset(_enemyScoreBg.rectTransform, 0f);
            _enemyScoreText = Ui.MakeText("Score", enemyBlock, "0", 24, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            StretchInset(_enemyScoreText.rectTransform, 0f);

            Image seamL = Ui.MakeImage("SeamLeft", header, MenuArt.ObsidianSolid, round: false, raycast: false);
            seamL.rectTransform.anchorMin = new Vector2(0.28f, 0f);
            seamL.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            seamL.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            seamL.rectTransform.anchoredPosition = Vector2.zero;
            seamL.rectTransform.sizeDelta = new Vector2(1.5f, 0f);

            Image seamR = Ui.MakeImage("SeamRight", header, MenuArt.ObsidianSolid, round: false, raycast: false);
            seamR.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            seamR.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            seamR.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            seamR.rectTransform.anchoredPosition = Vector2.zero;
            seamR.rectTransform.sizeDelta = new Vector2(1.5f, 0f);

            // 2b. >= 48 dp top-strip Barangay Radar toggle button (clear of PauseButton, SquadWings, HeroVitalsChip & Header across 21:9..4:3)
            MapToggleButton = BuildMapToggleButton(
                SafeRoot,
                audio,
                out MapToggleButtonRect,
                out _mapToggleRim,
                out _mapToggleDisc,
                out _mapToggleRingIcon,
                out _mapToggleLabel);

            // 3. Layout-only OwnTeam anchor seated over AllySquadWing (zero graphic children; strictly disjoint from PauseButton, HeroVitalsChip, and Header)
            OwnTeamRect = Ui.Panel(
                "OwnTeam",
                SafeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(116f, -8f),
                new Vector2(220f, 46f));

            // 4. Multi-touch thumb zones + radial ability arc & 4-card cycle tray (always built so screenshots show full controls)
            Look = LookPad.Build(SafeRoot);
            Look.SetLeftHanded(leftHanded);

            Joystick = VirtualJoystick.Build(SafeRoot);
            Joystick.SetLeftHanded(leftHanded);
            JoystickBaseRect = Joystick.transform.Find("Base") as RectTransform;

            // built after the stick so it takes touches over the top of the move zone
            Ads = AdsButton.Build(SafeRoot, ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas), interactive);
            Ads.ToggleMode = SettingsStore.Current.AdsToggle;

            AbilityArc = AbilityArcView.Build(SafeRoot, leftHanded);
            AbilityArc.Audio = audio;
            CardHand = CardHandView.Build(SafeRoot, leftHanded, SettingsStore.Current.CardsTrayPlacement);

            UsingCardsTray = CardHandView.ShouldUseCardsTray(sim, SettingsStore.Current);
            CardHand.Root.gameObject.SetActive(UsingCardsTray);
            AbilityArc.Root.gameObject.SetActive(!UsingCardsTray);

            // 4b. 5-wedge radial Tactical Ping Wheel & non-sightbox Taglish banter strip (under full-screen Pause/Results modals)
            PingAndBanter = new PingAndBanterView(SafeRoot, Canvas, interactive);
            PingAndBanter.PingEmitted += OnPingEmitted;

            // 5. Persistent HUD overlays & modal sheets on top
            Overlays = new MatchOverlaysView(SafeRoot, sim, camera, interactive, onPlayAgain, onBackToHome, audio);
            HeroVitalsRect = Overlays.Root.Find("HeroVitalsChip") as RectTransform;
            PauseButtonRect = Overlays.Root.Find("PauseButton") as RectTransform;
            QuickChatButtonRect = Overlays.Root.Find("QuickChatButton") as RectTransform;
            _introOverlay = Overlays.Root.Find("RoundIntroOverlay");
            if (!interactive && _introOverlay != null && _introOverlay.gameObject.activeSelf)
                _introOverlay.gameObject.SetActive(false);

            UpdateOwnTeamBounds();
            UpdateMapToggleAndMinimapLayout();
            SyncMapToggleVisual(force: true);

            bool initModalBlocking = Overlays != null && Overlays.IsBlockingGameplayInput && (interactive || (sim != null && sim.Phase != MatchPhaseKind.Intro));
            if (initModalBlocking)
            {
                if (MapToggleButtonRect != null && MapToggleButtonRect.gameObject.activeSelf)
                    MapToggleButtonRect.gameObject.SetActive(false);
                if (Minimap != null && Minimap.Root != null && Minimap.Root.gameObject.activeSelf)
                    Minimap.Root.gameObject.SetActive(false);
                if (PingAndBanter != null && PingAndBanter.PingButtonRect != null && PingAndBanter.PingButtonRect.gameObject.activeSelf)
                    PingAndBanter.PingButtonRect.gameObject.SetActive(false);
            }

            if (sim != null)
            {
                Crosshair.Bind(sim);
                BindAudioSim(sim);
            }

            SettingsStore.Current.Changed += OnSettingsChanged;
            SafeArea.Changed += OnSafeAreaChanged;
            // Subscribed AFTER PingAndBanter so MatchHud's zero-overlap Ping/Banter slots always run after PingAndBanter.RefreshLayout
            ScreenFitMetrics.SimulatedViewportChanged += OnSimulatedViewportChanged;
        }

        /// <summary>Returns all active persistent/interactive HUD control RectTransforms for ScreenFitBoundsTests non-overlap audits.</summary>
        public IReadOnlyList<RectTransform> ActiveControlRects
        {
            get
            {
                CollectActiveControlRects(_activeControlsScratch);
                return _activeControlsScratch;
            }
        }

        public void CollectActiveControlRects(List<RectTransform> output)
        {
            if (output == null) return;
            output.Clear();
            AddIfActive(output, JoystickBaseRect);
            AddIfActive(output, HeaderRect);
            AddIfActive(output, HeroVitalsRect);
            AddIfActive(output, PauseButtonRect);
            AddIfActive(output, QuickChatButtonRect);
            AddIfActive(output, MapToggleButtonRect);
            AddIfActive(output, PingAndBanter?.PingButtonRect);

            if (UsingCardsTray)
            {
                if (CardHand != null && CardHand.Root != null && CardHand.Root.gameObject.activeInHierarchy)
                {
                    IReadOnlyList<RectTransform> slots = CardHand.CardSlotRects;
                    for (int i = 0; i < slots.Count; i++)
                        AddIfActive(output, slots[i]);
                }
            }
            else if (AbilityArc != null)
            {
                IReadOnlyList<RectTransform> arcButtons = AbilityArc.AllActiveControlRects;
                for (int i = 0; i < arcButtons.Count; i++)
                    AddIfActive(output, arcButtons[i]);
            }
        }

        static void AddIfActive(List<RectTransform> list, RectTransform rt)
        {
            if (rt != null && rt.gameObject.activeInHierarchy)
                list.Add(rt);
        }

        public void SetLeftHanded(bool leftHanded)
        {
            _appliedLeftHanded = leftHanded;
            Joystick?.SetLeftHanded(leftHanded);
            Look?.SetLeftHanded(leftHanded);
            AbilityArc?.SetLeftHanded(leftHanded);
            CardHand?.SetLeftHanded(leftHanded);
            Minimap?.SetLeftHanded(leftHanded);
            UpdateMapToggleAndMinimapLayout();
        }

        public void RefreshLayout()
        {
            if (_disposed) return;

            if (Joystick != null && AbilityArc != null && Joystick.LeftHanded != AbilityArc.LeftHanded)
            {
                bool stickHand = Joystick.LeftHanded;
                _appliedLeftHanded = stickHand;
                Look?.SetLeftHanded(stickHand);
                AbilityArc.SetLeftHanded(stickHand);
                CardHand?.SetLeftHanded(stickHand);
                Minimap?.SetLeftHanded(stickHand);
            }

            UpdateOwnTeamBounds();
            Joystick?.RefreshLayout();
            Look?.RefreshLayout();
            Crosshair?.RefreshLayout();
            AbilityArc?.RefreshLayout();
            CardHand?.RefreshLayout();
            Overlays?.RefreshLayout();
            PingAndBanter?.RefreshLayout();
            UpdateMapToggleAndMinimapLayout();
        }

        public void Refresh(SimBridge sim)
        {
            Camera cam = Canvas != null ? Canvas.worldCamera : null;
            float dt = Application.isPlaying ? Time.deltaTime : 0f;
            Refresh(sim, cam, null, null, dt);
        }

        public void Refresh(SimBridge sim, Camera worldCamera, ShoulderCamera shoulder, MapLayoutData layout, float dt)
        {
            if (_disposed || sim == null) return;

            // the canvas scale and the thumb clusters settle after the HUD is built and change with the safe area: re-fit the HP chip
            // then (not every frame: the floating stick's base moves while it is held)
            Vector2 safeSize = SafeRoot != null ? SafeRoot.rect.size : Vector2.zero;
            if (safeSize != _vitalsFitSize || _appliedLeftHanded != _vitalsFitLeftHanded)
            {
                _vitalsFitSize = safeSize;
                _vitalsFitLeftHanded = _appliedLeftHanded;
                LayoutHeroVitals();
            }

            float stepDt = Mathf.Max(0f, dt);
            if (_banterCooldownLeft > 0f && stepDt > 0f)
                _banterCooldownLeft = Mathf.Max(0f, _banterCooldownLeft - stepDt);

            Camera effectiveCamera = worldCamera != null
                ? worldCamera
                : (Canvas != null ? Canvas.worldCamera : null);

            SyncHandedness();
            BindAudioSim(sim);
            RefreshHeader(sim);

            if (layout != null && !_minimapLayoutAssigned)
            {
                Minimap.SetLayout(layout);
                _minimapLayoutAssigned = true;
            }

            SyncCombatTrayMode(sim);

            bool preAiming = (AbilityArc != null && AbilityArc.IsDraggingAim) ||
                             (UsingCardsTray && CardHand != null && (CardHand.CurrentInput.HeldSlot >= 0 || CardHand.CurrentInput.IsDraggingAim));
            if (effectiveCamera != null)
                Overlays.WorldCamera = effectiveCamera;
            Overlays.SuppressTakedownFeed = preAiming;
            Overlays.Refresh(sim, stepDt);

            if (!Interactive && _introOverlay != null && _introOverlay.gameObject.activeSelf)
                _introOverlay.gameObject.SetActive(false);

            KidSnapshot? localKid = sim.LocalKid;
            bool localTagged = (localKid.HasValue && localKid.Value.Tagged) || (Overlays != null && Overlays.IsTaggedOverlayVisible);
            bool modalBlocking = Overlays != null && Overlays.IsBlockingGameplayInput && (Interactive || sim.Phase != MatchPhaseKind.Intro);
            bool hideCombatControls = modalBlocking || localTagged;

            SyncCombatControlsVisibility(hideCombatControls);

            AbilityInputState abilityState = default;
            bool isAimingAbility = false;
            AimCameraKind activeSkillAim = AimCameraKind.Normal;
            if (!hideCombatControls)
            {
                if (UsingCardsTray)
                    CardHand.Sync(sim, stepDt, shoulder, effectiveCamera, layout);
                else
                {
                    AbilityArc.Sync(sim, stepDt, shoulder, effectiveCamera, layout);
                    activeSkillAim = AbilityArc.ActiveAimCamera;
                }

                abilityState = UsingCardsTray ? CardHand.CurrentInput : AbilityArc.CurrentInput;
                isAimingAbility = (AbilityArc != null && AbilityArc.IsDraggingAim) || abilityState.HeldSlot >= 0 || abilityState.IsDraggingAim;
            }

            if (shoulder != null)
            {
                shoulder.SetSkillAimMode(hideCombatControls ? AimCameraKind.Normal : activeSkillAim);
                shoulder.ExtraPivotHeight = (!hideCombatControls && localKid.HasValue && localKid.Value.OnStilts) ? 0.95f : 0f;
                if (localKid.HasValue && !localKid.Value.Tagged)
                {
                    int curHp = localKid.Value.Hp;
                    if (_lastLocalDamageHp > 0 && curHp < _lastLocalDamageHp)
                        shoulder.AddHitShake(Mathf.Clamp01((_lastLocalDamageHp - curHp) / 30f));
                    _lastLocalDamageHp = curHp;
                }
                else
                {
                    _lastLocalDamageHp = -1;
                }
            }

            if (Overlays.SuppressTakedownFeed != isAimingAbility)
            {
                Overlays.SuppressTakedownFeed = isAimingAbility;
                Overlays.Refresh(sim, 0f);
                if (!Interactive && _introOverlay != null && _introOverlay.gameObject.activeSelf)
                    _introOverlay.gameObject.SetActive(false);
            }

            // Ping button hides cleanly during aim-cancel drag, modals, or tagged sidewalk state; banter yields to CaptureAlert/modals
            if (PingAndBanter != null)
            {
                bool showPingBtn = !hideCombatControls && !isAimingAbility && !preAiming;
                if (PingAndBanter.PingButtonRect != null && PingAndBanter.PingButtonRect.gameObject.activeSelf != showPingBtn)
                {
                    PingAndBanter.PingButtonRect.gameObject.SetActive(showPingBtn);
                    if (!showPingBtn) PingAndBanter.CloseWheel();
                }

                bool yieldBanter = modalBlocking || isAimingAbility || (Overlays != null && Overlays.IsCaptureAlertVisible);
                if (yieldBanter && PingAndBanter.IsBanterVisible)
                    PingAndBanter.HideBanter();
                else if (Application.isPlaying && !_matchStartBanterShown && !yieldBanter && sim.Phase == MatchPhaseKind.Playing)
                    TriggerMatchStartBanter(sim);
            }

            float camYaw = shoulder != null ? shoulder.Yaw : 0f;
            float adsBlend = shoulder != null ? shoulder.AdsBlend : 0f;
            if (Crosshair != null && Crosshair.Root != null && Crosshair.Root.gameObject.activeSelf != !hideCombatControls)
                Crosshair.Root.gameObject.SetActive(!hideCombatControls);
            if (!hideCombatControls)
            {
                Crosshair.Bind(sim);
                Crosshair.Sync(sim, stepDt, camYaw, isAimingAbility, abilityState.IsCancelHovered, effectiveCamera, adsBlend, layout);
            }

            OverheadBars.SetVisible(!modalBlocking);
            if (!modalBlocking && effectiveCamera != null)
                OverheadBars.Refresh(sim, effectiveCamera, WorldFeetOverride, layout);
            else
                OverheadBars.HideAll();

            bool showMapChrome = !modalBlocking;
            if (MapToggleButtonRect != null && MapToggleButtonRect.gameObject.activeSelf != showMapChrome)
                MapToggleButtonRect.gameObject.SetActive(showMapChrome);

            if (Minimap != null)
            {
                bool showMinimap = showMapChrome && Minimap.IsVisible;
                if (Minimap.Root != null && Minimap.Root.gameObject.activeSelf != showMinimap)
                    Minimap.Root.gameObject.SetActive(showMinimap);

                if (showMapChrome)
                {
                    SyncMapToggleVisual();
                    Minimap.Refresh(sim, camYaw, sim != null ? sim.Neutrals : null);
                }
            }
        }

        public AbilityInputState SampleAbilityInput(
            SimBridge sim,
            ShoulderCamera shoulder = null,
            Camera worldCamera = null,
            MapLayoutData layout = null)
        {
            if (!Interactive || _disposed)
                return default;

            bool localTagged = sim != null && sim.LocalKid.HasValue && sim.LocalKid.Value.Tagged;
            if (localTagged || (Overlays != null && (Overlays.IsBlockingGameplayInput || Overlays.IsTaggedOverlayVisible)))
            {
                CancelActiveTouches();
                return default;
            }

            SyncCombatTrayMode(sim);
            AbilityInputState state = UsingCardsTray
                ? CardHand.SampleInput(sim, shoulder, worldCamera, layout)
                : AbilityArc.SampleInput(sim, shoulder, worldCamera, layout);
            if (shoulder != null)
                shoulder.SetSkillAimMode(UsingCardsTray ? AimCameraKind.Normal : AbilityArc.ActiveAimCamera);
            if (Ads != null)
            {
                Ads.ToggleMode = SettingsStore.Current.AdsToggle;
                if (Ads.IsOn) state.Buttons |= InputButtons.Ads;
            }
            return state;
        }

        public bool HandleBack()
        {
            return Overlays != null && Overlays.HandleBack();
        }

        public void CancelActiveTouches()
        {
            Joystick?.Cancel();
            Look?.Cancel();
            Ads?.Cancel();
            AbilityArc?.CancelAll();
            CardHand?.CancelAll();
            PingAndBanter?.CloseWheel();
            if (Overlays != null)
                Overlays.SuppressTakedownFeed = false;
        }

        static Color ScoreboardAllyColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(0.22f, 0.64f, 1.00f, 1f);
                case 3: return new Color(0.16f, 0.86f, 0.80f, 1f);
                default: return ScoreAllyDefault;
            }
        }

        static Color ScoreboardEnemyColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(0.96f, 0.54f, 0.14f, 1f);
                case 3: return new Color(0.96f, 0.30f, 0.46f, 1f);
                default: return ScoreEnemyDefault;
            }
        }

        static string FormatScoreDigit(int score)
        {
            if (score >= 0 && score < PrebuiltScores.Length)
                return PrebuiltScores[score];
            return score.ToString(CultureInfo.InvariantCulture);
        }

        void RefreshHeader(SimBridge sim)
        {
            int cbMode = SettingsStore.Current.ColorblindMode;
            if (cbMode != _lastScoreColorblind)
            {
                _lastScoreColorblind = cbMode;
                if (_allyScoreBg != null) _allyScoreBg.color = ScoreboardAllyColor();
                if (_enemyScoreBg != null) _enemyScoreBg.color = ScoreboardEnemyColor();
            }

            int localTeam = sim.LocalPlayerTeam;
            int myScore = sim.ScoreOf(localTeam);
            int theirScore = sim.ScoreOf(1 - localTeam);
            if (myScore != _lastMyScore)
            {
                _lastMyScore = myScore;
                if (_allyScoreText != null) _allyScoreText.text = FormatScoreDigit(myScore);
            }
            if (theirScore != _lastTheirScore)
            {
                _lastTheirScore = theirScore;
                if (_enemyScoreText != null) _enemyScoreText.text = FormatScoreDigit(theirScore);
            }

            int seconds = Mathf.Max(0, sim.ClockCountsDown ? Mathf.CeilToInt(sim.ClockSeconds) : Mathf.FloorToInt(sim.ClockSeconds));
            MatchPhaseKind phase = sim.Phase;
            bool overtime = sim.ClockStage == ClockStageKind.Overtime;
            bool training = sim.IsTraining;
            if (seconds != _lastClockSeconds || phase != _lastHeaderPhase || overtime != _lastOvertime || training != _lastTraining)
            {
                _lastClockSeconds = seconds;
                _lastHeaderPhase = phase;
                _lastOvertime = overtime;
                _lastTraining = training;

                string mmss = (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("D2", CultureInfo.InvariantCulture);
                if (training)
                    _clock.text = "TRAIN";
                else if (overtime && phase == MatchPhaseKind.Playing)
                    _clock.text = "OT " + mmss;
                else
                    _clock.text = mmss;

                _clock.color = overtime ? MenuArt.CoralGold : MenuArt.PaperWhite;
            }
        }

        void SyncCombatTrayMode(SimBridge sim)
        {
            bool wantCards = CardHandView.ShouldUseCardsTray(sim, SettingsStore.Current);
            if (wantCards == UsingCardsTray) return;

            UsingCardsTray = wantCards;
            if (wantCards)
            {
                AbilityArc.CancelAll();
                AbilityArc.Root.gameObject.SetActive(false);
                CardHand.Root.gameObject.SetActive(!_combatControlsHidden);
                if (!_combatControlsHidden)
                    CardHand.RefreshLayout();
            }
            else
            {
                CardHand.CancelAll();
                CardHand.Root.gameObject.SetActive(false);
                AbilityArc.Root.gameObject.SetActive(!_combatControlsHidden);
                if (!_combatControlsHidden)
                    AbilityArc.RefreshLayout();
            }
        }

        void SyncCombatControlsVisibility(bool hide)
        {
            if (hide)
                CancelActiveTouches();

            _combatControlsHidden = hide;
            bool showControls = !hide;

            if (Joystick != null && Joystick.gameObject.activeSelf != showControls)
                Joystick.gameObject.SetActive(showControls);
            if (Look != null && Look.gameObject.activeSelf != showControls)
                Look.gameObject.SetActive(showControls);
            if (Ads != null && Ads.gameObject.activeSelf != showControls)
                Ads.gameObject.SetActive(showControls);

            bool showCards = showControls && UsingCardsTray;
            bool showArc = showControls && !UsingCardsTray;
            if (CardHand != null && CardHand.Root != null && CardHand.Root.gameObject.activeSelf != showCards)
            {
                CardHand.Root.gameObject.SetActive(showCards);
                if (showCards) CardHand.RefreshLayout();
            }
            if (AbilityArc != null && AbilityArc.Root != null && AbilityArc.Root.gameObject.activeSelf != showArc)
            {
                AbilityArc.Root.gameObject.SetActive(showArc);
                if (showArc) AbilityArc.RefreshLayout();
            }
        }

        Button BuildMapToggleButton(
            Transform parent,
            AudioHub audio,
            out RectTransform rect,
            out Image rim,
            out Image disc,
            out Image ringIcon,
            out Text label)
        {
            float sz = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
            rect = Ui.Panel("MapToggleButton", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, -12f), new Vector2(sz, sz));

            disc = Ui.MakeImage("Disc", rect, new Color(MenuArt.ObsidianSolid.r, MenuArt.ObsidianSolid.g, MenuArt.ObsidianSolid.b, 0.72f), round: true, raycast: Interactive);
            StretchInset(disc.rectTransform, 1.5f);

            rim = Ui.MakeImage("Rim", rect, new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.78f), round: false, raycast: false);
            rim.sprite = KomiksUi.RingSprite(96, 2);
            StretchInset(rim.rectTransform, 0f);

            ringIcon = Ui.MakeImage("RadarRing", rect, MenuArt.PaperWhite);
            ringIcon.sprite = KomiksUi.RingSprite(64, 5);
            ringIcon.rectTransform.anchorMin = ringIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ringIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ringIcon.rectTransform.anchoredPosition = new Vector2(0f, 7f);
            ringIcon.rectTransform.sizeDelta = new Vector2(28f, 28f);

            Image crossH = Ui.MakeImage("RadarCrossH", ringIcon.transform, new Color(1f, 1f, 1f, 0.45f));
            crossH.rectTransform.anchorMin = crossH.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crossH.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossH.rectTransform.sizeDelta = new Vector2(18f, 1.5f);

            Image crossV = Ui.MakeImage("RadarCrossV", ringIcon.transform, new Color(1f, 1f, 1f, 0.45f));
            crossV.rectTransform.anchorMin = crossV.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crossV.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossV.rectTransform.sizeDelta = new Vector2(1.5f, 18f);

            Image ping = Ui.MakeImage("RadarPing", ringIcon.transform, MenuArt.WaterCyan, round: true);
            ping.rectTransform.anchorMin = ping.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ping.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ping.rectTransform.anchoredPosition = new Vector2(4f, 3.5f);
            ping.rectTransform.sizeDelta = new Vector2(6f, 6f);

            label = Ui.MakeText("Label", rect, "MAP", 12, MenuArt.PaperWhite, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            label.rectTransform.offsetMin = new Vector2(4f, 6f);
            label.rectTransform.offsetMax = new Vector2(-4f, 0f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = disc;
            btn.interactable = Interactive;
            if (Interactive)
            {
                btn.onClick.AddListener(() =>
                {
                    Haptics.LightTap();
                    audio?.PlayUiTap();
                    Minimap?.Toggle();
                    SyncMapToggleVisual(force: true);
                });
            }
            UiShaders.AttachPressSpring(btn, 0.94f, 2f);
            return btn;
        }

        static void StretchInset(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        void SyncMapToggleVisual(bool force = false)
        {
            if (_mapToggleRim == null || Minimap == null) return;
            bool mapOn = Minimap.IsVisible;
            if (!force && mapOn == _lastMapVisible) return;
            _lastMapVisible = mapOn;

            _mapToggleRim.color = mapOn
                ? new Color(MenuArt.WaterCyan.r, MenuArt.WaterCyan.g, MenuArt.WaterCyan.b, 0.90f)
                : new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.72f);
            _mapToggleDisc.color = mapOn
                ? new Color(0.082f, 0.125f, 0.175f, 0.86f)
                : new Color(MenuArt.ObsidianSolid.r, MenuArt.ObsidianSolid.g, MenuArt.ObsidianSolid.b, 0.72f);
            if (_mapToggleRingIcon != null)
                _mapToggleRingIcon.color = mapOn ? MenuArt.WaterCyan : MenuArt.PaperWhite;
            if (_mapToggleLabel != null)
                _mapToggleLabel.color = mapOn ? MenuArt.WaterCyan : MenuArt.PaperWhite;
        }

        void UpdateMapToggleAndMinimapLayout()
        {
            if (MapToggleButtonRect == null) return;

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float dp = ScreenFitMetrics.DpToCanvasPx(1f, Canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
            MapToggleButtonRect.sizeDelta = new Vector2(minTouch, minTouch);

            RectTransform refCorner = _appliedLeftHanded ? QuickChatButtonRect : PauseButtonRect;
            float cornerInset = refCorner != null
                ? Mathf.Max(12f, Mathf.Abs(refCorner.anchoredPosition.x))
                : Mathf.Max(24f, 12f * dp);
            float cornerW = refCorner != null && refCorner.sizeDelta.x > 1f ? refCorner.sizeDelta.x : minTouch;
            float inlineX = cornerInset + cornerW + 10f;

            float sideReserve = Mathf.Max(140f, minTouch * 2f + 44f);
            float totalWingSpan = ScreenFitMetrics.ClampWingSpread(680f, sideReserve, 740f, Canvas);
            float wingOuterFromEdge = (safe.x - totalWingSpan) * 0.5f;

            if (inlineX + minTouch + 8f <= wingOuterFromEdge)
            {
                Vector2 topCorner = _appliedLeftHanded ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
                MapToggleButtonRect.anchorMin = MapToggleButtonRect.anchorMax = topCorner;
                MapToggleButtonRect.pivot = topCorner;
                MapToggleButtonRect.anchoredPosition = new Vector2(_appliedLeftHanded ? -inlineX : inlineX, -12f);
            }
            else if (!_appliedLeftHanded)
            {
                MapToggleButtonRect.anchorMin = MapToggleButtonRect.anchorMax = new Vector2(0f, 1f);
                MapToggleButtonRect.pivot = new Vector2(0f, 1f);
                float pauseRight = PauseButtonRect != null
                    ? Mathf.Abs(PauseButtonRect.anchoredPosition.x) + PauseButtonRect.sizeDelta.x
                    : 226f;
                MapToggleButtonRect.anchoredPosition = new Vector2(pauseRight + 10f, -68f);
            }
            else
            {
                MapToggleButtonRect.anchorMin = MapToggleButtonRect.anchorMax = new Vector2(1f, 1f);
                MapToggleButtonRect.pivot = new Vector2(1f, 1f);
                MapToggleButtonRect.anchoredPosition = new Vector2(-cornerInset, -(minTouch + 134f));
            }

            if (Minimap != null && Minimap.Root != null)
            {
                if (_appliedLeftHanded)
                {
                    Minimap.Root.anchorMin = Minimap.Root.anchorMax = new Vector2(1f, 1f);
                    Minimap.Root.pivot = new Vector2(1f, 1f);
                    float radarTop = inlineX + minTouch + 8f <= wingOuterFromEdge
                        ? -(minTouch + 22f + 110f)
                        : -(minTouch * 2f + 144f);
                    Minimap.Root.anchoredPosition = new Vector2(-cornerInset, radarTop);
                }
                else
                {
                    Minimap.Root.anchorMin = Minimap.Root.anchorMax = new Vector2(0f, 1f);
                    Minimap.Root.pivot = new Vector2(0f, 1f);
                    // top-left corner, cleanly under the pause button (HeroVitalsChip always stays bottom-center)
                    float pauseBottom = PauseButtonRect != null
                        ? Mathf.Abs(PauseButtonRect.anchoredPosition.y) + PauseButtonRect.sizeDelta.y
                        : minTouch + 12f;
                    Minimap.Root.anchoredPosition = new Vector2(12f, -(pauseBottom + 10f));
                }
            }

            if (Ads != null && Minimap != null && Minimap.Root != null)
            {
                // the camera and ADS zone of the 4-finger layout (docs/SPEC.md §8): under the minimap, centred on it; left-handed,
                // the minimap already sits lower (under the map button), so ADS goes beside it, toward the centre, clear of the stick
                RectTransform ads = Ads.Rect;
                ads.sizeDelta = new Vector2(minTouch, minTouch);
                ads.anchorMin = ads.anchorMax = ads.pivot = Minimap.Root.anchorMin;
                Vector2 at = Minimap.Root.anchoredPosition;
                float map = BarangayMinimapView.PanelSize;
                if (_appliedLeftHanded)
                {
                    float sightRight = safe.x * 0.725f + 2f;
                    float gap = Mathf.Clamp(safe.x - sightRight - Mathf.Abs(at.x) - map - minTouch, 0f, 12f);
                    ads.anchoredPosition = new Vector2(at.x - map - gap, at.y);
                }
                else
                {
                    ads.anchoredPosition = new Vector2(at.x + (map - minTouch) * 0.5f, at.y - map - 12f);
                }
            }

            LayoutPingAndBanter(safe, dp, minTouch);
            LayoutHeroVitals();
        }

        void LayoutPingAndBanter(Vector2 safe, float dp, float minTouch)
        {
            if (PingAndBanter == null) return;

            // Place PingButton on the top strip directly beside QuickChat (RightHanded) or Pause (LeftHanded),
            // strictly outside SquadWings, Header, Minimap, and the 45%x50% center sightbox across 21:9..4:3!
            RectTransform pingBtn = PingAndBanter.PingButtonRect;
            if (pingBtn != null)
            {
                pingBtn.sizeDelta = new Vector2(minTouch, minTouch);
                if (_appliedLeftHanded)
                {
                    float pauseInset = PauseButtonRect != null ? Mathf.Max(12f, Mathf.Abs(PauseButtonRect.anchoredPosition.x)) : 12f;
                    float pauseW = PauseButtonRect != null && PauseButtonRect.sizeDelta.x > 1f ? PauseButtonRect.sizeDelta.x : minTouch;
                    pingBtn.anchorMin = pingBtn.anchorMax = new Vector2(0f, 1f);
                    pingBtn.pivot = new Vector2(0f, 1f);
                    pingBtn.anchoredPosition = new Vector2(pauseInset + pauseW + 10f, -12f);
                }
                else
                {
                    float chatInset = QuickChatButtonRect != null
                        ? Mathf.Max(24f, Mathf.Abs(QuickChatButtonRect.anchoredPosition.x))
                        : Mathf.Max(24f, 12f * dp);
                    float chatW = QuickChatButtonRect != null && QuickChatButtonRect.sizeDelta.x > 1f ? QuickChatButtonRect.sizeDelta.x : minTouch;
                    pingBtn.anchorMin = pingBtn.anchorMax = new Vector2(1f, 1f);
                    pingBtn.pivot = new Vector2(1f, 1f);
                    pingBtn.anchoredPosition = new Vector2(-(chatInset + chatW + 10f), -12f);
                }
            }

            // Place Taglish Banter strip top-center right below the 46 px Header/Wings and strictly ABOVE the 45%x50% center sightbox
            RectTransform banter = PingAndBanter.BanterRoot;
            if (banter != null)
            {
                float banterW = Mathf.Clamp(safe.x * 0.34f, 260f, 360f);
                float sightboxTop = safe.y * 0.25f;
                float banterH = Mathf.Clamp(sightboxTop - 68f, 56f, 72f);
                banter.anchorMin = banter.anchorMax = new Vector2(0.5f, 1f);
                banter.pivot = new Vector2(0.5f, 1f);
                banter.anchoredPosition = new Vector2(0f, -60f);
                banter.sizeDelta = new Vector2(banterW, banterH);
            }
        }

        static readonly Vector3[] Corners = new Vector3[4];
        Vector2 _vitalsFitSize = new Vector2(-1f, -1f);
        bool _vitalsFitLeftHanded;

        /// <summary>
        /// Locked G5 (A1): Frameless floating HP + Water vitals ALWAYS stay cleanly anchored at bottom-center
        /// between the left thumbstick and right ability cluster (never jumping to top-left over the minimap during spawn!).
        /// Uses analytic fallback bounds when Canvas world corners have not settled on frame 0.
        /// </summary>
        void LayoutHeroVitals()
        {
            if (HeroVitalsRect == null || !(HeroVitalsRect.parent is RectTransform space)) return;

            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(Canvas);
            Rect area = space.rect;
            bool rectValid = area.width > 120f;
            float areaMinX = rectValid ? area.xMin : 0f;
            float areaMaxX = rectValid ? area.xMax : safeSize.x;
            float lo = areaMinX;
            float hi = areaMaxX;
            bool stickOnLeft = !_appliedLeftHanded;

            void Bound(RectTransform rt, bool onLeft)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) return;
                rt.GetWorldCorners(Corners);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    float x = space.InverseTransformPoint(Corners[i]).x;
                    min = Mathf.Min(min, x);
                    max = Mathf.Max(max, x);
                }
                if (max <= min + 1f) return;
                if (onLeft) lo = Mathf.Max(lo, max);
                else hi = Mathf.Min(hi, min);
            }

            if (rectValid)
            {
                Bound(JoystickBaseRect, stickOnLeft);
                if (UsingCardsTray && CardHand != null && CardHand.Root != null && CardHand.Root.gameObject.activeInHierarchy)
                {
                    IReadOnlyList<RectTransform> slots = CardHand.CardSlotRects;
                    for (int i = 0; i < slots.Count; i++)
                        Bound(slots[i], !stickOnLeft);
                }
                else if (AbilityArc != null && AbilityArc.Root != null && AbilityArc.Root.gameObject.activeInHierarchy)
                {
                    IReadOnlyList<RectTransform> buttons = AbilityArc.InteractiveButtonRects;
                    for (int i = 0; i < buttons.Count; i++)
                        Bound(buttons[i], !stickOnLeft);
                }
            }

            // Analytic fallback when Canvas layout has not settled yet (e.g. frame-0 spawn freeze capture)
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
            float dp = ScreenFitMetrics.DpToCanvasPx(1f, Canvas);
            float approxStickReach = Mathf.Clamp(minTouch * 2.5f + 44f, 200f, safeSize.x * 0.30f);
            float approxArcReach = Mathf.Clamp(264f * dp, 240f, safeSize.x * 0.50f);
            if (lo <= areaMinX + 4f)
                lo = areaMinX + (stickOnLeft ? approxStickReach : approxArcReach);
            if (hi >= areaMaxX - 4f)
                hi = areaMaxX - (stickOnLeft ? approxArcReach : approxStickReach);
            if (hi - lo < 120f)
            {
                float mid = (areaMinX + areaMaxX) * 0.5f;
                lo = mid - 96f;
                hi = mid + 96f;
            }

            const float margin = 8f;
            float width = Mathf.Max(1f, HeroVitalsRect.sizeDelta.x);
            float availW = Mathf.Max(96f, hi - lo - margin * 2f);
            float scale = Mathf.Clamp(availW / width, 0.48f, 1f);

            HeroVitalsRect.anchorMin = HeroVitalsRect.anchorMax = new Vector2(0f, 0f);
            HeroVitalsRect.pivot = new Vector2(0.5f, 0f);
            HeroVitalsRect.localScale = Vector3.one * scale;
            HeroVitalsRect.anchoredPosition = new Vector2((lo + hi) * 0.5f - areaMinX, 14f);
        }

        void SyncHandedness()
        {
            bool leftHanded = SettingsStore.Current.LeftHanded;
            if (leftHanded == _appliedLeftHanded) return;
            SetLeftHanded(leftHanded);
        }

        void UpdateOwnTeamBounds()
        {
            if (OwnTeamRect == null) return;
            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
            float sideReserve = Mathf.Max(140f, minTouch * 2f + 44f);
            float totalWingSpan = ScreenFitMetrics.ClampWingSpread(680f, sideReserve, 740f, Canvas);
            float leftX = Mathf.Max(minTouch + 20f, (safe.x - totalWingSpan) * 0.5f);
            float maxW = Mathf.Clamp(safe.x * 0.5f - 106f - leftX, 96f, 234f);
            OwnTeamRect.anchoredPosition = new Vector2(leftX, -8f);
            OwnTeamRect.sizeDelta = new Vector2(maxW, 46f);
        }

        void OnSettingsChanged()
        {
            if (_disposed) return;
            SyncHandedness();
            CardHand?.SetPlacement(SettingsStore.Current.CardsTrayPlacement);
        }

        void OnSafeAreaChanged(SafeArea _)
        {
            RefreshLayout();
        }

        void OnSimulatedViewportChanged()
        {
            if (_disposed) return;
            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float dp = ScreenFitMetrics.DpToCanvasPx(1f, Canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
            LayoutPingAndBanter(safe, dp, minTouch);
        }

        void OnPingEmitted(TacticalPingKind kind, string message)
        {
            if (_disposed) return;
            Haptics.LightTap();
            _audio?.PlayUiTap("confirm");
            if (_boundAudioSim != null && _boundAudioSim.LocalKid.HasValue && Minimap != null)
                Minimap.PushSoundRipple(_boundAudioSim.LocalKid.Value.Position, intensity: 1.15f, isSkill: kind == TacticalPingKind.Attack || kind == TacticalPingKind.Danger);
        }

        /// <summary>Triggers a LoreCodexData match-start Taglish banter toast (Ate Lorna or rival matchup intro).</summary>
        public void TriggerMatchStartBanter(SimBridge sim)
        {
            if (_disposed || PingAndBanter == null || sim == null) return;
            _matchStartBanterShown = true;
            _banterAlliesScratch.Clear();
            _banterEnemiesScratch.Clear();
            int localTeam = sim.LocalPlayerTeam;
            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            for (int i = 0; i < kids.Count; i++)
            {
                string cid = kids[i].CharacterId;
                if (string.IsNullOrEmpty(cid)) continue;
                if (kids[i].Team == localTeam)
                {
                    if (_banterAlliesScratch.Count < 5) _banterAlliesScratch.Add(cid);
                }
                else if (_banterEnemiesScratch.Count < 5)
                {
                    _banterEnemiesScratch.Add(cid);
                }
            }
            PingAndBanter.ShowMatchStartBanter(_banterAlliesScratch, _banterEnemiesScratch, _banterSeedCounter++);
            _banterCooldownLeft = BanterRescueCooldownSeconds;
            _audio?.PlayBanter();
        }

        void BindAudioSim(SimBridge sim)
        {
            if (ReferenceEquals(_boundAudioSim, sim)) return;
            UnbindAudioSim();
            _boundAudioSim = sim;
            _matchStartBanterShown = false;
            _banterCooldownLeft = 0f;
            if (_boundAudioSim == null) return;
            _boundAudioSim.TiradorReloadStarted += OnSimTiradorReloadStarted;
            _boundAudioSim.WaterInsufficient += OnSimWaterInsufficient;
            _boundAudioSim.Tagged += OnSimTaggedForBanter;
            _boundAudioSim.Rescued += OnSimRescuedForBanter;
        }

        void UnbindAudioSim()
        {
            if (_boundAudioSim == null) return;
            _boundAudioSim.TiradorReloadStarted -= OnSimTiradorReloadStarted;
            _boundAudioSim.WaterInsufficient -= OnSimWaterInsufficient;
            _boundAudioSim.Tagged -= OnSimTaggedForBanter;
            _boundAudioSim.Rescued -= OnSimRescuedForBanter;
            _boundAudioSim = null;
        }

        void OnSimTiradorReloadStarted(int playerId)
        {
            if (_boundAudioSim != null && playerId == _boundAudioSim.LocalPlayerId)
                _audio?.PlayReloadClack();
        }

        void OnSimWaterInsufficient(int playerId)
        {
            if (_boundAudioSim != null && playerId == _boundAudioSim.LocalPlayerId)
                _audio?.PlayWaterEmpty();
        }

        void OnSimTaggedForBanter(int victimId, int taggerId)
        {
            if (_disposed || !Application.isPlaying || _boundAudioSim == null || PingAndBanter == null) return;
            if (_banterCooldownLeft > 0f || (Overlays != null && (Overlays.IsCaptureAlertVisible || Overlays.IsBlockingGameplayInput))) return;

            int localId = _boundAudioSim.LocalPlayerId;
            if (taggerId != localId && victimId != localId) return;

            KidSnapshot? attacker = _boundAudioSim.Find(taggerId);
            KidSnapshot? victim = _boundAudioSim.Find(victimId);
            if (!attacker.HasValue || !victim.HasValue) return;

            PingAndBanter.ShowTagBanter(attacker.Value.CharacterId, victim.Value.CharacterId, _banterSeedCounter++);
            _banterCooldownLeft = BanterTagCooldownSeconds;
            _audio?.PlayBanter();
        }

        void OnSimRescuedForBanter(int savedId, int rescuerId)
        {
            if (_disposed || !Application.isPlaying || _boundAudioSim == null || PingAndBanter == null) return;
            if (_banterCooldownLeft > 0f || (Overlays != null && (Overlays.IsCaptureAlertVisible || Overlays.IsBlockingGameplayInput))) return;
            if (rescuerId == MatchSim.ReleasedByRevenge) return;

            int localId = _boundAudioSim.LocalPlayerId;
            if (savedId != localId && rescuerId != localId) return;

            KidSnapshot? rescuer = _boundAudioSim.Find(rescuerId);
            KidSnapshot? saved = _boundAudioSim.Find(savedId);
            if (!rescuer.HasValue || !saved.HasValue) return;

            PingAndBanter.ShowRescueBanter(rescuer.Value.CharacterId, saved.Value.CharacterId, _banterSeedCounter++);
            _banterCooldownLeft = BanterRescueCooldownSeconds;
            _audio?.PlayBanter();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnbindAudioSim();
            ScreenFitMetrics.SimulatedViewportChanged -= OnSimulatedViewportChanged;
            SettingsStore.Current.Changed -= OnSettingsChanged;
            if (SafeArea != null)
                SafeArea.Changed -= OnSafeAreaChanged;
            if (PingAndBanter != null)
                PingAndBanter.PingEmitted -= OnPingEmitted;

            Crosshair?.Unbind();
            PingAndBanter?.Dispose();
            Minimap?.Dispose();
            Overlays?.Dispose();

            if (Canvas != null)
                KalyeahResources.Kill(Canvas.gameObject);
        }
    }
}
