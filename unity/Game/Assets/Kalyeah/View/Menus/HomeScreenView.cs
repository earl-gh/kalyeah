using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The home lobby, laid out like CODM's main screen (1280x720 reference, SafeArea-pinned, every hit area &gt;= 48 dp): a top bar
    /// (profile and rank, support, settings), a left rail (KIDS, CODEX, HISTORY), the selected kid in 3D on a lit floor with its
    /// name plate, and on the right the mode card (tap to change) over one big gold PLAY with CODM's cut corner.
    /// PLAY is online first: it opens the online room; when the server can't be reached, <see cref="ShowOfflinePrompt"/> offers
    /// the offline hub (Hotspot, Practice vs AI, Practice solo).
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color Ink = new Color(0.059f, 0.098f, 0.137f, 1f);
        static readonly Color InkPanel = new Color(0.086f, 0.129f, 0.173f, 0.94f);
        static readonly Color Paper = new Color(0.925f, 0.910f, 0.882f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.66f, 0.70f, 1f);
        static readonly Color Hairline = new Color(0.925f, 0.910f, 0.882f, 0.18f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.82f, 0.82f, 0.86f, 1f);
        static readonly Color Gold = new Color(1f, 0.757f, 0.149f, 1f); // CODM-style primary: PLAY, edges, highlights

        const float TopBarHeight = 104f;
        const float RailWidth = 150f;
        const float RailTileHeight = 122f;
        const float HitSize = 96f;
        const float Gutter = 20f;
        const float NarrowTopBarWidth = 1000f;

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
        RectTransform _heroGlowRect;
        RectTransform _navRail;
        RectTransform _heroZone;
        RectTransform _modeColumn;
        RawImage _kidsIcon;
        RawImage _modeArt;
        AspectRatioFitter _modeArtFit;
        RectTransform _modeZone;
        RectTransform _playZone;
        RectTransform _supportTab;

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
                raw.color = new Color(0.42f, 0.44f, 0.50f, 0.55f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 16f / 9f;
            }

            // CODM-style lobby light: dark at the edges, a dim top strip under the bar, a lit floor under the kid
            RawImage leftShade = MakeRaw("LeftShade", bg, Ramp(true));
            leftShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.9f);
            leftShade.uvRect = new Rect(1f, 0f, -1f, 1f);
            Stretch(leftShade.rectTransform, Vector2.zero, new Vector2(0.28f, 1f));
            RawImage rightShade = MakeRaw("RightShade", bg, Ramp(true));
            rightShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.94f);
            Stretch(rightShade.rectTransform, new Vector2(0.55f, 0f), Vector2.one);
            RawImage floorShade = MakeRaw("FloorShade", bg, Ramp(false));
            floorShade.color = new Color(Ink.r, Ink.g, Ink.b, 0.9f);
            Stretch(floorShade.rectTransform, Vector2.zero, new Vector2(1f, 0.38f));
            RawImage topShade = MakeRaw("TopShade", bg, Ramp(false));
            topShade.color = new Color(0f, 0f, 0f, 0.7f);
            topShade.uvRect = new Rect(0f, 1f, 1f, -1f);
            Stretch(topShade.rectTransform, new Vector2(0f, 0.8f), Vector2.one);
        }

        void BuildHero(RectTransform parent)
        {
            // a soft gold floor light, then the contact shadow, then the kid
            Image glow = Ui.MakeImage("HeroGlow", parent, new Color(Gold.r, Gold.g, Gold.b, 0.16f), round: true);
            _heroGlowRect = glow.rectTransform;
            _heroGlowRect.anchorMin = _heroGlowRect.anchorMax = Vector2.zero;
            _heroGlowRect.pivot = new Vector2(0.5f, 0.5f);

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

            // profile: avatar, name, rank line + LP bar on a dark angled plate (one button: opens the profile sheet)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(330f, HitSize));
            Image profileHit = profile.gameObject.AddComponent<Image>();
            profileHit.color = new Color(0f, 0f, 0f, 0.45f);
            Image profileEdge = Ui.MakeImage("Edge", profile, Gold);
            Stretch(profileEdge.rectTransform, Vector2.zero, new Vector2(0f, 1f));
            profileEdge.rectTransform.sizeDelta = new Vector2(4f, 0f);
            profileEdge.rectTransform.pivot = new Vector2(0f, 0.5f);
            ProfileButton = AttachButton(profile.gameObject, profileHit, () => { TapFeedback(); Sheets?.OpenProfile(); });

            Image avatarFrame = Ui.MakeImage("AvatarFrame", profile, Gold);
            Place(avatarFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(68f, 68f));
            avatarFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
            Image avatarWell = Ui.MakeImage("AvatarWell", avatarFrame.rectTransform, Ink);
            Stretch(avatarWell.rectTransform, Vector2.zero, Vector2.one);
            avatarWell.rectTransform.offsetMin = new Vector2(2f, 2f);
            avatarWell.rectTransform.offsetMax = new Vector2(-2f, -2f);
            _avatarImage = MakeRaw("Avatar", avatarWell.rectTransform, null);

            _playerName = Label("Name", profile, "", 26, Paper, TextAnchor.LowerLeft, display: true);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(96f, 2f), new Vector2(-108f, 32f));
            _playerName.rectTransform.pivot = new Vector2(0f, 0f);
            _playerName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _playerName.resizeTextForBestFit = true;
            _playerName.resizeTextMinSize = 16;
            _playerName.resizeTextMaxSize = 26;

            _rankLabel = Label("Rank", profile, "", 15, Muted, TextAnchor.UpperLeft);
            Place(_rankLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(96f, -2f), new Vector2(-108f, 22f));
            _rankLabel.rectTransform.pivot = new Vector2(0f, 1f);

            Image rankTrack = Ui.MakeImage("RankTrack", profile, Hairline);
            Place(rankTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, -30f), new Vector2(170f, 4f));
            rankTrack.rectTransform.pivot = new Vector2(0f, 0.5f);
            _rankFillImage = Ui.MakeImage("RankFill", rankTrack.rectTransform, Gold);
            _rankFill = _rankFillImage.rectTransform;
            Stretch(_rankFill, Vector2.zero, new Vector2(0.2f, 1f));

            // right: settings gear tile, then support
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(HitSize, HitSize));
            Image settingsHit = settings.gameObject.AddComponent<Image>();
            settingsHit.color = new Color(0f, 0f, 0f, 0.45f);
            Texture2D gear = KalyeahResources.Texture("ui/hud_icons/gear");
            if (gear != null)
            {
                RawImage gearIcon = MakeRaw("Gear", settings, gear);
                Place(gearIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
                gearIcon.color = Paper;
            }
            else
            {
                Text gearText = Label("Gear", settings, "SET", 18, Paper, TextAnchor.MiddleCenter, display: true);
                Stretch(gearText.rectTransform, Vector2.zero, Vector2.one);
            }
            AttachButton(settings.gameObject, settingsHit, () => Navigate(MenuStack.SettingsId));

            _supportTab = Ui.Panel("Support", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter - HitSize - 10f, 0f), new Vector2(150f, HitSize));
            Image supportHit = _supportTab.gameObject.AddComponent<Image>();
            supportHit.color = new Color(0f, 0f, 0f, 0.45f);
            Text supportText = Label("Label", _supportTab, "SUPPORT", 20, Paper, TextAnchor.MiddleCenter, display: true);
            Stretch(supportText.rectTransform, Vector2.zero, Vector2.one);
            AttachButton(_supportTab.gameObject, supportHit, () => { TapFeedback(); Sheets?.OpenDonate(); });
        }

        /// <summary>One CODM-style rail tile: a dark plate, a picture, a label underneath, a gold edge on the left.</summary>
        RawImage MakeRailTile(RectTransform rail, string name, string title, Texture2D icon, Action onClick)
        {
            RectTransform tile = Ui.Panel(name, rail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RailTileHeight));
            Image plate = tile.gameObject.AddComponent<Image>();
            plate.color = new Color(0f, 0f, 0f, 0.55f);
            Image edge = Ui.MakeImage("Edge", tile, Gold);
            Stretch(edge.rectTransform, Vector2.zero, new Vector2(0f, 1f));
            edge.rectTransform.sizeDelta = new Vector2(4f, 0f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);

            RawImage picture = MakeRaw("Icon", tile, icon);
            Place(picture.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(76f, 76f));
            picture.rectTransform.pivot = new Vector2(0.5f, 1f);
            picture.enabled = icon != null;

            Text label = Label("Label", tile, title, 20, Paper, TextAnchor.LowerCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            AttachButton(tile.gameObject, plate, onClick);
            return picture;
        }

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // 0. left rail (CODM's side menu): KIDS (the selected kid's face), CODEX, HISTORY
            _navRail = Ui.Panel("NavRail", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(RailWidth, 0f));
            _kidsIcon = MakeRailTile(_navRail, "Tab_Kids", "KIDS", null, () => Navigate(MenuStack.PickId));
            MakeRailTile(_navRail, "Tab_Codex", "CODEX", KalyeahResources.Texture("ui/nav/nav_cards"), () => Navigate(MenuStack.CodexId));
            MakeRailTile(_navRail, "Tab_History", "HISTORY", KalyeahResources.Texture("ui/nav/nav_history"), () => Navigate(MenuStack.HistoryId));

            // 1. hero zone: the kid's name plate at the kid's feet (tap to change kid)
            _heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform plate = Ui.Panel("KidPlate", _heroZone, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, 112f));
            Image plateHit = plate.gameObject.AddComponent<Image>();
            plateHit.color = Clear;
            _kidName = Label("KidName", plate, "", 56, Paper, TextAnchor.UpperCenter, display: true);
            Stretch(_kidName.rectTransform, new Vector2(0f, 0.42f), Vector2.one);
            _kidName.resizeTextForBestFit = true;
            _kidName.resizeTextMinSize = 30;
            _kidName.resizeTextMaxSize = 56;
            Image changeChip = Ui.MakeImage("ChangeChip", plate, new Color(0f, 0f, 0f, 0.55f));
            Place(changeChip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(150f, 38f));
            changeChip.rectTransform.pivot = new Vector2(0.5f, 0f);
            Frame(changeChip.rectTransform, Gold, 2f);
            Text change = Label("Change", changeChip.rectTransform, "CHANGE KID", 16, Gold, TextAnchor.MiddleCenter, display: true);
            Stretch(change.rectTransform, Vector2.zero, Vector2.one);
            KidButton = AttachButton(plate.gameObject, plateHit, () => Navigate(MenuStack.PickId));

            // 2. right column: the mode card (tap to change) over CODM's big gold START, here PLAY
            _modeColumn = Ui.Panel("ModeColumn", _hubBody, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(420f, 0f));

            _modeZone = Ui.Panel("ModeZone", _modeColumn, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 220f));
            Image modeBg = _modeZone.gameObject.AddComponent<Image>();
            modeBg.color = new Color(0f, 0f, 0f, 0.6f);
            RectTransform modeArt = Ui.Fill("Art", _modeZone);
            modeArt.gameObject.AddComponent<RectMask2D>();
            _modeArt = MakeRaw("Picture", modeArt, null);
            _modeArt.color = new Color(0.8f, 0.8f, 0.84f, 1f);
            _modeArtFit = _modeArt.gameObject.AddComponent<AspectRatioFitter>();
            _modeArtFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            RawImage modeShade = MakeRaw("Shade", _modeZone, Ramp(false));
            modeShade.color = new Color(0f, 0f, 0f, 0.92f);
            Stretch(modeShade.rectTransform, Vector2.zero, new Vector2(1f, 0.75f));
            Image modeAccent = Ui.MakeImage("Accent", _modeZone, Gold);
            Stretch(modeAccent.rectTransform, new Vector2(0f, 1f), Vector2.one);
            modeAccent.rectTransform.sizeDelta = new Vector2(0f, 4f);
            modeAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            Text modeKicker = Label("Kicker", _modeZone, "AGAWANG BASE  /  BARANGAY", 16, Gold, TextAnchor.UpperLeft, display: true);
            Place(modeKicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-36f, 24f));
            modeKicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            _modeName = Label("ModeName", _modeZone, "", 48, Paper, TextAnchor.LowerLeft, display: true);
            Place(_modeName.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 56f), new Vector2(-36f, 60f));
            _modeName.rectTransform.pivot = new Vector2(0.5f, 0f);
            _modeName.resizeTextForBestFit = true;
            _modeName.resizeTextMinSize = 28;
            _modeName.resizeTextMaxSize = 48;
            _modeDetail = Label("ModeDetail", _modeZone, "", 18, Paper, TextAnchor.LowerLeft, display: true);
            Place(_modeDetail.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 32f));
            _modeDetail.rectTransform.pivot = new Vector2(0.5f, 0f);
            Text modeHint = Label("ModeHint", _modeZone, "TAP TO CHANGE", 16, Gold, TextAnchor.LowerRight, display: true);
            Place(modeHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(180f, 32f));
            modeHint.rectTransform.pivot = new Vector2(1f, 0f);
            ModeButton = AttachButton(_modeZone.gameObject, modeBg, CycleHomeMode);
            ShowHomeMode();

            _playZone = Ui.Panel("PlayZone", _modeColumn, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 128f));
            Image playFill = _playZone.gameObject.AddComponent<Image>();
            playFill.color = Gold;
            // CODM's cut corner: an ink triangle over the top-left corner of the gold button
            Image cut = Ui.MakeImage("Cut", _playZone, Ink);
            cut.raycastTarget = false;
            RectTransform cr = cut.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(44f, 44f);
            cr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Text playLabel = Label("Label", _playZone, "PLAY", 60, Ink, TextAnchor.MiddleCenter, display: true);
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
            Image top = Ui.MakeImage("Accent", card, Gold);
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


            _offlineHub.gameObject.SetActive(false);
        }

        /// <summary>Hotspot, Practice vs AI, Practice solo (Explore joins when it is built: only real content, docs/SPEC.md §8).</summary>
        const int OfflineCardCount = 3;

        RectTransform MakeOfflineCard(RectTransform cards, string name, int index, string title, string artPath, bool main)
        {
            const float gap = 14f;
            const float share = 1f / OfflineCardCount;
            RectTransform card = Ui.Panel(name, cards, new Vector2(index * share, 0f), new Vector2((index + 1) * share, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 0f);
            card.offsetMax = new Vector2(index == OfflineCardCount - 1 ? 0f : -gap * 0.5f, 0f);
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

            Image accent = Ui.MakeImage("Accent", card, main ? Gold : Hairline);
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
            fill.color = primary ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            if (!primary) Frame(rect, new Color(Paper.r, Paper.g, Paper.b, 0.55f), 2f);
            Text label = Label("Label", rect, title, 24, primary ? Ink : Paper, TextAnchor.MiddleCenter, display: true);
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
                _rankFillImage.color = ParseHexColor(tier.ColorHex, Gold);
            }

            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (_kidName != null)
                _kidName.text = (ReadCharacterDisplayName(kidId) ?? Capitalize(kidId)).ToUpperInvariant();
            if (_kidsIcon != null)
            {
                Texture2D face = KalyeahResources.Texture("portraits/faces/" + kidId);
                _kidsIcon.texture = face;
                _kidsIcon.enabled = face != null;
            }
            _stage.SetKid(kidId);
            _stage.RenderNow();
            RefreshLayout(); // the kid's feet height changes with the kid

            if (_difficultyLabel != null)
                _difficultyLabel.text = SettingsStore.NormalizeDifficulty(session.BotDifficulty).ToUpperInvariant();
        }

        /// <summary>Reflows the top bar, the three hub columns (rail, kid, mode) and the hero for 4:3 foldables through 21:9.</summary>
        public void RefreshLayout()
        {
            if (_disposed || _topBar == null || _hubBody == null) return;
            Rect safe = SafeRoot.rect;
            Rect full = Root.rect;
            float width = safe.width;

            _hubBody.offsetMin = new Vector2(Gutter, 16f);
            _hubBody.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));
            if (_supportTab != null) _supportTab.gameObject.SetActive(width >= NarrowTopBarWidth);

            // columns: rail on the left, mode column on the right, the kid's plate in the space between
            float bodyWidth = Mathf.Max(0f, width - 2f * Gutter);
            float rightWidth = Mathf.Clamp(bodyWidth * 0.34f, 320f, 440f);
            const float columnGap = 16f;
            if (_navRail != null)
            {
                _navRail.sizeDelta = new Vector2(RailWidth, 0f);
                _navRail.anchoredPosition = Vector2.zero;
                float bodyHeight = _hubBody.rect.height;
                float step = Mathf.Min(RailTileHeight + 12f, bodyHeight / Mathf.Max(1, _navRail.childCount));
                for (int i = 0; i < _navRail.childCount; i++)
                {
                    var tile = _navRail.GetChild(i) as RectTransform;
                    if (tile == null) continue;
                    tile.anchoredPosition = new Vector2(0f, -i * step);
                    tile.sizeDelta = new Vector2(0f, Mathf.Min(RailTileHeight, step - 8f));
                }
            }
            if (_modeColumn != null) _modeColumn.sizeDelta = new Vector2(rightWidth, 0f);
            float heroLeft = RailWidth + columnGap;
            float heroRight = bodyWidth - rightWidth - columnGap;
            if (_heroZone != null)
            {
                _heroZone.anchoredPosition = new Vector2(heroLeft, 0f);
                _heroZone.sizeDelta = new Vector2(Mathf.Max(0f, heroRight - heroLeft), 0f);
            }

            // the kid: nearly full height, centred over its plate, feet just above the name
            float heroHeight = full.height * 0.9f;
            float heroWidth = heroHeight * 0.75f;
            if (_heroImageRect != null && _heroZone != null)
            {
                Vector3 zoneCentre = Root.InverseTransformPoint(_heroZone.TransformPoint(_heroZone.rect.center));
                float heroCentre = zoneCentre.x - full.xMin;
                float plateTop = Root.InverseTransformPoint(_heroZone.TransformPoint(new Vector3(0f, _heroZone.rect.yMin + 120f, 0f))).y - full.yMin;
                _heroImageRect.sizeDelta = new Vector2(heroWidth, heroHeight);
                _heroImageRect.anchoredPosition = new Vector2(heroCentre, plateTop - heroHeight * _stage.FeetViewportY);
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
            if (_heroGlowRect != null)
            {
                _heroGlowRect.anchoredPosition = _heroShadowRect.anchoredPosition;
                _heroGlowRect.sizeDelta = new Vector2(size.x * 0.95f, size.x * 0.2f);
            }
        }

        // ---------------------------------------------------------------- flow

        /// <summary>PLAY: online first. With no server configured the offline prompt opens at once.</summary>
        /// <summary>The modes Home's PLAY can start (docs/SPEC.md §4); Ranked joins when it is built.</summary>
        enum HomeMode { Blitz, Quick, Offline }

        HomeMode _homeMode;
        Text _modeName;
        Text _modeDetail;

        /// <summary>Taps through Blitz (first to 3), Quick (first to 5) and Offline.</summary>
        public Button ModeButton { get; private set; }

        void CycleHomeMode()
        {
            TapFeedback();
            _homeMode = (HomeMode)(((int)_homeMode + 1) % 3);
            if (_homeMode != HomeMode.Offline) AppSession.Current.FirstTo = _homeMode == HomeMode.Blitz ? 3 : 5;
            ShowHomeMode();
        }

        void ShowHomeMode()
        {
            if (_homeMode != HomeMode.Offline) _homeMode = AppSession.Current.FirstTo >= 5 ? HomeMode.Quick : HomeMode.Blitz;
            if (_modeName != null) _modeName.text = _homeMode.ToString().ToUpperInvariant();
            if (_modeDetail != null)
                _modeDetail.text = _homeMode switch
                {
                    HomeMode.Blitz => "5V5  /  FIRST TO 3",
                    HomeMode.Quick => "5V5  /  FIRST TO 5",
                    _ => "HOTSPOT  /  PRACTICE",
                };
            if (_modeArt != null)
            {
                Texture2D art = KalyeahResources.Texture(_homeMode == HomeMode.Offline ? "ui/mode_pictures/hotspot" : "ui/mode_pictures/online");
                _modeArt.texture = art;
                _modeArt.enabled = art != null;
                if (art != null && _modeArtFit != null) _modeArtFit.aspectRatio = (float)art.width / art.height;
            }
        }

        void PressPlay()
        {
            CloseOfflinePrompt();
            AppSession session = AppSession.Current;
            if (_homeMode == HomeMode.Offline)
            {
                OpenOfflineHub();
                return;
            }
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
