using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// The street games' controls (port of scripts/ui/hud/street_game_pad.gd, landscape): the joystick moves the kid (MatchHud's), and the
    /// one action button stands where the ability arc turns: THROW in Tumbang Preso (hold to aim: the line grows with the hold and a drag
    /// turns it, fine while short and fast when full; let go to throw, or let go back on the button after dragging away to cancel), KICK for a
    /// thrower without its slipper, DIVE in Patintero and LUPA in Langit Lupa (a sweep fills while it cools down); nothing for the taya.
    /// What your kid is to do stands at the top centre under the score. White only, like the joystick (owner).
    /// </summary>
    public sealed class StreetPadView : MonoBehaviour
    {
        /// <summary>street_game_pad.gd WIDE_BUTTON_RADIUS.</summary>
        public const float ButtonRadius = 56f;
        const float WhiteOff = 0.45f;

        SimBridge _bridge;
        ShoulderFollow _camera;
        RectTransform _area, _button;
        Image _body, _sweep;
        Text _word, _title, _hint;
        int _pointer = int.MinValue;
        bool _left;
        StreetAction _shownAction = (StreetAction)(-1);

        public static StreetPadView Build(Transform safe, SimBridge bridge, ShoulderFollow camera, float wordsTop)
        {
            RectTransform area = Ui.Fill("StreetPad", safe);
            area.pivot = new Vector2(0f, 1f);
            var pad = area.gameObject.AddComponent<StreetPadView>();
            pad._area = area;
            pad._bridge = bridge;
            pad._camera = camera;
            SVector2 corner = ArcLayout.CornerPoint(new SVector2(1280f, 720f), SVector2.Zero);
            pad._body = Ui.Disc("Action", area, new Vector2(corner.X, corner.Y), ButtonRadius, new Color(1f, 1f, 1f, 0.12f));
            pad._button = pad._body.rectTransform;
            pad._button.anchorMin = pad._button.anchorMax = new Vector2(1f, 0f);
            pad._button.anchoredPosition = new Vector2(corner.X - 1280f, 720f - corner.Y);
            pad._body.raycastTarget = false;
            pad._sweep = Ui.Sweep("Sweep", pad._button, ButtonRadius, new Color(1f, 1f, 1f, 0.22f));
            Image rim = Ui.MakeImage("Rim", pad._button, Color.white);
            rim.sprite = Ui.Ring();
            rim.rectTransform.sizeDelta = Vector2.one * ButtonRadius * 2f;
            pad._word = Ui.MakeText("Word", pad._button, "", 24, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            var hit = new GameObject("Hit", typeof(RectTransform), typeof(Image));
            hit.transform.SetParent(pad._button, false);
            var hitImage = hit.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.rectTransform.sizeDelta = Vector2.one * ButtonRadius * 2.5f;
            var forward = hit.AddComponent<StreetPadTouch>();
            forward.Pad = pad;

            pad._title = Ui.MakeText("Title", area, "", 30, new Color(1f, 0.82f, 0.3f), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            Top(pad._title.rectTransform, wordsTop);
            pad._hint = Ui.MakeText("Hint", area, "", 20, Ui.Cream, TextAnchor.UpperCenter);
            Top(pad._hint.rectTransform, wordsTop + 40f);
            return pad;
        }

        static void Top(RectTransform r, float y)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -y);
            r.sizeDelta = new Vector2(760f, 40f);
        }

        /// <summary>Once a frame: the button, its sweep and the words from SimBridge.Pad.</summary>
        public void Sync()
        {
            StreetPadState s = _bridge.Pad;
            _title.text = s.Shown ? s.Title : "";
            _hint.text = s.Shown ? s.Hint : "";
            bool shown = s.Shown && s.Action != StreetAction.None;
            _button.gameObject.SetActive(shown);
            if (!shown)
            {
                Release(false);
                return;
            }
            if (s.Action != _shownAction)
            {
                if (_shownAction == StreetAction.Throw) Release(true);
                _shownAction = s.Action;
            }
            bool throwing = s.Action == StreetAction.Throw;
            _word.text = throwing ? (s.Aiming ? "AIM" : "THROW") : s.Action.ToString().ToUpperInvariant();
            float strength = s.CanAct || throwing ? 1f : WhiteOff;
            _word.color = new Color(1f, 1f, 1f, strength);
            bool ring = s.Action != StreetAction.Kick;
            _sweep.fillAmount = ring && s.Fill > 0f && (s.Fill < 1f || s.Aiming) ? s.Fill : 0f;
            _body.color = _pointer != int.MinValue ? new Color(1f, 1f, 1f, 0.32f) : new Color(1f, 1f, 1f, 0.12f);
        }

        /// <summary>Lets go of the Throw button without throwing (a menu opened).</summary>
        public void Cancel() => Release(true);

        void Release(bool cancel)
        {
            if (_pointer == int.MinValue) return;
            _pointer = int.MinValue;
            if (_bridge.Input.ThrowHeld) _bridge.ThrowReleased(cancel);
        }

        void OnDisable() => Cancel();

        bool OnButton(PointerEventData data) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_button, data.position, data.pressEventCamera, out Vector2 local) && local.magnitude <= ButtonRadius;

        internal void Down(PointerEventData data)
        {
            StreetPadState s = _bridge.Pad;
            if (_pointer != int.MinValue || !s.Shown || s.Action == StreetAction.None) return;
            _pointer = data.pointerId;
            _left = false;
            if (s.Action == StreetAction.Throw) _bridge.ThrowPressed();
            else _bridge.ActionPressed();
        }

        internal void Move(PointerEventData data)
        {
            if (data.pointerId != _pointer) return;
            if (!OnButton(data)) _left = true;
            if (!_bridge.Input.ThrowHeld) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.0001f) : 1f;
            var drag = new SVector2(data.delta.x / scale, -data.delta.y / scale);
            _bridge.ThrowDragged(drag, _camera.ScreenWay(new SVector2(1f, 0f)));
        }

        internal void Up(PointerEventData data)
        {
            if (data.pointerId != _pointer) return;
            bool cancel = _left && OnButton(data);
            Release(cancel);
        }
    }

    /// <summary>The action button's touch area: hands the finger to the pad (and keeps it while it drags off the button).</summary>
    public sealed class StreetPadTouch : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        internal StreetPadView Pad;

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
        public void OnPointerDown(PointerEventData data) => Pad.Down(data);
        public void OnDrag(PointerEventData data) => Pad.Move(data);
        public void OnPointerUp(PointerEventData data) => Pad.Up(data);
    }
}
