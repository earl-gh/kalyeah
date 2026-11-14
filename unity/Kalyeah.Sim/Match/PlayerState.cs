using System.Collections.Generic;

namespace Kalyeah.Sim
{
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
        /// <summary>Cosmetic only (CharacterDef id).</summary>
        public string CharacterId = "";
        /// <summary>The hand / loadout (an empty id = an empty slot).</summary>
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
        public bool Walking;
        public bool Crouching;
        public double AirY;
        public double VelY;
        public double JumpCd;
        public double SlideLeft;
        public double SlideCd;
        public Vec2 SlideDir = Vec2.Zero;
        public int BuffType;
        public double BuffLeft;
        public double BuffRegenCarry;

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

        /// <summary>GDScript Array.resize: grows with the default value, shrinks from the end.</summary>
        public static void Resize<T>(List<T> list, int count, T fill)
        {
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            while (list.Count < count) list.Add(fill);
        }
    }
}
