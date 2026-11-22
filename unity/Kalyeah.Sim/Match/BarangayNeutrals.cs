using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>The 4 kid roles for Baron role-specific buffs (docs/SPEC.md §6–7).</summary>
    public enum KidRole
    {
        Striker = 0,
        Defender = 1,
        Seeker = 2,
        Controller = 3,
    }

    /// <summary>Barangay epic neutral objectives (Crocodile = Dragon, Philippine Eagle = Baron).</summary>
    public enum NeutralBossKind
    {
        Dragon = 0,
        Baron = 1,
    }

    /// <summary>
    /// Cute-to-angry-to-flee lifecycle (docs/GRAPHICS.md: zero gore, never dying on screen).
    /// Dormant waits for the spawn/respawn timer; Idle is calm/cute; Angry grows larger and retaliates lightly;
    /// Fleeing shrinks and runs away when reaching 0 HP, granting the killer's team the timed buff.
    /// </summary>
    public enum NeutralBossState
    {
        Dormant = 0,
        Idle = 1,
        Angry = 2,
        Fleeing = 3,
    }

    /// <summary>Fixed untargetable healing vendor (Mang Taho, Fishball Cart; docs/SPEC.md §6).</summary>
    public sealed class VendorState
    {
        public string Id = "";
        public string Name = "";
        public string Lane = "";
        public Vec2 Position;
        public double Radius = 2.2;
        public int HealAmount = 40;
        public double EatSeconds = 2.0;
        public double EatSlowMult = 0.70;
        public double RestockSeconds = 30.0;
        public double RestockLeft;
        public int ServingPlayerId = -1;

        public bool IsEmpty => RestockLeft > MatchSim.Epsilon;
        public bool IsReady => !IsEmpty;
        public double RestockProgress01 => RestockSeconds > MatchSim.Epsilon
            ? GMath.Clampf(1.0 - RestockLeft / RestockSeconds, 0.0, 1.0)
            : 1.0;
    }

    /// <summary>Roaming untargetable Selecta ice-cream bike patrolling the river road (docs/SPEC.md §6).</summary>
    public sealed class SelectaBikeState
    {
        public bool Active;
        public Vec2 Position;
        public Vec2 Facing = new Vec2(1.0, 0.0);
        public int WaypointIndex;
        public double RespawnLeft;
        public double InitialSpawnSeconds = 30.0;
        public double RespawnSeconds = 60.0;
        public double PatrolSpeed = 4.2;
        public double CatchRadius = 2.0;
        public int InstantHeal = 20;
        public double BuffDuration = 10.0;
        public double SpeedBonus = 0.20;
        public double WaterRegenBonus = 3.0;
        public double BellPhase;
    }

    /// <summary>Crocodile (Dragon) or Philippine Eagle (Baron) deterministic runtime state.</summary>
    public sealed class NeutralBossInstance
    {
        public NeutralBossKind Kind;
        public string Id = "";
        public string DisplayName = "";
        public Vec2 HomePosition;
        public Vec2 Position;
        public Vec2 Facing = new Vec2(0.0, 1.0);
        public double HitRadius = 2.6;
        public int MaxHp = 600;
        public int Hp = 600;
        public NeutralBossState State = NeutralBossState.Dormant;
        public double InitialSpawnSeconds = 60.0;
        public double RespawnSeconds = 120.0;
        public double RespawnLeft = 60.0;
        public double VisualScale = 1.0;
        public double FleeDuration = 2.5;
        public double FleeTimeLeft;
        public Vec2 FleeDir = new Vec2(0.0, -1.0);
        public int LastAttackerId = -1;
        public int LastAttackerTeam = -1;
        public double AggroLeft;
        public double RetaliateCooldown;

        public bool CanBeDamaged => State == NeutralBossState.Idle || State == NeutralBossState.Angry;
        public double HpRatio01 => MaxHp > 0 ? GMath.Clampf((double)Hp / MaxHp, 0.0, 1.0) : 0.0;
    }

    /// <summary>Active timed neutral buffs for one team (end at round end; drive HUD buff row + foot aura).</summary>
    public struct TeamBuffState
    {
        public double DragonLeft;
        public double BaronLeft;
        public double SelectaLeft;

        public bool HasDragon => DragonLeft > MatchSim.Epsilon;
        public bool HasBaron => BaronLeft > MatchSim.Epsilon;
        public bool HasSelecta => SelectaLeft > MatchSim.Epsilon;
        public bool HasFootAura => HasDragon || HasBaron || HasSelecta;

        public void Clear()
        {
            DragonLeft = 0.0;
            BaronLeft = 0.0;
            SelectaLeft = 0.0;
        }
    }

    /// <summary>
    /// Deterministic half-time side swap helper (docs/SPEC.md §6):
    /// swaps Uptown/Riverside halves after round 2 (Blitz), round 4 (Quick), or round 6 (Standard),
    /// preserving round scores while resetting water (Elixir), ultimate charge, skill cooldowns, and running the 10 s swap phase.
    /// </summary>
    public sealed class HalfTimeSideSwap
    {
        public const double SwapPhaseSeconds = 10.0;

        public event Action<double> SwapStarted;
        public event Action SwapFinished;

        public bool Swapped { get; private set; }
        public double SwapPhaseLeft { get; private set; }
        public bool IsSwapping => SwapPhaseLeft > MatchSim.Epsilon;

        /// <summary>Halfway round count that triggers the swap (Blitz: 2, Quick: 4, Standard: 6).</summary>
        public static int HalfTimeRoundFor(int firstTo)
        {
            if (firstTo <= 3) return 2;
            if (firstTo <= 5) return 4;
            return 6;
        }

        /// <summary>Returns the map half (+1 Riverside, -1 Uptown) currently owned by <paramref name="team"/>.</summary>
        public int SideForTeam(int team)
        {
            int baseSide = team == 0 ? MapLayout.HalfRiverside : MapLayout.HalfUptown;
            return Swapped ? -baseSide : baseSide;
        }

        /// <summary>Display name ("UPTOWN" or "RIVERSIDE") of <paramref name="team"/>'s current half.</summary>
        public string HalfNameForTeam(int team) => MapLayout.HalfDisplayName(SideForTeam(team));

        /// <summary>Resets swap state for a brand-new match.</summary>
        public void Reset()
        {
            Swapped = false;
            SwapPhaseLeft = 0.0;
        }

        /// <summary>
        /// Triggers the one-time half-time swap once <paramref name="completedRounds"/> reaches <see cref="HalfTimeRoundFor"/>.
        /// Preserves <paramref name="score"/> untouched, flips halves, resets water &amp; ultimate/skill cooldowns,
        /// relocates spawns to the new half's base, resets neutrals, and starts the 10 s swap phase.
        /// </summary>
        public bool TryBeginHalfTimeSwap(
            int completedRounds,
            int firstTo,
            MatchScore score,
            OrderedDict<int, PlayerState> players,
            GameRules rules,
            MapLayout layout,
            BarangayNeutrals neutrals = null)
        {
            if (Swapped) return false;
            if (score != null && score.Winner >= 0) return false;
            if (completedRounds < HalfTimeRoundFor(firstTo)) return false;

            Swapped = true;
            SwapPhaseLeft = SwapPhaseSeconds;

            double waterFull = rules != null && rules.ElixirMax > 0.0 ? rules.ElixirMax : 100.0;
            int maxHp = rules != null && rules.PlayerMaxHp > 0 ? rules.PlayerMaxHp : 100;
            double maxStamina = rules != null && rules.StaminaMax > 0.0 ? rules.StaminaMax : 100.0;

            if (players != null)
            {
                IReadOnlyList<int> keys = players.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    PlayerState p = players[keys[i]];
                    int side = SideForTeam(p.Team);
                    Vec2 baseSpot = layout != null
                        ? (layout.ArenaRadius > 0.0 ? layout.AsymmetricBaseCenter(side) : layout.SpawnCenter(side))
                        : new Vec2(0.0, side * 64.0);

                    p.SpawnPosition = baseSpot;
                    p.Position = baseSpot;
                    p.Facing = new Vec2(0.0, -side);
                    p.Hp = maxHp;
                    p.Tagged = false;
                    p.TagTime = 0.0;
                    p.TagOrder = 0;
                    p.TaggedBy = -1;
                    p.RevengeLeft = 0.0;
                    p.ShieldLeft = 0.0;
                    p.Elixir = waterFull; // Chunk 2 integration: switch Elixir -> Water
                    p.UltCharge = 0.0;
                    p.Stamina = maxStamina;
                    p.PrimaryCd = 0.0;
                    p.DashCd = 0.0;
                    p.JumpCd = 0.0;
                    p.SlideCd = 0.0;
                    p.BuffType = PlayerState.BuffNone;
                    p.BuffLeft = 0.0;
                    p.BuffRegenCarry = 0.0;
                    for (int s = 0; s < p.WeaponCooldowns.Count; s++)
                        p.WeaponCooldowns[s] = 0.0;
                }
            }

            if (neutrals != null)
                neutrals.ResetRound();

            SwapStarted?.Invoke(SwapPhaseSeconds);
            return true;
        }

        /// <summary>Steps the 10 s swap phase; returns true on the tick it completes.</summary>
        public bool StepSwapPhase(double dt)
        {
            if (SwapPhaseLeft <= MatchSim.Epsilon) return false;
            SwapPhaseLeft = GMath.Maxf(0.0, SwapPhaseLeft - dt);
            if (SwapPhaseLeft <= MatchSim.Epsilon)
            {
                SwapPhaseLeft = 0.0;
                SwapFinished?.Invoke();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Deterministic 30 Hz pure C# living-world &amp; neutrals module for Barangay (docs/SPEC.md §6, docs/MAPS.md, docs/KITS.md L36).
    /// Zero engine dependencies and zero per-tick GC allocations; plugs cleanly into <see cref="MatchSim"/> in 3 lines
    /// via <see cref="Step(double, OrderedDict{int, PlayerState}, GodotRng)"/>.
    /// </summary>
    public sealed class BarangayNeutrals
    {
        public const double DragonBuffDuration = 90.0;
        public const double BaronBuffDuration = 90.0;
        public const double DragonDamageBonus = 0.08;
        public const double BaronRoleBonus = 0.08;
        public const double BossAggroTimeout = 6.0;
        public const double BossLeashRange = 12.0;
        public const double BossRetaliateInterval = 1.5;
        public const int BossRetaliateDamage = 6;
        public const double BossIdleScale = 1.0;
        public const double BossAngryScale = 1.45;
        public const double BossFleeScale = 0.55;
        public const double BossFleeSpeed = 9.5;
        public const double BossScaleLerpRate = 1.8;
        const int MaxEatingSlots = 16;

        struct EatingSlot
        {
            public bool Active;
            public int PlayerId;
            public int VendorIndex;
            public double TimeLeft;
            public double TotalSeconds;
            public int HealAmount;
            public int HealedSoFar;
            public double SlowMult;
        }

        public event Action<int, string> VendorUsed;
        public event Action<string> VendorRestocked;
        public event Action<int, int> SelectaClaimed;
        public event Action<NeutralBossKind, NeutralBossState, NeutralBossState> NeutralBossStateChanged;
        public event Action<NeutralBossKind, int, int> NeutralBossDefeated;
        public event Action<NeutralBossKind, int, int> NeutralBossRetaliated;

        public readonly MapLayout Layout;
        public readonly GameRules Rules;
        public readonly HalfTimeSideSwap SideSwap = new HalfTimeSideSwap();

        readonly VendorState[] _vendors;
        readonly Vec2[] _selectaWaypoints;
        readonly NeutralBossInstance[] _bosses = new NeutralBossInstance[2];
        readonly TeamBuffState[] _teamBuffs = new TeamBuffState[2];
        readonly EatingSlot[] _eatingSlots = new EatingSlot[MaxEatingSlots];

        public double RoundClock { get; private set; }
        public SelectaBikeState Selecta { get; } = new SelectaBikeState();
        public IReadOnlyList<VendorState> Vendors => _vendors;
        public IReadOnlyList<Vec2> SelectaWaypoints => _selectaWaypoints;
        public NeutralBossInstance Dragon => _bosses[(int)NeutralBossKind.Dragon];
        public NeutralBossInstance Baron => _bosses[(int)NeutralBossKind.Baron];

        public BarangayNeutrals(MapLayout layout, GameRules rules = null)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Rules = rules;

            IReadOnlyList<VendorSpot> vendorDefs = layout.GetVendorSpots();
            _vendors = new VendorState[vendorDefs.Count];
            for (int i = 0; i < vendorDefs.Count; i++)
            {
                VendorSpot v = vendorDefs[i];
                _vendors[i] = new VendorState
                {
                    Id = v.Id,
                    Name = v.Name,
                    Lane = v.Lane,
                    Position = v.Position,
                    Radius = v.Radius,
                    HealAmount = v.HealAmount,
                    EatSeconds = v.EatSeconds,
                    EatSlowMult = v.EatSlowMult,
                    RestockSeconds = v.RestockSeconds,
                };
            }

            IReadOnlyList<Vec2> wps = layout.GetSelectaWaypoints();
            _selectaWaypoints = new Vec2[wps.Count];
            for (int i = 0; i < wps.Count; i++)
                _selectaWaypoints[i] = wps[i];

            _bosses[(int)NeutralBossKind.Dragon] = new NeutralBossInstance
            {
                Kind = NeutralBossKind.Dragon,
                Id = "crocodile",
                DisplayName = "Crocodile",
                HomePosition = layout.DragonSpawn,
                Position = layout.DragonSpawn,
                HitRadius = 2.6,
                MaxHp = 600,
                Hp = 600,
                InitialSpawnSeconds = 60.0,
                RespawnSeconds = 120.0,
            };

            _bosses[(int)NeutralBossKind.Baron] = new NeutralBossInstance
            {
                Kind = NeutralBossKind.Baron,
                Id = "philippine_eagle",
                DisplayName = "Philippine Eagle",
                HomePosition = layout.BaronSpawn,
                Position = layout.BaronSpawn,
                HitRadius = 2.8,
                MaxHp = 600,
                Hp = 600,
                InitialSpawnSeconds = 180.0,
                RespawnSeconds = 180.0,
            };

            ResetRound();
        }

        /// <summary>Canonical kid role mapping (docs/SPEC.md §7).</summary>
        public static KidRole RoleOf(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return KidRole.Striker;
            if (EqualsCi(characterId, "jopay") || EqualsCi(characterId, "oriang") || EqualsCi(characterId, "paeng"))
                return KidRole.Defender;
            if (EqualsCi(characterId, "kikay") || EqualsCi(characterId, "karding") || EqualsCi(characterId, "manoy"))
                return KidRole.Seeker;
            if (EqualsCi(characterId, "tere") || EqualsCi(characterId, "pamela") || EqualsCi(characterId, "domeng"))
                return KidRole.Controller;
            return KidRole.Striker;
        }

        static bool EqualsCi(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Resets round-scoped living-world state: clears team buffs, restocks vendors, resets spawn timers.</summary>
        public void ResetRound()
        {
            RoundClock = 0.0;
            _teamBuffs[0].Clear();
            _teamBuffs[1].Clear();

            for (int i = 0; i < MaxEatingSlots; i++)
                _eatingSlots[i] = default;

            for (int i = 0; i < _vendors.Length; i++)
            {
                _vendors[i].RestockLeft = 0.0;
                _vendors[i].ServingPlayerId = -1;
            }

            Selecta.Active = false;
            Selecta.WaypointIndex = 0;
            Selecta.Position = _selectaWaypoints.Length > 0 ? _selectaWaypoints[0] : Vec2.Zero;
            Selecta.Facing = new Vec2(1.0, 0.0);
            Selecta.RespawnLeft = Selecta.InitialSpawnSeconds;
            Selecta.BellPhase = 0.0;

            for (int b = 0; b < 2; b++)
            {
                NeutralBossInstance boss = _bosses[b];
                boss.State = NeutralBossState.Dormant;
                boss.Hp = boss.MaxHp;
                boss.Position = boss.HomePosition;
                boss.Facing = new Vec2(0.0, 1.0);
                boss.RespawnLeft = boss.InitialSpawnSeconds;
                boss.VisualScale = BossIdleScale;
                boss.FleeTimeLeft = 0.0;
                boss.LastAttackerId = -1;
                boss.LastAttackerTeam = -1;
                boss.AggroLeft = 0.0;
                boss.RetaliateCooldown = 0.0;
            }
        }

        /// <summary>Convenience 1-line plug-in entry point for <see cref="MatchSim"/> integration.</summary>
        public void Step(double dt, MatchSim sim)
        {
            if (sim == null) return;
            Step(dt, sim.Players, sim.Rng);
        }

        /// <summary>Deterministic 30 Hz fixed-step update for vendors, Selecta bike, Crocodile, Eagle, buffs, and side swap.</summary>
        public void Step(double dt, OrderedDict<int, PlayerState> players, GodotRng rng = null)
        {
            if (dt <= 0.0) return;

            if (SideSwap.IsSwapping)
            {
                SideSwap.StepSwapPhase(dt);
                return;
            }

            RoundClock += dt;
            int maxHp = Rules != null && Rules.PlayerMaxHp > 0 ? Rules.PlayerMaxHp : 100;
            double maxWater = Rules != null && Rules.ElixirMax > 0.0 ? Rules.ElixirMax : 100.0;

            StepTeamBuffs(dt, players, maxWater);
            StepEatingWindows(dt, players, maxHp);
            StepVendors(dt, players);
            StepSelectaBike(dt, players, maxHp, rng);
            StepBoss(_bosses[(int)NeutralBossKind.Dragon], dt, players);
            StepBoss(_bosses[(int)NeutralBossKind.Baron], dt, players);
        }

        // ---- Damage / Untargetability / CC-Immunity Contracts -------------------------------------------------------------------

        /// <summary>Vendors are strictly untargetable (docs/SPEC.md §6, docs/GRAPHICS.md L144).</summary>
        public bool TryDamageVendor(string vendorId, int damage) => false;

        /// <summary>Selecta bike is strictly untargetable; claimed only by touching (docs/GRAPHICS.md L147).</summary>
        public bool TryDamageSelecta(int damage) => false;

        /// <summary>
        /// Crowd control and reveals do nothing to Dragon and Baron (docs/KITS.md L36).
        /// Always returns <c>false</c> without mutating boss state.
        /// </summary>
        public bool TryApplyNeutralCcOrReveal(NeutralBossKind kind, StatusEffects.Type effect) => false;

        /// <summary>
        /// Applies slingshot stone or damage-skill damage to Crocodile (Dragon) or Philippine Eagle (Baron).
        /// Non-damage skills (<paramref name="isDamageSkillOrStone"/> == false) are ignored.
        /// Transitions Idle -&gt; Angry on first hit, and at 0 HP enters Fleeing (zero gore!) and grants the killer's team the buff.
        /// </summary>
        public bool DamageNeutral(
            NeutralBossKind kind,
            int damage,
            int attackerId,
            int attackerTeam,
            bool isDamageSkillOrStone = true,
            GodotRng rng = null)
        {
            if (!isDamageSkillOrStone || damage <= 0) return false;
            NeutralBossInstance boss = _bosses[(int)kind];
            if (!boss.CanBeDamaged) return false;

            boss.Hp = GMath.Maxi(0, boss.Hp - damage);
            boss.LastAttackerId = attackerId;
            boss.LastAttackerTeam = attackerTeam;
            boss.AggroLeft = BossAggroTimeout;

            if (boss.Hp <= 0)
            {
                NeutralBossState prev = boss.State;
                boss.State = NeutralBossState.Fleeing;
                boss.FleeTimeLeft = boss.FleeDuration;
                boss.RetaliateCooldown = 0.0;

                double baseAngle = kind == NeutralBossKind.Dragon ? Math.PI : 0.0;
                double jitter = rng != null ? (rng.Randf() - 0.5) * 0.4 : 0.0;
                boss.FleeDir = Vec2.FromAngle(baseAngle + jitter);
                boss.Facing = boss.FleeDir;

                if (attackerTeam >= 0 && attackerTeam < 2)
                {
                    if (kind == NeutralBossKind.Dragon)
                        _teamBuffs[attackerTeam].DragonLeft = DragonBuffDuration;
                    else
                        _teamBuffs[attackerTeam].BaronLeft = BaronBuffDuration;
                }

                NeutralBossStateChanged?.Invoke(kind, prev, NeutralBossState.Fleeing);
                NeutralBossDefeated?.Invoke(kind, attackerId, attackerTeam);
                return true;
            }

            if (boss.State == NeutralBossState.Idle)
            {
                boss.State = NeutralBossState.Angry;
                boss.RetaliateCooldown = BossRetaliateInterval * 0.5;
                NeutralBossStateChanged?.Invoke(kind, NeutralBossState.Idle, NeutralBossState.Angry);
            }
            return true;
        }

        /// <summary>Gets the Dragon or Baron instance by kind.</summary>
        public NeutralBossInstance GetBoss(NeutralBossKind kind) => _bosses[(int)kind];

        // ---- Pure Deterministic Tall-Cover Stealth, Terrain & Buff Queries ------------------------------------------------------

        /// <summary>
        /// True when a kid at <paramref name="pos"/> is hidden inside tall cover from an enemy at <paramref name="viewerPos"/>:
        /// hidden beyond 6 m, visible within 6 m or when revealed (docs/SPEC.md §6, docs/GRAPHICS.md L73).
        /// </summary>
        public bool IsHiddenInTallCover(Vec2 pos, Vec2 viewerPos, bool revealed) =>
            IsHiddenInTallCover(pos, viewerPos, revealed, sameTeam: false, breakingAction: false);

        /// <summary>
        /// Full tall-cover stealth query: visible to allies, within 6 m, when revealed, or when performing a stealth-breaking
        /// action (attacking, starting a rescue, or capturing the enemy base).
        /// </summary>
        public bool IsHiddenInTallCover(Vec2 pos, Vec2 viewerPos, bool revealed, bool sameTeam, bool breakingAction)
        {
            if (sameTeam || revealed || breakingAction) return false;
            if (!Layout.IsInTallCover(pos)) return false;
            double r = Layout.TallCoverRevealRadius > 0.0 ? Layout.TallCoverRevealRadius : 6.0;
            return pos.DistanceSquaredTo(viewerPos) > r * r;
        }

        /// <summary>Terrain speed multiplier at <paramref name="pos"/> (0.85 in shallow river water, 1.0 on dry ground &amp; bridges).</summary>
        public double GetTerrainSpeedMultiplier(Vec2 pos) => Layout.GetTerrainSpeedMultiplier(pos);

        /// <summary>True when wading in shallow river water (makes footsteps louder; docs/SPEC.md §6).</summary>
        public bool IsLoudFootsteps(Vec2 pos) => Layout.IsInShallowWater(pos);

        /// <summary>True while <paramref name="playerId"/> is in a vendor eating window (healing + slowed).</summary>
        public bool IsPlayerEating(int playerId)
        {
            for (int i = 0; i < MaxEatingSlots; i++)
                if (_eatingSlots[i].Active && _eatingSlots[i].PlayerId == playerId) return true;
            return false;
        }

        /// <summary>Movement speed multiplier from vendor eating window (0.70 while eating, 1.0 otherwise).</summary>
        public double GetEatingSpeedMultiplier(int playerId)
        {
            for (int i = 0; i < MaxEatingSlots; i++)
                if (_eatingSlots[i].Active && _eatingSlots[i].PlayerId == playerId) return _eatingSlots[i].SlowMult;
            return 1.0;
        }

        /// <summary>Snapshot of timed neutral buffs for <paramref name="team"/> (0 or 1).</summary>
        public TeamBuffState GetTeamBuffs(int team) =>
            (team >= 0 && team < 2) ? _teamBuffs[team] : default;

        /// <summary>True when <paramref name="team"/> currently has any active neutral buff (shows foot aura ring).</summary>
        public bool HasFootAura(int team) => team >= 0 && team < 2 && _teamBuffs[team].HasFootAura;

        /// <summary>Combined damage multiplier from Dragon (+8% team) and Baron (+8% Strikers).</summary>
        public double GetDamageMultiplier(int team, string characterId)
        {
            if (team < 0 || team >= 2) return 1.0;
            double mult = 1.0;
            if (_teamBuffs[team].HasDragon) mult += DragonDamageBonus;
            if (_teamBuffs[team].HasBaron && RoleOf(characterId) == KidRole.Striker) mult += BaronRoleBonus;
            return mult;
        }

        /// <summary>Incoming damage multiplier from Baron (-8% damage taken for Defenders).</summary>
        public double GetDamageTakenMultiplier(int team, string characterId)
        {
            if (team < 0 || team >= 2) return 1.0;
            return _teamBuffs[team].HasBaron && RoleOf(characterId) == KidRole.Defender
                ? 1.0 - BaronRoleBonus
                : 1.0;
        }

        /// <summary>Skill cooldown duration multiplier from Baron (-8% cooldowns for Controllers).</summary>
        public double GetCooldownMultiplier(int team, string characterId)
        {
            if (team < 0 || team >= 2) return 1.0;
            return _teamBuffs[team].HasBaron && RoleOf(characterId) == KidRole.Controller
                ? 1.0 - BaronRoleBonus
                : 1.0;
        }

        /// <summary>Extra water/elixir regeneration per second from Selecta team buff.</summary>
        public double GetTeamWaterRegenBonus(int team)
        {
            if (team < 0 || team >= 2) return 0.0;
            return _teamBuffs[team].HasSelecta ? Selecta.WaterRegenBonus : 0.0;
        }

        /// <summary>
        /// Combined movement speed multiplier stacking shallow river water (0.85x), vendor eating slow (0.70x),
        /// Selecta team buff (+20%), and Baron Seeker role buff (+8%).
        /// </summary>
        public double GetPlayerMoveSpeedMultiplier(int playerId, int team, string characterId, Vec2 pos)
        {
            double mult = GetTerrainSpeedMultiplier(pos) * GetEatingSpeedMultiplier(playerId);
            if (team >= 0 && team < 2)
            {
                double bonus = 1.0;
                if (_teamBuffs[team].HasSelecta) bonus += Selecta.SpeedBonus;
                if (_teamBuffs[team].HasBaron && RoleOf(characterId) == KidRole.Seeker) bonus += BaronRoleBonus;
                mult *= bonus;
            }
            return mult;
        }

        // ---- Internal Zero-GC Fixed-Step Subsystems -----------------------------------------------------------------------------

        void StepTeamBuffs(double dt, OrderedDict<int, PlayerState> players, double maxWater)
        {
            for (int t = 0; t < 2; t++)
            {
                if (_teamBuffs[t].DragonLeft > 0.0)
                    _teamBuffs[t].DragonLeft = GMath.Maxf(0.0, _teamBuffs[t].DragonLeft - dt);
                if (_teamBuffs[t].BaronLeft > 0.0)
                    _teamBuffs[t].BaronLeft = GMath.Maxf(0.0, _teamBuffs[t].BaronLeft - dt);
                if (_teamBuffs[t].SelectaLeft > 0.0)
                    _teamBuffs[t].SelectaLeft = GMath.Maxf(0.0, _teamBuffs[t].SelectaLeft - dt);
            }

            if (players == null) return;
            if (!_teamBuffs[0].HasSelecta && !_teamBuffs[1].HasSelecta) return;

            IReadOnlyList<int> keys = players.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                PlayerState p = players[keys[i]];
                if (p.Tagged || p.Team < 0 || p.Team >= 2) continue;
                if (!_teamBuffs[p.Team].HasSelecta) continue;
                p.Elixir = GMath.Minf(maxWater, p.Elixir + Selecta.WaterRegenBonus * dt); // Chunk 2 integration: switch Elixir -> Water
            }
        }

        void StepEatingWindows(double dt, OrderedDict<int, PlayerState> players, int maxHp)
        {
            for (int i = 0; i < MaxEatingSlots; i++)
            {
                if (!_eatingSlots[i].Active) continue;
                EatingSlot slot = _eatingSlots[i];
                PlayerState p = players != null ? players.GetOrDefault(slot.PlayerId) : null;
                if (p == null || p.Tagged)
                {
                    ClearVendorServing(slot.VendorIndex, slot.PlayerId);
                    _eatingSlots[i] = default;
                    continue;
                }

                slot.TimeLeft = GMath.Maxf(0.0, slot.TimeLeft - dt);
                double progress = slot.TotalSeconds > MatchSim.Epsilon
                    ? GMath.Clampf(1.0 - slot.TimeLeft / slot.TotalSeconds, 0.0, 1.0)
                    : 1.0;
                int targetHealed = slot.TimeLeft <= MatchSim.Epsilon
                    ? slot.HealAmount
                    : GMath.Floori(slot.HealAmount * progress + MatchSim.Epsilon);
                int delta = targetHealed - slot.HealedSoFar;
                if (delta > 0)
                {
                    p.Hp = GMath.Mini(maxHp, p.Hp + delta);
                    slot.HealedSoFar += delta;
                }

                if (slot.TimeLeft <= MatchSim.Epsilon)
                {
                    ClearVendorServing(slot.VendorIndex, slot.PlayerId);
                    _eatingSlots[i] = default;
                }
                else
                {
                    _eatingSlots[i] = slot;
                }
            }
        }

        void ClearVendorServing(int vendorIndex, int playerId)
        {
            if (vendorIndex >= 0 && vendorIndex < _vendors.Length && _vendors[vendorIndex].ServingPlayerId == playerId)
                _vendors[vendorIndex].ServingPlayerId = -1;
        }

        void StepVendors(double dt, OrderedDict<int, PlayerState> players)
        {
            for (int v = 0; v < _vendors.Length; v++)
            {
                VendorState vendor = _vendors[v];
                if (vendor.RestockLeft > 0.0)
                {
                    vendor.RestockLeft = GMath.Maxf(0.0, vendor.RestockLeft - dt);
                    if (vendor.RestockLeft <= MatchSim.Epsilon)
                    {
                        vendor.RestockLeft = 0.0;
                        VendorRestocked?.Invoke(vendor.Id);
                    }
                    continue;
                }

                if (players == null) continue;
                double reachSq = vendor.Radius * vendor.Radius;
                IReadOnlyList<int> keys = players.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    PlayerState p = players[keys[i]];
                    if (p.Tagged || IsPlayerEating(p.Id)) continue;
                    if (p.Position.DistanceSquaredTo(vendor.Position) > reachSq) continue;

                    StartEating(v, vendor, p.Id);
                    vendor.RestockLeft = vendor.RestockSeconds;
                    vendor.ServingPlayerId = p.Id;
                    VendorUsed?.Invoke(p.Id, vendor.Id);
                    break;
                }
            }
        }

        void StartEating(int vendorIndex, VendorState vendor, int playerId)
        {
            for (int i = 0; i < MaxEatingSlots; i++)
            {
                if (_eatingSlots[i].Active) continue;
                _eatingSlots[i] = new EatingSlot
                {
                    Active = true,
                    PlayerId = playerId,
                    VendorIndex = vendorIndex,
                    TimeLeft = vendor.EatSeconds,
                    TotalSeconds = vendor.EatSeconds,
                    HealAmount = vendor.HealAmount,
                    HealedSoFar = 0,
                    SlowMult = vendor.EatSlowMult,
                };
                return;
            }
        }

        void StepSelectaBike(double dt, OrderedDict<int, PlayerState> players, int maxHp, GodotRng rng)
        {
            if (!Selecta.Active)
            {
                Selecta.RespawnLeft = GMath.Maxf(0.0, Selecta.RespawnLeft - dt);
                if (Selecta.RespawnLeft <= MatchSim.Epsilon)
                {
                    Selecta.RespawnLeft = 0.0;
                    Selecta.Active = true;
                    if (_selectaWaypoints.Length > 0)
                    {
                        int startIdx = rng != null ? rng.RandiRange(0, _selectaWaypoints.Length - 1) : 0;
                        Selecta.WaypointIndex = startIdx;
                        Selecta.Position = _selectaWaypoints[startIdx];
                    }
                }
                return;
            }

            Selecta.BellPhase = GMath.Fposmod(Selecta.BellPhase + dt * 1.4, 1.0);

            if (_selectaWaypoints.Length > 1)
            {
                double stepBudget = Selecta.PatrolSpeed * dt;
                int guard = 0;
                while (stepBudget > MatchSim.Epsilon && guard < 4)
                {
                    guard++;
                    int nextIdx = (Selecta.WaypointIndex + 1) % _selectaWaypoints.Length;
                    Vec2 target = _selectaWaypoints[nextIdx];
                    Vec2 delta = target - Selecta.Position;
                    double dist = delta.Length();
                    if (dist <= stepBudget)
                    {
                        Selecta.Position = target;
                        Selecta.WaypointIndex = nextIdx;
                        stepBudget -= dist;
                        if (dist > MatchSim.Epsilon) Selecta.Facing = delta / dist;
                    }
                    else
                    {
                        Vec2 dir = delta / dist;
                        Selecta.Position = Selecta.Position + dir * stepBudget;
                        Selecta.Facing = dir;
                        stepBudget = 0.0;
                    }
                }
            }

            if (players == null) return;
            double reachSq = Selecta.CatchRadius * Selecta.CatchRadius;
            IReadOnlyList<int> keys = players.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                PlayerState p = players[keys[i]];
                if (p.Tagged) continue;
                if (p.Position.DistanceSquaredTo(Selecta.Position) > reachSq) continue;

                p.Hp = GMath.Mini(maxHp, p.Hp + Selecta.InstantHeal);
                if (p.Team >= 0 && p.Team < 2)
                    _teamBuffs[p.Team].SelectaLeft = Selecta.BuffDuration;

                Selecta.Active = false;
                Selecta.RespawnLeft = Selecta.RespawnSeconds;
                SelectaClaimed?.Invoke(p.Id, p.Team);
                break;
            }
        }

        void StepBoss(NeutralBossInstance boss, double dt, OrderedDict<int, PlayerState> players)
        {
            switch (boss.State)
            {
                case NeutralBossState.Dormant:
                    boss.RespawnLeft = GMath.Maxf(0.0, boss.RespawnLeft - dt);
                    if (boss.RespawnLeft <= MatchSim.Epsilon)
                    {
                        boss.RespawnLeft = 0.0;
                        boss.Hp = boss.MaxHp;
                        boss.Position = boss.HomePosition;
                        boss.VisualScale = BossIdleScale;
                        boss.LastAttackerId = -1;
                        boss.LastAttackerTeam = -1;
                        boss.AggroLeft = 0.0;
                        boss.State = NeutralBossState.Idle;
                        NeutralBossStateChanged?.Invoke(boss.Kind, NeutralBossState.Dormant, NeutralBossState.Idle);
                    }
                    break;

                case NeutralBossState.Idle:
                    boss.VisualScale = MoveTowardScalar(boss.VisualScale, BossIdleScale, BossScaleLerpRate * dt);
                    break;

                case NeutralBossState.Angry:
                    boss.VisualScale = MoveTowardScalar(boss.VisualScale, BossAngryScale, BossScaleLerpRate * dt);
                    boss.AggroLeft = GMath.Maxf(0.0, boss.AggroLeft - dt);
                    if (boss.AggroLeft <= MatchSim.Epsilon)
                    {
                        boss.Hp = boss.MaxHp;
                        boss.LastAttackerId = -1;
                        boss.LastAttackerTeam = -1;
                        boss.State = NeutralBossState.Idle;
                        NeutralBossStateChanged?.Invoke(boss.Kind, NeutralBossState.Angry, NeutralBossState.Idle);
                        break;
                    }

                    boss.RetaliateCooldown = GMath.Maxf(0.0, boss.RetaliateCooldown - dt);
                    if (boss.RetaliateCooldown <= MatchSim.Epsilon && players != null)
                    {
                        PlayerState target = FindRetaliationTarget(boss, players);
                        if (target != null)
                        {
                            boss.Facing = boss.Position.DirectionTo(target.Position);
                            int applied = GMath.Mini(BossRetaliateDamage, GMath.Maxi(0, target.Hp - 1));
                            if (applied > 0)
                            {
                                target.Hp -= applied;
                                NeutralBossRetaliated?.Invoke(boss.Kind, target.Id, applied);
                            }
                            boss.RetaliateCooldown = BossRetaliateInterval;
                        }
                    }
                    break;

                case NeutralBossState.Fleeing:
                    boss.VisualScale = MoveTowardScalar(boss.VisualScale, BossFleeScale, BossScaleLerpRate * dt);
                    boss.Position = boss.Position + boss.FleeDir * (BossFleeSpeed * dt);
                    boss.FleeTimeLeft = GMath.Maxf(0.0, boss.FleeTimeLeft - dt);
                    if (boss.FleeTimeLeft <= MatchSim.Epsilon)
                    {
                        boss.FleeTimeLeft = 0.0;
                        boss.State = NeutralBossState.Dormant;
                        boss.RespawnLeft = boss.RespawnSeconds;
                        NeutralBossStateChanged?.Invoke(boss.Kind, NeutralBossState.Fleeing, NeutralBossState.Dormant);
                    }
                    break;
            }
        }

        static PlayerState FindRetaliationTarget(NeutralBossInstance boss, OrderedDict<int, PlayerState> players)
        {
            double leashSq = BossLeashRange * BossLeashRange;
            PlayerState preferred = players.GetOrDefault(boss.LastAttackerId);
            if (preferred != null && !preferred.Tagged && preferred.Position.DistanceSquaredTo(boss.Position) <= leashSq)
                return preferred;

            PlayerState nearest = null;
            double bestSq = leashSq;
            IReadOnlyList<int> keys = players.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                PlayerState p = players[keys[i]];
                if (p.Tagged) continue;
                double dSq = p.Position.DistanceSquaredTo(boss.Position);
                if (dSq <= bestSq)
                {
                    bestSq = dSq;
                    nearest = p;
                }
            }
            return nearest;
        }

        static double MoveTowardScalar(double current, double target, double maxDelta)
        {
            double diff = target - current;
            if (Math.Abs(diff) <= maxDelta) return target;
            return current + (diff > 0.0 ? maxDelta : -maxDelta);
        }
    }
}
