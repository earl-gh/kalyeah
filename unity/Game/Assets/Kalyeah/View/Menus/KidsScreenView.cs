using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kids roster &amp; hero page (Option A1 Editorial Paper + Smoked Obsidian + A2 Frosted-Titanium Skill Discs,
    /// zero role colours per docs/SPEC.md §10): 6x2 grid of clean rendered busts with hairline-underlined role filters,
    /// Paper-White ON HOME badge, 3D <see cref="HeroStage"/>, live 36-kit skill discs from <c>characters__*.json</c>,
    /// and <see cref="LoreCodexData"/> purok/rival/story dossiers.
    /// </summary>
    public sealed class KidsScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Paper = MenuArt.PaperWhite;
        static readonly Color Vermilion = MenuArt.SignalVermilion;
        static readonly Color VermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color CoralGold = MenuArt.CoralGold;
        static readonly Color WaterCyan = MenuArt.WaterCyan;
        static readonly Color ObsidianGlass = MenuArt.ObsidianGlass;
        static readonly Color ObsidianSolid = MenuArt.ObsidianSolid;
        static readonly Color ObsidianCard = new Color(0.075f, 0.102f, 0.140f, 0.88f);
        static readonly Color TitaniumBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.24f);
        static readonly Color TitaniumBorderStrong = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.46f);
        static readonly Color Muted = new Color(0.84f, 0.87f, 0.92f, 1f);
        static readonly Color MutedSoft = MenuArt.MutedText;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Shade = new Color(0.035f, 0.047f, 0.066f, 1f);
        static readonly Color SpotNormal = new Color(0.84f, 0.88f, 0.94f, 0.14f);
        static readonly Color SpotActive = new Color(1.00f, 0.94f, 0.84f, 0.24f);
        static readonly Color NameBandBg = new Color(0.045f, 0.062f, 0.088f, 0.84f);
        static readonly Color Hairline = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.20f);
        static readonly Color RoleMedallionBg = new Color(0.055f, 0.075f, 0.105f, 0.88f);
        static readonly Color PipEmpty = new Color(0.16f, 0.20f, 0.27f, 0.82f);
        static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        const float HeaderHeight = 96f;
        const float FilterHeight = 96f; // hit-box is the full 48 dp; centered editorial tab visual is 50 px tall
        const float FilterVisualInsetY = 23f;
        const float Gutter = 20f;
        const float HitSize = 96f;
        const float CardGap = 14f;
        const int Columns = 6;
        const int Rows = 2;
        const float ChipGap = 10f;

        // proportional filter widths (total = 764 px <= 920 px safe inner width on 1024x768 tablet)
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
            public Image TopAccent;
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
                    WaterDot.color = isUltimate ? CoralGold : WaterCyan;
                    WaterLabel.text = primary;
                    SetLabelColor(WaterLabel, isUltimate ? CoralGold : Paper);
                }
                CooldownChip.gameObject.SetActive(hasCooldown);
                if (hasCooldown)
                {
                    CooldownLabel.text = cooldown;
                    SetLabelColor(CooldownLabel, Paper);
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
        Text _pageRivalLine;
        Text _homeLabel;
        Button _homeButton;
        Image _homePlate;
        Image _homeIcon;
        Material _homeCtaMat;

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

            Texture2D stall = KalyeahResources.Texture("ui/home/stall") ?? KalyeahResources.Texture("ui/main_bg");
            if (stall != null)
            {
                RawImage art = MakeRaw("StallBackdrop", Root, stall);
                art.color = new Color(0.95f, 0.92f, 0.86f, 0.22f);
                var cover = art.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = stall.height > 0 ? (float)stall.width / stall.height : (16f / 9f);
            }

            Image scrim = Ui.MakeImage("BackdropScrim", Root, new Color(Shade.r, Shade.g, Shade.b, 0.78f));
            Stretch(scrim.rectTransform, Vector2.zero, Vector2.one);

            Image warmGlow = Ui.MakeImage("Light", Root, new Color(1f, 0.88f, 0.72f, 0.08f));
            warmGlow.sprite = MenuArt.Glow();
            Place(warmGlow.rectTransform, new Vector2(0.42f, 0.78f), new Vector2(0.42f, 0.78f), Vector2.zero, new Vector2(1200f, 760f));

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

            RectTransform countChip = Ui.Panel("Count", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 112f, 0f), new Vector2(128f, 34f));
            Image countBg = Ui.MakeImage("Bg", countChip, ObsidianSolid);
            Stretch(countBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(countBg, ObsidianSolid, TitaniumBorder, Vermilion, cornerCut: 4f, accentW: 3f);
            _countLabel = Label("Label", countChip, AppSession.DefaultRoster.Length + " KIDS", 18, Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(_countLabel);
            Stretch(_countLabel.rectTransform, Vector2.zero, Vector2.one);
            _countLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            _countLabel.rectTransform.offsetMax = new Vector2(-6f, 0f);

            // Hairline-underlined role filter tabs (zero role colors per docs/SPEC.md §10)
            _filterRow = Ui.Panel("Filters", _gridPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(Gutter, -HeaderHeight), new Vector2(-2f * Gutter, FilterHeight));
            Image filterTrack = Ui.MakeImage("FilterTrack", _filterRow, Hairline);
            Place(filterTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, FilterVisualInsetY), new Vector2(0f, 1f));
            filterTrack.rectTransform.pivot = new Vector2(0.5f, 0f);

            float cursorX = 0f;
            for (int i = 0; i < Filters.Length; i++)
            {
                string role = Filters[i];
                float chipWidth = FilterWidths[i];
                RectTransform chip = Ui.Panel("Filter_" + (role.Length == 0 ? "ALL" : role), _filterRow, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(cursorX, 0f), new Vector2(chipWidth, 0f));
                cursorX += chipWidth + ChipGap;

                Image hit = chip.gameObject.AddComponent<Image>();
                hit.color = Clear;

                Image plate = Ui.MakeImage("Plate", chip, ObsidianGlass);
                Stretch(plate.rectTransform, Vector2.zero, Vector2.one);
                plate.rectTransform.offsetMin = new Vector2(0f, FilterVisualInsetY);
                plate.rectTransform.offsetMax = new Vector2(0f, -FilterVisualInsetY);
                ApplyGlassSurface(plate, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);

                Image accent = Ui.MakeImage("Accent", plate.rectTransform, Vermilion);
                Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
                accent.rectTransform.pivot = new Vector2(0.5f, 0f);

                Text label = Label("Label", plate.rectTransform, role.Length == 0 ? "ALL" : role, 19, Paper, role.Length == 0 ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, display: true);
                Ui.ClearOutline(label);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                if (role.Length > 0)
                {
                    Image icon = Ui.MakeImage("Icon", plate.rectTransform, Paper);
                    icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
                    Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(20f, 20f));
                    icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                    label.rectTransform.offsetMin = new Vector2(44f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-10f, 0f);
                    _filterIcons.Add(icon);
                }
                else _filterIcons.Add(null);

                Button b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetFilter(role));
                UiShaders.AttachPressSpring(b, 0.96f, 2f);

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

            Image fill = Ui.MakeImage("Fill", rect, ObsidianCard);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.rectTransform.offsetMin = new Vector2(1f, 1f);
            fill.rectTransform.offsetMax = new Vector2(-1f, -1f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Image spotlight = Ui.MakeImage("Spotlight", fill.rectTransform, SpotNormal);
            spotlight.sprite = MenuArt.CardSpotlight();
            Stretch(spotlight.rectTransform, Vector2.zero, Vector2.one);

            Texture2D bustTex = KalyeahResources.KidBust(id);
            RawImage bust = MakeRaw("Bust", fill.rectTransform, bustTex);
            bust.uvRect = FullUv;
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f); // head stays framed cleanly
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = bustTex != null && bustTex.height > 0 ? (float)bustTex.width / bustTex.height : 0.78f;

            Image scrim = Ui.MakeImage("Fade", fill.rectTransform, Color.white);
            scrim.sprite = MenuArt.BottomScrim();
            Stretch(scrim.rectTransform, Vector2.zero, new Vector2(1f, 0.50f));

            Image nameBand = Ui.MakeImage("NameBand", fill.rectTransform, NameBandBg);
            Place(nameBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 44f));
            nameBand.rectTransform.pivot = new Vector2(0.5f, 0f);

            Image nameRule = Ui.MakeImage("NameRule", nameBand.rectTransform, Hairline);
            Place(nameRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1.5f));
            nameRule.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image frame = Ui.MakeSliced("Frame", rect, MenuArt.ChamferOutline(Color.white, cut: 8, borderPx: 2), TitaniumBorder, raycast: true);

            Image topAccent = Ui.MakeImage("TopAccent", rect, Vermilion);
            Place(topAccent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, 3f));
            topAccent.rectTransform.pivot = new Vector2(0.5f, 1f);

            // Role icon only, top left (KidsScreenTests asserts RoleChip/Icon and zero role words on cards)
            Image chip = Ui.MakeImage("RoleChip", rect, RoleMedallionBg, round: true);
            Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(34f, 34f));
            chip.rectTransform.pivot = new Vector2(0f, 1f);
            Image roleRing = Ui.MakeSliced("Ring", chip.rectTransform, MenuArt.RoundOutline(Color.white, radius: 17, borderPx: 2), TitaniumBorder);
            Image icon = Ui.MakeImage("Icon", chip.rectTransform, Paper);
            icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            // Paper-White "ON HOME" editorial badge (zero dark-ink smudge via ClearOutline!)
            RectTransform homeTag = Ui.Panel("OnHome", rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -10f), new Vector2(76f, 24f));
            Image homeTagBg = Ui.MakeImage("Plate", homeTag, Paper);
            Stretch(homeTagBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(homeTagBg, Paper, TitaniumBorderStrong, Color.clear, cornerCut: 4f, accentW: 0f);
            Text homeText = Label("Label", homeTag, "ON HOME", 13, Ink, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(homeText);
            Stretch(homeText.rectTransform, Vector2.zero, Vector2.one);
            homeText.rectTransform.offsetMin = new Vector2(4f, 0f);
            homeText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            homeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeText.verticalOverflow = VerticalWrapMode.Truncate;
            homeText.resizeTextForBestFit = true;
            homeText.resizeTextMinSize = 8;
            homeText.resizeTextMaxSize = 13;

            Text name = Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 25, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(name);
            Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-22f, 36f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 11;
            name.resizeTextMaxSize = 25;

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OpenPage(id));
            UiShaders.AttachPressSpring(b, 0.96f, 2f);

            return new Card
            {
                Id = id,
                Role = role,
                Rect = rect,
                Frame = frame,
                TopAccent = topAccent,
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
                c.Frame.color = onHome ? Paper : TitaniumBorder;
                c.TopAccent.gameObject.SetActive(onHome);
                c.Spotlight.color = onHome ? SpotActive : SpotNormal;
                c.RoleRing.color = onHome ? Paper : TitaniumBorder;
                c.RoleIcon.color = Paper;
                c.NameRule.color = onHome ? Vermilion : Hairline;
                c.HomeTag.SetActive(onHome);
            }

            if (_countLabel != null)
                _countLabel.text = Filter.Length == 0 ? AppSession.DefaultRoster.Length + " KIDS" : visible + " / " + AppSession.DefaultRoster.Length + " KIDS";

            for (int i = 0; i < Filters.Length; i++)
            {
                bool on = Filters[i] == Filter;
                ApplyGlassSurface(
                    _filterPlates[i],
                    on ? ObsidianSolid : new Color(0.055f, 0.075f, 0.102f, 0.42f),
                    on ? TitaniumBorderStrong : TitaniumBorder,
                    Color.clear,
                    cornerCut: 4f,
                    accentW: 0f);
                _filterAccents[i].gameObject.SetActive(on);
                SetLabelColor(_filterLabels[i], on ? Paper : MutedSoft);
                if (_filterIcons[i] != null) _filterIcons[i].color = on ? Paper : MutedSoft;
            }
            RefreshLayout();
        }

        // ---------------------------------------------------------------- kid page

        public const int TabSkills = 0;
        public const int TabSkins = 1;
        public const int TabStory = 2;
        public const int TabMastery = 3;
        static readonly string[] TabNames = { "SKILLS", "SKINS", "STORY", "MASTERY" };

        [Serializable] public sealed class SkillData { public string id, slot, name, what, cost; }
        [Serializable] public sealed class KidPageData { public string id, home, job, rival, idle, victory; public string[] friends; public SkillData[] skills; }
        [Serializable] sealed class KidPagesRoot { public KidPagesBody data; }
        [Serializable] sealed class KidPagesBody { public KidPageData[] kids; }

        readonly List<Text> _tabLabels = new List<Text>();
        readonly List<Image> _tabBars = new List<Image>();
        RectTransform _content;
        Text _pagePersonality;
        static KidPageData[] s_pages;

        /// <summary>The tab showing on the kid page (Skills, Skins, Story or Mastery).</summary>
        public int ActiveTab { get; private set; }

        /// <summary>A kid's skills and story text (display text fallback from assets/ui/kid_pages.json).</summary>
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

        /// <summary>
        /// Reads live 36-kit skill entries directly from <c>characters__&lt;id&gt;.json</c> (preserving canonical
        /// Water cost &amp; cooldown formatting for tests and resolving real skill IDs for <see cref="MenuArt.SkillEmblem"/>),
        /// falling back cleanly to <see cref="PageFor"/> if a minimal synthetic <see cref="DataSource"/> omits <c>skills</c>.
        /// </summary>
        internal static SkillData[] ResolveSkillsFor(DataSource data, string id)
        {
            if (!string.IsNullOrEmpty(id) && data != null)
            {
                try
                {
                    Dictionary<string, object> p = data.Props("characters__" + id);
                    if (p != null && p.TryGetValue("skills", out object skillsObj) && skillsObj is List<object> rawList && rawList.Count > 0)
                    {
                        var resolved = new SkillData[Mathf.Min(3, rawList.Count)];
                        for (int i = 0; i < resolved.Length; i++)
                        {
                            if (!(rawList[i] is Dictionary<string, object> sd)) continue;
                            string rawSlot = ReadDictString(sd, "slot", i == 0 ? "s1" : i == 1 ? "s2" : "ult");
                            string slot = rawSlot.ToUpperInvariant();
                            bool isUlt = slot == "ULT";
                            string skillId = ReadDictString(sd, "id", $"{id}_{rawSlot}");
                            string displayName = ReadDictString(sd, "display_name", skillId);
                            string desc = ReadDictString(sd, "description", "");
                            int water = ReadDictInt(sd, "water_cost", 25);
                            float cd = ReadDictFloat(sd, "cooldown", isUlt ? 85f : 10f);
                            string cdStr = cd.ToString("0.#", CultureInfo.InvariantCulture);
                            string cost = isUlt
                                ? $"Ultimate, {cdStr}s cooldown"
                                : $"{water} water, {cdStr}s cooldown";
                            resolved[i] = new SkillData
                            {
                                id = skillId,
                                slot = slot,
                                name = displayName,
                                what = desc,
                                cost = cost,
                            };
                        }
                        if (resolved.Length > 0 && resolved[0] != null)
                            return resolved;
                    }
                }
                catch
                {
                    // Fall through to kid_pages.json fallback.
                }
            }

            KidPageData page = PageFor(id);
            if (page != null && page.skills != null && page.skills.Length > 0)
            {
                for (int i = 0; i < page.skills.Length; i++)
                {
                    if (string.IsNullOrEmpty(page.skills[i].id))
                        page.skills[i].id = SlugFromName(page.skills[i].name);
                }
                return page.skills;
            }
            return Array.Empty<SkillData>();
        }

        void BuildPage()
        {
            _pagePanel = Ui.Fill("PagePanel", SafeRoot);

            // built first so its drag-to-orbit surface never intercepts Header, Info or Tab clicks
            RectTransform stageArea = Ui.Fill("StageArea", _pagePanel);
            stageArea.anchorMin = new Vector2(0.24f, 0.02f);
            stageArea.anchorMax = new Vector2(0.58f, 0.96f);
            stageArea.offsetMin = Vector2.zero;
            stageArea.offsetMax = Vector2.zero;

            Image stageSpot = Ui.MakeImage("StageSpot", stageArea, new Color(1.00f, 0.94f, 0.84f, 0.14f));
            stageSpot.sprite = MenuArt.CardSpotlight();
            Stretch(stageSpot.rectTransform, Vector2.zero, Vector2.one);

            _pageHero = MakeRaw("Hero", stageArea, _stage.Texture);
            _pageHero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = _pageHero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;
            _stage.AttachOrbitDrag(_pageHero.rectTransform);

            Header(_pagePanel, "", () => Back());

            // left column: role-purok pill, bold editorial name, vermilion slash, LoreCodexData bio & rival, Signal Vermilion CTA
            RectTransform info = Ui.Panel("Info", _pagePanel, new Vector2(0f, 0f), new Vector2(0.295f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            info.offsetMin = new Vector2(Gutter, Gutter);
            info.offsetMax = new Vector2(0f, -HeaderHeight);

            Image rolePill = Ui.MakeImage("RolePill", info, ObsidianSolid);
            Place(rolePill.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 42f));
            rolePill.rectTransform.pivot = new Vector2(0.5f, 1f);
            ApplyGlassSurface(rolePill, ObsidianSolid, TitaniumBorder, Paper, cornerCut: 5f, accentW: 3f);

            Image disc = Ui.MakeImage("RoleDisc", info, Paper, round: true);
            Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(30f, 30f));
            disc.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRoleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            Place(_pageRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            // Role stays a direct child of Info for KidsScreenTests
            _pageRole = Label("Role", info, "", 18, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(_pageRole);
            Place(_pageRole.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -2f), new Vector2(-56f, 42f));
            _pageRole.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRole.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageRole.verticalOverflow = VerticalWrapMode.Truncate;
            _pageRole.resizeTextForBestFit = true;
            _pageRole.resizeTextMinSize = 10;
            _pageRole.resizeTextMaxSize = 18;

            _pageName = Label("Name", info, "", 84, Paper, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_pageName);
            Place(_pageName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(0f, 94f));
            _pageName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageName.verticalOverflow = VerticalWrapMode.Truncate;
            _pageName.resizeTextForBestFit = true;
            _pageName.resizeTextMinSize = 26;
            _pageName.resizeTextMaxSize = 84;

            Image nameBar = Ui.MakeImage("NameBar", info, Vermilion);
            Place(nameBar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -150f), new Vector2(54f, 4f));
            nameBar.rectTransform.pivot = new Vector2(0f, 1f);

            _pageRivalLine = Label("RivalTag", info, "", 15, CoralGold, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_pageRivalLine);
            Place(_pageRivalLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -162f), new Vector2(0f, 22f));
            _pageRivalLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageRivalLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageRivalLine.verticalOverflow = VerticalWrapMode.Truncate;
            _pageRivalLine.resizeTextForBestFit = true;
            _pageRivalLine.resizeTextMinSize = 10;
            _pageRivalLine.resizeTextMaxSize = 15;

            _pagePersonality = Label("Personality", info, "", 20, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(_pagePersonality);
            Place(_pagePersonality.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(0f, 150f));
            _pagePersonality.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pagePersonality.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pagePersonality.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform home = Ui.Panel("ShowOnHome", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, HitSize));
            Image homeHit = home.gameObject.AddComponent<Image>();
            homeHit.color = Clear;

            _homePlate = Ui.MakeImage("Plate", home, Vermilion);
            Stretch(_homePlate.rectTransform, Vector2.zero, Vector2.one);
            _homePlate.rectTransform.offsetMin = new Vector2(0f, 12f);
            _homePlate.rectTransform.offsetMax = new Vector2(0f, -12f);
            _homeCtaMat = UiShaders.CreateMetallicCta(Vermilion, VermilionDeep, cornerCut: 10f);

            _homeIcon = Ui.MakeImage("CueIcon", _homePlate.rectTransform, Paper);
            Place(_homeIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(24f, 24f));
            _homeIcon.rectTransform.pivot = new Vector2(0f, 0.5f);

            _homeLabel = Label("Label", _homePlate.rectTransform, "SHOW ON HOME", 24, Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(_homeLabel);
            Stretch(_homeLabel.rectTransform, Vector2.zero, Vector2.one);
            _homeLabel.rectTransform.offsetMin = new Vector2(46f, 2f);
            _homeLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            _homeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _homeLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _homeLabel.resizeTextForBestFit = true;
            _homeLabel.resizeTextMinSize = 11;
            _homeLabel.resizeTextMaxSize = 24;

            _homeButton = home.gameObject.AddComponent<Button>();
            _homeButton.targetGraphic = _homePlate;
            _homeButton.transition = Selectable.Transition.None;
            _homeButton.onClick.AddListener(ShowOnHome);
            UiShaders.AttachPressSpring(_homeButton, 0.94f, 3f);

            // right: four razor-crisp hairline-underlined tabs, then smoked obsidian tactical glass content
            RectTransform tabs = Ui.Panel("Tabs", _pagePanel, new Vector2(0.52f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HitSize));
            tabs.offsetMin = new Vector2(Gutter, -HeaderHeight);
            tabs.offsetMax = new Vector2(-Gutter, 0f);

            Image tabTrack = Ui.MakeImage("Track", tabs, Hairline);
            Place(tabTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 1f));
            tabTrack.rectTransform.pivot = new Vector2(0.5f, 0f);

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                RectTransform tab = Ui.Panel("Tab_" + TabNames[i], tabs, new Vector2(i / (float)TabNames.Length, 0f), new Vector2((i + 1) / (float)TabNames.Length, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                tab.offsetMin = new Vector2(2f, 0f);
                tab.offsetMax = new Vector2(-2f, 0f);

                Image hit = tab.gameObject.AddComponent<Image>();
                hit.color = Clear;

                Text label = Label("Label", tab, TabNames[i], 24, MutedSoft, TextAnchor.MiddleCenter, display: true);
                Ui.ClearOutline(label);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                label.rectTransform.offsetMin = new Vector2(0f, 10f);

                Image bar = Ui.MakeImage("Bar", tab, Vermilion);
                Place(bar.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 9f), new Vector2(0f, 3f));
                bar.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectTab(index));
                UiShaders.AttachPressSpring(b, 0.96f, 2f);

                _tabLabels.Add(label);
                _tabBars.Add(bar);
            }

            RectTransform panel = Ui.Panel("ContentPanel", _pagePanel, new Vector2(0.52f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(Gutter, Gutter);
            panel.offsetMax = new Vector2(-Gutter, -(HeaderHeight + 4f));
            Image glassBg = Ui.MakeImage("Glass", panel, ObsidianGlass);
            Stretch(glassBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(glassBg, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

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
            string purok = Purok(description);
            if (string.IsNullOrEmpty(purok) && LoreCodexData.TryGetKid(id, out KidLoreEntry lorePurok))
                purok = LoreCodexData.PurokName(lorePurok.Purok);

            _pageRoleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _pageRole.text = role + " \u00B7 " + purok.ToUpperInvariant();
            _pageName.text = (Field(id, "display_name") ?? id).ToUpperInvariant();

            string rivalId = LoreCodexData.GetRivalId(id);
            string rivalLabel = "";
            if (!string.IsNullOrEmpty(rivalId))
            {
                string rName = LoreCodexData.TryGetKid(rivalId, out KidLoreEntry rLore) ? rLore.DisplayName : rivalId;
                rivalLabel = "RIVAL · " + rName.ToUpperInvariant();
            }
            if (_pageRivalLine != null) _pageRivalLine.text = rivalLabel;

            string story = Story(description);
            if (string.IsNullOrEmpty(story) && LoreCodexData.TryGetKid(id, out KidLoreEntry loreStory))
                story = loreStory.Personality;
            _pagePersonality.text = story;

            _stage.SetKid(id);
            _stage.ResetOrbit();
            _stage.RenderNow();
            SelectTab(TabSkills);
            RefreshPage();
            RefreshLayout();
        }

        /// <summary>Shows one tab's content for the open kid: live 36-kit skills, skins, LoreCodexData story, or mastery.</summary>
        public void SelectTab(int tab)
        {
            ActiveTab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            for (int i = 0; i < _tabLabels.Count; i++)
            {
                bool on = i == ActiveTab;
                SetLabelColor(_tabLabels[i], on ? Paper : MutedSoft);
                _tabBars[i].gameObject.SetActive(on);
            }
            for (int i = _content.childCount - 1; i >= 0; i--) KalyeahResources.Kill(_content.GetChild(i).gameObject);
            _rowTop = 0f;
            if (OpenKid == null) return;

            KidPageData page = PageFor(OpenKid);
            bool hasLore = LoreCodexData.TryGetKid(OpenKid, out KidLoreEntry lore);

            switch (ActiveTab)
            {
                case TabSkills:
                    SkillData[] liveSkills = ResolveSkillsFor(_data, OpenKid);
                    if (liveSkills.Length > 0) AddSkills(liveSkills);
                    break;
                case TabSkins:
                    string victory = hasLore && !string.IsNullOrEmpty(lore.VictoryPose) ? lore.VictoryPose : (page != null ? page.victory : "");
                    string idle = hasLore && !string.IsNullOrEmpty(lore.IdleAction) ? lore.IdleAction : (page != null ? page.idle : "");
                    AddRow("DEFAULT", "Equipped. New looks come from the pass and events, once they exist.", "SKIN", 122f, accentPrimary: true);
                    AddRow("VICTORY POSE", victory, "WIN", 122f, accentPrimary: false);
                    AddRow("IDLE", idle, "IDLE", 122f, accentPrimary: false);
                    break;
                case TabStory:
                    string home = hasLore && !string.IsNullOrEmpty(lore.HomeAndFamily) ? lore.HomeAndFamily : (page != null ? page.home : "");
                    string job = hasLore && !string.IsNullOrEmpty(lore.DreamJob) ? lore.DreamJob : (page != null ? page.job : "");
                    string purokText = Purok(Field(OpenKid, "description") ?? "");
                    if (string.IsNullOrEmpty(purokText) && hasLore) purokText = LoreCodexData.PurokName(lore.Purok);
                    string friendsText = ResolveFriendsText(OpenKid, page);
                    string rivalText = ResolveRivalText(OpenKid, page);
                    AddFact("HOME", home);
                    AddFact("DREAM JOB", job);
                    AddFact("PUROK", purokText + ", plays at home on the " + HomeMapName(OpenKid));
                    AddFact("FRIENDS", friendsText);
                    AddFact("RIVAL", rivalText);
                    break;
                default:
                    AddMasteryCard(Field(OpenKid, "display_name") ?? OpenKid);
                    break;
            }
        }

        static string ResolveFriendsText(string kidId, KidPageData page)
        {
            var ids = new List<string>(4);
            LoreCodexData.GetPurokFriends(kidId, ids);
            if (ids.Count > 0)
            {
                var names = new string[ids.Count];
                for (int i = 0; i < ids.Count; i++)
                    names[i] = LoreCodexData.TryGetKid(ids[i], out KidLoreEntry f) ? f.DisplayName : ids[i];
                return string.Join(", ", names);
            }
            return page != null && page.friends != null ? string.Join(", ", page.friends) : "";
        }

        static string ResolveRivalText(string kidId, KidPageData page)
        {
            string rivalId = LoreCodexData.GetRivalId(kidId);
            if (!string.IsNullOrEmpty(rivalId))
            {
                string name = LoreCodexData.TryGetKid(rivalId, out KidLoreEntry r) ? r.DisplayName : rivalId;
                if (page != null && !string.IsNullOrEmpty(page.rival) && page.rival.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return page.rival;
                return name;
            }
            return page != null ? (page.rival ?? "") : "";
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
        const float RowGap = 8f;
        const float BadgeWidth = 74f;

        RectTransform NewRow(string name, float height, bool accentPrimary)
        {
            RectTransform row = Ui.Panel(name, _content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_rowTop), new Vector2(0f, height));
            Image bg = Ui.MakeImage("Bg", row, ObsidianCard);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(bg, ObsidianCard, TitaniumBorder, accentPrimary ? Vermilion : Paper, cornerCut: 6f, accentW: accentPrimary ? 3f : 2f);
            _rowTop += height + RowGap;
            return row;
        }

        void AddRow(string title, string body, string badgeText, float height, bool accentPrimary)
        {
            RectTransform row = NewRow("Row_" + title, height, accentPrimary);
            RectTransform badge = Ui.Panel("Badge", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(BadgeWidth, 30f));
            Image badgeBg = Ui.MakeImage("Plate", badge, accentPrimary ? Paper : ObsidianSolid);
            Stretch(badgeBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(badgeBg, accentPrimary ? Paper : ObsidianSolid, TitaniumBorderStrong, Color.clear, cornerCut: 4f, accentW: 0f);
            Text bt = Label("Text", badge, badgeText, 16, accentPrimary ? Ink : Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(bt);
            Stretch(bt.rectTransform, Vector2.zero, Vector2.one);

            const float left = 16f + BadgeWidth + 12f;
            Text head = Label("Title", row, title, 24, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(head);
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -13f), new Vector2(-(left + 14f), 32f));
            head.rectTransform.pivot = new Vector2(0f, 1f);

            Text text = Label("Body", row, body, 19, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(text);
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -52f), new Vector2(-32f, height - 60f));
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void AddFact(string head, string body)
        {
            // Hairline-divided editorial fact row (de-boxed!)
            const float factH = 80f;
            RectTransform row = Ui.Panel("Fact_" + head, _content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_rowTop), new Vector2(0f, factH));
            Image rule = Ui.MakeImage("Rule", row, Hairline);
            Place(rule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-12f, 1f));
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);

            Text label = Label("Head", row, head, 16, CoralGold, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(label);
            Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 0f), new Vector2(122f, 0f));
            label.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text value = Label("Value", row, body, 19, Paper, TextAnchor.MiddleLeft);
            Ui.ClearOutline(value);
            Stretch(value.rectTransform, Vector2.zero, Vector2.one);
            value.rectTransform.offsetMin = new Vector2(142f, 6f);
            value.rectTransform.offsetMax = new Vector2(-12f, -6f);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            value.verticalOverflow = VerticalWrapMode.Overflow;
            _rowTop += factH + 4f;
        }

        void AddMasteryCard(string kidName)
        {
            RectTransform row = NewRow("Row_Mastery", 232f, accentPrimary: true);
            RectTransform badge = Ui.Panel("Badge", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(68f, 32f));
            Image badgeBg = Ui.MakeImage("Plate", badge, Paper);
            Stretch(badgeBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(badgeBg, Paper, TitaniumBorderStrong, Color.clear, cornerCut: 4f, accentW: 0f);
            Text bt = Label("Text", badge, "LVL 0", 16, Ink, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(bt);
            Stretch(bt.rectTransform, Vector2.zero, Vector2.one);

            Text head = Label("Title", row, "Mastery 0 of 7", 26, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(head);
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(98f, -16f), new Vector2(-114f, 36f));
            head.rectTransform.pivot = new Vector2(0f, 1f);

            // 7-segment mastery progress track (0 of 7 lit until accounts exist)
            RectTransform track = Ui.Panel("Track", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(-36f, 10f));
            for (int i = 0; i < 7; i++)
            {
                Image pip = Ui.MakeImage("Pip_" + i, track, PipEmpty);
                pip.rectTransform.anchorMin = new Vector2(i / 7f, 0f);
                pip.rectTransform.anchorMax = new Vector2((i + 1f) / 7f, 1f);
                pip.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 3f, 0f);
                pip.rectTransform.offsetMax = new Vector2(i == 6 ? 0f : -3f, 0f);
            }

            Text body = Label("Body", row, "Play matches with " + kidName + " to earn mastery. Level 3 gives a badge, 5 an emote and 7 a title: never power. Mastery comes with accounts.", 19, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(body);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -88f), new Vector2(-36f, 126f));
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
        /// The Skills tab: three A2 Frosted-Titanium Skill Discs with live <see cref="MenuArt.SkillEmblem"/> icons
        /// (zero literal "S1"/"S2"/"ULT" text circles!) and one detailed smoked-obsidian card with WATER and COOLDOWN pills.
        /// </summary>
        void AddSkills(SkillData[] skills)
        {
            int count = Mathf.Min(3, skills.Length);
            _skillIndex = Mathf.Clamp(_skillIndex, 0, Mathf.Max(0, count - 1));

            for (int i = 0; i < count; i++)
            {
                int index = i;
                SkillData skill = skills[i];
                bool on = i == _skillIndex;
                bool ultimate = skill.slot == "ULT";
                RectTransform tile = Ui.Panel("Skill_" + skill.slot, _content, new Vector2(i / (float)count, 1f), new Vector2((i + 1f) / count, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, SkillTileHeight));
                tile.offsetMin = new Vector2(i == 0 ? 0f : RowGap * 0.5f, tile.offsetMin.y);
                tile.offsetMax = new Vector2(i == count - 1 ? 0f : -RowGap * 0.5f, tile.offsetMax.y);

                Image plate = Ui.MakeImage("Plate", tile, ObsidianCard, raycast: true);
                Stretch(plate.rectTransform, Vector2.zero, Vector2.one);
                ApplyGlassSurface(
                    plate,
                    on ? ObsidianSolid : ObsidianCard,
                    on ? (ultimate ? Vermilion : Paper) : TitaniumBorder,
                    on ? (ultimate ? Vermilion : Paper) : Color.clear,
                    cornerCut: 6f,
                    accentW: on ? 3f : 0f);

                // A2 Frosted-Titanium Skill Disc + crisp vector SkillEmblem (kills literal "S1"/"S2"/"ULT" text circles!)
                RectTransform discHolder = Ui.Panel("Badge", tile, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(56f, 56f));
                Image discFace = Ui.MakeImage("DiscFace", discHolder, Color.white);
                Stretch(discFace.rectTransform, Vector2.zero, Vector2.one);
                Color rimColor = on
                    ? (ultimate ? Vermilion : Paper)
                    : (ultimate ? CoralGold : MenuArt.TitaniumRim);
                Material discMat = UiShaders.CreateFrostedTitaniumDisc(
                    new Color(0.075f, 0.105f, 0.145f, on ? 0.90f : 0.74f),
                    rimColor,
                    innerRing: 0.78f);
                if (discMat != null && discMat != Graphic.defaultGraphicMaterial)
                {
                    if (discMat.HasProperty(UiShaders.PropRimGlow)) discMat.SetFloat(UiShaders.PropRimGlow, on ? 0.90f : 0.25f);
                    if (discMat.HasProperty(UiShaders.PropUltPulse)) discMat.SetFloat(UiShaders.PropUltPulse, ultimate ? (on ? 0.85f : 0.35f) : 0f);
                }
                ApplyDiscMaterial(discFace, discMat, ObsidianSolid);

                Image emblem = Ui.MakeImage("Emblem", discHolder, on ? Paper : new Color(Paper.r, Paper.g, Paper.b, 0.86f));
                emblem.sprite = MenuArt.SkillEmblem(skill.id);
                emblem.preserveAspect = true;
                Stretch(emblem.rectTransform, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));

                Text caption = Label("Caption", tile, SlotCaption(skill.slot), 14, on ? Paper : (ultimate ? CoralGold : MutedSoft), TextAnchor.LowerCenter, display: true);
                Ui.ClearOutline(caption);
                Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-8f, 20f));
                caption.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tile.gameObject.AddComponent<Button>();
                b.targetGraphic = plate;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectSkill(index));
                UiShaders.AttachPressSpring(b, 0.94f, 2f);
            }

            SkillData shown = skills[_skillIndex];
            bool shownUlt = shown.slot == "ULT";
            RectTransform card = Ui.Panel("SkillCard", _content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMax = new Vector2(0f, -(SkillTileHeight + RowGap + 2f));
            Image cardBg = Ui.MakeImage("Bg", card, ObsidianCard);
            Stretch(cardBg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(cardBg, ObsidianCard, shownUlt ? Vermilion : TitaniumBorderStrong, shownUlt ? Vermilion : Paper, cornerCut: 8f, accentW: 3f);

            Text kicker = Label("Kicker", card, shownUlt ? "ULTIMATE ABILITY" : SlotCaption(shown.slot), 15, shownUlt ? Vermilion : CoralGold, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(kicker);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -14f), new Vector2(-42f, 22f));
            kicker.rectTransform.pivot = new Vector2(0f, 1f);

            Text title = Label("Title", card, shown.name, 36, Paper, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(title);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -36f), new Vector2(-42f, 44f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 15;
            title.resizeTextMaxSize = 36;

            SkillStatChips chips = BuildSkillStatChips(card, topOffsetY: -86f, leftPad: 22f, rightPad: 18f, height: 34f, fontSize: 17);
            chips.Apply(shown.cost);

            // keeps canonical raw cost string for KidsScreenTests while UI renders separate WATER + COOLDOWN stat pills
            Text rawCost = Label("Cost", card, shown.cost, 16, MutedSoft, TextAnchor.UpperLeft);
            rawCost.gameObject.SetActive(false);

            Image divider = Ui.MakeImage("Divider", card, Hairline);
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -130f), new Vector2(-42f, 1f));
            divider.rectTransform.pivot = new Vector2(0f, 1f);

            Text body = Label("Body", card, shown.what, 21, Paper, TextAnchor.UpperLeft);
            Ui.ClearOutline(body);
            Stretch(body.rectTransform, Vector2.zero, Vector2.one);
            body.rectTransform.offsetMin = new Vector2(22f, 16f);
            body.rectTransform.offsetMax = new Vector2(-20f, -142f);
            body.lineSpacing = 1.18f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 12;
            body.resizeTextMaxSize = 21;
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
            if (onHome)
            {
                ApplyGlassSurface(_homePlate, ObsidianSolid, TitaniumBorderStrong, Paper, cornerCut: 8f, accentW: 3f);
                _homeIcon.sprite = MenuArt.CheckBadge();
                _homeIcon.color = Paper;
                SetLabelColor(_homeLabel, Paper);
            }
            else
            {
                ApplyCtaSurface(_homePlate, _homeCtaMat, Vermilion);
                _homeIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Star);
                _homeIcon.color = Paper;
                SetLabelColor(_homeLabel, Paper);
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

        static string ReadDictString(Dictionary<string, object> d, string key, string fallback) =>
            d != null && d.TryGetValue(key, out object v) && v is string s && !string.IsNullOrWhiteSpace(s) ? s : fallback;

        static int ReadDictInt(Dictionary<string, object> d, string key, int fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { }
            }
            return fallback;
        }

        static float ReadDictFloat(Dictionary<string, object> d, string key, float fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
                catch { }
            }
            return fallback;
        }

        static string SlugFromName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "tirador";
            return raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }

        // ---------------------------------------------------------------- shared Option A1 builders

        internal static void ApplyGlassSurface(Image img, Color fill, Color border, Color leftAccent, float cornerCut = 6f, float accentW = 0f)
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

        internal static void ApplyCtaSurface(Image img, Material ctaMat, Color fallback)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            if (ctaMat != null && ctaMat != Graphic.defaultGraphicMaterial)
            {
                img.material = ctaMat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = fallback;
            }
        }

        internal static void ApplyDiscMaterial(Image img, Material discMat, Color fallback)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            if (discMat != null && discMat != Graphic.defaultGraphicMaterial)
            {
                img.material = discMat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = fallback;
            }
        }

        RectTransform Header(RectTransform parent, string title, Action onBack)
        {
            RectTransform header = Ui.Panel("Header", parent, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            MakeBackButton(header, Gutter, HitSize, onBack);
            if (title.Length > 0)
            {
                Image slash = Ui.MakeImage("TitleSlash", header, Vermilion);
                Place(slash.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 2f, 0f), new Vector2(4f, 34f));
                slash.rectTransform.pivot = new Vector2(0f, 0.5f);

                Text t = Label("Title", header, title, 44, Paper, TextAnchor.MiddleLeft, display: true);
                Ui.ClearOutline(t);
                Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 14f, 0f), new Vector2(140f, 56f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            return header;
        }

        internal static void MakeBackButton(RectTransform header, float gutter, float hitSize, Action onBack)
        {
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(gutter, 0f), new Vector2(hitSize, hitSize));
            Image hit = back.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Image plate = Ui.MakeImage("Plate", back, ObsidianSolid);
            Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            ApplyGlassSurface(plate, ObsidianSolid, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);
            Image arrow = Ui.MakeImage("Arrow", plate.rectTransform, Paper);
            arrow.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Button b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onBack());
            UiShaders.AttachPressSpring(b, 0.94f, 2f);
        }

        internal static SkillStatChips BuildSkillStatChips(Transform parent, float topOffsetY, float leftPad, float rightPad, float height, int fontSize)
        {
            RectTransform row = Ui.Panel("StatChips", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height));
            row.offsetMin = new Vector2(leftPad, topOffsetY - height);
            row.offsetMax = new Vector2(-rightPad, topOffsetY);

            RectTransform waterChip = MakeStatChipBox("WaterChip", row, 0f, 0.44f, 0f, -4f, fontSize, WaterCyan, out Image waterDot, out Text waterLabel);
            RectTransform cdChip = MakeStatChipBox("CooldownChip", row, 0.44f, 1f, 4f, 0f, fontSize, CoralGold, out _, out Text cdLabel);
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
            Image bg = Ui.MakeImage("Bg", chip, ObsidianSolid);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one);
            ApplyGlassSurface(bg, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);

            dot = Ui.MakeImage("Dot", chip, dotColor, round: true);
            Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(7f, 7f));
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);

            label = Label("Label", chip, "", fontSize, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(label);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = fontSize;
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
