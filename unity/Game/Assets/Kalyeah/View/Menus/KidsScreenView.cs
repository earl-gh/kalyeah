using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kids (docs/SPEC.md §8, read like Wild Rift and Valorant): the 12 kids as a 6 by 2 grid of rendered busts with a role
    /// filter, each card showing its role as an icon only (zero role colours per SPEC §10) and luminous gold brackets on the
    /// kid on your home screen; tapping a card opens the kid page: the kid in 3D (<see cref="HeroStage"/>), role and purok,
    /// their story, skills/skins/story/mastery tabs, and SHOW ON HOME. Lore lives here, never on home.
    /// Scales instead of wrapping: one row of six cards per row on every screen shape.
    /// </summary>
    public sealed class KidsScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color GoldLight = MenuArt.GoldLight;
        static readonly Color GoldBorder = MenuArt.GoldBorder;
        static readonly Color PanelBorder = MenuArt.PanelBorder;
        static readonly Color Muted = new Color(0.82f, 0.86f, 0.92f, 1f);
        static readonly Color MutedSoft = MenuArt.MutedText;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Shade = new Color(0.024f, 0.042f, 0.075f, 1f);
        static readonly Color CardTop = new Color(0.095f, 0.148f, 0.232f, 1f);
        static readonly Color CardBottom = new Color(0.040f, 0.068f, 0.125f, 1f);
        static readonly Color SpotNormal = new Color(0.55f, 0.84f, 0.96f, 0.24f);
        static readonly Color SpotActive = new Color(1.00f, 0.84f, 0.44f, 0.36f);
        static readonly Color NameBandBg = new Color(0.022f, 0.038f, 0.070f, 0.78f);
        static readonly Color Hairline = new Color(1f, 1f, 1f, 0.11f);
        static readonly Color SkillBadgeColor = new Color(0.16f, 0.23f, 0.35f, 1f);
        static readonly Color SkillActiveTop = new Color(0.145f, 0.205f, 0.305f, 0.96f);
        static readonly Color SkillActiveBottom = new Color(0.055f, 0.092f, 0.162f, 0.98f);
        static readonly Color HomePinnedTop = new Color(0.115f, 0.180f, 0.275f, 0.95f);
        static readonly Color HomePinnedBottom = new Color(0.052f, 0.090f, 0.158f, 0.97f);
        static readonly Color FilterActiveBottom = new Color(0.90f, 0.84f, 0.70f, 1f);
        static readonly Color RoleMedallionBg = new Color(0.035f, 0.062f, 0.112f, 0.86f);
        static readonly Color UltCardBorder = new Color(0.961f, 0.725f, 0.227f, 0.45f);
        static readonly Color PipEmpty = new Color(0.16f, 0.22f, 0.32f, 0.85f);

        const float HeaderHeight = 96f;
        const float FilterHeight = 96f; // hit-box is the full 48 dp; centered tactical pill visual is 54 px tall
        const float FilterVisualInsetY = 21f;
        const float Gutter = 20f;
        const float HitSize = 96f;
        const float CardGap = 14f;
        const int Columns = 6;
        const int Rows = 2;
        const float ChipGap = 10f;

        // proportional filter widths (total = 764 px <= 920 px safe inner width on 1024x768 tablet, never stretches into sausages)
        static readonly float[] FilterWidths = { 106f, 144f, 158f, 138f, 178f };

        /// <summary>The filter row: everyone, then the four roles in roster order.</summary>
        public static readonly string[] Filters = { "", "STRIKER", "DEFENDER", "SEEKER", "CONTROLLER" };

        sealed class KidsLifecycle : MonoBehaviour
        {
            public KidsScreenView Owner;
            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();
            void Update() => Owner?.Tick(Time.unscaledDeltaTime);
        }

        sealed class Card
        {
            public string Id;
            public string Role;
            public RectTransform Rect;
            public Image Frame;
            public Image Brackets;
            public Image Spotlight;
            public Image RoleRing;
            public Image RoleIcon;
            public Image NameRule;
            public GameObject HomeTag;
        }

        internal sealed class SkillStatChips
        {
            public RectTransform WaterChip;
            public Image WaterDot;
            public Text WaterLabel;
            public RectTransform CooldownChip;
            public Text CooldownLabel;

            public void Apply(string rawCost)
            {
                SplitSkillCost(rawCost, out string primary, out string cooldown, out bool isUltimate);
                bool hasPrimary = primary.Length > 0;
                bool hasCooldown = cooldown.Length > 0;
                WaterChip.gameObject.SetActive(hasPrimary);
                if (hasPrimary)
                {
                    WaterDot.color = isUltimate ? Gold : MenuArt.DiamondCyan;
                    WaterLabel.text = primary;
                    SetLabelColor(WaterLabel, isUltimate ? GoldLight : Cream);
                }
                CooldownChip.gameObject.SetActive(hasCooldown);
                if (hasCooldown)
                {
                    CooldownLabel.text = cooldown;
                    SetLabelColor(CooldownLabel, Cream);
                }
            }
        }

        readonly DataSource _data;
        readonly Action _onBack;
        readonly HeroStage _stage;
        readonly List<Card> _cards = new List<Card>();
        readonly List<Image> _filterPlates = new List<Image>();
        readonly List<Image> _filterAccents = new List<Image>();
        readonly List<Text> _filterLabels = new List<Text>();
        readonly List<Image> _filterIcons = new List<Image>();
        bool _disposed;
        bool _openedDirect;

        RectTransform _gridPanel;
        RectTransform _grid;
        RectTransform _pagePanel;
        RectTransform _filterRow;
        Text _countLabel;
        RawImage _pageHero;
        Text _pageRole;
        Image _pageRoleIcon;
        Text _pageName;
        Text _homeLabel;
        Button _homeButton;
        Image _homePlate;
        Image _homeIcon;
        RectTransform _homeRail;
        UiSheen _homeSheen;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public string Filter { get; private set; } = "";
        /// <summary>The kid whose page is open, or null on the grid.</summary>
        public string OpenKid { get; private set; }
        public Button HomeButton => _homeButton;

        public KidsScreenView(Transform parent, DataSource data, Action onBack = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data;
            _onBack = onBack;
            _stage = new HeroStage();

            Root = Ui.Fill("KidsScreen", parent);
            Image bg = Root.gameObject.AddComponent<Image>();
            bg.color = Shade;

            RawImage weave = MakeRaw("Weave", Root, MenuArt.Weave());
            weave.color = new Color(0.65f, 0.85f, 0.95f, 0.025f);
            weave.uvRect = new Rect(0f, 0f, 20f, 12f);

            Image warmGlow = Ui.MakeImage("Light", Root, new Color(1f, 0.82f, 0.52f, 0.11f));
            warmGlow.sprite = MenuArt.Glow();
            Place(warmGlow.rectTransform, new Vector2(0.38f, 0.82f), new Vector2(0.38f, 0.82f), Vector2.zero, new Vector2(1300f, 820f));

            Image coolGlow = Ui.MakeImage("CoolLight", Root, new Color(0.28f, 0.72f, 0.86f, 0.08f));
            coolGlow.sprite = MenuArt.Glow();
            Place(coolGlow.rectTransform, new Vector2(0.82f, 0.20f), new Vector2(0.82f, 0.20f), Vector2.zero, new Vector2(1050f, 700f));

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null) SafeRoot = Ui.Fill("KidsContent", Root);
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                SafeRoot.gameObject.AddComponent<SafeArea>().Apply();
            }

            BuildGrid();
            BuildPage();
            Root.gameObject.AddComponent<KidsLifecycle>().Owner = this;
            ShowGrid();
        }

        // ---------------------------------------------------------------- grid

        void BuildGrid()
        {
            _gridPanel = Ui.Fill("GridPanel", SafeRoot);
            RectTransform header = Header(_gridPanel, "KIDS", () => Back());

            RectTransform countChip = Ui.MakeStatChip("Count", header, AppSession.DefaultRoster.Length + " KIDS", Cream, out _countLabel, fontSize: 20, accentDot: Gold);
            Ui.ClearOutline(_countLabel);
            Place(countChip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 106f, -2f), new Vector2(136f, 36f));
            countChip.pivot = new Vector2(0f, 0.5f);

            _filterRow = Ui.Panel("Filters", _gridPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(Gutter, -HeaderHeight), new Vector2(-2f * Gutter, FilterHeight));
            Sprite filterActiveSprite = MenuArt.ChamferCard(Cream, FilterActiveBottom, GoldBorder, cut: 10, borderPx: 2, shine: 0.42f);
            float cursorX = 0f;
            for (int i = 0; i < Filters.Length; i++)
            {
                string role = Filters[i];
                float chipWidth = FilterWidths[i];
                RectTransform chip = Ui.Panel("Filter_" + (role.Length == 0 ? "ALL" : role), _filterRow, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(cursorX, 0f), new Vector2(chipWidth, 0f));
                cursorX += chipWidth + ChipGap;

                Image hit = chip.gameObject.AddComponent<Image>();
                hit.color = Clear;

                Image plate = Ui.MakeSliced("Plate", chip, i == 0 ? filterActiveSprite : MenuArt.ChamferGlass(10), Color.white);
                plate.rectTransform.offsetMin = new Vector2(0f, FilterVisualInsetY);
                plate.rectTransform.offsetMax = new Vector2(0f, -FilterVisualInsetY);

                Image accent = Ui.MakeSliced("Accent", plate.rectTransform, MenuArt.AccentRail(2), Gold);
                Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-20f, 4f));
                accent.rectTransform.pivot = new Vector2(0.5f, 0f);

                Text label = Label("Label", plate.rectTransform, role.Length == 0 ? "ALL" : role, 21, Cream, role.Length == 0 ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, display: true);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                if (role.Length > 0)
                {
                    Image icon = Ui.MakeImage("Icon", plate.rectTransform, Cream);
                    icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
                    Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(22f, 22f));
                    icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                    label.rectTransform.offsetMin = new Vector2(52f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-12f, 0f);
                    _filterIcons.Add(icon);
                }
                else _filterIcons.Add(null);

                Button b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetFilter(role));
                Ui.AddPressFeel(chip, 0.96f);

                _filterPlates.Add(plate);
                _filterAccents.Add(accent);
                _filterLabels.Add(label);
            }

            _grid = Ui.Fill("Grid", _gridPanel);
            _grid.offsetMin = new Vector2(Gutter - CardGap * 0.5f, Gutter - CardGap * 0.5f);
            _grid.offsetMax = new Vector2(-(Gutter - CardGap * 0.5f), -(HeaderHeight + FilterHeight + 4f - CardGap * 0.5f));
            foreach (string id in AppSession.DefaultRoster)
                _cards.Add(BuildCard(id));
        }

        Card BuildCard(string id)
        {
            string role = HomeScreenView.ParseRole(Field(id, "description"));
            RectTransform rect = Ui.Panel("Kid_" + id, _grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(180f, 240f));

            Image fill = Ui.MakeSliced("Fill", rect, MenuArt.ChamferCard(CardTop, CardBottom, Clear, cut: 12, borderPx: 0, shine: 0f), Color.white);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Image spotlight = Ui.MakeImage("Spotlight", fill.rectTransform, SpotNormal);
            spotlight.sprite = MenuArt.CardSpotlight();
            Stretch(spotlight.rectTransform, Vector2.zero, Vector2.one);

            RawImage bust = MakeRaw("Bust", fill.rectTransform, KalyeahResources.Texture("portraits/busts/" + id));
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f); // cropped from the bottom so the head stays framed
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 0.75f;

            Image scrim = Ui.MakeImage("Fade", fill.rectTransform, Color.white);
            scrim.sprite = MenuArt.BottomScrim();
            Stretch(scrim.rectTransform, Vector2.zero, new Vector2(1f, 0.52f));

            Image nameBand = Ui.MakeImage("NameBand", fill.rectTransform, NameBandBg);
            Place(nameBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 44f));
            nameBand.rectTransform.pivot = new Vector2(0.5f, 0f);

            Image nameRule = Ui.MakeImage("NameRule", nameBand.rectTransform, Hairline);
            Place(nameRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1.5f));
            nameRule.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image frame = Ui.MakeSliced("Frame", rect, MenuArt.ChamferOutline(Color.white, cut: 12, borderPx: 2), PanelBorder, raycast: true);
            Image brackets = Ui.MakeCornerBrackets("Brackets", rect, GoldLight, expand: 2f);

            // the role as an icon only, top left (KidsScreenTests asserts RoleChip/Icon and zero role words on cards)
            Image chip = Ui.MakeImage("RoleChip", rect, RoleMedallionBg, round: true);
            Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(36f, 36f));
            chip.rectTransform.pivot = new Vector2(0f, 1f);
            Image roleRing = Ui.MakeSliced("Ring", chip.rectTransform, MenuArt.RoundOutline(Color.white, radius: 18, borderPx: 2), PanelBorder);
            Image icon = Ui.MakeImage("Icon", chip.rectTransform, Cream);
            icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            // clean top-right mini gold badge pill (never collides with bottom nameplate)
            RectTransform homeTag = Ui.Panel("OnHome", rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -10f), new Vector2(74f, 24f));
            Ui.MakeSliced("Plate", homeTag, MenuArt.ChamferCard(GoldLight, Gold, GoldBorder, cut: 6, borderPx: 1, shine: 0.35f), Color.white);
            Text homeText = Label("Label", homeTag, "ON HOME", 15, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(homeText.rectTransform, Vector2.zero, Vector2.one);
            homeText.rectTransform.offsetMin = new Vector2(4f, 0f);
            homeText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            homeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeText.verticalOverflow = VerticalWrapMode.Truncate;
            homeText.resizeTextForBestFit = true;
            homeText.resizeTextMinSize = 8;
            homeText.resizeTextMaxSize = 15;

            Text name = Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 27, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-22f, 36f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 27;

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OpenPage(id));
            Ui.AddPressFeel(rect, 0.96f);

            return new Card
            {
                Id = id,
                Role = role,
                Rect = rect,
                Frame = frame,
                Brackets = brackets,
                Spotlight = spotlight,
                RoleRing = roleRing,
                RoleIcon = icon,
                NameRule = nameRule,
                HomeTag = homeTag.gameObject,
            };
        }

        public void SetFilter(string role)
        {
            Filter = role ?? "";
            Haptics.LightTap();
            RefreshGrid();
        }

        /// <summary>The cards the filter shows, in roster order.</summary>
        public IEnumerable<string> VisibleKids()
        {
            foreach (Card c in _cards)
                if (c.Rect.gameObject.activeSelf) yield return c.Id;
        }

        void RefreshGrid()
        {
            string home = HomeKid();
            int visible = 0;
            foreach (Card c in _cards)
            {
                bool shown = Filter.Length == 0 || c.Role == Filter;
                c.Rect.gameObject.SetActive(shown);
                if (shown) visible++;

                bool onHome = c.Id == home;
                c.Frame.color = onHome ? GoldBorder : PanelBorder;
                c.Brackets.gameObject.SetActive(onHome);
                c.Spotlight.color = onHome ? SpotActive : SpotNormal;
                c.RoleRing.color = onHome ? GoldBorder : PanelBorder;
                c.RoleIcon.color = onHome ? GoldLight : Cream;
                c.NameRule.color = onHome ? new Color(Gold.r, Gold.g, Gold.b, 0.62f) : Hairline;
                c.HomeTag.SetActive(onHome);
            }

            if (_countLabel != null)
                _countLabel.text = Filter.Length == 0 ? AppSession.DefaultRoster.Length + " KIDS" : visible + " / " + AppSession.DefaultRoster.Length + " KIDS";

            Sprite activePlate = MenuArt.ChamferCard(Cream, FilterActiveBottom, GoldBorder, cut: 10, borderPx: 2, shine: 0.42f);
            Sprite idlePlate = MenuArt.ChamferGlass(10);
            for (int i = 0; i < Filters.Length; i++)
            {
                bool on = Filters[i] == Filter;
                _filterPlates[i].sprite = on ? activePlate : idlePlate;
                _filterAccents[i].gameObject.SetActive(on);
                SetLabelColor(_filterLabels[i], on ? Ink : Muted);
                if (_filterIcons[i] != null) _filterIcons[i].color = on ? Ink : Muted;
            }
            RefreshLayout();
        }

        // ---------------------------------------------------------------- kid page

        public const int TabSkills = 0;
        public const int TabSkins = 1;
        public const int TabStory = 2;
        public const int TabMastery = 3;
        static readonly string[] TabNames = { "SKILLS", "SKINS", "STORY", "MASTERY" };

        [Serializable] public sealed class SkillData { public string slot, name, what, cost; }
        [Serializable] public sealed class KidPageData { public string id, home, job, rival, idle, victory; public string[] friends; public SkillData[] skills; }
        [Serializable] sealed class KidPagesRoot { public KidPagesBody data; }
        [Serializable] sealed class KidPagesBody { public KidPageData[] kids; }

        readonly List<Text> _tabLabels = new List<Text>();
        readonly List<Image> _tabBars = new List<Image>();
        readonly List<Image> _tabGlows = new List<Image>();
        RectTransform _content;
        Text _pagePersonality;
        static KidPageData[] s_pages;

        /// <summary>The tab showing on the kid page (Skills, Skins, Story or Mastery).</summary>
        public int ActiveTab { get; private set; }

        /// <summary>A kid's skills and story text (display text only, not sim data: assets/ui/kid_pages.json, from docs/KITS.md and docs/LORE.md).</summary>
        internal static KidPageData PageFor(string id)
        {
            if (s_pages == null)
            {
                try { s_pages = JsonUtility.FromJson<KidPagesRoot>(Resources.Load<TextAsset>("ui/kid_pages").text).data.kids; }
                catch { s_pages = new KidPageData[0]; }
            }
            foreach (KidPageData p in s_pages) if (p.id == id) return p;
            return null;
        }

        void BuildPage()
        {
            _pagePanel = Ui.Fill("PagePanel", SafeRoot);

            // built first so its drag-to-orbit surface never intercepts Header, Info or Tab clicks
            RectTransform stageArea = Ui.Fill("StageArea", _pagePanel);
            stageArea.anchorMin = new Vector2(0.24f, 0.02f);
            stageArea.anchorMax = new Vector2(0.59f, 0.96f);
            stageArea.offsetMin = Vector2.zero;
            stageArea.offsetMax = Vector2.zero;

            Image stageSpot = Ui.MakeImage("StageSpot", stageArea, new Color(1.00f, 0.86f, 0.56f, 0.17f));
            stageSpot.sprite = MenuArt.CardSpotlight();
            Stretch(stageSpot.rectTransform, Vector2.zero, Vector2.one);

            Image stageRim = Ui.MakeImage("StageRim", stageArea, new Color(0.32f, 0.82f, 0.94f, 0.12f));
            stageRim.sprite = MenuArt.Glow();
            Place(stageRim.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(440f, 440f));

            _pageHero = MakeRaw("Hero", stageArea, _stage.Texture);
            _pageHero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = _pageHero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;
            _stage.AttachOrbitDrag(_pageHero.rectTransform);

            Header(_pagePanel, "", () => Back());

            // left column: role-purok pill, giant name, accent bar, story, and tactile SHOW ON HOME CTA
            RectTransform info = Ui.Panel("Info", _pagePanel, new Vector2(0f, 0f), new Vector2(0.295f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            info.offsetMin = new Vector2(Gutter, Gutter);
            info.offsetMax = new Vector2(0f, -HeaderHeight);

            Image rolePill = Ui.MakeSliced("RolePill", info, MenuArt.StatChip(10), Color.white);
            Place(rolePill.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 44f));
            rolePill.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image disc = Ui.MakeImage("RoleDisc", info, Gold, round: true);
            Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -7f), new Vector2(34f, 34f));
            disc.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRoleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            Place(_pageRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            // Role stays a direct child of Info for KidsScreenTests
            _pageRole = Label("Role", info, "", 19, GoldLight, TextAnchor.MiddleLeft, display: true);
            Place(_pageRole.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -2f), new Vector2(-56f, 44f));
            _pageRole.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRole.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageRole.verticalOverflow = VerticalWrapMode.Truncate;
            _pageRole.resizeTextForBestFit = true;
            _pageRole.resizeTextMinSize = 11;
            _pageRole.resizeTextMaxSize = 19;

            _pageName = Label("Name", info, "", 92, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(_pageName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(0f, 102f));
            _pageName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageName.verticalOverflow = VerticalWrapMode.Truncate;
            _pageName.resizeTextForBestFit = true;
            _pageName.resizeTextMinSize = 28;
            _pageName.resizeTextMaxSize = 92;

            Image nameBar = Ui.MakeSliced("NameBar", info, MenuArt.AccentRail(2), Gold);
            Place(nameBar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -162f), new Vector2(56f, 4f));
            nameBar.rectTransform.pivot = new Vector2(0f, 1f);

            _pagePersonality = Label("Personality", info, "", 22, Muted, TextAnchor.UpperLeft);
            Place(_pagePersonality.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -180f), new Vector2(0f, 160f));
            _pagePersonality.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pagePersonality.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pagePersonality.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform home = Ui.Panel("ShowOnHome", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, HitSize));
            Image homeHit = home.gameObject.AddComponent<Image>();
            homeHit.color = Clear;

            _homePlate = Ui.MakeSliced("Plate", home, MenuArt.CtaGold(14), Color.white);
            _homePlate.rectTransform.offsetMin = new Vector2(0f, 10f);
            _homePlate.rectTransform.offsetMax = new Vector2(0f, -10f);
            _homeSheen = Ui.AddSheen(_homePlate.rectTransform);
            _homeRail = Ui.MakeAccentRail("Rail", _homePlate.rectTransform, Gold, width: 4f, verticalInset: 14f);

            _homeIcon = Ui.MakeImage("CueIcon", _homePlate.rectTransform, Ink);
            Place(_homeIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 2f), new Vector2(26f, 26f));
            _homeIcon.rectTransform.pivot = new Vector2(0f, 0.5f);

            _homeLabel = Label("Label", _homePlate.rectTransform, "SHOW ON HOME", 26, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(_homeLabel.rectTransform, Vector2.zero, Vector2.one);
            _homeLabel.rectTransform.offsetMin = new Vector2(46f, 4f);
            _homeLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            _homeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _homeLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _homeLabel.resizeTextForBestFit = true;
            _homeLabel.resizeTextMinSize = 12;
            _homeLabel.resizeTextMaxSize = 26;

            _homeButton = home.gameObject.AddComponent<Button>();
            _homeButton.targetGraphic = homeHit;
            _homeButton.transition = Selectable.Transition.None;
            _homeButton.onClick.AddListener(ShowOnHome);
            Ui.AddPressFeel(home, 0.95f);

            // right: four razor-crisp underlined tabs, then chamfered tactical glass content
            RectTransform tabs = Ui.Panel("Tabs", _pagePanel, new Vector2(0.52f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HitSize));
            tabs.offsetMin = new Vector2(Gutter, -HeaderHeight);
            tabs.offsetMax = new Vector2(-Gutter, 0f);

            Image tabTrack = Ui.MakeImage("Track", tabs, Hairline);
            Place(tabTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 1.5f));
            tabTrack.rectTransform.pivot = new Vector2(0.5f, 0f);

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                RectTransform tab = Ui.Panel("Tab_" + TabNames[i], tabs, new Vector2(i / (float)TabNames.Length, 0f), new Vector2((i + 1) / (float)TabNames.Length, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                tab.offsetMin = new Vector2(2f, 0f);
                tab.offsetMax = new Vector2(-2f, 0f);

                Image hit = tab.gameObject.AddComponent<Image>();
                hit.color = Clear;

                Image glow = Ui.MakeImage("Glow", tab, new Color(Gold.r, Gold.g, Gold.b, 0.14f));
                glow.sprite = MenuArt.CardSpotlight();
                Place(glow.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 52f));
                glow.rectTransform.pivot = new Vector2(0.5f, 0f);

                Text label = Label("Label", tab, TabNames[i], 26, MutedSoft, TextAnchor.MiddleCenter, display: true);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                label.rectTransform.offsetMin = new Vector2(0f, 10f);

                Image bar = Ui.MakeSliced("Bar", tab, MenuArt.AccentRail(2), Gold);
                Place(bar.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 9f), new Vector2(0f, 4f));
                bar.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectTab(index));
                Ui.AddPressFeel(tab, 0.96f);

                _tabLabels.Add(label);
                _tabBars.Add(bar);
                _tabGlows.Add(glow);
            }

            RectTransform panel = Ui.Panel("ContentPanel", _pagePanel, new Vector2(0.52f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(Gutter, Gutter);
            panel.offsetMax = new Vector2(-Gutter, -(HeaderHeight + 4f));
            Ui.MakeSliced("Glass", panel, MenuArt.ChamferGlass(16), Color.white);
            _content = Ui.Fill("Content", panel);
            _content.offsetMin = new Vector2(14f, 14f);
            _content.offsetMax = new Vector2(-14f, -14f);
            _content.gameObject.AddComponent<RectMask2D>();
        }

        public void OpenPage(string id)
        {
            OpenKid = id;
            _skillIndex = 0;
            _openedDirect = false;
            Haptics.LightTap();
            _gridPanel.gameObject.SetActive(false);
            _pagePanel.gameObject.SetActive(true);
            string description = Field(id, "description") ?? "";
            string role = HomeScreenView.ParseRole(description);
            _pageRoleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _pageRole.text = role + " \u00B7 " + Purok(description).ToUpperInvariant();
            _pageName.text = (Field(id, "display_name") ?? id).ToUpperInvariant();
            _pagePersonality.text = Story(description);
            _stage.SetKid(id);
            _stage.ResetOrbit();
            _stage.RenderNow();
            SelectTab(TabSkills);
            RefreshPage();
            RefreshLayout();
        }

        /// <summary>Shows one tab's content for the open kid: skills from docs/KITS.md, skins, story from docs/LORE.md, or mastery.</summary>
        public void SelectTab(int tab)
        {
            ActiveTab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            for (int i = 0; i < _tabLabels.Count; i++)
            {
                bool on = i == ActiveTab;
                SetLabelColor(_tabLabels[i], on ? Cream : MutedSoft);
                _tabBars[i].gameObject.SetActive(on);
                _tabGlows[i].gameObject.SetActive(on);
            }
            for (int i = _content.childCount - 1; i >= 0; i--) KalyeahResources.Kill(_content.GetChild(i).gameObject);
            _rowTop = 0f;
            KidPageData page = OpenKid != null ? PageFor(OpenKid) : null;
            if (page == null) return;
            switch (ActiveTab)
            {
                case TabSkills:
                    AddSkills(page);
                    break;
                case TabSkins:
                    AddRow("DEFAULT", "Equipped. New looks come from the pass and events, once they exist.", "SKIN", 126f, accentGold: true);
                    AddRow("VICTORY POSE", page.victory, "WIN", 126f, accentGold: false);
                    AddRow("IDLE", page.idle, "IDLE", 126f, accentGold: false);
                    break;
                case TabStory:
                    AddFact("HOME", page.home);
                    AddFact("DREAM JOB", page.job);
                    AddFact("PUROK", Purok(Field(OpenKid, "description") ?? "") + ", plays at home on the " + HomeMapName(OpenKid));
                    AddFact("FRIENDS", string.Join(", ", page.friends));
                    AddFact("RIVAL", page.rival);
                    break;
                default:
                    AddMasteryCard(Field(OpenKid, "display_name") ?? OpenKid);
                    break;
            }
        }

        static string HomeMapName(string id)
        {
            switch (id)
            {
                case "narda": case "jopay": case "kikay": return "Barangay";
                case "inggo": case "karding": case "pamela": return "Palengke";
                case "jumong": case "oriang": case "domeng": return "Pier";
                default: return "Hacienda";
            }
        }

        // rows are placed by hand (top to bottom) so they are right in a single frame, with no layout pass to wait for
        float _rowTop;
        const float RowGap = 10f;
        const float BadgeWidth = 76f;

        RectTransform NewRow(string name, float height, bool accentGold)
        {
            RectTransform row = Ui.Panel(name, _content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_rowTop), new Vector2(0f, height));
            Ui.MakeSliced("Bg", row, MenuArt.ChamferCard(CardTop, CardBottom, PanelBorder, cut: 12, borderPx: 1, shine: 0.22f), Color.white);
            Ui.MakeAccentRail("Accent", row, accentGold ? Gold : PanelBorder, width: 4f, verticalInset: 12f);
            _rowTop += height + RowGap;
            return row;
        }

        void AddRow(string title, string body, string badgeText, float height, bool accentGold)
        {
            RectTransform row = NewRow("Row_" + title, height, accentGold);
            RectTransform badge = Ui.Panel("Badge", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(BadgeWidth, 34f));
            Ui.MakeSliced("Plate", badge, accentGold ? MenuArt.ChamferCard(GoldLight, Gold, GoldBorder, cut: 6, borderPx: 1, shine: 0.35f) : MenuArt.StatChip(6), Color.white);
            Text bt = Label("Text", badge, badgeText, 18, accentGold ? Ink : GoldLight, TextAnchor.MiddleCenter, display: true);
            Stretch(bt.rectTransform, Vector2.zero, Vector2.one);

            const float left = 18f + BadgeWidth + 14f;
            Text head = Label("Title", row, title, 26, Cream, TextAnchor.MiddleLeft, display: true);
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -15f), new Vector2(-(left + 16f), 36f));
            head.rectTransform.pivot = new Vector2(0f, 1f);

            Text text = Label("Body", row, body, 21, Muted, TextAnchor.UpperLeft);
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -58f), new Vector2(-36f, height - 66f));
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void AddFact(string head, string body)
        {
            RectTransform row = NewRow("Fact_" + head, 84f, accentGold: false);
            Text label = Label("Head", row, head, 19, GoldLight, TextAnchor.MiddleLeft, display: true);
            Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 0f), new Vector2(126f, 0f));
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text value = Label("Value", row, body, 21, Cream, TextAnchor.MiddleLeft);
            Stretch(value.rectTransform, Vector2.zero, Vector2.one);
            value.rectTransform.offsetMin = new Vector2(148f, 6f);
            value.rectTransform.offsetMax = new Vector2(-14f, -6f);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void AddMasteryCard(string kidName)
        {
            RectTransform row = NewRow("Row_Mastery", 232f, accentGold: true);
            RectTransform badge = Ui.Panel("Badge", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(68f, 34f));
            Ui.MakeSliced("Plate", badge, MenuArt.StatChip(6), Color.white);
            Text bt = Label("Text", badge, "LVL 0", 18, GoldLight, TextAnchor.MiddleCenter, display: true);
            Stretch(bt.rectTransform, Vector2.zero, Vector2.one);

            Text head = Label("Title", row, "Mastery 0 of 7", 28, Cream, TextAnchor.MiddleLeft, display: true);
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(98f, -16f), new Vector2(-114f, 38f));
            head.rectTransform.pivot = new Vector2(0f, 1f);

            // 7-segment mastery progress track (0 of 7 lit until accounts exist)
            RectTransform track = Ui.Panel("Track", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-36f, 12f));
            for (int i = 0; i < 7; i++)
            {
                Image pip = Ui.MakeSliced("Pip_" + i, track, MenuArt.AccentRail(2), PipEmpty);
                pip.rectTransform.anchorMin = new Vector2(i / 7f, 0f);
                pip.rectTransform.anchorMax = new Vector2((i + 1f) / 7f, 1f);
                pip.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 3f, 0f);
                pip.rectTransform.offsetMax = new Vector2(i == 6 ? 0f : -3f, 0f);
            }

            Text body = Label("Body", row, "Play matches with " + kidName + " to earn mastery. Level 3 gives a badge, 5 an emote and 7 a title: never power. Mastery comes with accounts.", 21, Muted, TextAnchor.UpperLeft);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -92f), new Vector2(-36f, 126f));
            body.rectTransform.pivot = new Vector2(0f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
        }

        int _skillIndex;
        const float SkillTileHeight = 104f;

        /// <summary>The skill whose description shows on the Skills tab: 0 and 1 are the two skills, 2 the ultimate.</summary>
        public int SelectedSkill => _skillIndex;

        /// <summary>Shows one of the open kid's three skills on the Skills tab.</summary>
        public void SelectSkill(int index)
        {
            _skillIndex = Mathf.Clamp(index, 0, 2);
            if (OpenKid != null && ActiveTab == TabSkills) SelectTab(TabSkills);
        }

        /// <summary>
        /// The Skills tab: three tactical S1/S2/ULT selector cards and one detailed card with separate WATER and COOLDOWN stat
        /// chips so the longest description fits cleanly on every screen shape including 4:3 tablets.
        /// </summary>
        void AddSkills(KidPageData page)
        {
            int count = Mathf.Min(3, page.skills.Length);
            _skillIndex = Mathf.Clamp(_skillIndex, 0, Mathf.Max(0, count - 1));
            Sprite activeTileSprite = MenuArt.ChamferCard(SkillActiveTop, SkillActiveBottom, GoldBorder, cut: 12, borderPx: 2, shine: 0.34f);
            Sprite ultIdleSprite = MenuArt.ChamferCard(CardTop, CardBottom, UltCardBorder, cut: 12, borderPx: 2, shine: 0.24f);
            Sprite idleTileSprite = MenuArt.ChamferGlass(12);

            for (int i = 0; i < count; i++)
            {
                int index = i;
                SkillData skill = page.skills[i];
                bool on = i == _skillIndex, ultimate = skill.slot == "ULT";
                RectTransform tile = Ui.Panel("Skill_" + skill.slot, _content, new Vector2(i / (float)count, 1f), new Vector2((i + 1f) / count, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, SkillTileHeight));
                tile.offsetMin = new Vector2(i == 0 ? 0f : RowGap * 0.5f, tile.offsetMin.y);
                tile.offsetMax = new Vector2(i == count - 1 ? 0f : -RowGap * 0.5f, tile.offsetMax.y);

                Image plate = Ui.MakeSliced("Plate", tile, on ? activeTileSprite : ultimate ? ultIdleSprite : idleTileSprite, Color.white, raycast: true);
                if (on) Ui.MakeCornerBrackets("Brackets", tile, GoldLight);

                Image badge = Ui.MakeImage("Badge", tile, ultimate ? Gold : SkillBadgeColor, round: true);
                Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(52f, 52f));
                if (!ultimate) Ui.MakeSliced("Ring", badge.rectTransform, MenuArt.RoundOutline(Color.white, radius: 26, borderPx: 2), on ? GoldBorder : PanelBorder);
                Text badgeText = Label("Text", badge.rectTransform, skill.slot, 24, ultimate ? Ink : (on ? GoldLight : Gold), TextAnchor.MiddleCenter, display: true);
                Stretch(badgeText.rectTransform, Vector2.zero, Vector2.one);

                Text caption = Label("Caption", tile, SlotCaption(skill.slot), 15, on ? Cream : (ultimate ? GoldLight : MutedSoft), TextAnchor.LowerCenter, display: true);
                Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-8f, 22f));
                caption.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tile.gameObject.AddComponent<Button>();
                b.targetGraphic = plate;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectSkill(index));
                Ui.AddPressFeel(tile, 0.95f);
            }

            SkillData shown = page.skills[_skillIndex];
            bool shownUlt = shown.slot == "ULT";
            RectTransform card = Ui.Panel("SkillCard", _content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMax = new Vector2(0f, -(SkillTileHeight + RowGap + 2f));
            Ui.MakeSliced("Bg", card, MenuArt.ChamferCard(CardTop, CardBottom, shownUlt ? UltCardBorder : PanelBorder, cut: 14, borderPx: 2, shine: 0.25f), Color.white);
            Ui.MakeAccentRail("Accent", card, shownUlt ? Gold : GoldBorder, width: 4f, verticalInset: 16f);

            Text kicker = Label("Kicker", card, shownUlt ? "ULTIMATE ABILITY" : SlotCaption(shown.slot), 16, Gold, TextAnchor.UpperLeft, display: true);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -14f), new Vector2(-44f, 22f));
            kicker.rectTransform.pivot = new Vector2(0f, 1f);

            Text title = Label("Title", card, shown.name, 38, Cream, TextAnchor.UpperLeft, display: true);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -36f), new Vector2(-44f, 44f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 38;

            SkillStatChips chips = BuildSkillStatChips(card, topOffsetY: -88f, leftPad: 24f, rightPad: 20f, height: 34f, fontSize: 18);
            chips.Apply(shown.cost);

            // keeps canonical raw cost string for KidsScreenTests while UI renders separate WATER + COOLDOWN stat chips
            Text rawCost = Label("Cost", card, shown.cost, 16, MutedSoft, TextAnchor.UpperLeft);
            rawCost.gameObject.SetActive(false);

            Image divider = Ui.MakeImage("Divider", card, Hairline);
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -134f), new Vector2(-44f, 1.5f));
            divider.rectTransform.pivot = new Vector2(0f, 1f);

            Text body = Label("Body", card, shown.what, 23, Cream, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, Vector2.zero, Vector2.one);
            body.rectTransform.offsetMin = new Vector2(24f, 16f);
            body.rectTransform.offsetMax = new Vector2(-22f, -146f);
            body.lineSpacing = 1.18f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 14;
            body.resizeTextMaxSize = 23;
        }

        void ShowOnHome()
        {
            if (OpenKid == null || OpenKid == HomeKid()) return;
            AppSession.Current.SelectedCharacter = OpenKid;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Haptics.LightTap();
            RefreshPage();
        }

        void RefreshPage()
        {
            bool onHome = OpenKid == HomeKid();
            _homeLabel.text = onHome ? "ON YOUR HOME SCREEN" : "SHOW ON HOME";
            _homeButton.interactable = !onHome;
            _homePlate.sprite = onHome
                ? MenuArt.ChamferCard(HomePinnedTop, HomePinnedBottom, GoldBorder, cut: 14, borderPx: 2, shine: 0.30f)
                : MenuArt.CtaGold(14);
            _homeRail.gameObject.SetActive(onHome);
            _homeIcon.sprite = onHome ? MenuArt.CheckBadge() : MenuArt.Glyph(MenuArt.GlyphKind.Star);
            _homeIcon.color = onHome ? Color.white : Ink;
            SetLabelColor(_homeLabel, onHome ? Cream : Ink);
            if (_homeSheen != null)
            {
                _homeSheen.enabled = !onHome;
                if (onHome) _homeSheen.ResetSheen();
            }
        }

        void ShowGrid()
        {
            OpenKid = null;
            _pagePanel.gameObject.SetActive(false);
            _gridPanel.gameObject.SetActive(true);
            RefreshGrid();
        }

        /// <summary>Android Back: the kid page returns to the grid; on the grid, or on a page opened from home, Back leaves the screen (returns false).</summary>
        public bool HandleBack()
        {
            if (OpenKid == null || _openedDirect) return false;
            ShowGrid();
            return true;
        }

        void Back()
        {
            if (!HandleBack()) _onBack?.Invoke();
        }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            _openedDirect = false;
            ShowGrid();
        }

        /// <summary>Opens straight on a kid's page (the home name plate).</summary>
        public void ShowKid(string id)
        {
            Root.gameObject.SetActive(true);
            OpenPage(id);
            _openedDirect = true; // Back from a page opened from home returns home
        }

        public void Hide() => Root.gameObject.SetActive(false);

        void Tick(float dt)
        {
            if (_disposed || OpenKid == null || !Application.isPlaying) return;
            _stage.Tick(dt);
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Six cards across and two rows on every shape, never wrapped: each visible card is anchored to its sixth and half of the
        /// grid area, so the canvas sizes them on every screen with no pixel maths (and no stale rect at build time).
        /// </summary>
        public void RefreshLayout()
        {
            if (_disposed) return;
            int i = 0;
            foreach (Card c in _cards)
            {
                if (!c.Rect.gameObject.activeSelf) continue;
                int col = i % Columns, row = i / Columns;
                c.Rect.anchorMin = new Vector2((float)col / Columns, 1f - (row + 1f) / Rows);
                c.Rect.anchorMax = new Vector2((col + 1f) / Columns, 1f - (float)row / Rows);
                c.Rect.offsetMin = new Vector2(CardGap * 0.5f, CardGap * 0.5f);
                c.Rect.offsetMax = new Vector2(-CardGap * 0.5f, -CardGap * 0.5f);
                i++;
            }
        }

        // ---------------------------------------------------------------- data

        static string HomeKid()
        {
            string id = AppSession.Current.SelectedCharacter;
            return string.IsNullOrWhiteSpace(id) ? AppSession.DefaultCharacter : id;
        }

        /// <summary>"Seeker from Palengke. A tinkerer ..." gives "Palengke".</summary>
        public static string Purok(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";
            int dot = description.IndexOf('.');
            string first = dot > 0 ? description.Substring(0, dot) : description;
            int from = first.IndexOf(" from ", StringComparison.Ordinal);
            return from > 0 ? first.Substring(from + 6).Trim() : "";
        }

        /// <summary>The description after its "Role from Purok." opener.</summary>
        public static string Story(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";
            int dot = description.IndexOf('.');
            return dot > 0 && dot + 1 < description.Length ? description.Substring(dot + 1).Trim() : description.Trim();
        }

        string Field(string id, string field)
        {
            if (_data == null) return null;
            try
            {
                Dictionary<string, object> p = _data.Props("characters__" + id);
                if (p != null && p.TryGetValue(field, out object v) && v is string s && !string.IsNullOrWhiteSpace(s)) return s;
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        // ---------------------------------------------------------------- shared tactical builders

        RectTransform Header(RectTransform parent, string title, Action onBack)
        {
            RectTransform header = Ui.Panel("Header", parent, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            MakeBackButton(header, Gutter, HitSize, onBack);
            if (title.Length > 0)
            {
                Text t = Label("Title", header, title, 48, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
                Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 4f, 0f), new Vector2(140f, 60f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            return header;
        }

        internal static void MakeBackButton(RectTransform header, float gutter, float hitSize, Action onBack)
        {
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(gutter, 0f), new Vector2(hitSize, hitSize));
            Image hit = back.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Image plate = Ui.MakeSliced("Plate", back, MenuArt.CtaGhost(10), Color.white);
            Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            Image arrow = Ui.MakeImage("Arrow", plate.rectTransform, Cream);
            arrow.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Button b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onBack());
            Ui.AddPressFeel(back, 0.94f);
        }

        internal static SkillStatChips BuildSkillStatChips(Transform parent, float topOffsetY, float leftPad, float rightPad, float height, int fontSize)
        {
            RectTransform row = Ui.Panel("StatChips", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height));
            row.offsetMin = new Vector2(leftPad, topOffsetY - height);
            row.offsetMax = new Vector2(-rightPad, topOffsetY);

            RectTransform waterChip = MakeStatChipBox("WaterChip", row, 0f, 0.43f, 0f, -4f, fontSize, MenuArt.DiamondCyan, out Image waterDot, out Text waterLabel);
            RectTransform cdChip = MakeStatChipBox("CooldownChip", row, 0.43f, 1f, 4f, 0f, fontSize, Gold, out _, out Text cdLabel);
            return new SkillStatChips
            {
                WaterChip = waterChip,
                WaterDot = waterDot,
                WaterLabel = waterLabel,
                CooldownChip = cdChip,
                CooldownLabel = cdLabel,
            };
        }

        static RectTransform MakeStatChipBox(string name, Transform parent, float xMin, float xMax, float leftGap, float rightGap, int fontSize, Color dotColor, out Image dot, out Text label)
        {
            RectTransform chip = Ui.Panel(name, parent, new Vector2(xMin, 0f), new Vector2(xMax, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            chip.offsetMin = new Vector2(leftGap, 0f);
            chip.offsetMax = new Vector2(rightGap, 0f);
            Ui.MakeSliced("Bg", chip, MenuArt.StatChip(8), Color.white);

            dot = Ui.MakeImage("Dot", chip, dotColor, round: true);
            Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(7f, 7f));
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);

            label = Label("Label", chip, "", fontSize, Cream, TextAnchor.MiddleLeft, display: true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
            return chip;
        }

        internal static void SplitSkillCost(string rawCost, out string primary, out string cooldown, out bool isUltimate)
        {
            primary = "";
            cooldown = "";
            isUltimate = false;
            if (string.IsNullOrWhiteSpace(rawCost)) return;

            int sep = rawCost.IndexOf(',');
            if (sep < 0) sep = rawCost.IndexOf('\u00B7');
            if (sep > 0)
            {
                primary = rawCost.Substring(0, sep).Trim().ToUpperInvariant();
                cooldown = rawCost.Substring(sep + 1).Trim().ToUpperInvariant();
            }
            else
            {
                primary = rawCost.Trim().ToUpperInvariant();
            }
            isUltimate = primary.StartsWith("ULT", StringComparison.Ordinal);
        }

        internal static string SlotCaption(string slot)
        {
            if (slot == "S1") return "SKILL 1";
            if (slot == "S2") return "SKILL 2";
            if (slot == "ULT") return "ULTIMATE";
            return slot ?? "";
        }

        internal static void SetLabelColor(Text label, Color color)
        {
            if (label == null) return;
            label.color = color;
            if (Ui.Luminance(color) < 0.25f) Ui.ClearOutline(label);
        }

        internal static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, bool display = false, bool ink = false)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, display ? KalyeahResources.DisplayFont() : null);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null)
            {
                bool allowOutline = ink && Ui.Luminance(color) >= 0.25f;
                outline.enabled = allowOutline;
                if (allowOutline)
                {
                    outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.9f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }
            return label;
        }

        internal static RawImage MakeRaw(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, Vector2.zero, Vector2.one);
            return raw;
        }

        internal static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Texture2D s_ramp;

        /// <summary>A vertical alpha ramp: 1 at the bottom row to 0 at the top.</summary>
        internal static Texture2D Ramp()
        {
            if (s_ramp != null) return s_ramp;
            const int n = 64;
            var tex = new Texture2D(1, n, TextureFormat.RGBA32, false) { name = "KidsRamp", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < n; i++)
                tex.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, 1f - i / (n - 1f))));
            tex.Apply(false, true);
            s_ramp = tex;
            return tex;
        }
    }
}
