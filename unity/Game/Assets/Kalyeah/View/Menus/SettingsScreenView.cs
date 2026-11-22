using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Option A1 Editorial Settings screen (Audio, Controls, Graphics &amp; Accessibility, Donate + Reset Defaults):
    /// sleek left category rail, hairline-divided rows inside smoked-obsidian tactical glass, and Signal Vermilion active pills,
    /// bound to <see cref="SettingsStore.Current"/> and persisted to <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class SettingsScreenView : IDisposable
    {
        public const int TabAudio = 0;
        public const int TabControls = 1;
        public const int TabGraphics = 2;
        /// <summary>Donate lives in Settings, not on home (docs/SPEC.md §8).</summary>
        public const int TabDonate = 3;

        static readonly Color BackdropSolid = new Color(0.035f, 0.047f, 0.066f, 1f);
        static readonly Color BackdropWash = new Color(0.055f, 0.075f, 0.102f, 0.80f);
        static readonly Color ObsidianGlass = MenuArt.ObsidianGlass;
        static readonly Color ObsidianSolid = MenuArt.ObsidianSolid;
        static readonly Color ObsidianCard = new Color(0.075f, 0.102f, 0.140f, 0.88f);
        static readonly Color Paper = MenuArt.PaperWhite;
        static readonly Color Vermilion = MenuArt.SignalVermilion;
        static readonly Color VermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color TitaniumBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.24f);
        static readonly Color TitaniumBorderStrong = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.44f);
        static readonly Color HairlineColor = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.20f);
        static readonly Color TrackFill = new Color(0.042f, 0.056f, 0.078f, 0.96f);
        static readonly Color ResetFill = new Color(0.145f, 0.068f, 0.082f, 0.86f);
        static readonly Color ResetBorder = new Color(1.00f, 0.275f, 0.333f, 0.62f);
        static readonly Color ResetText = new Color(1f, 0.78f, 0.76f, 1f);
        static readonly Color Muted = MenuArt.MutedText;

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
        readonly LayoutElement[] _tabPillLayouts = new LayoutElement[4];
        readonly List<Action> _syncCallbacks = new List<Action>(20);
        readonly List<ToggleRowRefs> _toggleRows = new List<ToggleRowRefs>(8);
        readonly List<SliderRowRefs> _sliderRows = new List<SliderRowRefs>(4);
        readonly List<SegmentedRowRefs> _segmentedRows = new List<SegmentedRowRefs>(8);
        readonly List<ScrollPaneRefs> _scrollPanes = new List<ScrollPaneRefs>(4);
        readonly List<Material> _ownedMaterials = new List<Material>(4);
        DonateRowRefs _donateRow;

        RectTransform _headerRect;
        RectTransform _backBtnRect;
        RectTransform _resetBtnRect;
        RectTransform _headerTitleRect;
        RectTransform _headerAccentRect;
        RectTransform _tabBarRect;

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
                if (_tabPillBgs[i] != null)
                {
                    ApplyGlassSurface(
                        _tabPillBgs[i],
                        active ? ObsidianSolid : new Color(0.055f, 0.075f, 0.102f, 0.36f),
                        active ? TitaniumBorderStrong : TitaniumBorder,
                        active ? Vermilion : Color.clear,
                        cornerCut: 6f,
                        accentW: active ? 3.5f : 0f);
                }
                if (_tabPillLabels[i] != null)
                {
                    Ui.SetTextColor(_tabPillLabels[i], active ? Paper : Muted);
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
                if (_tabPillLabels[2] != null) _tabPillLabels[2].text = fil ? "GRAPIKA" : "GRAPHICS";
                if (_tabPillLabels[3] != null) _tabPillLabels[3].text = fil ? "SUPORTA" : "DONATE";

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
            ReleaseOwnedMaterials();
        }

        static bool IsOwnedMaterial(Material mat) =>
            mat != null && mat != Graphic.defaultGraphicMaterial && mat != Canvas.GetDefaultCanvasMaterial();

        void TrackOwnedMaterial(Material mat)
        {
            if (IsOwnedMaterial(mat)) _ownedMaterials.Add(mat);
        }

        void ReleaseOwnedMaterials()
        {
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (IsOwnedMaterial(_ownedMaterials[i]))
                    KalyeahResources.Kill(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
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
            float headerH = Mathf.Max(98f, minTouch + 8f);
            float railW = clampedWidth < 960f ? 186f : 212f;
            float bodyTopInset = -(4f + headerH + 8f);
            float paneW = Mathf.Max(420f, clampedWidth - railW - 10f);

            if (_headerRect != null)
            {
                _headerRect.anchoredPosition = new Vector2(0f, -4f);
                _headerRect.sizeDelta = new Vector2(0f, headerH);
            }

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(140f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            if (_backBtnRect != null)
            {
                _backBtnRect.anchoredPosition = new Vector2(10f, 0f);
                _backBtnRect.sizeDelta = backSize;
            }

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(210f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
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

            // Sleek left category rail strictly below Header and strictly left of activePane
            if (_tabBarRect != null)
            {
                _tabBarRect.anchorMin = new Vector2(0f, 0f);
                _tabBarRect.anchorMax = new Vector2(0f, 1f);
                _tabBarRect.pivot = new Vector2(0f, 0.5f);
                _tabBarRect.offsetMin = new Vector2(0f, 6f);
                _tabBarRect.offsetMax = new Vector2(railW, bodyTopInset);
            }

            for (int i = 0; i < _tabPillLayouts.Length; i++)
            {
                LayoutElement le = _tabPillLayouts[i];
                if (le == null) continue;
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;
            }

            for (int i = 0; i < _tabPanes.Length; i++)
            {
                if (_tabPanes[i] == null) continue;
                _tabPanes[i].offsetMin = new Vector2(railW + 10f, 6f);
                _tabPanes[i].offsetMax = new Vector2(0f, bodyTopInset);
            }

            float toggleRowH = Mathf.Max(98f, minTouch + 2f);
            Vector2 toggleBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(158f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            for (int i = 0; i < _toggleRows.Count; i++)
            {
                ToggleRowRefs r = _toggleRows[i];
                r.RowLayout.minHeight = toggleRowH;
                r.RowLayout.preferredHeight = toggleRowH;
                r.ToggleRect.anchoredPosition = new Vector2(-12f, 0f);
                r.ToggleRect.sizeDelta = toggleBtnSize;
                r.TitleRect.anchorMin = Vector2.zero;
                r.TitleRect.anchorMax = Vector2.one;
                r.TitleRect.offsetMin = new Vector2(14f, 6f);
                r.TitleRect.offsetMax = new Vector2(-(12f + toggleBtnSize.x + minGutter), -6f);
            }

            if (_donateRow != null)
            {
                float donateRowH = Mathf.Max(132f, minTouch + 36f);
                Vector2 donateBtnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(188f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
                _donateRow.RowLayout.minHeight = donateRowH;
                _donateRow.RowLayout.preferredHeight = donateRowH;
                _donateRow.ButtonRect.anchoredPosition = new Vector2(-12f, 0f);
                _donateRow.ButtonRect.sizeDelta = donateBtnSize;
                _donateRow.CopyRect.anchorMin = Vector2.zero;
                _donateRow.CopyRect.anchorMax = Vector2.one;
                _donateRow.CopyRect.offsetMin = new Vector2(76f, 12f);
                _donateRow.CopyRect.offsetMax = new Vector2(-(12f + donateBtnSize.x + minGutter), -12f);
            }

            float sliderRowH = Mathf.Max(100f, minTouch + 4f);
            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;
            float pctWidth = 76f;
            float rightPad = 12f;
            float plusRightEdge = -(rightPad + pctWidth + 8f);
            float sliderRightEdge = plusRightEdge - btnSide - minGutter;
            float titleMaxShare = paneW < 820f ? 0.27f : 0.31f;

            for (int i = 0; i < _sliderRows.Count; i++)
            {
                SliderRowRefs s = _sliderRows[i];
                s.RowLayout.minHeight = sliderRowH;
                s.RowLayout.preferredHeight = sliderRowH;

                s.TitleRect.anchorMin = new Vector2(0f, 0f);
                s.TitleRect.anchorMax = new Vector2(titleMaxShare, 1f);
                s.TitleRect.offsetMin = new Vector2(14f, 6f);
                s.TitleRect.offsetMax = new Vector2(-6f, -6f);

                s.MinusRect.anchorMin = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.anchorMax = new Vector2(titleMaxShare, 0.5f);
                s.MinusRect.pivot = new Vector2(0f, 0.5f);
                s.MinusRect.anchoredPosition = new Vector2(4f, 0f);
                s.MinusRect.sizeDelta = new Vector2(btnSide, btnSide);

                float sliderPadY = Mathf.Max(2f, (sliderRowH - minTouch) * 0.5f);
                s.SliderRect.anchorMin = new Vector2(titleMaxShare, 0f);
                s.SliderRect.anchorMax = new Vector2(1f, 1f);
                s.SliderRect.offsetMin = new Vector2(4f + btnSide + minGutter, sliderPadY);
                s.SliderRect.offsetMax = new Vector2(sliderRightEdge, -sliderPadY);

                s.TrackRect.anchorMin = new Vector2(0f, 0.5f);
                s.TrackRect.anchorMax = new Vector2(1f, 0.5f);
                s.TrackRect.offsetMin = new Vector2(halfKnob, -4f);
                s.TrackRect.offsetMax = new Vector2(-halfKnob, 4f);

                s.FillAreaRect.anchorMin = new Vector2(0f, 0.5f);
                s.FillAreaRect.anchorMax = new Vector2(1f, 0.5f);
                s.FillAreaRect.offsetMin = new Vector2(halfKnob, -4f);
                s.FillAreaRect.offsetMax = new Vector2(-halfKnob, 4f);

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
                s.PercentRect.sizeDelta = new Vector2(pctWidth, 40f);
            }

            float segRowH = Mathf.Max(98f, minTouch + 2f);
            float segTitleShare = paneW < 820f ? 0.27f : 0.31f;
            float segPadY = Mathf.Max(1f, (segRowH - minTouch) * 0.5f);
            for (int i = 0; i < _segmentedRows.Count; i++)
            {
                SegmentedRowRefs sr = _segmentedRows[i];
                sr.RowLayout.minHeight = segRowH;
                sr.RowLayout.preferredHeight = segRowH;
                sr.TitleRect.anchorMin = new Vector2(0f, 0f);
                sr.TitleRect.anchorMax = new Vector2(segTitleShare, 1f);
                sr.TitleRect.offsetMin = new Vector2(14f, 6f);
                sr.TitleRect.offsetMax = new Vector2(-6f, -6f);

                sr.GroupRect.anchorMin = new Vector2(segTitleShare, 0f);
                sr.GroupRect.anchorMax = new Vector2(1f, 1f);
                sr.GroupRect.offsetMin = new Vector2(6f, segPadY);
                sr.GroupRect.offsetMax = new Vector2(-12f, -segPadY);
                sr.GroupLayout.spacing = Mathf.Max(6f, minGutter * 0.5f);

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
                raw.color = new Color(0.95f, 0.92f, 0.86f, 0.24f);
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
            float headerH = Mathf.Max(98f, minTouch + 8f);

            _headerRect = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, headerH));

            Image divider = Ui.MakeImage("HeaderDivider", _headerRect, HairlineColor);
            Stretch(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 1f));

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(140f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _backBtnRect = MakeHeaderButton("BackButton", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize, isReset: false, () =>
            {
                SavePersistedSettings();
                _onBack?.Invoke();
            }, out _backLabel, "BACK");

            _headerAccentRect = Ui.Panel("TitleAccent", _headerRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(164f, 0f), new Vector2(4f, 34f));
            Image accentImg = _headerAccentRect.gameObject.AddComponent<Image>();
            accentImg.color = Vermilion;
            accentImg.raycastTarget = false;

            _headerTitle = MakeFittedText("Title", _headerRect, "SETTINGS", 30, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_headerTitle);
            _headerTitleRect = _headerTitle.rectTransform;
            Stretch(_headerTitleRect, Vector2.zero, Vector2.one, new Vector2(178f, 8f), new Vector2(-234f, -8f));

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(210f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            _resetBtnRect = MakeHeaderButton("ResetDefaultsButton", _headerRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), resetSize, isReset: true, ResetDefaults, out _resetLabel, "RESET DEFAULTS");
        }

        void BuildTabBar(RectTransform parent)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float headerH = Mathf.Max(98f, minTouch + 8f);
            float bodyTopInset = -(4f + headerH + 8f);

            // Sleek left category rail (named "TabBar" under Shell so ScreenFitBoundsTests finds Header / TabBar / activePane as 3 disjoint bands)
            _tabBarRect = Ui.Panel("TabBar", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _tabBarRect.offsetMin = new Vector2(0f, 6f);
            _tabBarRect.offsetMax = new Vector2(212f, bodyTopInset);
            MakeGlassPanel("RailBg", _tabBarRect, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            var scrollGo = new GameObject("RailScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(_tabBarRect, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentGo = new GameObject("RailContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var railLayout = contentGo.GetComponent<VerticalLayoutGroup>();
            railLayout.padding = new RectOffset(2, 2, 4, 4);
            railLayout.spacing = 6f;
            railLayout.childControlWidth = true;
            railLayout.childControlHeight = true;
            railLayout.childForceExpandWidth = true;
            railLayout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollRt;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            string[] names = { "AUDIO", "CONTROLS", "GRAPHICS", "DONATE" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var go = new GameObject($"SettingsTab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(content, false);
                var hitImg = go.GetComponent<Image>();
                hitImg.color = Color.clear;

                RectTransform plateRt = Ui.Panel("Visual", go.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 56f));
                Image visualBg = MakeGlassPanel("ActivePlate", plateRt, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 6f, accentW: 0f);

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = minTouch;
                le.minHeight = minTouch;
                le.preferredHeight = minTouch;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = visualBg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SetActiveTab(idx));
                UiShaders.AttachPressSpring(btn, 0.96f, 2f);

                Text lbl = MakeFittedText("Label", plateRt, names[i], 19, Paper, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
                Ui.ClearOutline(lbl);
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-10f, -4f));

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
            paneRoot.offsetMin = new Vector2(222f, 6f);
            paneRoot.offsetMax = new Vector2(0f, -110f);

            MakeGlassPanel("PaneBg", paneRoot, ObsidianGlass, TitaniumBorder, Color.clear, cornerCut: 8f, accentW: 0f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(paneRoot, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-18f, -8f));
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
            v.padding = new RectOffset(4, 4, 2, 6);
            v.spacing = 0f;
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

            // Thin non-interactive visual scroll indicator
            Image trackImg = Ui.MakeImage("ScrollTrack", paneRoot, HairlineColor, round: false, raycast: false);
            Stretch(trackImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-10f, 14f), new Vector2(-7f, -14f));
            Image thumbImg = Ui.MakeImage("ScrollThumb", trackImg.transform, Vermilion, round: false, raycast: false);
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

        /// <summary>Support Kalyeah: one editorial support card whose Signal Vermilion DONATE CTA opens the donate sheet.</summary>
        void BuildDonateTab(RectTransform parent)
        {
            RectTransform content = CreateScrollableTabPane("DonatePane", parent, out RectTransform paneRoot);
            _tabPanes[TabDonate] = paneRoot;

            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(132f, minTouch + 36f);
            RectTransform row = CreateRowContainer(content, "DonateRow", rowH, out LayoutElement rowLe, out Text titleText);

            RectTransform iconWell = Ui.Panel("IconWell", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(48f, 48f));
            MakeGlassPanel("WellBg", iconWell, ObsidianSolid, TitaniumBorderStrong, Vermilion, cornerCut: 6f, accentW: 3f);
            Image heartGlyph = Ui.MakeImage("HeartGlyph", iconWell, Vermilion, round: false, raycast: false);
            heartGlyph.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
            heartGlyph.rectTransform.anchorMin = heartGlyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.sizeDelta = new Vector2(22f, 22f);

            RectTransform copyStack = Ui.Panel("CopyStack", row, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            copyStack.offsetMin = new Vector2(76f, 12f);
            copyStack.offsetMax = new Vector2(-216f, -12f);
            titleText.transform.SetParent(copyStack, false);
            titleText.font = KalyeahResources.DisplayFont();
            titleText.fontSize = 22;
            titleText.resizeTextMaxSize = 22;
            titleText.alignment = TextAnchor.LowerLeft;
            Ui.ClearOutline(titleText);
            Stretch(titleText.rectTransform, new Vector2(0f, 0.48f), Vector2.one, Vector2.zero, Vector2.zero);

            Text subText = MakeFittedText("Subtitle", copyStack, "Optional community tip · unlocks zero gameplay power", 16, Muted, TextAnchor.UpperLeft);
            subText.resizeTextMinSize = 11;
            subText.resizeTextMaxSize = 16;
            Ui.ClearOutline(subText);
            Stretch(subText.rectTransform, Vector2.zero, new Vector2(1f, 0.44f), Vector2.zero, Vector2.zero);

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(188f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform buttonRt = Ui.Panel("DonateBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), btnSize);
            Image hitImg = buttonRt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;

            RectTransform visualRt = Ui.Panel("Visual", buttonRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
            Image visualBg = MakeCtaPanel("Face", visualRt, Vermilion, VermilionDeep, cornerCut: 8f);

            DonateButton = buttonRt.gameObject.AddComponent<Button>();
            DonateButton.targetGraphic = visualBg;
            DonateButton.transition = Selectable.Transition.None;
            DonateButton.onClick.AddListener(() => _onDonate?.Invoke());
            UiShaders.AttachPressSpring(DonateButton, 0.94f, 3f);

            Text label = MakeFittedText("Label", visualRt, "DONATE", 21, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(label);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

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

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(158f, minTouch), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform toggleRt = Ui.Panel("ToggleBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), btnSize);
            Image hitImg = toggleRt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;

            RectTransform switchVisual = Ui.MakeToggleVisual(
                "SwitchVisual",
                toggleRt,
                out Image trackImg,
                out RectTransform thumbRect,
                out Image _,
                out Text stateLbl,
                trackWidth: 112f,
                trackHeight: 42f);
            switchVisual.anchorMin = new Vector2(1f, 0.5f);
            switchVisual.anchorMax = new Vector2(1f, 0.5f);
            switchVisual.pivot = new Vector2(1f, 0.5f);
            switchVisual.anchoredPosition = new Vector2(-4f, 0f);
            stateLbl.resizeTextForBestFit = true;
            stateLbl.resizeTextMinSize = 11;
            stateLbl.resizeTextMaxSize = 15;
            stateLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            stateLbl.verticalOverflow = VerticalWrapMode.Overflow;

            Button btn = toggleRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = trackImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => setter(!getter()));
            UiShaders.AttachPressSpring(btn, 0.95f, 2f);

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
                trackImg.color = on ? Vermilion : ObsidianCard;
                Ui.SetTextColor(stateLbl, on ? Paper : Muted);
                Ui.ClearOutline(stateLbl);
            });
        }

        void AddSliderRow(RectTransform parent, string name, Func<string> titleGetter, Func<float> getter, Action<float> setter)
        {
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float rowH = Mathf.Max(100f, minTouch + 4f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.31f, 1f);

            float btnSide = minTouch;
            float knobPx = minTouch;
            float halfKnob = knobPx * 0.5f;

            RectTransform minusRt = MakeStepperButton(
                "MinusBtn",
                row,
                new Vector2(0.31f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(4f, 0f),
                btnSide,
                "-",
                () => setter(Mathf.Clamp01(getter() - 0.10f)));

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            Stretch(sliderRt, new Vector2(0.31f, 0f), new Vector2(1f, 1f), new Vector2(4f + btnSide + 12f, 4f), new Vector2(-(98f + btnSide + 12f), -4f));

            Image track = Ui.MakeImage("Track", sliderRt, TrackFill, round: false, raycast: false);
            Stretch(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(halfKnob, -4f), new Vector2(-halfKnob, 4f));

            RectTransform fillArea = Ui.Panel("FillArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(halfKnob, -4f);
            fillArea.offsetMax = new Vector2(-halfKnob, 4f);
            Image fill = Ui.MakeImage("Fill", fillArea, Vermilion, round: false, raycast: false);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform handleSlide = Ui.Panel("HandleSlideArea", sliderRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            handleSlide.offsetMin = new Vector2(halfKnob, -halfKnob);
            handleSlide.offsetMax = new Vector2(-halfKnob, halfKnob);

            Image handle = Ui.MakeImage("Handle", handleSlide, Color.clear, round: true, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(knobPx, 0f);

            Image handleFace = Ui.MakeImage("KnobFace", handle.transform, Paper, round: true, raycast: false);
            handleFace.rectTransform.anchorMin = handleFace.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handleFace.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handleFace.rectTransform.sizeDelta = new Vector2(26f, 26f);

            Image handlePip = Ui.MakeImage("KnobPip", handleFace.transform, Vermilion, round: true, raycast: false);
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

            RectTransform plusRt = MakeStepperButton(
                "PlusBtn",
                row,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-98f, 0f),
                btnSide,
                "+",
                () => setter(Mathf.Clamp01(getter() + 0.10f)));

            RectTransform pctChip = Ui.Panel("PercentChip", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(76f, 40f));
            MakeGlassPanel("Bg", pctChip, ObsidianSolid, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);
            Text pctLabel = MakeFittedText("Label", pctChip, "100%", 18, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(pctLabel);
            Stretch(pctLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));

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
            string glyph,
            Action onClick)
        {
            RectTransform rt = Ui.Panel(name, parent, anchor, anchor, pivot, pos, new Vector2(hitSide, hitSide));
            Image hitImg = rt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;

            RectTransform faceRt = Ui.Panel("Face", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            Image faceImg = MakeGlassPanel("Disc", faceRt, ObsidianCard, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiShaders.AttachPressSpring(btn, 0.92f, 2f);

            Text txt = MakeFittedText("Label", faceRt, glyph, 24, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(txt);
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
            float rowH = Mathf.Max(98f, minTouch + 2f);
            RectTransform row = CreateRowContainer(parent, name, rowH, out LayoutElement rowLe, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.31f, 1f);

            RectTransform group = Ui.Panel("Choices", row, new Vector2(0.31f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            group.offsetMin = new Vector2(6f, 2f);
            group.offsetMax = new Vector2(-12f, -2f);
            var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
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
                var hitImg = optGo.GetComponent<Image>();
                hitImg.color = Color.clear;

                RectTransform pillRt = Ui.Panel("Visual", optGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
                Image visualImg = MakeGlassPanel("Plate", pillRt, ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);

                var cle = optGo.GetComponent<LayoutElement>();
                cle.minWidth = minTouch;
                cle.minHeight = minTouch;
                cle.preferredHeight = minTouch;

                var btn = optGo.GetComponent<Button>();
                btn.targetGraphic = visualImg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => setter(idx));
                UiShaders.AttachPressSpring(btn, 0.95f, 2f);

                Text lbl = MakeFittedText("Label", pillRt, optionsEn[i], 17, Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Ui.ClearOutline(lbl);
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

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
                    if (active)
                        ApplyGlassSurface(bgs[i], Vermilion, Color.clear, Color.clear, cornerCut: 5f, accentW: 0f);
                    else
                        ApplyGlassSurface(bgs[i], ObsidianCard, TitaniumBorder, Color.clear, cornerCut: 5f, accentW: 0f);
                    Ui.SetTextColor(labels[i], active ? Paper : Muted);
                    Ui.ClearOutline(labels[i]);
                }
            });
        }

        // Hairline-divided row (no heavy box-per-row!)
        static RectTransform CreateRowContainer(RectTransform parent, string name, float rowH, out LayoutElement le, out Text titleText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            le = go.GetComponent<LayoutElement>();
            le.minHeight = rowH;
            le.preferredHeight = rowH;

            var rt = go.GetComponent<RectTransform>();
            Image bottomRule = Ui.MakeImage("Hairline", rt, HairlineColor, round: false, raycast: false);
            Stretch(bottomRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 0f), new Vector2(-12f, 1f));

            titleText = MakeFittedText("Title", rt, "", 19, Paper, TextAnchor.MiddleLeft);
            Ui.ClearOutline(titleText);
            Stretch(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(14f, 6f), new Vector2(-8f, -6f));
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
            hitImg.color = Color.clear;

            RectTransform visualRt = Ui.Panel("Visual", rt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f));
            Image visualBg = MakeGlassPanel(
                "Plate",
                visualRt,
                isReset ? ResetFill : ObsidianCard,
                isReset ? ResetBorder : TitaniumBorderStrong,
                Color.clear,
                cornerCut: 6f,
                accentW: 0f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = visualBg;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            UiShaders.AttachPressSpring(btn, 0.95f, 2f);

            float textLeft = 10f;
            if (!isReset)
            {
                Image chev = Ui.MakeImage("Chevron", visualRt, Paper, round: false, raycast: false);
                chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
                RectTransform cr = chev.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = new Vector2(20f, 0f);
                cr.sizeDelta = new Vector2(15f, 15f);
                cr.localEulerAngles = new Vector3(0f, 0f, 180f);
                textLeft = 32f;
            }

            label = MakeFittedText("Label", visualRt, text, isReset ? 17 : 19, isReset ? ResetText : Paper, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(label);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(textLeft, 4f), new Vector2(-10f, -4f));
            return rt;
        }

        static Image MakeGlassPanel(string name, Transform parent, Color fill, Color border, Color leftAccent, float cornerCut = 6f, float accentW = 0f)
        {
            Image img = Ui.MakeImage(name, parent, Color.white, round: false, raycast: false);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplyGlassSurface(img, fill, border, leftAccent, cornerCut, accentW);
            return img;
        }

        Image MakeCtaPanel(string name, Transform parent, Color top, Color bottom, float cornerCut = 8f)
        {
            Image img = Ui.MakeImage(name, parent, top, round: false, raycast: false);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Material mat = UiShaders.CreateMetallicCta(top, bottom, cornerCut);
            TrackOwnedMaterial(mat);
            if (IsOwnedMaterial(mat))
            {
                img.material = mat;
                img.color = Color.white;
            }
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

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(16, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = Mathf.Max(16, maxSize);
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
