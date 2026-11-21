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

        // the home's palette: deep navy, dark glass panels and one gold (docs/SPEC.md §10, the plan page v10)
        static readonly Color BackdropSolid = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color BackdropWash = new Color(0.02f, 0.035f, 0.07f, 0.72f);
        static readonly Color HeaderBg = new Color(0f, 0f, 0f, 0f);
        static readonly Color PanelBg = new Color(0.06f, 0.10f, 0.19f, 0.86f);
        static readonly Color RowBg = new Color(0.10f, 0.15f, 0.25f, 0.92f); // opaque navy: white at low alpha blends too bright in linear colour space
        static readonly Color ChoiceIdleBg = new Color(0.02f, 0.04f, 0.09f, 0.7f);
        static readonly Color Gold = new Color(0.961f, 0.725f, 0.227f, 1f);
        static readonly Color Coral = new Color(0.16f, 0.22f, 0.36f, 1f); // the back button: a light navy chip, not a red slab
        static readonly Color Teal = new Color(0.961f, 0.725f, 0.227f, 1f); // ON toggles and fills are gold, as on the plan page
        static readonly Color Ink = new Color(0.08f, 0.10f, 0.16f, 1f);

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

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _resetBtnRect;
        RectTransform _headerTitleRect;
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
                // underlined tabs, as on the kid page: the active one is cream over a gold bar
                if (_tabBars[i] != null) _tabBars[i].color = active ? Gold : new Color(1f, 1f, 1f, 0.18f);
                if (_tabPillLabels[i] != null)
                {
                    _tabPillLabels[i].color = active ? Ui.Cream : new Color(0.72f, 0.77f, 0.88f, 1f);
                    Outline outline = _tabPillLabels[i].GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
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
            float headerH = Mathf.Max(108f, minTouch + 12f);
            float tabBarH = Mathf.Max(100f, minTouch + 4f);

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

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(204f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            if (_resetBtnRect != null)
            {
                _resetBtnRect.anchoredPosition = new Vector2(-10f, 0f);
                _resetBtnRect.sizeDelta = resetSize;
            }

            if (_headerTitleRect != null)
            {
                _headerTitleRect.anchorMin = Vector2.zero;
                _headerTitleRect.anchorMax = Vector2.one;
                _headerTitleRect.offsetMin = new Vector2(10f + backSize.x + minGutter, 8f);
                _headerTitleRect.offsetMax = new Vector2(-(10f + resetSize.x + minGutter), -8f);
            }

            float tabBarTop = -(6f + headerH + 6f);
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

            float paneTopInset = tabBarTop - tabBarH - 8f;
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                if (_tabPanes[i] == null) continue;
                _tabPanes[i].offsetMin = new Vector2(0f, 6f);
                _tabPanes[i].offsetMax = new Vector2(0f, paneTopInset);
            }

            float toggleRowH = Mathf.Max(100f, minTouch + 4f);
            Vector2 toggleBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(184f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            for (int i = 0; i < _toggleRows.Count; i++)
            {
                ToggleRowRefs r = _toggleRows[i];
                r.RowLayout.minHeight = toggleRowH;
                r.RowLayout.preferredHeight = toggleRowH;
                r.ToggleRect.anchoredPosition = new Vector2(-14f, 0f);
                r.ToggleRect.sizeDelta = toggleBtnSize;
                r.TitleRect.anchorMin = Vector2.zero;
                r.TitleRect.anchorMax = Vector2.one;
                r.TitleRect.offsetMin = new Vector2(16f, 6f);
                r.TitleRect.offsetMax = new Vector2(-(14f + toggleBtnSize.x + minGutter), -6f);
            }

            float sliderRowH = Mathf.Max(108f, minTouch + 12f);
            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;
            float pctWidth = 82f;
            float rightPad = 12f;
            float plusRightEdge = -(rightPad + pctWidth + 10f);
            float sliderRightEdge = plusRightEdge - btnSide - minGutter;
            float titleMaxShare = clampedWidth < 1020f ? 0.33f : 0.37f;

            for (int i = 0; i < _sliderRows.Count; i++)
            {
                SliderRowRefs s = _sliderRows[i];
                s.RowLayout.minHeight = sliderRowH;
                s.RowLayout.preferredHeight = sliderRowH;

                s.TitleRect.anchorMin = new Vector2(0f, 0f);
                s.TitleRect.anchorMax = new Vector2(titleMaxShare, 1f);
                s.TitleRect.offsetMin = new Vector2(16f, 6f);
                s.TitleRect.offsetMax = new Vector2(-8f, -6f);

                s.MinusRect.anchorMin = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.anchorMax = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.pivot = new Vector2(0f, 0.5f);
                s.MinusRect.anchoredPosition = new Vector2(8f, 0f);
                s.MinusRect.sizeDelta = new Vector2(btnSide, btnSide);

                float sliderPadY = Mathf.Max(4f, (sliderRowH - minTouch) * 0.5f);
                s.SliderRect.anchorMin = new Vector2(titleMaxShare, 0f);
                s.SliderRect.anchorMax = new Vector2(1f, 1f);
                s.SliderRect.offsetMin = new Vector2(8f + btnSide + minGutter, sliderPadY);
                s.SliderRect.offsetMax = new Vector2(sliderRightEdge, -sliderPadY);

                s.TrackRect.anchorMin = new Vector2(0f, 0.5f);
                s.TrackRect.anchorMax = new Vector2(1f, 0.5f);
                s.TrackRect.offsetMin = new Vector2(halfKnob, -10f);
                s.TrackRect.offsetMax = new Vector2(-halfKnob, 10f);

                s.FillAreaRect.anchorMin = new Vector2(0f, 0.5f);
                s.FillAreaRect.anchorMax = new Vector2(1f, 0.5f);
                s.FillAreaRect.offsetMin = new Vector2(halfKnob, -10f);
                s.FillAreaRect.offsetMax = new Vector2(-halfKnob, 10f);

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

                s.PercentRect.anchorMin = new Vector2(1f, 0f);
                s.PercentRect.anchorMax = new Vector2(1f, 1f);
                s.PercentRect.offsetMin = new Vector2(-(rightPad + pctWidth), 6f);
                s.PercentRect.offsetMax = new Vector2(-rightPad, -6f);
            }

            float segRowH = Mathf.Max(104f, minTouch + 8f);
            float segTitleShare = clampedWidth < 1020f ? 0.34f : 0.38f;
            float segPadY = Mathf.Max(4f, (segRowH - minTouch) * 0.5f);
            for (int i = 0; i < _segmentedRows.Count; i++)
            {
                SegmentedRowRefs sr = _segmentedRows[i];
                sr.RowLayout.minHeight = segRowH;
                sr.RowLayout.preferredHeight = segRowH;
                sr.TitleRect.anchorMin = new Vector2(0f, 0f);
                sr.TitleRect.anchorMax = new Vector2(segTitleShare, 1f);
                sr.TitleRect.offsetMin = new Vector2(16f, 6f);
                sr.TitleRect.offsetMax = new Vector2(-8f, -6f);

                sr.GroupRect.anchorMin = new Vector2(segTitleShare, 0f);
                sr.GroupRect.anchorMax = new Vector2(1f, 1f);
                sr.GroupRect.offsetMin = new Vector2(8f, segPadY);
                sr.GroupRect.offsetMax = new Vector2(-12f, -segPadY);
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
            float headerH = Mathf.Max(108f, minTouch + 12f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(0f, headerH));
            Image bg = KomiksUi.MakeSlicedFill("HeaderBg", _headerRect, HeaderBg, KomiksUi.RadiusM);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(140f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = MakeHeaderButton("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize, Coral, () =>
            {
                SavePersistedSettings();
                _onBack?.Invoke();
            }, out _backLabel, "BACK");

            _headerTitle = MakeFittedText("Title", _headerRect, "SETTINGS", 28, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, Vector2.zero, Vector2.one, new Vector2(166f, 8f), new Vector2(-230f, -8f));

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(204f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _resetBtnRect = MakeHeaderButton("ResetDefaultsButton", _headerRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), resetSize, Coral, ResetDefaults, out _resetLabel, "RESET DEFAULTS");
            _resetLabel.color = new Color(1f, 0.66f, 0.62f, 1f); // a quiet glass chip with a warm label, not a red slab
        }

        void BuildTabBar(RectTransform parent)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(108f, minTouch + 12f);
            float tabBarH = Mathf.Max(100f, minTouch + 4f);

            _tabBarRect = Ui.Panel("TabBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(6f + headerH + 6f)), new Vector2(0f, tabBarH));
            _tabBarLayout = _tabBarRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            _tabBarLayout.padding = new RectOffset(0, 0, 2, 2);
            _tabBarLayout.spacing = 12f;
            _tabBarLayout.childControlWidth = true;
            _tabBarLayout.childControlHeight = true;
            _tabBarLayout.childForceExpandWidth = true;
            _tabBarLayout.childForceExpandHeight = true;

            string[] names = { "AUDIO", "CONTROLS", "GRAPHICS & ACCESSIBILITY", "DONATE" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var go = new GameObject($"SettingsTab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_tabBarRect, false);
                var img = go.GetComponent<Image>();
                img.sprite = MenuArt.RoundSolid(22);
                img.type = Image.Type.Sliced;
                img.color = Color.clear; // the tab is only its label and a bar; the clear image keeps the whole tab tappable

                Image bar = Ui.MakeImage("Bar", go.transform, new Color(1f, 1f, 1f, 0.18f));
                Stretch(bar.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.94f, 0f), new Vector2(0f, 6f), new Vector2(0f, 11f));
                _tabBars[i] = bar;

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
                le.preferredWidth = i == 2 ? 320f : i == 3 ? 180f : 220f;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetActiveTab(idx));

                Text lbl = MakeFittedText("Label", go.transform, names[i], 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));

                _tabPillBgs[i] = img;
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
                () => SettingsStore.Current.Language == "fil" ? "Kalidad / Turbo Mode" : "Graphics Quality & Turbo",
                new[] { "Turbo (0.75x)", "Standard", "High" },
                null,
                () => Mathf.Clamp(SettingsStore.Current.GraphicsQuality, 0, 2),
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
            paneRoot.offsetMax = new Vector2(0f, -228f);

            Image bg = KomiksUi.MakeSlicedFill("PaneBg", paneRoot, PanelBg, KomiksUi.RadiusXL);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(paneRoot, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-22f, -4f));
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
            v.spacing = 6f;
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

            // Non-interactive visual scroll indicator (zero sub-48dp Selectable components)
            Image trackImg = KomiksUi.MakeSlicedFill("ScrollTrack", paneRoot, new Color(0.04f, 0.06f, 0.14f, 0.88f), 4);
            Stretch(trackImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-15f, 10f), new Vector2(-7f, -10f));
            Image thumbImg = KomiksUi.MakeSlicedFill("ScrollThumb", trackImg.transform, Gold, 4);
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

        /// <summary>Support Kalyeah: one row whose DONATE button opens the donate sheet (grown-up check first; gifts give nothing).</summary>
        void BuildDonateTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("DonatePane", parent, out RectTransform paneRoot);
            _tabPanes[TabDonate] = paneRoot;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(100f, minTouch + 4f);
            RectTransform row = CreateRowContainer(content, "DonateRow", rowH, out LayoutElement rowLe, out Text titleText);
            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(184f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform buttonRt = Ui.Panel("DonateBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), btnSize);
            Image buttonBg = buttonRt.gameObject.AddComponent<Image>();
            buttonBg.sprite = MenuArt.RoundSolid(22);
            buttonBg.type = Image.Type.Sliced;
            buttonBg.color = Gold;
            DonateButton = buttonRt.gameObject.AddComponent<Button>();
            DonateButton.targetGraphic = buttonBg;
            DonateButton.onClick.AddListener(() => _onDonate?.Invoke());
            Text label = MakeFittedText("Label", buttonRt, "DONATE", 22, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Outline outline = label.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            _toggleRows.Add(new ToggleRowRefs { RowLayout = rowLe, TitleRect = titleText.rectTransform, ToggleRect = buttonRt });
            _syncCallbacks.Add(() => titleText.text = SettingsStore.Current.Language == "fil"
                ? "Suportahan ang Kalyeah (magtanong muna sa nakatatanda)"
                : "Support Kalyeah (ask a grown-up first)");
        }

        /// <summary>The DONATE button on the Donate tab.</summary>
        public Button DonateButton { get; private set; }

        void AddToggleRow(RectTransform parent, string name, Func<string> titleGetter, Func<bool> getter, Action<bool> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(100f, minTouch + 4f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(184f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform toggleRt = Ui.Panel("ToggleBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), btnSize);
            Image toggleBg = toggleRt.gameObject.AddComponent<Image>();
            toggleBg.sprite = MenuArt.RoundSolid(22);
            toggleBg.type = Image.Type.Sliced;
            Button btn = toggleRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = toggleBg;
            btn.onClick.AddListener(() => setter(!getter()));

            Text stateLbl = MakeFittedText("State", toggleRt, "ON", 20, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(stateLbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

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
                toggleBg.color = on ? Teal : ChoiceIdleBg;
                stateLbl.text = on ? (fil ? "BUKAS (ON)" : "ON") : (fil ? "PATAY (OFF)" : "OFF");
                stateLbl.color = on ? Ink : Ui.Cream;
                Outline outline = stateLbl.GetComponent<Outline>();
                if (outline != null) outline.enabled = !on;
            });
        }

        void AddSliderRow(RectTransform parent, string name, Func<string> titleGetter, Func<float> getter, Action<float> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(108f, minTouch + 12f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.36f, 1f);

            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;

            // Minus button
            RectTransform minusRt = Ui.Panel("MinusBtn", row, new Vector2(0.36f, 0.5f), new Vector2(0.36f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(btnSide, btnSide));
            Image minusImg = minusRt.gameObject.AddComponent<Image>();
            minusImg.sprite = MenuArt.RoundSolid(22);
            minusImg.type = Image.Type.Sliced;
            minusImg.color = ChoiceIdleBg;
            Button minusBtn = minusRt.gameObject.AddComponent<Button>();
            minusBtn.targetGraphic = minusImg;
            minusBtn.onClick.AddListener(() => setter(Mathf.Clamp01(getter() - 0.10f)));
            Text minusTxt = MakeFittedText("Label", minusRt, "-", 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(minusTxt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // UGUI Slider (height >= 48 dp; HandleSlideArea fixed knobPx tall & inset by halfKnob so circular 48 dp Handle never stretches or escapes Slider bounds)
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            Stretch(sliderRt, new Vector2(0.36f, 0f), new Vector2(1f, 1f), new Vector2(8f + btnSide + 16f, 6f), new Vector2(-(104f + btnSide + 16f), -6f));

            Image track = KomiksUi.MakeSlicedFill("Track", sliderRt, ChoiceIdleBg, 6);
            Stretch(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(halfKnob, -10f), new Vector2(-halfKnob, 10f));

            RectTransform fillArea = Ui.Panel("FillArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(halfKnob, -10f);
            fillArea.offsetMax = new Vector2(-halfKnob, 10f);
            Image fill = KomiksUi.MakeSlicedFill("Fill", fillArea, Gold, 6);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform handleSlide = Ui.Panel("HandleSlideArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            handleSlide.offsetMin = new Vector2(halfKnob, -halfKnob);
            handleSlide.offsetMax = new Vector2(-halfKnob, halfKnob);

            // the handle keeps the full 48 dp touch area but is invisible; the knob drawn on it is a slim cream disc with a gold pip
            Image handle = Ui.MakeImage("Handle", handleSlide, Color.clear, round: true, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(knobPx, 0f);

            Image handleFace = Ui.MakeImage("KnobFace", handle.transform, Ui.Cream, round: true, raycast: false);
            Stretch(handleFace.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));

            Image handlePip = Ui.MakeImage("KnobPip", handleFace.transform, Gold, round: true, raycast: false);
            Stretch(handlePip.rectTransform, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Vector2.zero, Vector2.zero);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handleFace;

            // Plus button
            RectTransform plusRt = Ui.Panel("PlusBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-104f, 0f), new Vector2(btnSide, btnSide));
            Image plusImg = plusRt.gameObject.AddComponent<Image>();
            plusImg.sprite = MenuArt.RoundSolid(22);
            plusImg.type = Image.Type.Sliced;
            plusImg.color = ChoiceIdleBg;
            Button plusBtn = plusRt.gameObject.AddComponent<Button>();
            plusBtn.targetGraphic = plusImg;
            plusBtn.onClick.AddListener(() => setter(Mathf.Clamp01(getter() + 0.10f)));
            Text plusTxt = MakeFittedText("Label", plusRt, "+", 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(plusTxt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text pctLabel = MakeFittedText("Percent", row, "100%", 20, Gold, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            Stretch(pctLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-94f, 6f), new Vector2(-12f, -6f));

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
                PercentRect = pctLabel.rectTransform,
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
            float rowH = Mathf.Max(104f, minTouch + 8f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.36f, 1f);

            RectTransform group = Ui.Panel("Choices", row, new Vector2(0.36f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            group.offsetMin = new Vector2(8f, 4f);
            group.offsetMax = new Vector2(-12f, -4f);
            var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            var bgs = new Image[optionsEn.Length];
            var labels = new Text[optionsEn.Length];
            var choiceLayouts = new LayoutElement[optionsEn.Length];

            for (int i = 0; i < optionsEn.Length; i++)
            {
                int idx = i;
                var optGo = new GameObject($"Choice_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                optGo.transform.SetParent(group, false);
                var img = optGo.GetComponent<Image>();
                img.sprite = MenuArt.RoundSolid(22);
                img.type = Image.Type.Sliced;
                img.color = ChoiceIdleBg;

                var cle = optGo.GetComponent<LayoutElement>();
                cle.minWidth = minTouch;
                cle.minHeight = minTouch;
                cle.preferredHeight = minTouch;

                var btn = optGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => setter(idx));

                Text lbl = MakeFittedText("Label", optGo.transform, optionsEn[i], 18, Ui.Cream, TextAnchor.MiddleCenter);
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

                bgs[i] = img;
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
                    bgs[i].color = active ? Gold : ChoiceIdleBg;
                    labels[i].color = active ? Ink : Ui.Cream;
                    Outline outline = labels[i].GetComponent<Outline>();
                    if (outline != null) outline.enabled = !active;
                }
            });
        }

        static RectTransform CreateRowContainer(RectTransform parent, string name, float rowH, out LayoutElement le, out Text titleText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = MenuArt.RoundSolid(22);
            img.type = Image.Type.Sliced;
            img.color = RowBg;

            le = go.GetComponent<LayoutElement>();
            le.minHeight = rowH;
            le.preferredHeight = rowH;

            var rt = go.GetComponent<RectTransform>();
            titleText = MakeFittedText("Title", rt, "", 20, Ui.Cream, TextAnchor.MiddleLeft);
            Stretch(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(16f, 6f), new Vector2(-8f, -6f));
            return rt;
        }

        static RectTransform MakeHeaderButton(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color color,
            Action onClick,
            out Text label,
            string text)
        {
            RectTransform rt = Ui.Panel(name, parent, anchor, anchor, pivot, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = MenuArt.RoundSolid(22);
            img.type = Image.Type.Sliced;
            img.color = color;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            label = MakeFittedText("Label", rt, text, 19, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
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
