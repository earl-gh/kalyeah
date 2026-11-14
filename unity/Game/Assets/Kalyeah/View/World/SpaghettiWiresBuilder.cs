using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Shared lane centerline/width helpers (ported 1-to-1 from scripts/sim/map_layout.gd) and zero-collider static mesh batching
    /// used by SpaghettiWiresBuilder and BarangayCurbsAndProps.
    /// </summary>
    internal static class BarangayLanes
    {
        public static float SmoothBand(float edge0, float edge1, float val)
        {
            float t = Mathf.Clamp01((val - edge0) / Mathf.Max(edge1 - edge0, 0.0001f));
            return t * t * (3f - 2f * t);
        }

        public static float LaneCenterX(float z)
        {
            float taper = 1f - SmoothBand(50f, 62f, Mathf.Abs(z));
            return 4.2f * Mathf.Sin(z * 0.048f) * taper;
        }

        public static float LaneEastX(float z)
        {
            float taper = 1f - SmoothBand(56f, 74f, Mathf.Abs(z));
            return 34.5f + (3.8f * Mathf.Sin(z * 0.052f + 0.55f) - 0.055f * Mathf.Abs(z)) * taper;
        }

        public static float LaneWestX(float z) => -LaneEastX(-z);

        public static float EskinitaSouthZ(float x) => 28f + 3f * Mathf.Sin(x * 0.058f);

        public static float EskinitaNorthZ(float x) => -EskinitaSouthZ(-x);

        public static float LaneCenterHalfWidth(float z)
        {
            float az = Mathf.Abs(z);
            float dzEsk = (az - 28f) / 8.5f;
            float dzFord = z / 8.5f;
            float dzPlaza = (az - 62f) / 10f;
            return 4.45f + 2.15f * Mathf.Exp(-dzEsk * dzEsk) + 1.85f * Mathf.Exp(-dzFord * dzFord) + 2.40f * Mathf.Exp(-dzPlaza * dzPlaza);
        }

        public static float LaneSideHalfWidth(float z)
        {
            float az = Mathf.Abs(z);
            float dzEsk = (az - 28f) / 9f;
            float dzFord = z / 8.5f;
            return 4.25f + 1.95f * Mathf.Exp(-dzEsk * dzEsk) + 1.65f * Mathf.Exp(-dzFord * dzFord);
        }

        public static float EskinitaHalfWidth(float x)
        {
            float ax = Mathf.Abs(x);
            float dxMid = ax / 9f;
            float dxSide = (ax - 34.5f) / 8.5f;
            return 3.55f + 1.55f * Mathf.Exp(-dxMid * dxMid) + 1.45f * Mathf.Exp(-dxSide * dxSide);
        }

        public static bool InAnyNavigationLane(SVector2 p, float margin)
        {
            if (Mathf.Abs(p.X - LaneCenterX(p.Y)) < LaneCenterHalfWidth(p.Y) + margin) return true;
            if (Mathf.Abs(p.X - LaneWestX(p.Y)) < LaneSideHalfWidth(p.Y) + margin) return true;
            if (Mathf.Abs(p.X - LaneEastX(p.Y)) < LaneSideHalfWidth(p.Y) + margin) return true;
            float eskHw = EskinitaHalfWidth(p.X) + margin;
            if (Mathf.Abs(p.Y - EskinitaSouthZ(p.X)) < eskHw) return true;
            if (Mathf.Abs(p.Y - EskinitaNorthZ(p.X)) < eskHw) return true;
            return false;
        }
    }

    [ExecuteAlways]
    internal sealed class ProceduralAssetCleaner : MonoBehaviour, IDisposable
    {
        readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>(24);
        bool _disposed;

        public T Track<T>(T asset) where T : UnityEngine.Object
        {
            if (asset != null) _assets.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _assets.Count - 1; i >= 0; i--)
            {
                if (_assets[i] != null)
                    KalyeahResources.Kill(_assets[i]);
            }
            _assets.Clear();
        }

        void OnDestroy() => Dispose();

        public static void DisposeRoot(GameObject root)
        {
            if (root == null) return;
            ProceduralAssetCleaner cleaner = root.GetComponent<ProceduralAssetCleaner>();
            cleaner?.Dispose();
            KalyeahResources.Kill(root);
        }
    }

    internal sealed class WorldMeshBatch
    {
        readonly List<Vector3> _verts = new List<Vector3>(2048);
        readonly List<Vector3> _normals = new List<Vector3>(2048);
        readonly List<Vector2> _uvs = new List<Vector2>(2048);
        readonly List<int> _tris = new List<int>(4096);
        readonly int _paletteCount;

        public WorldMeshBatch(int paletteCount = 1) { _paletteCount = Mathf.Max(paletteCount, 1); }

        public int VertexCount => _verts.Count;

        Vector2 PaletteUv(int colorIndex) => new Vector2((Mathf.Clamp(colorIndex, 0, _paletteCount - 1) + 0.5f) / _paletteCount, 0.5f);

        public void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 outwardNormal, int colorIndex = 0)
        {
            Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
            if (Vector3.Dot(cross, outwardNormal) < 0f)
            {
                Vector3 tmp = v1;
                v1 = v3;
                v3 = tmp;
                cross = -cross;
            }
            Vector3 n = cross.sqrMagnitude > 1e-10f ? cross.normalized : outwardNormal.normalized;
            Vector2 uv = PaletteUv(colorIndex);
            int baseIdx = _verts.Count;
            _verts.Add(v0); _verts.Add(v1); _verts.Add(v2); _verts.Add(v3);
            _normals.Add(n); _normals.Add(n); _normals.Add(n); _normals.Add(n);
            _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);
            _tris.Add(baseIdx); _tris.Add(baseIdx + 1); _tris.Add(baseIdx + 2);
            _tris.Add(baseIdx); _tris.Add(baseIdx + 2); _tris.Add(baseIdx + 3);
        }

        public void AddDoubleQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, int colorIndex = 0)
        {
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
            AddQuad(v0, v1, v2, v3, n, colorIndex);
            AddQuad(v0, v3, v2, v1, -n, colorIndex);
        }

        public void AddDoubleTri(Vector3 v0, Vector3 v1, Vector3 v2, int colorIndex = 0)
        {
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.forward;
            Vector2 uv = PaletteUv(colorIndex);
            int b = _verts.Count;
            _verts.Add(v0); _verts.Add(v1); _verts.Add(v2);
            _normals.Add(n); _normals.Add(n); _normals.Add(n);
            _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);
            _tris.Add(b); _tris.Add(b + 1); _tris.Add(b + 2);

            int b2 = _verts.Count;
            _verts.Add(v0); _verts.Add(v2); _verts.Add(v1);
            _normals.Add(-n); _normals.Add(-n); _normals.Add(-n);
            _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);
            _tris.Add(b2); _tris.Add(b2 + 1); _tris.Add(b2 + 2);
        }

        public void AddOrientedBox(Vector3 center, Vector3 size, Quaternion rot, int topColor, int sideColor, bool includeBottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 rx = rot * Vector3.right;
            Vector3 ry = rot * Vector3.up;
            Vector3 rz = rot * Vector3.forward;

            Vector3 p000 = center - rx * h.x - ry * h.y - rz * h.z;
            Vector3 p100 = center + rx * h.x - ry * h.y - rz * h.z;
            Vector3 p010 = center - rx * h.x + ry * h.y - rz * h.z;
            Vector3 p110 = center + rx * h.x + ry * h.y - rz * h.z;
            Vector3 p001 = center - rx * h.x - ry * h.y + rz * h.z;
            Vector3 p101 = center + rx * h.x - ry * h.y + rz * h.z;
            Vector3 p011 = center - rx * h.x + ry * h.y + rz * h.z;
            Vector3 p111 = center + rx * h.x + ry * h.y + rz * h.z;

            AddQuad(p010, p011, p111, p110, ry, topColor);
            if (includeBottom) AddQuad(p001, p000, p100, p101, -ry, sideColor);
            AddQuad(p001, p101, p111, p011, rz, sideColor);
            AddQuad(p100, p000, p010, p110, -rz, sideColor);
            AddQuad(p101, p100, p110, p111, rx, sideColor);
            AddQuad(p000, p001, p011, p010, -rx, sideColor);
        }

        public void AddVerticalCylinder(Vector3 baseCenter, float bottomRadius, float topRadius, float height, int sides, int topColor, int sideColor, bool capTop = true)
        {
            float step = Mathf.PI * 2f / sides;
            Vector3 topCenter = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * bottomRadius;
                Vector3 b1 = baseCenter + d1 * bottomRadius;
                Vector3 t0 = topCenter + d0 * topRadius;
                Vector3 t1 = topCenter + d1 * topRadius;
                AddQuad(b0, t0, t1, b1, (d0 + d1) * 0.5f, sideColor);
                if (capTop && topRadius > 0.001f)
                {
                    Vector2 uv = PaletteUv(topColor);
                    int idx = _verts.Count;
                    Vector3 vA = topCenter;
                    Vector3 vB = t0;
                    Vector3 vC = t1;
                    if (Vector3.Dot(Vector3.Cross(vB - vA, vC - vA), Vector3.up) < 0f) { Vector3 tmp = vB; vB = vC; vC = tmp; }
                    _verts.Add(vA); _verts.Add(vB); _verts.Add(vC);
                    _normals.Add(Vector3.up); _normals.Add(Vector3.up); _normals.Add(Vector3.up);
                    _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);
                    _tris.Add(idx); _tris.Add(idx + 1); _tris.Add(idx + 2);
                }
            }
        }

        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material CreatePaletteMaterial(string name, Color[] palette, bool lit, float smoothness = 0.12f)
            => CreatePaletteMaterial(name, palette, lit, smoothness, null);

        public static Material CreatePaletteMaterial(
            string name,
            Color[] palette,
            bool lit,
            float smoothness,
            ProceduralAssetCleaner cleaner)
        {
            Material template = Resources.Load<Material>("KalyeahMaterials/" + (lit ? "Ground" : "Unlit"));
            Shader fallback = template == null
                ? Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")
                : null;
            if (template == null && fallback == null) return null;

            var tex = new Texture2D(palette.Length, 1, TextureFormat.RGBA32, false)
            {
                name = name + "_Palette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(palette);
            tex.Apply(false, false);

            Material mat = template != null
                ? new Material(template) { name = name }
                : new Material(fallback) { name = name };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", tex);
            if (lit && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (cleaner != null)
            {
                cleaner.Track(tex);
                cleaner.Track(mat);
            }
            return mat;
        }
    }

    /// <summary>
    /// Builds the overhead barangay wire network (y >= 4.5 m, ShadowCastingMode.Off), individual occludable curbside utility poles,
    /// and Pinoy canopy fiesta banderitas + sampayan laundry strips, tracking all procedural Mesh/Material/Texture2D assets for zero leaks.
    /// </summary>
    public static class SpaghettiWiresBuilder
    {
        const float MinWireHeight = 4.52f;

        static readonly Color WireCharcoal = new Color(0.13f, 0.11f, 0.13f, 1f);
        static readonly Color[] CanopyPalette =
        {
            new Color(0.44f, 0.42f, 0.39f), // 0: concrete utility pole shaft
            new Color(0.32f, 0.25f, 0.19f), // 1: creosote wood crossarm
            new Color(0.84f, 0.82f, 0.77f), // 2: porcelain spool insulator
            new Color(0.92f, 0.24f, 0.20f), // 3: fiesta crimson pennant / shirt
            new Color(0.96f, 0.76f, 0.18f), // 4: fiesta sun-yellow pennant / jersey
            new Color(0.18f, 0.58f, 0.84f), // 5: fiesta sky-blue pennant / shorts
            new Color(0.24f, 0.68f, 0.38f), // 6: fiesta emerald pennant / towel
            new Color(0.94f, 0.48f, 0.16f), // 7: fiesta mango-orange pennant
            new Color(0.92f, 0.90f, 0.84f), // 8: white cotton sando / trim
            new Color(0.56f, 0.46f, 0.32f), // 9: twine / sampayan cord
        };

        struct PoleAnchor
        {
            public Vector3 BasePos;
            public Quaternion Rot;
            public Vector3 UpperLeft, UpperMid, UpperRight;
            public Vector3 LowerLeft, LowerRight, TelcoClamp, BannerTie;
        }

        public static GameObject Build(Transform parent, MapLayoutData layout)
            => Build(parent, layout, null);

        public static GameObject Build(Transform parent, MapLayoutData layout, List<GameObject> outPoles)
        {
            var root = new GameObject("SpaghettiWiresAndCanopy");
            if (parent != null) root.transform.SetParent(parent, false);
            var cleaner = root.AddComponent<ProceduralAssetCleaner>();
            if (layout == null) return root;

            var wireBatch = new WorldMeshBatch(1);
            var canopyBatch = new WorldMeshBatch(CanopyPalette.Length);
            Material canopyMat = WorldMeshBatch.CreatePaletteMaterial("CanopyPalette", CanopyPalette, true, 0.10f, cleaner);

            var polesParent = new GameObject("UtilityPoles").transform;
            polesParent.SetParent(root.transform, false);

            List<PoleAnchor> poles = BuildCurbPoles(layout, polesParent, canopyMat, cleaner, outPoles);
            AppendTrunkWireSpans(poles, wireBatch);
            AppendBuildingServiceDrops(layout, poles, wireBatch, canopyBatch);
            AppendFiestaBanderitas(poles, canopyBatch);
            AppendOverheadSampayan(layout, poles, polesParent, canopyMat, canopyBatch, cleaner, outPoles);

            if (polesParent.childCount == 0)
                KalyeahResources.Kill(polesParent.gameObject);

            if (wireBatch.VertexCount > 0)
            {
                Material wireMat = cleaner.Track(MakeWireMaterial());
                Mesh wireMesh = cleaner.Track(wireBatch.ToMesh("SpaghettiWiresMesh"));
                AttachStaticMesh(root.transform, "SpaghettiWires", wireMesh, wireMat);
            }
            if (canopyBatch.VertexCount > 0)
            {
                Mesh canopyMesh = cleaner.Track(canopyBatch.ToMesh("CanopyMesh"));
                AttachStaticMesh(root.transform, "BanderitasAndSampayan", canopyMesh, canopyMat);
            }
            return root;
        }

        public static void Dispose(GameObject root) => ProceduralAssetCleaner.DisposeRoot(root);

        static List<PoleAnchor> BuildCurbPoles(
            MapLayoutData layout,
            Transform polesParent,
            Material poleMat,
            ProceduralAssetCleaner cleaner,
            List<GameObject> outPoles)
        {
            var specs = new List<(SVector2 ground, float godotYaw, float height)>(40);
            if (layout.ArenaRadius > 0f)
            {
                float[] feederZs = { -52f, -31f, -8.8f, 8.8f, 31f, 52f };
                float[] centerZs = { -47.5f, -23.5f, -8.2f, 8.2f, 23.5f, 47.5f };

                foreach (float z in feederZs) specs.Add(CurbPoleSpec(layout, -1, z, -1f, 9.2f)); // 0..5 West feeder
                foreach (float z in centerZs) specs.Add(CurbPoleSpec(layout, 0, z, z > 0f ? -1f : 1f, 9.2f)); // 6..11 Center ave
                foreach (float z in feederZs) specs.Add(CurbPoleSpec(layout, 1, z, 1f, 9.2f)); // 12..17 East feeder

                // 18..23 opposite-curb partner poles for two-ended fiesta banderitas
                specs.Add(CurbPoleSpec(layout, -1, 31f, 1f, 9.0f));
                specs.Add(CurbPoleSpec(layout, 0, 47.5f, 1f, 9.0f));
                specs.Add(CurbPoleSpec(layout, 1, 31f, -1f, 9.0f));
                specs.Add(CurbPoleSpec(layout, -1, -31f, 1f, 9.0f));
                specs.Add(CurbPoleSpec(layout, 0, -47.5f, -1f, 9.0f));
                specs.Add(CurbPoleSpec(layout, 1, -31f, -1f, 9.0f));

                // 24..35 sparse outer perimeter utility ring (12 poles along r = arenaRadius - 0.55)
                float ringR = layout.ArenaRadius - 0.55f;
                for (int i = 0; i < 12; i++)
                {
                    float ang = Mathf.PI * 2f * (i + 0.35f) / 12f;
                    specs.Add((new SVector2(Mathf.Cos(ang) * ringR, Mathf.Sin(ang) * ringR), ang, 8.4f));
                }
            }
            else
            {
                float[] rectZs = { -18f, -6f, 6f, 18f };
                foreach (float z in rectZs) specs.Add((new SVector2(-10.8f, z), 0f, 8.8f));
                foreach (float z in rectZs) specs.Add((new SVector2(10.8f, z), Mathf.PI, 8.8f));
            }

            var poles = new List<PoleAnchor>(specs.Count);
            for (int i = 0; i < specs.Count; i++)
            {
                var (ground, godotYaw, h) = specs[i];
                Vector3 basePos = KalyeahResources.World(ground, 0f);
                float unityYaw = GodotSpace.ModelYawDegrees(godotYaw);
                float leanX = 1.3f * Mathf.Sin(i * 2.1f + 0.4f);
                float leanZ = 1.3f * Mathf.Cos(i * 1.7f - 0.3f);
                Quaternion rot = Quaternion.Euler(leanX, unityYaw, leanZ);

                var poleBatch = new WorldMeshBatch(CanopyPalette.Length);
                poleBatch.AddVerticalCylinder(Vector3.zero, 0.16f, 0.105f, h, 6, 0, 0);

                float yUpper = h - 0.38f;
                float yLower = h - 1.55f;
                float yTelco = Mathf.Max(h - 2.35f, 5.65f);
                float yBanner = Mathf.Max(h - 2.95f, 5.45f);

                Vector3 uLocal = rot * new Vector3(0f, yUpper, 0.12f);
                Vector3 lLocal = rot * new Vector3(0f, yLower, 0.12f);
                poleBatch.AddOrientedBox(uLocal, new Vector3(1.52f, 0.09f, 0.09f), rot, 1, 1, true);
                poleBatch.AddOrientedBox(lLocal, new Vector3(1.36f, 0.08f, 0.08f), rot, 1, 1, true);

                Vector3 uLLocal = uLocal + rot * new Vector3(-0.68f, 0.09f, 0f);
                Vector3 uMLocal = uLocal + rot * new Vector3(0.18f, 0.09f, 0f);
                Vector3 uRLocal = uLocal + rot * new Vector3(0.68f, 0.09f, 0f);
                Vector3 lLLocal = lLocal + rot * new Vector3(-0.60f, 0.08f, 0f);
                Vector3 lRLocal = lLocal + rot * new Vector3(0.60f, 0.08f, 0f);

                poleBatch.AddVerticalCylinder(uLLocal - Vector3.up * 0.04f, 0.04f, 0.035f, 0.09f, 5, 2, 2);
                poleBatch.AddVerticalCylinder(uMLocal - Vector3.up * 0.04f, 0.04f, 0.035f, 0.09f, 5, 2, 2);
                poleBatch.AddVerticalCylinder(uRLocal - Vector3.up * 0.04f, 0.04f, 0.035f, 0.09f, 5, 2, 2);

                Mesh poleMesh = cleaner.Track(poleBatch.ToMesh("UtilityPoleMesh_" + i));
                GameObject poleGo = AttachStaticMesh(polesParent, "UtilityPole_" + i, poleMesh, poleMat);
                poleGo.transform.position = basePos;
                outPoles?.Add(poleGo);

                poles.Add(new PoleAnchor
                {
                    BasePos = basePos,
                    Rot = rot,
                    UpperLeft = basePos + uLLocal,
                    UpperMid = basePos + uMLocal,
                    UpperRight = basePos + uRLocal,
                    LowerLeft = basePos + lLLocal,
                    LowerRight = basePos + lRLocal,
                    TelcoClamp = basePos + rot * new Vector3(0f, yTelco, -0.13f),
                    BannerTie = basePos + rot * new Vector3(0f, yBanner, 0f),
                });
            }
            return poles;
        }

        static (SVector2 ground, float godotYaw, float height) CurbPoleSpec(MapLayoutData layout, int laneId, float z, float sideSign, float height)
        {
            float xC, hw, dx;
            if (laneId == 0)
            {
                xC = BarangayLanes.LaneCenterX(z);
                hw = BarangayLanes.LaneCenterHalfWidth(z);
                dx = BarangayLanes.LaneCenterX(z + 1f) - BarangayLanes.LaneCenterX(z - 1f);
            }
            else if (laneId < 0)
            {
                xC = BarangayLanes.LaneWestX(z);
                hw = BarangayLanes.LaneSideHalfWidth(z);
                dx = BarangayLanes.LaneWestX(z + 1f) - BarangayLanes.LaneWestX(z - 1f);
            }
            else
            {
                xC = BarangayLanes.LaneEastX(z);
                hw = BarangayLanes.LaneSideHalfWidth(z);
                dx = BarangayLanes.LaneEastX(z + 1f) - BarangayLanes.LaneEastX(z - 1f);
            }

            SVector2 cand = new SVector2(xC + sideSign * (hw + 0.72f), z);
            for (int i = 0; i < layout.ParcelCount; i++)
            {
                GroundRect obs = layout.ParcelRect(i);
                var grown = new GroundRect(obs.X - 0.75f, obs.Y - 0.75f, obs.W + 1.5f, obs.H + 1.5f);
                if (grown.Contains(cand)) cand.X = xC + sideSign * (hw + 0.42f);
            }
            return (cand, Mathf.Atan2(dx, 2f), height);
        }

        static void AppendTrunkWireSpans(List<PoleAnchor> poles, WorldMeshBatch wires)
        {
            if (poles.Count < 8) return;

            // Sparse lane-margin chains keeping the central 0°-25° throw sky clean
            int[,] spans = poles.Count >= 36
                ? new[,]
                {
                    { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 }, { 4, 5 },
                    { 6, 7 }, { 7, 8 }, { 8, 9 }, { 9, 10 }, { 10, 11 },
                    { 12, 13 }, { 13, 14 }, { 14, 15 }, { 15, 16 }, { 16, 17 },
                    { 24, 25 }, { 25, 26 }, { 26, 27 }, { 27, 28 }, { 28, 29 }, { 29, 30 },
                    { 30, 31 }, { 31, 32 }, { 32, 33 }, { 33, 34 }, { 34, 35 }, { 35, 24 },
                }
                : new[,] { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 4, 5 }, { 5, 6 }, { 6, 7 } };

            int spanCount = spans.GetLength(0);
            for (int s = 0; s < spanCount; s++)
            {
                PoleAnchor a = poles[spans[s, 0]];
                PoleAnchor b = poles[spans[s, 1]];
                float dist = Vector2.Distance(new Vector2(a.BasePos.x, a.BasePos.z), new Vector2(b.BasePos.x, b.BasePos.z));
                if (dist < 1.5f || dist > 50f) continue;

                bool flip = Vector3.Dot(a.UpperRight - a.UpperLeft, b.UpperRight - b.UpperLeft) < 0f;
                float sagUpper = Mathf.Clamp(dist * 0.015f, 0.16f, 0.68f);
                float sagLower = Mathf.Clamp(dist * 0.019f + 0.06f, 0.20f, 0.78f);

                AppendCatenaryRibbon(wires, a.UpperLeft, flip ? b.UpperRight : b.UpperLeft, sagUpper, 0.022f, 6, 0);
                AppendCatenaryRibbon(wires, a.UpperMid, b.UpperMid, sagUpper + 0.04f, 0.020f, 6, 0);
                AppendCatenaryRibbon(wires, a.UpperRight, flip ? b.UpperLeft : b.UpperRight, sagUpper, 0.022f, 6, 0);
                AppendCatenaryRibbon(wires, a.LowerLeft, flip ? b.LowerRight : b.LowerLeft, sagLower, 0.019f, 5, 0);
                AppendCatenaryRibbon(wires, a.TelcoClamp, b.TelcoClamp, sagLower + 0.08f, 0.024f, 5, 0);
            }

            // Under-clamp telecom slack coils ("ikot ng kable") at y >= 5.2 m
            for (int i = 0; i < poles.Count; i += 2)
            {
                AppendWireCoil(wires, poles[i].TelcoClamp - new Vector3(0f, 0.22f, 0f), 0.18f, 0.21f, poles[i].Rot);
            }
        }

        static void AppendBuildingServiceDrops(MapLayoutData layout, List<PoleAnchor> poles, WorldMeshBatch wires, WorldMeshBatch canopy)
        {
            for (int i = 0; i < layout.ParcelCount; i++)
            {
                string prop = layout.ParcelProp(i);
                if (prop == "cover_wall" || prop == "bench" || prop == "waiting_shed" || prop == "toda_shed"
                    || prop == "jeepney" || prop == "owner_jeep" || prop == "passenger_jeepney" || prop == "tricycle")
                    continue;

                GroundRect rect = layout.ParcelRect(i);
                float yaw = layout.HasOrientedParcels ? layout.ParcelYawRad(i) : 0f;
                SVector2 ux = new SVector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
                SVector2 center = rect.Center;

                int bestPole = -1;
                float bestDist = 24f;
                Vector3 bestWallPt = Vector3.zero;
                for (int p = 0; p < poles.Count; p++)
                {
                    SVector2 pGround = GodotSpace.Ground(new System.Numerics.Vector3(poles[p].BasePos.x, 0f, poles[p].BasePos.z));
                    SVector2 rel = pGround - center;
                    float d = rel.Length();
                    if (d < 3.5f || d >= bestDist) continue;
                    float sideSign = SVector2.Dot(rel, ux) >= 0f ? 1f : -1f;
                    SVector2 wallGround = center + ux * (sideSign * Mathf.Max(rect.W * 0.5f - 0.25f, 0.8f));
                    bestDist = d;
                    bestPole = p;
                    bestWallPt = KalyeahResources.World(wallGround, 4.95f + (i % 3) * 0.28f);
                }

                if (bestPole >= 0)
                {
                    canopy.AddVerticalCylinder(bestWallPt - Vector3.up * 0.05f, 0.045f, 0.045f, 0.10f, 5, 2, 2);
                    AppendCatenaryRibbon(wires, poles[bestPole].LowerRight, bestWallPt, Mathf.Clamp(bestDist * 0.014f, 0.14f, 0.36f), 0.018f, 5, 0);
                }
            }
        }

        static void AppendFiestaBanderitas(List<PoleAnchor> poles, WorldMeshBatch canopy)
        {
            if (poles.Count < 24) return;
            int[,] pairs = { { 4, 18 }, { 11, 19 }, { 20, 16 }, { 1, 21 }, { 22, 6 }, { 23, 13 } };
            int[] flagColors = { 3, 4, 5, 6, 7, 8 };

            for (int p = 0; p < pairs.GetLength(0); p++)
            {
                Vector3 a = poles[pairs[p, 0]].BannerTie;
                Vector3 b = poles[pairs[p, 1]].BannerTie;
                float span = Vector3.Distance(a, b);
                if (span < 4f || span > 23f) continue;

                float sag = Mathf.Clamp(span * 0.032f, 0.18f, 0.52f);
                AppendCatenaryRibbon(canopy, a, b, sag, 0.014f, 8, 9);

                int flags = Mathf.Clamp(Mathf.RoundToInt(span * 2.1f), 12, 34);
                Vector3 along = (b - a).normalized;
                for (int f = 0; f < flags; f++)
                {
                    float t0 = (f + 0.18f) / flags;
                    float t1 = (f + 0.82f) / flags;
                    float tm = (t0 + t1) * 0.5f;
                    Vector3 p0 = SampleCatenary(a, b, sag, t0);
                    Vector3 p1 = SampleCatenary(a, b, sag, t1);
                    Vector3 pm = SampleCatenary(a, b, sag, tm);
                    float sway = 0.05f * Mathf.Sin(f * 1.9f + p * 1.3f);
                    Vector3 side = new Vector3(-along.z, 0f, along.x) * sway;
                    Vector3 tip = pm + new Vector3(0f, -0.34f, 0f) + side;
                    canopy.AddDoubleTri(p0, p1, tip, flagColors[(f + p) % flagColors.Length]);
                }
            }
        }

        static void AppendOverheadSampayan(
            MapLayoutData layout,
            List<PoleAnchor> poles,
            Transform polesParent,
            Material poleMat,
            WorldMeshBatch canopy,
            ProceduralAssetCleaner cleaner,
            List<GameObject> outPoles)
        {
            if (layout.ArenaRadius <= 0f || poles.Count < 18) return;

            // High eave-level laundry lines (y = 4.75..5.15 m, strictly above combat band) tucked along side-yard margins
            SVector2[] anchors =
            {
                new SVector2(-54.5f, 21.6f), new SVector2(-51.1f, 22.8f),
                new SVector2(54.5f, -21.6f), new SVector2(51.1f, -22.8f),
                new SVector2(53.8f, 25.0f), new SVector2(50.4f, 26.2f),
                new SVector2(-53.8f, -25.0f), new SVector2(-50.4f, -26.2f),
                new SVector2(-22.4f, 23.8f), new SVector2(-19.0f, 24.6f),
                new SVector2(22.4f, -23.8f), new SVector2(19.0f, -24.6f),
            };
            int[] clothColors = { 3, 4, 5, 6, 8, 7 };

            for (int s = 0; s < anchors.Length; s += 2)
            {
                if (BarangayLanes.InAnyNavigationLane(anchors[s], 0.8f) || BarangayLanes.InAnyNavigationLane(anchors[s + 1], 0.8f))
                    continue;

                Vector3 a = KalyeahResources.World(anchors[s], 4.92f);
                Vector3 b = KalyeahResources.World(anchors[s + 1], 4.88f);
                BuildSampayanPole(polesParent, poleMat, cleaner, outPoles, s, new Vector3(a.x, 0f, a.z), a.y + 0.08f);
                BuildSampayanPole(polesParent, poleMat, cleaner, outPoles, s + 1, new Vector3(b.x, 0f, b.z), b.y + 0.08f);
                AppendCatenaryRibbon(canopy, a, b, 0.14f, 0.015f, 5, 9);

                Vector3 chord = b - a;
                Vector3 dir = new Vector3(chord.x, 0f, chord.z).normalized;
                for (int g = 0; g < 4; g++)
                {
                    float t = (g + 0.65f) / 4.3f;
                    Vector3 top = SampleCatenary(a, b, 0.14f, t);
                    float w = (g % 2 == 0) ? 0.22f : 0.18f;
                    float drop = (g % 3 == 0) ? 0.56f : 0.44f;
                    Vector3 v0 = top - dir * w;
                    Vector3 v1 = top + dir * w;
                    Vector3 v2 = v1 - new Vector3(0f, drop, 0f);
                    Vector3 v3 = v0 - new Vector3(0f, drop, 0f);
                    canopy.AddDoubleQuad(v0, v1, v2, v3, clothColors[(s + g) % clothColors.Length]);
                }
            }
        }

        static void BuildSampayanPole(
            Transform polesParent,
            Material poleMat,
            ProceduralAssetCleaner cleaner,
            List<GameObject> outPoles,
            int index,
            Vector3 basePos,
            float height)
        {
            var batch = new WorldMeshBatch(CanopyPalette.Length);
            batch.AddVerticalCylinder(Vector3.zero, 0.045f, 0.038f, height, 5, 0, 0);
            Mesh mesh = cleaner.Track(batch.ToMesh("SampayanPoleMesh_" + index));
            GameObject go = AttachStaticMesh(polesParent, "SampayanPole_" + index, mesh, poleMat);
            go.transform.position = basePos;
            outPoles?.Add(go);
        }

        static Vector3 SampleCatenary(Vector3 a, Vector3 b, float sag, float t)
        {
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y = Mathf.Max(p.y - 4f * sag * t * (1f - t), MinWireHeight);
            return p;
        }

        static void AppendCatenaryRibbon(WorldMeshBatch batch, Vector3 a, Vector3 b, float sag, float halfWidth, int segments, int colorIdx)
        {
            Vector3 horiz = new Vector3(b.x - a.x, 0f, b.z - a.z);
            Vector3 side = horiz.sqrMagnitude > 0.0001f
                ? new Vector3(-horiz.z, 0f, horiz.x).normalized * halfWidth
                : new Vector3(halfWidth, 0f, 0f);
            Vector3 up = new Vector3(0f, halfWidth * 1.15f, 0f);

            for (int i = 0; i < segments; i++)
            {
                Vector3 p0 = SampleCatenary(a, b, sag, (float)i / segments);
                Vector3 p1 = SampleCatenary(a, b, sag, (float)(i + 1) / segments);
                batch.AddDoubleQuad(p0 - up, p0 + up, p1 + up, p1 - up, colorIdx);
                batch.AddDoubleQuad(p0 - side, p0 + side, p1 + side, p1 - side, colorIdx);
            }
        }

        static void AppendWireCoil(WorldMeshBatch batch, Vector3 center, float rx, float ry, Quaternion rot)
        {
            const int steps = 8;
            Vector3 thick = new Vector3(0f, 0.022f, 0f);
            Vector3 wide = rot * new Vector3(0.020f, 0f, 0f);
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.PI * 2f * i / steps;
                float a1 = Mathf.PI * 2f * (i + 1) / steps;
                Vector3 p0 = center + rot * new Vector3(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry, 0.03f * Mathf.Sin(a0 * 2f));
                Vector3 p1 = center + rot * new Vector3(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry, 0.03f * Mathf.Sin(a1 * 2f));
                p0.y = Mathf.Max(p0.y, MinWireHeight);
                p1.y = Mathf.Max(p1.y, MinWireHeight);
                batch.AddDoubleQuad(p0 - thick, p0 + thick, p1 + thick, p1 - thick, 0);
                batch.AddDoubleQuad(p0 - wide, p0 + wide, p1 + wide, p1 - wide, 0);
            }
        }

        static Material MakeWireMaterial()
        {
            Material baseUnlit = Resources.Load<Material>("KalyeahMaterials/Unlit");
            Shader fallback = baseUnlit == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (baseUnlit == null && fallback == null) return null;
            Material mat = baseUnlit != null
                ? new Material(baseUnlit) { name = "SpaghettiWireUnlit" }
                : new Material(fallback) { name = "SpaghettiWireUnlit" };
            mat.SetColor("_BaseColor", WireCharcoal);
            return mat;
        }

        static GameObject AttachStaticMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (material != null) mr.sharedMaterial = material;
            return go;
        }
    }
}
