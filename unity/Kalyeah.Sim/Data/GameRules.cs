using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Global gameplay numbers (docs/GDD.md "Core"), port of game_rules.gd. Values come from data/rules/game_rules.tres
    /// (unity/Data/rules__game_rules.json); the defaults below are the GDScript ones.
    /// </summary>
    [Serializable]
    public sealed class GameRules
    {
        public int TickRate;
        public int PlayerMaxHp;
        public double MoveSpeed;
        public double PlayerRadius;
        public double SpawnSpacing;
        public bool RescueFreesAll;
        public double RescueHoldTime = 5.0;
        public double RescueCooldown = 10.0;
        public bool NoCastInRescueArea = true;
        public bool NoCastInEnemyBase = true;
        public double DummyReleaseTime;
        public List<WeaponDef> Weapons = new List<WeaponDef>();
        public int LoadoutSize = 3;
        public WeaponDef PrimaryWeapon;
        public WeaponDef UltimateWeapon;
        public bool SprintEnabled;
        /// <summary>Seconds before running starts: of steady jogging under the spec rules (docs/SPEC.md §7), else out of combat.</summary>
        public double SprintDelay = 1.2;
        /// <summary>Spec walk speed (m/s) with the stick partly pushed; 0 keeps the old speed in proportion to the stick.</summary>
        public double WalkSpeed;
        public double SprintMultiplier = 1.32;
        public double WalkRunThreshold = 0.62;
        public double CrouchSpeedShare = 0.55;
        /// <summary>Speed share while aiming down sight (docs/SPEC.md §7: 30% slower).</summary>
        public double AdsSpeedShare = 0.7;
        public double JumpVelocity = 5.6;
        public double JumpGravity = 16.5;
        public double JumpCooldown = 0.25;
        public double SlideSpeed = 10.2;
        public double SlideDuration = 0.46;
        public double SlideCooldown = 3.2;
        /// <summary>The highest low cover a kid vaults with the jump button (docs/SPEC.md §7); 0 turns vaulting off.</summary>
        public double VaultMaxHeight;
        /// <summary>Seconds a vault takes (docs/SPEC.md §7: 0.4 s).</summary>
        public double VaultDuration;
        /// <summary>How close (metres from the kid's edge) a low cover must be ahead for the jump button to vault it.</summary>
        public double VaultReach;
        public double DashSpeed = 18.5;
        public double DashDuration = 0.24;
        public double DashCooldown = 4.2;
        public double PowerupPickupRadius = 1.65;
        public double PowerupRespawnSeconds = 25.0;
        public int PalamigHeal = 18;
        public double PalamigSpeedBonus = 0.22;
        public double PalamigDuration = 10.0;
        public double TeksDamageBonus = 0.20;
        public double TeksUltBonus = 25.0;
        public double TeksDuration = 12.0;
        public double TuronDamageReduction = 0.25;
        public double TuronRegenPerS = 4.0;
        public double TuronDuration = 10.0;
        public double UltPassivePerS = 2.2;
        public double UltPerDamage = 0.45;
        public List<CharacterDef> Characters = new List<CharacterDef>();
        public double WeaponPickTime;
        public double PolymorphSpeedScale;
        public double AimDeadzone;
        public double CcImmunity;
        public double BoomerangCatchDistance;
        /// <summary>
        /// docs/SPEC.md §5 Agawang Base: tagged kids wait in the enemy jail, rescues work like a defuse (moving, casting or a stun
        /// cancels), a defender in its base pauses the enemy capture, the jail is ready at round start and a same-tick double
        /// wipe replays the round. False keeps the pre-spec rules the recorded goldens play.
        /// </summary>
        public bool SpecRules;
        /// <summary>
        /// docs/KITS.md + docs/SPEC.md §7: Water mana, 7-shot Tirador slingshot with hold-to-charge range &amp; headshots,
        /// and 12 kid-specific 3-skill kits. Gated via <see cref="UsesSpecKits"/> so recorded goldens (SpecRules=false) stay exact.
        /// </summary>
        public bool SpecKitsEnabled;
        public double WaterMax = 100.0;
        public double WaterRegenPerS = 2.0;
        public double WaterFountainRegenPerS = 25.0;
        public int TiradorMaxAmmo = 7;
        public double TiradorReloadSeconds = 1.5;
        public double TiradorFireCooldown = 0.4;
        public int TiradorBodyDamage = 20;
        public double TiradorHeadshotMultiplier = 1.5;
        public double TiradorMinRange = 18.0;
        public double TiradorMaxRange = 30.0;
        public double TiradorChargeSeconds = 1.0;
        public double TiradorHeadshotRadius = 0.22;
        public double TiradorProjectileSpeed = 36.0;
        public double TrapRevealDistance = 3.0;
        public int TrapMaxPerPlayer = 2;
        public double PassiveHealDelay = 5.0;
        public double PassiveHealPerS = 5.0;
        /// <summary>Seconds into a rescue a cancel keeps once passed (0: none).</summary>
        public double RescueCheckpoint;
        /// <summary>How far (metres) a rescuer may drift before the rescue counts as moved.</summary>
        public double RescueStillTolerance = 0.05;
        /// <summary>Seconds after its tagger is tagged before a tagged kid walks free (0: no revenge release).</summary>
        public double RevengeReleaseSeconds;
        /// <summary>Seconds a revenge-released kid ignores all damage.</summary>
        public double RevengeShieldSeconds;
        /// <summary>With nobody capturing, the bar drains at this share of the fill speed instead of resetting (0: resets).</summary>
        public double CaptureDrainShare;
        /// <summary>Seconds at each round start when kids stay inside their own base and can't cast (0: none).</summary>
        public double BarrierSeconds;
        public double CaptureSoloTime = 12.0;
        public double CaptureAllyBonus = 0.75;
        public double OvertimeAfter;
        public double ExtendedSeconds;
        public double OvertimeSeconds;
        public double OvertimeCaptureTime;
        public double IntroTime;
        public double BriefingSeconds = 10.0;
        public double SfxLevel = 1.0;
        public double BgmVolume = 0.4;
        public double PointFreezeTime;
        public bool CardCycle = true;
        public int HandSize = 4;
        public bool ElixirEnabled = true;
        public double ElixirMax = 10.0;
        public double ElixirStart = 5.0;
        public bool ElixirResetOnRound = true;
        public double ElixirRegenPerS = 0.36;
        public bool StaminaEnabled = true;
        public double StaminaMax = 100.0;
        public double StaminaDrainPerS = 10.0;
        public double StaminaRegenPerS = 25.0;
        public double StaminaRegenDelay = 0.5;
        public double ExhaustSeconds = 2.0;
        public double OverclockSpeedShare = 0.5;
        public double TripChance = 0.12;
        public double TripInterval = 6.0;
        public double TripSeconds = 1.05;
        public double MoveArrivalRadius = 0.1;
        public double MoveStuckShare = 0.25;
        public double MoveStuckTime = 0.2;
        public bool MoveFollowHold = true;
        public double CardTapTime = 0.3;
        public double SelfCastRadius = 1.0;
        public double AimNoDirectionRadius = 0.1;
        public int BotBaseHolders = 1;
        public int FirstTo;
        public List<int> FirstToChoices = new List<int>();
        public double DummyZ;
        public double DummyStandX;
        public double DummyPatrolX;
        public double DummyPatrolRange;
        public double DummyPatrolSpeedScale;
        public double DummyAllyX;
        public double DummyAllyZ;
        public int PracticeFighters;
        public double BotAggroRange;
        public double BotCastGap;
        public double BotAimErrorDegrees;
        public double BotStandoffShare;
        public double BotSkill;
        public double BotAssaultDelay;
        public double BotAssaultWindow;
        public double BotRestTime;
        public double BotRescueAggroRange;
        public List<BotLevel> BotLevels = new List<BotLevel>();
        public string BotLevelDefault = "medium";

        /// <summary>The difficulty `id` (the default one when it is unknown), or null when the rules have none.</summary>
        public BotLevel GetBotLevel(string id)
        {
            BotLevel fallback = null;
            foreach (BotLevel level in BotLevels)
            {
                if (level.Id == id) return level;
                if (level.Id == BotLevelDefault) fallback = level;
            }
            return fallback;
        }

        /// <summary>These rules with the bot numbers of difficulty `id` (a copy: the shared rules are never changed).</summary>
        public GameRules WithBotLevel(string id)
        {
            BotLevel level = GetBotLevel(id);
            GameRules copy = Duplicate();
            if (level == null) return copy;
            copy.BotSkill = level.Skill;
            copy.BotAimErrorDegrees = level.AimErrorDegrees;
            copy.BotCastGap = level.CastGap;
            copy.BotAggroRange = level.AggroRange;
            copy.BotAssaultDelay = level.AssaultDelay;
            copy.BotAssaultWindow = level.AssaultWindow;
            copy.BotRestTime = level.RestTime;
            return copy;
        }

        /// <summary>A shallow copy (Godot's Resource.duplicate(): the arrays are new, the weapons and characters are shared).</summary>
        public GameRules Duplicate()
        {
            var copy = (GameRules)MemberwiseClone();
            copy.Weapons = new List<WeaponDef>(Weapons);
            copy.Characters = new List<CharacterDef>(Characters);
            copy.FirstToChoices = new List<int>(FirstToChoices);
            copy.BotLevels = new List<BotLevel>(BotLevels);
            return copy;
        }

        /// <summary>Fixed simulation step in seconds (1 / tick_rate).</summary>
        public double TickDt() => 1.0 / TickRate;

        /// <summary>True when cards are paid for with elixir (cards on and elixir on).</summary>
        public bool UsesElixir() => CardCycle && ElixirEnabled;

        /// <summary>True when docs/KITS.md spec kits, Water mana, and 7-shot Tirador are active.</summary>
        public bool UsesSpecKits() => SpecRules && SpecKitsEnabled;
    }
}
