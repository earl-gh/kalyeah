using System;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Mobile viewport, DPI/dp constancy, asymmetric cutout + gesture-pill math, 21:9..4:3 HUD spread clamps, and deterministic
    /// simulated viewport overrides for headless EditMode bounds checks and 844x390 cutout screenshots.
    /// </summary>
    public static class ScreenFitMetrics
    {
        public const float ReferenceDpi = 160f;
        public const float FallbackDpi = 320f;
        public const float MinDpi = 160f;
        public const float MaxDpi = 640f;

        public const float MinTouchDp = 48f;
        public const float PrimaryFireMinDp = 72f;
        public const float MinGutterDp = 8f;
        public const float GesturePillDp = 18f;

        public const float MinLandscapeAspect = 4f / 3f;
        public const float MaxLandscapeAspect = 21f / 9f;
        public const float MaxFoldableClusterHeightShare = 0.42f;
        public const float DefaultCutoutPx = 44f;

        static Vector2Int? _simScreenPx;
        static Rect? _simSafeAreaPx;
        static float? _simDpi;
        static ScreenOrientation? _simOrientation;

        public static event Action SimulatedViewportChanged;

        public static bool HasSimulatedViewport =>
            _simScreenPx.HasValue || _simSafeAreaPx.HasValue || _simDpi.HasValue || _simOrientation.HasValue;

        public static Vector2Int? SimulatedScreenPx => _simScreenPx;
        public static Rect? SimulatedSafeAreaPx => _simSafeAreaPx;

        /// <summary>Enables InputSystem EnhancedTouch idempotently (safe no-op in headless batchmode if native touch is absent).</summary>
        public static void EnsureEnhancedTouch()
        {
            try
            {
                if (!EnhancedTouchSupport.enabled)
                    EnhancedTouchSupport.Enable();
            }
            catch (Exception)
            {
                // Headless/batchmode environments without an active InputSystem backend still process UGUI PointerEventData cleanly.
            }
        }

        /// <summary>Overrides screen pixel size and safe-area rect for deterministic EditMode and 844x390 cutout screenshot tests.</summary>
        public static void SetSimulatedViewport(Vector2Int? screenPx, Rect? safeAreaPx)
        {
            SetSimulatedViewport(screenPx, safeAreaPx, null, null);
        }

        /// <summary>Full simulated viewport override including optional DPI and landscape orientation.</summary>
        public static void SetSimulatedViewport(Vector2Int? screenPx, Rect? safeAreaPx, float? dpi, ScreenOrientation? orientation = null)
        {
            _simScreenPx = screenPx;
            _simSafeAreaPx = safeAreaPx;
            _simDpi = dpi;
            _simOrientation = orientation;
            SimulatedViewportChanged?.Invoke();
        }

        public static void ClearSimulatedViewport() => SetSimulatedViewport(null, null, null, null);

        /// <summary>Canonical asymmetric punch-hole safe rect (default: 844x390 with 44 px left or right camera cutout).</summary>
        public static Rect AsymmetricCutoutSafeRect(int width = 844, int height = 390, float cutoutPx = DefaultCutoutPx, bool cutoutOnRight = false)
        {
            float clampedCutout = Mathf.Clamp(cutoutPx, 0f, Mathf.Max(0f, width * 0.25f));
            return cutoutOnRight
                ? new Rect(0f, 0f, Mathf.Max(1f, width - clampedCutout), height)
                : new Rect(clampedCutout, 0f, Mathf.Max(1f, width - clampedCutout), height);
        }

        public static Vector2Int ScreenSizePx
        {
            get
            {
                if (_simScreenPx.HasValue && _simScreenPx.Value.x > 0 && _simScreenPx.Value.y > 0)
                    return _simScreenPx.Value;
                if (Screen.width > 0 && Screen.height > 0)
                    return new Vector2Int(Screen.width, Screen.height);
                return new Vector2Int((int)Ui.Base.x, (int)Ui.Base.y);
            }
        }

        public static ScreenOrientation EffectiveOrientation => _simOrientation ?? Screen.orientation;

        public static float AspectRatio
        {
            get
            {
                Vector2Int s = ScreenSizePx;
                return s.x / Mathf.Max(1f, s.y);
            }
        }

        /// <summary>Clamps reported hardware DPI to [160..640], using 320f when Screen.dpi reports 0 or invalid values.</summary>
        public static float ClampDpi(float rawDpi)
        {
            if (rawDpi <= 0f || float.IsNaN(rawDpi) || float.IsInfinity(rawDpi))
                return FallbackDpi;
            return Mathf.Clamp(rawDpi, MinDpi, MaxDpi);
        }

        public static float EffectiveDpi
        {
            get
            {
                if (_simDpi.HasValue)
                    return ClampDpi(_simDpi.Value);
                if (_simScreenPx.HasValue)
                {
                    int shortEdge = Mathf.Min(_simScreenPx.Value.x, _simScreenPx.Value.y);
                    if (shortEdge < 720) return ClampDpi((shortEdge / 360f) * ReferenceDpi);
                    if (shortEdge <= 900) return FallbackDpi;
                    return 420f;
                }
                return ClampDpi(Screen.dpi);
            }
        }

        public static float DpToScreenPx(float dp) => Mathf.Max(0f, dp) * (EffectiveDpi / ReferenceDpi);

        public static float CanvasScaleFactor(Canvas canvas = null)
        {
            Vector2Int screen = ScreenSizePx;
            Vector2 refRes = Ui.Base;
            float match = screen.x >= screen.y ? 1f : 0f;

            if (canvas != null)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    refRes = scaler.referenceResolution;
                    match = scaler.matchWidthOrHeight;
                }
                else if (!HasSimulatedViewport && canvas.scaleFactor > 0.001f)
                {
                    return canvas.scaleFactor;
                }
            }

            float logWidth = Mathf.Log(Mathf.Max(1f, screen.x) / Mathf.Max(1f, refRes.x), 2f);
            float logHeight = Mathf.Log(Mathf.Max(1f, screen.y) / Mathf.Max(1f, refRes.y), 2f);
            return Mathf.Max(0.01f, Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match)));
        }

        public static float DpToCanvasPx(float dp, Canvas canvas = null) => DpToScreenPx(dp) / CanvasScaleFactor(canvas);

        /// <summary>Enforces physical >= 48 dp (or custom >= 48 dp floor) touch target size in canvas units.</summary>
        public static float ClampTouchSize(float canvasSize, float minDp = MinTouchDp, Canvas canvas = null)
        {
            float floorPx = DpToCanvasPx(Mathf.Max(MinTouchDp, minDp), canvas);
            return Mathf.Max(canvasSize, floorPx);
        }

        public static Vector2 ClampTouchSize(Vector2 canvasSize, float minDp = MinTouchDp, Canvas canvas = null)
        {
            float floorPx = DpToCanvasPx(Mathf.Max(MinTouchDp, minDp), canvas);
            return new Vector2(Mathf.Max(canvasSize.x, floorPx), Mathf.Max(canvasSize.y, floorPx));
        }

        /// <summary>Raw OS or simulated safe rect clamped inside ScreenSizePx (before bottom gesture-pill floor).</summary>
        public static Rect RawSafeAreaPx
        {
            get
            {
                Vector2Int screen = ScreenSizePx;
                Rect raw;
                if (_simSafeAreaPx.HasValue)
                    raw = _simSafeAreaPx.Value;
                else if (_simScreenPx.HasValue)
                    raw = new Rect(0f, 0f, screen.x, screen.y);
                else
                    raw = Screen.safeArea;

                if (raw.width <= 0f || raw.height <= 0f)
                    return new Rect(0f, 0f, screen.x, screen.y);

                float xMin = Mathf.Clamp(raw.xMin, 0f, screen.x);
                float yMin = Mathf.Clamp(raw.yMin, 0f, screen.y);
                float xMax = Mathf.Clamp(raw.xMax, xMin, screen.x);
                float yMax = Mathf.Clamp(raw.yMax, yMin, screen.y);
                return xMax > xMin && yMax > yMin
                    ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                    : new Rect(0f, 0f, screen.x, screen.y);
            }
        }

        /// <summary>Per-edge asymmetric screen-pixel insets (left, bottom, right, top), including bottom 18 dp gesture-pill clearance.</summary>
        public static Vector4 ResolveSafeInsetsPx(bool reserveGesturePill = true)
        {
            Vector2Int screen = ScreenSizePx;
            Rect raw = RawSafeAreaPx;

            float left = Mathf.Clamp(raw.xMin, 0f, screen.x * 0.25f);
            float right = Mathf.Clamp(screen.x - raw.xMax, 0f, screen.x * 0.25f);
            float bottom = Mathf.Clamp(raw.yMin, 0f, screen.y * 0.25f);
            float top = Mathf.Clamp(screen.y - raw.yMax, 0f, screen.y * 0.25f);

            if (reserveGesturePill)
            {
                float pillPx = Mathf.Min(DpToScreenPx(GesturePillDp), screen.y * 0.15f);
                bottom = Mathf.Max(bottom, pillPx);
            }

            return new Vector4(left, bottom, right, top);
        }

        public static Rect ResolveSafeRectPx(bool reserveGesturePill = true)
        {
            Vector2Int screen = ScreenSizePx;
            Vector4 insets = ResolveSafeInsetsPx(reserveGesturePill);
            return Rect.MinMaxRect(insets.x, insets.y, Mathf.Max(insets.x + 1f, screen.x - insets.z), Mathf.Max(insets.y + 1f, screen.y - insets.w));
        }

        public static Vector2 SafeCanvasSize(Canvas canvas = null)
        {
            Rect safePx = ResolveSafeRectPx(true);
            float scale = CanvasScaleFactor(canvas);
            return new Vector2(safePx.width / scale, safePx.height / scale);
        }

        /// <summary>
        /// Keeps the movement thumb zone anchored to physical outer-bezel reach on 21:9 (~0.32) through 16:9 (0.42) and 4:3 foldables (<= 0.42).
        /// </summary>
        public static float ClampThumbZoneShare(float baseShare = 0.42f, Canvas canvas = null)
        {
            float clampedBase = Mathf.Clamp(baseShare, 0.28f, 0.45f);
            float safeWidth = Mathf.Max(320f, SafeCanvasSize(canvas).x);
            float outerReachCanvas = clampedBase * Ui.Base.x;
            return Mathf.Clamp(outerReachCanvas / safeWidth, 0.28f, clampedBase);
        }

        /// <summary>
        /// Clamps top HUD 5v5 squad wing span across 4:3 foldables (preventing corner minimap/chat overlap) and 21:9 ultra-wides (preventing eye-scan drift).
        /// </summary>
        public static float ClampWingSpread(float preferredSpanCanvas, float sideReserveEachCanvas = 184f, float maxUltraWideSpanCanvas = 1160f, Canvas canvas = null)
        {
            float safeWidth = SafeCanvasSize(canvas).x;
            float maxAvailable = Mathf.Max(260f, safeWidth - sideReserveEachCanvas * 2f);
            float ceiling = Mathf.Min(maxAvailable, maxUltraWideSpanCanvas);
            return Mathf.Clamp(preferredSpanCanvas, Mathf.Min(preferredSpanCanvas, ceiling), ceiling);
        }

        /// <summary>
        /// Keeps corner-pinned thumb controls at outer safe-area bezels on 21:9..16:9 while preserving >= minCenterGapCanvas on 4:3 foldables.
        /// </summary>
        public static float ClampThumbSpreadOffset(float baseEdgeOffsetCanvas, float minCenterGapCanvas = 160f, Canvas canvas = null)
        {
            float safeWidth = SafeCanvasSize(canvas).x;
            float maxOffsetForGap = Mathf.Max(96f, (safeWidth - minCenterGapCanvas) * 0.5f);
            return Mathf.Min(baseEdgeOffsetCanvas, maxOffsetForGap);
        }

        /// <summary>
        /// Enforces <= 42% viewport height cap and center-gap non-collision on 4:3 inner foldables while returning 1.0 on 16:9..21:9 phones.
        /// </summary>
        public static float ClampFoldableClusterScale(float clusterWidthCanvas, float clusterHeightCanvas, float minCenterGapCanvas = 160f, Canvas canvas = null)
        {
            Vector2 safe = SafeCanvasSize(canvas);
            float heightScale = clusterHeightCanvas > 0.01f
                ? (safe.y * MaxFoldableClusterHeightShare) / clusterHeightCanvas
                : 1f;
            float widthScale = clusterWidthCanvas > 0.01f
                ? Mathf.Max(160f, (safe.x - minCenterGapCanvas) * 0.5f) / clusterWidthCanvas
                : 1f;
            return Mathf.Clamp(Mathf.Min(1f, Mathf.Min(heightScale, widthScale)), 0.65f, 1f);
        }
    }
}
