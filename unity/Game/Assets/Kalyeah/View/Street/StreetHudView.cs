using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// PointerId-locked touch handler for a Larong Kalye action button:
    ///   - Tap mode (SIPA, DIVE, LUPA): fires one clean press pulse on pointer-down when enabled.
    ///   - Hold-to-charge mode (TSINELAS / THROW): holds while pressed, accumulates drag pixels to steer aim,
    ///     lights up the top-right Cancel Zone when hovered, and cancels safely without firing on app pause/focus loss.
    /// </summary>
    public sealed class StreetActionButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        RectTransform _cancelZone;
        int _pointerId = NoPointer;
        bool _holdMode;
        bool _actionEnabled = true;
        bool _tapPending;
        bool _cancelPending;
        bool _leftButtonOnce;
        bool _hoveringCancel;
        Vector2 _dragDeltaPx;

        public bool IsHeld => _pointerId != NoPointer && _holdMode;
        public bool IsHoveringCancel => _hoveringCancel;
        public int ActivePointerId => _pointerId;

        public void Configure(bool holdToCharge, bool actionEnabled, RectTransform cancelZone)
        {
            if (_holdMode && !holdToCharge && _pointerId != NoPointer)
                CancelSilently(requestSimCancel: true);

            _holdMode = holdToCharge;
            _actionEnabled = actionEnabled;
            _cancelZone = cancelZone;
        }

        public bool ConsumeTap()
        {
            if (!_tapPending) return false;
            _tapPending = false;
            return true;
        }

        public bool ConsumeCancel()
        {
            if (!_cancelPending) return false;
            _cancelPending = false;
            return true;
        }

        public Vector2 TakeDragPixels()
        {
            Vector2 d = _dragDeltaPx;
            _dragDeltaPx = Vector2.zero;
            return d;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != NoPointer || !isActiveAndEnabled) return;
            if (!_actionEnabled)
            {
                Haptics.CancelTick();
                return;
            }

            _pointerId = eventData.pointerId;
            _leftButtonOnce = false;
            _hoveringCancel = false;
            Haptics.LightTap();

            if (!_holdMode)
                _tapPending = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || !_holdMode) return;
            _dragDeltaPx += eventData.delta;

            var selfRect = transform as RectTransform;
            if (selfRect != null && !RectTransformUtility.RectangleContainsScreenPoint(selfRect, eventData.position, eventData.pressEventCamera))
                _leftButtonOnce = true;

            bool overCancel = _cancelZone != null
                && _cancelZone.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, eventData.position, eventData.pressEventCamera);

            if (!overCancel && _leftButtonOnce && selfRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(selfRect, eventData.position, eventData.pressEventCamera))
            {
                // Matches StreetGamePad.gd: dragging away then returning onto the Throw button also cancels cleanly.
                overCancel = true;
            }

            if (overCancel && !_hoveringCancel)
                Haptics.CancelTick();
            _hoveringCancel = overCancel;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            bool shouldCancel = _holdMode && _hoveringCancel;
            _pointerId = NoPointer;
            _hoveringCancel = false;
            _leftButtonOnce = false;
            if (shouldCancel)
            {
                _cancelPending = true;
                Haptics.CancelTick();
            }
        }

        void OnDisable() => CancelSilently(requestSimCancel: true);

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelSilently(requestSimCancel: true);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) CancelSilently(requestSimCancel: true);
        }

        public void CancelSilently(bool requestSimCancel)
        {
            bool wasHolding = _pointerId != NoPointer && _holdMode;
            _pointerId = NoPointer;
            _hoveringCancel = false;
            _leftButtonOnce = false;
            _dragDeltaPx = Vector2.zero;
            _tapPending = false;
            if (wasHolding && requestSimCancel)
                _cancelPending = true;
        }
    }

    /// <summary>
    /// Responsive 1280x720 SafeArea-pinned HUD for Patintero, Tumbang Preso, and Langit Lupa:
    ///   - Top Header: Mode label, Team/FFA Score, Match Timer, and Patintero Stall bar
    ///   - Role Pill: High-contrast RUNNER / GUARD / TAYA pill + 1-line street tactical prompt
    ///   - Brownout Alert: Urgent golden-amber banner + countdown during Langit Lupa brownouts
    ///   - Thumb Controls: VirtualJoystick + LookPad + right-thumb StreetActionInfo buttons (Throw/Sipa/Dive/Lupa)
    ///     with top-right Cancel Zone, left-handed mirroring, >= 48 dp touch floors, and 4:3 foldable height caps
    ///   - Match Over Summary: Victory/Defeat modal with Ranked LP delta and Play Again / Home callbacks
    /// </summary>
    public sealed class StreetHudView : IDisposable
    {
        static readonly Color HeaderBg = new Color(0.05f, 0.09f, 0.21f, 0.93f);
        static readonly Color NavyPillBg = new Color(0.05f, 0.09f, 0.22f, 0.94f);
        static readonly Color NavyPillBorder = new Color(0.84f, 0.76f, 0.60f, 0.52f);
        static readonly Color RunnerPillBg = new Color(0.14f, 0.48f, 0.92f, 0.96f);
        static readonly Color GuardPillBg = new Color(0.92f, 0.42f, 0.14f, 0.96f);
        static readonly Color TayaPillBg = new Color(0.88f, 0.20f, 0.20f, 0.96f);
        static readonly Color RunnerPillCb = new Color(0.12f, 0.66f, 0.92f, 0.96f);
        static readonly Color DangerPillCb = new Color(0.94f, 0.42f, 0.08f, 0.96f);

        static readonly Color ThrowFace = new Color(1.00f, 0.76f, 0.18f, 0.96f);
        static readonly Color SipaFace = new Color(0.28f, 0.80f, 0.96f, 0.96f);
        static readonly Color DiveFace = new Color(0.32f, 0.88f, 0.52f, 0.96f);
        static readonly Color LupaFace = new Color(0.80f, 0.55f, 0.28f, 0.96f);
        static readonly Color DisabledFace = new Color(0.32f, 0.38f, 0.48f, 0.72f);
        static readonly Color ReadyRingColor = new Color(0.46f, 1.00f, 0.58f, 0.96f);
        static readonly Color ChargingRingColor = new Color(1.00f, 0.94f, 0.62f, 0.96f);
        static readonly Color CancelIdleBg = new Color(0.76f, 0.16f, 0.18f, 0.88f);
        static readonly Color CancelHoverBg = new Color(1.00f, 0.22f, 0.20f, 0.98f);
        static readonly Color BrownoutAlertBg = new Color(0.14f, 0.11f, 0.32f, 0.95f);
        static readonly Color GoldText = new Color(1.00f, 0.84f, 0.28f, 1f);

        public readonly Canvas Canvas;
        public readonly RectTransform Safe;
        public readonly SafeArea SafeAreaComponent;
        public readonly VirtualJoystick Joystick;
        public readonly LookPad Look;
        public readonly bool Interactive;

        readonly Action _onPlayAgain;
        readonly Action _onHome;

        // Top header, timer pill, pause/leave button & role pill
        readonly RectTransform _headerRect;
        readonly RectTransform _timerPillRect;
        readonly Text _modeTitleText;
        readonly Text _scoreText;
        readonly Text _timerText;
        readonly GameObject _stallBarRoot;
        readonly RectTransform _stallFillRect;

        readonly RectTransform _leaveButtonRect;
        readonly RectTransform _rolePillRect;
        readonly Image _rolePillBg;
        readonly Text _rolePillText;
        readonly RectTransform _statusHintPillRect;
        readonly Text _statusHintText;

        // Top-right cancel zone
        readonly RectTransform _cancelZoneRect;
        readonly Image _cancelZoneBg;

        // Brownout alert & toast
        readonly GameObject _brownoutBannerObj;
        readonly Image _brownoutBannerBg;
        readonly Text _brownoutBannerText;
        float _lightsOnFlashLeft;
        bool _wasBrownout;

        // Action cluster (right thumb by default, left thumb when LeftHanded)
        readonly RectTransform _actionClusterRect;
        readonly RectTransform _primaryBtnRect;
        readonly Image _primaryRingImg;
        readonly Image _primaryFaceImg;
        readonly Text _primaryLabelText;
        readonly StreetActionButton _primaryHandler;

        readonly RectTransform _secondaryBtnRect;
        readonly Image _secondaryRingImg;
        readonly Image _secondaryFaceImg;
        readonly Text _secondaryLabelText;
        readonly StreetActionButton _secondaryHandler;

        readonly GameObject _tayaCornerBadge;
        readonly Text _tayaCornerText;

        // Leave confirmation & Match over modals
        readonly GameObject _leaveConfirmRoot;
        readonly GameObject _matchOverRoot;
        readonly Text _matchOverHeadline;
        readonly Text _matchOverSubhead;
        readonly Text _matchOverLpText;
        bool _leaveConfirmShown;
        bool _matchOverShown;
        bool _lpRecorded;

        // Dirty-check caches for zero steady-state string allocations
        string _cachedModeTitle = "";
        int _cachedScoreA = int.MinValue;
        int _cachedScoreB = int.MinValue;
        bool _cachedFfaMode;
        int _cachedClockSeconds = int.MinValue;
        string _cachedRoleWord = "";
        string _cachedHintLine = "\0";
        int _cachedBrownoutSeconds = int.MinValue;
        string _cachedPrimaryLabel = "";
        string _cachedSecondaryLabel = "";
        bool _appliedLeftHanded;
        float _appliedControlScale = -1f;

        public RectTransform HeaderRect => _headerRect;
        public RectTransform TimerPillRect => _timerPillRect;
        public RectTransform PauseButton => _leaveButtonRect;
        public RectTransform LeaveButtonRect => _leaveButtonRect;
        public RectTransform RolePillRect => _rolePillRect;
        public RectTransform StatusHintPillRect => _statusHintPillRect;
        public RectTransform PrimaryButtonRect => _primaryBtnRect;
        public RectTransform SecondaryButtonRect => _secondaryBtnRect;

        public bool PrimaryHeld => Interactive && !IsBlockingGameplayInput && _primaryHandler != null && _primaryHandler.IsHeld;
        public bool IsCancelHovered => _primaryHandler != null && _primaryHandler.IsHoveringCancel;
        public string DisplayedRoleText => _rolePillText != null ? _rolePillText.text : "";
        public string DisplayedScoreText => _scoreText != null ? _scoreText.text : "";
        public string DisplayedTimerText => _timerText != null ? _timerText.text : "";
        public bool IsBrownoutAlertVisible => _brownoutBannerObj != null && _brownoutBannerObj.activeSelf;
        public bool IsLeaveConfirmVisible => _leaveConfirmRoot != null && _leaveConfirmRoot.activeSelf;
        public bool IsMatchOverVisible => _matchOverRoot != null && _matchOverRoot.activeSelf;
        public bool IsBlockingGameplayInput => IsLeaveConfirmVisible || IsMatchOverVisible;

        public StreetHudView(
            Transform parent,
            StreetBridge bridge,
            Camera camera,
            bool interactive,
            Action onPlayAgain = null,
            Action onHome = null)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));

            Interactive = interactive;
            _onPlayAgain = onPlayAgain;
            _onHome = onHome;

            Canvas = Ui.MakeCanvas("StreetHud", parent, camera);
            if (!interactive)
            {
                var raycaster = Canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }

            Safe = Ui.Fill("SafeArea", Canvas.transform);
            SafeAreaComponent = Safe.gameObject.AddComponent<SafeArea>();
            SafeAreaComponent.Apply();

            bool leftHanded = SettingsStore.Current.LeftHanded;
            _appliedLeftHanded = leftHanded;

            // Always build LookPad + VirtualJoystick visually so screenshots & bounds tests show full mobile controls;
            // input raycasting & polling remain gated by `interactive`.
            Look = LookPad.Build(Safe, leftHanded);
            Joystick = VirtualJoystick.Build(Safe, leftHanded);

            Font displayFont = KalyeahResources.DisplayFont();
            Sprite navyPillSprite = KomiksUi.BorderedSprite(KomiksUi.RadiusM, 2, NavyPillBg, NavyPillBorder, topGloss: true);
            Sprite roundedWhiteSprite = KomiksUi.RoundedSprite(KomiksUi.RadiusM);

            // 1. Top-Center Score Header + 6 px Spaced Dark Navy Timer Pill
            _headerRect = Ui.Panel("Header", Safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(332f, 76f));
            Image headerPlate = MakeSlicedPill("Plate", _headerRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(headerPlate.rectTransform);

            _modeTitleText = Ui.MakeText("ModeTitle", _headerRect, "", 18, GoldText, TextAnchor.UpperCenter, displayFont);
            RectTransform modeTitleRt = _modeTitleText.rectTransform;
            modeTitleRt.anchorMin = Vector2.zero;
            modeTitleRt.anchorMax = Vector2.one;
            modeTitleRt.offsetMin = new Vector2(10f, 0f);
            modeTitleRt.offsetMax = new Vector2(-10f, -6f);

            _scoreText = Ui.MakeText("Score", _headerRect, "0 - 0", 28, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
            RectTransform scoreRt = _scoreText.rectTransform;
            scoreRt.anchorMin = Vector2.zero;
            scoreRt.anchorMax = Vector2.one;
            scoreRt.offsetMin = new Vector2(10f, 12f);
            scoreRt.offsetMax = new Vector2(-10f, -24f);

            // Patintero stall bar tucked along bottom inside edge of header
            RectTransform stallTrack = Ui.Panel("StallTrack", _headerRect, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 5f));
            Ui.MakeImage("TrackBg", stallTrack, new Color(0f, 0f, 0f, 0.45f)).rectTransform.sizeDelta = Vector2.zero;
            StretchFull(stallTrack.GetChild(0) as RectTransform);
            Image stallFill = Ui.MakeImage("StallFill", stallTrack, new Color(1f, 0.72f, 0.22f, 0.95f));
            _stallFillRect = stallFill.rectTransform;
            _stallFillRect.anchorMin = Vector2.zero;
            _stallFillRect.anchorMax = Vector2.one;
            _stallFillRect.pivot = new Vector2(0f, 0.5f);
            _stallFillRect.sizeDelta = Vector2.zero;
            _stallBarRoot = stallTrack.gameObject;
            _stallBarRoot.SetActive(false);

            // Timer pill spaced exactly 6 px below _headerRect (-8 - 76 - 6 = -90)
            _timerPillRect = Ui.Panel("TimerPill", Safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(132f, 32f));
            Image timerPillBg = MakeSlicedPill("TimerBg", _timerPillRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(timerPillBg.rectTransform);
            _timerText = Ui.MakeText("Timer", _timerPillRect, "5:00", 21, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
            _timerText.fontStyle = FontStyle.Bold;
            StretchFull(_timerText.rectTransform);

            // 2. Top-Left Pause/Leave Button + Role Pill + Dark Navy Tactical Hint Pill
            _leaveButtonRect = BuildPillButton(
                Safe,
                "PauseButton",
                "II LEAVE",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -10f),
                new Vector2(104f, 48f),
                navyPillSprite,
                Color.white,
                displayFont,
                19,
                interactive,
                () => ToggleLeaveConfirm());

            _rolePillRect = Ui.Panel("RolePill", Safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(126f, -10f), new Vector2(144f, 48f));
            _rolePillBg = MakeSlicedPill("RoleBg", _rolePillRect, roundedWhiteSprite, RunnerPillBg, raycast: false);
            StretchFull(_rolePillBg.rectTransform);
            _rolePillText = Ui.MakeText("RoleLabel", _rolePillRect, "RUNNER", 23, Color.white, TextAnchor.MiddleCenter, displayFont);
            StretchFull(_rolePillText.rectTransform);

            _statusHintPillRect = Ui.Panel("StatusHintPill", Safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -64f), new Vector2(360f, 36f));
            Image hintBg = MakeSlicedPill("HintBg", _statusHintPillRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(hintBg.rectTransform);
            _statusHintText = Ui.MakeText("StatusHint", _statusHintPillRect, "", 18, Ui.Cream, TextAnchor.MiddleLeft);
            _statusHintText.fontStyle = FontStyle.Bold;
            _statusHintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _statusHintText.verticalOverflow = VerticalWrapMode.Overflow;
            _statusHintText.resizeTextForBestFit = true;
            _statusHintText.resizeTextMinSize = 12;
            _statusHintText.resizeTextMaxSize = 18;
            RectTransform hintTextRect = _statusHintText.rectTransform;
            hintTextRect.anchorMin = Vector2.zero;
            hintTextRect.anchorMax = Vector2.one;
            hintTextRect.offsetMin = new Vector2(14f, 2f);
            hintTextRect.offsetMax = new Vector2(-14f, -2f);

            // 3. Brownout Alert Pill (top-right corner during Langit Lupa, completely clear of Header, HintPill, and center sightbox)
            RectTransform brownoutRect = Ui.Panel("BrownoutAlert", Safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -10f), new Vector2(276f, 44f));
            _brownoutBannerBg = MakeSlicedPill("AlertBg", brownoutRect, roundedWhiteSprite, BrownoutAlertBg, raycast: false);
            StretchFull(_brownoutBannerBg.rectTransform);
            _brownoutBannerText = Ui.MakeText("AlertText", brownoutRect, "BROWNOUT! 5M SIGHT", 18, GoldText, TextAnchor.MiddleCenter, displayFont);
            StretchFull(_brownoutBannerText.rectTransform);
            _brownoutBannerObj = brownoutRect.gameObject;
            _brownoutBannerObj.SetActive(false);

            // 4. Top-Right Cancel Zone (shown while holding/dragging Throw; stays top-right free of PauseButton)
            _cancelZoneRect = Ui.Panel("CancelZone", Safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(224f, 54f));
            _cancelZoneBg = MakeSlicedPill("CancelBg", _cancelZoneRect, roundedWhiteSprite, CancelIdleBg, raycast: false);
            StretchFull(_cancelZoneBg.rectTransform);
            Text cancelText = Ui.MakeText("CancelLabel", _cancelZoneRect, "RELEASE HERE TO CANCEL", 18, Color.white, TextAnchor.MiddleCenter, displayFont);
            StretchFull(cancelText.rectTransform);
            _cancelZoneRect.gameObject.SetActive(false);

            // 5. Right-Thumb Action Cluster (Primary: TSINELAS/THROW; Secondary: SIPA/DIVE/LUPA)
            _actionClusterRect = Ui.Panel("ActionCluster", Safe, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(280f, 240f));

            _primaryBtnRect = BuildActionOrb(
                _actionClusterRect,
                "PrimaryAction",
                new Vector2(-106f, 98f),
                124f,
                ThrowFace,
                displayFont,
                interactive,
                out _primaryRingImg,
                out _primaryFaceImg,
                out _primaryLabelText,
                out _primaryHandler);

            _secondaryBtnRect = BuildActionOrb(
                _actionClusterRect,
                "SecondaryAction",
                new Vector2(-232f, 76f),
                98f,
                DiveFace,
                displayFont,
                interactive,
                out _secondaryRingImg,
                out _secondaryFaceImg,
                out _secondaryLabelText,
                out _secondaryHandler);

            RectTransform tayaChip = Ui.Panel("TayaBadge", _actionClusterRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 34f), new Vector2(218f, 50f));
            Image tayaChipBg = MakeSlicedPill("Bg", tayaChip, roundedWhiteSprite, new Color(0.42f, 0.10f, 0.14f, 0.90f), raycast: false);
            StretchFull(tayaChipBg.rectTransform);
            _tayaCornerText = Ui.MakeText("Text", tayaChip, "TAYA: TOUCH TO TAG!", 19, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
            StretchFull(_tayaCornerText.rectTransform);
            _tayaCornerBadge = tayaChip.gameObject;
            _tayaCornerBadge.SetActive(false);

            // 6. Leave Confirmation Modal & Match Over Summary Modal
            _leaveConfirmRoot = BuildLeaveConfirmModal(Safe, displayFont, navyPillSprite, roundedWhiteSprite);
            _leaveConfirmRoot.SetActive(false);

            _matchOverRoot = BuildMatchOverModal(Safe, displayFont, out _matchOverHeadline, out _matchOverSubhead, out _matchOverLpText);
            _matchOverRoot.SetActive(false);

            SafeAreaComponent.Changed += OnSafeAreaChanged;
            SettingsStore.Current.Changed += OnSettingsChanged;
            ScreenFitMetrics.SimulatedViewportChanged += ApplyResponsiveLayout;

            ApplyResponsiveLayout();
            Refresh(bridge, 0f);
        }

        static RectTransform BuildActionOrb(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            float diameter,
            Color faceColor,
            Font displayFont,
            bool interactive,
            out Image ringImg,
            out Image faceImg,
            out Text labelText,
            out StreetActionButton handler)
        {
            RectTransform orb = Ui.Panel(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), anchoredPos, Vector2.one * diameter);

            Image outerTrack = Ui.MakeImage("Track", orb, new Color(0.04f, 0.08f, 0.18f, 0.82f), round: true, raycast: interactive);
            StretchFull(outerTrack.rectTransform);

            ringImg = Ui.MakeImage("ReadyRing", orb, ReadyRingColor, round: true, raycast: false);
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = true;
            ringImg.fillAmount = 1f;
            StretchFull(ringImg.rectTransform);

            faceImg = Ui.MakeImage("Face", orb, faceColor, round: true, raycast: false);
            faceImg.rectTransform.anchorMin = Vector2.zero;
            faceImg.rectTransform.anchorMax = Vector2.one;
            faceImg.rectTransform.sizeDelta = new Vector2(-14f, -14f);

            labelText = Ui.MakeText("Label", orb, "", 22, Color.white, TextAnchor.MiddleCenter, displayFont);
            StretchFull(labelText.rectTransform);

            handler = interactive ? orb.gameObject.AddComponent<StreetActionButton>() : null;
            return orb;
        }

        static Image MakeSlicedPill(string name, Transform parent, Sprite sprite, Color color, bool raycast)
        {
            Image img = Ui.MakeImage(name, parent, color, round: false, raycast: raycast);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        static RectTransform BuildPillButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Sprite pillSprite,
            Color bgColor,
            Font font,
            int fontSize,
            bool interactive,
            Action onClick)
        {
            RectTransform rect = Ui.Panel(name, parent, anchor, anchor, pivot, pos, size);
            Image bg = MakeSlicedPill("Bg", rect, pillSprite, bgColor, raycast: interactive);
            StretchFull(bg.rectTransform);
            if (interactive)
            {
                var btn = rect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                if (onClick != null) btn.onClick.AddListener(() => onClick());
            }
            Text txt = Ui.MakeText("Label", rect, label, fontSize, Ui.Cream, TextAnchor.MiddleCenter, font);
            txt.fontStyle = FontStyle.Bold;
            StretchFull(txt.rectTransform);
            return rect;
        }

        static RectTransform BuildSimpleButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color bgColor,
            Font font,
            Action onClick)
        {
            Sprite sprite = KomiksUi.RoundedSprite(KomiksUi.RadiusM);
            return BuildPillButton(parent, name, label, anchor, pivot, pos, size, sprite, bgColor, font, 22, true, onClick);
        }

        GameObject BuildLeaveConfirmModal(
            Transform parent,
            Font displayFont,
            Sprite cardSprite,
            Sprite btnSprite)
        {
            RectTransform overlay = Ui.Fill("LeaveConfirmOverlay", parent);
            Ui.MakeImage("Dimmer", overlay, new Color(0.03f, 0.05f, 0.12f, 0.78f), raycast: Interactive);
            StretchFull(overlay.GetChild(0) as RectTransform);

            RectTransform card = Ui.Panel("LeaveCard", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(468f, 252f));
            Image cardBg = MakeSlicedPill("CardPlate", card, cardSprite, Color.white, raycast: Interactive);
            StretchFull(cardBg.rectTransform);

            Text title = Ui.MakeText("Headline", card, "LEAVE STREET MATCH?", 32, GoldText, TextAnchor.UpperCenter, displayFont);
            title.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            Text body = Ui.MakeText("Body", card, "Return to the Barangay Home menu?", 20, Ui.Cream, TextAnchor.MiddleCenter);
            body.rectTransform.anchoredPosition = new Vector2(0f, 12f);

            BuildPillButton(
                card,
                "ResumeButton",
                "RESUME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-108f, 26f),
                new Vector2(188f, 56f),
                btnSprite,
                new Color(0.18f, 0.68f, 0.38f, 0.98f),
                displayFont,
                21,
                Interactive,
                () =>
                {
                    Haptics.LightTap();
                    SetLeaveConfirmOpen(false);
                });

            BuildPillButton(
                card,
                "ConfirmLeaveButton",
                "LEAVE TO HOME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(108f, 26f),
                new Vector2(188f, 56f),
                btnSprite,
                new Color(0.84f, 0.24f, 0.22f, 0.98f),
                displayFont,
                20,
                Interactive,
                () =>
                {
                    Haptics.LightTap();
                    SetLeaveConfirmOpen(false);
                    _onHome?.Invoke();
                });

            return overlay.gameObject;
        }

        GameObject BuildMatchOverModal(
            Transform parent,
            Font displayFont,
            out Text headline,
            out Text subhead,
            out Text lpText)
        {
            RectTransform overlay = Ui.Fill("MatchOverOverlay", parent);
            Ui.MakeImage("Dimmer", overlay, new Color(0.03f, 0.05f, 0.12f, 0.78f), raycast: Interactive);
            StretchFull(overlay.GetChild(0) as RectTransform);

            RectTransform card = Ui.Panel("SummaryCard", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 310f));
            Image cardBg = Ui.MakeImage("CardPlate", card, new Color(0.08f, 0.13f, 0.28f, 0.96f));
            StretchFull(cardBg.rectTransform);

            headline = Ui.MakeText("Headline", card, "PANALO!", 48, GoldText, TextAnchor.UpperCenter, displayFont);
            headline.rectTransform.anchoredPosition = new Vector2(0f, 96f);

            subhead = Ui.MakeText("Subhead", card, "Score 0 - 0", 26, Ui.Cream, TextAnchor.MiddleCenter);
            subhead.rectTransform.anchoredPosition = new Vector2(0f, 28f);

            lpText = Ui.MakeText("RankedLp", card, "", 22, new Color(0.48f, 0.96f, 0.64f, 1f), TextAnchor.MiddleCenter, displayFont);
            lpText.rectTransform.anchoredPosition = new Vector2(0f, -18f);

            BuildSimpleButton(
                card,
                "PlayAgainButton",
                "PLAY AGAIN",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-116f, 26f),
                new Vector2(196f, 58f),
                new Color(0.18f, 0.68f, 0.36f, 0.98f),
                displayFont,
                () =>
                {
                    Haptics.LightTap();
                    _onPlayAgain?.Invoke();
                });

            BuildSimpleButton(
                card,
                "HomeButton",
                "HOME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(116f, 26f),
                new Vector2(196f, 58f),
                new Color(0.22f, 0.34f, 0.56f, 0.98f),
                displayFont,
                () =>
                {
                    Haptics.LightTap();
                    _onHome?.Invoke();
                });

            return overlay.gameObject;
        }

        static void StretchFull(RectTransform r)
        {
            if (r == null) return;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        void OnSafeAreaChanged(SafeArea _) => ApplyResponsiveLayout();

        void OnSettingsChanged() => ApplyResponsiveLayout();

        public void ApplyResponsiveLayout()
        {
            if (Canvas == null || _actionClusterRect == null) return;

            bool leftHanded = SettingsStore.Current.LeftHanded;
            float userScale = Mathf.Clamp(SettingsStore.Current.ControlScale, 0.85f, 1.2f);
            if (_appliedLeftHanded != leftHanded)
            {
                _appliedLeftHanded = leftHanded;
                if (Joystick != null) Joystick.SetLeftHanded(leftHanded);
                if (Look != null) Look.SetLeftHanded(leftHanded);
            }
            _appliedControlScale = userScale;

            float foldableScale = ScreenFitMetrics.ClampFoldableClusterScale(280f * userScale, 236f * userScale, 160f, Canvas);
            float effScale = userScale * foldableScale;

            float primarySize = ScreenFitMetrics.ClampTouchSize(124f * effScale, ScreenFitMetrics.PrimaryFireMinDp, Canvas);
            float secondarySize = ScreenFitMetrics.ClampTouchSize(96f * effScale, 52f, Canvas);
            float sign = leftHanded ? 1f : -1f;
            Vector2 corner = leftHanded ? new Vector2(0f, 0f) : new Vector2(1f, 0f);

            _actionClusterRect.anchorMin = _actionClusterRect.anchorMax = corner;
            _actionClusterRect.pivot = corner;
            _actionClusterRect.anchoredPosition = Vector2.zero;

            float minGap = Mathf.Max(20f, ScreenFitMetrics.DpToCanvasPx(10f, Canvas));
            float edgePrimary = ScreenFitMetrics.ClampThumbSpreadOffset(36f + primarySize * 0.5f, 160f, Canvas);
            _primaryBtnRect.anchorMin = _primaryBtnRect.anchorMax = corner;
            _primaryBtnRect.sizeDelta = Vector2.one * primarySize;
            _primaryBtnRect.anchoredPosition = new Vector2(sign * edgePrimary, 36f + primarySize * 0.5f);

            float secondaryOffset = edgePrimary + primarySize * 0.5f + secondarySize * 0.5f + minGap;
            _secondaryBtnRect.anchorMin = _secondaryBtnRect.anchorMax = corner;
            _secondaryBtnRect.sizeDelta = Vector2.one * secondarySize;
            _secondaryBtnRect.anchoredPosition = new Vector2(sign * secondaryOffset, 30f + secondarySize * 0.5f);

            Vector2 leaveSize = ScreenFitMetrics.ClampTouchSize(new Vector2(104f, 48f), ScreenFitMetrics.MinTouchDp, Canvas);
            if (_leaveButtonRect != null)
            {
                _leaveButtonRect.anchoredPosition = new Vector2(14f, -10f);
                _leaveButtonRect.sizeDelta = leaveSize;
            }

            if (_rolePillRect != null)
            {
                _rolePillRect.anchoredPosition = new Vector2(14f + leaveSize.x + 8f, -10f);
                _rolePillRect.sizeDelta = new Vector2(144f, Mathf.Max(48f, Mathf.Min(leaveSize.y, 62f)));
            }

            if (_statusHintPillRect != null)
            {
                UpdateStatusHintLayout(leaveSize.y);
            }

            if (_cancelZoneRect != null)
            {
                _cancelZoneRect.anchorMin = _cancelZoneRect.anchorMax = new Vector2(1f, 1f);
                _cancelZoneRect.pivot = new Vector2(1f, 1f);
                _cancelZoneRect.anchoredPosition = new Vector2(-16f, -12f);
            }

            if (_tayaCornerBadge != null && _tayaCornerBadge.transform is RectTransform tayaRect)
            {
                tayaRect.anchorMin = tayaRect.anchorMax = corner;
                tayaRect.pivot = corner;
                tayaRect.anchoredPosition = new Vector2(sign * 24f, 34f);
            }
        }

        public void CollectActiveControlRects(System.Collections.Generic.List<RectTransform> dst)
        {
            if (dst == null) return;
            dst.Clear();
            if (Joystick != null)
            {
                var stickBase = Joystick.transform.Find("Base") as RectTransform;
                if (stickBase != null && stickBase.gameObject.activeInHierarchy) dst.Add(stickBase);
            }
            if (_leaveButtonRect != null && _leaveButtonRect.gameObject.activeInHierarchy) dst.Add(_leaveButtonRect);
            if (_rolePillRect != null && _rolePillRect.gameObject.activeInHierarchy) dst.Add(_rolePillRect);
            if (_statusHintPillRect != null && _statusHintPillRect.gameObject.activeInHierarchy) dst.Add(_statusHintPillRect);
            if (_headerRect != null && _headerRect.gameObject.activeInHierarchy) dst.Add(_headerRect);
            if (_timerPillRect != null && _timerPillRect.gameObject.activeInHierarchy) dst.Add(_timerPillRect);
            if (_brownoutBannerObj != null && _brownoutBannerObj.activeInHierarchy && _brownoutBannerObj.transform is RectTransform br) dst.Add(br);
            if (_primaryBtnRect != null && _primaryBtnRect.gameObject.activeInHierarchy) dst.Add(_primaryBtnRect);
            if (_secondaryBtnRect != null && _secondaryBtnRect.gameObject.activeInHierarchy) dst.Add(_secondaryBtnRect);
            if (_tayaCornerBadge != null && _tayaCornerBadge.activeInHierarchy) dst.Add(_tayaCornerBadge.transform as RectTransform);
        }

        // ---- Back / Pause Modal & Input Consumption Helpers --------------------------------------------------------------------

        /// <summary>
        /// Handles Android Back gesture or Pause/Leave tap: dismisses an open Leave prompt, exits from Match Over,
        /// or opens the Leave confirmation modal instead of abruptly dropping an active street match.
        /// </summary>
        public bool HandleBack()
        {
            if (_matchOverShown)
            {
                Haptics.LightTap();
                _onHome?.Invoke();
                return true;
            }

            if (_leaveConfirmShown)
            {
                Haptics.LightTap();
                SetLeaveConfirmOpen(false);
                return true;
            }

            Haptics.LightTap();
            CancelActiveTouches();
            SetLeaveConfirmOpen(true);
            return true;
        }

        public void ToggleLeaveConfirm()
        {
            if (_matchOverShown)
            {
                _onHome?.Invoke();
                return;
            }
            CancelActiveTouches();
            SetLeaveConfirmOpen(!_leaveConfirmShown);
        }

        public void SetLeaveConfirmOpen(bool open)
        {
            _leaveConfirmShown = open && !_matchOverShown;
            if (_leaveConfirmShown)
                CancelActiveTouches();
            if (_leaveConfirmRoot != null && _leaveConfirmRoot.activeSelf != _leaveConfirmShown)
                _leaveConfirmRoot.SetActive(_leaveConfirmShown);
        }

        public bool ConsumeSecondaryPress()
        {
            if (!Interactive || IsBlockingGameplayInput || _secondaryHandler == null)
            {
                if (_secondaryHandler != null) _secondaryHandler.ConsumeTap();
                return false;
            }
            return _secondaryHandler.ConsumeTap();
        }

        public bool ConsumeCancelAim() => Interactive && _primaryHandler != null && _primaryHandler.ConsumeCancel();

        public Vector2 TakeAimDragPixels()
        {
            if (!Interactive || IsBlockingGameplayInput || _primaryHandler == null)
            {
                if (_primaryHandler != null) _primaryHandler.TakeDragPixels();
                return Vector2.zero;
            }
            return _primaryHandler.TakeDragPixels();
        }

        public void CancelActiveTouches()
        {
            if (Joystick != null) Joystick.Cancel();
            if (Look != null) Look.Cancel();
            if (_primaryHandler != null) _primaryHandler.CancelSilently(requestSimCancel: true);
            if (_secondaryHandler != null) _secondaryHandler.CancelSilently(requestSimCancel: false);
        }

        // ---- Per-Frame Refresh --------------------------------------------------------------------------------------------------

        public void Refresh(StreetBridge bridge, float dt = 0f)
        {
            if (bridge == null) return;
            float stepDt = Mathf.Max(0f, dt);

            if (SettingsStore.Current.LeftHanded != _appliedLeftHanded
                || !Mathf.Approximately(SettingsStore.Current.ControlScale, _appliedControlScale))
            {
                ApplyResponsiveLayout();
            }

            RefreshHeader(bridge);
            RefreshRoleAndHint(bridge);
            RefreshBrownoutBanner(bridge, stepDt);
            RefreshActionButtons(bridge);
            RefreshMatchOver(bridge);
        }

        void RefreshHeader(StreetBridge bridge)
        {
            string modeUpper = (bridge.DisplayTitle ?? StreetBridge.DefaultTitleOf(bridge.Kind)).ToUpperInvariant();
            if (!string.Equals(_cachedModeTitle, modeUpper, StringComparison.Ordinal))
            {
                _cachedModeTitle = modeUpper;
                _modeTitleText.text = modeUpper;
            }

            if (bridge.IsFreeForAll)
            {
                int myPts = bridge.PlayerScore(bridge.LocalPlayerId);
                int topOther = 0;
                var kids = bridge.Kids;
                for (int i = 0; i < kids.Count; i++)
                {
                    if (kids[i].Id != bridge.LocalPlayerId && kids[i].Score > topOther)
                        topOther = kids[i].Score;
                }
                if (!_cachedFfaMode || myPts != _cachedScoreA || topOther != _cachedScoreB)
                {
                    _cachedFfaMode = true;
                    _cachedScoreA = myPts;
                    _cachedScoreB = topOther;
                    _scoreText.text = $"YOU {myPts}  •  TOP {topOther}";
                }
            }
            else
            {
                int myTeam = bridge.LocalPlayerTeam;
                int ownPts = bridge.TeamScore(myTeam);
                int enemyPts = bridge.TeamScore(1 - myTeam);
                if (_cachedFfaMode || ownPts != _cachedScoreA || enemyPts != _cachedScoreB)
                {
                    _cachedFfaMode = false;
                    _cachedScoreA = ownPts;
                    _cachedScoreB = enemyPts;
                    _scoreText.text = $"{ownPts} - {enemyPts}";
                }
            }

            int sec = Mathf.Max(0, Mathf.CeilToInt(bridge.TimeLeft));
            if (sec != _cachedClockSeconds)
            {
                _cachedClockSeconds = sec;
                _timerText.text = $"{sec / 60}:{sec % 60:00}";
                _timerText.color = sec <= 30 ? GoldText : Ui.Cream;
            }

            bool showStall = bridge.Kind == StreetGameKind.Patintero
                && bridge.PatinteroState.StallTotal > 0.1f
                && !bridge.IsIntro
                && !bridge.IsMatchOver;
            if (_stallBarRoot.activeSelf != showStall)
                _stallBarRoot.SetActive(showStall);
            if (showStall)
            {
                float ratio = Mathf.Clamp01(bridge.PatinteroState.StallLeft / Mathf.Max(0.1f, bridge.PatinteroState.StallTotal));
                _stallFillRect.anchorMax = new Vector2(ratio, 1f);
            }
        }

        void RefreshRoleAndHint(StreetBridge bridge)
        {
            StreetKidSnapshot? me = bridge.LocalKid;
            StreetRoleKind role = me.HasValue ? me.Value.Role : StreetRoleKind.Runner;
            bool colorblind = SettingsStore.Current.IsColorblind;

            string roleWord;
            Color pillColor;
            switch (role)
            {
                case StreetRoleKind.Taya:
                    roleWord = "TAYA";
                    pillColor = colorblind ? DangerPillCb : TayaPillBg;
                    break;
                case StreetRoleKind.Guard:
                    roleWord = "GUARD";
                    pillColor = colorblind ? DangerPillCb : GuardPillBg;
                    break;
                default:
                    // Both Patintero/LangitLupa Runners and TumbangPreso non-Taya attackers display crisp RUNNER pill.
                    roleWord = "RUNNER";
                    pillColor = colorblind ? RunnerPillCb : RunnerPillBg;
                    break;
            }

            if (!string.Equals(_cachedRoleWord, roleWord, StringComparison.Ordinal))
            {
                _cachedRoleWord = roleWord;
                _rolePillText.text = roleWord;
            }
            _rolePillBg.color = pillColor;

            string hint = ResolveTacticalHint(bridge, me);
            bool showHint = !string.IsNullOrEmpty(hint);
            if (_statusHintPillRect != null && _statusHintPillRect.gameObject.activeSelf != showHint)
                _statusHintPillRect.gameObject.SetActive(showHint);
            if (!string.Equals(_cachedHintLine, hint, StringComparison.Ordinal))
            {
                _cachedHintLine = hint;
                _statusHintText.text = hint;
                UpdateStatusHintLayout();
            }
        }

        void UpdateStatusHintLayout(float leaveHeightOverride = -1f)
        {
            if (_statusHintPillRect == null || _statusHintText == null) return;

            float leaveH = leaveHeightOverride > 0f
                ? leaveHeightOverride
                : (_leaveButtonRect != null && _leaveButtonRect.sizeDelta.y > 1f
                    ? _leaveButtonRect.sizeDelta.y
                    : ScreenFitMetrics.ClampTouchSize(new Vector2(104f, 48f), ScreenFitMetrics.MinTouchDp, Canvas).y);

            float safeWidth = Safe != null && Safe.rect.width > 120f
                ? Safe.rect.width
                : ScreenFitMetrics.SafeCanvasSize(Canvas).x;

            string hint = _statusHintText.text ?? "";
            _statusHintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextGenerationSettings genSettings = _statusHintText.GetGenerationSettings(new Vector2(2048f, 40f));
            genSettings.resizeTextForBestFit = false;
            genSettings.fontSize = 18;
            genSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
            genSettings.scaleFactor = 1f;
            float genWidth = _statusHintText.cachedTextGeneratorForLayout.GetPreferredWidth(hint, genSettings);
            float preferred = Mathf.Max(_statusHintText.preferredWidth, Mathf.Max(genWidth, hint.Length * 9.8f));

            float maxWidth = Mathf.Min(580f, Mathf.Max(240f, safeWidth - 32f));
            float pillWidth = Mathf.Clamp(preferred + 36f, 240f, maxWidth);
            _statusHintPillRect.sizeDelta = new Vector2(pillWidth, 36f);
            _statusHintText.horizontalOverflow = preferred + 34f > pillWidth
                ? HorizontalWrapMode.Wrap
                : HorizontalWrapMode.Overflow;

            float topRowBottom = 10f + leaveH + 6f;
            // On narrow 4:3 viewports where the expanded hint pill reaches horizontally past TimerPill's left edge,
            // step down just below TimerPill (-90 - 32 - 6 = -128) so zero HUD pills ever overlap.
            float timerLeftEdge = safeWidth * 0.5f - 72f;
            if (14f + pillWidth > timerLeftEdge && topRowBottom < 128f)
                topRowBottom = 128f;

            _statusHintPillRect.anchoredPosition = new Vector2(14f, -topRowBottom);
        }

        static string ResolveTacticalHint(StreetBridge bridge, StreetKidSnapshot? me)
        {
            if (bridge.IsIntro) return "GET READY!";
            if (bridge.IsResetting) return "ROUND RESETTING...";
            if (!me.HasValue) return "";

            StreetKidSnapshot kid = me.Value;
            if (bridge.Kind == StreetGameKind.Patintero)
            {
                if (kid.Jailed) return "JAILED ON SIDEWALK — WAIT FOR NEXT TURN";
                if (kid.Role == StreetRoleKind.Guard) return "HOLD YOUR CHALK LINE & TAG RUNNERS!";
                return kid.CrossedFarLine ? "NOW DASH BACK HOME TO SCORE!" : "CROSS EVERY GUARD LINE TO THE GOAL!";
            }

            if (bridge.Kind == StreetGameKind.TumbangPreso)
            {
                TumbangPresoSnapshot tp = bridge.TumbangPresoState;
                if (kid.Role == StreetRoleKind.Taya)
                    return tp.CanUp ? "GUARD THE CAN & TAG THROWERS!" : "PICK UP & STAND THE CAN IN THE CIRCLE!";
                if (!kid.HasSlipper)
                    return tp.CanUp ? "FETCH YOUR TSINELAS OR SIPA THE CAN!" : "GRAB YOUR TSINELAS & RUN BEHIND TOE-LINE!";
                return "HOLD TSINELAS TO CHARGE & KNOCK THE CAN!";
            }

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            if (kid.Role == StreetRoleKind.Taya)
                return ll.IsBrownout ? "BROWNOUT! HUNT RUNNERS ON THE STREET!" : "TAG RUNNERS ON THE STREET!";
            if (kid.SafeOnLangit)
                return "SAFE ON LANGIT — WATCH PLATFORM WEAR!";
            if (kid.OnLangit)
                return "SAFE TIME UP — STEP DOWN TO REFILL!";
            return "CLIMB A LANGIT OR TAP LUPA TO RAISE EARTH!";
        }

        void RefreshBrownoutBanner(StreetBridge bridge, float dt)
        {
            if (bridge.Kind != StreetGameKind.LangitLupa)
            {
                if (_brownoutBannerObj.activeSelf) _brownoutBannerObj.SetActive(false);
                return;
            }

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            bool dark = ll.IsBrownout && !bridge.IsMatchOver;
            if (_wasBrownout && !dark)
                _lightsOnFlashLeft = 1.4f;
            _wasBrownout = dark;

            if (_lightsOnFlashLeft > 0f)
                _lightsOnFlashLeft = Mathf.Max(0f, _lightsOnFlashLeft - dt);

            bool showBanner = dark || _lightsOnFlashLeft > 0f;
            if (_brownoutBannerObj.activeSelf != showBanner)
                _brownoutBannerObj.SetActive(showBanner);

            if (!showBanner) return;

            if (dark)
            {
                int bSec = Mathf.Max(1, Mathf.CeilToInt(ll.BrownoutLeft));
                if (bSec != _cachedBrownoutSeconds)
                {
                    _cachedBrownoutSeconds = bSec;
                    _brownoutBannerText.text = $"BROWNOUT! {Mathf.RoundToInt(ll.BrownoutSight)}M SIGHT ({bSec}S)";
                }
                _brownoutBannerBg.color = BrownoutAlertBg;
            }
            else if (_cachedBrownoutSeconds != -2)
            {
                _cachedBrownoutSeconds = -2;
                _brownoutBannerText.text = "LIGHTS ON!";
                _brownoutBannerBg.color = new Color(0.16f, 0.52f, 0.30f, 0.90f);
            }
        }

        void RefreshActionButtons(StreetBridge bridge)
        {
            bool controlsLive = !bridge.IsMatchOver;
            StreetActionInfo info = bridge.LocalActions;

            // Primary action (TSINELAS / THROW)
            bool showPrimary = controlsLive && info.PrimaryVisible;
            if (_primaryBtnRect.gameObject.activeSelf != showPrimary)
                _primaryBtnRect.gameObject.SetActive(showPrimary);

            if (showPrimary)
            {
                string label = string.IsNullOrEmpty(info.PrimaryLabel) ? "THROW" : info.PrimaryLabel;
                if (!string.Equals(_cachedPrimaryLabel, label, StringComparison.Ordinal))
                {
                    _cachedPrimaryLabel = label;
                    _primaryLabelText.text = label;
                }
                _primaryHandler?.Configure(info.PrimaryHoldToCharge, info.PrimaryEnabled, _cancelZoneRect);

                float fill = Mathf.Clamp01(info.PrimaryReady01);
                _primaryRingImg.fillAmount = fill;
                _primaryRingImg.color = PrimaryHeld ? ChargingRingColor : ReadyRingColor;
                _primaryFaceImg.color = info.PrimaryEnabled ? ThrowFace : DisabledFace;
            }
            else
            {
                _primaryHandler?.Configure(false, false, _cancelZoneRect);
            }

            // Secondary action (SIPA / DIVE / LUPA)
            bool showSecondary = controlsLive && info.SecondaryVisible;
            if (_secondaryBtnRect.gameObject.activeSelf != showSecondary)
                _secondaryBtnRect.gameObject.SetActive(showSecondary);

            if (showSecondary)
            {
                string label = string.IsNullOrEmpty(info.SecondaryLabel) ? "ACT" : info.SecondaryLabel;
                if (!string.Equals(_cachedSecondaryLabel, label, StringComparison.Ordinal))
                {
                    _cachedSecondaryLabel = label;
                    _secondaryLabelText.text = label;
                }
                _secondaryHandler?.Configure(false, info.SecondaryEnabled, null);

                // If Primary is hidden (Patintero DIVE / Langit Lupa LUPA), anchor Secondary into the main corner spot!
                bool leftHanded = SettingsStore.Current.LeftHanded;
                float sign = leftHanded ? 1f : -1f;
                float mainSize = _primaryBtnRect.sizeDelta.x;
                float secSize = _secondaryBtnRect.sizeDelta.x;
                float minGap = Mathf.Max(20f, ScreenFitMetrics.DpToCanvasPx(10f, Canvas));
                float edgePrimary = ScreenFitMetrics.ClampThumbSpreadOffset(36f + mainSize * 0.5f, 160f, Canvas);
                if (!showPrimary)
                {
                    _secondaryBtnRect.anchoredPosition = new Vector2(sign * edgePrimary, 36f + mainSize * 0.5f);
                }
                else
                {
                    float secOffset = edgePrimary + mainSize * 0.5f + secSize * 0.5f + minGap;
                    _secondaryBtnRect.anchoredPosition = new Vector2(sign * secOffset, 30f + secSize * 0.5f);
                }

                float fill = Mathf.Clamp01(info.SecondaryReady01);
                _secondaryRingImg.fillAmount = fill;
                _secondaryRingImg.color = fill >= 0.99f ? ReadyRingColor : Color.white;
                _secondaryFaceImg.color = info.SecondaryEnabled ? FaceColorFor(label) : DisabledFace;
            }
            else
            {
                _secondaryHandler?.Configure(false, false, null);
            }

            // Cancel zone visibility when holding Throw
            bool showCancel = showPrimary && PrimaryHeld;
            if (_cancelZoneRect.gameObject.activeSelf != showCancel)
                _cancelZoneRect.gameObject.SetActive(showCancel);
            if (showCancel)
                _cancelZoneBg.color = IsCancelHovered ? CancelHoverBg : CancelIdleBg;

            // Taya corner prompt when neither button is shown
            bool showTayaChip = controlsLive && !showPrimary && !showSecondary;
            if (_tayaCornerBadge.activeSelf != showTayaChip)
                _tayaCornerBadge.SetActive(showTayaChip);
        }

        static Color FaceColorFor(string label)
        {
            if (string.Equals(label, "SIPA", StringComparison.OrdinalIgnoreCase)) return SipaFace;
            if (string.Equals(label, "LUPA", StringComparison.OrdinalIgnoreCase)) return LupaFace;
            return DiveFace;
        }

        void RefreshMatchOver(StreetBridge bridge)
        {
            if (!bridge.IsMatchOver)
            {
                if (_matchOverShown)
                {
                    _matchOverShown = false;
                    _matchOverRoot.SetActive(false);
                }
                return;
            }

            if (_matchOverShown) return;
            _matchOverShown = true;
            SetLeaveConfirmOpen(false);
            CancelActiveTouches();
            _matchOverRoot.SetActive(true);

            StreetMatchResult result = bridge.GetLocalResult();
            _matchOverHeadline.text = result.Won ? "PANALO! VICTORY!" : "TAPOS NA! MATCH OVER";
            _matchOverHeadline.color = result.Won ? GoldText : Ui.Cream;
            _matchOverSubhead.text = $"{bridge.DisplayTitle}  •  YOU {result.MyPoints}  —  OPP {result.BestOpponentPoints}";

            if (Interactive && !_lpRecorded)
            {
                _lpRecorded = true;
                StreetKidSnapshot? me = bridge.LocalKid;
                string charId = me.HasValue && !string.IsNullOrEmpty(me.Value.CharacterId)
                    ? me.Value.CharacterId
                    : AppSession.Current.SelectedCharacter;
                RankOutcomeInfo lp = AppSession.Current.RecordMatchResult(
                    bridge.ModeId,
                    charId,
                    result.Won,
                    result.MyPoints,
                    result.BestOpponentPoints);
                string sign = lp.Delta >= 0 ? "+" : "";
                _matchOverLpText.text = $"{lp.TierName}  •  {lp.NewLp} LP ({sign}{lp.Delta})";
            }
            else
            {
                RankTierInfo tier = AppSession.Current.CurrentTier;
                _matchOverLpText.text = $"{tier.Name}  •  {AppSession.Current.Lp} LP";
            }
        }

        public void Dispose()
        {
            if (SafeAreaComponent != null)
                SafeAreaComponent.Changed -= OnSafeAreaChanged;
            SettingsStore.Current.Changed -= OnSettingsChanged;
            ScreenFitMetrics.SimulatedViewportChanged -= ApplyResponsiveLayout;
            if (Canvas != null)
                KalyeahResources.Kill(Canvas.gameObject);
        }
    }
}
