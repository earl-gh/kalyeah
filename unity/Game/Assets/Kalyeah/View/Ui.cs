using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Small UGUI builders shared by the HUD and the title: a landscape canvas on the 1280 x 720 base (docs/GDD.md "Landscape layout":
    /// expand, so a long phone sees more to the sides), a safe-area panel, text, plain and round images. The round image is a code-drawn
    /// stand-in (owner rule: no generated art; the owner's HUD art replaces it).
    /// </summary>
    public static class Ui
    {
        public static readonly Vector2 Base = new Vector2(1280f, 720f);
        public static readonly Color Navy = new Color(0.06f, 0.10f, 0.22f, 0.72f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);

        static Sprite _circle;

        /// <summary>
        /// A canvas scaled to the 720 px tall base. With `camera` it renders through that camera (so Camera.Render captures it: the
        /// screenshots); without, it is an overlay.
        /// </summary>
        public static Canvas MakeCanvas(string name, Transform parent, Camera camera)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.05f, 0.5f);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Base;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        /// <summary>An EventSystem driven by the Input System (touches arrive as pointer events), unless the scene has one.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>A panel that fills its parent.</summary>
        public static RectTransform Fill(string name, Transform parent)
        {
            RectTransform rect = Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rect;
        }

        public static Image MakeImage(string name, Transform parent, Color color, bool round = false, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (round) image.sprite = Circle();
            return image;
        }

        public static Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor align, Font font = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font != null ? font : KalyeahResources.UiFont();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.07f, 0.16f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>A white disc with a soft edge, drawn once in code (a stand-in for the owner's round HUD art).</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KalyeahCircle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _circle.name = "KalyeahCircle";
            return _circle;
        }
    }
}
