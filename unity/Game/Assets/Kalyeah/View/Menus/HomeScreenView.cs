using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Vibrant Filipino street-komiks landscape Home hub (1280x720 reference, SafeArea-pinned, &gt;= 48 dp touch targets
    /// across 21:9 ultra-wide through 4:3 inner foldables): editable PlayerName, 7-Tier Tagalog Ranked badge, selected Kid
    /// portrait card + 3-superpower chips, 5v5 Agawang Base Practice launcher (Easy/Medium/Hard + Training pills),
    /// Larong Kalye street mode cards (Patintero, TumbangPreso, LangitLupa), Online Room (Create/Join),
    /// Local Hotspot (Host/Join), and top navigation buttons (Pick, Codex, History, Settings).
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color SkyWarm = new Color(0.99f, 0.72f, 0.34f, 1f);
        static readonly Color DuskPlum = new Color(0.24f, 0.14f, 0.32f, 1f);
        static readonly Color StreetSlate = new Color(0.11f, 0.14f, 0.24f, 0.92f);
        static readonly Color KomiksInk = new Color(0.09f, 0.07f, 0.14f, 0.96f);
        static readonly Color SunlitCream = new Color(0.98f, 0.95f, 0.89f, 0.98f);
        static readonly Color SunlitPaper = new Color(0.95f, 0.90f, 0.80f, 1f);
        static readonly Color Espresso = new Color(0.17f, 0.13f, 0.11f, 1f);
        static readonly Color MutedInk = new Color(0.35f, 0.29f, 0.25f, 1f);
        static readonly Color MangoGold = new Color(0.97f, 0.68f, 0.15f, 1f);
        static readonly Color JadeGreen = new Color(0.14f, 0.64f, 0.54f, 1f);
        static readonly Color BarrioBlue = new Color(0.19f, 0.48f, 0.88f, 1f);
        static readonly Color FiestaOrange = new Color(0.93f, 0.45f, 0.18f, 1f);
        static readonly Color PillIdleColor = new Color(0.20f, 0.25f, 0.38f, 0.95f);
        static readonly Color PillActiveColor = new Color(0.96f, 0.67f, 0.14f, 1f);
        static readonly Color TrainingPillColor = new Color(0.16f, 0.62f, 0.54f, 1f);

        struct KidMeta
        {
            public string Display;
            public string Epithet;
            public Color Accent;
        }

        static readonly Dictionary<string, KidMeta> KidMetadata = new Dictionary<string, KidMeta>(StringComparer.OrdinalIgnoreCase)
        {
            ["junjun"] = new KidMeta { Display = "Junjun", Epithet = "KAPITAN KUMOT", Accent = new Color(0.94f, 0.31f, 0.24f) },
            ["ligaya"] = new KidMeta { Display = "Ligaya", Epithet = "KUMANDER TEKS", Accent = new Color(1.00f, 0.55f, 0.26f) },
            ["migo"] = new KidMeta { Display = "Migo", Epithet = "PLASTIK-BALLOON", Accent = new Color(0.21f, 0.79f, 0.72f) },
            ["toni"] = new KidMeta { Display = "Toni", Epithet = "BARANGAY LIGA MVP", Accent = new Color(0.96f, 0.65f, 0.14f) },
            ["popoy"] = new KidMeta { Display = "Popoy", Epithet = "TANGKE NG TANGHALI", Accent = new Color(0.29f, 0.63f, 1.00f) },
            ["inday"] = new KidMeta { Display = "Inday", Epithet = "REYNA NG PATINTERO", Accent = new Color(1.00f, 0.82f, 0.36f) },
            ["kiko"] = new KidMeta { Display = "Kiko", Epithet = "TSINELAS SPRINTER", Accent = new Color(0.16f, 0.78f, 0.66f) },
            ["nene"] = new KidMeta { Display = "Nene", Epithet = "TIRADOR SCOUT", Accent = new Color(0.97f, 0.55f, 0.64f) },
            ["buknoy"] = new KidMeta { Display = "Buknoy", Epithet = "MEKANIKO NG KOLONG", Accent = new Color(0.88f, 0.48f, 0.22f) },
            ["tala"] = new KidMeta { Display = "Tala", Epithet = "KOMIKS PINTOR", Accent = new Color(0.70f, 0.49f, 0.96f) },
        };

        sealed class HomeLifecycle : MonoBehaviour
        {
            public HomeScreenView Owner;

            void OnEnable() => Owner?.OnEnabled();
            void OnDisable() => Owner?.OnDisabled();
            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();
            void OnDestroy() => Owner?.OnDisabled();
        }

        sealed class SuperpowerSlotUi
        {
            public Image Frame;
            public Image IconWell;
            public RawImage Icon;
            public AspectRatioFitter IconFitter;
            public Text NameLabel;
            public Text CostLabel;
        }

        readonly DataSource _data;
        readonly Action<string> _onNavigate;
        readonly Action<GameMode> _onLaunchMatch;
        readonly AudioHub _audio;
        readonly SuperpowerSlotUi[] _superpowerSlots = new SuperpowerSlotUi[3];

        Canvas _canvas;
        SafeArea _safeArea;
        bool _subscribed;
        bool _suppressNameCallback;
        bool _disposed;

        // Top header layout refs
        RectTransform _topBar;
        RectTransform _nameBoxRect;
        RectTransform _profileAvatarBtnRect;
        RawImage _profileAvatarImage;
        RectTransform _nameCaptionRect;
        RectTransform _namePlaceholderRect;
        RectTransform _nameInputTextRect;
        RectTransform _rankBadgeRect;
        RectTransform _brandCenterRect;
        readonly RectTransform[] _navBtnRects = new RectTransform[6];

        // 3-column body layout refs
        RectTransform _bodyContainer;
        RectTransform _leftCol;
        RectTransform _centerCol;
        RectTransform _rightCol;

        RectTransform _portraitCardRect;
        RectTransform _loadoutCardRect;

        RectTransform _practiceCardRect;
        RectTransform _practiceBannerRect;
        readonly RectTransform[] _pillRects = new RectTransform[4];
        RectTransform _playPracticeRect;

        RectTransform _streetCardRect;
        readonly RectTransform[] _streetModeRects = new RectTransform[3];

        RectTransform _onlineCardRect;
        RectTransform _onlineHeaderRect;
        RectTransform _onlineCreateRect;
        RectTransform _onlineJoinRect;

        RectTransform _hotspotCardRect;
        RectTransform _hotspotHeaderRect;
        RectTransform _hotspotHostRect;
        RectTransform _hotspotJoinRect;

        // Dynamic UI content refs
        Image _rankFillImage;
        Image _rankCrestBg;
        RectTransform _rankProgressFill;
        Text _rankTierText;
        Text _rankLpText;

        Image _portraitAuraBg;
        RawImage _kidPortraitImage;
        Text _kidNameText;
        Text _kidEpithetText;
        Text _cosmeticNoteText;
        Text _swapKitLabel;

        readonly Image[] _difficultyPillImages = new Image[3];
        readonly Text[] _difficultyPillLabels = new Text[3];
        Image _trainingPillImage;
        Text _trainingPillLabel;
        Text _practiceTitleLabel;
        Text _playPracticeLabel;
        Text _streetSectionTitle;
        Text _onlineTitleLabel;
        Text _hotspotTitleLabel;
        readonly Text[] _navLabels = new Text[6];

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public HomeSheetsView Sheets { get; private set; }

        public InputField PlayerNameInput { get; private set; }
        public Button ProfileSheetButton { get; private set; }
        public Button GameOfMonthButton { get; private set; }
        public Button DonateButton { get; private set; }
        public Button RankedBadgeButton { get; private set; }
        public Button PortraitCardButton { get; private set; }
        public Button LoadoutButton { get; private set; }

        public Button EasyPillButton { get; private set; }
        public Button MediumPillButton { get; private set; }
        public Button HardPillButton { get; private set; }
        public Button TrainingPillButton { get; private set; }
        public Button PlayPracticeButton { get; private set; }

        public Button PatinteroButton { get; private set; }
        public Button TumbangPresoButton { get; private set; }
        public Button LangitLupaButton { get; private set; }

        public Button OnlineCreateButton { get; private set; }
        public Button OnlineJoinButton { get; private set; }
        public Button HotspotHostButton { get; private set; }
        public Button HotspotJoinButton { get; private set; }

        public Button PickNavButton { get; private set; }
        public Button CodexNavButton { get; private set; }
        public Button HistoryNavButton { get; private set; }
        public Button SettingsNavButton { get; private set; }

        public HomeScreenView(
            Transform parent,
            Action<string> onNavigate,
            Action<GameMode> onLaunchMatch,
            AudioHub audio = null)
            : this(parent, TryDefaultData(), onNavigate, onLaunchMatch, audio)
        {
        }

        public HomeScreenView(
            Transform parent,
            DataSource data,
            Action<string> onNavigate,
            Action<GameMode> onLaunchMatch,
            AudioHub audio = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            _data = data;
            _onNavigate = onNavigate;
            _onLaunchMatch = onLaunchMatch;
            _audio = audio;
            _canvas = parent.GetComponentInParent<Canvas>();

            ScreenFitMetrics.EnsureEnhancedTouch();

            Root = Ui.Fill("HomeScreen", parent);
            BuildBackdrop(Root);

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null)
            {
                _safeArea = parentSafe;
                SafeRoot = Ui.Fill("HomeContent", Root);
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
                _safeArea.Apply();
            }

            BuildTopBar(SafeRoot);
            BuildBodyColumns(SafeRoot);
            Sheets = new HomeSheetsView(SafeRoot, _data, _audio, IsInteractive);
            Sheets.ProfileChanged += Refresh;

            var lifecycle = Root.gameObject.AddComponent<HomeLifecycle>();
            lifecycle.Owner = this;

            if (Root.gameObject.activeInHierarchy)
                OnEnabled();
            else
            {
                Refresh();
                RefreshLayout();
            }
        }

        bool IsInteractive()
        {
            if (_canvas == null && Root != null)
                _canvas = Root.GetComponentInParent<Canvas>();
            if (_canvas == null) return true;
            GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();
            return raycaster == null || raycaster.enabled;
        }

        public bool HandleBack() => Sheets != null && Sheets.Close();

        static DataSource TryDefaultData()
        {
            try { return KalyeahResources.Data(); }
            catch { return null; }
        }

        void OnEnabled()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                AppSession.Current.Changed += Refresh;
                SettingsStore.Current.Changed += Refresh;
                ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;
                if (_safeArea != null) _safeArea.Changed += OnSafeAreaChanged;
            }
            Refresh();
            RefreshLayout();
        }

        void OnDisabled()
        {
            Sheets?.Close();
            if (!_subscribed) return;
            _subscribed = false;
            AppSession.Current.Changed -= Refresh;
            SettingsStore.Current.Changed -= Refresh;
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnDisabled();
            if (Sheets != null)
            {
                Sheets.ProfileChanged -= Refresh;
                Sheets.Dispose();
                Sheets = null;
            }
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        void BuildBackdrop(RectTransform parent)
        {
            RectTransform bg = Ui.Fill("Backdrop", parent);
            Texture2D mainBg = KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (mainBg != null)
            {
                var rawGo = new GameObject("ArtBackdrop", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(bg, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = mainBg;
                raw.color = new Color(0.95f, 0.88f, 0.82f, 1f);
                raw.raycastTarget = false;
                RectTransform rr = raw.rectTransform;
                rr.anchorMin = Vector2.zero;
                rr.anchorMax = Vector2.one;
                rr.sizeDelta = Vector2.zero;

                Image wash = Ui.MakeImage("ReadabilityWash", bg, new Color(0.08f, 0.06f, 0.14f, 0.34f));
                wash.rectTransform.anchorMin = Vector2.zero;
                wash.rectTransform.anchorMax = Vector2.one;
                wash.rectTransform.sizeDelta = Vector2.zero;
            }
            else
            {
                Image sky = Ui.MakeImage("GoldenSky", bg, SkyWarm);
                sky.rectTransform.anchorMin = new Vector2(0f, 0.36f);
                sky.rectTransform.anchorMax = Vector2.one;
                sky.rectTransform.sizeDelta = Vector2.zero;

                Image dusk = Ui.MakeImage("BarrioDusk", bg, DuskPlum);
                dusk.rectTransform.anchorMin = Vector2.zero;
                dusk.rectTransform.anchorMax = new Vector2(1f, 0.36f);
                dusk.rectTransform.sizeDelta = Vector2.zero;

                Image sunBand = Ui.MakeImage("SunGlow", bg, new Color(1f, 0.90f, 0.52f, 0.26f));
                sunBand.rectTransform.anchorMin = new Vector2(0f, 0.34f);
                sunBand.rectTransform.anchorMax = new Vector2(1f, 0.56f);
                sunBand.rectTransform.sizeDelta = Vector2.zero;
            }
        }

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel(
                "TopBar",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(0f, 96f));

            // 1. Editable PlayerName card + ID Avatar button (top-left)
            _nameBoxRect = MakeKomiksFrame(
                "PlayerNameCard",
                _topBar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(12f, 0f),
                new Vector2(236f, 96f),
                SunlitCream,
                out Image nameBodyImg);
            nameBodyImg.raycastTarget = true;

            _profileAvatarBtnRect = MakeKomiksFrame(
                "ProfileAvatarButton",
                _nameBoxRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(8f, 0f),
                new Vector2(68f, 68f),
                StreetSlate,
                out Image avatarFrameFill);
            avatarFrameFill.raycastTarget = true;
            ProfileSheetButton = AttachButton(_profileAvatarBtnRect.gameObject, avatarFrameFill, () => Sheets?.OpenProfile());

            var avGo = new GameObject("AvatarPhoto", typeof(RectTransform), typeof(RawImage));
            avGo.transform.SetParent(_profileAvatarBtnRect, false);
            _profileAvatarImage = avGo.GetComponent<RawImage>();
            _profileAvatarImage.raycastTarget = false;
            RectTransform avr = _profileAvatarImage.rectTransform;
            avr.anchorMin = Vector2.zero;
            avr.anchorMax = Vector2.one;
            avr.offsetMin = new Vector2(3f, 3f);
            avr.offsetMax = new Vector2(-3f, -3f);

            Text nameCaption = Ui.MakeText("Caption", _nameBoxRect, "PLAYER TAG", 13, new Color(Espresso.r, Espresso.g, Espresso.b, 0.84f), TextAnchor.UpperLeft);
            nameCaption.fontStyle = FontStyle.Bold;
            nameCaption.GetComponent<Outline>().enabled = false;
            _nameCaptionRect = nameCaption.rectTransform;
            _nameCaptionRect.anchorMin = new Vector2(0f, 0.62f);
            _nameCaptionRect.anchorMax = new Vector2(1f, 1f);
            _nameCaptionRect.offsetMin = new Vector2(84f, 0f);
            _nameCaptionRect.offsetMax = new Vector2(-10f, -8f);

            PlayerNameInput = _nameBoxRect.gameObject.AddComponent<InputField>();
            PlayerNameInput.targetGraphic = nameBodyImg;
            PlayerNameInput.characterLimit = AppSession.MaxNameLength;
            PlayerNameInput.lineType = InputField.LineType.SingleLine;

            Text placeholder = Ui.MakeText("Placeholder", _nameBoxRect, "Enter name...", 20, new Color(0.45f, 0.40f, 0.35f, 0.65f), TextAnchor.LowerLeft);
            placeholder.GetComponent<Outline>().enabled = false;
            placeholder.fontStyle = FontStyle.Italic;
            _namePlaceholderRect = placeholder.rectTransform;
            _namePlaceholderRect.anchorMin = new Vector2(0f, 0f);
            _namePlaceholderRect.anchorMax = new Vector2(1f, 0.66f);
            _namePlaceholderRect.offsetMin = new Vector2(84f, 10f);
            _namePlaceholderRect.offsetMax = new Vector2(-10f, 0f);

            Text inputText = Ui.MakeText("Text", _nameBoxRect, AppSession.Current.PlayerName, 24, Espresso, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            inputText.GetComponent<Outline>().enabled = false;
            inputText.supportRichText = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Wrap;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            inputText.resizeTextForBestFit = true;
            inputText.resizeTextMinSize = 16;
            inputText.resizeTextMaxSize = 24;
            _nameInputTextRect = inputText.rectTransform;
            _nameInputTextRect.anchorMin = new Vector2(0f, 0f);
            _nameInputTextRect.anchorMax = new Vector2(1f, 0.66f);
            _nameInputTextRect.offsetMin = new Vector2(84f, 10f);
            _nameInputTextRect.offsetMax = new Vector2(-10f, 0f);

            PlayerNameInput.textComponent = inputText;
            PlayerNameInput.placeholder = placeholder;
            PlayerNameInput.text = AppSession.Current.PlayerName;
            PlayerNameInput.onEndEdit.AddListener(OnPlayerNameEndEdit);

            // 2. Ranked Tier Badge (beside PlayerName)
            _rankBadgeRect = MakeKomiksFrame(
                "RankedTierBadge",
                _topBar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(240f, 0f),
                new Vector2(190f, 96f),
                StreetSlate,
                out _rankFillImage);
            _rankFillImage.raycastTarget = true;
            RankedBadgeButton = AttachButton(_rankBadgeRect.gameObject, _rankFillImage, () => Navigate(MenuStack.HistoryId));

            _rankCrestBg = Ui.MakeImage("TierEmblem", _rankBadgeRect, MangoGold, round: true);
            RectTransform emblemRect = _rankCrestBg.rectTransform;
            emblemRect.anchorMin = emblemRect.anchorMax = new Vector2(0f, 0.56f);
            emblemRect.pivot = new Vector2(0f, 0.5f);
            emblemRect.anchoredPosition = new Vector2(12f, 0f);
            emblemRect.sizeDelta = new Vector2(34f, 34f);

            _rankTierText = Ui.MakeText("TierName", _rankBadgeRect, "Kahoy", 22, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            RectTransform tierRect = _rankTierText.rectTransform;
            tierRect.anchorMin = new Vector2(0f, 0.48f);
            tierRect.anchorMax = new Vector2(1f, 0.94f);
            tierRect.offsetMin = new Vector2(52f, 0f);
            tierRect.offsetMax = new Vector2(-10f, -4f);

            _rankLpText = Ui.MakeText("TierLp", _rankBadgeRect, "0 / 200 LP", 14, SunlitPaper, TextAnchor.MiddleLeft);
            _rankLpText.GetComponent<Outline>().enabled = false;
            RectTransform lpRect = _rankLpText.rectTransform;
            lpRect.anchorMin = new Vector2(0f, 0.22f);
            lpRect.anchorMax = new Vector2(1f, 0.52f);
            lpRect.offsetMin = new Vector2(52f, 0f);
            lpRect.offsetMax = new Vector2(-10f, 0f);

            RectTransform barTrack = Ui.Panel("LpTrack", _rankBadgeRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-24f, 8f));
            Ui.MakeImage("TrackBg", barTrack, new Color(0f, 0f, 0f, 0.45f)).rectTransform.SetFullStretch();
            Image fillImg = Ui.MakeImage("TrackFill", barTrack, MangoGold);
            _rankProgressFill = fillImg.rectTransform;
            _rankProgressFill.anchorMin = Vector2.zero;
            _rankProgressFill.anchorMax = new Vector2(0.15f, 1f);
            _rankProgressFill.sizeDelta = Vector2.zero;

            // 3. Brand lockup (center decorative)
            _brandCenterRect = Ui.Panel(
                "BrandLockup",
                _topBar,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(220f, 84f));
            Texture2D wordmark = KalyeahResources.Texture("ui/brand/kalyeah_wordmark")
                ?? KalyeahResources.Texture("ui/kalyeah_logo")
                ?? KalyeahResources.Texture("ui/brand/kalyeah_crest");
            if (wordmark != null)
            {
                var logoGo = new GameObject("BrandArt", typeof(RectTransform), typeof(RawImage));
                logoGo.transform.SetParent(_brandCenterRect, false);
                var raw = logoGo.GetComponent<RawImage>();
                raw.texture = wordmark;
                raw.raycastTarget = false;
                RectTransform lr = raw.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(4f, 4f);
                lr.offsetMax = new Vector2(-4f, -4f);
            }
            else
            {
                Text brandText = Ui.MakeText("BrandTitle", _brandCenterRect, "KALYEAH!", 38, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                brandText.rectTransform.SetFullStretch();
            }

            // 4. Top-right Sheet + Nav buttons: GameOfMonth, Donate, Pick, Codex, History, Settings
            GameOfMonthButton = MakeNavButton("Nav_GameOfMonth", "Monthly", "ui/tips_icon", MangoGold, 0, () => Sheets?.OpenGameOfMonth(), out _navBtnRects[0], out _navLabels[0]);
            DonateButton = MakeNavButton("Nav_Donate", "Donate", "ui/mail_icon", FiestaOrange, 1, () => Sheets?.OpenDonate(), out _navBtnRects[1], out _navLabels[1]);
            PickNavButton = MakeNavButton("Nav_Pick", "Pick", "ui/nav/nav_cards", BarrioBlue, 2, () => Navigate(MenuStack.PickId), out _navBtnRects[2], out _navLabels[2]);
            CodexNavButton = MakeNavButton("Nav_Codex", "Codex", "ui/nav/nav_street", FiestaOrange, 3, () => Navigate(MenuStack.CodexId), out _navBtnRects[3], out _navLabels[3]);
            HistoryNavButton = MakeNavButton("Nav_History", "History", "ui/nav/nav_history", MangoGold, 4, () => Navigate(MenuStack.HistoryId), out _navBtnRects[4], out _navLabels[4]);
            SettingsNavButton = MakeNavButton("Nav_Settings", "Settings", "ui/nav/nav_settings", JadeGreen, 5, () => Navigate(MenuStack.SettingsId), out _navBtnRects[5], out _navLabels[5]);
        }

        Button MakeNavButton(
            string goName,
            string title,
            string iconPath,
            Color accent,
            int index,
            Action onClick,
            out RectTransform rect,
            out Text label)
        {
            rect = MakeKomiksFrame(
                goName,
                _topBar,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(88f, 96f),
                StreetSlate,
                out Image bodyImg);
            bodyImg.raycastTarget = true;

            Image topStripe = Ui.MakeImage("Accent", rect, accent);
            RectTransform sr = topStripe.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = new Vector2(0f, -4f);
            sr.sizeDelta = new Vector2(-10f, 6f);

            Texture2D iconTex = KalyeahResources.Texture(iconPath) ?? (index == 5 ? KalyeahResources.Texture("ui/hud_icons/gear") : null);
            if (iconTex != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                iconGo.transform.SetParent(rect, false);
                var raw = iconGo.GetComponent<RawImage>();
                raw.texture = iconTex;
                raw.raycastTarget = false;
                RectTransform ir = raw.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.62f);
                ir.sizeDelta = new Vector2(34f, 34f);
            }

            label = Ui.MakeText("Label", rect, title, 16, Ui.Cream, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 16;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, iconTex != null ? 0.42f : 1f);
            lr.offsetMin = new Vector2(4f, 8f);
            lr.offsetMax = new Vector2(-4f, iconTex != null ? 0f : -8f);
            if (iconTex == null) label.alignment = TextAnchor.MiddleCenter;

            return AttachButton(rect.gameObject, bodyImg, onClick);
        }

        void BuildBodyColumns(RectTransform parent)
        {
            _bodyContainer = Ui.Panel(
                "HubBody",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 8f),
                new Vector2(1240f, 566f));

            _leftCol = Ui.Panel("KidColumn", _bodyContainer, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(320f, 0f));
            _centerCol = Ui.Panel("ModesColumn", _bodyContainer, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 0f));
            _rightCol = Ui.Panel("RoomsColumn", _bodyContainer, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(340f, 0f));

            BuildKidShowcaseColumn(_leftCol);
            BuildPracticeAndStreetColumn(_centerCol);
            BuildMultiplayerColumn(_rightCol);
        }

        void BuildKidShowcaseColumn(RectTransform col)
        {
            // Upper card: Selected Kid Portrait
            _portraitCardRect = MakeKomiksFrame(
                "PortraitCard",
                col,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 384f),
                SunlitCream,
                out Image cardBg);
            cardBg.raycastTarget = true;
            PortraitCardButton = AttachButton(_portraitCardRect.gameObject, cardBg, () => Navigate(MenuStack.PickId));

            _portraitAuraBg = Ui.MakeImage("AuraBackdrop", _portraitCardRect, BarrioBlue);
            _portraitAuraBg.sprite = MenuStack.RoundedSprite();
            _portraitAuraBg.type = Image.Type.Sliced;
            RectTransform auraRect = _portraitAuraBg.rectTransform;
            auraRect.anchorMin = new Vector2(0f, 0.24f);
            auraRect.anchorMax = Vector2.one;
            auraRect.offsetMin = new Vector2(8f, 0f);
            auraRect.offsetMax = new Vector2(-8f, -8f);

            RectTransform portraitStage = Ui.Fill("PortraitStage", auraRect);
            portraitStage.offsetMin = new Vector2(6f, 4f);
            portraitStage.offsetMax = new Vector2(-6f, -4f);

            var portraitGo = new GameObject("KidPortrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            portraitGo.transform.SetParent(portraitStage, false);
            _kidPortraitImage = portraitGo.GetComponent<RawImage>();
            _kidPortraitImage.raycastTarget = false;
            var fitter = portraitGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            RectTransform cosPill = Ui.Panel("CosmeticPill", auraRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(128f, 24f));
            Image cosBg = Ui.MakeImage("Bg", cosPill, new Color(0.06f, 0.08f, 0.18f, 0.90f));
            cosBg.sprite = MenuStack.RoundedSprite();
            cosBg.type = Image.Type.Sliced;
            cosBg.rectTransform.SetFullStretch();

            _cosmeticNoteText = Ui.MakeText("CosmeticText", cosPill, "COSMETIC ONLY", 12, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            RectTransform cosRect = _cosmeticNoteText.rectTransform;
            cosRect.anchorMin = Vector2.zero;
            cosRect.anchorMax = Vector2.one;
            cosRect.offsetMin = new Vector2(6f, 1f);
            cosRect.offsetMax = new Vector2(-6f, -1f);

            _kidNameText = Ui.MakeText("KidName", _portraitCardRect, "JUNJUN", 28, Espresso, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            _kidNameText.GetComponent<Outline>().enabled = false;
            RectTransform knRect = _kidNameText.rectTransform;
            knRect.anchorMin = new Vector2(0f, 0.09f);
            knRect.anchorMax = new Vector2(1f, 0.24f);
            knRect.offsetMin = new Vector2(14f, 0f);
            knRect.offsetMax = new Vector2(-12f, 0f);

            _kidEpithetText = Ui.MakeText("KidEpithet", _portraitCardRect, "KAPITAN KUMOT", 14, FiestaOrange, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
            _kidEpithetText.GetComponent<Outline>().enabled = false;
            RectTransform epRect = _kidEpithetText.rectTransform;
            epRect.anchorMin = new Vector2(0f, 0.01f);
            epRect.anchorMax = new Vector2(1f, 0.10f);
            epRect.offsetMin = new Vector2(14f, 4f);
            epRect.offsetMax = new Vector2(-12f, 0f);

            // Lower card: 3-Superpower Loadout chips (tapping opens Pick/Loadout builder)
            _loadoutCardRect = MakeKomiksFrame(
                "LoadoutCard",
                col,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 168f),
                StreetSlate,
                out Image loadoutBg);
            loadoutBg.raycastTarget = true;
            LoadoutButton = AttachButton(_loadoutCardRect.gameObject, loadoutBg, () => Navigate(MenuStack.PickId));

            _swapKitLabel = Ui.MakeText("LoadoutHeader", _loadoutCardRect, "SUPERPOWERS · TAP TO SWAP", 14, MangoGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            RectTransform lhRect = _swapKitLabel.rectTransform;
            lhRect.anchorMin = new Vector2(0f, 0.76f);
            lhRect.anchorMax = new Vector2(1f, 1f);
            lhRect.offsetMin = new Vector2(12f, 0f);
            lhRect.offsetMax = new Vector2(-12f, -4f);

            for (int i = 0; i < 3; i++)
            {
                float xMin = 0.03f + i * 0.32f;
                float xMax = xMin + 0.30f;
                RectTransform chip = Ui.Panel("Chip_" + i, _loadoutCardRect, new Vector2(xMin, 0.07f), new Vector2(xMax, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image frame = Ui.MakeImage("Bg", chip, SunlitCream);
                frame.sprite = MenuStack.RoundedSprite();
                frame.type = Image.Type.Sliced;
                frame.rectTransform.SetFullStretch();

                Image iconWell = Ui.MakeImage("IconWell", chip, new Color(0.08f, 0.11f, 0.22f, 0.96f));
                iconWell.sprite = MenuStack.RoundedSprite();
                iconWell.type = Image.Type.Sliced;
                RectTransform wr = iconWell.rectTransform;
                wr.anchorMin = new Vector2(0.08f, 0.31f);
                wr.anchorMax = new Vector2(0.92f, 0.94f);
                wr.offsetMin = Vector2.zero;
                wr.offsetMax = Vector2.zero;

                RectTransform iconPad = Ui.Fill("IconPad", wr);
                iconPad.offsetMin = new Vector2(4f, 4f);
                iconPad.offsetMax = new Vector2(-4f, -4f);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                iconGo.transform.SetParent(iconPad, false);
                var raw = iconGo.GetComponent<RawImage>();
                raw.raycastTarget = false;
                var iconFitter = iconGo.GetComponent<AspectRatioFitter>();
                iconFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                iconFitter.aspectRatio = 1f;

                Text nameLbl = Ui.MakeText("WeaponName", chip, "Bato", 14, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                nameLbl.GetComponent<Outline>().enabled = false;
                nameLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameLbl.verticalOverflow = VerticalWrapMode.Truncate;
                nameLbl.resizeTextForBestFit = true;
                nameLbl.resizeTextMinSize = 11;
                nameLbl.resizeTextMaxSize = 15;
                RectTransform nr = nameLbl.rectTransform;
                nr.anchorMin = new Vector2(0f, 0f);
                nr.anchorMax = new Vector2(1f, 0.31f);
                nr.offsetMin = new Vector2(3f, 2f);
                nr.offsetMax = new Vector2(-3f, 0f);

                Image drop = Ui.MakeImage("CostPip", chip, BarrioBlue, round: true);
                RectTransform dr = drop.rectTransform;
                dr.anchorMin = dr.anchorMax = new Vector2(1f, 1f);
                dr.pivot = new Vector2(1f, 1f);
                dr.anchoredPosition = new Vector2(-3f, -3f);
                dr.sizeDelta = new Vector2(22f, 22f);

                Text costLbl = Ui.MakeText("Cost", drop.transform, "2", 12, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                costLbl.rectTransform.SetFullStretch();

                _superpowerSlots[i] = new SuperpowerSlotUi
                {
                    Frame = frame,
                    IconWell = iconWell,
                    Icon = raw,
                    IconFitter = iconFitter,
                    NameLabel = nameLbl,
                    CostLabel = costLbl,
                };
            }
        }

        void BuildPracticeAndStreetColumn(RectTransform col)
        {
            // Top card: 5v5 Agawang Base Practice Launcher
            _practiceCardRect = MakeKomiksFrame(
                "PracticeLauncherCard",
                col,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 318f),
                SunlitCream,
                out _);

            _practiceBannerRect = Ui.Panel("Banner", _practiceCardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-12f, 88f));
            Image bannerTint = Ui.MakeImage("BannerTint", _practiceBannerRect, BarrioBlue);
            bannerTint.sprite = MenuStack.RoundedSprite();
            bannerTint.type = Image.Type.Sliced;
            bannerTint.rectTransform.SetFullStretch();

            Texture2D pracTex = KalyeahResources.Texture("ui/dioramas/practice")
                ?? KalyeahResources.Texture("ui/mode_pictures/practice")
                ?? KalyeahResources.Texture("ui/games/agawan_base");
            if (pracTex != null)
            {
                var rawGo = new GameObject("Diorama", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(_practiceBannerRect, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = pracTex;
                raw.color = new Color(1f, 1f, 1f, 0.34f);
                raw.raycastTarget = false;
                raw.rectTransform.SetFullStretch();
            }

            _practiceTitleLabel = Ui.MakeText("PracticeTitle", _practiceBannerRect, "5v5 AGAWANG BASE", 28, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _practiceTitleLabel.resizeTextForBestFit = true;
            _practiceTitleLabel.resizeTextMinSize = 20;
            _practiceTitleLabel.resizeTextMaxSize = 28;
            RectTransform ptRect = _practiceTitleLabel.rectTransform;
            ptRect.anchorMin = new Vector2(0f, 0.44f);
            ptRect.anchorMax = new Vector2(1f, 1f);
            ptRect.offsetMin = new Vector2(14f, 0f);
            ptRect.offsetMax = new Vector2(-14f, -3f);

            Image pracSubPlate = Ui.MakeImage("PracticeSubPlate", _practiceBannerRect, new Color(0.06f, 0.09f, 0.20f, 0.88f));
            pracSubPlate.sprite = MenuStack.RoundedSprite();
            pracSubPlate.type = Image.Type.Sliced;
            RectTransform pspRect = pracSubPlate.rectTransform;
            pspRect.anchorMin = new Vector2(0f, 0f);
            pspRect.anchorMax = new Vector2(1f, 0.45f);
            pspRect.offsetMin = new Vector2(6f, 5f);
            pspRect.offsetMax = new Vector2(-6f, -1f);

            Text pracSub = Ui.MakeText("PracticeSub", pracSubPlate.transform, "Tag rivals, rescue squadmates & claim the enemy post!", 18, Ui.Cream, TextAnchor.MiddleLeft);
            pracSub.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            pracSub.horizontalOverflow = HorizontalWrapMode.Wrap;
            pracSub.verticalOverflow = VerticalWrapMode.Truncate;
            pracSub.resizeTextForBestFit = true;
            pracSub.resizeTextMinSize = 14;
            pracSub.resizeTextMaxSize = 18;
            RectTransform psRect = pracSub.rectTransform;
            psRect.anchorMin = Vector2.zero;
            psRect.anchorMax = Vector2.one;
            psRect.offsetMin = new Vector2(10f, 1f);
            psRect.offsetMax = new Vector2(-10f, -1f);

            // Difficulty & Training pills row
            EasyPillButton = MakePillButton("Pill_Easy", _practiceCardRect, "Easy", PillIdleColor, () => SetDifficulty("easy"), out _pillRects[0], out _difficultyPillImages[0], out _difficultyPillLabels[0]);
            MediumPillButton = MakePillButton("Pill_Medium", _practiceCardRect, "Medium", PillActiveColor, () => SetDifficulty("medium"), out _pillRects[1], out _difficultyPillImages[1], out _difficultyPillLabels[1]);
            HardPillButton = MakePillButton("Pill_Hard", _practiceCardRect, "Hard", PillIdleColor, () => SetDifficulty("hard"), out _pillRects[2], out _difficultyPillImages[2], out _difficultyPillLabels[2]);
            TrainingPillButton = MakePillButton("Pill_Training", _practiceCardRect, "Training", TrainingPillColor, LaunchTraining, out _pillRects[3], out _trainingPillImage, out _trainingPillLabel);

            // Big Play 5v5 Practice CTA
            _playPracticeRect = Ui.Panel(
                "PlayPracticeButton",
                _practiceCardRect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 10f),
                new Vector2(480f, 98f));
            Image playImg = _playPracticeRect.gameObject.AddComponent<Image>();
            Sprite goldBtn = TryCachedSprite("ui/button_gold");
            playImg.sprite = goldBtn != null ? goldBtn : MenuStack.RoundedSprite();
            playImg.type = goldBtn != null ? Image.Type.Simple : Image.Type.Sliced;
            playImg.color = goldBtn != null ? Color.white : MangoGold;
            playImg.raycastTarget = true;
            PlayPracticeButton = AttachButton(_playPracticeRect.gameObject, playImg, LaunchPractice);

            _playPracticeLabel = Ui.MakeText("Label", _playPracticeRect, "LARO! · PLAY 5v5 PRACTICE", 28, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _playPracticeLabel.GetComponent<Outline>().enabled = false;
            _playPracticeLabel.rectTransform.SetFullStretch();

            // Bottom card: Larong Kalye Street Modes (Patintero, TumbangPreso, LangitLupa)
            _streetCardRect = MakeKomiksFrame(
                "LarongKalyeCard",
                col,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 236f),
                StreetSlate,
                out _);

            _streetSectionTitle = Ui.MakeText("StreetHeader", _streetCardRect, "LARONG KALYE · STREET MINI-GAMES", 18, MangoGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _streetSectionTitle.resizeTextForBestFit = true;
            _streetSectionTitle.resizeTextMinSize = 14;
            _streetSectionTitle.resizeTextMaxSize = 18;
            RectTransform shRect = _streetSectionTitle.rectTransform;
            shRect.anchorMin = new Vector2(0f, 1f);
            shRect.anchorMax = new Vector2(1f, 1f);
            shRect.pivot = new Vector2(0.5f, 1f);
            shRect.anchoredPosition = new Vector2(0f, -6f);
            shRect.sizeDelta = new Vector2(-24f, 28f);

            PatinteroButton = MakeStreetModeCard(
                "Street_Patintero",
                _streetCardRect,
                "Patintero",
                "Grid Runners",
                "ui/games/patintero",
                BarrioBlue,
                () => LaunchMode(GameMode.Patintero),
                out _streetModeRects[0]);

            TumbangPresoButton = MakeStreetModeCard(
                "Street_TumbangPreso",
                _streetCardRect,
                "Tumbang Preso",
                "Knock the Can",
                "ui/games/tumbang_preso",
                FiestaOrange,
                () => LaunchMode(GameMode.TumbangPreso),
                out _streetModeRects[1]);

            LangitLupaButton = MakeStreetModeCard(
                "Street_LangitLupa",
                _streetCardRect,
                "Langit-Lupa",
                "High Ground",
                "icons/art/langit_lupa",
                JadeGreen,
                () => LaunchMode(GameMode.LangitLupa),
                out _streetModeRects[2]);
        }

        static Button MakePillButton(
            string name,
            Transform parent,
            string title,
            Color color,
            Action onClick,
            out RectTransform rect,
            out Image bg,
            out Text label)
        {
            rect = Ui.Panel(name, parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(104f, 96f));
            bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = MenuStack.RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = color;
            bg.raycastTarget = true;

            label = Ui.MakeText("Label", rect, title, 19, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(4f, 4f);
            lr.offsetMax = new Vector2(-4f, -4f);

            return AttachButton(rect.gameObject, bg, onClick);
        }

        static Button MakeStreetModeCard(
            string goName,
            Transform parent,
            string title,
            string subtitle,
            string artPath,
            Color accent,
            Action onClick,
            out RectTransform rect)
        {
            rect = Ui.Panel(goName, parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 184f));
            Image bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = MenuStack.RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = SunlitCream;
            bg.raycastTarget = true;

            Image topArtBg = Ui.MakeImage("ArtPlate", rect, accent);
            topArtBg.sprite = MenuStack.RoundedSprite();
            topArtBg.type = Image.Type.Sliced;
            RectTransform ar = topArtBg.rectTransform;
            ar.anchorMin = new Vector2(0f, 0.42f);
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(5f, 2f);
            ar.offsetMax = new Vector2(-5f, -5f);

            Texture2D tex = KalyeahResources.Texture(artPath) ?? KalyeahResources.Texture("ui/hud_icons/langit_lupa");
            if (tex != null)
            {
                RectTransform artPad = Ui.Fill("ArtPad", ar);
                artPad.offsetMin = new Vector2(3f, 3f);
                artPad.offsetMax = new Vector2(-3f, -3f);

                var rawGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                rawGo.transform.SetParent(artPad, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                var fit = rawGo.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;
            }

            Text t = Ui.MakeText("Title", rect, title, 18, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            t.GetComponent<Outline>().enabled = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 14;
            t.resizeTextMaxSize = 18;
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.23f);
            tr.anchorMax = new Vector2(1f, 0.42f);
            tr.offsetMin = new Vector2(4f, 0f);
            tr.offsetMax = new Vector2(-4f, 0f);

            Image subPlate = Ui.MakeImage("SubPlate", rect, new Color(0.08f, 0.11f, 0.22f, 0.96f));
            subPlate.sprite = MenuStack.RoundedSprite();
            subPlate.type = Image.Type.Sliced;
            RectTransform spr = subPlate.rectTransform;
            spr.anchorMin = new Vector2(0f, 0f);
            spr.anchorMax = new Vector2(1f, 0.23f);
            spr.offsetMin = new Vector2(5f, 5f);
            spr.offsetMax = new Vector2(-5f, -1f);

            Text sub = Ui.MakeText("Subtitle", subPlate.transform, subtitle, 18, Ui.Cream, TextAnchor.MiddleCenter);
            sub.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.verticalOverflow = VerticalWrapMode.Truncate;
            sub.resizeTextForBestFit = true;
            sub.resizeTextMinSize = 14;
            sub.resizeTextMaxSize = 18;
            RectTransform sr = sub.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(4f, 1f);
            sr.offsetMax = new Vector2(-4f, -1f);

            return AttachButton(rect.gameObject, bg, onClick);
        }

        void BuildMultiplayerColumn(RectTransform col)
        {
            // Top card: Online Room (Create / Join)
            _onlineCardRect = MakeKomiksFrame(
                "OnlineRoomCard",
                col,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 276f),
                SunlitCream,
                out _);

            _onlineHeaderRect = BuildRoomCardHeader(
                _onlineCardRect,
                "ONLINE ROOM",
                "Invite friends with an 8-char Room Code",
                FiestaOrange,
                KalyeahResources.Texture("ui/dioramas/online") ?? KalyeahResources.Texture("ui/mode_pictures/online"),
                out _onlineTitleLabel);

            OnlineCreateButton = MakeRoomActionButton(
                "Online_Create",
                _onlineCardRect,
                "Create",
                FiestaOrange,
                "ui/button_orange",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(10f, 10f),
                () => OpenOnlineLobby(LobbyIntent.Create),
                out _onlineCreateRect);

            OnlineJoinButton = MakeRoomActionButton(
                "Online_Join",
                _onlineCardRect,
                "Join",
                BarrioBlue,
                "ui/button_blue",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-10f, 10f),
                () => OpenOnlineLobby(LobbyIntent.Join),
                out _onlineJoinRect);

            // Bottom card: Local Hotspot (Host / Join)
            _hotspotCardRect = MakeKomiksFrame(
                "LocalHotspotCard",
                col,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 276f),
                SunlitCream,
                out _);

            _hotspotHeaderRect = BuildRoomCardHeader(
                _hotspotCardRect,
                "LOCAL HOTSPOT",
                "Zero internet · Nearby Wi-Fi & QR scan",
                JadeGreen,
                KalyeahResources.Texture("ui/dioramas/hotspot") ?? KalyeahResources.Texture("ui/mode_pictures/hotspot"),
                out _hotspotTitleLabel);

            HotspotHostButton = MakeRoomActionButton(
                "Hotspot_Host",
                _hotspotCardRect,
                "Host",
                JadeGreen,
                "ui/button_green",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(10f, 10f),
                () => OpenHotspotFlow(host: true),
                out _hotspotHostRect);

            HotspotJoinButton = MakeRoomActionButton(
                "Hotspot_Join",
                _hotspotCardRect,
                "Join",
                BarrioBlue,
                "ui/button_blue",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-10f, 10f),
                () => OpenHotspotFlow(host: false),
                out _hotspotJoinRect);
        }

        static RectTransform BuildRoomCardHeader(
            RectTransform card,
            string title,
            string subtitle,
            Color bannerColor,
            Texture2D art,
            out Text titleLabel)
        {
            RectTransform header = Ui.Panel("Header", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-12f, 136f));
            Image bg = Ui.MakeImage("Bg", header, bannerColor);
            bg.sprite = MenuStack.RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.rectTransform.SetFullStretch();

            if (art != null)
            {
                var rawGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(header, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = art;
                raw.color = new Color(1f, 1f, 1f, 0.35f);
                raw.raycastTarget = false;
                raw.rectTransform.SetFullStretch();
            }

            titleLabel = Ui.MakeText("Title", header, title, 25, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            titleLabel.resizeTextForBestFit = true;
            titleLabel.resizeTextMinSize = 18;
            titleLabel.resizeTextMaxSize = 25;
            RectTransform tr = titleLabel.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.48f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(14f, 0f);
            tr.offsetMax = new Vector2(-12f, -6f);

            Image subPlate = Ui.MakeImage("SubPlate", header, new Color(0.06f, 0.08f, 0.18f, 0.88f));
            subPlate.sprite = MenuStack.RoundedSprite();
            subPlate.type = Image.Type.Sliced;
            RectTransform spRect = subPlate.rectTransform;
            spRect.anchorMin = new Vector2(0f, 0f);
            spRect.anchorMax = new Vector2(1f, 0.48f);
            spRect.offsetMin = new Vector2(6f, 6f);
            spRect.offsetMax = new Vector2(-6f, -2f);

            Text sub = Ui.MakeText("Subtitle", subPlate.transform, subtitle, 18, Ui.Cream, TextAnchor.MiddleLeft);
            sub.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.verticalOverflow = VerticalWrapMode.Truncate;
            sub.resizeTextForBestFit = true;
            sub.resizeTextMinSize = 14;
            sub.resizeTextMaxSize = 18;
            RectTransform sr = sub.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(10f, 2f);
            sr.offsetMax = new Vector2(-10f, -2f);
            return header;
        }

        static Button MakeRoomActionButton(
            string goName,
            Transform parent,
            string labelText,
            Color tint,
            string spritePath,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            Action onClick,
            out RectTransform rect)
        {
            rect = Ui.Panel(goName, parent, anchor, anchor, pivot, pos, new Vector2(114f, 96f));
            Image bg = rect.gameObject.AddComponent<Image>();
            Sprite art = TryCachedSprite(spritePath);
            bg.sprite = art != null ? art : MenuStack.RoundedSprite();
            bg.type = art != null ? Image.Type.Simple : Image.Type.Sliced;
            bg.color = art != null ? Color.white : tint;
            bg.raycastTarget = true;

            Text lbl = Ui.MakeText("Label", rect, labelText, 22, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            RectTransform lr = lbl.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(6f, 4f);
            lr.offsetMax = new Vector2(-6f, -4f);

            return AttachButton(rect.gameObject, bg, onClick);
        }

        /// <summary>Syncs all displayed profile, Ranked tier, Kid portrait/loadout, difficulty, and locale strings.</summary>
        public void Refresh()
        {
            AppSession session = AppSession.Current;
            SettingsStore settings = SettingsStore.Current;
            bool fil = string.Equals(settings.Language, "fil", StringComparison.OrdinalIgnoreCase);

            // 1. PlayerName & ID Avatar
            if (!IsInteractive() && Sheets != null && Sheets.IsOpen)
                Sheets.Close();

            if (PlayerNameInput != null && !PlayerNameInput.isFocused)
            {
                _suppressNameCallback = true;
                PlayerNameInput.text = session.PlayerName;
                _suppressNameCallback = false;
            }

            if (_profileAvatarImage != null && Sheets != null)
            {
                Sheets.SyncFromSession();
                _profileAvatarImage.texture = Sheets.CurrentIdTexture;
                _profileAvatarImage.uvRect = Sheets.CurrentIdUvRect;
                _profileAvatarImage.enabled = Sheets.CurrentIdTexture != null;
            }

            // 2. Ranked Tier Badge
            RankTierInfo tier = session.CurrentTier;
            Color tierMain = ParseHexColor(tier.ColorHex, MangoGold);
            Color tierDark = ParseHexColor(tier.DarkHex, StreetSlate);
            if (_rankFillImage != null) _rankFillImage.color = Color.Lerp(StreetSlate, tierDark, 0.72f);
            if (_rankCrestBg != null) _rankCrestBg.color = tierMain;
            if (_rankTierText != null) _rankTierText.text = tier.Name ?? "Kahoy";
            if (_rankLpText != null)
            {
                int nextLp = session.NextTierLp;
                _rankLpText.text = nextLp > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} / {1} LP", session.Lp, nextLp)
                    : string.Format(CultureInfo.InvariantCulture, "{0} LP · MAX", session.Lp);
            }
            if (_rankProgressFill != null)
            {
                float p = Mathf.Clamp(session.TierProgress01, 0.06f, 1f);
                _rankProgressFill.anchorMax = new Vector2(p, 1f);
                Image fillImg = _rankProgressFill.GetComponent<Image>();
                if (fillImg != null) fillImg.color = tierMain;
            }

            // 3. Selected Kid portrait & epithet
            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (!KidMetadata.TryGetValue(kidId, out KidMeta meta))
                meta = new KidMeta { Display = Capitalize(kidId), Epithet = "BATANG KALYE", Accent = BarrioBlue };

            string displayName = ReadCharacterDisplayName(kidId) ?? meta.Display;
            if (_kidNameText != null) _kidNameText.text = displayName.ToUpperInvariant();
            if (_kidEpithetText != null)
            {
                _kidEpithetText.text = meta.Epithet;
                _kidEpithetText.color = meta.Accent;
            }
            if (_cosmeticNoteText != null)
                _cosmeticNoteText.text = fil ? "PAMPORMA LANG" : "COSMETIC ONLY";
            if (_portraitAuraBg != null)
                _portraitAuraBg.color = Color.Lerp(DuskPlum, meta.Accent, 0.58f);
            if (_kidPortraitImage != null)
            {
                Texture2D portrait = LoadKidPortrait(kidId, out Rect portraitUv, out float portraitAspect);
                _kidPortraitImage.texture = portrait;
                _kidPortraitImage.uvRect = portraitUv;
                _kidPortraitImage.enabled = portrait != null;
                var fit = _kidPortraitImage.GetComponent<AspectRatioFitter>();
                if (fit != null)
                    fit.aspectRatio = portraitAspect;
            }

            // 4. Superpower chips
            IReadOnlyList<string> powers = session.SelectedSuperpowers;
            for (int i = 0; i < _superpowerSlots.Length; i++)
            {
                SuperpowerSlotUi slot = _superpowerSlots[i];
                if (slot == null) continue;
                string wid = (powers != null && i < powers.Count && !string.IsNullOrWhiteSpace(powers[i]))
                    ? powers[i]
                    : AppSession.DefaultSuperpowers[Mathf.Min(i, AppSession.DefaultSuperpowers.Length - 1)];

                ReadWeaponDisplay(wid, out string shortName, out int cost);
                slot.NameLabel.text = shortName;
                slot.CostLabel.text = cost.ToString(CultureInfo.InvariantCulture);

                Texture2D icon = LoadWeaponIcon(wid, out bool isHudFallback, out float iconAspect);
                slot.Icon.texture = icon;
                slot.Icon.enabled = icon != null;
                slot.Icon.color = isHudFallback ? Ui.Cream : Color.white;
                if (slot.IconFitter != null)
                    slot.IconFitter.aspectRatio = iconAspect;
                if (slot.IconWell != null)
                {
                    slot.IconWell.sprite = isHudFallback ? Ui.Circle() : MenuStack.RoundedSprite();
                    slot.IconWell.type = isHudFallback ? Image.Type.Simple : Image.Type.Sliced;
                    slot.IconWell.color = new Color(0.08f, 0.11f, 0.22f, 0.96f);
                }
            }

            // 5. Difficulty & Training pills (disable dark Outline whenever dark Espresso text is active)
            string activeDiff = SettingsStore.NormalizeDifficulty(session.BotDifficulty);
            bool isTraining = session.Mode == GameMode.Training;
            string[] diffs = { "easy", "medium", "hard" };
            for (int i = 0; i < 3; i++)
            {
                bool selected = !isTraining && string.Equals(activeDiff, diffs[i], StringComparison.Ordinal);
                if (_difficultyPillImages[i] != null)
                    _difficultyPillImages[i].color = selected ? PillActiveColor : PillIdleColor;
                if (_difficultyPillLabels[i] != null)
                {
                    _difficultyPillLabels[i].color = selected ? Espresso : Ui.Cream;
                    Outline ol = _difficultyPillLabels[i].GetComponent<Outline>();
                    if (ol != null) ol.enabled = !selected;
                }
            }
            if (_trainingPillImage != null)
                _trainingPillImage.color = isTraining ? PillActiveColor : TrainingPillColor;
            if (_trainingPillLabel != null)
            {
                _trainingPillLabel.color = isTraining ? Espresso : Ui.Cream;
                Outline trainOl = _trainingPillLabel.GetComponent<Outline>();
                if (trainOl != null) trainOl.enabled = !isTraining;
            }

            // 6. Localized chrome strings (keeping canonical mode & route labels crisp)
            if (_swapKitLabel != null)
                _swapKitLabel.text = fil ? "SUPERPOWERS · PALITAN" : "SUPERPOWERS · TAP TO SWAP";
            if (_playPracticeLabel != null)
                _playPracticeLabel.text = fil ? "LARO NA! · 5v5 PRACTICE" : "LARO! · PLAY 5v5 PRACTICE";
            if (_streetSectionTitle != null)
                _streetSectionTitle.text = fil ? "LARONG KALYE · MGA LARO SA KALYE" : "LARONG KALYE · STREET MINI-GAMES";
            if (_onlineTitleLabel != null)
                _onlineTitleLabel.text = "ONLINE ROOM";
            if (_hotspotTitleLabel != null)
                _hotspotTitleLabel.text = "LOCAL HOTSPOT";
            if (_navLabels[0] != null) _navLabels[0].text = fil ? "Buwan" : "Monthly";
            if (_navLabels[1] != null) _navLabels[1].text = fil ? "Suporta" : "Donate";
            if (_navLabels[2] != null) _navLabels[2].text = "Pick";
            if (_navLabels[3] != null) _navLabels[3].text = "Codex";
            if (_navLabels[4] != null) _navLabels[4].text = "History";
            if (_navLabels[5] != null) _navLabels[5].text = "Settings";
            if (_trainingPillLabel != null) _trainingPillLabel.text = "Training";
        }

        /// <summary>Reflows top bar and 3-column hub across 21:9 ultra-wides through 4:3 foldables with &gt;= 48 dp hitboxes.</summary>
        public void RefreshLayout()
        {
            if (_topBar == null || _bodyContainer == null) return;
            if (_canvas == null && Root != null)
                _canvas = Root.GetComponentInParent<Canvas>();

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float safeW = Mathf.Max(640f, safe.x);
            float safeH = Mathf.Max(480f, safe.y);
            float minTouch = ScreenFitMetrics.ClampTouchSize(64f, ScreenFitMetrics.MinTouchDp, _canvas);

            // Top bar sizing (pinned to safe-area top corners)
            float topH = Mathf.Clamp(minTouch, 84f, 104f);
            const float topPad = 10f;
            const float gap = 8f;
            const float navGap = 6f;
            _topBar.anchoredPosition = new Vector2(0f, -6f);
            _topBar.sizeDelta = new Vector2(0f, topH);

            int navCount = _navBtnRects.Length;
            float navBtnW = Mathf.Max(minTouch, Mathf.Min(88f, (safeW * 0.48f - navGap * (navCount - 1)) / navCount));
            for (int i = 0; i < navCount; i++)
            {
                RectTransform nr = _navBtnRects[i];
                if (nr == null) continue;
                int fromRight = (navCount - 1) - i;
                nr.sizeDelta = new Vector2(navBtnW, topH);
                nr.anchoredPosition = new Vector2(-(topPad + fromRight * (navBtnW + navGap)), 0f);
            }

            float rightNavSpan = topPad + navCount * navBtnW + (navCount - 1) * navGap;
            float leftAvail = Mathf.Max(minTouch * 2f + gap, safeW - rightNavSpan - topPad - gap * 2f);
            float nameW = Mathf.Clamp(leftAvail * 0.54f, minTouch, 244f);
            float rankW = Mathf.Clamp(leftAvail - nameW - gap, minTouch, 198f);

            _nameBoxRect.sizeDelta = new Vector2(nameW, topH);
            _nameBoxRect.anchoredPosition = new Vector2(topPad, 0f);

            if (_profileAvatarBtnRect != null)
            {
                bool showAvatar = nameW >= minTouch + 86f;
                _profileAvatarBtnRect.gameObject.SetActive(showAvatar);
                float avatarSide = Mathf.Clamp(topH - 18f, Mathf.Min(minTouch, topH - 12f), 74f);
                _profileAvatarBtnRect.sizeDelta = new Vector2(avatarSide, avatarSide);
                float textLeft = showAvatar ? (avatarSide + 16f) : 14f;
                if (_nameCaptionRect != null) _nameCaptionRect.offsetMin = new Vector2(textLeft, 0f);
                if (_namePlaceholderRect != null) _namePlaceholderRect.offsetMin = new Vector2(textLeft, 10f);
                if (_nameInputTextRect != null) _nameInputTextRect.offsetMin = new Vector2(textLeft, 10f);
            }

            _rankBadgeRect.sizeDelta = new Vector2(rankW, topH);
            _rankBadgeRect.anchoredPosition = new Vector2(topPad + nameW + gap, 0f);

            float leftUsed = topPad + nameW + gap + rankW;
            float centerGap = safeW - leftUsed - rightNavSpan - gap * 2f;
            if (_brandCenterRect != null)
            {
                bool showBrand = centerGap >= 88f;
                _brandCenterRect.gameObject.SetActive(showBrand);
                if (showBrand)
                {
                    float brandW = Mathf.Min(centerGap, 250f);
                    float centerX = leftUsed + gap + centerGap * 0.5f - safeW * 0.5f;
                    _brandCenterRect.anchoredPosition = new Vector2(centerX, 0f);
                    _brandCenterRect.sizeDelta = new Vector2(brandW, topH - 10f);
                }
            }

            Sheets?.RefreshLayout();

            // 3-column body container (clamped max width so 21:9 never stretches cards edge-to-edge)
            float bodyBottom = 8f;
            float bodyTopReserve = topH + 14f;
            float bodyH = Mathf.Max(420f, safeH - bodyTopReserve - bodyBottom);
            float bodyW = Mathf.Clamp(safeW - topPad * 2f, 620f, 1360f);
            _bodyContainer.anchoredPosition = new Vector2(0f, bodyBottom);
            _bodyContainer.sizeDelta = new Vector2(bodyW, bodyH);

            const float colGap = 10f;
            float availCols = bodyW - colGap * 2f;

            // Guarantee leftW + centerW + rightW == availCols with every interactive control >= minTouch
            float minCenterW = minTouch * 4f + 6f * 3f + 20f;
            float minRightW = minTouch * 2f + 8f + 20f;
            float minLeftW = minTouch + 16f;

            float centerW = Mathf.Clamp(availCols * 0.44f, minCenterW, Mathf.Max(minCenterW, availCols - minRightW - minLeftW));
            float rightW = Mathf.Clamp(availCols * 0.29f, minRightW, Mathf.Max(minRightW, availCols - centerW - minLeftW));
            float leftW = Mathf.Max(minTouch, availCols - centerW - rightW);

            _leftCol.anchoredPosition = Vector2.zero;
            _leftCol.sizeDelta = new Vector2(leftW, 0f);

            _centerCol.anchorMin = new Vector2(0f, 0f);
            _centerCol.anchorMax = new Vector2(0f, 1f);
            _centerCol.pivot = new Vector2(0f, 0.5f);
            _centerCol.anchoredPosition = new Vector2(leftW + colGap, 0f);
            _centerCol.sizeDelta = new Vector2(centerW, 0f);

            _rightCol.anchoredPosition = Vector2.zero;
            _rightCol.sizeDelta = new Vector2(rightW, 0f);

            // Column 1 vertical split
            float loadoutH = Mathf.Clamp(bodyH * 0.30f, Mathf.Max(minTouch, 140f), 176f);
            float portraitH = Mathf.Max(minTouch, bodyH - loadoutH - gap);
            _portraitCardRect.sizeDelta = new Vector2(0f, portraitH);
            _loadoutCardRect.sizeDelta = new Vector2(0f, loadoutH);

            // Column 2 vertical split (5v5 Practice + Larong Kalye)
            float practiceMinH = minTouch * 2f + 86f;
            float practiceH = Mathf.Clamp(bodyH * 0.57f, practiceMinH, Mathf.Max(practiceMinH, bodyH - (minTouch + 46f) - gap));
            float streetH = Mathf.Max(minTouch + 42f, bodyH - practiceH - gap);
            practiceH = bodyH - streetH - gap;
            _practiceCardRect.sizeDelta = new Vector2(0f, practiceH);
            _streetCardRect.sizeDelta = new Vector2(0f, streetH);

            float playCtaH = Mathf.Clamp(minTouch, 84f, 102f);
            _playPracticeRect.anchoredPosition = new Vector2(0f, 8f);
            _playPracticeRect.sizeDelta = new Vector2(centerW - 20f, playCtaH);

            float pillY = 8f + playCtaH + 6f;
            float pillH = Mathf.Clamp(minTouch, 80f, Mathf.Max(minTouch, practiceH - pillY - 68f));
            float pillGap = 6f;
            float pillW = Mathf.Max(minTouch, (centerW - 20f - pillGap * 3f) / 4f);
            for (int i = 0; i < 4; i++)
            {
                RectTransform pr = _pillRects[i];
                if (pr == null) continue;
                pr.anchoredPosition = new Vector2(10f + i * (pillW + pillGap), pillY);
                pr.sizeDelta = new Vector2(pillW, pillH);
            }

            if (_practiceBannerRect != null)
            {
                float bannerH = Mathf.Clamp(practiceH - (pillY + pillH) - 12f, 52f, 88f);
                _practiceBannerRect.sizeDelta = new Vector2(-12f, bannerH);
            }

            float streetCardBottom = 8f;
            float streetCardH = Mathf.Max(minTouch, streetH - 42f);
            float streetGap = 8f;
            float streetW = Mathf.Max(minTouch, (centerW - 20f - streetGap * 2f) / 3f);
            for (int i = 0; i < 3; i++)
            {
                RectTransform sr = _streetModeRects[i];
                if (sr == null) continue;
                sr.anchoredPosition = new Vector2(10f + i * (streetW + streetGap), streetCardBottom);
                sr.sizeDelta = new Vector2(streetW, streetCardH);
            }

            // Column 3 vertical split (Online + Hotspot)
            float roomCardH = (bodyH - gap) * 0.5f;
            _onlineCardRect.sizeDelta = new Vector2(0f, roomCardH);
            _hotspotCardRect.sizeDelta = new Vector2(0f, roomCardH);

            float roomBtnW = Mathf.Max(minTouch, (rightW - 28f) * 0.5f);
            float roomBtnH = Mathf.Clamp(minTouch, 84f, Mathf.Max(minTouch, roomCardH - 104f));
            _onlineCreateRect.sizeDelta = new Vector2(roomBtnW, roomBtnH);
            _onlineJoinRect.sizeDelta = new Vector2(roomBtnW, roomBtnH);
            _hotspotHostRect.sizeDelta = new Vector2(roomBtnW, roomBtnH);
            _hotspotJoinRect.sizeDelta = new Vector2(roomBtnW, roomBtnH);

            float roomHeaderH = Mathf.Clamp(roomCardH - roomBtnH - 22f, 68f, 140f);
            if (_onlineHeaderRect != null) _onlineHeaderRect.sizeDelta = new Vector2(-12f, roomHeaderH);
            if (_hotspotHeaderRect != null) _hotspotHeaderRect.sizeDelta = new Vector2(-12f, roomHeaderH);

            if (Root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(Root);
        }

        public void SetDifficulty(string difficulty)
        {
            string norm = SettingsStore.NormalizeDifficulty(difficulty);
            SettingsStore.Current.BotDifficulty = norm;
            AppSession.Current.BotDifficulty = norm;
            AppSession.Current.Mode = GameMode.Practice;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            TapFeedback();
            Refresh();
        }

        void OnPlayerNameEndEdit(string raw)
        {
            if (_suppressNameCallback) return;
            AppSession.Current.PlayerName = raw;
            if (PlayerNameInput != null)
            {
                _suppressNameCallback = true;
                PlayerNameInput.text = AppSession.Current.PlayerName;
                _suppressNameCallback = false;
            }
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            TapFeedback();
        }

        void Navigate(string screenId)
        {
            TapFeedback();
            _onNavigate?.Invoke(screenId);
        }

        void LaunchPractice()
        {
            AppSession.Current.BotDifficulty = SettingsStore.NormalizeDifficulty(AppSession.Current.BotDifficulty);
            LaunchMode(GameMode.Practice);
        }

        void LaunchTraining() => LaunchMode(GameMode.Training);

        void LaunchMode(GameMode mode)
        {
            AppSession.Current.Mode = mode;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Haptics.LightTap();
            _audio?.PlayUiTap("ready");
            _onLaunchMatch?.Invoke(mode);
        }

        void OpenOnlineLobby(LobbyIntent intent)
        {
            AppSession.Current.Mode = GameMode.Online;
            AppSession.Current.Intent = intent;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Navigate(MenuStack.LobbyId);
        }

        void OpenHotspotFlow(bool host)
        {
            AppSession.Current.Mode = host ? GameMode.HotspotHost : GameMode.HotspotClient;
            AppSession.Current.Intent = host ? LobbyIntent.Create : LobbyIntent.Join;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Navigate(MenuStack.HotspotId);
        }

        void TapFeedback()
        {
            Haptics.LightTap();
            _audio?.PlayUiTap(false);
        }

        string ReadCharacterDisplayName(string kidId)
        {
            if (_data == null || string.IsNullOrWhiteSpace(kidId)) return null;
            try
            {
                Dictionary<string, object> p = _data.Props("characters__" + kidId);
                if (p != null && p.TryGetValue("display_name", out object dn) && dn is string s && !string.IsNullOrWhiteSpace(s))
                    return s;
            }
            catch { }
            return null;
        }

        void ReadWeaponDisplay(string weaponId, out string shortName, out int elixirCost)
        {
            shortName = Capitalize(weaponId);
            elixirCost = 2;
            if (string.IsNullOrWhiteSpace(weaponId)) return;

            if (_data != null)
            {
                try
                {
                    Dictionary<string, object> p = _data.Props("weapons__" + weaponId);
                    if (p != null)
                    {
                        if (p.TryGetValue("short_name", out object sn) && sn is string s && !string.IsNullOrWhiteSpace(s))
                            shortName = s;
                        else if (p.TryGetValue("display_name", out object dn) && dn is string ds && !string.IsNullOrWhiteSpace(ds))
                            shortName = ds;

                        if (p.TryGetValue("elixir_cost", out object ec) && ec != null)
                            elixirCost = Convert.ToInt32(ec, CultureInfo.InvariantCulture);
                        return;
                    }
                }
                catch { }
            }

            int underscore = shortName.IndexOf('_');
            if (underscore > 0) shortName = shortName.Substring(0, underscore);
        }

        static Texture2D LoadKidPortrait(string kidId, out Rect uvRect, out float aspectRatio)
        {
            uvRect = new Rect(0f, 0f, 1f, 1f);
            aspectRatio = 1f;
            if (string.IsNullOrWhiteSpace(kidId)) return null;

            // portraits/<id>.png & portraits/cards/<id>.png are 600x800 (3:4) on disk (NPOT-imported at 512x1024);
            // top 75% in UV space (v: 0.25..1.00) maps 600x600 source pixels for a distortion-free 1:1 square bust.
            Texture2D bust = KalyeahResources.Texture("portraits/" + kidId);
            if (bust != null)
            {
                uvRect = new Rect(0f, 0.25f, 1f, 0.75f);
                return bust;
            }

            Texture2D face = KalyeahResources.Texture("portraits/faces/" + kidId);
            if (face != null)
                return face;

            Texture2D cardBust = KalyeahResources.Texture("portraits/cards/" + kidId);
            if (cardBust != null)
            {
                uvRect = new Rect(0f, 0.25f, 1f, 0.75f);
                return cardBust;
            }

            Texture2D charCard = KalyeahResources.Texture("ui/character_cards/" + kidId + "_card")
                ?? KalyeahResources.Texture("ui/character_cards/" + kidId);
            if (charCard != null)
            {
                uvRect = new Rect(0f, 0.3487f, 1f, 0.6513f);
                return charCard;
            }

            return KalyeahResources.Texture("ui/avatars/" + kidId);
        }

        static Texture2D LoadWeaponIcon(string weaponId, out bool isHudFallback, out float aspectRatio)
        {
            isHudFallback = false;
            aspectRatio = 1f;
            if (string.IsNullOrWhiteSpace(weaponId)) return null;

            Texture2D painted = Resources.Load<Texture2D>("icons/art/" + weaponId);
            if (painted != null)
                return painted;

            Texture2D skillCard = KalyeahResources.Texture("ui/skill_cards/" + weaponId);
            if (skillCard != null)
            {
                aspectRatio = 0.756f;
                return skillCard;
            }

            Texture2D hudIcon = KalyeahResources.Texture("ui/hud_icons/" + weaponId);
            if (hudIcon != null)
            {
                isHudFallback = true;
                return hudIcon;
            }

            return null;
        }

        static RectTransform MakeKomiksFrame(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color fillColor,
            out Image fillImage)
        {
            RectTransform outer = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image ink = outer.gameObject.AddComponent<Image>();
            ink.sprite = MenuStack.RoundedSprite();
            ink.type = Image.Type.Sliced;
            ink.color = KomiksInk;
            ink.raycastTarget = false;

            RectTransform inner = Ui.Fill("Surface", outer);
            inner.offsetMin = new Vector2(3f, 3f);
            inner.offsetMax = new Vector2(-3f, -3f);
            fillImage = inner.gameObject.AddComponent<Image>();
            fillImage.sprite = MenuStack.RoundedSprite();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            return outer;
        }

        static Button AttachButton(GameObject host, Graphic targetGraphic, Action onClick)
        {
            Button btn = host.GetComponent<Button>() ?? host.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.98f, 0.90f, 1f);
            cb.pressedColor = new Color(0.84f, 0.84f, 0.88f, 1f);
            cb.selectedColor = new Color(1f, 0.98f, 0.90f, 1f);
            btn.colors = cb;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        static Sprite TryCachedSprite(string resourcePath) => MenuStack.TryUiSprite(resourcePath);

        static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string clean = hex.Trim();
            if (!clean.StartsWith("#", StringComparison.Ordinal)) clean = "#" + clean;
            return ColorUtility.TryParseHtmlString(clean, out Color parsed) ? parsed : fallback;
        }

        static string Capitalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            return s.Length == 1
                ? s.ToUpperInvariant()
                : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }

    internal static class MenuRectExtensions
    {
        public static void SetFullStretch(this RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
