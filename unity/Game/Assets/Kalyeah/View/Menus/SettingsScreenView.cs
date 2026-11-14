using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Tabbed Settings screen (Audio, Controls, Graphics &amp; Accessibility + Reset Defaults)
    /// bound to <see cref="SettingsStore.Current"/> and persisted to <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class SettingsScreenView : IDisposable
    {
        public const int TabAudio = 0;
        public const int TabControls = 1;
        public const int TabGraphics = 2;

        static readonly Color BgTop = new Color(0.11f, 0.16f, 0.30f, 1f);
        static readonly Color BgBottom = new Color(0.05f, 0.08f, 0.16f, 1f);
        static readonly Color HeaderBg = new Color(0.06f, 0.10f, 0.22f, 0.94f);
        static readonly Color PanelBg = new Color(0.08f, 0.12f, 0.24f, 0.96f);
        static readonly Color RowBg = new Color(0.11f, 0.16f, 0.30f, 0.94f);
        static readonly Color ChoiceIdleBg = new Color(0.06f, 0.10f, 0.20f, 0.96f);
        static readonly Color Gold = new Color(0.99f, 0.79f, 0.20f, 1f);
        static readonly Color Coral = new Color(0.96f, 0.38f, 0.26f, 1f);
        static readonly Color Teal = new Color(0.22f, 0.78f, 0.68f, 1f);
        static readonly Color Ink = new Color(0.08f, 0.10f, 0.16f, 1f);

        readonly Action _onBack;
        readonly Canvas _canvas;
        readonly SafeArea _safeArea;
        readonly RectTransform _shell;

        readonly RectTransform[] _tabPanes = new RectTransform[3];
        readonly Image[] _tabPillBgs = new Image[3];
        readonly Text[] _tabPillLabels = new Text[3];
        readonly List<Action> _syncCallbacks = new List<Action>(20);

        Text _headerTitle;
        Text _backLabel;
        Text _resetLabel;

        int _activeTab;
        bool _updatingUi;
        bool _disposed;

        public RectTransform Root { get; }
        public int ActiveTab => _activeTab;

        public SettingsScreenView(Transform parent, DataSource data, Action onBack = null)
            : this(parent, onBack)
        {
            _ = data;
        }

        public SettingsScreenView(Transform parent, Action onBack = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _onBack = onBack;
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("SettingsScreen", parent);
            BuildBackdrop(Root);

            RectTransform safeRect = Ui.Fill("SafeArea", Root);
            _safeArea = safeRect.gameObject.AddComponent<SafeArea>();
            _safeArea.Changed += OnSafeAreaChanged;

            _shell = Ui.Panel("Shell", safeRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 0f));

            BuildHeader(_shell);
            BuildTabBar(_shell);
            BuildAudioTab(_shell);
            BuildControlsTab(_shell);
            BuildGraphicsTab(_shell);

            _safeArea.Apply();
            UpdateShellWidth();

            SettingsStore.Current.Changed += OnStoreChanged;
            ApplyRuntimeSettings();
            Refresh();
            SetActiveTab(TabAudio);
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
            _safeArea?.Apply();
            UpdateShellWidth();
            Refresh();
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
                if (_tabPillBgs[i] != null) _tabPillBgs[i].color = active ? Gold : RowBg;
                if (_tabPillLabels[i] != null) _tabPillLabels[i].color = active ? Ink : Ui.Cream;
            }
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
                if (_backLabel != null) _backLabel.text = fil ? "<- BALIK" : "<- BACK";
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
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SettingsStore.Current.Changed -= OnStoreChanged;
            if (_safeArea != null) _safeArea.Changed -= OnSafeAreaChanged;
        }

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

        void OnSafeAreaChanged(SafeArea _) => UpdateShellWidth();

        void UpdateShellWidth()
        {
            if (_shell == null) return;
            Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float clampedWidth = Mathf.Clamp(safeSize.x - 16f, 620f, 1180f);
            _shell.sizeDelta = new Vector2(clampedWidth, 0f);
        }

        static void BuildBackdrop(RectTransform root)
        {
            Image bottom = Ui.MakeImage("BackdropBottom", root, BgBottom);
            Stretch(bottom.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image top = Ui.MakeImage("BackdropTop", root, BgTop);
            Stretch(top.rectTransform, new Vector2(0f, 0.40f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform parent)
        {
            RectTransform bar = Ui.Panel("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(0f, 62f));
            Image bg = Ui.MakeImage("HeaderBg", bar, HeaderBg);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Vector2 backSize = ScreenFitMetrics.ClampTouchSize(new Vector2(132f, 50f), ScreenFitMetrics.MinTouchDp, _canvas);
            MakeHeaderButton("BackButton", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), backSize, Coral, () =>
            {
                SavePersistedSettings();
                _onBack?.Invoke();
            }, out _backLabel, "<- BACK");

            _headerTitle = MakeFittedText("Title", bar, "SETTINGS", 28, Gold, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(_headerTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(154f, 6f), new Vector2(-8f, -6f));

            Vector2 resetSize = ScreenFitMetrics.ClampTouchSize(new Vector2(196f, 50f), ScreenFitMetrics.MinTouchDp, _canvas);
            MakeHeaderButton("ResetDefaultsButton", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), resetSize, new Color(0.58f, 0.22f, 0.24f, 1f), ResetDefaults, out _resetLabel, "RESET DEFAULTS");
        }

        void BuildTabBar(RectTransform parent)
        {
            RectTransform tabs = Ui.Panel("TabBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(0f, 54f));
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            float minH = Mathf.Max(50f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas));
            string[] names = { "AUDIO", "CONTROLS", "GRAPHICS & ACCESSIBILITY" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var go = new GameObject($"SettingsTab_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(tabs, false);
                var img = go.GetComponent<Image>();
                img.color = RowBg;
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = minH;
                le.preferredWidth = i == 2 ? 320f : 220f;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetActiveTab(idx));

                Text lbl = MakeFittedText("Label", go.transform, names[i], 20, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

                _tabPillBgs[i] = img;
                _tabPillLabels[i] = lbl;
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

            AddSegmentedRow(
                content,
                "CardsTrayRow",
                () => SettingsStore.Current.Language == "fil" ? "Ayos ng Superpowers" : "Ability Layout (Cards Tray)",
                new[] { "Radial Arc", "4-Card Tray" },
                () => SettingsStore.Current.CardsTrayMode ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.CardsTrayMode = idx == 1));

            AddSegmentedRow(
                content,
                "CardsPlacementRow",
                () => SettingsStore.Current.Language == "fil" ? "Pwesto ng Card Tray" : "Card Tray Placement",
                new[] { "Bottom", "Sides" },
                () => SettingsStore.Current.CardsTrayPlacement == "sides" ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.CardsTrayPlacement = idx == 1 ? "sides" : "bottom"));

            AddSegmentedRow(
                content,
                "ControlScaleRow",
                () => SettingsStore.Current.Language == "fil" ? "Laki ng Pindutan (Control Scale)" : "Button Control Scale",
                new[] { "85% Small", "100% Normal", "120% Large" },
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
                () => SettingsStore.Current.TargetFrameRate <= 30 ? 0 : 1,
                idx => CommitChange(() => SettingsStore.Current.TargetFrameRate = idx == 0 ? 30 : 60));

            AddSegmentedRow(
                content,
                "GraphicsQualityRow",
                () => SettingsStore.Current.Language == "fil" ? "Kalidad / Turbo Mode" : "Graphics Quality & Turbo",
                new[] { "Turbo (0.75x)", "Standard", "High" },
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
                () => SettingsStore.Current.Language == "fil" ? "Kulay para sa Colorblind (0-3)" : "Colorblind Palette (0-3)",
                new[] { "0 · Off", "1 · Protan", "2 · Deuter", "3 · Tritan" },
                () => Mathf.Clamp(SettingsStore.Current.ColorblindMode, 0, 3),
                idx => CommitChange(() => SettingsStore.Current.ColorblindMode = idx));

            AddSegmentedRow(
                content,
                "LanguageRow",
                () => SettingsStore.Current.Language == "fil" ? "Wika (Language)" : "Language / Wika",
                new[] { "EN · English", "FIL · Filipino" },
                () => SettingsStore.Current.Language == "fil" ? 1 : 0,
                idx => CommitChange(() => SettingsStore.Current.Language = idx == 1 ? "fil" : "en"));
        }

        RectTransform CreateScrollableTabPane(string name, RectTransform parent, out RectTransform paneRoot)
        {
            paneRoot = Ui.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            paneRoot.offsetMin = new Vector2(0f, 8f);
            paneRoot.offsetMax = new Vector2(0f, -136f);

            Image bg = Ui.MakeImage("PaneBg", paneRoot, PanelBg);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(paneRoot, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
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
            v.padding = new RectOffset(8, 8, 8, 8);
            v.spacing = 10f;
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
            return content;
        }

        void AddToggleRow(RectTransform parent, string name, Func<string> titleGetter, Func<bool> getter, Action<bool> setter)
        {
            RectTransform row = CreateRowContainer(parent, name, out Text titleText);

            Vector2 btnSize = ScreenFitMetrics.ClampTouchSize(new Vector2(180f, 50f), ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform toggleRt = Ui.Panel("ToggleBtn", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), btnSize);
            Image toggleBg = toggleRt.gameObject.AddComponent<Image>();
            Button btn = toggleRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = toggleBg;
            btn.onClick.AddListener(() => setter(!getter()));

            Text stateLbl = MakeFittedText("State", toggleRt, "ON", 20, Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(stateLbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            _syncCallbacks.Add(() =>
            {
                titleText.text = titleGetter();
                bool on = getter();
                bool fil = SettingsStore.Current.Language == "fil";
                toggleBg.color = on ? Teal : ChoiceIdleBg;
                stateLbl.text = on ? (fil ? "BUKAS (ON)" : "ON") : (fil ? "PATAY (OFF)" : "OFF");
                stateLbl.color = on ? Ink : Ui.Cream;
                stateLbl.GetComponent<Outline>().enabled = !on;
            });
        }

        void AddSliderRow(RectTransform parent, string name, Func<string> titleGetter, Func<float> getter, Action<float> setter)
        {
            RectTransform row = CreateRowContainer(parent, name, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.42f, 1f);

            float btnSide = Mathf.Max(48f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas));

            // Minus button
            RectTransform minusRt = Ui.Panel("MinusBtn", row, new Vector2(0.44f, 0.5f), new Vector2(0.44f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(btnSide, btnSide));
            Image minusImg = minusRt.gameObject.AddComponent<Image>();
            minusImg.color = ChoiceIdleBg;
            Button minusBtn = minusRt.gameObject.AddComponent<Button>();
            minusBtn.targetGraphic = minusImg;
            minusBtn.onClick.AddListener(() => setter(Mathf.Clamp01(getter() - 0.10f)));
            Text minusTxt = MakeFittedText("Label", minusRt, "-", 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(minusTxt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // UGUI Slider
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            Stretch(sliderRt, new Vector2(0.51f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);

            Image track = Ui.MakeImage("Track", sliderRt, ChoiceIdleBg);
            Stretch(track.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 0.66f), Vector2.zero, Vector2.zero);

            RectTransform fillArea = Ui.Fill("FillArea", sliderRt);
            fillArea.offsetMin = new Vector2(0f, 12f);
            fillArea.offsetMax = new Vector2(0f, -12f);
            Image fill = Ui.MakeImage("Fill", fillArea, Gold);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform handleSlide = Ui.Fill("HandleSlideArea", sliderRt);
            Image handle = Ui.MakeImage("Handle", handleSlide, Ui.Cream, round: true, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(30f, 30f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;

            // Plus button
            RectTransform plusRt = Ui.Panel("PlusBtn", row, new Vector2(0.83f, 0.5f), new Vector2(0.83f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(btnSide, btnSide));
            Image plusImg = plusRt.gameObject.AddComponent<Image>();
            plusImg.color = ChoiceIdleBg;
            Button plusBtn = plusRt.gameObject.AddComponent<Button>();
            plusBtn.targetGraphic = plusImg;
            plusBtn.onClick.AddListener(() => setter(Mathf.Clamp01(getter() + 0.10f)));
            Text plusTxt = MakeFittedText("Label", plusRt, "+", 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(plusTxt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text pctLabel = MakeFittedText("Percent", row, "100%", 20, Gold, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            Stretch(pctLabel.rectTransform, new Vector2(0.89f, 0f), new Vector2(0.99f, 1f), Vector2.zero, new Vector2(-10f, 0f));

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

        void AddSegmentedRow(RectTransform parent, string name, Func<string> titleGetter, string[] options, Func<int> getter, Action<int> setter)
        {
            RectTransform row = CreateRowContainer(parent, name, out Text titleText);
            titleText.rectTransform.anchorMax = new Vector2(0.42f, 1f);

            RectTransform group = Ui.Panel("Choices", row, new Vector2(0.43f, 0.10f), new Vector2(0.99f, 0.90f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            group.offsetMax = new Vector2(-10f, 0f);
            var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            float minH = Mathf.Max(48f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas));
            var bgs = new Image[options.Length];
            var labels = new Text[options.Length];

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var optGo = new GameObject($"Choice_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                optGo.transform.SetParent(group, false);
                var img = optGo.GetComponent<Image>();
                img.color = ChoiceIdleBg;
                optGo.GetComponent<LayoutElement>().minHeight = minH;

                var btn = optGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => setter(idx));

                Text lbl = MakeFittedText("Label", optGo.transform, options[i], 18, Ui.Cream, TextAnchor.MiddleCenter);
                Stretch(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

                bgs[i] = img;
                labels[i] = lbl;
            }

            _syncCallbacks.Add(() =>
            {
                titleText.text = titleGetter();
                int selected = Mathf.Clamp(getter(), 0, options.Length - 1);
                for (int i = 0; i < options.Length; i++)
                {
                    bool active = i == selected;
                    bgs[i].color = active ? Gold : ChoiceIdleBg;
                    labels[i].color = active ? Ink : Ui.Cream;
                    labels[i].GetComponent<Outline>().enabled = !active;
                }
            });
        }

        RectTransform CreateRowContainer(RectTransform parent, string name, out Text titleText)
        {
            float rowH = Mathf.Max(66f, ScreenFitMetrics.DpToCanvasPx(54f, _canvas));
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = RowBg;
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = rowH;
            le.preferredHeight = rowH;

            var rt = go.GetComponent<RectTransform>();
            titleText = MakeFittedText("Title", rt, "", 20, Ui.Cream, TextAnchor.MiddleLeft);
            Stretch(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(16f, 4f), new Vector2(-8f, -4f));
            return rt;
        }

        static void MakeHeaderButton(
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
            img.color = color;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            label = MakeFittedText("Label", rt, text, 19, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        }

        static Text MakeFittedText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align, Font font = null)
        {
            Text t = Ui.MakeText(name, parent, text, Mathf.Max(18, maxSize), color, align, font);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
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
