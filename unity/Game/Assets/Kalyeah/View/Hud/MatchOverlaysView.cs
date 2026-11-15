using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full match HUD overlays on the 1280x720 SafeArea-pinned landscape canvas:
    /// 0. HurtPulse low-HP/damage screen-edge red vignette &amp; WorldWaypoints projected 3D-&gt;2D Home/Enemy/Rescue diamonds.
    /// 1. Local Hero Vitals chip (HP + damage trail + Stamina/Overclock + Merienda buff icon/timer) &amp; 5v5 Squad Wings (enemy HP hidden).
    /// 2. Top-right TakedownFeed (3 recent tags/rescues, auto-fading) &amp; CaptureAlert banner (0.4s hysteresis anti-flicker).
    /// 3. Tagged Sidewalk Overlay (rescue timer + teammate pips + 3-superpower loadout swap picker via SimBridge.TrySwapLoadout).
    /// 4. Quick-Chat / Sticker wheel + speech chips (SimBridge.SendQuickChat / QuickChatSaid).
    /// 5. First-match Agawang Base BriefingCard, Round Intro, Point Freeze, Match Victory &amp; Ranked LP summary, and In-Match Pause &amp; Quick Settings Sheet (+ Android Back toggle).
    /// </summary>
    public sealed class MatchOverlaysView : IDisposable
    {
        public const float CaptureHysteresisSeconds = 0.4f;
        public const float TakedownLifetimeSeconds = 3.4f;
        public const float TakedownEnterSeconds = 0.14f;
        public const float TakedownFadeSeconds = 0.50f;
        public const float SpeechDurationSeconds = 2.0f;
        public const float DamageTrailHoldSeconds = 0.16f;
        public const float DamageTrailDecayPerSec = 1.20f;
        public const float BriefingAutoDismissSeconds = 12.0f;
        public const float HurtEdgeDepth = 150f;
        public const float LowHealthThresholdShare = 0.30f;
        public const float HurtFlashMinAlpha = 0.20f;
        public const float HurtFlashMaxAlpha = 0.48f;
        public const float HurtTaggedAlpha = 0.70f;
        public const float HurtFlashSeconds = 0.40f;
        public const float HurtTaggedSeconds = 0.90f;
        public const float LowHealthBreathPeriod = 1.60f;

        public const int MaxTakedownRows = 3;
        public const int SquadWingSlots = 5;
        public const int SpeechPoolSize = 4;
        public const int MaxSwapWeaponTiles = 16;
        public const int ConfettiCount = 18;
        public const int MaxWaypoints = 3;

        public static readonly string[] StickerPhrases =
        {
            "Haha!", "Easy!", "Grrr!", "Love it!", "Huhu!", "Yum!",
        };

        public static readonly string[] StickerSlugs =
        {
            "haha", "easy", "grrr", "love_it", "huhu", "yum",
        };

        static readonly Color ColInk = new Color(0.05f, 0.07f, 0.12f, 0.94f);
        static readonly Color ColCardDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        static readonly Color ColModalDark = new Color(0.06f, 0.09f, 0.16f, 0.97f);
        static readonly Color ColCardWarm = new Color(0.98f, 0.95f, 0.89f, 0.99f);
        static readonly Color ColWarmText = new Color(0.16f, 0.14f, 0.13f, 1f);
        static readonly Color ColWarmSubText = new Color(0.30f, 0.25f, 0.21f, 1f);
        static readonly Color ColGold = new Color(1f, 0.80f, 0.24f, 1f);
        static readonly Color ColHpWhite = new Color(0.98f, 0.99f, 1f, 0.97f);
        static readonly Color ColMissingCoral = new Color(0.89f, 0.24f, 0.17f, 0.88f);
        static readonly Color ColStatusAlert = new Color(1f, 0.58f, 0.50f, 1f);
        static readonly Color ColTrailAmber = new Color(1f, 0.78f, 0.58f, 0.92f);
        static readonly Color ColRegenMint = new Color(0.28f, 0.92f, 0.56f, 0.96f);
        static readonly Color ColStamina = new Color(0.32f, 0.86f, 0.98f, 0.95f);
        static readonly Color ColOverclock = new Color(1f, 0.84f, 0.26f, 0.98f);
        static readonly Color ColPalamig = new Color(0.26f, 0.88f, 0.96f, 0.96f);
        static readonly Color ColTeks = new Color(0.98f, 0.74f, 0.22f, 0.96f);
        static readonly Color ColTuron = new Color(0.32f, 0.90f, 0.56f, 0.96f);
        static readonly Color ColHurtRed = new Color(0.92f, 0.10f, 0.08f, 1f);

        static readonly Color ColTileUnpicked = new Color(0.11f, 0.15f, 0.24f, 0.97f);
        static readonly Color ColTilePicked = new Color(0.20f, 0.34f, 0.56f, 0.99f);
        static readonly Color ColSwapReady = new Color(0.18f, 0.68f, 0.42f, 1f);
        static readonly Color ColSwapDisabled = new Color(0.28f, 0.32f, 0.40f, 0.92f);
        static readonly Color ColDeltaPos = new Color(0.075f, 0.431f, 0.380f, 1f);
        static readonly Color ColDeltaNeg = new Color(0.722f, 0.227f, 0.157f, 1f);
        static readonly Color ColDefeatCoral = new Color(0.96f, 0.52f, 0.44f, 1f);

        static readonly Color ColWaypointGold = new Color(1.000f, 0.839f, 0.439f, 1f);
        static readonly Color ColWaypointDefend = new Color(0.961f, 0.380f, 0.322f, 1f);
        static readonly Color ColWaypointRescue = new Color(0.380f, 0.941f, 0.620f, 1f);

        static Sprite s_roundedCard;
        static Sprite s_pillSprite;
        static Sprite s_warningSprite;
        static Sprite s_edgeGradientSprite;
        static Sprite s_chevronSprite;

        struct WaypointMarkerUi
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform DiamondBox;
            public Image DiamondRing;
            public Image DiamondFill;
            public RectTransform ChevronRect;
            public Image ChevronImg;
            public RectTransform PillRect;
            public Image PillBg;
            public Text PillText;
            public int CachedMeters;
            public bool CachedClamped;
            public bool CachedUrgent;
            public string CachedWord;
        }

        struct SquadSlotUi
        {
            public RectTransform Root;
            public Image Ring;
            public Image FallbackBg;
            public RawImage Face;
            public Text Initial;
            public Text YouTag;
            public Image HpTrack;
            public RectTransform HpFill;
            public Image HpFillImage;
            public GameObject TaggedCross;
            public GameObject UltPip;
            public int CachedId;
            public string CachedChar;
        }

        struct TakedownEntry
        {
            public string LeftName;
            public string Badge;
            public string RightName;
            public bool Friendly;
            public bool IsRescue;
            public float Age;
        }

        struct TakedownRowUi
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Accent;
            public Text LeftText;
            public Text BadgeText;
            public Text RightText;
        }

        struct SpeechBubbleUi
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Border;
            public RawImage StickerArt;
            public Text SpeakerText;
            public Text PhraseText;
            public int PlayerId;
            public float TimeLeft;
            public bool Active;
        }

        struct TeammatePipUi
        {
            public RectTransform Root;
            public Image Ring;
            public RawImage Face;
            public Text Label;
            public string CachedChar;
        }

        struct SwapTileUi
        {
            public RectTransform Root;
            public Button Button;
            public Image Bg;
            public Image RoleStrip;
            public RawImage Icon;
            public Text NameText;
            public GameObject SlotBadgeObj;
            public Text SlotBadgeText;
            public string WeaponId;
        }

        struct ConfettiPiece
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 StartPos;
            public Vector2 Velocity;
            public float SpinSpeed;
        }

        sealed class BackKeyBehaviour : MonoBehaviour
        {
            [NonSerialized] public MatchOverlaysView Owner;

            void Update()
            {
                Owner?.PollHardwareBackKey();
            }
        }

        /// <summary>
        /// Non-Selectable SafeArea tap catcher for modal outside-tap dismiss (avoids putting a Selectable on full-bleed scrims
        /// or overlapping child modal Buttons during automated bounds sweeps).
        /// </summary>
        sealed class DismissTapCatcher : MonoBehaviour, IPointerClickHandler
        {
            [NonSerialized] public Action OnTap;

            public void OnPointerClick(PointerEventData eventData)
            {
                OnTap?.Invoke();
            }
        }

        readonly Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly bool _interactive;

        public RectTransform Root { get; }
        public Camera WorldCamera { get; set; }
        public AudioHub Audio { get; set; }
        public Action OnPlayAgain { get; set; }
        public Action OnBackToHome { get; set; }
        public Action<bool> OnPauseChanged { get; set; }

        public bool IsPaused { get; private set; }
        public bool IsSwapPickerOpen { get; private set; }
        public bool IsQuickChatOpen { get; private set; }
        public bool IsBriefingVisible => _interactive && _briefingRoot != null && _briefingRoot.gameObject.activeSelf;
        public bool IsHurtPulseVisible => _hurtPulseRoot != null && _hurtPulseRoot.gameObject.activeSelf;
        public float HurtPulseAlpha => _hurtCurrentAlpha;
        public int ActiveWaypointCount { get; private set; }
        public bool IsTaggedOverlayVisible => _taggedRoot != null && _taggedRoot.gameObject.activeSelf;
        public bool IsCaptureAlertVisible => _captureAlertRoot != null && _captureAlertRoot.gameObject.activeSelf;
        public bool IsLeaveConfirmOpen => _pauseConfirmRow != null && _pauseConfirmRow.gameObject.activeSelf;
        public RankOutcomeInfo? LastRecordedOutcome { get; private set; }

        bool _suppressTakedownFeed;

        /// <summary>Hides the top-right TakedownFeed while AbilityArc aim-drag is active so CancelAimZone never collides with feed rows.</summary>
        public bool SuppressTakedownFeed
        {
            get => _suppressTakedownFeed;
            set
            {
                if (_suppressTakedownFeed == value) return;
                _suppressTakedownFeed = value;
                SyncOverlayChromeVisibility();
            }
        }

        public bool IsBlockingGameplayInput =>
            IsPaused || IsSwapPickerOpen || IsBriefingVisible || _lastPhase == MatchPhaseKind.Intro || _lastPhase == MatchPhaseKind.MatchOver;

        // Bound sim & transient tracking
        SimBridge _sim;
        MatchPhaseKind _lastPhase = (MatchPhaseKind)(-1);
        bool _matchResultRecorded;
        int _lastBackFrame = -1;
        bool _hasLocalKid;
        bool _rawCaptureAlertActive;

        // 1. Hero Vitals & 5v5 Squad Wings
        readonly RectTransform _heroVitalsRoot;
        readonly Image _heroRing;
        readonly RawImage _heroFace;
        readonly Text _heroInitial;
        readonly RectTransform _heroHpFill;
        readonly Image _heroHpFillImg;
        readonly RectTransform _heroTrailFill;
        readonly RectTransform _heroMissingFill;
        readonly Image _heroMissingImg;
        readonly Text _heroHpText;
        readonly RectTransform _heroStamFill;
        readonly Image _heroStamImg;
        readonly Text _heroStatusText;
        readonly GameObject _heroBuffBadge;
        readonly Image _heroBuffDot;
        readonly Text _heroBuffText;
        string _cachedHeroChar = "";
        bool _vitalsSynced;
        int _lastHp = -1;
        int _lastMaxHp = -1;
        float _trailShare = 1f;
        float _trailHoldLeft;
        BuffKind _lastBuffKind = BuffKind.None;
        int _lastBuffCeilSec = -1;
        string _lastHeroStatus = "";

        readonly RectTransform _wingsContainer;
        readonly RectTransform _allyWingRoot;
        readonly RectTransform _enemyWingRoot;
        readonly SquadSlotUi[] _allySlots = new SquadSlotUi[SquadWingSlots];
        readonly SquadSlotUi[] _enemySlots = new SquadSlotUi[SquadWingSlots];
        readonly List<KidSnapshot> _allyScratch = new List<KidSnapshot>(SquadWingSlots);
        readonly List<KidSnapshot> _enemyScratch = new List<KidSnapshot>(SquadWingSlots);

        // 2. TakedownFeed & CaptureAlert
        readonly RectTransform _takedownRoot;
        readonly TakedownRowUi[] _takedownRows = new TakedownRowUi[MaxTakedownRows];
        readonly List<TakedownEntry> _takedownEntries = new List<TakedownEntry>(MaxTakedownRows + 1);
        bool _takedownDirty = true;

        readonly RectTransform _captureAlertRoot;
        readonly RectTransform _captureIconRect;
        readonly Image _capturePlateImg;
        readonly RectTransform _captureProgFill;
        float _captureHoldLeft;
        float _capturePulseTime;
        bool _wasRawCapturing;
        int _lastCaptureColorblind = -1;

        // 3. Tagged Sidewalk Overlay & Swap Picker
        readonly RectTransform _taggedRoot;
        readonly Text _taggedTitleText;
        readonly Text _taggedTimerText;
        readonly Text _taggedHintText;
        readonly RectTransform _taggedProgressFill;
        readonly Image _taggedProgressImg;
        readonly TeammatePipUi[] _teammatePips = new TeammatePipUi[SquadWingSlots - 1];
        readonly Button _openSwapBtn;
        readonly RectTransform _openSwapRect;
        int _lastTaggedHintCode = -999;
        int _lastTaggedHintArg = -999;
        int _lastTaggedTenths = -999;

        readonly RectTransform _swapModalRoot;
        readonly RectTransform _swapDimRect;
        readonly RectTransform _swapCardRect;
        readonly RectTransform _swapTitleRect;
        readonly RectTransform _swapSubRect;
        readonly RectTransform _swapCloseRect;
        readonly RectTransform _swapViewportRect;
        readonly RectTransform _swapContentRect;
        readonly SwapTileUi[] _swapTiles = new SwapTileUi[MaxSwapWeaponTiles];
        readonly List<string> _swapPicks = new List<string>(3);
        readonly Button _swapConfirmBtn;
        readonly RectTransform _swapConfirmRect;
        readonly Image _swapConfirmImg;
        readonly Text _swapConfirmText;
        int _activeSwapTileCount;

        // 4. Quick-Chat button, Sticker wheel & Speech chips
        readonly RectTransform _quickChatBtnRect;
        readonly Button _quickChatBtn;
        readonly RectTransform _quickChatModalRoot;
        readonly RectTransform _quickChatDimRect;
        readonly RectTransform _quickChatCardRect;
        readonly RectTransform[] _stickerBtnRects = new RectTransform[6];
        readonly SpeechBubbleUi[] _speechPool = new SpeechBubbleUi[SpeechPoolSize];

        // 5a. Top-left Pause Button & Pause Quick-Settings Sheet
        readonly RectTransform _pauseBtnRect;
        readonly Button _pauseBtn;
        readonly RectTransform _pauseSheetRoot;
        readonly RectTransform _pauseDimRect;
        readonly RectTransform _pauseCardRect;
        readonly RectTransform _pauseTitleRect;
        readonly RectTransform _pauseCloseRect;
        readonly RectTransform _pauseViewportRect;
        readonly RectTransform _pauseContentRect;
        readonly ScrollRect _pauseScroll;
        readonly RectTransform _pauseScrollTrackRect;
        readonly RectTransform _pauseScrollThumbRect;
        readonly RectTransform[] _pausePillRects = new RectTransform[8];
        readonly Text _toggleMusicText;
        readonly Text _toggleSfxText;
        readonly Text _toggleHapticsText;
        readonly Text _toggleLeftHandText;
        readonly Text _toggleAimGuideText;
        readonly Text _toggleReduceMotionText;
        readonly Text _toggleFpsTurboText;
        readonly Text _toggleColorblindText;
        readonly RectTransform _pauseFooterRow;
        readonly RectTransform _pauseResumeRect;
        readonly RectTransform _pauseLeaveRect;
        readonly RectTransform _pauseConfirmRow;
        readonly RectTransform _pauseWarnTextRect;
        readonly RectTransform _pauseStayRect;
        readonly RectTransform _pauseConfirmLeaveRect;

        // 5b. Intro, Point Freeze & Match Victory / Ranked LP Summary
        readonly RectTransform _introRoot;
        readonly RectTransform _introDimRect;
        readonly RectTransform _introCardRect;
        readonly Text _introRoundText;
        readonly Text _introCountdownText;
        readonly RectTransform _introBarFill;
        float _introInitialSeconds = 10f;
        int _lastIntroCeil = -1;
        int _lastIntroRound = -1;

        readonly RectTransform _pointFreezeRoot;
        readonly RectTransform _pointFreezeDimRect;
        readonly Text _freezeSubjectText;
        readonly Text _freezeOutcomeText;
        readonly Text _freezeScoreText;
        int _lastPointWinnerTeam = -1;
        CaptureReasonKind _lastPointReason = CaptureReasonKind.None;

        readonly RectTransform _victoryRoot;
        readonly RectTransform _victoryDimRect;
        readonly RectTransform _victoryCardRect;
        readonly Text _victoryTitleText;
        readonly Text _victorySubText;
        readonly Text _victoryYouCapText;
        readonly Text _victoryThemCapText;
        readonly Text _victoryMyScoreText;
        readonly Text _victoryTheirScoreText;
        readonly GameObject _promoBannerObj;
        readonly RectTransform _victoryRankCardRect;
        readonly Image _rankEmblemBg;
        readonly Text _rankEmblemInitial;
        readonly Text _rankTierNameText;
        readonly Image _rankDeltaChipImg;
        readonly Text _rankDeltaText;
        readonly RectTransform _rankLpBarFill;
        readonly Image _rankLpBarImg;
        readonly Text _rankLpDetailText;
        readonly RectTransform _playAgainBtnRect;
        readonly RectTransform _homeBtnRect;
        readonly ConfettiPiece[] _confetti = new ConfettiPiece[ConfettiCount];
        float _lpAnimFrom;
        float _lpAnimTo;
        float _lpAnimElapsed;
        bool _confettiActive;
        float _confettiElapsed;

        // 0. HurtPulse screen-edge vignette & projected 3D->2D WorldWaypoints + first-match BriefingCard
        readonly RectTransform _hurtPulseRoot;
        readonly Image[] _hurtEdges = new Image[4];
        float _hurtFlashPeak;
        float _hurtFlashSpan = 0.4f;
        float _hurtFlashLeft;
        float _lowHpWeight;
        float _lowHpTime;
        float _hurtCurrentAlpha;

        readonly RectTransform _waypointsRoot;
        readonly WaypointMarkerUi[] _waypoints = new WaypointMarkerUi[MaxWaypoints];
        float _waypointPulseTime;
        string _cachedWaypointLang;
        int _cachedWaypointKeyCount = -1;
        string _cachedBaseWord = "BASE";
        string _cachedDefendWord = "DEFEND";
        string _cachedRescueWord = "RESCUE";

        readonly RectTransform _briefingRoot;
        readonly RectTransform _briefingDimRect;
        readonly RectTransform _briefingPanelRect;
        readonly RectTransform _briefingGotItRect;
        readonly RectTransform _briefingBarFill;
        readonly Text _briefingCountdownText;
        float _briefingTimeLeft = BriefingAutoDismissSeconds;
        int _lastBriefingCeil = -1;

        public MatchOverlaysView(
            RectTransform safeRoot,
            SimBridge sim,
            Camera worldCamera = null,
            bool interactive = true,
            Action onPlayAgain = null,
            Action onBackToHome = null,
            AudioHub audio = null)
        {
            if (safeRoot == null) throw new ArgumentNullException(nameof(safeRoot));
            _canvas = safeRoot.GetComponentInParent<Canvas>();
            _safeArea = safeRoot.GetComponentInParent<SafeArea>();
            _interactive = interactive;
            WorldCamera = worldCamera;
            Audio = audio;
            OnPlayAgain = onPlayAgain;
            OnBackToHome = onBackToHome;

            Root = Ui.Fill("MatchOverlays", safeRoot);
            if (_interactive)
            {
                var backListener = Root.gameObject.AddComponent<BackKeyBehaviour>();
                backListener.Owner = this;
            }

            // Build HUD layers back-to-front so modals cleanly sit over persistent chrome.
            BuildHurtPulseOverlay(out _hurtPulseRoot);
            BuildWorldWaypointsOverlay(out _waypointsRoot);
            BuildSquadWings(out _wingsContainer, out _allyWingRoot, out _enemyWingRoot);
            BuildHeroVitals(
                out _heroVitalsRoot, out _heroRing, out _heroFace, out _heroInitial,
                out _heroHpFill, out _heroHpFillImg, out _heroTrailFill, out _heroMissingFill, out _heroMissingImg,
                out _heroHpText, out _heroStamFill, out _heroStamImg, out _heroStatusText,
                out _heroBuffBadge, out _heroBuffDot, out _heroBuffText);
            BuildTakedownFeed(out _takedownRoot);
            BuildCaptureAlert(out _captureAlertRoot, out _captureIconRect, out _capturePlateImg, out _captureProgFill);
            BuildSpeechPool();
            BuildTaggedOverlay(
                out _taggedRoot, out _taggedTitleText, out _taggedTimerText, out _taggedHintText,
                out _taggedProgressFill, out _taggedProgressImg, out _openSwapBtn, out _openSwapRect);
            BuildCornerButtons(out _pauseBtnRect, out _pauseBtn, out _quickChatBtnRect, out _quickChatBtn);
            BuildQuickChatModal(out _quickChatModalRoot, out _quickChatDimRect, out _quickChatCardRect);
            BuildSwapPickerModal(
                out _swapModalRoot, out _swapDimRect, out _swapCardRect,
                out _swapTitleRect, out _swapSubRect, out _swapCloseRect,
                out _swapViewportRect, out _swapContentRect,
                out _swapConfirmBtn, out _swapConfirmRect, out _swapConfirmImg, out _swapConfirmText);
            BuildPointFreezeOverlay(out _pointFreezeRoot, out _pointFreezeDimRect, out _freezeSubjectText, out _freezeOutcomeText, out _freezeScoreText);
            BuildIntroOverlay(out _introRoot, out _introDimRect, out _introCardRect, out _introRoundText, out _introCountdownText, out _introBarFill);
            BuildBriefingCard(out _briefingRoot, out _briefingDimRect, out _briefingPanelRect, out _briefingGotItRect, out _briefingBarFill, out _briefingCountdownText);
            BuildVictoryOverlay(
                out _victoryRoot, out _victoryDimRect, out _victoryCardRect, out _victoryTitleText, out _victorySubText,
                out _victoryYouCapText, out _victoryThemCapText,
                out _victoryMyScoreText, out _victoryTheirScoreText, out _promoBannerObj,
                out _victoryRankCardRect, out _rankEmblemBg, out _rankEmblemInitial, out _rankTierNameText,
                out _rankDeltaChipImg, out _rankDeltaText, out _rankLpBarFill, out _rankLpBarImg,
                out _rankLpDetailText, out _playAgainBtnRect, out _homeBtnRect);
            BuildPauseSheet(
                out _pauseSheetRoot, out _pauseDimRect, out _pauseCardRect,
                out _pauseTitleRect, out _pauseCloseRect, out _pauseViewportRect, out _pauseContentRect,
                out _pauseScroll, out _pauseScrollTrackRect, out _pauseScrollThumbRect,
                out _toggleMusicText, out _toggleSfxText, out _toggleHapticsText, out _toggleLeftHandText,
                out _toggleAimGuideText, out _toggleReduceMotionText, out _toggleFpsTurboText,
                out _toggleColorblindText, out _pauseFooterRow, out _pauseResumeRect, out _pauseLeaveRect,
                out _pauseConfirmRow, out _pauseWarnTextRect, out _pauseStayRect, out _pauseConfirmLeaveRect);

            ScreenFitMetrics.SimulatedViewportChanged += OnExternalLayoutOrSettingsChanged;
            SettingsStore.Current.Changed += OnExternalLayoutOrSettingsChanged;
            if (_safeArea != null)
                _safeArea.Changed += OnSafeAreaChanged;

            BindSim(sim);
            SyncQuickSettingsLabels();
            RefreshLayout();
            SyncBriefingCard(sim != null ? sim.Phase : MatchPhaseKind.Playing, 0f);
            SyncOverlayChromeVisibility();
            if (sim != null) Refresh(sim, 0f);
        }

        public void Dispose()
        {
            ScreenFitMetrics.SimulatedViewportChanged -= OnExternalLayoutOrSettingsChanged;
            SettingsStore.Current.Changed -= OnExternalLayoutOrSettingsChanged;
            if (_safeArea != null)
                _safeArea.Changed -= OnSafeAreaChanged;
            UnbindSim();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        void OnSafeAreaChanged(SafeArea _)
        {
            if (Root == null) return;
            RefreshLayout();
        }

        void OnExternalLayoutOrSettingsChanged()
        {
            if (Root == null) return;
            Haptics.Enabled = SettingsStore.Current.Haptics;
            _takedownDirty = true;
            SyncAudioFromSettings();
            SyncQuickSettingsLabels();
            RefreshLayout();
            if (_sim != null) Refresh(_sim, 0f);
        }

        public void BindSim(SimBridge sim)
        {
            if (ReferenceEquals(_sim, sim)) return;
            UnbindSim();
            _sim = sim;
            _lastPhase = (MatchPhaseKind)(-1);
            _matchResultRecorded = false;
            LastRecordedOutcome = null;
            _vitalsSynced = false;
            _hasLocalKid = false;
            _rawCaptureAlertActive = false;
            _captureHoldLeft = 0f;
            _wasRawCapturing = false;
            _hurtFlashLeft = 0f;
            _lowHpWeight = 0f;
            _lowHpTime = 0f;
            _hurtCurrentAlpha = 0f;
            if (_hurtPulseRoot != null) _hurtPulseRoot.gameObject.SetActive(false);
            HideAllWaypoints();
            ClearTakedowns();
            ClearSpeechBubbles();
            SetSwapPickerOpen(false);
            SetQuickChatOpen(false);
            SetPaused(false);
            SyncBriefingCard(_sim != null ? _sim.Phase : MatchPhaseKind.Playing, 0f);
            SyncOverlayChromeVisibility();

            if (_sim != null)
            {
                _sim.Tagged += OnSimTagged;
                _sim.PlayerDamaged += OnSimPlayerDamaged;
                _sim.Rescued += OnSimRescued;
                _sim.PointScored += OnSimPointScored;
                _sim.QuickChatSaid += OnSimQuickChatSaid;
                PopulateSwapCatalog(_sim);
            }
        }

        void UnbindSim()
        {
            if (_sim == null) return;
            _sim.Tagged -= OnSimTagged;
            _sim.PlayerDamaged -= OnSimPlayerDamaged;
            _sim.Rescued -= OnSimRescued;
            _sim.PointScored -= OnSimPointScored;
            _sim.QuickChatSaid -= OnSimQuickChatSaid;
            _sim = null;
        }

        public void Refresh(SimBridge sim)
        {
            float dt = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
            Refresh(sim, dt);
        }

        public void Refresh(float dt)
        {
            if (_sim != null) Refresh(_sim, dt);
        }

        public void Refresh(SimBridge sim, float dt)
        {
            if (sim == null || Root == null) return;
            if (!ReferenceEquals(_sim, sim))
                BindSim(sim);

            float safeDt = Mathf.Max(0f, dt);
            int localId = sim.LocalPlayerId;
            int localTeam = sim.LocalPlayerTeam;
            KidSnapshot? localKid = sim.LocalKid;

            SyncPhaseOverlays(sim, localKid, localTeam, safeDt);
            TickHurtPulse(sim, localKid, safeDt);
            SyncHeroVitals(sim, localKid, safeDt);
            SyncSquadWings(sim, localId, localTeam);
            TickTakedownFeed(safeDt);
            SyncCaptureAlert(sim, localTeam, safeDt);
            SyncTaggedOverlay(sim, localKid, localTeam);
            TickSpeechBubbles(sim, safeDt);
            SyncWorldWaypoints(sim, localKid, localId, localTeam, safeDt);
            SyncOverlayChromeVisibility();
        }

        void SyncOverlayChromeVisibility()
        {
            bool hideHudBehindFullModal = IsPaused || IsSwapPickerOpen || _lastPhase == MatchPhaseKind.MatchOver;

            if (_pauseBtnRect != null && _pauseBtnRect.gameObject.activeSelf == hideHudBehindFullModal)
                _pauseBtnRect.gameObject.SetActive(!hideHudBehindFullModal);
            if (_quickChatBtnRect != null && _quickChatBtnRect.gameObject.activeSelf == hideHudBehindFullModal)
                _quickChatBtnRect.gameObject.SetActive(!hideHudBehindFullModal);
            if (_wingsContainer != null && _wingsContainer.gameObject.activeSelf == hideHudBehindFullModal)
                _wingsContainer.gameObject.SetActive(!hideHudBehindFullModal);

            bool showHeroVitals = _hasLocalKid && !hideHudBehindFullModal;
            if (_heroVitalsRoot != null && _heroVitalsRoot.gameObject.activeSelf != showHeroVitals)
                _heroVitalsRoot.gameObject.SetActive(showHeroVitals);

            bool showCapture = _rawCaptureAlertActive && !hideHudBehindFullModal && !IsBriefingVisible;
            if (_captureAlertRoot != null && _captureAlertRoot.gameObject.activeSelf != showCapture)
            {
                _captureAlertRoot.gameObject.SetActive(showCapture);
                if (_captureIconRect != null) _captureIconRect.localScale = Vector3.one;
            }

            bool showFeed = !_suppressTakedownFeed && !IsQuickChatOpen && !IsBlockingGameplayInput;
            if (_takedownRoot != null && _takedownRoot.gameObject.activeSelf != showFeed)
                _takedownRoot.gameObject.SetActive(showFeed);
        }

        void ResolveFullBleedOffsets(out Vector2 bleedMin, out Vector2 bleedMax)
        {
            if (_safeArea == null)
            {
                bleedMin = Vector2.zero;
                bleedMax = Vector2.zero;
                return;
            }

            Vector4 insetsPx = ScreenFitMetrics.ResolveSafeInsetsPx(_safeArea.ReserveGesturePill);
            float scale = Mathf.Max(0.01f, ScreenFitMetrics.CanvasScaleFactor(_canvas));
            bleedMin = new Vector2(-insetsPx.x / scale, -insetsPx.y / scale);
            bleedMax = new Vector2(insetsPx.z / scale, insetsPx.w / scale);
        }

        static void ApplyBleedOffsets(RectTransform rt, Vector2 bleedMin, Vector2 bleedMax)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = bleedMin;
            rt.offsetMax = bleedMax;
        }

        /// <summary>Responsive layout pass clamping 5v5 wing spread, full-bleed scrims, modal bounds, and >= 48 dp touch floors across 21:9..4:3.</summary>
        public void RefreshLayout()
        {
            if (Root == null) return;
            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);

            ResolveFullBleedOffsets(out Vector2 bleedMin, out Vector2 bleedMax);
            ApplyBleedOffsets(_swapDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_quickChatDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_pointFreezeDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_introDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_briefingDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_victoryDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_pauseDimRect, bleedMin, bleedMax);
            ApplyBleedOffsets(_hurtPulseRoot, bleedMin - new Vector2(8f, 8f), bleedMax + new Vector2(8f, 8f));

            float cornerRightInset = Mathf.Max(24f, ScreenFitMetrics.DpToCanvasPx(12f, _canvas));
            if (_pauseBtnRect != null)
            {
                _pauseBtnRect.sizeDelta = new Vector2(minTouch, minTouch);
                _pauseBtnRect.anchoredPosition = new Vector2(12f, -12f);
            }
            if (_quickChatBtnRect != null)
            {
                _quickChatBtnRect.sizeDelta = new Vector2(minTouch, minTouch);
                _quickChatBtnRect.anchoredPosition = new Vector2(-cornerRightInset, -12f);
            }

            float topControlsBottom = -(minTouch + 22f);
            if (_heroVitalsRoot != null)
                _heroVitalsRoot.anchoredPosition = new Vector2(12f, topControlsBottom);
            if (_takedownRoot != null)
                _takedownRoot.anchoredPosition = new Vector2(-cornerRightInset, topControlsBottom);

            // Clamp top 5v5 Squad Wings between corner Pause/Quick-Chat buttons and central Scoreboard across 4:3..21:9.
            if (_wingsContainer != null)
            {
                float sideReserve = Mathf.Max(140f, minTouch + 28f);
                float totalSpan = ScreenFitMetrics.ClampWingSpread(760f, sideReserve, 920f, _canvas);
                _wingsContainer.sizeDelta = new Vector2(totalSpan, 48f);

                const float centerGap = 256f;
                float eachWingWidth = Mathf.Max(164f, (totalSpan - centerGap) * 0.5f);
                _allyWingRoot.sizeDelta = new Vector2(eachWingWidth, 48f);
                _enemyWingRoot.sizeDelta = new Vector2(eachWingWidth, 48f);
                LayoutWingSlots(_allySlots, eachWingWidth, rightAlign: true);
                LayoutWingSlots(_enemySlots, eachWingWidth, rightAlign: false);
            }

            if (_taggedRoot != null)
            {
                float trayW = Mathf.Clamp(safeSize.x - 36f, 520f, 680f);
                float desiredSwapW = Mathf.Max(196f, trayW * 0.34f - 20f);
                Vector2 swapBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(desiredSwapW, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
                swapBtnSize.x = Mathf.Max(minTouch, Mathf.Min(swapBtnSize.x, trayW * 0.35f - 16f));
                float trayH = Mathf.Max(148f, swapBtnSize.y + 32f);
                _taggedRoot.sizeDelta = new Vector2(trayW, trayH);

                if (_openSwapRect != null)
                {
                    _openSwapRect.sizeDelta = swapBtnSize;
                    _openSwapRect.anchoredPosition = new Vector2(-16f, 0f);
                }
            }

            if (_quickChatCardRect != null)
            {
                float rightInset = Mathf.Max(24f, ScreenFitMetrics.DpToCanvasPx(12f, _canvas));
                float topOffset = minTouch + 20f;
                float stickerCellW = Mathf.Max(122f, minTouch + 20f);
                float stickerCellH = Mathf.Max(102f, minTouch + 6f);
                const float pad = 14f;
                const float gap = 10f;
                float cardW = stickerCellW * 2f + gap + pad * 2f;
                float maxCardH = Mathf.Max(minTouch * 3f + gap * 2f + pad * 2f, safeSize.y - topOffset - 14f);
                float cardH = Mathf.Min(stickerCellH * 3f + gap * 2f + pad * 2f, maxCardH);
                _quickChatCardRect.anchoredPosition = new Vector2(-rightInset, -topOffset);
                _quickChatCardRect.sizeDelta = new Vector2(cardW, cardH);

                float cellH = Mathf.Max(minTouch, (cardH - pad * 2f - gap * 2f) / 3f);
                for (int i = 0; i < _stickerBtnRects.Length; i++)
                {
                    int col = i % 2;
                    int row = i / 2;
                    RectTransform rt = _stickerBtnRects[i];
                    if (rt == null) continue;
                    rt.sizeDelta = new Vector2(stickerCellW, cellH);
                    rt.anchoredPosition = new Vector2(pad + col * (stickerCellW + gap), -(pad + row * (cellH + gap)));
                }
            }

            if (_swapCardRect != null)
            {
                float modalW = Mathf.Clamp(safeSize.x - 40f, 440f, 800f);
                float modalH = Mathf.Clamp(safeSize.y - 28f, 420f, 620f);
                _swapCardRect.sizeDelta = new Vector2(modalW, modalH);
                LayoutSwapGrid(modalW, modalH, minTouch);
            }

            if (_pauseCardRect != null)
            {
                float sheetW = Mathf.Clamp(safeSize.x - 44f, 440f, 660f);
                float sheetH = Mathf.Clamp(safeSize.y - 20f, 440f, 664f);
                _pauseCardRect.sizeDelta = new Vector2(sheetW, sheetH);
                LayoutPauseSheet(sheetW, sheetH, minTouch);
            }

            if (_introCardRect != null)
            {
                float introW = Mathf.Clamp(safeSize.x - 48f, 380f, 560f);
                _introCardRect.sizeDelta = new Vector2(introW, 284f);
            }

            Vector2 gotItSize = ScreenFitMetrics.ClampTouchSize(new Vector2(248f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            if (_briefingGotItRect != null)
            {
                _briefingGotItRect.sizeDelta = gotItSize;
                _briefingGotItRect.anchoredPosition = new Vector2(0f, 18f);
            }
            if (_briefingPanelRect != null)
            {
                float briefW = Mathf.Clamp(safeSize.x - 48f, 380f, 592f);
                float briefH = Mathf.Clamp(safeSize.y - 32f, 396f, Mathf.Max(412f, 288f + gotItSize.y));
                _briefingPanelRect.sizeDelta = new Vector2(briefW, briefH);
            }

            if (_victoryCardRect != null)
            {
                float vicW = Mathf.Clamp(safeSize.x - 48f, 420f, 600f);
                Vector2 vicBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2((vicW - 64f) * 0.5f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
                float vicH = Mathf.Clamp(safeSize.y - 28f, 480f, Mathf.Max(528f, 432f + vicBtnSize.y));
                _victoryCardRect.sizeDelta = new Vector2(vicW, vicH);

                if (_playAgainBtnRect != null)
                {
                    _playAgainBtnRect.sizeDelta = vicBtnSize;
                    _playAgainBtnRect.anchoredPosition = new Vector2(24f, 16f);
                }
                if (_homeBtnRect != null)
                {
                    _homeBtnRect.sizeDelta = vicBtnSize;
                    _homeBtnRect.anchoredPosition = new Vector2(-24f, 16f);
                }
                if (_victoryRankCardRect != null)
                {
                    float rankBottomY = 16f + vicBtnSize.y + 24f;
                    _victoryRankCardRect.anchoredPosition = new Vector2(0f, rankBottomY);
                    _victoryRankCardRect.sizeDelta = new Vector2(-48f, 132f);
                }
            }
        }

        void PollHardwareBackKey()
        {
            if (!_interactive || !Application.isPlaying) return;
            if (_lastBackFrame == Time.frameCount) return;
            try
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    HandleBack();
            }
            catch (Exception)
            {
                // Safe no-op in headless/test contexts without active InputSystem device backends.
            }
        }

        /// <summary>Dismisses the first-match Agawang Base onboarding BriefingCard and persists BriefingPending = false.</summary>
        public void DismissBriefing()
        {
            AppSession.Current.BriefingPending = false;
            SaveSessionPrefs();
            if (_briefingRoot != null && _briefingRoot.gameObject.activeSelf)
                _briefingRoot.gameObject.SetActive(false);
            if (_sim != null && _sim.Phase == MatchPhaseKind.Intro)
                _sim.SkipIntro();
            SyncOverlayChromeVisibility();
        }

        /// <summary>Android Back / Escape handler closing transient modals first, otherwise toggling the Pause sheet.</summary>
        public bool HandleBack()
        {
            if (Application.isPlaying)
                _lastBackFrame = Time.frameCount;

            if (IsBriefingVisible)
            {
                DismissBriefing();
                Audio?.PlayUiTap("confirm");
                return true;
            }
            if (_lastPhase == MatchPhaseKind.Intro)
            {
                if (_sim != null && _sim.SkipIntro())
                {
                    Audio?.PlayUiTap("confirm");
                    return true;
                }
            }
            if (IsQuickChatOpen)
            {
                SetQuickChatOpen(false);
                Audio?.PlayUiTap("cancel");
                return true;
            }
            if (IsSwapPickerOpen)
            {
                SetSwapPickerOpen(false);
                Audio?.PlayUiTap("cancel");
                return true;
            }
            if (IsPaused && IsLeaveConfirmOpen)
            {
                SetPauseLeaveConfirmOpen(false);
                Audio?.PlayUiTap("cancel");
                return true;
            }
            if (_lastPhase == MatchPhaseKind.MatchOver)
            {
                Audio?.PlayUiTap("back");
                OnBackToHome?.Invoke();
                return true;
            }

            SetPaused(!IsPaused);
            return true;
        }

        void SetPauseLeaveConfirmOpen(bool confirmOpen)
        {
            if (_pauseConfirmRow != null)
                _pauseConfirmRow.gameObject.SetActive(confirmOpen);
            if (_pauseFooterRow != null)
                _pauseFooterRow.gameObject.SetActive(!confirmOpen);
        }

        public void SetPaused(bool paused)
        {
            if (paused && _lastPhase == MatchPhaseKind.MatchOver)
                return;
            if (IsPaused == paused)
                return;

            IsPaused = paused;
            if (paused)
            {
                SetQuickChatOpen(false);
                SetSwapPickerOpen(false);
                SyncQuickSettingsLabels();
            }
            SetPauseLeaveConfirmOpen(false);
            if (_pauseSheetRoot != null)
                _pauseSheetRoot.gameObject.SetActive(paused);
            if (paused)
                UpdatePauseScrollIndicator();

            SyncOverlayChromeVisibility();
            OnPauseChanged?.Invoke(paused);
        }

        public void SetQuickChatOpen(bool open)
        {
            if (open && (_lastPhase == MatchPhaseKind.MatchOver || IsPaused || IsSwapPickerOpen))
                return;
            if (IsQuickChatOpen == open)
                return;
            IsQuickChatOpen = open;
            if (_quickChatModalRoot != null)
                _quickChatModalRoot.gameObject.SetActive(open);
            SyncOverlayChromeVisibility();
        }

        public void TriggerQuickChat(int stickerIndex)
        {
            if (stickerIndex < 0 || stickerIndex >= StickerPhrases.Length) return;
            SetQuickChatOpen(false);
            Haptics.LightTap();
            Audio?.PlayUiTap();
            _sim?.SendQuickChat(stickerIndex);
        }

        public void SetSwapPickerOpen(bool open)
        {
            if (open && (_sim == null || !_sim.CanSwapLoadout || _lastPhase == MatchPhaseKind.MatchOver))
                return;
            if (IsSwapPickerOpen == open)
                return;

            IsSwapPickerOpen = open;
            if (open && _sim != null)
            {
                _swapPicks.Clear();
                KidSnapshot? lk = _sim.LocalKid;
                if (lk.HasValue && lk.Value.HandWeaponIds != null)
                {
                    for (int i = 0; i < lk.Value.HandCount && i < lk.Value.HandWeaponIds.Length && _swapPicks.Count < 3; i++)
                    {
                        string wid = lk.Value.HandWeaponIds[i];
                        if (!string.IsNullOrEmpty(wid) && !_swapPicks.Contains(wid))
                            _swapPicks.Add(wid);
                    }
                }
                if (_swapPicks.Count == 0)
                {
                    IReadOnlyList<string> saved = AppSession.Current.SelectedSuperpowers;
                    for (int i = 0; i < saved.Count && _swapPicks.Count < 3; i++)
                        if (!string.IsNullOrEmpty(saved[i]) && !_swapPicks.Contains(saved[i]))
                            _swapPicks.Add(saved[i]);
                }
                RefreshSwapSelectionVisuals();
            }
            if (_swapModalRoot != null)
                _swapModalRoot.gameObject.SetActive(open);
            SyncOverlayChromeVisibility();
        }

        /// <summary>Applies a superpower loadout swap through SimBridge.TrySwapLoadout and updates AppSession.Current.</summary>
        public bool ConfirmLoadoutSwap(IList<string> weaponIds)
        {
            if (_sim == null || weaponIds == null || weaponIds.Count < 2) return false;
            bool ok = _sim.TrySwapLoadout(weaponIds);
            if (!ok)
            {
                Haptics.CancelTick();
                Audio?.PlayUiTap("deny");
                return false;
            }
            AppSession.Current.SetSuperpowers(weaponIds);
            SaveSessionPrefs();
            Haptics.LightTap();
            Audio?.PlayUiTap("confirm");
            SetSwapPickerOpen(false);
            return true;
        }

        public void PushTag(string taggerName, string victimName, bool allyScored, string detail = "")
        {
            string badge = string.IsNullOrWhiteSpace(detail)
                ? "TAG"
                : (TrimLabel(detail, 10).ToUpperInvariant() + " TAG");
            PushTakedownEntry(new TakedownEntry
            {
                LeftName = TrimLabel(taggerName, 12),
                Badge = badge,
                RightName = TrimLabel(victimName, 12),
                Friendly = allyScored,
                IsRescue = false,
                Age = 0f,
            });
        }

        public void PushRescue(string rescuerName, string savedName, bool allySide)
        {
            PushTakedownEntry(new TakedownEntry
            {
                LeftName = TrimLabel(rescuerName, 12),
                Badge = "SAVED",
                RightName = TrimLabel(savedName, 12),
                Friendly = allySide,
                IsRescue = true,
                Age = 0f,
            });
        }

        public void ClearTakedowns()
        {
            _takedownEntries.Clear();
            _takedownDirty = true;
            for (int i = 0; i < _takedownRows.Length; i++)
                if (_takedownRows[i].Root != null)
                    _takedownRows[i].Root.gameObject.SetActive(false);
        }

        static void AttachDismissCatcher(Transform modalRoot, bool interactive, Action onDismiss)
        {
            RectTransform catcherRt = Ui.Fill("DismissCatcher", modalRoot);
            Image catcherImg = Ui.MakeImage("HitSurface", catcherRt, Color.clear, round: false, raycast: interactive);
            Stretch(catcherImg.rectTransform, 0f);
            if (interactive && onDismiss != null)
            {
                var catcher = catcherImg.gameObject.AddComponent<DismissTapCatcher>();
                catcher.OnTap = onDismiss;
            }
        }

        // =============================================================================================================================
        // 1. Local Hero Vitals & 5v5 Squad Wings
        // =============================================================================================================================

        void BuildHeroVitals(
            out RectTransform root,
            out Image ring,
            out RawImage face,
            out Text initial,
            out RectTransform hpFill,
            out Image hpFillImg,
            out RectTransform trailFill,
            out RectTransform missingFill,
            out Image missingImg,
            out Text hpText,
            out RectTransform stamFill,
            out Image stamImg,
            out Text statusText,
            out GameObject buffBadge,
            out Image buffDot,
            out Text buffText)
        {
            root = Ui.Panel("HeroVitalsChip", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -80f), new Vector2(214f, 74f));
            Image card = KomiksUi.MakeInkPanel("Plate", root, ColCardDark, KomiksUi.InkOutline, 12, 2, raycast: false);
            Stretch(card.rectTransform, 0f);

            RectTransform avatarBox = Ui.Panel("Avatar", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(44f, 44f));
            ring = Ui.MakeImage("Ring", avatarBox, OwnTeamColor(), round: true);
            Stretch(ring.rectTransform, 0f);
            Image innerBg = Ui.MakeImage("Inner", avatarBox, ColInk, round: true);
            Stretch(innerBg.rectTransform, 3f);

            var faceGo = new GameObject("Face", typeof(RectTransform), typeof(RawImage));
            faceGo.transform.SetParent(avatarBox, false);
            face = faceGo.GetComponent<RawImage>();
            face.raycastTarget = false;
            Stretch(face.rectTransform, 4f);
            face.gameObject.SetActive(false);

            initial = Ui.MakeText("Initial", avatarBox, "J", 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(initial.rectTransform, 0f);

            // Disjoint Numeric HP (left) + Status tag (right) so "OVERCLOCK" never collides with "100 / 100"
            hpText = Ui.MakeText("HpText", root, "100 / 100", 16, ColHpWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            hpText.resizeTextForBestFit = true;
            hpText.resizeTextMinSize = 12;
            hpText.resizeTextMaxSize = 16;
            RectTransform hpTextRt = hpText.rectTransform;
            hpTextRt.anchorMin = hpTextRt.anchorMax = new Vector2(0f, 1f);
            hpTextRt.pivot = new Vector2(0f, 1f);
            hpTextRt.anchoredPosition = new Vector2(58f, -5f);
            hpTextRt.sizeDelta = new Vector2(74f, 22f);

            statusText = Ui.MakeText("StatusTag", root, "", 13, ColGold, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.resizeTextForBestFit = true;
            statusText.resizeTextMinSize = 10;
            statusText.resizeTextMaxSize = 13;
            RectTransform stRt = statusText.rectTransform;
            stRt.anchorMin = new Vector2(0f, 1f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.offsetMin = new Vector2(136f, -27f);
            stRt.offsetMax = new Vector2(-8f, -5f);

            // HP bar with missing-coral + hit-trail + live white/mint fill
            RectTransform hpTrack = Ui.Panel("HpBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(25f, -29f), new Vector2(-66f, 13f));
            Image hpBg = MakeSlicedImage("Track", hpTrack, ColInk, pill: true);
            Stretch(hpBg.rectTransform, 0f);

            missingImg = Ui.MakeImage("Missing", hpTrack, ColMissingCoral);
            missingFill = missingImg.rectTransform;
            missingFill.anchorMin = new Vector2(1f, 0f);
            missingFill.anchorMax = Vector2.one;
            missingFill.sizeDelta = Vector2.zero;

            Image trailImg = Ui.MakeImage("Trail", hpTrack, ColTrailAmber);
            trailFill = trailImg.rectTransform;
            trailFill.anchorMin = new Vector2(1f, 0f);
            trailFill.anchorMax = Vector2.one;
            trailFill.sizeDelta = Vector2.zero;

            hpFillImg = Ui.MakeImage("LiveHp", hpTrack, ColHpWhite);
            hpFill = hpFillImg.rectTransform;
            hpFill.anchorMin = Vector2.zero;
            hpFill.anchorMax = Vector2.one;
            hpFill.sizeDelta = Vector2.zero;

            // Gapless Stamina / Overclock bar directly below HP
            RectTransform stamTrack = Ui.Panel("StaminaBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(25f, -45f), new Vector2(-66f, 7f));
            Image stamBg = Ui.MakeImage("StamTrack", stamTrack, ColInk);
            Stretch(stamBg.rectTransform, 0f);
            stamImg = Ui.MakeImage("StamFill", stamTrack, ColStamina);
            stamFill = stamImg.rectTransform;
            stamFill.anchorMin = Vector2.zero;
            stamFill.anchorMax = Vector2.one;
            stamFill.sizeDelta = Vector2.zero;

            // Active Merienda Buff badge (Palamig, Teks, Turon)
            RectTransform buffRect = Ui.Panel("MeriendaBuff", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(25f, 4f), new Vector2(-66f, 17f));
            buffBadge = buffRect.gameObject;
            Image buffPlate = MakeSlicedImage("BuffPlate", buffRect, new Color(0.04f, 0.06f, 0.10f, 0.84f), pill: true);
            Stretch(buffPlate.rectTransform, 0f);
            buffDot = Ui.MakeImage("BuffDot", buffRect, ColPalamig, round: true);
            buffDot.rectTransform.anchorMin = buffDot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            buffDot.rectTransform.pivot = new Vector2(0f, 0.5f);
            buffDot.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            buffDot.rectTransform.sizeDelta = new Vector2(10f, 10f);
            buffText = Ui.MakeText("BuffLabel", buffRect, "PALAMIG 8s", 12, ColPalamig, TextAnchor.MiddleLeft);
            buffText.rectTransform.anchorMin = Vector2.zero;
            buffText.rectTransform.anchorMax = Vector2.one;
            buffText.rectTransform.offsetMin = new Vector2(19f, 0f);
            buffText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            buffBadge.SetActive(false);
        }

        void SyncHeroVitals(SimBridge sim, KidSnapshot? localKid, float dt)
        {
            if (_heroVitalsRoot == null) return;
            _hasLocalKid = localKid.HasValue;
            if (!localKid.HasValue)
            {
                _heroVitalsRoot.gameObject.SetActive(false);
                return;
            }

            KidSnapshot kid = localKid.Value;
            _heroRing.color = OwnTeamColor();
            if (!string.Equals(_cachedHeroChar, kid.CharacterId, StringComparison.Ordinal))
            {
                _cachedHeroChar = kid.CharacterId ?? "";
                ApplyAvatarArt(_cachedHeroChar, _heroFace, _heroInitial);
            }

            int maxHp = Mathf.Max(sim.MaxHp, 1);
            int curHp = kid.Tagged ? 0 : Mathf.Clamp(kid.Hp, 0, maxHp);
            float liveShare = Mathf.Clamp01((float)curHp / maxHp);

            if (!_vitalsSynced)
            {
                _vitalsSynced = true;
                _trailShare = liveShare;
                _trailHoldLeft = 0f;
            }
            else if (SettingsStore.Current.ReduceMotion)
            {
                _trailShare = liveShare;
                _trailHoldLeft = 0f;
            }
            else
            {
                if (curHp < _lastHp)
                    _trailHoldLeft = DamageTrailHoldSeconds;
                else if (liveShare > _trailShare)
                    _trailShare = liveShare;

                if (dt > 0f)
                {
                    if (_trailHoldLeft > 0f)
                        _trailHoldLeft = Mathf.Max(0f, _trailHoldLeft - dt);
                    else if (_trailShare > liveShare)
                        _trailShare = Mathf.Max(liveShare, _trailShare - DamageTrailDecayPerSec * dt);
                }
            }

            if (curHp != _lastHp || maxHp != _lastMaxHp)
            {
                _lastHp = curHp;
                _lastMaxHp = maxHp;
                _heroHpText.text = curHp.ToString(CultureInfo.InvariantCulture) + " / " + maxHp.ToString(CultureInfo.InvariantCulture);
            }

            bool regen = kid.ActiveBuff == BuffKind.Turon && kid.BuffLeft > 0.01f && !kid.Tagged;
            _heroHpFillImg.color = regen ? ColRegenMint : ColHpWhite;
            _heroMissingImg.color = SettingsStore.Current.IsColorblind ? EnemyTeamColor() : ColMissingCoral;

            _heroHpFill.anchorMax = new Vector2(liveShare, 1f);
            _heroTrailFill.anchorMin = new Vector2(liveShare, 0f);
            _heroTrailFill.anchorMax = new Vector2(Mathf.Clamp(_trailShare, liveShare, 1f), 1f);
            _heroMissingFill.anchorMin = new Vector2(Mathf.Clamp(_trailShare, liveShare, 1f), 0f);
            _heroMissingFill.anchorMax = Vector2.one;

            float maxStam = Mathf.Max(kid.MaxStamina > 0f ? kid.MaxStamina : sim.MaxStamina, 1f);
            float stamShare = Mathf.Clamp01(kid.Stamina / maxStam);
            _heroStamFill.anchorMax = new Vector2(stamShare, 1f);
            _heroStamImg.color = kid.Overclocked
                ? ColOverclock
                : (kid.ExhaustLeft > 0.01f ? ColStatusAlert : ColStamina);

            string statusWord = ResolveHeroStatusWord(kid, out Color statusColor);
            if (!string.Equals(_lastHeroStatus, statusWord, StringComparison.Ordinal))
            {
                _lastHeroStatus = statusWord;
                _heroStatusText.text = statusWord;
            }
            _heroStatusText.color = statusColor;

            BuffKind activeBuff = kid.BuffLeft > 0.01f ? kid.ActiveBuff : BuffKind.None;
            bool showBuff = activeBuff != BuffKind.None;
            if (_heroBuffBadge.activeSelf != showBuff)
                _heroBuffBadge.SetActive(showBuff);
            if (showBuff)
            {
                int sec = Mathf.Max(1, Mathf.CeilToInt(kid.BuffLeft));
                if (activeBuff != _lastBuffKind || sec != _lastBuffCeilSec)
                {
                    _lastBuffKind = activeBuff;
                    _lastBuffCeilSec = sec;
                    Color bc = BuffColorOf(activeBuff);
                    _heroBuffDot.color = bc;
                    _heroBuffText.color = bc;
                    _heroBuffText.text = BuffDisplayName(activeBuff) + " " + sec.ToString(CultureInfo.InvariantCulture) + "s";
                }
            }
            else
            {
                _lastBuffKind = BuffKind.None;
                _lastBuffCeilSec = -1;
            }
        }

        static string ResolveHeroStatusWord(KidSnapshot kid, out Color color)
        {
            if (kid.Tagged) { color = ColStatusAlert; return "OUT"; }
            if (kid.Overclocked) { color = ColOverclock; return "OVERCLOCK"; }
            if (kid.StatusKind == StatusKind.Stun || kid.StatusKind == StatusKind.Knockout || kid.StatusKind == StatusKind.Polymorph)
            {
                color = ColTeks;
                return "STUN";
            }
            if (kid.StatusKind == StatusKind.Tripped) { color = ColStatusAlert; return "TRIPPED"; }
            if (kid.ExhaustLeft > 0.01f) { color = ColStatusAlert; return "EXHAUST"; }
            if (kid.Dashing) { color = ColPalamig; return "VAULT"; }
            if (kid.Sliding) { color = ColPalamig; return "SLIDE"; }
            if (kid.Crouching) { color = Ui.Cream; return "LOW"; }
            color = Ui.Cream;
            return "";
        }

        void BuildSquadWings(out RectTransform container, out RectTransform allyWing, out RectTransform enemyWing)
        {
            container = Ui.Panel("SquadWings", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(760f, 48f));
            allyWing = Ui.Panel("AllySquadWing", container, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 48f));
            enemyWing = Ui.Panel("EnemySquadWing", container, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(240f, 48f));

            for (int i = 0; i < SquadWingSlots; i++)
            {
                _allySlots[i] = CreateWingSlot(allyWing, "AllySlot_" + i, includeHpBar: true);
                // Binding GDD rule: enemy HP is NEVER created or displayed during play!
                _enemySlots[i] = CreateWingSlot(enemyWing, "EnemySlot_" + i, includeHpBar: false);
            }
        }

        static SquadSlotUi CreateWingSlot(Transform parent, string name, bool includeHpBar)
        {
            RectTransform slot = Ui.Panel(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(36f, 46f));

            RectTransform disc = Ui.Panel("Portrait", slot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(34f, 34f));
            Image ring = Ui.MakeImage("Ring", disc, Color.white, round: true);
            Stretch(ring.rectTransform, 0f);
            Image bg = Ui.MakeImage("Bg", disc, ColInk, round: true);
            Stretch(bg.rectTransform, 2.5f);

            var faceGo = new GameObject("Face", typeof(RectTransform), typeof(RawImage));
            faceGo.transform.SetParent(disc, false);
            var face = faceGo.GetComponent<RawImage>();
            face.raycastTarget = false;
            Stretch(face.rectTransform, 3f);
            face.gameObject.SetActive(false);

            Text initial = Ui.MakeText("Initial", disc, "K", 15, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(initial.rectTransform, 0f);

            GameObject crossObj = Ui.Panel("TaggedCross", disc, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
            Text crossTxt = Ui.MakeText("X", crossObj.transform, "X", 20, ColStatusAlert, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(crossTxt.rectTransform, 0f);
            crossObj.SetActive(false);

            RectTransform ultRect = Ui.Panel("UltPip", disc, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(11f, 11f));
            Image ultImg = Ui.MakeImage("Star", ultRect, ColOverclock, round: true);
            Stretch(ultImg.rectTransform, 0f);
            ultRect.gameObject.SetActive(false);

            Text youTag = null;
            Image hpTrack = null;
            RectTransform hpFill = null;
            Image hpFillImg = null;

            if (includeHpBar)
            {
                youTag = Ui.MakeText("YouTag", disc, "YOU", 9, ColHpWhite, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
                youTag.rectTransform.anchorMin = new Vector2(0f, 0f);
                youTag.rectTransform.anchorMax = new Vector2(1f, 0f);
                youTag.rectTransform.pivot = new Vector2(0.5f, 0f);
                youTag.rectTransform.anchoredPosition = new Vector2(0f, -1f);
                youTag.rectTransform.sizeDelta = new Vector2(0f, 11f);
                youTag.gameObject.SetActive(false);

                RectTransform barBox = Ui.Panel("AllyHp", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(-2f, 5f));
                hpTrack = Ui.MakeImage("Track", barBox, ColInk);
                Stretch(hpTrack.rectTransform, 0f);
                hpFillImg = Ui.MakeImage("Fill", barBox, ColRegenMint);
                hpFill = hpFillImg.rectTransform;
                hpFill.anchorMin = Vector2.zero;
                hpFill.anchorMax = Vector2.one;
                hpFill.sizeDelta = Vector2.zero;
            }

            return new SquadSlotUi
            {
                Root = slot,
                Ring = ring,
                FallbackBg = bg,
                Face = face,
                Initial = initial,
                YouTag = youTag,
                HpTrack = hpTrack,
                HpFill = hpFill,
                HpFillImage = hpFillImg,
                TaggedCross = crossObj,
                UltPip = ultRect.gameObject,
                CachedId = -1,
                CachedChar = "",
            };
        }

        static void LayoutWingSlots(SquadSlotUi[] slots, float wingWidth, bool rightAlign)
        {
            const float gap = 5f;
            float slotW = Mathf.Clamp((wingWidth - gap * (SquadWingSlots - 1)) / SquadWingSlots, 28f, 38f);
            float totalUsed = slotW * SquadWingSlots + gap * (SquadWingSlots - 1);
            float startX = rightAlign ? Mathf.Max(0f, wingWidth - totalUsed) : 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Root == null) continue;
                slots[i].Root.sizeDelta = new Vector2(slotW, 46f);
                slots[i].Root.anchoredPosition = new Vector2(startX + i * (slotW + gap), 0f);
            }
        }

        void SyncSquadWings(SimBridge sim, int localId, int localTeam)
        {
            _allyScratch.Clear();
            _enemyScratch.Clear();

            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            for (int i = 0; i < kids.Count; i++)
            {
                KidSnapshot k = kids[i];
                if (k.Team == localTeam)
                {
                    if (k.Id == localId) _allyScratch.Insert(0, k);
                    else if (_allyScratch.Count < SquadWingSlots) _allyScratch.Add(k);
                }
                else if (_enemyScratch.Count < SquadWingSlots)
                {
                    _enemyScratch.Add(k);
                }
            }

            int maxHp = Mathf.Max(sim.MaxHp, 1);
            Color allyCol = OwnTeamColor();
            Color enemyCol = EnemyTeamColor();

            for (int i = 0; i < SquadWingSlots; i++)
            {
                SyncOneWingSlot(ref _allySlots[i], i < _allyScratch.Count ? (KidSnapshot?)_allyScratch[i] : null, isAlly: true, localId, maxHp, allyCol);
                SyncOneWingSlot(ref _enemySlots[i], i < _enemyScratch.Count ? (KidSnapshot?)_enemyScratch[i] : null, isAlly: false, localId, maxHp, enemyCol);
            }
        }

        static void SyncOneWingSlot(ref SquadSlotUi ui, KidSnapshot? snap, bool isAlly, int localId, int maxHp, Color teamColor)
        {
            if (ui.Root == null) return;
            bool active = snap.HasValue;
            if (ui.Root.gameObject.activeSelf != active)
                ui.Root.gameObject.SetActive(active);
            if (!active) return;

            KidSnapshot k = snap.Value;
            if (ui.CachedId != k.Id || !string.Equals(ui.CachedChar, k.CharacterId, StringComparison.Ordinal))
            {
                ui.CachedId = k.Id;
                ui.CachedChar = k.CharacterId ?? "";
                ApplyAvatarArt(ui.CachedChar, ui.Face, ui.Initial);
            }

            bool isMe = isAlly && k.Id == localId;
            bool isOut = k.Tagged;

            ui.Ring.color = isOut
                ? new Color(1f, 1f, 1f, 0.30f)
                : (isMe ? ColHpWhite : teamColor);
            if (ui.Face.gameObject.activeSelf)
                ui.Face.color = isOut ? new Color(0.45f, 0.48f, 0.54f, 0.55f) : Color.white;
            ui.Initial.color = isOut ? new Color(0.82f, 0.85f, 0.90f, 0.72f) : Color.white;

            if (ui.TaggedCross.activeSelf != isOut)
                ui.TaggedCross.SetActive(isOut);

            bool showUlt = isAlly && !isOut && k.UltCharge >= 99.9f;
            if (ui.UltPip.activeSelf != showUlt)
                ui.UltPip.SetActive(showUlt);

            if (isAlly && ui.HpFill != null)
            {
                if (ui.YouTag != null && ui.YouTag.gameObject.activeSelf != isMe)
                    ui.YouTag.gameObject.SetActive(isMe);

                float share = isOut ? 0f : Mathf.Clamp01((float)k.Hp / maxHp);
                ui.HpFill.anchorMax = new Vector2(share, 1f);
                if (SettingsStore.Current.IsColorblind)
                    ui.HpFillImage.color = teamColor;
                else
                    ui.HpFillImage.color = share <= 0.30f ? ColStatusAlert : (share <= 0.60f ? ColOverclock : ColRegenMint);
            }
        }

        // =============================================================================================================================
        // 2. TakedownFeed (Top-Right 3 Recent Tags) & CaptureAlert (0.4s Hysteresis Anti-Flicker)
        // =============================================================================================================================

        void BuildTakedownFeed(out RectTransform root)
        {
            const float rowH = 30f;
            const float gap = 5f;
            float totalH = MaxTakedownRows * rowH + (MaxTakedownRows - 1) * gap;
            root = Ui.Panel("TakedownFeed", Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(264f, totalH));

            for (int i = 0; i < MaxTakedownRows; i++)
            {
                RectTransform row = Ui.Panel("Row_" + i, root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * (rowH + gap)), new Vector2(0f, rowH));
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Image bg = KomiksUi.MakeInkPanel("Pill", row, new Color(0.05f, 0.08f, 0.14f, 0.88f), KomiksUi.InkOutline, 12, 2, raycast: false);
                Stretch(bg.rectTransform, 0f);

                Image accent = Ui.MakeImage("Accent", row, KillFeedOwnColor());
                accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                accent.rectTransform.anchoredPosition = new Vector2(5f, 0f);
                accent.rectTransform.sizeDelta = new Vector2(4f, -10f);

                Text left = Ui.MakeText("Left", row, "", 14, KillFeedOwnColor(), TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
                left.resizeTextForBestFit = true;
                left.resizeTextMinSize = 11;
                left.resizeTextMaxSize = 15;
                left.rectTransform.anchorMin = new Vector2(0f, 0f);
                left.rectTransform.anchorMax = new Vector2(0.39f, 1f);
                left.rectTransform.offsetMin = new Vector2(14f, 0f);
                left.rectTransform.offsetMax = Vector2.zero;

                Text badge = Ui.MakeText("Badge", row, "TAG", 12, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badge.resizeTextForBestFit = true;
                badge.resizeTextMinSize = 10;
                badge.resizeTextMaxSize = 12;
                badge.rectTransform.anchorMin = new Vector2(0.39f, 0f);
                badge.rectTransform.anchorMax = new Vector2(0.63f, 1f);
                badge.rectTransform.sizeDelta = Vector2.zero;

                Text right = Ui.MakeText("Right", row, "", 14, KillFeedEnemyColor(), TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
                right.resizeTextForBestFit = true;
                right.resizeTextMinSize = 11;
                right.resizeTextMaxSize = 15;
                right.rectTransform.anchorMin = new Vector2(0.63f, 0f);
                right.rectTransform.anchorMax = new Vector2(1f, 1f);
                right.rectTransform.offsetMin = Vector2.zero;
                right.rectTransform.offsetMax = new Vector2(-12f, 0f);

                row.gameObject.SetActive(false);
                _takedownRows[i] = new TakedownRowUi
                {
                    Root = row,
                    Group = group,
                    Accent = accent,
                    LeftText = left,
                    BadgeText = badge,
                    RightText = right,
                };
            }
        }

        void OnSimTagged(int victimId, int taggerId)
        {
            if (_sim == null) return;
            if (victimId == _sim.LocalPlayerId)
                TriggerHurtFlash(HurtTaggedAlpha, HurtTaggedSeconds);

            int localTeam = _sim.LocalPlayerTeam;
            KidSnapshot? victim = _sim.Find(victimId);
            KidSnapshot? tagger = _sim.Find(taggerId);
            bool allyScored = tagger.HasValue ? tagger.Value.Team == localTeam : (victim.HasValue && victim.Value.Team != localTeam);
            string taggerName = DisplayKidName(_sim, taggerId, tagger);
            string victimName = DisplayKidName(_sim, victimId, victim);
            PushTag(taggerName, victimName, allyScored);
        }

        void OnSimPlayerDamaged(int playerId, int amount)
        {
            if (_sim == null || amount <= 0 || playerId != _sim.LocalPlayerId) return;
            float share = Mathf.Clamp01((float)amount / Mathf.Max(_sim.MaxHp, 1));
            float peak = Mathf.Lerp(HurtFlashMinAlpha, HurtFlashMaxAlpha, Mathf.Clamp01(share / 0.30f));
            TriggerHurtFlash(peak, HurtFlashSeconds);
        }

        public void TriggerHurtFlash(float peak, float seconds)
        {
            bool reduce = SettingsStore.Current.ReduceMotion;
            float effPeak = reduce ? peak * 0.5f : peak;
            float effSpan = Mathf.Max(reduce ? Mathf.Min(seconds, 0.6f) : seconds, 0.05f);
            if (effPeak >= _hurtCurrentAlpha)
            {
                _hurtFlashPeak = effPeak;
                _hurtFlashSpan = effSpan;
                _hurtFlashLeft = effSpan;
                _hurtCurrentAlpha = effPeak;
                ApplyHurtPulseVisual(_hurtCurrentAlpha);
            }
        }

        void OnSimRescued(int savedId, int rescuerId)
        {
            if (_sim == null) return;
            int localTeam = _sim.LocalPlayerTeam;
            KidSnapshot? saved = _sim.Find(savedId);
            KidSnapshot? rescuer = _sim.Find(rescuerId);
            bool allySide = saved.HasValue && saved.Value.Team == localTeam;
            PushRescue(DisplayKidName(_sim, rescuerId, rescuer), DisplayKidName(_sim, savedId, saved), allySide);
        }

        void PushTakedownEntry(TakedownEntry entry)
        {
            _takedownEntries.Insert(0, entry);
            while (_takedownEntries.Count > MaxTakedownRows)
                _takedownEntries.RemoveAt(_takedownEntries.Count - 1);
            _takedownDirty = true;
            RenderTakedownRows(contentChanged: true);
        }

        void TickTakedownFeed(float dt)
        {
            if (_takedownEntries.Count == 0 && !_takedownDirty) return;

            bool removedAny = false;
            if (dt > 0f)
            {
                for (int i = _takedownEntries.Count - 1; i >= 0; i--)
                {
                    TakedownEntry e = _takedownEntries[i];
                    e.Age += dt;
                    if (e.Age >= TakedownLifetimeSeconds)
                    {
                        _takedownEntries.RemoveAt(i);
                        removedAny = true;
                    }
                    else
                    {
                        _takedownEntries[i] = e;
                    }
                }
            }

            RenderTakedownRows(contentChanged: _takedownDirty || removedAny);
        }

        void RenderTakedownRows(bool contentChanged)
        {
            const float rowH = 30f;
            const float gap = 5f;
            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            Color ally = contentChanged ? KillFeedOwnColor() : default;
            Color enemy = contentChanged ? KillFeedEnemyColor() : default;

            for (int i = 0; i < MaxTakedownRows; i++)
            {
                TakedownRowUi row = _takedownRows[i];
                if (row.Root == null) continue;
                bool active = i < _takedownEntries.Count;
                if (row.Root.gameObject.activeSelf != active)
                    row.Root.gameObject.SetActive(active);
                if (!active) continue;

                TakedownEntry e = _takedownEntries[i];
                float rem = TakedownLifetimeSeconds - e.Age;
                float alpha = rem < TakedownFadeSeconds ? Mathf.Clamp01(rem / TakedownFadeSeconds) : 1f;
                float slideX = (!reduceMotion && e.Age < TakedownEnterSeconds)
                    ? (1f - e.Age / TakedownEnterSeconds) * 18f
                    : 0f;

                row.Group.alpha = alpha;
                row.Root.anchoredPosition = new Vector2(slideX, -i * (rowH + gap));

                if (contentChanged)
                {
                    Color leftCol = e.Friendly ? ally : enemy;
                    Color rightCol = e.IsRescue ? leftCol : (e.Friendly ? enemy : ally);
                    row.Accent.color = leftCol;
                    row.LeftText.color = leftCol;
                    row.LeftText.text = e.LeftName;
                    row.BadgeText.color = e.IsRescue ? ColRegenMint : ColGold;
                    row.BadgeText.text = e.Badge;
                    row.RightText.color = rightCol;
                    row.RightText.text = e.RightName;
                }
            }

            if (contentChanged)
                _takedownDirty = false;
        }

        void BuildCaptureAlert(
            out RectTransform root,
            out RectTransform iconRect,
            out Image plateImg,
            out RectTransform progFill)
        {
            // High above the 45%x50% center sightbox (y in [-162..-102] on 720p height-locked canvas).
            root = Ui.Panel("CaptureAlert", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(392f, 62f));
            plateImg = KomiksUi.MakeInkPanel("Bar", root, new Color(0.52f, 0.13f, 0.12f, 0.95f), KomiksUi.InkOutline, 14, 2, raycast: false);
            Stretch(plateImg.rectTransform, 0f);

            iconRect = Ui.Panel("WarningIcon", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 2f), new Vector2(44f, 40f));
            Image tri = Ui.MakeImage("Triangle", iconRect, Color.white);
            tri.sprite = WarningTriangleSprite();
            Stretch(tri.rectTransform, 0f);

            Text small = Ui.MakeText("Small", root, "Enemy capturing base", 14, Color.white, TextAnchor.LowerLeft);
            small.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            small.rectTransform.anchorMax = new Vector2(1f, 1f);
            small.rectTransform.offsetMin = new Vector2(64f, 0f);
            small.rectTransform.offsetMax = new Vector2(-12f, -4f);

            Text big = Ui.MakeText("Big", root, "DEFEND THE BASE!", 22, ColGold, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
            big.rectTransform.anchorMin = new Vector2(0f, 0f);
            big.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            big.rectTransform.offsetMin = new Vector2(64f, 8f);
            big.rectTransform.offsetMax = new Vector2(-12f, 0f);

            RectTransform barTrack = Ui.Panel("CaptureProgress", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(-16f, 5f));
            Image trackImg = Ui.MakeImage("Track", barTrack, ColInk);
            Stretch(trackImg.rectTransform, 0f);
            Image fillImg = Ui.MakeImage("Fill", barTrack, ColGold);
            progFill = fillImg.rectTransform;
            progFill.anchorMin = Vector2.zero;
            progFill.anchorMax = new Vector2(0f, 1f);
            progFill.sizeDelta = Vector2.zero;

            root.gameObject.SetActive(false);
        }

        void SyncCaptureAlert(SimBridge sim, int localTeam, float dt)
        {
            if (_captureAlertRoot == null) return;
            int enemyTeam = 1 - localTeam;
            bool playing = sim.Phase == MatchPhaseKind.Playing;

            bool rawCapturing = false;
            float progress01 = 0f;
            if (playing && sim.Bases != null && localTeam >= 0 && localTeam < sim.Bases.Count)
            {
                BaseObjectiveSnapshot ownBase = sim.Bases[localTeam];
                rawCapturing = ownBase.Holders > 0 || ownBase.CapturingTeam == enemyTeam ||
                               (sim.CaptureTeam == enemyTeam && sim.CaptureReason == CaptureReasonKind.Base);
                progress01 = ownBase.CaptureProgress01 > 0f
                    ? ownBase.CaptureProgress01
                    : (sim.CaptureTeam == enemyTeam ? sim.CaptureProgress01 : 0f);
            }

            if (!playing)
            {
                _captureHoldLeft = 0f;
                _wasRawCapturing = false;
            }
            else if (rawCapturing)
            {
                if (!_wasRawCapturing && _captureHoldLeft <= 0f)
                    Audio?.PlayObjectiveEvent("contested");
                _captureHoldLeft = CaptureHysteresisSeconds;
                _wasRawCapturing = true;
            }
            else
            {
                _wasRawCapturing = false;
                if (_captureHoldLeft > 0f)
                    _captureHoldLeft = Mathf.Max(0f, _captureHoldLeft - dt);
            }

            _rawCaptureAlertActive = playing && (rawCapturing || _captureHoldLeft > 0f);
            if (!_rawCaptureAlertActive) return;

            int cbMode = SettingsStore.Current.ColorblindMode;
            if (cbMode != _lastCaptureColorblind)
            {
                _lastCaptureColorblind = cbMode;
                Color baseEnemy = EnemyTeamColor();
                var alertFill = new Color(baseEnemy.r * 0.55f, baseEnemy.g * 0.22f, baseEnemy.b * 0.22f, 0.95f);
                _capturePlateImg.sprite = KomiksUi.BorderedSprite(14, 2, alertFill, KomiksUi.InkOutline);
                _capturePlateImg.color = Color.white;
            }
            _captureProgFill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);

            if (SettingsStore.Current.ReduceMotion)
            {
                _captureIconRect.localScale = Vector3.one;
            }
            else
            {
                _capturePulseTime += dt;
                float pulse = 1f + 0.04f * (1f + Mathf.Sin(_capturePulseTime * Mathf.PI * 2f * 1.6f));
                _captureIconRect.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        // =============================================================================================================================
        // 3. Tagged Sidewalk Overlay & 3-Superpower Loadout Swap Picker
        // =============================================================================================================================

        void BuildTaggedOverlay(
            out RectTransform root,
            out Text titleText,
            out Text timerText,
            out Text hintText,
            out RectTransform progFill,
            out Image progImg,
            out Button swapBtn,
            out RectTransform swapRect)
        {
            root = Ui.Panel("TaggedSidewalkOverlay", Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(660f, 148f));
            Image plate = KomiksUi.MakeInkPanel("Plate", root, new Color(0.05f, 0.08f, 0.16f, 0.94f), KomiksUi.InkOutline, 16, 2, raycast: false);
            Stretch(plate.rectTransform, 0f);

            // Left column (0..0.63): Title + RescueTimer, RescueBar, Teammate pips, Hint — strictly disjoint from right Swap button!
            RectTransform leftCol = Ui.Panel("LeftCol", root, new Vector2(0f, 0f), new Vector2(0.63f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftCol.offsetMin = new Vector2(18f, 10f);
            leftCol.offsetMax = new Vector2(-8f, -10f);

            titleText = Ui.MakeText("Title", leftCol, "Tagged! Wait for a rescue", 20, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 14;
            titleText.resizeTextMaxSize = 21;
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.66f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = Vector2.zero;
            titleText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            timerText = Ui.MakeText("RescueTimer", leftCol, "SHED READY", 18, ColRegenMint, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            timerText.resizeTextForBestFit = true;
            timerText.resizeTextMinSize = 13;
            timerText.resizeTextMaxSize = 19;
            timerText.rectTransform.anchorMin = new Vector2(0.67f, 1f);
            timerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            timerText.rectTransform.pivot = new Vector2(1f, 1f);
            timerText.rectTransform.anchoredPosition = Vector2.zero;
            timerText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            RectTransform barBox = Ui.Panel("RescueBar", leftCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -33f), new Vector2(0f, 10f));
            Image barBg = MakeSlicedImage("Track", barBox, ColInk, pill: true);
            Stretch(barBg.rectTransform, 0f);
            progImg = Ui.MakeImage("Fill", barBox, ColRegenMint);
            progFill = progImg.rectTransform;
            progFill.anchorMin = Vector2.zero;
            progFill.anchorMax = new Vector2(0f, 1f);
            progFill.sizeDelta = Vector2.zero;

            for (int i = 0; i < _teammatePips.Length; i++)
            {
                RectTransform pip = Ui.Panel("AllyPip_" + i, leftCol, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * 42f, 28f), new Vector2(36f, 36f));
                Image ring = Ui.MakeImage("Ring", pip, OwnTeamColor(), round: true);
                Stretch(ring.rectTransform, 0f);
                Image inner = Ui.MakeImage("Inner", pip, ColInk, round: true);
                Stretch(inner.rectTransform, 2.5f);
                var fg = new GameObject("Face", typeof(RectTransform), typeof(RawImage));
                fg.transform.SetParent(pip, false);
                var face = fg.GetComponent<RawImage>();
                face.raycastTarget = false;
                Stretch(face.rectTransform, 3f);
                face.gameObject.SetActive(false);
                Text lbl = Ui.MakeText("Initial", pip, "A", 14, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, 0f);

                _teammatePips[i] = new TeammatePipUi
                {
                    Root = pip,
                    Ring = ring,
                    Face = face,
                    Label = lbl,
                    CachedChar = "",
                };
            }

            hintText = Ui.MakeText("Hint", leftCol, "A teammate beside you frees you in 5 s!", 14, Ui.Cream, TextAnchor.MiddleLeft);
            hintText.resizeTextForBestFit = true;
            hintText.resizeTextMinSize = 12;
            hintText.resizeTextMaxSize = 15;
            hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            hintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchoredPosition = Vector2.zero;
            hintText.rectTransform.sizeDelta = new Vector2(0f, 24f);

            // Right column (0.65..1.0): >= 48 dp "Swap Superpowers" button vertically centered
            swapBtn = MakeActionButton(
                "ChangeSuperpowersBtn",
                root,
                "Swap Superpowers",
                new Color(0.95f, 0.56f, 0.16f, 1f),
                17,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap();
                    SetSwapPickerOpen(true);
                },
                out swapRect,
                out _,
                out _);
            swapRect.anchorMin = swapRect.anchorMax = new Vector2(1f, 0.5f);
            swapRect.pivot = new Vector2(1f, 0.5f);
            swapRect.anchoredPosition = new Vector2(-16f, 0f);
            swapRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(204f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            root.gameObject.SetActive(false);
        }

        void SyncTaggedOverlay(SimBridge sim, KidSnapshot? localKid, int localTeam)
        {
            if (_taggedRoot == null) return;
            bool isTagged = localKid.HasValue && localKid.Value.Tagged && sim.Phase != MatchPhaseKind.MatchOver;
            if (!isTagged && IsSwapPickerOpen)
                SetSwapPickerOpen(false);

            bool showTray = isTagged && !IsSwapPickerOpen && !IsPaused && !IsBriefingVisible;
            if (_taggedRoot.gameObject.activeSelf != showTray)
                _taggedRoot.gameObject.SetActive(showTray);
            if (!showTray) return;

            KidSnapshot me = localKid.Value;
            bool canSwap = sim.CanSwapLoadout && !me.CardsMode;
            if (_openSwapBtn.gameObject.activeSelf != canSwap)
                _openSwapBtn.gameObject.SetActive(canSwap);

            // Sync teammate pips & check if local kid is next in TagOrder queue
            bool isNextInLine = true;
            int pipIdx = 0;
            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            Color allyCol = OwnTeamColor();
            for (int i = 0; i < kids.Count; i++)
            {
                KidSnapshot other = kids[i];
                if (other.Team != localTeam || other.Id == me.Id) continue;
                if (other.Tagged && other.TagOrder < me.TagOrder)
                    isNextInLine = false;

                if (pipIdx < _teammatePips.Length)
                {
                    ref TeammatePipUi pip = ref _teammatePips[pipIdx++];
                    if (!pip.Root.gameObject.activeSelf) pip.Root.gameObject.SetActive(true);
                    if (!string.Equals(pip.CachedChar, other.CharacterId, StringComparison.Ordinal))
                    {
                        pip.CachedChar = other.CharacterId ?? "";
                        ApplyAvatarArt(pip.CachedChar, pip.Face, pip.Label);
                    }
                    pip.Ring.color = other.Tagged ? ColStatusAlert : (other.InRescueZone ? ColRegenMint : allyCol);
                    if (pip.Face.gameObject.activeSelf)
                        pip.Face.color = other.Tagged ? new Color(0.5f, 0.5f, 0.55f, 0.55f) : Color.white;
                }
            }
            for (int i = pipIdx; i < _teammatePips.Length; i++)
                if (_teammatePips[i].Root.gameObject.activeSelf)
                    _teammatePips[i].Root.gameObject.SetActive(false);

            RescueObjectiveSnapshot rescue = (sim.Rescues != null && localTeam >= 0 && localTeam < sim.Rescues.Count)
                ? sim.Rescues[localTeam]
                : default;

            if (rescue.CooldownLeft > 0.01f)
            {
                int sec = Mathf.Max(1, Mathf.CeilToInt(rescue.CooldownLeft));
                float cdShare = rescue.CooldownTotal > 0.01f ? Mathf.Clamp01(1f - rescue.CooldownLeft / rescue.CooldownTotal) : 0f;
                _taggedProgressFill.anchorMax = new Vector2(cdShare, 1f);
                _taggedProgressImg.color = ColTeks;

                if (_lastTaggedHintCode != 1 || _lastTaggedHintArg != sec)
                {
                    _lastTaggedHintCode = 1;
                    _lastTaggedHintArg = sec;
                    _taggedTimerText.color = ColTeks;
                    _taggedTimerText.text = "COOLDOWN " + sec.ToString(CultureInfo.InvariantCulture) + "s";
                    _taggedHintText.text = "Your rescue area is ready in " + sec.ToString(CultureInfo.InvariantCulture) + " s!";
                }
            }
            else if (rescue.RescuerId >= 0 && rescue.HoldNeeded > 0.01f && isNextInLine)
            {
                float rem = Mathf.Max(0f, rescue.HoldNeeded - rescue.HoldElapsed);
                int tenths = Mathf.CeilToInt(rem * 10f);
                _taggedProgressFill.anchorMax = new Vector2(Mathf.Clamp01(rescue.HoldProgress01), 1f);
                _taggedProgressImg.color = ColRegenMint;

                if (_lastTaggedHintCode != 2 || _lastTaggedTenths != tenths)
                {
                    _lastTaggedHintCode = 2;
                    _lastTaggedTenths = tenths;
                    _taggedTimerText.color = ColRegenMint;
                    _taggedTimerText.text = "FREE IN " + (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture) + "s";
                    _taggedHintText.text = "Hold tight — a teammate is freeing you right now!";
                }
            }
            else
            {
                int holdSec = Mathf.Max(1, Mathf.CeilToInt(rescue.HoldNeeded > 0.01f ? rescue.HoldNeeded : 5f));
                _taggedProgressFill.anchorMax = new Vector2(0f, 1f);
                _taggedProgressImg.color = ColStamina;

                if (_lastTaggedHintCode != 0 || _lastTaggedHintArg != holdSec)
                {
                    _lastTaggedHintCode = 0;
                    _lastTaggedHintArg = holdSec;
                    _taggedTimerText.color = ColStamina;
                    _taggedTimerText.text = "SHED READY";
                    _taggedHintText.text = "A teammate beside you frees you in " + holdSec.ToString(CultureInfo.InvariantCulture) + " s!";
                }
            }
        }

        void BuildSwapPickerModal(
            out RectTransform modalRoot,
            out RectTransform dimRect,
            out RectTransform cardRect,
            out RectTransform titleRect,
            out RectTransform subRect,
            out RectTransform closeRect,
            out RectTransform viewportRect,
            out RectTransform contentRect,
            out Button confirmBtn,
            out RectTransform confirmRect,
            out Image confirmImg,
            out Text confirmText)
        {
            modalRoot = Ui.Fill("SwapLoadoutModal", Root);
            Image dim = Ui.MakeImage("DimBackdrop", modalRoot, new Color(0.02f, 0.04f, 0.09f, 0.84f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            AttachDismissCatcher(modalRoot, _interactive, () =>
            {
                Audio?.PlayUiTap("cancel");
                SetSwapPickerOpen(false);
            });

            cardRect = Ui.Panel("SwapCard", modalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 580f));
            Image cardBg = KomiksUi.MakeInkPanel("Bg", cardRect, ColModalDark, KomiksUi.InkOutline, 18, 2, raycast: _interactive);
            Stretch(cardBg.rectTransform, 0f);

            Text title = Ui.MakeText("Title", cardRect, "SWAP 3 SUPERPOWERS", 26, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 28;
            titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(22f, -52f);
            titleRect.offsetMax = new Vector2(-156f, -14f);

            Text sub = Ui.MakeText("Sub", cardRect, "Takes effect as soon as a teammate rescues you", 15, Ui.Cream, TextAnchor.MiddleLeft);
            sub.resizeTextForBestFit = true;
            sub.resizeTextMinSize = 12;
            sub.resizeTextMaxSize = 16;
            subRect = sub.rectTransform;
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.offsetMin = new Vector2(22f, -84f);
            subRect.offsetMax = new Vector2(-156f, -54f);

            MakeActionButton(
                "CloseSwapBtn",
                cardRect,
                "Close",
                new Color(0.32f, 0.36f, 0.46f, 1f),
                17,
                () =>
                {
                    Audio?.PlayUiTap("cancel");
                    SetSwapPickerOpen(false);
                },
                out closeRect,
                out _,
                out _);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -10f);
            closeRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(118f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            // Scrollable middle viewport (RectMask2D prevents any tile from colliding with header Close or footer Equip buttons)
            viewportRect = Ui.Panel("TilesViewport", cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewportRect.offsetMin = new Vector2(18f, 120f);
            viewportRect.offsetMax = new Vector2(-18f, -116f);
            Image vpHit = Ui.MakeImage("ViewportHit", viewportRect, new Color(0.03f, 0.05f, 0.10f, 0.42f), raycast: _interactive);
            Stretch(vpHit.rectTransform, 0f);
            viewportRect.gameObject.AddComponent<RectMask2D>();

            contentRect = Ui.Panel("TilesContent", viewportRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 380f));
            var scroll = viewportRect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            for (int i = 0; i < MaxSwapWeaponTiles; i++)
            {
                int capturedIdx = i;
                RectTransform tileRt = Ui.Panel("WeaponTile_" + i, contentRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(168f, 96f));
                Image bg = KomiksUi.MakeInkPanel("Bg", tileRt, ColTileUnpicked, KomiksUi.InkOutline, 12, 2, raycast: _interactive);
                Stretch(bg.rectTransform, 0f);

                var btn = tileRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.interactable = _interactive;
                btn.onClick.AddListener(() => OnSwapTilePressed(capturedIdx));

                Image roleStrip = Ui.MakeImage("Role", tileRt, ColStamina);
                roleStrip.rectTransform.anchorMin = new Vector2(0f, 0f);
                roleStrip.rectTransform.anchorMax = new Vector2(0f, 1f);
                roleStrip.rectTransform.pivot = new Vector2(0f, 0.5f);
                roleStrip.rectTransform.anchoredPosition = new Vector2(5f, 0f);
                roleStrip.rectTransform.sizeDelta = new Vector2(4f, -12f);

                RectTransform iconBox = Ui.Panel("IconBox", tileRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(48f, 48f));
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                iconGo.transform.SetParent(iconBox, false);
                var icon = iconGo.GetComponent<RawImage>();
                icon.raycastTarget = false;
                Stretch(icon.rectTransform, 0f);
                var iconFitter = iconGo.GetComponent<AspectRatioFitter>();
                iconFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                iconFitter.aspectRatio = 1f;

                Text wName = Ui.MakeText("Name", tileRt, "", 15, Ui.Cream, TextAnchor.MiddleLeft);
                wName.horizontalOverflow = HorizontalWrapMode.Wrap;
                wName.resizeTextForBestFit = true;
                wName.resizeTextMinSize = 12;
                wName.resizeTextMaxSize = 16;
                wName.rectTransform.anchorMin = Vector2.zero;
                wName.rectTransform.anchorMax = Vector2.one;
                wName.rectTransform.offsetMin = new Vector2(68f, 6f);
                wName.rectTransform.offsetMax = new Vector2(-32f, -6f);

                RectTransform slotBadge = Ui.Panel("SlotBadge", tileRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(26f, 26f));
                Image badgeCircle = Ui.MakeImage("Disc", slotBadge, ColGold, round: true);
                Stretch(badgeCircle.rectTransform, 0f);
                Text badgeTxt = Ui.MakeText("Num", slotBadge, "1", 15, ColInk, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badgeTxt.GetComponent<Outline>().enabled = false;
                Stretch(badgeTxt.rectTransform, 0f);
                slotBadge.gameObject.SetActive(false);

                tileRt.gameObject.SetActive(false);
                _swapTiles[i] = new SwapTileUi
                {
                    Root = tileRt,
                    Button = btn,
                    Bg = bg,
                    RoleStrip = roleStrip,
                    Icon = icon,
                    NameText = wName,
                    SlotBadgeObj = slotBadge.gameObject,
                    SlotBadgeText = badgeTxt,
                    WeaponId = "",
                };
            }

            confirmBtn = MakeActionButton(
                "ConfirmSwapBtn",
                cardRect,
                "Equip 3 Superpowers",
                ColSwapReady,
                18,
                () => ConfirmLoadoutSwap(_swapPicks),
                out confirmRect,
                out confirmImg,
                out confirmText);
            confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(0f, 12f);
            confirmRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(320f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            modalRoot.gameObject.SetActive(false);
        }

        void PopulateSwapCatalog(SimBridge sim)
        {
            _activeSwapTileCount = 0;
            string primaryId = sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.Id : "tirador_bato";
            string ultId = sim.UltimateWeaponInfo.HasValue ? sim.UltimateWeaponInfo.Value.Id : "ult_bagsak";

            IReadOnlyList<WeaponInfo> weapons = sim.AllWeapons;
            for (int i = 0; i < weapons.Count && _activeSwapTileCount < MaxSwapWeaponTiles; i++)
            {
                WeaponInfo w = weapons[i];
                if (string.IsNullOrEmpty(w.Id) ||
                    string.Equals(w.Id, primaryId, StringComparison.Ordinal) ||
                    string.Equals(w.Id, ultId, StringComparison.Ordinal))
                    continue;

                ref SwapTileUi tile = ref _swapTiles[_activeSwapTileCount++];
                tile.WeaponId = w.Id;
                tile.NameText.text = !string.IsNullOrEmpty(w.ShortName)
                    ? w.ShortName
                    : (!string.IsNullOrEmpty(w.DisplayName) ? w.DisplayName : w.Id);
                tile.RoleStrip.color = RoleColorOf(w.Kind);
                Texture2D iconTex = LoadWeaponTexture(w.Id);
                tile.Icon.texture = iconTex;
                tile.Icon.gameObject.SetActive(iconTex != null);
                tile.Root.gameObject.SetActive(true);
            }

            // Stand-in fallback so swap modal remains inspectable even if AllWeapons is empty
            if (_activeSwapTileCount == 0)
            {
                string[] fallback = AppSession.DefaultSuperpowers;
                for (int i = 0; i < fallback.Length && _activeSwapTileCount < MaxSwapWeaponTiles; i++)
                {
                    ref SwapTileUi tile = ref _swapTiles[_activeSwapTileCount++];
                    tile.WeaponId = fallback[i];
                    tile.NameText.text = fallback[i];
                    Texture2D iconTex = LoadWeaponTexture(fallback[i]);
                    tile.Icon.texture = iconTex;
                    tile.Icon.gameObject.SetActive(iconTex != null);
                    tile.Root.gameObject.SetActive(true);
                }
            }

            for (int i = _activeSwapTileCount; i < MaxSwapWeaponTiles; i++)
                _swapTiles[i].Root.gameObject.SetActive(false);

            if (_swapCardRect != null)
            {
                float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);
                LayoutSwapGrid(_swapCardRect.sizeDelta.x, _swapCardRect.sizeDelta.y, minTouch);
            }
        }

        void LayoutSwapGrid(float modalW, float modalH, float minTouch)
        {
            Vector2 closeSize = ScreenFitMetrics.ClampTouchSize(new Vector2(118f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            Vector2 confirmSize = ScreenFitMetrics.ClampTouchSize(new Vector2(320f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(108f, closeSize.y + 20f);
            float footerH = Mathf.Max(112f, confirmSize.y + 24f);

            if (_swapCloseRect != null)
            {
                _swapCloseRect.sizeDelta = closeSize;
                _swapCloseRect.anchoredPosition = new Vector2(-18f, -10f);
            }
            float rightReserve = closeSize.x + 32f;
            if (_swapTitleRect != null)
            {
                _swapTitleRect.offsetMin = new Vector2(22f, -52f);
                _swapTitleRect.offsetMax = new Vector2(-rightReserve, -14f);
            }
            if (_swapSubRect != null)
            {
                _swapSubRect.offsetMin = new Vector2(22f, -86f);
                _swapSubRect.offsetMax = new Vector2(-rightReserve, -54f);
            }
            if (_swapConfirmRect != null)
            {
                _swapConfirmRect.sizeDelta = confirmSize;
                _swapConfirmRect.anchoredPosition = new Vector2(0f, 12f);
            }
            if (_swapViewportRect != null)
            {
                _swapViewportRect.offsetMin = new Vector2(18f, footerH);
                _swapViewportRect.offsetMax = new Vector2(-18f, -headerH);
            }

            const int cols = 4;
            const float innerPad = 4f;
            const float gapX = 10f;
            const float gapY = 10f;

            int count = Mathf.Max(_activeSwapTileCount, 1);
            int rows = Mathf.Max(1, (count + cols - 1) / cols);
            float viewportW = Mathf.Max(360f, modalW - 36f);
            float cellW = Mathf.Max(minTouch, (viewportW - innerPad * 2f - gapX * (cols - 1)) / cols);
            float cellH = Mathf.Max(minTouch, 96f);

            for (int i = 0; i < _activeSwapTileCount; i++)
            {
                int c = i % cols;
                int r = i / cols;
                RectTransform rt = _swapTiles[i].Root;
                rt.sizeDelta = new Vector2(cellW, cellH);
                rt.anchoredPosition = new Vector2(innerPad + c * (cellW + gapX), -(innerPad + r * (cellH + gapY)));
            }

            if (_swapContentRect != null)
            {
                float contentH = innerPad * 2f + rows * cellH + Mathf.Max(0, rows - 1) * gapY;
                _swapContentRect.sizeDelta = new Vector2(0f, contentH);
            }
        }

        void OnSwapTilePressed(int index)
        {
            if (index < 0 || index >= _activeSwapTileCount) return;
            string wid = _swapTiles[index].WeaponId;
            if (string.IsNullOrEmpty(wid)) return;

            int existing = _swapPicks.IndexOf(wid);
            if (existing >= 0)
            {
                _swapPicks.RemoveAt(existing);
            }
            else
            {
                if (_swapPicks.Count >= 3)
                    _swapPicks.RemoveAt(2);
                _swapPicks.Add(wid);
            }

            Haptics.LightTap();
            Audio?.PlayUiTap();
            RefreshSwapSelectionVisuals();
        }

        void RefreshSwapSelectionVisuals()
        {
            Sprite pickedSprite = KomiksUi.BorderedSprite(12, 2, ColTilePicked, ColGold);
            Sprite unpickedSprite = KomiksUi.BorderedSprite(12, 2, ColTileUnpicked, KomiksUi.InkOutline);

            for (int i = 0; i < _activeSwapTileCount; i++)
            {
                ref SwapTileUi tile = ref _swapTiles[i];
                int slot = _swapPicks.IndexOf(tile.WeaponId);
                bool picked = slot >= 0;
                tile.Bg.sprite = picked ? pickedSprite : unpickedSprite;
                tile.Bg.color = Color.white;
                tile.SlotBadgeObj.SetActive(picked);
                if (picked)
                    tile.SlotBadgeText.text = (slot + 1).ToString(CultureInfo.InvariantCulture);
            }

            bool ready = _swapPicks.Count == 3 && (_sim == null || _sim.CanSwapLoadout);
            _swapConfirmBtn.interactable = ready;
            _swapConfirmImg.sprite = KomiksUi.BorderedSprite(14, 2, ready ? ColSwapReady : ColSwapDisabled, KomiksUi.InkOutline, topGloss: ready);
            _swapConfirmImg.color = Color.white;
            _swapConfirmText.text = "Equip Superpowers (" + _swapPicks.Count.ToString(CultureInfo.InvariantCulture) + "/3)";
        }

        // =============================================================================================================================
        // 4. Corner Pause & Quick-Chat Buttons, Sticker Wheel, and Speech Chips
        // =============================================================================================================================

        void BuildCornerButtons(
            out RectTransform pauseRect,
            out Button pauseBtn,
            out RectTransform chatRect,
            out Button chatBtn)
        {
            pauseBtn = MakeIconBadgeButton(
                "PauseButton",
                Root,
                KalyeahResources.Texture("ui/hud_icons/gear") ?? KalyeahResources.Texture("ui/match_gear") ?? KalyeahResources.Texture("icons/gear"),
                "II",
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap();
                    SetPaused(!IsPaused);
                },
                out pauseRect);
            pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(0f, 1f);
            pauseRect.pivot = new Vector2(0f, 1f);
            pauseRect.anchoredPosition = new Vector2(12f, -12f);

            chatBtn = MakeIconBadgeButton(
                "QuickChatButton",
                Root,
                KalyeahResources.Texture("ui/hand/chat"),
                "...",
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap();
                    SetQuickChatOpen(!IsQuickChatOpen);
                },
                out chatRect);
            chatRect.anchorMin = chatRect.anchorMax = new Vector2(1f, 1f);
            chatRect.pivot = new Vector2(1f, 1f);
            chatRect.anchoredPosition = new Vector2(-24f, -12f);
        }

        void BuildQuickChatModal(out RectTransform modalRoot, out RectTransform dimRect, out RectTransform cardRect)
        {
            modalRoot = Ui.Fill("QuickChatModal", Root);
            Image dim = Ui.MakeImage("DimBackdrop", modalRoot, new Color(0.02f, 0.04f, 0.09f, 0.42f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            AttachDismissCatcher(modalRoot, _interactive, () =>
            {
                Audio?.PlayUiTap("cancel");
                SetQuickChatOpen(false);
            });

            cardRect = Ui.Panel("StickerWheelPanel", modalRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -116f), new Vector2(282f, 354f));
            Image panelBg = KomiksUi.MakeInkPanel("Card", cardRect, ColCardWarm, KomiksUi.SlateEspresso, 16, 2, raycast: _interactive);
            Stretch(panelBg.rectTransform, 0f);

            for (int i = 0; i < StickerPhrases.Length; i++)
            {
                int capturedIndex = i;
                RectTransform cell = Ui.Panel("Sticker_" + StickerSlugs[i], cardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(122f, 102f));
                Image cellBg = KomiksUi.MakeInkPanel("CellBg", cell, new Color(0.94f, 0.90f, 0.82f, 1f), KomiksUi.SandBorder, 12, 2, raycast: _interactive);
                Stretch(cellBg.rectTransform, 0f);

                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = cellBg;
                btn.interactable = _interactive;
                btn.onClick.AddListener(() => TriggerQuickChat(capturedIndex));

                Texture2D stickerTex = KalyeahResources.Texture("ui/stickers/" + StickerSlugs[i]);
                if (stickerTex != null)
                {
                    RectTransform artBox = Ui.Panel("ArtBox", cell, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    artBox.offsetMin = new Vector2(8f, 28f);
                    artBox.offsetMax = new Vector2(-8f, -6f);

                    var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    artGo.transform.SetParent(artBox, false);
                    var raw = artGo.GetComponent<RawImage>();
                    raw.texture = stickerTex;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, 0f);
                    var fitter = artGo.GetComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fitter.aspectRatio = 1f;
                }

                Text word = Ui.MakeText("Phrase", cell, StickerPhrases[i], 16, ColWarmText, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
                word.GetComponent<Outline>().enabled = false;
                word.resizeTextForBestFit = true;
                word.resizeTextMinSize = 13;
                word.resizeTextMaxSize = 17;
                word.rectTransform.anchorMin = new Vector2(0f, 0f);
                word.rectTransform.anchorMax = new Vector2(1f, 0f);
                word.rectTransform.pivot = new Vector2(0.5f, 0f);
                word.rectTransform.anchoredPosition = new Vector2(0f, 5f);
                word.rectTransform.sizeDelta = new Vector2(-10f, 22f);

                _stickerBtnRects[i] = cell;
            }

            modalRoot.gameObject.SetActive(false);
        }

        void BuildSpeechPool()
        {
            RectTransform holder = Ui.Fill("SpeechChips", Root);
            for (int i = 0; i < SpeechPoolSize; i++)
            {
                RectTransform chip = Ui.Panel("SpeechChip_" + i, holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(162f, 54f));
                var group = chip.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Image border = MakeSlicedImage("Border", chip, OwnTeamColor(), pill: true);
                Stretch(border.rectTransform, 0f);
                Image inner = KomiksUi.MakeInkPanel("Inner", chip, ColCardWarm, KomiksUi.SlateEspresso, 12, 2);
                Stretch(inner.rectTransform, 2.5f);

                RectTransform stickerSlot = Ui.Panel("StickerSlot", chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(40f, 40f));
                var artGo = new GameObject("Sticker", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                artGo.transform.SetParent(stickerSlot, false);
                var art = artGo.GetComponent<RawImage>();
                art.raycastTarget = false;
                Stretch(art.rectTransform, 0f);
                var artFitter = artGo.GetComponent<AspectRatioFitter>();
                artFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                artFitter.aspectRatio = 1f;

                Text speaker = Ui.MakeText("Speaker", chip, "YOU", 11, ColWarmText, TextAnchor.UpperLeft);
                speaker.GetComponent<Outline>().enabled = false;
                speaker.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                speaker.rectTransform.anchorMax = new Vector2(1f, 1f);
                speaker.rectTransform.offsetMin = new Vector2(50f, 0f);
                speaker.rectTransform.offsetMax = new Vector2(-10f, -5f);

                Text phrase = Ui.MakeText("Phrase", chip, "Haha!", 16, ColWarmText, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
                phrase.GetComponent<Outline>().enabled = false;
                phrase.rectTransform.anchorMin = new Vector2(0f, 0f);
                phrase.rectTransform.anchorMax = new Vector2(1f, 0.58f);
                phrase.rectTransform.offsetMin = new Vector2(50f, 5f);
                phrase.rectTransform.offsetMax = new Vector2(-10f, 0f);

                chip.gameObject.SetActive(false);
                _speechPool[i] = new SpeechBubbleUi
                {
                    Root = chip,
                    Group = group,
                    Border = border,
                    StickerArt = art,
                    SpeakerText = speaker,
                    PhraseText = phrase,
                    PlayerId = -1,
                    TimeLeft = 0f,
                    Active = false,
                };
            }
        }

        void OnSimQuickChatSaid(int playerId, int stickerIndex)
        {
            if (_sim == null) return;
            int idx = Mathf.Clamp(stickerIndex, 0, StickerPhrases.Length - 1);

            int slot = -1;
            float lowestLeft = float.MaxValue;
            int oldestSlot = 0;
            for (int i = 0; i < SpeechPoolSize; i++)
            {
                if (_speechPool[i].Active && _speechPool[i].PlayerId == playerId)
                {
                    slot = i;
                    break;
                }
                if (!_speechPool[i].Active && slot < 0)
                    slot = i;
                if (_speechPool[i].TimeLeft < lowestLeft)
                {
                    lowestLeft = _speechPool[i].TimeLeft;
                    oldestSlot = i;
                }
            }
            if (slot < 0) slot = oldestSlot;

            KidSnapshot? kid = _sim.Find(playerId);
            bool ally = !kid.HasValue || kid.Value.Team == _sim.LocalPlayerTeam;
            ref SpeechBubbleUi b = ref _speechPool[slot];
            b.PlayerId = playerId;
            b.TimeLeft = SpeechDurationSeconds;
            b.Active = true;
            b.Border.color = ally ? OwnTeamColor() : EnemyTeamColor();
            b.SpeakerText.text = DisplayKidName(_sim, playerId, kid);
            b.PhraseText.text = StickerPhrases[idx];

            Texture2D tex = KalyeahResources.Texture("ui/stickers/" + StickerSlugs[idx]);
            b.StickerArt.texture = tex;
            b.StickerArt.gameObject.SetActive(tex != null);
            b.Group.alpha = 1f;
            b.Root.gameObject.SetActive(true);
            PositionSpeechBubble(ref b, _sim, slot);
        }

        void TickSpeechBubbles(SimBridge sim, float dt)
        {
            for (int i = 0; i < SpeechPoolSize; i++)
            {
                ref SpeechBubbleUi b = ref _speechPool[i];
                if (!b.Active) continue;
                b.TimeLeft -= dt;
                if (b.TimeLeft <= 0f)
                {
                    b.Active = false;
                    b.Root.gameObject.SetActive(false);
                    continue;
                }
                b.Group.alpha = b.TimeLeft < 0.35f ? Mathf.Clamp01(b.TimeLeft / 0.35f) : 1f;
                PositionSpeechBubble(ref b, sim, i);
            }
        }

        void PositionSpeechBubble(ref SpeechBubbleUi b, SimBridge sim, int slotIndex)
        {
            KidSnapshot? kid = sim.Find(b.PlayerId);
            if (WorldCamera != null && kid.HasValue)
            {
                Vector3 worldHead = KalyeahResources.World(kid.Value.Position, 1.95f + kid.Value.AirY);
                Vector3 screenPt = WorldCamera.WorldToScreenPoint(worldHead);
                if (screenPt.z > 0.1f)
                {
                    Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, new Vector2(screenPt.x, screenPt.y), uiCam, out Vector2 localPt))
                    {
                        Rect rect = Root.rect;
                        float halfW = 84f;
                        localPt.x = Mathf.Clamp(localPt.x, rect.xMin + halfW + 8f, rect.xMax - halfW - 8f);
                        localPt.y = Mathf.Clamp(localPt.y + 12f, rect.yMin + 24f, rect.yMax - 68f);
                        b.Root.anchoredPosition = localPt;
                        return;
                    }
                }
            }

            // Clean safe-area fallback lane (headless tests or offscreen speakers)
            Rect r = Root.rect;
            float x = r.width > 0f ? (r.xMax - 96f) : 420f;
            float y = r.height > 0f ? (r.yMax - 236f - slotIndex * 58f) : (120f - slotIndex * 58f);
            b.Root.anchoredPosition = new Vector2(x, y);
        }

        void ClearSpeechBubbles()
        {
            for (int i = 0; i < SpeechPoolSize; i++)
            {
                _speechPool[i].Active = false;
                _speechPool[i].TimeLeft = 0f;
                if (_speechPool[i].Root != null)
                    _speechPool[i].Root.gameObject.SetActive(false);
            }
        }

        // =============================================================================================================================
        // 5a. Round Intro, Point Freeze & Match Victory / Ranked LP Summary
        // =============================================================================================================================

        void BuildIntroOverlay(
            out RectTransform root,
            out RectTransform dimRect,
            out RectTransform cardRect,
            out Text roundText,
            out Text countdownText,
            out RectTransform barFill)
        {
            root = Ui.Fill("RoundIntroOverlay", Root);
            Image dim = Ui.MakeImage("Backdrop", root, new Color(0.01f, 0.03f, 0.08f, 0.80f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            AttachDismissCatcher(root, _interactive, () =>
            {
                if (_sim != null && _sim.SkipIntro())
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("confirm");
                }
            });

            cardRect = Ui.Panel("IntroCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(548f, 284f));
            Image plate = KomiksUi.MakeInkPanel("Plate", cardRect, ColModalDark, KomiksUi.InkOutline, 18, 2, raycast: _interactive);
            Stretch(plate.rectTransform, 0f);
            if (_interactive)
            {
                var cardCatcher = plate.gameObject.AddComponent<DismissTapCatcher>();
                cardCatcher.OnTap = () =>
                {
                    if (_sim != null && _sim.SkipIntro())
                    {
                        Haptics.LightTap();
                        Audio?.PlayUiTap("confirm");
                    }
                };
            }

            Text title = Ui.MakeText("Title", cardRect, "AGAWANG BASE", 34, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            title.rectTransform.sizeDelta = new Vector2(0f, 42f);

            countdownText = Ui.MakeText("Countdown", cardRect, "10", 36, Color.white, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            countdownText.rectTransform.anchorMin = new Vector2(0.75f, 1f);
            countdownText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countdownText.rectTransform.pivot = new Vector2(1f, 1f);
            countdownText.rectTransform.anchoredPosition = new Vector2(-24f, -16f);
            countdownText.rectTransform.sizeDelta = new Vector2(0f, 44f);

            RectTransform barBox = Ui.Panel("CountdownBar", cardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(-48f, 6f));
            Image barBg = Ui.MakeImage("Track", barBox, new Color(1f, 1f, 1f, 0.22f));
            Stretch(barBg.rectTransform, 0f);
            Image fillImg = Ui.MakeImage("Fill", barBox, Color.white);
            barFill = fillImg.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.sizeDelta = Vector2.zero;

            roundText = Ui.MakeText("RoundInfo", cardRect, "ROUND 1 · Capture enemy base or tag all 5 rivals!", 16, Ui.Cream, TextAnchor.MiddleLeft);
            roundText.horizontalOverflow = HorizontalWrapMode.Wrap;
            roundText.rectTransform.anchorMin = new Vector2(0f, 1f);
            roundText.rectTransform.anchorMax = new Vector2(1f, 1f);
            roundText.rectTransform.pivot = new Vector2(0.5f, 1f);
            roundText.rectTransform.anchoredPosition = new Vector2(0f, -86f);
            roundText.rectTransform.sizeDelta = new Vector2(-48f, 50f);

            Text controls = Ui.MakeText("Controls", cardRect, "MOVE  Left stick / tilt     ·     ATTACK  Tap Tirador or drag Superpower to aim", 15, ColStamina, TextAnchor.MiddleLeft);
            controls.horizontalOverflow = HorizontalWrapMode.Wrap;
            controls.rectTransform.anchorMin = new Vector2(0f, 0f);
            controls.rectTransform.anchorMax = new Vector2(1f, 0f);
            controls.rectTransform.pivot = new Vector2(0.5f, 0f);
            controls.rectTransform.anchoredPosition = new Vector2(0f, 54f);
            controls.rectTransform.sizeDelta = new Vector2(-48f, 46f);

            Text tapHint = Ui.MakeText("TapContinue", cardRect, "Tap to continue", 18, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            tapHint.rectTransform.anchorMin = new Vector2(0f, 0f);
            tapHint.rectTransform.anchorMax = new Vector2(1f, 0f);
            tapHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            tapHint.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            tapHint.rectTransform.sizeDelta = new Vector2(-48f, 28f);

            root.gameObject.SetActive(false);
        }

        void BuildBriefingCard(
            out RectTransform root,
            out RectTransform dimRect,
            out RectTransform panelRect,
            out RectTransform gotItRect,
            out RectTransform barFill,
            out Text countdownText)
        {
            root = Ui.Fill("BriefingCard", Root);
            Image dim = Ui.MakeImage("Backdrop", root, new Color(0.01f, 0.03f, 0.08f, 0.84f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            AttachDismissCatcher(root, _interactive, () =>
            {
                Haptics.LightTap();
                Audio?.PlayUiTap("confirm");
                DismissBriefing();
            });

            panelRect = Ui.Panel("OnboardingPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 412f));
            Image plate = KomiksUi.MakeInkPanel("Plate", panelRect, ColModalDark, KomiksUi.InkOutline, 18, 2, raycast: _interactive);
            Stretch(plate.rectTransform, 0f);

            Text kicker = Ui.MakeText("Kicker", panelRect, "FIRST MATCH BRIEFING", 14, ColStamina, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            kicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            kicker.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            kicker.rectTransform.pivot = new Vector2(0f, 1f);
            kicker.rectTransform.anchoredPosition = new Vector2(24f, -14f);
            kicker.rectTransform.sizeDelta = new Vector2(0f, 20f);

            Text title = Ui.MakeText("Title", panelRect, "AGAWANG BASE", 32, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(24f, -35f);
            title.rectTransform.sizeDelta = new Vector2(0f, 38f);

            countdownText = Ui.MakeText("Countdown", panelRect, "12", 30, Color.white, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            countdownText.rectTransform.anchorMin = new Vector2(0.76f, 1f);
            countdownText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countdownText.rectTransform.pivot = new Vector2(1f, 1f);
            countdownText.rectTransform.anchoredPosition = new Vector2(-24f, -24f);
            countdownText.rectTransform.sizeDelta = new Vector2(0f, 40f);

            RectTransform barBox = Ui.Panel("CountdownBar", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(-48f, 6f));
            Image barBg = Ui.MakeImage("Track", barBox, new Color(1f, 1f, 1f, 0.22f));
            Stretch(barBg.rectTransform, 0f);
            Image fillImg = Ui.MakeImage("Fill", barBox, ColGold);
            barFill = fillImg.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.sizeDelta = Vector2.zero;

            Text bullet1 = Ui.MakeText("RuleGoal", panelRect, "• WIN A POINT: Capture the Enemy Base post OR tag all 5 rivals (First to 3)!", 16, Ui.Cream, TextAnchor.MiddleLeft);
            bullet1.horizontalOverflow = HorizontalWrapMode.Wrap;
            bullet1.resizeTextForBestFit = true;
            bullet1.resizeTextMinSize = 13;
            bullet1.resizeTextMaxSize = 17;
            bullet1.rectTransform.anchorMin = new Vector2(0f, 1f);
            bullet1.rectTransform.anchorMax = new Vector2(1f, 1f);
            bullet1.rectTransform.pivot = new Vector2(0.5f, 1f);
            bullet1.rectTransform.anchoredPosition = new Vector2(0f, -96f);
            bullet1.rectTransform.sizeDelta = new Vector2(-48f, 46f);

            Text bullet2 = Ui.MakeText("RuleRescue", panelRect, "• RESCUE MATES: Stand in your Rescue Shed for 5 s to free tagged allies.", 16, ColRegenMint, TextAnchor.MiddleLeft);
            bullet2.horizontalOverflow = HorizontalWrapMode.Wrap;
            bullet2.resizeTextForBestFit = true;
            bullet2.resizeTextMinSize = 13;
            bullet2.resizeTextMaxSize = 17;
            bullet2.rectTransform.anchorMin = new Vector2(0f, 1f);
            bullet2.rectTransform.anchorMax = new Vector2(1f, 1f);
            bullet2.rectTransform.pivot = new Vector2(0.5f, 1f);
            bullet2.rectTransform.anchoredPosition = new Vector2(0f, -148f);
            bullet2.rectTransform.sizeDelta = new Vector2(-48f, 46f);

            Text bullet3 = Ui.MakeText("RuleControls", panelRect, "• THUMBS: Left stick moves · Tap Tirador or drag a Superpower to aim!", 15, ColStamina, TextAnchor.MiddleLeft);
            bullet3.horizontalOverflow = HorizontalWrapMode.Wrap;
            bullet3.resizeTextForBestFit = true;
            bullet3.resizeTextMinSize = 13;
            bullet3.resizeTextMaxSize = 16;
            bullet3.rectTransform.anchorMin = new Vector2(0f, 1f);
            bullet3.rectTransform.anchorMax = new Vector2(1f, 1f);
            bullet3.rectTransform.pivot = new Vector2(0.5f, 1f);
            bullet3.rectTransform.anchoredPosition = new Vector2(0f, -200f);
            bullet3.rectTransform.sizeDelta = new Vector2(-48f, 44f);

            MakeActionButton(
                "GotItButton",
                panelRect,
                "LET'S LARO!",
                new Color(0.18f, 0.68f, 0.42f, 1f),
                19,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("confirm");
                    DismissBriefing();
                },
                out gotItRect,
                out _,
                out _);
            gotItRect.anchorMin = gotItRect.anchorMax = new Vector2(0.5f, 0f);
            gotItRect.pivot = new Vector2(0.5f, 0f);
            gotItRect.anchoredPosition = new Vector2(0f, 18f);
            gotItRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(248f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            root.gameObject.SetActive(false);
        }

        void SyncBriefingCard(MatchPhaseKind phase, float dt)
        {
            if (_briefingRoot == null) return;
            bool shouldShow = _interactive && AppSession.Current.BriefingPending && phase != MatchPhaseKind.MatchOver;
            if (_briefingRoot.gameObject.activeSelf != shouldShow)
            {
                _briefingRoot.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    _briefingTimeLeft = BriefingAutoDismissSeconds;
                    _lastBriefingCeil = -1;
                }
            }
            if (!shouldShow) return;

            if (dt > 0f)
            {
                _briefingTimeLeft -= dt;
                if (_briefingTimeLeft <= 0f)
                {
                    DismissBriefing();
                    return;
                }
            }

            int ceilSec = Mathf.Max(1, Mathf.CeilToInt(_briefingTimeLeft));
            if (ceilSec != _lastBriefingCeil && _briefingCountdownText != null)
            {
                _lastBriefingCeil = ceilSec;
                _briefingCountdownText.text = ceilSec.ToString(CultureInfo.InvariantCulture);
            }
            if (_briefingBarFill != null)
                _briefingBarFill.anchorMax = new Vector2(Mathf.Clamp01(_briefingTimeLeft / BriefingAutoDismissSeconds), 1f);
        }

        // =============================================================================================================================
        // 0. HurtPulse Screen-Edge Vignette & Projected 3D->2D WorldWaypoints ([GAP-VIEW-7])
        // =============================================================================================================================

        void BuildHurtPulseOverlay(out RectTransform root)
        {
            root = Ui.Fill("HurtPulseOverlay", Root);
            root.offsetMin = new Vector2(-8f, -8f);
            root.offsetMax = new Vector2(8f, 8f);
            Sprite grad = EdgeGradientSprite();

            // 0 = Bottom (0 deg), 1 = Top (180 deg), 2 = Left (-90 deg), 3 = Right (+90 deg)
            _hurtEdges[0] = CreateHurtEdge("BottomEdge", root, grad, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, HurtEdgeDepth), 0f);
            _hurtEdges[1] = CreateHurtEdge("TopEdge", root, grad, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, HurtEdgeDepth), 180f);
            _hurtEdges[2] = CreateHurtEdge("LeftEdge", root, grad, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, HurtEdgeDepth), -90f);
            _hurtEdges[2].rectTransform.anchoredPosition = new Vector2(HurtEdgeDepth * 0.5f, 0f);
            _hurtEdges[3] = CreateHurtEdge("RightEdge", root, grad, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, HurtEdgeDepth), 90f);
            _hurtEdges[3].rectTransform.anchoredPosition = new Vector2(-HurtEdgeDepth * 0.5f, 0f);

            root.gameObject.SetActive(false);
        }

        static Image CreateHurtEdge(string name, Transform parent, Sprite grad, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, float rotZ)
        {
            RectTransform rt = Ui.Panel(name, parent, aMin, aMax, pivot, Vector2.zero, size);
            rt.localEulerAngles = new Vector3(0f, 0f, rotZ);
            Image img = Ui.MakeImage("Gradient", rt, ColHurtRed, round: false, raycast: false);
            img.sprite = grad;
            Stretch(img.rectTransform, 0f);
            return img;
        }

        void TickHurtPulse(SimBridge sim, KidSnapshot? localKid, float dt)
        {
            if (_hurtPulseRoot == null) return;

            if (_hurtFlashLeft > 0f)
                _hurtFlashLeft = Mathf.Max(0f, _hurtFlashLeft - dt);

            float hitAlpha = _hurtFlashLeft > 0f
                ? _hurtFlashPeak * (_hurtFlashLeft / Mathf.Max(_hurtFlashSpan, 0.05f))
                : 0f;

            bool lowHp = sim.Phase == MatchPhaseKind.Playing &&
                         localKid.HasValue &&
                         !localKid.Value.Tagged &&
                         localKid.Value.Hp > 0 &&
                         localKid.Value.Hp <= sim.MaxHp * LowHealthThresholdShare;

            float breathAlpha = 0f;
            if (SettingsStore.Current.ReduceMotion)
            {
                _lowHpWeight = Mathf.MoveTowards(_lowHpWeight, lowHp ? 1f : 0f, dt * 1.5f);
                breathAlpha = 0.14f * _lowHpWeight;
            }
            else
            {
                _lowHpWeight = Mathf.MoveTowards(_lowHpWeight, lowHp ? 1f : 0f, dt * (lowHp ? 3.0f : 2.5f));
                if (_lowHpWeight > 0.001f)
                {
                    _lowHpTime += dt;
                    float wave = 0.5f - 0.5f * Mathf.Cos(_lowHpTime * Mathf.PI * 2f / LowHealthBreathPeriod);
                    breathAlpha = Mathf.Lerp(0.10f, 0.28f, wave) * _lowHpWeight;
                }
                else
                {
                    _lowHpTime = 0f;
                }
            }

            _hurtCurrentAlpha = Mathf.Max(hitAlpha, breathAlpha);
            ApplyHurtPulseVisual(_hurtCurrentAlpha);
        }

        void ApplyHurtPulseVisual(float alpha)
        {
            if (_hurtPulseRoot == null) return;
            bool show = alpha > 0.003f;
            if (_hurtPulseRoot.gameObject.activeSelf != show)
                _hurtPulseRoot.gameObject.SetActive(show);
            if (!show) return;

            Color tint = SettingsStore.Current.IsColorblind
                ? Color.Lerp(ColHurtRed, EnemyTeamColor(), 0.35f)
                : ColHurtRed;
            tint.a = Mathf.Clamp01(alpha);
            for (int i = 0; i < _hurtEdges.Length; i++)
            {
                if (_hurtEdges[i] != null)
                    _hurtEdges[i].color = tint;
            }
        }

        void BuildWorldWaypointsOverlay(out RectTransform root)
        {
            root = Ui.Fill("WorldWaypointsOverlay", Root);
            Sprite chevron = ChevronSprite();

            for (int i = 0; i < MaxWaypoints; i++)
            {
                RectTransform marker = Ui.Panel("Waypoint_" + i, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 48f));
                var group = marker.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                RectTransform chevronRt = Ui.Panel("Chevron", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
                Image chevronImg = Ui.MakeImage("Arrow", chevronRt, ColGold, round: false, raycast: false);
                chevronImg.sprite = chevron;
                Stretch(chevronImg.rectTransform, 0f);
                chevronRt.gameObject.SetActive(false);

                RectTransform diamondBox = Ui.Panel("Diamond", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(18f, 18f));
                diamondBox.localEulerAngles = new Vector3(0f, 0f, 45f);
                Image dRing = Ui.MakeImage("Rim", diamondBox, ColInk, round: false, raycast: false);
                Stretch(dRing.rectTransform, 0f);
                Image dFill = Ui.MakeImage("Core", diamondBox, ColGold, round: false, raycast: false);
                Stretch(dFill.rectTransform, 2.5f);

                RectTransform pillRt = Ui.Panel("DistancePill", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(82f, 19f));
                Image pillBg = MakeSlicedImage("Plate", pillRt, new Color(0.04f, 0.06f, 0.11f, 0.84f), pill: true, raycast: false);
                Stretch(pillBg.rectTransform, 0f);
                Text pillTxt = Ui.MakeText("Label", pillRt, "38m", 11, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                pillTxt.raycastTarget = false;
                Stretch(pillTxt.rectTransform, 2f);

                marker.gameObject.SetActive(false);
                _waypoints[i] = new WaypointMarkerUi
                {
                    Root = marker,
                    Group = group,
                    DiamondBox = diamondBox,
                    DiamondRing = dRing,
                    DiamondFill = dFill,
                    ChevronRect = chevronRt,
                    ChevronImg = chevronImg,
                    PillRect = pillRt,
                    PillBg = pillBg,
                    PillText = pillTxt,
                    CachedMeters = -1,
                    CachedWord = "",
                };
            }
        }

        void HideAllWaypoints()
        {
            ActiveWaypointCount = 0;
            for (int i = 0; i < MaxWaypoints; i++)
            {
                if (_waypoints[i].Root != null && _waypoints[i].Root.gameObject.activeSelf)
                    _waypoints[i].Root.gameObject.SetActive(false);
            }
        }

        void RefreshWaypointWordCache()
        {
            string lang = SettingsStore.Current.Language ?? "en";
            int keyCount = I18n.KeyCount;
            if (keyCount == _cachedWaypointKeyCount && string.Equals(lang, _cachedWaypointLang, StringComparison.Ordinal))
                return;

            _cachedWaypointLang = lang;
            _cachedWaypointKeyCount = keyCount;
            _cachedBaseWord = I18n.Tr("BASE");
            _cachedDefendWord = I18n.Tr("DEFEND");
            _cachedRescueWord = I18n.Tr("RESCUE");
        }

        void SyncWorldWaypoints(SimBridge sim, KidSnapshot? localKid, int localId, int localTeam, float dt)
        {
            if (_waypointsRoot == null ||
                WorldCamera == null ||
                sim.Phase != MatchPhaseKind.Playing ||
                !localKid.HasValue ||
                localKid.Value.Tagged ||
                IsPaused ||
                IsBriefingVisible)
            {
                HideAllWaypoints();
                return;
            }

            if (!SettingsStore.Current.ReduceMotion)
                _waypointPulseTime += dt;

            RefreshWaypointWordCache();

            SVector2 myFeet = localKid.Value.Position;
            int enemyTeam = 1 - localTeam;
            bool cb = SettingsStore.Current.IsColorblind;
            int activeCount = 0;

            // Slot 0: Enemy Base ("BASE")
            bool showEnemyBase = sim.Bases != null && enemyTeam >= 0 && enemyTeam < sim.Bases.Count;
            if (showEnemyBase)
            {
                BaseObjectiveSnapshot eb = sim.Bases[enemyTeam];
                SVector2 targetXz = eb.PostPosition.LengthSquared() > 0.01f ? eb.PostPosition : eb.Center;
                bool urgent = eb.Holders > 0 || (sim.CaptureTeam == localTeam && sim.CaptureReason == CaptureReasonKind.Base);
                Color col = cb ? OwnTeamColor() : ColWaypointGold;
                if (SyncSingleWaypoint(ref _waypoints[0], myFeet, targetXz, 5.4f, _cachedBaseWord, col, urgent))
                    activeCount++;
            }
            else if (_waypoints[0].Root.gameObject.activeSelf)
            {
                _waypoints[0].Root.gameObject.SetActive(false);
            }

            // Slot 1: Own Base ("DEFEND") — shown only while contested / being captured by rivals
            bool showDefend = false;
            if (sim.Bases != null && localTeam >= 0 && localTeam < sim.Bases.Count)
            {
                BaseObjectiveSnapshot ob = sim.Bases[localTeam];
                bool underThreat = ob.Holders > 0 || ob.CapturingTeam == enemyTeam ||
                                   (sim.CaptureTeam == enemyTeam && sim.CaptureReason == CaptureReasonKind.Base);
                if (underThreat)
                {
                    SVector2 targetXz = ob.PostPosition.LengthSquared() > 0.01f ? ob.PostPosition : ob.Center;
                    Color col = cb ? EnemyTeamColor() : ColWaypointDefend;
                    showDefend = SyncSingleWaypoint(ref _waypoints[1], myFeet, targetXz, 5.4f, _cachedDefendWord, col, urgent: true);
                    if (showDefend) activeCount++;
                }
            }
            if (!showDefend && _waypoints[1].Root.gameObject.activeSelf)
                _waypoints[1].Root.gameObject.SetActive(false);

            // Slot 2: Own Rescue Shed ("RESCUE") — shown only when at least one ally is tagged on the sidewalk
            bool showRescue = false;
            if (sim.Rescues != null && localTeam >= 0 && localTeam < sim.Rescues.Count)
            {
                RescueObjectiveSnapshot rz = sim.Rescues[localTeam];
                bool allyWaiting = rz.WaitingCount > 0;
                if (!allyWaiting)
                {
                    IReadOnlyList<KidSnapshot> kids = sim.Kids;
                    for (int i = 0; i < kids.Count; i++)
                    {
                        if (kids[i].Team == localTeam && kids[i].Id != localId && kids[i].Tagged)
                        {
                            allyWaiting = true;
                            break;
                        }
                    }
                }
                if (allyWaiting)
                {
                    Color col = cb ? ColWaypointGold : ColWaypointRescue;
                    showRescue = SyncSingleWaypoint(ref _waypoints[2], myFeet, rz.Center, 4.8f, _cachedRescueWord, col, urgent: rz.RescuerId >= 0);
                    if (showRescue) activeCount++;
                }
            }
            if (!showRescue && _waypoints[2].Root.gameObject.activeSelf)
                _waypoints[2].Root.gameObject.SetActive(false);

            ActiveWaypointCount = activeCount;
        }

        bool SyncSingleWaypoint(
            ref WaypointMarkerUi ui,
            SVector2 myFeet,
            SVector2 targetGround,
            float worldY,
            string word,
            Color color,
            bool urgent)
        {
            float distMeters = SVector2.Distance(myFeet, targetGround);
            if (distMeters < 4.0f)
            {
                if (ui.Root.gameObject.activeSelf)
                    ui.Root.gameObject.SetActive(false);
                return false;
            }

            Rect rect = Root.rect;
            if (rect.width <= 1f || rect.height <= 1f)
                rect = new Rect(-640f, -360f, 1280f, 720f);

            const float marginX = 82f;
            const float marginTop = 94f;
            const float marginBottom = 108f;
            float minX = rect.xMin + marginX;
            float maxX = rect.xMax - marginX;
            float minY = rect.yMin + marginBottom;
            float maxY = rect.yMax - marginTop;
            if (minX > maxX) { minX = maxX = rect.center.x; }
            if (minY > maxY) { minY = maxY = rect.center.y; }

            Vector3 worldPos = KalyeahResources.World(targetGround, worldY);
            Vector3 camLocal = WorldCamera.transform.InverseTransformPoint(worldPos);
            Vector3 screenPt = WorldCamera.WorldToScreenPoint(worldPos);
            Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;

            Vector2 localPt;
            bool inFront = camLocal.z > 0.15f && screenPt.z > 0.15f;
            if (inFront && RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, new Vector2(screenPt.x, screenPt.y), uiCam, out Vector2 projected))
            {
                localPt = projected;
            }
            else
            {
                Vector2 dir = new Vector2(camLocal.x, camLocal.z > 0.15f ? camLocal.y : -Mathf.Abs(camLocal.y) - 0.35f);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                dir.Normalize();
                localPt = rect.center + dir * 2000f;
            }

            bool clamped = !inFront || localPt.x < minX || localPt.x > maxX || localPt.y < minY || localPt.y > maxY;
            Vector2 outDir = Vector2.up;
            if (clamped)
            {
                Vector2 delta = localPt - rect.center;
                if (delta.sqrMagnitude < 0.0001f) delta = Vector2.down;
                float halfW = Mathf.Max(1f, (maxX - minX) * 0.5f);
                float halfH = Mathf.Max(1f, (maxY - minY) * 0.5f);
                Vector2 boxCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                float sx = Mathf.Abs(delta.x) > 0.001f ? halfW / Mathf.Abs(delta.x) : float.MaxValue;
                float sy = Mathf.Abs(delta.y) > 0.001f ? halfH / Mathf.Abs(delta.y) : float.MaxValue;
                float t = Mathf.Min(sx, sy);
                localPt = boxCenter + delta * t;
                outDir = delta.normalized;
            }

            if (!ui.Root.gameObject.activeSelf)
                ui.Root.gameObject.SetActive(true);

            ui.Root.anchoredPosition = localPt;
            ui.Group.alpha = clamped ? (urgent ? 0.90f : 0.76f) : (urgent ? 0.78f : 0.48f);
            ui.DiamondFill.color = color;
            ui.PillText.color = color;

            float pulse = (urgent && !SettingsStore.Current.ReduceMotion)
                ? (1f + 0.10f * Mathf.Sin(_waypointPulseTime * Mathf.PI * 2f * 2.2f))
                : 1f;
            ui.DiamondBox.localScale = new Vector3(pulse, pulse, 1f);

            if (ui.ChevronRect.gameObject.activeSelf != clamped)
                ui.ChevronRect.gameObject.SetActive(clamped);
            if (clamped)
            {
                ui.ChevronImg.color = color;
                ui.ChevronRect.anchoredPosition = new Vector2(0f, 6f) + outDir * 16f;
                float deg = Mathf.Atan2(outDir.y, outDir.x) * Mathf.Rad2Deg - 90f;
                ui.ChevronRect.localEulerAngles = new Vector3(0f, 0f, deg);
            }

            int meters = Mathf.Max(1, Mathf.RoundToInt(distMeters));
            bool showWord = clamped || urgent;
            if (meters != ui.CachedMeters || showWord != ui.CachedClamped || urgent != ui.CachedUrgent || !string.Equals(word, ui.CachedWord, StringComparison.Ordinal))
            {
                ui.CachedMeters = meters;
                ui.CachedClamped = showWord;
                ui.CachedUrgent = urgent;
                ui.CachedWord = word;
                string mStr = meters.ToString(CultureInfo.InvariantCulture) + "m";
                ui.PillText.text = showWord ? (word + " · " + mStr) : mStr;
                ui.PillRect.sizeDelta = new Vector2(showWord ? 92f : 48f, 19f);
            }

            return true;
        }

        void BuildPointFreezeOverlay(
            out RectTransform root,
            out RectTransform dimRect,
            out Text subjectText,
            out Text outcomeText,
            out Text scoreText)
        {
            root = Ui.Fill("PointFreezeOverlay", Root);
            Image dim = Ui.MakeImage("Dim", root, new Color(0.02f, 0.04f, 0.10f, 0.56f), raycast: false);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            RectTransform box = Ui.Panel("CenterCallout", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(468f, 202f));
            Image card = KomiksUi.MakeInkPanel("Plate", box, ColModalDark, KomiksUi.InkOutline, 18, 2);
            Stretch(card.rectTransform, 0f);

            subjectText = Ui.MakeText("Subject", box, "Enemy base", 20, Ui.Cream, TextAnchor.MiddleCenter);
            subjectText.rectTransform.anchorMin = new Vector2(0f, 1f);
            subjectText.rectTransform.anchorMax = new Vector2(1f, 1f);
            subjectText.rectTransform.pivot = new Vector2(0.5f, 1f);
            subjectText.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            subjectText.rectTransform.sizeDelta = new Vector2(-24f, 30f);

            outcomeText = Ui.MakeText("Outcome", box, "CAPTURED", 42, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            outcomeText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            outcomeText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            outcomeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outcomeText.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            outcomeText.rectTransform.sizeDelta = new Vector2(-24f, 54f);

            scoreText = Ui.MakeText("Score", box, "1 - 0", 34, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            scoreText.rectTransform.anchorMin = new Vector2(0f, 0f);
            scoreText.rectTransform.anchorMax = new Vector2(1f, 0f);
            scoreText.rectTransform.pivot = new Vector2(0.5f, 0f);
            scoreText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            scoreText.rectTransform.sizeDelta = new Vector2(-24f, 44f);

            root.gameObject.SetActive(false);
        }

        void OnSimPointScored(int scoringTeam, int howRaw)
        {
            _lastPointWinnerTeam = scoringTeam;
            _lastPointReason = Enum.IsDefined(typeof(CaptureReasonKind), howRaw)
                ? (CaptureReasonKind)howRaw
                : CaptureReasonKind.Base;
        }

        void BuildVictoryOverlay(
            out RectTransform root,
            out RectTransform dimRect,
            out RectTransform cardRect,
            out Text titleText,
            out Text subText,
            out Text youCapText,
            out Text themCapText,
            out Text myScoreText,
            out Text theirScoreText,
            out GameObject promoBannerObj,
            out RectTransform rankCardRect,
            out Image emblemBg,
            out Text emblemInitial,
            out Text tierNameText,
            out Image deltaChipImg,
            out Text deltaText,
            out RectTransform lpBarFill,
            out Image lpBarImg,
            out Text lpDetailText,
            out RectTransform playAgainRect,
            out RectTransform homeRect)
        {
            root = Ui.Fill("MatchVictoryOverlay", Root);
            Image dim = Ui.MakeImage("Backdrop", root, new Color(0.02f, 0.04f, 0.09f, 0.88f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            cardRect = Ui.Panel("VictoryCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(588f, 528f));
            cardRect.gameObject.AddComponent<RectMask2D>();
            Image cardBg = KomiksUi.MakeInkPanel("Plate", cardRect, ColModalDark, KomiksUi.InkOutline, 18, 2, raycast: _interactive);
            Stretch(cardBg.rectTransform, 0f);

            // Preallocated fiesta banderitas contained strictly inside VictoryCard (zero runtime instantiation/GC, never spills over HUD)
            RectTransform confettiLayer = Ui.Fill("ConfettiLayer", cardRect);
            Color[] fiestaPalette =
            {
                new Color(0.96f, 0.65f, 0.14f, 0.92f),
                new Color(0.12f, 0.62f, 0.55f, 0.92f),
                new Color(0.89f, 0.33f, 0.24f, 0.92f),
                new Color(0.98f, 0.95f, 0.89f, 0.92f),
            };
            for (int i = 0; i < ConfettiCount; i++)
            {
                RectTransform p = Ui.Panel("Confetti_" + i, confettiLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(15f, 9f));
                Image img = Ui.MakeImage("Paper", p, fiestaPalette[i % fiestaPalette.Length]);
                Stretch(img.rectTransform, 0f);
                p.gameObject.SetActive(false);

                float spread = ((i / (float)(ConfettiCount - 1)) - 0.5f) * 520f;
                _confetti[i] = new ConfettiPiece
                {
                    Rect = p,
                    Image = img,
                    StartPos = new Vector2(spread, -10f - (i % 4) * 10f),
                    Velocity = new Vector2(((i % 5) - 2) * 28f, -185f - (i % 4) * 48f),
                    SpinSpeed = ((i % 2 == 0) ? 1f : -1f) * (140f + (i % 3) * 70f),
                };
            }

            titleText = Ui.MakeText("Title", cardRect, "Victory", 46, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            titleText.rectTransform.sizeDelta = new Vector2(-32f, 52f);

            subText = Ui.MakeText("Subtitle", cardRect, "Agawang Base (First to 3) (5v5)", 16, Ui.Cream, TextAnchor.MiddleCenter);
            subText.rectTransform.anchorMin = new Vector2(0f, 1f);
            subText.rectTransform.anchorMax = new Vector2(1f, 1f);
            subText.rectTransform.pivot = new Vector2(0.5f, 1f);
            subText.rectTransform.anchoredPosition = new Vector2(0f, -68f);
            subText.rectTransform.sizeDelta = new Vector2(-32f, 26f);

            // Disjoint ScoreRow: YOU / THEM caps strictly in [0.72..1.0], large digits strictly in [0.0..0.70]
            RectTransform scoreRow = Ui.Panel("ScoreRow", cardRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(340f, 72f));
            youCapText = Ui.MakeText("YouCap", scoreRow, "YOU", 14, KillFeedOwnColor(), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            youCapText.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            youCapText.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            youCapText.rectTransform.sizeDelta = Vector2.zero;

            myScoreText = Ui.MakeText("MyScore", scoreRow, "3", 40, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            myScoreText.rectTransform.anchorMin = new Vector2(0f, 0f);
            myScoreText.rectTransform.anchorMax = new Vector2(0.42f, 0.70f);
            myScoreText.rectTransform.sizeDelta = Vector2.zero;

            Text sep = Ui.MakeText("Dash", scoreRow, "-", 34, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            sep.rectTransform.anchorMin = new Vector2(0.42f, 0f);
            sep.rectTransform.anchorMax = new Vector2(0.58f, 0.74f);
            sep.rectTransform.sizeDelta = Vector2.zero;

            themCapText = Ui.MakeText("ThemCap", scoreRow, "THEM", 14, KillFeedEnemyColor(), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            themCapText.rectTransform.anchorMin = new Vector2(0.58f, 0.72f);
            themCapText.rectTransform.anchorMax = new Vector2(1f, 1f);
            themCapText.rectTransform.sizeDelta = Vector2.zero;

            theirScoreText = Ui.MakeText("TheirScore", scoreRow, "1", 40, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            theirScoreText.rectTransform.anchorMin = new Vector2(0.58f, 0f);
            theirScoreText.rectTransform.anchorMax = new Vector2(1f, 0.70f);
            theirScoreText.rectTransform.sizeDelta = Vector2.zero;

            // Optional "ANGAT RANGGO!" promotion pill seated cleanly below ScoreRow with 10 px clearance
            RectTransform promoRect = Ui.Panel("PromoBanner", cardRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(236f, 30f));
            Image promoBg = KomiksUi.MakeInkPanel("PromoBg", promoRect, ColGold, KomiksUi.SlateEspresso, 14, 2);
            Stretch(promoBg.rectTransform, 0f);
            Text promoTxt = Ui.MakeText("PromoText", promoRect, "ANGAT RANGGO!", 16, ColWarmText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            promoTxt.GetComponent<Outline>().enabled = false;
            Stretch(promoTxt.rectTransform, 0f);
            promoBannerObj = promoRect.gameObject;
            promoBannerObj.SetActive(false);

            // Warm sand Tagalog Ranked LP Card raised into the middle band with >= 24 px clearance above >= 48 dp action buttons
            rankCardRect = Ui.Panel("RankCard", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(-48f, 132f));
            Image rankBg = KomiksUi.MakeInkPanel("WarmCard", rankCardRect, ColCardWarm, KomiksUi.SlateEspresso, 16, 2);
            Stretch(rankBg.rectTransform, 0f);

            RectTransform emblemBox = Ui.Panel("RankEmblem", rankCardRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(72f, 72f));
            Image emblemRim = Ui.MakeImage("CrestRim", emblemBox, KomiksUi.SlateEspresso, round: true);
            Stretch(emblemRim.rectTransform, 0f);
            emblemBg = Ui.MakeImage("Crest", emblemBox, HexColor("c8874b", ColGold), round: true);
            Stretch(emblemBg.rectTransform, 3f);
            emblemInitial = Ui.MakeText("EmblemGlyph", emblemBox, "K", 32, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(emblemInitial.rectTransform, 0f);

            tierNameText = Ui.MakeText("TierName", rankCardRect, "KAHOY", 24, ColWarmText, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            tierNameText.GetComponent<Outline>().enabled = false;
            tierNameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            tierNameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            tierNameText.rectTransform.offsetMin = new Vector2(102f, -46f);
            tierNameText.rectTransform.offsetMax = new Vector2(-132f, -14f);

            RectTransform deltaBox = Ui.Panel("DeltaChip", rankCardRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(108f, 32f));
            deltaChipImg = KomiksUi.MakeInkPanel("ChipBg", deltaBox, ColDeltaPos, KomiksUi.SlateEspresso, 14, 2);
            Stretch(deltaChipImg.rectTransform, 0f);
            deltaText = Ui.MakeText("DeltaText", deltaBox, "+15 LP", 15, ColCardWarm, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            deltaText.GetComponent<Outline>().enabled = false;
            Stretch(deltaText.rectTransform, 0f);

            RectTransform lpTrack = Ui.Panel("RankLpBar", rankCardRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(43f, 0f), new Vector2(-118f, 18f));
            Image lpTrackImg = KomiksUi.MakeInkPanel("Track", lpTrack, HexColor("dfd0b4", Color.grey), KomiksUi.SandBorder, 9, 2);
            Stretch(lpTrackImg.rectTransform, 0f);
            lpBarImg = Ui.MakeImage("Fill", lpTrack, ColGold);
            lpBarFill = lpBarImg.rectTransform;
            lpBarFill.anchorMin = Vector2.zero;
            lpBarFill.anchorMax = new Vector2(0.2f, 1f);
            lpBarFill.offsetMin = new Vector2(2f, 2f);
            lpBarFill.offsetMax = new Vector2(-2f, -2f);

            lpDetailText = Ui.MakeText("LpDetail", rankCardRect, "15 / 200 LP · Next: Bakal", 16, ColWarmSubText, TextAnchor.MiddleLeft);
            lpDetailText.GetComponent<Outline>().enabled = false;
            lpDetailText.resizeTextForBestFit = true;
            lpDetailText.resizeTextMinSize = 13;
            lpDetailText.resizeTextMaxSize = 17;
            lpDetailText.rectTransform.anchorMin = new Vector2(0f, 0f);
            lpDetailText.rectTransform.anchorMax = new Vector2(1f, 0f);
            lpDetailText.rectTransform.offsetMin = new Vector2(102f, 14f);
            lpDetailText.rectTransform.offsetMax = new Vector2(-16f, 42f);

            // Action buttons: Play Again & Back to Home (>= 48 dp)
            MakeActionButton(
                "PlayAgainButton",
                cardRect,
                "Play Again",
                new Color(0.18f, 0.68f, 0.42f, 1f),
                19,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("confirm");
                    OnPlayAgain?.Invoke();
                },
                out playAgainRect,
                out _,
                out _);
            playAgainRect.anchorMin = playAgainRect.anchorMax = new Vector2(0f, 0f);
            playAgainRect.pivot = new Vector2(0f, 0f);
            playAgainRect.anchoredPosition = new Vector2(24f, 16f);

            MakeActionButton(
                "BackToHomeButton",
                cardRect,
                "Back to Home",
                new Color(0.20f, 0.48f, 0.88f, 1f),
                19,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("back");
                    OnBackToHome?.Invoke();
                },
                out homeRect,
                out _,
                out _);
            homeRect.anchorMin = homeRect.anchorMax = new Vector2(1f, 0f);
            homeRect.pivot = new Vector2(1f, 0f);
            homeRect.anchoredPosition = new Vector2(-24f, 16f);

            root.gameObject.SetActive(false);
        }

        void SyncPhaseOverlays(SimBridge sim, KidSnapshot? localKid, int localTeam, float dt)
        {
            MatchPhaseKind phase = sim.Phase;
            bool phaseChanged = phase != _lastPhase;
            _lastPhase = phase;

            SyncBriefingCard(phase, dt);

            bool showIntro = phase == MatchPhaseKind.Intro && !IsBriefingVisible;
            if (_introRoot.gameObject.activeSelf != showIntro)
            {
                _introRoot.gameObject.SetActive(showIntro);
                if (showIntro)
                    _introInitialSeconds = Mathf.Max(sim.PhaseTimeLeft, 0.1f);
            }
            if (showIntro)
            {
                int ceilSec = Mathf.Max(1, Mathf.CeilToInt(sim.PhaseTimeLeft));
                if (ceilSec != _lastIntroCeil)
                {
                    _lastIntroCeil = ceilSec;
                    _introCountdownText.text = ceilSec.ToString(CultureInfo.InvariantCulture);
                }
                int round = Mathf.Max(1, sim.RoundNumber);
                if (round != _lastIntroRound)
                {
                    _lastIntroRound = round;
                    _introRoundText.text = "ROUND " + round.ToString(CultureInfo.InvariantCulture) + " · Capture the enemy base or tag all enemies to score!";
                }
                _introBarFill.anchorMax = new Vector2(Mathf.Clamp01(sim.PhaseTimeLeft / Mathf.Max(_introInitialSeconds, 0.1f)), 1f);
            }

            bool showFreeze = phase == MatchPhaseKind.PointFreeze;
            if (_pointFreezeRoot.gameObject.activeSelf != showFreeze)
                _pointFreezeRoot.gameObject.SetActive(showFreeze);
            if (showFreeze && phaseChanged)
            {
                int scoringTeam = _lastPointWinnerTeam >= 0 ? _lastPointWinnerTeam : localTeam;
                bool ours = scoringTeam == localTeam;
                CaptureReasonKind how = _lastPointReason != CaptureReasonKind.None ? _lastPointReason : sim.CaptureReason;
                if (how == CaptureReasonKind.AllTagged)
                {
                    _freezeSubjectText.text = ours ? "All enemies" : "All allies";
                    _freezeOutcomeText.text = "TAGGED";
                }
                else
                {
                    _freezeSubjectText.text = ours ? "Enemy base" : "Ally base";
                    _freezeOutcomeText.text = "CAPTURED";
                }
                _freezeOutcomeText.color = ours ? ColGold : KillFeedEnemyColor();
                int myPts = sim.ScoreOf(localTeam);
                int theirPts = sim.ScoreOf(1 - localTeam);
                _freezeScoreText.text = myPts.ToString(CultureInfo.InvariantCulture) + " - " + theirPts.ToString(CultureInfo.InvariantCulture);
            }

            bool showVictory = phase == MatchPhaseKind.MatchOver;
            if (_victoryRoot.gameObject.activeSelf != showVictory)
            {
                _victoryRoot.gameObject.SetActive(showVictory);
                if (showVictory)
                {
                    SetPaused(false);
                    SetQuickChatOpen(false);
                    SetSwapPickerOpen(false);
                    PopulateVictorySummary(sim, localKid, localTeam);
                }
                else
                {
                    _confettiActive = false;
                }
            }

            if (showVictory)
            {
                if (!SettingsStore.Current.ReduceMotion && Mathf.Abs(_lpAnimTo - _lpAnimFrom) > 0.005f && _lpAnimElapsed < 0.55f)
                {
                    _lpAnimElapsed = Mathf.Min(0.55f, _lpAnimElapsed + dt);
                    float t = _lpAnimElapsed / 0.55f;
                    float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                    _rankLpBarFill.anchorMax = new Vector2(Mathf.Lerp(_lpAnimFrom, _lpAnimTo, eased), 1f);
                }

                if (_confettiActive)
                {
                    _confettiElapsed += dt;
                    if (_confettiElapsed > 2.8f)
                    {
                        _confettiActive = false;
                        for (int i = 0; i < ConfettiCount; i++)
                            _confetti[i].Rect.gameObject.SetActive(false);
                    }
                    else
                    {
                        for (int i = 0; i < ConfettiCount; i++)
                        {
                            ConfettiPiece c = _confetti[i];
                            Vector2 pos = c.StartPos + c.Velocity * _confettiElapsed + new Vector2(0f, -140f * _confettiElapsed * _confettiElapsed);
                            c.Rect.anchoredPosition = pos;
                            c.Rect.localEulerAngles = new Vector3(0f, 0f, c.SpinSpeed * _confettiElapsed);
                        }
                    }
                }
            }
        }

        void PopulateVictorySummary(SimBridge sim, KidSnapshot? localKid, int localTeam)
        {
            int myScore = sim.ScoreOf(localTeam);
            int theirScore = sim.ScoreOf(1 - localTeam);
            bool won = sim.WinnerTeam >= 0 ? (sim.WinnerTeam == localTeam) : (myScore >= theirScore);

            _victoryTitleText.text = won ? "Victory" : "Defeat";
            _victoryTitleText.color = won ? ColGold : ColDefeatCoral;
            if (_victoryYouCapText != null) _victoryYouCapText.color = KillFeedOwnColor();
            if (_victoryThemCapText != null) _victoryThemCapText.color = KillFeedEnemyColor();

            int allies = 0;
            int enemies = 0;
            for (int i = 0; i < sim.Kids.Count; i++)
            {
                if (sim.Kids[i].Team == localTeam) allies++;
                else enemies++;
            }
            string modeLabel = sim.IsOnline ? "Online Battle" : (sim.IsTraining ? "Training" : "Agawang Base");
            _victorySubText.text = modeLabel + " (First to " + Mathf.Max(1, sim.PointsToWin).ToString(CultureInfo.InvariantCulture) + ") (" +
                                   Mathf.Max(1, allies).ToString(CultureInfo.InvariantCulture) + "v" + Mathf.Max(1, enemies).ToString(CultureInfo.InvariantCulture) + ")";
            _victoryMyScoreText.text = myScore.ToString(CultureInfo.InvariantCulture);
            _victoryTheirScoreText.text = theirScore.ToString(CultureInfo.InvariantCulture);

            string kidId = (localKid.HasValue && !string.IsNullOrEmpty(localKid.Value.CharacterId))
                ? localKid.Value.CharacterId
                : AppSession.Current.SelectedCharacter;

            RankOutcomeInfo outcome;
            if (!_matchResultRecorded)
            {
                _matchResultRecorded = true;
                RankOutcomeInfo? preRecorded = AppSession.Current.ConsumeLastOutcome();
                if (preRecorded.HasValue)
                {
                    outcome = preRecorded.Value;
                }
                else
                {
                    outcome = AppSession.Current.RecordMatchResult(null, kidId, won, myScore, theirScore);
                    SaveSessionPrefs();
                }
                LastRecordedOutcome = outcome;
                if (won) Audio?.PlayObjectiveEvent("victory");
            }
            else if (LastRecordedOutcome.HasValue)
            {
                outcome = LastRecordedOutcome.Value;
            }
            else
            {
                RankTierInfo cur = AppSession.Current.CurrentTier;
                outcome = new RankOutcomeInfo
                {
                    OldLp = AppSession.Current.Lp,
                    NewLp = AppSession.Current.Lp,
                    Delta = 0,
                    TierId = cur.Id,
                    TierName = cur.Name,
                    Promoted = false,
                };
            }

            RankTierInfo tierSpec = AppSession.Current.CurrentTier;
            Color tierCol = HexColor(tierSpec.ColorHex, ColGold);
            Color tierDark = HexColor(tierSpec.DarkHex, ColWarmText);

            _rankEmblemBg.color = tierCol;
            string tierTitle = !string.IsNullOrEmpty(outcome.TierName) ? outcome.TierName : tierSpec.Name;
            _rankEmblemInitial.text = !string.IsNullOrEmpty(tierTitle) ? tierTitle.Substring(0, 1).ToUpperInvariant() : "K";
            _rankTierNameText.text = (tierTitle ?? "KAHOY").ToUpperInvariant();
            _rankTierNameText.color = Color.Lerp(ColWarmText, tierDark, 0.55f);

            if (outcome.Delta >= 0)
            {
                _rankDeltaText.text = "+" + outcome.Delta.ToString(CultureInfo.InvariantCulture) + " LP";
                _rankDeltaChipImg.sprite = KomiksUi.BorderedSprite(14, 2, ColDeltaPos, KomiksUi.SlateEspresso);
            }
            else
            {
                _rankDeltaText.text = outcome.Delta.ToString(CultureInfo.InvariantCulture) + " LP";
                _rankDeltaChipImg.sprite = KomiksUi.BorderedSprite(14, 2, ColDeltaNeg, KomiksUi.SlateEspresso);
            }
            _rankDeltaChipImg.color = Color.white;

            int nextCeil = AppSession.Current.NextTierLp;
            if (nextCeil > 0)
            {
                int nextIdx = Mathf.Clamp(AppSession.Current.TierIndex + 1, 0, AppSession.RankTiers.Length - 1);
                string nextName = AppSession.RankTiers[nextIdx].Name;
                _rankLpDetailText.text = outcome.NewLp.ToString(CultureInfo.InvariantCulture) + " / " +
                                         nextCeil.ToString(CultureInfo.InvariantCulture) + " LP · Next: " + nextName;
            }
            else
            {
                _rankLpDetailText.text = outcome.NewLp.ToString(CultureInfo.InvariantCulture) + " LP · Top Barrio Rank";
            }

            _promoBannerObj.SetActive(outcome.Promoted);
            _lpAnimTo = Mathf.Clamp01(AppSession.Current.TierProgress01);
            _lpAnimFrom = (SettingsStore.Current.ReduceMotion || outcome.Promoted) ? _lpAnimTo : Mathf.Clamp01(_lpAnimTo - Mathf.Abs(outcome.Delta) / 200f);
            _lpAnimElapsed = 0f;
            _rankLpBarImg.color = tierCol;
            _rankLpBarFill.anchorMax = new Vector2(SettingsStore.Current.ReduceMotion ? _lpAnimTo : _lpAnimFrom, 1f);

            _confettiActive = won && !SettingsStore.Current.ReduceMotion;
            _confettiElapsed = 0f;
            for (int i = 0; i < ConfettiCount; i++)
            {
                _confetti[i].Rect.gameObject.SetActive(_confettiActive);
                if (_confettiActive)
                    _confetti[i].Rect.anchoredPosition = _confetti[i].StartPos;
            }
        }

        // =============================================================================================================================
        // 5b. In-Match Pause & Quick Settings Sheet
        // =============================================================================================================================

        void BuildPauseSheet(
            out RectTransform root,
            out RectTransform dimRect,
            out RectTransform cardRect,
            out RectTransform titleRect,
            out RectTransform closeRect,
            out RectTransform viewportRect,
            out RectTransform contentRect,
            out ScrollRect scroll,
            out RectTransform scrollTrackRect,
            out RectTransform scrollThumbRect,
            out Text musicTxt,
            out Text sfxTxt,
            out Text hapticsTxt,
            out Text leftHandTxt,
            out Text aimGuideTxt,
            out Text reduceMotionTxt,
            out Text fpsTurboTxt,
            out Text colorblindTxt,
            out RectTransform footerRow,
            out RectTransform resumeRect,
            out RectTransform leaveRect,
            out RectTransform confirmRow,
            out RectTransform warnTextRect,
            out RectTransform stayRect,
            out RectTransform confirmLeaveRect)
        {
            root = Ui.Fill("PauseQuickSettingsSheet", Root);
            Image dim = Ui.MakeImage("DimBackdrop", root, new Color(0.02f, 0.04f, 0.10f, 0.84f), raycast: _interactive);
            dimRect = dim.rectTransform;
            Stretch(dimRect, 0f);

            AttachDismissCatcher(root, _interactive, () =>
            {
                Audio?.PlayUiTap("cancel");
                SetPaused(false);
            });

            cardRect = Ui.Panel("SettingsCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 664f));
            Image bg = KomiksUi.MakeInkPanel("Plate", cardRect, ColModalDark, KomiksUi.InkOutline, 18, 2, raycast: _interactive);
            Stretch(bg.rectTransform, 0f);

            Text header = Ui.MakeText("Title", cardRect, "MATCH PAUSE & SETTINGS", 26, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            header.resizeTextForBestFit = true;
            header.resizeTextMinSize = 18;
            header.resizeTextMaxSize = 26;
            titleRect = header.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(22f, -100f);
            titleRect.offsetMax = new Vector2(-132f, -8f);

            MakeActionButton(
                "ClosePauseButton",
                cardRect,
                "X",
                new Color(0.32f, 0.36f, 0.46f, 1f),
                22,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("cancel");
                    SetPaused(false);
                },
                out closeRect,
                out _,
                out _);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -8f);
            closeRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(96f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            // 2-column ScrollRect of >= 48 dp quick-setting toggle pills + non-Selectable Komiks scroll track/thumb indicator
            viewportRect = Ui.Panel("SettingsViewport", cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewportRect.offsetMin = new Vector2(18f, 110f);
            viewportRect.offsetMax = new Vector2(-32f, -108f);
            Image vpHit = Ui.MakeImage("ViewportHit", viewportRect, new Color(0.03f, 0.05f, 0.10f, 0.42f), raycast: _interactive);
            Stretch(vpHit.rectTransform, 0f);
            viewportRect.gameObject.AddComponent<RectMask2D>();

            contentRect = Ui.Panel("SettingsContent", viewportRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 416f));
            scroll = viewportRect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            Image scrollTrackImg = KomiksUi.MakeInkPanel("ScrollTrack", cardRect, new Color(0.03f, 0.05f, 0.11f, 0.88f), KomiksUi.InkOutline, 4, 1, raycast: false);
            scrollTrackRect = scrollTrackImg.rectTransform;
            scrollTrackRect.anchorMin = new Vector2(1f, 0f);
            scrollTrackRect.anchorMax = new Vector2(1f, 1f);
            scrollTrackRect.offsetMin = new Vector2(-24f, 116f);
            scrollTrackRect.offsetMax = new Vector2(-16f, -114f);

            Image scrollThumbImg = KomiksUi.MakeSlicedFill("ScrollThumb", scrollTrackRect, ColGold, 3);
            scrollThumbRect = scrollThumbImg.rectTransform;
            scrollThumbRect.anchorMin = new Vector2(0f, 0f);
            scrollThumbRect.anchorMax = new Vector2(1f, 1f);
            scrollThumbRect.offsetMin = new Vector2(1f, 1f);
            scrollThumbRect.offsetMax = new Vector2(-1f, -1f);
            scroll.onValueChanged.AddListener(_ => UpdatePauseScrollIndicator());

            musicTxt = BuildSettingPill(contentRect, 0, () =>
            {
                SettingsStore.Current.MusicEnabled = !SettingsStore.Current.MusicEnabled;
                PersistSettings();
            }, out _pausePillRects[0]);
            sfxTxt = BuildSettingPill(contentRect, 1, () =>
            {
                SettingsStore.Current.SoundsEnabled = !SettingsStore.Current.SoundsEnabled;
                PersistSettings();
            }, out _pausePillRects[1]);
            hapticsTxt = BuildSettingPill(contentRect, 2, () =>
            {
                SettingsStore.Current.Haptics = !SettingsStore.Current.Haptics;
                Haptics.Enabled = SettingsStore.Current.Haptics;
                PersistSettings();
            }, out _pausePillRects[2]);
            leftHandTxt = BuildSettingPill(contentRect, 3, () =>
            {
                SettingsStore.Current.LeftHanded = !SettingsStore.Current.LeftHanded;
                PersistSettings();
            }, out _pausePillRects[3]);
            aimGuideTxt = BuildSettingPill(contentRect, 4, () =>
            {
                SettingsStore.Current.AimGuide = !SettingsStore.Current.AimGuide;
                PersistSettings();
            }, out _pausePillRects[4]);
            reduceMotionTxt = BuildSettingPill(contentRect, 5, () =>
            {
                SettingsStore.Current.ReduceMotion = !SettingsStore.Current.ReduceMotion;
                PersistSettings();
            }, out _pausePillRects[5]);
            fpsTurboTxt = BuildSettingPill(contentRect, 6, () =>
            {
                if (SettingsStore.Current.TargetFrameRate == 60 && !SettingsStore.Current.IsTurboGraphics)
                {
                    SettingsStore.Current.GraphicsQuality = 0;
                }
                else if (SettingsStore.Current.IsTurboGraphics)
                {
                    SettingsStore.Current.GraphicsQuality = 1;
                    SettingsStore.Current.TargetFrameRate = 30;
                }
                else
                {
                    SettingsStore.Current.GraphicsQuality = 1;
                    SettingsStore.Current.TargetFrameRate = 60;
                }
                Application.targetFrameRate = SettingsStore.Current.TargetFrameRate;
                PersistSettings();
            }, out _pausePillRects[6]);
            colorblindTxt = BuildSettingPill(contentRect, 7, () =>
            {
                SettingsStore.Current.ColorblindMode = (SettingsStore.Current.ColorblindMode + 1) % 4;
                PersistSettings();
            }, out _pausePillRects[7]);

            // Default bottom footer row: Resume & Leave battle (swaps cleanly with LeaveConfirmRow so zero footer overlap!)
            footerRow = Ui.Panel("FooterRow", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 120f));

            MakeActionButton(
                "ResumeMatchButton",
                footerRow,
                "Resume",
                new Color(0.18f, 0.68f, 0.42f, 1f),
                19,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("confirm");
                    SetPaused(false);
                },
                out resumeRect,
                out _,
                out _);
            resumeRect.anchorMin = resumeRect.anchorMax = new Vector2(0f, 0.5f);
            resumeRect.pivot = new Vector2(0f, 0.5f);
            resumeRect.anchoredPosition = new Vector2(20f, 0f);
            resumeRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(240f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            MakeActionButton(
                "LeaveMatchButton",
                footerRow,
                "Leave battle",
                new Color(0.82f, 0.24f, 0.20f, 1f),
                19,
                () =>
                {
                    if (_sim != null && _sim.IsTraining)
                    {
                        SetPaused(false);
                        OnBackToHome?.Invoke();
                        return;
                    }
                    Haptics.CancelTick();
                    Audio?.PlayUiTap("cancel");
                    SetPauseLeaveConfirmOpen(true);
                },
                out leaveRect,
                out _,
                out _);
            leaveRect.anchorMin = leaveRect.anchorMax = new Vector2(1f, 0.5f);
            leaveRect.pivot = new Vector2(1f, 0.5f);
            leaveRect.anchoredPosition = new Vector2(-20f, 0f);
            leaveRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(240f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            // Mutually-exclusive Leave confirmation footer (>= 48 dp Stay & Leave buttons, zero overlap with Resume/LeaveMatch)
            confirmRow = Ui.Panel("LeaveConfirmRow", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-36f, 104f));
            Image warnBg = KomiksUi.MakeInkPanel("WarnBg", confirmRow, new Color(0.42f, 0.12f, 0.13f, 0.98f), KomiksUi.InkOutline, 14, 2);
            Stretch(warnBg.rectTransform, 0f);

            Text warnTxt = Ui.MakeText("WarnText", confirmRow, "Leave battle? Counts as a loss.", 16, Ui.Cream, TextAnchor.MiddleLeft);
            warnTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            warnTxt.resizeTextForBestFit = true;
            warnTxt.resizeTextMinSize = 12;
            warnTxt.resizeTextMaxSize = 17;
            warnTextRect = warnTxt.rectTransform;
            warnTextRect.anchorMin = new Vector2(0f, 0f);
            warnTextRect.anchorMax = new Vector2(1f, 1f);
            warnTextRect.offsetMin = new Vector2(16f, 8f);
            warnTextRect.offsetMax = new Vector2(-268f, -8f);

            MakeActionButton(
                "CancelLeaveBtn",
                confirmRow,
                "Stay",
                new Color(0.28f, 0.34f, 0.44f, 1f),
                17,
                () =>
                {
                    Audio?.PlayUiTap("cancel");
                    SetPauseLeaveConfirmOpen(false);
                },
                out stayRect,
                out _,
                out _);
            stayRect.anchorMin = stayRect.anchorMax = new Vector2(1f, 0.5f);
            stayRect.pivot = new Vector2(1f, 0.5f);
            stayRect.anchoredPosition = new Vector2(-138f, 0f);
            stayRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(118f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);

            MakeActionButton(
                "ConfirmLeaveBtn",
                confirmRow,
                "Leave",
                new Color(0.84f, 0.24f, 0.20f, 1f),
                17,
                () =>
                {
                    Haptics.CancelTick();
                    Audio?.PlayUiTap("back");
                    SetPaused(false);
                    OnBackToHome?.Invoke();
                },
                out confirmLeaveRect,
                out _,
                out _);
            confirmLeaveRect.anchorMin = confirmLeaveRect.anchorMax = new Vector2(1f, 0.5f);
            confirmLeaveRect.pivot = new Vector2(1f, 0.5f);
            confirmLeaveRect.anchoredPosition = new Vector2(-10f, 0f);
            confirmLeaveRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(118f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            confirmRow.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
        }

        void LayoutPauseSheet(float sheetW, float sheetH, float minTouch)
        {
            Vector2 closeSize = new Vector2(minTouch, minTouch);
            Vector2 mainBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2((sheetW - 56f) * 0.5f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            Vector2 confirmBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(116f, 96f), ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(108f, closeSize.y + 12f);
            float footerH = Mathf.Max(110f, Mathf.Max(mainBtnSize.y, confirmBtnSize.y) + 14f);

            if (_pauseCloseRect != null)
            {
                _pauseCloseRect.sizeDelta = closeSize;
                _pauseCloseRect.anchoredPosition = new Vector2(-18f, -8f);
            }
            if (_pauseTitleRect != null)
            {
                _pauseTitleRect.offsetMin = new Vector2(22f, -(headerH - 8f));
                _pauseTitleRect.offsetMax = new Vector2(-(closeSize.x + 30f), -8f);
            }
            if (_pauseFooterRow != null)
            {
                _pauseFooterRow.sizeDelta = new Vector2(0f, footerH);
                _pauseFooterRow.anchoredPosition = Vector2.zero;
            }
            if (_pauseResumeRect != null)
            {
                _pauseResumeRect.sizeDelta = mainBtnSize;
                _pauseResumeRect.anchoredPosition = new Vector2(20f, 0f);
            }
            if (_pauseLeaveRect != null)
            {
                _pauseLeaveRect.sizeDelta = mainBtnSize;
                _pauseLeaveRect.anchoredPosition = new Vector2(-20f, 0f);
            }
            if (_pauseConfirmRow != null)
            {
                float rowH = confirmBtnSize.y + 8f;
                _pauseConfirmRow.sizeDelta = new Vector2(-36f, rowH);
                _pauseConfirmRow.anchoredPosition = new Vector2(0f, (footerH - rowH) * 0.5f);
            }
            if (_pauseConfirmLeaveRect != null)
            {
                _pauseConfirmLeaveRect.sizeDelta = confirmBtnSize;
                _pauseConfirmLeaveRect.anchoredPosition = new Vector2(-10f, 0f);
            }
            if (_pauseStayRect != null)
            {
                _pauseStayRect.sizeDelta = confirmBtnSize;
                _pauseStayRect.anchoredPosition = new Vector2(-(10f + confirmBtnSize.x + 10f), 0f);
            }
            if (_pauseWarnTextRect != null)
            {
                float rightUsed = 10f + confirmBtnSize.x * 2f + 10f + 14f;
                _pauseWarnTextRect.offsetMin = new Vector2(16f, 6f);
                _pauseWarnTextRect.offsetMax = new Vector2(-rightUsed, -6f);
            }
            if (_pauseViewportRect != null)
            {
                _pauseViewportRect.offsetMin = new Vector2(18f, footerH);
                _pauseViewportRect.offsetMax = new Vector2(-32f, -headerH);
            }
            if (_pauseScrollTrackRect != null)
            {
                _pauseScrollTrackRect.offsetMin = new Vector2(-24f, footerH + 6f);
                _pauseScrollTrackRect.offsetMax = new Vector2(-16f, -(headerH + 6f));
            }

            const float innerPad = 4f;
            const float gapX = 10f;
            const float gapY = 8f;
            float viewportW = Mathf.Max(300f, sheetW - 50f);
            float pillW = Mathf.Max(minTouch, (viewportW - innerPad * 2f - gapX) * 0.5f);
            float pillH = Mathf.Max(minTouch, 96f);

            for (int i = 0; i < _pausePillRects.Length; i++)
            {
                RectTransform rt = _pausePillRects[i];
                if (rt == null) continue;
                int col = i % 2;
                int row = i / 2;
                rt.sizeDelta = new Vector2(pillW, pillH);
                rt.anchoredPosition = new Vector2(innerPad + col * (pillW + gapX), -(innerPad + row * (pillH + gapY)));
            }

            float contentH = innerPad * 2f + 4f * pillH + 3f * gapY;
            if (_pauseContentRect != null)
                _pauseContentRect.sizeDelta = new Vector2(0f, contentH);

            UpdatePauseScrollIndicator(Mathf.Max(1f, sheetH - headerH - footerH), contentH);
        }

        void UpdatePauseScrollIndicator(float fallbackViewH = 0f, float fallbackContentH = 0f)
        {
            if (_pauseScrollTrackRect == null || _pauseScrollThumbRect == null)
                return;

            float viewH = _pauseViewportRect != null ? _pauseViewportRect.rect.height : 0f;
            if (viewH <= 2f)
            {
                viewH = fallbackViewH > 2f
                    ? fallbackViewH
                    : (_pauseCardRect != null && _pauseViewportRect != null
                        ? Mathf.Max(1f, _pauseCardRect.sizeDelta.y + _pauseViewportRect.offsetMax.y - _pauseViewportRect.offsetMin.y)
                        : 1f);
            }

            float contentH = _pauseContentRect != null && _pauseContentRect.sizeDelta.y > 2f
                ? _pauseContentRect.sizeDelta.y
                : Mathf.Max(viewH, fallbackContentH);
            float ratio = Mathf.Clamp(viewH / Mathf.Max(1f, contentH), 0.20f, 1f);
            float norm = _pauseScroll != null ? Mathf.Clamp01(_pauseScroll.verticalNormalizedPosition) : 1f;
            float bottom = (1f - ratio) * norm;

            _pauseScrollThumbRect.anchorMin = new Vector2(0f, bottom);
            _pauseScrollThumbRect.anchorMax = new Vector2(1f, bottom + ratio);
            _pauseScrollThumbRect.offsetMin = new Vector2(1f, 1f);
            _pauseScrollThumbRect.offsetMax = new Vector2(-1f, -1f);
        }

        Text BuildSettingPill(RectTransform parent, int index, Action onClick, out RectTransform cellRect)
        {
            int col = index % 2;
            int row = index / 2;
            cellRect = Ui.Panel("Setting_" + col + "_" + row, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 96f));

            Image bg = KomiksUi.MakeInkPanel("PillBg", cellRect, new Color(0.12f, 0.18f, 0.30f, 0.98f), KomiksUi.InkOutline, 14, 2, raycast: _interactive);
            Stretch(bg.rectTransform, 0f);

            if (_interactive)
            {
                var btn = cellRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap();
                    onClick?.Invoke();
                });
            }

            Text lbl = Ui.MakeText("Label", cellRect, "", 16, Ui.Cream, TextAnchor.MiddleCenter);
            lbl.resizeTextForBestFit = true;
            lbl.resizeTextMinSize = 13;
            lbl.resizeTextMaxSize = 17;
            Stretch(lbl.rectTransform, 10f);
            return lbl;
        }

        void SyncQuickSettingsLabels()
        {
            SettingsStore s = SettingsStore.Current;
            if (_toggleMusicText != null) _toggleMusicText.text = "Music: " + (s.MusicEnabled ? "ON" : "OFF");
            if (_toggleSfxText != null) _toggleSfxText.text = "SFX: " + (s.SoundsEnabled ? "ON" : "OFF");
            if (_toggleHapticsText != null) _toggleHapticsText.text = "Haptics: " + (s.Haptics ? "ON" : "OFF");
            if (_toggleLeftHandText != null) _toggleLeftHandText.text = "Left-Handed: " + (s.LeftHanded ? "ON" : "OFF");
            if (_toggleAimGuideText != null) _toggleAimGuideText.text = "Aim Guide: " + (s.AimGuide ? "ON" : "OFF");
            if (_toggleReduceMotionText != null) _toggleReduceMotionText.text = "Reduce Motion: " + (s.ReduceMotion ? "ON" : "OFF");
            if (_toggleFpsTurboText != null)
            {
                string mode = s.IsTurboGraphics ? "60 FPS Turbo" : (s.TargetFrameRate.ToString(CultureInfo.InvariantCulture) + " FPS");
                _toggleFpsTurboText.text = "Perf: " + mode;
            }
            if (_toggleColorblindText != null)
            {
                string cb = s.ColorblindMode == 1 ? "Protan" : (s.ColorblindMode == 2 ? "Deutan" : (s.ColorblindMode == 3 ? "Tritan" : "Off"));
                _toggleColorblindText.text = "Colorblind: " + cb;
            }
        }

        void SyncAudioFromSettings()
        {
            if (Audio == null) return;
            SettingsStore s = SettingsStore.Current;
            Audio.BindVolumes(s.MusicVolume, s.SfxVolume, s.MusicEnabled, s.SoundsEnabled, s.MasterVolume);
        }

        void PersistSettings()
        {
            ApplyUrpRenderScale();
            SyncAudioFromSettings();
            SyncQuickSettingsLabels();
            try
            {
                PlayerPrefs.SetString(SettingsStore.PlayerPrefsKey, SettingsStore.Current.ToJson());
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
            }
        }

        static void ApplyUrpRenderScale()
        {
            var urp = (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                      ?? (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            if (urp != null)
                urp.renderScale = SettingsStore.Current.IsTurboGraphics ? SettingsStore.TurboRenderScale : 1f;
        }

        static void SaveSessionPrefs()
        {
            try
            {
                PlayerPrefs.SetString(AppSession.PlayerPrefsKey, AppSession.Current.ToJson());
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
            }
        }

        // =============================================================================================================================
        // Shared UI, Texture, Palette & Procedural Sprite Helpers
        // =============================================================================================================================

        Button MakeIconBadgeButton(
            string name,
            Transform parent,
            Texture2D iconTex,
            string fallbackGlyph,
            Action onClick,
            out RectTransform rect)
        {
            float sz = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);
            rect = Ui.Panel(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(sz, sz));
            Image outerInk = Ui.MakeImage("InkRim", rect, ColInk, round: true, raycast: _interactive);
            Stretch(outerInk.rectTransform, 0f);
            Image ring = Ui.MakeImage("Rim", rect, new Color(0.98f, 0.95f, 0.89f, 0.92f), round: true, raycast: false);
            Stretch(ring.rectTransform, 2f);
            Image disc = Ui.MakeImage("Disc", rect, ColCardDark, round: true);
            Stretch(disc.rectTransform, 4.5f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = outerInk;
            btn.interactable = _interactive;
            if (_interactive && onClick != null)
                btn.onClick.AddListener(() => onClick());

            if (iconTex != null)
            {
                RectTransform iconSlot = Ui.Panel("IconSlot", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Stretch(iconSlot, 14f);
                var go = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                go.transform.SetParent(iconSlot, false);
                var raw = go.GetComponent<RawImage>();
                raw.texture = iconTex;
                raw.raycastTarget = false;
                Stretch(raw.rectTransform, 0f);
                var fitter = go.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 1f;
            }
            else
            {
                Text txt = Ui.MakeText("Glyph", rect, fallbackGlyph, 18, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(txt.rectTransform, 0f);
            }
            return btn;
        }

        Button MakeActionButton(
            string name,
            Transform parent,
            string label,
            Color color,
            int fontSize,
            Action onClick,
            out RectTransform rect,
            out Image bgImage,
            out Text labelText)
        {
            rect = Ui.Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 96f));
            bgImage = KomiksUi.MakeInkPanel("Bg", rect, color, KomiksUi.InkOutline, 14, 2, topGloss: true, raycast: _interactive);
            Stretch(bgImage.rectTransform, 0f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bgImage;
            btn.interactable = _interactive;
            if (_interactive && onClick != null)
                btn.onClick.AddListener(() => onClick());

            labelText = Ui.MakeText("Label", rect, label, fontSize, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 14;
            labelText.resizeTextMaxSize = fontSize;
            Stretch(labelText.rectTransform, 12f);
            return btn;
        }

        static Image MakeSlicedImage(string name, Transform parent, Color color, bool pill = false, bool raycast = false)
        {
            Image img = Ui.MakeImage(name, parent, color, round: false, raycast: raycast);
            img.sprite = pill ? PillSprite() : RoundedCardSprite();
            img.type = Image.Type.Sliced;
            return img;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void ApplyAvatarArt(string characterId, RawImage face, Text fallbackInitial)
        {
            Texture2D tex = !string.IsNullOrEmpty(characterId)
                ? (KalyeahResources.Texture("portraits/faces/" + characterId) ?? KalyeahResources.Texture("portraits/" + characterId))
                : null;
            if (tex != null)
            {
                face.texture = tex;
                face.gameObject.SetActive(true);
                fallbackInitial.gameObject.SetActive(false);
            }
            else
            {
                face.gameObject.SetActive(false);
                fallbackInitial.gameObject.SetActive(true);
                string clean = (characterId ?? "").Trim();
                fallbackInitial.text = clean.Length > 0 ? clean.Substring(0, 1).ToUpperInvariant() : "K";
            }
        }

        static Texture2D LoadWeaponTexture(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            return KalyeahResources.Texture("icons/art/" + weaponId)
                ?? KalyeahResources.Texture("ui/hud_icons/" + weaponId)
                ?? KalyeahResources.Texture("ui/skill_cards/" + weaponId);
        }

        static string DisplayKidName(SimBridge sim, int playerId, KidSnapshot? snap)
        {
            if (sim != null && playerId == sim.LocalPlayerId)
                return "YOU";
            if (snap.HasValue && !string.IsNullOrEmpty(snap.Value.CharacterId))
            {
                string raw = snap.Value.CharacterId.Trim();
                if (raw.Length > 0)
                    return char.ToUpperInvariant(raw[0]) + (raw.Length > 1 ? raw.Substring(1) : "");
            }
            return playerId > 0 ? ("Kid #" + playerId.ToString(CultureInfo.InvariantCulture)) : "Street";
        }

        static string TrimLabel(string raw, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Kid";
            string s = raw.Trim();
            return s.Length <= maxLen ? s : (s.Substring(0, Math.Max(1, maxLen - 1)) + "…");
        }

        static Color OwnTeamColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(0.26f, 0.66f, 1f, 0.96f);
                case 3: return new Color(0.18f, 0.90f, 0.84f, 0.96f);
                default: return new Color(0.32f, 0.68f, 1f, 0.96f);
            }
        }

        static Color EnemyTeamColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(1f, 0.56f, 0.12f, 0.96f);
                case 3: return new Color(1f, 0.32f, 0.47f, 0.96f);
                default: return new Color(0.95f, 0.36f, 0.34f, 0.96f);
            }
        }

        static Color KillFeedOwnColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(0.48f, 0.82f, 1f, 1f);
                case 3: return new Color(0.42f, 0.96f, 0.90f, 1f);
                default: return new Color(0.48f, 0.80f, 1f, 1f);
            }
        }

        static Color KillFeedEnemyColor()
        {
            switch (SettingsStore.Current.ColorblindMode)
            {
                case 1:
                case 2: return new Color(1f, 0.72f, 0.32f, 1f);
                case 3: return new Color(1f, 0.56f, 0.68f, 1f);
                default: return new Color(1f, 0.55f, 0.48f, 1f);
            }
        }

        static Color BuffColorOf(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Palamig: return ColPalamig;
                case BuffKind.Teks: return ColTeks;
                default: return ColTuron;
            }
        }

        static string BuffDisplayName(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Palamig: return "PALAMIG";
                case BuffKind.Teks: return "TEKS";
                case BuffKind.Turon: return "TURON";
                default: return "";
            }
        }

        static Color RoleColorOf(WeaponRoleKind role)
        {
            switch (role)
            {
                case WeaponRoleKind.CrowdControl: return ColTeks;
                case WeaponRoleKind.Block: return ColPalamig;
                case WeaponRoleKind.Heal: return ColRegenMint;
                default: return KillFeedEnemyColor();
            }
        }

        static Color HexColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            string clean = hex[0] == '#' ? hex.Substring(1) : hex;
            if (clean.Length != 6) return fallback;
            if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            {
                float r = ((rgb >> 16) & 0xFFu) / 255f;
                float g = ((rgb >> 8) & 0xFFu) / 255f;
                float b = (rgb & 0xFFu) / 255f;
                return new Color(r, g, b, 1f);
            }
            return fallback;
        }

        static Sprite RoundedCardSprite()
        {
            if (s_roundedCard != null) return s_roundedCard;
            s_roundedCard = BuildRoundedSprite("KalyeahOverlayCard", 64, 64, 12f, new Vector4(14f, 14f, 14f, 14f));
            return s_roundedCard;
        }

        static Sprite PillSprite()
        {
            if (s_pillSprite != null) return s_pillSprite;
            s_pillSprite = BuildRoundedSprite("KalyeahOverlayPill", 64, 32, 14f, new Vector4(16f, 14f, 16f, 14f));
            return s_pillSprite;
        }

        static Sprite BuildRoundedSprite(string name, int w, int h, float radius, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[w * h];
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float innerW = halfW - radius;
            float innerH = halfH - radius;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - halfW) - innerW);
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - halfH) - innerH);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float edgeAlpha = Mathf.Clamp01(radius - dist);
                    byte lum = (dist > radius - 2.0f) ? (byte)255 : (byte)238;
                    px[y * w + x] = new Color32(lum, lum, lum, (byte)(edgeAlpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
            sp.name = name;
            return sp;
        }

        static Sprite WarningTriangleSprite()
        {
            if (s_warningSprite != null) return s_warningSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahWarningTriangle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            var yellow = new Color32(255, 210, 31, 255);
            var dark = new Color32(11, 11, 15, 255);

            Vector2 a = new Vector2(32f, 56f);
            Vector2 b = new Vector2(8f, 10f);
            Vector2 c = new Vector2(56f, 10f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float sd = SignedDistTriangle(p, a, b, c);
                    if (sd > 4.5f)
                    {
                        px[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    float alpha = Mathf.Clamp01(4.5f - sd);
                    bool inYellow = sd <= 0.6f;

                    // Exclamation bar and dot inside yellow body
                    float exX = Mathf.Abs(p.x - 32f);
                    bool inBar = exX <= 3.0f && p.y >= 24f && p.y <= 41f;
                    bool inDot = Vector2.Distance(p, new Vector2(32f, 16.5f)) <= 3.4f;

                    Color32 col = (inYellow && !inBar && !inDot) ? yellow : dark;
                    col.a = (byte)(alpha * 255f);
                    px[y * size + x] = col;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_warningSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            s_warningSprite.name = "KalyeahWarningTriangle";
            return s_warningSprite;
        }

        static Sprite EdgeGradientSprite()
        {
            if (s_edgeGradientSprite != null) return s_edgeGradientSprite;
            const int w = 4;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KalyeahHurtEdgeGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                float falloff = (1f - t) * (1f - t);
                byte a = (byte)Mathf.RoundToInt(falloff * 255f);
                var c = new Color32(255, 255, 255, a);
                for (int x = 0; x < w; x++)
                    px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_edgeGradientSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            s_edgeGradientSprite.name = "KalyeahHurtEdgeGradient";
            return s_edgeGradientSprite;
        }

        static Sprite ChevronSprite()
        {
            if (s_chevronSprite != null) return s_chevronSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahWaypointChevron",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            Vector2 tip = new Vector2(16f, 26f);
            Vector2 bl = new Vector2(5f, 10f);
            Vector2 br = new Vector2(27f, 10f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float sd = SignedDistTriangle(p, tip, bl, br);
                    float a = Mathf.Clamp01(1.5f - sd);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_chevronSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            s_chevronSprite.name = "KalyeahWaypointChevron";
            return s_chevronSprite;
        }

        static float SignedDistTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(
                Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }
    }
}
