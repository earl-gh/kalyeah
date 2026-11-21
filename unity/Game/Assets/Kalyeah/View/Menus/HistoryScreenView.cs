using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// 7-Tier Tagalog Ranked progression screen (Kahoy -&gt; Batang Kalyeah, LP progress bar, Peak LP, Win Rate)
    /// plus scrollable <see cref="AppSession.History"/> match log.
    /// </summary>
    public sealed class HistoryScreenView : IDisposable
    {
        static readonly Color BackdropSolid = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color BackdropWash = new Color(0.02f, 0.035f, 0.07f, 0.72f);
        static readonly Color TileTop = new Color(0.100f, 0.152f, 0.242f, 0.92f);
        static readonly Color TileBottom = new Color(0.052f, 0.086f, 0.152f, 0.96f);
        static readonly Color TileLockedTop = new Color(0.058f, 0.088f, 0.148f, 0.78f);
        static readonly Color TileLockedBottom = new Color(0.034f, 0.054f, 0.098f, 0.86f);
        static readonly Color WinRowTop = new Color(0.080f, 0.195f, 0.185f, 0.92f);
        static readonly Color WinRowBottom = new Color(0.042f, 0.108f, 0.125f, 0.96f);
        static readonly Color WinRowBorder = new Color(0.24f, 0.82f, 0.56f, 0.34f);
        static readonly Color LossRowTop = new Color(0.205f, 0.095f, 0.135f, 0.92f);
        static readonly Color LossRowBottom = new Color(0.112f, 0.052f, 0.088f, 0.96f);
        static readonly Color LossRowBorder = new Color(0.96f, 0.38f, 0.26f, 0.34f);
        static readonly Color TrackTop = new Color(0.024f, 0.044f, 0.082f, 0.98f);
        static readonly Color TrackBottom = new Color(0.056f, 0.092f, 0.160f, 0.98f);
        static readonly Color TrackBorder = new Color(0.76f, 0.88f, 0.96f, 0.24f);
        static readonly Color DividerColor = new Color(0.76f, 0.88f, 0.96f, 0.14f);
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Coral = new Color(0.96f, 0.38f, 0.26f, 1f);
        static readonly Color WinGreen = MenuArt.SuccessGreen;
        static readonly Color Muted = MenuArt.MutedText;
        static readonly Color Ink = MenuArt.Ink;

        static readonly string[] TierEmblemInitials = { "K", "B", "T", "P", "G", "BR", "BK" };

        [ExecuteAlways]
        sealed class HistoryLifecycle : MonoBehaviour
        {
            [NonSerialized] public HistoryScreenView Owner;
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

        readonly Action _onBack;
        Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _shell;

        readonly Image[] _tierBadgeBgs = new Image[AppSession.RankTiers.Length];
        readonly Image[] _tierBadgeOutlines = new Image[AppSession.RankTiers.Length];
        readonly Text[] _tierBadgeLabels = new Text[AppSession.RankTiers.Length];
        readonly List<GameObject> _spawnedRows = new List<GameObject>(32);

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _headerAccentRect;
        RectTransform _headerTitleRect;
        RectTransform _playerChipRect;
        RectTransform _bodyRect;
        LadderCellSizer _ladderSizer;

        Text _headerTitle;
        Text _backLabel;
        Text _playerChipText;

        Image _heroAccentRail;
        Image _heroCrestBg;
        Text _heroCrestEmblem;
        Text _heroTierName;
        Text _heroFlavorText;
        Text _heroLpNumbers;
        RectTransform _lpBarFill;
        Text _peakLpValue;
        Text _winRateValue;
        Text _matchesValue;
        Text _ladderHeader;

        Text _logHeader;
        RectTransform _historyContent;
        GameObject _emptyStateCard;
        Text _emptyStateText;

        bool _refreshingLayout;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public SafeArea SafeAreaComponent => _safeArea;
        public int DisplayedHistoryCount => _spawnedRows.Count;

        public HistoryScreenView(Transform parent, DataSource data, Action onBack = null)
            : this(parent, onBack)
        {
            _ = data;
        }

        public HistoryScreenView(Transform parent, Action onBack = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _onBack = onBack;
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("HistoryScreen", parent);
            BuildBackdrop(Root);

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null)
            {
                _safeArea = parentSafe;
                SafeRoot = Ui.Fill("SafeRoot", Root);
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
            }
            _safeArea.Changed += OnSafeAreaChanged;

            _shell = Ui.Panel("Shell", SafeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 0f));

            BuildHeader(_shell);
            BuildBody(_shell);

            var lifecycle = Root.gameObject.AddComponent<HistoryLifecycle>();
            lifecycle.Owner = this;

            AppSession.Current.Changed += Refresh;
            SettingsStore.Current.Changed += Refresh;
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;

            _safeArea.Apply();
            Refresh();
            RefreshLayout();
        }

        public void Show()
        {
            if (Root != null) Root.gameObject.SetActive(true);
            Refresh();
            RefreshLayout();
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AppSession.Current.Changed -= Refresh;
            SettingsStore.Current.Changed -= Refresh;
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void DisposeFromLifecycle() => Dispose();

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        public void RefreshLayout()
        {
            if (_disposed || _refreshingLayout || Root == null || _shell == null) return;
            _refreshingLayout = true;
            try
            {
                if (_canvas == null) _canvas = Root.GetComponentInParent<Canvas>();

                _safeArea?.Apply();

                Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float clampedWidth = Mathf.Clamp(safeSize.x - 24f, 640f, 1320f);
                _shell.sizeDelta = new Vector2(clampedWidth, 0f);

                float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
                float minGutter = Mathf.Max(8f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));
                float headerH = Mathf.Max(104f, minTouch + 8f);

                if (_headerRect != null)
                {
                    _headerRect.anchoredPosition = new Vector2(0f, -4f);
                    _headerRect.sizeDelta = new Vector2(0f, headerH);
                }

                Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(142f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                if (_backBtnRect != null)
                {
                    _backBtnRect.anchoredPosition = new Vector2(10f, 0f);
                    _backBtnRect.sizeDelta = backSize;
                }

                float titleLeft = 10f + backSize.x + minGutter + 6f;
                if (_headerAccentRect != null)
                    _headerAccentRect.anchoredPosition = new Vector2(titleLeft, 0f);

                if (_headerTitleRect != null)
                {
                    _headerTitleRect.anchorMin = new Vector2(0f, 0f);
                    _headerTitleRect.anchorMax = new Vector2(0.67f, 1f);
                    _headerTitleRect.offsetMin = new Vector2(titleLeft + 14f, 8f);
                    _headerTitleRect.offsetMax = new Vector2(-8f, -8f);
                }

                if (_playerChipRect != null)
                {
                    _playerChipRect.anchorMin = new Vector2(0.69f, 0.5f);
                    _playerChipRect.anchorMax = new Vector2(1f, 0.5f);
                    _playerChipRect.offsetMin = new Vector2(0f, -24f);
                    _playerChipRect.offsetMax = new Vector2(-10f, 24f);
                }

                if (_bodyRect != null)
                {
                    _bodyRect.offsetMin = new Vector2(0f, 8f);
                    _bodyRect.offsetMax = new Vector2(0f, -(4f + headerH + 8f));
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(_shell);
                if (_ladderSizer != null)
                {
                    _ladderSizer.Apply();
                    if (_ladderSizer.transform is RectTransform ladderRt)
                        LayoutRebuilder.ForceRebuildLayoutImmediate(ladderRt);
                }
            }
            finally
            {
                _refreshingLayout = false;
            }
        }

        static void BuildBackdrop(RectTransform root)
        {
            Image baseFill = Ui.MakeImage("Backdrop", root, BackdropSolid);
            Stretch(baseFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            baseFill.gameObject.AddComponent<RectMask2D>();

            Texture2D mainBg = KalyeahResources.Texture("ui/home/stall") ?? KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (mainBg != null)
            {
                var rawGo = new GameObject("ArtBackdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                rawGo.transform.SetParent(baseFill.transform, false);
                var raw = rawGo.GetComponent<RawImage>();
                raw.texture = mainBg;
                raw.color = new Color(0.90f, 0.88f, 0.96f, 0.24f);
                raw.raycastTarget = false;
                Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var fitter = rawGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = mainBg.height > 0 ? (float)mainBg.width / mainBg.height : (16f / 9f);
            }

            Image wash = Ui.MakeImage("BackdropWash", baseFill.transform, BackdropWash);
            Stretch(wash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform parent)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(104f, minTouch + 8f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, headerH));

            Image divider = Ui.MakeImage("HeaderDivider", _headerRect, DividerColor);
            Stretch(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 1.5f));

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(142f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = Ui.Panel("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize);
            Image backHit = _backBtnRect.gameObject.AddComponent<Image>();
            backHit.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform backVisual = Ui.Panel("Visual", _backBtnRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
            Image backFace = Ui.MakeSliced("Plate", backVisual, MenuArt.CtaGhost(12), Color.white, raycast: false);

            Image chev = Ui.MakeImage("Chevron", backVisual, MenuArt.GoldLight, round: false, raycast: false);
            chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            RectTransform cr = chev.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(22f, 0f);
            cr.sizeDelta = new Vector2(16f, 16f);
            cr.localEulerAngles = new Vector3(0f, 0f, 180f);

            Button backBtn = _backBtnRect.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backFace;
            backBtn.onClick.AddListener(() => _onBack?.Invoke());
            Ui.AddPressFeel(_backBtnRect, 0.95f);

            _backLabel = MakeFittedText("Label", backVisual, "BACK", 20, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_backLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 4f), new Vector2(-10f, -4f));

            _headerAccentRect = Ui.Panel("TitleAccent", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(166f, 0f), new Vector2(4f, 34f));
            Image accentImg = _headerAccentRect.gameObject.AddComponent<Image>();
            accentImg.sprite = MenuArt.AccentRail(2);
            accentImg.type = Image.Type.Sliced;
            accentImg.color = Gold;
            accentImg.raycastTarget = false;

            _headerTitle = MakeFittedText("Title", _headerRect, "RANKED & MATCH HISTORY", 28, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, new Vector2(0f, 0f), new Vector2(0.67f, 1f), new Vector2(180f, 8f), new Vector2(-8f, -8f));

            Image playerChip = Ui.MakeSliced("PlayerChip", _headerRect, MenuArt.StatChip(10), Color.white, raycast: false);
            _playerChipRect = playerChip.rectTransform;
            Stretch(_playerChipRect, new Vector2(0.69f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -24f), new Vector2(-10f, 24f));
            Ui.MakeAccentRail("ChipRail", _playerChipRect, Gold, width: 3f, verticalInset: 10f);
            _playerChipText = MakeFittedText("PlayerName", playerChip.transform, "", 20, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_playerChipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-10f, -2f));
        }

        void BuildBody(RectTransform parent)
        {
            _bodyRect = Ui.Panel("Body", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _bodyRect.offsetMin = new Vector2(0f, 8f);
            _bodyRect.offsetMax = new Vector2(0f, -116f);

            // Left 46%: Current Tier Hero Card + 7-Tier Tagalog Ranked Ladder
            RectTransform leftCol = Ui.Panel("RankedColumn", _bodyRect, new Vector2(0f, 0f), new Vector2(0.46f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftCol.offsetMax = new Vector2(-8f, 0f);

            Image heroCardBg = Ui.MakeSliced("HeroTierCard", leftCol, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            Stretch(heroCardBg.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Ui.MakeCornerBrackets("HeroBrackets", heroCardBg.transform, new Color(Gold.r, Gold.g, Gold.b, 0.45f));
            _heroAccentRail = Ui.MakeAccentRail("HeroRail", heroCardBg.transform, Gold, width: 4f, verticalInset: 18f).GetComponent<Image>();

            RectTransform crestFrame = Ui.Panel("CrestFrame", heroCardBg.transform, new Vector2(0.05f, 0.56f), new Vector2(0.26f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image crestGlow = Ui.MakeImage("CrestGlow", crestFrame, new Color(Gold.r, Gold.g, Gold.b, 0.24f), round: false, raycast: false);
            crestGlow.sprite = MenuArt.Glow();
            Stretch(crestGlow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));

            var crestGo = new GameObject("TierCrest", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            crestGo.transform.SetParent(crestFrame, false);
            _heroCrestBg = crestGo.GetComponent<Image>();
            _heroCrestBg.sprite = Ui.Circle();
            _heroCrestBg.color = Gold;
            _heroCrestBg.raycastTarget = false;
            Stretch(_heroCrestBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var crestArf = crestGo.GetComponent<AspectRatioFitter>();
            crestArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            crestArf.aspectRatio = 1f;

            Image crestRing = Ui.MakeImage("CrestRing", _heroCrestBg.transform, new Color(1f, 0.96f, 0.86f, 0.52f));
            crestRing.sprite = KomiksUi.RingSprite(128, 4);
            Stretch(crestRing.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));

            _heroCrestEmblem = MakeFittedText("Emblem", _heroCrestBg.transform, "K", 36, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _heroCrestEmblem.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(_heroCrestEmblem.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));

            _heroTierName = MakeFittedText("TierName", heroCardBg.transform, "KAHOY", 34, Gold, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            Stretch(_heroTierName.rectTransform, new Vector2(0.29f, 0.72f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            _heroFlavorText = MakeFittedText("TierFlavor", heroCardBg.transform, "", 18, Muted, TextAnchor.UpperLeft);
            Ui.ClearOutline(_heroFlavorText);
            Stretch(_heroFlavorText.rectTransform, new Vector2(0.29f, 0.56f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);

            // LP Progress Bar
            Image barTrack = Ui.MakeSliced("LpBarTrack", heroCardBg.transform, MenuArt.Round(TrackTop, TrackBottom, TrackBorder, 8, 1, 0.12f), Color.white, raycast: false);
            Stretch(barTrack.rectTransform, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero);

            Image barFillImg = Ui.MakeSliced("LpBarFill", barTrack.transform, MenuArt.AccentRail(6), Gold, raycast: false);
            _lpBarFill = barFillImg.rectTransform;
            Stretch(_lpBarFill, Vector2.zero, new Vector2(0.25f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));

            _heroLpNumbers = MakeFittedText("LpNumbers", barTrack.transform, "0 / 200 LP", 18, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _heroLpNumbers.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(_heroLpNumbers.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            // 3 Stat Tiles: Peak LP, Win Rate, Matches
            _peakLpValue = BuildStatTile(heroCardBg.rectTransform, "PeakLpTile", new Vector2(0.05f, 0.06f), new Vector2(0.33f, 0.30f), "PEAK LP");
            _winRateValue = BuildStatTile(heroCardBg.rectTransform, "WinRateTile", new Vector2(0.36f, 0.06f), new Vector2(0.64f, 0.30f), "WIN RATE");
            _matchesValue = BuildStatTile(heroCardBg.rectTransform, "MatchesTile", new Vector2(0.67f, 0.06f), new Vector2(0.95f, 0.30f), "RECORD");

            // 7-Tier Tagalog Progression Strip (Kahoy -> Batang Kalyeah)
            Image ladderCard = Ui.MakeSliced("TierLadderCard", leftCol, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            Stretch(ladderCard.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.41f), Vector2.zero, Vector2.zero);

            _ladderHeader = MakeFittedText("LadderTitle", ladderCard.transform, "7-TIER TAGALOG LADDER", 19, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_ladderHeader);
            Stretch(_ladderHeader.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);

            RectTransform tiersGrid = Ui.Panel("TiersGrid", ladderCard.transform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.79f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var grid = tiersGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.spacing = new Vector2(6f, 6f);
            grid.cellSize = new Vector2(96f, 76f);
            _ladderSizer = tiersGrid.gameObject.AddComponent<LadderCellSizer>();

            Sprite unlockedBadgeSprite = MenuArt.ChamferCard(TileTop, TileBottom, MenuArt.PanelBorder, 8, 1, 0.20f);
            Sprite activeOutlineSprite = MenuArt.ChamferOutline(MenuArt.GoldBorder, 8, 2);

            for (int i = 0; i < AppSession.RankTiers.Length; i++)
            {
                RankTierInfo t = AppSession.RankTiers[i];
                var badgeGo = new GameObject($"Tier_{t.Id}", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(tiersGrid, false);
                var bg = badgeGo.GetComponent<Image>();
                bg.sprite = unlockedBadgeSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = false;

                Image topBand = Ui.MakeSliced("Band", badgeGo.transform, MenuArt.AccentRail(2), HexColor(t.ColorHex, Gold), raycast: false);
                Stretch(topBand.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -5f), new Vector2(-8f, -1f));

                Image activeOutline = Ui.MakeSliced("ActiveOutline", badgeGo.transform, activeOutlineSprite, Color.white, raycast: false);
                activeOutline.enabled = false;

                Text lbl = MakeFittedText("Label", badgeGo.transform, $"{t.Name}\n{t.MinLp}+ LP", 16, MenuArt.Cream, TextAnchor.MiddleCenter, minSize: 11);
                lbl.resizeTextMinSize = 11;
                lbl.resizeTextMaxSize = 16;
                lbl.lineSpacing = 0.92f;
                Ui.ClearOutline(lbl);
                Stretch(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.87f), new Vector2(3f, 2f), new Vector2(-3f, -1f));

                _tierBadgeBgs[i] = bg;
                _tierBadgeOutlines[i] = activeOutline;
                _tierBadgeLabels[i] = lbl;
            }

            // Right 54%: Scrollable Match Log
            RectTransform rightCol = Ui.Panel("HistoryColumn", _bodyRect, new Vector2(0.46f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            rightCol.offsetMin = new Vector2(8f, 0f);
            Image rightBg = Ui.MakeSliced("HistoryBg", rightCol, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            Stretch(rightBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _logHeader = MakeFittedText("LogTitle", rightCol, "RECENT MATCHES", 22, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_logHeader);
            Stretch(_logHeader.rectTransform, new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.99f), Vector2.zero, Vector2.zero);

            var scrollGo = new GameObject("HistoryScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(rightCol, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.89f), Vector2.zero, Vector2.zero);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _historyContent = contentGo.GetComponent<RectTransform>();
            _historyContent.anchorMin = new Vector2(0f, 1f);
            _historyContent.anchorMax = new Vector2(1f, 1f);
            _historyContent.pivot = new Vector2(0.5f, 1f);
            _historyContent.anchoredPosition = Vector2.zero;
            _historyContent.sizeDelta = Vector2.zero;

            var vLayout = contentGo.GetComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(6, 6, 6, 6);
            vLayout.spacing = 8f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollRt;
            scroll.content = _historyContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            _emptyStateCard = new GameObject("EmptyState", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _emptyStateCard.transform.SetParent(_historyContent, false);
            var emptyImg = _emptyStateCard.GetComponent<Image>();
            emptyImg.sprite = MenuArt.ChamferCard(TileTop, TileBottom, MenuArt.PanelBorder, 10, 1, 0.20f);
            emptyImg.type = Image.Type.Sliced;
            emptyImg.color = Color.white;
            emptyImg.raycastTarget = false;
            var emptyLe = _emptyStateCard.GetComponent<LayoutElement>();
            emptyLe.minHeight = 140f;
            emptyLe.preferredHeight = 140f;
            _emptyStateText = MakeFittedText("EmptyText", _emptyStateCard.transform, "", 19, Muted, TextAnchor.MiddleCenter);
            Ui.ClearOutline(_emptyStateText);
            Stretch(_emptyStateText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -12f));
        }

        static Text BuildStatTile(RectTransform parent, string name, Vector2 min, Vector2 max, string caption)
        {
            RectTransform tile = Ui.Panel(name, parent, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Ui.MakeSliced("Bg", tile, MenuArt.StatChip(8), Color.white, raycast: false);

            Text cap = MakeFittedText("Caption", tile, caption, 16, Muted, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(cap);
            cap.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(cap.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 0.94f), new Vector2(4f, 0f), new Vector2(-4f, -2f));

            Text val = MakeFittedText("Value", tile, "-", 22, MenuArt.GoldLight, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            val.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(val.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.56f), new Vector2(4f, 2f), new Vector2(-4f, 0f));
            return val;
        }

        public void Refresh()
        {
            AppSession session = AppSession.Current;
            bool fil = SettingsStore.Current.Language == "fil";

            if (_headerTitle != null) _headerTitle.text = fil ? "RANGGO AT KASAYSAYAN" : "RANKED & MATCH HISTORY";
            if (_backLabel != null) _backLabel.text = fil ? "BALIK" : "BACK";
            if (_ladderHeader != null) _ladderHeader.text = fil ? "7-ANTAS NG RANGGO SA KALYE" : "7-TIER TAGALOG LADDER";
            if (_logHeader != null) _logHeader.text = fil ? $"MGA LABAN ({session.History.Count})" : $"MATCH LOG ({session.History.Count})";
            if (_playerChipText != null) _playerChipText.text = $"{session.PlayerName}  ·  {session.CurrentTier.Name.ToUpperInvariant()}";

            int currentTierIdx = Mathf.Clamp(session.TierIndex, 0, AppSession.RankTiers.Length - 1);
            RankTierInfo tier = session.CurrentTier;
            Color tierColor = HexColor(tier.ColorHex, Gold);
            Color tierAccent = HexColor(tier.AccentHex, Gold);

            if (_heroAccentRail != null) _heroAccentRail.color = tierColor;
            if (_heroCrestBg != null) _heroCrestBg.color = tierColor;
            if (_heroCrestEmblem != null)
            {
                string initial = currentTierIdx < TierEmblemInitials.Length
                    ? TierEmblemInitials[currentTierIdx]
                    : (!string.IsNullOrEmpty(tier.Name) ? tier.Name.Substring(0, 1).ToUpperInvariant() : "K");
                _heroCrestEmblem.text = initial;
                bool lightDisc = Ui.Luminance(tierColor) > 0.62f;
                Ui.SetTextColor(_heroCrestEmblem, lightDisc ? Ink : MenuArt.Cream);
            }
            if (_heroTierName != null)
            {
                _heroTierName.text = tier.Name.ToUpperInvariant();
                _heroTierName.color = tierAccent;
            }
            if (_heroFlavorText != null) _heroFlavorText.text = tier.Flavor;

            float progress = Mathf.Clamp01(session.TierProgress01);
            if (_lpBarFill != null)
            {
                _lpBarFill.anchorMax = new Vector2(Mathf.Max(0.03f, progress), 1f);
                var fillImg = _lpBarFill.GetComponent<Image>();
                if (fillImg != null) fillImg.color = tierColor;
            }

            int nextLp = session.NextTierLp;
            if (_heroLpNumbers != null)
            {
                _heroLpNumbers.text = nextLp > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} / {1} LP  ({2:0}%)", session.Lp, nextLp, progress * 100f)
                    : string.Format(CultureInfo.InvariantCulture, "{0} LP  ·  MYTHIC CROWN", session.Lp);
            }

            if (_peakLpValue != null) _peakLpValue.text = session.PeakLp.ToString(CultureInfo.InvariantCulture) + " LP";
            if (_winRateValue != null)
                _winRateValue.text = session.TotalMatches > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0:0}%", session.WinRate01 * 100f)
                    : "0%";
            if (_matchesValue != null)
                _matchesValue.text = string.Format(CultureInfo.InvariantCulture, "{0}W - {1}L", session.TotalWins, Mathf.Max(0, session.TotalMatches - session.TotalWins));

            Sprite unlockedBadgeSprite = MenuArt.ChamferCard(TileTop, TileBottom, MenuArt.PanelBorder, 8, 1, 0.20f);
            Sprite lockedBadgeSprite = MenuArt.ChamferCard(TileLockedTop, TileLockedBottom, DividerColor, 8, 1, 0.12f);
            for (int i = 0; i < AppSession.RankTiers.Length; i++)
            {
                RankTierInfo t = AppSession.RankTiers[i];
                bool isCurrent = i == currentTierIdx;
                bool unlocked = i <= currentTierIdx;
                if (_tierBadgeBgs[i] != null)
                {
                    _tierBadgeBgs[i].sprite = unlocked ? unlockedBadgeSprite : lockedBadgeSprite;
                    _tierBadgeBgs[i].color = Color.white;
                }
                if (_tierBadgeOutlines[i] != null)
                    _tierBadgeOutlines[i].enabled = isCurrent;
                if (_tierBadgeLabels[i] != null)
                {
                    string marker = isCurrent ? "» " : ""; // Barlow Condensed has no star
                    _tierBadgeLabels[i].text = $"{marker}{t.Name}\n{t.MinLp}+ LP";
                    _tierBadgeLabels[i].color = isCurrent ? MenuArt.GoldLight : unlocked ? MenuArt.Cream : new Color(0.55f, 0.62f, 0.74f, 0.85f);
                }
            }

            RebuildHistoryRows(session.History, fil);
        }

        void RebuildHistoryRows(IReadOnlyList<MatchHistoryEntry> entries, bool fil)
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
                KalyeahResources.Kill(_spawnedRows[i]);
            _spawnedRows.Clear();

            bool empty = entries == null || entries.Count == 0;
            if (_emptyStateCard != null) _emptyStateCard.SetActive(empty);
            if (empty)
            {
                if (_emptyStateText != null)
                    _emptyStateText.text = fil
                        ? "Wala pang naitalang laban.\nMaglaro ng Agawang Base o Larong Kalye para umakyat mula Kahoy hanggang Batang Kalyeah!"
                        : "No street matches recorded yet.\nPlay Agawang Base or Larong Kalye to climb from Kahoy to Batang Kalyeah!";
                return;
            }

            Sprite winCard = MenuArt.ChamferCard(WinRowTop, WinRowBottom, WinRowBorder, 10, 1, 0.22f);
            Sprite lossCard = MenuArt.ChamferCard(LossRowTop, LossRowBottom, LossRowBorder, 10, 1, 0.22f);

            for (int i = 0; i < entries.Count; i++)
            {
                MatchHistoryEntry e = entries[i];
                var rowGo = new GameObject($"MatchRow_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                rowGo.transform.SetParent(_historyContent, false);
                var rowImg = rowGo.GetComponent<Image>();
                rowImg.sprite = e.Won ? winCard : lossCard;
                rowImg.type = Image.Type.Sliced;
                rowImg.color = Color.white;
                rowImg.raycastTarget = false;

                var le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = 86f;
                le.preferredHeight = 86f;

                Ui.MakeAccentRail("OutcomeStripe", rowGo.transform, e.Won ? WinGreen : Coral, width: 5f, verticalInset: 10f);

                RectTransform portraitFrame = Ui.Panel("PortraitFrame", rowGo.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                Stretch(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 8f), new Vector2(72f, -8f));
                Ui.MakeSliced("Plate", portraitFrame, MenuArt.StatChip(8), Color.white, raycast: false);

                Texture2D portrait = KalyeahResources.Texture("portraits/" + e.CharacterId) ?? KalyeahResources.Texture("ui/avatars/" + e.CharacterId);
                if (portrait != null)
                {
                    RectTransform portraitSlot = Ui.Panel("PortraitSlot", portraitFrame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    Stretch(portraitSlot, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

                    var pGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    pGo.transform.SetParent(portraitSlot, false);
                    var raw = pGo.GetComponent<RawImage>();
                    raw.texture = portrait;
                    raw.raycastTarget = false;
                    Stretch(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                    var arf = pGo.GetComponent<AspectRatioFitter>();
                    arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    arf.aspectRatio = 0.75f;
                }

                string outcomeWord = e.Won ? (fil ? "PANALO" : "VICTORY") : (fil ? "TALO" : "DEFEAT");
                Text outcomeLbl = MakeRowText("Outcome", rowGo.transform, outcomeWord, 21, e.Won ? WinGreen : Coral, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
                Stretch(outcomeLbl.rectTransform, new Vector2(0f, 0.50f), new Vector2(0.54f, 0.94f), new Vector2(84f, 0f), Vector2.zero);

                string kidLabel = string.IsNullOrEmpty(e.CharacterId) ? "Junjun" : (char.ToUpperInvariant(e.CharacterId[0]) + e.CharacterId.Substring(1));
                string modeLabel = FormatModeName(e.Mode);
                Text subLbl = MakeRowText("ModeAndKid", rowGo.transform, $"{modeLabel}  ·  {kidLabel}", 17, MenuArt.Cream, TextAnchor.UpperLeft);
                Ui.ClearOutline(subLbl);
                Stretch(subLbl.rectTransform, new Vector2(0f, 0.06f), new Vector2(0.54f, 0.50f), new Vector2(84f, 0f), Vector2.zero);

                // Framed tactical score chip
                RectTransform scoreChip = Ui.MakeStatChip("ScoreChip", rowGo.transform, $"{e.MyScore} - {e.TheirScore}", MenuArt.Cream, out Text scoreLbl, fontSize: 22, font: KalyeahResources.DisplayFont());
                Stretch(scoreChip, new Vector2(0.55f, 0.5f), new Vector2(0.73f, 0.5f), new Vector2(0f, -22f), new Vector2(0f, 22f));
                scoreLbl.resizeTextForBestFit = true;
                scoreLbl.resizeTextMinSize = 12;
                scoreLbl.resizeTextMaxSize = 22;
                scoreLbl.verticalOverflow = VerticalWrapMode.Overflow;

                string deltaStr = e.LpDelta >= 0 ? ("+" + e.LpDelta.ToString(CultureInfo.InvariantCulture) + " LP") : (e.LpDelta.ToString(CultureInfo.InvariantCulture) + " LP");
                Text deltaLbl = MakeRowText("LpDelta", rowGo.transform, deltaStr, 20, e.LpDelta >= 0 ? MenuArt.GoldLight : Coral, TextAnchor.LowerRight, KalyeahResources.DisplayFont());
                Stretch(deltaLbl.rectTransform, new Vector2(0.74f, 0.50f), new Vector2(1f, 0.94f), Vector2.zero, new Vector2(-14f, 0f));

                string timeStr = FormatTimestamp(e.TimestampUnixSeconds);
                Text timeLbl = MakeRowText("Timestamp", rowGo.transform, timeStr, 17, Muted, TextAnchor.UpperRight);
                Ui.ClearOutline(timeLbl);
                Stretch(timeLbl.rectTransform, new Vector2(0.74f, 0.06f), new Vector2(1f, 0.50f), Vector2.zero, new Vector2(-14f, 0f));

                _spawnedRows.Add(rowGo);
            }
        }

        static string FormatModeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Agawang Base";
            switch (raw.Trim().ToLowerInvariant())
            {
                case "practice": return "5v5 Practice";
                case "training": return "Training";
                case "online": return "Online 5v5";
                case "hotspothost":
                case "hotspotclient":
                case "hotspot": return "LAN Hotspot";
                case "patintero": return "Patintero";
                case "tumbang_preso":
                case "tumbangpreso": return "Tumbang Preso";
                case "langit_lupa":
                case "langitlupa": return "Langit-Lupa";
                default: return raw;
            }
        }

        static string FormatTimestamp(long unixSeconds)
        {
            if (unixSeconds <= 0) return "Recent";
            try
            {
                DateTime dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().DateTime;
                return dt.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "Recent";
            }
        }

        static Color HexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string clean = hex.StartsWith("#", StringComparison.Ordinal) ? hex : ("#" + hex);
            return ColorUtility.TryParseHtmlString(clean, out Color parsed) ? parsed : fallback;
        }

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null, int minSize = 12)
        {
            int clampedMin = Mathf.Max(10, minSize);
            int clampedMax = Mathf.Max(clampedMin, maxSize);
            Text t = Ui.MakeText(name, parent, text, clampedMax, color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = clampedMin;
            t.resizeTextMaxSize = clampedMax;
            return t;
        }

        static Text MakeRowText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            int clampedMax = Mathf.Max(14, maxSize);
            Text t = Ui.MakeText(name, parent, text, clampedMax, color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = clampedMax;
            return t;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        [ExecuteAlways]
        sealed class LadderCellSizer : MonoBehaviour
        {
            bool _subscribed;

            void OnEnable()
            {
                EnsureSubscribed();
                Apply();
            }

            void OnDisable() => Unsubscribe();

            void OnDestroy() => Unsubscribe();

            void OnRectTransformDimensionsChange() => Apply();

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
                Apply();
            }

            public void Apply()
            {
                var grid = GetComponent<GridLayoutGroup>();
                var rt = transform as RectTransform;
                if (grid == null || rt == null) return;
                Rect r = rt.rect;
                if (r.width <= 10f || r.height <= 10f) return;
                float cellW = (r.width - grid.spacing.x * 3f) / 4f;
                float cellH = (r.height - grid.spacing.y) / 2f;
                grid.cellSize = new Vector2(Mathf.Max(48f, cellW), Mathf.Max(36f, cellH));
            }
        }
    }
}
