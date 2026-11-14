using System;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Godot's Vector2 (32-bit float components, real_t), with Godot 4.6's formulas, so vector maths rounds exactly like the Godot sim.
    /// Scalars follow GDScript: a component read as <c>x</c>/<c>y</c> (and every length, dot, angle...) is a 64-bit double, the way GDScript
    /// promotes it; a double scalar multiplied into a vector is first cast to float (Godot's Variant operators do the same).
    /// <c>X</c>/<c>Y</c> are the raw float components.
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(double x, double y) { X = (float)x; Y = (float)y; }

        static Vec2 F(float x, float y) { Vec2 v; v.X = x; v.Y = y; return v; }

        /// <summary>The x component as GDScript reads it (a double).</summary>
        public double x { get => X; set => X = (float)value; }
        /// <summary>The y component as GDScript reads it (a double).</summary>
        public double y { get => Y; set => Y = (float)value; }

        public static readonly Vec2 Zero = F(0f, 0f);
        public static readonly Vec2 One = F(1f, 1f);
        public static readonly Vec2 Inf = F(float.PositiveInfinity, float.PositiveInfinity);

        public static Vec2 operator +(Vec2 a, Vec2 b) => F(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => F(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => F(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, Vec2 b) => F(a.X * b.X, a.Y * b.Y);
        public static Vec2 operator /(Vec2 a, Vec2 b) => F(a.X / b.X, a.Y / b.Y);
        public static Vec2 operator *(Vec2 a, double s) { float f = (float)s; return F(a.X * f, a.Y * f); }
        public static Vec2 operator *(double s, Vec2 a) { float f = (float)s; return F(a.X * f, a.Y * f); }
        public static Vec2 operator /(Vec2 a, double s) { float f = (float)s; return F(a.X / f, a.Y / f); }
        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        public bool Equals(Vec2 other) => this == other;
        public override bool Equals(object obj) => obj is Vec2 v && this == v;
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X}, {Y})";

        public double Length() => MathF.Sqrt(X * X + Y * Y);
        public double LengthSquared() => X * X + Y * Y;

        public double DistanceTo(Vec2 to) => MathF.Sqrt((X - to.X) * (X - to.X) + (Y - to.Y) * (Y - to.Y));
        public double DistanceSquaredTo(Vec2 to) => (X - to.X) * (X - to.X) + (Y - to.Y) * (Y - to.Y);

        public Vec2 Normalized()
        {
            float l = X * X + Y * Y;
            if (l != 0f)
            {
                l = MathF.Sqrt(l);
                return F(X / l, Y / l);
            }
            return this;
        }

        public Vec2 LimitLength(double length = 1.0)
        {
            float len = (float)length;
            float l = MathF.Sqrt(X * X + Y * Y);
            Vec2 v = this;
            if (l > 0f && len < l)
            {
                v = F(v.X / l, v.Y / l);
                v = F(v.X * len, v.Y * len);
            }
            return v;
        }

        public Vec2 DirectionTo(Vec2 to) => F(to.X - X, to.Y - Y).Normalized();

        public double Dot(Vec2 b) => X * b.X + Y * b.Y;
        public double Cross(Vec2 b) => X * b.Y - Y * b.X;
        public double Angle() => MathF.Atan2(Y, X);
        public double AngleTo(Vec2 to) => MathF.Atan2(X * to.Y - Y * to.X, X * to.X + Y * to.Y);

        public Vec2 Rotated(double by)
        {
            float sine = MathF.Sin((float)by);
            float cosi = MathF.Cos((float)by);
            return F(X * cosi - Y * sine, X * sine + Y * cosi);
        }

        public static Vec2 FromAngle(double angle) => F(MathF.Cos((float)angle), MathF.Sin((float)angle));

        public Vec2 Orthogonal() => F(Y, -X);
        public Vec2 Abs() => F(MathF.Abs(X), MathF.Abs(Y));
        public Vec2 Clamp(Vec2 min, Vec2 max) => F(ClampF(X, min.X, max.X), ClampF(Y, min.Y, max.Y));
        public Vec2 Min(Vec2 b) => F(MathF.Min(X, b.X), MathF.Min(Y, b.Y));
        public Vec2 Max(Vec2 b) => F(MathF.Max(X, b.X), MathF.Max(Y, b.Y));
        public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y);
        public bool IsZeroApprox() => GMath.IsZeroApproxF(X) && GMath.IsZeroApproxF(Y);
        public bool IsEqualApprox(Vec2 b) => GMath.IsEqualApproxF(X, b.X) && GMath.IsEqualApproxF(Y, b.Y);

        public Vec2 Lerp(Vec2 to, double weight)
        {
            float w = (float)weight;
            return F(X + w * (to.X - X), Y + w * (to.Y - Y));
        }

        public Vec2 MoveToward(Vec2 to, double delta)
        {
            float d = (float)delta;
            Vec2 vd = to - this;
            float len = MathF.Sqrt(vd.X * vd.X + vd.Y * vd.Y);
            return len <= d || len < GMath.CmpEpsilonF ? to : this + F(vd.X / len, vd.Y / len) * d;
        }

        /// <summary>Component by index (GDScript's <c>v[axis]</c>), as a double.</summary>
        public double this[int axis] => axis == 0 ? X : Y;

        // Godot's CLAMP(m_a, m_min, m_max)
        static float ClampF(float a, float min, float max) => a < min ? min : (a > max ? max : a);
    }

    /// <summary>Godot's Vector2i.</summary>
    [Serializable]
    public struct Vec2I : IEquatable<Vec2I>
    {
        public int X;
        public int Y;
        public Vec2I(int x, int y) { X = x; Y = y; }
        public bool Equals(Vec2I o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Vec2I v && Equals(v);
        public override int GetHashCode() => X * 397 ^ Y;
        public static bool operator ==(Vec2I a, Vec2I b) => a.Equals(b);
        public static bool operator !=(Vec2I a, Vec2I b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>Godot's Color (cosmetic data only).</summary>
    [Serializable]
    public struct GColor
    {
        public float R, G, B, A;
        public GColor(double r, double g, double b, double a = 1.0) { R = (float)r; G = (float)g; B = (float)b; A = (float)a; }
        public static readonly GColor White = new GColor(1, 1, 1, 1);
    }
}
