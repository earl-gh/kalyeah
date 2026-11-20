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
        static readonly Color BackdropSolid = new Color(0.06f, 0.10f, 0.20f, 1f);
        static readonly Color BackdropWash = new Color(0.05f, 0.08f, 0.17f, 0.84f);
        static readonly Color HeaderBg = new Color(0.06f, 0.10f, 0.22f, 0.96f);
        static readonly Color PanelBg = new Color(0.08f, 0.12f, 0.24f, 0.96f);
        static readonly Color CardBg = new Color(0.11f, 0.16f, 0.30f, 0.94f);
        static readonly Color WinRowBg = new Color(0.10f, 0.28f, 0.22f, 0.95f);
        static readonly Color LossRowBg = new Color(0.28f, 0.12f, 0.16f, 0.95f);
        static readonly Color Gold = new Color(0.99f, 0.79f, 0.20f, 1f);
        static readonly Color Coral = new Color(0.96f, 0.38f, 0.26f, 1f);
        static readonly Color WinGreen = new Color(0.32f, 0.92f, 0.56f, 1f);
        static readonly Color Muted = new Color(0.76f, 0.83f, 0.94f, 1f);
        static readonly Color Ink = new Color(0.08f, 0.10f, 0.16f, 1f);

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
        readonly Text[] _tierBadgeLabels = new Text[AppSession.RankTiers.Length];
        readonly List<GameObject> _spawnedRows = new List<GameObject>(32);

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _headerTitleRect;
        RectTransform _playerChipRect;
        RectTransform _bodyRect;
        LadderCellSizer _ladderSizer;

        Text _headerTitle;
        Text _backLabel;
        Text _playerChipText;

        Image _heroCardBg;
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
                float headerH = Mathf.Max(108f, minTouch + 12f);

                if (_headerRect != null)
                {
                    _headerRect.anchoredPosition = new Vector2(0f, -6f);
                    _headerRect.sizeDelta = new Vector2(0f, headerH);
                }

                Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(140f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                if (_backBtnRect != null)
                {
                    _backBtnRect.anchoredPosition = new Vector2(10f, 0f);
                    _backBtnRect.sizeDelta = backSize;
                }

                if (_headerTitleRect != null)
                {
                    _headerTitleRect.anchorMin = new Vector2(0f, 0f);
                    _headerTitleRect.anchorMax = new Vector2(0.67f, 1f);
                    _headerTitleRect.offsetMin = new Vector2(10f + backSize.x + minGutter, 8f);
                    _headerTitleRect.offsetMax = new Vector2(-8f, -8f);
                }

                if (_playerChipRect != null)
                {
                    _playerChipRect.anchorMin = new Vector2(0.69f, 0.18f);
                    _playerChipRect.anchorMax = new Vector2(1f, 0.82f);
                    _playerChipRect.offsetMin = Vector2.zero;
                    _playerChipRect.offsetMax = new Vector2(-10f, 0f);
                }

                if (_bodyRect != null)
                {
                    _bodyRect.offsetMin = new Vector2(0f, 8f);
                    _bodyRect.offsetMax = new Vector2(0f, -(6f + headerH + 10f));
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

            Texture2D mainBg = KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
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
            float headerH = Mathf.Max(108f, minTouch + 12f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(0f, headerH));
            Image bg = KomiksUi.MakeSlicedFill("HeaderBg", _headerRect, HeaderBg, KomiksUi.RadiusM);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(140f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = Ui.Panel("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize);
            Image backImg = _backBtnRect.gameObject.AddComponent<Image>();
            backImg.sprite = KomiksUi.RoundedSprite(KomiksUi.RadiusS);
            backImg.type = Image.Type.Sliced;
            backImg.color = Coral;
            Button backBtn = _backBtnRect.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() => _onBack?.Invoke());

            _backLabel = MakeFittedText("Label", _backBtnRect, "<- BACK", 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_backLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            _headerTitle = MakeFittedText("Title", _headerRect, "RANKED & MATCH HISTORY", 26, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, new Vector2(0f, 0f), new Vector2(0.67f, 1f), new Vector2(166f, 8f), new Vector2(-8f, -8f));

            Image playerChip = KomiksUi.MakeSlicedFill("PlayerChip", _headerRect, CardBg, KomiksUi.RadiusS);
            _playerChipRect = playerChip.rectTransform;
            Stretch(_playerChipRect, new Vector2(0.69f, 0.18f), new Vector2(1f, 0.82f), Vector2.zero, new Vector2(-10f, 0f));
            _playerChipText = MakeFittedText("PlayerName", playerChip.transform, "", 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_playerChipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
        }

        void BuildBody(RectTransform parent)
        {
            _bodyRect = Ui.Panel("Body", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _bodyRect.offsetMin = new Vector2(0f, 8f);
            _bodyRect.offsetMax = new Vector2(0f, -124f);

            // Left 46%: Current Tier Hero Card + 7-Tier Tagalog Ranked Ladder
            RectTransform leftCol = Ui.Panel("RankedColumn", _bodyRect, new Vector2(0f, 0f), new Vector2(0.46f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftCol.offsetMax = new Vector2(-8f, 0f);

            _heroCardBg = KomiksUi.MakeSlicedFill("HeroTierCard", leftCol, PanelBg, KomiksUi.RadiusM);
            Stretch(_heroCardBg.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            RectTransform crestFrame = Ui.Panel("CrestFrame", _heroCardBg.transform, new Vector2(0.05f, 0.56f), new Vector2(0.26f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
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

            _heroCrestEmblem = MakeFittedText("Emblem", _heroCrestBg.transform, "K", 36, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _heroCrestEmblem.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(_heroCrestEmblem.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));

            _heroTierName = MakeFittedText("TierName", _heroCardBg.transform, "KAHOY", 34, Gold, TextAnchor.LowerLeft, KalyeahResources.DisplayFont());
            Stretch(_heroTierName.rectTransform, new Vector2(0.29f, 0.72f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            _heroFlavorText = MakeFittedText("TierFlavor", _heroCardBg.transform, "", 18, Muted, TextAnchor.UpperLeft);
            Stretch(_heroFlavorText.rectTransform, new Vector2(0.29f, 0.56f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);

            // LP Progress Bar
            Image barTrack = KomiksUi.MakeSlicedFill("LpBarTrack", _heroCardBg.transform, new Color(0.03f, 0.05f, 0.11f, 0.95f), KomiksUi.RadiusS);
            Stretch(barTrack.rectTransform, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero);

            Image barFillImg = KomiksUi.MakeSlicedFill("LpBarFill", barTrack.transform, Gold, KomiksUi.RadiusS);
            _lpBarFill = barFillImg.rectTransform;
            Stretch(_lpBarFill, Vector2.zero, new Vector2(0.25f, 1f), new Vector2(2f, 2f), new Vector2(-2f, -2f));

            _heroLpNumbers = MakeFittedText("LpNumbers", barTrack.transform, "0 / 200 LP", 18, Ui.Cream, TextAnchor.MiddleCenter);
            _heroLpNumbers.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(_heroLpNumbers.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            // 3 Stat Tiles: Peak LP, Win Rate, Matches
            _peakLpValue = BuildStatTile(_heroCardBg.rectTransform, "PeakLpTile", new Vector2(0.05f, 0.06f), new Vector2(0.33f, 0.30f), "PEAK LP");
            _winRateValue = BuildStatTile(_heroCardBg.rectTransform, "WinRateTile", new Vector2(0.36f, 0.06f), new Vector2(0.64f, 0.30f), "WIN RATE");
            _matchesValue = BuildStatTile(_heroCardBg.rectTransform, "MatchesTile", new Vector2(0.67f, 0.06f), new Vector2(0.95f, 0.30f), "RECORD");

            // 7-Tier Tagalog Progression Strip (Kahoy -> Batang Kalyeah)
            Image ladderCard = KomiksUi.MakeSlicedFill("TierLadderCard", leftCol, PanelBg, KomiksUi.RadiusM);
            Stretch(ladderCard.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.41f), Vector2.zero, Vector2.zero);

            _ladderHeader = MakeFittedText("LadderTitle", ladderCard.transform, "7-TIER TAGALOG LADDER", 19, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_ladderHeader.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);

            RectTransform tiersGrid = Ui.Panel("TiersGrid", ladderCard.transform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.79f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var grid = tiersGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.spacing = new Vector2(6f, 6f);
            grid.cellSize = new Vector2(96f, 76f);
            _ladderSizer = tiersGrid.gameObject.AddComponent<LadderCellSizer>();

            for (int i = 0; i < AppSession.RankTiers.Length; i++)
            {
                RankTierInfo t = AppSession.RankTiers[i];
                var badgeGo = new GameObject($"Tier_{t.Id}", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(tiersGrid, false);
                var bg = badgeGo.GetComponent<Image>();
                bg.sprite = KomiksUi.RoundedSprite(KomiksUi.RadiusS);
                bg.type = Image.Type.Sliced;
                bg.color = CardBg;

                Image topBand = Ui.MakeImage("Band", badgeGo.transform, HexColor(t.ColorHex, Gold));
                Stretch(topBand.rectTransform, new Vector2(0f, 0.88f), Vector2.one, new Vector2(2f, 0f), new Vector2(-2f, 0f));

                Text lbl = MakeFittedText("Label", badgeGo.transform, $"{t.Name}\n{t.MinLp}+ LP", 16, Ui.Cream, TextAnchor.MiddleCenter, minSize: 11);
                lbl.resizeTextMinSize = 11;
                lbl.resizeTextMaxSize = 16;
                lbl.lineSpacing = 0.92f;
                Stretch(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.87f), new Vector2(3f, 2f), new Vector2(-3f, -1f));

                _tierBadgeBgs[i] = bg;
                _tierBadgeLabels[i] = lbl;
            }

            // Right 54%: Scrollable Match Log
            RectTransform rightCol = Ui.Panel("HistoryColumn", _bodyRect, new Vector2(0.46f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            rightCol.offsetMin = new Vector2(8f, 0f);
            Image rightBg = KomiksUi.MakeSlicedFill("HistoryBg", rightCol, PanelBg, KomiksUi.RadiusM);
            Stretch(rightBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _logHeader = MakeFittedText("LogTitle", rightCol, "RECENT MATCHES", 22, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
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
            emptyImg.sprite = KomiksUi.RoundedSprite(KomiksUi.RadiusS);
            emptyImg.type = Image.Type.Sliced;
            emptyImg.color = new Color(0.06f, 0.10f, 0.20f, 0.9f);
            var emptyLe = _emptyStateCard.GetComponent<LayoutElement>();
            emptyLe.minHeight = 140f;
            emptyLe.preferredHeight = 140f;
            _emptyStateText = MakeFittedText("EmptyText", _emptyStateCard.transform, "", 19, Muted, TextAnchor.MiddleCenter);
            Stretch(_emptyStateText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -12f));
        }

        static Text BuildStatTile(RectTransform parent, string name, Vector2 min, Vector2 max, string caption)
        {
            RectTransform tile = Ui.Panel(name, parent, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image bg = KomiksUi.MakeSlicedFill("Bg", tile, new Color(0.05f, 0.08f, 0.16f, 0.92f), KomiksUi.RadiusS);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text cap = MakeFittedText("Caption", tile, caption, 18, Muted, TextAnchor.UpperCenter);
            cap.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(cap.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 0.96f), new Vector2(4f, 0f), new Vector2(-4f, -2f));

            Text val = MakeFittedText("Value", tile, "-", 22, Ui.Cream, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            val.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(val.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.56f), new Vector2(4f, 2f), new Vector2(-4f, 0f));
            return val;
        }

        public void Refresh()
        {
            AppSession session = AppSession.Current;
            bool fil = SettingsStore.Current.Language == "fil";

            if (_headerTitle != null) _headerTitle.text = fil ? "RANGGO AT KASAYSAYAN" : "RANKED & MATCH HISTORY";
            if (_backLabel != null) _backLabel.text = fil ? "<- BALIK" : "<- BACK";
            if (_ladderHeader != null) _ladderHeader.text = fil ? "7-ANTAS NG RANGGO SA KALYE" : "7-TIER TAGALOG LADDER";
            if (_logHeader != null) _logHeader.text = fil ? $"MGA LABAN ({session.History.Count})" : $"MATCH LOG ({session.History.Count})";
            if (_playerChipText != null) _playerChipText.text = $"{session.PlayerName}  ·  {session.CurrentTier.Name.ToUpperInvariant()}";

            int currentTierIdx = Mathf.Clamp(session.TierIndex, 0, AppSession.RankTiers.Length - 1);
            RankTierInfo tier = session.CurrentTier;
            Color tierColor = HexColor(tier.ColorHex, Gold);
            Color tierDark = HexColor(tier.DarkHex, PanelBg);
            Color tierAccent = HexColor(tier.AccentHex, Gold);

            if (_heroCardBg != null)
                _heroCardBg.color = new Color(Mathf.Lerp(PanelBg.r, tierDark.r, 0.45f), Mathf.Lerp(PanelBg.g, tierDark.g, 0.45f), Mathf.Lerp(PanelBg.b, tierDark.b, 0.45f), 0.97f);
            if (_heroCrestBg != null) _heroCrestBg.color = tierColor;
            if (_heroCrestEmblem != null)
            {
                string initial = currentTierIdx < TierEmblemInitials.Length
                    ? TierEmblemInitials[currentTierIdx]
                    : (!string.IsNullOrEmpty(tier.Name) ? tier.Name.Substring(0, 1).ToUpperInvariant() : "K");
                _heroCrestEmblem.text = initial;
                bool lightDisc = (0.2126f * tierColor.r + 0.7152f * tierColor.g + 0.0722f * tierColor.b) > 0.62f;
                _heroCrestEmblem.color = lightDisc ? Ink : Ui.Cream;
                Outline emblemOutline = _heroCrestEmblem.GetComponent<Outline>();
                if (emblemOutline != null) emblemOutline.enabled = !lightDisc;
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

            for (int i = 0; i < AppSession.RankTiers.Length; i++)
            {
                RankTierInfo t = AppSession.RankTiers[i];
                bool isCurrent = i == currentTierIdx;
                bool unlocked = i <= currentTierIdx;
                if (_tierBadgeBgs[i] != null)
                {
                    _tierBadgeBgs[i].color = isCurrent
                        ? new Color(0.24f, 0.34f, 0.56f, 1f)
                        : unlocked
                            ? CardBg
                            : new Color(0.05f, 0.08f, 0.15f, 0.72f);
                }
                if (_tierBadgeLabels[i] != null)
                {
                    string marker = isCurrent ? "» " : ""; // Barlow Condensed has no star
                    _tierBadgeLabels[i].text = $"{marker}{t.Name}\n{t.MinLp}+ LP";
                    _tierBadgeLabels[i].color = isCurrent ? Gold : unlocked ? Ui.Cream : new Color(0.55f, 0.62f, 0.74f, 0.85f);
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

            for (int i = 0; i < entries.Count; i++)
            {
                MatchHistoryEntry e = entries[i];
                var rowGo = new GameObject($"MatchRow_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                rowGo.transform.SetParent(_historyContent, false);
                var rowImg = rowGo.GetComponent<Image>();
                rowImg.sprite = KomiksUi.RoundedSprite(KomiksUi.RadiusS);
                rowImg.type = Image.Type.Sliced;
                rowImg.color = e.Won ? WinRowBg : LossRowBg;

                var le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = 86f;
                le.preferredHeight = 86f;

                Image stripe = Ui.MakeImage("OutcomeStripe", rowGo.transform, e.Won ? WinGreen : Coral);
                Stretch(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f), new Vector2(8f, -2f));

                RectTransform portraitFrame = Ui.Panel("PortraitFrame", rowGo.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                Stretch(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 8f), new Vector2(70f, -8f));
                Image pPlate = KomiksUi.MakeSlicedFill("Plate", portraitFrame, new Color(0.06f, 0.09f, 0.18f, 0.85f), KomiksUi.RadiusS);
                Stretch(pPlate.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                Texture2D portrait = KalyeahResources.Texture("portraits/" + e.CharacterId) ?? KalyeahResources.Texture("ui/avatars/" + e.CharacterId);
                if (portrait != null)
                {
                    RectTransform portraitSlot = Ui.Panel("PortraitSlot", portraitFrame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    Stretch(portraitSlot, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

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
                Stretch(outcomeLbl.rectTransform, new Vector2(0f, 0.50f), new Vector2(0.54f, 0.94f), new Vector2(82f, 0f), Vector2.zero);

                string kidLabel = string.IsNullOrEmpty(e.CharacterId) ? "Junjun" : (char.ToUpperInvariant(e.CharacterId[0]) + e.CharacterId.Substring(1));
                string modeLabel = FormatModeName(e.Mode);
                Text subLbl = MakeRowText("ModeAndKid", rowGo.transform, $"{modeLabel}  ·  {kidLabel}", 18, Ui.Cream, TextAnchor.UpperLeft);
                Stretch(subLbl.rectTransform, new Vector2(0f, 0.06f), new Vector2(0.56f, 0.50f), new Vector2(82f, 0f), Vector2.zero);

                Text scoreLbl = MakeRowText("Score", rowGo.transform, $"{e.MyScore} - {e.TheirScore}", 24, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(scoreLbl.rectTransform, new Vector2(0.56f, 0.14f), new Vector2(0.73f, 0.86f), Vector2.zero, Vector2.zero);

                string deltaStr = e.LpDelta >= 0 ? ("+" + e.LpDelta.ToString(CultureInfo.InvariantCulture) + " LP") : (e.LpDelta.ToString(CultureInfo.InvariantCulture) + " LP");
                Text deltaLbl = MakeRowText("LpDelta", rowGo.transform, deltaStr, 20, e.LpDelta >= 0 ? Gold : Coral, TextAnchor.LowerRight, KalyeahResources.DisplayFont());
                Stretch(deltaLbl.rectTransform, new Vector2(0.73f, 0.50f), new Vector2(1f, 0.94f), Vector2.zero, new Vector2(-14f, 0f));

                string timeStr = FormatTimestamp(e.TimestampUnixSeconds);
                Text timeLbl = MakeRowText("Timestamp", rowGo.transform, timeStr, 18, Muted, TextAnchor.UpperRight);
                Stretch(timeLbl.rectTransform, new Vector2(0.73f, 0.06f), new Vector2(1f, 0.50f), Vector2.zero, new Vector2(-14f, 0f));

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
