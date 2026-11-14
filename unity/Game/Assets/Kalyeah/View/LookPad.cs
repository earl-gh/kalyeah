using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The right of the screen (docs/GDD.md "Looking", LookTouch): a finger that lands there on no button and drags turns the view.
    /// </summary>
    public sealed class LookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        int _pointer = int.MinValue;
        Vector2 _pending;

        public static LookPad Build(Transform parent)
        {
            RectTransform zone = Ui.Panel("LookZone", parent, new Vector2(0.42f, 0f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero, Vector2.zero);
            Image hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            return zone.gameObject.AddComponent<LookPad>();
        }

        /// <summary>The drag since the last call, in screen pixels (y up).</summary>
        public Vector2 TakeDrag()
        {
            Vector2 drag = _pending;
            _pending = Vector2.zero;
            return drag;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointer == int.MinValue) _pointer = data.pointerId;
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == _pointer) _pending += data.delta;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == _pointer) _pointer = int.MinValue;
        }

        void OnDisable()
        {
            _pointer = int.MinValue;
            _pending = Vector2.zero;
        }
    }
}
