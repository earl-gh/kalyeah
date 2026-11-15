using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class AssetAndSpaceTests
    {
        static readonly string[] All32SfxClips =
        {
            "bato_heavy_boom",
            "bato_meteor_fall",
            "bola_bounce",
            "bola_throw",
            "card_cancel",
            "card_pick",
            "card_ready",
            "fall_thud",
            "gunting_big_charge",
            "gunting_big_snip",
            "gunting_light",
            "heal",
            "jacks_scatter",
            "lata_hit",
            "lata_poof",
            "lata_toss",
            "laugh_at_fall_thud",
            "paper_block",
            "paper_shield",
            "slowed_sting",
            "soil_block",
            "soil_expand",
            "soil_rise",
            "stunned",
            "trap_place",
            "trap_spring",
            "trumpo",
            "tsinelas_hit",
            "tsinelas_hit_heavy",
            "tsinelas_throw_heavy",
            "tsinelas_throw_light",
            "victory",
        };

        static (int width, int height) ReadSourceTextureSize(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti)
            {
                ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w > 0 && h > 0)
                    return (w, h);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string diskPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(diskPath))
            {
                byte[] header = new byte[24];
                using (FileStream fs = File.OpenRead(diskPath))
                {
                    if (fs.Read(header, 0, 24) == 24 && header[0] == 0x89 && header[1] == 0x50)
                    {
                        int w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                        int h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                        return (w, h);
                    }
                }
            }
            return (0, 0);
        }

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
            Assert.AreEqual(2.55f, rules.ShoulderDistance, 0.001f, "2.55 m close Deadlock shoulder distance");
            Assert.AreEqual(1.42f, rules.ShoulderHeight, 0.001f, "1.42 m low-shoulder pivot height");
            Assert.AreEqual(0.68f, rules.ShoulderOffset, 0.001f, "0.68 m right-shoulder offset");
            Assert.AreEqual(9.5f, rules.LookPitchDegrees, 0.001f, "9.5 deg street-canyon pitch");
            Assert.AreEqual(66f, rules.ShoulderFov, 0.001f, "66 deg vertical perspective FOV");

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
            CollectionAssert.AreEqual(AppSession.DefaultRoster, kids, "Canonical 10-kid order must match AppSession.DefaultRoster");
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
        public void AllTenKids_PortraitsFacesCardsAndCharacterCards_ExistWithCellProportions()
        {
            DataSource data = KalyeahResources.Data();
            string[] kids = data.CharacterIds();
            Assert.AreEqual(10, kids.Length);

            float? expectedCardAspect = null;
            for (int i = 0; i < kids.Length; i++)
            {
                string kid = kids[i];
                Assert.IsNotNull(KalyeahResources.Texture("portraits/" + kid), $"Missing portrait: portraits/{kid}");
                Assert.IsNotNull(KalyeahResources.Texture("portraits/faces/" + kid), $"Missing HUD face: portraits/faces/{kid}");
                Assert.IsNotNull(KalyeahResources.Texture("portraits/cards/" + kid), $"Missing card portrait: portraits/cards/{kid}");

                Texture2D charCard = KalyeahResources.Texture("ui/character_cards/" + kid);
                Assert.IsNotNull(charCard, $"Missing character card picture: ui/character_cards/{kid}");

                var (srcW, srcH) = ReadSourceTextureSize($"Assets/Imported/Resources/ui/character_cards/{kid}.png");
                Assert.Greater(srcW, 0, $"Invalid width for ui/character_cards/{kid}.png");
                Assert.LessOrEqual(srcW, 520, $"{kid} character card width ({srcW}) must be downscaled <= 520 px for mobile");
                float aspect = srcW / (float)srcH;
                if (!expectedCardAspect.HasValue)
                    expectedCardAspect = aspect;
                Assert.AreEqual(expectedCardAspect.Value, aspect, 0.015f, $"{kid} character card aspect matches uniform cell aspect");
            }
        }

        [Test]
        public void AllFourteenDeckWeapons_SkillCardsHudIconsAndCastHitSfx_AreComplete()
        {
            DataSource data = KalyeahResources.Data();
            var sim = new SimBridge(data, new MatchSetup { Seed = 1L, TrainingMode = true });
            IReadOnlyList<WeaponInfo> deckWeapons = sim.AllWeapons;
            Assert.AreEqual(14, deckWeapons.Count, "All 14 deck superpowers must be loaded in SimBridge.AllWeapons");

            for (int i = 0; i < deckWeapons.Count; i++)
            {
                string wid = deckWeapons[i].Id;
                Assert.IsFalse(string.IsNullOrEmpty(wid));

                Texture2D cardTex = KalyeahResources.Texture("ui/skill_cards/" + wid);
                Assert.IsNotNull(cardTex, $"Missing skill card picture: ui/skill_cards/{wid}");

                var (srcW, srcH) = ReadSourceTextureSize($"Assets/Imported/Resources/ui/skill_cards/{wid}.png");
                Assert.That(srcW, Is.InRange(400, 640), $"{wid} skill card width ({srcW}) must be trimmed in [400..640]");
                float aspect = srcW / (float)srcH;
                Assert.That(aspect, Is.InRange(0.74f, 0.79f), $"{wid} skill card aspect ({aspect:F4}) must sit in [0.74..0.79]");

                Texture2D hudOrArt = KalyeahResources.Texture("ui/hud_icons/" + wid)
                                  ?? KalyeahResources.Texture("icons/art/" + wid)
                                  ?? KalyeahResources.Texture("icons/" + wid);
                Assert.IsNotNull(hudOrArt, $"Missing HUD/icon art for weapon {wid}");

                string castClip = AudioHub.ResolveWeaponCastClip(wid, out float castPitch);
                Assert.IsFalse(string.IsNullOrEmpty(castClip), $"AudioHub.ResolveWeaponCastClip missing entry for {wid}");
                Assert.Greater(castPitch, 0f);
                Assert.IsNotNull(Resources.Load<AudioClip>("audio/sfx/" + castClip), $"Missing cast AudioClip audio/sfx/{castClip} for {wid}");
            }

            // Primary Tirador + Ultimate Bagsak ng Kalye cast clips
            string primId = sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.Id : "tirador_bato";
            string primCast = AudioHub.ResolveWeaponCastClip(primId, out _);
            Assert.IsFalse(string.IsNullOrEmpty(primCast));
            Assert.IsNotNull(Resources.Load<AudioClip>("audio/sfx/" + primCast));

            string ultId = sim.UltimateWeaponInfo.HasValue ? sim.UltimateWeaponInfo.Value.Id : "ult_bagsak";
            string ultCast = AudioHub.ResolveWeaponCastClip(ultId, out _);
            Assert.IsFalse(string.IsNullOrEmpty(ultCast));
            Assert.IsNotNull(Resources.Load<AudioClip>("audio/sfx/" + ultCast));

            // Per-weapon hit SFX differentiation (test_sfx_view.gd:65-69)
            Assert.AreEqual("tsinelas_hit_heavy", AudioHub.ResolveHitClip(false, "tsinelas_heavy", out _));
            Assert.AreEqual("tsinelas_hit", AudioHub.ResolveHitClip(false, "tsinelas_light", out _));
            Assert.AreEqual("bato_heavy_boom", AudioHub.ResolveHitClip(false, "bato_heavy", out _));
            Assert.AreEqual("lata_hit", AudioHub.ResolveHitClip(false, "lata", out _));
            Assert.AreEqual("tsinelas_hit", AudioHub.ResolveHitClip(false, "bato_light", out float batoPitch));
            Assert.AreEqual(0.75f, batoPitch, 0.001f);
        }

        [Test]
        public void SyncedAudioClips_BgmAndAll32Sfx_LoadCleanly()
        {
            AudioClip bgm = Resources.Load<AudioClip>("audio/bgm");
            Assert.IsNotNull(bgm, "Missing AudioClip at Resources/audio/bgm");
            Assert.Greater(bgm.length, 0f, "BGM clip has zero length");

            for (int i = 0; i < All32SfxClips.Length; i++)
            {
                string path = "audio/sfx/" + All32SfxClips[i];
                AudioClip clip = Resources.Load<AudioClip>(path);
                Assert.IsNotNull(clip, $"Missing AudioClip at Resources/{path}");
                Assert.Greater(clip.length, 0f, $"AudioClip {path} has zero length");
            }
        }

        [Test]
        public void AllSixQuickChatStickers_AndConvertedWebpTextures_LoadWithZeroLeftoverWebp()
        {
            Assert.AreEqual(6, MatchOverlaysView.StickerSlugs.Length);
            for (int i = 0; i < MatchOverlaysView.StickerSlugs.Length; i++)
            {
                string slug = MatchOverlaysView.StickerSlugs[i];
                Texture2D sticker = KalyeahResources.Texture("ui/stickers/" + slug);
                Assert.IsNotNull(sticker, $"Missing quick-chat sticker texture: ui/stickers/{slug}");
                Assert.Greater(sticker.width, 0);
                Assert.Greater(sticker.height, 0);
            }

            string[] coreUiPaths =
            {
                "ui/main_bg",
                "ui/brand/kalyeah_wordmark",
                "ui/mode_pictures/practice",
            };
            for (int i = 0; i < coreUiPaths.Length; i++)
            {
                Texture2D tex = KalyeahResources.Texture(coreUiPaths[i]);
                Assert.IsNotNull(tex, $"Missing Texture2D at Resources/{coreUiPaths[i]}");
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

        [Test]
        public void ModelTexturesAndNormalMaps_AreVramCompressedAndPresentForAllTenKids()
        {
            string[] modelFolders =
            {
                "Assets/Imported/Resources/models/barangay",
                "Assets/Imported/Resources/models/props",
                "Assets/Imported/Resources/models/weapons",
                "Assets/Imported/Resources/models/characters",
            };

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            int inspectedCount = 0;
            for (int f = 0; f < modelFolders.Length; f++)
            {
                string absDir = Path.Combine(projectRoot, modelFolders[f].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absDir)) continue;

                string[] files = Directory.GetFiles(absDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string ext = Path.GetExtension(files[i]);
                    if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string assetPath = modelFolders[f] + "/" + Path.GetFileName(files[i]);
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    Assert.IsNotNull(importer, $"Missing TextureImporter for {assetPath}");
                    Assert.AreNotEqual(
                        TextureImporterCompression.Uncompressed,
                        importer.textureCompression,
                        $"{assetPath} must use VRAM texture compression (never Uncompressed)");
                    inspectedCount++;
                }
            }
            Assert.Greater(inspectedCount, 40, "Should inspect > 40 3D model textures across barangay, props, weapons, and characters");

            foreach (string kid in AppSession.DefaultRoster)
            {
                string normalPath = $"Assets/Imported/Resources/models/characters/{kid}_2.png";
                var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                Assert.IsNotNull(normalImporter, $"Missing normal-map texture importer at {normalPath}");
                Assert.AreNotEqual(
                    TextureImporterCompression.Uncompressed,
                    normalImporter.textureCompression,
                    $"{kid}'s normal map ({normalPath}) must be compressed in VRAM");
                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                Assert.IsNotNull(normalTex, $"Failed to load {normalPath}");
                Assert.Greater(normalTex.width, 0);
            }
        }
    }
}
