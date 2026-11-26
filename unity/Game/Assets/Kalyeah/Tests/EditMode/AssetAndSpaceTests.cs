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
        public void Crosshair_ReachReadout_SaysWhetherTheStoneGetsThere()
        {
            // Spec tirador: a tap reaches 18 m, a full charge 30 m.
            Assert.AreEqual(0, CrosshairView.ClassifyReach(12f, 18f, 30f), "inside a tap's reach");
            Assert.AreEqual(1, CrosshairView.ClassifyReach(24f, 18f, 30f), "only a charged shot gets there: HOLD");
            Assert.AreEqual(0, CrosshairView.ClassifyReach(24f, 25f, 30f), "charging far enough makes it reach");
            Assert.AreEqual(2, CrosshairView.ClassifyReach(35f, 18f, 30f), "past a full charge: out of reach");
        }

        [Test]
        public void SlingshotStone_FliesLevelThenDropsSmoothlyToTheStreetAtItsRange()
        {
            const float range = 30f, h = 0.72f;
            float dropStart = range * (1f - ProjectileViews.TiradorDropShare);
            Assert.AreEqual(h, ProjectileViews.TiradorFlightHeight(5f, range, h), 1e-5f, "level flight first");
            Assert.AreEqual(h, ProjectileViews.TiradorFlightHeight(dropStart, range, h), 1e-5f);
            Assert.AreEqual(0f, ProjectileViews.TiradorFlightSlope(dropStart, range, h), 1e-5f, "no kink where the drop starts");
            Assert.AreEqual(0f, ProjectileViews.TiradorFlightHeight(range, range, h), 1e-5f, "on the street at the range end");
            float prev = h;
            for (float d = dropStart; d <= range; d += 0.5f)
            {
                float y = ProjectileViews.TiradorFlightHeight(d, range, h);
                Assert.LessOrEqual(y, prev + 1e-5f, "it only goes down");
                prev = y;
            }
        }

        [Test]
        public void AimIndicator_TiradorArc_FollowsTheStonesCurveAndLandsAtTheRange()
        {
            var root = new GameObject("ArcTest");
            try
            {
                var view = new AimIndicatorView(root.transform);
                var feet = new SVector2(0f, 20f);
                view.ShowTiradorArc(feet, new SVector2(0f, -1f), 24f, 0.72f, Color.white);
                LineRenderer arc = null;
                foreach (LineRenderer l in root.GetComponentsInChildren<LineRenderer>(true))
                    if (l.name == "TiradorArc") arc = l;
                Assert.IsNotNull(arc);
                Assert.IsTrue(arc.gameObject.activeSelf);
                Vector3 first = arc.GetPosition(0), last = arc.GetPosition(arc.positionCount - 1);
                Assert.AreEqual(0.72f, first.y, 1e-4f, "starts at the stone's flight height");
                Assert.AreEqual(0f, last.y, 1e-4f, "ends on the street");
                Vector3 landing = KalyeahResources.World(feet + new SVector2(0f, -1f) * 24f, 0f);
                Assert.AreEqual(0f, Vector3.Distance(new Vector3(last.x, 0f, last.z), landing), 1e-3f, "lands at the charged range");
                view.HideTiradorArc();
                Assert.IsFalse(arc.gameObject.activeSelf);
                view.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SlingshotStone_ThatRanOutOfRange_IsToldApartFromAHit()
        {
            var flying = new WorldEffectSnapshot { WeaponId = "tirador_bato", MaxRange = 18f, Travelled = 17.2f };
            Assert.IsTrue(ProjectileViews.RanOutOfRange(flying), "removed one tick before 18 m: it ran out and lands in dust");
            var hit = new WorldEffectSnapshot { WeaponId = "tirador_bato", MaxRange = 18f, Travelled = 9f };
            Assert.IsFalse(ProjectileViews.RanOutOfRange(hit), "removed at 9 m: it hit something, no landing puff");
        }

        [Test]
        public void ShoulderCamera_FpsControls_OnlyASwipeTurnsTheView()
        {
            var shoulder = new ShoulderCamera(CameraRulesData.Load(KalyeahResources.Data()), 0);
            float yaw = shoulder.Yaw;
            for (int i = 0; i < 90; i++)
                shoulder.SyncLocalKid(facing: new SVector2(1f, 0f), speed01: 1f, sprintingOrDashing: false, crouchingOrSliding: false, airY: 0f, dt: 1f / 30f);
            Assert.AreEqual(yaw, shoulder.Yaw, 1e-4f, "3 s of running sideways leaves the view where the player put it");

            shoulder.Look(new Vector2(100f, 0f), 720f);
            Assert.AreNotEqual(yaw, shoulder.Yaw, "a swipe turns it");
        }

        [Test]
        public void ShoulderCamera_AimDownSight_MovesInAlongTheViewAndNarrowsTheFov()
        {
            CameraRulesData rules = CameraRulesData.Load(KalyeahResources.Data());
            Assert.Less(rules.AdsDistance, rules.ShoulderDistance, "ADS moves in close (docs/SPEC.md §7)");
            Assert.Less(rules.AdsFov, rules.ShoulderFov, "ADS narrows the view");

            var go = new GameObject("AdsCam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.aspect = 16f / 9f;
                var shoulder = new ShoulderCamera(rules, 0) { ReduceMotion = true, CollisionMask = 0 };
                SVector2 feet = new SVector2(0f, 20f);
                shoulder.Apply(cam, feet, 1f / 30f);
                Vector3 hipEye = cam.transform.position;
                Vector3 hipForward = cam.transform.forward;
                Assert.AreEqual(rules.ShoulderFov, cam.fieldOfView, 0.6f);

                shoulder.AimingDown = true;
                shoulder.Apply(cam, feet, 1f / 30f);
                Assert.AreEqual(1f, shoulder.AdsBlend, 1e-4f);
                Assert.AreEqual(rules.AdsFov, cam.fieldOfView, 0.6f);
                Vector3 push = cam.transform.position - hipEye;
                Assert.AreEqual(rules.ShoulderDistance - rules.AdsDistance, push.magnitude, 0.05f);
                Assert.Greater(Vector3.Dot(push.normalized, hipForward), 0.999f, "moves straight along the view: the kid keeps their place left of the crosshair");

                shoulder.AimingDown = false;
                shoulder.Apply(cam, feet, 1f / 30f);
                Assert.AreEqual(0f, shoulder.AdsBlend, 1e-4f);
                Assert.AreEqual(hipEye.x, cam.transform.position.x, 0.01f);
                Assert.AreEqual(hipEye.z, cam.transform.position.z, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AdsButton_TapTogglesOrHolds_AndCancelDropsIt()
        {
            Ui.EnsureEventSystem();
            var root = new GameObject("AdsRoot", typeof(RectTransform));
            try
            {
                AdsButton ads = AdsButton.Build(root.transform, 96f, interactive: true);
                var down = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 3 };

                ads.ToggleMode = true;
                ads.OnPointerDown(down);
                ads.OnPointerUp(down);
                Assert.IsTrue(ads.IsOn, "tap mode: a tap turns ADS on");
                ads.OnPointerDown(down);
                ads.OnPointerUp(down);
                Assert.IsFalse(ads.IsOn, "tap mode: a second tap turns it off");
                ads.OnPointerDown(down);
                ads.Cancel();
                Assert.IsFalse(ads.IsOn, "cancel drops a toggled aim");

                ads.ToggleMode = false;
                ads.OnPointerDown(down);
                Assert.IsTrue(ads.IsOn, "hold mode: on while held");
                ads.OnPointerUp(down);
                Assert.IsFalse(ads.IsOn, "hold mode: off on release");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShoulderRig_PoseAndYawToward_PlaceCameraBehindAndRightOfKid()
        {
            DataSource data = KalyeahResources.Data();
            CameraRulesData rules = CameraRulesData.Load(data);
            // Competitive AAA 3P right-shoulder framing: hero in lower-left third (~37% X) with open sightline past right shoulder
            Assert.AreEqual(4.15f, rules.ShoulderDistance, 0.001f, "4.15 m behind the kid");
            Assert.AreEqual(1.9f, rules.ShoulderHeight, 0.001f, "1.9 m pivot, a little above the kid");
            Assert.AreEqual(0.88f, rules.ShoulderOffset, 0.001f, "0.88 m to the right: hero sits cleanly in the lower-left third");
            Assert.AreEqual(12f, rules.LookPitchDegrees, 0.001f, "12 deg looking down over the street");
            Assert.AreEqual(50f, rules.ShoulderFov, 0.001f, "50 deg vertical FOV: a kid at 20 m stays readable on a phone");
            Assert.AreEqual(1f, rules.KidViewScale, 0.001f, "kids drawn at full 1.85 m so enemies read at weapon range");

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
            Assert.AreEqual(12, kids.Length, "GameRules must reference all 12 kids (docs/SPEC.md §7)");
            CollectionAssert.AreEqual(AppSession.DefaultRoster, kids, "Canonical 12-kid order must match AppSession.DefaultRoster");
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
        public void SyncedGlbModels_CharactersAndWeapons_LoadAndEveryBarangayKitDraws()
        {
            foreach (string kid in AppSession.DefaultRoster)
            {
                GameObject model = KalyeahResources.Model("characters", kid);
                Assert.IsNotNull(model, $"Missing character .glb prefab: models/characters/{kid}");
                Assert.IsNotEmpty(model.GetComponentsInChildren<Renderer>(true), $"Character {kid} has no Renderer");
            }

            // The script-made barangay kits were retired (docs/GRAPHICS.md): every placement must still draw, as its licence-checked
            // model or as a grey-box block of real size.
            foreach (KitPlacement kit in BarangayPlan.Build(MapLayoutData.Load(KalyeahResources.Data())))
            {
                bool hasModel = KalyeahResources.Model("barangay", kit.Kit) != null;
                Vector3 block = GreyBoxKit.SizeOf(kit);
                Assert.IsTrue(hasModel || (block.x > 0f && block.y > 0f && block.z > 0f),
                    $"Barangay kit {kit.Kit} has neither a model nor a grey-box size");
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
        public void AllTwelveKids_PortraitsFacesCardsAndCharacterCards_ExistWithCellProportions()
        {
            DataSource data = KalyeahResources.Data();
            string[] kids = data.CharacterIds();
            Assert.AreEqual(12, kids.Length);

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
        public void ModelTexturesAndNormalMaps_AreVramCompressedAndPresentForAllTwelveKids()
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
