using System;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Training-dummy behaviour for practice mode (port of dummy_brain.gd), and the base class of every bot brain (FightBrain, TeamBrain
    /// extend it and override <see cref="Think"/>). Produces a PlayerInput each tick; pure data in, input out.
    /// </summary>
    public class DummyBrain
    {
        public enum BrainMode { Stand, Patrol, Rescuer }

        /// <summary>A rescuer back within this many metres of its spot stands still.</summary>
        public const double HomeTolerance = 0.3;
        public const int UnpinTicks = 4;

        public BrainMode Mode = BrainMode.Stand;
        public double OriginX;
        public double HalfRange;
        /// <summary>Stick magnitude while patrolling (1.0 = full speed).</summary>
        public double SpeedScale = 1.0;
        /// <summary>Where a rescuer waits.</summary>
        public Vec2 Home = Vec2.Zero;

        double _direction = 1.0;
        BarangayNav _fallbackNav;
        MapLayout _fallbackLayout;
        Vec2 _prevPos = Vec2.Inf;
        int _pinnedTicks;
        double _nudgeSide = 1.0;

        /// <summary>True for a bot that fights back (FightBrain overrides it): MatchSim.IsFighter reads it.</summary>
        public virtual bool IsFighter => false;

        public static DummyBrain Standing() => new DummyBrain();

        public static DummyBrain Patrolling(double originX, double halfRange, double speedScale) =>
            new DummyBrain { Mode = BrainMode.Patrol, OriginX = originX, HalfRange = halfRange, SpeedScale = speedScale };

        /// <summary>Waits at `home` until a teammate is tagged, then fetches them.</summary>
        public static DummyBrain Rescuer(Vec2 home) => new DummyBrain { Mode = BrainMode.Rescuer, Home = home };

        public virtual PlayerInput Think(PlayerState state, MatchSim sim)
        {
            if (Mode == BrainMode.Rescuer) return Rescue(state, sim);
            if (Mode == BrainMode.Stand) return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0);
            if (state.Position.x >= OriginX + HalfRange) _direction = -1.0;
            else if (state.Position.x <= OriginX - HalfRange) _direction = 1.0;
            return PlayerInput.Create(new Vec2(_direction * SpeedScale, 0.0), Vec2.Zero, 0, 0);
        }

        PlayerInput Rescue(PlayerState state, MatchSim sim)
        {
            Vec2 target = Home;
            foreach (var pair in sim.Players)
            {
                PlayerState other = pair.Value;
                if (other.Team == state.Team && other.Tagged)
                {
                    target = sim.Layout.RescueRect(sim.SideForTeam(state.Team)).GetCenter();
                    break;
                }
            }
            Vec2 directOffset = target - state.Position;
            if (directOffset.Length() <= HomeTolerance)
            {
                _pinnedTicks = 0;
                _prevPos = state.Position;
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0);
            }
            _pinnedTicks = state.Position.DistanceSquaredTo(_prevPos) < 0.0001 ? _pinnedTicks + 1 : 0;
            _prevPos = state.Position;
            Vec2 waypoint = NextWaypoint(state, sim, target);
            Vec2 offset = waypoint - state.Position;
            if (offset.Length() <= 0.001) offset = directOffset;
            if (offset.Length() <= HomeTolerance && directOffset.Length() <= HomeTolerance) return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0);
            Vec2 dir = offset.Normalized();
            if (_pinnedTicks >= UnpinTicks)
            {
                dir = dir.Rotated(_nudgeSide * Math.PI * 0.45);
                _pinnedTicks = 0;
                _nudgeSide = -_nudgeSide;
            }
            return PlayerInput.Create(dir, Vec2.Zero, 0, 0);
        }

        Vec2 NextWaypoint(PlayerState state, MatchSim sim, Vec2 goal)
        {
            if (sim == null || sim.Layout == null) return goal;
            if (sim.Nav != null) return sim.Nav.NextWaypoint(state.Position, goal);
            if (_fallbackNav == null || _fallbackLayout != sim.Layout)
            {
                _fallbackLayout = sim.Layout;
                _fallbackNav = new BarangayNav(sim.Layout, state.Radius);
            }
            return _fallbackNav.NextWaypoint(state.Position, goal);
        }
    }

    /// <summary>
    /// The seam for the timed street games (Tumbang Preso, Patintero, Langit Lupa): the members MatchSim calls on scripts/sim/street_mode.gd.
    /// The StreetMode port implements this; with no street mode set (<see cref="MatchSim.Mode"/> null) MatchSim plays Agawang Base.
    /// </summary>
    public interface IStreetMode
    {
        /// <summary>The game's numbers.</summary>
        StreetModeRules Rules { get; }
        /// <summary>Seconds left in the match (0 = time is up).</summary>
        double TimeLeft { get; }
        /// <summary>Everyone to their places for the first turn (called once by MatchSim.StartMode).</summary>
        void Setup(MatchSim sim);
        /// <summary>One tick of the game's own rules (after the players and the weapons moved).</summary>
        void Step(MatchSim sim, double dt);
        /// <summary>The pause after a tag: everyone stands still.</summary>
        bool IsResetting();
        /// <summary>The game's own actions for one free player (instead of skills and cards).</summary>
        void HandleInput(MatchSim sim, PlayerState state, PlayerInput input, double dt);
        /// <summary>Keeps a player where the game allows it, after its move.</summary>
        void Constrain(MatchSim sim, PlayerState state);
        /// <summary>False where standing still never refills stamina (Langit Lupa's safe spots).</summary>
        bool CanRest(PlayerState state);
        /// <summary>The game's speed share for this player (the taya runs faster).</summary>
        double SpeedScale(PlayerState state);
        /// <summary>Where a move target sent by a player really goes in this game.</summary>
        Vec2 MovePoint(PlayerState state, Vec2 point);
    }
}
