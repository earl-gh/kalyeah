using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Plays one team of bots in a Practice or bot-filled match (port of scripts/sim/team_coach.gd). Every bot is a real player of the sim
    /// (it counts for a capture, can be tagged and rescued), driven by a TeamBrain, and the coach decides each tick whether the team
    /// attacks or defends: both teams wait `BotAssaultDelay` seconds, then attack in waves (`BotAssaultWindow` seconds of attack,
    /// `BotRestTime` back home), only while at least two thirds of them are free. A team with a human to follow also attacks whenever that
    /// human is free in the enemy half.
    /// Per tick it hands out jobs: a rescuer (the free bot nearest the own rescue area while a teammate is tagged), up to two guards (sent
    /// home while the own base is being captured during an attack), a pusher (on a team backing up a defending human, once at most a third
    /// of the enemies are free) and `BotBaseHolders` attackers that hold courtyard spots inside the enemy base while the rest screen outside.
    /// Use: <c>new TeamCoach(team, followId)</c>, <c>Add(sim, id, skill)</c> for each bot already added to the sim with
    /// <c>sim.AddPlayer</c>, then every tick <c>Drive(sim)</c> before <c>sim.Step(dt)</c>: it calls <c>sim.SetInput(id, ...)</c> for every
    /// free bot.
    /// </summary>
    public sealed class TeamCoach
    {
        public enum CoachPlan { Defend, Assault }

        /// <summary>Compact pentagon courtyard unit offsets around the enemy base centre, so 5 bots stay well inside the base.</summary>
        public static readonly Vec2[] CourtyardOffsets =
        {
            new Vec2(0.0, -0.42), new Vec2(-0.72, -0.14), new Vec2(0.72, -0.14), new Vec2(-0.46, 0.62), new Vec2(0.46, 0.62),
        };
        /// <summary>Distance at which a vanguard screener cycles to its alternate patrol post so it never stands idle.</summary>
        public const double ScreenReach = 2.4;
        /// <summary>Maximum home-base defenders peeled off an assault wave on a 4-5 bot squad.</summary>
        public const int MaxSquadGuards = 2;

        public readonly int Team;
        public CoachPlan Plan = CoachPlan.Defend;
        /// <summary>The human this team backs up (-1: none, the team plans by itself).</summary>
        public int FollowId = -1;

        readonly OrderedDict<int, TeamBrain> _brains = new OrderedDict<int, TeamBrain>();
        List<int> _holders = new List<int>();
        readonly Dictionary<int, Vec2> _courtyardSpots = new Dictionary<int, Vec2>();
        readonly Dictionary<int, int> _screenPhase = new Dictionary<int, int>();

        /// <summary>The bots under this coach (id to brain), in the order they were added (GDScript `_brains`).</summary>
        public OrderedDict<int, TeamBrain> Brains => _brains;
        /// <summary>The attackers holding the enemy base, in the order they took the job (GDScript `_holders`).</summary>
        public List<int> Holders => _holders;

        public TeamCoach(int team, int followId = -1)
        {
            Team = team;
            FollowId = followId;
        }

        /// <summary>Puts `id` (already added to the sim) under this coach, with its courtyard spot inside the enemy base.</summary>
        public void Add(MatchSim sim, int id, double skill)
        {
            PlayerState state = sim.Players[id];
            int enemySide = -sim.SideForTeam(Team);
            int slot = _brains.Count;
            Vec2 spot = CourtyardSpot(sim, enemySide, slot);
            _courtyardSpots[id] = spot;
            _screenPhase[id] = slot % 2;
            TeamBrain brain = TeamBrain.Member(state.SpawnPosition, spot);
            brain.Skill = skill;
            _brains[id] = brain;
        }

        public List<int> Members() => _brains.KeyList();

        /// <summary>Decides the plan and sets the input of every free bot for this tick. Call before `sim.Step`.</summary>
        public void Drive(MatchSim sim)
        {
            if (sim.Phase != MatchSim.MatchPhase.Playing) return;
            Plan = Decide(sim);
            int rescuer = RescuerId(sim);
            List<int> guards = GuardIds(sim, rescuer);
            int pusher = PusherId(sim, rescuer);
            PickHolders(sim, rescuer, guards, pusher);
            bool enemiesFree = AnyFreeEnemy(sim);
            int vanguardSlot = 0;
            Vec2 fallbackSpot = sim.Layout.BaseCenter(-sim.SideForTeam(Team));
            foreach (var pair in _brains)
            {
                int id = pair.Key;
                PlayerState state = sim.Players[id];
                if (state.Tagged) continue;
                TeamBrain brain = pair.Value;
                bool attack = (Plan == CoachPlan.Assault && !guards.Contains(id)) || id == pusher;
                brain.Role = attack ? TeamBrain.BrainRole.Rush : TeamBrain.BrainRole.Defend;
                brain.RescueDuty = id == rescuer ? 1 : 0;
                brain.Hold = attack && _holders.Contains(id);
                if (brain.Hold || sim.Rules.BotBaseHolders <= 0 || !enemiesFree)
                    brain.Target = _courtyardSpots.TryGetValue(id, out Vec2 spot) ? spot : fallbackSpot;
                else if (attack)
                {
                    brain.Target = VanguardTarget(sim, state, vanguardSlot);
                    vanguardSlot += 1;
                }
                sim.SetInput(id, brain.Think(state, sim));
            }
        }

        /// <summary>Chooses which attackers hold the enemy base this tick: the ones already holding stay, then the free attackers nearest it.</summary>
        void PickHolders(MatchSim sim, int rescuer, List<int> guards, int pusher)
        {
            int wanted = sim.Rules.BotBaseHolders;
            var attackers = new List<int>();
            foreach (var pair in _brains)
            {
                int id = pair.Key;
                if (sim.Players[id].Tagged || id == rescuer || guards.Contains(id)) continue;
                if (Plan == CoachPlan.Assault || id == pusher) attackers.Add(id);
            }
            _holders = _holders.FindAll(attackers.Contains);
            Vec2 spot = sim.Layout.BaseCenter(-sim.SideForTeam(Team));
            InsertionSort(attackers, (a, b) => sim.Players[a].Position.DistanceSquaredTo(spot) < sim.Players[b].Position.DistanceSquaredTo(spot));
            foreach (int id in attackers)
            {
                if (_holders.Count >= wanted) break;
                if (!_holders.Contains(id)) _holders.Add(id);
            }
            while (_holders.Count > wanted) _holders.RemoveAt(_holders.Count - 1);
        }

        /// <summary>Godot's sort_custom for short arrays (at most 16 items: its final insertion sort), so ties keep their order the same way.</summary>
        static void InsertionSort(List<int> items, System.Func<int, int, bool> less)
        {
            for (int i = 1; i < items.Count; i++)
            {
                int value = items[i];
                int j = i;
                while (j > 0 && less(value, items[j - 1]))
                {
                    items[j] = items[j - 1];
                    j--;
                }
                items[j] = value;
            }
        }

        /// <summary>The bot that fetches a tagged teammate: the free one nearest the team's rescue area (-1: nobody is tagged, or no bot is free).</summary>
        public int RescuerId(MatchSim sim)
        {
            if (!sim.Objectives.TeammateWaiting(sim, Team)) return -1;
            return NearestFree(sim, sim.Layout.RescueRect(sim.SideForTeam(Team)).GetCenter(), -1);
        }

        /// <summary>Primary guard sent home while the other team captures this team's base during an attack (-1: none needed).</summary>
        public int GuardId(MatchSim sim, int rescuer)
        {
            List<int> guards = GuardIds(sim, rescuer);
            return guards.Count > 0 ? guards[0] : -1;
        }

        /// <summary>Up to 2 guards on 4-5 bot squads (1 on smaller ones) peel back to contest when the own base is being captured during an assault.</summary>
        public List<int> GuardIds(MatchSim sim, int rescuer)
        {
            var result = new List<int>();
            if (Plan != CoachPlan.Assault || sim.CaptureTeam != 1 - Team || sim.CurrentCaptureKind != MatchSim.CaptureKind.Base) return result;
            Vec2 home = sim.Layout.BaseCenter(sim.SideForTeam(Team));
            int first = NearestFree(sim, home, rescuer);
            if (first < 0) return result;
            result.Add(first);
            if (_brains.Count < 4) return result;
            int available = 0;
            foreach (var pair in _brains)
                if (!sim.Players[pair.Key].Tagged && pair.Key != rescuer) available += 1;
            if (available >= 3)
            {
                int second = NearestFree(sim, home, rescuer, first);
                if (second >= 0 && result.Count < MaxSquadGuards) result.Add(second);
            }
            return result;
        }

        /// <summary>The bot that attacks alone while a team backing up a human defends, once that is safe (-1: none).</summary>
        public int PusherId(MatchSim sim, int rescuer)
        {
            if (FollowId < 0 || Plan != CoachPlan.Defend) return -1;
            int enemies = 0;
            int free = 0;
            foreach (var pair in sim.Players)
            {
                PlayerState other = pair.Value;
                if (other.Team != Team)
                {
                    enemies += 1;
                    if (!other.Tagged) free += 1;
                }
            }
            if (enemies == 0 || free * 3 > enemies) return -1;
            return NearestFree(sim, sim.Layout.BaseCenter(-sim.SideForTeam(Team)), rescuer);
        }

        /// <summary>The free bot of this team nearest `point`, skipping `skip` and `secondSkip` (-1: none).</summary>
        int NearestFree(MatchSim sim, Vec2 point, int skip, int secondSkip = -1)
        {
            int best = -1;
            double bestDistance = GMath.Inf;
            foreach (var pair in _brains)
            {
                int id = pair.Key;
                PlayerState state = sim.Players[id];
                if (state.Tagged || id == skip || id == secondSkip) continue;
                double distance = state.Position.DistanceTo(point);
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public CoachPlan Decide(MatchSim sim)
        {
            if (FollowId >= 0 && sim.Players.ContainsKey(FollowId))
            {
                PlayerState human = sim.Players[FollowId];
                if (!human.Tagged && InEnemyHalf(sim, human)) return CoachPlan.Assault;
            }
            if (sim.RoundTime < sim.Rules.BotAssaultDelay) return CoachPlan.Defend;
            double window = sim.Rules.BotAssaultWindow;
            if (window > 0.0 && (sim.RoundTime - sim.Rules.BotAssaultDelay) % (window + sim.Rules.BotRestTime) >= window) return CoachPlan.Defend;
            int free = 0;
            foreach (var pair in _brains)
                if (!sim.Players[pair.Key].Tagged) free += 1;
            return free * 3 >= _brains.Count * 2 ? CoachPlan.Assault : CoachPlan.Defend;
        }

        /// <summary>The half of the street on the far side of this team's own base.</summary>
        bool InEnemyHalf(MatchSim sim, PlayerState state)
        {
            double ownZ = sim.Layout.BaseCenter(sim.SideForTeam(Team)).y;
            return state.Position.y * ownZ < 0.0;
        }

        /// <summary>Compact courtyard formation around the base centre of `side`, kept inside the base.</summary>
        static Vec2 CourtyardSpot(MatchSim sim, int side, int slot)
        {
            Vec2 center = sim.Layout.BaseCenter(side);
            double spread = GMath.Clampf(sim.Layout.BaseRadius * 0.32, 1.0, 3.8);
            Vec2 unit = CourtyardOffsets[slot % CourtyardOffsets.Length];
            Vec2 spot = center + new Vec2(unit.x, unit.y * (double)(-side)) * spread;
            double margin = GMath.Minf(GMath.Maxf(sim.Rules.PlayerRadius + 0.6, 1.0), sim.Layout.BaseRadius * 0.35);
            Rect2 safeRect = sim.Layout.BaseRect(side).Grow(-margin);
            if (safeRect.Size.x > 0.0 && safeRect.Size.y > 0.0) spot = spot.Clamp(safeRect.Position, safeRect.End);
            return spot;
        }

        /// <summary>Screening waypoint across the three pathways outside the enemy base (or a nearby active merienda stand).</summary>
        Vec2 VanguardTarget(MatchSim sim, PlayerState state, int laneSlot)
        {
            if (state.BuffLeft <= 1.5)
            {
                Vec2 stand = NearbyPowerupSpot(sim, state.Position, 18.0);
                if (stand.IsFinite()) return stand;
            }
            int enemySide = -sim.SideForTeam(Team);
            int phase = _screenPhase.TryGetValue(state.Id, out int p) ? p : 0;
            Vec2 goal = ScreenSpot(sim, enemySide, laneSlot, phase);
            if (state.Position.DistanceTo(goal) <= ScreenReach || state.StuckTime > 0.35)
            {
                phase = 1 - phase;
                _screenPhase[state.Id] = phase;
                goal = ScreenSpot(sim, enemySide, laneSlot, phase);
            }
            return goal;
        }

        static Vec2 NearbyPowerupSpot(MatchSim sim, Vec2 origin, double maxDist)
        {
            Vec2 best = Vec2.Inf;
            double bestDist = maxDist;
            foreach (MatchSim.Powerup entry in sim.Powerups)
            {
                if (!entry.Active) continue;
                double d = origin.DistanceTo(entry.Pos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = entry.Pos;
                }
            }
            return best;
        }

        static Vec2 ScreenSpot(MatchSim sim, int enemySide, int laneSlot, int phase)
        {
            MapLayout layout = sim.Layout;
            Vec2 baseCenter = layout.BaseCenter(enemySide);
            Vec2 inward = baseCenter.LengthSquared() > 0.001 ? (-baseCenter).Normalized() : new Vec2(0.0, -(double)enemySide);
            Vec2 across = new Vec2(-inward.y, inward.x);
            bool circular = layout.IsCircular();
            double depth = layout.BaseRadius + (circular ? (phase == 0 ? 8.0 : 16.0) : (phase == 0 ? 5.2 : 9.6));
            double maxLat = GMath.Maxf(layout.LaneWidth * 0.32, 2.5);
            double wideLat = circular ? 28.0 : 8.5;
            double midLat = circular ? 16.0 : 3.6;
            int lane = laneSlot % 3;
            double lateral;
            if (lane == 0) lateral = -GMath.Minf(phase == 0 ? wideLat : midLat, maxLat);
            else if (lane == 1) lateral = GMath.Minf(phase == 0 ? wideLat : midLat, maxLat);
            else lateral = GMath.Minf(3.2, maxLat) * (phase == 0 ? -1.0 : 1.0);
            Vec2 point = baseCenter + inward * depth + across * lateral;
            if (layout.InBase(enemySide, point)) point += inward * (layout.BaseRadius * 0.5 + 3.0);
            double clearance = sim.Rules.PlayerRadius + 0.8;
            point = layout.KeepInStreet(point, clearance);
            foreach (Rect2 rect in layout.WalkObstacleRects())
            {
                Rect2 padded = rect.Grow(clearance);
                if (!padded.HasPoint(point)) continue;
                Vec2 end = padded.End;
                double dLeft = GMath.Absf(point.x - padded.Position.x);
                double dRight = GMath.Absf(end.x - point.x);
                double dTop = GMath.Absf(point.y - padded.Position.y);
                double dBot = GMath.Absf(end.y - point.y);
                double best = GMath.Minf(GMath.Minf(dLeft, dRight), GMath.Minf(dTop, dBot));
                if (GMath.IsEqualApprox(best, dLeft)) point.x = padded.Position.x - 0.2;
                else if (GMath.IsEqualApprox(best, dRight)) point.x = end.x + 0.2;
                else if (GMath.IsEqualApprox(best, dTop)) point.y = padded.Position.y - 0.2;
                else point.y = end.y + 0.2;
            }
            return layout.KeepInStreet(point, clearance);
        }

        bool AnyFreeEnemy(MatchSim sim)
        {
            foreach (var pair in sim.Players)
                if (pair.Value.Team != Team && !pair.Value.Tagged) return true;
            return false;
        }
    }
}
