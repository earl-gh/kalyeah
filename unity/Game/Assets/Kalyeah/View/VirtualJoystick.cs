using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The joystick under the left thumb (docs/GDD.md "Landscape layout", MoveStick): a finger anywhere in its zone (the left 42% of the
    /// screen) puts the stick's base under it; the push is the way and the speed share, with a 12% dead zone; letting go stops the kid and
    /// the stick rests at the bottom left. White only (owner): a white rim, a white thumb, a faint white body.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>The stick's reach, in base UI pixels.</summary>
        public const float Radius = 110f;
        const float DeadZone = 0.12f;

        RectTransform _zone, _base, _knob;
        Vector2 _rest;
        int _pointer = int.MinValue;

        /// <summary>x right, y up, length 0..1 (0 inside the dead zone).</summary>
        public Vector2 Value { get; private set; }

        /// <summary>Builds the zone, base and thumb under `parent` (a safe-area panel).</summary>
        public static VirtualJoystick Build(Transform parent)
        {
            RectTransform zone = Ui.Panel("MoveZone", parent, Vector2.zero, new Vector2(0.42f, 0.8f), Vector2.zero, Vector2.zero, Vector2.zero);
            Image hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var stick = zone.gameObject.AddComponent<VirtualJoystick>();
            stick._zone = zone;
            stick._rest = new Vector2(24f + Radius + 20f, 24f + Radius + 20f);
            Image ring = Ui.MakeImage("Base", zone, new Color(1f, 1f, 1f, 0.16f), round: true);
            stick._base = ring.rectTransform;
            stick._base.anchorMin = stick._base.anchorMax = Vector2.zero;
            stick._base.sizeDelta = Vector2.one * Radius * 2f;
            stick._base.anchoredPosition = stick._rest;
            Image rim = Ui.MakeImage("Rim", stick._base, new Color(1f, 1f, 1f, 0.85f), round: true);
            rim.rectTransform.sizeDelta = Vector2.one * (Radius * 2f + 6f);
            rim.rectTransform.SetAsFirstSibling();
            Image inner = Ui.MakeImage("Body", stick._base, new Color(1f, 1f, 1f, 0.12f), round: true);
            inner.rectTransform.sizeDelta = Vector2.one * Radius * 2f;
            Image knob = Ui.MakeImage("Thumb", stick._base, Color.white, round: true);
            stick._knob = knob.rectTransform;
            stick._knob.sizeDelta = Vector2.one * 84f;
            return stick;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointer != int.MinValue) return;
            _pointer = data.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, data.position, data.pressEventCamera, out Vector2 local))
                _base.anchoredPosition = local - _zone.rect.min;
            Push(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == _pointer) Push(data);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId != _pointer) return;
            _pointer = int.MinValue;
            Value = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
            _base.anchoredPosition = _rest;
        }

        void OnDisable()
        {
            _pointer = int.MinValue;
            Value = Vector2.zero;
        }

        void Push(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, data.position, data.pressEventCamera, out Vector2 local)) return;
            Vector2 push = Vector2.ClampMagnitude(local / Radius, 1f);
            _knob.anchoredPosition = push * Radius;
            Value = push.magnitude < DeadZone ? Vector2.zero : push;
        }
    }
}
