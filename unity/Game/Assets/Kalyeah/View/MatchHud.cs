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
        public readonly MatchOverlaysView Overlays;
        public readonly RectTransform HeaderRect;
        public readonly RectTransform OwnTeamRect;
        public readonly RectTransform JoystickBaseRect;
        public readonly RectTransform HeroVitalsRect;
        public readonly RectTransform PauseButtonRect;
        public readonly RectTransform QuickChatButtonRect;
        public readonly bool Interactive;

        public bool UsingCardsTray { get; private set; }
        public Func<int, Vector3?> WorldFeetOverride { get; set; }

        readonly Text _score;
        readonly Text _clock;
        readonly Transform _introOverlay;
        readonly List<RectTransform> _activeControlsScratch = new List<RectTransform>(16);

        bool _appliedLeftHanded;
        bool _minimapLayoutAssigned;
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

            // 1. World-projected 10-kid overhead plates (enemy HP strictly hidden) + center crosshair + toggleable minimap (default OFF)
            OverheadBars = new OverheadBarsView(SafeRoot, Canvas);
            Crosshair = CrosshairView.Build(SafeRoot);
            Minimap = new BarangayMinimapView(SafeRoot, null, Canvas, initialVisible: false);
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
            Image headerPlate = KomiksUi.MakeInkPanel("Backdrop", header, Ui.Navy, KomiksUi.SoftBorder, 12, 2);
            headerPlate.rectTransform.anchorMin = Vector2.zero;
            headerPlate.rectTransform.anchorMax = Vector2.one;
            headerPlate.rectTransform.offsetMin = Vector2.zero;
            headerPlate.rectTransform.offsetMax = Vector2.zero;

            _score = Ui.MakeText("Score", header, "0 - 0", 30, Ui.Cream, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            _score.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            _score.rectTransform.anchorMax = Vector2.one;
            _score.rectTransform.offsetMin = new Vector2(8f, 0f);
            _score.rectTransform.offsetMax = new Vector2(-8f, -4f);

            _clock = Ui.MakeText("Clock", header, "3:00", 17, new Color(1f, 0.90f, 0.58f, 0.98f), TextAnchor.LowerCenter);
            _clock.rectTransform.anchorMin = Vector2.zero;
            _clock.rectTransform.anchorMax = new Vector2(1f, 0.44f);
            _clock.rectTransform.offsetMin = new Vector2(8f, 5f);
            _clock.rectTransform.offsetMax = new Vector2(-8f, 0f);

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

            if (UsingCardsTray)
                CardHand.Sync(sim, stepDt, shoulder, effectiveCamera, layout);
            else
                AbilityArc.Sync(sim, stepDt, shoulder, effectiveCamera, layout);

            AbilityInputState abilityState = UsingCardsTray ? CardHand.CurrentInput : AbilityArc.CurrentInput;
            float camYaw = shoulder != null ? shoulder.Yaw : 0f;
            bool isAimingAbility = abilityState.HeldSlot >= 0 || abilityState.IsDraggingAim;

            Crosshair.Bind(sim);
            Crosshair.Sync(sim, stepDt, camYaw, isAimingAbility, abilityState.IsCancelHovered);

            if (effectiveCamera != null)
                OverheadBars.Refresh(sim, effectiveCamera, WorldFeetOverride, layout);
            else
                OverheadBars.HideAll();

            Minimap.Refresh(sim, camYaw);

            if (effectiveCamera != null)
                Overlays.WorldCamera = effectiveCamera;
            Overlays.Refresh(sim, stepDt);

            if (!Interactive && _introOverlay != null && _introOverlay.gameObject.activeSelf)
                _introOverlay.gameObject.SetActive(false);
        }

        public AbilityInputState SampleAbilityInput(
            SimBridge sim,
            ShoulderCamera shoulder = null,
            Camera worldCamera = null,
            MapLayoutData layout = null)
        {
            if (!Interactive || _disposed)
                return default;

            if (Overlays != null && Overlays.IsBlockingGameplayInput)
            {
                AbilityArc.CancelAll();
                CardHand.CancelAll();
                return default;
            }

            SyncCombatTrayMode(sim);
            return UsingCardsTray
                ? CardHand.SampleInput(sim, shoulder, worldCamera, layout)
                : AbilityArc.SampleInput(sim, shoulder, worldCamera, layout);
        }

        public bool HandleBack()
        {
            return Overlays != null && Overlays.HandleBack();
        }

        public void CancelActiveTouches()
        {
            Joystick?.Cancel();
            Look?.Cancel();
            AbilityArc?.CancelAll();
            CardHand?.CancelAll();
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
                CardHand.Root.gameObject.SetActive(true);
                CardHand.RefreshLayout();
            }
            else
            {
                CardHand.CancelAll();
                CardHand.Root.gameObject.SetActive(false);
                AbilityArc.Root.gameObject.SetActive(true);
                AbilityArc.RefreshLayout();
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
            float leftX = minTouch + 20f;
            float maxW = Mathf.Clamp(safe.x * 0.5f - 126f - leftX, 96f, 220f);
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
