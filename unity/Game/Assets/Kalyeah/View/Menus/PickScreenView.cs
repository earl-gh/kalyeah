using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kid select, after the queue (docs/SPEC.md §4 and §8; Valorant × Wild Rift tactical craft, zero role colours per §10):
    /// the 12 kids in four role rows on the left, the chosen kid in 3D on a tactical nameplate in the middle, and on the right
    /// their three skills (S1/S2/ULT) with separate WATER and COOLDOWN stat chips and description; a grounded squad dock at the
    /// foot shows your kid, teammates' picks and structured open-role chips beside a beveled LOCK IN CTA.
    /// Every control stays >= 48 dp with zero overlaps across 1280x720, 844x390, 934x400 and 1024x768.
    /// </summary>
    public sealed class PickScreenView : IDisposable
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
        static readonly Color SkillBadge = new Color(0.16f, 0.23f, 0.35f, 1f);
        static readonly Color SkillActiveTop = new Color(0.145f, 0.205f, 0.305f, 0.96f);
        static readonly Color SkillActiveBottom = new Color(0.055f, 0.092f, 0.162f, 0.98f);
        static readonly Color UltCardBorder = new Color(0.961f, 0.725f, 0.227f, 0.45f);
        static readonly Color TakenScrim = new Color(0.020f, 0.034f, 0.062f, 0.82f);
        static readonly Color WarnAmber = new Color(1f, 0.78f, 0.42f, 1f);
        static readonly Color RoleMedallionBg = new Color(0.035f, 0.062f, 0.112f, 0.88f);
        static readonly Color LockDisabledTop = new Color(0.095f, 0.138f, 0.210f, 0.94f);
        static readonly Color LockDisabledBottom = new Color(0.045f, 0.072f, 0.125f, 0.96f);

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
        const float BadgeSize = 46f;
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
        }

        sealed class Card
        {
            public Image Frame;
            public Image Brackets;
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
            public Image Brackets;
            public Image Badge;
            public Image BadgeRing;
            public Text BadgeText;
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
        UiSheen _lockSheen;

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
                raw.color = new Color(1f, 1f, 1f, 0.28f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 16f / 9f;
            }
            Image wash = Ui.MakeImage("Wash", fill.rectTransform, new Color(Shade.r, Shade.g, Shade.b, 0.72f));
            KidsScreenView.Stretch(wash.rectTransform, Vector2.zero, Vector2.one);

            Image heroAmbient = Ui.MakeImage("HeroAmbient", fill.rectTransform, new Color(1f, 0.84f, 0.54f, 0.12f));
            heroAmbient.sprite = MenuArt.Glow();
            KidsScreenView.Place(heroAmbient.rectTransform, new Vector2(0.56f, 0.58f), new Vector2(0.56f, 0.58f), Vector2.zero, new Vector2(820f, 760f));

            Image coolAmbient = Ui.MakeImage("CoolAmbient", fill.rectTransform, new Color(0.28f, 0.74f, 0.88f, 0.08f));
            coolAmbient.sprite = MenuArt.Glow();
            KidsScreenView.Place(coolAmbient.rectTransform, new Vector2(0.84f, 0.24f), new Vector2(0.84f, 0.24f), Vector2.zero, new Vector2(860f, 620f));
        }

        void BuildHeader()
        {
            RectTransform header = Ui.Panel("HeaderBar", SafeRoot, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            KidsScreenView.MakeBackButton(header, Gutter, HitSize, GoBack);

            Text title = KidsScreenView.Label("Title", header, "CHOOSE YOUR KID", 46, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            KidsScreenView.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 4f, 0f), new Vector2(520f, 60f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            Image rule = Ui.MakeImage("HeaderRule", header, new Color(1f, 1f, 1f, 0.08f));
            KidsScreenView.Place(rule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-2f * Gutter, 1.5f));
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>The chosen kid in 3D, standing over a spotlighted tactical nameplate with role medallion, purok and bold name.</summary>
        void BuildHero(RectTransform body)
        {
            RectTransform area = Ui.Panel("HeroArea", body, new Vector2(HeroAreaStart, 0f), new Vector2(HeroAreaEnd, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.offsetMin = new Vector2(0f, NamePlateHeight - 10f); // the kid stands cleanly above the nameplate

            Image spot = Ui.MakeImage("Spotlight", area, new Color(1.00f, 0.86f, 0.56f, 0.20f));
            spot.sprite = MenuArt.CardSpotlight();
            KidsScreenView.Stretch(spot.rectTransform, Vector2.zero, Vector2.one);

            Image rim = Ui.MakeImage("RimGlow", area, new Color(0.32f, 0.82f, 0.94f, 0.12f));
            rim.sprite = MenuArt.Glow();
            KidsScreenView.Place(rim.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(360f, 360f));

            RawImage hero = KidsScreenView.MakeRaw("Hero", area, _stage.Texture);
            hero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = hero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;
            _stage.AttachOrbitDrag(hero.rectTransform);

            RectTransform plate = Ui.Panel("NamePlate", body, new Vector2(NamePlateStart, 0f), new Vector2(NamePlateEnd, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, NamePlateHeight));
            Ui.MakeSliced("Glass", plate, MenuArt.ChamferGlass(12), Color.white);
            Ui.MakeAccentRail("Accent", plate, Gold, width: 4f, verticalInset: 10f);

            Image disc = Ui.MakeImage("RoleDisc", plate, Gold, round: true);
            KidsScreenView.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(38f, 38f));
            disc.rectTransform.pivot = new Vector2(0f, 0.5f);
            _roleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            KidsScreenView.Place(_roleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

            _roleLabel = KidsScreenView.Label("Role", plate, "", 18, GoldLight, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Stretch(_roleLabel.rectTransform, Vector2.zero, Vector2.one);
            _roleLabel.rectTransform.offsetMin = new Vector2(64f, 0f);
            _roleLabel.rectTransform.offsetMax = new Vector2(-10f, -10f);

            _nameLabel = KidsScreenView.Label("Name", plate, "", 42, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            KidsScreenView.Stretch(_nameLabel.rectTransform, Vector2.zero, Vector2.one);
            _nameLabel.rectTransform.offsetMin = new Vector2(64f, 6f);
            _nameLabel.rectTransform.offsetMax = new Vector2(-10f, -28f);
            _nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap; // best fit only shrinks text constrained to its box
            _nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _nameLabel.resizeTextForBestFit = true;
            _nameLabel.resizeTextMinSize = 14;
            _nameLabel.resizeTextMaxSize = 42;
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
                Image tagBg = Ui.MakeSliced("TagBg", label, MenuArt.StatChip(8), Color.white);
                tagBg.rectTransform.offsetMin = new Vector2(0f, 2f);
                tagBg.rectTransform.offsetMax = new Vector2(-6f, -2f);

                Image disc = Ui.MakeImage("Disc", tagBg.rectTransform, RoleMedallionBg, round: true);
                KidsScreenView.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(38f, 38f));
                disc.rectTransform.pivot = new Vector2(0.5f, 1f);
                Ui.MakeSliced("Ring", disc.rectTransform, MenuArt.RoundOutline(Color.white, radius: 19, borderPx: 2), PanelBorder);
                Image icon = Ui.MakeImage("Icon", disc.rectTransform, Cream);
                icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(Roles[r]));
                KidsScreenView.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

                Text word = KidsScreenView.Label("Word", tagBg.rectTransform, Roles[r], 17, Muted, TextAnchor.LowerCenter, display: true);
                KidsScreenView.Place(word.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(-4f, 26f));
                word.rectTransform.pivot = new Vector2(0.5f, 0f);
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
                word.resizeTextForBestFit = true;
                word.resizeTextMinSize = 10;
                word.resizeTextMaxSize = 17;

                // the three cards share what is left of the row after the role label (exact 3 children for PickScreenTests)
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

            Image fill = Ui.MakeSliced("Fill", rect, MenuArt.ChamferCard(CardTop, CardBottom, Clear, cut: 10, borderPx: 0, shine: 0f), Color.white);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Image spotlight = Ui.MakeImage("Spotlight", fill.rectTransform, SpotNormal);
            spotlight.sprite = MenuArt.CardSpotlight();
            KidsScreenView.Stretch(spotlight.rectTransform, Vector2.zero, Vector2.one);

            RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, KalyeahResources.Texture("portraits/busts/" + id));
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f); // cropped from the bottom so the head stays framed
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 0.75f;

            Image scrim = Ui.MakeImage("Fade", fill.rectTransform, Color.white);
            scrim.sprite = MenuArt.BottomScrim();
            KidsScreenView.Stretch(scrim.rectTransform, Vector2.zero, new Vector2(1f, 0.56f));

            Image nameBand = Ui.MakeImage("NameBand", fill.rectTransform, NameBandBg);
            KidsScreenView.Place(nameBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 30f));
            nameBand.rectTransform.pivot = new Vector2(0.5f, 0f);

            Image nameRule = Ui.MakeImage("NameRule", nameBand.rectTransform, Hairline);
            KidsScreenView.Place(nameRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1.5f));
            nameRule.rectTransform.pivot = new Vector2(0.5f, 1f);

            Image frame = Ui.MakeSliced("Frame", rect, MenuArt.ChamferOutline(Color.white, cut: 10, borderPx: 2), PanelBorder, raycast: true);
            Image brackets = Ui.MakeCornerBrackets("Brackets", rect, GoldLight, expand: 2f);

            Text name = KidsScreenView.Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 22, Cream, TextAnchor.MiddleCenter, display: true, ink: true);
            KidsScreenView.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-10f, 28f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 10;
            name.resizeTextMaxSize = 22;

            // exact child name "Dim" asserted by PickScreenTests when teammate took this kid
            Image dim = Ui.MakeSliced("Dim", rect, MenuArt.ChamferCard(TakenScrim, TakenScrim, new Color(1f, 1f, 1f, 0.16f), cut: 10, borderPx: 1, shine: 0f), Color.white);
            Image lockIcon = Ui.MakeImage("LockIcon", dim.rectTransform, Gold);
            lockIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Lock);
            KidsScreenView.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(22f, 22f));
            Text taken = KidsScreenView.Label("Taken", dim.rectTransform, "TAKEN", 18, GoldLight, TextAnchor.MiddleCenter, display: true);
            KidsScreenView.Place(taken.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -12f), new Vector2(-8f, 24f));

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SelectCharacter(id));
            Ui.AddPressFeel(rect, 0.96f);

            return new Card
            {
                Frame = frame,
                Brackets = brackets,
                Spotlight = spotlight,
                NameRule = nameRule,
                TakenTag = dim.gameObject,
            };
        }

        /// <summary>
        /// The right column: three tactical S1/S2/ULT selector tiles and a chamfered detail card with separate WATER and COOLDOWN
        /// stat chips and clean typography that fits the longest description on every screen shape.
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

                Image plate = Ui.MakeSliced("Plate", tab, MenuArt.ChamferGlass(10), Color.white, raycast: true);
                Image brackets = Ui.MakeCornerBrackets("Brackets", tab, GoldLight);

                Image badge = Ui.MakeImage("Badge", tab, SkillBadge, round: true);
                KidsScreenView.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(BadgeSize, BadgeSize));
                Image badgeRing = Ui.MakeSliced("Ring", badge.rectTransform, MenuArt.RoundOutline(Color.white, radius: 23, borderPx: 2), PanelBorder);
                Text badgeText = KidsScreenView.Label("Text", badge.rectTransform, "", 22, Gold, TextAnchor.MiddleCenter, display: true);
                KidsScreenView.Stretch(badgeText.rectTransform, Vector2.zero, Vector2.one);

                Text caption = KidsScreenView.Label("Caption", tab, "", 14, MutedSoft, TextAnchor.LowerCenter, display: true);
                KidsScreenView.Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-6f, 20f));
                caption.rectTransform.pivot = new Vector2(0.5f, 0f);

                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = plate;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectSkill(index));
                Ui.AddPressFeel(tab, 0.95f);

                _skillTabs.Add(new SkillTab
                {
                    Plate = plate,
                    Brackets = brackets,
                    Badge = badge,
                    BadgeRing = badgeRing,
                    BadgeText = badgeText,
                    Caption = caption,
                    Root = tab.gameObject,
                });
            }

            RectTransform card = Ui.Panel("SkillCard", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMax = new Vector2(0f, -(SkillTabHeight + 10f));
            _skillCardBg = Ui.MakeSliced("Glass", card, MenuArt.ChamferGlass(14), Color.white);
            Ui.MakeAccentRail("Accent", card, Gold, width: 4f, verticalInset: 14f);

            _skillKicker = KidsScreenView.Label("Kicker", card, "", 15, Gold, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Place(_skillKicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -14f), new Vector2(-36f, 20f));
            _skillKicker.rectTransform.pivot = new Vector2(0f, 1f);

            _skillTitle = KidsScreenView.Label("Title", card, "", 34, Cream, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Place(_skillTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -32f), new Vector2(-36f, 40f));
            _skillTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _skillTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillTitle.verticalOverflow = VerticalWrapMode.Truncate;
            _skillTitle.resizeTextForBestFit = true;
            _skillTitle.resizeTextMinSize = 14;
            _skillTitle.resizeTextMaxSize = 34;

            _skillChips = KidsScreenView.BuildSkillStatChips(card, topOffsetY: -80f, leftPad: 20f, rightPad: 16f, height: 30f, fontSize: 17);

            // keeps canonical raw cost string for PickScreenTests while UI renders separate WATER + COOLDOWN stat chips
            _skillCostRaw = KidsScreenView.Label("Cost", card, "", 16, MutedSoft, TextAnchor.UpperLeft);
            _skillCostRaw.gameObject.SetActive(false);

            Image divider = Ui.MakeImage("Divider", card, Hairline);
            KidsScreenView.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -118f), new Vector2(-36f, 1.5f));
            divider.rectTransform.pivot = new Vector2(0f, 1f);

            _skillBody = KidsScreenView.Label("Body", card, "", 21, Cream, TextAnchor.UpperLeft);
            KidsScreenView.Stretch(_skillBody.rectTransform, Vector2.zero, Vector2.one);
            _skillBody.rectTransform.offsetMin = new Vector2(20f, 14f);
            _skillBody.rectTransform.offsetMax = new Vector2(-16f, -126f);
            _skillBody.lineSpacing = 1.16f;
            _skillBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillBody.verticalOverflow = VerticalWrapMode.Truncate;
            _skillBody.resizeTextForBestFit = true;
            _skillBody.resizeTextMinSize = 14;
            _skillBody.resizeTextMaxSize = 21;
        }

        void BuildTeamBar()
        {
            RectTransform bar = Ui.Panel("TeamBar", SafeRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, GutterBottom), new Vector2(-2f * Gutter, TeamBarHeight));

            // grounded tactical squad dock holding the 5 player slots and structured open-role chips
            RectTransform dock = Ui.Panel("SquadDock", bar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dock.offsetMin = new Vector2(0f, 10f);
            dock.offsetMax = new Vector2(-(LockWidth + 16f), -10f);
            Ui.MakeSliced("Glass", dock, MenuArt.ChamferGlass(12), Color.white);
            Ui.MakeAccentRail("Accent", dock, Gold, width: 4f, verticalInset: 10f);

            const float slotsStartX = 16f;
            for (int i = 0; i < TeamSize; i++)
            {
                float x = slotsStartX + i * (SlotSize + SlotGap);
                if (i == 0)
                {
                    Image glow = Ui.MakeImage("LocalGlow", dock, new Color(Gold.r, Gold.g, Gold.b, 0.42f));
                    glow.sprite = MenuArt.Glow();
                    KidsScreenView.Place(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + SlotSize * 0.5f, 0f), new Vector2(SlotSize + 20f, SlotSize + 20f));
                }

                Image ring = Ui.MakeImage("Slot_" + i, dock, PanelBorder, round: true);
                KidsScreenView.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));
                ring.rectTransform.pivot = new Vector2(0f, 0.5f);

                Image fill = Ui.MakeImage("Fill", ring.rectTransform, CardBottom, round: true);
                KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
                fill.rectTransform.offsetMin = new Vector2(3f, 3f);
                fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
                fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                Image spot = Ui.MakeImage("Spot", fill.rectTransform, i == 0 ? SpotActive : SpotNormal);
                spot.sprite = MenuArt.CardSpotlight();
                KidsScreenView.Stretch(spot.rectTransform, Vector2.zero, Vector2.one);

                Image emptyIcon = Ui.MakeImage("Empty", fill.rectTransform, new Color(Cream.r, Cream.g, Cream.b, 0.22f));
                emptyIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Kid);
                KidsScreenView.Place(emptyIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

                RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, null);
                bust.rectTransform.pivot = new Vector2(0.5f, 1f);
                var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = 0.75f;

                if (i == 0)
                {
                    RectTransform youPill = Ui.Panel("YouTag", ring.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(36f, 16f));
                    Ui.MakeSliced("Plate", youPill, MenuArt.RoundSolid(6), Gold);
                    Text youText = KidsScreenView.Label("Label", youPill, "YOU", 11, Ink, TextAnchor.MiddleCenter, display: true);
                    KidsScreenView.Stretch(youText.rectTransform, Vector2.zero, Vector2.one);
                }

                _slots.Add(new Slot { Ring = ring, Bust = bust, EmptyIcon = emptyIcon.gameObject });
            }

            float dividerX = slotsStartX + TeamSize * (SlotSize + SlotGap) + 4f;
            Image sep = Ui.MakeImage("SquadDivider", dock, Hairline);
            KidsScreenView.Place(sep.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(dividerX, 0f), new Vector2(1.5f, -24f));
            sep.rectTransform.pivot = new Vector2(0f, 0.5f);

            // structured open-role status + role chips (fits cleanly inside 286 px on 1024x768 tablet)
            RectTransform openPanel = Ui.Panel("OpenRolesPanel", dock, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            openPanel.offsetMin = new Vector2(dividerX + 12f, 0f);
            openPanel.offsetMax = new Vector2(-12f, 0f);

            _openRoles = KidsScreenView.Label("OpenRoles", openPanel, "", 16, MutedSoft, TextAnchor.UpperLeft, display: true);
            _openRolesRect = _openRoles.rectTransform;
            KidsScreenView.Stretch(_openRolesRect, Vector2.zero, Vector2.one);
            _openRolesRect.offsetMin = new Vector2(0f, 38f);
            _openRolesRect.offsetMax = new Vector2(0f, -8f);

            RectTransform chipRow = Ui.Panel("OpenRoleChips", openPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(0f, 28f));
            for (int i = 0; i < MaxOpenRoleChips; i++)
            {
                RectTransform chip = Ui.Panel("OpenChip_" + i, chipRow, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(i * (OpenChipWidth + OpenChipGap), 0f), new Vector2(OpenChipWidth, 0f));
                Ui.MakeSliced("Bg", chip, MenuArt.StatChip(6), Color.white);
                Image outline = Ui.MakeSliced("Outline", chip, MenuArt.ChamferOutline(Color.white, cut: 6, borderPx: 1), PanelBorder);
                Image icon = Ui.MakeImage("Icon", chip, Cream);
                KidsScreenView.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(14f, 14f));
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);

                Text lbl = KidsScreenView.Label("Label", chip, "", 14, Cream, TextAnchor.MiddleLeft, display: true);
                KidsScreenView.Stretch(lbl.rectTransform, Vector2.zero, Vector2.one);
                lbl.rectTransform.offsetMin = new Vector2(23f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-4f, 0f);
                lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                lbl.verticalOverflow = VerticalWrapMode.Truncate;
                lbl.resizeTextForBestFit = true;
                lbl.resizeTextMinSize = 8;
                lbl.resizeTextMaxSize = 14;

                _openRoleChips.Add(new OpenRoleChip { Root = chip.gameObject, Outline = outline, Icon = icon, Label = lbl });
            }

            RectTransform lockRect = Ui.Panel("LockIn", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(LockWidth, TeamBarHeight));
            Image lockHit = lockRect.gameObject.AddComponent<Image>();
            lockHit.color = Clear;

            _lockPlate = Ui.MakeSliced("Plate", lockRect, MenuArt.CtaGold(14), Color.white);
            _lockPlate.rectTransform.offsetMin = new Vector2(0f, 10f);
            _lockPlate.rectTransform.offsetMax = new Vector2(0f, -10f);
            _lockSheen = Ui.AddSheen(_lockPlate.rectTransform);

            _lockIcon = Ui.MakeImage("LockIcon", _lockPlate.rectTransform, MutedSoft);
            _lockIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Lock);
            KidsScreenView.Place(_lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-52f, 0f), new Vector2(22f, 22f));

            _lockLabel = KidsScreenView.Label("Label", _lockPlate.rectTransform, "LOCK IN", 36, Ink, TextAnchor.MiddleCenter, display: true);
            KidsScreenView.Stretch(_lockLabel.rectTransform, Vector2.zero, Vector2.one);

            _lockButton = lockRect.gameObject.AddComponent<Button>();
            _lockButton.targetGraphic = lockHit;
            _lockButton.transition = Selectable.Transition.None;
            _lockButton.onClick.AddListener(ConfirmSelection);
            Ui.AddPressFeel(lockRect, 0.94f);
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
                pair.Value.Frame.color = selected ? GoldBorder : PanelBorder;
                pair.Value.Brackets.gameObject.SetActive(selected);
                pair.Value.Spotlight.color = selected ? SpotActive : SpotNormal;
                pair.Value.NameRule.color = selected ? new Color(Gold.r, Gold.g, Gold.b, 0.62f) : Hairline;
                pair.Value.TakenTag.SetActive(_taken.Contains(pair.Key));
            }

            string role = RoleOf(_selected);
            string description = Field(_selected, "description") ?? "";
            _roleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _roleLabel.text = KidsScreenView.Purok(description).ToUpperInvariant();
            _nameLabel.text = (Field(_selected, "display_name") ?? _selected).ToUpperInvariant();

            Sprite activeTabSprite = MenuArt.ChamferCard(SkillActiveTop, SkillActiveBottom, GoldBorder, cut: 10, borderPx: 2, shine: 0.34f);
            Sprite ultIdleSprite = MenuArt.ChamferCard(CardTop, CardBottom, UltCardBorder, cut: 10, borderPx: 2, shine: 0.24f);
            Sprite idleTabSprite = MenuArt.ChamferGlass(10);

            KidsScreenView.KidPageData page = KidsScreenView.PageFor(_selected);
            for (int i = 0; i < _skillTabs.Count; i++)
            {
                KidsScreenView.SkillData skill = page != null && page.skills != null && i < page.skills.Length ? page.skills[i] : null;
                SkillTab tab = _skillTabs[i];
                tab.Root.SetActive(skill != null);
                if (skill == null) continue;
                bool ultimate = skill.slot == "ULT", on = i == _skill;
                tab.Plate.sprite = on ? activeTabSprite : ultimate ? ultIdleSprite : idleTabSprite;
                tab.Brackets.gameObject.SetActive(on);
                tab.Badge.color = ultimate ? Gold : SkillBadge;
                tab.BadgeRing.gameObject.SetActive(!ultimate);
                tab.BadgeRing.color = on ? GoldBorder : PanelBorder;
                tab.BadgeText.text = skill.slot;
                KidsScreenView.SetLabelColor(tab.BadgeText, ultimate ? Ink : (on ? GoldLight : Gold));
                tab.Caption.text = KidsScreenView.SlotCaption(skill.slot);
                KidsScreenView.SetLabelColor(tab.Caption, on ? Cream : (ultimate ? GoldLight : MutedSoft));

                if (on)
                {
                    _skillCardBg.sprite = ultimate
                        ? MenuArt.ChamferCard(CardTop, CardBottom, UltCardBorder, cut: 14, borderPx: 2, shine: 0.26f)
                        : MenuArt.ChamferGlass(14);
                    _skillKicker.text = ultimate ? "ULTIMATE ABILITY" : KidsScreenView.SlotCaption(skill.slot);
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
            _lockPlate.sprite = can
                ? MenuArt.CtaGold(14)
                : MenuArt.ChamferCard(LockDisabledTop, LockDisabledBottom, PanelBorder, cut: 14, borderPx: 2, shine: 0.20f);
            _lockIcon.gameObject.SetActive(taken);
            _lockLabel.text = taken ? "TAKEN" : "LOCK IN";
            _lockLabel.rectTransform.offsetMin = new Vector2(taken ? 22f : 0f, can ? 4f : 0f);
            KidsScreenView.SetLabelColor(_lockLabel, can ? Ink : MutedSoft);
            _lockButton.interactable = can;
            if (_lockSheen != null)
            {
                _lockSheen.enabled = can;
                if (!can) _lockSheen.ResetSheen();
            }
        }

        void RefreshTeamBar()
        {
            // you first, then the kids teammates took; an empty slot shows a quiet silhouette
            for (int i = 0; i < _slots.Count; i++)
            {
                string id = i == 0 ? _selected : i - 1 < _takenOrder.Count ? _takenOrder[i - 1] : null;
                Texture2D tex = id != null ? KalyeahResources.Texture("portraits/busts/" + id) : null;
                _slots[i].Bust.texture = tex;
                _slots[i].Bust.enabled = tex != null;
                _slots[i].EmptyIcon.SetActive(tex == null);
                _slots[i].Ring.color = i == 0 ? GoldLight : id != null ? Cream : PanelBorder;
            }

            List<string> missing = MissingRoles();
            bool full = _takenOrder.Count + 1 >= TeamSize; // a full team with a role missing is a warning; before that it is open roles
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
                _openRoleChips[i].Icon.color = full ? WarnAmber : Cream;
                _openRoleChips[i].Outline.color = full ? new Color(WarnAmber.r, WarnAmber.g, WarnAmber.b, 0.55f) : PanelBorder;
                _openRoleChips[i].Label.text = role;
                KidsScreenView.SetLabelColor(_openRoleChips[i].Label, full ? WarnAmber : Cream);
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stage?.Dispose();
            if (Root != null) KalyeahResources.Kill(Root.gameObject);
        }
    }
}
