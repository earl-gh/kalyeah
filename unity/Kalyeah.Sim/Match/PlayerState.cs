using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>Authoritative hit-confirm event payload for electric combat feedback (body, headshot, TAYA! tag).</summary>
    public struct HitConfirmEvent
    {
        public int Tick;
        public int AttackerId;
        public int TargetId;
        public string WeaponId;
        public int Damage;
        public bool IsHeadshot;
        public bool IsTag;
        public Vec2 WorldPos;
    }

    /// <summary>Per-player simulation state (port of player_state.gd). Plain data; MatchSim owns and mutates it.</summary>
    public sealed class PlayerState
    {
        public const int BuffNone = 0;
        public const int BuffPalamig = 1;
        public const int BuffTeks = 2;
        public const int BuffTuron = 3;

        public int Id;
        public int Team;
        /// <summary>Position in the XZ plane (x, z). +Z is the own-base end.</summary>
        public Vec2 Position = Vec2.Zero;
        /// <summary>Last non-zero move direction.</summary>
        public Vec2 Facing = Vec2.Zero;
        public int Hp;
        /// <summary>At 0 HP a player is tagged: they wait on the sidewalk until a teammate rescues them.</summary>
        public bool Tagged;
        public double TagTime;
        public double Radius;
        public Vec2 SpawnPosition = Vec2.Zero;
        /// <summary>Rooted by a heavy attack's cast animation.</summary>
        public double CastLockLeft;
        /// <summary>CharacterDef id (also identifies this kid's 3-skill spec kit).</summary>
        public string CharacterId = "";
        /// <summary>The hand / loadout (an empty id = an empty slot). Under spec kits: [S1, S2, ULT].</summary>
        public List<string> Weapons = new List<string>();
        public List<double> WeaponCooldowns = new List<double> { 0.0, 0.0 };
        public bool Cards;
        public List<string> Deck = new List<string>();
        public string NextCard = "";
        public string ShuffleCard = "";
        public double ShuffleLeft;
        public double ShuffleTotal;
        public List<string> Incoming = new List<string>();
        /// <summary>Seconds each weapon button has been held; -1 when not held.</summary>
        public List<double> AimHold = new List<double> { -1.0, -1.0 };
        /// <summary>Buttons from the previous tick, to see presses and releases.</summary>
        public int PreviousButtons;
        public List<Vec2> AimLock = new List<Vec2> { Vec2.Zero, Vec2.Zero };
        public List<int> AimTarget = new List<int> { -1, -1 };
        public StatusEffects Effects = new StatusEffects();
        public double Elixir;
        public double Stamina;
        public bool Overclocked;
        public double ExhaustLeft;
        public int TagOrder;
        /// <summary>Who tagged this kid (-1: nobody); a revenge release follows when that player is tagged.</summary>
        public int TaggedBy = -1;
        /// <summary>Seconds until a revenge release (0: none pending).</summary>
        public double RevengeLeft;
        /// <summary>Seconds of the revenge shield left: all damage is ignored.</summary>
        public double ShieldLeft;
        public double RunTime;
        public bool Trips = true;
        public double StillTime;
        public bool HasStamina = true;
        public Vec2 MoveTarget = Vec2.Zero;
        public bool HasMoveTarget;
        public double StuckTime;
        public double PrimaryCd;
        public double DashCd;
        public double DashLeft;
        public Vec2 DashDir = Vec2.Zero;
        public double UltCharge;
        public bool Sprinting;
        public double OutOfCombatTime;
        /// <summary>Seconds of steady jogging (spec rules): running starts at `sprint_delay`; stopping, walking, crouching, a sharp
        /// turn or a cast reset it.</summary>
        public double JogTime;
        public bool Walking;
        public bool Crouching;
        /// <summary>Aiming down sight this tick (the ADS button held, outside street modes).</summary>
        public bool AimingDown;
        public double AirY;
        public double VelY;
        public double JumpCd;
        public double SlideLeft;
        public double SlideCd;
        public Vec2 SlideDir = Vec2.Zero;
        public int BuffType;
        public double BuffLeft;
        public double BuffRegenCarry;

        // ---- docs/KITS.md + docs/SPEC.md §7 spec combat state ----
        /// <summary>True when this kid is equipped with its 3-skill spec kit [S1, S2, ULT].</summary>
        public bool UsingSpecKit;
        /// <summary>Water mana (0..100): S1 &amp; S2 spend water; ULT costs 0.</summary>
        public double Water = 100.0;
        /// <summary>True while standing in own home base fountain (fast 25/s water refill).</summary>
        public bool InHomeFountain;
        /// <summary>Remaining stones in the 7-stone Tirador magazine.</summary>
        public int TiradorAmmo = 7;
        /// <summary>Seconds left on Tirador reload (0: ready to shoot).</summary>
        public double TiradorReloadLeft;
        /// <summary>Seconds Tirador has been pulled back (-1: not charging; 0..1s scales range 18m..30m).</summary>
        public double TiradorChargeTime = -1.0;
        /// <summary>Current charges for S1 (e.g. Narda's 2-charge Luksong Tinik).</summary>
        public int S1Charges = 1;
        /// <summary>Seconds until the next S1 charge regenerates.</summary>
        public double S1ChargeRegenLeft;
        /// <summary>Jumong S1 Syato marked target id (-1: none) and remaining recast window.</summary>
        public int SyatoTargetId = -1;
        public double SyatoRecastLeft;
        /// <summary>Next Tirador shot bonus damage ratio (e.g. +0.50 Luksong Tinik, +0.40 Tago!) and window.</summary>
        public double EmpoweredTiradorBonus;
        public double EmpoweredTiradorLeft;
        /// <summary>Seconds of edge-blur remaining (Jumong Pukpok Palayok, Manoy Sili-Tubig).</summary>
        public double BlurLeft;
        /// <summary>Seconds this kid glows with an outline through walls for the enemy team.</summary>
        public double RevealedOutlineLeft;
        /// <summary>Seconds the revealed alert ("eye" icon) stays active on this kid.</summary>
        public double RevealAlertLeft;
        /// <summary>True when the current reveal alerts the revealed kid (false for Karding's silent Piko).</summary>
        public bool KnownRevealAlert;
        /// <summary>Seconds of invisibility remaining (Kikay S2 Tago!); breaks on attack, rescue, or capture.</summary>
        public double InvisibleLeft;
        /// <summary>Seconds on stilts (Inggo S2 Tiyakad: +40% Tirador range, -20% speed, sees over cover &amp; elevated).</summary>
        public double StiltsLeft;
        /// <summary>Seconds inside Sack Race (Paeng S1: 40% DR + bump knockback).</summary>
        public double SackRaceLeft;
        /// <summary>Seconds crouching as Luksong Baka (Paeng S2: allies vault 10m off Paeng).</summary>
        public double LuksongBakaLeft;
        /// <summary>Seconds spinning as Trumpo Ikot (Jumong S2: AoE dmg + slow immune).</summary>
        public double TrumpoSpinLeft;
        public double TrumpoSpinTickTimer;
        /// <summary>Seconds storing damage as Sungka shells (Oriang S2) + total damage stored.</summary>
        public double SungkaAbsorbLeft;
        public int SungkaStoredDamage;
        /// <summary>Temporary shield HP (from Oriang S2 Sungka release) absorbing damage before real HP.</summary>
        public double ShieldHp;
        public double ShieldHpLeft;
        /// <summary>Seconds of Langit! protection (Oriang ULT: allies in 8m cannot drop below 1 HP).</summary>
        public double LangitLeft;
        /// <summary>Seconds remaining on Manoy ULT Taya! count-to-3 before map-wide snapshot reveal.</summary>
        public double TayaCountLeft;
        /// <summary>Temporary movement speed bonus ratio (e.g. Oriang Chinese Garter snap) and duration.</summary>
        public double SpeedBoostRatio;
        public double SpeedBoostLeft;
        /// <summary>Out-of-combat passive healing fractional accumulator (5 HP/s after 5 s).</summary>
        public double PassiveHealCarry;
        /// <summary>True when standing on an elevated spot (safe from ground-only Domeng ULT Lupa!).</summary>
        public bool IsElevated;
        /// <summary>Deferred slow triggered after Tere ULT Garter Net root finishes.</summary>
        public double PendingSlowAfterRootLeft;
        public double PendingSlowAfterRootDuration;
        public double PendingSlowAfterRootMagnitude;
        /// <summary>True while vault-dashing (Luksong Tinik / Luksong Baka): clears waist-high cover.</summary>
        public bool VaultDashActive;
        /// <summary>Jumong ULT Pukpok Palayok leap state.</summary>
        public double LeapLeft;
        public double LeapDuration;
        public Vec2 LeapStart = Vec2.Zero;
        public Vec2 LeapTarget = Vec2.Zero;
        public string LeapSkillId = "";
        /// <summary>Inggo ULT Bato! 1s windup state.</summary>
        public double BatoWindupLeft;
        public Vec2 BatoWindupAim = Vec2.Zero;
        /// <summary>Narda ULT Tsinelas Bagsik mark for 50% ULT CD refund on tag.</summary>
        public double TsinelasMarkedLeft;
        public int TsinelasMarkedBy = -1;
        /// <summary>Bitmasks for per-cast bump/vault deduplication (zero allocations).</summary>
        public int SackBumpMask;
        public int LuksongBakaUsedMask;
        /// <summary>Most recent authoritative hit landed by this kid.</summary>
        public HitConfirmEvent LastHitEvent;

        /// <summary>One cooldown, hold timer and aim lock per hand slot; a slot that is new is idle.</summary>
        public void ResizeSlots()
        {
            int count = Weapons.Count;
            int had = AimHold.Count;
            Resize(WeaponCooldowns, count, 0.0);
            Resize(AimHold, count, 0.0);
            Resize(AimLock, count, Vec2.Zero);
            Resize(AimTarget, count, 0);
            for (int slot = had; slot < count; slot++)
            {
                WeaponCooldowns[slot] = 0.0;
                AimHold[slot] = -1.0;
                AimLock[slot] = Vec2.Zero;
                AimTarget[slot] = -1;
            }
        }

        /// <summary>Not tagged and not under a status that stops casting.</summary>
        public bool CanAct() => !Tagged && Effects.CanCast();

        /// <summary>True when this kid is above the street floor (jumping, on Tiyakad stilts, or on raised cover).</summary>
        public bool IsOffGround() => AirY >= MatchSim.JumpClearHeight || StiltsLeft > 0.0 || IsElevated;

        /// <summary>GDScript Array.resize: grows with the default value, shrinks from the end.</summary>
        public static void Resize<T>(List<T> list, int count, T fill)
        {
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            while (list.Count < count) list.Add(fill);
        }
    }
}
