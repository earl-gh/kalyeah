using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Bots.BotKit;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>
    /// Deterministic tests proving all 12 kids' 36 skills load cleanly from unity/Data/*.json and that
    /// role-aware Spec-Kit bots (Striker, Defender, Seeker, Controller) make distinct, legal, role-authentic
    /// tactical decisions while respecting Tirador 7-shot reload/charge range and Water fountain management.
    /// </summary>
    public class RoleAwareBotsTests
    {
        static GameRules SpecRules()
        {
            GameRules rules = Kit.Rules();
            rules.SpecRules = true;
            rules.SpecKitsEnabled = true;
            rules.BotAimErrorDegrees = 0.0;
            return rules;
        }

        static MatchSim OpenSpecSim(long seed = 42) => new MatchSim(SpecRules(), OpenLayout(), seed);

        static PlayerState AddSpecKid(MatchSim sim, int id, int team, string kidId, Vec2 pos)
        {
            PlayerState p = sim.AddPlayer(id, team);
            p.Position = pos;
            p.CharacterId = kidId;
            sim.EquipSpecKit(id);
            return p;
        }

        static List<string> RecordCasts(MatchSim sim, int botId)
        {
            var casts = new List<string>();
            sim.WeaponCast += (id, skill) => { if (id == botId) casts.Add(skill); };
            return casts;
        }

        [Test]
        public void AllTwelveKidsHaveThreeBalancedSpecSkillsAndValidWaterAndTiradorRules()
        {
            GameRules rules = Kit.Data.GameRules;
            Assert.IsTrue(rules.SpecKitsEnabled, "spec_kits_enabled active in rules__game_rules.json");
            Assert.AreEqual(100.0, rules.WaterMax, 1e-6);
            Assert.AreEqual(2.0, rules.WaterRegenPerS, 1e-6);
            Assert.AreEqual(25.0, rules.WaterFountainRegenPerS, 1e-6);
            Assert.AreEqual(7, rules.TiradorMaxAmmo);
            Assert.AreEqual(1.5, rules.TiradorReloadSeconds, 1e-6);
            Assert.AreEqual(18.0, rules.TiradorMinRange, 1e-6);
            Assert.AreEqual(30.0, rules.TiradorMaxRange, 1e-6);
            Assert.AreEqual(1.5, rules.TiradorHeadshotMultiplier, 1e-6);

            Assert.AreEqual(12, rules.Characters.Count);
            var roleCounts = new Dictionary<CharacterRole, int>();
            var skillIds = new HashSet<string>();
            var mechanics = new HashSet<SkillMechanic>();
            foreach (CharacterDef kid in rules.Characters)
            {
                roleCounts[kid.Role] = roleCounts.TryGetValue(kid.Role, out int c) ? c + 1 : 1;
                Assert.AreEqual(3, kid.Skills.Count, $"{kid.Id} must define exactly 3 skills (S1, S2, ULT)");
                Assert.AreEqual(SkillSlot.S1, kid.Skills[0].Slot, $"{kid.Id} slot 0");
                Assert.AreEqual(SkillSlot.S2, kid.Skills[1].Slot, $"{kid.Id} slot 1");
                Assert.AreEqual(SkillSlot.Ult, kid.Skills[2].Slot, $"{kid.Id} slot 2");
                foreach (SkillDef sk in kid.Skills)
                {
                    Assert.IsTrue(skillIds.Add(sk.Id), $"unique skill id {sk.Id}");
                    Assert.IsTrue(mechanics.Add(sk.Mechanic), $"unique mechanic {sk.Mechanic}");
                    Assert.Less(sk.Damage, 100, $"{sk.Id} must never solo-tag a 100 HP kid");
                    Assert.Greater(sk.Cooldown, 0.0, $"{sk.Id} positive cooldown");
                    if (sk.Slot == SkillSlot.Ult)
                    {
                        Assert.AreEqual(0, sk.WaterCost, $"{sk.Id} ult costs 0 water");
                    }
                    else
                    {
                        Assert.GreaterOrEqual(sk.WaterCost, 20, $"{sk.Id} water cost floor");
                        Assert.LessOrEqual(sk.WaterCost, 35, $"{sk.Id} water cost ceiling");
                    }
                    if (sk.Mechanic == SkillMechanic.Piko)
                    {
                        Assert.AreEqual(RevealKind.Outline, sk.RevealKind);
                        Assert.IsFalse(sk.KnownRevealAlert, "Karding Piko is a silent reveal (docs/KITS.md)");
                    }
                    else if (sk.RevealKind != RevealKind.None)
                    {
                        Assert.IsTrue(sk.KnownRevealAlert, $"{sk.Id} alerts the revealed kid");
                    }
                }
            }
            Assert.AreEqual(3, roleCounts[CharacterRole.Striker]);
            Assert.AreEqual(3, roleCounts[CharacterRole.Defender]);
            Assert.AreEqual(3, roleCounts[CharacterRole.Seeker]);
            Assert.AreEqual(3, roleCounts[CharacterRole.Controller]);
            Assert.AreEqual(36, mechanics.Count);

            // Verify key AimCamera modes from docs/KITS.md
            var sim = OpenSpecSim(1);
            Assert.AreEqual(AimCameraMode.Ads, sim.GetSkillDef("tira_holens").AimCamera);
            Assert.AreEqual(AimCameraMode.Ads, sim.GetSkillDef("bato_execute").AimCamera);
            Assert.AreEqual(AimCameraMode.Ads, sim.GetSkillDef("yoyo").AimCamera);
            Assert.AreEqual(AimCameraMode.MapCast, sim.GetSkillDef("saranggola").AimCamera);
            Assert.AreEqual(AimCameraMode.ZoomOut, sim.GetSkillDef("bahay_bahayan").AimCamera);
            Assert.AreEqual(AimCameraMode.ZoomOut, sim.GetSkillDef("tinik_wall").AimCamera);
            Assert.AreEqual(AimCameraMode.ZoomOut, sim.GetSkillDef("garter_net").AimCamera);
            Assert.AreEqual(AimCameraMode.ZoomOut, sim.GetSkillDef("patintero_lines").AimCamera);
            Assert.AreEqual(AimCameraMode.ZoomOut, sim.GetSkillDef("pabitin_snare").AimCamera);
        }

        [Test]
        public void DefenderJopayHealsAndWallsHurtAllyUnderFire()
        {
            MatchSim sim = OpenSpecSim(11);
            PlayerState jopay = AddSpecKid(sim, 1, 0, "jopay", new Vec2(0.0, 4.0));
            PlayerState hurtAlly = AddSpecKid(sim, 2, 0, "narda", new Vec2(3.0, 4.0));
            PlayerState enemy = AddSpecKid(sim, 3, 1, "jumong", new Vec2(0.0, -5.0));
            hurtAlly.Hp = 40;
            enemy.Hp = 100;
            jopay.Water = 100.0;

            FightBrain brain = FightBrain.Fighter(jopay.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 120; i++)
            {
                sim.SetInput(1, brain.Think(jopay, sim));
                sim.Step(DT);
            }

            CollectionAssert.Contains(casts, "pabitin_heal", "Jopay drops Pabitin to heal her hurt ally");
            Assert.Greater(hurtAlly.Hp, 40, "Pabitin heal zone restores the hurt ally's HP");

            // With Pabitin on cooldown, Jopay protects the hurt ally with Bahay-Bahayan wall
            MatchSim simWall = OpenSpecSim(110);
            PlayerState jopay2 = AddSpecKid(simWall, 1, 0, "jopay", new Vec2(0.0, 4.0));
            PlayerState hurt2 = AddSpecKid(simWall, 2, 0, "narda", new Vec2(3.0, 4.0));
            AddSpecKid(simWall, 3, 1, "jumong", new Vec2(0.0, -5.0));
            hurt2.Hp = 40;
            jopay2.WeaponCooldowns[1] = 20.0;
            FightBrain brain2 = FightBrain.Fighter(jopay2.Position);
            brain2.Skill = 0.8;
            List<string> wallCasts = RecordCasts(simWall, 1);
            for (int i = 0; i < 60; i++)
            {
                simWall.SetInput(1, brain2.Think(jopay2, simWall));
                simWall.Step(DT);
            }
            CollectionAssert.Contains(wallCasts, "bahay_bahayan", "Jopay places Bahay-Bahayan wall between enemy and hurt ally");
        }

        [Test]
        public void DefenderOriangCastsLangitToSaveCriticalAlly()
        {
            MatchSim sim = OpenSpecSim(12);
            PlayerState oriang = AddSpecKid(sim, 1, 0, "oriang", new Vec2(0.0, 3.0));
            PlayerState criticalAlly = AddSpecKid(sim, 2, 0, "inggo", new Vec2(3.0, 3.0));
            AddSpecKid(sim, 3, 1, "narda", new Vec2(0.0, -6.0));
            criticalAlly.Hp = 28;

            FightBrain brain = FightBrain.Fighter(oriang.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 45; i++)
            {
                sim.SetInput(1, brain.Think(oriang, sim));
                sim.Step(DT);
            }

            CollectionAssert.Contains(casts, "langit", "Oriang pops Langit! when a nearby ally drops below 40% HP under fire");
        }

        [Test]
        public void DefenderJopayCastsPaloseboWhenEnemyEntersHomeBase()
        {
            MatchSim sim = OpenSpecSim(13);
            Vec2 homeBase = sim.Layout.BaseCenter(sim.SideForTeam(0));
            PlayerState jopay = AddSpecKid(sim, 1, 0, "jopay", homeBase + new Vec2(2.0, 0.0));
            AddSpecKid(sim, 2, 1, "jumong", homeBase + new Vec2(-1.5, 0.0));

            TeamBrain brain = TeamBrain.Defender(homeBase);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 45; i++)
            {
                sim.SetInput(1, brain.Think(jopay, sim));
                sim.Step(DT);
            }

            CollectionAssert.Contains(casts, "palosebo", "Jopay drops Palosebo to halt an enemy capturing home base");
        }

        [Test]
        public void SeekerKikayDeploysTripwireAndRevealsUnseenEnemiesWithSaranggola()
        {
            MatchSim sim = OpenSpecSim(14);
            PlayerState kikay = AddSpecKid(sim, 1, 0, "kikay", new Vec2(0.0, 12.0));
            PlayerState hiddenEnemy = AddSpecKid(sim, 2, 1, "kikay", new Vec2(0.0, -18.0));
            hiddenEnemy.InvisibleLeft = 4.0;
            kikay.Water = 100.0;

            FightBrain brain = FightBrain.Fighter(kikay.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 90; i++)
            {
                sim.SetInput(1, brain.Think(kikay, sim));
                sim.Step(DT);
            }

            Assert.IsTrue(casts.Contains("saranggola") || casts.Contains("teks_tripwire"),
                $"Kikay should cast Saranggola or Teks Tripwire for vision/traps (casts: {string.Join(",", casts)})");
        }

        [Test]
        public void SeekerKardingDisarmsVisibleEnemyTrapWithPiko()
        {
            MatchSim sim = OpenSpecSim(15);
            PlayerState karding = AddSpecKid(sim, 1, 0, "karding", new Vec2(0.0, 5.0));
            PlayerState enemyKikay = AddSpecKid(sim, 2, 1, "kikay", new Vec2(0.0, -45.0));
            // Enemy Kikay places a tripwire close enough for Karding to see (<= 3 m)
            enemyKikay.Position = new Vec2(0.0, 2.5);
            Kit.Cast(sim, 2, 0, Vec2.Zero);
            enemyKikay.Position = new Vec2(0.0, -45.0);
            Assert.Greater(sim.Weapons.Traps.Count, 0, "enemy trap placed");

            FightBrain brain = FightBrain.Fighter(karding.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 60; i++)
            {
                sim.SetInput(1, brain.Think(karding, sim));
                sim.Step(DT);
            }

            CollectionAssert.Contains(casts, "piko", "Karding casts Piko to reveal and disarm detected enemy traps");
        }

        [Test]
        public void ControllerTereZonesEnemyClusterWithGarterNetOrJackstone()
        {
            MatchSim sim = OpenSpecSim(16);
            PlayerState tere = AddSpecKid(sim, 1, 0, "tere", new Vec2(0.0, 8.0));
            AddSpecKid(sim, 2, 1, "narda", new Vec2(-1.5, -2.0));
            AddSpecKid(sim, 3, 1, "jumong", new Vec2(1.5, -2.0));

            FightBrain brain = FightBrain.Fighter(tere.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            for (int i = 0; i < 60; i++)
            {
                sim.SetInput(1, brain.Think(tere, sim));
                sim.Step(DT);
            }

            Assert.IsTrue(casts.Contains("garter_net") || casts.Contains("jackstone"),
                $"Tere zones clustered enemies with Garter Net / Jackstone (casts: {string.Join(",", casts)})");
        }

        [Test]
        public void ControllerDomengHoldsLupaWhenEnemiesAreAirborneAndCastsOnGroundedCluster()
        {
            MatchSim sim = OpenSpecSim(17);
            PlayerState domeng = AddSpecKid(sim, 1, 0, "domeng", Vec2.Zero);
            PlayerState e1 = AddSpecKid(sim, 2, 1, "narda", new Vec2(-2.0, -3.0));
            PlayerState e2 = AddSpecKid(sim, 3, 1, "jumong", new Vec2(2.0, -3.0));
            // Put S1 & S2 on cooldown so only Lupa! (ULT) is available
            domeng.WeaponCooldowns[0] = 20.0;
            domeng.WeaponCooldowns[1] = 20.0;

            FightBrain brain = FightBrain.Fighter(domeng.Position);
            brain.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            // Both enemies airborne -> Domeng holds Lupa!
            for (int i = 0; i < 20; i++)
            {
                e1.AirY = 1.2;
                e2.AirY = 1.2;
                sim.SetInput(1, brain.Think(domeng, sim));
                sim.Step(DT);
            }
            CollectionAssert.DoesNotContain(casts, "lupa_quake", "Domeng never wastes Lupa! on raised/airborne enemies");

            // Enemies land on the ground within 8 m -> Domeng casts Lupa!
            domeng.Position = Vec2.Zero;
            e1.Position = new Vec2(-2.0, -3.0);
            e2.Position = new Vec2(2.0, -3.0);
            e1.AirY = 0.0;
            e2.AirY = 0.0;
            e1.VelY = 0.0;
            e2.VelY = 0.0;
            for (int i = 0; i < 45; i++)
            {
                sim.SetInput(1, brain.Think(domeng, sim));
                sim.Step(DT);
            }
            CollectionAssert.Contains(casts, "lupa_quake", "Domeng casts Lupa! immediately on a grounded enemy cluster");
        }

        [Test]
        public void SeekerManoyDeploysTrumpoBantayAndControllerPamelaPolymorphsWithLata()
        {
            // Manoy deploys Trumpo Bantay sentry at choke when no active sentry exists
            MatchSim simM = OpenSpecSim(171);
            PlayerState manoy = AddSpecKid(simM, 1, 0, "manoy", new Vec2(0.0, 12.0));
            AddSpecKid(simM, 2, 1, "narda", new Vec2(0.0, -50.0));
            manoy.WeaponCooldowns[2] = 90.0;
            FightBrain manoyBrain = FightBrain.Fighter(manoy.Position);
            List<string> manoyCasts = RecordCasts(simM, 1);
            for (int i = 0; i < 60; i++)
            {
                simM.SetInput(1, manoyBrain.Think(manoy, simM));
                simM.Step(DT);
            }
            CollectionAssert.Contains(manoyCasts, "trumpo_bantay", "Manoy deploys Trumpo Bantay sentry");

            // Pamela throws Lata! to polymorph an active enemy threat
            MatchSim simP = OpenSpecSim(172);
            PlayerState pamela = AddSpecKid(simP, 1, 0, "pamela", new Vec2(0.0, 7.0));
            AddSpecKid(simP, 2, 1, "jumong", new Vec2(0.0, -5.0));
            pamela.WeaponCooldowns[2] = 90.0;
            FightBrain pamBrain = FightBrain.Fighter(pamela.Position);
            pamBrain.Skill = 0.9;
            List<string> pamCasts = RecordCasts(simP, 1);
            for (int i = 0; i < 60; i++)
            {
                simP.SetInput(1, pamBrain.Think(pamela, simP));
                simP.Step(DT);
            }
            CollectionAssert.Contains(pamCasts, "lata_morph", "Pamela shuts down enemy threat with Lata! can polymorph");
        }

        [Test]
        public void StrikerInggoHoldsBatoExecuteForLowHpTargetAndNardaBurstsLowHpTarget()
        {
            MatchSim sim = OpenSpecSim(18);
            PlayerState inggo = AddSpecKid(sim, 1, 0, "inggo", new Vec2(0.0, 12.0));
            PlayerState target = AddSpecKid(sim, 2, 1, "paeng", new Vec2(0.0, -10.0));
            inggo.WeaponCooldowns[0] = 20.0;
            inggo.WeaponCooldowns[1] = 20.0;

            FightBrain inggoBrain = FightBrain.Fighter(inggo.Position);
            inggoBrain.Skill = 0.9;
            List<string> inggoCasts = RecordCasts(sim, 1);

            // Full HP target: Inggo holds Bato! execute
            for (int i = 0; i < 15; i++)
            {
                target.Hp = 100;
                sim.SetInput(1, inggoBrain.Think(inggo, sim));
                sim.Step(DT);
            }
            CollectionAssert.DoesNotContain(inggoCasts, "bato_execute", "Inggo holds missing-HP execute Bato! when target is full HP");

            // Target drops to 45 HP: Inggo fires Bato!
            target.Hp = 45;
            for (int i = 0; i < 45; i++)
            {
                sim.SetInput(1, inggoBrain.Think(inggo, sim));
                sim.Step(DT);
            }
            CollectionAssert.Contains(inggoCasts, "bato_execute", "Inggo fires Bato! to execute a low-HP enemy");

            // Narda aggressively bursts a low-HP target
            MatchSim sim2 = OpenSpecSim(19);
            PlayerState narda = AddSpecKid(sim2, 1, 0, "narda", new Vec2(0.0, 6.0));
            PlayerState lowFoe = AddSpecKid(sim2, 2, 1, "kikay", new Vec2(0.0, -5.0));
            lowFoe.Hp = 50;
            FightBrain nardaBrain = FightBrain.Fighter(narda.Position);
            nardaBrain.Skill = 0.9;
            List<string> nardaCasts = RecordCasts(sim2, 1);
            for (int i = 0; i < 60; i++)
            {
                sim2.SetInput(1, nardaBrain.Think(narda, sim2));
                sim2.Step(DT);
            }
            Assert.IsTrue(nardaCasts.Contains("tsinelas_bagsik") || nardaCasts.Contains("tira_holens") || nardaCasts.Contains("luksong_tinik"),
                $"Narda should burst low-HP target (casts: {string.Join(",", nardaCasts)})");
        }

        [Test]
        public void BotsRespectWaterFountainRefillNoSpamReloadAndChargeRange()
        {
            // 1. Safe low-water bot retreats toward home base fountain and refills, never spamming into empty air
            MatchSim sim = OpenSpecSim(20);
            Vec2 homeBase = sim.Layout.BaseCenter(sim.SideForTeam(0));
            Vec2 enemyBase = sim.Layout.BaseCenter(-sim.SideForTeam(0));
            PlayerState narda = AddSpecKid(sim, 1, 0, "narda", new Vec2(0.0, 10.0));
            AddSpecKid(sim, 2, 1, "jopay", enemyBase);
            narda.Water = 10.0;

            TeamBrain rusher = TeamBrain.Rusher(homeBase, enemyBase);
            rusher.Skill = 0.8;
            List<string> casts = RecordCasts(sim, 1);

            double startDistToHome = narda.Position.DistanceTo(homeBase);
            for (int i = 0; i < 60; i++)
            {
                sim.SetInput(1, rusher.Think(narda, sim));
                sim.Step(DT);
            }
            Assert.AreEqual(0, casts.Count, "Striker never spams skills into empty air when no enemy is in sight");
            Assert.Less(narda.Position.DistanceTo(homeBase), startDistToHome - 1.0, "Low-water safe bot retreats toward home fountain");

            // 2. Empty/low Tirador triggers manual reload out of combat, and 24 m enemy triggers multi-tick charge
            MatchSim sim2 = OpenSpecSim(21);
            PlayerState karding = AddSpecKid(sim2, 1, 0, "karding", new Vec2(0.0, 12.0));
            AddSpecKid(sim2, 2, 1, "paeng", new Vec2(0.0, -50.0));
            karding.TiradorAmmo = 3;
            FightBrain brain = FightBrain.Fighter(karding.Position);
            PlayerInput reloadInput = brain.Think(karding, sim2);
            Assert.IsTrue(reloadInput.IsPressed(PlayerInput.BtnReload), "Bot reloads partial Tirador magazine out of combat");

            // Move enemy to 24 m (> 18 m tap range, < 30 m max range) and lock skills so Tirador charges
            karding.TiradorAmmo = 7;
            karding.WeaponCooldowns[0] = 30.0;
            karding.WeaponCooldowns[1] = 30.0;
            karding.WeaponCooldowns[2] = 90.0;
            sim2.Players[2].Position = new Vec2(0.0, -12.0);
            int primaryHoldTicks = 0;
            bool released = false;
            for (int i = 0; i < 45; i++)
            {
                PlayerInput inp = brain.Think(karding, sim2);
                if (inp.IsPressed(PlayerInput.BtnPrimary)) primaryHoldTicks++;
                else if (primaryHoldTicks > 0) { released = true; break; }
                sim2.SetInput(1, inp);
                sim2.Step(DT);
            }
            Assert.Greater(primaryHoldTicks, 2, "Bot holds BtnPrimary across multiple ticks to charge range past 18 m");
            Assert.IsTrue(released, "Bot releases BtnPrimary to fire the charged Tirador stone");
        }

        [Test]
        public void TeamCoachPrefersDefenderAsBaseGuardAndFiveVsFiveSpecBotsAreDeterministic()
        {
            MatchSim sim = OpenSpecSim(22);
            Vec2 homeBase = sim.Layout.BaseCenter(sim.SideForTeam(0));
            // Striker (id 1) is closer to home base than Defender (id 2), yet TeamCoach prefers Defender as base guard!
            AddSpecKid(sim, 1, 0, "narda", homeBase + new Vec2(0.0, -14.0));
            AddSpecKid(sim, 2, 0, "jopay", homeBase + new Vec2(0.0, -22.0));
            AddSpecKid(sim, 3, 0, "kikay", homeBase + new Vec2(0.0, -25.0));
            AddSpecKid(sim, 4, 1, "jumong", homeBase);

            var coach = new TeamCoach(0);
            coach.Add(sim, 1, 0.7);
            coach.Add(sim, 2, 0.7);
            coach.Add(sim, 3, 0.7);
            sim.Step(DT); // triggers CaptureTeam = 1 on home base
            coach.Plan = TeamCoach.CoachPlan.Assault;
            int guard = coach.GuardId(sim, -1);
            Assert.AreEqual(2, guard, "TeamCoach assigns Defender Jopay to guard home base ahead of closer Striker Narda");

            // Full determinism check across two independent runs with identical seed
            (Vec2 pos, int hp, double water) RunShort(long seed)
            {
                MatchSim s = new MatchSim(SpecRules(), Kit.Layout, seed);
                var c0 = new TeamCoach(0);
                var c1 = new TeamCoach(1);
                string[] t0 = { "narda", "jopay", "kikay", "tere" };
                string[] t1 = { "jumong", "oriang", "karding", "domeng" };
                for (int i = 0; i < 4; i++)
                {
                    PlayerState a = s.AddPlayer(i + 1, 0);
                    a.CharacterId = t0[i];
                    s.EquipSpecKit(i + 1);
                    c0.Add(s, i + 1, 0.6);

                    PlayerState b = s.AddPlayer(i + 5, 1);
                    b.CharacterId = t1[i];
                    s.EquipSpecKit(i + 5);
                    c1.Add(s, i + 5, 0.6);
                }
                for (int t = 0; t < 180; t++)
                {
                    c0.Drive(s);
                    c1.Drive(s);
                    s.Step(DT);
                }
                PlayerState p = s.Players[1];
                return (p.Position, p.Hp, p.Water);
            }

            var r1 = RunShort(777);
            var r2 = RunShort(777);
            Assert.AreEqual(r1.pos, r2.pos, "deterministic position");
            Assert.AreEqual(r1.hp, r2.hp, "deterministic HP");
            Assert.AreEqual(r1.water, r2.water, 1e-9, "deterministic Water");
        }
    }
}
