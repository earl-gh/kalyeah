using System;

namespace Kalyeah.Sim
{
    /// <summary>Which of a kid's 3 fixed skill slots a SkillDef occupies (docs/KITS.md).</summary>
    public enum SkillSlot { S1 = 0, S2 = 1, Ult = 2 }

    /// <summary>Camera framing while drag-aiming a skill (docs/KITS.md: Normal, ADS, ZoomOut, MapCast).</summary>
    public enum AimCameraMode { Normal = 0, Ads = 1, ZoomOut = 2, MapCast = 3 }

    /// <summary>Vision reveal type produced by a Seeker skill (docs/KITS.md: Outline vs one-time Snapshot).</summary>
    public enum RevealKind { None = 0, Outline = 1, Snapshot = 2 }

    /// <summary>Exact mechanic executed by each of the 36 approved kid skills in docs/KITS.md.</summary>
    public enum SkillMechanic
    {
        None = 0,
        // Strikers
        LuksongTinik = 1,
        TiraHolens = 2,
        TsinelasBagsik = 3,
        Syato = 4,
        TrumpoIkot = 5,
        PukpokPalayok = 6,
        Sipa = 7,
        Tiyakad = 8,
        BatoExecute = 9,
        // Defenders
        BahayBahayan = 10,
        PabitinHeal = 11,
        Palosebo = 12,
        ChineseGarter = 13,
        Sungka = 14,
        Langit = 15,
        SackRace = 16,
        LuksongBaka = 17,
        TinikWall = 18,
        // Seekers
        TeksTripwire = 19,
        Tago = 20,
        Saranggola = 21,
        Yoyo = 22,
        Piko = 23,
        PogsSlam = 24,
        SiliTubig = 25,
        TrumpoBantay = 26,
        Taya = 27,
        // Controllers
        Jackstone = 28,
        LuksongLubid = 29,
        GarterNet = 30,
        HolensSpill = 31,
        LataMorph = 32,
        PatinteroLines = 33,
        PabitinSnare = 34,
        SakoTrap = 35,
        LupaQuake = 36,
    }

    /// <summary>Data-driven definition for one kid skill (S1, S2 or ULT) loaded from unity/Data/characters__*.json.</summary>
    [Serializable]
    public sealed class SkillDef
    {
        public string Id = "";
        public string DisplayName = "";
        public string ShortName = "";
        public string Description = "";
        public string StreetGame = "";
        public SkillSlot Slot = SkillSlot.S1;
        public SkillMechanic Mechanic = SkillMechanic.None;
        public AimCameraMode AimCamera = AimCameraMode.Normal;
        public RevealKind RevealKind = RevealKind.None;
        public bool KnownRevealAlert;
        public int WaterCost;
        public double Cooldown;
        public int MaxCharges = 1;
        public double MaxRange;
        public double Radius;
        public double SecondaryRadius;
        public double Width;
        public double AngleDegrees;
        public int Damage;
        public int SecondaryDamage;
        public int Heal;
        public int WallHp;
        public double Duration;
        public double SecondaryDuration;
        public double Delay;
        public double Interval;
        public double Speed;
        public int Count = 1;
        public double EffectDuration;
        public double EffectMagnitude;
        public double BlurDuration;
        public double RevealDuration;
        public double RecastWindow;
        public double BonusRatio;
        public double SpeedDelta;
        public double Knockback;

        public bool IsUltimate => Slot == SkillSlot.Ult;
        public bool DealsDamage() => Damage > 0 || SecondaryDamage > 0;
        public SkillDef Duplicate() => (SkillDef)MemberwiseClone();
    }

    /// <summary>
    /// One weapon (docs/GDD.md "Weapons"), port of scripts/sim/weapon_def.gd. Every weapon is one generic <see cref="Shape"/> executor
    /// plus numbers; values live in data/weapons/*.tres (unity/Data/weapons__*.json).
    /// </summary>
    [Serializable]
    public sealed class WeaponDef
    {
        public enum WeaponKind { Attack, CrowdControl, Block, Heal }

        public enum WeaponShape
        {
            /// <summary>projectile that homes on the nearest enemy in range</summary>
            Targeted,
            /// <summary>instant (or delayed) cone in the aim direction</summary>
            Cone,
            /// <summary>circle at the aimed point after a delay (plus travel time if speed &gt; 0)</summary>
            GroundAoe,
            /// <summary>placed; arms, then turns into a slow zone when an enemy steps near</summary>
            Trap,
            /// <summary>placed zone that ticks damage + effect for a while</summary>
            Field,
            /// <summary>out to range and back to the thrower</summary>
            Boomerang,
            /// <summary>wall in front of the thrower that blocks enemy projectiles</summary>
            Shield,
            /// <summary>skillshot that bounces `count` times, AoE at each bounce</summary>
            Bouncer,
            /// <summary>linear projectile that passes through enemies up to max range</summary>
            Spinner,
            /// <summary>instant heal (self on a tap, a teammate on a drag)</summary>
            Heal,
            /// <summary>fast linear projectile that stops on the first enemy hit</summary>
            Skillshot,
        }

        public string Id = "";
        public string DisplayName = "";
        public string ShortName = "";
        public string Description = "";
        public string StreetGame = "";
        public WeaponKind Kind = WeaponKind.Attack;
        public WeaponShape Shape = WeaponShape.Targeted;
        public double Cooldown;
        public int ElixirCost = 3;
        public double MaxRange;
        public int Damage;
        public double Radius;
        public double AngleDegrees;
        public double Delay;
        public double Speed;
        public int Count = 1;
        public double Interval;
        public double Duration;
        public double ArmTime;
        public double TriggerRadius;
        public double Lifetime;
        public double Width;
        public double Offset;
        public double PushDistance;
        public double PushTime;
        public double ExpandFactor = 1.0;
        public double ExpandDelay;
        public double ExpandTime;
        public double ProjectileRadius;
        public StatusEffects.Type Effect = StatusEffects.Type.None;
        public double EffectDuration;
        public double EffectMagnitude;
        public int SnipsMin = 1;
        public int SnipsMax = 1;
        public double SnipHoldStep;
        public int Heal;
        public string Look = "";
        public double CastLock;
        public int CardPower;

        public bool DealsDamage() => Damage > 0;

        public string KindLabel()
        {
            switch (Kind)
            {
                case WeaponKind.Attack: return "Attack";
                case WeaponKind.CrowdControl: return "Control";
                case WeaponKind.Heal: return "Heal";
            }
            return "Block";
        }

        /// <summary>Snips for a cone cast after holding the button for `holdSeconds`.</summary>
        public int SnipCount(double holdSeconds)
        {
            if (SnipsMax <= SnipsMin || SnipHoldStep <= 0.0) return SnipsMin;
            return GMath.Clampi(SnipsMin + GMath.Floori(holdSeconds / SnipHoldStep), SnipsMin, SnipsMax);
        }

        public WeaponDef Duplicate() => (WeaponDef)MemberwiseClone();
    }
}
