using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Procedural 9-slice street-komiks rounded-rect &amp; ink-bordered <see cref="Sprite"/> cache plus colorblind-aware
    /// (<see cref="SettingsStore.ColorblindMode"/> 0..3) team, health, stamina, and Merienda palette helpers.
    /// Zero per-frame allocations once sprites are cached.
    /// </summary>
    public static class KomiksUi
    {
        public const int RadiusS = 8;
        public const int RadiusM = 16;
        public const int RadiusL = 24;
        public const int RadiusXL = 32;

        public const float StaminaLowThreshold = 0.25f;
        public const float ExhaustBlinkPeriod = 0.25f;

        // Sunlit 90s Philippine Kalye signboard & HUD palette (matches scripts/ui/komiks_style.gd & overhead_hud.gd)
        public static readonly Color SunlitCream = new Color(0.980f, 0.953f, 0.890f, 1f);
        public static readonly Color CreamShade = new Color(0.949f, 0.886f, 0.769f, 1f);
        public static readonly Color SlateEspresso = new Color(0.165f, 0.145f, 0.133f, 1f);
        public static readonly Color EspressoRaised = new Color(0.220f, 0.192f, 0.176f, 1f);
        public static readonly Color MangoAmber = new Color(0.961f, 0.651f, 0.137f, 1f);
        public static readonly Color MangoGold = new Color(1.000f, 0.831f, 0.400f, 1f);
        public static readonly Color JadeTeal = new Color(0.122f, 0.616f, 0.545f, 1f);
        public static readonly Color TerracottaCoral = new Color(0.886f, 0.333f, 0.243f, 1f);
        public static readonly Color NarraWood = new Color(0.478f, 0.259f, 0.114f, 1f);
        public static readonly Color SandBorder = new Color(0.851f, 0.773f, 0.639f, 1f);

        public static readonly Color InkOutline = new Color(0.060f, 0.080f, 0.130f, 0.92f);
        public static readonly Color SoftBorder = new Color(0.100f, 0.110f, 0.140f, 0.68f);
        public static readonly Color EmptyBar = new Color(0.180f, 0.190f, 0.210f, 0.92f);
        public static readonly Color RadarGlass = new Color(0.060f, 0.080f, 0.110f, 0.62f);

        // Standard (Mode 0) team & bar colors
        static readonly Color StandardOwn = new Color(0.130f, 0.520f, 1.000f, 1f);
        static readonly Color StandardAlly = new Color(0.350f, 0.650f, 1.000f, 1f);
        static readonly Color StandardEnemy = new Color(1.000f, 0.240f, 0.270f, 1f);
        static readonly Color StandardSelfBar = new Color(0.350f, 0.950f, 0.350f, 1f);

        // Protanopia (Mode 1) — Okabe-Ito blue vs orange + near-white self bar
        static readonly Color ProtanOwn = new Color(0.000f, 0.447f, 0.698f, 1f);
        static readonly Color ProtanAlly = new Color(0.280f, 0.660f, 0.900f, 1f);
        static readonly Color ProtanEnemy = new Color(0.878f, 0.482f, 0.000f, 1f);

        // Deuteranopia (Mode 2) — high-separation cobalt blue vs warm vermilion-amber + near-white self bar
        static readonly Color DeutanOwn = new Color(0.040f, 0.500f, 0.960f, 1f);
        static readonly Color DeutanAlly = new Color(0.340f, 0.710f, 0.960f, 1f);
        static readonly Color DeutanEnemy = new Color(0.890f, 0.560f, 0.000f, 1f);

        // Tritanopia (Mode 3) — teal-cyan vs vivid crimson-rose + near-white self bar
        static readonly Color TritanOwn = new Color(0.000f, 0.650f, 0.720f, 1f);
        static readonly Color TritanAlly = new Color(0.260f, 0.820f, 0.880f, 1f);
        static readonly Color TritanEnemy = new Color(0.900f, 0.180f, 0.420f, 1f);

        static readonly Color ColorblindSelfBar = new Color(0.960f, 0.960f, 0.960f, 1f);

        static readonly Color StaminaFullBlue = new Color(0.220f, 0.520f, 1.000f, 1f);
        static readonly Color StaminaWarnAmber = new Color(0.960f, 0.650f, 0.140f, 1f);
        static readonly Color StaminaExhaustRed = new Color(0.950f, 0.240f, 0.240f, 1f);

        static readonly Color BuffPalamigCyan = new Color(0.280f, 0.880f, 0.980f, 1f);
        static readonly Color BuffTeksGold = new Color(1.000f, 0.760f, 0.240f, 1f);
        static readonly Color BuffTuronGreen = new Color(0.340f, 0.920f, 0.580f, 1f);

        readonly struct SpriteKey : IEquatable<SpriteKey>
        {
            public readonly int Size;
            public readonly int Radius;
            public readonly int Border;
            public readonly uint FillRgba;
            public readonly uint BorderRgba;
            public readonly byte Flags;

            public SpriteKey(int size, int radius, int border, Color32 fill, Color32 stroke, byte flags)
            {
                Size = size;
                Radius = radius;
                Border = border;
                FillRgba = Pack(fill);
                BorderRgba = Pack(stroke);
                Flags = flags;
            }

            static uint Pack(Color32 c) => (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);

            public bool Equals(SpriteKey other) =>
                Size == other.Size &&
                Radius == other.Radius &&
                Border == other.Border &&
                FillRgba == other.FillRgba &&
                BorderRgba == other.BorderRgba &&
                Flags == other.Flags;

            public override bool Equals(object obj) => obj is SpriteKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Size;
                    h = (h * 397) ^ Radius;
                    h = (h * 397) ^ Border;
                    h = (h * 397) ^ (int)FillRgba;
                    h = (h * 397) ^ (int)BorderRgba;
                    h = (h * 397) ^ Flags;
                    return h;
                }
            }
        }

        static readonly Dictionary<SpriteKey, Sprite> SpriteCache = new Dictionary<SpriteKey, Sprite>(32);

        /// <summary>Rounded-rect bar radius: one quarter of bar height, clamped to [2..8] px.</summary>
        public static int BarRadius(float barHeightPx) => Mathf.Clamp(Mathf.RoundToInt(barHeightPx * 0.25f), 2, RadiusS);

        public static int ResolveColorblindMode(int? overrideMode = null)
        {
            int raw = overrideMode ?? SettingsStore.Current.ColorblindMode;
            return Mathf.Clamp(raw, 0, 3);
        }

        public static Color TeamOwnColor(int? colorblindMode = null)
        {
            switch (ResolveColorblindMode(colorblindMode))
            {
                case 1: return ProtanOwn;
                case 2: return DeutanOwn;
                case 3: return TritanOwn;
                default: return StandardOwn;
            }
        }

        public static Color TeamAllyColor(int? colorblindMode = null)
        {
            switch (ResolveColorblindMode(colorblindMode))
            {
                case 1: return ProtanAlly;
                case 2: return DeutanAlly;
                case 3: return TritanAlly;
                default: return StandardAlly;
            }
        }

        public static Color TeamEnemyColor(int? colorblindMode = null)
        {
            switch (ResolveColorblindMode(colorblindMode))
            {
                case 1: return ProtanEnemy;
                case 2: return DeutanEnemy;
                case 3: return TritanEnemy;
                default: return StandardEnemy;
            }
        }

        public static Color TeamColor(int team, int localTeam = SimBridge.LocalTeam, int? colorblindMode = null) =>
            team == localTeam ? TeamOwnColor(colorblindMode) : TeamEnemyColor(colorblindMode);

        public static Color ScoreColor(int team, int localTeam = SimBridge.LocalTeam, int? colorblindMode = null) =>
            TeamColor(team, localTeam, colorblindMode);

        public static Color CaptureTintColor(int team, int localTeam = SimBridge.LocalTeam, int? colorblindMode = null) =>
            Color.Lerp(TeamColor(team, localTeam, colorblindMode), Color.white, 0.45f);

        public static Color SelfHealthColor(int? colorblindMode = null) =>
            ResolveColorblindMode(colorblindMode) == 0 ? StandardSelfBar : ColorblindSelfBar;

        public static Color AllyHealthColor(int? colorblindMode = null) => TeamAllyColor(colorblindMode);

        public static Color StaminaColor(float ratio01, bool overclocked, float exhaustLeft, bool reduceMotion, out float displayRatio01)
        {
            if (overclocked && exhaustLeft > 0f)
            {
                bool lit = reduceMotion || (Mathf.FloorToInt(exhaustLeft / ExhaustBlinkPeriod) % 2 == 0);
                displayRatio01 = lit ? 1f : 0f;
                return StaminaExhaustRed;
            }

            displayRatio01 = Mathf.Clamp01(ratio01);
            if (overclocked || displayRatio01 < StaminaLowThreshold)
                return StaminaWarnAmber;
            return StaminaFullBlue;
        }

        public static Color MeriendaColor(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Palamig: return BuffPalamigCyan;
                case BuffKind.Teks: return BuffTeksGold;
                case BuffKind.Turon: return BuffTuronGreen;
                default: return MangoGold;
            }
        }

        /// <summary>Cached 9-slice anti-aliased solid white rounded-rect sprite (tintable via Image.color).</summary>
        public static Sprite RoundedSprite(int radiusPx = RadiusS, int sizePx = 64)
        {
            return BorderedSprite(radiusPx, 0, Color.white, Color.clear, topGloss: false, sizePx: sizePx);
        }

        /// <summary>Cached 9-slice street-komiks rounded-rect sprite with an anti-aliased ink border and optional upper gloss band.</summary>
        public static Sprite BorderedSprite(
            int radiusPx = RadiusS,
            int borderPx = 2,
            Color? fill = null,
            Color? border = null,
            bool topGloss = false,
            int sizePx = 64)
        {
            int size = Mathf.Clamp(sizePx, 24, 256);
            int maxR = (size / 2) - 2;
            int r = Mathf.Clamp(radiusPx, 1, Mathf.Max(1, maxR));
            int b = Mathf.Clamp(borderPx, 0, Mathf.Max(0, r));
            Color32 fill32 = fill ?? Color.white;
            Color32 stroke32 = b > 0 ? (Color32)(border ?? SlateEspresso) : new Color32(0, 0, 0, 0);
            byte flags = topGloss ? (byte)1 : (byte)0;

            var key = new SpriteKey(size, r, b, fill32, stroke32, flags);
            if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"KomiksRect_{size}_r{r}_b{b}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float innerHalf = half - 1f - r;
            Color fillF = fill32;
            Color strokeF = stroke32;

            for (int y = 0; y < size; y++)
            {
                bool inTopGloss = topGloss && y >= (size * 0.56f);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) - innerHalf;
                    float dy = Mathf.Abs(y + 0.5f - half) - innerHalf;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                    float sdf = outside + inside - r;

                    float outerAlpha = Mathf.Clamp01(0.5f - sdf);
                    if (outerAlpha <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color body = inTopGloss ? Color.Lerp(fillF, Color.white, 0.14f) : fillF;
                    Color outColor;
                    if (b > 0)
                    {
                        float innerEdge = Mathf.Clamp01(0.5f - (sdf + b));
                        outColor = Color.Lerp(strokeF, body, innerEdge);
                    }
                    else
                    {
                        outColor = body;
                    }

                    outColor.a *= outerAlpha;
                    pixels[y * size + x] = outColor;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            float slice = Mathf.Min(half - 1f, r + b + 2f);
            var borderVec = new Vector4(slice, slice, slice, slice);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, borderVec);
            sprite.name = tex.name;
            SpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>Cached crisp circular ring sprite for radar rings, objective halos, and progress callouts.</summary>
        public static Sprite RingSprite(int sizePx = 128, int thicknessPx = 3)
        {
            int size = Mathf.Clamp(sizePx, 32, 256);
            int thick = Mathf.Clamp(thicknessPx, 1, size / 4);
            var key = new SpriteKey(size, size / 2, thick, new Color32(0, 0, 0, 0), new Color32(255, 255, 255, 255), 2);
            if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"KomiksRing_{size}_t{thick}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float center = size * 0.5f;
            float outerR = center - 1f;
            float innerR = Mathf.Max(0f, outerR - thick);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float aOuter = Mathf.Clamp01(outerR + 0.5f - d);
                    float aInner = Mathf.Clamp01(d - (innerR - 0.5f));
                    byte a = (byte)Mathf.RoundToInt(aOuter * aInner * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            SpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>Creates a non-raycast 9-slice komiks panel Image with baked ink border.</summary>
        public static Image MakeInkPanel(
            string name,
            Transform parent,
            Color fill,
            Color? border = null,
            int radiusPx = RadiusM,
            int borderPx = 2,
            bool topGloss = false,
            bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = BorderedSprite(radiusPx, borderPx, fill, border ?? SlateEspresso, topGloss);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Creates a tintable 9-slice rounded-rect Image (uses white sprite so Image.color drives fill tint with 0 allocations).</summary>
        public static Image MakeSlicedFill(string name, Transform parent, Color color, int radiusPx = RadiusS, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSprite(radiusPx);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }
    }
}
