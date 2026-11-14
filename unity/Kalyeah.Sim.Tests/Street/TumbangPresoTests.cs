using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Street.StreetKit;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>Tumbang Preso (port of the Tumbang Preso part of tests/unit/test_street_modes.gd and of tests/unit/test_tumbang_roll.gd).</summary>
    public class TumbangPresoTests
    {
        static readonly Vec2 Up = new Vec2(0.0, -1.0);

        static TumbangPreso Game(MatchSim sim) => (TumbangPreso)sim.Mode;

        [Test]
        public void TumbangHasOneTayaAndEveryoneElseThrowsFromTheThrowingArea()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            Assert.AreEqual(4, game.TayaId);
            foreach (PlayerState state in sim.Players.Values)
            {
                if (state.Id == game.TayaId)
                {
                    Assert.AreEqual(TumbangRules.CanSpot.y - TumbangRules.GuardOffset, state.Position.y, 0.001, "the taya starts just behind the can");
                }
                else
                {
                    Assert.IsTrue(game.AtHome(state.Position), "throwers start in the throwing area");
                    Assert.IsTrue(game.HasSlipper(state.Id));
                }
            }
            Assert.IsTrue(game.CanUp);
            Assert.IsTrue(game.IsFreeForAll());
            Assert.AreEqual(TumbangRules.MatchSeconds, sim.ClockLeft(), "five minutes");
            Assert.IsTrue(sim.CountsDown());
        }

        [Test]
        public void TheTayaRunsOneAndAHalfTimesAsFast()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            sim.Players[1].Position = new Vec2(-9.0, 20.0);
            game.Slippers[1] = new Vec2(30.0, 30.0);  // no slipper in hand: free to run anywhere
            sim.Players[4].Position = new Vec2(9.0, 0.0);
            Vec2 fromThrower = sim.Players[1].Position;
            Vec2 fromTaya = sim.Players[4].Position;
            Input(sim, 1, Up);
            Input(sim, 4, Up);
            Wait(sim, 0.5);
            Assert.AreEqual(TumbangRules.TayaSpeedShare,
                sim.Players[4].Position.DistanceTo(fromTaya) / sim.Players[1].Position.DistanceTo(fromThrower), 0.05);
        }

        [Test]
        public void HoldingTheThrowButtonGrowsTheAimLineAndLettingGoThrows()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            Assert.AreEqual(0.0, game.AimRange(1), "no aim until the button is held");
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard1, 0));
            sim.Step(DT);
            double shortRange = game.AimRange(1);
            Assert.AreEqual(TumbangRules.ThrowMinRange + (TumbangRules.ThrowMaxRange - TumbangRules.ThrowMinRange) * DT / TumbangRules.ThrowChargeTime, shortRange, 0.01);
            Wait(sim, 0.5);
            Assert.Greater(game.AimRange(1), shortRange + 2.0, "the longer you hold, the longer the line");
            Wait(sim, 2.0);
            Assert.AreEqual(TumbangRules.ThrowMaxRange, game.AimRange(1), "and it stops at the longest");
            Assert.AreEqual(game.AimAtCan(sim.Players[1].Position), game.AimDir[1], "it starts aimed at the can");
            var thrown = new List<int>();
            game.SlipperThrown += id => thrown.Add(id);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0));
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 1 }, thrown);
            Assert.IsFalse(game.IsAiming(1));
            Assert.IsFalse(game.HasSlipper(1), "the slipper is in the air");
            Assert.AreEqual("tsinelas_light", sim.Weapons.Projectiles[0].Def.Id, "tsinelas, drawn and animated like Agawang Base's");
        }

        [Test]
        public void TheAimSensitivityIsLowWhenTheLineIsShortAndHighAtFullLength()
        {
            TumbangPreso game = Game(NewMatch(TumbangRules));
            Assert.AreEqual(TumbangRules.AimSensitivityLow, game.SensitivityAt(0.0));
            Assert.AreEqual(TumbangRules.AimSensitivityHigh, game.SensitivityAt(1.0));
            Assert.Greater(game.SensitivityAt(1.0), game.SensitivityAt(0.0) * 2.0);
            Assert.Less(game.SensitivityAt(0.3), game.SensitivityAt(0.7));
        }

        [Test]
        public void AThrowerWithItsSlipperInHandCannotEnterTheFetchArea()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 1);
            sim.Players[1].Position = new Vec2(0.0, 14.0);
            Input(sim, 1, Up);
            Wait(sim, 1.5);
            Assert.AreEqual(TumbangRules.HomeZ, sim.Players[1].Position.y, 0.01, "stopped at the throw line");
            Input(sim, 1, new Vec2(1.0, 0.0));
            Wait(sim, 0.5);
            Assert.Greater(sim.Players[1].Position.x, 0.5, "free to move along the throwing area");
            game.Slippers[1] = new Vec2(0.0, 5.0);  // once it has thrown, the slipper is not in hand: it may go out
            Input(sim, 1, Up);
            Wait(sim, 0.5);
            Assert.Less(sim.Players[1].Position.y, TumbangRules.HomeZ - 1.0, "into the fetch area");
        }

        [Test]
        public void TheTayaStaysInTheFetchArea()
        {
            MatchSim sim = NewMatch(TumbangRules);
            Input(sim, 4, new Vec2(0.0, 1.0));
            Wait(sim, 3.0);
            Assert.Less(sim.Players[4].Position.y, TumbangRules.HomeZ, "never into the throwing area");
        }

        [Test]
        public void ACancelledAimAndAThrowFromTheFetchAreaThrowNothing()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard1, 0));
            sim.Step(DT);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnAimCancel, 0));
            sim.Step(DT);
            Assert.IsTrue(game.HasSlipper(1), "cancelled");
            game.Slippers[2] = new Vec2(0.0, 5.0);  // kid 2 has already thrown and stands out in the fetch area
            sim.Players[2].Position = new Vec2(0.0, 8.0);
            Throw(sim, 2, Up, 0.3);
            Assert.IsFalse(game.Flights.ContainsKey(2), "only from the throwing area");
        }

        [Test]
        public void AHitFlingsTheCanAndTheSlipperApart()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            var cans = new List<List<Vec2>>();
            var slips = new List<List<Vec2>>();
            game.CanFlung += (from, path) => cans.Add(path);
            game.SlipperFlung += (id, from, path) => slips.Add(path);
            ClearStreet(sim, 1, 4);
            sim.Players[4].Position = new Vec2(-9.0, 0.0);
            sim.Players[1].Position = new Vec2(TumbangRules.CanSpot.x, 14.0);
            Throw(sim, 1, Up, TumbangRules.ThrowChargeTime);
            Fly(sim, game, 1);
            Assert.IsFalse(game.CanUp, "the can is down");
            Assert.AreEqual(1, cans.Count);
            Assert.AreEqual(1, slips.Count);
            Vec2 canEnd = cans[0].Last();
            Vec2 slipperEnd = slips[0].Last();
            Assert.AreEqual(canEnd, game.CanPosition);
            Assert.AreEqual(slipperEnd, game.Slippers[1], "the slipper lies where it was flung");
            Assert.Less(canEnd.y, TumbangRules.CanSpot.y - 1.0, "a dead-on hit sends the can on along the throw");
            Assert.Greater(slipperEnd.y, TumbangRules.CanSpot.y, "and the slipper bounces back");
            Assert.Greater(game.CanBusyLeft, 0.0, "neither can be picked up before it lands");
            Assert.Greater(game.SlipperBusy[1], 0.0);
        }

        [Test]
        public void TheAimGuideShowsTheWayTheCanAndTheSlipperWouldGoOnlyWhenTheAimHits()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            Assert.IsNull(game.AimGuide(sim, 1), "no aim, no guide");
            sim.Players[1].Position = new Vec2(TumbangRules.CanSpot.x, 14.0);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Up, PlayerInput.BtnCard1, 0));
            Wait(sim, TumbangRules.ThrowChargeTime);
            TumbangPreso.Guide guide = game.AimGuide(sim, 1);
            Assert.IsNotNull(guide, "the line reaches the can");
            Assert.AreEqual(-1.0, guide.CanDir.y, 0.01, "dead on: the can goes straight on");
            Assert.Greater(guide.SlipperDir.y, 0.0, "the slipper bounces back");
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, new Vec2(0.4, -1.0), PlayerInput.BtnCard1, 0));
            sim.Step(DT);
            Assert.IsNull(game.AimGuide(sim, 1), "aimed wide: nothing to show");
            game.CanUp = false;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Up, PlayerInput.BtnCard1, 0));
            sim.Step(DT);
            Assert.IsNull(game.AimGuide(sim, 1), "nothing standing to hit");
        }

        [Test]
        public void TheRealFlingIsALittleOffTheGuideSoItCannotBeAimedToTheOtherEnd()
        {
            var off = new List<double>();
            for (int seed = 0; seed < 12; seed++)
            {
                var sim = new MatchSim(SlowRules(), Kit.Layout, seed);
                for (int i = 0; i < 4; i++) sim.AddPlayer(1 + i, 0);
                sim.StartMode(StreetMode.Create(TumbangRules));
                TumbangPreso game = Game(sim);
                game.TayaId = 4;
                game.PlaceTeams(sim);
                ClearStreet(sim, 1, 4);
                sim.Players[4].Position = new Vec2(-9.0, 0.0);
                sim.Players[1].Position = new Vec2(TumbangRules.CanSpot.x, 14.0);
                var cans = new List<(Vec2 from, List<Vec2> path)>();
                game.CanFlung += (from, path) => cans.Add((from, path));
                Throw(sim, 1, Up, TumbangRules.ThrowChargeTime);
                Fly(sim, game, 1);
                Vec2 way = cans[0].path[0] - cans[0].from;
                off.Add(RadToDeg(Math.Abs(way.AngleTo(Up))));
                Assert.LessOrEqual(way.Length(), TumbangRules.CanFlingDistance * (1.0 + TumbangRules.FlingDistanceError) + 0.01, "short");
                Assert.Less(game.Slippers[1].y, TumbangRules.HomeZ, "and nowhere near the other end");
            }
            Assert.Greater(off.Max(), 1.0, "sometimes a little off");
            Assert.LessOrEqual(off.Max(), TumbangRules.FlingErrorDegrees + 0.5, "never more than the error");
        }

        [Test]
        public void AHitBouncesOnceMoreWithTheBounceChanceForALongerTrip()
        {
            foreach (double chance in new[] { 0.0, 1.0 })
            {
                StreetModeRules rules = TumbangRules.Duplicate();
                rules.BounceChance = chance;
                MatchSim sim = NewMatch(rules);
                TumbangPreso game = Game(sim);
                var paths = new List<int>();
                game.CanFlung += (from, path) => paths.Add(path.Count);
                ClearStreet(sim, 1, 4);
                sim.Players[4].Position = new Vec2(-9.0, 0.0);
                sim.Players[1].Position = new Vec2(rules.CanSpot.x, 14.0);
                Throw(sim, 1, Up, rules.ThrowChargeTime);
                Fly(sim, game, 1);
                CollectionAssert.AreEqual(new[] { chance == 0.0 ? 1 : 2 }, paths, $"bounce chance {chance}");
                if (chance == 1.0) Assert.AreEqual(rules.FlingTime * 2.0, game.CanBusyLeft, DT * 3.0, "two flings take two landings");
            }
        }

        [Test]
        public void FetchTheSlipperAndRunToTheOtherEndForAPointAndBackToTheThrowingArea()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 1, 4);
            sim.Players[4].Position = new Vec2(-10.0, 5.0);
            sim.Players[1].Position = new Vec2(0.0, TumbangRules.HomeZ + 2.0);
            Throw(sim, 1, Up, TumbangRules.ThrowChargeTime);
            Fly(sim, game, 1);
            Wait(sim, 1.2);
            sim.Players[1].Position = game.Slippers[1];
            sim.Step(DT);
            Assert.IsFalse(game.Slippers.ContainsKey(1), "picked up");
            Assert.IsTrue(game.Fetched.ContainsKey(1));
            Assert.IsTrue(game.HasSlipper(1), "in hand again, and out in the fetch area it may stay");
            sim.Players[1].Position = new Vec2(0.0, TumbangRules.GoalZ - 0.5);
            sim.Step(DT);
            Assert.AreEqual(1, game.Points[1], "a point at the other end");
            Assert.IsTrue(game.AtHome(sim.Players[1].Position), "and it is back in the throwing area on its own");
            Assert.IsFalse(game.Fetched.ContainsKey(1));
        }

        [Test]
        public void ASlipperThatEndsUpAtTheOtherEndScoresWithoutFetching()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            game.Slippers[1] = new Vec2(0.0, TumbangRules.GoalZ - 1.0);
            game.SlipperBusy[1] = 0.0;
            game.SlipperRests(sim, 1);
            Assert.AreEqual(1, game.Points[1]);
            Assert.IsTrue(game.HasSlipper(1), "the slipper is back in hand");
            Assert.IsTrue(game.AtHome(sim.Players[1].Position));
        }

        [Test]
        public void AThrowerHasFiveSecondsOutsideTheFetchAreaOrBecomesTheTaya()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            var changes = new List<(int, int, string)>();
            game.TayaChanged += (old, now, why) => changes.Add((old, now, why));
            ClearStreet(sim, 1, 4);
            sim.Players[4].Position = new Vec2(-10.0, 5.0);
            sim.Players[1].Position = new Vec2(0.0, 14.0);
            Throw(sim, 1, Up, TumbangRules.ThrowChargeTime);
            Fly(sim, game, 1);
            Wait(sim, 1.2);
            Assert.IsTrue(game.ScaredLeft.ContainsKey(1), "its slipper lies in the fetch area: the clock runs");
            Wait(sim, TumbangRules.ScaredSeconds - 2.0);
            Assert.Less(game.ScaredLeft[1], 2.5);
            sim.Players[1].Position = new Vec2(0.0, TumbangRules.HomeZ - 3.0);
            double left = game.ScaredLeft[1];
            Wait(sim, 3.0);
            Assert.AreEqual(left, game.ScaredLeft[1], 0.05, "paused while in the fetch area");
            Assert.IsEmpty(changes);
            sim.Players[1].Position = new Vec2(0.0, TumbangRules.HomeZ + 3.0);
            Wait(sim, left + 0.5);
            CollectionAssert.AreEqual(new[] { (4, 1, "scared") }, changes, "too scared: the thrower is the taya now");
            Assert.AreEqual(1, game.TayaId);
            Assert.IsTrue(game.IsResetting(), "a pause, then the round resets");
            Wait(sim, TumbangRules.ResetSeconds + 0.2);
            Assert.IsTrue(game.HasSlipper(4), "the old taya throws again, slipper in hand");
            Assert.IsTrue(game.AtHome(sim.Players[4].Position), "from the throwing area");
            Assert.IsFalse(game.Slippers.ContainsKey(1), "the new taya has no slipper to fetch");
        }

        [Test]
        public void TheTayaCannotTagWhileTheCanIsDown()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 1, 4);
            game.GraceLeft = 0.0;
            game.CanUp = false;
            game.CanPosition = new Vec2(-8.0, -6.0);
            game.Slippers[1] = new Vec2(0.0, 5.0);
            sim.Players[1].Position = new Vec2(0.0, 6.0);
            sim.Players[4].Position = new Vec2(0.0, 5.4);
            sim.Step(DT);
            Assert.AreEqual(4, game.TayaId, "the can is down and not in its place: nobody is tagged");
        }

        [Test]
        public void MovingIntoAThrowersTagCircleTagsItAndTheRoundResetsWithEveryoneBackInPlace()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            var casts = new List<(int, string)>();
            sim.WeaponCast += (id, weapon) => casts.Add((id, weapon));
            ClearStreet(sim, 1, 4);
            game.GraceLeft = 0.0;
            game.Points[2] = 3;
            game.Slippers[1] = new Vec2(0.0, 5.0);
            sim.Players[1].Position = new Vec2(0.0, 6.0);
            sim.Players[4].Position = new Vec2(TumbangRules.TagCircleRadius + 0.3, 6.0);
            sim.Step(DT);
            Assert.AreEqual(4, game.TayaId, "outside the circle");
            sim.Players[4].Position = new Vec2(TumbangRules.TagCircleRadius - 0.2, 6.0);
            sim.Step(DT);
            Assert.AreEqual(1, game.TayaId, "inside the circle: the thrower is it");
            CollectionAssert.AreEqual(new[] { (4, StreetMode.TagCast) }, casts, "the taya plays its touch");
            Assert.IsTrue(game.IsResetting());
            Vec2 at = sim.Players[1].Position;
            Input(sim, 1, new Vec2(1.0, 0.0));
            Wait(sim, 0.5);
            Assert.AreEqual(at, sim.Players[1].Position, "nobody moves during the pause");
            Wait(sim, TumbangRules.ResetSeconds);
            Assert.IsFalse(game.IsResetting());
            Assert.IsTrue(game.AtHome(sim.Players[4].Position), "the old taya is a thrower in the throwing area");
            Assert.IsTrue(game.HasSlipper(4));
            Assert.AreEqual(TumbangRules.CanSpot.y - TumbangRules.GuardOffset, sim.Players[1].Position.y, 0.01, "the new taya starts just behind the can");
            Assert.AreEqual(0, game.Slippers.Count, "every slipper is back in a hand");
            Assert.IsTrue(game.CanUp, "and the can stands");
            Assert.AreEqual(3, game.Points[2], "the points stay");
            Assert.Greater(game.GraceLeft, 0.0, "a moment of grace after the reset");
        }

        [Test]
        public void TheTayaFetchesTheFallenCanAndStandsItUpAgain()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 4);
            game.CanUp = false;
            game.CanPosition = new Vec2(-6.0, -4.0);
            sim.Players[4].Position = new Vec2(-6.0, -3.5);
            sim.Step(DT);
            Assert.AreEqual(4, game.CanCarrier, "the taya picked it up");
            sim.Players[4].Position = TumbangRules.CanSpot;
            sim.Step(DT);
            Assert.IsTrue(game.CanUp, "the can is back in its place");
        }

        [Test]
        public void AFlungCanCannotBePickedUpBeforeItLands()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 4);
            game.CanUp = false;
            game.CanPosition = new Vec2(-6.0, -4.0);
            game.CanBusyLeft = 0.5;
            sim.Players[4].Position = new Vec2(-6.0, -3.9);
            sim.Step(DT);
            Assert.AreEqual(-1, game.CanCarrier, "still in the air");
            Wait(sim, 0.6);
            Assert.AreEqual(4, game.CanCarrier);
        }

        [Test]
        public void AThrowerWithoutItsSlipperCanKickTheCanOnceInARandomMidRangeDirection()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 1, 4);
            sim.Players[4].Position = new Vec2(-10.0, 6.0);
            game.Slippers[1] = new Vec2(5.0, 6.0);
            sim.Players[1].Position = TumbangRules.CanSpot + new Vec2(1.0, 0.0);
            var kicks = new List<int>();
            game.CanKicked += id => kicks.Add(id);
            Vec2 from = game.CanPosition;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, 0));
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { 1 }, kicks);
            Assert.IsFalse(game.CanUp, "kicked over");
            double distance = game.CanPosition.DistanceTo(from);
            Assert.GreaterOrEqual(distance, TumbangRules.KickMinDistance - 0.5);
            Assert.LessOrEqual(distance, TumbangRules.KickMaxDistance + 0.5, "mid range");
            Vec2 away = from - sim.Players[1].Position;
            Assert.LessOrEqual(Math.Abs(away.AngleTo(game.CanPosition - from)), GMath.DegToRad(TumbangRules.KickConeDegrees) + 0.05, "within the cone");
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, 0));
            Wait(sim, 1.0);
            Assert.AreEqual(1, kicks.Count, "holding the button does not kick again");
            Assert.Greater(game.KickLeft[1], 0.0);
        }

        [Test]
        public void KicksGoAwayFromTheKickerInARandomDirectionWithinTheCone()
        {
            var angles = new List<double>();
            for (int seed = 0; seed < 60; seed++)
            {
                var sim = new MatchSim(SlowRules(), Kit.Layout, seed);
                for (int i = 0; i < 4; i++) sim.AddPlayer(1 + i, 0);
                sim.StartMode(StreetMode.Create(TumbangRules));
                TumbangPreso game = Game(sim);
                ClearStreet(sim, 1);
                game.Slippers[1] = new Vec2(5.0, 8.0);
                sim.Players[1].Position = TumbangRules.CanSpot + new Vec2(0.0, 1.0);
                Vec2 from = game.CanPosition;
                game.Kick(sim, sim.Players[1]);
                Vec2 away = from - sim.Players[1].Position;
                double turn = away.AngleTo(game.CanPosition - from);
                Assert.LessOrEqual(Math.Abs(turn), GMath.DegToRad(TumbangRules.KickConeDegrees) + 0.05, "never back at the kicker");
                angles.Add(turn);
            }
            Assert.Less(angles.Min(), -GMath.DegToRad(TumbangRules.KickConeDegrees) * 0.6, "it can go to either side ...");
            Assert.Greater(angles.Max(), GMath.DegToRad(TumbangRules.KickConeDegrees) * 0.6);
            MatchSim sim2 = NewMatch(TumbangRules);
            TumbangPreso game2 = Game(sim2);
            sim2.Players[1].Position = game2.CanPosition;
            game2.Kick(sim2, sim2.Players[1]);
            Assert.IsFalse(game2.CanUp, "... and a kicker standing on the can sends it anywhere");
        }

        [Test]
        public void TheCanStandsInTheMiddleOfTheStreet()
        {
            Assert.AreEqual(Vec2.Zero, TumbangRules.CanSpot);
        }

        [Test]
        public void AThrowerWithItsSlipperCannotKickAndAFarOneCannotReach()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            ClearStreet(sim, 1, 4);
            sim.Players[1].Position = TumbangRules.CanSpot + new Vec2(1.0, 0.0);
            Assert.IsFalse(game.CanKick(sim.Players[1]), "the slipper is in hand: that is the Throw button");
            game.Slippers[1] = new Vec2(5.0, 6.0);
            Assert.IsTrue(game.CanKick(sim.Players[1]));
            sim.Players[1].Position = TumbangRules.CanSpot + new Vec2(5.0, 0.0);
            Assert.IsFalse(game.CanKick(sim.Players[1]), "too far from the can");
        }

        [Test]
        public void TheKidWithTheMostPointsWinsAndATiePlaysOnToTheNextPoint()
        {
            MatchSim sim = NewMatch(TumbangRules);
            TumbangPreso game = Game(sim);
            var won = new List<int>();
            sim.MatchWon += team => won.Add(team);
            StandAll(sim);
            game.TimeLeft = DT / 2.0;
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase, "0 : 0 plays on");
            game.Points[2] = 3;
            game.Points[3] = 3;
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.Playing, sim.Phase, "a tie for the lead plays on");
            game.Award(sim, sim.Players[3]);
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, sim.Phase);
            Assert.AreEqual(3, game.WinnerId);
            Assert.AreEqual(new StreetMode.Result { Won = true, Mine = 4, Theirs = 3 }, game.LocalResult(sim, 3));
            Assert.AreEqual(new StreetMode.Result { Won = false, Mine = 3, Theirs = 4 }, game.LocalResult(sim, 2));
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 4 }, game.Ranking());
            Assert.AreEqual(1, won.Count);
        }

        // ---- the first taya (tests/unit/test_tumbang_roll.gd) -------------------------------------

        static MatchSim RollSim(long seed, int players = 4)
        {
            var sim = new MatchSim(Kit.Data.GameRules, Kit.Layout, seed);
            for (int id = 1; id <= players; id++) sim.AddPlayer(id, 0);
            sim.AssignCharacters();
            sim.StartMode(StreetMode.Create(StreetMode.RulesFor(Kit.Data, "tumbang_preso")));
            return sim;
        }

        [Test]
        public void TheFirstTayaIsDrawnFromEveryoneAndTheSameSeedDrawsTheSameKid()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 60; seed++)
            {
                int taya = Game(RollSim(seed)).TayaId;
                Assert.IsTrue(taya >= 1 && taya <= 4, "one of the four");
                seen.Add(taya);
                Assert.AreEqual(taya, Game(RollSim(seed)).TayaId, "the same seed, the same taya");
            }
            Assert.AreEqual(4, seen.Count, "over many matches everybody is drawn, not only the last kid to join");
        }

        [Test]
        public void TheOpeningShowIsLongerByTheRollInTumbangPresoOnly()
        {
            StreetModeRules game = StreetMode.RulesFor(Kit.Data, "tumbang_preso");
            Assert.Greater(game.RollSeconds, 0.0);
            MatchSim sim = RollSim(1);
            Assert.AreEqual(Kit.Data.GameRules.IntroTime + game.RollSeconds, sim.IntroTotal(), 0.0001);
            sim.BeginIntro();
            Assert.AreEqual(sim.IntroTotal(), sim.IntroLeft, 0.0001);
            Assert.AreEqual(0.0, StreetMode.RulesFor(Kit.Data, "patintero").RollSeconds, "Patintero has no roll");
            var plain = new MatchSim(Kit.Data.GameRules, Kit.Layout, 1);
            Assert.AreEqual(Kit.Data.GameRules.IntroTime, plain.IntroTotal(), "nor Agawang Base");
        }
    }
}
