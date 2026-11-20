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
        Text _pageStory;
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

        void BuildPage()
        {
            _pagePanel = Ui.Fill("PagePanel", SafeRoot);
            Header(_pagePanel, "", () => Back());

            // the kid fills the space right of the info column at the stage's own aspect, standing on the bottom edge
            RectTransform stageArea = Ui.Fill("StageArea", _pagePanel);
            stageArea.anchorMin = new Vector2(0.36f, 0f);
            stageArea.offsetMax = new Vector2(-Gutter, -HeaderHeight * 0.5f);
            _pageHero = MakeRaw("Hero", stageArea, _stage.Texture);
            _pageHero.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fit = _pageHero.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.75f;

            RectTransform info = Ui.Panel("Info", _pagePanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(460f, 0f));
            info.offsetMin = new Vector2(Gutter, Gutter);
            info.offsetMax = new Vector2(Gutter + 460f, -HeaderHeight);

            Image disc = Ui.MakeImage("RoleDisc", info, Gold, round: true);
            Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(40f, 40f));
            disc.rectTransform.pivot = new Vector2(0f, 1f);
            _pageRoleIcon = Ui.MakeImage("Icon", disc.rectTransform, Ink);
            Place(_pageRoleIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            _pageRole = Label("Role", info, "", 22, Gold, TextAnchor.MiddleLeft, display: true);
            Place(_pageRole.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(52f, -8f), new Vector2(-52f, 40f)); // starts 52 in: clear of the 40-wide disc
            _pageRole.rectTransform.pivot = new Vector2(0f, 1f);

            _pageName = Label("Name", info, "", 96, Cream, TextAnchor.UpperLeft, display: true, ink: true);
            Place(_pageName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, 110f));
            _pageName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageName.resizeTextForBestFit = true;
            _pageName.resizeTextMinSize = 48;
            _pageName.resizeTextMaxSize = 96;

            Text storyHead = Label("StoryHead", info, "STORY", 22, Gold, TextAnchor.UpperLeft, display: true);
            Place(storyHead.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -186f), new Vector2(0f, 30f));
            storyHead.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageStory = Label("Story", info, "", 24, Muted, TextAnchor.UpperLeft);
            Place(_pageStory.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -222f), new Vector2(0f, 150f));
            _pageStory.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pageStory.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pageStory.verticalOverflow = VerticalWrapMode.Overflow;
            _pageStory.lineSpacing = 1.15f;

            RectTransform home = Ui.Panel("ShowOnHome", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, HitSize));
            _homePlate = Ui.MakeImage("Plate", home, Color.white, false, true);
            _homePlate.sprite = MenuArt.RoundGold(22);
            _homePlate.type = Image.Type.Sliced;
            Stretch(_homePlate.rectTransform, Vector2.zero, Vector2.one);
            _homeLabel = Label("Label", home, "SHOW ON HOME", 28, Ink, TextAnchor.MiddleCenter, display: true);
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
            _pageStory.text = Story(description);
            _stage.SetKid(id);
            _stage.RenderNow();
            RefreshPage();
            RefreshLayout();
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
                ? MenuArt.RoundGlass(22)
                : MenuArt.RoundGold(22);
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
