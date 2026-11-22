using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kid select after the queue (Option A1 Editorial Paper + Smoked Obsidian + A2 Frosted-Titanium Skill Discs,
    /// zero role colours per docs/SPEC.md §10): 12 kids in four role rows on the left, 3D hero + <see cref="LoreCodexData"/>
    /// nameplate in the center, live 36-kit skill discs (<see cref="MenuArt.SkillEmblem"/>) + WATER/COOLDOWN chips on the right,
    /// and a grounded smoked-obsidian squad dock beside a Signal Vermilion LOCK IN CTA.
    /// </summary>
    public sealed class PickScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Paper = MenuArt.PaperWhite;
        static readonly Color Vermilion = MenuArt.SignalVermilion;
        static readonly Color VermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color CoralGold = MenuArt.CoralGold;
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
        static readonly Color TakenScrim = new Color(0.030f, 0.042f, 0.060f, 0.84f);
        static readonly Color WarnAmber = MenuArt.CoralGold;
        static readonly Color RoleMedallionBg = new Color(0.055f, 0.075f, 0.105f, 0.88f);
        static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        const float HeaderHeight = 96f;
        const float TeamBarHeight = 96f;
        const float Gutter = 20f;
        const float GutterBottom = 16f;
        const float HitSize = 96f;
        const float RowGap = 8f;
        const float RowLabelWidth = 88f;
        const float CardGap = 8f;
        const float NamePlateHeight = 86f;
        const float SkillTabHeight = 98f;
        const float SkillTabGap = 8f;
        const float DiscSize = 48f;
        const float SlotSize = 54f;
        const float SlotGap = 8f;
        const float LockWidth = 260f;
        const float OpenChipWidth = 92f;
        const float OpenChipGap = 5f;
        const int Columns = 3;
        const int TeamSize = 5;
        const int SkillCount = 3;
        const int MaxOpenRoleChips = 3;

        // the body's three columns, as shares of its width (920 px on 1024x768 tablet, 1240 px on 16:9)
        const float RosterEnd = 0.445f;
        const float HeroAreaStart = 0.435f;
        const float HeroAreaEnd = 0.685f;
        const float NamePlateStart = 0.455f;
        const float NamePlateEnd = 0.665f;
        const float DetailStart = 0.675f;

        /// <summary>The four role rows, top to bottom (the order of the kids filter).</summary>
        public static readonly string[] Roles = { "STRIKER", "DEFENDER", "SEEKER", "CONTROLLER" };

        sealed class PickLifecycle : MonoBehaviour
        {
            public PickScreenView Owner;
            void Update() => Owner?.Tick(Time.unscaledDeltaTime);
            void OnDestroy() => Owner?.ReleaseOwnedMaterials();
        }

        sealed class Card
        {
            public Image Frame;
            public Image TopAccent;
            public Image Spotlight;
            public Image NameRule;
            public GameObject TakenTag;
        }

        sealed class Slot
        {
            public Image Ring;
            public RawImage Bust;
            public GameObject EmptyIcon;
        }

        sealed class OpenRoleChip
        {
            public GameObject Root;
            public Image Outline;
            public Image Icon;
            public Text Label;
        }

        sealed class SkillTab
        {
            public Image Plate;
            public Image DiscFace;
            public Material DiscMat;
            public Image Emblem;
            public Text Caption;
            public GameObject Root;
        }

        readonly DataSource _data;
        readonly Action _onBack;
        readonly Action _onConfirmed;
        readonly HeroStage _stage;
        readonly Dictionary<string, Card> _cards = new Dictionary<string, Card>(StringComparer.Ordinal);
        readonly HashSet<string> _taken = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _takenOrder = new List<string>(TeamSize);
        readonly List<Slot> _slots = new List<Slot>(TeamSize);
        readonly List<OpenRoleChip> _openRoleChips = new List<OpenRoleChip>(MaxOpenRoleChips);
        readonly List<SkillTab> _skillTabs = new List<SkillTab>(SkillCount);
        string _selected = AppSession.DefaultCharacter;
        int _skill;
        bool _disposed;

        Text _roleLabel;
        Image _roleIcon;
        Text _nameLabel;
        Image _skillCardBg;
        Text _skillKicker;
        Text _skillTitle;
        KidsScreenView.SkillStatChips _skillChips;
        Text _skillCostRaw;
        Text _skillBody;
        Text _openRoles;
        RectTransform _openRolesRect;
        Button _lockButton;
        Image _lockPlate;
        Image _lockIcon;
        Text _lockLabel;
        Material _lockCtaMat;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public GameObject GameObject => Root != null ? Root.gameObject : null;
        public AudioHub Audio { get; set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
        public string SelectedCharacterId => _selected;
        /// <summary>The skill whose description shows: 0 and 1 are the two skills, 2 the ultimate.</summary>
        public int SelectedSkill => _skill;
        public Button LockButton => _lockButton;

        /// <summary>LOCK IN works when the kid is real and no teammate took it.</summary>
        public bool CanConfirm => Array.IndexOf(AppSession.DefaultRoster, _selected) >= 0 && !_taken.Contains(_selected);

        public event Action BackRequested;
        public event Action Confirmed;
        public event Action<string> CharacterSelected;

        public PickScreenView(Transform parent, DataSource data, Action onBack, Action onConfirmed = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data;
            _onBack = onBack;
            _onConfirmed = onConfirmed;
            _stage = new HeroStage();

            Root = Ui.Fill("PickScreen", parent);
            BuildBackdrop(Root);

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null) SafeRoot = Ui.Fill("PickContent", Root);
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                SafeRoot.gameObject.AddComponent<SafeArea>().Apply();
            }

            BuildHeader();
            RectTransform body = Ui.Fill("Body", SafeRoot);
            body.offsetMin = new Vector2(Gutter, GutterBottom + TeamBarHeight + 14f);
            body.offsetMax = new Vector2(-Gutter, -HeaderHeight);

            BuildHero(body);
            BuildRoster(body);
            BuildDetail(body);
            BuildTeamBar();

            Root.gameObject.AddComponent<PickLifecycle>().Owner = this;
            RefreshFromSession();
        }

        // ---------------------------------------------------------------- build

        static void BuildBackdrop(RectTransform root)
        {
            Image fill = Ui.MakeImage("Backdrop", root, Shade, raycast: true);
            KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.gameObject.AddComponent<RectMask2D>();
            Texture2D art = KalyeahResources.Texture("ui/home/stall") ?? KalyeahResources.Texture("ui/main_bg");
            if (art != null)
            {
                RawImage raw = KidsScreenView.MakeRaw("Stall", fill.rectTransform, art);
                raw.color = new Color(0.95f, 0.92f, 0.86f, 0.24f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 16f / 9f;
            }
            Image wash = Ui.MakeImage("Wash", fill.rectTransform, new Color(Shade.r, Shade.g, Shade.b, 0.78f));
            KidsScreenView.Stretch(wash.rectTransform, Vector2.zero, Vector2.one);

            Image heroAmbient = Ui.MakeImage("HeroAmbient", fill.rectTransform, new Color(1f, 0.92f, 0.80f, 0.09f));
            heroAmbient.sprite = MenuArt.Glow();
            KidsScreenView.Place(heroAmbient.rectTransform, new Vector2(0.56f, 0.58f), new Vector2(0.56f, 0.58f), Vector2.zero, new Vector2(820f, 760f));
        }

        void BuildHeader()
        {
            RectTransform header = Ui.Panel("HeaderBar", SafeRoot, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            KidsScreenView.MakeBackButton(header, Gutter, HitSize, GoBack);

            Image slash = Ui.MakeImage("TitleSlash", header, Vermilion);
            KidsScreenView.Place(slash.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 2f, 0f), new Vector2(4f, 34f));
            slash.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text title = KidsScreenView.Label("Title", header, "CHOOSE YOUR KID", 44, Paper, TextAnchor.MiddleLeft, display: true);
            Ui.ClearOutline(title);
            KidsScreenView.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 14f, 0f), new Vector2(520f, 58f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            Image rule = Ui.MakeImage("HeaderRule", header, Hairline);
            KidsScreenView.Place(rule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-2f * Gutter, 1f));
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>The chosen kid in 3D, standing over an editorial smoked-obsidian nameplate with role glyph, purok, rival &amp; bold name.</summary>
        void BuildHero(RectTransform body)
        {
            RectTransform area = Ui.Panel("HeroArea", body, new Vector2(HeroAreaStart, 0f), new Vector2(HeroAreaEnd, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.offsetMin = new Vector2(0f, NamePlateHeight - 10f);

            Image spot = Ui.MakeImage("Spotlight", area, new Color(1.00f, 0.94f, 0.84f, 0.16f));
            spot.sprite = MenuArt.CardSpotlight();
            KidsScreenView.Stretch(spot.rectTransform, Vector2.zero, Vector2.one);

            RawImage hero = KidsScreenView.MakeRaw("Hero", area, _stage.Texture);
            hero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = hero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;
            _stage.AttachOrbitDrag(hero.rectTransform);

            RectTransform plate = Ui.Panel("NamePlate", body, new Vector2(NamePlateStart, 0f), new Vector2(NamePlateEnd, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, NamePlateHeight));
            Image plateBg = Ui.MakeImage("Glass", plate, ObsidianSolid);
            KidsScreenView.Stretch(plateBg.rectTransform, Vector2.zero, Vector2.one);
            KidsScreenView.ApplyGlassSurface(plateBg, ObsidianSolid, TitaniumBorderStrong, Vermilion, cornerCut: 6f, accentW: 3f);

            Image disc = Ui.MakeImage("RoleDisc", plate, Paper, round: true);
            KidsScreenView.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(36f, 36f));
            disc.rectTransform.pivot = new Vector2(0f, 0.5f);
            _roleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            KidsScreenView.Place(_roleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            _roleLabel = KidsScreenView.Label("Role", plate, "", 15, CoralGold, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_roleLabel);
            KidsScreenView.Stretch(_roleLabel.rectTransform, Vector2.zero, Vector2.one);
            _roleLabel.rectTransform.offsetMin = new Vector2(60f, 0f);
            _roleLabel.rectTransform.offsetMax = new Vector2(-10f, -10f);
            _roleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _roleLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _roleLabel.resizeTextForBestFit = true;
            _roleLabel.resizeTextMinSize = 9;
            _roleLabel.resizeTextMaxSize = 15;

            _nameLabel = KidsScreenView.Label("Name", plate, "", 40, Paper, TextAnchor.LowerLeft, display: true);
            Ui.ClearOutline(_nameLabel);
            KidsScreenView.Stretch(_nameLabel.rectTransform, Vector2.zero, Vector2.one);
            _nameLabel.rectTransform.offsetMin = new Vector2(60f, 6f);
            _nameLabel.rectTransform.offsetMax = new Vector2(-10f, -26f);
            _nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _nameLabel.resizeTextForBestFit = true;
            _nameLabel.resizeTextMinSize = 14;
            _nameLabel.resizeTextMaxSize = 40;
        }

        void BuildRoster(RectTransform body)
        {
            RectTransform panel = Ui.Panel("RosterPanel", body, new Vector2(0f, 0f), new Vector2(RosterEnd, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int r = 0; r < Roles.Length; r++)
            {
                float top = 1f - r / (float)Roles.Length, bottom = 1f - (r + 1f) / Roles.Length;
                RectTransform row = Ui.Panel("Row_" + Roles[r], panel, new Vector2(0f, bottom), new Vector2(1f, top), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                row.offsetMin = new Vector2(0f, RowGap * 0.5f);
                row.offsetMax = new Vector2(0f, -RowGap * 0.5f);

                RectTransform label = Ui.Panel("Role", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(RowLabelWidth, 0f));
                Image tagBg = Ui.MakeImage("TagBg", label, ObsidianGlass);
                KidsScreenView.Stretch(tagBg.rectTransform, Vector2.zero, Vector2.one);
                tagBg.rectTransform.offsetMin = new Vector2(0f, 2f);
                tagBg.rectTransform.offsetMax = new Vector2(-6f, -2f);
                KidsScreenView.ApplyGlassSurface(tagBg, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);

                Image disc = Ui.MakeImage("Disc", tagBg.rectTransform, RoleMedallionBg, round: true);
                KidsScreenView.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(36f, 36f));
                disc.rectTransform.pivot = new Vector2(0.5f, 1f);
                Ui.MakeSliced("Ring", disc.rectTransform, MenuArt.RoundOutline(Color.white, radius: 18, borderPx: 2), TitaniumBorder);
                Image icon = Ui.MakeImage("Icon", disc.rectTransform, Paper);
                icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(Roles[r]));
                KidsScreenView.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19f, 19f));

                Text word = KidsScreenView.Label("Word", tagBg.rectTransform, Roles[r], 16, Muted, TextAnchor.LowerCenter, display: true);
                Ui.ClearOutline(word);
                KidsScreenView.Place(word.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(-4f, 24f));
                word.rectTransform.pivot = new Vector2(0.5f, 0f);
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
                word.resizeTextForBestFit = true;
                word.resizeTextMinSize = 9;
                word.resizeTextMaxSize = 16;

                // exact 3 children for PickScreenTests
                RectTransform cards = Ui.Fill("Cards", row);
                cards.offsetMin = new Vector2(RowLabelWidth, 0f);
                int column = 0;
                foreach (string id in AppSession.DefaultRoster)
                {
                    if (RoleOf(id) != Roles[r]) continue;
                    _cards[id] = BuildCard(cards, id, column++);
                }
            }
        }

        Card BuildCard(RectTransform cards, string id, int column)
        {
            RectTransform rect = Ui.Panel("Kid_" + id, cards, new Vector2(column / (float)Columns, 0f), new Vector2((column + 1f) / Columns, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(CardGap * 0.5f, 0f);
            rect.offsetMax = new Vector2(-CardGap * 0.5f, 0f);

            Image fill = Ui.MakeImage("Fill", rect, ObsidianCard);
            KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.rectTransform.offsetMin = new Vector2(1f, 1f);
            fill.rectTransform.offsetMax = new Vector2(-1f, -1f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Image spotlight = Ui.MakeImage("Spotlight", fill.rectTransform, SpotNormal);
            spotlight.sprite = MenuArt.CardSpotlight();
            KidsScreenView.Stretch(spotlight.rectTransform, Vector2.zero, Vector2.one);

            Texture2D bustTex = KalyeahResources.KidBust(id);
            RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, bustTex);
            bust.uvRect = FullUv;
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f);
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = bustTex != null && bustTex.height > 0 ? (float)bustTex.width / bustTex.height : 0.78f;

            Image scrim = Ui.MakeImage("Fade", fill.rectTransform, Color.white);
            scrim.sprite = MenuArt.BottomScrim();
            KidsScreenView.Stretch(scrim.rectTransform, Vector2.zero, new Vector2(1f, 0.56f));

            Image nameBand = Ui.MakeImage("NameBand", fill.rectTransform, NameBandBg);
            KidsScreenView.Place(nameBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 28f));
            nameBand.rectTransform.pivot = new Vector2(0.5f, 0f);

            Image nameRule = Ui.MakeImage("NameRule", nameBand.rectTransform, Hairline);
            KidsScreenView.Place(nameRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1.5f));
            nameRule.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image frame = Ui.MakeSliced("Frame", rect, MenuArt.ChamferOutline(Color.white, cut: 8, borderPx: 2), TitaniumBorder, raycast: true);

            Image topAccent = Ui.MakeImage("TopAccent", rect, Vermilion);
            KidsScreenView.Place(topAccent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, 3f));
            topAccent.rectTransform.pivot = new Vector2(0.5f, 1f);

            Text name = KidsScreenView.Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 20, Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(name);
            KidsScreenView.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-10f, 26f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 9;
            name.resizeTextMaxSize = 20;

            // exact child name "Dim" asserted by PickScreenTests when teammate took this kid
            Image dim = Ui.MakeImage("Dim", rect, TakenScrim);
            KidsScreenView.Stretch(dim.rectTransform, Vector2.zero, Vector2.one);
            Image lockIcon = Ui.MakeImage("LockIcon", dim.rectTransform, CoralGold);
            lockIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Lock);
            KidsScreenView.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(20f, 20f));
            Text taken = KidsScreenView.Label("Taken", dim.rectTransform, "TAKEN", 16, Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(taken);
            KidsScreenView.Place(taken.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -12f), new Vector2(-8f, 22f));

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SelectCharacter(id));
            UiShaders.AttachPressSpring(b, 0.96f, 2f);

            return new Card
            {
                Frame = frame,
                TopAccent = topAccent,
                Spotlight = spotlight,
                NameRule = nameRule,
                TakenTag = dim.gameObject,
            };
        }

        /// <summary>
        /// The right column: three A2 Frosted-Titanium Skill Disc tiles with <see cref="MenuArt.SkillEmblem"/> icons
        /// (zero literal "S1"/"S2"/"ULT" text circles!) and a smoked-obsidian detail card with WATER &amp; COOLDOWN pills.
        /// </summary>
        void BuildDetail(RectTransform body)
        {
            RectTransform panel = Ui.Panel("DetailPanel", body, new Vector2(DetailStart, 0f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform tabs = Ui.Panel("SkillTabs", panel, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, SkillTabHeight));
            for (int i = 0; i < SkillCount; i++)
            {
                int index = i;
                RectTransform tab = Ui.Panel("Skill_" + i, tabs, new Vector2(i / (float)SkillCount, 0f), new Vector2((i + 1f) / SkillCount, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                tab.offsetMin = new Vector2(i == 0 ? 0f : SkillTabGap * 0.5f, 0f);
                tab.offsetMax = new Vector2(i == SkillCount - 1 ? 0f : -SkillTabGap * 0.5f, 0f);

                Image plate = Ui.MakeImage("Plate", tab, ObsidianCard, raycast: true);
                KidsScreenView.Stretch(plate.rectTransform, Vector2.zero, Vector2.one);
                KidsScreenView.ApplyGlassSurface(plate, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

                // A2 Frosted-Titanium Skill Disc + SkillEmblem icon
                RectTransform discHolder = Ui.Panel("Badge", tab, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(DiscSize, DiscSize));
                Image discFace = Ui.MakeImage("DiscFace", discHolder, Color.white);
                KidsScreenView.Stretch(discFace.rectTransform, Vector2.zero, Vector2.one);
                Material discMat = UiShaders.CreateFrostedTitaniumDisc(new Color(0.075f, 0.105f, 0.145f, 0.80f), MenuArt.TitaniumRim, innerRing: 0.78f);
                KidsScreenView.ApplyDiscMaterial(discFace, discMat, ObsidianSolid);

                Image emblem = Ui.MakeImage("Emblem", discHolder, Paper);
                emblem.preserveAspect = true;
                KidsScreenView.Stretch(emblem.rectTransform, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));

                Text caption = KidsScreenView.Label("Caption", tab, "", 13, MutedSoft, TextAnchor.LowerCenter, display: true);
                Ui.ClearOutline(caption);
                KidsScreenView.Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-6f, 19f));
                caption.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = plate;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectSkill(index));
                UiShaders.AttachPressSpring(b, 0.94f, 2f);

                _skillTabs.Add(new SkillTab
                {
                    Plate = plate,
                    DiscFace = discFace,
                    DiscMat = discMat,
                    Emblem = emblem,
                    Caption = caption,
                    Root = tab.gameObject,
                });
            }

            RectTransform card = Ui.Panel("SkillCard", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMax = new Vector2(0f, -(SkillTabHeight + 10f));
            _skillCardBg = Ui.MakeImage("Glass", card, ObsidianGlass);
            KidsScreenView.Stretch(_skillCardBg.rectTransform, Vector2.zero, Vector2.one);
            KidsScreenView.ApplyGlassSurface(_skillCardBg, ObsidianGlass, TitaniumBorderStrong, Paper, cornerCut: 8f, accentW: 3f);

            _skillKicker = KidsScreenView.Label("Kicker", card, "", 14, CoralGold, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_skillKicker);
            KidsScreenView.Place(_skillKicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -14f), new Vector2(-36f, 20f));
            _skillKicker.rectTransform.pivot = new Vector2(0f, 1f);

            _skillTitle = KidsScreenView.Label("Title", card, "", 32, Paper, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_skillTitle);
            KidsScreenView.Place(_skillTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -32f), new Vector2(-36f, 40f));
            _skillTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _skillTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillTitle.verticalOverflow = VerticalWrapMode.Truncate;
            _skillTitle.resizeTextForBestFit = true;
            _skillTitle.resizeTextMinSize = 14;
            _skillTitle.resizeTextMaxSize = 32;

            _skillChips = KidsScreenView.BuildSkillStatChips(card, topOffsetY: -78f, leftPad: 20f, rightPad: 16f, height: 30f, fontSize: 16);

            // keeps canonical raw cost string for PickScreenTests while UI renders separate WATER + COOLDOWN stat pills
            _skillCostRaw = KidsScreenView.Label("Cost", card, "", 16, MutedSoft, TextAnchor.UpperLeft);
            _skillCostRaw.gameObject.SetActive(false);

            Image divider = Ui.MakeImage("Divider", card, Hairline);
            KidsScreenView.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -116f), new Vector2(-36f, 1f));
            divider.rectTransform.pivot = new Vector2(0f, 1f);

            _skillBody = KidsScreenView.Label("Body", card, "", 20, Paper, TextAnchor.UpperLeft);
            Ui.ClearOutline(_skillBody);
            KidsScreenView.Stretch(_skillBody.rectTransform, Vector2.zero, Vector2.one);
            _skillBody.rectTransform.offsetMin = new Vector2(20f, 14f);
            _skillBody.rectTransform.offsetMax = new Vector2(-16f, -124f);
            _skillBody.lineSpacing = 1.16f;
            _skillBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillBody.verticalOverflow = VerticalWrapMode.Truncate;
            _skillBody.resizeTextForBestFit = true;
            _skillBody.resizeTextMinSize = 12;
            _skillBody.resizeTextMaxSize = 20;
        }

        void BuildTeamBar()
        {
            RectTransform bar = Ui.Panel("TeamBar", SafeRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, GutterBottom), new Vector2(-2f * Gutter, TeamBarHeight));

            RectTransform dock = Ui.Panel("SquadDock", bar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dock.offsetMin = new Vector2(0f, 10f);
            dock.offsetMax = new Vector2(-(LockWidth + 16f), -10f);
            Image dockBg = Ui.MakeImage("Glass", dock, ObsidianGlass);
            KidsScreenView.Stretch(dockBg.rectTransform, Vector2.zero, Vector2.one);
            KidsScreenView.ApplyGlassSurface(dockBg, ObsidianGlass, TitaniumBorderStrong, Paper, cornerCut: 6f, accentW: 3f);

            const float slotsStartX = 16f;
            for (int i = 0; i < TeamSize; i++)
            {
                float x = slotsStartX + i * (SlotSize + SlotGap);

                Image ring = Ui.MakeImage("Slot_" + i, dock, TitaniumBorder, round: true);
                KidsScreenView.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));
                ring.rectTransform.pivot = new Vector2(0f, 0.5f);

                Image fill = Ui.MakeImage("Fill", ring.rectTransform, ObsidianSolid, round: true);
                KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
                fill.rectTransform.offsetMin = new Vector2(2.5f, 2.5f);
                fill.rectTransform.offsetMax = new Vector2(-2.5f, -2.5f);
                fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                Image spot = Ui.MakeImage("Spot", fill.rectTransform, i == 0 ? SpotActive : SpotNormal);
                spot.sprite = MenuArt.CardSpotlight();
                KidsScreenView.Stretch(spot.rectTransform, Vector2.zero, Vector2.one);

                Image emptyIcon = Ui.MakeImage("Empty", fill.rectTransform, new Color(Paper.r, Paper.g, Paper.b, 0.22f));
                emptyIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Kid);
                KidsScreenView.Place(emptyIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

                RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, null);
                bust.uvRect = FullUv;
                bust.rectTransform.pivot = new Vector2(0.5f, 1f);
                var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = 0.78f;

                if (i == 0)
                {
                    RectTransform youPill = Ui.Panel("YouTag", ring.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(36f, 16f));
                    Image youBg = Ui.MakeImage("Plate", youPill, Paper);
                    KidsScreenView.Stretch(youBg.rectTransform, Vector2.zero, Vector2.one);
                    Text youText = KidsScreenView.Label("Label", youPill, "YOU", 11, Ink, TextAnchor.MiddleCenter, display: true);
                    Ui.ClearOutline(youText);
                    KidsScreenView.Stretch(youText.rectTransform, Vector2.zero, Vector2.one);
                }

                _slots.Add(new Slot { Ring = ring, Bust = bust, EmptyIcon = emptyIcon.gameObject });
            }

            float dividerX = slotsStartX + TeamSize * (SlotSize + SlotGap) + 4f;
            Image sep = Ui.MakeImage("SquadDivider", dock, Hairline);
            KidsScreenView.Place(sep.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(dividerX, 0f), new Vector2(1f, -24f));
            sep.rectTransform.pivot = new Vector2(0f, 0.5f);

            RectTransform openPanel = Ui.Panel("OpenRolesPanel", dock, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            openPanel.offsetMin = new Vector2(dividerX + 12f, 0f);
            openPanel.offsetMax = new Vector2(-12f, 0f);

            _openRoles = KidsScreenView.Label("OpenRoles", openPanel, "", 16, MutedSoft, TextAnchor.UpperLeft, display: true);
            Ui.ClearOutline(_openRoles);
            _openRolesRect = _openRoles.rectTransform;
            KidsScreenView.Stretch(_openRolesRect, Vector2.zero, Vector2.one);
            _openRolesRect.offsetMin = new Vector2(0f, 38f);
            _openRolesRect.offsetMax = new Vector2(0f, -8f);

            RectTransform chipRow = Ui.Panel("OpenRoleChips", openPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(0f, 28f));
            for (int i = 0; i < MaxOpenRoleChips; i++)
            {
                RectTransform chip = Ui.Panel("OpenChip_" + i, chipRow, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(i * (OpenChipWidth + OpenChipGap), 0f), new Vector2(OpenChipWidth, 0f));
                Image chipBg = Ui.MakeImage("Bg", chip, ObsidianSolid);
                KidsScreenView.Stretch(chipBg.rectTransform, Vector2.zero, Vector2.one);
                KidsScreenView.ApplyGlassSurface(chipBg, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);
                Image outline = Ui.MakeSliced("Outline", chip, MenuArt.ChamferOutline(Color.white, cut: 4, borderPx: 1), TitaniumBorder);
                Image icon = Ui.MakeImage("Icon", chip, Paper);
                KidsScreenView.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(14f, 14f));
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);

                Text lbl = KidsScreenView.Label("Label", chip, "", 13, Paper, TextAnchor.MiddleLeft, display: true);
                Ui.ClearOutline(lbl);
                KidsScreenView.Stretch(lbl.rectTransform, Vector2.zero, Vector2.one);
                lbl.rectTransform.offsetMin = new Vector2(23f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-4f, 0f);
                lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                lbl.verticalOverflow = VerticalWrapMode.Truncate;
                lbl.resizeTextForBestFit = true;
                lbl.resizeTextMinSize = 8;
                lbl.resizeTextMaxSize = 13;

                _openRoleChips.Add(new OpenRoleChip { Root = chip.gameObject, Outline = outline, Icon = icon, Label = lbl });
            }

            // Signal Vermilion LOCK IN CTA (CreateMetallicCta + AttachPressSpring)
            RectTransform lockRect = Ui.Panel("LockIn", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(LockWidth, TeamBarHeight));
            Image lockHit = lockRect.gameObject.AddComponent<Image>();
            lockHit.color = Clear;

            _lockPlate = Ui.MakeImage("Plate", lockRect, Vermilion);
            KidsScreenView.Stretch(_lockPlate.rectTransform, Vector2.zero, Vector2.one);
            _lockPlate.rectTransform.offsetMin = new Vector2(0f, 12f);
            _lockPlate.rectTransform.offsetMax = new Vector2(0f, -12f);
            _lockCtaMat = UiShaders.CreateMetallicCta(Vermilion, VermilionDeep, cornerCut: 10f);

            _lockIcon = Ui.MakeImage("LockIcon", _lockPlate.rectTransform, MutedSoft);
            _lockIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Lock);
            KidsScreenView.Place(_lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-52f, 0f), new Vector2(20f, 20f));

            _lockLabel = KidsScreenView.Label("Label", _lockPlate.rectTransform, "LOCK IN", 32, Paper, TextAnchor.MiddleCenter, display: true);
            Ui.ClearOutline(_lockLabel);
            KidsScreenView.Stretch(_lockLabel.rectTransform, Vector2.zero, Vector2.one);

            _lockButton = lockRect.gameObject.AddComponent<Button>();
            _lockButton.targetGraphic = _lockPlate;
            _lockButton.transition = Selectable.Transition.None;
            _lockButton.onClick.AddListener(ConfirmSelection);
            UiShaders.AttachPressSpring(_lockButton, 0.94f, 3f);
        }

        // ---------------------------------------------------------------- state

        /// <summary>Takes the saved kid from the session (the one the player last used) and redraws.</summary>
        public void RefreshFromSession()
        {
            string id = AppSession.Current.SelectedCharacter;
            _selected = Array.IndexOf(AppSession.DefaultRoster, id) >= 0 ? id : AppSession.DefaultCharacter;
            _skill = 0;
            Refresh();
        }

        /// <summary>The kids teammates already hold (greyed, cannot be locked); they also show in the team bar.</summary>
        public void SetTakenCharacters(IEnumerable<string> characterIds)
        {
            _taken.Clear();
            _takenOrder.Clear();
            if (characterIds != null)
                foreach (string id in characterIds)
                    if (Array.IndexOf(AppSession.DefaultRoster, id) >= 0 && _taken.Add(id)) _takenOrder.Add(id);
            Refresh();
        }

        public bool IsCharacterTaken(string id) => _taken.Contains(id);

        /// <summary>The roles no kid on the team (yours and the ones teammates took) covers yet, in row order.</summary>
        public List<string> MissingRoles()
        {
            var have = new HashSet<string>(StringComparer.Ordinal) { RoleOf(_selected) };
            foreach (string id in _taken) have.Add(RoleOf(id));
            var missing = new List<string>(Roles.Length);
            foreach (string role in Roles)
                if (!have.Contains(role)) missing.Add(role);
            return missing;
        }

        /// <summary>Previews a kid: the 3D kid, role, name and the three skills. Returns false for an unknown kid.</summary>
        public bool SelectCharacter(string id)
        {
            if (Array.IndexOf(AppSession.DefaultRoster, id) < 0) return false;
            _selected = id;
            _skill = 0;
            _stage.ResetOrbit();
            Haptics.LightTap();
            Audio?.PlayUiTap("select");
            Refresh();
            CharacterSelected?.Invoke(id);
            return true;
        }

        /// <summary>Shows one of the kid's three skills (0 and 1, then the ultimate) in the description card.</summary>
        public void SelectSkill(int index)
        {
            _skill = Mathf.Clamp(index, 0, SkillCount - 1);
            Haptics.LightTap();
            Refresh();
        }

        void Refresh()
        {
            if (_disposed) return;
            foreach (KeyValuePair<string, Card> pair in _cards)
            {
                bool selected = pair.Key == _selected;
                pair.Value.Frame.color = selected ? Paper : TitaniumBorder;
                pair.Value.TopAccent.gameObject.SetActive(selected);
                pair.Value.Spotlight.color = selected ? SpotActive : SpotNormal;
                pair.Value.NameRule.color = selected ? Vermilion : Hairline;
                pair.Value.TakenTag.SetActive(_taken.Contains(pair.Key));
            }

            string role = RoleOf(_selected);
            string description = Field(_selected, "description") ?? "";
            string purok = KidsScreenView.Purok(description);
            if (string.IsNullOrEmpty(purok) && LoreCodexData.TryGetKid(_selected, out KidLoreEntry lore))
                purok = LoreCodexData.PurokName(lore.Purok);

            string rivalId = LoreCodexData.GetRivalId(_selected);
            string rivalPart = "";
            if (!string.IsNullOrEmpty(rivalId))
            {
                string rName = LoreCodexData.TryGetKid(rivalId, out KidLoreEntry rLore) ? rLore.DisplayName : rivalId;
                rivalPart = "  ·  VS " + rName.ToUpperInvariant();
            }

            _roleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _roleLabel.text = purok.ToUpperInvariant() + rivalPart;
            _nameLabel.text = (Field(_selected, "display_name") ?? _selected).ToUpperInvariant();

            KidsScreenView.SkillData[] skills = KidsScreenView.ResolveSkillsFor(_data, _selected);
            for (int i = 0; i < _skillTabs.Count; i++)
            {
                KidsScreenView.SkillData skill = skills != null && i < skills.Length ? skills[i] : null;
                SkillTab tab = _skillTabs[i];
                tab.Root.SetActive(skill != null);
                if (skill == null) continue;
                bool ultimate = skill.slot == "ULT";
                bool on = i == _skill;

                KidsScreenView.ApplyGlassSurface(
                    tab.Plate,
                    on ? ObsidianSolid : ObsidianCard,
                    on ? (ultimate ? Vermilion : Paper) : TitaniumBorder,
                    on ? (ultimate ? Vermilion : Paper) : Color.clear,
                    cornerCut: 6f,
                    accentW: on ? 3f : 0f);

                if (tab.DiscMat != null && tab.DiscMat != Graphic.defaultGraphicMaterial)
                {
                    Color rimColor = on
                        ? (ultimate ? Vermilion : Paper)
                        : (ultimate ? CoralGold : MenuArt.TitaniumRim);
                    if (tab.DiscMat.HasProperty(UiShaders.PropRimColor)) tab.DiscMat.SetColor(UiShaders.PropRimColor, rimColor);
                    if (tab.DiscMat.HasProperty(UiShaders.PropRimGlow)) tab.DiscMat.SetFloat(UiShaders.PropRimGlow, on ? 0.90f : 0.25f);
                    if (tab.DiscMat.HasProperty(UiShaders.PropUltPulse)) tab.DiscMat.SetFloat(UiShaders.PropUltPulse, ultimate ? (on ? 0.85f : 0.35f) : 0f);
                }

                tab.Emblem.sprite = MenuArt.SkillEmblem(skill.id);
                tab.Emblem.color = on ? Paper : new Color(Paper.r, Paper.g, Paper.b, 0.86f);
                tab.Caption.text = KidsScreenView.SlotCaption(skill.slot);
                KidsScreenView.SetLabelColor(tab.Caption, on ? Paper : (ultimate ? CoralGold : MutedSoft));

                if (on)
                {
                    KidsScreenView.ApplyGlassSurface(
                        _skillCardBg,
                        ObsidianGlass,
                        ultimate ? Vermilion : TitaniumBorderStrong,
                        ultimate ? Vermilion : Paper,
                        cornerCut: 8f,
                        accentW: 3f);
                    _skillKicker.text = ultimate ? "ULTIMATE ABILITY" : KidsScreenView.SlotCaption(skill.slot);
                    KidsScreenView.SetLabelColor(_skillKicker, ultimate ? Vermilion : CoralGold);
                    _skillTitle.text = skill.name;
                    _skillChips.Apply(skill.cost);
                    _skillCostRaw.text = skill.cost;
                    _skillBody.text = skill.what;
                }
            }
            _stage.SetKid(_selected);
            _stage.RenderNow();

            RefreshTeamBar();
            bool can = CanConfirm;
            bool taken = _taken.Contains(_selected);
            if (can)
                KidsScreenView.ApplyCtaSurface(_lockPlate, _lockCtaMat, Vermilion);
            else
                KidsScreenView.ApplyGlassSurface(_lockPlate, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);
            _lockIcon.gameObject.SetActive(taken);
            _lockLabel.text = taken ? "TAKEN" : "LOCK IN";
            _lockLabel.rectTransform.offsetMin = new Vector2(taken ? 22f : 0f, can ? 2f : 0f);
            KidsScreenView.SetLabelColor(_lockLabel, can ? Paper : MutedSoft);
            _lockButton.interactable = can;
        }

        void RefreshTeamBar()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                string id = i == 0 ? _selected : i - 1 < _takenOrder.Count ? _takenOrder[i - 1] : null;
                Texture2D tex = id != null ? KalyeahResources.KidBust(id) : null;
                _slots[i].Bust.texture = tex;
                _slots[i].Bust.uvRect = FullUv;
                _slots[i].Bust.enabled = tex != null;
                _slots[i].EmptyIcon.SetActive(tex == null);
                _slots[i].Ring.color = i == 0 ? Vermilion : id != null ? Paper : TitaniumBorder;
            }

            List<string> missing = MissingRoles();
            bool full = _takenOrder.Count + 1 >= TeamSize;
            if (missing.Count == 0)
            {
                _openRoles.text = "Every role is covered";
                _openRoles.alignment = TextAnchor.MiddleLeft;
                _openRolesRect.offsetMin = Vector2.zero;
                _openRolesRect.offsetMax = Vector2.zero;
                KidsScreenView.SetLabelColor(_openRoles, MenuArt.SuccessGreen);
            }
            else
            {
                _openRoles.alignment = TextAnchor.UpperLeft;
                _openRolesRect.offsetMin = new Vector2(0f, 38f);
                _openRolesRect.offsetMax = new Vector2(0f, -8f);
                if (full)
                {
                    var pretty = new string[missing.Count];
                    for (int i = 0; i < missing.Count; i++)
                        pretty[i] = missing[i].Substring(0, 1) + missing[i].Substring(1).ToLowerInvariant();
                    _openRoles.text = "No " + string.Join(", ", pretty);
                    KidsScreenView.SetLabelColor(_openRoles, WarnAmber);
                }
                else
                {
                    _openRoles.text = "Still open";
                    KidsScreenView.SetLabelColor(_openRoles, MutedSoft);
                }
            }

            for (int i = 0; i < _openRoleChips.Count; i++)
            {
                bool active = i < missing.Count;
                _openRoleChips[i].Root.SetActive(active);
                if (!active) continue;
                string role = missing[i];
                _openRoleChips[i].Icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
                _openRoleChips[i].Icon.color = full ? WarnAmber : Paper;
                _openRoleChips[i].Outline.color = full ? new Color(WarnAmber.r, WarnAmber.g, WarnAmber.b, 0.55f) : TitaniumBorder;
                _openRoleChips[i].Label.text = role;
                KidsScreenView.SetLabelColor(_openRoleChips[i].Label, full ? WarnAmber : Paper);
            }
        }

        string RoleOf(string id) => HomeScreenView.ParseRole(Field(id, "description"));

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

        // ---------------------------------------------------------------- flow

        /// <summary>Locks the previewed kid in as the player's kid and leaves the screen; refused when a teammate took it.</summary>
        public void ConfirmSelection()
        {
            if (!CanConfirm)
            {
                Haptics.CancelTick();
                Audio?.PlayUiTap("deny");
                return;
            }
            AppSession.Current.SelectedCharacter = _selected;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            Haptics.LightTap();
            Audio?.PlayUiTap("confirm");
            Confirmed?.Invoke();
            _onConfirmed?.Invoke();
        }

        public void GoBack()
        {
            Haptics.LightTap();
            Audio?.PlayUiTap("back");
            BackRequested?.Invoke();
            _onBack?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            if (Root == null) return;
            Root.gameObject.SetActive(visible);
            if (visible) RefreshFromSession();
        }

        void Tick(float dt)
        {
            if (_disposed || !Application.isPlaying) return;
            _stage.Tick(dt);
        }

        static bool IsOwnedMaterial(Material mat) =>
            mat != null && mat != Graphic.defaultGraphicMaterial && mat != Canvas.GetDefaultCanvasMaterial();

        void ReleaseOwnedMaterials()
        {
            for (int i = 0; i < _skillTabs.Count; i++)
            {
                if (IsOwnedMaterial(_skillTabs[i].DiscMat))
                    KalyeahResources.Kill(_skillTabs[i].DiscMat);
                _skillTabs[i].DiscMat = null;
            }
            if (IsOwnedMaterial(_lockCtaMat))
                KalyeahResources.Kill(_lockCtaMat);
            _lockCtaMat = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseOwnedMaterials();
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }
    }
}
