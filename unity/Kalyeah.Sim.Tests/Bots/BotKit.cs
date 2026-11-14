using System.Collections.Generic;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>Helpers shared by the ported bot tests.</summary>
    public static class BotKit
    {
        public const double DT = 1.0 / 30.0;

        /// <summary>The street with no cover and no creek (the GUT tests' `_open_layout`).</summary>
        public static MapLayout OpenLayout()
        {
            MapLayout layout = Kit.Layout.Duplicate();
            layout.Obstacles = new List<Rect2>();
            layout.WaterObstacles = new List<Rect2>();
            return layout;
        }

        /// <summary>Drives every coach, then steps, for `seconds`.</summary>
        public static void Run(MatchSim sim, IEnumerable<TeamCoach> coaches, double seconds)
        {
            int ticks = GMath.Ceili(seconds / DT);
            for (int i = 0; i < ticks; i++)
            {
                foreach (TeamCoach coach in coaches) coach.Drive(sim);
                sim.Step(DT);
            }
        }

        public static void Run(MatchSim sim, TeamCoach coach, double seconds) => Run(sim, new[] { coach }, seconds);

        /// <summary>Where a bot's input walks it, as a unit direction (zero: it stands).</summary>
        public static Vec2 Way(PlayerInput input, PlayerState state)
        {
            Vec2 offset = input.IsPressed(PlayerInput.BtnMoveTarget) ? input.MoveTarget - state.Position : input.Move;
            return offset.Length() > 0.001 ? offset.Normalized() : Vec2.Zero;
        }

        /// <summary>Any hand-slot bit (a card held).</summary>
        public static int CardBits(PlayerInput input) =>
            input.Buttons & (PlayerInput.BtnCard1 | PlayerInput.BtnCard2 | PlayerInput.BtnCard3 | PlayerInput.BtnCard4);

        public static void FillCooldowns(PlayerState state, double value)
        {
            for (int i = 0; i < state.WeaponCooldowns.Count; i++) state.WeaponCooldowns[i] = value;
        }
    }
}
