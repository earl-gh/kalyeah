using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full-screen MLBB x Wild Rift x CODM Player Profile Hub (<see cref="MenuStack.ProfileId"/>, docs/SPEC.md §8):
    /// left vertical tactical navigation rail (<see cref="TabOverview"/>, <see cref="TabHistory"/>, <see cref="TabIdentity"/>),
    /// live 3D favourite-kid <see cref="HeroStage"/> showcase, CODM-style calling-card banner, 4-KPI career strip with
    /// per-kid win-rate sub-readout, neutral-icon Favourite Roles chart, recent form badges, inline last-20 Match History log,
    /// and full-screen Live Calling Card / Player Tag / 12-Kid Avatar / On-Device Selfie studio.
    /// </summary>
    public sealed class ProfileScreenView : IDisposable
    {
        public const int TabOverview = 0;
        public const int TabHistory = 1;
        public const int TabIdentity = 2;

        static readonly string[] RoleOrder = { "STRIKER", "DEFENDER", "SEEKER", "CONTROLLER" };
        static readonly string[] TabIds = { "Overview", "History", "Identity" };
        static readonly string[] TabTitles = { "OVERVIEW", "HISTORY", "IDENTITY" };
        static readonly MenuArt.GlyphKind[] TabGlyphs =
        {
            MenuArt.GlyphKind.Kid,
            MenuArt.GlyphKind.History,
            MenuArt.GlyphKind.Camera,
        };

        static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        static readonly Color Ink = MenuArt.Ink;
        static readonly Color Cream = MenuArt.PaperWhite;
        static readonly Color Gold = MenuArt.SignalVermilion;
        static readonly Color GoldLight = MenuArt.CoralGold;
        static readonly Color GoldBorder = MenuArt.PaperWhite;
        static readonly Color VermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color ObsidianGlass = MenuArt.ObsidianGlass;
        static readonly Color ObsidianSolid = MenuArt.ObsidianSolid;
        static readonly Color ObsidianCard = new Color(0.075f, 0.102f, 0.140f, 0.88f);
        static readonly Color TitaniumBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.24f);
        static readonly Color TitaniumBorderStrong = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.44f);
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color SuccessGreen = MenuArt.SuccessGreen;
        static readonly Color Coral = MenuArt.SignalVermilion;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Shade = new Color(0.035f, 0.047f, 0.066f, 1f);
        static readonly Color BackdropWash = new Color(0.055f, 0.075f, 0.102f, 0.80f);
        static readonly Color Hairline = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.22f);
        static readonly Color TrackBg = new Color(0.040f, 0.054f, 0.076f, 0.96f);
        static readonly Color NeutralBarFill = new Color(0.84f, 0.88f, 0.94f, 0.78f);
        static readonly Color InputIdleBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.46f);
        static readonly Color ErrorRed = MenuArt.SignalVermilion;
        static readonly Color WinRowFill = new Color(0.068f, 0.118f, 0.118f, 0.88f);
        static readonly Color WinRowBorder = new Color(0.24f, 0.82f, 0.56f, 0.34f);
        static readonly Color LossRowFill = new Color(0.128f, 0.078f, 0.098f, 0.88f);
        static readonly Color LossRowBorder = new Color(1.00f, 0.275f, 0.333f, 0.34f);
        static readonly Color FormEmptyBg = new Color(0.082f, 0.108f, 0.148f, 0.80f);

        const float HeaderHeight = 96f;
        const float Gutter = 20f;
        const float HitHeight = 96f;
        const float BodyTopInset = 104f;
        const float BodyBottomInset = 12f;
        const int MaxInlineHistoryRows = 20;

        [ExecuteAlways]
        sealed class ProfileLifecycle : MonoBehaviour
        {
            [NonSerialized] public ProfileScreenView Owner;
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

            void Update() => Owner?.Tick(Time.unscaledDeltaTime);

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

        sealed class TabUi
        {
            public Image Plate;
            public Image AccentRail;
            public Image Icon;
            public Text Label;
        }

        sealed class RoleBarUi
        {
            public Image Icon;
            public Text NameLabel;
            public RectTransform FillRect;
            public Image FillImage;
            public Text PctLabel;
        }

        sealed class AvatarTileUi
        {
            public int Index;
            public RectTransform Rect;
            public Image Outline;
            public Image CheckBadge;
        }

        readonly DataSource _data;
        readonly Action _onBack;
        readonly Action _onOpenHistory;
        readonly Action _onOpenKids;
        readonly Action _onOpenIdCamera;
        readonly HomeSheetsView _sheets;
        readonly AudioHub _audio;
        readonly HeroStage _stage;
        readonly SafeArea _safeArea;

        readonly TabUi[] _tabs = new TabUi[3];
        readonly RoleBarUi[] _roleBars = new RoleBarUi[4];
        readonly Image[] _formBgs = new Image[5];
        readonly Text[] _formTexts = new Text[5];
        readonly List<AvatarTileUi> _avatarTiles = new List<AvatarTileUi>(12);
        readonly List<GameObject> _historyRows = new List<GameObject>(24);
        readonly Dictionary<string, string> _kidRoleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _kidPurokMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _kidNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RectTransform _headerBar;
        RectTransform _backBtnRect;
        RectTransform _body;
        RectTransform _navRail;
        RectTransform _contentStage;
        RectTransform _overviewPane;
        RectTransform _historyPane;
        RectTransform _identityPane;

        // Header & nav rail refs
        Text _playerIdChipText;
        Text _navLevelText;
        RectTransform _navLevelFill;
        Text _navMatchesText;

        // Overview refs
        RectTransform _heroCol;
        RectTransform _careerCol;
        RectTransform _changeKidBtnRect;
        RectTransform _editBtnRect;
        RectTransform _historyBtnRect;
        RawImage _heroRaw;
        Image _heroRoleIcon;
        Text _heroKicker;
        Text _heroName;
        RawImage _bannerAvatar;
        Text _bannerLevelText;
        Text _bannerKicker;
        Text _bannerName;
        Text _bannerHonorText;
        Text _bannerCareerText;
        Text _statMatchesValue;
        Text _statWinRateValue;
        Text _statWinsValue;
        Text _statTopKidValue;
        Text _statTopKidSub;
        Text _rolesFootnote;
        Text _latestMatchTitle;
        Text _latestMatchDetail;

        // History tab refs
        RectTransform _historyTopBar;
        RectTransform _historyLogCard;
        RectTransform _fullHistoryBtnRect;
        Text _histCountValue;
        Text _histWinsValue;
        Text _histLossesValue;
        Text _histRateValue;
        ScrollRect _historyScroll;
        RectTransform _historyContent;

        // Identity tab refs
        RectTransform _callingCol;
        RectTransform _callingCardRect;
        RectTransform _livePreviewRect;
        RectTransform _callingHairlineRect;
        RectTransform _captionRowRect;
        RectTransform _nameFieldBoxRect;
        RectTransform _avatarCol;
        RectTransform _avatarGridRect;
        RawImage _identityPreviewImg;
        Text _identityPreviewLevel;
        Text _identityLiveName;
        Text _identityLiveSub;
        Text _nameCaptionText;
        Text _nameHintText;
        Text _nameLetterCount;
        Image _inputFocusOutline;
        InputField _nameInput;
        RectTransform _saveBtnRect;
        RectTransform _photoRowRect;
        RectTransform _selfieBtnRect;
        RectTransform _removePhotoBtnRect;
        Button _removePhotoButton;
        bool _wasNameFocused;
        bool _hasLocalAvatarIndex;
        int _localAvatarIndex;
        Texture2D _localCustomTex;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public int ActiveTab { get; private set; } = TabOverview;
        public Button BackButton { get; private set; }
        public Button EditIdentityButton { get; private set; }
        public Button ChangeKidButton { get; private set; }
        public Button HistoryButton { get; private set; }
        public Button FullHistoryButton { get; private set; }
        public Button SaveNameButton { get; private set; }
        public Button TakeSelfieButton { get; private set; }
        public Button RemovePhotoButton => _removePhotoButton;
        public InputField PlayerNameInput => _nameInput;

        public ProfileScreenView(
            Transform parent,
            DataSource data = null,
            Action onBack = null,
            Action onOpenHistory = null,
            Action onOpenKids = null,
            Action onOpenIdCamera = null,
            HomeSheetsView sheets = null,
            AudioHub audio = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data;
            _onBack = onBack;
            _onOpenHistory = onOpenHistory;
            _onOpenKids = onOpenKids;
            _onOpenIdCamera = onOpenIdCamera;
            _sheets = sheets;
            _audio = audio;
            _stage = new HeroStage { ShowPedestal = true };

            BuildKidMetadataLookup();

            Root = Ui.Fill("ProfileScreen", parent);
            BuildBackdrop();

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null)
            {
                SafeRoot = Ui.Fill("ProfileSafeContent", Root);
                _safeArea = parentSafe;
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
                _safeArea.Apply();
            }

            BuildHeader();
            BuildBody();
            BuildNavRail();
            BuildOverviewPane();
            BuildHistoryPane();
            BuildIdentityPane();

            var lifecycle = Root.gameObject.AddComponent<ProfileLifecycle>();
            lifecycle.Owner = this;

            if (_sheets != null)
                _sheets.ProfileChanged += HandleExternalProfileChanged;

            SetActiveTab(TabOverview);
            Refresh();
            RefreshLayout();
            Root.gameObject.SetActive(false);
        }

        public void Show() => Show(TabOverview);

        public void Show(int initialTab)
        {
            if (_disposed || Root == null) return;
            Root.gameObject.SetActive(true);
            SetActiveTab(initialTab);
            Refresh();
            if (_safeArea != null)
                _safeArea.ForceApply();
            RefreshLayout();
            if (!Application.isPlaying)
                _stage?.RenderNow();
        }

        public void Hide()
        {
            if (Root != null)
                Root.gameObject.SetActive(false);
        }

        public bool HandleBack()
        {
            if (_sheets != null && _sheets.IsOpen)
                return _sheets.Close();
            if (ActiveTab != TabOverview)
            {
                SetActiveTab(TabOverview);
                return true;
            }
            return false;
        }

        public void SetActiveTab(int tab)
        {
            ActiveTab = Mathf.Clamp(tab, TabOverview, TabIdentity);
            if (_overviewPane != null) _overviewPane.gameObject.SetActive(ActiveTab == TabOverview);
            if (_historyPane != null) _historyPane.gameObject.SetActive(ActiveTab == TabHistory);
            if (_identityPane != null) _identityPane.gameObject.SetActive(ActiveTab == TabIdentity);

            for (int i = 0; i < _tabs.Length; i++)
            {
                TabUi t = _tabs[i];
                if (t == null) continue;
                bool active = i == ActiveTab;
                ApplyGlassSurface(
                    t.Plate,
                    active ? ObsidianSolid : new Color(0.055f, 0.075f, 0.102f, 0.45f),
                    active ? TitaniumBorderStrong : TitaniumBorder,
                    active ? Gold : Color.clear,
                    cornerCut: 6f,
                    accentW: active ? 3f : 0f);
                if (t.AccentRail != null) t.AccentRail.gameObject.SetActive(false);
                if (t.Icon != null) t.Icon.color = active ? Cream : Muted;
                if (t.Label != null) Ui.SetTextColor(t.Label, active ? Cream : Muted);
            }

            if (ActiveTab == TabIdentity && _nameInput != null)
            {
                string currentName = ResolvePlayerName(AppSession.Current);
                _nameInput.text = currentName;
                UpdateNameCounter(currentName);
                UpdateLivePreviewName(currentName);
                SetNameHintVisible(false);
            }

            if (ActiveTab == TabOverview && !Application.isPlaying)
                _stage?.RenderNow();

            RefreshLayout();
        }

        public void SelectAvatar(int index)
        {
            int clamped = Mathf.Clamp(index, 0, HomeSheetsView.AvatarIds.Length - 1);
            if (_sheets != null)
            {
                // HomeSheetsView.SelectAvatar fires ProfileChanged -> HandleExternalProfileChanged -> Refresh().
                _sheets.SelectAvatar(clamped);
                return;
            }

            _hasLocalAvatarIndex = true;
            _localAvatarIndex = clamped;
            int sessionClamped = Mathf.Clamp(clamped, 0, AppSession.AvatarPhotoIds.Length - 1);
            AppSession.Current.AvatarIndex = sessionClamped;
            AppSession.Current.HasCustomIdPhoto = false;
            PlayerPrefs.SetInt(HomeSheetsView.AvatarPrefKey, clamped);
            PlayerPrefs.SetInt(HomeSheetsView.CustomPhotoPrefKey, 0);
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
            Refresh();
        }

        public bool SavePlayerName()
        {
            string raw = _nameInput != null ? _nameInput.text : AppSession.Current.PlayerName;
            string cleaned = AppSession.CleanName((raw ?? "").ToUpperInvariant());
            if (string.IsNullOrEmpty(cleaned))
            {
                if (_nameHintText != null) _nameHintText.text = "Pick a name!";
                SetNameHintVisible(true);
                _audio?.PlayUiTap("deny");
                Haptics.CancelTick();
                return false;
            }

            AppSession.Current.PlayerName = cleaned;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            if (_nameInput != null)
            {
                _nameInput.text = cleaned;
                UpdateNameCounter(cleaned);
            }
            SetNameHintVisible(false);
            _audio?.PlayUiTap("confirm");
            Haptics.LightTap();
            Refresh();
            return true;
        }

        public void Refresh()
        {
            if (_disposed) return;

            AppSession session = AppSession.Current;
            string playerName = ResolvePlayerName(session);
            string kidId = string.IsNullOrWhiteSpace(session.SelectedCharacter)
                ? AppSession.DefaultCharacter
                : session.SelectedCharacter.ToLowerInvariant();

            string kidName = ResolveKidDisplayName(kidId);
            string kidRole = ResolveKidRole(kidId);
            string kidPurok = ResolveKidPurok(kidId);

            int totalMatches = session.TotalMatches;
            int totalWins = session.TotalWins;
            int accountLevel = 1 + (totalMatches / 2);
            float levelProgress = totalMatches > 0 ? ((totalMatches % 2) * 0.5f) : 0f;
            int honorLevel = 1 + Mathf.Clamp(totalWins / 5, 0, 4);

            // Sync 3D favorite kid showcase
            _stage.SetKid(kidId);
            if (!Application.isPlaying)
                _stage.RenderNow();

            // Header & NavRail (NavRail focuses on Account Level + XP + Matches; no duplicate Honor readout)
            if (_playerIdChipText != null)
                _playerIdChipText.text = "#KLY-" + playerName;
            if (_navLevelText != null)
                _navLevelText.text = "LV. " + accountLevel.ToString(CultureInfo.InvariantCulture);
            if (_navLevelFill != null)
            {
                float fill = Mathf.Clamp01(levelProgress);
                _navLevelFill.anchorMax = new Vector2(fill, 1f);
                _navLevelFill.gameObject.SetActive(fill > 0.001f);
            }
            if (_navMatchesText != null)
                _navMatchesText.text = totalMatches == 1
                    ? "1 MATCH"
                    : totalMatches.ToString(CultureInfo.InvariantCulture) + " MATCHES";

            // Hero column
            if (_heroRoleIcon != null)
                _heroRoleIcon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(kidRole));
            if (_heroKicker != null)
                _heroKicker.text = string.IsNullOrEmpty(kidPurok) ? kidRole : kidRole + "  \u00B7  " + kidPurok;
            if (_heroName != null)
                _heroName.text = kidName;

            // CODM Calling Card banner & Identity live preview
            ApplyAvatarGraphic(_bannerAvatar);
            ApplyAvatarGraphic(_identityPreviewImg);
            string levelStr = accountLevel.ToString(CultureInfo.InvariantCulture);
            if (_bannerLevelText != null)
                _bannerLevelText.text = levelStr;
            if (_identityPreviewLevel != null)
                _identityPreviewLevel.text = levelStr;
            if (_bannerKicker != null)
                _bannerKicker.text = string.IsNullOrEmpty(kidPurok) ? kidRole : kidPurok + "  \u00B7  " + kidRole;
            if (_bannerName != null)
                _bannerName.text = playerName;
            if (_bannerHonorText != null)
                _bannerHonorText.text = "HONOR LV. " + honorLevel.ToString(CultureInfo.InvariantCulture);

            // Streak & career badge (short, 4:3-safe copy)
            int streak = ComputeCurrentWinStreak(session.History);
            if (_bannerCareerText != null)
                _bannerCareerText.text = streak >= 2
                    ? streak.ToString(CultureInfo.InvariantCulture) + "W STREAK"
                    : (totalMatches > 0 ? "CAREER" : "NEW");

            // 4-KPI career row + honest per-kid win-rate sub-caption (SPEC §8)
            if (_statMatchesValue != null)
                _statMatchesValue.text = totalMatches.ToString(CultureInfo.InvariantCulture);
            if (_statWinRateValue != null)
                _statWinRateValue.text = totalMatches > 0
                    ? Mathf.RoundToInt(session.WinRate01 * 100f).ToString(CultureInfo.InvariantCulture) + "%"
                    : "--";
            if (_statWinsValue != null)
                _statWinsValue.text = totalWins.ToString(CultureInfo.InvariantCulture);

            string topKid = FindMostPlayedKid(session.History, kidId, out int topKidMatches, out int topKidWins);
            if (_statTopKidValue != null)
                _statTopKidValue.text = ResolveKidDisplayName(topKid);
            if (_statTopKidSub != null)
            {
                if (topKidMatches > 0)
                {
                    int wr = Mathf.RoundToInt((topKidWins * 100f) / topKidMatches);
                    _statTopKidSub.text = topKidMatches.ToString(CultureInfo.InvariantCulture)
                        + "M \u00B7 " + wr.ToString(CultureInfo.InvariantCulture) + "% WR";
                }
                else
                {
                    _statTopKidSub.text = "MAIN KID";
                }
            }

            // Favourite roles chart, recent form, inline last-20 history & identity studio
            RefreshRoleChart(session.History, kidRole);
            RefreshRecentForm(session.History);
            RefreshHistoryTab(session.History);
            RefreshIdentityTab(playerName);
        }

        public void RefreshLayout()
        {
            if (_disposed || _body == null || _navRail == null || _contentStage == null) return;

            Canvas canvas = Root != null ? Root.GetComponentInParent<Canvas>() : null;
            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(canvas);
            float safeW = Mathf.Max(640f, safe.x);
            float safeH = Mathf.Max(420f, safe.y);
            float minTouch = ScreenFitMetrics.ClampTouchSize(HitHeight, ScreenFitMetrics.MinTouchDp, canvas);

            float bodyW = Mathf.Max(600f, safeW - 2f * Gutter);
            float bodyH = Mathf.Max(300f, safeH - BodyTopInset - BodyBottomInset);

            if (_backBtnRect != null)
                _backBtnRect.sizeDelta = new Vector2(Mathf.Max(126f, minTouch), minTouch);

            float navW = safeW < 1040f ? 168f : 188f;
            _navRail.anchorMin = new Vector2(0f, 0f);
            _navRail.anchorMax = new Vector2(0f, 1f);
            _navRail.pivot = new Vector2(0f, 0.5f);
            _navRail.anchoredPosition = Vector2.zero;
            _navRail.sizeDelta = new Vector2(navW, 0f);

            float stageGap = 14f;
            _contentStage.anchorMin = Vector2.zero;
            _contentStage.anchorMax = Vector2.one;
            _contentStage.offsetMin = new Vector2(navW + stageGap, 0f);
            _contentStage.offsetMax = Vector2.zero;

            float stageW = Mathf.Max(480f, bodyW - navW - stageGap);

            // Overview columns & touch floors
            if (_heroCol != null && _careerCol != null)
            {
                float heroFrac = stageW < 820f ? 0.345f : 0.365f;
                float colGap = 14f;
                _heroCol.anchorMin = new Vector2(0f, 0f);
                _heroCol.anchorMax = new Vector2(heroFrac, 1f);
                _heroCol.offsetMin = Vector2.zero;
                _heroCol.offsetMax = new Vector2(-colGap * 0.5f, 0f);

                _careerCol.anchorMin = new Vector2(heroFrac, 0f);
                _careerCol.anchorMax = new Vector2(1f, 1f);
                _careerCol.offsetMin = new Vector2(colGap * 0.5f, 0f);
                _careerCol.offsetMax = Vector2.zero;
            }

            if (_changeKidBtnRect != null)
                _changeKidBtnRect.sizeDelta = new Vector2(-20f, minTouch);
            if (_editBtnRect != null)
                _editBtnRect.sizeDelta = new Vector2(Mathf.Max(100f, minTouch), minTouch);
            if (_historyBtnRect != null)
                _historyBtnRect.sizeDelta = new Vector2(0f, minTouch);

            // History disjoint top bar & log card
            if (_historyTopBar != null)
                _historyTopBar.sizeDelta = new Vector2(0f, minTouch);
            if (_fullHistoryBtnRect != null)
                _fullHistoryBtnRect.sizeDelta = new Vector2(0f, minTouch);
            if (_historyLogCard != null)
                _historyLogCard.offsetMax = new Vector2(0f, -(minTouch + 8f));

            // Identity columns + dynamic minTouch stack + 4x3 Avatar grid math (guarantees >= 48 dp on all 4 aspects)
            if (_callingCol != null && _avatarCol != null)
            {
                float leftFrac = stageW < 820f ? 0.41f : 0.42f;
                float colGap = 14f;
                _callingCol.anchorMin = new Vector2(0f, 0f);
                _callingCol.anchorMax = new Vector2(leftFrac, 1f);
                _callingCol.offsetMin = Vector2.zero;
                _callingCol.offsetMax = new Vector2(-colGap * 0.5f, 0f);

                _avatarCol.anchorMin = new Vector2(leftFrac, 0f);
                _avatarCol.anchorMax = new Vector2(1f, 1f);
                _avatarCol.offsetMin = new Vector2(colGap * 0.5f, 0f);
                _avatarCol.offsetMax = Vector2.zero;

                if (_callingCardRect != null)
                {
                    _callingCardRect.offsetMin = new Vector2(0f, minTouch * 2f + 14f);
                    _callingCardRect.offsetMax = Vector2.zero;
                }
                float cardH = Mathf.Max(220f, bodyH - (minTouch * 2f + 14f));
                float previewH = Mathf.Clamp(cardH - minTouch - 48f, 88f, 114f);
                if (_livePreviewRect != null)
                {
                    _livePreviewRect.anchoredPosition = new Vector2(0f, -10f);
                    _livePreviewRect.sizeDelta = new Vector2(-24f, previewH);
                }
                float hairlineY = -(10f + previewH + 8f);
                if (_callingHairlineRect != null)
                {
                    _callingHairlineRect.anchoredPosition = new Vector2(0f, hairlineY);
                    _callingHairlineRect.sizeDelta = new Vector2(-24f, 1f);
                }
                if (_captionRowRect != null)
                {
                    _captionRowRect.anchoredPosition = new Vector2(0f, hairlineY - 8f);
                    _captionRowRect.sizeDelta = new Vector2(-24f, 22f);
                }
                if (_nameFieldBoxRect != null)
                {
                    _nameFieldBoxRect.anchoredPosition = new Vector2(0f, hairlineY - 32f);
                    _nameFieldBoxRect.sizeDelta = new Vector2(-24f, minTouch);
                }
                if (_saveBtnRect != null)
                {
                    _saveBtnRect.anchoredPosition = new Vector2(0f, minTouch + 6f);
                    _saveBtnRect.sizeDelta = new Vector2(0f, minTouch);
                }
                if (_photoRowRect != null)
                    _photoRowRect.sizeDelta = new Vector2(0f, minTouch);

                float avatarColW = stageW * (1f - leftFrac) - colGap * 0.5f;
                LayoutAvatarGrid(avatarColW, bodyH, minTouch);
                LayoutPhotoActionButtons();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_sheets != null)
                _sheets.ProfileChanged -= HandleExternalProfileChanged;
            ClearHistoryRows();
            _stage?.Dispose();
            if (_localCustomTex != null)
            {
                KalyeahResources.Kill(_localCustomTex);
                _localCustomTex = null;
            }
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        void DisposeFromLifecycle()
        {
            if (_disposed) return;
            _disposed = true;
            if (_sheets != null)
                _sheets.ProfileChanged -= HandleExternalProfileChanged;
            _stage?.Dispose();
            if (_localCustomTex != null)
            {
                KalyeahResources.Kill(_localCustomTex);
                _localCustomTex = null;
            }
        }

        // =================================================================================================================
        // Build: Backdrop, Header, NavRail
        // =================================================================================================================

        void BuildBackdrop()
        {
            RectTransform backdrop = Ui.Fill("Backdrop", Root);
            Image baseBg = backdrop.gameObject.AddComponent<Image>();
            baseBg.color = Shade;
            baseBg.raycastTarget = false;

            Texture2D stall = KalyeahResources.Texture("ui/home/stall");
            if (stall != null)
            {
                var bgGo = new GameObject("StallArt", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                bgGo.transform.SetParent(backdrop, false);
                RectTransform bgR = bgGo.GetComponent<RectTransform>();
                Stretch(bgR, 0f, 0f, 0f, 0f);
                RawImage bgImg = bgGo.GetComponent<RawImage>();
                bgImg.texture = stall;
                bgImg.color = new Color(0.70f, 0.72f, 0.78f, 0.36f);
                bgImg.raycastTarget = false;
                AspectRatioFitter fit = bgGo.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = stall.height > 0 ? (float)stall.width / stall.height : 16f / 9f;
            }

            Image wash = Ui.MakeImage("Wash", backdrop, BackdropWash, round: false, raycast: false);
            Stretch(wash.rectTransform, 0f, 0f, 0f, 0f);

            var weaveGo = new GameObject("Weave", typeof(RectTransform), typeof(RawImage));
            weaveGo.transform.SetParent(backdrop, false);
            RectTransform weaveR = weaveGo.GetComponent<RectTransform>();
            Stretch(weaveR, 0f, 0f, 0f, 0f);
            RawImage weave = weaveGo.GetComponent<RawImage>();
            weave.texture = MenuArt.Weave();
            weave.color = new Color(0.65f, 0.85f, 0.95f, 0.025f);
            weave.uvRect = new Rect(0f, 0f, 20f, 12f);
            weave.raycastTarget = false;

            Image warmGlow = Ui.MakeImage("WarmGlow", backdrop, new Color(1f, 0.80f, 0.48f, 0.11f), round: false, raycast: false);
            warmGlow.sprite = MenuArt.Glow();
            Place(warmGlow.rectTransform, new Vector2(0.32f, 0.72f), new Vector2(0.32f, 0.72f), Vector2.zero, new Vector2(1180f, 780f));

            Image coolGlow = Ui.MakeImage("CoolGlow", backdrop, new Color(0.24f, 0.68f, 0.86f, 0.08f), round: false, raycast: false);
            coolGlow.sprite = MenuArt.Glow();
            Place(coolGlow.rectTransform, new Vector2(0.84f, 0.24f), new Vector2(0.84f, 0.24f), Vector2.zero, new Vector2(960f, 660f));
        }

        void BuildHeader()
        {
            _headerBar = Ui.Panel("HeaderBar", SafeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));

            // Back button (>= 48 dp hit box, sleek 52 px visual pill; pops straight back to Home)
            _backBtnRect = Ui.Panel("BackButton", _headerBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, 0f), new Vector2(126f, HitHeight));
            Image backHitImg = _backBtnRect.gameObject.AddComponent<Image>();
            backHitImg.color = Clear;
            backHitImg.raycastTarget = true;

            RectTransform backVisual = Ui.Panel("Visual", _backBtnRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
            Image backPlate = MakeGlassPanel("Plate", backVisual, ObsidianCard, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);
            Image backChevron = Ui.MakeImage("Icon", backVisual, Cream);
            backChevron.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            Place(backChevron.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(16f, 16f));
            backChevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

            Text backLabel = MakeDisplayText("Label", backVisual, "BACK", 21, Cream, TextAnchor.MiddleCenter);
            Ui.ClearOutline(backLabel);
            FitSingleLine(backLabel, 12, 21);
            Stretch(backLabel.rectTransform, 32f, 0f, -10f, 0f);
            BackButton = AttachButton(_backBtnRect.gameObject, backPlate, OnHeaderBackClicked, 0.94f);

            // Title accent rail + PROFILE heading
            RectTransform rail = Ui.Panel("TitleRail", _headerBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + 142f, 0f), new Vector2(4f, 34f));
            Image railImg = rail.gameObject.AddComponent<Image>();
            railImg.color = Gold;
            railImg.raycastTarget = false;

            Text title = MakeDisplayText("Title", _headerBar, "PROFILE", 38, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(title);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter + 158f, -1f), new Vector2(240f, 56f));
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Right-side Player ID tactical chip (concise #KLY-<IGN>, non-Selectable, 4:3-safe even at 12 chars)
            RectTransform idChip = Ui.Panel("PlayerIdChip", _headerBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Gutter, 0f), new Vector2(256f, 40f));
            MakeGlassPanel("Bg", idChip, ObsidianSolid, TitaniumBorder, Gold, cornerCut: 5f, accentW: 3f);
            _playerIdChipText = MakeDisplayText("Label", idChip, "#KLY-GABO", 18, Cream, TextAnchor.MiddleCenter);
            Ui.ClearOutline(_playerIdChipText);
            FitSingleLine(_playerIdChipText, 11, 18);
            Stretch(_playerIdChipText.rectTransform, 10f, 0f, -10f, 0f);

            // Bottom subtle rule
            Image rule = Ui.MakeImage("Rule", _headerBar, Hairline, round: false, raycast: false);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.offsetMin = new Vector2(Gutter, 0f);
            rule.rectTransform.offsetMax = new Vector2(-Gutter, 1f);
        }

        void BuildBody()
        {
            _body = Ui.Fill("Body", SafeRoot);
            _body.offsetMin = new Vector2(Gutter, BodyBottomInset);
            _body.offsetMax = new Vector2(-Gutter, -BodyTopInset);

            _navRail = Ui.Panel("NavRail", _body, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(184f, 0f));
            _contentStage = Ui.Fill("ContentStage", _body);
            _contentStage.offsetMin = new Vector2(198f, 0f);
            _contentStage.offsetMax = Vector2.zero;
        }

        void BuildNavRail()
        {
            MakeGlassPanel("RailBg", _navRail, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            // Top kicker (>= 15 px nominal)
            Text railKicker = MakeDisplayText("Kicker", _navRail, "PLAYER HUB", 15, Muted, TextAnchor.MiddleLeft);
            Ui.ClearOutline(railKicker);
            FitSingleLine(railKicker, 10, 15);
            railKicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            railKicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            railKicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            railKicker.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            railKicker.rectTransform.sizeDelta = new Vector2(-24f, 22f);

            // 3 vertical editorial tabs (>= 96 px hit height, 62 px visual smoked-obsidian plate)
            float startY = -36f;
            float gap = 6f;
            for (int i = 0; i < 3; i++)
            {
                int tabIndex = i;
                RectTransform hit = Ui.Panel("Tab_" + TabIds[i], _navRail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, startY - i * (HitHeight + gap)), new Vector2(-12f, HitHeight));
                Image hitImg = hit.gameObject.AddComponent<Image>();
                hitImg.color = Clear;
                hitImg.raycastTarget = true;

                RectTransform visual = Ui.Panel("Visual", hit, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 62f));
                Image plate = MakeGlassPanel("Plate", visual, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

                RectTransform accentRect = Ui.MakeAccentRail("AccentRail", visual, Gold, width: 3f, verticalInset: 10f);
                Image accentImg = accentRect.GetComponent<Image>();

                Image icon = Ui.MakeImage("Icon", visual, Cream);
                icon.sprite = MenuArt.Glyph(TabGlyphs[i]);
                Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(22f, 22f));

                Text label = MakeDisplayText("Label", visual, TabTitles[i], 20, Cream, TextAnchor.MiddleLeft);
                Ui.ClearOutline(label);
                Stretch(label.rectTransform, 46f, 0f, -8f, 0f);
                FitSingleLine(label, 12, 20);

                AttachButton(hit.gameObject, plate, () =>
                {
                    TapFeedback();
                    SetActiveTab(tabIndex);
                }, 0.96f);

                _tabs[i] = new TabUi
                {
                    Plate = plate,
                    AccentRail = accentImg,
                    Icon = icon,
                    Label = label,
                };
            }

            // Bottom hairline-separated Account Level + XP progress + Match count (de-boxed!)
            RectTransform crestCard = Ui.Panel("AccountCrest", _navRail, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-16f, 120f));
            Image crestRule = Ui.MakeImage("TopRule", crestCard, Hairline, round: false, raycast: false);
            crestRule.rectTransform.anchorMin = new Vector2(0f, 1f);
            crestRule.rectTransform.anchorMax = new Vector2(1f, 1f);
            crestRule.rectTransform.offsetMin = new Vector2(6f, -1f);
            crestRule.rectTransform.offsetMax = new Vector2(-6f, 0f);

            Text acctKicker = MakeDisplayText("AcctKicker", crestCard, "ACCOUNT", 15, Muted, TextAnchor.MiddleLeft);
            Ui.ClearOutline(acctKicker);
            FitSingleLine(acctKicker, 10, 15);
            acctKicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            acctKicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            acctKicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            acctKicker.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            acctKicker.rectTransform.sizeDelta = new Vector2(-20f, 20f);

            _navLevelText = MakeDisplayText("LevelText", crestCard, "LV. 1", 28, GoldLight, TextAnchor.MiddleLeft);
            FitSingleLine(_navLevelText, 16, 28);
            _navLevelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _navLevelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _navLevelText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _navLevelText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            _navLevelText.rectTransform.sizeDelta = new Vector2(-20f, 34f);

            RectTransform progTrack = Ui.Panel("ProgressTrack", crestCard, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(-20f, 7f));
            Image trackImg = progTrack.gameObject.AddComponent<Image>();
            trackImg.sprite = MenuArt.RoundSolid(4);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = TrackBg;
            trackImg.raycastTarget = false;

            _navLevelFill = Ui.Fill("Fill", progTrack);
            _navLevelFill.anchorMax = new Vector2(0f, 1f);
            Image fillImg = _navLevelFill.gameObject.AddComponent<Image>();
            fillImg.sprite = MenuArt.RoundSolid(4);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Gold;
            fillImg.raycastTarget = false;
            _navLevelFill.gameObject.SetActive(false);

            Image matchGlyph = Ui.MakeImage("MatchesIcon", crestCard, MenuArt.TealLight);
            matchGlyph.sprite = MenuArt.Glyph(MenuArt.GlyphKind.History);
            Place(matchGlyph.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 22f), new Vector2(16f, 16f));

            _navMatchesText = MakeDisplayText("MatchesText", crestCard, "0 MATCHES", 15, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_navMatchesText);
            _navMatchesText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _navMatchesText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _navMatchesText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _navMatchesText.rectTransform.anchoredPosition = new Vector2(12f, 10f);
            _navMatchesText.rectTransform.sizeDelta = new Vector2(-36f, 24f);
            FitSingleLine(_navMatchesText, 10, 15);
        }

        // =================================================================================================================
        // Tab 0: Overview Pane (MLBB x Wild Rift x CODM Showcase)
        // =================================================================================================================

        void BuildOverviewPane()
        {
            _overviewPane = Ui.Fill("OverviewPane", _contentStage);

            // Left Column: 3D Favorite Kid Showcase (sleek smoked-obsidian glass, zero nested chamfers)
            _heroCol = Ui.Panel("HeroCol", _overviewPane, Vector2.zero, new Vector2(0.36f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            MakeGlassPanel("StageCard", _heroCol, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            Image spotlight = Ui.MakeImage("Spotlight", _heroCol, new Color(1.00f, 0.94f, 0.84f, 0.16f), round: false, raycast: false);
            spotlight.sprite = MenuArt.CardSpotlight();
            Stretch(spotlight.rectTransform, 6f, 6f, -6f, -6f);

            // 3D HeroStage RawImage lifted cleanly above nameplate (HeroStage renders its own grounded 3D pedestal)
            RectTransform heroSlot = Ui.Fill("HeroSlot", _heroCol);
            heroSlot.offsetMin = new Vector2(6f, 150f);
            heroSlot.offsetMax = new Vector2(-6f, -24f);
            var heroGo = new GameObject("Hero3D", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            heroGo.transform.SetParent(heroSlot, false);
            _heroRaw = heroGo.GetComponent<RawImage>();
            _heroRaw.texture = _stage.Texture;
            _heroRaw.color = Color.white;
            _heroRaw.raycastTarget = true;
            Stretch(_heroRaw.rectTransform, 0f, 0f, 0f, 0f);
            AspectRatioFitter heroFit = heroGo.GetComponent<AspectRatioFitter>();
            heroFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            heroFit.aspectRatio = _stage.Texture != null && _stage.Texture.height > 0
                ? (float)_stage.Texture.width / _stage.Texture.height
                : 0.75f;
            _stage.AttachOrbitDrag(_heroRaw.rectTransform);

            // Top-left MAIN KID tag (<= 116 px) + top-right 360 DRAG cue (<= 90 px): sleek obsidian hairlines
            RectTransform mainTag = Ui.Panel("MainKidTag", _heroCol, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(116f, 32f));
            MakeGlassPanel("Bg", mainTag, ObsidianSolid, TitaniumBorder, Gold, cornerCut: 4f, accentW: 2.5f);
            _heroRoleIcon = Ui.MakeImage("RoleIcon", mainTag, Cream);
            _heroRoleIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Seeker);
            Place(_heroRoleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(15f, 15f));
            Text mainTagLabel = MakeDisplayText("Label", mainTag, "MAIN KID", 14, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(mainTagLabel);
            FitSingleLine(mainTagLabel, 10, 14);
            Stretch(mainTagLabel.rectTransform, 30f, 0f, -6f, 0f);

            RectTransform orbitCue = Ui.Panel("OrbitCue", _heroCol, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(90f, 32f));
            MakeGlassPanel("Bg", orbitCue, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);
            Text orbitLabel = MakeDisplayText("Label", orbitCue, "360\u00B0 DRAG", 14, Muted, TextAnchor.MiddleCenter);
            Ui.ClearOutline(orbitLabel);
            FitSingleLine(orbitLabel, 10, 14);
            Stretch(orbitLabel.rectTransform, 6f, 0f, -6f, 0f);

            // Grounded dark scrim + compact nameplate strictly beneath HeroSlot feet
            Image scrim = Ui.MakeImage("BottomScrim", _heroCol, new Color(1f, 1f, 1f, 0.92f), round: false, raycast: false);
            scrim.sprite = MenuArt.BottomScrim();
            scrim.rectTransform.anchorMin = new Vector2(0f, 0f);
            scrim.rectTransform.anchorMax = new Vector2(1f, 0f);
            scrim.rectTransform.pivot = new Vector2(0.5f, 0f);
            scrim.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            scrim.rectTransform.sizeDelta = new Vector2(-8f, 168f);

            _heroKicker = MakeDisplayText("HeroKicker", _heroCol, "SEEKER  \u00B7  PALENGKE", 15, Gold, TextAnchor.MiddleCenter);
            Ui.ClearOutline(_heroKicker);
            FitSingleLine(_heroKicker, 10, 15);
            _heroKicker.rectTransform.anchorMin = new Vector2(0f, 0f);
            _heroKicker.rectTransform.anchorMax = new Vector2(1f, 0f);
            _heroKicker.rectTransform.pivot = new Vector2(0.5f, 0f);
            _heroKicker.rectTransform.anchoredPosition = new Vector2(0f, 134f);
            _heroKicker.rectTransform.sizeDelta = new Vector2(-24f, 22f);

            _heroName = MakeDisplayText("HeroName", _heroCol, "KARDING", 34, Cream, TextAnchor.MiddleCenter);
            FitSingleLine(_heroName, 18, 34);
            _heroName.rectTransform.anchorMin = new Vector2(0f, 0f);
            _heroName.rectTransform.anchorMax = new Vector2(1f, 0f);
            _heroName.rectTransform.pivot = new Vector2(0.5f, 0f);
            _heroName.rectTransform.anchoredPosition = new Vector2(0f, 96f);
            _heroName.rectTransform.sizeDelta = new Vector2(-24f, 38f);

            // Bottom Change Kid CTA (>= 48 dp hit box, sleek 48 px visual button cleanly beneath nameplate)
            ChangeKidButton = MakeTacticalButton(
                "ChangeKidButton",
                _heroCol,
                "CHANGE KID",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Chevron,
                glyphOnRight: true,
                onClick: () =>
                {
                    TapFeedback();
                    _onOpenKids?.Invoke();
                },
                out _changeKidBtnRect);
            _changeKidBtnRect.anchorMin = new Vector2(0f, 0f);
            _changeKidBtnRect.anchorMax = new Vector2(1f, 0f);
            _changeKidBtnRect.pivot = new Vector2(0.5f, 0f);
            _changeKidBtnRect.anchoredPosition = Vector2.zero;
            _changeKidBtnRect.sizeDelta = new Vector2(-20f, HitHeight);

            // Right Column: CODM Calling Card Banner + 4-KPI Row + Role/Form Analytics Card
            _careerCol = Ui.Panel("CareerCol", _overviewPane, new Vector2(0.36f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

            BuildBannerCard();
            BuildStatsRow();
            BuildAnalyticsCard();
        }

        void BuildBannerCard()
        {
            RectTransform card = Ui.Panel("BannerCard", _careerCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 132f));
            MakeGlassPanel("Bg", card, ObsidianGlass, TitaniumBorderStrong, Gold, cornerCut: 8f, accentW: 3.5f);

            // Framed Avatar Portrait (clean KidBust portrait, 1 px titanium rim)
            RectTransform avatarFrame = Ui.Panel("AvatarFrame", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(86f, 86f));
            MakeGlassPanel("Well", avatarFrame, ObsidianSolid, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            RectTransform photoSlot = Ui.Fill("PhotoSlot", avatarFrame);
            photoSlot.offsetMin = new Vector2(3f, 3f);
            photoSlot.offsetMax = new Vector2(-3f, -3f);
            photoSlot.gameObject.AddComponent<RectMask2D>();
            var avGo = new GameObject("AvatarPhoto", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            avGo.transform.SetParent(photoSlot, false);
            _bannerAvatar = avGo.GetComponent<RawImage>();
            _bannerAvatar.uvRect = FullUv;
            _bannerAvatar.raycastTarget = false;
            Stretch(_bannerAvatar.rectTransform, 0f, 0f, 0f, 0f);
            AspectRatioFitter avFit = avGo.GetComponent<AspectRatioFitter>();
            avFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            avFit.aspectRatio = 1f;

            // Account level medallion at bottom-right of avatar
            _bannerLevelText = BuildLevelMedallion(avatarFrame, 30f, 15);

            // Middle identity block (leaves 114 px left and 116 px right -> 246 px wide on 1024x768)
            RectTransform info = Ui.Fill("IdentityInfo", card);
            info.offsetMin = new Vector2(114f, 10f);
            info.offsetMax = new Vector2(-116f, -10f);

            _bannerKicker = MakeDisplayText("Kicker", info, "PALENGKE  \u00B7  SEEKER", 15, GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_bannerKicker);
            FitSingleLine(_bannerKicker, 10, 15);
            _bannerKicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            _bannerKicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            _bannerKicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bannerKicker.rectTransform.anchoredPosition = Vector2.zero;
            _bannerKicker.rectTransform.sizeDelta = new Vector2(0f, 22f);

            _bannerName = MakeDisplayText("PlayerName", info, "GABO", 34, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_bannerName);
            FitSingleLine(_bannerName, 16, 34);
            _bannerName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _bannerName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _bannerName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bannerName.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            _bannerName.rectTransform.sizeDelta = new Vector2(0f, 46f);

            // Bottom micro chips row (Honor + concise Career/Streak badge, 30 px tall, sleek hairline obsidian pills)
            RectTransform chipRow = Ui.Panel("Badges", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 30f));

            RectTransform honorChip = Ui.Panel("HonorChip", chipRow, new Vector2(0f, 0f), new Vector2(0.52f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            MakeGlassPanel("Bg", honorChip, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);
            _bannerHonorText = MakeDisplayText("Label", honorChip, "HONOR LV. 1", 14, Cream, TextAnchor.MiddleCenter);
            Ui.ClearOutline(_bannerHonorText);
            FitSingleLine(_bannerHonorText, 10, 14);
            Stretch(_bannerHonorText.rectTransform, 6f, 0f, -6f, 0f);

            RectTransform careerChip = Ui.Panel("CareerChip", chipRow, new Vector2(0.55f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            MakeGlassPanel("Bg", careerChip, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);
            _bannerCareerText = MakeDisplayText("Label", careerChip, "NEW", 14, GoldLight, TextAnchor.MiddleCenter);
            Ui.ClearOutline(_bannerCareerText);
            FitSingleLine(_bannerCareerText, 10, 14);
            Stretch(_bannerCareerText.rectTransform, 6f, 0f, -6f, 0f);

            // Right Edit button (100 x 96 >= 48 dp on all 4 aspects)
            EditIdentityButton = MakeTacticalButton(
                "EditButton",
                card,
                "EDIT",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Camera,
                glyphOnRight: false,
                onClick: () =>
                {
                    TapFeedback();
                    SetActiveTab(TabIdentity);
                },
                out _editBtnRect);
            _editBtnRect.anchorMin = new Vector2(1f, 0.5f);
            _editBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _editBtnRect.pivot = new Vector2(1f, 0.5f);
            _editBtnRect.anchoredPosition = new Vector2(-10f, 0f);
            _editBtnRect.sizeDelta = new Vector2(100f, HitHeight);
        }

        void BuildStatsRow()
        {
            RectTransform row = Ui.Panel("StatsRow", _careerCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(0f, 114f));

            _statMatchesValue = MakeKpiTile(row, 0, "MATCHES", "0", Cream, accentTop: false, out _);
            _statWinRateValue = MakeKpiTile(row, 1, "WIN RATE", "--", Gold, accentTop: true, out _);
            _statWinsValue = MakeKpiTile(row, 2, "WINS", "0", SuccessGreen, accentTop: false, out _);
            _statTopKidValue = MakeKpiTile(row, 3, "TOP KID", "KARDING", Cream, accentTop: false, out _statTopKidSub, withSubCaption: true);
        }

        Text MakeKpiTile(
            RectTransform parent,
            int index,
            string caption,
            string initialValue,
            Color valueColor,
            bool accentTop,
            out Text subCaption,
            bool withSubCaption = false)
        {
            subCaption = null;
            float step = 0.25f;
            float left = index * step;
            float right = (index + 1) * step;
            RectTransform tile = Ui.Panel("Kpi_" + index, parent, new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tile.offsetMin = new Vector2(index == 0 ? 0f : 5f, 0f);
            tile.offsetMax = new Vector2(index == 3 ? 0f : -5f, 0f);

            MakeGlassPanel("Bg", tile, accentTop ? ObsidianSolid : ObsidianGlass, accentTop ? TitaniumBorderStrong : TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

            if (accentTop)
            {
                Image topBar = Ui.MakeImage("TopAccent", tile, Gold, round: false, raycast: false);
                topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
                topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
                topBar.rectTransform.offsetMin = new Vector2(12f, -3f);
                topBar.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }

            Text cap = MakeDisplayText("Caption", tile, caption, 15, Muted, TextAnchor.MiddleCenter);
            Ui.ClearOutline(cap);
            FitSingleLine(cap, 10, 15);
            cap.rectTransform.anchorMin = new Vector2(0f, 1f);
            cap.rectTransform.anchorMax = new Vector2(1f, 1f);
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            cap.rectTransform.sizeDelta = new Vector2(-12f, 22f);

            Text val = MakeDisplayText("Value", tile, initialValue, withSubCaption ? 28 : 32, valueColor, TextAnchor.MiddleCenter);
            FitSingleLine(val, 13, withSubCaption ? 28 : 32);
            val.rectTransform.anchorMin = new Vector2(0f, 0f);
            val.rectTransform.anchorMax = new Vector2(1f, 1f);
            val.rectTransform.offsetMin = new Vector2(8f, withSubCaption ? 30f : 12f);
            val.rectTransform.offsetMax = new Vector2(-8f, -32f);

            if (withSubCaption)
            {
                subCaption = MakeDisplayText("Sub", tile, "MAIN KID", 15, Muted, TextAnchor.MiddleCenter);
                Ui.ClearOutline(subCaption);
                FitSingleLine(subCaption, 10, 15);
                subCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
                subCaption.rectTransform.anchorMax = new Vector2(1f, 0f);
                subCaption.rectTransform.pivot = new Vector2(0.5f, 0f);
                subCaption.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                subCaption.rectTransform.sizeDelta = new Vector2(-12f, 22f);
            }

            return val;
        }

        void BuildAnalyticsCard()
        {
            RectTransform card = Ui.Fill("AnalyticsCard", _careerCol);
            card.offsetMin = Vector2.zero;
            card.offsetMax = new Vector2(0f, -266f);
            MakeGlassPanel("Bg", card, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            // Left 54%: Favourite Roles chart (neutral icons, zero role colors per SPEC §10)
            RectTransform rolesCol = Ui.Panel("RolesCol", card, Vector2.zero, new Vector2(0.54f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            rolesCol.offsetMin = new Vector2(16f, 12f);
            rolesCol.offsetMax = new Vector2(-10f, -12f);

            Text rolesHeader = MakeDisplayText("Header", rolesCol, "FAVOURITE ROLES", 19, GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(rolesHeader);
            FitSingleLine(rolesHeader, 10, 19);
            rolesHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            rolesHeader.rectTransform.anchorMax = new Vector2(0.64f, 1f);
            rolesHeader.rectTransform.pivot = new Vector2(0f, 1f);
            rolesHeader.rectTransform.anchoredPosition = Vector2.zero;
            rolesHeader.rectTransform.sizeDelta = new Vector2(0f, 26f);

            Text rolesSub = MakeDisplayText("Sub", rolesCol, "BY MATCHES", 15, Muted, TextAnchor.MiddleRight);
            Ui.ClearOutline(rolesSub);
            FitSingleLine(rolesSub, 10, 15);
            rolesSub.rectTransform.anchorMin = new Vector2(0.65f, 1f);
            rolesSub.rectTransform.anchorMax = new Vector2(1f, 1f);
            rolesSub.rectTransform.pivot = new Vector2(1f, 1f);
            rolesSub.rectTransform.anchoredPosition = Vector2.zero;
            rolesSub.rectTransform.sizeDelta = new Vector2(0f, 26f);

            float rowTop = -34f;
            float rowHeight = 46f;
            float rowGap = 7f;
            for (int i = 0; i < RoleOrder.Length; i++)
            {
                string role = RoleOrder[i];
                RectTransform row = Ui.Panel("Role_" + role, rolesCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, rowTop - i * (rowHeight + rowGap)), new Vector2(0f, rowHeight));

                Image icon = Ui.MakeImage("Icon", row, Cream);
                icon.sprite = MenuArt.Glyph(HomeScreenView.RoleGlyph(role));
                Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -11f), new Vector2(18f, 18f));

                Text nameLbl = MakeDisplayText("Name", row, role, 16, Cream, TextAnchor.MiddleLeft);
                Ui.ClearOutline(nameLbl);
                FitSingleLine(nameLbl, 10, 16);
                nameLbl.rectTransform.anchorMin = new Vector2(0f, 1f);
                nameLbl.rectTransform.anchorMax = new Vector2(0.72f, 1f);
                nameLbl.rectTransform.pivot = new Vector2(0f, 1f);
                nameLbl.rectTransform.anchoredPosition = new Vector2(28f, -1f);
                nameLbl.rectTransform.sizeDelta = new Vector2(-28f, 22f);

                Text pctLbl = MakeDisplayText("Pct", row, "0%", 15, Muted, TextAnchor.MiddleRight);
                Ui.ClearOutline(pctLbl);
                FitSingleLine(pctLbl, 10, 15);
                pctLbl.rectTransform.anchorMin = new Vector2(0.72f, 1f);
                pctLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
                pctLbl.rectTransform.pivot = new Vector2(1f, 1f);
                pctLbl.rectTransform.anchoredPosition = new Vector2(0f, -1f);
                pctLbl.rectTransform.sizeDelta = new Vector2(0f, 22f);

                RectTransform track = Ui.Panel("Track", row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(0f, 10f));
                Image trackImg = track.gameObject.AddComponent<Image>();
                trackImg.sprite = MenuArt.RoundSolid(4);
                trackImg.type = Image.Type.Sliced;
                trackImg.color = TrackBg;
                trackImg.raycastTarget = false;

                RectTransform fill = Ui.Fill("Fill", track);
                fill.anchorMax = new Vector2(0.08f, 1f);
                Image fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.sprite = MenuArt.RoundSolid(4);
                fillImg.type = Image.Type.Sliced;
                fillImg.color = NeutralBarFill;
                fillImg.raycastTarget = false;

                _roleBars[i] = new RoleBarUi
                {
                    Icon = icon,
                    NameLabel = nameLbl,
                    FillRect = fill,
                    FillImage = fillImg,
                    PctLabel = pctLbl,
                };
            }

            _rolesFootnote = Ui.MakeText("Footnote", rolesCol, "Play matches to chart roles", 15, Muted, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_rolesFootnote);
            FitSingleLine(_rolesFootnote, 10, 15);
            _rolesFootnote.rectTransform.anchorMin = new Vector2(0f, 0f);
            _rolesFootnote.rectTransform.anchorMax = new Vector2(1f, 0f);
            _rolesFootnote.rectTransform.pivot = new Vector2(0.5f, 0f);
            _rolesFootnote.rectTransform.anchoredPosition = Vector2.zero;
            _rolesFootnote.rectTransform.sizeDelta = new Vector2(0f, 24f);

            // Vertical divider hairline
            Image divider = Ui.MakeImage("Divider", card, Hairline, round: false, raycast: false);
            divider.rectTransform.anchorMin = new Vector2(0.545f, 0f);
            divider.rectTransform.anchorMax = new Vector2(0.545f, 1f);
            divider.rectTransform.offsetMin = new Vector2(0f, 18f);
            divider.rectTransform.offsetMax = new Vector2(1f, -18f);

            // Right 44%: Recent Form + Latest Match + History jump button
            RectTransform formCol = Ui.Panel("FormCol", card, new Vector2(0.56f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            formCol.offsetMin = new Vector2(8f, 8f);
            formCol.offsetMax = new Vector2(-14f, -12f);

            Text formHeader = MakeDisplayText("Header", formCol, "RECENT FORM", 19, GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(formHeader);
            FitSingleLine(formHeader, 10, 19);
            formHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            formHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            formHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            formHeader.rectTransform.anchoredPosition = Vector2.zero;
            formHeader.rectTransform.sizeDelta = new Vector2(0f, 26f);

            // 5 recent W/L form badges
            RectTransform badgesRow = Ui.Panel("FormBadges", formCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(0f, 42f));
            for (int i = 0; i < 5; i++)
            {
                float x0 = i * 0.20f;
                float x1 = (i + 1) * 0.20f;
                RectTransform chip = Ui.Panel("Badge_" + i, badgesRow, new Vector2(x0, 0f), new Vector2(x1, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                chip.offsetMin = new Vector2(i == 0 ? 0f : 3f, 0f);
                chip.offsetMax = new Vector2(i == 4 ? 0f : -3f, 0f);

                Image bg = Ui.MakeSliced("Bg", chip, MenuArt.RoundSolid(8), FormEmptyBg);
                Text lbl = MakeDisplayText("Text", chip, "-", 20, Muted, TextAnchor.MiddleCenter);
                Ui.ClearOutline(lbl);
                Stretch(lbl.rectTransform, 0f, 0f, 0f, 0f);
                _formBgs[i] = bg;
                _formTexts[i] = lbl;
            }

            // Latest Match summary (hairline-framed inside AnalyticsCard, de-boxed)
            RectTransform latestCard = Ui.Panel("LatestMatch", formCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(0f, 84f));
            MakeGlassPanel("Bg", latestCard, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

            _latestMatchTitle = MakeDisplayText("Title", latestCard, "NO MATCHES YET", 17, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_latestMatchTitle);
            FitSingleLine(_latestMatchTitle, 10, 17);
            _latestMatchTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _latestMatchTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _latestMatchTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _latestMatchTitle.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _latestMatchTitle.rectTransform.sizeDelta = new Vector2(-20f, 26f);

            _latestMatchDetail = Ui.MakeText("Detail", latestCard, "Play Blitz or Practice\nto log your career.", 15, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(_latestMatchDetail);
            _latestMatchDetail.resizeTextForBestFit = true;
            _latestMatchDetail.resizeTextMinSize = 10;
            _latestMatchDetail.resizeTextMaxSize = 15;
            _latestMatchDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _latestMatchDetail.verticalOverflow = VerticalWrapMode.Truncate;
            _latestMatchDetail.rectTransform.anchorMin = Vector2.zero;
            _latestMatchDetail.rectTransform.anchorMax = Vector2.one;
            _latestMatchDetail.rectTransform.offsetMin = new Vector2(10f, 8f);
            _latestMatchDetail.rectTransform.offsetMax = new Vector2(-10f, -34f);

            // Match History button (>= 48 dp hit height at bottom of formCol)
            HistoryButton = MakeTacticalButton(
                "HistoryButton",
                formCol,
                "MATCH HISTORY",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Chevron,
                glyphOnRight: true,
                onClick: () =>
                {
                    TapFeedback();
                    SetActiveTab(TabHistory);
                },
                out _historyBtnRect);
            _historyBtnRect.anchorMin = new Vector2(0f, 0f);
            _historyBtnRect.anchorMax = new Vector2(1f, 0f);
            _historyBtnRect.pivot = new Vector2(0.5f, 0f);
            _historyBtnRect.anchoredPosition = Vector2.zero;
            _historyBtnRect.sizeDelta = new Vector2(0f, HitHeight);
        }

        // =================================================================================================================
        // Tab 1: Inline Match History Pane (Last 20 matches per SPEC §8)
        // =================================================================================================================

        void BuildHistoryPane()
        {
            _historyPane = Ui.Fill("HistoryPane", _contentStage);

            // Disjoint top bar (>= 48 dp tall): 4 summary chips + Full Season Log button
            _historyTopBar = Ui.Panel("HistoryTopBar", _historyPane, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HitHeight));

            RectTransform summaryStrip = Ui.Panel("SummaryStrip", _historyTopBar, new Vector2(0f, 0.5f), new Vector2(0.73f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 68f));
            _histCountValue = MakeMiniSummaryChip(summaryStrip, 0, "RECENT 20", "0", Cream);
            _histWinsValue = MakeMiniSummaryChip(summaryStrip, 1, "WINS", "0", SuccessGreen);
            _histLossesValue = MakeMiniSummaryChip(summaryStrip, 2, "LOSSES", "0", Coral);
            _histRateValue = MakeMiniSummaryChip(summaryStrip, 3, "WIN RATE", "--", Gold);

            FullHistoryButton = MakeTacticalButton(
                "FullHistoryButton",
                _historyTopBar,
                "SEASON LOG",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Chevron,
                glyphOnRight: true,
                onClick: () =>
                {
                    TapFeedback();
                    _onOpenHistory?.Invoke();
                },
                out _fullHistoryBtnRect);
            _fullHistoryBtnRect.anchorMin = new Vector2(0.75f, 0.5f);
            _fullHistoryBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _fullHistoryBtnRect.pivot = new Vector2(0.5f, 0.5f);
            _fullHistoryBtnRect.anchoredPosition = Vector2.zero;
            _fullHistoryBtnRect.sizeDelta = new Vector2(0f, HitHeight);

            // Scrollable match log card (strictly below topBar)
            _historyLogCard = Ui.Fill("HistoryLogCard", _historyPane);
            _historyLogCard.offsetMin = Vector2.zero;
            _historyLogCard.offsetMax = new Vector2(0f, -104f);
            MakeGlassPanel("Bg", _historyLogCard, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            var scrollGo = new GameObject("HistoryScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_historyLogCard, false);
            RectTransform scrollR = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollR, 12f, 12f, -12f, -12f);

            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            vpGo.transform.SetParent(scrollR, false);
            RectTransform vpR = vpGo.GetComponent<RectTransform>();
            Stretch(vpR, 0f, 0f, 0f, 0f);
            Image vpImg = vpGo.GetComponent<Image>();
            vpImg.color = Color.white;
            vpImg.raycastTarget = true;
            vpGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(vpR, false);
            _historyContent = contentGo.GetComponent<RectTransform>();
            _historyContent.anchorMin = new Vector2(0f, 1f);
            _historyContent.anchorMax = new Vector2(1f, 1f);
            _historyContent.pivot = new Vector2(0.5f, 1f);
            _historyContent.anchoredPosition = Vector2.zero;
            _historyContent.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _historyScroll = scrollGo.GetComponent<ScrollRect>();
            _historyScroll.viewport = vpR;
            _historyScroll.content = _historyContent;
            _historyScroll.horizontal = false;
            _historyScroll.vertical = true;
            _historyScroll.movementType = ScrollRect.MovementType.Elastic;
            _historyScroll.scrollSensitivity = 28f;
        }

        Text MakeMiniSummaryChip(RectTransform parent, int index, string label, string value, Color valueColor)
        {
            float x0 = index * 0.25f;
            float x1 = (index + 1) * 0.25f;
            RectTransform chip = Ui.Panel("Sum_" + index, parent, new Vector2(x0, 0f), new Vector2(x1, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            chip.offsetMin = new Vector2(index == 0 ? 0f : 4f, 0f);
            chip.offsetMax = new Vector2(index == 3 ? 0f : -4f, 0f);
            MakeGlassPanel("Bg", chip, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

            Text cap = MakeDisplayText("Cap", chip, label, 15, Muted, TextAnchor.MiddleCenter);
            Ui.ClearOutline(cap);
            FitSingleLine(cap, 10, 15);
            cap.rectTransform.anchorMin = new Vector2(0f, 1f);
            cap.rectTransform.anchorMax = new Vector2(1f, 1f);
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            cap.rectTransform.sizeDelta = new Vector2(-8f, 20f);

            Text val = MakeDisplayText("Val", chip, value, 24, valueColor, TextAnchor.MiddleCenter);
            FitSingleLine(val, 13, 24);
            val.rectTransform.anchorMin = new Vector2(0f, 0f);
            val.rectTransform.anchorMax = new Vector2(1f, 1f);
            val.rectTransform.offsetMin = new Vector2(6f, 6f);
            val.rectTransform.offsetMax = new Vector2(-6f, -26f);
            return val;
        }

        // =================================================================================================================
        // Tab 2: Full-Screen Identity & Live Calling Card Studio
        // =================================================================================================================

        void BuildIdentityPane()
        {
            _identityPane = Ui.Fill("IdentityPane", _contentStage);

            // Left Column: Live Calling Card Preview + Player Tag Input + Save + Selfie
            _callingCol = Ui.Panel("CallingCol", _identityPane, Vector2.zero, new Vector2(0.42f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            _callingCardRect = Ui.Fill("CallingCard", _callingCol);
            _callingCardRect.offsetMin = new Vector2(0f, HitHeight * 2f + 14f);
            _callingCardRect.offsetMax = Vector2.zero;
            MakeGlassPanel("Bg", _callingCardRect, ObsidianGlass, TitaniumBorderStrong, Gold, cornerCut: 8f, accentW: 3f);

            // De-boxed Live Calling Card preview directly inside CallingCard (separated by a crisp 1 px titanium hairline)
            _livePreviewRect = Ui.Panel("LiveCardPreview", _callingCardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-24f, 112f));

            RectTransform previewFrame = Ui.Panel("PreviewFrame", _livePreviewRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(88f, 88f));
            MakeGlassPanel("Well", previewFrame, ObsidianSolid, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            RectTransform pSlot = Ui.Fill("Slot", previewFrame);
            pSlot.offsetMin = new Vector2(3f, 3f);
            pSlot.offsetMax = new Vector2(-3f, -3f);
            pSlot.gameObject.AddComponent<RectMask2D>();
            var pGo = new GameObject("PreviewPhoto", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            pGo.transform.SetParent(pSlot, false);
            _identityPreviewImg = pGo.GetComponent<RawImage>();
            _identityPreviewImg.uvRect = FullUv;
            _identityPreviewImg.raycastTarget = false;
            Stretch(_identityPreviewImg.rectTransform, 0f, 0f, 0f, 0f);
            AspectRatioFitter pFit = pGo.GetComponent<AspectRatioFitter>();
            pFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            pFit.aspectRatio = 1f;

            _identityPreviewLevel = BuildLevelMedallion(previewFrame, 28f, 14);

            RectTransform previewMeta = Ui.Fill("LiveMeta", _livePreviewRect);
            previewMeta.offsetMin = new Vector2(106f, 10f);
            previewMeta.offsetMax = new Vector2(-6f, -10f);

            Text liveKicker = MakeDisplayText("LiveKicker", previewMeta, "LIVE CARD", 14, GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(liveKicker);
            FitSingleLine(liveKicker, 10, 14);
            liveKicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            liveKicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            liveKicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            liveKicker.rectTransform.anchoredPosition = Vector2.zero;
            liveKicker.rectTransform.sizeDelta = new Vector2(0f, 20f);

            _identityLiveName = MakeDisplayText("LiveTag", previewMeta, "GABO", 28, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_identityLiveName);
            FitSingleLine(_identityLiveName, 12, 28);
            _identityLiveName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _identityLiveName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _identityLiveName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _identityLiveName.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            _identityLiveName.rectTransform.sizeDelta = new Vector2(0f, 40f);

            _identityLiveSub = MakeDisplayText("LiveId", previewMeta, "#KLY-GABO", 15, Muted, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_identityLiveSub);
            FitSingleLine(_identityLiveSub, 10, 15);
            _identityLiveSub.rectTransform.anchorMin = new Vector2(0f, 0f);
            _identityLiveSub.rectTransform.anchorMax = new Vector2(1f, 0f);
            _identityLiveSub.rectTransform.pivot = new Vector2(0.5f, 0f);
            _identityLiveSub.rectTransform.anchoredPosition = Vector2.zero;
            _identityLiveSub.rectTransform.sizeDelta = new Vector2(0f, 22f);

            // Crisp 1 px titanium hairline directly separating LIVE CARD from PLAYER TAG (tight editorial rhythm!)
            _callingHairlineRect = Ui.Panel("CallingHairline", _callingCardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(-24f, 1f));
            Image callLineImg = _callingHairlineRect.gameObject.AddComponent<Image>();
            callLineImg.color = Hairline;
            callLineImg.raycastTarget = false;

            // Caption + letter counter row directly beneath hairline (synced in RefreshLayout)
            _captionRowRect = Ui.Panel("CaptionRow", _callingCardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(-24f, 22f));

            _nameCaptionText = MakeDisplayText("Caption", _captionRowRect, "PLAYER TAG", 15, GoldLight, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_nameCaptionText);
            FitSingleLine(_nameCaptionText, 10, 15);
            _nameCaptionText.rectTransform.anchorMin = Vector2.zero;
            _nameCaptionText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            _nameCaptionText.rectTransform.offsetMin = Vector2.zero;
            _nameCaptionText.rectTransform.offsetMax = Vector2.zero;

            _nameHintText = MakeDisplayText("Hint", _captionRowRect, "Pick a name!", 15, ErrorRed, TextAnchor.MiddleLeft);
            Ui.ClearOutline(_nameHintText);
            FitSingleLine(_nameHintText, 10, 15);
            _nameHintText.rectTransform.anchorMin = Vector2.zero;
            _nameHintText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            _nameHintText.rectTransform.offsetMin = Vector2.zero;
            _nameHintText.rectTransform.offsetMax = Vector2.zero;
            _nameHintText.gameObject.SetActive(false);

            _nameLetterCount = MakeDisplayText("Counter", _captionRowRect, "4/12", 15, Muted, TextAnchor.MiddleRight);
            Ui.ClearOutline(_nameLetterCount);
            FitSingleLine(_nameLetterCount, 10, 15);
            _nameLetterCount.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            _nameLetterCount.rectTransform.anchorMax = Vector2.one;
            _nameLetterCount.rectTransform.offsetMin = Vector2.zero;
            _nameLetterCount.rectTransform.offsetMax = Vector2.zero;

            // Top-anchored tactical InputField right under PLAYER TAG (>= 48 dp hit target)
            _nameFieldBoxRect = Ui.Panel("NameFieldBox", _callingCardRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(-24f, HitHeight));
            Image inputHit = _nameFieldBoxRect.gameObject.AddComponent<Image>();
            inputHit.color = Clear;
            inputHit.raycastTarget = true;

            RectTransform inputFill = Ui.Panel("InputFill", _nameFieldBoxRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 56f));
            Image inputBg = inputFill.gameObject.AddComponent<Image>();
            inputBg.raycastTarget = false;

            _inputFocusOutline = Ui.MakeSliced("FocusOutline", inputFill, MenuArt.RoundOutline(Color.white, 12, 2), InputIdleBorder);

            _nameInput = _nameFieldBoxRect.gameObject.AddComponent<InputField>();
            _nameInput.targetGraphic = inputBg;
            _nameInput.characterLimit = AppSession.MaxNameLength;
            _nameInput.lineType = InputField.LineType.SingleLine;
            _nameInput.caretColor = GoldLight;
            _nameInput.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.32f);

            Text ph = Ui.MakeText("Placeholder", inputFill, "YOUR NAME", 19, Muted, TextAnchor.MiddleCenter);
            Ui.ClearOutline(ph);
            FitSingleLine(ph, 12, 19);
            Stretch(ph.rectTransform, 12f, 4f, -12f, -4f);

            Text txt = Ui.MakeText("Text", inputFill, AppSession.Current.PlayerName, 25, Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.supportRichText = false;
            FitSingleLine(txt, 14, 25);
            Stretch(txt.rectTransform, 12f, 4f, -12f, -4f);

            Ui.StyleTacticalInput(inputBg, _inputFocusOutline, txt, ph, focused: false);
            Ui.ClearOutline(ph);
            _inputFocusOutline.color = InputIdleBorder;

            _nameInput.textComponent = txt;
            _nameInput.placeholder = ph;
            _nameInput.onValueChanged.AddListener(OnNameChanged);

            // Save Name CTA (beveled gold, >= 48 dp hit height)
            SaveNameButton = MakeTacticalButton(
                "SaveNameButton",
                _callingCol,
                "SAVE NAME",
                isPrimary: true,
                glyph: MenuArt.GlyphKind.Check,
                glyphOnRight: false,
                onClick: () => SavePlayerName(),
                out _saveBtnRect);
            _saveBtnRect.anchorMin = new Vector2(0f, 0f);
            _saveBtnRect.anchorMax = new Vector2(1f, 0f);
            _saveBtnRect.pivot = new Vector2(0.5f, 0f);
            _saveBtnRect.anchoredPosition = new Vector2(0f, HitHeight + 6f);
            _saveBtnRect.sizeDelta = new Vector2(0f, HitHeight);

            // Selfie / Clear Photo row (>= 48 dp hit height)
            _photoRowRect = Ui.Panel("PhotoActionRow", _callingCol, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, HitHeight));

            TakeSelfieButton = MakeTacticalButton(
                "TakeSelfieButton",
                _photoRowRect,
                "TAKE SELFIE",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Camera,
                glyphOnRight: false,
                onClick: () =>
                {
                    TapFeedback();
                    if (_onOpenIdCamera != null) _onOpenIdCamera.Invoke();
                    else _sheets?.OpenIdCameraOver(Root);
                },
                out _selfieBtnRect);

            _removePhotoButton = MakeTacticalButton(
                "RemovePhotoButton",
                _photoRowRect,
                "CLEAR",
                isPrimary: false,
                glyph: MenuArt.GlyphKind.Close,
                glyphOnRight: false,
                onClick: OnRemovePhotoClicked,
                out _removePhotoBtnRect);

            // Right Column: 12-Kid Avatar Picker Grid (clean KidBust portraits, Option A1 obsidian glass)
            _avatarCol = Ui.Panel("AvatarCol", _identityPane, new Vector2(0.42f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            MakeGlassPanel("Bg", _avatarCol, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            Text gridTitle = MakeDisplayText("GridTitle", _avatarCol, "KID AVATARS (12)", 18, Cream, TextAnchor.MiddleLeft);
            Ui.ClearOutline(gridTitle);
            FitSingleLine(gridTitle, 10, 18);
            gridTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            gridTitle.rectTransform.anchorMax = new Vector2(0.65f, 1f);
            gridTitle.rectTransform.pivot = new Vector2(0f, 1f);
            gridTitle.rectTransform.anchoredPosition = new Vector2(16f, -12f);
            gridTitle.rectTransform.sizeDelta = new Vector2(-16f, 28f);

            Text gridNote = MakeDisplayText("GridNote", _avatarCol, "ON-DEVICE", 14, Muted, TextAnchor.MiddleRight);
            Ui.ClearOutline(gridNote);
            FitSingleLine(gridNote, 10, 14);
            gridNote.rectTransform.anchorMin = new Vector2(0.66f, 1f);
            gridNote.rectTransform.anchorMax = new Vector2(1f, 1f);
            gridNote.rectTransform.pivot = new Vector2(1f, 1f);
            gridNote.rectTransform.anchoredPosition = new Vector2(-16f, -12f);
            gridNote.rectTransform.sizeDelta = new Vector2(-16f, 28f);

            _avatarGridRect = Ui.Fill("AvatarGrid", _avatarCol);
            _avatarGridRect.offsetMin = new Vector2(12f, 12f);
            _avatarGridRect.offsetMax = new Vector2(-12f, -48f);

            for (int i = 0; i < HomeSheetsView.AvatarIds.Length; i++)
            {
                int idx = i;
                string kidId = HomeSheetsView.AvatarIds[i];

                RectTransform tile = Ui.Panel("Avatar_" + kidId, _avatarGridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(100f, 120f));
                Image bg = Ui.MakeImage("Bg", tile, ObsidianCard, round: false, raycast: true);
                Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);
                ApplyGlassSurface(bg, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

                RectTransform artSlot = Ui.Fill("ArtSlot", tile);
                artSlot.offsetMin = new Vector2(3f, 3f);
                artSlot.offsetMax = new Vector2(-3f, -3f);
                artSlot.gameObject.AddComponent<RectMask2D>();

                var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                photoGo.transform.SetParent(artSlot, false);
                RawImage photo = photoGo.GetComponent<RawImage>();
                photo.texture = LoadAvatarTexture(kidId);
                photo.uvRect = FullUv;
                photo.raycastTarget = false;
                Stretch(photo.rectTransform, 0f, 0f, 0f, 0f);
                AspectRatioFitter tileFit = photoGo.GetComponent<AspectRatioFitter>();
                tileFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                tileFit.aspectRatio = 0.85f;

                Image nameScrim = Ui.MakeImage("Scrim", artSlot, new Color(1f, 1f, 1f, 0.88f), round: false, raycast: false);
                nameScrim.sprite = MenuArt.BottomScrim();
                nameScrim.rectTransform.anchorMin = Vector2.zero;
                nameScrim.rectTransform.anchorMax = new Vector2(1f, 0.44f);
                nameScrim.rectTransform.offsetMin = Vector2.zero;
                nameScrim.rectTransform.offsetMax = Vector2.zero;

                Text tileName = MakeDisplayText("Name", artSlot, ResolveKidDisplayName(kidId), 15, Cream, TextAnchor.LowerCenter);
                Ui.ClearOutline(tileName);
                FitSingleLine(tileName, 10, 15);
                tileName.rectTransform.anchorMin = Vector2.zero;
                tileName.rectTransform.anchorMax = new Vector2(1f, 0.38f);
                tileName.rectTransform.offsetMin = new Vector2(4f, 4f);
                tileName.rectTransform.offsetMax = new Vector2(-4f, 0f);

                Image outline = Ui.MakeSliced("Outline", tile, MenuArt.ChamferOutline(Gold, cut: 6, borderPx: 3), Color.white);
                outline.gameObject.SetActive(false);

                Image check = Ui.MakeImage("CheckBadge", tile, Color.white);
                check.sprite = MenuArt.CheckBadge();
                Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(24f, 24f));
                check.gameObject.SetActive(false);

                AttachButton(tile.gameObject, bg, () => SelectAvatar(idx), 0.95f);

                _avatarTiles.Add(new AvatarTileUi
                {
                    Index = idx,
                    Rect = tile,
                    Outline = outline,
                    CheckBadge = check,
                });
            }
        }

        // =================================================================================================================
        // Data Refresh & Layout Helpers
        // =================================================================================================================

        void RefreshRoleChart(IReadOnlyList<MatchHistoryEntry> history, string fallbackRole)
        {
            int[] counts = new int[4];
            int total = 0;
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    string r = ResolveKidRole(history[i].CharacterId);
                    int idx = RoleIndex(r);
                    if (idx >= 0)
                    {
                        counts[idx]++;
                        total++;
                    }
                }
            }

            int bestIdx = RoleIndex(fallbackRole);
            if (total > 0)
            {
                int bestCount = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (counts[i] > bestCount)
                    {
                        bestCount = counts[i];
                        bestIdx = i;
                    }
                }
            }

            for (int i = 0; i < _roleBars.Length; i++)
            {
                RoleBarUi bar = _roleBars[i];
                if (bar == null) continue;
                bool isTop = i == bestIdx;
                float frac = total > 0 ? (float)counts[i] / total : (isTop ? 0.16f : 0.04f);
                int pct = total > 0 ? Mathf.RoundToInt(frac * 100f) : 0;

                bar.FillRect.anchorMax = new Vector2(Mathf.Clamp(frac, 0.04f, 1f), 1f);
                bar.FillImage.color = isTop ? Gold : NeutralBarFill;
                bar.Icon.color = isTop ? GoldLight : Cream;
                Ui.SetTextColor(bar.NameLabel, isTop ? GoldLight : Cream);
                bar.PctLabel.text = total > 0 ? pct.ToString(CultureInfo.InvariantCulture) + "%" : (isTop ? "MAIN" : "0%");
                Ui.SetTextColor(bar.PctLabel, isTop ? GoldLight : Muted);
            }

            if (_rolesFootnote != null)
            {
                _rolesFootnote.text = total > 0
                    ? (total == 1 ? "1 logged match" : total.ToString(CultureInfo.InvariantCulture) + " logged matches")
                    : "Play matches to chart roles";
            }
        }

        void RefreshRecentForm(IReadOnlyList<MatchHistoryEntry> history)
        {
            int count = history != null ? history.Count : 0;
            for (int i = 0; i < 5; i++)
            {
                if (_formBgs[i] == null || _formTexts[i] == null) continue;
                if (i < count)
                {
                    bool won = history[i].Won;
                    _formBgs[i].color = won
                        ? new Color(SuccessGreen.r, SuccessGreen.g, SuccessGreen.b, 0.28f)
                        : new Color(Coral.r, Coral.g, Coral.b, 0.28f);
                    _formTexts[i].text = won ? "W" : "L";
                    Ui.SetTextColor(_formTexts[i], won ? SuccessGreen : Coral);
                }
                else
                {
                    _formBgs[i].color = FormEmptyBg;
                    _formTexts[i].text = "-";
                    Ui.SetTextColor(_formTexts[i], Muted);
                }
            }

            if (_latestMatchTitle != null && _latestMatchDetail != null)
            {
                if (count > 0)
                {
                    MatchHistoryEntry last = history[0];
                    string verdict = last.Won ? "VICTORY" : "DEFEAT";
                    string mode = FormatModeName(last.Mode);
                    string kid = ResolveKidDisplayName(last.CharacterId);
                    _latestMatchTitle.text = verdict + "  \u00B7  " + last.MyScore.ToString(CultureInfo.InvariantCulture) + " - " + last.TheirScore.ToString(CultureInfo.InvariantCulture);
                    Ui.SetTextColor(_latestMatchTitle, last.Won ? SuccessGreen : Coral);
                    _latestMatchDetail.text = mode + " \u00B7 " + kid + "\n" + FormatTimestamp(last.TimestampUnixSeconds);
                }
                else
                {
                    _latestMatchTitle.text = "NO MATCHES YET";
                    Ui.SetTextColor(_latestMatchTitle, Cream);
                    _latestMatchDetail.text = "Play Blitz or Practice\nto log your career.";
                }
            }
        }

        void RefreshHistoryTab(IReadOnlyList<MatchHistoryEntry> history)
        {
            ClearHistoryRows();

            int total = history != null ? Mathf.Min(history.Count, MaxInlineHistoryRows) : 0;
            int wins = 0;
            for (int i = 0; i < total; i++)
            {
                if (history[i].Won) wins++;
            }
            int losses = total - wins;

            if (_histCountValue != null) _histCountValue.text = total.ToString(CultureInfo.InvariantCulture);
            if (_histWinsValue != null) _histWinsValue.text = wins.ToString(CultureInfo.InvariantCulture);
            if (_histLossesValue != null) _histLossesValue.text = losses.ToString(CultureInfo.InvariantCulture);
            if (_histRateValue != null)
                _histRateValue.text = total > 0
                    ? Mathf.RoundToInt((wins * 100f) / total).ToString(CultureInfo.InvariantCulture) + "%"
                    : "--";

            if (_historyContent == null) return;
            if (_historyScroll != null)
                _historyScroll.verticalNormalizedPosition = 1f;

            if (total == 0)
            {
                var emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(LayoutElement));
                emptyGo.transform.SetParent(_historyContent, false);
                emptyGo.GetComponent<LayoutElement>().preferredHeight = 140f;
                RectTransform r = emptyGo.GetComponent<RectTransform>();
                MakeGlassPanel("Bg", r, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);
                Text lbl = MakeDisplayText("Text", r, "NO MATCHES RECORDED YET", 24, Cream, TextAnchor.MiddleCenter);
                FitSingleLine(lbl, 14, 24);
                lbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                lbl.rectTransform.anchorMax = Vector2.one;
                lbl.rectTransform.offsetMin = new Vector2(16f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-16f, -14f);

                Text sub = Ui.MakeText("Sub", r, "Finish a Blitz, Quick, or Practice match to build your last-20 career log.", 16, Muted, TextAnchor.UpperCenter);
                Ui.ClearOutline(sub);
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;
                sub.verticalOverflow = VerticalWrapMode.Truncate;
                sub.resizeTextForBestFit = true;
                sub.resizeTextMinSize = 11;
                sub.resizeTextMaxSize = 16;
                sub.rectTransform.anchorMin = Vector2.zero;
                sub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                sub.rectTransform.offsetMin = new Vector2(16f, 14f);
                sub.rectTransform.offsetMax = new Vector2(-16f, 0f);
                _historyRows.Add(emptyGo);
                return;
            }

            for (int i = 0; i < total; i++)
            {
                MatchHistoryEntry entry = history[i];
                var rowGo = new GameObject("MatchRow_" + i, typeof(RectTransform), typeof(LayoutElement));
                rowGo.transform.SetParent(_historyContent, false);
                rowGo.GetComponent<LayoutElement>().preferredHeight = 76f;
                RectTransform row = rowGo.GetComponent<RectTransform>();

                bool won = entry.Won;
                MakeGlassPanel(
                    "Bg",
                    row,
                    won ? WinRowFill : LossRowFill,
                    won ? WinRowBorder : LossRowBorder,
                    won ? SuccessGreen : Coral,
                    cornerCut: 6f,
                    accentW: 3.5f);

                // Kid portrait thumbnail (clean KidBust portrait, FullUv)
                RectTransform thumb = Ui.Panel("Thumb", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(54f, 54f));
                MakeGlassPanel("Well", thumb, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 4f, accentW: 0f);

                RectTransform thumbSlot = Ui.Fill("Slot", thumb);
                thumbSlot.offsetMin = new Vector2(2f, 2f);
                thumbSlot.offsetMax = new Vector2(-2f, -2f);
                thumbSlot.gameObject.AddComponent<RectMask2D>();
                var thumbPhotoGo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                thumbPhotoGo.transform.SetParent(thumbSlot, false);
                RawImage thumbPhoto = thumbPhotoGo.GetComponent<RawImage>();
                thumbPhoto.texture = LoadAvatarTexture(entry.CharacterId);
                thumbPhoto.uvRect = FullUv;
                thumbPhoto.raycastTarget = false;
                Stretch(thumbPhoto.rectTransform, 0f, 0f, 0f, 0f);
                AspectRatioFitter thumbFit = thumbPhotoGo.GetComponent<AspectRatioFitter>();
                thumbFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                thumbFit.aspectRatio = 0.85f;

                // Verdict + Mode + Kid
                Text verdict = MakeDisplayText("Verdict", row, won ? "VICTORY" : "DEFEAT", 22, won ? SuccessGreen : Coral, TextAnchor.LowerLeft);
                FitSingleLine(verdict, 14, 22);
                verdict.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                verdict.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                verdict.rectTransform.offsetMin = new Vector2(84f, 2f);
                verdict.rectTransform.offsetMax = new Vector2(0f, -8f);

                Text meta = Ui.MakeText("Meta", row, FormatModeName(entry.Mode) + "  \u00B7  " + ResolveKidDisplayName(entry.CharacterId), 15, Cream, TextAnchor.UpperLeft);
                Ui.ClearOutline(meta);
                FitSingleLine(meta, 11, 15);
                meta.rectTransform.anchorMin = new Vector2(0f, 0f);
                meta.rectTransform.anchorMax = new Vector2(0.62f, 0.5f);
                meta.rectTransform.offsetMin = new Vector2(84f, 8f);
                meta.rectTransform.offsetMax = new Vector2(0f, -2f);

                // Right score + timestamp
                Text score = MakeDisplayText("Score", row, entry.MyScore.ToString(CultureInfo.InvariantCulture) + " - " + entry.TheirScore.ToString(CultureInfo.InvariantCulture), 26, Cream, TextAnchor.LowerRight);
                FitSingleLine(score, 16, 26);
                score.rectTransform.anchorMin = new Vector2(0.62f, 0.5f);
                score.rectTransform.anchorMax = Vector2.one;
                score.rectTransform.offsetMin = Vector2.zero;
                score.rectTransform.offsetMax = new Vector2(-18f, -8f);

                Text stamp = Ui.MakeText("Time", row, FormatTimestamp(entry.TimestampUnixSeconds), 15, Muted, TextAnchor.UpperRight);
                Ui.ClearOutline(stamp);
                FitSingleLine(stamp, 10, 15);
                stamp.rectTransform.anchorMin = new Vector2(0.62f, 0f);
                stamp.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                stamp.rectTransform.offsetMin = new Vector2(0f, 8f);
                stamp.rectTransform.offsetMax = new Vector2(-18f, -2f);

                _historyRows.Add(rowGo);
            }
        }

        void RefreshIdentityTab(string playerName)
        {
            UpdateLivePreviewName(playerName);
            if (_nameInput != null && !_nameInput.isFocused)
            {
                _nameInput.text = playerName;
                UpdateNameCounter(playerName);
            }

            int activeIdx = ResolveActiveAvatarIndex();
            bool hasCustom = HasCustomPhoto();
            for (int i = 0; i < _avatarTiles.Count; i++)
            {
                AvatarTileUi t = _avatarTiles[i];
                bool selected = !hasCustom && t.Index == activeIdx;
                t.Outline.gameObject.SetActive(selected);
                t.CheckBadge.gameObject.SetActive(selected);
            }

            LayoutPhotoActionButtons();
        }

        void LayoutAvatarGrid(float colWidth, float bodyH, float minTouch)
        {
            if (_avatarGridRect == null || _avatarTiles.Count == 0) return;

            const int cols = 4;
            const int rows = 3;
            float innerW = Mathf.Max(360f, colWidth - 24f);
            float innerH = Mathf.Max(290f, bodyH - 60f);
            float gapX = innerW < 420f ? 6f : 10f;
            float gapY = 10f;

            float maxCellW = (innerW - gapX * (cols - 1)) / cols;
            float maxCellH = (innerH - gapY * (rows - 1)) / rows;

            // Keep every tile >= minTouch AND within balanced near-square proportions across 4:3..21:9
            float cellW = Mathf.Max(minTouch, Mathf.Min(maxCellW, maxCellH * 1.22f));
            if (cellW > maxCellW) cellW = maxCellW;

            float cellH = Mathf.Max(minTouch, Mathf.Min(maxCellH, cellW * 1.24f));
            if (cellH > maxCellH) cellH = maxCellH;

            float totalW = cellW * cols + gapX * (cols - 1);
            float totalH = cellH * rows + gapY * (rows - 1);
            float startX = Mathf.Max(0f, (innerW - totalW) * 0.5f);
            float startY = Mathf.Max(0f, (innerH - totalH) * 0.5f);

            for (int i = 0; i < _avatarTiles.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                RectTransform r = _avatarTiles[i].Rect;
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(startX + col * (cellW + gapX), -(startY + row * (cellH + gapY)));
                r.sizeDelta = new Vector2(cellW, cellH);
            }
        }

        void LayoutPhotoActionButtons()
        {
            if (_selfieBtnRect == null || _removePhotoBtnRect == null) return;
            bool hasCustom = HasCustomPhoto();
            if (_removePhotoButton != null)
                _removePhotoButton.gameObject.SetActive(hasCustom);

            if (hasCustom)
            {
                // 0.56 / 0.44 split gives >= 126 c (>= 134 px >> 48 dp) to CLEAR even on 1024x768
                _selfieBtnRect.anchorMin = new Vector2(0f, 0f);
                _selfieBtnRect.anchorMax = new Vector2(0.56f, 1f);
                _selfieBtnRect.offsetMin = Vector2.zero;
                _selfieBtnRect.offsetMax = new Vector2(-4f, 0f);

                _removePhotoBtnRect.anchorMin = new Vector2(0.56f, 0f);
                _removePhotoBtnRect.anchorMax = Vector2.one;
                _removePhotoBtnRect.offsetMin = new Vector2(4f, 0f);
                _removePhotoBtnRect.offsetMax = Vector2.zero;
            }
            else
            {
                _selfieBtnRect.anchorMin = Vector2.zero;
                _selfieBtnRect.anchorMax = Vector2.one;
                _selfieBtnRect.offsetMin = Vector2.zero;
                _selfieBtnRect.offsetMax = Vector2.zero;
            }
        }

        void Tick(float dt)
        {
            if (_disposed || Root == null || !Root.gameObject.activeInHierarchy) return;

            if (ActiveTab == TabOverview)
                _stage?.Tick(dt);

            if (ActiveTab == TabIdentity && _nameInput != null && _inputFocusOutline != null)
            {
                bool focused = _nameInput.isFocused;
                if (focused != _wasNameFocused)
                {
                    _wasNameFocused = focused;
                    _inputFocusOutline.color = focused ? GoldBorder : InputIdleBorder;
                }
            }
        }

        void HandleExternalProfileChanged()
        {
            if (!_disposed) Refresh();
        }

        void OnHeaderBackClicked()
        {
            TapFeedback();
            _onBack?.Invoke();
        }

        void OnRemovePhotoClicked()
        {
            if (_sheets != null)
            {
                // HomeSheetsView.RemoveCustomPhoto fires ProfileChanged -> HandleExternalProfileChanged -> Refresh().
                _sheets.RemoveCustomPhoto();
                return;
            }

            AppSession.Current.HasCustomIdPhoto = false;
            PlayerPrefs.SetInt(HomeSheetsView.CustomPhotoPrefKey, 0);
            try
            {
                string path = HomeSheetsView.LocalIdPhotoPath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup in standalone tests.
            }
            if (_localCustomTex != null)
            {
                KalyeahResources.Kill(_localCustomTex);
                _localCustomTex = null;
            }
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            _audio?.PlayUiTap("cancel");
            Haptics.LightTap();
            Refresh();
        }

        void OnNameChanged(string typed)
        {
            string upper = (typed ?? "").ToUpperInvariant();
            if (_nameInput != null && _nameInput.text != upper)
                _nameInput.SetTextWithoutNotify(upper);
            UpdateNameCounter(upper);
            SetNameHintVisible(false);
            string display = string.IsNullOrWhiteSpace(upper) ? ResolvePlayerName(AppSession.Current) : upper;
            UpdateLivePreviewName(display);
        }

        void UpdateLivePreviewName(string name)
        {
            if (_identityLiveName != null)
                _identityLiveName.text = name;
            if (_identityLiveSub != null)
                _identityLiveSub.text = "#KLY-" + name;
        }

        void UpdateNameCounter(string text)
        {
            if (_nameLetterCount != null)
                _nameLetterCount.text = (text != null ? text.Length : 0).ToString(CultureInfo.InvariantCulture)
                    + "/" + AppSession.MaxNameLength.ToString(CultureInfo.InvariantCulture);
        }

        void SetNameHintVisible(bool visible)
        {
            if (_nameHintText != null) _nameHintText.gameObject.SetActive(visible);
            if (_nameCaptionText != null) _nameCaptionText.gameObject.SetActive(!visible);
        }

        void ClearHistoryRows()
        {
            for (int i = 0; i < _historyRows.Count; i++)
            {
                if (_historyRows[i] != null)
                    KalyeahResources.Kill(_historyRows[i]);
            }
            _historyRows.Clear();
        }

        // =================================================================================================================
        // Avatar, Kid Metadata & Utility Helpers
        // =================================================================================================================

        static string ResolvePlayerName(AppSession session) =>
            string.IsNullOrWhiteSpace(session.PlayerName)
                ? AppSession.DefaultPlayerName
                : session.PlayerName.ToUpperInvariant();

        int ResolveActiveAvatarIndex()
        {
            if (_sheets != null) return _sheets.AvatarIndex;
            if (_hasLocalAvatarIndex)
                return Mathf.Clamp(_localAvatarIndex, 0, HomeSheetsView.AvatarIds.Length - 1);
            if (PlayerPrefs.HasKey(HomeSheetsView.AvatarPrefKey))
                return Mathf.Clamp(PlayerPrefs.GetInt(HomeSheetsView.AvatarPrefKey, AppSession.Current.AvatarIndex), 0, HomeSheetsView.AvatarIds.Length - 1);
            return Mathf.Clamp(AppSession.Current.AvatarIndex, 0, HomeSheetsView.AvatarIds.Length - 1);
        }

        bool HasCustomPhoto()
        {
            if (_sheets != null) return _sheets.HasCustomIdPhoto;
            return AppSession.Current.HasCustomIdPhoto && TryEnsureLocalCustomPhoto();
        }

        void ApplyAvatarGraphic(RawImage target)
        {
            if (target == null) return;
            if (_sheets != null && _sheets.CurrentIdTexture != null)
            {
                target.texture = _sheets.CurrentIdTexture;
                target.uvRect = _sheets.CurrentIdUvRect;
                return;
            }

            if (AppSession.Current.HasCustomIdPhoto && TryEnsureLocalCustomPhoto())
            {
                target.texture = _localCustomTex;
                target.uvRect = FullUv;
                return;
            }

            int idx = ResolveActiveAvatarIndex();
            string kidId = HomeSheetsView.AvatarIds[Mathf.Clamp(idx, 0, HomeSheetsView.AvatarIds.Length - 1)];
            target.texture = LoadAvatarTexture(kidId);
            target.uvRect = FullUv;
        }

        bool TryEnsureLocalCustomPhoto()
        {
            if (_localCustomTex != null) return true;
            try
            {
                string path = HomeSheetsView.LocalIdPhotoPath;
                if (!File.Exists(path)) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0) return false;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    KalyeahResources.Kill(tex);
                    return false;
                }
                _localCustomTex = tex;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static Texture LoadAvatarTexture(string kidId)
        {
            string id = string.IsNullOrWhiteSpace(kidId) ? AppSession.DefaultCharacter : kidId.ToLowerInvariant();
            return KalyeahResources.KidBust(id)
                ?? KalyeahResources.Texture("portraits/faces/" + id)
                ?? KalyeahResources.Texture("portraits/" + id);
        }

        void BuildKidMetadataLookup()
        {
            for (int i = 0; i < AppSession.DefaultRoster.Length; i++)
            {
                string id = AppSession.DefaultRoster[i];
                string display = id.ToUpperInvariant();
                string role = RoleOrder[Mathf.Clamp(i / 3, 0, 3)];
                string purok = "";

                if (_data != null)
                {
                    try
                    {
                        Dictionary<string, object> props = _data.Props("characters__" + id);
                        if (props != null)
                        {
                            string dName = JsonRead.Str(props, "display_name", "");
                            if (!string.IsNullOrWhiteSpace(dName)) display = dName.ToUpperInvariant();
                            string desc = JsonRead.Str(props, "description", "");
                            string parsedRole = HomeScreenView.ParseRole(desc);
                            if (!string.IsNullOrWhiteSpace(parsedRole)) role = parsedRole.ToUpperInvariant();
                            string parsedPurok = KidsScreenView.Purok(desc);
                            if (!string.IsNullOrWhiteSpace(parsedPurok)) purok = parsedPurok.ToUpperInvariant();
                        }
                    }
                    catch
                    {
                        // Minimal/mock DataSource instances fall back cleanly to roster defaults.
                    }
                }

                _kidNameMap[id] = display;
                _kidRoleMap[id] = role;
                _kidPurokMap[id] = purok;
            }
        }

        string ResolveKidDisplayName(string kidId)
        {
            if (string.IsNullOrWhiteSpace(kidId)) return "KARDING";
            return _kidNameMap.TryGetValue(kidId, out string name) ? name : kidId.ToUpperInvariant();
        }

        string ResolveKidRole(string kidId)
        {
            if (string.IsNullOrWhiteSpace(kidId)) return "SEEKER";
            return _kidRoleMap.TryGetValue(kidId, out string role) ? role : "SEEKER";
        }

        string ResolveKidPurok(string kidId)
        {
            if (string.IsNullOrWhiteSpace(kidId)) return "";
            return _kidPurokMap.TryGetValue(kidId, out string purok) ? purok : "";
        }

        static int RoleIndex(string role)
        {
            for (int i = 0; i < RoleOrder.Length; i++)
            {
                if (string.Equals(RoleOrder[i], role, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        static string FindMostPlayedKid(
            IReadOnlyList<MatchHistoryEntry> history,
            string fallbackKid,
            out int matches,
            out int wins)
        {
            matches = 0;
            wins = 0;
            if (history == null || history.Count == 0) return fallbackKid;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var winCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string best = fallbackKid;
            int bestCount = 0;
            for (int i = 0; i < history.Count; i++)
            {
                string id = history[i].CharacterId;
                if (string.IsNullOrWhiteSpace(id)) continue;
                int c = counts.TryGetValue(id, out int prev) ? prev + 1 : 1;
                counts[id] = c;
                if (history[i].Won)
                    winCounts[id] = winCounts.TryGetValue(id, out int prevW) ? prevW + 1 : 1;
                if (c > bestCount)
                {
                    bestCount = c;
                    best = id;
                }
            }

            matches = bestCount;
            wins = winCounts.TryGetValue(best, out int bestWins) ? bestWins : 0;
            return best;
        }

        static int ComputeCurrentWinStreak(IReadOnlyList<MatchHistoryEntry> history)
        {
            if (history == null || history.Count == 0) return 0;
            int streak = 0;
            for (int i = 0; i < history.Count; i++)
            {
                if (!history[i].Won) break;
                streak++;
            }
            return streak;
        }

        static string FormatModeName(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "BLITZ";
            return mode.Replace('_', ' ').ToUpperInvariant();
        }

        static string FormatTimestamp(long unixSeconds)
        {
            if (unixSeconds <= 0) return "RECENT";
            try
            {
                DateTimeOffset dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
                return dt.ToString("MMM dd \u00B7 HH:mm", CultureInfo.InvariantCulture).ToUpperInvariant();
            }
            catch
            {
                return "RECENT";
            }
        }

        static Text BuildLevelMedallion(RectTransform avatarFrame, float diameter, int fontSize)
        {
            int outerRadius = Mathf.Max(8, Mathf.RoundToInt(diameter * 0.5f));
            int innerRadius = Mathf.Max(6, outerRadius - 3);
            RectTransform lvlRim = Ui.Panel("LevelBadge", avatarFrame, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-2f, 2f), new Vector2(diameter, diameter));
            Image rimImg = lvlRim.gameObject.AddComponent<Image>();
            rimImg.sprite = MenuArt.RoundSolid(outerRadius);
            rimImg.type = Image.Type.Sliced;
            rimImg.color = Ink;
            rimImg.raycastTarget = false;

            RectTransform lvlCore = Ui.Fill("Core", lvlRim);
            lvlCore.offsetMin = new Vector2(2f, 2f);
            lvlCore.offsetMax = new Vector2(-2f, -2f);
            Image coreImg = lvlCore.gameObject.AddComponent<Image>();
            coreImg.sprite = MenuArt.RoundSolid(innerRadius);
            coreImg.type = Image.Type.Sliced;
            coreImg.color = Cream;
            coreImg.raycastTarget = false;

            Text lvlText = MakeDisplayText("Text", lvlCore, "1", fontSize, Ink, TextAnchor.MiddleCenter);
            Ui.ClearOutline(lvlText);
            FitSingleLine(lvlText, 10, fontSize);
            Stretch(lvlText.rectTransform, 0f, 0f, 0f, 0f);
            return lvlText;
        }

        Button MakeTacticalButton(
            string name,
            Transform parent,
            string labelText,
            bool isPrimary,
            MenuArt.GlyphKind glyph,
            bool glyphOnRight,
            Action onClick,
            out RectTransform hitRect)
        {
            hitRect = Ui.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, HitHeight));
            Image hitImg = hitRect.gameObject.AddComponent<Image>();
            hitImg.color = Clear;
            hitImg.raycastTarget = true;

            RectTransform visual = Ui.Panel("Visual", hitRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, isPrimary ? 54f : 48f));
            Image plate;
            if (isPrimary)
            {
                plate = Ui.MakeImage("Plate", visual, Gold, round: false, raycast: false);
                Stretch(plate.rectTransform, 0f, 0f, 0f, 0f);
                Material ctaMat = UiShaders.CreateMetallicCta(Gold, VermilionDeep, cornerCut: 8f);
                if (ctaMat != null && ctaMat != Graphic.defaultGraphicMaterial)
                {
                    plate.material = ctaMat;
                    plate.color = Color.white;
                }
            }
            else
            {
                plate = MakeGlassPanel("Plate", visual, ObsidianCard, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);
            }

            Image icon = Ui.MakeImage("Icon", visual, Cream);
            icon.sprite = MenuArt.Glyph(glyph);
            if (glyphOnRight)
                Place(icon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(16f, 16f));
            else
                Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(17f, 17f));

            Text lbl = MakeDisplayText("Label", visual, labelText, isPrimary ? 21 : 18, Cream, TextAnchor.MiddleCenter);
            Ui.ClearOutline(lbl);
            FitSingleLine(lbl, 10, isPrimary ? 21 : 18);
            Stretch(lbl.rectTransform, glyphOnRight ? 10f : 32f, 0f, glyphOnRight ? -30f : -10f, 0f);

            return AttachButton(hitRect.gameObject, plate, onClick, isPrimary ? 0.94f : 0.95f);
        }

        Button AttachButton(GameObject go, Graphic targetGraphic, Action onClick, float pressScale)
        {
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            btn.transition = Selectable.Transition.None;
            UiShaders.AttachPressSpring(btn, pressScale, 2.5f);
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        static Image MakeGlassPanel(string name, Transform parent, Color fill, Color border, Color leftAccent, float cornerCut = 6f, float accentW = 0f)
        {
            Image img = Ui.MakeImage(name, parent, Color.white, round: false, raycast: false);
            Stretch(img.rectTransform, 0f, 0f, 0f, 0f);
            ApplyGlassSurface(img, fill, border, leftAccent, cornerCut, accentW);
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

        void TapFeedback()
        {
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
        }

        static Text MakeDisplayText(string name, Transform parent, string text, int size, Color color, TextAnchor align)
        {
            Text t = Ui.MakeText(name, parent, text, size, color, align, KalyeahResources.DisplayFont());
            if (Ui.Luminance(color) < 0.25f)
                Ui.ClearOutline(t);
            return t;
        }

        static void FitSingleLine(Text t, int minSize, int maxSize)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = maxSize;
        }

        static void Stretch(RectTransform r, float l, float b, float ri, float t)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(ri, t);
        }

        static void Place(RectTransform r, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}
