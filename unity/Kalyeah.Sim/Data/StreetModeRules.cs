using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// The numbers of a timed street game (port of street_mode_rules.gd; data only). Values come from data/rules/tumbang_preso.tres,
    /// patintero.tres and langit_lupa.tres (unity/Data/rules__*.json).
    /// </summary>
    [Serializable]
    public sealed class StreetModeRules
    {
        public string Id = "";
        public string Title = "";
        public double MatchSeconds = 300.0;
        public int RoomMinPlayers = 2;
        public double RollSeconds;
        public int RoomMinTeam = 1;
        public double SwapGrace = 1.5;
        public double TagCircleRadius = 1.3;
        public double ResetSeconds = 1.3;
        public double HomeZ = 16.0;
        public double GoalZ = -18.0;
        public double SpawnSpacing = 3.0;

        // Tumbang Preso
        public Vec2 CanSpot = Vec2.Zero;
        public double CanHitRadius = 0.45;
        public double ThrowMinRange = 6.0;
        public double ThrowMaxRange = 18.0;
        public double ThrowChargeTime = 1.4;
        public double ThrowSpeed = 18.0;
        public double AimArcDegrees = 80.0;
        public double AimSensitivityLow = 0.0015;
        public double AimSensitivityHigh = 0.006;
        public double PickupRadius = 0.7;
        public double PlaceRadius = 0.8;
        public double GuardOffset = 1.6;
        public double TayaSpeedShare = 1.5;
        public double ScaredSeconds = 5.0;
        public double CanFlingDistance = 6.0;
        public double SlipperFlingDistance = 5.0;
        public double FlingErrorDegrees = 18.0;
        public double FlingDistanceError = 0.2;
        public double CanDeflectDegrees = 35.0;
        public double SlipperBackDegrees = 150.0;
        public double SlipperSideDegrees = 80.0;
        public double BounceChance = 0.3;
        public double BounceShare = 0.5;
        public double BounceErrorDegrees = 25.0;
        public double FlingTime = 0.55;
        public double GuideLength = 2.5;
        public double KickReach = 1.6;
        public double KickMinDistance = 4.0;
        public double KickMaxDistance = 8.0;
        public double KickCooldown = 4.0;
        public double KickConeDegrees = 75.0;

        // Patintero
        public double LineSpan = 10.0;
        public double SidewalkWidth = 2.4;
        public double CatchSeconds = 0.7;
        public double StallSeconds = 20.0;
        public double DiveSpeedShare = 2.6;
        public double DiveTime = 0.4;
        public double DiveRecover = 0.5;
        public double DiveCooldown = 5.0;
        public double DiveReturnSpeedShare = 1.5;

        // Langit Lupa
        public List<Rect2> LangitSpots = new List<Rect2>();
        public double LangitSeconds = 3.0;
        public double LangitRefillPerS = 1.0;
        public double LangitWearSeconds = 10.0;
        public double MoundRadius = 1.0;
        public double MoundSeconds = 4.0;
        public double MoundCooldown = 12.0;
        public bool LangitBlocksRest = true;
        public double SurviveSeconds = 10.0;
        public int TagPoints = 1;
        public double BrownoutFirst = 45.0;
        public double BrownoutEvery = 60.0;
        public double BrownoutSeconds = 10.0;
        public double BrownoutSight = 5.0;
        public double RingRadius = 9.0;

        // Bots
        public double BotThrowError = 0.8;
        public double BotThrowMargin = 0.5;
        public double BotThrowWait = 2.0;
        public double BotGuardSpeed = 0.8;
        public double BotSafeDistance = 5.0;
        public double BotDiveRange = 3.0;
        public double BotRushSeconds = 2.0;
    }
}
