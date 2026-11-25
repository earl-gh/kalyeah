using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Authoritative match simulation (port of scripts/sim/match_sim.gd). Never touches the engine; the same class runs on the server,
    /// in offline practice and in tests. Advances only through <see cref="Step"/> and uses its own seeded RNG.
    /// GDScript names map snake_case to PascalCase (signals become C# events of the same name; the views subscribe to them).
    /// </summary>
    public sealed class MatchSim
    {
        // ---- signals ----
        /// <summary>(id, byId): `id` was tagged; `byId` dealt the blow (-1: scripted).</summary>
        public event Action<int, int> PlayerTagged;
        /// <summary>(id, byId): `byId` is the rescuing teammate, or -1 (a dummy walked out, or a round reset).</summary>
        public event Action<int, int> PlayerRescued;
        /// <summary>(team, how): a team scored (`how` is a CaptureKind). A freeze follows.</summary>
        public event Action<int, int> PointScored;
        public event Action<int> MatchWon;
        /// <summary>The freeze is over: everyone is back at their post at full HP.</summary>
        public event Action PointReset;
        /// <summary>The opening show is over: play starts now.</summary>
        public event Action IntroFinished;
        public event Action<int, StatusEffects.Type> EffectApplied;
        public event Action<int, int> PlayerHealed;
        /// <summary>(id, weaponId): a weapon or skill was cast (for sounds and effects).</summary>
        public event Action<int, string> WeaponCast;
        /// <summary>(id, weaponId): a weapon's hit landed on player `id` (before its damage or effect).</summary>
        public event Action<int, string> WeaponHit;
        /// <summary>(id, slot): a new card took a slot of a hand (slot -1: the window).</summary>
        public event Action<int, int> CardDrawn;
        public event Action<int, int> PlayerDamaged;
        public event Action<int> Exhausted;
        public event Action<int> PlayerDashed;
        public event Action<int> UltimateCast;
        /// <summary>(playerId, powerupId, kind).</summary>
        public event Action<int, int, int> PowerupTaken;

        // ---- Chunk 2 spec combat events ----
        /// <summary>Authoritative hit-confirm event (body hit, headshot, TAYA! tag).</summary>
        public event Action<HitConfirmEvent> HitConfirmed;
        /// <summary>(ownerId, worldPos): a destructible player wall broke.</summary>
        public event Action<int, Vec2> WallBroken;
        /// <summary>(ownerId, worldPos, isDisarmed): a hidden trap or sentry triggered or was broken/disarmed.</summary>
        public event Action<int, Vec2, bool> TrapTriggered;
        /// <summary>(targetId, viewerTeam, kind, knownAlert): an enemy was revealed via Outline or Snapshot.</summary>
        public event Action<int, int, RevealKind, bool> PlayerRevealed;
        /// <summary>(playerId): Tirador 7-stone reload started (auto at 0 or manual via BtnReload).</summary>
        public event Action<int> TiradorReloadStarted;
        /// <summary>(playerId): player attempted to cast a skill without enough water.</summary>
        public event Action<int> WaterInsufficient;

        public const int ReleasedAlone = -1;
        /// <summary>PlayerRescued's rescuer id for a revenge release (docs/SPEC.md §5): the tagger was tagged.</summary>
        public const int ReleasedByRevenge = -2;
        /// <summary>Float slack for timers and the elixir bar (sums of 1/30 s ticks), not a gameplay number.</summary>
        public const double Epsilon = 0.00001;
        /// <summary>Mixed into the match seed for the trip rolls' own generator.</summary>
        public const long TripSeedMix = 0x7121B;
        /// <summary>Mixed into the match seed for deterministic BarangayNeutrals spawns/flee angles.</summary>
        public const long NeutralsSeedMix = 0x5EED42;
        /// <summary>Water restored immediately on Mang Taho / Fishball vendor pickup under SpecKits.</summary>
        public const double VendorWaterRestore = 35.0;
        /// <summary>HP restored immediately on Mang Taho / Fishball vendor pickup under SpecKits.</summary>
        public const int VendorInstantHeal = 30;
        public const int PowerupNone = 0;
        public const int PowerupPalamig = 1;
        public const int PowerupTeks = 2;
        public const int PowerupTuron = 3;
        /// <summary>Minimum `AirY` height (metres) that clears low pavement snares and jackstone fields.</summary>
        public const double JumpClearHeight = 0.45;
        public static readonly Vec2[] DefaultPowerupSpots = { new Vec2(-34.0, 0.0), new Vec2(0.0, 0.0), new Vec2(34.0, 0.0) };

        /// <summary>INTRO: the opening show; nothing moves.</summary>
        public enum MatchPhase { Playing, PointFreeze, MatchOver, Intro }
        public enum CaptureKind { None = -1, Base = 0, AllTagged = 1, Touched = 2, TooSlow = 3 }
        public enum ClockStage { Regular, Extended, Overtime }

        /// <summary>A Barangay merienda power-up stand (GDScript: a Dictionary with these keys).</summary>
        public sealed class Powerup
        {
            public int Id;
            public int Kind;
            public Vec2 Pos;
            public bool Active;
            public double RespawnLeft;
        }

        public GameRules Rules;
        public MapLayout Layout;
        public GodotRng Rng = new GodotRng();
        readonly GodotRng _tripRng = new GodotRng();
        readonly GodotRng _neutralsRng = new GodotRng();
        public int Tick;
        /// <summary>id to state, in insertion order (stepping order is deterministic).</summary>
        public OrderedDict<int, PlayerState> Players = new OrderedDict<int, PlayerState>();
        /// <summary>Cover on the street (from the map layout).</summary>
        public List<Rect2> Obstacles = new List<Rect2>();
        /// <summary>Low covers (from the map layout): they stop walking, not throws, and the jump button vaults them.</summary>
        public List<Rect2> LowCovers = new List<Rect2>();
        /// <summary>Height of each low cover, in the order of <see cref="LowCovers"/>.</summary>
        public List<double> LowCoverHeights = new List<double>();
        public WeaponSystem Weapons = new WeaponSystem();
        public WeaponCasting Casting = new WeaponCasting();
        public MatchObjectives Objectives = new MatchObjectives();
        public Dictionary<string, WeaponDef> WeaponDefs = new Dictionary<string, WeaponDef>();
        /// <summary>All loaded docs/KITS.md skill definitions indexed by SkillDef.Id.</summary>
        public Dictionary<string, SkillDef> SkillDefs = new Dictionary<string, SkillDef>();
        /// <summary>All loaded CharacterDefs indexed by CharacterDef.Id.</summary>
        public Dictionary<string, CharacterDef> CharacterDefs = new Dictionary<string, CharacterDef>();
        public MatchScore Score;
        /// <summary>The team whose capture countdown is running (-1: none), how, and for how long.</summary>
        public int CaptureTeam = -1;
        public CaptureKind CurrentCaptureKind = CaptureKind.None;
        public double CaptureElapsed;
        public double CaptureNeeded;
        public double CaptureRate = 1.0;
        /// <summary>Per team (0, 1): the teammate rescuing now (-1: none), the seconds into it, and seconds until the area is ready.</summary>
        public int[] RescueRescuer = { -1, -1 };
        public double[] RescueElapsed = { 0.0, 0.0 };
        public double[] RescueReadyLeft = { 0.0, 0.0 };
        /// <summary>Per team: where the current rescuer started (a defuse-style rescue cancels when the rescuer moves off it).</summary>
        public Vec2[] RescueAnchor = { Vec2.Zero, Vec2.Zero };
        /// <summary>Seconds of the round-start barrier left: kids stay in their own base and can't cast.</summary>
        public double BarrierLeft;
        public int TagCounter;
        public double RoundTime;
        public MatchPhase Phase = MatchPhase.Playing;
        public double FreezeLeft;
        public double IntroLeft;
        public double MatchTime;
        /// <summary>How every point was won, in order: (team that scored, CaptureKind).</summary>
        public List<Vec2I> PointLog = new List<Vec2I>();
        public List<Powerup> Powerups = new List<Powerup>();
        /// <summary>Live Barangay living-world vendors, Selecta bike, Crocodile &amp; Eagle (active strictly when Rules.UsesSpecKits() is true).</summary>
        public BarangayNeutrals Neutrals { get; private set; }
        /// <summary>A timed street game in place of Agawang Base's rescues and captures; null for Agawang Base.</summary>
        public IStreetMode Mode;

        readonly OrderedDict<int, PlayerInput> _inputs = new OrderedDict<int, PlayerInput>();
        readonly OrderedDict<int, DummyBrain> _brains = new OrderedDict<int, DummyBrain>();
        readonly Dictionary<int, int> _teamCounts = new Dictionary<int, int>();
        readonly List<Rect2> _blocking = new List<Rect2>();
        readonly List<Rect2> _walkBlocking = new List<Rect2>();
        readonly List<Rect2> _walkObstacles = new List<Rect2>();
        readonly List<Rect2> _boundaryBlocking = new List<Rect2>();
        public BarangayNav Nav;
        int _castSerial;
        readonly Dictionary<int, int> _castTick = new Dictionary<int, int>();
        Rect2 _street;

        /// <summary>The bots driving players (id to brain), in the order they were added. Coaches and tests read it.</summary>
        public OrderedDict<int, DummyBrain> Brains => _brains;
        /// <summary>The latest input of every player (id to input).</summary>
        public OrderedDict<int, PlayerInput> Inputs => _inputs;

        public MatchSim(GameRules rules, MapLayout layout, long seed)
        {
            Rules = rules;
            Layout = layout;
            Rng.Seed = seed;
            _tripRng.Seed = seed ^ TripSeedMix;
            _neutralsRng.Seed = seed ^ NeutralsSeedMix;
            Obstacles = layout.ObstacleRects();
            LowCovers = layout.LowCoverRects();
            for (int i = 0; i < LowCovers.Count; i++) LowCoverHeights.Add(layout.LowCoverHeight(i));
            _walkObstacles.AddRange(layout.WalkObstacleRects());
            _street = layout.LaneRect();
            _boundaryBlocking.AddRange(layout.BoundaryRects());
            _blocking.AddRange(_boundaryBlocking);
            _blocking.AddRange(Obstacles);
            _walkBlocking.AddRange(_boundaryBlocking);
            _walkBlocking.AddRange(_walkObstacles);
            Nav = new BarangayNav(layout, rules.PlayerRadius);
            foreach (WeaponDef def in rules.Weapons) WeaponDefs[def.Id] = def;
            if (rules.PrimaryWeapon != null) WeaponDefs[rules.PrimaryWeapon.Id] = rules.PrimaryWeapon;
            if (rules.UltimateWeapon != null) WeaponDefs[rules.UltimateWeapon.Id] = rules.UltimateWeapon;
            IndexCharactersAndSkills();
            Score = new MatchScore(rules.FirstTo);
            CaptureNeeded = rules.CaptureSoloTime;
            double ready = rules.SpecRules ? 0.0 : rules.RescueCooldown;
            RescueReadyLeft = new[] { ready, ready };
            BarrierLeft = rules.BarrierSeconds;
            ResetPowerups();
            if (rules != null && rules.UsesSpecKits() && layout != null)
            {
                Neutrals = new BarangayNeutrals(layout, rules);
                ConfigureLiveVendors(Neutrals);
                BindNeutrals(Neutrals);
            }
        }

        static void ConfigureLiveVendors(BarangayNeutrals neutrals)
        {
            if (neutrals == null) return;
            IReadOnlyList<VendorState> vendors = neutrals.Vendors;
            for (int i = 0; i < vendors.Count; i++)
            {
                // Instant +30 HP + +35 Water applied on pickup in OnNeutralVendorUsed; retain 30 s restock & eating slow window.
                vendors[i].HealAmount = 0;
            }
        }

        void BindNeutrals(BarangayNeutrals neutrals)
        {
            if (neutrals == null) return;
            neutrals.VendorUsed += OnNeutralVendorUsed;
            neutrals.SelectaClaimed += OnNeutralSelectaClaimed;
            neutrals.NeutralBossRetaliated += OnNeutralBossRetaliated;
        }

        void UnbindNeutrals()
        {
            if (Neutrals == null) return;
            Neutrals.VendorUsed -= OnNeutralVendorUsed;
            Neutrals.SelectaClaimed -= OnNeutralSelectaClaimed;
            Neutrals.NeutralBossRetaliated -= OnNeutralBossRetaliated;
        }

        void OnNeutralVendorUsed(int playerId, string _)
        {
            if (!Players.TryGetValue(playerId, out PlayerState state) || state.Tagged) return;
            if (Rules != null && Rules.UsesSpecKits())
            {
                state.Water = GMath.Minf(Rules.WaterMax, state.Water + VendorWaterRestore);
                Heal(playerId, VendorInstantHeal);
            }
        }

        void OnNeutralSelectaClaimed(int playerId, int _)
        {
            if (Neutrals != null && Neutrals.Selecta.InstantHeal > 0)
                PlayerHealed?.Invoke(playerId, Neutrals.Selecta.InstantHeal);
        }

        void OnNeutralBossRetaliated(NeutralBossKind _, int targetId, int applied)
        {
            if (applied > 0)
                PlayerDamaged?.Invoke(targetId, applied);
        }

        /// <summary>Applies damage directly to Crocodile (Dragon) or Philippine Eagle (Baron) when live neutrals are active.</summary>
        public bool DamageNeutral(NeutralBossKind kind, int damage, int attackerId, int attackerTeam, bool isDamageSkillOrStone = true)
        {
            if (Neutrals == null || Mode != null) return false;
            return Neutrals.DamageNeutral(kind, damage, attackerId, attackerTeam, isDamageSkillOrStone, _neutralsRng);
        }

        void IndexCharactersAndSkills()
        {
            CharacterDefs.Clear();
            SkillDefs.Clear();
            if (Rules.Characters == null) return;
            for (int i = 0; i < Rules.Characters.Count; i++)
            {
                CharacterDef c = Rules.Characters[i];
                if (c == null || string.IsNullOrEmpty(c.Id)) continue;
                CharacterDefs[c.Id] = c;
                if (c.Skills == null) continue;
                for (int s = 0; s < c.Skills.Count; s++)
                {
                    SkillDef sk = c.Skills[s];
                    if (sk != null && !string.IsNullOrEmpty(sk.Id)) SkillDefs[sk.Id] = sk;
                }
            }
        }

        // ---- signal emitters (GDScript's `sim.<signal>.emit(...)` from the other sim classes) ----
        public void EmitPlayerTagged(int id, int byId)
        {
            if (Players.TryGetValue(id, out PlayerState tagged))
            {
                Weapons.OnPlayerTagged(id);
                ClearTransientSpecState(tagged);
                // Narda ULT Tsinelas Bagsik: tagging an enemy hit by one refunds 50% of ULT cooldown.
                if (tagged.TsinelasMarkedLeft > 0.0 && tagged.TsinelasMarkedBy >= 0)
                {
                    if (Players.TryGetValue(tagged.TsinelasMarkedBy, out PlayerState narda)
                        && narda.UsingSpecKit && narda.WeaponCooldowns.Count >= 3)
                    {
                        SkillDef ult = GetPlayerSkill(narda, 2);
                        if (ult != null && ult.Mechanic == SkillMechanic.TsinelasBagsik)
                        {
                            double ratio = ult.BonusRatio > 0.0 ? ult.BonusRatio : 0.50;
                            narda.WeaponCooldowns[2] = GMath.Maxf(0.0, narda.WeaponCooldowns[2] - ult.Cooldown * ratio);
                        }
                    }
                    tagged.TsinelasMarkedLeft = 0.0;
                    tagged.TsinelasMarkedBy = -1;
                }
            }
            PlayerTagged?.Invoke(id, byId);
        }

        public void EmitPlayerRescued(int id, int byId) => PlayerRescued?.Invoke(id, byId);
        public void EmitPointScored(int team, int how) => PointScored?.Invoke(team, how);
        public void EmitMatchWon(int team) => MatchWon?.Invoke(team);
        public void EmitPointReset() => PointReset?.Invoke();
        public void EmitWeaponHit(int id, string weaponId) => WeaponHit?.Invoke(id, weaponId);
        public void EmitCardDrawn(int id, int slot) => CardDrawn?.Invoke(id, slot);
        public void EmitPlayerDashed(int id) => PlayerDashed?.Invoke(id);
        public void EmitUltimateCast(int id) => UltimateCast?.Invoke(id);
        public void EmitWallBroken(int ownerId, Vec2 pos) => WallBroken?.Invoke(ownerId, pos);
        public void EmitTrapTriggered(int ownerId, Vec2 pos, bool isDisarmed) => TrapTriggered?.Invoke(ownerId, pos, isDisarmed);
        public void EmitPlayerRevealed(int targetId, int viewerTeam, RevealKind kind, bool knownAlert) =>
            PlayerRevealed?.Invoke(targetId, viewerTeam, kind, knownAlert);
        public void EmitTiradorReloadStarted(int playerId) => TiradorReloadStarted?.Invoke(playerId);
        public void EmitWaterInsufficient(int playerId) => WaterInsufficient?.Invoke(playerId);

        /// <summary>Emits WeaponCast (the sim counts every cast first, like the GDScript's own first listener).</summary>
        public void EmitWeaponCast(int id, string weaponId)
        {
            _castSerial += 1;
            _castTick[id] = Tick;
            WeaponCast?.Invoke(id, weaponId);
        }

        // ---- spec kit helpers ----

        public SkillDef GetSkillDef(string id) => !string.IsNullOrEmpty(id) && SkillDefs.TryGetValue(id, out SkillDef def) ? def : null;

        public CharacterDef GetCharacterDef(string id) => !string.IsNullOrEmpty(id) && CharacterDefs.TryGetValue(id, out CharacterDef def) ? def : null;

        public bool HasSpecKit(string characterId)
        {
            CharacterDef def = GetCharacterDef(characterId);
            return def != null && def.HasSpecKit();
        }

        public SkillDef GetPlayerSkill(PlayerState state, int slot)
        {
            if (state == null || slot < 0 || slot >= state.Weapons.Count) return null;
            return GetSkillDef(state.Weapons[slot]);
        }

        /// <summary>Equips `id` with its CharacterDef's 3 spec skills [S1, S2, ULT]. Returns false if the kid has no kit.</summary>
        public bool EquipSpecKit(int id)
        {
            if (!Players.TryGetValue(id, out PlayerState state)) return false;
            CharacterDef def = GetCharacterDef(state.CharacterId);
            if (def == null || !def.HasSpecKit()) return false;
            state.UsingSpecKit = true;
            state.Cards = false;
            state.Weapons = new List<string> { def.Skills[0].Id, def.Skills[1].Id, def.Skills[2].Id };
            SizeSlots(state);
            state.Water = Rules.WaterMax;
            state.TiradorAmmo = Rules.TiradorMaxAmmo;
            state.TiradorReloadLeft = 0.0;
            state.TiradorChargeTime = -1.0;
            state.S1Charges = GMath.Maxi(1, def.Skills[0].MaxCharges);
            state.S1ChargeRegenLeft = 0.0;
            ClearTransientSpecState(state);
            return true;
        }

        /// <summary>Resets all players' ultimate cooldowns and water (called at half-time side swap per docs/SPEC.md §5 &amp; §6).</summary>
        public void ResetUltimateCooldowns()
        {
            foreach (var pair in Players)
            {
                PlayerState state = pair.Value;
                if (state.UsingSpecKit && state.WeaponCooldowns.Count >= 3)
                    state.WeaponCooldowns[2] = 0.0;
                state.Water = Rules.WaterMax;
            }
        }

        /// <summary>True when `state` can legally cast spec skill in `slot` right now (not tagged, off CD, affordable, not blocked).</summary>
        public bool CanCastSkill(PlayerState state, int slot) => WeaponReady(state, slot);

        /// <summary>True when `enemy` is visible to `viewerTeam` (not invisible from Tago!, or currently outline-revealed).</summary>
        public bool IsEnemyVisibleToTeam(PlayerState enemy, int viewerTeam)
        {
            if (enemy == null) return false;
            if (enemy.Team == viewerTeam) return true;
            if (enemy.InvisibleLeft <= 0.0) return true;
            return enemy.RevealedOutlineLeft > 0.0;
        }

        /// <summary>True when `trap` is visible to `viewerTeam`: allies always see it; enemies see it within 3m or when revealed.</summary>
        public bool IsTrapVisibleToTeam(WeaponSystem.HiddenTrap trap, int viewerTeam)
        {
            if (trap == null) return false;
            if (trap.Team == viewerTeam) return true;
            if (trap.RevealedToEnemy) return true;
            double distLimit = Rules.TrapRevealDistance > 0.0 ? Rules.TrapRevealDistance : 3.0;
            foreach (var pair in Players)
            {
                PlayerState p = pair.Value;
                if (p.Team == viewerTeam && !p.Tagged && p.Position.DistanceTo(trap.Position) <= distLimit)
                    return true;
            }
            return false;
        }

        static void ClearTransientSpecState(PlayerState state)
        {
            state.TiradorChargeTime = -1.0;
            state.SyatoTargetId = -1;
            state.SyatoRecastLeft = 0.0;
            state.EmpoweredTiradorBonus = 0.0;
            state.EmpoweredTiradorLeft = 0.0;
            state.BlurLeft = 0.0;
            state.RevealedOutlineLeft = 0.0;
            state.RevealAlertLeft = 0.0;
            state.KnownRevealAlert = false;
            state.InvisibleLeft = 0.0;
            state.StiltsLeft = 0.0;
            state.SackRaceLeft = 0.0;
            state.LuksongBakaLeft = 0.0;
            state.TrumpoSpinLeft = 0.0;
            state.TrumpoSpinTickTimer = 0.0;
            state.SungkaAbsorbLeft = 0.0;
            state.SungkaStoredDamage = 0;
            state.ShieldHp = 0.0;
            state.ShieldHpLeft = 0.0;
            state.LangitLeft = 0.0;
            state.TayaCountLeft = 0.0;
            state.SpeedBoostRatio = 0.0;
            state.SpeedBoostLeft = 0.0;
            state.PendingSlowAfterRootLeft = 0.0;
            state.PendingSlowAfterRootDuration = 0.0;
            state.PendingSlowAfterRootMagnitude = 0.0;
            state.VaultDashActive = false;
            state.LeapLeft = 0.0;
            state.LeapDuration = 0.0;
            state.LeapSkillId = "";
            state.BatoWindupLeft = 0.0;
            state.SackBumpMask = 0;
            state.LuksongBakaUsedMask = 0;
        }

        /// <summary>Team 0 plays from the own (+Z) end, team 1 from the enemy (-Z) end.</summary>
        public int SideForTeam(int team) => team == 0 ? MapLayout.SideOwn : MapLayout.SideEnemy;

        /// <summary>The end whose jail (MapLayout.RescueRect) holds `team`'s tagged kids and where `team` rescues them: beside the
        /// enemy base under `RescueAtEnemyJail` (docs/SPEC.md §5), else beside its own base.</summary>
        public int JailSideFor(int team) => Rules.SpecRules ? -SideForTeam(team) : SideForTeam(team);

        /// <summary>Player `id` cast something during the current tick.</summary>
        public bool CastThisTick(int id) => _castTick.TryGetValue(id, out int tick) && tick == Tick;

        /// <summary>First to `count` points (one of `Rules.FirstToChoices`; anything else is ignored).</summary>
        public void SetFirstTo(int count)
        {
            if (Rules.FirstToChoices.Contains(count)) Score = new MatchScore(count);
        }

        /// <summary>Plays a street game instead of Agawang Base and puts everyone in their places. Call once all players are in.</summary>
        public void StartMode(IStreetMode mode)
        {
            Mode = mode;
            UnbindNeutrals();
            Neutrals = null;
            Powerups.Clear();
            Obstacles.Clear();
            LowCovers.Clear();
            LowCoverHeights.Clear();
            _walkObstacles.Clear();
            _blocking.Clear();
            _blocking.AddRange(_boundaryBlocking);
            _walkBlocking.Clear();
            _walkBlocking.AddRange(_boundaryBlocking);
            Score.FirstTo = 0;
            Mode.Setup(this);
        }

        /// <summary>Client-side prediction: runs only the movement part of a tick for one player.</summary>
        public void PredictMove(int id, PlayerInput input, double dt)
        {
            PlayerState state = Players.GetOrDefault(id);
            if (state == null || state.Tagged || Phase != MatchPhase.Playing || (Mode != null && Mode.IsResetting())) return;
            Vec2 before = state.Position;
            Move(state, input, dt);
            if (Mode == null) KeepBehindBarrier(state, before);
        }

        public PlayerState AddPlayer(int id, int team)
        {
            int slot = _teamCounts.TryGetValue(team, out int had) ? had : 0;
            _teamCounts[team] = slot + 1;
            int side = SideForTeam(team);
            var state = new PlayerState { Id = id, Team = team };
            state.SpawnPosition = SpawnPosition(side, slot);
            state.Position = state.SpawnPosition;
            state.Facing = new Vec2(0.0, -side);
            state.Hp = Rules.PlayerMaxHp;
            state.Radius = Rules.PlayerRadius;
            state.Effects.ImmunityLength = Rules.CcImmunity;
            state.Elixir = Rules.ElixirStart;
            state.Stamina = Rules.StaminaMax;
            state.Water = Rules.WaterMax;
            state.TiradorAmmo = Rules.TiradorMaxAmmo;
            Players[id] = state;
            return state;
        }

        /// <summary>A random loadout with at least one weapon that hurts.</summary>
        public List<string> FightingLoadout()
        {
            List<string> kit = RandomLoadout();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                bool hasDamage = false;
                foreach (string wid in kit)
                {
                    if (WeaponDefs.TryGetValue(wid, out WeaponDef def) && def.Damage > 0)
                    {
                        hasDamage = true;
                        break;
                    }
                }
                if (hasDamage) break;
                kit = RandomLoadout();
            }
            return kit;
        }

        /// <summary>True when `id` is driven by a bot that fights back.</summary>
        public bool IsFighter(int id) => _brains.TryGetValue(id, out DummyBrain brain) && brain.IsFighter;

        public bool SetLoadout(int id, string first, string second, string third = "")
        {
            var chosen = new List<string> { first, second };
            if (third != "") chosen.Add(third);
            return SetLoadoutList(id, chosen);
        }

        public bool SetLoadoutList(int id, IList<string> chosen)
        {
            if (!Players.ContainsKey(id) || chosen.Count < 2 || chosen.Count > PlayerInput.CardBits.Length) return false;
            for (int i = 0; i < chosen.Count; i++)
            {
                string wid = chosen[i];
                if (wid == "" || !Rules.Weapons.Exists(w => w.Id == wid)) return false;
                for (int j = i + 1; j < chosen.Count; j++)
                    if (chosen[j] == wid) return false;
            }
            PlayerState state = Players[id];
            state.UsingSpecKit = false;
            state.Weapons = new List<string>(chosen);
            SizeSlots(state);
            return true;
        }

        void SizeSlots(PlayerState state)
        {
            state.AimHold.Clear();
            state.ResizeSlots();
        }

        /// <summary>Cards mode: shuffles every weapon into the player's deck with the match's seeded rng.</summary>
        public bool DealCards(int id)
        {
            int hand = GMath.Clampi(Rules.HandSize, 1, PlayerInput.CardBits.Length);
            if (!Players.ContainsKey(id) || WeaponDefs.Count < hand + 2) return false;
            PlayerState state = Players[id];
            var ids = new List<string>();
            foreach (WeaponDef def in Rules.Weapons) ids.Add(def.Id);
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Rng.RandiRange(0, i);
                string held = ids[i];
                ids[i] = ids[j];
                ids[j] = held;
            }
            state.UsingSpecKit = false;
            state.Cards = true;
            state.Weapons = ids.GetRange(0, Math.Min(hand, ids.Count));
            state.NextCard = hand < ids.Count ? ids[hand] : "";
            state.Deck = hand + 1 < ids.Count ? ids.GetRange(hand + 1, ids.Count - hand - 1) : new List<string>();
            state.Incoming.Clear();
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            SizeSlots(state);
            return true;
        }

        /// <summary>True when `state` pays for its cards with elixir; otherwise cards play their cooldown.</summary>
        public bool PaysElixir(PlayerState state) => state.Cards && Rules.UsesElixir();

        /// <summary>Equips a player: spec kit when enabled and character has skills, else cards or random legacy weapons.</summary>
        public void Equip(int id, bool fighter = false)
        {
            if (Rules.CardCycle)
            {
                DealCards(id);
                return;
            }
            if (Rules.UsesSpecKits() && Players.TryGetValue(id, out PlayerState p) && HasSpecKit(p.CharacterId))
            {
                EquipSpecKit(id);
                return;
            }
            List<string> kit = fighter ? FightingLoadout() : RandomLoadout();
            SetLoadoutList(id, kit);
        }

        /// <summary>A card was used from `slot`: it cycles through the deck (elixir) or the shuffler (cooldowns).</summary>
        public void CycleUsed(PlayerState state, int slot)
        {
            if (!state.Cards) return;
            if (PaysElixir(state))
            {
                state.Deck.Add(state.Weapons[slot]);
                state.Weapons[slot] = state.NextCard;
                state.NextCard = PopFront(state.Deck);
                EmitCardDrawn(state.Id, slot);
                return;
            }
            state.Incoming.Add(state.Weapons[slot]);
            state.Weapons[slot] = state.NextCard;
            state.NextCard = "";
            state.WeaponCooldowns[slot] = 0.0;
            if (state.Weapons[slot] != "") EmitCardDrawn(state.Id, slot);
            TickShuffler(state, 0.0);
        }

        static string PopFront(List<string> list)
        {
            if (list.Count == 0) return "";
            string first = list[0];
            list.RemoveAt(0);
            return first;
        }

        void TickShuffler(PlayerState state, double dt)
        {
            if (!state.Cards) return;
            if (state.ShuffleCard == "" && state.Incoming.Count > 0)
            {
                state.ShuffleCard = PopFront(state.Incoming);
                state.ShuffleTotal = WeaponDefs[state.ShuffleCard].Cooldown;
                state.ShuffleLeft = state.ShuffleTotal;
            }
            if (state.ShuffleCard == "") return;
            state.ShuffleLeft -= dt;
            if (state.ShuffleLeft > 0.0) return;
            state.Deck.Add(state.ShuffleCard);
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            PlaceNext(state);
        }

        void PlaceNext(PlayerState state)
        {
            if (state.Deck.Count == 0) return;
            for (int slot = 0; slot < state.Weapons.Count; slot++)
            {
                if (state.Weapons[slot] == "")
                {
                    state.Weapons[slot] = PopFront(state.Deck);
                    EmitCardDrawn(state.Id, slot);
                    return;
                }
            }
            if (state.NextCard == "")
            {
                state.NextCard = PopFront(state.Deck);
                EmitCardDrawn(state.Id, -1);
            }
        }

        /// <summary>A new round: refills water/ammo, resets S1/S2 (keeping ULT CD across rounds under spec kits), and finishes card reloads.</summary>
        public void FinishReloads(PlayerState state)
        {
            if (state.UsingSpecKit)
            {
                // docs/SPEC.md §5 & docs/KITS.md: Water, S1 and S2 refill on round start; ULT cooldown carries across rounds.
                if (state.WeaponCooldowns.Count > 0) state.WeaponCooldowns[0] = 0.0;
                if (state.WeaponCooldowns.Count > 1) state.WeaponCooldowns[1] = 0.0;
                state.Water = Rules.WaterMax;
                state.TiradorAmmo = Rules.TiradorMaxAmmo;
                state.TiradorReloadLeft = 0.0;
                state.TiradorChargeTime = -1.0;
                SkillDef s1 = GetPlayerSkill(state, 0);
                state.S1Charges = s1 != null ? GMath.Maxi(1, s1.MaxCharges) : 1;
                state.S1ChargeRegenLeft = 0.0;
                state.PassiveHealCarry = 0.0;
                state.TsinelasMarkedLeft = 0.0;
                state.TsinelasMarkedBy = -1;
                ClearTransientSpecState(state);
                return;
            }
            for (int slot = 0; slot < state.WeaponCooldowns.Count; slot++) state.WeaponCooldowns[slot] = 0.0;
            if (!state.Cards) return;
            if (state.ShuffleCard != "") state.Deck.Add(state.ShuffleCard);
            state.Deck.AddRange(state.Incoming);
            state.Incoming.Clear();
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            int attempts = state.Weapons.Count + 1;
            for (int attempt = 0; attempt < attempts; attempt++) PlaceNext(state);
        }

        /// <summary>Swap: only while tagged. A weapon you keep keeps its cooldown.</summary>
        public bool SwapLoadout(int id, string first, string second, string third = "")
        {
            var chosen = new List<string> { first, second };
            if (third != "") chosen.Add(third);
            return SwapLoadoutList(id, chosen);
        }

        public bool SwapLoadoutList(int id, IList<string> chosen)
        {
            if (Rules.CardCycle || !Players.ContainsKey(id) || !Players[id].Tagged) return false;
            PlayerState state = Players[id];
            var kept = new Dictionary<string, double>();
            for (int slot = 0; slot < state.Weapons.Count; slot++) kept[state.Weapons[slot]] = state.WeaponCooldowns[slot];
            if (!SetLoadoutList(id, chosen)) return false;
            for (int slot = 0; slot < state.Weapons.Count; slot++)
                state.WeaponCooldowns[slot] = kept.TryGetValue(state.Weapons[slot], out double cd) ? cd : 0.0;
            return true;
        }

        /// <summary>Distinct random weapons (`LoadoutSize` slots).</summary>
        public List<string> RandomLoadout()
        {
            var ids = new List<string>();
            foreach (WeaponDef def in Rules.Weapons) ids.Add(def.Id);
            int count = GMath.Clampi(Rules.LoadoutSize, 2, GMath.Mini(PlayerInput.CardBits.Length, ids.Count));
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Rng.RandiRange(0, i);
                string swap = ids[i];
                ids[i] = ids[j];
                ids[j] = swap;
            }
            return ids.GetRange(0, Math.Min(count, ids.Count));
        }

        /// <summary>Sets a player's character; a teammate who held it gets the previous one back. False for an unknown id or player.</summary>
        public bool SetCharacter(int id, string characterId)
        {
            if (!Players.ContainsKey(id) || !Rules.Characters.Exists(c => c.Id == characterId)) return false;
            string previous = Players[id].CharacterId;
            foreach (var pair in Players)
                if (pair.Key != id && pair.Value.Team == Players[id].Team && pair.Value.CharacterId == characterId) pair.Value.CharacterId = previous;
            Players[id].CharacterId = characterId;
            return true;
        }

        /// <summary>The kids a teammate of `id` has locked in (they confirmed), which `id` cannot take.</summary>
        public List<string> TeammatesKids(int id, IEnumerable<int> confirmed)
        {
            var kids = new List<string>();
            foreach (int other in confirmed)
                if (other != id && Players.ContainsKey(other) && Players.ContainsKey(id) && Players[other].Team == Players[id].Team)
                    kids.Add(Players[other].CharacterId);
            return kids;
        }

        /// <summary>Gives every player a random character, with no duplicates on the same team (seeded).</summary>
        public void AssignCharacters()
        {
            var baseIds = new List<string>();
            foreach (CharacterDef character in Rules.Characters) baseIds.Add(character.Id);
            if (baseIds.Count == 0) return;
            var pools = new Dictionary<int, List<string>>();
            var cursors = new Dictionary<int, int>();
            foreach (var pair in Players)
            {
                int t = pair.Value.Team;
                if (!pools.ContainsKey(t))
                {
                    var pool = new List<string>(baseIds);
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = Rng.RandiRange(0, i);
                        string swap = pool[i];
                        pool[i] = pool[j];
                        pool[j] = swap;
                    }
                    pools[t] = pool;
                    cursors[t] = 0;
                }
                List<string> poolT = pools[t];
                int idx = cursors[t];
                pair.Value.CharacterId = poolT[idx % poolT.Count];
                cursors[t] = idx + 1;
            }
        }

        /// <summary>Players that can be hit (everyone not waiting on the sidewalk).</summary>
        public bool IsTargetable(PlayerState state) => !state.Tagged;

        public List<PlayerState> EnemiesOf(int team)
        {
            var result = new List<PlayerState>();
            foreach (var pair in Players)
                if (pair.Value.Team != team && IsTargetable(pair.Value)) result.Add(pair.Value);
            return result;
        }

        public PlayerState NearestEnemy(PlayerState caster, double maxRange)
        {
            PlayerState best = null;
            double bestDistance = maxRange;
            foreach (PlayerState target in EnemiesOf(caster.Team))
            {
                if (Rules.UsesSpecKits() && !IsEnemyVisibleToTeam(target, caster.Team)) continue;
                double distance = caster.Position.DistanceTo(target.Position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>Status effect from a weapon or skill.</summary>
        public void ApplyEffect(int id, StatusEffects.Type type, double duration, double magnitude)
        {
            ApplyEffectAndReport(id, type, duration, magnitude);
        }

        /// <summary>Status effect from a weapon or skill; returns true when applied.</summary>
        public bool ApplyEffectAndReport(int id, StatusEffects.Type type, double duration, double magnitude)
        {
            if (!Players.ContainsKey(id) || !IsTargetable(Players[id])) return false;
            PlayerState state = Players[id];
            // Jumong S2 Trumpo Ikot is immune to Slow & Slip.
            if (state.TrumpoSpinLeft > 0.0 && (type == StatusEffects.Type.Slow || type == StatusEffects.Type.Slip))
                return false;
            bool fresh = type != StatusEffects.Type.None && !state.Effects.Has(type);
            bool applied = state.Effects.Apply(type, duration, magnitude);
            if (applied && (state.Effects.IsHardCc(type) || type == StatusEffects.Type.Root)) EndMove(state);
            if (fresh && applied) EffectApplied?.Invoke(id, type);
            return applied;
        }

        /// <summary>A training dummy (or bot): a normal player on `team` at `position`, driven by `brain`. Dummies don't count for captures.</summary>
        public PlayerState AddDummy(int id, int team, Vec2 position, DummyBrain brain)
        {
            PlayerState state = AddPlayer(id, team);
            state.Position = position;
            state.SpawnPosition = position;
            state.HasStamina = false;
            state.Trips = false;
            _brains[id] = brain;
            return state;
        }

        public void SetInput(int id, PlayerInput input) => _inputs[id] = input;

        /// <summary>Applies damage (`by` is who dealt it, -1 for nobody). At 0 HP the player is tagged.</summary>
        public void Damage(int id, int amount, int by = -1) =>
            DamageCore(id, amount, by, "", false, false, true);

        /// <summary>Applies Tirador or skill damage with headshot/tag HitConfirmEvent and no-solo-100-HP-skill-kill enforcement.</summary>
        public void DamageCombat(int id, int amount, int by, string weaponId, bool isHeadshot, bool isSkill) =>
            DamageCore(id, amount, by, weaponId ?? "", isHeadshot, isSkill, true);

        void DamageCore(int id, int amount, int by, string weaponId, bool isHeadshot, bool isSkill, bool allowRedirect)
        {
            if (!Players.ContainsKey(id) || amount <= 0) return;
            PlayerState state = Players[id];
            if (state.Tagged || Mode != null || state.ShieldLeft > 0.0) return;

            int finalAmount = amount;
            if (by >= 0 && Players.ContainsKey(by))
            {
                PlayerState src = Players[by];
                if (src.BuffType == PowerupTeks && src.BuffLeft > 0.0)
                    finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * (1.0 + Rules.TeksDamageBonus)));
                if (Neutrals != null)
                {
                    double dmgMult = Neutrals.GetDamageMultiplier(src.Team, src.CharacterId);
                    if (dmgMult > 1.0 + Epsilon)
                        finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * dmgMult));
                }
            }
            if (state.BuffType == PowerupTuron && state.BuffLeft > 0.0)
                finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * (1.0 - Rules.TuronDamageReduction)));
            if (Neutrals != null)
            {
                double takenMult = Neutrals.GetDamageTakenMultiplier(state.Team, state.CharacterId);
                if (takenMult < 1.0 - Epsilon)
                    finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * takenMult));
            }

            // docs/KITS.md: No single skill tags a full-HP kid alone.
            if (isSkill && state.Hp >= Rules.PlayerMaxHp && finalAmount >= Rules.PlayerMaxHp)
                finalAmount = GMath.Maxi(1, Rules.PlayerMaxHp - 1);

            // Paeng S1 Sack Race: 40% less damage taken.
            if (state.SackRaceLeft > 0.0)
                finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * 0.60));

            // Oriang S1 Chinese Garter: 30% of tethered ally damage redirects to Oriang.
            if (allowRedirect)
            {
                WeaponSystem.AllyTether tether = Weapons.TetherForAlly(id);
                if (tether != null && tether.OwnerId != id && Players.TryGetValue(tether.OwnerId, out PlayerState oriang) && !oriang.Tagged)
                {
                    int redirected = GMath.Maxi(1, GMath.Roundi(finalAmount * tether.RedirectRatio));
                    finalAmount = GMath.Maxi(0, finalAmount - redirected);
                    if (redirected > 0) DamageCore(oriang.Id, redirected, by, weaponId, false, isSkill, false);
                }
            }

            // Oriang S2 Sungka: store damage taken as shells during the 3s absorb window.
            if (state.SungkaAbsorbLeft > 0.0 && finalAmount > 0)
            {
                SkillDef s2 = GetPlayerSkill(state, 1);
                int maxShells = s2 != null && s2.WallHp > 0 ? s2.WallHp : 50;
                state.SungkaStoredDamage = GMath.Mini(maxShells, state.SungkaStoredDamage + finalAmount);
            }

            // Shared Sungka ShieldHp absorbs damage before real HP.
            int hpDamage = finalAmount;
            if (state.ShieldHp > 0.0 && hpDamage > 0)
            {
                double absorbed = GMath.Minf(state.ShieldHp, hpDamage);
                state.ShieldHp = GMath.Maxf(0.0, state.ShieldHp - absorbed);
                hpDamage = GMath.Maxi(0, hpDamage - GMath.Roundi(absorbed));
            }

            int nextHp = GMath.Maxi(0, state.Hp - hpDamage);
            // Oriang ULT Langit!: allies in 8m cannot drop below 1 HP (cannot be tagged).
            if (state.LangitLeft > 0.0 && nextHp < 1) nextHp = 1;

            Vec2 hitPos = state.Position;
            state.Hp = nextHp;
            state.OutOfCombatTime = 0.0;
            state.Sprinting = false;
            state.UltCharge = GMath.Minf(100.0, state.UltCharge + finalAmount * Rules.UltPerDamage * 0.45);
            if (by >= 0 && by != id && Players.ContainsKey(by))
            {
                PlayerState attacker = Players[by];
                if (attacker.Team != state.Team)
                    attacker.UltCharge = GMath.Minf(100.0, attacker.UltCharge + finalAmount * Rules.UltPerDamage);
            }
            PlayerDamaged?.Invoke(id, finalAmount);

            bool isTag = state.Hp <= 0;
            if (isTag) Objectives.Tag(this, state, by);

            if (by >= 0 && Players.TryGetValue(by, out PlayerState hitter) && (Rules.UsesSpecKits() || !string.IsNullOrEmpty(weaponId)))
            {
                var ev = new HitConfirmEvent
                {
                    Tick = Tick,
                    AttackerId = by,
                    TargetId = id,
                    WeaponId = weaponId,
                    Damage = finalAmount,
                    IsHeadshot = isHeadshot,
                    IsTag = isTag,
                    WorldPos = hitPos,
                };
                hitter.LastHitEvent = ev;
                HitConfirmed?.Invoke(ev);
            }
        }

        /// <summary>Heals up to max HP; returns the HP actually gained. Nothing while tagged.</summary>
        public int Heal(int id, int amount)
        {
            PlayerState state = Players.GetOrDefault(id);
            if (state == null || amount <= 0 || state.Tagged) return 0;
            int gained = GMath.Mini(amount, Rules.PlayerMaxHp - state.Hp);
            if (gained <= 0) return 0;
            state.Hp += gained;
            PlayerHealed?.Invoke(id, gained);
            return gained;
        }

        /// <summary>Tags a player straight away (scripted or out-of-play).</summary>
        public void Kill(int id)
        {
            if (Players.ContainsKey(id) && !Players[id].Tagged)
            {
                Players[id].Hp = 0;
                Objectives.Tag(this, Players[id]);
            }
        }

        /// <summary>How long the opening show lasts (a street game with a roll adds the roll's time).</summary>
        public double IntroTotal() => Rules.IntroTime + (Mode != null ? Mode.Rules.RollSeconds : 0.0);

        /// <summary>Starts the opening show. Call once, before the first step.</summary>
        public void BeginIntro()
        {
            Phase = MatchPhase.Intro;
            IntroLeft = IntroTotal();
        }

        /// <summary>Everyone on `team` is gone: the match ends now and the other team wins.</summary>
        public void Forfeit(int team)
        {
            if (Phase == MatchPhase.MatchOver) return;
            Score.Winner = 1 - team;
            foreach (var pair in Players) EndMove(pair.Value);
            Phase = MatchPhase.MatchOver;
            MatchWon?.Invoke(Score.Winner);
        }

        public void Step(double dt)
        {
            if (Phase == MatchPhase.MatchOver)
            {
                Tick += 1;
                return;
            }
            if (Phase == MatchPhase.Intro)
            {
                IntroLeft -= dt;
                if (IntroLeft <= 0.0)
                {
                    IntroLeft = 0.0;
                    Phase = MatchPhase.Playing;
                    IntroFinished?.Invoke();
                }
                Tick += 1;
                return;
            }
            if (Phase == MatchPhase.PointFreeze)
            {
                FreezeLeft -= dt;
                if (FreezeLeft <= 0.0) Objectives.EndFreeze(this);
                Tick += 1;
                return;
            }
            foreach (var pair in _brains)
                if (!Players[pair.Key].Tagged) _inputs[pair.Key] = pair.Value.Think(Players[pair.Key], this);
            foreach (var pair in Players)
            {
                PlayerState state = pair.Value;
                PlayerInput input = _inputs.GetOrDefault(pair.Key);
                if (state.Tagged) StepTagged(state, input, dt);
                else StepFree(state, input, dt);
            }
            MatchTime += dt;
            RoundTime += dt;
            BarrierLeft = GMath.Maxf(0.0, BarrierLeft - dt);
            CaptureNeeded = CurrentBaseCaptureTime();
            Weapons.Step(this, dt);
            if (Mode != null)
            {
                Mode.Step(this, dt);
            }
            else
            {
                StepPowerups(dt);
                if (Neutrals != null) StepNeutrals(dt);
                Objectives.CheckRescues(this, dt);
                StepCapturesWithPalosebo(dt);
            }
            Tick += 1;
        }

        void StepNeutrals(double dt)
        {
            Neutrals.Step(dt, Players, _neutralsRng);
            StepNeutralBossProjectiles(Neutrals.Dragon);
            StepNeutralBossProjectiles(Neutrals.Baron);
        }

        void StepNeutralBossProjectiles(NeutralBossInstance boss)
        {
            if (boss == null || !boss.CanBeDamaged) return;
            List<WeaponSystem.Projectile> projectiles = Weapons.Projectiles;
            if (projectiles.Count == 0) return;

            int syntheticHitId = -100 - (int)boss.Kind;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                WeaponSystem.Projectile pr = projectiles[i];
                int dmg = pr.DamageOverride > 0
                    ? pr.DamageOverride
                    : (pr.Skill != null ? pr.Skill.Damage : (pr.Def != null ? pr.Def.Damage : 0));
                if (dmg <= 0) continue;

                double reach = boss.HitRadius + pr.HitRadius;
                if (pr.Position.DistanceSquaredTo(boss.Position) > reach * reach) continue;

                bool pierces = (pr.Skill != null && pr.Skill.Mechanic == SkillMechanic.TsinelasBagsik)
                    || (pr.Def != null && (pr.Def.Shape == WeaponDef.WeaponShape.Boomerang || pr.Def.Shape == WeaponDef.WeaponShape.Spinner));
                if (pierces && !pr.HitIds.Add(syntheticHitId)) continue;

                bool damaged = Neutrals.DamageNeutral(boss.Kind, dmg, pr.OwnerId, pr.Team, isDamageSkillOrStone: true, rng: _neutralsRng);
                if (!damaged) continue;

                bool defeated = boss.State == NeutralBossState.Fleeing;
                if (pr.OwnerId >= 0 && Players.TryGetValue(pr.OwnerId, out PlayerState hitter))
                {
                    var ev = new HitConfirmEvent
                    {
                        Tick = Tick,
                        AttackerId = pr.OwnerId,
                        TargetId = -(int)boss.Kind - 1,
                        WeaponId = pr.EffectId ?? "",
                        Damage = dmg,
                        IsHeadshot = false,
                        IsTag = defeated,
                        WorldPos = boss.Position,
                    };
                    hitter.LastHitEvent = ev;
                    HitConfirmed?.Invoke(ev);
                }

                if (!pierces)
                    projectiles.RemoveAt(i);
                if (!boss.CanBeDamaged) break;
            }
        }

        void StepCapturesWithPalosebo(double dt)
        {
            // Jopay ULT Palosebo: greased pole in defending base halts enemy capture progress (team wipes still score).
            bool wipe0 = Objectives.AllEnemiesTagged(this, 0, _brains);
            bool wipe1 = Objectives.AllEnemiesTagged(this, 1, _brains);
            if (!wipe0 && !wipe1)
            {
                int cap0 = Objectives.CapturersInEnemyBase(this, 0, _brains);
                int cap1 = Objectives.CapturersInEnemyBase(this, 1, _brains);
                if (cap0 > 0 && cap1 == 0 && Weapons.HasActivePalosebo(1))
                {
                    PauseCaptureForPalosebo(0);
                    return;
                }
                if (cap1 > 0 && cap0 == 0 && Weapons.HasActivePalosebo(0))
                {
                    PauseCaptureForPalosebo(1);
                    return;
                }
            }
            Objectives.CheckCaptures(this, _brains, dt);
        }

        void PauseCaptureForPalosebo(int capturingTeam)
        {
            if (CaptureTeam != capturingTeam || CurrentCaptureKind != CaptureKind.Base)
            {
                CaptureTeam = capturingTeam;
                CurrentCaptureKind = CaptureKind.Base;
                CaptureElapsed = 0.0;
                CaptureNeeded = CurrentBaseCaptureTime();
            }
            CaptureRate = 0.0;
        }

        void TickCooldowns(PlayerState state, double dt)
        {
            double cdDt = dt;
            if (Neutrals != null && state.UsingSpecKit)
            {
                double cdMult = Neutrals.GetCooldownMultiplier(state.Team, state.CharacterId);
                if (cdMult > Epsilon && cdMult < 1.0 - Epsilon)
                    cdDt = dt / cdMult;
            }
            for (int slot = 0; slot < state.WeaponCooldowns.Count; slot++)
                state.WeaponCooldowns[slot] = GMath.Maxf(0.0, state.WeaponCooldowns[slot] - cdDt);
            state.PrimaryCd = GMath.Maxf(0.0, state.PrimaryCd - dt);
            state.DashCd = GMath.Maxf(0.0, state.DashCd - dt);
            state.JumpCd = GMath.Maxf(0.0, state.JumpCd - dt);
            state.SlideCd = GMath.Maxf(0.0, state.SlideCd - dt);
            state.ShieldLeft = GMath.Maxf(0.0, state.ShieldLeft - dt);
            TickBuff(state, dt);
            if (Mode == null && !state.Tagged) state.UltCharge = GMath.Minf(100.0, state.UltCharge + Rules.UltPassivePerS * dt);
            TickShuffler(state, dt);
            if (Rules.UsesSpecKits()) TickSpecWaterAndCharges(state, dt);
        }

        void TickSpecWaterAndCharges(PlayerState state, double dt)
        {
            // Water mana: 2/s passive in street, 25/s in own home base fountain, plus Selecta team regen bonus when active.
            state.InHomeFountain = !state.Tagged && Mode == null && Layout.InBase(SideForTeam(state.Team), state.Position);
            double rate = state.InHomeFountain ? Rules.WaterFountainRegenPerS : Rules.WaterRegenPerS;
            if (Neutrals != null && Mode == null)
                rate += Neutrals.GetTeamWaterRegenBonus(state.Team);
            state.Water = GMath.Minf(Rules.WaterMax, state.Water + rate * dt);

            // Multi-charge S1 regeneration (Narda Luksong Tinik 2 charges).
            if (state.UsingSpecKit && state.Weapons.Count > 0)
            {
                SkillDef s1 = GetPlayerSkill(state, 0);
                if (s1 != null && s1.MaxCharges > 1 && state.S1Charges < s1.MaxCharges)
                {
                    state.S1ChargeRegenLeft = GMath.Maxf(0.0, state.S1ChargeRegenLeft - dt);
                    if (state.S1ChargeRegenLeft <= Epsilon)
                    {
                        state.S1Charges = GMath.Mini(s1.MaxCharges, state.S1Charges + 1);
                        state.S1ChargeRegenLeft = state.S1Charges < s1.MaxCharges ? s1.Cooldown : 0.0;
                        if (state.S1Charges > 0 && state.WeaponCooldowns.Count > 0 && state.WeaponCooldowns[0] > 0.25)
                            state.WeaponCooldowns[0] = 0.0;
                    }
                    else if (state.S1Charges == 0 && state.WeaponCooldowns.Count > 0)
                    {
                        state.WeaponCooldowns[0] = state.S1ChargeRegenLeft;
                    }
                }
            }

            // Tirador reload timer ticks even while walking/in jail.
            if (state.TiradorReloadLeft > 0.0)
            {
                state.TiradorReloadLeft = GMath.Maxf(0.0, state.TiradorReloadLeft - dt);
                if (state.TiradorReloadLeft <= Epsilon)
                {
                    state.TiradorReloadLeft = 0.0;
                    state.TiradorAmmo = Rules.TiradorMaxAmmo;
                }
            }
        }

        void StepFree(PlayerState state, PlayerInput input, double dt)
        {
            state.Effects.Step(dt);
            if (Mode != null && Mode.IsResetting()) return;
            TickCooldowns(state, dt);
            TickElixir(state, dt);
            if (Rules.UsesSpecKits()) StepSpecState(state, dt);
            if (Mode != null)
            {
                Mode.HandleInput(this, state, input, dt);
            }
            else if (input != null)
            {
                StepHeroActions(state, input, dt);
                HandleWeaponCasting(state, input, dt);
            }
            Vec2 before = state.Position;
            Move(state, input, dt);
            if (Mode != null) Mode.Constrain(this, state);
            else KeepBehindBarrier(state, before);
            bool moved = state.Position.DistanceSquaredTo(before) > Epsilon * Epsilon;
            TickStamina(state, moved, dt);
            TickTrip(state, moved, dt);
        }

        void StepSpecState(PlayerState state, double dt)
        {
            // Deferred slow after Tere ULT Garter Net root ends.
            if (state.PendingSlowAfterRootLeft > 0.0)
            {
                state.PendingSlowAfterRootLeft = GMath.Maxf(0.0, state.PendingSlowAfterRootLeft - dt);
                if (state.PendingSlowAfterRootLeft <= Epsilon && !state.Effects.Has(StatusEffects.Type.Root))
                {
                    ApplyEffect(state.Id, StatusEffects.Type.Slow, state.PendingSlowAfterRootDuration, state.PendingSlowAfterRootMagnitude);
                    state.PendingSlowAfterRootLeft = 0.0;
                }
            }

            // Timers: Syato recast, Empowered Tirador, Blur, Reveals, Invisibility, Stilts, Speed boost, Shield, Langit, Taya, Tsinelas mark.
            if (state.SyatoRecastLeft > 0.0)
            {
                state.SyatoRecastLeft = GMath.Maxf(0.0, state.SyatoRecastLeft - dt);
                PlayerState marked = Players.GetOrDefault(state.SyatoTargetId);
                if (state.SyatoRecastLeft <= Epsilon || marked == null || marked.Tagged)
                {
                    state.SyatoRecastLeft = 0.0;
                    state.SyatoTargetId = -1;
                }
            }
            if (state.EmpoweredTiradorLeft > 0.0)
            {
                state.EmpoweredTiradorLeft = GMath.Maxf(0.0, state.EmpoweredTiradorLeft - dt);
                if (state.EmpoweredTiradorLeft <= Epsilon)
                {
                    state.EmpoweredTiradorLeft = 0.0;
                    state.EmpoweredTiradorBonus = 0.0;
                }
            }
            state.BlurLeft = GMath.Maxf(0.0, state.BlurLeft - dt);
            state.RevealedOutlineLeft = GMath.Maxf(0.0, state.RevealedOutlineLeft - dt);
            if (state.RevealAlertLeft > 0.0)
            {
                state.RevealAlertLeft = GMath.Maxf(0.0, state.RevealAlertLeft - dt);
                if (state.RevealAlertLeft <= Epsilon)
                {
                    state.RevealAlertLeft = 0.0;
                    state.KnownRevealAlert = false;
                }
            }
            if (state.InvisibleLeft > 0.0)
            {
                state.InvisibleLeft = GMath.Maxf(0.0, state.InvisibleLeft - dt);
                // docs/SPEC.md §6: starting a rescue or standing in enemy base ends hiding / Tago!
                if (RescueRescuer[state.Team] == state.Id || Layout.InBase(-SideForTeam(state.Team), state.Position))
                    state.InvisibleLeft = 0.0;
            }
            state.StiltsLeft = GMath.Maxf(0.0, state.StiltsLeft - dt);
            state.LangitLeft = GMath.Maxf(0.0, state.LangitLeft - dt);
            state.TayaCountLeft = GMath.Maxf(0.0, state.TayaCountLeft - dt);
            if (state.SpeedBoostLeft > 0.0)
            {
                state.SpeedBoostLeft = GMath.Maxf(0.0, state.SpeedBoostLeft - dt);
                if (state.SpeedBoostLeft <= Epsilon)
                {
                    state.SpeedBoostLeft = 0.0;
                    state.SpeedBoostRatio = 0.0;
                }
            }
            if (state.ShieldHpLeft > 0.0)
            {
                state.ShieldHpLeft = GMath.Maxf(0.0, state.ShieldHpLeft - dt);
                if (state.ShieldHpLeft <= Epsilon)
                {
                    state.ShieldHpLeft = 0.0;
                    state.ShieldHp = 0.0;
                }
            }
            if (state.TsinelasMarkedLeft > 0.0)
            {
                state.TsinelasMarkedLeft = GMath.Maxf(0.0, state.TsinelasMarkedLeft - dt);
                if (state.TsinelasMarkedLeft <= Epsilon)
                {
                    state.TsinelasMarkedLeft = 0.0;
                    state.TsinelasMarkedBy = -1;
                }
            }

            // Jumong S2 Trumpo Ikot: periodic spin damage around Jumong.
            if (state.TrumpoSpinLeft > 0.0)
            {
                state.TrumpoSpinLeft = GMath.Maxf(0.0, state.TrumpoSpinLeft - dt);
                state.TrumpoSpinTickTimer -= dt;
                if (state.TrumpoSpinTickTimer <= 0.0)
                {
                    SkillDef s2 = GetPlayerSkill(state, 1);
                    double interval = s2 != null && s2.Interval > 0.0 ? s2.Interval : 0.5;
                    double radius = s2 != null && s2.Radius > 0.0 ? s2.Radius : 3.2;
                    int dmg = s2 != null && s2.Damage > 0 ? s2.Damage : 6;
                    string wid = s2 != null ? s2.Id : "trumpo_ikot";
                    state.TrumpoSpinTickTimer += interval;
                    foreach (PlayerState enemy in Weapons.EnemiesWithin(this, state.Team, state.Position, radius))
                    {
                        EmitWeaponHit(enemy.Id, wid);
                        DamageCombat(enemy.Id, dmg, state.Id, wid, false, true);
                    }
                }
            }

            // Jumong ULT Pukpok Palayok: parabolic leap & smash + 2s Blur.
            if (state.LeapLeft > 0.0)
            {
                state.LeapLeft = GMath.Maxf(0.0, state.LeapLeft - dt);
                double t = state.LeapDuration > 0.0 ? GMath.Clampf(1.0 - state.LeapLeft / state.LeapDuration, 0.0, 1.0) : 1.0;
                state.Position = state.LeapStart.Lerp(state.LeapTarget, t);
                state.AirY = 4.0 * 2.5 * t * (1.0 - t);
                if (state.LeapLeft <= Epsilon)
                {
                    state.LeapLeft = 0.0;
                    state.AirY = 0.0;
                    state.Position = Weapons.ResolveWallPushOut(ClampMoveTarget(state.LeapTarget, state.Radius), state.Radius);
                    SkillDef ult = GetSkillDef(state.LeapSkillId) ?? GetPlayerSkill(state, 2);
                    double radius = ult != null && ult.Radius > 0.0 ? ult.Radius : 4.5;
                    int dmg = ult != null && ult.Damage > 0 ? ult.Damage : 40;
                    double blurDur = ult != null && ult.BlurDuration > 0.0 ? ult.BlurDuration : 2.0;
                    string wid = ult != null ? ult.Id : "pukpok_palayok";
                    foreach (PlayerState enemy in Weapons.EnemiesWithin(this, state.Team, state.Position, radius))
                    {
                        EmitWeaponHit(enemy.Id, wid);
                        enemy.BlurLeft = GMath.Maxf(enemy.BlurLeft, blurDur);
                        DamageCombat(enemy.Id, dmg, state.Id, wid, false, true);
                    }
                    state.LeapSkillId = "";
                }
            }

            // Inggo ULT Bato!: 1s windup then fires missing-HP execute stone.
            if (state.BatoWindupLeft > 0.0)
            {
                state.BatoWindupLeft = GMath.Maxf(0.0, state.BatoWindupLeft - dt);
                if (state.BatoWindupLeft <= Epsilon)
                {
                    state.BatoWindupLeft = 0.0;
                    SkillDef ult = GetPlayerSkill(state, 2);
                    if (ult != null && ult.Mechanic == SkillMechanic.BatoExecute)
                        Weapons.LaunchBatoExecute(state, ult, state.BatoWindupAim);
                }
            }

            // Oriang S2 Sungka: release stored damage as shared ally shield when 3s absorb ends.
            if (state.SungkaAbsorbLeft > 0.0)
            {
                state.SungkaAbsorbLeft = GMath.Maxf(0.0, state.SungkaAbsorbLeft - dt);
                if (state.SungkaAbsorbLeft <= Epsilon)
                {
                    state.SungkaAbsorbLeft = 0.0;
                    int stored = state.SungkaStoredDamage;
                    state.SungkaStoredDamage = 0;
                    if (stored > 0)
                    {
                        SkillDef s2 = GetPlayerSkill(state, 1);
                        double radius = s2 != null && s2.Radius > 0.0 ? s2.Radius : 7.0;
                        double shieldDur = s2 != null && s2.SecondaryDuration > 0.0 ? s2.SecondaryDuration : 4.0;
                        foreach (var pair in Players)
                        {
                            PlayerState ally = pair.Value;
                            if (ally.Team == state.Team && !ally.Tagged && state.Position.DistanceTo(ally.Position) <= radius + ally.Radius)
                            {
                                ally.ShieldHp = GMath.Maxf(ally.ShieldHp, stored);
                                ally.ShieldHpLeft = GMath.Maxf(ally.ShieldHpLeft, shieldDur);
                            }
                        }
                    }
                }
            }

            // Paeng S1 Sack Race: bump knockback against enemies.
            if (state.SackRaceLeft > 0.0)
            {
                state.SackRaceLeft = GMath.Maxf(0.0, state.SackRaceLeft - dt);
                SkillDef s1 = GetPlayerSkill(state, 0);
                double kb = s1 != null && s1.Knockback > 0.0 ? s1.Knockback : 3.5;
                int dmg = s1 != null && s1.Damage > 0 ? s1.Damage : 18;
                string wid = s1 != null ? s1.Id : "sack_race";
                foreach (PlayerState enemy in EnemiesOf(state.Team))
                {
                    int bit = 1 << (enemy.Id & 31);
                    if ((state.SackBumpMask & bit) != 0) continue;
                    if (state.Position.DistanceTo(enemy.Position) <= state.Radius + enemy.Radius + 0.35)
                    {
                        state.SackBumpMask |= bit;
                        Vec2 pushDir = state.Position.DistanceSquaredTo(enemy.Position) > 0.001
                            ? state.Position.DirectionTo(enemy.Position)
                            : state.Facing;
                        MoveBy(enemy, pushDir * kb);
                        EmitWeaponHit(enemy.Id, wid);
                        DamageCombat(enemy.Id, dmg, state.Id, wid, false, true);
                    }
                }
                if (state.SackRaceLeft <= Epsilon) { state.SackRaceLeft = 0.0; state.SackBumpMask = 0; }
            }

            // Paeng S2 Luksong Baka: ally-only 10m vault leap off crouching Paeng.
            if (state.LuksongBakaLeft > 0.0)
            {
                state.LuksongBakaLeft = GMath.Maxf(0.0, state.LuksongBakaLeft - dt);
                SkillDef s2 = GetPlayerSkill(state, 1);
                double triggerR = s2 != null && s2.Radius > 0.0 ? s2.Radius : 2.0;
                double leapDist = s2 != null && s2.MaxRange > 0.0 ? s2.MaxRange : 10.0;
                double dashSpeed = Rules.DashSpeed > 0.0 ? Rules.DashSpeed : 18.5;
                foreach (var pair in Players)
                {
                    PlayerState ally = pair.Value;
                    if (ally.Id == state.Id || ally.Team != state.Team || ally.Tagged) continue;
                    int bit = 1 << (ally.Id & 31);
                    if ((state.LuksongBakaUsedMask & bit) != 0) continue;
                    if (ally.Position.DistanceTo(state.Position) <= triggerR + ally.Radius)
                    {
                        state.LuksongBakaUsedMask |= bit;
                        Vec2 vaultDir = ally.Facing.LengthSquared() > 0.001
                            ? ally.Facing.Normalized()
                            : (state.Position.DistanceSquaredTo(ally.Position) > 0.001 ? state.Position.DirectionTo(ally.Position) : new Vec2(0.0, -SideForTeam(ally.Team)));
                        ally.DashDir = vaultDir;
                        ally.Facing = vaultDir;
                        ally.DashLeft = leapDist / dashSpeed;
                        ally.VaultDashActive = true;
                        ally.VelY = GMath.Maxf(ally.VelY, 5.2);
                        ally.Crouching = false;
                        EmitPlayerDashed(ally.Id);
                    }
                }
                if (state.LuksongBakaLeft <= Epsilon) { state.LuksongBakaLeft = 0.0; state.LuksongBakaUsedMask = 0; }
            }

            // docs/SPEC.md §7 passive healing: 5 HP/s after 5 s without damage, attacking, or CC (spec kit kids only so legacy cooldown tests stay exact).
            if (state.UsingSpecKit && Mode == null && Rules.PassiveHealPerS > 0.0 && state.Hp < Rules.PlayerMaxHp
                && state.OutOfCombatTime >= Rules.PassiveHealDelay && !state.Effects.HasAny())
            {
                state.PassiveHealCarry += Rules.PassiveHealPerS * dt;
                int whole = (int)Math.Floor(state.PassiveHealCarry + Epsilon);
                if (whole > 0)
                {
                    state.PassiveHealCarry = GMath.Maxf(0.0, state.PassiveHealCarry - whole);
                    Heal(state.Id, whole);
                }
            }
            else
            {
                state.PassiveHealCarry = 0.0;
            }
        }

        int HeldSlot(PlayerState state)
        {
            for (int slot = 0; slot < state.AimHold.Count; slot++)
                if (state.AimHold[slot] >= 0.0) return slot;
            return -1;
        }

        /// <summary>Weapon slot aiming/release; a release that casts nothing keeps the slot's cooldown at 0 (cooldown mode).</summary>
        void HandleWeaponCasting(PlayerState state, PlayerInput input, double dt)
        {
            int releasedSlot = HeldSlot(state);
            bool wasReady = releasedSlot >= 0 && releasedSlot < state.WeaponCooldowns.Count && state.WeaponCooldowns[releasedSlot] <= 0.0;
            bool didRelease = wasReady && (state.PreviousButtons & WeaponCasting.CardBits[releasedSlot]) != 0
                && !input.IsPressed(WeaponCasting.CardBits[releasedSlot]);
            int beforeSerial = _castSerial;
            Casting.Handle(this, state, input, dt);
            if (didRelease && !Rules.CardCycle && !state.UsingSpecKit && _castSerial == beforeSerial) state.WeaponCooldowns[releasedSlot] = 0.0;
        }

        void StepHeroActions(PlayerState state, PlayerInput input, double dt)
        {
            bool morphed = Rules.UsesSpecKits() && state.Effects.Has(StatusEffects.Type.Polymorph);
            bool slipping = state.Effects.IsSlipping();

            Vec2 stickMove = input.Move.LimitLength(1.0);
            double stickMag = stickMove.Length();
            bool hasTargetRun = stickMag <= 0.001 && state.HasMoveTarget && state.Position.DistanceSquaredTo(state.MoveTarget) > 0.04;

            // One jump button (docs/SPEC.md §7): moving into a low cover vaults it, anything else is a jump. Blocked while morphed
            // into a can, slipping (Holens Spill / Palosebo), on stilts or mid luksong baka.
            bool jumpFresh = input.IsPressed(PlayerInput.BtnJump) && (state.PreviousButtons & PlayerInput.BtnJump) == 0;
            if (jumpFresh && !morphed && !slipping && state.StiltsLeft <= 0.0 && state.LuksongBakaLeft <= 0.0 && state.AirY <= 0.01 && state.JumpCd <= 0.0 && state.DashLeft <= 0.0 && state.VaultLeft <= 0.0
                && state.SlideLeft <= 0.0 && state.CastLockLeft <= 0.0 && state.Effects.CanMove())
            {
                Vec2 moveDir = stickMag > 0.001 ? stickMove : hasTargetRun ? state.MoveTarget - state.Position : Vec2.Zero;
                if (TryStartVault(state, moveDir))
                {
                    state.JumpCd = Rules.JumpCooldown;
                    state.Crouching = false;
                }
                else if (Rules.JumpVelocity > 0.0)
                {
                    state.VelY = Rules.JumpVelocity;
                    state.JumpCd = Rules.JumpCooldown;
                    state.Crouching = false;
                }
            }
            bool runningFast = (stickMag >= Rules.WalkRunThreshold || hasTargetRun) && !state.Overclocked;
            bool slideFresh = input.IsPressed(PlayerInput.BtnSlide) && (state.PreviousButtons & PlayerInput.BtnSlide) == 0;
            bool crouchFresh = input.IsPressed(PlayerInput.BtnCrouch) && (state.PreviousButtons & PlayerInput.BtnCrouch) == 0;
            if ((slideFresh || (crouchFresh && !state.Crouching)) && !morphed && state.StiltsLeft <= 0.0 && state.LuksongBakaLeft <= 0.0
                && runningFast && state.AirY <= 0.01
                && state.SlideCd <= 0.0 && state.SlideLeft <= 0.0 && state.DashLeft <= 0.0
                && state.CastLockLeft <= 0.0 && state.Effects.CanMove() && Rules.SlideDuration > 0.0)
            {
                Vec2 sDir;
                if (stickMag > 0.001) sDir = stickMove.Normalized();
                else if (hasTargetRun) sDir = (state.MoveTarget - state.Position).Normalized();
                else sDir = state.Facing.LengthSquared() > 0.001 ? state.Facing : new Vec2(0.0, -SideForTeam(state.Team));
                state.SlideDir = sDir;
                state.Facing = sDir;
                state.SlideLeft = Rules.SlideDuration;
                state.SlideCd = Rules.SlideCooldown;
                state.Crouching = true;
                state.Walking = false;
                state.RunTime = 0.0;
            }
            else if (crouchFresh && !runningFast && state.SlideLeft > 0.0)
            {
                state.SlideLeft = 0.0;
            }

            // 1. Dash (fresh press; blocked when morphed into a can)
            bool dashFresh = input.IsPressed(PlayerInput.BtnDash) && (state.PreviousButtons & PlayerInput.BtnDash) == 0;
            if (dashFresh && !morphed && state.DashCd <= 0.0 && state.DashLeft <= 0.0 && state.VaultLeft <= 0.0 && state.Effects.CanMove() && Rules.DashDuration > 0.0)
            {
                Vec2 dir;
                if (input.Move.LengthSquared() > 0.001) dir = input.Move.Normalized();
                else if (state.HasMoveTarget && state.Position.DistanceSquaredTo(state.MoveTarget) > 0.04) dir = (state.MoveTarget - state.Position).Normalized();
                else if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite() && state.Position.DistanceSquaredTo(input.AimPoint) > 0.04)
                    dir = (input.AimPoint - state.Position).Normalized();
                else dir = state.Facing.LengthSquared() > 0.001 ? state.Facing : new Vec2(0.0, -SideForTeam(state.Team));
                state.DashDir = dir;
                state.Facing = dir;
                state.DashLeft = Rules.DashDuration;
                state.DashCd = Rules.DashCooldown;
                state.VaultDashActive = false;
                state.SlideLeft = 0.0;
                state.Crouching = false;
                state.Effects.ClearType(StatusEffects.Type.Slow);
                PlayerDashed?.Invoke(state.Id);
            }

            // 2. Legacy Ultimate (when NOT using spec kits; under spec kits WeaponCasting routes BtnUltimate to slot 2!)
            if (!state.UsingSpecKit)
            {
                bool ultFresh = input.IsPressed(PlayerInput.BtnUltimate) && (state.PreviousButtons & PlayerInput.BtnUltimate) == 0;
                if (ultFresh && Rules.UltimateWeapon != null && state.UltCharge >= 100.0 - Epsilon && state.CanAct() && !CastingBlocked(state))
                {
                    WeaponDef ultDef = Rules.UltimateWeapon;
                    Vec2 ultAim = Vec2.Zero;
                    if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite())
                    {
                        ultAim = AimFromPoint(state, ultDef, input.AimPoint);
                    }
                    else if (input.Aim.IsFinite() && input.Aim.Length() >= Rules.AimDeadzone)
                    {
                        ultAim = input.Aim.LimitLength(1.0);
                    }
                    else
                    {
                        PlayerState near = NearestEnemy(state, ultDef.MaxRange);
                        if (near != null && ultDef.MaxRange > 0.0) ultAim = ((near.Position - state.Position) / ultDef.MaxRange).LimitLength(1.0);
                    }
                    if (Weapons.Fire(this, state, ultDef, ultAim, 0.0, -1))
                    {
                        state.UltCharge = 0.0;
                        state.OutOfCombatTime = 0.0;
                        state.Sprinting = false;
                        if (ultDef.Heal > 0) Heal(state.Id, ultDef.Heal);
                        UltimateCast?.Invoke(state.Id);
                        EmitWeaponCast(state.Id, ultDef.Id);
                    }
                }
            }

            // 3. Primary fire (Tirador): Spec 7-stone magazine + hold-to-charge 18m..30m + reload OR legacy instant fire
            if (state.UsingSpecKit)
            {
                StepSpecTirador(state, input, dt);
            }
            else if (input.IsPressed(PlayerInput.BtnPrimary) && Rules.PrimaryWeapon != null && state.PrimaryCd <= 0.0
                && state.CanAct() && !CastingBlocked(state) && HeldSlot(state) < 0)
            {
                WeaponDef pDef = Rules.PrimaryWeapon;
                Vec2 pAim;
                if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite()) pAim = AimFromPoint(state, pDef, input.AimPoint);
                else if (input.Aim.IsFinite() && input.Aim.Length() >= Rules.AimDeadzone) pAim = input.Aim.LimitLength(1.0);
                else pAim = Weapons.AutoAim(this, state, pDef);
                if (Weapons.Fire(this, state, pDef, pAim, 0.0, -1))
                {
                    if (pAim.LengthSquared() > 0.001) state.Facing = pAim.Normalized();
                    state.PrimaryCd = pDef.Cooldown;
                    state.OutOfCombatTime = 0.0;
                    state.Sprinting = false;
                    EmitWeaponCast(state.Id, pDef.Id);
                }
            }
        }

        void StepSpecTirador(PlayerState state, PlayerInput input, double dt)
        {
            // Manual reload (BtnReload) when magazine is not full and not already reloading.
            bool reloadFresh = input.IsPressed(PlayerInput.BtnReload) && (state.PreviousButtons & PlayerInput.BtnReload) == 0;
            if (reloadFresh && state.TiradorReloadLeft <= 0.0 && state.TiradorAmmo < Rules.TiradorMaxAmmo && state.CanAct())
            {
                state.TiradorReloadLeft = Rules.TiradorReloadSeconds;
                state.TiradorChargeTime = -1.0;
                EmitTiradorReloadStarted(state.Id);
                return;
            }

            // Auto-reload if magazine hit 0 and reload hasn't started yet.
            if (state.TiradorAmmo <= 0 && state.TiradorReloadLeft <= 0.0)
            {
                state.TiradorReloadLeft = Rules.TiradorReloadSeconds;
                state.TiradorChargeTime = -1.0;
                EmitTiradorReloadStarted(state.Id);
                return;
            }

            if (state.TiradorReloadLeft > 0.0 || !state.CanAct() || CastingBlocked(state) || HeldSlot(state) >= 0)
            {
                state.TiradorChargeTime = -1.0;
                return;
            }

            bool primaryHeld = input.IsPressed(PlayerInput.BtnPrimary);
            if (primaryHeld)
            {
                if (state.TiradorChargeTime < 0.0)
                {
                    if (state.PrimaryCd <= 0.0 && state.TiradorAmmo > 0) state.TiradorChargeTime = 0.0;
                }
                else
                {
                    state.TiradorChargeTime = GMath.Minf(Rules.TiradorChargeSeconds, state.TiradorChargeTime + dt);
                }
                return;
            }

            // Released after charging (1-tick tap = charge 0.0 -> 18 m; full 1s hold -> 30 m; range ONLY, never extra dmg!).
            if (state.TiradorChargeTime >= 0.0)
            {
                double held = state.TiradorChargeTime;
                state.TiradorChargeTime = -1.0;
                if (input.IsPressed(PlayerInput.BtnAimCancel) || state.TiradorAmmo <= 0 || state.PrimaryCd > 0.0) return;

                double charge01 = Rules.TiradorChargeSeconds > 0.0 ? GMath.Clampf(held / Rules.TiradorChargeSeconds, 0.0, 1.0) : 0.0;
                double range = GMath.Lerpf(Rules.TiradorMinRange, Rules.TiradorMaxRange, charge01);
                if (state.StiltsLeft > 0.0)
                {
                    SkillDef s2 = GetPlayerSkill(state, 1);
                    double rangeBonus = s2 != null && s2.BonusRatio > 0.0 ? s2.BonusRatio : 0.40;
                    range *= 1.0 + rangeBonus;
                }

                int dmg = Rules.TiradorBodyDamage;
                if (state.EmpoweredTiradorLeft > 0.0 && state.EmpoweredTiradorBonus > 0.0)
                {
                    dmg = GMath.Roundi(dmg * (1.0 + state.EmpoweredTiradorBonus));
                    state.EmpoweredTiradorLeft = 0.0;
                    state.EmpoweredTiradorBonus = 0.0;
                }

                WeaponDef pDef = Rules.PrimaryWeapon ?? new WeaponDef { Id = "tirador_bato", Speed = Rules.TiradorProjectileSpeed, ProjectileRadius = 0.24 };
                Vec2 pAim;
                if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite()) pAim = AimFromPoint(state, pDef, input.AimPoint);
                else if (input.Aim.IsFinite() && input.Aim.Length() >= Rules.AimDeadzone) pAim = input.Aim.LimitLength(1.0);
                else pAim = AutoAimTirador(state, range);

                Vec2 dir = pAim.LengthSquared() > 0.0001 ? pAim.Normalized() : state.Facing;
                bool aimedDown = input.IsPressed(PlayerInput.BtnAds) || state.AimingDown;
                Weapons.LaunchTirador(state, pDef, dir, range, dmg, aimedDown);
                state.Facing = dir;
                state.InvisibleLeft = 0.0; // Attacking breaks Kikay Tago! stealth.
                state.TiradorAmmo = GMath.Maxi(0, state.TiradorAmmo - 1);
                state.PrimaryCd = Rules.TiradorFireCooldown;
                state.OutOfCombatTime = 0.0;
                state.JogTime = 0.0;
                state.Sprinting = false;
                EmitWeaponCast(state.Id, pDef.Id);
                if (state.TiradorAmmo <= 0)
                {
                    state.TiradorReloadLeft = Rules.TiradorReloadSeconds;
                    EmitTiradorReloadStarted(state.Id);
                }
            }
        }

        Vec2 AutoAimTirador(PlayerState caster, double range)
        {
            PlayerState target = NearestEnemy(caster, range);
            if (target != null && target.Position != caster.Position)
                return caster.Position.DirectionTo(target.Position);
            return caster.Facing;
        }

        /// <summary>Each `TripInterval` s of running without stopping is one roll at `TripChance` (the trip rng).</summary>
        void TickTrip(PlayerState state, bool moved, double dt)
        {
            if (!state.Trips || Rules.TripChance <= 0.0 || Rules.TripInterval <= 0.0) return;
            if (!moved || state.Walking || state.Crouching || state.SlideLeft > 0.0 || state.DashLeft > 0.0
                || state.Overclocked || !state.Effects.CanMove())
            {
                state.RunTime = 0.0;
                return;
            }
            if (state.AirY > 0.0) return;
            state.RunTime += dt;
            if (state.RunTime < Rules.TripInterval - Epsilon) return;
            state.RunTime = 0.0;
            if (_tripRng.Randf() < Rules.TripChance) ApplyEffect(state.Id, StatusEffects.Type.Tripped, Rules.TripSeconds, 0.0);
        }

        /// <summary>Spawns or resets the merienda power-up stands (circular map, no street mode) and live BarangayNeutrals.</summary>
        public void ResetPowerups()
        {
            Powerups.Clear();
            if (Neutrals != null && Mode == null)
                Neutrals.ResetRound();
            if (Layout == null || !Layout.IsCircular() || Mode != null) return;
            var spots = new List<Vec2>(Layout.PowerupSpots);
            if (spots.Count == 0) spots.AddRange(DefaultPowerupSpots);
            for (int i = 0; i < spots.Count; i++)
                Powerups.Add(new Powerup { Id = i + 1, Kind = (i % 3) + 1, Pos = spots[i], Active = true, RespawnLeft = 0.0 });
        }

        void StepPowerups(double dt)
        {
            if (Powerups.Count == 0 || Mode != null) return;
            foreach (Powerup entry in Powerups)
            {
                if (!entry.Active)
                {
                    double rem = GMath.Maxf(0.0, entry.RespawnLeft - dt);
                    entry.RespawnLeft = rem;
                    if (rem <= Epsilon)
                    {
                        entry.RespawnLeft = 0.0;
                        entry.Active = true;
                    }
                    continue;
                }
                PlayerState winner = null;
                double bestDist = double.PositiveInfinity;
                foreach (var pair in Players)
                {
                    if (_brains.ContainsKey(pair.Key)) continue;
                    PlayerState candidate = pair.Value;
                    if (candidate.Tagged) continue;
                    double reach = Rules.PowerupPickupRadius + candidate.Radius;
                    double dist = candidate.Position.DistanceTo(entry.Pos);
                    if (dist <= reach && dist < bestDist)
                    {
                        bestDist = dist;
                        winner = candidate;
                    }
                }
                if (winner == null) continue;
                entry.Active = false;
                entry.RespawnLeft = Rules.PowerupRespawnSeconds;
                ApplyPowerup(winner, entry.Kind);
                PowerupTaken?.Invoke(winner.Id, entry.Id, entry.Kind);
            }
        }

        void ApplyPowerup(PlayerState state, int kind)
        {
            state.BuffRegenCarry = 0.0;
            switch (kind)
            {
                case PowerupPalamig:
                    Heal(state.Id, Rules.PalamigHeal);
                    state.BuffType = PowerupPalamig;
                    state.BuffLeft = Rules.PalamigDuration;
                    break;
                case PowerupTeks:
                    if (Mode == null) state.UltCharge = GMath.Minf(100.0, state.UltCharge + Rules.TeksUltBonus);
                    state.BuffType = PowerupTeks;
                    state.BuffLeft = Rules.TeksDuration;
                    break;
                case PowerupTuron:
                    state.BuffType = PowerupTuron;
                    state.BuffLeft = Rules.TuronDuration;
                    break;
            }
        }

        void TickBuff(PlayerState state, double dt)
        {
            if (state.BuffLeft <= 0.0 || state.BuffType == PowerupNone)
            {
                state.BuffLeft = 0.0;
                state.BuffType = PowerupNone;
                state.BuffRegenCarry = 0.0;
                return;
            }
            double stepDt = GMath.Minf(dt, state.BuffLeft);
            state.BuffLeft = GMath.Maxf(0.0, state.BuffLeft - dt);
            if (!state.Tagged && state.BuffType == PowerupTuron && Rules.TuronRegenPerS > 0.0)
            {
                if (state.Hp < Rules.PlayerMaxHp)
                {
                    state.BuffRegenCarry += Rules.TuronRegenPerS * stepDt;
                    int wholeHp = (int)Math.Floor(state.BuffRegenCarry + Epsilon);
                    if (wholeHp > 0)
                    {
                        state.BuffRegenCarry = GMath.Maxf(0.0, state.BuffRegenCarry - wholeHp);
                        Heal(state.Id, wholeHp);
                    }
                }
                else
                {
                    state.BuffRegenCarry = 0.0;
                }
            }
            if (state.BuffLeft <= Epsilon)
            {
                state.BuffLeft = 0.0;
                state.BuffType = PowerupNone;
                state.BuffRegenCarry = 0.0;
            }
        }

        /// <summary>Extra height the vault arc clears above the cover.</summary>
        public const double VaultClearance = 0.25;
        /// <summary>How far past the far side of the cover a vault lands.</summary>
        const double VaultLandingGap = 0.05;

        /// <summary>
        /// Starts a vault when a low cover no higher than <c>vault_max_height</c> lies within <c>vault_reach</c> ahead along
        /// <paramref name="moveDir"/> and the kid fits on its far side. Returns false (a plain jump) otherwise.
        /// </summary>
        bool TryStartVault(PlayerState state, Vec2 moveDir)
        {
            if (Rules.VaultDuration <= 0.0 || Rules.VaultMaxHeight <= 0.0 || moveDir.LengthSquared() < 1e-6) return false;
            Vec2 dir = moveDir.Normalized();
            int best = -1;
            double bestEnter = double.PositiveInfinity, bestExit = 0.0;
            for (int i = 0; i < LowCovers.Count; i++)
            {
                if (LowCoverHeights[i] > Rules.VaultMaxHeight + Epsilon) continue;
                if (!RayEntersRect(state.Position, dir, LowCovers[i].Grow(state.Radius), out double enter, out double exit)) continue;
                if (enter > Rules.VaultReach || enter >= bestEnter) continue;
                best = i;
                bestEnter = enter;
                bestExit = exit;
            }
            if (best < 0) return false;
            Vec2 landing = state.Position + dir * (bestExit + VaultLandingGap);
            Vec2 settled = Layout.ClampToArena(Collision.Resolve(landing, state.Radius, _walkBlocking), state.Radius);
            if (settled.DistanceSquaredTo(landing) > 1e-6) return false; // no room on the far side
            state.VaultFrom = state.Position;
            state.VaultTo = landing;
            state.VaultHeight = LowCoverHeights[best];
            state.VaultLeft = Rules.VaultDuration;
            state.Facing = dir;
            state.SlideLeft = 0.0;
            return true;
        }

        /// <summary>Distances along a unit ray at which it enters and leaves <paramref name="rect"/> (enter may be ~0 when touching).</summary>
        static bool RayEntersRect(Vec2 origin, Vec2 dir, Rect2 rect, out double enter, out double exit)
        {
            enter = -Epsilon;
            exit = double.PositiveInfinity;
            Vec2 end = rect.End;
            for (int axis = 0; axis < 2; axis++)
            {
                double o = origin[axis], d = dir[axis], low = rect.Position[axis], high = end[axis];
                if (GMath.IsZeroApprox(d))
                {
                    if (o < low || o > high) return false;
                    continue;
                }
                double t1 = (low - o) / d, t2 = (high - o) / d;
                enter = GMath.Maxf(enter, GMath.Minf(t1, t2));
                exit = GMath.Minf(exit, GMath.Maxf(t1, t2));
                if (enter > exit) return false;
            }
            return exit > 0.0;
        }

        /// <summary>Carries a vaulting kid over the cover on an arc, ignoring walls (the landing was checked at the start).</summary>
        void StepVault(PlayerState state, double dt)
        {
            state.VaultLeft = GMath.Maxf(0.0, state.VaultLeft - dt);
            double f = Rules.VaultDuration > 0.0 ? 1.0 - state.VaultLeft / Rules.VaultDuration : 1.0;
            state.Position = state.VaultFrom.Lerp(state.VaultTo, f);
            state.AirY = state.VaultLeft > 0.0 ? (state.VaultHeight + VaultClearance) * 4.0 * f * (1.0 - f) : 0.0;
            state.VelY = 0.0;
            state.Walking = false;
            state.Crouching = false;
        }

        void StepAir(PlayerState state, double dt)
        {
            if (state.VaultLeft > 0.0) return; // the vault arc owns the height
            if (state.AirY > 0.0 || state.VelY > 0.0)
            {
                state.AirY += state.VelY * dt;
                state.VelY -= Rules.JumpGravity * dt;
                if (state.AirY <= 0.0)
                {
                    state.AirY = 0.0;
                    state.VelY = 0.0;
                }
            }
        }

        /// <summary>Elixir fills while the match runs (also while tagged), up to the bar.</summary>
        void TickElixir(PlayerState state, double dt)
        {
            if (PaysElixir(state)) state.Elixir = GMath.Minf(Rules.ElixirMax, state.Elixir + Rules.ElixirRegenPerS * dt);
        }

        /// <summary>Running drains stamina; standing still refills it after `StaminaRegenDelay`. At 0 the kid is overclocked.</summary>
        void TickStamina(PlayerState state, bool moved, double dt)
        {
            if (!Rules.StaminaEnabled || !state.HasStamina) return;
            if (state.Overclocked)
            {
                state.StillTime = 0.0;
                if (state.ExhaustLeft > 0.0)
                {
                    state.ExhaustLeft = state.ExhaustLeft - dt > Epsilon ? state.ExhaustLeft - dt : 0.0;
                    return;
                }
                if (Mode != null && !Mode.CanRest(state)) return;
                state.Stamina = GMath.Minf(Rules.StaminaMax, state.Stamina + Rules.StaminaRegenPerS * dt);
                if (state.Stamina >= Rules.StaminaMax - Epsilon)
                {
                    state.Stamina = Rules.StaminaMax;
                    state.Overclocked = false;
                }
                return;
            }
            if (moved)
            {
                state.StillTime = 0.0;
                state.Stamina = GMath.Maxf(0.0, state.Stamina - Rules.StaminaDrainPerS * dt);
                if (state.Stamina <= Epsilon)
                {
                    state.Stamina = 0.0;
                    state.Overclocked = true;
                    state.ExhaustLeft = Rules.ExhaustSeconds;
                    Exhausted?.Invoke(state.Id);
                }
                return;
            }
            if (Mode != null && !Mode.CanRest(state))
            {
                state.StillTime = 0.0;
                return;
            }
            double refill = GMath.Clampf(state.StillTime + dt - Rules.StaminaRegenDelay, 0.0, dt);
            state.StillTime += dt;
            state.Stamina = GMath.Minf(Rules.StaminaMax, state.Stamina + Rules.StaminaRegenPerS * refill);
        }

        /// <summary>Full stamina, not overclocked.</summary>
        public void RefillStamina(PlayerState state)
        {
            state.Stamina = Rules.StaminaMax;
            state.Overclocked = false;
            state.ExhaustLeft = 0.0;
            state.StillTime = 0.0;
        }

        /// <summary>Waiting on the sidewalk: no moving or skills, cooldowns keep running. Training dummies walk out on their own.</summary>
        void StepTagged(PlayerState state, PlayerInput input, double dt)
        {
            TickCooldowns(state, dt);
            TickElixir(state, dt);
            TickStamina(state, false, dt);
            state.TagTime += dt;
            if (input != null) state.PreviousButtons = input.Buttons;
            if (Mode == null) Objectives.StepRevenge(this, state, dt);
            if (!state.Tagged) return;
            if (_brains.ContainsKey(state.Id) && state.TagTime >= Rules.DummyReleaseTime) Objectives.Rescue(this, state, ReleasedAlone);
        }

        /// <summary>The round-start barrier: a kid that was inside its own base can't step out of it while `BarrierLeft` runs.</summary>
        void KeepBehindBarrier(PlayerState state, Vec2 before)
        {
            if (BarrierLeft <= 0.0) return;
            int side = SideForTeam(state.Team);
            if (Layout.InBase(side, before) && !Layout.InBase(side, state.Position)) state.Position = before;
        }

        /// <summary>The street point a move target really goes to: pulled into the street and out of cover (the server's own clamp).</summary>
        public Vec2 ClampMoveTarget(Vec2 point, double radius)
        {
            Rect2 street = _street.Grow(-radius);
            Vec2 streetEnd = street.End;
            Vec2 result = Layout.ClampToArena(point, radius);
            foreach (Rect2 rect in _walkObstacles)
            {
                Rect2 grown = rect.Grow(radius);
                if (!grown.HasPoint(result)) continue;
                Vec2 end = grown.End;
                Vec2[] exits =
                {
                    new Vec2(grown.Position.x, result.y), new Vec2(end.x, result.y),
                    new Vec2(result.x, grown.Position.y), new Vec2(result.x, end.y),
                };
                Vec2 best = result;
                double bestDistance = double.PositiveInfinity;
                foreach (Vec2 exit in exits)
                {
                    bool inside = Layout.ArenaRadius > 0.0
                        ? Layout.InArena(exit, GMath.Maxf(radius - Epsilon, 0.0))
                        : exit.x >= street.Position.x - Epsilon && exit.x <= streetEnd.x + Epsilon
                          && exit.y >= street.Position.y - Epsilon && exit.y <= streetEnd.y + Epsilon;
                    if (inside && exit.DistanceTo(result) < bestDistance)
                    {
                        bestDistance = exit.DistanceTo(result);
                        best = exit;
                    }
                }
                result = best;
            }
            return result;
        }

        /// <summary>True when the card or skill in `slot` can be paid for now (Water for spec S1/S2, 0 for ULT, Elixir for cards).</summary>
        public bool CanAfford(PlayerState state, int slot)
        {
            if (state.UsingSpecKit)
            {
                SkillDef skill = GetPlayerSkill(state, slot);
                if (skill == null) return false;
                if (skill.IsUltimate || skill.WaterCost <= 0) return true;
                if (slot == 0 && skill.Mechanic == SkillMechanic.Syato && state.SyatoRecastLeft > 0.0 && state.SyatoTargetId >= 0)
                    return true;
                return state.Water + Epsilon >= skill.WaterCost;
            }
            if (!PaysElixir(state)) return true;
            if (slot < 0 || slot >= state.Weapons.Count) return false;
            WeaponDefs.TryGetValue(state.Weapons[slot], out WeaponDef def);
            return def != null && state.Elixir + Epsilon >= def.ElixirCost;
        }

        /// <summary>Ends the kid's walk to its move target.</summary>
        public void EndMove(PlayerState state)
        {
            state.HasMoveTarget = false;
            state.StuckTime = 0.0;
        }

        /// <summary>A card aimed at street `point`, as the stick-style aim WeaponSystem.Fire reads.</summary>
        public Vec2 AimFromPoint(PlayerState state, WeaponDef def, Vec2 point)
        {
            Vec2 offset = point - state.Position;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Heal:
                    return offset.Length() <= Rules.SelfCastRadius ? Vec2.Zero : offset.Normalized();
                case WeaponDef.WeaponShape.GroundAoe:
                case WeaponDef.WeaponShape.Trap:
                case WeaponDef.WeaponShape.Field:
                    return def.MaxRange > 0.0 ? (offset / def.MaxRange).LimitLength(1.0) : Vec2.Zero;
            }
            if (offset.Length() <= Rules.AimNoDirectionRadius) return state.Facing;
            return offset.Normalized();
        }

        /// <summary>Maps an aimed street point to a stick-style vector for a Spec SkillDef.</summary>
        public Vec2 AimFromPointSkill(PlayerState state, SkillDef skill, Vec2 point)
        {
            Vec2 offset = point - state.Position;
            switch (skill.Mechanic)
            {
                case SkillMechanic.PukpokPalayok:
                case SkillMechanic.BahayBahayan:
                case SkillMechanic.PabitinHeal:
                case SkillMechanic.Palosebo:
                case SkillMechanic.ChineseGarter:
                case SkillMechanic.TinikWall:
                case SkillMechanic.TeksTripwire:
                case SkillMechanic.Saranggola:
                case SkillMechanic.Piko:
                case SkillMechanic.PogsSlam:
                case SkillMechanic.TrumpoBantay:
                case SkillMechanic.Jackstone:
                case SkillMechanic.LuksongLubid:
                case SkillMechanic.GarterNet:
                case SkillMechanic.PatinteroLines:
                case SkillMechanic.PabitinSnare:
                case SkillMechanic.SakoTrap:
                    return skill.MaxRange > 0.0 ? (offset / skill.MaxRange).LimitLength(1.0) : Vec2.Zero;
            }
            if (offset.Length() <= Rules.AimNoDirectionRadius) return state.Facing;
            return offset.Normalized();
        }

        /// <summary>The targetable enemy nearest street `point` among those within `def.MaxRange` of the kid (-1: none).</summary>
        public int TargetNearPoint(PlayerState state, WeaponDef def, Vec2 point)
        {
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            foreach (PlayerState enemy in EnemiesOf(state.Team))
            {
                if (state.Position.DistanceTo(enemy.Position) > def.MaxRange) continue;
                double distance = enemy.Position.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy.Id;
                }
            }
            return best;
        }

        public bool WeaponReady(PlayerState state, int slot)
        {
            if (Mode != null) return false;
            return Casting.WeaponReady(state, slot) && CanAfford(state, slot) && !CastingBlocked(state);
        }

        /// <summary>Where a free player cannot cast: in the enemy base, or in its own rescue area while a teammate waits.</summary>
        public bool CastingBlocked(PlayerState state) => !state.Tagged && CastingBlockedAt(state, state.Position);

        public bool CastingBlockedAt(PlayerState state, Vec2 position)
        {
            if (Mode != null) return false;
            if (BarrierLeft > 0.0) return true;
            if (Rules.NoCastInEnemyBase && Layout.InBase(-SideForTeam(state.Team), position)) return true;
            return Rules.NoCastInRescueArea && Objectives.TeammateWaiting(this, state.Team)
                && Layout.RescueRect(JailSideFor(state.Team)).Grow(state.Radius).HasPoint(position);
        }

        public Vec2 ResolvedAim(PlayerState state, int slot, Vec2 stick) => Casting.ResolvedAim(this, state, slot, stick);

        public int ResolvedTarget(PlayerState state, int slot) => Casting.ResolvedTarget(this, state, slot);

        /// <summary>True when releasing `slot` now would throw nothing (a TARGETED weapon with nobody in range).</summary>
        public bool LacksTarget(PlayerState state, int slot)
        {
            if (slot < 0 || slot >= state.Weapons.Count) return false;
            if (state.UsingSpecKit) return false;
            WeaponDefs.TryGetValue(state.Weapons[slot], out WeaponDef def);
            if (def == null || def.Shape != WeaponDef.WeaponShape.Targeted) return false;
            return Weapons.TargetInRange(this, state, def, ResolvedTarget(state, slot)) == null;
        }

        /// <summary>cos 60 degrees: a turn sharper than this drops a run back to a jog.</summary>
        const double SharpTurnCos = 0.5;

        void Move(PlayerState state, PlayerInput input, double dt)
        {
            if (input != null) TakeMoveOrder(state, input);
            state.AimingDown = Mode == null && input != null && input.IsPressed(PlayerInput.BtnAds);
            if (state.LeapLeft > 0.0) return; // Jumong Pukpok Palayok leap controls position directly.
            StepAir(state, dt);
            if (state.VaultLeft > 0.0)
            {
                StepVault(state, dt);
                return;
            }
            if (state.DashLeft > 0.0)
            {
                state.DashLeft = GMath.Maxf(0.0, state.DashLeft - dt);
                state.Walking = false;
                state.Crouching = false;
                if (state.Effects.CanMove())
                {
                    bool vault = state.VaultDashActive;
                    MoveByInternal(state, state.DashDir * Rules.DashSpeed * dt, vault);
                    if (state.DashLeft <= 0.0 && vault)
                    {
                        state.VaultDashActive = false;
                        state.Position = Weapons.ResolveWallPushOut(
                            Layout.ClampToArena(Collision.Resolve(state.Position, state.Radius, _walkBlocking), state.Radius), state.Radius);
                    }
                    return;
                }
                state.DashLeft = 0.0;
                state.VaultDashActive = false;
            }
            if (state.SlideLeft > 0.0)
            {
                state.SlideLeft = GMath.Maxf(0.0, state.SlideLeft - dt);
                state.Walking = false;
                state.Sprinting = false;
                if (state.Effects.CanMove() && state.AirY <= 0.01)
                {
                    state.Crouching = state.SlideLeft > 0.0 || (input != null && input.IsPressed(PlayerInput.BtnCrouch));
                    double slideV = Rules.SlideSpeed;
                    if (Mode == null && state.BuffType == PowerupPalamig && state.BuffLeft > 0.0) slideV *= 1.0 + Rules.PalamigSpeedBonus;
                    MoveBy(state, state.SlideDir * slideV * dt);
                    return;
                }
                state.SlideLeft = 0.0;
            }
            if (state.LuksongBakaLeft > 0.0)
            {
                state.Crouching = true;
                state.Sprinting = false;
                state.Walking = false;
                return;
            }
            if (state.CastLockLeft > 0.0)
            {
                state.CastLockLeft = GMath.Maxf(0.0, state.CastLockLeft - dt);
                state.Sprinting = false;
                state.Walking = false;
                state.Crouching = false;
                return;
            }
            if (!state.Effects.CanMove())
            {
                state.Sprinting = false;
                state.Walking = false;
                state.Crouching = false;
                return;
            }
            state.OutOfCombatTime += dt;
            Vec2 move = input != null ? input.Move.LimitLength(1.0) : Vec2.Zero;
            double stickLen = move.Length();
            // FPS controls: a player who sends a look faces it and strafes; bots and old clients face the way they move
            bool looking = input != null && input.Look.IsFinite() && input.Look.LengthSquared() > 0.0001;
            if (looking) state.Facing = input.Look.Normalized();
            state.Crouching = Mode == null && state.StiltsLeft <= 0.0 && state.AirY <= 0.01 && input != null && input.IsPressed(PlayerInput.BtnCrouch);
            state.Walking = !state.Crouching && stickLen > 0.0 && stickLen < Rules.WalkRunThreshold;
            double speed = Mode != null ? Mode.Rules.MoveSpeed : Rules.MoveSpeed;
            if (Mode != null) speed *= Mode.SpeedScale(state);
            if (state.Overclocked) speed *= Rules.OverclockSpeedShare;
            else if (state.Crouching) speed *= Rules.CrouchSpeedShare;
            if (state.AimingDown) speed *= Rules.AdsSpeedShare;
            if (Mode == null && state.BuffType == PowerupPalamig && state.BuffLeft > 0.0) speed *= 1.0 + Rules.PalamigSpeedBonus;
            if (state.StiltsLeft > 0.0) speed *= 0.80; // Inggo Tiyakad: -20% speed
            if (state.SackRaceLeft > 0.0) speed *= 1.15; // Paeng Sack Race: +15% hop speed
            if (state.SpeedBoostLeft > 0.0 && state.SpeedBoostRatio > 0.0) speed *= 1.0 + state.SpeedBoostRatio;
            if (Neutrals != null && Mode == null)
                speed *= Neutrals.GetPlayerMoveSpeedMultiplier(state.Id, state.Team, state.CharacterId, state.Position);
            speed *= state.Effects.SpeedMultiplier(Rules.PolymorphSpeedScale);
            bool wantsMove = stickLen > 0.0 || state.HasMoveTarget;
            bool canRun = wantsMove && !state.Walking && !state.Crouching && !state.AimingDown && Rules.SprintEnabled
                && Mode == null && !state.Overclocked && !state.Effects.Has(StatusEffects.Type.Slow)
                && !state.Effects.IsSlipping() && !(Rules.UsesSpecKits() && state.Effects.Has(StatusEffects.Type.Polymorph)) && state.StiltsLeft <= 0.0;
            if (Rules.SpecRules)
            {
                // walk, jog, run (docs/SPEC.md §7): a steady jog turns into a run; a sharp turn (over 60 degrees) drops it
                bool sharpTurn = stickLen > 0.0 && state.Facing.Dot(move / stickLen) < SharpTurnCos;
                state.JogTime = canRun && !sharpTurn ? state.JogTime + dt : 0.0;
                state.Sprinting = canRun && state.JogTime >= Rules.SprintDelay;
            }
            else
            {
                state.Sprinting = canRun && state.OutOfCombatTime >= Rules.SprintDelay;
            }
            if (state.Sprinting) speed *= Rules.SprintMultiplier;
            Vec2 prevPos = state.Position;
            if (stickLen > 0.0)
            {
                Vec2 way = move / stickLen;
                if (!looking) state.Facing = way;
                // spec: a partly pushed stick walks at walk_speed, a fully pushed one jogs at move_speed (not in proportion)
                if (Rules.SpecRules && Rules.WalkSpeed > 0.0 && Mode == null)
                    move = state.Walking ? way * (Rules.WalkSpeed / Rules.MoveSpeed) : way;
                MoveBy(state, move * speed * dt);
            }
            else if (state.HasMoveTarget)
            {
                WalkToTarget(state, speed * dt, dt);
            }
            if (state.Walking && state.Position.DistanceSquaredTo(prevPos) <= Epsilon * Epsilon) state.Walking = false;
        }

        /// <summary>A non-zero stick clears the move target; a new street point replaces it (pulled into the street and out of cover).</summary>
        void TakeMoveOrder(PlayerState state, PlayerInput input)
        {
            if (input.Move.LengthSquared() > 0.0)
            {
                EndMove(state);
                return;
            }
            if (!input.IsPressed(PlayerInput.BtnMoveTarget) || !input.MoveTarget.IsFinite()) return;
            Vec2 point = ClampMoveTarget(Mode == null ? input.MoveTarget : Mode.MovePoint(state, input.MoveTarget), state.Radius);
            if (state.HasMoveTarget && point == state.MoveTarget) return;
            state.MoveTarget = point;
            state.HasMoveTarget = true;
            state.StuckTime = 0.0;
        }

        /// <summary>One tick of walking toward the move target in a straight line (sliding along walls).</summary>
        void WalkToTarget(PlayerState state, double reach, double dt)
        {
            Vec2 offset = state.MoveTarget - state.Position;
            double distance = offset.Length();
            if (distance <= Rules.MoveArrivalRadius)
            {
                EndMove(state);
                return;
            }
            Vec2 direction = offset / distance;
            state.Facing = direction;
            double intended = GMath.Minf(reach, distance);
            Vec2 before = state.Position;
            MoveBy(state, direction * intended);
            if (state.Position.DistanceTo(state.MoveTarget) <= Rules.MoveArrivalRadius)
            {
                EndMove(state);
                return;
            }
            if ((state.Position - before).Dot(direction) < Rules.MoveStuckShare * intended)
            {
                state.StuckTime += dt;
                if (state.StuckTime + Epsilon >= Rules.MoveStuckTime) EndMove(state);
            }
            else
            {
                state.StuckTime = 0.0;
            }
        }

        /// <summary>Moves by `delta` in sub-steps no longer than the player's radius so fast moves never tunnel through an obstacle.</summary>
        public void MoveBy(PlayerState state, Vec2 delta) => MoveByInternal(state, delta, false);

        void MoveByInternal(PlayerState state, Vec2 delta, bool vaultOverLowCover)
        {
            double length = delta.Length();
            if (length <= 0.0) return;
            int count = GMath.Maxi(1, GMath.Ceili(length / state.Radius));
            Vec2 piece = delta / count;
            IReadOnlyList<Rect2> rects = vaultOverLowCover ? _boundaryBlocking : _walkBlocking;
            for (int i = 0; i < count; i++)
            {
                Vec2 next = Layout.ClampToArena(Collision.Resolve(state.Position + piece, state.Radius, rects), state.Radius);
                if (Weapons.Walls.Count > 0) next = Layout.ClampToArena(Weapons.ResolveWallPushOut(next, state.Radius), state.Radius);
                state.Position = next;
            }
        }

        /// <summary>Slot 0 next to the post, then alternating +x / -x by spawn_spacing.</summary>
        Vec2 SpawnPosition(int side, int slot)
        {
            Vec2 basePos = Layout.SpawnCenter(side);
            int offset = GMath.Ceili(slot / 2.0);
            int direction = slot % 2 == 1 ? 1 : -1;
            return basePos + new Vec2(direction * offset * Rules.SpawnSpacing, 0.0);
        }

        /// <summary>What stops `team`'s players and throws: the street edges and every obstacle.</summary>
        public IReadOnlyList<Rect2> BlockingRectsForTeam(int team) => _blocking;

        /// <summary>Index of the obstacle at `point`, or -1.</summary>
        public int ObstacleAtPoint(Vec2 point)
        {
            for (int index = 0; index < Obstacles.Count; index++)
                if (Obstacles[index].HasPoint(point)) return index;
            return -1;
        }

        public bool TeamInEnemyBase(int team) => Objectives.TeamInEnemyBase(this, team, _brains);

        public int CapturersInEnemyBase(int team) => Objectives.CapturersInEnemyBase(this, team, _brains);

        public bool AllEnemiesTagged(int team) => Objectives.AllEnemiesTagged(this, team, _brains);

        /// <summary>Solo base hold time, shortened only by configured overtime.</summary>
        public double CurrentBaseCaptureTime()
        {
            if (InOvertime()) return GMath.Minf(Rules.CaptureSoloTime, Rules.OvertimeCaptureTime);
            return Rules.CaptureSoloTime;
        }

        /// <summary>The stage of the round clock, from the round's time only (GDScript `clock_stage()`).</summary>
        public ClockStage GetClockStage()
        {
            if (Mode != null) return ClockStage.Regular;
            if (Rules.OvertimeAfter <= 0.0 || RoundTime < Rules.OvertimeAfter) return ClockStage.Regular;
            if (RoundTime < Rules.OvertimeAfter + Rules.ExtendedSeconds) return ClockStage.Extended;
            return ClockStage.Overtime;
        }

        /// <summary>The round time at which `stage` begins.</summary>
        public double ClockStageStart(ClockStage stage)
        {
            switch (stage)
            {
                case ClockStage.Extended: return Rules.OvertimeAfter;
                case ClockStage.Overtime: return Rules.OvertimeAfter + Rules.ExtendedSeconds;
            }
            return 0.0;
        }

        /// <summary>Seconds left in the current stage (the header's clock).</summary>
        public double ClockLeft()
        {
            if (Mode != null) return Mode.TimeLeft;
            double end = Rules.OvertimeAfter;
            switch (GetClockStage())
            {
                case ClockStage.Extended: end += Rules.ExtendedSeconds; break;
                case ClockStage.Overtime: end += Rules.ExtendedSeconds + Rules.OvertimeSeconds; break;
            }
            return GMath.Maxf(end - RoundTime, 0.0);
        }

        /// <summary>True when the header's clock counts down.</summary>
        public bool CountsDown() => Mode != null || Rules.OvertimeAfter > 0.0;

        public bool InOvertime() => GetClockStage() == ClockStage.Overtime;

        /// <summary>Seconds left on the running capture countdown (0 when none runs).</summary>
        public double CaptureTimeLeft() => Objectives.CaptureTimeLeft(this);
    }
}
