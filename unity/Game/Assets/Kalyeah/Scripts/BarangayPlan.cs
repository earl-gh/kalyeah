using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kalyeah.Game
{
    /// <summary>How a kit stands at its spot.</summary>
    public enum KitFit
    {
        /// <summary>The model's own origin on the spot (Godot _place_walkthrough / _add_prop at y 0): walls, trees, posts, perimeter houses.</summary>
        Origin,
        /// <summary>The model's footprint (its bounds' centre) on the spot and its lowest point `Lift` above the ground: a parcel.</summary>
        Footprint,
    }

    /// <summary>One barangay kit (mapped to real textured .glb models in assets/models/props/ or assets/models/barangay/) to put in the street.</summary>
    public sealed class KitPlacement
    {
        public string Kit;
        /// <summary>Optional real textured .glb prop model id under <c>assets/models/props/&lt;PropModel&gt;.glb</c>.</summary>
        public string PropModel;
        /// <summary>Deterministic variant index used for multi-house prop variation and Manila stucco palette variety.</summary>
        public int VariantIndex;
        /// <summary>The spot, sim XZ.</summary>
        public Vector2 Ground;
        /// <summary>As the Godot view turns it; GodotSpace.ModelYawDegrees makes it a Unity yaw.</summary>
        public float GodotYawRad;
        /// <summary>Scale per axis, in the model's own frame.</summary>
        public Vector3 Scale = Vector3.One;
        /// <summary>When above 0: first scale the whole model so it stands this tall (metres), then apply `Scale` on top.</summary>
        public float UniformHeight;
        /// <summary>Per axis, when above 0: that axis is scaled so the model measures this many metres, kept in [AxisMin, AxisMax].</summary>
        public Vector3 AxisTarget;
        public Vector3 AxisMin = Vector3.Zero;
        public Vector3 AxisMax = new Vector3(float.MaxValue);
        public KitFit Fit = KitFit.Origin;
        /// <summary>The lowest point's height above the ground (Footprint only).</summary>
        public float Lift;
        /// <summary>A building that may stand between the camera and your kid.</summary>
        public bool Occluder;
        /// <summary>
        /// The stand-in's size in metres (model frame: X width, Y height, Z depth) before `Scale`, also used to fit unit-normalized
        /// prop .glb models to exact real-world parcel and wall footprints.
        /// </summary>
        public Vector3 BlockSize;

        /// <summary>The final scale for a model whose unscaled size is `natural` (metres, model frame).</summary>
        public Vector3 ScaleFor(Vector3 natural)
        {
            Vector3 s = Scale;
            if (UniformHeight > 0f) s *= UniformHeight / MathF.Max(natural.Y, 0.1f);
            if (AxisTarget.X > 0f) s.X = Math.Clamp(AxisTarget.X / MathF.Max(natural.X, 0.1f), AxisMin.X, AxisMax.X);
            if (AxisTarget.Y > 0f) s.Y = Math.Clamp(AxisTarget.Y / MathF.Max(natural.Y, 0.1f), AxisMin.Y, AxisMax.Y);
            if (AxisTarget.Z > 0f) s.Z = Math.Clamp(AxisTarget.Z / MathF.Max(natural.Z, 0.1f), AxisMin.Z, AxisMax.Z);
            return s;
        }

        /// <summary>
        /// Fits a textured <c>assets/models/props/*.glb</c> model (many of which are unit-normalized ~1 m Meshy exports)
        /// cleanly into this placement's real-world meter bounds without overhanging navigation lanes or distorting silhouettes.
        /// </summary>
        public Vector3 FitPropScale(Vector3 natural)
        {
            float natX = MathF.Max(natural.X, 0.08f);
            float natY = MathF.Max(natural.Y, 0.08f);
            float natZ = MathF.Max(natural.Z, 0.08f);

            // Base posts (`post_blue`, `post_red`) already specify exact UniformHeight + PostWidthShare.
            if (UniformHeight > 0f)
                return ScaleFor(natural);

            // Perimeter walls (`plank_fence`) fit exact chord width, wall height, and sturdy depth.
            if (Kit == "brgy_chb_wall")
            {
                float w = BlockSize.X > 0.1f ? BlockSize.X : 8f;
                float h = BlockSize.Y > 0.1f ? BlockSize.Y : 2.25f;
                float d = MathF.Max(BlockSize.Z, 0.28f);
                return new Vector3(w / natX, h / natY, d / natZ);
            }

            // Compact standalone props (`ice_cream_cart`, `tricycle`, `crates`) scale uniformly to natural human height.
            if (Kit == BarangayPlan.PowerupStand || Kit == "brgy_tricycle" || Kit == "brgy_cover_wall")
            {
                float targetH = BlockSize.Y > 0.1f ? BlockSize.Y * Scale.Y : 1.45f;
                float u = Math.Clamp(targetH / natY, 0.25f, 3.2f);
                return new Vector3(u, u, u);
            }

            // Houses, stores, terminals, and tents: fit strictly inside the sim parcel footprint (94% inset so eaves stay clear of lanes).
            if (BlockSize.X > 0.1f && BlockSize.Y > 0.1f && BlockSize.Z > 0.1f)
            {
                float targetX = BlockSize.X * Scale.X * 0.94f;
                float targetY = BlockSize.Y * Scale.Y;
                float targetZ = BlockSize.Z * Scale.Z * 0.94f;

                float sx = targetX / natX;
                float sz = targetZ / natZ;
                float sy = targetY / natY;

                // Preserve clean architectural proportions on unit-normalized (~1 m) Meshy buildings.
                float horiz = 0.5f * (sx + sz);
                if (natY <= 1.6f)
                    sy = Math.Clamp(sy, horiz * 0.58f, horiz * 1.25f);

                return new Vector3(sx, sy, sz);
            }

            return ScaleFor(natural);
        }
    }

    /// <summary>
    /// Where the barangay's kits stand: places the 32 building parcels, the two base posts, the merienda/power-up stands,
    /// and the perimeter wall, trees, and two rings of neighborhood houses, mapping each slot to our real textured Filipino
    /// <c>assets/models/props/*.glb</c> library with deterministic variant cycling.
    /// </summary>
    public static class BarangayPlan
    {
        /// <summary>street_map.gd OBSTACLE_KIT_MAP: the kit drawn for each parcel prop id.</summary>
        public static readonly Dictionary<string, string> ObstacleKits = new Dictionary<string, string>
        {
            ["tabi_cluster_a"] = "brgy_tabi_cluster_a",
            ["tabi_cluster_b"] = "brgy_tabi_cluster_b",
            ["poso_well"] = "brgy_poso_well",
            ["sari_sari"] = "brgy_sari_sari",
            ["bakery_oven"] = "brgy_bakery_oven",
            ["kapilya"] = "brgy_kapilya",
            ["house_two_storey"] = "brgy_house_two_storey",
            ["shophouse_row"] = "brgy_shophouse_row",
            ["palengke_stall"] = "brgy_palengke_stall",
            ["waiting_shed"] = "brgy_waiting_shed",
            ["toda_shed"] = "brgy_toda_shed",
            ["jeepney"] = "brgy_jeepney",
            ["owner_jeep"] = "brgy_owner_jeep",
            ["passenger_jeepney"] = "brgy_passenger_jeepney",
            ["tricycle"] = "brgy_tricycle",
            ["cover_wall"] = "brgy_cover_wall",
            ["bench"] = "brgy_cover_wall",
            ["tent"] = "brgy_palengke_stall",
        };

        /// <summary>
        /// Maps each barangay kit id to one or more verified textured <c>.glb</c> prop ids synced from
        /// <c>assets/models/props/</c> into Unity <c>Resources/models/props/</c>.
        /// </summary>
        public static readonly Dictionary<string, string[]> PropModelMap = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["brgy_sari_sari"] = new[] { "nena_store" },
            ["brgy_tabi_cluster_a"] = new[] { "house_1", "barber_shop", "house_3" },
            ["brgy_tabi_cluster_b"] = new[] { "house_2", "vulcanizing", "house_1" },
            ["brgy_house_two_storey"] = new[] { "house_2", "house_3", "house_1" },
            ["brgy_shophouse_row"] = new[] { "barber_shop", "nena_store", "vulcanizing" },
            ["brgy_bakery_oven"] = new[] { "karinderya", "nena_store" },
            ["brgy_palengke_stall"] = new[] { "rooster_tent", "nena_store" },
            ["brgy_toda_shed"] = new[] { "tricycle_terminal" },
            ["brgy_waiting_shed"] = new[] { "tricycle_terminal" },
            ["brgy_tricycle"] = new[] { "tricycle" },
            ["brgy_post_blue"] = new[] { "post_blue" },
            ["brgy_post_red"] = new[] { "post_red" },
            ["brgy_merienda_stand"] = new[] { "ice_cream_cart" },
            ["brgy_sorbetes_cart"] = new[] { "ice_cream_cart" },
            ["brgy_chb_wall"] = new[] { "plank_fence" },
            ["brgy_cover_wall"] = new[] { "crates", "plank_fence" },
            ["brgy_wooden_hoop"] = new[] { "basketball_pole" },
            ["brgy_poster_cluster"] = new[] { "no_id_no_entry" },
            ["brgy_bench"] = new[] { "bench" },
            ["brgy_water_drum"] = new[] { "blue_water_drum" },
        };

        /// <summary>Resolves the deterministic <c>assets/models/props/&lt;id&gt;.glb</c> name for a kit, or null when procedural stand-in applies.</summary>
        public static string ResolvePropModel(string kitId, int variantIndex = 0)
        {
            if (string.IsNullOrEmpty(kitId)) return null;
            if (!PropModelMap.TryGetValue(kitId, out string[] variants) || variants == null || variants.Length == 0)
                return null;
            int idx = ((variantIndex % variants.Length) + variants.Length) % variants.Length;
            return variants[idx];
        }

        /// <summary>street_map.gd BUILTIN_SLAB_KITS: kits that bring their own slab (no 4.5 cm plinth lift).</summary>
        static readonly HashSet<string> SlabKits = new HashSet<string>
        {
            "brgy_toda_shed", "brgy_palengke_stall", "brgy_bakery_oven", "brgy_kapilya", "brgy_waiting_shed", "brgy_cover_wall",
        };

        static readonly string[] PerimeterKits =
        {
            "brgy_tabi_cluster_a", "brgy_shophouse_row", "brgy_tabi_cluster_b", "brgy_house_two_storey", "brgy_sari_sari",
            "brgy_tabi_cluster_a", "brgy_house_two_storey",
        };

        static readonly string[] PerimeterPropModels =
        {
            "house_1", "nena_store", "house_2", "barber_shop", "house_3", "vulcanizing", "house_1", "house_2",
        };

        static readonly string[] TreeKits = { "brgy_mango_tree", "brgy_coconut_palm", "brgy_banana_clump" };

        public const string PostBlue = "brgy_post_blue";
        public const string PostRed = "brgy_post_red";
        public const string PowerupStand = "brgy_merienda_stand";

        static readonly Vector3 TreeBlock = new Vector3(2.4f, 6f, 2.4f);
        static readonly Vector3 HouseBlock = new Vector3(7f, 6f, 6f);

        /// <summary>How tall a parcel's stand-in / target height is, by what stands there (metres).</summary>
        static float BlockHeight(string prop) => prop switch
        {
            "jeepney" or "owner_jeep" or "passenger_jeepney" => 2.4f,
            "tricycle" => 1.8f,
            "bench" => 0.9f,
            "poso_well" => 1.6f,
            "waiting_shed" or "toda_shed" or "palengke_stall" or "tent" or "bakery_oven" => 3.4f,
            "kapilya" => 7.5f,
            _ => 5.6f,
        };

        public static List<KitPlacement> Build(MapLayoutData layout)
        {
            var result = new List<KitPlacement>();
            AddParcels(layout, result);
            AddPosts(layout, result);
            int standIdx = 0;
            foreach (Vector2 spot in layout.PowerupSpots)
            {
                result.Add(new KitPlacement
                {
                    Kit = PowerupStand,
                    PropModel = ResolvePropModel(PowerupStand, standIdx),
                    VariantIndex = standIdx,
                    Ground = spot,
                    Fit = KitFit.Footprint,
                    BlockSize = new Vector3(1.6f, 1.45f, 1.0f),
                });
                standIdx++;
            }
            AddPerimeter(layout, result);
            return result;
        }

        static void AddParcels(MapLayoutData layout, List<KitPlacement> result)
        {
            for (int index = 0; index < layout.ParcelCount; index++)
            {
                string prop = layout.ParcelProp(index);
                string kit = ObstacleKits.TryGetValue(prop, out string k) ? k : "brgy_cover_wall";
                float u = Math.Clamp(layout.ParcelScale(index), 0.90f, 1.08f);
                float sy = u * (0.97f + 0.05f * MathF.Sin(index * 2.3f + 0.5f));
                result.Add(new KitPlacement
                {
                    Kit = kit,
                    PropModel = ResolvePropModel(kit, index),
                    VariantIndex = index,
                    Ground = layout.ParcelRect(index).Center,
                    GodotYawRad = layout.HasOrientedParcels ? layout.ParcelYawRad(index) : 0f,
                    Scale = new Vector3(u, sy, u),
                    // a low cover wall is exactly cover_height tall
                    AxisTarget = prop == "cover_wall" ? new Vector3(0f, layout.CoverHeight, 0f) : Vector3.Zero,
                    Fit = KitFit.Footprint,
                    Lift = SlabKits.Contains(kit) ? 0f : 0.045f,
                    Occluder = true,
                    BlockSize = new Vector3(
                        layout.ParcelRect(index).W,
                        prop == "cover_wall" ? layout.CoverHeight : BlockHeight(prop),
                        layout.ParcelRect(index).H),
                });
            }
        }

        static void AddPosts(MapLayoutData layout, List<KitPlacement> result)
        {
            // Your post is the blue one, theirs the red one (street_map.gd _dress_post with team 0 on the own side); the blue model is turned
            // half way round. post_height tall, post_width_share as wide as that scale.
            foreach (int side in new[] { MapLayoutData.SideOwn, MapLayoutData.SideEnemy })
            {
                bool own = side == MapLayoutData.SideOwn;
                string kitId = own ? PostBlue : PostRed;
                result.Add(new KitPlacement
                {
                    Kit = kitId,
                    PropModel = ResolvePropModel(kitId, 0),
                    VariantIndex = own ? 0 : 1,
                    Ground = layout.PostPosition(side),
                    GodotYawRad = own ? MathF.PI : 0f,
                    UniformHeight = layout.PostHeight,
                    Scale = new Vector3(layout.PostWidthShare, 1f, layout.PostWidthShare),
                    BlockSize = new Vector3(0.6f, layout.PostHeight, 0.6f),
                });
            }
        }

        static void AddPerimeter(MapLayoutData layout, List<KitPlacement> result)
        {
            if (layout.ArenaRadius <= 0f) return;
            float ringRadius = layout.ArenaRadius;
            const int innerSlots = 48;
            float wallR = ringRadius + 1.15f;
            float houseInnerR = ringRadius + 6.8f;
            float wallChord = (2f * MathF.PI * wallR / innerSlots) * 1.05f;
            GroundRect rOwn = layout.RescueRect(MapLayoutData.SideOwn);
            GroundRect rEne = layout.RescueRect(MapLayoutData.SideEnemy);

            for (int i = 0; i < innerSlots; i++)
            {
                float angle = 2f * MathF.PI * i / innerSlots;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 pWall = dir * wallR;
                bool inCanalMouth = MathF.Abs(pWall.Y - MapLayoutData.CanalZ(pWall.X)) < 5.4f;
                bool inOwnShed = pWall.X < 0f && pWall.Y >= rOwn.Y - 1.5f && pWall.Y <= rOwn.End.Y + 1.5f;
                bool inEneShed = pWall.X > 0f && pWall.Y >= rEne.Y - 1.5f && pWall.Y <= rEne.End.Y + 1.5f;
                bool inPlaza = MathF.Abs(pWall.X) < 10.5f && MathF.Abs(pWall.Y) > 76f;
                float inwardYaw = MathF.Atan2(-dir.X, -dir.Y);

                if (!(inCanalMouth || inOwnShed || inEneShed || inPlaza))
                {
                    result.Add(new KitPlacement
                    {
                        Kit = "brgy_chb_wall",
                        PropModel = ResolvePropModel("brgy_chb_wall", i),
                        VariantIndex = i,
                        Ground = pWall,
                        GodotYawRad = inwardYaw,
                        AxisTarget = new Vector3(wallChord, 2.25f, 0f),
                        AxisMin = new Vector3(0.96f, 0.92f, 0f),
                        AxisMax = new Vector3(1.88f, 1.15f, 0f),
                        BlockSize = new Vector3(wallChord, 2.25f, 0.3f),
                    });
                    if (i % 2 == 0)
                    {
                        float ts = 0.94f + 0.24f * MathF.Abs(MathF.Sin(i * 2.3f));
                        result.Add(new KitPlacement
                        {
                            Kit = TreeKits[(i / 2) % TreeKits.Length],
                            VariantIndex = i / 2,
                            Ground = dir * (ringRadius + 3.9f + 1.8f * MathF.Abs(MathF.Cos(i * 1.9f))),
                            GodotYawRad = i * 1.1f,
                            Scale = new Vector3(ts),
                            BlockSize = TreeBlock,
                        });
                    }
                }

                if (i % 4 != 2)
                {
                    float localR = houseInnerR + 2.2f * MathF.Sin(i * 2.9f + 0.4f);
                    if (MathF.Abs(dir.Y) > 0.88f) localR += 23.5f;
                    else if (inCanalMouth || inOwnShed || inEneShed) localR += 4.8f;
                    Vector2 pos = dir * localR;
                    if (Vector2.Distance(pos, new Vector2(21.5f, 88.2f)) < 9.2f || Vector2.Distance(pos, new Vector2(-21.5f, -88.2f)) < 9.2f)
                        pos = dir * (localR + 9.6f);
                    float us = 0.94f + 0.14f * MathF.Abs(MathF.Sin(i * 2.13f + 0.3f));
                    result.Add(new KitPlacement
                    {
                        Kit = PerimeterKits[i % PerimeterKits.Length],
                        PropModel = PerimeterPropModels[i % PerimeterPropModels.Length],
                        VariantIndex = i,
                        Ground = pos,
                        GodotYawRad = inwardYaw + 11.5f * MathF.Sin(i * 3.7f + 0.9f) * MathF.PI / 180f,
                        Scale = new Vector3(us),
                        Occluder = true,
                        BlockSize = HouseBlock,
                    });
                }
            }

            const int outerCount = 40;
            float outerR = ringRadius + 19.6f;
            for (int j = 0; j < outerCount; j++)
            {
                float angleO = 2f * MathF.PI * (j + 0.5f) / outerCount + 0.018f * MathF.Cos(j * 3.1f);
                var dirO = new Vector2(MathF.Cos(angleO), MathF.Sin(angleO));
                float rO = outerR + 2.8f * MathF.Sin(j * 3.3f + 1.1f);
                if (MathF.Abs(dirO.Y) > 0.88f) rO += 16f;
                float uso = 0.96f + 0.16f * MathF.Abs(MathF.Cos(j * 2.3f + 0.7f));
                result.Add(new KitPlacement
                {
                    Kit = PerimeterKits[(j + 2) % PerimeterKits.Length],
                    PropModel = PerimeterPropModels[(j + 3) % PerimeterPropModels.Length],
                    VariantIndex = j + 64,
                    Ground = dirO * rO,
                    GodotYawRad = MathF.Atan2(-dirO.X, -dirO.Y) + 12.5f * MathF.Cos(j * 2.9f) * MathF.PI / 180f,
                    Scale = new Vector3(uso),
                    Occluder = true,
                    BlockSize = HouseBlock,
                });
                if (j % 2 == 0)
                {
                    float gs = 1.04f + 0.22f * MathF.Abs(MathF.Sin(j * 1.7f));
                    result.Add(new KitPlacement
                    {
                        Kit = TreeKits[(j + 1) % TreeKits.Length],
                        VariantIndex = j + 100,
                        Ground = dirO * (rO + 8.5f),
                        GodotYawRad = j * 1.4f,
                        Scale = new Vector3(gs),
                        BlockSize = TreeBlock,
                    });
                }
            }
        }
    }
}
