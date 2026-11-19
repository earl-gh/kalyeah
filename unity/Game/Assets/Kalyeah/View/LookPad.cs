using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Multi-touch pointerId-isolated camera drag zone (LookTouch): sits opposite the movement joystick zone,
    /// ignores secondary touches while a look finger is active, and mirrors horizontally in left-handed mode.
    /// </summary>
    public sealed class LookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        const float TopAnchor = 0.85f;

        RectTransform _zone;
        int _pointer = NoPointer;
        Vector2 _pending;
        bool _leftHanded;
        bool _updatingLayout;

        public int ActivePointerId => _pointer;
        public bool IsDragging => _pointer != NoPointer;
        public bool LeftHanded => _leftHanded;

        public static LookPad Build(Transform parent) => Build(parent, false);

        public static LookPad Build(Transform parent, bool leftHanded)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();

            RectTransform zone = Ui.Panel("LookZone", parent, new Vector2(0.42f, 0f), new Vector2(1f, TopAnchor), Vector2.zero, Vector2.zero, Vector2.zero);
            Image hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var pad = zone.gameObject.AddComponent<LookPad>();
            pad._zone = zone;
            pad._leftHanded = leftHanded;
            pad.RefreshLayout();
            return pad;
        }

        void OnEnable()
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;
            if (_zone == null) _zone = transform as RectTransform;
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
            if (_zone == null) _zone = transform as RectTransform;
            if (_zone == null || _updatingLayout) return;

            _updatingLayout = true;
            try
            {
                Canvas canvas = _zone.GetComponentInParent<Canvas>();
                float stickShare = ScreenFitMetrics.ClampThumbZoneShare(0.42f, canvas);
                Vector2 targetMin = _leftHanded ? Vector2.zero : new Vector2(stickShare, 0f);
                Vector2 targetMax = _leftHanded ? new Vector2(1f - stickShare, TopAnchor) : new Vector2(1f, TopAnchor);

                if ((_zone.anchorMin - targetMin).sqrMagnitude > 0.00001f || (_zone.anchorMax - targetMax).sqrMagnitude > 0.00001f)
                {
                    _zone.anchorMin = targetMin;
                    _zone.anchorMax = targetMax;
                    _zone.offsetMin = Vector2.zero;
                    _zone.offsetMax = Vector2.zero;
                }
            }
            finally
            {
                _updatingLayout = false;
            }
        }

        /// <summary>The drag since the last call, in screen pixels (y up).</summary>
        public Vector2 TakeDrag()
        {
            Vector2 drag = _pending;
            _pending = Vector2.zero;
            return drag;
        }

        public void Cancel() => Release();

        public void Release()
        {
            _pointer = NoPointer;
            _pending = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointer == NoPointer && isActiveAndEnabled)
                _pointer = data.pointerId;
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == _pointer)
                _pending += data.delta;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == _pointer)
                _pointer = NoPointer;
        }
    }
}
