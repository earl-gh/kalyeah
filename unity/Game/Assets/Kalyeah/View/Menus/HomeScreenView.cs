using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Home (docs/SPEC.md §8, owner layout read like CODM and Wild Rift; 1280x720 reference, SafeArea-pinned, hit areas &gt;= 48 dp).
    /// The selected kid stands in 3D (<see cref="HeroStage"/>) in front of a rendered Palengke stall (<c>ui/home/stall</c>, shot
    /// through the hero camera so both share one perspective), with dust drifting in the light. Profile (name only: rank shows
    /// only where Ranked is chosen) top left, settings top right; a bottom bar to Kids and the Codex; the kid's name plate at
    /// their feet; and on the right a mode card (the mode's name and how it feels, never team size, scoring or map rules) over
    /// one big gold PLAY. Only real content: the pass, events, store, friends and party join when they are built (§12).
    /// The connection check runs before home (<see cref="CheckConnectionAtLaunch"/>) and again on PLAY; offline is never a mode.
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Muted = new Color(0.80f, 0.84f, 0.90f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.80f, 0.80f, 0.84f, 1f);
        static readonly Color Shade = new Color(0.02f, 0.035f, 0.07f, 1f);

        const float TopBarHeight = 112f;
        const float HitSize = 96f;
        const float Gutter = 20f;
        const float PlayHeight = 124f;
        const float ModeHeight = 176f;
        const float ColumnGap = 20f;
        const float NavItemWidth = 112f;
        const float LeftWidth = 340f;
        const float CardHeight = 96f;
        const float NavHeight = 96f;
        const float WeaveTile = 96f;
        const int MoteCount = 18;

        // the stall plate is rendered through HeroStage's camera opened from 26 to 40 degrees tall, at 2400 by 1000
        const float PlateOverHero = 1.5765f; // tan(20 deg) / tan(13 deg)
        const float PlateAspect = 2.4f;

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

        // the scene behind the UI
        RawImage _stall;
        RectTransform _stallRect;
        RectTransform _heroImageRect;
        RectTransform _heroShadowRect;
        RectTransform _shadeLeft;
        RectTransform _shadeRight;
        readonly RectTransform[] _moteRects = new RectTransform[MoteCount];
        readonly Image[] _moteImages = new Image[MoteCount];
        Vector2 _areaSize = new Vector2(1280f, 720f);

        // bars and columns
        RectTransform _topBar;
        RectTransform _hubBody;
        RectTransform _bottomNav;
        RectTransform _heroZone;
        RectTransform _modeColumn;
        RectTransform _modeZone;
        RectTransform _playZone;
        RectTransform _kidPlate;
        RectTransform _leftColumn;

        RawImage _heroImage;
        RawImage _avatarImage;
        Text _playerName;
        Text _kidName;
        Image _kidRoleIcon;

        // PLAY
        RectTransform _playClip;
        RectTransform _playShine;
        Image _playGlow;

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
            BuildHero(Root);
            BuildShades(Root);

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
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toast.gameObject.SetActive(false);
            if (SettingsStore.Current.ReduceMotion) return;
            PlaceMotes();
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
                Sheets.HistoryRequested -= OpenHistory;
                Sheets.Dispose();
                Sheets = null;
            }
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        // ---------------------------------------------------------------- the scene

        void BuildBackdrop(RectTransform parent)
        {
            RectTransform bg = Ui.Fill("Backdrop", parent);
            Image floor = bg.gameObject.AddComponent<Image>();
            floor.color = Shade;
            floor.raycastTarget = false;

            // the Palengke stall: Karding's chalk map on the roll-up shutter, sun through the tin roof (docs/MAPS.md)
            _stall = MakeRaw("Stall", bg, KalyeahResources.Texture("ui/home/stall"));
            _stallRect = _stall.rectTransform;
            _stallRect.anchorMin = _stallRect.anchorMax = Vector2.zero;
            _stallRect.pivot = new Vector2(0.5f, 0.5f);
            _stall.enabled = _stall.texture != null;

            // dust in the light: small cream motes drifting down (a fixed frame when motion is reduced)
            RectTransform dust = Ui.Fill("Dust", bg);
            dust.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < MoteCount; i++)
            {
                Image mote = Ui.MakeImage("Mote" + i, dust, Cream, round: true);
                float size = 3f + (i * 7 % 4);
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
            Image shadow = Ui.MakeImage("HeroShadow", parent, new Color(0f, 0f, 0f, 0.55f), round: true);
            _heroShadowRect = shadow.rectTransform;
            AnchorLowerLeft(_heroShadowRect);
            _heroShadowRect.sizeDelta = new Vector2(320f, 56f);

            _heroImage = MakeRaw("Hero", parent, _stage.Texture);
            _heroImageRect = _heroImage.rectTransform;
            _heroImageRect.anchorMin = _heroImageRect.anchorMax = new Vector2(0f, 0f);
            _heroImageRect.pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>Readability comes from light falling off toward the edges, not from panels behind the text.</summary>
        void BuildShades(RectTransform parent)
        {
            RectTransform shades = Ui.Fill("Shades", parent);
            // Ramp's first row is its bottom (dark there, clear at the top): flip it for the top shade
            RawImage top = MakeRaw("Top", shades, Ramp(false));
            top.color = new Color(Shade.r, Shade.g, Shade.b, 0.75f);
            top.uvRect = new Rect(0f, 1f, 1f, -1f);
            Stretch(top.rectTransform, new Vector2(0f, 0.78f), Vector2.one);
            RawImage bottom = MakeRaw("Bottom", shades, Ramp(false));
            bottom.color = new Color(Shade.r, Shade.g, Shade.b, 0.92f);
            Stretch(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0.36f));
            RawImage left = MakeRaw("Left", shades, Ramp(true));
            left.color = new Color(Shade.r, Shade.g, Shade.b, 0.6f);
            _shadeLeft = left.rectTransform;
            Stretch(_shadeLeft, Vector2.zero, new Vector2(0f, 1f));
            _shadeLeft.pivot = new Vector2(0f, 0.5f);
            RawImage right = MakeRaw("Right", shades, Ramp(true));
            right.color = new Color(Shade.r, Shade.g, Shade.b, 0.85f);
            right.uvRect = new Rect(1f, 0f, -1f, 1f);
            _shadeRight = right.rectTransform;
            Stretch(_shadeRight, new Vector2(1f, 0f), Vector2.one);
            _shadeRight.pivot = new Vector2(1f, 0.5f);
        }

        // ---------------------------------------------------------------- bars

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));

            // profile: the player's picture in a gold ring with a level badge, and their name (one button: opens the profile)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(380f, HitSize));
            Image profileHit = profile.gameObject.AddComponent<Image>();
            profileHit.color = Clear;
            ProfileButton = AttachButton(profile.gameObject, profileHit, () => { TapFeedback(); Sheets?.OpenProfile(); });

            RectTransform avatar = Ui.Panel("Avatar", profile, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            Image ring = Ui.MakeImage("Ring", avatar, Gold, round: true);
            Stretch(ring.rectTransform, Vector2.zero, Vector2.one);
            Image well = Ui.MakeImage("Well", avatar, MenuArt.NightMid, round: true);
            Stretch(well.rectTransform, Vector2.zero, Vector2.one);
            well.rectTransform.offsetMin = new Vector2(4f, 4f);
            well.rectTransform.offsetMax = new Vector2(-4f, -4f);
            well.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _avatarImage = MakeRaw("Photo", well.rectTransform, null);
            Image level = Ui.MakeImage("Level", avatar, Gold, round: true);
            Place(level.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(6f, -4f), new Vector2(40f, 40f));
            Text levelText = Label("Text", level.rectTransform, "9", 24, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(levelText.rectTransform, Vector2.zero, Vector2.one);

            _playerName = Label("Name", profile, "", 34, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(98f, 0f), new Vector2(-98f, 48f));
            _playerName.rectTransform.pivot = new Vector2(0f, 0.5f);
            _playerName.resizeTextForBestFit = true;
            _playerName.resizeTextMinSize = 22;
            _playerName.resizeTextMaxSize = 34;

            // settings (Donate lives there, docs/SPEC.md §8)
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(HitSize, HitSize));
            Image settingsPlate = RoundPlate(settings, MenuArt.RoundGlass(40));
            Image gear = Ui.MakeImage("Gear", settings, Cream);
            gear.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Gear);
            Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            AttachButton(settings.gameObject, settingsPlate, () => Navigate(MenuStack.SettingsId));

            // currencies (sample numbers until accounts and the store exist, docs/SPEC.md §12)
            RectTransform chips = Ui.Panel("Currencies", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(Gutter + HitSize + 16f), 0f), new Vector2(400f, 64f));
            MakeCurrencyChip(chips, 0, "12,450", MenuArt.Gold, "barya", () => ComingSoon("Barya: earned in matches and tasks"));
            MakeCurrencyChip(chips, 1, "80", new Color(0.35f, 0.76f, 0.94f, 1f), "diamonds", () => ComingSoon("Diamonds: the shop opens with accounts"));
        }

        void MakeCurrencyChip(RectTransform row, int fromRight, string value, Color coin, string name, Action onClick)
        {
            float w = fromRight == 0 ? 190f : 160f;
            float x = fromRight == 0 ? -(160f + 12f) : 0f;
            // the chip draws 64 tall inside a 96 tall hit area (48 dp)
            RectTransform chip = Ui.Panel("Chip_" + name, row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, HitSize));
            Image plate = RoundPlate(chip, MenuArt.RoundGlass(32));
            plate.rectTransform.offsetMin = new Vector2(0f, 16f);
            plate.rectTransform.offsetMax = new Vector2(0f, -16f);
            Image disc = Ui.MakeImage("Coin", chip, coin, round: true);
            Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(44f, 44f));
            disc.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text v = Label("Value", chip, value, 28, Cream, TextAnchor.MiddleLeft, display: true);
            Stretch(v.rectTransform, Vector2.zero, Vector2.one);
            v.rectTransform.offsetMin = new Vector2(62f, 0f);
            v.rectTransform.offsetMax = new Vector2(-12f, 0f);
            AttachButton(chip.gameObject, plate, onClick);
        }

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // 0a. left column (CODM and Wild Rift): the pass, the event and the daily gift. Sample content until those systems
            // exist; tapping each says so (docs/SPEC.md §12)
            _leftColumn = Ui.Panel("LeftColumn", _hubBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(LeftWidth, 3f * CardHeight + 2f * CardGap));
            MakeLeftCard(0, "Pass", "PASS  ·  1/3", "Rescue 3 teammates", null, () => ComingSoon("The battle pass comes with accounts"));
            MakeLeftCard(1, "Event", "PASKO EVENT", "Ends in 12 days", new Color(0.86f, 0.28f, 0.34f, 1f), () => ComingSoon("Events come with accounts"));
            MakeLeftCard(2, "Daily", "DAY 3 GIFT", "100 barya", Gold, () => ComingSoon("Daily gifts come with accounts"));

            // 0b. bottom bar (Wild Rift): Kids, Store and Friends; the store and friends say so until they exist
            _bottomNav = Ui.Panel("BottomNav", _hubBody, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(NavItemWidth * 3f, NavHeight));
            MakeNavItem(_bottomNav, 0, "Nav_Kids", "KIDS", MenuArt.GlyphKind.Kid, () => Navigate(MenuStack.KidsId));
            MakeNavItem(_bottomNav, 1, "Nav_Store", "STORE", MenuArt.GlyphKind.Bag, () => ComingSoon("The store comes with accounts"));
            MakeNavItem(_bottomNav, 2, "Nav_Friends", "FRIENDS", MenuArt.GlyphKind.People, () => ComingSoon("Friends and party come with accounts"), badge: "3");

            // 1. hero zone: the kid's name plate at their feet (tap for their kid page)
            _heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _kidPlate = Ui.Panel("KidPlate", _heroZone, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(300f, HitSize));
            Image plateHit = _kidPlate.gameObject.AddComponent<Image>();
            plateHit.color = Clear;
            RectTransform pill = Ui.Panel("Pill", _kidPlate, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 64f));
            Image pillPlate = Ui.MakeImage("Plate", pill, Color.white);
            pillPlate.sprite = MenuArt.Round(new Color(Shade.r, Shade.g, Shade.b, 0.55f), new Color(Shade.r, Shade.g, Shade.b, 0.55f), new Color(1f, 1f, 1f, 0.12f), 32, 2, 0f);
            pillPlate.type = Image.Type.Sliced;
            Stretch(pillPlate.rectTransform, Vector2.zero, Vector2.one);
            Image roleDisc = Ui.MakeImage("RoleDisc", pill, Gold, round: true);
            Place(roleDisc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(44f, 44f));
            roleDisc.rectTransform.pivot = new Vector2(0f, 0.5f);
            _kidRoleIcon = Ui.MakeImage("RoleIcon", roleDisc.rectTransform, Ink);
            Place(_kidRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            _kidName = Label("KidName", pill, "", 34, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Stretch(_kidName.rectTransform, Vector2.zero, Vector2.one);
            _kidName.rectTransform.offsetMin = new Vector2(68f, 0f);
            _kidName.rectTransform.offsetMax = new Vector2(-40f, 0f);
            _kidName.resizeTextForBestFit = true;
            _kidName.resizeTextMinSize = 22;
            _kidName.resizeTextMaxSize = 34;
            Image chevron = Ui.MakeImage("Chevron", pill, Gold);
            chevron.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(24f, 24f));
            chevron.rectTransform.pivot = new Vector2(1f, 0.5f);
            KidButton = AttachButton(_kidPlate.gameObject, plateHit, () => Navigate(MenuStack.KidPageId));

            // 2. right column: the mode card (tap to change) over one big gold PLAY
            _modeColumn = Ui.Panel("ModeColumn", _hubBody, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(380f, 0f));
            BuildModeCard(_modeColumn);
            BuildPlay(_modeColumn);

            BuildToast(parent);
        }

        /// <summary>One left-column card: a ring (the pass) or a round badge, a gold title and a line, on dark glass.</summary>
        void MakeLeftCard(int index, string name, string title, string line, Color? badge, Action onClick)
        {
            RectTransform card = Ui.Panel("Card_" + name, _leftColumn, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * (CardHeight + CardGap)), new Vector2(0f, CardHeight));
            Image plate = RoundPlate(card, MenuArt.RoundGlass(40));

            RectTransform art = Ui.Panel("Art", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(72f, 72f));
            if (badge == null)
            {
                Image track = Ui.MakeImage("Track", art, new Color(1f, 1f, 1f, 0.14f), round: true);
                track.sprite = KomiksUi.RingSprite(128, 7);
                Stretch(track.rectTransform, Vector2.zero, Vector2.one);
                Image arc = Ui.MakeImage("Arc", art, Gold);
                arc.sprite = KomiksUi.RingSprite(128, 7);
                arc.type = Image.Type.Filled;
                arc.fillMethod = Image.FillMethod.Radial360;
                arc.fillOrigin = (int)Image.Origin360.Top;
                arc.fillClockwise = true;
                arc.fillAmount = 0.4f;
                Stretch(arc.rectTransform, Vector2.zero, Vector2.one);
                Text level = Label("Level", art, "12", 28, Cream, TextAnchor.MiddleCenter, display: true);
                Stretch(level.rectTransform, Vector2.zero, Vector2.one);
            }
            else
            {
                Image disc = Ui.MakeImage("Badge", art, badge.Value, round: true);
                Stretch(disc.rectTransform, Vector2.zero, Vector2.one);
                Image mark = Ui.MakeImage("Mark", art, new Color(1f, 0.9f, 0.66f, 1f));
                mark.sprite = MenuArt.Glyph(index == 1 ? MenuArt.GlyphKind.Star : MenuArt.GlyphKind.Diamond);
                Place(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            }

            Text t = Label("Title", card, title, 24, Gold, TextAnchor.LowerLeft, display: true);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(96f, 2f), new Vector2(-108f, 30f));
            t.rectTransform.pivot = new Vector2(0f, 0f);
            Text l = Label("Line", card, line, 22, Cream, TextAnchor.UpperLeft);
            Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(96f, -4f), new Vector2(-108f, 30f));
            l.rectTransform.pivot = new Vector2(0f, 1f);
            AttachButton(card.gameObject, plate, onClick);
        }

        /// <summary>One bottom-bar item: a line icon over its label, with a thin divider before every item but the first.</summary>
        void MakeNavItem(RectTransform bar, int index, string name, string title, MenuArt.GlyphKind glyph, Action onClick, string badge = null)
        {
            RectTransform item = Ui.Panel(name, bar, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(index * NavItemWidth, 0f), new Vector2(NavItemWidth, 0f));
            Image hit = item.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Image icon = Ui.MakeImage("Icon", item, Cream);
            icon.sprite = MenuArt.Glyph(glyph);
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(40f, 40f));
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            Text label = Label("Label", item, title, 22, Cream, TextAnchor.LowerCenter, display: true, ink: true);
            Stretch(label.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            if (index > 0)
            {
                Image divider = Ui.MakeImage("Divider", item, new Color(Cream.r, Cream.g, Cream.b, 0.2f));
                Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 40f));
            }
            if (badge != null)
            {
                Image dot = Ui.MakeImage("Badge", item, new Color(0.36f, 0.78f, 0.58f, 1f), round: true);
                Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(22f, -4f), new Vector2(32f, 32f));
                Text count = Label("Count", dot.rectTransform, badge, 22, Ink, TextAnchor.MiddleCenter, display: true);
                Stretch(count.rectTransform, Vector2.zero, Vector2.one);
            }
            AttachButton(item.gameObject, hit, onClick);
        }

        void BuildModeCard(RectTransform column)
        {
            _modeZone = Ui.Panel("ModeZone", column, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PlayHeight + ColumnGap + 8f), new Vector2(0f, ModeHeight));
            Image plate = RoundPlate(_modeZone, MenuArt.RoundGlass(26));

            // a gold bar on the left edge marks the choice
            Image bar = Ui.MakeImage("Bar", _modeZone, Gold);
            Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(5f, -56f));
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text kicker = Label("Kicker", _modeZone, "MODE", 22, Gold, TextAnchor.UpperLeft, display: true);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(-64f, 28f));
            kicker.rectTransform.pivot = new Vector2(0.5f, 1f);

            _modeName = Label("ModeName", _modeZone, "", 72, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Place(_modeName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -52f), new Vector2(-120f, 76f));
            _modeName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _modeName.resizeTextForBestFit = true;
            _modeName.resizeTextMinSize = 40;
            _modeName.resizeTextMaxSize = 72;

            _modeDetail = Label("ModeDetail", _modeZone, "", 22, Muted, TextAnchor.UpperLeft);
            Place(_modeDetail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(-64f, 30f));
            _modeDetail.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image chevronDisc = Ui.MakeImage("ChevronDisc", _modeZone, new Color(1f, 1f, 1f, 0.12f), round: true);
            Place(chevronDisc.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 10f), new Vector2(52f, 52f));
            chevronDisc.rectTransform.pivot = new Vector2(1f, 0.5f);
            Image chevron = Ui.MakeImage("Chevron", chevronDisc.rectTransform, Cream);
            chevron.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            ModeButton = AttachButton(_modeZone.gameObject, plate, CycleHomeMode);
            ShowHomeMode();
        }

        void BuildPlay(RectTransform column)
        {
            _playZone = Ui.Panel("PlayZone", column, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PlayHeight));

            // a soft gold glow behind the button that breathes
            _playGlow = Ui.MakeImage("Glow", _playZone, new Color(Gold.r, Gold.g, Gold.b, 0.35f));
            _playGlow.sprite = MenuArt.Glow();
            Stretch(_playGlow.rectTransform, Vector2.zero, Vector2.one);
            _playGlow.rectTransform.offsetMin = new Vector2(-40f, -40f);
            _playGlow.rectTransform.offsetMax = new Vector2(40f, 40f);

            Image plate = RoundPlate(_playZone, MenuArt.RoundGold(24));

            // a light sweep across the button now and then
            _playClip = Ui.Fill("ShineClip", _playZone);
            _playClip.offsetMin = new Vector2(22f, 10f);
            _playClip.offsetMax = new Vector2(-22f, -10f);
            _playClip.gameObject.AddComponent<RectMask2D>();
            Image shine = Ui.MakeImage("Shine", _playClip, new Color(1f, 1f, 1f, 0.55f));
            shine.sprite = MenuArt.Glow();
            _playShine = shine.rectTransform;
            _playShine.anchorMin = _playShine.anchorMax = new Vector2(0f, 0.5f);
            _playShine.sizeDelta = new Vector2(100f, 320f);
            _playShine.localRotation = Quaternion.Euler(0f, 0f, 20f);
            _playShine.anchoredPosition = new Vector2(-200f, 0f);

            Image triangle = Ui.MakeImage("Triangle", _playZone, Ink);
            triangle.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Play);
            Place(triangle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(52f, 52f));
            triangle.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text label = Label("Label", _playZone, "PLAY", 70, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(96f, 0f);
            label.rectTransform.offsetMax = new Vector2(-40f, 0f);
            PlayButton = AttachButton(_playZone.gameObject, plate, PressPlay);
        }

        /// <summary>A rounded panel (no cut corners, no hard shadow): the plate of every card on the new home.</summary>
        static Image RoundPlate(RectTransform host, Sprite sprite)
        {
            Image image = Ui.MakeImage("Plate", host, Color.white, false, true);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform, Vector2.zero, Vector2.one);
            return image;
        }

        // ---------------------------------------------------------------- toast

        RectTransform _toast;
        Text _toastText;
        float _toastUntil;

        /// <summary>A short message at the top centre (Wild Rift style), for panels whose systems are not built yet.</summary>
        void BuildToast(RectTransform parent)
        {
            _toast = Ui.Panel("Toast", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TopBarHeight + 4f)), new Vector2(640f, 64f));
            Image plate = RoundPlate(_toast, MenuArt.Round(new Color(0.03f, 0.05f, 0.09f, 0.9f), new Color(0.03f, 0.05f, 0.09f, 0.9f), new Color(1f, 1f, 1f, 0.16f), 32, 2, 0f));
            plate.raycastTarget = false;
            _toastText = Label("Text", _toast, "", 24, Cream, TextAnchor.MiddleCenter, display: false);
            Stretch(_toastText.rectTransform, Vector2.zero, Vector2.one);
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

        // ---------------------------------------------------------------- offline

        void BuildOfflinePrompt()
        {
            _offlinePrompt = Ui.Fill("OfflinePrompt", Root);
            Image scrim = _offlinePrompt.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.01f, 0.04f, 0.06f, 0.84f);
            scrim.raycastTarget = true;

            RectTransform card = Ui.Panel("Card", _offlinePrompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 310f));
            Tag(card, MenuArt.Glass(24), 24, false, 0.6f, 8f);

            Text title = Label("Title", card, "YOU'RE OFFLINE", 48, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Stretch(title.rectTransform, new Vector2(0f, 1f), Vector2.one);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-72f, 60f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            Image rule = Ui.MakeImage("Rule", card, Gold);
            Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -94f), new Vector2(84f, 5f));
            rule.rectTransform.pivot = new Vector2(0f, 1f);
            Text body = Label("Body", card, "Play offline now, or retry once you are connected.", 22, Muted, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0f, 1f), Vector2.one);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.sizeDelta = new Vector2(-72f, 32f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -112f);

            RectTransform actions = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-72f, HitSize));
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
            label.resizeTextMinSize = 22;
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
            label.resizeTextMinSize = 22;
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

            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (_kidName != null)
                _kidName.text = (ReadCharacterField(kidId, "display_name") ?? Capitalize(kidId)).ToUpperInvariant();
            if (_kidRoleIcon != null)
                _kidRoleIcon.sprite = MenuArt.Glyph(RoleGlyph(ParseRole(ReadCharacterField(kidId, "description"))));
            _stage.SetKid(kidId);
            _stage.RenderNow();
            RefreshLayout(); // the kid's feet height changes with the kid

            if (_difficultyLabel != null)
                _difficultyLabel.text = SettingsStore.NormalizeDifficulty(session.BotDifficulty).ToUpperInvariant();
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
        /// Lays out the top bar, the three hub columns (bottom bar, kid, mode) and the scene. The kid stands in the space between
        /// the bottom bar and the mode column; the stall plate is scaled and centred on the kid's picture so both keep one
        /// perspective, and grows only when a screen is taller or wider than the plate.
        /// </summary>
        public void RefreshLayout()
        {
            if (_disposed || _topBar == null || _hubBody == null) return;
            Rect safe = SafeRoot.rect;
            Rect full = Root.rect;
            float width = safe.width;
            _areaSize = new Vector2(Mathf.Max(1f, full.width), Mathf.Max(1f, full.height));

            _hubBody.offsetMin = new Vector2(Gutter, 16f);
            _hubBody.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            float bodyWidth = Mathf.Max(0f, width - 2f * Gutter);
            float rightWidth = Mathf.Clamp(bodyWidth * 0.3f, 340f, 400f);
            float navWidth = LeftWidth;
            if (_modeColumn != null) _modeColumn.sizeDelta = new Vector2(rightWidth, 0f);
            if (_bottomNav != null) _bottomNav.anchoredPosition = Vector2.zero;
            float heroLeft = navWidth + ColumnGap;
            float heroRight = bodyWidth - rightWidth - ColumnGap;
            if (_heroZone != null)
            {
                _heroZone.anchoredPosition = new Vector2(heroLeft, 0f);
                _heroZone.sizeDelta = new Vector2(Mathf.Max(0f, heroRight - heroLeft), 0f);
            }
            if (_kidPlate != null)
            {
                _kidPlate.anchoredPosition = new Vector2(0f, NavHeight + 12f);
                _kidPlate.sizeDelta = new Vector2(Mathf.Min(300f, Mathf.Max(200f, heroRight - heroLeft)), HitSize);
            }

            // the kid: most of the height, centred on the space between the columns (shifted right of the bar), feet on the plate
            // zoomed in like Wild Rift, Mobile Legends and CODM: thigh up, the head near the top, the feet below the screen. The kid
            // is KidInTexture of the picture's height; the picture is sized so the visible top 60% of the kid fills 0.67 of the screen.
            const float KidInTexture = 0.755f, HeadTop = 0.93f, VisibleKid = 0.67f / 0.6f;
            float heroHeight = full.height * VisibleKid / KidInTexture;
            float heroWidth = heroHeight * 0.75f;
            if (_heroImageRect != null && _heroZone != null)
            {
                Vector3 zoneCentre = Root.InverseTransformPoint(_heroZone.TransformPoint(_heroZone.rect.center));
                float heroCentre = zoneCentre.x - full.xMin;
                float feetY = HeadTop * full.height - KidInTexture * heroHeight;
                _heroImageRect.sizeDelta = new Vector2(heroWidth, heroHeight);
                _heroImageRect.anchoredPosition = new Vector2(heroCentre, feetY - heroHeight * _stage.FeetViewportY);
            }
            PlaceScene();
            PlaceMotes();
        }

        /// <summary>The stall plate follows the kid's picture; the contact shadow sits under the feet; the side shades frame the UI.</summary>
        void PlaceScene()
        {
            if (_heroImageRect == null) return;
            Vector2 size = _heroImageRect.sizeDelta;
            Vector2 pos = _heroImageRect.anchoredPosition;
            Vector2 centre = new Vector2(pos.x, pos.y + size.y * 0.5f);

            if (_stallRect != null)
            {
                float plateH = size.y * PlateOverHero;
                plateH = Mathf.Max(plateH, 2f * Mathf.Max(centre.y, _areaSize.y - centre.y));
                float plateW = plateH * PlateAspect;
                float needW = 2f * Mathf.Max(centre.x, _areaSize.x - centre.x);
                if (plateW < needW) { plateW = needW; plateH = plateW / PlateAspect; }
                SetRect(_stallRect, centre, new Vector2(plateW, plateH));
            }
            if (_heroShadowRect != null)
                SetRect(_heroShadowRect, new Vector2(pos.x, pos.y + size.y * _stage.FeetViewportY), new Vector2(size.x * 0.44f, size.x * 0.07f));
            if (_shadeLeft != null) _shadeLeft.sizeDelta = new Vector2(_areaSize.x * 0.42f, 0f);
            if (_shadeRight != null) _shadeRight.sizeDelta = new Vector2(_areaSize.x * 0.45f, 0f);
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ---------------------------------------------------------------- motion

        static readonly float[] MoteSeed = { 0.07f, 0.31f, 0.52f, 0.76f, 0.18f, 0.64f, 0.93f, 0.41f, 0.27f, 0.85f, 0.58f, 0.12f, 0.71f, 0.46f, 0.36f, 0.88f, 0.03f, 0.67f };

        /// <summary>Dust drifting slowly down through the light, by the clock (a fixed frame when motion is reduced or nothing ticks).</summary>
        void PlaceMotes()
        {
            bool still = !Application.isPlaying || SettingsStore.Current.ReduceMotion;
            for (int i = 0; i < MoteCount; i++)
            {
                if (_moteRects[i] == null) continue;
                float fall = Mathf.Repeat(MoteSeed[i] + (still ? 0f : _time * (0.006f + 0.003f * (i % 3))), 1f);
                float x = (0.3f + 0.4f * Mathf.Repeat(MoteSeed[(i + 7) % MoteCount] * 1.9f + i * 0.113f, 1f)) * _areaSize.x;
                x += still ? 0f : Mathf.Sin(_time * 0.4f + i) * 10f;
                _moteRects[i].anchoredPosition = new Vector2(x, (1f - fall) * _areaSize.y);
                Color c = _moteImages[i].color;
                c.a = Mathf.Sin(fall * Mathf.PI) * 0.45f;
                _moteImages[i].color = c;
            }
        }

        void AnimatePlay()
        {
            if (_playGlow != null)
            {
                Color g = _playGlow.color;
                g.a = 0.22f + 0.13f * (0.5f + 0.5f * Mathf.Sin(_time * 2.2f));
                _playGlow.color = g;
            }
            if (_playShine != null && _playClip != null)
            {
                // a sweep every 5.5 s: across in about a second, then a rest
                float phase = Mathf.Repeat(_time, 5.5f) / 1.1f;
                float travel = _playClip.rect.width + 240f;
                _playShine.anchoredPosition = new Vector2(-120f + Mathf.Clamp01(phase) * travel, 0f);
            }
        }

        // ---------------------------------------------------------------- flow

        /// <summary>The modes Home's PLAY starts today (docs/SPEC.md §4); Ranked and private rooms join when they are built.</summary>
        enum HomeMode { Blitz, Quick }

        HomeMode _homeMode;
        Text _modeName;
        Text _modeDetail;

        /// <summary>Switches between Blitz and Quick.</summary>
        public Button ModeButton { get; private set; }

        /// <summary>The mode's line on the card: how it feels, never team size, scoring or map rules (owner rule).</summary>
        public string ModeLine => _modeDetail != null ? _modeDetail.text : "";

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
            if (_modeName != null) _modeName.text = _homeMode.ToString().ToUpperInvariant();
            if (_modeDetail != null)
                _modeDetail.text = _homeMode == HomeMode.Blitz ? "Short rounds, fast wins" : "The full match";
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
        static Texture2D s_rampH;

        /// <summary>A code-drawn alpha ramp for shading washes: 1 at the bottom row to 0 at the top, or 1 at the left to 0 at the right.</summary>
        static Texture2D Ramp(bool horizontal)
        {
            Texture2D cached = horizontal ? s_rampH : s_rampV;
            if (cached != null) return cached;
            const int n = 64;
            var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
            {
                name = horizontal ? "KalyeahRampH" : "KalyeahRampV",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var c = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, 1f - t));
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

        static string Capitalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            return s.Length == 1 ? s.ToUpperInvariant() : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
