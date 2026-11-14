using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Compact toggleable barangay tactical street radar (<c>SetVisible(bool)</c>, default OFF per binding GDD landscape rule
    /// unless explicitly toggled on). Renders circular barangay streets, Estero canal &amp; 3 bridges, own/enemy bases &amp; posts,
    /// NW/SE rescue zones, Merienda stands, local player + camera FOV rays, and free/tagged allies with <c>0 B</c> per-frame GC.
    /// </summary>
    public sealed class BarangayMinimapView : IDisposable
    {
        public const float PanelSize = 148f;
        public const float RadarRadiusPx = 66f;
        public const float DefaultWorldRadius = 84f;
        const int BackdropTexSize = 256;
        const int MaxKidDots = 12;
        const int MaxMeriendaDots = 4;
        const float FovHalfAngleRad = 0.44f;
        const float FovRayLengthPx = 22f;

        static readonly Color CaptureGold = new Color(1.00f, 0.82f, 0.32f, 0.94f);
        static readonly Color RescueCooldownWhite = new Color(1.00f, 1.00f, 1.00f, 0.50f);

        public RectTransform Root { get; }
        public bool IsVisible { get; private set; }
        public bool ShowEnemies { get; set; }
        public bool LeftHanded { get; private set; }

        readonly Image _backdropImage;
        readonly Image[] _rescueRects = new Image[2];
        readonly Image[] _baseRings = new Image[2];
        readonly Image[] _basePosts = new Image[2];
        readonly Image[] _captureHalos = new Image[2];
        readonly Image[] _meriendaDots = new Image[MaxMeriendaDots];
        readonly Image[] _kidDots = new Image[MaxKidDots];
        readonly RectTransform _selfDotRect;
        readonly Image _selfDotImage;
        readonly RectTransform _fovLeftRect;
        readonly RectTransform _fovRightRect;

        MapLayoutData _layout;
        Texture2D _backdropTex;
        Sprite _backdropSprite;
        bool _disposed;

        public BarangayMinimapView(Transform parent, MapLayoutData layout = null, Canvas canvas = null, bool initialVisible = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _layout = layout;

            Root = Ui.Panel(
                "BarangayMinimap",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -14f),
                new Vector2(PanelSize, PanelSize));

            var backdropGo = new GameObject("RadarBackdrop", typeof(RectTransform), typeof(Image));
            backdropGo.transform.SetParent(Root, false);
            _backdropImage = backdropGo.GetComponent<Image>();
            _backdropImage.raycastTarget = false;
            _backdropImage.rectTransform.anchorMin = Vector2.zero;
            _backdropImage.rectTransform.anchorMax = Vector2.one;
            _backdropImage.rectTransform.sizeDelta = Vector2.zero;

            RebuildBackdropTexture();

            // 2 Rescue Zones (own + enemy)
            for (int i = 0; i < 2; i++)
            {
                Image rescue = KomiksUi.MakeSlicedFill($"Rescue_{i}", Root, RescueCooldownWhite, 3);
                SetCenteredPivot(rescue.rectTransform, new Vector2(10f, 14f));
                _rescueRects[i] = rescue;
            }

            // 2 Base circles + electric posts + capture pulse halos
            Sprite ringSprite = KomiksUi.RingSprite(64, 4);
            for (int i = 0; i < 2; i++)
            {
                Image halo = Ui.MakeImage($"BaseCaptureHalo_{i}", Root, CaptureGold);
                halo.sprite = ringSprite;
                SetCenteredPivot(halo.rectTransform, new Vector2(22f, 22f));
                halo.gameObject.SetActive(false);
                _captureHalos[i] = halo;

                Image ring = Ui.MakeImage($"BaseRing_{i}", Root, Color.white);
                ring.sprite = ringSprite;
                SetCenteredPivot(ring.rectTransform, new Vector2(16f, 16f));
                _baseRings[i] = ring;

                Image post = Ui.MakeImage($"BasePost_{i}", Root, Color.white, round: true);
                SetCenteredPivot(post.rectTransform, new Vector2(6f, 6f));
                _basePosts[i] = post;
            }

            // 3+ Merienda stands along mid-canal
            for (int i = 0; i < MaxMeriendaDots; i++)
            {
                Image stand = Ui.MakeImage($"Merienda_{i}", Root, KomiksUi.MangoGold, round: true);
                SetCenteredPivot(stand.rectTransform, new Vector2(7f, 7f));
                stand.gameObject.SetActive(false);
                _meriendaDots[i] = stand;
            }

            // Remote ally (and optional enemy) dots
            for (int i = 0; i < MaxKidDots; i++)
            {
                Image dot = Ui.MakeImage($"KidDot_{i}", Root, Color.white, round: true);
                SetCenteredPivot(dot.rectTransform, new Vector2(8f, 8f));
                dot.gameObject.SetActive(false);
                _kidDots[i] = dot;
            }

            // Local camera FOV rays + self dot on top
            _fovLeftRect = BuildFovRay("FovRayLeft");
            _fovRightRect = BuildFovRay("FovRayRight");

            _selfDotImage = Ui.MakeImage("SelfDot", Root, KomiksUi.SelfHealthColor(0), round: true);
            _selfDotRect = _selfDotImage.rectTransform;
            SetCenteredPivot(_selfDotRect, new Vector2(10f, 10f));
            _selfDotImage.gameObject.SetActive(false);

            // Binding GDD landscape rule: minimap is OFF by default unless explicitly toggled on
            SetVisible(initialVisible);
        }

        RectTransform BuildFovRay(string name)
        {
            Image ray = Ui.MakeImage(name, Root, new Color(1f, 1f, 1f, 0.36f));
            RectTransform rt = ray.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(2f, FovRayLengthPx);
            ray.gameObject.SetActive(false);
            return rt;
        }

        static void SetCenteredPivot(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            IsVisible = visible;
            if (Root != null && Root.gameObject.activeSelf != visible)
                Root.gameObject.SetActive(visible);
        }

        public void Toggle() => SetVisible(!IsVisible);

        public void SetLeftHanded(bool leftHanded)
        {
            if (_disposed || LeftHanded == leftHanded) return;
            LeftHanded = leftHanded;
            if (leftHanded)
            {
                Root.anchorMin = Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(1f, 1f);
                Root.anchoredPosition = new Vector2(-18f, -14f);
            }
            else
            {
                Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = new Vector2(0f, 1f);
                Root.anchoredPosition = new Vector2(18f, -14f);
            }
        }

        public void SetLayout(MapLayoutData layout)
        {
            if (_disposed) return;
            _layout = layout;
            RebuildBackdropTexture();
        }

        /// <summary>
        /// Converts sim ground XZ into centered UGUI radar coordinates (+X right, +Y up) rotated by <paramref name="ownSide"/>
        /// so the local team's own base (+Z for Team 0, -Z for Team 1) always sits at the bottom (-Y) and the enemy base at the top (+Y).
        /// </summary>
        public static Vector2 WorldToRadar(SVector2 simXz, float arenaRadius, int ownSide)
        {
            float r = Mathf.Max(1f, arenaRadius);
            float scalePx = RadarRadiusPx / r;
            float side = ownSide >= 0 ? 1f : -1f;
            var p = new Vector2(simXz.X * side * scalePx, -simXz.Y * side * scalePx);
            float maxLen = RadarRadiusPx - 2f;
            float sq = p.sqrMagnitude;
            if (sq > maxLen * maxLen)
                p = p * (maxLen / Mathf.Sqrt(sq));
            return p;
        }

        /// <summary>Syncs radar markers with 0 B heap allocation when visible (no-op when hidden).</summary>
        public void Refresh(SimBridge sim, float cameraYawDegrees = 0f)
        {
            if (_disposed || !IsVisible || sim == null)
                return;

            float worldR = sim.ArenaRadius > 1f
                ? sim.ArenaRadius
                : (_layout != null && _layout.ArenaRadius > 1f ? _layout.ArenaRadius : DefaultWorldRadius);
            int localId = sim.LocalPlayerId;
            int localTeam = sim.LocalPlayerTeam;
            int ownSide = localTeam == 0 ? 1 : -1;
            int cbMode = SettingsStore.Current.ColorblindMode;
            float scalePx = RadarRadiusPx / Mathf.Max(1f, worldR);

            SyncBases(sim, worldR, ownSide, cbMode, scalePx);
            SyncRescues(sim, worldR, ownSide, localTeam, cbMode, scalePx);
            SyncMeriendas(sim, worldR, ownSide);
            SyncKids(sim, worldR, ownSide, localId, localTeam, cbMode, cameraYawDegrees);
        }

        void SyncBases(SimBridge sim, float worldR, int ownSide, int cbMode, float scalePx)
        {
            IReadOnlyList<BaseObjectiveSnapshot> bases = sim.Bases;
            for (int t = 0; t < 2; t++)
            {
                SVector2 center;
                SVector2 postPos;
                float radiusM;
                bool capturing = false;

                if (bases != null && t < bases.Count && bases[t].Radius > 0.1f)
                {
                    BaseObjectiveSnapshot b = bases[t];
                    center = b.Center;
                    postPos = b.PostPosition;
                    radiusM = b.Radius;
                    capturing = sim.Phase == MatchPhaseKind.Playing && b.CapturingTeam >= 0 && b.CaptureProgress01 > 0.001f;
                }
                else if (_layout != null)
                {
                    int side = t == 0 ? 1 : -1;
                    center = _layout.BaseCenter(side);
                    postPos = _layout.PostPosition(side);
                    radiusM = _layout.BaseRadius;
                }
                else
                {
                    float z = (t == 0 ? 1f : -1f) * (worldR - 14f);
                    center = new SVector2(0f, z);
                    postPos = new SVector2(0f, z + (t == 0 ? 3.5f : -3.5f));
                    radiusM = 8f;
                }

                Color col = KomiksUi.TeamColor(t, sim.LocalPlayerTeam, cbMode);
                float diamPx = Mathf.Clamp(radiusM * 2f * scalePx, 12f, 24f);

                RectTransform ringRt = _baseRings[t].rectTransform;
                ringRt.anchoredPosition = WorldToRadar(center, worldR, ownSide);
                ringRt.sizeDelta = new Vector2(diamPx, diamPx);
                _baseRings[t].color = col;

                RectTransform postRt = _basePosts[t].rectTransform;
                postRt.anchoredPosition = WorldToRadar(postPos, worldR, ownSide);
                _basePosts[t].color = Color.Lerp(col, Color.white, 0.35f);

                Image halo = _captureHalos[t];
                if (halo.gameObject.activeSelf != capturing)
                    halo.gameObject.SetActive(capturing);
                if (capturing)
                {
                    float pulse = SettingsStore.Current.ReduceMotion
                        ? 1f
                        : (1f + 0.14f * Mathf.Sin(sim.ClockSeconds * 7f));
                    halo.rectTransform.anchoredPosition = ringRt.anchoredPosition;
                    halo.rectTransform.sizeDelta = new Vector2((diamPx + 7f) * pulse, (diamPx + 7f) * pulse);
                }
            }
        }

        void SyncRescues(SimBridge sim, float worldR, int ownSide, int localTeam, int cbMode, float scalePx)
        {
            IReadOnlyList<RescueObjectiveSnapshot> rescues = sim.Rescues;
            for (int t = 0; t < 2; t++)
            {
                GroundRect zone;
                bool ready = true;
                if (rescues != null && t < rescues.Count && rescues[t].Zone.W > 0.1f)
                {
                    RescueObjectiveSnapshot r = rescues[t];
                    zone = r.Zone;
                    ready = r.Ready || r.CooldownLeft <= 0.001f;
                }
                else if (_layout != null)
                {
                    zone = _layout.RescueRect(t == 0 ? 1 : -1);
                }
                else
                {
                    float side = t == 0 ? 1f : -1f;
                    zone = new GroundRect(-side * (worldR * 0.78f), -side * (worldR * 0.375f), 5f, 8f);
                }

                Image img = _rescueRects[t];
                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = WorldToRadar(zone.Center, worldR, ownSide);
                rt.sizeDelta = new Vector2(
                    Mathf.Clamp(zone.W * scalePx, 7f, 18f),
                    Mathf.Clamp(zone.H * scalePx, 9f, 22f));

                if (ready)
                {
                    Color c = KomiksUi.TeamColor(t, localTeam, cbMode);
                    c.a = 0.78f;
                    img.color = c;
                }
                else
                {
                    img.color = RescueCooldownWhite;
                }
            }
        }

        void SyncMeriendas(SimBridge sim, float worldR, int ownSide)
        {
            IReadOnlyList<MeriendaStandSnapshot> stands = sim.MeriendaStands;
            int count = stands != null ? Mathf.Min(stands.Count, MaxMeriendaDots) : 0;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    MeriendaStandSnapshot s = stands[i];
                    Image dot = _meriendaDots[i];
                    if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = WorldToRadar(s.Position, worldR, ownSide);
                    Color c = KomiksUi.MeriendaColor(s.Item);
                    c.a = s.Active ? 0.96f : 0.26f;
                    dot.color = c;
                }
                for (int i = count; i < MaxMeriendaDots; i++)
                {
                    if (_meriendaDots[i].gameObject.activeSelf)
                        _meriendaDots[i].gameObject.SetActive(false);
                }
                return;
            }

            int fallbackCount = (_layout != null && _layout.PowerupSpots != null)
                ? Mathf.Min(_layout.PowerupSpots.Length, MaxMeriendaDots)
                : 0;
            for (int i = 0; i < MaxMeriendaDots; i++)
            {
                Image dot = _meriendaDots[i];
                bool on = i < fallbackCount;
                if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
                if (on)
                {
                    dot.rectTransform.anchoredPosition = WorldToRadar(_layout.PowerupSpots[i], worldR, ownSide);
                    dot.color = KomiksUi.MeriendaColor((BuffKind)((i % 3) + 1));
                }
            }
        }

        void SyncKids(SimBridge sim, float worldR, int ownSide, int localId, int localTeam, int cbMode, float cameraYawDeg)
        {
            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            int n = kids.Count;
            int dotCursor = 0;
            bool hasSelf = false;

            for (int i = 0; i < n; i++)
            {
                KidSnapshot kid = kids[i];
                if (kid.Id == localId)
                {
                    hasSelf = true;
                    Vector2 selfPos = WorldToRadar(kid.Position, worldR, ownSide);
                    _selfDotRect.anchoredPosition = selfPos;
                    Color selfCol = KomiksUi.SelfHealthColor(cbMode);
                    selfCol.a = kid.Tagged ? 0.48f : 1f;
                    _selfDotImage.color = selfCol;

                    // Camera forward in sim XZ is (-sin(yaw), -cos(yaw)); map to UGUI radar (+X right, +Y up)
                    float yawRad = cameraYawDeg * Mathf.Deg2Rad;
                    float side = ownSide >= 0 ? 1f : -1f;
                    var radarFwd = new Vector2(-Mathf.Sin(yawRad) * side, Mathf.Cos(yawRad) * side);
                    if (radarFwd.sqrMagnitude < 0.0001f) radarFwd = Vector2.up;
                    else radarFwd.Normalize();

                    float centerAngleDeg = Mathf.Atan2(-radarFwd.x, radarFwd.y) * Mathf.Rad2Deg;
                    float halfDeg = FovHalfAngleRad * Mathf.Rad2Deg;

                    _fovLeftRect.anchoredPosition = selfPos;
                    _fovLeftRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg + halfDeg);
                    _fovRightRect.anchoredPosition = selfPos;
                    _fovRightRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg - halfDeg);
                    continue;
                }

                bool isAlly = kid.Team == localTeam;
                if (!isAlly && !ShowEnemies) continue;
                if (dotCursor >= MaxKidDots) continue;

                Image dot = _kidDots[dotCursor++];
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = WorldToRadar(kid.Position, worldR, ownSide);
                Color c = isAlly ? KomiksUi.TeamAllyColor(cbMode) : KomiksUi.TeamEnemyColor(cbMode);
                c.a = kid.Tagged ? 0.42f : 0.96f;
                dot.color = c;
            }

            if (_selfDotImage.gameObject.activeSelf != hasSelf)
            {
                _selfDotImage.gameObject.SetActive(hasSelf);
                _fovLeftRect.gameObject.SetActive(hasSelf);
                _fovRightRect.gameObject.SetActive(hasSelf);
            }

            for (int i = dotCursor; i < MaxKidDots; i++)
            {
                if (_kidDots[i].gameObject.activeSelf)
                    _kidDots[i].gameObject.SetActive(false);
            }
        }

        void RebuildBackdropTexture()
        {
            if (_disposed) return;

            if (_backdropTex == null)
            {
                _backdropTex = new Texture2D(BackdropTexSize, BackdropTexSize, TextureFormat.RGBA32, false)
                {
                    name = "BarangayMinimapBackdrop",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            var pixels = new Color32[BackdropTexSize * BackdropTexSize];
            float half = BackdropTexSize * 0.5f;
            float rPx = (RadarRadiusPx / PanelSize) * BackdropTexSize;
            float worldR = (_layout != null && _layout.ArenaRadius > 1f) ? _layout.ArenaRadius : DefaultWorldRadius;
            float mPerPx = worldR / Mathf.Max(1f, rPx);

            for (int y = 0; y < BackdropTexSize; y++)
            {
                for (int x = 0; x < BackdropTexSize; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float edgeAlpha = Mathf.Clamp01(rPx + 0.6f - dist);
                    if (edgeAlpha <= 0.001f)
                    {
                        pixels[y * BackdropTexSize + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Sim ground coords for Team 0 view (X right, -Z up)
                    float wx = dx * mPerPx;
                    float wz = -dy * mPerPx;

                    Color col = KomiksUi.RadarGlass;

                    // 3 N-S pathways (X ≈ -34, 0, +34) + 2 E-W alleys (Z ≈ ±26)
                    bool onPath = Mathf.Abs(wx) < 3.6f || Mathf.Abs(Mathf.Abs(wx) - 34f) < 3.4f || Mathf.Abs(Mathf.Abs(wz) - 26f) < 3.0f;
                    if (onPath)
                        col = Color.Lerp(col, Color.white, 0.08f);

                    // Walkable Estero creek + 3 bridges at X = -34, 0, +34
                    float canalZ = MapLayoutData.CanalZ(wx);
                    if (Mathf.Abs(wz - canalZ) < 2.8f)
                    {
                        bool onBridge = Mathf.Abs(wx) < 4.2f || Mathf.Abs(Mathf.Abs(wx) - 34f) < 4.2f;
                        col = onBridge
                            ? Color.Lerp(col, KomiksUi.CreamShade, 0.45f)
                            : Color.Lerp(col, new Color(0.20f, 0.74f, 0.78f, 0.85f), 0.36f);
                    }

                    // Building parcels if layout is loaded
                    if (_layout != null && _layout.Obstacles != null)
                    {
                        int parcels = _layout.ParcelCount;
                        var pt = new SVector2(wx, wz);
                        for (int p = 0; p < parcels; p++)
                        {
                            if (_layout.ParcelRect(p).Contains(pt))
                            {
                                col = Color.Lerp(col, KomiksUi.SandBorder, 0.24f);
                                break;
                            }
                        }
                    }

                    // Crisp outer compass rim
                    float rimBlend = Mathf.Clamp01(1f - Mathf.Abs(dist - (rPx - 1.2f)) * 0.85f);
                    col = Color.Lerp(col, new Color(1f, 1f, 1f, 0.46f), rimBlend * 0.75f);
                    col.a *= edgeAlpha;
                    pixels[y * BackdropTexSize + x] = col;
                }
            }

            _backdropTex.SetPixels32(pixels);
            _backdropTex.Apply(false, false);

            if (_backdropSprite == null)
            {
                _backdropSprite = Sprite.Create(
                    _backdropTex,
                    new Rect(0f, 0f, BackdropTexSize, BackdropTexSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _backdropSprite.name = "BarangayMinimapSprite";
            }
            if (_backdropImage != null)
                _backdropImage.sprite = _backdropSprite;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_backdropImage != null)
                _backdropImage.sprite = null;

            if (_backdropSprite != null)
            {
                KalyeahResources.Kill(_backdropSprite);
                _backdropSprite = null;
            }

            if (_backdropTex != null)
            {
                KalyeahResources.Kill(_backdropTex);
                _backdropTex = null;
            }
        }
    }
}
