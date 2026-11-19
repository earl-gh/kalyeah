using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Ports of test_round_pace.gd and test_scoring.gd: 3-stage round clock (Regular, Extended, Overtime),
    /// ClockLeft stage resets, OvertimeCaptureTime clamping, PointLog (AllTagged vs Base), MatchScore
    /// first-to-3/5/7, SetFirstTo validation, Intro/PointFreeze clock pause, and full between-point reset.
    /// </summary>
    public class RoundPaceAndScoreTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp() => _rules = Rules();

        MatchSim OpenSim()
        {
            var sim = new MatchSim(_rules, OpenLayout(), 4);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 0);
            sim.AddPlayer(3, 1);
            sim.AddPlayer(4, 1);
            return sim;
        }

        void ConfigureStageRules()
        {
            _rules.OvertimeAfter = 30.0;
            _rules.ExtendedSeconds = 10.0;
            _rules.OvertimeSeconds = 5.0;
        }

        [Test]
        public void ShippedRulesHaveNoRoundClockAndMatchTheScoring()
        {
            Assert.AreEqual(0.0, _rules.OvertimeAfter, "docs/SPEC.md §5: no round or match timer");
            Assert.AreEqual(8.0, _rules.CaptureSoloTime);
            Assert.AreEqual(3.0, _rules.PointFreezeTime);
            Assert.AreEqual(3, _rules.FirstTo);
            CollectionAssert.AreEqual(new[] { 3, 5, 7 }, _rules.FirstToChoices);
        }

        [Test]
        public void MatchScoreFirstToThreeFiveSevenAndSetFirstToValidation()
        {
            var score = new MatchScore(3);
            Assert.AreEqual(3, score.PointsToWin());
            Assert.AreEqual(MatchScore.Result.Point, score.AwardPoint(0));
            Assert.AreEqual(MatchScore.Result.Point, score.AwardPoint(1));
            Assert.AreEqual(MatchScore.Result.Point, score.AwardPoint(1));
            Assert.AreEqual(MatchScore.Result.Match, score.AwardPoint(1));
            Assert.AreEqual(1, score.Winner);
            Assert.AreEqual(MatchScore.Result.Match, score.AwardPoint(0), "locked after match won");
            CollectionAssert.AreEqual(new[] { 1, 3 }, score.Points);

            Assert.AreEqual(5, new MatchScore(5).PointsToWin());
            var seven = new MatchScore(7);
            Assert.AreEqual(7, seven.PointsToWin());
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(MatchScore.Result.Point, seven.AwardPoint(0));
            Assert.AreEqual(MatchScore.Result.Match, seven.AwardPoint(0));

            MatchSim sim = OpenSim();
            Assert.AreEqual(3, sim.Score.FirstTo);
            sim.SetFirstTo(5);
            Assert.AreEqual(5, sim.Score.FirstTo);
            sim.SetFirstTo(7);
            Assert.AreEqual(7, sim.Score.FirstTo);
            sim.SetFirstTo(4);
            Assert.AreEqual(7, sim.Score.FirstTo, "ignores choices outside [3, 5, 7]");
        }

        [Test]
        public void NoShippedClockButTheThreeStageClockStillWorksWhenConfigured()
        {
            MatchSim shipped = OpenSim();
            Assert.IsFalse(shipped.CountsDown(), "the shipped rules have no clock");
            foreach (double t in new[] { 0.0, 240.0, 3600.0 })
            {
                shipped.RoundTime = t;
                Assert.AreEqual(MatchSim.ClockStage.Regular, shipped.GetClockStage(), $"no stage change at {t}");
            }

            ConfigureStageRules();
            MatchSim sim = OpenSim();
            Assert.AreEqual(0.0, sim.ClockStageStart(MatchSim.ClockStage.Regular));
            Assert.AreEqual(30.0, sim.ClockStageStart(MatchSim.ClockStage.Extended));
            Assert.AreEqual(40.0, sim.ClockStageStart(MatchSim.ClockStage.Overtime));

            (double t, double left, MatchSim.ClockStage stage)[] customCases =
            {
                (0.0, 30.0, MatchSim.ClockStage.Regular),
                (10.0, 20.0, MatchSim.ClockStage.Regular),
                (29.9, 0.1, MatchSim.ClockStage.Regular),
                (30.0, 10.0, MatchSim.ClockStage.Extended),
                (35.0, 5.0, MatchSim.ClockStage.Extended),
                (40.0, 5.0, MatchSim.ClockStage.Overtime),
                (42.0, 3.0, MatchSim.ClockStage.Overtime),
                (45.0, 0.0, MatchSim.ClockStage.Overtime),
                (500.0, 0.0, MatchSim.ClockStage.Overtime),
            };
            foreach (var (t, left, stage) in customCases)
            {
                sim.RoundTime = t;
                Assert.AreEqual(stage, sim.GetClockStage(), $"stage at {t}");
                Assert.AreEqual(left, sim.ClockLeft(), 0.001, $"clock left at {t}");
            }

            MatchSim stepped = OpenSim();
            Wait(stepped, 31.0);
            Assert.AreEqual(MatchSim.ClockStage.Extended, stepped.GetClockStage());
            Wait(stepped, 10.0);
            Assert.AreEqual(MatchSim.ClockStage.Overtime, stepped.GetClockStage());
            stepped.Objectives.EndFreeze(stepped);
            Assert.AreEqual(MatchSim.ClockStage.Regular, stepped.GetClockStage());
            Assert.AreEqual(30.0, stepped.ClockLeft(), 0.001);

            _rules.ExtendedSeconds = 0.0;
            MatchSim noExt = OpenSim();
            noExt.RoundTime = 30.0;
            Assert.AreEqual(MatchSim.ClockStage.Overtime, noExt.GetClockStage());
            Assert.AreEqual(5.0, noExt.ClockLeft(), 0.001);

            _rules.ExtendedSeconds = 10.0;
            _rules.OvertimeSeconds = 0.0;
            MatchSim zeroOt = OpenSim();
            zeroOt.RoundTime = 40.0;
            Assert.AreEqual(MatchSim.ClockStage.Overtime, zeroOt.GetClockStage());
            Assert.AreEqual(0.0, zeroOt.ClockLeft(), 0.001);

            _rules.OvertimeAfter = 0.0;
            MatchSim countUp = OpenSim();
            Assert.IsFalse(countUp.CountsDown());
            countUp.RoundTime = 500.0;
            Assert.AreEqual(MatchSim.ClockStage.Regular, countUp.GetClockStage());
            Assert.IsFalse(countUp.InOvertime());
        }

        [Test]
        public void OvertimeShortensCaptureNeverLengthensItAndExtendedStaysRegularLength()
        {
            _rules.OvertimeAfter = 0.0;
            MatchSim noOt = OpenSim();
            Wait(noOt, 300.0);
            Assert.AreEqual(_rules.CaptureSoloTime, noOt.CaptureNeeded);
            Assert.AreEqual(_rules.CaptureSoloTime, noOt.CurrentBaseCaptureTime());

            _rules.OvertimeAfter = 1.0;
            _rules.ExtendedSeconds = 0.0;
            _rules.OvertimeCaptureTime = 99.0;
            MatchSim clamped = OpenSim();
            Wait(clamped, 2.0);
            Assert.AreEqual(_rules.CaptureSoloTime, clamped.CaptureNeeded, "overtime never lengthens capture");

            ConfigureStageRules();
            _rules.OvertimeCaptureTime = 3.0;
            MatchSim staged = OpenSim();
            staged.RoundTime = 35.0;
            Assert.IsFalse(staged.InOvertime());
            Assert.AreEqual(_rules.CaptureSoloTime, staged.CurrentBaseCaptureTime(), "Extended keeps solo capture time");
            staged.RoundTime = 40.0;
            Assert.IsTrue(staged.InOvertime());
            Assert.AreEqual(3.0, staged.CurrentBaseCaptureTime());

            _rules.OvertimeAfter = 5.0;
            _rules.ExtendedSeconds = 0.0;
            _rules.OvertimeCaptureTime = 2.0;
            MatchSim quick = OpenSim();
            var scored = new List<int>();
            quick.PointScored += (team, _) => scored.Add(team);
            Wait(quick, 6.0);
            quick.Players[1].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            quick.Players[3].Position = Vec2.Zero;
            quick.Players[4].Position = new Vec2(2.0, 0.0);
            Wait(quick, 1.5);
            CollectionAssert.IsEmpty(scored);
            Wait(quick, 1.0);
            CollectionAssert.AreEqual(new[] { 0 }, scored);
        }

        [Test]
        public void PointLogIntroFreezeResetFirstToFiveAndDummyNonScoring()
        {
            MatchSim sim = OpenSim();
            sim.Kill(3);
            sim.Kill(4);
            Wait(sim, 0.2);
            CollectionAssert.AreEqual(new[] { new Vec2I(0, (int)MatchSim.CaptureKind.AllTagged) }, sim.PointLog);
            sim.Objectives.EndFreeze(sim);

            Vec2 ownBase = Layout.BaseCenter(MapLayout.SideOwn);
            sim.Players[3].Position = ownBase;
            sim.Players[4].Position = ownBase + new Vec2(1.0, 0.0);
            sim.Players[1].Position = Vec2.Zero;
            sim.Players[2].Position = new Vec2(2.0, 0.0);
            Wait(sim, _rules.CaptureSoloTime + 0.5);
            Assert.AreEqual(2, sim.PointLog.Count);
            Assert.AreEqual(new Vec2I(1, (int)MatchSim.CaptureKind.Base), sim.PointLog[1]);

            // Intro & PointFreeze pause clocks and movement, and EndFreeze resets full round state
            var resetSim = new MatchSim(_rules, Layout, 3);
            PlayerState me = resetSim.AddPlayer(1, 0);
            PlayerState them = resetSim.AddPlayer(2, 1);
            resetSim.SetLoadout(1, "bato_light", "lata");
            resetSim.BeginIntro();
            bool introDone = false;
            resetSim.IntroFinished += () => introDone = true;
            Vec2 introStart = me.Position;
            Run(resetSim, 1, new Vec2(1.0, 0.0), GMath.Ceili((_rules.IntroTime - 0.2) / DT));
            Assert.AreEqual(introStart, me.Position, "nobody moves during Intro");
            Assert.AreEqual(0.0, resetSim.MatchTime);
            Assert.AreEqual(0.0, resetSim.RoundTime);
            Wait(resetSim, 0.4);
            Assert.IsTrue(introDone);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, resetSim.Phase);
            Wait(resetSim, 2.0);
            Assert.That(resetSim.MatchTime, Is.InRange(2.0, 2.5));

            me.WeaponCooldowns = new List<double> { 3.0, 9.0 };
            resetSim.ApplyEffect(2, StatusEffects.Type.Slow, 10.0, 0.4);
            resetSim.Kill(2);
            resetSim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.PointFreeze, resetSim.Phase);
            double frozenMatchTime = resetSim.MatchTime;
            Vec2 frozenAt = me.Position;
            bool pointResetFired = false;
            resetSim.PointReset += () => pointResetFired = true;
            Run(resetSim, 1, new Vec2(1.0, 0.0), GMath.Ceili(1.5 / DT));
            Assert.AreEqual(frozenAt, me.Position, "nobody moves during PointFreeze");
            Assert.AreEqual(frozenMatchTime, resetSim.MatchTime, 1e-6, "MatchTime pauses during PointFreeze");
            resetSim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, resetSim.Tick));
            Wait(resetSim, 2.0);
            Assert.IsTrue(pointResetFired);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, resetSim.Phase);
            Assert.AreEqual(me.SpawnPosition, me.Position);
            Assert.AreEqual(them.SpawnPosition, them.Position);
            Assert.IsFalse(them.Tagged);
            Assert.AreEqual(_rules.PlayerMaxHp, them.Hp);
            Assert.IsFalse(them.Effects.Has(StatusEffects.Type.Slow));
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, me.WeaponCooldowns);

            // First-to-5 plays 5 points without switching ends and stops movement on MatchOver
            var fiveSim = new MatchSim(_rules, Layout, 3);
            fiveSim.SetFirstTo(5);
            PlayerState runner = fiveSim.AddPlayer(1, 0);
            Vec2 initialSpawn = runner.SpawnPosition;
            int winner = -1;
            fiveSim.MatchWon += t => winner = t;
            for (int i = 0; i < 4; i++)
            {
                runner.Position = Layout.BaseCenter(-fiveSim.SideForTeam(0));
                Wait(fiveSim, _rules.CaptureSoloTime + 0.2);
                Wait(fiveSim, _rules.PointFreezeTime + 0.2);
            }
            Assert.AreEqual(MatchSim.MatchPhase.Playing, fiveSim.Phase);
            Assert.AreEqual(initialSpawn, runner.SpawnPosition, "ends never switch");
            runner.Position = Layout.BaseCenter(-fiveSim.SideForTeam(0));
            Wait(fiveSim, _rules.CaptureSoloTime + 0.2);
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, fiveSim.Phase);
            Assert.AreEqual(0, winner);
            Vec2 overPos = runner.Position;
            Run(fiveSim, 1, new Vec2(1.0, 0.0), 60);
            Assert.AreEqual(overPos, runner.Position, "nothing moves after MatchOver");

            // Training dummies never score when player is tagged
            var practiceSim = new MatchSim(_rules, Layout, 3);
            practiceSim.AddPlayer(1, 0);
            practiceSim.AddDummy(2, 1, new Vec2(0.0, -10.0), DummyBrain.Standing());
            practiceSim.Kill(1);
            Wait(practiceSim, _rules.CaptureSoloTime + 0.5);
            CollectionAssert.AreEqual(new[] { 0, 0 }, practiceSim.Score.Points);
        }
    }
}
