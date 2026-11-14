using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// One 1v1 between two sparring bots (FightBrain) on an open street, for balance numbers (port of scripts/sim/balance_duel.gd): the
    /// first to be tagged loses, and a duel nobody wins in time is a draw. Pure sim, deterministic for a seed.
    /// </summary>
    public static class BalanceDuel
    {
        /// <summary>How far apart they start (metres), along the street.</summary>
        public const double StartDistance = 10.0;
        public const double MaxSeconds = 60.0;
        public const int NoWinner = -1;

        /// <summary>The result of a duel (GDScript: {winner, seconds, hp: [first, second]}).</summary>
        public sealed class Result
        {
            /// <summary>0, 1 or <see cref="NoWinner"/>.</summary>
            public int Winner;
            public double Seconds;
            /// <summary>HP of the first and the second bot at the end.</summary>
            public int[] Hp;
        }

        /// <summary>The street without its cover (a fair, empty arena); the walls stay.</summary>
        public static MapLayout Arena(MapLayout layout)
        {
            MapLayout open = layout.Duplicate();
            open.Obstacles = new List<Rect2>();
            return open;
        }

        /// <summary>
        /// Plays `first` (team 0) against `second` (team 1). `skill` is how well both bots aim (0 to 1, see FightBrain); `cover` keeps the
        /// street's own cover in the way.
        /// </summary>
        public static Result Run(GameRules rules, MapLayout layout, IList<string> first, IList<string> second, long seed,
            double maxSeconds = MaxSeconds, double skill = 0.0, bool cover = false)
        {
            var sim = new MatchSim(rules, cover ? layout : Arena(layout), seed);
            Vec2 top = new Vec2(0.0, -StartDistance / 2.0);
            Vec2 bottom = new Vec2(0.0, StartDistance / 2.0);
            FightBrain low = FightBrain.Fighter(bottom);
            FightBrain high = FightBrain.Fighter(top);
            low.Skill = skill;
            high.Skill = skill;
            sim.AddDummy(1, 0, bottom, low);
            sim.AddDummy(2, 1, top, high);
            sim.SetLoadout(1, first[0], first[1]);
            sim.SetLoadout(2, second[0], second[1]);
            double dt = rules.TickDt();
            int ticks = GMath.Ceili(maxSeconds / dt);
            int winner = NoWinner;
            double seconds = maxSeconds;
            for (int i = 0; i < ticks; i++)
            {
                sim.Step(dt);
                bool firstDown = sim.Players[1].Tagged;
                bool secondDown = sim.Players[2].Tagged;
                if (firstDown || secondDown)
                {
                    if (firstDown && !secondDown) winner = 1;
                    else if (secondDown && !firstDown) winner = 0;
                    seconds = (double)(i + 1) * dt;
                    break;
                }
            }
            return new Result { Winner = winner, Seconds = seconds, Hp = new[] { sim.Players[1].Hp, sim.Players[2].Hp } };
        }

        /// <summary>Every two-weapon loadout the rules allow (two different weapons), as [first, second] ids in file order.</summary>
        public static List<string[]> AllLoadouts(GameRules rules)
        {
            var found = new List<string[]>();
            for (int i = 0; i < rules.Weapons.Count; i++)
                for (int j = i + 1; j < rules.Weapons.Count; j++)
                    found.Add(new[] { rules.Weapons[i].Id, rules.Weapons[j].Id });
            return found;
        }
    }
}
