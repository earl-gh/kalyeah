using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Kalyeah.Game
{
    /// <summary>
    /// 4-card cycle Hand + NEXT card preview + 10-segment Ice water (Elixir/MaxElixir) bar when CardsMode or
    /// SettingsStore.CardsTrayMode is active (port of scripts/ui/hud/card_hand.gd).
    /// Supports both "bottom" and "sides" tray placements, LeftHanded mirroring, quick-tap (<= 0.30 s) auto-aim,
    /// drag-to-street world ground aim, and zero-penalty drop-on-hand / cancel-zone abort outputting AbilityInputState.
    /// </summary>
    public sealed class CardHandView : MonoBehaviour
    {
        public const int MaxSlots = 4;
        public const int MaxElixirSegments = 10;
        public const float QuickTapMaxSeconds = 0.30f;
        public const float DragDeadZoneDp = 12f;
        public const float ElixirCatchUpPerSec = 8f;

        const int NoPointer = int.MinValue;
        const float DenyFlashDuration = 0.28f;

        static readonly Color TrayPlateColor = new Color(0.05f, 0.10f, 0.24f, 0.78f);
        static readonly Color CardReadyColor = new Color(0.96f, 0.94f, 0.88f, 1.00f);
        static readonly Color CardBackColor = new Color(0.10f, 0.15f, 0.28f, 0.72f);
        static readonly Color CardRimReady = new Color(0.95f, 0.97f, 1.00f, 0.88f);
        static readonly Color CardRimHeld = new Color(1.00f, 0.84f, 0.32f, 1.00f);
        static readonly Color CardRimDim = new Color(0.45f, 0.50f, 0.62f, 0.48f);
        static readonly Color MissingCostShade = new Color(0.08f, 0.10f, 0.16f, 0.76f);
        static readonly Color IceWaterFill = new Color(0.28f, 0.78f, 1.00f, 0.96f);
        static readonly Color IceWaterRefill = new Color(0.75f, 0.93f, 1.00f, 0.45f);
        static readonly Color IceWaterEmpty = new Color(0.04f, 0.14f, 0.38f, 0.90f);
        static readonly Color DropBadgeColor = new Color(0.18f, 0.68f, 0.98f, 0.98f);
        static readonly Color CancelIdleColor = new Color(0.62f, 0.12f, 0.14f, 0.84f);
        static readonly Color CancelHotColor = new Color(0.94f, 0.16f, 0.16f, 0.96f);

        sealed class CardSlotHit : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
        {
            public CardHandView Owner;
            public int SlotIndex;

            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointerDown(SlotIndex, eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandleDrag(SlotIndex, eventData);
            public void OnPointerUp(PointerEventData eventData) => Owner?.HandlePointerUp(SlotIndex, eventData);
            public void OnCancel(BaseEventData eventData) => Owner?.HandlePointerCancel(SlotIndex, eventData as PointerEventData);
        }

        sealed class CardSlotWidget
        {
            public int Index;
            public RectTransform Rect;
            public Image Plate;
            public Image Rim;
            public Image Art;
            public Image MissingShade;
            public Text TitleText;
            public Text CooldownText;
            public RectTransform CostDrop;
            public Text CostText;
            public string LastWeaponId = "";
            public bool Pickable;
            public float DenyTimer;
            public Vector2 RestPos;
        }

        readonly CardSlotWidget[] _slots = new CardSlotWidget[MaxSlots];
        readonly RectTransform[] _slotRects = new RectTransform[MaxSlots];
        readonly Image[] _elixirSegments = new Image[MaxElixirSegments];
        readonly string[] _slotWeaponIds = { "", "", "", "" };
        readonly float[] _slotMaxRanges = { 16f, 16f, 16f, 16f };

        RectTransform _root;
        RectTransform _trayPlate;
        RectTransform _nextContainer;
        Image _nextArt;
        Image _nextShade;
        Text _nextTitle;
        Text _nextHeaderLabel;

        RectTransform _elixirRoot;
        Text _elixirWholeText;
        Text _elixirMaxText;
        RectTransform _cheapestMarker;

        RectTransform _heldPreviewCard;
        Image _heldPreviewArt;
        Image _heldPreviewRim;
        Text _heldPreviewTitle;
        Text _heldPreviewCost;

        RectTransform _cancelZone;
        Image _cancelPlate;

        bool _leftHanded;
        string _placement = "bottom";
        float _controlScale = SettingsStore.ScaleNormal;
        bool _reduceMotion;
        bool _updatingLayout;

        float _shownElixir;
        bool _elixirSynced;

        int _activePointer = NoPointer;
        int _heldSlot = -1;
        float _holdElapsed;
        Vector2 _pressScreenPos;
        Vector2 _currentScreenPos;
        Vector2 _screenDragWay;
        bool _hasDraggedOut;
        bool _isCancelHovered;

        bool _slotPressObservedBySim;
        int _pendingReleaseSlot = -1;
        bool _pendingReleaseCancel;
        bool _pendingReleaseStreetAim;
        Vector2 _pendingReleaseScreenPos;
        Vector2 _pendingReleaseDragWay;
        float _pendingReleaseMaxRange;
        bool _latchedAimCancel;
        int _lastSampledSimTick = int.MinValue;

        bool _matchInteractive = true;
        bool _canCast = true;

        public RectTransform Root => _root;
        public IReadOnlyList<RectTransform> CardSlotRects => _slotRects;
        public RectTransform NextCardRect => _nextContainer;
        public RectTransform ElixirBarRect => _elixirRoot;

        public bool LeftHanded => _leftHanded;
        public string Placement => _placement;
        public int HeldSlot => _heldSlot;
        public bool IsAiming => _heldSlot >= 0;
        public bool IsCancelHovered => _isCancelHovered;
        public float ShownElixir => _shownElixir;
        public AbilityInputState CurrentInput { get; private set; }

        public static bool ShouldUseCardsTray(SimBridge sim, SettingsStore settings = null)
        {
            SettingsStore s = settings ?? SettingsStore.Current;
            bool simCards = sim != null && sim.LocalKid.HasValue && sim.LocalKid.Value.CardsMode;
            return simCards || s.CardsTrayMode;
        }

        public static CardHandView Build(Transform parent, bool leftHanded = false, string placement = "bottom")
        {
            ScreenFitMetrics.EnsureEnhancedTouch();

            RectTransform root = Ui.Fill("CardHand", parent);
            var view = root.gameObject.AddComponent<CardHandView>();
            view._root = root;
            view._leftHanded = leftHanded;
            view._placement = placement == "sides" ? "sides" : "bottom";
            view._controlScale = SettingsStore.Current.ControlScale;
            view._reduceMotion = SettingsStore.Current.ReduceMotion;
            view.BuildHierarchy();
            view.RefreshLayout();
            return view;
        }

        void BuildHierarchy()
        {
            _trayPlate = Ui.Panel("TrayPlate", _root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(560f, 152f));
            Image plateBg = Ui.MakeImage("Bg", _trayPlate, TrayPlateColor, round: false, raycast: false);
            plateBg.sprite = HudIconCache.RoundedPlateSprite();
            plateBg.type = Image.Type.Sliced;
            FillRect(plateBg.rectTransform, Vector2.zero);

            // NEXT preview card
            _nextContainer = Ui.Panel("NextCard", _trayPlate, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 80f));
            Image nextBg = Ui.MakeImage("Bg", _nextContainer, CardBackColor, round: false, raycast: false);
            nextBg.sprite = HudIconCache.RoundedPlateSprite();
            nextBg.type = Image.Type.Sliced;
            FillRect(nextBg.rectTransform, Vector2.zero);

            _nextArt = Ui.MakeImage("Art", _nextContainer, Color.white, round: false, raycast: false);
            _nextArt.preserveAspect = true;
            FillRect(_nextArt.rectTransform, new Vector2(4f, 4f));

            _nextTitle = Ui.MakeText("Title", _nextContainer, "", 12, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(_nextTitle.rectTransform, new Vector2(3f, 3f));

            _nextShade = Ui.MakeImage("ReloadShade", _nextContainer, MissingCostShade, round: false, raycast: false);
            _nextShade.type = Image.Type.Filled;
            _nextShade.fillMethod = Image.FillMethod.Vertical;
            _nextShade.fillOrigin = (int)Image.OriginVertical.Top;
            _nextShade.fillAmount = 0f;
            FillRect(_nextShade.rectTransform, Vector2.zero);

            _nextHeaderLabel = Ui.MakeText("NextLabel", _nextContainer, "NEXT", 13, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            _nextHeaderLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _nextHeaderLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _nextHeaderLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _nextHeaderLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            _nextHeaderLabel.rectTransform.sizeDelta = new Vector2(0f, 18f);

            // 4 Hand Card Slots
            for (int i = 0; i < MaxSlots; i++)
                CreateCardSlot(i);

            // 10-segment Ice water (Elixir) bar
            _elixirRoot = Ui.Panel("IceWaterBar", _trayPlate, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(0f, 22f));
            Image barBg = Ui.MakeImage("Track", _elixirRoot, IceWaterEmpty, round: false, raycast: false);
            barBg.sprite = HudIconCache.RoundedPlateSprite();
            barBg.type = Image.Type.Sliced;
            FillRect(barBg.rectTransform, Vector2.zero);

            for (int seg = 0; seg < MaxElixirSegments; seg++)
            {
                float a0 = seg / (float)MaxElixirSegments;
                float a1 = (seg + 1) / (float)MaxElixirSegments;
                RectTransform segRt = Ui.Panel($"Seg_{seg}", _elixirRoot, new Vector2(a0, 0f), new Vector2(a1, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                segRt.offsetMin = new Vector2(1.5f, 2f);
                segRt.offsetMax = new Vector2(-1.5f, -2f);
                Image fill = Ui.MakeImage("Fill", segRt, IceWaterFill, round: false, raycast: false);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                FillRect(fill.rectTransform, Vector2.zero);
                _elixirSegments[seg] = fill;
            }

            _cheapestMarker = Ui.Panel("CheapestMarker", _elixirRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 4f));
            Image markerImg = Ui.MakeImage("Line", _cheapestMarker, Color.white, round: false, raycast: false);
            FillRect(markerImg.rectTransform, Vector2.zero);

            RectTransform dropHolder = Ui.Panel("IceWaterDrop", _elixirRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 2f), new Vector2(36f, 40f));
            Image dropImg = Ui.MakeImage("Drop", dropHolder, DropBadgeColor, round: false, raycast: false);
            dropImg.sprite = HudIconCache.DropSprite();
            dropImg.preserveAspect = true;
            FillRect(dropImg.rectTransform, Vector2.zero);

            _elixirWholeText = Ui.MakeText("Count", dropHolder, "5", 20, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(_elixirWholeText.rectTransform, Vector2.zero);

            _elixirMaxText = Ui.MakeText("MaxLabel", _elixirRoot, "Max: 10", 12, new Color(0.82f, 0.94f, 1f, 0.88f), TextAnchor.MiddleRight);
            _elixirMaxText.rectTransform.anchorMin = new Vector2(1f, 1f);
            _elixirMaxText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _elixirMaxText.rectTransform.pivot = new Vector2(1f, 0f);
            _elixirMaxText.rectTransform.anchoredPosition = new Vector2(-4f, 1f);
            _elixirMaxText.rectTransform.sizeDelta = new Vector2(72f, 14f);

            // Shrunk floating card under finger while dragging out onto the street
            _heldPreviewCard = Ui.Panel("HeldCardPreview", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(68f, 84f));
            Image heldBg = Ui.MakeImage("Bg", _heldPreviewCard, CardReadyColor, round: false, raycast: false);
            heldBg.sprite = HudIconCache.RoundedPlateSprite();
            heldBg.type = Image.Type.Sliced;
            FillRect(heldBg.rectTransform, Vector2.zero);
            _heldPreviewArt = Ui.MakeImage("Art", _heldPreviewCard, Color.white, round: false, raycast: false);
            _heldPreviewArt.preserveAspect = true;
            FillRect(_heldPreviewArt.rectTransform, new Vector2(4f, 4f));
            _heldPreviewTitle = Ui.MakeText("Title", _heldPreviewCard, "", 13, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(_heldPreviewTitle.rectTransform, new Vector2(4f, 4f));
            _heldPreviewRim = Ui.MakeImage("Rim", _heldPreviewCard, CardRimHeld, round: false, raycast: false);
            _heldPreviewRim.sprite = HudIconCache.RoundedPlateSprite();
            _heldPreviewRim.type = Image.Type.Sliced;
            FillRect(_heldPreviewRim.rectTransform, new Vector2(-2f, -2f));
            _heldPreviewCost = Ui.MakeText("Cost", _heldPreviewCard, "", 15, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            FillRect(_heldPreviewCost.rectTransform, new Vector2(0f, 3f));
            _heldPreviewCard.gameObject.SetActive(false);

            // Top-band explicit red cancel target clear of QuickChatButton / PauseButton (in addition to dropping back onto the hand tray)
            _cancelZone = Ui.Panel("CardCancelZone", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-132f, -12f), new Vector2(220f, 96f));
            _cancelPlate = Ui.MakeImage("Plate", _cancelZone, CancelIdleColor, round: false, raycast: false);
            _cancelPlate.sprite = HudIconCache.RoundedPlateSprite();
            _cancelPlate.type = Image.Type.Sliced;
            FillRect(_cancelPlate.rectTransform, Vector2.zero);
            Text cancelTxt = Ui.MakeText("Label", _cancelZone, "Release to\ncancel", 18, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            cancelTxt.lineSpacing = 0.95f;
            cancelTxt.resizeTextForBestFit = true;
            cancelTxt.resizeTextMinSize = 14;
            cancelTxt.resizeTextMaxSize = 18;
            FillRect(cancelTxt.rectTransform, new Vector2(12f, 8f));
            _cancelZone.gameObject.SetActive(false);
        }

        void CreateCardSlot(int slotIndex)
        {
            RectTransform rect = Ui.Panel($"Card_{slotIndex + 1}", _trayPlate, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 114f));
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = HudIconCache.RoundedPlateSprite();
            plate.type = Image.Type.Sliced;
            plate.color = CardReadyColor;
            plate.raycastTarget = true;

            var hit = rect.gameObject.AddComponent<CardSlotHit>();
            hit.Owner = this;
            hit.SlotIndex = slotIndex;

            Image art = Ui.MakeImage("Art", rect, Color.white, round: false, raycast: false);
            art.preserveAspect = true;
            FillRect(art.rectTransform, new Vector2(5f, 8f));

            Text title = Ui.MakeText("FallbackTitle", rect, "", 15, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(title.rectTransform, new Vector2(6f, 16f));

            Image shade = Ui.MakeImage("MissingShade", rect, MissingCostShade, round: false, raycast: false);
            shade.type = Image.Type.Filled;
            shade.fillMethod = Image.FillMethod.Vertical;
            shade.fillOrigin = (int)Image.OriginVertical.Top;
            shade.fillAmount = 0f;
            FillRect(shade.rectTransform, Vector2.zero);

            Image rim = Ui.MakeImage("Rim", rect, CardRimReady, round: false, raycast: false);
            rim.sprite = HudIconCache.RoundedPlateSprite();
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            FillRect(rim.rectTransform, Vector2.zero);

            Text cdText = Ui.MakeText("CooldownText", rect, "", 20, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(cdText.rectTransform, Vector2.zero);

            RectTransform drop = Ui.Panel("CostDrop", rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(28f, 30f));
            Image dropImg = Ui.MakeImage("Drop", drop, DropBadgeColor, round: false, raycast: false);
            dropImg.sprite = HudIconCache.DropSprite();
            dropImg.preserveAspect = true;
            FillRect(dropImg.rectTransform, Vector2.zero);

            Text costTxt = Ui.MakeText("Cost", drop, "3", 16, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(costTxt.rectTransform, Vector2.zero);

            _slots[slotIndex] = new CardSlotWidget
            {
                Index = slotIndex,
                Rect = rect,
                Plate = plate,
                Rim = rim,
                Art = art,
                MissingShade = shade,
                TitleText = title,
                CooldownText = cdText,
                CostDrop = drop,
                CostText = costTxt,
            };
            _slotRects[slotIndex] = rect;
        }

        static void FillRect(RectTransform rt, Vector2 inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = inset;
            rt.offsetMax = -inset;
        }

        void OnEnable()
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;
            SettingsStore.Current.Changed += OnSettingsChanged;
            ApplySettings(SettingsStore.Current);
            RefreshLayout();
        }

        void OnDisable()
        {
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            SettingsStore.Current.Changed -= OnSettingsChanged;
            CancelAll();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelAll();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) CancelAll();
        }

        void OnRectTransformDimensionsChange()
        {
            if (!_updatingLayout && _root != null)
                RefreshLayout();
        }

        void OnSettingsChanged() => ApplySettings(SettingsStore.Current);

        public void ApplySettings(SettingsStore settings = null)
        {
            SettingsStore s = settings ?? SettingsStore.Current;
            _reduceMotion = s.ReduceMotion;
            Haptics.Enabled = s.Haptics;
            string nextPlacement = s.CardsTrayPlacement == "sides" ? "sides" : "bottom";
            bool dirty = _leftHanded != s.LeftHanded || _placement != nextPlacement || Mathf.Abs(_controlScale - s.ControlScale) > 0.001f;
            _leftHanded = s.LeftHanded;
            _placement = nextPlacement;
            _controlScale = s.ControlScale;
            if (dirty)
            {
                CancelAll();
                RefreshLayout();
            }
        }

        public void SetLeftHanded(bool leftHanded)
        {
            if (_leftHanded == leftHanded) return;
            _leftHanded = leftHanded;
            CancelAll();
            RefreshLayout();
        }

        public void SetPlacement(string placement)
        {
            string clean = placement == "sides" ? "sides" : "bottom";
            if (_placement == clean) return;
            _placement = clean;
            CancelAll();
            RefreshLayout();
        }

        public void SetControlScale(float scale)
        {
            float snapped = SettingsStore.SnapControlScale(scale);
            if (Mathf.Abs(_controlScale - snapped) <= 0.001f) return;
            _controlScale = snapped;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_root == null || _trayPlate == null || _updatingLayout) return;
            _updatingLayout = true;
            try
            {
                Canvas canvas = _root.GetComponentInParent<Canvas>();
                float dp = ScreenFitMetrics.DpToCanvasPx(1f, canvas);
                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(canvas);
                float gutter = Mathf.Max(8f * dp, 16f);

                if (_placement == "sides")
                    LayoutSides(canvas, dp, safe, gutter);
                else
                    LayoutBottom(canvas, dp, safe, gutter);

                if (_cancelZone != null)
                {
                    Vector2 cAnchor = _leftHanded ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                    _cancelZone.anchorMin = _cancelZone.anchorMax = cAnchor;
                    _cancelZone.pivot = cAnchor;
                    float ch = ScreenFitMetrics.ClampTouchSize(48f * dp, ScreenFitMetrics.MinTouchDp, canvas);
                    float minCornerBtn = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, canvas);
                    float cornerInset = Mathf.Max(24f, 12f * dp);
                    float innerOffset = cornerInset + minCornerBtn + 12f;
                    float openWingGap = safe.x * 0.5f - 344f - innerOffset;
                    if (openWingGap >= 160f)
                    {
                        float cw = Mathf.Clamp(openWingGap, 164f, 224f);
                        _cancelZone.sizeDelta = new Vector2(cw, ch);
                        _cancelZone.anchoredPosition = new Vector2(_leftHanded ? innerOffset : -innerOffset, -12f);
                    }
                    else
                    {
                        float cw = _leftHanded ? Mathf.Max(124f, ch * 1.35f) : Mathf.Max(188f, ch * 2.1f);
                        float cx = _leftHanded ? 234f : -innerOffset;
                        _cancelZone.sizeDelta = new Vector2(cw, ch);
                        _cancelZone.anchoredPosition = new Vector2(cx, -68f);
                    }
                }
            }
            finally
            {
                _updatingLayout = false;
            }
        }

        void LayoutBottom(Canvas canvas, float dp, Vector2 safe, float gutter)
        {
            // Keep bottom tray on the combat-thumb half (clear of VirtualJoystick) and <= 25% viewport height (outside 45%x50% sightbox)
            float stickShare = ScreenFitMetrics.ClampThumbZoneShare(0.42f, canvas);
            float maxTrayWidth = Mathf.Max(420f, safe.x * (1f - stickShare) - 24f);
            float minCardW = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, canvas);
            float cardW = Mathf.Clamp((maxTrayWidth - gutter * 5.5f) / 4.7f, minCardW, minCardW * 1.12f);
            float cardH = Mathf.Clamp(cardW * 1.12f, minCardW, Mathf.Max(minCardW, safe.y * 0.165f));
            float nextW = Mathf.Max(54f, cardW * 0.64f);
            float nextH = Mathf.Max(64f, cardH * 0.68f);
            float barH = 22f;

            float totalW = nextW + gutter + MaxSlots * cardW + (MaxSlots - 1) * gutter + 28f;
            float totalH = cardH + barH + 22f;

            Vector2 corner = _leftHanded ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            _trayPlate.anchorMin = _trayPlate.anchorMax = corner;
            _trayPlate.pivot = corner;
            _trayPlate.sizeDelta = new Vector2(totalW, totalH);
            _trayPlate.anchoredPosition = new Vector2(_leftHanded ? 12f : -12f, 4f);

            float yCards = barH + 14f + cardH * 0.5f;
            float cursorX = 14f;

            if (!_leftHanded)
            {
                PlaceNext(new Vector2(cursorX + nextW * 0.5f, barH + 14f + nextH * 0.5f), new Vector2(nextW, nextH));
                cursorX += nextW + gutter;
                for (int i = 0; i < MaxSlots; i++)
                {
                    PlaceSlot(i, new Vector2(cursorX + cardW * 0.5f, yCards), new Vector2(cardW, cardH));
                    cursorX += cardW + gutter;
                }
            }
            else
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    PlaceSlot(i, new Vector2(cursorX + cardW * 0.5f, yCards), new Vector2(cardW, cardH));
                    cursorX += cardW + gutter;
                }
                PlaceNext(new Vector2(cursorX + nextW * 0.5f, barH + 14f + nextH * 0.5f), new Vector2(nextW, nextH));
            }

            _elixirRoot.anchorMin = new Vector2(0f, 0f);
            _elixirRoot.anchorMax = new Vector2(1f, 0f);
            _elixirRoot.pivot = new Vector2(0.5f, 0f);
            _elixirRoot.offsetMin = new Vector2(36f, 6f);
            _elixirRoot.offsetMax = new Vector2(-12f, 6f + barH);
        }

        void LayoutSides(Canvas canvas, float dp, Vector2 safe, float gutter)
        {
            // Vertical column inside outer 27.5% bezel strip (outside 45%x50% center sightbox on all aspects)
            float maxStripW = Mathf.Max(116f, safe.x * 0.27f - 16f);
            float minTouch = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, canvas);
            float cardW = Mathf.Clamp(minTouch * 1.04f, minTouch, maxStripW - 16f);
            float availH = Mathf.Max(360f, safe.y - 128f);
            float cardH = Mathf.Clamp((availH - 96f - gutter * 4f) / 4f, minTouch, cardW * 1.12f);
            float nextW = Mathf.Max(52f, cardW * 0.68f);
            float nextH = Mathf.Max(56f, cardH * 0.68f);

            float colW = cardW + 20f;
            float colH = 38f + MaxSlots * (cardH + gutter) + nextH + 24f;

            Vector2 corner = _leftHanded ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            _trayPlate.anchorMin = _trayPlate.anchorMax = corner;
            _trayPlate.pivot = corner;
            _trayPlate.sizeDelta = new Vector2(colW, colH);
            _trayPlate.anchoredPosition = new Vector2(_leftHanded ? 10f : -10f, 8f);

            float centerX = colW * 0.5f;
            float yCursor = 40f;
            for (int i = 0; i < MaxSlots; i++)
            {
                PlaceSlot(i, new Vector2(centerX, yCursor + cardH * 0.5f), new Vector2(cardW, cardH));
                yCursor += cardH + gutter;
            }
            PlaceNext(new Vector2(centerX, yCursor + nextH * 0.5f), new Vector2(nextW, nextH));

            _elixirRoot.anchorMin = new Vector2(0f, 0f);
            _elixirRoot.anchorMax = new Vector2(1f, 0f);
            _elixirRoot.pivot = new Vector2(0.5f, 0f);
            _elixirRoot.offsetMin = new Vector2(28f, 8f);
            _elixirRoot.offsetMax = new Vector2(-8f, 28f);
        }

        void PlaceSlot(int index, Vector2 posFromBottomLeft, Vector2 size)
        {
            CardSlotWidget s = _slots[index];
            s.Rect.anchorMin = s.Rect.anchorMax = Vector2.zero;
            s.Rect.pivot = new Vector2(0.5f, 0.5f);
            s.Rect.sizeDelta = size;
            s.RestPos = posFromBottomLeft;
            s.Rect.anchoredPosition = posFromBottomLeft;
        }

        void PlaceNext(Vector2 posFromBottomLeft, Vector2 size)
        {
            _nextContainer.anchorMin = _nextContainer.anchorMax = Vector2.zero;
            _nextContainer.pivot = new Vector2(0.5f, 0.5f);
            _nextContainer.sizeDelta = size;
            _nextContainer.anchoredPosition = posFromBottomLeft;
        }

        public void CancelAll()
        {
            bool wasHolding = _heldSlot >= 0 || _pendingReleaseSlot >= 0;
            _activePointer = NoPointer;
            _heldSlot = -1;
            _holdElapsed = 0f;
            _screenDragWay = Vector2.zero;
            _hasDraggedOut = false;
            _isCancelHovered = false;
            _slotPressObservedBySim = false;
            _pendingReleaseSlot = -1;

            if (wasHolding)
                _latchedAimCancel = true;

            if (_heldPreviewCard != null && _heldPreviewCard.gameObject.activeSelf)
                _heldPreviewCard.gameObject.SetActive(false);
            if (_cancelZone != null && _cancelZone.gameObject.activeSelf)
                _cancelZone.gameObject.SetActive(false);

            CurrentInput = new AbilityInputState
            {
                Buttons = wasHolding ? InputButtons.AimCancel : 0,
                HeldSlot = -1,
                PreviewWeaponId = "",
            };
        }

        public void Sync(SimBridge sim, float dt, ShoulderCamera shoulder = null, Camera worldCamera = null, MapLayoutData layout = null)
        {
            StepDenyTimers(dt);

            if (sim == null)
            {
                CurrentInput = default;
                return;
            }

            KidSnapshot? localOpt = sim.LocalKid;
            if (!localOpt.HasValue)
            {
                CancelAll();
                return;
            }

            KidSnapshot kid = localOpt.Value;
            bool visible = !kid.Tagged;
            if (_trayPlate != null && _trayPlate.gameObject.activeSelf != visible)
                _trayPlate.gameObject.SetActive(visible);

            bool statusCanCast = kid.StatusKind != StatusKind.Stun &&
                                 kid.StatusKind != StatusKind.Polymorph &&
                                 kid.StatusKind != StatusKind.Knockout &&
                                 kid.StatusKind != StatusKind.Tripped;
            _matchInteractive = sim.Phase == MatchPhaseKind.Playing && !kid.Tagged;
            _canCast = _matchInteractive && statusCanCast && !kid.CastingBlocked && !kid.InRescueZone;

            if (!_canCast && _heldSlot >= 0)
                CancelAll();

            // Ice water (Elixir) catch-up & segmented bar
            float maxElixir = Mathf.Max(1f, kid.MaxElixir > 0.1f ? kid.MaxElixir : sim.MaxElixir);
            float targetElixir = kid.CardsMode ? Mathf.Clamp(kid.Elixir, 0f, maxElixir) : maxElixir;
            if (!_elixirSynced || _reduceMotion)
            {
                _shownElixir = targetElixir;
                _elixirSynced = true;
            }
            else
            {
                _shownElixir = Mathf.MoveTowards(_shownElixir, targetElixir, ElixirCatchUpPerSec * Mathf.Max(0f, dt));
            }

            if (_elixirRoot != null)
            {
                bool showElixir = kid.CardsMode;
                if (_elixirRoot.gameObject.activeSelf != showElixir)
                    _elixirRoot.gameObject.SetActive(showElixir);
                if (showElixir)
                    UpdateElixirBar(maxElixir, kid);
            }

            // 4 Card Slots
            int handCount = kid.HandWeaponIds != null ? kid.HandWeaponIds.Length : 0;
            for (int i = 0; i < MaxSlots; i++)
            {
                CardSlotWidget slot = _slots[i];
                string wid = i < handCount ? (kid.HandWeaponIds[i] ?? "") : "";
                _slotWeaponIds[i] = wid;

                bool showSlot = i < Mathf.Max(3, handCount);
                if (slot.Rect.gameObject.activeSelf != showSlot)
                    slot.Rect.gameObject.SetActive(showSlot);
                if (!showSlot) continue;

                if (string.IsNullOrEmpty(wid))
                {
                    slot.Pickable = false;
                    slot.Art.enabled = false;
                    HudIconCache.SetTextIfChanged(slot.TitleText, "...");
                    HudIconCache.SetTextIfChanged(slot.CooldownText, "");
                    slot.CostDrop.gameObject.SetActive(false);
                    slot.Plate.color = CardBackColor;
                    slot.Rim.color = CardRimDim;
                    slot.MissingShade.fillAmount = 0f;
                    continue;
                }

                WeaponInfo? info = sim.GetWeaponInfo(wid);
                _slotMaxRanges[i] = info.HasValue && info.Value.MaxRange > 0.1f ? info.Value.MaxRange : 16f;
                BindCardArt(slot, wid, info);

                float cd = (kid.SlotCooldowns != null && i < kid.SlotCooldowns.Length) ? Mathf.Max(0f, kid.SlotCooldowns[i]) : 0f;
                float maxCd = (kid.SlotMaxCooldowns != null && i < kid.SlotMaxCooldowns.Length && kid.SlotMaxCooldowns[i] > 0.01f)
                    ? kid.SlotMaxCooldowns[i]
                    : (info.HasValue ? Mathf.Max(0.1f, info.Value.Cooldown) : 1f);
                int cost = (kid.SlotElixirCosts != null && i < kid.SlotElixirCosts.Length)
                    ? kid.SlotElixirCosts[i]
                    : (info.HasValue ? info.Value.ElixirCost : 0);

                bool affordable = !kid.CardsMode || kid.Elixir + 0.001f >= cost;
                bool ready = _canCast && cd <= 0.01f && affordable;
                slot.Pickable = ready;

                bool showCost = kid.CardsMode && cost > 0;
                if (slot.CostDrop.gameObject.activeSelf != showCost)
                    slot.CostDrop.gameObject.SetActive(showCost);
                if (showCost)
                    HudIconCache.SetTextIfChanged(slot.CostText, HudIconCache.Whole(cost));

                float missing = cd > 0.04f
                    ? Mathf.Clamp01(cd / maxCd)
                    : (!affordable && cost > 0 ? Mathf.Clamp01(1f - _shownElixir / cost) : 0f);
                slot.MissingShade.fillAmount = missing;
                HudIconCache.SetTextIfChanged(slot.CooldownText, cd > 0.04f ? HudIconCache.Tenths(cd) : "");

                bool held = _heldSlot == i;
                slot.Plate.color = slot.DenyTimer > 0.001f
                    ? new Color(0.95f, 0.25f, 0.22f, 0.92f)
                    : (ready ? CardReadyColor : new Color(0.55f, 0.56f, 0.62f, 0.85f));
                slot.Rim.color = held ? CardRimHeld : (ready ? CardRimReady : CardRimDim);

                if (slot.DenyTimer <= 0.001f)
                    slot.Rect.anchoredPosition = held ? new Vector2(slot.RestPos.x, slot.RestPos.y + 14f) : slot.RestPos;
            }

            // NEXT card preview
            UpdateNextPreview(sim, kid);

            // Held street preview & cancel highlight
            if (_heldSlot >= 0)
            {
                _holdElapsed += Mathf.Max(0f, dt);
                UpdateHeldPreviewCard(sim);
            }
            if (_cancelZone != null)
            {
                bool showCancel = _heldSlot >= 0;
                if (_cancelZone.gameObject.activeSelf != showCancel)
                    _cancelZone.gameObject.SetActive(showCancel);
                if (showCancel)
                    _cancelPlate.color = _isCancelHovered ? CancelHotColor : CancelIdleColor;
            }

            CurrentInput = SampleInput(sim, shoulder, worldCamera, layout);
        }

        public AbilityInputState SampleInput(SimBridge sim, ShoulderCamera shoulder = null, Camera worldCamera = null, MapLayoutData layout = null)
        {
            int simTick = sim != null ? sim.Tick : (_lastSampledSimTick + 1);
            bool tickAdvanced = simTick != _lastSampledSimTick;
            if (tickAdvanced)
                _lastSampledSimTick = simTick;

            KidSnapshot? localOpt = sim?.LocalKid;
            SVector2 kidFeet = localOpt.HasValue ? localOpt.Value.Position : SVector2.Zero;
            SVector2 kidFacing = localOpt.HasValue ? localOpt.Value.Facing : new SVector2(0f, -1f);
            float originH = (localOpt.HasValue && (localOpt.Value.Crouching || localOpt.Value.Sliding))
                ? AimSolver.CrouchedOriginHeight
                : AimSolver.DefaultOriginHeight;

            int buttons = 0;
            SVector2 aim = SVector2.Zero;
            SVector2 aimPoint = SVector2.Zero;
            bool hasAimPoint = false;
            string previewWid = "";
            bool isDragging = false;

            if (_heldSlot >= 0)
            {
                buttons |= InputButtons.CardSlotBit(_heldSlot);
                if (tickAdvanced)
                    _slotPressObservedBySim = true;

                previewWid = _slotWeaponIds[_heldSlot];
                float range = _slotMaxRanges[_heldSlot];
                isDragging = _hasDraggedOut || _holdElapsed > QuickTapMaxSeconds;

                if (_hasDraggedOut)
                {
                    AimSolution sol = ResolveStreetAim(worldCamera, shoulder, kidFeet, kidFacing, _currentScreenPos, _screenDragWay, range, layout, originH);
                    aim = sol.SimAimDirection;
                    aimPoint = sol.SimAimPoint;
                    hasAimPoint = true;
                }
                else if (_holdElapsed > QuickTapMaxSeconds)
                {
                    AimSolution sol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, range, layout, originH);
                    aim = sol.SimAimDirection;
                    aimPoint = sol.SimAimPoint;
                    hasAimPoint = true;
                }
            }
            else if (_pendingReleaseSlot >= 0)
            {
                if (!_slotPressObservedBySim)
                {
                    buttons |= InputButtons.CardSlotBit(_pendingReleaseSlot);
                    if (tickAdvanced)
                        _slotPressObservedBySim = true;
                }
                else
                {
                    if (_pendingReleaseCancel)
                    {
                        buttons |= InputButtons.AimCancel;
                    }
                    else if (_pendingReleaseStreetAim)
                    {
                        AimSolution sol = ResolveStreetAim(worldCamera, shoulder, kidFeet, kidFacing, _pendingReleaseScreenPos, _pendingReleaseDragWay, _pendingReleaseMaxRange, layout, originH);
                        aim = sol.SimAimDirection;
                        aimPoint = sol.SimAimPoint;
                        hasAimPoint = true;
                    }

                    if (tickAdvanced)
                    {
                        _pendingReleaseSlot = -1;
                        _slotPressObservedBySim = false;
                    }
                }
            }

            if (_latchedAimCancel)
            {
                buttons |= InputButtons.AimCancel;
                if (tickAdvanced)
                    _latchedAimCancel = false;
            }

            var state = new AbilityInputState
            {
                Buttons = buttons,
                Aim = aim,
                AimPoint = aimPoint,
                HasAimPoint = hasAimPoint,
                PreviewWeaponId = previewWid ?? "",
                IsDraggingAim = isDragging,
                IsCancelHovered = _heldSlot >= 0 && _isCancelHovered,
                HeldSlot = _heldSlot,
                ScreenDragWay = _screenDragWay,
                AimScreenPosition = _currentScreenPos,
            };
            CurrentInput = state;
            return state;
        }

        static AimSolution ResolveStreetAim(
            Camera worldCamera,
            ShoulderCamera shoulder,
            SVector2 kidFeet,
            SVector2 kidFacing,
            Vector2 screenPos,
            Vector2 fallbackDragWay,
            float maxRange,
            MapLayoutData layout,
            float originHeight)
        {
            if (worldCamera != null)
            {
                Ray ray = worldCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
                return AimSolver.SolveRay(ray, kidFeet, kidFacing, maxRange, layout, originHeight);
            }
            return AimSolver.SolveArcDrag(shoulder, kidFeet, kidFacing, fallbackDragWay, maxRange, layout, originHeight);
        }

        void UpdateElixirBar(float maxElixir, KidSnapshot kid)
        {
            int whole = Mathf.FloorToInt(_shownElixir + 0.0001f);
            float partial = Mathf.Clamp01(_shownElixir - whole);
            HudIconCache.SetTextIfChanged(_elixirWholeText, HudIconCache.Whole(whole));

            for (int s = 0; s < MaxElixirSegments; s++)
            {
                Image seg = _elixirSegments[s];
                if (s < whole)
                {
                    seg.fillAmount = 1f;
                    seg.color = IceWaterFill;
                }
                else if (s == whole && partial > 0.001f)
                {
                    seg.fillAmount = partial;
                    seg.color = IceWaterRefill;
                }
                else
                {
                    seg.fillAmount = 0f;
                }
            }

            int cheapest = int.MaxValue;
            if (kid.SlotElixirCosts != null)
            {
                for (int i = 0; i < kid.SlotElixirCosts.Length; i++)
                {
                    int c = kid.SlotElixirCosts[i];
                    if (c > 0 && c < cheapest) cheapest = c;
                }
            }
            if (_cheapestMarker != null)
            {
                bool showMarker = cheapest > 0 && cheapest < int.MaxValue && cheapest <= maxElixir;
                if (_cheapestMarker.gameObject.activeSelf != showMarker)
                    _cheapestMarker.gameObject.SetActive(showMarker);
                if (showMarker)
                {
                    float xShare = Mathf.Clamp01(cheapest / maxElixir);
                    _cheapestMarker.anchorMin = new Vector2(xShare, 0f);
                    _cheapestMarker.anchorMax = new Vector2(xShare, 1f);
                    _cheapestMarker.anchoredPosition = Vector2.zero;
                }
            }
        }

        void UpdateNextPreview(SimBridge sim, KidSnapshot kid)
        {
            if (_nextContainer == null) return;
            string nextId = !string.IsNullOrEmpty(kid.NextWeaponId) ? kid.NextWeaponId : kid.ShuffleWeaponId;
            bool hasNext = !string.IsNullOrEmpty(nextId);
            if (_nextContainer.gameObject.activeSelf != hasNext)
                _nextContainer.gameObject.SetActive(hasNext);
            if (!hasNext) return;

            WeaponInfo? info = sim.GetWeaponInfo(nextId);
            Sprite sp = HudIconCache.LoadWeaponSprite(nextId, info.HasValue ? info.Value.IconPath : null, preferCardArt: true);
            if (sp != null)
            {
                _nextArt.sprite = sp;
                _nextArt.enabled = true;
                HudIconCache.SetTextIfChanged(_nextTitle, "");
            }
            else
            {
                _nextArt.enabled = false;
                HudIconCache.SetTextIfChanged(
                    _nextTitle,
                    HudIconCache.ShortLabel(nextId, info.HasValue ? info.Value.ShortName : "", info.HasValue ? info.Value.DisplayName : ""));
            }

            float reloadMissing = kid.ShuffleTotal > 0.01f ? Mathf.Clamp01(kid.ShuffleLeft / kid.ShuffleTotal) : 0f;
            _nextShade.fillAmount = reloadMissing;
        }

        void UpdateHeldPreviewCard(SimBridge sim)
        {
            if (_heldPreviewCard == null || _heldSlot < 0) return;
            bool showFloating = _hasDraggedOut;
            if (_heldPreviewCard.gameObject.activeSelf != showFloating)
                _heldPreviewCard.gameObject.SetActive(showFloating);
            if (!showFloating) return;

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, _currentScreenPos, eventCam, out Vector2 local))
                _heldPreviewCard.anchoredPosition = new Vector2(local.x, local.y + 22f);

            string wid = _slotWeaponIds[_heldSlot];
            WeaponInfo? info = sim.GetWeaponInfo(wid);
            Sprite sp = HudIconCache.LoadWeaponSprite(wid, info.HasValue ? info.Value.IconPath : null, preferCardArt: true);
            _heldPreviewArt.sprite = sp;
            _heldPreviewArt.enabled = sp != null;
            HudIconCache.SetTextIfChanged(_heldPreviewTitle, sp == null ? HudIconCache.ShortLabel(wid, "", "") : "");
            _heldPreviewRim.color = _isCancelHovered ? CancelHotColor : CardRimHeld;
        }

        static void BindCardArt(CardSlotWidget slot, string weaponId, WeaponInfo? info)
        {
            if (string.Equals(slot.LastWeaponId, weaponId, StringComparison.Ordinal)) return;
            slot.LastWeaponId = weaponId;

            Sprite sp = HudIconCache.LoadWeaponSprite(weaponId, info.HasValue ? info.Value.IconPath : null, preferCardArt: true);
            if (sp != null)
            {
                slot.Art.sprite = sp;
                slot.Art.enabled = true;
                HudIconCache.SetTextIfChanged(slot.TitleText, "");
            }
            else
            {
                slot.Art.enabled = false;
                HudIconCache.SetTextIfChanged(
                    slot.TitleText,
                    HudIconCache.ShortLabel(
                        weaponId,
                        info.HasValue ? info.Value.ShortName : "",
                        info.HasValue ? info.Value.DisplayName : ""));
            }
        }

        void HandlePointerDown(int slotIndex, PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _activePointer != NoPointer || _heldSlot >= 0) return;
            if (slotIndex < 0 || slotIndex >= MaxSlots) return;

            CardSlotWidget slot = _slots[slotIndex];
            if (!slot.Pickable || !_canCast || string.IsNullOrEmpty(_slotWeaponIds[slotIndex]))
            {
                slot.DenyTimer = DenyFlashDuration;
                Haptics.CancelTick();
                return;
            }

            _activePointer = eventData.pointerId;
            _heldSlot = slotIndex;
            _holdElapsed = 0f;
            _pressScreenPos = eventData.position;
            _currentScreenPos = eventData.position;
            _screenDragWay = Vector2.zero;
            _hasDraggedOut = false;
            _isCancelHovered = false;
            _slotPressObservedBySim = false;
            _pendingReleaseSlot = -1;
            if (_cancelZone != null) _cancelZone.gameObject.SetActive(true);
            Haptics.LightTap();
        }

        void HandleDrag(int slotIndex, PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer || _heldSlot != slotIndex) return;
            _currentScreenPos = eventData.position;

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            Camera eventCam = eventData.pressEventCamera ?? (canvas != null ? canvas.worldCamera : null);
            float deadZonePx = Mathf.Max(14f, ScreenFitMetrics.DpToScreenPx(DragDeadZoneDp));
            float dragRadiusPx = Mathf.Max(48f, ScreenFitMetrics.DpToScreenPx(72f));

            Vector2 delta = eventData.position - _pressScreenPos;
            if (delta.magnitude > deadZonePx)
                _hasDraggedOut = true;

            _screenDragWay = _hasDraggedOut ? Vector2.ClampMagnitude(delta / dragRadiusPx, 1f) : Vector2.zero;

            bool overCancelPill = _cancelZone != null && _cancelZone.gameObject.activeInHierarchy &&
                                  RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, eventData.position, eventCam);
            // Dropping a dragged-out card back onto the hand tray also cancels cleanly (Clash Royale UX)
            bool returnedToTray = _hasDraggedOut && _trayPlate != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(_trayPlate, eventData.position, eventCam) &&
                                  !RectTransformUtility.RectangleContainsScreenPoint(_slots[slotIndex].Rect, eventData.position, eventCam);

            bool cancelNow = overCancelPill || returnedToTray;
            if (cancelNow && !_isCancelHovered)
                Haptics.CancelTick();
            _isCancelHovered = cancelNow;
        }

        void HandlePointerUp(int slotIndex, PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer || _heldSlot != slotIndex) return;

            if (_isCancelHovered)
                Haptics.CancelTick();

            _pendingReleaseSlot = _heldSlot;
            _pendingReleaseCancel = _isCancelHovered;
            _pendingReleaseStreetAim = !_isCancelHovered && (_hasDraggedOut || _holdElapsed > QuickTapMaxSeconds);
            _pendingReleaseScreenPos = eventData.position;
            _pendingReleaseDragWay = _screenDragWay;
            _pendingReleaseMaxRange = _slotMaxRanges[_heldSlot];

            _activePointer = NoPointer;
            _heldSlot = -1;
            _holdElapsed = 0f;
            _screenDragWay = Vector2.zero;
            _hasDraggedOut = false;
            _isCancelHovered = false;
            if (_heldPreviewCard != null) _heldPreviewCard.gameObject.SetActive(false);
            if (_cancelZone != null) _cancelZone.gameObject.SetActive(false);
        }

        void HandlePointerCancel(int slotIndex, PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId == _activePointer)
                CancelAll();
        }

        void StepDenyTimers(float dt)
        {
            if (_activePointer != NoPointer && EnhancedTouchSupport.enabled)
            {
                var touches = ETouch.activeTouches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].phase == ETouchPhase.Canceled && touches[i].touchId == _activePointer)
                    {
                        CancelAll();
                        break;
                    }
                }
            }
            if (dt <= 0f) return;
            for (int i = 0; i < MaxSlots; i++)
            {
                CardSlotWidget s = _slots[i];
                if (s == null || s.DenyTimer <= 0f) continue;
                s.DenyTimer = Mathf.Max(0f, s.DenyTimer - dt);
                float shake = (!_reduceMotion && s.DenyTimer > 0f)
                    ? Mathf.Sin((1f - s.DenyTimer / DenyFlashDuration) * Mathf.PI * 6f) * 6f
                    : 0f;
                s.Rect.anchoredPosition = new Vector2(s.RestPos.x + shake, s.RestPos.y);
            }
        }
    }
}
