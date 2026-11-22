using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Option A1 Editorial async scene-transition curtain (<c>Title &lt;-&gt; Match</c>) and deterministic dual-aspect preview:
    /// full-bleed sunlit barangay backdrop + smoked obsidian scrim on <see cref="Root"/>, and four strictly disjoint
    /// SafeArea bands: Brand + 5v5 frameless bust faceoff, Mode pill, Ate Lorna (<see cref="LoreCodexData"/>) Tip card,
    /// and a glowing Signal Vermilion full-width progress bar.
    /// </summary>
    public sealed class LoadingScreenView : IDisposable
    {
        public const int CurtainSortingOrder = 500;
        public const string DefaultModeTitle = "AGAWANG BASE \u00B7 5V5";
        public const string HomeModeTitle = "BARANGAY HOME";

        static readonly Color FallbackBackdropColor = new Color(0.035f, 0.047f, 0.066f, 1f);
        static readonly Color BackdropTintColor = new Color(0.95f, 0.92f, 0.86f, 0.34f);
        static readonly Color ScrimTintColor = new Color(0.055f, 0.075f, 0.102f, 0.78f);
        static readonly Color ObsidianGlass = MenuArt.ObsidianGlass;
        static readonly Color ObsidianSolid = MenuArt.ObsidianSolid;
        static readonly Color Paper = MenuArt.PaperWhite;
        static readonly Color Vermilion = MenuArt.SignalVermilion;
        static readonly Color VermilionDeep = MenuArt.SignalVermilionDeep;
        static readonly Color CoralGold = MenuArt.CoralGold;
        static readonly Color TitaniumBorder = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.24f);
        static readonly Color TitaniumBorderStrong = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.44f);
        static readonly Color AllyBlue = MenuArt.AllyBlue;
        static readonly Color EnemyRed = MenuArt.EnemyRed;
        static readonly Color TrackFill = new Color(0.040f, 0.054f, 0.076f, 0.96f);

        static readonly string[] DefaultBlueSquad = { "narda", "karding", "oriang", "paeng", "domeng" };
        static readonly string[] DefaultRedSquad = { "jumong", "kikay", "pamela", "jopay", "manoy" };

        readonly struct TipEntry
        {
            public readonly string MatchToken;
            public readonly string En;
            public readonly string Fil;

            public TipEntry(string matchToken, string en, string fil)
            {
                MatchToken = matchToken;
                En = en;
                Fil = fil;
            }
        }

        static readonly TipEntry[] CuratedTips =
        {
            new TipEntry(
                "AGAWANG",
                "Stand in your Rescue Shed for 5 s to free tagged allies, then push together to capture the Enemy Base post!",
                "Tumayo sa inyong Rescue Shed nang 5 s para palayain ang natayang kakampi, saka sama-samang agawin ang Enemy Base!"),
            new TipEntry(
                "PATINTERO",
                "Watch the center line guard! Fake a sprint to bait their lunge, then dash cleanly across the chalk lines and back.",
                "Bantayan ang patotot sa gitnang guhit! Mag-fake sprint bago tumawid nang malinis pabalik sa dulo."),
            new TipEntry(
                "TUMBANG",
                "Knock down the tin can with your tsinelas first so the taya must stand it back up while you retrieve your slipper!",
                "Patumbahin muna ang lata gamit ang tsinelas para abala ang taya habang binabawi mo ang tsinelas mo!"),
            new TipEntry(
                "LANGIT",
                "High curbs, benches, and crates count as Langit! Hop down to Lupa before the step timer runs out to keep moving.",
                "Ligtas ka sa matataas na bangketa at kahon bilang Langit! Bumaba bago maubos ang oras para hindi mataya."),
            new TipEntry(
                "HOTSPOT",
                "Playing on Barangay Hotspot? Keep phones within clear line-of-sight of the host for crisp 30 Hz street sync.",
                "Sa Barangay Hotspot, panatilihing malapit ang mga telepono sa host para swabe ang 30 Hz na laro."),
            new TipEntry(
                "BARANGAY",
                "Every kid brings 3 unique street-game skills on a shared Water bar — time your Ultimate when the squad pushes!",
                "May 3 kakaibang larong-kalye skill ang bawat bata — itugma ang Ultimate kapag sabay-sabay na sumusugod ang barkada!"),
        };

        [ExecuteAlways]
        sealed class LoadingLayoutHook : MonoBehaviour
        {
            [NonSerialized] public LoadingScreenView Owner;
            bool _subscribed;

            void OnEnable()
            {
                EnsureSubscribed();
                Owner?.RefreshLayout();
            }

            void OnDisable() => Unsubscribe();

            void OnDestroy() => Unsubscribe();

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
                if (!isActiveAndEnabled) return;
                Owner?.RefreshLayout();
            }
        }

        sealed class LoadingCurtainRunner : MonoBehaviour
        {
            LoadingScreenView _view;
            Coroutine _routine;
            bool _inFlight;
            bool _sceneReadySignaled;

            public bool IsInFlight => _inFlight;

            public void BeginTransition(string sceneName, DataSource data, string modeTitle)
            {
                if (_inFlight) return;

                if (Time.timeScale <= 0f)
                    Time.timeScale = 1f;

                DataSource resolvedData = data ?? KalyeahResources.Data();
                MatchBoot.ApplyBootResourcesAndGraphics(resolvedData);

                if (_view == null)
                {
                    _view = new LoadingScreenView(
                        transform,
                        modeTitle,
                        progress: 0.08f,
                        blockRaycasts: true,
                        deterministicTipIndex: ResolveTipIndexForMode(modeTitle, advanceCursor: true));
                }
                else
                {
                    _view.Canvas.gameObject.SetActive(true);
                    _view.SetTipIndex(ResolveTipIndexForMode(modeTitle, advanceCursor: true));
                    _view.SetModeTitle(modeTitle);
                    _view.SetProgress(0.08f);
                }

                _view.SetCanvasAlpha(1f, blockRaycasts: true);
                _view.RefreshLayout();
                Canvas.ForceUpdateCanvases();

                _sceneReadySignaled = false;
                _inFlight = true;
                if (_routine != null)
                    StopCoroutine(_routine);
                _routine = StartCoroutine(RunTransition(sceneName));
            }

            public void SignalSceneReady()
            {
                _sceneReadySignaled = true;
                if (!_inFlight && _view != null && _view.Canvas != null)
                {
                    _view.SetCanvasAlpha(0f, blockRaycasts: false);
                    _view.Canvas.gameObject.SetActive(false);
                }
            }

            public void HideNow()
            {
                if (_routine != null)
                {
                    StopCoroutine(_routine);
                    _routine = null;
                }
                _inFlight = false;
                _sceneReadySignaled = true;
                if (_view != null && _view.Canvas != null)
                {
                    _view.SetCanvasAlpha(0f, blockRaycasts: false);
                    _view.Canvas.gameObject.SetActive(false);
                }
            }

            IEnumerator RunTransition(string sceneName)
            {
                yield return null;

                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
                float shown = 0.08f;
                float tipTimer = 0f;

                while (op != null && !op.isDone)
                {
                    float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
                    tipTimer += dt;

                    float raw = Mathf.Clamp01(op.progress / 0.9f);
                    float target = Mathf.Lerp(0.14f, 0.90f, raw);
                    bool reduceMotion = SettingsStore.Current.ReduceMotion;

                    shown = reduceMotion
                        ? Mathf.Max(shown, target)
                        : Mathf.MoveTowards(shown, Mathf.Max(shown, target), dt * 1.85f);
                    _view.SetProgress(shown);

                    if (!reduceMotion && tipTimer >= 3.2f)
                    {
                        tipTimer = 0f;
                        _view.NextTip();
                    }

                    yield return null;
                }

                int settleFrames = 0;
                while (!_sceneReadySignaled && settleFrames < 3)
                {
                    settleFrames++;
                    yield return null;
                }

                _view.SetProgress(1f);

                if (!SettingsStore.Current.ReduceMotion)
                {
                    yield return null;
                    const float fadeDuration = 0.16f;
                    float t = 0f;
                    while (t < fadeDuration)
                    {
                        t += Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
                        float alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                        _view.SetCanvasAlpha(alpha, blockRaycasts: false);
                        yield return null;
                    }
                }

                _view.SetCanvasAlpha(0f, blockRaycasts: false);
                if (_view.Canvas != null)
                    _view.Canvas.gameObject.SetActive(false);

                _inFlight = false;
                _routine = null;
            }

            void OnDestroy()
            {
                _view?.Dispose();
                _view = null;
                if (ReferenceEquals(s_runner, this))
                    s_runner = null;
            }
        }

        static LoadingCurtainRunner s_runner;
        static int s_tipCursor;

        readonly bool _ownsCanvas;
        readonly CanvasGroup _canvasGroup;
        readonly Image _scrimImage;
        readonly LoadingLayoutHook _layoutHook;
        readonly RectTransform _tipIconWellRect;
        readonly RectTransform _tipHeaderRect;
        readonly RectTransform _tipBodyRect;
        readonly RectTransform _statusRect;
        readonly RectTransform _percentRect;
        readonly RectTransform _progressInnerRect;
        readonly Material _gaugeMat;

        int _tipIndex;
        string _statusOverride;
        bool _disposed;

        public Canvas Canvas { get; }
        public RectTransform Root { get; }
        public RectTransform BackdropRect { get; }
        public RectTransform BackdropArtRect { get; }
        public RectTransform ScrimRect { get; }
        public SafeArea SafeArea { get; }
        public RectTransform SafeRoot { get; }
        public RectTransform LogoFrameRect { get; }
        public RectTransform ModeBadgeRect { get; }
        public RectTransform TipCardRect { get; }
        public RectTransform ProgressCardRect { get; }
        public RectTransform ProgressTrackRect { get; }
        public RectTransform ProgressFillRect { get; }

        public Text ModeTitleLabel { get; }
        public Text TipHeaderLabel { get; }
        public Text TipBodyLabel { get; }
        public Text StatusLabel { get; }
        public Text PercentLabel { get; }

        public string ModeTitle { get; private set; }
        public float Progress { get; private set; }

        public static bool IsTransitionActive => s_runner != null && s_runner.IsInFlight;

        /// <summary>
        /// Builds a deterministic static preview of <see cref="LoadingScreenView"/> under <paramref name="parent"/>
        /// for dual-aspect <c>GuiRoastScreenshots</c> and <c>ScreenFitBoundsTests</c>.
        /// </summary>
        public static LoadingScreenView BuildPreview(
            Transform parent,
            DataSource data,
            string modeTitle = DefaultModeTitle,
            float progress = 0.68f)
        {
            if (data != null)
                MatchBoot.ApplyBootResourcesAndGraphics(data);

            var view = new LoadingScreenView(
                parent,
                modeTitle,
                progress,
                blockRaycasts: false,
                deterministicTipIndex: ResolveTipIndexForMode(modeTitle, advanceCursor: false));

            if (view._ownsCanvas && view.Canvas != null)
            {
                GraphicRaycaster raycaster = view.Canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                    raycaster.enabled = false;
            }

            view.RefreshLayout();
            return view;
        }

        public static void LoadScene(string sceneName, DataSource data = null, string modeTitle = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;

            string resolvedMode = !string.IsNullOrWhiteSpace(modeTitle)
                ? modeTitle.Trim()
                : ResolveSessionModeTitle(AppSession.Current);

            if (!Application.isPlaying)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (s_runner == null)
            {
                var curtainGo = new GameObject("KalyeahLoadingCurtain");
                UnityEngine.Object.DontDestroyOnLoad(curtainGo);
                s_runner = curtainGo.AddComponent<LoadingCurtainRunner>();
            }

            s_runner.BeginTransition(sceneName, data, resolvedMode);
        }

        public static void CompleteAndHide()
        {
            if (s_runner != null)
                s_runner.SignalSceneReady();
        }

        public static void HideImmediate()
        {
            if (s_runner != null)
                s_runner.HideNow();
        }

        public static string ResolveHomeModeTitle()
        {
            return IsFilipinoLocale() ? "BARANGAY \u00B7 UWIAN" : HomeModeTitle;
        }

        public static string ResolveSessionModeTitle(AppSession session = null)
        {
            AppSession s = session ?? AppSession.Current;
            bool fil = IsFilipinoLocale();
            if (s.TryGetActiveStreetKind(out StreetGameKind streetKind))
            {
                string prefix = s.Mode == GameMode.Online
                    ? "ONLINE \u00B7 "
                    : (s.Mode == GameMode.HotspotHost || s.Mode == GameMode.HotspotClient)
                        ? "HOTSPOT LAN \u00B7 "
                        : string.Empty;
                switch (streetKind)
                {
                    case StreetGameKind.Patintero:
                        return string.IsNullOrEmpty(prefix)
                            ? "PATINTERO \u00B7 HARANGANG TAGA"
                            : prefix + "PATINTERO";
                    case StreetGameKind.TumbangPreso:
                        return string.IsNullOrEmpty(prefix)
                            ? "TUMBANG PRESO \u00B7 TUMBA LATA"
                            : prefix + "TUMBANG PRESO";
                    case StreetGameKind.LangitLupa:
                        return string.IsNullOrEmpty(prefix)
                            ? (fil ? "LANGIT-LUPA \u00B7 HABULAN" : "LANGIT-LUPA \u00B7 STREET TAG")
                            : prefix + "LANGIT-LUPA";
                }
            }

            switch (s.Mode)
            {
                case GameMode.Training:
                    return fil ? "ENSAYO \u00B7 AGAWANG BASE" : "TRAINING \u00B7 AGAWANG BASE";
                case GameMode.Online:
                    return "ONLINE \u00B7 AGAWANG BASE 5V5";
                case GameMode.HotspotHost:
                case GameMode.HotspotClient:
                    return "HOTSPOT LAN \u00B7 AGAWANG BASE 5V5";
                default:
                    return DefaultModeTitle;
            }
        }

        LoadingScreenView(
            Transform parent,
            string modeTitle,
            float progress,
            bool blockRaycasts,
            int deterministicTipIndex)
        {
            Canvas parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            if (parentCanvas != null)
            {
                Canvas = parentCanvas;
                _ownsCanvas = false;
                Root = Ui.Fill("LoadingScreen", parent);
            }
            else
            {
                Canvas = Ui.MakeCanvas("LoadingCanvas", parent, camera: null);
                Canvas.overrideSorting = true;
                Canvas.sortingOrder = CurtainSortingOrder;
                _ownsCanvas = true;
                Root = Ui.Fill("LoadingScreen", Canvas.transform);
            }

            _canvasGroup = Root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = blockRaycasts;

            _layoutHook = Root.gameObject.AddComponent<LoadingLayoutHook>();
            _layoutHook.Owner = this;

            // 1. Full-bleed Backdrop + EnvelopeParent art + Option A1 Smoked Obsidian Scrim on Root
            Image backdropFallback = Ui.MakeImage("Backdrop", Root, FallbackBackdropColor, round: false, raycast: false);
            BackdropRect = backdropFallback.rectTransform;
            FillFullBleed(BackdropRect);
            BackdropRect.gameObject.AddComponent<RectMask2D>();

            Texture2D bgTex = KalyeahResources.Texture("ui/home/stall") ?? KalyeahResources.Texture("ui/main_bg") ?? KalyeahResources.Texture("ui/boot/boot_bg");
            if (bgTex != null)
            {
                var bgArtGo = new GameObject("BackdropArt", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                bgArtGo.transform.SetParent(BackdropRect, false);
                var bgRaw = bgArtGo.GetComponent<RawImage>();
                bgRaw.texture = bgTex;
                bgRaw.color = BackdropTintColor;
                bgRaw.raycastTarget = false;

                var bgFitter = bgArtGo.GetComponent<AspectRatioFitter>();
                bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                bgFitter.aspectRatio = bgTex.width / Mathf.Max(1f, (float)bgTex.height);
                BackdropArtRect = bgRaw.rectTransform;
            }

            _scrimImage = Ui.MakeImage("Scrim", Root, ScrimTintColor, round: false, raycast: blockRaycasts);
            ScrimRect = _scrimImage.rectTransform;
            FillFullBleed(ScrimRect);

            // 2. SafeArea child holding 4 strictly disjoint editorial bands
            SafeRoot = Ui.Fill("SafeArea", Root);
            SafeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
            SafeArea.Changed += OnSafeAreaChanged;

            Font displayFont = KalyeahResources.DisplayFont();
            Font uiFont = KalyeahResources.UiFont();

            // 2A. Brand Wordmark + Crisp A1 5v5 Frameless Bust Faceoff Strip (contained cleanly inside LogoFrameRect)
            LogoFrameRect = Ui.Panel(
                "LogoFrame",
                SafeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 186f),
                new Vector2(780f, 192f));

            RectTransform wordmarkSlot = Ui.Panel("WordmarkSlot", LogoFrameRect, new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Texture2D logoTex = KalyeahResources.Texture("ui/title_logo")
                ?? KalyeahResources.Texture("ui/kalyeah_logo")
                ?? KalyeahResources.Texture("ui/brand/kalyeah_wordmark")
                ?? KalyeahResources.Texture("ui/brand/kalyeah_crest");
            if (logoTex != null)
            {
                var logoGo = new GameObject("TitleLogo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                logoGo.transform.SetParent(wordmarkSlot, false);
                var logoRaw = logoGo.GetComponent<RawImage>();
                logoRaw.texture = logoTex;
                logoRaw.color = Color.white;
                logoRaw.raycastTarget = false;

                var logoFitter = logoGo.GetComponent<AspectRatioFitter>();
                logoFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                logoFitter.aspectRatio = logoTex.width / Mathf.Max(1f, (float)logoTex.height);
            }
            else
            {
                Text fallbackLogo = Ui.MakeText("TitleLogoFallback", wordmarkSlot, "KALYEAH!", 38, Paper, TextAnchor.MiddleCenter, displayFont);
                Ui.ClearOutline(fallbackLogo);
                ConfigureReadableText(fallbackLogo, minSize: 18, maxSize: 38);
                FillInset(fallbackLogo.rectTransform, 8f, 2f, 8f, 2f);
            }

            // 5v5 Frameless Bust Faceoff Cards (Blue 5 vs VS vs Red 5)
            BuildFaceoffRow(LogoFrameRect, displayFont);

            // 2B. Mode Badge Pill (smoked obsidian editorial plate with Signal Vermilion left accent)
            Image badgeBg = Ui.MakeImage("ModeBadge", SafeRoot, ObsidianSolid, round: false, raycast: false);
            ModeBadgeRect = badgeBg.rectTransform;
            ModeBadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            ModeBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            ModeBadgeRect.pivot = new Vector2(0.5f, 0.5f);
            ApplyGlassSurface(badgeBg, ObsidianSolid, TitaniumBorderStrong, Vermilion, cornerCut: 6f, accentW: 3.5f);

            ModeTitleLabel = Ui.MakeText("ModeTitle", ModeBadgeRect, "", 22, Paper, TextAnchor.MiddleCenter, displayFont);
            Ui.ClearOutline(ModeTitleLabel);
            ConfigureReadableText(ModeTitleLabel, minSize: 13, maxSize: 22);
            FillInset(ModeTitleLabel.rectTransform, 18f, 4f, 18f, 4f);

            // 2C. Ate Lorna Sari-Sari Intro + Barangay Tip Card
            Image tipBg = Ui.MakeImage("TipCard", SafeRoot, ObsidianGlass, round: false, raycast: false);
            TipCardRect = tipBg.rectTransform;
            TipCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            TipCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            TipCardRect.pivot = new Vector2(0.5f, 0.5f);
            ApplyGlassSurface(tipBg, ObsidianGlass, TitaniumBorder, Paper, cornerCut: 8f, accentW: 3f);

            Image iconWell = Ui.MakeImage("TipIconWell", TipCardRect, ObsidianSolid, round: false, raycast: false);
            _tipIconWellRect = iconWell.rectTransform;
            _tipIconWellRect.anchorMin = new Vector2(0f, 0.5f);
            _tipIconWellRect.anchorMax = new Vector2(0f, 0.5f);
            _tipIconWellRect.pivot = new Vector2(0f, 0.5f);
            ApplyGlassSurface(iconWell, ObsidianSolid, TitaniumBorderStrong, Color.clear, cornerCut: 6f, accentW: 0f);

            string featuredKid = string.IsNullOrWhiteSpace(AppSession.Current.SelectedCharacter)
                ? AppSession.DefaultCharacter
                : AppSession.Current.SelectedCharacter;
            Sprite bustSprite = KalyeahResources.KidBustSprite(featuredKid);
            if (bustSprite != null)
            {
                Image bustImg = Ui.MakeImage("TipBust", _tipIconWellRect, Color.white, round: false, raycast: false);
                bustImg.sprite = bustSprite;
                bustImg.preserveAspect = true;
                FillInset(bustImg.rectTransform, 4f, 4f, 4f, 4f);
            }
            else
            {
                Text bulbFallback = Ui.MakeText("TipGlyph", _tipIconWellRect, "AL", 22, CoralGold, TextAnchor.MiddleCenter, displayFont);
                Ui.ClearOutline(bulbFallback);
                ConfigureReadableText(bulbFallback, minSize: 14, maxSize: 22);
                FillFullBleed(bulbFallback.rectTransform);
            }

            TipHeaderLabel = Ui.MakeText("TipHeader", TipCardRect, "", 18, CoralGold, TextAnchor.MiddleLeft, displayFont);
            ConfigureReadableText(TipHeaderLabel, minSize: 12, maxSize: 18);
            Ui.ClearOutline(TipHeaderLabel);
            _tipHeaderRect = TipHeaderLabel.rectTransform;

            TipBodyLabel = Ui.MakeText("TipBody", TipCardRect, "", 18, Paper, TextAnchor.MiddleLeft, uiFont);
            ConfigureReadableText(TipBodyLabel, minSize: 11, maxSize: 18);
            TipBodyLabel.lineSpacing = 1.08f;
            Ui.ClearOutline(TipBodyLabel);
            _tipBodyRect = TipBodyLabel.rectTransform;

            // 2D. Glowing Signal Vermilion Full-Width Progress Bar Card
            Image progCardBg = Ui.MakeImage("ProgressCard", SafeRoot, ObsidianGlass, round: false, raycast: false);
            ProgressCardRect = progCardBg.rectTransform;
            ProgressCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            ProgressCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            ProgressCardRect.pivot = new Vector2(0.5f, 0.5f);
            ApplyGlassSurface(progCardBg, ObsidianGlass, TitaniumBorderStrong, Color.clear, cornerCut: 8f, accentW: 0f);

            StatusLabel = Ui.MakeText("StatusLabel", ProgressCardRect, "", 19, Paper, TextAnchor.MiddleLeft, displayFont);
            Ui.ClearOutline(StatusLabel);
            ConfigureReadableText(StatusLabel, minSize: 13, maxSize: 19);
            _statusRect = StatusLabel.rectTransform;

            PercentLabel = Ui.MakeText("PercentLabel", ProgressCardRect, "", 20, Vermilion, TextAnchor.MiddleRight, displayFont);
            Ui.ClearOutline(PercentLabel);
            ConfigureReadableText(PercentLabel, minSize: 13, maxSize: 20);
            _percentRect = PercentLabel.rectTransform;

            Image trackBg = Ui.MakeImage("ProgressTrack", ProgressCardRect, TrackFill, round: false, raycast: false);
            ProgressTrackRect = trackBg.rectTransform;
            _gaugeMat = UiShaders.CreateLiquidGauge(Vermilion, CoralGold, segments: 0, animatedLiquid: true);
            bool hasShaderGauge = _gaugeMat != null && _gaugeMat != Graphic.defaultGraphicMaterial;
            if (hasShaderGauge)
            {
                trackBg.material = _gaugeMat;
                trackBg.color = Color.white;
            }

            _progressInnerRect = Ui.Fill("ProgressInner", ProgressTrackRect);
            FillInset(_progressInnerRect, 2f, 2f, 2f, 2f);
            _progressInnerRect.gameObject.AddComponent<RectMask2D>();

            Image fillImg = Ui.MakeImage("ProgressFill", _progressInnerRect, hasShaderGauge ? Color.clear : Vermilion, round: false, raycast: false);
            ProgressFillRect = fillImg.rectTransform;
            ProgressFillRect.anchorMin = Vector2.zero;
            ProgressFillRect.anchorMax = new Vector2(0.68f, 1f);
            ProgressFillRect.offsetMin = Vector2.zero;
            ProgressFillRect.offsetMax = Vector2.zero;

            SetTipIndex(deterministicTipIndex);
            SetModeTitle(string.IsNullOrWhiteSpace(modeTitle) ? DefaultModeTitle : modeTitle);
            SetProgress(progress);
            RefreshLayout();
        }

        static void BuildFaceoffRow(RectTransform logoFrame, Font displayFont)
        {
            RectTransform faceoff = Ui.Panel("Faceoff5v5", logoFrame, new Vector2(0f, 0f), new Vector2(1f, 0.58f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

            string localKid = string.IsNullOrWhiteSpace(AppSession.Current.SelectedCharacter)
                ? DefaultBlueSquad[0]
                : AppSession.Current.SelectedCharacter.ToLowerInvariant();
            string[] blueIds = BuildBlueSquadWithLocal(localKid);

            RectTransform blueBand = Ui.Panel("BlueSquad", faceoff, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            BuildFramelessSquadStrip(blueBand, blueIds, AllyBlue, isRightSide: false);

            // Center editorial VS marker
            RectTransform vsBox = Ui.Panel("VsBadge", faceoff, new Vector2(0.45f, 0.16f), new Vector2(0.55f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image vsSlash = Ui.MakeImage("VsSlash", vsBox, Vermilion, round: false, raycast: false);
            FillInset(vsSlash.rectTransform, 6f, 14f, 6f, 14f);
            Material vsMat = UiShaders.CreateMetallicCta(Vermilion, VermilionDeep, cornerCut: 6f);
            if (vsMat != null && vsMat != Graphic.defaultGraphicMaterial)
            {
                vsSlash.material = vsMat;
                vsSlash.color = Color.white;
            }
            Text vsTxt = Ui.MakeText("VsLabel", vsBox, "VS", 18, Paper, TextAnchor.MiddleCenter, displayFont);
            Ui.ClearOutline(vsTxt);
            ConfigureReadableText(vsTxt, minSize: 11, maxSize: 18);
            FillFullBleed(vsTxt.rectTransform);

            RectTransform redBand = Ui.Panel("RedSquad", faceoff, new Vector2(0.55f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            BuildFramelessSquadStrip(redBand, DefaultRedSquad, EnemyRed, isRightSide: true);
        }

        static string[] BuildBlueSquadWithLocal(string localKid)
        {
            var result = new string[5];
            result[0] = localKid;
            int idx = 1;
            for (int i = 0; i < DefaultBlueSquad.Length && idx < 5; i++)
            {
                if (string.Equals(DefaultBlueSquad[i], localKid, StringComparison.OrdinalIgnoreCase)) continue;
                result[idx++] = DefaultBlueSquad[i];
            }
            if (idx < 5) result[idx] = "inggo";
            return result;
        }

        static void BuildFramelessSquadStrip(RectTransform parent, string[] kidIds, Color teamHairline, bool isRightSide)
        {
            var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.padding = new RectOffset(4, 4, 2, 2);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            for (int i = 0; i < kidIds.Length; i++)
            {
                string id = kidIds[i];
                var slotGo = new GameObject($"Faceoff_{id}", typeof(RectTransform));
                slotGo.transform.SetParent(parent, false);

                // Subtle ground-anchor obsidian pedestal haze + crisp 2 px team hairline (frameless portrait above!)
                Image floorLine = Ui.MakeImage("TeamRule", slotGo.transform, teamHairline, round: false, raycast: false);
                floorLine.rectTransform.anchorMin = new Vector2(0.08f, 0f);
                floorLine.rectTransform.anchorMax = new Vector2(0.92f, 0f);
                floorLine.rectTransform.pivot = new Vector2(0.5f, 0f);
                floorLine.rectTransform.anchoredPosition = Vector2.zero;
                floorLine.rectTransform.sizeDelta = new Vector2(0f, 2.5f);

                Sprite bust = KalyeahResources.KidBustSprite(id);
                if (bust != null)
                {
                    Image portrait = Ui.MakeImage("Bust", slotGo.transform, Color.white, round: false, raycast: false);
                    portrait.sprite = bust;
                    portrait.preserveAspect = true;
                    portrait.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                    portrait.rectTransform.anchorMax = new Vector2(1f, 1f);
                    portrait.rectTransform.offsetMin = Vector2.zero;
                    portrait.rectTransform.offsetMax = Vector2.zero;
                    if (isRightSide)
                        portrait.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                }
                else
                {
                    Image standIn = Ui.MakeImage("StandIn", slotGo.transform, new Color(teamHairline.r, teamHairline.g, teamHairline.b, 0.28f), round: true, raycast: false);
                    standIn.rectTransform.anchorMin = new Vector2(0.18f, 0.14f);
                    standIn.rectTransform.anchorMax = new Vector2(0.82f, 0.88f);
                    standIn.rectTransform.offsetMin = Vector2.zero;
                    standIn.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        public void SetModeTitle(string modeTitle)
        {
            ModeTitle = string.IsNullOrWhiteSpace(modeTitle) ? DefaultModeTitle : modeTitle.Trim();
            if (ModeTitleLabel != null)
                ModeTitleLabel.text = ModeTitle;
            RefreshModeBadgeWidth();
            RefreshTipCopy();
        }

        public void SetTipIndex(int index)
        {
            if (CuratedTips.Length == 0) return;
            _tipIndex = ((index % CuratedTips.Length) + CuratedTips.Length) % CuratedTips.Length;
            RefreshTipCopy();
        }

        public void NextTip()
        {
            if (CuratedTips.Length == 0) return;
            SetTipIndex(_tipIndex + 1);
        }

        public void SetProgress(float progress01, string statusOverride = null)
        {
            Progress = Mathf.Clamp01(progress01);
            if (statusOverride != null)
                _statusOverride = statusOverride;

            int pct = Mathf.RoundToInt(Progress * 100f);
            if (PercentLabel != null)
                PercentLabel.text = string.Format(CultureInfo.InvariantCulture, "{0}%", pct);

            if (StatusLabel != null)
            {
                if (!string.IsNullOrWhiteSpace(_statusOverride))
                {
                    StatusLabel.text = _statusOverride;
                }
                else
                {
                    bool fil = IsFilipinoLocale();
                    if (Progress >= 0.995f)
                        StatusLabel.text = fil ? "HANDA NA ANG KALYE!" : "STREET READY!";
                    else
                        StatusLabel.text = fil ? "INIHAHANDA ANG KALYE..." : "LOADING BARANGAY...";
                }
            }

            float visualFill = Progress <= 0.001f ? 0f : Mathf.Clamp(Progress, 0.04f, 1f);
            if (_gaugeMat != null && _gaugeMat != Graphic.defaultGraphicMaterial)
            {
                if (_gaugeMat.HasProperty(UiShaders.PropFill)) _gaugeMat.SetFloat(UiShaders.PropFill, visualFill);
                if (_gaugeMat.HasProperty(UiShaders.PropTrailFill)) _gaugeMat.SetFloat(UiShaders.PropTrailFill, visualFill);
                UiShaders.SyncReduceMotion(_gaugeMat);
            }

            if (ProgressFillRect != null)
            {
                ProgressFillRect.anchorMin = Vector2.zero;
                ProgressFillRect.anchorMax = new Vector2(visualFill, 1f);
                ProgressFillRect.offsetMin = Vector2.zero;
                ProgressFillRect.offsetMax = Vector2.zero;
            }
        }

        public void RefreshLayout()
        {
            if (_disposed || Root == null) return;

            if (SafeArea != null)
                SafeArea.Apply();

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
            float scale = Mathf.Clamp(safe.y / 684f, 0.78f, 1f);
            float contentW = Mathf.Clamp(safe.x - 96f, 480f, 820f);

            if (LogoFrameRect != null)
            {
                LogoFrameRect.sizeDelta = new Vector2(contentW, 192f * scale);
                LogoFrameRect.anchoredPosition = new Vector2(0f, 186f * scale);
            }

            RefreshModeBadgeWidth(scale, contentW);
            if (ModeBadgeRect != null)
                ModeBadgeRect.anchoredPosition = new Vector2(0f, 54f * scale);

            if (TipCardRect != null)
            {
                float tipH = 148f * scale;
                TipCardRect.sizeDelta = new Vector2(contentW, tipH);
                TipCardRect.anchoredPosition = new Vector2(0f, -62f * scale);

                float iconSide = Mathf.Round(66f * scale);
                float leftPad = 20f;
                float textLeft = leftPad + iconSide + 16f;
                float rightPad = 20f;

                if (_tipIconWellRect != null)
                {
                    _tipIconWellRect.sizeDelta = new Vector2(iconSide, iconSide);
                    _tipIconWellRect.anchoredPosition = new Vector2(leftPad, 0f);
                }

                if (_tipHeaderRect != null)
                {
                    _tipHeaderRect.anchorMin = new Vector2(0f, 1f);
                    _tipHeaderRect.anchorMax = new Vector2(1f, 1f);
                    _tipHeaderRect.pivot = new Vector2(0.5f, 1f);
                    _tipHeaderRect.offsetMin = new Vector2(textLeft, -38f * scale);
                    _tipHeaderRect.offsetMax = new Vector2(-rightPad, -10f * scale);
                }

                if (_tipBodyRect != null)
                {
                    _tipBodyRect.anchorMin = Vector2.zero;
                    _tipBodyRect.anchorMax = Vector2.one;
                    _tipBodyRect.pivot = new Vector2(0.5f, 0.5f);
                    _tipBodyRect.offsetMin = new Vector2(textLeft, 10f * scale);
                    _tipBodyRect.offsetMax = new Vector2(-rightPad, -42f * scale);
                }
            }

            if (ProgressCardRect != null)
            {
                float progH = 90f * scale;
                ProgressCardRect.sizeDelta = new Vector2(contentW, progH);
                ProgressCardRect.anchoredPosition = new Vector2(0f, -206f * scale);

                if (_statusRect != null)
                {
                    _statusRect.anchorMin = new Vector2(0f, 1f);
                    _statusRect.anchorMax = new Vector2(0.75f, 1f);
                    _statusRect.pivot = new Vector2(0f, 1f);
                    _statusRect.offsetMin = new Vector2(18f, -38f * scale);
                    _statusRect.offsetMax = new Vector2(-8f, -8f * scale);
                }

                if (_percentRect != null)
                {
                    _percentRect.anchorMin = new Vector2(0.77f, 1f);
                    _percentRect.anchorMax = new Vector2(1f, 1f);
                    _percentRect.pivot = new Vector2(1f, 1f);
                    _percentRect.offsetMin = new Vector2(8f, -38f * scale);
                    _percentRect.offsetMax = new Vector2(-18f, -8f * scale);
                }

                if (ProgressTrackRect != null)
                {
                    ProgressTrackRect.anchorMin = new Vector2(0f, 0f);
                    ProgressTrackRect.anchorMax = new Vector2(1f, 0f);
                    ProgressTrackRect.pivot = new Vector2(0.5f, 0f);
                    ProgressTrackRect.offsetMin = new Vector2(16f, 12f * scale);
                    ProgressTrackRect.offsetMax = new Vector2(-16f, 40f * scale);
                }
            }

            RefreshTipCopy();
            SetProgress(Progress);

            if (BackdropRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(BackdropRect);
            if (LogoFrameRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(LogoFrameRect);
            if (_tipIconWellRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tipIconWellRect);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (SafeArea != null)
                SafeArea.Changed -= OnSafeAreaChanged;

            if (_layoutHook != null)
                _layoutHook.Owner = null;

            if (_ownsCanvas && Canvas != null)
                KalyeahResources.Kill(Canvas.gameObject);
            else if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        void SetCanvasAlpha(float alpha, bool blockRaycasts)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Clamp01(alpha);
                _canvasGroup.blocksRaycasts = blockRaycasts;
            }
            if (_scrimImage != null)
                _scrimImage.raycastTarget = blockRaycasts;
        }

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        void RefreshModeBadgeWidth(float scale = 1f, float contentW = 820f)
        {
            if (ModeBadgeRect == null) return;
            if (contentW <= 0f)
            {
                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
                scale = Mathf.Clamp(safe.y / 684f, 0.78f, 1f);
                contentW = Mathf.Clamp(safe.x - 96f, 480f, 820f);
            }

            int charCount = !string.IsNullOrEmpty(ModeTitle) ? ModeTitle.Length : 18;
            float estWidth = charCount * 15f + 56f;
            float badgeW = Mathf.Clamp(estWidth, 320f, contentW - 40f);
            ModeBadgeRect.sizeDelta = new Vector2(badgeW, 46f * scale);
        }

        void RefreshTipCopy()
        {
            bool fil = IsFilipinoLocale();
            if (TipHeaderLabel != null)
                TipHeaderLabel.text = fil
                    ? $"{LoreCodexData.AteLornaRoleTag}  \u00B7  TIP SA KALYE"
                    : $"{LoreCodexData.AteLornaRoleTag}  \u00B7  SARI-SARI TIP";

            if (TipBodyLabel != null && CuratedTips.Length > 0)
            {
                string intro = LoreCodexData.ResolveAteLornaIntro(ModeTitle ?? "Barangay", _tipIndex).Text;
                TipEntry entry = CuratedTips[Mathf.Clamp(_tipIndex, 0, CuratedTips.Length - 1)];
                string gameplayTip = fil && !string.IsNullOrEmpty(entry.Fil) ? entry.Fil : entry.En;
                TipBodyLabel.text = $"\"{intro}\"\n{gameplayTip}";
            }
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

        static int ResolveTipIndexForMode(string modeTitle, bool advanceCursor)
        {
            if (!string.IsNullOrEmpty(modeTitle))
            {
                string upper = modeTitle.ToUpperInvariant();
                for (int i = 0; i < CuratedTips.Length; i++)
                {
                    if (upper.IndexOf(CuratedTips[i].MatchToken, StringComparison.Ordinal) >= 0)
                    {
                        if (advanceCursor)
                            s_tipCursor = (i + 1) % CuratedTips.Length;
                        return i;
                    }
                }
            }

            int chosen = s_tipCursor % CuratedTips.Length;
            if (advanceCursor)
                s_tipCursor = (s_tipCursor + 1) % CuratedTips.Length;
            return chosen;
        }

        static bool IsFilipinoLocale()
        {
            string lang = SettingsStore.Current.Language;
            return string.Equals(lang, "fil", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lang, "tl", StringComparison.OrdinalIgnoreCase);
        }

        static void ConfigureReadableText(Text label, int minSize, int maxSize)
        {
            if (label == null) return;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, minSize);
            label.resizeTextMaxSize = Mathf.Max(label.resizeTextMinSize, maxSize);
        }

        static void FillFullBleed(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void FillInset(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
