using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Tactile button press feedback: smooth subtle scale-down on PointerDown and crisp spring-back on PointerUp/PointerExit.
    /// Strictly inert (stays at rest scale 1.0) when <c>!Application.isPlaying</c> so static EditMode CI captures are deterministic.
    /// Zero per-frame GC allocations and zero canvas writes when idle.
    /// </summary>
    public sealed class UiPressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float PressedScale = 0.95f;
        public float PressSpeed = 24f;
        public float ReleaseSpeed = 16f;

        Vector3 _restScale = Vector3.one;
        bool _capturedRest;
        float _current = 1f;
        float _target = 1f;
        Selectable _selectable;

        void CaptureRest()
        {
            if (_capturedRest) return;
            Vector3 s = transform.localScale;
            _restScale = s.sqrMagnitude > 1e-6f ? s : Vector3.one;
            _capturedRest = true;
            _selectable = GetComponent<Selectable>();
        }

        void Awake() => CaptureRest();

        void OnEnable() => SnapToRest();

        void OnDisable() => SnapToRest();

        public void SnapToRest()
        {
            CaptureRest();
            _current = 1f;
            _target = 1f;
            transform.localScale = _restScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            CaptureRest();
            if (_selectable != null && !_selectable.interactable) return;
            _target = Mathf.Clamp(PressedScale, 0.85f, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            _target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            _target = 1f;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (_current != 1f) SnapToRest();
                return;
            }
            if (Mathf.Abs(_current - _target) < 0.0005f)
            {
                if (_current != _target)
                {
                    _current = _target;
                    transform.localScale = _restScale * _current;
                }
                return;
            }
            float speed = _target < _current ? PressSpeed : ReleaseSpeed;
            float t = 1f - Mathf.Exp(-Mathf.Max(1f, speed) * Time.unscaledDeltaTime);
            _current = Mathf.Lerp(_current, _target, t);
            transform.localScale = _restScale * _current;
        }
    }

    /// <summary>
    /// Subtle ambient diagonal glossy sheen sweep for primary CTAs (PLAY / LOCK IN / SAVE NAME).
    /// Strictly inert (beam disabled) when <c>!Application.isPlaying</c> or when <c>SettingsStore.Current.ReduceMotion</c> is enabled.
    /// Zero per-frame GC allocations.
    /// </summary>
    public sealed class UiSheen : MonoBehaviour
    {
        public float Period = 3.6f;
        public float SweepDuration = 0.72f;
        public float PeakAlpha = 0.30f;

        RectTransform _hostRect;
        RectTransform _beamRect;
        Image _beamImage;
        float _elapsed;

        public void Configure(RectTransform hostRect, RectTransform beamRect, Image beamImage)
        {
            _hostRect = hostRect;
            _beamRect = beamRect;
            _beamImage = beamImage;
            ResetSheen();
        }

        public void ResetSheen()
        {
            _elapsed = 0f;
            if (_beamImage != null)
            {
                Color c = _beamImage.color;
                c.a = 0f;
                _beamImage.color = c;
                _beamImage.enabled = false;
            }
        }

        void OnEnable() => ResetSheen();

        void OnDisable() => ResetSheen();

        void Update()
        {
            if (_beamImage == null || _beamRect == null || _hostRect == null) return;
            bool inert = !Application.isPlaying || (SettingsStore.Current != null && SettingsStore.Current.ReduceMotion);
            if (inert)
            {
                if (_beamImage.enabled) ResetSheen();
                return;
            }

            float period = Mathf.Max(0.8f, Period);
            float sweep = Mathf.Clamp(SweepDuration, 0.15f, period * 0.85f);
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= period) _elapsed -= period;

            if (_elapsed > sweep)
            {
                if (_beamImage.enabled) _beamImage.enabled = false;
                return;
            }

            float width = _hostRect.rect.width;
            if (width <= 1f) return;

            float beamWidth = Mathf.Clamp(width * 0.42f, 64f, 160f);
            float u = _elapsed / sweep;
            float eased = u * u * (3f - 2f * u);
            float x = Mathf.Lerp(-beamWidth * 0.65f, width + beamWidth * 0.65f, eased);
            float alpha = Mathf.Sin(u * Mathf.PI) * Mathf.Clamp01(PeakAlpha);

            if (!_beamImage.enabled) _beamImage.enabled = true;
            _beamRect.sizeDelta = new Vector2(beamWidth, 0f);
            _beamRect.anchoredPosition = new Vector2(x, 0f);
            Color c = _beamImage.color;
            c.a = alpha;
            _beamImage.color = c;
        }
    }

    /// <summary>
    /// Subtle ambient alpha/scale breathing pulse for live event indicators or CTA back-glows.
    /// Strictly inert (stays at rest colour and scale 1.0) when <c>!Application.isPlaying</c> or <c>ReduceMotion</c> is enabled.
    /// Zero per-frame GC allocations.
    /// </summary>
    public sealed class UiPulse : MonoBehaviour
    {
        public float MinAlpha = 0.55f;
        public float MaxAlpha = 1f;
        public float Speed = 2.4f;
        public float ScaleAmplitude = 0f;

        Graphic _target;
        Color _restColor = Color.white;
        Vector3 _restScale = Vector3.one;
        bool _capturedRest;
        float _elapsed;
        bool _dirty;

        public void Configure(Graphic target)
        {
            _target = target;
            _capturedRest = false;
            CaptureRest();
            SnapToRest();
        }

        void CaptureRest()
        {
            if (_capturedRest || _target == null) return;
            _restColor = _target.color;
            Vector3 s = _target.rectTransform.localScale;
            _restScale = s.sqrMagnitude > 1e-6f ? s : Vector3.one;
            _capturedRest = true;
        }

        public void SnapToRest()
        {
            CaptureRest();
            _elapsed = 0f;
            if (_target != null)
            {
                _target.color = _restColor;
                _target.rectTransform.localScale = _restScale;
            }
            _dirty = false;
        }

        void OnEnable() => SnapToRest();

        void OnDisable() => SnapToRest();

        void Update()
        {
            if (_target == null) return;
            bool inert = !Application.isPlaying || (SettingsStore.Current != null && SettingsStore.Current.ReduceMotion);
            if (inert)
            {
                if (_dirty) SnapToRest();
                return;
            }

            CaptureRest();
            _elapsed += Time.unscaledDeltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(_elapsed * Speed);
            Color c = _restColor;
            c.a = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
            _target.color = c;
            if (ScaleAmplitude > 0.0001f)
                _target.rectTransform.localScale = _restScale * (1f + ScaleAmplitude * wave);
            _dirty = true;
        }
    }

    /// <summary>
    /// Small UGUI builders shared by the HUD and the title: a landscape canvas on the 1280 x 720 base (docs/GDD.md "Landscape layout":
    /// expand, so a long phone sees more to the sides), a safe-area panel, text, plain and round images, plus lightweight tactile feel
    /// and tactical styling helpers. The round image is a code-drawn stand-in (owner rule: no generated art; the owner's HUD art replaces it).
    /// </summary>
    public static class Ui
    {
        public static readonly Vector2 Base = new Vector2(1280f, 720f);
        public static readonly Color Navy = new Color(0.06f, 0.10f, 0.22f, 0.72f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);

        static Sprite _circle;

        /// <summary>Rec. 709 relative luminance (used to prevent dark-ink + dark-outline smudge).</summary>
        public static float Luminance(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;

        /// <summary>Disables any <see cref="Outline"/> on <paramref name="label"/> so dark-ink text renders razor-crisp.</summary>
        public static void ClearOutline(Text label)
        {
            if (label == null) return;
            if (label.TryGetComponent(out Outline outline)) outline.enabled = false;
        }

        /// <summary>Sets <paramref name="label"/>'s colour and automatically disables its dark outline when <paramref name="color"/> is dark ink.</summary>
        public static void SetTextColor(Text label, Color color)
        {
            if (label == null) return;
            label.color = color;
            if (label.TryGetComponent(out Outline outline))
                outline.enabled = Luminance(color) >= 0.25f;
        }

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
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
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

        /// <summary>Creates a full-fill 9-sliced <see cref="Image"/> (falls back to Simple when the sprite has no border).</summary>
        public static Image MakeSliced(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            Image image = MakeImage(name, parent, color, round: false, raycast: raycast);
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            outline.enabled = Luminance(color) >= 0.25f;
            return label;
        }

        /// <summary>Attaches (or updates) tactile <see cref="UiPressFeel"/> on <paramref name="target"/>; inert in EditMode.</summary>
        public static UiPressFeel AddPressFeel(Component target, float pressedScale = 0.95f)
        {
            return target != null ? AddPressFeel(target.gameObject, pressedScale) : null;
        }

        /// <summary>Attaches (or updates) tactile <see cref="UiPressFeel"/> on <paramref name="target"/>; inert in EditMode.</summary>
        public static UiPressFeel AddPressFeel(GameObject target, float pressedScale = 0.95f)
        {
            if (target == null) return null;
            UiPressFeel feel = target.GetComponent<UiPressFeel>();
            if (feel == null) feel = target.AddComponent<UiPressFeel>();
            feel.PressedScale = pressedScale;
            return feel;
        }

        /// <summary>
        /// Attaches a clipped ambient <see cref="UiSheen"/> sweep over <paramref name="target"/> (non-raycast, EditMode-inert).
        /// Ideal for primary CTAs like PLAY, LOCK IN and Save Name.
        /// </summary>
        public static UiSheen AddSheen(RectTransform target, float period = 3.6f, float sweepDuration = 0.72f, float peakAlpha = 0.30f)
        {
            if (target == null) return null;
            UiSheen sheen = target.GetComponent<UiSheen>();
            if (sheen != null)
            {
                sheen.Period = period;
                sheen.SweepDuration = sweepDuration;
                sheen.PeakAlpha = peakAlpha;
                return sheen;
            }

            RectTransform clip = Fill("SheenClip", target);
            clip.gameObject.AddComponent<RectMask2D>();

            Image beam = MakeImage("SheenBeam", clip, new Color(1f, 0.97f, 0.86f, 0f), round: false, raycast: false);
            beam.sprite = MenuArt.SheenStreak();
            beam.enabled = false;
            RectTransform beamRect = beam.rectTransform;
            beamRect.anchorMin = new Vector2(0f, 0f);
            beamRect.anchorMax = new Vector2(0f, 1f);
            beamRect.pivot = new Vector2(0.5f, 0.5f);
            beamRect.anchoredPosition = Vector2.zero;
            beamRect.sizeDelta = new Vector2(100f, 0f);

            sheen = target.gameObject.AddComponent<UiSheen>();
            sheen.Period = period;
            sheen.SweepDuration = sweepDuration;
            sheen.PeakAlpha = peakAlpha;
            sheen.Configure(target, beamRect, beam);
            return sheen;
        }

        /// <summary>Attaches (or updates) an ambient <see cref="UiPulse"/> on <paramref name="target"/>; inert in EditMode.</summary>
        public static UiPulse AddPulse(Graphic target, float minAlpha = 0.55f, float maxAlpha = 1f, float speed = 2.4f, float scaleAmplitude = 0f)
        {
            if (target == null) return null;
            UiPulse pulse = target.GetComponent<UiPulse>();
            if (pulse == null) pulse = target.gameObject.AddComponent<UiPulse>();
            pulse.MinAlpha = minAlpha;
            pulse.MaxAlpha = maxAlpha;
            pulse.Speed = speed;
            pulse.ScaleAmplitude = scaleAmplitude;
            pulse.Configure(target);
            return pulse;
        }

        /// <summary>Adds a crisp luminous vertical accent rail along the left (or right) edge of a card or strip (non-raycast).</summary>
        public static RectTransform MakeAccentRail(string name, Transform parent, Color color, float width = 4f, float verticalInset = 10f, bool rightSide = false)
        {
            float edge = rightSide ? 1f : 0f;
            float insetX = rightSide ? -6f : 6f;
            RectTransform rail = Panel(name, parent, new Vector2(edge, 0f), new Vector2(edge, 1f), new Vector2(edge, 0.5f), new Vector2(insetX, 0f), new Vector2(width, -verticalInset * 2f));
            Image img = rail.gameObject.AddComponent<Image>();
            img.sprite = MenuArt.AccentRail();
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rail;
        }

        /// <summary>Overlays crisp 9-sliced tactical corner brackets over <paramref name="parent"/> (non-raycast).</summary>
        public static Image MakeCornerBrackets(string name, Transform parent, Color color, float expand = 0f)
        {
            Image brackets = MakeSliced(name, parent, MenuArt.CornerBrackets(), color, raycast: false);
            if (Mathf.Abs(expand) > 0.01f)
            {
                brackets.rectTransform.offsetMin = new Vector2(-expand, -expand);
                brackets.rectTransform.offsetMax = new Vector2(expand, expand);
            }
            return brackets;
        }

        /// <summary>
        /// Creates a sleek dark-ink tactical stat chip (<see cref="MenuArt.StatChip"/>) with crisp contrast-safe text and an optional
        /// left accent dot (e.g. cyan for water, gold for cooldown). Non-raycast.
        /// </summary>
        public static RectTransform MakeStatChip(
            string name,
            Transform parent,
            string text,
            Color textColor,
            out Text label,
            int fontSize = 18,
            Font font = null,
            Color? accentDot = null)
        {
            RectTransform chip = Panel(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120f, 32f));
            Image bg = chip.gameObject.AddComponent<Image>();
            bg.sprite = MenuArt.StatChip();
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;

            float leftPad = 10f;
            if (accentDot.HasValue)
            {
                Image dot = MakeImage("Dot", chip, accentDot.Value, round: true, raycast: false);
                RectTransform dotRect = dot.rectTransform;
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0.5f);
                dotRect.pivot = new Vector2(0f, 0.5f);
                dotRect.anchoredPosition = new Vector2(10f, 0f);
                dotRect.sizeDelta = new Vector2(8f, 8f);
                leftPad = 24f;
            }

            label = MakeText("Label", chip, text, fontSize, textColor, accentDot.HasValue ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, font != null ? font : KalyeahResources.DisplayFont());
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(leftPad, 0f);
            lr.offsetMax = new Vector2(-10f, 0f);
            return chip;
        }

        /// <summary>
        /// Applies the dark deep-ink framed tactical input field styling (replaces harsh white boxes).
        /// Any parameter may be null if the caller manages that piece separately.
        /// </summary>
        public static void StyleTacticalInput(Image background, Image focusOutline, Text text, Text placeholder, bool focused = false)
        {
            if (background != null)
            {
                background.sprite = MenuArt.RoundSolid(12);
                background.type = Image.Type.Sliced;
                background.color = MenuArt.InputBg;
            }
            if (focusOutline != null)
            {
                focusOutline.sprite = MenuArt.RoundOutline(Color.white, 12, 2);
                focusOutline.type = Image.Type.Sliced;
                focusOutline.color = focused ? MenuArt.GoldBorder : MenuArt.InputBorder;
                focusOutline.raycastTarget = false;
            }
            if (text != null) SetTextColor(text, MenuArt.Cream);
            if (placeholder != null) SetTextColor(placeholder, MenuArt.MutedText);
        }

        /// <summary>
        /// Builds a sleek centered non-raycast pill toggle visual inside a &gt;= 48 dp hit-target Button parent.
        /// Call <see cref="ApplyToggleVisual"/> to snap the track, thumb and ON/OFF micro-label.
        /// </summary>
        public static RectTransform MakeToggleVisual(
            string name,
            Transform parent,
            out Image trackImage,
            out RectTransform thumbRect,
            out Image thumbImage,
            out Text stateLabel,
            float trackWidth = 82f,
            float trackHeight = 38f)
        {
            RectTransform visual = Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackWidth, trackHeight));

            trackImage = MakeSliced("Track", visual, MenuArt.ToggleTrackOff(), Color.white, raycast: false);

            stateLabel = MakeText("State", visual, "OFF", 15, MenuArt.MutedText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            RectTransform labelRect = stateLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            float thumbSize = Mathf.Max(22f, trackHeight - 6f);
            thumbImage = MakeImage("Thumb", visual, Color.white, round: false, raycast: false);
            thumbImage.sprite = MenuArt.ToggleThumb();
            thumbRect = thumbImage.rectTransform;
            thumbRect.sizeDelta = new Vector2(thumbSize, thumbSize);

            ApplyToggleVisual(false, trackImage, thumbRect, stateLabel);
            return visual;
        }

        /// <summary>Updates a <see cref="MakeToggleVisual"/> switch to its ON or OFF visual state with contrast-safe label styling.</summary>
        public static void ApplyToggleVisual(bool isOn, Image trackImage, RectTransform thumbRect, Text stateLabel = null, string onText = "ON", string offText = "OFF")
        {
            if (trackImage != null)
            {
                trackImage.sprite = isOn ? MenuArt.ToggleTrackOn() : MenuArt.ToggleTrackOff();
                trackImage.type = Image.Type.Sliced;
                trackImage.color = Color.white;
            }
            if (thumbRect != null)
            {
                float edge = isOn ? 1f : 0f;
                thumbRect.anchorMin = new Vector2(edge, 0.5f);
                thumbRect.anchorMax = new Vector2(edge, 0.5f);
                thumbRect.pivot = new Vector2(edge, 0.5f);
                thumbRect.anchoredPosition = new Vector2(isOn ? -3f : 3f, 0f);
            }
            if (stateLabel != null)
            {
                stateLabel.text = isOn ? onText : offText;
                SetTextColor(stateLabel, isOn ? MenuArt.Ink : MenuArt.MutedText);
                float pad = thumbRect != null ? thumbRect.sizeDelta.x + 6f : 36f;
                RectTransform lr = stateLabel.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(isOn ? 6f : pad, 0f);
                lr.offsetMax = new Vector2(isOn ? -pad : -6f, 0f);
                stateLabel.alignment = TextAnchor.MiddleCenter;
            }
        }

        /// <summary>A white disc with a soft edge, drawn once in code (a stand-in for the owner's round HUD art).</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahCircle",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
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
            _circle.hideFlags = HideFlags.HideAndDontSave;
            return _circle;
        }
    }
}
