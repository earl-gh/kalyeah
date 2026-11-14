using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Street.StreetKit;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>Patintero (port of the Patintero part of tests/unit/test_street_modes.gd).</summary>
    public class PatinteroTests
    {
        static Patintero Lines(MatchSim sim) => (Patintero)sim.Mode;

        static MatchSim Sim3() => NewMatch(PatinteroRules, 3);

        static void TagRunner(MatchSim sim, int runner, int guard = 4)
        {
            Lines(sim).GraceLeft = 0.0;
            sim.Players[runner].Position = sim.Players[guard].Position + new Vec2(PatinteroRules.TagCircleRadius - 0.2, 0.0);
            sim.Step(DT);
        }

        static void Score(MatchSim sim, int runner, double x)
        {
            CrossMiddle(sim, runner);
            sim.Players[runner].Position = new Vec2(x, PatinteroRules.GoalZ - 0.2);
            sim.Step(DT);
            sim.Players[runner].Position = new Vec2(x, PatinteroRules.HomeZ + 0.5);
            sim.Step(DT);
        }

        [Test]
        public void RunnersStartBehindTheHomeLineAndOneGuardTakesEachLine()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            foreach (int id in new[] { 1, 2, 3 }) Assert.IsTrue(lines.AtHome(sim.Players[id].Position), "runners start behind the line");
            Assert.AreEqual(Patintero.Axis.Horizontal, lines.GuardAxis[4]);
            Assert.AreEqual(Patintero.Axis.Horizontal, lines.GuardAxis[5]);
            Assert.AreEqual(Patintero.Axis.Vertical, lines.GuardAxis[6], "the third guard takes the vertical line");
            Assert.AreEqual(new Vec2(0.0, PatinteroRules.LineSpan), sim.Players[4].Position);
            Assert.AreEqual(new Vec2(0.0, -PatinteroRules.LineSpan), sim.Players[5].Position);
            Assert.AreEqual(Vec2.Zero, sim.Players[6].Position, "in the middle, where the vertical line crosses");
        }

        [Test]
        public void AGuardCanOnlyMoveAlongItsOwnLine()
        {
            MatchSim sim = Sim3();
            StandAll(sim);
            Input(sim, 4, new Vec2(0.6, 0.8));
            Input(sim, 6, new Vec2(0.8, 0.6));
            Kit.Steps(sim, 30);
            Assert.AreEqual(PatinteroRules.LineSpan, sim.Players[4].Position.y, 0.0001, "it keeps to its horizontal line");
            Assert.Greater(sim.Players[4].Position.x, 0.5, "but moves along it");
            Assert.AreEqual(0.0, sim.Players[6].Position.x, 0.0001, "it keeps to the vertical line");
            Assert.Greater(sim.Players[6].Position.y, 0.5);
            Input(sim, 6, new Vec2(0.0, 1.0));
            Kit.Steps(sim, 120);
            Assert.AreEqual(PatinteroRules.LineSpan, sim.Players[6].Position.y, 0.01, "and stops at the horizontal line");
            Assert.AreEqual(new Vec2(3.0, PatinteroRules.LineSpan), Lines(sim).MovePoint(sim.Players[4], new Vec2(3.0, 2.0)));
            Assert.AreEqual(new Vec2(0.0, PatinteroRules.LineSpan), Lines(sim).MovePoint(sim.Players[6], new Vec2(3.0, 50.0)));
        }

        [Test]
        public void ARunnerScoresOnlyAfterGettingPastTheLastGuardAndBackToTheBase()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            foreach (int id in new[] { 4, 5, 6 }) sim.Players[id].Position = id != 6 ? new Vec2(11.0, 0.0) : Vec2.Zero;
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.LineSpan - 2.0);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.HomeZ + 0.5);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points, "back to the base after only the first guard: not counted");
            Assert.IsFalse(lines.Crossed.ContainsKey(1));
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.GoalZ - 0.2);
            sim.Step(DT);
            Assert.IsFalse(lines.Crossed.ContainsKey(1), "the vertical guard has not been crossed yet");
            CrossMiddle(sim, 1);
            Assert.IsTrue(lines.MiddleCrossed.ContainsKey(1));
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.GoalZ - 0.2);
            sim.Step(DT);
            Assert.IsTrue(lines.Crossed.ContainsKey(1), "past the third guard");
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points, "half way is no point");
            sim.Players[1].Position = new Vec2(-9.0, -PatinteroRules.LineSpan + 3.0);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.GoalZ - 0.2);
            sim.Step(DT);
            Assert.IsTrue(lines.Crossed.ContainsKey(1), "it still has passed the last guard");
            sim.Players[1].Position = new Vec2(-9.0, PatinteroRules.HomeZ + 0.5);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 1, 0 }, sim.Score.Points, "past the last guard and back at the base: the team point");
            Assert.IsFalse(lines.Crossed.ContainsKey(1));
            Assert.IsFalse(lines.MiddleCrossed.ContainsKey(1), "the next run crosses the middle again");
        }

        [Test]
        public void TheVerticalLineIsCrossedByChangingSidesBetweenTheHorizontalLines()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            StandAll(sim);
            sim.Players[1].Position = new Vec2(-2.0, PatinteroRules.LineSpan + 3.0);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(2.0, PatinteroRules.LineSpan + 3.0);
            sim.Step(DT);
            Assert.IsFalse(lines.MiddleCrossed.ContainsKey(1), "changing sides outside the court is not crossing it");
            sim.Players[1].Position = new Vec2(3.0, 5.0);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(8.0, -5.0);
            sim.Step(DT);
            Assert.IsFalse(lines.MiddleCrossed.ContainsKey(1), "staying on one side is not either");
            sim.Players[1].Position = new Vec2(-0.5, -5.0);
            sim.Step(DT);
            Assert.IsTrue(lines.MiddleCrossed.ContainsKey(1), "from one side to the other between the lines");
            sim.Players[1].Position = sim.Players[4].Position + new Vec2(PatinteroRules.TagCircleRadius - 0.2, 0.0);
            lines.GraceLeft = 0.0;
            sim.Step(DT);
            Assert.IsFalse(lines.MiddleCrossed.ContainsKey(1), "a tagged runner starts over");
        }

        [Test]
        public void TheCourtHasNoLinesOfItsOwnBeyondTheFirstAndLastGuard()
        {
            Assert.Greater(PatinteroRules.HomeZ, PatinteroRules.LineSpan + PatinteroRules.TagCircleRadius - 0.01);
            Assert.Less(PatinteroRules.GoalZ, -PatinteroRules.LineSpan - PatinteroRules.TagCircleRadius + 0.01);
            Assert.Less(PatinteroRules.HomeZ, PatinteroRules.LineSpan + PatinteroRules.TagCircleRadius + 1.0);
        }

        [Test]
        public void ATaggedRunnerStumblesThenWaitsOnASidewalkAndTheRoundGoesOn()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            var casts = new List<(int, string)>();
            sim.WeaponCast += (id, weapon) => casts.Add((id, weapon));
            var tags = new List<(int, int)>();
            lines.Tagged += (guard, runner) => tags.Add((guard, runner));
            StandAll(sim);
            lines.GraceLeft = 0.0;
            sim.Players[1].Position = new Vec2(PatinteroRules.TagCircleRadius + 0.3, PatinteroRules.LineSpan);
            sim.Step(DT);
            Assert.IsEmpty(tags, "outside the circle");
            TagRunner(sim, 1);
            CollectionAssert.AreEqual(new[] { (4, 1) }, tags);
            CollectionAssert.AreEqual(new[] { (4, StreetMode.TagCast) }, casts, "the guard plays its touch");
            Assert.IsTrue(lines.Caught.ContainsKey(1), "it stumbles first");
            Assert.IsFalse(lines.IsResetting(), "one tag does not end the round");
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points);
            Wait(sim, PatinteroRules.CatchSeconds + 0.2);
            Assert.IsTrue(lines.IsJailed(1), "then it waits on a sidewalk");
            int side = lines.Jailed[1];
            Vec2 room = lines.SidewalkRange(sim, sim.Players[1].Radius);
            double across = side * sim.Players[1].Position.x;
            Assert.That(across, Is.InRange(room.x, room.y), "outside the street's edge");
            Assert.AreEqual(0.0, sim.Players[1].Position.y, 0.001, "in the middle of the sidewalk");
            Assert.IsFalse(lines.IsTaggable(sim.Players[1]), "it cannot be tagged again");
            Vec2 from = sim.Players[1].Position;
            Input(sim, 1, new Vec2(0.0, 1.0), PlayerInput.BtnCard2);
            Wait(sim, 0.5);
            Assert.Greater(sim.Players[1].Position.y, from.y + 1.0, "it walks along the sidewalk");
            Input(sim, 1, new Vec2(-(double)side, 1.0).Normalized(), PlayerInput.BtnCard2);
            Wait(sim, 3.0);
            Assert.That(side * sim.Players[1].Position.x, Is.InRange(room.x - 0.001, room.y + 0.001), "the street is shut to it");
            Assert.LessOrEqual(sim.Players[1].Position.y, PatinteroRules.LineSpan + 0.001);
            Assert.IsFalse(lines.IsBusy(1), "no dive from a sidewalk");
            Assert.IsFalse(lines.CanDive(sim.Players[1]));
            Assert.IsFalse(lines.FirstGuardPassed);
        }

        [Test]
        public void TaggedRunnersWaitOnTheLeftOrTheRightSidewalkAtRandomAndSpreadAlongIt()
        {
            var sides = new HashSet<int>();
            for (int seed = 0; seed < 12; seed++)
            {
                var sim = new MatchSim(SlowRules(), Kit.Layout, seed);
                for (int i = 0; i < 3; i++)
                {
                    sim.AddPlayer(1 + i, 0);
                    sim.AddPlayer(4 + i, 1);
                }
                sim.StartMode(StreetMode.Create(PatinteroRules));
                Patintero lines = Lines(sim);
                StandAll(sim);
                TagRunner(sim, 1);
                Wait(sim, PatinteroRules.CatchSeconds + 0.2);
                sides.Add(lines.Jailed[1]);
                TagRunner(sim, 2);
                Wait(sim, PatinteroRules.CatchSeconds + 0.2);
                if (lines.Jailed[1] == lines.Jailed[2])
                    Assert.Greater(Math.Abs(sim.Players[1].Position.y - sim.Players[2].Position.y), 1.0, "two on one sidewalk do not stand on each other");
            }
            Assert.AreEqual(2, sides.Count, "both sidewalks are used");
        }

        [Test]
        public void OneFreeRunnerScoringFreesTheTaggedAndTheRunnersStayRunners()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            StandAll(sim);
            TagRunner(sim, 1);
            Wait(sim, PatinteroRules.CatchSeconds + 0.2);
            Assert.IsTrue(lines.IsJailed(1));
            Score(sim, 2, -2.0);
            CollectionAssert.AreEqual(new[] { 1, 0 }, sim.Score.Points, "one runner is enough for the team point");
            Assert.AreEqual(new Vec2I(0, (int)MatchSim.CaptureKind.None), sim.PointLog[sim.PointLog.Count - 1]);
            Assert.IsTrue(lines.IsResetting());
            Wait(sim, PatinteroRules.ResetSeconds + 0.2);
            Assert.IsFalse(lines.IsJailed(1), "the tagged runner is free again");
            Assert.IsTrue(lines.AtHome(sim.Players[1].Position), "back at the base with the others");
            Assert.AreEqual(0, lines.AttackTeam, "runners that score stay runners");
        }

        [Test]
        public void TaggingAllThreeRunnersIsTheGuardsPointAndTheRolesSwitch()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            var swaps = new List<int>();
            lines.RolesSwapped += who => swaps.Add(who);
            StandAll(sim);
            TagRunner(sim, 1);
            Wait(sim, PatinteroRules.CatchSeconds + 0.2);
            TagRunner(sim, 2);
            Wait(sim, PatinteroRules.CatchSeconds + 0.2);
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points, "two of three is no point");
            Assert.IsTrue(lines.RunnersOut() == 2 && !lines.IsResetting());
            TagRunner(sim, 3);
            CollectionAssert.AreEqual(new[] { 0, 1 }, sim.Score.Points, "all three tagged: the guards' team earns the point");
            Assert.AreEqual(new Vec2I(1, (int)MatchSim.CaptureKind.Touched), sim.PointLog[sim.PointLog.Count - 1]);
            Assert.IsTrue(lines.IsResetting());
            Wait(sim, PatinteroRules.ResetSeconds + 0.2);
            Assert.AreEqual(1, lines.AttackTeam, "the roles switch");
            CollectionAssert.AreEqual(new[] { 1 }, swaps);
            Assert.AreEqual(0, lines.RunnersOut(), "the sidewalks are empty");
            Assert.IsTrue(lines.AtHome(sim.Players[4].Position), "team 1 now starts at the base");
            Assert.AreEqual(PatinteroRules.LineSpan, sim.Players[1].Position.y, "and team 0 stands on the lines");
            StandAll(sim);
            lines.GraceLeft = 0.0;
            Score(sim, 4, 2.0);
            CollectionAssert.AreEqual(new[] { 0, 2 }, sim.Score.Points, "points belong to the team, not the role");
            Wait(sim, PatinteroRules.ResetSeconds + 0.2);
            Assert.AreEqual(1, lines.AttackTeam, "and they stay the runners");
        }

        [Test]
        public void RunnersThatNeverLeaveTheBaseGiveTheGuardsAPointAfterTheStallTime()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            StandAll(sim);
            Assert.AreEqual(20.0, PatinteroRules.StallSeconds);
            Wait(sim, PatinteroRules.StallSeconds - 1.0);
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points, "not yet");
            Wait(sim, 1.2);
            CollectionAssert.AreEqual(new[] { 0, 1 }, sim.Score.Points, "20 s and nobody past the first guard: the guards' point");
            Assert.AreEqual((int)MatchSim.CaptureKind.TooSlow, sim.PointLog[sim.PointLog.Count - 1].Y);
            Wait(sim, PatinteroRules.ResetSeconds + 0.2);
            Assert.AreEqual(1, lines.AttackTeam, "the guards become the runners");
            Assert.AreEqual(PatinteroRules.StallSeconds, lines.StallLeft, 1.0, "with a fresh clock");
        }

        [Test]
        public void ARunnerPastTheFirstGuardStopsTheStallClock()
        {
            MatchSim sim = Sim3();
            StandAll(sim);
            Wait(sim, PatinteroRules.StallSeconds - 3.0);
            sim.Players[1].Position = new Vec2(2.0, PatinteroRules.LineSpan - 1.0);
            sim.Step(DT);
            Wait(sim, PatinteroRules.StallSeconds);
            CollectionAssert.AreEqual(new[] { 0, 0 }, sim.Score.Points, "somebody is playing: no stall point");
            Assert.AreEqual(0, Lines(sim).AttackTeam);
        }

        [Test]
        public void ARunnerDivesAndTheCooldownStartsWhenItIsStandingAgain()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            GameRules rules = sim.Rules;
            StandAll(sim);
            var dives = new List<(int, Vec2, double, double)>();
            lines.Dived += (id, way, time, recover) => dives.Add((id, way, time, recover));
            sim.Players[1].Position = new Vec2(2.0, 14.0);
            Vec2 from = sim.Players[1].Position;
            Input(sim, 1, new Vec2(0.0, -1.0), PlayerInput.BtnCard2);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { (1, new Vec2(0.0, -1.0), PatinteroRules.DiveTime, PatinteroRules.DiveRecover) }, dives);
            Assert.IsTrue(lines.IsDiving(1));
            Assert.IsFalse(lines.CanDive(sim.Players[1]));
            Input(sim, 1, new Vec2(1.0, 0.0));
            Wait(sim, PatinteroRules.DiveTime);
            double distance = from.DistanceTo(sim.Players[1].Position);
            Assert.AreEqual(PatinteroRules.DiveSpeedShare * rules.MoveSpeed * PatinteroRules.DiveTime, distance, 0.8, "a dive covers its short burst");
            Assert.Greater(distance, rules.MoveSpeed * PatinteroRules.DiveTime * 1.8, "much faster than a run");
            Assert.IsFalse(lines.IsDiving(1), "the burst is over ...");
            Assert.IsTrue(lines.IsBusy(1), "... but it is still lying and getting up");
            Assert.AreEqual(0.0, lines.DiveReadyShare(1), "no cooldown yet");
            Vec2 lyingAt = sim.Players[1].Position;
            Wait(sim, PatinteroRules.DiveRecover * 0.6);
            Assert.AreEqual(lyingAt, sim.Players[1].Position, "the stick does not steer the get-up");
            Assert.AreEqual(0.0, lines.DiveReadyShare(1));
            Wait(sim, PatinteroRules.DiveRecover * 0.4 + DT * 2.0);
            Assert.IsFalse(lines.IsBusy(1), "standing again");
            Assert.IsTrue(lines.DiveCool.ContainsKey(1), "now the cooldown starts");
            Assert.AreEqual(PatinteroRules.DiveCooldown, lines.DiveCool[1], DT * 4.0);
            Input(sim, 1, new Vec2(0.0, -1.0), PlayerInput.BtnCard2);
            sim.Step(DT);
            Assert.AreEqual(1, dives.Count, "pressing again while it cools down does nothing");
            Input(sim, 1, Vec2.Zero);
            Wait(sim, PatinteroRules.DiveCooldown * 0.5);
            Assert.That(lines.DiveReadyShare(1), Is.InRange(0.35, 0.65), "the ring fills while it cools");
            Assert.IsFalse(lines.CanDive(sim.Players[1]));
            Wait(sim, PatinteroRules.DiveCooldown * 0.5 + DT * 4.0);
            Assert.IsTrue(lines.CanDive(sim.Players[1]));
            Assert.AreEqual(1.0, lines.DiveReadyShare(1));
            Assert.AreEqual(5.0, PatinteroRules.DiveCooldown, "five seconds");
        }

        [Test]
        public void AGuardDivesOffItsLineThenRunsBackAndCannotMoveOrDiveUntilItIsThere()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            GameRules rules = sim.Rules;
            StandAll(sim);
            Input(sim, 4, new Vec2(0.6, -0.8));
            Wait(sim, 0.4);
            Assert.AreEqual(PatinteroRules.LineSpan, sim.Players[4].Position.y, 0.001, "running only along the line");
            sim.Players[4].Position = new Vec2(0.0, PatinteroRules.LineSpan);
            Input(sim, 4, new Vec2(0.0, -1.0), PlayerInput.BtnCard2);
            sim.Step(DT);
            Input(sim, 4, Vec2.Zero);
            Wait(sim, PatinteroRules.DiveTime);
            double off = Math.Abs(sim.Players[4].Position.y - PatinteroRules.LineSpan);
            Assert.Greater(off, 3.0, $"the dive took it well off its line ({off:F1} m)");
            Assert.IsTrue(lines.IsBusy(4));
            Assert.IsFalse(lines.IsReturning(4), "it is getting up first");
            Wait(sim, PatinteroRules.DiveRecover + DT);
            Assert.IsTrue(lines.IsReturning(4), "then it runs back");
            Assert.AreEqual(0.0, lines.DiveReadyShare(4));
            Assert.IsFalse(lines.CanDive(sim.Players[4]));
            Vec2 here = sim.Players[4].Position;
            Input(sim, 4, new Vec2(1.0, 0.0), PlayerInput.BtnCard2);
            sim.Step(DT);
            Vec2 after = sim.Players[4].Position;
            Vec2 linePoint = lines.NearestOnLine(4, here);
            Assert.Less(after.DistanceTo(linePoint), here.DistanceTo(linePoint), "it runs toward the nearest point of its line");
            Assert.AreEqual(PatinteroRules.DiveReturnSpeedShare * rules.MoveSpeed * DT, here.DistanceTo(after), 0.02, "at its own speed");
            Assert.AreEqual(0.0, lines.DiveCool.GetOrDefault(4, 0.0), "the cooldown has not started: it is not back yet");
            for (int i = 0; i < 120 && lines.IsReturning(4); i++) sim.Step(DT);
            Assert.IsFalse(lines.IsBusy(4), "back on its line");
            Assert.AreEqual(PatinteroRules.LineSpan, sim.Players[4].Position.y, 0.001);
            Assert.AreEqual(PatinteroRules.DiveCooldown, lines.DiveCool[4], DT * 2.0, "the cooldown starts now");
            Assert.IsFalse(lines.CanDive(sim.Players[4]));
            sim.Players[6].Position = Vec2.Zero;
            Input(sim, 6, new Vec2(1.0, 0.0), PlayerInput.BtnCard2);
            sim.Step(DT);
            Input(sim, 6, Vec2.Zero);
            Wait(sim, PatinteroRules.DiveTime + PatinteroRules.DiveRecover + 0.3);
            Assert.Greater(sim.Players[6].Position.x, 1.0, "diving sideways took it off the vertical line");
            for (int i = 0; i < 120 && lines.IsBusy(6); i++) sim.Step(DT);
            Assert.AreEqual(0.0, sim.Players[6].Position.x, 0.001, "and it is back on it");
        }

        [Test]
        public void AfterTheMatchTimeTheLeaderWinsAndATiePlaysOn()
        {
            MatchSim sim = Sim3();
            Patintero lines = Lines(sim);
            var won = new List<int>();
            sim.MatchWon += team => won.Add(team);
            StandAll(sim);
            lines.TimeLeft = DT / 2.0;
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase, "0 : 0 plays on");
            Assert.AreEqual(0.0, sim.ClockLeft());
            foreach (int id in new[] { 4, 5, 6 }) sim.Players[id].Position = new Vec2(11.0, 0.0);
            CrossMiddle(sim, 1);
            sim.Players[1].Position = new Vec2(-10.0, PatinteroRules.GoalZ - 0.5);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(-10.0, PatinteroRules.HomeZ + 0.5);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 0 }, won, "the golden point wins");
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, sim.Phase);
            Assert.AreEqual(new StreetMode.Result { Won = true, Mine = 1, Theirs = 0 }, lines.LocalResult(sim, 1));
            Assert.AreEqual(new StreetMode.Result { Won = false, Mine = 0, Theirs = 1 }, lines.LocalResult(sim, 4));
        }
    }
}
