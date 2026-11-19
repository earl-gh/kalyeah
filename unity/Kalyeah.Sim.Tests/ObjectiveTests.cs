using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Ports of test_tag.gd and test_rescue_countdown.gd, on the docs/SPEC.md §5 rules: tagged kids wait in the jail
    /// beside the enemy base, and a rescue there works like a defuse.</summary>
    public class TagAndRescueTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _rules.RescueCooldown = 0.0;
        }

        MatchSim Sim() => new MatchSim(_rules, Layout, 11);

        /// <summary>The jail where `team`'s tagged kids wait and are rescued (beside the enemy base).</summary>
        static Rect2 Area(MatchSim sim, int team) => Layout.RescueRect(sim.JailSideFor(team));

        /// <summary>Puts `friend` in its team's jail; `slot` spreads several rescuers 1 m apart so they never push each other.</summary>
        static void InArea(MatchSim sim, PlayerState friend, int slot = 0) =>
            friend.Position = Area(sim, friend.Team).GetCenter() + new Vec2(0.0, slot * 1.0);

        [Test]
        public void ZeroHpTagsAndSendsYouToTheJailBesideTheEnemyBase()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            me.Position = new Vec2(3.0, -10.0);
            var tagged = new List<(int, int)>();
            sim.PlayerTagged += (id, by) => tagged.Add((id, by));
            sim.Damage(1, 500);
            Assert.IsTrue(me.Tagged);
            Assert.AreEqual(0, me.Hp);
            Assert.AreEqual(-sim.SideForTeam(0), sim.JailSideFor(0), "the jail is at the enemy end");
            Assert.AreEqual(Layout.TaggedPlace(sim.JailSideFor(0), 0), me.Position);
            Assert.AreEqual(Layout.TaggedFacing(sim.JailSideFor(0)), me.Facing, "facing in");
            Rect2 area = Area(sim, 0);
            Assert.That(me.Position.y, Is.InRange(area.Position.y, area.End.y), "beside the jail");
            CollectionAssert.AreEqual(new[] { (1, -1) }, tagged);
        }

        [Test]
        public void TaggedOpponentsGoToTheJailAtTheOtherEnd()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0), them = sim.AddPlayer(2, 1);
            sim.Kill(1);
            sim.Kill(2);
            Assert.AreEqual(Layout.TaggedPlace(sim.JailSideFor(1), 0), them.Position);
            Assert.AreEqual(sim.SideForTeam(0), sim.JailSideFor(1), "team 1 waits beside team 0's base");
            Assert.Less(me.Position.y * them.Position.y, 0.0, "the two jails are at opposite ends");
        }

        [Test]
        public void TaggedKidsStandInARowAlongTheSidewalk()
        {
            MatchSim sim = Sim();
            PlayerState a = sim.AddPlayer(1, 0), b = sim.AddPlayer(2, 0), c = sim.AddPlayer(3, 0);
            sim.Kill(1);
            sim.Kill(2);
            sim.Kill(3);
            Assert.AreEqual(c.Position.Length(), a.Position.Length(), 0.001);
            Assert.AreEqual(c.Position.Length(), b.Position.Length(), 0.001);
            Assert.Less(a.Position.y, b.Position.y);
            Assert.Less(b.Position.y, c.Position.y);
        }

        [Test]
        public void TaggedPlayersCannotMoveActOrBeHit()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            sim.SetLoadout(1, "bato_light", "lata");
            sim.Kill(1);
            Vec2 at = me.Position;
            for (int i = 0; i < 10; i++) Tick(sim, 1, new Vec2(1.0, 0.0), PlayerInput.BtnWeapon1);
            Assert.AreEqual(at, me.Position);
            Assert.IsFalse(me.CanAct());
            Assert.IsFalse(sim.WeaponReady(me, 0));
            Assert.IsFalse(sim.IsTargetable(me));
            Assert.AreEqual(0, sim.Heal(1, 50));
        }

        [Test]
        public void YouGetTaggedEveryTimeNotJustOnce()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            for (int round = 0; round < 3; round++)
            {
                sim.Damage(1, 500);
                Assert.IsTrue(me.Tagged, $"tag {round}");
                InArea(sim, friend);
                Wait(sim, _rules.RescueHoldTime + 0.2);
                Assert.IsFalse(me.Tagged, $"rescue {round}");
                friend.Position = Vec2.Zero;
                Wait(sim, _rules.RescueCooldown + 0.1);
            }
        }

        [Test]
        public void ATeammateWhoStaysTheHoldTimeRescuesYouBackToYourPost()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.SetLoadout(1, "bato_light", "lata");
            me.WeaponCooldowns = new List<double> { 2.0, 0.0 };
            sim.Kill(1);
            var rescued = new List<(int, int)>();
            sim.PlayerRescued += (id, by) => rescued.Add((id, by));
            InArea(sim, friend);
            Assert.AreEqual(6.0, _rules.RescueHoldTime, "docs/SPEC.md §5: a rescue takes 6 s");
            Wait(sim, _rules.RescueHoldTime - 0.5);
            Assert.IsTrue(me.Tagged);
            Assert.Greater(sim.RescueElapsed[0], _rules.RescueHoldTime - 1.0);
            Wait(sim, 0.7);
            Assert.IsFalse(me.Tagged);
            Assert.AreEqual(_rules.PlayerMaxHp, me.Hp);
            Assert.AreEqual(me.SpawnPosition, me.Position);
            Assert.IsTrue(Layout.InBase(sim.SideForTeam(0), me.Position));
            CollectionAssert.AreEqual(new[] { (1, 2) }, rescued);
        }

        [Test]
        public void LeavingBeforeTheCheckpointStartsTheCountOver()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.Kill(1);
            InArea(sim, friend);
            Wait(sim, _rules.RescueCheckpoint - 1.0);
            friend.Position = Vec2.Zero;
            Wait(sim, 0.2);
            Assert.AreEqual(-1, sim.RescueRescuer[0]);
            Assert.AreEqual(0.0, sim.RescueElapsed[0]);
            InArea(sim, friend);
            Wait(sim, _rules.RescueHoldTime - 0.5);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 0.7);
            Assert.IsFalse(me.Tagged);
        }

        [Test]
        public void ACancelAfterTheCheckpointKeepsTheLine()
        {
            Assert.AreEqual(3.0, _rules.RescueCheckpoint, "docs/SPEC.md §5: the halfway line is at 3 s");
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.Kill(1);
            InArea(sim, friend);
            Wait(sim, _rules.RescueCheckpoint + 1.0);
            friend.Position = Vec2.Zero;
            sim.Step(DT);
            Assert.AreEqual(-1, sim.RescueRescuer[0]);
            Assert.AreEqual(_rules.RescueCheckpoint, sim.RescueElapsed[0], 1e-9, "back to the line, not to zero");
            InArea(sim, friend);
            Wait(sim, _rules.RescueHoldTime - _rules.RescueCheckpoint + 0.2);
            Assert.IsFalse(me.Tagged, "only the second half was left");
        }

        [Test]
        public void MovingCastingOrAStunCancelsTheRescue()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.SetLoadout(2, "papel_shield", "lata");
            sim.Kill(1);

            InArea(sim, friend);
            Wait(sim, 1.0);
            Run(sim, 2, new Vec2(1.0, 0.0), 2);
            Assert.AreEqual(0.0, sim.RescueElapsed[0], DT * 2.0, "stepping off the spot starts over");
            Run(sim, 2, Vec2.Zero, 1);

            Wait(sim, 1.0);
            Assert.AreEqual(2, sim.RescueRescuer[0], "standing still again starts a new rescue");
            Cast(sim, 2, 0, new Vec2(0.0, -1.0));
            Assert.AreEqual(0.0, sim.RescueElapsed[0], DT * 2.0, "casting starts over");

            Wait(sim, 1.0);
            sim.ApplyEffect(2, StatusEffects.Type.Stun, 0.5, 0.0);
            sim.Step(DT);
            Assert.AreEqual(-1, sim.RescueRescuer[0], "a stun stops it");
            Assert.AreEqual(0.0, sim.RescueElapsed[0]);
            Assert.IsTrue(me.Tagged);
        }

        [Test]
        public void OnlyOneTeammateRescuesAtATime()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState first = sim.AddPlayer(2, 0), second = sim.AddPlayer(3, 0);
            sim.Kill(1);
            InArea(sim, first);
            Wait(sim, 1.5);
            InArea(sim, second, 1);
            Wait(sim, _rules.RescueHoldTime - 2.0);
            Assert.AreEqual(2, sim.RescueRescuer[0]);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 0.7);
            Assert.IsFalse(me.Tagged);
        }

        [Test]
        public void WhenTheRescuerGoesTheNextOneStartsFromTheCheckpoint()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState first = sim.AddPlayer(2, 0), second = sim.AddPlayer(3, 0);
            sim.Kill(1);
            InArea(sim, first);
            InArea(sim, second, 1);
            Wait(sim, _rules.RescueCheckpoint + 0.5);
            first.Position = Vec2.Zero;
            Wait(sim, 0.2);
            Assert.AreEqual(3, sim.RescueRescuer[0]);
            Assert.AreEqual(_rules.RescueCheckpoint + 0.2, sim.RescueElapsed[0], 0.1, "the team keeps the line");
            Wait(sim, _rules.RescueHoldTime - _rules.RescueCheckpoint - 0.5);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 0.6);
            Assert.IsFalse(me.Tagged);
        }

        [Test]
        public void ARescuerWhoIsTaggedLosesTheRescue()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.Kill(1);
            InArea(sim, friend);
            Wait(sim, 3.0);
            sim.Kill(2);
            Wait(sim, 0.2);
            Assert.AreEqual(-1, sim.RescueRescuer[0]);
            Assert.IsTrue(me.Tagged && friend.Tagged);
        }

        [Test]
        public void TheFirstTaggedIsTheFirstRescued()
        {
            MatchSim sim = Sim();
            PlayerState a = sim.AddPlayer(1, 0), b = sim.AddPlayer(2, 0), c = sim.AddPlayer(3, 0), friend = sim.AddPlayer(4, 0);
            sim.Kill(3);
            sim.Kill(1);
            sim.Kill(2);
            InArea(sim, friend);
            Wait(sim, _rules.RescueHoldTime + 0.2);
            Assert.IsFalse(c.Tagged);
            Assert.IsTrue(a.Tagged && b.Tagged);
            Assert.AreEqual(4, sim.RescueRescuer[0]);
            Wait(sim, _rules.RescueHoldTime + 0.2);
            Assert.IsFalse(a.Tagged);
            Assert.IsTrue(b.Tagged);
        }

        [Test]
        public void AnEnemyInTheRescueAreaRescuesNobody()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState them = sim.AddPlayer(2, 1);
            sim.AddPlayer(3, 0);
            sim.Kill(1);
            them.Position = Area(sim, 0).GetCenter();
            Wait(sim, 6.0);
            Assert.IsTrue(me.Tagged);
        }

        [Test]
        public void WithTheToggleOnOneRescueFreesEveryoneWaiting()
        {
            _rules.RescueFreesAll = true;
            var sim = new MatchSim(_rules, Layout, 3);
            PlayerState a = sim.AddPlayer(1, 0), b = sim.AddPlayer(2, 0), friend = sim.AddPlayer(3, 0);
            sim.Kill(1);
            sim.Kill(2);
            InArea(sim, friend);
            Wait(sim, _rules.RescueHoldTime + 0.2);
            Assert.IsFalse(a.Tagged);
            Assert.IsFalse(b.Tagged);
        }

        [Test]
        public void AHoldTimeOfZeroIsATouchRescue()
        {
            _rules.RescueHoldTime = 0.0;
            var sim = new MatchSim(_rules, Layout, 3);
            PlayerState me = sim.AddPlayer(1, 0), friend = sim.AddPlayer(2, 0);
            sim.Kill(1);
            InArea(sim, friend);
            sim.Step(DT);
            Assert.IsFalse(me.Tagged);
        }

        [Test]
        public void TaggedTrainingDummiesWalkOutOnTheirOwn()
        {
            MatchSim sim = Sim();
            PlayerState dummy = sim.AddDummy(5, 1, new Vec2(2.0, 3.0), DummyBrain.Standing());
            sim.Kill(5);
            Wait(sim, _rules.DummyReleaseTime - 0.2);
            Assert.IsTrue(dummy.Tagged);
            var rescued = new List<(int, int)>();
            sim.PlayerRescued += (id, by) => rescued.Add((id, by));
            Wait(sim, 0.4);
            Assert.IsFalse(dummy.Tagged);
            Assert.AreEqual(new Vec2(2.0, 3.0), dummy.Position);
            CollectionAssert.AreEqual(new[] { (5, MatchSim.ReleasedAlone) }, rescued);
        }

        [Test]
        public void PlayersNeverWalkOutOnTheirOwn()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            sim.AddPlayer(3, 0);
            sim.Kill(1);
            Wait(sim, _rules.DummyReleaseTime + 1.0);
            Assert.IsTrue(me.Tagged);
        }

        [Test]
        public void WeaponsCanBeSwappedOnlyWhileTagged()
        {
            MatchSim sim = Sim();
            sim.AddPlayer(1, 0);
            sim.SetLoadout(1, "bato_light", "lata");
            Assert.IsFalse(sim.SwapLoadout(1, "jacks", "bola"));
            sim.Kill(1);
            Assert.IsTrue(sim.SwapLoadout(1, "jacks", "bola"));
            CollectionAssert.AreEqual(new[] { "jacks", "bola" }, sim.Players[1].Weapons);
        }

        [Test]
        public void TaggingTheTaggerFreesItsCatchTenSecondsLaterBehindAShield()
        {
            Assert.AreEqual(10.0, _rules.RevengeReleaseSeconds, "docs/SPEC.md §5");
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0), mate = sim.AddPlayer(2, 0);
            PlayerState tagger = sim.AddPlayer(3, 1);
            sim.AddPlayer(4, 1);
            var rescued = new List<(int, int)>();
            sim.PlayerRescued += (id, by) => rescued.Add((id, by));
            sim.Damage(1, 500, 3);
            Assert.AreEqual(3, me.TaggedBy);
            sim.Damage(3, 500, 2);
            Wait(sim, _rules.RevengeReleaseSeconds - 0.5);
            Assert.IsTrue(me.Tagged, "not yet");
            Wait(sim, 0.7);
            Assert.IsFalse(me.Tagged);
            Assert.AreEqual(me.SpawnPosition, me.Position, "back at its own base");
            CollectionAssert.AreEqual(new[] { (1, MatchSim.ReleasedByRevenge) }, rescued);
            sim.Damage(1, 30, 4);
            Assert.AreEqual(_rules.PlayerMaxHp, me.Hp, "the shield blocks all damage");
            Wait(sim, _rules.RevengeShieldSeconds);
            sim.Damage(1, 30, 4);
            Assert.AreEqual(_rules.PlayerMaxHp - 30, me.Hp, "the shield is gone after its time");
            Assert.IsTrue(tagger.Tagged && !mate.Tagged);
        }

        [Test]
        public void ARescueBeforeTheRevengeEndsItAndOnlyTheTaggersCatchIsFreed()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0), other = sim.AddPlayer(2, 0), friend = sim.AddPlayer(5, 0);
            sim.AddPlayer(3, 1);
            sim.AddPlayer(4, 1);
            sim.Damage(1, 500, 3);
            sim.Damage(2, 500, 4);
            sim.Damage(3, 500, 5);
            Assert.Greater(me.RevengeLeft, 0.0);
            Assert.AreEqual(0.0, other.RevengeLeft, "tagged by someone still free");
            InArea(sim, friend);
            Wait(sim, _rules.RescueHoldTime + 0.2);
            Assert.IsFalse(me.Tagged, "rescued first");
            Assert.AreEqual(0.0, me.RevengeLeft);
            friend.Position = Vec2.Zero;  // this fixture has no rescue cooldown: leave so only a revenge could free `other`
            Wait(sim, _rules.RevengeReleaseSeconds);
            Assert.IsTrue(other.Tagged);
        }

        [Test]
        public void AFreeKidWalkingToTheEdgeStopsInTheStreet()
        {
            MatchSim sim = Sim();
            PlayerState kid = sim.AddPlayer(1, 0);
            PlayerState enemy = sim.AddPlayer(2, 1);
            sim.Kill(1);
            Vec2 spot = kid.Position;
            enemy.Position = Area(sim, 0).GetCenter();
            Run(sim, 2, new Vec2(-1.0, 0.0), 60);
            Assert.IsTrue(Layout.InArena(enemy.Position));
            Assert.AreEqual(spot, kid.Position);
        }
    }

    public class RescueCountdownTests
    {
        static MatchSim Sim()
        {
            var sim = new MatchSim(Kit.Rules(), Layout, 23);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 0);
            sim.AddPlayer(3, 1);
            return sim;
        }

        static void InArea(MatchSim sim, int id) =>
            sim.Players[id].Position = Layout.RescueRect(sim.JailSideFor(sim.Players[id].Team)).GetCenter();

        static GameRules R => Kit.Rules();

        [Test]
        public void TheShippedCooldownIsFifteenSecondsAndTheJailStartsReady()
        {
            Assert.AreEqual(15.0, R.RescueCooldown, "docs/SPEC.md §5");
            MatchSim sim = Sim();
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, sim.RescueReadyLeft);
        }

        [Test]
        public void NobodyCanRescueWhileTheJailCountsDown()
        {
            MatchSim sim = Sim();
            sim.Kill(1);
            InArea(sim, 2);
            Wait(sim, R.RescueHoldTime + 0.1);
            Assert.IsFalse(sim.Players[1].Tagged);
            sim.Kill(1);
            InArea(sim, 2);
            Wait(sim, R.RescueCooldown - 0.5);
            Assert.IsTrue(sim.Players[1].Tagged);
            Assert.AreEqual(-1, sim.RescueRescuer[0]);
            Assert.AreEqual(0.0, sim.RescueElapsed[0]);
            Wait(sim, 0.5 + R.RescueHoldTime + 0.2);
            Assert.IsFalse(sim.Players[1].Tagged);
        }

        [Test]
        public void ARescueStartsTheCountdownAgain()
        {
            MatchSim sim = Sim();
            sim.Kill(1);
            InArea(sim, 2);
            Wait(sim, R.RescueHoldTime + 0.1);
            Assert.IsFalse(sim.Players[1].Tagged);
            Assert.Greater(sim.RescueReadyLeft[0], R.RescueCooldown - 0.5);
            Assert.AreEqual(0.0, sim.RescueReadyLeft[1]);
            sim.Kill(1);
            InArea(sim, 2);
            Wait(sim, R.RescueHoldTime + 0.5);
            Assert.IsTrue(sim.Players[1].Tagged);
        }

        [Test]
        public void ANewRoundMakesTheJailReadyAgain()
        {
            MatchSim sim = Sim();
            sim.Kill(1);
            InArea(sim, 2);
            Wait(sim, R.RescueHoldTime + 0.1);
            Assert.Greater(sim.RescueReadyLeft[0], 0.0, "cooling down after the rescue");
            sim.Kill(3);
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.PointFreeze, sim.Phase);
            Wait(sim, R.PointFreezeTime + 0.1);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, sim.RescueReadyLeft);
        }

        [Test]
        public void ATaggedPlayerIsNeverFreedByWaiting()
        {
            MatchSim sim = Sim();
            sim.Kill(1);
            Wait(sim, 90.0);
            Assert.IsTrue(sim.Players[1].Tagged);
        }
    }

    /// <summary>Port of test_captures.gd on the docs/SPEC.md §5 rules: 8 s alone, +50% per extra kid, a defender pauses it,
    /// the bar drains at half speed, no round clock.</summary>
    public class CaptureTests
    {
        GameRules _rules;
        List<int> _scored;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _scored = new List<int>();
        }

        MatchSim Sim(int size = 2)
        {
            var sim = new MatchSim(_rules, Layout, 5);
            for (int slot = 0; slot < size; slot++)
            {
                sim.AddPlayer(10 + slot, 0);
                sim.AddPlayer(20 + slot, 1);
            }
            sim.PointScored += (team, how) => _scored.Add(team);
            return sim;
        }

        double BaseTime(int n) => _rules.CaptureSoloTime / (1.0 + (n - 1) * _rules.CaptureAllyBonus);

        static void Rush(MatchSim sim, int team)
        {
            Vec2 centre = Layout.BaseCenter(-sim.SideForTeam(team));
            int index = 0;
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team) pair.Value.Position = centre + new Vec2(index++ - 0.5, 0.0);
        }

        static void Away(MatchSim sim, int team)
        {
            int index = 0;
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team) pair.Value.Position = new Vec2(index++ * 2.0 - 1.0, 0.0);
        }

        [Test]
        public void ABaseCaptureScoresAfterTheTimeForThePlayersInTheBase()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Rush(sim, 0);
            Wait(sim, BaseTime(2) - 0.5);
            CollectionAssert.IsEmpty(_scored);
            Wait(sim, 1.0);
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
            Assert.AreEqual(1, sim.Score.Points[0]);
        }

        [Test]
        public void OnePlayerAloneTakesTheSoloTime()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Away(sim, 0);
            sim.Players[10].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            Assert.AreEqual(8.0, _rules.CaptureSoloTime);
            Wait(sim, _rules.CaptureSoloTime - 0.5);
            CollectionAssert.IsEmpty(_scored);
            Wait(sim, 1.0);
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
        }

        [Test]
        public void EveryTeammateInTheBaseMakesTheCaptureFaster()
        {
            Assert.Greater(BaseTime(1), BaseTime(2));
            Assert.Greater(BaseTime(2), BaseTime(3));
            Assert.Greater(BaseTime(3), BaseTime(5));
            Assert.AreEqual(0.5, _rules.CaptureAllyBonus, "each extra kid adds 50%");
            Assert.AreEqual(8.0 / 2.0, BaseTime(3), 0.05);
            Assert.AreEqual(8.0 / 3.0, BaseTime(5), 0.05);
        }

        [Test]
        public void ATeammateLeavingSlowsTheCountWithoutLosingTheProgress()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Rush(sim, 0);
            Wait(sim, BaseTime(2) / 2.0);
            double half = sim.CaptureElapsed;
            Assert.AreEqual(0.5, half / sim.CaptureNeeded, 0.05);
            sim.Players[11].Position = Vec2.Zero;
            sim.Step(DT);
            Assert.AreEqual(1.0, sim.CaptureRate, 0.001);
            Assert.Greater(sim.CaptureElapsed, half);
            Wait(sim, _rules.CaptureSoloTime * 0.5 + 0.5);
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
        }

        [Test]
        public void TheTimeLeftFollowsTheSpeedOfTheCount()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Rush(sim, 0);
            sim.Step(DT);
            Assert.AreEqual(BaseTime(2) - DT, sim.CaptureTimeLeft(), 0.05);
        }

        [Test]
        public void TeamOneScoresALoneBaseCaptureJustTheSame()
        {
            MatchSim sim = Sim();
            Away(sim, 0);
            Rush(sim, 1);
            Wait(sim, BaseTime(2) + 0.5);
            CollectionAssert.AreEqual(new[] { 1 }, _scored);
        }

        [Test]
        public void BothTeamsInEachOthersBaseAtOnceScoresForNobody()
        {
            MatchSim sim = Sim();
            Rush(sim, 0);
            Rush(sim, 1);
            Wait(sim, _rules.CaptureSoloTime);
            CollectionAssert.IsEmpty(_scored);
            Assert.AreEqual(-1, sim.CaptureTeam);
            Away(sim, 1);
            Wait(sim, BaseTime(2) + 0.5);
            CollectionAssert.AreEqual(new[] { 0 }, _scored, "the tie ends when one team leaves");
        }

        [Test]
        public void BothTeamsFullyTaggedAtOnceReplaysTheRound()
        {
            MatchSim sim = Sim();
            foreach (int id in sim.Players.KeyList()) sim.Kill(id);
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.PointFreeze, sim.Phase, "the round ends");
            Wait(sim, _rules.PointFreezeTime + 0.1);
            CollectionAssert.IsEmpty(_scored, "with no point");
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase);
            foreach (var pair in sim.Players) Assert.IsFalse(pair.Value.Tagged, "everyone is back for the replay");
        }

        [Test]
        public void ADefenderInItsBasePausesTheCapture()
        {
            MatchSim sim = Sim();
            Rush(sim, 0);
            Away(sim, 1);
            Wait(sim, 2.0);
            double progress = sim.CaptureElapsed;
            sim.Players[20].Position = Layout.BaseCenter(MapLayout.SideEnemy) + new Vec2(1.5, 0.0);
            Wait(sim, BaseTime(2));
            Assert.AreEqual(0, sim.CaptureTeam);
            Assert.AreEqual(progress, sim.CaptureElapsed, 1e-9, "no progress and no drain while it stays");
            CollectionAssert.IsEmpty(_scored);
            sim.Players[20].Position = Vec2.Zero;
            Wait(sim, BaseTime(2));
            CollectionAssert.AreEqual(new[] { 0 }, _scored, "the capture resumes where it stopped");
        }

        [Test]
        public void WithNobodyInsideTheBarDrainsAtHalfTheFillSpeed()
        {
            Assert.AreEqual(0.5, _rules.CaptureDrainShare);
            MatchSim sim = Sim();
            Away(sim, 1);
            Away(sim, 0);
            sim.Players[10].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            Wait(sim, 4.0);
            double filled = sim.CaptureElapsed;
            sim.Players[10].Position = Vec2.Zero;
            Wait(sim, 2.0);
            Assert.AreEqual(0, sim.CaptureTeam, "the progress stays");
            Assert.AreEqual(filled - 1.0, sim.CaptureElapsed, DT * 2.0, "2 s outside drain 1 s of fill");
            Wait(sim, filled * 2.0);
            Assert.AreEqual(-1, sim.CaptureTeam, "empty");
            Assert.AreEqual(0.0, sim.CaptureElapsed);
        }

        [Test]
        public void AStunnedCapturerDoesNotCapture()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Away(sim, 0);
            sim.Players[10].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            Wait(sim, 2.0);
            double filled = sim.CaptureElapsed;
            sim.ApplyEffect(10, StatusEffects.Type.Stun, 1.0, 0.0);
            sim.Step(DT);
            Assert.Less(sim.CaptureElapsed, filled, "a stun stops it and the bar drains");
        }

        [Test]
        public void AllEnemiesTaggedScoresImmediately()
        {
            MatchSim sim = Sim();
            sim.Kill(20);
            sim.Kill(21);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
            Assert.AreEqual(MatchSim.CaptureKind.None, sim.CurrentCaptureKind);
            Assert.AreEqual(new Vec2I(0, (int)MatchSim.CaptureKind.AllTagged), sim.PointLog[0]);
        }

        [Test]
        public void ATaggedTeammateDoesNotStopTheFreeOneCapturingAlone()
        {
            MatchSim sim = Sim();
            Away(sim, 1);
            Rush(sim, 0);
            sim.Kill(11);
            Wait(sim, _rules.CaptureSoloTime - 0.5);
            CollectionAssert.IsEmpty(_scored);
            Wait(sim, 1.0);
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
        }

        [Test]
        public void ThereIsNoRoundClock()
        {
            MatchSim sim = Sim();
            Assert.AreEqual(0.0, _rules.OvertimeAfter, "docs/SPEC.md §5: no round or match timer");
            Assert.IsFalse(sim.CountsDown());
            sim.RoundTime = 3600.0;
            Assert.AreEqual(MatchSim.ClockStage.Regular, sim.GetClockStage());
            Assert.AreEqual(_rules.CaptureSoloTime, sim.CurrentBaseCaptureTime(), "nothing escalates");
        }

        [Test]
        public void TheRoundStartBarrierKeepsKidsInTheirBaseAndStopsCasting()
        {
            _rules.BarrierSeconds = Data.GameRules.BarrierSeconds;
            Assert.AreEqual(5.0, _rules.BarrierSeconds, "docs/SPEC.md §5: a 5 s barrier phase");
            MatchSim sim = Sim(1);
            PlayerState kid = sim.Players[10];
            sim.SetLoadout(10, "bato_light", "lata");
            int home = sim.SideForTeam(0);
            Assert.IsTrue(Layout.InBase(home, kid.Position), "kids start in their base");
            Assert.IsFalse(sim.WeaponReady(kid, 0), "no casting behind the barrier");
            Run(sim, 10, new Vec2(0.0, -home), GMath.Ceili(3.0 / DT));
            Assert.IsTrue(Layout.InBase(home, kid.Position), "walking out is blocked");
            Run(sim, 10, new Vec2(0.0, -home), GMath.Ceili(3.0 / DT));
            Assert.IsFalse(Layout.InBase(home, kid.Position), "the barrier drops after its time");
            Assert.IsTrue(sim.WeaponReady(kid, 0));

            sim.Kill(20);
            sim.Step(DT);
            Wait(sim, _rules.PointFreezeTime + 0.1);
            Assert.AreEqual(_rules.BarrierSeconds, sim.BarrierLeft, 0.2, "every round starts behind the barrier");
        }

        [Test]
        public void FirstToThreeWinsTheMatch()
        {
            MatchSim sim = Sim();
            int won = -1;
            sim.MatchWon += team => won = team;
            for (int point = 0; point < 3; point++)
            {
                sim.Kill(20);
                sim.Kill(21);
                sim.Step(DT);
                if (point < 2)
                {
                    Assert.AreEqual(MatchSim.MatchPhase.PointFreeze, sim.Phase);
                    Wait(sim, _rules.PointFreezeTime + 0.1);
                    Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase);
                    Assert.IsFalse(sim.Players[20].Tagged, "everyone back at the post");
                }
            }
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, sim.Phase);
            Assert.AreEqual(0, won);
            Assert.AreEqual(0, sim.Score.Winner);
        }
    }
}
