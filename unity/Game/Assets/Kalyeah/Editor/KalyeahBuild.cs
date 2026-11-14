using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>The same SDKs as the Godot game's APK (export_presets.cfg / the Android APK workflow: min 24, target android-35).</summary>
    const int MinSdk = 24, TargetSdk = 35;

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
    const int ShotWidth = 1280, ShotHeight = 720;

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
        string version = Path.Combine(ProjectRoot, "..", "..", "version.txt");
        if (File.Exists(version)) PlayerSettings.bundleVersion = File.ReadAllText(version).Trim();

        // one APK, arm64-v8a only (IL2CPP is the only backend that builds arm64)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
        PlayerSettings.Android.forceInternetPermission = true;   // Online and Hotspot open sockets (KalyeahAndroidManifest adds the rest)
        PlayerSettings.Android.forceSDCardPermission = false;
        EditorUserBuildSettings.buildAppBundle = false;

        // landscape only, either way round (the phone may be turned half way): no portrait at all
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.useAnimatedAutorotation = true;
        // the 1280 x 720 landscape base (the HUD's CanvasScaler uses the same reference)
        PlayerSettings.defaultScreenWidth = (int)Ui.Base.x;
        PlayerSettings.defaultScreenHeight = (int)Ui.Base.y;

        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);

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

    /// <summary>The materials the code makes things with (KalyeahResources.Material), saved so their shaders go into the build.</summary>
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
    }

    static void EnsureMaterial(string name, string shaderName, Action<Material> configure)
    {
        string path = $"{MaterialsDir}/{name}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"KalyeahBuild: shader {shaderName} not found (is URP installed?)");
            return;
        }
        var material = new Material(shader) { name = name };
        configure(material);
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
            bool builtIn = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()
                .Any(m => m.shader == null || m.shader.name.StartsWith("glTF/", StringComparison.Ordinal) || m.shader.name.Contains("InternalErrorShader"));
            if (legacy || builtIn) again.Add(path);
        }
        if (again.Count == 0) return;
        Debug.Log($"KalyeahBuild: importing {again.Count} models again for URP / legacy clips");
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in again) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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
        new Spot("match_07_high_look", 20f, 20f, 315f, 40f),
    };

    public static void Screenshots()
    {
        Setup();
        bool asyncShaders = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false; // no cyan placeholder shaders in a still
        string dir = Path.Combine(ProjectRoot, "Build", "screenshots");
        Directory.CreateDirectory(dir);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var titleRoot = new GameObject("TitleShot").transform;
            var titleCamera = new GameObject("TitleCamera", typeof(Camera)).GetComponent<Camera>();
            titleCamera.transform.SetParent(titleRoot, false);
            TitleScreen.Build(titleRoot, titleCamera, null);
            Capture(titleCamera, Path.Combine(dir, "title.png"));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MatchShot").transform;
            var world = new MatchWorld(root, KalyeahResources.Data(), 7, "junjun", interactive: false, hudCamera: true);
            float dt = world.Sim.TickDt;
            Capture(world.Camera, Path.Combine(dir, "match_01_spawn.png"));

            // two seconds of running forward with the stick pushed up: the bots spread out, the kids run
            world.Sim.SetLocalInput(world.Shoulder.GroundWay(Vector2.up), 0);
            for (int i = 0; i < 60; i++)
            {
                world.StepTick();
                world.Draw(1f, dt);
            }
            Capture(world.Camera, Path.Combine(dir, "match_02_running.png"));

            foreach (Spot spot in Spots)
            {
                world.Shoulder.Yaw = spot.Yaw;
                world.Shoulder.Pitch = spot.Pitch;
                world.Shoulder.Apply(world.Camera, spot.Feet);
                Capture(world.Camera, Path.Combine(dir, spot.Name + ".png"));
            }
            Debug.Log($"KalyeahBuild: screenshots in {dir}");
        }
        finally
        {
            EditorSettings.asyncShaderCompilation = asyncShaders;
        }
    }

    /// <summary>Renders `camera` into a 1280 x 720 texture and writes it as a PNG.</summary>
    static void Capture(Camera camera, string file)
    {
        var target = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        target.Create();
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.aspect = (float)ShotWidth / ShotHeight;
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
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
