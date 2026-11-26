using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    public sealed class KidRigAndGraphicsTests
    {
        string _savedSettingsJson;
        int _savedMipLimit;
        SkinWeights _savedSkinWeights;
        GameObject _testRoot;

        [SetUp]
        public void SetUp()
        {
            _savedSettingsJson = SettingsStore.Current.ToJson();
            _savedMipLimit = QualitySettings.globalTextureMipmapLimit;
            _savedSkinWeights = QualitySettings.skinWeights;
            _testRoot = new GameObject("KidRigAndGraphicsTests_Root");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                KalyeahResources.Kill(_testRoot);
                _testRoot = null;
            }

            SettingsStore.Current.ResetToDefaults();
            if (!string.IsNullOrEmpty(_savedSettingsJson))
                SettingsStore.Current.LoadJson(_savedSettingsJson);

            QualitySettings.globalTextureMipmapLimit = _savedMipLimit;
            QualitySettings.skinWeights = _savedSkinWeights;
            MatchBoot.ApplyDeviceQualityAndFrameRate();
        }

        [Test]
        public void GraphicsTierProfile_AllFourTiers_MatchSpecBudgetsAndCutoffs()
        {
            GraphicsTierProfile low = GraphicsTierProfile.ForQuality(SettingsStore.GraphicsLow);
            GraphicsTierProfile med = GraphicsTierProfile.ForQuality(SettingsStore.GraphicsMedium);
            GraphicsTierProfile high = GraphicsTierProfile.ForQuality(SettingsStore.GraphicsHigh);
            GraphicsTierProfile ultra = GraphicsTierProfile.ForQuality(SettingsStore.GraphicsUltra);

            // Low (0)
            Assert.AreEqual(0, low.QualityIndex);
            Assert.AreEqual("Low", low.Name);
            Assert.AreEqual(30, low.MaxFpsCap);
            Assert.AreEqual(0.85f, low.RenderScale, 0.0001f);
            Assert.AreEqual(1, low.MsaaSamples);
            Assert.IsTrue(low.BlobShadowsOnly);
            Assert.IsFalse(low.RealTimeShadows);
            Assert.AreEqual(1, low.TextureMipmapLimit);
            Assert.AreEqual(2, low.SkinWeightsCount);
            Assert.AreEqual(8f, low.Lod1Distance, 0.001f);
            Assert.AreEqual(18f, low.Lod2Distance, 0.001f);
            Assert.IsFalse(low.SpringBonesEnabled);
            Assert.IsFalse(low.FootIkEnabled);
            Assert.IsFalse(low.CorrectivesEnabled);
            Assert.IsFalse(low.PostProcessingEnabled);
            Assert.IsFalse(low.UseNormalAndOrmMaps);
            Assert.AreEqual(1, low.ResolveCharacterLod(4f, isLocal: false));
            Assert.AreEqual(2, low.ResolveCharacterLod(10f, isLocal: false));
            Assert.AreEqual(1, low.ResolveCharacterLod(4f, isLocal: true));
            Assert.AreEqual(2, low.ResolveAnimUpdateInterval(15f, isVisible: true));
            Assert.AreEqual(3, low.ResolveAnimUpdateInterval(28f, isVisible: true));
            Assert.AreEqual(0, low.ResolveAnimUpdateInterval(4f, isVisible: false));

            // Medium (1)
            Assert.AreEqual(1, med.QualityIndex);
            Assert.AreEqual("Medium", med.Name);
            Assert.AreEqual(60, med.MaxFpsCap);
            Assert.AreEqual(1.00f, med.RenderScale, 0.0001f);
            Assert.AreEqual(1, med.MsaaSamples);
            Assert.IsTrue(med.RealTimeShadows);
            Assert.IsTrue(med.SoftShadows);
            Assert.AreEqual(0, med.TextureMipmapLimit);
            Assert.AreEqual(4, med.SkinWeightsCount);
            Assert.AreEqual(15f, med.Lod1Distance, 0.001f);
            Assert.AreEqual(35f, med.Lod2Distance, 0.001f);
            Assert.IsFalse(med.SpringBonesEnabled);
            Assert.IsTrue(med.ShouldRunFootIk(4f, isLocal: true));
            Assert.IsFalse(med.ShouldRunFootIk(4f, isLocal: false));
            Assert.IsTrue(med.ShouldRunCorrectives(4f, isLocal: true));
            Assert.IsFalse(med.ShouldRunCorrectives(4f, isLocal: false));
            Assert.IsTrue(med.PostProcessingEnabled);
            Assert.IsTrue(med.UseNormalAndOrmMaps);
            Assert.AreEqual(0, med.ResolveCharacterLod(10f, isLocal: false));
            Assert.AreEqual(1, med.ResolveCharacterLod(20f, isLocal: false));
            Assert.AreEqual(2, med.ResolveCharacterLod(42f, isLocal: false));

            // High (2)
            Assert.AreEqual(2, high.QualityIndex);
            Assert.AreEqual("High", high.Name);
            Assert.AreEqual(60, high.MaxFpsCap);
            Assert.AreEqual(1.00f, high.RenderScale, 0.0001f);
            Assert.AreEqual(2, high.MsaaSamples);
            Assert.IsTrue(high.RealTimeShadows);
            Assert.IsTrue(high.SoftShadows);
            Assert.AreEqual(0, high.TextureMipmapLimit);
            Assert.AreEqual(4, high.SkinWeightsCount);
            Assert.AreEqual(15f, high.Lod1Distance, 0.001f);
            Assert.AreEqual(35f, high.Lod2Distance, 0.001f);
            Assert.IsTrue(high.ShouldRunSprings(18f));
            Assert.IsFalse(high.ShouldRunSprings(24f));
            Assert.IsTrue(high.ShouldRunFootIk(18f, isLocal: false));
            Assert.IsTrue(high.ShouldRunCorrectives(18f, isLocal: false));
            Assert.IsTrue(high.PostProcessingEnabled);
            Assert.IsFalse(high.FullPostProcessing);
            Assert.IsTrue(high.UseNormalAndOrmMaps);

            // Ultra (3)
            Assert.AreEqual(3, ultra.QualityIndex);
            Assert.AreEqual("Ultra", ultra.Name);
            Assert.IsTrue(ultra.UsesDeviceRefreshCap);
            Assert.AreEqual(120, ultra.MaxFpsCap);
            Assert.AreEqual(1.00f, ultra.RenderScale, 0.0001f);
            Assert.AreEqual(4, ultra.MsaaSamples);
            Assert.IsTrue(ultra.SoftShadows);
            Assert.Greater(ultra.ShadowDistance, high.ShadowDistance);
            Assert.AreEqual(0, ultra.TextureMipmapLimit);
            Assert.AreEqual(4, ultra.SkinWeightsCount);
            Assert.AreEqual(22f, ultra.Lod1Distance, 0.001f);
            Assert.AreEqual(45f, ultra.Lod2Distance, 0.001f);
            Assert.IsTrue(ultra.ShouldRunSprings(26f));
            Assert.IsFalse(ultra.ShouldRunSprings(32f));
            Assert.IsTrue(ultra.PostProcessingEnabled);
            Assert.IsTrue(ultra.FullPostProcessing);
            Assert.AreEqual(0, ultra.ResolveCharacterLod(20f, isLocal: false));
            Assert.AreEqual(1, ultra.ResolveCharacterLod(30f, isLocal: false));
            Assert.AreEqual(2, ultra.ResolveCharacterLod(50f, isLocal: false));

            // Out-of-range clamps safely
            Assert.AreEqual(low.QualityIndex, GraphicsTierProfile.ForQuality(-5).QualityIndex);
            Assert.AreEqual(ultra.QualityIndex, GraphicsTierProfile.ForQuality(99).QualityIndex);
        }

        [Test]
        public void SettingsStore_GraphicsQuality_AllFourTiers_RoundTripAndClamping()
        {
            for (int tier = SettingsStore.GraphicsLow; tier <= SettingsStore.GraphicsUltra; tier++)
            {
                var store = new SettingsStore
                {
                    GraphicsQuality = tier,
                    EnemyHighlightMode = tier % 3,
                };

                Assert.AreEqual(tier, store.GraphicsQuality);
                Assert.AreEqual(tier == 0, store.IsTurboGraphics);
                Assert.AreEqual(tier, store.GraphicsProfile.QualityIndex);

                string json = store.ToJson();
                SettingsStore restored = SettingsStore.FromJson(json);
                Assert.AreEqual(tier, restored.GraphicsQuality, $"tier {tier} roundtrip");
                Assert.AreEqual(tier == 0, restored.IsTurboGraphics, $"tier {tier} IsTurboGraphics");
                Assert.AreEqual(tier % 3, restored.EnemyHighlightMode, $"tier {tier} enemy highlight");
            }

            var clampTest = new SettingsStore { GraphicsQuality = -4 };
            Assert.AreEqual(0, clampTest.GraphicsQuality);
            clampTest.GraphicsQuality = 17;
            Assert.AreEqual(3, clampTest.GraphicsQuality);

            // Effective FPS cap across tiers & refresh rates
            clampTest.TargetFrameRate = 60;
            clampTest.GraphicsQuality = SettingsStore.GraphicsLow;
            Assert.AreEqual(30, clampTest.EffectiveFrameRateCap(120));
            clampTest.GraphicsQuality = SettingsStore.GraphicsHigh;
            Assert.AreEqual(60, clampTest.EffectiveFrameRateCap(120));
            clampTest.GraphicsQuality = SettingsStore.GraphicsUltra;
            Assert.AreEqual(60, clampTest.EffectiveFrameRateCap(60));
            Assert.AreEqual(90, clampTest.EffectiveFrameRateCap(90));
            Assert.AreEqual(120, clampTest.EffectiveFrameRateCap(120));
            clampTest.TargetFrameRate = 30;
            Assert.AreEqual(30, clampTest.EffectiveFrameRateCap(120), "Explicit 30 FPS saver still respected on Ultra");

            // Colorblind-safe enemy highlight resolution
            clampTest.EnemyHighlightMode = SettingsStore.EnemyHighlightRed;
            clampTest.ColorblindMode = 1;
            Assert.AreEqual(SettingsStore.EnemyHighlightYellow, clampTest.ResolvedEnemyHighlightMode);
            clampTest.ColorblindMode = 3;
            Assert.AreEqual(SettingsStore.EnemyHighlightPurple, clampTest.ResolvedEnemyHighlightMode);
        }

        [Test]
        public void JointLimitsAndClipMetrics_ParseJson_WrappedUnwrappedAndOptionalFiles()
        {
            // Graceful fallback on missing / broken payloads
            RigJointLimitsData emptyLimits = RigJointLimitsData.ParseJson(null);
            Assert.Greater(emptyLimits.JointCount, 8);
            Assert.IsTrue(emptyLimits.TryGetLimit("LeftForeArm", "narda", out JointLimitEntry defaultElbow));
            Assert.IsTrue(defaultElbow.IsHinge);

            AnimClipMetricsData emptyMetrics = AnimClipMetricsData.ParseJson("{not valid json");
            Assert.Greater(emptyMetrics.ClipCount, 0);
            Assert.AreEqual(0.15f, emptyMetrics.FadeLocomotion, 0.001f);

            // Wrapped rig__joint_limits.json matching exact 995b623f schema (axes.local_unity, kind, twistRequiresFlexDeg, capsules, overrides)
            const string sampleLimitsJson = @"{
              ""source"": ""res://data/rig/joint_limits.tres"",
              ""class"": ""RigJointLimits"",
              ""data"": {
                ""$class"": ""RigJointLimits"",
                ""schemaVersion"": 1,
                ""referenceLegLengthM"": 0.6957,
                ""joints"": {
                  ""LeftForeArm"": {
                    ""bone"": ""mixamorig:LeftForeArm"",
                    ""parent"": ""mixamorig:LeftArm"",
                    ""kind"": ""hinge"",
                    ""flex"": [-5.0, 145.0],
                    ""abduct"": [0.0, 0.0],
                    ""twist"": [-85.0, 85.0],
                    ""twistRequiresFlexDeg"": 0.0,
                    ""axes"": {
                      ""local_unity"": { ""flex"": [1.0, 0.0, 0.0], ""abduct"": [0.0, 0.0, -1.0], ""twist"": [0.0, -1.0, 0.0] }
                    }
                  },
                  ""LeftLeg"": {
                    ""bone"": ""mixamorig:LeftLeg"",
                    ""parent"": ""mixamorig:LeftUpLeg"",
                    ""kind"": ""hinge"",
                    ""flex"": [-3.0, 140.0],
                    ""abduct"": [0.0, 0.0],
                    ""twist"": [-10.0, 10.0],
                    ""twistRequiresFlexDeg"": 20.0,
                    ""axes"": {
                      ""local_unity"": { ""flex"": [1.0, 0.0, 0.0], ""abduct"": [0.0, 0.0, -1.0], ""twist"": [0.0, -1.0, 0.0] }
                    }
                  }
                },
                ""capsules"": {
                  ""domeng"": { ""heightM"": 1.70, ""legLengthM"": 0.555, ""legLengthRatio"": 0.798 }
                },
                ""overrides"": {
                  ""domeng"": {
                    ""LeftForeArm"": { ""flex"": [-20.0, 122.0] },
                    ""LeftLeg"": { ""flex"": [-3.0, 105.0] }
                  }
                }
              }
            }";

            RigJointLimitsData parsedLimits = RigJointLimitsData.ParseJson(sampleLimitsJson);
            Assert.IsTrue(parsedLimits.TryGetLimit("LeftForeArm", "narda", out JointLimitEntry nardaElbow));
            Assert.AreEqual(-5f, nardaElbow.FlexMinDeg, 0.01f);
            Assert.AreEqual(145f, nardaElbow.FlexMaxDeg, 0.01f);
            Assert.IsTrue(nardaElbow.IsHinge);
            Assert.AreEqual(-1f, nardaElbow.TwistAxis.y, 0.001f, "local_unity twist axis preserved verbatim");

            Assert.IsTrue(parsedLimits.TryGetLimit("LeftLeg", "narda", out JointLimitEntry nardaKnee));
            Assert.IsTrue(nardaKnee.IsHinge);
            Assert.AreEqual(20f, nardaKnee.TwistMinFlexDeg, 0.01f);

            Assert.IsTrue(parsedLimits.TryGetKidLegLengthRatio("domeng", out float domengLegRatio));
            Assert.AreEqual(0.798f, domengLegRatio, 0.001f);

            // Domeng override narrows max 145 -> 122 and rejects widening min -20 (stays -5).
            Assert.IsTrue(parsedLimits.TryGetLimit("LeftForeArm", "domeng", out JointLimitEntry domengElbow));
            Assert.AreEqual(-5f, domengElbow.FlexMinDeg, 0.01f, "Per-kid override must never widen min limit");
            Assert.AreEqual(122f, domengElbow.FlexMaxDeg, 0.01f, "Per-kid override must narrow max limit");

            // Wrapped anim__clip_metrics.json
            const string sampleMetricsJson = @"{
              ""source"": ""res://data/anim/clip_metrics.tres"",
              ""class"": ""AnimClipMetrics"",
              ""data"": {
                ""schemaVersion"": 1,
                ""fps"": 30,
                ""referenceLegLengthM"": 0.6957,
                ""speedMatchClamp"": { ""min"": 0.7, ""max"": 1.35 },
                ""crossfadeMatrixSec"": {
                  ""locomotionToLocomotion"": 0.15,
                  ""intoCast"": 0.05,
                  ""intoHit"": 0.03,
                  ""outOfHitOrStun"": 0.20,
                  ""intoAir"": 0.08,
                  ""default"": 0.14
                },
                ""clips"": {
                  ""Run"": {
                    ""name"": ""Run"",
                    ""durationSec"": 0.60,
                    ""frameCount"": 18,
                    ""fps"": 30,
                    ""loop"": true,
                    ""strideDistanceM"": 3.15,
                    ""strideSpeedMps"": 5.25,
                    ""releaseTimeSec"": 0.0,
                    ""releaseFrame"": 0,
                    ""crossfadeInSec"": 0.15,
                    ""archetype"": ""locomotion"",
                    ""upperBodyMask"": false,
                    ""syncGroup"": ""loco"",
                    ""maxFootSlideM"": 0.012
                  }
                },
                ""standIns"": { ""Strafe_L"": ""walk"" },
                ""kids"": {
                  ""domeng"": {
                    ""legLengthM"": 0.66,
                    ""legLengthRatio"": 0.949,
                    ""girthArmAbductDeg"": 8.5,
                    ""girthHipSpreadDeg"": 4.0,
                    ""aimYawMaxDeg"": 55,
                    ""aimPitchMaxDeg"": 35
                  }
                }
              }
            }";

            AnimClipMetricsData parsedMetrics = AnimClipMetricsData.ParseJson(sampleMetricsJson);
            Assert.IsTrue(parsedMetrics.TryGetClip("Run", out ClipMetricEntry runMetric));
            Assert.AreEqual(5.25f, runMetric.StrideSpeedMps, 0.01f);
            Assert.LessOrEqual(runMetric.MaxFootSlideM, 0.02f);
            Assert.IsTrue(parsedMetrics.TryGetStandIn("Strafe_L", out string strafeStandIn));
            Assert.AreEqual("walk", strafeStandIn);
            KidAnimAdaptation domengAdapt = parsedMetrics.GetKidAdaptation("domeng");
            Assert.AreEqual(8.5f, domengAdapt.GirthArmAbductDeg, 0.01f);

            // Parse real unity/Data files if synced on disk
            string dataDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Data"));
            string realLimitsPath = Path.Combine(dataDir, "rig__joint_limits.json");
            if (File.Exists(realLimitsPath))
            {
                RigJointLimitsData liveLimits = RigJointLimitsData.ParseJson(File.ReadAllText(realLimitsPath));
                Assert.Greater(liveLimits.JointCount, 8);
                Assert.IsTrue(liveLimits.TryGetLimit("ForeArm", null, out _));
            }

            string realMetricsPath = Path.Combine(dataDir, "anim__clip_metrics.json");
            if (File.Exists(realMetricsPath))
            {
                AnimClipMetricsData liveMetrics = AnimClipMetricsData.ParseJson(File.ReadAllText(realMetricsPath));
                Assert.Greater(liveMetrics.ClipCount, 0);
                Assert.AreEqual(0.15f, liveMetrics.FadeLocomotion, 0.001f);
            }
        }

        [Test]
        public void CharacterRigData_ParseJson_ConvertsGltfSpaceAndCorrectives()
        {
            const string rigJson = @"{
              ""schemaVersion"": 1,
              ""kid"": ""narda"",
              ""coordSpace"": ""gltf_yup"",
              ""heightM"": 1.50,
              ""legLengthM"": 0.71,
              ""refLegLengthM"": 0.70,
              ""legLengthRatio"": 1.014,
              ""girth"": { ""torsoRadiusM"": 0.13, ""armRadiusM"": 0.04, ""thighRadiusM"": 0.07, ""armAbductOffsetDeg"": 2.5, ""hipSpreadOffsetDeg"": 1.5 },
              ""capsules"": {
                ""Torso"": { ""bone"": ""mixamorig:Spine"", ""a"": [0.1, 0.2, 0.3], ""b"": [0.1, 0.5, 0.3], ""radius"": 0.13 }
              },
              ""twistBones"": [{ ""bone"": ""LeftArmTwist"", ""source"": ""LeftArm"", ""share"": 0.5 }],
              ""helperBones"": [{ ""bone"": ""LeftForeArmHelper"", ""source"": ""LeftForeArm"", ""share"": 0.5 }],
              ""springChains"": [{ ""name"": ""skirt_back"", ""kind"": ""skirt"", ""rootParent"": ""Hips"", ""bones"": [""Skirt_01"", ""Skirt_02""], ""stiffness"": 0.4, ""damping"": 0.2, ""gravity"": 0.5, ""colliders"": [""Torso""] }],
              ""correctives"": [
                { ""shape"": ""corr_LeftForeArm"", ""kind"": ""hinge"", ""bone"": ""LeftForeArm"", ""parent"": ""LeftArm"", ""axis"": [1, 0, 0], ""restAngleDeg"": 5, ""startAngleDeg"": 40, ""fullAngleDeg"": 135 },
                { ""shape"": ""corr_LeftArm_Up"", ""kind"": ""cone"", ""bone"": ""LeftArm"", ""parent"": ""LeftShoulder"", ""targetDir"": [0, 1, 0], ""boneRestDir"": [1, 0, 0], ""startAngleDeg"": 30, ""fullAngleDeg"": 110 }
              ]
            }";

            CharacterRigData rig = CharacterRigData.ParseJson(rigJson);
            Assert.IsNotNull(rig);
            Assert.AreEqual("narda", rig.KidId);
            Assert.AreEqual(1.014f, rig.LegLengthRatio, 0.001f);
            Assert.IsTrue(rig.Capsules.TryGetValue("Torso", out RigCapsuleDef torso));
            Assert.AreEqual("Spine", torso.Bone);
            // gltf_yup -> Unity flips X on positions
            Assert.AreEqual(-0.1f, torso.A.x, 0.0001f);
            Assert.AreEqual(0.2f, torso.A.y, 0.0001f);
            Assert.AreEqual(0.3f, torso.A.z, 0.0001f);
            Assert.AreEqual(1, rig.SpringChains.Length);
            Assert.AreEqual(2, rig.Correctives.Length);
            // gltf_yup -> Unity preserves X and negates Y,Z on rotation axes; flips X on directions
            Assert.AreEqual(1f, rig.Correctives[0].Axis.x, 0.001f);
            Assert.AreEqual(-1f, rig.Correctives[1].BoneRestDir.x, 0.001f);
        }

        [Test]
        public void SwingTwist_DecompositionAndJointLimitClamping_EnforcesHingeAndBallLimits()
        {
            // 1. Accurate swing * twist decomposition
            Quaternion pureSwing = Quaternion.AngleAxis(40f, Vector3.right);
            Quaternion pureTwist = Quaternion.AngleAxis(-65f, Vector3.up);
            Quaternion combined = pureSwing * pureTwist;

            RigJointLimitsData.DecomposeSwingTwist(combined, Vector3.up, out Quaternion outSwing, out Quaternion outTwist, out float twistDeg);
            Assert.AreEqual(-65f, twistDeg, 0.05f);
            Quaternion recomposed = outSwing * outTwist;
            Assert.Less(Quaternion.Angle(combined, recomposed), 0.05f);

            // 2. Hinge enforcement (ForeArm): sideways abduct is strictly zeroed, over-flex clamped to 145 deg
            var elbow = new JointLimitEntry("ForeArm", Vector3.right, Vector3.forward, Vector3.up, -5f, 145f, 0f, 0f, -85f, 85f, isHinge: true);
            Quaternion badSideways = Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.AngleAxis(40f, Vector3.forward);
            Quaternion clampedHinge = RigJointLimitsData.ClampSwingTwist(badSideways, in elbow);
            RigJointLimitsData.DecomposeSwingTwist(clampedHinge, elbow.TwistAxis, out Quaternion hSwing, out _, out _);
            hSwing.ToAngleAxis(out float hAngle, out Vector3 hAxis);
            if (hAngle > 180f) { hAngle = 360f - hAngle; hAxis = -hAxis; }
            float sidewaysComponent = Mathf.Abs(Vector3.Dot(hAxis * hAngle, elbow.AbductAxis));
            Assert.Less(sidewaysComponent, 0.05f, "Hinge joint must allow ZERO sideways abduct bend");

            Quaternion overFlex = Quaternion.AngleAxis(172f, Vector3.right);
            Quaternion clampedFlex = RigJointLimitsData.ClampSwingTwist(overFlex, in elbow);
            Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity, clampedFlex), 145.1f);

            // 3. Conditional knee twist (Leg): zero twist when straight (< 20 deg flex), up to +-10 deg when flexed
            var knee = new JointLimitEntry("Leg", Vector3.right, Vector3.forward, Vector3.up, -3f, 140f, 0f, 0f, -10f, 10f, isHinge: true, twistMinFlexDeg: 20f);
            Quaternion straightTwisted = Quaternion.AngleAxis(5f, Vector3.right) * Quaternion.AngleAxis(35f, Vector3.up);
            Quaternion straightClamped = RigJointLimitsData.ClampSwingTwist(straightTwisted, in knee);
            RigJointLimitsData.DecomposeSwingTwist(straightClamped, knee.TwistAxis, out _, out _, out float straightTwist);
            Assert.Less(Mathf.Abs(straightTwist), 0.1f, "Straight knee must block axial twist");

            Quaternion bentTwisted = Quaternion.AngleAxis(75f, Vector3.right) * Quaternion.AngleAxis(35f, Vector3.up);
            Quaternion bentClamped = RigJointLimitsData.ClampSwingTwist(bentTwisted, in knee);
            RigJointLimitsData.DecomposeSwingTwist(bentClamped, knee.TwistAxis, out _, out _, out float bentTwist);
            Assert.AreEqual(10f, bentTwist, 0.2f, "Flexed knee clamps twist to +10 deg");

            // 4. Ball-joint ellipse clamp (Arm): combined extreme pulls onto ellipse
            var arm = new JointLimitEntry("Arm", Vector3.right, Vector3.forward, Vector3.up, -50f, 160f, -30f, 140f, -80f, 80f, isHinge: false);
            Quaternion extremeBall = Quaternion.AngleAxis(160f, Vector3.right) * Quaternion.AngleAxis(140f, Vector3.forward);
            Quaternion clampedBall = RigJointLimitsData.ClampSwingTwist(extremeBall, in arm);
            RigJointLimitsData.DecomposeSwingTwist(clampedBall, arm.TwistAxis, out Quaternion bSwing, out _, out _);
            bSwing.ToAngleAxis(out float bAng, out Vector3 bAx);
            if (bAng > 180f) { bAng = 360f - bAng; bAx = -bAx; }
            float fPart = Vector3.Dot(bAx * bAng, arm.FlexAxis) / 160f;
            float aPart = Vector3.Dot(bAx * bAng, arm.AbductAxis) / 140f;
            Assert.LessOrEqual(fPart * fPart + aPart * aPart, 1.02f, "Ball swing must stay inside flex x abduct ellipse");
        }

        [Test]
        public void KidView_ModelFacesTheWayTheKidFaces()
        {
            var facing = new SVector2(0f, -1f);
            var snap = new KidSnapshot { Id = 1, Team = 0, CharacterId = "narda", Position = SVector2.Zero, Facing = facing, Hp = 100 };
            var kid = new KidView(_testRoot.transform, snap, 1f, Color.blue, null);
            kid.Place(SVector2.Zero, facing);

            Transform foot = null, toe = null;
            foreach (Transform t in kid.Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("LeftFoot", StringComparison.Ordinal)) foot = t;
                else if (t.name.EndsWith("LeftToeBase", StringComparison.Ordinal)) toe = t;
            }
            Assert.IsNotNull(foot, "narda has a LeftFoot bone");
            Assert.IsNotNull(toe, "narda has a LeftToeBase bone");

            Vector3 toes = toe.position - foot.position;
            toes.y = 0f;
            Vector3 want = KalyeahResources.World(facing) - KalyeahResources.World(SVector2.Zero);
            Assert.Greater(Vector3.Dot(toes.normalized, want.normalized), 0.5f, "the toes point where the kid faces, not back at the camera");
        }

        [Test]
        public void ContactShadow_DrawsOverTheBaseAndZoneOverlays()
        {
            Material blob = KalyeahResources.SharedBlobShadowMaterial();
            Assert.IsNotNull(blob);
            Assert.Greater(blob.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent,
                "zones and base mats draw at Transparent; a shadow before them is hidden at spawn");
        }

        [Test]
        public void KidView_FarKidStillDrawsWithItsOwnLodRenderer()
        {
            var snap = new KidSnapshot { Id = 1, Team = 1, CharacterId = "narda", Position = SVector2.Zero, Facing = new SVector2(0f, -1f), Hp = 100 };
            var kid = new KidView(_testRoot.transform, snap, 1f, Color.red, null);
            var meshes = new System.Collections.Generic.Dictionary<SkinnedMeshRenderer, Mesh>();
            foreach (SkinnedMeshRenderer r in kid.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) meshes[r] = r.sharedMesh;

            GraphicsTierProfile medium = MatchBoot.ApplyGraphicsPreset(SettingsStore.GraphicsMedium, deviceRefreshOverrideHz: 60);
            int shownLod0Vertices = 0, shownFarVertices = 0;
            kid.ApplyGraphicsProfile(medium, cameraDistanceMeters: 5f, visible: true);
            foreach (SkinnedMeshRenderer r in meshes.Keys)
                if (r.enabled && r.name != "TeamOutlineHull") shownLod0Vertices += r.sharedMesh.vertexCount;
            kid.ApplyGraphicsProfile(medium, cameraDistanceMeters: 46f, visible: true);
            Assert.AreEqual(2, kid.ActiveLodIndex);
            foreach (SkinnedMeshRenderer r in kid.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (meshes.TryGetValue(r, out Mesh before))
                    Assert.AreSame(before, r.sharedMesh, $"{r.name} keeps its own mesh: swapping LOD meshes stops Unity drawing the kid");
                if (r.enabled && r.name != "TeamOutlineHull") shownFarVertices += r.sharedMesh.vertexCount;
            }
            Assert.Greater(shownFarVertices, 0, "a kid 46 m away is still drawn");
            Assert.Less(shownFarVertices, shownLod0Vertices, "and with the lighter LOD mesh");
        }

        [Test]
        public void KidView_LodAndLivePresetSwitching_HandlesMissingAndPresentAssetsCleanly()
        {
            var snap = new KidSnapshot
            {
                Id = 1,
                Team = 0,
                CharacterId = "narda",
                Position = new SVector2(0f, 0f),
                Facing = new SVector2(0f, -1f),
                Hp = 100,
                Speed01 = 0.7f,
            };

            var kid = new KidView(_testRoot.transform, snap, 0.82f, new Color(0.95f, 0.25f, 0.25f, 1f), null);
            Assert.IsNotNull(kid.Root);

            // Live quality preset transitions via MatchBoot notify active KidView with zero restart or exceptions.
            for (int tier = SettingsStore.GraphicsLow; tier <= SettingsStore.GraphicsUltra; tier++)
            {
                GraphicsTierProfile applied = MatchBoot.ApplyGraphicsPreset(tier, deviceRefreshOverrideHz: 90);
                Assert.AreEqual(tier, MatchBoot.ActiveGraphicsProfile.QualityIndex);
                Assert.AreEqual(tier, kid.ActiveGraphicsProfile.QualityIndex);
                Assert.AreEqual(applied.TextureMipmapLimit, QualitySettings.globalTextureMipmapLimit);
                Assert.AreEqual(applied.SkinWeightsCount <= 2 ? SkinWeights.TwoBones : SkinWeights.FourBones, QualitySettings.skinWeights);

                // Verify distance + visibility LOD switching never throws regardless of LOD1/LOD2 presence.
                kid.ApplyGraphicsProfile(applied, cameraDistanceMeters: 5f, visible: true);
                Assert.AreEqual(applied.ResolveCharacterLod(5f, kid.IsLocalKid), kid.ActiveLodIndex);
                Assert.AreEqual(1, kid.ActiveAnimUpdateInterval);

                kid.ApplyGraphicsProfile(applied, cameraDistanceMeters: 20f, visible: true);
                Assert.AreEqual(applied.ResolveCharacterLod(20f, kid.IsLocalKid), kid.ActiveLodIndex);

                kid.ApplyGraphicsProfile(applied, cameraDistanceMeters: 46f, visible: true);
                Assert.AreEqual(2, kid.ActiveLodIndex, "Far kids (> 45m) use LOD2 and stay active");

                kid.ApplyGraphicsProfile(applied, cameraDistanceMeters: 10f, visible: false);
                Assert.AreEqual(0, kid.ActiveAnimUpdateInterval, "Off-screen kids drop to state-change-only sampling");
            }

            // Exercise full SyncState + Cast + Aim + EnemyHighlight + LateUpdate pass without exceptions.
            kid.ApplyGraphicsProfile(GraphicsTierProfile.High, cameraDistanceMeters: 6f, visible: true);
            kid.SetEnemyHighlight(true, KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightPurple));
            kid.SetAimTarget(new Vector3(2f, 1.2f, -6f), true);
            kid.TriggerCast(moving: true, duration: 0.36f, elapsedSec: 0.04f);
            kid.SyncState(
                ground: new SVector2(1.2f, -0.5f),
                airY: 0f,
                facing: new SVector2(0.2f, -1f),
                speed01: 0.85f,
                crouching: false,
                sliding: false,
                dashing: false,
                statusKind: 0,
                tagged: false,
                hitFlash01: 0.4f,
                buffId: "palamig",
                hideNearCamera: false,
                dt: 0.016f);
            kid.EvaluateLateUpdateNow(0.016f);
            Assert.IsNotNull(kid.CurrentPose);

            // Unknown kid ID safely falls back without crashing
            var fallbackSnap = snap;
            fallbackSnap.Id = 2;
            fallbackSnap.CharacterId = "nonexistent_kid_id";
            var fallbackKid = new KidView(_testRoot.transform, fallbackSnap, 0.82f, Color.blue, null);
            fallbackKid.ApplyGraphicsProfile(GraphicsTierProfile.Low, cameraDistanceMeters: 12f, visible: true);
            fallbackKid.EvaluateLateUpdateNow(0.016f);
            Assert.IsNotNull(fallbackKid.Root);
        }

        [Test]
        public void KidView_EightWayStandInGuard_NeverAliasesWalkOrRun()
        {
            var animGo = new GameObject("EightWayGuardHost");
            animGo.transform.SetParent(_testRoot.transform, false);
            Animation anim = animGo.AddComponent<Animation>();

            var walkClip = new AnimationClip { legacy = true, name = "walk" };
            var runClip = new AnimationClip { legacy = true, name = "Run" };
            var strafeLClip = new AnimationClip { legacy = true, name = "Strafe_L" };
            var strafeRClip = new AnimationClip { legacy = true, name = "Strafe_R" };
            var backClip = new AnimationClip { legacy = true, name = "Backpedal" };

            anim.AddClip(walkClip, "walk");
            anim.AddClip(runClip, "Run");
            anim.AddClip(strafeLClip, "Strafe_L");
            anim.AddClip(strafeRClip, "Strafe_R");
            anim.AddClip(backClip, "Backpedal");

            AnimationState stWalk = anim["walk"];
            AnimationState stRun = anim["Run"];
            AnimationState stL = anim["Strafe_L"];
            AnimationState stR = anim["Strafe_R"];
            AnimationState stBack = anim["Backpedal"];

            // Stand-in aliases (all pointing to walk) MUST NOT enable 8-way blending
            Assert.IsFalse(KidView.AreDistinctEightWayStates(stWalk, stRun, stWalk, stWalk, stWalk));
            Assert.IsFalse(KidView.AreDistinctEightWayStates(stWalk, stRun, stL, stL, stBack));
            Assert.IsFalse(KidView.AreDistinctEightWayStates(stWalk, stRun, stL, stR, stRun));
            Assert.IsFalse(KidView.AreDistinctEightWayStates(stWalk, stRun, null, stR, stBack));

            // Mutually distinct native states pass
            Assert.IsTrue(KidView.AreDistinctEightWayStates(stWalk, stRun, stL, stR, stBack));

            // The character GLBs carry the authored Strafe_L/R and Backpedal clips (tools/blender/kids/author_clips.py), so
            // the shipped kid resolves them as native, distinct states and turns 8-way blending on.
            var snap = new KidSnapshot { Id = 7, Team = 0, CharacterId = "narda", Facing = new SVector2(0f, -1f) };
            var kid = new KidView(_testRoot.transform, snap, 0.82f, Color.blue, null);
            Assert.IsTrue(kid.HasEightWayClips, "narda's GLB must ship native Strafe_L, Strafe_R and Backpedal clips");

            KalyeahResources.Kill(walkClip);
            KalyeahResources.Kill(runClip);
            KalyeahResources.Kill(strafeLClip);
            KalyeahResources.Kill(strafeRClip);
            KalyeahResources.Kill(backClip);
        }

        [Test]
        public void EveryKidModel_ShipsNativeStrafeAndBackpedalClips()
        {
            string[] eightWay = { "Strafe_L", "Strafe_R", "Backpedal" };
            foreach (string kidId in AppSession.DefaultRoster)
            {
                GameObject prefab = KalyeahResources.CharacterModel(kidId, 0);
                Assert.IsNotNull(prefab, $"Missing character model for {kidId}");
                Animation animation = prefab.GetComponentInChildren<Animation>(true);
                Assert.IsNotNull(animation, $"{kidId} model has no Animation component");
                foreach (string clip in eightWay)
                    Assert.IsNotNull(animation.GetClip(clip), $"{kidId} model is missing the {clip} clip");
            }
        }

        [Test]
        public void KidView_ModelToBoneLocalConversions_MatchRestPoseGeometryAndCorrectiveAxes()
        {
            var modelGo = new GameObject("MockModel");
            modelGo.transform.SetParent(_testRoot.transform, false);
            modelGo.transform.localPosition = new Vector3(0.25f, 0f, -0.4f);
            modelGo.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            modelGo.transform.localScale = Vector3.one * 0.82f;

            var boneGo = new GameObject("MockSpine");
            boneGo.transform.SetParent(modelGo.transform, false);
            boneGo.transform.localPosition = new Vector3(0f, 0.95f, 0.04f);
            boneGo.transform.localRotation = Quaternion.Euler(12f, -25f, 40f);

            // 1. Model-space capsule endpoints round-trip identically through bone-local space
            Vector3 modelA = new Vector3(0.02f, 0.72f, 0.01f);
            Vector3 modelB = new Vector3(-0.01f, 1.24f, 0.05f);
            KidView.ConvertModelCapsuleToBoneLocal(modelGo.transform, boneGo.transform, modelA, modelB, out Vector3 localA, out Vector3 localB);

            Vector3 worldAExpected = modelGo.transform.TransformPoint(modelA);
            Vector3 worldBExpected = modelGo.transform.TransformPoint(modelB);
            Assert.Less(Vector3.Distance(worldAExpected, boneGo.transform.TransformPoint(localA)), 0.0005f);
            Assert.Less(Vector3.Distance(worldBExpected, boneGo.transform.TransformPoint(localB)), 0.0005f);

            // 2. Model-space corrective axis converts cleanly into bone rest-local frame
            Vector3 modelAxis = new Vector3(1f, 0f, 0f);
            Vector3 modelTargetDir = new Vector3(0f, 1f, 0f);
            Vector3 modelRestDir = new Vector3(0.766f, -0.643f, 0f).normalized;
            KidView.ConvertModelCorrectiveToBoneLocal(
                modelGo.transform, boneGo.transform, modelAxis, modelTargetDir, modelRestDir,
                out Vector3 localAxis, out Vector3 localTargetDir, out Vector3 localRestDir);

            // Rotating bone in bone rest-local space by +60 deg around localAxis must yield signedDeg == +60 deg
            Quaternion delta = Quaternion.AngleAxis(60f, localAxis);
            RigJointLimitsData.DecomposeSwingTwist(delta, localAxis, out _, out _, out float measuredDeg);
            Assert.AreEqual(60f, measuredDeg, 0.05f);

            // Dot product between localRestDir and localTargetDir equals model-space dot product
            Assert.AreEqual(Vector3.Dot(modelRestDir, modelTargetDir), Vector3.Dot(localRestDir, localTargetDir), 0.001f);
        }

        [Test]
        public void KidView_AimOffsetAngles_TurnTowardTargetOnLocalUnitySpineAxes()
        {
            // Root space faces -Z (+X = kid left, +Y = up).
            // In glTFast Spine1 bone-local rest space (local_unity from 995b623f): nose faces +Z, kid left is -X,
            // twist = (0, -1, 0), flex = (1, 0, 0).
            Vector3 spineTwistAxis = new Vector3(0f, -1f, 0f);
            Vector3 spineFlexAxis = new Vector3(1f, 0f, 0f);
            Vector3 spineRestForward = Vector3.forward; // +Z in Spine1 local_unity space

            // Aiming 45 deg to kid's LEFT in Root space (+X, -Z) -> +45 deg yaw -> rotates Spine1 +Z toward -X (bone-local left)
            Vector3 leftTargetRoot = new Vector3(1f, 0f, -1f).normalized;
            KidView.ComputeLocalAimAngles(leftTargetRoot, 55f, 35f, out float yawLeft, out float pitchFlat);
            Assert.AreEqual(45f, yawLeft, 0.05f);
            Assert.AreEqual(0f, pitchFlat, 0.05f);
            Vector3 turnedLeft = Quaternion.AngleAxis(yawLeft, spineTwistAxis) * spineRestForward;
            Assert.Less(turnedLeft.x, -0.65f, "Aiming left must rotate Spine1 forward toward -X (bone-local kid left)");
            Assert.Greater(turnedLeft.z, 0.65f);

            // Aiming 45 deg to kid's RIGHT in Root space (-X, -Z) -> -45 deg yaw -> rotates Spine1 +Z toward +X (bone-local right)
            Vector3 rightTargetRoot = new Vector3(-1f, 0f, -1f).normalized;
            KidView.ComputeLocalAimAngles(rightTargetRoot, 55f, 35f, out float yawRight, out _);
            Assert.AreEqual(-45f, yawRight, 0.05f);
            Vector3 turnedRight = Quaternion.AngleAxis(yawRight, spineTwistAxis) * spineRestForward;
            Assert.Greater(turnedRight.x, 0.65f, "Aiming right must rotate Spine1 forward toward +X (bone-local kid right)");
            Assert.Greater(turnedRight.z, 0.65f);

            // Aiming 30 deg UP in Root space (0, +sin30, -cos30) -> +30 deg pitch -> ApplyAimShare applies -pitchDeg around +X -> tilts Spine1 +Z toward +Y
            Vector3 upTargetRoot = new Vector3(0f, Mathf.Sin(30f * Mathf.Deg2Rad), -Mathf.Cos(30f * Mathf.Deg2Rad));
            KidView.ComputeLocalAimAngles(upTargetRoot, 55f, 35f, out float yawCenter, out float pitchUp);
            Assert.AreEqual(0f, yawCenter, 0.05f);
            Assert.AreEqual(30f, pitchUp, 0.05f);
            Vector3 tiltedUp = Quaternion.AngleAxis(-pitchUp, spineFlexAxis) * spineRestForward;
            Assert.Greater(tiltedUp.y, 0.45f, "Aiming up must tilt Spine1 forward upward toward +Y");
            Assert.Greater(tiltedUp.z, 0.80f);
        }

        [Test]
        public void MatchBoot_GlobalLowKeywordAndClarityGlobals_ToggleCleanlyAcrossAllFourTiers()
        {
            Assert.IsTrue(MatchBoot.IsLowQualityKeywordActive(SettingsStore.GraphicsLow));
            Assert.IsTrue(MatchBoot.IsLowQualityKeywordActive(-2), "Below-range tier clamps to Low");
            Assert.IsFalse(MatchBoot.IsLowQualityKeywordActive(SettingsStore.GraphicsMedium));
            Assert.IsFalse(MatchBoot.IsLowQualityKeywordActive(SettingsStore.GraphicsHigh));
            Assert.IsFalse(MatchBoot.IsLowQualityKeywordActive(SettingsStore.GraphicsUltra));
            Assert.IsFalse(MatchBoot.IsLowQualityKeywordActive(99), "Above-range tier clamps to Ultra");

            int tierId = Shader.PropertyToID("_KalyeahGraphicsTier");
            int detailId = Shader.PropertyToID("_KalyeahUseDetailMaps");
            int ambMinId = Shader.PropertyToID("_KalyeahAmbientMin");
            int ambMaxId = Shader.PropertyToID("_KalyeahAmbientMax");
            int sunDirId = Shader.PropertyToID("_KalyeahSunDir");

            for (int tier = SettingsStore.GraphicsLow; tier <= SettingsStore.GraphicsUltra; tier++)
            {
                MatchBoot.ApplyGraphicsPreset(tier, deviceRefreshOverrideHz: 90);
                bool expectedLow = tier == SettingsStore.GraphicsLow;
                Assert.AreEqual(
                    expectedLow,
                    Shader.IsKeywordEnabled(MatchBoot.LowQualityShaderKeyword),
                    $"KALYEAH_QUALITY_LOW mismatch on tier {tier}");
                Assert.AreEqual(tier, Shader.GetGlobalFloat(tierId), 0.001f);
                Assert.AreEqual(expectedLow ? 0f : 1f, Shader.GetGlobalFloat(detailId), 0.001f);

                float ambMin = Shader.GetGlobalFloat(ambMinId);
                float ambMax = Shader.GetGlobalFloat(ambMaxId);
                Assert.AreEqual(MatchBoot.DefaultAmbientMin, ambMin, 0.001f);
                Assert.AreEqual(MatchBoot.DefaultAmbientMax, ambMax, 0.001f);
                Assert.Greater(ambMax, ambMin);

                Vector4 sunVec = Shader.GetGlobalVector(sunDirId);
                Assert.AreEqual(1f, new Vector3(sunVec.x, sunVec.y, sunVec.z).magnitude, 0.002f);
                Assert.AreEqual(1f, sunVec.w, 0.001f);
            }

            // Directional light override aligns _KalyeahSunDir with -light.transform.forward (toward sun);
            // null sun cleanly falls back to MatchBoot.FallbackSunDirection.
            var sunGo = new GameObject("TestSunLight");
            sunGo.transform.SetParent(_testRoot.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light testSun = sunGo.AddComponent<Light>();
            testSun.type = LightType.Directional;

            MatchBoot.ApplyGlobalClarityGlobals(GraphicsTierProfile.High, SettingsStore.EnemyHighlightRed, sunOverride: testSun);
            Vector3 expectedSun = (-testSun.transform.forward).normalized;
            Vector4 actualSun = Shader.GetGlobalVector(sunDirId);
            Assert.Less(Vector3.Distance(expectedSun, new Vector3(actualSun.x, actualSun.y, actualSun.z)), 0.002f);

            // RenderSettings.sun falls back to the brightest active directional light in the scene (the editor's default scene has
            // one), so every light is switched off to reach MatchBoot's own fallback direction.
            Light prevSun = RenderSettings.sun;
            Light[] sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool[] lightWasEnabled = new bool[sceneLights.Length];
            for (int i = 0; i < sceneLights.Length; i++)
            {
                lightWasEnabled[i] = sceneLights[i].enabled;
                sceneLights[i].enabled = false;
            }
            try
            {
                RenderSettings.sun = null;
                MatchBoot.ApplyGlobalClarityGlobals(GraphicsTierProfile.Low, SettingsStore.EnemyHighlightRed, sunOverride: null);
                Assert.IsTrue(Shader.IsKeywordEnabled(MatchBoot.LowQualityShaderKeyword));
                Vector4 fallbackSun = Shader.GetGlobalVector(sunDirId);
                Assert.Less(
                    Vector3.Distance(MatchBoot.FallbackSunDirection, new Vector3(fallbackSun.x, fallbackSun.y, fallbackSun.z)),
                    0.002f);
            }
            finally
            {
                for (int i = 0; i < sceneLights.Length; i++)
                    if (sceneLights[i] != null) sceneLights[i].enabled = lightWasEnabled[i];
                RenderSettings.sun = prevSun;
            }
        }

        [Test]
        public void MatchBoot_ResolveEnemyHighlightColor_ReturnsDistinctColorblindSafeColorsAndSyncsGlobal()
        {
            Color red = MatchBoot.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightRed);
            Color yellow = MatchBoot.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightYellow);
            Color purple = MatchBoot.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightPurple);

            Assert.AreEqual(KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightRed), red);
            Assert.AreEqual(KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightYellow), yellow);
            Assert.AreEqual(KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.EnemyHighlightPurple), purple);

            // Distinct chromatic separation for Red / Yellow / Purple colorblind-safe highlights.
            Assert.Greater(red.r, 0.80f);
            Assert.Greater(red.r, red.g * 2f);
            Assert.Greater(red.r, red.b * 2f);

            Assert.Greater(yellow.r, 0.80f);
            Assert.Greater(yellow.g, 0.70f);
            Assert.Less(yellow.b, 0.35f);

            Assert.Greater(purple.b, 0.80f);
            Assert.Greater(purple.r, 0.55f);
            Assert.Less(purple.g, 0.50f);

            Assert.Greater(ColorRgbDistance(red, yellow), 0.55f, "Red and Yellow highlights must stay distinct");
            Assert.Greater(ColorRgbDistance(red, purple), 0.55f, "Red and Purple highlights must stay distinct");
            Assert.Greater(ColorRgbDistance(yellow, purple), 0.55f, "Yellow and Purple highlights must stay distinct");

            // Global _KalyeahEnemyHighlightColor tracks the active highlight mode. Whether Shader.SetGlobalColor converts to linear is
            // Unity's business, so the expected value is what Unity itself stores and returns for the mode's colour.
            int globalHighlightId = Shader.PropertyToID("_KalyeahEnemyHighlightColor");
            int calibrationId = Shader.PropertyToID("_KalyeahTestColorCalibration");
            int[] modes =
            {
                SettingsStore.EnemyHighlightRed,
                SettingsStore.EnemyHighlightYellow,
                SettingsStore.EnemyHighlightPurple,
            };

            SettingsStore.Current.ColorblindMode = 0;
            for (int i = 0; i < modes.Length; i++)
            {
                SettingsStore.Current.EnemyHighlightMode = modes[i];
                MatchBoot.ApplyDeviceQualityAndFrameRate();
                Color expected = MatchBoot.ResolveEnemyHighlightColor(modes[i]);
                Shader.SetGlobalColor(calibrationId, expected);
                Color expectedShader = Shader.GetGlobalColor(calibrationId);
                Color actualShader = Shader.GetGlobalColor(globalHighlightId);
                Assert.Less(ColorRgbDistance(expectedShader, actualShader), 0.01f, $"Global enemy highlight mismatch for mode {modes[i]}");
            }
        }

        [Test]
        public void ValorantClarityShaders_AllSixResolveExposeExpectedPropertiesAndLowVariant()
        {
            AssertClarityShader(
                "Kalyeah/Kid",
                "KalyeahKid.shader",
                new[]
                {
                    "_DiffShadowColor",
                    "_DiffMidPos",
                    "_DiffHighPos",
                    "_DiffShadowLift",
                    "_SkinWarmShift",
                    "_SkinUnderglowColor",
                    "_SkinUnderglowStrength",
                    "_SpecStrength",
                    "_SpecSkyColor",
                    "_SpecGroundColor",
                    "_SpecSunBoost",
                    "_SpecIndirectShadow",
                    "_AmbientMin",
                    "_AmbientMax",
                    "_SkyBounceColor",
                    "_GroundBounceColor",
                    "_SunRimColor",
                    "_SunRimStrength",
                    "_SunRimPower",
                    "_TeamRimColor",
                    "_TeamRimStrength",
                    "_VertGradMin",
                    "_VertGradMax",
                    "_VertGradStrength",
                    "_FresnelUpWeight",
                    "_FresnelHeightMin",
                    "_FresnelHeightMax",
                    "_FresnelPower",
                    "_EnemyHighlight",
                    "_EnemyHighlightColor",
                    "_AllyFresnelColor",
                    "_AllyFresnelStrength",
                    "_DepthStart",
                    "_DepthEnd",
                    "_DepthBrightenMax",
                    "_DepthFresnelBoost",
                    "_HitFlash",
                    "_DecayTint",
                    "_BlindDesat",
                    "_StealthAlpha",
                    "_StealthRim",
                    "_UltGlow",
                    "_RevealPulse",
                });

            AssertClarityShader(
                "Kalyeah/TeamOutline",
                "TeamOutline.shader",
                new[]
                {
                    "_OutlineColor",
                    "_OutlineWidth",
                    "_DistanceComp",
                    "_DistanceBoost",
                    "_DepthStart",
                    "_DepthEnd",
                    "_MaxDistance",
                    "_HitFlash",
                    "_AuraColor",
                    "_RevealPulse",
                    "_UltGlow",
                    "_StealthAlpha",
                });

            AssertClarityShader(
                "Kalyeah/BarangayGround",
                "BarangayGround.shader",
                new[]
                {
                    "_AmbientMin",
                    "_AmbientMax",
                    "_DetailStrength",
                    "_SpecStrength",
                    "_BaseColor",
                    "_ShadowFloor",
                });

            AssertClarityShader(
                "Kalyeah/EsteroWater",
                "EsteroWater.shader",
                new[]
                {
                    "_SunHotspotPower",
                    "_SunHotspotStrength",
                    "_FresnelColor",
                    "_FresnelStrength",
                    "_AmbientMin",
                    "_AmbientMax",
                    "_ShallowColor",
                    "_DeepColor",
                    "_FoamColor",
                });

            AssertClarityShader(
                "Kalyeah/TropicalSky",
                "TropicalSky.shader",
                new[]
                {
                    "_SunDiscSize",
                    "_HorizonClarityBand",
                    "_SkyZenith",
                    "_SkyHorizon",
                    "_Exposure",
                });

            AssertClarityShader(
                "Kalyeah/ArcaneFx",
                "ArcaneFx.shader",
                new[]
                {
                    "_HardEdgeWidth",
                    "_DashCount",
                    "_EdgeBoost",
                    "_InteriorAlpha",
                    "_BaseColor",
                    "_Tint",
                    "_Phase",
                });
        }

        [Test]
        public void Phase5_KidAndOutlineShaders_StencilMaskAndSrpBatcherCbufferAreConsistent()
        {
            string kidPath = Path.Combine(Application.dataPath, "Kalyeah", "Shaders", "KalyeahKid.shader");
            string outlinePath = Path.Combine(Application.dataPath, "Kalyeah", "Shaders", "TeamOutline.shader");
            Assert.IsTrue(File.Exists(kidPath), "KalyeahKid.shader must exist");
            Assert.IsTrue(File.Exists(outlinePath), "TeamOutline.shader must exist");

            string kidSrc = File.ReadAllText(kidPath);
            string outlineSrc = File.ReadAllText(outlinePath);

            // Stencil Ref 4 write on Kalyeah/Kid and NotEqual mask on Kalyeah/TeamOutline prevent interior face/ear bleed.
            Assert.IsTrue(kidSrc.Contains("Ref 4"), "KalyeahKid.shader must write Stencil Ref 4");
            Assert.IsTrue(kidSrc.Contains("Comp Always"), "KalyeahKid.shader Stencil must use Comp Always");
            Assert.IsTrue(kidSrc.Contains("Pass Replace"), "KalyeahKid.shader Stencil must use Pass Replace");

            Assert.IsTrue(outlineSrc.Contains("Ref 4"), "TeamOutline.shader must test Stencil Ref 4");
            Assert.IsTrue(outlineSrc.Contains("Comp NotEqual"), "TeamOutline.shader Stencil must reject Ref 4 via Comp NotEqual");

            // Verify every Phase 5 gameplay state & clarity uniform lives inside CBUFFER_START(UnityPerMaterial).
            int cbStart = kidSrc.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int cbEnd = kidSrc.IndexOf("CBUFFER_END", cbStart >= 0 ? cbStart : 0, StringComparison.Ordinal);
            Assert.Greater(cbStart, 0, "KalyeahKid.shader must declare UnityPerMaterial CBUFFER");
            Assert.Greater(cbEnd, cbStart, "KalyeahKid.shader must close UnityPerMaterial CBUFFER");
            string kidCbuffer = kidSrc.Substring(cbStart, cbEnd - cbStart);

            string[] requiredCbufferUniforms =
            {
                "_SkyBounceColor",
                "_GroundBounceColor",
                "_SunRimColor",
                "_SunRimStrength",
                "_TeamRimColor",
                "_TeamRimStrength",
                "_VertGradMin",
                "_VertGradMax",
                "_VertGradStrength",
                "_HitFlash",
                "_DecayTint",
                "_BlindDesat",
                "_StealthAlpha",
                "_StealthRim",
                "_UltGlow",
                "_RevealPulse",
            };
            for (int i = 0; i < requiredCbufferUniforms.Length; i++)
            {
                Assert.IsTrue(
                    kidCbuffer.Contains(requiredCbufferUniforms[i]),
                    $"KalyeahKid.shader UnityPerMaterial CBUFFER missing '{requiredCbufferUniforms[i]}'");
            }
        }

        [Test]
        public void Phase5_KidView_SetCombatVisualStatesIsDirtyOnlyAndContactShadowPersistsOnAllTiers()
        {
            var snap = new KidSnapshot
            {
                Id = 11,
                Team = 0,
                CharacterId = "narda",
                Position = new SVector2(0f, 0f),
                Facing = new SVector2(0f, -1f),
                Hp = 100,
            };

            var kid = new KidView(_testRoot.transform, snap, 0.82f, new Color(0.22f, 0.56f, 0.98f, 1f), null);

            // Grounded contact shadow must stay active on ALL four quality tiers (0.26 with RT shadows, 0.42 on Low/airborne).
            kid.ApplyGraphicsProfile(GraphicsTierProfile.High, cameraDistanceMeters: 6f, visible: true);
            kid.SyncState(snap.Position, 0f, snap.Facing, 0f, false, false, false, 0, false, 0f, "", false, 0.016f);
            Assert.IsTrue(kid.IsContactShadowVisible, "Contact shadow must remain visible on High tier");
            Assert.AreEqual(KidView.ContactShadowAlphaRt, kid.ActiveContactShadowAlpha, 0.001f);

            kid.ApplyGraphicsProfile(GraphicsTierProfile.Medium, cameraDistanceMeters: 6f, visible: true);
            Assert.IsTrue(kid.IsContactShadowVisible, "Contact shadow must remain visible on Medium tier");
            Assert.AreEqual(KidView.ContactShadowAlphaRt, kid.ActiveContactShadowAlpha, 0.001f);

            kid.ApplyGraphicsProfile(GraphicsTierProfile.Ultra, cameraDistanceMeters: 6f, visible: true);
            Assert.IsTrue(kid.IsContactShadowVisible, "Contact shadow must remain visible on Ultra tier");
            Assert.AreEqual(KidView.ContactShadowAlphaRt, kid.ActiveContactShadowAlpha, 0.001f);

            // Airborne bumps alpha to 0.42 even on High/Ultra
            kid.SyncState(snap.Position, 0.45f, snap.Facing, 0f, false, false, false, 0, false, 0f, "", false, 0.016f);
            Assert.IsTrue(kid.IsContactShadowVisible);
            Assert.AreEqual(KidView.ContactShadowAlphaBlob, kid.ActiveContactShadowAlpha, 0.001f);

            // Low tier grounded also uses 0.42 blob shadow
            kid.ApplyGraphicsProfile(GraphicsTierProfile.Low, cameraDistanceMeters: 6f, visible: true);
            kid.SyncState(snap.Position, 0f, snap.Facing, 0f, false, false, false, 0, false, 0f, "", false, 0.016f);
            Assert.IsTrue(kid.IsContactShadowVisible, "Contact shadow must remain visible on Low tier");
            Assert.AreEqual(KidView.ContactShadowAlphaBlob, kid.ActiveContactShadowAlpha, 0.001f);

            // Dirty-only zero-GC SetCombatVisualStates: first change applies once, repeated identical calls apply zero times.
            Color decay = new Color(0.46f, 0.36f, 0.68f, 0.55f);
            int before = kid.MaterialBlockApplyCount;
            kid.SetCombatVisualStates(0.6f, 0.25f, 0.4f, 0.75f, 0.5f, decay);
            int afterFirst = kid.MaterialBlockApplyCount;
            Assert.AreEqual(before + 1, afterFirst, "First state change must apply MaterialPropertyBlock once");

            for (int i = 0; i < 64; i++)
                kid.SetCombatVisualStates(0.6f, 0.25f, 0.4f, 0.75f, 0.5f, decay);
            Assert.AreEqual(afterFirst, kid.MaterialBlockApplyCount, "Identical calls must skip redundant MaterialPropertyBlock updates");

            // Clearing back to neutral ally defaults applies once and restores clean fast-path.
            kid.SetCombatVisualStates(0f, 0f, 0f, 0f, 0f, Color.clear);
            Assert.AreEqual(afterFirst + 1, kid.MaterialBlockApplyCount);
        }

        [Test]
        public void Phase5_FacialBlendShapesAndHeroStageStudioRig_MissingSafeAndDeterministic()
        {
            // Missing-safe lookup on null / empty / zero-blendshape mesh never throws.
            Assert.IsFalse(KidView.TryFindFaceBlendShapes((GameObject)null, out FaceBlendShapeBinding nullBind));
            Assert.IsFalse(nullBind.HasAnyShape);

            var emptyGo = new GameObject("NoBlendShapes");
            emptyGo.transform.SetParent(_testRoot.transform, false);
            Assert.IsFalse(KidView.TryFindFaceBlendShapes(emptyGo, out _));

            // Synthetic mesh with Phase 5 glTF morph targets: Blink, Smile, Determined, Talk_Open.
            var faceGo = new GameObject("SyntheticFace");
            faceGo.transform.SetParent(_testRoot.transform, false);
            SkinnedMeshRenderer smr = faceGo.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "SyntheticFaceMesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            Vector3[] dv = { Vector3.zero, new Vector3(0f, 0.01f, 0f), Vector3.zero };
            mesh.AddBlendShapeFrame("Blink", 100f, dv, null, null);
            mesh.AddBlendShapeFrame("Smile", 100f, dv, null, null);
            mesh.AddBlendShapeFrame("Determined", 100f, dv, null, null);
            mesh.AddBlendShapeFrame("Talk_Open", 100f, dv, null, null);
            smr.sharedMesh = mesh;

            Assert.IsTrue(KidView.TryFindFaceBlendShapes(faceGo, out FaceBlendShapeBinding found));
            Assert.IsTrue(found.HasAnyShape);
            Assert.IsTrue(found.HasBlink);
            Assert.AreEqual(0, found.Blink);
            Assert.AreEqual(1, found.Smile);
            Assert.AreEqual(2, found.Determined);
            Assert.AreEqual(3, found.TalkOpen);

            KidView.SetFaceShapeWeightSafe(in found, found.Smile, 35f);
            Assert.AreEqual(35f, smr.GetBlendShapeWeight(found.Smile), 0.01f);
            // Out-of-range index is a safe no-op
            KidView.SetFaceShapeWeightSafe(in found, 99, 50f);
            KidView.SetFaceShapeWeightSafe(in found, -1, 50f);

            // HeroStage studio rig: 1 Directional Key + 1 Spot Rim + 1 Point Fill (zero dead multi-directional bug).
            var prevAmbientMode = RenderSettings.ambientMode;
            Color prevSky = RenderSettings.ambientSkyColor;
            Color prevEq = RenderSettings.ambientEquatorColor;
            Color prevGround = RenderSettings.ambientGroundColor;
            try
            {
                using (var stage = new HeroStage())
                {
                    Assert.IsNotNull(stage.StageRoot);
                    Light[] lights = stage.StageRoot.GetComponentsInChildren<Light>(true);
                    int dirCount = 0;
                    int spotCount = 0;
                    int pointCount = 0;
                    for (int i = 0; i < lights.Length; i++)
                    {
                        if (lights[i].type == LightType.Directional) dirCount++;
                        else if (lights[i].type == LightType.Spot) spotCount++;
                        else if (lights[i].type == LightType.Point) pointCount++;
                    }
                    Assert.AreEqual(1, dirCount, "HeroStage must use exactly 1 Directional Key light in URP");
                    Assert.AreEqual(1, spotCount, "HeroStage must include 1 cool Spot Rim light");
                    Assert.AreEqual(1, pointCount, "HeroStage must include 1 warm Point Fill light");

                    stage.SetKid("narda");
                    SettingsStore.Current.ReduceMotion = true;
                    stage.Tick(0.10f);
                    SettingsStore.Current.ReduceMotion = false;
                    stage.Tick(0.10f);
                    Assert.AreEqual(0f, stage.OrbitYaw, 0.001f, "EditMode HeroStage.Tick must keep deterministic front pose");
                }
            }
            finally
            {
                RenderSettings.ambientMode = prevAmbientMode;
                RenderSettings.ambientSkyColor = prevSky;
                RenderSettings.ambientEquatorColor = prevEq;
                RenderSettings.ambientGroundColor = prevGround;
            }

            KalyeahResources.Kill(mesh);
        }

        [Test]
        public void Phase5_KidView_CompetitiveHitboxCapsules_CoverAllZonesZeroPhysicsAndParseJson()
        {
            Assert.AreEqual(1.5f, KidView.GetHitboxZoneMultiplier(HitboxZone.Head), 0.001f);
            Assert.AreEqual(1.0f, KidView.GetHitboxZoneMultiplier(HitboxZone.Chest), 0.001f);
            Assert.AreEqual(1.0f, KidView.GetHitboxZoneMultiplier(HitboxZone.Stomach), 0.001f);
            Assert.AreEqual(0.85f, KidView.GetHitboxZoneMultiplier(HitboxZone.Limb), 0.001f);

            var snap = new KidSnapshot
            {
                Id = 19,
                Team = 1,
                CharacterId = "narda",
                Position = new SVector2(0f, 0f),
                Facing = new SVector2(0f, -1f),
                Hp = 100,
            };

            var kid = new KidView(_testRoot.transform, snap, 0.82f, new Color(0.95f, 0.25f, 0.25f, 1f), null);

            // Zero runtime Physics Collider overhead on KidView hierarchy.
            Collider[] colliders = kid.Root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(0, colliders.Length, "Hitbox registry must attach zero runtime Unity Colliders");

            Assert.IsNotNull(kid.HitboxCapsules);
            Assert.GreaterOrEqual(kid.HitboxCapsules.Count, 4);

            bool hasHead = false, hasChest = false, hasStomach = false, hasLimb = false;
            Vector3 headCenter = Vector3.zero;
            for (int i = 0; i < kid.HitboxCapsules.Count; i++)
            {
                HitboxCapsuleEntry cap = kid.HitboxCapsules[i];
                Assert.IsTrue(cap.IsValid, $"Hitbox capsule '{cap.Name}' must be valid");
                Assert.AreEqual(KidView.GetHitboxZoneMultiplier(cap.Zone), cap.DamageMultiplier, 0.001f);
                if (cap.Zone == HitboxZone.Head)
                {
                    hasHead = true;
                    cap.TryGetWorldSegment(out Vector3 a, out Vector3 b);
                    headCenter = (a + b) * 0.5f;
                }
                else if (cap.Zone == HitboxZone.Chest) hasChest = true;
                else if (cap.Zone == HitboxZone.Stomach) hasStomach = true;
                else if (cap.Zone == HitboxZone.Limb) hasLimb = true;
            }
            Assert.IsTrue(hasHead, "Hitbox registry must include Head zone");
            Assert.IsTrue(hasChest, "Hitbox registry must include Chest zone");
            Assert.IsTrue(hasStomach, "Hitbox registry must include Stomach zone");
            Assert.IsTrue(hasLimb, "Hitbox registry must include Limb zones");

            // Analytic ray query directly through head center resolves Head with 1.5x multiplier.
            Vector3 rayOrigin = headCenter + Vector3.back * 3f;
            Assert.IsTrue(kid.RaycastHitboxes(rayOrigin, Vector3.forward, 6f, out HitboxZone hitZone, out float hitDist, out float hitMult));
            Assert.AreEqual(HitboxZone.Head, hitZone);
            Assert.AreEqual(1.5f, hitMult, 0.001f);
            Assert.Greater(hitDist, 0f);
            Assert.Less(hitDist, 4f);

            // Optional rig__hitbox_capsules.json parsing against synthetic mixamorig bones + malformed fallback.
            var mockRig = new GameObject("MockMixamoRig");
            mockRig.transform.SetParent(_testRoot.transform, false);
            var bHead = new GameObject("mixamorig:Head");
            bHead.transform.SetParent(mockRig.transform, false);
            bHead.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            var bChest = new GameObject("mixamorig:Spine2");
            bChest.transform.SetParent(mockRig.transform, false);
            bChest.transform.localPosition = new Vector3(0f, 1.05f, 0f);

            const string sampleHitboxJson = @"{
              ""data"": {
                ""coordSpace"": ""gltf_yup"",
                ""multipliers"": { ""Head"": 1.5, ""Chest"": 1.0, ""Stomach"": 1.0, ""Limb"": 0.85 },
                ""capsules"": [
                  { ""name"": ""Head"", ""zone"": ""Head"", ""bone"": ""mixamorig:Head"", ""a"": [0, 1.32, 0], ""b"": [0, 1.50, 0], ""radius"": 0.12 },
                  { ""name"": ""Chest"", ""zone"": ""Chest"", ""bone"": ""mixamorig:Spine2"", ""a"": [0, 0.95, 0], ""b"": [0, 1.24, 0], ""radius"": 0.15 }
                ]
              }
            }";

            HitboxCapsuleEntry[] parsed = KidView.BuildHitboxCapsulesFromJson(mockRig.transform, mockRig.transform, sampleHitboxJson);
            Assert.AreEqual(2, parsed.Length);
            Assert.AreEqual(HitboxZone.Head, parsed[0].Zone);
            Assert.AreEqual(1.5f, parsed[0].DamageMultiplier, 0.001f);
            Assert.AreEqual(HitboxZone.Chest, parsed[1].Zone);
            Assert.AreEqual(1.0f, parsed[1].DamageMultiplier, 0.001f);

            // Malformed JSON safely falls back to deterministic bone capsules without throwing.
            HitboxCapsuleEntry[] fallback = KidView.BuildHitboxCapsulesFromJson(mockRig.transform, mockRig.transform, "{broken");
            Assert.GreaterOrEqual(fallback.Length, 2);
        }

        static float ColorRgbDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        static void AssertClarityShader(string shaderName, string shaderFileName, string[] expectedProperties)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, $"Shader '{shaderName}' must resolve cleanly via Shader.Find");

            for (int i = 0; i < expectedProperties.Length; i++)
            {
                string prop = expectedProperties[i];
                Assert.GreaterOrEqual(
                    shader.FindPropertyIndex(prop),
                    0,
                    $"Shader '{shaderName}' is missing expected VALORANT clarity property '{prop}'");
            }

            string shaderPath = Path.Combine(Application.dataPath, "Kalyeah", "Shaders", shaderFileName);
            Assert.IsTrue(File.Exists(shaderPath), $"Expected shader file at '{shaderPath}'");
            string source = File.ReadAllText(shaderPath);
            Assert.IsTrue(
                source.Contains("#pragma multi_compile _ " + MatchBoot.LowQualityShaderKeyword),
                $"Shader '{shaderFileName}' must declare '#pragma multi_compile _ {MatchBoot.LowQualityShaderKeyword}' so Low scaling never strips");
        }
    }
}
