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
    /// only where Ranked is chosen) top left, currencies and settings top right; sleek mission rail top left; frosted bottom nav
    /// dock; grounded tactical hero pedestal nameplate at the kid's feet; and an esports queue mode card over a luminous gold PLAY.
    /// Only real content plus the owner-approved sample panels (pass, event, daily gift, currencies, store, friends) that announce
    /// themselves when tapped (§12). The connection check runs before home (<see cref="CheckConnectionAtLaunch"/>) and on PLAY;
    /// offline is never a mode on Home.
    /// </summary>
    public sealed class HomeScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color PressTint = new Color(0.84f, 0.86f, 0.90f, 1f);
        static readonly Color Shade = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color EventCrimson = new Color(0.92f, 0.27f, 0.34f, 1f);

        const float TopBarHeight = 104f;
        const float HitSize = 96f;
        const float Gutter = 20f;
        const float PlayHeight = 116f;
        const float ModeHeight = 166f;
        const float ModeToPlayGap = 16f;
        const float ColumnGap = 16f;
        const float NavItemWidth = 98f;
        const float NavItemGap = 2f;
        const float NavDockPadX = 4f;
        const float BottomNavWidth = NavItemWidth * 3f + NavItemGap * 2f + NavDockPadX * 2f; // 306f
        const float DefaultLeftWidth = 300f;
        const float CardHeight = HitSize;
        const float CardGap = 4f;
        const float NavHeight = HitSize;
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
        Text _kidMeta;
        Text _kidName;
        Image _kidRoleIcon;

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
            _entry = 0f;
            _entryDone = false;
            Refresh();
            RefreshLayout();
        }

        // entry: the five HUD clusters ease in smoothly in Play mode, then the Palengke stall drifts subtly behind the kid
        float _entry;
        bool _entryDone;
        CanvasGroup[] _groups;
        Vector2 _stallBase;

        static CanvasGroup Group(RectTransform rect)
        {
            if (rect == null) return null;
            return rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();
        }

        void AnimateEntry(float dt)
        {
            bool still = SettingsStore.Current.ReduceMotion;
            if (!_entryDone)
            {
                if (_groups == null) _groups = new[] { Group(_topBar), Group(_leftColumn), Group(_bottomNav), Group(_modeColumn), Group(_heroZone) };
                _entry = still ? 10f : _entry + dt;
                bool allDone = true;
                for (int i = 0; i < _groups.Length; i++)
                {
                    if (_groups[i] == null) continue;
                    float t = Mathf.Clamp01((_entry - 0.08f - 0.09f * i) / 0.36f);
                    if (t < 1f) allDone = false;
                    float eased = t >= 1f ? 1f : 1f - (1f - t) * (1f - t) * (1f - t);
                    _groups[i].alpha = eased;
                    _groups[i].transform.localScale = t >= 1f ? Vector3.one : Vector3.one * (0.96f + 0.04f * eased);
                }
                if (allDone) _entryDone = true;
            }
            if (!still && _stallRect != null)
                _stallRect.anchoredPosition = _stallBase + new Vector2(Mathf.Sin(_time * 0.2f) * 12f, Mathf.Sin(_time * 0.13f) * 5f);
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
            AnimateEntry(dt);
            if (SettingsStore.Current.ReduceMotion) return;
            PlaceMotes();
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
            Image shadow = Ui.MakeImage("HeroShadow", parent, new Color(1f, 1f, 1f, 0.52f));
            shadow.sprite = MenuArt.PedestalGlow();
            _heroShadowRect = shadow.rectTransform;
            AnchorLowerLeft(_heroShadowRect);
            _heroShadowRect.sizeDelta = new Vector2(360f, 76f);

            _heroImage = MakeRaw("Hero", parent, _stage.Texture);
            _heroImageRect = _heroImage.rectTransform;
            _heroImageRect.anchorMin = _heroImageRect.anchorMax = new Vector2(0f, 0f);
            _heroImageRect.pivot = new Vector2(0.5f, 0f);
            _stage.AttachOrbitDrag(_heroImageRect, 0.45f);
        }

        /// <summary>Readability comes from light falling off toward the edges, leaving the center chalk map and 3D kid luminous.</summary>
        void BuildShades(RectTransform parent)
        {
            RectTransform shades = Ui.Fill("Shades", parent);
            // Ramp's first row is its bottom (dark there, clear at the top): flip it for the top shade
            RawImage top = MakeRaw("Top", shades, Ramp(false));
            top.color = new Color(Shade.r, Shade.g, Shade.b, 0.74f);
            top.uvRect = new Rect(0f, 1f, 1f, -1f);
            Stretch(top.rectTransform, new Vector2(0f, 0.78f), Vector2.one);
            RawImage bottom = MakeRaw("Bottom", shades, Ramp(false));
            bottom.color = new Color(Shade.r, Shade.g, Shade.b, 0.90f);
            Stretch(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0.34f));
            RawImage left = MakeRaw("Left", shades, Ramp(true));
            left.color = new Color(Shade.r, Shade.g, Shade.b, 0.54f);
            _shadeLeft = left.rectTransform;
            Stretch(_shadeLeft, Vector2.zero, new Vector2(0f, 1f));
            _shadeLeft.pivot = new Vector2(0f, 0.5f);
            RawImage right = MakeRaw("Right", shades, Ramp(true));
            right.color = new Color(Shade.r, Shade.g, Shade.b, 0.84f);
            right.uvRect = new Rect(1f, 0f, -1f, 1f);
            _shadeRight = right.rectTransform;
            Stretch(_shadeRight, new Vector2(1f, 0f), Vector2.one);
            _shadeRight.pivot = new Vector2(1f, 0.5f);
        }

        // ---------------------------------------------------------------- bars

        void BuildTopBar(RectTransform parent)
        {
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));

            // 1. Profile badge (one >= 48 dp hit button: framed gold-rim avatar + level crest medallion + uppercase IGN + edit cue; NO rank per SPEC §8)
            RectTransform profile = Ui.Panel("Profile", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(340f, HitSize));
            Image profileHit = profile.gameObject.AddComponent<Image>();
            profileHit.color = Clear;
            profileHit.raycastTarget = true;

            RectTransform profilePlateHost = Ui.Fill("PlateHost", profile);
            profilePlateHost.offsetMin = new Vector2(22f, 11f);
            profilePlateHost.offsetMax = new Vector2(0f, -11f);
            Image profilePlate = Ui.MakeSliced("Plate", profilePlateHost, MenuArt.ChamferGlass(12), Color.white);
            Image profileCue = Ui.MakeImage("EditCue", profilePlateHost, new Color(Gold.r, Gold.g, Gold.b, 0.72f));
            profileCue.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(profileCue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(18f, 18f));
            profileCue.rectTransform.pivot = new Vector2(1f, 0.5f);

            RectTransform avatar = Ui.Panel("Avatar", profile, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
            Image outerInk = Ui.MakeImage("OuterInk", avatar, Ink, round: true);
            Stretch(outerInk.rectTransform, Vector2.zero, Vector2.one);
            Image ring = Ui.MakeImage("Ring", avatar, MenuArt.GoldBorder, round: true);
            Stretch(ring.rectTransform, Vector2.zero, Vector2.one);
            ring.rectTransform.offsetMin = new Vector2(2f, 2f);
            ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
            Image well = Ui.MakeImage("Well", avatar, MenuArt.NightMid, round: true);
            Stretch(well.rectTransform, Vector2.zero, Vector2.one);
            well.rectTransform.offsetMin = new Vector2(6f, 6f);
            well.rectTransform.offsetMax = new Vector2(-6f, -6f);
            well.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _avatarImage = MakeRaw("Photo", well.rectTransform, null);

            // level crest medallion tucked cleanly inside TopBar so it never collides with the left mission rail
            RectTransform levelBadge = Ui.Panel("Level", avatar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 6f), new Vector2(34f, 34f));
            Image levelRim = Ui.MakeImage("Rim", levelBadge, Ink, round: true);
            Stretch(levelRim.rectTransform, Vector2.zero, Vector2.one);
            Image levelCore = Ui.MakeImage("Core", levelBadge, Gold, round: true);
            Stretch(levelCore.rectTransform, Vector2.zero, Vector2.one);
            levelCore.rectTransform.offsetMin = new Vector2(3f, 3f);
            levelCore.rectTransform.offsetMax = new Vector2(-3f, -3f);
            Text levelText = Label("Text", levelCore.rectTransform, "9", 18, Ink, TextAnchor.MiddleCenter, display: true, ink: false);
            Stretch(levelText.rectTransform, Vector2.zero, Vector2.one);

            Text profileKicker = Label("Kicker", profile, "PROFILE", 14, Gold, TextAnchor.LowerLeft, display: true, ink: false);
            Place(profileKicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 9f), new Vector2(-124f, 20f));
            profileKicker.rectTransform.pivot = new Vector2(0f, 0f);
            FitSingleLine(profileKicker, 9, 14);

            _playerName = Label("Name", profile, "", 30, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(_playerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 8f), new Vector2(-124f, 38f));
            _playerName.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(_playerName, 12, 30);

            ProfileButton = AttachButton(profile.gameObject, profilePlate, () => Navigate(MenuStack.ProfileId), 0.96f);

            // 2. Framed Settings button (Donate lives inside Settings, docs/SPEC.md §8)
            RectTransform settings = Ui.Panel("Settings", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(HitSize, HitSize));
            Image settingsHit = settings.gameObject.AddComponent<Image>();
            settingsHit.color = Clear;
            settingsHit.raycastTarget = true;
            RectTransform settingsVisual = Ui.Panel("Visual", settings, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            Image settingsPlate = Ui.MakeSliced("Plate", settingsVisual, MenuArt.ChamferGlass(12), Color.white);
            Ui.MakeCornerBrackets("Brackets", settingsVisual, new Color(Gold.r, Gold.g, Gold.b, 0.42f));
            Image gear = Ui.MakeImage("Gear", settingsVisual, Cream);
            gear.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Gear);
            Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            AttachButton(settings.gameObject, settingsPlate, () => Navigate(MenuStack.SettingsId), 0.92f);

            // 3. Currencies (embossed Barya coin & faceted Diamond gem; sample balances until accounts/store exist, §12)
            RectTransform chips = Ui.Panel("Currencies", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(Gutter + HitSize + 12f), 0f), new Vector2(366f, HitSize));
            MakeCurrencyChip(chips, 0, "12,450", MenuArt.BaryaCoin(), Gold, "barya", () => ComingSoon("Barya: earned in matches and tasks"));
            MakeCurrencyChip(chips, 1, "80", MenuArt.DiamondGem(), MenuArt.DiamondCyan, "diamonds", () => ComingSoon("Diamonds: the shop opens with accounts"));
        }

        void MakeCurrencyChip(RectTransform row, int fromRight, string value, Sprite iconSprite, Color accent, string name, Action onClick)
        {
            float w = fromRight == 0 ? 186f : 162f;
            float x = fromRight == 0 ? -(162f + 10f) : 0f;
            // sleek 56 tall chamfered capsule centered inside a 96 tall (>= 48 dp) hit area
            RectTransform chip = Ui.Panel("Chip_" + name, row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, HitSize));
            Image hit = chip.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            RectTransform visual = Ui.Fill("Visual", chip);
            visual.offsetMin = new Vector2(0f, 20f);
            visual.offsetMax = new Vector2(0f, -20f);
            Image plate = Ui.MakeSliced("Plate", visual, MenuArt.ChamferGlass(10), Color.white);
            Ui.MakeAccentRail("Rail", visual, new Color(accent.r, accent.g, accent.b, 0.72f), width: 3f, verticalInset: 10f);

            Image icon = Ui.MakeImage("Coin", visual, Color.white);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(38f, 38f));
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text v = Label("Value", visual, value, 26, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Stretch(v.rectTransform, Vector2.zero, Vector2.one);
            v.rectTransform.offsetMin = new Vector2(58f, 0f);
            v.rectTransform.offsetMax = new Vector2(-12f, 0f);
            FitSingleLine(v, 12, 26);

            AttachButton(chip.gameObject, plate, onClick, 0.94f);
        }

        void BuildHub(RectTransform parent)
        {
            _hubBody = Ui.Fill("HubBody", parent);

            // Child 0: Left Mission Rail (PASS · PASKO EVENT · DAY 3 GIFT) — sample panels until systems exist (§12)
            _leftColumn = Ui.Panel("LeftColumn", _hubBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(DefaultLeftWidth, 3f * CardHeight + 2f * CardGap));
            MakeLeftCard(0, "Pass", "PASS  ·  1/3", "Rescue 3 teammates", Gold, () => ComingSoon("The battle pass comes with accounts"));
            MakeLeftCard(1, "Event", "PASKO EVENT", "Ends in 12 days", EventCrimson, () => ComingSoon("Events come with accounts"));
            MakeLeftCard(2, "Daily", "DAY 3 GIFT", "100 barya", Gold, () => ComingSoon("Daily gifts come with accounts"));

            // Child 1: Bottom-Left Frosted Tactical Nav Dock (KIDS · STORE · FRIENDS)
            _bottomNav = Ui.Panel("BottomNav", _hubBody, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(BottomNavWidth, NavHeight));
            RectTransform dockVisual = Ui.Fill("DockVisual", _bottomNav);
            dockVisual.offsetMin = new Vector2(0f, 6f);
            dockVisual.offsetMax = new Vector2(0f, -6f);
            Ui.MakeSliced("DockPlate", dockVisual, MenuArt.ChamferGlass(12), Color.white);
            Image dockRule = Ui.MakeImage("DockRule", dockVisual, new Color(Gold.r, Gold.g, Gold.b, 0.42f));
            Place(dockRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-24f, 2f));
            dockRule.rectTransform.pivot = new Vector2(0.5f, 0f);

            MakeNavItem(_bottomNav, 0, "Nav_Kids", "KIDS", MenuArt.GlyphKind.Kid, () => Navigate(MenuStack.KidsId));
            MakeNavItem(_bottomNav, 1, "Nav_Store", "STORE", MenuArt.GlyphKind.Bag, () => ComingSoon("The store comes with accounts"));
            MakeNavItem(_bottomNav, 2, "Nav_Friends", "FRIENDS", MenuArt.GlyphKind.People, () => ComingSoon("Friends and party come with accounts"), badge: "3");

            // Child 2: Hero Zone — grounded tactical hero pedestal tag at the kid's feet (tap opens the kid page)
            _heroZone = Ui.Panel("HeroZone", _hubBody, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _kidPlate = Ui.Panel("KidPlate", _heroZone, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, HitSize));
            Image plateHit = _kidPlate.gameObject.AddComponent<Image>();
            plateHit.color = Clear;
            plateHit.raycastTarget = true;

            RectTransform pill = Ui.Panel("Pill", _kidPlate, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 76f));
            Image pillPlate = Ui.MakeSliced("Plate", pill, MenuArt.ChamferGlass(12), Color.white);
            Ui.MakeCornerBrackets("Brackets", pill, new Color(Gold.r, Gold.g, Gold.b, 0.52f));
            Image pedestalRule = Ui.MakeImage("PedestalRule", pill, new Color(Gold.r, Gold.g, Gold.b, 0.78f));
            Place(pedestalRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-28f, 3f));
            pedestalRule.rectTransform.pivot = new Vector2(0.5f, 0f);

            // Role badge (neutral gold + ink shape icon — zero role colours per SPEC §10)
            RectTransform roleWell = Ui.Panel("RoleDisc", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 44f));
            Ui.MakeSliced("WellPlate", roleWell, MenuArt.ChamferCard(MenuArt.GoldLight, MenuArt.GoldDeep, MenuArt.GoldBorder, 8, 2, 0.45f), Color.white);
            _kidRoleIcon = Ui.MakeImage("RoleIcon", roleWell, Ink);
            Place(_kidRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            // Strictly bounded text block between role badge (x=66) and framed chevron (right-54) — zero overlap at 1024x768..21:9
            RectTransform textBlock = Ui.Fill("NameBlock", pill);
            textBlock.offsetMin = new Vector2(66f, 8f);
            textBlock.offsetMax = new Vector2(-54f, -8f);

            _kidMeta = Label("KidMeta", textBlock, "", 15, Gold, TextAnchor.LowerLeft, display: true, ink: false);
            Place(_kidMeta.rectTransform, new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 22f));
            _kidMeta.rectTransform.pivot = new Vector2(0.5f, 1f);
            FitSingleLine(_kidMeta, 8, 15);

            _kidName = Label("KidName", textBlock, "", 32, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(_kidName.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 36f));
            _kidName.rectTransform.pivot = new Vector2(0.5f, 0f);
            FitSingleLine(_kidName, 12, 32);

            RectTransform chevronBox = Ui.Panel("ChevronFrame", pill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(32f, 32f));
            Ui.MakeSliced("BoxPlate", chevronBox, MenuArt.StatChip(6), Color.white);
            Image chevron = Ui.MakeImage("Chevron", chevronBox, Gold);
            chevron.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

            KidButton = AttachButton(_kidPlate.gameObject, pillPlate, () => Navigate(MenuStack.KidPageId), 0.96f);

            // Child 3: Right Queue Cluster (Esports Mode card over luminous beveled PLAY CTA)
            _modeColumn = Ui.Panel("ModeColumn", _hubBody, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(372f, 0f));
            BuildModeCard(_modeColumn);
            BuildPlay(_modeColumn);

            BuildToast(parent);
        }

        /// <summary>One sleek tactical left-rail mission strip inside a &gt;= 48 dp hit-box.</summary>
        void MakeLeftCard(int index, string name, string title, string line, Color accent, Action onClick)
        {
            RectTransform card = Ui.Panel("Card_" + name, _leftColumn, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * (CardHeight + CardGap)), new Vector2(0f, CardHeight));
            Image hit = card.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            RectTransform visual = Ui.Fill("Visual", card);
            visual.offsetMin = new Vector2(0f, 8f);
            visual.offsetMax = new Vector2(0f, -8f);
            Image plate = Ui.MakeSliced("Plate", visual, MenuArt.ChamferGlass(12), Color.white);

            // soft left ambient accent glow nestled inside the strip + crisp glowing vertical accent rail
            Image glow = Ui.MakeImage("AccentGlow", visual, new Color(accent.r, accent.g, accent.b, index == 2 ? 0.22f : 0.15f));
            glow.sprite = MenuArt.Glow();
            Place(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(90f, 74f));
            Ui.MakeAccentRail("Rail", visual, accent, width: 4.5f, verticalInset: 10f);

            RectTransform art = Ui.Panel("Art", visual, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(54f, 54f));
            if (index == 0)
            {
                // Battle Pass radial progress ring + crisp tier number
                Image track = Ui.MakeImage("Track", art, new Color(1f, 1f, 1f, 0.14f));
                track.sprite = KomiksUi.RingSprite(128, 8);
                Stretch(track.rectTransform, Vector2.zero, Vector2.one);
                Image arc = Ui.MakeImage("Arc", art, Gold);
                arc.sprite = KomiksUi.RingSprite(128, 8);
                arc.type = Image.Type.Filled;
                arc.fillMethod = Image.FillMethod.Radial360;
                arc.fillOrigin = (int)Image.Origin360.Top;
                arc.fillClockwise = true;
                arc.fillAmount = 0.4f;
                Stretch(arc.rectTransform, Vector2.zero, Vector2.one);
                Text level = Label("Level", art, "12", 22, Cream, TextAnchor.MiddleCenter, display: true, ink: true);
                Stretch(level.rectTransform, Vector2.zero, Vector2.one);
            }
            else if (index == 1)
            {
                // Pasko Event crimson tactical emblem
                Ui.MakeSliced("Badge", art, MenuArt.ChamferCard(new Color(0.95f, 0.34f, 0.38f, 1f), new Color(0.62f, 0.14f, 0.22f, 1f), new Color(1f, 0.68f, 0.70f, 0.75f), 10, 2, 0.35f), Color.white);
                Image mark = Ui.MakeImage("Mark", art, Cream);
                mark.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Star);
                Place(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            }
            else
            {
                // Daily Gift warm amber claim highlight + embossed Barya coin
                Ui.MakeSliced("Badge", art, MenuArt.ChamferCard(new Color(0.24f, 0.18f, 0.08f, 0.96f), new Color(0.11f, 0.08f, 0.04f, 0.98f), new Color(Gold.r, Gold.g, Gold.b, 0.72f), 10, 2, 0.3f), Color.white);
                Image coin = Ui.MakeImage("Coin", art, Color.white);
                coin.sprite = MenuArt.BaryaCoin();
                coin.preserveAspect = true;
                Place(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
                Ui.MakeSliced("ClaimRim", visual, MenuArt.ChamferOutline(new Color(Gold.r, Gold.g, Gold.b, 0.52f), 12, 2), Color.white);
                Ui.AddPulse(glow, 0.14f, 0.28f, 2.2f, 0f);
            }

            float titleRightPad = index == 1 ? -34f : -14f;
            Text t = Label("Title", visual, title, 20, index == 1 ? new Color(1f, 0.76f, 0.74f, 1f) : Gold, TextAnchor.LowerLeft, display: true, ink: true);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(82f, index == 0 ? 4f : 1f), new Vector2(titleRightPad - 82f, 26f));
            t.rectTransform.pivot = new Vector2(0f, 0f);
            FitSingleLine(t, 12, 20);

            Text l = Label("Line", visual, line, 18, new Color(Cream.r, Cream.g, Cream.b, 0.92f), TextAnchor.UpperLeft, display: false, ink: false);
            Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(82f, index == 0 ? 1f : -2f), new Vector2(-96f, 24f));
            l.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(l, 10, 18);

            if (index == 0)
            {
                // crisp thin progress bar along bottom of Pass text column
                RectTransform barBg = Ui.Panel("ProgressTrack", visual, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(82f, 10f), new Vector2(-98f, 4f));
                Image barBgImg = barBg.gameObject.AddComponent<Image>();
                barBgImg.color = new Color(1f, 1f, 1f, 0.14f);
                barBgImg.raycastTarget = false;
                RectTransform barFill = Ui.Panel("ProgressFill", barBg, Vector2.zero, new Vector2(0.34f, 1f), Vector2.zero, Vector2.zero, Vector2.zero);
                Image barFillImg = barFill.gameObject.AddComponent<Image>();
                barFillImg.color = Gold;
                barFillImg.raycastTarget = false;
            }
            else if (index == 1)
            {
                // live pulsing dot on Pasko Event strip (EditMode-inert)
                Image halo = Ui.MakeImage("LiveHalo", visual, new Color(EventCrimson.r, EventCrimson.g, EventCrimson.b, 0.35f), round: true);
                Place(halo.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(18f, 18f));
                Ui.AddPulse(halo, 0.15f, 0.55f, 3.0f, 0.18f);
                Image dot = Ui.MakeImage("LiveDot", visual, EventCrimson, round: true);
                Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(9f, 9f));
                Ui.AddPulse(dot, 0.55f, 1f, 3.0f, 0f);
            }

            AttachButton(card.gameObject, plate, onClick, 0.96f);
        }

        /// <summary>One bottom-dock nav item (&gt;= 48 dp hit-box) seated on the frosted tactical dock plate.</summary>
        void MakeNavItem(RectTransform bar, int index, string name, string title, MenuArt.GlyphKind glyph, Action onClick, string badge = null)
        {
            float x = NavDockPadX + index * (NavItemWidth + NavItemGap);
            RectTransform item = Ui.Panel(name, bar, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(x, 0f), new Vector2(NavItemWidth, 0f));
            Image hit = item.gameObject.AddComponent<Image>();
            hit.color = Clear;
            hit.raycastTarget = true;

            Image icon = Ui.MakeImage("Icon", item, Cream);
            icon.sprite = MenuArt.Glyph(glyph);
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(34f, 34f));
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);

            Text label = Label("Label", item, title, 18, Cream, TextAnchor.LowerCenter, display: true, ink: true);
            Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(-8f, 24f));
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            FitSingleLine(label, 10, 18);

            if (index > 0)
            {
                Image divider = Ui.MakeImage("Divider", item, new Color(Cream.r, Cream.g, Cream.b, 0.16f));
                Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-1f, 0f), new Vector2(1.5f, 44f));
            }
            if (badge != null)
            {
                RectTransform badgeHost = Ui.Panel("Badge", item, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(22f, -20f), new Vector2(30f, 26f));
                Ui.MakeSliced("BadgeRim", badgeHost, MenuArt.RoundSolid(13), Ink);
                RectTransform core = Ui.Fill("BadgeCore", badgeHost);
                core.offsetMin = new Vector2(2f, 2f);
                core.offsetMax = new Vector2(-2f, -2f);
                Ui.MakeSliced("Fill", core, MenuArt.RoundSolid(11), MenuArt.SuccessGreen);
                Text count = Label("Count", core, badge, 16, Ink, TextAnchor.MiddleCenter, display: true, ink: false);
                Stretch(count.rectTransform, Vector2.zero, Vector2.one);
            }

            AttachButton(item.gameObject, icon, onClick, 0.92f);
        }

        void BuildModeCard(RectTransform column)
        {
            _modeZone = Ui.Panel("ModeZone", column, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PlayHeight + ModeToPlayGap), new Vector2(0f, ModeHeight));
            Image plate = Ui.MakeSliced("Plate", _modeZone, MenuArt.ChamferGlass(14), Color.white, raycast: true);
            Image spot = Ui.MakeImage("Spotlight", _modeZone, new Color(Gold.r, Gold.g, Gold.b, 0.08f));
            spot.sprite = MenuArt.CardSpotlight();
            Stretch(spot.rectTransform, Vector2.zero, Vector2.one);
            Ui.MakeCornerBrackets("Brackets", _modeZone, new Color(Gold.r, Gold.g, Gold.b, 0.48f));

            // crisp gold left accent bar marks the active queue selection
            Ui.MakeAccentRail("Bar", _modeZone, Gold, width: 5f, verticalInset: 18f);

            Text kicker = Label("Kicker", _modeZone, "QUEUE MODE", 18, Gold, TextAnchor.UpperLeft, display: true, ink: false);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-102f, 24f));
            kicker.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(kicker, 11, 18);

            _modeName = Label("ModeName", _modeZone, "", 62, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Place(_modeName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -44f), new Vector2(-102f, 68f));
            _modeName.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(_modeName, 28, 62);

            _modeDetail = Label("ModeDetail", _modeZone, "", 20, Muted, TextAnchor.UpperLeft, display: false, ink: false);
            Place(_modeDetail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -120f), new Vector2(-46f, 28f));
            _modeDetail.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(_modeDetail, 11, 20);

            // framed swap chevron visual (non-Selectable to avoid nested Selectables)
            RectTransform chevronFrame = Ui.Panel("ChevronDisc", _modeZone, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 6f), new Vector2(48f, 48f));
            Ui.MakeSliced("Frame", chevronFrame, MenuArt.CtaGhost(10), Color.white);
            Image chevron = Ui.MakeImage("Chevron", chevronFrame, Gold);
            chevron.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

            ModeButton = AttachButton(_modeZone.gameObject, plate, CycleHomeMode, 0.97f);
            ShowHomeMode();
        }

        void BuildPlay(RectTransform column)
        {
            _playZone = Ui.Panel("PlayZone", column, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, PlayHeight));

            // soft warm gold aura behind PLAY (EditMode-inert ambient pulse)
            Image playGlow = Ui.MakeImage("Glow", _playZone, new Color(Gold.r, Gold.g, Gold.b, 0.30f));
            playGlow.sprite = MenuArt.Glow();
            Stretch(playGlow.rectTransform, Vector2.zero, Vector2.one);
            playGlow.rectTransform.offsetMin = new Vector2(-34f, -26f);
            playGlow.rectTransform.offsetMax = new Vector2(34f, 26f);
            Ui.AddPulse(playGlow, 0.18f, 0.36f, 2.2f, 0f);

            Image plate = Ui.MakeSliced("Plate", _playZone, MenuArt.CtaGold(16), Color.white, raycast: true);

            // clipped ambient diagonal sheen in Play mode (strictly inert in EditMode & ReduceMotion)
            Ui.AddSheen(_playZone, period: 3.8f, sweepDuration: 0.78f, peakAlpha: 0.34f);

            // centered icon + PLAY lockup lifted +3px above the bottom 3D press-lip bevel
            RectTransform content = Ui.Panel("Content", _playZone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(236f, 68f));
            Image triangle = Ui.MakeImage("Triangle", content, Ink);
            triangle.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Play);
            Place(triangle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(40f, 40f));
            triangle.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text label = Label("Label", content, "PLAY", 64, Ink, TextAnchor.MiddleLeft, display: true, ink: false);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(66f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            FitSingleLine(label, 36, 64);

            PlayButton = AttachButton(_playZone.gameObject, plate, PressPlay, 0.96f);
        }

        // ---------------------------------------------------------------- toast

        RectTransform _toast;
        Text _toastText;
        float _toastUntil;

        /// <summary>A sleek tactical notification strip at top centre (Wild Rift style) for panels whose systems arrive later.</summary>
        void BuildToast(RectTransform parent)
        {
            _toast = Ui.Panel("Toast", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TopBarHeight + 6f)), new Vector2(620f, 58f));
            Ui.MakeSliced("Plate", _toast, MenuArt.ChamferGlass(12), Color.white);
            Ui.MakeAccentRail("Rail", _toast, Gold, width: 4f, verticalInset: 10f);
            Ui.MakeCornerBrackets("Brackets", _toast, new Color(Gold.r, Gold.g, Gold.b, 0.42f));
            _toastText = Label("Text", _toast, "", 22, Cream, TextAnchor.MiddleCenter, display: false, ink: false);
            Stretch(_toastText.rectTransform, Vector2.zero, Vector2.one);
            _toastText.rectTransform.offsetMin = new Vector2(24f, 0f);
            _toastText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            FitSingleLine(_toastText, 12, 22);
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
            scrim.color = new Color(0.01f, 0.03f, 0.06f, 0.86f);
            scrim.raycastTarget = true;

            RectTransform card = Ui.Panel("Card", _offlinePrompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 320f));
            Ui.MakeSliced("Plate", card, MenuArt.ChamferGlass(16), Color.white);
            Ui.MakeCornerBrackets("Brackets", card, new Color(Gold.r, Gold.g, Gold.b, 0.56f));
            Ui.MakeAccentRail("Rail", card, Gold, width: 5f, verticalInset: 24f);

            Text kicker = Label("Kicker", card, "CONNECTION CHECK", 18, Gold, TextAnchor.UpperLeft, display: true, ink: false);
            Place(kicker.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(36f, -24f), new Vector2(-72f, 24f));
            kicker.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(kicker, 14, 18);

            Text title = Label("Title", card, "YOU'RE OFFLINE", 46, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(36f, -50f), new Vector2(-72f, 56f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(title, 30, 46);

            Image rule = Ui.MakeImage("Rule", card, Gold);
            Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -110f), new Vector2(88f, 4f));
            rule.rectTransform.pivot = new Vector2(0f, 1f);

            Text body = Label("Body", card, "Play offline now, or retry once you are connected.", 21, Muted, TextAnchor.UpperLeft, display: false, ink: false);
            Place(body.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(36f, -126f), new Vector2(-72f, 34f));
            body.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(body, 12, 21);

            RectTransform actions = Ui.Panel("Actions", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(-72f, HitSize));
            MakeActionButton(actions, "Retry", "RETRY", false, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Retry);
            Button goOffline = MakeActionButton(actions, "GoOffline", "PLAY OFFLINE", true, new Vector2(0.46f, 0f), new Vector2(1f, 1f), () =>
            {
                CloseOfflinePrompt();
                OpenOfflineHub();
            });
            RectTransform goOfflineVisual = goOffline.transform.Find("Visual") as RectTransform;
            Ui.AddSheen(goOfflineVisual ?? goOffline.GetComponent<RectTransform>(), period: 4.0f, sweepDuration: 0.72f, peakAlpha: 0.28f);
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
            weave.color = new Color(MenuArt.TealLight.r, MenuArt.TealLight.g, MenuArt.TealLight.b, 0.08f);
            weave.uvRect = new Rect(0f, 0f, 1280f / WeaveTile, 720f / WeaveTile);
            MakeRaw("Vignette", _offlineHubBackdrop, MenuArt.Vignette());
            _offlineHubBackdrop.gameObject.SetActive(false);
            _offlineHubBackdrop.SetSiblingIndex(SafeRoot.GetSiblingIndex()); // behind the hub, over the hero and the stage

            _offlineHub = Ui.Fill("OfflineHub", SafeRoot);
            RectTransform header = Ui.Panel("Header", _offlineHub, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(140f, HitSize));
            Image backHit = back.gameObject.AddComponent<Image>();
            backHit.color = Clear;
            backHit.raycastTarget = true;
            RectTransform backVisual = Ui.Fill("Visual", back);
            backVisual.offsetMin = new Vector2(0f, 14f);
            backVisual.offsetMax = new Vector2(0f, -14f);
            Image backPlate = Ui.MakeSliced("Plate", backVisual, MenuArt.CtaGhost(12), Color.white);
            Text backLabel = Label("Label", backVisual, "BACK", 24, Cream, TextAnchor.MiddleCenter, display: true, ink: true);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one);
            AttachButton(back.gameObject, backPlate, CloseOfflineHub, 0.94f);

            Text hubKicker = Label("Kicker", header, "LOCAL & PRACTICE", 16, Gold, TextAnchor.LowerLeft, display: true, ink: false);
            Place(hubKicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 140f + 24f, 16f), new Vector2(-(Gutter + 140f + 24f) - Gutter, 20f));
            hubKicker.rectTransform.pivot = new Vector2(0f, 0f);
            FitSingleLine(hubKicker, 13, 16);

            Text title = Label("Title", header, "OFFLINE", 48, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 140f + 24f, 14f), new Vector2(-(Gutter + 140f + 24f) - Gutter, 52f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            FitSingleLine(title, 32, 48);

            RectTransform cards = Ui.Fill("Cards", _offlineHub);
            cards.offsetMin = new Vector2(Gutter, 20f);
            cards.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            RectTransform hotspot = MakeOfflineCard(cards, "Hotspot", 0, "HOTSPOT", "Play over phone hotspot", "hotspot", MenuArt.Teal);
            RectTransform hotspotRow = ActionRow(hotspot);
            MakeActionButton(hotspotRow, "Host", "HOST", true, Vector2.zero, new Vector2(0.48f, 1f), () => OpenHotspotFlow(true));
            MakeActionButton(hotspotRow, "Join", "JOIN", false, new Vector2(0.52f, 0f), Vector2.one, () => OpenHotspotFlow(false));

            RectTransform ai = MakeOfflineCard(cards, "PracticeAi", 1, "PRACTICE VS AI", "Fight bots at your pace", "ai", Gold);
            RectTransform aiRow = ActionRow(ai);
            Button diff = MakeActionButton(aiRow, "Difficulty", "MEDIUM", false, Vector2.zero, new Vector2(0.48f, 1f), CycleDifficulty);
            _difficultyLabel = diff.GetComponentInChildren<Text>(true);
            MakeActionButton(aiRow, "Play", "PLAY", true, new Vector2(0.52f, 0f), Vector2.one, () => LaunchMode(GameMode.Practice));

            RectTransform solo = MakeOfflineCard(cards, "PracticeSolo", 2, "PRACTICE SOLO", "Free movement and aim", "solo", MenuArt.Coral);
            MakeActionButton(ActionRow(solo), "Play", "PLAY", true, Vector2.zero, Vector2.one, () => LaunchMode(GameMode.Training));

            _offlineHub.gameObject.SetActive(false);
        }

        /// <summary>Hotspot, Practice vs AI, Practice solo (Explore joins when it is built: only real content, docs/SPEC.md §8).</summary>
        const int OfflineCardCount = 3;

        RectTransform MakeOfflineCard(RectTransform cards, string name, int index, string title, string subtitle, string glyph, Color accent)
        {
            const float gap = 18f;
            const float share = 1f / OfflineCardCount;
            RectTransform card = Ui.Panel(name, cards, new Vector2(index * share, 0f), new Vector2((index + 1) * share, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 8f);
            card.offsetMax = new Vector2(index == OfflineCardCount - 1 ? 0f : -gap * 0.5f, 0f);
            Ui.MakeSliced("Plate", card, MenuArt.ChamferGlass(16), Color.white);
            Ui.MakeCornerBrackets("Brackets", card, new Color(accent.r, accent.g, accent.b, 0.45f));

            // top accent bar across the card crown
            Image topRule = Ui.MakeImage("TopBar", card, accent);
            Place(topRule.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -2f), new Vector2(-44f, 4f));
            topRule.rectTransform.pivot = new Vector2(0.5f, 1f);

            // framed deep-tinted art bay with spotlight, weave and crisp icon glyph
            RectTransform art = Ui.Panel("Art", card, new Vector2(0f, 0.44f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            art.offsetMin = new Vector2(14f, 0f);
            art.offsetMax = new Vector2(-14f, -16f);
            Color bayTop = Color.Lerp(MenuArt.PanelTop, accent, 0.42f);
            Color bayBot = Color.Lerp(MenuArt.PanelBottom, accent, 0.18f);
            Ui.MakeSliced("Colour", art, MenuArt.ChamferCard(bayTop, bayBot, new Color(accent.r, accent.g, accent.b, 0.48f), 12, 2, 0.30f), Color.white);
            RectTransform clip = Ui.Fill("Clip", art);
            clip.offsetMin = new Vector2(4f, 4f);
            clip.offsetMax = new Vector2(-4f, -4f);
            clip.gameObject.AddComponent<RectMask2D>();
            RawImage weave = MakeRaw("Weave", clip, MenuArt.Weave());
            weave.color = new Color(1f, 1f, 1f, 0.10f);
            weave.uvRect = new Rect(0f, 0f, 4f, 2f);
            Image spot = Ui.MakeImage("Spot", clip, new Color(accent.r, accent.g, accent.b, 0.24f));
            spot.sprite = MenuArt.CardSpotlight();
            Stretch(spot.rectTransform, Vector2.zero, Vector2.one);
            Image bottomFade = Ui.MakeImage("Scrim", clip, new Color(1f, 1f, 1f, 0.65f));
            bottomFade.sprite = MenuArt.BottomScrim();
            Stretch(bottomFade.rectTransform, Vector2.zero, new Vector2(1f, 0.55f));
            BuildCardGlyph(art, glyph, accent);

            Text label = Label("Title", card, title, 30, Cream, TextAnchor.LowerLeft, display: true, ink: true);
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
            Color cream = new Color(Cream.r, Cream.g, Cream.b, 0.96f);
            switch (glyph)
            {
                case "hotspot":
                    for (int i = 0; i < 3; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, new Color(cream.r, cream.g, cream.b, 0.95f - 0.26f * i));
                        ring.sprite = KomiksUi.RingSprite(256, 12);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (56f + i * 50f));
                    }
                    Image dot = Ui.MakeImage("Dot", art, accent, round: true);
                    Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
                    break;
                case "ai":
                    Text vs = Label("Vs", art, "VS", 86, cream, TextAnchor.MiddleCenter, display: true, ink: true);
                    Stretch(vs.rectTransform, Vector2.zero, Vector2.one);
                    break;
                default:
                    for (int i = 0; i < 2; i++)
                    {
                        Image ring = Ui.MakeImage("Ring" + i, art, new Color(cream.r, cream.g, cream.b, 0.92f - 0.25f * i));
                        ring.sprite = KomiksUi.RingSprite(256, 14);
                        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (138f - i * 58f));
                    }
                    Image core = Ui.MakeImage("Core", art, accent, round: true);
                    Place(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
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
            visual.offsetMin = new Vector2(0f, 10f);
            visual.offsetMax = new Vector2(0f, -10f);
            Image plate = Ui.MakeSliced("Plate", visual, primary ? MenuArt.CtaGold(12) : MenuArt.CtaGhost(12), Color.white);
            Text label = Label("Label", visual, title, 25, primary ? Ink : Cream, TextAnchor.MiddleCenter, display: true, ink: !primary);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(8f, primary ? 2f : 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            FitSingleLine(label, 15, 25);
            return AttachButton(rect.gameObject, plate, onClick, 0.95f);
        }

        // ---------------------------------------------------------------- refresh

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
                _playerName.text = string.IsNullOrWhiteSpace(session.PlayerName) ? "PLAYER" : session.PlayerName.Trim().ToUpperInvariant();

            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter) ? AppSession.DefaultCharacter : session.SelectedCharacter;
            if (_avatarImage != null && Sheets != null)
            {
                Sheets.SyncFromSession();
                if (Sheets.HasCustomIdPhoto)
                {
                    _avatarImage.texture = Sheets.CurrentIdTexture;
                    _avatarImage.uvRect = Sheets.CurrentIdUvRect;
                }
                else
                {
                    _avatarImage.texture = KalyeahResources.Texture("portraits/faces/" + kidId);
                    _avatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                }
                _avatarImage.enabled = _avatarImage.texture != null;
            }
            string description = ReadCharacterField(kidId, "description");
            string role = ParseRole(description);
            string purok = ParsePurok(description);
            if (_kidMeta != null)
            {
                if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(purok))
                    _kidMeta.text = role + " · " + purok;
                else if (!string.IsNullOrEmpty(role))
                    _kidMeta.text = role;
                else
                    _kidMeta.text = "KID";
            }
            if (_kidName != null)
                _kidName.text = (ReadCharacterField(kidId, "display_name") ?? Capitalize(kidId)).ToUpperInvariant();
            if (_kidRoleIcon != null)
                _kidRoleIcon.sprite = MenuArt.Glyph(RoleGlyph(role));
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

        /// <summary>Parses the purok name after " from " up to the first period in a kid description.</summary>
        static string ParsePurok(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";
            int from = description.IndexOf(" from ", StringComparison.Ordinal);
            if (from < 0) return "";
            int start = from + 6;
            if (start >= description.Length) return "";
            int dot = description.IndexOf('.', start);
            string raw = dot > start ? description.Substring(start, dot - start) : description.Substring(start);
            return raw.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Lays out the top bar, the hub columns (left rail / bottom dock, hero zone, mode column) and the scene.
        /// Guarantees zero overlap and &gt;= 48 dp touch targets across 1280x720 (16:9), 844x390 (notched 19.5:9),
        /// 934x400 (21:9), and 1024x768 (4:3 tablet).
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
            Rect full = Root.rect;
            float width = safe.width;
            _areaSize = new Vector2(Mathf.Max(1f, full.width), Mathf.Max(1f, full.height));

            _hubBody.offsetMin = new Vector2(Gutter, 16f);
            _hubBody.offsetMax = new Vector2(-Gutter, -(TopBarHeight + 8f));

            float bodyWidth = Mathf.Max(0f, width - 2f * Gutter);
            float leftWidth = Mathf.Clamp(bodyWidth * 0.245f, 284f, 304f);
            float rightWidth = Mathf.Clamp(bodyWidth * 0.30f, 326f, 376f);
            float leftSpan = Mathf.Max(leftWidth, BottomNavWidth);

            if (_leftColumn != null)
                _leftColumn.sizeDelta = new Vector2(leftWidth, 3f * CardHeight + 2f * CardGap);
            if (_bottomNav != null)
            {
                _bottomNav.anchoredPosition = Vector2.zero;
                _bottomNav.sizeDelta = new Vector2(BottomNavWidth, NavHeight);
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
            if (_kidPlate != null)
            {
                float plateWidth = zoneWidth > 4f ? Mathf.Min(320f, Mathf.Max(Mathf.Min(220f, zoneWidth), zoneWidth - 4f)) : 252f;
                _kidPlate.anchoredPosition = Vector2.zero;
                _kidPlate.sizeDelta = new Vector2(plateWidth, HitSize);
            }

            // the kid: thigh-up framing on phones (feet below the screen), grounded cleanly over the pedestal tag on 4:3 tablet
            const float KidInTexture = 0.755f, HeadTop = 0.93f, VisibleKid = 0.67f / 0.6f;
            float heroHeight = full.height * VisibleKid / KidInTexture * Mathf.Clamp(zoneWidth / 520f, 0.72f, 1f);
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

        /// <summary>The stall plate follows the kid's picture; the pedestal shadow sits under the feet; the side shades frame the UI.</summary>
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
                _stallBase = centre;
                SetRect(_stallRect, centre, new Vector2(plateW, plateH));
            }
            if (_heroShadowRect != null)
                SetRect(_heroShadowRect, new Vector2(pos.x, pos.y + size.y * _stage.FeetViewportY), new Vector2(size.x * 0.48f, size.x * 0.095f));
            if (_shadeLeft != null) _shadeLeft.sizeDelta = new Vector2(_areaSize.x * 0.40f, 0f);
            if (_shadeRight != null) _shadeRight.sizeDelta = new Vector2(_areaSize.x * 0.44f, 0f);
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
                bool allowOutline = ink && Ui.Luminance(color) >= 0.25f;
                outline.enabled = allowOutline;
                if (allowOutline)
                {
                    outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.92f);
                    outline.effectDistance = new Vector2(2f, -2f);
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

        static Button AttachButton(GameObject host, Graphic targetGraphic, Action onClick, float pressedScale = 0.95f)
        {
            Button btn = host.GetComponent<Button>() ?? host.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = PressTint;
            btn.colors = cb;
            Ui.AddPressFeel(host, pressedScale);
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
