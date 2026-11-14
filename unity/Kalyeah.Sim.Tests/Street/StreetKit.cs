using System.Collections.Generic;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>Shared helpers of the street games' tests (tests/unit/test_street_modes.gd, test_langit_lupa.gd).</summary>
    public static class StreetKit
    {
        public const double DT = Kit.DT;

        public static StreetModeRules TumbangRules => Kit.Data.StreetModeRules("tumbang_preso");
        public static StreetModeRules PatinteroRules => Kit.Data.StreetModeRules("patintero");
        public static StreetModeRules LangitRules => Kit.Data.StreetModeRules("langit_lupa");

        /// <summary>test_street_modes.gd `before_each`: the shipped rules at move speed 5 without sprint.</summary>
        public static GameRules SlowRules()
        {
            GameRules rules = Kit.Rules();
            rules.MoveSpeed = 5.0;
            rules.SprintEnabled = false;
            return rules;
        }

        /// <summary>The street with nothing on it (test_street_modes.gd `_open_layout`).</summary>
        public static MapLayout OpenLayout()
        {
            MapLayout open = Kit.Layout.Duplicate();
            open.Tiles = new Vec2I(16, 32);
            open.ArenaRadius = 0.0;
            open.Obstacles = new List<Rect2>();
            open.WaterObstacles = new List<Rect2>();
            return open;
        }

        /// <summary>Tumbang Preso: `size` kids on one team, ids 1..size, the last the taya. Patintero: `size` kids a side, team 0 (1..) runs.</summary>
        public static MatchSim NewMatch(StreetModeRules modeRules, int size = 4, GameRules rules = null)
        {
            var sim = new MatchSim(rules ?? SlowRules(), OpenLayout(), 7);
            for (int i = 0; i < size; i++) sim.AddPlayer(1 + i, 0);
            if (modeRules.Id != "tumbang_preso")
                for (int i = 0; i < size; i++) sim.AddPlayer(4 + i, 1);
            sim.StartMode(StreetMode.Create(modeRules));
            if (sim.Mode is TumbangPreso game)
            {
                // the match draws the first taya at random (TumbangRollTests); these tests want the last kid in
                game.TayaId = size;
                game.PlaceTeams(sim);
            }
            return sim;
        }

        public static void Wait(MatchSim sim, double seconds) => Kit.Wait(sim, seconds);

        public static void Input(MatchSim sim, int id, Vec2 move, int buttons = 0, Vec2 aim = default) =>
            sim.SetInput(id, PlayerInput.Create(move, aim, buttons, sim.Tick));

        public static void StandAll(MatchSim sim)
        {
            foreach (int id in sim.Players.Keys) sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0));
        }

        /// <summary>Everyone but `keep` far from the play, in the throwing area.</summary>
        public static void ClearStreet(MatchSim sim, params int[] keep)
        {
            double x = -10.0;
            foreach (PlayerState state in sim.Players.Values)
            {
                if (System.Array.IndexOf(keep, state.Id) >= 0) continue;
                state.Position = new Vec2(x, 20.0);
                x += 2.0;
            }
        }

        /// <summary>Holds the Throw button of `id` for `seconds` aimed along `way`, then lets go (one tick without it).</summary>
        public static void Throw(MatchSim sim, int id, Vec2 way, double seconds)
        {
            int ticks = GMath.Ceili(seconds / DT);
            for (int i = 0; i < ticks; i++)
            {
                sim.SetInput(id, PlayerInput.Create(Vec2.Zero, way, PlayerInput.BtnCard1, sim.Tick));
                sim.Step(DT);
            }
            sim.SetInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick));
            sim.Step(DT);
            sim.SetInput(id, null);
        }

        /// <summary>Steps until the slipper of `id` has left the air (or `limit` seconds pass).</summary>
        public static void Fly(MatchSim sim, TumbangPreso game, int id, double limit = 3.0)
        {
            int left = GMath.Ceili(limit / DT);
            while (game.Flights.ContainsKey(id) && left > 0)
            {
                sim.Step(DT);
                left -= 1;
            }
        }

        /// <summary>Sends Patintero runner `id` over the vertical line (between the two horizontal lines) so the far end counts for it.</summary>
        public static void CrossMiddle(MatchSim sim, int id)
        {
            sim.Players[id].Position = new Vec2(-1.5, 0.0);
            sim.Step(DT);
            sim.Players[id].Position = new Vec2(1.5, 0.0);
            sim.Step(DT);
        }

        public static void AreNear(Vec2 want, Vec2 got, double tolerance, string message = "")
        {
            Assert.AreEqual(want.x, got.x, tolerance, message + " (x)");
            Assert.AreEqual(want.y, got.y, tolerance, message + " (z)");
        }

        public static double RadToDeg(double rad) => GMath.RadToDeg(rad);
    }
}
