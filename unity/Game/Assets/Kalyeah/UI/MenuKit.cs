using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// The menus' UGUI builders, one look for every screen (kalyeah_theme.gd's colours: espresso ink, cream, amber, blue, jade,
    /// coral): a 1280 x 720 landscape canvas, rounded painted boxes (a code-drawn stand-in, owner rule: no generated art), labels in the
    /// game's fonts, buttons with a 2 px ink trim, on/off switches, segmented choices, scroll lists and toasts. Every touch target is
    /// at least 88 design pixels where it is a main action and 64 elsewhere.
    /// </summary>
    public static class MenuKit
    {
        public static readonly Vector2 Base = new Vector2(1280f, 720f);
        public const float MinTarget = 64f;

        public static readonly Color Ink = Hex("2a2522");
        public static readonly Color InkDeep = Hex("14100e");
        public static readonly Color Cream = Hex("faf3e3");
        public static readonly Color Muted = Hex("d8cbb5");
        public static readonly Color Amber = Hex("f5a623");
        public static readonly Color Gold = Hex("ffe680");
        public static readonly Color Blue = Hex("2d9bf0");
        public static readonly Color Jade = Hex("1f9d8b");
        public static readonly Color Coral = Hex("e2553e");
        public static readonly Color Slate = Hex("463d38");
        public static readonly Color Glass = new Color(0.09f, 0.08f, 0.07f, 0.82f);
        public static readonly Color Tint = new Color(0.01f, 0.025f, 0.06f, 0.82f);
        public static readonly Color ModalBody = Hex("faf3e3");
        public static readonly Color ModalText = Hex("2a2522");
        public static readonly Color ModalMuted = Hex("635449");
        public static readonly Color Night = Hex("0d1a3a");
        public static readonly Color Dusk = Hex("1d3f86");

        public const int Display = 54;
        public const int Headline = 36;
        public const int Body = 28;
        public const int Detail = 22;
        public const int Caption = 18;

        static Sprite _rounded;
        static Sprite _disc;
        static Font _body;
        static Font _display;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        /// <summary>The semantic colour of a button tone (orange/gold, blue, green, red, grey).</summary>
        public static Color Tone(string tone)
        {
            switch (tone)
            {
                case "orange": case "gold": return Amber;
                case "blue": return Blue;
                case "green": return Jade;
                case "red": return Coral;
                case "glass": return Glass;
                default: return Slate;
            }
        }

        /// <summary>Team colours; colour-blind players get blue and orange (docs/GDD.md "Accessibility preferences").</summary>
        public static Color TeamColor(int team, bool colorblind) =>
            team == 0 ? Hex("1d56b5") : colorblind ? Hex("e08a1e") : Hex("b8352b");

        public static Font BodyFont() => _body != null ? _body : _body = FontOr("fonts/brand/KalyeahSans-Variable");
        public static Font DisplayFont() => _display != null ? _display : _display = FontOr("fonts/brand/KalyeahDisplay-Regular");

        static Font FontOr(string path)
        {
            Font font = Resources.Load<Font>(path);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A picture the sync script copied from the Godot project (null when it is missing or not importable, e.g. .webp).</summary>
        public static Texture2D Picture(string path) => Resources.Load<Texture2D>(path);

        // ---- structure -------------------------------------------------------------------------------------------------------------

        /// <summary>An overlay canvas on the 1280 x 720 base, scaled by height (a long phone sees more to the sides).</summary>
        public static Canvas Canvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
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
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors `rect` at `anchor` (0..1 of the parent) with its `pivot` there, moved by `position`, `size` big.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Stretches `rect` over its parent minus the insets (left, bottom, right, top).</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Fill(string name, Transform parent) => Stretch(Rect(name, parent));

        public static LayoutElement Size(Component item, float width = -1f, float height = -1f, float flexWidth = -1f)
        {
            // no `??` on Unity objects: a missing component can be a "fake null" in the editor
            LayoutElement layout = item.GetComponent<LayoutElement>();
            if (layout == null) layout = item.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) layout.preferredWidth = layout.minWidth = width;
            if (height >= 0f) layout.preferredHeight = layout.minHeight = height;
            if (flexWidth >= 0f) layout.flexibleWidth = flexWidth;
            return layout;
        }

        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor align = TextAnchor.MiddleCenter, bool expand = true)
        {
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expand;
            group.childForceExpandHeight = true;
            return group;
        }

        /// <summary>Makes `rect` grow with its children (a list in a scroll view).</summary>
        public static void FitHeight(RectTransform rect)
        {
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        // ---- drawing ---------------------------------------------------------------------------------------------------------------

        /// <summary>A painted box: rounded (the code-drawn stand-in) or square, `color`, optionally taking touches.</summary>
        public static Image Box(string name, Transform parent, Color color, bool raycast = false, bool rounded = true, bool trim = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (rounded)
            {
                image.sprite = Rounded();
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }
            if (trim)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = Ink;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            return image;
        }

        public static Image Disc(string name, Transform parent, Color color)
        {
            Image image = Box(name, parent, color, rounded: false);
            image.sprite = DiscSprite();
            return image;
        }

        /// <summary>A RawImage of `texture` (null: nothing is drawn), keeping its shape inside the rect.</summary>
        public static RawImage Art(string name, Transform parent, Texture2D texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = texture != null;
            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null ? (float)texture.width / Mathf.Max(texture.height, 1) : 1f;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            bool display = false, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = display ? DisplayFont() : BodyFont();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>Big words over art: the display face with a dark outline.</summary>
        public static Text Title(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            Text label = Label(name, parent, text, size, color, align, display: true);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(InkDeep.r, InkDeep.g, InkDeep.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        // ---- controls --------------------------------------------------------------------------------------------------------------

        /// <summary>A painted button with its label; `tone` is orange/gold, blue, green, red, grey or glass.</summary>
        public static Button Button(string name, Transform parent, string text, string tone, UnityAction onClick, int fontSize = Body)
        {
            Image face = Box(name, parent, Tone(tone), raycast: true, trim: tone != "glass");
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Text label = Label("Label", face.transform, text, fontSize, TextOn(tone), TextAnchor.MiddleCenter, display: true);
            Stretch(label.rectTransform, 12f, 4f, 12f, 4f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            label.resizeTextMaxSize = fontSize;
            if (onClick != null) button.onClick.AddListener(onClick);
            button.onClick.AddListener(MenuServices.Buzz);
            return button;
        }

        public static Color TextOn(string tone) => tone == "grey" || tone == "glass" || tone == "slate" ? Cream : InkDeep;

        /// <summary>Repaints a button made by `Button` in another tone (and its label's colour), with new words when given.</summary>
        public static void Restyle(Button button, string tone, string text = null)
        {
            ((Image)button.targetGraphic).color = Tone(tone);
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.color = TextOn(tone);
            if (text != null) label.text = text;
        }

        public static void SetText(Button button, string text)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }

        /// <summary>An On / Off switch (UiToggle): a pill that is jade with its knob right when on, slate with it left when off.</summary>
        public sealed class Switch
        {
            public readonly Button Button;
            readonly Image _track;
            readonly RectTransform _knob;
            public bool On { get; private set; }

            public Switch(Transform parent, bool on, Action<bool> changed)
            {
                _track = Box("Switch", parent, Slate, raycast: true, trim: true);
                Size(_track, 112f, MinTarget);
                Button = _track.gameObject.AddComponent<Button>();
                Button.targetGraphic = _track;
                Image knob = Disc("Knob", _track.transform, Cream);
                _knob = Place(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
                Set(on);
                Button.onClick.AddListener(() =>
                {
                    Set(!On);
                    MenuServices.Buzz();
                    changed?.Invoke(On);
                });
            }

            public void Set(bool on)
            {
                On = on;
                _track.color = on ? Jade : Slate;
                _knob.anchorMin = _knob.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
                _knob.anchoredPosition = new Vector2(on ? -32f : 32f, 0f);
            }
        }

        /// <summary>A row of choices, one lit (UiSegmented).</summary>
        public sealed class Segmented
        {
            public readonly RectTransform Root;
            readonly List<(string Id, Button Button)> _options = new List<(string, Button)>();
            public string Selected { get; private set; } = "";

            public Segmented(Transform parent, IList<(string Id, string Label)> options, string selected, Action<string> chosen, float height = MinTarget)
            {
                Root = Rect("Segmented", parent);
                Row(Root, 8f);
                Size(Root, -1f, height);
                foreach (var option in options)
                {
                    string id = option.Id;
                    Button button = Button("Option_" + id, Root, option.Label, "grey", () =>
                    {
                        Select(id);
                        chosen?.Invoke(id);
                    }, Detail + 2);
                    _options.Add((id, button));
                }
                Select(selected);
            }

            public void Select(string id)
            {
                Selected = id;
                foreach (var option in _options) Restyle(option.Button, option.Id == id ? "orange" : "grey");
            }
        }

        /// <summary>A vertical scroll view filling `parent`; returns the content to add rows to (laid out top to bottom).</summary>
        public static RectTransform Scroll(string name, Transform parent, float spacing, int padding = 0, bool horizontal = false)
        {
            RectTransform view = Fill(name, parent);
            view.gameObject.AddComponent<RectMask2D>();
            Image hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            RectTransform content = Rect("Content", view);
            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.sizeDelta = Vector2.zero;
                HorizontalLayoutGroup row = Row(content, spacing, TextAnchor.MiddleLeft, expand: false);
                row.padding = new RectOffset(padding, padding, padding, padding);
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = Vector2.zero;
                Column(content, spacing, padding);
                FitHeight(content);
            }
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        /// <summary>A one-line text field (the name, the room code); the phone's keyboard opens on a tap.</summary>
        public static InputField Field(string name, Transform parent, string value, int limit, Action<string> changed)
        {
            Image box = Box(name, parent, Cream, raycast: true, trim: true);
            var field = box.gameObject.AddComponent<InputField>();
            Text text = Label("Text", box.transform, "", Body, Ink, TextAnchor.MiddleLeft, display: true);
            Stretch(text.rectTransform, 18f, 4f, 18f, 4f);
            text.supportRichText = false;
            field.textComponent = text;
            field.targetGraphic = box;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            field.text = value;
            if (changed != null) field.onValueChanged.AddListener(v => changed(v));
            return field;
        }

        /// <summary>A short message over everything for a moment (UiToast): "info" blue, "warn" amber, "error" coral, "ok" jade.</summary>
        public static void Toast(Transform canvas, string text, string kind = "ok")
        {
            Color tone = kind == "warn" ? Amber : kind == "error" ? Coral : kind == "info" ? Blue : Jade;
            Image box = Box("Toast", canvas, new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            RectTransform rect = Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(720f, 76f));
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = tone;
            outline.effectDistance = new Vector2(3f, -3f);
            Text label = Label("Text", rect, text, Detail + 2, Cream, TextAnchor.MiddleCenter, wrap: true);
            Stretch(label.rectTransform, 20f, 6f, 20f, 6f);
            box.gameObject.AddComponent<ToastLife>();
        }

        sealed class ToastLife : MonoBehaviour
        {
            float _left = 2.4f;

            void Update()
            {
                _left -= Time.unscaledDeltaTime;
                if (_left <= 0f) Destroy(gameObject);
            }
        }

        // ---- code-drawn stand-ins (the owner's UI art replaces them) ---------------------------------------------------------------

        /// <summary>A white rounded square with soft edges, nine-sliced, drawn once in code.</summary>
        public static Sprite Rounded()
        {
            if (_rounded != null) return _rounded;
            const int size = 64;
            const float radius = 20f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KalyeahRounded", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _rounded = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f));
            _rounded.name = "KalyeahRounded";
            return _rounded;
        }

        public static Sprite DiscSprite()
        {
            if (_disc != null) return _disc;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KalyeahDisc", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _disc = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _disc.name = "KalyeahDisc";
            return _disc;
        }

        /// <summary>A vertical two-colour gradient texture (the backdrop's stand-in for the owner's woven picture, a .webp Unity cannot import).</summary>
        public static Texture2D Gradient(Color top, Color bottom)
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { name = "KalyeahGradient", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            texture.Apply(false, true);
            return texture;
        }
    }

    /// <summary>Keeps its RectTransform inside Screen.safeArea (notch, rounded corners, home bar), in either landscape way round.</summary>
    public sealed class MenuSafeArea : MonoBehaviour
    {
        Rect _applied;

        void Update() => Apply();

        public void Apply()
        {
            Rect safe = Screen.safeArea;
            if (safe == _applied || Screen.width <= 0 || Screen.height <= 0) return;
            _applied = safe;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
