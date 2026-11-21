using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kids (docs/SPEC.md §8, read like Wild Rift and Mobile Legends): the 12 kids as a 6 by 2 grid of rendered busts with a role
    /// filter, each card showing its role as an icon only and a gold frame on the kid on your home screen; tapping a card opens
    /// the kid page: the kid in 3D (<see cref="HeroStage"/>), role and purok, their story, and SHOW ON HOME. Lore lives here, never
    /// on home. Only real content: skills, skins and mastery join the kid page when the 12 kits and mastery are built (§12).
    /// Scales instead of wrapping: one row of six cards per row on every screen shape.
    /// </summary>
    public sealed class KidsScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Muted = new Color(0.80f, 0.84f, 0.90f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Shade = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color CardBg = new Color(0.10f, 0.14f, 0.22f, 1f);

        const float HeaderHeight = 112f;
        const float FilterHeight = 96f; // the chips look 68 tall, but each one's hit area is the full 48 dp
        const float Gutter = 20f;
        const float HitSize = 96f;
        const float CardGap = 14f;
        const int Columns = 6;
        const int Rows = 2;
        const float AllShare = 0.12f;
        const float RoleShare = 0.22f; // 0.12 + 4 x 0.22 fills the row
        const float ChipGap = 10f;

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
            public GameObject HomeTag;
        }

        readonly DataSource _data;
        readonly Action _onBack;
        readonly HeroStage _stage;
        readonly List<Card> _cards = new List<Card>();
        readonly List<Image> _filterPlates = new List<Image>();
        readonly List<Text> _filterLabels = new List<Text>();
        readonly List<Image> _filterIcons = new List<Image>();
        bool _disposed;
        bool _openedDirect;

        RectTransform _gridPanel;
        RectTransform _grid;
        RectTransform _pagePanel;
        RectTransform _filterRow;
        RawImage _pageHero;
        Text _pageRole;
        Image _pageRoleIcon;
        Text _pageName;
        Text _homeLabel;
        Button _homeButton;
        Image _homePlate;

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
            Image light = Ui.MakeImage("Light", Root, new Color(1f, 0.84f, 0.6f, 0.10f));
            light.sprite = MenuArt.Glow();
            Place(light.rectTransform, new Vector2(0.4f, 0.8f), new Vector2(0.4f, 0.8f), Vector2.zero, new Vector2(1400f, 900f));

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
            Text count = Label("Count", header, AppSession.DefaultRoster.Length + " KIDS", 22, Muted, TextAnchor.MiddleLeft, display: true);
            Place(count.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 150f, -2f), new Vector2(240f, 40f));
            count.rectTransform.pivot = new Vector2(0f, 0.5f);

            _filterRow = Ui.Panel("Filters", _gridPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(Gutter, -HeaderHeight), new Vector2(-2f * Gutter, FilterHeight));
            for (int i = 0; i < Filters.Length; i++)
            {
                string role = Filters[i];
                float from = i == 0 ? 0f : AllShare + (i - 1) * RoleShare, to = i == 0 ? AllShare : from + RoleShare;
                RectTransform chip = Ui.Panel("Filter_" + (role.Length == 0 ? "ALL" : role), _filterRow, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                chip.offsetMin = new Vector2(i == 0 ? 0f : ChipGap * 0.5f, 0f);
                chip.offsetMax = new Vector2(i == Filters.Length - 1 ? 0f : -ChipGap * 0.5f, 0f);
                Image hit = chip.gameObject.AddComponent<Image>();
                hit.color = Clear;
                Image plate = Ui.MakeImage("Plate", chip, Color.white);
                plate.sprite = MenuArt.RoundSolid(30);
                plate.type = Image.Type.Sliced;
                Stretch(plate.rectTransform, Vector2.zero, Vector2.one);
                plate.rectTransform.offsetMin = new Vector2(0f, 14f);
                plate.rectTransform.offsetMax = new Vector2(0f, -14f);
                Text label = Label("Label", chip, role.Length == 0 ? "ALL" : role, 22, Cream, TextAnchor.MiddleCenter, display: true);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                if (role.Length > 0)
                {
                    Image icon = Ui.MakeImage("Icon", chip, Cream);
                    icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
                    Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
                    icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                    label.rectTransform.offsetMin = new Vector2(52f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-8f, 0f);
                    _filterIcons.Add(icon);
                }
                else _filterIcons.Add(null);
                Button b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.onClick.AddListener(() => SetFilter(role));
                _filterPlates.Add(plate);
                _filterLabels.Add(label);
            }

            _grid = Ui.Fill("Grid", _gridPanel);
            _grid.offsetMin = new Vector2(Gutter - CardGap * 0.5f, Gutter - CardGap * 0.5f);
            _grid.offsetMax = new Vector2(-(Gutter - CardGap * 0.5f), -(HeaderHeight + FilterHeight + 16f - CardGap * 0.5f));
            foreach (string id in AppSession.DefaultRoster)
                _cards.Add(BuildCard(id));
        }

        Card BuildCard(string id)
        {
            string role = HomeScreenView.ParseRole(Field(id, "description"));
            RectTransform rect = Ui.Panel("Kid_" + id, _grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(180f, 240f));
            Image frame = Ui.MakeImage("Frame", rect, Gold, false, true);
            frame.sprite = MenuArt.RoundSolid(18);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
            Image fill = Ui.MakeImage("Fill", rect, CardBg);
            fill.sprite = MenuArt.RoundSolid(16);
            fill.type = Image.Type.Sliced;
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            RawImage bust = MakeRaw("Bust", fill.rectTransform, KalyeahResources.Texture("portraits/busts/" + id));
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f); // the bust covers the card and is cropped from the bottom, so the head stays
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 0.75f;
            RawImage fade = MakeRaw("Fade", fill.rectTransform, Ramp());
            fade.color = new Color(Shade.r, Shade.g, Shade.b, 0.95f); // Ramp's first row is its bottom: dark at the card's foot
            Stretch(fade.rectTransform, Vector2.zero, new Vector2(1f, 0.45f));

            // the role as an icon only, top left
            Image chip = Ui.MakeImage("RoleChip", rect, new Color(Shade.r, Shade.g, Shade.b, 0.7f), round: true);
            Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(44f, 44f));
            chip.rectTransform.pivot = new Vector2(0f, 1f);
            Image icon = Ui.MakeImage("Icon", chip.rectTransform, Cream);
            icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            Text name = Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 30, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            Stretch(name.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.sizeDelta = new Vector2(-28f, 40f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 34f);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 22;
            name.resizeTextMaxSize = 30;

            Text home = Label("OnHome", rect, "On home", 22, Gold, TextAnchor.LowerLeft);
            Stretch(home.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            home.rectTransform.pivot = new Vector2(0.5f, 0f);
            home.rectTransform.sizeDelta = new Vector2(-28f, 28f);
            home.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.onClick.AddListener(() => OpenPage(id));
            return new Card { Id = id, Role = role, Rect = rect, Frame = frame, HomeTag = home.gameObject };
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
            foreach (Card c in _cards)
            {
                c.Rect.gameObject.SetActive(Filter.Length == 0 || c.Role == Filter);
                bool onHome = c.Id == home;
                c.Frame.color = onHome ? Gold : new Color(Cream.r, Cream.g, Cream.b, 0.12f);
                c.HomeTag.SetActive(onHome);
            }
            for (int i = 0; i < Filters.Length; i++)
            {
                bool on = Filters[i] == Filter;
                _filterPlates[i].color = on ? Cream : new Color(Cream.r, Cream.g, Cream.b, 0.08f);
                _filterLabels[i].color = on ? Ink : Muted;
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
        RectTransform _content;
        Text _pagePersonality;
        KidPageData[] _pages;

        /// <summary>The tab showing on the kid page (Skills, Skins, Story or Mastery).</summary>
        public int ActiveTab { get; private set; }

        KidPageData PageFor(string id)
        {
            if (_pages == null)
            {
                try { _pages = JsonUtility.FromJson<KidPagesRoot>(_data.Text("rules__kid_pages")).data.kids; }
                catch { _pages = new KidPageData[0]; }
            }
            foreach (KidPageData p in _pages) if (p.id == id) return p;
            return null;
        }

        void BuildPage()
        {
            _pagePanel = Ui.Fill("PagePanel", SafeRoot);
            Header(_pagePanel, "", () => Back());

            // the kid fills the space between the left column and the content panel, standing on the bottom edge
            RectTransform stageArea = Ui.Fill("StageArea", _pagePanel);
            stageArea.anchorMin = new Vector2(0.3f, 0f);
            stageArea.anchorMax = new Vector2(0.62f, 1f);
            stageArea.offsetMin = Vector2.zero;
            stageArea.offsetMax = new Vector2(0f, -HeaderHeight * 0.5f);
            _pageHero = MakeRaw("Hero", stageArea, _stage.Texture);
            _pageHero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = _pageHero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;

            // left column: role, name, one line about them, then the tabs
            RectTransform info = Ui.Panel("Info", _pagePanel, new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            info.offsetMin = new Vector2(Gutter, Gutter);
            info.offsetMax = new Vector2(0f, -HeaderHeight);

            Image disc = Ui.MakeImage("RoleDisc", info, Gold, round: true);
            Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(44f, 44f));
            disc.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRoleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            Place(_pageRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            _pageRole = Label("Role", info, "", 22, Gold, TextAnchor.MiddleLeft, display: true);
            Place(_pageRole.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(56f, -4f), new Vector2(-56f, 44f));
            _pageRole.rectTransform.pivot = new Vector2(0f, 1f);

            _pageName = Label("Name", info, "", 104, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(_pageName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(0f, 116f));
            _pageName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageName.resizeTextForBestFit = true;
            _pageName.resizeTextMinSize = 48;
            _pageName.resizeTextMaxSize = 104;

            _pagePersonality = Label("Personality", info, "", 22, Muted, TextAnchor.UpperLeft);
            Place(_pagePersonality.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -172f), new Vector2(0f, 64f));
            _pagePersonality.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pagePersonality.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pagePersonality.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform tabs = Ui.Panel("Tabs", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, TabNames.Length * HitSize));
            tabs.offsetMin = new Vector2(0f, 8f);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                RectTransform tab = Ui.Panel("Tab_" + TabNames[i], tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * HitSize), new Vector2(0f, HitSize));
                Image hit = tab.gameObject.AddComponent<Image>();
                hit.color = Clear;
                Image bar = Ui.MakeImage("Bar", tab, new Color(1f, 1f, 1f, 0.18f));
                Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 44f));
                bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                Text label = Label("Label", tab, TabNames[i], 30, Muted, TextAnchor.MiddleLeft, display: true);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.onClick.AddListener(() => SelectTab(index));
                _tabLabels.Add(label);
                _tabBars.Add(bar);
            }

            // right: the tab's content on dark glass, and the action under it
            RectTransform panel = Ui.Panel("ContentPanel", _pagePanel, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(Gutter, Gutter + HitSize + 16f);
            panel.offsetMax = new Vector2(-Gutter, -HeaderHeight);
            Image glass = Ui.MakeImage("Glass", panel, Color.white);
            glass.sprite = MenuArt.RoundGlass(26);
            glass.type = Image.Type.Sliced;
            Stretch(glass.rectTransform, Vector2.zero, Vector2.one);
            _content = Ui.Fill("Content", panel);
            _content.offsetMin = new Vector2(8f, 8f);
            _content.offsetMax = new Vector2(-8f, -8f);
            _content.gameObject.AddComponent<RectMask2D>();
            var column = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 12, 12);
            column.spacing = 6f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.childAlignment = TextAnchor.UpperLeft;

            RectTransform home = Ui.Panel("ShowOnHome", _pagePanel, new Vector2(0.62f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, HitSize));
            home.offsetMin = new Vector2(Gutter, Gutter);
            home.offsetMax = new Vector2(-Gutter, Gutter + HitSize);
            _homePlate = Ui.MakeImage("Plate", home, Color.white, false, true);
            _homePlate.sprite = MenuArt.RoundGold(22);
            _homePlate.type = Image.Type.Sliced;
            Stretch(_homePlate.rectTransform, Vector2.zero, Vector2.one);
            _homeLabel = Label("Label", home, "SHOW ON HOME", 30, Ink, TextAnchor.MiddleCenter, display: true);
            Stretch(_homeLabel.rectTransform, Vector2.zero, Vector2.one);
            _homeButton = home.gameObject.AddComponent<Button>();
            _homeButton.targetGraphic = _homePlate;
            _homeButton.onClick.AddListener(ShowOnHome);
        }

        public void OpenPage(string id)
        {
            OpenKid = id;
            _openedDirect = false;
            Haptics.LightTap();
            _gridPanel.gameObject.SetActive(false);
            _pagePanel.gameObject.SetActive(true);
            string description = Field(id, "description") ?? "";
            string role = HomeScreenView.ParseRole(description);
            _pageRoleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _pageRole.text = role + "  ·  " + Purok(description).ToUpperInvariant();
            _pageName.text = (Field(id, "display_name") ?? id).ToUpperInvariant();
            _pagePersonality.text = Story(description);
            _stage.SetKid(id);
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
                _tabLabels[i].color = on ? Cream : Muted;
                _tabBars[i].color = on ? Gold : new Color(1f, 1f, 1f, 0.18f);
            }
            for (int i = _content.childCount - 1; i >= 0; i--) KalyeahResources.Kill(_content.GetChild(i).gameObject);
            KidPageData page = OpenKid != null ? PageFor(OpenKid) : null;
            if (page == null) return;
            switch (ActiveTab)
            {
                case TabSkills:
                    foreach (SkillData skill in page.skills) AddSkill(skill);
                    break;
                case TabSkins:
                    AddRow("DEFAULT", "Equipped. New looks come from the pass and events, once they exist.", "SKIN");
                    AddRow("Victory pose", page.victory, "WIN");
                    AddRow("Idle", page.idle, "IDLE");
                    break;
                case TabStory:
                    AddFact("HOME", page.home);
                    AddFact("DREAM JOB", page.job);
                    AddFact("PUROK", Purok(Field(OpenKid, "description") ?? "") + ", plays at home on the " + HomeMapName(OpenKid));
                    AddFact("FRIENDS", string.Join(", ", page.friends));
                    AddFact("RIVAL", page.rival);
                    break;
                default:
                    AddRow("Mastery 0 of 7", "Play matches with " + (Field(OpenKid, "display_name") ?? OpenKid) + " to earn mastery. Level 3 gives a badge, 5 an emote and 7 a title: never power. Mastery comes with accounts.", "0");
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

        void AddSkill(SkillData skill)
        {
            RectTransform row = NewRow("Skill_" + skill.slot);
            Image badge = Ui.MakeImage("Badge", row, skill.slot == "ULT" ? Gold : new Color(0.2f, 0.27f, 0.4f, 1f));
            badge.sprite = MenuArt.RoundSolid(18);
            badge.type = Image.Type.Sliced;
            var badgeSize = badge.gameObject.AddComponent<LayoutElement>();
            badgeSize.minWidth = badgeSize.preferredWidth = 80f;
            badgeSize.minHeight = badgeSize.preferredHeight = 80f;
            Text slot = Label("Slot", badge.rectTransform, skill.slot, 28, skill.slot == "ULT" ? Ink : Gold, TextAnchor.MiddleCenter, display: true);
            Stretch(slot.rectTransform, Vector2.zero, Vector2.one);
            TextBlock(row, skill.name, skill.cost, skill.what);
        }

        void AddRow(string title, string body, string badgeText)
        {
            RectTransform row = NewRow("Row_" + title);
            Image badge = Ui.MakeImage("Badge", row, new Color(0.2f, 0.27f, 0.4f, 1f));
            badge.sprite = MenuArt.RoundSolid(18);
            badge.type = Image.Type.Sliced;
            var size = badge.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 80f;
            size.minHeight = size.preferredHeight = 80f;
            Text t = Label("Text", badge.rectTransform, badgeText, 24, Gold, TextAnchor.MiddleCenter, display: true);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one);
            TextBlock(row, title, null, body);
        }

        void AddFact(string head, string body)
        {
            RectTransform row = NewRow("Fact_" + head);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            Text label = Label("Head", row, head, 22, Gold, TextAnchor.UpperLeft, display: true);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 150f;
            Text value = Label("Value", row, body, 24, Cream, TextAnchor.UpperLeft);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            var vle = value.gameObject.AddComponent<LayoutElement>();
            vle.flexibleWidth = 1f;
            h.padding = new RectOffset(10, 10, 6, 6);
        }

        RectTransform NewRow(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
            go.transform.SetParent(_content, false);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            bg.sprite = MenuArt.RoundSolid(16);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 12, 12);
            h.spacing = 16f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.UpperLeft;
            return go.GetComponent<RectTransform>();
        }

        void TextBlock(RectTransform row, string title, string meta, string body)
        {
            var go = new GameObject("TextBlock", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            Text head = Label("Title", go.transform, meta == null ? title : title + "   " + meta, 30, Cream, TextAnchor.UpperLeft, display: true);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text text = Label("Body", go.transform, body, 22, Muted, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
            _homePlate.sprite = onHome ? MenuArt.RoundGlass(22) : MenuArt.RoundGold(22);
            _homeLabel.color = onHome ? Cream : Ink;
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

        // ---------------------------------------------------------------- builders

        RectTransform Header(RectTransform parent, string title, Action onBack)
        {
            RectTransform header = Ui.Panel("Header", parent, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(HitSize, HitSize));
            Image hit = back.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Image arrow = Ui.MakeImage("Arrow", back, Cream);
            arrow.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Button b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.onClick.AddListener(() => onBack());
            if (title.Length > 0)
            {
                Text t = Label("Title", header, title, 48, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
                Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 8f, 0f), new Vector2(160f, 60f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            return header;
        }

        static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, bool display = false, bool ink = false)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, display ? KalyeahResources.DisplayFont() : null);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = ink;
                if (ink)
                {
                    outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.9f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }
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

        static Texture2D s_ramp;

        /// <summary>A vertical alpha ramp: 1 at the bottom row to 0 at the top.</summary>
        static Texture2D Ramp()
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
