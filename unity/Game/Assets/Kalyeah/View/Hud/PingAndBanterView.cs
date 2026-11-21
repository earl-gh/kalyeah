using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    public enum TacticalPingKind
    {
        Attack = 0,
        Defend = 1,
        Rescue = 2,
        Danger = 3,
        OnMyWay = 4,
    }

    /// <summary>
    /// Self-contained 4-finger mobile tactical ping wheel (tap-to-ping / hold-for-5-wedge-radial-wheel with zero-cost
    /// center cancel and anti-spam cooldown) paired with a compact Taglish banter toast strip strictly outside the
    /// 45%x50% central combat sightbox (<c>docs/SPEC.md</c> §3 L39-41 + <c>docs/LORE.md</c>).
    /// Zero per-frame GC allocations; strictly inert in EditMode unless driven via <see cref="Tick"/>.
    /// </summary>
    public sealed class PingAndBanterView : IDisposable
    {
        public const int PingCount = 5;
        public const float HoldThresholdSeconds = 0.22f;
        public const float CooldownSeconds = 1.50f;
        public const float CancelRadiusCanvas = 36f;
        public const float WheelRadiusCanvas = 112f;
        public const float WheelWedgeOrbitCanvas = 74f;
        public const float DefaultBanterDuration = 3.20f;
        public const float BanterFadeInSeconds = 0.18f;
        public const float BanterFadeOutSeconds = 0.35f;

        const float TopOffsetCanvas = 68f;
        const float EdgeInsetCanvas = 14f;
        const float BanterHeightCanvas = 74f;
        const float QuickDragOpenPx = 22f;

        static readonly Color ColWheelBg = new Color(0.035f, 0.062f, 0.115f, 0.90f);
        static readonly Color ColWedgeIdle = new Color(0.095f, 0.148f, 0.232f, 0.94f);
        static readonly Color ColWedgeActive = new Color(0.20f, 0.28f, 0.42f, 0.98f);
        static readonly Color ColCancelActive = new Color(0.86f, 0.28f, 0.24f, 0.96f);
        static readonly Color ColCooldownTint = new Color(0.45f, 0.50f, 0.58f, 0.78f);

        // Colorblind-safe distinct accent palette + distinct geometric glyphs per ping wedge.
        static readonly Color[] s_pingColors =
        {
            new Color(0.96f, 0.44f, 0.22f, 1f), // Attack: bright orange-coral
            new Color(0.34f, 0.64f, 0.98f, 1f), // Defend: sky blue
            new Color(0.26f, 0.86f, 0.56f, 1f), // Rescue: emerald mint
            new Color(0.98f, 0.80f, 0.22f, 1f), // Danger: high-vis amber yellow
            new Color(0.24f, 0.86f, 0.94f, 1f), // OnMyWay: bright cyan-teal
        };

        static readonly MenuArt.GlyphKind[] s_pingGlyphs =
        {
            MenuArt.GlyphKind.Striker,  // Attack (lightning)
            MenuArt.GlyphKind.Defender, // Defend (shield)
            MenuArt.GlyphKind.Heart,    // Rescue (heart)
            MenuArt.GlyphKind.Diamond,  // Danger (hazard diamond)
            MenuArt.GlyphKind.Chevron,  // OnMyWay (directional chevron)
        };

        static readonly string[] s_pingShortLabels =
        {
            "ATTACK",
            "DEFEND",
            "RESCUE",
            "DANGER",
            "OMW",
        };

        static readonly string[] s_pingMessages =
        {
            "ATTACK · Sugod! Going in!",
            "DEFEND · Bantay base!",
            "RESCUE · Rescue me!",
            "DANGER · Ingat! Wait!",
            "ON MY WAY · Papunta na!",
        };

        // 5 evenly spaced radial angles (degrees): Attack top, OMW upper-right, Defend lower-right, Rescue lower-left, Danger upper-left.
        static readonly float[] s_wedgeAnglesDeg = { 90f, -54f, -126f, 162f, 18f };

        static readonly Vector2[] s_wedgeDirs = BuildWedgeDirs();

        // Pre-cached cooldown countdown strings so Update/Tick allocates zero managed strings.
        static readonly string[] s_cooldownStrings = { "", "1s", "2s", "3s", "4s", "5s" };

        static Vector2[] BuildWedgeDirs()
        {
            var dirs = new Vector2[PingCount];
            for (int i = 0; i < PingCount; i++)
            {
                float rad = s_wedgeAnglesDeg[i] * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }
            return dirs;
        }

        [ExecuteAlways]
        sealed class PingBanterLifecycle : MonoBehaviour
        {
            [NonSerialized] public PingAndBanterView Owner;
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

            void Update()
            {
                if (!Application.isPlaying) return;
                Owner?.Tick(Time.unscaledDeltaTime);
            }

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

        sealed class PingTriggerHit : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
        {
            [NonSerialized] public PingAndBanterView Owner;

            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointerDown(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandleDrag(eventData);
            public void OnPointerUp(PointerEventData eventData) => Owner?.HandlePointerUp(eventData);
            public void OnCancel(BaseEventData eventData) => Owner?.CancelPointer();
        }

        struct WedgeUi
        {
            public RectTransform Root;
            public Image Plate;
            public Image Ring;
            public Image Icon;
            public Text Label;
        }

        public readonly RectTransform Root;
        public readonly RectTransform PingButtonRect;
        public readonly RectTransform WheelRoot;
        public readonly RectTransform BanterRoot;

        public TacticalPingKind DefaultTapPing { get; set; } = TacticalPingKind.Attack;
        public bool IsWheelOpen => _wheelOpen;
        public bool IsBanterVisible => _banterVisible;
        public float CooldownRemaining => _cooldownRemaining;
        public TacticalPingKind? HighlightedPing => _highlightedPing;
        public TacticalPingKind? LastEmittedPing => _lastEmittedPing;
        public string ActiveSpeakerName => _activeSpeakerName;
        public string ActiveBanterText => _activeBanterText;

        public event Action<TacticalPingKind, string> PingEmitted;

        readonly Canvas _canvas;
        readonly bool _interactive;
        readonly Image _pingPlate;
        readonly Image _pingIcon;
        readonly Text _pingLabel;
        readonly Text _cooldownLabel;

        readonly WedgeUi[] _wedges = new WedgeUi[PingCount];
        readonly Image _cancelDisc;
        readonly Text _wheelCenterLabel;

        readonly CanvasGroup _banterGroup;
        readonly Image _banterRail;
        readonly Image _banterFallbackDisc;
        readonly Text _banterMonogram;
        readonly RawImage _banterPortrait;
        readonly AspectRatioFitter _banterPortraitFitter;
        readonly Text _banterSpeakerLabel;
        readonly Image _banterTagBg;
        readonly Text _banterTagLabel;
        readonly Text _banterLineLabel;

        bool _pointerDown;
        int _activePointerId = int.MinValue;
        Vector2 _pointerDownScreenPos;
        Vector2 _pointerScreenPos;
        float _holdElapsed;
        bool _wheelOpen;
        TacticalPingKind? _highlightedPing;
        TacticalPingKind? _lastEmittedPing;

        float _cooldownRemaining;
        int _lastCooldownCeil = -1;

        bool _banterVisible;
        float _banterElapsed;
        float _banterDuration;
        string _activeSpeakerName = string.Empty;
        string _activeBanterText = string.Empty;
        string _cachedPortraitId = string.Empty;
        bool _disposed;

        public static string GetPingShortLabel(TacticalPingKind kind)
        {
            int idx = (int)kind;
            return idx >= 0 && idx < s_pingShortLabels.Length ? s_pingShortLabels[idx] : s_pingShortLabels[0];
        }

        public static string GetPingMessage(TacticalPingKind kind)
        {
            int idx = (int)kind;
            return idx >= 0 && idx < s_pingMessages.Length ? s_pingMessages[idx] : s_pingMessages[0];
        }

        public static Vector2 GetWedgeDirection(TacticalPingKind kind)
        {
            int idx = (int)kind;
            return idx >= 0 && idx < s_wedgeDirs.Length ? s_wedgeDirs[idx] : Vector2.up;
        }

        public PingAndBanterView(Transform safeParent, Canvas canvas = null, bool interactive = true)
        {
            _canvas = canvas != null ? canvas : (safeParent != null ? safeParent.GetComponentInParent<Canvas>() : null);
            _interactive = interactive;

            Root = Ui.Fill("PingAndBanterRoot", safeParent);
            var life = Root.gameObject.AddComponent<PingBanterLifecycle>();
            life.Owner = this;

            // 1. Tactical Ping Trigger Button (>= 48 dp physical touch floor, strictly outside 45%x50% sightbox)
            var btnGo = new GameObject("PingButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(Root, false);
            PingButtonRect = btnGo.GetComponent<RectTransform>();
            var hitImg = btnGo.GetComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0.001f); // transparent >= 48 dp touch catcher
            hitImg.raycastTarget = interactive;

            var btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactive;

            if (interactive)
            {
                var hit = btnGo.AddComponent<PingTriggerHit>();
                hit.Owner = this;
                Ui.AddPressFeel(btnGo, 0.94f);
            }

            RectTransform visualBtn = Ui.Panel("Visual", PingButtonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            _pingPlate = Ui.MakeImage("Disc", visualBtn, ColWheelBg, round: true, raycast: false);
            Stretch(_pingPlate.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image rim = Ui.MakeSliced("Rim", visualBtn, MenuArt.RoundOutline(MenuArt.GoldBorder, 32, 2), Color.white, raycast: false);
            Stretch(rim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _pingIcon = Ui.MakeImage("Icon", visualBtn, MenuArt.GoldLight, round: false, raycast: false);
            _pingIcon.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Striker);
            _pingIcon.preserveAspect = true;
            Stretch(_pingIcon.rectTransform, new Vector2(0.24f, 0.34f), new Vector2(0.76f, 0.84f), Vector2.zero, Vector2.zero);

            _pingLabel = MakeFittedText("Label", visualBtn, "PING", 13, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 9);
            Stretch(_pingLabel.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero);

            _cooldownLabel = MakeFittedText("Cooldown", visualBtn, "", 20, MenuArt.GoldLight, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 11);
            Stretch(_cooldownLabel.rectTransform, new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.84f), Vector2.zero, Vector2.zero);
            _cooldownLabel.enabled = false;

            // 2. 5-Wedge Radial Tactical Ping Wheel (hidden until hold >= 0.22s)
            float wheelDiam = WheelRadiusCanvas * 2f;
            WheelRoot = Ui.Panel("PingWheel", Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(wheelDiam, wheelDiam));

            Image wheelBg = Ui.MakeImage("WheelBackdrop", WheelRoot, ColWheelBg, round: true, raycast: false);
            Stretch(wheelBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image wheelRim = Ui.MakeSliced("WheelRim", WheelRoot, MenuArt.RoundOutline(MenuArt.PanelBorder, 40, 2), Color.white, raycast: false);
            Stretch(wheelRim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            for (int i = 0; i < PingCount; i++)
            {
                Vector2 pos = s_wedgeDirs[i] * WheelWedgeOrbitCanvas;
                RectTransform wRect = Ui.Panel("Wedge_" + s_pingShortLabels[i], WheelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(68f, 58f));

                Image wPlate = Ui.MakeSliced("Plate", wRect, MenuArt.RoundSolid(14), ColWedgeIdle, raycast: false);
                Image wRing = Ui.MakeSliced("Ring", wRect, MenuArt.RoundOutline(Color.white, 14, 2), s_pingColors[i], raycast: false);

                Image wIcon = Ui.MakeImage("Icon", wRect, s_pingColors[i], round: false, raycast: false);
                wIcon.sprite = MenuArt.Glyph(s_pingGlyphs[i]);
                wIcon.preserveAspect = true;
                Stretch(wIcon.rectTransform, new Vector2(0.28f, 0.40f), new Vector2(0.72f, 0.88f), Vector2.zero, Vector2.zero);

                Text wLabel = MakeFittedText("Text", wRect, s_pingShortLabels[i], 12, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 9);
                Stretch(wLabel.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.40f), Vector2.zero, Vector2.zero);

                _wedges[i] = new WedgeUi
                {
                    Root = wRect,
                    Plate = wPlate,
                    Ring = wRing,
                    Icon = wIcon,
                    Label = wLabel,
                };
            }

            float cancelDiam = CancelRadiusCanvas * 2f;
            RectTransform cancelRt = Ui.Panel("CancelHub", WheelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cancelDiam, cancelDiam));
            _cancelDisc = Ui.MakeImage("Disc", cancelRt, ColWedgeIdle, round: true, raycast: false);
            Stretch(_cancelDisc.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image cancelIcon = Ui.MakeImage("CloseIcon", cancelRt, MenuArt.Cream, round: false, raycast: false);
            cancelIcon.sprite = MenuArt.CloseIcon();
            cancelIcon.preserveAspect = true;
            Stretch(cancelIcon.rectTransform, new Vector2(0.32f, 0.46f), new Vector2(0.68f, 0.80f), Vector2.zero, Vector2.zero);

            _wheelCenterLabel = MakeFittedText("CenterLabel", cancelRt, "CANCEL", 11, MenuArt.MutedText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 8);
            Stretch(_wheelCenterLabel.rectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.44f), Vector2.zero, Vector2.zero);

            WheelRoot.gameObject.SetActive(false);

            // 3. Compact Taglish Banter Toast Strip (strictly above 45%x50% center sightbox!)
            BanterRoot = Ui.Panel("BanterStrip", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(EdgeInsetCanvas, -TopOffsetCanvas), new Vector2(360f, BanterHeightCanvas));
            _banterGroup = BanterRoot.gameObject.AddComponent<CanvasGroup>();
            _banterGroup.blocksRaycasts = false;
            _banterGroup.interactable = false;
            _banterGroup.alpha = 0f;

            Ui.MakeSliced("CardBg", BanterRoot, MenuArt.ChamferGlass(12), Color.white, raycast: false);
            RectTransform railRt = Ui.MakeAccentRail("AccentRail", BanterRoot, MenuArt.Gold, width: 4f, verticalInset: 8f);
            _banterRail = railRt.GetComponent<Image>();

            // Speaker portrait thumbnail (RectMask2D + AspectRatioFitter EnvelopeParent -> zero portrait distortion)
            RectTransform portraitFrame = Ui.Panel("PortraitFrame", BanterRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(54f, 54f));
            Ui.MakeSliced("PortraitPlate", portraitFrame, MenuArt.StatChip(8), Color.white, raycast: false);

            RectTransform portraitMask = Ui.Fill("PortraitMask", portraitFrame);
            portraitMask.offsetMin = new Vector2(3f, 3f);
            portraitMask.offsetMax = new Vector2(-3f, -3f);
            portraitMask.gameObject.AddComponent<RectMask2D>();

            _banterFallbackDisc = Ui.MakeImage("FallbackDisc", portraitMask, MenuArt.Gold, round: true, raycast: false);
            Stretch(_banterFallbackDisc.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);

            _banterMonogram = MakeFittedText("Monogram", portraitMask, "AL", 20, MenuArt.Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 12);
            Ui.ClearOutline(_banterMonogram);
            Stretch(_banterMonogram.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var rawGo = new GameObject("PortraitRaw", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawGo.transform.SetParent(portraitMask, false);
            _banterPortrait = rawGo.GetComponent<RawImage>();
            _banterPortrait.raycastTarget = false;
            _banterPortrait.enabled = false;
            Stretch(_banterPortrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _banterPortraitFitter = rawGo.GetComponent<AspectRatioFitter>();
            _banterPortraitFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _banterPortraitFitter.aspectRatio = 1f;

            Ui.MakeSliced("PortraitRim", portraitFrame, MenuArt.ChamferOutline(MenuArt.GoldBorder, 8, 1), Color.white, raycast: false);

            // Header row: Speaker Name + micro-tag pill
            RectTransform textArea = Ui.Panel("TextArea", BanterRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            textArea.offsetMin = new Vector2(76f, 6f);
            textArea.offsetMax = new Vector2(-10f, -6f);

            _banterSpeakerLabel = MakeFittedText("SpeakerName", textArea, "", 17, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont(), minSize: 10);
            Stretch(_banterSpeakerLabel.rectTransform, new Vector2(0f, 0.62f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);

            RectTransform tagPill = Ui.Panel("TagPill", textArea, new Vector2(0.66f, 0.64f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _banterTagBg = Ui.MakeSliced("Bg", tagPill, MenuArt.RoundSolid(6), MenuArt.Gold, raycast: false);
            _banterTagLabel = MakeFittedText("TagText", tagPill, "", 12, MenuArt.Ink, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont(), minSize: 9);
            Ui.ClearOutline(_banterTagLabel);
            Stretch(_banterTagLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 1f), new Vector2(-4f, -1f));

            _banterLineLabel = Ui.MakeText("BanterLine", textArea, "", 15, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.UiFont());
            Ui.ClearOutline(_banterLineLabel);
            _banterLineLabel.resizeTextForBestFit = true;
            _banterLineLabel.resizeTextMinSize = 9;
            _banterLineLabel.resizeTextMaxSize = 15;
            _banterLineLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _banterLineLabel.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(_banterLineLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);

            BanterRoot.gameObject.SetActive(false);

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_disposed || Root == null) return;

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, _canvas);
            float btnSize = Mathf.Max(90f, minTouch);

            bool leftHanded = SettingsStore.Current != null && SettingsStore.Current.LeftHanded;

            // Keep Ping button strictly in upper perimeter outside the 45%x50% center sightbox
            // Top 25% of safeH is sightbox-free; clamp top offset so bottom of PingButton stays above sightbox top edge when possible.
            float maxTopFree = safe.y * 0.25f;
            float pingTopOffset = Mathf.Min(TopOffsetCanvas, Mathf.Max(4f, maxTopFree - btnSize - 4f));

            if (leftHanded)
            {
                PingButtonRect.anchorMin = new Vector2(0f, 1f);
                PingButtonRect.anchorMax = new Vector2(0f, 1f);
                PingButtonRect.pivot = new Vector2(0f, 1f);
                PingButtonRect.anchoredPosition = new Vector2(EdgeInsetCanvas, -pingTopOffset);
            }
            else
            {
                PingButtonRect.anchorMin = new Vector2(1f, 1f);
                PingButtonRect.anchorMax = new Vector2(1f, 1f);
                PingButtonRect.pivot = new Vector2(1f, 1f);
                PingButtonRect.anchoredPosition = new Vector2(-EdgeInsetCanvas, -pingTopOffset);
            }
            PingButtonRect.sizeDelta = new Vector2(btnSize, btnSize);

            // Banter strip sits on the opposite upper corner strictly ABOVE the 45%x50% center sightbox (top 25% of safeH)
            float banterTopOffset = Mathf.Min(TopOffsetCanvas, Mathf.Max(4f, maxTopFree - BanterHeightCanvas - 8f));
            float maxBanterW = Mathf.Max(220f, safe.x - btnSize - EdgeInsetCanvas * 3f - 16f);
            float banterW = Mathf.Min(Mathf.Clamp(safe.x * 0.38f, 280f, 388f), maxBanterW);

            if (leftHanded)
            {
                BanterRoot.anchorMin = new Vector2(1f, 1f);
                BanterRoot.anchorMax = new Vector2(1f, 1f);
                BanterRoot.pivot = new Vector2(1f, 1f);
                BanterRoot.anchoredPosition = new Vector2(-EdgeInsetCanvas, -banterTopOffset);
            }
            else
            {
                BanterRoot.anchorMin = new Vector2(0f, 1f);
                BanterRoot.anchorMax = new Vector2(0f, 1f);
                BanterRoot.pivot = new Vector2(0f, 1f);
                BanterRoot.anchoredPosition = new Vector2(EdgeInsetCanvas, -banterTopOffset);
            }
            BanterRoot.sizeDelta = new Vector2(banterW, BanterHeightCanvas);

            PositionWheelInsideSafe(safe, btnSize, pingTopOffset, leftHanded);
        }

        void PositionWheelInsideSafe(Vector2 safe, float btnSize, float pingTopOffset, bool leftHanded)
        {
            float halfW = safe.x * 0.5f;
            float halfH = safe.y * 0.5f;
            float margin = WheelRadiusCanvas + 8f;

            float desiredX = leftHanded
                ? (-halfW + EdgeInsetCanvas + btnSize * 0.5f + 36f)
                : (halfW - EdgeInsetCanvas - btnSize * 0.5f - 36f);
            float desiredY = halfH - pingTopOffset - btnSize * 0.5f - 36f;

            float clampedX = Mathf.Clamp(desiredX, -halfW + margin, halfW - margin);
            float clampedY = Mathf.Clamp(desiredY, -halfH + margin, halfH - margin);
            WheelRoot.anchoredPosition = new Vector2(clampedX, clampedY);
        }

        public bool TryEmitPing(TacticalPingKind kind)
        {
            if (_disposed || _cooldownRemaining > 0f)
                return false;

            _cooldownRemaining = CooldownSeconds;
            _lastEmittedPing = kind;
            UpdateCooldownVisual();
            PingEmitted?.Invoke(kind, GetPingMessage(kind));
            return true;
        }

        public void OpenWheelForTesting()
        {
            if (_disposed) return;
            OpenWheelInternal();
        }

        public void CloseWheel()
        {
            _wheelOpen = false;
            _highlightedPing = null;
            _pointerDown = false;
            _activePointerId = int.MinValue;
            if (WheelRoot != null)
                WheelRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Deterministic helper for unit tests and controller input: simulates holding the ping button for
        /// <paramref name="holdSeconds"/>, dragging along <paramref name="wheelLocalOffset"/> (magnitude &lt;
        /// <see cref="CancelRadiusCanvas"/> cancels at zero cost), and releasing.
        /// </summary>
        public bool SimulateHoldDragRelease(Vector2 wheelLocalOffset, float holdSeconds = 0.25f)
        {
            if (_disposed) return false;
            if (holdSeconds < HoldThresholdSeconds)
                return TryEmitPing(DefaultTapPing);

            OpenWheelInternal();
            UpdateHighlightFromLocalDelta(wheelLocalOffset);
            TacticalPingKind? chosen = _highlightedPing;
            CloseWheel();
            return chosen.HasValue && TryEmitPing(chosen.Value);
        }

        public void ShowBanter(in BanterLine line, float durationSeconds = DefaultBanterDuration)
        {
            if (_disposed || BanterRoot == null) return;

            _activeSpeakerName = line.SpeakerName ?? string.Empty;
            _activeBanterText = line.Text ?? string.Empty;
            _banterElapsed = 0f;
            _banterDuration = Mathf.Max(0.8f, durationSeconds);
            _banterVisible = true;

            Color accent = AccentForRelation(line.Relation);
            if (_banterRail != null) _banterRail.color = accent;
            if (_banterTagBg != null) _banterTagBg.color = accent;
            if (_banterFallbackDisc != null) _banterFallbackDisc.color = accent;

            if (_banterSpeakerLabel != null)
                _banterSpeakerLabel.text = _activeSpeakerName.ToUpperInvariant();
            if (_banterTagLabel != null)
                _banterTagLabel.text = string.IsNullOrEmpty(line.Tag) ? "BARANGAY" : line.Tag.ToUpperInvariant();
            if (_banterLineLabel != null)
                _banterLineLabel.text = _activeBanterText;

            UpdatePortrait(line.SpeakerId, _activeSpeakerName);

            BanterRoot.gameObject.SetActive(true);
            bool instant = !Application.isPlaying || (SettingsStore.Current != null && SettingsStore.Current.ReduceMotion);
            _banterGroup.alpha = instant ? 1f : 0f;
        }

        public BanterLine ShowMatchStartBanter(IReadOnlyList<string> allies, IReadOnlyList<string> enemies, int seed)
        {
            BanterLine line = LoreCodexData.ResolveMatchStartBanter(allies, enemies, seed);
            ShowBanter(in line);
            return line;
        }

        public BanterLine ShowTagBanter(string attackerId, string victimId, int seed)
        {
            BanterLine line = LoreCodexData.ResolveTagBanter(attackerId, victimId, seed);
            ShowBanter(in line);
            return line;
        }

        public BanterLine ShowRescueBanter(string rescuerId, string rescuedId, int seed)
        {
            BanterLine line = LoreCodexData.ResolveRescueBanter(rescuerId, rescuedId, seed);
            ShowBanter(in line);
            return line;
        }

        public void HideBanter()
        {
            _banterVisible = false;
            _banterElapsed = 0f;
            if (_banterGroup != null) _banterGroup.alpha = 0f;
            if (BanterRoot != null) BanterRoot.gameObject.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (_disposed || dt <= 0f) return;

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
                UpdateCooldownVisual();
            }

            if (_pointerDown && !_wheelOpen)
            {
                _holdElapsed += dt;
                if (_holdElapsed >= HoldThresholdSeconds)
                {
                    OpenWheelInternal();
                    UpdateHighlightFromScreenPos(_pointerScreenPos);
                }
            }

            if (_banterVisible && _banterGroup != null)
            {
                _banterElapsed += dt;
                bool reduceMotion = SettingsStore.Current != null && SettingsStore.Current.ReduceMotion;
                if (_banterElapsed >= _banterDuration)
                {
                    HideBanter();
                }
                else if (reduceMotion)
                {
                    _banterGroup.alpha = 1f;
                }
                else if (_banterElapsed < BanterFadeInSeconds)
                {
                    _banterGroup.alpha = Mathf.Clamp01(_banterElapsed / BanterFadeInSeconds);
                }
                else if (_banterElapsed > _banterDuration - BanterFadeOutSeconds)
                {
                    float remain = _banterDuration - _banterElapsed;
                    _banterGroup.alpha = Mathf.Clamp01(remain / BanterFadeOutSeconds);
                }
                else
                {
                    _banterGroup.alpha = 1f;
                }
            }
        }

        void HandlePointerDown(PointerEventData eventData)
        {
            if (_disposed || !_interactive || _pointerDown) return;
            _pointerDown = true;
            _activePointerId = eventData.pointerId;
            _pointerDownScreenPos = eventData.position;
            _pointerScreenPos = eventData.position;
            _holdElapsed = 0f;
            _wheelOpen = false;
            _highlightedPing = null;
        }

        void HandleDrag(PointerEventData eventData)
        {
            if (_disposed || !_pointerDown || eventData.pointerId != _activePointerId) return;
            _pointerScreenPos = eventData.position;
            if (!_wheelOpen && (_pointerScreenPos - _pointerDownScreenPos).sqrMagnitude >= QuickDragOpenPx * QuickDragOpenPx)
                OpenWheelInternal();
            if (_wheelOpen)
                UpdateHighlightFromScreenPos(_pointerScreenPos);
        }

        void HandlePointerUp(PointerEventData eventData)
        {
            if (_disposed || !_pointerDown || eventData.pointerId != _activePointerId) return;
            bool wasWheel = _wheelOpen;
            if (wasWheel)
                UpdateHighlightFromScreenPos(eventData.position);

            TacticalPingKind? chosen = wasWheel ? _highlightedPing : DefaultTapPing;
            CloseWheel();

            if (chosen.HasValue)
                TryEmitPing(chosen.Value);
        }

        void CancelPointer()
        {
            CloseWheel();
        }

        void OpenWheelInternal()
        {
            _wheelOpen = true;
            _highlightedPing = null;
            if (WheelRoot != null)
                WheelRoot.gameObject.SetActive(true);
            RefreshWedgeHighlights();
        }

        void UpdateHighlightFromScreenPos(Vector2 screenPos)
        {
            if (WheelRoot == null) return;
            Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(WheelRoot, screenPos, uiCam, out Vector2 local))
                UpdateHighlightFromLocalDelta(local);
        }

        void UpdateHighlightFromLocalDelta(Vector2 localDelta)
        {
            float distSq = localDelta.sqrMagnitude;
            if (distSq < CancelRadiusCanvas * CancelRadiusCanvas)
            {
                if (_highlightedPing.HasValue)
                {
                    _highlightedPing = null;
                    RefreshWedgeHighlights();
                }
                return;
            }

            Vector2 dir = localDelta / Mathf.Sqrt(distSq);
            int bestIdx = 0;
            float bestDot = -2f;
            for (int i = 0; i < PingCount; i++)
            {
                float d = Vector2.Dot(dir, s_wedgeDirs[i]);
                if (d > bestDot)
                {
                    bestDot = d;
                    bestIdx = i;
                }
            }

            TacticalPingKind next = (TacticalPingKind)bestIdx;
            if (!_highlightedPing.HasValue || _highlightedPing.Value != next)
            {
                _highlightedPing = next;
                RefreshWedgeHighlights();
            }
        }

        void RefreshWedgeHighlights()
        {
            for (int i = 0; i < PingCount; i++)
            {
                bool active = _highlightedPing.HasValue && (int)_highlightedPing.Value == i;
                WedgeUi w = _wedges[i];
                if (w.Root != null)
                    w.Root.localScale = active ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
                if (w.Plate != null)
                    w.Plate.color = active ? ColWedgeActive : ColWedgeIdle;
                if (w.Ring != null)
                    w.Ring.color = active ? MenuArt.GoldBorder : s_pingColors[i];
                if (w.Icon != null)
                    w.Icon.color = active ? Color.white : s_pingColors[i];
                if (w.Label != null)
                    Ui.SetTextColor(w.Label, active ? MenuArt.GoldLight : MenuArt.Cream);
            }

            bool isCancel = !_highlightedPing.HasValue;
            if (_cancelDisc != null)
                _cancelDisc.color = isCancel ? ColCancelActive : ColWedgeIdle;
            if (_wheelCenterLabel != null)
            {
                _wheelCenterLabel.text = isCancel ? "CANCEL" : s_pingShortLabels[(int)_highlightedPing.Value];
                Ui.SetTextColor(_wheelCenterLabel, isCancel ? MenuArt.Cream : MenuArt.GoldLight);
            }
        }

        void UpdateCooldownVisual()
        {
            int ceil = _cooldownRemaining > 0.001f ? Mathf.CeilToInt(_cooldownRemaining) : 0;
            if (ceil == _lastCooldownCeil) return;
            _lastCooldownCeil = ceil;

            bool cooling = ceil > 0;
            if (_pingPlate != null) _pingPlate.color = cooling ? ColCooldownTint : ColWheelBg;
            if (_pingIcon != null) _pingIcon.enabled = !cooling;
            if (_pingLabel != null) _pingLabel.enabled = !cooling;
            if (_cooldownLabel != null)
            {
                _cooldownLabel.enabled = cooling;
                int clamped = Mathf.Clamp(ceil, 0, s_cooldownStrings.Length - 1);
                _cooldownLabel.text = s_cooldownStrings[clamped];
            }
        }

        void UpdatePortrait(string speakerId, string speakerName)
        {
            string key = speakerId ?? string.Empty;
            if (string.Equals(key, _cachedPortraitId, StringComparison.OrdinalIgnoreCase))
                return;
            _cachedPortraitId = key;

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(key) && !string.Equals(key, LoreCodexData.AteLornaId, StringComparison.OrdinalIgnoreCase))
            {
                tex = KalyeahResources.Texture("portraits/" + key)
                   ?? KalyeahResources.Texture("ui/avatars/" + key);
            }

            if (_banterPortrait != null)
            {
                _banterPortrait.texture = tex;
                _banterPortrait.enabled = tex != null;
                if (tex != null && _banterPortraitFitter != null && tex.height > 0)
                    _banterPortraitFitter.aspectRatio = (float)tex.width / tex.height;
            }

            bool showFallback = tex == null;
            if (_banterFallbackDisc != null) _banterFallbackDisc.enabled = showFallback;
            if (_banterMonogram != null)
            {
                _banterMonogram.enabled = showFallback;
                if (showFallback)
                    _banterMonogram.text = BuildMonogram(key, speakerName);
            }
        }

        static string BuildMonogram(string speakerId, string speakerName)
        {
            if (string.Equals(speakerId, LoreCodexData.AteLornaId, StringComparison.OrdinalIgnoreCase))
                return "AL";
            if (!string.IsNullOrEmpty(speakerName))
                return speakerName.Substring(0, 1).ToUpperInvariant();
            return "K";
        }

        static Color AccentForRelation(BanterRelation relation)
        {
            switch (relation)
            {
                case BanterRelation.AteLorna: return MenuArt.Gold;
                case BanterRelation.Rival: return MenuArt.Coral;
                case BanterRelation.PurokFriend: return MenuArt.Teal;
                default: return MenuArt.GoldLight;
            }
        }

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
            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }
    }
}
