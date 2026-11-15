using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kalyeah.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the Unity project from code, so no fragile hand-written asset YAML is kept in git. Entry points for -executeMethod (global
/// namespace on purpose: `-executeMethod KalyeahBuild.Setup`):
///   KalyeahBuild.Setup         player settings (package, landscape, arm64, SDKs, input), the URP pipeline asset, the few materials the
///                              code needs, the glTFast import settings, the Title and Match scenes and the build's scene list.
///   KalyeahBuild.BuildAndroid  Setup, then the APK at -customBuildPath (GameCI passes it) or Build/Android/kalyeah-unity.apk.
///   KalyeahBuild.Screenshots   Setup, then the title and the match from fixed shoulder-camera spots, 1280 x 720 PNGs in Build/screenshots.
///                              Needs a graphics device: run without -nographics (GameCI's editor image wraps Unity in xvfb-run).
/// Run tools/unity_export/sync_assets.sh first (the art, the data and the sim come from there).
/// </summary>
public static class KalyeahBuild
{
    public const string PackageId = "com.kalyeah.game";
    public const string ProductName = "Kalyeah";
    /// <summary>Android SDK floor (24 on Unity 6.0 LTS / GameCI; 26 on Unity 6.3+ where the Editor enforces API 26+) and target 35.</summary>
#if UNITY_6000_3_OR_NEWER
    const int MinSdk = 26, TargetSdk = 35;
#else
    const int MinSdk = 24, TargetSdk = 35;
#endif

    const string Generated = "Assets/Kalyeah/Generated";
    const string MaterialsDir = Generated + "/Resources/KalyeahMaterials";
    const string ScenesDir = Generated + "/Scenes";
    const string RenderingDir = Generated + "/Rendering";
    const string PipelinePath = RenderingDir + "/KalyeahURP.asset";
    const string RendererPath = RenderingDir + "/KalyeahURP_Renderer.asset";
    const string PostProcessPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
    const string TitleScenePath = ScenesDir + "/" + TitleScreen.SceneName + ".unity";
    const string MatchScenePath = ScenesDir + "/" + MatchBoot.SceneName + ".unity";
    const string ModelsRoot = "Assets/Imported/Resources/models";
    const string CharactersRoot = ModelsRoot + "/characters";
    const string AppIconDir = "Assets/Imported/AppIcon";
    const string ImportedDataDir = "Assets/Imported/Resources/Data";
    const string BuildStampAssetPath = ImportedDataDir + "/build_stamp.txt";
    const int ShotWidth = 1280, ShotHeight = 720;
    const int NarrowWidth = 844, NarrowHeight = 390;
    const int UltraWidth = 934, UltraHeight = 400;
    const int FoldWidth = 1024, FoldHeight = 768;

    static readonly string[] GltfShaderGraphPaths =
    {
        "Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-pbrMetallicRoughness.shadergraph",
        "Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-pbrSpecularGlossiness.shadergraph",
        "Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-unlit.shadergraph",
    };

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    // ---- Setup ------------------------------------------------------------------------------------------------------------------

    [MenuItem("Kalyeah/Setup Project")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(ModelsRoot))
            Debug.LogWarning("KalyeahBuild: Assets/Imported is empty. Run tools/unity_export/sync_assets.sh, then open the project again.");
        EnsureFolder(MaterialsDir);
        EnsureFolder(ScenesDir);
        EnsureFolder(RenderingDir);
        ApplyPlayerSettings();
        EnsurePipeline();
        PrepareGltfForUrp();
        EnsureMaterials();
        FixModelImports();
        EnsureScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("KalyeahBuild: setup done");
    }

    static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = ProductName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
        StampVersionAndBuildInfo();

        // one APK, arm64-v8a only (IL2CPP is the only backend that builds arm64)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
        PlayerSettings.Android.forceInternetPermission = true;   // Online and Hotspot open sockets (KalyeahAndroidManifest adds the rest)
        PlayerSettings.Android.forceSDCardPermission = false;
        EditorUserBuildSettings.buildAppBundle = false;

        // Android immersive fullscreen + edge-to-edge safe-area rendering (SafeArea.cs handles cutouts and gesture bars)
        PlayerSettings.statusBarHidden = true;
#pragma warning disable CS0618
        PlayerSettings.Android.startInFullscreen = true;
#pragma warning restore CS0618
        PlayerSettings.Android.renderOutsideSafeArea = true;

        // landscape only, either way round (the phone may be turned half way): no portrait at all
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
#pragma warning disable CS0618
        PlayerSettings.useAnimatedAutorotation = true;
#pragma warning restore CS0618
        // the 1280 x 720 landscape base (the HUD's CanvasScaler uses the same reference)
        PlayerSettings.defaultScreenWidth = (int)Ui.Base.x;
        PlayerSettings.defaultScreenHeight = (int)Ui.Base.y;

        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);

        ConfigureAppIconsAndSplash();

        // Input System for the touches (and Android Back); "Both" keeps any old-Input code from throwing
        Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
        if (playerSettings != null)
        {
            var serialized = new SerializedObject(playerSettings);
            SerializedProperty handler = serialized.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 2)
            {
                handler.intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static void StampVersionAndBuildInfo()
    {
        string versionFile = Path.Combine(ProjectRoot, "..", "..", "version.txt");
        string baseVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "0.1.0";
        if (string.IsNullOrEmpty(baseVersion)) baseVersion = "0.1.0";

        string sha = Environment.GetEnvironmentVariable("GITHUB_SHA")?.Trim() ?? "";
        if (sha.Length > 7) sha = sha.Substring(0, 7);
        PlayerSettings.bundleVersion = sha.Length > 0 ? $"{baseVersion}+{sha}" : baseVersion;

        string runNumberRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER")?.Trim();
        if (string.IsNullOrEmpty(runNumberRaw))
            runNumberRaw = Environment.GetEnvironmentVariable("KALYEAH_VERSION_CODE")?.Trim();
        if (!string.IsNullOrEmpty(runNumberRaw) && int.TryParse(runNumberRaw, out int versionCode) && versionCode > 0)
            PlayerSettings.Android.bundleVersionCode = versionCode;
        else if (PlayerSettings.Android.bundleVersionCode < 1)
            PlayerSettings.Android.bundleVersionCode = 1;

        string rawServerUrl = Arg("-serverUrl");
        if (string.IsNullOrWhiteSpace(rawServerUrl))
            rawServerUrl = Environment.GetEnvironmentVariable("KALYEAH_SERVER_URL");
        if (string.IsNullOrWhiteSpace(rawServerUrl))
            rawServerUrl = Environment.GetEnvironmentVariable("BAYABAS_SERVER_URL");

        string normalizedServerUrl = SettingsStore.NormalizeServerUrl(rawServerUrl ?? "");
        string dataDirDisk = Path.Combine(ProjectRoot, ImportedDataDir);
        if (Directory.Exists(dataDirDisk))
        {
            string stampDiskPath = Path.Combine(ProjectRoot, BuildStampAssetPath);
            if (string.IsNullOrEmpty(normalizedServerUrl))
            {
                if (File.Exists(stampDiskPath))
                    AssetDatabase.DeleteAsset(BuildStampAssetPath);
            }
            else
            {
                string content = normalizedServerUrl + "\n";
                if (!File.Exists(stampDiskPath) || File.ReadAllText(stampDiskPath) != content)
                {
                    File.WriteAllBytes(stampDiskPath, System.Text.Encoding.UTF8.GetBytes(content));
                    AssetDatabase.ImportAsset(BuildStampAssetPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }
    }

    static void ConfigureAppIconsAndSplash()
    {
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Color.black;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;

        if (!AssetDatabase.IsValidFolder(AppIconDir))
            return;

        Texture2D adaptiveBg = LoadAppIconTexture(AppIconDir + "/adaptive_background.png", asSprite: false);
        Texture2D adaptiveFg = LoadAppIconTexture(AppIconDir + "/adaptive_foreground.png", asSprite: false);
        Texture2D main192 = LoadAppIconTexture(AppIconDir + "/main_192.png", asSprite: false);
        Texture2D legacyIcon = LoadAppIconTexture(AppIconDir + "/icon.png", asSprite: false);
        Texture2D fallbackIcon = main192 != null ? main192 : legacyIcon;

        if (fallbackIcon != null)
        {
            int[] unknownSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Application);
            int count = unknownSizes != null && unknownSizes.Length > 0 ? unknownSizes.Length : 1;
            var fallbackArray = new Texture2D[count];
            for (int i = 0; i < count; i++) fallbackArray[i] = fallbackIcon;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, fallbackArray, IconKind.Application);
        }

        PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android);
        if (kinds != null)
        {
            foreach (PlatformIconKind kind in kinds)
            {
                string kindName = kind.ToString() ?? "";
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (slots == null || slots.Length == 0) continue;

                if (kindName.IndexOf("Adaptive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (adaptiveBg == null || adaptiveFg == null) continue;
                    foreach (PlatformIcon slot in slots)
                    {
                        if (slot.maxLayerCount >= 2)
                        {
                            slot.SetTexture(adaptiveBg, 0);
                            slot.SetTexture(adaptiveFg, 1);
                        }
                    }
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
                }
                else if (fallbackIcon != null)
                {
                    foreach (PlatformIcon slot in slots)
                    {
                        if (slot.maxLayerCount >= 1)
                            slot.SetTexture(fallbackIcon, 0);
                    }
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
                }
            }
        }

        string splashPath = AppIconDir + "/boot_splash.png";
        LoadAppIconTexture(splashPath, asSprite: true);
        Sprite splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(splashPath);
        if (splashSprite != null)
        {
            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(2.0f, splashSprite),
            };
        }
    }

    static Texture2D LoadAppIconTexture(string assetPath, bool asSprite)
    {
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            return null;

        if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti)
        {
            bool dirty = false;
            TextureImporterType desiredType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (ti.textureType != desiredType) { ti.textureType = desiredType; dirty = true; }
            if (asSprite && ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    /// <summary>A URP asset and its renderer, made once and set as the project's pipeline for every quality level.</summary>
    static void EnsurePipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            var post = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessPath);
            if (post != null) renderer.postProcessData = post;
            AssetDatabase.CreateAsset(renderer, RendererPath);
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;     // phones: LDR, 2x MSAA, shadows over the street's length
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 90f;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int current = QualitySettings.GetQualityLevel();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);
    }

    /// <summary>
    /// On a cold checkout (ProjectSettings/* ignored), Unity imports .glb files before EnsurePipeline sets URP, so glTFast latches
    /// RenderPipeline.BuiltIn and BuiltInMaterialGenerator in private static fields. Right after EnsurePipeline sets KalyeahURP,
    /// synchronously recompile glTFast's ShaderGraphs for URP and reset glTFast's static caches so FixModelImports generates URP PBR materials.
    /// </summary>
    static void PrepareGltfForUrp()
    {
        foreach (string graphPath in GltfShaderGraphPaths)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(graphPath)))
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type rpUtils = asm.GetType("GLTFast.RenderPipelineUtils", false);
            if (rpUtils != null)
            {
                FieldInfo rpField = rpUtils.GetField("s_RenderPipeline", StaticAny);
                if (rpField != null)
                    rpField.SetValue(null, rpField.FieldType.IsEnum ? Enum.ToObject(rpField.FieldType, 0) : 0);
            }

            ResetStaticGeneratorFields(asm.GetType("GLTFast.Materials.MaterialGenerator", false), StaticAny);
            ResetStaticGeneratorFields(asm.GetType("GLTFast.Materials.BuiltInMaterialGenerator", false), StaticAny);

            Type sgGen = asm.GetType("GLTFast.Materials.ShaderGraphMaterialGenerator", false);
            if (sgGen != null)
            {
                SetStaticField(sgGen, "s_MetallicShader", null, StaticAny);
                SetStaticField(sgGen, "s_SpecularShader", null, StaticAny);
                SetStaticField(sgGen, "s_UnlitShader", null, StaticAny);
                SetStaticField(sgGen, "s_MetallicShaderQueried", false, StaticAny);
                SetStaticField(sgGen, "s_SpecularShaderQueried", false, StaticAny);
                SetStaticField(sgGen, "s_UnlitShaderQueried", false, StaticAny);
                ClearStaticDictionary(sgGen, "s_MetallicShaders", StaticAny);
                ClearStaticDictionary(sgGen, "s_SpecularShaders", StaticAny);
                ClearStaticDictionary(sgGen, "s_UnlitShaders", StaticAny);
            }
        }
    }

    static void ResetStaticGeneratorFields(Type type, BindingFlags flags)
    {
        if (type == null) return;
        SetStaticField(type, "s_DefaultMaterialGenerator", null, flags);
        SetStaticField(type, "s_DefaultMaterialGenerated", false, flags);
        SetStaticField(type, "s_DefaultMaterial", null, flags);
    }

    static void SetStaticField(Type type, string fieldName, object value, BindingFlags flags)
    {
        FieldInfo field = type.GetField(fieldName, flags);
        if (field != null && !field.IsInitOnly)
            field.SetValue(null, value);
    }

    static void ClearStaticDictionary(Type type, string fieldName, BindingFlags flags)
    {
        FieldInfo field = type.GetField(fieldName, flags);
        if (field?.GetValue(null) is IDictionary dict)
            dict.Clear();
    }

    /// <summary>The materials the code makes things with (KalyeahResources.Material), saved so their shaders go into the IL2CPP build.</summary>
    static void EnsureMaterials()
    {
        EnsureMaterial("Ground", "Universal Render Pipeline/Lit", m =>
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.08f);
        });
        EnsureMaterial("Unlit", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", Color.white));
        EnsureMaterial("Sky", "Skybox/Procedural", m =>
        {
            m.SetFloat("_SunSize", 0.04f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.52f, 0.68f, 0.92f));
            m.SetColor("_GroundColor", new Color(0.62f, 0.58f, 0.52f));
            m.SetFloat("_Exposure", 1.25f);
        });
        EnsureMaterial("BarangayGround", "Kalyeah/BarangayGround");
        EnsureMaterial("EsteroWater", "Kalyeah/EsteroWater");
        EnsureMaterial("TropicalSky", "Kalyeah/TropicalSky");
        EnsureMaterial("TeamOutline", "Kalyeah/TeamOutline", m =>
        {
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.20f, 0.50f, 1.00f, 1f));
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.024f);
        });
        EnsureMaterial("ArcaneFx", "Kalyeah/ArcaneFx", m => m.enableInstancing = true);
        EnsureMaterial("TransparentUnlit", "Universal Render Pipeline/Unlit", m =>
        {
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        });
        EnsureMaterial("GltfPbr", "Shader Graphs/glTF-pbrMetallicRoughness", _ => { }, optional: true);
    }

    static void EnsureMaterial(string name, string shaderName, Action<Material> configure = null, bool optional = false)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            if (optional) Debug.LogWarning($"KalyeahBuild: optional shader {shaderName} not found");
            else Debug.LogError($"KalyeahBuild: shader {shaderName} not found (is URP installed?)");
            return;
        }
        string path = $"{MaterialsDir}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            if (existing.shader != shader || !existing.shader.isSupported)
            {
                existing.shader = shader;
                configure?.Invoke(existing);
                EditorUtility.SetDirty(existing);
            }
            return;
        }
        var material = new Material(shader) { name = name };
        configure?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
    }

    /// <summary>
    /// glTFast imported the .glb files when the project opened, before this set URP: their materials then use glTFast's built-in
    /// shaders ("glTF/..."), pink under URP. Those are imported again now (URP picks glTFast's shader graphs). The kids' clips are
    /// imported as legacy Animation clips (KidView plays them with the Animation component).
    /// </summary>
    static void FixModelImports()
    {
        if (!AssetDatabase.IsValidFolder(ModelsRoot)) return;
        var again = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("", new[] { ModelsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || again.Contains(path)) continue;
            bool legacy = path.StartsWith(CharactersRoot, StringComparison.Ordinal) && UseLegacyAnimation(path);
            bool brokenMaterial = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()
                .Any(m => m == null
                    || m.shader == null
                    || !m.shader.isSupported
                    || m.shader.name.StartsWith("glTF/", StringComparison.Ordinal)
                    || m.shader.name == "Standard"
                    || m.shader.name.Contains("InternalErrorShader"));
            if (legacy || brokenMaterial) again.Add(path);
        }
        if (again.Count == 0) return;
        Debug.Log($"KalyeahBuild: importing {again.Count} models again for URP / legacy clips");
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in again)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    /// <summary>Sets glTFast's importer to legacy animation clips (found by name, so it does not depend on its field path).</summary>
    static bool UseLegacyAnimation(string path)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null) return false;
        var serialized = new SerializedObject(importer);
        SerializedProperty property = serialized.GetIterator();
        bool changed = false;
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.Enum) continue;
            if (property.name.IndexOf("animationMethod", StringComparison.OrdinalIgnoreCase) < 0) continue;
            int legacy = Array.IndexOf(property.enumNames, "Legacy");
            if (legacy >= 0 && property.enumValueIndex != legacy)
            {
                property.enumValueIndex = legacy;
                changed = true;
            }
        }
        if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    /// <summary>Two scenes, each one GameObject with the component that builds everything in code; the title opens the app.</summary>
    static void EnsureScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        WriteScene(TitleScenePath, TitleScreen.SceneName, typeof(TitleScreen));
        WriteScene(MatchScenePath, MatchBoot.SceneName, typeof(MatchBoot));
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(MatchScenePath, true),
        };
    }

    static void WriteScene(string path, string rootName, Type component)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(rootName, component);
        root.transform.position = Vector3.zero;
        EditorSceneManager.SaveScene(scene, path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---- BuildAndroid -----------------------------------------------------------------------------------------------------------

    public static void BuildAndroid()
    {
        Setup();
        ConfigureAndroidKeystoreFromEnv();
        string abiOverride = Arg("-androidAbi") ?? Environment.GetEnvironmentVariable("KALYEAH_ANDROID_ABI");
        if (string.Equals(abiOverride?.Trim(), "x86_64", StringComparison.OrdinalIgnoreCase))
        {
#pragma warning disable CS0618
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
#pragma warning restore CS0618
        }
        else
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        string path = Arg("-customBuildPath") ?? Path.Combine(ProjectRoot, "Build", "Android", "kalyeah-unity.apk");
        if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "kalyeah-unity.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { TitleScenePath, MatchScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"KalyeahBuild: {summary.result} {path} {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}");
        if (summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Android build {summary.result}: {summary.totalErrors} errors (see the log above)");
    }

    static void ConfigureAndroidKeystoreFromEnv()
    {
        string keystoreB64 = Environment.GetEnvironmentVariable("KALYEAH_DEBUG_KEYSTORE_B64")?.Trim();
        string keystorePathEnv = Environment.GetEnvironmentVariable("KALYEAH_ANDROID_KEYSTORE_PATH")?.Trim();

        string keystorePath = null;
        if (!string.IsNullOrEmpty(keystoreB64))
        {
            byte[] decoded = Convert.FromBase64String(keystoreB64);
            keystorePath = Path.Combine(Path.GetTempPath(), "kalyeah-debug.keystore");
            File.WriteAllBytes(keystorePath, decoded);
        }
        else if (!string.IsNullOrEmpty(keystorePathEnv) && File.Exists(keystorePathEnv))
        {
            keystorePath = keystorePathEnv;
        }

        if (!string.IsNullOrEmpty(keystorePath))
        {
            string storePass = Environment.GetEnvironmentVariable("KALYEAH_ANDROID_KEYSTORE_PASS") ?? "android";
            string aliasName = Environment.GetEnvironmentVariable("KALYEAH_ANDROID_KEYALIAS_NAME") ?? "androiddebugkey";
            string aliasPass = Environment.GetEnvironmentVariable("KALYEAH_ANDROID_KEYALIAS_PASS") ?? storePass;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = aliasPass;
        }
    }

    // ---- Screenshots ------------------------------------------------------------------------------------------------------------

    /// <summary>A fixed shoulder-camera spot: your kid's feet (sim XZ), where the view looks (yaw 0 = toward the enemy end), its pitch.</summary>
    readonly struct Spot
    {
        public readonly string Name;
        public readonly System.Numerics.Vector2 Feet;
        public readonly float Yaw, Pitch;

        public Spot(string name, float x, float z, float yaw, float pitch)
        {
            Name = name;
            Feet = new System.Numerics.Vector2(x, z);
            Yaw = yaw;
            Pitch = pitch;
        }
    }

    static readonly Spot[] Spots =
    {
        new Spot("match_03_midfield", 0f, 10f, 0f, 9.5f),
        new Spot("match_04_west_alley", -30f, 2f, 270f, 9.5f),
        new Spot("match_05_enemy_post", 0f, -48f, 0f, 4f),
        new Spot("match_06_own_base_behind", 0f, 44f, 180f, 12f),
        new Spot("match_07_high_look", 20f, 20f, 315f, -14f),
    };

    public static void Screenshots()
    {
        Setup();
        bool asyncShaders = EditorSettings.asyncShaderCompilation;
        bool prevAsync = ShaderUtil.allowAsyncCompilation;
        EditorSettings.asyncShaderCompilation = false; // no cyan placeholder shaders in a still
        ShaderUtil.allowAsyncCompilation = false;
        string dir = Path.Combine(ProjectRoot, "Build", "screenshots");
        Directory.CreateDirectory(dir);
        DataSource data = KalyeahResources.Data();
        try
        {
            CaptureTitleShot(dir, "title.png", ShotWidth, ShotHeight, new Rect(0f, 0f, ShotWidth, ShotHeight));
            CaptureTitleShot(
                dir,
                $"title_{NarrowWidth}x{NarrowHeight}.png",
                NarrowWidth,
                NarrowHeight,
                ScreenFitMetrics.AsymmetricCutoutSafeRect(NarrowWidth, NarrowHeight));

            SetFullViewport();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pickRoot = new GameObject("PickShot").transform;
            var pickCamera = new GameObject("PickCamera", typeof(Camera)).GetComponent<Camera>();
            pickCamera.transform.SetParent(pickRoot, false);
            pickCamera.clearFlags = CameraClearFlags.SolidColor;
            pickCamera.backgroundColor = Ui.Navy;
            pickCamera.orthographic = true;
            Canvas pickCanvas = Ui.MakeCanvas("PickCanvas", pickRoot, pickCamera);
            var pickView = new PickScreenView(pickCanvas.transform, data, null);
            if (pickView.SafeRoot.GetComponent<SafeArea>() == null)
                pickView.SafeRoot.gameObject.AddComponent<SafeArea>().ForceApply();
            CaptureQuadAspect(pickCamera, dir, "menu_pick", pickView.RefreshResponsiveLayout);

            SetFullViewport();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MatchShot").transform;
            using (var world = new MatchWorld(root, data, 7, "junjun", interactive: false, hudCamera: true))
            {
                world.Sim.SkipIntro();
                world.Draw(1f, 0f);
                CaptureQuadAspect(world.Camera, dir, "match_01_spawn", () => world.Draw(1f, 0f));

                // 75 ticks (2.5 s) running forward while firing primary: bots spread out, weapon & HUD fire active
                float dt = world.Sim.TickDt;
                for (int i = 0; i < 75; i++)
                {
                    System.Numerics.Vector2 move = world.Shoulder.GroundWay(Vector2.up);
                    world.Sim.SetLocalInput(move, move, InputButtons.Primary);
                    world.StepTick();
                    world.Draw(1f, dt);
                }
                CaptureDualAspect(world.Camera, dir, "match_02_running", () => world.Draw(1f, 0f));

                SetFullViewport();
                foreach (Spot spot in Spots)
                {
                    (System.Numerics.Vector2 _, System.Numerics.Vector2 down) = GodotSpace.GroundAxes(spot.Yaw);
                    world.Sim.SetPlayerPositionForPreview(SimBridge.LocalId, spot.Feet, -down);
                    world.Draw(1f, 0f);
                    world.Shoulder.Yaw = spot.Yaw;
                    world.Shoulder.Pitch = spot.Pitch;
                    world.Shoulder.Apply(world.Camera, spot.Feet, 0f);
                    world.Hud.Refresh(world.Sim, world.Camera, world.Shoulder, world.View.Layout, 0f);
                    Capture(world.Camera, Path.Combine(dir, spot.Name + ".png"), ShotWidth, ShotHeight, () =>
                    {
                        world.Shoulder.Yaw = spot.Yaw;
                        world.Shoulder.Pitch = spot.Pitch;
                        world.Shoulder.Apply(world.Camera, spot.Feet, 0f);
                        world.Hud.Refresh(world.Sim, world.Camera, world.Shoulder, world.View.Layout, 0f);
                    });
                }
            }

            long tumbangSeed = FindTumbangThrowerSeed(data);
            CaptureStreetShot(data, dir, StreetGameKind.Patintero, 11, "street_patintero", dualAspect: true, brownoutPreview: false);
            CaptureStreetShot(data, dir, StreetGameKind.TumbangPreso, tumbangSeed, "street_tumbang_preso", dualAspect: true, brownoutPreview: false);
            CaptureStreetShot(data, dir, StreetGameKind.LangitLupa, 23, "street_langit_lupa", dualAspect: true, brownoutPreview: true);

            Debug.Log($"KalyeahBuild: screenshots in {dir}");
        }
        finally
        {
            ScreenFitMetrics.ClearSimulatedViewport();
            ShaderUtil.allowAsyncCompilation = prevAsync;
            EditorSettings.asyncShaderCompilation = asyncShaders;
        }
    }

    static long FindTumbangThrowerSeed(DataSource data)
    {
        for (long seed = 1; seed <= 30; seed++)
        {
            var probeRoot = new GameObject("TumbangSeedProbe");
            try
            {
                using var probe = new StreetMatchWorld(probeRoot.transform, data, StreetGameKind.TumbangPreso, seed, "junjun", interactive: false, hudCamera: false);
                probe.Sim.SkipIntro();
                for (int w = 0; w < 150 && probe.Sim.IsIntro; w++)
                    probe.StepTick();
                if (probe.Sim.LocalKid.HasValue && probe.Sim.LocalKid.Value.Role != StreetRoleKind.Taya)
                    return seed;
            }
            finally
            {
                Object.DestroyImmediate(probeRoot);
            }
        }
        return 1;
    }

    static void CaptureTitleShot(string dir, string fileName, int width, int height, Rect safeRect)
    {
        ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(width, height), safeRect);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var titleRoot = new GameObject("TitleShot").transform;
        var titleCamera = new GameObject("TitleCamera", typeof(Camera)).GetComponent<Camera>();
        titleCamera.transform.SetParent(titleRoot, false);
        TitleScreen.Build(titleRoot, titleCamera, null);
        Capture(titleCamera, Path.Combine(dir, fileName), width, height);
    }

    static void CaptureStreetShot(
        DataSource data,
        string dir,
        StreetGameKind kind,
        long seed,
        string stem,
        bool dualAspect,
        bool brownoutPreview)
    {
        SetFullViewport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var streetRoot = new GameObject("StreetShot_" + kind).transform;
        using var street = new StreetMatchWorld(streetRoot, data, kind, seed, "junjun", interactive: false, hudCamera: true);
        street.Sim.SkipIntro();
        for (int w = 0; w < 150 && street.Sim.IsIntro; w++)
            street.StepTick();

        float dt = street.Sim.TickDt;
        int ticks = kind == StreetGameKind.TumbangPreso ? 35 : 45;
        for (int i = 0; i < ticks; i++)
        {
            if (kind == StreetGameKind.TumbangPreso)
            {
                System.Numerics.Vector2 pos = street.Sim.LocalKid.HasValue ? street.Sim.LocalKid.Value.Position : System.Numerics.Vector2.Zero;
                System.Numerics.Vector2 toCan = street.Sim.TumbangPresoState.CanPosition - pos;
                System.Numerics.Vector2 aim = toCan.LengthSquared() > 0.001f
                    ? System.Numerics.Vector2.Normalize(toCan)
                    : street.Shoulder.GroundWay(Vector2.up);
                if (i == 0)
                    street.Shoulder.Yaw = ShoulderRig.YawToward(pos, street.Sim.TumbangPresoState.CanPosition);
                System.Numerics.Vector2 stepTowardToe = i < 16 ? aim : System.Numerics.Vector2.Zero;
                street.Sim.SetLocalInput(stepTowardToe, aim, primaryHeld: i >= 16 && i < 31, secondaryPressed: false, cancelAim: false);
            }
            else
            {
                Vector2 stick = kind == StreetGameKind.LangitLupa ? new Vector2(0.25f, 1f) : Vector2.up;
                System.Numerics.Vector2 move = street.Shoulder.GroundWay(stick);
                street.Sim.SetLocalInput(move, move, primaryHeld: false, secondaryPressed: kind == StreetGameKind.Patintero && i == 25, cancelAim: false);
            }
            street.StepTick();
            street.Draw(1f, dt);
        }

        if (brownoutPreview)
            street.SetBrownoutPreview(true);

        if (dualAspect)
            CaptureDualAspect(street.Camera, dir, stem, () => street.Draw(1f, 0f));
        else
            Capture(street.Camera, Path.Combine(dir, stem + ".png"), ShotWidth, ShotHeight, () => street.Draw(1f, 0f));
    }

    static void SetFullViewport() =>
        ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(ShotWidth, ShotHeight), new Rect(0f, 0f, ShotWidth, ShotHeight));

    static void SetNotchedViewport() =>
        ScreenFitMetrics.SetSimulatedViewport(
            new Vector2Int(NarrowWidth, NarrowHeight),
            ScreenFitMetrics.AsymmetricCutoutSafeRect(NarrowWidth, NarrowHeight));

    static void CaptureDualAspect(Camera camera, string dir, string stem, Action onViewportReflow = null)
    {
        SetFullViewport();
        Capture(camera, Path.Combine(dir, stem + ".png"), ShotWidth, ShotHeight, onViewportReflow);
        SetNotchedViewport();
        Capture(camera, Path.Combine(dir, $"{stem}_{NarrowWidth}x{NarrowHeight}.png"), NarrowWidth, NarrowHeight, onViewportReflow);
        SetFullViewport();
    }

    static void CaptureQuadAspect(Camera camera, string dir, string stem, Action onViewportReflow = null)
    {
        CaptureDualAspect(camera, dir, stem, onViewportReflow);
        ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(UltraWidth, UltraHeight), new Rect(0f, 0f, UltraWidth, UltraHeight));
        Capture(camera, Path.Combine(dir, $"{stem}_{UltraWidth}x{UltraHeight}.png"), UltraWidth, UltraHeight, onViewportReflow);
        ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(FoldWidth, FoldHeight), new Rect(0f, 0f, FoldWidth, FoldHeight));
        Capture(camera, Path.Combine(dir, $"{stem}_{FoldWidth}x{FoldHeight}.png"), FoldWidth, FoldHeight, onViewportReflow);
        SetFullViewport();
    }

    /// <summary>Renders `camera` into a `width` x `height` texture (reflowing SafeAreas and Canvases first) and writes it as a PNG.</summary>
    static void Capture(Camera camera, string file, int width = ShotWidth, int height = ShotHeight, Action onViewportReflow = null)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        target.Create();
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.aspect = (float)width / height;
        Canvas.ForceUpdateCanvases();
        if (camera.gameObject.scene.IsValid())
        {
            GameObject[] roots = camera.gameObject.scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                SafeArea[] safeAreas = roots[r].GetComponentsInChildren<SafeArea>(true);
                for (int i = 0; i < safeAreas.Length; i++)
                    safeAreas[i].ForceApply();
            }
        }
        onViewportReflow?.Invoke();
        Canvas.ForceUpdateCanvases();

        bool prevAllowAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();

            if (ShaderUtil.anythingCompiling)
            {
                for (int w = 0; w < 200 && ShaderUtil.anythingCompiling; w++)
                    System.Threading.Thread.Sleep(25);
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
            }
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = prevAllowAsync;
        }

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previousActive;
        camera.targetTexture = previousTarget;
        camera.ResetAspect();
        File.WriteAllBytes(file, pixels.EncodeToPNG());
        Object.DestroyImmediate(pixels);
        target.Release();
        Object.DestroyImmediate(target);
        Debug.Log($"KalyeahBuild: wrote {file}");
    }

    static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
