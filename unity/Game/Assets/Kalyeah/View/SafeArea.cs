using System;
using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// Insets its RectTransform by per-edge asymmetric cutouts plus bottom 18 dp Android gesture-pill clearance, re-applying on
    /// Screen.safeArea changes, resolution resizes, LandscapeLeft/LandscapeRight 180-degree flips, or ScreenFitMetrics simulated viewports.
    /// </summary>
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        bool _initialized;
        bool _subscribed;
        Rect _appliedRawSafe;
        Vector2Int _appliedScreen;
        ScreenOrientation _appliedOrientation;
        float _appliedDpi;
        bool _appliedGesturePill;

        public bool ReserveGesturePill { get; set; } = true;

        /// <summary>Resolved screen-pixel insets: (x = left, y = bottom, z = right, w = top).</summary>
        public Vector4 InsetsPx { get; private set; }

        /// <summary>Resolved screen-pixel safe rectangle including bottom gesture-pill clearance.</summary>
        public Rect AppliedSafeRectPx { get; private set; }

        public ScreenOrientation AppliedOrientation { get; private set; }

        public event Action<SafeArea> Changed;

        void Awake()
        {
            if (isActiveAndEnabled) EnsureSubscribed();
            Apply();
        }

        void OnEnable()
        {
            EnsureSubscribed();
            Apply();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        void OnRectTransformDimensionsChange() => Apply();

        void Update() => Apply();

        void EnsureSubscribed()
        {
            if (_subscribed) return;
            ScreenFitMetrics.SimulatedViewportChanged += HandleSimulatedViewportChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            ScreenFitMetrics.SimulatedViewportChanged -= HandleSimulatedViewportChanged;
            _subscribed = false;
        }

        void HandleSimulatedViewportChanged()
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }
            if (!isActiveAndEnabled) return;
            ForceApply();
        }

        public void ForceApply()
        {
            _initialized = false;
            Apply();
        }

        public void Apply()
        {
            if (this == null) return;
            if (isActiveAndEnabled) EnsureSubscribed();

            Vector2Int screen = ScreenFitMetrics.ScreenSizePx;
            if (screen.x <= 0 || screen.y <= 0) return;

            Rect rawSafe = ScreenFitMetrics.RawSafeAreaPx;
            ScreenOrientation orientation = ScreenFitMetrics.EffectiveOrientation;
            float dpi = ScreenFitMetrics.EffectiveDpi;

            if (_initialized
                && rawSafe == _appliedRawSafe
                && screen == _appliedScreen
                && orientation == _appliedOrientation
                && Mathf.Approximately(dpi, _appliedDpi)
                && ReserveGesturePill == _appliedGesturePill)
            {
                return;
            }

            _initialized = true;
            _appliedRawSafe = rawSafe;
            _appliedScreen = screen;
            _appliedOrientation = orientation;
            _appliedDpi = dpi;
            _appliedGesturePill = ReserveGesturePill;

            Vector4 insets = ScreenFitMetrics.ResolveSafeInsetsPx(ReserveGesturePill);
            InsetsPx = insets;
            AppliedSafeRectPx = Rect.MinMaxRect(
                insets.x,
                insets.y,
                Mathf.Max(insets.x + 1f, screen.x - insets.z),
                Mathf.Max(insets.y + 1f, screen.y - insets.w));
            AppliedOrientation = orientation;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(insets.x / screen.x, insets.y / screen.y);
            rect.anchorMax = new Vector2(1f - insets.z / screen.x, 1f - insets.w / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (!Application.isPlaying)
            {
                foreach (VirtualJoystick stick in GetComponentsInChildren<VirtualJoystick>(true))
                    stick.RefreshLayout();
                foreach (LookPad look in GetComponentsInChildren<LookPad>(true))
                    look.RefreshLayout();
            }

            Changed?.Invoke(this);
        }
    }
}
