using System;

namespace Kalyeah.Sim
{
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
