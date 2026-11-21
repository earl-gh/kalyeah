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
        static readonly Color BrownoutShadeTint = new Color(0.06f, 0.09f, 0.19f, 1f);
        static readonly Color TumbangResultColor = new Color(1.00f, 0.30f, 0.26f, 1f);
        static readonly Color TumbangSlotIdleBorder = new Color(0.20f, 0.27f, 0.42f, 0.88f);
        static readonly Color GoldText = new Color(1.00f, 0.84f, 0.28f, 1f);

        const float BrownoutFadeSeconds = 0.40f;
        const float BrownoutFeatherShare = 0.30f;
        const float BrownoutShadeMaxAlpha = 0.56f;

        const int TumbangRollLaps = 2;
        const float TumbangRollCurve = 1.6f;
        const float TumbangRollFinishShare = 0.78f;
        const float TumbangRollTotalSeconds = 2.20f;

        static Sprite s_brownoutHoleSprite;

        public readonly Canvas Canvas;
        public readonly RectTransform Safe;
        public readonly SafeArea SafeAreaComponent;
        public readonly VirtualJoystick Joystick;
        public readonly LookPad Look;
        public readonly bool Interactive;

        readonly Camera _defaultCamera;
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

        // Brownout radial screen-space spotlight shade (under Safe) + alert banner
        readonly RectTransform _brownoutShadeRoot;
        readonly RectTransform _brownoutHoleRect;
        readonly Image _brownoutHoleImg;
        readonly RectTransform[] _brownoutEdgeRects;
        readonly Image[] _brownoutEdgeImgs;
        float _brownoutShadeStrength;

        readonly GameObject _brownoutBannerObj;
        readonly RectTransform _brownoutBannerRect;
        readonly Image _brownoutBannerBg;
        readonly Text _brownoutBannerText;
        float _lightsOnFlashLeft;
        bool _wasBrownout;

        // Interactive-only Tumbang Preso opening roulette ("Mataya-taya!")
        readonly RectTransform _tumbangRollRect;
        readonly GameObject _tumbangRollRoot;
        readonly Image[] _tumbangRollSlotBorders;
        readonly RectTransform[] _tumbangRollSlotRects;
        readonly int[] _tumbangRollSlotIds;
        readonly string[] _tumbangRollSpinNames;
        readonly string[] _tumbangRollLandedTexts;
        readonly Text _tumbangRollWhoText;
        float _tumbangRollElapsed;
        int _tumbangRollTargetKidId = int.MinValue;
        int _tumbangRollTargetSlot;
        int _tumbangRollTotalSteps;
        int _tumbangRollStepIndex = -1;
        int _tumbangRollActiveSlot = -1;
        bool _tumbangRollLandedFired;
        bool _tumbangRollDismissed;

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

        // Leave confirmation & Match over modals (full-bleed scrim on Canvas + SafeArea card container)
        readonly GameObject _leaveConfirmRoot;
        readonly SafeArea _leaveModalSafe;
        readonly RectTransform _leaveCardRect;
        readonly RectTransform _leaveResumeBtnRect;
        readonly RectTransform _leaveConfirmBtnRect;

        readonly GameObject _matchOverRoot;
        readonly SafeArea _matchOverModalSafe;
        readonly RectTransform _matchOverCardRect;
        readonly RectTransform _matchOverPlayAgainBtnRect;
        readonly RectTransform _matchOverHomeBtnRect;
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
        public RectTransform CancelZoneRect => _cancelZoneRect;
        public RectTransform TumbangRollRect => _tumbangRollRect;
        public RectTransform PrimaryButtonRect => _primaryBtnRect;
        public RectTransform SecondaryButtonRect => _secondaryBtnRect;

        public event Action TumbangRollStepped;
        public event Action TumbangRollLanded;

        public bool? ForceBrownoutPreview { get; set; }
        public RectTransform BrownoutShadeRect => _brownoutShadeRoot;
        public bool IsBrownoutShadeVisible => _brownoutShadeRoot != null && _brownoutShadeRoot.gameObject.activeSelf;
        public float BrownoutShadeStrength => _brownoutShadeStrength;
        public bool IsTumbangRollVisible => _tumbangRollRoot != null && _tumbangRollRoot.activeSelf;

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
            _defaultCamera = camera;
            _onPlayAgain = onPlayAgain;
            _onHome = onHome;

            Canvas = Ui.MakeCanvas("StreetHud", parent, camera);
            if (!interactive)
            {
                var raycaster = Canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }

            // Place BrownoutShade at siblingIndex 0 directly under Canvas BEFORE SafeArea so all HUD pills,
            // labels, joystick, and action buttons inside Safe stay 100% bright and readable above the shade.
            _brownoutShadeRoot = Ui.Fill("BrownoutShade", Canvas.transform);
            BuildBrownoutShade(
                _brownoutShadeRoot,
                out _brownoutHoleRect,
                out _brownoutHoleImg,
                out _brownoutEdgeRects,
                out _brownoutEdgeImgs);
            _brownoutShadeRoot.gameObject.SetActive(false);

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
            Sprite modalCardSprite = KomiksUi.BorderedSprite(KomiksUi.RadiusL, 3, NavyPillBg, NavyPillBorder, topGloss: true);
            Sprite roundedWhiteSprite = KomiksUi.RoundedSprite(KomiksUi.RadiusM);

            // 1. Top-Center Score Header + 4 px Spaced Dark Navy Timer Pill (compact so TumbangRoll fits outside sightbox)
            _headerRect = Ui.Panel("Header", Safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(316f, 56f));
            Image headerPlate = MakeSlicedPill("Plate", _headerRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(headerPlate.rectTransform);

            _modeTitleText = Ui.MakeText("ModeTitle", _headerRect, "", 16, GoldText, TextAnchor.UpperCenter, displayFont);
            _modeTitleText.raycastTarget = false;
            _modeTitleText.resizeTextForBestFit = true;
            _modeTitleText.resizeTextMinSize = 12;
            _modeTitleText.resizeTextMaxSize = 16;
            RectTransform modeTitleRt = _modeTitleText.rectTransform;
            modeTitleRt.anchorMin = Vector2.zero;
            modeTitleRt.anchorMax = Vector2.one;
            modeTitleRt.offsetMin = new Vector2(12f, 30f);
            modeTitleRt.offsetMax = new Vector2(-12f, -4f);

            _scoreText = Ui.MakeText("Score", _headerRect, "0 - 0", 25, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
            _scoreText.raycastTarget = false;
            _scoreText.resizeTextForBestFit = true;
            _scoreText.resizeTextMinSize = 16;
            _scoreText.resizeTextMaxSize = 25;
            RectTransform scoreRt = _scoreText.rectTransform;
            scoreRt.anchorMin = Vector2.zero;
            scoreRt.anchorMax = Vector2.one;
            scoreRt.offsetMin = new Vector2(12f, 6f);
            scoreRt.offsetMax = new Vector2(-12f, -22f);

            // Patintero stall bar tucked along bottom inside edge of header
            RectTransform stallTrack = Ui.Panel("StallTrack", _headerRect, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(0f, 4f));
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

            // Timer pill spaced 4 px below _headerRect (-6 - 56 - 4 = -66)
            _timerPillRect = Ui.Panel("TimerPill", Safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(128f, 28f));
            Image timerPillBg = MakeSlicedPill("TimerBg", _timerPillRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(timerPillBg.rectTransform);
            _timerText = Ui.MakeText("Timer", _timerPillRect, "5:00", 19, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
            _timerText.fontStyle = FontStyle.Bold;
            _timerText.raycastTarget = false;
            RectTransform timerTextRt = _timerText.rectTransform;
            timerTextRt.anchorMin = Vector2.zero;
            timerTextRt.anchorMax = Vector2.one;
            timerTextRt.offsetMin = new Vector2(8f, 2f);
            timerTextRt.offsetMax = new Vector2(-8f, -2f);

            // 2. Top-Left "|| PAUSE" Button (>= 48 dp) + Role Pill + Padded Dark Navy Tactical Hint Pill (>= 8 dp gap below)
            _leaveButtonRect = BuildPillButton(
                Safe,
                "PauseButton",
                "|| PAUSE",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -8f),
                new Vector2(126f, 96f),
                navyPillSprite,
                Color.white,
                displayFont,
                20,
                interactive,
                () => ToggleLeaveConfirm());

            _rolePillRect = Ui.Panel("RolePill", Safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -8f), new Vector2(134f, 52f));
            _rolePillBg = MakeSlicedPill("RoleBg", _rolePillRect, roundedWhiteSprite, RunnerPillBg, raycast: false);
            StretchFull(_rolePillBg.rectTransform);
            _rolePillText = Ui.MakeText("RoleLabel", _rolePillRect, "RUNNER", 21, Color.white, TextAnchor.MiddleCenter, displayFont);
            _rolePillText.raycastTarget = false;
            _rolePillText.resizeTextForBestFit = true;
            _rolePillText.resizeTextMinSize = 14;
            _rolePillText.resizeTextMaxSize = 21;
            RectTransform roleLabelRt = _rolePillText.rectTransform;
            roleLabelRt.anchorMin = Vector2.zero;
            roleLabelRt.anchorMax = Vector2.one;
            roleLabelRt.offsetMin = new Vector2(10f, 4f);
            roleLabelRt.offsetMax = new Vector2(-10f, -4f);

            _statusHintPillRect = Ui.Panel("StatusHintPill", Safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -120f), new Vector2(360f, 38f));
            Image hintBg = MakeSlicedPill("HintBg", _statusHintPillRect, navyPillSprite, Color.white, raycast: false);
            StretchFull(hintBg.rectTransform);
            _statusHintText = Ui.MakeText("StatusHint", _statusHintPillRect, "", 17, Ui.Cream, TextAnchor.MiddleLeft);
            _statusHintText.fontStyle = FontStyle.Bold;
            _statusHintText.raycastTarget = false;
            _statusHintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _statusHintText.verticalOverflow = VerticalWrapMode.Truncate;
            _statusHintText.resizeTextForBestFit = true;
            _statusHintText.resizeTextMinSize = 13;
            _statusHintText.resizeTextMaxSize = 17;
            RectTransform hintTextRect = _statusHintText.rectTransform;
            hintTextRect.anchorMin = Vector2.zero;
            hintTextRect.anchorMax = Vector2.one;
            hintTextRect.offsetMin = new Vector2(18f, 4f);
            hintTextRect.offsetMax = new Vector2(-18f, -4f);

            // 3. Brownout Alert Pill (top-right corner during Langit Lupa, clear of Header, CancelZone, and center sightbox)
            _brownoutBannerRect = Ui.Panel("BrownoutAlert", Safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -8f), new Vector2(276f, 44f));
            _brownoutBannerBg = MakeSlicedPill("AlertBg", _brownoutBannerRect, roundedWhiteSprite, BrownoutAlertBg, raycast: false);
            StretchFull(_brownoutBannerBg.rectTransform);
            _brownoutBannerText = Ui.MakeText("AlertText", _brownoutBannerRect, "BROWNOUT! 5M SIGHT", 17, GoldText, TextAnchor.MiddleCenter, displayFont);
            _brownoutBannerText.raycastTarget = false;
            _brownoutBannerText.resizeTextForBestFit = true;
            _brownoutBannerText.resizeTextMinSize = 13;
            _brownoutBannerText.resizeTextMaxSize = 17;
            RectTransform brownoutTextRt = _brownoutBannerText.rectTransform;
            brownoutTextRt.anchorMin = Vector2.zero;
            brownoutTextRt.anchorMax = Vector2.one;
            brownoutTextRt.offsetMin = new Vector2(14f, 4f);
            brownoutTextRt.offsetMax = new Vector2(-14f, -4f);
            _brownoutBannerObj = _brownoutBannerRect.gameObject;
            _brownoutBannerObj.SetActive(false);

            // 4. Cancel Zone (>= 48 dp, mirrored with LeftHanded, spaced >= 8 dp clear of BrownoutAlert & StatusHintPill)
            _cancelZoneRect = Ui.Panel("CancelZone", Safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -68f), new Vector2(228f, 96f));
            _cancelZoneBg = MakeSlicedPill("CancelBg", _cancelZoneRect, roundedWhiteSprite, CancelIdleBg, raycast: false);
            StretchFull(_cancelZoneBg.rectTransform);
            Text cancelText = Ui.MakeText("CancelLabel", _cancelZoneRect, "RELEASE HERE TO CANCEL", 18, Color.white, TextAnchor.MiddleCenter, displayFont);
            cancelText.fontStyle = FontStyle.Bold;
            cancelText.raycastTarget = false;
            cancelText.resizeTextForBestFit = true;
            cancelText.resizeTextMinSize = 14;
            cancelText.resizeTextMaxSize = 18;
            RectTransform cancelTextRt = cancelText.rectTransform;
            cancelTextRt.anchorMin = Vector2.zero;
            cancelTextRt.anchorMax = Vector2.one;
            cancelTextRt.offsetMin = new Vector2(14f, 6f);
            cancelTextRt.offsetMax = new Vector2(-14f, -6f);
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
            _tayaCornerText.raycastTarget = false;
            RectTransform tayaTextRt = _tayaCornerText.rectTransform;
            tayaTextRt.anchorMin = Vector2.zero;
            tayaTextRt.anchorMax = Vector2.one;
            tayaTextRt.offsetMin = new Vector2(12f, 4f);
            tayaTextRt.offsetMax = new Vector2(-12f, -4f);
            _tayaCornerBadge = tayaChip.gameObject;
            _tayaCornerBadge.SetActive(false);

            // 5b. Interactive-only Tumbang Preso opening roulette ("Mataya-taya!"), docked below TimerPill outside sightbox
            if (interactive && bridge.Kind == StreetGameKind.TumbangPreso && bridge.Kids != null && bridge.Kids.Count > 0)
            {
                _tumbangRollRect = BuildTumbangRollBanner(
                    Safe,
                    bridge,
                    displayFont,
                    navyPillSprite,
                    roundedWhiteSprite,
                    out _tumbangRollSlotBorders,
                    out _tumbangRollSlotRects,
                    out _tumbangRollSlotIds,
                    out _tumbangRollSpinNames,
                    out _tumbangRollLandedTexts,
                    out _tumbangRollWhoText);
                _tumbangRollRoot = _tumbangRollRect.gameObject;
                SyncTumbangRollTarget(bridge.TumbangPresoState.TayaId);
                if (!bridge.IsIntro)
                {
                    _tumbangRollDismissed = true;
                    _tumbangRollRoot.SetActive(false);
                }
            }

            // 6. Full-bleed Leave Confirmation & Match Over Modals directly under Canvas (full-bleed scrim + SafeArea card)
            _leaveConfirmRoot = BuildLeaveConfirmModal(
                Canvas.transform,
                displayFont,
                modalCardSprite,
                roundedWhiteSprite,
                out _leaveModalSafe,
                out _leaveCardRect,
                out _leaveResumeBtnRect,
                out _leaveConfirmBtnRect);
            _leaveConfirmRoot.SetActive(false);

            _matchOverRoot = BuildMatchOverModal(
                Canvas.transform,
                displayFont,
                modalCardSprite,
                roundedWhiteSprite,
                out _matchOverModalSafe,
                out _matchOverCardRect,
                out _matchOverPlayAgainBtnRect,
                out _matchOverHomeBtnRect,
                out _matchOverHeadline,
                out _matchOverSubhead,
                out _matchOverLpText);
            _matchOverRoot.SetActive(false);

            SafeAreaComponent.Changed += OnSafeAreaChanged;
            SettingsStore.Current.Changed += OnSettingsChanged;
            ScreenFitMetrics.SimulatedViewportChanged += ApplyResponsiveLayout;

            ApplyResponsiveLayout();
            Refresh(bridge, 0f, camera);
        }

        static void BuildBrownoutShade(
            RectTransform shadeRoot,
            out RectTransform holeRect,
            out Image holeImg,
            out RectTransform[] edgeRects,
            out Image[] edgeImgs)
        {
            holeImg = Ui.MakeImage("SpotlightHole", shadeRoot, Color.clear, round: false, raycast: false);
            holeImg.sprite = GetBrownoutHoleSprite();
            holeImg.type = Image.Type.Simple;
            holeRect = holeImg.rectTransform;
            StretchFull(holeRect);

            string[] edgeNames = { "TopFill", "BottomFill", "LeftFill", "RightFill" };
            edgeRects = new RectTransform[4];
            edgeImgs = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Image fill = Ui.MakeImage(edgeNames[i], shadeRoot, Color.clear, round: false, raycast: false);
                edgeImgs[i] = fill;
                edgeRects[i] = fill.rectTransform;
                StretchFull(fill.rectTransform);
            }
        }

        static Sprite GetBrownoutHoleSprite()
        {
            if (s_brownoutHoleSprite != null)
                return s_brownoutHoleSprite;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "KalyeahBrownoutSpotlight",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            const float outerR = 0.98f;
            float innerR = outerR * (1f - BrownoutFeatherShare);
            float invFeather = 1f / Mathf.Max(0.001f, outerR - innerR);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    byte a;
                    if (r <= innerR)
                    {
                        a = 0;
                    }
                    else if (r >= outerR)
                    {
                        a = 255;
                    }
                    else
                    {
                        float t = Mathf.Clamp01((r - innerR) * invFeather);
                        float smooth = t * t * (3f - 2f * t);
                        a = (byte)Mathf.RoundToInt(smooth * 255f);
                    }
                    pixels[row + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            s_brownoutHoleSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            s_brownoutHoleSprite.name = "KalyeahBrownoutSpotlightSprite";
            s_brownoutHoleSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_brownoutHoleSprite;
        }

        static RectTransform BuildTumbangRollBanner(
            Transform parent,
            StreetBridge bridge,
            Font displayFont,
            Sprite navyPillSprite,
            Sprite roundedWhiteSprite,
            out Image[] slotBorders,
            out RectTransform[] slotRects,
            out int[] slotIds,
            out string[] spinNames,
            out string[] landedTexts,
            out Text whoText)
        {
            RectTransform banner = Ui.Panel(
                "TumbangRoll",
                parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -98f),
                new Vector2(640f, 74f));

            Image bg = MakeSlicedPill("RollBg", banner, navyPillSprite, Color.white, raycast: false);
            StretchFull(bg.rectTransform);

            // Left status column ("MATAYA-TAYA!" + live roulette / landed result)
            Text title = Ui.MakeText("Title", banner, "MATAYA-TAYA!", 18, GoldText, TextAnchor.UpperLeft, displayFont);
            title.raycastTarget = false;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = 18;
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(16f, 2f);
            titleRt.offsetMax = new Vector2(180f, -8f);

            whoText = Ui.MakeText("WhoLabel", banner, "", 18, Ui.Cream, TextAnchor.LowerLeft, displayFont);
            whoText.fontStyle = FontStyle.Bold;
            whoText.raycastTarget = false;
            whoText.resizeTextForBestFit = true;
            whoText.resizeTextMinSize = 13;
            whoText.resizeTextMaxSize = 18;
            RectTransform whoRt = whoText.rectTransform;
            whoRt.anchorMin = new Vector2(0f, 0f);
            whoRt.anchorMax = new Vector2(0f, 0.52f);
            whoRt.pivot = new Vector2(0f, 0f);
            whoRt.offsetMin = new Vector2(16f, 8f);
            whoRt.offsetMax = new Vector2(180f, -2f);

            var kids = bridge.Kids;
            int count = kids.Count;
            slotBorders = new Image[count];
            slotRects = new RectTransform[count];
            slotIds = new int[count];
            spinNames = new string[count];
            landedTexts = new string[count];

            for (int i = 0; i < count; i++)
            {
                StreetKidSnapshot kid = kids[i];
                slotIds[i] = kid.Id;
                bool isLocal = kid.Id == bridge.LocalPlayerId;
                string upperName = FormatKidShortName(kid.CharacterId, kid.Id);
                string cardCaption = isLocal ? "YOU" : upperName;
                spinNames[i] = cardCaption;
                landedTexts[i] = isLocal ? "YOU'RE IT!" : $"{upperName} IS IT!";

                RectTransform slot = Ui.Panel(
                    $"RollSlot_{i}",
                    banner,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(56f, 60f));
                slotRects[i] = slot;

                Image border = MakeSlicedPill("Border", slot, roundedWhiteSprite, TumbangSlotIdleBorder, raycast: false);
                StretchFull(border.rectTransform);
                slotBorders[i] = border;

                Image inner = MakeSlicedPill("Inner", slot, roundedWhiteSprite, new Color(0.06f, 0.10f, 0.21f, 0.96f), raycast: false);
                inner.rectTransform.anchorMin = Vector2.zero;
                inner.rectTransform.anchorMax = Vector2.one;
                inner.rectTransform.offsetMin = new Vector2(2f, 2f);
                inner.rectTransform.offsetMax = new Vector2(-2f, -2f);

                Texture2D portraitTex = !string.IsNullOrEmpty(kid.CharacterId)
                    ? KalyeahResources.Texture("portraits/" + kid.CharacterId)
                    : null;
                if (portraitTex != null)
                {
                    var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
                    portraitGo.transform.SetParent(slot, false);
                    var raw = portraitGo.GetComponent<RawImage>();
                    raw.texture = portraitTex;
                    raw.uvRect = new Rect(0f, 0.25f, 1f, 0.75f);
                    raw.raycastTarget = false;
                    RectTransform prt = raw.rectTransform;
                    prt.anchorMin = Vector2.zero;
                    prt.anchorMax = Vector2.one;
                    prt.offsetMin = new Vector2(4f, 15f);
                    prt.offsetMax = new Vector2(-4f, -4f);
                }
                else
                {
                    string initial = upperName.Length > 0 ? upperName.Substring(0, 1) : "?";
                    Text badge = Ui.MakeText("Initial", slot, initial, 20, GoldText, TextAnchor.MiddleCenter, displayFont);
                    badge.raycastTarget = false;
                    badge.resizeTextForBestFit = true;
                    badge.resizeTextMinSize = 12;
                    badge.resizeTextMaxSize = 20;
                    RectTransform brt = badge.rectTransform;
                    brt.anchorMin = Vector2.zero;
                    brt.anchorMax = Vector2.one;
                    brt.offsetMin = new Vector2(3f, 15f);
                    brt.offsetMax = new Vector2(-3f, -3f);
                }

                Text cap = Ui.MakeText("Name", slot, cardCaption, 11, isLocal ? GoldText : Ui.Cream, TextAnchor.LowerCenter, displayFont);
                cap.raycastTarget = false;
                cap.resizeTextForBestFit = true;
                cap.resizeTextMinSize = 9;
                cap.resizeTextMaxSize = 11;
                RectTransform crt = cap.rectTransform;
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(1f, 0f);
                crt.pivot = new Vector2(0.5f, 0f);
                crt.anchoredPosition = new Vector2(0f, 2f);
                crt.sizeDelta = new Vector2(-4f, 13f);
            }

            LayoutTumbangRollSlots(slotRects, 640f, 74f);
            return banner;
        }

        static void LayoutTumbangRollSlots(RectTransform[] slotRects, float bannerW, float bannerH)
        {
            if (slotRects == null || slotRects.Length == 0) return;

            int count = slotRects.Length;
            const float leftReserved = 186f;
            const float rightPad = 14f;
            float availW = Mathf.Max(120f, bannerW - leftReserved - rightPad);
            float gap = count >= 8 ? 4f : 6f;
            float slotW = Mathf.Clamp((availW - Mathf.Max(0, count - 1) * gap) / count, 26f, 62f);
            float slotH = Mathf.Max(48f, bannerH - 12f);
            float totalRowW = count * slotW + Mathf.Max(0, count - 1) * gap;
            float startX = leftReserved + (availW - totalRowW) * 0.5f + slotW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                RectTransform slot = slotRects[i];
                if (slot == null) continue;
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(slotW, slotH);
                slot.anchoredPosition = new Vector2(startX + i * (slotW + gap), 0f);
            }
        }

        static string FormatKidShortName(string characterId, int fallbackId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return $"KID {fallbackId + 1}";
            string trimmed = characterId.Trim();
            return trimmed.Length <= 8 ? trimmed.ToUpperInvariant() : trimmed.Substring(0, 8).ToUpperInvariant();
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
            txt.raycastTarget = false;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 14;
            txt.resizeTextMaxSize = fontSize;
            RectTransform txtRt = txt.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(12f, 6f);
            txtRt.offsetMax = new Vector2(-12f, -6f);
            return rect;
        }

        GameObject BuildLeaveConfirmModal(
            Transform canvasRoot,
            Font displayFont,
            Sprite cardSprite,
            Sprite btnSprite,
            out SafeArea modalSafe,
            out RectTransform cardRect,
            out RectTransform resumeBtnRect,
            out RectTransform confirmBtnRect)
        {
            RectTransform overlay = Ui.Fill("LeaveConfirmOverlay", canvasRoot);
            Image dimmer = Ui.MakeImage("Dimmer", overlay, new Color(0.03f, 0.05f, 0.12f, 0.82f), raycast: Interactive);
            StretchFull(dimmer.rectTransform);

            RectTransform safeFrame = Ui.Fill("ModalSafe", overlay);
            modalSafe = safeFrame.gameObject.AddComponent<SafeArea>();
            modalSafe.Apply();

            cardRect = Ui.Panel("LeaveCard", safeFrame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 304f));
            Image cardBg = MakeSlicedPill("CardPlate", cardRect, cardSprite, Color.white, raycast: Interactive);
            StretchFull(cardBg.rectTransform);

            Text title = Ui.MakeText("Headline", cardRect, "LEAVE STREET MATCH?", 32, GoldText, TextAnchor.MiddleCenter, displayFont);
            title.raycastTarget = false;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 20;
            title.resizeTextMaxSize = 32;
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -22f);
            titleRt.sizeDelta = new Vector2(-44f, 48f);

            Text body = Ui.MakeText("Body", cardRect, "Return to the Barangay Home menu?", 21, Ui.Cream, TextAnchor.MiddleCenter);
            body.raycastTarget = false;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 16;
            body.resizeTextMaxSize = 21;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -80f);
            bodyRt.sizeDelta = new Vector2(-48f, 68f);

            resumeBtnRect = BuildPillButton(
                cardRect,
                "ResumeButton",
                "RESUME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-130f, 20f),
                new Vector2(240f, 96f),
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

            confirmBtnRect = BuildPillButton(
                cardRect,
                "ConfirmLeaveButton",
                "LEAVE TO HOME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(130f, 20f),
                new Vector2(240f, 96f),
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
            Transform canvasRoot,
            Font displayFont,
            Sprite cardSprite,
            Sprite btnSprite,
            out SafeArea modalSafe,
            out RectTransform cardRect,
            out RectTransform playAgainBtnRect,
            out RectTransform homeBtnRect,
            out Text headline,
            out Text subhead,
            out Text lpText)
        {
            RectTransform overlay = Ui.Fill("MatchOverOverlay", canvasRoot);
            Image dimmer = Ui.MakeImage("Dimmer", overlay, new Color(0.03f, 0.05f, 0.12f, 0.82f), raycast: Interactive);
            StretchFull(dimmer.rectTransform);

            RectTransform safeFrame = Ui.Fill("ModalSafe", overlay);
            modalSafe = safeFrame.gameObject.AddComponent<SafeArea>();
            modalSafe.Apply();

            cardRect = Ui.Panel("SummaryCard", safeFrame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(564f, 344f));
            Image cardBg = MakeSlicedPill("CardPlate", cardRect, cardSprite, Color.white, raycast: Interactive);
            StretchFull(cardBg.rectTransform);

            headline = Ui.MakeText("Headline", cardRect, "PANALO!", 38, GoldText, TextAnchor.MiddleCenter, displayFont);
            headline.raycastTarget = false;
            headline.resizeTextForBestFit = true;
            headline.resizeTextMinSize = 20;
            headline.resizeTextMaxSize = 38;
            RectTransform headRt = headline.rectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(1f, 1f);
            headRt.pivot = new Vector2(0.5f, 1f);
            headRt.anchoredPosition = new Vector2(0f, -22f);
            headRt.sizeDelta = new Vector2(-44f, 54f);

            subhead = Ui.MakeText("Subhead", cardRect, "Score 0 - 0", 24, Ui.Cream, TextAnchor.MiddleCenter);
            subhead.raycastTarget = false;
            subhead.resizeTextForBestFit = true;
            subhead.resizeTextMinSize = 16;
            subhead.resizeTextMaxSize = 24;
            RectTransform subRt = subhead.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -84f);
            subRt.sizeDelta = new Vector2(-48f, 40f);

            RectTransform lpBadge = Ui.Panel("RankedLpBadge", cardRect, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(0f, 52f));
            Image lpBadgeBg = MakeSlicedPill("BadgeBg", lpBadge, btnSprite, new Color(0.04f, 0.08f, 0.18f, 0.76f), raycast: false);
            StretchFull(lpBadgeBg.rectTransform);

            lpText = Ui.MakeText("RankedLp", lpBadge, "", 22, new Color(0.48f, 0.96f, 0.64f, 1f), TextAnchor.MiddleCenter, displayFont);
            lpText.raycastTarget = false;
            lpText.resizeTextForBestFit = true;
            lpText.resizeTextMinSize = 15;
            lpText.resizeTextMaxSize = 22;
            RectTransform lpRt = lpText.rectTransform;
            lpRt.anchorMin = Vector2.zero;
            lpRt.anchorMax = Vector2.one;
            lpRt.offsetMin = new Vector2(16f, 4f);
            lpRt.offsetMax = new Vector2(-16f, -4f);

            playAgainBtnRect = BuildPillButton(
                cardRect,
                "PlayAgainButton",
                "PLAY AGAIN",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-136f, 20f),
                new Vector2(252f, 96f),
                btnSprite,
                new Color(0.18f, 0.68f, 0.36f, 0.98f),
                displayFont,
                22,
                Interactive,
                () =>
                {
                    Haptics.LightTap();
                    _onPlayAgain?.Invoke();
                });

            homeBtnRect = BuildPillButton(
                cardRect,
                "HomeButton",
                "HOME",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(136f, 20f),
                new Vector2(252f, 96f),
                btnSprite,
                new Color(0.22f, 0.34f, 0.56f, 0.98f),
                displayFont,
                22,
                Interactive,
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

        bool _inResponsiveLayout;

        void OnSafeAreaChanged(SafeArea _) => ApplyResponsiveLayout();

        void OnSettingsChanged() => ApplyResponsiveLayout();

        public void ApplyResponsiveLayout()
        {
            if (_inResponsiveLayout || Canvas == null || _actionClusterRect == null) return;
            _inResponsiveLayout = true;
            try
            {
                _leaveModalSafe?.ForceApply();
                _matchOverModalSafe?.ForceApply();

            bool leftHanded = SettingsStore.Current.LeftHanded;
            float userScale = Mathf.Clamp(SettingsStore.Current.ControlScale, 0.85f, 1.2f);
            if (_appliedLeftHanded != leftHanded)
            {
                _appliedLeftHanded = leftHanded;
                if (Joystick != null) Joystick.SetLeftHanded(leftHanded);
                if (Look != null) Look.SetLeftHanded(leftHanded);
            }
            _appliedControlScale = userScale;

            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float safeWidth = safeSize.x;
            float safeHeight = safeSize.y;

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

            float gutterPx = Mathf.Max(16f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, Canvas));
            Vector2 leaveSize = ScreenFitMetrics.ClampTouchSize(new Vector2(126f, 96f), ScreenFitMetrics.MinTouchDp, Canvas);
            if (_leaveButtonRect != null)
            {
                _leaveButtonRect.anchoredPosition = new Vector2(14f, -8f);
                _leaveButtonRect.sizeDelta = leaveSize;
            }

            if (_rolePillRect != null)
            {
                _rolePillRect.anchoredPosition = new Vector2(14f + leaveSize.x + 10f, -8f);
                _rolePillRect.sizeDelta = new Vector2(134f, 52f);
            }

            if (_statusHintPillRect != null)
            {
                UpdateStatusHintLayout(leaveSize.y);
            }

            if (_brownoutBannerRect != null)
            {
                float maxAlertW = Mathf.Clamp(safeWidth * 0.5f - 184f, 220f, 284f);
                _brownoutBannerRect.anchoredPosition = new Vector2(-16f, -8f);
                _brownoutBannerRect.sizeDelta = new Vector2(maxAlertW, 44f);
            }

            if (_cancelZoneRect != null)
            {
                Vector2 cancelSize = ScreenFitMetrics.ClampTouchSize(new Vector2(228f, 96f), ScreenFitMetrics.MinTouchDp, Canvas);
                _cancelZoneRect.sizeDelta = cancelSize;
                if (leftHanded)
                {
                    float hintBottom = _statusHintPillRect != null
                        ? (-_statusHintPillRect.anchoredPosition.y + _statusHintPillRect.sizeDelta.y)
                        : (8f + leaveSize.y);
                    _cancelZoneRect.anchorMin = _cancelZoneRect.anchorMax = new Vector2(0f, 1f);
                    _cancelZoneRect.pivot = new Vector2(0f, 1f);
                    _cancelZoneRect.anchoredPosition = new Vector2(16f, -(hintBottom + gutterPx));
                }
                else
                {
                    float alertBottom = 8f + 44f;
                    _cancelZoneRect.anchorMin = _cancelZoneRect.anchorMax = new Vector2(1f, 1f);
                    _cancelZoneRect.pivot = new Vector2(1f, 1f);
                    _cancelZoneRect.anchoredPosition = new Vector2(-16f, -(alertBottom + gutterPx));
                }
            }

            if (_tayaCornerBadge != null && _tayaCornerBadge.transform is RectTransform tayaRect)
            {
                tayaRect.anchorMin = tayaRect.anchorMax = corner;
                tayaRect.pivot = corner;
                tayaRect.anchoredPosition = new Vector2(sign * 24f, 34f);
            }

            if (_tumbangRollRect != null)
            {
                float maxHalfW = safeWidth * 0.5f - (14f + leaveSize.x + 16f);
                float rollW = Mathf.Clamp(maxHalfW * 2f, 320f, 640f);
                float rollTop = 98f;
                float maxSightTop = safeHeight * 0.25f - 3f;
                float rollH = Mathf.Clamp(maxSightTop - rollTop, 64f, 76f);
                _tumbangRollRect.sizeDelta = new Vector2(rollW, rollH);
                _tumbangRollRect.anchoredPosition = new Vector2(0f, -rollTop);
                LayoutTumbangRollSlots(_tumbangRollSlotRects, rollW, rollH);
            }

            // Responsive >= 48 dp sizing for LeaveConfirmModal & MatchOverModal cards and buttons
            if (_leaveModalSafe != null && _leaveConfirmShown) _leaveModalSafe.ForceApply();
            if (_matchOverModalSafe != null && _matchOverShown) _matchOverModalSafe.ForceApply();

            float minModalTouch = ScreenFitMetrics.ClampTouchSize(96f, ScreenFitMetrics.MinTouchDp, Canvas);
            float modalBtnH = Mathf.Max(96f, minModalTouch);

            if (_leaveCardRect != null)
            {
                float cardW = Mathf.Clamp(safeWidth - 48f, Mathf.Max(420f, minModalTouch * 2f + 96f), 560f);
                float cardH = Mathf.Clamp(modalBtnH + 208f, 304f, Mathf.Max(304f, safeHeight - 32f));
                float btnW = Mathf.Max(minModalTouch, (cardW - 60f) * 0.5f);
                _leaveCardRect.sizeDelta = new Vector2(cardW, cardH);
                if (_leaveResumeBtnRect != null)
                {
                    _leaveResumeBtnRect.sizeDelta = new Vector2(btnW, modalBtnH);
                    _leaveResumeBtnRect.anchoredPosition = new Vector2(-(btnW * 0.5f + 10f), 20f);
                }
                if (_leaveConfirmBtnRect != null)
                {
                    _leaveConfirmBtnRect.sizeDelta = new Vector2(btnW, modalBtnH);
                    _leaveConfirmBtnRect.anchoredPosition = new Vector2(btnW * 0.5f + 10f, 20f);
                }
            }

            if (_matchOverCardRect != null)
            {
                float cardW = Mathf.Clamp(safeWidth - 48f, Mathf.Max(440f, minModalTouch * 2f + 96f), 584f);
                float cardH = Mathf.Clamp(modalBtnH + 248f, 344f, Mathf.Max(344f, safeHeight - 32f));
                float btnW = Mathf.Max(minModalTouch, (cardW - 60f) * 0.5f);
                _matchOverCardRect.sizeDelta = new Vector2(cardW, cardH);
                if (_matchOverPlayAgainBtnRect != null)
                {
                    _matchOverPlayAgainBtnRect.sizeDelta = new Vector2(btnW, modalBtnH);
                    _matchOverPlayAgainBtnRect.anchoredPosition = new Vector2(-(btnW * 0.5f + 10f), 20f);
                }
                if (_matchOverHomeBtnRect != null)
                {
                    _matchOverHomeBtnRect.sizeDelta = new Vector2(btnW, modalBtnH);
                    _matchOverHomeBtnRect.anchoredPosition = new Vector2(btnW * 0.5f + 10f, 20f);
                }
            }
            }
            finally
            {
                _inResponsiveLayout = false;
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
            if (_tumbangRollRect != null && _tumbangRollRect.gameObject.activeInHierarchy) dst.Add(_tumbangRollRect);
            if (_cancelZoneRect != null && _cancelZoneRect.gameObject.activeInHierarchy) dst.Add(_cancelZoneRect);
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
            {
                _leaveConfirmRoot.SetActive(_leaveConfirmShown);
                if (_leaveConfirmShown)
                {
                    _leaveModalSafe?.ForceApply();
                    ApplyResponsiveLayout();
                }
            }
            SyncGameplayControlsVisibility(_matchOverShown);
            if (_statusHintPillRect != null)
            {
                bool showHint = !_leaveConfirmShown && !_matchOverShown && !IsTumbangRollVisible && !string.IsNullOrEmpty(_cachedHintLine);
                if (_statusHintPillRect.gameObject.activeSelf != showHint)
                    _statusHintPillRect.gameObject.SetActive(showHint);
            }
        }

        void SyncGameplayControlsVisibility(bool matchOver)
        {
            bool showControls = !matchOver && !IsBlockingGameplayInput;
            if (Joystick != null && Joystick.gameObject.activeSelf != showControls)
            {
                if (!showControls) Joystick.Cancel();
                Joystick.gameObject.SetActive(showControls);
            }
            if (Look != null && Look.gameObject.activeSelf != showControls)
            {
                if (!showControls) Look.Cancel();
                Look.gameObject.SetActive(showControls);
            }
            if (_actionClusterRect != null && _actionClusterRect.gameObject.activeSelf != showControls)
            {
                if (!showControls)
                {
                    _primaryHandler?.CancelSilently(requestSimCancel: true);
                    _secondaryHandler?.CancelSilently(requestSimCancel: false);
                }
                _actionClusterRect.gameObject.SetActive(showControls);
            }
            if (!showControls && _cancelZoneRect != null && _cancelZoneRect.gameObject.activeSelf)
                _cancelZoneRect.gameObject.SetActive(false);
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

        public void Refresh(StreetBridge bridge, float dt = 0f, Camera worldCamera = null, Vector3? localWorldPos = null)
        {
            if (bridge == null) return;
            float stepDt = Mathf.Max(0f, dt);

            if (SettingsStore.Current.LeftHanded != _appliedLeftHanded
                || !Mathf.Approximately(SettingsStore.Current.ControlScale, _appliedControlScale))
            {
                ApplyResponsiveLayout();
            }

            RefreshHeader(bridge);
            RefreshBrownoutShade(bridge, stepDt, worldCamera, localWorldPos);
            RefreshBrownoutBanner(bridge, stepDt);
            RefreshTumbangRoll(bridge, stepDt);
            RefreshRoleAndHint(bridge);
            RefreshMatchOver(bridge);
            SyncGameplayControlsVisibility(bridge.IsMatchOver);
            RefreshActionButtons(bridge);
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
            bool showHint = !string.IsNullOrEmpty(hint)
                && !IsTumbangRollVisible
                && !bridge.IsMatchOver
                && !IsBlockingGameplayInput;
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
                    : ScreenFitMetrics.ClampTouchSize(new Vector2(126f, 96f), ScreenFitMetrics.MinTouchDp, Canvas).y);

            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float safeWidth = safeSize.x;
            float safeHeight = safeSize.y;

            float gutterPx = Mathf.Max(16f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, Canvas));
            float topRowBottom = 8f + leaveH + gutterPx;
            float maxSightTop = safeHeight * 0.25f - 2f;
            float pillH = Mathf.Clamp(maxSightTop - topRowBottom, 30f, 38f);

            string hint = _statusHintText.text ?? "";
            TextGenerationSettings genSettings = _statusHintText.GetGenerationSettings(new Vector2(2048f, pillH));
            genSettings.resizeTextForBestFit = false;
            genSettings.fontSize = 17;
            genSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
            genSettings.scaleFactor = 1f;
            float genWidth = _statusHintText.cachedTextGeneratorForLayout.GetPreferredWidth(hint, genSettings);
            float preferred = Mathf.Max(genWidth, hint.Length * 9.4f);

            float maxWidth = Mathf.Min(560f, Mathf.Max(220f, safeWidth - 32f));
            float pillWidth = Mathf.Clamp(preferred + 46f, 220f, maxWidth);
            _statusHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusHintPillRect.sizeDelta = new Vector2(pillWidth, pillH);
            _statusHintPillRect.anchoredPosition = new Vector2(14f, -topRowBottom);

            if (SettingsStore.Current.LeftHanded && _cancelZoneRect != null)
            {
                _cancelZoneRect.anchoredPosition = new Vector2(16f, -(topRowBottom + pillH + gutterPx));
            }
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

        void RefreshBrownoutShade(StreetBridge bridge, float dt, Camera worldCamera, Vector3? localWorldPos)
        {
            if (_brownoutShadeRoot == null) return;
            if (bridge.Kind != StreetGameKind.LangitLupa)
            {
                _brownoutShadeStrength = 0f;
                if (_brownoutShadeRoot.gameObject.activeSelf)
                    _brownoutShadeRoot.gameObject.SetActive(false);
                return;
            }

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            bool dark = (ForceBrownoutPreview ?? ll.IsBrownout) && !bridge.IsMatchOver;
            float target = dark ? 1f : 0f;
            bool instant = !Application.isPlaying
                || dt <= 0f
                || SettingsStore.Current.ReduceMotion
                || ForceBrownoutPreview.HasValue;

            _brownoutShadeStrength = instant
                ? target
                : Mathf.MoveTowards(_brownoutShadeStrength, target, dt / BrownoutFadeSeconds);

            bool visible = _brownoutShadeStrength > 0.002f;
            if (_brownoutShadeRoot.gameObject.activeSelf != visible)
                _brownoutShadeRoot.gameObject.SetActive(visible);
            if (!visible) return;

            Camera cam = worldCamera != null ? worldCamera : _defaultCamera;
            float aspect = 16f / 9f;
            if (cam != null && cam.aspect > 0.1f)
            {
                aspect = cam.aspect;
            }
            else
            {
                Vector2 safeSz = ScreenFitMetrics.SafeCanvasSize(Canvas);
                if (safeSz.y > 1f) aspect = safeSz.x / safeSz.y;
                else if (Screen.height > 0) aspect = (float)Screen.width / Screen.height;
            }
            aspect = Mathf.Clamp(aspect, 0.4f, 3.5f);

            float cx = 0.5f;
            float cy = 0.44f;
            float ry = 0.46f;

            StreetKidSnapshot? me = bridge.LocalKid;
            if (cam != null && (localWorldPos.HasValue || me.HasValue))
            {
                Vector3 focusWorld = localWorldPos ?? KalyeahResources.World(me.Value.Position, 0.35f);
                Vector3 vpCenter = cam.WorldToViewportPoint(focusWorld);
                if (vpCenter.z > 0.05f)
                {
                    cx = Mathf.Clamp(vpCenter.x, 0.14f, 0.86f);
                    cy = Mathf.Clamp(vpCenter.y, 0.16f, 0.84f);

                    float sightMeters = Mathf.Max(4.5f, ll.BrownoutSight);
                    Vector3 vpEdge = cam.WorldToViewportPoint(focusWorld + cam.transform.right * sightMeters);
                    float dxNormH = (vpEdge.x - vpCenter.x) * aspect;
                    float dyNormH = vpEdge.y - vpCenter.y;
                    float projectedRy = Mathf.Sqrt(dxNormH * dxNormH + dyNormH * dyNormH);
                    ry = Mathf.Clamp(projectedRy, 0.38f, 0.66f);
                }
            }

            float rx = ry / aspect;
            float x0 = cx - rx;
            float x1 = cx + rx;
            float y0 = cy - ry;
            float y1 = cy + ry;

            float cx0 = Mathf.Clamp01(x0);
            float cx1 = Mathf.Clamp01(x1);
            float cy0 = Mathf.Clamp01(y0);
            float cy1 = Mathf.Clamp01(y1);

            SetNormRect(_brownoutHoleRect, x0, y0, x1, y1);
            SetNormRect(_brownoutEdgeRects[0], 0f, cy1, 1f, 1f);
            SetNormRect(_brownoutEdgeRects[1], 0f, 0f, 1f, cy0);
            SetNormRect(_brownoutEdgeRects[2], 0f, cy0, cx0, cy1);
            SetNormRect(_brownoutEdgeRects[3], cx1, cy0, 1f, cy1);

            Color shadeCol = new Color(
                BrownoutShadeTint.r,
                BrownoutShadeTint.g,
                BrownoutShadeTint.b,
                BrownoutShadeMaxAlpha * _brownoutShadeStrength);

            _brownoutHoleImg.color = shadeCol;
            for (int i = 0; i < _brownoutEdgeImgs.Length; i++)
                _brownoutEdgeImgs[i].color = shadeCol;
        }

        static void SetNormRect(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            bool active = (x1 - x0) > 0.0001f && (y1 - y0) > 0.0001f;
            if (rt.gameObject.activeSelf != active)
                rt.gameObject.SetActive(active);
            if (!active) return;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void RefreshBrownoutBanner(StreetBridge bridge, float dt)
        {
            if (bridge.Kind != StreetGameKind.LangitLupa)
            {
                if (_brownoutBannerObj.activeSelf) _brownoutBannerObj.SetActive(false);
                return;
            }

            LangitLupaSnapshot ll = bridge.LangitLupaState;
            bool dark = (ForceBrownoutPreview ?? ll.IsBrownout) && !bridge.IsMatchOver;
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
                int bSec = ll.BrownoutLeft > 0.05f ? Mathf.CeilToInt(ll.BrownoutLeft) : 6;
                if (bSec != _cachedBrownoutSeconds)
                {
                    _cachedBrownoutSeconds = bSec;
                    float sight = ll.BrownoutSight > 0.5f ? ll.BrownoutSight : 5f;
                    _brownoutBannerText.text = $"BROWNOUT! {Mathf.RoundToInt(sight)}M SIGHT ({bSec}S)";
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

        void SyncTumbangRollTarget(int tayaId)
        {
            if (_tumbangRollSlotIds == null || _tumbangRollSlotIds.Length == 0)
                return;
            if (_tumbangRollTargetKidId == tayaId && _tumbangRollTotalSteps > 0)
                return;

            _tumbangRollTargetKidId = tayaId;
            int found = 0;
            for (int i = 0; i < _tumbangRollSlotIds.Length; i++)
            {
                if (_tumbangRollSlotIds[i] == tayaId)
                {
                    found = i;
                    break;
                }
            }
            _tumbangRollTargetSlot = found;
            _tumbangRollTotalSteps = TumbangRollLaps * _tumbangRollSlotIds.Length + found;
            if (_tumbangRollTotalSteps <= 0)
                _tumbangRollTotalSteps = _tumbangRollSlotIds.Length;
        }

        void RefreshTumbangRoll(StreetBridge bridge, float dt)
        {
            if (_tumbangRollRoot == null || _tumbangRollDismissed)
                return;

            if (!Interactive || bridge.Kind != StreetGameKind.TumbangPreso || bridge.IsMatchOver || IsBlockingGameplayInput)
            {
                if (_tumbangRollRoot.activeSelf)
                    _tumbangRollRoot.SetActive(false);
                if (bridge.IsMatchOver)
                    _tumbangRollDismissed = true;
                return;
            }

            SyncTumbangRollTarget(bridge.TumbangPresoState.TayaId);
            int slotCount = _tumbangRollSlotIds.Length;
            if (slotCount == 0) return;

            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            _tumbangRollElapsed += dt;

            float dismissAfter = reduceMotion ? 1.0f : TumbangRollTotalSeconds;
            if (!bridge.IsIntro || _tumbangRollElapsed >= dismissAfter)
            {
                _tumbangRollDismissed = true;
                if (_tumbangRollRoot.activeSelf)
                    _tumbangRollRoot.SetActive(false);
                return;
            }

            if (!_tumbangRollRoot.activeSelf)
                _tumbangRollRoot.SetActive(true);

            if (reduceMotion)
            {
                ApplyTumbangRollHighlight(_tumbangRollTargetSlot, landed: true);
                if (!_tumbangRollLandedFired && dt > 0f)
                {
                    _tumbangRollLandedFired = true;
                    TumbangRollLanded?.Invoke();
                }
                return;
            }

            float spinDuration = TumbangRollTotalSeconds * TumbangRollFinishShare;
            int desiredStep = 0;
            if (_tumbangRollElapsed >= spinDuration)
            {
                desiredStep = _tumbangRollTotalSteps;
            }
            else
            {
                for (int k = 1; k <= _tumbangRollTotalSteps; k++)
                {
                    float norm = k / (float)_tumbangRollTotalSteps;
                    float stepTime = spinDuration * Mathf.Pow(norm, TumbangRollCurve);
                    if (_tumbangRollElapsed >= stepTime)
                        desiredStep = k;
                    else
                        break;
                }
            }

            if (desiredStep != _tumbangRollStepIndex)
            {
                bool hadPriorStep = _tumbangRollStepIndex >= 0;
                _tumbangRollStepIndex = desiredStep;

                if (desiredStep >= _tumbangRollTotalSteps)
                {
                    ApplyTumbangRollHighlight(_tumbangRollTargetSlot, landed: true);
                    if (!_tumbangRollLandedFired)
                    {
                        _tumbangRollLandedFired = true;
                        if (dt > 0f)
                            TumbangRollLanded?.Invoke();
                    }
                }
                else
                {
                    ApplyTumbangRollHighlight(desiredStep % slotCount, landed: false);
                    if (hadPriorStep && dt > 0f)
                        TumbangRollStepped?.Invoke();
                }
            }
        }

        void ApplyTumbangRollHighlight(int activeIndex, bool landed)
        {
            int encoded = landed ? (activeIndex + 1000) : activeIndex;
            if (_tumbangRollActiveSlot == encoded)
                return;
            _tumbangRollActiveSlot = encoded;

            for (int i = 0; i < _tumbangRollSlotBorders.Length; i++)
            {
                bool active = i == activeIndex;
                _tumbangRollSlotBorders[i].color = active
                    ? (landed ? TumbangResultColor : GoldText)
                    : TumbangSlotIdleBorder;
                _tumbangRollSlotRects[i].localScale = active
                    ? Vector3.one * (landed ? 1.08f : 1.04f)
                    : Vector3.one;
            }

            if (_tumbangRollWhoText != null && activeIndex >= 0 && activeIndex < _tumbangRollSpinNames.Length)
            {
                _tumbangRollWhoText.text = landed
                    ? _tumbangRollLandedTexts[activeIndex]
                    : _tumbangRollSpinNames[activeIndex];
                _tumbangRollWhoText.color = landed ? TumbangResultColor : Ui.Cream;
            }
        }

        void RefreshActionButtons(StreetBridge bridge)
        {
            bool controlsLive = !bridge.IsMatchOver && !IsBlockingGameplayInput;
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
                    SyncGameplayControlsVisibility(false);
                }
                return;
            }

            if (_matchOverShown) return;
            _matchOverShown = true;
            SetLeaveConfirmOpen(false);
            CancelActiveTouches();
            _matchOverRoot.SetActive(true);
            _matchOverModalSafe?.ForceApply();
            ApplyResponsiveLayout();
            SyncGameplayControlsVisibility(true);

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
                MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
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
