using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Locked Option A1 Main Lobby (1280x720 reference, SafeArea-pinned, all active buttons &gt;= 48 dp):
    /// L1 full-bleed live 10:30 AM sunlit 3D Manila Barangay street corner (<see cref="HeroStage.ShowManilaStreetStage"/>)
    /// with zero sky watermarks and zero hero nameplate; L3 cardless top-left circular kid bust + bottom-rim level badge +
    /// bare condensed IGN strictly matched to L4's <see cref="TopBarHeight"/> (<c>56 cpx</c>) and shared centerline;
    /// L4 flat smoked-obsidian currency strip + square Settings gear; L5 three slim smoked-obsidian mission slivers with
    /// crisp Paper-White left accent ticks; L6 flat rectangular bottom-left nav rail (KIDS · STORE · FRIENDS);
    /// L7 high-contrast Paper-White mode pill; and L8 chamfered Signal Vermilion PLAY CTA.
    /// Offline is never a mode on Home: the connection check runs at launch (<see cref="CheckConnectionAtLaunch"/>) and on PLAY.
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color ObsidianGlass = MenuArt.ObsidianGlass;
        static readonly Color ObsidianSolid = MenuArt.ObsidianSolid;
        static readonly Color PaperWhite = MenuArt.PaperWhite;
        static readonly Color SignalVermilion = MenuArt.SignalVermilion;
        static readonly Color SignalVermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color TitaniumRim = MenuArt.TitaniumRim;
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.86f, 0.88f, 0.92f, 1f);
        static readonly Color SkyFallback = new Color(0.620f, 0.816f, 0.949f, 1f);      // #9ED0F2
        static readonly Color AvatarWellBg = new Color(0.11f, 0.15f, 0.21f, 1f);
        static readonly Color GlassHairline = new Color(TitaniumRim.r, TitaniumRim.g, TitaniumRim.b, 0.22f);
        static readonly Color SoftShadowInk = new Color(0.055f, 0.075f, 0.102f, 0.80f);
        static readonly Color OfflineSkyTop = new Color(0.08f, 0.12f, 0.18f, 1f);

        /// <summary>Locked shared visual height (<c>56 cpx</c> on 720p) for both L3 cardless profile and L4 currency/settings bar.</summary>
        public const float TopBarHeight = 56f;

        const float HitSize = 96f;
        const float TopBandHeight = HitSize;
        const float Gutter = 20f;
        const float PlayHitHeight = HitSize;
        const float PlayVisualHeight = 84f;
        const float ModeHitHeight = HitSize;
        const float ModeVisualHeight = 44f;
        const float ModeToPlayHitGap = 2f;
        const float ColumnGap = 16f;
        const float NavItemWidth = 98f;
        const float NavItemGap = 2f;
        const float NavDockPadX = 2f;
        const float BottomNavWidth = NavItemWidth * 3f + NavItemGap * 2f + NavDockPadX * 2f; // 302f
        const float NavVisualHeight = 54f;
        const float DefaultLeftWidth = 324f;
        const float CardHitHeight = HitSize;
        const float CardVisualHeight = 40f;
        const float CardGap = 2f;
        const float WeaveTile = 96f;

        static readonly string[] Difficulties = { "easy", "medium", "hard" };

        sealed class HomeLifecycle : MonoBehaviour
        {
            public HomeScreenView Owner;
            void OnEnable() => Owner?.OnEnabled();
            void OnDisable() => Owner?.OnDisabled();
            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();
            void OnDestroy() => Owner?.OnDisabled();
            void Update() => Owner?.Tick(Time.unscaledDeltaTime);
        }

        readonly DataSource _data;
        readonly Action<string> _onNavigate;
        readonly Action<GameMode> _onLaunchMatch;
        readonly AudioHub _audio;
        readonly HeroStage _stage;
        readonly List<Material> _ownedMaterials = new List<Material>(8);

        Canvas _canvas;
        SafeArea _safeArea;
        bool _subscribed;
        bool _disposed;

        // Live 3D sunlit Manila street stage
        RectTransform _stageHost;
        RawImage _heroImage;
        AspectRatioFitter _heroFitter;

        // Bars and columns
        RectTransform _topBar;
        RectTransform _hubBody;
        RectTransform _bottomNav;
        RectTransform _heroZone;
        RectTransform _modeColumn;
        RectTransform _modeZone;
        RectTransform _playZone;
        RectTransform _kidPlate;
        RectTransform _leftColumn;

        Image _avatarBustImage;
        RawImage _avatarPhotoRaw;
        Text _playerName;
        Text _levelText;

        RectTransform _offlinePrompt;
        RectTransform _offlineHubBackdrop;
        RectTransform _offlineHub;
        Text _difficultyLabel;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public HomeSheetsView Sheets { get; private set; }

        public Button ProfileButton { get; private set; }
        public Button KidButton { get; private set; }
        public Button PlayButton { get; private set; }
        public RectTransform ProfileVisualRect { get; private set; }
        public RectTransform CurrencyStripVisualRect { get; private set; }
        public RectTransform SettingsVisualRect { get; private set; }
        public bool IsHeroPlateVisible => _kidPlate != null && _kidPlate.gameObject.activeInHierarchy;
        public bool IsManilaStreetStageActive => _stage != null && _stage.ShowManilaStreetStage;
        public bool OfflinePromptOpen => _offlinePrompt != null && _offlinePrompt.gameObject.activeSelf;
        /// <summary>The hero stage's texture and state line (capture diagnostics).</summary>
        public Texture HeroTexture => _stage?.Texture;
        public string HeroDebug => _stage?.Describe();
        public void RenderHeroNow() => _stage?.RenderNow();

        public bool OfflineHubOpen => _offlineHub != null && _offlineHub.gameObject.activeSelf;

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
            _stage = new HeroStage { ShowManilaStreetStage = true };

            ScreenFitMetrics.EnsureEnhancedTouch();

            Root = Ui.Fill("HomeScreen", parent);
            BuildBackdrop(Root);
            BuildHeroStageLayer(Root);

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
            BuildHub(SafeRoot);
            BuildOfflineHub();
            BuildOfflinePrompt();

            Sheets = new HomeSheetsView(Root, _data, _audio, IsInteractive);
            Sheets.ProfileChanged += Refresh;
            Sheets.HistoryRequested += OpenHistory;

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

        static DataSource TryDefaultData()
        {
            try { return KalyeahResources.Data(); }
            catch { return null; }
        }

        bool IsInteractive()
        {
            if (_canvas == null && Root != null)
                _canvas = Root.GetComponentInParent<Canvas>();
            if (_canvas == null) return true;
            GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();
            return raycaster == null || raycaster.enabled;
        }

        /// <summary>Android Back: closes the offline prompt, then the offline hub, then any sheet.</summary>
        public bool HandleBack()
        {
            if (OfflinePromptOpen) { CloseOfflinePrompt(); return true; }
            if (OfflineHubOpen) { CloseOfflineHub(); return true; }
            return Sheets != null && Sheets.Close();
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
            _entry = 0f;
            _entryDone = false;
            Refresh();
            RefreshLayout();
        }

        // Play-mode staggered entrance for the HUD clusters (EditMode stays fully visible at alpha 1)
        float _entry;
        bool _entryDone;
        CanvasGroup[] _groups;

        static CanvasGroup Group(RectTransform rect)
        {
            if (rect == null) return null;
            return rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();
        }

        void AnimateEntry(float dt)
        {
            if (_entryDone) return;
            bool still = SettingsStore.Current.ReduceMotion;
            if (_groups == null) _groups = new[] { Group(_topBar), Group(_leftColumn), Group(_bottomNav), Group(_modeColumn) };
            _entry = still ? 10f : _entry + dt;
            bool allDone = true;
            for (int i = 0; i < _groups.Length; i++)
            {
                if (_groups[i] == null) continue;
                float t = Mathf.Clamp01((_entry - 0.06f - 0.07f * i) / 0.30f);
                if (t < 1f) allDone = false;
                float eased = t >= 1f ? 1f : 1f - (1f - t) * (1f - t) * (1f - t);
                _groups[i].alpha = eased;
                _groups[i].transform.localScale = t >= 1f ? Vector3.one : Vector3.one * (0.97f + 0.03f * eased);
            }
            if (allDone) _entryDone = true;
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

        void Tick(float dt)
        {
            if (_disposed || !Application.isPlaying) return;
            _stage?.Tick(dt);
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toast.gameObject.SetActive(false);
            AnimateEntry(dt);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnDisabled();
            if (Sheets != null)
            {
                Sheets.ProfileChanged -= Refresh;
                Sheets.HistoryRequested -= OpenHistory;
                Sheets.Dispose();
                Sheets = null;
            }
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null) KalyeahResources.Kill(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        // ---------------------------------------------------------------- L1 live sunlit 3D Manila stage

        void BuildBackdrop(RectTransform parent)
        {
            // Full-bleed sunlit tropical sky fallback behind the live 16:9 3D Manila street stage (retires legacy ui/home/stall)
            RectTransform bg = Ui.Fill("Backdrop", parent);
            Image sky = bg.gameObject.AddComponent<Image>();
            sky.color = SkyFallback;
            sky.raycastTarget = false;
        }

        void BuildHeroStageLayer(RectTransform parent)
        {
            _stageHost = Ui.Fill("StageHost", parent);
            _stageHost.gameObject.AddComponent<RectMask2D>();

            _heroImage = MakeRaw("Hero", _stageHost, _stage.Texture);
            _heroFitter = _heroImage.gameObject.AddComponent<AspectRatioFitter>();
            _heroFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _heroFitter.aspectRatio = (float)HeroStage.StreetTextureWidth / HeroStage.StreetTextureHeight;
            _stage.AttachOrbitDrag(_heroImage.rectTransform, 0.45f);
        }

        // ---------------------------------------------------------------- L3 + L4 shared top bar (exact 56 cpx height & centerline)

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBandHeight));

            // L3: Custom Cardless Profile (NO backing card/box/pill; exact 56 cpx height matching L4 on shared centerline)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(260f, HitSize));
            Image profileHit = profile.gameObject.AddComponent<Image>();
            profileHit.color = Clear;
            profileHit.raycastTarget = true;

            ProfileVisualRect = Ui.Panel("Visual", profile, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, TopBarHeight));

            RectTransform avatar = Ui.Panel("Avatar", ProfileVisualRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(TopBarHeight, TopBarHeight));
            Image avatarRim = Ui.MakeImage("Rim", avatar, PaperWhite, round: true);
            Stretch(avatarRim.rectTransform, Vector2.zero, Vector2.one);

            Image well = Ui.MakeImage("Well", avatar, AvatarWellBg, round: true);
            Stretch(well.rectTransform, Vector2.zero, Vector2.one);
            well.rectTransform.offsetMin = new Vector2(2f, 2f);
            well.rectTransform.offsetMax = new Vector2(-2f, -2f);
            well.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _avatarBustImage = Ui.MakeImage("Bust", well.rectTransform, Color.white);
            _avatarBustImage.preserveAspect = true;
            Place(_avatarBustImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -17f), new Vector2(96f, 128f));

            _avatarPhotoRaw = MakeRaw("Photo", well.rectTransform, null);
            _avatarPhotoRaw.enabled = false;

            // Small 20x20 dark-obsidian circular level badge (#0E131A + 1.5 px white ring) overlapping bottom center rim
            RectTransform levelBadge = Ui.Panel("Level", avatar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(20f, 20f));
            Image levelRing = Ui.MakeImage("Ring", levelBadge, PaperWhite, round: true);
            Stretch(levelRing.rectTransform, Vector2.zero, Vector2.one);
            Image levelCore = Ui.MakeImage("Core", levelBadge, ObsidianSolid, round: true);
            Stretch(levelCore.rectTransform, Vector2.zero, Vector2.one);
            levelCore.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
            levelCore.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
            _levelText = Label("Text", levelCore.rectTransform, "9", 13, Color.white, TextAnchor.MiddleCenter, display: true, ink: false);
            Stretch(_levelText.rectTransform, Vector2.zero, Vector2.one);

            // Bare uppercase condensed IGN directly to the right (10 cpx gap) at 24 px cap-height matching L4 currencies; zero rank badge
            _playerName = Label("Name", ProfileVisualRect, AppSession.DefaultPlayerName, 26, Color.white, TextAnchor.MiddleLeft, display: true, ink: true, shadowAlpha: 0.78f);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(TopBarHeight + 10f, 0f), new Vector2(-(TopBarHeight + 10f), 38f));
            FitSingleLine(_playerName, 12, 26);

            ProfileButton = AttachSpringButton(profile.gameObject, avatarRim, () => Navigate(MenuStack.ProfileId), 0.95f, 1.5f);

            // L4a: Square 56x56 smoked-obsidian Settings gear button centered inside 96x96 (>= 48 dp) hit area on exact top-bar centerline
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(HitSize, HitSize));
            Image settingsHit = settings.gameObject.AddComponent<Image>();
            settingsHit.color = Clear;
            settingsHit.raycastTarget = true;

            // Align visual right edge with Gutter (20 cpx from safe right edge -> -18 cpx inside settings hit rect)
            SettingsVisualRect = Ui.Panel("Visual", settings, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(Gutter - 2f), 0f), new Vector2(TopBarHeight, TopBarHeight));
            Image settingsPlate = MakeTacticalGlassPlate("Plate", SettingsVisualRect, ObsidianGlass, GlassHairline, Clear, cornerCut: 3f, accentW: 0f, topHighlight: 0.14f);
            Image gear = Ui.MakeImage("Gear", SettingsVisualRect, PaperWhite);
            gear.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Gear);
            Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            AttachSpringButton(settings.gameObject, settingsPlate, () => Navigate(MenuStack.SettingsId), 0.92f, 1.5f);

            // L4b: Unified flat 56 cpx smoked-obsidian Currency strip (Barya + Diamonds) on exact same centerline, 10 cpx visual gap to Settings
            const float currencyStripHitW = 268f;
            RectTransform currencies = Ui.Panel("Currencies", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(HitSize + 6f), 0f), new Vector2(currencyStripHitW, HitSize));

            // Single unified smoked-obsidian bar spanning smoothly to within 10 cpx of the square Settings button
            CurrencyStripVisualRect = Ui.Panel("Visual", currencies, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, TopBarHeight));
            MakeTacticalGlassPlate("Plate", CurrencyStripVisualRect, ObsidianGlass, GlassHairline, Clear, cornerCut: 3f, accentW: 0f, topHighlight: 0.14f);

            MakeCurrencyTapZone(currencies, 0f, 156f, "12,450", MenuArt.BaryaCoin(), "barya", () => ComingSoon("Barya: earned in matches and tasks"));
            MakeCurrencyTapZone(currencies, 158f, 110f, "80", MenuArt.DiamondGem(), "diamonds", () => ComingSoon("Diamonds: the shop opens with accounts"));
        }

        void MakeCurrencyTapZone(RectTransform row, float xLeft, float width, string value, Sprite iconSprite, string name, Action onClick)
        {
            RectTransform chip = Ui.Panel("Chip_" + name, row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(xLeft, 0f), new Vector2(width, HitSize));
            Image hit = chip.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            RectTransform content = Ui.Panel("Content", chip, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, TopBarHeight));

            Image icon = Ui.MakeImage("Coin", content, Color.white);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(28f, 28f));

            Text v = Label("Value", content, value, 26, PaperWhite, TextAnchor.MiddleLeft, display: true, ink: true, shadowAlpha: 0.70f);
            Stretch(v.rectTransform, Vector2.zero, Vector2.one);
            v.rectTransform.offsetMin = new Vector2(50f, 0f);
            v.rectTransform.offsetMax = new Vector2(-6f, 0f);
            FitSingleLine(v, 12, 26);

            AttachSpringButton(chip.gameObject, icon, onClick, 0.94f, 1f);
        }

        // ---------------------------------------------------------------- L5 + L6 + L7 + L8 hub body

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // Child 0 (L5): Three ultra-slim airy smoked-obsidian slivers with crisp 3 px Paper-White left accent ticks
            _leftColumn = Ui.Panel("LeftColumn", _hubBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(DefaultLeftWidth, 3f * CardHitHeight + 2f * CardGap));
            MakeLeftSliver(0, "Pass", "PASS LV. 12 — Rescue 3 teammates", () => ComingSoon("The battle pass comes with accounts"));
            MakeLeftSliver(1, "Event", "SUMMER SHOWDOWN", () => ComingSoon("Events come with accounts"));
            MakeLeftSliver(2, "Daily", "DAY 3 GIFT — 100 Barya", () => ComingSoon("Daily gifts come with accounts"));

            // Child 1 (L6): Sharp flat rectangular smoked-obsidian bottom-left navigation rail (KIDS · STORE · FRIENDS)
            _bottomNav = Ui.Panel("BottomNav", _hubBody, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(BottomNavWidth, HitSize));
            RectTransform dockVisual = Ui.Panel("DockVisual", _bottomNav, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, NavVisualHeight));
            MakeTacticalGlassPlate("DockPlate", dockVisual, ObsidianGlass, GlassHairline, Clear, cornerCut: 2f, accentW: 0f, topHighlight: 0.14f);

            MakeNavItem(_bottomNav, 0, "Nav_Kids", "KIDS", () => Navigate(MenuStack.KidsId));
            MakeNavItem(_bottomNav, 1, "Nav_Store", "STORE", () => ComingSoon("The store comes with accounts"));
            MakeNavItem(_bottomNav, 2, "Nav_Friends", "FRIENDS", () => ComingSoon("Friends and party come with accounts"));

            // Child 2: Open middle hero breathing column (zero visible hero nameplate on Home; KidButton kept inactive for API compatibility)
            _heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _kidPlate = Ui.Panel("KidPlate", _heroZone, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(240f, HitSize));
            Image hiddenHit = _kidPlate.gameObject.AddComponent<Image>();
            hiddenHit.color = Clear;
            hiddenHit.raycastTarget = false;
            KidButton = AttachSpringButton(_kidPlate.gameObject, hiddenHit, () => Navigate(MenuStack.KidPageId), 0.96f, 0f);
            _kidPlate.gameObject.SetActive(false);

            // Child 3 (L7 + L8): Paper-White mode pill stacked cleanly above chamfered Signal Vermilion PLAY CTA
            _modeColumn = Ui.Panel("ModeColumn", _hubBody, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(340f, 0f));
            BuildModePill(_modeColumn);
            BuildPlayCta(_modeColumn);

            BuildToast(parent);
        }

        /// <summary>One ultra-slim 40 cpx smoked-obsidian editorial sliver with 3 px Paper-White left tick inside a 96 cpx (&gt;= 48 dp) hit area.</summary>
        void MakeLeftSliver(int index, string name, string copy, Action onClick)
        {
            RectTransform card = Ui.Panel("Card_" + name, _leftColumn, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * (CardHitHeight + CardGap)), new Vector2(0f, CardHitHeight));
            Image hit = card.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            // Evenly spaced 32 cpx airy gaps between the three 40 cpx visual slivers while keeping 96 cpx sibling hit rects 100% disjoint
            float bottomOffset = index == 0 ? 2f : (index == 1 ? 28f : 54f);
            float topOffset = CardHitHeight - CardVisualHeight - bottomOffset;

            RectTransform visual = Ui.Fill("Visual", card);
            visual.offsetMin = new Vector2(0f, bottomOffset);
            visual.offsetMax = new Vector2(0f, -topOffset);
            Image plate = MakeTacticalGlassPlate("Plate", visual, ObsidianGlass, GlassHairline, PaperWhite, cornerCut: 3f, accentW: 3f, topHighlight: 0.12f);

            Text label = Label("Line", visual, copy, 20, PaperWhite, TextAnchor.MiddleLeft, display: true, ink: false);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            FitSingleLine(label, 9, 20);

            AttachSpringButton(card.gameObject, plate, onClick, 0.96f, 1.5f);
        }

        /// <summary>One uppercase condensed nav item (&gt;= 48 dp hit area) seated on the flat bottom-left smoked-obsidian rail.</summary>
        void MakeNavItem(RectTransform bar, int index, string name, string title, Action onClick)
        {
            float x = NavDockPadX + index * (NavItemWidth + NavItemGap);
            RectTransform item = Ui.Panel(name, bar, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(x, 0f), new Vector2(NavItemWidth, 0f));
            Image hit = item.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            Text label = Label("Label", item, title, 24, PaperWhite, TextAnchor.MiddleCenter, display: true, ink: false);
            Place(label.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(-8f, NavVisualHeight));
            FitSingleLine(label, 12, 24);

            if (index > 0)
            {
                Image dot = Ui.MakeImage("Dot", item, new Color(PaperWhite.r, PaperWhite.g, PaperWhite.b, 0.55f), round: true);
                Place(dot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-1f, NavVisualHeight * 0.5f), new Vector2(4f, 4f));
            }

            AttachSpringButton(item.gameObject, label, onClick, 0.94f, 1.5f);
        }

        /// <summary>L7: High-contrast Paper-White rounded mode pill with crisp dark charcoal ink (zero outline smudge), stacked directly over PLAY.</summary>
        void BuildModePill(RectTransform column)
        {
            _modeZone = Ui.Panel("ModeZone", column, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PlayHitHeight + ModeToPlayHitGap), new Vector2(0f, ModeHitHeight));
            Image hit = _modeZone.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            // Visual pill sits cleanly near bottom of its 96 cpx hit zone (16 cpx above the 84 cpx PLAY face)
            RectTransform pill = Ui.Panel("Pill", _modeZone, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(0f, ModeVisualHeight));
            Image plate = Ui.MakeSliced("Plate", pill, MenuArt.RoundSolid(22), PaperWhite);

            _modeLabel = Label("ModeLabel", pill, "", 21, ObsidianSolid, TextAnchor.MiddleCenter, display: true, ink: false);
            Ui.ClearOutline(_modeLabel);
            Stretch(_modeLabel.rectTransform, Vector2.zero, Vector2.one);
            _modeLabel.rectTransform.offsetMin = new Vector2(16f, 0f);
            _modeLabel.rectTransform.offsetMax = new Vector2(-16f, 0f);
            FitSingleLine(_modeLabel, 9, 21);

            ModeButton = AttachSpringButton(_modeZone.gameObject, plate, CycleHomeMode, 0.96f, 2f);
            ShowHomeMode();
        }

        /// <summary>L8: Sharp chamfered Signal Vermilion PLAY CTA with GPU diagonal metallic sheen and tactile 3 px press spring.</summary>
        void BuildPlayCta(RectTransform column)
        {
            _playZone = Ui.Panel("PlayZone", column, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PlayHitHeight));
            Image hit = _playZone.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            RectTransform visual = Ui.Panel("Visual", _playZone, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PlayVisualHeight));
            Image plate = MakeMetallicCtaPlate("Plate", visual, SignalVermilion, SignalVermilionDeep, cornerCut: 10f);

            Text label = Label("Label", visual, "PLAY", 54, Color.white, TextAnchor.MiddleCenter, display: true, ink: true, shadowAlpha: 0.42f);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(12f, 3f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            FitSingleLine(label, 28, 54);

            PlayButton = AttachSpringButton(_playZone.gameObject, plate, PressPlay, 0.95f, 3f);
        }

        // ---------------------------------------------------------------- toast

        RectTransform _toast;
        Text _toastText;
        float _toastUntil;

        /// <summary>A sleek smoked-obsidian notification strip at top centre for panels whose systems arrive later.</summary>
        void BuildToast(RectTransform parent)
        {
            _toast = Ui.Panel("Toast", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TopBandHeight + 4f)), new Vector2(580f, 48f));
            MakeTacticalGlassPlate("Plate", _toast, ObsidianSolid, GlassHairline, PaperWhite, cornerCut: 4f, accentW: 3f, topHighlight: 0.14f);
            _toastText = Label("Text", _toast, "", 20, PaperWhite, TextAnchor.MiddleCenter, display: false, ink: false);
            Stretch(_toastText.rectTransform, Vector2.zero, Vector2.one);
            _toastText.rectTransform.offsetMin = new Vector2(20f, 0f);
            _toastText.rectTransform.offsetMax = new Vector2(-16f, 0f);
            FitSingleLine(_toastText, 12, 20);
            _toast.gameObject.SetActive(false);
        }

        /// <summary>The message the last panel tap showed (empty when none is up).</summary>
        public string ToastText => _toast != null && _toast.gameObject.activeSelf ? _toastText.text : "";

        void ComingSoon(string message)
        {
            TapFeedback();
            if (_toast == null) return;
            _toastText.text = message;
            _toast.SetAsLastSibling();
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        // ---------------------------------------------------------------- offline prompt & hub (Option A1 unified obsidian + vermilion)

        void BuildOfflinePrompt()
        {
            _offlinePrompt = Ui.Fill("OfflinePrompt", Root);
            Image scrim = _offlinePrompt.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.03f, 0.05f, 0.08f, 0.82f);
            scrim.raycastTarget = true;

            RectTransform card = Ui.Panel("Card", _offlinePrompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 308f));
            MakeTacticalGlassPlate("Plate", card, ObsidianSolid, GlassHairline, PaperWhite, cornerCut: 8f, accentW: 4f, topHighlight: 0.16f);

            Text kicker = Label("Kicker", card, "CONNECTION CHECK", 18, TitaniumRim, TextAnchor.UpperLeft, display: true, ink: false);
            Place(kicker.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(-72f, 24f));
            FitSingleLine(kicker, 13, 18);

            Text title = Label("Title", card, "YOU'RE OFFLINE", 44, PaperWhite, TextAnchor.UpperLeft, display: true, ink: false);
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(36f, -50f), new Vector2(-72f, 54f));
            FitSingleLine(title, 28, 44);

            Image rule = Ui.MakeImage("Rule", card, PaperWhite);
            Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -108f), new Vector2(72f, 3f));

            Text body = Label("Body", card, "Play offline now, or retry once you are connected.", 20, Muted, TextAnchor.UpperLeft, display: false, ink: false);
            Place(body.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(36f, -124f), new Vector2(-72f, 32f));
            FitSingleLine(body, 12, 20);

            RectTransform actions = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(-72f, HitSize));
            MakeActionButton(actions, "Retry", "RETRY", false, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Retry);
            MakeActionButton(actions, "GoOffline", "PLAY OFFLINE", true, new Vector2(0.46f, 0f), new Vector2(1f, 1f), () =>
            {
                CloseOfflinePrompt();
                OpenOfflineHub();
            });
            _offlinePrompt.gameObject.SetActive(false);
        }

        void BuildOfflineHub()
        {
            _offlineHubBackdrop = Ui.Fill("OfflineHubBackdrop", Root);
            Image block = _offlineHubBackdrop.gameObject.AddComponent<Image>();
            block.color = OfflineSkyTop;
            block.raycastTarget = true;
            RawImage weave = MakeRaw("Weave", _offlineHubBackdrop, MenuArt.Weave());
            weave.color = new Color(PaperWhite.r, PaperWhite.g, PaperWhite.b, 0.04f);
            weave.uvRect = new Rect(0f, 0f, 1280f / WeaveTile, 720f / WeaveTile);
            _offlineHubBackdrop.gameObject.SetActive(false);
            _offlineHubBackdrop.SetSiblingIndex(SafeRoot.GetSiblingIndex());

            _offlineHub = Ui.Fill("OfflineHub", SafeRoot);
            RectTransform header = Ui.Panel("Header", _offlineHub, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBandHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(140f, HitSize));
            Image backHit = back.gameObject.AddComponent<Image>();
            backHit.color = Clear;
            backHit.raycastTarget = true;
            RectTransform backVisual = Ui.Fill("Visual", back);
            backVisual.offsetMin = new Vector2(0f, 20f);
            backVisual.offsetMax = new Vector2(0f, -20f);
            Image backPlate = MakeTacticalGlassPlate("Plate", backVisual, ObsidianGlass, GlassHairline, PaperWhite, cornerCut: 4f, accentW: 3f, topHighlight: 0.14f);
            Text backLabel = Label("Label", backVisual, "BACK", 24, PaperWhite, TextAnchor.MiddleCenter, display: true, ink: false);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one);
            AttachSpringButton(back.gameObject, backPlate, CloseOfflineHub, 0.94f, 1.5f);

            Text hubKicker = Label("Kicker", header, "LOCAL & PRACTICE", 16, TitaniumRim, TextAnchor.LowerLeft, display: true, ink: false);
            Place(hubKicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(Gutter + 140f + 24f, 14f), new Vector2(-(Gutter + 140f + 24f) - Gutter, 20f));
            FitSingleLine(hubKicker, 12, 16);

            Text title = Label("Title", header, "OFFLINE", 44, PaperWhite, TextAnchor.UpperLeft, display: true, ink: false);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(Gutter + 140f + 24f, 12f), new Vector2(-(Gutter + 140f + 24f) - Gutter, 48f));
            FitSingleLine(title, 28, 44);

            RectTransform cards = Ui.Fill("Cards", _offlineHub);
            cards.offsetMin = new Vector2(Gutter, 20f);
            cards.offsetMax = new Vector2(-Gutter, -(TopBandHeight + 8f));

            RectTransform hotspot = MakeOfflineCard(cards, "Hotspot", 0, "HOTSPOT", "Play over phone hotspot", "hotspot", MenuArt.WaterCyan);
            RectTransform hotspotRow = ActionRow(hotspot);
            MakeActionButton(hotspotRow, "Host", "HOST", true, Vector2.zero, new Vector2(0.48f, 1f), () => OpenHotspotFlow(true));
            MakeActionButton(hotspotRow, "Join", "JOIN", false, new Vector2(0.52f, 0f), Vector2.one, () => OpenHotspotFlow(false));

            RectTransform ai = MakeOfflineCard(cards, "PracticeAi", 1, "PRACTICE VS AI", "Fight bots at your pace", "ai", PaperWhite);
            RectTransform aiRow = ActionRow(ai);
            Button diff = MakeActionButton(aiRow, "Difficulty", "MEDIUM", false, Vector2.zero, new Vector2(0.48f, 1f), CycleDifficulty);
            _difficultyLabel = diff.GetComponentInChildren<Text>(true);
            MakeActionButton(aiRow, "Play", "PLAY", true, new Vector2(0.52f, 0f), Vector2.one, () => LaunchMode(GameMode.Practice));

            RectTransform solo = MakeOfflineCard(cards, "PracticeSolo", 2, "PRACTICE SOLO", "Free movement and aim", "solo", SignalVermilion);
            MakeActionButton(ActionRow(solo), "Play", "PLAY", true, Vector2.zero, Vector2.one, () => LaunchMode(GameMode.Training));

            _offlineHub.gameObject.SetActive(false);
        }

        /// <summary>Hotspot, Practice vs AI, Practice solo (only real built modes, docs/SPEC.md §8).</summary>
        const int OfflineCardCount = 3;

        RectTransform MakeOfflineCard(RectTransform cards, string name, int index, string title, string subtitle, string glyph, Color accent)
        {
            const float gap = 18f;
            const float share = 1f / OfflineCardCount;
            RectTransform card = Ui.Panel(name, cards, new Vector2(index * share, 0f), new Vector2((index + 1) * share, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 8f);
            card.offsetMax = new Vector2(index == OfflineCardCount - 1 ? 0f : -gap * 0.5f, 0f);
            MakeTacticalGlassPlate("Plate", card, ObsidianSolid, GlassHairline, accent, cornerCut: 8f, accentW: 3f, topHighlight: 0.14f);

            RectTransform art = Ui.Panel("Art", card, new Vector2(0f, 0.44f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            art.offsetMin = new Vector2(14f, 0f);
            art.offsetMax = new Vector2(-14f, -16f);
            Color bayBg = Color.Lerp(ObsidianSolid, accent, 0.18f);
            MakeTacticalGlassPlate("Colour", art, new Color(bayBg.r, bayBg.g, bayBg.b, 0.90f), new Color(accent.r, accent.g, accent.b, 0.35f), Clear, cornerCut: 6f, accentW: 0f, topHighlight: 0.10f);
            BuildCardGlyph(art, glyph, accent);

            Text label = Label("Title", card, title, 30, PaperWhite, TextAnchor.LowerLeft, display: true, ink: false);
            Stretch(label.rectTransform, new Vector2(0f, 0.31f), new Vector2(1f, 0.42f));
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = new Vector2(-18f, 0f);
            FitSingleLine(label, 14, 30);

            Text sub = Label("Subtitle", card, subtitle, 18, Muted, TextAnchor.UpperLeft, display: false, ink: false);
            Stretch(sub.rectTransform, new Vector2(0f, 0.22f), new Vector2(1f, 0.30f));
            sub.rectTransform.offsetMin = new Vector2(20f, 0f);
            sub.rectTransform.offsetMax = new Vector2(-18f, 0f);
            FitSingleLine(sub, 10, 18);
            return card;
        }

        static void BuildCardGlyph(RectTransform art, string glyph, Color accent)
        {
            Color tint = new Color(PaperWhite.r, PaperWhite.g, PaperWhite.b, 0.94f);
            switch (glyph)
            {
                case "hotspot":
                    for (int i = 0; i < 3; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, new Color(tint.r, tint.g, tint.b, 0.90f - 0.24f * i));
                        ring.sprite = KomiksUi.RingSprite(256, 12);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (54f + i * 48f));
                    }
                    Image dot = Ui.MakeImage("Dot", art, accent, round: true);
                    Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
                    break;
                case "ai":
                    Text vs = Label("Vs", art, "VS", 82, tint, TextAnchor.MiddleCenter, display: true, ink: false);
                    Stretch(vs.rectTransform, Vector2.zero, Vector2.one);
                    break;
                default:
                    for (int i = 0; i < 2; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, new Color(tint.r, tint.g, tint.b, 0.88f - 0.24f * i));
                        ring.sprite = KomiksUi.RingSprite(256, 14);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (132f - i * 54f));
                    }
                    Image core = Ui.MakeImage("Core", art, accent, round: true);
                    Place(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
                    break;
            }
        }

        static RectTransform ActionRow(RectTransform card)
        {
            return Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-36f, HitSize));
        }

        Button MakeActionButton(RectTransform row, string name, string title, bool primary, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            RectTransform rect = Ui.Panel(name, row, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            RectTransform visual = Ui.Fill("Visual", rect);
            visual.offsetMin = new Vector2(0f, 16f);
            visual.offsetMax = new Vector2(0f, -16f);

            Image plate = primary
                ? MakeMetallicCtaPlate("Plate", visual, SignalVermilion, SignalVermilionDeep, cornerCut: 8f)
                : MakeTacticalGlassPlate("Plate", visual, ObsidianGlass, GlassHairline, Clear, cornerCut: 6f, accentW: 0f, topHighlight: 0.14f);

            Text label = Label("Label", visual, title, 24, primary ? Color.white : PaperWhite, TextAnchor.MiddleCenter, display: true, ink: false);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(8f, primary ? 2f : 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            FitSingleLine(label, 14, 24);
            return AttachSpringButton(rect.gameObject, plate, onClick, 0.95f, primary ? 3f : 1.5f);
        }

        // ---------------------------------------------------------------- refresh & responsive layout

        public void Refresh()
        {
            if (_disposed || Root == null)
            {
                OnDisabled();
                return;
            }
            AppSession session = AppSession.Current;

            if (!IsInteractive() && Sheets != null && Sheets.IsOpen)
                Sheets.Close();

            if (_playerName != null)
                _playerName.text = string.IsNullOrWhiteSpace(session.PlayerName) ? AppSession.DefaultPlayerName : session.PlayerName.Trim().ToUpperInvariant();

            if (_levelText != null)
            {
                int accountLevel = session.TotalMatches > 0 ? 1 + (session.TotalMatches / 2) : 9;
                _levelText.text = accountLevel.ToString(CultureInfo.InvariantCulture);
            }

            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (Sheets != null)
                Sheets.SyncFromSession();

            bool useCustomPhoto = Sheets != null && Sheets.HasCustomIdPhoto && Sheets.CurrentIdTexture != null;
            if (_avatarPhotoRaw != null)
            {
                _avatarPhotoRaw.texture = useCustomPhoto ? Sheets.CurrentIdTexture : null;
                _avatarPhotoRaw.uvRect = useCustomPhoto ? Sheets.CurrentIdUvRect : new Rect(0f, 0f, 1f, 1f);
                _avatarPhotoRaw.enabled = useCustomPhoto;
            }
            if (_avatarBustImage != null)
            {
                if (useCustomPhoto)
                {
                    _avatarBustImage.enabled = false;
                }
                else
                {
                    Sprite bust = KalyeahResources.KidBustSprite(kidId);
                    _avatarBustImage.sprite = bust;
                    _avatarBustImage.enabled = bust != null;
                    if (bust != null && bust.rect.width > 1f)
                    {
                        // Transparent 3:4 full-body/bust renders zoom cleanly onto head+shoulders; square fallback faces fill 1:1
                        bool isPortraitBust = (bust.rect.height / bust.rect.width) > 1.15f;
                        _avatarBustImage.rectTransform.sizeDelta = isPortraitBust ? new Vector2(96f, 128f) : new Vector2(54f, 54f);
                        _avatarBustImage.rectTransform.anchoredPosition = isPortraitBust ? new Vector2(0f, -17f) : Vector2.zero;
                    }
                }
            }

            _stage.SetKid(kidId);
            if (_heroImage != null) _heroImage.texture = _stage.Texture;
            _stage.RenderNow();
            RefreshLayout();

            if (_difficultyLabel != null)
                _difficultyLabel.text = SettingsStore.NormalizeDifficulty(session.BotDifficulty).ToUpperInvariant();

            for (int i = 0; i < _ownedMaterials.Count; i++)
                UiShaders.SyncReduceMotion(_ownedMaterials[i]);
        }

        /// <summary>The role's icon: no role colours (docs/SPEC.md §10), only the shape tells them apart.</summary>
        public static MenuArt.GlyphKind RoleGlyph(string role)
        {
            switch (role)
            {
                case "STRIKER": return MenuArt.GlyphKind.Striker;
                case "DEFENDER": return MenuArt.GlyphKind.Defender;
                case "SEEKER": return MenuArt.GlyphKind.Seeker;
                default: return MenuArt.GlyphKind.Controller;
            }
        }

        /// <summary>Every kid's description starts "Role from Purok." (Data/characters__*.json); returns the role in capitals.</summary>
        public static string ParseRole(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";
            int from = description.IndexOf(" from ", StringComparison.Ordinal);
            return from > 0 ? description.Substring(0, from).Trim().ToUpperInvariant() : "";
        }

        /// <summary>
        /// Lays out the shared L3/L4 top bar, hub columns (L5 mission stack, L6 nav rail, open center hero column, L7/L8 queue cluster),
        /// and full-bleed 16:9 Manila street stage across 1280x720 (16:9), 844x390 (notched 19.5:9), 934x400 (21:9), and 1024x768 (4:3).
        /// </summary>
        public void RefreshLayout()
        {
            if (_disposed || Root == null)
            {
                OnDisabled();
                return;
            }
            if (_topBar == null || _hubBody == null) return;

            Rect safe = SafeRoot.rect;
            float width = safe.width;

            _hubBody.offsetMin = new Vector2(Gutter, 16f);
            _hubBody.offsetMax = new Vector2(-Gutter, -(TopBandHeight + 4f));

            float bodyWidth = Mathf.Max(0f, width - 2f * Gutter);
            float leftWidth = Mathf.Clamp(bodyWidth * 0.275f, 268f, 336f);
            float rightWidth = Mathf.Clamp(bodyWidth * 0.295f, 280f, 340f);
            float leftSpan = Mathf.Max(leftWidth, BottomNavWidth);

            if (_leftColumn != null)
                _leftColumn.sizeDelta = new Vector2(leftWidth, 3f * CardHitHeight + 2f * CardGap);
            if (_bottomNav != null)
            {
                _bottomNav.anchoredPosition = Vector2.zero;
                _bottomNav.sizeDelta = new Vector2(BottomNavWidth, HitSize);
            }
            if (_modeColumn != null)
                _modeColumn.sizeDelta = new Vector2(rightWidth, 0f);

            float heroLeft = leftSpan + ColumnGap;
            float heroRight = bodyWidth - rightWidth - ColumnGap;
            float zoneWidth = Mathf.Max(0f, heroRight - heroLeft);
            if (_heroZone != null)
            {
                _heroZone.anchoredPosition = new Vector2(heroLeft, 0f);
                _heroZone.sizeDelta = new Vector2(zoneWidth, 0f);
            }

            if (_heroFitter != null)
            {
                _heroFitter.SetLayoutHorizontal();
                _heroFitter.SetLayoutVertical();
            }
        }

        // ---------------------------------------------------------------- flow

        /// <summary>The modes Home's PLAY starts today (docs/SPEC.md §4); Ranked and private rooms join when they are built.</summary>
        enum HomeMode { Blitz, Quick }

        HomeMode _homeMode;
        Text _modeLabel;

        /// <summary>Switches between Blitz and Quick.</summary>
        public Button ModeButton { get; private set; }

        /// <summary>The mode's line on the pill: how it feels, never team size, scoring or map rules (owner rule).</summary>
        public string ModeLine => _homeMode == HomeMode.Blitz ? "Short rounds, fast wins" : "The full match";

        void CycleHomeMode()
        {
            TapFeedback();
            _homeMode = _homeMode == HomeMode.Blitz ? HomeMode.Quick : HomeMode.Blitz;
            AppSession.Current.FirstTo = _homeMode == HomeMode.Blitz ? 3 : 5;
            ShowHomeMode();
        }

        void ShowHomeMode()
        {
            _homeMode = AppSession.Current.FirstTo >= 5 ? HomeMode.Quick : HomeMode.Blitz;
            if (_modeLabel != null)
            {
                string title = _homeMode == HomeMode.Blitz ? "BLITZ" : "QUICK";
                _modeLabel.text = title + " — " + ModeLine;
            }
        }

        /// <summary>
        /// The check before home (docs/SPEC.md §4): with no server set or no network, the offline prompt opens straight away
        /// (RETRY or PLAY OFFLINE). Returns whether the player is online.
        /// </summary>
        public bool CheckConnectionAtLaunch()
        {
            bool online = !string.IsNullOrWhiteSpace(AppSession.Current.ServerUrl)
                && Application.internetReachability != NetworkReachability.NotReachable;
            if (!online) ShowOfflinePrompt();
            return online;
        }

        void OpenHistory()
        {
            Sheets?.Close();
            Navigate(MenuStack.HistoryId);
        }

        void PressPlay()
        {
            CloseOfflinePrompt();
            AppSession session = AppSession.Current;
            if (string.IsNullOrWhiteSpace(session.ServerUrl))
            {
                ShowOfflinePrompt();
                _promptFromPlay = true;
                return;
            }
            session.Mode = GameMode.Online;
            session.Intent = LobbyIntent.Create;
            MenuStack.SavePersistedState(session, SettingsStore.Current);
            Navigate(MenuStack.LobbyId);
        }

        bool _promptFromPlay;

        /// <summary>RETRY: from PLAY it tries PLAY again; from the launch check it checks the connection again.</summary>
        void Retry()
        {
            CloseOfflinePrompt();
            if (_promptFromPlay) PressPlay();
            else CheckConnectionAtLaunch();
        }

        /// <summary>"You're offline": RETRY or PLAY OFFLINE (opens the offline hub).</summary>
        public void ShowOfflinePrompt()
        {
            if (_offlinePrompt == null) return;
            _promptFromPlay = false;
            Sheets?.Close();
            _offlinePrompt.SetAsLastSibling();
            _offlinePrompt.gameObject.SetActive(true);
        }

        public void CloseOfflinePrompt()
        {
            if (_offlinePrompt != null) _offlinePrompt.gameObject.SetActive(false);
        }

        public void OpenOfflineHub()
        {
            if (_offlineHub == null) return;
            TapFeedback();
            _offlineHubBackdrop.gameObject.SetActive(true);
            _offlineHub.gameObject.SetActive(true);
            _topBar.gameObject.SetActive(false);
            _hubBody.gameObject.SetActive(false);
            if (_heroImage != null) _heroImage.enabled = false;
            Refresh();
        }

        public void CloseOfflineHub()
        {
            if (_offlineHub == null) return;
            _offlineHub.gameObject.SetActive(false);
            _offlineHubBackdrop.gameObject.SetActive(false);
            _topBar.gameObject.SetActive(true);
            _hubBody.gameObject.SetActive(true);
            if (_heroImage != null) _heroImage.enabled = true;
            RefreshLayout();
        }

        void CycleDifficulty()
        {
            string current = SettingsStore.NormalizeDifficulty(AppSession.Current.BotDifficulty);
            int index = Array.IndexOf(Difficulties, current);
            string next = Difficulties[(index + 1 + Difficulties.Length) % Difficulties.Length];
            SettingsStore.Current.BotDifficulty = next;
            AppSession.Current.BotDifficulty = next;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            TapFeedback();
            Refresh();
        }

        void Navigate(string screenId)
        {
            TapFeedback();
            _onNavigate?.Invoke(screenId);
        }

        void LaunchMode(GameMode mode)
        {
            AppSession.Current.Mode = mode;
            if (mode == GameMode.Practice)
                AppSession.Current.BotDifficulty = SettingsStore.NormalizeDifficulty(AppSession.Current.BotDifficulty);
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Haptics.LightTap();
            _audio?.PlayUiTap("ready");
            _onLaunchMatch?.Invoke(mode);
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

        // ---------------------------------------------------------------- Option A1 surface & control builders

        static Image MakeTacticalGlassPlate(string name, Transform parent, Color fill, Color border, Color leftAccent, float cornerCut, float accentW, float topHighlight)
        {
            Image img = Ui.MakeImage(name, parent, Color.white);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one);
            Material mat = UiShaders.CreateTacticalGlass(fill, border, leftAccent, cornerCut, accentW, topHighlight);
            if (mat != null && mat != Graphic.defaultGraphicMaterial && mat != Canvas.GetDefaultCanvasMaterial())
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.color = fill;
            }
            return img;
        }

        Image MakeMetallicCtaPlate(string name, Transform parent, Color top, Color bottom, float cornerCut)
        {
            Image img = Ui.MakeImage(name, parent, Color.white);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one);
            Material mat = UiShaders.CreateMetallicCta(top, bottom, cornerCut);
            if (mat != null && mat != Graphic.defaultGraphicMaterial && mat != Canvas.GetDefaultCanvasMaterial())
            {
                _ownedMaterials.Add(mat);
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.color = top;
            }
            return img;
        }

        static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, bool display = false, bool ink = false, float shadowAlpha = 0.85f)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, display ? KalyeahResources.DisplayFont() : null);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null)
            {
                bool allowOutline = ink && Ui.Luminance(color) >= 0.25f;
                outline.enabled = allowOutline;
                if (allowOutline)
                {
                    outline.effectColor = new Color(SoftShadowInk.r, SoftShadowInk.g, SoftShadowInk.b, shadowAlpha);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }
            return label;
        }

        /// <summary>Constrains a single-line label strictly within its RectTransform width so glyphs never spill onto adjacent icons.</summary>
        static void FitSingleLine(Text label, int minSize, int maxSize)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minSize;
            label.resizeTextMaxSize = maxSize;
        }

        static RawImage MakeRaw(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, Vector2.zero, Vector2.one);
            return raw;
        }

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Button AttachSpringButton(GameObject host, Graphic targetGraphic, Action onClick, float pressedScale = 0.95f, float lipDipPx = 1.5f)
        {
            Button btn = host.GetComponent<Button>() ?? host.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = PressTint;
            btn.colors = cb;
            UiShaders.AttachPressSpring(btn, pressedScale, lipDipPx);
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }
    }
}
