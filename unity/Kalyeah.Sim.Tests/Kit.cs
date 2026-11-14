using System.Collections.Generic;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Shared helpers for the ported GUT tests (tests/unit/*.gd).</summary>
    public static class Kit
    {
        public const double DT = 1.0 / 30.0;

        public static SimData Data => GoldenMatchTests.Data;
        /// <summary>A fresh copy of the shipped rules (tests may change it).</summary>
        public static GameRules Rules() => Data.GameRules.Duplicate();
        public static MapLayout Layout => Data.MapLayout;

        /// <summary>The street with one cover wall and nothing else (test_collision / test_weapons).</summary>
        public static MapLayout CoverStreet()
        {
            MapLayout street = Layout.Duplicate();
            street.Obstacles = new List<Rect2> { new Rect2(-13.5, 10.5, 3.0, 0.5) };
            street.WaterObstacles = new List<Rect2>();
            street.PowerupSpots = new List<Vec2> { new Vec2(-70.0, 70.0) };
            return street;
        }

        public static MapLayout OpenLayout()
        {
            MapLayout street = Layout.Duplicate();
            street.Obstacles = new List<Rect2>();
            street.WaterObstacles = new List<Rect2>();
            street.PowerupSpots = new List<Vec2> { new Vec2(-70.0, 70.0) };
            return street;
        }

        public static void Wait(MatchSim sim, double seconds)
        {
            int ticks = GMath.Ceili(seconds / DT);
            for (int i = 0; i < ticks; i++) sim.Step(DT);
        }

        public static void Steps(MatchSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(DT);
        }

        public static void Run(MatchSim sim, int id, Vec2 move, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.SetInput(id, PlayerInput.Create(move, Vec2.Zero, 0, sim.Tick));
                sim.Step(DT);
            }
        }

        public static void Tick(MatchSim sim, int id, Vec2 move, int buttons)
        {
            sim.SetInput(id, PlayerInput.Create(move, Vec2.Zero, buttons, sim.Tick));
            sim.Step(DT);
        }

        /// <summary>Hold slot `slot` for `holdTicks`, then release with `aim` (or cancel).</summary>
        public static void Cast(MatchSim sim, int id, int slot, Vec2 aim, int holdTicks = 1, bool cancel = false)
        {
            int bit = PlayerInput.CardBits[slot];
            for (int i = 0; i < holdTicks; i++)
            {
                sim.SetInput(id, PlayerInput.Create(Vec2.Zero, aim, bit, sim.Tick));
                sim.Step(DT);
            }
            sim.SetInput(id, PlayerInput.Create(Vec2.Zero, aim, cancel ? PlayerInput.BtnAimCancel : 0, sim.Tick));
            sim.Step(DT);
        }
    }
}
