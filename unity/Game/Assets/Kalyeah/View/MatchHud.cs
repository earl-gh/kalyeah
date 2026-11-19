using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full Agawang Base match HUD on the 1280 x 720 landscape SafeArea canvas:
    /// - Preserves <see cref="Canvas"/>, <see cref="Joystick"/>, <see cref="Look"/>, <c>SafeArea/Header</c>, and <c>SafeArea/OwnTeam</c>
    ///   bounds invariants verified by <c>ScreenFitBoundsTests</c> across 21:9..4:3 + LeftHanded + 180 deg flips.
    /// - Wires <see cref="OverheadBarsView"/> (enemy HP hidden), <see cref="CrosshairView"/>, <see cref="BarangayMinimapView"/> (default OFF),
    ///   <see cref="AbilityArcView"/> + <see cref="CardHandView"/> (toggled via <see cref="CardHandView.ShouldUseCardsTray"/>),
    ///   and <see cref="MatchOverlaysView"/> (HeroVitalsChip, 5v5 SquadWings, TakedownFeed, CaptureAlert, Tagged/Swap, QuickChat, Pause, Victory).
    /// - Always builds visible HUD chrome even when <c>!interactive</c> (disabling <see cref="GraphicRaycaster"/> and hiding the intro scrim
    ///   so headless EditMode screenshots capture crisp gameplay + full HUD).
    /// </summary>
    public sealed class MatchHud : IDisposable
    {
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

        readonly Text _score;
        readonly Text _clock;
        readonly Image _mapToggleRim;
        readonly Image _mapToggleDisc;
        readonly Image _mapToggleRingIcon;
        readonly Text _mapToggleLabel;
        readonly Transform _introOverlay;
        readonly List<RectTransform> _activeControlsScratch = new List<RectTransform>(16);

        bool _appliedLeftHanded;
        bool _minimapLayoutAssigned;
        bool _lastMapVisible;
        bool _combatControlsHidden;
        int _lastMyScore = int.MinValue;
        int _lastTheirScore = int.MinValue;
        int _lastClockSeconds = int.MinValue;
        MatchPhaseKind _lastHeaderPhase = (MatchPhaseKind)(-1);
        bool _lastOvertime;
        bool _lastTraining;
        bool _disposed;

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
            Minimap = new BarangayMinimapView(SafeRoot, null, Canvas, initialVisible: true);
            Minimap.SetLeftHanded(leftHanded);

            // 2. Top-center Score & Clock header (236 px wide so it nests cleanly inside MatchOverlaysView's 256 px SquadWings center gap)
            RectTransform header = Ui.Panel(
                "Header",
                SafeRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -10f),
                new Vector2(236f, 78f));
            HeaderRect = header;
            Image headerPlate = KomiksUi.MakeInkPanel(
                "Backdrop",
                header,
                new Color(0.08f, 0.11f, 0.20f, 0.94f),
                new Color(0.95f, 0.96f, 1.00f, 0.44f),
                12,
                2);
            headerPlate.rectTransform.anchorMin = Vector2.zero;
            headerPlate.rectTransform.anchorMax = Vector2.one;
            headerPlate.rectTransform.offsetMin = Vector2.zero;
            headerPlate.rectTransform.offsetMax = Vector2.zero;

            _score = Ui.MakeText("Score", header, "0 - 0", 30, Ui.Cream, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            _score.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            _score.rectTransform.anchorMax = Vector2.one;
            _score.rectTransform.offsetMin = new Vector2(8f, 0f);
            _score.rectTransform.offsetMax = new Vector2(-8f, -4f);

            _clock = Ui.MakeText("Clock", header, "3:00", 18, new Color(1f, 0.92f, 0.60f, 0.98f), TextAnchor.LowerCenter);
            _clock.rectTransform.anchorMin = Vector2.zero;
            _clock.rectTransform.anchorMax = new Vector2(1f, 0.44f);
            _clock.rectTransform.offsetMin = new Vector2(8f, 5f);
            _clock.rectTransform.offsetMax = new Vector2(-8f, 0f);

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
                new Vector2(116f, -14f),
                new Vector2(220f, 48f));

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
            CardHand = CardHandView.Build(SafeRoot, leftHanded, SettingsStore.Current.CardsTrayPlacement);

            UsingCardsTray = CardHandView.ShouldUseCardsTray(sim, SettingsStore.Current);
            CardHand.Root.gameObject.SetActive(UsingCardsTray);
            AbilityArc.Root.gameObject.SetActive(!UsingCardsTray);

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
            }

            if (sim != null)
                Crosshair.Bind(sim);

            SettingsStore.Current.Changed += OnSettingsChanged;
            SafeArea.Changed += OnSafeAreaChanged;
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
            Camera effectiveCamera = worldCamera != null
                ? worldCamera
                : (Canvas != null ? Canvas.worldCamera : null);

            SyncHandedness();
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
            if (!hideCombatControls)
            {
                if (UsingCardsTray)
                    CardHand.Sync(sim, stepDt, shoulder, effectiveCamera, layout);
                else
                    AbilityArc.Sync(sim, stepDt, shoulder, effectiveCamera, layout);

                abilityState = UsingCardsTray ? CardHand.CurrentInput : AbilityArc.CurrentInput;
                isAimingAbility = (AbilityArc != null && AbilityArc.IsDraggingAim) || abilityState.HeldSlot >= 0 || abilityState.IsDraggingAim;
            }

            if (Overlays.SuppressTakedownFeed != isAimingAbility)
            {
                Overlays.SuppressTakedownFeed = isAimingAbility;
                Overlays.Refresh(sim, 0f);
                if (!Interactive && _introOverlay != null && _introOverlay.gameObject.activeSelf)
                    _introOverlay.gameObject.SetActive(false);
            }

            float camYaw = shoulder != null ? shoulder.Yaw : 0f;
            if (Crosshair != null && Crosshair.Root != null && Crosshair.Root.gameObject.activeSelf != !hideCombatControls)
                Crosshair.Root.gameObject.SetActive(!hideCombatControls);
            if (!hideCombatControls)
            {
                Crosshair.Bind(sim);
                Crosshair.Sync(sim, stepDt, camYaw, isAimingAbility, abilityState.IsCancelHovered);
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
                    Minimap.Refresh(sim, camYaw);
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
            if (Overlays != null)
                Overlays.SuppressTakedownFeed = false;
        }

        void RefreshHeader(SimBridge sim)
        {
            int localTeam = sim.LocalPlayerTeam;
            int myScore = sim.ScoreOf(localTeam);
            int theirScore = sim.ScoreOf(1 - localTeam);
            if (myScore != _lastMyScore || theirScore != _lastTheirScore)
            {
                _lastMyScore = myScore;
                _lastTheirScore = theirScore;
                _score.text = myScore.ToString(CultureInfo.InvariantCulture) + " - " + theirScore.ToString(CultureInfo.InvariantCulture);
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
                    _clock.text = "TRAINING";
                else if (overtime && phase == MatchPhaseKind.Playing)
                    _clock.text = "OT " + mmss;
                else
                    _clock.text = mmss;

                _clock.color = overtime
                    ? new Color(1f, 0.56f, 0.34f, 1f)
                    : new Color(1f, 0.90f, 0.58f, 0.98f);
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

            rim = Ui.MakeImage("Rim", rect, new Color(1f, 1f, 1f, 0.85f), round: true, raycast: Interactive);
            StretchInset(rim.rectTransform, 2f);

            disc = Ui.MakeImage("Disc", rect, new Color(0.09f, 0.11f, 0.19f, 0.92f), round: true);
            StretchInset(disc.rectTransform, 4.5f);

            ringIcon = Ui.MakeImage("RadarRing", rect, Color.white);
            ringIcon.sprite = KomiksUi.RingSprite(64, 5);
            ringIcon.rectTransform.anchorMin = ringIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ringIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ringIcon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            ringIcon.rectTransform.sizeDelta = new Vector2(34f, 34f);

            Image crossH = Ui.MakeImage("RadarCrossH", ringIcon.transform, new Color(1f, 1f, 1f, 0.45f));
            crossH.rectTransform.anchorMin = crossH.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crossH.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossH.rectTransform.sizeDelta = new Vector2(22f, 2f);

            Image crossV = Ui.MakeImage("RadarCrossV", ringIcon.transform, new Color(1f, 1f, 1f, 0.45f));
            crossV.rectTransform.anchorMin = crossV.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crossV.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossV.rectTransform.sizeDelta = new Vector2(2f, 22f);

            Image ping = Ui.MakeImage("RadarPing", ringIcon.transform, KomiksUi.MangoGold, round: true);
            ping.rectTransform.anchorMin = ping.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ping.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ping.rectTransform.anchoredPosition = new Vector2(5f, 4f);
            ping.rectTransform.sizeDelta = new Vector2(7f, 7f);

            label = Ui.MakeText("Label", rect, "MAP", 13, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            label.rectTransform.offsetMin = new Vector2(4f, 8f);
            label.rectTransform.offsetMax = new Vector2(-4f, 0f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = rim;
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

            _mapToggleRim.color = mapOn ? KomiksUi.MangoGold : new Color(1f, 1f, 1f, 0.85f);
            _mapToggleDisc.color = mapOn
                ? new Color(0.14f, 0.22f, 0.38f, 0.96f)
                : new Color(0.09f, 0.11f, 0.19f, 0.92f);
            if (_mapToggleRingIcon != null)
                _mapToggleRingIcon.color = mapOn ? KomiksUi.MangoGold : Color.white;
            if (_mapToggleLabel != null)
                _mapToggleLabel.color = mapOn ? KomiksUi.MangoGold : Color.white;
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

            float sideReserve = Mathf.Max(140f, minTouch + 28f);
            float totalWingSpan = ScreenFitMetrics.ClampWingSpread(760f, sideReserve, 920f, Canvas);
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
                    // top-left corner, under the settings button (the HP chip moved to the bottom centre)
                    float pauseBottom = PauseButtonRect != null
                        ? Mathf.Abs(PauseButtonRect.anchoredPosition.y) + PauseButtonRect.sizeDelta.y
                        : minTouch + 12f;
                    Minimap.Root.anchoredPosition = new Vector2(12f, -(pauseBottom + 10f));
                }
            }

            if (Ads != null && Minimap != null && Minimap.Root != null)
            {
                // under the minimap, centred on it: the camera and ADS zone of the 4-finger layout (docs/SPEC.md §8)
                RectTransform ads = Ads.Rect;
                ads.sizeDelta = new Vector2(minTouch, minTouch);
                ads.anchorMin = ads.anchorMax = ads.pivot = Minimap.Root.anchorMin;
                float inward = (BarangayMinimapView.PanelSize - minTouch) * 0.5f;
                Vector2 at = Minimap.Root.anchoredPosition;
                ads.anchoredPosition = new Vector2(at.x + (_appliedLeftHanded ? -inward : inward), at.y - BarangayMinimapView.PanelSize - 12f);
            }

            LayoutHeroVitals();
        }

        static readonly Vector3[] Corners = new Vector3[4];
        Vector2 _vitalsFitSize = new Vector2(-1f, -1f);
        bool _vitalsFitLeftHanded;

        /// <summary>
        /// HP and water sit bottom centre between the thumbs (docs/SPEC.md §8): centred in the gap between the stick and the ability
        /// cluster, scaled down when the gap is narrow (4:3 foldables), and back in the top-left corner when even that does not fit.
        /// </summary>
        void LayoutHeroVitals()
        {
            if (HeroVitalsRect == null || !(HeroVitalsRect.parent is RectTransform space)) return;
            Rect area = space.rect;
            float lo = area.xMin;
            float hi = area.xMax;
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
                if (onLeft) lo = Mathf.Max(lo, max);
                else hi = Mathf.Min(hi, min);
            }

            Bound(JoystickBaseRect, stickOnLeft);
            if (AbilityArc != null && AbilityArc.Root != null && AbilityArc.Root.gameObject.activeInHierarchy)
            {
                IReadOnlyList<RectTransform> buttons = AbilityArc.InteractiveButtonRects;
                for (int i = 0; i < buttons.Count; i++)
                    Bound(buttons[i], !stickOnLeft);
            }

            const float margin = 12f;
            float width = Mathf.Max(1f, HeroVitalsRect.sizeDelta.x);
            float scale = Mathf.Min(1f, (hi - lo - margin * 2f) / width);
            if (scale >= 0.6f)
            {
                HeroVitalsRect.anchorMin = HeroVitalsRect.anchorMax = new Vector2(0f, 0f);
                HeroVitalsRect.pivot = new Vector2(0.5f, 0f);
                HeroVitalsRect.localScale = Vector3.one * scale;
                HeroVitalsRect.anchoredPosition = new Vector2((lo + hi) * 0.5f - area.xMin, 14f);
            }
            else
            {
                float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, Canvas);
                HeroVitalsRect.anchorMin = HeroVitalsRect.anchorMax = new Vector2(0f, 1f);
                HeroVitalsRect.pivot = new Vector2(0f, 1f);
                HeroVitalsRect.localScale = Vector3.one;
                HeroVitalsRect.anchoredPosition = new Vector2(12f, -(minTouch + 22f));
                if (!_appliedLeftHanded && Minimap != null && Minimap.Root != null)
                    Minimap.Root.anchoredPosition = new Vector2(12f, -(minTouch + 22f + HeroVitalsRect.sizeDelta.y + 10f));
            }
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
            float sideReserve = Mathf.Max(140f, minTouch + 28f);
            float totalWingSpan = ScreenFitMetrics.ClampWingSpread(760f, sideReserve, 920f, Canvas);
            float leftX = Mathf.Max(minTouch + 20f, (safe.x - totalWingSpan) * 0.5f);
            float maxW = Mathf.Clamp(safe.x * 0.5f - 128f - leftX, 96f, 252f);
            OwnTeamRect.anchoredPosition = new Vector2(leftX, -14f);
            OwnTeamRect.sizeDelta = new Vector2(maxW, 48f);
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SettingsStore.Current.Changed -= OnSettingsChanged;
            if (SafeArea != null)
                SafeArea.Changed -= OnSafeAreaChanged;

            Crosshair?.Unbind();
            Minimap?.Dispose();
            Overlays?.Dispose();

            if (Canvas != null)
                KalyeahResources.Kill(Canvas.gameObject);
        }
    }
}
