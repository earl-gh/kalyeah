using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Plays the bots of a street game in Practice (port of scripts/sim/street_mode_coach.gd) by role. Bots walk with the stick and never cast
    /// cards. Pure data in, input out, with the match's seeded rng, so it stays deterministic. Call <see cref="Drive"/> before every
    /// <c>sim.Step</c>.
    /// - Tumbang Preso thrower: waits a moment, holds the Throw button aimed at the can until the line reaches it and lets go; then fetches
    ///   its slipper when the can is down or the taya is far from it (or its scared timer is nearly out), kicks the can when it stands
    ///   beside it, and runs for the other end, away from the taya.
    /// - Tumbang Preso taya: fetches the fallen can and carries it back; otherwise chases a thrower out in the fetch area, else waits by the can.
    /// - Patintero runner: runs for the other end and back, crossing the middle line once on the way, sidestepping the guard of the next
    ///   horizontal line until it has room to pass. Patintero guard: follows the nearest runner along its own line. Both dive when it pays.
    /// - Langit Lupa kid: rests on the street while the taya is far; when it comes near, runs for the standing platform with the most room, or
    ///   raises a mound when the taya is nearly on it; steps down once its safe time runs low. Langit Lupa taya: chases the nearest kid it can
    ///   see that is not safe, else the nearest kid it can see, else walks to the middle.
    /// </summary>
    public sealed class StreetModeCoach
    {
        /// <summary>A runner this close (m, along the street) to the next line sidesteps its guard instead of running on.</summary>
        public const double LineCaution = 4.0;
        /// <summary>... while that guard is less than this far to the side (m).</summary>
        public const double SidestepRoom = 3.0;
        /// <summary>A stick this close to its target counts as there.</summary>
        public const double Arrived = 0.2;
        /// <summary>The taya chases throwers this close to the can (m).</summary>
        public const double ChaseRange = 24.0;
        /// <summary>A runner keeps this far from the middle (m) where the vertical guard stands.</summary>
        public const double MiddleRoom = 5.0;
        /// <summary>The sidestep lane of a runner is this far (m) in from the street's edge.</summary>
        public const double LaneMargin = 18.0;
        /// <summary>A Langit Lupa kid steps down from a langit, and stops looking for one, below this share of its safe time.</summary>
        public const double LowSafeShare = 0.35;
        /// <summary>... and raises a mound when the taya is this close (m).</summary>
        public const double MoundRange = 2.5;

        public List<int> Ids = new List<int>();

        /// <summary>Seconds a Tumbang Preso thrower still waits before it aims (absent: not waiting yet).</summary>
        readonly OrderedDict<int, double> _wait = new OrderedDict<int, double>();
        /// <summary>A Tumbang Preso thrower that is aiming: the way it aims, and the seconds it holds the button for.</summary>
        readonly OrderedDict<int, (Vec2 way, double seconds)> _aim = new OrderedDict<int, (Vec2 way, double seconds)>();
        /// <summary>The side (-1 or 1) a Patintero runner sidesteps the guard ahead to, kept until it is past.</summary>
        readonly OrderedDict<int, double> _side = new OrderedDict<int, double>();

        public void Add(int id) => Ids.Add(id);

        /// <summary>Sets the input of every free bot for this tick. Call before `sim.Step`.</summary>
        public void Drive(MatchSim sim)
        {
            if (sim.Phase != MatchSim.MatchPhase.Playing || sim.Mode == null) return;
            foreach (int id in Ids)
            {
                PlayerState state = sim.Players[id];
                if (!state.Tagged) sim.SetInput(id, Think(sim, state));
            }
        }

        public PlayerInput Think(MatchSim sim, PlayerState state)
        {
            switch (sim.Mode)
            {
                case TumbangPreso game:
                    return state.Id == game.TayaId ? It(sim, game, state) : Thrower(sim, game, state);
                case Patintero lines:
                    return lines.IsAttacker(state) ? Runner(sim, lines, state) : LineGuard(sim, lines, state);
                case LangitLupa langit:
                    return state.Id == langit.TayaId ? Chaser(sim, langit, state) : Climber(sim, langit, state);
            }
            return Stick(Vec2.Zero);
        }

        // ---- Tumbang Preso -------------------------------------------------------------------------

        PlayerInput Thrower(MatchSim sim, TumbangPreso game, PlayerState state)
        {
            int id = state.Id;
            PlayerState taya = sim.Players[game.TayaId];
            if (game.Flights.ContainsKey(id)) return Stick(Vec2.Zero);
            if (game.Fetched.ContainsKey(id))
            {
                _aim.Remove(id);
                return Stick(AwayFrom(state.Position, taya.Position, new Vec2(state.Position.x, game.Rules.GoalZ - 2.0), game.Rules.BotSafeDistance));
            }
            if (game.HasSlipper(id))
            {
                if (!game.AtHome(state.Position)) return Stick(Toward(state.Position, new Vec2(state.Position.x, game.Rules.HomeZ + 1.0)));
                return AimAndThrow(sim, game, state);
            }
            _aim.Remove(id);
            if (game.SlipperBusy.GetOrDefault(id, 0.0) > 0.0 || !game.Slippers.ContainsKey(id)) return Stick(Vec2.Zero);
            Vec2 slipper = game.Slippers[id];
            bool safe = !game.CanUp || taya.Position.DistanceTo(slipper) > game.Rules.BotSafeDistance;
            bool rush = game.ScaredLeft.GetOrDefault(id, GMath.Inf) <= game.Rules.BotRushSeconds;
            if (game.CanKick(state) && game.CanUp) return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, 0);
            if (safe || rush) return Stick(Toward(state.Position, slipper));
            return Stick(Vec2.Zero);
        }

        /// <summary>Waits a moment, then holds the Throw button aimed at the can until the line is long enough, then lets go.</summary>
        PlayerInput AimAndThrow(MatchSim sim, TumbangPreso game, PlayerState state)
        {
            int id = state.Id;
            if (!_aim.ContainsKey(id))
            {
                if (!_wait.ContainsKey(id)) _wait[id] = sim.Rng.RandfRange(0.0f, (float)game.Rules.BotThrowWait);
                _wait[id] -= sim.Rules.TickDt();
                if (_wait[id] > 0.0) return Stick(Vec2.Zero);
                _wait.Remove(id);
                double error = game.Rules.BotThrowError;
                double ex = sim.Rng.RandfRange((float)-error, (float)error);
                double ey = sim.Rng.RandfRange((float)-error, (float)error);
                Vec2 target = game.CanPosition + new Vec2(ex, ey);
                double distance = state.Position.DistanceTo(target);
                double span = game.Rules.ThrowMaxRange - game.Rules.ThrowMinRange;
                double power = span > 0.0 ? GMath.Clampf((distance + game.Rules.BotThrowMargin - game.Rules.ThrowMinRange) / span, 0.0, 1.0) : 1.0;
                _aim[id] = ((target - state.Position).Normalized(), power * game.Rules.ThrowChargeTime);
            }
            var plan = _aim[id];
            if (game.Hold.GetOrDefault(id, 0.0) < plan.seconds) return PlayerInput.Create(Vec2.Zero, plan.way, PlayerInput.BtnCard1, 0);
            _aim.Remove(id);
            return Stick(Vec2.Zero);  // let go: the slipper flies
        }

        PlayerInput It(MatchSim sim, TumbangPreso game, PlayerState state)
        {
            double speed = game.Rules.BotGuardSpeed;
            if (!game.CanUp)
            {
                if (game.CanBusyLeft > 0.0) return Stick(Vec2.Zero);
                if (game.CanCarrier == state.Id) return Stick(Toward(state.Position, game.Rules.CanSpot));
                return Stick(Toward(state.Position, game.CanPosition) * speed);
            }
            PlayerState best = null;
            foreach (PlayerState other in sim.Players.Values)
            {
                if (other.Id != state.Id && !game.AtHome(other.Position) && !game.AtGoal(other.Position)
                    && other.Position.DistanceTo(game.CanPosition) <= ChaseRange
                    && (best == null || other.Position.DistanceTo(state.Position) < best.Position.DistanceTo(state.Position)))
                    best = other;
            }
            Vec2 target = best != null ? best.Position : game.Rules.CanSpot - new Vec2(0.0, game.Rules.GuardOffset);
            return Stick(Toward(state.Position, target) * speed);
        }

        // ---- Patintero -----------------------------------------------------------------------------

        PlayerInput Runner(MatchSim sim, Patintero lines, PlayerState state)
        {
            if (lines.IsTaggedOut(state.Id)) return Stick(Vec2.Zero);  // a tagged runner waits on its sidewalk
            double laneLimit = sim.Layout.LaneWidth / 2.0 - LaneMargin;
            bool goingHome = lines.Crossed.ContainsKey(state.Id);
            double targetZ = goingHome ? lines.Rules.HomeZ + 1.5 : lines.Rules.GoalZ - 1.5;
            double way = Signf(targetZ - state.Position.y);
            // between the horizontal lines the vertical line is crossed once before the far end counts
            if (!goingHome && !lines.MiddleCrossed.ContainsKey(state.Id) && System.Math.Abs(state.Position.y) < lines.Rules.LineSpan)
            {
                double over = System.Math.Abs(state.Position.x) > 0.05 ? -Signf(state.Position.x) : 1.0;
                return Stick(new Vec2(over, way * 0.5).Normalized());
            }
            // the guard of the next horizontal line ahead
            PlayerState ahead = null;
            double gap = GMath.Inf;
            foreach (var pair in lines.GuardLine)
            {
                int id = pair.Key;
                if (lines.GuardAxis[id] != Patintero.Axis.Horizontal || !sim.Players.ContainsKey(id) || sim.Players[id].Tagged) continue;
                double along = (pair.Value - state.Position.y) * way;
                if (along > 0.0 && along < gap)
                {
                    gap = along;
                    ahead = sim.Players[id];
                }
            }
            if (ahead == null || gap >= LineCaution)
            {
                _side.Remove(state.Id);
                return Stick(KeepOffTheMiddle(lines, state, new Vec2(0.0, way)));
            }
            double dx = state.Position.x - ahead.Position.x;
            if (!_side.ContainsKey(state.Id))
            {
                // toward the open side of the street: the side of the guard with more room
                _side[state.Id] = System.Math.Abs(ahead.Position.x) > 1.0 ? -Signf(ahead.Position.x) : (dx != 0.0 ? Signf(dx) : 1.0);
            }
            double side = _side[state.Id];
            double lane = side * laneLimit;
            double across = GMath.Clampf(lane - state.Position.x, -1.0, 1.0);
            if (System.Math.Abs(dx) < SidestepRoom || Signf(dx) != side) return Stick(new Vec2(across, -way * 0.2).Normalized());
            return Stick(new Vec2(across * 0.5, way).Normalized(), gap < lines.Rules.BotDiveRange && lines.CanDive(state));
        }

        /// <summary>A runner between the horizontal lines leans away from the middle, where the vertical guard stands.</summary>
        static Vec2 KeepOffTheMiddle(Patintero lines, PlayerState state, Vec2 way)
        {
            if (System.Math.Abs(state.Position.y) > lines.Rules.LineSpan + 1.0 || System.Math.Abs(state.Position.x) >= MiddleRoom) return way;
            double side = state.Position.x != 0.0 ? Signf(state.Position.x) : 1.0;
            return new Vec2(side * 0.7, way.y).Normalized();
        }

        PlayerInput LineGuard(MatchSim sim, Patintero lines, PlayerState state)
        {
            bool vertical = lines.GuardAxis.GetOrDefault(state.Id, Patintero.Axis.Horizontal) == Patintero.Axis.Vertical;
            double line = lines.GuardLine.GetOrDefault(state.Id, 0.0);
            PlayerState best = null;
            double bestDistance = GMath.Inf;
            foreach (PlayerState other in sim.Players.Values)
            {
                if (!lines.IsAttacker(other) || other.Tagged || lines.IsTaggedOut(other.Id)) continue;
                double distance = vertical ? System.Math.Abs(other.Position.x - line) : System.Math.Abs(other.Position.y - line);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }
            if (best == null) return Stick(Vec2.Zero);
            double offset = vertical ? best.Position.y - state.Position.y : best.Position.x - state.Position.x;
            if (System.Math.Abs(offset) < Arrived) return Stick(Vec2.Zero);
            double move = Signf(offset) * GMath.Minf(1.0, System.Math.Abs(offset)) * lines.Rules.BotGuardSpeed;
            Vec2 toRunner = best.Position - state.Position;
            bool dive = toRunner.Length() > lines.Rules.TagCircleRadius && toRunner.Length() < lines.Rules.BotDiveRange * 2.0
                && lines.IsTaggable(best) && lines.CanDive(state);
            if (dive) return Stick(toRunner.Normalized(), true);  // a dive is the one thing that takes a guard off its line
            return Stick(vertical ? new Vec2(0.0, move) : new Vec2(move, 0.0));
        }

        // ---- Langit Lupa --------------------------------------------------------------------------

        PlayerInput Climber(MatchSim sim, LangitLupa game, PlayerState state)
        {
            PlayerState taya = sim.Players.GetOrDefault(game.TayaId);
            bool seen = taya != null && game.VisibleTo(sim, state.Id, taya.Id);
            double distance = seen ? state.Position.DistanceTo(taya.Position) : GMath.Inf;
            bool low = game.SafeShare(state.Id) < LowSafeShare;
            if (game.IsSafe(state) && !low) return Stick(Vec2.Zero);  // safe up here for now
            if (game.OnLangit(state))
            {
                Vec2 away = seen ? state.Position + (state.Position - taya.Position).Normalized() * 3.0 : state.Position + new Vec2(0.0, 2.0);
                return Stick(Toward(state.Position, sim.Layout.KeepInStreet(away, state.Radius)));  // step down before the time is up
            }
            if (distance >= game.Rules.BotSafeDistance) return Stick(Vec2.Zero);  // rest on the street (only the street refills stamina)
            if (distance < MoundRange && game.CanRaise(state) && !low) return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, 0);
            Vec2 target = state.Position + (state.Position - taya.Position).Normalized() * game.Rules.BotSafeDistance;
            if (!low)
            {
                double best = double.NegativeInfinity;
                for (int index = 0; index < game.Rules.LangitSpots.Count; index++)
                {
                    if (!game.SpotStanding(index)) continue;
                    Vec2 spot = game.Rules.LangitSpots[index].GetCenter();
                    double room = spot.DistanceTo(taya.Position) - spot.DistanceTo(state.Position) * 1.5;
                    if (room > best)
                    {
                        best = room;
                        target = spot;
                    }
                }
            }
            return Stick(AwayFrom(state.Position, taya.Position, sim.Layout.KeepInStreet(target, state.Radius), game.Rules.BotSafeDistance));
        }

        PlayerInput Chaser(MatchSim sim, LangitLupa game, PlayerState state)
        {
            PlayerState prey = null;
            PlayerState near = null;
            foreach (PlayerState other in sim.Players.Values)
            {
                if (other.Id == state.Id || !game.VisibleTo(sim, state.Id, other.Id)) continue;
                double distance = other.Position.DistanceTo(state.Position);
                if (near == null || distance < near.Position.DistanceTo(state.Position)) near = other;
                if (game.IsTaggable(other) && (prey == null || distance < prey.Position.DistanceTo(state.Position))) prey = other;
            }
            Vec2 target = Vec2.Zero;
            if (prey != null) target = prey.Position;
            else if (near != null) target = near.Position;
            return Stick(Toward(state.Position, target));
        }

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>Toward `target`, leaning away from `danger` while it is within `room` m.</summary>
        static Vec2 AwayFrom(Vec2 from, Vec2 danger, Vec2 target, double room)
        {
            Vec2 way = Toward(from, target);
            Vec2 offset = from - danger;
            double distance = offset.Length();
            if (distance > 0.0 && distance < room) way += offset / distance * (1.0 - distance / room);
            return way.Normalized();
        }

        static Vec2 Toward(Vec2 from, Vec2 to)
        {
            Vec2 offset = to - from;
            return offset.Length() <= Arrived ? Vec2.Zero : offset.Normalized();
        }

        static PlayerInput Stick(Vec2 move, bool dive = false) => PlayerInput.Create(move, Vec2.Zero, dive ? PlayerInput.BtnCard2 : 0, 0);

        /// <summary>GDScript's signf (0 for 0).</summary>
        static double Signf(double v) => v > 0.0 ? 1.0 : (v < 0.0 ? -1.0 : 0.0);
    }
}
