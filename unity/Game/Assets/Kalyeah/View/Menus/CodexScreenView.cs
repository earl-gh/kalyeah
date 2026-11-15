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

        static readonly Color BgTop = new Color(0.11f, 0.16f, 0.30f, 1f);
        static readonly Color BgBottom = new Color(0.05f, 0.08f, 0.17f, 1f);
        static readonly Color HeaderBg = new Color(0.06f, 0.10f, 0.22f, 0.94f);
        static readonly Color CardBg = new Color(0.10f, 0.15f, 0.28f, 0.95f);
        static readonly Color CardSelectedBg = new Color(0.20f, 0.32f, 0.56f, 0.98f);
        static readonly Color InspectorBg = new Color(0.08f, 0.12f, 0.24f, 0.97f);
        static readonly Color Gold = new Color(0.99f, 0.79f, 0.20f, 1f);
        static readonly Color Coral = new Color(0.96f, 0.38f, 0.26f, 1f);
        static readonly Color Teal = new Color(0.24f, 0.80f, 0.72f, 1f);
        static readonly Color Muted = new Color(0.76f, 0.83f, 0.94f, 1f);
        static readonly Color Ink = new Color(0.08f, 0.10f, 0.16f, 1f);

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

        readonly DataSource _data;
        readonly Action _onBack;
        readonly Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _shell;

        readonly List<KidEntry> _kids = new List<KidEntry>(10);
        readonly List<WeaponEntry> _weapons = new List<WeaponEntry>(16);
        readonly List<HeritageEntry> _heritage = new List<HeritageEntry>(12);
        readonly Dictionary<string, string> _streetBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        readonly RectTransform[] _tabPanes = new RectTransform[3];
        readonly Image[] _tabPillBgs = new Image[3];
        readonly Text[] _tabPillLabels = new Text[3];

        readonly List<Image> _kidCardBgs = new List<Image>(10);
        readonly List<Image> _weaponCardBgs = new List<Image>(16);
        readonly List<Image> _monthButtonBgs = new List<Image>(12);

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
        bool _disposed;

        public RectTransform Root { get; }
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

            RectTransform safeRect = Ui.Fill("SafeArea", Root);
            _safeArea = safeRect.gameObject.AddComponent<SafeArea>();
            _safeArea.Changed += OnSafeAreaChanged;

            _shell = Ui.Panel("Shell", safeRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 0f));

            BuildHeader(_shell);
            BuildKidsTab(_shell);
            BuildWeaponsTab(_shell);
            BuildLarongKalyeTab(_shell);

            _safeArea.Apply();
            UpdateShellWidth();

            SettingsStore.Current.Changed += RefreshLocalization;
            RefreshLocalization();
            SelectKid(0);
            SelectWeapon(0);
            SelectMonth(0);
            SetActiveTab(TabKids);
        }

        public void Show()
        {
            if (Root != null) Root.gameObject.SetActive(true);
            _safeArea?.Apply();
            UpdateShellWidth();
            RefreshLocalization();
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
                if (_tabPillBgs[i] != null) _tabPillBgs[i].color = active ? Gold : CardBg;
                if (_tabPillLabels[i] != null) _tabPillLabels[i].color = active ? Ink : Ui.Cream;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SettingsStore.Current.Changed -= RefreshLocalization;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void OnSafeAreaChanged(SafeArea _) => UpdateShellWidth();

        void UpdateShellWidth()
        {
            if (_shell == null) return;
            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float clampedWidth = Mathf.Clamp(safeSize.x - 16f, 640f, 1240f);
            _shell.sizeDelta = new Vector2(clampedWidth, 0f);
        }

        static void BuildBackdrop(RectTransform root)
        {
            Image baseFill = Ui.MakeImage("BackdropBottom", root, BgBottom);
            Stretch(baseFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image topGlow = Ui.MakeImage("BackdropTop", root, BgTop);
            Stretch(topGlow.rectTransform, new Vector2(0f, 0.38f), Vector2.one, Vector2.zero, Vector2.zero);

            Image stripe = Ui.MakeImage("SunStripe", root, new Color(0.99f, 0.72f, 0.22f, 0.12f));
            Stretch(stripe.rectTransform, new Vector2(0f, 0.86f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform parent)
        {
            RectTransform bar = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(0f, 64f));
            Image bg = Ui.MakeImage("HeaderBg", bar, HeaderBg);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(132f, 50f), ScreenFitMetrics.MinTouchDp, _canvas);
            Button backBtn = MakeButton("BackButton", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize, Coral, () => _onBack?.Invoke(), out _backLabel, "<- BACK", 20, Ui.Cream);
            _ = backBtn;

            _headerTitle = MakeFittedText("Title", bar, "KOMIKS ALMANAC", 26, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_headerTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(154f, 6f), new Vector2(-8f, -6f));

            RectTransform tabsRow = Ui.Panel("Tabs", bar, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            tabsRow.offsetMin = new Vector2(4f, 6f);
            tabsRow.offsetMax = new Vector2(-10f, -6f);

            var layout = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            float minTabH = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            string[] defaults = { "KIDS (10)", "WEAPONS & KIT (16)", "LARONG KALYE (12)" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                tabGo.transform.SetParent(tabsRow, false);
                var img = tabGo.GetComponent<Image>();
                img.color = CardBg;
                var le = tabGo.GetComponent<LayoutElement>();
                le.minHeight = Mathf.Max(48f, minTabH);
                le.preferredWidth = i == 1 ? 220f : 185f;

                var btn = tabGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetActiveTab(captured));

                Text lbl = MakeFittedText("Label", tabGo.transform, defaults[i], 19, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

                _tabPillBgs[i] = img;
                _tabPillLabels[i] = lbl;
            }
        }

        void BuildKidsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("KidsPane", parent);
            _tabPanes[TabKids] = pane;

            // Left 60%: 2x5 grid of all 10 kids
            RectTransform gridHolder = Ui.Panel("KidsGridHolder", pane, new Vector2(0f, 0f), new Vector2(0.60f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            gridHolder.offsetMin = new Vector2(0f, 0f);
            gridHolder.offsetMax = new Vector2(-8f, 0f);

            Image gridPlate = Ui.MakeImage("GridBg", gridHolder, new Color(0.06f, 0.09f, 0.19f, 0.82f));
            Stretch(gridPlate.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform gridRect = Ui.Fill("KidsGrid2x5", gridHolder);
            gridRect.offsetMin = new Vector2(10f, 10f);
            gridRect.offsetMax = new Vector2(-10f, -10f);

            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.spacing = new Vector2(8f, 10f);
            grid.cellSize = new Vector2(128f, 250f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            // Auto-fit 2x5 cells cleanly when width shrinks on 4:3 foldables
            gridRect.gameObject.AddComponent<GridCellAutoSizer>().Configure(5, 2, new Vector2(8f, 10f), minCellPx: ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas));

            _kidCardBgs.Clear();
            for (int i = 0; i < _kids.Count; i++)
            {
                int idx = i;
                KidEntry kid = _kids[i];

                var cardGo = new GameObject($"KidCard_{kid.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(gridRect, false);
                var bg = cardGo.GetComponent<Image>();
                bg.color = CardBg;
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectKid(idx));

                Image accentBar = Ui.MakeImage("Accent", cardGo.transform, kid.Accent);
                Stretch(accentBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

                RectTransform artBox = Ui.Panel("ArtBox", cardGo.transform, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.90f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Texture2D portrait = LoadKidTexture(kid.Id);
                if (portrait != null)
                {
                    var rawGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
                    rawGo.transform.SetParent(artBox, false);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = portrait;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
                else
                {
                    Image disc = Ui.MakeImage("StandIn", artBox, kid.Accent, round: true);
                    Stretch(disc.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
                }

                Text nameLbl = MakeFittedText("Name", cardGo.transform, kid.Name, 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(nameLbl.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f), new Vector2(4f, 4f), new Vector2(-4f, -2f));

                _kidCardBgs.Add(bg);
            }

            // Right 40%: Selected Kid Inspector
            RectTransform inspector = Ui.Panel("KidInspector", pane, new Vector2(0.60f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            inspector.offsetMax = Vector2.zero;

            Image inspBg = Ui.MakeImage("Bg", inspector, InspectorBg);
            Stretch(inspBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform portraitFrame = Ui.Panel("PortraitFrame", inspector, new Vector2(0.06f, 0.54f), new Vector2(0.46f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _kidPortraitFallback = Ui.MakeImage("PortraitPlate", portraitFrame, new Color(0.16f, 0.24f, 0.42f, 1f));
            Stretch(_kidPortraitFallback.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var rawPortraitGo = new GameObject("PortraitRaw", typeof(RectTransform), typeof(RawImage));
            rawPortraitGo.transform.SetParent(portraitFrame, false);
            _kidPortrait = rawPortraitGo.GetComponent<RawImage>();
            _kidPortrait.raycastTarget = false;
            Stretch(_kidPortrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            _kidNameText = MakeFittedText("KidName", inspector, "", 32, Gold, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            Stretch(_kidNameText.rectTransform, new Vector2(0.49f, 0.74f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            Image fairChip = Ui.MakeImage("FairChip", inspector, new Color(0.14f, 0.46f, 0.38f, 0.95f));
            Stretch(fairChip.rectTransform, new Vector2(0.49f, 0.56f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);
            _kidBadgeText = MakeWrappedText("FairText", fairChip.transform, "COSMETIC ONLY\nEqual HP, Speed & Hitbox", 18, Ui.Cream, TextAnchor.MiddleCenter);
            Stretch(_kidBadgeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            _kidBioText = MakeWrappedText("Bio", inspector, "", 20, Ui.Cream, TextAnchor.UpperLeft);
            Stretch(_kidBioText.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.50f), Vector2.zero, Vector2.zero);
        }

        void BuildWeaponsTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("WeaponsPane", parent);
            _tabPanes[TabWeapons] = pane;

            // Left 62%: Scrollable 4-column grid of all 16 Weapons & Kit cards (incl. Tirador & Bagsak)
            RectTransform leftPane = Ui.Panel("WeaponsScrollHolder", pane, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftPane.offsetMax = new Vector2(-8f, 0f);
            Image leftBg = Ui.MakeImage("Bg", leftPane, new Color(0.06f, 0.09f, 0.19f, 0.82f));
            Stretch(leftBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            ScrollRect scroll = CreateVerticalScroll("WeaponsScroll", leftPane, out RectTransform content);
            _ = scroll;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(168f, 132f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _weaponCardBgs.Clear();
            for (int i = 0; i < _weapons.Count; i++)
            {
                int idx = i;
                WeaponEntry w = _weapons[i];

                var cardGo = new GameObject($"WeaponCard_{w.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(content, false);
                var bg = cardGo.GetComponent<Image>();
                bg.color = CardBg;
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectWeapon(idx));

                Image tagStrip = Ui.MakeImage("RoleStrip", cardGo.transform, w.RoleColor);
                Stretch(tagStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), Vector2.zero);

                Text badge = MakeFittedText("Badge", tagStrip.transform, w.RoleLabel, 18, Ink, TextAnchor.MiddleCenter);
                badge.GetComponent<Outline>().enabled = false;
                Stretch(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

                Texture2D icon = LoadWeaponTexture(w.Id);
                if (icon != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                    iconGo.transform.SetParent(cardGo.transform, false);
                    var raw = iconGo.GetComponent<RawImage>();
                    raw.texture = icon;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, new Vector2(0.06f, 0.30f), new Vector2(0.40f, 0.78f), Vector2.zero, Vector2.zero);
                }
                else
                {
                    Image orb = Ui.MakeImage("StandIn", cardGo.transform, w.RoleColor, round: true);
                    Stretch(orb.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.36f, 0.74f), Vector2.zero, Vector2.zero);
                }

                Text title = MakeWrappedText("Title", cardGo.transform, w.Name, 18, Ui.Cream, TextAnchor.MiddleLeft);
                Stretch(title.rectTransform, new Vector2(0.42f, 0.28f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);

                string meta = w.IsUltimate ? "100% Charge" : string.Format(CultureInfo.InvariantCulture, "{0:0.#}s CD", w.Cooldown);
                Text sub = MakeFittedText("Meta", cardGo.transform, meta, 18, Muted, TextAnchor.LowerRight);
                Stretch(sub.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.95f, 0.26f), Vector2.zero, Vector2.zero);

                _weaponCardBgs.Add(bg);
            }

            // Right 38%: Weapon & Street-Origin Inspector
            RectTransform inspector = Ui.Panel("WeaponInspector", pane, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            inspector.offsetMin = new Vector2(8f, 0f);
            Image inspBg = Ui.MakeImage("Bg", inspector, InspectorBg);
            Stretch(inspBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform iconFrame = Ui.Panel("IconFrame", inspector, new Vector2(0.06f, 0.70f), new Vector2(0.32f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image iconPlate = Ui.MakeImage("Plate", iconFrame, new Color(0.14f, 0.20f, 0.36f, 1f));
            Stretch(iconPlate.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var rawGo2 = new GameObject("IconRaw", typeof(RectTransform), typeof(RawImage));
            rawGo2.transform.SetParent(iconFrame, false);
            _weaponIcon = rawGo2.GetComponent<RawImage>();
            _weaponIcon.raycastTarget = false;
            Stretch(_weaponIcon.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            _weaponRoleChip = Ui.MakeImage("RoleChip", inspector, Gold);
            Stretch(_weaponRoleChip.rectTransform, new Vector2(0.35f, 0.85f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            _weaponRoleText = MakeFittedText("RoleLabel", _weaponRoleChip.transform, "", 18, Ink, TextAnchor.MiddleCenter);
            _weaponRoleText.GetComponent<Outline>().enabled = false;
            Stretch(_weaponRoleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

            _weaponNameText = MakeFittedText("WeaponName", inspector, "", 26, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_weaponNameText.rectTransform, new Vector2(0.35f, 0.74f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero);

            _weaponOriginText = MakeFittedText("Origin", inspector, "", 18, Teal, TextAnchor.UpperLeft);
            Stretch(_weaponOriginText.rectTransform, new Vector2(0.35f, 0.66f), new Vector2(0.94f, 0.74f), Vector2.zero, Vector2.zero);

            _weaponDescText = MakeWrappedText("Desc", inspector, "", 19, Ui.Cream, TextAnchor.UpperLeft);
            Stretch(_weaponDescText.rectTransform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.65f), Vector2.zero, Vector2.zero);

            Image statsBox = Ui.MakeImage("StatsBox", inspector, new Color(0.05f, 0.08f, 0.16f, 0.9f));
            Stretch(statsBox.rectTransform, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.40f), Vector2.zero, Vector2.zero);
            _weaponStatsText = MakeWrappedText("Stats", statsBox.transform, "", 18, Gold, TextAnchor.MiddleLeft);
            Stretch(_weaponStatsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));

            _weaponBlurbText = MakeWrappedText("CulturalBlurb", inspector, "", 18, Muted, TextAnchor.UpperLeft);
            Stretch(_weaponBlurbText.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.20f), Vector2.zero, Vector2.zero);
        }

        void BuildLarongKalyeTab(RectTransform parent)
        {
            RectTransform pane = CreateTabBody("LarongKalyePane", parent);
            _tabPanes[TabLarongKalye] = pane;

            // Left 34%: Scrollable 12-Month Heritage Selector
            RectTransform monthListPane = Ui.Panel("MonthList", pane, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            monthListPane.offsetMax = new Vector2(-8f, 0f);
            Image listBg = Ui.MakeImage("Bg", monthListPane, new Color(0.06f, 0.09f, 0.19f, 0.85f));
            Stretch(listBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

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

            float rowMinH = Mathf.Max(50f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas));
            _monthButtonBgs.Clear();
            for (int i = 0; i < _heritage.Count; i++)
            {
                int idx = i;
                HeritageEntry h = _heritage[i];
                string mShort = h.Month >= 1 && h.Month <= 12 ? MonthNamesEn[h.Month - 1].Substring(0, 3).ToUpperInvariant() : ("M" + h.Month);

                var rowGo = new GameObject($"Month_{h.Month}_{h.Name}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                rowGo.transform.SetParent(monthContent, false);
                var bg = rowGo.GetComponent<Image>();
                bg.color = CardBg;
                var le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = rowMinH;
                le.preferredHeight = rowMinH;

                var btn = rowGo.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectMonth(idx));

                Text txt = MakeFittedText("Label", rowGo.transform, $"{mShort} · {h.Name}", 19, Ui.Cream, TextAnchor.MiddleLeft);
                Stretch(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-10f, -4f));

                _monthButtonBgs.Add(bg);
            }

            // Right 66%: Heritage Book Spread
            RectTransform book = Ui.Panel("HeritageBook", pane, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            book.offsetMin = new Vector2(8f, 0f);
            Image bookBg = Ui.MakeImage("BookBg", book, InspectorBg);
            Stretch(bookBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image monthChip = Ui.MakeImage("MonthChip", book, Gold);
            Stretch(monthChip.rectTransform, new Vector2(0.04f, 0.88f), new Vector2(0.36f, 0.96f), Vector2.zero, Vector2.zero);
            _heritageMonthBadge = MakeFittedText("MonthText", monthChip.transform, "", 18, Ink, TextAnchor.MiddleCenter);
            _heritageMonthBadge.GetComponent<Outline>().enabled = false;
            Stretch(_heritageMonthBadge.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            _heritageTitleText = MakeFittedText("GameTitle", book, "", 32, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_heritageTitleText.rectTransform, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero);

            _heritageMetaText = MakeFittedText("Meta", book, "", 18, Teal, TextAnchor.MiddleLeft);
            Stretch(_heritageMetaText.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.76f), Vector2.zero, Vector2.zero);

            Image goalBanner = Ui.MakeImage("GoalBanner", book, new Color(0.16f, 0.25f, 0.44f, 0.95f));
            Stretch(goalBanner.rectTransform, new Vector2(0.04f, 0.56f), new Vector2(0.96f, 0.67f), Vector2.zero, Vector2.zero);
            _heritageGoalText = MakeWrappedText("GoalText", goalBanner.transform, "", 19, Ui.Cream, TextAnchor.MiddleLeft);
            Stretch(_heritageGoalText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));

            _heritageSetupText = BuildBookColumn(book, "SetupCol", new Vector2(0.04f, 0.05f), new Vector2(0.33f, 0.53f), "SETUP");
            _heritageHowText = BuildBookColumn(book, "HowCol", new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.53f), "HOW TO PLAY");
            _heritageRulesText = BuildBookColumn(book, "RulesCol", new Vector2(0.67f, 0.05f), new Vector2(0.96f, 0.53f), "RULES & SAFETY");
        }

        static Text BuildBookColumn(RectTransform parent, string name, Vector2 min, Vector2 max, string heading)
        {
            RectTransform col = Ui.Panel(name, parent, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image bg = Ui.MakeImage("ColBg", col, new Color(0.05f, 0.08f, 0.16f, 0.88f));
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text head = MakeFittedText("Heading", col, heading, 18, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(head.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, -4f));

            Text body = MakeWrappedText("Body", col, "", 18, Ui.Cream, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.83f), new Vector2(10f, 8f), new Vector2(-10f, -2f));
            return body;
        }

        public void SelectKid(int index)
        {
            if (_kids.Count == 0) return;
            _selectedKidIndex = Mathf.Clamp(index, 0, _kids.Count - 1);
            for (int i = 0; i < _kidCardBgs.Count; i++)
                _kidCardBgs[i].color = i == _selectedKidIndex ? CardSelectedBg : CardBg;

            KidEntry kid = _kids[_selectedKidIndex];
            Texture2D tex = LoadKidTexture(kid.Id);
            if (_kidPortrait != null)
            {
                _kidPortrait.texture = tex;
                _kidPortrait.enabled = tex != null;
            }
            if (_kidPortraitFallback != null)
                _kidPortraitFallback.color = new Color(kid.Accent.r * 0.45f, kid.Accent.g * 0.45f, kid.Accent.b * 0.45f, 1f);
            if (_kidNameText != null) _kidNameText.text = kid.Name.ToUpperInvariant();
            if (_kidBioText != null) _kidBioText.text = kid.Description;
        }

        public void SelectWeapon(int index)
        {
            if (_weapons.Count == 0) return;
            _selectedWeaponIndex = Mathf.Clamp(index, 0, _weapons.Count - 1);
            for (int i = 0; i < _weaponCardBgs.Count; i++)
                _weaponCardBgs[i].color = i == _selectedWeaponIndex ? CardSelectedBg : CardBg;

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
            for (int i = 0; i < _monthButtonBgs.Count; i++)
                _monthButtonBgs[i].color = i == _selectedMonthIndex ? CardSelectedBg : CardBg;

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
        }

        void RefreshLocalization()
        {
            bool fil = SettingsStore.Current.Language == "fil";
            if (_headerTitle != null) _headerTitle.text = fil ? "ALMANAK NG KALYE" : "KOMIKS ALMANAC";
            if (_backLabel != null) _backLabel.text = fil ? "<- BALIK" : "<- BACK";
            if (_tabPillLabels[0] != null) _tabPillLabels[0].text = fil ? $"BATA ({_kids.Count})" : $"KIDS ({_kids.Count})";
            if (_tabPillLabels[1] != null) _tabPillLabels[1].text = fil ? $"GAMIT ({_weapons.Count})" : $"WEAPONS & KIT ({_weapons.Count})";
            if (_tabPillLabels[2] != null) _tabPillLabels[2].text = $"LARONG KALYE ({_heritage.Count})";
            if (_kidBadgeText != null)
                _kidBadgeText.text = fil
                    ? "PAMPorma LANG\nPantay na HP, Bilis at Hitbox"
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
            body.offsetMax = new Vector2(0f, -78f);
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

        static Button MakeButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color bgColor,
            Action onClick,
            out Text label,
            string text,
            int fontSize,
            Color textColor)
        {
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = bgColor;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            label = MakeFittedText("Label", rt, text, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return btn;
        }

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
            t.resizeTextMaxSize = Mathf.Max(18, maxSize);
            return t;
        }

        static Text MakeWrappedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
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

        /// <summary>Keeps a fixed-row/column GridLayoutGroup fitted cleanly inside its container across 21:9..4:3 viewports.</summary>
        sealed class GridCellAutoSizer : MonoBehaviour
        {
            int _cols = 5;
            int _rows = 2;
            Vector2 _spacing = new Vector2(8f, 10f);
            float _minCell = 48f;

            public void Configure(int cols, int rows, Vector2 spacing, float minCellPx)
            {
                _cols = Mathf.Max(1, cols);
                _rows = Mathf.Max(1, rows);
                _spacing = spacing;
                _minCell = Mathf.Max(48f, minCellPx);
                Apply();
            }

            void OnEnable() => Apply();
            void OnRectTransformDimensionsChange() => Apply();

            void Apply()
            {
                var grid = GetComponent<GridLayoutGroup>();
                var rt = transform as RectTransform;
                if (grid == null || rt == null) return;
                Rect r = rt.rect;
                if (r.width <= 10f || r.height <= 10f) return;

                float cellW = (r.width - _spacing.x * (_cols - 1)) / _cols;
                float cellH = (r.height - _spacing.y * (_rows - 1)) / _rows;
                grid.spacing = _spacing;
                grid.cellSize = new Vector2(Mathf.Max(_minCell, cellW), Mathf.Max(_minCell, cellH));
            }
        }
    }
}
