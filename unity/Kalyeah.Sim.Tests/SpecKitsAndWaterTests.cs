using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Comprehensive deterministic tests for docs/KITS.md + docs/SPEC.md §7:
    /// Water mana (street/fountain/round reset), 7-shot Tirador slingshot (reload/charge range/headshots/hit-confirm),
    /// every single one of the 12 kids x 36 approved skills, shared rules (walls, hidden traps, reveals, root vs hard stop,
    /// no solo 100-HP skill kill), SimData snake_case enum loading, and Snapshot wire round-trip.
    /// </summary>
    public class SpecKitsAndWaterTests
    {
        static GameRules SpecRulesFixture()
        {
            GameRules rules = Rules();
            rules.SpecRules = true;
            rules.SpecKitsEnabled = true;
            rules.CardCycle = false;
            rules.TripChance = 0.0;
            rules.NoCastInEnemyBase = false;
            rules.Characters = BuildAllTwelveKids();
            return rules;
        }

        static List<CharacterDef> BuildAllTwelveKids()
        {
            return new List<CharacterDef>
            {
                // ---- STRIKERS ----
                Kid("narda", "Narda", CharacterRole.Striker,
                    new SkillDef { Id = "luksong_tinik", DisplayName = "Luksong Tinik", Slot = SkillSlot.S1, Mechanic = SkillMechanic.LuksongTinik, AimCamera = AimCameraMode.Normal, WaterCost = 25, Cooldown = 8.0, MaxCharges = 2, MaxRange = 6.0, Duration = 0.25, SecondaryDuration = 2.0, BonusRatio = 0.50 },
                    new SkillDef { Id = "tira_holens", DisplayName = "Tira Holens", Slot = SkillSlot.S2, Mechanic = SkillMechanic.TiraHolens, AimCamera = AimCameraMode.Ads, WaterCost = 30, Cooldown = 9.0, MaxRange = 26.0, Speed = 36.0, Damage = 20, BonusRatio = 2.0 },
                    new SkillDef { Id = "tsinelas_bagsik", DisplayName = "Tsinelas Bagsik", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.TsinelasBagsik, AimCamera = AimCameraMode.Normal, WaterCost = 0, Cooldown = 80.0, MaxRange = 20.0, Speed = 26.0, Damage = 22, Count = 3, Interval = 0.65, BonusRatio = 0.50 }),
                Kid("jumong", "Jumong", CharacterRole.Striker,
                    new SkillDef { Id = "syato", DisplayName = "Syato", Slot = SkillSlot.S1, Mechanic = SkillMechanic.Syato, AimCamera = AimCameraMode.Normal, WaterCost = 30, Cooldown = 10.0, MaxRange = 16.0, Speed = 30.0, Damage = 18, SecondaryDamage = 22, EffectDuration = 0.5, RecastWindow = 2.0 },
                    new SkillDef { Id = "trumpo_ikot", DisplayName = "Trumpo Ikot", Slot = SkillSlot.S2, Mechanic = SkillMechanic.TrumpoIkot, AimCamera = AimCameraMode.Normal, WaterCost = 30, Cooldown = 11.0, Radius = 3.2, Duration = 2.0, Interval = 0.5, Damage = 6 },
                    new SkillDef { Id = "pukpok_palayok", DisplayName = "Pukpok Palayok", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.PukpokPalayok, AimCamera = AimCameraMode.ZoomOut, WaterCost = 0, Cooldown = 90.0, MaxRange = 10.0, Radius = 4.5, Duration = 0.4, Damage = 40, BlurDuration = 2.0 }),
                Kid("inggo", "Inggo", CharacterRole.Striker,
                    new SkillDef { Id = "sipa", DisplayName = "Sipa", Slot = SkillSlot.S1, Mechanic = SkillMechanic.Sipa, AimCamera = AimCameraMode.Normal, WaterCost = 25, Cooldown = 8.0, MaxRange = 18.0, SecondaryRadius = 7.0, Speed = 26.0, Count = 3, Damage = 20 },
                    new SkillDef { Id = "tiyakad", DisplayName = "Tiyakad", Slot = SkillSlot.S2, Mechanic = SkillMechanic.Tiyakad, AimCamera = AimCameraMode.Normal, WaterCost = 35, Cooldown = 14.0, Duration = 4.0, BonusRatio = 0.40, SpeedDelta = -0.20 },
                    new SkillDef { Id = "bato_execute", DisplayName = "Bato!", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.BatoExecute, AimCamera = AimCameraMode.Ads, WaterCost = 0, Cooldown = 85.0, MaxRange = 34.0, Delay = 1.0, Speed = 42.0, Damage = 35, SecondaryDamage = 45, Heal = 30 }),

                // ---- DEFENDERS ----
                Kid("jopay", "Jopay", CharacterRole.Defender,
                    new SkillDef { Id = "bahay_bahayan", DisplayName = "Bahay-Bahayan", Slot = SkillSlot.S1, Mechanic = SkillMechanic.BahayBahayan, AimCamera = AimCameraMode.ZoomOut, WaterCost = 35, Cooldown = 16.0, MaxRange = 14.0, Width = 6.0, WallHp = 80, Duration = 8.0 },
                    new SkillDef { Id = "pabitin_heal", DisplayName = "Pabitin", Slot = SkillSlot.S2, Mechanic = SkillMechanic.PabitinHeal, AimCamera = AimCameraMode.ZoomOut, WaterCost = 35, Cooldown = 14.0, MaxRange = 14.0, Radius = 4.5, Duration = 4.0, Interval = 0.5, Heal = 6 },
                    new SkillDef { Id = "palosebo", DisplayName = "Palosebo", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.Palosebo, AimCamera = AimCameraMode.Normal, WaterCost = 0, Cooldown = 90.0, MaxRange = 10.0, Radius = 7.0, Duration = 8.0, EffectMagnitude = 0.35 }),
                Kid("oriang", "Oriang", CharacterRole.Defender,
                    new SkillDef { Id = "chinese_garter", DisplayName = "Chinese Garter", Slot = SkillSlot.S1, Mechanic = SkillMechanic.ChineseGarter, AimCamera = AimCameraMode.Normal, WaterCost = 25, Cooldown = 10.0, MaxRange = 12.0, Duration = 6.0, SecondaryDuration = 2.0, BonusRatio = 0.30, SpeedDelta = 0.25 },
                    new SkillDef { Id = "sungka", DisplayName = "Sungka", Slot = SkillSlot.S2, Mechanic = SkillMechanic.Sungka, AimCamera = AimCameraMode.Normal, WaterCost = 30, Cooldown = 12.0, Radius = 7.0, Duration = 3.0, SecondaryDuration = 4.0, WallHp = 50 },
                    new SkillDef { Id = "langit", DisplayName = "Langit!", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.Langit, AimCamera = AimCameraMode.Normal, WaterCost = 0, Cooldown = 100.0, Radius = 8.0, Duration = 3.0 }),
                Kid("paeng", "Paeng", CharacterRole.Defender,
                    new SkillDef { Id = "sack_race", DisplayName = "Sack Race", Slot = SkillSlot.S1, Mechanic = SkillMechanic.SackRace, AimCamera = AimCameraMode.Normal, WaterCost = 30, Cooldown = 12.0, Duration = 3.0, Damage = 18, BonusRatio = 0.40, SpeedDelta = 0.15, Knockback = 3.5 },
                    new SkillDef { Id = "luksong_baka", DisplayName = "Luksong Baka", Slot = SkillSlot.S2, Mechanic = SkillMechanic.LuksongBaka, AimCamera = AimCameraMode.Normal, WaterCost = 20, Cooldown = 14.0, MaxRange = 10.0, Radius = 2.0, Duration = 4.0 },
                    new SkillDef { Id = "tinik_wall", DisplayName = "Tinik Wall", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.TinikWall, AimCamera = AimCameraMode.ZoomOut, WaterCost = 0, Cooldown = 85.0, MaxRange = 14.0, Width = 7.0, WallHp = 120, Duration = 4.0, Damage = 35, EffectDuration = 0.9 }),

                // ---- SEEKERS ----
                Kid("kikay", "Kikay", CharacterRole.Seeker,
                    new SkillDef { Id = "teks_tripwire", DisplayName = "Teks Tripwire", Slot = SkillSlot.S1, Mechanic = SkillMechanic.TeksTripwire, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Outline, KnownRevealAlert = true, WaterCost = 20, Cooldown = 10.0, MaxRange = 8.0, Radius = 1.6, RevealDuration = 6.0, EffectDuration = 3.0, EffectMagnitude = 0.20 },
                    new SkillDef { Id = "tago", DisplayName = "Tago!", Slot = SkillSlot.S2, Mechanic = SkillMechanic.Tago, AimCamera = AimCameraMode.Normal, WaterCost = 35, Cooldown = 16.0, Duration = 4.0, SecondaryDuration = 3.0, BonusRatio = 0.40 },
                    new SkillDef { Id = "saranggola", DisplayName = "Saranggola", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.Saranggola, AimCamera = AimCameraMode.MapCast, RevealKind = RevealKind.Outline, KnownRevealAlert = true, WaterCost = 0, Cooldown = 80.0, MaxRange = 85.0, Radius = 15.0, Duration = 6.0 }),
                Kid("karding", "Karding", CharacterRole.Seeker,
                    new SkillDef { Id = "yoyo", DisplayName = "Yoyo", Slot = SkillSlot.S1, Mechanic = SkillMechanic.Yoyo, AimCamera = AimCameraMode.Ads, RevealKind = RevealKind.Outline, KnownRevealAlert = true, WaterCost = 25, Cooldown = 10.0, MaxRange = 20.0, Speed = 32.0, Damage = 20, RevealDuration = 5.0, Knockback = 2.0 },
                    new SkillDef { Id = "piko", DisplayName = "Piko", Slot = SkillSlot.S2, Mechanic = SkillMechanic.Piko, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Outline, KnownRevealAlert = false, WaterCost = 30, Cooldown = 14.0, MaxRange = 16.0, Radius = 5.0, Duration = 6.0 },
                    new SkillDef { Id = "pogs_slam", DisplayName = "Pogs Slam", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.PogsSlam, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Snapshot, KnownRevealAlert = true, WaterCost = 0, Cooldown = 85.0, MaxRange = 10.0, Radius = 6.0, SecondaryRadius = 14.0, Damage = 38, RevealDuration = 4.0 }),
                Kid("manoy", "Manoy", CharacterRole.Seeker,
                    new SkillDef { Id = "sili_tubig", DisplayName = "Sili-Tubig", Slot = SkillSlot.S1, Mechanic = SkillMechanic.SiliTubig, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Outline, KnownRevealAlert = true, WaterCost = 30, Cooldown = 11.0, MaxRange = 10.0, AngleDegrees = 55.0, Damage = 18, RevealDuration = 4.0, BlurDuration = 1.5 },
                    new SkillDef { Id = "trumpo_bantay", DisplayName = "Trumpo Bantay", Slot = SkillSlot.S2, Mechanic = SkillMechanic.TrumpoBantay, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Snapshot, KnownRevealAlert = true, WaterCost = 20, Cooldown = 15.0, MaxRange = 12.0, Radius = 8.0, Duration = 20.0, Interval = 1.0, RevealDuration = 1.2 },
                    new SkillDef { Id = "taya", DisplayName = "Taya!", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.Taya, AimCamera = AimCameraMode.Normal, RevealKind = RevealKind.Snapshot, KnownRevealAlert = true, WaterCost = 0, Cooldown = 100.0, Delay = 3.0, RevealDuration = 4.0 }),

                // ---- CONTROLLERS ----
                Kid("tere", "Tere", CharacterRole.Controller,
                    new SkillDef { Id = "jackstone", DisplayName = "Jackstone", Slot = SkillSlot.S1, Mechanic = SkillMechanic.Jackstone, AimCamera = AimCameraMode.ZoomOut, WaterCost = 25, Cooldown = 10.0, MaxRange = 16.0, Radius = 4.0, Duration = 5.0, Interval = 1.0, Damage = 5, EffectMagnitude = 0.30 },
                    new SkillDef { Id = "luksong_lubid", DisplayName = "Luksong Lubid", Slot = SkillSlot.S2, Mechanic = SkillMechanic.LuksongLubid, AimCamera = AimCameraMode.ZoomOut, WaterCost = 30, Cooldown = 13.0, MaxRange = 14.0, Width = 6.0, Duration = 10.0, Damage = 16, EffectDuration = 1.0 },
                    new SkillDef { Id = "garter_net", DisplayName = "Garter Net", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.GarterNet, AimCamera = AimCameraMode.ZoomOut, WaterCost = 0, Cooldown = 90.0, MaxRange = 16.0, Radius = 6.0, Damage = 35, EffectDuration = 1.25, SecondaryDuration = 2.0, EffectMagnitude = 0.35 }),
                Kid("pamela", "Pamela", CharacterRole.Controller,
                    new SkillDef { Id = "holens_spill", DisplayName = "Holens Spill", Slot = SkillSlot.S1, Mechanic = SkillMechanic.HolensSpill, AimCamera = AimCameraMode.Normal, WaterCost = 25, Cooldown = 10.0, MaxRange = 10.0, AngleDegrees = 50.0, Duration = 3.0, Damage = 15, EffectMagnitude = 0.35 },
                    new SkillDef { Id = "lata_morph", DisplayName = "Lata!", Slot = SkillSlot.S2, Mechanic = SkillMechanic.LataMorph, AimCamera = AimCameraMode.Normal, WaterCost = 35, Cooldown = 15.0, MaxRange = 18.0, Speed = 28.0, Damage = 16, EffectDuration = 1.5 },
                    new SkillDef { Id = "patintero_lines", DisplayName = "Patintero!", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.PatinteroLines, AimCamera = AimCameraMode.ZoomOut, WaterCost = 0, Cooldown = 90.0, MaxRange = 14.0, Width = 7.0, Count = 3, SecondaryRadius = 2.5, Duration = 5.0, Damage = 15, EffectDuration = 0.8 }),
                Kid("domeng", "Domeng", CharacterRole.Controller,
                    new SkillDef { Id = "pabitin_snare", DisplayName = "Pabitin Snare", Slot = SkillSlot.S1, Mechanic = SkillMechanic.PabitinSnare, AimCamera = AimCameraMode.ZoomOut, WaterCost = 30, Cooldown = 11.0, MaxRange = 16.0, Radius = 4.0, Delay = 0.5, Damage = 18, EffectDuration = 1.2 },
                    new SkillDef { Id = "sako_trap", DisplayName = "Sako Trap", Slot = SkillSlot.S2, Mechanic = SkillMechanic.SakoTrap, AimCamera = AimCameraMode.Normal, WaterCost = 20, Cooldown = 12.0, MaxRange = 8.0, Radius = 1.6, EffectDuration = 3.0, EffectMagnitude = 0.50 },
                    new SkillDef { Id = "lupa_quake", DisplayName = "Lupa!", Slot = SkillSlot.Ult, Mechanic = SkillMechanic.LupaQuake, AimCamera = AimCameraMode.Normal, WaterCost = 0, Cooldown = 95.0, Radius = 8.0, Delay = 0.6, Damage = 40, EffectDuration = 1.0 }),
            };
        }

        static CharacterDef Kid(string id, string name, CharacterRole role, SkillDef s1, SkillDef s2, SkillDef ult)
        {
            return new CharacterDef
            {
                Id = id,
                DisplayName = name,
                Description = name,
                Role = role,
                Skills = new List<SkillDef> { s1, s2, ult },
            };
        }

        static MatchSim OpenSpecSim(long seed = 42) => new MatchSim(SpecRulesFixture(), OpenLayout(), seed);

        static PlayerState AddKid(MatchSim sim, int id, int team, string kidId, Vec2 pos)
        {
            PlayerState p = sim.AddPlayer(id, team);
            p.CharacterId = kidId;
            p.Position = pos;
            p.Facing = new Vec2(0.0, team == 0 ? -1.0 : 1.0);
            Assert.IsTrue(sim.EquipSpecKit(id), $"EquipSpecKit({kidId})");
            return p;
        }

        static void CastSlotAt(MatchSim sim, int id, int slot, Vec2 worldPoint, int holdTicks = 1)
        {
            int bit = PlayerInput.CardBits[slot];
            for (int i = 0; i < holdTicks; i++)
            {
                sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, bit, sim.Tick).WithAimPoint(worldPoint));
                sim.Step(DT);
            }
            sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithAimPoint(worldPoint));
            sim.Step(DT);
        }

        static void FireTiradorAt(MatchSim sim, int id, Vec2 worldPoint, int holdTicks = 1, bool ads = false)
        {
            int btns = PlayerInput.BtnPrimary | (ads ? PlayerInput.BtnAds : 0);
            for (int i = 0; i < holdTicks; i++)
            {
                sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, btns, sim.Tick).WithAimPoint(worldPoint));
                sim.Step(DT);
            }
            sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, ads ? PlayerInput.BtnAds : 0, sim.Tick).WithAimPoint(worldPoint));
            sim.Step(DT);
        }

        // ---- 1. WATER MANA & ROUND-RESET / ULT COOLDOWN CARRY-OVER ----

        [Test]
        public void WaterManaRegensInStreetFastInFountainBlocksWhenEmptyAndResetsOnRoundWhileUltCarriesOver()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState narda = AddKid(sim, 1, 0, "narda", Vec2.Zero);
            AddKid(sim, 2, 1, "domeng", new Vec2(0.0, -10.0));
            Assert.AreEqual(100.0, narda.Water, 0.01);

            // Spend 30 water on S2 Tira Holens
            CastSlotAt(sim, 1, 1, new Vec2(0.0, -10.0));
            Assert.AreEqual(70.0, narda.Water, 0.5);

            // 2/s passive street regen
            double beforeStreet = narda.Water;
            Wait(sim, 2.0);
            Assert.AreEqual(beforeStreet + 4.0, narda.Water, 0.3, "2/s street regen");
            Assert.IsFalse(narda.InHomeFountain);

            // 25/s home base fountain regen
            narda.Water = 10.0;
            narda.Position = sim.Layout.BaseCenter(sim.SideForTeam(0));
            Wait(sim, 1.0);
            Assert.IsTrue(narda.InHomeFountain);
            Assert.Greater(narda.Water, 32.0, "25/s fountain regen");

            // Insufficient water fires event and blocks cast
            narda.Position = Vec2.Zero;
            narda.Water = 5.0;
            narda.WeaponCooldowns[1] = 0.0;
            int insufficientCount = 0;
            sim.WaterInsufficient += pid => { if (pid == 1) insufficientCount++; };
            CastSlotAt(sim, 1, 1, new Vec2(0.0, -10.0));
            Assert.AreEqual(1, insufficientCount);
            Assert.AreEqual(0.0, narda.WeaponCooldowns[1], "failed cast did not trigger cooldown");

            // ULT costs 0 water even when at 5 water, and ULT CD carries across round reset!
            CastSlotAt(sim, 1, 2, new Vec2(0.0, -10.0));
            Assert.Greater(narda.WeaponCooldowns[2], 75.0, "ULT entered cooldown with 0 water cost");
            double ultCdBeforeReset = narda.WeaponCooldowns[2];
            narda.WeaponCooldowns[1] = 7.0;
            narda.Water = 15.0;
            sim.FinishReloads(narda);
            Assert.AreEqual(100.0, narda.Water, 0.01, "water refills to 100 on round start");
            Assert.AreEqual(0.0, narda.WeaponCooldowns[0], 0.01, "S1 resets on round start");
            Assert.AreEqual(0.0, narda.WeaponCooldowns[1], 0.01, "S2 resets on round start");
            Assert.AreEqual(ultCdBeforeReset, narda.WeaponCooldowns[2], 0.01, "ULT CD carries across rounds");

            // Side swap resets ULT cooldowns
            sim.ResetUltimateCooldowns();
            Assert.AreEqual(0.0, narda.WeaponCooldowns[2], 0.01);
        }

        // ---- 2. TIRADOR 7-SHOT MAGAZINE, RELOAD, HOLD-TO-CHARGE RANGE & HEADSHOTS ----

        [Test]
        public void TiradorSevenShotsReloadChargeRangeAndHeadshotsWorkAuthoritatively()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState shooter = AddKid(sim, 1, 0, "narda", Vec2.Zero);
            PlayerState farEnemy = AddKid(sim, 2, 1, "domeng", new Vec2(0.0, -25.0));

            // Tap shot (1 tick hold = 18m range): drops short of 25m target
            FireTiradorAt(sim, 1, farEnemy.Position, holdTicks: 1);
            Assert.AreEqual(6, shooter.TiradorAmmo);
            Wait(sim, 1.0);
            Assert.AreEqual(100, farEnemy.Hp, "18m tap shot drops before 25m target");

            // Full 1s charge shot (30m range): reaches 25m target! Same 20 body damage (never extra damage from charge)
            FireTiradorAt(sim, 1, new Vec2(0.35, -25.0), holdTicks: 31, ads: false);
            Wait(sim, 1.0);
            Assert.AreEqual(80, farEnemy.Hp, "charged shot reaches 25m for 20 body dmg (range only)");

            // Dead-center ADS shot registers a HEADSHOT (30 dmg) + HitConfirmed event
            farEnemy.Position = new Vec2(0.0, -10.0);
            farEnemy.Hp = 100;
            var hits = new List<HitConfirmEvent>();
            sim.HitConfirmed += ev => hits.Add(ev);
            FireTiradorAt(sim, 1, farEnemy.Position, holdTicks: 1, ads: true);
            Wait(sim, 0.5);
            Assert.AreEqual(70, farEnemy.Hp, "ADS center shot deals 30 headshot damage");
            Assert.IsTrue(hits.Exists(h => h.TargetId == 2 && h.IsHeadshot && h.Damage == 30));
            Assert.IsTrue(shooter.LastHitEvent.IsHeadshot);

            // Sliding enemy ducks headshot (takes 20 body damage instead of 30)
            farEnemy.Hp = 100;
            farEnemy.SlideLeft = 0.5;
            FireTiradorAt(sim, 1, farEnemy.Position, holdTicks: 1, ads: true);
            Wait(sim, 0.5);
            Assert.AreEqual(80, farEnemy.Hp, "slide ducks headshot to 20 body damage");

            // Manual reload via BtnReload
            int reloadCount = 0;
            sim.TiradorReloadStarted += pid => { if (pid == 1) reloadCount++; };
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnReload);
            Assert.AreEqual(1, reloadCount);
            Assert.Greater(shooter.TiradorReloadLeft, 1.3);
            Wait(sim, 1.6);
            Assert.AreEqual(7, shooter.TiradorAmmo, "reloaded back to 7 stones");

            // Empty magazine triggers auto-reload
            shooter.TiradorAmmo = 1;
            FireTiradorAt(sim, 1, farEnemy.Position, holdTicks: 1);
            Assert.AreEqual(0, shooter.TiradorAmmo);
            Assert.AreEqual(2, reloadCount, "auto-reload started at 0 stones");
            Assert.Greater(shooter.TiradorReloadLeft, 1.3);
            Wait(sim, 1.6);
            Assert.AreEqual(7, shooter.TiradorAmmo);
        }

        // ---- 3. STRIKERS (NARDA, JUMONG, INGGO — ALL 9 SKILLS) ----

        [Test]
        public void NardaKit_LuksongTinikTiraHolensAndTsinelasBagsik()
        {
            MapLayout cover = CoverStreet();
            MatchSim sim = new MatchSim(SpecRulesFixture(), cover, 10);
            PlayerState narda = AddKid(sim, 1, 0, "narda", new Vec2(-12.0, 12.0));
            PlayerState foe = AddKid(sim, 2, 1, "domeng", new Vec2(-12.0, 4.0));

            // S1 Luksong Tinik: 2 charges, vaults over waist-high cover (y=10.5..11.0), +50% next Tirador (30 body!)
            Assert.AreEqual(2, narda.S1Charges);
            CastSlotAt(sim, 1, 0, new Vec2(-12.0, 6.0));
            Assert.AreEqual(1, narda.S1Charges);
            Assert.Greater(narda.EmpoweredTiradorBonus, 0.45);
            Wait(sim, 0.4);
            Assert.Less(narda.Position.y, 10.0, "Luksong Tinik vaulted cleanly over cover");

            // Empowered body shot deals 30 (20 * 1.5)
            FireTiradorAt(sim, 1, new Vec2(-11.65, 4.0), holdTicks: 1);
            Wait(sim, 0.4);
            Assert.AreEqual(70, foe.Hp, "Luksong Tinik +50% Tirador shot");

            // 2nd charge available right away
            CastSlotAt(sim, 1, 0, new Vec2(-12.0, 1.0));
            Assert.AreEqual(0, narda.S1Charges);

            // S2 Tira Holens: wall ricochet deals 2x damage (40)
            narda.Water = 100.0;
            narda.Position = new Vec2(-12.0, 8.0);
            foe.Position = new Vec2(-12.0, 5.0);
            foe.Hp = 100;
            // Aim straight up at cover at y=10.5 so marble bounces straight back down into foe at y=5.0!
            CastSlotAt(sim, 1, 1, new Vec2(-12.0, 10.75));
            Wait(sim, 0.8);
            Assert.AreEqual(60, foe.Hp, "Tira Holens ricochet hit deals double damage (40)");

            // ULT Tsinelas Bagsik: 3 boomerangs + 50% ULT CD refund on tag!
            narda.Position = new Vec2(0.0, 10.0);
            foe.Position = new Vec2(0.0, 3.0);
            foe.Hp = 20; // low enough to be tagged by slipper!
            CastSlotAt(sim, 1, 2, foe.Position);
            Assert.Greater(narda.WeaponCooldowns[2], 78.0);
            Wait(sim, 0.6);
            Assert.IsTrue(foe.Tagged, "Tsinelas Bagsik tagged low enemy");
            Assert.Less(narda.WeaponCooldowns[2], 42.0, "50% ULT cooldown refunded on tag");
        }

        [Test]
        public void JumongKit_SyatoTrumpoIkotAndPukpokPalayok()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState jumong = AddKid(sim, 1, 0, "jumong", new Vec2(0.0, 8.0));
            PlayerState foe = AddKid(sim, 2, 1, "domeng", new Vec2(0.0, 0.0));

            // S1 Syato: pop-up 0.5s Airborne + free recast dash-whack within 2s
            CastSlotAt(sim, 1, 0, foe.Position);
            Wait(sim, 0.4);
            Assert.AreEqual(82, foe.Hp, "Syato stick hit for 18");
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Airborne), "Syato popped enemy up");
            Assert.AreEqual(2, jumong.SyatoTargetId);
            Assert.Greater(jumong.SyatoRecastLeft, 1.0);
            double waterBeforeRecast = jumong.Water;
            CastSlotAt(sim, 1, 0, foe.Position); // recast!
            Assert.AreEqual(60, foe.Hp, "Syato recast whacked for 22");
            Assert.Less(jumong.Position.DistanceTo(foe.Position), 2.0, "Syato recast dashed to marked enemy");
            Assert.GreaterOrEqual(jumong.Water, waterBeforeRecast - 0.1, "Syato recast costs 0 extra water");

            // S2 Trumpo Ikot: 2s spin AoE dmg + slow immune
            jumong.Water = 100.0;
            foe.Hp = 100;
            foe.Position = jumong.Position + new Vec2(2.0, 0.0);
            sim.ApplyEffect(1, StatusEffects.Type.Slow, 3.0, 0.5);
            CastSlotAt(sim, 1, 1, foe.Position);
            Assert.IsFalse(jumong.Effects.Has(StatusEffects.Type.Slow), "Trumpo Ikot cleanses slow");
            sim.ApplyEffect(1, StatusEffects.Type.Slow, 3.0, 0.5);
            Assert.IsFalse(jumong.Effects.Has(StatusEffects.Type.Slow), "Trumpo Ikot is slow-immune");
            Wait(sim, 2.2);
            Assert.Less(foe.Hp, 85, "Trumpo Ikot dealt spin AoE damage");

            // ULT Pukpok Palayok: leap 10m + 40 AoE dmg + 2s Blur
            jumong.Position = new Vec2(0.0, 10.0);
            foe.Position = new Vec2(0.0, 0.0);
            foe.Hp = 100;
            CastSlotAt(sim, 1, 2, foe.Position);
            Wait(sim, 0.5);
            Assert.Less(jumong.Position.DistanceTo(foe.Position), 1.0, "Jumong leaped to target");
            Assert.AreEqual(60, foe.Hp, "Pukpok Palayok dealt 40 AoE damage");
            Assert.Greater(foe.BlurLeft, 1.5, "Pukpok Palayok blurred hit enemy for 2s");
        }

        [Test]
        public void InggoKit_SipaTiyakadAndBatoExecute()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState inggo = AddKid(sim, 1, 0, "inggo", new Vec2(0.0, 10.0));
            PlayerState f1 = AddKid(sim, 2, 1, "narda", new Vec2(0.0, 2.0));
            PlayerState f2 = AddKid(sim, 3, 1, "jumong", new Vec2(4.0, 2.0));
            PlayerState f3 = AddKid(sim, 4, 1, "jopay", new Vec2(8.0, 2.0));

            // S1 Sipa: bounces across all 3 enemies
            CastSlotAt(sim, 1, 0, f1.Position);
            Wait(sim, 1.2);
            Assert.AreEqual(80, f1.Hp, "Sipa hit #1");
            Assert.AreEqual(80, f2.Hp, "Sipa bounced to #2");
            Assert.AreEqual(80, f3.Hp, "Sipa bounced to #3");

            // S2 Tiyakad: 4s stilts (+40% Tirador range, -20% speed, elevated)
            inggo.Water = 100.0;
            CastSlotAt(sim, 1, 1, Vec2.Zero);
            Assert.Greater(inggo.StiltsLeft, 3.5);
            Assert.IsTrue(inggo.IsOffGround(), "Tiyakad stilts elevate Inggo");
            // Tap Tirador now reaches 18 * 1.4 = 25.2m (beats 22m target!)
            f1.Position = new Vec2(0.0, -12.0); // 22m away from (0,10)
            f1.Hp = 100;
            FireTiradorAt(sim, 1, new Vec2(0.35, -12.0), holdTicks: 1);
            Wait(sim, 1.0);
            Assert.AreEqual(80, f1.Hp, "Tiyakad +40% Tirador range reached 22m on a tap");

            // ULT Bato!: 1s windup, missing-HP execute + heal 30 on tag
            inggo.Position = new Vec2(0.0, 10.0);
            inggo.Hp = 50;
            f1.Position = new Vec2(0.0, -5.0);
            f1.Hp = 100;
            CastSlotAt(sim, 1, 2, f1.Position);
            Wait(sim, 0.5);
            Assert.AreEqual(100, f1.Hp, "still in 1s windup");
            Wait(sim, 1.2);
            Assert.AreEqual(65, f1.Hp, "full-HP target takes 35 base damage");

            // Now test execute on low-HP target -> tags & heals Inggo 30!
            inggo.WeaponCooldowns[2] = 0.0;
            f1.Hp = 40; // 60% missing HP -> 35 + 27 = 62 -> tags!
            CastSlotAt(sim, 1, 2, f1.Position);
            Wait(sim, 1.8);
            Assert.IsTrue(f1.Tagged, "Bato! executed low-HP enemy");
            Assert.AreEqual(80, inggo.Hp, "Bato! healed Inggo 30 HP on tag");
        }

        // ---- 4. DEFENDERS (JOPAY, ORIANG, PAENG — ALL 9 SKILLS) ----

        [Test]
        public void JopayKit_BahayBahayanWallPabitinHealAndPaloseboPole()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState jopay = AddKid(sim, 1, 0, "jopay", new Vec2(0.0, 10.0));
            PlayerState ally = AddKid(sim, 2, 0, "narda", new Vec2(2.0, 10.0));
            PlayerState foe = AddKid(sim, 3, 1, "domeng", new Vec2(0.0, 0.0));

            // S1 Bahay-Bahayan: 6m wide, 80 HP wall blocks BOTH teams' movement and shots
            CastSlotAt(sim, 1, 0, new Vec2(0.0, 5.0));
            Assert.AreEqual(1, sim.Weapons.Walls.Count);
            Assert.AreEqual(80, sim.Weapons.Walls[0].Hp);
            // Own team cannot walk through wall either
            Run(sim, 1, new Vec2(0.0, -1.0), 45);
            Assert.Greater(jopay.Position.y, 5.1, "Bahay-Bahayan blocks own team movement");
            // Enemy stones hit wall and destroy it after 4 stones (80 HP / 20 dmg)
            int brokenCount = 0;
            sim.WallBroken += (_, __) => brokenCount++;
            for (int i = 0; i < 4; i++)
            {
                FireTiradorAt(sim, 3, new Vec2(0.0, 5.0), holdTicks: 1);
                Wait(sim, 0.5);
            }
            Assert.AreEqual(0, sim.Weapons.Walls.Count, "4 stones (80 dmg) broke Bahay-Bahayan wall");
            Assert.AreEqual(1, brokenCount);

            // S2 Pabitin: ally-only heal zone over 4s
            jopay.Water = 100.0;
            ally.Hp = 50;
            foe.Hp = 50;
            ally.Position = new Vec2(0.0, 6.0);
            foe.Position = new Vec2(1.0, 6.0);
            CastSlotAt(sim, 1, 1, new Vec2(0.0, 6.0));
            Wait(sim, 2.2);
            Assert.Greater(ally.Hp, 65, "Pabitin healed ally");
            Assert.AreEqual(50, foe.Hp, "Pabitin never heals enemies");

            // ULT Palosebo: greased pole in own base halts enemy capture + applies Slip
            Vec2 ownBase = sim.Layout.BaseCenter(sim.SideForTeam(0));
            jopay.Position = ownBase + new Vec2(0.0, -15.0);
            ally.Position = new Vec2(30.0, 30.0);
            foe.Position = ownBase;
            Wait(sim, 0.5);
            double progressBefore = sim.CaptureElapsed;
            Assert.Greater(progressBefore, 0.2, "enemy was capturing");
            CastSlotAt(sim, 1, 2, ownBase);
            Wait(sim, 1.0);
            Assert.AreEqual(progressBefore, sim.CaptureElapsed, 0.1, "Palosebo halted enemy capture");
            Assert.IsTrue(foe.Effects.IsSlipping(), "Palosebo applied Slip near pole");
        }

        [Test]
        public void OriangKit_ChineseGarterSungkaAndLangit()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState oriang = AddKid(sim, 1, 0, "oriang", new Vec2(0.0, 10.0));
            PlayerState ally = AddKid(sim, 2, 0, "narda", new Vec2(4.0, 10.0));
            AddKid(sim, 3, 1, "domeng", new Vec2(0.0, 0.0));

            // S1 Chinese Garter: redirects 30% of ally damage to Oriang; stretching > 12m snaps & speeds both
            CastSlotAt(sim, 1, 0, ally.Position);
            Assert.AreEqual(1, sim.Weapons.Tethers.Count);
            sim.Damage(2, 20, 3);
            Assert.AreEqual(86, ally.Hp, "ally took 70% (14)");
            Assert.AreEqual(94, oriang.Hp, "Oriang redirected 30% (6)");
            ally.Position = new Vec2(14.0, 10.0); // > 12m stretch
            sim.Step(DT);
            Assert.AreEqual(0, sim.Weapons.Tethers.Count, "tether snapped past 12m");
            Assert.Greater(oriang.SpeedBoostLeft, 1.5, "Oriang got snap speed boost");
            Assert.Greater(ally.SpeedBoostLeft, 1.5, "ally got snap speed boost");

            // S2 Sungka: 3s damage absorb -> shared ally ShieldHp
            oriang.Water = 100.0;
            oriang.Hp = 100;
            ally.Position = new Vec2(3.0, 10.0);
            ally.Hp = 100;
            CastSlotAt(sim, 1, 1, Vec2.Zero);
            sim.Damage(1, 30, 3);
            Assert.AreEqual(30, oriang.SungkaStoredDamage);
            Wait(sim, 3.1);
            Assert.AreEqual(30.0, ally.ShieldHp, 0.1, "Sungka released 30 shared shield to nearby ally");
            sim.Damage(2, 20, 3);
            Assert.AreEqual(100, ally.Hp, "Sungka shield absorbed 20 dmg");
            Assert.AreEqual(10.0, ally.ShieldHp, 0.1);

            // ULT Langit!: 3s 8m allies cannot drop below 1 HP (cannot be tagged)
            ally.ShieldHp = 0.0;
            ally.Hp = 10;
            CastSlotAt(sim, 1, 2, Vec2.Zero);
            sim.Damage(2, 50, 3);
            Assert.AreEqual(1, ally.Hp, "Langit! clamped ally at 1 HP");
            Assert.IsFalse(ally.Tagged, "Langit! prevented tagging");
            // Stuns still apply during Langit!
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.8, 0.0);
            Assert.IsTrue(ally.Effects.Has(StatusEffects.Type.Stun), "stuns still apply under Langit!");
        }

        [Test]
        public void PaengKit_SackRaceLuksongBakaAndTinikWall()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState paeng = AddKid(sim, 1, 0, "paeng", new Vec2(0.0, 10.0));
            PlayerState ally = AddKid(sim, 2, 0, "narda", new Vec2(4.0, 10.0));
            PlayerState foe = AddKid(sim, 3, 1, "domeng", new Vec2(0.0, 9.0));

            // S1 Sack Race: 40% DR + bump knockback
            CastSlotAt(sim, 1, 0, foe.Position);
            sim.Damage(1, 20, 3);
            Assert.AreEqual(88, paeng.Hp, "Sack Race reduced 20 dmg by 40% to 12");
            Wait(sim, 0.15);
            Assert.Less(foe.Position.y, 7.0, "Sack Race bumped enemy back");
            Assert.AreEqual(82, foe.Hp, "Sack Race dealt 18 bump dmg");

            // S2 Luksong Baka: ally-only 10m vault leap (enemies cannot vault!)
            paeng.Water = 100.0;
            paeng.SackRaceLeft = 0.0;
            paeng.Position = new Vec2(0.0, 10.0);
            foe.Position = new Vec2(0.5, 10.0);
            CastSlotAt(sim, 1, 1, Vec2.Zero);
            Wait(sim, 0.2);
            Assert.AreEqual(0.0, foe.DashLeft, "enemies NEVER vault off Paeng's Luksong Baka");
            ally.Position = new Vec2(1.0, 10.0);
            ally.Facing = new Vec2(0.0, -1.0);
            sim.Step(DT);
            Assert.Greater(ally.DashLeft, 0.3, "ally vaulted off Paeng's Luksong Baka");
            Wait(sim, 0.7);
            Assert.Less(ally.Position.y, 1.5, "ally vaulted ~10m forward");

            // ULT Tinik Wall: blocking wall + knock-up on rise
            foe.Position = new Vec2(0.0, 4.0);
            foe.Hp = 100;
            CastSlotAt(sim, 1, 2, foe.Position);
            Assert.AreEqual(1, sim.Weapons.Walls.Count);
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Airborne), "Tinik Wall knocked up enemy on rise");
            Assert.AreEqual(65, foe.Hp, "Tinik Wall dealt 35 rise damage");
        }

        // ---- 5. SEEKERS (KIKAY, KARDING, MANOY — ALL 9 SKILLS) ----

        [Test]
        public void KikayKit_TeksTripwireTagoAndSaranggola()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState kikay = AddKid(sim, 1, 0, "kikay", new Vec2(0.0, 10.0));
            PlayerState foe = AddKid(sim, 2, 1, "domeng", new Vec2(0.0, -10.0));

            // S1 Teks Tripwire: max 2 traps (3rd removes oldest), enemy sees within 3m, 1 stone breaks it
            CastSlotAt(sim, 1, 0, new Vec2(-2.0, 6.0));
            kikay.WeaponCooldowns[0] = 0.0; kikay.Water = 100.0;
            CastSlotAt(sim, 1, 0, new Vec2(2.0, 6.0));
            Assert.AreEqual(2, sim.Weapons.Traps.Count);
            kikay.WeaponCooldowns[0] = 0.0; kikay.Water = 100.0;
            CastSlotAt(sim, 1, 0, new Vec2(0.0, 5.0));
            Assert.AreEqual(2, sim.Weapons.Traps.Count, "3rd trap removed oldest");
            Assert.IsFalse(sim.IsTrapVisibleToTeam(sim.Weapons.Traps[1], 1), "hidden from enemy > 3m away");
            // Move enemy within 3m -> visible -> 1 Tirador stone breaks it!
            foe.Position = new Vec2(0.0, 2.5);
            Assert.IsTrue(sim.IsTrapVisibleToTeam(sim.Weapons.Traps[1], 1), "seen by enemy <= 3m");
            FireTiradorAt(sim, 2, sim.Weapons.Traps[1].Position, holdTicks: 1);
            Wait(sim, 0.3);
            Assert.AreEqual(1, sim.Weapons.Traps.Count, "1 stone broke seen trap");

            // Stepping on remaining armed trap triggers Outline reveal 6s (known) + 20% slow
            Wait(sim, 0.5);
            foe.Position = sim.Weapons.Traps[0].Position;
            sim.Step(DT);
            Assert.AreEqual(0, sim.Weapons.Traps.Count);
            Assert.Greater(foe.RevealedOutlineLeft, 5.5);
            Assert.IsTrue(foe.KnownRevealAlert, "Teks Tripwire alerts victim");
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Slow));

            // S2 Tago!: 4s invisible, breaks on attack, first Tirador deals +40% (28 dmg)
            kikay.Water = 100.0;
            foe.Position = new Vec2(0.0, 2.0);
            foe.Hp = 100;
            CastSlotAt(sim, 1, 1, Vec2.Zero);
            Assert.Greater(kikay.InvisibleLeft, 3.5);
            Assert.IsFalse(sim.IsEnemyVisibleToTeam(kikay, 1));
            FireTiradorAt(sim, 1, new Vec2(0.35, 2.0), holdTicks: 1);
            Assert.AreEqual(0.0, kikay.InvisibleLeft, "attacking broke Tago! invisibility");
            Wait(sim, 0.5);
            Assert.AreEqual(72, foe.Hp, "first shot after Tago! dealt +40% (28)");

            // ULT Saranggola: map-cast 15m kite reveals enemies AND enemy traps
            foe.RevealedOutlineLeft = 0.0;
            CastSlotAt(sim, 2, 1, new Vec2(0.0, -4.0)); // Domeng drops Sako trap at (0, -4)
            CastSlotAt(sim, 1, 2, new Vec2(0.0, -4.0)); // Kikay flies Saranggola over (0, -4)
            Assert.Greater(foe.RevealedOutlineLeft, 0.0, "Saranggola revealed enemy");
            Assert.IsTrue(foe.KnownRevealAlert, "Saranggola alerts enemy");
            Assert.IsTrue(sim.Weapons.Traps[0].RevealedToEnemy, "Saranggola revealed enemy trap");
        }

        [Test]
        public void KardingKit_YoyoPikoSilentRevealAndPogsSlam()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState karding = AddKid(sim, 1, 0, "karding", new Vec2(0.0, 10.0));
            PlayerState jopayFoe = AddKid(sim, 2, 1, "jopay", new Vec2(0.0, 0.0));
            PlayerState domengFoe = AddKid(sim, 3, 1, "domeng", new Vec2(0.0, -10.0));

            // S1 Yoyo: reveals 5s (known) + tugs enemy 2m toward Karding
            CastSlotAt(sim, 1, 0, jopayFoe.Position);
            Wait(sim, 0.5);
            Assert.Greater(jopayFoe.RevealedOutlineLeft, 4.0);
            Assert.IsTrue(jopayFoe.KnownRevealAlert);
            Assert.Greater(jopayFoe.Position.y, 1.5, "Yoyo tugged enemy ~2m toward Karding");

            // S2 Piko: disarms enemy traps inside + SILENT outline reveal (KnownRevealAlert == false!)
            karding.Water = 100.0;
            CastSlotAt(sim, 3, 1, new Vec2(0.0, -8.0)); // Domeng drops Sako trap
            Assert.AreEqual(1, sim.Weapons.Traps.Count);
            domengFoe.Position = new Vec2(0.0, -6.0);
            domengFoe.RevealedOutlineLeft = 0.0;
            domengFoe.RevealAlertLeft = 0.0;
            domengFoe.KnownRevealAlert = false;
            karding.Position = new Vec2(0.0, 0.0);
            CastSlotAt(sim, 1, 1, new Vec2(0.0, -7.0));
            Assert.AreEqual(0, sim.Weapons.Traps.Count, "Piko disarmed enemy trap");
            Assert.Greater(domengFoe.RevealedOutlineLeft, 0.0, "Piko revealed enemy inside grid");
            Assert.IsFalse(domengFoe.KnownRevealAlert, "Piko reveal is SILENT (docs/KITS.md)");

            // Upgrading from silent Piko to an alert Outline reveal fires PlayerRevealed(knownAlert: true)
            bool upgradedAlertFired = false;
            sim.PlayerRevealed += (tid, _, kind, known) =>
            {
                if (tid == 3 && kind == RevealKind.Outline && known) upgradedAlertFired = true;
            };
            sim.Weapons.ApplyOutlineReveal(sim, 0, domengFoe, 4.0, true);
            Assert.IsTrue(upgradedAlertFired, "alert reveal over silent Piko still fires PlayerRevealed(knownAlert: true)");
            Assert.IsTrue(domengFoe.KnownRevealAlert);

            // ULT Pogs Slam: destroys enemy traps & walls in 6m + Snapshot reveal in 14m
            jopayFoe.Position = new Vec2(0.0, -4.0);
            CastSlotAt(sim, 2, 0, new Vec2(0.0, -3.0)); // Jopay places Bahay-Bahayan wall at (0, -3)
            Assert.AreEqual(1, sim.Weapons.Walls.Count);
            domengFoe.Position = new Vec2(0.0, -11.0); // 11m from (0, 0) -> inside 14m reveal, outside 6m damage
            CastSlotAt(sim, 1, 2, new Vec2(0.0, -1.0));
            Assert.AreEqual(0, sim.Weapons.Walls.Count, "Pogs Slam destroyed enemy wall within 6m");
            Assert.GreaterOrEqual(sim.Weapons.RevealMarkers.Count, 2, "Pogs Slam created Snapshot markers within 14m");
            Assert.IsTrue(domengFoe.KnownRevealAlert, "Pogs Slam alerts enemies");
        }

        [Test]
        public void ManoyKit_SiliTubigTrumpoBantayAndTayaMapReveal()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState manoy = AddKid(sim, 1, 0, "manoy", new Vec2(0.0, 10.0));
            PlayerState kikayFoe = AddKid(sim, 2, 1, "kikay", new Vec2(0.0, 4.0));

            // S1 Sili-Tubig: cone reveal 4s (known) + 1.5s Blur
            CastSlotAt(sim, 1, 0, kikayFoe.Position);
            Assert.AreEqual(82, kikayFoe.Hp, "Sili-Tubig dealt 18 cone damage");
            Assert.Greater(kikayFoe.RevealedOutlineLeft, 3.5);
            Assert.Greater(kikayFoe.BlurLeft, 1.2);
            Assert.IsTrue(kikayFoe.KnownRevealAlert);

            // S2 Trumpo Bantay: 20s 8m sentry pings Snapshot markers every 1s; 1 stone breaks it
            manoy.Water = 100.0;
            CastSlotAt(sim, 1, 1, new Vec2(0.0, 6.0));
            Assert.AreEqual(1, sim.Weapons.Sentries.Count);
            Wait(sim, 0.2);
            Assert.GreaterOrEqual(sim.Weapons.RevealMarkers.Count, 1, "Trumpo Bantay pinged enemy in 8m");
            FireTiradorAt(sim, 2, new Vec2(0.0, 6.0), holdTicks: 1);
            Wait(sim, 0.3);
            Assert.AreEqual(0, sim.Weapons.Sentries.Count, "1 stone broke Trumpo Bantay sentry");

            // ULT Taya!: counts to 3 then snapshots EVERY enemy on the map including invisible Tago!
            sim.Weapons.RevealMarkers.Clear();
            kikayFoe.Position = new Vec2(35.0, -35.0);
            CastSlotAt(sim, 2, 1, Vec2.Zero); // Kikay goes invisible with Tago!
            Assert.Greater(kikayFoe.InvisibleLeft, 3.5);
            CastSlotAt(sim, 1, 2, Vec2.Zero);
            Assert.Greater(manoy.TayaCountLeft, 2.5);
            Wait(sim, 3.2);
            Assert.AreEqual(1, sim.Weapons.RevealMarkers.Count, "Taya! snapped invisible enemy across the map");
            Assert.AreEqual(kikayFoe.Position.x, sim.Weapons.RevealMarkers[0].Position.x, 0.01);
            Assert.IsTrue(kikayFoe.KnownRevealAlert);
        }

        // ---- 6. CONTROLLERS (TERE, PAMELA, DOMENG — ALL 9 SKILLS) ----

        [Test]
        public void TereKit_JackstoneLuksongLubidAndGarterNet()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState tere = AddKid(sim, 1, 0, "tere", new Vec2(0.0, 12.0));
            PlayerState foe = AddKid(sim, 2, 1, "narda", new Vec2(0.0, 4.0));

            // S1 Jackstone: 5s slow + light DoT
            CastSlotAt(sim, 1, 0, foe.Position);
            Wait(sim, 1.2);
            Assert.Less(foe.Hp, 100, "Jackstone dealt light DoT");
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Slow), "Jackstone slowed grounded enemy");

            // S2 Luksong Lubid: 2-point rope trips first grounded enemy crossing it (1s knockdown)
            tere.Water = 100.0;
            foe.Position = new Vec2(0.0, 1.0);
            CastSlotAt(sim, 1, 1, new Vec2(0.0, 3.0));
            Run(sim, 2, new Vec2(0.0, 1.0), 18); // cross rope at y=3
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Tripped), "Luksong Lubid tripped enemy crossing rope");

            // ULT Garter Net: 6m Root 1.25s (can still shoot!) -> Slow 2s after root ends
            Wait(sim, 2.5); // let tripped + CC immunity expire
            foe.Position = new Vec2(0.0, 5.0);
            foe.Hp = 100;
            CastSlotAt(sim, 1, 2, foe.Position);
            Assert.AreEqual(65, foe.Hp, "Garter Net dealt 35 damage");
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Root), "Garter Net rooted enemy");
            Assert.IsFalse(foe.Effects.CanMove(), "Root blocks movement");
            Assert.IsTrue(foe.Effects.CanCast(), "Root still allows shooting & skills");
            Wait(sim, 1.4);
            Assert.IsFalse(foe.Effects.Has(StatusEffects.Type.Root));
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Slow), "Garter Net transitioned into 2s Slow");
        }

        [Test]
        public void PamelaKit_HolensSpillLataMorphAndPatinteroLines()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState pamela = AddKid(sim, 1, 0, "pamela", new Vec2(0.0, 10.0));
            PlayerState foe = AddKid(sim, 2, 1, "narda", new Vec2(0.0, 4.0));

            // S1 Holens Spill: 3s cone Slip (slowed, no jump, no sprint)
            CastSlotAt(sim, 1, 0, foe.Position);
            Assert.AreEqual(85, foe.Hp);
            Assert.IsTrue(foe.Effects.IsSlipping(), "Holens Spill applied Slip");
            Tick(sim, 2, new Vec2(0.0, -1.0), PlayerInput.BtnJump);
            Assert.AreEqual(0.0, foe.AirY, 0.001, "Slip blocks jumping");

            // S2 Lata!: 1.5s can-polymorph (can wobble slowly, cannot shoot/cast/jump/dash)
            pamela.Water = 100.0;
            Wait(sim, 3.2);
            foe.Position = new Vec2(0.0, 4.0);
            CastSlotAt(sim, 1, 1, foe.Position);
            Wait(sim, 0.4);
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Polymorph), "Lata! turned enemy into a can");
            Assert.IsFalse(foe.CanAct(), "can cannot shoot or use skills");
            Assert.IsTrue(foe.Effects.CanMove(), "can may wobble slowly");
            Tick(sim, 2, new Vec2(1.0, 0.0), PlayerInput.BtnJump | PlayerInput.BtnDash);
            Assert.AreEqual(0.0, foe.AirY, 0.001, "can cannot jump");
            Assert.AreEqual(0.0, foe.DashLeft, 0.001, "can cannot dash");

            // ULT Patintero!: 5s 3 chalk lines, stuns 0.8s once per line
            Wait(sim, 3.0); // clear morph + CC immunity
            foe.Position = new Vec2(0.0, 1.0);
            foe.Hp = 100;
            CastSlotAt(sim, 1, 2, new Vec2(0.0, 4.0));
            Run(sim, 2, new Vec2(0.0, 1.0), 10); // cross first line
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Stun), "Patintero line stunned enemy");
            Assert.AreEqual(85, foe.Hp);
        }

        [Test]
        public void DomengKit_PabitinSnareSakoTrapAndLupaGroundOnlyQuake()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState domeng = AddKid(sim, 1, 0, "domeng", new Vec2(0.0, 10.0));
            PlayerState groundFoe = AddKid(sim, 2, 1, "narda", new Vec2(0.0, 5.0));
            PlayerState elevatedFoe = AddKid(sim, 3, 1, "inggo", new Vec2(2.0, 5.0));

            // S1 Pabitin Snare: 0.5s delay then Root 1.2s
            CastSlotAt(sim, 1, 0, groundFoe.Position);
            Wait(sim, 0.25);
            Assert.IsFalse(groundFoe.Effects.Has(StatusEffects.Type.Root), "still in 0.5s delay");
            Wait(sim, 0.35);
            Assert.IsTrue(groundFoe.Effects.Has(StatusEffects.Type.Root), "Pabitin Snare rooted after 0.5s");

            // S2 Sako Trap: hidden max-2 trap, 50% hop-slow 3s
            domeng.Water = 100.0;
            CastSlotAt(sim, 1, 1, new Vec2(0.0, 8.0));
            Wait(sim, 0.5); // arm trap
            groundFoe.Position = new Vec2(0.0, 8.0);
            sim.Step(DT);
            Assert.IsTrue(groundFoe.Effects.Has(StatusEffects.Type.Slow), "Sako Trap applied hop-slow");
            Assert.AreEqual(0.50, groundFoe.Effects.SlowMagnitude, 0.01);

            // ULT Lupa!: 0.6s delay, 8m ground-only knockup+stun 1s — elevated / Tiyakad kids are 100% SAFE!
            Wait(sim, 3.2); // clear prior CC
            groundFoe.Position = new Vec2(0.0, 6.0);
            groundFoe.Hp = 100;
            elevatedFoe.Position = new Vec2(2.0, 6.0);
            elevatedFoe.Hp = 100;
            elevatedFoe.IsElevated = true;
            CastSlotAt(sim, 1, 2, Vec2.Zero);
            Wait(sim, 0.7);
            Assert.AreEqual(60, groundFoe.Hp, "Lupa! hit grounded enemy for 40");
            Assert.IsTrue(groundFoe.Effects.Has(StatusEffects.Type.Stun), "Lupa! stunned grounded enemy");
            Assert.Greater(groundFoe.VelY, 0.0, "Lupa! knocked grounded enemy up");
            Assert.AreEqual(100, elevatedFoe.Hp, "elevated kid is 100% safe from Lupa!");
            Assert.IsFalse(elevatedFoe.Effects.Has(StatusEffects.Type.Stun));
        }

        // ---- 7. SHARED RULES, SIMDATA PARSING & SNAPSHOT WIRE ROUND-TRIP ----

        [Test]
        public void SharedRules_NoSoloOneShotSkillAndTaggedKidsKeepWallsTrapsSentries()
        {
            MatchSim sim = OpenSpecSim();
            PlayerState jopay = AddKid(sim, 1, 0, "jopay", new Vec2(0.0, 10.0));
            PlayerState kikay = AddKid(sim, 2, 0, "kikay", new Vec2(3.0, 10.0));
            PlayerState manoy = AddKid(sim, 3, 0, "manoy", new Vec2(-3.0, 10.0));
            PlayerState foe = AddKid(sim, 4, 1, "domeng", new Vec2(0.0, 0.0));

            // No single skill can tag a full-HP kid alone (even with oversized damage value)
            sim.DamageCombat(4, 150, 1, "test_nuke", false, isSkill: true);
            Assert.AreEqual(1, foe.Hp, "full-HP kid survives single skill hit at 1 HP");
            Assert.IsFalse(foe.Tagged);

            // Place wall, trap, sentry, then tag their owners -> entities STAY alive (docs/SPEC.md §5)!
            CastSlotAt(sim, 1, 0, new Vec2(0.0, 6.0));
            CastSlotAt(sim, 2, 0, new Vec2(3.0, 6.0));
            CastSlotAt(sim, 3, 1, new Vec2(-3.0, 6.0));
            sim.Kill(1);
            sim.Kill(2);
            sim.Kill(3);
            Assert.AreEqual(1, sim.Weapons.Walls.Count, "wall survives owner tag");
            Assert.AreEqual(1, sim.Weapons.Traps.Count, "trap survives owner tag");
            Assert.AreEqual(1, sim.Weapons.Sentries.Count, "sentry survives owner tag");
        }

        [Test]
        public void SimDataParsesSnakeCaseSkillEnumsWithZeroUnmappedKeys()
        {
            var data = new SimData();
            data.AddFile(@"{
                ""source"": ""res://data/characters/test_kid.tres"",
                ""class"": ""CharacterDef"",
                ""data"": {
                    ""id"": ""narda"",
                    ""display_name"": ""Narda"",
                    ""role"": ""striker"",
                    ""skills"": [
                        {
                            ""id"": ""luksong_tinik"",
                            ""display_name"": ""Luksong Tinik"",
                            ""slot"": ""s1"",
                            ""mechanic"": ""luksong_tinik"",
                            ""aim_camera"": ""zoom_out"",
                            ""reveal_kind"": ""outline"",
                            ""known_reveal_alert"": true,
                            ""water_cost"": 25,
                            ""cooldown"": 8.0,
                            ""max_charges"": 2
                        }
                    ]
                }
            }");
            CharacterDef kid = data.Get<CharacterDef>("res://data/characters/test_kid.tres");
            CollectionAssert.IsEmpty(data.UnmappedKeys);
            Assert.AreEqual(CharacterRole.Striker, kid.Role);
            Assert.AreEqual(1, kid.Skills.Count);
            Assert.AreEqual(SkillMechanic.LuksongTinik, kid.Skills[0].Mechanic);
            Assert.AreEqual(AimCameraMode.ZoomOut, kid.Skills[0].AimCamera);
            Assert.AreEqual(RevealKind.Outline, kid.Skills[0].RevealKind);
            Assert.AreEqual(2, kid.Skills[0].MaxCharges);
        }

        [Test]
        public void PassiveHealBtnUltimateRouteRootImmunityAndVisibilityChecksWorkDeterministically()
        {
            MatchSim sim = OpenSpecSim(99);
            PlayerState narda = AddKid(sim, 1, 0, "narda", new Vec2(0.0, 10.0));
            PlayerState kikay = AddKid(sim, 2, 0, "kikay", new Vec2(4.0, 10.0));
            PlayerState foe = AddKid(sim, 3, 1, "domeng", new Vec2(0.0, -30.0));

            // BtnUltimate also drives slot 2 ULT cleanly (press -> release)
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnUltimate, sim.Tick).WithAimPoint(foe.Position));
            sim.Step(DT);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithAimPoint(foe.Position));
            sim.Step(DT);
            Assert.Greater(narda.WeaponCooldowns[2], 78.0, "BtnUltimate fired spec ULT slot 2");

            // Root triggers 1s hard-CC immunity per docs/SPEC.md §7
            sim.ApplyEffect(3, StatusEffects.Type.Root, 0.5, 0.0);
            Assert.IsTrue(foe.Effects.Has(StatusEffects.Type.Root));
            Wait(sim, 0.6);
            sim.ApplyEffect(3, StatusEffects.Type.Stun, 1.0, 0.0);
            Assert.IsFalse(foe.Effects.Has(StatusEffects.Type.Stun), "1s CC immunity after Root blocks chained Stun");

            // Passive out-of-combat healing: 5 HP/s after 5s without damage/attacking/CC
            narda.Hp = 60;
            narda.OutOfCombatTime = 4.9;
            Wait(sim, 1.5);
            Assert.Greater(narda.Hp, 64, "5 HP/s out-of-combat passive healing after 5s");

            // Visibility queries: Tago! invisibility & >3m unrevealed hidden trap
            CastSlotAt(sim, 2, 0, new Vec2(4.0, 6.0));
            CastSlotAt(sim, 2, 1, Vec2.Zero);
            Assert.IsFalse(sim.IsEnemyVisibleToTeam(kikay, 1), "invisible Tago! Kikay hidden from enemy team");
            Assert.IsTrue(sim.IsEnemyVisibleToTeam(kikay, 0), "allies still see invisible Kikay");
            Assert.IsFalse(sim.IsTrapVisibleToTeam(sim.Weapons.Traps[0], 1), "trap > 3m hidden from enemy");
            Assert.IsTrue(sim.IsTrapVisibleToTeam(sim.Weapons.Traps[0], 0), "allies always see own trap");
        }
    }
}
