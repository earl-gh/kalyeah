using System.Collections.Generic;
using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// The menus' art, drawn in code (no picture files): the Kalyeah colours, tactical cut-corner and rounded glass plates,
    /// luminous beveled CTAs, embossed currency and toggle sprites, spotlights, scrims and crisp supersampled glyphs.
    /// Every texture is made once, cached and flagged so scene loads keep it. Brand lives in colour, type, pattern and shape:
    /// the name and logo show only on the splash (docs/SPEC.md §8).
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
        public static readonly Color Gold = new Color(0.961f, 0.725f, 0.227f, 1f);   // #F5B93A, palette 1
        public static readonly Color GoldLight = new Color(1f, 0.886f, 0.627f, 1f);  // #FFE2A0
        public static readonly Color GoldDeep = new Color(0.788f, 0.541f, 0.071f, 1f); // #C98A12
        public static readonly Color Coral = new Color(0.90f, 0.33f, 0.24f, 1f);
        public static readonly Color WoodLight = new Color(0.58f, 0.37f, 0.20f, 1f);
        public static readonly Color WoodDark = new Color(0.34f, 0.20f, 0.11f, 1f);
        public static readonly Color GlassTop = new Color(0.10f, 0.26f, 0.31f, 0.94f);
        public static readonly Color GlassBottom = new Color(0.035f, 0.10f, 0.135f, 0.96f);
        public static readonly Color Rim = new Color(0.62f, 0.92f, 0.88f, 0.42f);

        // tactical AAA menu surfaces & accents (neutral Ink/Paper/Gold/Teal/Coral — zero role colours per SPEC §10)
        public static readonly Color PanelTop = new Color(0.082f, 0.133f, 0.212f, 0.92f);
        public static readonly Color PanelBottom = new Color(0.031f, 0.055f, 0.102f, 0.96f);
        public static readonly Color PanelBorder = new Color(0.76f, 0.88f, 0.96f, 0.24f);
        public static readonly Color InputBg = new Color(0.035f, 0.055f, 0.095f, 0.96f);
        public static readonly Color InputBorder = new Color(0.70f, 0.82f, 0.92f, 0.30f);
        public static readonly Color GoldBorder = new Color(1.00f, 0.82f, 0.36f, 0.95f);
        public static readonly Color GoldShadow = new Color(0.52f, 0.30f, 0.02f, 1f);
        public static readonly Color MutedText = new Color(0.70f, 0.77f, 0.84f, 0.90f);
        public static readonly Color DiamondCyan = new Color(0.32f, 0.84f, 1.00f, 1f);
        public static readonly Color SuccessGreen = new Color(0.24f, 0.82f, 0.56f, 1f);

        // Locked Option A1 Editorial + A2 Frosted Titanium + A3 Split Scoreboard & Coral-Gold tokens
        public static readonly Color ObsidianGlass = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.74f);       // #0E131A @ 74%
        public static readonly Color ObsidianSolid = new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.94f);       // #0E131A @ 94%
        public static readonly Color PaperWhite = new Color(245f / 255f, 241f / 255f, 232f / 255f, 1f);          // #F5F1E8
        public static readonly Color SignalVermilion = new Color(255f / 255f, 70f / 255f, 85f / 255f, 1f);       // #FF4655
        public static readonly Color SignalVermilionDeep = new Color(212f / 255f, 49f / 255f, 64f / 255f, 1f);   // #D43140
        public static readonly Color TitaniumRim = new Color(213f / 255f, 223f / 255f, 233f / 255f, 1f);         // #D5DFE9
        public static readonly Color CoralGold = new Color(255f / 255f, 155f / 255f, 106f / 255f, 1f);           // #FF9B6A
        public static readonly Color ValorantRadarRoad = new Color(74f / 255f, 90f / 255f, 102f / 255f, 1f);     // #4A5A66
        public static readonly Color ValorantRadarWall = new Color(180f / 255f, 192f / 255f, 200f / 255f, 1f);   // #B4C0C8
        public static readonly Color ValorantRadarBg = new Color(22f / 255f, 32f / 255f, 40f / 255f, 1f);        // #162028
        public static readonly Color WaterCyan = new Color(0.24f, 0.85f, 0.98f, 1f);
        public static readonly Color AllyBlue = new Color(0.18f, 0.56f, 0.94f, 1f);
        public static readonly Color EnemyRed = new Color(0.92f, 0.27f, 0.33f, 1f);

        public enum GlyphKind
        {
            Gear, Heart, Clock, Play, Diamond, Kid, Cards, Chevron,
            Striker, Defender, Seeker, Controller, Bag, People, Star,
            Check, Close, Lock, Camera, History
        }

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f));
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

        static Sprite MakeCachedSprite(string key, string name, int width, int height, Color32[] pixels, Vector4 border)
        {
            Texture2D tex = Finish(NewTexture(name, width, height, TextureWrapMode.Clamp), pixels);
            Sprite sprite = border == Vector4.zero
                ? Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f)
                : Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = sprite;
            return sprite;
        }

        static float SignedChamferBox(float fx, float fy, float w, float h, float majorCut, float minorCut)
        {
            const float inv = 0.70710678f;
            float d = Mathf.Min(Mathf.Min(fx, w - fx), Mathf.Min(fy, h - fy));
            if (majorCut > 0f)
            {
                d = Mathf.Min(d, (fx + (h - fy) - majorCut) * inv); // top-left cut
                d = Mathf.Min(d, ((w - fx) + fy - majorCut) * inv); // bottom-right cut
            }
            if (minorCut > 0f)
            {
                d = Mathf.Min(d, ((w - fx) + (h - fy) - minorCut) * inv); // top-right micro-cut
                d = Mathf.Min(d, (fx + fy - minorCut) * inv);             // bottom-left micro-cut
            }
            return d;
        }

        static float SignedRoundBox(float fx, float fy, float w, float h, float r)
        {
            float qx = Mathf.Max(r - fx, fx - (w - r), 0f);
            float qy = Mathf.Max(r - fy, fy - (h - r), 0f);
            if (qx > 0f && qy > 0f) return r - Mathf.Sqrt(qx * qx + qy * qy);
            return Mathf.Min(Mathf.Min(fx, w - fx), Mathf.Min(fy, h - fy));
        }

        const int ShapePlate = 0;
        const int ShapeRound = 1;
        const int ShapeChamfer = 2;

        static float PanelSdf(int shape, float fx, float fy, float w, float h, float primary, float minor)
        {
            if (shape == ShapeRound) return SignedRoundBox(fx, fy, w, h, primary);
            return SignedChamferBox(fx, fy, w, h, primary, shape == ShapeChamfer ? minor : 0f);
        }

        static Sprite BuildPanel(string key, string texName, int shape, Color top, Color bottom, Color border, float primary, float minor, int slice, int size, int borderPx, float shine)
        {
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            bool flat = borderPx <= 0 && shine <= 0f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = PanelSdf(shape, fx, fy, size, size, primary, minor);
                    float ny = fy / size;
                    Color fill = Color.Lerp(bottom, top, ny);
                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f)
                    {
                        pixels[y * size + x] = ToColor32(new Color(fill.r, fill.g, fill.b, 0f));
                        continue;
                    }
                    if (flat)
                    {
                        fill.a *= cover;
                        pixels[y * size + x] = ToColor32(fill);
                        continue;
                    }

                    float inBorder = borderPx <= 0 ? 0f : 1f - Mathf.Clamp01(d - borderPx + 0.5f);
                    Color col = Color.Lerp(fill, border, inBorder * border.a);
                    col.a = Mathf.Lerp(fill.a, Mathf.Max(fill.a, border.a), inBorder);

                    float topCatch = shine * (1f - Mathf.Clamp01(Mathf.Abs(d - (borderPx + 1.0f)) / 1.15f)) * Smooth(0.55f, 0.92f, ny);
                    col = Color.Lerp(col, new Color(1f, 0.99f, 0.95f, col.a), topCatch);

                    float bottomRim = shine * 0.28f * (1f - Mathf.Clamp01(Mathf.Abs(d - (borderPx + 1.2f)) / 1.4f)) * (1f - Smooth(0.05f, 0.26f, ny));
                    col = Color.Lerp(col, new Color(0.01f, 0.02f, 0.04f, col.a), bottomRim);

                    col.a *= cover;
                    pixels[y * size + x] = ToColor32(col);
                }
            }

            return MakeCachedSprite(key, texName, size, size, pixels, new Vector4(slice, slice, slice, slice));
        }

        static Sprite BuildOutline(string key, string texName, int shape, Color border, float primary, float minor, int borderPx)
        {
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            int stroke = Mathf.Clamp(borderPx, 1, 8);
            int slice = Mathf.CeilToInt(primary) + stroke + 2;
            int size = slice * 2 + 8;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = PanelSdf(shape, x + 0.5f, y + 0.5f, size, size, primary, minor);
                    float a = Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(stroke - d + 0.5f) * border.a;
                    pixels[y * size + x] = ToColor32(new Color(border.r, border.g, border.b, a));
                }
            }

            return MakeCachedSprite(key, texName, size, size, pixels, new Vector4(slice, slice, slice, slice));
        }

        // ---------------------------------------------------------------- plates & tactical cards

        /// <summary>
        /// The Kalyeah tag: a rectangle with the top-left and bottom-right corners cut, a vertical gradient fill, a border and a
        /// crisp top light-catch. 9-sliced, so use it on an Image with type Sliced. Tint a white plate to recolour it.
        /// </summary>
        public static Sprite Plate(Color top, Color bottom, Color border, int cut = 18, int borderPx = 3, float shine = 0.35f)
        {
            int c = Mathf.Max(0, cut);
            int slice = Mathf.Max(4, c + 2);
            return BuildPanel($"plate|{top}|{bottom}|{border}|{cut}|{borderPx}|{shine}", "KalyeahPlate",
                ShapePlate, top, bottom, border, c, 0f, slice, slice * 2 + 8, borderPx, shine);
        }

        /// <summary>
        /// A rounded panel (the home and Kids look): a rounded rectangle with a vertical gradient, a razor-crisp anti-aliased border,
        /// a top specular light-catch and a subtle grounded bottom shadow rim. 9-sliced, so use it on an Image with type Sliced.
        /// </summary>
        public static Sprite Round(Color top, Color bottom, Color border, int radius = 22, int borderPx = 2, float shine = 0.18f)
        {
            int r = Mathf.Max(0, radius);
            int slice = Mathf.Max(4, r + 2);
            return BuildPanel($"round|{top}|{bottom}|{border}|{radius}|{borderPx}|{shine}", "KalyeahRound",
                ShapeRound, top, bottom, border, r, 0f, slice, slice * 2 + 8, borderPx, shine);
        }

        /// <summary>
        /// 45-degree chamfered tactical card (Valorant × Kalyeah tag craft): major cut on top-left and bottom-right,
        /// crisp micro-bevel on opposite corners, anti-aliased bevel, top specular light-catch and subtle bottom shadow rim.
        /// 9-sliced; use with Image.Type.Sliced.
        /// </summary>
        public static Sprite ChamferCard(Color top, Color bottom, Color border, int cut = 14, int borderPx = 2, float shine = 0.28f)
        {
            int major = Mathf.Clamp(cut, 4, 32);
            int minor = Mathf.Max(3, major / 3);
            int slice = major + 3;
            return BuildPanel($"chamfer|{top}|{bottom}|{border}|{cut}|{borderPx}|{shine}", "KalyeahChamferCard",
                ShapeChamfer, top, bottom, border, major, minor, slice, slice * 2 + 12, borderPx, shine);
        }

        /// <summary>Dark frosted 45-degree chamfered tactical glass card with crisp cool rim and top light-catch.</summary>
        public static Sprite ChamferGlass(int cut = 14) => ChamferCard(PanelTop, PanelBottom, PanelBorder, cut, 2, 0.30f);

        /// <summary>
        /// Transparent-center 45-degree chamfered border outline (9-sliced). Ideal for active highlights, focus frames and
        /// avatar borders without tinting or veiling the content underneath.
        /// </summary>
        public static Sprite ChamferOutline(Color border, int cut = 14, int borderPx = 2)
        {
            int major = Mathf.Clamp(cut, 4, 32);
            return BuildOutline($"chamferOut|{border}|{cut}|{borderPx}", "KalyeahChamferOutline",
                ShapeChamfer, border, major, Mathf.Max(3, major / 3), borderPx);
        }

        /// <summary>
        /// Transparent-center rounded border outline (9-sliced). Ideal for crisp avatar selection rims, focus frames and ghost buttons
        /// without tinting or veiling faces.
        /// </summary>
        public static Sprite RoundOutline(Color border, int radius = 18, int borderPx = 2)
        {
            int r = Mathf.Clamp(radius, 4, 40);
            return BuildOutline($"roundOut|{border}|{radius}|{borderPx}", "KalyeahRoundOutline",
                ShapeRound, border, r, 0f, borderPx);
        }

        /// <summary>
        /// 9-sliced white tactical corner L-bracket overlay (transparent center and mid-edges). Tint with Image.color
        /// (e.g. <see cref="Gold"/> or <see cref="GoldLight"/>) for active/selected cards and hero focus frames.
        /// </summary>
        public static Sprite CornerBrackets(int armPx = 18, int thicknessPx = 3, int insetPx = 0)
        {
            string key = $"brackets|{armPx}|{thicknessPx}|{insetPx}";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            int arm = Mathf.Clamp(armPx, 6, 48);
            int thick = Mathf.Clamp(thicknessPx, 1, 10);
            int inset = Mathf.Clamp(insetPx, 0, 12);
            int slice = arm + inset + 2;
            int size = slice * 2 + 8;
            float notch = thick * 0.65f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    float mx = Mathf.Min(fx, size - fx) - inset;
                    float my = Mathf.Min(fy, size - fy) - inset;

                    float hBar = Mathf.Min(Mathf.Min(mx, arm - mx), Mathf.Min(my, thick - my));
                    float vBar = Mathf.Min(Mathf.Min(my, arm - my), Mathf.Min(mx, thick - mx));
                    float d = Mathf.Max(hBar, vBar);
                    float cornerClip = (mx + my - notch) * 0.70710678f;
                    d = Mathf.Min(d, cornerClip);

                    float a = Mathf.Clamp01(d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            return MakeCachedSprite(key, "KalyeahCornerBrackets", size, size, pixels, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>
        /// Layered luminous gold-amber primary CTA pill (PLAY / LOCK IN / SAVE NAME): crisp top specular lip, grounded 3D bottom
        /// press-lip thickness, deep anchor rim and a subtle seam-free diagonal glossy sheen streak. 9-sliced; use Color.white.
        /// </summary>
        public static Sprite PlayPill(int radius = 18) => BuildBeveledCta(chamfered: false, cornerPx: radius);

        /// <summary>
        /// Layered luminous gold-amber primary CTA in the 45-degree chamfered tactical silhouette: crisp top specular lip, grounded
        /// 3D bottom press-lip thickness and subtle diagonal glossy sheen. 9-sliced; use Color.white.
        /// </summary>
        public static Sprite CtaGold(int cut = 14) => BuildBeveledCta(chamfered: true, cornerPx: cut);

        /// <summary>Refined dark-glass secondary tactical button plate (BACK, RESET DEFAULTS, Match History, ID Camera).</summary>
        public static Sprite CtaGhost(int cut = 12) => ChamferCard(
            new Color(0.115f, 0.175f, 0.265f, 0.88f),
            new Color(0.050f, 0.088f, 0.155f, 0.94f),
            new Color(0.76f, 0.88f, 0.96f, 0.34f),
            cut, 2, 0.28f);

        static Sprite BuildBeveledCta(bool chamfered, int cornerPx)
        {
            int c = Mathf.Clamp(cornerPx, 6, 28);
            string key = $"bevelCta|{chamfered}|{c}";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            int slice = c + 4;
            int width = Mathf.Max(256, slice * 2 + 64);
            int height = Mathf.Max(76, slice * 2 + 24);
            int minor = Mathf.Max(3, c / 3);
            const float rimPx = 1.6f;
            const float bottomLip = 5.5f;

            Color rimColor = new Color(0.36f, 0.20f, 0.02f, 1f);
            Color lipBottom = new Color(0.58f, 0.33f, 0.02f, 1f);
            Color lipTop = new Color(0.74f, 0.45f, 0.04f, 1f);
            Color faceBottom = new Color(0.84f, 0.56f, 0.08f, 1f);
            Color faceMid = new Color(0.97f, 0.75f, 0.24f, 1f);
            Color faceTop = new Color(1.00f, 0.90f, 0.56f, 1f);
            Color specColor = new Color(1.00f, 0.98f, 0.88f, 1f);

            var pixels = new Color32[width * height];
            float sliceNx = (float)slice / width;
            float sliceNy = (float)slice / height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    float d = chamfered
                        ? SignedChamferBox(fx, fy, width, height, c, minor)
                        : SignedRoundBox(fx, fy, width, height, c);

                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f)
                    {
                        pixels[y * width + x] = ToColor32(new Color(rimColor.r, rimColor.g, rimColor.b, 0f));
                        continue;
                    }

                    // 1. luminous multi-stop gold face
                    float faceT = Mathf.Clamp01((fy - bottomLip) / Mathf.Max(1f, height - bottomLip - rimPx));
                    Color face = faceT < 0.55f
                        ? Color.Lerp(faceBottom, faceMid, faceT / 0.55f)
                        : Color.Lerp(faceMid, faceTop, (faceT - 0.55f) / 0.45f);

                    // subtle horizontal curvature shading inside left/right shoulders
                    float nx = fx / width;
                    float ny = fy / height;
                    float sideShade = 1f - 0.05f * Mathf.Pow(Mathf.Abs(nx * 2f - 1f), 2.5f);
                    face = new Color(face.r * sideShade, face.g * sideShade, face.b * sideShade, 1f);

                    // 2. seam-free diagonal glossy sheen strictly feathered inside the 9-slice center region
                    float centerMaskX = Smooth(sliceNx, sliceNx + 0.08f, nx) * (1f - Smooth(1f - sliceNx - 0.08f, 1f - sliceNx, nx));
                    float centerMaskY = Smooth(sliceNy, sliceNy + 0.10f, ny) * (1f - Smooth(1f - sliceNy - 0.10f, 1f - sliceNy, ny));
                    float centerMask = centerMaskX * centerMaskY;
                    if (centerMask > 0.001f)
                    {
                        float diag = nx - (ny - 0.5f) * 0.24f;
                        float band1 = Mathf.Exp(-Mathf.Pow((diag - 0.28f) / 0.068f, 2f)) * 0.21f;
                        float band2 = Mathf.Exp(-Mathf.Pow((diag - 0.41f) / 0.022f, 2f)) * 0.13f;
                        face = Color.Lerp(face, specColor, (band1 + band2) * centerMask);
                    }

                    // 3. grounded 3D bottom press-lip thickness (strictly inside bottom slice so height stays constant in 9-slice)
                    float inLip = 1f - Smooth(bottomLip - 0.6f, bottomLip + 0.6f, fy);
                    Color lip = Color.Lerp(lipBottom, lipTop, Mathf.Clamp01(fy / bottomLip));
                    Color col = Color.Lerp(face, lip, inLip);

                    // crisp catchlight crease right where face meets the 3D bottom press-lip
                    float crease = 1f - Mathf.Clamp01(Mathf.Abs(fy - (bottomLip + 1.0f)) / 1.1f);
                    col = Color.Lerp(col, GoldLight, crease * 0.34f * Smooth(rimPx, rimPx + 2f, d));

                    // 4. razor-crisp top specular lip along upper edge
                    float topRegion = Smooth(height - slice, height - rimPx - 3f, fy);
                    float topSpec = (1f - Mathf.Clamp01(Mathf.Abs(d - (rimPx + 1.2f)) / 1.3f)) * topRegion;
                    col = Color.Lerp(col, specColor, topSpec * 0.80f);

                    // 5. grounded dark bronze outer anchor rim
                    float inRim = 1f - Mathf.Clamp01(d - rimPx + 0.5f);
                    col = Color.Lerp(col, rimColor, inRim);
                    col.a = cover;
                    pixels[y * width + x] = ToColor32(col);
                }
            }

            return MakeCachedSprite(key, chamfered ? "KalyeahCtaGold" : "KalyeahPlayPill", width, height, pixels, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>The dark glass panel of the home and Kids screens: deep slate-navy with a crisp cool rim and top light-catch.</summary>
        public static Sprite RoundGlass(int radius = 22) => Round(PanelTop, PanelBottom, PanelBorder, radius, 2, 0.28f);

        /// <summary>The gold of PLAY and primary buttons: layered luminous gold-amber bevel with crisp top specular and 3D press-lip.</summary>
        public static Sprite RoundGold(int radius = 22) => PlayPill(radius);

        /// <summary>A flat white rounded fill, tinted with the Image colour (chips, cards, shadows).</summary>
        public static Sprite RoundSolid(int radius = 14) => Round(Color.white, Color.white, Color.white, radius, 0, 0f);

        /// <summary>A flat white tag, for hard drop shadows and tinted fills (same shape as <see cref="Plate"/>).</summary>
        public static Sprite Solid(int cut = 18) => Plate(Color.white, Color.white, Color.white, cut, 0, 0f);

        /// <summary>The dark glass plate with a light rim: the base of every card and tile.</summary>
        public static Sprite Glass(int cut = 18) => Plate(GlassTop, GlassBottom, Rim, cut, 2, 0.28f);

        /// <summary>A white plate that is lighter at the top; tint it with <see cref="UnityEngine.UI.Graphic.color"/> for chips and bars.</summary>
        public static Sprite Chip(int cut = 10) => Plate(Color.white, new Color(0.70f, 0.70f, 0.70f, 1f), Ink, cut, 2, 0.4f);

        /// <summary>Sleek dark-ink chamfered stat chip plate (WATER, COOLDOWN, 100%, 12 KIDS, ON HOME). 9-sliced.</summary>
        public static Sprite StatChip(int cut = 8) => ChamferCard(
            new Color(0.095f, 0.155f, 0.245f, 0.94f),
            new Color(0.042f, 0.072f, 0.132f, 0.97f),
            new Color(0.76f, 0.88f, 0.96f, 0.28f),
            cut, 1, 0.22f);

        /// <summary>Luminous white 9-sliced pill bar with subtle core gleam; tint with Gold, Coral, Teal or SuccessGreen for accent rails.</summary>
        public static Sprite AccentRail(int radius = 4)
        {
            int r = Mathf.Clamp(radius, 2, 12);
            string key = "accentRail|" + r;
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            int slice = r + 2;
            int size = slice * 2 + 8;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = SignedRoundBox(fx, fy, size, size, r);
                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f)
                    {
                        pixels[y * size + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }
                    float ny = fy / size;
                    float bright = Mathf.Lerp(0.84f, 1.0f, ny);
                    float core = Smooth(0.5f, 2.5f, d) * 0.12f;
                    float v = Mathf.Clamp01(bright + core);
                    pixels[y * size + x] = ToColor32(new Color(v, v, v, cover));
                }
            }

            return MakeCachedSprite(key, "KalyeahAccentRail", size, size, pixels, new Vector4(slice, slice, slice, slice));
        }

        // ---------------------------------------------------------------- currency, toggles & status sprites

        /// <summary>
        /// Embossed metallic Barya coin icon (96×96, 4×4 supersampled): bronze anchor rim, milled gold bevel,
        /// brushed metallic sheen and a raised 8-ray Filipino sunburst emblem. Use with Color.white.
        /// </summary>
        public static Sprite BaryaCoin()
        {
            const string key = "baryaCoin";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 96;
            const int samples = 4;
            var pixels = new Color32[size * size];

            Color outerRimDark = new Color(0.42f, 0.22f, 0.02f, 1f);
            Color bevelShadow = new Color(0.68f, 0.41f, 0.04f, 1f);
            Color bevelMid = new Color(0.98f, 0.76f, 0.22f, 1f);
            Color bevelBright = new Color(1.00f, 0.96f, 0.74f, 1f);
            Color faceDark = new Color(0.76f, 0.49f, 0.05f, 1f);
            Color faceWarm = new Color(0.95f, 0.71f, 0.17f, 1f);
            Color grooveShadow = new Color(0.50f, 0.28f, 0.02f, 1f);
            Color sunShadow = new Color(0.58f, 0.34f, 0.03f, 1f);
            Color sunGold = new Color(1.00f, 0.89f, 0.48f, 1f);
            Color sunSpec = new Color(1.00f, 0.98f, 0.84f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                            float r = Mathf.Sqrt(u * u + v * v);
                            if (r > 0.94f) continue;

                            float lit = Mathf.Clamp01((-u * 0.65f + v * 0.76f) * 0.55f + 0.50f);
                            Color col;
                            if (r > 0.87f)
                            {
                                col = Color.Lerp(outerRimDark, bevelShadow, lit * 0.6f);
                            }
                            else if (r > 0.71f)
                            {
                                float angle = Mathf.Atan2(v, u);
                                float milling = 0.055f * Mathf.Sin(24f * angle) * Smooth(0.73f, 0.78f, r) * (1f - Smooth(0.83f, 0.87f, r));
                                float rimLit = Mathf.Clamp01(lit + milling);
                                col = rimLit < 0.55f
                                    ? Color.Lerp(bevelShadow, bevelMid, rimLit / 0.55f)
                                    : Color.Lerp(bevelMid, bevelBright, (rimLit - 0.55f) / 0.45f);
                            }
                            else if (r > 0.66f)
                            {
                                // stamped coin inner dish step: shadow top-left, bright bottom-right
                                col = Color.Lerp(bevelBright, grooveShadow, lit);
                            }
                            else
                            {
                                // recessed coin face with diagonal brushed metallic sheen
                                col = Color.Lerp(faceDark, faceWarm, lit);
                                float sheen = Mathf.Exp(-Mathf.Pow((u - v * 0.65f + 0.14f) * 4.0f, 2f));
                                col = Color.Lerp(col, bevelBright, sheen * 0.32f);

                                // offset embossed shadow for 8-ray Filipino sunburst
                                if (InSunburst(u - 0.032f, v + 0.032f))
                                    col = Color.Lerp(col, sunShadow, 0.65f);

                                // raised 8-ray Filipino sunburst emblem
                                if (InSunburst(u, v))
                                {
                                    float sunLit = Mathf.Clamp01((-u * 0.6f + v * 0.8f) * 0.8f + 0.55f);
                                    col = Color.Lerp(sunGold, sunSpec, sunLit * 0.72f);
                                }
                            }

                            sr += col.r;
                            sg += col.g;
                            sb += col.b;
                            sa += 1f;
                        }
                    }

                    if (sa <= 0f)
                    {
                        pixels[y * size + x] = new Color32(180, 120, 20, 0);
                    }
                    else
                    {
                        float inv = 1f / sa;
                        pixels[y * size + x] = ToColor32(new Color(sr * inv, sg * inv, sb * inv, sa / (samples * samples)));
                    }
                }
            }

            return MakeCachedSprite(key, "KalyeahBaryaCoin", size, size, pixels, Vector4.zero);
        }

        static bool InSunburst(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r < 0.19f) return true;
            if (r > 0.49f) return false;
            float angle = Mathf.Atan2(v, u);
            float sector = Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 0.25f) + 0.5f, 1f) - 0.5f); // 8 rays
            float rayHalfWidth = Mathf.Lerp(0.24f, 0.02f, (r - 0.19f) / 0.30f);
            return sector < rayHalfWidth;
        }

        /// <summary>
        /// Faceted brilliant-cut Diamond gem icon (96×96, 4×4 supersampled): distinct refracted sky-cyan/sapphire facets,
        /// crisp ridge highlights and a top-left specular sparkle star. Use with Color.white.
        /// </summary>
        public static Sprite DiamondGem()
        {
            const string key = "diamondGem";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 96;
            const int samples = 4;
            var pixels = new Color32[size * size];

            Color outline = new Color(0.04f, 0.18f, 0.38f, 1f);
            Color crownLeft = new Color(0.86f, 0.99f, 1.00f, 1f);
            Color crownMid = new Color(0.56f, 0.93f, 1.00f, 1f);
            Color crownRight = new Color(0.28f, 0.80f, 0.98f, 1f);
            Color pavLeft = new Color(0.24f, 0.76f, 0.97f, 1f);
            Color pavMid = new Color(0.11f, 0.58f, 0.90f, 1f);
            Color pavRight = new Color(0.06f, 0.38f, 0.72f, 1f);
            Color caustic = new Color(0.62f, 0.96f, 1.00f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;

                            // brilliant-cut 5-edge gem SDF (positive inside)
                            float dTop = 0.72f - v;
                            float dCrownL = (u - (-0.90f + (v - 0.24f) * 0.875f)) * 0.7525f;
                            float dCrownR = ((0.90f - (v - 0.24f) * 0.875f) - u) * 0.7525f;
                            float dPavL = (u - (-0.90f * (v + 0.86f) / 1.10f)) * 0.7739f;
                            float dPavR = ((0.90f * (v + 0.86f) / 1.10f) - u) * 0.7739f;
                            float dGem = Mathf.Min(dTop, Mathf.Min(Mathf.Min(dCrownL, dCrownR), Mathf.Min(dPavL, dPavR)));

                            // crisp 4-point specular sparkle star on top-left crown shoulder
                            float su = u + 0.48f;
                            float sv = v - 0.68f;
                            float starDist = Mathf.Min(
                                Mathf.Max(Mathf.Abs(su) * 4.6f, Mathf.Abs(sv) * 1.15f),
                                Mathf.Max(Mathf.Abs(sv) * 4.6f, Mathf.Abs(su) * 1.15f));
                            bool inStar = starDist < 0.24f;

                            if (dGem < 0f)
                            {
                                if (inStar)
                                {
                                    float glow = 1f - Mathf.Clamp01(starDist / 0.24f);
                                    float a = Mathf.Clamp01(glow * 1.4f);
                                    sr += a;
                                    sg += a;
                                    sb += a;
                                    sa += a;
                                }
                                continue;
                            }

                            Color col;
                            if (dGem < 0.048f)
                            {
                                col = outline;
                            }
                            else if (v >= 0.24f)
                            {
                                // crown facets
                                float t = Mathf.Clamp01((v - 0.24f) / 0.48f);
                                float splitX = Mathf.Lerp(0.42f, 0.28f, t);
                                if (u < -splitX)
                                    col = crownLeft;
                                else if (u > splitX)
                                    col = crownRight;
                                else
                                    col = Color.Lerp(crownMid, Color.white, t * 0.35f + (u < 0f ? 0.12f : 0f));

                                // crisp ridge highlight along facet seams
                                float ridge = 1f - Mathf.Clamp01(Mathf.Min(Mathf.Abs(u + splitX), Mathf.Abs(u - splitX)) / 0.04f);
                                col = Color.Lerp(col, Color.white, ridge * 0.38f);
                            }
                            else
                            {
                                // pavilion facets
                                float t = Mathf.Clamp01((v + 0.86f) / 1.10f);
                                float splitX = 0.42f * t;
                                if (u < -splitX)
                                    col = Color.Lerp(pavMid, pavLeft, t);
                                else if (u > splitX)
                                    col = Color.Lerp(pavRight, new Color(0.12f, 0.50f, 0.84f, 1f), t * 0.5f);
                                else
                                {
                                    // center kite with subtle internal refraction bounce near lower tip
                                    float bounce = Mathf.Exp(-Mathf.Pow((v + 0.34f) * 3.2f, 2f)) * (1f - Mathf.Clamp01(Mathf.Abs(u) * 3.5f));
                                    col = Color.Lerp(pavMid, caustic, bounce * 0.55f + (u < 0f ? 0.14f : 0f));
                                }

                                float ridge = 1f - Mathf.Clamp01(Mathf.Min(Mathf.Abs(u + splitX), Mathf.Abs(u - splitX)) / 0.036f);
                                col = Color.Lerp(col, caustic, ridge * 0.34f);
                            }

                            // crisp horizontal girdle reflection line
                            float girdle = 1f - Mathf.Clamp01(Mathf.Abs(v - 0.24f) / 0.032f);
                            if (dGem >= 0.048f)
                                col = Color.Lerp(col, Color.white, girdle * 0.52f);

                            if (inStar)
                            {
                                float glow = 1f - Mathf.Clamp01(starDist / 0.24f);
                                col = Color.Lerp(col, Color.white, Mathf.Clamp01(glow * 1.4f));
                            }

                            sr += col.r;
                            sg += col.g;
                            sb += col.b;
                            sa += 1f;
                        }
                    }

                    if (sa <= 0f)
                    {
                        pixels[y * size + x] = new Color32(40, 160, 230, 0);
                    }
                    else
                    {
                        float inv = 1f / sa;
                        pixels[y * size + x] = ToColor32(new Color(sr * inv, sg * inv, sb * inv, Mathf.Clamp01(sa / (samples * samples))));
                    }
                }
            }

            return MakeCachedSprite(key, "KalyeahDiamondGem", size, size, pixels, Vector4.zero);
        }

        /// <summary>Modern tactical pill toggle track in the ON state (luminous warm gold with recessed bevel). 9-sliced; use Color.white.</summary>
        public static Sprite ToggleTrackOn() => BuildToggleTrack(isOn: true);

        /// <summary>Modern tactical pill toggle track in the OFF state (deep recessed slate-ink with crisp hairline rim). 9-sliced; use Color.white.</summary>
        public static Sprite ToggleTrackOff() => BuildToggleTrack(isOn: false);

        static Sprite BuildToggleTrack(bool isOn)
        {
            string key = isOn ? "toggleOn" : "toggleOff";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int width = 96;
            const int height = 44;
            const int radius = 22;
            const int slice = 22;
            var pixels = new Color32[width * height];

            Color fillTop = isOn ? new Color(0.84f, 0.56f, 0.08f, 1f) : new Color(0.030f, 0.052f, 0.095f, 0.96f);
            Color fillBottom = isOn ? new Color(1.00f, 0.80f, 0.28f, 1f) : new Color(0.068f, 0.110f, 0.180f, 0.96f);
            Color rim = isOn ? new Color(0.55f, 0.32f, 0.03f, 1f) : new Color(0.68f, 0.82f, 0.94f, 0.34f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = SignedRoundBox(fx, fy, width, height, radius);
                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f)
                    {
                        pixels[y * width + x] = ToColor32(new Color(fillBottom.r, fillBottom.g, fillBottom.b, 0f));
                        continue;
                    }

                    float ny = fy / height;
                    // recessed track: slightly darker near top (cast inner shadow), warmer/brighter near bottom
                    Color col = Color.Lerp(fillBottom, fillTop, Smooth(0.15f, 0.90f, ny));
                    float innerTopShadow = (1f - Smooth(1.5f, 6.0f, height - fy)) * (isOn ? 0.22f : 0.38f);
                    col = Color.Lerp(col, new Color(0.01f, 0.02f, 0.04f, col.a), innerTopShadow);

                    float inRim = 1f - Mathf.Clamp01(d - 1.6f + 0.5f);
                    col = Color.Lerp(col, rim, inRim * rim.a);
                    col.a = Mathf.Lerp(col.a, Mathf.Max(col.a, rim.a), inRim) * cover;
                    pixels[y * width + x] = ToColor32(col);
                }
            }

            return MakeCachedSprite(key, isOn ? "KalyeahToggleOn" : "KalyeahToggleOff", width, height, pixels, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>Crisp 3D domed toggle switch thumb (64×64, 4×4 supersampled) with subtle drop shadow and specular dome. Use Color.white.</summary>
        public static Sprite ToggleThumb()
        {
            const string key = "toggleThumb";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 64;
            const int samples = 4;
            var pixels = new Color32[size * size];

            Color domeTop = new Color(1.00f, 0.99f, 0.95f, 1f);
            Color domeBottom = new Color(0.84f, 0.87f, 0.91f, 1f);
            Color rimColor = new Color(0.55f, 0.58f, 0.64f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                            float r = Mathf.Sqrt(u * u + (v - 0.03f) * (v - 0.03f));
                            float rShadow = Mathf.Sqrt(u * u + (v + 0.09f) * (v + 0.09f));

                            if (r <= 0.82f)
                            {
                                float lit = Mathf.Clamp01((v * 0.85f - u * 0.25f) * 0.55f + 0.55f);
                                Color col = Color.Lerp(domeBottom, domeTop, lit);
                                // crisp top specular crescent
                                float specR = Mathf.Sqrt(u * u + (v - 0.28f) * (v - 0.28f));
                                float spec = Mathf.Exp(-specR * specR * 7.5f) * 0.35f;
                                col = Color.Lerp(col, Color.white, spec);
                                if (r > 0.74f) col = Color.Lerp(col, rimColor, Smooth(0.74f, 0.82f, r) * 0.55f);
                                sr += col.r;
                                sg += col.g;
                                sb += col.b;
                                sa += 1f;
                            }
                            else if (rShadow < 0.92f)
                            {
                                float shadowA = (1f - Smooth(0.68f, 0.92f, rShadow)) * 0.42f;
                                sr += 0.02f * shadowA;
                                sg += 0.04f * shadowA;
                                sb += 0.08f * shadowA;
                                sa += shadowA;
                            }
                        }
                    }

                    if (sa <= 0f)
                        pixels[y * size + x] = new Color32(240, 240, 240, 0);
                    else
                    {
                        float inv = 1f / sa;
                        pixels[y * size + x] = ToColor32(new Color(sr * inv, sg * inv, sb * inv, Mathf.Clamp01(sa / (samples * samples))));
                    }
                }
            }

            return MakeCachedSprite(key, "KalyeahToggleThumb", size, size, pixels, Vector4.zero);
        }

        /// <summary>
        /// Crisp golden circular check medallion (64×64, 4×4 supersampled) with embossed dark-ink checkmark.
        /// Replaces face-washing tints on selected avatars and ON HOME badges. Use with Color.white.
        /// </summary>
        public static Sprite CheckBadge()
        {
            const string key = "checkBadge";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 64;
            const int samples = 4;
            var pixels = new Color32[size * size];
            Vector2 pA = new Vector2(-0.38f, 0.00f);
            Vector2 pB = new Vector2(-0.10f, -0.30f);
            Vector2 pC = new Vector2(0.42f, 0.30f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                            float r = Mathf.Sqrt(u * u + v * v);
                            if (r > 0.92f) continue;

                            Color col;
                            if (r > 0.82f)
                            {
                                col = Ink;
                            }
                            else
                            {
                                float lit = Mathf.Clamp01(v * 0.55f + 0.52f);
                                col = Color.Lerp(GoldDeep, GoldLight, lit);
                                if (r > 0.74f) col = Color.Lerp(col, GoldLight, Smooth(0.74f, 0.80f, r) * 0.45f);
                                Vector2 p = new Vector2(u, v);
                                float dCheck = Mathf.Min(DistToSegment(p, pA, pB), DistToSegment(p, pB, pC));
                                if (dCheck < 0.125f) col = Ink;
                            }

                            sr += col.r;
                            sg += col.g;
                            sb += col.b;
                            sa += 1f;
                        }
                    }

                    if (sa <= 0f)
                        pixels[y * size + x] = new Color32(245, 185, 58, 0);
                    else
                    {
                        float inv = 1f / sa;
                        pixels[y * size + x] = ToColor32(new Color(sr * inv, sg * inv, sb * inv, sa / (samples * samples)));
                    }
                }
            }

            return MakeCachedSprite(key, "KalyeahCheckBadge", size, size, pixels, Vector4.zero);
        }

        /// <summary>Crisp white geometric close '×' icon (64×64, 4×4 supersampled); tint with Image.color.</summary>
        public static Sprite CloseIcon() => Glyph(GlyphKind.Close);

        // ---------------------------------------------------------------- light, scrims & pattern

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
            return MakeCachedSprite(key, "KalyeahGlow", size, size, pixels, Vector4.zero);
        }

        /// <summary>
        /// Radial card spotlight (128×128, white with alpha) biased slightly toward upper-center so kid busts and 3D heroes
        /// separate heroically off dark cards. Tint with low-alpha cool teal or warm gold.
        /// </summary>
        public static Sprite CardSpotlight()
        {
            const string key = "cardSpotlight";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;
                    float dx = (nx - 0.50f) * 1.95f;
                    float dy = (ny - 0.58f) * 1.85f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(1f - Mathf.Clamp01(r), 1.85f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            return MakeCachedSprite(key, "KalyeahCardSpotlight", size, size, pixels, Vector4.zero);
        }

        /// <summary>
        /// Smooth bottom-up dark ink contrast scrim (4×128): deep ink at y=0 easing to transparent at y=1.
        /// Place behind card nameplates so white/cream names stay 100% legible over bright shirts and capes. Use Color.white or alpha tint.
        /// </summary>
        public static Sprite BottomScrim() => BuildVerticalScrim(fromTop: false);

        /// <summary>Smooth top-down dark ink contrast scrim (4×128): deep ink at y=1 easing to transparent at y=0.</summary>
        public static Sprite TopScrim() => BuildVerticalScrim(fromTop: true);

        static Sprite BuildVerticalScrim(bool fromTop)
        {
            string key = fromTop ? "topScrim" : "bottomScrim";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int width = 4;
            const int height = 128;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (y + 0.5f) / height;
                float u = fromTop ? t : 1f - t; // 1 at dark edge, 0 at clear edge
                float a = Mathf.Pow(u, 1.55f) * 0.94f;
                var px = new Color32(6, 11, 20, (byte)Mathf.RoundToInt(a * 255f));
                for (int x = 0; x < width; x++) pixels[y * width + x] = px;
            }

            return MakeCachedSprite(key, fromTop ? "KalyeahTopScrim" : "KalyeahBottomScrim", width, height, pixels, Vector4.zero);
        }

        /// <summary>Tilted glossy white-gold diagonal sheen beam (128×128) used by <see cref="UiSheen"/> for primary CTAs.</summary>
        public static Sprite SheenStreak()
        {
            const string key = "sheenStreak";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;
                    float diag = nx - (ny - 0.5f) * 0.34f;
                    float main = Mathf.Exp(-Mathf.Pow((diag - 0.44f) / 0.13f, 2f));
                    float twin = Mathf.Exp(-Mathf.Pow((diag - 0.67f) / 0.045f, 2f)) * 0.60f;
                    float edgeFade = Smooth(0.02f, 0.14f, nx) * (1f - Smooth(0.86f, 0.98f, nx));
                    float a = Mathf.Clamp01((main + twin) * edgeFade);
                    pixels[y * size + x] = new Color32(255, 250, 232, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            return MakeCachedSprite(key, "KalyeahSheenStreak", size, size, pixels, Vector4.zero);
        }

        /// <summary>
        /// Grounded elliptical hero pedestal shadow + subtle luminous gold-teal stage ring (256×64) for 2D UI hero staging.
        /// Use with Color.white.
        /// </summary>
        public static Sprite PedestalGlow()
        {
            const string key = "pedestalGlow";
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int width = 256;
            const int height = 64;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width * 2f - 1f;
                    float ny = (y + 0.5f) / height * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    pixels[y * width + x] = EvaluatePedestalPixel(r);
                }
            }

            return MakeCachedSprite(key, "KalyeahPedestalGlow", width, height, pixels, Vector4.zero);
        }

        /// <summary>
        /// 3D circular floor disc texture (256×256) used inside <see cref="HeroStage"/> directly under the kid's feet:
        /// grounded soft contact shadow plus a subtle luminous tactical stage ring.
        /// </summary>
        public static Texture2D PedestalFloorTexture()
        {
            const string key = "pedestalFloor";
            if (Textures.TryGetValue(key, out Texture2D hit) && hit != null) return hit;

            const int size = 256;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    pixels[y * size + x] = EvaluatePedestalPixel(r);
                }
            }

            Texture2D tex = Finish(NewTexture("KalyeahPedestalFloor", size, size, TextureWrapMode.Clamp), pixels);
            Textures[key] = tex;
            return tex;
        }

        static Color32 EvaluatePedestalPixel(float r)
        {
            if (r >= 1f) return new Color32(8, 14, 24, 0);
            float contact = Mathf.Pow(1f - Mathf.Clamp01(r / 0.72f), 2.1f) * 0.78f;
            float ring = Mathf.Exp(-Mathf.Pow((r - 0.79f) / 0.042f, 2f)) * 0.52f;
            float outerHaze = Mathf.Exp(-Mathf.Pow((r - 0.79f) / 0.14f, 2f)) * 0.14f;
            float ringStrength = Mathf.Clamp01(ring + outerHaze);
            float totalA = Mathf.Clamp01(contact + ringStrength * (1f - contact * 0.6f));
            if (totalA <= 0.002f) return new Color32(8, 14, 24, 0);

            Color shadowCol = new Color(0.015f, 0.025f, 0.045f, 1f);
            Color ringCol = new Color(0.92f, 0.84f, 0.56f, 1f);
            float mix = Mathf.Clamp01(ringStrength / Mathf.Max(0.01f, totalA));
            Color col = Color.Lerp(shadowCol, ringCol, mix);
            col.a = totalA;
            return ToColor32(col);
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
                pixels[y] = ToColor32(c);
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
            return MakeCachedSprite(key, "KalyeahGlyph_" + kind, size, size, pixels, Vector4.zero);
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
                case GlyphKind.Kid:   // a head over shoulders
                    return new Vector2(u, v - 0.38f).magnitude < 0.34f
                        || (v < -0.08f && v > -0.9f && new Vector2(u, (v + 0.9f) * 0.9f).magnitude < 0.78f);
                case GlyphKind.Cards: // two fanned cards
                    return InBox(Rotate(u + 0.22f, v, 0.2f), 0.36f, 0.6f) || InBox(Rotate(u - 0.22f, v, -0.16f), 0.36f, 0.6f);
                case GlyphKind.Chevron:
                    return Mathf.Abs(Mathf.Abs(v) * 0.9f - (0.32f - u)) < 0.16f && u > -0.35f && u < 0.5f;
                case GlyphKind.Striker: // a lightning bolt
                    return InPolygon(u, v, new[] { new Vector2(0.18f, 0.92f), new Vector2(-0.52f, -0.08f), new Vector2(-0.04f, -0.08f), new Vector2(-0.2f, -0.92f), new Vector2(0.52f, 0.14f), new Vector2(0.04f, 0.14f) });
                case GlyphKind.Defender: // a shield
                    return v < 0.82f && (v > 0.1f ? Mathf.Abs(u) < 0.68f : Mathf.Abs(u) < 0.68f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.1f - v) / 0.98f, 2f))));
                case GlyphKind.Seeker: // a magnifier
                {
                    float lr = new Vector2(u + 0.14f, v - 0.14f).magnitude;
                    bool handle = Mathf.Abs((u - v) * 0.7071f - 0.56f) < 0.32f && Mathf.Abs((u + v) * 0.7071f) < 0.11f;
                    return (lr < 0.58f && lr > 0.38f) || handle;
                }
                case GlyphKind.Bag: // a shopping bag: a body with a handle
                {
                    bool body = Mathf.Abs(u) < 0.72f && v > -0.82f && v < 0.28f;
                    float hr = new Vector2(u, v - 0.28f).magnitude;
                    return body || (hr > 0.28f && hr < 0.46f && v > 0.26f);
                }
                case GlyphKind.People: // two heads over shoulders
                    return new Vector2(u + 0.3f, v - 0.38f).magnitude < 0.28f
                        || (v < 0.02f && v > -0.85f && new Vector2((u + 0.3f) * 0.8f, v + 0.85f).magnitude < 0.6f)
                        || new Vector2(u - 0.42f, v - 0.28f).magnitude < 0.21f
                        || (v < -0.06f && v > -0.85f && new Vector2((u - 0.42f) * 1.1f, v + 0.85f).magnitude < 0.5f);
                case GlyphKind.Star:
                {
                    var star = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, rad = i % 2 == 0 ? 0.95f : 0.4f;
                        star[i] = new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
                    }
                    return InPolygon(u, v, star);
                }
                case GlyphKind.Controller: // a target
                    return (r < 0.9f && r > 0.72f) || (r < 0.48f && r > 0.32f) || r < 0.14f;
                case GlyphKind.Check:
                {
                    Vector2 p = new Vector2(u, v);
                    return Mathf.Min(
                        DistToSegment(p, new Vector2(-0.56f, 0.02f), new Vector2(-0.14f, -0.44f)),
                        DistToSegment(p, new Vector2(-0.14f, -0.44f), new Vector2(0.62f, 0.46f))) < 0.15f;
                }
                case GlyphKind.Close:
                {
                    Vector2 p = new Vector2(u, v);
                    return Mathf.Min(
                        DistToSegment(p, new Vector2(-0.52f, -0.52f), new Vector2(0.52f, 0.52f)),
                        DistToSegment(p, new Vector2(-0.52f, 0.52f), new Vector2(0.52f, -0.52f))) < 0.135f;
                }
                case GlyphKind.Lock: // padlock for taken/locked kids
                {
                    bool body = Mathf.Abs(u) < 0.62f && v > -0.78f && v < 0.10f;
                    bool keyhole = new Vector2(u, v + 0.28f).magnitude < 0.13f || (Mathf.Abs(u) < 0.06f && v > -0.56f && v < -0.28f);
                    float sr = new Vector2(u, v - 0.18f).magnitude;
                    bool shackle = v >= 0.08f && sr > 0.26f && sr < 0.44f;
                    return (body && !keyhole) || shackle;
                }
                case GlyphKind.Camera: // ID camera glyph
                {
                    bool body = Mathf.Abs(u) < 0.80f && v > -0.58f && v < 0.42f;
                    bool bump = Mathf.Abs(u) < 0.30f && v >= 0.38f && v < 0.64f;
                    float lr = new Vector2(u, v + 0.08f).magnitude;
                    bool lensHole = lr < 0.34f && lr > 0.18f;
                    return (body || bump) && !lensHole;
                }
                case GlyphKind.History: // three stacked tactical list lines
                {
                    bool row1 = Mathf.Abs(v - 0.44f) < 0.11f && u > -0.72f && u < 0.72f;
                    bool row2 = Mathf.Abs(v) < 0.11f && u > -0.72f && u < 0.72f;
                    bool row3 = Mathf.Abs(v + 0.44f) < 0.11f && u > -0.72f && u < 0.52f;
                    return row1 || row2 || row3;
                }
                default:
                    return false;
            }
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return (p - (a + ab * t)).magnitude;
        }

        static Vector2 Rotate(float u, float v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(u * c - v * s, u * s + v * c);
        }

        static bool InBox(Vector2 p, float halfW, float halfH) => Mathf.Abs(p.x) < halfW && Mathf.Abs(p.y) < halfH;

        static bool InPolygon(float u, float v, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > v) != (poly[j].y > v) && u < (poly[j].x - poly[i].x) * (v - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // ---------------------------------------------------------------- 36 kit skill emblems + combat archetypes

        /// <summary>
        /// Crisp 64x64 4x4 supersampled Valorant/CODM minimalist white vector emblem with subtle top-lit bevel shading
        /// for every one of the 36 <c>docs/KITS.md</c> skills plus core combat actions (<c>tirador</c>, <c>reload</c>,
        /// <c>jump</c>, <c>slide</c>, <c>vault</c>, <c>ads</c>). Never returns null and never falls back to English text.
        /// </summary>
        public static Sprite SkillEmblem(string skillId)
        {
            string canonical = CanonicalSkillId(skillId);
            string key = "skillEmblem|" + canonical;
            if (Sprites.TryGetValue(key, out Sprite hit) && hit != null) return hit;

            const int size = 64;
            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                byte lum = (byte)Mathf.RoundToInt(Mathf.Lerp(226f, 255f, ny));
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            if (InsideSkill(canonical, u, v)) inside++;
                        }
                    }
                    byte alpha = (byte)(inside * 255 / (samples * samples));
                    pixels[y * size + x] = new Color32(lum, lum, lum, alpha);
                }
            }

            return MakeCachedSprite(key, "KalyeahSkill_" + canonical, size, size, pixels, Vector4.zero);
        }

        static string CanonicalSkillId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "tactical_default";
            string s = raw.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_').Replace("!", "");
            switch (s)
            {
                case "tirador_bato":
                case "slingshot":
                    return "tirador";
                case "aim":
                    return "ads";
                case "bato":
                case "bato_heavy":
                case "bato_light":
                    return "bato_execute";
                case "lata":
                    return "lata_morph";
                case "lupa":
                case "ult_bagsak":
                    return "lupa_quake";
                case "patintero":
                    return "patintero_lines";
                case "pabitin":
                case "heal":
                    return "pabitin_heal";
                case "tsinelas_light":
                case "tsinelas_heavy":
                case "tsinelas":
                    return "tsinelas_bagsik";
                case "papel_shield":
                    return "sungka";
                case "papel_trap":
                    return "teks_tripwire";
                case "jacks":
                    return "jackstone";
                case "trumpo":
                    return "trumpo_ikot";
                case "bola":
                    return "tira_holens";
                case "langit_lupa":
                    return "langit";
                default:
                    return s;
            }
        }

        static bool Seg(float u, float v, float ax, float ay, float bx, float by, float halfW)
        {
            float abx = bx - ax, aby = by - ay;
            float lenSq = abx * abx + aby * aby;
            float t = lenSq > 1e-6f ? Mathf.Clamp01(((u - ax) * abx + (v - ay) * aby) / lenSq) : 0f;
            float dx = u - (ax + abx * t);
            float dy = v - (ay + aby * t);
            return dx * dx + dy * dy < halfW * halfW;
        }

        static bool Circ(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx, dy = v - cy;
            return dx * dx + dy * dy < r * r;
        }

        static bool Ring(float u, float v, float cx, float cy, float rIn, float rOut)
        {
            float dx = u - cx, dy = v - cy;
            float r2 = dx * dx + dy * dy;
            return r2 > rIn * rIn && r2 < rOut * rOut;
        }

        static bool Tri(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (u - bx) * (ay - by) - (ax - bx) * (v - by);
            float d2 = (u - cx) * (by - cy) - (bx - cx) * (v - cy);
            float d3 = (u - ax) * (cy - ay) - (cx - ax) * (v - ay);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        static bool RotBox(float u, float v, float cx, float cy, float rad, float hw, float hh)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            float dx = u - cx, dy = v - cy;
            float rx = dx * c - dy * s;
            float ry = dx * s + dy * c;
            return Mathf.Abs(rx) < hw && Mathf.Abs(ry) < hh;
        }

        static bool InsideSkill(string id, float u, float v)
        {
            switch (id)
            {
                // ---- Narda (Striker) ----
                case "luksong_tinik": // hurdle vault arch + twin speed dash chevrons over crossed thorn bars
                {
                    bool bar1 = Seg(u, v, -0.78f, -0.56f, 0.78f, -0.22f, 0.08f);
                    bool bar2 = Seg(u, v, -0.78f, -0.22f, 0.78f, -0.56f, 0.08f);
                    bool arch = Ring(u, v, 0f, -0.18f, 0.44f, 0.62f) && v > -0.10f;
                    bool head = Tri(u, v, 0.80f, 0.08f, 0.36f, 0.24f, 0.48f, -0.16f);
                    bool streak = Seg(u, v, -0.78f, 0.54f, -0.16f, 0.74f, 0.07f);
                    return bar1 || bar2 || arch || head || streak;
                }
                case "tira_holens": // glossy marble orb with specular cut + ricochet bounce path
                {
                    bool marble = Circ(u, v, -0.34f, -0.20f, 0.40f) && !Circ(u, v, -0.46f, -0.06f, 0.13f);
                    bool bounce1 = Seg(u, v, 0.04f, -0.16f, 0.62f, 0.54f, 0.075f);
                    bool bounce2 = Seg(u, v, 0.62f, 0.54f, 0.12f, 0.72f, 0.07f);
                    bool wall = Seg(u, v, 0.76f, 0.12f, 0.76f, 0.82f, 0.07f);
                    bool arrow = Tri(u, v, -0.10f, 0.78f, 0.22f, 0.88f, 0.16f, 0.56f);
                    return marble || bounce1 || bounce2 || wall || arrow;
                }
                case "tsinelas_bagsik": // triple fanned curved boomerang slippers
                {
                    bool s1 = (RotBox(u, v, -0.42f, 0.12f, 0.52f, 0.18f, 0.52f) || Circ(u, v, -0.56f, 0.50f, 0.17f))
                        && !Seg(u, v, -0.54f, 0.40f, -0.32f, 0.16f, 0.055f);
                    bool s2 = (RotBox(u, v, 0.02f, 0.16f, 0.0f, 0.19f, 0.56f) || Circ(u, v, 0.02f, 0.64f, 0.18f))
                        && !Circ(u, v, 0.02f, 0.34f, 0.085f);
                    bool s3 = (RotBox(u, v, 0.46f, 0.04f, -0.52f, 0.18f, 0.50f) || Circ(u, v, 0.60f, 0.42f, 0.17f))
                        && !Seg(u, v, 0.56f, 0.32f, 0.34f, 0.08f, 0.055f);
                    bool arc = Ring(u, v, 0f, -0.20f, 0.66f, 0.78f) && v < -0.26f;
                    return s1 || s2 || s3 || arc;
                }

                // ---- Jumong (Striker) ----
                case "syato": // long striker stick + popped short syato peg + upward knock-up burst
                {
                    bool longStick = Seg(u, v, -0.74f, -0.66f, 0.54f, 0.24f, 0.10f);
                    bool shortPeg = Seg(u, v, -0.10f, 0.44f, 0.56f, 0.72f, 0.095f);
                    bool popArc = Ring(u, v, -0.14f, 0.02f, 0.44f, 0.55f) && u < 0.05f && v > 0.18f;
                    bool popTip = Tri(u, v, -0.24f, 0.76f, -0.46f, 0.48f, -0.10f, 0.52f);
                    return longStick || shortPeg || popArc || popTip;
                }
                case "trumpo_ikot": // spinning wooden top + twin cyclone spin blades
                {
                    bool topCap = Tri(u, v, 0f, -0.68f, -0.40f, 0.24f, 0.40f, 0.24f);
                    bool knob = RotBox(u, v, 0f, 0.38f, 0f, 0.14f, 0.16f);
                    bool swirlL = Ring(u, v, 0f, -0.04f, 0.58f, 0.72f) && u < -0.18f && v > -0.48f && v < 0.46f;
                    bool swirlR = Ring(u, v, 0f, -0.04f, 0.58f, 0.72f) && u > 0.18f && v > -0.48f && v < 0.46f;
                    bool stripe = Mathf.Abs(v - 0.02f) < 0.055f && Mathf.Abs(u) < 0.34f;
                    return ((topCap || knob) && !stripe) || swirlL || swirlR;
                }
                case "pukpok_palayok": // clay palayok pot shattered from above by smashing club + starburst
                {
                    bool pot = Circ(u, v, 0f, -0.26f, 0.48f) && v < 0.08f;
                    bool rim = RotBox(u, v, 0f, 0.12f, 0f, 0.36f, 0.08f);
                    bool crack = Seg(u, v, 0f, 0.10f, -0.16f, -0.24f, 0.045f) || Seg(u, v, -0.16f, -0.24f, 0.14f, -0.52f, 0.045f);
                    bool club = Seg(u, v, 0.14f, 0.28f, 0.74f, 0.80f, 0.10f);
                    bool shard1 = Tri(u, v, -0.66f, 0.56f, -0.34f, 0.38f, -0.46f, 0.22f);
                    bool shard2 = Tri(u, v, -0.18f, 0.78f, -0.04f, 0.44f, -0.24f, 0.46f);
                    return ((pot || rim) && !crack) || club || shard1 || shard2;
                }

                // ---- Inggo (Striker) ----
                case "sipa": // feathered rattan sipa shuttlecock + 3-bounce arc dots
                {
                    bool washer = Ring(u, v, 0f, -0.46f, 0.11f, 0.28f);
                    bool featherL = Tri(u, v, -0.08f, -0.26f, -0.64f, 0.54f, -0.22f, 0.68f);
                    bool featherM = Tri(u, v, 0f, -0.24f, -0.20f, 0.76f, 0.20f, 0.76f);
                    bool featherR = Tri(u, v, 0.08f, -0.26f, 0.22f, 0.68f, 0.64f, 0.54f);
                    bool hop1 = Circ(u, v, -0.66f, -0.58f, 0.11f);
                    bool hop2 = Circ(u, v, 0.66f, -0.58f, 0.11f);
                    return washer || featherL || featherM || featherR || hop1 || hop2;
                }
                case "tiyakad": // twin tall bamboo stilts with foot-pegs + sniper horizon bar
                {
                    bool poleL = Seg(u, v, -0.38f, -0.82f, -0.28f, 0.72f, 0.08f);
                    bool poleR = Seg(u, v, 0.38f, -0.82f, 0.28f, 0.72f, 0.08f);
                    bool pegL = Seg(u, v, -0.66f, -0.22f, -0.28f, -0.22f, 0.075f);
                    bool pegR = Seg(u, v, 0.28f, -0.22f, 0.66f, -0.22f, 0.075f);
                    bool scope = Ring(u, v, 0f, 0.42f, 0.18f, 0.30f) || Seg(u, v, -0.78f, 0.42f, 0.78f, 0.42f, 0.045f);
                    return poleL || poleR || pegL || pegR || scope;
                }
                case "bato_execute": // heavy faceted boulder meteor + execution crosshair diamond
                {
                    bool rock = (Mathf.Abs(u) * 0.92f + Mathf.Abs(v + 0.06f) * 1.05f < 0.72f)
                        && (Mathf.Abs(u) < 0.62f) && (v > -0.72f && v < 0.64f);
                    bool crossH = Mathf.Abs(v + 0.04f) < 0.065f && Mathf.Abs(u) < 0.84f;
                    bool crossV = Mathf.Abs(u) < 0.065f && Mathf.Abs(v + 0.04f) < 0.84f;
                    bool coreCut = Mathf.Abs(u) + Mathf.Abs(v + 0.04f) < 0.22f;
                    bool tail = Tri(u, v, 0f, 0.88f, -0.28f, 0.56f, 0.28f, 0.56f);
                    return (rock && !coreCut) || (crossH && !rock) || (crossV && !rock) || tail;
                }

                // ---- Jopay (Defender) ----
                case "bahay_bahayan": // fortified 3-panel box barricade wall with brace
                {
                    bool b1 = RotBox(u, v, -0.52f, -0.18f, 0f, 0.22f, 0.44f);
                    bool b2 = RotBox(u, v, 0f, 0.02f, 0f, 0.23f, 0.54f);
                    bool b3 = RotBox(u, v, 0.52f, -0.18f, 0f, 0.22f, 0.44f);
                    bool roof = Tri(u, v, 0f, 0.84f, -0.78f, 0.38f, 0.78f, 0.38f) && !Tri(u, v, 0f, 0.66f, -0.52f, 0.38f, 0.52f, 0.38f);
                    bool seam = Mathf.Abs(u - 0.26f) < 0.035f || Mathf.Abs(u + 0.26f) < 0.035f;
                    return ((b1 || b2 || b3) && !seam) || roof;
                }
                case "pabitin_heal": // hanging bamboo treat rack + crisp medical heal cross
                {
                    bool cordL = Seg(u, v, 0f, 0.86f, -0.66f, 0.44f, 0.06f);
                    bool cordR = Seg(u, v, 0f, 0.86f, 0.66f, 0.44f, 0.06f);
                    bool rack = RotBox(u, v, 0f, 0.40f, 0f, 0.74f, 0.08f);
                    bool plusV = RotBox(u, v, 0f, -0.22f, 0f, 0.16f, 0.44f);
                    bool plusH = RotBox(u, v, 0f, -0.22f, 0f, 0.44f, 0.16f);
                    return cordL || cordR || rack || plusV || plusH;
                }
                case "palosebo": // upright greased bamboo pole anchored in circular base ring with slip ripples
                {
                    bool pole = RotBox(u, v, 0f, 0.08f, 0f, 0.11f, 0.74f);
                    bool flag = Tri(u, v, 0.10f, 0.80f, 0.62f, 0.62f, 0.10f, 0.42f);
                    bool baseRing = Ring(u, v, 0f, -0.58f, 0.36f, 0.50f) && v < -0.34f;
                    bool slip1 = Seg(u, v, -0.58f, 0.14f, -0.24f, -0.10f, 0.065f);
                    bool slip2 = Seg(u, v, 0.58f, -0.02f, 0.24f, -0.26f, 0.065f);
                    return pole || flag || baseRing || slip1 || slip2;
                }

                // ---- Oriang (Defender) ----
                case "chinese_garter": // two ally diamond anchor nodes joined by taut twin elastic strands
                {
                    bool nodeL = Mathf.Abs(u + 0.62f) + Mathf.Abs(v) < 0.30f;
                    bool nodeR = Mathf.Abs(u - 0.62f) + Mathf.Abs(v) < 0.30f;
                    bool holeL = Mathf.Abs(u + 0.62f) + Mathf.Abs(v) < 0.11f;
                    bool holeR = Mathf.Abs(u - 0.62f) + Mathf.Abs(v) < 0.11f;
                    bool strand1 = Seg(u, v, -0.40f, 0.16f, 0.40f, 0.16f, 0.07f);
                    bool strand2 = Seg(u, v, -0.40f, -0.16f, 0.40f, -0.16f, 0.07f);
                    bool spark = Mathf.Abs(u) + Mathf.Abs(v) < 0.18f;
                    return (nodeL && !holeL) || (nodeR && !holeR) || strand1 || strand2 || spark;
                }
                case "sungka": // carved sungka board shell pits + protective shield dome
                {
                    bool shield = Ring(u, v, 0f, -0.16f, 0.62f, 0.78f) && v > -0.06f;
                    bool board = RotBox(u, v, 0f, -0.28f, 0f, 0.66f, 0.26f)
                        || Circ(u, v, -0.66f, -0.28f, 0.26f)
                        || Circ(u, v, 0.66f, -0.28f, 0.26f);
                    bool pit1 = Circ(u, v, -0.40f, -0.28f, 0.11f);
                    bool pit2 = Circ(u, v, 0f, -0.28f, 0.11f);
                    bool pit3 = Circ(u, v, 0.40f, -0.28f, 0.11f);
                    return shield || (board && !pit1 && !pit2 && !pit3);
                }
                case "langit": // ascending sanctuary wings + invulnerability halo crown
                {
                    bool halo = Ring(u, v, 0f, 0.56f, 0.16f, 0.28f);
                    bool wingL1 = Tri(u, v, -0.10f, 0.08f, -0.84f, 0.46f, -0.60f, -0.16f);
                    bool wingL2 = Tri(u, v, -0.12f, -0.12f, -0.68f, -0.22f, -0.34f, -0.62f);
                    bool wingR1 = Tri(u, v, 0.10f, 0.08f, 0.84f, 0.46f, 0.60f, -0.16f);
                    bool wingR2 = Tri(u, v, 0.12f, -0.12f, 0.68f, -0.22f, 0.34f, -0.62f);
                    bool core = Mathf.Abs(u) + Mathf.Abs(v + 0.08f) < 0.24f;
                    return halo || wingL1 || wingL2 || wingR1 || wingR2 || core;
                }

                // ---- Paeng (Defender) ----
                case "sack_race": // tied burlap sack charging forward + impact shock arcs
                {
                    bool body = Circ(u, v, -0.12f, -0.24f, 0.48f) || RotBox(u, v, -0.12f, -0.10f, 0f, 0.40f, 0.34f);
                    bool neck = Tri(u, v, -0.12f, 0.26f, -0.38f, 0.68f, 0.14f, 0.68f);
                    bool tie = Mathf.Abs(v - 0.28f) < 0.055f && Mathf.Abs(u + 0.12f) < 0.32f;
                    bool shock1 = Ring(u, v, -0.06f, -0.10f, 0.62f, 0.76f) && u > 0.36f;
                    bool shock2 = Seg(u, v, 0.56f, 0.44f, 0.82f, 0.60f, 0.07f);
                    return ((body || neck) && !tie) || shock1 || shock2;
                }
                case "luksong_baka": // vaulted springboard leap arrow arcing cleanly over crouched hump
                {
                    bool hump = Circ(u, v, 0f, -0.46f, 0.42f) && v > -0.66f;
                    bool arc = Ring(u, v, 0f, -0.18f, 0.54f, 0.72f) && v > -0.04f;
                    bool arrow = Tri(u, v, 0.84f, 0.14f, 0.44f, 0.38f, 0.48f, -0.10f);
                    return hump || arc || arrow;
                }
                case "tinik_wall": // row of 4 rising spiky thorn pillars across lane
                {
                    bool baseBar = RotBox(u, v, 0f, -0.64f, 0f, 0.82f, 0.12f);
                    bool s1 = Tri(u, v, -0.60f, 0.46f, -0.78f, -0.56f, -0.42f, -0.56f);
                    bool s2 = Tri(u, v, -0.20f, 0.80f, -0.40f, -0.56f, 0.00f, -0.56f);
                    bool s3 = Tri(u, v, 0.20f, 0.80f, 0.00f, -0.56f, 0.40f, -0.56f);
                    bool s4 = Tri(u, v, 0.60f, 0.46f, 0.42f, -0.56f, 0.78f, -0.56f);
                    return baseBar || s1 || s2 || s3 || s4;
                }

                // ---- Kikay (Seeker) ----
                case "teks_tripwire": // twin angled teks cards spanned by taut laser tripwire + trigger node
                {
                    bool cardL = RotBox(u, v, -0.62f, 0f, 0.18f, 0.20f, 0.38f);
                    bool cardR = RotBox(u, v, 0.62f, 0f, -0.18f, 0.20f, 0.38f);
                    bool wire = Seg(u, v, -0.44f, 0f, 0.44f, 0f, 0.06f);
                    bool spark = Mathf.Abs(u) + Mathf.Abs(v) < 0.22f;
                    bool dot = Circ(u, v, 0f, 0f, 0.08f);
                    return cardL || cardR || wire || (spark && !dot);
                }
                case "tago": // stealth eye slashed by sleek diagonal cloak bar
                {
                    bool eye = Circ(u, v, 0f, -0.46f, 0.88f) && Circ(u, v, 0f, 0.46f, 0.88f);
                    bool pupil = Circ(u, v, 0f, 0f, 0.22f);
                    bool slashGap = Seg(u, v, -0.78f, -0.68f, 0.78f, 0.68f, 0.14f);
                    bool slash = Seg(u, v, -0.78f, -0.68f, 0.78f, 0.68f, 0.075f);
                    return (eye && !pupil && !slashGap) || slash;
                }
                case "saranggola": // diamond Filipino kite + ribbon tail hovering over radar circle
                {
                    bool kite = Mathf.Abs(u) * 1.15f + Mathf.Abs(v - 0.24f) < 0.56f;
                    bool ribV = Mathf.Abs(u) < 0.045f && v > -0.32f && v < 0.80f;
                    bool ribH = Mathf.Abs(v - 0.24f) < 0.045f && Mathf.Abs(u) < 0.48f;
                    bool tail = Seg(u, v, 0f, -0.30f, -0.22f, -0.56f, 0.06f) || Seg(u, v, -0.22f, -0.56f, 0.16f, -0.82f, 0.06f);
                    bool scan = Ring(u, v, 0f, -0.42f, 0.48f, 0.60f) && v < -0.28f;
                    return (kite && !(ribV || ribH)) || tail || scan;
                }

                // ---- Karding (Seeker) ----
                case "yoyo": // yoyo spool on taut diagonal string with pullback hook arrow
                {
                    bool spool = Circ(u, v, 0.34f, -0.26f, 0.40f) && !Ring(u, v, 0.34f, -0.26f, 0.14f, 0.24f);
                    bool cord = Seg(u, v, -0.62f, 0.62f, 0.12f, -0.04f, 0.075f);
                    bool hook = Tri(u, v, -0.78f, 0.78f, -0.34f, 0.74f, -0.74f, 0.34f);
                    return spool || cord || hook;
                }
                case "piko": // classic hopscotch piko chalk grid with center sweeper pip
                {
                    bool top = RotBox(u, v, 0f, 0.56f, 0f, 0.24f, 0.20f) && !RotBox(u, v, 0f, 0.56f, 0f, 0.12f, 0.09f);
                    bool midL = RotBox(u, v, -0.30f, 0.12f, 0f, 0.24f, 0.20f) && !RotBox(u, v, -0.30f, 0.12f, 0f, 0.12f, 0.09f);
                    bool midR = RotBox(u, v, 0.30f, 0.12f, 0f, 0.24f, 0.20f) && !RotBox(u, v, 0.30f, 0.12f, 0f, 0.12f, 0.09f);
                    bool bot = RotBox(u, v, 0f, -0.32f, 0f, 0.24f, 0.20f) && !RotBox(u, v, 0f, -0.32f, 0f, 0.12f, 0.09f);
                    bool foot = RotBox(u, v, 0f, -0.72f, 0f, 0.36f, 0.14f);
                    bool pip = Circ(u, v, 0f, 0.56f, 0.08f);
                    return top || midL || midR || bot || foot || pip;
                }
                case "pogs_slam": // heavy beveled pog disc slamming down with shockwave ring
                {
                    bool pog = Circ(u, v, 0f, 0.14f, 0.42f) && !Ring(u, v, 0f, 0.14f, 0.20f, 0.29f);
                    bool wave = Ring(u, v, 0f, -0.22f, 0.64f, 0.80f) && v < 0.05f;
                    bool slamL = Seg(u, v, -0.64f, 0.68f, -0.34f, 0.42f, 0.07f);
                    bool slamR = Seg(u, v, 0.64f, 0.68f, 0.34f, 0.42f, 0.07f);
                    return pog || wave || slamL || slamR;
                }

                // ---- Manoy (Seeker) ----
                case "sili_tubig": // chili nozzle spraying a 3-ray mist cone
                {
                    bool pod = (Circ(u, v, -0.52f, -0.14f, 0.26f) || Tri(u, v, -0.82f, -0.58f, -0.68f, -0.02f, -0.28f, -0.30f));
                    bool stem = Seg(u, v, -0.44f, 0.12f, -0.26f, 0.34f, 0.065f);
                    bool ray1 = Seg(u, v, -0.12f, 0.08f, 0.72f, 0.60f, 0.075f);
                    bool ray2 = Seg(u, v, -0.08f, -0.04f, 0.80f, -0.02f, 0.075f);
                    bool ray3 = Seg(u, v, -0.12f, -0.16f, 0.70f, -0.62f, 0.075f);
                    return pod || stem || ray1 || ray2 || ray3;
                }
                case "trumpo_bantay": // upright sentry top emitting twin sonar ping arcs overhead
                {
                    bool sentry = Tri(u, v, 0f, -0.80f, -0.32f, -0.16f, 0.32f, -0.16f);
                    bool head = Circ(u, v, 0f, -0.08f, 0.16f);
                    bool ping1 = Ring(u, v, 0f, -0.08f, 0.38f, 0.50f) && v > 0.08f;
                    bool ping2 = Ring(u, v, 0f, -0.08f, 0.66f, 0.78f) && v > 0.14f;
                    return sentry || head || ping1 || ping2;
                }
                case "taya": // all-seeing tactical map-reveal eye inside 4 corner brackets
                {
                    bool eye = Circ(u, v, 0f, -0.52f, 0.86f) && Circ(u, v, 0f, 0.52f, 0.86f);
                    bool iris = Ring(u, v, 0f, 0f, 0.12f, 0.25f);
                    bool bTL = (Mathf.Abs(u + 0.74f) < 0.075f && v > 0.34f && v < 0.80f) || (Mathf.Abs(v - 0.74f) < 0.075f && u > -0.80f && u < -0.34f);
                    bool bTR = (Mathf.Abs(u - 0.74f) < 0.075f && v > 0.34f && v < 0.80f) || (Mathf.Abs(v - 0.74f) < 0.075f && u < 0.80f && u > 0.34f);
                    bool bBL = (Mathf.Abs(u + 0.74f) < 0.075f && v < -0.34f && v > -0.80f) || (Mathf.Abs(v + 0.74f) < 0.075f && u > -0.80f && u < -0.34f);
                    bool bBR = (Mathf.Abs(u - 0.74f) < 0.075f && v < -0.34f && v > -0.80f) || (Mathf.Abs(v + 0.74f) < 0.075f && u < 0.80f && u > 0.34f);
                    return (eye && !iris) || bTL || bTR || bBL || bBR;
                }

                // ---- Tere (Controller) ----
                case "jackstone": // 6-arm star jack caltrop + twin bouncing rubber ball pips
                {
                    bool a1 = Seg(u, v, -0.58f, 0f, 0.58f, 0f, 0.09f);
                    bool a2 = Seg(u, v, -0.30f, -0.52f, 0.30f, 0.52f, 0.09f);
                    bool a3 = Seg(u, v, -0.30f, 0.52f, 0.30f, -0.52f, 0.09f);
                    bool tip1 = Circ(u, v, -0.62f, 0f, 0.13f) || Circ(u, v, 0.62f, 0f, 0.13f);
                    bool tip2 = Circ(u, v, 0.32f, 0.56f, 0.13f) || Circ(u, v, -0.32f, -0.56f, 0.13f);
                    bool tip3 = Circ(u, v, -0.32f, 0.56f, 0.13f) || Circ(u, v, 0.32f, -0.56f, 0.13f);
                    bool ball = Circ(u, v, 0.64f, -0.56f, 0.18f);
                    return a1 || a2 || a3 || tip1 || tip2 || tip3 || ball;
                }
                case "luksong_lubid": // twin handles spanned by sweeping curved jump-rope snare arc
                {
                    bool postL = RotBox(u, v, -0.72f, 0.24f, 0f, 0.11f, 0.36f);
                    bool postR = RotBox(u, v, 0.72f, 0.24f, 0f, 0.11f, 0.36f);
                    bool rope = Ring(u, v, 0f, 0.30f, 0.66f, 0.80f) && v < 0.24f;
                    bool knot = Circ(u, v, 0f, -0.44f, 0.16f);
                    return postL || postR || rope || knot;
                }
                case "garter_net": // woven diamond lattice net inside circular containment ring
                {
                    bool ring = Ring(u, v, 0f, 0f, 0.68f, 0.82f);
                    bool inDisc = Circ(u, v, 0f, 0f, 0.70f);
                    bool l1 = Seg(u, v, -0.68f, -0.22f, 0.22f, 0.68f, 0.055f);
                    bool l2 = Seg(u, v, -0.22f, -0.68f, 0.68f, 0.22f, 0.055f);
                    bool l3 = Seg(u, v, -0.68f, 0.22f, 0.22f, -0.68f, 0.055f);
                    bool l4 = Seg(u, v, -0.22f, 0.68f, 0.68f, -0.22f, 0.055f);
                    return ring || (inDisc && (l1 || l2 || l3 || l4));
                }

                // ---- Pamela (Controller) ----
                case "holens_spill": // scatter cone spilling 5 crisp glass marbles
                {
                    bool m1 = Circ(u, v, -0.56f, -0.44f, 0.20f) && !Circ(u, v, -0.62f, -0.38f, 0.06f);
                    bool m2 = Circ(u, v, -0.10f, -0.18f, 0.21f) && !Circ(u, v, -0.16f, -0.12f, 0.06f);
                    bool m3 = Circ(u, v, 0.44f, -0.36f, 0.19f) && !Circ(u, v, 0.38f, -0.30f, 0.06f);
                    bool m4 = Circ(u, v, 0.02f, 0.44f, 0.20f) && !Circ(u, v, -0.04f, 0.50f, 0.06f);
                    bool m5 = Circ(u, v, 0.56f, 0.30f, 0.18f) && !Circ(u, v, 0.50f, 0.36f, 0.055f);
                    return m1 || m2 || m3 || m4 || m5;
                }
                case "lata_morph": // dented tumbang-preso tin can topped by morph spiral halo
                {
                    bool canBody = RotBox(u, v, 0f, -0.22f, 0f, 0.38f, 0.46f);
                    bool rib1 = Mathf.Abs(v + 0.04f) < 0.045f && Mathf.Abs(u) < 0.36f;
                    bool rib2 = Mathf.Abs(v + 0.38f) < 0.045f && Mathf.Abs(u) < 0.36f;
                    bool dizzy1 = Ring(u, v, 0f, 0.54f, 0.16f, 0.28f) && u > -0.18f;
                    bool dizzy2 = Ring(u, v, -0.10f, 0.54f, 0.34f, 0.44f) && v > 0.38f;
                    return (canBody && !rib1 && !rib2) || dizzy1 || dizzy2;
                }
                case "patintero_lines": // 3 parallel chalk lane lines crossed by an angled stun bolt
                {
                    bool r1 = RotBox(u, v, 0f, 0.52f, 0f, 0.78f, 0.085f);
                    bool r2 = RotBox(u, v, 0f, 0.00f, 0f, 0.78f, 0.085f);
                    bool r3 = RotBox(u, v, 0f, -0.52f, 0f, 0.78f, 0.085f);
                    bool bolt = Seg(u, v, 0.24f, 0.80f, -0.22f, 0.08f, 0.10f)
                        || Seg(u, v, -0.22f, 0.08f, 0.20f, -0.08f, 0.10f)
                        || Seg(u, v, 0.20f, -0.08f, -0.26f, -0.80f, 0.10f);
                    return r1 || r2 || r3 || bolt;
                }

                // ---- Domeng (Controller) ----
                case "pabitin_snare": // descending snare cage with downward lock chevrons
                {
                    bool topBar = RotBox(u, v, 0f, 0.54f, 0f, 0.76f, 0.09f);
                    bool barL = RotBox(u, v, -0.54f, 0.06f, 0f, 0.08f, 0.46f);
                    bool barM = RotBox(u, v, 0f, 0.06f, 0f, 0.08f, 0.46f);
                    bool barR = RotBox(u, v, 0.54f, 0.06f, 0f, 0.08f, 0.46f);
                    bool botBar = RotBox(u, v, 0f, -0.34f, 0f, 0.76f, 0.09f);
                    bool downArrow = Tri(u, v, 0f, -0.86f, -0.34f, -0.50f, 0.34f, -0.50f);
                    return topBar || barL || barM || barR || botBar || downArrow;
                }
                case "sako_trap": // jawed open burlap snare trap with central trigger diamond
                {
                    bool jawL = Ring(u, v, 0f, 0f, 0.54f, 0.74f) && u < -0.14f;
                    bool jawR = Ring(u, v, 0f, 0f, 0.54f, 0.74f) && u > 0.14f;
                    bool tooth1 = Tri(u, v, -0.22f, 0.32f, -0.56f, 0.44f, -0.52f, 0.16f);
                    bool tooth2 = Tri(u, v, 0.22f, 0.32f, 0.56f, 0.44f, 0.52f, 0.16f);
                    bool tooth3 = Tri(u, v, -0.22f, -0.32f, -0.56f, -0.44f, -0.52f, -0.16f);
                    bool tooth4 = Tri(u, v, 0.22f, -0.32f, 0.56f, -0.44f, 0.52f, -0.16f);
                    bool trigger = Mathf.Abs(u) + Mathf.Abs(v) < 0.26f;
                    return jawL || jawR || tooth1 || tooth2 || tooth3 || tooth4 || trigger;
                }
                case "lupa_quake": // ground fracture fissure erupting in 3 seismic spire spikes
                {
                    bool baseSlab = RotBox(u, v, 0f, -0.62f, 0f, 0.84f, 0.13f);
                    bool midSpire = Tri(u, v, 0f, 0.84f, -0.30f, -0.50f, 0.30f, -0.50f);
                    bool leftSpire = Tri(u, v, -0.54f, 0.44f, -0.78f, -0.50f, -0.26f, -0.50f);
                    bool rightSpire = Tri(u, v, 0.54f, 0.44f, 0.26f, -0.50f, 0.78f, -0.50f);
                    bool fissure = Seg(u, v, 0f, 0.44f, -0.10f, -0.14f, 0.045f) || Seg(u, v, -0.10f, -0.14f, 0.10f, -0.48f, 0.045f);
                    return baseSlab || ((midSpire || leftSpire || rightSpire) && !fissure);
                }

                // ---- Core Combat Controls ----
                case "tirador": // classic Y-shaped wooden tirador slingshot fork + elastic band + centered stone
                {
                    bool handle = Seg(u, v, 0f, -0.80f, 0f, -0.06f, 0.12f);
                    bool forkL = Seg(u, v, 0f, -0.08f, -0.54f, 0.68f, 0.11f);
                    bool forkR = Seg(u, v, 0f, -0.08f, 0.54f, 0.68f, 0.11f);
                    bool bandL = Seg(u, v, -0.54f, 0.68f, 0f, 0.24f, 0.055f);
                    bool bandR = Seg(u, v, 0.54f, 0.68f, 0f, 0.24f, 0.055f);
                    bool stone = Circ(u, v, 0f, 0.24f, 0.16f);
                    return handle || forkL || forkR || bandL || bandR || stone;
                }
                case "reload": // circular twin-arrow reload loop
                {
                    bool arcTop = Ring(u, v, 0f, 0f, 0.48f, 0.66f) && v > 0.08f;
                    bool arcBot = Ring(u, v, 0f, 0f, 0.48f, 0.66f) && v < -0.08f;
                    bool tipR = Tri(u, v, 0.64f, -0.06f, 0.36f, 0.24f, 0.80f, 0.24f);
                    bool tipL = Tri(u, v, -0.64f, 0.06f, -0.36f, -0.24f, -0.80f, -0.24f);
                    return arcTop || arcBot || tipR || tipL;
                }
                case "jump": // bold upward double launch chevron
                {
                    bool up1 = Seg(u, v, -0.54f, 0.06f, 0f, 0.64f, 0.11f) || Seg(u, v, 0f, 0.64f, 0.54f, 0.06f, 0.11f);
                    bool up2 = Seg(u, v, -0.46f, -0.46f, 0f, 0.08f, 0.10f) || Seg(u, v, 0f, 0.08f, 0.46f, -0.46f, 0.10f);
                    return up1 || up2;
                }
                case "slide": // low-profile forward speed-slide dash
                {
                    bool body = Seg(u, v, -0.64f, 0.34f, 0.36f, -0.34f, 0.13f);
                    bool floorLine = Seg(u, v, -0.78f, -0.56f, 0.78f, -0.56f, 0.075f);
                    bool tip = Tri(u, v, 0.78f, -0.34f, 0.30f, -0.06f, 0.30f, -0.48f);
                    return body || floorLine || tip;
                }
                case "vault": // crisp hurdle-vault arc over low rail
                {
                    bool rail = RotBox(u, v, 0f, -0.42f, 0f, 0.58f, 0.11f);
                    bool arc = Ring(u, v, 0f, -0.22f, 0.52f, 0.70f) && v > -0.10f;
                    bool head = Tri(u, v, 0.80f, 0.04f, 0.42f, 0.28f, 0.46f, -0.18f);
                    return rail || arc || head;
                }
                case "ads": // precision 4-tick sniper scope ring
                {
                    bool ring = Ring(u, v, 0f, 0f, 0.46f, 0.62f);
                    bool tL = Seg(u, v, -0.84f, 0f, -0.34f, 0f, 0.075f);
                    bool tR = Seg(u, v, 0.34f, 0f, 0.84f, 0f, 0.075f);
                    bool tT = Seg(u, v, 0f, 0.34f, 0f, 0.84f, 0.075f);
                    bool tB = Seg(u, v, 0f, -0.84f, 0f, -0.34f, 0.075f);
                    bool dot = Circ(u, v, 0f, 0f, 0.10f);
                    return ring || tL || tR || tT || tB || dot;
                }

                // ---- Clean Tactical Diamond Fallback (never text) ----
                default:
                {
                    float d = Mathf.Abs(u) + Mathf.Abs(v);
                    bool outer = d < 0.76f && d > 0.54f;
                    bool core = d < 0.24f;
                    return outer || core;
                }
            }
        }
    }
}
