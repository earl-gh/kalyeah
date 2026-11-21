using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// The stand-in drawn for a barangay kit whose .glb is not in the build: clean, readable grey-and-white blockout
    /// silhouettes at the kit's true size with lighter top-ledge trims (docs/GRAPHICS.md L206 &amp; L220). Replaced kit by kit
    /// as licence-checked models arrive.
    /// </summary>
    public static class GreyBoxKit
    {
        public static readonly Color Body = new Color(0.62f, 0.63f, 0.65f);
        public static readonly Color Top = new Color(0.93f, 0.93f, 0.92f);
        public static readonly Color Trim = new Color(0.78f, 0.79f, 0.80f);
        const float CapThickness = 0.08f;

        static Material _body;
        static Material _top;
        static Material _trim;

        /// <summary>The block's size in metres for this placement (`BlockSize` times `Scale`), or zero when it has none.</summary>
        public static Vector3 SizeOf(KitPlacement kit) => new Vector3(
            kit.BlockSize.X * kit.Scale.X,
            kit.BlockSize.Y * kit.Scale.Y,
            kit.BlockSize.Z * kit.Scale.Z);

        /// <summary>Builds the silhouette blockout under `parent` standing on the kit's spot, or returns null when the kit has no block size.</summary>
        public static GameObject Build(Transform parent, KitPlacement kit)
        {
            Vector3 size = SizeOf(kit);
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return null;

            var root = new GameObject(kit.Kit + "_GreyBox");
            root.transform.SetParent(parent, false);
            root.transform.position = KalyeahResources.World(kit.Ground) + Vector3.up * kit.Lift;
            root.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);

            string id = kit.Kit ?? "";
            float cap = Mathf.Min(CapThickness, size.y * 0.25f);
            Material bodyMat = BodyMaterial();
            Material topMat = TopMaterial();
            Material trimMat = TrimMaterial();

            if (id == "brgy_kapilya")
            {
                BuildKapilyaBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_barangay_hall" || id == "brgy_hall_plaza")
            {
                BuildHallBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_covered_court")
            {
                BuildCourtBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_sari_sari" || id == "brgy_palengke_stall")
            {
                BuildShopStallBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_jeepney" || id == "brgy_owner_jeep" || id == "brgy_passenger_jeepney")
            {
                BuildJeepneyBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_merienda_stand")
            {
                BuildStandBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else
            {
                // Clean default grey-box block with slightly lighter overhanging top-ledge trim (docs/GRAPHICS.md L206)
                AddCube(root.transform, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), bodyMat);
                AddCube(root.transform, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.08f, cap, size.z + 0.08f), topMat);
            }
            return root;
        }

        static void BuildKapilyaBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float naveH = size.y * 0.62f;
            float towerH = size.y - naveH;
            float towerW = Mathf.Min(size.x * 0.34f, size.z * 0.36f);
            AddCube(root, "Nave", new Vector3(0f, naveH * 0.5f, 0f), new Vector3(size.x, naveH, size.z), body);
            AddCube(root, "NaveRoofTrim", new Vector3(0f, naveH + cap * 0.5f, 0f), new Vector3(size.x + 0.14f, cap, size.z + 0.14f), top);
            AddCube(root, "BellTower", new Vector3(0f, naveH + towerH * 0.5f, size.z * 0.28f), new Vector3(towerW, towerH, towerW), trim);
            AddCube(root, "BelfryCap", new Vector3(0f, size.y - cap * 0.5f, size.z * 0.28f), new Vector3(towerW + 0.16f, cap, towerW + 0.16f), top);
        }

        static void BuildHallBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float stepH = Mathf.Min(0.45f, size.y * 0.12f);
            AddCube(root, "HallSteps", new Vector3(0f, stepH * 0.5f, size.z * 0.52f), new Vector3(size.x * 0.52f, stepH, 1.2f), trim);
            AddCube(root, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), body);
            AddCube(root, "Cornice", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.24f, cap * 1.4f, size.z + 0.24f), top);
        }

        static void BuildCourtBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float roofH = Mathf.Max(0.45f, size.y * 0.16f);
            float colH = Mathf.Max(0.5f, size.y - roofH);
            float colW = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.06f, 0.25f, 0.55f);
            float ox = size.x * 0.45f;
            float oz = size.z * 0.45f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    AddCube(root, "Pillar", new Vector3(sx * ox, colH * 0.5f, sz * oz), new Vector3(colW, colH, colW), trim);
            AddCube(root, "Roof", new Vector3(0f, colH + (roofH - cap) * 0.5f, 0f), new Vector3(size.x, roofH - cap, size.z), body);
            AddCube(root, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.12f, cap, size.z + 0.12f), top);
        }

        static void BuildShopStallBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            AddCube(root, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), body);
            // Band-3 upper awning shelf above head height (2.4 m+) + lighter climbable roof trim
            float awningY = Mathf.Max(2.35f, size.y * 0.72f);
            if (awningY < size.y - cap)
                AddCube(root, "Awning", new Vector3(0f, awningY, size.z * 0.5f + 0.42f), new Vector3(size.x * 0.92f, 0.16f, 0.88f), trim);
            AddCube(root, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.14f, cap, size.z + 0.14f), top);
        }

        static void BuildJeepneyBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float cabinLen = size.z * 0.72f;
            float hoodLen = size.z - cabinLen;
            float hoodH = size.y * 0.58f;
            AddCube(root, "Cabin", new Vector3(0f, (size.y - cap) * 0.5f, -hoodLen * 0.5f), new Vector3(size.x, size.y - cap, cabinLen), body);
            AddCube(root, "Hood", new Vector3(0f, hoodH * 0.5f, cabinLen * 0.5f), new Vector3(size.x * 0.88f, hoodH, hoodLen), trim);
            AddCube(root, "ClimbRoofTop", new Vector3(0f, size.y - cap * 0.5f, -hoodLen * 0.5f), new Vector3(size.x + 0.08f, cap, cabinLen + 0.08f), top);
        }

        static void BuildStandBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float counterH = size.y * 0.58f;
            AddCube(root, "Counter", new Vector3(0f, counterH * 0.5f, 0f), new Vector3(size.x, counterH, size.z), body);
            float postH = size.y - counterH - cap;
            if (postH > 0.08f)
            {
                AddCube(root, "PostL", new Vector3(-size.x * 0.42f, counterH + postH * 0.5f, 0f), new Vector3(0.08f, postH, 0.08f), trim);
                AddCube(root, "PostR", new Vector3(size.x * 0.42f, counterH + postH * 0.5f, 0f), new Vector3(0.08f, postH, 0.08f), trim);
            }
            AddCube(root, "CanopyTop", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.18f, cap, size.z + 0.18f), top);
        }

        static void AddCube(Transform parent, string name, Vector3 localPos, Vector3 size, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            KalyeahResources.Kill(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        static Material BodyMaterial() => _body != null ? _body : _body = Tinted("GreyBoxBody", Body);

        static Material TopMaterial() => _top != null ? _top : _top = Tinted("GreyBoxTop", Top);

        static Material TrimMaterial() => _trim != null ? _trim : _trim = Tinted("GreyBoxTrim", Trim);

        static Material Tinted(string name, Color color)
        {
            Material template = KalyeahResources.Material("Ground", "Universal Render Pipeline/Lit");
            if (template == null) return null;
            var mat = new Material(template) { name = name, enableInstancing = true };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
