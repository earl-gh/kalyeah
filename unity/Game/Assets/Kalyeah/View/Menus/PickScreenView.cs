using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kid select, after the queue (docs/SPEC.md §4 and §8; the shape of an agent select, not a copy of one): the 12 kids in four
    /// role rows on the left, the chosen kid in 3D on a name plate in the middle, and on the right their three skills as a
    /// selector with the chosen skill's cost and description below it; a team bar at the foot shows your kid, the kids teammates
    /// took and the roles still open, beside LOCK IN. Every part is anchored to a share of the screen, sized so each control
    /// still reaches 48 dp and each text still fits on the narrowest menu canvas (960 units, a 4:3 tablet) and grows on wider
    /// ones, so nothing wraps or overflows.
    /// Only real content: the 30 s timer, hovers, hidden enemy picks and skill clips join when matchmaking and the clips exist.
    /// </summary>
    public sealed class PickScreenView : IDisposable
    {
        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.Cream;
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Muted = new Color(0.80f, 0.84f, 0.90f, 1f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Shade = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color CardBg = new Color(0.10f, 0.14f, 0.22f, 1f);
        static readonly Color Rim = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color SkillBadge = new Color(0.2f, 0.27f, 0.4f, 1f);

        const float HeaderHeight = 96f;
        const float TeamBarHeight = 96f;
        const float Gutter = 20f;
        const float HitSize = 96f;
        const float RowGap = 10f;
        const float RowLabelWidth = 100f;
        const float CardGap = 10f;
        const float NamePlateHeight = 84f;
        const float SkillTabHeight = 112f;
        const float SkillTabGap = 10f;
        const float BadgeSize = 64f;
        const float SlotSize = 64f;
        const float SlotGap = 10f;
        const float LockWidth = 300f;
        const int Columns = 3;
        const int TeamSize = 5;
        const int SkillCount = 3;

        // the body's three columns, as shares of its width
        const float RosterEnd = 0.45f;
        const float HeroStart = 0.45f;
        const float HeroEnd = 0.66f;
        const float DetailStart = 0.67f;

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
            public GameObject TakenTag;
        }

        sealed class Slot
        {
            public Image Ring;
            public RawImage Bust;
        }

        sealed class SkillTab
        {
            public Image Frame;
            public Image Badge;
            public Text BadgeText;
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
        readonly List<SkillTab> _skillTabs = new List<SkillTab>(SkillCount);
        string _selected = AppSession.DefaultCharacter;
        int _skill;
        bool _disposed;

        Text _roleLabel;
        Image _roleIcon;
        Text _nameLabel;
        Text _skillTitle;
        Text _skillCost;
        Text _skillBody;
        Text _openRoles;
        Button _lockButton;
        Image _lockPlate;
        Text _lockLabel;

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
            body.offsetMin = new Vector2(Gutter, TeamBarHeight + 2f * Gutter);
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
                raw.color = new Color(1f, 1f, 1f, 0.30f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 16f / 9f;
            }
            Image wash = Ui.MakeImage("Wash", fill.rectTransform, new Color(Shade.r, Shade.g, Shade.b, 0.70f));
            KidsScreenView.Stretch(wash.rectTransform, Vector2.zero, Vector2.one);
        }

        void BuildHeader()
        {
            RectTransform header = Ui.Panel("HeaderBar", SafeRoot, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            RectTransform back = Ui.Panel("Back", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(HitSize, HitSize));
            Image hit = back.gameObject.AddComponent<Image>();
            hit.color = Clear;
            Image arrow = Ui.MakeImage("Arrow", back, Cream);
            arrow.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            KidsScreenView.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Button b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.onClick.AddListener(GoBack);

            Text title = KidsScreenView.Label("Title", header, "CHOOSE YOUR KID", 48, Cream, TextAnchor.MiddleLeft, display: true, ink: true);
            KidsScreenView.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + HitSize + 8f, 0f), new Vector2(520f, 60f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        /// <summary>The chosen kid in 3D, standing on a glass plate with the role icon, their purok and their name.</summary>
        void BuildHero(RectTransform body)
        {
            RectTransform area = Ui.Panel("HeroArea", body, new Vector2(HeroStart, 0f), new Vector2(HeroEnd, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.offsetMin = new Vector2(0f, NamePlateHeight - 6f); // the kid stands on the name plate
            RawImage hero = KidsScreenView.MakeRaw("Hero", area, _stage.Texture);
            hero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = hero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;

            RectTransform plate = Ui.Panel("NamePlate", body, new Vector2(HeroStart, 0f), new Vector2(HeroEnd, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, NamePlateHeight));
            Image glass = Ui.MakeImage("Glass", plate, Color.white);
            glass.sprite = MenuArt.RoundGlass(22);
            glass.type = Image.Type.Sliced;
            KidsScreenView.Stretch(glass.rectTransform, Vector2.zero, Vector2.one);

            Image disc = Ui.MakeImage("RoleDisc", plate, Gold, round: true);
            KidsScreenView.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(44f, 44f));
            disc.rectTransform.pivot = new Vector2(0f, 0.5f);
            _roleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            KidsScreenView.Place(_roleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            _roleLabel = KidsScreenView.Label("Role", plate, "", 22, Gold, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Stretch(_roleLabel.rectTransform, Vector2.zero, Vector2.one);
            _roleLabel.rectTransform.offsetMin = new Vector2(72f, 0f);
            _roleLabel.rectTransform.offsetMax = new Vector2(-8f, -6f);
            _roleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameLabel = KidsScreenView.Label("Name", plate, "", 48, Cream, TextAnchor.LowerLeft, display: true, ink: true);
            KidsScreenView.Stretch(_nameLabel.rectTransform, Vector2.zero, Vector2.one);
            _nameLabel.rectTransform.offsetMin = new Vector2(72f, 2f);
            _nameLabel.rectTransform.offsetMax = new Vector2(-8f, -26f);
            _nameLabel.resizeTextForBestFit = true;
            _nameLabel.resizeTextMinSize = 30;
            _nameLabel.resizeTextMaxSize = 48;
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
                Image disc = Ui.MakeImage("Disc", label, new Color(1f, 1f, 1f, 0.10f), round: true);
                KidsScreenView.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(48f, 48f));
                disc.rectTransform.pivot = new Vector2(0.5f, 1f);
                Image icon = Ui.MakeImage("Icon", disc.rectTransform, Cream);
                icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(Roles[r]));
                KidsScreenView.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
                Text word = KidsScreenView.Label("Word", label, Roles[r], 22, Muted, TextAnchor.UpperCenter, display: true);
                KidsScreenView.Stretch(word.rectTransform, Vector2.zero, new Vector2(1f, 0.5f));
                word.rectTransform.offsetMax = new Vector2(0f, 4f);
                word.horizontalOverflow = HorizontalWrapMode.Overflow;

                // the three cards share what is left of the row after the role label
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

            Image frame = Ui.MakeImage("Frame", rect, Rim, false, true);
            frame.sprite = MenuArt.RoundSolid(18);
            frame.type = Image.Type.Sliced;
            KidsScreenView.Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
            Image fill = Ui.MakeImage("Fill", rect, CardBg);
            fill.sprite = MenuArt.RoundSolid(16);
            fill.type = Image.Type.Sliced;
            KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, KalyeahResources.Texture("portraits/busts/" + id));
            bust.enabled = bust.texture != null;
            bust.rectTransform.pivot = new Vector2(0.5f, 1f); // covers the card, cropped from the bottom so the head stays
            var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 0.75f;
            RawImage fade = KidsScreenView.MakeRaw("Fade", fill.rectTransform, KidsScreenView.Ramp());
            fade.color = new Color(Shade.r, Shade.g, Shade.b, 0.95f);
            KidsScreenView.Stretch(fade.rectTransform, Vector2.zero, new Vector2(1f, 0.42f));

            Text name = KidsScreenView.Label("Name", rect, (Field(id, "display_name") ?? id).ToUpperInvariant(), 24, Cream, TextAnchor.LowerCenter, display: true, ink: true);
            KidsScreenView.Stretch(name.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.sizeDelta = new Vector2(-8f, 32f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            Image dim = Ui.MakeImage("Dim", rect, new Color(Shade.r, Shade.g, Shade.b, 0.72f));
            dim.sprite = MenuArt.RoundSolid(16);
            dim.type = Image.Type.Sliced;
            KidsScreenView.Stretch(dim.rectTransform, Vector2.zero, Vector2.one);
            Text taken = KidsScreenView.Label("Taken", dim.rectTransform, "TAKEN", 22, Gold, TextAnchor.MiddleCenter, display: true);
            KidsScreenView.Stretch(taken.rectTransform, Vector2.zero, Vector2.one);

            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.onClick.AddListener(() => SelectCharacter(id));
            return new Card { Frame = frame, TakenTag = dim.gameObject };
        }

        /// <summary>
        /// The right column: a selector of the kid's three skills (S1, S2, ultimate) and, below it, the chosen skill's name, cost and
        /// description in a card with room for the longest one.
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

                Image frame = Ui.MakeImage("Frame", tab, Rim, false, true);
                frame.sprite = MenuArt.RoundSolid(20);
                frame.type = Image.Type.Sliced;
                KidsScreenView.Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
                Image glass = Ui.MakeImage("Glass", tab, Color.white);
                glass.sprite = MenuArt.RoundGlass(18);
                glass.type = Image.Type.Sliced;
                KidsScreenView.Stretch(glass.rectTransform, Vector2.zero, Vector2.one);
                glass.rectTransform.offsetMin = new Vector2(3f, 3f);
                glass.rectTransform.offsetMax = new Vector2(-3f, -3f);

                Image badge = Ui.MakeImage("Badge", tab, SkillBadge, round: true);
                KidsScreenView.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BadgeSize, BadgeSize));
                Text badgeText = KidsScreenView.Label("Text", badge.rectTransform, "", 28, Gold, TextAnchor.MiddleCenter, display: true);
                KidsScreenView.Stretch(badgeText.rectTransform, Vector2.zero, Vector2.one);

                Button b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = frame;
                b.onClick.AddListener(() => SelectSkill(index));
                _skillTabs.Add(new SkillTab { Frame = frame, Badge = badge, BadgeText = badgeText, Root = tab.gameObject });
            }

            RectTransform card = Ui.Panel("SkillCard", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.offsetMax = new Vector2(0f, -(SkillTabHeight + 12f));
            Image cardGlass = Ui.MakeImage("Glass", card, Color.white);
            cardGlass.sprite = MenuArt.RoundGlass(24);
            cardGlass.type = Image.Type.Sliced;
            KidsScreenView.Stretch(cardGlass.rectTransform, Vector2.zero, Vector2.one);

            _skillTitle = KidsScreenView.Label("Title", card, "", 40, Cream, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Place(_skillTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(-48f, 48f));
            _skillTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _skillTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _skillCost = KidsScreenView.Label("Cost", card, "", 24, Gold, TextAnchor.UpperLeft, display: true);
            KidsScreenView.Place(_skillCost.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -68f), new Vector2(-48f, 34f));
            _skillCost.rectTransform.pivot = new Vector2(0.5f, 1f);
            _skillCost.horizontalOverflow = HorizontalWrapMode.Overflow;
            _skillBody = KidsScreenView.Label("Body", card, "", 24, Cream, TextAnchor.UpperLeft);
            KidsScreenView.Stretch(_skillBody.rectTransform, Vector2.zero, Vector2.one);
            _skillBody.rectTransform.offsetMin = new Vector2(20f, 14f);
            _skillBody.rectTransform.offsetMax = new Vector2(-20f, -104f);
            _skillBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _skillBody.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildTeamBar()
        {
            RectTransform bar = Ui.Panel("TeamBar", SafeRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Gutter), new Vector2(-2f * Gutter, TeamBarHeight));

            for (int i = 0; i < TeamSize; i++)
            {
                Image ring = Ui.MakeImage("Slot_" + i, bar, Rim, round: true);
                KidsScreenView.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (SlotSize + SlotGap), 0f), new Vector2(SlotSize, SlotSize));
                ring.rectTransform.pivot = new Vector2(0f, 0.5f);
                Image fill = Ui.MakeImage("Fill", ring.rectTransform, CardBg, round: true);
                KidsScreenView.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
                fill.rectTransform.offsetMin = new Vector2(4f, 4f);
                fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
                fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                RawImage bust = KidsScreenView.MakeRaw("Bust", fill.rectTransform, null);
                bust.rectTransform.pivot = new Vector2(0.5f, 1f);
                var cover = bust.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = 0.75f;
                _slots.Add(new Slot { Ring = ring, Bust = bust });
            }

            // the missing-role note fills the space between the slots and LOCK IN
            float noteLeft = TeamSize * (SlotSize + SlotGap) + 6f;
            _openRoles = KidsScreenView.Label("OpenRoles", bar, "", 22, Muted, TextAnchor.MiddleLeft);
            KidsScreenView.Stretch(_openRoles.rectTransform, Vector2.zero, Vector2.one);
            _openRoles.rectTransform.offsetMin = new Vector2(noteLeft, 0f);
            _openRoles.rectTransform.offsetMax = new Vector2(-(LockWidth + 16f), 0f);
            _openRoles.horizontalOverflow = HorizontalWrapMode.Wrap;
            _openRoles.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform lockRect = Ui.Panel("LockIn", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(LockWidth, TeamBarHeight));
            _lockPlate = Ui.MakeImage("Plate", lockRect, Color.white, false, true);
            _lockPlate.type = Image.Type.Sliced;
            KidsScreenView.Stretch(_lockPlate.rectTransform, Vector2.zero, Vector2.one);
            _lockLabel = KidsScreenView.Label("Label", lockRect, "LOCK IN", 40, Ink, TextAnchor.MiddleCenter, display: true);
            KidsScreenView.Stretch(_lockLabel.rectTransform, Vector2.zero, Vector2.one);
            _lockButton = lockRect.gameObject.AddComponent<Button>();
            _lockButton.targetGraphic = _lockPlate;
            _lockButton.onClick.AddListener(ConfirmSelection);
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
                pair.Value.Frame.color = pair.Key == _selected ? Gold : Rim;
                pair.Value.TakenTag.SetActive(_taken.Contains(pair.Key));
            }

            string role = RoleOf(_selected);
            string description = Field(_selected, "description") ?? "";
            _roleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
            _roleLabel.text = KidsScreenView.Purok(description).ToUpperInvariant();
            _nameLabel.text = (Field(_selected, "display_name") ?? _selected).ToUpperInvariant();

            KidsScreenView.KidPageData page = KidsScreenView.PageFor(_selected);
            for (int i = 0; i < _skillTabs.Count; i++)
            {
                KidsScreenView.SkillData skill = page != null && page.skills != null && i < page.skills.Length ? page.skills[i] : null;
                SkillTab tab = _skillTabs[i];
                tab.Root.SetActive(skill != null);
                if (skill == null) continue;
                bool ultimate = skill.slot == "ULT", on = i == _skill;
                tab.BadgeText.text = skill.slot;
                tab.BadgeText.color = ultimate ? Ink : Gold;
                tab.Badge.color = ultimate ? Gold : SkillBadge;
                tab.Frame.color = on ? Gold : Rim;
                if (on)
                {
                    _skillTitle.text = skill.name;
                    _skillCost.text = skill.cost;
                    _skillBody.text = skill.what;
                }
            }
            _stage.SetKid(_selected);
            _stage.RenderNow();

            RefreshTeamBar();
            bool can = CanConfirm;
            _lockPlate.sprite = can ? MenuArt.RoundGold(24) : MenuArt.RoundGlass(24);
            _lockLabel.text = _taken.Contains(_selected) ? "TAKEN" : "LOCK IN";
            _lockLabel.color = can ? Ink : Muted;
            _lockButton.interactable = can;
        }

        void RefreshTeamBar()
        {
            // you first, then the kids teammates took; an empty ring is a teammate who has not picked
            for (int i = 0; i < _slots.Count; i++)
            {
                string id = i == 0 ? _selected : i - 1 < _takenOrder.Count ? _takenOrder[i - 1] : null;
                _slots[i].Bust.texture = id != null ? KalyeahResources.Texture("portraits/busts/" + id) : null;
                _slots[i].Bust.enabled = _slots[i].Bust.texture != null;
                _slots[i].Ring.color = i == 0 ? Gold : id != null ? Cream : Rim;
            }
            List<string> missing = MissingRoles();
            for (int i = 0; i < missing.Count; i++) missing[i] = missing[i].Substring(0, 1) + missing[i].Substring(1).ToLowerInvariant();
            bool full = _takenOrder.Count + 1 >= TeamSize; // a full team with a role missing is a warning; before that it is only what is still open
            _openRoles.text = missing.Count == 0 ? "Every role is covered" : full ? "No " + string.Join(", ", missing) : "Still open: " + string.Join(" · ", missing);
            _openRoles.color = missing.Count > 0 && full ? new Color(1f, 0.78f, 0.45f, 1f) : Muted;
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
