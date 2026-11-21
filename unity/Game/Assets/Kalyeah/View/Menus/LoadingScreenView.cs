using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Full-bleed barangay Komiks async scene-transition curtain (<c>Title &lt;-&gt; Match</c>) and deterministic
    /// dual-aspect preview view. Splits full-bleed art + twilight scrim on <see cref="Root"/> from inset brand,
    /// mode badge, gameplay tip card, and progress bar under <see cref="SafeRoot"/> (<see cref="SafeArea"/>).
    /// </summary>
    public sealed class LoadingScreenView : IDisposable
    {
        public const int CurtainSortingOrder = 500;
        public const string DefaultModeTitle = "AGAWANG BASE \u00B7 5V5";
        public const string HomeModeTitle = "BARANGAY HOME";

        static readonly Color FallbackBackdropColor = new Color(0.02f, 0.035f, 0.07f, 1f);
        static readonly Color BackdropTintColor = Color.white;
        static readonly Color ScrimTintColor = new Color(0.02f, 0.035f, 0.07f, 0.68f);
        static readonly Color HorizonGlowColor = new Color(0.961f, 0.725f, 0.227f, 0.08f);
        static readonly Color BadgeTop = new Color(0.115f, 0.175f, 0.285f, 0.94f);
        static readonly Color BadgeBottom = new Color(0.052f, 0.085f, 0.155f, 0.97f);
        static readonly Color TrackTop = new Color(0.024f, 0.044f, 0.082f, 0.98f);
        static readonly Color TrackBottom = new Color(0.056f, 0.092f, 0.160f, 0.98f);
        static readonly Color TrackBorder = new Color(0.76f, 0.88f, 0.96f, 0.25f);

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
                "Every kid has identical movement and health — pick your favorite barkada style and tune your 3-superpower loadout!",
                "Pantay ang bilis at lakas ng bawat bata — piliin ang paborito mong porma at ayusin ang 3 superpower mo!"),
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
                // Yield one full frame first so the Komiks loading curtain paints before scene/asset construction begins.
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

        /// <summary>
        /// Transitions to <paramref name="sceneName"/> behind a full-bleed <c>DontDestroyOnLoad</c> Komiks loading curtain
        /// in PlayMode (yielding 1 frame before <see cref="SceneManager.LoadSceneAsync(string)"/>), or synchronously in EditMode.
        /// </summary>
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

        /// <summary>Signals that the destination scene has finished building its world/UI and the loading curtain may fade out.</summary>
        public static void CompleteAndHide()
        {
            if (s_runner != null)
                s_runner.SignalSceneReady();
        }

        /// <summary>Immediately hides any active runtime loading curtain without waiting for a fade.</summary>
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

            // 1. Full-bleed Backdrop + EnvelopeParent art + full-bleed Scrim on Root (covers 100% of cutouts & gesture bar)
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

            Image glowImg = Ui.MakeImage("HorizonGlow", Root, HorizonGlowColor, round: false, raycast: false);
            RectTransform glowRect = glowImg.rectTransform;
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = new Vector2(1f, 0.38f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            // 2. SafeArea child holding all readable Komiks content & progress bar
            SafeRoot = Ui.Fill("SafeArea", Root);
            SafeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
            SafeArea.Changed += OnSafeAreaChanged;

            Font displayFont = KalyeahResources.DisplayFont();
            Font uiFont = KalyeahResources.UiFont();

            // 2A. Brand Lockup (FitInParent)
            LogoFrameRect = Ui.Panel(
                "LogoFrame",
                SafeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 194f),
                new Vector2(440f, 132f));

            Texture2D logoTex = KalyeahResources.Texture("ui/title_logo")
                ?? KalyeahResources.Texture("ui/kalyeah_logo")
                ?? KalyeahResources.Texture("ui/brand/kalyeah_wordmark")
                ?? KalyeahResources.Texture("ui/brand/kalyeah_crest");
            if (logoTex != null)
            {
                var logoGo = new GameObject("TitleLogo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                logoGo.transform.SetParent(LogoFrameRect, false);
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
                Text fallbackLogo = Ui.MakeText("TitleLogoFallback", LogoFrameRect, "KALYEAH!", 48, Ui.Cream, TextAnchor.MiddleCenter, displayFont);
                ConfigureReadableText(fallbackLogo, minSize: 24, maxSize: 48);
                FillInset(fallbackLogo.rectTransform, 8f, 4f, 8f, 4f);
            }

            // 2B. Mode Badge Pill (tactical chamfered plate with gold border & accent pips)
            Image badgeBg = Ui.MakeSliced(
                "ModeBadge",
                SafeRoot,
                MenuArt.ChamferCard(BadgeTop, BadgeBottom, MenuArt.GoldBorder, 12, 2, 0.30f),
                Color.white,
                raycast: false);
            ModeBadgeRect = badgeBg.rectTransform;
            ModeBadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            ModeBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            ModeBadgeRect.pivot = new Vector2(0.5f, 0.5f);
            Ui.MakeAccentRail("LeftPip", ModeBadgeRect, MenuArt.Gold, width: 3f, verticalInset: 12f);
            Ui.MakeAccentRail("RightPip", ModeBadgeRect, MenuArt.Gold, width: 3f, verticalInset: 12f, rightSide: true);

            ModeTitleLabel = Ui.MakeText("ModeTitle", ModeBadgeRect, "", 24, MenuArt.GoldLight, TextAnchor.MiddleCenter, displayFont);
            ConfigureReadableText(ModeTitleLabel, minSize: 14, maxSize: 24);
            FillInset(ModeTitleLabel.rectTransform, 22f, 6f, 22f, 6f);

            // 2C. Barangay Gameplay Tip Card
            Image tipBg = Ui.MakeSliced("TipCard", SafeRoot, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            TipCardRect = tipBg.rectTransform;
            TipCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            TipCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            TipCardRect.pivot = new Vector2(0.5f, 0.5f);
            Ui.MakeAccentRail("TipRail", TipCardRect, MenuArt.Gold, width: 4f, verticalInset: 16f);

            Image iconWell = Ui.MakeSliced("TipIconWell", TipCardRect, MenuArt.StatChip(10), Color.white, raycast: false);
            _tipIconWellRect = iconWell.rectTransform;
            _tipIconWellRect.anchorMin = new Vector2(0f, 0.5f);
            _tipIconWellRect.anchorMax = new Vector2(0f, 0.5f);
            _tipIconWellRect.pivot = new Vector2(0f, 0.5f);
            Ui.MakeSliced("WellRim", _tipIconWellRect, MenuArt.ChamferOutline(MenuArt.GoldBorder, 10, 1), Color.white, raycast: false);

            Texture2D tipIconTex = KalyeahResources.Texture("ui/tips_icon");
            if (tipIconTex != null)
            {
                var iconContainer = Ui.Fill("TipIconPadding", _tipIconWellRect);
                FillInset(iconContainer, 8f, 8f, 8f, 8f);
                var iconGo = new GameObject("TipIcon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                iconGo.transform.SetParent(iconContainer, false);
                var iconRaw = iconGo.GetComponent<RawImage>();
                iconRaw.texture = tipIconTex;
                iconRaw.color = Color.white;
                iconRaw.raycastTarget = false;
                var iconFitter = iconGo.GetComponent<AspectRatioFitter>();
                iconFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                iconFitter.aspectRatio = tipIconTex.width / Mathf.Max(1f, (float)tipIconTex.height);
            }
            else
            {
                Text bulbFallback = Ui.MakeText("TipGlyph", _tipIconWellRect, "!", 28, MenuArt.GoldLight, TextAnchor.MiddleCenter, displayFont);
                ConfigureReadableText(bulbFallback, minSize: 18, maxSize: 28);
                FillFullBleed(bulbFallback.rectTransform);
            }

            TipHeaderLabel = Ui.MakeText("TipHeader", TipCardRect, "", 20, MenuArt.GoldLight, TextAnchor.MiddleLeft, displayFont);
            ConfigureReadableText(TipHeaderLabel, minSize: 18, maxSize: 20);
            Ui.ClearOutline(TipHeaderLabel);
            _tipHeaderRect = TipHeaderLabel.rectTransform;

            TipBodyLabel = Ui.MakeText("TipBody", TipCardRect, "", 20, MenuArt.Cream, TextAnchor.MiddleLeft, uiFont);
            ConfigureReadableText(TipBodyLabel, minSize: 14, maxSize: 21);
            TipBodyLabel.lineSpacing = 1.08f;
            Ui.ClearOutline(TipBodyLabel);
            _tipBodyRect = TipBodyLabel.rectTransform;

            // 2D. Tactical Progress Bar Card
            Image progCardBg = Ui.MakeSliced("ProgressCard", SafeRoot, MenuArt.ChamferGlass(16), Color.white, raycast: false);
            ProgressCardRect = progCardBg.rectTransform;
            ProgressCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            ProgressCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            ProgressCardRect.pivot = new Vector2(0.5f, 0.5f);
            Ui.MakeCornerBrackets("ProgressBrackets", ProgressCardRect, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.45f));

            StatusLabel = Ui.MakeText("StatusLabel", ProgressCardRect, "", 20, MenuArt.Cream, TextAnchor.MiddleLeft, displayFont);
            ConfigureReadableText(StatusLabel, minSize: 18, maxSize: 20);
            _statusRect = StatusLabel.rectTransform;

            PercentLabel = Ui.MakeText("PercentLabel", ProgressCardRect, "", 22, MenuArt.GoldLight, TextAnchor.MiddleRight, displayFont);
            ConfigureReadableText(PercentLabel, minSize: 18, maxSize: 22);
            _percentRect = PercentLabel.rectTransform;

            Image trackBg = Ui.MakeSliced(
                "ProgressTrack",
                ProgressCardRect,
                MenuArt.Round(TrackTop, TrackBottom, TrackBorder, 8, 1, 0.12f),
                Color.white,
                raycast: false);
            ProgressTrackRect = trackBg.rectTransform;

            _progressInnerRect = Ui.Fill("ProgressInner", ProgressTrackRect);
            FillInset(_progressInnerRect, 4f, 4f, 4f, 4f);
            _progressInnerRect.gameObject.AddComponent<RectMask2D>();

            Image fillImg = Ui.MakeSliced("ProgressFill", _progressInnerRect, MenuArt.AccentRail(6), MenuArt.Gold, raycast: false);
            ProgressFillRect = fillImg.rectTransform;
            ProgressFillRect.anchorMin = Vector2.zero;
            ProgressFillRect.anchorMax = new Vector2(0.68f, 1f);
            ProgressFillRect.offsetMin = Vector2.zero;
            ProgressFillRect.offsetMax = Vector2.zero;
            Ui.AddSheen(ProgressFillRect);

            SetTipIndex(deterministicTipIndex);
            SetModeTitle(string.IsNullOrWhiteSpace(modeTitle) ? DefaultModeTitle : modeTitle);
            SetProgress(progress);
            RefreshLayout();
        }

        public void SetModeTitle(string modeTitle)
        {
            ModeTitle = string.IsNullOrWhiteSpace(modeTitle) ? DefaultModeTitle : modeTitle.Trim();
            if (ModeTitleLabel != null)
                ModeTitleLabel.text = ModeTitle;
            RefreshModeBadgeWidth();
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

            if (ProgressFillRect != null)
            {
                float visualFill = Progress <= 0.001f ? 0f : Mathf.Clamp(Progress, 0.04f, 1f);
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
            float contentW = Mathf.Clamp(safe.x - 96f, 480f, 800f);

            if (LogoFrameRect != null)
            {
                float logoW = Mathf.Min(440f * scale, contentW - 40f);
                float logoH = 132f * scale;
                LogoFrameRect.sizeDelta = new Vector2(logoW, logoH);
                LogoFrameRect.anchoredPosition = new Vector2(0f, 194f * scale);
            }

            RefreshModeBadgeWidth(scale, contentW);
            if (ModeBadgeRect != null)
                ModeBadgeRect.anchoredPosition = new Vector2(0f, 80f * scale);

            if (TipCardRect != null)
            {
                float tipH = 152f * scale;
                TipCardRect.sizeDelta = new Vector2(contentW, tipH);
                TipCardRect.anchoredPosition = new Vector2(0f, -45f * scale);

                float iconSide = Mathf.Round(68f * scale);
                float leftPad = 22f;
                float textLeft = leftPad + iconSide + 18f;
                float rightPad = 22f;

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
                    _tipHeaderRect.offsetMin = new Vector2(textLeft, -42f * scale);
                    _tipHeaderRect.offsetMax = new Vector2(-rightPad, -12f * scale);
                }

                if (_tipBodyRect != null)
                {
                    _tipBodyRect.anchorMin = Vector2.zero;
                    _tipBodyRect.anchorMax = Vector2.one;
                    _tipBodyRect.pivot = new Vector2(0.5f, 0.5f);
                    _tipBodyRect.offsetMin = new Vector2(textLeft, 12f * scale);
                    _tipBodyRect.offsetMax = new Vector2(-rightPad, -48f * scale);
                }
            }

            if (ProgressCardRect != null)
            {
                float progH = 92f * scale;
                ProgressCardRect.sizeDelta = new Vector2(contentW, progH);
                ProgressCardRect.anchoredPosition = new Vector2(0f, -193f * scale);

                if (_statusRect != null)
                {
                    _statusRect.anchorMin = new Vector2(0f, 1f);
                    _statusRect.anchorMax = new Vector2(0.75f, 1f);
                    _statusRect.pivot = new Vector2(0f, 1f);
                    _statusRect.offsetMin = new Vector2(18f, -40f * scale);
                    _statusRect.offsetMax = new Vector2(-8f, -8f * scale);
                }

                if (_percentRect != null)
                {
                    _percentRect.anchorMin = new Vector2(0.77f, 1f);
                    _percentRect.anchorMax = new Vector2(1f, 1f);
                    _percentRect.pivot = new Vector2(1f, 1f);
                    _percentRect.offsetMin = new Vector2(8f, -40f * scale);
                    _percentRect.offsetMax = new Vector2(-18f, -8f * scale);
                }

                if (ProgressTrackRect != null)
                {
                    ProgressTrackRect.anchorMin = new Vector2(0f, 0f);
                    ProgressTrackRect.anchorMax = new Vector2(1f, 0f);
                    ProgressTrackRect.pivot = new Vector2(0.5f, 0f);
                    ProgressTrackRect.offsetMin = new Vector2(16f, 10f * scale);
                    ProgressTrackRect.offsetMax = new Vector2(-16f, 44f * scale);
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

        void RefreshModeBadgeWidth(float scale = 1f, float contentW = 800f)
        {
            if (ModeBadgeRect == null) return;
            if (contentW <= 0f)
            {
                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(Canvas);
                scale = Mathf.Clamp(safe.y / 684f, 0.78f, 1f);
                contentW = Mathf.Clamp(safe.x - 96f, 480f, 800f);
            }

            int charCount = !string.IsNullOrEmpty(ModeTitle) ? ModeTitle.Length : 18;
            float estWidth = charCount * 15.5f + 64f;
            float badgeW = Mathf.Clamp(estWidth, 340f, contentW - 40f);
            ModeBadgeRect.sizeDelta = new Vector2(badgeW, 54f * scale);
        }

        void RefreshTipCopy()
        {
            bool fil = IsFilipinoLocale();
            if (TipHeaderLabel != null)
                TipHeaderLabel.text = fil ? "TIP SA KALYE" : "KALYE TIP";

            if (TipBodyLabel != null && CuratedTips.Length > 0)
            {
                TipEntry entry = CuratedTips[Mathf.Clamp(_tipIndex, 0, CuratedTips.Length - 1)];
                TipBodyLabel.text = fil && !string.IsNullOrEmpty(entry.Fil) ? entry.Fil : entry.En;
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
            label.resizeTextMinSize = Mathf.Max(14, minSize);
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
