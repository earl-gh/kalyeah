using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Port of test_trip.gd: continuous running trip chance every TripInterval seconds,
    /// analog walk / standing / stopping / overclocked / dummy exemptions, Tripped hard-CC
    /// movement &amp; cast lock, and isolated _tripRng determinism leaving MatchSim.Rng untouched.
    /// </summary>
    public class RunningTripTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp() => _rules = Rules();

        MatchSim Sim(long seed = 5)
        {
            var sim = new MatchSim(_rules, OpenLayout(), seed);
            sim.AddPlayer(1, 0);
            sim.Players[1].Position = new Vec2(0.0, 56.0);
            sim.Equip(1);
            return sim;
        }

        static void RunBackAndForth(MatchSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                double way = (i / 30) % 2 == 0 ? 1.0 : -1.0;
                Tick(sim, 1, new Vec2(way, 0.0), 0);
            }
        }

        static void Stand(MatchSim sim, int ticks) => Run(sim, 1, Vec2.Zero, ticks);

        static int TicksFor(double seconds) => GMath.Ceili(seconds / DT) + 1;

        [Test]
        public void ShippedTripNumbersMatchData()
        {
            Assert.AreEqual(0.12, _rules.TripChance, 1e-4);
            Assert.AreEqual(6.0, _rules.TripInterval, 1e-4);
            Assert.AreEqual(1.05, _rules.TripSeconds, 1e-4);
        }

        [Test]
        public void AnalogWalkBelowThresholdSetsWalkingAndNeverTrips()
        {
            _rules.TripChance = 1.0;
            MatchSim sim = Sim();
            int total = TicksFor(_rules.TripInterval) * 2;
            for (int i = 0; i < total; i++)
            {
                double way = (i / 30) % 2 == 0 ? 0.5 : -0.5;
                Tick(sim, 1, new Vec2(way, 0.0), 0);
                Assert.IsTrue(sim.Players[1].Walking, "analog deflection < 0.62 walks");
                Assert.IsFalse(sim.Players[1].Effects.Has(StatusEffects.Type.Tripped), "walking never rolls a trip");
            }
        }

        [Test]
        public void SureTripDropsRunnerAfterIntervalAndKidCannotMoveOrCastUntilUp()
        {
            var fx = new StatusEffects();
            Assert.IsTrue(fx.Apply(StatusEffects.Type.Tripped, 1.2));
            CollectionAssert.AreEqual(new[] { "Tripped" }, fx.DisplayNameList());
            Assert.IsFalse(fx.CanMove());
            Assert.IsFalse(fx.CanCast());

            _rules.TripChance = 1.0;
            MatchSim sim = Sim();
            RunBackAndForth(sim, TicksFor(_rules.TripInterval) - 3);
            Assert.IsFalse(sim.Players[1].Effects.Has(StatusEffects.Type.Tripped), "no roll before interval");
            RunBackAndForth(sim, 6);
            Assert.IsTrue(sim.Players[1].Effects.Has(StatusEffects.Type.Tripped), "tripped once interval completes");

            Vec2 fallenAt = sim.Players[1].Position;
            RunBackAndForth(sim, TicksFor(_rules.TripSeconds) - 6);
            Assert.IsTrue(sim.Players[1].Effects.Has(StatusEffects.Type.Tripped));
            Assert.AreEqual(fallenAt, sim.Players[1].Position, "lying on pavement");
            Assert.IsFalse(sim.Players[1].CanAct(), "cannot cast while tripped");

            RunBackAndForth(sim, 8);
            Assert.IsFalse(sim.Players[1].Effects.Has(StatusEffects.Type.Tripped), "up again");
            RunBackAndForth(sim, 3);
            Assert.AreNotEqual(fallenAt, sim.Players[1].Position, "running again");
        }

        [Test]
        public void ZeroChanceStandingStoppingOverclockedAndTrainingDummiesNeverTrip()
        {
            _rules.TripChance = 0.0;
            MatchSim noTrip = Sim();
            for (int sec = 0; sec < 60; sec++)
            {
                RunBackAndForth(noTrip, 30);
                Assert.IsFalse(noTrip.Players[1].Effects.Has(StatusEffects.Type.Tripped));
                noTrip.Players[1].Stamina = _rules.StaminaMax;
            }

            _rules.TripChance = 1.0;
            MatchSim stopSim = Sim();
            Stand(stopSim, 30 * 20);
            Assert.IsFalse(stopSim.Players[1].Effects.Has(StatusEffects.Type.Tripped));
            RunBackAndForth(stopSim, TicksFor(_rules.TripInterval) - 10);
            Stand(stopSim, 1);
            RunBackAndForth(stopSim, TicksFor(_rules.TripInterval) - 10);
            Assert.IsFalse(stopSim.Players[1].Effects.Has(StatusEffects.Type.Tripped), "stopping resets RunTime");

            MatchSim wornOut = Sim();
            wornOut.Players[1].Overclocked = true;
            RunBackAndForth(wornOut, TicksFor(_rules.TripInterval) * 2);
            Assert.IsFalse(wornOut.Players[1].Effects.Has(StatusEffects.Type.Tripped), "overclocked walk never trips");

            PlayerState dummy = wornOut.AddDummy(7, 1, new Vec2(12.0, -20.0), DummyBrain.Standing());
            Assert.IsFalse(dummy.Trips);
        }

        [Test]
        public void TripProbabilitySeedDeterminismAndIsolatedMatchRng()
        {
            _rules.TripChance = 0.2;
            int tripped = 0;
            const int runs = 400;
            int intervalTicks = TicksFor(_rules.TripInterval);
            for (int seed = 0; seed < runs; seed++)
            {
                MatchSim sim = Sim(seed);
                RunBackAndForth(sim, intervalTicks);
                if (sim.Players[1].Effects.Has(StatusEffects.Type.Tripped)) tripped++;
            }
            Assert.AreEqual(_rules.TripChance, (double)tripped / runs, 0.07, $"{tripped} of {runs} tripped");

            MatchSim first = Sim(21), second = Sim(21);
            for (int i = 0; i < 600; i++)
            {
                RunBackAndForth(first, 1);
                RunBackAndForth(second, 1);
                Assert.AreEqual(
                    first.Players[1].Effects.Has(StatusEffects.Type.Tripped),
                    second.Players[1].Effects.Has(StatusEffects.Type.Tripped));
            }

            _rules.TripChance = 0.2;
            MatchSim withTrips = Sim(9);
            RunBackAndForth(withTrips, 300);
            _rules.TripChance = 0.0;
            MatchSim withoutTrips = Sim(9);
            RunBackAndForth(withoutTrips, 300);
            Assert.AreEqual(withoutTrips.Rng.Randi(), withTrips.Rng.Randi(), "trip RNG never advances MatchSim.Rng");
        }
    }
}
