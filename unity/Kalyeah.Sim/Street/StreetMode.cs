using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A timed street game played on the Agawang Base street (port of scripts/sim/street_mode.gd; docs/GDD.md "Tumbang Preso and
    /// Patintero"): Patintero (two teams, runners against guards on their lines), Tumbang Preso and Langit Lupa (every kid for itself against
    /// one taya). After `MatchSeconds` the leader wins (a tie plays on to the next point). Plain sim class: MatchSim calls it each tick (through
    /// <see cref="IStreetMode"/>) instead of the rescue and capture rules. There are no skills in these games: casting is off and nothing deals
    /// damage. The subclasses fill in the hooks.
    /// </summary>
    public abstract class StreetMode : IStreetMode
    {
        /// <summary>The street games by RoomService.MAP_IDS id (Agawang Base, `street`, has none: it is the base game).</summary>
        public static readonly string[] GameIds = { "tumbang_preso", "patintero", "langit_lupa" };

        /// <summary>The id the tagger's touch is sent to the views as (`MatchSim.WeaponCast`): the kid plays its run-cast clip.</summary>
        public const string TagCast = "tag";

        /// <summary>(tagger, target): `tagger` caught `target`.</summary>
        public event Action<int, int> Tagged;
        /// <summary>(team, id): `id` (of `team`) earned a point.</summary>
        public event Action<int, int> Scored;
        /// <summary>(who): the roles changed hands: Patintero's `who` is the team that runs now; a taya game's is the new taya's id.</summary>
        public event Action<int> RolesSwapped;

        protected void EmitTagged(int tagger, int target) => Tagged?.Invoke(tagger, target);
        protected void EmitScored(int team, int id) => Scored?.Invoke(team, id);
        protected void EmitRolesSwapped(int who) => RolesSwapped?.Invoke(who);

        public StreetModeRules Rules { get; }
        /// <summary>Patintero: the team that runs this turn; the other guards.</summary>
        public int AttackTeam;
        /// <summary>Seconds left in the match (0 = time is up; a tie plays on).</summary>
        public double TimeLeft { get; set; }
        /// <summary>Seconds left in which nobody can be tagged (after a change of roles).</summary>
        public double GraceLeft;
        /// <summary>Seconds left of the pause after a tag (everyone stands still, then the round resets); 0 = playing.</summary>
        public double ResetLeft;

        protected StreetMode(StreetModeRules rules)
        {
            Rules = rules;
            TimeLeft = rules.MatchSeconds;
        }

        /// <summary>The game for `rules.Id` (Tumbang Preso, Patintero or Langit Lupa), or null for another id.</summary>
        public static StreetMode Create(StreetModeRules rules)
        {
            switch (rules.Id)
            {
                case "tumbang_preso": return new TumbangPreso(rules);
                case "patintero": return new Patintero(rules);
                case "langit_lupa": return new LangitLupa(rules);
            }
            return null;
        }

        /// <summary>The rules of game `id`, or null for Agawang Base (or an unknown id).</summary>
        public static StreetModeRules RulesFor(SimData data, string id) => Array.IndexOf(GameIds, id) >= 0 ? data.StreetModeRules(id) : null;

        // ---- the client's copy (Snapshot) ---------------------------------------------------------

        /// <summary>One synced field: its GDScript name, a copy of its value, and a writer that skips a value of another type.</summary>
        protected sealed class SyncedField
        {
            public string Name;
            public Func<object> Get;
            public Action<object> Set;
        }

        protected static SyncedField Value<T>(string name, Func<T> get, Action<T> set) =>
            new SyncedField { Name = name, Get = () => get(), Set = v => { if (v is T t) set(t); } };

        protected static SyncedField Table<T>(string name, OrderedDict<int, T> table) =>
            new SyncedField
            {
                Name = name,
                Get = () => CopyTable(table),
                Set = v =>
                {
                    if (!(v is OrderedDict<int, T> source)) return;
                    OrderedDict<int, T> copy = CopyTable(source);
                    table.Clear();
                    foreach (var pair in copy) table[pair.Key] = pair.Value;
                },
            };

        public static OrderedDict<int, T> CopyTable<T>(OrderedDict<int, T> table)
        {
            var copy = new OrderedDict<int, T>();
            foreach (var pair in table) copy[pair.Key] = pair.Value;
            return copy;
        }

        /// <summary>The fields a client's copy needs to show the game as the server plays it. Each game adds its own.</summary>
        protected virtual void AddSyncedFields(List<SyncedField> fields)
        {
            fields.Add(Value("attack_team", () => AttackTeam, v => AttackTeam = v));
            fields.Add(Value("time_left", () => TimeLeft, v => TimeLeft = v));
            fields.Add(Value("grace_left", () => GraceLeft, v => GraceLeft = v));
            fields.Add(Value("reset_left", () => ResetLeft, v => ResetLeft = v));
        }

        List<SyncedField> Fields()
        {
            var fields = new List<SyncedField>();
            AddSyncedFields(fields);
            return fields;
        }

        /// <summary>The names of the synced fields (GDScript's snake_case, as the SNAPSHOT message keys them).</summary>
        public List<string> SyncedFields() => Fields().ConvertAll(f => f.Name);

        /// <summary>This game's state as plain data (for the SNAPSHOT message): tables are copies (OrderedDict&lt;int, T&gt;).</summary>
        public virtual OrderedDict<string, object> Snapshot()
        {
            var data = new OrderedDict<string, object>();
            foreach (SyncedField field in Fields()) data[field.Name] = field.Get();
            return data;
        }

        /// <summary>Writes a `Snapshot()` into this game (a client's copy), after `sim`'s own state was written. A missing field, or one of
        /// another type (an untrusted message), is skipped.</summary>
        public virtual void Restore(OrderedDict<string, object> data, MatchSim sim = null)
        {
            foreach (SyncedField field in Fields())
                if (data.TryGetValue(field.Name, out object value)) field.Set(value);
        }

        // ---- what each phone sees -------------------------------------------------------------------

        /// <summary>True when the kid `viewer` can see `target` now (Langit Lupa's brownout hides the far ones). Every view asks this.</summary>
        public virtual bool VisibleTo(MatchSim sim, int viewer, int target) => true;

        /// <summary>The kids `viewer` may not be told about now (Langit Lupa's brownout): the server leaves them out of that phone's snapshot.</summary>
        public virtual List<int> HiddenFrom(MatchSim sim, int viewer) => new List<int>();

        /// <summary>`data` (this game's `Snapshot()`) as the phone that cannot see `hidden` gets it.</summary>
        public virtual OrderedDict<string, object> FilterFor(OrderedDict<string, object> data, IList<int> hidden) => data;

        // ---- the game ---------------------------------------------------------------------------

        /// <summary>Everyone to their places for the first turn (call once all players are in).</summary>
        public virtual void Setup(MatchSim sim)
        {
            foreach (PlayerState state in sim.Players.Values) state.HasStamina = false;  // speed is the game's own: nobody tires
            PlaceTeams(sim);
        }

        /// <summary>True when `state` is a runner (Patintero) or a thrower (Tumbang Preso) that a guard or the taya could tag right now.</summary>
        public virtual bool IsTaggable(PlayerState state) =>
            IsAttacker(state) && !state.Tagged && !AtHome(state.Position) && !AtGoal(state.Position);

        /// <summary>How long (s) a tag's touch plays before the game goes on or resets.</summary>
        public virtual double CatchSeconds() => Rules.ResetSeconds;

        /// <summary>A runner (Patintero) or a thrower (Tumbang Preso); the other side guards.</summary>
        public virtual bool IsAttacker(PlayerState state) => state.Team == AttackTeam;

        /// <summary>In the safe home (behind the throw line, or the start line).</summary>
        public bool AtHome(Vec2 position) => position.y >= Rules.HomeZ;

        /// <summary>Past the other end.</summary>
        public bool AtGoal(Vec2 position) => position.y <= Rules.GoalZ;

        /// <summary>True for a free-for-all of single kids (Tumbang Preso, Langit Lupa) and not two teams.</summary>
        public virtual bool IsFreeForAll() => false;

        /// <summary>How fast `state` runs next to a normal kid (the taya is faster).</summary>
        public virtual double SpeedScale(PlayerState state) => 1.0;

        /// <summary>One tick of play (after the kids moved and the weapons flew).</summary>
        public void Step(MatchSim sim, double dt)
        {
            TimeLeft = GMath.Maxf(0.0, TimeLeft - dt);
            GraceLeft = GMath.Maxf(0.0, GraceLeft - dt);
            if (ResetLeft > 0.0)
            {
                ResetLeft -= dt;
                if (ResetLeft <= 0.0)
                {
                    ResetLeft = 0.0;
                    NewRound(sim);
                }
                return;
            }
            StepGame(sim, dt);
            if (sim.Phase != MatchSim.MatchPhase.Playing) return;
            StepTurn(sim, dt);
            if (sim.Phase == MatchSim.MatchPhase.Playing && TimeLeft <= 0.0 && Leader(sim) != -1) End(sim);
        }

        /// <summary>True during the pause after a tag: everyone stands still.</summary>
        public bool IsResetting() => ResetLeft > 0.0;

        /// <summary>After a tag the round resets by itself: a short pause (the tagger's touch plays), then `NewRound`.</summary>
        public void BeginReset(MatchSim sim)
        {
            ResetLeft = Rules.ResetSeconds;
            foreach (PlayerState state in sim.Players.Values) sim.EndMove(state);
        }

        /// <summary>Everyone back to their places for the next round (the points stay).</summary>
        protected virtual void NewRound(MatchSim sim)
        {
            PlaceTeams(sim);
            GraceLeft = Rules.SwapGrace;
        }

        /// <summary>The rules of the game itself (fetching, scoring, tagging).</summary>
        protected virtual void StepGame(MatchSim sim, double dt) { }

        /// <summary>What the game does after its own rules each tick (Patintero's stall timer).</summary>
        protected virtual void StepTurn(MatchSim sim, double dt) { }

        /// <summary>The game's own actions from `state`'s input this tick (a Tumbang Preso throw or kick, a dive, a mound).</summary>
        public virtual void HandleInput(MatchSim sim, PlayerState state, PlayerInput input, double dt) { }

        /// <summary>True when standing still refills `state`'s stamina here (Langit Lupa: not on a safe spot).</summary>
        public virtual bool CanRest(PlayerState state) => true;

        /// <summary>Where a walk to `point` really goes (a Patintero guard stays on its line).</summary>
        public virtual Vec2 MovePoint(PlayerState state, Vec2 point) => point;

        /// <summary>Called after `state` moved this tick (a guard is held on its line, a thrower behind the throw line).</summary>
        public virtual void Constrain(MatchSim sim, PlayerState state) { }

        /// <summary>True when `guard` may tag `runner` now.</summary>
        public virtual bool CanTag(MatchSim sim, PlayerState guard, PlayerState runner) =>
            GraceLeft <= 0.0 && !guard.Tagged && guard.Effects.CanMove() && IsTaggable(runner);

        /// <summary>Every guard tags every runner it touches (the first touch of the tick: it may change who guards).</summary>
        public void CheckTags(MatchSim sim)
        {
            foreach (PlayerState guard in sim.Players.Values)
            {
                if (IsAttacker(guard)) continue;
                foreach (PlayerState runner in sim.Players.Values)
                {
                    if (!IsAttacker(runner) || !CanTag(sim, guard, runner)) continue;
                    if (guard.Position.DistanceTo(runner.Position) <= Rules.TagCircleRadius)
                    {
                        Tag(sim, guard, runner);
                        return;
                    }
                }
            }
        }

        /// <summary>`guard` moved into the tag circle of `runner` (GDScript `_tag`): it plays its touch and the round resets.</summary>
        protected virtual void Tag(MatchSim sim, PlayerState guard, PlayerState runner)
        {
            sim.EmitWeaponCast(guard.Id, TagCast);
            EmitTagged(guard.Id, runner.Id);
            BeginReset(sim);
        }

        /// <summary>A point for `state` (and its team); with `sendHome` it goes back to its place to run again. Once the time is up the
        /// point wins the match.</summary>
        public virtual void Award(MatchSim sim, PlayerState state, bool sendHome = true, MatchSim.CaptureKind how = MatchSim.CaptureKind.None)
        {
            AddTeamPoint(sim, state.Team, state.Id, how);
            if (sendHome)
            {
                state.Position = Spot(sim, state);
                sim.EndMove(state);
            }
        }

        /// <summary>A point for `team`, earned by `id`. Once the time is up the point wins the match.</summary>
        protected void AddTeamPoint(MatchSim sim, int team, int id, MatchSim.CaptureKind how = MatchSim.CaptureKind.None)
        {
            sim.Score.Points[team] += 1;
            sim.PointLog.Add(new Vec2I(team, (int)how));
            EmitScored(team, id);
            if (TimeLeft <= 0.0 && sim.Phase == MatchSim.MatchPhase.Playing) End(sim);
        }

        /// <summary>Everyone to their place for this turn at full HP, back from the sidewalk; the game's own things back to the start.</summary>
        public void PlaceTeams(MatchSim sim)
        {
            ResetGame(sim);
            foreach (PlayerState state in sim.Players.Values)
            {
                bool wasTagged = state.Tagged;
                sim.Objectives.ClearActions(state);
                state.Tagged = false;
                state.TagTime = 0.0;
                state.TagOrder = 0;
                state.Hp = sim.Rules.PlayerMaxHp;
                state.Position = Spot(sim, state);
                state.SpawnPosition = state.Position;
                state.Facing = IsAttacker(state) ? new Vec2(0.0, -1.0) : new Vec2(0.0, 1.0);
                sim.RefillStamina(state);
                if (wasTagged) sim.EmitPlayerRescued(state.Id, MatchSim.ReleasedAlone);
            }
        }

        /// <summary>The game's own things (the can, the slippers, who crossed) back to the start of a turn.</summary>
        protected virtual void ResetGame(MatchSim sim) { }

        /// <summary>Where `state` stands at the start of a turn (GDScript `_spot`).</summary>
        public virtual Vec2 Spot(MatchSim sim, PlayerState state)
        {
            var mates = new List<int>();
            foreach (PlayerState other in sim.Players.Values)
                if (other.Team == state.Team && IsAttacker(other) == IsAttacker(state)) mates.Add(other.Id);
            return SpotFor(state, IsAttacker(state), mates.IndexOf(state.Id), mates.Count);
        }

        /// <summary>The start spot of the `slot`-th of `count` kids of a role. Attackers stand in a row behind the home line.</summary>
        public virtual Vec2 SpotFor(PlayerState state, bool attacker, int slot, int count)
        {
            double x = (slot - (count - 1) / 2.0) * Rules.SpawnSpacing;
            return new Vec2(x, attacker ? Rules.HomeZ + Rules.SpawnSpacing : 0.0);
        }

        /// <summary>Who leads: a team (Patintero), or a kid (a taya game), or -1 when it is a tie.</summary>
        public virtual int Leader(MatchSim sim)
        {
            int[] points = sim.Score.Points;
            return points[0] == points[1] ? -1 : (points[0] > points[1] ? 0 : 1);
        }

        /// <summary>Time is up and one side leads (or a golden point was scored): it wins.</summary>
        protected virtual void End(MatchSim sim)
        {
            sim.Score.Winner = GMath.Maxi(Leader(sim), 0);
            foreach (PlayerState state in sim.Players.Values) sim.EndMove(state);
            sim.Phase = MatchSim.MatchPhase.MatchOver;
            sim.EmitMatchWon(sim.Score.Winner);
        }

        /// <summary>How the match went for a kid: won, and the points of its side and of the best other side.</summary>
        public struct Result
        {
            public bool Won;
            public int Mine;
            public int Theirs;
        }

        /// <summary>How the match went for the local kid.</summary>
        public virtual Result LocalResult(MatchSim sim, int localId)
        {
            int team = sim.Players.TryGetValue(localId, out PlayerState state) ? state.Team : 0;
            return new Result { Won = sim.Score.Winner == team, Mine = sim.Score.Points[team], Theirs = sim.Score.Points[1 - team] };
        }
    }
}
