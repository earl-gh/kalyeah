using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Ports of test_collision.gd, test_status_effects.gd, test_cc_immunity.gd, test_match_sim.gd, test_match_sim_skills.gd.</summary>
    public class CollisionTests
    {
        static readonly Rect2 R = new Rect2(0.0, 0.0, 4.0, 2.0);

        static Rect2 Cover() => CoverStreet().ObstacleRects()[0];

        [Test]
        public void PushOutLeavesAFreeCircleAlone()
        {
            Assert.AreEqual(new Vec2(-3.0, 1.0), Collision.PushOut(new Vec2(-3.0, 1.0), 0.5, R));
            Assert.AreEqual(new Vec2(-0.5, 1.0), Collision.PushOut(new Vec2(-0.5, 1.0), 0.5, R), "just touching");
        }

        [Test]
        public void PushOutFromAFace()
        {
            Vec2 result = Collision.PushOut(new Vec2(-0.2, 1.0), 0.5, R);
            Assert.AreEqual(-0.5, result.x, 1e-4);
            Assert.AreEqual(1.0, result.y, 1e-4);
        }

        [Test]
        public void PushOutFromACornerIsDiagonal()
        {
            Vec2 result = Collision.PushOut(new Vec2(-0.1, -0.1), 0.5, R);
            Assert.AreEqual(0.5, result.DistanceTo(Vec2.Zero), 1e-4);
            Assert.Less(result.x, 0.0);
            Assert.Less(result.y, 0.0);
        }

        [Test]
        public void PushOutWhenCenterIsInside()
        {
            Vec2 result = Collision.PushOut(new Vec2(2.0, 0.3), 0.5, R);
            Assert.AreEqual(2.0, result.x, 1e-4);
            Assert.AreEqual(-0.5, result.y, 1e-4);
        }

        [Test]
        public void PlayerStopsAtAnObstacle()
        {
            var sim = new MatchSim(Data.GameRules, CoverStreet(), 1);
            PlayerState p = sim.AddPlayer(1, 0);
            p.Position = new Vec2(Cover().GetCenter().x, Cover().End.y + 2.0);
            Run(sim, 1, new Vec2(0.0, -1.0), 60);
            Assert.AreEqual(Cover().End.y + p.Radius, p.Position.y, 0.05);
        }

        [Test]
        public void ObstaclesBlockBothTeamsTheSame()
        {
            for (int team = 0; team < 2; team++)
            {
                var sim = new MatchSim(Data.GameRules, CoverStreet(), 1);
                PlayerState p = sim.AddPlayer(1, team);
                p.Position = new Vec2(Cover().GetCenter().x, Cover().Position.y - 2.0);
                Run(sim, 1, new Vec2(0.0, 1.0), 60);
                Assert.AreEqual(Cover().Position.y - p.Radius, p.Position.y, 0.05, $"team {team} held back");
            }
        }

        [Test]
        public void PlayerSlidesAlongAnObstacle()
        {
            var sim = new MatchSim(Data.GameRules, CoverStreet(), 1);
            PlayerState p = sim.AddPlayer(1, 0);
            var start = new Vec2(Cover().Position.x + 0.4, Cover().End.y + 0.5);
            p.Position = start;
            Run(sim, 1, new Vec2(1.0, -1.0), 10);
            Assert.AreEqual(Cover().End.y + p.Radius, p.Position.y, 0.05, "held back");
            Assert.Greater(p.Position.x, start.x + 0.5, "slid sideways");
        }

        [Test]
        public void PlayerCannotLeaveTheStreet()
        {
            var sim = new MatchSim(Data.GameRules, CoverStreet(), 1);
            PlayerState p = sim.AddPlayer(1, 0);
            p.Position = Vec2.Zero;
            Run(sim, 1, new Vec2(1.0, 0.0), 900);
            Assert.AreEqual(Layout.ArenaRadius - p.Radius, p.Position.x, 0.05);
            Run(sim, 1, new Vec2(-1.0, 0.0), 1200);
            Assert.AreEqual(-Layout.ArenaRadius + p.Radius, p.Position.x, 0.05);
            p.Position = Vec2.Zero;
            Run(sim, 1, new Vec2(0.0, 1.0), 900);
            Assert.AreEqual(Layout.ArenaRadius - p.Radius, p.Position.y, 0.05);
        }
    }

    public class StatusEffectTests
    {
        [Test]
        public void ApplyAndExpire()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Stun, 1.0);
            Assert.IsTrue(fx.Has(StatusEffects.Type.Stun));
            fx.Step(0.6);
            Assert.IsTrue(fx.Has(StatusEffects.Type.Stun));
            fx.Step(0.5);
            Assert.IsFalse(fx.Has(StatusEffects.Type.Stun));
        }

        [Test]
        public void SameEffectRestartsWithTheNewDuration()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Stun, 2.0);
            fx.Apply(StatusEffects.Type.Stun, 0.5);
            Assert.AreEqual(0.5, fx.TimeLeft(StatusEffects.Type.Stun), 1e-4);
            fx.Step(1.5);
            fx.Apply(StatusEffects.Type.Stun, 2.0);
            Assert.AreEqual(2.0, fx.TimeLeft(StatusEffects.Type.Stun), 1e-4);
        }

        [Test]
        public void SlowUsesTheNewStrengthAndResetsWhenItEnds()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.3);
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.4);
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.2);
            Assert.AreEqual(0.8, fx.SpeedMultiplier(0.5), 1e-4);
            fx.Step(1.1);
            Assert.AreEqual(1.0, fx.SpeedMultiplier(0.5));
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.1);
            Assert.AreEqual(0.9, fx.SpeedMultiplier(0.5), 1e-4);
        }

        [Test]
        public void HardCcBlocksMovingAndCasting()
        {
            foreach (var type in new[] { StatusEffects.Type.Stun, StatusEffects.Type.Airborne, StatusEffects.Type.Bounce, StatusEffects.Type.Knockout })
            {
                var fx = new StatusEffects();
                fx.Apply(type, 1.0);
                Assert.IsFalse(fx.CanMove(), type.ToString());
                Assert.IsFalse(fx.CanCast());
                Assert.IsTrue(fx.IsHardCc(type));
            }
        }

        [Test]
        public void PolymorphWalksAtHalfSpeedButCannotCast()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Polymorph, 2.0);
            Assert.IsTrue(fx.CanMove());
            Assert.IsFalse(fx.CanCast());
            Assert.AreEqual(0.5, fx.SpeedMultiplier(0.5), 1e-4);
            Assert.IsFalse(fx.IsHardCc(StatusEffects.Type.Polymorph));
        }

        [Test]
        public void SlowAndNoneDoNotStopAnything()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.4);
            fx.Apply(StatusEffects.Type.None, 5.0);
            Assert.IsTrue(fx.CanMove());
            Assert.IsTrue(fx.CanCast());
            Assert.IsFalse(fx.Has(StatusEffects.Type.None));
        }

        [Test]
        public void ClearAndNames()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Stun, 1.0);
            CollectionAssert.Contains(fx.DisplayNameList(), "Stunned");
            CollectionAssert.AreEqual(new[] { "STUN" }, fx.ActiveNames());
            fx.Clear();
            Assert.IsTrue(fx.CanMove());
            Assert.AreEqual(1.0, fx.SpeedMultiplier(0.5));
        }

        [Test]
        public void ANewStatusOverwritesTheCurrentOne()
        {
            var fx = new StatusEffects();
            fx.Apply(StatusEffects.Type.Stun, 2.0);
            fx.Apply(StatusEffects.Type.Slow, 1.0, 0.5);
            Assert.AreEqual(1, fx.ActiveNames().Count);
            Assert.IsTrue(fx.Has(StatusEffects.Type.Slow));
            Assert.IsFalse(fx.Has(StatusEffects.Type.Stun));
            Assert.IsTrue(fx.CanMove());
        }
    }

    public class CcImmunityTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp() => _rules = Rules();

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Layout, 6);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            return sim;
        }

        [Test]
        public void TheShippedImmunityIsOneSecond() => Assert.AreEqual(1.0, _rules.CcImmunity);

        [Test]
        public void ASecondStunRightAfterTheFirstEndsIsIgnored()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 1.0, 0.0);
            Wait(sim, 1.1);
            Assert.IsFalse(sim.Players[2].Effects.Has(StatusEffects.Type.Stun));
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 2.0, 0.0);
            Assert.IsFalse(sim.Players[2].Effects.Has(StatusEffects.Type.Stun), "immune for a second");
        }

        [Test]
        public void ADifferentHardCcIsIgnoredTooAndTheSignalDoesNotFire()
        {
            MatchSim sim = Sim();
            var seen = new List<StatusEffects.Type>();
            sim.EffectApplied += (id, type) => seen.Add(type);
            sim.ApplyEffect(2, StatusEffects.Type.Airborne, 0.5, 0.0);
            Wait(sim, 0.6);
            sim.ApplyEffect(2, StatusEffects.Type.Polymorph, 2.0, 0.0);
            Assert.IsFalse(sim.Players[2].Effects.Has(StatusEffects.Type.Polymorph));
            CollectionAssert.AreEqual(new[] { StatusEffects.Type.Airborne }, seen);
        }

        [Test]
        public void TheImmunityWearsOff()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            Wait(sim, 0.6 + _rules.CcImmunity + 0.1);
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 2.0, 0.0);
            Assert.IsTrue(sim.Players[2].Effects.Has(StatusEffects.Type.Stun));
        }

        [Test]
        public void ASlowIsNeverBlockedByTheImmunity()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            Wait(sim, 0.6);
            sim.ApplyEffect(2, StatusEffects.Type.Slow, 2.0, 0.4);
            Assert.IsTrue(sim.Players[2].Effects.Has(StatusEffects.Type.Slow));
        }

        [Test]
        public void ReapplyingTheSameCcWhileItIsOnStillExtendsIt()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 1.0, 0.0);
            Wait(sim, 0.5);
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 2.0, 0.0);
            Assert.AreEqual(2.0, sim.Players[2].Effects.TimeLeft(StatusEffects.Type.Stun), 0.05);
        }

        [Test]
        public void WithTheImmunitySetToZeroNothingChanges()
        {
            _rules.CcImmunity = 0.0;
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            Wait(sim, 0.6);
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 1.0, 0.0);
            Assert.IsTrue(sim.Players[2].Effects.Has(StatusEffects.Type.Stun));
        }

        [Test]
        public void ARescueClearsTheImmunity()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            Wait(sim, 0.6);
            sim.Kill(2);
            sim.Objectives.Rescue(sim, sim.Players[2], 1);
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 1.0, 0.0);
            Assert.IsTrue(sim.Players[2].Effects.Has(StatusEffects.Type.Stun));
        }

        [Test]
        public void DamageStillLandsWhileTheCcIsIgnored()
        {
            MatchSim sim = Sim();
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            Wait(sim, 0.6);
            sim.Damage(2, 10);
            Assert.AreEqual(_rules.PlayerMaxHp - 10, sim.Players[2].Hp);
        }
    }

    public class MovementTests
    {
        static MatchSim Sim(long seed = 1234) => new MatchSim(Data.GameRules, Layout, seed);

        [Test]
        public void RulesMatchGddCoreNumbers()
        {
            GameRules r = Data.GameRules;
            Assert.AreEqual(30, r.TickRate);
            Assert.AreEqual(DT, r.TickDt(), 1e-5);
            Assert.AreEqual(100, r.PlayerMaxHp);
            Assert.AreEqual(6.4, r.MoveSpeed, 0.01);
            Assert.AreEqual(12.0, r.CaptureSoloTime, 0.01);
        }

        [Test]
        public void PlayersSpawnAtTheirOwnBaseWithFullHp()
        {
            MatchSim sim = Sim();
            PlayerState a = sim.AddPlayer(1, 0);
            PlayerState b = sim.AddPlayer(2, 1);
            Assert.AreEqual(Layout.SpawnCenter(MapLayout.SideOwn), a.Position);
            Assert.AreEqual(Layout.SpawnCenter(MapLayout.SideEnemy), b.Position);
            Assert.AreEqual(100, a.Hp);
            Assert.IsFalse(a.Tagged);
            Assert.AreEqual(0.45, a.Radius, 0.001);
        }

        [Test]
        public void TeammatesSpawnSideBySide()
        {
            MatchSim sim = Sim();
            PlayerState first = sim.AddPlayer(1, 0), second = sim.AddPlayer(2, 0), third = sim.AddPlayer(3, 0);
            Assert.AreEqual(first.Position.y, second.Position.y);
            Assert.AreEqual(3, new[] { first.Position.x, second.Position.x, third.Position.x }.Distinct().Count());
        }

        [Test]
        public void FullStickMovesAtMoveSpeed()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            Vec2 start = p.Position;
            Run(sim, 1, new Vec2(0.0, -1.0), 30);
            Assert.AreEqual(start.y - Data.GameRules.MoveSpeed, p.Position.y, 0.02);
            Assert.AreEqual(start.x, p.Position.x, 1e-4);
        }

        [Test]
        public void DiagonalIsNotFaster()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            Vec2 start = p.Position;
            Run(sim, 1, new Vec2(1.0, -1.0), 30);
            Assert.AreEqual(Data.GameRules.MoveSpeed, p.Position.DistanceTo(start), 0.02);
        }

        [Test]
        public void HalfStickMovesHalfSpeed()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            Vec2 start = p.Position;
            Run(sim, 1, new Vec2(0.0, -0.5), 30);
            Assert.AreEqual(Data.GameRules.MoveSpeed * 0.5, p.Position.DistanceTo(start), 0.02);
        }

        [Test]
        public void NoInputMeansNoMovement()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            Vec2 start = p.Position;
            sim.Step(DT);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0));
            sim.Step(DT);
            Assert.AreEqual(start, p.Position);
        }

        [Test]
        public void TaggedPlayersDoNotMove()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            sim.Kill(1);
            Assert.IsTrue(p.Tagged);
            Vec2 start = p.Position;
            Run(sim, 1, new Vec2(1.0, 0.0), 10);
            Assert.AreEqual(start, p.Position);
        }

        [Test]
        public void TickCountsSteps()
        {
            MatchSim sim = Sim();
            Steps(sim, 7);
            Assert.AreEqual(7, sim.Tick);
        }

        [Test]
        public void FacingFollowsLastMoveDirection()
        {
            MatchSim sim = Sim();
            PlayerState p = sim.AddPlayer(1, 0);
            Assert.AreEqual(new Vec2(0.0, -1.0), p.Facing);
            Run(sim, 1, new Vec2(2.0, 0.0), 1);
            Assert.AreEqual(new Vec2(1.0, 0.0), p.Facing);
            Run(sim, 1, Vec2.Zero, 1);
            Assert.AreEqual(new Vec2(1.0, 0.0), p.Facing);
        }

        [Test]
        public void SameSeedAndInputsGiveIdenticalResults()
        {
            MatchSim a = Sim(99), b = Sim(99);
            foreach (MatchSim sim in new[] { a, b })
            {
                sim.AddPlayer(1, 0);
                sim.AddPlayer(2, 1);
                Run(sim, 1, new Vec2(0.3, -0.9), 45);
                Run(sim, 2, new Vec2(-0.5, 0.5), 45);
            }
            Assert.AreEqual(a.Players[1].Position, b.Players[1].Position);
            Assert.AreEqual(a.Players[2].Position, b.Players[2].Position);
            Assert.AreEqual(a.Rng.Randi(), b.Rng.Randi());
        }

        [Test]
        public void RngIsSeededAndDiffersBetweenSeeds()
        {
            MatchSim a = Sim(1), b = Sim(2), again = Sim(1);
            var seqA = new[] { a.Rng.Randi(), a.Rng.Randi(), a.Rng.Randi() };
            CollectionAssert.AreNotEqual(seqA, new[] { b.Rng.Randi(), b.Rng.Randi(), b.Rng.Randi() });
            CollectionAssert.AreEqual(seqA, new[] { again.Rng.Randi(), again.Rng.Randi(), again.Rng.Randi() });
        }

        [Test]
        public void PlayerInputButtonBits()
        {
            var input = PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnWeapon1 | PlayerInput.BtnWeapon2, 3);
            Assert.IsTrue(input.IsPressed(PlayerInput.BtnWeapon1));
            Assert.IsTrue(input.IsPressed(PlayerInput.BtnWeapon2));
            Assert.IsFalse(input.IsPressed(PlayerInput.BtnAimCancel));
            Assert.AreEqual(3, input.Tick);
        }

        [Test]
        public void UnusedButtonBitsMoveNobody()
        {
            MatchSim sim = new MatchSim(Data.GameRules, Layout, 7);
            PlayerState p = sim.AddPlayer(1, 0);
            Vec2 start = p.Position;
            Tick(sim, 1, Vec2.Zero, (1 << 2) | (1 << 3) | (1 << 4));
            Run(sim, 1, Vec2.Zero, 10);
            Assert.AreEqual(start, p.Position);
        }

        [Test]
        public void DamageReducesHpAndIsIgnoredForTaggedOrUnknownPlayers()
        {
            MatchSim sim = new MatchSim(Data.GameRules, Layout, 7);
            PlayerState p = sim.AddPlayer(1, 0);
            sim.Damage(1, 30);
            Assert.AreEqual(70, p.Hp);
            Assert.IsFalse(p.Tagged);
            sim.Kill(1);
            int events = 0;
            sim.PlayerTagged += (a, b) => events++;
            sim.PlayerDamaged += (a, b) => events++;
            sim.Damage(1, 10);
            sim.Damage(99, 10);
            sim.Damage(1, 0);
            Assert.AreEqual(0, events);
            Assert.AreEqual(0, p.Hp);
        }

        [Test]
        public void AMoveTargetIsWalkedToAndEndsOnArrival()
        {
            MatchSim sim = new MatchSim(Data.GameRules, OpenLayout(), 3);
            PlayerState p = sim.AddPlayer(1, 0);
            p.Position = new Vec2(0.0, 5.0);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0).WithMoveTarget(new Vec2(4.0, 5.0)));
            sim.Step(DT);
            Assert.IsTrue(p.HasMoveTarget);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 1));
            Steps(sim, 30);
            Assert.IsFalse(p.HasMoveTarget, "arrived");
            Assert.AreEqual(4.0, p.Position.x, Data.GameRules.MoveArrivalRadius + 0.01);
        }
    }
}
