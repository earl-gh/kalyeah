using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Tabbed Komiks Almanac screen (Kids 2x5 roster grid, Weapons &amp; Kit 16 cards incl. Tirador &amp; Bagsak,
    /// and Larong Kalye 12-month Filipino street-game heritage book read from Data/rules__larong_kalye).
    /// </summary>
    public sealed class CodexScreenView : IDisposable
    {
        public const int TabKids = 0;
        public const int TabWeapons = 1;
        public const int TabLarongKalye = 2;

        static readonly Color BackdropSolid = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color BackdropWash = new Color(0.02f, 0.035f, 0.07f, 0.72f);
        static readonly Color CardTop = new Color(0.100f, 0.152f, 0.242f, 0.92f);
        static readonly Color CardBottom = new Color(0.052f, 0.086f, 0.152f, 0.96f);
        static readonly Color FairTop = new Color(0.10f, 0.34f, 0.28f, 0.94f);
        static readonly Color FairBottom = new Color(0.05f, 0.19f, 0.17f, 0.97f);
        static readonly Color FairBorder = new Color(0.24f, 0.82f, 0.56f, 0.45f);
        static readonly Color GoalTop = new Color(0.13f, 0.20f, 0.34f, 0.94f);
        static readonly Color GoalBottom = new Color(0.07f, 0.11f, 0.21f, 0.97f);
        static readonly Color DividerColor = new Color(0.76f, 0.88f, 0.96f, 0.14f);
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Coral = new Color(0.96f, 0.38f, 0.26f, 1f);
        static readonly Color Teal = MenuArt.GoldLight;
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color Ink = MenuArt.Ink;

        static readonly string[] MonthNamesEn =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        sealed class KidEntry
        {
            public string Id;
            public string Name;
            public string Description;
            public Color Accent;
        }

        sealed class WeaponEntry
        {
            public string Id;
            public string Name;
            public string ShortName;
            public string Description;
            public string StreetGame;
            public string RoleLabel;
            public Color RoleColor;
            public int Damage;
            public int Heal;
            public int ElixirCost;
            public float Cooldown;
            public float Range;
            public float Radius;
            public bool IsPrimary;
            public bool IsUltimate;
        }

        sealed class HeritageEntry
        {
            public int Month;
            public string Name;
            public string Also;
            public int PlayersMin;
            public int PlayersMax;
            public string Time;
            public string Place;
            public string[] Items;
            public string Goal;
            public string[] Setup;
            public string[] How;
            public string[] Rules;
            public string Safety;
        }

        [ExecuteAlways]
        sealed class CodexLifecycle : MonoBehaviour
        {
            [NonSerialized] public CodexScreenView Owner;
            bool _subscribed;

            void OnEnable()
            {
                EnsureSubscribed();
                Owner?.RefreshLayout();
            }

            void OnDisable() => Unsubscribe();

            void OnDestroy()
            {
                Unsubscribe();
                Owner?.DisposeFromLifecycle();
            }

            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();

            void EnsureSubscribed()
            {
                if (_subscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged += HandleViewportChanged;
                _subscribed = true;
            }

            void Unsubscribe()
            {
                if (!_subscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged -= HandleViewportChanged;
                _subscribed = false;
            }

            void HandleViewportChanged()
            {
                if (this == null)
                {
                    Unsubscribe();
                    return;
                }
                Owner?.RefreshLayout();
            }
        }

        readonly DataSource _data;
        readonly Action _onBack;
        Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _shell;

        readonly List<KidEntry> _kids = new List<KidEntry>(10);
        readonly List<WeaponEntry> _weapons = new List<WeaponEntry>(16);
        readonly List<HeritageEntry> _heritage = new List<HeritageEntry>(12);
        readonly Dictionary<string, string> _streetBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        readonly RectTransform[] _tabPanes = new RectTransform[3];
        readonly Image[] _tabPillBgs = new Image[3];
        readonly Text[] _tabPillLabels = new Text[3];
        readonly LayoutElement[] _tabPillLayouts = new LayoutElement[3];

        readonly List<GameObject> _kidCardOutlines = new List<GameObject>(12);
        readonly List<GameObject> _weaponCardOutlines = new List<GameObject>(16);
        readonly List<Image> _monthButtonBgs = new List<Image>(12);
        readonly List<Text> _monthButtonLabels = new List<Text>(12);
        readonly List<LayoutElement> _monthButtonLayouts = new List<LayoutElement>(12);

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _headerAccentRect;
        RectTransform _headerTitleRect;
        RectTransform _tabsRowRect;
        HorizontalLayoutGroup _tabsRowLayout;
        GridCellAutoSizer _kidsGridSizer;
        GridCellAutoSizer _weaponsGridSizer;
        ScrollRect _heritageScroll;

        Text _headerTitle;
        Text _backLabel;

        // Kids inspector
        RawImage _kidPortrait;
        Image _kidPortraitFallback;
        Text _kidNameText;
        Text _kidBadgeText;
        Text _kidBioText;

        // Weapons inspector
        RawImage _weaponIcon;
        Image _weaponRoleChip;
        Text _weaponRoleText;
        Text _weaponNameText;
        Text _weaponOriginText;
        Text _weaponDescText;
        Text _weaponStatsText;
        Text _weaponBlurbText;

        // Heritage inspector
        Text _heritageMonthBadge;
        Text _heritageTitleText;
        Text _heritageMetaText;
        Text _heritageGoalText;
        Text _heritageSetupText;
        Text _heritageHowText;
        Text _heritageRulesText;

        int _activeTab;
        int _selectedKidIndex;
        int _selectedWeaponIndex;
        int _selectedMonthIndex;
        bool _refreshingLayout;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public SafeArea SafeAreaComponent => _safeArea;
        public int ActiveTab => _activeTab;
        public int KidCount => _kids.Count;
        public int WeaponCardCount => _weapons.Count;
        public int HeritageMonthCount => _heritage.Count;

        public CodexScreenView(Transform parent, Action onBack)
            : this(parent, null, onBack)
        {
        }

        public CodexScreenView(Transform parent, DataSource data = null, Action onBack = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data ?? KalyeahResources.Data();
            _onBack = onBack;
            _canvas = parent.GetComponentInParent<Canvas>();

            LoadCatalogData();

            Root = Ui.Fill("CodexScreen", parent);
            BuildBackdrop(Root);

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null)
            {
                _safeArea = parentSafe;
                SafeRoot = Ui.Fill("SafeRoot", Root);
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
            }
            _safeArea.Changed += OnSafeAreaChanged;

            _shell = Ui.Panel("Shell", SafeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 0f));

            BuildHeader(_shell);
            BuildKidsTab(_shell);
            BuildWeaponsTab(_shell);
            BuildLarongKalyeTab(_shell);

            var lifecycle = Root.gameObject.AddComponent<CodexLifecycle>();
            lifecycle.Owner = this;

            SettingsStore.Current.Changed += RefreshLocalization;
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;

            _safeArea.Apply();
            RefreshLocalization();
            SelectKid(0);
            SelectWeapon(0);
            SelectMonth(0);
            SetActiveTab(TabKids);
            RefreshLayout();
        }

        public void Show()
        {
            if (Root != null) Root.gameObject.SetActive(true);
            RefreshLocalization();
            RefreshLayout();
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        public void SetActiveTab(int tabIndex)
        {
            _activeTab = Mathf.Clamp(tabIndex, 0, _tabPanes.Length - 1);
            Sprite activeSprite = MenuArt.CtaGold(10);
            Sprite idleSprite = MenuArt.CtaGhost(10);
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                bool active = i == _activeTab;
                if (_tabPanes[i] != null) _tabPanes[i].gameObject.SetActive(active);
                if (_tabPillBgs[i] != null)
                {
                    _tabPillBgs[i].sprite = active ? activeSprite : idleSprite;
                    _tabPillBgs[i].color = Color.white;
                }
                if (_tabPillLabels[i] != null)
                    Ui.SetTextColor(_tabPillLabels[i], active ? Ink : MenuArt.Cream);
            }
            _kidsGridSizer?.Apply();
            _weaponsGridSizer?.Apply();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SettingsStore.Current.Changed -= RefreshLocalization;
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void DisposeFromLifecycle() => Dispose();

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        public void RefreshLayout()
        {
            if (_disposed || _refreshingLayout || Root == null || _shell == null) return;
            _refreshingLayout = true;
            try
            {
                if (_canvas == null) _canvas = Root.GetComponentInParent<Canvas>();

                _safeArea?.Apply();

                Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float clampedWidth = Mathf.Clamp(safeSize.x - 24f, 640f, 1320f);
                _shell.sizeDelta = new Vector2(clampedWidth, 0f);

                float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
                float minGutter = Mathf.Max(8f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));
                float headerH = Mathf.Max(104f, minTouch + 8f);

                if (_headerRect != null)
                {
                    _headerRect.anchoredPosition = new Vector2(0f, -4f);
                    _headerRect.sizeDelta = new Vector2(0f, headerH);
                }

                Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(142f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                if (_backBtnRect != null)
                {
                    _backBtnRect.anchoredPosition = new Vector2(10f, 0f);
                    _backBtnRect.sizeDelta = backSize;
                }

                float titleLeft = 10f + backSize.x + minGutter + 6f;
                if (_headerAccentRect != null)
                    _headerAccentRect.anchoredPosition = new Vector2(titleLeft, 0f);

                float tabAnchorMinX = clampedWidth < 1040f ? 0.40f : 0.42f;
                if (_headerTitleRect != null)
                {
                    _headerTitleRect.anchorMin = new Vector2(0f, 0f);
                    _headerTitleRect.anchorMax = new Vector2(tabAnchorMinX, 1f);
                    _headerTitleRect.offsetMin = new Vector2(titleLeft + 14f, 8f);
                    _headerTitleRect.offsetMax = new Vector2(-minGutter * 0.5f, -8f);
                }

                if (_tabsRowRect != null)
                {
                    float padY = Mathf.Max(2f, (headerH - minTouch) * 0.5f);
                    _tabsRowRect.anchorMin = new Vector2(tabAnchorMinX, 0f);
                    _tabsRowRect.anchorMax = new Vector2(1f, 1f);
                    _tabsRowRect.offsetMin = new Vector2(minGutter * 0.5f, padY);
                    _tabsRowRect.offsetMax = new Vector2(-10f, -padY);
                }

                if (_tabsRowLayout != null)
                    _tabsRowLayout.spacing = Mathf.Max(8f, minGutter * 0.75f);

                for (int i = 0; i < _tabPillLayouts.Length; i++)
                {
                    LayoutElement le = _tabPillLayouts[i];
                    if (le == null) continue;
                    le.minWidth = minTouch;
                    le.minHeight = minTouch;
                    le.preferredHeight = minTouch;
                }

                float bodyTopInset = -(4f + headerH + 8f);
                for (int i = 0; i < _tabPanes.Length; i++)
                {
                    if (_tabPanes[i] == null) continue;
                    _tabPanes[i].offsetMin = new Vector2(0f, 8f);
                    _tabPanes[i].offsetMax = new Vector2(0f, bodyTopInset);
                }

                for (int i = 0; i < _monthButtonLayouts.Count; i++)
                {
                    LayoutElement le = _monthButtonLayouts[i];
                    if (le == null) continue;
                    le.minHeight = minTouch;
                    le.preferredHeight = minTouch;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(_shell);
                _kidsGridSizer?.SetMinCell(minTouch);
                _weaponsGridSizer?.SetMinCell(minTouch);
            }
            finally
            {
                _refreshingLayout = false;
            }
        }

        static void BuildBackdrop(RectTransform root)
        {
            Image baseFill = Ui.MakeImage("Backdrop", root, BackdropSolid);
            Stretch(baseFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            baseFill.gameObject.AddComponent<RectMask2D>();

            Texture2D mainBg = KalyeahResources.Texture("ui/home/stall") ?? KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (mainBg != null)
            {
                var rawGo = new GameObject("ArtBackdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                rawGo.transform.SetParent(baseFill.transform, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = mainBg;
                raw.color = new Color(0.90f, 0.88f, 0.96f, 0.24f);
                raw.raycastTarget = false;
                Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var fitter = rawGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = mainBg.height > 0 ? (float)mainBg.width / mainBg.height : (16f / 9f);
            }

            Image wash = Ui.MakeImage("BackdropWash", baseFill.transform, BackdropWash);
            Stretch(wash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform parent)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(104f, minTouch + 8f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, headerH));

            Image divider = Ui.MakeImage("HeaderDivider", _headerRect, DividerColor);
            Stretch(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 1.5f));

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(142f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = Ui.Panel("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize);
            Image backHit = _backBtnRect.gameObject.AddComponent<Image>();
            backHit.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform backVisual = Ui.Panel("Visual", _backBtnRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
            Image backFace = Ui.MakeSliced("Plate", backVisual, MenuArt.CtaGhost(12), Color.white, raycast: false);

            Image chev = Ui.MakeImage("Chevron", backVisual, MenuArt.GoldLight, round: false, raycast: false);
            chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            RectTransform cr = chev.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(22f, 0f);
            cr.sizeDelta = new Vector2(16f, 16f);
            cr.localEulerAngles = new Vector3(0f, 0f, 180f);

            Button backBtn = _backBtnRect.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backFace;
            backBtn.onClick.AddListener(() => _onBack?.Invoke());
            Ui.AddPressFeel(_backBtnRect, 0.95f);

            _backLabel = MakeFittedText("Label", backVisual, "BACK", 20, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_backLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 4f), new Vector2(-10f, -4f));

            _headerAccentRect = Ui.Panel("TitleAccent", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(166f, 0f), new Vector2(4f, 34f));
            Image accentImg = _headerAccentRect.gameObject.AddComponent<Image>();
            accentImg.sprite = MenuArt.AccentRail(2);
            accentImg.type = Image.Type.Sliced;
            accentImg.color = Gold;
            accentImg.raycastTarget = false;

            _headerTitle = MakeFittedText("Title", _headerRect, "KOMIKS ALMANAC", 28, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(180f, 8f), new Vector2(-8f, -8f));

            _tabsRowRect = Ui.Panel("Tabs", _headerRect, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            _tabsRowRect.offsetMin = new Vector2(8f, 4f);
            _tabsRowRect.offsetMax = new Vector2(-10f, -4f);

            _tabsRowLayout = _tabsRowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            _tabsRowLayout.spacing = 10f;
            _tabsRowLayout.childAlignment = TextAnchor.MiddleRight;
            _tabsRowLayout.childControlWidth = true;
            _tabsRowLayout.childControlHeight = true;
            _tabsRowLayout.childForceExpandWidth = true;
            _tabsRowLayout.childForceExpandHeight = true;

            Sprite idleTabSprite = MenuArt.CtaGhost(10);
            string[] defaults = { "KIDS (10)", "WEAPONS & KIT (16)", "LARONG KALYE (12)" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                tabGo.transform.SetParent(_tabsRowRect, false);
                var hitImg = tabGo.GetComponent<Image>();
                hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

                RectTransform visualRt = Ui.Panel("Visual", tabGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
                Image visualImg = Ui.MakeSliced("Plate", visualRt, idleTabSprite, Color.white, raycast: false);

                var le = tabGo.GetComponent<LayoutElement>();
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
                le.preferredWidth = i == 1 ? 220f : 185f;

                var btn = tabGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.onClick.AddListener(() => SetActiveTab(captured));
                Ui.AddPressFeel(tabGo, 0.96f);

                Text lbl = MakeFittedText("Label", visualRt, defaults[i], 19, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

                _tabPillBgs[i] = visualImg;
                _tabPillLabels[i] = lbl;
                _tabPillLayouts[i] = le;
            }
        }

        void BuildKidsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("KidsPane", parent);
            _tabPanes[TabKids] = pane;

            // Left 58%: Auto-sized grid of kids (wrapped in vertical ScrollRect fallback for high-DPI viewports)
            RectTransform gridHolder = Ui.Panel("KidsGridHolder", pane, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            gridHolder.offsetMin = Vector2.zero;
            gridHolder.offsetMax = new Vector2(-8f, 0f);

            Ui.MakeSliced("GridBg", gridHolder, MenuArt.ChamferGlass(16), Color.white, raycast: false);

            ScrollRect kidsScroll = CreateVerticalScroll("KidsScroll", gridHolder, out RectTransform gridRect);
            _ = kidsScroll;

            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.spacing = new Vector2(8f, 10f);
            grid.cellSize = new Vector2(124f, 240f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            var gridFitter = gridRect.gameObject.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            _kidsGridSizer = gridRect.gameObject.AddComponent<GridCellAutoSizer>();
            _kidsGridSizer.Configure(5, 2, new Vector2(8f, 10f), minTouch, preferredCellHeight: 0f);

            Sprite cardSprite = MenuArt.ChamferCard(CardTop, CardBottom, MenuArt.PanelBorder, 10, 1, 0.22f);
            Sprite selectOutlineSprite = MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 2);

            _kidCardOutlines.Clear();
            for (int i = 0; i < _kids.Count; i++)
            {
                int idx = i;
                KidEntry kid = _kids[i];

                var cardGo = new GameObject($"KidCard_{kid.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(gridRect, false);
                var bg = cardGo.GetComponent<Image>();
                bg.sprite = cardSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectKid(idx));
                Ui.AddPressFeel(cardGo, 0.95f);

                Image spotlight = Ui.MakeImage("Spotlight", cardGo.transform, new Color(1f, 0.94f, 0.80f, 0.14f), round: false, raycast: false);
                spotlight.sprite = MenuArt.CardSpotlight();
                Stretch(spotlight.rectTransform, new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

                Image accentBar = Ui.MakeSliced("Accent", cardGo.transform, MenuArt.AccentRail(2), kid.Accent, raycast: false);
                Stretch(accentBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -5f), new Vector2(-10f, -1f));

                RectTransform artBox = Ui.Panel("ArtBox", cardGo.transform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.90f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Texture2D portrait = LoadKidTexture(kid.Id);
                if (portrait != null)
                {
                    var rawGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    rawGo.transform.SetParent(artBox, false);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = portrait;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                    var arf = rawGo.GetComponent<AspectRatioFitter>();
                    arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    arf.aspectRatio = 0.75f;
                }
                else
                {
                    Image disc = Ui.MakeImage("StandIn", artBox, kid.Accent, round: true);
                    Stretch(disc.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
                }

                Text nameLbl = MakeFittedText("Name", cardGo.transform, kid.Name, 20, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(nameLbl.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f), new Vector2(4f, 4f), new Vector2(-4f, -2f));

                RectTransform selFrame = Ui.Fill("SelectedFrame", cardGo.transform);
                Ui.MakeSliced("Outline", selFrame, selectOutlineSprite, Color.white, raycast: false);
                Ui.MakeCornerBrackets("Brackets", selFrame, MenuArt.GoldLight);
                selFrame.gameObject.SetActive(false);

                _kidCardOutlines.Add(selFrame.gameObject);
            }

            // Right 42%: Selected Kid Inspector
            RectTransform inspector = Ui.Panel("KidInspector", pane, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            inspector.offsetMax = Vector2.zero;

            Ui.MakeSliced("Bg", inspector, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            Ui.MakeCornerBrackets("InspBrackets", inspector, new Color(Gold.r, Gold.g, Gold.b, 0.42f));

            RectTransform portraitFrame = Ui.Panel("PortraitFrame", inspector, new Vector2(0.05f, 0.53f), new Vector2(0.39f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _kidPortraitFallback = Ui.MakeSliced("PortraitPlate", portraitFrame, MenuArt.StatChip(10), Color.white, raycast: false);

            Image inspSpot = Ui.MakeImage("InspSpotlight", portraitFrame, new Color(1f, 0.94f, 0.80f, 0.18f), round: false, raycast: false);
            inspSpot.sprite = MenuArt.CardSpotlight();
            Stretch(inspSpot.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            RectTransform portraitInner = Ui.Fill("PortraitInner", portraitFrame);
            portraitInner.offsetMin = new Vector2(6f, 6f);
            portraitInner.offsetMax = new Vector2(-6f, -6f);

            var rawPortraitGo = new GameObject("PortraitRaw", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawPortraitGo.transform.SetParent(portraitInner, false);
            _kidPortrait = rawPortraitGo.GetComponent<RawImage>();
            _kidPortrait.raycastTarget = false;
            Stretch(_kidPortrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var inspArf = rawPortraitGo.GetComponent<AspectRatioFitter>();
            inspArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            inspArf.aspectRatio = 0.75f;

            Ui.MakeSliced("PortraitRim", portraitFrame, MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 2), Color.white, raycast: false);

            _kidNameText = MakeFittedText("KidName", inspector, "", 32, MenuArt.GoldLight, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            Stretch(_kidNameText.rectTransform, new Vector2(0.42f, 0.76f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            Image fairChip = Ui.MakeSliced("FairChip", inspector, MenuArt.ChamferCard(FairTop, FairBottom, FairBorder, 10, 1, 0.22f), Color.white, raycast: false);
            Stretch(fairChip.rectTransform, new Vector2(0.42f, 0.53f), new Vector2(0.96f, 0.74f), Vector2.zero, Vector2.zero);
            Ui.MakeAccentRail("FairRail", fairChip.transform, MenuArt.SuccessGreen, width: 3f, verticalInset: 10f);
            _kidBadgeText = MakeFittedText("FairText", fairChip.transform, "COSMETIC ONLY\nEqual HP, Speed & Hitbox", 18, MenuArt.Cream, TextAnchor.MiddleCenter);
            _kidBadgeText.resizeTextMinSize = 14;
            _kidBadgeText.verticalOverflow = VerticalWrapMode.Overflow;
            Ui.ClearOutline(_kidBadgeText);
            Stretch(_kidBadgeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-8f, -4f));

            _kidBioText = MakeWrappedText("Bio", inspector, "", 20, MenuArt.Cream, TextAnchor.UpperLeft);
            Ui.ClearOutline(_kidBioText);
            Stretch(_kidBioText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.49f), Vector2.zero, Vector2.zero);
        }

        void BuildWeaponsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("WeaponsPane", parent);
            _tabPanes[TabWeapons] = pane;

            // Left 60%: Scrollable auto-sized 4-column grid of all 16 Weapons & Kit cards (incl. Tirador & Bagsak)
            RectTransform leftPane = Ui.Panel("WeaponsScrollHolder", pane, new Vector2(0f, 0f), new Vector2(0.60f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftPane.offsetMax = new Vector2(-8f, 0f);
            Ui.MakeSliced("Bg", leftPane, MenuArt.ChamferGlass(16), Color.white, raycast: false);

            ScrollRect scroll = CreateVerticalScroll("WeaponsScroll", leftPane, out RectTransform content);
            _ = scroll;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(160f, 132f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            _weaponsGridSizer = content.gameObject.AddComponent<GridCellAutoSizer>();
            _weaponsGridSizer.Configure(4, 0, new Vector2(8f, 8f), minTouch, preferredCellHeight: 132f);

            Sprite cardSprite = MenuArt.ChamferCard(CardTop, CardBottom, MenuArt.PanelBorder, 10, 1, 0.22f);
            Sprite selectOutlineSprite = MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 2);

            _weaponCardOutlines.Clear();
            for (int i = 0; i < _weapons.Count; i++)
            {
                int idx = i;
                WeaponEntry w = _weapons[i];

                var cardGo = new GameObject($"WeaponCard_{w.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(content, false);
                var bg = cardGo.GetComponent<Image>();
                bg.sprite = cardSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectWeapon(idx));
                Ui.AddPressFeel(cardGo, 0.95f);

                Image tagStrip = Ui.MakeSliced("RoleStrip", cardGo.transform, MenuArt.RoundSolid(6), w.RoleColor, raycast: false);
                Stretch(tagStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -26f), new Vector2(-4f, -2f));

                Text badge = MakeFittedText("Badge", tagStrip.transform, w.RoleLabel, 17, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badge.resizeTextMinSize = 10;
                Ui.ClearOutline(badge);
                Stretch(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

                RectTransform iconBox = Ui.Panel("IconBox", cardGo.transform, new Vector2(0.06f, 0.30f), new Vector2(0.38f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Texture2D icon = LoadWeaponTexture(w.Id);
                if (icon != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    iconGo.transform.SetParent(iconBox, false);
                    var raw = iconGo.GetComponent<RawImage>();
                    raw.texture = icon;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                    var arf = iconGo.GetComponent<AspectRatioFitter>();
                    arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    arf.aspectRatio = 1f;
                }
                else
                {
                    Image orb = Ui.MakeImage("StandIn", iconBox, w.RoleColor, round: true);
                    Stretch(orb.rectTransform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), Vector2.zero, Vector2.zero);
                }

                Text title = MakeWrappedText("Title", cardGo.transform, w.Name, 18, MenuArt.Cream, TextAnchor.MiddleLeft);
                Ui.ClearOutline(title);
                Stretch(title.rectTransform, new Vector2(0.41f, 0.28f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);

                string meta = w.IsUltimate ? "100% Charge" : string.Format(CultureInfo.InvariantCulture, "{0:0.#}s CD", w.Cooldown);
                Text sub = MakeFittedText("Meta", cardGo.transform, meta, 16, Muted, TextAnchor.LowerRight, KalyeahResources.DisplayFont());
                Ui.ClearOutline(sub);
                Stretch(sub.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.95f, 0.26f), Vector2.zero, Vector2.zero);

                RectTransform selFrame = Ui.Fill("SelectedFrame", cardGo.transform);
                Ui.MakeSliced("Outline", selFrame, selectOutlineSprite, Color.white, raycast: false);
                Ui.MakeCornerBrackets("Brackets", selFrame, MenuArt.GoldLight);
                selFrame.gameObject.SetActive(false);

                _weaponCardOutlines.Add(selFrame.gameObject);
            }

            // Right 40%: Weapon & Street-Origin Inspector
            RectTransform inspector = Ui.Panel("WeaponInspector", pane, new Vector2(0.60f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            Ui.MakeSliced("Bg", inspector, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            Ui.MakeCornerBrackets("InspBrackets", inspector, new Color(Gold.r, Gold.g, Gold.b, 0.42f));

            RectTransform iconFrame = Ui.Panel("IconFrame", inspector, new Vector2(0.06f, 0.70f), new Vector2(0.32f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("Plate", iconFrame, MenuArt.StatChip(10), Color.white, raycast: false);
            Ui.MakeSliced("PlateRim", iconFrame, MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 1), Color.white, raycast: false);

            RectTransform iconInner = Ui.Fill("IconInner", iconFrame);
            iconInner.offsetMin = new Vector2(8f, 8f);
            iconInner.offsetMax = new Vector2(-8f, -8f);

            var rawGo2 = new GameObject("IconRaw", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawGo2.transform.SetParent(iconInner, false);
            _weaponIcon = rawGo2.GetComponent<RawImage>();
            _weaponIcon.raycastTarget = false;
            Stretch(_weaponIcon.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var weaponInspArf = rawGo2.GetComponent<AspectRatioFitter>();
            weaponInspArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            weaponInspArf.aspectRatio = 1f;

            _weaponRoleChip = Ui.MakeSliced("RoleChip", inspector, MenuArt.RoundSolid(8), Gold, raycast: false);
            Stretch(_weaponRoleChip.rectTransform, new Vector2(0.35f, 0.85f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            _weaponRoleText = MakeFittedText("RoleLabel", _weaponRoleChip.transform, "", 18, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_weaponRoleText);
            Stretch(_weaponRoleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

            _weaponNameText = MakeFittedText("WeaponName", inspector, "", 26, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_weaponNameText.rectTransform, new Vector2(0.35f, 0.74f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero);

            _weaponOriginText = MakeFittedText("Origin", inspector, "", 18, Teal, TextAnchor.UpperLeft);
            Ui.ClearOutline(_weaponOriginText);
            Stretch(_weaponOriginText.rectTransform, new Vector2(0.35f, 0.66f), new Vector2(0.94f, 0.74f), Vector2.zero, Vector2.zero);

            _weaponDescText = MakeWrappedText("Desc", inspector, "", 19, MenuArt.Cream, TextAnchor.UpperLeft);
            Ui.ClearOutline(_weaponDescText);
            Stretch(_weaponDescText.rectTransform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.65f), Vector2.zero, Vector2.zero);

            Image statsBox = Ui.MakeSliced("StatsBox", inspector, MenuArt.StatChip(10), Color.white, raycast: false);
            Stretch(statsBox.rectTransform, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.40f), Vector2.zero, Vector2.zero);
            Ui.MakeAccentRail("StatsRail", statsBox.transform, Gold, width: 3f, verticalInset: 10f);
            _weaponStatsText = MakeWrappedText("Stats", statsBox.transform, "", 18, MenuArt.GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_weaponStatsText);
            Stretch(_weaponStatsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-10f, -4f));

            _weaponBlurbText = MakeWrappedText("CulturalBlurb", inspector, "", 18, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(_weaponBlurbText);
            Stretch(_weaponBlurbText.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.20f), Vector2.zero, Vector2.zero);
        }

        void BuildLarongKalyeTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("LarongKalyePane", parent);
            _tabPanes[TabLarongKalye] = pane;

            // Left 34%: Scrollable 12-Month Heritage Selector
            RectTransform monthListPane = Ui.Panel("MonthList", pane, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            monthListPane.offsetMax = new Vector2(-8f, 0f);
            Ui.MakeSliced("Bg", monthListPane, MenuArt.ChamferGlass(16), Color.white, raycast: false);

            CreateVerticalScroll("MonthsScroll", monthListPane, out RectTransform monthContent);
            var vLayout = monthContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(8, 8, 8, 8);
            vLayout.spacing = 6f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            var fitter = monthContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float rowMinH = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            Sprite idleMonthSprite = MenuArt.CtaGhost(10);
            _monthButtonBgs.Clear();
            _monthButtonLabels.Clear();
            _monthButtonLayouts.Clear();
            for (int i = 0; i < _heritage.Count; i++)
            {
                int idx = i;
                HeritageEntry h = _heritage[i];
                string mShort = h.Month >= 1 && h.Month <= 12 ? MonthNamesEn[h.Month - 1].Substring(0, 3).ToUpperInvariant() : ("M" + h.Month);

                var rowGo = new GameObject($"Month_{h.Month}_{h.Name}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                rowGo.transform.SetParent(monthContent, false);
                var hitImg = rowGo.GetComponent<Image>();
                hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

                RectTransform visualRt = Ui.Panel("Visual", rowGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 54f));
                Image visualImg = Ui.MakeSliced("Plate", visualRt, idleMonthSprite, Color.white, raycast: false);

                var le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = rowMinH;
                le.preferredHeight = rowMinH;

                var btn = rowGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.onClick.AddListener(() => SelectMonth(idx));
                Ui.AddPressFeel(rowGo, 0.96f);

                Text txt = MakeFittedText("Label", visualRt, $"{mShort} · {h.Name}", 19, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
                Stretch(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-10f, -4f));

                _monthButtonBgs.Add(visualImg);
                _monthButtonLabels.Add(txt);
                _monthButtonLayouts.Add(le);
            }

            // Right 66%: Heritage Book Spread with fixed top banner + scrollable Goal & 3-column rules
            RectTransform book = Ui.Panel("HeritageBook", pane, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            book.offsetMin = new Vector2(8f, 0f);
            Ui.MakeSliced("BookBg", book, MenuArt.ChamferGlass(16), Color.white, raycast: false);

            RectTransform bookHeader = Ui.Panel("BookHeader", book, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(0f, 134f));
            bookHeader.offsetMin = new Vector2(16f, -144f);
            bookHeader.offsetMax = new Vector2(-16f, -10f);

            Image monthChip = Ui.MakeSliced("MonthChip", bookHeader, MenuArt.CtaGold(8), Color.white, raycast: false);
            Stretch(monthChip.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero);
            _heritageMonthBadge = MakeFittedText("MonthText", monthChip.transform, "", 18, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_heritageMonthBadge);
            Stretch(_heritageMonthBadge.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            _heritageTitleText = MakeFittedText("GameTitle", bookHeader, "", 30, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_heritageTitleText.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.70f), Vector2.zero, Vector2.zero);

            _heritageMetaText = MakeFittedText("Meta", bookHeader, "", 18, Teal, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_heritageMetaText);
            Stretch(_heritageMetaText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);

            RectTransform bookBodyHolder = Ui.Panel("BookScrollHolder", book, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bookBodyHolder.offsetMin = new Vector2(12f, 10f);
            bookBodyHolder.offsetMax = new Vector2(-12f, -150f);

            _heritageScroll = CreateVerticalScroll("HeritageScroll", bookBodyHolder, out RectTransform bookContent);
            var bookVLayout = bookContent.gameObject.AddComponent<VerticalLayoutGroup>();
            bookVLayout.padding = new RectOffset(4, 4, 4, 8);
            bookVLayout.spacing = 10f;
            bookVLayout.childControlWidth = true;
            bookVLayout.childControlHeight = true;
            bookVLayout.childForceExpandWidth = true;
            bookVLayout.childForceExpandHeight = false;
            var bookFitter = bookContent.gameObject.AddComponent<ContentSizeFitter>();
            bookFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var goalGo = new GameObject("GoalBanner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            goalGo.transform.SetParent(bookContent, false);
            var goalImg = goalGo.GetComponent<Image>();
            goalImg.sprite = MenuArt.ChamferCard(GoalTop, GoalBottom, MenuArt.GoldBorder, 10, 1, 0.22f);
            goalImg.type = Image.Type.Sliced;
            goalImg.color = Color.white;
            goalImg.raycastTarget = false;
            var goalLayout = goalGo.GetComponent<VerticalLayoutGroup>();
            goalLayout.padding = new RectOffset(16, 14, 10, 10);
            goalLayout.childControlWidth = true;
            goalLayout.childControlHeight = true;
            goalLayout.childForceExpandWidth = true;
            goalLayout.childForceExpandHeight = false;

            _heritageGoalText = Ui.MakeText("GoalText", goalGo.transform, "", 19, MenuArt.Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_heritageGoalText);
            _heritageGoalText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _heritageGoalText.verticalOverflow = VerticalWrapMode.Overflow;

            var colsRowGo = new GameObject("ColumnsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            colsRowGo.transform.SetParent(bookContent, false);
            var colsHLayout = colsRowGo.GetComponent<HorizontalLayoutGroup>();
            colsHLayout.spacing = 10f;
            colsHLayout.childAlignment = TextAnchor.UpperLeft;
            colsHLayout.childControlWidth = true;
            colsHLayout.childControlHeight = true;
            colsHLayout.childForceExpandWidth = true;
            colsHLayout.childForceExpandHeight = true;

            _heritageSetupText = BuildScrollableBookColumn(colsRowGo.transform, "SetupCol", "SETUP");
            _heritageHowText = BuildScrollableBookColumn(colsRowGo.transform, "HowCol", "HOW TO PLAY");
            _heritageRulesText = BuildScrollableBookColumn(colsRowGo.transform, "RulesCol", "RULES & SAFETY");
        }

        static Text BuildScrollableBookColumn(Transform parent, string name, string heading)
        {
            var colGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            colGo.transform.SetParent(parent, false);
            var bg = colGo.GetComponent<Image>();
            bg.sprite = MenuArt.StatChip(10);
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;

            var le = colGo.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 120f;

            var v = colGo.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 10, 12);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            Text head = MakeFittedText("Heading", colGo.transform, heading, 18, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(head);
            var headLe = head.gameObject.AddComponent<LayoutElement>();
            headLe.minHeight = 26f;
            headLe.preferredHeight = 26f;

            Text body = Ui.MakeText("Body", colGo.transform, "", 18, MenuArt.Cream, TextAnchor.UpperLeft);
            Ui.ClearOutline(body);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            return body;
        }

        public void SelectKid(int index)
        {
            if (_kids.Count == 0) return;
            _selectedKidIndex = Mathf.Clamp(index, 0, _kids.Count - 1);
            for (int i = 0; i < _kidCardOutlines.Count; i++)
                _kidCardOutlines[i].SetActive(i == _selectedKidIndex);

            KidEntry kid = _kids[_selectedKidIndex];
            Texture2D tex = LoadKidTexture(kid.Id);
            if (_kidPortrait != null)
            {
                _kidPortrait.texture = tex;
                _kidPortrait.enabled = tex != null;
            }
            if (_kidPortraitFallback != null)
                _kidPortraitFallback.color = Color.white;
            if (_kidNameText != null) _kidNameText.text = kid.Name.ToUpperInvariant();
            if (_kidBioText != null) _kidBioText.text = kid.Description;
        }

        public void SelectWeapon(int index)
        {
            if (_weapons.Count == 0) return;
            _selectedWeaponIndex = Mathf.Clamp(index, 0, _weapons.Count - 1);
            for (int i = 0; i < _weaponCardOutlines.Count; i++)
                _weaponCardOutlines[i].SetActive(i == _selectedWeaponIndex);

            WeaponEntry w = _weapons[_selectedWeaponIndex];
            Texture2D icon = LoadWeaponTexture(w.Id);
            if (_weaponIcon != null)
            {
                _weaponIcon.texture = icon;
                _weaponIcon.enabled = icon != null;
            }
            if (_weaponRoleChip != null) _weaponRoleChip.color = w.RoleColor;
            if (_weaponRoleText != null) _weaponRoleText.text = w.RoleLabel;
            if (_weaponNameText != null) _weaponNameText.text = w.Name;
            if (_weaponOriginText != null)
                _weaponOriginText.text = string.IsNullOrEmpty(w.StreetGame) ? "Barangay Kit" : ("Inspired by: " + w.StreetGame);
            if (_weaponDescText != null) _weaponDescText.text = w.Description;

            if (_weaponStatsText != null)
            {
                string impact = w.Heal > 0 && w.Damage > 0
                    ? $"DMG {w.Damage}  |  HEAL +{w.Heal}"
                    : w.Heal > 0
                        ? $"HEAL +{w.Heal}"
                        : w.Damage > 0
                            ? $"DMG {w.Damage}"
                            : "UTILITY / SHIELD";
                string rangePart = w.Range > 0f ? string.Format(CultureInfo.InvariantCulture, "RANGE {0:0.#}m", w.Range) : "SELF / ZONE";
                string cdPart = w.IsUltimate ? "CHARGE 100%" : string.Format(CultureInfo.InvariantCulture, "CD {0:0.##}s  |  ICE {1}", w.Cooldown, w.ElixirCost);
                _weaponStatsText.text = $"{impact}\n{rangePart}  |  {cdPart}";
            }

            if (_weaponBlurbText != null)
            {
                if (!string.IsNullOrEmpty(w.StreetGame) && _streetBlurbs.TryGetValue(w.StreetGame, out string blurb))
                    _weaponBlurbText.text = $"\"{blurb}\"";
                else
                    _weaponBlurbText.text = w.IsPrimary
                        ? "Always equipped under your resting thumb — reliable street pebble shots between superpower cooldowns."
                        : w.IsUltimate
                            ? "Charges passively and on street skirmish hits; turns teamfights at the enemy base post!"
                            : "Equip up to 3 Superpowers in Pick Kid & Loadout, or swap while Tagged on the sidewalk.";
            }
        }

        public void SelectMonth(int index)
        {
            if (_heritage.Count == 0) return;
            _selectedMonthIndex = Mathf.Clamp(index, 0, _heritage.Count - 1);
            Sprite activeSprite = MenuArt.CtaGold(10);
            Sprite idleSprite = MenuArt.CtaGhost(10);
            for (int i = 0; i < _monthButtonBgs.Count; i++)
            {
                bool active = i == _selectedMonthIndex;
                _monthButtonBgs[i].sprite = active ? activeSprite : idleSprite;
                _monthButtonBgs[i].color = Color.white;
                if (i < _monthButtonLabels.Count && _monthButtonLabels[i] != null)
                    Ui.SetTextColor(_monthButtonLabels[i], active ? Ink : MenuArt.Cream);
            }

            HeritageEntry h = _heritage[_selectedMonthIndex];
            string mName = h.Month >= 1 && h.Month <= 12 ? MonthNamesEn[h.Month - 1].ToUpperInvariant() : ("MONTH " + h.Month);
            if (_heritageMonthBadge != null) _heritageMonthBadge.text = $"MONTH {h.Month} · {mName}";
            if (_heritageTitleText != null)
                _heritageTitleText.text = string.IsNullOrEmpty(h.Also) ? h.Name : $"{h.Name}  ({h.Also})";

            string itemsJoined = h.Items != null && h.Items.Length > 0 ? string.Join(", ", h.Items) : "None";
            if (_heritageMetaText != null)
                _heritageMetaText.text = $"Players: {h.PlayersMin}–{h.PlayersMax}   |   Time: {h.Time}   |   Place: {h.Place}   |   Gear: {itemsJoined}";
            if (_heritageGoalText != null)
                _heritageGoalText.text = "GOAL: " + h.Goal;
            if (_heritageSetupText != null)
                _heritageSetupText.text = FormatBullets(h.Setup);
            if (_heritageHowText != null)
                _heritageHowText.text = FormatBullets(h.How);
            if (_heritageRulesText != null)
            {
                string rules = FormatBullets(h.Rules);
                if (!string.IsNullOrEmpty(h.Safety))
                    rules = string.IsNullOrEmpty(rules) ? ("Safety: " + h.Safety) : (rules + "\n\nSafety: " + h.Safety);
                _heritageRulesText.text = rules;
            }

            if (_heritageScroll != null && _heritageScroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_heritageScroll.content);
                _heritageScroll.verticalNormalizedPosition = 1f;
            }
        }

        void RefreshLocalization()
        {
            bool fil = SettingsStore.Current.Language == "fil";
            if (_headerTitle != null) _headerTitle.text = fil ? "ALMANAK NG KALYE" : "KOMIKS ALMANAC";
            if (_backLabel != null) _backLabel.text = fil ? "BALIK" : "BACK";
            if (_tabPillLabels[0] != null) _tabPillLabels[0].text = fil ? $"BATA ({_kids.Count})" : $"KIDS ({_kids.Count})";
            if (_tabPillLabels[1] != null) _tabPillLabels[1].text = fil ? $"GAMIT ({_weapons.Count})" : $"WEAPONS & KIT ({_weapons.Count})";
            if (_tabPillLabels[2] != null) _tabPillLabels[2].text = $"LARONG KALYE ({_heritage.Count})";
            if (_kidBadgeText != null)
                _kidBadgeText.text = fil
                    ? "PAMPORMA LANG\nPantay na HP, Bilis at Hitbox"
                    : "COSMETIC ONLY\nEqual HP, Speed & Hitbox";
        }

        void LoadCatalogData()
        {
            _kids.Clear();
            _weapons.Clear();
            _heritage.Clear();
            _streetBlurbs.Clear();

            // 1. Street-game cultural blurbs
            try
            {
                Dictionary<string, object> sg = _data.Props("rules__street_games");
                if (sg.TryGetValue("blurbs", out object blurbsObj) && blurbsObj is Dictionary<string, object> blurbs)
                {
                    foreach (KeyValuePair<string, object> kv in blurbs)
                        if (kv.Value is string s) _streetBlurbs[kv.Key] = s;
                }
            }
            catch
            {
                // Optional enrichment if missing in synthetic test DataSource.
            }

            // 2. Kids (10)
            string[] kidIds;
            try
            {
                kidIds = _data.CharacterIds();
            }
            catch
            {
                kidIds = AppSession.DefaultRoster;
            }

            for (int i = 0; i < kidIds.Length; i++)
            {
                string id = kidIds[i];
                try
                {
                    Dictionary<string, object> p = _data.Props("characters__" + id);
                    _kids.Add(new KidEntry
                    {
                        Id = id,
                        Name = ReadStr(p, "display_name", Capitalize(id)),
                        Description = ReadStr(p, "description", "Barangay street contender."),
                        Accent = ReadColor(p, "extra_color", Gold),
                    });
                }
                catch
                {
                    _kids.Add(new KidEntry { Id = id, Name = Capitalize(id), Description = "Barangay street contender.", Accent = Gold });
                }
            }

            // 3. Weapons & Kit (16: Primary Tirador + 14 Deck Superpowers + Ultimate Bagsak)
            try
            {
                Dictionary<string, object> gr = _data.Props("rules__game_rules");
                if (gr.TryGetValue("primary_weapon", out object primRef) && primRef != null)
                    AddWeaponFromDataName(DataSource.NameOfRef(primRef), isPrimary: true, isUltimate: false);

                if (gr.TryGetValue("weapons", out object listObj) && listObj is List<object> weaponRefs)
                {
                    for (int i = 0; i < weaponRefs.Count; i++)
                        AddWeaponFromDataName(DataSource.NameOfRef(weaponRefs[i]), isPrimary: false, isUltimate: false);
                }

                if (gr.TryGetValue("ultimate_weapon", out object ultRef) && ultRef != null)
                    AddWeaponFromDataName(DataSource.NameOfRef(ultRef), isPrimary: false, isUltimate: true);
            }
            catch
            {
                // Fallback if minimal test DataSource omits rules__game_rules
                AddWeaponFallback("tirador_bato", "Tirador · Bato", isPrimary: true, isUltimate: false);
                string[] fallbackDeck =
                {
                    "bato_light", "bato_heavy", "gunting_light", "gunting_heavy", "papel_trap", "papel_shield",
                    "tsinelas_light", "tsinelas_heavy", "lata", "jacks", "bola", "trumpo", "heal", "langit_lupa"
                };
                for (int i = 0; i < fallbackDeck.Length; i++)
                    AddWeaponFallback(fallbackDeck[i], Capitalize(fallbackDeck[i]), isPrimary: false, isUltimate: false);
                AddWeaponFallback("ult_bagsak", "Bagsak ng Kalye", isPrimary: false, isUltimate: true);
            }

            // 4. Larong Kalye 12-Month Heritage Almanac
            try
            {
                Dictionary<string, object> lk = _data.Props("rules__larong_kalye");
                if (lk.TryGetValue("games", out object gamesObj) && gamesObj is List<object> games)
                {
                    for (int i = 0; i < games.Count; i++)
                    {
                        if (!(games[i] is Dictionary<string, object> g)) continue;
                        _heritage.Add(new HeritageEntry
                        {
                            Month = ReadInt(g, "month", i + 1),
                            Name = ReadStr(g, "name", "Larong Kalye"),
                            Also = ReadStr(g, "also", ""),
                            PlayersMin = ReadInt(g, "players_min", 2),
                            PlayersMax = ReadInt(g, "players_max", 10),
                            Time = ReadStr(g, "time", "20 min"),
                            Place = ReadStr(g, "place", "Street or yard"),
                            Items = ReadStrings(g, "items"),
                            Goal = ReadStr(g, "goal", ""),
                            Setup = ReadStrings(g, "setup"),
                            How = ReadStrings(g, "how"),
                            Rules = ReadStrings(g, "rules"),
                            Safety = ReadStr(g, "safety", ""),
                        });
                    }
                }
            }
            catch
            {
                // Keep empty or fallback if synthetic test omits rules__larong_kalye
            }
        }

        void AddWeaponFromDataName(string dataName, bool isPrimary, bool isUltimate)
        {
            Dictionary<string, object> p = _data.Props(dataName);
            string id = ReadStr(p, "id", dataName);
            int kind = ReadInt(p, "kind", 0);
            string roleLabel = isPrimary ? "PRIMARY KIT" : isUltimate ? "ULTIMATE" : RoleLabelFor(kind);
            Color roleColor = isPrimary ? Teal : isUltimate ? Gold : RoleColorFor(kind);

            _weapons.Add(new WeaponEntry
            {
                Id = id,
                Name = ReadStr(p, "display_name", Capitalize(id)),
                ShortName = ReadStr(p, "short_name", ""),
                Description = ReadStr(p, "description", ""),
                StreetGame = ReadStr(p, "street_game", ""),
                RoleLabel = roleLabel,
                RoleColor = roleColor,
                Damage = ReadInt(p, "damage", 0),
                Heal = ReadInt(p, "heal", 0),
                ElixirCost = ReadInt(p, "elixir_cost", 0),
                Cooldown = ReadFloat(p, "cooldown", 0f),
                Range = ReadFloat(p, "max_range", 0f),
                Radius = ReadFloat(p, "radius", 0f),
                IsPrimary = isPrimary,
                IsUltimate = isUltimate,
            });
        }

        void AddWeaponFallback(string id, string name, bool isPrimary, bool isUltimate)
        {
            _weapons.Add(new WeaponEntry
            {
                Id = id,
                Name = name,
                ShortName = name,
                Description = "Barangay street kit.",
                RoleLabel = isPrimary ? "PRIMARY KIT" : isUltimate ? "ULTIMATE" : "SUPERPOWER",
                RoleColor = isPrimary ? Teal : isUltimate ? Gold : Coral,
                IsPrimary = isPrimary,
                IsUltimate = isUltimate,
            });
        }

        static string RoleLabelFor(int kind)
        {
            switch ((WeaponRoleKind)kind)
            {
                case WeaponRoleKind.CrowdControl: return "CROWD CTRL";
                case WeaponRoleKind.Block: return "SHIELD / BLOCK";
                case WeaponRoleKind.Heal: return "HEAL / MERIENDA";
                default: return "ATTACK";
            }
        }

        static Color RoleColorFor(int kind)
        {
            switch ((WeaponRoleKind)kind)
            {
                case WeaponRoleKind.CrowdControl: return new Color(0.72f, 0.52f, 0.98f, 1f);
                case WeaponRoleKind.Block: return new Color(0.32f, 0.76f, 0.98f, 1f);
                case WeaponRoleKind.Heal: return new Color(0.34f, 0.88f, 0.52f, 1f);
                default: return Coral;
            }
        }

        static Texture2D LoadKidTexture(string kidId)
        {
            if (string.IsNullOrEmpty(kidId)) return null;
            return KalyeahResources.Texture("portraits/" + kidId)
                ?? KalyeahResources.Texture("ui/avatars/" + kidId);
        }

        static Texture2D LoadWeaponTexture(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            return KalyeahResources.Texture("icons/art/" + weaponId)
                ?? KalyeahResources.Texture("icons/cards/" + weaponId)
                ?? KalyeahResources.Texture("ui/weapons/" + weaponId)
                ?? KalyeahResources.Texture("icons/" + weaponId);
        }

        static RectTransform CreateTabBody(string name, RectTransform parent)
        {
            RectTransform body = Ui.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            body.offsetMin = new Vector2(0f, 8f);
            body.offsetMax = new Vector2(0f, -116f);
            return body;
        }

        static ScrollRect CreateVerticalScroll(string name, RectTransform parent, out RectTransform content)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var maskBg = scrollGo.GetComponent<Image>();
            maskBg.color = new Color(0f, 0f, 0f, 0.01f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollRectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return scroll;
        }

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = Mathf.Max(18, maxSize);
            return t;
        }

        static Text MakeWrappedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = Mathf.Max(18, maxSize);
            return t;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static string FormatBullets(string[] items)
        {
            if (items == null || items.Length == 0) return "";
            var sb = new System.Text.StringBuilder(160);
            for (int i = 0; i < items.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(items[i])) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("• ").Append(items[i].Trim());
            }
            return sb.ToString();
        }

        static string ReadStr(Dictionary<string, object> d, string key, string fallback) =>
            d != null && d.TryGetValue(key, out object v) && v is string s && !string.IsNullOrEmpty(s) ? s : fallback;

        static int ReadInt(Dictionary<string, object> d, string key, int fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { }
            }
            return fallback;
        }

        static float ReadFloat(Dictionary<string, object> d, string key, float fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
                catch { }
            }
            return fallback;
        }

        static string[] ReadStrings(Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is List<object> list)
            {
                var arr = new string[list.Count];
                for (int i = 0; i < list.Count; i++) arr[i] = list[i]?.ToString() ?? "";
                return arr;
            }
            return Array.Empty<string>();
        }

        static Color ReadColor(Dictionary<string, object> d, string key, Color fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is Dictionary<string, object> c)
            {
                float r = ReadFloat(c, "r", fallback.r);
                float g = ReadFloat(c, "g", fallback.g);
                float b = ReadFloat(c, "b", fallback.b);
                return new Color(r, g, b, 1f);
            }
            return fallback;
        }

        static string Capitalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = raw.Replace('_', ' ');
            return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
        }

        /// <summary>Keeps a fixed-column GridLayoutGroup fitted cleanly inside its ScrollRect/container across 21:9..4:3 viewports.</summary>
        [ExecuteAlways]
        sealed class GridCellAutoSizer : MonoBehaviour
        {
            int _cols = 5;
            int _rows = 2;
            Vector2 _spacing = new Vector2(8f, 10f);
            float _minCell = 48f;
            float _preferredCellHeight;
            bool _subscribed;

            public void Configure(int cols, int rows, Vector2 spacing, float minCellPx, float preferredCellHeight = 0f)
            {
                _cols = Mathf.Max(1, cols);
                _rows = Mathf.Max(0, rows);
                _spacing = spacing;
                _minCell = Mathf.Max(48f, minCellPx);
                _preferredCellHeight = preferredCellHeight;
                Apply();
            }

            public void SetMinCell(float minCellPx)
            {
                _minCell = Mathf.Max(48f, minCellPx);
                Apply();
            }

            void OnEnable()
            {
                EnsureSubscribed();
                Apply();
            }

            void OnDisable() => Unsubscribe();

            void OnDestroy() => Unsubscribe();

            void OnRectTransformDimensionsChange() => Apply();

            void EnsureSubscribed()
            {
                if (_subscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged += HandleViewportChanged;
                _subscribed = true;
            }

            void Unsubscribe()
            {
                if (!_subscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged -= HandleViewportChanged;
                _subscribed = false;
            }

            void HandleViewportChanged()
            {
                if (this == null)
                {
                    Unsubscribe();
                    return;
                }
                Apply();
            }

            public void Apply()
            {
                var grid = GetComponent<GridLayoutGroup>();
                var rt = transform as RectTransform;
                if (grid == null || rt == null) return;

                RectTransform viewportRt = rt.parent as RectTransform;
                float width = rt.rect.width;
                if (width <= 10f && viewportRt != null)
                    width = viewportRt.rect.width;
                if (width <= 10f) return;

                float availW = Mathf.Max(48f, width - grid.padding.left - grid.padding.right);
                int fitCols = Mathf.Clamp(Mathf.FloorToInt((availW + _spacing.x) / (_minCell + _spacing.x)), 1, _cols);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = fitCols;

                float cellW = (availW - _spacing.x * (fitCols - 1)) / fitCols;
                float cellH;
                if (_rows > 0 && viewportRt != null && viewportRt.rect.height > 10f)
                {
                    float availH = Mathf.Max(48f, viewportRt.rect.height - grid.padding.top - grid.padding.bottom);
                    int effectiveRows = Mathf.CeilToInt((float)(_cols * _rows) / fitCols);
                    cellH = (availH - _spacing.y * Mathf.Max(0, effectiveRows - 1)) / Mathf.Max(1, effectiveRows);
                    cellH = Mathf.Max(_minCell, cellH);
                }
                else
                {
                    cellH = Mathf.Max(_minCell, _preferredCellHeight > 0f ? _preferredCellHeight : cellW);
                }

                grid.spacing = _spacing;
                grid.cellSize = new Vector2(Mathf.Max(_minCell, cellW), cellH);
            }
        }
    }
}
