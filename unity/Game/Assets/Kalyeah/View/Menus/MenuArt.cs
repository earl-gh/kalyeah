using System.Collections.Generic;
using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// The menus' art, drawn in code (no picture files): the Kalyeah colours, one plate shape with cut corners (the "tag"), soft
    /// glows, a sunburst, a Filipino lozenge weave and a few icon glyphs. Every texture is made once, cached and flagged so scene
    /// loads keep it. Brand lives in colour, type, pattern and shape: the name and logo show only on the splash (docs/SPEC.md §8).
    /// </summary>
    public static class MenuArt
    {
        // the palette: night teal, jeepney gold, banig teal, cream paper, sign-painter coral, bayong wood
        public static readonly Color Ink = new Color(0.055f, 0.075f, 0.095f, 1f);
        public static readonly Color NightTop = new Color(0.043f, 0.205f, 0.243f, 1f);
        public static readonly Color NightMid = new Color(0.031f, 0.118f, 0.157f, 1f);
        public static readonly Color NightBottom = new Color(0.024f, 0.047f, 0.063f, 1f);
        public static readonly Color Teal = new Color(0.13f, 0.68f, 0.62f, 1f);
        public static readonly Color TealLight = new Color(0.52f, 0.90f, 0.84f, 1f);
        public static readonly Color Cream = new Color(0.98f, 0.93f, 0.80f, 1f);
        public static readonly Color Gold = new Color(0.98f, 0.70f, 0.12f, 1f);
        public static readonly Color GoldLight = new Color(1f, 0.87f, 0.42f, 1f);
        public static readonly Color GoldDeep = new Color(0.88f, 0.48f, 0.05f, 1f);
        public static readonly Color Coral = new Color(0.90f, 0.33f, 0.24f, 1f);
        public static readonly Color WoodLight = new Color(0.58f, 0.37f, 0.20f, 1f);
        public static readonly Color WoodDark = new Color(0.34f, 0.20f, 0.11f, 1f);
        public static readonly Color GlassTop = new Color(0.10f, 0.26f, 0.31f, 0.94f);
        public static readonly Color GlassBottom = new Color(0.035f, 0.10f, 0.135f, 0.96f);
        public static readonly Color Rim = new Color(0.62f, 0.92f, 0.88f, 0.42f);

        public enum GlyphKind { Gear, Heart, Clock, Play, Diamond }

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Texture2D NewTexture(string name, int width, int height, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        static Texture2D Finish(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // ---------------------------------------------------------------- plates

        /// <summary>
        /// The Kalyeah tag: a rectangle with the top-left and bottom-right corners cut, a vertical gradient fill, a border and a thin
        /// light line just inside its top edge. 9-sliced, so use it on an Image with type Sliced. Tint a white plate to recolour it.
        /// </summary>
        public static Sprite Plate(Color top, Color bottom, Color border, int cut = 18, int borderPx = 3, float shine = 0.35f)
        {
            string key = $"plate|{top}|{bottom}|{border}|{cut}|{borderPx}|{shine}";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            int slice = cut + 2;
            int size = slice * 2 + 8;
            float w = size;
            float h = size;
            float c = cut;
            const float inv = 0.70710678f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    float d = Mathf.Min(Mathf.Min(fx, w - fx), Mathf.Min(fy, h - fy));
                    d = Mathf.Min(d, (fx + (h - fy) - c) * inv);       // top-left cut
                    d = Mathf.Min(d, ((w - fx) + fy - c) * inv);       // bottom-right cut
                    Color fill = Color.Lerp(bottom, top, fy / h);
                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f)
                    {
                        pixels[y * size + x] = new Color32((byte)(fill.r * 255f), (byte)(fill.g * 255f), (byte)(fill.b * 255f), 0);
                        continue;
                    }
                    float inBorder = borderPx <= 0 ? 0f : 1f - Mathf.Clamp01(d - borderPx + 0.5f);
                    Color col = Color.Lerp(fill, border, inBorder);
                    float line = shine * (1f - Mathf.Clamp01(Mathf.Abs(d - (borderPx + 1.5f)) / 1.2f)) * Mathf.Clamp01((fy / h - 0.45f) * 4f);
                    col = Color.Lerp(col, new Color(1f, 1f, 1f, col.a), line);
                    col.a *= cover;
                    pixels[y * size + x] = (Color32)col;
                }
            }

            Texture2D tex = Finish(NewTexture("KalyeahPlate", size, size, TextureWrapMode.Clamp), pixels);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(slice, slice, slice, slice));
            sprite.name = "KalyeahPlate";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>A flat white tag, for hard drop shadows and tinted fills (same shape as <see cref="Plate"/>).</summary>
        public static Sprite Solid(int cut = 18) => Plate(Color.white, Color.white, Color.white, cut, 0, 0f);

        /// <summary>The dark glass plate with a light rim: the base of every card and tile.</summary>
        public static Sprite Glass(int cut = 18) => Plate(GlassTop, GlassBottom, Rim, cut, 2, 0.22f);

        /// <summary>A white plate that is lighter at the top; tint it with <see cref="UnityEngine.UI.Graphic.color"/> for chips and bars.</summary>
        public static Sprite Chip(int cut = 10) => Plate(Color.white, new Color(0.70f, 0.70f, 0.70f, 1f), Ink, cut, 2, 0.4f);

        // ---------------------------------------------------------------- light and pattern

        /// <summary>A soft white radial glow (alpha falls off to zero at the edge).</summary>
        public static Sprite Glow()
        {
            const string key = "glow";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;
            const int size = 128;
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - centre) * (x + 0.5f - centre) + (y + 0.5f - centre) * (y + 0.5f - centre)) / centre;
                    float a = Mathf.Pow(1f - Mathf.Clamp01(r), 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            Texture2D tex = Finish(NewTexture("KalyeahGlow", size, size, TextureWrapMode.Clamp), pixels);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>Alternating sun rays fading out from the centre (the sun of the Philippine flag), white with alpha.</summary>
        public static Texture2D Sunburst()
        {
            const string key = "sunburst";
            if (Textures.TryGetValue(key, out Texture2D hit) && hit != null) return hit;
            const int size = 512;
            const float pairs = 12f;
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - centre) / centre;
                    float dy = (y + 0.5f - centre) / centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (r < 1f)
                    {
                        float s = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * pairs, 1f);
                        float ray = Smooth(0f, 0.03f, s) * (1f - Smooth(0.47f, 0.5f, s));
                        a = ray * (1f - Smooth(0.2f, 1f, r)) * Smooth(0.03f, 0.22f, r);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            Texture2D tex = Finish(NewTexture("KalyeahSunburst", size, size, TextureWrapMode.Clamp), pixels);
            Textures[key] = tex;
            return tex;
        }

        /// <summary>A tileable lattice of nested lozenges, like a woven banig or inabel cloth. White with alpha; tile it with a RawImage uvRect.</summary>
        public static Texture2D Weave()
        {
            const string key = "weave";
            if (Textures.TryGetValue(key, out Texture2D hit) && hit != null) return hit;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    float d = Mathf.Abs(fx - 32f) + Mathf.Abs(fy - 32f);   // lozenges around the tile centre
                    float a = Line(d, 32f, 1.1f);
                    a = Mathf.Max(a, 0.6f * Line(d, 18f, 1.1f));
                    a = Mathf.Max(a, 0.9f * Mathf.Clamp01(5f - d));
                    float corner = Mathf.Min(fx, size - fx) + Mathf.Min(fy, size - fy); // the small lozenge where four tiles meet
                    a = Mathf.Max(a, 0.7f * Mathf.Clamp01(4.5f - corner));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            Texture2D tex = Finish(NewTexture("KalyeahWeave", size, size, TextureWrapMode.Repeat), pixels);
            Textures[key] = tex;
            return tex;
        }

        static float Line(float d, float at, float halfWidth) => Mathf.Clamp01(halfWidth - Mathf.Abs(d - at) + 0.5f);

        /// <summary>A dark radial vignette: clear in the middle, near-black in the corners.</summary>
        public static Texture2D Vignette()
        {
            const string key = "vignette";
            if (Textures.TryGetValue(key, out Texture2D hit) && hit != null) return hit;
            const int size = 128;
            var pixels = new Color32[size * size];
            float centre = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - centre) * (x + 0.5f - centre) + (y + 0.5f - centre) * (y + 0.5f - centre)) / centre;
                    float a = Smooth(0.5f, 1.3f, r) * 0.9f;
                    pixels[y * size + x] = new Color32(0, 6, 9, (byte)(a * 255f));
                }
            Texture2D tex = Finish(NewTexture("KalyeahVignette", size, size, TextureWrapMode.Clamp), pixels);
            Textures[key] = tex;
            return tex;
        }

        /// <summary>The night-teal backdrop: a vertical ramp from the top glow to the dark floor.</summary>
        public static Texture2D Sky()
        {
            const string key = "sky";
            if (Textures.TryGetValue(key, out Texture2D hit) && hit != null) return hit;
            const int height = 128;
            var pixels = new Color32[height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);   // 0 bottom, 1 top
                Color c = t > 0.5f
                    ? Color.Lerp(NightMid, NightTop, Smooth(0.5f, 1f, t))
                    : Color.Lerp(NightBottom, NightMid, Smooth(0f, 0.5f, t));
                pixels[y] = (Color32)c;
            }
            Texture2D tex = Finish(NewTexture("KalyeahSky", 1, height, TextureWrapMode.Clamp), pixels);
            Textures[key] = tex;
            return tex;
        }

        // ---------------------------------------------------------------- glyphs

        /// <summary>A white icon (supersampled 4x4 for smooth edges); tint it with the Image colour.</summary>
        public static Sprite Glyph(GlyphKind kind)
        {
            string key = "glyph|" + kind;
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;
            const int size = 64;
            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                            if (Inside(kind, u, v)) inside++;
                        }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / (samples * samples)));
                }
            Texture2D tex = Finish(NewTexture("KalyeahGlyph_" + kind, size, size, TextureWrapMode.Clamp), pixels);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>Whether (u, v) in -1..1 (v up) is inside the glyph.</summary>
        static bool Inside(GlyphKind kind, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (kind)
            {
                case GlyphKind.Gear:
                {
                    float edge = Mathf.Sin(8f * Mathf.Atan2(v, u)) > -0.25f ? 0.90f : 0.72f;   // eight teeth
                    return r < edge && r > 0.30f;
                }
                case GlyphKind.Heart:
                {
                    float x = u * 1.25f;
                    float y = v * 1.25f + 0.15f;
                    float a = x * x + y * y - 1f;
                    return a * a * a - x * x * y * y * y <= 0f;
                }
                case GlyphKind.Clock:
                {
                    bool ring = r > 0.64f && r < 0.92f;
                    bool minute = Mathf.Abs(u) < 0.07f && v > -0.06f && v < 0.46f;
                    bool hour = Mathf.Abs(v) < 0.07f && u > -0.06f && u < 0.32f;
                    return ring || minute || hour || r < 0.11f;
                }
                case GlyphKind.Play:
                    return u > -0.45f && u < 0.8f && Mathf.Abs(v) < 0.75f * (0.8f - u) / 1.25f;
                case GlyphKind.Diamond:
                    return Mathf.Abs(u) + Mathf.Abs(v) < 0.9f;
                default:
                    return false;
            }
        }
    }
}
