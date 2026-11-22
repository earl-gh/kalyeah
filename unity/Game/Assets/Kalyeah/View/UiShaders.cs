using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Tactile button spring choreography for Option A1 / A2 CTAs and skill discs:
    /// smooth scale squash + vertical 3 px press-lip dip on PointerDown, damped spring-back on release,
    /// and GPU <c>_PressAmount</c> feedback when paired with <c>Kalyeah/UI_MetallicSheen</c>.
    /// Strictly inert in EditMode (zero writes to Transform/RectTransform so static captures stay deterministic),
    /// <see cref="SettingsStore.ReduceMotion"/>-safe, and zero per-frame GC allocations.
    /// </summary>
    public sealed class UiPressSpring : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float PressScale = 0.94f;
        public float LipDipPx = 3f;
        public float PressSpeed = 28f;
        public float ReleaseSpeed = 18f;

        Selectable _selectable;
        RectTransform _rect;
        Graphic _graphic;
        bool _hasPressShaderProp;
        bool _pressed;
        bool _capturedRest;
        Vector3 _restScale = Vector3.one;
        Vector2 _restAnchoredPos;
        float _pressAmount;
        float _scaleVelocity;
        float _currentScale = 1f;

        public void Configure(Selectable selectable, float pressScale, float lipDipPx)
        {
            _selectable = selectable != null ? selectable : GetComponent<Selectable>();
            _rect = transform as RectTransform;
            PressScale = Mathf.Clamp(pressScale, 0.84f, 1f);
            LipDipPx = Mathf.Clamp(lipDipPx, 0f, 12f);
            RefreshGraphicHook();
            SnapToRest();
        }

        void Awake()
        {
            if (_selectable == null) _selectable = GetComponent<Selectable>();
            if (_rect == null) _rect = transform as RectTransform;
            RefreshGraphicHook();
        }

        void OnEnable() => SnapToRest();

        void OnDisable() => SnapToRest();

        void RefreshGraphicHook()
        {
            _graphic = _selectable != null ? _selectable.targetGraphic : GetComponent<Graphic>();
            Material mat = _graphic != null ? _graphic.material : null;
            _hasPressShaderProp = mat != null && mat.HasProperty(UiShaders.PropPressAmount);
        }

        void CaptureRestIfIdle()
        {
            if (_capturedRest) return;
            Vector3 s = transform.localScale;
            _restScale = s.sqrMagnitude > 1e-6f ? s : Vector3.one;
            if (_rect != null) _restAnchoredPos = _rect.anchoredPosition;
            _capturedRest = true;
        }

        public void SnapToRest()
        {
            _pressed = false;
            _pressAmount = 0f;
            _scaleVelocity = 0f;
            _currentScale = 1f;
            if (_capturedRest)
            {
                transform.localScale = _restScale;
                if (_rect != null && LipDipPx > 0.001f)
                    _rect.anchoredPosition = _restAnchoredPos;
                _capturedRest = false;
            }
            ApplyShaderPress(0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            if (_selectable != null && !_selectable.interactable) return;
            RefreshGraphicHook();
            CaptureRestIfIdle();
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            _pressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            _pressed = false;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (_capturedRest || _pressAmount > 0.0001f) SnapToRest();
                return;
            }

            if (!_pressed && !_capturedRest && _pressAmount <= 0.0001f)
                return;

            bool reduceMotion = SettingsStore.Current != null && SettingsStore.Current.ReduceMotion;
            float targetPress = _pressed ? 1f : 0f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (reduceMotion)
            {
                _pressAmount = targetPress;
                _currentScale = 1f;
                _scaleVelocity = 0f;
                if (_capturedRest)
                {
                    transform.localScale = _restScale;
                    if (_rect != null && LipDipPx > 0.001f)
                        _rect.anchoredPosition = _restAnchoredPos;
                }
                ApplyShaderPress(_pressAmount);
                if (!_pressed) _capturedRest = false;
                return;
            }

            float targetScale = _pressed ? PressScale : 1f;
            float pressRate = _pressed ? PressSpeed : ReleaseSpeed;
            float t = 1f - Mathf.Exp(-Mathf.Max(1f, pressRate) * dt);
            _pressAmount = Mathf.Lerp(_pressAmount, targetPress, t);

            // Critically-to-lightly underdamped spring on release for crisp tactile rebound.
            float stiffness = _pressed ? 520f : 340f;
            float damping = _pressed ? 34f : 20f;
            float accel = (targetScale - _currentScale) * stiffness - _scaleVelocity * damping;
            _scaleVelocity += accel * dt;
            _currentScale = Mathf.Clamp(_currentScale + _scaleVelocity * dt, 0.80f, 1.05f);

            bool settled = !_pressed
                && _pressAmount < 0.002f
                && Mathf.Abs(_currentScale - 1f) < 0.001f
                && Mathf.Abs(_scaleVelocity) < 0.01f;

            if (settled)
            {
                SnapToRest();
                return;
            }

            if (_capturedRest)
            {
                transform.localScale = _restScale * _currentScale;
                if (_rect != null && LipDipPx > 0.001f)
                    _rect.anchoredPosition = new Vector2(_restAnchoredPos.x, _restAnchoredPos.y - LipDipPx * _pressAmount);
            }
            ApplyShaderPress(_pressAmount);
        }

        void ApplyShaderPress(float amount)
        {
            if (!_hasPressShaderProp || _graphic == null) return;
            Material mat = _graphic.material;
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
                mat.SetFloat(UiShaders.PropPressAmount, amount);
        }
    }

    /// <summary>
    /// Cached URP UI Shader material factory and zero-GC property IDs for Kalyeah's Locked Option A1 / A2 UI system.
    /// Resolves shaders from retained <c>KalyeahMaterials</c> presets first (Android IL2CPP safe), then <see cref="Shader.Find"/>,
    /// falling back gracefully to <see cref="Graphic.defaultGraphicMaterial"/> in minimal headless test contexts.
    /// </summary>
    public static class UiShaders
    {
        public const string TacticalGlassShader = "Kalyeah/UI_TacticalGlass";
        public const string MetallicSheenShader = "Kalyeah/UI_MetallicSheen";
        public const string LiquidGaugeShader = "Kalyeah/UI_LiquidGauge";
        public const string RadialSkillDiscShader = "Kalyeah/UI_RadialSkillDisc";
        public const string SoftGlowShader = "Kalyeah/UI_SoftGlow";

        public static readonly string[] AllShaderNames =
        {
            TacticalGlassShader,
            MetallicSheenShader,
            LiquidGaugeShader,
            RadialSkillDiscShader,
            SoftGlowShader,
        };

        // Cached Shader Property IDs (zero per-frame GC for HUD & menu animations).
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropRectSize = Shader.PropertyToID("_RectSize");
        public static readonly int PropCornerCut = Shader.PropertyToID("_CornerCut");
        public static readonly int PropBorderWidth = Shader.PropertyToID("_BorderWidth");
        public static readonly int PropBorderColor = Shader.PropertyToID("_BorderColor");
        public static readonly int PropTopHighlight = Shader.PropertyToID("_TopHighlight");
        public static readonly int PropAccentColor = Shader.PropertyToID("_AccentColor");
        public static readonly int PropAccentWidth = Shader.PropertyToID("_AccentWidth");
        public static readonly int PropGlassAlpha = Shader.PropertyToID("_GlassAlpha");
        public static readonly int PropNoiseGrain = Shader.PropertyToID("_NoiseGrain");

        public static readonly int PropBaseTop = Shader.PropertyToID("_BaseTop");
        public static readonly int PropBaseBottom = Shader.PropertyToID("_BaseBottom");
        public static readonly int PropSheenColor = Shader.PropertyToID("_SheenColor");
        public static readonly int PropSheenSpeed = Shader.PropertyToID("_SheenSpeed");
        public static readonly int PropSheenWidth = Shader.PropertyToID("_SheenWidth");
        public static readonly int PropPressAmount = Shader.PropertyToID("_PressAmount");

        public static readonly int PropFill = Shader.PropertyToID("_Fill");
        public static readonly int PropTrailFill = Shader.PropertyToID("_TrailFill");
        public static readonly int PropSegments = Shader.PropertyToID("_Segments");
        public static readonly int PropFillColor = Shader.PropertyToID("_FillColor");
        public static readonly int PropTrailColor = Shader.PropertyToID("_TrailColor");
        public static readonly int PropTrackColor = Shader.PropertyToID("_TrackColor");
        public static readonly int PropMeniscusGlow = Shader.PropertyToID("_MeniscusGlow");
        public static readonly int PropWaveSpeed = Shader.PropertyToID("_WaveSpeed");
        public static readonly int PropLowPulse = Shader.PropertyToID("_LowPulse");

        public static readonly int PropCooldown01 = Shader.PropertyToID("_Cooldown01");
        public static readonly int PropArc01 = Shader.PropertyToID("_Arc01");
        public static readonly int PropRimColor = Shader.PropertyToID("_RimColor");
        public static readonly int PropRimGlow = Shader.PropertyToID("_RimGlow");
        public static readonly int PropGlassTint = Shader.PropertyToID("_GlassTint");
        public static readonly int PropInnerRing = Shader.PropertyToID("_InnerRing");
        public static readonly int PropLockedDesat = Shader.PropertyToID("_LockedDesat");
        public static readonly int PropUltPulse = Shader.PropertyToID("_UltPulse");

        public static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        public static readonly int PropSoftness = Shader.PropertyToID("_Softness");
        public static readonly int PropPulseSpeed = Shader.PropertyToID("_PulseSpeed");
        public static readonly int PropReduceMotion = Shader.PropertyToID("_ReduceMotion");

        static readonly Dictionary<string, Shader> ShaderCache = new Dictionary<string, Shader>(8, StringComparer.Ordinal);
        static readonly Dictionary<string, Material> StaticMaterialCache = new Dictionary<string, Material>(32, StringComparer.Ordinal);

        /// <summary>Clears cached shader &amp; material lookups (used on test teardown / domain reset).</summary>
        public static void ClearCache()
        {
            ShaderCache.Clear();
            StaticMaterialCache.Clear();
        }

        /// <summary>Returns the current <c>_ReduceMotion</c> uniform flag (<c>1</c> when ReduceMotion is enabled, else <c>0</c>).</summary>
        public static float ReduceMotionFlag()
        {
            return SettingsStore.Current != null && SettingsStore.Current.ReduceMotion ? 1f : 0f;
        }

        /// <summary>Synchronizes <c>_ReduceMotion</c> on a UI shader material without allocations.</summary>
        public static void SyncReduceMotion(Material mat)
        {
            if (mat == null || mat == Graphic.defaultGraphicMaterial) return;
            if (mat.HasProperty(PropReduceMotion))
                mat.SetFloat(PropReduceMotion, ReduceMotionFlag());
        }

        /// <summary>
        /// Creates or returns a cached A1/A2 smoked-obsidian glass panel material (<c>Kalyeah/UI_TacticalGlass</c>)
        /// with crisp chamfered SDF edges, optional paper-white left accent tick, and 1 px brushed-titanium top catchlight.
        /// </summary>
        public static Material CreateTacticalGlass(
            Color fill,
            Color border,
            Color leftAccent,
            float cornerCut = 4f,
            float accentW = 3f,
            float topHighlight = 0.14f)
        {
            string key = $"glass|{PackColor(fill)}|{PackColor(border)}|{PackColor(leftAccent)}|{Mathf.RoundToInt(cornerCut * 10f)}|{Mathf.RoundToInt(accentW * 10f)}|{Mathf.RoundToInt(topHighlight * 100f)}";
            if (StaticMaterialCache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader shader = ResolveShader("UI_TacticalGlass", TacticalGlassShader);
            if (shader == null) return FallbackMaterial();

            var mat = new Material(shader)
            {
                name = "Kalyeah_UI_TacticalGlass",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetColor(PropColor, new Color(fill.r, fill.g, fill.b, 1f));
            mat.SetFloat(PropGlassAlpha, Mathf.Clamp01(fill.a));
            mat.SetVector(PropRectSize, Vector4.zero);
            mat.SetFloat(PropCornerCut, Mathf.Max(0f, cornerCut));
            mat.SetFloat(PropBorderWidth, border.a > 0.001f ? 1f : 0f);
            mat.SetColor(PropBorderColor, border);
            mat.SetFloat(PropTopHighlight, Mathf.Clamp01(topHighlight));
            mat.SetColor(PropAccentColor, leftAccent);
            mat.SetFloat(PropAccentWidth, Mathf.Max(0f, accentW));
            mat.SetFloat(PropNoiseGrain, 0.022f);

            StaticMaterialCache[key] = mat;
            return mat;
        }

        /// <summary>
        /// Creates a per-disc G7 A2 frosted smoked-glass + brushed-titanium circular skill disc material
        /// (<c>Kalyeah/UI_RadialSkillDisc</c>) so each ability button animates <c>_Cooldown01</c>, <c>_Arc01</c>,
        /// <c>_LockedDesat</c>, and <c>_UltPulse</c> independently in 1 draw call.
        /// </summary>
        public static Material CreateFrostedTitaniumDisc(Color glassTint, Color rimColor, float innerRing = 0.78f)
        {
            Shader shader = ResolveShader("UI_RadialSkillDisc", RadialSkillDiscShader);
            if (shader == null) return FallbackMaterial();

            var mat = new Material(shader)
            {
                name = "Kalyeah_UI_RadialSkillDisc",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetColor(PropColor, Color.white);
            mat.SetFloat(PropCooldown01, 0f);
            mat.SetFloat(PropArc01, 0f);
            mat.SetColor(PropRimColor, rimColor);
            mat.SetFloat(PropRimGlow, 0.28f);
            mat.SetColor(PropGlassTint, glassTint);
            mat.SetFloat(PropInnerRing, Mathf.Clamp(innerRing, 0f, 0.92f));
            mat.SetFloat(PropLockedDesat, 0f);
            mat.SetFloat(PropUltPulse, 0f);
            mat.SetFloat(PropReduceMotion, ReduceMotionFlag());
            return mat;
        }

        /// <summary>
        /// Creates a sharp chamfered primary CTA material (<c>Kalyeah/UI_MetallicSheen</c>, e.g. A1 Signal Vermilion PLAY / LOCK IN)
        /// with GPU diagonal titanium sheen sweep and tactile <c>_PressAmount</c> response.
        /// </summary>
        public static Material CreateMetallicCta(Color top, Color bottom, float cornerCut = 10f)
        {
            Shader shader = ResolveShader("UI_MetallicSheen", MetallicSheenShader);
            if (shader == null) return FallbackMaterial();

            var mat = new Material(shader)
            {
                name = "Kalyeah_UI_MetallicSheen",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetColor(PropColor, Color.white);
            mat.SetVector(PropRectSize, Vector4.zero);
            mat.SetFloat(PropCornerCut, Mathf.Max(0f, cornerCut));
            mat.SetColor(PropBaseTop, top);
            mat.SetColor(PropBaseBottom, bottom);
            mat.SetColor(PropSheenColor, new Color(1f, 0.96f, 0.94f, 0.44f));
            mat.SetFloat(PropSheenSpeed, 0.85f);
            mat.SetFloat(PropSheenWidth, 0.14f);
            mat.SetFloat(PropPressAmount, 0f);
            mat.SetFloat(PropReduceMotion, ReduceMotionFlag());
            return mat;
        }

        /// <summary>
        /// Creates a single-draw-call tactical vitals gauge material (<c>Kalyeah/UI_LiquidGauge</c>) for
        /// G5 A1 4-segment Paper-White HP + amber delayed drain and glowing animated Cyan Water bars.
        /// </summary>
        public static Material CreateLiquidGauge(Color fillColor, Color trailColor, int segments, bool animatedLiquid)
        {
            Shader shader = ResolveShader("UI_LiquidGauge", LiquidGaugeShader);
            if (shader == null) return FallbackMaterial();

            var mat = new Material(shader)
            {
                name = "Kalyeah_UI_LiquidGauge",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetColor(PropColor, Color.white);
            mat.SetFloat(PropFill, 1f);
            mat.SetFloat(PropTrailFill, 1f);
            mat.SetFloat(PropSegments, Mathf.Max(0, segments));
            mat.SetColor(PropFillColor, fillColor);
            mat.SetColor(PropTrailColor, trailColor);
            mat.SetColor(PropTrackColor, new Color(0.055f, 0.075f, 0.102f, 0.74f));
            mat.SetFloat(PropMeniscusGlow, animatedLiquid ? 0.85f : 0.45f);
            mat.SetFloat(PropWaveSpeed, animatedLiquid ? 1.15f : 0f);
            mat.SetFloat(PropLowPulse, 0f);
            mat.SetFloat(PropReduceMotion, ReduceMotionFlag());
            return mat;
        }

        /// <summary>
        /// Creates or returns a cached additive optical radial halo material (<c>Kalyeah/UI_SoftGlow</c>)
        /// for Ult-ready auras, active selections, and coral-gold hit confirms.
        /// </summary>
        public static Material CreateSoftGlow(Color glowColor)
        {
            string key = $"glow|{PackColor(glowColor)}|{Mathf.RoundToInt(ReduceMotionFlag())}";
            if (StaticMaterialCache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader shader = ResolveShader("UI_SoftGlow", SoftGlowShader);
            if (shader == null) return FallbackMaterial();

            var mat = new Material(shader)
            {
                name = "Kalyeah_UI_SoftGlow",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetColor(PropColor, Color.white);
            mat.SetColor(PropGlowColor, glowColor);
            mat.SetFloat(PropSoftness, 2.35f);
            mat.SetFloat(PropPulseSpeed, 2.6f);
            mat.SetFloat(PropReduceMotion, ReduceMotionFlag());

            StaticMaterialCache[key] = mat;
            return mat;
        }

        /// <summary>
        /// Attaches (or reconfigures) tactile <see cref="UiPressSpring"/> squash + press-lip dip choreography on
        /// <paramref name="target"/>. Safe with null targets, strictly EditMode-inert, and <c>ReduceMotion</c>-aware.
        /// </summary>
        public static UiPressSpring AttachPressSpring(Selectable target, float pressScale = 0.94f, float lipDipPx = 3f)
        {
            if (target == null) return null;
            UiPressSpring spring = target.GetComponent<UiPressSpring>();
            if (spring == null) spring = target.gameObject.AddComponent<UiPressSpring>();
            spring.Configure(target, pressScale, lipDipPx);
            return spring;
        }

        static Shader ResolveShader(string presetName, string shaderName)
        {
            if (ShaderCache.TryGetValue(shaderName, out Shader cached) && cached != null && cached.isSupported)
                return cached;

            Material preset = Resources.Load<Material>("KalyeahMaterials/" + presetName);
            Shader resolved = preset != null && preset.shader != null && preset.shader.isSupported
                ? preset.shader
                : Shader.Find(shaderName);

            if (resolved != null && resolved.isSupported)
            {
                ShaderCache[shaderName] = resolved;
                return resolved;
            }
            return null;
        }

        static Material FallbackMaterial()
        {
            Material mat = Graphic.defaultGraphicMaterial;
            return mat != null ? mat : Canvas.GetDefaultCanvasMaterial();
        }

        static uint PackColor(Color c)
        {
            uint r = (uint)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            uint g = (uint)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            uint b = (uint)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            uint a = (uint)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }
    }
}
