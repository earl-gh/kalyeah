using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The aim-down-sight button under the minimap (docs/SPEC.md §8, left top on the stick side). A setting picks tap to toggle or hold
    /// (docs/SPEC.md §7); <see cref="IsOn"/> becomes the held ADS bit of the local input.
    /// </summary>
    public sealed class AdsButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        static readonly Color DiscIdle = new Color(0.09f, 0.11f, 0.19f, 0.92f);
        static readonly Color DiscOn = new Color(1f, 0.757f, 0.149f, 0.96f);
        static readonly Color InkOn = new Color(0.06f, 0.07f, 0.11f, 1f);

        int _pointer = NoPointer;
        bool _toggledOn;
        Image _disc;
        Image[] _marks;
        Text _label;
        bool _shownOn;

        public RectTransform Rect { get; private set; }

        /// <summary>Tap toggles ADS when true, else ADS lasts while the button is held.</summary>
        public bool ToggleMode = true;

        public bool IsOn => ToggleMode ? _toggledOn : _pointer != NoPointer;

        public static AdsButton Build(Transform parent, float size, bool interactive)
        {
            RectTransform rect = Ui.Panel("AdsButton", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(size, size));
            Image rim = Ui.MakeImage("Rim", rect, new Color(1f, 1f, 1f, 0.85f), round: true, raycast: interactive);
            Stretch(rim.rectTransform, 2f);
            Image disc = Ui.MakeImage("Disc", rect, DiscIdle, round: true);
            Stretch(disc.rectTransform, 4.5f);

            // a scope reticle: ring with four ticks, the same mark the crosshair uses
            Image ring = Ui.MakeImage("Reticle", rect, Color.white);
            ring.sprite = KomiksUi.RingSprite(64, 5);
            Centre(ring.rectTransform, new Vector2(0f, 7f), new Vector2(26f, 26f));
            var marks = new Image[5];
            marks[0] = ring;
            Vector2[] tick = { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(-1f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                Image t = Ui.MakeImage("Tick" + i, ring.transform, Color.white);
                Centre(t.rectTransform, tick[i] * 13f, tick[i].x != 0f ? new Vector2(8f, 2.5f) : new Vector2(2.5f, 8f));
                marks[i + 1] = t;
            }

            Text label = Ui.MakeText("Label", rect, "ADS", 13, Color.white, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            label.rectTransform.offsetMin = new Vector2(4f, 7f);
            label.rectTransform.offsetMax = new Vector2(-4f, 0f);

            var button = rect.gameObject.AddComponent<AdsButton>();
            button.Rect = rect;
            button._disc = disc;
            button._marks = marks;
            button._label = label;
            button.enabled = interactive;
            return button;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!enabled || _pointer != NoPointer) return;
            _pointer = eventData.pointerId;
            if (ToggleMode) _toggledOn = !_toggledOn;
            Haptics.LightTap();
            Sync();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer) return;
            _pointer = NoPointer;
            Sync();
        }

        /// <summary>Drops ADS (tagged, a menu opened, focus lost): a toggled aim never survives an interruption.</summary>
        public void Cancel()
        {
            _pointer = NoPointer;
            _toggledOn = false;
            Sync();
        }

        void OnDisable() => Cancel();

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Cancel();
        }

        void Sync()
        {
            bool on = IsOn;
            if (_disc == null || on == _shownOn) return;
            _shownOn = on;
            _disc.color = on ? DiscOn : DiscIdle;
            Color ink = on ? InkOn : Color.white;
            for (int i = 0; i < _marks.Length; i++) _marks[i].color = ink;
            _label.color = ink;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void Centre(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
