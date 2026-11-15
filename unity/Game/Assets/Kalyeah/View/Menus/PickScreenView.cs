using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Landscape 10-kid Filipino roster picker (carousel + grid, Resources portraits, outfit swatches, cosmetic-only note, bio)
    /// and 3-Superpower Loadout Builder across all 14 deck weapons (icons, Damage/Range/Cooldown stat bars, multiplayer
    /// taken-character/weapon locks, and direct persistence to AppSession.Current + PlayerPrefs).
    /// </summary>
    public sealed class PickScreenView : IDisposable
    {
        public const int LoadoutSlotCount = 3;
        public const string CosmeticOnlyNote = "100% Cosmetic · Same Stats";

        static readonly string[] SlotBadges = { "SLOT 1", "SLOT 2", "SLOT 3" };

        static readonly string[] FallbackDeckWeaponIds =
        {
            "bato_light", "bato_heavy", "gunting_light", "gunting_heavy",
            "papel_trap", "papel_shield", "tsinelas_light", "tsinelas_heavy",
            "lata", "jacks", "bola", "trumpo", "heal", "langit_lupa",
        };

        // Street-komiks palette
        static readonly Color ColBackdropBottom = new Color(0.07f, 0.08f, 0.17f, 0.98f);
        static readonly Color ColBackdropTop = new Color(0.20f, 0.12f, 0.28f, 0.96f);
        static readonly Color ColSunGlow = new Color(0.98f, 0.58f, 0.20f, 0.16f);
        static readonly Color ColPanelDark = new Color(0.09f, 0.12f, 0.24f, 0.92f);
        static readonly Color ColPanelInset = new Color(0.05f, 0.08f, 0.16f, 0.94f);
        static readonly Color ColCardIdle = new Color(0.14f, 0.18f, 0.33f, 0.95f);
        static readonly Color ColCardSelected = new Color(0.32f, 0.25f, 0.12f, 0.98f);
        static readonly Color ColCardTaken = new Color(0.10f, 0.11f, 0.16f, 0.88f);
        static readonly Color ColGold = new Color(1.00f, 0.78f, 0.22f, 1.00f);
        static readonly Color ColAmber = new Color(0.96f, 0.60f, 0.16f, 1.00f);
        static readonly Color ColReady = new Color(0.98f, 0.54f, 0.14f, 1.00f);
        static readonly Color ColDisabledBtn = new Color(0.28f, 0.32f, 0.42f, 0.92f);
        static readonly Color ColBlueBtn = new Color(0.18f, 0.42f, 0.78f, 0.96f);
        static readonly Color ColTealBtn = new Color(0.14f, 0.56f, 0.52f, 0.96f);
        static readonly Color ColMutedText = new Color(0.76f, 0.84f, 0.96f, 1.00f);
        static readonly Color ColSoftLabel = new Color(0.86f, 0.92f, 1.00f, 1.00f);
        static readonly Color ColCosmeticPill = new Color(0.10f, 0.32f, 0.26f, 0.96f);
        static readonly Color ColCosmeticText = new Color(0.72f, 1.00f, 0.86f, 1.00f);
        static readonly Color ColTakenBadge = new Color(0.76f, 0.20f, 0.22f, 0.96f);
        static readonly Color ColBarTrack = new Color(0.04f, 0.07f, 0.16f, 0.96f);
        static readonly Color ColBarDamage = new Color(1.00f, 0.78f, 0.24f, 1.00f);
        static readonly Color ColBarHeal = new Color(0.36f, 0.94f, 0.58f, 1.00f);
        static readonly Color ColBarRange = new Color(0.42f, 0.78f, 1.00f, 1.00f);
        static readonly Color ColBarCooldown = new Color(1.00f, 0.58f, 0.46f, 1.00f);
        static readonly Color ColInk = new Color(0.08f, 0.06f, 0.05f, 1.00f);

        const float ScrollHintFooterHeight = 30f;

        static Sprite s_roundedSprite;

        public sealed class CharacterCardData
        {
            public string Id;
            public string DisplayName;
            public string Description;
            public Color Tint;
            public Color Accent;
            public Color[] OutfitSwatches;
            public Texture2D PortraitTexture;
            public Rect PortraitUvRect = new Rect(0f, 0f, 1f, 1f);
            public Texture2D CardTexture;
        }

        sealed class KidTileRefs
        {
            public string Id;
            public Button Button;
            public Image Frame;
            public Image Border;
            public RawImage Portrait;
            public Image FallbackDisc;
            public Text Initials;
            public Text NameLabel;
            public GameObject SelectedBadge;
            public GameObject TakenBadge;
        }

        sealed class SlotRefs
        {
            public int Index;
            public Button Button;
            public Image Frame;
            public Image ActiveRing;
            public RectTransform IconHolder;
            public RawImage Icon;
            public AspectRatioFitter IconFitter;
            public Image FallbackBadge;
            public Text FallbackCode;
            public Text SlotKeyLabel;
            public Text WeaponNameLabel;
            public Text RoleLabel;
        }

        sealed class WeaponTileRefs
        {
            public string Id;
            public WeaponInfo Info;
            public Button Button;
            public Image Frame;
            public Image Border;
            public RectTransform IconHolder;
            public RawImage Icon;
            public AspectRatioFitter IconFitter;
            public Image FallbackBadge;
            public Text FallbackCode;
            public Text NameLabel;
            public Text RoleLabel;
            public Image PowerFill;
            public Image RangeFill;
            public Image CooldownFill;
            public GameObject EquippedBadge;
            public Text EquippedSlotText;
            public GameObject TakenBadge;
        }

        sealed class StatMeterRefs
        {
            public Text CaptionLabel;
            public Image FillImage;
            public Text ValueLabel;
        }

        [ExecuteAlways]
        sealed class LifecycleHook : MonoBehaviour
        {
            [NonSerialized] public PickScreenView Owner;

            void OnEnable() => Owner?.OnRootEnabled();
            void OnRectTransformDimensionsChange() => Owner?.RefreshResponsiveLayout();
            void OnDestroy() => Owner?.DisposeSubscriptions();
        }

        readonly DataSource _data;
        readonly Action _onBack;
        readonly Action _onConfirmed;

        readonly List<CharacterCardData> _characters = new List<CharacterCardData>(10);
        readonly Dictionary<string, CharacterCardData> _characterById = new Dictionary<string, CharacterCardData>(StringComparer.Ordinal);
        readonly List<WeaponInfo> _deckWeapons = new List<WeaponInfo>(14);
        readonly Dictionary<string, WeaponInfo> _weaponById = new Dictionary<string, WeaponInfo>(StringComparer.Ordinal);
        readonly List<Kalyeah.Sim.WeaponDef> _simDeckWeapons = new List<Kalyeah.Sim.WeaponDef>(14);
        readonly Dictionary<string, Kalyeah.Sim.WeaponDef> _simWeaponById = new Dictionary<string, Kalyeah.Sim.WeaponDef>(StringComparer.Ordinal);
        readonly Dictionary<string, Texture2D> _weaponIcons = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly HashSet<string> _takenCharacters = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _takenWeapons = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _selectedWeapons = new List<string>(LoadoutSlotCount);

        readonly List<KidTileRefs> _kidTiles = new List<KidTileRefs>(10);
        readonly SlotRefs[] _slots = new SlotRefs[LoadoutSlotCount];
        readonly List<WeaponTileRefs> _weaponTiles = new List<WeaponTileRefs>(14);

        float _maxPower = 38f;
        float _maxRange = 24f;
        float _maxCooldown = 12f;

        string _selectedCharacterId = AppSession.DefaultCharacter;
        string _focusedWeaponId = "";
        int _activeSlotIndex;
        bool _suppressSessionSync;
        bool _subscribed;
        bool _disposed;

        // Responsive layout refs
        Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _workspace;
        readonly RectTransform _headerBar;
        readonly RectTransform _backBtnRect;
        readonly RectTransform _headerTextRect;
        readonly RectTransform _suggestBtnRect;
        readonly RectTransform _confirmBtnRect;
        readonly RectTransform _leftPanel;
        readonly RectTransform _spotlightRow;
        readonly RectTransform _prevKidRect;
        readonly RectTransform _spotlightCardRect;
        readonly RectTransform _nextKidRect;
        readonly RectTransform _bioCardRect;
        readonly RectTransform _cosmeticPillRect;
        readonly RectTransform _rosterScrollRect;
        readonly RectTransform _rosterViewportRect;
        readonly ScrollRect _rosterScroll;
        readonly GameObject _rosterScrollCue;
        readonly RectTransform _rosterGridRect;
        readonly GridLayoutGroup _rosterGrid;
        readonly RectTransform _rightPanel;
        readonly RectTransform _slotsRowRect;
        readonly RectTransform _weaponsScrollRect;
        readonly RectTransform _weaponsViewportRect;
        readonly ScrollRect _weaponsScroll;
        readonly GameObject _weaponsScrollCue;
        readonly RectTransform _weaponsGridRect;
        readonly GridLayoutGroup _weaponsGrid;
        readonly RectTransform _inspectorRect;

        // Dynamic UI display refs
        readonly Button _backButton;
        readonly Button _suggestButton;
        readonly Button _confirmButton;
        readonly Image _confirmBtnImage;
        readonly Text _confirmBtnLabel;
        readonly Text _subtitleLabel;

        readonly Button _prevKidButton;
        readonly Button _nextKidButton;
        readonly Image _spotlightFrame;
        readonly Image _spotlightStage;
        readonly RawImage _spotlightPortrait;
        readonly Image _spotlightFallbackDisc;
        readonly Text _spotlightInitials;
        readonly Text _kidIndexLabel;
        readonly Image[] _swatchDots = new Image[5];
        readonly Text _kidNameLabel;
        readonly Text _cosmeticNoteLabel;
        readonly Text _kidBioLabel;

        readonly RawImage _inspectorIcon;
        readonly AspectRatioFitter _inspectorIconFitter;
        readonly Image _inspectorFallbackBadge;
        readonly Text _inspectorFallbackCode;
        readonly Text _inspectorNameLabel;
        readonly Image _inspectorRoleChip;
        readonly Text _inspectorRoleLabel;
        readonly Text _inspectorGameLabel;
        readonly Text _inspectorDescLabel;
        readonly StatMeterRefs _powerMeter;
        readonly StatMeterRefs _rangeMeter;
        readonly StatMeterRefs _cooldownMeter;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public GameObject GameObject => Root != null ? Root.gameObject : null;
        public AudioHub Audio { get; set; }

        public event Action BackRequested;
        public event Action Confirmed;
        public event Action<string> CharacterSelected;
        public event Action<IReadOnlyList<string>> LoadoutChanged;

        public IReadOnlyList<CharacterCardData> Characters => _characters;
        public IReadOnlyList<WeaponInfo> DeckWeapons => _deckWeapons;
        public string SelectedCharacterId => _selectedCharacterId;
        public IReadOnlyList<string> SelectedWeaponIds => _selectedWeapons;
        public string FocusedWeaponId => _focusedWeaponId;
        public int ActiveSlotIndex => _activeSlotIndex;
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        public bool IsZeroDamageLoadout => DealsZeroDirectDamage(_selectedWeapons);

        public bool CanConfirm
        {
            get
            {
                if (string.IsNullOrEmpty(_selectedCharacterId) || _takenCharacters.Contains(_selectedCharacterId))
                    return false;
                if (_selectedWeapons.Count < LoadoutSlotCount)
                    return false;
                for (int i = 0; i < _selectedWeapons.Count; i++)
                {
                    string wid = _selectedWeapons[i];
                    if (string.IsNullOrEmpty(wid) || _takenWeapons.Contains(wid))
                        return false;
                }
                if (DealsZeroDirectDamage(_selectedWeapons))
                    return false;
                return true;
            }
        }

        public PickScreenView(Transform parent, DataSource data, Action onBack, Action onConfirmed = null)
        {
            _data = data ?? KalyeahResources.Data();
            _onBack = onBack;
            _onConfirmed = onConfirmed;
            _canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;

            ScreenFitMetrics.EnsureEnhancedTouch();
            LoadCatalogs();

            Root = Ui.Fill("PickScreen", parent);
            BuildBackdrop(Root);

            SafeArea parentSafe = parent != null ? parent.GetComponentInParent<SafeArea>() : null;
            if (parentSafe != null)
            {
                _safeArea = parentSafe;
                SafeRoot = Ui.Fill("PickContent", Root);
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
                _safeArea.Apply();
            }

            _workspace = Ui.Panel("Workspace", SafeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // 1. Header bar
            _headerBar = Ui.Panel("HeaderBar", _workspace, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 104f));
            MakePlate("HeaderPlate", _headerBar, ColPanelDark);

            _backButton = BuildActionButton("BackButton", _headerBar, "< BACK", ColBlueBtn, Ui.Cream, 22, HandleBackClicked, out _backBtnRect);
            _backBtnRect.anchorMin = new Vector2(0f, 0.5f);
            _backBtnRect.anchorMax = new Vector2(0f, 0.5f);
            _backBtnRect.pivot = new Vector2(0f, 0.5f);

            _headerTextRect = Ui.Panel("HeaderTitles", _headerBar, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text headerTitle = Ui.MakeText("Title", _headerTextRect, "PICK KID & SUPERPOWERS", 30, Ui.Cream, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            headerTitle.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            headerTitle.rectTransform.anchorMax = Vector2.one;
            headerTitle.rectTransform.offsetMin = new Vector2(8f, 0f);
            headerTitle.rectTransform.offsetMax = new Vector2(-8f, -5f);
            headerTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            headerTitle.verticalOverflow = VerticalWrapMode.Truncate;
            headerTitle.resizeTextForBestFit = true;
            headerTitle.resizeTextMinSize = 14;
            headerTitle.resizeTextMaxSize = 30;

            _subtitleLabel = Ui.MakeText("Subtitle", _headerTextRect, "", 18, ColGold, TextAnchor.LowerCenter);
            _subtitleLabel.rectTransform.anchorMin = Vector2.zero;
            _subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.50f);
            _subtitleLabel.rectTransform.offsetMin = new Vector2(8f, 5f);
            _subtitleLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
            _subtitleLabel.lineSpacing = 0.94f;
            _subtitleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _subtitleLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _subtitleLabel.resizeTextForBestFit = true;
            _subtitleLabel.resizeTextMinSize = 12;
            _subtitleLabel.resizeTextMaxSize = 18;

            _suggestButton = BuildActionButton("SuggestButton", _headerBar, "SUGGEST", ColTealBtn, Ui.Cream, 20, () => ApplySuggestedLoadout(true), out _suggestBtnRect);
            _suggestBtnRect.anchorMin = new Vector2(1f, 0.5f);
            _suggestBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _suggestBtnRect.pivot = new Vector2(1f, 0.5f);

            _confirmButton = BuildActionButton("ConfirmButton", _headerBar, "READY!", ColReady, ColInk, 24, ConfirmSelection, out _confirmBtnRect);
            _confirmBtnRect.anchorMin = new Vector2(1f, 0.5f);
            _confirmBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _confirmBtnRect.pivot = new Vector2(1f, 0.5f);
            _confirmBtnImage = _confirmButton.GetComponent<Image>();
            _confirmBtnLabel = _confirmButton.GetComponentInChildren<Text>();
            if (_confirmBtnLabel != null)
            {
                _confirmBtnLabel.font = KalyeahResources.DisplayFont();
                Outline ol = _confirmBtnLabel.GetComponent<Outline>();
                if (ol != null) ol.enabled = false;
            }

            // 2. Left Roster Panel (Carousel + Bio + Cosmetic Note + 10-Kid Grid)
            _leftPanel = Ui.Panel("RosterPanel", _workspace, new Vector2(0f, 0f), new Vector2(0.415f, 1f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            MakePlate("RosterPlate", _leftPanel, ColPanelDark);

            _spotlightRow = Ui.Panel("SpotlightHeroRow", _leftPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-16f, 112f));

            _prevKidButton = BuildActionButton("PrevKidButton", _spotlightRow, "<", ColCardIdle, Ui.Cream, 28, () => StepCharacterCarousel(-1), out _prevKidRect);
            _prevKidRect.anchorMin = new Vector2(0f, 0.5f);
            _prevKidRect.anchorMax = new Vector2(0f, 0.5f);
            _prevKidRect.pivot = new Vector2(0f, 0.5f);

            _spotlightCardRect = Ui.Panel("SpotlightCard", _spotlightRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 112f));
            _spotlightFrame = MakePlate("SpotlightFrame", _spotlightCardRect, ColCardSelected);
            Image spotInner = MakePlate("SpotlightInner", _spotlightCardRect, ColPanelInset);
            spotInner.rectTransform.offsetMin = new Vector2(4f, 4f);
            spotInner.rectTransform.offsetMax = new Vector2(-4f, -4f);

            _spotlightFallbackDisc = Ui.MakeImage("FallbackDisc", spotInner.transform, ColAmber, round: true);
            _spotlightFallbackDisc.rectTransform.anchorMin = new Vector2(0.5f, 0.56f);
            _spotlightFallbackDisc.rectTransform.anchorMax = new Vector2(0.5f, 0.56f);
            _spotlightFallbackDisc.rectTransform.sizeDelta = new Vector2(64f, 64f);
            _spotlightInitials = Ui.MakeText("Initials", _spotlightFallbackDisc.transform, "JJ", 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_spotlightInitials.rectTransform, 0f, 0f, 0f, 0f);

            _spotlightStage = MakePlate("SpotlightStage", spotInner.transform, ColPanelInset);
            Stretch(_spotlightStage.rectTransform, 3f, 20f, -3f, -3f);

            RectTransform spotPad = Ui.Fill("SpotlightPortraitPad", _spotlightStage.transform);
            Stretch(spotPad, 2f, 2f, -2f, -2f);

            _spotlightPortrait = MakeSquareRawImage("SpotlightPortrait", spotPad);

            RectTransform swatchBar = Ui.Panel("OutfitSwatches", spotInner.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(-8f, 15f));
            var swatchGroup = swatchBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            swatchGroup.childAlignment = TextAnchor.MiddleCenter;
            swatchGroup.spacing = 4f;
            swatchGroup.childControlWidth = false;
            swatchGroup.childControlHeight = false;
            swatchGroup.childForceExpandWidth = false;
            swatchGroup.childForceExpandHeight = false;
            for (int i = 0; i < _swatchDots.Length; i++)
            {
                Image dot = Ui.MakeImage("Swatch_" + i, swatchBar, Color.white, round: true);
                dot.rectTransform.sizeDelta = new Vector2(12f, 12f);
                _swatchDots[i] = dot;
            }

            RectTransform kidIndexPill = Ui.Panel("KidIndexPill", _spotlightCardRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(58f, 26f));
            MakePlate("Bg", kidIndexPill, new Color(0.04f, 0.06f, 0.14f, 0.94f));
            _kidIndexLabel = Ui.MakeText("KidIndex", kidIndexPill, "1/10", 16, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _kidIndexLabel.resizeTextForBestFit = true;
            _kidIndexLabel.resizeTextMinSize = 16;
            _kidIndexLabel.resizeTextMaxSize = 16;
            Stretch(_kidIndexLabel.rectTransform, 4f, 1f, -4f, -1f);

            _nextKidButton = BuildActionButton("NextKidButton", _spotlightRow, ">", ColCardIdle, Ui.Cream, 28, () => StepCharacterCarousel(1), out _nextKidRect);
            _nextKidRect.anchorMin = new Vector2(1f, 0.5f);
            _nextKidRect.anchorMax = new Vector2(1f, 0.5f);
            _nextKidRect.pivot = new Vector2(1f, 0.5f);

            // Kid Bio + Proportional Cosmetic-only Note Card (0..0.44 name + 0.46..1 cosmetic pill so 4:3 never crushes KidName)
            _bioCardRect = Ui.Panel("KidBioCard", _leftPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-16f, 140f));
            MakePlate("BioPlate", _bioCardRect, ColPanelInset);

            _kidNameLabel = Ui.MakeText("SelectedKidName", _bioCardRect, "JUNJUN", 24, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _kidNameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _kidNameLabel.rectTransform.anchorMax = new Vector2(0.44f, 1f);
            _kidNameLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _kidNameLabel.rectTransform.offsetMin = new Vector2(10f, -38f);
            _kidNameLabel.rectTransform.offsetMax = new Vector2(-2f, -4f);
            _kidNameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _kidNameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _kidNameLabel.resizeTextForBestFit = true;
            _kidNameLabel.resizeTextMinSize = 13;
            _kidNameLabel.resizeTextMaxSize = 24;

            _cosmeticPillRect = Ui.Panel("CosmeticNotePill", _bioCardRect, new Vector2(0.46f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _cosmeticPillRect.offsetMin = new Vector2(0f, -38f);
            _cosmeticPillRect.offsetMax = new Vector2(-8f, -4f);
            MakePlate("PillBg", _cosmeticPillRect, ColCosmeticPill);
            _cosmeticNoteLabel = Ui.MakeText("CosmeticNoteText", _cosmeticPillRect, CosmeticOnlyNote, 16, ColCosmeticText, TextAnchor.MiddleCenter);
            _cosmeticNoteLabel.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            _cosmeticNoteLabel.lineSpacing = 0.90f;
            Stretch(_cosmeticNoteLabel.rectTransform, 5f, 2f, -5f, -2f);
            _cosmeticNoteLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cosmeticNoteLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _cosmeticNoteLabel.resizeTextForBestFit = true;
            _cosmeticNoteLabel.resizeTextMinSize = 12;
            _cosmeticNoteLabel.resizeTextMaxSize = 16;

            _kidBioLabel = Ui.MakeText("SelectedKidBio", _bioCardRect, "", 17, ColSoftLabel, TextAnchor.UpperLeft);
            _kidBioLabel.rectTransform.anchorMin = Vector2.zero;
            _kidBioLabel.rectTransform.anchorMax = Vector2.one;
            _kidBioLabel.rectTransform.offsetMin = new Vector2(8f, 5f);
            _kidBioLabel.rectTransform.offsetMax = new Vector2(-8f, -40f);
            _kidBioLabel.lineSpacing = 0.92f;
            _kidBioLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _kidBioLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _kidBioLabel.resizeTextForBestFit = true;
            _kidBioLabel.resizeTextMinSize = 12;
            _kidBioLabel.resizeTextMaxSize = 17;

            // 10-Kid Scrollable Grid
            _rosterScrollRect = BuildScrollGrid(
                "RosterScroll",
                _leftPanel,
                "RosterGrid",
                columns: 3,
                spacing: new Vector2(6f, 6f),
                out _rosterViewportRect,
                out _rosterScroll,
                out _rosterScrollCue,
                out _rosterGridRect,
                out _rosterGrid);
            BuildKidTiles();

            // 3. Right Loadout Builder Panel (3 Slots + 14-Weapon Grid + Inspector)
            _rightPanel = Ui.Panel("LoadoutPanel", _workspace, new Vector2(0.43f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            MakePlate("LoadoutPlate", _rightPanel, ColPanelDark);

            _slotsRowRect = Ui.Panel("SlotsRow", _rightPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-16f, 100f));
            var slotsLayout = _slotsRowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotsLayout.spacing = 8f;
            slotsLayout.childAlignment = TextAnchor.MiddleCenter;
            slotsLayout.childControlWidth = true;
            slotsLayout.childControlHeight = true;
            slotsLayout.childForceExpandWidth = true;
            slotsLayout.childForceExpandHeight = true;
            BuildLoadoutSlots();

            _weaponsScrollRect = BuildScrollGrid(
                "WeaponsScroll",
                _rightPanel,
                "WeaponsGrid",
                columns: 3,
                spacing: new Vector2(8f, 8f),
                out _weaponsViewportRect,
                out _weaponsScroll,
                out _weaponsScrollCue,
                out _weaponsGridRect,
                out _weaponsGrid);
            BuildWeaponTiles();

            // Focused Weapon Inspector + Damage/Range/Cooldown Stat Bars
            _inspectorRect = Ui.Panel("WeaponInspector", _rightPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-16f, 144f));
            MakePlate("InspectorPlate", _inspectorRect, ColPanelInset);
            BuildWeaponInspector(
                _inspectorRect,
                out _inspectorIcon,
                out _inspectorIconFitter,
                out _inspectorFallbackBadge,
                out _inspectorFallbackCode,
                out _inspectorNameLabel,
                out _inspectorRoleChip,
                out _inspectorRoleLabel,
                out _inspectorGameLabel,
                out _inspectorDescLabel,
                out _powerMeter,
                out _rangeMeter,
                out _cooldownMeter);

            var hook = Root.gameObject.AddComponent<LifecycleHook>();
            hook.Owner = this;

            EnsureSubscribed();
            RefreshFromSession();
            RefreshResponsiveLayout();
            FocusDefaultControl();
        }

        void EnsureSubscribed()
        {
            if (_disposed || _subscribed) return;
            _subscribed = true;
            AppSession.Current.Changed += OnSessionChanged;
            SettingsStore.Current.Changed += OnSettingsChanged;
            ScreenFitMetrics.SimulatedViewportChanged += RefreshResponsiveLayout;
            if (_safeArea != null) _safeArea.Changed += OnSafeAreaChanged;
        }

        void OnSafeAreaChanged(SafeArea _) => RefreshResponsiveLayout();

        void OnSettingsChanged()
        {
            if (_disposed || Root == null) return;
            RefreshAllVisuals();
        }

        public void Dispose()
        {
            DisposeSubscriptions();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        public void SetVisible(bool visible)
        {
            if (Root == null) return;
            Root.gameObject.SetActive(visible);
            if (visible)
            {
                EnsureSubscribed();
                RefreshFromSession();
                RefreshResponsiveLayout();
                FocusDefaultControl();
            }
        }

        public bool IsCharacterTaken(string characterId) =>
            !string.IsNullOrEmpty(characterId) && _takenCharacters.Contains(characterId);

        public bool IsWeaponTaken(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && _takenWeapons.Contains(weaponId);

        /// <summary>Updates teammate-locked characters; if the local pick is now taken, steps to the next free kid.</summary>
        public void SetTakenCharacters(IEnumerable<string> characterIds)
        {
            _takenCharacters.Clear();
            if (characterIds != null)
            {
                foreach (string raw in characterIds)
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                        _takenCharacters.Add(raw.Trim());
                }
            }

            if (_takenCharacters.Contains(_selectedCharacterId) && _characters.Count > 0)
            {
                int currentIdx = IndexOfCharacter(_selectedCharacterId);
                if (currentIdx < 0) currentIdx = 0;
                for (int step = 1; step <= _characters.Count; step++)
                {
                    string candidate = _characters[(currentIdx + step) % _characters.Count].Id;
                    if (!_takenCharacters.Contains(candidate))
                    {
                        _selectedCharacterId = candidate;
                        SaveToSessionAndPrefs();
                        CharacterSelected?.Invoke(_selectedCharacterId);
                        break;
                    }
                }
            }

            RefreshAllVisuals();
        }

        /// <summary>Updates locked deck weapons; replaces any newly-taken equipped slot with the next available untaken superpower.</summary>
        public void SetTakenWeapons(IEnumerable<string> weaponIds)
        {
            _takenWeapons.Clear();
            if (weaponIds != null)
            {
                foreach (string raw in weaponIds)
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                        _takenWeapons.Add(raw.Trim());
                }
            }

            bool mutated = false;
            for (int i = _selectedWeapons.Count - 1; i >= 0; i--)
            {
                if (_takenWeapons.Contains(_selectedWeapons[i]))
                {
                    _selectedWeapons.RemoveAt(i);
                    mutated = true;
                }
            }

            if (mutated)
            {
                FillMissingSlotsFromUntaken();
                _activeSlotIndex = Mathf.Clamp(_selectedWeapons.Count < LoadoutSlotCount ? _selectedWeapons.Count : 0, 0, LoadoutSlotCount - 1);
                SaveToSessionAndPrefs();
                LoadoutChanged?.Invoke(_selectedWeapons);
            }

            RefreshAllVisuals();
        }

        public void RefreshFromSession()
        {
            string sessionKid = AppSession.Current.SelectedCharacter;
            if (string.IsNullOrEmpty(sessionKid) || !_characterById.ContainsKey(sessionKid))
                sessionKid = _characters.Count > 0 ? _characters[0].Id : AppSession.DefaultCharacter;

            if (_takenCharacters.Contains(sessionKid))
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    if (!_takenCharacters.Contains(_characters[i].Id))
                    {
                        sessionKid = _characters[i].Id;
                        break;
                    }
                }
            }
            _selectedCharacterId = sessionKid;

            _selectedWeapons.Clear();
            IReadOnlyList<string> saved = AppSession.Current.SelectedSuperpowers;
            if (saved != null)
            {
                for (int i = 0; i < saved.Count && _selectedWeapons.Count < LoadoutSlotCount; i++)
                {
                    string wid = saved[i];
                    if (!string.IsNullOrEmpty(wid)
                        && _weaponById.ContainsKey(wid)
                        && !_takenWeapons.Contains(wid)
                        && !_selectedWeapons.Contains(wid))
                    {
                        _selectedWeapons.Add(wid);
                    }
                }
            }

            if (_selectedWeapons.Count < LoadoutSlotCount)
                FillMissingSlotsFromUntaken();

            _activeSlotIndex = Mathf.Clamp(_activeSlotIndex, 0, LoadoutSlotCount - 1);
            if (string.IsNullOrEmpty(_focusedWeaponId) || !_weaponById.ContainsKey(_focusedWeaponId))
            {
                _focusedWeaponId = _selectedWeapons.Count > 0
                    ? _selectedWeapons[0]
                    : (_deckWeapons.Count > 0 ? _deckWeapons[0].Id : "");
            }

            RefreshAllVisuals();
        }

        public bool SelectCharacter(string characterId, bool saveImmediately = true)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return false;
            string clean = characterId.Trim();
            if (!_characterById.ContainsKey(clean) || _takenCharacters.Contains(clean))
            {
                Haptics.CancelTick();
                Audio?.PlayUiTap("deny");
                return false;
            }

            bool changed = !string.Equals(_selectedCharacterId, clean, StringComparison.Ordinal);
            _selectedCharacterId = clean;
            Haptics.LightTap();
            Audio?.PlayUiTap(false);

            if (saveImmediately)
                SaveToSessionAndPrefs();

            RefreshAllVisuals();
            if (changed)
                CharacterSelected?.Invoke(_selectedCharacterId);
            return true;
        }

        public void StepCharacterCarousel(int delta)
        {
            if (_characters.Count == 0 || delta == 0) return;
            int stepDir = delta > 0 ? 1 : -1;
            int start = IndexOfCharacter(_selectedCharacterId);
            if (start < 0) start = 0;

            for (int step = 1; step <= _characters.Count; step++)
            {
                int nextIdx = PositiveMod(start + step * stepDir, _characters.Count);
                string candidate = _characters[nextIdx].Id;
                if (!_takenCharacters.Contains(candidate))
                {
                    SelectCharacter(candidate, saveImmediately: true);
                    ScrollKidIntoView(nextIdx);
                    return;
                }
            }

            Haptics.CancelTick();
            Audio?.PlayUiTap("deny");
        }

        public void SelectSlot(int slotIndex)
        {
            _activeSlotIndex = Mathf.Clamp(slotIndex, 0, LoadoutSlotCount - 1);
            if (_activeSlotIndex < _selectedWeapons.Count)
                _focusedWeaponId = _selectedWeapons[_activeSlotIndex];
            Haptics.LightTap();
            Audio?.PlayUiTap(false);
            RefreshAllVisuals();
        }

        public void FocusWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || !_weaponById.ContainsKey(weaponId)) return;
            _focusedWeaponId = weaponId;
            RefreshInspector();
        }

        public bool TapWeapon(string weaponId, bool saveImmediately = true)
        {
            if (string.IsNullOrWhiteSpace(weaponId)) return false;
            string clean = weaponId.Trim();
            if (!_weaponById.ContainsKey(clean)) return false;

            _focusedWeaponId = clean;
            if (_takenWeapons.Contains(clean))
            {
                Haptics.CancelTick();
                Audio?.PlayUiTap("deny");
                RefreshAllVisuals();
                return false;
            }

            int existingSlot = _selectedWeapons.IndexOf(clean);
            if (existingSlot >= 0)
            {
                _selectedWeapons.RemoveAt(existingSlot);
                _activeSlotIndex = Mathf.Clamp(existingSlot, 0, LoadoutSlotCount - 1);
                Haptics.LightTap();
                Audio?.PlayUiTap("cancel");
            }
            else if (_selectedWeapons.Count < LoadoutSlotCount)
            {
                int insertAt = Mathf.Clamp(_activeSlotIndex, 0, _selectedWeapons.Count);
                _selectedWeapons.Insert(insertAt, clean);
                _activeSlotIndex = _selectedWeapons.Count < LoadoutSlotCount
                    ? _selectedWeapons.Count
                    : ((insertAt + 1) % LoadoutSlotCount);
                Haptics.LightTap();
                Audio?.PlayUiTap(false);
            }
            else
            {
                int replaceAt = Mathf.Clamp(_activeSlotIndex, 0, LoadoutSlotCount - 1);
                _selectedWeapons[replaceAt] = clean;
                _activeSlotIndex = (replaceAt + 1) % LoadoutSlotCount;
                Haptics.LightTap();
                Audio?.PlayUiTap(false);
            }

            if (saveImmediately && _selectedWeapons.Count > 0)
                SaveToSessionAndPrefs();

            RefreshAllVisuals();
            LoadoutChanged?.Invoke(_selectedWeapons);
            return true;
        }

        public void ApplySuggestedLoadout(bool saveImmediately = true)
        {
            List<string> suggested = BuildSuggestedTrio();
            if (suggested.Count == 0) return;

            _selectedWeapons.Clear();
            _selectedWeapons.AddRange(suggested);
            _activeSlotIndex = 0;
            _focusedWeaponId = _selectedWeapons[0];

            Haptics.LightTap();
            Audio?.PlayUiTap("ready");

            if (saveImmediately)
                SaveToSessionAndPrefs();

            RefreshAllVisuals();
            LoadoutChanged?.Invoke(_selectedWeapons);
        }

        public string[] GetSelectedWeapons() => _selectedWeapons.ToArray();

        /// <summary>Persists SelectedCharacter + SelectedSuperpowers into AppSession.Current and PlayerPrefs immediately.</summary>
        public bool SaveToSessionAndPrefs()
        {
            bool savedOk = true;
            _suppressSessionSync = true;
            try
            {
                if (!string.IsNullOrEmpty(_selectedCharacterId) && !_takenCharacters.Contains(_selectedCharacterId))
                    AppSession.Current.SelectedCharacter = _selectedCharacterId;

                if (_selectedWeapons.Count > 0 && !DealsZeroDirectDamage(_selectedWeapons))
                    savedOk = AppSession.Current.SetSuperpowers(_selectedWeapons);
                else if (_selectedWeapons.Count > 0)
                    savedOk = false;

                PlayerPrefs.SetString(AppSession.PlayerPrefsKey, AppSession.Current.ToJson());
                PlayerPrefs.Save();

                var client = AppSession.Current.Client;
                if (client != null && AppSession.Current.IsOnlineConnected && !string.IsNullOrEmpty(_selectedCharacterId))
                {
                    if (client.Stage == "pick" && _selectedWeapons.Count >= 2 && !DealsZeroDirectDamage(_selectedWeapons))
                    {
                        string third = _selectedWeapons.Count > 2 ? _selectedWeapons[2] : "";
                        client.PickWeapons(_selectedWeapons[0], _selectedWeapons[1], _selectedCharacterId, third);
                    }
                    else if (!client.Room.IsEmpty && client.Stage != "play")
                    {
                        client.SetCharacter(_selectedCharacterId);
                    }
                }
            }
            finally
            {
                _suppressSessionSync = false;
            }
            return savedOk;
        }

        public void ConfirmSelection()
        {
            if (_selectedWeapons.Count < LoadoutSlotCount)
                FillMissingSlotsFromUntaken();

            if (!CanConfirm)
            {
                Haptics.CancelTick();
                Audio?.PlayUiTap("deny");
                RefreshAllVisuals();
                return;
            }

            SaveToSessionAndPrefs();
            Haptics.LightTap();
            Audio?.PlayUiTap("confirm");
            RefreshAllVisuals();
            Confirmed?.Invoke();
            _onConfirmed?.Invoke();
        }

        public void GoBack() => HandleBackClicked();

        void HandleBackClicked()
        {
            if (_selectedWeapons.Count == LoadoutSlotCount)
                SaveToSessionAndPrefs();
            Haptics.LightTap();
            Audio?.PlayUiTap("back");
            BackRequested?.Invoke();
            _onBack?.Invoke();
        }

        void OnRootEnabled()
        {
            EnsureSubscribed();
            RefreshFromSession();
            RefreshResponsiveLayout();
            FocusDefaultControl();
        }

        void OnSessionChanged()
        {
            if (_suppressSessionSync || _disposed || Root == null) return;
            RefreshFromSession();
        }

        void DisposeSubscriptions()
        {
            _disposed = true;
            if (!_subscribed) return;
            _subscribed = false;
            AppSession.Current.Changed -= OnSessionChanged;
            SettingsStore.Current.Changed -= OnSettingsChanged;
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshResponsiveLayout;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void FocusDefaultControl()
        {
            if (EventSystem.current == null) return;
            for (int i = 0; i < _kidTiles.Count; i++)
            {
                if (_kidTiles[i].Id == _selectedCharacterId && _kidTiles[i].Button != null && _kidTiles[i].Button.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(_kidTiles[i].Button.gameObject);
                    return;
                }
            }
            if (_confirmButton != null)
                EventSystem.current.SetSelectedGameObject(_confirmButton.gameObject);
        }

        public void RefreshResponsiveLayout()
        {
            if (_disposed || Root == null || _workspace == null) return;
            if (_canvas == null)
                _canvas = Root.GetComponentInParent<Canvas>();

            _safeArea?.Apply();

            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float outerPadX = 8f;
            float outerPadY = 6f;
            float usableW = Mathf.Clamp(safeSize.x - outerPadX * 2f, 560f, 1380f);
            float usableH = Mathf.Max(420f, safeSize.y - outerPadY * 2f);

            _workspace.sizeDelta = new Vector2(usableW, 0f);
            _workspace.offsetMin = new Vector2(-usableW * 0.5f, outerPadY);
            _workspace.offsetMax = new Vector2(usableW * 0.5f, -outerPadY);

            float minTouch = ScreenFitMetrics.ClampTouchSize(52f, ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Clamp(minTouch + 8f, 98f, 108f);
            float btnH = Mathf.Max(minTouch, headerH - 8f);
            if (btnH + 4f > headerH)
                headerH = btnH + 4f;
            _headerBar.sizeDelta = new Vector2(0f, headerH);

            float backW = Mathf.Max(minTouch, 120f);
            float suggestW = Mathf.Max(minTouch, 122f);
            float confirmW = Mathf.Max(minTouch, 144f);

            _backBtnRect.anchoredPosition = new Vector2(6f, 0f);
            _backBtnRect.sizeDelta = new Vector2(backW, btnH);

            _confirmBtnRect.anchoredPosition = new Vector2(-6f, 0f);
            _confirmBtnRect.sizeDelta = new Vector2(confirmW, btnH);

            _suggestBtnRect.anchoredPosition = new Vector2(-(6f + confirmW + 8f), 0f);
            _suggestBtnRect.sizeDelta = new Vector2(suggestW, btnH);

            _headerTextRect.offsetMin = new Vector2(backW + 12f, 0f);
            _headerTextRect.offsetMax = new Vector2(-(suggestW + confirmW + 22f), 0f);

            float bodyTopInset = headerH + 6f;
            float bodyH = Mathf.Max(300f, usableH - bodyTopInset);

            _leftPanel.offsetMin = Vector2.zero;
            _leftPanel.offsetMax = new Vector2(0f, -bodyTopInset);
            _rightPanel.offsetMin = Vector2.zero;
            _rightPanel.offsetMax = new Vector2(0f, -bodyTopInset);

            // Left panel vertical stack: Spotlight row + Bio card + 10-kid ScrollGrid
            float leftW = usableW * 0.415f;
            float innerLeftW = Mathf.Max(260f, leftW - 16f);
            float navSide = Mathf.Max(minTouch, Mathf.Min(98f, innerLeftW * 0.25f));
            float spotH = Mathf.Clamp(Mathf.Max(minTouch + 4f, bodyH * 0.195f), minTouch + 4f, 124f);
            float maxSpotW = Mathf.Max(88f, innerLeftW - (navSide * 2f + 16f));
            float spotCardW = Mathf.Clamp(Mathf.Max(92f, spotH - 4f), 88f, Mathf.Min(maxSpotW, 132f));

            _spotlightRow.anchoredPosition = new Vector2(0f, -6f);
            _spotlightRow.sizeDelta = new Vector2(-16f, spotH);
            _prevKidRect.anchoredPosition = Vector2.zero;
            _prevKidRect.sizeDelta = new Vector2(navSide, Mathf.Max(minTouch, spotH - 4f));
            _nextKidRect.anchoredPosition = Vector2.zero;
            _nextKidRect.sizeDelta = new Vector2(navSide, Mathf.Max(minTouch, spotH - 4f));
            _spotlightCardRect.sizeDelta = new Vector2(spotCardW, spotH);

            float bioTop = 6f + spotH + 6f;
            float bioH = innerLeftW < 430f
                ? Mathf.Clamp(bodyH * 0.265f, 146f, 158f)
                : Mathf.Clamp(bodyH * 0.24f, 134f, 148f);
            _bioCardRect.anchoredPosition = new Vector2(0f, -bioTop);
            _bioCardRect.sizeDelta = new Vector2(-16f, bioH);

            float rosterTop = bioTop + bioH + 6f;
            _rosterScrollRect.offsetMin = new Vector2(8f, 6f);
            _rosterScrollRect.offsetMax = new Vector2(-8f, -rosterTop);
            float rosterContainerH = Mathf.Max(minTouch + 16f, bodyH - rosterTop - 6f);

            int kidCols = innerLeftW >= 460f ? 4 : 3;
            float kidGap = 6f;
            if (kidCols == 4 && minTouch * 4f + kidGap * 3f + 8f > innerLeftW)
                kidCols = 3;
            if (kidCols == 3 && minTouch * 3f + kidGap * 2f + 8f > innerLeftW)
                kidCols = 2;

            float kidCellW = Mathf.Max(minTouch, Mathf.Floor((innerLeftW - 8f - kidGap * (kidCols - 1)) / kidCols));
            int kidRows = Mathf.CeilToInt(_kidTiles.Count / (float)kidCols);
            int kidPadY = _rosterGrid.padding.top + _rosterGrid.padding.bottom;

            // Check whether all rows fit without scroll cue; otherwise reserve dedicated bottom hint strip and fit whole rows cleanly
            float minKidCellH = Mathf.Max(minTouch, 96f);
            float fullRosterNeededH = kidRows * minKidCellH + Mathf.Max(0, kidRows - 1) * kidGap + kidPadY;
            bool rosterOverflows = fullRosterNeededH > rosterContainerH + 0.5f;
            float rosterFooterReserve = rosterOverflows ? ScrollHintFooterHeight : 0f;
            float rosterViewportH = Mathf.Max(minTouch, rosterContainerH - rosterFooterReserve);

            int visibleKidRows = rosterOverflows
                ? Mathf.Clamp(Mathf.FloorToInt((rosterViewportH - kidPadY + kidGap) / (minKidCellH + kidGap)), 1, Mathf.Max(1, kidRows - 1))
                : kidRows;
            float fittedKidCellH = visibleKidRows > 0
                ? Mathf.Floor((rosterViewportH - kidPadY - Mathf.Max(0, visibleKidRows - 1) * kidGap) / visibleKidRows)
                : minKidCellH;
            float kidCellH = Mathf.Clamp(Mathf.Max(minKidCellH, fittedKidCellH), minKidCellH, 114f);

            _rosterViewportRect.offsetMin = new Vector2(0f, rosterFooterReserve);
            _rosterViewportRect.offsetMax = Vector2.zero;
            _rosterGrid.spacing = new Vector2(kidGap, kidGap);
            _rosterGrid.constraintCount = kidCols;
            _rosterGrid.cellSize = new Vector2(kidCellW, kidCellH);
            _rosterGridRect.sizeDelta = new Vector2(0f, kidRows * kidCellH + Mathf.Max(0, kidRows - 1) * kidGap + kidPadY);

            // Right panel vertical stack: 3 Slots + 14-Weapon ScrollGrid + Inspector
            float rightW = usableW * 0.57f;
            float innerRightW = Mathf.Max(320f, rightW - 16f);
            float slotsH = Mathf.Max(minTouch, Mathf.Clamp(bodyH * 0.175f, 96f, 106f));
            _slotsRowRect.anchoredPosition = new Vector2(0f, -6f);
            _slotsRowRect.sizeDelta = new Vector2(-16f, slotsH);

            float inspectorH = Mathf.Clamp(bodyH * 0.265f, 144f, 162f);
            _inspectorRect.anchoredPosition = new Vector2(0f, 6f);
            _inspectorRect.sizeDelta = new Vector2(-16f, inspectorH);

            float slotCellW = (innerRightW - 16f) / LoadoutSlotCount;
            bool compactSlot = slotCellW < 180f;
            float slotIconSide = compactSlot ? 40f : 52f;
            float slotIconX = compactSlot ? 6f : 8f;
            float slotTextLeft = compactSlot ? 50f : 64f;
            float slotTextRight = compactSlot ? -5f : -6f;
            for (int i = 0; i < LoadoutSlotCount; i++)
            {
                SlotRefs slot = _slots[i];
                if (slot == null) continue;
                if (slot.IconHolder != null)
                {
                    slot.IconHolder.anchoredPosition = new Vector2(slotIconX, 0f);
                    slot.IconHolder.sizeDelta = new Vector2(slotIconSide, slotIconSide);
                }
                if (slot.SlotKeyLabel != null)
                {
                    slot.SlotKeyLabel.rectTransform.offsetMin = new Vector2(slotTextLeft, 0f);
                    slot.SlotKeyLabel.rectTransform.offsetMax = new Vector2(slotTextRight, 0f);
                }
                if (slot.WeaponNameLabel != null)
                {
                    slot.WeaponNameLabel.rectTransform.offsetMin = new Vector2(slotTextLeft, 0f);
                    slot.WeaponNameLabel.rectTransform.offsetMax = new Vector2(slotTextRight, 0f);
                }
                if (slot.RoleLabel != null)
                {
                    slot.RoleLabel.rectTransform.offsetMin = new Vector2(slotTextLeft, 0f);
                    slot.RoleLabel.rectTransform.offsetMax = new Vector2(slotTextRight, 0f);
                }
            }

            float weaponsTop = 6f + slotsH + 6f;
            float weaponsBottom = 6f + inspectorH + 6f;
            _weaponsScrollRect.offsetMin = new Vector2(8f, weaponsBottom);
            _weaponsScrollRect.offsetMax = new Vector2(-8f, -weaponsTop);
            float weaponsContainerH = Mathf.Max(minTouch + 16f, bodyH - weaponsTop - weaponsBottom);

            int weapCols = 3;
            float weapGap = 8f;
            if (minTouch * 3f + weapGap * 2f + 8f > innerRightW)
                weapCols = 2;

            float rawWeapW = Mathf.Floor((innerRightW - 8f - weapGap * (weapCols - 1)) / weapCols);
            float weapCellW = Mathf.Clamp(rawWeapW, minTouch, 228f);
            bool compactWeap = weapCellW < 175f;
            float weapIconSide = compactWeap ? 40f : 48f;
            float weapIconInset = compactWeap ? 5f : 6f;
            float weapTextLeft = compactWeap ? 48f : 58f;
            float weapNameRight = compactWeap ? -26f : -28f;
            for (int i = 0; i < _weaponTiles.Count; i++)
            {
                WeaponTileRefs tile = _weaponTiles[i];
                if (tile.IconHolder != null)
                {
                    tile.IconHolder.anchoredPosition = new Vector2(weapIconInset, -weapIconInset);
                    tile.IconHolder.sizeDelta = new Vector2(weapIconSide, weapIconSide);
                }
                if (tile.NameLabel != null)
                {
                    tile.NameLabel.rectTransform.offsetMin = new Vector2(weapTextLeft, 0f);
                    tile.NameLabel.rectTransform.offsetMax = new Vector2(weapNameRight, compactWeap ? -4f : -5f);
                }
                if (tile.RoleLabel != null)
                {
                    tile.RoleLabel.rectTransform.offsetMin = new Vector2(weapTextLeft, 0f);
                    tile.RoleLabel.rectTransform.offsetMax = new Vector2(compactWeap ? -6f : -8f, 0f);
                }
            }
            int weapRows = Mathf.CeilToInt(_weaponTiles.Count / (float)weapCols);
            int weapPadY = _weaponsGrid.padding.top + _weaponsGrid.padding.bottom;

            float minWeapCellH = Mathf.Max(minTouch, 114f);
            float fullWeapNeededH = weapRows * minWeapCellH + Mathf.Max(0, weapRows - 1) * weapGap + weapPadY;
            bool weaponsOverflow = fullWeapNeededH > weaponsContainerH + 0.5f;
            float weaponsFooterReserve = weaponsOverflow ? ScrollHintFooterHeight : 0f;
            float weaponsViewportH = Mathf.Max(minTouch, weaponsContainerH - weaponsFooterReserve);

            int visibleWeapRows = weaponsOverflow
                ? Mathf.Clamp(Mathf.FloorToInt((weaponsViewportH - weapPadY + weapGap) / (minWeapCellH + weapGap)), 1, Mathf.Max(1, weapRows - 1))
                : weapRows;
            float exactWeapRowH = visibleWeapRows > 0
                ? Mathf.Floor((weaponsViewportH - weapPadY - Mathf.Max(0, visibleWeapRows - 1) * weapGap) / visibleWeapRows)
                : minWeapCellH;
            float weapCellH = Mathf.Clamp(Mathf.Max(minWeapCellH, exactWeapRowH), minWeapCellH, 134f);

            _weaponsViewportRect.offsetMin = new Vector2(0f, weaponsFooterReserve);
            _weaponsViewportRect.offsetMax = Vector2.zero;
            _weaponsGrid.spacing = new Vector2(weapGap, weapGap);
            _weaponsGrid.constraintCount = weapCols;
            _weaponsGrid.cellSize = new Vector2(weapCellW, weapCellH);
            _weaponsGridRect.sizeDelta = new Vector2(0f, weapRows * weapCellH + Mathf.Max(0, weapRows - 1) * weapGap + weapPadY);

            if (Root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(Root);
            UpdateScrollCue(_rosterScroll, _rosterGridRect, _rosterScrollCue);
            UpdateScrollCue(_weaponsScroll, _weaponsGridRect, _weaponsScrollCue);
        }

        void RefreshAllVisuals()
        {
            RefreshHeaderHintAndConfirm();
            RefreshSpotlightAndRoster();
            RefreshSlotsAndWeapons();
            RefreshInspector();
        }

        void RefreshHeaderHintAndConfirm()
        {
            bool ready = CanConfirm;
            if (_confirmButton != null)
                _confirmButton.interactable = ready;
            if (_confirmBtnImage != null)
                _confirmBtnImage.color = ready ? ColReady : ColDisabledBtn;
            if (_confirmBtnLabel != null)
            {
                _confirmBtnLabel.color = ready ? ColInk : ColMutedText;
                Outline ol = _confirmBtnLabel.GetComponent<Outline>();
                if (ol != null) ol.enabled = false;
            }

            if (_subtitleLabel == null) return;
            if (_selectedWeapons.Count < LoadoutSlotCount)
            {
                int remaining = LoadoutSlotCount - _selectedWeapons.Count;
                _subtitleLabel.text = remaining == 1
                    ? "Choose 1 more superpower to complete your 3-slot kit"
                    : $"Choose {remaining} more superpowers ({_selectedWeapons.Count}/{LoadoutSlotCount} equipped)";
                _subtitleLabel.color = ColGold;
            }
            else if (DealsZeroDirectDamage(_selectedWeapons))
            {
                _subtitleLabel.text = "Pick at least 1 attack superpower that deals damage (or tap SUGGEST)";
                _subtitleLabel.color = new Color(1.0f, 0.72f, 0.42f, 1f);
            }
            else
            {
                string slotTag = SlotBadges[Mathf.Clamp(_activeSlotIndex, 0, SlotBadges.Length - 1)];
                _subtitleLabel.text = $"3/3 Superpowers Ready \u00B7 Tap any card to swap {slotTag}";
                _subtitleLabel.color = ColCosmeticText;
            }
        }

        void RefreshSpotlightAndRoster()
        {
            int idx = IndexOfCharacter(_selectedCharacterId);
            CharacterCardData active = idx >= 0 ? _characters[idx] : (_characters.Count > 0 ? _characters[0] : null);
            if (active != null)
            {
                bool fil = string.Equals(SettingsStore.Current.Language, "fil", StringComparison.OrdinalIgnoreCase);
                _kidNameLabel.text = active.DisplayName.ToUpperInvariant();
                _kidBioLabel.text = active.Description;
                _cosmeticNoteLabel.text = fil ? "100% Porma · Pantay Stats" : "100% Cosmetic · Same Stats";
                _kidIndexLabel.text = $"{(idx >= 0 ? idx + 1 : 1)}/{_characters.Count}";
                _spotlightFrame.color = Color.Lerp(active.Tint, ColGold, 0.45f);
                if (_spotlightStage != null)
                    _spotlightStage.color = Color.Lerp(ColPanelInset, active.Tint, 0.30f);

                bool hasPortrait = active.PortraitTexture != null;
                Texture2D heroTex = hasPortrait ? active.PortraitTexture : active.CardTexture;
                _spotlightPortrait.texture = heroTex;
                _spotlightPortrait.uvRect = hasPortrait ? active.PortraitUvRect : new Rect(0f, 0.3487f, 1f, 0.6513f);
                _spotlightPortrait.gameObject.SetActive(heroTex != null);
                _spotlightFallbackDisc.gameObject.SetActive(heroTex == null);
                _spotlightFallbackDisc.color = active.Tint;
                _spotlightInitials.text = InitialsOf(active.DisplayName);

                for (int s = 0; s < _swatchDots.Length; s++)
                {
                    bool hasSwatch = active.OutfitSwatches != null && s < active.OutfitSwatches.Length;
                    _swatchDots[s].gameObject.SetActive(hasSwatch);
                    if (hasSwatch) _swatchDots[s].color = active.OutfitSwatches[s];
                }
            }

            for (int i = 0; i < _kidTiles.Count; i++)
            {
                KidTileRefs tile = _kidTiles[i];
                bool isPicked = string.Equals(tile.Id, _selectedCharacterId, StringComparison.Ordinal);
                bool isTaken = _takenCharacters.Contains(tile.Id);
                tile.Button.interactable = !isTaken;
                tile.Frame.color = isTaken ? ColCardTaken : (isPicked ? ColCardSelected : ColCardIdle);
                tile.Border.color = isTaken
                    ? new Color(0.35f, 0.35f, 0.42f, 0.6f)
                    : (isPicked ? ColGold : new Color(1f, 1f, 1f, 0.14f));
                if (tile.Portrait != null)
                    tile.Portrait.color = isTaken ? new Color(0.45f, 0.45f, 0.50f, 0.80f) : Color.white;
                tile.NameLabel.color = isTaken ? ColMutedText : (isPicked ? ColGold : Ui.Cream);
                tile.SelectedBadge.SetActive(isPicked && !isTaken);
                tile.TakenBadge.SetActive(isTaken);
            }
        }

        void RefreshSlotsAndWeapons()
        {
            for (int i = 0; i < LoadoutSlotCount; i++)
            {
                SlotRefs slot = _slots[i];
                bool isTarget = i == _activeSlotIndex;
                string wid = i < _selectedWeapons.Count ? _selectedWeapons[i] : "";
                bool hasWeapon = !string.IsNullOrEmpty(wid) && _weaponById.TryGetValue(wid, out _);
                slot.ActiveRing.color = isTarget ? ColGold : new Color(1f, 1f, 1f, 0.16f);
                slot.Frame.color = isTarget ? new Color(0.22f, 0.20f, 0.12f, 0.96f) : ColPanelInset;
                slot.SlotKeyLabel.text = SlotBadges[i];

                if (hasWeapon && _weaponById.TryGetValue(wid, out WeaponInfo equipped))
                {
                    Texture2D tex = GetWeaponTexture(wid);
                    slot.Icon.texture = tex;
                    if (slot.IconFitter != null)
                        slot.IconFitter.aspectRatio = TextureAspectOrSquare(tex);
                    slot.Icon.gameObject.SetActive(tex != null);
                    slot.FallbackBadge.gameObject.SetActive(tex == null);
                    slot.FallbackBadge.color = RoleColor(equipped.Kind);
                    slot.FallbackCode.text = ShortBadgeCode(equipped);
                    slot.WeaponNameLabel.text = ShortWeaponTitle(equipped);
                    slot.WeaponNameLabel.color = Ui.Cream;
                    slot.RoleLabel.text = RoleName(equipped.Kind).ToUpperInvariant();
                    slot.RoleLabel.color = RoleTextColor(equipped.Kind);
                }
                else
                {
                    slot.Icon.gameObject.SetActive(false);
                    slot.FallbackBadge.gameObject.SetActive(true);
                    slot.FallbackBadge.color = ColDisabledBtn;
                    slot.FallbackCode.text = "+";
                    slot.WeaponNameLabel.text = "Tap Superpower";
                    slot.WeaponNameLabel.color = ColMutedText;
                    slot.RoleLabel.text = "EMPTY";
                    slot.RoleLabel.color = ColMutedText;
                }
            }

            for (int i = 0; i < _weaponTiles.Count; i++)
            {
                WeaponTileRefs tile = _weaponTiles[i];
                int slotIdx = _selectedWeapons.IndexOf(tile.Id);
                bool isEquipped = slotIdx >= 0;
                bool isFocused = string.Equals(tile.Id, _focusedWeaponId, StringComparison.Ordinal);
                bool isTaken = _takenWeapons.Contains(tile.Id);

                tile.Button.interactable = !isTaken;
                tile.Frame.color = isTaken
                    ? ColCardTaken
                    : (isEquipped ? ColCardSelected : (isFocused ? new Color(0.19f, 0.24f, 0.42f, 0.98f) : ColCardIdle));
                tile.Border.color = isTaken
                    ? new Color(0.35f, 0.35f, 0.42f, 0.55f)
                    : (isEquipped ? ColGold : (isFocused ? ColBarRange : new Color(1f, 1f, 1f, 0.14f)));

                if (tile.Icon != null)
                    tile.Icon.color = isTaken ? new Color(0.45f, 0.45f, 0.50f, 0.78f) : Color.white;
                tile.NameLabel.color = isTaken ? ColMutedText : (isEquipped ? ColGold : Ui.Cream);
                tile.RoleLabel.color = isTaken ? ColMutedText : RoleTextColor(tile.Info.Kind);
                tile.EquippedBadge.SetActive(isEquipped && !isTaken);
                if (isEquipped)
                    tile.EquippedSlotText.text = (slotIdx + 1).ToString(CultureInfo.InvariantCulture);
                tile.TakenBadge.SetActive(isTaken);
            }
        }

        void RefreshInspector()
        {
            if (string.IsNullOrEmpty(_focusedWeaponId) || !_weaponById.TryGetValue(_focusedWeaponId, out WeaponInfo info))
            {
                if (_deckWeapons.Count == 0) return;
                info = _deckWeapons[0];
                _focusedWeaponId = info.Id;
            }

            Texture2D tex = GetWeaponTexture(info.Id);
            _inspectorIcon.texture = tex;
            if (_inspectorIconFitter != null)
                _inspectorIconFitter.aspectRatio = TextureAspectOrSquare(tex);
            _inspectorIcon.gameObject.SetActive(tex != null);
            _inspectorFallbackBadge.gameObject.SetActive(tex == null);
            _inspectorFallbackBadge.color = RoleColor(info.Kind);
            _inspectorFallbackCode.text = ShortBadgeCode(info);

            _inspectorNameLabel.text = info.DisplayName;
            _inspectorRoleChip.color = RoleColor(info.Kind);
            _inspectorRoleLabel.text = RoleName(info.Kind).ToUpperInvariant();
            _inspectorGameLabel.text = string.IsNullOrEmpty(info.StreetGame) ? "" : ("Larong Kalye \u00B7 " + info.StreetGame);
            _inspectorDescLabel.text = info.Description;

            if (info.Heal > 0)
            {
                SetStatMeter(_powerMeter, "Heal", info.Heal / Mathf.Max(1f, _maxPower), ColBarHeal, $"{info.Heal} HP");
            }
            else if (info.Damage > 0)
            {
                SetStatMeter(_powerMeter, "Damage", info.Damage / Mathf.Max(1f, _maxPower), ColBarDamage, $"{info.Damage} DMG");
            }
            else
            {
                SetStatMeter(_powerMeter, "Effect", 0.14f, RoleTextColor(info.Kind), RoleName(info.Kind));
            }

            float rangeFrac = info.MaxRange > 0f ? Mathf.Clamp01(info.MaxRange / Mathf.Max(1f, _maxRange)) : 0.10f;
            string rangeText = info.MaxRange > 0f ? FormatNumber(info.MaxRange) + "m" : "Self";
            SetStatMeter(_rangeMeter, "Range", rangeFrac, ColBarRange, rangeText);

            float cdFrac = Mathf.Clamp01(info.Cooldown / Mathf.Max(0.1f, _maxCooldown));
            SetStatMeter(_cooldownMeter, "Cooldown", cdFrac, ColBarCooldown, FormatNumber(info.Cooldown) + "s");
        }

        static void SetStatMeter(StatMeterRefs meter, string caption, float fill01, Color color, string valueText)
        {
            meter.CaptionLabel.text = caption;
            meter.FillImage.color = color;
            meter.FillImage.rectTransform.anchorMax = new Vector2(Mathf.Clamp(fill01, 0.05f, 1f), 1f);
            meter.ValueLabel.text = valueText;
            meter.ValueLabel.color = color;
        }

        void ScrollKidIntoView(int index)
        {
            if (_rosterScroll == null || _rosterGrid == null || _kidTiles.Count <= _rosterGrid.constraintCount) return;
            int cols = Mathf.Max(1, _rosterGrid.constraintCount);
            int totalRows = Mathf.CeilToInt(_kidTiles.Count / (float)cols);
            if (totalRows <= 1) return;
            int row = index / cols;
            _rosterScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(row / (float)(totalRows - 1));
        }

        void LoadCatalogs()
        {
            _characters.Clear();
            _characterById.Clear();
            _deckWeapons.Clear();
            _weaponById.Clear();
            _simDeckWeapons.Clear();
            _simWeaponById.Clear();
            _weaponIcons.Clear();

            Dictionary<string, object> gameRules = TryProps("rules__game_rules");

            // 1. Load 10 Filipino roster kids
            var kidRefs = gameRules != null && gameRules.TryGetValue("characters", out object rawChars)
                ? rawChars as List<object>
                : null;

            if (kidRefs != null && kidRefs.Count > 0)
            {
                for (int i = 0; i < kidRefs.Count; i++)
                {
                    string file = DataSource.NameOfRef(kidRefs[i]);
                    AddCharacterFromProps(TryProps(file), AppSession.DefaultRoster[i % AppSession.DefaultRoster.Length]);
                }
            }
            else
            {
                for (int i = 0; i < AppSession.DefaultRoster.Length; i++)
                {
                    string fallbackId = AppSession.DefaultRoster[i];
                    AddCharacterFromProps(TryProps("characters__" + fallbackId), fallbackId);
                }
            }

            // 2. Load all 14 deck superpowers
            var weaponRefs = gameRules != null && gameRules.TryGetValue("weapons", out object rawWeaps)
                ? rawWeaps as List<object>
                : null;

            if (weaponRefs != null && weaponRefs.Count > 0)
            {
                for (int i = 0; i < weaponRefs.Count; i++)
                {
                    string file = DataSource.NameOfRef(weaponRefs[i]);
                    AddWeaponFromProps(TryProps(file), FallbackDeckWeaponIds[i % FallbackDeckWeaponIds.Length]);
                }
            }
            else
            {
                for (int i = 0; i < FallbackDeckWeaponIds.Length; i++)
                {
                    string fallbackId = FallbackDeckWeaponIds[i];
                    AddWeaponFromProps(TryProps("weapons__" + fallbackId), fallbackId);
                }
            }

            _maxPower = 1f;
            _maxRange = 1f;
            _maxCooldown = 1f;
            for (int i = 0; i < _deckWeapons.Count; i++)
            {
                WeaponInfo w = _deckWeapons[i];
                _maxPower = Mathf.Max(_maxPower, Mathf.Max(w.Damage, w.Heal));
                _maxRange = Mathf.Max(_maxRange, w.MaxRange);
                _maxCooldown = Mathf.Max(_maxCooldown, w.Cooldown);
            }
        }

        Dictionary<string, object> TryProps(string name)
        {
            if (_data == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                return _data.Props(name);
            }
            catch
            {
                return null;
            }
        }

        void AddCharacterFromProps(Dictionary<string, object> props, string fallbackId)
        {
            string id = ReadStr(props, "id", fallbackId);
            if (string.IsNullOrEmpty(id) || _characterById.ContainsKey(id)) return;

            Color tint = ReadColor(props, "tint", new Color(0.85f, 0.52f, 0.24f, 1f));
            Color accent = ReadColor(props, "top_accent", ColGold);
            Color hair = ReadColor(props, "hair_color", new Color(0.16f, 0.11f, 0.08f, 1f));
            Color bottom = ReadColor(props, "bottom_color", new Color(0.20f, 0.28f, 0.46f, 1f));
            Color shoe = ReadColor(props, "shoe_color", new Color(0.22f, 0.22f, 0.26f, 1f));

            Texture2D portrait = LoadSquareKidPortrait(id, out Rect portraitUv);
            Texture2D card = LoadFirstTexture(
                "ui/character_cards/" + id,
                "portraits/cards/" + id,
                "portraits/" + id,
                "portraits/faces/" + id);

            var entry = new CharacterCardData
            {
                Id = id,
                DisplayName = ReadStr(props, "display_name", CapitalizeId(id)),
                Description = ReadStr(props, "description", "Barangay street contender ready for Agawang Base."),
                Tint = tint,
                Accent = accent,
                OutfitSwatches = new[] { hair, tint, accent, bottom, shoe },
                PortraitTexture = portrait,
                PortraitUvRect = portraitUv,
                CardTexture = card,
            };
            _characters.Add(entry);
            _characterById[id] = entry;
        }

        void AddWeaponFromProps(Dictionary<string, object> props, string fallbackId)
        {
            string id = ReadStr(props, "id", fallbackId);
            if (string.IsNullOrEmpty(id) || _weaponById.ContainsKey(id)) return;

            var info = new WeaponInfo
            {
                Id = id,
                DisplayName = ReadStr(props, "display_name", CapitalizeId(id)),
                ShortName = ReadStr(props, "short_name", CapitalizeId(id)),
                Description = ReadStr(props, "description", ""),
                StreetGame = ReadStr(props, "street_game", ""),
                IconPath = "icons/art/" + id,
                Look = ReadStr(props, "look", ""),
                Kind = (WeaponRoleKind)ReadInt(props, "kind", 0),
                Shape = (WeaponShapeKind)ReadInt(props, "shape", 0),
                ElixirCost = ReadInt(props, "elixir_cost", 0),
                Damage = ReadInt(props, "damage", 0),
                Heal = ReadInt(props, "heal", 0),
                Count = ReadInt(props, "count", 1),
                CardPower = ReadInt(props, "card_power", 0),
                Cooldown = ReadFloat(props, "cooldown", 6f),
                MaxRange = ReadFloat(props, "max_range", 0f),
                Radius = ReadFloat(props, "radius", 0f),
                AngleDegrees = ReadFloat(props, "angle_degrees", 0f),
                Delay = ReadFloat(props, "delay", 0f),
                Speed = ReadFloat(props, "speed", 0f),
                Duration = ReadFloat(props, "duration", 0f),
                ArmTime = ReadFloat(props, "arm_time", 0f),
                TriggerRadius = ReadFloat(props, "trigger_radius", 0f),
                Width = ReadFloat(props, "width", 0f),
                Offset = ReadFloat(props, "offset", 0f),
                ProjectileRadius = ReadFloat(props, "projectile_radius", 0f),
                Effect = (StatusKind)ReadInt(props, "effect", 0),
                EffectDuration = ReadFloat(props, "effect_duration", 0f),
                EffectMagnitude = ReadFloat(props, "effect_magnitude", 0f),
                CastLock = ReadFloat(props, "cast_lock", 0f),
            };

            Texture2D icon = LoadFirstTexture(
                "icons/art/" + id,
                "ui/hud_icons/" + id,
                "ui/skill_cards/" + id,
                "ui/weapons/" + id);
            if (icon != null)
                _weaponIcons[id] = icon;

            _deckWeapons.Add(info);
            _weaponById[id] = info;

            var simDef = new Kalyeah.Sim.WeaponDef
            {
                Id = id,
                DisplayName = info.DisplayName,
                ShortName = info.ShortName,
                Description = info.Description,
                StreetGame = info.StreetGame,
                Kind = (Kalyeah.Sim.WeaponDef.WeaponKind)Mathf.Clamp((int)info.Kind, 0, 3),
                Shape = (Kalyeah.Sim.WeaponDef.WeaponShape)Mathf.Clamp((int)info.Shape, 0, 10),
                Cooldown = info.Cooldown,
                ElixirCost = info.ElixirCost,
                MaxRange = info.MaxRange,
                Damage = info.Damage,
                Heal = info.Heal,
                Radius = info.Radius,
                AngleDegrees = info.AngleDegrees,
                Delay = info.Delay,
                Speed = info.Speed,
                Count = info.Count,
                Duration = info.Duration,
                ArmTime = info.ArmTime,
                TriggerRadius = info.TriggerRadius,
                Width = info.Width,
                Offset = info.Offset,
                ProjectileRadius = info.ProjectileRadius,
                Look = info.Look,
                CastLock = info.CastLock,
                CardPower = info.CardPower,
            };
            _simDeckWeapons.Add(simDef);
            _simWeaponById[id] = simDef;
        }

        Texture2D GetWeaponTexture(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && _weaponIcons.TryGetValue(weaponId, out Texture2D tex) ? tex : null;

        void BuildKidTiles()
        {
            _kidTiles.Clear();
            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterCardData kid = _characters[i];
                string kidId = kid.Id;

                var cellGo = new GameObject("Kid_" + kidId, typeof(RectTransform), typeof(Image), typeof(Button));
                cellGo.transform.SetParent(_rosterGridRect, false);
                var frame = cellGo.GetComponent<Image>();
                frame.sprite = RoundedPlate();
                frame.type = Image.Type.Sliced;
                frame.color = ColCardIdle;

                var btn = cellGo.GetComponent<Button>();
                btn.targetGraphic = frame;
                ConfigureButtonColors(btn);
                btn.onClick.AddListener(() => SelectCharacter(kidId, saveImmediately: true));

                Image border = MakePlate("Border", cellGo.transform, new Color(1f, 1f, 1f, 0.14f));
                border.raycastTarget = false;
                Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

                Image inner = MakePlate("Inner", cellGo.transform, Color.Lerp(ColPanelInset, kid.Tint, 0.22f));
                Stretch(inner.rectTransform, 3f, 26f, -3f, -3f);

                Image fallbackDisc = Ui.MakeImage("FallbackDisc", inner.transform, kid.Tint, round: true);
                fallbackDisc.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                fallbackDisc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                fallbackDisc.rectTransform.sizeDelta = new Vector2(50f, 50f);
                Text initials = Ui.MakeText("Initials", fallbackDisc.transform, InitialsOf(kid.DisplayName), 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(initials.rectTransform, 0f, 0f, 0f, 0f);

                RectTransform portraitPad = Ui.Fill("PortraitPad", inner.transform);
                Stretch(portraitPad, 2f, 2f, -2f, -2f);

                RawImage portrait = MakeSquareRawImage("Portrait", portraitPad);
                portrait.texture = kid.PortraitTexture;
                portrait.uvRect = kid.PortraitUvRect;
                portrait.gameObject.SetActive(kid.PortraitTexture != null);
                fallbackDisc.gameObject.SetActive(kid.PortraitTexture == null);

                Text nameLabel = Ui.MakeText("Name", cellGo.transform, kid.DisplayName, 17, Ui.Cream, TextAnchor.MiddleCenter);
                nameLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
                nameLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
                nameLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                nameLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                nameLabel.rectTransform.sizeDelta = new Vector2(-6f, 23f);
                nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
                nameLabel.resizeTextForBestFit = true;
                nameLabel.resizeTextMinSize = 12;
                nameLabel.resizeTextMaxSize = 17;

                RectTransform pickedBadge = Ui.Panel("SelectedBadge", cellGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(52f, 24f));
                MakePlate("Bg", pickedBadge, ColGold);
                Text pickedTxt = Ui.MakeText("Text", pickedBadge, "YOU", 16, ColInk, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                pickedTxt.GetComponent<Outline>().enabled = false;
                pickedTxt.resizeTextForBestFit = true;
                pickedTxt.resizeTextMinSize = 16;
                pickedTxt.resizeTextMaxSize = 16;
                Stretch(pickedTxt.rectTransform, 2f, 0f, -2f, 0f);

                RectTransform takenBadge = Ui.Panel("TakenBadge", cellGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 26f));
                MakePlate("Bg", takenBadge, ColTakenBadge);
                Text takenTxt = Ui.MakeText("Text", takenBadge, "TAKEN", 16, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                takenTxt.resizeTextForBestFit = true;
                takenTxt.resizeTextMinSize = 16;
                takenTxt.resizeTextMaxSize = 16;
                Stretch(takenTxt.rectTransform, 2f, 0f, -2f, 0f);
                takenBadge.gameObject.SetActive(false);

                _kidTiles.Add(new KidTileRefs
                {
                    Id = kidId,
                    Button = btn,
                    Frame = frame,
                    Border = border,
                    Portrait = portrait,
                    FallbackDisc = fallbackDisc,
                    Initials = initials,
                    NameLabel = nameLabel,
                    SelectedBadge = pickedBadge.gameObject,
                    TakenBadge = takenBadge.gameObject,
                });
            }
        }

        void BuildLoadoutSlots()
        {
            for (int i = 0; i < LoadoutSlotCount; i++)
            {
                int slotIndex = i;
                var slotGo = new GameObject("Slot_" + slotIndex, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                slotGo.transform.SetParent(_slotsRowRect, false);

                var activeRing = slotGo.GetComponent<Image>();
                activeRing.sprite = RoundedPlate();
                activeRing.type = Image.Type.Sliced;
                activeRing.color = ColGold;

                var btn = slotGo.GetComponent<Button>();
                btn.targetGraphic = activeRing;
                ConfigureButtonColors(btn);
                btn.onClick.AddListener(() => SelectSlot(slotIndex));

                Image frame = MakePlate("Frame", slotGo.transform, ColPanelInset);
                Stretch(frame.rectTransform, 3f, 3f, -3f, -3f);

                RectTransform iconHolder = Ui.Panel("IconHolder", frame.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(52f, 52f));
                Image fallbackBadge = MakePlate("FallbackBadge", iconHolder, ColDisabledBtn);
                Text fallbackCode = Ui.MakeText("FallbackCode", fallbackBadge.transform, "+", 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(fallbackCode.rectTransform, 0f, 0f, 0f, 0f);
                RawImage icon = MakeFittedRawImage("Icon", iconHolder, 1f, out AspectRatioFitter iconFitter);

                Text slotKey = Ui.MakeText("SlotKey", frame.transform, SlotBadges[slotIndex], 16, ColGold, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
                slotKey.rectTransform.anchorMin = new Vector2(0f, 0.71f);
                slotKey.rectTransform.anchorMax = new Vector2(1f, 0.97f);
                slotKey.rectTransform.offsetMin = new Vector2(64f, 0f);
                slotKey.rectTransform.offsetMax = new Vector2(-6f, 0f);
                slotKey.resizeTextForBestFit = true;
                slotKey.resizeTextMinSize = 12;
                slotKey.resizeTextMaxSize = 16;

                Text wName = Ui.MakeText("WeaponName", frame.transform, "Tap Superpower", 18, Ui.Cream, TextAnchor.MiddleLeft);
                wName.rectTransform.anchorMin = new Vector2(0f, 0.31f);
                wName.rectTransform.anchorMax = new Vector2(1f, 0.71f);
                wName.rectTransform.offsetMin = new Vector2(64f, 0f);
                wName.rectTransform.offsetMax = new Vector2(-6f, 0f);
                wName.horizontalOverflow = HorizontalWrapMode.Wrap;
                wName.verticalOverflow = VerticalWrapMode.Truncate;
                wName.resizeTextForBestFit = true;
                wName.resizeTextMinSize = 12;
                wName.resizeTextMaxSize = 18;

                Text role = Ui.MakeText("Role", frame.transform, "EMPTY", 16, ColMutedText, TextAnchor.LowerLeft);
                role.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                role.rectTransform.anchorMax = new Vector2(1f, 0.31f);
                role.rectTransform.offsetMin = new Vector2(64f, 0f);
                role.rectTransform.offsetMax = new Vector2(-6f, 0f);
                role.resizeTextForBestFit = true;
                role.resizeTextMinSize = 12;
                role.resizeTextMaxSize = 16;

                _slots[slotIndex] = new SlotRefs
                {
                    Index = slotIndex,
                    Button = btn,
                    Frame = frame,
                    ActiveRing = activeRing,
                    IconHolder = iconHolder,
                    Icon = icon,
                    IconFitter = iconFitter,
                    FallbackBadge = fallbackBadge,
                    FallbackCode = fallbackCode,
                    SlotKeyLabel = slotKey,
                    WeaponNameLabel = wName,
                    RoleLabel = role,
                };
            }
        }

        void BuildWeaponTiles()
        {
            _weaponTiles.Clear();
            for (int i = 0; i < _deckWeapons.Count; i++)
            {
                WeaponInfo info = _deckWeapons[i];
                string wid = info.Id;

                var cellGo = new GameObject("Weapon_" + wid, typeof(RectTransform), typeof(Image), typeof(Button));
                cellGo.transform.SetParent(_weaponsGridRect, false);
                var border = cellGo.GetComponent<Image>();
                border.sprite = RoundedPlate();
                border.type = Image.Type.Sliced;
                border.color = new Color(1f, 1f, 1f, 0.14f);

                var btn = cellGo.GetComponent<Button>();
                btn.targetGraphic = border;
                ConfigureButtonColors(btn);
                btn.onClick.AddListener(() => TapWeapon(wid, saveImmediately: true));

                Image frame = MakePlate("Frame", cellGo.transform, ColCardIdle);
                Stretch(frame.rectTransform, 3f, 3f, -3f, -3f);

                RectTransform iconHolder = Ui.Panel("IconHolder", frame.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(48f, 48f));
                Image fallbackBadge = MakePlate("FallbackBadge", iconHolder, RoleColor(info.Kind));
                Text fallbackCode = Ui.MakeText("FallbackCode", fallbackBadge.transform, ShortBadgeCode(info), 18, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(fallbackCode.rectTransform, 0f, 0f, 0f, 0f);

                Texture2D tex = GetWeaponTexture(wid);
                RawImage icon = MakeFittedRawImage("Icon", iconHolder, TextureAspectOrSquare(tex), out AspectRatioFitter iconFitter);
                icon.texture = tex;
                icon.gameObject.SetActive(tex != null);
                fallbackBadge.gameObject.SetActive(tex == null);

                Text nameLabel = Ui.MakeText("Name", frame.transform, ShortWeaponTitle(info), 18, Ui.Cream, TextAnchor.UpperLeft);
                nameLabel.rectTransform.anchorMin = new Vector2(0f, 0.60f);
                nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
                nameLabel.rectTransform.offsetMin = new Vector2(58f, 0f);
                nameLabel.rectTransform.offsetMax = new Vector2(-28f, -5f);
                nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
                nameLabel.resizeTextForBestFit = true;
                nameLabel.resizeTextMinSize = 12;
                nameLabel.resizeTextMaxSize = 18;

                Text roleLabel = Ui.MakeText("Role", frame.transform, RoleName(info.Kind), 16, RoleTextColor(info.Kind), TextAnchor.LowerLeft);
                roleLabel.rectTransform.anchorMin = new Vector2(0f, 0.41f);
                roleLabel.rectTransform.anchorMax = new Vector2(1f, 0.61f);
                roleLabel.rectTransform.offsetMin = new Vector2(58f, 0f);
                roleLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
                roleLabel.resizeTextForBestFit = true;
                roleLabel.resizeTextMinSize = 12;
                roleLabel.resizeTextMaxSize = 16;

                // 3-column high-contrast >= 16 px mini stat gauges (DMG / RNG / CD) across the bottom band
                float pwrFrac = info.Heal > 0
                    ? Mathf.Clamp01(info.Heal / Mathf.Max(1f, _maxPower))
                    : (info.Damage > 0 ? Mathf.Clamp01(info.Damage / Mathf.Max(1f, _maxPower)) : 0.14f);
                Color pwrCol = info.Heal > 0 ? ColBarHeal : (info.Damage > 0 ? ColBarDamage : RoleTextColor(info.Kind));
                string pwrTag = info.Heal > 0 ? "HEAL" : "DMG";
                float rngFrac = info.MaxRange > 0f ? Mathf.Clamp01(info.MaxRange / Mathf.Max(1f, _maxRange)) : 0.10f;
                float cdFrac = Mathf.Clamp01(info.Cooldown / Mathf.Max(0.1f, _maxCooldown));

                RectTransform miniRow = Ui.Panel("MiniStatsRow", frame.transform, new Vector2(0f, 0f), new Vector2(1f, 0.40f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
                miniRow.offsetMin = new Vector2(6f, 4f);
                miniRow.offsetMax = new Vector2(-6f, -1f);
                var miniGroup = miniRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                miniGroup.spacing = 6f;
                miniGroup.childControlWidth = true;
                miniGroup.childControlHeight = true;
                miniGroup.childForceExpandWidth = true;
                miniGroup.childForceExpandHeight = true;

                Image pwrFill = BuildMiniStatColumn("DmgBar", miniRow, pwrTag, pwrFrac, pwrCol);
                Image rngFill = BuildMiniStatColumn("RngBar", miniRow, "RNG", rngFrac, ColBarRange);
                Image cdFill = BuildMiniStatColumn("CdBar", miniRow, "CD", cdFrac, ColBarCooldown);

                RectTransform eqBadge = Ui.Panel("EquippedBadge", cellGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(26f, 26f));
                MakePlate("Bg", eqBadge, ColGold);
                Text eqTxt = Ui.MakeText("SlotNum", eqBadge, "1", 16, ColInk, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                eqTxt.GetComponent<Outline>().enabled = false;
                Stretch(eqTxt.rectTransform, 0f, 0f, 0f, 0f);
                eqBadge.gameObject.SetActive(false);

                RectTransform takenBadge = Ui.Panel("TakenBadge", cellGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 28f));
                MakePlate("Bg", takenBadge, ColTakenBadge);
                Text takenTxt = Ui.MakeText("Text", takenBadge, "TAKEN", 16, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(takenTxt.rectTransform, 2f, 0f, -2f, 0f);
                takenBadge.gameObject.SetActive(false);

                _weaponTiles.Add(new WeaponTileRefs
                {
                    Id = wid,
                    Info = info,
                    Button = btn,
                    Frame = frame,
                    Border = border,
                    IconHolder = iconHolder,
                    Icon = icon,
                    IconFitter = iconFitter,
                    FallbackBadge = fallbackBadge,
                    FallbackCode = fallbackCode,
                    NameLabel = nameLabel,
                    RoleLabel = roleLabel,
                    PowerFill = pwrFill,
                    RangeFill = rngFill,
                    CooldownFill = cdFill,
                    EquippedBadge = eqBadge.gameObject,
                    EquippedSlotText = eqTxt,
                    TakenBadge = takenBadge.gameObject,
                });
            }
        }

        static Image BuildMiniStatColumn(string name, Transform parent, string tag, float frac, Color fillColor)
        {
            var col = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            col.transform.SetParent(parent, false);

            Text lbl = Ui.MakeText("Tag", col.transform, tag, 16, ColSoftLabel, TextAnchor.LowerLeft);
            lbl.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            lbl.resizeTextForBestFit = true;
            lbl.resizeTextMinSize = 16;
            lbl.resizeTextMaxSize = 16;
            lbl.rectTransform.anchorMin = new Vector2(0f, 0.34f);
            lbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            lbl.rectTransform.offsetMin = Vector2.zero;
            lbl.rectTransform.offsetMax = Vector2.zero;

            RectTransform track = Ui.Panel("Track", col.transform, new Vector2(0f, 0.04f), new Vector2(1f, 0.30f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            MakePlate("Bg", track, ColBarTrack);

            Image fill = MakePlate("Fill", track, fillColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp(frac, 0.06f, 1f), 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        static void BuildWeaponInspector(
            RectTransform root,
            out RawImage icon,
            out AspectRatioFitter iconFitter,
            out Image fallbackBadge,
            out Text fallbackCode,
            out Text nameLabel,
            out Image roleChip,
            out Text roleLabel,
            out Text gameLabel,
            out Text descLabel,
            out StatMeterRefs powerMeter,
            out StatMeterRefs rangeMeter,
            out StatMeterRefs cooldownMeter)
        {
            RectTransform iconBox = Ui.Panel("InspectorIcon", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(58f, 58f));
            fallbackBadge = MakePlate("FallbackBadge", iconBox, ColAmber);
            fallbackCode = Ui.MakeText("Code", fallbackBadge.transform, "SP", 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(fallbackCode.rectTransform, 0f, 0f, 0f, 0f);
            icon = MakeFittedRawImage("Icon", iconBox, 1f, out iconFitter);

            nameLabel = Ui.MakeText("InfoName", root, "", 20, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLabel.rectTransform.pivot = new Vector2(0f, 1f);
            nameLabel.rectTransform.offsetMin = new Vector2(74f, -30f);
            nameLabel.rectTransform.offsetMax = new Vector2(-130f, -4f);
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 13;
            nameLabel.resizeTextMaxSize = 20;

            RectTransform rolePill = Ui.Panel("RoleChip", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(114f, 26f));
            roleChip = MakePlate("Bg", rolePill, RoleColor(WeaponRoleKind.Attack));
            roleLabel = Ui.MakeText("RoleText", rolePill, "ATTACK", 16, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            roleLabel.resizeTextForBestFit = true;
            roleLabel.resizeTextMinSize = 12;
            roleLabel.resizeTextMaxSize = 16;
            Stretch(roleLabel.rectTransform, 4f, 1f, -4f, -1f);

            gameLabel = Ui.MakeText("StreetGame", root, "", 16, ColGold, TextAnchor.MiddleLeft);
            gameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            gameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            gameLabel.rectTransform.pivot = new Vector2(0f, 1f);
            gameLabel.rectTransform.offsetMin = new Vector2(74f, -49f);
            gameLabel.rectTransform.offsetMax = new Vector2(-10f, -29f);
            gameLabel.resizeTextForBestFit = true;
            gameLabel.resizeTextMinSize = 12;
            gameLabel.resizeTextMaxSize = 16;

            descLabel = Ui.MakeText("Description", root, "", 16, ColSoftLabel, TextAnchor.UpperLeft);
            descLabel.rectTransform.anchorMin = new Vector2(0f, 0.32f);
            descLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            descLabel.rectTransform.offsetMin = new Vector2(74f, 0f);
            descLabel.rectTransform.offsetMax = new Vector2(-10f, -50f);
            descLabel.lineSpacing = 0.92f;
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descLabel.verticalOverflow = VerticalWrapMode.Truncate;
            descLabel.resizeTextForBestFit = true;
            descLabel.resizeTextMinSize = 12;
            descLabel.resizeTextMaxSize = 16;

            RectTransform barsRow = Ui.Panel("StatBarsRow", root, new Vector2(0f, 0f), new Vector2(1f, 0.34f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            barsRow.offsetMin = new Vector2(10f, 5f);
            barsRow.offsetMax = new Vector2(-10f, -2f);
            var hGroup = barsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hGroup.spacing = 12f;
            hGroup.childControlWidth = true;
            hGroup.childControlHeight = true;
            hGroup.childForceExpandWidth = true;
            hGroup.childForceExpandHeight = true;

            powerMeter = BuildInspectorMeter("Stat_Damage", barsRow, "Damage", ColBarDamage);
            rangeMeter = BuildInspectorMeter("Stat_Range", barsRow, "Range", ColBarRange);
            cooldownMeter = BuildInspectorMeter("Stat_Cooldown", barsRow, "Cooldown", ColBarCooldown);
        }

        static StatMeterRefs BuildInspectorMeter(string name, Transform parent, string caption, Color color)
        {
            var col = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            col.transform.SetParent(parent, false);

            Text cap = Ui.MakeText("Caption", col.transform, caption, 16, ColSoftLabel, TextAnchor.LowerLeft);
            cap.rectTransform.anchorMin = new Vector2(0f, 0.44f);
            cap.rectTransform.anchorMax = new Vector2(0.56f, 1f);
            cap.rectTransform.offsetMin = Vector2.zero;
            cap.rectTransform.offsetMax = Vector2.zero;
            cap.resizeTextForBestFit = true;
            cap.resizeTextMinSize = 16;
            cap.resizeTextMaxSize = 16;

            Text val = Ui.MakeText("Value", col.transform, "", 16, color, TextAnchor.LowerRight, KalyeahResources.DisplayFont());
            val.rectTransform.anchorMin = new Vector2(0.50f, 0.44f);
            val.rectTransform.anchorMax = new Vector2(1f, 1f);
            val.rectTransform.offsetMin = Vector2.zero;
            val.rectTransform.offsetMax = Vector2.zero;
            val.resizeTextForBestFit = true;
            val.resizeTextMinSize = 16;
            val.resizeTextMaxSize = 16;

            RectTransform track = Ui.Panel("Track", col.transform, new Vector2(0f, 0.08f), new Vector2(1f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            MakePlate("Bg", track, ColBarTrack);

            Image fill = MakePlate("Fill", track, color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            return new StatMeterRefs
            {
                CaptionLabel = cap,
                FillImage = fill,
                ValueLabel = val,
            };
        }

        void FillMissingSlotsFromUntaken()
        {
            List<string> suggested = BuildSuggestedTrio();
            for (int i = 0; i < suggested.Count && _selectedWeapons.Count < LoadoutSlotCount; i++)
            {
                string cand = suggested[i];
                if (!_takenWeapons.Contains(cand) && !_selectedWeapons.Contains(cand))
                    _selectedWeapons.Add(cand);
            }
            for (int i = 0; i < _deckWeapons.Count && _selectedWeapons.Count < LoadoutSlotCount; i++)
            {
                string cand = _deckWeapons[i].Id;
                if (!_takenWeapons.Contains(cand) && !_selectedWeapons.Contains(cand))
                    _selectedWeapons.Add(cand);
            }
        }

        List<string> BuildSuggestedTrio()
        {
            var availableDefs = new List<Kalyeah.Sim.WeaponDef>(_simDeckWeapons.Count);
            for (int i = 0; i < _simDeckWeapons.Count; i++)
            {
                Kalyeah.Sim.WeaponDef def = _simDeckWeapons[i];
                if (def != null && !_takenWeapons.Contains(def.Id))
                    availableDefs.Add(def);
            }

            var result = new List<string>(LoadoutSlotCount);
            List<string> corePair = Kalyeah.Sim.WeaponPick.Suggestion(availableDefs);
            for (int i = 0; i < corePair.Count && result.Count < LoadoutSlotCount; i++)
            {
                if (!result.Contains(corePair[i]))
                    result.Add(corePair[i]);
            }

            List<string> suggestedSet = Kalyeah.Sim.WeaponPick.SuggestionFor(availableDefs, LoadoutSlotCount);
            for (int i = 0; i < suggestedSet.Count && result.Count < LoadoutSlotCount; i++)
            {
                if (!result.Contains(suggestedSet[i]))
                    result.Add(suggestedSet[i]);
            }

            for (int i = 0; i < availableDefs.Count && result.Count < LoadoutSlotCount; i++)
            {
                string wid = availableDefs[i].Id;
                if (!result.Contains(wid))
                    result.Add(wid);
            }
            return result;
        }

        bool DealsZeroDirectDamage(IList<string> weaponIds)
        {
            if (weaponIds == null || weaponIds.Count == 0) return false;
            return Kalyeah.Sim.WeaponPick.DealsNoDamage(weaponIds, _simWeaponById);
        }

        int IndexOfCharacter(string characterId)
        {
            for (int i = 0; i < _characters.Count; i++)
                if (string.Equals(_characters[i].Id, characterId, StringComparison.Ordinal)) return i;
            return -1;
        }

        static void BuildBackdrop(Transform root)
        {
            RectTransform backdrop = Ui.Fill("Backdrop", root);
            backdrop.gameObject.AddComponent<RectMask2D>();
            Image bottom = Ui.MakeImage("DuskBase", backdrop, ColBackdropBottom, raycast: true);
            Stretch(bottom.rectTransform, 0f, 0f, 0f, 0f);

            Texture2D mainBg = KalyeahResources.Texture("ui/main_bg");
            if (mainBg != null)
            {
                var bgGo = new GameObject("KeyArtBackdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                bgGo.transform.SetParent(backdrop, false);
                var bgArt = bgGo.GetComponent<RawImage>();
                bgArt.raycastTarget = false;
                Stretch(bgArt.rectTransform, 0f, 0f, 0f, 0f);
                bgArt.texture = mainBg;
                bgArt.color = new Color(0.24f, 0.22f, 0.34f, 0.32f);

                var fitter = bgGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = mainBg.height > 0 ? (float)mainBg.width / mainBg.height : (16f / 9f);
            }

            Image topBand = Ui.MakeImage("SkyGlow", backdrop, ColBackdropTop);
            topBand.rectTransform.anchorMin = new Vector2(0f, 0.58f);
            topBand.rectTransform.anchorMax = Vector2.one;
            topBand.rectTransform.offsetMin = Vector2.zero;
            topBand.rectTransform.offsetMax = Vector2.zero;

            Image sunAccent = Ui.MakeImage("SunStripe", backdrop, ColSunGlow);
            sunAccent.rectTransform.anchorMin = new Vector2(0f, 0.86f);
            sunAccent.rectTransform.anchorMax = Vector2.one;
            sunAccent.rectTransform.offsetMin = Vector2.zero;
            sunAccent.rectTransform.offsetMax = Vector2.zero;
        }

        static RectTransform BuildScrollGrid(
            string scrollName,
            Transform parent,
            string gridName,
            int columns,
            Vector2 spacing,
            out RectTransform viewportRect,
            out ScrollRect scroll,
            out GameObject scrollCueRoot,
            out RectTransform contentRect,
            out GridLayoutGroup grid)
        {
            var scrollGo = new GameObject(scrollName, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.SetParent(parent, false);
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;

            var containerImg = scrollGo.GetComponent<Image>();
            containerImg.sprite = RoundedPlate();
            containerImg.type = Image.Type.Sliced;
            containerImg.color = new Color(0.04f, 0.06f, 0.13f, 0.78f);
            containerImg.raycastTarget = true;

            // Masked viewport cleanly inset above the dedicated bottom scroll-hint strip so cards are NEVER covered or half-sliced
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportRect = vpGo.GetComponent<RectTransform>();
            viewportRect.SetParent(scrollRectTransform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var vpImg = vpGo.GetComponent<Image>();
            vpImg.color = Color.clear;
            vpImg.raycastTarget = true;

            var contentGo = new GameObject(gridName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 320f);

            grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.spacing = spacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            // Dedicated >= 16 px scroll-hint footer strip docked cleanly below Viewport
            RectTransform cueOverlay = Ui.Panel(
                "BottomScrollCue",
                scrollRectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, ScrollHintFooterHeight));
            Image stripBg = MakePlate("StripBg", cueOverlay, new Color(0.03f, 0.05f, 0.12f, 0.94f));
            Stretch(stripBg.rectTransform, 2f, 2f, -2f, 0f);

            RectTransform cuePill = Ui.Panel("CuePill", cueOverlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(204f, 24f));
            MakePlate("Border", cuePill, new Color(1.00f, 0.78f, 0.22f, 0.42f));
            Image pillBg = MakePlate("Bg", cuePill, new Color(0.06f, 0.09f, 0.20f, 0.96f));
            Stretch(pillBg.rectTransform, 1f, 1f, -1f, -1f);
            Text cueTxt = Ui.MakeText("Label", cuePill, "SWIPE FOR MORE", 16, ColGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            cueTxt.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            cueTxt.resizeTextForBestFit = true;
            cueTxt.resizeTextMinSize = 16;
            cueTxt.resizeTextMaxSize = 16;
            Stretch(cueTxt.rectTransform, 6f, 1f, -6f, -1f);

            GameObject cueGo = cueOverlay.gameObject;
            ScrollRect localScroll = scroll;
            RectTransform localContent = contentRect;
            scroll.onValueChanged.AddListener(_ => UpdateScrollCue(localScroll, localContent, cueGo));
            scrollCueRoot = cueGo;

            return scrollRectTransform;
        }

        static void UpdateScrollCue(ScrollRect scroll, RectTransform content, GameObject cue)
        {
            if (scroll == null || content == null || cue == null || scroll.viewport == null) return;
            float overflow = content.rect.height - scroll.viewport.rect.height;
            bool show = overflow > 6f && scroll.verticalNormalizedPosition > 0.04f;
            if (cue.activeSelf != show)
                cue.SetActive(show);
        }

        static Button BuildActionButton(
            string name,
            Transform parent,
            string labelText,
            Color plateColor,
            Color textColor,
            int fontSize,
            UnityEngine.Events.UnityAction onClick,
            out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = RoundedPlate();
            img.type = Image.Type.Sliced;
            img.color = plateColor;
            img.raycastTarget = true;

            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.03f, 0.08f, 0.85f);
            shadow.effectDistance = new Vector2(0f, -3f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ConfigureButtonColors(btn);
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text label = Ui.MakeText("Label", rect, labelText, fontSize, textColor, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 6f, 2f, -6f, -2f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = fontSize;
            if ((0.2126f * textColor.r + 0.7152f * textColor.g + 0.0722f * textColor.b) < 0.25f)
                label.GetComponent<Outline>().enabled = false;
            return btn;
        }

        static void ConfigureButtonColors(Button btn)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.0f, 0.96f, 0.88f, 1f);
            cb.pressedColor = new Color(0.80f, 0.80f, 0.84f, 1f);
            cb.selectedColor = new Color(1.0f, 0.96f, 0.86f, 1f);
            cb.disabledColor = new Color(0.58f, 0.58f, 0.62f, 0.78f);
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
        }

        static Image MakePlate(string name, Transform parent, Color color)
        {
            Image img = Ui.MakeImage(name, parent, color, round: false, raycast: false);
            img.sprite = RoundedPlate();
            img.type = Image.Type.Sliced;
            Stretch(img.rectTransform, 0f, 0f, 0f, 0f);
            return img;
        }

        static RawImage MakeFittedRawImage(string name, Transform parent, float aspectRatio, out AspectRatioFitter fitter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = aspectRatio > 0.01f ? aspectRatio : 1f;
            return raw;
        }

        static RawImage MakeSquareRawImage(string name, Transform parent) =>
            MakeFittedRawImage(name, parent, 1f, out _);

        static float TextureAspectOrSquare(Texture2D tex) =>
            tex != null && tex.height > 0 ? (float)tex.width / tex.height : 1f;

        static Texture2D LoadSquareKidPortrait(string id, out Rect uvRect)
        {
            uvRect = new Rect(0f, 0f, 1f, 1f);
            if (string.IsNullOrEmpty(id)) return null;

            // portraits/<id>.png is 600x800 (3:4) on disk; v: 0.25..1.00 maps 600x600 source pixels for exact 1:1 aspect.
            Texture2D bust = KalyeahResources.Texture("portraits/" + id);
            if (bust != null)
            {
                uvRect = new Rect(0f, 0.25f, 1f, 0.75f);
                return bust;
            }

            Texture2D face = KalyeahResources.Texture("portraits/faces/" + id);
            if (face != null)
                return face;

            Texture2D cardBust = KalyeahResources.Texture("portraits/cards/" + id);
            if (cardBust != null)
            {
                uvRect = new Rect(0f, 0.25f, 1f, 0.75f);
                return cardBust;
            }

            Texture2D avatar = KalyeahResources.Texture("ui/avatars/" + id);
            if (avatar != null)
                return avatar;

            Texture2D charCard = KalyeahResources.Texture("ui/character_cards/" + id);
            if (charCard != null)
            {
                uvRect = new Rect(0f, 0.3487f, 1f, 0.6513f);
                return charCard;
            }

            return null;
        }

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        static Sprite RoundedPlate()
        {
            if (s_roundedSprite != null) return s_roundedSprite;
            const int size = 64;
            const float radius = 14f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahPickRoundedPlate",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(0f, Mathf.Max(radius - px, px - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - py, py - (size - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - dist + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            s_roundedSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
            s_roundedSprite.name = "KalyeahPickRoundedPlate";
            return s_roundedSprite;
        }

        static Texture2D LoadFirstTexture(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i])) continue;
                Texture2D found = KalyeahResources.Texture(paths[i]);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Saturated Komiks badge fill for fallback chips and inspector role pill (paired with bright Cream text).</summary>
        static Color RoleColor(WeaponRoleKind kind)
        {
            switch (kind)
            {
                case WeaponRoleKind.Attack: return new Color(0.78f, 0.24f, 0.18f, 1f);
                case WeaponRoleKind.CrowdControl: return new Color(0.48f, 0.26f, 0.74f, 1f);
                case WeaponRoleKind.Block: return new Color(0.12f, 0.48f, 0.68f, 1f);
                case WeaponRoleKind.Heal: return new Color(0.14f, 0.54f, 0.28f, 1f);
                default: return ColAmber;
            }
        }

        /// <summary>Bright >= 4.5:1 contrast role label tint for dark navy loadout slots and weapon cards.</summary>
        static Color RoleTextColor(WeaponRoleKind kind)
        {
            switch (kind)
            {
                case WeaponRoleKind.Attack: return new Color(1.00f, 0.68f, 0.56f, 1f);
                case WeaponRoleKind.CrowdControl: return new Color(0.86f, 0.72f, 1.00f, 1f);
                case WeaponRoleKind.Block: return new Color(0.56f, 0.90f, 1.00f, 1f);
                case WeaponRoleKind.Heal: return new Color(0.58f, 0.96f, 0.72f, 1f);
                default: return ColGold;
            }
        }

        static string RoleName(WeaponRoleKind kind)
        {
            switch (kind)
            {
                case WeaponRoleKind.Attack: return "Attack";
                case WeaponRoleKind.CrowdControl: return "Control";
                case WeaponRoleKind.Block: return "Block";
                case WeaponRoleKind.Heal: return "Heal";
                default: return "Skill";
            }
        }

        static string ShortWeaponTitle(WeaponInfo info)
        {
            if (string.IsNullOrEmpty(info.DisplayName)) return CapitalizeId(info.Id);
            int dot = info.DisplayName.IndexOf('\u00B7');
            if (dot > 0 && dot + 1 < info.DisplayName.Length)
                return info.DisplayName.Substring(dot + 1).Trim();
            return info.DisplayName;
        }

        static string ShortBadgeCode(WeaponInfo info)
        {
            string src = !string.IsNullOrEmpty(info.ShortName) ? info.ShortName : info.DisplayName;
            return InitialsOf(src);
        }

        static string InitialsOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "?";
            string trimmed = text.Trim();
            if (trimmed.Length <= 2) return trimmed.ToUpperInvariant();
            return trimmed.Substring(0, 2).ToUpperInvariant();
        }

        static string CapitalizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string spaced = id.Replace('_', ' ');
            return char.ToUpperInvariant(spaced[0]) + (spaced.Length > 1 ? spaced.Substring(1) : "");
        }

        static string FormatNumber(float value)
        {
            float rounded = Mathf.Round(value);
            return Mathf.Abs(value - rounded) < 0.05f
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        static int PositiveMod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        static string ReadStr(Dictionary<string, object> props, string key, string fallback)
        {
            if (props != null && props.TryGetValue(key, out object v) && v is string s && !string.IsNullOrEmpty(s))
                return s;
            return fallback;
        }

        static int ReadInt(Dictionary<string, object> props, string key, int fallback)
        {
            if (props != null && props.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { return fallback; }
            }
            return fallback;
        }

        static float ReadFloat(Dictionary<string, object> props, string key, float fallback)
        {
            if (props != null && props.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
                catch { return fallback; }
            }
            return fallback;
        }

        static Color ReadColor(Dictionary<string, object> props, string key, Color fallback)
        {
            if (props != null && props.TryGetValue(key, out object raw) && raw is Dictionary<string, object> cd)
            {
                float r = ReadFloat(cd, "r", fallback.r);
                float g = ReadFloat(cd, "g", fallback.g);
                float b = ReadFloat(cd, "b", fallback.b);
                float a = ReadFloat(cd, "a", fallback.a);
                return new Color(r, g, b, a);
            }
            return fallback;
        }
    }
}
