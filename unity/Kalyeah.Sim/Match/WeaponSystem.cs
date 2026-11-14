using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Runs every weapon effect that outlives the cast tick (port of weapon_system.gd): projectiles, delayed areas, traps, fields, shields
    /// and queued boomerangs. Owned by MatchSim; generic per <see cref="WeaponDef.WeaponShape"/>, never per weapon.
    /// </summary>
    public sealed class WeaponSystem
    {
        /// <summary>Max distance a projectile moves between hit checks.</summary>
        public const double ProjectileStep = 0.25;

        public enum ZoneKind { Explosion, Trap, SlowZone, Field }

        /// <summary>A cone weapon struck: (owner id, def, origin, direction).</summary>
        public event Action<int, WeaponDef, Vec2, Vec2> ConeStruck;
        /// <summary>A delayed cone was cast: it strikes `def.Delay` seconds later.</summary>
        public event Action<int, WeaponDef, Vec2, Vec2> ConeWinding;
        /// <summary>A heal landed: (owner id, target id, def).</summary>
        public event Action<int, int, WeaponDef> Healed;

        public sealed class Projectile
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public WeaponDef Def;
            public Vec2 Position;
            public Vec2 Direction;
            public double Travelled;
            /// <summary>TARGETED: homes on this player.</summary>
            public int TargetId = -1;
            /// <summary>BOOMERANG: false = going out, true = coming back.</summary>
            public bool Returning;
            /// <summary>BOUNCER: bounces done so far.</summary>
            public int Bounces;
            /// <summary>Ids already hit (per direction for boomerangs, once for spinners).</summary>
            public HashSet<int> HitIds = new HashSet<int>();
        }

        public sealed class Zone
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public WeaponDef Def;
            public ZoneKind Kind;
            public Vec2 Center;
            public double Radius;
            public double TimeLeft;
            /// <summary>TRAP: arming time left. FIELD: time to the next damage tick.</summary>
            public double Timer;
        }

        public sealed class Shield
        {
            public int Id;
            public WeaponDef Def;
            public int Team;
            public Vec2 Center;
            /// <summary>Unit vector along the wall.</summary>
            public Vec2 Along;
            public double HalfWidth;
            public double TimeLeft;
            /// <summary>Where it was cast (infinite: a wall placed by hand, which then stays as given).</summary>
            public Vec2 Origin = Vec2.Inf;
            public double BaseHalfWidth;
            public Vec2 PrevCenter;
            public double PrevHalfWidth;

            /// <summary>The way it faces: `Along` turned a quarter back.</summary>
            public Vec2 Forward() => new Vec2(-Along.y, Along.x);

            /// <summary>Seconds since it was cast.</summary>
            public double Age() => (Def != null ? Def.Duration : 0.0) - TimeLeft;

            /// <summary>Moves and widens it to its age's place (the previous line is kept for the sweep).</summary>
            public void Place()
            {
                PrevCenter = Center;
                PrevHalfWidth = HalfWidth;
                if (!Origin.IsFinite() || Def == null) return;
                double now = Age();
                Center = Origin + Forward() * WallMotion.Push(Def, now);
                HalfWidth = BaseHalfWidth * WallMotion.WidthScale(Def, now);
            }
        }

        public sealed class PendingCone
        {
            public int OwnerId;
            public WeaponDef Def;
            public Vec2 Direction;
            public double TimeLeft;
        }

        public sealed class QueuedThrow
        {
            public int OwnerId;
            public WeaponDef Def;
            public Vec2 Direction;
            public double TimeLeft;
        }

        public List<Projectile> Projectiles = new List<Projectile>();
        public List<Zone> Zones = new List<Zone>();
        public List<Shield> Shields = new List<Shield>();

        List<PendingCone> _cones = new List<PendingCone>();
        List<QueuedThrow> _throws = new List<QueuedThrow>();
        int _nextId = 1;

        public IReadOnlyList<PendingCone> PendingCones => _cones;
        public IReadOnlyList<QueuedThrow> QueuedThrows => _throws;

        /// <summary>The enemy a TARGETED cast would throw at: `targetId` while it is a targetable enemy in range, else null.</summary>
        public PlayerState TargetInRange(MatchSim sim, PlayerState caster, WeaponDef def, int targetId)
        {
            PlayerState target = sim.Players.GetOrDefault(targetId);
            if (target == null || !sim.IsTargetable(target) || target.Team == caster.Team) return null;
            return caster.Position.DistanceTo(target.Position) <= def.MaxRange ? target : null;
        }

        /// <summary>Casts `def` for `caster` with an already-resolved aim. Returns false when nothing could be cast.</summary>
        public bool Fire(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 aim, double holdSeconds, int targetId = -1)
        {
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Targeted:
                {
                    PlayerState target = TargetInRange(sim, caster, def, targetId);
                    if (target == null) return false;
                    Projectile projectile = NewProjectile(caster, def, caster.Position.DirectionTo(target.Position));
                    projectile.TargetId = target.Id;
                    break;
                }
                case WeaponDef.WeaponShape.Cone:
                {
                    Vec2 direction = AimDirection(caster, aim);
                    if (def.Delay > 0.0)
                    {
                        _cones.Add(new PendingCone { OwnerId = caster.Id, Def = def, Direction = direction, TimeLeft = def.Delay });
                        ConeWinding?.Invoke(caster.Id, def, caster.Position, direction);
                    }
                    else
                    {
                        HitCone(sim, caster, def, direction, def.SnipCount(holdSeconds));
                    }
                    break;
                }
                case WeaponDef.WeaponShape.GroundAoe:
                {
                    Vec2 point = AimPoint(caster, def, aim);
                    double travel = def.Speed > 0.0 ? caster.Position.DistanceTo(point) / def.Speed : 0.0;
                    NewZone(caster, def, ZoneKind.Explosion, point, def.Radius, def.Delay + travel);
                    break;
                }
                case WeaponDef.WeaponShape.Trap:
                {
                    Zone trap = NewZone(caster, def, ZoneKind.Trap, AimPoint(caster, def, aim), def.TriggerRadius, def.Lifetime);
                    trap.Timer = def.ArmTime;
                    break;
                }
                case WeaponDef.WeaponShape.Field:
                {
                    Zone field = NewZone(caster, def, ZoneKind.Field, AimPoint(caster, def, aim), def.Radius, def.Duration);
                    field.Timer = 0.0;
                    break;
                }
                case WeaponDef.WeaponShape.Boomerang:
                {
                    Vec2 direction = AimDirection(caster, aim);
                    NewProjectile(caster, def, direction);
                    for (int i = 1; i < def.Count; i++)
                        _throws.Add(new QueuedThrow { OwnerId = caster.Id, Def = def, Direction = direction, TimeLeft = def.Interval * i });
                    break;
                }
                case WeaponDef.WeaponShape.Shield:
                {
                    Vec2 facing = AimDirection(caster, aim);
                    var shield = new Shield { Id = TakeId(), Def = def, Team = caster.Team };
                    shield.Center = caster.Position + facing * def.Offset;
                    shield.Origin = shield.Center;
                    shield.Along = facing.Orthogonal();
                    shield.HalfWidth = def.Width / 2.0;
                    shield.BaseHalfWidth = shield.HalfWidth;
                    shield.TimeLeft = def.Duration;
                    shield.PrevCenter = shield.Center;
                    shield.PrevHalfWidth = shield.HalfWidth;
                    Shields.Add(shield);
                    break;
                }
                case WeaponDef.WeaponShape.Bouncer:
                case WeaponDef.WeaponShape.Spinner:
                case WeaponDef.WeaponShape.Skillshot:
                    NewProjectile(caster, def, AimDirection(caster, aim));
                    break;
                case WeaponDef.WeaponShape.Heal:
                {
                    PlayerState patient = HealTarget(sim, caster, def, aim);
                    if (patient == null) return false;
                    sim.Heal(patient.Id, def.Heal);
                    Healed?.Invoke(caster.Id, patient.Id, def);
                    break;
                }
            }
            return true;
        }

        public void Step(MatchSim sim, double dt)
        {
            StepThrows(sim, dt);
            StepCones(sim, dt);
            StepProjectiles(sim, dt);
            StepZones(sim, dt);
            StepShields(dt);
        }

        /// <summary>Clears everything (e.g. between points).</summary>
        public void Reset()
        {
            Projectiles.Clear();
            Zones.Clear();
            Shields.Clear();
            _cones.Clear();
            _throws.Clear();
        }

        // ---- aiming ----

        /// <summary>Auto-aim as a stick-style vector: toward the nearest enemy in range, else straight ahead at full range.</summary>
        public Vec2 AutoAim(MatchSim sim, PlayerState caster, WeaponDef def)
        {
            if (def.Shape == WeaponDef.WeaponShape.Heal) return Vec2.Zero;
            PlayerState target = sim.NearestEnemy(caster, def.MaxRange);
            if (target != null && target.Position != caster.Position)
            {
                Vec2 offset = target.Position - caster.Position;
                if (def.MaxRange > 0.0) return (offset / def.MaxRange).LimitLength(1.0);
                return offset.Normalized();
            }
            return caster.Facing;
        }

        /// <summary>Who a heal aimed with `aim` reaches: the caster for a tap, else the teammate nearest the dragged direction.</summary>
        public PlayerState HealTarget(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 aim)
        {
            if (aim == Vec2.Zero) return sim.IsTargetable(caster) ? caster : null;
            Vec2 direction = aim.Normalized();
            double half = GMath.DegToRad(def.AngleDegrees / 2.0);
            PlayerState best = null;
            double bestAngle = half;
            foreach (var pair in sim.Players)
            {
                PlayerState other = pair.Value;
                if (other.Id == caster.Id || other.Team != caster.Team || !sim.IsTargetable(other)) continue;
                Vec2 offset = other.Position - caster.Position;
                if (offset.Length() > def.MaxRange + other.Radius) continue;
                double angle = offset.Length() > 0.001 ? Math.Abs(direction.AngleTo(offset)) : 0.0;
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    best = other;
                }
            }
            return best;
        }

        /// <summary>Direction of a resolved aim (the facing if the aim is zero).</summary>
        public Vec2 AimDirection(PlayerState caster, Vec2 aim) => aim != Vec2.Zero ? aim.Normalized() : caster.Facing;

        /// <summary>Point of a resolved aim: aim length scales the range.</summary>
        public Vec2 AimPoint(PlayerState caster, WeaponDef def, Vec2 aim) => caster.Position + aim.LimitLength(1.0) * def.MaxRange;

        /// <summary>False when releasing now would cast nothing (a targeted throw with nobody locked in range, a heal with nobody).</summary>
        public bool WouldCast(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 aim, int targetId = -1)
        {
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Targeted:
                {
                    PlayerState target = sim.Players.GetOrDefault(targetId);
                    return target != null && sim.IsTargetable(target) && target.Team != caster.Team
                        && caster.Position.DistanceTo(target.Position) <= def.MaxRange;
                }
                case WeaponDef.WeaponShape.Heal:
                    return HealTarget(sim, caster, def, aim) != null;
            }
            return true;
        }

        /// <summary>Who a cast with `aim` would reach right now (the aim preview rings them).</summary>
        public List<PlayerState> WouldReach(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 aim, int targetId = -1)
        {
            var reached = new List<PlayerState>();
            if (!WouldCast(sim, caster, def, aim, targetId)) return reached;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Targeted:
                    reached.Add(sim.Players[targetId]);
                    break;
                case WeaponDef.WeaponShape.Heal:
                    reached.Add(HealTarget(sim, caster, def, aim));
                    break;
                case WeaponDef.WeaponShape.Cone:
                    reached = ConeTargets(sim, caster, def, AimDirection(caster, aim));
                    break;
                case WeaponDef.WeaponShape.GroundAoe:
                case WeaponDef.WeaponShape.Field:
                    reached = EnemiesWithin(sim, caster.Team, AimPoint(caster, def, aim), def.Radius);
                    break;
                case WeaponDef.WeaponShape.Trap:
                    reached = EnemiesWithin(sim, caster.Team, AimPoint(caster, def, aim), def.TriggerRadius);
                    break;
                case WeaponDef.WeaponShape.Bouncer:
                {
                    Vec2 direction = AimDirection(caster, aim);
                    for (int i = 0; i < def.Count; i++)
                    {
                        Vec2 at = caster.Position + direction * def.MaxRange * (double)(i + 1) / (double)GMath.Maxi(def.Count, 1);
                        foreach (PlayerState target in EnemiesWithin(sim, caster.Team, at, def.Radius))
                            if (!reached.Contains(target)) reached.Add(target);
                    }
                    break;
                }
                case WeaponDef.WeaponShape.Boomerang:
                case WeaponDef.WeaponShape.Spinner:
                case WeaponDef.WeaponShape.Skillshot:
                {
                    Vec2 direction = AimDirection(caster, aim);
                    Vec2 end = caster.Position + direction * def.MaxRange;
                    double width = GMath.Maxf(def.ProjectileRadius, def.Shape == WeaponDef.WeaponShape.Spinner ? def.Radius : 0.0);
                    foreach (PlayerState target in sim.EnemiesOf(caster.Team))
                    {
                        Vec2 nearest = Geometry2D.GetClosestPointToSegment(target.Position, caster.Position, end);
                        if (nearest.DistanceTo(target.Position) <= width + target.Radius)
                        {
                            reached.Add(target);
                            if (def.Shape == WeaponDef.WeaponShape.Skillshot) break;
                        }
                    }
                    break;
                }
            }
            return reached;
        }

        // ---- per shape ----

        void HitCone(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 direction, int hits)
        {
            ConeStruck?.Invoke(caster.Id, def, caster.Position, direction);
            foreach (PlayerState target in ConeTargets(sim, caster, def, direction))
                for (int i = 0; i < hits; i++) Hit(sim, target, def, caster.Id);
        }

        /// <summary>The enemies a cone from `caster` along `direction` reaches.</summary>
        public List<PlayerState> ConeTargets(MatchSim sim, PlayerState caster, WeaponDef def, Vec2 direction)
        {
            var reached = new List<PlayerState>();
            double halfAngle = GMath.DegToRad(def.AngleDegrees / 2.0);
            foreach (PlayerState target in sim.EnemiesOf(caster.Team))
            {
                Vec2 offset = target.Position - caster.Position;
                if (offset.Length() > def.MaxRange + target.Radius) continue;
                if (offset.Length() > 0.001 && Math.Abs(direction.AngleTo(offset)) > halfAngle) continue;
                reached.Add(target);
            }
            return reached;
        }

        void StepCones(MatchSim sim, double dt)
        {
            var still = new List<PendingCone>();
            foreach (PendingCone cone in _cones)
            {
                cone.TimeLeft -= dt;
                if (cone.TimeLeft > 0.0)
                {
                    still.Add(cone);
                    continue;
                }
                PlayerState caster = sim.Players.GetOrDefault(cone.OwnerId);
                if (caster != null && !caster.Tagged) HitCone(sim, caster, cone.Def, cone.Direction, 1);
            }
            _cones = still;
        }

        void StepThrows(MatchSim sim, double dt)
        {
            var still = new List<QueuedThrow>();
            foreach (QueuedThrow queued in _throws)
            {
                queued.TimeLeft -= dt;
                if (queued.TimeLeft > 0.0)
                {
                    still.Add(queued);
                    continue;
                }
                PlayerState caster = sim.Players.GetOrDefault(queued.OwnerId);
                if (caster != null && !caster.Tagged) NewProjectile(caster, queued.Def, queued.Direction);
            }
            _throws = still;
        }

        void StepProjectiles(MatchSim sim, double dt)
        {
            var still = new List<Projectile>();
            // GDScript iterates the array it is given; projectiles launched meanwhile (none in the base game) join next tick.
            var current = Projectiles;
            for (int i = 0; i < current.Count; i++)
            {
                Projectile projectile = current[i];
                if (AdvanceProjectile(sim, projectile, projectile.Def.Speed * dt)) still.Add(projectile);
            }
            Projectiles = still;
        }

        /// <summary>Moves a projectile in small steps; returns false once it is used up.</summary>
        bool AdvanceProjectile(MatchSim sim, Projectile projectile, double distance)
        {
            double left = distance;
            IReadOnlyList<Rect2> blocking = sim.BlockingRectsForTeam(projectile.Team);
            while (left > 0.0)
            {
                double move = GMath.Minf(ProjectileStep, left);
                left -= move;
                Vec2 previous = projectile.Position;
                if (!MoveProjectile(sim, projectile, move)) return false;
                if (Blocked(projectile, previous, blocking))
                {
                    if (projectile.Def.Shape == WeaponDef.WeaponShape.Boomerang && !projectile.Returning)
                    {
                        projectile.Position = previous;
                        TurnBack(projectile);
                        continue;
                    }
                    return false;
                }
                if (!CheckHits(sim, projectile)) return false;
            }
            return true;
        }

        /// <summary>Steers and moves; returns false when the projectile has finished its flight.</summary>
        bool MoveProjectile(MatchSim sim, Projectile projectile, double move)
        {
            WeaponDef def = projectile.Def;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Targeted:
                    if (projectile.TargetId >= 0)
                    {
                        PlayerState target = sim.Players.GetOrDefault(projectile.TargetId);
                        if (target == null || !sim.IsTargetable(target)) return false;
                        projectile.Direction = projectile.Position.DirectionTo(target.Position);
                    }
                    break;
                case WeaponDef.WeaponShape.Boomerang:
                    if (projectile.Returning)
                    {
                        PlayerState thrower = sim.Players.GetOrDefault(projectile.OwnerId);
                        if (thrower == null || thrower.Tagged) return false;
                        if (projectile.Position.DistanceTo(thrower.Position) <= sim.Rules.BoomerangCatchDistance) return false;
                        projectile.Direction = projectile.Position.DirectionTo(thrower.Position);
                    }
                    break;
            }
            projectile.Position += projectile.Direction * move;
            projectile.Travelled += move;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Boomerang:
                    if (!projectile.Returning && projectile.Travelled >= def.MaxRange) TurnBack(projectile);
                    break;
                case WeaponDef.WeaponShape.Bouncer:
                {
                    double nextBounce = def.MaxRange * (double)(projectile.Bounces + 1) / (double)GMath.Maxi(def.Count, 1);
                    if (projectile.Travelled >= nextBounce)
                    {
                        projectile.Bounces += 1;
                        AreaHit(sim, projectile.Team, def, projectile.Position, def.Radius, projectile.OwnerId);
                        if (projectile.Bounces >= def.Count) return false;
                    }
                    break;
                }
                case WeaponDef.WeaponShape.Spinner:
                    if (projectile.Travelled >= def.MaxRange)
                    {
                        CheckHits(sim, projectile);
                        return false;
                    }
                    break;
                case WeaponDef.WeaponShape.Skillshot:
                    if (projectile.Travelled >= def.MaxRange) return false;
                    break;
            }
            return true;
        }

        static void TurnBack(Projectile projectile)
        {
            projectile.Returning = true;
            projectile.HitIds.Clear();
        }

        /// <summary>Obstacles, street edges and enemy shields stop projectiles.</summary>
        bool Blocked(Projectile projectile, Vec2 previous, IReadOnlyList<Rect2> blocking)
        {
            for (int i = 0; i < blocking.Count; i++)
                if (blocking[i].HasPoint(projectile.Position)) return true;
            return BlocksSegment(projectile.Team, previous, projectile.Position);
        }

        /// <summary>True if an enemy (of `team`) wall crosses the segment a-b: its line now, a tick ago, or the ground swept between.</summary>
        public bool BlocksSegment(int team, Vec2 a, Vec2 b)
        {
            foreach (Shield shield in Shields)
            {
                if (shield.Team == team) continue;
                Vec2 left = shield.Center - shield.Along * shield.HalfWidth;
                Vec2 right = shield.Center + shield.Along * shield.HalfWidth;
                if (Geometry2D.SegmentIntersectsSegment(a, b, left, right)) return true;
                if (shield.PrevCenter == shield.Center && shield.PrevHalfWidth == shield.HalfWidth) continue;
                Vec2 oldLeft = shield.PrevCenter - shield.Along * shield.PrevHalfWidth;
                Vec2 oldRight = shield.PrevCenter + shield.Along * shield.PrevHalfWidth;
                if (Geometry2D.SegmentIntersectsSegment(a, b, oldLeft, oldRight)) return true;
                var swept = new[] { oldLeft, oldRight, right, left };
                if (Geometry2D.IsPointInPolygon(a, swept) || Geometry2D.IsPointInPolygon(b, swept)) return true;
            }
            return false;
        }

        /// <summary>Returns false if the projectile is consumed by the hit.</summary>
        bool CheckHits(MatchSim sim, Projectile projectile)
        {
            WeaponDef def = projectile.Def;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Targeted:
                {
                    PlayerState target = sim.Players.GetOrDefault(projectile.TargetId);
                    if (target != null && projectile.Position.DistanceTo(target.Position) <= def.ProjectileRadius + target.Radius)
                    {
                        Hit(sim, target, def, projectile.OwnerId);
                        return false;
                    }
                    break;
                }
                case WeaponDef.WeaponShape.Boomerang:
                    foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
                    {
                        if (projectile.HitIds.Contains(target.Id)) continue;
                        if (projectile.Position.DistanceTo(target.Position) <= def.ProjectileRadius + target.Radius)
                        {
                            projectile.HitIds.Add(target.Id);
                            Hit(sim, target, def, projectile.OwnerId);
                        }
                    }
                    break;
                case WeaponDef.WeaponShape.Spinner:
                    foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
                    {
                        if (projectile.HitIds.Contains(target.Id)) continue;
                        if (projectile.Position.DistanceTo(target.Position) <= def.Radius + target.Radius)
                        {
                            projectile.HitIds.Add(target.Id);
                            Hit(sim, target, def, projectile.OwnerId);
                        }
                    }
                    break;
                case WeaponDef.WeaponShape.Skillshot:
                    foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
                    {
                        if (projectile.Position.DistanceTo(target.Position) <= def.ProjectileRadius + target.Radius)
                        {
                            Hit(sim, target, def, projectile.OwnerId);
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }

        void StepZones(MatchSim sim, double dt)
        {
            var still = new List<Zone>();
            var current = Zones;
            for (int i = 0; i < current.Count; i++)
                if (StepZone(sim, current[i], dt)) still.Add(current[i]);
            Zones = still;
        }

        /// <summary>Returns false once the zone is gone.</summary>
        bool StepZone(MatchSim sim, Zone zone, double dt)
        {
            zone.TimeLeft -= dt;
            switch (zone.Kind)
            {
                case ZoneKind.Explosion:
                    if (zone.TimeLeft <= 0.0)
                    {
                        AreaHit(sim, zone.Team, zone.Def, zone.Center, zone.Radius, zone.OwnerId);
                        return false;
                    }
                    break;
                case ZoneKind.Trap:
                    zone.Timer -= dt;
                    if (zone.Timer <= 0.0 && AnyGroundedEnemyWithin(sim, zone.Team, zone.Center, zone.Radius))
                    {
                        zone.Kind = ZoneKind.SlowZone;
                        zone.Radius = zone.Def.Radius;
                        zone.TimeLeft = zone.Def.Duration;
                        return true;
                    }
                    return zone.TimeLeft > 0.0;
                case ZoneKind.SlowZone:
                    foreach (PlayerState target in EnemiesWithin(sim, zone.Team, zone.Center, zone.Radius))
                        if (target.AirY < MatchSim.JumpClearHeight)
                            sim.ApplyEffect(target.Id, zone.Def.Effect, zone.Def.EffectDuration, zone.Def.EffectMagnitude);
                    return zone.TimeLeft > 0.0;
                case ZoneKind.Field:
                    zone.Timer -= dt;
                    if (zone.Timer <= 0.0)
                    {
                        zone.Timer += zone.Def.Interval;
                        GroundedAreaHit(sim, zone.Team, zone.Def, zone.Center, zone.Radius, zone.OwnerId);
                    }
                    return zone.TimeLeft > 0.0;
            }
            return true;
        }

        void StepShields(double dt)
        {
            var still = new List<Shield>();
            foreach (Shield shield in Shields)
            {
                shield.TimeLeft -= dt;
                if (shield.TimeLeft > 0.0)
                {
                    shield.Place();
                    still.Add(shield);
                }
            }
            Shields = still;
        }

        // ---- helpers ----

        void AreaHit(MatchSim sim, int team, WeaponDef def, Vec2 center, double radius, int by)
        {
            foreach (PlayerState target in EnemiesWithin(sim, team, center, radius)) Hit(sim, target, def, by);
        }

        void GroundedAreaHit(MatchSim sim, int team, WeaponDef def, Vec2 center, double radius, int by)
        {
            foreach (PlayerState target in EnemiesWithin(sim, team, center, radius))
                if (target.AirY < MatchSim.JumpClearHeight) Hit(sim, target, def, by);
        }

        public List<PlayerState> EnemiesWithin(MatchSim sim, int team, Vec2 center, double radius)
        {
            var result = new List<PlayerState>();
            foreach (PlayerState target in sim.EnemiesOf(team))
                if (center.DistanceTo(target.Position) <= radius + target.Radius) result.Add(target);
            return result;
        }

        public bool AnyEnemyWithin(MatchSim sim, int team, Vec2 center, double radius) => EnemiesWithin(sim, team, center, radius).Count > 0;

        bool AnyGroundedEnemyWithin(MatchSim sim, int team, Vec2 center, double radius)
        {
            foreach (PlayerState target in EnemiesWithin(sim, team, center, radius))
                if (target.AirY < MatchSim.JumpClearHeight) return true;
            return false;
        }

        void Hit(MatchSim sim, PlayerState target, WeaponDef def, int by)
        {
            sim.EmitWeaponHit(target.Id, def.Id);
            if (def.Effect != StatusEffects.Type.None) sim.ApplyEffect(target.Id, def.Effect, def.EffectDuration, def.EffectMagnitude);
            if (def.Damage > 0) sim.Damage(target.Id, def.Damage, by);
        }

        /// <summary>A projectile of `def` leaves `caster` along `direction` outside the card rules (Tumbang Preso's thrown slipper).</summary>
        public Projectile Launch(PlayerState caster, WeaponDef def, Vec2 direction) => NewProjectile(caster, def, direction);

        Projectile NewProjectile(PlayerState caster, WeaponDef def, Vec2 direction)
        {
            var projectile = new Projectile
            {
                Id = TakeId(), OwnerId = caster.Id, Team = caster.Team, Def = def, Position = caster.Position, Direction = direction,
            };
            Projectiles.Add(projectile);
            return projectile;
        }

        Zone NewZone(PlayerState caster, WeaponDef def, ZoneKind kind, Vec2 center, double radius, double time)
        {
            var zone = new Zone
            {
                Id = TakeId(), OwnerId = caster.Id, Team = caster.Team, Def = def, Kind = kind, Center = center, Radius = radius, TimeLeft = time,
            };
            Zones.Add(zone);
            return zone;
        }

        int TakeId()
        {
            _nextId += 1;
            return _nextId;
        }
    }
}
