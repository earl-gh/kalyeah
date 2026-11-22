using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// Stylized sunlit-Manila architectural stand-in builder for any barangay kit whose .glb is absent:
    /// uses the SRP-batched <c>Kalyeah/BarangayBuilding</c> tri-planar 3-band facade shader across a cohesive
    /// 5-tone Manila neighborhood stucco palette (with pitched yero roof overhangs, striped awnings, organic
    /// clustered foliage trees, and solid Default-layer <see cref="BoxCollider"/>s for camera whisker &amp; Foot IK).
    /// </summary>
    public static class GreyBoxKit
    {
        public const string BuildingShaderName = "Kalyeah/BarangayBuilding";
        public const string BuildingMaterialName = "BarangayBuilding";

        public static readonly Color Body = new Color(0.875f, 0.824f, 0.745f); // Warm Manila cream #DFD2BE
        public static readonly Color Top = new Color(0.64f, 0.68f, 0.72f);     // Galvanized yero sheen
        public static readonly Color Trim = new Color(0.76f, 0.72f, 0.65f);    // Warm cornice plaster

        /// <summary>Cohesive Option A1 sunlit Manila stucco palette matching the target neighborhood art bible.</summary>
        public static readonly Color[] ManilaPalette =
        {
            new Color(0.875f, 0.824f, 0.745f, 1f), // 0: Warm Manila cream (#DFD2BE)
            new Color(0.769f, 0.549f, 0.447f, 1f), // 1: Muted terracotta (#C48C72)
            new Color(0.592f, 0.667f, 0.608f, 1f), // 2: Pale sage (#97AA9B)
            new Color(0.824f, 0.698f, 0.486f, 1f), // 3: Aged ochre (#D2B27C)
            new Color(0.553f, 0.620f, 0.671f, 1f), // 4: Dusty blue-slate (#8D9EAB)
        };

        static readonly Color AwningOchre = new Color(0.82f, 0.54f, 0.28f, 1f);
        static readonly Color AwningSlateBlue = new Color(0.32f, 0.49f, 0.66f, 1f);
        static readonly Color TreeBark = new Color(0.41f, 0.30f, 0.21f, 1f);
        static readonly Color TreeLeafDeep = new Color(0.24f, 0.44f, 0.23f, 1f);
        static readonly Color TreeLeafSunlit = new Color(0.38f, 0.60f, 0.28f, 1f);

        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropColor = Shader.PropertyToID("_Color");
        static readonly int PropBaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int PropSmoothness = Shader.PropertyToID("_Smoothness");
        static readonly int PropMetallic = Shader.PropertyToID("_Metallic");

        const float CapThickness = 0.14f;

        static readonly Material[] BuildingMats = new Material[5];
        static Material _top;
        static Material _trim;
        static Material _awningWarm;
        static Material _awningCool;
        static Material _treeBark;
        static Material _treeLeafDeep;
        static Material _treeLeafLit;

        /// <summary>The block's size in metres for this placement (`BlockSize` times `Scale`), or zero when it has none.</summary>
        public static Vector3 SizeOf(KitPlacement kit) => new Vector3(
            kit.BlockSize.X * kit.Scale.X,
            kit.BlockSize.Y * kit.Scale.Y,
            kit.BlockSize.Z * kit.Scale.Z);

        /// <summary>Deterministic 5-color Manila stucco index from world position so adjacent buildings vary harmoniously.</summary>
        public static int PaletteIndexFor(Vector3 worldPos, int seed = 0)
        {
            int hx = Mathf.RoundToInt(worldPos.x * 0.37f);
            int hz = Mathf.RoundToInt(worldPos.z * 0.53f);
            int hash = (hx * 73856093) ^ (hz * 19349663) ^ (seed * 83492791);
            return ((hash % ManilaPalette.Length) + ManilaPalette.Length) % ManilaPalette.Length;
        }

        /// <summary>
        /// Shared SRP-batched <c>Kalyeah/BarangayBuilding</c> material for one of the 5 sunlit Manila stucco tones
        /// (reused across GreyBoxKit, BarangayEnvironmentBuilder, and StreetMatchWorld with zero per-building duplicates).
        /// </summary>
        public static Material GetBarangayBuildingMaterial(int paletteIndex)
        {
            int idx = ((paletteIndex % ManilaPalette.Length) + ManilaPalette.Length) % ManilaPalette.Length;
            if (BuildingMats[idx] != null && BuildingMats[idx].shader != null && BuildingMats[idx].shader.isSupported)
                return BuildingMats[idx];

            Material mat = CreateBuildingMaterial("BarangayBuilding_" + idx, ManilaPalette[idx]);
            BuildingMats[idx] = mat;
            return mat;
        }

        /// <summary>Shared galvanized yero roof material.</summary>
        public static Material GetRoofYeroMaterial() => TopMaterial();

        /// <summary>Shared awning canopy material (alternates warm ochre and cool Manila slate-blue).</summary>
        public static Material GetAwningMaterial(int variant)
        {
            if ((variant & 1) == 0)
                return _awningWarm != null ? _awningWarm : _awningWarm = TintedLit("ManilaAwningOchre", AwningOchre, 0.16f, 0f);
            return _awningCool != null ? _awningCool : _awningCool = TintedLit("ManilaAwningBlue", AwningSlateBlue, 0.16f, 0f);
        }

        /// <summary>Builds the stylized architectural stand-in under `parent` standing on the kit's spot, or returns null when it has no block size.</summary>
        public static GameObject Build(Transform parent, KitPlacement kit)
        {
            Vector3 size = SizeOf(kit);
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return null;

            var root = new GameObject(kit.Kit + "_GreyBox");
            root.layer = 0;
            root.transform.SetParent(parent, false);
            Vector3 worldPos = KalyeahResources.World(kit.Ground) + Vector3.up * kit.Lift;
            root.transform.position = worldPos;
            root.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);

            string id = kit.Kit ?? "";
            if (id == "brgy_mango_tree" || id == "brgy_coconut_palm" || id == "brgy_banana_clump")
            {
                BuildOrganicTreeStandIn(root.transform, id, size, kit.VariantIndex);
                return root;
            }

            int paletteIdx = PaletteIndexFor(worldPos, kit.VariantIndex);
            float cap = Mathf.Min(CapThickness, size.y * 0.22f);
            Material bodyMat = GetBarangayBuildingMaterial(paletteIdx);
            Material topMat = TopMaterial();
            Material trimMat = TrimMaterial();
            Material awningMat = GetAwningMaterial(paletteIdx);

            if (id == "brgy_kapilya")
            {
                BuildKapilyaBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_barangay_hall" || id == "brgy_hall_plaza")
            {
                BuildHallBlock(root.transform, size, cap, bodyMat, topMat, trimMat, awningMat);
            }
            else if (id == "brgy_covered_court")
            {
                BuildCourtBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_sari_sari" || id == "brgy_palengke_stall" || id == "brgy_shophouse_row")
            {
                BuildShopStallBlock(root.transform, size, cap, bodyMat, topMat, awningMat);
            }
            else if (id == "brgy_jeepney" || id == "brgy_owner_jeep" || id == "brgy_passenger_jeepney")
            {
                BuildJeepneyBlock(root.transform, size, cap, bodyMat, topMat, trimMat);
            }
            else if (id == "brgy_merienda_stand")
            {
                BuildStandBlock(root.transform, size, cap, bodyMat, awningMat, trimMat);
            }
            else
            {
                // Sunlit Manila house/building with tri-planar 3-band facade + overhanging galvanized yero roof eaves
                AddCube(root.transform, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), bodyMat);
                AddCube(root.transform, "YeroRoof", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.34f, cap, size.z + 0.34f), topMat);
                if (size.y >= 3.2f)
                {
                    AddCube(root.transform, "RoofRidge", new Vector3(0f, size.y + 0.14f, 0f), new Vector3(size.x * 0.88f, 0.24f, size.z * 0.62f), topMat);
                }
            }

            // Solid Default-layer BoxCollider so ShoulderCamera.SolveWhiskerArm & AimSolver hit every building cleanly.
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
            col.size = size;
            col.isTrigger = false;

            return root;
        }

        static void BuildOrganicTreeStandIn(Transform root, string id, Vector3 size, int variant)
        {
            Material barkMat = _treeBark != null ? _treeBark : _treeBark = TintedLit("ManilaTreeBark", TreeBark, 0.08f, 0f);
            Material deepMat = _treeLeafDeep != null ? _treeLeafDeep : _treeLeafDeep = TintedLit("ManilaTreeLeafDeep", TreeLeafDeep, 0.12f, 0f);
            Material litMat = _treeLeafLit != null ? _treeLeafLit : _treeLeafLit = TintedLit("ManilaTreeLeafSunlit", TreeLeafSunlit, 0.16f, 0f);

            float h = Mathf.Clamp(size.y, 2.8f, 7.2f);
            float trunkH = id == "brgy_banana_clump" ? h * 0.42f : h * 0.56f;
            float trunkR = id == "brgy_coconut_palm" ? 0.18f : 0.26f;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            KalyeahResources.Kill(trunk.GetComponent<Collider>());
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(trunkR * 2f, trunkH * 0.5f, trunkR * 2f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

            float crownBaseY = trunkH + 0.35f;
            float spread = Mathf.Clamp(Mathf.Max(size.x, size.z) * 0.62f, 1.15f, 2.35f);
            AddFoliageLobe(root, "CrownCore", new Vector3(0f, crownBaseY + 0.55f, 0f), new Vector3(spread * 1.45f, spread * 0.95f, spread * 1.45f), litMat);
            for (int i = 0; i < 4; i++)
            {
                float ang = (i * 1.5707963f) + variant * 0.65f;
                Vector3 off = new Vector3(Mathf.Cos(ang) * spread * 0.52f, crownBaseY + ((i & 1) == 0 ? 0.12f : 0.38f), Mathf.Sin(ang) * spread * 0.52f);
                Vector3 lobeScale = new Vector3(spread * 0.92f, spread * 0.64f, spread * 0.92f);
                AddFoliageLobe(root, "CrownLobe_" + i, off, lobeScale, (i & 1) == 0 ? deepMat : litMat);
            }
        }

        static void AddFoliageLobe(Transform parent, string name, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lobe.name = name;
            KalyeahResources.Kill(lobe.GetComponent<Collider>());
            lobe.transform.SetParent(parent, false);
            lobe.transform.localPosition = localPos;
            lobe.transform.localScale = scale;
            lobe.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void BuildKapilyaBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float naveH = size.y * 0.62f;
            float towerH = size.y - naveH;
            float towerW = Mathf.Min(size.x * 0.34f, size.z * 0.36f);
            AddCube(root, "Nave", new Vector3(0f, naveH * 0.5f, 0f), new Vector3(size.x, naveH, size.z), body);
            AddCube(root, "NaveRoofTrim", new Vector3(0f, naveH + cap * 0.5f, 0f), new Vector3(size.x + 0.28f, cap, size.z + 0.28f), top);
            AddCube(root, "BellTower", new Vector3(0f, naveH + towerH * 0.5f, size.z * 0.28f), new Vector3(towerW, towerH, towerW), trim);
            AddCube(root, "BelfryCap", new Vector3(0f, size.y - cap * 0.5f, size.z * 0.28f), new Vector3(towerW + 0.24f, cap, towerW + 0.24f), top);
        }

        static void BuildHallBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim, Material awning)
        {
            float stepH = Mathf.Min(0.45f, size.y * 0.12f);
            AddCube(root, "HallSteps", new Vector3(0f, stepH * 0.5f, size.z * 0.52f), new Vector3(size.x * 0.52f, stepH, 1.2f), trim);
            AddCube(root, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), body);
            AddCube(root, "EntranceAwning", new Vector3(0f, Mathf.Max(2.5f, size.y * 0.48f), size.z * 0.5f + 0.55f), new Vector3(size.x * 0.44f, 0.18f, 1.15f), awning);
            AddCube(root, "Cornice", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.36f, cap * 1.4f, size.z + 0.36f), top);
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
            AddCube(root, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.28f, cap, size.z + 0.28f), top);
        }

        static void BuildShopStallBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material awning)
        {
            AddCube(root, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), body);
            // Band-3 upper awning shelf strictly above head height (2.4 m+) + galvanized yero roof eaves
            float awningY = Mathf.Max(2.45f, size.y * 0.62f);
            if (awningY < size.y - cap)
            {
                GameObject awningGo = AddCube(root, "Awning", new Vector3(0f, awningY, size.z * 0.5f + 0.45f), new Vector3(size.x * 0.90f, 0.14f, 0.95f), awning);
                awningGo.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            }
            AddCube(root, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.30f, cap, size.z + 0.30f), top);
        }

        static void BuildJeepneyBlock(Transform root, Vector3 size, float cap, Material body, Material top, Material trim)
        {
            float cabinLen = size.z * 0.72f;
            float hoodLen = size.z - cabinLen;
            float hoodH = size.y * 0.58f;
            AddCube(root, "Cabin", new Vector3(0f, (size.y - cap) * 0.5f, -hoodLen * 0.5f), new Vector3(size.x, size.y - cap, cabinLen), body);
            AddCube(root, "Hood", new Vector3(0f, hoodH * 0.5f, cabinLen * 0.5f), new Vector3(size.x * 0.88f, hoodH, hoodLen), trim);
            AddCube(root, "ClimbRoofTop", new Vector3(0f, size.y - cap * 0.5f, -hoodLen * 0.5f), new Vector3(size.x + 0.12f, cap, cabinLen + 0.12f), top);
        }

        static void BuildStandBlock(Transform root, Vector3 size, float cap, Material body, Material awning, Material trim)
        {
            float counterH = size.y * 0.58f;
            AddCube(root, "Counter", new Vector3(0f, counterH * 0.5f, 0f), new Vector3(size.x, counterH, size.z), body);
            float postH = size.y - counterH - cap;
            if (postH > 0.08f)
            {
                AddCube(root, "PostL", new Vector3(-size.x * 0.42f, counterH + postH * 0.5f, 0f), new Vector3(0.08f, postH, 0.08f), trim);
                AddCube(root, "PostR", new Vector3(size.x * 0.42f, counterH + postH * 0.5f, 0f), new Vector3(0.08f, postH, 0.08f), trim);
            }
            AddCube(root, "CanopyTop", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x + 0.24f, cap, size.z + 0.24f), awning);
        }

        static GameObject AddCube(Transform parent, string name, Vector3 localPos, Vector3 size, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            KalyeahResources.Kill(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return cube;
        }

        static Material TopMaterial() => _top != null ? _top : _top = CreateRoofMaterial();

        static Material TrimMaterial() => _trim != null ? _trim : _trim = TintedLit("ManilaCorniceTrim", Trim, 0.14f, 0f);

        static Material CreateBuildingMaterial(string name, Color stuccoColor)
        {
            Material preset = Resources.Load<Material>("KalyeahMaterials/" + BuildingMaterialName);
            Shader bldgShader = (preset != null && preset.shader != null && preset.shader.isSupported)
                ? preset.shader
                : Shader.Find(BuildingShaderName);

            Material mat;
            if (bldgShader != null && bldgShader.isSupported)
            {
                mat = preset != null
                    ? new Material(preset) { name = name, enableInstancing = true, hideFlags = HideFlags.DontSave }
                    : new Material(bldgShader) { name = name, enableInstancing = true, hideFlags = HideFlags.DontSave };
                if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, stuccoColor);
                return mat;
            }

            return TintedLit(name, stuccoColor, 0.12f, 0f);
        }

        static Material CreateRoofMaterial()
        {
            Material preset = Resources.Load<Material>("KalyeahMaterials/" + BuildingMaterialName);
            Shader bldgShader = (preset != null && preset.shader != null && preset.shader.isSupported)
                ? preset.shader
                : Shader.Find(BuildingShaderName);

            if (bldgShader != null && bldgShader.isSupported)
            {
                var mat = preset != null
                    ? new Material(preset) { name = "ManilaYeroRoof", enableInstancing = true, hideFlags = HideFlags.DontSave }
                    : new Material(bldgShader) { name = "ManilaYeroRoof", enableInstancing = true, hideFlags = HideFlags.DontSave };
                if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, Top);
                return mat;
            }
            return TintedLit("ManilaYeroRoof", Top, 0.42f, 0.35f);
        }

        static Material TintedLit(string name, Color color, float smoothness, float metallic)
        {
            Material template = KalyeahResources.Material("Ground", "Universal Render Pipeline/Lit");
            if (template == null) return null;
            var mat = new Material(template) { name = name, enableInstancing = true, hideFlags = HideFlags.DontSave };
            if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, color);
            if (mat.HasProperty(PropColor)) mat.SetColor(PropColor, color);
            if (mat.HasProperty(PropBaseMap)) mat.SetTexture(PropBaseMap, null);
            if (mat.HasProperty(PropSmoothness)) mat.SetFloat(PropSmoothness, smoothness);
            if (mat.HasProperty(PropMetallic)) mat.SetFloat(PropMetallic, metallic);
            return mat;
        }
    }
}
