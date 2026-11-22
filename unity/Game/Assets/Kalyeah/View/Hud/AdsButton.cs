using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Whisper-thin smoked-obsidian aim-down-sight control under the minimap (docs/SPEC.md §8, Option A1 G6).
    /// A setting picks tap-to-toggle or hold (docs/SPEC.md §7); <see cref="IsOn"/> becomes the held ADS bit of local input.
    /// </summary>
    public sealed class AdsButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        static readonly Color DiscIdle = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.44f);
        static readonly Color DiscOn = new Color(26f / 255f, 36f / 255f, 48f / 255f, 0.80f);
        static readonly Color RimIdle = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.68f);
        static readonly Color RimOn = new Color(MenuArt.CoralGold.r, MenuArt.CoralGold.g, MenuArt.CoralGold.b, 0.98f);
        static readonly Color IconIdle = new Color(MenuArt.PaperWhite.r, MenuArt.PaperWhite.g, MenuArt.PaperWhite.b, 0.94f);
        static readonly Color IconOn = MenuArt.CoralGold;

        int _pointer = NoPointer;
        bool _toggledOn;
        Image _disc;
        Image _rim;
        Image _reticle;
        bool _shownOn;

        public RectTransform Rect { get; private set; }

        /// <summary>Tap toggles ADS when true, else ADS lasts while the button is held.</summary>
        public bool ToggleMode = true;

        public bool IsOn => ToggleMode ? _toggledOn : _pointer != NoPointer;

        public static AdsButton Build(Transform parent, float size, bool interactive)
        {
            RectTransform rect = Ui.Panel("AdsButton", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(size, size));

            Image disc = Ui.MakeImage("Disc", rect, DiscIdle, round: true, raycast: interactive);
            Stretch(disc.rectTransform, 2f);

            Image rim = Ui.MakeImage("Rim", rect, RimIdle, round: false, raycast: false);
            rim.sprite = KomiksUi.RingSprite(128, 2);
            Stretch(rim.rectTransform, 1.5f);

            Image reticle = Ui.MakeImage("Reticle", rect, IconIdle, round: false, raycast: false);
            reticle.sprite = MenuArt.SkillEmblem("ads");
            reticle.preserveAspect = true;
            reticle.rectTransform.anchorMin = new Vector2(0.22f, 0.22f);
            reticle.rectTransform.anchorMax = new Vector2(0.78f, 0.78f);
            reticle.rectTransform.offsetMin = Vector2.zero;
            reticle.rectTransform.offsetMax = Vector2.zero;

            var button = rect.gameObject.AddComponent<AdsButton>();
            button.Rect = rect;
            button._disc = disc;
            button._rim = rim;
            button._reticle = reticle;
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
            if (_rim != null) _rim.color = on ? RimOn : RimIdle;
            if (_reticle != null) _reticle.color = on ? IconOn : IconIdle;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
