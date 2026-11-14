namespace Kalyeah.Sim
{
    /// <summary>
    /// A bot that plays a round of tag, not just a duel (port of scripts/sim/team_brain.gd), in this order: 1. tag enemies by killing them
    /// (anything within `BotAggroRange` is fought), 2. stay at the enemy base (a RUSHER walks in and holds it; a DEFENDER holds its own base
    /// and does not chase out of it), 3. rescue a tagged teammate (the lowest-numbered free one, and only once it stands at its post).
    /// Pure sim, deterministic for a seed; a TeamCoach sets <see cref="Role"/>, <see cref="Hold"/>, <see cref="Target"/> and RescueDuty.
    /// </summary>
    public class TeamBrain : FightBrain
    {
        /// <summary>A defender stays this far inside its base's edge (metres).</summary>
        public const double GuardMargin = 0.5;
        /// <summary>A rescuer waiting in its rescue area walks about this far inside its edge (metres).</summary>
        public const double RescuePatrolMargin = 0.8;
        /// <summary>Where a rusher that stepped out to fight waits for the next enemy: this far outside the enemy base's edge (metres).</summary>
        public const double SiegeGap = 2.0;

        public enum BrainRole { Rush, Defend }

        public BrainRole Role = BrainRole.Defend;
        /// <summary>Set by the coach on an attacker that stays in the enemy base for good (never steps out to fight).</summary>
        public bool Hold;
        /// <summary>Where a rusher goes: its place in the enemy base.</summary>
        public Vec2 Target = Vec2.Zero;

        public static TeamBrain Rusher(Vec2 spot, Vec2 enemyBaseSpot) =>
            new TeamBrain { Mode = BrainMode.Stand, Home = spot, Role = BrainRole.Rush, Target = enemyBaseSpot };

        /// <summary>A member of a squad that switches between attacking and defending (TeamCoach sets `Role`): it starts as a defender.</summary>
        public static TeamBrain Member(Vec2 spot, Vec2 enemyBaseSpot)
        {
            TeamBrain brain = Rusher(spot, enemyBaseSpot);
            brain.Role = BrainRole.Defend;
            return brain;
        }

        public static TeamBrain Defender(Vec2 spot) => new TeamBrain { Mode = BrainMode.Stand, Home = spot };

        public override PlayerInput Think(PlayerState state, MatchSim sim)
        {
            PlayerInput input = base.Think(state, sim);
            if (Role == BrainRole.Defend) return Guard(state, sim, input);
            return input;
        }

        /// <summary>
        /// No enemy in reach: a rusher goes to the enemy base and walks about in it, a defender walks about in its own, and only one already
        /// at its post sets off to rescue. Nobody stands still while there is something to do.
        /// </summary>
        protected override PlayerInput NoEnemy(PlayerState state, MatchSim sim)
        {
            int teamSide = sim.SideForTeam(state.Team);
            if (RescueDue(state, sim) && IsRescuer(state, sim))
            {
                Rect2 area = sim.Layout.RescueRect(teamSide);
                if (sim.Objectives.InRescueArea(sim, state)) return Patrol(state, sim, area, RescuePatrolMargin);
                return WalkToNav(state, sim, area.GetCenter());
            }
            if (Role == BrainRole.Rush)
            {
                int enemySide = -teamSide;
                if (_stayOutLeft > 0.0 && !Hold && !sim.Layout.InBase(enemySide, state.Position))
                    return Patrol(state, sim, SiegeArea(sim, enemySide), 0.0);
                if (sim.Layout.InBase(enemySide, state.Position))
                    return Patrol(state, sim, sim.Layout.BaseRect(enemySide), sim.Layout.BaseCorner);
                return WalkToNav(state, sim, Target);
            }
            if (sim.Layout.InBase(teamSide, state.Position))
                return Patrol(state, sim, sim.Layout.BaseRect(teamSide), sim.Layout.BaseCorner);
            return WalkToNav(state, sim, Home);
        }

        /// <summary>A patch of street just outside the enemy base, on the side toward the middle of the street.</summary>
        static Rect2 SiegeArea(MatchSim sim, int enemySide)
        {
            Rect2 rect = sim.Layout.BaseRect(enemySide);
            double toward = -(double)enemySide;
            double z = toward < 0.0 ? rect.Position.y - SiegeGap : rect.End.y + SiegeGap;
            return new Rect2(rect.Position.x, z - 1.0, rect.Size.x, 2.0).Intersection(sim.Layout.LaneRect());
        }

        /// <summary>A rusher is at its post in the enemy base, a defender in its own.</summary>
        protected override bool AtPost(PlayerState state, MatchSim sim)
        {
            int side = Role == BrainRole.Rush ? -sim.SideForTeam(state.Team) : sim.SideForTeam(state.Team);
            return sim.Layout.InBase(side, state.Position);
        }

        /// <summary>A defender fights from inside its own base and does not chase out of it: at the edge it slides along it instead.</summary>
        PlayerInput Guard(PlayerState state, MatchSim sim, PlayerInput input)
        {
            if (!input.IsPressed(PlayerInput.BtnMoveTarget) || sim.NearestEnemy(state, sim.Rules.BotAggroRange) == null) return input;
            Vec2 way = input.MoveTarget - state.Position;
            if (way.LengthSquared() <= 0.0) return input;
            Vec2 centre = sim.Layout.BaseCenter(sim.SideForTeam(state.Team));
            double edge = sim.Layout.BaseRadius - GuardMargin;
            Vec2 ahead = state.Position + way.Normalized() * 0.3;
            if (ahead.DistanceTo(centre) > edge && ahead.DistanceTo(centre) > state.Position.DistanceTo(centre))
            {
                Vec2 radial = (state.Position - centre).Normalized();
                Vec2 along = radial.Orthogonal();
                if (along.Dot(way) < 0.0) along = -along;
                input.MoveTarget = state.Position + along * SteerAhead;  // along the edge instead of stepping out
            }
            return input;
        }

        /// <summary>A base holder stays in the enemy base it is in, even with an enemy near (nothing can be cast there, but the capture runs).</summary>
        protected override bool HoldsZone(PlayerState state, MatchSim sim) =>
            Hold && Role == BrainRole.Rush && sim.Layout.InBase(-sim.SideForTeam(state.Team), state.Position);

        /// <summary>The team's rescuer fetches the tagged ones: the one its TeamCoach picked, or, deciding alone, the free teammate with the lowest id.</summary>
        bool IsRescuer(PlayerState state, MatchSim sim)
        {
            if (RescueDuty >= 0) return RescueDuty == 1;
            foreach (var pair in sim.Players)
            {
                PlayerState other = pair.Value;
                if (other.Team == state.Team && !other.Tagged && other.Id < state.Id) return false;
            }
            return true;
        }
    }
}
