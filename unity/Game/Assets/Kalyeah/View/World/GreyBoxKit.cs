using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// The stand-in drawn for a barangay kit whose .glb is not in the build: a clean grey block with a white top, at the kit's true
    /// size (docs/GRAPHICS.md "Building and checking the art"). Replaced kit by kit as licence-checked models arrive.
    /// </summary>
    public static class GreyBoxKit
    {
        public static readonly Color Body = new Color(0.62f, 0.63f, 0.65f);
        public static readonly Color Top = new Color(0.93f, 0.93f, 0.92f);
        const float CapThickness = 0.06f;

        static Material _body;
        static Material _top;

        /// <summary>The block's size in metres for this placement (`BlockSize` times `Scale`), or zero when it has none.</summary>
        public static Vector3 SizeOf(KitPlacement kit) => new Vector3(
            kit.BlockSize.X * kit.Scale.X,
            kit.BlockSize.Y * kit.Scale.Y,
            kit.BlockSize.Z * kit.Scale.Z);

        /// <summary>Builds the block under `parent` standing on the kit's spot, or returns null when the kit has no block size.</summary>
        public static GameObject Build(Transform parent, KitPlacement kit)
        {
            Vector3 size = SizeOf(kit);
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return null;

            var root = new GameObject(kit.Kit + "_GreyBox");
            root.transform.SetParent(parent, false);
            root.transform.position = KalyeahResources.World(kit.Ground) + Vector3.up * kit.Lift;
            root.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);

            float cap = Mathf.Min(CapThickness, size.y * 0.25f);
            AddCube(root.transform, "Body", new Vector3(0f, (size.y - cap) * 0.5f, 0f), new Vector3(size.x, size.y - cap, size.z), BodyMaterial());
            AddCube(root.transform, "Top", new Vector3(0f, size.y - cap * 0.5f, 0f), new Vector3(size.x, cap, size.z), TopMaterial());
            return root;
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
