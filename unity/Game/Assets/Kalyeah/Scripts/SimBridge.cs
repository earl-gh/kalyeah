// =====================================================================================================================================
// SimBridge: the ONE place the Unity game calls the Agawang Base sim (unity/Kalyeah.Sim) and online client (unity/Kalyeah.Net).
// Everything in unity/Game/Assets/Kalyeah/View reads the engine-free snapshots and events below.
// =====================================================================================================================================
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Kalyeah.Net;
using Kalyeah.Sim;

namespace Kalyeah.Game
{
    public enum MatchPhaseKind
    {
        Playing = 0,
        PointFreeze = 1,
        MatchOver = 2,
        Intro = 3,
    }

    public enum ClockStageKind
    {
        Regular = 0,
        Extended = 1,
        Overtime = 2,
    }

    public enum CaptureReasonKind
    {
        None = -1,
        Base = 0,
        AllTagged = 1,
        Touched = 2,
        TooSlow = 3,
    }

    public enum StatusKind
    {
        None = 0,
        Stun = 1,
        Slow = 2,
        Airborne = 3,
        Polymorph = 4,
        Bounce = 5,
        Knockout = 6,
        Tripped = 7,
        Root = 8,
        Slip = 9,
    }

    public enum BuffKind
    {
        None = 0,
        Palamig = 1,
        Teks = 2,
        Turon = 3,
    }

    public enum WeaponRoleKind
    {
        Attack = 0,
        CrowdControl = 1,
        Block = 2,
        Heal = 3,
    }

    public enum WeaponShapeKind
    {
        Targeted = 0,
        Cone = 1,
        GroundAoe = 2,
        Trap = 3,
        Field = 4,
        Boomerang = 5,
        Shield = 6,
        Bouncer = 7,
        Spinner = 8,
        Heal = 9,
        Skillshot = 10,
    }

    public enum WorldEffectKind
    {
        Projectile = 0,
        ExplosionZone = 1,
        TrapArmed = 2,
        TrapArming = 3,
        SlowZone = 4,
        FieldZone = 5,
        ShieldWall = 6,
        PendingCone = 7,
    }

    public enum AimCameraKind
    {
        Normal = 0,
        Ads = 1,
        ZoomOut = 2,
        MapCast = 3,
    }

    public enum RevealMarkerKind
    {
        None = 0,
        Outline = 1,
        Snapshot = 2,
    }

    public enum CharacterRoleKind
    {
        Striker = 0,
        Defender = 1,
        Seeker = 2,
        Controller = 3,
    }

    public enum SkillZoneVisualKind
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

    /// <summary>Bit flags matching Kalyeah.Sim.PlayerInput bit-for-bit.</summary>
    public static class InputButtons
    {
        public const int Card1 = 1 << 0;
        public const int Card2 = 1 << 1;
        public const int Card3 = 1 << 2;
        public const int Card4 = 1 << 3;
        public const int Weapon1 = Card1;
        public const int Weapon2 = Card2;
        public const int AimCancel = 1 << 5;
        public const int MoveTarget = 1 << 6;
        public const int AimPoint = 1 << 7;
        public const int Primary = 1 << 8;
        public const int Dash = 1 << 9;
        public const int Ultimate = 1 << 10;
        public const int Jump = 1 << 11;
        public const int Crouch = 1 << 12;
        public const int Slide = 1 << 13;
        public const int Ads = 1 << 14;
        public const int Reload = 1 << 15;

        public static int CardSlotBit(int slot) => (slot >= 0 && slot < 4) ? (1 << slot) : 0;
    }

    /// <summary>Full per-tick input frame from touch/gamepad/keyboard + AimSolver.</summary>
    public struct LocalInputFrame
    {
        public Vector2 Move;
        public Vector2 Aim;
        /// <summary>The camera's look on the ground (the kid faces it); zero = face the way you move.</summary>
        public Vector2 Look;
        public int Buttons;
        public Vector2 MoveTarget;
        public Vector2 AimPoint;
        public bool HasMoveTarget;
        public bool HasAimPoint;
    }

    /// <summary>Configuration for constructing a <see cref="SimBridge"/> match (Practice 5v5, Training sandbox, or Online/Hotspot).</summary>
    public sealed class MatchSetup
    {
        public long Seed;
        public string LocalCharacter = "narda";
        public string[] SuperpowerLoadout;
        public string BotDifficulty;
        public bool TrainingMode;
        public bool? CardCycleOverride;
        public bool? SpecKitsOverride;
        public int FirstTo;
        public GameClient OnlineClient;
    }

    /// <summary>Authoritative hit-confirm snapshot for crosshair hitmarkers, headshot dings, and TAYA! confirm stamps.</summary>
    public struct HitConfirmInfo
    {
        public int Tick;
        public int AttackerId;
        public int TargetId;
        public string WeaponId;
        public int Damage;
        public bool IsHeadshot;
        public bool IsTag;
        public Vector2 WorldPos;
    }

    /// <summary>What the view draws of one kid, copied out of the sim after each tick (zero per-tick heap allocations).</summary>
    public struct KidSnapshot
    {
        public int Id;
        public int Team;
        public string CharacterId;
        public CharacterRoleKind Role;
        /// <summary>Feet on the ground, sim XZ (GodotSpace.World makes it a Unity point).</summary>
        public Vector2 Position;
        public Vector2 Facing;
        public int Hp;
        public bool Tagged;
        /// <summary>How fast it moved horizontally this tick, as a share of a full run (0..1).</summary>
        public float Speed01;

        // Vertical & kinetic street locomotion
        public float AirY;
        public float JumpVel;
        public bool Walking;
        public bool Sprinting;
        public bool Crouching;
        public bool Sliding;
        public bool Dashing;
        public Vector2 DashDir;
        public Vector2 SlideDir;

        // Resources (legacy stamina/elixir + docs/KITS.md Water mana & 7-stone Tirador)
        public float Stamina;
        public float MaxStamina;
        public float ExhaustLeft;
        public bool Overclocked;
        public float Elixir;
        public float MaxElixir;
        public float UltCharge;
        public bool UsingSpecKit;
        public float Water;
        public float MaxWater;
        public bool InHomeFountain;
        public int TiradorAmmo;
        public int TiradorMaxAmmo;
        public float TiradorReloadLeft;
        public float TiradorReloadTotal;
        /// <summary>0..1 while pulling back Tirador; -1 when not charging.</summary>
        public float TiradorCharge01;
        public float TiradorCurrentRange;

        // Status, Merienda buff & spec kit combat states
        public StatusKind StatusKind;
        public float StatusLeft;
        public float ImmuneLeft;
        public float SlowMagnitude;
        public BuffKind ActiveBuff;
        public float BuffLeft;
        public string BuffId;
        public float BlurLeft;
        public float RevealedOutlineLeft;
        public float RevealAlertLeft;
        public bool KnownRevealAlert;
        public bool Invisible;
        public bool OnStilts;
        public bool InSackRace;
        public bool InLuksongBaka;
        public bool SpinningTrumpo;
        public float ShieldHp;
        public bool LangitProtected;
        public float TayaCountLeft;
        public bool SyatoRecastReady;
        public bool EmpoweredShotReady;

        // Casting, hold-aim & ability cooldowns
        public string CastingWeaponId;
        public float CastWindupLeft;
        public float CastLockLeft;
        public int HeldSlot;
        public float AimHoldSeconds;
        public float PrimaryCooldown;
        public float PrimaryMaxCooldown;
        public float DashCooldown;
        public float DashMaxCooldown;
        public float JumpCooldown;
        public float SlideCooldown;

        // Superpower loadout / 3-skill spec kit / 4-card cycle hand
        public bool CardsMode;
        public int HandCount;
        public string[] HandWeaponIds;
        public float[] SlotCooldowns;
        public float[] SlotMaxCooldowns;
        public int[] SlotElixirCosts;
        public int[] SlotWaterCosts;
        public int[] SlotCharges;
        public int[] SlotMaxCharges;
        public AimCameraKind[] SlotAimCameras;
        public string NextWeaponId;
        public string ShuffleWeaponId;
        public float ShuffleLeft;
        public float ShuffleTotal;

        // Objectives & tag state
        public bool InRescueZone;
        public bool NearEnemyBase;
        public bool CarryingFlag;
        public bool CastingBlocked;
        public int TagOrder;
        public float TagTime;
    }

    /// <summary>Active projectile, ground zone, trap, slow field, shield wall, or winding cone on the street.</summary>
    public struct WorldEffectSnapshot
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public WorldEffectKind Kind;
        public WeaponShapeKind Shape;
        public string WeaponId;
        public string Look;
        public Vector2 Position;
        public Vector2 Direction;
        public Vector2 EndPosition;
        public float Radius;
        public float HalfWidth;
        public float AngleDegrees;
        public float TimeLeft;
        public float Duration;
        public float Travelled;
        public float MaxRange;
        public bool Returning;
        public int Bounces;
    }

    public struct WallSnapshot
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public string SkillId;
        public Vector2 Center;
        public Vector2 Along;
        public float HalfWidth;
        public int Hp;
        public int MaxHp;
        public float TimeLeft;
        public float Duration;
    }

    public struct TrapSnapshot
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public string SkillId;
        public Vector2 Position;
        public float TriggerRadius;
        public bool Armed;
        public bool VisibleToLocal;
        public bool Revealed;
    }

    public struct SentrySnapshot
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public string SkillId;
        public Vector2 Position;
        public float Radius;
        public float TimeLeft;
        public float Duration;
        public float PingPulse01;
    }

    public struct TetherSnapshot
    {
        public int Id;
        public int OwnerId;
        public int AllyId;
        public int Team;
        public string SkillId;
        public Vector2 OwnerPos;
        public Vector2 AllyPos;
        public float MaxRange;
        public float Stretch01;
        public float TimeLeft;
    }

    public struct SkillZoneSnapshot
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public string SkillId;
        public SkillZoneVisualKind Kind;
        public Vector2 Position;
        public Vector2 EndPosition;
        public Vector2 Direction;
        public float Radius;
        public float HalfWidth;
        public float AngleDegrees;
        public int Count;
        public float Spacing;
        public float TimeLeft;
        public float Duration;
        public float DelayLeft;
    }

    public struct RevealMarkerSnapshot
    {
        public int Id;
        public int TargetId;
        public int Team;
        public string SkillId;
        public Vector2 Position;
        public float TimeLeft;
        public float Duration;
    }

    /// <summary>Barangay Merienda stand state (Palamig, Teks, Turon).</summary>
    public struct MeriendaStandSnapshot
    {
        public int Id;
        public BuffKind Item;
        public string ItemId;
        public Vector2 Position;
        public bool Active;
        public float RespawnLeft;
        public float RespawnTotal;
    }

    /// <summary>Team base post & capture ring state.</summary>
    public struct BaseObjectiveSnapshot
    {
        public int Team;
        public Vector2 Center;
        public Vector2 PostPosition;
        public float Radius;
        public float CaptureProgress01;
        public float CaptureElapsed;
        public float CaptureNeeded;
        public float CaptureTimeLeft;
        public int CapturingTeam;
        public int Holders;
        public bool Contested;
    }

    /// <summary>Team rescue shed zone, hold progress, and cooldown state.</summary>
    public struct RescueObjectiveSnapshot
    {
        public int Team;
        public GroundRect Zone;
        public Vector2 Center;
        public int RescuerId;
        public int WaitingCount;
        public float HoldProgress01;
        public float HoldElapsed;
        public float HoldNeeded;
        public float CooldownLeft;
        public float CooldownTotal;
        public bool Ready;
    }

    /// <summary>Static metadata and combat numbers for a weapon, superpower, or docs/KITS.md kid skill.</summary>
    public struct WeaponInfo
    {
        public string Id;
        public string DisplayName;
        public string ShortName;
        public string Description;
        public string StreetGame;
        public string IconPath;
        public string Look;
        public WeaponRoleKind Kind;
        public WeaponShapeKind Shape;
        public int ElixirCost;
        public int WaterCost;
        public int MaxCharges;
        public AimCameraKind AimCamera;
        public RevealMarkerKind RevealKind;
        public bool KnownRevealAlert;
        public bool IsUltimate;
        public int Damage;
        public int Heal;
        public int Count;
        public int CardPower;
        public float Cooldown;
        public float MaxRange;
        public float Radius;
        public float AngleDegrees;
        public float Delay;
        public float Speed;
        public float Duration;
        public float ArmTime;
        public float TriggerRadius;
        public float Width;
        public float Offset;
        public float ProjectileRadius;
        public StatusKind Effect;
        public float EffectDuration;
        public float EffectMagnitude;
        public float CastLock;
    }

    /// <summary>Agawang Base match bridge (Practice 5v5 bots, Training sandbox, or Online/Hotspot client mirror).</summary>
    public sealed class SimBridge : IDisposable
    {
        /// <summary>Default offline IDs matching scripts/view/practice_match.gd.</summary>
        public const int LocalId = 1;
        public static readonly int[] AllyIds = { 2, 3, 4, 5 };
        public static readonly int[] EnemyIds = { 6, 7, 8, 9, 10 };
        public const int LocalTeam = 0;

        public const int TrainingEnemyStandId = 2;
        public const int TrainingEnemyPatrolId = 3;
        public const int TrainingAllyRescuerId = 4;
        public const int TrainingEnemyRescueId = 5;

        // Convenience button bit aliases matching InputButtons / PlayerInput
        public const int BtnCard1 = InputButtons.Card1;
        public const int BtnCard2 = InputButtons.Card2;
        public const int BtnCard3 = InputButtons.Card3;
        public const int BtnCard4 = InputButtons.Card4;
        public const int BtnAimCancel = InputButtons.AimCancel;
        public const int BtnMoveTarget = InputButtons.MoveTarget;
        public const int BtnAimPoint = InputButtons.AimPoint;
        public const int BtnPrimary = InputButtons.Primary;
        public const int BtnDash = InputButtons.Dash;
        public const int BtnUltimate = InputButtons.Ultimate;
        public const int BtnJump = InputButtons.Jump;
        public const int BtnCrouch = InputButtons.Crouch;
        public const int BtnSlide = InputButtons.Slide;
        public const int BtnAds = InputButtons.Ads;
        public const int BtnReload = InputButtons.Reload;

        static readonly Regex RefPattern = new Regex("\"\\$ref\"\\s*:\\s*\"(res://data/[^\"]+)\"", RegexOptions.Compiled);

        static readonly StatusEffects.Type[] StatusPriority =
        {
            StatusEffects.Type.Knockout,
            StatusEffects.Type.Polymorph,
            StatusEffects.Type.Airborne,
            StatusEffects.Type.Stun,
            StatusEffects.Type.Root,
            StatusEffects.Type.Bounce,
            StatusEffects.Type.Tripped,
            StatusEffects.Type.Slip,
            StatusEffects.Type.Slow,
        };

        sealed class KidSlotCache
        {
            public string[] HandIds = Array.Empty<string>();
            public float[] Cooldowns = Array.Empty<float>();
            public float[] MaxCooldowns = Array.Empty<float>();
            public int[] Costs = Array.Empty<int>();
            public int[] WaterCosts = Array.Empty<int>();
            public int[] Charges = Array.Empty<int>();
            public int[] MaxCharges = Array.Empty<int>();
            public AimCameraKind[] AimCameras = Array.Empty<AimCameraKind>();

            public void EnsureSize(int count)
            {
                if (HandIds.Length == count) return;
                HandIds = count == 0 ? Array.Empty<string>() : new string[count];
                Cooldowns = count == 0 ? Array.Empty<float>() : new float[count];
                MaxCooldowns = count == 0 ? Array.Empty<float>() : new float[count];
                Costs = count == 0 ? Array.Empty<int>() : new int[count];
                WaterCosts = count == 0 ? Array.Empty<int>() : new int[count];
                Charges = count == 0 ? Array.Empty<int>() : new int[count];
                MaxCharges = count == 0 ? Array.Empty<int>() : new int[count];
                AimCameras = count == 0 ? Array.Empty<AimCameraKind>() : new AimCameraKind[count];
            }
        }

        readonly List<KidSnapshot> _kids = new List<KidSnapshot>(12);
        readonly List<WorldEffectSnapshot> _projectiles = new List<WorldEffectSnapshot>(32);
        readonly List<WorldEffectSnapshot> _worldEffects = new List<WorldEffectSnapshot>(48);
        readonly List<WallSnapshot> _walls = new List<WallSnapshot>(12);
        readonly List<TrapSnapshot> _traps = new List<TrapSnapshot>(16);
        readonly List<SentrySnapshot> _sentries = new List<SentrySnapshot>(8);
        readonly List<TetherSnapshot> _tethers = new List<TetherSnapshot>(6);
        readonly List<SkillZoneSnapshot> _skillZones = new List<SkillZoneSnapshot>(24);
        readonly List<RevealMarkerSnapshot> _revealMarkers = new List<RevealMarkerSnapshot>(24);
        readonly List<MeriendaStandSnapshot> _meriendaStands = new List<MeriendaStandSnapshot>(4);
        readonly BaseObjectiveSnapshot[] _bases = new BaseObjectiveSnapshot[2];
        readonly RescueObjectiveSnapshot[] _rescues = new RescueObjectiveSnapshot[2];
        readonly List<WeaponInfo> _allWeapons = new List<WeaponInfo>(16);
        readonly Dictionary<string, WeaponInfo> _weaponLookup = new Dictionary<string, WeaponInfo>(StringComparer.Ordinal);
        readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>(12);
        readonly Dictionary<int, KidSlotCache> _slotCaches = new Dictionary<int, KidSlotCache>(12);

        readonly float _runPerTick;
        LocalInputFrame _localInput;

        /// <summary>Seconds per tick (1 / tick_rate: 1/30).</summary>
        public float TickDt { get; }
        public int MaxHp { get; }
        public float MaxStamina { get; }
        public float MaxElixir { get; }
        public float MaxWater { get; }
        public int TiradorMaxAmmo { get; }
        public float TiradorMinRange { get; }
        public float TiradorMaxRange { get; }
        public float ArenaRadius { get; }
        public float PlayerRadius { get; }
        public bool UsesStandIn { get; }
        public bool IsOnline { get; }
        public bool IsTraining { get; }
        public bool SpecKitsActive => ActiveSim.Rules.UsesSpecKits() && ActiveSim.Players.TryGetValue(LocalPlayerId, out PlayerState lp) && lp.UsingSpecKit;

        public WeaponInfo? PrimaryWeaponInfo { get; }
        public WeaponInfo? UltimateWeaponInfo { get; }

        public IReadOnlyList<KidSnapshot> Kids => _kids;
        public IReadOnlyList<WorldEffectSnapshot> Projectiles => _projectiles;
        public IReadOnlyList<WorldEffectSnapshot> WorldEffects => _worldEffects;
        public IReadOnlyList<WallSnapshot> Walls => _walls;
        public IReadOnlyList<TrapSnapshot> Traps => _traps;
        public IReadOnlyList<SentrySnapshot> Sentries => _sentries;
        public IReadOnlyList<TetherSnapshot> Tethers => _tethers;
        public IReadOnlyList<SkillZoneSnapshot> SkillZones => _skillZones;
        public IReadOnlyList<RevealMarkerSnapshot> RevealMarkers => _revealMarkers;
        public IReadOnlyList<MeriendaStandSnapshot> MeriendaStands => _meriendaStands;
        public IReadOnlyList<BaseObjectiveSnapshot> Bases => _bases;
        public IReadOnlyList<RescueObjectiveSnapshot> Rescues => _rescues;
        public IReadOnlyList<WeaponInfo> AllWeapons => _allWeapons;
        /// <summary>Live authoritative BarangayNeutrals state (Mang Taho, Fishball, Selecta, Crocodile, Eagle) when SpecKits are active; null otherwise.</summary>
        public BarangayNeutrals Neutrals => ActiveSim?.Neutrals;

        // Events raised during Step (or mirror snapshot sync)
        public event Action<int, int> Tagged;
        public event Action<int, int> Rescued;
        public event Action<int, int> PointScored;
        public event Action<int> MatchWon;
        public event Action PointReset;
        public event Action IntroFinished;
        public event Action<int, string> WeaponCast;
        public event Action<int, string> WeaponHit;
        public event Action<int, int> PlayerDamaged;
        public event Action<int, int> PlayerHealed;
        public event Action<int, StatusKind> StatusApplied;
        public event Action<int> PlayerDashed;
        public event Action<int> UltimateCast;
        public event Action<int> PlayerExhausted;
        public event Action<int, int, BuffKind> PowerupTaken;
        /// <summary>(ownerId, weaponId, origin, direction, isWindup).</summary>
        public event Action<int, string, Vector2, Vector2, bool> ConeTriggered;
        /// <summary>(playerId, stickerIndex) quick-chat sticker event.</summary>
        public event Action<int, int> QuickChatSaid;

        // Chunk 2 spec combat events
        public event Action<HitConfirmInfo> HitConfirmed;
        public event Action<int, Vector2> WallBroken;
        public event Action<int, Vector2, bool> TrapTriggered;
        public event Action<int, int, RevealMarkerKind, bool> PlayerRevealed;
        public event Action<int> TiradorReloadStarted;
        public event Action<int> WaterInsufficient;

        // Live BarangayNeutrals events (forwarded zero-GC when SpecKits are active)
        public event Action<int, string> VendorUsed;
        public event Action<string> VendorRestocked;
        public event Action<int, int> SelectaClaimed;
        public event Action<NeutralBossKind, NeutralBossState, NeutralBossState> NeutralBossStateChanged;
        public event Action<NeutralBossKind, int, int> NeutralBossDefeated;

        public SimBridge(DataSource data, long seed, string localCharacter)
            : this(data, new MatchSetup { Seed = seed, LocalCharacter = localCharacter })
        {
        }

        readonly MatchSim _offlineSim;
        readonly TeamCoach[] _coaches;
        readonly GameClient _client;
        MatchSim _boundSim;
        int _onlineInputTick;

        MatchSim ActiveSim => (_client != null && _client.Mirror != null) ? _client.Mirror : _offlineSim;

        public SimBridge(DataSource data, MatchSetup setup)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            setup ??= new MatchSetup { Seed = 1L, LocalCharacter = "narda" };

            SimData simData = LoadSimDataGraph(data, "rules__game_rules", "rules__map_layout");
            GameRules rules = string.IsNullOrWhiteSpace(setup.BotDifficulty)
                ? simData.GameRules.Duplicate()
                : simData.GameRules.WithBotLevel(setup.BotDifficulty);

            if (setup.CardCycleOverride.HasValue)
                rules.CardCycle = setup.CardCycleOverride.Value;
            if (setup.SpecKitsOverride.HasValue)
                rules.SpecKitsEnabled = setup.SpecKitsOverride.Value;

            MapLayout layout = simData.MapLayout;
            TickDt = 1f / rules.TickRate;
            MaxHp = rules.PlayerMaxHp;
            MaxStamina = (float)rules.StaminaMax;
            MaxElixir = (float)rules.ElixirMax;
            MaxWater = (float)rules.WaterMax;
            TiradorMaxAmmo = rules.TiradorMaxAmmo;
            TiradorMinRange = (float)rules.TiradorMinRange;
            TiradorMaxRange = (float)rules.TiradorMaxRange;
            ArenaRadius = (float)layout.ArenaRadius;
            PlayerRadius = (float)rules.PlayerRadius;
            _runPerTick = (float)rules.MoveSpeed * TickDt;
            UsesStandIn = false;
            IsTraining = setup.TrainingMode && setup.OnlineClient == null;
            if (IsTraining) rules.BarrierSeconds = 0.0;  // the training range is a sandbox, not a round of Agawang Base
            IsOnline = setup.OnlineClient != null;
            _client = setup.OnlineClient;

            BuildWeaponCatalog(rules);
            if (rules.PrimaryWeapon != null) PrimaryWeaponInfo = ToWeaponInfo(rules.PrimaryWeapon);
            if (rules.UltimateWeapon != null) UltimateWeaponInfo = ToWeaponInfo(rules.UltimateWeapon);

            if (_client != null)
            {
                _offlineSim = new MatchSim(rules, layout, setup.Seed);
                _coaches = Array.Empty<TeamCoach>();
                _client.Said += OnClientSaid;
                EnsureBoundSim(ActiveSim);
                Refresh();
                return;
            }

            _offlineSim = new MatchSim(rules, layout, setup.Seed);
            if (setup.FirstTo > 0)
                _offlineSim.SetFirstTo(setup.FirstTo);
            EnsureBoundSim(_offlineSim);

            string avatar = string.IsNullOrWhiteSpace(setup.LocalCharacter) ? "narda" : setup.LocalCharacter;
            if (IsTraining)
            {
                _coaches = Array.Empty<TeamCoach>();
                SetupTrainingMatch(_offlineSim, rules, layout, avatar, setup.SuperpowerLoadout, setup.SpecKitsOverride);
            }
            else
            {
                _coaches = SetupBotMatch(_offlineSim, rules, avatar, setup.SuperpowerLoadout, setup.SpecKitsOverride);
            }

            _offlineSim.BeginIntro();
            Refresh();
        }

        static TeamCoach[] SetupBotMatch(MatchSim sim, GameRules rules, string avatar, string[] loadout, bool? specOverride)
        {
            sim.AddPlayer(LocalId, LocalTeam);
            var allies = new TeamCoach(LocalTeam, LocalId);
            var foes = new TeamCoach(1 - LocalTeam);
            foreach (int id in AllyIds) { sim.AddPlayer(id, LocalTeam); allies.Add(sim, id, rules.BotSkill); }
            foreach (int id in EnemyIds) { sim.AddPlayer(id, 1 - LocalTeam); foes.Add(sim, id, rules.BotSkill); }

            AssignUniqueRoster(sim, rules, avatar);
            bool useSpec = rules.UsesSpecKits() && !rules.CardCycle && specOverride != false && sim.HasSpecKit(avatar);
            if (useSpec) sim.EquipSpecKit(LocalId);
            else EquipLocalPlayer(sim, LocalId, loadout);
            foreach (int id in AllyIds) sim.Equip(id, true);
            foreach (int id in EnemyIds) sim.Equip(id, true);
            return new[] { allies, foes };
        }

        static void SetupTrainingMatch(MatchSim sim, GameRules rules, MapLayout layout, string avatar, string[] loadout, bool? specOverride)
        {
            sim.AddPlayer(LocalId, LocalTeam);
            var standSpot = new Vec2(rules.DummyStandX, rules.DummyZ);
            var patrolSpot = new Vec2(rules.DummyPatrolX, rules.DummyZ);
            DummyBrain patrol = DummyBrain.Patrolling(rules.DummyPatrolX, rules.DummyPatrolRange, rules.DummyPatrolSpeedScale);

            sim.AddDummy(
                TrainingEnemyStandId,
                1,
                standSpot,
                rules.PracticeFighters >= 2 ? (DummyBrain)FightBrain.Fighter(standSpot) : DummyBrain.Standing());
            sim.AddDummy(
                TrainingEnemyPatrolId,
                1,
                patrolSpot,
                rules.PracticeFighters >= 1 ? (DummyBrain)FightBrain.Fighter(patrolSpot) : patrol);

            var allySpot = new Vec2(rules.DummyAllyX, rules.DummyAllyZ);
            sim.AddDummy(TrainingAllyRescuerId, LocalTeam, allySpot, DummyBrain.Rescuer(allySpot));
            sim.AddDummy(TrainingEnemyRescueId, 1, layout.RescueRect(sim.JailSideFor(1)).GetCenter(), DummyBrain.Standing());

            sim.AssignCharacters();
            sim.SetCharacter(LocalId, avatar);
            bool explicitLegacy = loadout != null && loadout.Length > 0 && specOverride != true;
            if (!explicitLegacy && rules.UsesSpecKits() && !rules.CardCycle && specOverride != false && sim.HasSpecKit(avatar))
                sim.EquipSpecKit(LocalId);
            else
                EquipLocalPlayer(sim, LocalId, loadout);
            if (sim.IsFighter(TrainingEnemyStandId)) sim.Equip(TrainingEnemyStandId, true);
            if (sim.IsFighter(TrainingEnemyPatrolId)) sim.Equip(TrainingEnemyPatrolId, true);
        }

        static void EquipLocalPlayer(MatchSim sim, int playerId, string[] loadout)
        {
            if (loadout != null && loadout.Length > 0 && !sim.Rules.CardCycle)
            {
                if (sim.SetLoadoutList(playerId, loadout))
                    return;
            }
            sim.Equip(playerId);
        }

        bool _disposed;

        void OnClientSaid(int pid, int sticker) => QuickChatSaid?.Invoke(pid, sticker);
        void OnSimPlayerTagged(int id, int by) => Tagged?.Invoke(id, by);
        void OnSimPlayerRescued(int id, int by) => Rescued?.Invoke(id, by);
        void OnSimPointScored(int team, int how) => PointScored?.Invoke(team, how);
        void OnSimMatchWon(int team) => MatchWon?.Invoke(team);
        void OnSimPointReset() => PointReset?.Invoke();
        void OnSimIntroFinished() => IntroFinished?.Invoke();
        void OnSimWeaponCast(int id, string wid) => WeaponCast?.Invoke(id, wid);
        void OnSimWeaponHit(int id, string wid) => WeaponHit?.Invoke(id, wid);
        void OnSimPlayerDamaged(int id, int amt) => PlayerDamaged?.Invoke(id, amt);
        void OnSimPlayerHealed(int id, int amt) => PlayerHealed?.Invoke(id, amt);
        void OnSimEffectApplied(int id, StatusEffects.Type type) => StatusApplied?.Invoke(id, (StatusKind)(int)type);
        void OnSimPlayerDashed(int id) => PlayerDashed?.Invoke(id);
        void OnSimUltimateCast(int id) => UltimateCast?.Invoke(id);
        void OnSimExhausted(int id) => PlayerExhausted?.Invoke(id);
        void OnSimPowerupTaken(int pid, int standId, int kind) => PowerupTaken?.Invoke(pid, standId, (BuffKind)kind);

        void OnSimHitConfirmed(HitConfirmEvent ev) => HitConfirmed?.Invoke(new HitConfirmInfo
        {
            Tick = ev.Tick,
            AttackerId = ev.AttackerId,
            TargetId = ev.TargetId,
            WeaponId = ev.WeaponId ?? "",
            Damage = ev.Damage,
            IsHeadshot = ev.IsHeadshot,
            IsTag = ev.IsTag,
            WorldPos = new Vector2(ev.WorldPos.X, ev.WorldPos.Y),
        });

        void OnSimWallBroken(int ownerId, Vec2 pos) => WallBroken?.Invoke(ownerId, new Vector2(pos.X, pos.Y));
        void OnSimTrapTriggered(int ownerId, Vec2 pos, bool disarmed) => TrapTriggered?.Invoke(ownerId, new Vector2(pos.X, pos.Y), disarmed);
        void OnSimPlayerRevealed(int targetId, int viewerTeam, RevealKind kind, bool knownAlert) =>
            PlayerRevealed?.Invoke(targetId, viewerTeam, (RevealMarkerKind)(int)kind, knownAlert);
        void OnSimTiradorReloadStarted(int playerId) => TiradorReloadStarted?.Invoke(playerId);
        void OnSimWaterInsufficient(int playerId) => WaterInsufficient?.Invoke(playerId);
        void OnNeutralVendorUsed(int playerId, string vendorId) => VendorUsed?.Invoke(playerId, vendorId);
        void OnNeutralVendorRestocked(string vendorId) => VendorRestocked?.Invoke(vendorId);
        void OnNeutralSelectaClaimed(int playerId, int team) => SelectaClaimed?.Invoke(playerId, team);
        void OnNeutralBossStateChanged(NeutralBossKind kind, NeutralBossState from, NeutralBossState to) => NeutralBossStateChanged?.Invoke(kind, from, to);
        void OnNeutralBossDefeated(NeutralBossKind kind, int killerId, int killerTeam) => NeutralBossDefeated?.Invoke(kind, killerId, killerTeam);

        void OnSimConeWinding(int owner, WeaponDef def, Vec2 origin, Vec2 dir) =>
            ConeTriggered?.Invoke(owner, def.Id, new Vector2(origin.X, origin.Y), new Vector2(dir.X, dir.Y), true);

        void OnSimConeStruck(int owner, WeaponDef def, Vec2 origin, Vec2 dir) =>
            ConeTriggered?.Invoke(owner, def.Id, new Vector2(origin.X, origin.Y), new Vector2(dir.X, dir.Y), false);

        void EnsureBoundSim(MatchSim sim)
        {
            if (sim == null || ReferenceEquals(_boundSim, sim)) return;
            UnbindSim();
            _boundSim = sim;
            sim.PlayerTagged += OnSimPlayerTagged;
            sim.PlayerRescued += OnSimPlayerRescued;
            sim.PointScored += OnSimPointScored;
            sim.MatchWon += OnSimMatchWon;
            sim.PointReset += OnSimPointReset;
            sim.IntroFinished += OnSimIntroFinished;
            sim.WeaponCast += OnSimWeaponCast;
            sim.WeaponHit += OnSimWeaponHit;
            sim.PlayerDamaged += OnSimPlayerDamaged;
            sim.PlayerHealed += OnSimPlayerHealed;
            sim.EffectApplied += OnSimEffectApplied;
            sim.PlayerDashed += OnSimPlayerDashed;
            sim.UltimateCast += OnSimUltimateCast;
            sim.Exhausted += OnSimExhausted;
            sim.PowerupTaken += OnSimPowerupTaken;
            sim.HitConfirmed += OnSimHitConfirmed;
            sim.WallBroken += OnSimWallBroken;
            sim.TrapTriggered += OnSimTrapTriggered;
            sim.PlayerRevealed += OnSimPlayerRevealed;
            sim.TiradorReloadStarted += OnSimTiradorReloadStarted;
            sim.WaterInsufficient += OnSimWaterInsufficient;
            sim.Weapons.ConeWinding += OnSimConeWinding;
            sim.Weapons.ConeStruck += OnSimConeStruck;
            if (sim.Neutrals != null)
            {
                sim.Neutrals.VendorUsed += OnNeutralVendorUsed;
                sim.Neutrals.VendorRestocked += OnNeutralVendorRestocked;
                sim.Neutrals.SelectaClaimed += OnNeutralSelectaClaimed;
                sim.Neutrals.NeutralBossStateChanged += OnNeutralBossStateChanged;
                sim.Neutrals.NeutralBossDefeated += OnNeutralBossDefeated;
            }
        }

        void UnbindSim()
        {
            if (_boundSim == null) return;
            MatchSim sim = _boundSim;
            _boundSim = null;
            sim.PlayerTagged -= OnSimPlayerTagged;
            sim.PlayerRescued -= OnSimPlayerRescued;
            sim.PointScored -= OnSimPointScored;
            sim.MatchWon -= OnSimMatchWon;
            sim.PointReset -= OnSimPointReset;
            sim.IntroFinished -= OnSimIntroFinished;
            sim.WeaponCast -= OnSimWeaponCast;
            sim.WeaponHit -= OnSimWeaponHit;
            sim.PlayerDamaged -= OnSimPlayerDamaged;
            sim.PlayerHealed -= OnSimPlayerHealed;
            sim.EffectApplied -= OnSimEffectApplied;
            sim.PlayerDashed -= OnSimPlayerDashed;
            sim.UltimateCast -= OnSimUltimateCast;
            sim.Exhausted -= OnSimExhausted;
            sim.PowerupTaken -= OnSimPowerupTaken;
            sim.HitConfirmed -= OnSimHitConfirmed;
            sim.WallBroken -= OnSimWallBroken;
            sim.TrapTriggered -= OnSimTrapTriggered;
            sim.PlayerRevealed -= OnSimPlayerRevealed;
            sim.TiradorReloadStarted -= OnSimTiradorReloadStarted;
            sim.WaterInsufficient -= OnSimWaterInsufficient;
            sim.Weapons.ConeWinding -= OnSimConeWinding;
            sim.Weapons.ConeStruck -= OnSimConeStruck;
            if (sim.Neutrals != null)
            {
                sim.Neutrals.VendorUsed -= OnNeutralVendorUsed;
                sim.Neutrals.VendorRestocked -= OnNeutralVendorRestocked;
                sim.Neutrals.SelectaClaimed -= OnNeutralSelectaClaimed;
                sim.Neutrals.NeutralBossStateChanged -= OnNeutralBossStateChanged;
                sim.Neutrals.NeutralBossDefeated -= OnNeutralBossDefeated;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_client != null)
                _client.Said -= OnClientSaid;
            UnbindSim();
        }

        /// <summary>Places player <paramref name="id"/> at <paramref name="position"/> facing <paramref name="facing"/> and refreshes snapshots (for preview/screenshot staging).</summary>
        public void SetPlayerPositionForPreview(int id, Vector2 position, Vector2 facing)
        {
            MatchSim sim = ActiveSim;
            if (sim.Players.TryGetValue(id, out PlayerState state))
            {
                state.Position = new Vec2(position.X, position.Y);
                if (facing.LengthSquared() > 0.0001f)
                {
                    Vector2 norm = Vector2.Normalize(facing);
                    state.Facing = new Vec2(norm.X, norm.Y);
                }
                state.AirY = 0.0;
                state.VelY = 0.0;
                state.Tagged = false;
                state.HasMoveTarget = false;
                _lastPositions[id] = position;
            }
            Refresh();
        }

        internal static SimData LoadSimDataGraph(DataSource data, params string[] roots)
        {
            var simData = new SimData();
            var pending = new Queue<string>(roots);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                string text = data.Text(name);
                simData.AddFile(text);
                foreach (Match m in RefPattern.Matches(text))
                    pending.Enqueue(DataSource.NameOfResPath(m.Groups[1].Value));
            }
            return simData;
        }

        static void AssignUniqueRoster(MatchSim sim, GameRules rules, string avatar)
        {
            if (rules.Characters.Count < sim.Players.Count || string.IsNullOrEmpty(avatar))
            {
                sim.AssignCharacters();
                if (!string.IsNullOrEmpty(avatar)) sim.SetCharacter(LocalId, avatar);
                return;
            }
            var pool = new List<string>(rules.Characters.Count);
            foreach (CharacterDef c in rules.Characters) pool.Add(c.Id);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = sim.Rng.RandiRange(0, i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int wanted = pool.IndexOf(avatar);
            if (wanted > 0) { pool[wanted] = pool[0]; pool[0] = avatar; }
            int cursor = 0;
            foreach (var pair in sim.Players) pair.Value.CharacterId = pool[cursor++];
        }

        public int LocalPlayerId => (_client != null && _client.PlayerId > 0) ? _client.PlayerId : LocalId;

        public int LocalPlayerTeam
        {
            get
            {
                MatchSim sim = ActiveSim;
                if (sim.Players.TryGetValue(LocalPlayerId, out PlayerState p))
                    return p.Team;
                return LocalTeam;
            }
        }

        public int Tick => ActiveSim.Tick;
        public float ClockSeconds => (float)ActiveSim.MatchTime;
        public float RoundClockLeft => (float)ActiveSim.ClockLeft();
        public bool ClockCountsDown => ActiveSim.CountsDown();
        public MatchPhaseKind Phase => (MatchPhaseKind)(int)ActiveSim.Phase;
        public ClockStageKind ClockStage => (ClockStageKind)(int)ActiveSim.GetClockStage();
        public CaptureReasonKind CaptureReason => (CaptureReasonKind)(int)ActiveSim.CurrentCaptureKind;

        public float PhaseTimeLeft
        {
            get
            {
                MatchSim sim = ActiveSim;
                switch (sim.Phase)
                {
                    case MatchSim.MatchPhase.Intro: return (float)sim.IntroLeft;
                    case MatchSim.MatchPhase.PointFreeze: return (float)sim.FreezeLeft;
                    default: return 0f;
                }
            }
        }

        public int WinnerTeam => ActiveSim.Score.Winner;
        public int PointsToWin => ActiveSim.Score.FirstTo;
        public int RoundNumber => ActiveSim.PointLog.Count + 1;
        public int CaptureTeam => ActiveSim.CaptureTeam;

        public float CaptureProgress01
        {
            get
            {
                MatchSim sim = ActiveSim;
                if (sim.CaptureTeam < 0 || sim.CaptureNeeded <= 0.0) return 0f;
                return Math.Clamp((float)(sim.CaptureElapsed / sim.CaptureNeeded), 0f, 1f);
            }
        }

        public bool CanSwapLoadout
        {
            get
            {
                MatchSim sim = ActiveSim;
                return !sim.Rules.CardCycle &&
                       sim.Players.TryGetValue(LocalPlayerId, out PlayerState p) &&
                       p.Tagged;
            }
        }

        public int ScoreOf(int team)
        {
            int[] pts = ActiveSim.Score.Points;
            return (team >= 0 && team < pts.Length) ? pts[team] : 0;
        }

        public void Step()
        {
            if (_client != null)
            {
                EnsureBoundSim(ActiveSim);
                if (_client.IsOnline())
                {
                    _onlineInputTick += 1;
                    _client.SendInput(BuildSimInput(_onlineInputTick), TickDt);
                }
                _client.Interpolate();
                Refresh();
                return;
            }

            for (int i = 0; i < _coaches.Length; i++)
                _coaches[i].Drive(_offlineSim);
            _offlineSim.SetInput(LocalId, BuildSimInput(_offlineSim.Tick));
            _offlineSim.Step(TickDt);
            Refresh();
        }

        /// <summary>Updates snapshots from the client's sub-tick interpolation buffer (no-op offline).</summary>
        public void SyncInterpolated()
        {
            if (_client == null || _client.Mirror == null) return;
            EnsureBoundSim(_client.Mirror);
            _client.Interpolate();
            Refresh();
        }

        public bool SkipIntro()
        {
            if (_client != null || _offlineSim.Phase != MatchSim.MatchPhase.Intro) return false;
            _offlineSim.IntroLeft = 0.0;
            _offlineSim.Phase = MatchSim.MatchPhase.Playing;
            IntroFinished?.Invoke();
            Refresh();
            return true;
        }

        public bool ApplyLoadout(IList<string> weaponIds, string characterId = null)
        {
            if (weaponIds == null || weaponIds.Count == 0) return false;
            string first = weaponIds[0] ?? "";
            string second = weaponIds.Count > 1 ? (weaponIds[1] ?? "") : "";
            string third = weaponIds.Count > 2 ? (weaponIds[2] ?? "") : "";

            if (_client != null)
            {
                _client.PickWeapons(first, second, characterId ?? "", third);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(characterId))
                _offlineSim.SetCharacter(LocalPlayerId, characterId.Trim());
            bool ok = _offlineSim.SetLoadoutList(LocalPlayerId, weaponIds);
            Refresh();
            return ok;
        }

        /// <summary>D13 mid-match weapon swap while tagged on the sidewalk.</summary>
        public bool TrySwapLoadout(IList<string> weaponIds)
        {
            if (weaponIds == null || weaponIds.Count < 2) return false;
            string first = weaponIds[0] ?? "";
            string second = weaponIds[1] ?? "";
            string third = weaponIds.Count > 2 ? (weaponIds[2] ?? "") : "";

            if (_client != null)
            {
                if (!CanSwapLoadout) return false;
                _client.SwapWeapons(first, second, third);
                return true;
            }

            bool ok = _offlineSim.SwapLoadoutList(LocalPlayerId, weaponIds);
            if (ok) Refresh();
            return ok;
        }

        public void SendQuickChat(int sticker)
        {
            if (_client != null)
            {
                _client.Say(sticker);
                return;
            }
            QuickChatSaid?.Invoke(LocalPlayerId, sticker);
        }

        PlayerInput BuildSimInput(int tick)
        {
            int buttons = _localInput.Buttons;
            if (_localInput.HasMoveTarget) buttons |= PlayerInput.BtnMoveTarget;
            if (_localInput.HasAimPoint) buttons |= PlayerInput.BtnAimPoint;

            PlayerInput input = PlayerInput.Create(
                new Vec2(_localInput.Move.X, _localInput.Move.Y),
                new Vec2(_localInput.Aim.X, _localInput.Aim.Y),
                buttons,
                tick);
            input.Look = new Vec2(_localInput.Look.X, _localInput.Look.Y);

            if ((buttons & PlayerInput.BtnMoveTarget) != 0)
                input.MoveTarget = new Vec2(_localInput.MoveTarget.X, _localInput.MoveTarget.Y);
            if ((buttons & PlayerInput.BtnAimPoint) != 0)
                input.AimPoint = new Vec2(_localInput.AimPoint.X, _localInput.AimPoint.Y);
            return input;
        }

        void BuildWeaponCatalog(GameRules rules)
        {
            _allWeapons.Clear();
            _weaponLookup.Clear();
            for (int i = 0; i < rules.Weapons.Count; i++)
            {
                WeaponDef def = rules.Weapons[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                WeaponInfo info = ToWeaponInfo(def);
                _allWeapons.Add(info);
                _weaponLookup[info.Id] = info;
            }
            if (rules.PrimaryWeapon != null && !string.IsNullOrEmpty(rules.PrimaryWeapon.Id))
                _weaponLookup[rules.PrimaryWeapon.Id] = ToWeaponInfo(rules.PrimaryWeapon);
            if (rules.UltimateWeapon != null && !string.IsNullOrEmpty(rules.UltimateWeapon.Id))
                _weaponLookup[rules.UltimateWeapon.Id] = ToWeaponInfo(rules.UltimateWeapon);

            if (rules.Characters != null)
            {
                for (int c = 0; c < rules.Characters.Count; c++)
                {
                    CharacterDef ch = rules.Characters[c];
                    if (ch == null || ch.Skills == null) continue;
                    for (int s = 0; s < ch.Skills.Count; s++)
                    {
                        SkillDef sk = ch.Skills[s];
                        if (sk == null || string.IsNullOrEmpty(sk.Id)) continue;
                        _weaponLookup[sk.Id] = ToSkillWeaponInfo(sk);
                    }
                }
            }
        }

        static WeaponInfo ToWeaponInfo(WeaponDef def)
        {
            return new WeaponInfo
            {
                Id = def.Id ?? "",
                DisplayName = def.DisplayName ?? "",
                ShortName = def.ShortName ?? "",
                Description = def.Description ?? "",
                StreetGame = def.StreetGame ?? "",
                IconPath = string.IsNullOrEmpty(def.Id) ? "" : ("ui/weapons/" + def.Id),
                Look = def.Look ?? "",
                Kind = (WeaponRoleKind)(int)def.Kind,
                Shape = (WeaponShapeKind)(int)def.Shape,
                ElixirCost = def.ElixirCost,
                WaterCost = 0,
                MaxCharges = 1,
                AimCamera = AimCameraKind.Normal,
                RevealKind = RevealMarkerKind.None,
                KnownRevealAlert = false,
                IsUltimate = false,
                Damage = def.Damage,
                Heal = def.Heal,
                Count = def.Count,
                CardPower = def.CardPower,
                Cooldown = (float)def.Cooldown,
                MaxRange = (float)def.MaxRange,
                Radius = (float)def.Radius,
                AngleDegrees = (float)def.AngleDegrees,
                Delay = (float)def.Delay,
                Speed = (float)def.Speed,
                Duration = (float)def.Duration,
                ArmTime = (float)def.ArmTime,
                TriggerRadius = (float)def.TriggerRadius,
                Width = (float)def.Width,
                Offset = (float)def.Offset,
                ProjectileRadius = (float)def.ProjectileRadius,
                Effect = (StatusKind)(int)def.Effect,
                EffectDuration = (float)def.EffectDuration,
                EffectMagnitude = (float)def.EffectMagnitude,
                CastLock = (float)def.CastLock,
            };
        }

        static WeaponInfo ToSkillWeaponInfo(SkillDef sk)
        {
            WeaponRoleKind roleKind = sk.Heal > 0 ? WeaponRoleKind.Heal
                : (sk.WallHp > 0 ? WeaponRoleKind.Block
                : (sk.Damage > 0 ? WeaponRoleKind.Attack : WeaponRoleKind.CrowdControl));
            return new WeaponInfo
            {
                Id = sk.Id ?? "",
                DisplayName = sk.DisplayName ?? "",
                ShortName = sk.ShortName ?? "",
                Description = sk.Description ?? "",
                StreetGame = sk.StreetGame ?? "",
                IconPath = string.IsNullOrEmpty(sk.Id) ? "" : ("ui/weapons/" + sk.Id),
                Look = "",
                Kind = roleKind,
                Shape = ShapeForSkill(sk.Mechanic),
                ElixirCost = 0,
                WaterCost = sk.WaterCost,
                MaxCharges = Math.Max(1, sk.MaxCharges),
                AimCamera = (AimCameraKind)(int)sk.AimCamera,
                RevealKind = (RevealMarkerKind)(int)sk.RevealKind,
                KnownRevealAlert = sk.KnownRevealAlert,
                IsUltimate = sk.IsUltimate,
                Damage = sk.Damage,
                Heal = sk.Heal,
                Count = Math.Max(1, sk.Count),
                CardPower = 0,
                Cooldown = (float)sk.Cooldown,
                MaxRange = (float)sk.MaxRange,
                Radius = (float)sk.Radius,
                AngleDegrees = (float)sk.AngleDegrees,
                Delay = (float)sk.Delay,
                Speed = (float)sk.Speed,
                Duration = (float)sk.Duration,
                ArmTime = 0.4f,
                TriggerRadius = (float)sk.Radius,
                Width = (float)sk.Width,
                Offset = 0f,
                ProjectileRadius = 0.30f,
                Effect = StatusKind.None,
                EffectDuration = (float)sk.EffectDuration,
                EffectMagnitude = (float)sk.EffectMagnitude,
                CastLock = 0f,
            };
        }

        static WeaponShapeKind ShapeForSkill(SkillMechanic mechanic)
        {
            switch (mechanic)
            {
                case SkillMechanic.TsinelasBagsik: return WeaponShapeKind.Boomerang;
                case SkillMechanic.SiliTubig:
                case SkillMechanic.HolensSpill: return WeaponShapeKind.Cone;
                case SkillMechanic.BahayBahayan:
                case SkillMechanic.TinikWall: return WeaponShapeKind.Shield;
                case SkillMechanic.TeksTripwire:
                case SkillMechanic.SakoTrap: return WeaponShapeKind.Trap;
                case SkillMechanic.PabitinHeal:
                case SkillMechanic.Palosebo:
                case SkillMechanic.Saranggola:
                case SkillMechanic.Piko:
                case SkillMechanic.Jackstone:
                case SkillMechanic.GarterNet:
                case SkillMechanic.PatinteroLines:
                case SkillMechanic.LuksongLubid: return WeaponShapeKind.Field;
                case SkillMechanic.PukpokPalayok:
                case SkillMechanic.PogsSlam:
                case SkillMechanic.PabitinSnare:
                case SkillMechanic.LupaQuake: return WeaponShapeKind.GroundAoe;
                default: return WeaponShapeKind.Skillshot;
            }
        }

        void Refresh()
        {
            MatchSim sim = ActiveSim;
            GameRules rules = sim.Rules;
            MapLayout layout = sim.Layout;

            _kids.Clear();
            IReadOnlyList<WeaponSystem.PendingCone> pendingCones = sim.Weapons.PendingCones;

            foreach (var pair in sim.Players)
            {
                PlayerState p = pair.Value;
                var pos = new Vector2(p.Position.X, p.Position.Y);
                var facing = new Vector2(p.Facing.X, p.Facing.Y);
                float speed = MeasureSpeed01(p.Id, pos);

                StatusKind domStatus = DominantStatus(p.Effects, out float statusLeft);
                int enemySide = sim.SideForTeam(1 - p.Team);
                bool inEnemyBase = !p.Tagged && layout.InBase(enemySide, p.Position);
                bool inOwnRescue = !p.Tagged && sim.Objectives.InRescueArea(sim, p);
                bool castBlocked = (rules.NoCastInEnemyBase && inEnemyBase) || (rules.NoCastInRescueArea && inOwnRescue);

                int heldSlot = -1;
                float heldSec = 0f;
                for (int s = 0; s < p.AimHold.Count; s++)
                {
                    if (p.AimHold[s] >= 0.0)
                    {
                        heldSlot = s;
                        heldSec = (float)p.AimHold[s];
                        break;
                    }
                }

                string castingWid = "";
                float windupLeft = 0f;
                for (int c = 0; c < pendingCones.Count; c++)
                {
                    if (pendingCones[c].OwnerId == p.Id)
                    {
                        castingWid = pendingCones[c].Def != null ? pendingWid(pendingCones[c].Def) : "";
                        windupLeft = (float)pendingCones[c].TimeLeft;
                        break;
                    }
                }
                if (castingWid.Length == 0 && heldSlot >= 0 && heldSlot < p.Weapons.Count)
                    castingWid = p.Weapons[heldSlot] ?? "";

                if (!_slotCaches.TryGetValue(p.Id, out KidSlotCache slotCache))
                {
                    slotCache = new KidSlotCache();
                    _slotCaches[p.Id] = slotCache;
                }
                int handCount = p.Weapons.Count;
                slotCache.EnsureSize(handCount);
                for (int s = 0; s < handCount; s++)
                {
                    string wid = p.Weapons[s] ?? "";
                    slotCache.HandIds[s] = wid;
                    slotCache.Cooldowns[s] = s < p.WeaponCooldowns.Count ? (float)p.WeaponCooldowns[s] : 0f;
                    if (p.UsingSpecKit && sim.SkillDefs.TryGetValue(wid, out SkillDef sdef))
                    {
                        slotCache.MaxCooldowns[s] = (float)sdef.Cooldown;
                        slotCache.Costs[s] = 0;
                        slotCache.WaterCosts[s] = sdef.WaterCost;
                        int maxChg = Math.Max(1, sdef.MaxCharges);
                        slotCache.MaxCharges[s] = maxChg;
                        slotCache.Charges[s] = (s == 0 && maxChg > 1) ? p.S1Charges : (slotCache.Cooldowns[s] <= 0f ? 1 : 0);
                        slotCache.AimCameras[s] = (AimCameraKind)(int)sdef.AimCamera;
                    }
                    else if (sim.WeaponDefs.TryGetValue(wid, out WeaponDef wdef))
                    {
                        slotCache.MaxCooldowns[s] = (float)wdef.Cooldown;
                        slotCache.Costs[s] = wdef.ElixirCost;
                        slotCache.WaterCosts[s] = 0;
                        slotCache.MaxCharges[s] = 1;
                        slotCache.Charges[s] = slotCache.Cooldowns[s] <= 0f ? 1 : 0;
                        slotCache.AimCameras[s] = AimCameraKind.Normal;
                    }
                    else
                    {
                        slotCache.MaxCooldowns[s] = 0f;
                        slotCache.Costs[s] = 0;
                        slotCache.WaterCosts[s] = 0;
                        slotCache.MaxCharges[s] = 1;
                        slotCache.Charges[s] = 0;
                        slotCache.AimCameras[s] = AimCameraKind.Normal;
                    }
                }

                float charge01 = p.TiradorChargeTime >= 0.0
                    ? Math.Clamp((float)(rules.TiradorChargeSeconds > 0.0 ? p.TiradorChargeTime / rules.TiradorChargeSeconds : 0.0), 0f, 1f)
                    : -1f;
                float currentRange = (float)GMath.Lerpf(rules.TiradorMinRange, rules.TiradorMaxRange, charge01 >= 0f ? charge01 : 0f);
                if (p.StiltsLeft > 0.0) currentRange *= 1.4f;

                CharacterDef cdef = sim.GetCharacterDef(p.CharacterId);
                CharacterRoleKind role = cdef != null ? (CharacterRoleKind)(int)cdef.Role : CharacterRoleKind.Striker;
                BuffKind buff = (BuffKind)p.BuffType;
                _kids.Add(new KidSnapshot
                {
                    Id = p.Id,
                    Team = p.Team,
                    CharacterId = p.CharacterId ?? "",
                    Role = role,
                    Position = pos,
                    Facing = facing,
                    Hp = p.Hp,
                    Tagged = p.Tagged,
                    Speed01 = speed,

                    AirY = (float)p.AirY,
                    JumpVel = (float)p.VelY,
                    Walking = p.Walking,
                    Sprinting = p.Sprinting,
                    Crouching = p.Crouching,
                    Sliding = p.SlideLeft > 0.0,
                    Dashing = p.DashLeft > 0.0,
                    DashDir = new Vector2(p.DashDir.X, p.DashDir.Y),
                    SlideDir = new Vector2(p.SlideDir.X, p.SlideDir.Y),

                    Stamina = (float)p.Stamina,
                    MaxStamina = (float)rules.StaminaMax,
                    ExhaustLeft = (float)p.ExhaustLeft,
                    Overclocked = p.Overclocked,
                    Elixir = (float)p.Elixir,
                    MaxElixir = (float)rules.ElixirMax,
                    UltCharge = (float)p.UltCharge,
                    UsingSpecKit = p.UsingSpecKit,
                    Water = (float)p.Water,
                    MaxWater = (float)rules.WaterMax,
                    InHomeFountain = p.InHomeFountain,
                    TiradorAmmo = p.TiradorAmmo,
                    TiradorMaxAmmo = rules.TiradorMaxAmmo,
                    TiradorReloadLeft = (float)p.TiradorReloadLeft,
                    TiradorReloadTotal = (float)rules.TiradorReloadSeconds,
                    TiradorCharge01 = charge01,
                    TiradorCurrentRange = currentRange,

                    StatusKind = domStatus,
                    StatusLeft = statusLeft,
                    ImmuneLeft = (float)p.Effects.ImmuneLeft,
                    SlowMagnitude = (float)p.Effects.SlowMagnitude,
                    ActiveBuff = buff,
                    BuffLeft = (float)p.BuffLeft,
                    BuffId = BuffIdOf(buff),
                    BlurLeft = (float)p.BlurLeft,
                    RevealedOutlineLeft = (float)p.RevealedOutlineLeft,
                    RevealAlertLeft = (float)p.RevealAlertLeft,
                    KnownRevealAlert = p.KnownRevealAlert,
                    Invisible = p.InvisibleLeft > 0.0,
                    OnStilts = p.StiltsLeft > 0.0,
                    InSackRace = p.SackRaceLeft > 0.0,
                    InLuksongBaka = p.LuksongBakaLeft > 0.0,
                    SpinningTrumpo = p.TrumpoSpinLeft > 0.0,
                    ShieldHp = (float)p.ShieldHp,
                    LangitProtected = p.LangitLeft > 0.0,
                    TayaCountLeft = (float)p.TayaCountLeft,
                    SyatoRecastReady = p.SyatoRecastLeft > 0.0 && p.SyatoTargetId >= 0,
                    EmpoweredShotReady = p.EmpoweredTiradorLeft > 0.0 && p.EmpoweredTiradorBonus > 0.0,

                    CastingWeaponId = castingWid,
                    CastWindupLeft = windupLeft,
                    CastLockLeft = (float)p.CastLockLeft,
                    HeldSlot = heldSlot,
                    AimHoldSeconds = heldSec,
                    PrimaryCooldown = (float)p.PrimaryCd,
                    PrimaryMaxCooldown = p.UsingSpecKit ? (float)rules.TiradorFireCooldown : (rules.PrimaryWeapon != null ? (float)rules.PrimaryWeapon.Cooldown : 0f),
                    DashCooldown = (float)p.DashCd,
                    DashMaxCooldown = (float)rules.DashCooldown,
                    JumpCooldown = (float)p.JumpCd,
                    SlideCooldown = (float)p.SlideCd,

                    CardsMode = p.Cards,
                    HandCount = handCount,
                    HandWeaponIds = slotCache.HandIds,
                    SlotCooldowns = slotCache.Cooldowns,
                    SlotMaxCooldowns = slotCache.MaxCooldowns,
                    SlotElixirCosts = slotCache.Costs,
                    SlotWaterCosts = slotCache.WaterCosts,
                    SlotCharges = slotCache.Charges,
                    SlotMaxCharges = slotCache.MaxCharges,
                    SlotAimCameras = slotCache.AimCameras,
                    NextWeaponId = p.NextCard ?? "",
                    ShuffleWeaponId = p.ShuffleCard ?? "",
                    ShuffleLeft = (float)p.ShuffleLeft,
                    ShuffleTotal = (float)p.ShuffleTotal,

                    InRescueZone = inOwnRescue,
                    NearEnemyBase = inEnemyBase,
                    CarryingFlag = inEnemyBase && sim.CaptureTeam == p.Team,
                    CastingBlocked = castBlocked,
                    TagOrder = p.TagOrder,
                    TagTime = (float)p.TagTime,
                });
            }

            RefreshWorldEffects(sim);
            RefreshSpecEntities(sim);
            RefreshObjectivesAndStands(sim);
        }

        static string pendingWid(WeaponDef def) => def.Id ?? "";

        static StatusKind DominantStatus(StatusEffects effects, out float timeLeft)
        {
            for (int i = 0; i < StatusPriority.Length; i++)
            {
                if (effects.Has(StatusPriority[i]))
                {
                    timeLeft = (float)effects.TimeLeft(StatusPriority[i]);
                    return (StatusKind)(int)StatusPriority[i];
                }
            }
            timeLeft = 0f;
            return StatusKind.None;
        }

        void RefreshWorldEffects(MatchSim sim)
        {
            _projectiles.Clear();
            _worldEffects.Clear();

            List<WeaponSystem.Projectile> projs = sim.Weapons.Projectiles;
            for (int i = 0; i < projs.Count; i++)
            {
                WeaponSystem.Projectile pr = projs[i];
                WeaponDef def = pr.Def;
                SkillDef sk = pr.Skill;
                WeaponShapeKind shape = sk != null ? ShapeForSkill(sk.Mechanic) : (def != null ? (WeaponShapeKind)(int)def.Shape : WeaponShapeKind.Skillshot);
                var snap = new WorldEffectSnapshot
                {
                    Id = pr.Id,
                    OwnerId = pr.OwnerId,
                    Team = pr.Team,
                    Kind = WorldEffectKind.Projectile,
                    Shape = shape,
                    WeaponId = pr.EffectId ?? "",
                    Look = def != null ? (def.Look ?? "") : "",
                    Position = new Vector2(pr.Position.X, pr.Position.Y),
                    Direction = new Vector2(pr.Direction.X, pr.Direction.Y),
                    Radius = sk != null ? 0.30f : (def != null ? (float)Math.Max(def.ProjectileRadius, def.Radius) : 0.25f),
                    Travelled = (float)pr.Travelled,
                    MaxRange = (float)pr.EffectiveMaxRange,
                    Returning = pr.Returning,
                    Bounces = pr.Bounces,
                };
                _projectiles.Add(snap);
                _worldEffects.Add(snap);
            }

            List<WeaponSystem.Zone> zones = sim.Weapons.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                WeaponSystem.Zone z = zones[i];
                WeaponDef def = z.Def;
                WorldEffectKind kind;
                switch (z.Kind)
                {
                    case WeaponSystem.ZoneKind.Explosion:
                        kind = WorldEffectKind.ExplosionZone;
                        break;
                    case WeaponSystem.ZoneKind.Trap:
                        kind = z.Timer > 0.0 ? WorldEffectKind.TrapArming : WorldEffectKind.TrapArmed;
                        break;
                    case WeaponSystem.ZoneKind.SlowZone:
                        kind = WorldEffectKind.SlowZone;
                        break;
                    default:
                        kind = WorldEffectKind.FieldZone;
                        break;
                }
                _worldEffects.Add(new WorldEffectSnapshot
                {
                    Id = z.Id,
                    OwnerId = z.OwnerId,
                    Team = z.Team,
                    Kind = kind,
                    Shape = def != null ? (WeaponShapeKind)(int)def.Shape : WeaponShapeKind.GroundAoe,
                    WeaponId = def != null ? (def.Id ?? "") : "",
                    Look = def != null ? (def.Look ?? "") : "",
                    Position = new Vector2(z.Center.X, z.Center.Y),
                    Radius = (float)z.Radius,
                    TimeLeft = (float)z.TimeLeft,
                    Duration = def != null ? (float)Math.Max(def.Duration, def.Lifetime) : (float)z.TimeLeft,
                });
            }

            List<WeaponSystem.Shield> shields = sim.Weapons.Shields;
            for (int i = 0; i < shields.Count; i++)
            {
                WeaponSystem.Shield sh = shields[i];
                WeaponDef def = sh.Def;
                Vec2 fwd = sh.Forward();
                Vec2 endA = sh.Center - sh.Along * sh.HalfWidth;
                Vec2 endB = sh.Center + sh.Along * sh.HalfWidth;
                _worldEffects.Add(new WorldEffectSnapshot
                {
                    Id = sh.Id,
                    OwnerId = -1,
                    Team = sh.Team,
                    Kind = WorldEffectKind.ShieldWall,
                    Shape = WeaponShapeKind.Shield,
                    WeaponId = def != null ? (def.Id ?? "") : "papel_shield",
                    Look = def != null ? (def.Look ?? "") : "",
                    Position = new Vector2(sh.Center.X, sh.Center.Y),
                    Direction = new Vector2(fwd.X, fwd.Y),
                    EndPosition = new Vector2(endB.X - endA.X, endB.Y - endA.Y),
                    HalfWidth = (float)sh.HalfWidth,
                    TimeLeft = (float)sh.TimeLeft,
                    Duration = def != null ? (float)def.Duration : (float)sh.TimeLeft,
                });
            }

            IReadOnlyList<WeaponSystem.PendingCone> cones = sim.Weapons.PendingCones;
            for (int i = 0; i < cones.Count; i++)
            {
                WeaponSystem.PendingCone pc = cones[i];
                WeaponDef def = pc.Def;
                Vec2 origin = sim.Players.TryGetValue(pc.OwnerId, out PlayerState owner) ? owner.Position : Vec2.Zero;
                int ownerTeam = owner != null ? owner.Team : 0;
                _worldEffects.Add(new WorldEffectSnapshot
                {
                    Id = -(i + 1),
                    OwnerId = pc.OwnerId,
                    Team = ownerTeam,
                    Kind = WorldEffectKind.PendingCone,
                    Shape = WeaponShapeKind.Cone,
                    WeaponId = def != null ? (def.Id ?? "") : "",
                    Look = def != null ? (def.Look ?? "") : "",
                    Position = new Vector2(origin.X, origin.Y),
                    Direction = new Vector2(pc.Direction.X, pc.Direction.Y),
                    Radius = def != null ? (float)def.Radius : 0f,
                    AngleDegrees = def != null ? (float)def.AngleDegrees : 0f,
                    TimeLeft = (float)pc.TimeLeft,
                    Duration = def != null ? (float)def.Delay : (float)pc.TimeLeft,
                });
            }
        }

        void RefreshSpecEntities(MatchSim sim)
        {
            int localTeam = LocalPlayerTeam;

            _walls.Clear();
            List<WeaponSystem.DestructibleWall> walls = sim.Weapons.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WeaponSystem.DestructibleWall w = walls[i];
                _walls.Add(new WallSnapshot
                {
                    Id = w.Id,
                    OwnerId = w.OwnerId,
                    Team = w.Team,
                    SkillId = w.SkillId ?? "",
                    Center = new Vector2(w.Center.X, w.Center.Y),
                    Along = new Vector2(w.Along.X, w.Along.Y),
                    HalfWidth = (float)w.HalfWidth,
                    Hp = w.Hp,
                    MaxHp = w.MaxHp,
                    TimeLeft = (float)w.TimeLeft,
                    Duration = (float)w.Duration,
                });
            }

            _traps.Clear();
            List<WeaponSystem.HiddenTrap> traps = sim.Weapons.Traps;
            for (int i = 0; i < traps.Count; i++)
            {
                WeaponSystem.HiddenTrap t = traps[i];
                _traps.Add(new TrapSnapshot
                {
                    Id = t.Id,
                    OwnerId = t.OwnerId,
                    Team = t.Team,
                    SkillId = t.SkillId ?? "",
                    Position = new Vector2(t.Position.X, t.Position.Y),
                    TriggerRadius = (float)t.TriggerRadius,
                    Armed = t.Armed,
                    VisibleToLocal = sim.IsTrapVisibleToTeam(t, localTeam),
                    Revealed = t.RevealedToEnemy,
                });
            }

            _sentries.Clear();
            List<WeaponSystem.Sentry> sentries = sim.Weapons.Sentries;
            for (int i = 0; i < sentries.Count; i++)
            {
                WeaponSystem.Sentry s = sentries[i];
                float interval = s.PingInterval > 0.0 ? (float)s.PingInterval : 1f;
                float pulse01 = Math.Clamp(1f - (float)s.PingTimer / interval, 0f, 1f);
                _sentries.Add(new SentrySnapshot
                {
                    Id = s.Id,
                    OwnerId = s.OwnerId,
                    Team = s.Team,
                    SkillId = s.SkillId ?? "",
                    Position = new Vector2(s.Position.X, s.Position.Y),
                    Radius = (float)s.Radius,
                    TimeLeft = (float)s.TimeLeft,
                    Duration = (float)s.Duration,
                    PingPulse01 = pulse01,
                });
            }

            _tethers.Clear();
            List<WeaponSystem.AllyTether> tethers = sim.Weapons.Tethers;
            for (int i = 0; i < tethers.Count; i++)
            {
                WeaponSystem.AllyTether te = tethers[i];
                Vec2 op = sim.Players.TryGetValue(te.OwnerId, out PlayerState owner) ? owner.Position : Vec2.Zero;
                Vec2 ap = sim.Players.TryGetValue(te.AllyId, out PlayerState ally) ? ally.Position : Vec2.Zero;
                float stretch01 = te.MaxRange > 0.0 ? Math.Clamp((float)(op.DistanceTo(ap) / te.MaxRange), 0f, 1f) : 0f;
                _tethers.Add(new TetherSnapshot
                {
                    Id = te.Id,
                    OwnerId = te.OwnerId,
                    AllyId = te.AllyId,
                    Team = te.Team,
                    SkillId = te.SkillId ?? "",
                    OwnerPos = new Vector2(op.X, op.Y),
                    AllyPos = new Vector2(ap.X, ap.Y),
                    MaxRange = (float)te.MaxRange,
                    Stretch01 = stretch01,
                    TimeLeft = (float)te.TimeLeft,
                });
            }

            _skillZones.Clear();
            List<WeaponSystem.SkillZone> szones = sim.Weapons.SkillZones;
            for (int i = 0; i < szones.Count; i++)
            {
                WeaponSystem.SkillZone z = szones[i];
                _skillZones.Add(new SkillZoneSnapshot
                {
                    Id = z.Id,
                    OwnerId = z.OwnerId,
                    Team = z.Team,
                    SkillId = z.SkillId ?? "",
                    Kind = (SkillZoneVisualKind)(int)z.Kind,
                    Position = new Vector2(z.Position.X, z.Position.Y),
                    EndPosition = new Vector2(z.EndPosition.X, z.EndPosition.Y),
                    Direction = new Vector2(z.Direction.X, z.Direction.Y),
                    Radius = (float)z.Radius,
                    HalfWidth = (float)z.HalfWidth,
                    AngleDegrees = (float)z.AngleDegrees,
                    Count = z.Count,
                    Spacing = (float)z.Spacing,
                    TimeLeft = (float)z.TimeLeft,
                    Duration = (float)z.Duration,
                    DelayLeft = (float)z.DelayLeft,
                });
            }

            _revealMarkers.Clear();
            List<WeaponSystem.RevealMarker> rmarks = sim.Weapons.RevealMarkers;
            for (int i = 0; i < rmarks.Count; i++)
            {
                WeaponSystem.RevealMarker m = rmarks[i];
                if (m.Team != localTeam) continue;
                _revealMarkers.Add(new RevealMarkerSnapshot
                {
                    Id = m.Id,
                    TargetId = m.TargetId,
                    Team = m.Team,
                    SkillId = m.SkillId ?? "",
                    Position = new Vector2(m.Position.X, m.Position.Y),
                    TimeLeft = (float)m.TimeLeft,
                    Duration = (float)m.Duration,
                });
            }
        }

        void RefreshObjectivesAndStands(MatchSim sim)
        {
            _meriendaStands.Clear();
            float respawnTotal = (float)sim.Rules.PowerupRespawnSeconds;
            for (int i = 0; i < sim.Powerups.Count; i++)
            {
                MatchSim.Powerup p = sim.Powerups[i];
                BuffKind item = (BuffKind)p.Kind;
                _meriendaStands.Add(new MeriendaStandSnapshot
                {
                    Id = p.Id,
                    Item = item,
                    ItemId = BuffIdOf(item),
                    Position = new Vector2(p.Pos.X, p.Pos.Y),
                    Active = p.Active,
                    RespawnLeft = (float)p.RespawnLeft,
                    RespawnTotal = respawnTotal,
                });
            }

            int attackers0 = sim.CapturersInEnemyBase(0);
            int attackers1 = sim.CapturersInEnemyBase(1);

            for (int team = 0; team < 2; team++)
            {
                int side = sim.SideForTeam(team);
                Vec2 center = sim.Layout.BaseCenter(side);
                Vec2 post = sim.Layout.PostPosition(side);
                int attackingTeam = 1 - team;
                int holders = attackingTeam == 0 ? attackers0 : attackers1;
                bool capturingThisBase = sim.CaptureTeam == attackingTeam &&
                                         sim.CurrentCaptureKind == MatchSim.CaptureKind.Base;
                float elapsed = capturingThisBase ? (float)sim.CaptureElapsed : 0f;
                float needed = capturingThisBase ? (float)sim.CaptureNeeded : (float)sim.CurrentBaseCaptureTime();
                float prog = (capturingThisBase && needed > 0f) ? Math.Clamp(elapsed / needed, 0f, 1f) : 0f;

                _bases[team] = new BaseObjectiveSnapshot
                {
                    Team = team,
                    Center = new Vector2(center.X, center.Y),
                    PostPosition = new Vector2(post.X, post.Y),
                    Radius = (float)sim.Layout.BaseRadius,
                    CaptureProgress01 = prog,
                    CaptureElapsed = elapsed,
                    CaptureNeeded = needed,
                    CaptureTimeLeft = capturingThisBase ? (float)sim.CaptureTimeLeft() : 0f,
                    CapturingTeam = capturingThisBase ? attackingTeam : -1,
                    Holders = holders,
                    // docs/SPEC.md §5: a defender in its own base pauses the capture (the bar stays, at rate 0)
                    Contested = capturingThisBase && holders > 0 && sim.CaptureRate <= 0.0,
                };

                Rect2 rRect = sim.Layout.RescueRect(sim.JailSideFor(team));
                Vec2 rCenter = rRect.GetCenter();
                int waiting = 0;
                foreach (var pair in sim.Players)
                    if (pair.Value.Team == team && pair.Value.Tagged) waiting++;

                float holdNeeded = (float)sim.Rules.RescueHoldTime;
                float holdElapsed = (float)sim.RescueElapsed[team];
                float cdLeft = (float)sim.RescueReadyLeft[team];

                _rescues[team] = new RescueObjectiveSnapshot
                {
                    Team = team,
                    Zone = new GroundRect(rRect.Position.X, rRect.Position.Y, rRect.Size.X, rRect.Size.Y),
                    Center = new Vector2(rCenter.X, rCenter.Y),
                    RescuerId = sim.RescueRescuer[team],
                    WaitingCount = waiting,
                    HoldProgress01 = holdNeeded > 0f ? Math.Clamp(holdElapsed / holdNeeded, 0f, 1f) : 0f,
                    HoldElapsed = holdElapsed,
                    HoldNeeded = holdNeeded,
                    CooldownLeft = cdLeft,
                    CooldownTotal = (float)sim.Rules.RescueCooldown,
                    Ready = cdLeft <= 0f,
                };
            }
        }

        /// <summary>Backwards-compatible input setter (`move` in sim XZ, `buttons` bitmask).</summary>
        public void SetLocalInput(Vector2 move, int buttons)
        {
            _localInput = new LocalInputFrame
            {
                Move = move,
                Buttons = buttons,
                HasMoveTarget = (buttons & InputButtons.MoveTarget) != 0,
                HasAimPoint = (buttons & InputButtons.AimPoint) != 0,
            };
        }

        /// <summary>Full combat input setter with aim vector and optional world ground targets.</summary>
        public void SetLocalInput(Vector2 move, Vector2 aim, int buttons, Vector2 moveTarget = default, Vector2 aimPoint = default)
        {
            _localInput = new LocalInputFrame
            {
                Move = move,
                Aim = aim,
                Buttons = buttons,
                MoveTarget = moveTarget,
                AimPoint = aimPoint,
                HasMoveTarget = (buttons & InputButtons.MoveTarget) != 0,
                HasAimPoint = (buttons & InputButtons.AimPoint) != 0,
            };
        }

        public void SetLocalInput(LocalInputFrame frame)
        {
            _localInput = frame;
        }

        public KidSnapshot? LocalKid => Find(LocalPlayerId);

        public KidSnapshot? Find(int id)
        {
            for (int i = 0; i < _kids.Count; i++)
                if (_kids[i].Id == id) return _kids[i];
            return null;
        }

        public WeaponInfo? GetWeaponInfo(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            return _weaponLookup.TryGetValue(weaponId, out WeaponInfo info) ? info : (WeaponInfo?)null;
        }

        public static string BuffIdOf(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Palamig: return "palamig";
                case BuffKind.Teks: return "teks";
                case BuffKind.Turon: return "turon";
                default: return "";
            }
        }

        float MeasureSpeed01(int id, Vector2 position)
        {
            float speed = 0f;
            if (_lastPositions.TryGetValue(id, out Vector2 last) && _runPerTick > 0f)
                speed = Math.Clamp(Vector2.Distance(position, last) / _runPerTick, 0f, 1f);
            _lastPositions[id] = position;
            return speed;
        }
    }
}
