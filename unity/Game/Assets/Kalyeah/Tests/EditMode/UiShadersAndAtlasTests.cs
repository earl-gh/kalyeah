using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class UiShadersAndAtlasTests
    {
        readonly List<UnityObject> _cleanup = new List<UnityObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null)
                    UnityObject.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
            UiShaders.ClearCache();
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
                if (t != null) return t;
            }
            return null;
        }

        [Test]
        public void AllFiveUiShaders_DeclareStencilClipRectAlphaClipAndUnityPerMaterial()
        {
            var shaderExpectations = new (string file, string shaderName, string[] requiredProps)[]
            {
                (
                    "UI_TacticalGlass.shader",
                    UiShaders.TacticalGlassShader,
                    new[] { "_RectSize", "_CornerCut", "_BorderWidth", "_BorderColor", "_TopHighlight", "_AccentColor", "_AccentWidth", "_GlassAlpha", "_NoiseGrain" }
                ),
                (
                    "UI_MetallicSheen.shader",
                    UiShaders.MetallicSheenShader,
                    new[] { "_RectSize", "_CornerCut", "_BaseTop", "_BaseBottom", "_SheenColor", "_SheenSpeed", "_SheenWidth", "_PressAmount", "_ReduceMotion" }
                ),
                (
                    "UI_LiquidGauge.shader",
                    UiShaders.LiquidGaugeShader,
                    new[] { "_Fill", "_TrailFill", "_Segments", "_FillColor", "_TrailColor", "_TrackColor", "_MeniscusGlow", "_WaveSpeed", "_LowPulse", "_ReduceMotion" }
                ),
                (
                    "UI_RadialSkillDisc.shader",
                    UiShaders.RadialSkillDiscShader,
                    new[] { "_Cooldown01", "_Arc01", "_RimColor", "_RimGlow", "_GlassTint", "_InnerRing", "_LockedDesat", "_UltPulse", "_ReduceMotion" }
                ),
                (
                    "UI_SoftGlow.shader",
                    UiShaders.SoftGlowShader,
                    new[] { "_GlowColor", "_Softness", "_PulseSpeed", "_ReduceMotion" }
                ),
            };

            string uiShaderDir = Path.Combine(Application.dataPath, "Kalyeah", "Shaders", "UI");
            Assert.IsTrue(Directory.Exists(uiShaderDir), "UI shader directory must exist at Assets/Kalyeah/Shaders/UI");

            string[] requiredUiContractTokens =
            {
                "_StencilComp",
                "_Stencil",
                "_StencilOp",
                "_StencilWriteMask",
                "_StencilReadMask",
                "_ColorMask",
                "Stencil",
                "UNITY_UI_CLIP_RECT",
                "UNITY_UI_ALPHACLIP",
                "CBUFFER_START(UnityPerMaterial)",
                "CBUFFER_END",
                "ZTest [unity_GUIZTestMode]",
                "\"RenderPipeline\" = \"UniversalPipeline\"",
            };

            for (int i = 0; i < shaderExpectations.Length; i++)
            {
                var spec = shaderExpectations[i];
                string diskPath = Path.Combine(uiShaderDir, spec.file);
                Assert.IsTrue(File.Exists(diskPath), $"Missing shader file {diskPath}");

                string src = File.ReadAllText(diskPath);
                Assert.IsFalse(src.Contains("\r\n"), $"{spec.file} must use LF line endings (CRLF=0)");
                Assert.IsTrue(src.Contains($"Shader \"{spec.shaderName}\""), $"{spec.file} must declare exact shader name {spec.shaderName}");

                for (int t = 0; t < requiredUiContractTokens.Length; t++)
                    StringAssert.Contains(requiredUiContractTokens[t], src, $"{spec.file} missing required uGUI/URP token {requiredUiContractTokens[t]}");

                for (int p = 0; p < spec.requiredProps.Length; p++)
                    StringAssert.Contains(spec.requiredProps[p], src, $"{spec.file} missing required property {spec.requiredProps[p]}");

                Shader compiled = Shader.Find(spec.shaderName);
                if (compiled != null)
                {
                    Assert.IsTrue(compiled.isSupported, $"{spec.shaderName} must compile cleanly and be supported on URP");
                }
            }
        }

        [Test]
        public void UiShaders_FactoriesAndPressSpring_AreFallbackSafeAndEditModeInert()
        {
            Material glassA = UiShaders.CreateTacticalGlass(MenuArt.ObsidianGlass, MenuArt.TitaniumRim, MenuArt.PaperWhite);
            Material glassB = UiShaders.CreateTacticalGlass(MenuArt.ObsidianGlass, MenuArt.TitaniumRim, MenuArt.PaperWhite);
            Assert.IsNotNull(glassA, "CreateTacticalGlass must never return null");
            Assert.AreSame(glassA, glassB, "Identical CreateTacticalGlass calls should share cached material");

            Material glowA = UiShaders.CreateSoftGlow(MenuArt.CoralGold);
            Material glowB = UiShaders.CreateSoftGlow(MenuArt.CoralGold);
            Assert.IsNotNull(glowA, "CreateSoftGlow must never return null");
            Assert.AreSame(glowA, glowB, "Identical CreateSoftGlow calls should share cached material");

            Material disc1 = UiShaders.CreateFrostedTitaniumDisc(MenuArt.ObsidianGlass, MenuArt.TitaniumRim);
            Material disc2 = UiShaders.CreateFrostedTitaniumDisc(MenuArt.ObsidianGlass, MenuArt.TitaniumRim);
            Assert.IsNotNull(disc1);
            Assert.IsNotNull(disc2);
            if (disc1 != Graphic.defaultGraphicMaterial)
            {
                Assert.AreNotSame(disc1, disc2, "Skill disc materials must be per-widget so S1 cooldown never affects S2");
                _cleanup.Add(disc1);
                _cleanup.Add(disc2);
            }

            Material cta = UiShaders.CreateMetallicCta(MenuArt.SignalVermilion, MenuArt.SignalVermilionDeep, 10f);
            Assert.IsNotNull(cta);
            if (cta != Graphic.defaultGraphicMaterial) _cleanup.Add(cta);

            Material hpGauge = UiShaders.CreateLiquidGauge(MenuArt.PaperWhite, MenuArt.CoralGold, 4, false);
            Material waterGauge = UiShaders.CreateLiquidGauge(MenuArt.WaterCyan, MenuArt.DiamondCyan, 0, true);
            Assert.IsNotNull(hpGauge);
            Assert.IsNotNull(waterGauge);
            if (hpGauge != Graphic.defaultGraphicMaterial && hpGauge.HasProperty(UiShaders.PropSegments))
            {
                Assert.AreEqual(4f, hpGauge.GetFloat(UiShaders.PropSegments), 0.01f);
                Assert.AreEqual(0f, hpGauge.GetFloat(UiShaders.PropWaveSpeed), 0.01f);
                _cleanup.Add(hpGauge);
            }
            if (waterGauge != Graphic.defaultGraphicMaterial && waterGauge.HasProperty(UiShaders.PropWaveSpeed))
            {
                Assert.Greater(waterGauge.GetFloat(UiShaders.PropWaveSpeed), 0.5f);
                _cleanup.Add(waterGauge);
            }

            Assert.IsNull(UiShaders.AttachPressSpring(null), "AttachPressSpring(null) must safely return null");

            var btnGo = new GameObject("PlayBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            _cleanup.Add(btnGo);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(42f, -18f);
            Button btn = btnGo.GetComponent<Button>();

            UiPressSpring spring = UiShaders.AttachPressSpring(btn, 0.94f, 3f);
            Assert.IsNotNull(spring);
            Assert.AreSame(spring, UiShaders.AttachPressSpring(btn, 0.92f, 2f), "Re-attaching must reuse existing component");
            Assert.AreEqual(0.92f, spring.PressScale, 0.001f);

            // Must remain strictly inert in EditMode so static captures and ScreenFitBoundsTests never drift.
            spring.OnPointerDown(new PointerEventData(EventSystem.current));
            Assert.AreEqual(Vector3.one, btnGo.transform.localScale);
            Assert.AreEqual(new Vector2(42f, -18f), rt.anchoredPosition);
            spring.OnPointerUp(new PointerEventData(EventSystem.current));
        }

        [Test]
        public void MenuArt_OptionA1TokensAndAll36SpecSkillEmblems_AreDistinctAndNonNull()
        {
            // Verify locked Option A1 Editorial + A2 Frosted Titanium + A3 Radar palette tokens.
            Assert.AreEqual(14f / 255f, MenuArt.ObsidianGlass.r, 0.005f);
            Assert.AreEqual(19f / 255f, MenuArt.ObsidianGlass.g, 0.005f);
            Assert.AreEqual(26f / 255f, MenuArt.ObsidianGlass.b, 0.005f);
            Assert.AreEqual(245f / 255f, MenuArt.PaperWhite.r, 0.005f);
            Assert.AreEqual(255f / 255f, MenuArt.SignalVermilion.r, 0.005f);
            Assert.AreEqual(70f / 255f, MenuArt.SignalVermilion.g, 0.005f);
            Assert.AreEqual(85f / 255f, MenuArt.SignalVermilion.b, 0.005f);
            Assert.AreEqual(213f / 255f, MenuArt.TitaniumRim.r, 0.005f);
            Assert.AreEqual(255f / 255f, MenuArt.CoralGold.r, 0.005f);
            Assert.AreEqual(74f / 255f, MenuArt.ValorantRadarRoad.r, 0.005f);
            Assert.AreEqual(180f / 255f, MenuArt.ValorantRadarWall.r, 0.005f);
            Assert.AreEqual(22f / 255f, MenuArt.ValorantRadarBg.r, 0.005f);

            // Discover all 36 canonical skill IDs directly from unity/Data/characters__*.json.
            string dataDir = Path.Combine(RepoRoot(), "unity", "Data");
            string[] charFiles = Directory.GetFiles(dataDir, "characters__*.json");
            Assert.AreEqual(12, charFiles.Length, "Expected 12 character JSON catalogs in unity/Data");

            var skillIds = new List<string>(36);
            var skillIdRegex = new Regex("\"mechanic\"\\s*:\\s*\"([a-z0-9_]+)\"", RegexOptions.Compiled);
            for (int i = 0; i < charFiles.Length; i++)
            {
                string json = File.ReadAllText(charFiles[i]);
                MatchCollection matches = skillIdRegex.Matches(json);
                Assert.AreEqual(3, matches.Count, $"{Path.GetFileName(charFiles[i])} must declare 3 skills");
                for (int m = 0; m < matches.Count; m++)
                    skillIds.Add(matches[m].Groups[1].Value);
            }
            Assert.AreEqual(36, skillIds.Count, "Expected exactly 36 spec skills across 12 kids");

            var distinctTextures = new HashSet<Texture2D>();
            var distinctNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < skillIds.Count; i++)
            {
                string id = skillIds[i];
                Sprite emblem = MenuArt.SkillEmblem(id);
                Assert.IsNotNull(emblem, $"MenuArt.SkillEmblem(\"{id}\") returned null");
                Assert.IsNotNull(emblem.texture, $"MenuArt.SkillEmblem(\"{id}\") texture is null");
                Assert.GreaterOrEqual(emblem.texture.width, 64);
                Assert.GreaterOrEqual(emblem.texture.height, 64);
                Assert.AreSame(emblem, MenuArt.SkillEmblem(id), $"MenuArt.SkillEmblem(\"{id}\") must cache its Sprite");
                Assert.IsTrue(distinctTextures.Add(emblem.texture), $"Skill \"{id}\" collided on Texture2D");
                Assert.IsTrue(distinctNames.Add(emblem.name), $"Skill \"{id}\" collided on Sprite name");
            }
            Assert.AreEqual(36, distinctTextures.Count);

            // Core combat controls and unknown fallback must also return crisp non-null sprites.
            string[] combatIds = { "tirador", "reload", "jump", "slide", "vault", "ads", "", null, "unknown_future_skill" };
            for (int i = 0; i < combatIds.Length; i++)
                Assert.IsNotNull(MenuArt.SkillEmblem(combatIds[i]), $"Combat/fallback emblem failed for '{combatIds[i]}'");
        }

        [Test]
        public void KalyeahResources_BustRoutingAndTmpHelpers_AreSafeAndPreserveLegacyUi()
        {
            Assert.AreEqual(84, KalyeahResources.TmpSamplingPointSize);
            Assert.AreEqual(9, KalyeahResources.TmpAtlasPadding);
            Assert.AreEqual(1024, KalyeahResources.TmpAtlasSize);

            TMP_FontAsset displayFont = KalyeahResources.TmpDisplayFont();
            TMP_FontAsset bodyFont = KalyeahResources.TmpBodyFont();
            if (displayFont != null)
            {
                Assert.AreEqual(1f, displayFont.faceInfo.scale, 0.001f, "TMP font asset scale must be 1.0");
                Assert.AreEqual(AtlasPopulationMode.Dynamic, displayFont.atlasPopulationMode);
                Material presetA = KalyeahResources.TmpHighContrastMaterial(displayFont, isDisplay: true);
                Material presetB = KalyeahResources.TmpHighContrastMaterial(displayFont, isDisplay: true);
                Assert.IsNotNull(presetA);
                Assert.AreSame(presetA, presetB, "High-contrast TMP preset must be shared (zero duplicate materials)");
            }
            if (bodyFont != null)
            {
                Assert.AreEqual(1f, bodyFont.faceInfo.scale, 0.001f);
            }

            var root = new GameObject("TmpTestRoot", typeof(RectTransform));
            _cleanup.Add(root);

            TextMeshProUGUI lightTmp = Ui.MakeTmpText("PlayLabel", root.transform, "PLAY", 32f, MenuArt.PaperWhite, TextAlignmentOptions.Center, displayFont: true);
            Assert.IsNotNull(lightTmp);
            Assert.AreEqual("PLAY", lightTmp.text);
            Assert.IsFalse(lightTmp.enableAutoSizing, "Runtime TMP labels must disable AutoSize");
            Assert.IsFalse(lightTmp.raycastTarget, "Decorative TMP labels must disable RaycastTarget");
            Assert.AreEqual(TextWrappingModes.NoWrap, lightTmp.textWrappingMode);

            TextMeshProUGUI darkTmp = Ui.MakeTmpText("ModeLabel", root.transform, "BLITZ", 20f, MenuArt.Ink, TextAlignmentOptions.MidlineLeft, displayFont: false);
            Assert.IsNotNull(darkTmp);
            Ui.SetTmpColor(darkTmp, MenuArt.PaperWhite);
            Assert.AreEqual(MenuArt.PaperWhite, darkTmp.color);

            // Legacy Ui.MakeText must stay 100% intact for backward compatibility.
            Text legacy = Ui.MakeText("LegacyLabel", root.transform, "READY", 22, MenuArt.Cream, TextAnchor.MiddleCenter);
            Assert.IsNotNull(legacy);
            Assert.AreEqual("READY", legacy.text);

            // Clean bust portrait routing: ui/avatars/<kid> routes to portraits/busts/<kid> when synced.
            Texture2D directBust = Resources.Load<Texture2D>("portraits/busts/narda");
            Texture2D routed = KalyeahResources.Texture("ui/avatars/narda");
            if (directBust != null)
            {
                Assert.AreSame(directBust, KalyeahResources.KidBust("narda"), "KidBust must prefer clean transparent portraits/busts/<kid>");
                Assert.AreSame(directBust, routed, "Legacy ui/avatars/<kid> calls must transparently route to portraits/busts/<kid>");
                Assert.IsNotNull(KalyeahResources.KidBustSprite("narda"));
            }
        }

        [Test]
        public void KalyeahUiAtlasPrebuild_ConformsToSpriteAtlasV2SkillContract()
        {
            Type prebuildType = FindEditorType("Kalyeah.Game.Editor.KalyeahUiAtlasPrebuild");
            Assert.IsNotNull(prebuildType, "KalyeahUiAtlasPrebuild must compile in Kalyeah.Editor");
            Assert.IsTrue(typeof(IPreprocessBuildWithReport).IsAssignableFrom(prebuildType), "Must implement IPreprocessBuildWithReport");

            MethodInfo validator = prebuildType.GetMethod("IsValidProjectSpritePath", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(validator);
            Assert.IsTrue((bool)validator.Invoke(null, new object[] { "Assets/Imported/Resources/portraits/busts/narda.png" }));
            Assert.IsFalse((bool)validator.Invoke(null, new object[] { "Packages/com.unity.ugui/Sprites/Test.png" }));
            Assert.IsFalse((bool)validator.Invoke(null, new object[] { "" }));

            string scriptPath = Path.Combine(Application.dataPath, "Kalyeah", "Editor", "KalyeahUiAtlasPrebuild.cs");
            Assert.IsTrue(File.Exists(scriptPath));
            string src = File.ReadAllText(scriptPath);
            Assert.IsFalse(src.Contains("\r\n"), "KalyeahUiAtlasPrebuild.cs must use LF line endings");
            StringAssert.Contains("// [UNITY-SKILL:SPRITEATLAS]", src);
            StringAssert.Contains("SpritePackerMode.SpriteAtlasV2", src);
            StringAssert.Contains("SpriteAtlasAsset", src);
            StringAssert.Contains("SpriteAtlasImporter", src);
            StringAssert.Contains("UI_HudIcons.spriteatlasv2", src);
            StringAssert.Contains("UI_Portraits.spriteatlasv2", src);
            StringAssert.Contains("TextureImporterFormat.ASTC_6x6", src);
            StringAssert.Contains("enableRotation = false", src);
            StringAssert.Contains("enableTightPacking = false", src);
            StringAssert.Contains("padding = 4", src);
            StringAssert.Contains("generateMipMaps = false", src);
            StringAssert.Contains("includeInBuild", src);

            // Strictly enforce V2 rules: no manual [MenuItem] and no deprecated SpriteAtlasAsset setters.
            Assert.IsFalse(src.Contains("[MenuItem"), "Prebuild SpriteAtlas script must not declare [MenuItem]");
            Assert.IsFalse(src.Contains("SetTextureSettings("), "Must not use deprecated SpriteAtlasAsset.SetTextureSettings");
            Assert.IsFalse(src.Contains("SetPackingSettings("), "Must not use deprecated SpriteAtlasAsset.SetPackingSettings");
            Assert.IsFalse(src.Contains("SetIncludeInBuild("), "Must not use deprecated SpriteAtlasAsset.SetIncludeInBuild");

            // Confirm SpritePackerMode can be enabled and read back cleanly without leaving persistent test mutations.
            SpritePackerMode previousMode = EditorSettings.spritePackerMode;
            try
            {
                MethodInfo enablePacker = prebuildType.GetMethod("EnsureSpritePackerV2Enabled", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(enablePacker);
                Assert.IsTrue((bool)enablePacker.Invoke(null, null));
                Assert.AreEqual(SpritePackerMode.SpriteAtlasV2, EditorSettings.spritePackerMode);
            }
            finally
            {
                EditorSettings.spritePackerMode = previousMode;
            }
        }
    }
}
