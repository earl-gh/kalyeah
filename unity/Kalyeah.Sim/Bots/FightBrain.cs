using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A practice sparring partner (port of scripts/sim/fight_brain.gd): a training dummy that fights back. It keeps its spot until an
    /// enemy comes within `BotAggroRange`, then closes to a standoff distance, strafes, and taps its weapons the way a finger would (press
    /// one tick, release the next, aimed at the enemy with a little error). A hurt one heals itself, a free one fetches a tagged teammate
    /// when no enemy is close. Same PlayerInput as everyone else, no RNG of its own (the match's seeded one for the aim error).
    /// When `state.UsingSpecKit` is active (docs/KITS.md, docs/SPEC.md §7), plays a full role-aware tactical brain across all 4 roles
    /// (Striker, Defender, Seeker, Controller) and all 36 kid skills, managing Water fountain refills and Tirador 7-shot magazine + charge range.
    /// When `state.UsingSpecKit` is false, preserves 100% bit-for-bit legacy golden behavior.
    /// </summary>
    public class FightBrain : DummyBrain
    {
        /// <summary>Below this share of health a bot heals itself, and an enemy this hurt is worth going after first.</summary>
        public const double HealBelow = 0.5;
        /// <summary>Defender ally-protection threshold (55% HP): drop walls, heals, tethers, or shields.</summary>
        public const double AllyProtectBelow = 0.55;
        /// <summary>Critical ally HP threshold (40% HP): triggers Oriang Langit! invulnerability save.</summary>
        public const double AllyCriticalBelow = 0.40;
        /// <summary>Low water threshold below which a safe bot dips to home base fountain to refill.</summary>
        public const double WaterLowThreshold = 25.0;
        /// <summary>Target water level before leaving home base fountain during a refill trip.</summary>
        public const double WaterRefillTarget = 75.0;
        /// <summary>Minimum clear distance from visible enemies to safely step back for a fountain water refill.</summary>
        public const double WaterSafeDistance = 16.0;
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
        /// <summary>True while a spec-kit bot is dipping back to its own base fountain to refill low Water.</summary>
        protected bool _refillingWater;
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
        double _proactiveCooldown;
        bool _chargingPrimary;
        int _primaryHoldTicks;
        int _primaryChargeTicksGoal;
        int _primaryTargetId = -1;
        Vec2 _primaryStaticTarget = Vec2.Inf;
        readonly Dictionary<int, Vec2> _seen = new Dictionary<int, Vec2>();
        readonly Dictionary<int, Vec2> _velocity = new Dictionary<int, Vec2>();

        public override bool IsFighter => true;

        /// <summary>A fighter that waits at `spot`.</summary>
        public static FightBrain Fighter(Vec2 spot) => new FightBrain { Mode = BrainMode.Stand, Home = spot };

        public override PlayerInput Think(PlayerState state, MatchSim sim)
        {
            double dt = sim.Rules.TickDt();
            _cooldown = GMath.Maxf(0.0, _cooldown - dt);
            _stayOutLeft = GMath.Maxf(0.0, _stayOutLeft - dt);
            _pinned = state.Position.DistanceSquaredTo(_lastPosition) < 0.0001 ? _pinned + 1 : 0;
            _lastPosition = state.Position;
            Track(state, sim);
            if (state.UsingSpecKit) return ThinkSpecKit(state, sim, dt);
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

        // =========================================================================
        // SPEC-KIT ROLE-AWARE TACTICAL AI (docs/KITS.md + docs/SPEC.md §7)
        // =========================================================================

        PlayerInput ThinkSpecKit(PlayerState state, MatchSim sim, double dt)
        {
            _proactiveCooldown = GMath.Maxf(0.0, _proactiveCooldown - dt);
            // Complete an in-progress 2-tick S1/S2 press->release cast
            if (_pressSlot >= 0)
            {
                _pressSlot = -1;
                if (!state.CanAct() || sim.CastingBlocked(state))
                    return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnAimCancel, sim.Tick);
                PlayerInput release = PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick);
                return _tapRelease ? release : release.WithAimPoint(_aimPoint);
            }
            // Hold a live rescue (moving or casting cancels spec rescue)
            if (RescueDuty == 1 && RescueDue(state, sim) && sim.Objectives.InRescueArea(sim, state))
            {
                _chargingPrimary = false;
                _primaryHoldTicks = 0;
                return NoEnemy(state, sim);
            }
            CharacterRole role = RoleOf(state, sim);
            double engageReach = EngageRange(sim);
            if (state.StiltsLeft > 0.0) engageReach = GMath.Maxf(engageReach, EffectiveTiradorRange(state, sim, true));
            else if (state.CharacterId == "inggo" && sim.CanCastSkill(state, 2))
            {
                SkillDef ult = sim.GetPlayerSkill(state, 2);
                if (ult != null) engageReach = GMath.Maxf(engageReach, ult.MaxRange);
            }
            PlayerState enemy = PickSpecEnemy(state, sim, engageReach, role);
            // Continue or release an active Tirador hold-to-charge pull
            if (_chargingPrimary)
            {
                PlayerInput chargeStep = StepPrimaryCharge(state, sim, enemy, role);
                if (chargeStep != null) return chargeStep;
            }
            if (enemy == null)
            {
                // Out of combat: support injured allies, drop Seeker/Controller vision & choke traps, break visible traps, or reload cleanly
                PlayerInput utility = TryOutOfCombatUtility(state, sim, role);
                if (utility != null) return utility;
                PlayerInput idleMove = NoEnemy(state, sim);
                if (state.CanAct() && state.TiradorReloadLeft <= 0.0 && state.TiradorAmmo < sim.Rules.TiradorMaxAmmo)
                    idleMove.Buttons |= PlayerInput.BtnReload;
                return idleMove;
            }
            _refillingWater = false;
            if (RescueDuty != 1) _rescuing = false;
            return FightSpecKit(state, sim, enemy, role);
        }

        PlayerInput StepPrimaryCharge(PlayerState state, MatchSim sim, PlayerState fallbackEnemy, CharacterRole role)
        {
            if (!state.CanAct() || sim.CastingBlocked(state) || state.TiradorAmmo <= 0 || state.TiradorReloadLeft > 0.0)
            {
                _chargingPrimary = false;
                _primaryHoldTicks = 0;
                PlayerInput cancel = fallbackEnemy != null ? BuildCombatMove(state, sim, fallbackEnemy, role, false) : NoEnemy(state, sim);
                cancel.Buttons |= PlayerInput.BtnAimCancel;
                return cancel;
            }
            Vec2 targetPos;
            bool isPlayerTarget = _primaryTargetId >= 0;
            PlayerState liveTarget = null;
            if (isPlayerTarget)
            {
                if (!sim.Players.TryGetValue(_primaryTargetId, out liveTarget) || liveTarget.Tagged
                    || !sim.IsEnemyVisibleToTeam(liveTarget, state.Team))
                {
                    _chargingPrimary = false;
                    _primaryHoldTicks = 0;
                    PlayerInput cancel = fallbackEnemy != null ? BuildCombatMove(state, sim, fallbackEnemy, role, false) : NoEnemy(state, sim);
                    cancel.Buttons |= PlayerInput.BtnAimCancel;
                    return cancel;
                }
                targetPos = liveTarget.Position;
            }
            else
            {
                if (!_primaryStaticTarget.IsFinite())
                {
                    _chargingPrimary = false;
                    _primaryHoldTicks = 0;
                    return null;
                }
                targetPos = _primaryStaticTarget;
            }
            double dist = state.Position.DistanceTo(targetPos);
            double maxRange = EffectiveTiradorRange(state, sim, true);
            bool blocked = state.StiltsLeft <= 0.0 && InCoverSpec(state.Position, targetPos, sim);
            if (blocked || dist > maxRange * 1.04)
            {
                _chargingPrimary = false;
                _primaryHoldTicks = 0;
                PlayerInput cancel = liveTarget != null ? BuildCombatMove(state, sim, liveTarget, role, blocked) : NoEnemy(state, sim);
                cancel.Buttons |= PlayerInput.BtnAimCancel;
                return cancel;
            }
            // Interrupt charge immediately if Defender needs an emergency Langit! save
            if (role == CharacterRole.Defender && state.CharacterId == "oriang" && sim.CanCastSkill(state, 2)
                && FindHurtAlly(state, sim, 8.0, AllyCriticalBelow, true) != null)
            {
                _chargingPrimary = false;
                _primaryHoldTicks = 0;
                PlayerInput cancel = liveTarget != null ? BuildCombatMove(state, sim, liveTarget, role, false) : NoEnemy(state, sim);
                cancel.Buttons |= PlayerInput.BtnAimCancel;
                return cancel;
            }
            Vec2 aimPt = isPlayerTarget
                ? LeadAimPoint(state, liveTarget, sim.Rules.TiradorProjectileSpeed, 0.0, maxRange, sim)
                : targetPos;
            PlayerInput moveOut = liveTarget != null ? BuildCombatMove(state, sim, liveTarget, role, false) : NoEnemy(state, sim);
            int maxTicks = GMath.Ceili(GMath.Maxf(sim.Rules.TiradorChargeSeconds, 0.1) / sim.Rules.TickDt()) + 1;
            if (_primaryHoldTicks >= _primaryChargeTicksGoal || _primaryHoldTicks >= maxTicks)
            {
                // Release BtnPrimary with live aim point to fire the Tirador stone
                _chargingPrimary = false;
                _primaryHoldTicks = 0;
                moveOut.Buttons &= ~PlayerInput.BtnPrimary;
                moveOut.WithAimPoint(aimPt);
                return moveOut;
            }
            _primaryHoldTicks++;
            moveOut.Buttons |= PlayerInput.BtnPrimary;
            if (dist > EffectiveTiradorRange(state, sim, false) * 0.85 && EffectiveSkill(sim) >= 0.4)
                moveOut.Buttons |= PlayerInput.BtnAds;
            moveOut.WithAimPoint(aimPt);
            return moveOut;
        }

        PlayerInput TryOutOfCombatUtility(PlayerState state, MatchSim sim, CharacterRole role)
        {
            if (!state.CanAct() || sim.CastingBlocked(state) || HoldsZone(state, sim)) return null;
            // Defenders heal hurt teammates or link up even after a skirmish clears
            if (role == CharacterRole.Defender && _cooldown <= 0.0)
            {
                if (state.CharacterId == "jopay" && sim.CanCastSkill(state, 1))
                {
                    SkillDef healSkill = sim.GetPlayerSkill(state, 1);
                    PlayerState hurt = FindHurtAlly(state, sim, healSkill != null ? healSkill.MaxRange : 14.0, AllyProtectBelow, true);
                    if (hurt != null) return BeginSkillCast(state, sim, 1, hurt.Position, false);
                }
                else if (state.CharacterId == "oriang" && sim.CanCastSkill(state, 0) && !HasActiveTether(state, sim) && state.Hp >= 45)
                {
                    SkillDef tether = sim.GetPlayerSkill(state, 0);
                    PlayerState ally = FindRescuingAlly(state, sim, tether != null ? tether.MaxRange : 12.0)
                        ?? FindHurtAlly(state, sim, tether != null ? tether.MaxRange : 12.0, AllyProtectBelow, false);
                    if (ally != null) return BeginSkillCast(state, sim, 0, ally.Position, false);
                }
                else if (state.CharacterId == "paeng" && sim.CanCastSkill(state, 1))
                {
                    PlayerState rescuer = FindRescuingAlly(state, sim, 5.0);
                    if (rescuer != null) return BeginSkillCast(state, sim, 1, state.Position, true);
                }
            }
            // Seekers & Controllers proactively deploy vision, choke traps, or clear visible traps when water is comfortable
            if (_proactiveCooldown <= 0.0 && _cooldown <= 0.0)
            {
                if (role == CharacterRole.Seeker)
                {
                    PlayerInput seekerAct = TrySeekerProactive(state, sim);
                    if (seekerAct != null)
                    {
                        _proactiveCooldown = 3.5;
                        return seekerAct;
                    }
                }
                else if (role == CharacterRole.Controller && state.CharacterId == "domeng" && sim.CanCastSkill(state, 1)
                    && state.Water >= 55.0 && CountOwnTraps(state, sim) < sim.Rules.TrapMaxPerPlayer)
                {
                    Vec2 choke = ChokeSpotNear(state, sim, 9.0);
                    if (choke.IsFinite() && !HasFriendlyTrapNear(state.Team, choke, 6.0, sim))
                    {
                        _proactiveCooldown = 4.0;
                        return BeginSkillCast(state, sim, 1, choke, false);
                    }
                }
            }
            // Break a visible enemy trap or sentry with 1 Tirador stone (docs/KITS.md L40)
            if (state.TiradorAmmo > 0 && state.TiradorReloadLeft <= 0.0 && state.PrimaryCd <= 0.0)
            {
                double minRange = EffectiveTiradorRange(state, sim, false);
                Vec2 hazard = FindBreakableEnemyUtility(state, sim, minRange * 0.92);
                if (hazard.IsFinite() && !InCoverSpec(state.Position, hazard, sim))
                {
                    _chargingPrimary = true;
                    _primaryHoldTicks = 1;
                    _primaryChargeTicksGoal = 1;
                    _primaryTargetId = -1;
                    _primaryStaticTarget = hazard;
                    PlayerInput output = NoEnemy(state, sim);
                    output.Buttons |= PlayerInput.BtnPrimary;
                    output.WithAimPoint(hazard);
                    return output;
                }
            }
            return null;
        }

        PlayerInput TrySeekerProactive(PlayerState state, MatchSim sim)
        {
            // Karding: disarm visible enemy traps from afar with Piko or shatter walls/traps with Pogs Slam
            if (state.CharacterId == "karding")
            {
                if (sim.CanCastSkill(state, 1))
                {
                    SkillDef piko = sim.GetPlayerSkill(state, 1);
                    double range = piko != null ? piko.MaxRange : 16.0;
                    Vec2 trapPos = FindVisibleEnemyTrapPos(state, sim, range);
                    if (trapPos.IsFinite()) return BeginSkillCast(state, sim, 1, trapPos, false);
                }
                if (sim.CanCastSkill(state, 2))
                {
                    SkillDef pogs = sim.GetPlayerSkill(state, 2);
                    double range = pogs != null ? pogs.MaxRange : 12.0;
                    Vec2 wallOrTrap = FindEnemyWallOrTrapPos(state, sim, range);
                    if (wallOrTrap.IsFinite()) return BeginUltCast(state, sim, wallOrTrap);
                }
                return null;
            }
            // Kikay: place hidden Teks Tripwire at choke (< max traps) or fly Saranggola over unseen enemy area
            if (state.CharacterId == "kikay")
            {
                if (sim.CanCastSkill(state, 2))
                {
                    int unseen = CountUnseenEnemies(state, sim, out Vec2 scoutPoint);
                    if (unseen >= 1 && scoutPoint.IsFinite()) return BeginUltCast(state, sim, scoutPoint);
                }
                if (sim.CanCastSkill(state, 0) && state.Water >= 55.0 && CountOwnTraps(state, sim) < sim.Rules.TrapMaxPerPlayer)
                {
                    Vec2 choke = ChokeSpotNear(state, sim, 9.0);
                    if (choke.IsFinite() && !HasFriendlyTrapNear(state.Team, choke, 6.0, sim))
                        return BeginSkillCast(state, sim, 0, choke, false);
                }
                return null;
            }
            // Manoy: deploy Trumpo Bantay sentry at choke (max 1 active) or count Taya! when 2+ enemies unseen
            if (state.CharacterId == "manoy")
            {
                if (sim.CanCastSkill(state, 2))
                {
                    int unseen = CountUnseenEnemies(state, sim, out _);
                    if (unseen >= 2) return BeginUltCast(state, sim, state.Position);
                }
                if (sim.CanCastSkill(state, 1) && state.Water >= 45.0 && CountOwnSentries(state, sim) == 0)
                {
                    Vec2 choke = ChokeSpotNear(state, sim, 9.0);
                    if (choke.IsFinite()) return BeginSkillCast(state, sim, 1, choke, false);
                }
            }
            return null;
        }

        PlayerInput FightSpecKit(PlayerState state, MatchSim sim, PlayerState enemy, CharacterRole role)
        {
            if (sim.CastingBlocked(state))
            {
                if (HoldsZone(state, sim)) return NoEnemy(state, sim);
                _stayOutLeft = ReenterDelay;
                return Steer(state, sim, OutOfZone(state, sim));
            }
            double distance = state.Position.DistanceTo(enemy.Position);
            bool blocked = state.StiltsLeft <= 0.0 && InCoverSpec(state.Position, enemy.Position, sim);
            // Empowered Tirador shot priority (Narda Luksong Tinik +50%, Kikay Tago! +40%): cash in the buff first!
            bool prioritizeEmpoweredShot = (state.EmpoweredTiradorLeft > 0.0 || (state.InvisibleLeft > 0.0 && distance <= 16.0))
                && !blocked && state.TiradorAmmo > 0 && state.TiradorReloadLeft <= 0.0 && state.PrimaryCd <= 0.0
                && distance <= EffectiveTiradorRange(state, sim, true) * 0.96;
            // Jumong Syato recast bypasses normal bot cast gap so the 2 s follow-up always connects
            bool syatoRecastNow = state.CharacterId == "jumong" && state.SyatoRecastLeft > 0.0 && state.SyatoTargetId >= 0;
            if (!prioritizeEmpoweredShot && state.CanAct() && (_cooldown <= 0.0 || syatoRecastNow))
            {
                if (TryPickSpecSkill(state, sim, enemy, role, distance, blocked, out int slot, out Vec2 aimPoint, out bool tapRelease))
                {
                    if (slot == 2) return BeginUltCast(state, sim, aimPoint);
                    return BeginSkillCast(state, sim, slot, aimPoint, tapRelease);
                }
            }
            PlayerInput output = BuildCombatMove(state, sim, enemy, role, blocked);
            double maxRange = EffectiveTiradorRange(state, sim, true);
            double minRange = EffectiveTiradorRange(state, sim, false);
            // Tactical reload in combat when nearly empty and temporarily unable to get a clean lethal shot
            if (state.TiradorReloadLeft <= 0.0 && state.CanAct()
                && (state.TiradorAmmo <= 0 || (state.TiradorAmmo <= 2 && (blocked || distance > maxRange * 0.92) && enemy.Hp > 30)))
            {
                output.Buttons |= PlayerInput.BtnReload;
            }
            else if (!blocked && state.TiradorAmmo > 0 && state.TiradorReloadLeft <= 0.0 && state.PrimaryCd <= 0.0
                && state.CanAct() && distance <= maxRange * 0.97)
            {
                // Start Tirador shot (1-tick tap <= 18m; hold-to-charge scales smoothly 18m -> 30m)
                int ticksNeeded = 1;
                if (distance > minRange * 0.95 && maxRange > minRange + 0.1)
                {
                    double frac = GMath.Clampf((distance - minRange) / (maxRange - minRange) + 0.10, 0.0, 1.0);
                    ticksNeeded = GMath.Maxi(1, GMath.Ceili(frac * sim.Rules.TiradorChargeSeconds / sim.Rules.TickDt()));
                }
                _chargingPrimary = true;
                _primaryHoldTicks = 1;
                _primaryChargeTicksGoal = ticksNeeded;
                _primaryTargetId = enemy.Id;
                _primaryStaticTarget = Vec2.Inf;
                Vec2 leadPt = LeadAimPoint(state, enemy, sim.Rules.TiradorProjectileSpeed, 0.0, maxRange, sim);
                output.Buttons |= PlayerInput.BtnPrimary;
                if (distance > minRange * 0.85 && EffectiveSkill(sim) >= 0.4) output.Buttons |= PlayerInput.BtnAds;
                output.WithAimPoint(leadPt);
            }
            else if (blocked && state.TiradorAmmo > 0 && state.TiradorReloadLeft <= 0.0 && state.PrimaryCd <= 0.0 && state.CanAct())
            {
                // If an enemy destructible wall blocks our shot, shoot it down!
                Vec2 wallCenter = FindEnemyWallBetween(state, enemy.Position, sim, minRange);
                if (wallCenter.IsFinite())
                {
                    _chargingPrimary = true;
                    _primaryHoldTicks = 1;
                    _primaryChargeTicksGoal = 1;
                    _primaryTargetId = -1;
                    _primaryStaticTarget = wallCenter;
                    output.Buttons |= PlayerInput.BtnPrimary;
                    output.WithAimPoint(wallCenter);
                }
            }
            AddSpecLocomotion(state, sim, distance, SpecStandoff(state, sim, role), output);
            return output;
        }

        PlayerInput BeginSkillCast(PlayerState state, MatchSim sim, int slot, Vec2 aimPoint, bool tapRelease)
        {
            _pressSlot = slot;
            _cooldown = sim.Rules.BotCastGap;
            _tapRelease = tapRelease;
            _aimPoint = aimPoint;
            int bit = WeaponCasting.CardBits[slot];
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, bit, sim.Tick);
        }

        PlayerInput BeginUltCast(PlayerState state, MatchSim sim, Vec2 aimPoint)
        {
            _cooldown = sim.Rules.BotCastGap;
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnUltimate, sim.Tick).WithAimPoint(aimPoint);
        }

        /// <summary>
        /// Role- and kid-specific tactical decision across all 36 skills (docs/KITS.md).
        /// Never spams skills on cooldown into empty air; checks water, range, cover, clusters, ally HP, traps, and grounded state.
        /// </summary>
        bool TryPickSpecSkill(PlayerState state, MatchSim sim, PlayerState enemy, CharacterRole role, double distance, bool blocked,
            out int chosenSlot, out Vec2 aimPoint, out bool tapRelease)
        {
            chosenSlot = -1;
            aimPoint = enemy.Position;
            tapRelease = false;
            switch (role)
            {
                case CharacterRole.Striker:
                    return PickStrikerSkill(state, sim, enemy, distance, blocked, out chosenSlot, out aimPoint, out tapRelease);
                case CharacterRole.Defender:
                    return PickDefenderSkill(state, sim, enemy, distance, blocked, out chosenSlot, out aimPoint, out tapRelease);
                case CharacterRole.Seeker:
                    return PickSeekerSkill(state, sim, enemy, distance, blocked, out chosenSlot, out aimPoint, out tapRelease);
                case CharacterRole.Controller:
                    return PickControllerSkill(state, sim, enemy, distance, blocked, out chosenSlot, out aimPoint, out tapRelease);
                default:
                    return false;
            }
        }

        bool PickStrikerSkill(PlayerState state, MatchSim sim, PlayerState enemy, double distance, bool blocked,
            out int slot, out Vec2 aimPoint, out bool tapRelease)
        {
            slot = -1;
            aimPoint = enemy.Position;
            tapRelease = false;
            SkillDef s1 = sim.GetPlayerSkill(state, 0);
            SkillDef s2 = sim.GetPlayerSkill(state, 1);
            SkillDef ult = sim.GetPlayerSkill(state, 2);
            if (s1 == null || s2 == null || ult == null) return false;

            // ---- NARDA: Luksong Tinik (2-charge dash+buff), Tira Holens (ricochet marble), Tsinelas Bagsik (3 boomerangs) ----
            if (s1.Mechanic == SkillMechanic.LuksongTinik)
            {
                if (sim.CanCastSkill(state, 2) && !blocked && distance <= ult.MaxRange * 0.92
                    && (enemy.Hp <= 70 || CountEnemiesNearPoint(state, sim, enemy.Position, 5.5) >= 2))
                {
                    slot = 2;
                    aimPoint = LeadAimPoint(state, enemy, ult.Speed, ult.Delay, ult.MaxRange, sim);
                    return true;
                }
                if (sim.CanCastSkill(state, 1) && distance <= s2.MaxRange * 0.92)
                {
                    if (!blocked)
                    {
                        slot = 1;
                        aimPoint = LeadAimPoint(state, enemy, s2.Speed, s2.Delay, s2.MaxRange, sim);
                        return true;
                    }
                    Vec2 bank = FindBankShotPoint(state, enemy, sim, s2.MaxRange);
                    if (bank.IsFinite())
                    {
                        slot = 1;
                        aimPoint = bank;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && state.EmpoweredTiradorLeft <= 0.0
                    && (state.S1Charges >= 2 || enemy.Hp <= 65 || state.Hp <= 50 || blocked))
                {
                    Vec2 toward = (enemy.Position - state.Position).Normalized();
                    Vec2 dashDir = state.Hp <= 45 ? toward.Rotated(GMath.Pi * 0.5 * _turn)
                        : blocked ? StepAside(state, enemy, sim) : toward.Rotated(0.35 * _turn);
                    Vec2 dest = state.Position + dashDir.Normalized() * s1.MaxRange;
                    if (!sim.CastingBlockedAt(state, dest))
                    {
                        slot = 0;
                        aimPoint = dest;
                        return true;
                    }
                }
                return false;
            }

            // ---- JUMONG: Syato (stick pop-up + 2s recast dash-whack), Trumpo Ikot (2s spin), Pukpok Palayok (10m leap smash + blur) ----
            if (s1.Mechanic == SkillMechanic.Syato)
            {
                // Immediate recast dash-whack when Syato stick landed!
                if (state.SyatoRecastLeft > 0.0 && state.SyatoTargetId >= 0 && sim.CanCastSkill(state, 0)
                    && sim.Players.TryGetValue(state.SyatoTargetId, out PlayerState marked) && !marked.Tagged
                    && !sim.CastingBlockedAt(state, marked.Position) && (state.Hp >= 30 || marked.Hp <= 35))
                {
                    slot = 0;
                    aimPoint = marked.Position;
                    tapRelease = true;
                    return true;
                }
                if (sim.CanCastSkill(state, 2) && distance <= ult.MaxRange)
                {
                    FindEnemyCluster(state, sim, ult.MaxRange, ult.Radius, false, out int clCount, out Vec2 clCenter);
                    if (clCount >= 2 || enemy.Hp <= 65 || ThreatRank(state, enemy, sim) <= 1)
                    {
                        Vec2 leapTo = clCount >= 2 ? clCenter : enemy.Position;
                        if (!sim.CastingBlockedAt(state, leapTo))
                        {
                            slot = 2;
                            aimPoint = leapTo;
                            return true;
                        }
                    }
                }
                if (sim.CanCastSkill(state, 1) && (distance <= s2.Radius + 1.2 || (state.Effects.Has(StatusEffects.Type.Slow) && distance <= 8.0)))
                {
                    slot = 1;
                    aimPoint = state.Position;
                    tapRelease = true;
                    return true;
                }
                if (sim.CanCastSkill(state, 0) && !blocked && distance <= s1.MaxRange * 0.92 && state.SyatoRecastLeft <= 0.0)
                {
                    slot = 0;
                    aimPoint = LeadAimPoint(state, enemy, s1.Speed, s1.Delay, s1.MaxRange, sim);
                    return true;
                }
                return false;
            }

            // ---- INGGO: Sipa (3-enemy bounce), Tiyakad (4s stilts +40% range), Bato! (1s wind-up missing-HP execute + heal 30) ----
            if (s1.Mechanic == SkillMechanic.Sipa)
            {
                if (sim.CanCastSkill(state, 2) && !blocked && distance <= ult.MaxRange * 0.95)
                {
                    // Hold Bato! for an execute target (<= 60% HP, or <= 70% if Inggo needs the 30 HP heal)
                    PlayerState execTarget = FindExecuteTarget(state, sim, ult.MaxRange * 0.95, state.Hp <= 65 ? 70 : 60);
                    if (execTarget != null)
                    {
                        slot = 2;
                        aimPoint = LeadAimPoint(state, execTarget, ult.Speed, ult.Delay, ult.MaxRange, sim);
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && !blocked && distance <= s1.MaxRange * 0.92)
                {
                    slot = 0;
                    aimPoint = LeadAimPoint(state, enemy, s1.Speed, s1.Delay, s1.MaxRange, sim);
                    return true;
                }
                if (sim.CanCastSkill(state, 1) && state.StiltsLeft <= 0.0 && state.TiradorAmmo >= 3
                    && ((distance >= 15.0 && distance <= 36.0) || (blocked && distance >= 9.0 && distance <= 32.0)))
                {
                    slot = 1;
                    aimPoint = state.Position;
                    tapRelease = true;
                    return true;
                }
            }
            return false;
        }

        bool PickDefenderSkill(PlayerState state, MatchSim sim, PlayerState enemy, double distance, bool blocked,
            out int slot, out Vec2 aimPoint, out bool tapRelease)
        {
            slot = -1;
            aimPoint = enemy.Position;
            tapRelease = false;
            SkillDef s1 = sim.GetPlayerSkill(state, 0);
            SkillDef s2 = sim.GetPlayerSkill(state, 1);
            SkillDef ult = sim.GetPlayerSkill(state, 2);
            if (s1 == null || s2 == null || ult == null) return false;
            int ownSide = sim.SideForTeam(state.Team);
            Vec2 ownBase = sim.Layout.BaseCenter(ownSide);
            bool enemyNearHomeBase = sim.Layout.InBase(ownSide, enemy.Position)
                || enemy.Position.DistanceTo(ownBase) <= sim.Layout.BaseRadius + 6.0
                || (sim.CaptureTeam == 1 - state.Team && sim.CurrentCaptureKind == MatchSim.CaptureKind.Base);

            // ---- JOPAY: Bahay-Bahayan (80HP box wall), Pabitin (4s heal zone), Palosebo (base anti-capture pole) ----
            if (s1.Mechanic == SkillMechanic.BahayBahayan)
            {
                if (sim.CanCastSkill(state, 2) && enemyNearHomeBase && state.Position.DistanceTo(ownBase) <= ult.MaxRange + sim.Layout.BaseRadius)
                {
                    slot = 2;
                    aimPoint = ClampAimRange(state.Position, ownBase, ult.MaxRange);
                    return true;
                }
                if (sim.CanCastSkill(state, 1))
                {
                    PlayerState hurt = FindHurtAlly(state, sim, s2.MaxRange, AllyProtectBelow, true);
                    if (hurt != null)
                    {
                        slot = 1;
                        aimPoint = hurt.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0))
                {
                    PlayerState protectee = FindRescuingAlly(state, sim, s1.MaxRange)
                        ?? FindHurtAlly(state, sim, s1.MaxRange, AllyProtectBelow, true);
                    if (protectee != null)
                    {
                        Vec2 dirToEnemy = (enemy.Position - protectee.Position).Normalized();
                        Vec2 wallPos = ClampAimRange(state.Position, protectee.Position + dirToEnemy * 2.4, s1.MaxRange);
                        slot = 0;
                        aimPoint = wallPos;
                        return true;
                    }
                    if (enemyNearHomeBase && distance <= s1.MaxRange)
                    {
                        Vec2 dirToEnemy = (enemy.Position - ownBase).Normalized();
                        Vec2 wallPos = ClampAimRange(state.Position, ownBase + dirToEnemy * (sim.Layout.BaseRadius * 0.7), s1.MaxRange);
                        slot = 0;
                        aimPoint = wallPos;
                        return true;
                    }
                }
                return false;
            }

            // ---- ORIANG: Chinese Garter (12m ally tether 30% DR), Sungka (3s dmg absorb -> shared shield), Langit! (8m 1HP floor) ----
            if (s1.Mechanic == SkillMechanic.ChineseGarter)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    PlayerState critical = FindHurtAlly(state, sim, ult.Radius, AllyCriticalBelow, true);
                    PlayerState rescuingHurt = FindRescuingAlly(state, sim, ult.Radius);
                    if (critical != null || (rescuingHurt != null && rescuingHurt.Hp < sim.Rules.PlayerMaxHp * AllyProtectBelow))
                    {
                        slot = 2;
                        aimPoint = state.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && !HasActiveTether(state, sim) && state.Hp >= 35)
                {
                    PlayerState ally = FindHurtAlly(state, sim, s1.MaxRange, AllyProtectBelow, false)
                        ?? FindRescuingAlly(state, sim, s1.MaxRange)
                        ?? FindThreatenedAlly(state, sim, s1.MaxRange, 14.0);
                    if (ally != null)
                    {
                        slot = 0;
                        aimPoint = ally.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 1) && distance <= 15.0
                    && (state.Hp < sim.Rules.PlayerMaxHp * 0.85 || CountAlliesNear(state, sim, s2.Radius) >= 1))
                {
                    slot = 1;
                    aimPoint = state.Position;
                    tapRelease = true;
                    return true;
                }
                return false;
            }

            // ---- PAENG: Sack Race (3s 40% DR + knockback), Luksong Baka (ally 10m vault), Tinik Wall (4s wall + knock-up) ----
            if (s1.Mechanic == SkillMechanic.SackRace)
            {
                if (sim.CanCastSkill(state, 2) && distance <= ult.MaxRange)
                {
                    FindEnemyCluster(state, sim, ult.MaxRange, ult.Width * 0.5, false, out int clCount, out Vec2 clCenter);
                    bool urgent = clCount >= 2 || enemyNearHomeBase || FindHurtAlly(state, sim, 12.0, AllyProtectBelow, false) != null;
                    if (urgent)
                    {
                        slot = 2;
                        aimPoint = clCount >= 2 ? clCenter : enemy.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && (distance <= 9.0 || state.Hp < sim.Rules.PlayerMaxHp * 0.70 || enemyNearHomeBase))
                {
                    slot = 0;
                    aimPoint = enemy.Position;
                    tapRelease = true;
                    return true;
                }
                if (sim.CanCastSkill(state, 1))
                {
                    PlayerState hurtAlly = FindHurtAlly(state, sim, 6.0, AllyProtectBelow, false);
                    PlayerState rescuer = FindRescuingAlly(state, sim, 6.0);
                    if (hurtAlly != null || rescuer != null)
                    {
                        slot = 1;
                        aimPoint = state.Position;
                        tapRelease = true;
                        return true;
                    }
                }
            }
            return false;
        }

        bool PickSeekerSkill(PlayerState state, MatchSim sim, PlayerState enemy, double distance, bool blocked,
            out int slot, out Vec2 aimPoint, out bool tapRelease)
        {
            slot = -1;
            aimPoint = enemy.Position;
            tapRelease = false;
            SkillDef s1 = sim.GetPlayerSkill(state, 0);
            SkillDef s2 = sim.GetPlayerSkill(state, 1);
            SkillDef ult = sim.GetPlayerSkill(state, 2);
            if (s1 == null || s2 == null || ult == null) return false;

            // ---- KIKAY: Teks Tripwire (hidden reveal+slow trap), Tago! (4s invis +40% shot), Saranggola (15m map kite reveal) ----
            if (s1.Mechanic == SkillMechanic.TeksTripwire)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    int unseen = CountUnseenEnemies(state, sim, out Vec2 scoutPoint);
                    if (unseen >= 1)
                    {
                        slot = 2;
                        aimPoint = scoutPoint.IsFinite() ? scoutPoint : enemy.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 1) && state.InvisibleLeft <= 0.0
                    && ((distance >= 7.0 && distance <= 20.0 && state.TiradorAmmo >= 2) || state.Hp <= 45))
                {
                    slot = 1;
                    aimPoint = state.Position;
                    tapRelease = true;
                    return true;
                }
                if (sim.CanCastSkill(state, 0) && CountOwnTraps(state, sim) < sim.Rules.TrapMaxPerPlayer && distance <= s1.MaxRange + 4.0)
                {
                    Vec2 place = ClampAimRange(state.Position, (state.Position + enemy.Position) * 0.5, s1.MaxRange);
                    if (!HasFriendlyTrapNear(state.Team, place, 5.5, sim))
                    {
                        slot = 0;
                        aimPoint = place;
                        return true;
                    }
                }
                return false;
            }

            // ---- KARDING: Yoyo (outline 5s + 2m tug), Piko (disarm traps + silent outline grid), Pogs Slam (shatter walls/traps + 14m reveal) ----
            if (s1.Mechanic == SkillMechanic.Yoyo)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    Vec2 enemyWallOrTrap = FindEnemyWallOrTrapPos(state, sim, ult.MaxRange);
                    int unseen = CountUnseenEnemies(state, sim, out _);
                    int nearby = CountEnemiesNearPoint(state, sim, enemy.Position, ult.Radius);
                    if (enemyWallOrTrap.IsFinite())
                    {
                        slot = 2;
                        aimPoint = enemyWallOrTrap;
                        return true;
                    }
                    if (distance <= ult.MaxRange && (nearby >= 2 || unseen >= 1))
                    {
                        slot = 2;
                        aimPoint = enemy.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 1))
                {
                    Vec2 trapPos = FindVisibleEnemyTrapPos(state, sim, s2.MaxRange);
                    if (trapPos.IsFinite())
                    {
                        slot = 1;
                        aimPoint = trapPos;
                        return true;
                    }
                    if (distance <= s2.MaxRange && (blocked || enemy.RevealedOutlineLeft <= 0.0))
                    {
                        slot = 1;
                        aimPoint = enemy.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && !blocked && distance <= s1.MaxRange * 0.92)
                {
                    slot = 0;
                    aimPoint = LeadAimPoint(state, enemy, s1.Speed, s1.Delay, s1.MaxRange, sim);
                    return true;
                }
                return false;
            }

            // ---- MANOY: Sili-Tubig (cone outline 4s + blur 1.5s), Trumpo Bantay (20s 8m sentry), Taya! (3s count map-wide snapshot) ----
            if (s1.Mechanic == SkillMechanic.SiliTubig)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    int unseen = CountUnseenEnemies(state, sim, out _);
                    if (unseen >= 2)
                    {
                        slot = 2;
                        aimPoint = state.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && distance <= s1.MaxRange * 0.94)
                {
                    slot = 0;
                    aimPoint = enemy.Position;
                    return true;
                }
                if (sim.CanCastSkill(state, 1) && CountOwnSentries(state, sim) == 0 && distance <= s2.MaxRange + 6.0)
                {
                    Vec2 place = ClampAimRange(state.Position, (state.Position + enemy.Position) * 0.5, s2.MaxRange);
                    slot = 1;
                    aimPoint = place;
                    return true;
                }
            }
            return false;
        }

        bool PickControllerSkill(PlayerState state, MatchSim sim, PlayerState enemy, double distance, bool blocked,
            out int slot, out Vec2 aimPoint, out bool tapRelease)
        {
            slot = -1;
            aimPoint = enemy.Position;
            tapRelease = false;
            SkillDef s1 = sim.GetPlayerSkill(state, 0);
            SkillDef s2 = sim.GetPlayerSkill(state, 1);
            SkillDef ult = sim.GetPlayerSkill(state, 2);
            if (s1 == null || s2 == null || ult == null) return false;

            // ---- TERE: Jackstone (5s slow+dot circle), Luksong Lubid (trip rope 1s knockdown), Garter Net (6m root 1.25s -> slow 2s) ----
            if (s1.Mechanic == SkillMechanic.Jackstone)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    FindEnemyCluster(state, sim, ult.MaxRange, ult.Radius, false, out int clCount, out Vec2 clCenter);
                    if (clCount >= 2 || (distance <= ult.MaxRange * 0.9 && (ThreatRank(state, enemy, sim) <= 1 || enemy.Hp <= 65)))
                    {
                        slot = 2;
                        aimPoint = clCount >= 2 ? clCenter : LeadAimPoint(state, enemy, 0.0, ult.Delay, ult.MaxRange, sim);
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 1) && distance <= s2.MaxRange * 0.94)
                {
                    Vec2 vel = _velocity.TryGetValue(enemy.Id, out Vec2 v) ? v : Vec2.Zero;
                    Vec2 ahead = ClampAimRange(state.Position, enemy.Position + vel * 0.45, s2.MaxRange);
                    slot = 1;
                    aimPoint = ahead;
                    return true;
                }
                if (sim.CanCastSkill(state, 0) && distance <= s1.MaxRange * 0.95)
                {
                    FindEnemyCluster(state, sim, s1.MaxRange, s1.Radius, false, out int clCount, out Vec2 clCenter);
                    slot = 0;
                    aimPoint = clCount >= 2 ? clCenter : enemy.Position;
                    return true;
                }
                return false;
            }

            // ---- PAMELA: Holens Spill (3s slip cone), Lata! (1.5s can polymorph), Patintero! (3 chalk lines 0.8s stun each) ----
            if (s1.Mechanic == SkillMechanic.HolensSpill)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    FindEnemyCluster(state, sim, ult.MaxRange, ult.Width * 0.5, false, out int clCount, out Vec2 clCenter);
                    if (clCount >= 2 || (distance <= ult.MaxRange * 0.9 && ThreatRank(state, enemy, sim) <= 1))
                    {
                        slot = 2;
                        aimPoint = clCount >= 2 ? clCenter : enemy.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 1) && !blocked && distance <= s2.MaxRange * 0.92 && enemy.Effects.CanCast())
                {
                    slot = 1;
                    aimPoint = LeadAimPoint(state, enemy, s2.Speed, s2.Delay, s2.MaxRange, sim);
                    return true;
                }
                if (sim.CanCastSkill(state, 0) && distance <= s1.MaxRange * 0.94)
                {
                    slot = 0;
                    aimPoint = enemy.Position;
                    return true;
                }
                return false;
            }

            // ---- DOMENG: Pabitin Snare (0.5s delay root 1.2s), Sako Trap (hidden 50% hop-slow), Lupa! (8m grounded-only knockup+stun) ----
            if (s1.Mechanic == SkillMechanic.PabitinSnare)
            {
                if (sim.CanCastSkill(state, 2))
                {
                    // Hold Lupa! strictly for GROUNDED enemies (AirY <= 0.05) — raised kids are safe!
                    int groundedInRange = CountGroundedEnemiesWithin(state, sim, ult.Radius - 0.5);
                    bool urgentGrounded = enemy.AirY <= 0.05 && distance <= ult.Radius - 1.2
                        && (ThreatRank(state, enemy, sim) <= 1 || enemy.Hp <= 65);
                    if (groundedInRange >= 2 || urgentGrounded)
                    {
                        slot = 2;
                        aimPoint = state.Position;
                        return true;
                    }
                }
                if (sim.CanCastSkill(state, 0) && distance <= s1.MaxRange * 0.94)
                {
                    FindEnemyCluster(state, sim, s1.MaxRange, s1.Radius, false, out int clCount, out Vec2 clCenter);
                    slot = 0;
                    aimPoint = clCount >= 2 ? clCenter : LeadAimPoint(state, enemy, 0.0, s1.Delay, s1.MaxRange, sim);
                    return true;
                }
                if (sim.CanCastSkill(state, 1) && CountOwnTraps(state, sim) < sim.Rules.TrapMaxPerPlayer && distance <= s2.MaxRange + 3.0)
                {
                    Vec2 place = ClampAimRange(state.Position, (state.Position + enemy.Position) * 0.5, s2.MaxRange);
                    if (!HasFriendlyTrapNear(state.Team, place, 5.5, sim))
                    {
                        slot = 1;
                        aimPoint = place;
                        return true;
                    }
                }
            }
            return false;
        }

        PlayerInput BuildCombatMove(PlayerState state, MatchSim sim, PlayerState enemy, CharacterRole role, bool blocked)
        {
            Vec2 offset = enemy.Position - state.Position;
            double distance = offset.Length();
            Vec2 toward = distance > 0.001 ? offset.Normalized() : new Vec2(0.0, 1.0);
            double standoff = SpecStandoff(state, sim, role);
            Vec2 move = Vec2.Zero;
            // Active close-range brawling states (Jumong spinning, Paeng Sack Race): press into the enemy!
            if (state.TrumpoSpinLeft > 0.0 || state.SackRaceLeft > 0.0)
            {
                move = toward;
            }
            else if (blocked)
            {
                move = StepAside(state, enemy, sim);
            }
            else if (role == CharacterRole.Defender && state.CharacterId == "oriang")
            {
                // Stay close enough to a hurt teammate to cover them with Chinese Garter / Sungka / Langit!
                PlayerState hurtAlly = FindHurtAlly(state, sim, 16.0, AllyProtectBelow, false);
                if (hurtAlly != null && state.Position.DistanceTo(hurtAlly.Position) > 7.5)
                    move = (hurtAlly.Position - state.Position).Normalized();
            }
            if (move == Vec2.Zero)
            {
                if (distance > standoff + 0.6) move = toward;
                else if (distance < standoff - TooCloseSlack) move = -toward;
                else
                {
                    double side = ((int)((double)sim.Tick * sim.Rules.TickDt() / StrafeSwitch) + state.Id) % 2 == 0 ? 1.0 : -1.0;
                    move = toward.Rotated(side * GMath.Pi / 2.0);
                }
            }
            return Steer(state, sim, Unpin(AvoidEntering(state, sim, move)));
        }

        void AddSpecLocomotion(PlayerState state, MatchSim sim, double distance, double standoff, PlayerInput output)
        {
            bool slipping = state.Effects.Has(StatusEffects.Type.Slip);
            // Jump over imminent Domeng Lupa! ground quake (raised kids are safe!) or enemy ground hazards
            if (!slipping && state.JumpCd <= 0.0 && state.AirY <= 0.01 && (LupaQuakeImminent(state, sim) || GroundHazardNear(state, sim)))
            {
                output.Buttons |= PlayerInput.BtnJump;
                return;
            }
            if (state.DashCd <= 0.0 && (state.Effects.Has(StatusEffects.Type.Slow) || distance > standoff + 8.0))
                output.Buttons |= PlayerInput.BtnDash;
            else if (!slipping && state.SlideCd <= 0.0 && state.AirY <= 0.01 && state.SlideLeft <= 0.0
                && distance > standoff + 4.5 && EffectiveSkill(sim) >= 0.35)
                output.Buttons |= PlayerInput.BtnSlide;
        }

        /// <summary>Role-aware enemy selection respecting team vision (invisible Kikay is never targeted until visible/revealed).</summary>
        public PlayerState PickSpecEnemy(PlayerState state, MatchSim sim, double reach, CharacterRole role)
        {
            PlayerState best = null;
            int bestRank = 6;
            double bestDistance = GMath.Inf;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
            {
                if (!sim.IsEnemyVisibleToTeam(enemy, state.Team)) continue;
                double distance = state.Position.DistanceTo(enemy.Position);
                if (distance > reach) continue;
                int rank = SpecThreatRank(state, enemy, sim, role);
                if (rank < bestRank || (rank == bestRank && distance < bestDistance))
                {
                    best = enemy;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }
            return best;
        }

        int SpecThreatRank(PlayerState state, PlayerState enemy, MatchSim sim, CharacterRole role)
        {
            if (sim.Layout.InBase(sim.SideForTeam(state.Team), enemy.Position)) return 0;
            if (sim.RescueRescuer[enemy.Team] == enemy.Id) return 1;
            if (role == CharacterRole.Striker)
            {
                if (enemy.Hp <= 45) return 2;
                if ((double)enemy.Hp < (double)sim.Rules.PlayerMaxHp * 0.65) return 3;
                return 4;
            }
            if (role == CharacterRole.Defender)
            {
                PlayerState hurtAlly = FindHurtAlly(state, sim, 20.0, AllyProtectBelow, false);
                if (hurtAlly != null && enemy.Position.DistanceTo(hurtAlly.Position) <= 12.0) return 2;
                if ((double)enemy.Hp < (double)sim.Rules.PlayerMaxHp * HealBelow) return 3;
                return 4;
            }
            if (role == CharacterRole.Seeker)
            {
                if (enemy.RevealedOutlineLeft <= 0.0) return 2;
                if ((double)enemy.Hp < (double)sim.Rules.PlayerMaxHp * HealBelow) return 3;
                return 4;
            }
            // Controller: prioritize enemies standing in a cluster for maximum AoE CC value
            if (CountEnemiesNearPoint(state, sim, enemy.Position, 6.0) >= 2) return 2;
            if ((double)enemy.Hp < (double)sim.Rules.PlayerMaxHp * HealBelow) return 3;
            return 4;
        }

        protected static CharacterRole RoleOf(PlayerState state, MatchSim sim)
        {
            CharacterDef def = sim.GetCharacterDef(state.CharacterId);
            return def != null ? def.Role : CharacterRole.Striker;
        }

        static double EffectiveTiradorRange(PlayerState state, MatchSim sim, bool max)
        {
            double baseRange = max ? sim.Rules.TiradorMaxRange : sim.Rules.TiradorMinRange;
            if (baseRange <= 0.0) baseRange = max ? 30.0 : 18.0;
            if (state.StiltsLeft > 0.0)
            {
                SkillDef s2 = sim.GetPlayerSkill(state, 1);
                double mult = 1.0 + (s2 != null && s2.BonusRatio > 0.0 ? s2.BonusRatio : 0.40);
                baseRange *= mult;
            }
            return baseRange;
        }

        static double SpecStandoff(PlayerState state, MatchSim sim, CharacterRole role)
        {
            if (state.CharacterId == "inggo") return state.StiltsLeft > 0.0 ? 20.0 : 14.0;
            if (state.CharacterId == "jumong") return state.TrumpoSpinLeft > 0.0 ? 2.2 : 6.5;
            if (state.CharacterId == "paeng") return state.SackRaceLeft > 0.0 ? 2.2 : 7.0;
            if (state.CharacterId == "manoy") return 8.5;
            if (state.CharacterId == "domeng") return sim.CanCastSkill(state, 2) ? 6.0 : 8.5;
            switch (role)
            {
                case CharacterRole.Striker: return 10.5;
                case CharacterRole.Defender: return 11.5;
                case CharacterRole.Seeker: return 13.0;
                case CharacterRole.Controller: return 12.5;
                default: return 11.0;
            }
        }

        Vec2 LeadAimPoint(PlayerState state, PlayerState enemy, double speed, double delay, double maxRange, MatchSim sim)
        {
            Vec2 enemyVel = _velocity.TryGetValue(enemy.Id, out Vec2 v) ? v : Vec2.Zero;
            double dist = state.Position.DistanceTo(enemy.Position);
            double flight = speed > 0.0 ? dist / speed : 0.0;
            Vec2 predicted = enemy.Position + enemyVel * (delay + flight) * EffectiveSkill(sim);
            Vec2 offset = predicted - state.Position;
            if (offset.LengthSquared() < 0.0001) offset = enemy.Position - state.Position;
            double limit = GMath.DegToRad(sim.Rules.BotAimErrorDegrees) * GMath.Lerpf(1.0, SkilledErrorShare, EffectiveSkill(sim));
            Vec2 dir = offset.Normalized().Rotated(sim.Rng.RandfRange((float)-limit, (float)limit));
            double clamped = GMath.Minf(offset.Length(), GMath.Maxf(maxRange, 1.0));
            return state.Position + dir * clamped;
        }

        static Vec2 ClampAimRange(Vec2 origin, Vec2 target, double maxRange)
        {
            Vec2 delta = target - origin;
            double len = delta.Length();
            if (len <= maxRange || maxRange <= 0.0 || len <= 0.0001) return target;
            return origin + (delta / len) * maxRange;
        }

        static Vec2 FindBankShotPoint(PlayerState state, PlayerState enemy, MatchSim sim, double maxRange)
        {
            Vec2 mid = (state.Position + enemy.Position) * 0.5;
            Vec2 side = (enemy.Position - state.Position).Normalized().Orthogonal();
            for (int i = 0; i < BothWays.Length; i++)
            {
                Vec2 probe = mid + side * (4.5 * BothWays[i]);
                if (state.Position.DistanceTo(probe) <= maxRange
                    && !InCoverSpec(state.Position, probe, sim) && !InCoverSpec(probe, enemy.Position, sim))
                    return probe;
            }
            return Vec2.Inf;
        }

        PlayerState FindExecuteTarget(PlayerState state, MatchSim sim, double maxRange, int hpCeiling)
        {
            PlayerState best = null;
            int lowestHp = hpCeiling + 1;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
            {
                if (!sim.IsEnemyVisibleToTeam(enemy, state.Team)) continue;
                if (enemy.Hp > hpCeiling) continue;
                if (state.Position.DistanceTo(enemy.Position) > maxRange) continue;
                if (InCoverSpec(state.Position, enemy.Position, sim)) continue;
                if (enemy.Hp < lowestHp)
                {
                    lowestHp = enemy.Hp;
                    best = enemy;
                }
            }
            return best;
        }

        static PlayerState FindHurtAlly(PlayerState state, MatchSim sim, double maxRange, double hpShare, bool includeSelf)
        {
            PlayerState best = null;
            int lowestHp = (int)(sim.Rules.PlayerMaxHp * hpShare);
            foreach (var pair in sim.Players)
            {
                PlayerState ally = pair.Value;
                if (ally.Team != state.Team || ally.Tagged) continue;
                if (!includeSelf && ally.Id == state.Id) continue;
                if (ally.Hp >= lowestHp) continue;
                if (state.Position.DistanceTo(ally.Position) > maxRange) continue;
                lowestHp = ally.Hp;
                best = ally;
            }
            return best;
        }

        static PlayerState FindRescuingAlly(PlayerState state, MatchSim sim, double maxRange)
        {
            int rescuerId = sim.RescueRescuer[state.Team];
            if (rescuerId < 0 || rescuerId == state.Id) return null;
            if (!sim.Players.TryGetValue(rescuerId, out PlayerState ally) || ally.Tagged) return null;
            return state.Position.DistanceTo(ally.Position) <= maxRange ? ally : null;
        }

        static PlayerState FindThreatenedAlly(PlayerState state, MatchSim sim, double maxRange, double enemyProximity)
        {
            PlayerState best = null;
            double bestDist = GMath.Inf;
            foreach (var pair in sim.Players)
            {
                PlayerState ally = pair.Value;
                if (ally.Team != state.Team || ally.Tagged || ally.Id == state.Id) continue;
                double d = state.Position.DistanceTo(ally.Position);
                if (d > maxRange) continue;
                bool underThreat = false;
                foreach (PlayerState foe in sim.EnemiesOf(state.Team))
                {
                    if (!sim.IsEnemyVisibleToTeam(foe, state.Team)) continue;
                    if (ally.Position.DistanceTo(foe.Position) <= enemyProximity)
                    {
                        underThreat = true;
                        break;
                    }
                }
                if (underThreat && d < bestDist)
                {
                    bestDist = d;
                    best = ally;
                }
            }
            return best;
        }

        static int CountAlliesNear(PlayerState state, MatchSim sim, double radius)
        {
            int count = 0;
            foreach (var pair in sim.Players)
            {
                PlayerState ally = pair.Value;
                if (ally.Team == state.Team && !ally.Tagged && ally.Id != state.Id
                    && state.Position.DistanceTo(ally.Position) <= radius)
                    count++;
            }
            return count;
        }

        static int CountEnemiesNearPoint(PlayerState state, MatchSim sim, Vec2 center, double radius)
        {
            int count = 0;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
                if (sim.IsEnemyVisibleToTeam(enemy, state.Team) && enemy.Position.DistanceTo(center) <= radius) count++;
            return count;
        }

        static int CountGroundedEnemiesWithin(PlayerState state, MatchSim sim, double radius)
        {
            int count = 0;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
                if (sim.IsEnemyVisibleToTeam(enemy, state.Team) && enemy.AirY <= 0.05 && state.Position.DistanceTo(enemy.Position) <= radius)
                    count++;
            return count;
        }

        static void FindEnemyCluster(PlayerState state, MatchSim sim, double maxRange, double clusterRadius, bool groundedOnly,
            out int bestCount, out Vec2 bestCenter)
        {
            bestCount = 0;
            bestCenter = Vec2.Zero;
            foreach (PlayerState anchor in sim.EnemiesOf(state.Team))
            {
                if (!sim.IsEnemyVisibleToTeam(anchor, state.Team)) continue;
                if (groundedOnly && anchor.AirY > 0.05) continue;
                if (state.Position.DistanceTo(anchor.Position) > maxRange) continue;
                int count = 0;
                Vec2 sum = Vec2.Zero;
                foreach (PlayerState other in sim.EnemiesOf(state.Team))
                {
                    if (!sim.IsEnemyVisibleToTeam(other, state.Team)) continue;
                    if (groundedOnly && other.AirY > 0.05) continue;
                    if (anchor.Position.DistanceTo(other.Position) <= clusterRadius)
                    {
                        count++;
                        sum += other.Position;
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestCenter = sum / count;
                }
            }
        }

        static int CountUnseenEnemies(PlayerState state, MatchSim sim, out Vec2 scoutPoint)
        {
            int unseen = 0;
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
                if (!sim.IsEnemyVisibleToTeam(enemy, state.Team)) unseen++;
            // Fair tactical guess (never cheats with exact hidden position): scout enemy base approach or mid choke
            int enemySide = -sim.SideForTeam(state.Team);
            Vec2 enemyBase = sim.Layout.BaseCenter(enemySide);
            scoutPoint = enemyBase * 0.65;
            return unseen;
        }

        protected static bool AnyVisibleEnemyWithin(PlayerState state, MatchSim sim, double range)
        {
            foreach (PlayerState enemy in sim.EnemiesOf(state.Team))
                if (sim.IsEnemyVisibleToTeam(enemy, state.Team) && state.Position.DistanceTo(enemy.Position) <= range)
                    return true;
            return false;
        }

        static int CountOwnTraps(PlayerState state, MatchSim sim)
        {
            int count = 0;
            List<WeaponSystem.HiddenTrap> traps = sim.Weapons.Traps;
            for (int i = 0; i < traps.Count; i++)
                if (traps[i].OwnerId == state.Id) count++;
            return count;
        }

        static bool HasFriendlyTrapNear(int team, Vec2 pos, double minDist, MatchSim sim)
        {
            List<WeaponSystem.HiddenTrap> traps = sim.Weapons.Traps;
            for (int i = 0; i < traps.Count; i++)
                if (traps[i].Team == team && traps[i].Position.DistanceTo(pos) < minDist) return true;
            return false;
        }

        static int CountOwnSentries(PlayerState state, MatchSim sim)
        {
            int count = 0;
            List<WeaponSystem.Sentry> sentries = sim.Weapons.Sentries;
            for (int i = 0; i < sentries.Count; i++)
                if (sentries[i].OwnerId == state.Id && sentries[i].TimeLeft > 0.0) count++;
            return count;
        }

        static bool HasActiveTether(PlayerState state, MatchSim sim)
        {
            List<WeaponSystem.AllyTether> tethers = sim.Weapons.Tethers;
            for (int i = 0; i < tethers.Count; i++)
                if (tethers[i].OwnerId == state.Id && tethers[i].TimeLeft > 0.0) return true;
            return false;
        }

        static Vec2 FindVisibleEnemyTrapPos(PlayerState state, MatchSim sim, double maxRange)
        {
            Vec2 best = Vec2.Inf;
            double bestDist = maxRange;
            List<WeaponSystem.HiddenTrap> traps = sim.Weapons.Traps;
            for (int i = 0; i < traps.Count; i++)
            {
                WeaponSystem.HiddenTrap trap = traps[i];
                if (trap.Team == state.Team || !sim.IsTrapVisibleToTeam(trap, state.Team)) continue;
                double d = state.Position.DistanceTo(trap.Position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = trap.Position;
                }
            }
            return best;
        }

        static Vec2 FindEnemyWallOrTrapPos(PlayerState state, MatchSim sim, double maxRange)
        {
            Vec2 trapPos = FindVisibleEnemyTrapPos(state, sim, maxRange);
            if (trapPos.IsFinite()) return trapPos;
            List<WeaponSystem.DestructibleWall> walls = sim.Weapons.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WeaponSystem.DestructibleWall wall = walls[i];
                if (wall.Team == state.Team || wall.Hp <= 0 || wall.TimeLeft <= 0.0) continue;
                if (state.Position.DistanceTo(wall.Center) <= maxRange) return wall.Center;
            }
            return Vec2.Inf;
        }

        static Vec2 FindBreakableEnemyUtility(PlayerState state, MatchSim sim, double maxRange)
        {
            Vec2 trapPos = FindVisibleEnemyTrapPos(state, sim, maxRange);
            if (trapPos.IsFinite()) return trapPos;
            List<WeaponSystem.Sentry> sentries = sim.Weapons.Sentries;
            for (int i = 0; i < sentries.Count; i++)
            {
                WeaponSystem.Sentry sentry = sentries[i];
                if (sentry.Team == state.Team || sentry.TimeLeft <= 0.0) continue;
                if (state.Position.DistanceTo(sentry.Position) <= maxRange) return sentry.Position;
            }
            return Vec2.Inf;
        }

        static Vec2 FindEnemyWallBetween(PlayerState state, Vec2 target, MatchSim sim, double maxRange)
        {
            List<WeaponSystem.DestructibleWall> walls = sim.Weapons.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WeaponSystem.DestructibleWall wall = walls[i];
                if (wall.Team == state.Team || wall.Hp <= 0 || wall.TimeLeft <= 0.0) continue;
                if (state.Position.DistanceTo(wall.Center) > maxRange) continue;
                Vec2 a = wall.Center - wall.Along * wall.HalfWidth;
                Vec2 b = wall.Center + wall.Along * wall.HalfWidth;
                if (SegmentsCross(state.Position, target, a, b)) return wall.Center;
            }
            return Vec2.Inf;
        }

        static Vec2 ChokeSpotNear(PlayerState state, MatchSim sim, double maxCastRange)
        {
            int enemySide = -sim.SideForTeam(state.Team);
            Vec2 towardEnemy = (sim.Layout.BaseCenter(enemySide) - state.Position).Normalized();
            Vec2 lateral = towardEnemy.Orthogonal() * (state.Id % 2 == 0 ? 2.2 : -2.2);
            Vec2 desired = state.Position + towardEnemy * GMath.Minf(5.0, maxCastRange * 0.65) + lateral;
            desired = sim.Layout.KeepInStreet(desired, sim.Rules.PlayerRadius + 0.6);
            if (InsideCover(desired, sim) || sim.CastingBlockedAt(state, desired)) return Vec2.Inf;
            return state.Position.DistanceTo(desired) <= maxCastRange ? desired : Vec2.Inf;
        }

        static bool LupaQuakeImminent(PlayerState state, MatchSim sim)
        {
            List<WeaponSystem.SkillZone> zones = sim.Weapons.SkillZones;
            for (int i = 0; i < zones.Count; i++)
            {
                WeaponSystem.SkillZone z = zones[i];
                if (z.Team == state.Team || z.DelayLeft <= 0.0 || z.DelayLeft > 0.35) continue;
                if (z.SkillId == "lupa_quake" && state.Position.DistanceTo(z.Position) <= z.Radius + 0.6) return true;
            }
            return false;
        }

        static bool InCoverSpec(Vec2 from, Vec2 to, MatchSim sim)
        {
            if (InCover(from, to, sim)) return true;
            List<WeaponSystem.DestructibleWall> walls = sim.Weapons.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WeaponSystem.DestructibleWall wall = walls[i];
                if (wall.Hp <= 0 || wall.TimeLeft <= 0.0) continue;
                Vec2 a = wall.Center - wall.Along * (wall.HalfWidth + CoverMargin);
                Vec2 b = wall.Center + wall.Along * (wall.HalfWidth + CoverMargin);
                if (SegmentsCross(from, to, a, b)) return true;
            }
            return false;
        }

        static bool SegmentsCross(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
        {
            Vec2 r = p2 - p1;
            Vec2 s = q2 - q1;
            double denom = r.x * s.y - r.y * s.x;
            if (GMath.Absf(denom) < 1e-9) return false;
            Vec2 qp = q1 - p1;
            double t = (qp.x * s.y - qp.y * s.x) / denom;
            double u = (qp.x * r.y - qp.y * r.x) / denom;
            return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
        }

        // =========================================================================
        // LEGACY & SHARED BOT LOGIC (bit-for-bit preserved when !UsingSpecKit)
        // =========================================================================

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
            if (RescueDue(state, sim)) return WalkToNav(state, sim, sim.Layout.RescueRect(sim.JailSideFor(state.Team)).GetCenter());
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
            Vec2 area = sim.Layout.RescueRect(sim.JailSideFor(state.Team)).GetCenter();
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
            Vec2 from = sim.Layout.RescueRect(sim.JailSideFor(state.Team)).GetCenter();
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
            if (state.UsingSpecKit) return SpecStandoff(state, sim, RoleOf(state, sim));
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
            else if (state.JumpCd <= 0.0 && state.AirY <= 0.01 && !state.Effects.Has(StatusEffects.Type.Slip) && GroundHazardNear(state, sim))
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
            if (state.UsingSpecKit)
            {
                List<WeaponSystem.SkillZone> sz = sim.Weapons.SkillZones;
                for (int i = 0; i < sz.Count; i++)
                {
                    WeaponSystem.SkillZone z = sz[i];
                    if (z.Team == state.Team || z.TimeLeft <= 0.0) continue;
                    if (z.SkillId == "jackstone" || z.SkillId == "holens_spill" || z.SkillId == "pabitin_snare" || z.SkillId == "garter_net")
                        if (state.Position.DistanceTo(z.Position) <= z.Radius + state.Radius + 0.5) return true;
                }
            }
            return false;
        }
    }
}
