using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The home lobby (1280x720 reference, SafeArea-pinned, every hit area &gt;= 48 dp), drawn entirely in code from the Kalyeah
    /// palette and one tag shape (<see cref="MenuArt"/>): a night-teal stage with a woven lozenge pattern, a sunburst and drifting
    /// motes behind the selected kid on a lit floor; the profile, support and settings tiles on top; a rail of KIDS, CODEX and
    /// HISTORY on the left; the kid's wooden name board with role and purok at its feet; and on the right the mode card (tap to
    /// change) over one big gold PLAY. No logo or tagline: the name shows only on the splash (docs/SPEC.md §8).
    /// PLAY is online first: it opens the online room; when the server can't be reached, <see cref="ShowOfflinePrompt"/> offers
    /// the offline hub (Hotspot, Practice vs AI, Practice solo).
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Muted = new Color(0.66f, 0.80f, 0.82f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.80f, 0.80f, 0.84f, 1f);

        const float TopBarHeight = 112f;
        const float RailWidth = 150f;
        const float RailTileHeight = 118f;
        const float HitSize = 96f;
        const float Gutter = 20f;
        const float NarrowTopBarWidth = 1000f;
        const float PlayHeight = 136f;
        const float ModeHeight = 232f;
        const float ColumnGap = 24f;
        const float WeaveTile = 96f;
        const int MoteCount = 14;

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
        float _time;

        // stage: behind the kid
        RawImage _weave;
        RectTransform _sunburst;
        RawImage _sunburstRaw;
        Image _glowWide;
        RectTransform _glowWideRect;
        RectTransform _glowCoreRect;
        RectTransform _floorDisc;
        RectTransform _ringOuter;
        RectTransform _ringInner;
        RectTransform _heroImageRect;
        RectTransform _heroShadowRect;
        readonly RectTransform[] _moteRects = new RectTransform[MoteCount];
        readonly Image[] _moteImages = new Image[MoteCount];
        Vector2 _areaSize = new Vector2(1280f, 720f);

        // bars and columns
        RectTransform _topBar;
        RectTransform _hubBody;
        RectTransform _navRail;
        RectTransform _heroZone;
        RectTransform _modeColumn;
        RectTransform _modeZone;
        RectTransform _playZone;
        RectTransform _supportTab;
        RawImage _kidsIcon;

        RawImage _heroImage;
        RawImage _avatarImage;
        Text _playerName;
        Text _rankLabel;
        RectTransform _rankFill;
        Image _rankFillImage;
        Text _kidName;
        Text _kidRole;
        Image _kidRoleChip;

        // mode card
        RawImage _modeWeave;
        Image _modeHeader;
        Image _modeGlow;
        Text _modeKicker;
        readonly Image[] _pagerDots = new Image[3];
        Color _accent = Gold;
        Color _accentTarget = Gold;

        // PLAY
        RectTransform _playClip;
        RectTransform _playShine;

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
            _stage = new HeroStage();

            ScreenFitMetrics.EnsureEnhancedTouch();

            Root = Ui.Fill("HomeScreen", parent);
            BuildBackdrop(Root);
            BuildAtmosphere(Root);
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
            _time += dt;
            BlendAccent(dt);
            if (SettingsStore.Current.ReduceMotion) return;
            AnimateStage();
            AnimatePlay();
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

        // ---------------------------------------------------------------- stage

        void BuildBackdrop(RectTransform parent)
        {
            RectTransform bg = Ui.Fill("Backdrop", parent);
            RawImage sky = MakeRaw("Sky", bg, MenuArt.Sky());
            sky.color = Color.white;

            // the woven lozenge pattern, tiled and drifting slowly
            _weave = MakeRaw("Weave", bg, MenuArt.Weave());
            _weave.color = new Color(MenuArt.TealLight.r, MenuArt.TealLight.g, MenuArt.TealLight.b, 0.10f);

            RawImage vignette = MakeRaw("Vignette", bg, MenuArt.Vignette());
            vignette.color = Color.white;

            // the top strip stays dark under the bar and the floor darkens toward the bottom edge
            RawImage topShade = MakeRaw("TopShade", bg, Ramp(false));
            topShade.color = new Color(0f, 0.02f, 0.03f, 0.55f);
            topShade.uvRect = new Rect(0f, 1f, 1f, -1f);
            Stretch(topShade.rectTransform, new Vector2(0f, 0.8f), Vector2.one);
            RawImage floorShade = MakeRaw("FloorShade", bg, Ramp(false));
            floorShade.color = new Color(0f, 0.02f, 0.03f, 0.7f);
            Stretch(floorShade.rectTransform, Vector2.zero, new Vector2(1f, 0.22f));
        }

        /// <summary>The light behind the kid: sunburst, warm glows, the lit floor and drifting motes (clipped to the screen).</summary>
        void BuildAtmosphere(RectTransform parent)
        {
            RectTransform atmosphere = Ui.Fill("Atmosphere", parent);
            atmosphere.gameObject.AddComponent<RectMask2D>();

            _sunburstRaw = MakeRaw("Sunburst", atmosphere, MenuArt.Sunburst());
            _sunburstRaw.color = new Color(Gold.r, Gold.g, Gold.b, 0.2f);
            _sunburst = _sunburstRaw.rectTransform;
            AnchorLowerLeft(_sunburst);

            _glowWide = Ui.MakeImage("GlowWide", atmosphere, new Color(Gold.r, Gold.g, Gold.b, 0.22f));
            _glowWide.sprite = MenuArt.Glow();
            _glowWideRect = _glowWide.rectTransform;
            AnchorLowerLeft(_glowWideRect);
            Image core = Ui.MakeImage("GlowCore", atmosphere, new Color(MenuArt.GoldLight.r, MenuArt.GoldLight.g, MenuArt.GoldLight.b, 0.20f));
            core.sprite = MenuArt.Glow();
            _glowCoreRect = core.rectTransform;
            AnchorLowerLeft(_glowCoreRect);

            // the floor: a dark teal disc, two gold rings
            Image disc = Ui.MakeImage("FloorDisc", atmosphere, new Color(0.02f, 0.10f, 0.12f, 0.85f));
            disc.sprite = MenuArt.Glow();
            _floorDisc = disc.rectTransform;
            AnchorLowerLeft(_floorDisc);
            Image outer = Ui.MakeImage("FloorRingOuter", atmosphere, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
            outer.sprite = KomiksUi.RingSprite(256, 6);
            _ringOuter = outer.rectTransform;
            AnchorLowerLeft(_ringOuter);
            Image inner = Ui.MakeImage("FloorRingInner", atmosphere, new Color(MenuArt.TealLight.r, MenuArt.TealLight.g, MenuArt.TealLight.b, 0.35f));
            inner.sprite = KomiksUi.RingSprite(256, 5);
            _ringInner = inner.rectTransform;
            AnchorLowerLeft(_ringInner);

            // motes: small gold, cream and teal diamonds drifting up
            Color[] palette = { Gold, Cream, MenuArt.TealLight };
            for (int i = 0; i < MoteCount; i++)
            {
                Image mote = Ui.MakeImage("Mote" + i, atmosphere, palette[i % palette.Length]);
                mote.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Diamond);
                float size = 7f + (i * 5 % 7);
                mote.rectTransform.sizeDelta = new Vector2(size, size);
                AnchorLowerLeft(mote.rectTransform);
                _moteRects[i] = mote.rectTransform;
                _moteImages[i] = mote;
            }
        }

        static void AnchorLowerLeft(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        void BuildHero(RectTransform parent)
        {
            Image shadow = Ui.MakeImage("HeroShadow", parent, new Color(0f, 0.02f, 0.03f, 0.6f), round: true);
            _heroShadowRect = shadow.rectTransform;
            AnchorLowerLeft(_heroShadowRect);
            _heroShadowRect.sizeDelta = new Vector2(320f, 56f);

            _heroImage = MakeRaw("Hero", parent, _stage.Texture);
            _heroImageRect = _heroImage.rectTransform;
            _heroImageRect.anchorMin = _heroImageRect.anchorMax = new Vector2(0f, 0f);
            _heroImageRect.pivot = new Vector2(0.5f, 0f);
        }

        // ---------------------------------------------------------------- bars

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));

            // profile: avatar, name, rank line and LP bar on a glass tag (one button: opens the profile sheet)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(360f, HitSize));
            Image profilePlate = Tag(profile, MenuArt.Glass(18), 18, true);
            ProfileButton = AttachButton(profile.gameObject, profilePlate, () => { TapFeedback(); Sheets?.OpenProfile(); });

            RectTransform avatar = Ui.Panel("Avatar", profile, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(68f, 68f));
            Image avatarFrame = ChipImage("Frame", avatar, Gold, 8);
            Stretch(avatarFrame.rectTransform, Vector2.zero, Vector2.one);
            Image avatarWell = Ui.MakeImage("Well", avatar, MenuArt.NightMid);
            Stretch(avatarWell.rectTransform, Vector2.zero, Vector2.one);
            avatarWell.rectTransform.offsetMin = new Vector2(4f, 4f);
            avatarWell.rectTransform.offsetMax = new Vector2(-4f, -4f);
            _avatarImage = MakeRaw("Photo", avatarWell.rectTransform, null);

            _playerName = Label("Name", profile, "", 28, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(98f, 2f), new Vector2(-114f, 34f));
            _playerName.rectTransform.pivot = new Vector2(0f, 0f);
            _playerName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _playerName.resizeTextForBestFit = true;
            _playerName.resizeTextMinSize = 16;
            _playerName.resizeTextMaxSize = 28;

            _rankLabel = Label("Rank", profile, "", 15, MenuArt.TealLight, TextAnchor.UpperLeft);
            Place(_rankLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(98f, -3f), new Vector2(-114f, 22f));
            _rankLabel.rectTransform.pivot = new Vector2(0f, 1f);

            Image rankTrack = Ui.MakeImage("RankTrack", profile, new Color(0f, 0.02f, 0.03f, 0.65f));
            Place(rankTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(98f, -32f), new Vector2(176f, 8f));
            rankTrack.rectTransform.pivot = new Vector2(0f, 0.5f);
            _rankFillImage = Ui.MakeImage("RankFill", rankTrack.rectTransform, Gold);
            _rankFill = _rankFillImage.rectTransform;
            Stretch(_rankFill, Vector2.zero, new Vector2(0.2f, 1f));

            // right: settings gear tile, then support
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(HitSize, HitSize));
            Image settingsPlate = Tag(settings, MenuArt.Glass(18), 18, true);
            Image gear = Ui.MakeImage("Gear", settings, Cream);
            gear.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Gear);
            Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            AttachButton(settings.gameObject, settingsPlate, () => Navigate(MenuStack.SettingsId));

            _supportTab = Ui.Panel("Support", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter - HitSize - 16f, 0f), new Vector2(190f, HitSize));
            Image supportPlate = Tag(_supportTab, MenuArt.Glass(18), 18, true);
            Image heart = Ui.MakeImage("Heart", _supportTab, MenuArt.Coral);
            heart.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
            Place(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(40f, 40f));
            heart.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text supportText = Label("Label", _supportTab, "SUPPORT", 22, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Stretch(supportText.rectTransform, Vector2.zero, Vector2.one);
            supportText.rectTransform.offsetMin = new Vector2(76f, 0f);
            supportText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            AttachButton(_supportTab.gameObject, supportPlate, () => { TapFeedback(); Sheets?.OpenDonate(); });
        }

        /// <summary>One rail tile: a glass tag with a drawn icon on top and its label underneath.</summary>
        RectTransform MakeRailTile(RectTransform rail, string name, string title, Action<RectTransform> buildIcon, Action onClick)
        {
            RectTransform tile = Ui.Panel(name, rail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RailTileHeight));
            Image plate = Tag(tile, MenuArt.Glass(14), 14, true, 0.5f, 5f);

            RectTransform iconBox = Ui.Panel("IconBox", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(70f, 70f));
            buildIcon(iconBox);

            Text label = Label("Label", tile, title, 20, Cream, TextAnchor.LowerCenter, display: true, ink: true);
            Stretch(label.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            AttachButton(tile.gameObject, plate, onClick);
            return tile;
        }

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // 0. left rail: KIDS (the selected kid's face), CODEX (three fanned cards), HISTORY (a clock)
            _navRail = Ui.Panel("NavRail", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(RailWidth, 0f));
            MakeRailTile(_navRail, "Tab_Kids", "KIDS", box =>
            {
                Image frame = ChipImage("Frame", box, Gold, 8);
                Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
                Image well = Ui.MakeImage("Well", box, MenuArt.NightMid);
                Stretch(well.rectTransform, Vector2.zero, Vector2.one);
                well.rectTransform.offsetMin = new Vector2(4f, 4f);
                well.rectTransform.offsetMax = new Vector2(-4f, -4f);
                _kidsIcon = MakeRaw("Face", well.rectTransform, null);
            }, () => Navigate(MenuStack.PickId));
            MakeRailTile(_navRail, "Tab_Codex", "CODEX", box =>
            {
                Color[] colours = { MenuArt.Teal, Gold, MenuArt.Coral };
                Vector2[] at = { new Vector2(-15f, -2f), new Vector2(15f, -2f), new Vector2(0f, 3f) };
                float[] turn = { 15f, -15f, 0f };
                for (int i = 0; i < 3; i++)
                {
                    Image card = ChipImage("Card" + i, box, colours[i], 6);
                    Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at[i], new Vector2(38f, 54f));
                    card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, turn[i]);
                }
            }, () => Navigate(MenuStack.CodexId));
            MakeRailTile(_navRail, "Tab_History", "HISTORY", box =>
            {
                Image clock = Ui.MakeImage("Clock", box, Cream);
                clock.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Clock);
                Place(clock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 66f));
            }, () => Navigate(MenuStack.HistoryId));

            // 1. hero zone: the kid's wooden name board at its feet (tap to change kid)
            _heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform plate = Ui.Panel("KidPlate", _heroZone, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(360f, 120f));
            Image plateHit = plate.gameObject.AddComponent<Image>();
            plateHit.color = Clear;

            RectTransform board = Ui.Panel("Board", plate, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(330f, 68f));
            Tag(board, MenuArt.Plate(MenuArt.WoodLight, MenuArt.WoodDark, Ink, 14, 3, 0.3f), 14, false, 0.55f, 6f);
            for (int i = 0; i < 2; i++)
            {
                Image nail = Ui.MakeImage("Nail" + i, board, new Color(Cream.r, Cream.g, Cream.b, 0.85f), round: true);
                float side = i == 0 ? 0f : 1f;
                Place(nail.rectTransform, new Vector2(side, 0.5f), new Vector2(side, 0.5f), new Vector2(i == 0 ? 22f : -22f, 0f), new Vector2(9f, 9f));
            }
            _kidName = Label("KidName", board, "", 48, Cream, TextAnchor.MiddleCenter, display: true, ink: true);
            Stretch(_kidName.rectTransform, Vector2.zero, Vector2.one);
            _kidName.rectTransform.offsetMin = new Vector2(40f, 2f);
            _kidName.rectTransform.offsetMax = new Vector2(-40f, -2f);
            _kidName.resizeTextForBestFit = true;
            _kidName.resizeTextMinSize = 26;
            _kidName.resizeTextMaxSize = 48;

            RectTransform chips = Ui.Panel("Chips", plate, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(330f, 36f));
            RectTransform roleChip = Ui.Panel("Role", chips, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(184f, 0f));
            _kidRoleChip = ChipImage("Plate", roleChip, Gold, 9);
            Stretch(_kidRoleChip.rectTransform, Vector2.zero, Vector2.one);
            _kidRole = Label("Text", roleChip, "", 17, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(_kidRole.rectTransform, Vector2.zero, Vector2.one);
            _kidRole.resizeTextForBestFit = true;
            _kidRole.resizeTextMinSize = 11;
            _kidRole.resizeTextMaxSize = 17;
            RectTransform changeChip = Ui.Panel("Change", chips, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(134f, 0f));
            Image changePlate = Ui.MakeImage("Plate", changeChip, Color.white);
            changePlate.sprite = MenuArt.Plate(MenuArt.GlassTop, MenuArt.GlassBottom, Gold, 9, 2, 0.2f);
            changePlate.type = Image.Type.Sliced;
            Stretch(changePlate.rectTransform, Vector2.zero, Vector2.one);
            Text change = Label("Text", changeChip, "CHANGE", 17, Gold, TextAnchor.MiddleCenter, display: true);
            Stretch(change.rectTransform, Vector2.zero, Vector2.one);
            KidButton = AttachButton(plate.gameObject, plateHit, () => Navigate(MenuStack.PickId));

            // 2. right column: the mode card (tap to change) over one big gold PLAY
            _modeColumn = Ui.Panel("ModeColumn", _hubBody, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(420f, 0f));
            BuildModeCard(_modeColumn);
            BuildPlay(_modeColumn);
        }

        void BuildModeCard(RectTransform column)
        {
            _modeZone = Ui.Panel("ModeZone", column, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PlayHeight + ColumnGap), new Vector2(0f, ModeHeight));
            Image plate = Tag(_modeZone, MenuArt.Glass(22), 22, true, 0.55f, 7f);

            // inside the card: a corner glow in the mode's colour and the weave, clipped to the card
            RectTransform inside = Ui.Fill("Inside", _modeZone);
            inside.offsetMin = new Vector2(4f, 4f);
            inside.offsetMax = new Vector2(-4f, -4f);
            inside.gameObject.AddComponent<RectMask2D>();
            _modeWeave = MakeRaw("Weave", inside, MenuArt.Weave());
            _modeWeave.color = new Color(MenuArt.TealLight.r, MenuArt.TealLight.g, MenuArt.TealLight.b, 0.08f);
            _modeGlow = Ui.MakeImage("Glow", inside, new Color(Gold.r, Gold.g, Gold.b, 0.34f));
            _modeGlow.sprite = MenuArt.Glow();
            Place(_modeGlow.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(300f, 260f));

            // the header chip in the mode's colour
            RectTransform headerHost = Ui.Panel("Header", _modeZone, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-40f, 36f));
            _modeHeader = ChipImage("Plate", headerHost, Gold, 9);
            Stretch(_modeHeader.rectTransform, Vector2.zero, Vector2.one);
            _modeKicker = Label("Kicker", headerHost, "AGAWANG BASE  /  BARANGAY", 17, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(_modeKicker.rectTransform, Vector2.zero, Vector2.one);
            _modeKicker.resizeTextForBestFit = true;
            _modeKicker.resizeTextMinSize = 11;
            _modeKicker.resizeTextMaxSize = 17;

            _modeName = Label("ModeName", _modeZone, "", 66, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            Place(_modeName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -112f), new Vector2(-48f, 72f));
            _modeName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _modeName.resizeTextForBestFit = true;
            _modeName.resizeTextMinSize = 30;
            _modeName.resizeTextMaxSize = 66;
            _modeDetail = Label("ModeDetail", _modeZone, "", 20, MenuArt.TealLight, TextAnchor.UpperLeft, display: true);
            Place(_modeDetail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(-48f, 28f));
            _modeDetail.rectTransform.pivot = new Vector2(0.5f, 1f);

            // the pager (which of the three modes) and the tap hint
            for (int i = 0; i < _pagerDots.Length; i++)
            {
                Image dot = Ui.MakeImage("Dot" + i, _modeZone, Cream, round: true);
                Place(dot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f + i * 22f, 24f), new Vector2(12f, 12f));
                _pagerDots[i] = dot;
            }
            Text hint = Label("ModeHint", _modeZone, "TAP TO CHANGE", 16, Gold, TextAnchor.MiddleRight, display: true);
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-46f, 24f), new Vector2(170f, 28f));
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
            Image arrow = Ui.MakeImage("Arrow", _modeZone, Gold);
            arrow.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Play);
            Place(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(20f, 20f));

            ModeButton = AttachButton(_modeZone.gameObject, plate, CycleHomeMode);
            ShowHomeMode();
        }

        void BuildPlay(RectTransform column)
        {
            _playZone = Ui.Panel("PlayZone", column, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PlayHeight));
            Sprite gold = MenuArt.Plate(MenuArt.GoldLight, MenuArt.GoldDeep, Ink, 26, 4, 0.55f);
            Image plate = Tag(_playZone, gold, 26, true, 0.7f, 9f);

            // a soft shine that sweeps across now and then, kept off the cut corners
            _playClip = Ui.Fill("ShineClip", _playZone);
            _playClip.offsetMin = new Vector2(30f, 12f);
            _playClip.offsetMax = new Vector2(-30f, -12f);
            _playClip.gameObject.AddComponent<RectMask2D>();
            Image shine = Ui.MakeImage("Shine", _playClip, new Color(1f, 1f, 1f, 0.55f));
            shine.sprite = MenuArt.Glow();
            _playShine = shine.rectTransform;
            _playShine.anchorMin = _playShine.anchorMax = new Vector2(0f, 0.5f);
            _playShine.sizeDelta = new Vector2(110f, 340f);
            _playShine.localRotation = Quaternion.Euler(0f, 0f, 18f);
            _playShine.anchoredPosition = new Vector2(-200f, 0f);

            Image triangle = Ui.MakeImage("Triangle", _playZone, Ink);
            triangle.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Play);
            Place(triangle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f));
            triangle.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text label = Label("Label", _playZone, "PLAY", 68, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(96f, 0f);
            label.rectTransform.offsetMax = new Vector2(-24f, 0f);
            var emboss = label.gameObject.AddComponent<Shadow>();
            emboss.effectColor = new Color(1f, 0.96f, 0.72f, 0.75f);
            emboss.effectDistance = new Vector2(0f, -3f);
            PlayButton = AttachButton(_playZone.gameObject, plate, PressPlay);
        }

        // ---------------------------------------------------------------- offline

        void BuildOfflinePrompt()
        {
            _offlinePrompt = Ui.Fill("OfflinePrompt", Root);
            Image scrim = _offlinePrompt.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.01f, 0.04f, 0.06f, 0.84f);
            scrim.raycastTarget = true;

            RectTransform card = Ui.Panel("Card", _offlinePrompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 310f));
            Tag(card, MenuArt.Glass(24), 24, false, 0.6f, 8f);

            Text title = Label("Title", card, "OFFLINE", 48, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Stretch(title.rectTransform, new Vector2(0f, 1f), Vector2.one);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-72f, 60f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            Image rule = Ui.MakeImage("Rule", card, Gold);
            Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -94f), new Vector2(84f, 5f));
            rule.rectTransform.pivot = new Vector2(0f, 1f);
            Text body = Label("Body", card, "Can't reach the game server.", 22, Muted, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0f, 1f), Vector2.one);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.sizeDelta = new Vector2(-72f, 32f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -112f);

            RectTransform actions = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-72f, HitSize));
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
            Image block = _offlineHubBackdrop.gameObject.AddComponent<Image>();
            block.color = Clear;
            block.raycastTarget = true;
            MakeRaw("Sky", _offlineHubBackdrop, MenuArt.Sky());
            RawImage weave = MakeRaw("Weave", _offlineHubBackdrop, MenuArt.Weave());
            weave.color = new Color(MenuArt.TealLight.r, MenuArt.TealLight.g, MenuArt.TealLight.b, 0.09f);
            weave.uvRect = new Rect(0f, 0f, 1280f / WeaveTile, 720f / WeaveTile);
            MakeRaw("Vignette", _offlineHubBackdrop, MenuArt.Vignette());
            _offlineHubBackdrop.gameObject.SetActive(false);
            _offlineHubBackdrop.SetSiblingIndex(SafeRoot.GetSiblingIndex()); // behind the hub, over the hero and the stage

            _offlineHub = Ui.Fill("OfflineHub", SafeRoot);
            RectTransform header = Ui.Panel("Header", _offlineHub, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(140f, HitSize));
            Image backPlate = Tag(back, MenuArt.Glass(18), 18, true);
            Text backLabel = Label("Label", back, "BACK", 24, Cream, TextAnchor.MiddleCenter, display: true, ink: true);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one);
            AttachButton(back.gameObject, backPlate, CloseOfflineHub);
            Text title = Label("Title", header, "OFFLINE", 52, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 140f + 28f, 0f), new Vector2(-(Gutter + 140f + 28f) - Gutter, 64f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            RectTransform cards = Ui.Fill("Cards", _offlineHub);
            cards.offsetMin = new Vector2(Gutter, 20f);
            cards.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            RectTransform hotspot = MakeOfflineCard(cards, "Hotspot", 0, "HOTSPOT", "hotspot", MenuArt.Teal);
            RectTransform hotspotRow = ActionRow(hotspot);
            MakeActionButton(hotspotRow, "Host", "HOST", true, Vector2.zero, new Vector2(0.49f, 1f), () => OpenHotspotFlow(true));
            MakeActionButton(hotspotRow, "Join", "JOIN", false, new Vector2(0.51f, 0f), Vector2.one, () => OpenHotspotFlow(false));

            RectTransform ai = MakeOfflineCard(cards, "PracticeAi", 1, "PRACTICE VS AI", "ai", Gold);
            RectTransform aiRow = ActionRow(ai);
            Button diff = MakeActionButton(aiRow, "Difficulty", "MEDIUM", false, Vector2.zero, new Vector2(0.49f, 1f), CycleDifficulty);
            _difficultyLabel = diff.GetComponentInChildren<Text>(true);
            MakeActionButton(aiRow, "Play", "PLAY", true, new Vector2(0.51f, 0f), Vector2.one, () => LaunchMode(GameMode.Practice));

            RectTransform solo = MakeOfflineCard(cards, "PracticeSolo", 2, "PRACTICE SOLO", "solo", MenuArt.Coral);
            MakeActionButton(ActionRow(solo), "Play", "PLAY", true, Vector2.zero, Vector2.one, () => LaunchMode(GameMode.Training));

            _offlineHub.gameObject.SetActive(false);
        }

        /// <summary>Hotspot, Practice vs AI, Practice solo (Explore joins when it is built: only real content, docs/SPEC.md §8).</summary>
        const int OfflineCardCount = 3;

        RectTransform MakeOfflineCard(RectTransform cards, string name, int index, string title, string glyph, Color accent)
        {
            const float gap = 20f;
            const float share = 1f / OfflineCardCount;
            RectTransform card = Ui.Panel(name, cards, new Vector2(index * share, 0f), new Vector2((index + 1) * share, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 8f);
            card.offsetMax = new Vector2(index == OfflineCardCount - 1 ? -8f : -gap * 0.5f, 0f);
            Tag(card, MenuArt.Glass(22), 22, false, 0.55f, 7f);

            // the art: the card's colour with the weave and a drawn glyph (no pictures)
            RectTransform art = Ui.Panel("Art", card, new Vector2(0f, 0.42f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            art.offsetMin = new Vector2(12f, 0f);
            art.offsetMax = new Vector2(-12f, -12f);
            Stretch(ChipImage("Colour", art, accent, 14).rectTransform, Vector2.zero, Vector2.one);
            RectTransform clip = Ui.Fill("Clip", art);
            clip.offsetMin = new Vector2(4f, 4f);
            clip.offsetMax = new Vector2(-4f, -4f);
            clip.gameObject.AddComponent<RectMask2D>();
            RawImage weave = MakeRaw("Weave", clip, MenuArt.Weave());
            weave.color = new Color(1f, 1f, 1f, 0.16f);
            weave.uvRect = new Rect(0f, 0f, 4f, 2f);
            BuildCardGlyph(art, glyph);

            Text label = Label("Title", card, title, 32, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            Stretch(label.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.4f));
            label.rectTransform.offsetMin = new Vector2(22f, 0f);
            label.rectTransform.offsetMax = new Vector2(-14f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 32;
            return card;
        }

        static void BuildCardGlyph(RectTransform art, string glyph)
        {
            Color cream = new Color(Cream.r, Cream.g, Cream.b, 0.95f);
            switch (glyph)
            {
                case "hotspot":
                    for (int i = 0; i < 3; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, new Color(cream.r, cream.g, cream.b, 1f - 0.3f * i));
                        ring.sprite = KomiksUi.RingSprite(256, 12);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (60f + i * 56f));
                    }
                    Image dot = Ui.MakeImage("Dot", art, cream, round: true);
                    Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
                    break;
                case "ai":
                    Text vs = Label("Vs", art, "VS", 92, cream, TextAnchor.MiddleCenter, display: true, ink: true);
                    Stretch(vs.rectTransform, Vector2.zero, Vector2.one);
                    break;
                default:
                    for (int i = 0; i < 2; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, cream);
                        ring.sprite = KomiksUi.RingSprite(256, 14);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (150f - i * 66f));
                    }
                    Image core = Ui.MakeImage("Core", art, cream, round: true);
                    Place(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                    break;
            }
        }

        static RectTransform ActionRow(RectTransform card)
        {
            RectTransform row = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, HitSize));
            return row;
        }

        Button MakeActionButton(RectTransform row, string name, string title, bool primary, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            RectTransform rect = Ui.Panel(name, row, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Sprite sprite = primary
                ? MenuArt.Plate(MenuArt.GoldLight, MenuArt.GoldDeep, Ink, 14, 3, 0.5f)
                : MenuArt.Plate(MenuArt.GlassTop, MenuArt.GlassBottom, new Color(Cream.r, Cream.g, Cream.b, 0.7f), 14, 2, 0.2f);
            Image plate = Tag(rect, sprite, 14, true, primary ? 0.55f : 0.4f, 5f);
            Text label = Label("Label", rect, title, 26, primary ? Ink : Cream, TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 26;
            return AttachButton(rect.gameObject, plate, onClick);
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
                _kidName.text = (ReadCharacterField(kidId, "display_name") ?? Capitalize(kidId)).ToUpperInvariant();
            if (_kidRole != null)
            {
                ParseRoleAndPurok(ReadCharacterField(kidId, "description"), out string role, out string purok);
                _kidRole.text = string.IsNullOrEmpty(purok) ? role : role + "  /  " + purok;
                _kidRoleChip.color = RoleColor(role);
            }
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

        /// <summary>Striker coral, Defender teal, Controller gold, Seeker sky blue.</summary>
        static Color RoleColor(string role)
        {
            switch (role)
            {
                case "STRIKER": return MenuArt.Coral;
                case "DEFENDER": return MenuArt.Teal;
                case "SEEKER": return new Color(0.42f, 0.66f, 0.96f, 1f);
                default: return Gold;
            }
        }

        /// <summary>Every kid's description starts "Role from Purok." (data/characters__*.json).</summary>
        static void ParseRoleAndPurok(string description, out string role, out string purok)
        {
            role = "";
            purok = "";
            if (string.IsNullOrWhiteSpace(description)) return;
            int dot = description.IndexOf('.');
            string first = dot > 0 ? description.Substring(0, dot) : description;
            int from = first.IndexOf(" from ", StringComparison.Ordinal);
            if (from <= 0) return;
            role = first.Substring(0, from).Trim().ToUpperInvariant();
            purok = first.Substring(from + 6).Trim().ToUpperInvariant();
        }

        /// <summary>Reflows the top bar, the three hub columns (rail, kid, mode) and the stage for 4:3 foldables through 21:9.</summary>
        public void RefreshLayout()
        {
            if (_disposed || _topBar == null || _hubBody == null) return;
            Rect safe = SafeRoot.rect;
            Rect full = Root.rect;
            float width = safe.width;
            _areaSize = new Vector2(Mathf.Max(1f, full.width), Mathf.Max(1f, full.height));

            _hubBody.offsetMin = new Vector2(Gutter, 20f);
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
                float step = Mathf.Min(RailTileHeight + 14f, bodyHeight / Mathf.Max(1, _navRail.childCount));
                for (int i = 0; i < _navRail.childCount; i++)
                {
                    var tile = _navRail.GetChild(i) as RectTransform;
                    if (tile == null) continue;
                    tile.anchoredPosition = new Vector2(0f, -i * step);
                    tile.sizeDelta = new Vector2(0f, Mathf.Min(RailTileHeight, step - 10f));
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

            // the kid: nearly full height, centred over its plate, feet just above the board
            float heroHeight = full.height * 0.9f;
            float heroWidth = heroHeight * 0.75f;
            if (_heroImageRect != null && _heroZone != null)
            {
                Vector3 zoneCentre = Root.InverseTransformPoint(_heroZone.TransformPoint(_heroZone.rect.center));
                float heroCentre = zoneCentre.x - full.xMin;
                float plateTop = Root.InverseTransformPoint(_heroZone.TransformPoint(new Vector3(0f, _heroZone.rect.yMin + 128f, 0f))).y - full.yMin;
                _heroImageRect.sizeDelta = new Vector2(heroWidth, heroHeight);
                _heroImageRect.anchoredPosition = new Vector2(heroCentre, plateTop - heroHeight * _stage.FeetViewportY);
            }
            PlaceStage();

            if (_weave != null) _weave.uvRect = new Rect(_weave.uvRect.x, _weave.uvRect.y, _areaSize.x / WeaveTile, _areaSize.y / WeaveTile);
            if (_modeWeave != null && _modeZone != null)
                _modeWeave.uvRect = new Rect(0f, 0f, Mathf.Max(1f, _modeZone.rect.width / 84f), Mathf.Max(1f, _modeZone.rect.height / 84f));
            PlaceMotes();
        }

        /// <summary>The floor, rings, glows and sunburst follow the kid's feet; the contact shadow sits under them.</summary>
        void PlaceStage()
        {
            if (_heroImageRect == null || _heroShadowRect == null) return;
            Vector2 size = _heroImageRect.sizeDelta;
            Vector2 pos = _heroImageRect.anchoredPosition;
            Vector2 feet = new Vector2(pos.x, pos.y + size.y * _stage.FeetViewportY);

            _heroShadowRect.anchoredPosition = feet;
            _heroShadowRect.sizeDelta = new Vector2(size.x * 0.42f, size.x * 0.07f);

            SetRect(_floorDisc, feet, new Vector2(size.x * 1.4f, size.x * 0.32f));
            SetRect(_ringOuter, feet, new Vector2(size.x * 1.08f, size.x * 0.23f));
            SetRect(_ringInner, feet, new Vector2(size.x * 0.74f, size.x * 0.16f));
            Vector2 chest = feet + new Vector2(0f, size.y * 0.42f);
            SetRect(_sunburst, chest, Vector2.one * (size.y * 2.1f));
            SetRect(_glowWideRect, chest, new Vector2(size.y * 1.4f, size.y * 1.15f));
            SetRect(_glowCoreRect, chest + new Vector2(0f, size.y * 0.05f), new Vector2(size.y * 0.7f, size.y * 0.85f));
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ---------------------------------------------------------------- motion

        static readonly float[] MoteSeed = { 0.07f, 0.31f, 0.52f, 0.76f, 0.18f, 0.64f, 0.93f, 0.41f, 0.27f, 0.85f, 0.58f, 0.12f, 0.71f, 0.46f };

        /// <summary>Places the motes by the clock (a fixed frame when motion is reduced or nothing ticks, e.g. in a capture).</summary>
        void PlaceMotes()
        {
            bool still = !Application.isPlaying || SettingsStore.Current.ReduceMotion;
            for (int i = 0; i < MoteCount; i++)
            {
                if (_moteRects[i] == null) continue;
                float rise = Mathf.Repeat(MoteSeed[i] + (still ? 0f : _time * (0.012f + 0.004f * (i % 4))), 1f);
                float x = Mathf.Repeat(MoteSeed[(i + 5) % MoteCount] * 1.7f + i * 0.137f, 1f) * _areaSize.x;
                x += still ? 0f : Mathf.Sin(_time * 0.6f + i) * 14f;
                _moteRects[i].anchoredPosition = new Vector2(x, rise * _areaSize.y);
                Color c = _moteImages[i].color;
                c.a = Mathf.Sin(rise * Mathf.PI) * 0.5f;
                _moteImages[i].color = c;
            }
        }

        void AnimateStage()
        {
            if (_sunburst != null) _sunburst.localRotation = Quaternion.Euler(0f, 0f, -_time * 3.2f);
            if (_weave != null)
            {
                Rect uv = _weave.uvRect;
                _weave.uvRect = new Rect(_time * 0.004f, _time * 0.0022f, uv.width, uv.height);
            }
            PlaceMotes();
        }

        void AnimatePlay()
        {
            if (_playZone != null) _playZone.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(_time * 2.2f));
            if (_playShine != null && _playClip != null)
            {
                // a sweep every 3.4 s: across in about a second, then a rest
                float phase = Mathf.Repeat(_time, 3.4f) / 1.1f;
                float travel = _playClip.rect.width + 240f;
                _playShine.anchoredPosition = new Vector2(-120f + Mathf.Clamp01(phase) * travel, 0f);
            }
        }

        /// <summary>The mode's colour: sunburst, glow, header chip and pager. Eases toward the target while ticking, jumps otherwise.</summary>
        void BlendAccent(float dt)
        {
            Color next = Color.Lerp(_accent, _accentTarget, 1f - Mathf.Exp(-dt * 6f));
            if (next == _accent) return;
            _accent = next;
            ApplyAccent();
        }

        void ApplyAccent()
        {
            if (_sunburstRaw != null) _sunburstRaw.color = new Color(_accent.r, _accent.g, _accent.b, 0.2f);
            if (_glowWide != null) _glowWide.color = new Color(_accent.r, _accent.g, _accent.b, 0.22f);
            if (_modeHeader != null) _modeHeader.color = _accent;
            if (_modeGlow != null) _modeGlow.color = new Color(_accent.r, _accent.g, _accent.b, 0.34f);
            for (int i = 0; i < _pagerDots.Length; i++)
                if (_pagerDots[i] != null)
                    _pagerDots[i].color = i == (int)_homeMode ? _accent : new Color(Cream.r, Cream.g, Cream.b, 0.3f);
        }

        // ---------------------------------------------------------------- flow

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
            if (_modeKicker != null)
                _modeKicker.text = _homeMode == HomeMode.Offline ? "HOTSPOT  /  PRACTICE" : "AGAWANG BASE  /  BARANGAY";
            if (_modeDetail != null)
                _modeDetail.text = _homeMode switch
                {
                    HomeMode.Blitz => "5V5  /  FIRST TO 3",
                    HomeMode.Quick => "5V5  /  FIRST TO 5",
                    _ => "WITH FRIENDS OR BOTS",
                };
            _accentTarget = _homeMode switch
            {
                HomeMode.Blitz => Gold,
                HomeMode.Quick => MenuArt.Teal,
                _ => MenuArt.Coral,
            };
            if (!Application.isPlaying) _accent = _accentTarget;
            ApplyAccent();
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

        string ReadCharacterField(string kidId, string field)
        {
            if (_data == null || string.IsNullOrWhiteSpace(kidId)) return null;
            try
            {
                Dictionary<string, object> p = _data.Props("characters__" + kidId);
                if (p != null && p.TryGetValue(field, out object value) && value is string s && !string.IsNullOrWhiteSpace(s))
                    return s;
            }
            catch { }
            return null;
        }

        // ---------------------------------------------------------------- small builders

        static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, bool display = false, bool ink = false)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, display ? KalyeahResources.DisplayFont() : null);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = ink;
                if (ink)
                {
                    outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.95f);
                    outline.effectDistance = new Vector2(2.5f, -2.5f);
                }
            }
            return label;
        }

        /// <summary>
        /// A tag on <paramref name="host"/>: an optional hard drop shadow (the same tag shape in dark ink, shifted down and right),
        /// then the plate itself. Returns the plate, the graphic a Button on <paramref name="host"/> should target.
        /// </summary>
        static Image Tag(RectTransform host, Sprite plate, int cut, bool raycast, float shadow = 0.5f, float offset = 6f)
        {
            if (shadow > 0f)
            {
                Image shade = Ui.MakeImage("Shade", host, new Color(0f, 0.02f, 0.03f, shadow));
                shade.sprite = MenuArt.Solid(cut);
                shade.type = Image.Type.Sliced;
                Stretch(shade.rectTransform, Vector2.zero, Vector2.one);
                shade.rectTransform.offsetMin = new Vector2(offset, -offset);
                shade.rectTransform.offsetMax = new Vector2(offset, -offset);
            }
            Image image = Ui.MakeImage("Plate", host, Color.white, false, raycast);
            image.sprite = plate;
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform, Vector2.zero, Vector2.one);
            return image;
        }

        /// <summary>A white chip plate (lighter at the top) tinted <paramref name="tint"/>.</summary>
        static Image ChipImage(string name, Transform parent, Color tint, int cut)
        {
            Image image = Ui.MakeImage(name, parent, tint);
            image.sprite = MenuArt.Chip(cut);
            image.type = Image.Type.Sliced;
            return image;
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

        static Texture2D s_rampV;

        /// <summary>A code-drawn alpha ramp (1 at the top to 0 at the bottom) for shading washes.</summary>
        static Texture2D Ramp(bool horizontal)
        {
            if (s_rampV != null) return s_rampV;
            const int n = 64;
            var tex = new Texture2D(1, n, TextureFormat.RGBA32, false)
            {
                name = "KalyeahRampV",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                tex.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, 1f - t)));
            }
            tex.Apply(false, true);
            s_rampV = tex;
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
