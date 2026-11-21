using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Tabbed Settings screen (Audio, Controls, Graphics &amp; Accessibility, Donate + Reset Defaults)
    /// bound to <see cref="SettingsStore.Current"/> and persisted to <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class SettingsScreenView : IDisposable
    {
        public const int TabAudio = 0;
        public const int TabControls = 1;
        public const int TabGraphics = 2;
        /// <summary>Donate lives in Settings, not on home (docs/SPEC.md §8).</summary>
        public const int TabDonate = 3;

        // the home's palette: deep navy, dark tactical glass panels and Jeepney gold (docs/SPEC.md §10)
        static readonly Color BackdropSolid = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color BackdropWash = new Color(0.02f, 0.035f, 0.07f, 0.72f);
        static readonly Color RowTop = new Color(0.100f, 0.152f, 0.242f, 0.90f);
        static readonly Color RowBottom = new Color(0.054f, 0.088f, 0.156f, 0.94f);
        static readonly Color RowBorder = new Color(0.76f, 0.88f, 0.96f, 0.16f);
        static readonly Color TrackTop = new Color(0.024f, 0.044f, 0.082f, 0.98f);
        static readonly Color TrackBottom = new Color(0.056f, 0.092f, 0.160f, 0.98f);
        static readonly Color TrackBorder = new Color(0.76f, 0.88f, 0.96f, 0.24f);
        static readonly Color StepperTop = new Color(0.145f, 0.215f, 0.325f, 0.95f);
        static readonly Color StepperBottom = new Color(0.062f, 0.102f, 0.180f, 0.98f);
        static readonly Color StepperBorder = new Color(0.76f, 0.88f, 0.96f, 0.38f);
        static readonly Color ResetTop = new Color(0.16f, 0.08f, 0.10f, 0.76f);
        static readonly Color ResetBottom = new Color(0.09f, 0.04f, 0.06f, 0.86f);
        static readonly Color ResetBorder = new Color(0.96f, 0.44f, 0.36f, 0.72f);
        static readonly Color ResetText = new Color(1f, 0.74f, 0.68f, 1f);
        static readonly Color TabActiveTop = new Color(0.13f, 0.20f, 0.31f, 0.86f);
        static readonly Color TabActiveBottom = new Color(0.07f, 0.11f, 0.20f, 0.92f);
        static readonly Color DividerColor = new Color(0.76f, 0.88f, 0.96f, 0.14f);
        static readonly Color RowRailIdle = new Color(0.76f, 0.88f, 0.96f, 0.24f);
        static readonly Color TabBarIdle = new Color(0.76f, 0.88f, 0.96f, 0.18f);
        static readonly Color Gold = MenuArt.Gold;
        static readonly Color Ink = MenuArt.Ink;

        [ExecuteAlways]
        sealed class SettingsLifecycle : MonoBehaviour
        {
            [NonSerialized] public SettingsScreenView Owner;
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

        sealed class ToggleRowRefs
        {
            public LayoutElement RowLayout;
            public RectTransform TitleRect;
            public RectTransform ToggleRect;
        }

        sealed class SliderRowRefs
        {
            public LayoutElement RowLayout;
            public RectTransform TitleRect;
            public RectTransform MinusRect;
            public RectTransform SliderRect;
            public RectTransform TrackRect;
            public RectTransform FillAreaRect;
            public RectTransform HandleSlideRect;
            public RectTransform HandleRect;
            public RectTransform PlusRect;
            public RectTransform PercentRect;
        }

        sealed class SegmentedRowRefs
        {
            public LayoutElement RowLayout;
            public RectTransform TitleRect;
            public RectTransform GroupRect;
            public HorizontalLayoutGroup GroupLayout;
            public LayoutElement[] ChoiceLayouts;
        }

        sealed class DonateRowRefs
        {
            public LayoutElement RowLayout;
            public RectTransform CopyRect;
            public RectTransform ButtonRect;
        }

        sealed class ScrollPaneRefs
        {
            public ScrollRect Scroll;
            public RectTransform TrackRect;
            public RectTransform ThumbRect;
        }

        readonly Action _onBack;
        readonly Action _onDonate;
        Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _shell;

        readonly RectTransform[] _tabPanes = new RectTransform[4];
        readonly Image[] _tabPillBgs = new Image[4];
        readonly Text[] _tabPillLabels = new Text[4];
        readonly Image[] _tabBars = new Image[4];
        readonly LayoutElement[] _tabPillLayouts = new LayoutElement[4];
        readonly List<Action> _syncCallbacks = new List<Action>(20);
        readonly List<ToggleRowRefs> _toggleRows = new List<ToggleRowRefs>(8);
        readonly List<SliderRowRefs> _sliderRows = new List<SliderRowRefs>(4);
        readonly List<SegmentedRowRefs> _segmentedRows = new List<SegmentedRowRefs>(8);
        readonly List<ScrollPaneRefs> _scrollPanes = new List<ScrollPaneRefs>(3);
        DonateRowRefs _donateRow;

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _resetBtnRect;
        RectTransform _headerTitleRect;
        RectTransform _headerAccentRect;
        RectTransform _tabBarRect;
        HorizontalLayoutGroup _tabBarLayout;

        Text _headerTitle;
        Text _backLabel;
        Text _resetLabel;

        int _activeTab;
        bool _updatingUi;
        bool _refreshingLayout;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public SafeArea SafeAreaComponent => _safeArea;
        public int ActiveTab => _activeTab;

        public SettingsScreenView(Transform parent, DataSource data, Action onBack = null, Action onDonate = null)
            : this(parent, onBack, onDonate)
        {
            _ = data;
        }

        public SettingsScreenView(Transform parent, Action onBack = null, Action onDonate = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _onBack = onBack;
            _onDonate = onDonate;
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("SettingsScreen", parent);
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

            _shell = Ui.Panel("Shell", SafeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 0f));

            BuildHeader(_shell);
            BuildTabBar(_shell);
            BuildAudioTab(_shell);
            BuildControlsTab(_shell);
            BuildGraphicsTab(_shell);
            BuildDonateTab(_shell);

            var lifecycle = Root.gameObject.AddComponent<SettingsLifecycle>();
            lifecycle.Owner = this;

            SettingsStore.Current.Changed += OnStoreChanged;
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;

            _safeArea.Apply();
            ApplyRuntimeSettings();
            Refresh();
            SetActiveTab(TabAudio);
            RefreshLayout();
        }

        public static void LoadPersistedSettings()
        {
            if (PlayerPrefs.HasKey(SettingsStore.PlayerPrefsKey))
            {
                string json = PlayerPrefs.GetString(SettingsStore.PlayerPrefsKey, "");
                if (!string.IsNullOrWhiteSpace(json))
                    SettingsStore.Current.LoadJson(json);
            }
            ApplyRuntimeSettings();
        }

        public static void SavePersistedSettings()
        {
            PlayerPrefs.SetString(SettingsStore.PlayerPrefsKey, SettingsStore.Current.ToJson());
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public static void ApplyRuntimeSettings()
        {
            SettingsStore s = SettingsStore.Current;
            Application.targetFrameRate = s.TargetFrameRate;
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
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

        public void SetActiveTab(int tabIndex)
        {
            _activeTab = Mathf.Clamp(tabIndex, 0, _tabPanes.Length - 1);
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                bool active = i == _activeTab;
                if (_tabPanes[i] != null) _tabPanes[i].gameObject.SetActive(active);
                if (_tabPillBgs[i] != null) _tabPillBgs[i].enabled = active;
                if (_tabBars[i] != null) _tabBars[i].color = active ? Gold : TabBarIdle;
                if (_tabPillLabels[i] != null)
                {
                    Ui.SetTextColor(_tabPillLabels[i], active ? MenuArt.GoldLight : MenuArt.MutedText);
                    Ui.ClearOutline(_tabPillLabels[i]);
                }
            }
            UpdateScrollIndicators();
        }

        public void ResetDefaults()
        {
            SettingsStore.Current.ResetToDefaults();
            SavePersistedSettings();
            Refresh();
        }

        public void Refresh()
        {
            _updatingUi = true;
            try
            {
                bool fil = SettingsStore.Current.Language == "fil";
                if (_headerTitle != null) _headerTitle.text = fil ? "MGA SETTING" : "SETTINGS";
                if (_backLabel != null) _backLabel.text = fil ? "BALIK" : "BACK";
                if (_resetLabel != null) _resetLabel.text = fil ? "IBALIK SA DATI" : "RESET DEFAULTS";
                if (_tabPillLabels[0] != null) _tabPillLabels[0].text = fil ? "TUNOG" : "AUDIO";
                if (_tabPillLabels[1] != null) _tabPillLabels[1].text = fil ? "KONTROL" : "CONTROLS";
                if (_tabPillLabels[2] != null) _tabPillLabels[2].text = fil ? "GRAPIKA AT TULONG" : "GRAPHICS & ACCESSIBILITY";

                for (int i = 0; i < _syncCallbacks.Count; i++)
                    _syncCallbacks[i]?.Invoke();
            }
            finally
            {
                _updatingUi = false;
            }
            UpdateScrollIndicators();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SettingsStore.Current.Changed -= OnStoreChanged;
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

        void DisposeFromLifecycle() => Dispose();

        void OnStoreChanged()
        {
            if (_updatingUi) return;
            ApplyRuntimeSettings();
            Refresh();
        }

        void CommitChange(Action mutate)
        {
            if (_updatingUi) return;
            mutate?.Invoke();
            SavePersistedSettings();
            Refresh();
        }

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        public void RefreshLayout()
        {
            if (_disposed || _refreshingLayout || Root == null || _shell == null) return;
            _refreshingLayout = true;
            try
            {
                RefreshLayoutCore();
            }
            finally
            {
                _refreshingLayout = false;
            }
        }

        void RefreshLayoutCore()
        {
            if (_canvas == null) _canvas = Root.GetComponentInParent<Canvas>();

            _safeArea?.Apply();

            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float clampedWidth = Mathf.Clamp(safeSize.x - 24f, 640f, 1260f);
            _shell.sizeDelta = new Vector2(clampedWidth, 0f);

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float minGutter = Mathf.Max(8f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));
            float headerH = Mathf.Max(104f, minTouch + 8f);
            float tabBarH = Mathf.Max(96f, minTouch);

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

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(214f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            if (_resetBtnRect != null)
            {
                _resetBtnRect.anchoredPosition = new Vector2(-10f, 0f);
                _resetBtnRect.sizeDelta = resetSize;
            }

            float titleLeft = 10f + backSize.x + minGutter + 6f;
            if (_headerAccentRect != null)
                _headerAccentRect.anchoredPosition = new Vector2(titleLeft, 0f);

            if (_headerTitleRect != null)
            {
                _headerTitleRect.anchorMin = Vector2.zero;
                _headerTitleRect.anchorMax = Vector2.one;
                _headerTitleRect.offsetMin = new Vector2(titleLeft + 14f, 8f);
                _headerTitleRect.offsetMax = new Vector2(-(10f + resetSize.x + minGutter), -8f);
            }

            float tabBarTop = -(4f + headerH + 4f);
            if (_tabBarRect != null)
            {
                _tabBarRect.anchoredPosition = new Vector2(0f, tabBarTop);
                _tabBarRect.sizeDelta = new Vector2(0f, tabBarH);
            }

            if (_tabBarLayout != null)
                _tabBarLayout.spacing = Mathf.Max(10f, minGutter);

            for (int i = 0; i < _tabPillLayouts.Length; i++)
            {
                LayoutElement le = _tabPillLayouts[i];
                if (le == null) continue;
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
            }

            float paneTopInset = tabBarTop - tabBarH - 6f;
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                if (_tabPanes[i] == null) continue;
                _tabPanes[i].offsetMin = new Vector2(0f, 6f);
                _tabPanes[i].offsetMax = new Vector2(0f, paneTopInset);
            }

            float toggleRowH = Mathf.Max(98f, minTouch + 2f);
            Vector2 toggleBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(164f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            for (int i = 0; i < _toggleRows.Count; i++)
            {
                ToggleRowRefs r = _toggleRows[i];
                r.RowLayout.minHeight = toggleRowH;
                r.RowLayout.preferredHeight = toggleRowH;
                r.ToggleRect.anchoredPosition = new Vector2(-14f, 0f);
                r.ToggleRect.sizeDelta = toggleBtnSize;
                r.TitleRect.anchorMin = Vector2.zero;
                r.TitleRect.anchorMax = Vector2.one;
                r.TitleRect.offsetMin = new Vector2(22f, 6f);
                r.TitleRect.offsetMax = new Vector2(-(14f + toggleBtnSize.x + minGutter), -6f);
            }

            if (_donateRow != null)
            {
                float donateRowH = Mathf.Max(132f, minTouch + 36f);
                Vector2 donateBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(196f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                _donateRow.RowLayout.minHeight = donateRowH;
                _donateRow.RowLayout.preferredHeight = donateRowH;
                _donateRow.ButtonRect.anchoredPosition = new Vector2(-16f, 0f);
                _donateRow.ButtonRect.sizeDelta = donateBtnSize;
                _donateRow.CopyRect.anchorMin = Vector2.zero;
                _donateRow.CopyRect.anchorMax = Vector2.one;
                _donateRow.CopyRect.offsetMin = new Vector2(86f, 12f);
                _donateRow.CopyRect.offsetMax = new Vector2(-(16f + donateBtnSize.x + minGutter), -12f);
            }

            float sliderRowH = Mathf.Max(102f, minTouch + 6f);
            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;
            float pctWidth = 86f;
            float rightPad = 14f;
            float plusRightEdge = -(rightPad + pctWidth + 10f);
            float sliderRightEdge = plusRightEdge - btnSide - minGutter;
            float titleMaxShare = clampedWidth < 1020f ? 0.31f : 0.35f;

            for (int i = 0; i < _sliderRows.Count; i++)
            {
                SliderRowRefs s = _sliderRows[i];
                s.RowLayout.minHeight = sliderRowH;
                s.RowLayout.preferredHeight = sliderRowH;

                s.TitleRect.anchorMin = new Vector2(0f, 0f);
                s.TitleRect.anchorMax = new Vector2(titleMaxShare, 1f);
                s.TitleRect.offsetMin = new Vector2(22f, 6f);
                s.TitleRect.offsetMax = new Vector2(-8f, -6f);

                s.MinusRect.anchorMin = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.anchorMax = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.pivot = new Vector2(0f, 0.5f);
                s.MinusRect.anchoredPosition = new Vector2(6f, 0f);
                s.MinusRect.sizeDelta = new Vector2(btnSide, btnSide);

                float sliderPadY = Mathf.Max(3f, (sliderRowH - minTouch) * 0.5f);
                s.SliderRect.anchorMin = new Vector2(titleMaxShare, 0f);
                s.SliderRect.anchorMax = new Vector2(1f, 1f);
                s.SliderRect.offsetMin = new Vector2(6f + btnSide + minGutter, sliderPadY);
                s.SliderRect.offsetMax = new Vector2(sliderRightEdge, -sliderPadY);

                s.TrackRect.anchorMin = new Vector2(0f, 0.5f);
                s.TrackRect.anchorMax = new Vector2(1f, 0.5f);
                s.TrackRect.offsetMin = new Vector2(halfKnob, -5f);
                s.TrackRect.offsetMax = new Vector2(-halfKnob, 5f);

                s.FillAreaRect.anchorMin = new Vector2(0f, 0.5f);
                s.FillAreaRect.anchorMax = new Vector2(1f, 0.5f);
                s.FillAreaRect.offsetMin = new Vector2(halfKnob, -5f);
                s.FillAreaRect.offsetMax = new Vector2(-halfKnob, 5f);

                s.HandleSlideRect.anchorMin = new Vector2(0f, 0.5f);
                s.HandleSlideRect.anchorMax = new Vector2(1f, 0.5f);
                s.HandleSlideRect.offsetMin = new Vector2(halfKnob, -halfKnob);
                s.HandleSlideRect.offsetMax = new Vector2(-halfKnob, halfKnob);
                s.HandleRect.sizeDelta = new Vector2(knobPx, 0f);

                s.PlusRect.anchorMin = new Vector2(1f, 0.5f);
                s.PlusRect.anchorMax = new Vector2(1f, 0.5f);
                s.PlusRect.pivot = new Vector2(1f, 0.5f);
                s.PlusRect.anchoredPosition = new Vector2(plusRightEdge, 0f);
                s.PlusRect.sizeDelta = new Vector2(btnSide, btnSide);

                s.PercentRect.anchorMin = new Vector2(1f, 0.5f);
                s.PercentRect.anchorMax = new Vector2(1f, 0.5f);
                s.PercentRect.pivot = new Vector2(1f, 0.5f);
                s.PercentRect.anchoredPosition = new Vector2(-rightPad, 0f);
                s.PercentRect.sizeDelta = new Vector2(pctWidth, 42f);
            }

            float segRowH = Mathf.Max(100f, minTouch + 4f);
            float segTitleShare = clampedWidth < 1020f ? 0.31f : 0.36f;
            float segPadY = Mathf.Max(2f, (segRowH - minTouch) * 0.5f);
            for (int i = 0; i < _segmentedRows.Count; i++)
            {
                SegmentedRowRefs sr = _segmentedRows[i];
                sr.RowLayout.minHeight = segRowH;
                sr.RowLayout.preferredHeight = segRowH;
                sr.TitleRect.anchorMin = new Vector2(0f, 0f);
                sr.TitleRect.anchorMax = new Vector2(segTitleShare, 1f);
                sr.TitleRect.offsetMin = new Vector2(22f, 6f);
                sr.TitleRect.offsetMax = new Vector2(-8f, -6f);

                sr.GroupRect.anchorMin = new Vector2(segTitleShare, 0f);
                sr.GroupRect.anchorMax = new Vector2(1f, 1f);
                sr.GroupRect.offsetMin = new Vector2(8f, segPadY);
                sr.GroupRect.offsetMax = new Vector2(-14f, -segPadY);
                sr.GroupLayout.spacing = Mathf.Max(8f, minGutter * 0.5f);

                for (int c = 0; c < sr.ChoiceLayouts.Length; c++)
                {
                    sr.ChoiceLayouts[c].minWidth = minTouch;
                    sr.ChoiceLayouts[c].minHeight = minTouch;
                    sr.ChoiceLayouts[c].preferredHeight = minTouch;
                }
            }

            UpdateScrollIndicators();
        }

        void UpdateScrollIndicators()
        {
            for (int i = 0; i < _scrollPanes.Count; i++)
            {
                ScrollPaneRefs sp = _scrollPanes[i];
                if (sp.Scroll == null || sp.Scroll.viewport == null || sp.Scroll.content == null || sp.TrackRect == null || sp.ThumbRect == null)
                    continue;

                if (sp.Scroll.content.gameObject.activeInHierarchy)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(sp.Scroll.content);

                float viewH = sp.Scroll.viewport.rect.height;
                float contentH = sp.Scroll.content.rect.height;
                bool canScroll = contentH > viewH + 4f && viewH > 10f;
                sp.TrackRect.gameObject.SetActive(canScroll);
                if (!canScroll) continue;

                float ratio = Mathf.Clamp(viewH / contentH, 0.18f, 1f);
                float norm = Mathf.Clamp01(sp.Scroll.verticalNormalizedPosition);
                float top = Mathf.Lerp(1f - ratio, 1f, norm);
                float bottom = top - ratio;
                sp.ThumbRect.anchorMin = new Vector2(0f, bottom);
                sp.ThumbRect.anchorMax = new Vector2(1f, top);
                sp.ThumbRect.offsetMin = Vector2.zero;
                sp.ThumbRect.offsetMax = Vector2.zero;
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
                raw.color = new Color(1f, 1f, 1f, 0.32f);
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
            _backBtnRect = MakeHeaderButton("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize, isReset: false, () =>
            {
                SavePersistedSettings();
                _onBack?.Invoke();
            }, out _backLabel, "BACK");

            _headerAccentRect = Ui.Panel("TitleAccent", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(166f, 0f), new Vector2(4f, 34f));
            Image accentImg = _headerAccentRect.gameObject.AddComponent<Image>();
            accentImg.sprite = MenuArt.AccentRail(2);
            accentImg.type = Image.Type.Sliced;
            accentImg.color = Gold;
            accentImg.raycastTarget = false;

            _headerTitle = MakeFittedText("Title", _headerRect, "SETTINGS", 32, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, Vector2.zero, Vector2.one, new Vector2(180f, 8f), new Vector2(-238f, -8f));

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(214f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _resetBtnRect = MakeHeaderButton("ResetDefaultsButton", _headerRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), resetSize, isReset: true, ResetDefaults, out _resetLabel, "RESET DEFAULTS");
        }

        void BuildTabBar(RectTransform parent)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(104f, minTouch + 8f);
            float tabBarH = Mathf.Max(96f, minTouch);

            _tabBarRect = Ui.Panel("TabBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(4f + headerH + 4f)), new Vector2(0f, tabBarH));
            _tabBarLayout = _tabBarRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            _tabBarLayout.padding = new RectOffset(0, 0, 0, 0);
            _tabBarLayout.spacing = 12f;
            _tabBarLayout.childControlWidth = true;
            _tabBarLayout.childControlHeight = true;
            _tabBarLayout.childForceExpandWidth = true;
            _tabBarLayout.childForceExpandHeight = true;

            Sprite activePlate = MenuArt.ChamferCard(TabActiveTop, TabActiveBottom, MenuArt.PanelBorder, 10, 1, 0.24f);
            string[] names = { "AUDIO", "CONTROLS", "GRAPHICS & ACCESSIBILITY", "DONATE" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var go = new GameObject($"SettingsTab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_tabBarRect, false);
                var hitImg = go.GetComponent<Image>();
                hitImg.color = Color.clear; // transparent >= 48 dp hit target

                RectTransform plateRt = Ui.Panel("Visual", go.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
                Image visualBg = Ui.MakeSliced("ActivePlate", plateRt, activePlate, Color.white, raycast: false);
                visualBg.enabled = false;

                Image bar = Ui.MakeSliced("Bar", plateRt, MenuArt.AccentRail(2), TabBarIdle, raycast: false);
                Stretch(bar.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.94f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f));
                _tabBars[i] = bar;

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
                le.preferredWidth = i == 2 ? 320f : i == 3 ? 180f : 220f;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = hitImg;
                btn.onClick.AddListener(() => SetActiveTab(idx));
                Ui.AddPressFeel(go, 0.97f);

                Text lbl = MakeFittedText("Label", plateRt, names[i], 20, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -4f));

                _tabPillBgs[i] = visualBg;
                _tabPillLabels[i] = lbl;
                _tabPillLayouts[i] = le;
            }
        }

        void BuildAudioTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("AudioPane", parent, out RectTransform paneRoot);
            _tabPanes[TabAudio] = paneRoot;

            AddSliderRow(
                content,
                "MasterVolumeRow",
                () => SettingsStore.Current.Language == "fil" ? "Pangkalahatang Lakas (Master)" : "Master Volume",
                () => SettingsStore.Current.MasterVolume,
                v => CommitChange(() => SettingsStore.Current.MasterVolume = v));

            AddToggleRow(
                content,
                "MusicEnabledRow",
                () => SettingsStore.Current.Language == "fil" ? "Musika sa Kalye (Music)" : "Background Music",
                () => SettingsStore.Current.MusicEnabled,
                on => CommitChange(() => SettingsStore.Current.MusicEnabled = on));

            AddSliderRow(
                content,
                "MusicVolumeRow",
                () => SettingsStore.Current.Language == "fil" ? "Lakas ng Musika" : "Music Volume",
                () => SettingsStore.Current.MusicVolume,
                v => CommitChange(() => SettingsStore.Current.MusicVolume = v));

            AddToggleRow(
                content,
                "SoundsEnabledRow",
                () => SettingsStore.Current.Language == "fil" ? "Tunog ng Laro (SFX)" : "Sound Effects (SFX)",
                () => SettingsStore.Current.SoundsEnabled,
                on => CommitChange(() => SettingsStore.Current.SoundsEnabled = on));

            AddSliderRow(
                content,
                "SfxVolumeRow",
                () => SettingsStore.Current.Language == "fil" ? "Lakas ng SFX" : "SFX Volume",
                () => SettingsStore.Current.SfxVolume,
                v => CommitChange(() => SettingsStore.Current.SfxVolume = v));

            AddToggleRow(
                content,
                "HapticsRow",
                () => SettingsStore.Current.Language == "fil" ? "Yugyog sa Daliri (Haptics)" : "Touch Haptics",
                () => SettingsStore.Current.Haptics,
                on => CommitChange(() => SettingsStore.Current.Haptics = on));
        }

        void BuildControlsTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("ControlsPane", parent, out RectTransform paneRoot);
            _tabPanes[TabControls] = paneRoot;

            AddToggleRow(
                content,
                "LeftHandedRow",
                () => SettingsStore.Current.Language == "fil" ? "Kaliwete (Swap Stick at Tirador)" : "Left-Handed Thumb Swap",
                () => SettingsStore.Current.LeftHanded,
                on => CommitChange(() => SettingsStore.Current.LeftHanded = on));

            AddToggleRow(
                content,
                "TiltToRunRow",
                () => SettingsStore.Current.Language == "fil" ? "Ikiling para Tumakbo (Tilt)" : "Tilt Phone to Run",
                () => SettingsStore.Current.TiltToRun,
                on => CommitChange(() => SettingsStore.Current.TiltToRun = on));

            AddToggleRow(
                content,
                "AimGuideRow",
                () => SettingsStore.Current.Language == "fil" ? "Gabay sa Asinta (Aim Guide)" : "World Aim Guide",
                () => SettingsStore.Current.AimGuide,
                on => CommitChange(() => SettingsStore.Current.AimGuide = on));

            AddToggleRow(
                content,
                "AdsToggleRow",
                () => SettingsStore.Current.Language == "fil" ? "Pindot para Asinta (ADS Tap)" : "Tap to Aim Down Sight (off: hold)",
                () => SettingsStore.Current.AdsToggle,
                on => CommitChange(() => SettingsStore.Current.AdsToggle = on));

            AddSegmentedRow(
                content,
                "CardsTrayRow",
                () => SettingsStore.Current.Language == "fil" ? "Ayos ng Superpowers" : "Ability Layout (Cards Tray)",
                new[] { "Radial Arc", "4-Card Tray" },
                null,
                () => SettingsStore.Current.CardsTrayMode ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.CardsTrayMode = idx == 1));

            AddSegmentedRow(
                content,
                "CardsPlacementRow",
                () => SettingsStore.Current.Language == "fil" ? "Pwesto ng Card Tray" : "Card Tray Placement",
                new[] { "Bottom", "Sides" },
                null,
                () => SettingsStore.Current.CardsTrayPlacement == "sides" ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.CardsTrayPlacement = idx == 1 ? "sides" : "bottom"));

            AddSegmentedRow(
                content,
                "ControlScaleRow",
                () => SettingsStore.Current.Language == "fil" ? "Laki ng Pindutan (Control Scale)" : "Button Control Scale",
                new[] { "85% Small", "100% Normal", "120% Large" },
                null,
                () =>
                {
                    float s = SettingsStore.Current.ControlScale;
                    if (s <= 0.90f) return 0;
                    if (s >= 1.10f) return 2;
                    return 1;
                },
                idx =>
                {
                    float next = idx == 0 ? SettingsStore.ScaleSmall : idx == 2 ? SettingsStore.ScaleLarge : SettingsStore.ScaleNormal;
                    CommitChange(() => SettingsStore.Current.ControlScale = next);
                });
        }

        void BuildGraphicsTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("GraphicsPane", parent, out RectTransform paneRoot);
            _tabPanes[TabGraphics] = paneRoot;

            AddSegmentedRow(
                content,
                "FpsRow",
                () => SettingsStore.Current.Language == "fil" ? "Bilis ng Frame (FPS Cap)" : "Target Frame Rate",
                new[] { "30 FPS Saver", "60 FPS Smooth" },
                null,
                () => SettingsStore.Current.TargetFrameRate <= 30 ? 0 : 1,
                idx => CommitChange(() => SettingsStore.Current.TargetFrameRate = idx == 0 ? 30 : 60));

            AddSegmentedRow(
                content,
                "GraphicsQualityRow",
                () => SettingsStore.Current.Language == "fil" ? "Kalidad ng Grapika" : "Graphics Quality",
                new[] { "Low", "Medium", "High", "Ultra" },
                new[] { "Mababa", "Katamtaman", "Mataas", "Ultra" },
                () => Mathf.Clamp(SettingsStore.Current.GraphicsQuality, 0, 3),
                idx => CommitChange(() => SettingsStore.Current.GraphicsQuality = idx));

            AddToggleRow(
                content,
                "ReduceMotionRow",
                () => SettingsStore.Current.Language == "fil" ? "Bawasan ang Galaw (Reduce Motion)" : "Reduce Motion",
                () => SettingsStore.Current.ReduceMotion,
                on => CommitChange(() => SettingsStore.Current.ReduceMotion = on));

            AddToggleRow(
                content,
                "ScreenShakeRow",
                () => SettingsStore.Current.Language == "fil" ? "Yugyog ng Kamera (Screen Shake)" : "Camera Screen Shake",
                () => SettingsStore.Current.ScreenShake,
                on => CommitChange(() => SettingsStore.Current.ScreenShake = on));

            AddSegmentedRow(
                content,
                "ColorblindRow",
                () => SettingsStore.Current.Language == "fil" ? "Kulay para sa Colorblind" : "Colorblind Palette",
                new[] { "Off", "Protan", "Deuter", "Tritan" },
                new[] { "Wala", "Protan", "Deuter", "Tritan" },
                () => Mathf.Clamp(SettingsStore.Current.ColorblindMode, 0, 3),
                idx => CommitChange(() => SettingsStore.Current.ColorblindMode = idx));

            AddSegmentedRow(
                content,
                "LanguageRow",
                () => SettingsStore.Current.Language == "fil" ? "Wika (Language)" : "Language / Wika",
                new[] { "EN · English", "FIL · Filipino" },
                null,
                () => SettingsStore.Current.Language == "fil" ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.Language = idx == 1 ? "fil" : "en"));
        }

        RectTransform CreateScrollableTabPane(string name, RectTransform parent, out RectTransform paneRoot)
        {
            paneRoot = Ui.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            paneRoot.offsetMin = new Vector2(0f, 6f);
            paneRoot.offsetMax = new Vector2(0f, -214f);

            Ui.MakeSliced("PaneBg", paneRoot, MenuArt.ChamferGlass(18), Color.white, raycast: false);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(paneRoot, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-20f, -8f));
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var v = contentGo.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(6, 6, 4, 8);
            v.spacing = 8f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollRt;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            // Sleek thin non-interactive visual scroll indicator (zero sub-48dp Selectable components)
            Image trackImg = Ui.MakeSliced("ScrollTrack", paneRoot, MenuArt.AccentRail(2), new Color(0.76f, 0.88f, 0.96f, 0.14f), raycast: false);
            Stretch(trackImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-12f, 14f), new Vector2(-8f, -14f));
            Image thumbImg = Ui.MakeSliced("ScrollThumb", trackImg.transform, MenuArt.AccentRail(2), Gold, raycast: false);
            Stretch(thumbImg.rectTransform, new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero);

            var refs = new ScrollPaneRefs
            {
                Scroll = scroll,
                TrackRect = trackImg.rectTransform,
                ThumbRect = thumbImg.rectTransform,
            };
            _scrollPanes.Add(refs);
            scroll.onValueChanged.AddListener(_ => UpdateScrollIndicators());
            return content;
        }

        /// <summary>Support Kalyeah: one tactical support card whose DONATE CTA opens the donate sheet (grown-up check first; gifts give nothing).</summary>
        void BuildDonateTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("DonatePane", parent, out RectTransform paneRoot);
            _tabPanes[TabDonate] = paneRoot;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(132f, minTouch + 36f);
            RectTransform row = CreateRowContainer(content, "DonateRow", rowH, out LayoutElement rowLe, out Text titleText, railColor: Gold);
            Ui.MakeCornerBrackets("DonateBrackets", row, new Color(Gold.r, Gold.g, Gold.b, 0.42f));

            // Heart emblem medallion on the left
            RectTransform iconWell = Ui.Panel("IconWell", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(50f, 50f));
            Ui.MakeSliced("WellBg", iconWell, MenuArt.StatChip(10), Color.white, raycast: false);
            Image heartGlyph = Ui.MakeImage("HeartGlyph", iconWell, Gold, round: false, raycast: false);
            heartGlyph.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
            heartGlyph.rectTransform.anchorMin = heartGlyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.sizeDelta = new Vector2(24f, 24f);

            // Title + honest subtitle stack
            RectTransform copyStack = Ui.Panel("CopyStack", row, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            copyStack.offsetMin = new Vector2(86f, 12f);
            copyStack.offsetMax = new Vector2(-228f, -12f);
            titleText.transform.SetParent(copyStack, false);
            titleText.font = KalyeahResources.DisplayFont();
            titleText.fontSize = 24;
            titleText.resizeTextMaxSize = 24;
            titleText.alignment = TextAnchor.LowerLeft;
            Stretch(titleText.rectTransform, new Vector2(0f, 0.48f), Vector2.one, Vector2.zero, Vector2.zero);

            Text subText = MakeFittedText("Subtitle", copyStack, "Optional community tip · unlocks zero gameplay power", 17, MenuArt.MutedText, TextAnchor.UpperLeft);
            subText.resizeTextMinSize = 12;
            subText.resizeTextMaxSize = 17;
            Ui.ClearOutline(subText);
            Stretch(subText.rectTransform, Vector2.zero, new Vector2(1f, 0.44f), Vector2.zero, Vector2.zero);

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(196f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform buttonRt = Ui.Panel("DonateBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), btnSize);
            Image hitImg = buttonRt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform visualRt = Ui.Panel("Visual", buttonRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 54f));
            Image visualBg = Ui.MakeSliced("Face", visualRt, MenuArt.CtaGold(12), Color.white, raycast: false);
            Ui.AddSheen(visualRt);

            DonateButton = buttonRt.gameObject.AddComponent<Button>();
            DonateButton.targetGraphic = visualBg;
            DonateButton.onClick.AddListener(() => _onDonate?.Invoke());
            Ui.AddPressFeel(buttonRt, 0.95f);

            Text label = MakeFittedText("Label", visualRt, "DONATE", 22, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Ui.ClearOutline(label);

            _donateRow = new DonateRowRefs { RowLayout = rowLe, CopyRect = copyStack, ButtonRect = buttonRt };
            _syncCallbacks.Add(() =>
            {
                bool fil = SettingsStore.Current.Language == "fil";
                titleText.text = fil
                    ? "Suportahan ang Kalyeah (magtanong muna sa nakatatanda)"
                    : "Support Kalyeah (ask a grown-up first)";
                subText.text = fil
                    ? "Opsyonal na suporta · walang dagdag na lakas sa laro"
                    : "Optional community tip · unlocks zero gameplay power";
            });
        }

        /// <summary>The DONATE button on the Donate tab.</summary>
        public Button DonateButton { get; private set; }

        void AddToggleRow(RectTransform parent, string name, Func<string> titleGetter, Func<bool> getter, Action<bool> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(98f, minTouch + 2f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(164f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform toggleRt = Ui.Panel("ToggleBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), btnSize);
            Image hitImg = toggleRt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform switchVisual = Ui.MakeToggleVisual(
                "SwitchVisual",
                toggleRt,
                out Image trackImg,
                out RectTransform thumbRect,
                out Image _,
                out Text stateLbl,
                trackWidth: 112f,
                trackHeight: 44f);
            switchVisual.anchorMin = new Vector2(1f, 0.5f);
            switchVisual.anchorMax = new Vector2(1f, 0.5f);
            switchVisual.pivot = new Vector2(1f, 0.5f);
            switchVisual.anchoredPosition = new Vector2(-4f, 0f);
            stateLbl.resizeTextForBestFit = true;
            stateLbl.resizeTextMinSize = 11;
            stateLbl.resizeTextMaxSize = 16;
            stateLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            stateLbl.verticalOverflow = VerticalWrapMode.Overflow;

            Button btn = toggleRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = trackImg;
            btn.onClick.AddListener(() => setter(!getter()));
            Ui.AddPressFeel(toggleRt, 0.95f);

            _toggleRows.Add(new ToggleRowRefs
            {
                RowLayout = rowLe,
                TitleRect = titleText.rectTransform,
                ToggleRect = toggleRt,
            });

            _syncCallbacks.Add(() =>
            {
                titleText.text = titleGetter();
                bool on = getter();
                bool fil = SettingsStore.Current.Language == "fil";
                Ui.ApplyToggleVisual(on, trackImg, thumbRect, stateLbl, fil ? "BUKAS" : "ON", fil ? "PATAY" : "OFF");
            });
        }

        void AddSliderRow(RectTransform parent, string name, Func<string> titleGetter, Func<float> getter, Action<float> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(102f, minTouch + 6f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.35f, 1f);

            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;
            Sprite stepperSprite = MenuArt.Round(StepperTop, StepperBottom, StepperBorder, 25, 2, 0.30f);

            // Sleek circular beveled Minus stepper inside >= 48 dp transparent hit catcher
            RectTransform minusRt = MakeStepperButton(
                "MinusBtn",
                row,
                new Vector2(0.35f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(6f, 0f),
                btnSide,
                stepperSprite,
                "-",
                () => setter(Mathf.Clamp01(getter() - 0.10f)));

            // UGUI Slider (height >= 48 dp; HandleSlideArea fixed knobPx tall & inset by halfKnob so circular 48 dp Handle never stretches or escapes Slider bounds)
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            Stretch(sliderRt, new Vector2(0.35f, 0f), new Vector2(1f, 1f), new Vector2(6f + btnSide + 14f, 4f), new Vector2(-(110f + btnSide + 14f), -4f));

            Sprite trackSprite = MenuArt.Round(TrackTop, TrackBottom, TrackBorder, 5, 1, 0.10f);
            Image track = Ui.MakeSliced("Track", sliderRt, trackSprite, Color.white, raycast: false);
            Stretch(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(halfKnob, -5f), new Vector2(-halfKnob, 5f));

            RectTransform fillArea = Ui.Panel("FillArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(halfKnob, -5f);
            fillArea.offsetMax = new Vector2(-halfKnob, 5f);
            Image fill = Ui.MakeSliced("Fill", fillArea, MenuArt.AccentRail(4), Gold, raycast: false);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform handleSlide = Ui.Panel("HandleSlideArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            handleSlide.offsetMin = new Vector2(halfKnob, -halfKnob);
            handleSlide.offsetMax = new Vector2(-halfKnob, halfKnob);

            // Handle keeps the full 48 dp transparent touch area; glowing 3D knob + gold core pip centered inside
            Image handle = Ui.MakeImage("Handle", handleSlide, Color.clear, round: true, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(knobPx, 0f);

            Image knobGlow = Ui.MakeImage("KnobGlow", handle.transform, new Color(1f, 0.78f, 0.28f, 0.40f), round: false, raycast: false);
            knobGlow.sprite = MenuArt.Glow();
            knobGlow.rectTransform.anchorMin = knobGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knobGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knobGlow.rectTransform.sizeDelta = new Vector2(46f, 46f);

            Image handleFace = Ui.MakeImage("KnobFace", handle.transform, Color.white, round: false, raycast: false);
            handleFace.sprite = MenuArt.ToggleThumb();
            handleFace.rectTransform.anchorMin = handleFace.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handleFace.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handleFace.rectTransform.sizeDelta = new Vector2(30f, 30f);

            Image handlePip = Ui.MakeImage("KnobPip", handleFace.transform, Gold, round: true, raycast: false);
            handlePip.rectTransform.anchorMin = handlePip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handlePip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handlePip.rectTransform.sizeDelta = new Vector2(10f, 10f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handleFace;

            // Sleek circular beveled Plus stepper inside >= 48 dp transparent hit catcher
            RectTransform plusRt = MakeStepperButton(
                "PlusBtn",
                row,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-110f, 0f),
                btnSide,
                stepperSprite,
                "+",
                () => setter(Mathf.Clamp01(getter() + 0.10f)));

            // Framed gold % readout chip
            RectTransform pctChip = Ui.MakeStatChip("PercentChip", row, "100%", MenuArt.GoldLight, out Text pctLabel, fontSize: 19, font: KalyeahResources.DisplayFont());
            pctChip.anchorMin = new Vector2(1f, 0.5f);
            pctChip.anchorMax = new Vector2(1f, 0.5f);
            pctChip.pivot = new Vector2(1f, 0.5f);
            pctChip.anchoredPosition = new Vector2(-14f, 0f);
            pctChip.sizeDelta = new Vector2(86f, 42f);
            pctLabel.resizeTextForBestFit = true;
            pctLabel.resizeTextMinSize = 12;
            pctLabel.resizeTextMaxSize = 19;
            pctLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            pctLabel.verticalOverflow = VerticalWrapMode.Overflow;

            _sliderRows.Add(new SliderRowRefs
            {
                RowLayout = rowLe,
                TitleRect = titleText.rectTransform,
                MinusRect = minusRt,
                SliderRect = sliderRt,
                TrackRect = track.rectTransform,
                FillAreaRect = fillArea,
                HandleSlideRect = handleSlide,
                HandleRect = handle.rectTransform,
                PlusRect = plusRt,
                PercentRect = pctChip,
            });

            slider.onValueChanged.AddListener(v =>
            {
                if (_updatingUi) return;
                setter(v);
            });

            _syncCallbacks.Add(() =>
            {
                titleText.text = titleGetter();
                float val = Mathf.Clamp01(getter());
                slider.SetValueWithoutNotify(val);
                pctLabel.text = Mathf.RoundToInt(val * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            });
        }

        static RectTransform MakeStepperButton(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            float hitSide,
            Sprite visualSprite,
            string glyph,
            Action onClick)
        {
            RectTransform rt = Ui.Panel(name, parent, anchor, anchor, pivot, pos, new Vector2(hitSide, hitSide));
            Image hitImg = rt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform faceRt = Ui.Panel("Face", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            Image faceImg = Ui.MakeSliced("Disc", faceRt, visualSprite, Color.white, raycast: false);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.onClick.AddListener(() => onClick?.Invoke());
            Ui.AddPressFeel(rt, 0.93f);

            Text txt = MakeFittedText("Label", faceRt, glyph, 26, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        void AddSegmentedRow(
            RectTransform parent,
            string name,
            Func<string> titleGetter,
            string[] optionsEn,
            string[] optionsFil,
            Func<int> getter,
            Action<int> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(100f, minTouch + 4f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.36f, 1f);

            RectTransform group = Ui.Panel("Choices", row, new Vector2(0.36f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            group.offsetMin = new Vector2(8f, 2f);
            group.offsetMax = new Vector2(-14f, -2f);
            var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            Sprite activeSprite = MenuArt.CtaGold(10);
            Sprite idleSprite = MenuArt.CtaGhost(10);
            var bgs = new Image[optionsEn.Length];
            var labels = new Text[optionsEn.Length];
            var choiceLayouts = new LayoutElement[optionsEn.Length];

            for (int i = 0; i < optionsEn.Length; i++)
            {
                int idx = i;
                var optGo = new GameObject($"Choice_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                optGo.transform.SetParent(group, false);
                var hitImg = optGo.GetComponent<Image>();
                hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

                RectTransform pillRt = Ui.Panel("Visual", optGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
                Image visualImg = Ui.MakeSliced("Plate", pillRt, idleSprite, Color.white, raycast: false);

                var cle = optGo.GetComponent<LayoutElement>();
                cle.minWidth = minTouch;
                cle.minHeight = minTouch;
                cle.preferredHeight = minTouch;

                var btn = optGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.onClick.AddListener(() => setter(idx));
                Ui.AddPressFeel(optGo, 0.96f);

                Text lbl = MakeFittedText("Label", pillRt, optionsEn[i], 18, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

                bgs[i] = visualImg;
                labels[i] = lbl;
                choiceLayouts[i] = cle;
            }

            _segmentedRows.Add(new SegmentedRowRefs
            {
                RowLayout = rowLe,
                TitleRect = titleText.rectTransform,
                GroupRect = group,
                GroupLayout = h,
                ChoiceLayouts = choiceLayouts,
            });

            _syncCallbacks.Add(() =>
            {
                bool fil = SettingsStore.Current.Language == "fil";
                titleText.text = titleGetter();
                int selected = Mathf.Clamp(getter(), 0, optionsEn.Length - 1);
                for (int i = 0; i < optionsEn.Length; i++)
                {
                    bool active = i == selected;
                    if (optionsFil != null && i < optionsFil.Length)
                        labels[i].text = fil ? optionsFil[i] : optionsEn[i];
                    bgs[i].sprite = active ? activeSprite : idleSprite;
                    bgs[i].color = Color.white;
                    Ui.SetTextColor(labels[i], active ? Ink : MenuArt.Cream);
                }
            });
        }

        static RectTransform CreateRowContainer(RectTransform parent, string name, float rowH, out LayoutElement le, out Text titleText, Color? railColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = MenuArt.ChamferCard(RowTop, RowBottom, RowBorder, 10, 1, 0.20f);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;

            le = go.GetComponent<LayoutElement>();
            le.minHeight = rowH;
            le.preferredHeight = rowH;

            var rt = go.GetComponent<RectTransform>();
            Ui.MakeAccentRail("RowRail", rt, railColor ?? RowRailIdle, width: 3f, verticalInset: 16f);

            titleText = MakeFittedText("Title", rt, "", 20, MenuArt.Cream, TextAnchor.MiddleLeft);
            Stretch(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(22f, 6f), new Vector2(-8f, -6f));
            return rt;
        }

        static RectTransform MakeHeaderButton(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            bool isReset,
            Action onClick,
            out Text label,
            string text)
        {
            RectTransform rt = Ui.Panel(name, parent, anchor, anchor, pivot, pos, size);
            Image hitImg = rt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher

            RectTransform visualRt = Ui.Panel("Visual", rt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
            Sprite plateSprite = isReset
                ? MenuArt.ChamferCard(ResetTop, ResetBottom, ResetBorder, 12, 2, 0.22f)
                : MenuArt.CtaGhost(12);
            Image visualBg = Ui.MakeSliced("Plate", visualRt, plateSprite, Color.white, raycast: false);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = visualBg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            Ui.AddPressFeel(rt, 0.95f);

            float textLeft = 10f;
            if (!isReset)
            {
                Image chev = Ui.MakeImage("Chevron", visualRt, MenuArt.GoldLight, round: false, raycast: false);
                chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
                RectTransform cr = chev.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = new Vector2(22f, 0f);
                cr.sizeDelta = new Vector2(16f, 16f);
                cr.localEulerAngles = new Vector3(0f, 0f, 180f);
                textLeft = 34f;
            }

            label = MakeFittedText("Label", visualRt, text, isReset ? 18 : 20, isReset ? ResetText : MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(textLeft, 4f), new Vector2(-10f, -4f));
            return rt;
        }

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = Mathf.Max(18, maxSize);
            return t;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
