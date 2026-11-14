using UnityEngine;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Where the game finds what tools/unity_export/sync_assets.sh copied in (Assets/Imported/Resources/...) and what KalyeahBuild.Setup
    /// generated (Assets/Kalyeah/Generated/Resources/KalyeahMaterials). Paths mirror the Godot project's assets/ folder.
    /// </summary>
    public static class KalyeahResources
    {
        /// <summary>unity/Data/*.json as the view and the sim read it.</summary>
        public static DataSource Data() => new DataSource(name =>
        {
            TextAsset text = Resources.Load<TextAsset>("Data/" + name);
            return text != null ? text.text : null;
        });

        /// <summary>A .glb that glTFast imported (folder "characters", "barangay", "props" or "weapons"), or null.</summary>
        public static GameObject Model(string folder, string id) => Resources.Load<GameObject>($"models/{folder}/{id}");

        public static Texture2D Texture(string path) => Resources.Load<Texture2D>(path);

        public static Font UiFont() => FontOr("fonts/brand/KalyeahSans-Variable");

        public static Font DisplayFont() => FontOr("fonts/brand/KalyeahDisplay-Regular");

        static Font FontOr(string path)
        {
            Font font = Resources.Load<Font>(path);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A material KalyeahBuild.Setup made (so its shader is in the build); falls back to a fresh one in the editor.</summary>
        public static Material Material(string name, string shader)
        {
            Material made = Resources.Load<Material>("KalyeahMaterials/" + name);
            if (made != null) return made;
            Shader found = Shader.Find(shader);
            return found != null ? new Material(found) : null;
        }

        public static Vector3 U(SVector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static SVector2 S(Vector2 v) => new SVector2(v.x, v.y);

        /// <summary>A sim ground point (x, z) as a Unity world point (GodotSpace.World).</summary>
        public static Vector3 World(SVector2 ground, float y = 0f) => U(GodotSpace.World(ground, y));

        /// <summary>Object.Destroy in play, DestroyImmediate in the editor's edit mode (the screenshots build the match there).</summary>
        public static void Kill(Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        /// <summary>The world bounds of every renderer under `root` (its size at identity is the model's own size).</summary>
        public static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
