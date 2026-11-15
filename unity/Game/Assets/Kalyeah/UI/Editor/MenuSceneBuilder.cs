using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kalyeah.UI.Editor
{
    /// <summary>
    /// Writes the Title scene the menus run in: one GameObject with MenuRoot, which builds everything else in code (no hand-made scene
    /// YAML in git). KalyeahBuild calls `BuildTitleScene` when it writes the build's scenes; it can also be run from the menu
    /// (Kalyeah > Build Menu Scene) or with `-executeMethod Kalyeah.UI.Editor.MenuSceneBuilder.Build`.
    /// </summary>
    public static class MenuSceneBuilder
    {
        /// <summary>Where KalyeahBuild keeps its generated scenes (Assets/Kalyeah/Generated is git-ignored).</summary>
        public const string DefaultPath = "Assets/Kalyeah/Generated/Scenes/" + MenuRoot.SceneName + ".unity";

        [MenuItem("Kalyeah/Build Menu Scene")]
        public static void Build() => BuildTitleScene(DefaultPath);

        /// <summary>Creates (or replaces) the Title scene at `path` and returns that path.</summary>
        public static string BuildTitleScene(string path)
        {
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EnsureFolder(folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(MenuRoot.SceneName, typeof(MenuRoot));
            root.transform.position = Vector3.zero;
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
