using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Port of test_weapons.gd (cooldown mode, the shipped rules), plus WallMotion, WeaponPick and the data loader.</summary>
    public class WeaponTests
    {
        const int Caster = 1;
        static GameRules R => Data.GameRules;

        static MatchSim Sim() => new MatchSim(R, CoverStreet(), 11);

        static PlayerState MakeCaster(MatchSim sim, string first, string second = "papel_shield")
        {
            PlayerState caster = sim.AddPlayer(Caster, 0);
            caster.Position = Vec2.Zero;
            Assert.IsTrue(sim.SetLoadout(Caster, first, second));
            return caster;
        }

        static PlayerState Target(MatchSim sim, int id, Vec2 at) => sim.AddDummy(id, 1, at, DummyBrain.Standing());

        static WeaponDef Def(string id) => R.Weapons.First(w => w.Id == id);

        [Test]
        public void TheWeaponsMatchTheGddTable()
        {
            Assert.AreEqual(14, R.Weapons.Count);
            var expected = new Dictionary<string, (WeaponDef.WeaponKind, double, double, int)>
            {
                { "bato_light", (WeaponDef.WeaponKind.Attack, 4.8, 20.0, 18) },
                { "bato_heavy", (WeaponDef.WeaponKind.Attack, 10.5, 24.0, 36) },
                { "gunting_light", (WeaponDef.WeaponKind.Attack, 5.4, 7.5, 9) },
                { "gunting_heavy", (WeaponDef.WeaponKind.Attack, 9.0, 9.0, 38) },
                { "papel_trap", (WeaponDef.WeaponKind.CrowdControl, 9.5, 18.0, 0) },
                { "papel_shield", (WeaponDef.WeaponKind.Block, 11.0, 0.0, 0) },
                { "tsinelas_light", (WeaponDef.WeaponKind.Attack, 7.0, 21.0, 9) },
                { "tsinelas_heavy", (WeaponDef.WeaponKind.Attack, 9.0, 24.0, 25) },
                { "lata", (WeaponDef.WeaponKind.CrowdControl, 12.0, 20.0, 14) },
                { "jacks", (WeaponDef.WeaponKind.CrowdControl, 10.5, 19.0, 7) },
                { "bola", (WeaponDef.WeaponKind.CrowdControl, 9.8, 23.0, 14) },
                { "trumpo", (WeaponDef.WeaponKind.CrowdControl, 11.5, 22.0, 22) },
            };
            foreach (var pair in expected)
            {
                WeaponDef def = Def(pair.Key);
                Assert.AreEqual(pair.Value.Item1, def.Kind, pair.Key);
                Assert.AreEqual(pair.Value.Item2, def.Cooldown, 0.001, pair.Key);
                Assert.AreEqual(pair.Value.Item3, def.MaxRange, 0.001, pair.Key);
                Assert.AreEqual(pair.Value.Item4, def.Damage, pair.Key);
                Assert.AreNotEqual("", def.StreetGame);
            }
            Assert.AreEqual(StatusEffects.Type.Stun, Def("bato_heavy").Effect);
            Assert.AreEqual(StatusEffects.Type.Slow, Def("papel_trap").Effect);
            Assert.AreEqual(StatusEffects.Type.Polymorph, Def("lata").Effect);
            Assert.AreEqual(StatusEffects.Type.Bounce, Def("bola").Effect);
            Assert.AreEqual(StatusEffects.Type.Airborne, Def("trumpo").Effect);
        }

        [Test]
        public void LoadoutRejectsDuplicatesAndUnknownWeapons()
        {
            MatchSim sim = Sim();
            sim.AddPlayer(Caster, 0);
            Assert.IsFalse(sim.SetLoadout(Caster, "bato_light", "bato_light"));
            Assert.IsFalse(sim.SetLoadout(Caster, "bato_light", "nope"));
            Assert.IsTrue(sim.SetLoadout(Caster, "bato_light", "bato_heavy"));
            Assert.IsTrue(sim.SetLoadout(Caster, "bato_light", "bato_heavy", "lata"));
            CollectionAssert.AreEqual(new[] { "bato_light", "bato_heavy", "lata" }, sim.Players[Caster].Weapons);
        }

        [Test]
        public void RandomLoadoutIsDistinctWeaponsAndSeeded()
        {
            var a = new MatchSim(R, Layout, 5);
            var b = new MatchSim(R, Layout, 5);
            for (int i = 0; i < 50; i++)
            {
                List<string> pick = a.RandomLoadout();
                Assert.AreEqual(R.LoadoutSize, pick.Count);
                Assert.AreEqual(pick.Count, pick.Distinct().Count());
                CollectionAssert.AreEqual(pick, b.RandomLoadout());
            }
        }

        [Test]
        public void WeaponsCastOnReleaseAndGoOnCooldown()
        {
            MatchSim sim = Sim();
            PlayerState caster = MakeCaster(sim, "bato_light");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -5.0));
            Tick(sim, Caster, Vec2.Zero, PlayerInput.BtnWeapon1);
            Assert.AreEqual(0.0, caster.WeaponCooldowns[0], "holding does not cast yet");
            Tick(sim, Caster, Vec2.Zero, 0);
            Assert.AreEqual(4.8, caster.WeaponCooldowns[0], 0.001);
            Wait(sim, 1.0);
            Assert.AreEqual(82, enemy.Hp);
        }

        [Test]
        public void ReleaseOverTheCancelZoneDoesNotCast()
        {
            MatchSim sim = Sim();
            PlayerState caster = MakeCaster(sim, "bato_light");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -5.0));
            Cast(sim, Caster, 0, Vec2.Zero, 1, true);
            Wait(sim, 1.0);
            Assert.AreEqual(0.0, caster.WeaponCooldowns[0]);
            Assert.AreEqual(100, enemy.Hp);
        }

        [Test]
        public void CooldownBlocksRecasting()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "bato_light");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -5.0));
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 1.0);
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 1.0);
            Assert.AreEqual(82, enemy.Hp);
            Wait(sim, 3.2);
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 1.0);
            Assert.AreEqual(64, enemy.Hp);
        }

        [Test]
        public void CannotCastWhileStunnedPolymorphedOrTagged()
        {
            MatchSim sim = Sim();
            PlayerState caster = MakeCaster(sim, "bato_light");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -5.0));
            sim.ApplyEffect(Caster, StatusEffects.Type.Stun, 1.0, 0.0);
            Cast(sim, Caster, 0, Vec2.Zero);
            Assert.AreEqual(0.0, caster.WeaponCooldowns[0]);
            Wait(sim, 2.1);
            sim.ApplyEffect(Caster, StatusEffects.Type.Polymorph, 1.0, 0.0);
            Cast(sim, Caster, 0, Vec2.Zero);
            Assert.AreEqual(0.0, caster.WeaponCooldowns[0]);
            Wait(sim, 1.0);
            sim.Damage(Caster, 100);
            Assert.IsTrue(caster.Tagged);
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 1.0);
            Assert.AreEqual(100, enemy.Hp);
        }

        [Test]
        public void SlowReducesWalkingSpeed()
        {
            MatchSim sim = Sim();
            PlayerState caster = MakeCaster(sim, "bato_light");
            sim.ApplyEffect(Caster, StatusEffects.Type.Slow, 5.0, 0.4);
            Vec2 start = caster.Position;
            Run(sim, Caster, new Vec2(1.0, 0.0), 30);
            Assert.AreEqual(R.MoveSpeed * 0.6, caster.Position.x - start.x, 0.08);
        }

        [Test]
        public void AHitTellsWhichWeaponLanded()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "bato_light");
            Target(sim, 2, new Vec2(0.0, -4.0));
            var hits = new List<(int, string)>();
            sim.WeaponHit += (id, w) => hits.Add((id, w));
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 2.0);
            CollectionAssert.AreEqual(new[] { (2, "bato_light") }, hits);
        }

        [Test]
        public void TargetedBatoHitsTheNearestEnemyOnlyInRange()
        {
            MatchSim sim = Sim();
            PlayerState caster = MakeCaster(sim, "bato_light");
            PlayerState far = Target(sim, 2, new Vec2(0.0, -22.0));
            Cast(sim, Caster, 0, Vec2.Zero);
            Assert.AreEqual(0.0, caster.WeaponCooldowns[0], "nobody in range: nothing thrown, no cooldown");
            PlayerState near = Target(sim, 3, new Vec2(3.0, -3.0));
            Cast(sim, Caster, 0, Vec2.Zero);
            Wait(sim, 1.0);
            Assert.AreEqual(82, near.Hp);
            Assert.AreEqual(100, far.Hp);
        }

        [Test]
        public void GroundAoeLandsAfterTheDelayWithStun()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "bato_heavy");
            PlayerState hit = Target(sim, 2, new Vec2(0.0, -6.0));
            PlayerState nearMiss = Target(sim, 3, new Vec2(5.2, -6.0));
            Cast(sim, Caster, 0, new Vec2(0.0, -6.0) / 24.0);
            Wait(sim, 0.3);
            Assert.AreEqual(100, hit.Hp);
            Wait(sim, 0.4);
            Assert.AreEqual(64, hit.Hp);
            Assert.IsTrue(hit.Effects.Has(StatusEffects.Type.Stun));
            Assert.AreEqual(100, nearMiss.Hp);
        }

        [Test]
        public void GuntingLightSnipsByHoldTimeInACone()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "gunting_light");
            PlayerState front = Target(sim, 2, new Vec2(0.0, -2.0));
            PlayerState behind = Target(sim, 3, new Vec2(0.0, 2.0));
            PlayerState tooFar = Target(sim, 4, new Vec2(0.5, -9.8));
            Cast(sim, Caster, 0, new Vec2(0.0, -1.0), 1);
            Assert.AreEqual(82, front.Hp, "a tap is 2 snips of 9");
            Assert.AreEqual(100, behind.Hp);
            Assert.AreEqual(100, tooFar.Hp);
            Wait(sim, 5.6);
            Cast(sim, Caster, 0, new Vec2(0.0, -1.0), 40);
            Assert.AreEqual(37, front.Hp, "a long hold is 5 snips of 9");
        }

        [Test]
        public void SnipCountFromHoldTime()
        {
            WeaponDef def = Def("gunting_light");
            Assert.AreEqual(2, def.SnipCount(0.0));
            Assert.AreEqual(3, def.SnipCount(0.23));
            Assert.AreEqual(4, def.SnipCount(0.45));
            Assert.AreEqual(5, def.SnipCount(5.0));
        }

        [Test]
        public void GuntingHeavyIsOneDelayedSnip()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "gunting_heavy");
            PlayerState enemy = Target(sim, 2, new Vec2(1.0, -2.5));
            Cast(sim, Caster, 0, new Vec2(0.0, -1.0));
            Assert.AreEqual(100, enemy.Hp);
            Wait(sim, 0.6);
            Assert.AreEqual(62, enemy.Hp);
        }

        [Test]
        public void PapelTrapArmsThenBecomesASlowZone()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "papel_trap");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -4.0));
            Cast(sim, Caster, 0, new Vec2(0.0, -4.0) / 18.0);
            Wait(sim, 0.4);
            Assert.IsFalse(enemy.Effects.Has(StatusEffects.Type.Slow));
            Wait(sim, 0.5);
            Assert.IsTrue(enemy.Effects.Has(StatusEffects.Type.Slow));
            Assert.AreEqual(100, enemy.Hp);
            Wait(sim, 4.3);
            Assert.IsFalse(enemy.Effects.Has(StatusEffects.Type.Slow));
            CollectionAssert.IsEmpty(sim.Weapons.Zones);
        }

        [Test]
        public void PapelTrapWaitsForAnEnemy()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "papel_trap");
            Cast(sim, Caster, 0, new Vec2(0.0, -1.0));
            Wait(sim, 3.0);
            Assert.AreEqual(1, sim.Weapons.Zones.Count);
            Assert.AreEqual(WeaponSystem.ZoneKind.Trap, sim.Weapons.Zones[0].Kind);
        }

        [Test]
        public void AnEnemyShieldStopsAThrow()
        {
            MatchSim sim = Sim();
            MakeCaster(sim, "tsinelas_heavy", "bato_light");
            PlayerState enemy = Target(sim, 2, new Vec2(0.0, -10.0));
            enemy.Facing = new Vec2(0.0, 1.0);
            Assert.IsTrue(sim.SetLoadout(2, "papel_shield", "bato_light"));
            Assert.IsTrue(sim.Weapons.Fire(sim, enemy, Def("papel_shield"), new Vec2(0.0, 1.0), 0.0));
            Assert.AreEqual(1, sim.Weapons.Shields.Count);
            Assert.IsTrue(sim.Weapons.BlocksSegment(0, new Vec2(0.0, -5.0), new Vec2(0.0, -12.0)), "the wall stands between them");
            Assert.IsFalse(sim.Weapons.BlocksSegment(1, new Vec2(0.0, -5.0), new Vec2(0.0, -12.0)), "never for its own team");
        }
    }

    public class WallMotionAndPickTests
    {
        [Test]
        public void AWallWithoutMotionDataNeverMovesOrWidens()
        {
            var def = new WeaponDef();
            Assert.AreEqual(0.0, WallMotion.Push(def, 5.0));
            Assert.AreEqual(1.0, WallMotion.WidthScale(def, 5.0));
        }

        [Test]
        public void APushedWallGlidesWithACubicEaseOutAndAWideningOne()
        {
            var def = new WeaponDef { PushDistance = 3.0, PushTime = 1.0, ExpandFactor = 2.0, ExpandDelay = 0.5, ExpandTime = 1.0 };
            Assert.AreEqual(0.0, WallMotion.Push(def, 0.0));
            Assert.AreEqual(3.0 * (1.0 - 0.125), WallMotion.Push(def, 0.5), 1e-9);
            Assert.AreEqual(3.0, WallMotion.Push(def, 2.0));
            Assert.AreEqual(1.0, WallMotion.WidthScale(def, 0.4));
            Assert.AreEqual(1.5, WallMotion.WidthScale(def, 1.0), 1e-9);
            Assert.AreEqual(2.0, WallMotion.WidthScale(def, 3.0));
        }

        [Test]
        public void TheWeaponPickReplacesTheOldestAndFillsOnTimeout()
        {
            var ids = Data.GameRules.Weapons.Select(w => w.Id).ToList();
            var pick = new WeaponPick(ids, 2.0);
            pick.Toggle("bato_light");
            pick.Toggle("lata");
            pick.Toggle("jacks");
            CollectionAssert.AreEqual(new[] { "lata", "jacks" }, pick.Picks);
            Assert.IsTrue(pick.CanConfirm());
            pick.Toggle("lata");
            CollectionAssert.AreEqual(new[] { "jacks" }, pick.Picks);
            var rng = new GodotRng(3);
            Assert.IsFalse(pick.Step(1.0, rng));
            Assert.IsTrue(pick.Step(1.5, rng));
            Assert.AreEqual(2, pick.Picks.Count);
            Assert.AreEqual(2, pick.Picks.Distinct().Count());
        }

        [Test]
        public void TheSuggestionIsAHomingAttackAndABlock()
        {
            CollectionAssert.AreEqual(new[] { "bato_light", "papel_shield" }, WeaponPick.Suggestion(Data.GameRules.Weapons));
            List<string> three = WeaponPick.SuggestionFor(Data.GameRules.Weapons, 3);
            Assert.AreEqual(3, three.Count);
            Assert.AreEqual("bato_light", three[0]);
        }
    }

    public class SimDataTests
    {
        [Test]
        public void EveryExportedPropertyHasAHomeInThePort()
        {
            SimData data = SimData.Load(GoldenMatchTests.DataDir);
            foreach (string source in data.Sources)
            {
                string cls = data.ClassOf(source);
                if (cls == "CameraRules" || cls == "NetRules") continue;
                data.Get<object>(source);
            }
            CollectionAssert.IsEmpty(data.UnmappedKeys);
        }

        [Test]
        public void ReferencesResolveToSharedInstances()
        {
            SimData data = Data;
            Assert.AreSame(data.Weapon("bato_light"), data.GameRules.Weapons[0]);
            Assert.AreSame(data.Weapon("tirador_bato"), data.GameRules.PrimaryWeapon);
            Assert.AreEqual(10, data.GameRules.Characters.Count);
            Assert.AreEqual(3, data.GameRules.BotLevels.Count);
            Assert.AreEqual(84.0, data.MapLayout.ArenaRadius);
            Assert.IsNotNull(data.StreetModeRules("patintero"));
            Assert.IsNull(data.StreetModeRules("street"));
            Assert.AreEqual("medium", data.GameRules.WithBotLevel("nope").BotLevelDefault);
        }
    }
}
