using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full match HUD overlays on the 1280x720 SafeArea-pinned landscape canvas:
    /// 1. Local Hero Vitals chip (HP + damage trail + Stamina/Overclock + Merienda buff icon/timer) &amp; 5v5 Squad Wings (enemy HP hidden).
    /// 2. Top-right TakedownFeed (3 recent tags/rescues, auto-fading) &amp; CaptureAlert banner (0.4s hysteresis anti-flicker).
    /// 3. Tagged Sidewalk Overlay (rescue timer + teammate pips + D13 3-superpower loadout swap picker via SimBridge.TrySwapLoadout).
    /// 4. Quick-Chat / Sticker wheel + speech chips (SimBridge.SendQuickChat / QuickChatSaid).
    /// 5. Round Intro, Point Freeze, Match Victory &amp; Ranked LP summary, and In-Match Pause &amp; Quick Settings Sheet (+ Android Back toggle).
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

        public const int MaxTakedownRows = 3;
        public const int SquadWingSlots = 5;
        public const int SpeechPoolSize = 4;
        public const int MaxSwapWeaponTiles = 16;
        public const int ConfettiCount = 18;

        public static readonly string[] StickerPhrases =
        {
            "Haha!", "Easy!", "Grrr!", "Love it!", "Huhu!", "Yum!",
        };

        public static readonly string[] StickerSlugs =
        {
            "haha", "easy", "grrr", "love_it", "huhu", "yum",
        };

        static readonly Color ColInk = new Color(0.05f, 0.07f, 0.12f, 0.92f);
        static readonly Color ColCardDark = new Color(0.06f, 0.09f, 0.16f, 0.86f);
        static readonly Color ColCardWarm = new Color(0.98f, 0.95f, 0.89f, 0.98f);
        static readonly Color ColWarmText = new Color(0.16f, 0.14f, 0.13f, 1f);
        static readonly Color ColGold = new Color(1f, 0.80f, 0.24f, 1f);
        static readonly Color ColHpWhite = new Color(0.98f, 0.99f, 1f, 0.97f);
        static readonly Color ColMissingCoral = new Color(0.89f, 0.24f, 0.17f, 0.88f);
        static readonly Color ColTrailAmber = new Color(1f, 0.78f, 0.58f, 0.92f);
        static readonly Color ColRegenMint = new Color(0.28f, 0.92f, 0.56f, 0.96f);
        static readonly Color ColStamina = new Color(0.32f, 0.86f, 0.98f, 0.95f);
        static readonly Color ColOverclock = new Color(1f, 0.84f, 0.26f, 0.98f);
        static readonly Color ColPalamig = new Color(0.26f, 0.88f, 0.96f, 0.96f);
        static readonly Color ColTeks = new Color(0.98f, 0.74f, 0.22f, 0.96f);
        static readonly Color ColTuron = new Color(0.32f, 0.90f, 0.56f, 0.96f);

        static Sprite s_roundedCard;
        static Sprite s_pillSprite;
        static Sprite s_warningSprite;

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
            public MatchOverlaysView Owner;

            void Update()
            {
                Owner?.PollHardwareBackKey();
            }
        }

        readonly Canvas _canvas;
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
        public bool IsTaggedOverlayVisible => _taggedRoot != null && _taggedRoot.gameObject.activeSelf;
        public bool IsCaptureAlertVisible => _captureAlertRoot != null && _captureAlertRoot.gameObject.activeSelf;
        public bool IsLeaveConfirmOpen => _pauseConfirmRow != null && _pauseConfirmRow.gameObject.activeSelf;
        public RankOutcomeInfo? LastRecordedOutcome { get; private set; }

        public bool IsBlockingGameplayInput =>
            IsPaused || IsSwapPickerOpen || _lastPhase == MatchPhaseKind.Intro || _lastPhase == MatchPhaseKind.MatchOver;

        // Bound sim & transient tracking
        SimBridge _sim;
        MatchPhaseKind _lastPhase = (MatchPhaseKind)(-1);
        bool _matchResultRecorded;
        int _lastBackFrame = -1;

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

        readonly RectTransform _captureAlertRoot;
        readonly RectTransform _captureIconRect;
        readonly Image _capturePlateImg;
        readonly RectTransform _captureProgFill;
        float _captureHoldLeft;
        float _capturePulseTime;
        bool _wasRawCapturing;

        // 3. Tagged Sidewalk Overlay & D13 Swap Picker
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
        readonly RectTransform _swapCardRect;
        readonly SwapTileUi[] _swapTiles = new SwapTileUi[MaxSwapWeaponTiles];
        readonly List<string> _swapPicks = new List<string>(3);
        readonly Button _swapConfirmBtn;
        readonly Image _swapConfirmImg;
        readonly Text _swapConfirmText;
        int _activeSwapTileCount;

        // 4. Quick-Chat button, Sticker wheel & Speech chips
        readonly RectTransform _quickChatBtnRect;
        readonly Button _quickChatBtn;
        readonly RectTransform _quickChatModalRoot;
        readonly RectTransform _quickChatCardRect;
        readonly RectTransform[] _stickerBtnRects = new RectTransform[6];
        readonly SpeechBubbleUi[] _speechPool = new SpeechBubbleUi[SpeechPoolSize];

        // 5a. Top-left Pause Button & Pause Quick-Settings Sheet
        readonly RectTransform _pauseBtnRect;
        readonly Button _pauseBtn;
        readonly RectTransform _pauseSheetRoot;
        readonly RectTransform _pauseCardRect;
        readonly Text _toggleMusicText;
        readonly Text _toggleSfxText;
        readonly Text _toggleHapticsText;
        readonly Text _toggleLeftHandText;
        readonly Text _toggleAimGuideText;
        readonly Text _toggleReduceMotionText;
        readonly Text _toggleFpsTurboText;
        readonly Text _toggleColorblindText;
        readonly RectTransform _pauseConfirmRow;

        // 5b. Intro, Point Freeze & Match Victory / Ranked LP Summary
        readonly RectTransform _introRoot;
        readonly Text _introRoundText;
        readonly Text _introCountdownText;
        readonly RectTransform _introBarFill;
        float _introInitialSeconds = 10f;
        int _lastIntroCeil = -1;
        int _lastIntroRound = -1;

        readonly RectTransform _pointFreezeRoot;
        readonly Text _freezeSubjectText;
        readonly Text _freezeOutcomeText;
        readonly Text _freezeScoreText;
        int _lastPointWinnerTeam = -1;
        CaptureReasonKind _lastPointReason = CaptureReasonKind.None;

        readonly RectTransform _victoryRoot;
        readonly RectTransform _victoryCardRect;
        readonly Text _victoryTitleText;
        readonly Text _victorySubText;
        readonly Text _victoryMyScoreText;
        readonly Text _victoryTheirScoreText;
        readonly GameObject _promoBannerObj;
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
            BuildQuickChatModal(out _quickChatModalRoot, out _quickChatCardRect);
            BuildSwapPickerModal(
                out _swapModalRoot, out _swapCardRect, out _swapConfirmBtn, out _swapConfirmImg, out _swapConfirmText);
            BuildPointFreezeOverlay(out _pointFreezeRoot, out _freezeSubjectText, out _freezeOutcomeText, out _freezeScoreText);
            BuildIntroOverlay(out _introRoot, out _introRoundText, out _introCountdownText, out _introBarFill);
            BuildVictoryOverlay(
                out _victoryRoot, out _victoryCardRect, out _victoryTitleText, out _victorySubText,
                out _victoryMyScoreText, out _victoryTheirScoreText, out _promoBannerObj,
                out _rankEmblemBg, out _rankEmblemInitial, out _rankTierNameText,
                out _rankDeltaChipImg, out _rankDeltaText, out _rankLpBarFill, out _rankLpBarImg,
                out _rankLpDetailText, out _playAgainBtnRect, out _homeBtnRect);
            BuildPauseSheet(
                out _pauseSheetRoot, out _pauseCardRect,
                out _toggleMusicText, out _toggleSfxText, out _toggleHapticsText, out _toggleLeftHandText,
                out _toggleAimGuideText, out _toggleReduceMotionText, out _toggleFpsTurboText,
                out _toggleColorblindText, out _pauseConfirmRow);

            ScreenFitMetrics.SimulatedViewportChanged += OnExternalLayoutOrSettingsChanged;
            SettingsStore.Current.Changed += OnExternalLayoutOrSettingsChanged;

            BindSim(sim);
            SyncQuickSettingsLabels();
            RefreshLayout();
            if (sim != null) Refresh(sim, 0f);
        }

        public void Dispose()
        {
            ScreenFitMetrics.SimulatedViewportChanged -= OnExternalLayoutOrSettingsChanged;
            SettingsStore.Current.Changed -= OnExternalLayoutOrSettingsChanged;
            UnbindSim();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        void OnExternalLayoutOrSettingsChanged()
        {
            if (Root == null) return;
            Haptics.Enabled = SettingsStore.Current.Haptics;
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
            _captureHoldLeft = 0f;
            _wasRawCapturing = false;
            ClearTakedowns();
            ClearSpeechBubbles();
            SetSwapPickerOpen(false);
            SetQuickChatOpen(false);
            SetPaused(false);

            if (_sim != null)
            {
                _sim.Tagged += OnSimTagged;
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

            SyncHeroVitals(sim, localKid, safeDt);
            SyncSquadWings(sim, localId, localTeam);
            TickTakedownFeed(safeDt);
            SyncCaptureAlert(sim, localTeam, safeDt);
            SyncTaggedOverlay(sim, localKid, localTeam);
            TickSpeechBubbles(sim, safeDt);
            SyncPhaseOverlays(sim, localKid, localTeam, safeDt);
        }

        /// <summary>Responsive layout pass clamping 5v5 wing spread, modal bounds, and >= 48 dp touch floors across 21:9..4:3.</summary>
        public void RefreshLayout()
        {
            if (Root == null) return;
            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);

            if (_pauseBtnRect != null)
            {
                _pauseBtnRect.sizeDelta = new Vector2(minTouch, minTouch);
                _pauseBtnRect.anchoredPosition = new Vector2(12f, -12f);
            }
            if (_quickChatBtnRect != null)
            {
                _quickChatBtnRect.sizeDelta = new Vector2(minTouch, minTouch);
                _quickChatBtnRect.anchoredPosition = new Vector2(-12f, -12f);
            }

            float topControlsBottom = -(minTouch + 22f);
            if (_heroVitalsRoot != null)
                _heroVitalsRoot.anchoredPosition = new Vector2(12f, topControlsBottom);
            if (_takedownRoot != null)
                _takedownRoot.anchoredPosition = new Vector2(-12f, topControlsBottom);

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
                float trayW = Mathf.Clamp(safeSize.x - 36f, 320f, 660f);
                _taggedRoot.sizeDelta = new Vector2(trayW, 140f);
            }
            if (_openSwapRect != null)
                _openSwapRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(204f, 52f), ScreenFitMetrics.MinTouchDp, _canvas);

            if (_quickChatCardRect != null)
            {
                float stickerCellW = Mathf.Max(118f, minTouch + 24f);
                float stickerCellH = Mathf.Max(92f, minTouch + 16f);
                const float pad = 14f;
                const float gap = 10f;
                float cardW = stickerCellW * 2f + gap + pad * 2f;
                float cardH = stickerCellH * 3f + gap * 2f + pad * 2f;
                _quickChatCardRect.anchoredPosition = new Vector2(-12f, -(minTouch + 18f));
                _quickChatCardRect.sizeDelta = new Vector2(cardW, Mathf.Min(cardH, safeSize.y - minTouch - 28f));

                float cellH = (_quickChatCardRect.sizeDelta.y - pad * 2f - gap * 2f) / 3f;
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
                float modalW = Mathf.Clamp(safeSize.x - 40f, 360f, 740f);
                float modalH = Mathf.Clamp(safeSize.y - 32f, 300f, 460f);
                _swapCardRect.sizeDelta = new Vector2(modalW, modalH);
                LayoutSwapGrid(modalW, modalH);
            }

            if (_pauseCardRect != null)
            {
                float sheetW = Mathf.Clamp(safeSize.x - 48f, 340f, 600f);
                float sheetH = Mathf.Clamp(safeSize.y - 32f, 300f, 456f);
                _pauseCardRect.sizeDelta = new Vector2(sheetW, sheetH);
            }

            if (_victoryCardRect != null)
            {
                float vicW = Mathf.Clamp(safeSize.x - 48f, 340f, 580f);
                float vicH = Mathf.Clamp(safeSize.y - 28f, 310f, 470f);
                _victoryCardRect.sizeDelta = new Vector2(vicW, vicH);
            }
            if (_playAgainBtnRect != null)
                _playAgainBtnRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(220f, 58f), ScreenFitMetrics.MinTouchDp, _canvas);
            if (_homeBtnRect != null)
                _homeBtnRect.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(220f, 58f), ScreenFitMetrics.MinTouchDp, _canvas);
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

        /// <summary>Android Back / Escape handler closing transient modals first, otherwise toggling the Pause sheet.</summary>
        public bool HandleBack()
        {
            if (Application.isPlaying)
                _lastBackFrame = Time.frameCount;

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
                _pauseConfirmRow.gameObject.SetActive(false);
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
            if (_pauseConfirmRow != null)
                _pauseConfirmRow.gameObject.SetActive(false);
            if (_pauseSheetRoot != null)
                _pauseSheetRoot.gameObject.SetActive(paused);

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
        }

        /// <summary>Applies a D13 superpower swap through SimBridge.TrySwapLoadout and updates AppSession.Current.</summary>
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
            for (int i = 0; i < _takedownRows.Length; i++)
                if (_takedownRows[i].Root != null)
                    _takedownRows[i].Root.gameObject.SetActive(false);
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
            Image card = MakeSlicedImage("Plate", root, ColCardDark, raycast: false);
            Stretch(card.rectTransform, 0f);

            RectTransform avatarBox = Ui.Panel("Avatar", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(46f, 46f));
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

            // Numeric HP + Status tag row
            hpText = Ui.MakeText("HpText", root, "100 / 100", 17, ColHpWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            RectTransform hpTextRt = hpText.rectTransform;
            hpTextRt.anchorMin = hpTextRt.anchorMax = new Vector2(0f, 1f);
            hpTextRt.pivot = new Vector2(0f, 1f);
            hpTextRt.anchoredPosition = new Vector2(62f, -6f);
            hpTextRt.sizeDelta = new Vector2(92f, 22f);

            statusText = Ui.MakeText("StatusTag", root, "", 13, ColGold, TextAnchor.MiddleRight);
            RectTransform stRt = statusText.rectTransform;
            stRt.anchorMin = stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(1f, 1f);
            stRt.anchoredPosition = new Vector2(-8f, -7f);
            stRt.sizeDelta = new Vector2(62f, 20f);

            // HP bar with missing-coral + hit-trail + live white/mint fill
            RectTransform hpTrack = Ui.Panel("HpBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(27f, -30f), new Vector2(-70f, 13f));
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
            RectTransform stamTrack = Ui.Panel("StaminaBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(27f, -46f), new Vector2(-70f, 7f));
            Image stamBg = Ui.MakeImage("StamTrack", stamTrack, ColInk);
            Stretch(stamBg.rectTransform, 0f);
            stamImg = Ui.MakeImage("StamFill", stamTrack, ColStamina);
            stamFill = stamImg.rectTransform;
            stamFill.anchorMin = Vector2.zero;
            stamFill.anchorMax = Vector2.one;
            stamFill.sizeDelta = Vector2.zero;

            // Active Merienda Buff badge (Palamig, Teks, Turon)
            RectTransform buffRect = Ui.Panel("MeriendaBuff", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(27f, 4f), new Vector2(-70f, 17f));
            buffBadge = buffRect.gameObject;
            Image buffPlate = MakeSlicedImage("BuffPlate", buffRect, new Color(0.04f, 0.06f, 0.10f, 0.78f), pill: true);
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
            if (!localKid.HasValue)
            {
                _heroVitalsRoot.gameObject.SetActive(false);
                return;
            }
            if (!_heroVitalsRoot.gameObject.activeSelf)
                _heroVitalsRoot.gameObject.SetActive(true);

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
                : (kid.ExhaustLeft > 0.01f ? ColMissingCoral : ColStamina);

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
            if (kid.Tagged) { color = ColMissingCoral; return "OUT"; }
            if (kid.Overclocked) { color = ColOverclock; return "OVERCLOCK"; }
            if (kid.StatusKind == StatusKind.Stun || kid.StatusKind == StatusKind.Knockout || kid.StatusKind == StatusKind.Polymorph)
            {
                color = ColTeks;
                return "STUN";
            }
            if (kid.StatusKind == StatusKind.Tripped) { color = ColMissingCoral; return "TRIPPED"; }
            if (kid.ExhaustLeft > 0.01f) { color = ColMissingCoral; return "EXHAUST"; }
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
            Text crossTxt = Ui.MakeText("X", crossObj.transform, "X", 20, ColMissingCoral, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
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
                ? new Color(1f, 1f, 1f, 0.25f)
                : (isMe ? ColHpWhite : teamColor);
            if (ui.Face.gameObject.activeSelf)
                ui.Face.color = isOut ? new Color(0.45f, 0.48f, 0.54f, 0.50f) : Color.white;
            ui.Initial.color = isOut ? new Color(1f, 1f, 1f, 0.45f) : Color.white;

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
                    ui.HpFillImage.color = share <= 0.30f ? ColMissingCoral : (share <= 0.60f ? ColOverclock : ColRegenMint);
            }
        }

        // =============================================================================================================================
        // 2. TakedownFeed (Top-Right 3 Recent Tags) & CaptureAlert (0.4s Hysteresis Anti-Flicker)
        // =============================================================================================================================

        void BuildTakedownFeed(out RectTransform root)
        {
            const float rowH = 28f;
            const float gap = 5f;
            float totalH = MaxTakedownRows * rowH + (MaxTakedownRows - 1) * gap;
            root = Ui.Panel("TakedownFeed", Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -80f), new Vector2(246f, totalH));

            for (int i = 0; i < MaxTakedownRows; i++)
            {
                RectTransform row = Ui.Panel("Row_" + i, root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * (rowH + gap)), new Vector2(0f, rowH));
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Image bg = MakeSlicedImage("Pill", row, new Color(0.06f, 0.08f, 0.14f, 0.72f), pill: true);
                Stretch(bg.rectTransform, 0f);

                Image accent = Ui.MakeImage("Accent", row, OwnTeamColor());
                accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                accent.rectTransform.anchoredPosition = new Vector2(4f, 0f);
                accent.rectTransform.sizeDelta = new Vector2(4f, -10f);

                Text left = Ui.MakeText("Left", row, "", 13, OwnTeamColor(), TextAnchor.MiddleLeft);
                left.rectTransform.anchorMin = new Vector2(0f, 0f);
                left.rectTransform.anchorMax = new Vector2(0.40f, 1f);
                left.rectTransform.offsetMin = new Vector2(13f, 0f);
                left.rectTransform.offsetMax = Vector2.zero;

                Text badge = Ui.MakeText("Badge", row, "TAG", 11, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badge.rectTransform.anchorMin = new Vector2(0.40f, 0f);
                badge.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                badge.rectTransform.sizeDelta = Vector2.zero;

                Text right = Ui.MakeText("Right", row, "", 13, EnemyTeamColor(), TextAnchor.MiddleRight);
                right.rectTransform.anchorMin = new Vector2(0.62f, 0f);
                right.rectTransform.anchorMax = new Vector2(1f, 1f);
                right.rectTransform.offsetMin = Vector2.zero;
                right.rectTransform.offsetMax = new Vector2(-10f, 0f);

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
            int localTeam = _sim.LocalPlayerTeam;
            KidSnapshot? victim = _sim.Find(victimId);
            KidSnapshot? tagger = _sim.Find(taggerId);
            bool allyScored = tagger.HasValue ? tagger.Value.Team == localTeam : (victim.HasValue && victim.Value.Team != localTeam);
            string taggerName = DisplayKidName(_sim, taggerId, tagger);
            string victimName = DisplayKidName(_sim, victimId, victim);
            PushTag(taggerName, victimName, allyScored);
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
            RenderTakedownRows();
        }

        void TickTakedownFeed(float dt)
        {
            if (_takedownEntries.Count == 0) return;
            if (dt > 0f)
            {
                for (int i = _takedownEntries.Count - 1; i >= 0; i--)
                {
                    TakedownEntry e = _takedownEntries[i];
                    e.Age += dt;
                    if (e.Age >= TakedownLifetimeSeconds)
                        _takedownEntries.RemoveAt(i);
                    else
                        _takedownEntries[i] = e;
                }
            }
            RenderTakedownRows();
        }

        void RenderTakedownRows()
        {
            const float rowH = 28f;
            const float gap = 5f;
            Color ally = OwnTeamColor();
            Color enemy = EnemyTeamColor();
            bool reduceMotion = SettingsStore.Current.ReduceMotion;

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

        void BuildCaptureAlert(
            out RectTransform root,
            out RectTransform iconRect,
            out Image plateImg,
            out RectTransform progFill)
        {
            // High above the 45%x50% center sightbox (y in [-162..-102] on 720p height-locked canvas).
            root = Ui.Panel("CaptureAlert", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(384f, 60f));
            plateImg = MakeSlicedImage("Bar", root, new Color(0.52f, 0.13f, 0.12f, 0.94f));
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

            bool show = playing && (rawCapturing || _captureHoldLeft > 0f);
            if (_captureAlertRoot.gameObject.activeSelf != show)
            {
                _captureAlertRoot.gameObject.SetActive(show);
                _captureIconRect.localScale = Vector3.one;
            }
            if (!show) return;

            Color baseEnemy = EnemyTeamColor();
            _capturePlateImg.color = new Color(baseEnemy.r * 0.55f, baseEnemy.g * 0.22f, baseEnemy.b * 0.22f, 0.94f);
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
        // 3. Tagged Sidewalk Overlay & D13 3-Superpower Loadout Swap Picker
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
            root = Ui.Panel("TaggedSidewalkOverlay", Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(640f, 140f));
            Image plate = MakeSlicedImage("Plate", root, new Color(0.05f, 0.08f, 0.16f, 0.90f));
            Stretch(plate.rectTransform, 0f);

            titleText = Ui.MakeText("Title", root, "Tagged! Wait for a rescue", 22, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(18f, -10f);
            titleText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            timerText = Ui.MakeText("RescueTimer", root, "READY", 20, ColRegenMint, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            timerText.rectTransform.anchorMin = new Vector2(0.68f, 1f);
            timerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            timerText.rectTransform.pivot = new Vector2(1f, 1f);
            timerText.rectTransform.anchoredPosition = new Vector2(-18f, -10f);
            timerText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            // Rescue / Cooldown progress bar
            RectTransform barBox = Ui.Panel("RescueBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(-36f, 10f));
            Image barBg = MakeSlicedImage("Track", barBox, ColInk, pill: true);
            Stretch(barBg.rectTransform, 0f);
            progImg = Ui.MakeImage("Fill", barBox, ColRegenMint);
            progFill = progImg.rectTransform;
            progFill.anchorMin = Vector2.zero;
            progFill.anchorMax = new Vector2(0f, 1f);
            progFill.sizeDelta = Vector2.zero;

            // Teammate pips on bottom-left + D13 Change Superpowers button on bottom-right
            for (int i = 0; i < _teammatePips.Length; i++)
            {
                RectTransform pip = Ui.Panel("AllyPip_" + i, root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f + i * 44f, 34f), new Vector2(38f, 38f));
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

            hintText = Ui.MakeText("Hint", root, "A teammate beside you frees you in 5 s!", 14, Ui.Cream, TextAnchor.MiddleLeft);
            hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            hintText.rectTransform.anchorMax = new Vector2(0.65f, 0f);
            hintText.rectTransform.pivot = new Vector2(0f, 0f);
            hintText.rectTransform.anchoredPosition = new Vector2(18f, 8f);
            hintText.rectTransform.sizeDelta = new Vector2(0f, 22f);

            swapBtn = MakeActionButton(
                "ChangeSuperpowersBtn",
                root,
                "Change weapons",
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
            swapRect.anchorMin = swapRect.anchorMax = new Vector2(1f, 0f);
            swapRect.pivot = new Vector2(1f, 0f);
            swapRect.anchoredPosition = new Vector2(-16f, 14f);
            swapRect.sizeDelta = new Vector2(204f, 52f);

            root.gameObject.SetActive(false);
        }

        void SyncTaggedOverlay(SimBridge sim, KidSnapshot? localKid, int localTeam)
        {
            if (_taggedRoot == null) return;
            bool isTagged = localKid.HasValue && localKid.Value.Tagged && sim.Phase != MatchPhaseKind.MatchOver;
            if (!isTagged && IsSwapPickerOpen)
                SetSwapPickerOpen(false);

            bool showTray = isTagged && !IsSwapPickerOpen && !IsPaused;
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
                    pip.Ring.color = other.Tagged ? ColMissingCoral : (other.InRescueZone ? ColRegenMint : allyCol);
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
            out RectTransform cardRect,
            out Button confirmBtn,
            out Image confirmImg,
            out Text confirmText)
        {
            modalRoot = Ui.Fill("SwapLoadoutModal", Root);
            Image dim = Ui.MakeImage("DimBackdrop", modalRoot, new Color(0.02f, 0.04f, 0.09f, 0.76f), raycast: _interactive);
            Stretch(dim.rectTransform, 0f);
            if (_interactive)
            {
                var dismiss = dim.gameObject.AddComponent<Button>();
                dismiss.transition = Selectable.Transition.None;
                dismiss.onClick.AddListener(() =>
                {
                    Audio?.PlayUiTap("cancel");
                    SetSwapPickerOpen(false);
                });
            }

            cardRect = Ui.Panel("SwapCard", modalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 440f));
            Image cardBg = MakeSlicedImage("Bg", cardRect, ColCardDark, raycast: _interactive);
            Stretch(cardBg.rectTransform, 0f);

            Text title = Ui.MakeText("Title", cardRect, "SWAP 3 SUPERPOWERS", 24, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(20f, -12f);
            title.rectTransform.sizeDelta = new Vector2(0f, 32f);

            Text sub = Ui.MakeText("Sub", cardRect, "Takes effect as soon as a teammate rescues you (D13)", 13, Ui.Cream, TextAnchor.MiddleLeft);
            sub.rectTransform.anchorMin = new Vector2(0f, 1f);
            sub.rectTransform.anchorMax = new Vector2(0.82f, 1f);
            sub.rectTransform.pivot = new Vector2(0f, 1f);
            sub.rectTransform.anchoredPosition = new Vector2(20f, -42f);
            sub.rectTransform.sizeDelta = new Vector2(0f, 20f);

            Button closeBtn = MakeActionButton(
                "CloseSwapBtn",
                cardRect,
                "Close",
                new Color(0.32f, 0.36f, 0.46f, 1f),
                16,
                () =>
                {
                    Audio?.PlayUiTap("cancel");
                    SetSwapPickerOpen(false);
                },
                out RectTransform closeRt,
                out _,
                out _);
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-16f, -12f);
            closeRt.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(96f, 48f), ScreenFitMetrics.MinTouchDp, _canvas);

            for (int i = 0; i < MaxSwapWeaponTiles; i++)
            {
                int capturedIdx = i;
                RectTransform tileRt = Ui.Panel("WeaponTile_" + i, cardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(150f, 64f));
                Image bg = MakeSlicedImage("Bg", tileRt, new Color(0.11f, 0.15f, 0.24f, 0.96f), raycast: _interactive);
                Stretch(bg.rectTransform, 0f);

                var btn = tileRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnSwapTilePressed(capturedIdx));

                Image roleStrip = Ui.MakeImage("Role", tileRt, ColStamina);
                roleStrip.rectTransform.anchorMin = new Vector2(0f, 0f);
                roleStrip.rectTransform.anchorMax = new Vector2(0f, 1f);
                roleStrip.rectTransform.pivot = new Vector2(0f, 0.5f);
                roleStrip.rectTransform.anchoredPosition = new Vector2(4f, 0f);
                roleStrip.rectTransform.sizeDelta = new Vector2(4f, -10f);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                iconGo.transform.SetParent(tileRt, false);
                var icon = iconGo.GetComponent<RawImage>();
                icon.raycastTarget = false;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(12f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(38f, 38f);

                Text wName = Ui.MakeText("Name", tileRt, "", 14, Ui.Cream, TextAnchor.MiddleLeft);
                wName.horizontalOverflow = HorizontalWrapMode.Wrap;
                wName.rectTransform.anchorMin = Vector2.zero;
                wName.rectTransform.anchorMax = Vector2.one;
                wName.rectTransform.offsetMin = new Vector2(54f, 4f);
                wName.rectTransform.offsetMax = new Vector2(-26f, -4f);

                RectTransform slotBadge = Ui.Panel("SlotBadge", tileRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(22f, 22f));
                Image badgeCircle = Ui.MakeImage("Disc", slotBadge, ColGold, round: true);
                Stretch(badgeCircle.rectTransform, 0f);
                Text badgeTxt = Ui.MakeText("Num", slotBadge, "1", 13, ColInk, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
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
                new Color(0.18f, 0.68f, 0.42f, 1f),
                18,
                () => ConfirmLoadoutSwap(_swapPicks),
                out RectTransform confirmRt,
                out confirmImg,
                out confirmText);
            confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, 12f);
            confirmRt.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(280f, 52f), ScreenFitMetrics.MinTouchDp, _canvas);

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

            // Stand-in fallback so D13 UI remains inspectable even if AllWeapons is empty
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
                LayoutSwapGrid(_swapCardRect.sizeDelta.x, _swapCardRect.sizeDelta.y);
        }

        void LayoutSwapGrid(float modalW, float modalH)
        {
            const int cols = 4;
            const float padX = 18f;
            const float topY = 68f;
            const float bottomY = 74f;
            const float gapX = 10f;
            const float gapY = 8f;

            int count = Mathf.Max(_activeSwapTileCount, 1);
            int rows = Mathf.Max(1, (count + cols - 1) / cols);
            float availW = Mathf.Max(240f, modalW - padX * 2f - gapX * (cols - 1));
            float availH = Mathf.Max(160f, modalH - topY - bottomY - gapY * (rows - 1));
            float cellW = availW / cols;
            float cellH = Mathf.Max(48f, availH / rows);

            for (int i = 0; i < _activeSwapTileCount; i++)
            {
                int c = i % cols;
                int r = i / cols;
                RectTransform rt = _swapTiles[i].Root;
                rt.sizeDelta = new Vector2(cellW, cellH);
                rt.anchoredPosition = new Vector2(padX + c * (cellW + gapX), -(topY + r * (cellH + gapY)));
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
            for (int i = 0; i < _activeSwapTileCount; i++)
            {
                ref SwapTileUi tile = ref _swapTiles[i];
                int slot = _swapPicks.IndexOf(tile.WeaponId);
                bool picked = slot >= 0;
                tile.Bg.color = picked
                    ? new Color(0.20f, 0.34f, 0.56f, 0.98f)
                    : new Color(0.11f, 0.15f, 0.24f, 0.96f);
                tile.SlotBadgeObj.SetActive(picked);
                if (picked)
                    tile.SlotBadgeText.text = (slot + 1).ToString(CultureInfo.InvariantCulture);
            }

            bool ready = _swapPicks.Count == 3 && (_sim == null || _sim.CanSwapLoadout);
            _swapConfirmBtn.interactable = ready;
            _swapConfirmImg.color = ready ? new Color(0.18f, 0.68f, 0.42f, 1f) : new Color(0.28f, 0.32f, 0.40f, 0.85f);
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
            chatRect.anchoredPosition = new Vector2(-12f, -12f);
        }

        void BuildQuickChatModal(out RectTransform modalRoot, out RectTransform cardRect)
        {
            modalRoot = Ui.Fill("QuickChatModal", Root);
            Image catchAll = Ui.MakeImage("DismissCatcher", modalRoot, new Color(0f, 0f, 0f, 0.01f), raycast: _interactive);
            Stretch(catchAll.rectTransform, 0f);
            if (_interactive)
            {
                var dismiss = catchAll.gameObject.AddComponent<Button>();
                dismiss.transition = Selectable.Transition.None;
                dismiss.onClick.AddListener(() => SetQuickChatOpen(false));
            }

            cardRect = Ui.Panel("StickerWheelPanel", modalRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -76f), new Vector2(276f, 324f));
            Image panelBg = MakeSlicedImage("Card", cardRect, ColCardWarm, raycast: _interactive);
            Stretch(panelBg.rectTransform, 0f);

            for (int i = 0; i < StickerPhrases.Length; i++)
            {
                int capturedIndex = i;
                RectTransform cell = Ui.Panel("Sticker_" + StickerSlugs[i], cardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(120f, 94f));
                Image cellBg = MakeSlicedImage("CellBg", cell, new Color(0.92f, 0.89f, 0.82f, 1f), raycast: _interactive);
                Stretch(cellBg.rectTransform, 0f);

                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = cellBg;
                btn.onClick.AddListener(() => TriggerQuickChat(capturedIndex));

                Texture2D stickerTex = KalyeahResources.Texture("ui/stickers/" + StickerSlugs[i]);
                if (stickerTex != null)
                {
                    var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
                    artGo.transform.SetParent(cell, false);
                    var raw = artGo.GetComponent<RawImage>();
                    raw.texture = stickerTex;
                    raw.raycastTarget = false;
                    raw.rectTransform.anchorMin = raw.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    raw.rectTransform.pivot = new Vector2(0.5f, 1f);
                    raw.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                    raw.rectTransform.sizeDelta = new Vector2(58f, 58f);
                }

                Text word = Ui.MakeText("Phrase", cell, StickerPhrases[i], 15, ColWarmText, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
                word.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.5f);
                word.rectTransform.anchorMin = new Vector2(0f, 0f);
                word.rectTransform.anchorMax = new Vector2(1f, 0f);
                word.rectTransform.pivot = new Vector2(0.5f, 0f);
                word.rectTransform.anchoredPosition = new Vector2(0f, 4f);
                word.rectTransform.sizeDelta = new Vector2(-6f, 24f);

                _stickerBtnRects[i] = cell;
            }

            modalRoot.gameObject.SetActive(false);
        }

        void BuildSpeechPool()
        {
            RectTransform holder = Ui.Fill("SpeechChips", Root);
            for (int i = 0; i < SpeechPoolSize; i++)
            {
                RectTransform chip = Ui.Panel("SpeechChip_" + i, holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(158f, 52f));
                var group = chip.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Image border = MakeSlicedImage("Border", chip, OwnTeamColor(), pill: true);
                Stretch(border.rectTransform, 0f);
                Image inner = MakeSlicedImage("Inner", chip, ColCardWarm, pill: true);
                Stretch(inner.rectTransform, 2.5f);

                var artGo = new GameObject("Sticker", typeof(RectTransform), typeof(RawImage));
                artGo.transform.SetParent(chip, false);
                var art = artGo.GetComponent<RawImage>();
                art.raycastTarget = false;
                art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                art.rectTransform.pivot = new Vector2(0f, 0.5f);
                art.rectTransform.anchoredPosition = new Vector2(6f, 0f);
                art.rectTransform.sizeDelta = new Vector2(40f, 40f);

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
            float x = r.width > 0f ? (r.xMax - 92f) : 420f;
            float y = r.height > 0f ? (r.yMax - 236f - slotIndex * 56f) : (120f - slotIndex * 56f);
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
            out Text roundText,
            out Text countdownText,
            out RectTransform barFill)
        {
            root = Ui.Fill("RoundIntroOverlay", Root);
            Image dim = Ui.MakeImage("Backdrop", root, new Color(0.01f, 0.03f, 0.08f, 0.72f), raycast: _interactive);
            Stretch(dim.rectTransform, 0f);
            if (_interactive)
            {
                var tapStart = dim.gameObject.AddComponent<Button>();
                tapStart.transition = Selectable.Transition.None;
                tapStart.onClick.AddListener(() =>
                {
                    if (_sim != null && _sim.SkipIntro())
                    {
                        Haptics.LightTap();
                        Audio?.PlayUiTap("confirm");
                    }
                });
            }

            RectTransform card = Ui.Panel("BriefingCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 270f));
            Image plate = MakeSlicedImage("Plate", card, ColCardDark);
            Stretch(plate.rectTransform, 0f);

            Text title = Ui.MakeText("Title", card, "AGAWANG BASE", 34, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            title.rectTransform.sizeDelta = new Vector2(0f, 42f);

            countdownText = Ui.MakeText("Countdown", card, "10", 36, Color.white, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            countdownText.rectTransform.anchorMin = new Vector2(0.75f, 1f);
            countdownText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countdownText.rectTransform.pivot = new Vector2(1f, 1f);
            countdownText.rectTransform.anchoredPosition = new Vector2(-24f, -16f);
            countdownText.rectTransform.sizeDelta = new Vector2(0f, 44f);

            RectTransform barBox = Ui.Panel("CountdownBar", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-48f, 6f));
            Image barBg = Ui.MakeImage("Track", barBox, new Color(1f, 1f, 1f, 0.22f));
            Stretch(barBg.rectTransform, 0f);
            Image fillImg = Ui.MakeImage("Fill", barBox, Color.white);
            barFill = fillImg.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.sizeDelta = Vector2.zero;

            roundText = Ui.MakeText("RoundInfo", card, "ROUND 1 · Capture enemy base or tag all 5 rivals!", 16, Ui.Cream, TextAnchor.MiddleLeft);
            roundText.horizontalOverflow = HorizontalWrapMode.Wrap;
            roundText.rectTransform.anchorMin = new Vector2(0f, 1f);
            roundText.rectTransform.anchorMax = new Vector2(1f, 1f);
            roundText.rectTransform.pivot = new Vector2(0.5f, 1f);
            roundText.rectTransform.anchoredPosition = new Vector2(0f, -84f);
            roundText.rectTransform.sizeDelta = new Vector2(-48f, 48f);

            Text controls = Ui.MakeText("Controls", card, "MOVE  Left stick / tilt     ·     ATTACK  Tap Tirador or drag Superpower to aim", 14, ColStamina, TextAnchor.MiddleLeft);
            controls.horizontalOverflow = HorizontalWrapMode.Wrap;
            controls.rectTransform.anchorMin = new Vector2(0f, 0f);
            controls.rectTransform.anchorMax = new Vector2(1f, 0f);
            controls.rectTransform.pivot = new Vector2(0.5f, 0f);
            controls.rectTransform.anchoredPosition = new Vector2(0f, 52f);
            controls.rectTransform.sizeDelta = new Vector2(-48f, 44f);

            Text tapHint = Ui.MakeText("TapContinue", card, "Tap to continue", 17, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            tapHint.rectTransform.anchorMin = new Vector2(0f, 0f);
            tapHint.rectTransform.anchorMax = new Vector2(1f, 0f);
            tapHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            tapHint.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            tapHint.rectTransform.sizeDelta = new Vector2(-48f, 28f);

            root.gameObject.SetActive(false);
        }

        void BuildPointFreezeOverlay(
            out RectTransform root,
            out Text subjectText,
            out Text outcomeText,
            out Text scoreText)
        {
            root = Ui.Fill("PointFreezeOverlay", Root);
            Image dim = Ui.MakeImage("Dim", root, new Color(0.02f, 0.04f, 0.10f, 0.48f), raycast: false);
            Stretch(dim.rectTransform, 0f);

            RectTransform box = Ui.Panel("CenterCallout", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 196f));
            Image card = MakeSlicedImage("Plate", box, ColCardDark);
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
            out RectTransform cardRect,
            out Text titleText,
            out Text subText,
            out Text myScoreText,
            out Text theirScoreText,
            out GameObject promoBannerObj,
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
            Image dim = Ui.MakeImage("Backdrop", root, new Color(0.02f, 0.04f, 0.09f, 0.80f), raycast: _interactive);
            Stretch(dim.rectTransform, 0f);

            // Preallocated fiesta banderitas confetti behind the result card (zero runtime instantiation/GC)
            RectTransform confettiLayer = Ui.Fill("ConfettiLayer", root);
            Color[] fiestaPalette =
            {
                new Color(0.96f, 0.65f, 0.14f, 0.95f),
                new Color(0.12f, 0.62f, 0.55f, 0.95f),
                new Color(0.89f, 0.33f, 0.24f, 0.95f),
                new Color(0.98f, 0.95f, 0.89f, 0.95f),
            };
            for (int i = 0; i < ConfettiCount; i++)
            {
                RectTransform p = Ui.Panel("Confetti_" + i, confettiLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 10f));
                Image img = Ui.MakeImage("Paper", p, fiestaPalette[i % fiestaPalette.Length]);
                Stretch(img.rectTransform, 0f);
                p.gameObject.SetActive(false);

                float spread = ((i / (float)(ConfettiCount - 1)) - 0.5f) * 620f;
                _confetti[i] = new ConfettiPiece
                {
                    Rect = p,
                    Image = img,
                    StartPos = new Vector2(spread, 18f + (i % 4) * 14f),
                    Velocity = new Vector2(((i % 5) - 2) * 32f, -210f - (i % 4) * 55f),
                    SpinSpeed = ((i % 2 == 0) ? 1f : -1f) * (140f + (i % 3) * 70f),
                };
            }

            cardRect = Ui.Panel("VictoryCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 448f));
            Image cardBg = MakeSlicedImage("Plate", cardRect, ColCardDark, raycast: _interactive);
            Stretch(cardBg.rectTransform, 0f);

            titleText = Ui.MakeText("Title", cardRect, "VICTORY", 46, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            titleText.rectTransform.sizeDelta = new Vector2(-24f, 54f);

            subText = Ui.MakeText("Subtitle", cardRect, "Agawang Base (First to 3) (5v5)", 15, Ui.Cream, TextAnchor.MiddleCenter);
            subText.rectTransform.anchorMin = new Vector2(0f, 1f);
            subText.rectTransform.anchorMax = new Vector2(1f, 1f);
            subText.rectTransform.pivot = new Vector2(0.5f, 1f);
            subText.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            subText.rectTransform.sizeDelta = new Vector2(-24f, 24f);

            // Score row: YOU  X  -  Y  THEM
            RectTransform scoreRow = Ui.Panel("ScoreRow", cardRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(320f, 66f));
            Text youCap = Ui.MakeText("YouCap", scoreRow, "YOU", 13, OwnTeamColor(), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            youCap.rectTransform.anchorMin = new Vector2(0f, 0.65f);
            youCap.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            youCap.rectTransform.sizeDelta = Vector2.zero;

            myScoreText = Ui.MakeText("MyScore", scoreRow, "3", 40, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            myScoreText.rectTransform.anchorMin = new Vector2(0f, 0f);
            myScoreText.rectTransform.anchorMax = new Vector2(0.42f, 0.72f);
            myScoreText.rectTransform.sizeDelta = Vector2.zero;

            Text sep = Ui.MakeText("Dash", scoreRow, "-", 34, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            sep.rectTransform.anchorMin = new Vector2(0.42f, 0f);
            sep.rectTransform.anchorMax = new Vector2(0.58f, 0.80f);
            sep.rectTransform.sizeDelta = Vector2.zero;

            Text themCap = Ui.MakeText("ThemCap", scoreRow, "THEM", 13, EnemyTeamColor(), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            themCap.rectTransform.anchorMin = new Vector2(0.58f, 0.65f);
            themCap.rectTransform.anchorMax = new Vector2(1f, 1f);
            themCap.rectTransform.sizeDelta = Vector2.zero;

            theirScoreText = Ui.MakeText("TheirScore", scoreRow, "1", 40, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            theirScoreText.rectTransform.anchorMin = new Vector2(0.58f, 0f);
            theirScoreText.rectTransform.anchorMax = new Vector2(1f, 0.72f);
            theirScoreText.rectTransform.sizeDelta = Vector2.zero;

            // Optional "ANGAT RANGGO!" promotion pill
            RectTransform promoRect = Ui.Panel("PromoBanner", cardRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(220f, 28f));
            Image promoBg = MakeSlicedImage("PromoBg", promoRect, ColGold, pill: true);
            Stretch(promoBg.rectTransform, 0f);
            Text promoTxt = Ui.MakeText("PromoText", promoRect, "ANGAT RANGGO!", 15, ColWarmText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            promoTxt.GetComponent<Outline>().enabled = false;
            Stretch(promoTxt.rectTransform, 0f);
            promoBannerObj = promoRect.gameObject;
            promoBannerObj.SetActive(false);

            // Warm sand Tagalog Ranked LP Card
            RectTransform rankCard = Ui.Panel("RankCard", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(-36f, 108f));
            Image rankBg = MakeSlicedImage("WarmCard", rankCard, ColCardWarm);
            Stretch(rankBg.rectTransform, 0f);

            RectTransform emblemBox = Ui.Panel("RankEmblem", rankCard, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(68f, 68f));
            emblemBg = Ui.MakeImage("Crest", emblemBox, HexColor("c8874b", ColGold), round: true);
            Stretch(emblemBg.rectTransform, 0f);
            emblemInitial = Ui.MakeText("EmblemGlyph", emblemBox, "K", 30, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(emblemInitial.rectTransform, 0f);

            tierNameText = Ui.MakeText("TierName", rankCard, "KAHOY", 22, ColWarmText, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            tierNameText.GetComponent<Outline>().enabled = false;
            tierNameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            tierNameText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            tierNameText.rectTransform.pivot = new Vector2(0f, 1f);
            tierNameText.rectTransform.anchoredPosition = new Vector2(96f, -12f);
            tierNameText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            RectTransform deltaBox = Ui.Panel("DeltaChip", rankCard, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(96f, 28f));
            deltaChipImg = MakeSlicedImage("ChipBg", deltaBox, HexColor("136e61", ColRegenMint), pill: true);
            Stretch(deltaChipImg.rectTransform, 0f);
            deltaText = Ui.MakeText("DeltaText", deltaBox, "+15 LP", 14, ColCardWarm, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            deltaText.GetComponent<Outline>().enabled = false;
            Stretch(deltaText.rectTransform, 0f);

            RectTransform lpTrack = Ui.Panel("RankLpBar", rankCard, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(41f, -4f), new Vector2(-110f, 16f));
            Image lpTrackImg = MakeSlicedImage("Track", lpTrack, HexColor("dfd0b4", Color.grey), pill: true);
            Stretch(lpTrackImg.rectTransform, 0f);
            lpBarImg = Ui.MakeImage("Fill", lpTrack, ColGold);
            lpBarFill = lpBarImg.rectTransform;
            lpBarFill.anchorMin = Vector2.zero;
            lpBarFill.anchorMax = new Vector2(0.2f, 1f);
            lpBarFill.sizeDelta = Vector2.zero;

            lpDetailText = Ui.MakeText("LpDetail", rankCard, "15 / 200 LP · Next: Bakal", 13, HexColor("57493f", ColWarmText), TextAnchor.MiddleLeft);
            lpDetailText.GetComponent<Outline>().enabled = false;
            lpDetailText.rectTransform.anchorMin = new Vector2(0f, 0f);
            lpDetailText.rectTransform.anchorMax = new Vector2(1f, 0f);
            lpDetailText.rectTransform.pivot = new Vector2(0.5f, 0f);
            lpDetailText.rectTransform.anchoredPosition = new Vector2(41f, 10f);
            lpDetailText.rectTransform.sizeDelta = new Vector2(-110f, 22f);

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
            playAgainRect.anchoredPosition = new Vector2(24f, 14f);

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
            homeRect.anchoredPosition = new Vector2(-24f, 14f);

            root.gameObject.SetActive(false);
        }

        void SyncPhaseOverlays(SimBridge sim, KidSnapshot? localKid, int localTeam, float dt)
        {
            MatchPhaseKind phase = sim.Phase;
            bool phaseChanged = phase != _lastPhase;
            _lastPhase = phase;

            bool showIntro = phase == MatchPhaseKind.Intro;
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
                _freezeOutcomeText.color = ours ? ColGold : EnemyTeamColor();
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
            _victoryTitleText.color = won ? ColGold : HexColor("f2735c", ColMissingCoral);

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
            _rankTierNameText.color = Color.Lerp(ColWarmText, tierDark, 0.65f);

            if (outcome.Delta >= 0)
            {
                _rankDeltaText.text = "+" + outcome.Delta.ToString(CultureInfo.InvariantCulture) + " LP";
                _rankDeltaChipImg.color = HexColor("136e61", ColRegenMint);
            }
            else
            {
                _rankDeltaText.text = outcome.Delta.ToString(CultureInfo.InvariantCulture) + " LP";
                _rankDeltaChipImg.color = HexColor("b83a28", ColMissingCoral);
            }

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
            out RectTransform cardRect,
            out Text musicTxt,
            out Text sfxTxt,
            out Text hapticsTxt,
            out Text leftHandTxt,
            out Text aimGuideTxt,
            out Text reduceMotionTxt,
            out Text fpsTurboTxt,
            out Text colorblindTxt,
            out RectTransform confirmRow)
        {
            root = Ui.Fill("PauseQuickSettingsSheet", Root);
            Image dim = Ui.MakeImage("DimBackdrop", root, new Color(0.02f, 0.04f, 0.10f, 0.74f), raycast: _interactive);
            Stretch(dim.rectTransform, 0f);
            if (_interactive)
            {
                var tapResume = dim.gameObject.AddComponent<Button>();
                tapResume.transition = Selectable.Transition.None;
                tapResume.onClick.AddListener(() =>
                {
                    Audio?.PlayUiTap("cancel");
                    SetPaused(false);
                });
            }

            cardRect = Ui.Panel("SettingsCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 436f));
            Image bg = MakeSlicedImage("Plate", cardRect, ColCardDark, raycast: _interactive);
            Stretch(bg.rectTransform, 0f);

            Text header = Ui.MakeText("Title", cardRect, "MATCH PAUSE & SETTINGS", 24, ColGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            header.rectTransform.sizeDelta = new Vector2(-40f, 32f);

            // 2 columns x 4 rows of >= 48 dp quick-setting toggle pills
            musicTxt = BuildSettingPill(cardRect, 0, 0, () =>
            {
                SettingsStore.Current.MusicEnabled = !SettingsStore.Current.MusicEnabled;
                PersistSettings();
            });
            sfxTxt = BuildSettingPill(cardRect, 1, 0, () =>
            {
                SettingsStore.Current.SoundsEnabled = !SettingsStore.Current.SoundsEnabled;
                PersistSettings();
            });
            hapticsTxt = BuildSettingPill(cardRect, 0, 1, () =>
            {
                SettingsStore.Current.Haptics = !SettingsStore.Current.Haptics;
                Haptics.Enabled = SettingsStore.Current.Haptics;
                PersistSettings();
            });
            leftHandTxt = BuildSettingPill(cardRect, 1, 1, () =>
            {
                SettingsStore.Current.LeftHanded = !SettingsStore.Current.LeftHanded;
                PersistSettings();
            });
            aimGuideTxt = BuildSettingPill(cardRect, 0, 2, () =>
            {
                SettingsStore.Current.AimGuide = !SettingsStore.Current.AimGuide;
                PersistSettings();
            });
            reduceMotionTxt = BuildSettingPill(cardRect, 1, 2, () =>
            {
                SettingsStore.Current.ReduceMotion = !SettingsStore.Current.ReduceMotion;
                PersistSettings();
            });
            fpsTurboTxt = BuildSettingPill(cardRect, 0, 3, () =>
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
            });
            colorblindTxt = BuildSettingPill(cardRect, 1, 3, () =>
            {
                SettingsStore.Current.ColorblindMode = (SettingsStore.Current.ColorblindMode + 1) % 4;
                PersistSettings();
            });

            // Leave confirmation bar (shown when Leave Match is pressed outside Training)
            confirmRow = Ui.Panel("LeaveConfirmRow", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(-40f, 52f));
            Image warnBg = MakeSlicedImage("WarnBg", confirmRow, new Color(0.46f, 0.14f, 0.14f, 0.96f));
            Stretch(warnBg.rectTransform, 0f);
            Text warnTxt = Ui.MakeText("WarnText", confirmRow, "Leave battle? Counts as a loss.", 14, Ui.Cream, TextAnchor.MiddleLeft);
            warnTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            warnTxt.rectTransform.anchorMax = new Vector2(0.56f, 1f);
            warnTxt.rectTransform.offsetMin = new Vector2(12f, 0f);
            warnTxt.rectTransform.offsetMax = Vector2.zero;

            MakeActionButton(
                "CancelLeaveBtn",
                confirmRow,
                "Stay",
                new Color(0.28f, 0.34f, 0.44f, 1f),
                15,
                () =>
                {
                    Audio?.PlayUiTap("cancel");
                    _pauseConfirmRow.gameObject.SetActive(false);
                },
                out RectTransform stayRt,
                out _,
                out _);
            stayRt.anchorMin = stayRt.anchorMax = new Vector2(1f, 0.5f);
            stayRt.pivot = new Vector2(1f, 0.5f);
            stayRt.anchoredPosition = new Vector2(-118f, 0f);
            stayRt.sizeDelta = new Vector2(88f, 44f);

            MakeActionButton(
                "ConfirmLeaveBtn",
                confirmRow,
                "Leave",
                new Color(0.84f, 0.24f, 0.20f, 1f),
                15,
                () =>
                {
                    Haptics.CancelTick();
                    Audio?.PlayUiTap("back");
                    SetPaused(false);
                    OnBackToHome?.Invoke();
                },
                out RectTransform confirmLeaveRt,
                out _,
                out _);
            confirmLeaveRt.anchorMin = confirmLeaveRt.anchorMax = new Vector2(1f, 0.5f);
            confirmLeaveRt.pivot = new Vector2(1f, 0.5f);
            confirmLeaveRt.anchoredPosition = new Vector2(-8f, 0f);
            confirmLeaveRt.sizeDelta = new Vector2(102f, 44f);
            confirmRow.gameObject.SetActive(false);

            // Bottom row: Resume & Leave Match
            MakeActionButton(
                "ResumeMatchButton",
                cardRect,
                "Resume",
                new Color(0.18f, 0.68f, 0.42f, 1f),
                18,
                () =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap("confirm");
                    SetPaused(false);
                },
                out RectTransform resumeRt,
                out _,
                out _);
            resumeRt.anchorMin = resumeRt.anchorMax = new Vector2(0f, 0f);
            resumeRt.pivot = new Vector2(0f, 0f);
            resumeRt.anchoredPosition = new Vector2(20f, 14f);
            resumeRt.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(236f, 52f), ScreenFitMetrics.MinTouchDp, _canvas);

            MakeActionButton(
                "LeaveMatchButton",
                cardRect,
                "Leave battle",
                new Color(0.82f, 0.24f, 0.20f, 1f),
                18,
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
                    _pauseConfirmRow.gameObject.SetActive(true);
                },
                out RectTransform leaveRt,
                out _,
                out _);
            leaveRt.anchorMin = leaveRt.anchorMax = new Vector2(1f, 0f);
            leaveRt.pivot = new Vector2(1f, 0f);
            leaveRt.anchoredPosition = new Vector2(-20f, 14f);
            leaveRt.sizeDelta = ScreenFitMetrics.ClampTouchSize(new Vector2(236f, 52f), ScreenFitMetrics.MinTouchDp, _canvas);

            root.gameObject.SetActive(false);
        }

        Text BuildSettingPill(RectTransform parent, int col, int row, Action onClick)
        {
            float anchorMinX = col == 0 ? 0f : 0.5f;
            float anchorMaxX = col == 0 ? 0.5f : 1f;
            float y = -56f - row * 58f;

            RectTransform cell = Ui.Panel("Setting_" + col + "_" + row, parent, new Vector2(anchorMinX, 1f), new Vector2(anchorMaxX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, 50f));
            cell.offsetMin = new Vector2(col == 0 ? 20f : 6f, cell.offsetMin.y);
            cell.offsetMax = new Vector2(col == 0 ? -6f : -20f, cell.offsetMax.y);

            Image bg = MakeSlicedImage("PillBg", cell, new Color(0.12f, 0.17f, 0.28f, 0.96f), raycast: _interactive);
            Stretch(bg.rectTransform, 0f);

            if (_interactive)
            {
                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    Haptics.LightTap();
                    Audio?.PlayUiTap();
                    onClick?.Invoke();
                });
            }

            Text lbl = Ui.MakeText("Label", cell, "", 15, Ui.Cream, TextAnchor.MiddleCenter);
            Stretch(lbl.rectTransform, 6f);
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
        // Shared UI, Texture, Palette & Procedural Sprite Helpers (Self-Contained — zero dependency on sibling Wave 2 files)
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
            Image ring = Ui.MakeImage("Rim", rect, new Color(1f, 1f, 1f, 0.85f), round: true, raycast: _interactive);
            Stretch(ring.rectTransform, 2f);
            Image disc = Ui.MakeImage("Disc", rect, ColCardDark, round: true);
            Stretch(disc.rectTransform, 4.5f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = ring;
            btn.interactable = _interactive;
            if (_interactive && onClick != null)
                btn.onClick.AddListener(() => onClick());

            if (iconTex != null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(rect, false);
                var raw = go.GetComponent<RawImage>();
                raw.texture = iconTex;
                raw.raycastTarget = false;
                Stretch(raw.rectTransform, 13f);
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
            rect = Ui.Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 52f));
            bgImage = MakeSlicedImage("Bg", rect, color, raycast: _interactive);
            Stretch(bgImage.rectTransform, 0f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bgImage;
            btn.interactable = _interactive;
            if (_interactive && onClick != null)
                btn.onClick.AddListener(() => onClick());

            labelText = Ui.MakeText("Label", rect, label, fontSize, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(labelText.rectTransform, 6f);
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
                default: return EnemyTeamColor();
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
