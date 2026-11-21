using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>Typed ground/world visual categories for all docs/KITS.md skill zones.</summary>
    public enum SkillZoneKind
    {
        PabitinHeal = 0,
        PaloseboPole = 1,
        SaranggolaKite = 2,
        PikoGrid = 3,
        JackstoneCircle = 4,
        LuksongLubidRope = 5,
        GarterNet = 6,
        HolensSpillCone = 7,
        PatinteroLines = 8,
        PabitinSnare = 9,
        LupaQuake = 10,
        PukpokLanding = 11,
        TayaCount = 12,
        LangitAura = 13,
        TrumpoSpin = 14,
        SungkaAbsorb = 15,
    }

    /// <summary>
    /// Runs every weapon and spec-skill effect that outlives the cast tick (port of weapon_system.gd):
    /// projectiles, delayed areas, traps, fields, shields, queued throws, plus docs/KITS.md destructible walls,
    /// hidden traps, sentries, ally tethers, skill zones, and snapshot reveal markers.
    /// </summary>
    public sealed class WeaponSystem
    {
        /// <summary>Max distance a projectile moves between hit checks.</summary>
        public const double ProjectileStep = 0.25;
        /// <summary>Half-thickness (metres) of a destructible player wall for movement collision.</summary>
        public const double WallHalfThickness = 0.28;

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
            public SkillDef Skill;
            public Vec2 Position;
            public Vec2 Origin;
            public Vec2 Direction;
            public double Travelled;
            /// <summary>TARGETED or Sipa chain: homes on this player.</summary>
            public int TargetId = -1;
            /// <summary>BOOMERANG: false = going out, true = coming back.</summary>
            public bool Returning;
            /// <summary>BOUNCER: bounces done so far.</summary>
            public int Bounces;
            /// <summary>Ids already hit (per direction for boomerangs, once for spinners / Sipa chain).</summary>
            public HashSet<int> HitIds = new HashSet<int>();
            /// <summary>True when this projectile is a docs/SPEC.md §7 Tirador stone.</summary>
            public bool IsTirador;
            public bool AimedDown;
            public double MaxRangeOverride;
            public int DamageOverride;
            public bool Ricocheted;
            public int ChainBouncesLeft;
            public double ChainRange;
            public int CastSlot = -1;

            public string EffectId => Skill != null ? Skill.Id : (Def != null ? Def.Id : "");
            public double Speed => Skill != null && Skill.Speed > 0.0 ? Skill.Speed : (Def != null ? Def.Speed : 0.0);
            public double EffectiveMaxRange => MaxRangeOverride > 0.0 ? MaxRangeOverride : (Skill != null ? Skill.MaxRange : (Def != null ? Def.MaxRange : 0.0));
            public double HitRadius => Skill != null ? 0.30 : (Def != null ? Def.ProjectileRadius : 0.24);
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

        /// <summary>Destructible wall blocking BOTH teams' movement and projectiles (Jopay Bahay-Bahayan, Paeng Tinik Wall).</summary>
        public sealed class DestructibleWall
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public string SkillId = "";
            public Vec2 Center;
            public Vec2 Along;
            public double HalfWidth;
            public int Hp;
            public int MaxHp;
            public double TimeLeft;
            public double Duration;
        }

        /// <summary>Hidden ground trap (Kikay Teks Tripwire, Domeng Sako Trap): max 2 per kid, visible &lt;= 3m or revealed, 1 stone breaks it.</summary>
        public sealed class HiddenTrap
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public string SkillId = "";
            public SkillMechanic Mechanic;
            public Vec2 Position;
            public double TriggerRadius;
            public double TimeLeft;
            public double ArmLeft;
            public bool Armed;
            public bool RevealedToEnemy;
            public double EffectDuration;
            public double EffectMagnitude;
            public double RevealDuration;
            public bool KnownRevealAlert;
        }

        /// <summary>Spinning top sentry (Manoy Trumpo Bantay): lasts 20s, pings enemies in 8m every 1s, 1 stone breaks it.</summary>
        public sealed class Sentry
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public string SkillId = "";
            public Vec2 Position;
            public double Radius;
            public double TimeLeft;
            public double Duration;
            public double PingInterval;
            public double PingTimer;
            public double RevealDuration;
            public bool KnownRevealAlert;
            public int Hp = 1;
        }

        /// <summary>Ally damage-redirect tether (Oriang Chinese Garter): 30% dmg to Oriang; snaps past 12m for speed boost.</summary>
        public sealed class AllyTether
        {
            public int Id;
            public int OwnerId;
            public int AllyId;
            public int Team;
            public string SkillId = "";
            public double MaxRange;
            public double TimeLeft;
            public double Duration;
            public double RedirectRatio;
            public double SpeedBonusRatio;
            public double BuffDuration;
        }

        /// <summary>Active ground/line/cone/aura/delayed skill zone from docs/KITS.md.</summary>
        public sealed class SkillZone
        {
            public int Id;
            public int OwnerId;
            public int Team;
            public string SkillId = "";
            public SkillZoneKind Kind;
            public Vec2 Position;
            public Vec2 EndPosition;
            public Vec2 Direction;
            public Vec2 Along;
            public double Radius;
            public double HalfWidth;
            public double AngleDegrees;
            public int Count = 1;
            public double Spacing;
            public double TimeLeft;
            public double Duration;
            public double DelayLeft;
            public double Interval;
            public double TickTimer;
            public int Damage;
            public int Heal;
            public double EffectDuration;
            public double SecondaryDuration;
            public double EffectMagnitude;
            public double RevealDuration;
            public bool KnownRevealAlert;
            public bool Triggered;
            /// <summary>Bitmasks of enemy IDs already stunned per Patintero line (0..2).</summary>
            public int HitLineMask0;
            public int HitLineMask1;
            public int HitLineMask2;
        }

        /// <summary>One-time world/minimap Snapshot reveal marker (Pogs Slam, Trumpo Bantay, Taya!).</summary>
        public sealed class RevealMarker
        {
            public int Id;
            public int Team;
            public int TargetId;
            public string SkillId = "";
            public Vec2 Position;
            public double TimeLeft;
            public double Duration;
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
            public SkillDef Skill;
            public Vec2 Direction;
            public double TimeLeft;
            public int CastSlot = -1;
        }

        public List<Projectile> Projectiles = new List<Projectile>();
        public List<Zone> Zones = new List<Zone>();
        public List<Shield> Shields = new List<Shield>();
        public List<DestructibleWall> Walls = new List<DestructibleWall>();
        public List<HiddenTrap> Traps = new List<HiddenTrap>();
        public List<Sentry> Sentries = new List<Sentry>();
        public List<AllyTether> Tethers = new List<AllyTether>();
        public List<SkillZone> SkillZones = new List<SkillZone>();
        public List<RevealMarker> RevealMarkers = new List<RevealMarker>();

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

        /// <summary>Launches a docs/SPEC.md §7 Tirador stone with charged range, optional empowered damage, and ADS precision.</summary>
        public Projectile LaunchTirador(PlayerState caster, WeaponDef primaryDef, Vec2 direction, double range, int damage, bool aimedDown)
        {
            var projectile = new Projectile
            {
                Id = TakeId(),
                OwnerId = caster.Id,
                Team = caster.Team,
                Def = primaryDef,
                Position = caster.Position,
                Origin = caster.Position,
                Direction = direction.LengthSquared() > 0.0001 ? direction.Normalized() : caster.Facing,
                IsTirador = true,
                AimedDown = aimedDown,
                MaxRangeOverride = range,
                DamageOverride = damage,
            };
            Projectiles.Add(projectile);
            return projectile;
        }

        /// <summary>Executes any of the 36 approved docs/KITS.md kid skills. Returns false if no legal target/cast.</summary>
        public bool FireSkill(MatchSim sim, PlayerState caster, SkillDef skill, Vec2 aim, double holdSeconds, int targetId, int slot)
        {
            Vec2 dir = AimDirection(caster, aim);
            Vec2 aimPoint = AimPointSkill(caster, skill, aim);
            switch (skill.Mechanic)
            {
                // ---- STRIKERS ----
                case SkillMechanic.LuksongTinik:
                {
                    double dist = skill.MaxRange > 0.0 ? skill.MaxRange : 6.0;
                    double dashSpeed = sim.Rules.DashSpeed > 0.0 ? sim.Rules.DashSpeed : 18.5;
                    caster.DashDir = dir;
                    caster.Facing = dir;
                    caster.DashLeft = dist / dashSpeed;
                    caster.VaultDashActive = true;
                    caster.SlideLeft = 0.0;
                    caster.Crouching = false;
                    caster.VelY = GMath.Maxf(caster.VelY, 4.2);
                    caster.Effects.ClearType(StatusEffects.Type.Slow);
                    caster.EmpoweredTiradorBonus = skill.BonusRatio > 0.0 ? skill.BonusRatio : 0.50;
                    double empDur = GMath.Maxf(skill.Duration, skill.SecondaryDuration);
                    caster.EmpoweredTiradorLeft = empDur > 0.0 ? empDur : 2.0;
                    sim.EmitPlayerDashed(caster.Id);
                    return true;
                }
                case SkillMechanic.TiraHolens:
                {
                    Projectile p = NewSkillProjectile(caster, skill, dir, slot);
                    p.DamageOverride = skill.Damage > 0 ? skill.Damage : 20;
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.TsinelasBagsik:
                {
                    int count = GMath.Maxi(1, skill.Count > 0 ? skill.Count : 3);
                    double interval = skill.Interval > 0.0 ? skill.Interval : 0.65;
                    Projectile first = NewSkillProjectile(caster, skill, dir, slot);
                    first.DamageOverride = skill.Damage > 0 ? skill.Damage : 22;
                    for (int i = 1; i < count; i++)
                        _throws.Add(new QueuedThrow { OwnerId = caster.Id, Skill = skill, Direction = dir, TimeLeft = interval * i, CastSlot = slot });
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.Syato:
                {
                    if (caster.SyatoRecastLeft > 0.0 && caster.SyatoTargetId >= 0)
                    {
                        PlayerState marked = sim.Players.GetOrDefault(caster.SyatoTargetId);
                        caster.SyatoRecastLeft = 0.0;
                        caster.SyatoTargetId = -1;
                        if (marked == null || marked.Tagged) return false;
                        Vec2 dashDir = caster.Position.DistanceSquaredTo(marked.Position) > 0.001
                            ? caster.Position.DirectionTo(marked.Position)
                            : caster.Facing;
                        Vec2 land = marked.Position - dashDir * (caster.Radius + marked.Radius + 0.15);
                        caster.Position = ResolveWallPushOut(sim.ClampMoveTarget(land, caster.Radius), caster.Radius);
                        caster.Facing = dashDir;
                        int whack = skill.SecondaryDamage > 0 ? skill.SecondaryDamage : 22;
                        sim.EmitWeaponHit(marked.Id, skill.Id);
                        sim.DamageCombat(marked.Id, whack, caster.Id, skill.Id, false, true);
                        return true;
                    }
                    Projectile stick = NewSkillProjectile(caster, skill, dir, slot);
                    stick.DamageOverride = skill.Damage > 0 ? skill.Damage : 18;
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.TrumpoIkot:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 2.0;
                    caster.TrumpoSpinLeft = dur;
                    caster.TrumpoSpinTickTimer = 0.0;
                    caster.Effects.ClearType(StatusEffects.Type.Slow);
                    caster.Effects.ClearType(StatusEffects.Type.Slip);
                    NewSkillZone(caster, skill, SkillZoneKind.TrumpoSpin, caster.Position, dur);
                    return true;
                }
                case SkillMechanic.PukpokPalayok:
                {
                    Vec2 dest = sim.ClampMoveTarget(aimPoint, caster.Radius);
                    double dur = skill.Duration > 0.0 ? skill.Duration : (skill.Delay > 0.0 ? skill.Delay : 0.4);
                    caster.LeapStart = caster.Position;
                    caster.LeapTarget = dest;
                    caster.LeapDuration = dur;
                    caster.LeapLeft = dur;
                    caster.LeapSkillId = skill.Id;
                    caster.Facing = caster.Position.DistanceSquaredTo(dest) > 0.001 ? caster.Position.DirectionTo(dest) : dir;
                    SkillZone lz = NewSkillZone(caster, skill, SkillZoneKind.PukpokLanding, dest, dur);
                    lz.DelayLeft = dur;
                    return true;
                }
                case SkillMechanic.Sipa:
                {
                    Projectile sipa = NewSkillProjectile(caster, skill, dir, slot);
                    sipa.DamageOverride = skill.Damage > 0 ? skill.Damage : 20;
                    sipa.ChainBouncesLeft = GMath.Maxi(0, (skill.Count > 0 ? skill.Count : 3) - 1);
                    sipa.ChainRange = skill.SecondaryRadius > 0.0 ? skill.SecondaryRadius : 7.0;
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.Tiyakad:
                {
                    caster.StiltsLeft = skill.Duration > 0.0 ? skill.Duration : 4.0;
                    caster.Crouching = false;
                    caster.SlideLeft = 0.0;
                    return true;
                }
                case SkillMechanic.BatoExecute:
                {
                    double windup = skill.Delay > 0.0 ? skill.Delay : 1.0;
                    caster.BatoWindupLeft = windup;
                    caster.BatoWindupAim = dir;
                    caster.Facing = dir;
                    caster.CastLockLeft = windup;
                    sim.EndMove(caster);
                    return true;
                }

                // ---- DEFENDERS ----
                case SkillMechanic.BahayBahayan:
                {
                    double halfW = (skill.Width > 0.0 ? skill.Width : 6.0) * 0.5;
                    int hp = skill.WallHp > 0 ? skill.WallHp : 80;
                    double dur = skill.Duration > 0.0 ? skill.Duration : 8.0;
                    DestructibleWall wall = AddWall(sim, caster, skill.Id, aimPoint, dir.Orthogonal(), halfW, hp, dur);
                    PushPlayersOutOfWall(sim, wall);
                    return true;
                }
                case SkillMechanic.PabitinHeal:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 4.0;
                    double interval = skill.Interval > 0.0 ? skill.Interval : 0.5;
                    int healTick = skill.Heal > 12 ? GMath.Maxi(1, GMath.Roundi(skill.Heal * interval / dur)) : (skill.Heal > 0 ? skill.Heal : 6);
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.PabitinHeal, aimPoint, dur);
                    z.Interval = interval;
                    z.Heal = healTick;
                    z.TickTimer = 0.0;
                    return true;
                }
                case SkillMechanic.Palosebo:
                {
                    int ownSide = sim.SideForTeam(caster.Team);
                    Vec2 baseCenter = sim.Layout.BaseCenter(ownSide);
                    Vec2 polePos = sim.Layout.InBase(ownSide, aimPoint) ? aimPoint : baseCenter;
                    double dur = skill.Duration > 0.0 ? skill.Duration : 8.0;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.PaloseboPole, polePos, dur);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 7.0;
                    z.EffectMagnitude = skill.EffectMagnitude > 0.0 ? skill.EffectMagnitude : 0.35;
                    return true;
                }
                case SkillMechanic.ChineseGarter:
                {
                    double maxRange = skill.MaxRange > 0.0 ? skill.MaxRange : 12.0;
                    PlayerState ally = FindBestAllyForTether(sim, caster, aimPoint, maxRange);
                    if (ally == null) return false;
                    RemoveTethersOwnedBy(caster.Id);
                    double dur = skill.Duration > 0.0 ? skill.Duration : 6.0;
                    Tethers.Add(new AllyTether
                    {
                        Id = TakeId(),
                        OwnerId = caster.Id,
                        AllyId = ally.Id,
                        Team = caster.Team,
                        SkillId = skill.Id,
                        MaxRange = maxRange,
                        TimeLeft = dur,
                        Duration = dur,
                        RedirectRatio = skill.BonusRatio > 0.0 ? skill.BonusRatio : 0.30,
                        SpeedBonusRatio = skill.SpeedDelta > 0.0 ? skill.SpeedDelta : 0.25,
                        BuffDuration = skill.SecondaryDuration > 0.0 ? skill.SecondaryDuration : 2.0,
                    });
                    return true;
                }
                case SkillMechanic.Sungka:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 3.0;
                    caster.SungkaAbsorbLeft = dur;
                    caster.SungkaStoredDamage = 0;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.SungkaAbsorb, caster.Position, dur);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 7.0;
                    return true;
                }
                case SkillMechanic.Langit:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 3.0;
                    double radius = skill.Radius > 0.0 ? skill.Radius : 8.0;
                    caster.LangitLeft = dur;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.LangitAura, caster.Position, dur);
                    z.Radius = radius;
                    ApplyLangitAuraTick(sim, caster, radius);
                    return true;
                }
                case SkillMechanic.SackRace:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 3.0;
                    caster.SackRaceLeft = dur;
                    caster.SackBumpMask = 0;
                    return true;
                }
                case SkillMechanic.LuksongBaka:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 4.0;
                    caster.LuksongBakaLeft = dur;
                    caster.LuksongBakaUsedMask = 0;
                    caster.Crouching = true;
                    sim.EndMove(caster);
                    return true;
                }
                case SkillMechanic.TinikWall:
                {
                    double halfW = (skill.Width > 0.0 ? skill.Width : 7.0) * 0.5;
                    int hp = skill.WallHp > 0 ? skill.WallHp : 120;
                    double dur = skill.Duration > 0.0 ? skill.Duration : 4.0;
                    Vec2 along = dir.Orthogonal();
                    DestructibleWall wall = AddWall(sim, caster, skill.Id, aimPoint, along, halfW, hp, dur);
                    Vec2 segA = wall.Center - wall.Along * wall.HalfWidth;
                    Vec2 segB = wall.Center + wall.Along * wall.HalfWidth;
                    double knockDur = skill.EffectDuration > 0.0 ? skill.EffectDuration : 0.9;
                    int riseDmg = skill.Damage > 0 ? skill.Damage : 35;
                    foreach (PlayerState enemy in sim.EnemiesOf(caster.Team))
                    {
                        Vec2 closest = Geometry2D.GetClosestPointToSegment(enemy.Position, segA, segB);
                        if (closest.DistanceTo(enemy.Position) <= enemy.Radius + 0.85)
                        {
                            sim.EmitWeaponHit(enemy.Id, skill.Id);
                            sim.ApplyEffect(enemy.Id, StatusEffects.Type.Airborne, knockDur, 0.0);
                            enemy.VelY = GMath.Maxf(enemy.VelY, 5.0);
                            sim.DamageCombat(enemy.Id, riseDmg, caster.Id, skill.Id, false, true);
                        }
                    }
                    PushPlayersOutOfWall(sim, wall);
                    return true;
                }

                // ---- SEEKERS ----
                case SkillMechanic.TeksTripwire:
                {
                    AddHiddenTrap(sim, caster, skill, aimPoint);
                    return true;
                }
                case SkillMechanic.Tago:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 4.0;
                    caster.InvisibleLeft = dur;
                    caster.EmpoweredTiradorBonus = skill.BonusRatio > 0.0 ? skill.BonusRatio : 0.40;
                    caster.EmpoweredTiradorLeft = dur + (skill.SecondaryDuration > 0.0 ? skill.SecondaryDuration : 3.0);
                    return true;
                }
                case SkillMechanic.Saranggola:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 6.0;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.SaranggolaKite, aimPoint, dur);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 15.0;
                    z.KnownRevealAlert = true;
                    ApplySaranggolaTick(sim, z);
                    return true;
                }
                case SkillMechanic.Yoyo:
                {
                    Projectile yoyo = NewSkillProjectile(caster, skill, dir, slot);
                    yoyo.DamageOverride = skill.Damage > 0 ? skill.Damage : 20;
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.Piko:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 6.0;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.PikoGrid, aimPoint, dur);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 5.0;
                    z.KnownRevealAlert = false; // silent reveal per docs/KITS.md
                    ApplyPikoTick(sim, z);
                    return true;
                }
                case SkillMechanic.PogsSlam:
                {
                    double breakR = skill.Radius > 0.0 ? skill.Radius : 6.0;
                    double revealR = skill.SecondaryRadius > 0.0 ? skill.SecondaryRadius : 14.0;
                    double revealDur = skill.RevealDuration > 0.0 ? skill.RevealDuration : 4.0;
                    int slamDmg = skill.Damage > 0 ? skill.Damage : 38;
                    DisarmTrapsAndSentriesInRadius(sim, caster.Team, aimPoint, breakR);
                    DestroyEnemyWallsInRadius(sim, caster.Team, aimPoint, breakR);
                    foreach (PlayerState enemy in sim.EnemiesOf(caster.Team))
                    {
                        double d = aimPoint.DistanceTo(enemy.Position);
                        if (d <= breakR + enemy.Radius)
                        {
                            sim.EmitWeaponHit(enemy.Id, skill.Id);
                            sim.DamageCombat(enemy.Id, slamDmg, caster.Id, skill.Id, false, true);
                        }
                        if (!enemy.Tagged && d <= revealR + enemy.Radius)
                            EmitSnapshotReveal(sim, caster.Team, enemy, skill.Id, revealDur, true);
                    }
                    return true;
                }
                case SkillMechanic.SiliTubig:
                {
                    double range = skill.MaxRange > 0.0 ? skill.MaxRange : 10.0;
                    double halfAngle = GMath.DegToRad((skill.AngleDegrees > 0.0 ? skill.AngleDegrees : 55.0) * 0.5);
                    double revealDur = skill.RevealDuration > 0.0 ? skill.RevealDuration : 4.0;
                    double blurDur = skill.BlurDuration > 0.0 ? skill.BlurDuration : 1.5;
                    int dmg = skill.Damage > 0 ? skill.Damage : 18;
                    caster.Facing = dir;
                    foreach (PlayerState enemy in sim.EnemiesOf(caster.Team))
                    {
                        Vec2 offset = enemy.Position - caster.Position;
                        if (offset.Length() > range + enemy.Radius) continue;
                        if (offset.Length() > 0.001 && Math.Abs(dir.AngleTo(offset)) > halfAngle) continue;
                        sim.EmitWeaponHit(enemy.Id, skill.Id);
                        ApplyOutlineReveal(sim, caster.Team, enemy, revealDur, true);
                        enemy.BlurLeft = GMath.Maxf(enemy.BlurLeft, blurDur);
                        sim.DamageCombat(enemy.Id, dmg, caster.Id, skill.Id, false, true);
                    }
                    return true;
                }
                case SkillMechanic.TrumpoBantay:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 20.0;
                    Sentries.Add(new Sentry
                    {
                        Id = TakeId(),
                        OwnerId = caster.Id,
                        Team = caster.Team,
                        SkillId = skill.Id,
                        Position = aimPoint,
                        Radius = skill.Radius > 0.0 ? skill.Radius : 8.0,
                        TimeLeft = dur,
                        Duration = dur,
                        PingInterval = skill.Interval > 0.0 ? skill.Interval : 1.0,
                        PingTimer = 0.0,
                        RevealDuration = skill.RevealDuration > 0.0 ? skill.RevealDuration : 1.2,
                        KnownRevealAlert = true,
                        Hp = 1,
                    });
                    return true;
                }
                case SkillMechanic.Taya:
                {
                    double countDur = skill.Delay > 0.0 ? skill.Delay : 3.0;
                    caster.TayaCountLeft = countDur;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.TayaCount, caster.Position, countDur);
                    z.DelayLeft = countDur;
                    z.RevealDuration = skill.RevealDuration > 0.0 ? skill.RevealDuration : 4.0;
                    z.KnownRevealAlert = true;
                    return true;
                }

                // ---- CONTROLLERS ----
                case SkillMechanic.Jackstone:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 5.0;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.JackstoneCircle, aimPoint, dur);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 4.0;
                    z.Interval = skill.Interval > 0.0 ? skill.Interval : 1.0;
                    z.Damage = skill.Damage > 0 ? skill.Damage : 5;
                    z.EffectMagnitude = skill.EffectMagnitude > 0.0 ? skill.EffectMagnitude : 0.30;
                    z.TickTimer = 0.0;
                    return true;
                }
                case SkillMechanic.LuksongLubid:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 10.0;
                    double halfW = (skill.Width > 0.0 ? skill.Width : 6.0) * 0.5;
                    Vec2 along = dir.Orthogonal();
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.LuksongLubidRope, aimPoint - along * halfW, dur);
                    z.EndPosition = aimPoint + along * halfW;
                    z.Along = along;
                    z.HalfWidth = halfW;
                    z.Damage = skill.Damage > 0 ? skill.Damage : 16;
                    z.EffectDuration = skill.EffectDuration > 0.0 ? skill.EffectDuration : 1.0;
                    return true;
                }
                case SkillMechanic.GarterNet:
                {
                    double radius = skill.Radius > 0.0 ? skill.Radius : 6.0;
                    double rootDur = skill.EffectDuration > 0.0 ? skill.EffectDuration : 1.25;
                    double slowDur = skill.SecondaryDuration > 0.0 ? skill.SecondaryDuration : 2.0;
                    double slowMag = skill.EffectMagnitude > 0.0 ? skill.EffectMagnitude : 0.35;
                    int dmg = skill.Damage > 0 ? skill.Damage : 35;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.GarterNet, aimPoint, rootDur);
                    z.Radius = radius;
                    foreach (PlayerState enemy in EnemiesWithin(sim, caster.Team, aimPoint, radius))
                    {
                        sim.EmitWeaponHit(enemy.Id, skill.Id);
                        bool rooted = sim.ApplyEffectAndReport(enemy.Id, StatusEffects.Type.Root, rootDur, 0.0);
                        if (rooted)
                        {
                            enemy.PendingSlowAfterRootLeft = rootDur;
                            enemy.PendingSlowAfterRootDuration = slowDur;
                            enemy.PendingSlowAfterRootMagnitude = slowMag;
                        }
                        else
                        {
                            sim.ApplyEffect(enemy.Id, StatusEffects.Type.Slow, slowDur, slowMag);
                        }
                        sim.DamageCombat(enemy.Id, dmg, caster.Id, skill.Id, false, true);
                    }
                    return true;
                }
                case SkillMechanic.HolensSpill:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 3.0;
                    double range = skill.MaxRange > 0.0 ? skill.MaxRange : 10.0;
                    double angle = skill.AngleDegrees > 0.0 ? skill.AngleDegrees : 50.0;
                    double slipMag = skill.EffectMagnitude > 0.0 ? skill.EffectMagnitude : 0.35;
                    int dmg = skill.Damage > 0 ? skill.Damage : 15;
                    caster.Facing = dir;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.HolensSpillCone, caster.Position, dur);
                    z.Direction = dir;
                    z.Radius = range;
                    z.AngleDegrees = angle;
                    z.EffectMagnitude = slipMag;
                    double halfAngle = GMath.DegToRad(angle * 0.5);
                    foreach (PlayerState enemy in sim.EnemiesOf(caster.Team))
                    {
                        if (enemy.IsOffGround()) continue;
                        Vec2 offset = enemy.Position - caster.Position;
                        if (offset.Length() > range + enemy.Radius) continue;
                        if (offset.Length() > 0.001 && Math.Abs(dir.AngleTo(offset)) > halfAngle) continue;
                        sim.EmitWeaponHit(enemy.Id, skill.Id);
                        sim.ApplyEffect(enemy.Id, StatusEffects.Type.Slip, 0.45, slipMag);
                        sim.DamageCombat(enemy.Id, dmg, caster.Id, skill.Id, false, true);
                    }
                    return true;
                }
                case SkillMechanic.LataMorph:
                {
                    Projectile can = NewSkillProjectile(caster, skill, dir, slot);
                    can.DamageOverride = skill.Damage > 0 ? skill.Damage : 16;
                    caster.Facing = dir;
                    return true;
                }
                case SkillMechanic.PatinteroLines:
                {
                    double dur = skill.Duration > 0.0 ? skill.Duration : 5.0;
                    double halfW = (skill.Width > 0.0 ? skill.Width : 7.0) * 0.5;
                    int count = GMath.Clampi(skill.Count > 0 ? skill.Count : 3, 1, 3);
                    double spacing = skill.SecondaryRadius > 0.0 ? skill.SecondaryRadius : (skill.Radius > 0.0 ? skill.Radius : 2.5);
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.PatinteroLines, aimPoint, dur);
                    z.Direction = dir;
                    z.Along = dir.Orthogonal();
                    z.HalfWidth = halfW;
                    z.Count = count;
                    z.Spacing = spacing;
                    z.Damage = skill.Damage > 0 ? skill.Damage : 15;
                    z.EffectDuration = skill.EffectDuration > 0.0 ? skill.EffectDuration : 0.8;
                    return true;
                }
                case SkillMechanic.PabitinSnare:
                {
                    double delay = skill.Delay > 0.0 ? skill.Delay : 0.5;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.PabitinSnare, aimPoint, delay + 0.6);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 4.0;
                    z.DelayLeft = delay;
                    z.Damage = skill.Damage > 0 ? skill.Damage : 18;
                    z.EffectDuration = skill.EffectDuration > 0.0 ? skill.EffectDuration : 1.2;
                    return true;
                }
                case SkillMechanic.SakoTrap:
                {
                    AddHiddenTrap(sim, caster, skill, aimPoint);
                    return true;
                }
                case SkillMechanic.LupaQuake:
                {
                    double delay = skill.Delay > 0.0 ? skill.Delay : 0.6;
                    SkillZone z = NewSkillZone(caster, skill, SkillZoneKind.LupaQuake, caster.Position, delay + 0.5);
                    z.Radius = skill.Radius > 0.0 ? skill.Radius : 8.0;
                    z.DelayLeft = delay;
                    z.Damage = skill.Damage > 0 ? skill.Damage : 40;
                    z.EffectDuration = skill.EffectDuration > 0.0 ? skill.EffectDuration : 1.0;
                    return true;
                }
            }
            return false;
        }

        public void Step(MatchSim sim, double dt)
        {
            StepThrows(sim, dt);
            StepCones(sim, dt);
            StepProjectiles(sim, dt);
            StepZones(sim, dt);
            StepShields(dt);
            if (Walls.Count > 0 || Traps.Count > 0 || Sentries.Count > 0 || Tethers.Count > 0 || SkillZones.Count > 0 || RevealMarkers.Count > 0)
            {
                StepWalls(dt);
                StepTraps(sim, dt);
                StepSentries(sim, dt);
                StepTethers(sim, dt);
                StepSkillZones(sim, dt);
                StepRevealMarkers(dt);
            }
        }

        /// <summary>Clears everything (e.g. between points).</summary>
        public void Reset()
        {
            Projectiles.Clear();
            Zones.Clear();
            Shields.Clear();
            Walls.Clear();
            Traps.Clear();
            Sentries.Clear();
            Tethers.Clear();
            SkillZones.Clear();
            RevealMarkers.Clear();
            _cones.Clear();
            _throws.Clear();
        }

        /// <summary>When a kid is tagged: keep their walls, traps and sentries alive (docs/SPEC.md §5), end tethers and self-auras.</summary>
        public void OnPlayerTagged(int playerId)
        {
            for (int i = Tethers.Count - 1; i >= 0; i--)
                if (Tethers[i].OwnerId == playerId || Tethers[i].AllyId == playerId)
                    Tethers.RemoveAt(i);
            for (int i = SkillZones.Count - 1; i >= 0; i--)
            {
                SkillZone z = SkillZones[i];
                if (z.OwnerId == playerId && (z.Kind == SkillZoneKind.TrumpoSpin || z.Kind == SkillZoneKind.LangitAura
                    || z.Kind == SkillZoneKind.SungkaAbsorb || z.Kind == SkillZoneKind.PukpokLanding))
                {
                    SkillZones.RemoveAt(i);
                }
            }
        }

        /// <summary>True when `defendingTeam` has an active Jopay ULT Palosebo pole halting enemy capture.</summary>
        public bool HasActivePalosebo(int defendingTeam)
        {
            for (int i = 0; i < SkillZones.Count; i++)
                if (SkillZones[i].Kind == SkillZoneKind.PaloseboPole && SkillZones[i].Team == defendingTeam && SkillZones[i].TimeLeft > 0.0)
                    return true;
            return false;
        }

        /// <summary>Active Oriang Chinese Garter tether protecting `allyId`, or null.</summary>
        public AllyTether TetherForAlly(int allyId)
        {
            for (int i = 0; i < Tethers.Count; i++)
                if (Tethers[i].AllyId == allyId && Tethers[i].TimeLeft > 0.0)
                    return Tethers[i];
            return null;
        }

        /// <summary>Pushes a circle of `radius` out of every active destructible wall (blocks BOTH teams).</summary>
        public Vec2 ResolveWallPushOut(Vec2 pos, double radius)
        {
            if (Walls.Count == 0) return pos;
            Vec2 result = pos;
            double minDist = radius + WallHalfThickness;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < Walls.Count; i++)
                {
                    DestructibleWall w = Walls[i];
                    Vec2 a = w.Center - w.Along * w.HalfWidth;
                    Vec2 b = w.Center + w.Along * w.HalfWidth;
                    Vec2 closest = Geometry2D.GetClosestPointToSegment(result, a, b);
                    Vec2 delta = result - closest;
                    double d2 = delta.LengthSquared();
                    if (d2 >= minDist * minDist) continue;
                    if (d2 > 1e-8)
                    {
                        result = closest + delta / Math.Sqrt(d2) * minDist;
                    }
                    else
                    {
                        Vec2 normal = new Vec2(-w.Along.y, w.Along.x);
                        result = closest + normal * minDist;
                    }
                }
            }
            return result;
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

        /// <summary>Auto-aim stick vector for a Spec SkillDef.</summary>
        public Vec2 AutoAimSkill(MatchSim sim, PlayerState caster, SkillDef skill)
        {
            if (skill.Mechanic == SkillMechanic.ChineseGarter || skill.Mechanic == SkillMechanic.PabitinHeal)
            {
                PlayerState ally = FindBestAllyForTether(sim, caster, caster.Position, skill.MaxRange > 0.0 ? skill.MaxRange : 12.0);
                if (ally != null && skill.MaxRange > 0.0)
                    return ((ally.Position - caster.Position) / skill.MaxRange).LimitLength(1.0);
                return Vec2.Zero;
            }
            double range = skill.MaxRange > 0.0 ? skill.MaxRange : 16.0;
            PlayerState target = sim.NearestEnemy(caster, range);
            if (target != null && target.Position != caster.Position)
            {
                Vec2 offset = target.Position - caster.Position;
                return (offset / range).LimitLength(1.0);
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

        /// <summary>Point of a resolved aim for a SkillDef.</summary>
        public Vec2 AimPointSkill(PlayerState caster, SkillDef skill, Vec2 aim)
        {
            double range = skill.MaxRange > 0.0 ? skill.MaxRange : 12.0;
            return caster.Position + aim.LimitLength(1.0) * range;
        }

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
                if (caster != null && !caster.Tagged)
                {
                    if (queued.Skill != null)
                    {
                        Projectile p = NewSkillProjectile(caster, queued.Skill, queued.Direction, queued.CastSlot);
                        p.DamageOverride = queued.Skill.Damage > 0 ? queued.Skill.Damage : 22;
                    }
                    else
                    {
                        NewProjectile(caster, queued.Def, queued.Direction);
                    }
                }
            }
            _throws = still;
        }

        void StepProjectiles(MatchSim sim, double dt)
        {
            var still = new List<Projectile>();
            // GDScript iterates the array it is given; projectiles launched meanwhile join next tick.
            var current = Projectiles;
            for (int i = 0; i < current.Count; i++)
            {
                Projectile projectile = current[i];
                if (AdvanceProjectile(sim, projectile, projectile.Speed * dt)) still.Add(projectile);
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

                // Karding Yoyo disarms traps/sentries along its path; Tirador breaks seen traps/sentries in 1 stone.
                if (CheckTrapAndSentryHits(sim, projectile)) return false;

                // Destructible walls block BOTH teams' shots (and take damage from enemy shots / ricochet Tira Holens).
                DestructibleWall hitWall = FindHitWall(previous, projectile.Position);
                if (hitWall != null)
                {
                    if (projectile.Skill != null && projectile.Skill.Mechanic == SkillMechanic.TiraHolens && !projectile.Ricocheted)
                    {
                        Vec2 normal = new Vec2(-hitWall.Along.y, hitWall.Along.x);
                        projectile.Direction = (projectile.Direction - normal * (2.0 * projectile.Direction.Dot(normal))).Normalized();
                        projectile.Position = previous;
                        projectile.Ricocheted = true;
                        double mult = projectile.Skill.BonusRatio > 0.0 ? projectile.Skill.BonusRatio : 2.0;
                        projectile.DamageOverride = GMath.Roundi((projectile.Skill.Damage > 0 ? projectile.Skill.Damage : 20) * mult);
                        continue;
                    }
                    if (hitWall.Team != projectile.Team)
                    {
                        int wallDmg = projectile.DamageOverride > 0 ? projectile.DamageOverride
                            : (projectile.Skill != null ? projectile.Skill.Damage : (projectile.Def != null ? projectile.Def.Damage : 20));
                        if (wallDmg > 0) DamageWall(sim, hitWall, wallDmg);
                    }
                    if (IsBoomerangProjectile(projectile) && !projectile.Returning)
                    {
                        projectile.Position = previous;
                        TurnBack(projectile);
                        continue;
                    }
                    return false;
                }

                // Map obstacles & legacy shields
                if (Blocked(projectile, previous, blocking, out Rect2 hitObstacle, out bool hitRect))
                {
                    if (hitRect && projectile.Skill != null && projectile.Skill.Mechanic == SkillMechanic.TiraHolens && !projectile.Ricocheted)
                    {
                        projectile.Direction = ReflectOffRect(previous, projectile.Direction, hitObstacle);
                        projectile.Position = previous;
                        projectile.Ricocheted = true;
                        double mult = projectile.Skill.BonusRatio > 0.0 ? projectile.Skill.BonusRatio : 2.0;
                        projectile.DamageOverride = GMath.Roundi((projectile.Skill.Damage > 0 ? projectile.Skill.Damage : 20) * mult);
                        continue;
                    }
                    if (IsBoomerangProjectile(projectile) && !projectile.Returning)
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

        static bool IsBoomerangProjectile(Projectile p) =>
            (p.Skill != null && p.Skill.Mechanic == SkillMechanic.TsinelasBagsik)
            || (p.Def != null && p.Def.Shape == WeaponDef.WeaponShape.Boomerang);

        static Vec2 ReflectOffRect(Vec2 previous, Vec2 dir, Rect2 rect)
        {
            Vec2 end = rect.End;
            bool outsideX = previous.x < rect.Position.x || previous.x > end.x;
            bool outsideY = previous.y < rect.Position.y || previous.y > end.y;
            if (outsideX && !outsideY) return new Vec2(-dir.x, dir.y).Normalized();
            if (outsideY && !outsideX) return new Vec2(dir.x, -dir.y).Normalized();
            double dx = GMath.Minf(Math.Abs(previous.x - rect.Position.x), Math.Abs(previous.x - end.x));
            double dy = GMath.Minf(Math.Abs(previous.y - rect.Position.y), Math.Abs(previous.y - end.y));
            return (dx <= dy ? new Vec2(-dir.x, dir.y) : new Vec2(dir.x, -dir.y)).Normalized();
        }

        /// <summary>Steers and moves; returns false when the projectile has finished its flight.</summary>
        bool MoveProjectile(MatchSim sim, Projectile projectile, double move)
        {
            if (projectile.Skill != null)
            {
                if (projectile.Skill.Mechanic == SkillMechanic.TsinelasBagsik)
                {
                    if (projectile.Returning)
                    {
                        PlayerState thrower = sim.Players.GetOrDefault(projectile.OwnerId);
                        if (thrower == null || thrower.Tagged) return false;
                        if (projectile.Position.DistanceTo(thrower.Position) <= sim.Rules.BoomerangCatchDistance) return false;
                        projectile.Direction = projectile.Position.DirectionTo(thrower.Position);
                    }
                }
                else if (projectile.Skill.Mechanic == SkillMechanic.Sipa && projectile.TargetId >= 0)
                {
                    PlayerState chainTarget = sim.Players.GetOrDefault(projectile.TargetId);
                    if (chainTarget == null || !sim.IsTargetable(chainTarget)) return false;
                    projectile.Direction = projectile.Position.DirectionTo(chainTarget.Position);
                }

                projectile.Position += projectile.Direction * move;
                projectile.Travelled += move;
                double maxRange = projectile.EffectiveMaxRange;
                if (projectile.Skill.Mechanic == SkillMechanic.TsinelasBagsik)
                {
                    if (!projectile.Returning && projectile.Travelled >= maxRange) TurnBack(projectile);
                }
                else if (projectile.Travelled >= maxRange)
                {
                    return false;
                }
                return true;
            }

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
            double limit = projectile.EffectiveMaxRange;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Boomerang:
                    if (!projectile.Returning && projectile.Travelled >= limit) TurnBack(projectile);
                    break;
                case WeaponDef.WeaponShape.Bouncer:
                {
                    double nextBounce = limit * (double)(projectile.Bounces + 1) / (double)GMath.Maxi(def.Count, 1);
                    if (projectile.Travelled >= nextBounce)
                    {
                        projectile.Bounces += 1;
                        AreaHit(sim, projectile.Team, def, projectile.Position, def.Radius, projectile.OwnerId);
                        if (projectile.Bounces >= def.Count) return false;
                    }
                    break;
                }
                case WeaponDef.WeaponShape.Spinner:
                    if (projectile.Travelled >= limit)
                    {
                        CheckHits(sim, projectile);
                        return false;
                    }
                    break;
                case WeaponDef.WeaponShape.Skillshot:
                    if (projectile.Travelled >= limit) return false;
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
        bool Blocked(Projectile projectile, Vec2 previous, IReadOnlyList<Rect2> blocking, out Rect2 hitRect, out bool wasRect)
        {
            for (int i = 0; i < blocking.Count; i++)
            {
                if (blocking[i].HasPoint(projectile.Position))
                {
                    hitRect = blocking[i];
                    wasRect = true;
                    return true;
                }
            }
            hitRect = default;
            wasRect = false;
            return BlocksSegment(projectile.Team, previous, projectile.Position);
        }

        DestructibleWall FindHitWall(Vec2 a, Vec2 b)
        {
            for (int i = 0; i < Walls.Count; i++)
            {
                DestructibleWall w = Walls[i];
                if (w.TimeLeft <= 0.0 || w.Hp <= 0) continue;
                Vec2 left = w.Center - w.Along * w.HalfWidth;
                Vec2 right = w.Center + w.Along * w.HalfWidth;
                if (Geometry2D.SegmentIntersectsSegment(a, b, left, right)) return w;
            }
            return null;
        }

        void DamageWall(MatchSim sim, DestructibleWall wall, int amount)
        {
            if (amount <= 0 || wall.Hp <= 0) return;
            wall.Hp = GMath.Maxi(0, wall.Hp - amount);
            if (wall.Hp <= 0)
            {
                Vec2 pos = wall.Center;
                int owner = wall.OwnerId;
                Walls.Remove(wall);
                sim.EmitWallBroken(owner, pos);
            }
        }

        /// <summary>Returns true if a Tirador stone or Karding Yoyo destroyed a trap/sentry and was consumed.</summary>
        bool CheckTrapAndSentryHits(MatchSim sim, Projectile projectile)
        {
            bool isYoyo = projectile.Skill != null && projectile.Skill.Mechanic == SkillMechanic.Yoyo;
            if (!projectile.IsTirador && !isYoyo) return false;

            for (int i = Traps.Count - 1; i >= 0; i--)
            {
                HiddenTrap trap = Traps[i];
                if (trap.Team == projectile.Team) continue;
                if (!isYoyo && !sim.IsTrapVisibleToTeam(trap, projectile.Team)) continue;
                if (projectile.Position.DistanceTo(trap.Position) <= 0.65)
                {
                    Vec2 pos = trap.Position;
                    int owner = trap.OwnerId;
                    Traps.RemoveAt(i);
                    sim.EmitTrapTriggered(owner, pos, true);
                    if (projectile.IsTirador) return true;
                }
            }
            for (int i = Sentries.Count - 1; i >= 0; i--)
            {
                Sentry sentry = Sentries[i];
                if (sentry.Team == projectile.Team) continue;
                if (projectile.Position.DistanceTo(sentry.Position) <= 0.70)
                {
                    Vec2 pos = sentry.Position;
                    int owner = sentry.OwnerId;
                    Sentries.RemoveAt(i);
                    sim.EmitTrapTriggered(owner, pos, true);
                    if (projectile.IsTirador) return true;
                }
            }
            return false;
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
            if (projectile.Skill != null) return CheckSkillProjectileHits(sim, projectile);

            if (projectile.IsTirador)
            {
                foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
                {
                    if (projectile.Position.DistanceTo(target.Position) <= projectile.HitRadius + target.Radius)
                    {
                        Vec2 toTarget = target.Position - projectile.Origin;
                        double perpDist = Math.Abs(toTarget.x * (-projectile.Direction.y) + toTarget.y * projectile.Direction.x);
                        double hsRadius = projectile.AimedDown ? sim.Rules.TiradorHeadshotRadius : sim.Rules.TiradorHeadshotRadius * 0.65;
                        bool isHeadshot = target.SlideLeft <= 0.0 && !target.Crouching && perpDist <= hsRadius;
                        int dmg = projectile.DamageOverride > 0 ? projectile.DamageOverride : sim.Rules.TiradorBodyDamage;
                        if (isHeadshot) dmg = GMath.Roundi(dmg * sim.Rules.TiradorHeadshotMultiplier);
                        sim.EmitWeaponHit(target.Id, projectile.EffectId);
                        sim.DamageCombat(target.Id, dmg, projectile.OwnerId, projectile.EffectId, isHeadshot, false);
                        return false;
                    }
                }
                return true;
            }

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

        bool CheckSkillProjectileHits(MatchSim sim, Projectile projectile)
        {
            SkillDef skill = projectile.Skill;
            double hitR = projectile.HitRadius;

            if (skill.Mechanic == SkillMechanic.TsinelasBagsik)
            {
                foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
                {
                    if (projectile.HitIds.Contains(target.Id)) continue;
                    if (projectile.Position.DistanceTo(target.Position) <= hitR + target.Radius)
                    {
                        projectile.HitIds.Add(target.Id);
                        target.TsinelasMarkedLeft = 4.0;
                        target.TsinelasMarkedBy = projectile.OwnerId;
                        sim.EmitWeaponHit(target.Id, skill.Id);
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                    }
                }
                return true;
            }

            foreach (PlayerState target in sim.EnemiesOf(projectile.Team))
            {
                if (projectile.HitIds.Contains(target.Id)) continue;
                if (projectile.TargetId >= 0 && target.Id != projectile.TargetId) continue;
                if (projectile.Position.DistanceTo(target.Position) > hitR + target.Radius) continue;

                sim.EmitWeaponHit(target.Id, skill.Id);
                switch (skill.Mechanic)
                {
                    case SkillMechanic.TiraHolens:
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                        return false;
                    case SkillMechanic.Syato:
                    {
                        double popDur = skill.EffectDuration > 0.0 ? skill.EffectDuration : 0.5;
                        sim.ApplyEffect(target.Id, StatusEffects.Type.Airborne, popDur, 0.0);
                        target.VelY = GMath.Maxf(target.VelY, 3.6);
                        PlayerState caster = sim.Players.GetOrDefault(projectile.OwnerId);
                        if (caster != null && !caster.Tagged)
                        {
                            caster.SyatoTargetId = target.Id;
                            caster.SyatoRecastLeft = skill.RecastWindow > 0.0 ? skill.RecastWindow : 2.0;
                        }
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                        return false;
                    }
                    case SkillMechanic.Sipa:
                    {
                        projectile.HitIds.Add(target.Id);
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                        if (projectile.ChainBouncesLeft <= 0) return false;
                        PlayerState next = FindNearestUnvisitedEnemy(sim, projectile.Team, target.Position, projectile.ChainRange, projectile.HitIds);
                        if (next == null) return false;
                        projectile.ChainBouncesLeft -= 1;
                        projectile.TargetId = next.Id;
                        projectile.Travelled = 0.0;
                        projectile.MaxRangeOverride = projectile.ChainRange + 2.5;
                        projectile.Direction = projectile.Position.DirectionTo(next.Position);
                        return true;
                    }
                    case SkillMechanic.BatoExecute:
                    {
                        double missingRatio = GMath.Clampf((double)(sim.Rules.PlayerMaxHp - target.Hp) / GMath.Maxi(1, sim.Rules.PlayerMaxHp), 0.0, 1.0);
                        int baseDmg = skill.Damage > 0 ? skill.Damage : 35;
                        int maxBonus = skill.SecondaryDamage > 0 ? skill.SecondaryDamage : 45;
                        int totalDmg = baseDmg + GMath.Roundi(maxBonus * missingRatio);
                        sim.DamageCombat(target.Id, totalDmg, projectile.OwnerId, skill.Id, false, true);
                        if (target.Tagged)
                        {
                            int healAmt = skill.Heal > 0 ? skill.Heal : 30;
                            sim.Heal(projectile.OwnerId, healAmt);
                        }
                        return false;
                    }
                    case SkillMechanic.Yoyo:
                    {
                        double revealDur = skill.RevealDuration > 0.0 ? skill.RevealDuration : 5.0;
                        double tug = skill.Knockback > 0.0 ? skill.Knockback : 2.0;
                        ApplyOutlineReveal(sim, projectile.Team, target, revealDur, true);
                        PlayerState caster = sim.Players.GetOrDefault(projectile.OwnerId);
                        if (caster != null && target.Position.DistanceSquaredTo(caster.Position) > 0.01)
                            sim.MoveBy(target, target.Position.DirectionTo(caster.Position) * tug);
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                        return false;
                    }
                    case SkillMechanic.LataMorph:
                    {
                        double morphDur = skill.EffectDuration > 0.0 ? skill.EffectDuration : 1.5;
                        sim.ApplyEffect(target.Id, StatusEffects.Type.Polymorph, morphDur, 0.0);
                        sim.DamageCombat(target.Id, projectile.DamageOverride, projectile.OwnerId, skill.Id, false, true);
                        return false;
                    }
                }
                return false;
            }
            return true;
        }

        PlayerState FindNearestUnvisitedEnemy(MatchSim sim, int team, Vec2 from, double maxRange, HashSet<int> visited)
        {
            PlayerState best = null;
            double bestDist = maxRange;
            foreach (PlayerState enemy in sim.EnemiesOf(team))
            {
                if (visited.Contains(enemy.Id)) continue;
                double d = from.DistanceTo(enemy.Position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            return best;
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

        // ---- spec world entities ----

        void StepWalls(double dt)
        {
            for (int i = Walls.Count - 1; i >= 0; i--)
            {
                Walls[i].TimeLeft -= dt;
                if (Walls[i].TimeLeft <= 0.0 || Walls[i].Hp <= 0)
                    Walls.RemoveAt(i);
            }
        }

        void StepTraps(MatchSim sim, double dt)
        {
            for (int i = Traps.Count - 1; i >= 0; i--)
            {
                HiddenTrap trap = Traps[i];
                trap.TimeLeft -= dt;
                if (trap.TimeLeft <= 0.0)
                {
                    Traps.RemoveAt(i);
                    continue;
                }
                if (!trap.Armed)
                {
                    trap.ArmLeft -= dt;
                    if (trap.ArmLeft <= 0.0) trap.Armed = true;
                    continue;
                }
                PlayerState victim = null;
                foreach (PlayerState enemy in sim.EnemiesOf(trap.Team))
                {
                    if (enemy.IsOffGround()) continue;
                    if (trap.Position.DistanceTo(enemy.Position) <= trap.TriggerRadius + enemy.Radius)
                    {
                        victim = enemy;
                        break;
                    }
                }
                if (victim == null) continue;
                if (trap.Mechanic == SkillMechanic.TeksTripwire)
                {
                    ApplyOutlineReveal(sim, trap.Team, victim, trap.RevealDuration > 0.0 ? trap.RevealDuration : 6.0, true);
                    sim.ApplyEffect(victim.Id, StatusEffects.Type.Slow, trap.EffectDuration > 0.0 ? trap.EffectDuration : 3.0,
                        trap.EffectMagnitude > 0.0 ? trap.EffectMagnitude : 0.20);
                }
                else if (trap.Mechanic == SkillMechanic.SakoTrap)
                {
                    sim.ApplyEffect(victim.Id, StatusEffects.Type.Slow, trap.EffectDuration > 0.0 ? trap.EffectDuration : 3.0,
                        trap.EffectMagnitude > 0.0 ? trap.EffectMagnitude : 0.50);
                }
                sim.EmitTrapTriggered(trap.OwnerId, trap.Position, false);
                Traps.RemoveAt(i);
            }
        }

        void StepSentries(MatchSim sim, double dt)
        {
            for (int i = Sentries.Count - 1; i >= 0; i--)
            {
                Sentry sentry = Sentries[i];
                sentry.TimeLeft -= dt;
                if (sentry.TimeLeft <= 0.0 || sentry.Hp <= 0)
                {
                    Sentries.RemoveAt(i);
                    continue;
                }
                sentry.PingTimer -= dt;
                if (sentry.PingTimer > 0.0) continue;
                sentry.PingTimer += sentry.PingInterval > 0.0 ? sentry.PingInterval : 1.0;
                foreach (PlayerState enemy in sim.EnemiesOf(sentry.Team))
                {
                    if (sentry.Position.DistanceTo(enemy.Position) <= sentry.Radius + enemy.Radius)
                        EmitSnapshotReveal(sim, sentry.Team, enemy, sentry.SkillId, sentry.RevealDuration > 0.0 ? sentry.RevealDuration : 1.2, true);
                }
            }
        }

        void StepTethers(MatchSim sim, double dt)
        {
            for (int i = Tethers.Count - 1; i >= 0; i--)
            {
                AllyTether tether = Tethers[i];
                tether.TimeLeft -= dt;
                PlayerState owner = sim.Players.GetOrDefault(tether.OwnerId);
                PlayerState ally = sim.Players.GetOrDefault(tether.AllyId);
                if (tether.TimeLeft <= 0.0 || owner == null || ally == null || owner.Tagged || ally.Tagged)
                {
                    Tethers.RemoveAt(i);
                    continue;
                }
                if (owner.Position.DistanceTo(ally.Position) > tether.MaxRange)
                {
                    owner.SpeedBoostRatio = GMath.Maxf(owner.SpeedBoostRatio, tether.SpeedBonusRatio);
                    owner.SpeedBoostLeft = GMath.Maxf(owner.SpeedBoostLeft, tether.BuffDuration);
                    ally.SpeedBoostRatio = GMath.Maxf(ally.SpeedBoostRatio, tether.SpeedBonusRatio);
                    ally.SpeedBoostLeft = GMath.Maxf(ally.SpeedBoostLeft, tether.BuffDuration);
                    Tethers.RemoveAt(i);
                }
            }
        }

        void StepSkillZones(MatchSim sim, double dt)
        {
            for (int i = SkillZones.Count - 1; i >= 0; i--)
            {
                SkillZone z = SkillZones[i];
                z.TimeLeft -= dt;
                if (z.DelayLeft > 0.0) z.DelayLeft = GMath.Maxf(0.0, z.DelayLeft - dt);

                switch (z.Kind)
                {
                    case SkillZoneKind.PabitinHeal:
                        z.TickTimer -= dt;
                        if (z.TickTimer <= 0.0)
                        {
                            z.TickTimer += z.Interval > 0.0 ? z.Interval : 0.5;
                            foreach (var pair in sim.Players)
                            {
                                PlayerState ally = pair.Value;
                                if (ally.Team == z.Team && !ally.Tagged && z.Position.DistanceTo(ally.Position) <= z.Radius + ally.Radius)
                                    sim.Heal(ally.Id, z.Heal);
                            }
                        }
                        break;

                    case SkillZoneKind.PaloseboPole:
                        foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                            if (enemy.Effects.CanMove() && enemy.Effects.CanCast())
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Slip, 0.35, z.EffectMagnitude);
                        break;

                    case SkillZoneKind.SaranggolaKite:
                        ApplySaranggolaTick(sim, z);
                        break;

                    case SkillZoneKind.PikoGrid:
                        ApplyPikoTick(sim, z);
                        break;

                    case SkillZoneKind.JackstoneCircle:
                        foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                            if (!enemy.IsOffGround() && enemy.Effects.CanMove() && enemy.Effects.CanCast())
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Slow, 0.35, z.EffectMagnitude);
                        z.TickTimer -= dt;
                        if (z.TickTimer <= 0.0)
                        {
                            z.TickTimer += z.Interval > 0.0 ? z.Interval : 1.0;
                            foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                                if (!enemy.IsOffGround())
                                {
                                    sim.EmitWeaponHit(enemy.Id, z.SkillId);
                                    sim.DamageCombat(enemy.Id, z.Damage, z.OwnerId, z.SkillId, false, true);
                                }
                        }
                        break;

                    case SkillZoneKind.LuksongLubidRope:
                        foreach (PlayerState enemy in sim.EnemiesOf(z.Team))
                        {
                            if (enemy.IsOffGround()) continue;
                            Vec2 closest = Geometry2D.GetClosestPointToSegment(enemy.Position, z.Position, z.EndPosition);
                            if (closest.DistanceTo(enemy.Position) <= enemy.Radius + 0.35)
                            {
                                sim.EmitWeaponHit(enemy.Id, z.SkillId);
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Tripped, z.EffectDuration, 0.0);
                                sim.DamageCombat(enemy.Id, z.Damage, z.OwnerId, z.SkillId, false, true);
                                z.TimeLeft = 0.0;
                                break;
                            }
                        }
                        break;

                    case SkillZoneKind.HolensSpillCone:
                    {
                        double halfAngle = GMath.DegToRad(z.AngleDegrees * 0.5);
                        foreach (PlayerState enemy in sim.EnemiesOf(z.Team))
                        {
                            if (enemy.IsOffGround()) continue;
                            Vec2 offset = enemy.Position - z.Position;
                            if (offset.Length() > z.Radius + enemy.Radius) continue;
                            if (offset.Length() > 0.001 && Math.Abs(z.Direction.AngleTo(offset)) > halfAngle) continue;
                            if (enemy.Effects.CanMove() && enemy.Effects.CanCast())
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Slip, 0.35, z.EffectMagnitude);
                        }
                        break;
                    }

                    case SkillZoneKind.PatinteroLines:
                    {
                        for (int lineIdx = 0; lineIdx < z.Count; lineIdx++)
                        {
                            double offsetDist = (lineIdx - (z.Count - 1) * 0.5) * z.Spacing;
                            Vec2 center = z.Position + z.Direction * offsetDist;
                            Vec2 a = center - z.Along * z.HalfWidth;
                            Vec2 b = center + z.Along * z.HalfWidth;
                            foreach (PlayerState enemy in sim.EnemiesOf(z.Team))
                            {
                                int bit = 1 << (enemy.Id & 31);
                                if (LineHasBit(z, lineIdx, bit)) continue;
                                Vec2 closest = Geometry2D.GetClosestPointToSegment(enemy.Position, a, b);
                                if (closest.DistanceTo(enemy.Position) <= enemy.Radius + 0.35)
                                {
                                    SetLineBit(z, lineIdx, bit);
                                    sim.EmitWeaponHit(enemy.Id, z.SkillId);
                                    sim.ApplyEffect(enemy.Id, StatusEffects.Type.Stun, z.EffectDuration, 0.0);
                                    sim.DamageCombat(enemy.Id, z.Damage, z.OwnerId, z.SkillId, false, true);
                                }
                            }
                        }
                        break;
                    }

                    case SkillZoneKind.PabitinSnare:
                        if (!z.Triggered && z.DelayLeft <= 0.0)
                        {
                            z.Triggered = true;
                            foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                            {
                                sim.EmitWeaponHit(enemy.Id, z.SkillId);
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Root, z.EffectDuration, 0.0);
                                sim.DamageCombat(enemy.Id, z.Damage, z.OwnerId, z.SkillId, false, true);
                            }
                        }
                        break;

                    case SkillZoneKind.LupaQuake:
                        if (!z.Triggered && z.DelayLeft <= 0.0)
                        {
                            z.Triggered = true;
                            foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                            {
                                if (enemy.IsOffGround()) continue; // elevated / jumping / Tiyakad kids are safe!
                                sim.EmitWeaponHit(enemy.Id, z.SkillId);
                                sim.ApplyEffect(enemy.Id, StatusEffects.Type.Stun, z.EffectDuration, 0.0);
                                enemy.VelY = GMath.Maxf(enemy.VelY, 4.8);
                                sim.DamageCombat(enemy.Id, z.Damage, z.OwnerId, z.SkillId, false, true);
                            }
                        }
                        break;

                    case SkillZoneKind.TayaCount:
                    {
                        PlayerState manoy = sim.Players.GetOrDefault(z.OwnerId);
                        if (manoy != null) z.Position = manoy.Position;
                        if (!z.Triggered && z.DelayLeft <= 0.0)
                        {
                            z.Triggered = true;
                            double revDur = z.RevealDuration > 0.0 ? z.RevealDuration : 4.0;
                            foreach (PlayerState enemy in sim.EnemiesOf(z.Team))
                                EmitSnapshotReveal(sim, z.Team, enemy, z.SkillId, revDur, true);
                        }
                        break;
                    }

                    case SkillZoneKind.LangitAura:
                    {
                        PlayerState oriang = sim.Players.GetOrDefault(z.OwnerId);
                        if (oriang == null || oriang.Tagged) { z.TimeLeft = 0.0; break; }
                        z.Position = oriang.Position;
                        ApplyLangitAuraTick(sim, oriang, z.Radius);
                        break;
                    }

                    case SkillZoneKind.TrumpoSpin:
                    {
                        PlayerState jumong = sim.Players.GetOrDefault(z.OwnerId);
                        if (jumong == null || jumong.Tagged) { z.TimeLeft = 0.0; break; }
                        z.Position = jumong.Position;
                        break;
                    }

                    case SkillZoneKind.SungkaAbsorb:
                    {
                        PlayerState oriang = sim.Players.GetOrDefault(z.OwnerId);
                        if (oriang == null || oriang.Tagged) { z.TimeLeft = 0.0; break; }
                        z.Position = oriang.Position;
                        break;
                    }
                }

                if (z.TimeLeft <= 0.0) SkillZones.RemoveAt(i);
            }
        }

        void StepRevealMarkers(double dt)
        {
            for (int i = RevealMarkers.Count - 1; i >= 0; i--)
            {
                RevealMarkers[i].TimeLeft -= dt;
                if (RevealMarkers[i].TimeLeft <= 0.0) RevealMarkers.RemoveAt(i);
            }
        }

        static bool LineHasBit(SkillZone z, int idx, int bit)
        {
            if (idx == 0) return (z.HitLineMask0 & bit) != 0;
            if (idx == 1) return (z.HitLineMask1 & bit) != 0;
            return (z.HitLineMask2 & bit) != 0;
        }

        static void SetLineBit(SkillZone z, int idx, int bit)
        {
            if (idx == 0) z.HitLineMask0 |= bit;
            else if (idx == 1) z.HitLineMask1 |= bit;
            else z.HitLineMask2 |= bit;
        }

        void ApplyLangitAuraTick(MatchSim sim, PlayerState oriang, double radius)
        {
            foreach (var pair in sim.Players)
            {
                PlayerState ally = pair.Value;
                if (ally.Team == oriang.Team && !ally.Tagged && oriang.Position.DistanceTo(ally.Position) <= radius + ally.Radius)
                    ally.LangitLeft = GMath.Maxf(ally.LangitLeft, 0.15);
            }
        }

        void ApplySaranggolaTick(MatchSim sim, SkillZone z)
        {
            foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                ApplyOutlineReveal(sim, z.Team, enemy, 0.25, true);
            for (int i = 0; i < Traps.Count; i++)
                if (Traps[i].Team != z.Team && z.Position.DistanceTo(Traps[i].Position) <= z.Radius)
                    Traps[i].RevealedToEnemy = true;
        }

        void ApplyPikoTick(MatchSim sim, SkillZone z)
        {
            DisarmTrapsAndSentriesInRadius(sim, z.Team, z.Position, z.Radius);
            foreach (PlayerState enemy in EnemiesWithin(sim, z.Team, z.Position, z.Radius))
                ApplyOutlineReveal(sim, z.Team, enemy, 0.25, false); // silent: KnownRevealAlert = false
        }

        public void ApplyOutlineReveal(MatchSim sim, int viewerTeam, PlayerState enemy, double duration, bool knownAlert)
        {
            bool fresh = enemy.RevealedOutlineLeft <= 0.0 || (knownAlert && !enemy.KnownRevealAlert);
            enemy.RevealedOutlineLeft = GMath.Maxf(enemy.RevealedOutlineLeft, duration);
            if (knownAlert)
            {
                enemy.RevealAlertLeft = GMath.Maxf(enemy.RevealAlertLeft, duration);
                enemy.KnownRevealAlert = true;
            }
            if (fresh) sim.EmitPlayerRevealed(enemy.Id, viewerTeam, RevealKind.Outline, knownAlert);
        }

        public void EmitSnapshotReveal(MatchSim sim, int viewerTeam, PlayerState enemy, string skillId, double duration, bool knownAlert)
        {
            RevealMarkers.Add(new RevealMarker
            {
                Id = TakeId(),
                Team = viewerTeam,
                TargetId = enemy.Id,
                SkillId = skillId,
                Position = enemy.Position,
                TimeLeft = duration,
                Duration = duration,
            });
            if (knownAlert)
            {
                enemy.RevealAlertLeft = GMath.Maxf(enemy.RevealAlertLeft, duration);
                enemy.KnownRevealAlert = true;
            }
            sim.EmitPlayerRevealed(enemy.Id, viewerTeam, RevealKind.Snapshot, knownAlert);
        }

        void DisarmTrapsAndSentriesInRadius(MatchSim sim, int team, Vec2 center, double radius)
        {
            for (int i = Traps.Count - 1; i >= 0; i--)
            {
                if (Traps[i].Team == team) continue;
                if (center.DistanceTo(Traps[i].Position) <= radius)
                {
                    Vec2 pos = Traps[i].Position;
                    int owner = Traps[i].OwnerId;
                    Traps.RemoveAt(i);
                    sim.EmitTrapTriggered(owner, pos, true);
                }
            }
            for (int i = Sentries.Count - 1; i >= 0; i--)
            {
                if (Sentries[i].Team == team) continue;
                if (center.DistanceTo(Sentries[i].Position) <= radius)
                {
                    Vec2 pos = Sentries[i].Position;
                    int owner = Sentries[i].OwnerId;
                    Sentries.RemoveAt(i);
                    sim.EmitTrapTriggered(owner, pos, true);
                }
            }
        }

        void DestroyEnemyWallsInRadius(MatchSim sim, int team, Vec2 center, double radius)
        {
            for (int i = Walls.Count - 1; i >= 0; i--)
            {
                DestructibleWall w = Walls[i];
                if (w.Team == team) continue;
                Vec2 a = w.Center - w.Along * w.HalfWidth;
                Vec2 b = w.Center + w.Along * w.HalfWidth;
                if (Geometry2D.GetClosestPointToSegment(center, a, b).DistanceTo(center) <= radius)
                {
                    Vec2 pos = w.Center;
                    int owner = w.OwnerId;
                    Walls.RemoveAt(i);
                    sim.EmitWallBroken(owner, pos);
                }
            }
        }

        DestructibleWall AddWall(MatchSim sim, PlayerState caster, string skillId, Vec2 center, Vec2 along, double halfWidth, int hp, double duration)
        {
            var wall = new DestructibleWall
            {
                Id = TakeId(),
                OwnerId = caster.Id,
                Team = caster.Team,
                SkillId = skillId,
                Center = sim.Layout.ClampToArena(center, 0.5),
                Along = along.LengthSquared() > 0.001 ? along.Normalized() : new Vec2(1.0, 0.0),
                HalfWidth = halfWidth,
                Hp = hp,
                MaxHp = hp,
                TimeLeft = duration,
                Duration = duration,
            };
            Walls.Add(wall);
            return wall;
        }

        void PushPlayersOutOfWall(MatchSim sim, DestructibleWall wall)
        {
            Vec2 a = wall.Center - wall.Along * wall.HalfWidth;
            Vec2 b = wall.Center + wall.Along * wall.HalfWidth;
            foreach (var pair in sim.Players)
            {
                PlayerState p = pair.Value;
                if (p.Tagged) continue;
                Vec2 closest = Geometry2D.GetClosestPointToSegment(p.Position, a, b);
                double minDist = p.Radius + WallHalfThickness;
                Vec2 delta = p.Position - closest;
                if (delta.LengthSquared() < minDist * minDist)
                {
                    Vec2 push = delta.LengthSquared() > 1e-8 ? delta.Normalized() : new Vec2(-wall.Along.y, wall.Along.x);
                    p.Position = sim.Layout.ClampToArena(closest + push * minDist, p.Radius);
                }
            }
        }

        void AddHiddenTrap(MatchSim sim, PlayerState caster, SkillDef skill, Vec2 pos)
        {
            int limit = GMath.Maxi(1, sim.Rules.TrapMaxPerPlayer);
            int owned = 0;
            int oldestIdx = -1;
            for (int i = 0; i < Traps.Count; i++)
            {
                if (Traps[i].OwnerId == caster.Id)
                {
                    owned++;
                    if (oldestIdx < 0) oldestIdx = i;
                }
            }
            if (owned >= limit && oldestIdx >= 0) Traps.RemoveAt(oldestIdx);

            Traps.Add(new HiddenTrap
            {
                Id = TakeId(),
                OwnerId = caster.Id,
                Team = caster.Team,
                SkillId = skill.Id,
                Mechanic = skill.Mechanic,
                Position = sim.Layout.ClampToArena(pos, 0.5),
                TriggerRadius = skill.Radius > 0.0 ? skill.Radius : 1.6,
                TimeLeft = 9999.0, // lasts until round end per docs/KITS.md
                ArmLeft = 0.4,
                Armed = false,
                RevealedToEnemy = false,
                EffectDuration = skill.EffectDuration > 0.0 ? skill.EffectDuration : 3.0,
                EffectMagnitude = skill.EffectMagnitude > 0.0 ? skill.EffectMagnitude : (skill.Mechanic == SkillMechanic.SakoTrap ? 0.50 : 0.20),
                RevealDuration = skill.RevealDuration,
                KnownRevealAlert = skill.KnownRevealAlert,
            });
        }

        PlayerState FindBestAllyForTether(MatchSim sim, PlayerState caster, Vec2 aimPoint, double maxRange)
        {
            PlayerState best = null;
            double bestScore = double.PositiveInfinity;
            foreach (var pair in sim.Players)
            {
                PlayerState ally = pair.Value;
                if (ally.Id == caster.Id || ally.Team != caster.Team || ally.Tagged) continue;
                double dFromCaster = caster.Position.DistanceTo(ally.Position);
                if (dFromCaster > maxRange) continue;
                double dFromAim = aimPoint.DistanceTo(ally.Position);
                if (dFromAim < bestScore)
                {
                    bestScore = dFromAim;
                    best = ally;
                }
            }
            return best;
        }

        void RemoveTethersOwnedBy(int ownerId)
        {
            for (int i = Tethers.Count - 1; i >= 0; i--)
                if (Tethers[i].OwnerId == ownerId) Tethers.RemoveAt(i);
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

        /// <summary>Launches a delayed Inggo ULT Bato! stone after wind-up completes.</summary>
        public Projectile LaunchBatoExecute(PlayerState caster, SkillDef skill, Vec2 direction)
        {
            Projectile p = NewSkillProjectile(caster, skill, direction, 2);
            p.DamageOverride = skill.Damage > 0 ? skill.Damage : 35;
            return p;
        }

        Projectile NewProjectile(PlayerState caster, WeaponDef def, Vec2 direction)
        {
            var projectile = new Projectile
            {
                Id = TakeId(), OwnerId = caster.Id, Team = caster.Team, Def = def, Position = caster.Position, Origin = caster.Position, Direction = direction,
            };
            Projectiles.Add(projectile);
            return projectile;
        }

        Projectile NewSkillProjectile(PlayerState caster, SkillDef skill, Vec2 direction, int slot)
        {
            var projectile = new Projectile
            {
                Id = TakeId(),
                OwnerId = caster.Id,
                Team = caster.Team,
                Skill = skill,
                Position = caster.Position,
                Origin = caster.Position,
                Direction = direction.LengthSquared() > 0.0001 ? direction.Normalized() : caster.Facing,
                CastSlot = slot,
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

        SkillZone NewSkillZone(PlayerState caster, SkillDef skill, SkillZoneKind kind, Vec2 position, double duration)
        {
            var zone = new SkillZone
            {
                Id = TakeId(),
                OwnerId = caster.Id,
                Team = caster.Team,
                SkillId = skill.Id,
                Kind = kind,
                Position = position,
                EndPosition = position,
                Direction = caster.Facing,
                Along = caster.Facing.Orthogonal(),
                Radius = skill.Radius,
                HalfWidth = skill.Width * 0.5,
                AngleDegrees = skill.AngleDegrees,
                TimeLeft = duration,
                Duration = duration,
                RevealDuration = skill.RevealDuration,
                KnownRevealAlert = skill.KnownRevealAlert,
            };
            SkillZones.Add(zone);
            return zone;
        }

        int TakeId()
        {
            _nextId += 1;
            return _nextId;
        }
    }
}
