using System;

namespace Kalyeah.Sim
{
    /// <summary>GDScript's global scalar functions (on 64-bit doubles, like GDScript's float), with Godot 4.6's formulas.</summary>
    public static class GMath
    {
        public const double CmpEpsilon = 0.00001;
        public const float CmpEpsilonF = 0.00001f;
        public const double Pi = Math.PI;
        public const double Tau = Math.PI * 2.0;
        public const double Inf = double.PositiveInfinity;

        public static double Clampf(double v, double min, double max) => v < min ? min : (v > max ? max : v);
        public static int Clampi(long v, long min, long max) => (int)(v < min ? min : (v > max ? max : v));
        public static double Maxf(double a, double b) => a > b ? a : b;
        public static double Minf(double a, double b) => a < b ? a : b;
        public static int Maxi(int a, int b) => a > b ? a : b;
        public static int Mini(int a, int b) => a < b ? a : b;
        public static double Absf(double v) => Math.Abs(v);
        /// <summary>roundi: half away from zero (C's round()).</summary>
        public static int Roundi(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static int Ceili(double v) => (int)Math.Ceiling(v);
        public static int Floori(double v) => (int)Math.Floor(v);
        public static double Floorf(double v) => Math.Floor(v);
        public static double Lerpf(double from, double to, double weight) => from + (to - from) * weight;
        public static double DegToRad(double deg) => deg * (Math.PI / 180.0);
        public static double RadToDeg(double rad) => rad * (180.0 / Math.PI);

        public static long Posmod(long a, long b)
        {
            long value = a % b;
            if ((value < 0 && b > 0) || (value > 0 && b < 0)) value += b;
            return value;
        }

        public static double Fposmod(double x, double y)
        {
            double value = x % y;
            if ((value < 0 && y > 0) || (value > 0 && y < 0)) value += y;
            value += 0.0;
            return value;
        }

        public static double Wrapf(double value, double min, double max)
        {
            double range = max - min;
            if (IsZeroApprox(range)) return min;
            double result = value - (range * Math.Floor((value - min) / range));
            if (IsEqualApprox(result, max)) return min;
            return result;
        }

        public static bool IsZeroApprox(double v) => Math.Abs(v) < CmpEpsilon;
        public static bool IsZeroApproxF(float v) => MathF.Abs(v) < CmpEpsilonF;

        public static bool IsEqualApprox(double a, double b)
        {
            if (a == b) return true;
            double tolerance = CmpEpsilon * Math.Abs(a);
            if (tolerance < CmpEpsilon) tolerance = CmpEpsilon;
            return Math.Abs(a - b) < tolerance;
        }

        public static bool IsEqualApproxF(float a, float b)
        {
            if (a == b) return true;
            float tolerance = CmpEpsilonF * MathF.Abs(a);
            if (tolerance < CmpEpsilonF) tolerance = CmpEpsilonF;
            return MathF.Abs(a - b) < tolerance;
        }
    }
}
