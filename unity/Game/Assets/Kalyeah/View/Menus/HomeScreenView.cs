using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The home lobby (1280x720 reference, SafeArea-pinned, every control's hit area &gt;= 48 dp): the selected kid as a full-height
    /// 3D hero on the left, a thin top bar (profile and rank, text tabs, settings), the mode card and one dominant PLAY on the
    /// right (Wild Rift / Mobile Legends lobby layout, flat sharp Valorant-like styling: ink navy, off-white, one red accent).
    /// PLAY is online first: it opens the online room; when the server can't be reached, <see cref="ShowOfflinePrompt"/> offers
    /// the offline hub (Hotspot, Practice vs AI, Practice solo, Explore Barangay).
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color Ink = new Color(0.059f, 0.098f, 0.137f, 1f);
        static readonly Color InkPanel = new Color(0.086f, 0.129f, 0.173f, 0.94f);
        static readonly Color Paper = new Color(0.925f, 0.910f, 0.882f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.66f, 0.70f, 1f);
        static readonly Color Hairline = new Color(0.925f, 0.910f, 0.882f, 0.18f);
        static readonly Color Red = new Color(1f, 0.275f, 0.333f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.82f, 0.82f, 0.86f, 1f);

        const float TopBarHeight = 96f;
        const float HeroCentreX = 0.36f; // hero and slash centre, as a fraction of the screen width (right of the name plate)
        const float HitSize = 96f;
        const float Gutter = 20f;
        const float NarrowTopBarWidth = 1100f;

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

        Canvas _canvas;
        SafeArea _safeArea;
        bool _subscribed;
        bool _disposed;

        RectTransform _topBar;
        RectTransform _hubBody;
        RectTransform _heroImageRect;
        RectTransform _heroShadowRect;
        RectTransform _modeZone;
        RectTransform _playZone;
        RectTransform _supportTab;
        RectTransform _tabsRow;

        RawImage _heroImage;
        RawImage _avatarImage;
        Text _playerName;
        Text _rankLabel;
        RectTransform _rankFill;
        Image _rankFillImage;
        Text _kidName;

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
        public bool OfflinePromptOpen => _offlinePrompt != null && _offlinePrompt.gameObject.activeSelf;
        /// <summary>The hero stage's texture and state line (capture diagnostics).</summary>
        public Texture HeroTexture => _stage?.Texture;
        public string HeroDebug => _stage?.Describe();
        public void RenderHeroNow() => _stage?.RenderNow();

        public bool OfflineHubOpen =>_offlineHub != null && _offlineHub.gameObject.activeSelf;

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
            _stage = new HeroStage();

            ScreenFitMetrics.EnsureEnhancedTouch();

            Root = Ui.Fill("HomeScreen", parent);
            BuildBackdrop(Root);
            BuildHero(Root);

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

        void Tick(float dt)
        {
            if (_disposed || !Application.isPlaying) return;
            _stage?.Tick(dt);
        }

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
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        // ---------------------------------------------------------------- build

        void BuildBackdrop(RectTransform parent)
        {
            RectTransform bg = Ui.Fill("Backdrop", parent);
            Image ink = bg.gameObject.AddComponent<Image>();
            ink.color = Ink;
            ink.raycastTarget = false;

            Texture2D art = KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (art != null)
            {
                RawImage raw = MakeRaw("Art", bg, art);
                raw.color = new Color(0.55f, 0.58f, 0.66f, 0.42f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 16f / 9f;
            }

            // darken toward the right (where the controls sit) and toward the bottom
            RawImage sideShade = MakeRaw("SideShade", bg, Ramp(true));
            sideShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.92f);
            Stretch(sideShade.rectTransform, new Vector2(0.35f, 0f), Vector2.one);
            RawImage floorShade = MakeRaw("FloorShade", bg, Ramp(false));
            floorShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.85f);
            Stretch(floorShade.rectTransform, Vector2.zero, new Vector2(1f, 0.42f));

            // one bold red slash behind the hero
            // one bold red slash behind the hero, clipped under the top bar so it frames the kid, not the tabs
            RectTransform slashClip = Ui.Fill("SlashClip", bg);
            slashClip.offsetMax = new Vector2(0f, -(TopBarHeight + 8f));
            slashClip.gameObject.AddComponent<RectMask2D>();
            Image slash = Ui.MakeImage("HeroSlash", slashClip, new Color(Red.r, Red.g, Red.b, 0.85f));
            RectTransform sr = slash.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(HeroCentreX, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(130f, 1400f);
            sr.localRotation = Quaternion.Euler(0f, 0f, -16f);
            Image slashEdge = Ui.MakeImage("HeroSlashEdge", slashClip, new Color(Paper.r, Paper.g, Paper.b, 0.5f));
            RectTransform er = slashEdge.rectTransform;
            er.anchorMin = er.anchorMax = new Vector2(HeroCentreX, 0.5f);
            er.pivot = new Vector2(0.5f, 0.5f);
            er.anchoredPosition = new Vector2(118f, 0f);
            er.sizeDelta = new Vector2(4f, 1400f);
            er.localRotation = Quaternion.Euler(0f, 0f, -16f);
        }

        void BuildHero(RectTransform parent)
        {
            Image shadow = Ui.MakeImage("HeroShadow", parent, new Color(0f, 0f, 0f, 0.55f), round: true);
            _heroShadowRect = shadow.rectTransform;
            _heroShadowRect.anchorMin = _heroShadowRect.anchorMax = new Vector2(0f, 0f);
            _heroShadowRect.pivot = new Vector2(0.5f, 0.5f);
            _heroShadowRect.sizeDelta = new Vector2(320f, 56f);

            _heroImage = MakeRaw("Hero", parent, _stage.Texture);
            _heroImageRect = _heroImage.rectTransform;
            _heroImageRect.anchorMin = _heroImageRect.anchorMax = new Vector2(0f, 0f);
            _heroImageRect.pivot = new Vector2(0.5f, 0f);
        }

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));

            Image rule = Ui.MakeImage("Rule", _topBar, Hairline);
            Stretch(rule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
            rule.rectTransform.sizeDelta = new Vector2(-2f * Gutter, 2f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            // profile: avatar, name, rank line + LP bar (one button: opens the profile sheet)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(300f, HitSize));
            Image profileHit = profile.gameObject.AddComponent<Image>();
            profileHit.color = Clear;
            ProfileButton = AttachButton(profile.gameObject, profileHit, () => { TapFeedback(); Sheets?.OpenProfile(); });

            Image avatarFrame = Ui.MakeImage("AvatarFrame", profile, Red);
            Place(avatarFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(60f, 60f));
            avatarFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
            Image avatarWell = Ui.MakeImage("AvatarWell", avatarFrame.rectTransform, Ink);
            Stretch(avatarWell.rectTransform, Vector2.zero, Vector2.one);
            avatarWell.rectTransform.offsetMin = new Vector2(2f, 2f);
            avatarWell.rectTransform.offsetMax = new Vector2(-2f, -2f);
            _avatarImage = MakeRaw("Avatar", avatarWell.rectTransform, null);

            _playerName = Label("Name", profile, "", 26, Paper, TextAnchor.LowerLeft, display: true);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(76f, 2f), new Vector2(-76f, 32f));
            _playerName.rectTransform.pivot = new Vector2(0f, 0f);
            _playerName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _playerName.resizeTextForBestFit = true;
            _playerName.resizeTextMinSize = 16;
            _playerName.resizeTextMaxSize = 26;

            _rankLabel = Label("Rank", profile, "", 15, Muted, TextAnchor.UpperLeft);
            Place(_rankLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(76f, -2f), new Vector2(-76f, 22f));
            _rankLabel.rectTransform.pivot = new Vector2(0f, 1f);

            Image rankTrack = Ui.MakeImage("RankTrack", profile, Hairline);
            Place(rankTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, -30f), new Vector2(150f, 4f));
            rankTrack.rectTransform.pivot = new Vector2(0f, 0.5f);
            _rankFillImage = Ui.MakeImage("RankFill", rankTrack.rectTransform, Red);
            _rankFill = _rankFillImage.rectTransform;
            Stretch(_rankFill, Vector2.zero, new Vector2(0.2f, 1f));

            // text tabs (Valorant-style nav): kids, codex, history, events
            _tabsRow = Ui.Panel("Tabs", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + 300f + 24f, 0f), new Vector2(4f * 120f, HitSize));
            MakeTab(_tabsRow, "Tab_Kids", "KIDS", 0, () => Navigate(MenuStack.PickId));
            MakeTab(_tabsRow, "Tab_Codex", "CODEX", 1, () => Navigate(MenuStack.CodexId));
            MakeTab(_tabsRow, "Tab_History", "HISTORY", 2, () => Navigate(MenuStack.HistoryId));
            MakeTab(_tabsRow, "Tab_Events", "EVENTS", 3, () => { TapFeedback(); Sheets?.OpenGameOfMonth(); });

            // right: support (donate) + settings gear
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(HitSize, HitSize));
            Image settingsHit = settings.gameObject.AddComponent<Image>();
            settingsHit.color = Clear;
            Texture2D gear = KalyeahResources.Texture("ui/hud_icons/gear");
            if (gear != null)
            {
                RawImage gearIcon = MakeRaw("Gear", settings, gear);
                Place(gearIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                gearIcon.color = Paper;
            }
            else
            {
                Text gearText = Label("Gear", settings, "SET", 18, Paper, TextAnchor.MiddleCenter, display: true);
                Stretch(gearText.rectTransform, Vector2.zero, Vector2.one);
            }
            AttachButton(settings.gameObject, settingsHit, () => Navigate(MenuStack.SettingsId));

            _supportTab = Ui.Panel("Support", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter - HitSize - 8f, 0f), new Vector2(132f, HitSize));
            Image supportHit = _supportTab.gameObject.AddComponent<Image>();
            supportHit.color = Clear;
            Text supportText = Label("Label", _supportTab, "SUPPORT", 20, Muted, TextAnchor.MiddleCenter, display: true);
            Stretch(supportText.rectTransform, Vector2.zero, Vector2.one);
            AttachButton(_supportTab.gameObject, supportHit, () => { TapFeedback(); Sheets?.OpenDonate(); });
        }

        void MakeTab(RectTransform row, string name, string title, int index, Action onClick)
        {
            RectTransform tab = Ui.Panel(name, row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(index * 120f, 0f), new Vector2(112f, HitSize));
            Image hit = tab.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Text label = Label("Label", tab, title, 22, Paper, TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            AttachButton(tab.gameObject, hit, onClick);
        }

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // 0. hero zone: the kid's name plate (tap to change kid)
            RectTransform heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0.46f, 1f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            // name stacked over CHANGE, bottom-left, clear of the hero's feet
            RectTransform plate = Ui.Panel("KidPlate", heroZone, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300f, 124f));
            Image plateHit = plate.gameObject.AddComponent<Image>();
            plateHit.color = Clear;
            Image bar = Ui.MakeImage("Bar", plate, Red);
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 70f));
            bar.rectTransform.pivot = new Vector2(0f, 1f);
            _kidName = Label("KidName", plate, "", 60, Paper, TextAnchor.MiddleLeft, display: true);
            Place(_kidName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(13f, 0f), new Vector2(-26f, 70f));
            _kidName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _kidName.resizeTextForBestFit = true;
            _kidName.resizeTextMinSize = 32;
            _kidName.resizeTextMaxSize = 60;
            Text change = Label("Change", plate, "CHANGE", 16, Paper, TextAnchor.MiddleCenter, display: true);
            Place(change.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26f, 8f), new Vector2(96f, 34f));
            change.rectTransform.pivot = Vector2.zero;
            Frame(change.rectTransform, Hairline, 2f);
            KidButton = AttachButton(plate.gameObject, plateHit, () => Navigate(MenuStack.PickId));

            // 1. mode zone: the one live mode
            _modeZone = Ui.Panel("ModeZone", _hubBody, Vector2.one, Vector2.one, Vector2.one, Vector2.zero, new Vector2(420f, 150f));
            Image modeBg = _modeZone.gameObject.AddComponent<Image>();
            modeBg.color = InkPanel;
            modeBg.raycastTarget = false;
            RectTransform modeArt = Ui.Fill("Art", _modeZone);
            modeArt.gameObject.AddComponent<RectMask2D>();
            Texture2D modeTex = KalyeahResources.Texture("ui/games/agawan_base") ?? KalyeahResources.Texture("ui/mode_pictures/practice");
            if (modeTex != null)
            {
                RawImage art = MakeRaw("Picture", modeArt, modeTex);
                art.color = new Color(0.86f, 0.86f, 0.9f, 1f);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = modeTex.height > 0 ? (float)modeTex.width / modeTex.height : 2f;
            }
            RawImage modeShade = MakeRaw("Shade", _modeZone, Ramp(true));
            modeShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.95f);
            modeShade.uvRect = new Rect(1f, 0f, -1f, 1f);
            Stretch(modeShade.rectTransform, Vector2.zero, new Vector2(0.8f, 1f));
            Image modeAccent = Ui.MakeImage("Accent", _modeZone, Red);
            Stretch(modeAccent.rectTransform, Vector2.zero, new Vector2(0f, 1f));
            modeAccent.rectTransform.sizeDelta = new Vector2(6f, 0f);
            modeAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text modeName = Label("ModeName", _modeZone, "AGAWANG BASE", 38, Paper, TextAnchor.LowerLeft, display: true);
            Stretch(modeName.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
            modeName.rectTransform.offsetMin = new Vector2(26f, 0f);
            modeName.rectTransform.offsetMax = new Vector2(-16f, -12f);
            modeName.resizeTextForBestFit = true;
            modeName.resizeTextMinSize = 24;
            modeName.resizeTextMaxSize = 38;
            Image sizeChip = Ui.MakeImage("SizeChip", _modeZone, Paper);
            Place(sizeChip.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26f, 18f), new Vector2(64f, 32f));
            sizeChip.rectTransform.pivot = Vector2.zero;
            Text modeSize = Label("ModeSize", sizeChip.rectTransform, "5V5", 18, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(modeSize.rectTransform, Vector2.zero, Vector2.one);

            // 2. play zone: the one big button
            _playZone = Ui.Panel("PlayZone", _hubBody, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(420f, 120f));
            Image playFill = _playZone.gameObject.AddComponent<Image>();
            playFill.color = Red;
            Image playEdge = Ui.MakeImage("Edge", _playZone, new Color(Paper.r, Paper.g, Paper.b, 0.9f));
            Stretch(playEdge.rectTransform, new Vector2(0f, 1f), Vector2.one);
            playEdge.rectTransform.pivot = new Vector2(0.5f, 1f);
            playEdge.rectTransform.sizeDelta = new Vector2(0f, 4f);
            Text playLabel = Label("Label", _playZone, "PLAY", 54, Paper, TextAnchor.MiddleCenter, display: true);
            Stretch(playLabel.rectTransform, Vector2.zero, Vector2.one);
            PlayButton = AttachButton(_playZone.gameObject, playFill, PressPlay);
        }

        void BuildOfflinePrompt()
        {
            _offlinePrompt = Ui.Fill("OfflinePrompt", Root);
            Image scrim = _offlinePrompt.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.04f, 0.06f, 0.82f);
            scrim.raycastTarget = true;

            RectTransform card = Ui.Panel("Card", _offlinePrompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 300f));
            Image cardBg = card.gameObject.AddComponent<Image>();
            cardBg.color = Ink;
            Image top = Ui.MakeImage("Accent", card, Red);
            Stretch(top.rectTransform, new Vector2(0f, 1f), Vector2.one);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.sizeDelta = new Vector2(0f, 6f);
            Frame(card, Hairline, 2f);

            Text title = Label("Title", card, "OFFLINE", 44, Paper, TextAnchor.UpperLeft, display: true);
            Stretch(title.rectTransform, new Vector2(0f, 1f), Vector2.one);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-64f, 56f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            Text body = Label("Body", card, "Can't reach the game server.", 22, Muted, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0f, 1f), Vector2.one);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.sizeDelta = new Vector2(-64f, 32f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -92f);

            RectTransform actions = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(-64f, HitSize));
            MakeActionButton(actions, "Retry", "RETRY", false, new Vector2(0f, 0f), new Vector2(0.42f, 1f), PressPlay);
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
            Image bg = _offlineHubBackdrop.gameObject.AddComponent<Image>();
            bg.color = new Color(Ink.r, Ink.g, Ink.b, 0.97f);
            bg.raycastTarget = true;
            _offlineHubBackdrop.gameObject.SetActive(false);
            _offlineHubBackdrop.SetSiblingIndex(SafeRoot.GetSiblingIndex()); // behind the hub, over the hero and backdrop

            _offlineHub = Ui.Fill("OfflineHub", SafeRoot);
            RectTransform header = Ui.Panel("Header", _offlineHub, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(132f, HitSize));
            Image backHit = back.gameObject.AddComponent<Image>();
            backHit.color = Clear;
            Text backLabel = Label("Label", back, "BACK", 22, Paper, TextAnchor.MiddleCenter, display: true);
            Place(backLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 52f));
            Frame(backLabel.rectTransform, Hairline, 2f);
            AttachButton(back.gameObject, backHit, CloseOfflineHub);
            Text title = Label("Title", header, "OFFLINE", 44, Paper, TextAnchor.MiddleLeft, display: true);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 132f + 24f, 0f), new Vector2(-(Gutter + 132f + 24f) - Gutter, 60f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            RectTransform cards = Ui.Fill("Cards", _offlineHub);
            cards.offsetMin = new Vector2(Gutter, 16f);
            cards.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            RectTransform hotspot = MakeOfflineCard(cards, "Hotspot", 0, "HOTSPOT", "ui/dioramas/hotspot", main: true);
            RectTransform hotspotRow = ActionRow(hotspot);
            MakeActionButton(hotspotRow, "Host", "HOST", true, Vector2.zero, new Vector2(0.49f, 1f), () => OpenHotspotFlow(true));
            MakeActionButton(hotspotRow, "Join", "JOIN", false, new Vector2(0.51f, 0f), Vector2.one, () => OpenHotspotFlow(false));

            RectTransform ai = MakeOfflineCard(cards, "PracticeAi", 1, "PRACTICE VS AI", "ui/games/agawan_base", main: false);
            RectTransform aiRow = ActionRow(ai);
            Button diff = MakeActionButton(aiRow, "Difficulty", "MEDIUM", false, Vector2.zero, new Vector2(0.49f, 1f), CycleDifficulty);
            _difficultyLabel = diff.GetComponentInChildren<Text>(true);
            MakeActionButton(aiRow, "Play", "PLAY", true, new Vector2(0.51f, 0f), Vector2.one, () => LaunchMode(GameMode.Practice));

            RectTransform solo = MakeOfflineCard(cards, "PracticeSolo", 2, "PRACTICE SOLO", "ui/dioramas/practice", main: false);
            MakeActionButton(ActionRow(solo), "Play", "PLAY", true, Vector2.zero, Vector2.one, () => LaunchMode(GameMode.Training));

            RectTransform explore = MakeOfflineCard(cards, "Explore", 3, "EXPLORE BARANGAY", "ui/main_bg", main: false);
            Text soon = Label("Soon", ActionRow(explore), "SOON", 22, Muted, TextAnchor.MiddleCenter, display: true);
            Stretch(soon.rectTransform, Vector2.zero, Vector2.one);
            Frame(soon.rectTransform, Hairline, 2f);

            _offlineHub.gameObject.SetActive(false);
        }

        RectTransform MakeOfflineCard(RectTransform cards, string name, int index, string title, string artPath, bool main)
        {
            const float gap = 14f;
            RectTransform card = Ui.Panel(name, cards, new Vector2(index * 0.25f, 0f), new Vector2((index + 1) * 0.25f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 0f);
            card.offsetMax = new Vector2(index == 3 ? 0f : -gap * 0.5f, 0f);
            Image bg = card.gameObject.AddComponent<Image>();
            bg.color = InkPanel;
            bg.raycastTarget = false;

            RectTransform artArea = Ui.Panel("Art", card, new Vector2(0f, 0.42f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            artArea.gameObject.AddComponent<RectMask2D>();
            Texture2D tex = KalyeahResources.Texture(artPath);
            if (tex != null)
            {
                RawImage art = MakeRaw("Picture", artArea, tex);
                art.color = new Color(0.8f, 0.8f, 0.86f, 1f);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            }
            RawImage shade = MakeRaw("Shade", artArea, Ramp(false));
            shade.color = new Color(InkPanel.r, InkPanel.g, InkPanel.b, 1f);
            Stretch(shade.rectTransform, Vector2.zero, new Vector2(1f, 0.6f));

            Image accent = Ui.MakeImage("Accent", card, main ? Red : Hairline);
            Stretch(accent.rectTransform, new Vector2(0f, 1f), Vector2.one);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(0f, main ? 6f : 2f);

            Text label = Label("Title", card, title, 30, Paper, TextAnchor.LowerLeft, display: true);
            Stretch(label.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.44f));
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 30;
            return card;
        }

        static RectTransform ActionRow(RectTransform card)
        {
            RectTransform row = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-16f, HitSize));
            return row;
        }

        Button MakeActionButton(RectTransform row, string name, string title, bool primary, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            RectTransform rect = Ui.Panel(name, row, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.color = primary ? Red : new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            if (!primary) Frame(rect, new Color(Paper.r, Paper.g, Paper.b, 0.55f), 2f);
            Text label = Label("Label", rect, title, 24, Paper, TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 24;
            return AttachButton(rect.gameObject, fill, onClick);
        }

        // ---------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (_disposed) return;
            AppSession session = AppSession.Current;

            if (!IsInteractive() && Sheets != null && Sheets.IsOpen)
                Sheets.Close();

            if (_playerName != null)
                _playerName.text = string.IsNullOrWhiteSpace(session.PlayerName) ? "PLAYER" : session.PlayerName.Trim().ToUpperInvariant();

            if (_avatarImage != null && Sheets != null)
            {
                Sheets.SyncFromSession();
                _avatarImage.texture = Sheets.CurrentIdTexture;
                _avatarImage.uvRect = Sheets.CurrentIdUvRect;
                _avatarImage.enabled = Sheets.CurrentIdTexture != null;
            }

            RankTierInfo tier = session.CurrentTier;
            string tierName = string.IsNullOrWhiteSpace(tier.Name) ? "Kahoy" : tier.Name.Trim();
            if (_rankLabel != null)
            {
                int next = session.NextTierLp;
                _rankLabel.text = next > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1} / {2} LP", tierName.ToUpperInvariant(), session.Lp, next)
                    : string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1} LP", tierName.ToUpperInvariant(), session.Lp);
            }
            if (_rankFill != null)
            {
                _rankFill.anchorMax = new Vector2(Mathf.Clamp(session.TierProgress01, 0.04f, 1f), 1f);
                _rankFillImage.color = ParseHexColor(tier.ColorHex, Red);
            }

            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (_kidName != null)
                _kidName.text = (ReadCharacterDisplayName(kidId) ?? Capitalize(kidId)).ToUpperInvariant();
            _stage.SetKid(kidId);
            _stage.RenderNow();
            PlaceHeroShadow();

            if (_difficultyLabel != null)
                _difficultyLabel.text = SettingsStore.NormalizeDifficulty(session.BotDifficulty).ToUpperInvariant();
        }

        /// <summary>Reflows the top bar, hub zones and hero for 4:3 foldables through 21:9 ultra-wides.</summary>
        public void RefreshLayout()
        {
            if (_disposed || _topBar == null || _hubBody == null) return;
            Rect safe = SafeRoot.rect;
            Rect full = Root.rect;
            float width = safe.width;

            _hubBody.offsetMin = new Vector2(Gutter, 16f);
            _hubBody.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            bool roomForSupport = width >= NarrowTopBarWidth;
            if (_supportTab != null) _supportTab.gameObject.SetActive(roomForSupport);
            if (_tabsRow != null)
            {
                // tabs shrink their spacing on narrow screens; each keeps a 96 unit hit area
                float available = width - (Gutter + 300f + 24f) - (Gutter + HitSize + 8f) - (roomForSupport ? 132f + 8f : 0f);
                float step = Mathf.Clamp(available / 4f, HitSize + 4f, 132f);
                for (int i = 0; i < _tabsRow.childCount; i++)
                {
                    var tab = _tabsRow.GetChild(i) as RectTransform;
                    if (tab == null) continue;
                    tab.anchoredPosition = new Vector2(i * step, 0f);
                    tab.sizeDelta = new Vector2(step - 4f, HitSize);
                }
                _tabsRow.sizeDelta = new Vector2(4f * step, HitSize);
            }

            float bodyWidth = Mathf.Max(0f, width - 2f * Gutter);
            float rightWidth = Mathf.Clamp(bodyWidth * 0.36f, 340f, 460f);
            rightWidth = Mathf.Min(rightWidth, bodyWidth * 0.54f - 16f);
            if (_modeZone != null) _modeZone.sizeDelta = new Vector2(rightWidth, 150f);
            if (_playZone != null) _playZone.sizeDelta = new Vector2(rightWidth, 120f);

            // hero: full height, centred on the red slash, right of the name plate
            float heroHeight = full.height * 1.02f;
            float heroWidth = heroHeight * 0.75f;
            if (_heroImageRect != null)
            {
                _heroImageRect.sizeDelta = new Vector2(heroWidth, heroHeight);
                _heroImageRect.anchoredPosition = new Vector2(full.width * HeroCentreX, -full.height * 0.02f);
            }
            PlaceHeroShadow();
        }

        void PlaceHeroShadow()
        {
            if (_heroShadowRect == null || _heroImageRect == null) return;
            Vector2 size = _heroImageRect.sizeDelta;
            Vector2 pos = _heroImageRect.anchoredPosition;
            _heroShadowRect.anchoredPosition = new Vector2(pos.x, pos.y + size.y * _stage.FeetViewportY);
            _heroShadowRect.sizeDelta = new Vector2(size.x * 0.42f, size.x * 0.07f);
        }

        // ---------------------------------------------------------------- flow

        /// <summary>PLAY: online first. With no server configured the offline prompt opens at once.</summary>
        void PressPlay()
        {
            CloseOfflinePrompt();
            AppSession session = AppSession.Current;
            if (string.IsNullOrWhiteSpace(session.ServerUrl))
            {
                ShowOfflinePrompt();
                return;
            }
            session.Mode = GameMode.Online;
            session.Intent = LobbyIntent.Create;
            MenuStack.SavePersistedState(session, SettingsStore.Current);
            Navigate(MenuStack.LobbyId);
        }

        /// <summary>"Can't reach the game server": RETRY or PLAY OFFLINE (opens the offline hub).</summary>
        public void ShowOfflinePrompt()
        {
            if (_offlinePrompt == null) return;
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

        // ---------------------------------------------------------------- small builders

        static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, bool display = false)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, display ? KalyeahResources.DisplayFont() : null);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            return label;
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

        /// <summary>A sharp hairline border (four thin edge images) around <paramref name="rect"/>.</summary>
        static void Frame(RectTransform rect, Color color, float thickness)
        {
            for (int i = 0; i < 4; i++)
            {
                Image edge = Ui.MakeImage("Edge" + i, rect, color);
                RectTransform r = edge.rectTransform;
                bool horizontal = i < 2;
                r.anchorMin = horizontal ? new Vector2(0f, i) : new Vector2(i - 2, 0f);
                r.anchorMax = horizontal ? new Vector2(1f, i) : new Vector2(i - 2, 1f);
                r.pivot = horizontal ? new Vector2(0.5f, i) : new Vector2(i - 2, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
            }
        }

        static Texture2D s_rampH;
        static Texture2D s_rampV;

        /// <summary>A code-drawn alpha ramp (0 to 1) for flat shading washes: horizontal (left to right) or vertical (top to bottom).</summary>
        static Texture2D Ramp(bool horizontal)
        {
            Texture2D cached = horizontal ? s_rampH : s_rampV;
            if (cached != null) return cached;
            const int n = 64;
            var tex = horizontal
                ? new Texture2D(n, 1, TextureFormat.RGBA32, false)
                : new Texture2D(1, n, TextureFormat.RGBA32, false);
            tex.name = horizontal ? "KalyeahRampH" : "KalyeahRampV";
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float a = horizontal ? Mathf.SmoothStep(0f, 1f, t) : Mathf.SmoothStep(0f, 1f, 1f - t);
                var c = new Color(1f, 1f, 1f, a);
                if (horizontal) tex.SetPixel(i, 0, c);
                else tex.SetPixel(0, i, c);
            }
            tex.Apply(false, true);
            if (horizontal) s_rampH = tex; else s_rampV = tex;
            return tex;
        }

        static Button AttachButton(GameObject host, Graphic targetGraphic, Action onClick)
        {
            Button btn = host.GetComponent<Button>() ?? host.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = PressTint;
            btn.colors = cb;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

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
            return s.Length == 1 ? s.ToUpperInvariant() : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
