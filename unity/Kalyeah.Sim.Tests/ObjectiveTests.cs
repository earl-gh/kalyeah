using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Ports of test_tag.gd and test_rescue_countdown.gd.</summary>
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

        static Rect2 Area(MatchSim sim, int team) => Layout.RescueRect(sim.SideForTeam(team));

        static void InArea(MatchSim sim, PlayerState friend) => friend.Position = Area(sim, friend.Team).GetCenter();

        [Test]
        public void ZeroHpTagsAndSendsYouToTheSidewalkBesideYourRescueArea()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            me.Position = new Vec2(3.0, -10.0);
            var tagged = new List<(int, int)>();
            sim.PlayerTagged += (id, by) => tagged.Add((id, by));
            sim.Damage(1, 500);
            Assert.IsTrue(me.Tagged);
            Assert.AreEqual(0, me.Hp);
            Assert.IsFalse(Layout.InArena(me.Position), "outside the play area");
            Assert.Less(me.Position.x, 0.0);
            Assert.Less(me.Position.y, 0.0);
            Assert.AreEqual(new Vec2(1.0, 0.0), me.Facing, "facing in");
            Rect2 area = Area(sim, 0);
            Assert.That(me.Position.y, Is.InRange(area.Position.y, area.End.y));
            CollectionAssert.AreEqual(new[] { (1, -1) }, tagged);
        }

        [Test]
        public void TaggedOpponentsGoToTheRightSidewalkAtTheBottom()
        {
            MatchSim sim = Sim();
            PlayerState them = sim.AddPlayer(2, 1);
            sim.Damage(2, 500);
            Assert.IsFalse(Layout.InArena(them.Position));
            Assert.Greater(them.Position.x, 0.0);
            Assert.Greater(them.Position.y, 0.0);
            Assert.AreEqual(new Vec2(-1.0, 0.0), them.Facing);
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
                sim.Step(DT);
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
            Assert.AreEqual(4.0, _rules.RescueHoldTime);
            Wait(sim, 3.5);
            Assert.IsTrue(me.Tagged);
            Assert.Greater(sim.RescueElapsed[0], 3.0);
            Wait(sim, 0.7);
            Assert.IsFalse(me.Tagged);
            Assert.AreEqual(_rules.PlayerMaxHp, me.Hp);
            Assert.AreEqual(me.SpawnPosition, me.Position);
            Assert.IsTrue(Layout.InBase(sim.SideForTeam(0), me.Position));
            CollectionAssert.AreEqual(new[] { (1, 2) }, rescued);
        }

        [Test]
        public void LeavingStartsTheCountOver()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState friend = sim.AddPlayer(2, 0);
            sim.Kill(1);
            InArea(sim, friend);
            Wait(sim, 3.0);
            friend.Position = Vec2.Zero;
            Wait(sim, 0.2);
            Assert.AreEqual(-1, sim.RescueRescuer[0]);
            Assert.AreEqual(0.0, sim.RescueElapsed[0]);
            InArea(sim, friend);
            Wait(sim, 3.0);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 1.2);
            Assert.IsFalse(me.Tagged);
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
            InArea(sim, second);
            Wait(sim, 1.5);
            Assert.AreEqual(2, sim.RescueRescuer[0]);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 1.2);
            Assert.IsFalse(me.Tagged);
        }

        [Test]
        public void WhenTheRescuerGoesTheNextOneStartsFromZero()
        {
            MatchSim sim = Sim();
            PlayerState me = sim.AddPlayer(1, 0);
            PlayerState first = sim.AddPlayer(2, 0), second = sim.AddPlayer(3, 0);
            sim.Kill(1);
            InArea(sim, first);
            InArea(sim, second);
            Wait(sim, 3.0);
            first.Position = Vec2.Zero;
            Wait(sim, 0.2);
            Assert.AreEqual(3, sim.RescueRescuer[0]);
            Assert.Less(sim.RescueElapsed[0], 0.5);
            Wait(sim, 3.0);
            Assert.IsTrue(me.Tagged);
            Wait(sim, 1.2);
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
            Wait(sim, 4.2);
            Assert.IsFalse(c.Tagged);
            Assert.IsTrue(a.Tagged && b.Tagged);
            Assert.AreEqual(4, sim.RescueRescuer[0]);
            Wait(sim, 4.2);
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
            Wait(sim, 4.2);
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
            var sim = new MatchSim(Data.GameRules, Layout, 23);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 0);
            sim.AddPlayer(3, 1);
            return sim;
        }

        static void InArea(MatchSim sim, int id) =>
            sim.Players[id].Position = Layout.RescueRect(sim.SideForTeam(sim.Players[id].Team)).GetCenter();

        static GameRules R => Data.GameRules;

        [Test]
        public void TheShippedCountdownIsEightSecondsAndRunsFromTheStart()
        {
            Assert.AreEqual(8.0, R.RescueCooldown);
            MatchSim sim = Sim();
            CollectionAssert.AreEqual(new[] { 8.0, 8.0 }, sim.RescueReadyLeft);
            Wait(sim, 3.0);
            Assert.AreEqual(5.0, sim.RescueReadyLeft[0], DT * 2.0);
            Wait(sim, 5.5);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, sim.RescueReadyLeft);
        }

        [Test]
        public void NobodyCanRescueWhileTheAreaCountsDown()
        {
            MatchSim sim = Sim();
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
            Wait(sim, R.RescueCooldown + 0.1);
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
        public void ANewRoundStartsTheCountdownOver()
        {
            MatchSim sim = Sim();
            Wait(sim, R.RescueCooldown + 0.1);
            sim.Kill(3);
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.PointFreeze, sim.Phase);
            Wait(sim, R.PointFreezeTime + 0.1);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase);
            Assert.Greater(sim.RescueReadyLeft[0], R.RescueCooldown - 0.5);
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

    /// <summary>Port of test_captures.gd, plus overtime from the round clock.</summary>
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
            Assert.AreEqual(12.0, _rules.CaptureSoloTime);
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
            Assert.AreEqual(12.0 / 2.3, BaseTime(3), 0.05);
            Assert.AreEqual(12.0 / 3.6, BaseTime(5), 0.05);
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
        public void BothTeamsFullyTaggedAtOnceScoresForNobody()
        {
            MatchSim sim = Sim();
            foreach (int id in sim.Players.KeyList()) sim.Kill(id);
            Wait(sim, _rules.CaptureSoloTime * 2.0);
            CollectionAssert.IsEmpty(_scored);
        }

        [Test]
        public void ADefenderInTheBaseDoesNotBlockOrResetCapture()
        {
            MatchSim sim = Sim();
            Rush(sim, 0);
            Away(sim, 1);
            Wait(sim, 2.0);
            double progress = sim.CaptureElapsed;
            sim.Players[20].Position = Layout.BaseCenter(MapLayout.SideEnemy) + new Vec2(1.5, 0.0);
            sim.Step(DT);
            Assert.AreEqual(0, sim.CaptureTeam);
            Assert.Greater(sim.CaptureElapsed, progress);
            Wait(sim, BaseTime(2));
            CollectionAssert.AreEqual(new[] { 0 }, _scored);
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
        public void TheRoundClockGoesRegularExtendedOvertimeAndOvertimeShortensTheCapture()
        {
            MatchSim sim = Sim();
            Away(sim, 0);
            Away(sim, 1);
            Assert.AreEqual(MatchSim.ClockStage.Regular, sim.GetClockStage());
            Assert.IsTrue(sim.CountsDown());
            sim.RoundTime = _rules.OvertimeAfter;
            Assert.AreEqual(MatchSim.ClockStage.Extended, sim.GetClockStage());
            sim.RoundTime = _rules.OvertimeAfter + _rules.ExtendedSeconds;
            Assert.AreEqual(MatchSim.ClockStage.Overtime, sim.GetClockStage());
            Assert.IsTrue(sim.InOvertime());
            Assert.AreEqual(_rules.OvertimeCaptureTime, sim.CurrentBaseCaptureTime());
            sim.Players[10].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            Wait(sim, _rules.OvertimeCaptureTime + 0.2);
            CollectionAssert.AreEqual(new[] { 0 }, _scored, "an overtime capture takes the shorter time");
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
