using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class BuildAndPlatformParityTests
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        static readonly string[] Expected12Permissions =
        {
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.ACCESS_WIFI_STATE",
            "android.permission.CAMERA",
            "android.permission.CHANGE_NETWORK_STATE",
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            "android.permission.CHANGE_WIFI_STATE",
            "android.permission.INTERNET",
            "android.permission.NEARBY_WIFI_DEVICES",
            "android.permission.NFC",
            "android.permission.VIBRATE",
        };

        static readonly string[] ExpectedOptionalFeatures =
        {
            "android.hardware.camera",
            "android.hardware.wifi",
            "android.hardware.location",
            "android.hardware.nfc",
            "android.hardware.nfc.hce",
        };

        readonly List<GameObject> _spawned = new List<GameObject>();
        string _savedSessionJson;
        string _savedSettingsJson;

        [SetUp]
        public void SetUp()
        {
            _savedSessionJson = AppSession.Current.ToJson();
            _savedSettingsJson = SettingsStore.Current.ToJson();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
            ScreenFitMetrics.ClearSimulatedViewport();

            SettingsStore.Current.ResetToDefaults();
            if (!string.IsNullOrEmpty(_savedSettingsJson))
                SettingsStore.Current.LoadJson(_savedSettingsJson);
            if (!string.IsNullOrEmpty(_savedSessionJson))
                AppSession.Current.LoadJson(_savedSessionJson);
        }

        static string RepoRoot()
        {
            DirectoryInfo gameDir = Directory.GetParent(Application.dataPath); // unity/Game
            DirectoryInfo unityDir = gameDir?.Parent; // unity
            return unityDir?.Parent != null ? unityDir.Parent.FullName : gameDir.FullName;
        }

        static Type FindEditorType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type t = assemblies[i].GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }

        static string ReadConst(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, type.Name + "." + name + " must exist");
            return (string)field.GetRawConstantValue();
        }

        [Test]
        public void AndroidPlayerSettings_AndBuildScenes_EnforceLandscapeArm64Il2CppContract()
        {
            Type buildType = FindEditorType("KalyeahBuild");
            Assert.IsNotNull(buildType, "KalyeahBuild editor script must compile in Kalyeah.Editor");

            MethodInfo applySettings = buildType.GetMethod(
                "ApplyPlayerSettings",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(applySettings, "KalyeahBuild.ApplyPlayerSettings must exist");
            applySettings.Invoke(null, null);

            Assert.AreEqual(
                "com.kalyeah.game",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                "Android package identifier must be com.kalyeah.game");
            Assert.AreEqual(
                ScriptingImplementation.IL2CPP,
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                "Android build must use IL2CPP");
            Assert.AreEqual(
                AndroidArchitecture.ARM64,
                PlayerSettings.Android.targetArchitectures,
                "Android APK must ship arm64-v8a only");

            int minSdk = (int)PlayerSettings.Android.minSdkVersion;
            int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            Assert.That(minSdk, Is.InRange(24, 26), "Android minSdkVersion must sit in [24..26] (Unity 6 clamps API 24 to supported floor)");
            Assert.GreaterOrEqual(targetSdk, 35, "Android targetSdkVersion must target API 35+");

            Assert.AreEqual(
                UIOrientation.AutoRotation,
                PlayerSettings.defaultInterfaceOrientation,
                "Sensor-landscape uses AutoRotation restricted to LandscapeLeft + LandscapeRight");
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft, "LandscapeLeft must be allowed");
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeRight, "LandscapeRight must be allowed");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait, "Portrait orientation must be locked off");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown, "UpsideDown portrait orientation must be locked off");

            Assert.AreEqual(1280, PlayerSettings.defaultScreenWidth);
            Assert.AreEqual(720, PlayerSettings.defaultScreenHeight);
            Assert.IsFalse(EditorUserBuildSettings.buildAppBundle, "Direct APK export keeps buildAppBundle disabled");

            // BuildAndroid / Screenshots pass these two paths explicitly; EditorBuildSettings is only written by
            // KalyeahBuild.Setup, which a fresh CI checkout (ProjectSettings/*.asset gitignored) has not run.
            string[] scenePaths = { ReadConst(buildType, "TitleScenePath"), ReadConst(buildType, "MatchScenePath") };
            Assert.IsTrue(scenePaths[0].EndsWith("/Title.unity", StringComparison.Ordinal), "Title scene path: " + scenePaths[0]);
            Assert.IsTrue(scenePaths[1].EndsWith("/Match.unity", StringComparison.Ordinal), "Match scene path: " + scenePaths[1]);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0)
            {
                foreach (string path in scenePaths)
                    Assert.IsTrue(scenes.Any(s => s.enabled && s.path == path), "EditorBuildSettings must register " + path);
            }
        }

        [Test]
        public void KalyeahAndroidManifest_DeclaresExact12PermissionsOptionalFeaturesAndAllowBackupFalse()
        {
            Type manifestType = FindEditorType("KalyeahAndroidManifest");
            if (manifestType != null)
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "kalyeah_manifest_test_" + Guid.NewGuid().ToString("N"));
                string srcMain = Path.Combine(tempDir, "src", "main");
                Directory.CreateDirectory(srcMain);
                string xmlPath = Path.Combine(srcMain, "AndroidManifest.xml");
                try
                {
                    File.WriteAllText(
                        xmlPath,
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                        "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.kalyeah.game\">\n" +
                        "  <application android:label=\"Kalyeah\" />\n" +
                        "</manifest>\n");

                    object instance = Activator.CreateInstance(manifestType);
                    MethodInfo postGen = manifestType.GetMethod(
                        "OnPostGenerateGradleAndroidProject",
                        BindingFlags.Instance | BindingFlags.Public);
                    Assert.IsNotNull(postGen);
                    postGen.Invoke(instance, new object[] { tempDir });

                    var doc = new XmlDocument();
                    doc.Load(xmlPath);
                    XmlElement root = doc.DocumentElement;
                    Assert.IsNotNull(root);

                    var foundPerms = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
                    var foundFeatures = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
                    foreach (XmlNode node in root.ChildNodes)
                    {
                        if (!(node is XmlElement el)) continue;
                        string name = el.GetAttribute("name", AndroidNs);
                        if (el.Name == "uses-permission" && !string.IsNullOrEmpty(name))
                            foundPerms[name] = el;
                        else if (el.Name == "uses-feature" && !string.IsNullOrEmpty(name))
                            foundFeatures[name] = el;
                    }

                    CollectionAssert.AreEquivalent(
                        Expected12Permissions,
                        foundPerms.Keys,
                        "Generated AndroidManifest.xml must declare the exact 12 Kalyeah permissions");
                    Assert.AreEqual(
                        "neverForLocation",
                        foundPerms["android.permission.NEARBY_WIFI_DEVICES"].GetAttribute("usesPermissionFlags", AndroidNs));
                    Assert.AreEqual(
                        "32",
                        foundPerms["android.permission.ACCESS_COARSE_LOCATION"].GetAttribute("maxSdkVersion", AndroidNs));
                    Assert.AreEqual(
                        "32",
                        foundPerms["android.permission.ACCESS_FINE_LOCATION"].GetAttribute("maxSdkVersion", AndroidNs));

                    for (int i = 0; i < ExpectedOptionalFeatures.Length; i++)
                    {
                        string feat = ExpectedOptionalFeatures[i];
                        Assert.IsTrue(foundFeatures.ContainsKey(feat), $"Missing optional <uses-feature> {feat}");
                        Assert.AreEqual("false", foundFeatures[feat].GetAttribute("required", AndroidNs), $"{feat} must be optional (required=false)");
                    }

                    XmlElement appEl = root["application"];
                    Assert.IsNotNull(appEl);
                    Assert.AreEqual("false", appEl.GetAttribute("allowBackup", AndroidNs), "android:allowBackup must be false");
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
            }

            // Also verify source KalyeahAndroidManifest.cs regardless of active Editor build target (#if UNITY_ANDROID)
            string csPath = Path.Combine(Application.dataPath, "Kalyeah", "Editor", "KalyeahAndroidManifest.cs");
            Assert.IsTrue(File.Exists(csPath), $"Missing {csPath}");
            string source = File.ReadAllText(csPath);
            for (int i = 0; i < Expected12Permissions.Length; i++)
                StringAssert.Contains(Expected12Permissions[i], source, $"KalyeahAndroidManifest.cs must declare {Expected12Permissions[i]}");
            for (int i = 0; i < ExpectedOptionalFeatures.Length; i++)
                StringAssert.Contains(ExpectedOptionalFeatures[i], source, $"KalyeahAndroidManifest.cs must declare optional {ExpectedOptionalFeatures[i]}");
            StringAssert.Contains("neverForLocation", source);
            StringAssert.Contains("allowBackup", source);
        }

        [Test]
        public void ApkInspectorGoldenFixtures_PassGoodDumpAndRejectEveryBrokenMutation()
        {
            string goldenDir = Path.Combine(RepoRoot(), "unity", "Golden", "Android");
            string badgingPath = Path.Combine(goldenDir, "badging.txt");
            string manifestPath = Path.Combine(goldenDir, "manifest.txt");
            Assert.IsTrue(File.Exists(badgingPath), $"Missing golden badging fixture at {badgingPath}");
            Assert.IsTrue(File.Exists(manifestPath), $"Missing golden manifest fixture at {manifestPath}");

            string badging = File.ReadAllText(badgingPath);
            string manifest = File.ReadAllText(manifestPath);

            var good = InspectApkDumps(badging, manifest, "0.7.0");
            Assert.IsTrue(good.Passed, good.Report);
            StringAssert.DoesNotContain("FAIL", good.Report);

            // Negative mutation checks matching test_inspect_apk.gd
            var highMinSdk = InspectApkDumps(badging, manifest.Replace("minSdkVersion(0x0101020c)=24", "minSdkVersion(0x0101020c)=28"), "0.7.0");
            Assert.IsFalse(highMinSdk.Passed);
            StringAssert.Contains("FAIL  min SDK is 24 or lower", highMinSdk.Report);

            var lowTargetSdk = InspectApkDumps(badging, manifest.Replace("targetSdkVersion(0x01010270)=35", "targetSdkVersion(0x01010270)=34"), "0.7.0");
            Assert.IsFalse(lowTargetSdk.Passed);
            StringAssert.Contains("FAIL  target SDK is 35 or higher", lowTargetSdk.Report);

            var noLauncher = InspectApkDumps(badging, manifest.Replace("android.intent.category.LAUNCHER", "android.intent.category.INFO"), "0.7.0");
            Assert.IsFalse(noLauncher.Passed);
            StringAssert.Contains("FAIL  it has a launcher activity", noLauncher.Report);

            var glEs2 = InspectApkDumps(badging, manifest.Replace("glEsVersion(0x01010281)=0x00030000", "glEsVersion(0x01010281)=0x00020000"), "0.7.0");
            Assert.IsFalse(glEs2.Passed);
            StringAssert.Contains("FAIL  it needs OpenGL ES 3.0", glEs2.Report);

            var portraitOri = InspectApkDumps(badging, manifest.Replace("screenOrientation(0x0101001e)=6", "screenOrientation(0x0101001e)=1"), "0.7.0");
            Assert.IsFalse(portraitOri.Passed);
            StringAssert.Contains("FAIL  the activity is locked to sensorLandscape", portraitOri.Report);

            var backupOn = InspectApkDumps(badging, manifest.Replace("allowBackup(0x01010280)=false", "allowBackup(0x01010280)=true"), "0.7.0");
            Assert.IsFalse(backupOn.Passed);
            StringAssert.Contains("FAIL  backups are off", backupOn.Report);

            string missingMulticast = Regex.Replace(badging, @"^uses-permission: name='android\.permission\.CHANGE_WIFI_MULTICAST_STATE'.*\r?\n", "", RegexOptions.Multiline);
            var noMulticast = InspectApkDumps(missingMulticast, manifest, "0.7.0");
            Assert.IsFalse(noMulticast.Passed);
            StringAssert.Contains("FAIL  only the permissions the game uses", noMulticast.Report);

            var extraAudio = InspectApkDumps(badging + "\nuses-permission: name='android.permission.RECORD_AUDIO'\n", manifest, "0.7.0");
            Assert.IsFalse(extraAudio.Passed);
            StringAssert.Contains("FAIL  only the permissions the game uses", extraAudio.Report);
        }

        static (bool Passed, string Report) InspectApkDumps(string badging, string manifest, string expectedVersion)
        {
            var lines = new List<string>(16);
            bool failed = false;

            void Check(string desc, bool ok)
            {
                if (ok) lines.Add("ok    " + desc);
                else { lines.Add("FAIL  " + desc); failed = true; }
            }

            int? AttrNum(string attrPattern)
            {
                Match m = Regex.Match(
                    manifest,
                    attrPattern + @"=(?:\(type 0x[0-9a-f]+\))?(0x[0-9a-f]+|[0-9]+)\s*$",
                    RegexOptions.Multiline);
                if (!m.Success) return null;
                string raw = m.Groups[1].Value;
                if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return int.Parse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return int.Parse(raw, CultureInfo.InvariantCulture);
            }

            Check("package is com.kalyeah.game", Regex.IsMatch(badging, @"^package: name='com\.kalyeah\.game' ", RegexOptions.Multiline));
            Check("version name starts with " + expectedVersion, badging.Contains("versionName='" + expectedVersion, StringComparison.Ordinal));

            int? minSdk = AttrNum(@"minSdkVersion\(0x0101020c\)");
            Check("min SDK is 24 or lower", minSdk.HasValue && minSdk.Value <= 24);

            int? targetSdk = AttrNum(@"targetSdkVersion\(0x01010270\)");
            Check("target SDK is 35 or higher", targetSdk.HasValue && targetSdk.Value >= 35);

            Check("it has a launcher activity", manifest.Contains("android.intent.category.LAUNCHER", StringComparison.Ordinal));

            int? glEs = AttrNum(@"glEsVersion\(0x01010281\)");
            Check("it needs OpenGL ES 3.0", glEs.HasValue && glEs.Value >= 0x30000);

            int? ori = AttrNum(@"screenOrientation\(0x0101001e\)");
            Check("the activity is locked to sensorLandscape", ori.HasValue && ori.Value == 6);

            Check("backups are off", Regex.IsMatch(manifest, @"allowBackup\(0x01010280\)=(?:false|\(type 0x12\)0x0)\s*$", RegexOptions.Multiline));

            string nativeLine = badging
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.StartsWith("native-code:", StringComparison.Ordinal)) ?? "";
            Check("arm64-v8a is the only native code", nativeLine == "native-code: 'arm64-v8a'");

            var declared = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(badging, @"^uses-permission: name='([^']+)'", RegexOptions.Multiline))
            {
                string p = m.Groups[1].Value;
                if (!p.EndsWith(".DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION", StringComparison.Ordinal))
                    declared.Add(p);
            }
            bool permsMatch = declared.SetEquals(Expected12Permissions);
            Check("only the permissions the game uses (see PERMISSIONS)", permsMatch);

            return (!failed, string.Join("\n", lines));
        }

        [Test]
        public void MenuStack_HandleBack_DismissesModalAndSubscreensBeforeAskingToQuit()
        {
            var hostGo = new GameObject("MenuStackTestCanvas");
            _spawned.Add(hostGo);
            Canvas canvas = Ui.MakeCanvas("Canvas", hostGo.transform, null);

            bool quitConfirmed = false;
            using (var stack = new MenuStack(canvas.transform, onQuitConfirmed: () => quitConfirmed = true))
            {
                RectTransform homeRoot = Ui.Fill("Home", canvas.transform);
                RectTransform settingsRoot = Ui.Fill("Settings", canvas.transform);
                bool subscreenOverrideOpen = false;
                RectTransform pickRoot = Ui.Fill("Pick", canvas.transform);

                stack.Register(MenuStack.HomeId, homeRoot);
                stack.Register(MenuStack.SettingsId, settingsRoot);
                stack.Register(
                    MenuStack.PickId,
                    pickRoot,
                    onShow: null,
                    onHide: null,
                    onBackOverride: () =>
                    {
                        if (!subscreenOverrideOpen) return false;
                        subscreenOverrideOpen = false;
                        return true;
                    });

                stack.Push(MenuStack.HomeId);
                Assert.AreEqual(MenuStack.HomeId, stack.CurrentId);
                Assert.IsFalse(stack.IsQuitModalOpen);

                // Back on Home opens "Quit Kalyeah?" modal instead of quitting
                Assert.IsTrue(stack.HandleBack());
                Assert.IsTrue(stack.IsQuitModalOpen, "First Back on Home opens Quit confirmation modal");
                Assert.IsFalse(quitConfirmed, "Back on Home must never silently quit");

                // Second Back dismisses Quit modal and keeps Home active
                Assert.IsTrue(stack.HandleBack());
                Assert.IsFalse(stack.IsQuitModalOpen, "Second Back dismisses Quit modal");
                Assert.AreEqual(MenuStack.HomeId, stack.CurrentId);
                Assert.IsFalse(quitConfirmed);

                // Subscreen + open Quit modal -> Back closes Quit modal first, then second Back pops subscreen to Home
                stack.Push(MenuStack.SettingsId);
                Assert.AreEqual(MenuStack.SettingsId, stack.CurrentId);
                stack.ShowQuitModal();
                Assert.IsTrue(stack.IsQuitModalOpen);

                Assert.IsTrue(stack.HandleBack());
                Assert.IsFalse(stack.IsQuitModalOpen, "Dialog on top of subscreen closes first");
                Assert.AreEqual(MenuStack.SettingsId, stack.CurrentId, "Subscreen remains beneath dismissed dialog");

                Assert.IsTrue(stack.HandleBack());
                Assert.AreEqual(MenuStack.HomeId, stack.CurrentId, "Next Back pops subscreen back to Home");

                // Subscreen with internal child modal (onBackOverride) consumes Back before popping
                stack.Push(MenuStack.PickId);
                subscreenOverrideOpen = true;
                Assert.IsTrue(stack.HandleBack());
                Assert.IsFalse(subscreenOverrideOpen, "Child modal override consumed Back");
                Assert.AreEqual(MenuStack.PickId, stack.CurrentId, "Pick screen stays open after closing child modal");

                Assert.IsTrue(stack.HandleBack());
                Assert.AreEqual(MenuStack.HomeId, stack.CurrentId, "Second Back pops Pick screen back to Home");
                Assert.IsFalse(quitConfirmed);
            }
        }

        [Test]
        public void MatchOverlaysView_HandleBack_ClosesChildModalsBeforeTogglingPauseSheet()
        {
            DataSource baseData = KalyeahResources.Data();
            DataSource tagData = new DataSource(name =>
            {
                string json = baseData.Text(name);
                if (name != "rules__game_rules") return json;
                json = Regex.Replace(json, "\"player_max_hp\"\\s*:\\s*[^,\\r\\n}]+", "\"player_max_hp\": 1");
                json = Regex.Replace(json, "\"practice_fighters\"\\s*:\\s*[^,\\r\\n}]+", "\"practice_fighters\": 2");
                json = Regex.Replace(json, "\"dummy_stand_x\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_stand_x\": 0.0");
                json = Regex.Replace(json, "\"dummy_patrol_x\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_patrol_x\": 2.0");
                json = Regex.Replace(json, "\"dummy_z\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_z\": 50.0");
                return json;
            });

            var sim = new SimBridge(tagData, new MatchSetup
            {
                Seed = 123L,
                LocalCharacter = "junjun",
                SuperpowerLoadout = new[] { "bato_light", "tsinelas_light", "papel_shield" },
                TrainingMode = true,
            });
            sim.SkipIntro();

            var hostGo = new GameObject("MatchOverlaysBackHost");
            _spawned.Add(hostGo);
            Canvas canvas = Ui.MakeCanvas("HudCanvas", hostGo.transform, null);
            RectTransform safe = Ui.Fill("Safe", canvas.transform);

            bool leftToHome = false;
            using (var overlays = new MatchOverlaysView(
                safe,
                sim,
                worldCamera: null,
                interactive: true,
                onPlayAgain: null,
                onBackToHome: () => leftToHome = true))
            {
                overlays.Refresh(sim, 0f);
                Assert.IsFalse(overlays.IsPaused);
                Assert.IsFalse(overlays.IsQuickChatOpen);

                // 1. Open Quick-Chat modal -> Back closes Quick-Chat first without opening Pause
                overlays.SetQuickChatOpen(true);
                Assert.IsTrue(overlays.IsQuickChatOpen);
                Assert.IsTrue(overlays.HandleBack());
                Assert.IsFalse(overlays.IsQuickChatOpen, "Back closes QuickChat modal first");
                Assert.IsFalse(overlays.IsPaused, "Closing QuickChat must not open Pause sheet");
                Assert.IsFalse(leftToHome);

                // 2. Tag local kid so D13 SwapPicker can open -> Back closes SwapPicker first without opening Pause
                sim.SetLocalInput(new LocalInputFrame { Move = new SVector2(0f, -1f) });
                for (int i = 0; i < 180 && !sim.LocalKid.Value.Tagged; i++)
                    sim.Step();
                Assert.IsTrue(sim.CanSwapLoadout, "Tagged kid on sidewalk can open D13 SwapPicker");
                overlays.Refresh(sim, 0f);

                overlays.SetSwapPickerOpen(true);
                Assert.IsTrue(overlays.IsSwapPickerOpen);
                Assert.IsTrue(overlays.HandleBack());
                Assert.IsFalse(overlays.IsSwapPickerOpen, "Back closes D13 SwapPicker modal first");
                Assert.IsFalse(overlays.IsPaused, "Closing SwapPicker must not open Pause sheet");
                Assert.IsFalse(leftToHome);

                // 3. With no transient modal open during Playing, Back toggles Pause on and second Back closes it
                Assert.IsTrue(overlays.HandleBack());
                Assert.IsTrue(overlays.IsPaused, "Back in match opens Pause / Quick-Settings sheet");
                Assert.IsFalse(leftToHome, "Back never silently quits match");

                Assert.IsTrue(overlays.HandleBack());
                Assert.IsFalse(overlays.IsPaused, "Second Back closes Pause sheet and resumes match");
                Assert.IsFalse(leftToHome);
            }
        }
    }
}
