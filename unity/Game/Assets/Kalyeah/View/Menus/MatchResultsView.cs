using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    public sealed class MatchResultsPayload
    {
        public bool LocalTeamWon;
        public int LocalTeam;
        public int BlueScore;
        public int RedScore;
        public MatchRewardMode Mode = MatchRewardMode.Blitz;
        public bool IsRanked;
        public bool IsHotspot;
        public bool IsFirstWinOfDay;
        public int WeeklyBaryaAlreadyEarned;
        public string MapName = "Barangay";
        public IReadOnlyList<MatchPlayerStats> Players = Array.Empty<MatchPlayerStats>();
    }

    /// <summary>
    /// Post-match Victory/Defeat, Dual-Team 5v5 Scoreboard, Honest Dual-MVP (with Ranked "STAR SAVED" note),
    /// Barya &amp; Mastery XP reward strip, and 1-Tap Honor (1 honor per match for teammate OR rival) screen
    /// (<c>docs/SPEC.md</c> §8 L376-386, §9 L401-409). Verified across 844x390 (+cutout), 1280x720, 934x400, 1024x768.
    /// Zero per-frame GC allocations; strictly EditMode-safe.
    /// </summary>
    public sealed class MatchResultsView : IDisposable
    {
        public const int MaxPlayersPerTeam = 5;

        const float SideMarginCanvas = 8f;
        const float HeroBannerHeightCanvas = 72f;
        const float TeamHeaderHeightCanvas = 22f;
        const float BandGapCanvas = 2f;
        const float RowGapCanvas = 2f;
        const float RowCardInsetY = 6f;
        const float StatColWidthCanvas = 38f;
        const float MinHonorColWidthCanvas = 96f;

        static readonly Color ColVictoryWash = new Color(0.08f, 0.30f, 0.24f, 0.38f);
        static readonly Color ColDefeatWash = new Color(0.28f, 0.10f, 0.14f, 0.42f);
        static readonly Color ColVictorySun = new Color(0.98f, 0.80f, 0.28f, 0.14f);
        static readonly Color ColDefeatSun = new Color(0.90f, 0.34f, 0.28f, 0.10f);
        static readonly Color ColBlueAccent = new Color(0.26f, 0.72f, 0.98f, 1f);
        static readonly Color ColRedAccent = new Color(0.94f, 0.36f, 0.30f, 1f);
        static readonly Color ColRowTop = new Color(0.092f, 0.146f, 0.230f, 0.92f);
        static readonly Color ColRowBottom = new Color(0.044f, 0.076f, 0.138f, 0.96f);
        static readonly Color ColRowMvpTop = new Color(0.155f, 0.175f, 0.215f, 0.95f);
        static readonly Color ColRowMvpBottom = new Color(0.072f, 0.095f, 0.148f, 0.98f);
        static readonly Color ColHeaderBar = new Color(0.036f, 0.062f, 0.115f, 0.92f);
        static readonly Color ColDivider = new Color(0.76f, 0.88f, 0.96f, 0.18f);
        static readonly Color ColHonorDim = new Color(1f, 1f, 1f, 0.42f);

        [ExecuteAlways]
        sealed class ResultsLifecycle : MonoBehaviour
        {
            [NonSerialized] public MatchResultsView Owner;
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

        sealed class TeamHeaderUi
        {
            public Text TeamTitle;
            public RectTransform ColT;
            public RectTransform ColR;
            public RectTransform ColC;
            public RectTransform ColHonor;
        }

        sealed class PlayerRowUi
        {
            public RectTransform Root;
            public Image CardBg;
            public Image MvpOutline;
            public Image TeamRail;
            public Image PortraitFallbackDisc;
            public Text PortraitInitial;
            public RawImage PortraitRaw;
            public AspectRatioFitter PortraitFitter;
            public RectTransform InfoBox;
            public Text PlayerNameText;
            public Text KidRoleText;
            public RectTransform MvpBadgeRoot;
            public Text MvpBadgeText;
            public RectTransform StatTRect;
            public RectTransform StatRRect;
            public RectTransform StatCRect;
            public Text StatTText;
            public Text StatRText;
            public Text StatCText;
            public RectTransform HonorButtonRect;
            public Button HonorButton;
            public Image HonorPlate;
            public Image HonorIcon;
            public Text HonorLabel;
            public CanvasGroup HonorVisualGroup;
            public RectTransform SelfBadgeRect;

            public bool Active;
            public int PlayerId;
            public string PlayerName;
            public bool IsLocalPlayer;
        }

        public readonly RectTransform Root;
        public readonly RectTransform BackdropRect;
        public readonly RectTransform SafeRoot;
        public readonly SafeArea SafeAreaComponent;
        public readonly RectTransform HeroBannerRect;
        public readonly RectTransform TeamHeadersRect;
        public readonly RectTransform ScoreboardViewportRect;
        public readonly RectTransform BottomBarRect;
        public readonly Button PlayAgainButton;
        public readonly Button HomeButton;

        public bool HasHonored => _honoredPlayerId >= 0;
        public int HonoredPlayerId => _honoredPlayerId;
        public int BlueMvpPlayerId => _blueMvpPlayerId;
        public int RedMvpPlayerId => _redMvpPlayerId;
        public MatchRewardSummary LastRewards => _lastRewards;
        public string OutcomeTitleText => _outcomeTitle != null ? _outcomeTitle.text : string.Empty;

        public event Action<int, string> PlayerHonored;

        readonly Canvas _canvas;
        readonly Action _onPlayAgain;
        readonly Action _onHome;

        readonly Image _backdropWash;
        readonly RawImage _sunburstImg;
        readonly Image _heroBannerBg;
        readonly Image _heroTopRail;
        readonly Text _outcomeTitle;
        readonly Text _modeSubtitle;
        readonly Text _scoreLabel;
        readonly Text _baryaValueText;
        readonly Text _baryaBreakdownText;
        readonly Text _masteryXpText;
        readonly Text _rankedStarText;

        readonly TeamHeaderUi _blueHeader;
        readonly TeamHeaderUi _redHeader;

        readonly ScrollRect _scrollRect;
        readonly RectTransform _scrollContent;
        readonly RectTransform _blueColumn;
        readonly RectTransform _redColumn;
        readonly PlayerRowUi[] _blueRows = new PlayerRowUi[MaxPlayersPerTeam];
        readonly PlayerRowUi[] _redRows = new PlayerRowUi[MaxPlayersPerTeam];

        readonly RectTransform _homeButtonRect;
        readonly RectTransform _playAgainButtonRect;
        readonly RectTransform _honorStatusRect;
        readonly Text _honorStatusLabel;

        readonly List<MatchPlayerStats> _blueScratch = new List<MatchPlayerStats>(MaxPlayersPerTeam);
        readonly List<MatchPlayerStats> _redScratch = new List<MatchPlayerStats>(MaxPlayersPerTeam);

        int _localPlayerId = int.MinValue;
        int _blueMvpPlayerId = -1;
        int _redMvpPlayerId = -1;
        int _honoredPlayerId = -1;
        int _activeRowPairs;
        MatchRewardSummary _lastRewards;
        bool _disposed;

        public MatchResultsView(
            Transform canvasParent,
            Action onPlayAgain = null,
            Action onHome = null,
            Canvas canvas = null)
        {
            _onPlayAgain = onPlayAgain;
            _onHome = onHome;
            _canvas = canvas != null ? canvas : (canvasParent != null ? canvasParent.GetComponentInParent<Canvas>() : null);

            Root = Ui.Fill("MatchResultsView", canvasParent);
            var life = Root.gameObject.AddComponent<ResultsLifecycle>();
            life.Owner = this;

            // 1. Full-bleed atmospheric backdrop (100% raw screen incl. notches & bottom gesture bar)
            BackdropRect = Ui.Fill("Backdrop", Root);
            var skyGo = new GameObject("Sky", typeof(RectTransform), typeof(RawImage));
            skyGo.transform.SetParent(BackdropRect, false);
            var skyRaw = skyGo.GetComponent<RawImage>();
            skyRaw.texture = MenuArt.Sky();
            skyRaw.raycastTarget = true; // consumes taps behind modal
            Stretch(skyRaw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _backdropWash = Ui.MakeImage("OutcomeWash", BackdropRect, ColVictoryWash, round: false, raycast: false);
            Stretch(_backdropWash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var sunGo = new GameObject("Sunburst", typeof(RectTransform), typeof(RawImage));
            sunGo.transform.SetParent(BackdropRect, false);
            _sunburstImg = sunGo.GetComponent<RawImage>();
            _sunburstImg.texture = MenuArt.Sunburst();
            _sunburstImg.color = ColVictorySun;
            _sunburstImg.raycastTarget = false;
            Stretch(_sunburstImg.rectTransform, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 1.35f), Vector2.zero, Vector2.zero);

            var vigGo = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            vigGo.transform.SetParent(BackdropRect, false);
            var vigRaw = vigGo.GetComponent<RawImage>();
            vigRaw.texture = MenuArt.Vignette();
            vigRaw.raycastTarget = false;
            Stretch(vigRaw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 2. SafeArea container (handles asymmetric cutouts + 18 dp gesture pill + 180-deg flips)
            SafeRoot = Ui.Fill("SafeArea", Root);
            SafeAreaComponent = SafeRoot.gameObject.AddComponent<SafeArea>();
            SafeAreaComponent.Changed += HandleSafeAreaChanged;
            SafeAreaComponent.Apply();

            // 3. Band 1: Top Hero Banner (VICTORY/DEFEAT + Score + Barya Coin & Mastery XP reward strip)
            HeroBannerRect = Ui.Panel("HeroBanner", SafeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            HeroBannerRect.offsetMin = new Vector2(SideMarginCanvas, -HeroBannerHeightCanvas);
            HeroBannerRect.offsetMax = new Vector2(-SideMarginCanvas, 0f);

            _heroBannerBg = Ui.MakeSliced("Bg", HeroBannerRect, MenuArt.ChamferGlass(12), Color.white, raycast: false);
            _heroTopRail = Ui.MakeSliced("TopRail", HeroBannerRect, MenuArt.AccentRail(2), MenuArt.Gold, raycast: false);
            Stretch(_heroTopRail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -4f), new Vector2(-14f, 0f));

            // Left: Outcome title + mode/map subtitle
            RectTransform titleZone = Ui.Panel("TitleZone", HeroBannerRect, new Vector2(0.015f, 0.06f), new Vector2(0.35f, 0.92f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _outcomeTitle = MakeFittedText("OutcomeTitle", titleZone, "VICTORY", 38, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 18);
            Stretch(_outcomeTitle.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            _modeSubtitle = MakeFittedText("ModeSubtitle", titleZone, "BLITZ · BARANGAY", 13, MenuArt.MutedText, TextAnchor.MiddleLeft, KalyeahResources.UiFont(), minSize: 9);
            Ui.ClearOutline(_modeSubtitle);
            Stretch(_modeSubtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.34f), Vector2.zero, Vector2.zero);

            // Center: Score pill (Blue - Red)
            RectTransform scoreZone = Ui.Panel("ScoreZone", HeroBannerRect, new Vector2(0.36f, 0.12f), new Vector2(0.54f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("ScorePlate", scoreZone, MenuArt.StatChip(8), Color.white, raycast: false);
            Ui.MakeAccentRail("BluePip", scoreZone, ColBlueAccent, width: 4f, verticalInset: 8f, rightSide: false);
            Ui.MakeAccentRail("RedPip", scoreZone, ColRedAccent, width: 4f, verticalInset: 8f, rightSide: true);
            _scoreLabel = MakeFittedText("ScoreText", scoreZone, "3  -  1", 32, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 16);
            Stretch(_scoreLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f));

            // Right: Barya coin + Mastery XP + Ranked Star strip
            RectTransform rewardZone = Ui.Panel("RewardStrip", HeroBannerRect, new Vector2(0.555f, 0.10f), new Vector2(0.985f, 0.90f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("RewardPlate", rewardZone, MenuArt.StatChip(8), Color.white, raycast: false);

            Image coinImg = Ui.MakeImage("BaryaCoin", rewardZone, Color.white, round: false, raycast: false);
            coinImg.sprite = MenuArt.BaryaCoin();
            coinImg.preserveAspect = true;
            RectTransform coinRt = coinImg.rectTransform;
            coinRt.anchorMin = new Vector2(0f, 0.5f);
            coinRt.anchorMax = new Vector2(0f, 0.5f);
            coinRt.pivot = new Vector2(0f, 0.5f);
            coinRt.anchoredPosition = new Vector2(10f, 0f);
            coinRt.sizeDelta = new Vector2(36f, 36f);

            RectTransform baryaCol = Ui.Panel("BaryaCol", rewardZone, new Vector2(0f, 0.06f), new Vector2(0.54f, 0.94f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            baryaCol.offsetMin = new Vector2(52f, 0f);
            baryaCol.offsetMax = new Vector2(-4f, 0f);
            _baryaValueText = MakeFittedText("BaryaValue", baryaCol, "+100 BARYA", 20, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 10);
            Stretch(_baryaValueText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _baryaBreakdownText = MakeFittedText("BaryaBreakdown", baryaCol, "MATCH REWARD", 12, MenuArt.MutedText, TextAnchor.MiddleLeft, KalyeahResources.UiFont(), minSize: 8);
            Ui.ClearOutline(_baryaBreakdownText);
            Stretch(_baryaBreakdownText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);

            Image div = Ui.MakeImage("RewardDivider", rewardZone, ColDivider, round: false, raycast: false);
            Stretch(div.rectTransform, new Vector2(0.545f, 0.16f), new Vector2(0.548f, 0.84f), Vector2.zero, Vector2.zero);

            RectTransform xpCol = Ui.Panel("MasteryCol", rewardZone, new Vector2(0.56f, 0.06f), new Vector2(0.98f, 0.94f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _masteryXpText = MakeFittedText("MasteryXp", xpCol, "+100 MASTERY XP", 18, MenuArt.TealLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 9);
            Stretch(_masteryXpText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _rankedStarText = MakeFittedText("RankedStar", xpCol, "BARANGAY SHOWDOWN", 12, MenuArt.MutedText, TextAnchor.MiddleLeft, KalyeahResources.UiFont(), minSize: 8);
            Ui.ClearOutline(_rankedStarText);
            Stretch(_rankedStarText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);

            // 4. Band 2: Dual-Team Column Headers (BLUE TEAM | T R C HONOR   vs   RED TEAM | T R C HONOR)
            float headerTop = HeroBannerHeightCanvas + BandGapCanvas;
            TeamHeadersRect = Ui.Panel("TeamHeaders", SafeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            TeamHeadersRect.offsetMin = new Vector2(SideMarginCanvas, -(headerTop + TeamHeaderHeightCanvas));
            TeamHeadersRect.offsetMax = new Vector2(-SideMarginCanvas, -headerTop);

            _blueHeader = BuildTeamHeader("BlueHeader", TeamHeadersRect, "BLUE TEAM", ColBlueAccent, 0f, 0.493f);
            _redHeader = BuildTeamHeader("RedHeader", TeamHeadersRect, "RED TEAM", ColRedAccent, 0.507f, 1f);

            // 5. Band 3: Dual-Team 5v5 Scoreboard inside ScrollRect + RectMask2D
            var vpGo = new GameObject("ScoreboardViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            vpGo.transform.SetParent(SafeRoot, false);
            ScoreboardViewportRect = vpGo.GetComponent<RectTransform>();
            ScoreboardViewportRect.anchorMin = Vector2.zero;
            ScoreboardViewportRect.anchorMax = Vector2.one;
            ScoreboardViewportRect.pivot = new Vector2(0.5f, 0.5f);

            var vpBg = vpGo.GetComponent<Image>();
            vpBg.color = new Color(0f, 0f, 0f, 0.001f);

            var contentGo = new GameObject("ScoreboardContent", typeof(RectTransform));
            contentGo.transform.SetParent(ScoreboardViewportRect, false);
            _scrollContent = contentGo.GetComponent<RectTransform>();
            _scrollContent.anchorMin = new Vector2(0f, 1f);
            _scrollContent.anchorMax = new Vector2(1f, 1f);
            _scrollContent.pivot = new Vector2(0.5f, 1f);
            _scrollContent.anchoredPosition = Vector2.zero;
            _scrollContent.sizeDelta = new Vector2(0f, 488f);

            _scrollRect = vpGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = ScoreboardViewportRect;
            _scrollRect.content = _scrollContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 36f;

            _blueColumn = Ui.Panel("BlueTeamColumn", _scrollContent, new Vector2(0f, 0f), new Vector2(0.493f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            _redColumn = Ui.Panel("RedTeamColumn", _scrollContent, new Vector2(0.507f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            for (int i = 0; i < MaxPlayersPerTeam; i++)
            {
                _blueRows[i] = BuildPlayerRow("BlueRow_" + i, _blueColumn, ColBlueAccent);
                _redRows[i] = BuildPlayerRow("RedRow_" + i, _redColumn, ColRedAccent);
            }

            // 6. Band 4: Bottom Action Bar (>= 48 dp HOME & beveled gold PLAY AGAIN)
            BottomBarRect = Ui.Panel("BottomActionBar", SafeRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

            HomeButton = BuildActionButton(
                "HomeButton",
                BottomBarRect,
                "HOME",
                MenuArt.CtaGhost(12),
                MenuArt.Cream,
                addSheen: false,
                out _homeButtonRect);
            HomeButton.onClick.AddListener(HandleHomeClicked);

            PlayAgainButton = BuildActionButton(
                "PlayAgainButton",
                BottomBarRect,
                "PLAY AGAIN",
                MenuArt.CtaGold(14),
                MenuArt.Ink,
                addSheen: true,
                out _playAgainButtonRect);
            PlayAgainButton.onClick.AddListener(HandlePlayAgainClicked);

            _honorStatusRect = Ui.Panel("HonorStatus", BottomBarRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 36f));
            _honorStatusLabel = MakeFittedText(
                "Text",
                _honorStatusRect,
                "1 HONOR PER MATCH · TAP A TEAMMATE OR RIVAL",
                14,
                MenuArt.MutedText,
                TextAnchor.MiddleCenter,
                KalyeahResources.UiFont(),
                minSize: 10);
            Ui.ClearOutline(_honorStatusLabel);
            Stretch(_honorStatusLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            RefreshLayout();
        }

        public void Bind(MatchResultsPayload payload)
        {
            if (_disposed || payload == null) return;

            _honoredPlayerId = -1;
            _localPlayerId = int.MinValue;
            _blueScratch.Clear();
            _redScratch.Clear();

            IReadOnlyList<MatchPlayerStats> players = payload.Players ?? Array.Empty<MatchPlayerStats>();
            MvpCalculator.ResolveTeamMvps(players, out _blueMvpPlayerId, out _redMvpPlayerId);

            bool localIsMvp = false;
            for (int i = 0; i < players.Count; i++)
            {
                MatchPlayerStats p = players[i];
                if (p.IsLocalPlayer)
                {
                    _localPlayerId = p.PlayerId;
                    localIsMvp = (p.Team == 0 && p.PlayerId == _blueMvpPlayerId)
                              || (p.Team == 1 && p.PlayerId == _redMvpPlayerId);
                }

                if (p.Team == 0)
                {
                    if (_blueScratch.Count < MaxPlayersPerTeam) _blueScratch.Add(p);
                }
                else
                {
                    if (_redScratch.Count < MaxPlayersPerTeam) _redScratch.Add(p);
                }
            }

            _lastRewards = MvpCalculator.ComputeRewards(
                payload.Mode,
                payload.LocalTeamWon,
                localIsMvp,
                payload.IsFirstWinOfDay,
                payload.IsRanked,
                payload.IsHotspot,
                payload.WeeklyBaryaAlreadyEarned);

            ApplyHeroBanner(payload, localIsMvp);

            bool blueWon = (payload.LocalTeam == 0 && payload.LocalTeamWon)
                        || (payload.LocalTeam != 0 && !payload.LocalTeamWon);
            bool redWon = !blueWon;

            for (int i = 0; i < MaxPlayersPerTeam; i++)
            {
                if (i < _blueScratch.Count)
                {
                    MatchPlayerStats p = _blueScratch[i];
                    bool isMvp = p.PlayerId == _blueMvpPlayerId;
                    BindRow(_blueRows[i], in p, isMvp, starSaved: isMvp && !blueWon && payload.IsRanked, ColBlueAccent);
                }
                else
                {
                    HideRow(_blueRows[i]);
                }

                if (i < _redScratch.Count)
                {
                    MatchPlayerStats p = _redScratch[i];
                    bool isMvp = p.PlayerId == _redMvpPlayerId;
                    BindRow(_redRows[i], in p, isMvp, starSaved: isMvp && !redWon && payload.IsRanked, ColRedAccent);
                }
                else
                {
                    HideRow(_redRows[i]);
                }
            }

            _activeRowPairs = Mathf.Max(1, Mathf.Max(_blueScratch.Count, _redScratch.Count));
            if (_honorStatusLabel != null)
            {
                _honorStatusLabel.text = "1 HONOR PER MATCH · TAP A TEAMMATE OR RIVAL";
                Ui.SetTextColor(_honorStatusLabel, MenuArt.MutedText);
            }

            RefreshLayout();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;
        }

        public bool TryHonorPlayer(int playerId)
        {
            if (_disposed || _honoredPlayerId >= 0 || playerId == _localPlayerId)
                return false;

            PlayerRowUi target = FindActiveRow(playerId);
            if (target == null || target.IsLocalPlayer)
                return false;

            _honoredPlayerId = playerId;
            UpdateHonorVisuals();

            string honoredName = target.PlayerName ?? ("Player " + playerId);
            if (_honorStatusLabel != null)
            {
                _honorStatusLabel.text = "HONORED " + honoredName.ToUpperInvariant() + " · SPORTSMANSHIP +1";
                Ui.SetTextColor(_honorStatusLabel, MenuArt.GoldLight);
            }

            PlayerHonored?.Invoke(playerId, honoredName);
            return true;
        }

        public bool TryGetRowHonorButton(int playerId, out Button button)
        {
            PlayerRowUi row = FindActiveRow(playerId);
            if (row != null && row.HonorButton != null && row.HonorButton.gameObject.activeSelf)
            {
                button = row.HonorButton;
                return true;
            }
            button = null;
            return false;
        }

        public bool TryGetRowMvpBadgeText(int playerId, out string text)
        {
            PlayerRowUi row = FindActiveRow(playerId);
            if (row != null && row.MvpBadgeRoot != null && row.MvpBadgeRoot.gameObject.activeSelf && row.MvpBadgeText != null)
            {
                text = row.MvpBadgeText.text;
                return true;
            }
            text = string.Empty;
            return false;
        }

        public void RefreshLayout()
        {
            if (_disposed || SafeRoot == null) return;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float bottomH = Mathf.Max(90f, minTouch);
            float rowH = Mathf.Max(96f, minTouch);
            float honorW = Mathf.Max(MinHonorColWidthCanvas, minTouch);

            // Band 1: Hero Banner (top 0..-72)
            HeroBannerRect.offsetMin = new Vector2(SideMarginCanvas, -HeroBannerHeightCanvas);
            HeroBannerRect.offsetMax = new Vector2(-SideMarginCanvas, 0f);

            // Band 2: Team Headers (top -74..-96)
            float headerTop = HeroBannerHeightCanvas + BandGapCanvas;
            TeamHeadersRect.offsetMin = new Vector2(SideMarginCanvas, -(headerTop + TeamHeaderHeightCanvas));
            TeamHeadersRect.offsetMax = new Vector2(-SideMarginCanvas, -headerTop);
            LayoutTeamHeader(_blueHeader, honorW);
            LayoutTeamHeader(_redHeader, honorW);

            // Band 4: Bottom Action Bar (bottom 0..bottomH)
            BottomBarRect.offsetMin = new Vector2(SideMarginCanvas, 0f);
            BottomBarRect.offsetMax = new Vector2(-SideMarginCanvas, bottomH);

            float homeW = Mathf.Max(180f, minTouch);
            float playW = Mathf.Max(230f, minTouch);
            _homeButtonRect.anchorMin = new Vector2(0f, 0f);
            _homeButtonRect.anchorMax = new Vector2(0f, 1f);
            _homeButtonRect.pivot = new Vector2(0f, 0.5f);
            _homeButtonRect.anchoredPosition = Vector2.zero;
            _homeButtonRect.sizeDelta = new Vector2(homeW, 0f);

            _playAgainButtonRect.anchorMin = new Vector2(1f, 0f);
            _playAgainButtonRect.anchorMax = new Vector2(1f, 1f);
            _playAgainButtonRect.pivot = new Vector2(1f, 0.5f);
            _playAgainButtonRect.anchoredPosition = Vector2.zero;
            _playAgainButtonRect.sizeDelta = new Vector2(playW, 0f);

            _honorStatusRect.offsetMin = new Vector2(homeW + 10f, -18f);
            _honorStatusRect.offsetMax = new Vector2(-(playW + 10f), 18f);

            // Band 3: Scoreboard ScrollRect Viewport strictly disjoint between TeamHeaders and BottomBar
            float vpTop = headerTop + TeamHeaderHeightCanvas + BandGapCanvas;
            float vpBottom = bottomH + BandGapCanvas;
            ScoreboardViewportRect.offsetMin = new Vector2(SideMarginCanvas, vpBottom);
            ScoreboardViewportRect.offsetMax = new Vector2(-SideMarginCanvas, -vpTop);

            int rows = _activeRowPairs > 0 ? _activeRowPairs : MaxPlayersPerTeam;
            float contentH = rows * rowH + Mathf.Max(0, rows - 1) * RowGapCanvas;
            _scrollContent.sizeDelta = new Vector2(0f, contentH);

            LayoutTeamColumn(_blueRows, rowH, honorW);
            LayoutTeamColumn(_redRows, rowH, honorW);
        }

        void ApplyHeroBanner(MatchResultsPayload payload, bool localIsMvp)
        {
            bool won = payload.LocalTeamWon;
            if (_backdropWash != null) _backdropWash.color = won ? ColVictoryWash : ColDefeatWash;
            if (_sunburstImg != null) _sunburstImg.color = won ? ColVictorySun : ColDefeatSun;
            if (_heroTopRail != null) _heroTopRail.color = won ? MenuArt.Gold : MenuArt.Coral;
            if (_heroBannerBg != null)
            {
                Color border = won ? MenuArt.GoldBorder : new Color(0.92f, 0.42f, 0.36f, 0.55f);
                _heroBannerBg.sprite = MenuArt.ChamferCard(MenuArt.PanelTop, MenuArt.PanelBottom, border, 12, 2, 0.28f);
            }

            if (_outcomeTitle != null)
            {
                _outcomeTitle.text = won ? "VICTORY" : "DEFEAT";
                Ui.SetTextColor(_outcomeTitle, won ? MenuArt.GoldLight : MenuArt.Coral);
            }

            string modeName = payload.Mode.ToString().ToUpperInvariant();
            string mapName = string.IsNullOrWhiteSpace(payload.MapName) ? "BARANGAY" : payload.MapName.Trim().ToUpperInvariant();
            string prefix = payload.IsHotspot ? "HOTSPOT · " : (payload.IsRanked ? "RANKED · " : string.Empty);
            if (_modeSubtitle != null)
                _modeSubtitle.text = prefix + modeName + " · " + mapName;

            if (_scoreLabel != null)
                _scoreLabel.text = payload.BlueScore + "  -  " + payload.RedScore;

            if (payload.IsHotspot)
            {
                if (_baryaValueText != null) _baryaValueText.text = "HOTSPOT · FOR FUN";
                if (_baryaBreakdownText != null) _baryaBreakdownText.text = "ALL KIDS UNLOCKED";
                if (_masteryXpText != null) _masteryXpText.text = "NO ONLINE REWARDS";
                if (_rankedStarText != null)
                {
                    _rankedStarText.text = "LOCAL LAN SHOWDOWN";
                    Ui.SetTextColor(_rankedStarText, MenuArt.MutedText);
                }
                return;
            }

            if (_baryaValueText != null)
                _baryaValueText.text = "+" + _lastRewards.TotalBarya + " BARYA";

            if (_baryaBreakdownText != null)
            {
                if (_lastRewards.MvpBaryaBonus > 0 && _lastRewards.FirstWinBaryaBonus > 0)
                    _baryaBreakdownText.text = "+20 MVP · +300 1ST WIN";
                else if (_lastRewards.FirstWinBaryaBonus > 0)
                    _baryaBreakdownText.text = "+" + _lastRewards.BaseBarya + " BASE · +300 1ST";
                else if (_lastRewards.MvpBaryaBonus > 0)
                    _baryaBreakdownText.text = "+" + _lastRewards.BaseBarya + " BASE · +20 MVP";
                else
                    _baryaBreakdownText.text = "MATCH BARYA REWARD";
            }

            if (_masteryXpText != null)
                _masteryXpText.text = "+" + _lastRewards.TotalMasteryXp + " MASTERY XP";

            if (_rankedStarText != null)
            {
                if (_lastRewards.StarSavedByMvp)
                {
                    _rankedStarText.text = "MVP STAR SAVED";
                    Ui.SetTextColor(_rankedStarText, MenuArt.GoldLight);
                }
                else if (payload.IsRanked)
                {
                    _rankedStarText.text = _lastRewards.RankedStarDelta > 0 ? "+1 RANKED STAR" : "-1 RANKED STAR";
                    Ui.SetTextColor(_rankedStarText, _lastRewards.RankedStarDelta > 0 ? MenuArt.SuccessGreen : MenuArt.Coral);
                }
                else if (localIsMvp)
                {
                    _rankedStarText.text = "TEAM MVP BONUS";
                    Ui.SetTextColor(_rankedStarText, MenuArt.GoldLight);
                }
                else
                {
                    _rankedStarText.text = "KID MASTERY PROGRESS";
                    Ui.SetTextColor(_rankedStarText, MenuArt.MutedText);
                }
            }
        }

        void BindRow(PlayerRowUi row, in MatchPlayerStats stats, bool isMvp, bool starSaved, Color teamAccent)
        {
            row.Active = true;
            row.PlayerId = stats.PlayerId;
            row.PlayerName = stats.PlayerName;
            row.IsLocalPlayer = stats.IsLocalPlayer;
            row.Root.gameObject.SetActive(true);

            row.CardBg.sprite = MenuArt.ChamferCard(
                isMvp ? ColRowMvpTop : ColRowTop,
                isMvp ? ColRowMvpBottom : ColRowBottom,
                isMvp ? MenuArt.GoldBorder : MenuArt.PanelBorder,
                10,
                isMvp ? 2 : 1,
                0.24f);
            row.MvpOutline.gameObject.SetActive(isMvp);
            row.TeamRail.color = isMvp ? MenuArt.Gold : teamAccent;

            bool hasKid = LoreCodexData.TryGetKid(stats.KidId, out KidLoreEntry kid);
            string kidDisplay = hasKid ? kid.DisplayName : stats.KidId;
            string roleDisplay = hasKid ? LoreCodexData.RoleName(kid.Role) : "Kid";

            row.PlayerNameText.text = stats.PlayerName;
            Ui.SetTextColor(row.PlayerNameText, stats.IsLocalPlayer ? MenuArt.GoldLight : MenuArt.Cream);
            row.KidRoleText.text = kidDisplay + " · " + roleDisplay;

            row.MvpBadgeRoot.gameObject.SetActive(isMvp);
            if (isMvp)
                row.MvpBadgeText.text = starSaved ? "MVP · STAR SAVED" : "MVP";

            row.StatTText.text = stats.Tags.ToString();
            row.StatRText.text = stats.Rescues.ToString();
            row.StatCText.text = stats.Captures.ToString();
            Color statColor = isMvp ? MenuArt.GoldLight : MenuArt.Cream;
            Ui.SetTextColor(row.StatTText, statColor);
            Ui.SetTextColor(row.StatRText, statColor);
            Ui.SetTextColor(row.StatCText, statColor);

            // Portrait with RectMask2D + EnvelopeParent (zero distortion)
            Texture2D tex = KalyeahResources.Texture("portraits/" + stats.KidId)
                         ?? KalyeahResources.Texture("ui/avatars/" + stats.KidId);
            row.PortraitRaw.texture = tex;
            row.PortraitRaw.enabled = tex != null;
            if (tex != null && tex.height > 0)
                row.PortraitFitter.aspectRatio = (float)tex.width / tex.height;

            bool fallback = tex == null;
            row.PortraitFallbackDisc.enabled = fallback;
            row.PortraitFallbackDisc.color = teamAccent;
            row.PortraitInitial.enabled = fallback;
            if (fallback)
                row.PortraitInitial.text = kidDisplay.Length > 0 ? kidDisplay.Substring(0, 1).ToUpperInvariant() : "K";

            // Honor button vs local self badge
            bool canHonor = !stats.IsLocalPlayer;
            row.HonorButtonRect.gameObject.SetActive(canHonor);
            row.SelfBadgeRect.gameObject.SetActive(!canHonor);
            if (canHonor)
            {
                row.HonorButton.interactable = true;
                ApplyHonorButtonVisual(row, honoredThis: false, honorLockedElsewhere: false);
            }
        }

        static void HideRow(PlayerRowUi row)
        {
            row.Active = false;
            row.PlayerId = int.MinValue;
            row.PlayerName = string.Empty;
            row.IsLocalPlayer = false;
            row.Root.gameObject.SetActive(false);
        }

        void UpdateHonorVisuals()
        {
            UpdateColumnHonorVisuals(_blueRows);
            UpdateColumnHonorVisuals(_redRows);
        }

        void UpdateColumnHonorVisuals(PlayerRowUi[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                PlayerRowUi row = rows[i];
                if (!row.Active || row.IsLocalPlayer) continue;

                bool isHonored = _honoredPlayerId == row.PlayerId;
                bool lockedElsewhere = _honoredPlayerId >= 0 && !isHonored;
                row.HonorButton.interactable = !HasHonored;
                ApplyHonorButtonVisual(row, isHonored, lockedElsewhere);
            }
        }

        static void ApplyHonorButtonVisual(PlayerRowUi row, bool honoredThis, bool honorLockedElsewhere)
        {
            if (honoredThis)
            {
                row.HonorPlate.sprite = MenuArt.CtaGold(8);
                row.HonorPlate.color = Color.white;
                row.HonorIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Check);
                row.HonorIcon.color = MenuArt.Ink;
                row.HonorLabel.text = "HONORED";
                Ui.SetTextColor(row.HonorLabel, MenuArt.Ink);
                row.HonorVisualGroup.alpha = 1f;
            }
            else
            {
                row.HonorPlate.sprite = MenuArt.CtaGhost(8);
                row.HonorPlate.color = Color.white;
                row.HonorIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
                row.HonorIcon.color = MenuArt.GoldLight;
                row.HonorLabel.text = "HONOR";
                Ui.SetTextColor(row.HonorLabel, MenuArt.Cream);
                row.HonorVisualGroup.alpha = honorLockedElsewhere ? ColHonorDim.a : 1f;
            }
        }

        PlayerRowUi FindActiveRow(int playerId)
        {
            for (int i = 0; i < _blueRows.Length; i++)
            {
                if (_blueRows[i].Active && _blueRows[i].PlayerId == playerId)
                    return _blueRows[i];
            }
            for (int i = 0; i < _redRows.Length; i++)
            {
                if (_redRows[i].Active && _redRows[i].PlayerId == playerId)
                    return _redRows[i];
            }
            return null;
        }

        TeamHeaderUi BuildTeamHeader(string name, Transform parent, string title, Color accent, float anchorMinX, float anchorMaxX)
        {
            RectTransform rt = Ui.Panel(name, parent, new Vector2(anchorMinX, 0f), new Vector2(anchorMaxX, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("Bg", rt, MenuArt.RoundSolid(6), ColHeaderBar, raycast: false);
            Ui.MakeAccentRail("Rail", rt, accent, width: 4f, verticalInset: 4f);

            Text teamTitle = MakeFittedText("TeamTitle", rt, title, 14, accent, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 10);
            Ui.ClearOutline(teamTitle);

            RectTransform colT = MakeHeaderCol("ColT", rt, "T");
            RectTransform colR = MakeHeaderCol("ColR", rt, "R");
            RectTransform colC = MakeHeaderCol("ColC", rt, "C");
            RectTransform colHonor = MakeHeaderCol("ColHonor", rt, "HONOR");

            return new TeamHeaderUi
            {
                TeamTitle = teamTitle,
                ColT = colT,
                ColR = colR,
                ColC = colC,
                ColHonor = colHonor,
            };
        }

        static RectTransform MakeHeaderCol(string name, Transform parent, string text)
        {
            RectTransform rt = Ui.Panel(name, parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(StatColWidthCanvas, 0f));
            Text lbl = MakeFittedText("Label", rt, text, 13, MenuArt.MutedText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 9);
            Ui.ClearOutline(lbl);
            Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 0f), new Vector2(-2f, 0f));
            return rt;
        }

        static void LayoutTeamHeader(TeamHeaderUi h, float honorW)
        {
            float rightStats = honorW + 4f;
            h.ColHonor.anchoredPosition = Vector2.zero;
            h.ColHonor.sizeDelta = new Vector2(honorW, 0f);

            h.ColC.anchoredPosition = new Vector2(-rightStats, 0f);
            h.ColC.sizeDelta = new Vector2(StatColWidthCanvas, 0f);

            h.ColR.anchoredPosition = new Vector2(-(rightStats + StatColWidthCanvas), 0f);
            h.ColR.sizeDelta = new Vector2(StatColWidthCanvas, 0f);

            h.ColT.anchoredPosition = new Vector2(-(rightStats + StatColWidthCanvas * 2f), 0f);
            h.ColT.sizeDelta = new Vector2(StatColWidthCanvas, 0f);

            float titleRightInset = rightStats + StatColWidthCanvas * 3f + 6f;
            Stretch(h.TeamTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-titleRightInset, 0f));
        }

        PlayerRowUi BuildPlayerRow(string name, Transform parent, Color teamAccent)
        {
            RectTransform rowRt = Ui.Panel(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 96f));

            // Visual card inset 6 c top/bottom so cards show crisp 14 c breathing room while Honor hit targets stay >= 48 dp
            RectTransform cardRt = Ui.Fill("CardVisual", rowRt);
            cardRt.offsetMin = new Vector2(0f, RowCardInsetY);
            cardRt.offsetMax = new Vector2(0f, -RowCardInsetY);

            Image cardBg = Ui.MakeSliced("Bg", cardRt, MenuArt.ChamferCard(ColRowTop, ColRowBottom, MenuArt.PanelBorder, 10, 1, 0.24f), Color.white, raycast: false);
            Image mvpOutline = Ui.MakeSliced("MvpOutline", cardRt, MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 2), Color.white, raycast: false);
            mvpOutline.gameObject.SetActive(false);

            RectTransform railRt = Ui.MakeAccentRail("TeamRail", cardRt, teamAccent, width: 4f, verticalInset: 8f);
            Image teamRail = railRt.GetComponent<Image>();

            // Portrait thumbnail (RectMask2D + EnvelopeParent -> zero stretch across all 4 aspects)
            RectTransform portraitFrame = Ui.Panel("PortraitFrame", cardRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(54f, 54f));
            Ui.MakeSliced("Plate", portraitFrame, MenuArt.StatChip(8), Color.white, raycast: false);

            RectTransform maskRt = Ui.Fill("Mask", portraitFrame);
            maskRt.offsetMin = new Vector2(3f, 3f);
            maskRt.offsetMax = new Vector2(-3f, -3f);
            maskRt.gameObject.AddComponent<RectMask2D>();

            Image fallbackDisc = Ui.MakeImage("FallbackDisc", maskRt, teamAccent, round: true, raycast: false);
            Stretch(fallbackDisc.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);

            Text initialText = MakeFittedText("Initial", maskRt, "K", 22, MenuArt.Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 12);
            Ui.ClearOutline(initialText);
            Stretch(initialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var rawGo = new GameObject("PortraitRaw", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawGo.transform.SetParent(maskRt, false);
            var rawImg = rawGo.GetComponent<RawImage>();
            rawImg.raycastTarget = false;
            rawImg.enabled = false;
            Stretch(rawImg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var arf = rawGo.GetComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = 1f;

            // Player Name + Kid/Role + Gold MVP Crown Badge
            RectTransform infoBox = Ui.Fill("InfoBox", cardRt);
            Text nameText = MakeFittedText("PlayerName", infoBox, "", 17, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 10);
            Stretch(nameText.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);

            Text kidRoleText = MakeFittedText("KidRole", infoBox, "", 12, MenuArt.MutedText, TextAnchor.MiddleLeft, KalyeahResources.UiFont(), minSize: 8);
            Ui.ClearOutline(kidRoleText);
            Stretch(kidRoleText.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);

            RectTransform mvpBadge = Ui.Panel("MvpBadge", infoBox, new Vector2(0f, 0.04f), new Vector2(0.96f, 0.34f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("Pill", mvpBadge, MenuArt.RoundSolid(6), MenuArt.Gold, raycast: false);
            Image crownIcon = Ui.MakeImage("Crown", mvpBadge, MenuArt.Ink, round: false, raycast: false);
            crownIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Star);
            crownIcon.preserveAspect = true;
            RectTransform crownRt = crownIcon.rectTransform;
            crownRt.anchorMin = new Vector2(0f, 0.5f);
            crownRt.anchorMax = new Vector2(0f, 0.5f);
            crownRt.pivot = new Vector2(0f, 0.5f);
            crownRt.anchoredPosition = new Vector2(5f, 0f);
            crownRt.sizeDelta = new Vector2(14f, 14f);

            Text mvpText = MakeFittedText("MvpText", mvpBadge, "MVP", 12, MenuArt.Ink, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 8);
            Ui.ClearOutline(mvpText);
            Stretch(mvpText.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 1f), new Vector2(-4f, -1f));
            mvpBadge.gameObject.SetActive(false);

            // T / R / C stat columns
            RectTransform statT = MakeStatCell("StatT", cardRt, out Text statTText);
            RectTransform statR = MakeStatCell("StatR", cardRt, out Text statRText);
            RectTransform statC = MakeStatCell("StatC", cardRt, out Text statCText);

            // 1-Tap Honor Button (>= 48 dp hit catcher spanning full row height)
            var honorGo = new GameObject("HonorButton", typeof(RectTransform), typeof(Image), typeof(Button));
            honorGo.transform.SetParent(rowRt, false);
            RectTransform honorBtnRt = honorGo.GetComponent<RectTransform>();
            honorBtnRt.anchorMin = new Vector2(1f, 0f);
            honorBtnRt.anchorMax = new Vector2(1f, 1f);
            honorBtnRt.pivot = new Vector2(1f, 0.5f);
            honorBtnRt.anchoredPosition = Vector2.zero;
            honorBtnRt.sizeDelta = new Vector2(MinHonorColWidthCanvas, 0f);

            var honorHit = honorGo.GetComponent<Image>();
            honorHit.color = new Color(0f, 0f, 0f, 0.001f);

            RectTransform honorVis = Ui.Panel("Visual", honorBtnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 44f));
            CanvasGroup honorCg = honorVis.gameObject.AddComponent<CanvasGroup>();
            Image honorPlate = Ui.MakeSliced("Plate", honorVis, MenuArt.CtaGhost(8), Color.white, raycast: false);

            Image honorIcon = Ui.MakeImage("Icon", honorVis, MenuArt.GoldLight, round: false, raycast: false);
            honorIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
            honorIcon.preserveAspect = true;
            RectTransform iconRt = honorIcon.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(7f, 0f);
            iconRt.sizeDelta = new Vector2(15f, 15f);

            Text honorLabel = MakeFittedText("Label", honorVis, "HONOR", 12, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 9);
            Stretch(honorLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(23f, 2f), new Vector2(-5f, -2f));

            var honorBtn = honorGo.GetComponent<Button>();
            honorBtn.targetGraphic = honorPlate;
            Ui.AddPressFeel(honorGo, 0.94f);

            // Non-interactive YOU pill shown on local player's own row (prevents self-honor)
            RectTransform selfBadge = Ui.Panel("SelfBadge", rowRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(88f, 38f));
            Ui.MakeSliced("Plate", selfBadge, MenuArt.StatChip(8), Color.white, raycast: false);
            Text selfText = MakeFittedText("Label", selfBadge, "YOU", 13, MenuArt.GoldLight, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 10);
            Stretch(selfText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            selfBadge.gameObject.SetActive(false);

            var row = new PlayerRowUi
            {
                Root = rowRt,
                CardBg = cardBg,
                MvpOutline = mvpOutline,
                TeamRail = teamRail,
                PortraitFallbackDisc = fallbackDisc,
                PortraitInitial = initialText,
                PortraitRaw = rawImg,
                PortraitFitter = arf,
                InfoBox = infoBox,
                PlayerNameText = nameText,
                KidRoleText = kidRoleText,
                MvpBadgeRoot = mvpBadge,
                MvpBadgeText = mvpText,
                StatTRect = statT,
                StatRRect = statR,
                StatCRect = statC,
                StatTText = statTText,
                StatRText = statRText,
                StatCText = statCText,
                HonorButtonRect = honorBtnRt,
                HonorButton = honorBtn,
                HonorPlate = honorPlate,
                HonorIcon = honorIcon,
                HonorLabel = honorLabel,
                HonorVisualGroup = honorCg,
                SelfBadgeRect = selfBadge,
            };

            honorBtn.onClick.AddListener(() => TryHonorPlayer(row.PlayerId));
            rowRt.gameObject.SetActive(false);
            return row;
        }

        static RectTransform MakeStatCell(string name, Transform parent, out Text label)
        {
            RectTransform rt = Ui.Panel(name, parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(StatColWidthCanvas, 44f));
            label = MakeFittedText("Val", rt, "0", 19, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 11);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 0f), new Vector2(-2f, 0f));
            return rt;
        }

        static void LayoutTeamColumn(PlayerRowUi[] rows, float rowH, float honorW)
        {
            float rightStats = honorW + 4f;
            float infoRightInset = rightStats + StatColWidthCanvas * 3f + 6f;

            for (int i = 0; i < rows.Length; i++)
            {
                PlayerRowUi r = rows[i];
                float y = -(i * (rowH + RowGapCanvas));
                r.Root.anchoredPosition = new Vector2(0f, y);
                r.Root.sizeDelta = new Vector2(0f, rowH);

                r.HonorButtonRect.sizeDelta = new Vector2(honorW, 0f);
                r.SelfBadgeRect.anchoredPosition = new Vector2(-(honorW - 88f) * 0.5f, 0f);

                r.StatCRect.anchoredPosition = new Vector2(-rightStats, 0f);
                r.StatRRect.anchoredPosition = new Vector2(-(rightStats + StatColWidthCanvas), 0f);
                r.StatTRect.anchoredPosition = new Vector2(-(rightStats + StatColWidthCanvas * 2f), 0f);

                r.InfoBox.offsetMin = new Vector2(72f, 5f);
                r.InfoBox.offsetMax = new Vector2(-infoRightInset, -5f);
            }
        }

        static Button BuildActionButton(
            string name,
            Transform parent,
            string label,
            Sprite sprite,
            Color textColor,
            bool addSheen,
            out RectTransform hitRect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            hitRect = go.GetComponent<RectTransform>();

            var hitImg = go.GetComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0.001f); // transparent >= 48 dp touch catcher

            RectTransform vis = Ui.Panel("Visual", hitRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, addSheen ? 58f : 54f));
            Image plate = Ui.MakeSliced("Plate", vis, sprite, Color.white, raycast: false);
            if (addSheen)
                Ui.AddSheen(vis);

            Text txt = MakeFittedText("Label", vis, label, addSheen ? 22 : 20, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 12);
            Ui.SetTextColor(txt, textColor);
            Stretch(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = plate;
            Ui.AddPressFeel(go, 0.95f);
            return btn;
        }

        void HandleSafeAreaChanged(SafeArea _) => RefreshLayout();

        void HandlePlayAgainClicked() => _onPlayAgain?.Invoke();

        void HandleHomeClicked() => _onHome?.Invoke();

        static Text MakeFittedText(string name, Transform parent, string text, int size, Color color, TextAnchor align, Font font, int minSize)
        {
            Text label = Ui.MakeText(name, parent, text, size, color, align, font);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minSize;
            label.resizeTextMaxSize = size;
            return label;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        void DisposeFromLifecycle()
        {
            if (SafeAreaComponent != null)
                SafeAreaComponent.Changed -= HandleSafeAreaChanged;
            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (SafeAreaComponent != null)
                SafeAreaComponent.Changed -= HandleSafeAreaChanged;
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }
    }
}
