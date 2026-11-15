using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A street game with one taya (the "it") and every other kid playing for itself (port of scripts/sim/taya_game.gd; docs/GDD.md
    /// "Tumbang Preso", "Langit Lupa"): the first taya is drawn at random from everyone (the match's seeded generator), each kid keeps its own
    /// points and the one with the most wins when the time is up. Tumbang Preso and Langit Lupa fill in what the taya and the others do.
    /// </summary>
    public abstract class TayaGame : StreetMode
    {
        /// <summary>The taya (the "it").</summary>
        public int TayaId = -1;
        /// <summary>id to points (the match is won by the kid with the most).</summary>
        public OrderedDict<int, int> Points = new OrderedDict<int, int>();
        /// <summary>The kid that won (-1 until the match is over).</summary>
        public int WinnerId = -1;

        protected TayaGame(StreetModeRules rules) : base(rules) { }

        public override bool IsFreeForAll() => true;

        protected override void AddSyncedFields(List<SyncedField> fields)
        {
            base.AddSyncedFields(fields);
            fields.Add(Value("taya_id", () => TayaId, v => TayaId = v));
            fields.Add(Table("points", Points));
            fields.Add(Value("winner_id", () => WinnerId, v => WinnerId = v));
        }

        public override bool IsAttacker(PlayerState state) => state.Id != TayaId;

        public override double SpeedScale(PlayerState state) => state.Id == TayaId ? Rules.TayaSpeedShare : 1.0;

        /// <summary>The first taya is drawn at random from everyone in the match, then everyone goes to their place.</summary>
        public override void Setup(MatchSim sim)
        {
            var kids = new List<int>();
            foreach (int id in sim.Players.Keys)
            {
                Points[id] = 0;
                kids.Add(id);
            }
            if (kids.Count > 0) TayaId = kids[sim.Rng.RandiRange(0, kids.Count - 1)];
            base.Setup(sim);
        }

        /// <summary>The kids by points, most first (ties by id).</summary>
        public List<int> Ranking()
        {
            var ids = new List<int>(Points.Keys);
            // GDScript's sort_custom is not stable either; the order is total (ties by id), so any sort gives the same list
            ids.Sort((a, b) => Points[a] != Points[b] ? Points[b].CompareTo(Points[a]) : a.CompareTo(b));
            return ids;
        }

        /// <summary>A point for `state`; with `sendHome` it is back in its place. Once the time is up the point wins the match.</summary>
        public override void Award(MatchSim sim, PlayerState state, bool sendHome = true, MatchSim.CaptureKind how = MatchSim.CaptureKind.None)
        {
            Points[state.Id] = Points.GetOrDefault(state.Id, 0) + 1;
            EmitScored(state.Team, state.Id);
            if (sendHome)
            {
                state.Position = Spot(sim, state);
                sim.EndMove(state);
            }
            if (TimeLeft <= 0.0) End(sim);
        }

        /// <summary>The kid with the most points, or -1 when two or more share the most (or nobody has any).</summary>
        public override int Leader(MatchSim sim)
        {
            int best = -1;
            bool tie = false;
            int top = 0;
            foreach (var pair in Points)
            {
                if (pair.Value > top)
                {
                    top = pair.Value;
                    best = pair.Key;
                    tie = false;
                }
                else if (pair.Value == top && top > 0)
                {
                    tie = true;
                }
            }
            return tie ? -1 : best;
        }

        protected override void End(MatchSim sim)
        {
            WinnerId = Leader(sim);
            sim.Score.Winner = 0;  // the sim's own winner is a team: everyone is on team 0, the kid that won is `WinnerId`
            foreach (PlayerState state in sim.Players.Values) sim.EndMove(state);
            sim.Phase = MatchSim.MatchPhase.MatchOver;
            sim.EmitMatchWon(0);
        }

        public override Result LocalResult(MatchSim sim, int localId)
        {
            int theirs = 0;
            foreach (var pair in Points)
                if (pair.Key != localId) theirs = GMath.Maxi(theirs, pair.Value);
            return new Result { Won = WinnerId == localId, Mine = Points.GetOrDefault(localId, 0), Theirs = theirs };
        }
    }
}
