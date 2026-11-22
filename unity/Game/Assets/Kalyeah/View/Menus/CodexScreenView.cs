using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Option A1 Editorial Barangay Almanac screen:
    /// 12 Kids (4 Puroks, 6 symmetric Rival pairs, Ate Lorna's sari-sari profiles &amp; clean busts),
    /// 36 live <c>docs/KITS.md</c> Spec Skills (read from <c>characters__*.json</c> with A2 frosted-titanium discs &amp; <see cref="MenuArt.SkillEmblem"/> icons),
    /// and the 12-Month Larong Kalye Filipino street-game heritage book (<c>rules__larong_kalye</c>).
    /// </summary>
    public sealed class CodexScreenView : IDisposable
    {
        public const int TabKids = 0;
        public const int TabWeapons = 1;
        public const int TabLarongKalye = 2;

        static readonly Color BackdropSolid = new Color(0.035f, 0.047f, 0.066f, 1f);
        static readonly Color BackdropWash = new Color(0.055f, 0.075f, 0.102f, 0.80f);
        static readonly Color ObsidianPanel = MenuArt.ObsidianGlass;
        static readonly Color ObsidianDeep = MenuArt.ObsidianSolid;
        static readonly Color ObsidianCard = new Color(0.078f, 0.104f, 0.142f, 0.88f);
        static readonly Color Paper = MenuArt.PaperWhite;
        static readonly Color Vermilion = MenuArt.SignalVermilion;
        static readonly Color TitaniumBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.24f);
        static readonly Color TitaniumBorderStrong = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.42f);
        static readonly Color HairlineColor = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.20f);
        static readonly Color CoralGold = MenuArt.CoralGold;
        static readonly Color WaterCyan = MenuArt.WaterCyan;
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color Ink = MenuArt.Ink;
        static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

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
            public string Role;
            public string PurokName;
            public string PurokLandmark;
            public string RivalName;
            public string FriendsLine;
            public string Personality;
            public string HomeAndFamily;
            public string DreamJob;
            public string AteLornaSummary;
            public readonly List<SkillEntry> Skills = new List<SkillEntry>(3);
        }

        sealed class SkillEntry
        {
            public string Id;
            public string KidId;
            public string KidName;
            public string RoleName;
            public string PurokName;
            public string Slot;
            public string Name;
            public string ShortName;
            public string Description;
            public string StreetGame;
            public int WaterCost;
            public float Cooldown;
            public int MaxCharges;
            public int Damage;
            public int Heal;
            public int WallHp;
            public float Range;
            public float Radius;
            public float Duration;
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

        sealed class MiniSkillBadgeUi
        {
            public Image DiscBg;
            public Material DiscMat;
            public Image Emblem;
            public Text NameLabel;
            public Text CostLabel;
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

        readonly List<KidEntry> _kids = new List<KidEntry>(12);
        readonly List<SkillEntry> _skills = new List<SkillEntry>(36);
        readonly List<HeritageEntry> _heritage = new List<HeritageEntry>(12);
        readonly Dictionary<string, string> _streetBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        readonly RectTransform[] _tabPanes = new RectTransform[3];
        readonly Image[] _tabPillBgs = new Image[3];
        readonly Image[] _tabUnderlines = new Image[3];
        readonly Text[] _tabPillLabels = new Text[3];
        readonly LayoutElement[] _tabPillLayouts = new LayoutElement[3];

        readonly List<GameObject> _kidCardOutlines = new List<GameObject>(12);
        readonly List<GameObject> _skillCardOutlines = new List<GameObject>(36);
        readonly List<Image> _monthButtonBgs = new List<Image>(12);
        readonly List<Text> _monthButtonLabels = new List<Text>(12);
        readonly List<LayoutElement> _monthButtonLayouts = new List<LayoutElement>(12);
        readonly MiniSkillBadgeUi[] _kidInspectorSkills = new MiniSkillBadgeUi[3];

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _headerAccentRect;
        RectTransform _headerTitleRect;
        RectTransform _tabsRowRect;
        HorizontalLayoutGroup _tabsRowLayout;
        GridCellAutoSizer _kidsGridSizer;
        GridCellAutoSizer _skillsGridSizer;
        ScrollRect _kidBioScroll;
        ScrollRect _skillInspectorScroll;
        ScrollRect _heritageScroll;

        Text _headerTitle;
        Text _backLabel;

        // Kids inspector
        RawImage _kidPortrait;
        Text _kidNameText;
        Text _kidMetaBadgeText;
        Text _kidRivalText;
        Text _kidBioText;

        // Skills inspector
        Image _skillDiscImg;
        Material _skillDiscMat;
        Image _skillEmblemImg;
        Image _skillSlotChip;
        Text _skillSlotText;
        Text _skillNameText;
        Text _skillOriginText;
        Text _skillDescText;
        Text _skillStatsText;
        Text _skillBlurbText;

        // Heritage inspector
        Text _heritageMonthBadge;
        Text _heritageTitleText;
        Text _heritageMetaText;
        Text _heritageGoalText;
        Text _heritageAteLornaText;
        Text _heritageSetupText;
        Text _heritageHowText;
        Text _heritageRulesText;

        int _activeTab;
        int _selectedKidIndex;
        int _selectedSkillIndex;
        int _selectedMonthIndex;
        bool _refreshingLayout;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public SafeArea SafeAreaComponent => _safeArea;
        public int ActiveTab => _activeTab;
        public int KidCount => _kids.Count;
        public int WeaponCardCount => _skills.Count;
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
            BuildSkillsTab(_shell);
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
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                bool active = i == _activeTab;
                if (_tabPanes[i] != null) _tabPanes[i].gameObject.SetActive(active);
                if (_tabPillBgs[i] != null)
                {
                    if (active)
                        ApplyPaperSurface(_tabPillBgs[i], cornerCut: 6f);
                    else
                        ApplyGlassSurface(_tabPillBgs[i], ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);
                }
                if (_tabUnderlines[i] != null)
                    _tabUnderlines[i].gameObject.SetActive(active);
                if (_tabPillLabels[i] != null)
                {
                    Ui.SetTextColor(_tabPillLabels[i], active ? Ink : Paper);
                    if (active) Ui.ClearOutline(_tabPillLabels[i]);
                }
            }
            _kidsGridSizer?.Apply();
            _skillsGridSizer?.Apply();
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
                float headerH = Mathf.Max(96f, minTouch + 8f);

                if (_headerRect != null)
                {
                    _headerRect.anchoredPosition = new Vector2(0f, -4f);
                    _headerRect.sizeDelta = new Vector2(0f, headerH);
                }

                Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(138f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                if (_backBtnRect != null)
                {
                    _backBtnRect.anchoredPosition = new Vector2(10f, 0f);
                    _backBtnRect.sizeDelta = backSize;
                }

                float titleLeft = 10f + backSize.x + minGutter + 6f;
                if (_headerAccentRect != null)
                    _headerAccentRect.anchoredPosition = new Vector2(titleLeft, 0f);

                float tabAnchorMinX = clampedWidth < 1040f ? 0.40f : 0.43f;
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
                _skillsGridSizer?.SetMinCell(minTouch);
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

            Texture2D mainBg = KalyeahResources.Texture("ui/home/stall")
                ?? KalyeahResources.Texture("ui/main_bg")
                ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (mainBg != null)
            {
                var rawGo = new GameObject("ArtBackdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                rawGo.transform.SetParent(baseFill.transform, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = mainBg;
                raw.color = new Color(0.95f, 0.92f, 0.86f, 0.24f);
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
            float headerH = Mathf.Max(96f, minTouch + 8f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, headerH));

            Image divider = Ui.MakeImage("HeaderDivider", _headerRect, HairlineColor);
            Stretch(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 1f));

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(138f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = Ui.Panel("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize);
            Image backHit = _backBtnRect.gameObject.AddComponent<Image>();
            backHit.color = Color.clear;

            RectTransform backVisual = Ui.Panel("Visual", _backBtnRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
            Image backFace = MakeGlassPanel("Plate", backVisual, ObsidianCard, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            Image chev = Ui.MakeImage("Chevron", backVisual, Paper, round: false, raycast: false);
            chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            RectTransform cr = chev.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(20f, 0f);
            cr.sizeDelta = new Vector2(15f, 15f);
            cr.localEulerAngles = new Vector3(0f, 0f, 180f);

            Button backBtn = _backBtnRect.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backFace;
            backBtn.onClick.AddListener(() => _onBack?.Invoke());
            UiShaders.AttachPressSpring(backBtn, 0.95f, 2f);

            _backLabel = MakeFittedText("Label", backVisual, "BACK", 19, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_backLabel);
            Stretch(_backLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(32f, 4f), new Vector2(-10f, -4f));

            _headerAccentRect = Ui.Panel("TitleAccent", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(162f, 0f), new Vector2(4f, 34f));
            Image accentImg = _headerAccentRect.gameObject.AddComponent<Image>();
            accentImg.color = Vermilion;
            accentImg.raycastTarget = false;

            _headerTitle = MakeFittedText("Title", _headerRect, "BARANGAY ALMANAC", 26, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_headerTitle);
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, new Vector2(0f, 0f), new Vector2(0.43f, 1f), new Vector2(176f, 8f), new Vector2(-8f, -8f));

            _tabsRowRect = Ui.Panel("Tabs", _headerRect, new Vector2(0.43f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            _tabsRowRect.offsetMin = new Vector2(8f, 4f);
            _tabsRowRect.offsetMax = new Vector2(-10f, -4f);

            _tabsRowLayout = _tabsRowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            _tabsRowLayout.spacing = 10f;
            _tabsRowLayout.childAlignment = TextAnchor.MiddleRight;
            _tabsRowLayout.childControlWidth = true;
            _tabsRowLayout.childControlHeight = true;
            _tabsRowLayout.childForceExpandWidth = true;
            _tabsRowLayout.childForceExpandHeight = true;

            string[] defaults = { "KIDS (12)", "SKILLS & KITS (36)", "LARONG KALYE (12)" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                tabGo.transform.SetParent(_tabsRowRect, false);
                var hitImg = tabGo.GetComponent<Image>();
                hitImg.color = Color.clear;

                RectTransform visualRt = Ui.Panel("Visual", tabGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
                Image visualImg = MakeGlassPanel("Plate", visualRt, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

                Image underline = Ui.MakeImage("ActiveBar", visualRt, Vermilion, round: false, raycast: false);
                Stretch(underline.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 3f));
                underline.gameObject.SetActive(false);

                var le = tabGo.GetComponent<LayoutElement>();
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
                le.preferredWidth = i == 1 ? 226f : 184f;

                var btn = tabGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.onClick.AddListener(() => SetActiveTab(captured));
                UiShaders.AttachPressSpring(btn, 0.96f, 2f);

                Text lbl = MakeFittedText("Label", visualRt, defaults[i], 18, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Ui.ClearOutline(lbl);
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

                _tabPillBgs[i] = visualImg;
                _tabUnderlines[i] = underline;
                _tabPillLabels[i] = lbl;
                _tabPillLayouts[i] = le;
            }
        }

        void BuildKidsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("KidsPane", parent);
            _tabPanes[TabKids] = pane;

            // Left 54%: Auto-sized 4x3 grid of all 12 Barangay Kids (zero role colors, clean busts)
            RectTransform gridHolder = Ui.Panel("KidsGridHolder", pane, new Vector2(0f, 0f), new Vector2(0.54f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            gridHolder.offsetMin = Vector2.zero;
            gridHolder.offsetMax = new Vector2(-8f, 0f);

            MakeGlassPanel("GridBg", gridHolder, ObsidianPanel, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            ScrollRect kidsScroll = CreateVerticalScroll("KidsScroll", gridHolder, out RectTransform gridRect);
            _ = kidsScroll;

            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(140f, 152f);
            grid.childAlignment = TextAnchor.UpperCenter;

            var gridFitter = gridRect.gameObject.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            _kidsGridSizer = gridRect.gameObject.AddComponent<GridCellAutoSizer>();
            _kidsGridSizer.Configure(4, 3, new Vector2(8f, 8f), minTouch, preferredCellHeight: 0f);

            _kidCardOutlines.Clear();
            for (int i = 0; i < _kids.Count; i++)
            {
                int idx = i;
                KidEntry kid = _kids[i];

                var cardGo = new GameObject($"KidCard_{kid.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(gridRect, false);
                var bg = cardGo.GetComponent<Image>();
                ApplyGlassSurface(bg, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectKid(idx));
                UiShaders.AttachPressSpring(btn, 0.95f, 2f);

                Image topHairline = Ui.MakeImage("TopRule", cardGo.transform, HairlineColor, round: false, raycast: false);
                Stretch(topHairline.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -2f), new Vector2(-10f, -1f));

                RectTransform artBox = Ui.Panel("ArtBox", cardGo.transform, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Texture2D portrait = LoadKidTexture(kid.Id);
                if (portrait != null)
                {
                    var rawGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    rawGo.transform.SetParent(artBox, false);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = portrait;
                    raw.uvRect = FullUv;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                    var arf = rawGo.GetComponent<AspectRatioFitter>();
                    arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    arf.aspectRatio = portrait.height > 0 ? (float)portrait.width / portrait.height : 0.85f;
                }
                else
                {
                    Image disc = Ui.MakeImage("StandIn", artBox, TitaniumBorderStrong, round: true, raycast: false);
                    Stretch(disc.rectTransform, new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.84f), Vector2.zero, Vector2.zero);
                }

                Text nameLbl = MakeFittedText("Name", cardGo.transform, kid.Name.ToUpperInvariant(), 17, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                nameLbl.resizeTextMinSize = 8;
                Ui.ClearOutline(nameLbl);
                Stretch(nameLbl.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.34f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

                Text purokLbl = MakeFittedText("Purok", cardGo.transform, kid.PurokName.ToUpperInvariant(), 12, Muted, TextAnchor.MiddleCenter);
                purokLbl.resizeTextMinSize = 7;
                Ui.ClearOutline(purokLbl);
                Stretch(purokLbl.rectTransform, new Vector2(0f, 0.01f), new Vector2(1f, 0.16f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

                RectTransform selFrame = Ui.Fill("SelectedFrame", cardGo.transform);
                Image selBar = Ui.MakeImage("SelectRail", selFrame, Vermilion, round: false, raycast: false);
                Stretch(selBar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(3.5f, -6f));
                Ui.MakeSliced("Outline", selFrame, MenuArt.ChamferOutline(Paper, 6, 2), Paper, raycast: false);
                selFrame.gameObject.SetActive(false);

                _kidCardOutlines.Add(selFrame.gameObject);
            }

            // Right 46%: Selected Kid Editorial Dossier (LoreCodexData + Ate Lorna + 3 Live Skill Discs)
            RectTransform inspector = Ui.Panel("KidInspector", pane, new Vector2(0.54f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            inspector.offsetMax = Vector2.zero;

            MakeGlassPanel("Bg", inspector, ObsidianPanel, TitaniumBorderStrong, Paper, cornerCut: 8f, accentW: 3f);

            // Top band (38%): Clean bust portrait + Name + Purok/Role + Rival
            RectTransform portraitFrame = Ui.Panel("PortraitFrame", inspector, new Vector2(0.04f, 0.60f), new Vector2(0.35f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            MakeGlassPanel("PortraitPlate", portraitFrame, ObsidianDeep, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            RectTransform portraitInner = Ui.Fill("PortraitInner", portraitFrame);
            portraitInner.offsetMin = new Vector2(4f, 4f);
            portraitInner.offsetMax = new Vector2(-4f, -4f);

            var rawPortraitGo = new GameObject("PortraitRaw", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawPortraitGo.transform.SetParent(portraitInner, false);
            _kidPortrait = rawPortraitGo.GetComponent<RawImage>();
            _kidPortrait.uvRect = FullUv;
            _kidPortrait.raycastTarget = false;
            Stretch(_kidPortrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var inspArf = rawPortraitGo.GetComponent<AspectRatioFitter>();
            inspArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            inspArf.aspectRatio = 0.85f;

            _kidNameText = MakeFittedText("KidName", inspector, "", 28, Paper, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            _kidNameText.resizeTextMinSize = 14;
            Ui.ClearOutline(_kidNameText);
            Stretch(_kidNameText.rectTransform, new Vector2(0.38f, 0.82f), new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero);

            Image metaChip = MakePaperPanel("MetaChip", inspector, cornerCut: 4f);
            Stretch(metaChip.rectTransform, new Vector2(0.38f, 0.71f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);
            _kidMetaBadgeText = MakeFittedText("MetaText", metaChip.transform, "", 14, Ink, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _kidMetaBadgeText.resizeTextMinSize = 9;
            Ui.ClearOutline(_kidMetaBadgeText);
            Stretch(_kidMetaBadgeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 1f), new Vector2(-8f, -1f));

            Image rivalPlate = MakeGlassPanel("RivalPlate", inspector, ObsidianDeep, TitaniumBorder, Vermilion, cornerCut: 4f, accentW: 3f);
            Stretch(rivalPlate.rectTransform, new Vector2(0.38f, 0.60f), new Vector2(0.96f, 0.69f), Vector2.zero, Vector2.zero);
            _kidRivalText = MakeFittedText("RivalText", rivalPlate.transform, "", 13, CoralGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _kidRivalText.resizeTextMinSize = 9;
            Ui.ClearOutline(_kidRivalText);
            Stretch(_kidRivalText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 1f), new Vector2(-8f, -1f));

            Image midRule = Ui.MakeImage("MidRule", inspector, HairlineColor, round: false, raycast: false);
            Stretch(midRule.rectTransform, new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.58f), Vector2.zero, new Vector2(0f, 1f));

            // Middle band (34%): Scrollable Ate Lorna & LoreCodexData bio (Overflow inside ScrollRect -> zero truncation!)
            RectTransform bioHolder = Ui.Panel("BioScrollHolder", inspector, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _kidBioScroll = CreateVerticalScroll("KidBioScroll", bioHolder, out RectTransform bioContent);
            var bioLayout = bioContent.gameObject.AddComponent<VerticalLayoutGroup>();
            bioLayout.padding = new RectOffset(4, 6, 2, 4);
            bioLayout.childControlWidth = true;
            bioLayout.childControlHeight = true;
            bioLayout.childForceExpandWidth = true;
            bioLayout.childForceExpandHeight = false;
            var bioFitter = bioContent.gameObject.AddComponent<ContentSizeFitter>();
            bioFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _kidBioText = Ui.MakeText("Bio", bioContent, "", 16, Paper, TextAnchor.UpperLeft);
            Ui.ClearOutline(_kidBioText);
            _kidBioText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _kidBioText.verticalOverflow = VerticalWrapMode.Overflow;

            Image kitRule = Ui.MakeImage("KitRule", inspector, HairlineColor, round: false, raycast: false);
            Stretch(kitRule.rectTransform, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.22f), Vector2.zero, new Vector2(0f, 1f));

            // Bottom band (18%): 3 live signature skill mini-discs (A2 Frosted-Titanium + SkillEmblem)
            RectTransform kitRow = Ui.Panel("KitStrip", inspector, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.20f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var kitLayout = kitRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            kitLayout.spacing = 8f;
            kitLayout.childAlignment = TextAnchor.MiddleCenter;
            kitLayout.childControlWidth = true;
            kitLayout.childControlHeight = true;
            kitLayout.childForceExpandWidth = true;
            kitLayout.childForceExpandHeight = true;

            for (int s = 0; s < 3; s++)
            {
                bool isUlt = s == 2;
                var slotGo = new GameObject($"KidSkill_{s}", typeof(RectTransform), typeof(Image));
                slotGo.transform.SetParent(kitRow, false);
                ApplyGlassSurface(slotGo.GetComponent<Image>(), ObsidianDeep, isUlt ? CoralGold : TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);

                RectTransform discBox = Ui.Panel("Disc", slotGo.transform, new Vector2(0.04f, 0.12f), new Vector2(0.34f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var discArf = discBox.gameObject.AddComponent<AspectRatioFitter>();
                discArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                discArf.aspectRatio = 1f;

                Image discImg = Ui.MakeImage("DiscFace", discBox, Color.white, round: false, raycast: false);
                Stretch(discImg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Material discMat = UiShaders.CreateFrostedTitaniumDisc(
                    new Color(0.075f, 0.105f, 0.145f, 0.82f),
                    isUlt ? CoralGold : MenuArt.TitaniumRim,
                    0.76f);
                ApplyDiscMaterial(discImg, discMat, isUlt ? CoralGold : ObsidianCard);

                Image emblemImg = Ui.MakeImage("Emblem", discBox, Paper, round: false, raycast: false);
                emblemImg.preserveAspect = true;
                Stretch(emblemImg.rectTransform, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f), Vector2.zero, Vector2.zero);

                Text nameTxt = MakeFittedText("SkillName", slotGo.transform, "", 13, Paper, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
                nameTxt.resizeTextMinSize = 8;
                Ui.ClearOutline(nameTxt);
                Stretch(nameTxt.rectTransform, new Vector2(0.36f, 0.46f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero);

                Text costTxt = MakeFittedText("SkillCost", slotGo.transform, "", 11, isUlt ? CoralGold : WaterCyan, TextAnchor.UpperLeft);
                costTxt.resizeTextMinSize = 8;
                Ui.ClearOutline(costTxt);
                Stretch(costTxt.rectTransform, new Vector2(0.36f, 0.08f), new Vector2(0.96f, 0.46f), Vector2.zero, Vector2.zero);

                _kidInspectorSkills[s] = new MiniSkillBadgeUi
                {
                    DiscBg = discImg,
                    DiscMat = discMat,
                    Emblem = emblemImg,
                    NameLabel = nameTxt,
                    CostLabel = costTxt,
                };
            }
        }

        void BuildSkillsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("WeaponsPane", parent);
            _tabPanes[TabWeapons] = pane;

            // Left 58%: Scrollable 4-column grid of all 36 live docs/KITS.md skills
            RectTransform leftPane = Ui.Panel("WeaponsScrollHolder", pane, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftPane.offsetMax = new Vector2(-8f, 0f);
            MakeGlassPanel("Bg", leftPane, ObsidianPanel, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            ScrollRect scroll = CreateVerticalScroll("WeaponsScroll", leftPane, out RectTransform content);
            _ = scroll;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(156f, 128f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            _skillsGridSizer = content.gameObject.AddComponent<GridCellAutoSizer>();
            _skillsGridSizer.Configure(4, 0, new Vector2(8f, 8f), minTouch, preferredCellHeight: 128f);

            _skillCardOutlines.Clear();
            for (int i = 0; i < _skills.Count; i++)
            {
                int idx = i;
                SkillEntry sk = _skills[i];

                var cardGo = new GameObject($"SkillCard_{sk.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(content, false);
                var bg = cardGo.GetComponent<Image>();
                ApplyGlassSurface(bg, ObsidianCard, sk.IsUltimate ? new Color(CoralGold.r, CoralGold.g, CoralGold.b, 0.40f) : TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectWeapon(idx));
                UiShaders.AttachPressSpring(btn, 0.95f, 2f);

                // Top editorial tag strip (Paper-White for ULT, deep obsidian+hairline for S1/S2 — zero role colors)
                Image tagStrip = Ui.MakeImage("SlotStrip", cardGo.transform, sk.IsUltimate ? Paper : ObsidianDeep, round: false, raycast: false);
                Stretch(tagStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -26f), new Vector2(-4f, -2f));

                string slotCaption = $"{sk.KidName.ToUpperInvariant()} · {SlotCode(sk.Slot)}";
                Text badge = MakeFittedText("Badge", tagStrip.transform, slotCaption, 12, sk.IsUltimate ? Ink : Muted, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badge.resizeTextMinSize = 8;
                Ui.ClearOutline(badge);
                Stretch(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

                // A2 Frosted-Titanium Skill Disc + MenuArt.SkillEmblem
                RectTransform discHolder = Ui.Panel("IconBox", cardGo.transform, new Vector2(0.05f, 0.24f), new Vector2(0.40f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var discArf = discHolder.gameObject.AddComponent<AspectRatioFitter>();
                discArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                discArf.aspectRatio = 1f;

                Image discImg = Ui.MakeImage("DiscFace", discHolder, Color.white, round: false, raycast: false);
                Stretch(discImg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Material discMat = UiShaders.CreateFrostedTitaniumDisc(
                    new Color(0.075f, 0.105f, 0.145f, 0.80f),
                    sk.IsUltimate ? CoralGold : MenuArt.TitaniumRim,
                    0.78f);
                ApplyDiscMaterial(discImg, discMat, ObsidianDeep);

                Image emblem = Ui.MakeImage("Emblem", discHolder, Paper, round: false, raycast: false);
                emblem.sprite = MenuArt.SkillEmblem(sk.Id);
                emblem.preserveAspect = true;
                Stretch(emblem.rectTransform, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f), Vector2.zero, Vector2.zero);

                Text title = MakeFittedText("Title", cardGo.transform, sk.Name, 15, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
                title.resizeTextMinSize = 9;
                Ui.ClearOutline(title);
                Stretch(title.rectTransform, new Vector2(0.42f, 0.26f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);

                string meta = sk.IsUltimate
                    ? string.Format(CultureInfo.InvariantCulture, "ULT · {0:0.#}s", sk.Cooldown)
                    : string.Format(CultureInfo.InvariantCulture, "{0} WATER · {1:0.#}s", sk.WaterCost, sk.Cooldown);
                Text sub = MakeFittedText("Meta", cardGo.transform, meta, 12, sk.IsUltimate ? CoralGold : WaterCyan, TextAnchor.LowerRight, KalyeahResources.DisplayFont());
                sub.resizeTextMinSize = 8;
                Ui.ClearOutline(sub);
                Stretch(sub.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.95f, 0.24f), Vector2.zero, Vector2.zero);

                RectTransform selFrame = Ui.Fill("SelectedFrame", cardGo.transform);
                Image selRail = Ui.MakeImage("SelectRail", selFrame, Vermilion, round: false, raycast: false);
                Stretch(selRail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(3f, -6f));
                Ui.MakeSliced("Outline", selFrame, MenuArt.ChamferOutline(Paper, 6, 2), Paper, raycast: false);
                selFrame.gameObject.SetActive(false);

                _skillCardOutlines.Add(selFrame.gameObject);
            }

            // Right 42%: Skill & Street-Origin Inspector
            RectTransform inspector = Ui.Panel("WeaponInspector", pane, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            MakeGlassPanel("Bg", inspector, ObsidianPanel, TitaniumBorderStrong, Vermilion, cornerCut: 8f, accentW: 3f);

            // Large A2 Frosted-Titanium Disc
            RectTransform discFrame = Ui.Panel("IconFrame", inspector, new Vector2(0.05f, 0.68f), new Vector2(0.30f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var inspDiscArf = discFrame.gameObject.AddComponent<AspectRatioFitter>();
            inspDiscArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            inspDiscArf.aspectRatio = 1f;

            _skillDiscImg = Ui.MakeImage("DiscFace", discFrame, Color.white, round: false, raycast: false);
            Stretch(_skillDiscImg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _skillDiscMat = UiShaders.CreateFrostedTitaniumDisc(new Color(0.075f, 0.105f, 0.145f, 0.84f), MenuArt.TitaniumRim, 0.78f);
            ApplyDiscMaterial(_skillDiscImg, _skillDiscMat, ObsidianDeep);

            _skillEmblemImg = Ui.MakeImage("Emblem", discFrame, Paper, round: false, raycast: false);
            _skillEmblemImg.preserveAspect = true;
            Stretch(_skillEmblemImg.rectTransform, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f), Vector2.zero, Vector2.zero);

            _skillSlotChip = MakePaperPanel("RoleChip", inspector, cornerCut: 4f);
            Stretch(_skillSlotChip.rectTransform, new Vector2(0.33f, 0.85f), new Vector2(0.95f, 0.94f), Vector2.zero, Vector2.zero);
            _skillSlotText = MakeFittedText("RoleLabel", _skillSlotChip.transform, "", 14, Ink, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _skillSlotText.resizeTextMinSize = 9;
            Ui.ClearOutline(_skillSlotText);
            Stretch(_skillSlotText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 1f), new Vector2(-8f, -1f));

            _skillNameText = MakeFittedText("WeaponName", inspector, "", 24, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _skillNameText.resizeTextMinSize = 12;
            Ui.ClearOutline(_skillNameText);
            Stretch(_skillNameText.rectTransform, new Vector2(0.33f, 0.74f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);

            _skillOriginText = MakeFittedText("Origin", inspector, "", 15, CoralGold, TextAnchor.UpperLeft);
            _skillOriginText.resizeTextMinSize = 9;
            Ui.ClearOutline(_skillOriginText);
            Stretch(_skillOriginText.rectTransform, new Vector2(0.33f, 0.66f), new Vector2(0.95f, 0.74f), Vector2.zero, Vector2.zero);

            Image rule = Ui.MakeImage("SkillRule", inspector, HairlineColor, round: false, raycast: false);
            Stretch(rule.rectTransform, new Vector2(0.05f, 0.64f), new Vector2(0.95f, 0.64f), Vector2.zero, new Vector2(0f, 1f));

            // Scrollable detail stack (description + live stats plate + cultural blurb -> zero truncation at 844x390!)
            RectTransform bodyHolder = Ui.Panel("SkillDetailScrollHolder", inspector, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _skillInspectorScroll = CreateVerticalScroll("SkillDetailScroll", bodyHolder, out RectTransform detailContent);
            var detailLayout = detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(4, 6, 4, 8);
            detailLayout.spacing = 10f;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            var detailFitter = detailContent.gameObject.AddComponent<ContentSizeFitter>();
            detailFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _skillDescText = Ui.MakeText("Desc", detailContent, "", 17, Paper, TextAnchor.UpperLeft);
            Ui.ClearOutline(_skillDescText);
            _skillDescText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillDescText.verticalOverflow = VerticalWrapMode.Overflow;

            var statsGo = new GameObject("StatsBox", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            statsGo.transform.SetParent(detailContent, false);
            ApplyGlassSurface(statsGo.GetComponent<Image>(), ObsidianDeep, TitaniumBorderStrong, WaterCyan, cornerCut: 6f, accentW: 3f);
            var statsLayout = statsGo.GetComponent<VerticalLayoutGroup>();
            statsLayout.padding = new RectOffset(14, 12, 10, 10);
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = false;

            _skillStatsText = Ui.MakeText("Stats", statsGo.transform, "", 15, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_skillStatsText);
            _skillStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillStatsText.verticalOverflow = VerticalWrapMode.Overflow;

            _skillBlurbText = Ui.MakeText("CulturalBlurb", detailContent, "", 15, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(_skillBlurbText);
            _skillBlurbText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillBlurbText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void BuildLarongKalyeTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("LarongKalyePane", parent);
            _tabPanes[TabLarongKalye] = pane;

            // Left 34%: Scrollable 12-Month Heritage Selector
            RectTransform monthListPane = Ui.Panel("MonthList", pane, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            monthListPane.offsetMax = new Vector2(-8f, 0f);
            MakeGlassPanel("Bg", monthListPane, ObsidianPanel, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

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
                hitImg.color = Color.clear;

                RectTransform visualRt = Ui.Panel("Visual", rowGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
                Image visualImg = MakeGlassPanel("Plate", visualRt, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);

                var le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = rowMinH;
                le.preferredHeight = rowMinH;

                var btn = rowGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.onClick.AddListener(() => SelectMonth(idx));
                UiShaders.AttachPressSpring(btn, 0.96f, 2f);

                Text txt = MakeFittedText("Label", visualRt, $"{mShort} · {h.Name}", 17, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
                txt.resizeTextMinSize = 10;
                Ui.ClearOutline(txt);
                Stretch(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-10f, -4f));

                _monthButtonBgs.Add(visualImg);
                _monthButtonLabels.Add(txt);
                _monthButtonLayouts.Add(le);
            }

            // Right 66%: Heritage Book Spread with fixed top banner + scrollable Goal, Ate Lorna note & 3-column rules
            RectTransform book = Ui.Panel("HeritageBook", pane, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            book.offsetMin = new Vector2(8f, 0f);
            MakeGlassPanel("BookBg", book, ObsidianPanel, TitaniumBorderStrong, Color.clear, cornerCut: 8f, accentW: 0f);

            RectTransform bookHeader = Ui.Panel("BookHeader", book, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(0f, 124f));
            bookHeader.offsetMin = new Vector2(16f, -134f);
            bookHeader.offsetMax = new Vector2(-16f, -10f);

            Image monthChip = MakeGlassPanel("MonthChip", bookHeader, Vermilion, Color.clear, Color.clear, cornerCut: 5f, accentW: 0f);
            Stretch(monthChip.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.40f, 1f), Vector2.zero, Vector2.zero);
            _heritageMonthBadge = MakeFittedText("MonthText", monthChip.transform, "", 15, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _heritageMonthBadge.resizeTextMinSize = 9;
            Ui.ClearOutline(_heritageMonthBadge);
            Stretch(_heritageMonthBadge.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            _heritageTitleText = MakeFittedText("GameTitle", bookHeader, "", 27, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _heritageTitleText.resizeTextMinSize = 14;
            Ui.ClearOutline(_heritageTitleText);
            Stretch(_heritageTitleText.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.70f), Vector2.zero, Vector2.zero);

            _heritageMetaText = MakeFittedText("Meta", bookHeader, "", 15, CoralGold, TextAnchor.MiddleLeft);
            _heritageMetaText.resizeTextMinSize = 9;
            Ui.ClearOutline(_heritageMetaText);
            Stretch(_heritageMetaText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);

            Image hdrRule = Ui.MakeImage("BookHeaderRule", book, HairlineColor, round: false, raycast: false);
            Stretch(hdrRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -139f), new Vector2(-16f, -138f));

            RectTransform bookBodyHolder = Ui.Panel("BookScrollHolder", book, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bookBodyHolder.offsetMin = new Vector2(12f, 10f);
            bookBodyHolder.offsetMax = new Vector2(-12f, -144f);

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
            ApplyGlassSurface(goalGo.GetComponent<Image>(), ObsidianDeep, TitaniumBorderStrong, Paper, cornerCut: 6f, accentW: 3f);
            var goalLayout = goalGo.GetComponent<VerticalLayoutGroup>();
            goalLayout.padding = new RectOffset(16, 14, 10, 10);
            goalLayout.spacing = 4f;
            goalLayout.childControlWidth = true;
            goalLayout.childControlHeight = true;
            goalLayout.childForceExpandWidth = true;
            goalLayout.childForceExpandHeight = false;

            _heritageGoalText = Ui.MakeText("GoalText", goalGo.transform, "", 17, Paper, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_heritageGoalText);
            _heritageGoalText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _heritageGoalText.verticalOverflow = VerticalWrapMode.Overflow;

            _heritageAteLornaText = Ui.MakeText("AteLornaNote", goalGo.transform, "", 14, Muted, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_heritageAteLornaText);
            _heritageAteLornaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _heritageAteLornaText.verticalOverflow = VerticalWrapMode.Overflow;

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
            ApplyGlassSurface(colGo.GetComponent<Image>(), ObsidianDeep, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

            var le = colGo.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 110f;

            var v = colGo.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 10, 12);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            Text head = MakeFittedText("Heading", colGo.transform, heading, 16, CoralGold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            head.resizeTextMinSize = 10;
            Ui.ClearOutline(head);
            var headLe = head.gameObject.AddComponent<LayoutElement>();
            headLe.minHeight = 24f;
            headLe.preferredHeight = 24f;

            Text body = Ui.MakeText("Body", colGo.transform, "", 16, Paper, TextAnchor.UpperLeft);
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
                _kidPortrait.uvRect = FullUv;
                _kidPortrait.enabled = tex != null;
            }
            if (_kidNameText != null) _kidNameText.text = kid.Name.ToUpperInvariant();
            if (_kidMetaBadgeText != null) _kidMetaBadgeText.text = $"{kid.PurokName.ToUpperInvariant()}  ·  {kid.Role.ToUpperInvariant()}";
            if (_kidRivalText != null) _kidRivalText.text = string.IsNullOrEmpty(kid.RivalName) ? "BARANGAY CREW" : $"RIVAL: {kid.RivalName.ToUpperInvariant()}";

            if (_kidBioText != null)
            {
                var sb = new StringBuilder(320);
                if (!string.IsNullOrEmpty(kid.AteLornaSummary))
                    sb.Append(LoreCodexData.AteLornaName).Append(": \"").Append(kid.AteLornaSummary).Append('"');
                else if (!string.IsNullOrEmpty(kid.Description))
                    sb.Append(kid.Description);

                if (!string.IsNullOrEmpty(kid.Personality))
                    sb.Append("\n\nVIBE: ").Append(kid.Personality);
                if (!string.IsNullOrEmpty(kid.HomeAndFamily))
                    sb.Append("\nHOME: ").Append(kid.HomeAndFamily);
                if (!string.IsNullOrEmpty(kid.DreamJob))
                    sb.Append("\nDREAM: ").Append(kid.DreamJob);
                if (!string.IsNullOrEmpty(kid.FriendsLine))
                    sb.Append("\nPUROK CREW: ").Append(kid.FriendsLine);
                if (!string.IsNullOrEmpty(kid.PurokLandmark))
                    sb.Append("\nHANGOUT: ").Append(kid.PurokLandmark);

                _kidBioText.text = sb.ToString();
            }

            for (int s = 0; s < _kidInspectorSkills.Length; s++)
            {
                MiniSkillBadgeUi ui = _kidInspectorSkills[s];
                if (ui == null) continue;
                if (s < kid.Skills.Count)
                {
                    SkillEntry sk = kid.Skills[s];
                    if (ui.Emblem != null) ui.Emblem.sprite = MenuArt.SkillEmblem(sk.Id);
                    if (ui.NameLabel != null) ui.NameLabel.text = sk.Name;
                    if (ui.CostLabel != null)
                    {
                        ui.CostLabel.text = sk.IsUltimate
                            ? string.Format(CultureInfo.InvariantCulture, "ULT · {0:0.#}s", sk.Cooldown)
                            : string.Format(CultureInfo.InvariantCulture, "{0} WATER · {1:0.#}s", sk.WaterCost, sk.Cooldown);
                    }
                    if (ui.DiscMat != null && ui.DiscMat != Graphic.defaultGraphicMaterial && ui.DiscMat.HasProperty(UiShaders.PropRimColor))
                        ui.DiscMat.SetColor(UiShaders.PropRimColor, sk.IsUltimate ? CoralGold : MenuArt.TitaniumRim);
                }
                else
                {
                    if (ui.NameLabel != null) ui.NameLabel.text = "";
                    if (ui.CostLabel != null) ui.CostLabel.text = "";
                }
            }

            if (_kidBioScroll != null && _kidBioScroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_kidBioScroll.content);
                _kidBioScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void SelectWeapon(int index)
        {
            if (_skills.Count == 0) return;
            _selectedSkillIndex = Mathf.Clamp(index, 0, _skills.Count - 1);
            for (int i = 0; i < _skillCardOutlines.Count; i++)
                _skillCardOutlines[i].SetActive(i == _selectedSkillIndex);

            SkillEntry sk = _skills[_selectedSkillIndex];
            if (_skillEmblemImg != null)
                _skillEmblemImg.sprite = MenuArt.SkillEmblem(sk.Id);
            if (_skillDiscMat != null && _skillDiscMat != Graphic.defaultGraphicMaterial && _skillDiscMat.HasProperty(UiShaders.PropRimColor))
            {
                _skillDiscMat.SetColor(UiShaders.PropRimColor, sk.IsUltimate ? Vermilion : MenuArt.TitaniumRim);
                if (_skillDiscMat.HasProperty(UiShaders.PropUltPulse))
                    _skillDiscMat.SetFloat(UiShaders.PropUltPulse, sk.IsUltimate ? 0.55f : 0f);
            }

            if (_skillSlotChip != null)
            {
                if (sk.IsUltimate)
                    ApplyGlassSurface(_skillSlotChip, Vermilion, Color.clear, Color.clear, cornerCut: 4f, accentW: 0f);
                else
                    ApplyPaperSurface(_skillSlotChip, cornerCut: 4f);
            }
            if (_skillSlotText != null)
            {
                _skillSlotText.text = $"{sk.KidName.ToUpperInvariant()}  ·  {sk.RoleName.ToUpperInvariant()}  ·  {SlotCode(sk.Slot)}";
                Ui.SetTextColor(_skillSlotText, sk.IsUltimate ? Paper : Ink);
                Ui.ClearOutline(_skillSlotText);
            }
            if (_skillNameText != null) _skillNameText.text = sk.Name;
            if (_skillOriginText != null)
                _skillOriginText.text = string.IsNullOrEmpty(sk.StreetGame)
                    ? $"{sk.PurokName} Signature Kit"
                    : $"Larong Kalye: {sk.StreetGame}  ·  {sk.PurokName}";
            if (_skillDescText != null) _skillDescText.text = sk.Description;

            if (_skillStatsText != null)
            {
                var parts = new List<string>(6);
                parts.Add(sk.IsUltimate ? "ULTIMATE · 0 WATER" : $"WATER {sk.WaterCost}");
                parts.Add(string.Format(CultureInfo.InvariantCulture, "CD {0:0.#}s", sk.Cooldown));
                if (sk.MaxCharges > 1) parts.Add($"{sk.MaxCharges} CHARGES");
                if (sk.Damage > 0) parts.Add($"DMG {sk.Damage}");
                if (sk.Heal > 0) parts.Add($"HEAL +{sk.Heal}");
                if (sk.WallHp > 0) parts.Add($"WALL {sk.WallHp} HP");
                if (sk.Range > 0f) parts.Add(string.Format(CultureInfo.InvariantCulture, "RANGE {0:0.#}m", sk.Range));
                if (sk.Radius > 0f) parts.Add(string.Format(CultureInfo.InvariantCulture, "RADIUS {0:0.#}m", sk.Radius));
                if (sk.Duration > 0f) parts.Add(string.Format(CultureInfo.InvariantCulture, "DURATION {0:0.#}s", sk.Duration));
                _skillStatsText.text = string.Join("  |  ", parts);
            }

            if (_skillBlurbText != null)
            {
                if (!string.IsNullOrEmpty(sk.StreetGame) && _streetBlurbs.TryGetValue(sk.StreetGame, out string blurb))
                    _skillBlurbText.text = $"\"{blurb}\"";
                else
                    _skillBlurbText.text = $"{LoreCodexData.AteLornaName}: \"{LoreCodexData.ResolveAteLornaIntro(sk.PurokName, _selectedSkillIndex).Text}\"";
            }

            if (_skillInspectorScroll != null && _skillInspectorScroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_skillInspectorScroll.content);
                _skillInspectorScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void SelectMonth(int index)
        {
            if (_heritage.Count == 0) return;
            _selectedMonthIndex = Mathf.Clamp(index, 0, _heritage.Count - 1);
            for (int i = 0; i < _monthButtonBgs.Count; i++)
            {
                bool active = i == _selectedMonthIndex;
                if (active)
                    ApplyPaperSurface(_monthButtonBgs[i], cornerCut: 5f);
                else
                    ApplyGlassSurface(_monthButtonBgs[i], ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);
                if (i < _monthButtonLabels.Count && _monthButtonLabels[i] != null)
                {
                    Ui.SetTextColor(_monthButtonLabels[i], active ? Ink : Paper);
                    if (active) Ui.ClearOutline(_monthButtonLabels[i]);
                }
            }

            HeritageEntry h = _heritage[_selectedMonthIndex];
            string mName = h.Month >= 1 && h.Month <= 12 ? MonthNamesEn[h.Month - 1].ToUpperInvariant() : ("MONTH " + h.Month);
            if (_heritageMonthBadge != null) _heritageMonthBadge.text = $"MONTH {h.Month} · {mName}";
            if (_heritageTitleText != null)
                _heritageTitleText.text = string.IsNullOrEmpty(h.Also) ? h.Name : $"{h.Name}  ({h.Also})";

            string itemsJoined = h.Items != null && h.Items.Length > 0 ? string.Join(", ", h.Items) : "None";
            if (_heritageMetaText != null)
                _heritageMetaText.text = $"Players: {h.PlayersMin}-{h.PlayersMax}   |   Time: {h.Time}   |   Place: {h.Place}   |   Gear: {itemsJoined}";
            if (_heritageGoalText != null)
                _heritageGoalText.text = "GOAL: " + h.Goal;
            if (_heritageAteLornaText != null)
                _heritageAteLornaText.text = $"{LoreCodexData.AteLornaName} — {LoreCodexData.ResolveAteLornaIntro(h.Name, h.Month).Text}";
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
            if (_headerTitle != null) _headerTitle.text = fil ? "ALMANAK NG BARANGAY" : "BARANGAY ALMANAC";
            if (_backLabel != null) _backLabel.text = fil ? "BALIK" : "BACK";
            if (_tabPillLabels[0] != null) _tabPillLabels[0].text = fil ? $"MGA BATA ({_kids.Count})" : $"KIDS ({_kids.Count})";
            if (_tabPillLabels[1] != null) _tabPillLabels[1].text = fil ? $"KAKAYAHAN ({_skills.Count})" : $"SKILLS & KITS ({_skills.Count})";
            if (_tabPillLabels[2] != null) _tabPillLabels[2].text = $"LARONG KALYE ({_heritage.Count})";
        }

        void LoadCatalogData()
        {
            _kids.Clear();
            _skills.Clear();
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

            // 2. All 12 Kids (AppSession.DefaultRoster + LoreCodexData) + All 36 docs/KITS.md Skills
            string[] kidIds = AppSession.DefaultRoster;
            var friendIds = new List<string>(4);
            for (int i = 0; i < kidIds.Length; i++)
            {
                string id = kidIds[i];
                bool hasLore = LoreCodexData.TryGetKid(id, out KidLoreEntry lore);
                PurokEntry purok = hasLore ? LoreCodexData.GetPurok(lore.Purok) : default;
                string rivalId = LoreCodexData.GetRivalId(id);
                string rivalName = "";
                if (!string.IsNullOrEmpty(rivalId))
                    rivalName = LoreCodexData.TryGetKid(rivalId, out KidLoreEntry rivalLore) ? rivalLore.DisplayName : Capitalize(rivalId);

                LoreCodexData.GetPurokFriends(id, friendIds);
                var friendNames = new List<string>(friendIds.Count);
                for (int f = 0; f < friendIds.Count; f++)
                {
                    friendNames.Add(LoreCodexData.TryGetKid(friendIds[f], out KidLoreEntry fLore)
                        ? fLore.DisplayName
                        : Capitalize(friendIds[f]));
                }

                Dictionary<string, object> charProps = null;
                try
                {
                    charProps = _data.Props("characters__" + id);
                }
                catch
                {
                    charProps = null;
                }

                string displayName = ReadStr(charProps, "display_name", hasLore ? lore.DisplayName : Capitalize(id));
                string roleName = hasLore ? LoreCodexData.RoleName(lore.Role) : Capitalize(ReadStr(charProps, "role", "Striker"));
                string purokName = hasLore && !string.IsNullOrEmpty(purok.Name) ? purok.Name : "Barangay";

                var entry = new KidEntry
                {
                    Id = id,
                    Name = displayName,
                    Description = ReadStr(charProps, "description", hasLore ? lore.AteLornaCodexSummary : "Barangay street contender."),
                    Role = roleName,
                    PurokName = purokName,
                    PurokLandmark = hasLore ? purok.Description : "",
                    RivalName = rivalName,
                    FriendsLine = friendNames.Count > 0 ? string.Join(", ", friendNames) : "",
                    Personality = hasLore ? lore.Personality : "",
                    HomeAndFamily = hasLore ? lore.HomeAndFamily : "",
                    DreamJob = hasLore ? lore.DreamJob : "",
                    AteLornaSummary = hasLore ? lore.AteLornaCodexSummary : "",
                };

                // Read 3 live skills directly from characters__<id>.json
                int addedSkills = 0;
                if (charProps != null && charProps.TryGetValue("skills", out object skillsObj) && skillsObj is List<object> rawSkills)
                {
                    for (int s = 0; s < rawSkills.Count; s++)
                    {
                        if (!(rawSkills[s] is Dictionary<string, object> sd)) continue;
                        string slot = ReadStr(sd, "slot", s == 0 ? "s1" : s == 1 ? "s2" : "ult");
                        bool isUlt = string.Equals(slot, "ult", StringComparison.OrdinalIgnoreCase);
                        string skillId = ReadStr(sd, "id", $"{id}_{slot}");
                        var skill = new SkillEntry
                        {
                            Id = skillId,
                            KidId = id,
                            KidName = displayName,
                            RoleName = roleName,
                            PurokName = purokName,
                            Slot = slot,
                            Name = ReadStr(sd, "display_name", Capitalize(skillId)),
                            ShortName = ReadStr(sd, "short_name", ""),
                            Description = ReadStr(sd, "description", "Barangay street skill."),
                            StreetGame = ReadStr(sd, "street_game", ""),
                            WaterCost = isUlt ? 0 : ReadInt(sd, "water_cost", 25),
                            Cooldown = ReadFloat(sd, "cooldown", isUlt ? 85f : 10f),
                            MaxCharges = ReadInt(sd, "max_charges", 1),
                            Damage = ReadInt(sd, "damage", 0),
                            Heal = ReadInt(sd, "heal", 0),
                            WallHp = ReadInt(sd, "wall_hp", 0),
                            Range = ReadFloat(sd, "max_range", 0f),
                            Radius = ReadFloat(sd, "radius", 0f),
                            Duration = ReadFloat(sd, "duration", 0f),
                            IsUltimate = isUlt,
                        };
                        entry.Skills.Add(skill);
                        _skills.Add(skill);
                        addedSkills++;
                    }
                }

                // Fallback for minimal synthetic test DataSources lacking character skills arrays
                if (addedSkills == 0)
                {
                    string[] fallbackSlots = { "s1", "s2", "ult" };
                    for (int s = 0; s < 3; s++)
                    {
                        bool isUlt = s == 2;
                        var skill = new SkillEntry
                        {
                            Id = $"{id}_{fallbackSlots[s]}",
                            KidId = id,
                            KidName = displayName,
                            RoleName = roleName,
                            PurokName = purokName,
                            Slot = fallbackSlots[s],
                            Name = isUlt ? $"{displayName} Ultimate" : $"{displayName} Skill {s + 1}",
                            ShortName = SlotCode(fallbackSlots[s]),
                            Description = entry.Description,
                            StreetGame = "Larong Kalye",
                            WaterCost = isUlt ? 0 : 25,
                            Cooldown = isUlt ? 85f : 10f,
                            MaxCharges = 1,
                            IsUltimate = isUlt,
                        };
                        entry.Skills.Add(skill);
                        _skills.Add(skill);
                    }
                }

                _kids.Add(entry);
            }

            // 3. Larong Kalye 12-Month Heritage Almanac
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
                // Keep empty if synthetic test omits rules__larong_kalye.
            }
        }

        static string SlotCode(string slot)
        {
            if (string.Equals(slot, "ult", StringComparison.OrdinalIgnoreCase)) return "ULT";
            if (string.Equals(slot, "s2", StringComparison.OrdinalIgnoreCase)) return "S2";
            return "S1";
        }

        static Texture2D LoadKidTexture(string kidId)
        {
            if (string.IsNullOrEmpty(kidId)) return null;
            return KalyeahResources.KidBust(kidId)
                ?? KalyeahResources.Texture("portraits/faces/" + kidId)
                ?? KalyeahResources.Texture("portraits/" + kidId);
        }

        static Image MakeGlassPanel(string name, Transform parent, Color fill, Color border, Color leftAccent, float cornerCut = 6f, float accentW = 0f)
        {
            Image img = Ui.MakeImage(name, parent, Color.white, round: false, raycast: false);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplyGlassSurface(img, fill, border, leftAccent, cornerCut, accentW);
            return img;
        }

        static Image MakePaperPanel(string name, Transform parent, float cornerCut = 4f)
        {
            Image img = Ui.MakeImage(name, parent, Paper, round: false, raycast: false);
            ApplyPaperSurface(img, cornerCut);
            return img;
        }

        static void ApplyGlassSurface(Image img, Color fill, Color border, Color leftAccent, float cornerCut, float accentW)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            Material mat = UiShaders.CreateTacticalGlass(fill, border, leftAccent, cornerCut, accentW, topHighlight: 0.14f);
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = fill;
            }
        }

        static void ApplyPaperSurface(Image img, float cornerCut)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            Material mat = UiShaders.CreateTacticalGlass(Paper, TitaniumBorderStrong, Color.clear, cornerCut, accentW: 0f, topHighlight: 0.06f);
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = Paper;
            }
        }

        static void ApplyDiscMaterial(Image img, Material mat, Color fallback)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = fallback;
            }
        }

        static RectTransform CreateTabBody(string name, RectTransform parent)
        {
            RectTransform body = Ui.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            body.offsetMin = new Vector2(0f, 8f);
            body.offsetMax = new Vector2(0f, -108f);
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
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(14, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 9;
            t.resizeTextMaxSize = Mathf.Max(14, maxSize);
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
            var sb = new StringBuilder(160);
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
            int _cols = 4;
            int _rows = 3;
            Vector2 _spacing = new Vector2(8f, 8f);
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
