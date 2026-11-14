using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class AssetAndSpaceTests
    {
        [Test]
        public void GodotSpace_NegZToPosZ_RoundtripsGroundWorldAndAxes()
        {
            SVector2[] samples =
            {
                new SVector2(0f, 0f),
                new SVector2(14.5f, -32.25f),
                new SVector2(-41.75f, 58.5f),
                new SVector2(34f, 26f),
            };

            for (int i = 0; i < samples.Length; i++)
            {
                SVector2 g = samples[i];
                SVector3 u = GodotSpace.World(g, 1.52f);
                Assert.AreEqual(g.X, u.X, 0.0001f);
                Assert.AreEqual(1.52f, u.Y, 0.0001f);
                Assert.AreEqual(-g.Y, u.Z, 0.0001f, "Godot -Z maps to Unity +Z");

                SVector2 back = GodotSpace.Ground(u);
                Assert.AreEqual(g.X, back.X, 0.0001f);
                Assert.AreEqual(g.Y, back.Y, 0.0001f);
            }

            // Yaw 0 in Godot looks down -Z -> Unity +Z (0, 0, +1)
            SVector3 fwd0 = GodotSpace.Forward(0f, 0f);
            Assert.AreEqual(0f, fwd0.X, 0.0001f);
            Assert.AreEqual(0f, fwd0.Y, 0.0001f);
            Assert.AreEqual(1f, fwd0.Z, 0.0001f);

            // Pitching down 30 deg tilts look vector downward while staying unit length
            SVector3 fwdPitch = GodotSpace.Forward(30f, 0f);
            Assert.AreEqual(1f, fwdPitch.Length(), 0.0001f);
            Assert.AreEqual(-0.5f, fwdPitch.Y, 0.0001f);
            Assert.Greater(fwdPitch.Z, 0.85f);

            // Pushing screen-way up (0, -1 in screen-down convention) at yaw 0 walks toward enemy end (0, -1 in sim XZ)
            SVector2 wayForward = GodotSpace.GroundWay(new SVector2(0f, -1f), 0f);
            Assert.AreEqual(0f, wayForward.X, 0.0001f);
            Assert.AreEqual(-1f, wayForward.Y, 0.0001f);

            // glTFast -X handedness compensation is 180 degrees at 0 rad
            Assert.AreEqual(180f, GodotSpace.ModelYawDegrees(0f), 0.0001f);
        }

        [Test]
        public void ShoulderRig_PoseAndYawToward_PlaceCameraBehindAndRightOfKid()
        {
            DataSource data = KalyeahResources.Data();
            CameraRulesData rules = CameraRulesData.Load(data);
            Assert.Greater(rules.ShoulderHeight, 0.5f);
            Assert.Greater(rules.ShoulderDistance, 1f);
            Assert.Greater(rules.ShoulderOffset, 0f, "Over-the-right-shoulder offset must be positive");

            SVector2 feet = new SVector2(4f, 20f); // Unity ground (4, 0, -20)
            var (eye, forward) = ShoulderRig.Pose(rules, feet, yawDegrees: 0f, pitchDegrees: rules.LookPitchDegrees);

            Assert.AreEqual(1f, forward.Length(), 0.0001f);
            Assert.Greater(forward.Z, 0.8f, "Yaw 0 looks along Unity +Z");
            Assert.Less(forward.Y, 0f, "Positive pitch looks downward");

            SVector3 unityFeet = GodotSpace.World(feet, 0f);
            Assert.Greater(eye.X, unityFeet.X, "Eye sits to the right (+X) of the kid at yaw 0");
            Assert.Less(eye.Z, unityFeet.Z, "Eye sits behind (-Z) the kid at yaw 0");
            Assert.Greater(eye.Y, rules.ShoulderHeight, "Positive pitch raises eye above shoulder pivot");

            Assert.AreEqual(0f, ShoulderRig.YawToward(new SVector2(0f, 10f), new SVector2(0f, -10f)), 0.01f);
            Assert.AreEqual(180f, ShoulderRig.YawToward(new SVector2(0f, -10f), new SVector2(0f, 10f)), 0.01f);
            Assert.AreEqual(270f, ShoulderRig.YawToward(new SVector2(0f, 0f), new SVector2(10f, 0f)), 0.01f);
            Assert.AreEqual(90f, ShoulderRig.YawToward(new SVector2(0f, 0f), new SVector2(-10f, 0f)), 0.01f);
        }

        [Test]
        public void KalyeahResources_DataJsonAndI18nStrings_ArePresentAndValid()
        {
            DataSource data = KalyeahResources.Data();

            string[] requiredRules =
            {
                "rules__game_rules",
                "rules__map_layout",
                "rules__camera_rules",
                "rules__net_rules",
                "rules__street_games",
                "rules__patintero",
                "rules__tumbang_preso",
                "rules__langit_lupa",
                "rules__larong_kalye",
            };

            for (int i = 0; i < requiredRules.Length; i++)
            {
                var props = data.Props(requiredRules[i]);
                Assert.IsNotNull(props, $"Missing parsed properties for {requiredRules[i]}");
                Assert.Greater(props.Count, 0, $"Empty properties in {requiredRules[i]}");
            }

            string[] kids = data.CharacterIds();
            Assert.AreEqual(10, kids.Length, "GameRules must reference all 10 Filipino kids");
            for (int i = 0; i < kids.Length; i++)
            {
                var charProps = data.Props("characters__" + kids[i]);
                Assert.AreEqual(kids[i], DataSource.S(charProps, "id"));
            }

            TextAsset i18n = Resources.Load<TextAsset>("Data/i18n__strings");
            Assert.IsNotNull(i18n, "Data/i18n__strings.csv must be synced into Resources/Data/");
            Assert.Greater(i18n.text.Length, 100, "i18n__strings.csv should contain localization rows");
        }

        [Test]
        public void SyncedGlbModels_CharactersBarangayAndWeapons_LoadAsPrefabs()
        {
            foreach (string kid in AppSession.DefaultRoster)
            {
                GameObject model = KalyeahResources.Model("characters", kid);
                Assert.IsNotNull(model, $"Missing character .glb prefab: models/characters/{kid}");
                Assert.IsNotEmpty(model.GetComponentsInChildren<Renderer>(true), $"Character {kid} has no Renderer");
            }

            string[] barangayLandmarks =
            {
                "brgy_covered_court",
                "brgy_hall_plaza",
                "brgy_waiting_shed",
                "brgy_sari_sari",
                "brgy_merienda_stand",
                "brgy_post_blue",
                "brgy_post_red",
            };
            for (int i = 0; i < barangayLandmarks.Length; i++)
            {
                GameObject prop = KalyeahResources.Model("barangay", barangayLandmarks[i]);
                Assert.IsNotNull(prop, $"Missing barangay .glb prefab: models/barangay/{barangayLandmarks[i]}");
            }

            string[] weaponModels =
            {
                "tirador", "slipper", "can", "trumpo", "paper", "crumpled_paper",
                "meteor", "scissor", "spikes", "marble", "green_cross", "soil_block",
            };
            for (int i = 0; i < weaponModels.Length; i++)
            {
                GameObject weapon = KalyeahResources.Model("weapons", weaponModels[i]);
                Assert.IsNotNull(weapon, $"Missing weapon .glb prefab: models/weapons/{weaponModels[i]}");
            }
        }

        [Test]
        public void SyncedAudioClips_BgmAndSfx_LoadCleanly()
        {
            string[] audioPaths =
            {
                "audio/bgm",
                "audio/sfx/bato_heavy_boom",
                "audio/sfx/card_pick",
                "audio/sfx/victory",
            };

            for (int i = 0; i < audioPaths.Length; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>(audioPaths[i]);
                Assert.IsNotNull(clip, $"Missing AudioClip at Resources/{audioPaths[i]}");
                Assert.Greater(clip.length, 0f, $"AudioClip {audioPaths[i]} has zero length");
            }
        }

        [Test]
        public void SyncedTextures_IncludingConvertedWebpUiAndPortraits_LoadAsTexture2D()
        {
            // Includes textures originally authored as .webp in Godot (main_bg, character_cards, skill_cards, mode_pictures, portraits/cards)
            string[] texturePaths =
            {
                "ui/main_bg",
                "ui/brand/kalyeah_wordmark",
                "ui/character_cards/junjun",
                "ui/character_cards/tala",
                "ui/skill_cards/bato_light",
                "ui/skill_cards/tsinelas_light",
                "ui/mode_pictures/practice",
                "portraits/junjun",
                "portraits/tala",
                "portraits/cards/ligaya",
                "portraits/faces/junjun",
                "portraits/faces/migo",
            };

            for (int i = 0; i < texturePaths.Length; i++)
            {
                Texture2D tex = KalyeahResources.Texture(texturePaths[i]);
                Assert.IsNotNull(tex, $"Missing Texture2D (or unconverted .webp) at Resources/{texturePaths[i]}");
                Assert.Greater(tex.width, 0);
                Assert.Greater(tex.height, 0);
            }

            string importedResourcesDir = Path.Combine(Application.dataPath, "Imported", "Resources");
            if (Directory.Exists(importedResourcesDir))
            {
                string[] leftoverWebp = Directory.GetFiles(importedResourcesDir, "*.webp", SearchOption.AllDirectories);
                Assert.IsEmpty(leftoverWebp, "sync_assets must convert all .webp images into .png for Unity Resources.Load<Texture2D>");
            }
        }
    }
}
