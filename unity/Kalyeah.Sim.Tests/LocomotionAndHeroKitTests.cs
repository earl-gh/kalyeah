using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Port of test_barangay_5v5.gd (section 4): 3-superpower hero-action loadout, Overhaul v2 analog
    /// walk/run/crouch/slide/jump locomotion, Merienda power-up stands (Palamig, Teks, Turon),
    /// Tirador primary fire, Luksong-Tinik slow-cleansing dash, and Bagsak ultimate charge & cast.
    /// </summary>
    public class LocomotionAndHeroKitTests
    {
        static GameRules NoTripRules()
        {
            GameRules copy = Rules();
            copy.TripChance = 0.0;
            return copy;
        }

        static MapLayout OpenCircularLayout() => new MapLayout
        {
            Tiles = new Vec2I(58, 58),
            TileSize = 3.0,
            ArenaRadius = 84.0,
            BaseInset = 16.0,
            BaseRadius = 12.5,
            BaseCorner = 3.0,
            SpawnOffset = 6.0,
            RescueSize = new Vec2(8.0, 14.0),
            TaggedPlaces = 5,
            BoundaryThickness = 2.0,
            PowerupSpots = new List<Vec2>
            {
                new Vec2(-31.5, 4.8),
                new Vec2(5.8, 6.4),
                new Vec2(-5.8, -6.4),
                new Vec2(31.5, -4.8),
            },
        };

        [Test]
        public void ThreeSuperpowerLoadoutAndFighterAutoEquip()
        {
            GameRules rules = Data.GameRules;
            Assert.AreEqual(3, rules.LoadoutSize);
            Assert.IsFalse(rules.CardCycle);
            Assert.IsTrue(rules.SprintEnabled, "a steady jog runs (docs/SPEC.md §7)");
            Assert.AreEqual(5.25, rules.MoveSpeed, 0.001);
            Assert.AreEqual(0.62, rules.WalkRunThreshold, 0.001);

            var sim = new MatchSim(rules, OpenCircularLayout(), 77);
            PlayerState p1 = sim.AddPlayer(1, 0);
            PlayerState p2 = sim.AddPlayer(2, 1);
            Assert.IsTrue(sim.SetLoadout(1, "bato_light", "papel_shield", "lata"));
            Assert.AreEqual(3, p1.Weapons.Count);
            Assert.IsFalse(sim.SetLoadout(1, "bato_light", "bato_light", "lata"));
            sim.Equip(2, true);
            Assert.AreEqual(3, p2.Weapons.Count);
            Assert.IsFalse(WeaponPick.DealsNoDamage(p2.Weapons, sim.WeaponDefs));
        }

        [Test]
        public void OverhaulV2AnalogWalkRunCrouchSlideAndJumpParabola()
        {
            GameRules rules = NoTripRules();
            var sim = new MatchSim(rules, OpenCircularLayout(), 808);
            PlayerState hero = sim.AddPlayer(1, 0);
            hero.Position = new Vec2(-12.0, 12.0);
            sim.SetLoadout(1, "bato_light", "papel_shield", "lata");

            Tick(sim, 1, new Vec2(-0.5, 0.0), 0);
            Assert.IsTrue(hero.Walking, "analog stick < 0.62 enters Walk");
            Assert.AreEqual(0.0, hero.RunTime, 1e-4, "walking never accumulates trip RunTime");

            Tick(sim, 1, new Vec2(-1.0, 0.0), 0);
            Assert.IsFalse(hero.Walking, "full deflection >= 0.62 enters Run");

            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCrouch);
            Assert.IsTrue(hero.Crouching, "BtnCrouch from rest enters Crouch");
            Vec2 crouchOrigin = hero.Position;
            Tick(sim, 1, new Vec2(-1.0, 0.0), PlayerInput.BtnCrouch);
            Assert.IsTrue(hero.Crouching, "holding BtnCrouch keeps Crouch while moving");
            Assert.AreEqual(rules.MoveSpeed * rules.CrouchSpeedShare * DT, hero.Position.DistanceTo(crouchOrigin), 0.03);

            Run(sim, 1, new Vec2(1.0, 0.0), 4);
            Vec2 slideOrigin = hero.Position;
            Tick(sim, 1, new Vec2(1.0, 0.0), PlayerInput.BtnSlide);
            Assert.Greater(hero.SlideLeft, 0.0, "BtnSlide starts ground slide");
            Assert.Greater(hero.Position.DistanceTo(slideOrigin), rules.MoveSpeed * DT * 1.2, "ground slide bursts faster than run");
            Run(sim, 1, Vec2.Zero, GMath.Ceili((rules.SlideDuration + 0.05) / DT));
            Assert.AreEqual(0.0, hero.SlideLeft, 1e-4);

            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnJump);
            Assert.Greater(hero.AirY, 0.0, "BtnJump launches jump parabola");
            double peakAirY = hero.AirY;
            for (int t = 0; t < 30; t++)
            {
                Tick(sim, 1, Vec2.Zero, 0);
                if (hero.AirY > peakAirY) peakAirY = hero.AirY;
            }
            Assert.GreaterOrEqual(peakAirY, MatchSim.JumpClearHeight, "jump apex clears grounded pavement snares");
            Assert.AreEqual(0.0, hero.AirY, 0.001, "jump lands cleanly on street plane");
        }

        [Test]
        public void MeriendaPowerupStandsGrantPalamigTeksAndTuronBuffsAndRespawn()
        {
            GameRules rules = NoTripRules();
            var sim = new MatchSim(rules, OpenCircularLayout(), 808);
            PlayerState hero = sim.AddPlayer(1, 0);
            PlayerState rival = sim.AddPlayer(2, 1);
            PlayerState dummy = sim.AddDummy(3, 1, OpenCircularLayout().PowerupSpots[3], DummyBrain.Standing());
            rival.Position = new Vec2(20.0, 20.0);

            var picked = new List<(int player, int stand, int kind)>();
            sim.PowerupTaken += (pid, sid, kind) => picked.Add((pid, sid, kind));

            Assert.AreEqual(4, sim.Powerups.Count);
            sim.Step(DT);
            Assert.IsTrue(sim.Powerups[3].Active, "training dummies never consume Merienda stands");
            Assert.AreEqual(0, dummy.BuffType);

            // Stand 0: Palamig (instant heal + speed bonus)
            hero.Hp = rules.PlayerMaxHp - 20;
            hero.Position = sim.Powerups[0].Pos;
            Tick(sim, 1, new Vec2(0.45, 0.0), 0);
            CollectionAssert.AreEqual(new[] { (1, 1, MatchSim.PowerupPalamig) }, picked);
            Assert.AreEqual(MatchSim.PowerupPalamig, hero.BuffType);
            Assert.Greater(hero.BuffLeft, 0.0);
            Assert.AreEqual(rules.PlayerMaxHp - 20 + rules.PalamigHeal, hero.Hp);
            Assert.IsFalse(sim.Powerups[0].Active);
            Assert.Greater(sim.Powerups[0].RespawnLeft, 0.0);

            Vec2 speedOrigin = hero.Position;
            Tick(sim, 1, new Vec2(1.0, 0.0), 0);
            Assert.AreEqual(rules.MoveSpeed * (1.0 + rules.PalamigSpeedBonus) * DT, hero.Position.DistanceTo(speedOrigin), 0.03);

            // Stand 1: Teks (+ult bonus + dealt damage bonus)
            hero.UltCharge = 0.0;
            hero.Position = sim.Powerups[1].Pos;
            Tick(sim, 1, Vec2.Zero, 0);
            Assert.AreEqual(MatchSim.PowerupTeks, hero.BuffType);
            Assert.GreaterOrEqual(hero.UltCharge, rules.TeksUltBonus);
            sim.Damage(2, 20, 1);
            Assert.AreEqual(rules.PlayerMaxHp - GMath.Roundi(20 * (1.0 + rules.TeksDamageBonus)), rival.Hp);

            // Stand 2: Turon (damage reduction + HP regen over time)
            hero.Hp = rules.PlayerMaxHp - 30;
            hero.Position = sim.Powerups[2].Pos;
            Tick(sim, 1, Vec2.Zero, 0);
            Assert.AreEqual(MatchSim.PowerupTuron, hero.BuffType);
            int hpBeforeHit = hero.Hp;
            sim.Damage(1, 20, 2);
            Assert.AreEqual(hpBeforeHit - GMath.Roundi(20 * (1.0 - rules.TuronDamageReduction)), hero.Hp);
            int hpAfterHit = hero.Hp;
            Run(sim, 1, Vec2.Zero, 35);
            Assert.Greater(hero.Hp, hpAfterHit, "Turon regenerates HP over time");

            // Stand 0 respawns after PowerupRespawnSeconds
            hero.Position = new Vec2(0.0, 40.0);
            Wait(sim, rules.PowerupRespawnSeconds + 0.5);
            Assert.IsTrue(sim.Powerups[0].Active, "picked Merienda stand respawns after cooldown");
        }

        [Test]
        public void TiradorPrimaryLuksongTinikDashAndBagsakUltimateCombatKit()
        {
            GameRules rules = NoTripRules();
            var sim = new MatchSim(rules, OpenCircularLayout(), 808);
            PlayerState hero = sim.AddPlayer(1, 0);
            PlayerState rival = sim.AddPlayer(2, 1);
            hero.Position = new Vec2(0.0, 10.0);
            rival.Position = new Vec2(0.0, 2.0);
            sim.SetLoadout(1, "bato_light", "papel_shield", "lata");
            sim.SetLoadout(2, "bato_light", "papel_shield", "lata");

            var castIds = new List<string>();
            var dashedPlayers = new List<int>();
            var ultPlayers = new List<int>();
            sim.WeaponCast += (_, wid) => castIds.Add(wid);
            sim.PlayerDashed += pid => dashedPlayers.Add(pid);
            sim.UltimateCast += pid => ultPlayers.Add(pid);

            // 1) Tirador (BtnPrimary)
            Assert.IsNotNull(rules.PrimaryWeapon);
            int rivalHpBefore = rival.Hp;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnPrimary, sim.Tick).WithAimPoint(rival.Position));
            sim.Step(DT);
            CollectionAssert.Contains(castIds, rules.PrimaryWeapon.Id);
            Assert.Greater(hero.PrimaryCd, 0.0);
            Run(sim, 1, Vec2.Zero, 20);
            Assert.Less(rival.Hp, rivalHpBefore);
            Assert.Greater(hero.UltCharge, 0.0);

            // 2) Luksong-Tinik (BtnDash): cleanses Slow and bursts forward
            sim.ApplyEffect(1, StatusEffects.Type.Slow, 3.0, 0.55);
            Assert.IsTrue(hero.Effects.Has(StatusEffects.Type.Slow));
            Vec2 dashOrigin = hero.Position;
            Tick(sim, 1, new Vec2(1.0, 0.0), PlayerInput.BtnDash);
            Assert.IsFalse(hero.Effects.Has(StatusEffects.Type.Slow), "dash cleanses Slow");
            CollectionAssert.Contains(dashedPlayers, 1);
            Assert.Greater(hero.DashCd, 0.0);
            Run(sim, 1, Vec2.Zero, GMath.Ceili(rules.DashDuration / DT));
            Assert.Greater(hero.Position.DistanceTo(dashOrigin), rules.MoveSpeed * rules.DashDuration * 1.4);

            // 3) Bagsak Ultimate (BtnUltimate): gated < 100%, fires & resets at 100%
            Assert.IsNotNull(rules.UltimateWeapon);
            hero.UltCharge = 85.0;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnUltimate, sim.Tick).WithAimPoint(rival.Position));
            sim.Step(DT);
            CollectionAssert.IsEmpty(ultPlayers);

            Tick(sim, 1, Vec2.Zero, 0);
            hero.UltCharge = 100.0;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnUltimate, sim.Tick).WithAimPoint(rival.Position));
            sim.Step(DT);
            CollectionAssert.Contains(ultPlayers, 1);
            CollectionAssert.Contains(castIds, rules.UltimateWeapon.Id);
            Assert.AreEqual(0.0, hero.UltCharge, 0.001);
        }
    }
}
