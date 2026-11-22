// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Kalyeah.Game.Editor
{
    /// <summary>
    /// Unity 6 SpriteAtlas V2 prebuild pipeline (<c>manage-sprite-atlas</c>):
    /// enables <see cref="SpritePackerMode.SpriteAtlasV2"/>, ensures HUD icons and clean transparent kid busts
    /// are imported as Sprites without mipmaps, and authors <c>UI_HudIcons.spriteatlasv2</c> and
    /// <c>UI_Portraits.spriteatlasv2</c> via <see cref="SpriteAtlasAsset"/> + <see cref="SpriteAtlasImporter"/>
    /// (<c>includeInBuild = true</c>, no rotation, no tight packing, 4 px padding, Android ASTC 6x6 / iOS ASTC 4x4).
    /// </summary>
    public sealed class KalyeahUiAtlasPrebuild : IPreprocessBuildWithReport
    {
        public const string AtlasOutputDir = "Assets/Kalyeah/Generated/Atlases";
        public const string HudIconsAtlasPath = AtlasOutputDir + "/UI_HudIcons.spriteatlasv2";
        public const string PortraitsAtlasPath = AtlasOutputDir + "/UI_Portraits.spriteatlasv2";

        static readonly string[] HudIconFolders =
        {
            "Assets/Imported/Resources/ui/hud_icons",
            "Assets/Imported/Resources/icons/art",
        };

        static readonly string[] PortraitFolders =
        {
            "Assets/Imported/Resources/portraits/busts",
            "Assets/Imported/Resources/portraits/faces",
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureTmpEssentialsImported();
            RebuildAllAtlases();
        }

        /// <summary>
        /// Ensures Unity SpriteAtlas V2 packing is active and builds/updates both built-in UI SpriteAtlases.
        /// </summary>
        public static void RebuildAllAtlases()
        {
            EnsureSpritePackerV2Enabled();

            GenerateAtlasFromFolders(HudIconFolders, HudIconsAtlasPath, includeInBuild: true);
            GenerateAtlasFromFolders(PortraitFolders, PortraitsAtlasPath, includeInBuild: true);

            AssetDatabase.SaveAssets();
        }

        /// <summary>Enables <see cref="SpritePackerMode.SpriteAtlasV2"/> and reads the setting back to confirm it took.</summary>
        public static bool EnsureSpritePackerV2Enabled()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            SpritePackerMode confirmed = EditorSettings.spritePackerMode;
            if (confirmed != SpritePackerMode.SpriteAtlasV2)
            {
                Debug.LogWarning($"[KalyeahUiAtlasPrebuild] SpritePackerMode read back as {confirmed} (expected SpriteAtlasV2).");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Non-interactively imports TextMeshPro Essentials if <c>Assets/TextMesh Pro</c> is absent so builds retain
        /// <c>TextMeshPro/Mobile/Distance Field</c> shaders cleanly.
        /// </summary>
        static void EnsureTmpEssentialsImported()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                return;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type importerType = assemblies[i].GetType("TMPro.TMP_PackageResourceImporter", throwOnError: false);
                if (importerType == null) continue;
                MethodInfo importMethod = importerType.GetMethod(
                    "ImportResources",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(bool), typeof(bool), typeof(bool) },
                    null);
                if (importMethod != null)
                {
                    try
                    {
                        importMethod.Invoke(null, new object[] { true, false, false });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[KalyeahUiAtlasPrebuild] Non-interactive TMP Essentials import skipped: {ex.Message}");
                    }
                }
                return;
            }
        }

        /// <summary>Validates that a sprite path lives strictly inside the project's <c>Assets/</c> folder.</summary>
        public static bool IsValidProjectSpritePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                && assetPath.IndexOf("/Packages/", StringComparison.Ordinal) < 0;
        }

        static void GenerateAtlasFromFolders(string[] sourceFolders, string atlasPath, bool includeInBuild)
        {
            var validFolders = new List<string>(sourceFolders.Length);
            for (int i = 0; i < sourceFolders.Length; i++)
            {
                string folder = sourceFolders[i];
                if (AssetDatabase.IsValidFolder(folder))
                {
                    EnsureFolderTexturesAreSprites(folder);
                    validFolders.Add(folder);
                }
            }

            if (validFolders.Count == 0)
            {
                Debug.LogWarning($"[KalyeahUiAtlasPrebuild] No synced UI folders found for {atlasPath}; skipping atlas generation.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", validFolders.ToArray());
            var sprites = new List<Object>(guids.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsValidProjectSpritePath(path) || !seen.Add(path))
                    continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[KalyeahUiAtlasPrebuild] No sprites found for {atlasPath}; skipping atlas generation.");
                return;
            }

            string diskDir = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(diskDir))
                EnsureAssetFolder(diskDir.Replace('\\', '/'));

            SpriteAtlasAsset atlasAsset;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullAtlasDiskPath = Path.Combine(projectRoot, atlasPath.Replace('/', Path.DirectorySeparatorChar));
            bool existsOnDisk = File.Exists(fullAtlasDiskPath);
            if (existsOnDisk)
            {
                atlasAsset = SpriteAtlasAsset.Load(atlasPath) ?? new SpriteAtlasAsset();
                SpriteAtlas runtimeAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (runtimeAtlas != null)
                {
                    Object[] existingPackables = runtimeAtlas.GetPackables();
                    if (existingPackables != null && existingPackables.Length > 0)
                        atlasAsset.Remove(existingPackables);
                }
            }
            else
            {
                atlasAsset = new SpriteAtlasAsset();
            }

            atlasAsset.Add(sprites.ToArray());
            SpriteAtlasAsset.Save(atlasAsset, atlasPath);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);

            ConfigureAtlasImporter(atlasPath, includeInBuild);
            Debug.Log($"[KalyeahUiAtlasPrebuild] Built {atlasPath} with {sprites.Count} sprites (includeInBuild={includeInBuild}).");
        }

        static void EnsureFolderTexturesAreSprites(string folder)
        {
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var reimportPaths = new List<string>();
            for (int i = 0; i < texGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                if (!IsValidProjectSpritePath(path)) continue;
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (ti.mipmapEnabled)
                {
                    ti.mipmapEnabled = false;
                    dirty = true;
                }
                if (dirty)
                    reimportPaths.Add(path);
            }

            if (reimportPaths.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < reimportPaths.Count; i++)
                {
                    TextureImporter ti = AssetImporter.GetAtPath(reimportPaths[i]) as TextureImporter;
                    ti?.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static void ConfigureAtlasImporter(string atlasPath, bool includeInBuild)
        {
            SpriteAtlasImporter importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
            if (importer == null) return;

            SpriteAtlasTextureSettings textureSettings = importer.textureSettings;
            textureSettings.filterMode = FilterMode.Bilinear;
            textureSettings.generateMipMaps = false;
            textureSettings.readable = false;
            importer.textureSettings = textureSettings;

            SpriteAtlasPackingSettings packingSettings = importer.packingSettings;
            packingSettings.enableRotation = false;
            packingSettings.enableTightPacking = false;
            packingSettings.padding = 4;
            packingSettings.enableAlphaDilation = true;
            importer.packingSettings = packingSettings;

            SetPlatformSettings(importer, "Android", TextureImporterFormat.ASTC_6x6);
            SetPlatformSettings(importer, "iOS", TextureImporterFormat.ASTC_4x4);

            importer.includeInBuild = includeInBuild;
            importer.SaveAndReimport();
        }

        static void SetPlatformSettings(SpriteAtlasImporter importer, string platformName, TextureImporterFormat format)
        {
            var settings = new TextureImporterPlatformSettings
            {
                name = platformName,
                overridden = true,
                maxTextureSize = 2048,
                format = format,
            };
            importer.SetPlatformSettings(settings);
        }

        static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
