using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Raised concrete street curbs + parcel foundation plinths merged into a single URP mesh, paired with deterministic micro-dressing
    /// loaded from KalyeahResources.Model("props", ...) (falling back to merged low-poly stand-ins when a prop .glb is not yet authored),
    /// strictly clear of all navigation lanes, Estero crossings, bases, and rescue zones.
    /// </summary>
    public static class BarangayCurbsAndProps
    {
        static readonly HashSet<string> BuiltinSlabProps = new HashSet<string>
        {
            "toda_shed", "palengke_stall", "tent", "bakery_oven", "kapilya", "waiting_shed", "cover_wall", "bench",
        };

        // Requested micro-dressing prop ids resolved via KalyeahResources.Model("props", id)
        static readonly string[] MicroPropIds =
        {
            "monobloc_chair",
            "blue_water_drum",
            "sari_icebox",
            "street_trash_bin",
            "planted_tire",
            "drainage_cover",
            "crates",
            "sacks",
            "bench",
            "chicken_cage",
            "dog",
            "cat",
        };

        static readonly Color[] CurbPalette =
        {
            new Color(0.41f, 0.39f, 0.35f), // 0: concrete sidewalk / curb top
            new Color(0.30f, 0.28f, 0.25f), // 1: concrete curb vertical riser
            new Color(0.76f, 0.62f, 0.24f), // 2: painted ochre crossing curb nose
            new Color(0.32f, 0.42f, 0.25f), // 3: mossy planter rim / tire foliage
            new Color(0.24f, 0.18f, 0.13f), // 4: dark soil / rubber tire
            new Color(0.84f, 0.82f, 0.77f), // 5: white plastic monobloc / cooler lid
            new Color(0.18f, 0.44f, 0.74f), // 6: blue plastic water drum
            new Color(0.76f, 0.25f, 0.21f), // 7: red sari-sari icebox body
            new Color(0.24f, 0.48f, 0.30f), // 8: barangay green trash bin
            new Color(0.48f, 0.47f, 0.44f), // 9: hollow CHB cinderblock grey
        };

        public static GameObject Build(Transform parent, MapLayoutData layout)
            => Build(parent, layout, null);

        public static GameObject Build(Transform parent, MapLayoutData layout, List<GameObject> outPoles)
        {
            var root = new GameObject("BarangayCurbsAndProps");
            if (parent != null) root.transform.SetParent(parent, false);
            var cleaner = root.AddComponent<ProceduralAssetCleaner>();
            if (layout == null) return root;

            var batch = new WorldMeshBatch(CurbPalette.Length);
            var propsRoot = new GameObject("MicroProps").transform;
            propsRoot.SetParent(root.transform, false);
            var collidersRoot = new GameObject("CurbAndPlinthColliders").transform;
            collidersRoot.SetParent(root.transform, false);

            AppendParcelPlinths(layout, batch, collidersRoot);
            AppendLaneCurbs(layout, batch, collidersRoot);
            AppendTreePlanters(layout, batch);
            PlaceDeterministicMicroProps(layout, propsRoot, batch, outPoles);

            if (propsRoot.childCount == 0) KalyeahResources.Kill(propsRoot.gameObject);
            if (collidersRoot.childCount == 0) KalyeahResources.Kill(collidersRoot.gameObject);

            if (batch.VertexCount > 0)
            {
                Material mat = WorldMeshBatch.CreatePaletteMaterial("CurbsAndPropsPalette", CurbPalette, true, 0.08f, cleaner);
                var meshGo = new GameObject("CurbsAndStandInProps");
                meshGo.transform.SetParent(root.transform, false);
                Mesh mesh = cleaner.Track(batch.ToMesh("BarangayCurbsMesh"));
                meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                if (mat != null) mr.sharedMaterial = mat;
            }
            return root;
        }

        public static void Dispose(GameObject root) => ProceduralAssetCleaner.DisposeRoot(root);

        static void AppendParcelPlinths(MapLayoutData layout, WorldMeshBatch batch, Transform collidersRoot)
        {
            for (int i = 0; i < layout.ParcelCount; i++)
            {
                string prop = layout.ParcelProp(i);
                if (BuiltinSlabProps.Contains(prop)) continue;

                GroundRect r = layout.ParcelRect(i);
                float godotYaw = layout.HasOrientedParcels ? layout.ParcelYawRad(i) : 0f;
                Quaternion rot = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYaw), 0f);
                Vector3 center = KalyeahResources.World(r.Center, 0.048f);
                Vector3 size = new Vector3(r.W + 0.68f, 0.096f, r.H + 0.68f);
                batch.AddOrientedBox(center, size, rot, 0, 1);
                AddBoxCollider(collidersRoot, "PlinthCol_" + i, center, rot, size);
            }
        }

        static void AppendLaneCurbs(MapLayoutData layout, WorldMeshBatch batch, Transform collidersRoot)
        {
            if (layout.ArenaRadius > 0f)
            {
                const float zStart = -66f;
                const float zEnd = 66f;
                const float step = 3.0f;

                for (int laneId = -1; laneId <= 1; laneId++)
                {
                    for (float sideSign = -1f; sideSign <= 1f; sideSign += 2f)
                    {
                        int segIndex = 0;
                        for (float z = zStart; z < zEnd; z += step, segIndex++)
                        {
                            float zm = z + step * 0.5f;
                            float x0 = SampleLaneEdgeX(laneId, z, sideSign);
                            float x1 = SampleLaneEdgeX(laneId, z + step, sideSign);
                            SVector2 mid = new SVector2((x0 + x1) * 0.5f, zm);

                            if (!IsValidCurbSegment(layout, mid)) continue;

                            Vector3 p0 = KalyeahResources.World(new SVector2(x0, z), 0.055f);
                            Vector3 p1 = KalyeahResources.World(new SVector2(x1, z + step), 0.055f);
                            Vector3 chord = p1 - p0;
                            float len = chord.magnitude;
                            if (len < 0.2f) continue;

                            Quaternion rot = Quaternion.LookRotation(chord / len, Vector3.up);
                            bool nearCrossing = Mathf.Abs(Mathf.Abs(zm) - 28f) < 6.5f || Mathf.Abs(zm - MapLayoutData.CanalZ(mid.X)) < 9.5f;
                            int topCol = (nearCrossing && (segIndex % 2 == 0)) ? 2 : 0;
                            Vector3 curbCenter = (p0 + p1) * 0.5f;
                            Vector3 curbSize = new Vector3(0.28f, 0.11f, len * 0.96f);
                            batch.AddOrientedBox(curbCenter, curbSize, rot, topCol, 1);
                            AddBoxCollider(collidersRoot, $"CurbCol_{laneId}_{sideSign}_{segIndex}", curbCenter, rot, curbSize);
                        }
                    }
                }
            }
            else
            {
                for (float sideSign = -1f; sideSign <= 1f; sideSign += 2f)
                {
                    Vector3 sideCenter = KalyeahResources.World(new SVector2(sideSign * 10.6f, 0f), 0.055f);
                    AddBoxCollider(collidersRoot, sideSign < 0f ? "RectCurbCol_W" : "RectCurbCol_E", sideCenter, Quaternion.identity, new Vector3(0.28f, 0.11f, 40f));
                    for (float z = -20f; z < 20f; z += 4f)
                    {
                        Vector3 center = KalyeahResources.World(new SVector2(sideSign * 10.6f, z + 2f), 0.055f);
                        batch.AddOrientedBox(center, new Vector3(0.28f, 0.11f, 3.85f), Quaternion.identity, 0, 1);
                    }
                }
            }
        }

        static void AddBoxCollider(Transform parent, string name, Vector3 worldCenter, Quaternion rot, Vector3 size)
        {
            if (parent == null) return;
            var go = new GameObject(name);
            go.layer = 0;
            go.transform.SetParent(parent, false);
            go.transform.position = worldCenter;
            go.transform.rotation = rot;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = false;
        }

        static float SampleLaneEdgeX(int laneId, float z, float sideSign)
        {
            if (laneId == 0) return BarangayLanes.LaneCenterX(z) + sideSign * (BarangayLanes.LaneCenterHalfWidth(z) + 0.22f);
            if (laneId < 0) return BarangayLanes.LaneWestX(z) + sideSign * (BarangayLanes.LaneSideHalfWidth(z) + 0.22f);
            return BarangayLanes.LaneEastX(z) + sideSign * (BarangayLanes.LaneSideHalfWidth(z) + 0.22f);
        }

        static bool IsValidCurbSegment(MapLayoutData layout, SVector2 p)
        {
            if (p.Length() > layout.ArenaRadius - 3.0f) return false;
            if (Mathf.Abs(p.Y - MapLayoutData.CanalZ(p.X)) < 6.8f) return false;
            float eskHw = BarangayLanes.EskinitaHalfWidth(p.X) + 0.9f;
            if (Mathf.Abs(p.Y - BarangayLanes.EskinitaSouthZ(p.X)) < eskHw) return false;
            if (Mathf.Abs(p.Y - BarangayLanes.EskinitaNorthZ(p.X)) < eskHw) return false;
            if (SVector2.Distance(p, layout.BaseCenter(MapLayoutData.SideOwn)) < layout.BaseRadius + 1.8f) return false;
            if (SVector2.Distance(p, layout.BaseCenter(MapLayoutData.SideEnemy)) < layout.BaseRadius + 1.8f) return false;

            for (int i = 0; i < layout.ParcelCount; i++)
            {
                GroundRect r = layout.ParcelRect(i);
                var grown = new GroundRect(r.X - 0.25f, r.Y - 0.25f, r.W + 0.5f, r.H + 0.5f);
                if (grown.Contains(p)) return false;
            }
            return true;
        }

        static void AppendTreePlanters(MapLayoutData layout, WorldMeshBatch batch)
        {
            if (layout.ArenaRadius <= 0f) return;
            SVector2[] spots =
            {
                new SVector2(-70.5f, 6.8f), new SVector2(70.5f, -6.8f),
                new SVector2(-68.2f, -6.4f), new SVector2(68.2f, 6.4f),
                new SVector2(-53.2f, 26.2f), new SVector2(53.2f, -26.2f),
                new SVector2(15.2f, 62.8f), new SVector2(-15.2f, -62.8f),
            };
            foreach (SVector2 s in spots)
            {
                if (BarangayLanes.InAnyNavigationLane(s, 1.4f)) continue;
                Vector3 wp = KalyeahResources.World(s, 0f);
                batch.AddVerticalCylinder(wp, 1.15f, 1.10f, 0.22f, 8, 3, 9);
                batch.AddVerticalCylinder(wp + Vector3.up * 0.04f, 0.92f, 0.92f, 0.19f, 8, 4, 4);
            }
        }

        static void PlaceDeterministicMicroProps(
            MapLayoutData layout,
            Transform propsParent,
            WorldMeshBatch batch,
            List<GameObject> outPoles)
        {
            var placed = new List<SVector2>(48);
            var prefabCache = new Dictionary<string, GameObject>(MicroPropIds.Length);
            var naturalHeights = new Dictionary<string, float>(MicroPropIds.Length);

            for (int i = 0; i < layout.ParcelCount; i++)
            {
                GroundRect r = layout.ParcelRect(i);
                float yaw = layout.HasOrientedParcels ? layout.ParcelYawRad(i) : 0f;
                SVector2 ux = new SVector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
                SVector2 uz = new SVector2(Mathf.Sin(yaw), Mathf.Cos(yaw));

                // Deterministic front/side apron offsets right beside the building plinth
                for (int slot = 0; slot < 2; slot++)
                {
                    int hash = (i * 7 + slot * 13 + 3) & 0x7FFFFFFF;
                    string propId = MicroPropIds[hash % MicroPropIds.Length];
                    float along = ((hash % 5) - 2) * 0.22f * r.W;
                    float sideSign = (slot == 0) ? 1f : -1f;
                    SVector2 spot = r.Center + uz * (sideSign * (r.H * 0.5f + 0.58f)) + ux * along;

                    if (!IsSafeMicroPropSpot(layout, spot, i, placed)) continue;
                    placed.Add(spot);

                    float propGodotYaw = yaw + (sideSign > 0f ? 0f : Mathf.PI) + (((hash >> 3) % 5) - 2) * 0.09f;
                    GameObject prefab = ResolvePropPrefab(propId, prefabCache);
                    if (prefab != null)
                    {
                        InstantiatePropModel(propsParent, propId, prefab, spot, propGodotYaw, naturalHeights, outPoles);
                    }
                    else
                    {
                        AppendProceduralMicroProp(batch, propId, spot, propGodotYaw);
                    }
                }
            }
        }

        static bool IsSafeMicroPropSpot(MapLayoutData layout, SVector2 p, int ownerParcel, List<SVector2> placed)
        {
            if (layout.ArenaRadius > 0f && p.Length() > layout.ArenaRadius - 3.2f) return false;
            if (layout.ArenaRadius > 0f && BarangayLanes.InAnyNavigationLane(p, 0.95f)) return false;
            if (layout.ArenaRadius <= 0f && Mathf.Abs(p.X) < 9.2f) return false;
            if (Mathf.Abs(p.Y - MapLayoutData.CanalZ(p.X)) < 6.8f) return false;
            if (SVector2.Distance(p, layout.BaseCenter(MapLayoutData.SideOwn)) < layout.BaseRadius + 2.4f) return false;
            if (SVector2.Distance(p, layout.BaseCenter(MapLayoutData.SideEnemy)) < layout.BaseRadius + 2.4f) return false;

            GroundRect rOwn = layout.RescueRect(MapLayoutData.SideOwn);
            GroundRect rEne = layout.RescueRect(MapLayoutData.SideEnemy);
            if (new GroundRect(rOwn.X - 2f, rOwn.Y - 2f, rOwn.W + 4f, rOwn.H + 4f).Contains(p)) return false;
            if (new GroundRect(rEne.X - 2f, rEne.Y - 2f, rEne.W + 4f, rEne.H + 4f).Contains(p)) return false;

            if (layout.PowerupSpots != null)
            {
                foreach (SVector2 pu in layout.PowerupSpots)
                    if (SVector2.Distance(p, pu) < 3.4f) return false;
            }

            for (int j = 0; j < layout.ParcelCount; j++)
            {
                GroundRect obs = layout.ParcelRect(j);
                float pad = (j == ownerParcel) ? 0.08f : 0.75f;
                if (new GroundRect(obs.X - pad, obs.Y - pad, obs.W + pad * 2f, obs.H + pad * 2f).Contains(p)) return false;
            }

            for (int k = 0; k < placed.Count; k++)
                if (SVector2.Distance(p, placed[k]) < 2.6f) return false;

            return true;
        }

        static GameObject ResolvePropPrefab(string propId, Dictionary<string, GameObject> cache)
        {
            if (cache.TryGetValue(propId, out GameObject cached)) return cached;
            GameObject found = KalyeahResources.Model("props", propId);
            if (found == null && propId == "blue_water_drum")
                found = KalyeahResources.Model("barangay", "brgy_water_drum");
            cache[propId] = found;
            return found;
        }

        static void InstantiatePropModel(
            Transform parent,
            string propId,
            GameObject prefab,
            SVector2 ground,
            float godotYaw,
            Dictionary<string, float> naturalHeights,
            List<GameObject> outPoles)
        {
            GameObject inst = Object.Instantiate(prefab, parent, false);
            inst.name = "Prop_" + propId;
            KalyeahResources.SanitizeModelMaterials(inst);
            foreach (Collider col in inst.GetComponentsInChildren<Collider>(true))
                KalyeahResources.Kill(col);

            Transform t = inst.transform;
            if (!naturalHeights.TryGetValue(propId, out float natH))
            {
                t.localScale = Vector3.one;
                t.rotation = Quaternion.identity;
                t.position = Vector3.zero;
                natH = Mathf.Max(KalyeahResources.RenderBounds(inst).size.y, 0.1f);
                naturalHeights[propId] = natH;
            }

            float targetH = TargetPropHeight(propId);
            float scale = Mathf.Clamp(targetH / natH, 0.25f, 1.35f);
            t.localScale = new Vector3(scale, scale, scale);
            t.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYaw), 0f);

            Vector3 spot = KalyeahResources.World(ground, 0f);
            t.position = spot;
            Bounds b = KalyeahResources.RenderBounds(inst);
            if (b.size.sqrMagnitude > 0.0001f)
                t.position += new Vector3(spot.x - b.center.x, -b.min.y, spot.z - b.center.z);
            if (b.size.y >= 2.2f)
                outPoles?.Add(inst);
        }

        static float TargetPropHeight(string propId)
        {
            switch (propId)
            {
                case "blue_water_drum": return 0.92f;
                case "monobloc_chair": return 0.78f;
                case "sari_icebox": return 0.68f;
                case "street_trash_bin": return 0.74f;
                case "planted_tire": return 0.48f;
                case "drainage_cover": return 0.08f;
                case "cinderblock_stack": return 0.52f;
                case "bench": return 0.56f;
                case "dog": return 0.58f;
                case "cat": return 0.36f;
                default: return 0.64f;
            }
        }

        static void AppendProceduralMicroProp(WorldMeshBatch batch, string propId, SVector2 ground, float godotYaw)
        {
            Vector3 wp = KalyeahResources.World(ground, 0f);
            Quaternion rot = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYaw), 0f);

            switch (propId)
            {
                case "blue_water_drum":
                    batch.AddVerticalCylinder(wp, 0.29f, 0.29f, 0.88f, 8, 6, 6);
                    batch.AddVerticalCylinder(wp + Vector3.up * 0.86f, 0.305f, 0.305f, 0.05f, 8, 4, 4);
                    break;
                case "sari_icebox":
                    batch.AddOrientedBox(wp + Vector3.up * 0.30f, new Vector3(0.76f, 0.60f, 0.50f), rot, 7, 7);
                    batch.AddOrientedBox(wp + Vector3.up * 0.63f, new Vector3(0.80f, 0.07f, 0.54f), rot, 5, 5);
                    break;
                case "street_trash_bin":
                    batch.AddVerticalCylinder(wp, 0.22f, 0.26f, 0.70f, 7, 4, 8);
                    batch.AddVerticalCylinder(wp + Vector3.up * 0.70f, 0.28f, 0.24f, 0.06f, 7, 8, 8);
                    break;
                case "planted_tire":
                    batch.AddVerticalCylinder(wp, 0.36f, 0.36f, 0.24f, 8, 4, 4);
                    batch.AddVerticalCylinder(wp + Vector3.up * 0.18f, 0.24f, 0.16f, 0.26f, 7, 3, 3);
                    break;
                case "cinderblock_stack":
                    batch.AddOrientedBox(wp + Vector3.up * 0.10f, new Vector3(0.42f, 0.20f, 0.22f), rot, 9, 1);
                    batch.AddOrientedBox(wp + rot * new Vector3(0.05f, 0.30f, 0.16f), new Vector3(0.42f, 0.20f, 0.22f), rot, 9, 1);
                    batch.AddOrientedBox(wp + rot * new Vector3(-0.18f, 0.10f, 0.24f), new Vector3(0.22f, 0.20f, 0.42f), rot, 9, 1);
                    break;
                default: // monobloc_chair & compact street props
                    batch.AddOrientedBox(wp + Vector3.up * 0.41f, new Vector3(0.46f, 0.06f, 0.46f), rot, 5, 5, true);
                    batch.AddOrientedBox(wp + rot * new Vector3(0f, 0.62f, -0.20f), new Vector3(0.44f, 0.38f, 0.06f), rot, 5, 5, true);
                    for (int lx = -1; lx <= 1; lx += 2)
                        for (int lz = -1; lz <= 1; lz += 2)
                            batch.AddOrientedBox(wp + rot * new Vector3(lx * 0.18f, 0.20f, lz * 0.18f), new Vector3(0.06f, 0.40f, 0.06f), rot, 5, 1);
                    break;
            }
        }
    }
}
