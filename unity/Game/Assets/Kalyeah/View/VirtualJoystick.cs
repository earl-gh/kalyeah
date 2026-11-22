using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Corner-pinned movement joystick (docs/GDD.md "Landscape layout", MoveStick, Option A1 G6): persistent pointerId lock,
    /// 12% dead zone, 0.62 walk/run dual-ring visual threshold, physical dp radius floor, 4:3 foldable height cap,
    /// left-handed corner mirroring, and whisper-thin smoked-obsidian translucent styling (#0E131A @ 42% + 1.5 px titanium hairline).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Baseline reach in 1280x720 UI pixels.</summary>
        public const float Radius = 110f;
        public const float WalkRunThreshold = 0.62f;
        public const float DeadZoneShare = 0.12f;

        const float EdgeMargin = 44f;
        const float ZoneTopAnchor = 0.72f;
        const int NoPointer = int.MinValue;

        static readonly Color SmokedBaseIdle = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.42f);
        static readonly Color SmokedBaseHeld = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.54f);
        static readonly Color SmokedThumbIdle = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.68f);
        static readonly Color SmokedThumbHeld = new Color(24f / 255f, 32f / 255f, 44f / 255f, 0.84f);

        static Sprite _ringSprite;

        RectTransform _zone, _base, _knob, _rimRect, _walkRingRect, _bodyRect;
        Image _bodyImage, _rimImage, _walkRingImage, _knobImage, _knobRimImage, _knobCoreImage;
        Vector2 _rest;
        float _effectiveRadius = Radius;
        int _pointer = NoPointer;
        bool _leftHanded;
        bool _updatingLayout;

        /// <summary>x right, y up, length 0..1 (0 inside the dead zone).</summary>
        public Vector2 Value { get; private set; }

        /// <summary>True when analog push reaches or crosses the 0.62 walk-to-run threshold ring.</summary>
        public bool IsRunning => Value.magnitude >= WalkRunThreshold;

        /// <summary>True when moving smoothly inside (DeadZoneShare .. WalkRunThreshold).</summary>
        public bool IsWalking => Value.sqrMagnitude > 0.0001f && !IsRunning;

        public bool IsHolding => _pointer != NoPointer;
        public int ActivePointerId => _pointer;
        public float EffectiveRadius => _effectiveRadius;
        public bool LeftHanded => _leftHanded;

        /// <summary>Builds the zone, dual-ring base and thumb under `parent` (a safe-area panel).</summary>
        public static VirtualJoystick Build(Transform parent) => Build(parent, false);

        public static VirtualJoystick Build(Transform parent, bool leftHanded)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();

            RectTransform zone = Ui.Panel("MoveZone", parent, Vector2.zero, new Vector2(0.42f, ZoneTopAnchor), Vector2.zero, Vector2.zero, Vector2.zero);
            Image hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var stick = zone.gameObject.AddComponent<VirtualJoystick>();
            stick._zone = zone;
            stick._leftHanded = leftHanded;

            Image body = Ui.MakeImage("Base", zone, SmokedBaseIdle, round: true);
            stick._bodyImage = body;
            stick._base = body.rectTransform;
            stick._bodyRect = body.rectTransform;

            Image rim = Ui.MakeImage("Rim", stick._base, new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.56f));
            rim.sprite = RingSprite();
            stick._rimImage = rim;
            stick._rimRect = rim.rectTransform;

            Image walkRing = Ui.MakeImage("WalkRing", stick._base, new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.18f));
            walkRing.sprite = RingSprite();
            stick._walkRingImage = walkRing;
            stick._walkRingRect = walkRing.rectTransform;

            Image knob = Ui.MakeImage("Thumb", stick._base, SmokedThumbIdle, round: true);
            stick._knobImage = knob;
            stick._knob = knob.rectTransform;

            Image knobRim = Ui.MakeImage("ThumbRim", stick._knob, new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.68f));
            knobRim.sprite = RingSprite();
            knobRim.rectTransform.anchorMin = Vector2.zero;
            knobRim.rectTransform.anchorMax = Vector2.one;
            knobRim.rectTransform.offsetMin = Vector2.zero;
            knobRim.rectTransform.offsetMax = Vector2.zero;
            stick._knobRimImage = knobRim;

            Image knobCore = Ui.MakeImage("ThumbCore", stick._knob, new Color(MenuArt.PaperWhite.r, MenuArt.PaperWhite.g, MenuArt.PaperWhite.b, 0.34f), round: true);
            knobCore.rectTransform.anchorMin = knobCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knobCore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knobCore.rectTransform.sizeDelta = new Vector2(14f, 14f);
            stick._knobCoreImage = knobCore;

            stick.RefreshLayout();
            stick.UpdateRingVisuals();
            return stick;
        }

        void OnEnable()
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;
            RefreshLayout();
        }

        void OnDisable()
        {
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            Release();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Release();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) Release();
        }

        void OnRectTransformDimensionsChange()
        {
            if (!_updatingLayout && _zone != null)
                RefreshLayout();
        }

        public void SetLeftHanded(bool leftHanded)
        {
            if (_leftHanded == leftHanded) return;
            _leftHanded = leftHanded;
            Release();
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_zone == null || _base == null || _updatingLayout) return;
            _updatingLayout = true;
            try
            {
                Canvas canvas = _zone.GetComponentInParent<Canvas>();
                float share = ScreenFitMetrics.ClampThumbZoneShare(0.42f, canvas);
                Vector2 targetMin = _leftHanded ? new Vector2(1f - share, 0f) : Vector2.zero;
                Vector2 targetMax = _leftHanded ? new Vector2(1f, ZoneTopAnchor) : new Vector2(share, ZoneTopAnchor);
                Vector2 targetPivot = _leftHanded ? new Vector2(1f, 0f) : Vector2.zero;

                if ((_zone.anchorMin - targetMin).sqrMagnitude > 0.00001f || (_zone.anchorMax - targetMax).sqrMagnitude > 0.00001f)
                {
                    _zone.anchorMin = targetMin;
                    _zone.anchorMax = targetMax;
                    _zone.pivot = targetPivot;
                    _zone.offsetMin = Vector2.zero;
                    _zone.offsetMax = Vector2.zero;
                }

                Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(canvas);
                float minRadiusDp = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, canvas);
                float maxFoldableRadius = Mathf.Max(72f, (safeSize.y * ScreenFitMetrics.MaxFoldableClusterHeightShare - EdgeMargin) * 0.5f);
                _effectiveRadius = Mathf.Clamp(Mathf.Max(Radius, minRadiusDp), 72f, Mathf.Max(minRadiusDp, maxFoldableRadius));

                float centerOffset = ScreenFitMetrics.ClampThumbSpreadOffset(EdgeMargin + _effectiveRadius, 160f, canvas);
                Vector2 cornerAnchor = _leftHanded ? new Vector2(1f, 0f) : Vector2.zero;
                _base.anchorMin = _base.anchorMax = cornerAnchor;
                _base.pivot = new Vector2(0.5f, 0.5f);
                _rest = new Vector2(_leftHanded ? -centerOffset : centerOffset, EdgeMargin + _effectiveRadius);

                float outerDiameter = _effectiveRadius * 2f;
                _bodyRect.sizeDelta = Vector2.one * outerDiameter;
                if (_rimRect != null) _rimRect.sizeDelta = Vector2.one * (outerDiameter + 6f);
                if (_walkRingRect != null) _walkRingRect.sizeDelta = Vector2.one * (outerDiameter * WalkRunThreshold);
                if (_knob != null)
                {
                    float thumbSize = Mathf.Min(
                        outerDiameter * 0.52f,
                        ScreenFitMetrics.ClampTouchSize(84f, ScreenFitMetrics.MinTouchDp, canvas));
                    _knob.sizeDelta = Vector2.one * thumbSize;
                }

                if (_pointer == NoPointer)
                    _base.anchoredPosition = _rest;
            }
            finally
            {
                _updatingLayout = false;
            }
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointer != NoPointer || !isActiveAndEnabled) return;
            _pointer = data.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, data.position, data.pressEventCamera, out Vector2 local))
            {
                Rect r = _zone.rect;
                float pad = _effectiveRadius;
                float clampedX = r.width > pad * 2f ? Mathf.Clamp(local.x, r.xMin + pad, r.xMax - pad) : r.center.x;
                float clampedY = r.height > pad * 2f ? Mathf.Clamp(local.y, r.yMin + pad, r.yMax - pad) : r.center.y;
                Vector2 anchorRef = new Vector2(
                    Mathf.Lerp(r.xMin, r.xMax, _base.anchorMin.x),
                    Mathf.Lerp(r.yMin, r.yMax, _base.anchorMin.y));
                _base.anchoredPosition = new Vector2(clampedX, clampedY) - anchorRef;
            }
            Push(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == _pointer) Push(data);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == _pointer) Release();
        }

        public void Cancel() => Release();

        void Release()
        {
            _pointer = NoPointer;
            Value = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            if (_base != null) _base.anchoredPosition = _rest;
            UpdateRingVisuals();
        }

        void Push(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, data.position, data.pressEventCamera, out Vector2 local)) return;
            float r = Mathf.Max(1f, _effectiveRadius);
            Vector2 push = Vector2.ClampMagnitude(local / r, 1f);
            _knob.anchoredPosition = push * r;
            Value = push.magnitude < DeadZoneShare ? Vector2.zero : push;
            UpdateRingVisuals();
        }

        void UpdateRingVisuals()
        {
            bool held = _pointer != NoPointer;
            float mag = Value.magnitude;
            bool walking = mag > 0.001f && mag < WalkRunThreshold;
            bool running = mag >= WalkRunThreshold;
            Color ti = MenuArt.TitaniumRim;
            Color pw = MenuArt.PaperWhite;
            if (_bodyImage != null)
                _bodyImage.color = held ? SmokedBaseHeld : SmokedBaseIdle;
            if (_walkRingImage != null)
                _walkRingImage.color = new Color(ti.r, ti.g, ti.b, walking ? 0.42f : 0.16f);
            if (_rimImage != null)
                _rimImage.color = running
                    ? new Color(pw.r, pw.g, pw.b, 0.92f)
                    : new Color(ti.r, ti.g, ti.b, held ? 0.74f : 0.54f);
            if (_knobImage != null)
                _knobImage.color = held ? SmokedThumbHeld : SmokedThumbIdle;
            if (_knobRimImage != null)
                _knobRimImage.color = running
                    ? new Color(pw.r, pw.g, pw.b, 0.96f)
                    : new Color(ti.r, ti.g, ti.b, held ? 0.86f : 0.64f);
            if (_knobCoreImage != null)
                _knobCoreImage.color = new Color(pw.r, pw.g, pw.b, held ? (running ? 0.82f : 0.56f) : 0.30f);
        }

        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahStickRing",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            float r = size * 0.5f - 1.5f;
            const float halfStroke = 1.35f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(halfStroke - Mathf.Abs(dist - r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "KalyeahStickRing";
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }
    }
}
