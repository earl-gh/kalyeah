using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A practice sparring partner (port of scripts/sim/fight_brain.gd): a training dummy that fights back. It keeps its spot until an
    /// enemy comes within `BotAggroRange`, then closes to a standoff distance, strafes, and taps its weapons the way a finger would (press
    /// one tick, release the next, aimed at the enemy with a little error). A hurt one heals itself, a free one fetches a tagged teammate
    /// when no enemy is close. Same PlayerInput as everyone else, no RNG of its own (the match's seeded one for the aim error).
    /// Priorities: 1. tag enemies by killing them, 2. stay at its post, 3. rescue a tagged teammate, only from its post.
    /// It plays by the players' rules: it walks by move targets and casts only the cards it can afford.
    /// </summary>
    public class FightBrain : DummyBrain
    {
        /// <summary>Below this share of health a bot heals itself, and an enemy this hurt is worth going after first.</summary>
        public const double HealBelow = 0.5;
        /// <summary>A rescuer sets off this many seconds before its rescue area would be ready on its arrival.</summary>
        public const double RescueEarly = 1.0;
        /// <summary>Closest and farthest a bot stands from an enemy, whatever its weapons reach.</summary>
        public const double StandoffMin = 4.5;
        public const double StandoffMax = 14.5;
        /// <summary>Backs off when the enemy is closer than the standoff by more than this.</summary>
        public const double TooCloseSlack = 1.0;
        /// <summary>Seconds between changes of strafing side.</summary>
        public const double StrafeSwitch = 1.5;
        /// <summary>At full skill the aim error is this share of `BotAimErrorDegrees`.</summary>
        public const double SkilledErrorShare = 0.3;
        /// <summary>How far to the side (metres) a bot looks for a spot with a clear line, nearest first.</summary>
        public static readonly double[] Sidesteps = { 2.0, 4.0, 6.0, 8.0 };
        /// <summary>A projectile is blocked by cover this much bigger than the cover (its own width).</summary>
        public const double CoverMargin = 0.3;
        /// <summary>While fighting it steers by putting its move target this far ahead in the way it wants to go.</summary>
        public const double SteerAhead = 1.0;
        /// <summary>A bot that waits at its post walks between spots of the post's area, new spot once it is this close to the last.</summary>
        public const double PatrolReach = 0.6;
        /// <summary>Where a fight does not enter a place nothing can be cast in: it looks this far ahead (metres).</summary>
        public const double EntryLookahead = 0.8;
        /// <summary>A fight move that has not moved the bot for this many ticks is turned a quarter to the side.</summary>
        public const int PinnedTicks = 4;
        /// <summary>After stepping out of a place nothing can be cast in to fight, it stays out this many seconds after its last enemy left.</summary>
        public const double ReenterDelay = 2.0;

        static readonly double[] QuarterTurns = { GMath.Pi / 2.0, -GMath.Pi / 2.0 };
        static readonly double[] BothWays = { 1.0, -1.0 };

        /// <summary>Aim quality 0 to 1; -1 takes `BotSkill` from the rules.</summary>
        public double Skill = -1.0;
        /// <summary>Who rescues: -1 the bot decides alone; set by a TeamCoach each tick, 1 = this bot is the team's rescuer, 0 = another is.</summary>
        public int RescueDuty = -1;

        int _pressSlot = -1;
        /// <summary>True once a bot standing at its post has set off to rescue a tagged teammate.</summary>
        protected bool _rescuing;
        Vec2 _aim = Vec2.Zero;
        Vec2 _aimPoint = Vec2.Zero;
        bool _tapRelease;
        Vec2 _patrolSpot = Vec2.Inf;
        Vec2 _lastPosition = Vec2.Inf;
        int _pinned;
        double _turn = 1.0;
        /// <summary>Seconds left in which it does not walk back into a place nothing can be cast in.</summary>
        protected double _stayOutLeft;
        double _cooldown;
        readonly Dictionary<int, Vec2> _seen = new Dictionary<int, Vec2>();
        readonly Dictionary<int, Vec2> _velocity = new Dictionary<int, Vec2>();

        public override bool IsFighter => true;

        /// <summary>A fighter that waits at `spot`.</summary>
        public static FightBrain Fighter(Vec2 spot) => new FightBrain { Mode = BrainMode.Stand, Home = spot };

        public override PlayerInput Think(PlayerState state, MatchSim sim)
        {
            _cooldown = GMath.Maxf(0.0, _cooldown - sim.Rules.TickDt());
            _stayOutLeft = GMath.Maxf(0.0, _stayOutLeft - sim.Rules.TickDt());
            _pinned = state.Position.DistanceSquaredTo(_lastPosition) < 0.0001 ? _pinned + 1 : 0;
            _lastPosition = state.Position;
            Track(state, sim);
            if (_pressSlot >= 0)
            {
                // the second half of a tap: let go, at the aimed point (or as a tap)
                _pressSlot = -1;
                PlayerInput release = PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick);
                return _tapRelease ? release : release.WithAimPoint(_aimPoint);
            }
            if (RescueDuty == 1 && RescueDue(state, sim) && sim.Objectives.InRescueArea(sim, state))
                return NoEnemy(state, sim);  // nothing can be cast here while a teammate waits: it holds the rescue
            PlayerState enemy = PickEnemy(state, sim, EngageRange(sim));
            if (enemy == null) return NoEnemy(state, sim);
            if (RescueDuty != 1) _rescuing = false;
            return Fight(state, sim, enemy);
        }

        /// <summary>How near an enemy must be to be fought: `BotAggroRange`, or only `BotRescueAggroRange` for the coach's rescuer on its way.</summary>
        double EngageRange(MatchSim sim)
        {
            if (RescueDuty == 1 && _rescuing) return GMath.Minf(sim.Rules.BotRescueAggroRange, sim.Rules.BotAggroRange);
            return sim.Rules.BotAggroRange;
        }

        /// <summary>The enemy within `reach` to fight: the one taking this team's base, then the one rescuing, then a hurt one; nearest within each.</summary>
        public PlayerState PickEnemy(PlayerState state, MatchSim sim, double reach)
        {
            PlayerState best = null;
            int bestRank = 4;
            double bestDistance = GMath.Inf;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
            {
                double distance = state.Position.DistanceTo(enemy.Position);
                if (distance > reach) continue;
                int rank = ThreatRank(state, enemy, sim);
                if (rank < bestRank || (rank == bestRank && distance < bestDistance))
                {
                    best = enemy;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>0: in this team's base, 1: rescuing a teammate of theirs, 2: below `HealBelow` of its health, 3: anyone else.</summary>
        int ThreatRank(PlayerState state, PlayerState enemy, MatchSim sim)
        {
            if (sim.Layout.InBase(sim.SideForTeam(state.Team), enemy.Position)) return 0;
            if (sim.RescueRescuer[enemy.Team] == enemy.Id) return 1;
            if ((double)enemy.Hp < (double)sim.Rules.PlayerMaxHp * HealBelow) return 2;
            return 3;
        }

        /// <summary>Stands where it is (a move target on itself ends any walk).</summary>
        protected PlayerInput Stop(PlayerState state, MatchSim sim) =>
            PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(state.Position);

        /// <summary>Walks the way of `direction` (zero: stands), by a move target `SteerAhead` in front of it.</summary>
        protected PlayerInput Steer(PlayerState state, MatchSim sim, Vec2 direction)
        {
            if (direction.LengthSquared() <= 0.0) return Stop(state, sim);
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(state.Position + direction.Normalized() * SteerAhead);
        }

        /// <summary>No enemy in reach: stay at the post, and rescue only from there.</summary>
        protected virtual PlayerInput NoEnemy(PlayerState state, MatchSim sim)
        {
            if (RescueDue(state, sim)) return WalkToNav(state, sim, sim.Layout.RescueRect(sim.SideForTeam(state.Team)).GetCenter());
            return WalkToNav(state, sim, Home);
        }

        /// <summary>True while this bot should be off rescuing: a teammate is tagged and the bot is already on its way or at its post.</summary>
        protected bool RescueDue(PlayerState state, MatchSim sim)
        {
            if (!TeammateTagged(state, sim) || RescueDuty == 0)
            {
                _rescuing = false;
                return false;
            }
            if (!_rescuing && (RescueDuty == 1 ? ReadyOnArrival(state, sim) : AtPost(state, sim))) _rescuing = true;
            return _rescuing;
        }

        /// <summary>The team's rescue area will be ready by the time this bot walks there, give or take `RescueEarly`.</summary>
        bool ReadyOnArrival(PlayerState state, MatchSim sim)
        {
            Vec2 area = sim.Layout.RescueRect(sim.SideForTeam(state.Team)).GetCenter();
            double walk = state.Position.DistanceTo(area) / GMath.Maxf(sim.Rules.MoveSpeed, 0.001);
            return sim.RescueReadyLeft[state.Team] <= walk + RescueEarly;
        }

        /// <summary>Whether the bot stands where it is meant to stay (a fighter: its spot).</summary>
        protected virtual bool AtPost(PlayerState state, MatchSim sim) => state.Position.DistanceTo(Home) <= HomeTolerance * 2.0;

        PlayerInput Fight(PlayerState state, MatchSim sim, PlayerState enemy)
        {
            if (sim.CastingBlocked(state))
            {
                if (HoldsZone(state, sim)) return NoEnemy(state, sim);  // holds the place it is in (the capture), walking about in it
                // in the enemy base or the rescue area nothing can be cast: step out and fight from outside, and stay out a while
                _stayOutLeft = ReenterDelay;
                return Steer(state, sim, OutOfZone(state, sim));
            }
            Vec2 offset = enemy.Position - state.Position;
            double distance = offset.Length();
            Vec2 toward = distance > 0.001 ? offset.Normalized() : new Vec2(0.0, 1.0);
            bool blocked = InCover(state.Position, enemy.Position, sim);
            if (sim.Rules.UltimateWeapon != null && state.UltCharge >= 100.0 - MatchSim.Epsilon && state.CanAct()
                && distance <= sim.Rules.UltimateWeapon.MaxRange)
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnUltimate, sim.Tick).WithAimPoint(enemy.Position);
            int slot = _cooldown <= 0.0 && state.CanAct() ? SlotToCast(state, sim, distance, blocked) : -1;
            if (slot >= 0)
            {
                _pressSlot = slot;
                _cooldown = sim.Rules.BotCastGap;
                WeaponDef def = sim.WeaponDefs[state.Weapons[slot]];
                // a tap on a heal heals yourself; anything else is released at the point it aims at
                _tapRelease = def.Kind == WeaponDef.WeaponKind.Heal;
                _aim = _tapRelease ? Vec2.Zero : AimAt(state, enemy, def, sim);
                _aimPoint = state.Position + _aim * GMath.Maxf(def.MaxRange, 1.0);
                int bit = WeaponCasting.CardBits[slot];
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, bit, sim.Tick);
            }
            double standoff = Standoff(state, sim);
            Vec2 move = Vec2.Zero;
            if (blocked) move = StepAside(state, enemy, sim);
            else if (distance > standoff + 0.5) move = toward;
            else if (distance < standoff - TooCloseSlack) move = -toward;
            else
            {
                double side = ((int)((double)sim.Tick * sim.Rules.TickDt() / StrafeSwitch) + state.Id) % 2 == 0 ? 1.0 : -1.0;
                move = toward.Rotated(side * GMath.Pi / 2.0);
            }
            PlayerInput output = Steer(state, sim, Unpin(AvoidEntering(state, sim, move)));
            // weave primary fire between superpower casts when the line is clear
            if (!blocked && sim.Rules.PrimaryWeapon != null && state.PrimaryCd <= 0.0 && state.CanAct()
                && distance <= sim.Rules.PrimaryWeapon.MaxRange)
            {
                Vec2 pAim = AimAt(state, enemy, sim.Rules.PrimaryWeapon, sim);
                output.WithAimPoint(state.Position + pAim * sim.Rules.PrimaryWeapon.MaxRange);
                output.Buttons |= PlayerInput.BtnPrimary;
            }
            // dash out of snares or to close distance; hop over ground traps and fields; slide to close medium gaps
            if (state.DashCd <= 0.0 && (state.Effects.Has(StatusEffects.Type.Slow) || distance > standoff + 8.0))
                output.Buttons |= PlayerInput.BtnDash;
            else if (state.JumpCd <= 0.0 && state.AirY <= 0.01 && GroundHazardNear(state, sim))
                output.Buttons |= PlayerInput.BtnJump;
            else if (state.SlideCd <= 0.0 && state.AirY <= 0.01 && state.SlideLeft <= 0.0 && distance > standoff + 4.5 && EffectiveSkill(sim) >= 0.35)
                output.Buttons |= PlayerInput.BtnSlide;
            return output;
        }

        /// <summary>True when this bot stays in the place it is in even though nothing can be cast there (a TeamBrain's base holder).</summary>
        protected virtual bool HoldsZone(PlayerState state, MatchSim sim) => false;

        /// <summary>A fight never walks into a place nothing can be cast in: it goes along the edge instead, or backs off.</summary>
        Vec2 AvoidEntering(PlayerState state, MatchSim sim, Vec2 move)
        {
            if (move == Vec2.Zero || sim.CastingBlocked(state)) return move;
            if (!sim.CastingBlockedAt(state, state.Position + move.Normalized() * EntryLookahead)) return move;
            foreach (double turn in QuarterTurns)
            {
                Vec2 along = move.Rotated(turn);
                if (!sim.CastingBlockedAt(state, state.Position + along.Normalized() * EntryLookahead)) return along;
            }
            return -move;
        }

        /// <summary>A move that has not got the bot anywhere for `PinnedTicks` ticks turns a quarter to one side (the other side next time).</summary>
        Vec2 Unpin(Vec2 move)
        {
            if (move == Vec2.Zero || _pinned < PinnedTicks) return move;
            _pinned = 0;
            _turn = -_turn;
            return move.Rotated(_turn * GMath.Pi / 2.0);
        }

        /// <summary>Walks between random spots of `area` (shrunk by `margin`), so a bot that waits keeps moving. Seeded by the match.</summary>
        protected PlayerInput Patrol(PlayerState state, MatchSim sim, Rect2 area, double margin)
        {
            Rect2 inner = area.Grow(-margin);
            if (inner.Size.x <= 0.0 || inner.Size.y <= 0.0) inner = new Rect2(area.GetCenter(), Vec2.Zero);
            if (_patrolSpot == Vec2.Inf || !inner.HasPoint(_patrolSpot) || state.Position.DistanceTo(_patrolSpot) <= PatrolReach)
            {
                Vec2 end = inner.End;
                float x = sim.Rng.RandfRange(inner.Position.X, end.X);
                float y = sim.Rng.RandfRange(inner.Position.Y, end.Y);
                _patrolSpot = new Vec2(x, y);
            }
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0).WithMoveTarget(_patrolSpot);
        }

        /// <summary>The way out of the place where nothing can be cast: away from the enemy base's centre, or from the rescue area's.</summary>
        Vec2 OutOfZone(PlayerState state, MatchSim sim)
        {
            int enemySide = -sim.SideForTeam(state.Team);
            Vec2 from = sim.Layout.RescueRect(sim.SideForTeam(state.Team)).GetCenter();
            if (sim.Layout.InBase(enemySide, state.Position)) from = sim.Layout.BaseCenter(enemySide);
            Vec2 way = state.Position - from;
            return way.LengthSquared() > 0.01 ? way : new Vec2(0.0, sim.SideForTeam(state.Team));
        }

        /// <summary>The first ready weapon that makes sense at `distance` (a heal when hurt, a shield when near, else in reach). -1 for none.</summary>
        int SlotToCast(PlayerState state, MatchSim sim, double distance, bool blocked)
        {
            bool paying = sim.PaysElixir(state);
            for (int slot = 0; slot < state.Weapons.Count; slot++)
            {
                string wid = state.Weapons[slot];
                WeaponDef def = wid != null && sim.WeaponDefs.TryGetValue(wid, out WeaponDef found) ? found : null;
                if (def == null || (paying && state.Elixir + MatchSim.Epsilon < (double)def.ElixirCost) || !sim.WeaponReady(state, slot)) continue;
                if (def.Kind == WeaponDef.WeaponKind.Heal)
                {
                    if ((double)state.Hp < (double)sim.Rules.PlayerMaxHp * HealBelow) return slot;
                    continue;
                }
                if (def.Shape == WeaponDef.WeaponShape.Shield)
                {
                    if (distance <= StandoffMax) return slot;
                    continue;
                }
                if (blocked && IsProjectile(def)) continue;
                if (distance <= def.MaxRange * 0.95) return slot;
            }
            return -1;
        }

        /// <summary>How far from the enemy it likes to stand: a share of its longest reach, kept inside StandoffMin..Max.</summary>
        public double Standoff(PlayerState state, MatchSim sim)
        {
            double reach = 0.0;
            foreach (string weaponId in state.Weapons)
            {
                if (string.IsNullOrEmpty(weaponId)) continue;  // an empty slot: its card is in the shuffler
                WeaponDef def = sim.WeaponDefs[weaponId];
                if (def.Kind != WeaponDef.WeaponKind.Heal && def.Shape != WeaponDef.WeaponShape.Shield) reach = GMath.Maxf(reach, def.MaxRange);
            }
            return GMath.Clampf(reach * sim.Rules.BotStandoffShare, StandoffMin, StandoffMax);
        }

        /// <summary>
        /// The aim to send for `def` at `enemy`: a direction for thrown and cone weapons, and for a ground circle, trap or field a length too,
        /// toward where the enemy will be when it lands (as far ahead as the skill says), with a little error.
        /// </summary>
        public Vec2 AimAt(PlayerState state, PlayerState enemy, WeaponDef def, MatchSim sim)
        {
            Vec2 enemyVel = _velocity.TryGetValue(enemy.Id, out Vec2 v) ? v : Vec2.Zero;
            Vec2 point = enemy.Position + enemyVel * FlightTime(state, enemy, def) * EffectiveSkill(sim);
            Vec2 offset = point - state.Position;
            if (offset.Length() < 0.001) offset = enemy.Position - state.Position;
            Vec2 aim = offset.Normalized();
            if (def.Shape == WeaponDef.WeaponShape.GroundAoe || def.Shape == WeaponDef.WeaponShape.Trap || def.Shape == WeaponDef.WeaponShape.Field)
                aim = (offset / GMath.Maxf(def.MaxRange, 0.001)).LimitLength(1.0);
            double limit = GMath.DegToRad(sim.Rules.BotAimErrorDegrees) * GMath.Lerpf(1.0, SkilledErrorShare, EffectiveSkill(sim));
            return aim.Rotated(sim.Rng.RandfRange((float)-limit, (float)limit));
        }

        /// <summary>Seconds from the press to the weapon landing on the enemy (a delay, and a flight at the weapon's speed).</summary>
        static double FlightTime(PlayerState state, PlayerState enemy, WeaponDef def)
        {
            double distance = state.Position.DistanceTo(enemy.Position);
            double flight = def.Speed > 0.0 ? distance / def.Speed : 0.0;
            return def.Delay + flight;
        }

        /// <summary>Projectiles stop at cover; a ground circle, a trap, a field, a shield and a cone do not.</summary>
        static bool IsProjectile(WeaponDef def) =>
            def.Shape == WeaponDef.WeaponShape.Targeted || def.Shape == WeaponDef.WeaponShape.Boomerang
            || def.Shape == WeaponDef.WeaponShape.Bouncer || def.Shape == WeaponDef.WeaponShape.Spinner;

        /// <summary>The aim quality in use (GDScript `_skill`): `Skill`, or the rules' `BotSkill` when unset, clamped to 0..1.</summary>
        public double EffectiveSkill(MatchSim sim) => GMath.Clampf(Skill >= 0.0 ? Skill : sim.Rules.BotSkill, 0.0, 1.0);

        /// <summary>Remembers where the enemies are, to know how fast they are going.</summary>
        public void Track(PlayerState state, MatchSim sim)
        {
            double dt = sim.Rules.TickDt();
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
            {
                if (_seen.TryGetValue(enemy.Id, out Vec2 seen)) _velocity[enemy.Id] = (enemy.Position - seen) / dt;
                _seen[enemy.Id] = enemy.Position;
            }
        }

        /// <summary>True when cover stands between `from` and `to`.</summary>
        static bool InCover(Vec2 from, Vec2 to, MatchSim sim)
        {
            foreach (Rect2 rect in sim.Obstacles)
                if (SegmentHitsRect(from, to, rect.Grow(CoverMargin))) return true;
            return false;
        }

        /// <summary>The way to go to get a clear line to `enemy`: the nearest sidestep with one, else straight at them.</summary>
        static Vec2 StepAside(PlayerState state, PlayerState enemy, MatchSim sim)
        {
            Vec2 toward = (enemy.Position - state.Position).Normalized();
            Vec2 side = toward.Orthogonal();
            foreach (double step in Sidesteps)
                foreach (double way in BothWays)
                {
                    Vec2 spot = state.Position + side * step * way;
                    if (!InCover(spot, enemy.Position, sim) && !InsideCover(spot, sim)) return (spot - state.Position).Normalized();
                }
            return toward;
        }

        static bool InsideCover(Vec2 point, MatchSim sim)
        {
            foreach (Rect2 rect in sim.Obstacles)
                if (rect.Grow(CoverMargin).HasPoint(point)) return true;
            return false;
        }

        /// <summary>True when the segment from `a` to `b` touches `rect` (the slab method).</summary>
        public static bool SegmentHitsRect(Vec2 a, Vec2 b, Rect2 rect) => Geometry2D.SegmentHitsRect(a, b, rect);

        static bool TeammateTagged(PlayerState state, MatchSim sim)
        {
            foreach (var pair in sim.Players)
                if (pair.Value.Team == state.Team && pair.Value.Tagged) return true;
            return false;
        }

        /// <summary>Walks to `target` by a move target (stands once within HomeTolerance).</summary>
        protected static PlayerInput WalkTo(PlayerState state, Vec2 target)
        {
            Vec2 point = state.Position.DistanceTo(target) <= HomeTolerance ? state.Position : target;
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 0).WithMoveTarget(point);
        }

        /// <summary>Walks to `target` routed through `sim.Nav` around buildings and the creek's bridges.</summary>
        protected PlayerInput WalkToNav(PlayerState state, MatchSim sim, Vec2 target)
        {
            if (state.Position.DistanceTo(target) <= HomeTolerance)
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(state.Position);
            Vec2 waypoint = sim.Nav != null ? sim.Nav.NextWaypoint(state.Position, target) : target;
            if (_pinned >= PinnedTicks)
            {
                Vec2 nudged = (waypoint - state.Position).Normalized().Rotated(_turn * GMath.Pi * 0.45);
                _pinned = 0;
                _turn = -_turn;
                waypoint = state.Position + nudged * 4.0;
            }
            PlayerInput output = PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(waypoint);
            if (state.DashCd <= 0.0 && state.Position.DistanceTo(target) > 36.0 && state.Effects.Has(StatusEffects.Type.Slow))
                output.Buttons |= PlayerInput.BtnDash;
            else if (state.JumpCd <= 0.0 && state.AirY <= 0.01 && GroundHazardNear(state, sim))
                output.Buttons |= PlayerInput.BtnJump;
            return output;
        }

        static bool GroundHazardNear(PlayerState state, MatchSim sim)
        {
            foreach (WeaponSystem.Zone zone in sim.Weapons.Zones)
            {
                if (zone.Team == state.Team) continue;
                if (zone.Kind == WeaponSystem.ZoneKind.Trap || zone.Kind == WeaponSystem.ZoneKind.SlowZone || zone.Kind == WeaponSystem.ZoneKind.Field)
                    if (state.Position.DistanceTo(zone.Center) <= zone.Radius + state.Radius + 0.6) return true;
            }
            return false;
        }
    }
}
