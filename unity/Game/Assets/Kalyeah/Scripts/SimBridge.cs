// =====================================================================================================================================
// SimBridge: the ONE place the Unity game calls the sim (unity/Kalyeah.Sim, the C# port of scripts/sim/*.gd). Everything else in
// unity/Game reads KidSnapshot / the properties below, so when the port's real names differ, only this file changes.
//
// Two builds of this file:
//   KALYEAH_SIM_PORTED defined  -> the real sim. Set automatically: tools/unity_export/sync_assets.sh writes the embedded package
//                                  com.kalyeah.sim at version 1.0.0 once unity/Kalyeah.Sim has `class MatchSim`, and
//                                  Kalyeah.Game.Core.asmdef turns that version into the define (versionDefines).
//   not defined (today)         -> StandInMatch.cs, a throwaway stand-in (NOT the rules) so the view can be built and seen now.
//
// SIM API USED (unity/Kalyeah.Sim; namespace Kalyeah.Sim; Vec2 is Godot's float32 Vector2, X = x and Y = z on the ground):
//   SimData (AddFile per unity/Data json, following $refs) -> GameRules, MapLayout
//   new MatchSim(GameRules, MapLayout, long seed): AddPlayer, Equip, SetInput, Step, BeginIntro, Rng, Players (insertion order), Tick,
//       MatchTime, Score.Points, events PlayerTagged (id, byId) and PointScored (team, how)
//   PlayerState: Id, Team, Hp, Tagged, CharacterId, Position, Facing;  PlayerInput.Create(Vec2 move, Vec2 aim, int buttons, int tick)
//   new TeamCoach(team, followId = -1): Add(sim, id, skill), Drive(sim) once per tick before Step
// =====================================================================================================================================
using System;
using System.Collections.Generic;
using System.Numerics;
#if KALYEAH_SIM_PORTED
using Kalyeah.Sim;
#endif

namespace Kalyeah.Game
{
    /// <summary>What the view draws of one kid, copied out of the sim after each tick.</summary>
    public struct KidSnapshot
    {
        public int Id;
        public int Team;
        public string CharacterId;
        /// <summary>Feet on the ground, sim XZ (GodotSpace.World makes it a Unity point).</summary>
        public Vector2 Position;
        public Vector2 Facing;
        public int Hp;
        public bool Tagged;
        /// <summary>How fast it moved this tick, as a share of a full run (0..1): idle or run.</summary>
        public float Speed01;
    }

    /// <summary>A 5v5 practice match (you and four bot allies against five bots), stepped at the sim's fixed tick.</summary>
    public sealed class SimBridge
    {
        /// <summary>Same ids as scripts/view/practice_match.gd (LOCAL_ID, BOT_ALLY_IDS, BOT_ENEMY_IDS).</summary>
        public const int LocalId = 1;
        public static readonly int[] AllyIds = { 2, 3, 4, 5 };
        public static readonly int[] EnemyIds = { 6, 7, 8, 9, 10 };
        public const int LocalTeam = 0;

        readonly List<KidSnapshot> _kids = new List<KidSnapshot>();
        readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();
        readonly float _runPerTick;
        Vector2 _move;
        int _buttons;

        /// <summary>Seconds per tick (1 / tick_rate: 1/30).</summary>
        public float TickDt { get; }
        public int MaxHp { get; }
        public IReadOnlyList<KidSnapshot> Kids => _kids;
        /// <summary>True while the stand-in runs instead of the ported sim.</summary>
        public bool UsesStandIn { get; }

        /// <summary>(id, byId) when a kid is tagged; (team, how) when a point is scored. Raised during Step.</summary>
        public event Action<int, int> Tagged;
        public event Action<int, int> PointScored;

#if KALYEAH_SIM_PORTED
        readonly MatchSim _sim;
        readonly TeamCoach[] _coaches;

        public SimBridge(DataSource data, long seed, string localCharacter)
        {
            SimData simData = LoadSimData(data, "rules__game_rules", "rules__map_layout");
            GameRules rules = simData.GameRules;
            MapLayout layout = simData.MapLayout;
            TickDt = 1f / rules.TickRate;
            MaxHp = rules.PlayerMaxHp;
            _runPerTick = (float)rules.MoveSpeed * TickDt;
            _sim = new MatchSim(rules, layout, seed);
            _sim.PlayerTagged += (id, by) => Tagged?.Invoke(id, by);
            _sim.PointScored += (team, how) => PointScored?.Invoke(team, (int)how);
            // practice_match.gd _setup_bot_match: you, four allies backing you up, five enemies
            _sim.AddPlayer(LocalId, LocalTeam);
            var allies = new TeamCoach(LocalTeam, LocalId);
            var foes = new TeamCoach(1 - LocalTeam);
            foreach (int id in AllyIds) { _sim.AddPlayer(id, LocalTeam); allies.Add(_sim, id, rules.BotSkill); }
            foreach (int id in EnemyIds) { _sim.AddPlayer(id, 1 - LocalTeam); foes.Add(_sim, id, rules.BotSkill); }
            _coaches = new[] { allies, foes };
            AssignUniqueRoster(_sim, rules, localCharacter);
            _sim.Equip(LocalId);
            foreach (int id in AllyIds) _sim.Equip(id, true);
            foreach (int id in EnemyIds) _sim.Equip(id, true);
            _sim.BeginIntro();
            UsesStandIn = false;
            Refresh();
        }

        /// <summary>The sim's data: the named files and every file they name through a `$ref`, from the game's copy of unity/Data.</summary>
        static SimData LoadSimData(DataSource data, params string[] roots)
        {
            var simData = new SimData();
            var pending = new Queue<string>(roots);
            var seen = new HashSet<string>();
            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (!seen.Add(name)) continue;
                string text = data.Text(name);
                simData.AddFile(text);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"\\$ref\"\\s*:\\s*\"(res://data/[^\"]+)\""))
                    pending.Enqueue(DataSource.NameOfResPath(m.Groups[1].Value));
            }
            return simData;
        }

        /// <summary>practice_match.gd _assign_unique_roster: ten different kids drawn from the match's rng, yours first (the same draws as the
        /// Godot game, so a Unity match follows the Godot one tick for tick; checked by the bot golden replays).</summary>
        static void AssignUniqueRoster(MatchSim sim, GameRules rules, string avatar)
        {
            if (rules.Characters.Count < sim.Players.Count || string.IsNullOrEmpty(avatar))
            {
                sim.AssignCharacters();
                if (!string.IsNullOrEmpty(avatar)) sim.SetCharacter(LocalId, avatar);
                return;
            }
            var pool = new List<string>();
            foreach (CharacterDef c in rules.Characters) pool.Add(c.Id);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = sim.Rng.RandiRange(0, i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int wanted = pool.IndexOf(avatar);
            if (wanted > 0) { pool[wanted] = pool[0]; pool[0] = avatar; }
            int cursor = 0;
            foreach (var pair in sim.Players) pair.Value.CharacterId = pool[cursor++];
        }

        public int Tick => _sim.Tick;
        public float ClockSeconds => (float)_sim.MatchTime;
        public int ScoreOf(int team) => _sim.Score.Points[team];

        /// <summary>One fixed tick: the bots decide, your input goes in, the sim steps.</summary>
        public void Step()
        {
            foreach (TeamCoach coach in _coaches) coach.Drive(_sim);
            _sim.SetInput(LocalId, PlayerInput.Create(new Vec2(_move.X, _move.Y), Vec2.Zero, _buttons, _sim.Tick));
            _sim.Step(TickDt);
            Refresh();
        }

        void Refresh()
        {
            _kids.Clear();
            foreach (var pair in _sim.Players)
            {
                PlayerState p = pair.Value;
                _kids.Add(Snapshot(p.Id, p.Team, p.CharacterId, new Vector2(p.Position.X, p.Position.Y), new Vector2(p.Facing.X, p.Facing.Y), p.Hp, p.Tagged));
            }
        }
#else
        readonly StandInMatch _sim;

        public SimBridge(DataSource data, long seed, string localCharacter)
        {
            var rules = data.Props("rules__game_rules");
            TickDt = 1f / DataSource.I(rules, "tick_rate");
            _runPerTick = DataSource.F(rules, "move_speed") * TickDt;
            _sim = new StandInMatch(data, MapLayoutData.Load(data), seed);
            MaxHp = _sim.MaxHp;
            _sim.AddPlayer(LocalId, LocalTeam);
            foreach (int id in AllyIds) _sim.AddPlayer(id, LocalTeam);
            foreach (int id in EnemyIds) _sim.AddPlayer(id, 1 - LocalTeam);
            _sim.AssignCharacters(LocalId, localCharacter);
            UsesStandIn = true;
            Refresh();
            _ = Tagged;
            _ = PointScored;
        }

        public int Tick => _sim.Tick;
        public float ClockSeconds => _sim.MatchTime;
        public int ScoreOf(int team) => _sim.Points[team];

        public void Step()
        {
            _sim.SetMove(LocalId, _move);
            _sim.Step(TickDt);
            Refresh();
        }

        void Refresh()
        {
            _kids.Clear();
            foreach (StandInMatch.Kid k in _sim.Kids.Values)
                _kids.Add(Snapshot(k.Id, k.Team, k.CharacterId, k.Position, k.Facing, k.Hp, false));
        }
#endif

        /// <summary>
        /// Your input for the next ticks: `move` is a way on the ground (sim XZ, length 0..1; GodotSpace.GroundWay turns the joystick into
        /// it), `buttons` the PlayerInput bit mask (0 for now: the card arc is not ported yet).
        /// </summary>
        public void SetLocalInput(Vector2 move, int buttons)
        {
            _move = move;
            _buttons = buttons;
        }

        public KidSnapshot? Find(int id)
        {
            foreach (KidSnapshot kid in _kids)
                if (kid.Id == id) return kid;
            return null;
        }

        KidSnapshot Snapshot(int id, int team, string character, Vector2 position, Vector2 facing, int hp, bool tagged)
        {
            float speed = 0f;
            if (_lastPositions.TryGetValue(id, out Vector2 last) && _runPerTick > 0f)
                speed = Math.Clamp(Vector2.Distance(position, last) / _runPerTick, 0f, 1f);
            _lastPositions[id] = position;
            return new KidSnapshot
            {
                Id = id, Team = team, CharacterId = character ?? "", Position = position, Facing = facing, Hp = hp, Tagged = tagged, Speed01 = speed,
            };
        }
    }
}
