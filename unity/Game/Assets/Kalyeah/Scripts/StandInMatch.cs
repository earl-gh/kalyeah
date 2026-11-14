#if !KALYEAH_SIM_PORTED
using System;
using System.Collections.Generic;
using System.Numerics;
using Kalyeah.Sim;

namespace Kalyeah.Game
{
    /// <summary>
    /// NOT THE GAME'S RULES. A throwaway stand-in that SimBridge drives while unity/Kalyeah.Sim has no MatchSim yet (the port is in
    /// progress), so the Unity view, camera, HUD and build can be checked now: ten kids at their spawn spots (the sim's own formula),
    /// yours walks with the joystick at move_speed, the bots wander the street with a seeded GodotRng, everyone is pushed out of the
    /// building parcels and kept inside the arena. Nobody casts, tags or scores. Compiled out once the port lands (KALYEAH_SIM_PORTED);
    /// delete this file then.
    /// </summary>
    internal sealed class StandInMatch
    {
        internal sealed class Kid
        {
            public int Id, Team, Hp;
            public string CharacterId = "";
            public Vector2 Position, Facing, Goal;
            public float Speed01, Wait;
        }

        readonly GodotRng _rng;
        readonly MapLayoutData _layout;
        readonly float _moveSpeed, _radius, _spacing;
        readonly int _maxHp;
        readonly string[] _characters;
        readonly GroundRect[] _parcels;
        readonly Dictionary<int, int> _teamCounts = new Dictionary<int, int>();
        readonly Dictionary<int, Vector2> _inputs = new Dictionary<int, Vector2>();

        public readonly SortedDictionary<int, Kid> Kids = new SortedDictionary<int, Kid>();
        public int Tick;
        public float MatchTime;
        public readonly int[] Points = { 0, 0 };

        public StandInMatch(DataSource data, MapLayoutData layout, long seed)
        {
            var rules = data.Props("rules__game_rules");
            _moveSpeed = DataSource.F(rules, "move_speed");
            _radius = DataSource.F(rules, "player_radius");
            _spacing = DataSource.F(rules, "spawn_spacing");
            _maxHp = DataSource.I(rules, "player_max_hp");
            _characters = data.CharacterIds();
            _layout = layout;
            _parcels = new GroundRect[layout.ParcelCount];
            for (int i = 0; i < _parcels.Length; i++) _parcels[i] = layout.ParcelRect(i);
            _rng = new GodotRng(seed);
        }

        public int MaxHp => _maxHp;

        /// <summary>match_sim.gd add_player + _spawn_position: team 0 at the own (+Z) end.</summary>
        public void AddPlayer(int id, int team)
        {
            _teamCounts.TryGetValue(team, out int slot);
            _teamCounts[team] = slot + 1;
            int side = team == 0 ? MapLayoutData.SideOwn : MapLayoutData.SideEnemy;
            int offset = (slot + 1) / 2;
            int direction = slot % 2 == 1 ? 1 : -1;
            Vector2 spawn = _layout.SpawnCenter(side) + new Vector2(direction * offset * _spacing, 0f);
            Kids[id] = new Kid { Id = id, Team = team, Hp = _maxHp, Position = spawn, Goal = spawn, Facing = new Vector2(0f, -side) };
        }

        /// <summary>Ten different kids, yours the one asked for (practice_match.gd _assign_unique_roster).</summary>
        public void AssignCharacters(int localId, string wanted)
        {
            var pool = new List<string>(_characters);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _rng.RandiRange(0, i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int at = pool.IndexOf(wanted);
            if (at > 0) (pool[0], pool[at]) = (pool[at], pool[0]);
            int k = 1;
            foreach (Kid kid in Kids.Values)
                kid.CharacterId = pool.Count == 0 ? "" : kid.Id == localId ? pool[0] : pool[k++ % pool.Count];
        }

        public void SetMove(int id, Vector2 move) => _inputs[id] = move;

        public void Step(float dt)
        {
            Tick++;
            MatchTime += dt;
            foreach (Kid kid in Kids.Values)
            {
                Vector2 way;
                if (_inputs.TryGetValue(kid.Id, out Vector2 move))
                {
                    float len = move.Length();
                    way = len > 1f ? move / len : move;
                }
                else
                {
                    way = Wander(kid, dt);
                }
                kid.Speed01 = way.Length();
                if (kid.Speed01 > 0.001f)
                {
                    kid.Facing = Vector2.Normalize(way);
                    kid.Position = KeepOut(kid.Position + way * _moveSpeed * dt);
                }
            }
        }

        Vector2 Wander(Kid kid, float dt)
        {
            if (kid.Wait > 0f)
            {
                kid.Wait -= dt;
                return Vector2.Zero;
            }
            Vector2 to = kid.Goal - kid.Position;
            if (to.Length() < 1f)
            {
                float reach = _layout.ArenaRadius * 0.7f;
                kid.Goal = new Vector2(_rng.RandfRange(-reach, reach), _rng.RandfRange(-reach, reach));
                kid.Wait = _rng.RandfRange(0.5f, 2.5f);
                return Vector2.Zero;
            }
            return Vector2.Normalize(to) * 0.6f;
        }

        Vector2 KeepOut(Vector2 p)
        {
            foreach (GroundRect r in _parcels)
            {
                var grown = new GroundRect(r.X - _radius, r.Y - _radius, r.W + 2f * _radius, r.H + 2f * _radius);
                if (!grown.Contains(p)) continue;
                float left = p.X - grown.X, right = grown.X + grown.W - p.X, top = p.Y - grown.Y, bottom = grown.Y + grown.H - p.Y;
                float min = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
                if (min == left) p.X = grown.X;
                else if (min == right) p.X = grown.X + grown.W;
                else if (min == top) p.Y = grown.Y;
                else p.Y = grown.Y + grown.H;
            }
            float limit = MathF.Max(_layout.ArenaRadius - _radius, 0f);
            if (limit > 0f && p.LengthSquared() > limit * limit) p = Vector2.Normalize(p) * limit;
            return p;
        }
    }
}
#endif
