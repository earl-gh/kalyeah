using System;

namespace Kalyeah.Sim
{
    /// <summary>Godot's Rect2 (float position and size) with Godot 4.6's formulas.</summary>
    [Serializable]
    public struct Rect2 : IEquatable<Rect2>
    {
        public Vec2 Position;
        public Vec2 Size;

        public Rect2(Vec2 position, Vec2 size) { Position = position; Size = size; }
        public Rect2(double x, double y, double w, double h) { Position = new Vec2(x, y); Size = new Vec2(w, h); }

        public Vec2 End => Position + Size;

        public Vec2 GetCenter() => Position + Size * 0.5;

        public bool HasPoint(Vec2 p)
        {
            if (p.X < Position.X) return false;
            if (p.Y < Position.Y) return false;
            if (p.X >= Position.X + Size.X) return false;
            if (p.Y >= Position.Y + Size.Y) return false;
            return true;
        }

        public bool Intersects(Rect2 r, bool includeBorders = false)
        {
            if (includeBorders)
            {
                if (Position.X > r.Position.X + r.Size.X) return false;
                if (Position.X + Size.X < r.Position.X) return false;
                if (Position.Y > r.Position.Y + r.Size.Y) return false;
                if (Position.Y + Size.Y < r.Position.Y) return false;
            }
            else
            {
                if (Position.X >= r.Position.X + r.Size.X) return false;
                if (Position.X + Size.X <= r.Position.X) return false;
                if (Position.Y >= r.Position.Y + r.Size.Y) return false;
                if (Position.Y + Size.Y <= r.Position.Y) return false;
            }
            return true;
        }

        public Rect2 Intersection(Rect2 r)
        {
            if (!Intersects(r)) return new Rect2();
            Rect2 result = r;
            result.Position = r.Position.Max(Position);
            Vec2 rEnd = r.Position + r.Size;
            Vec2 end = Position + Size;
            result.Size = rEnd.Min(end) - result.Position;
            return result;
        }

        public Rect2 Grow(double amount)
        {
            float a = (float)amount;
            Rect2 g = this;
            g.Position.X -= a;
            g.Position.Y -= a;
            g.Size.X += a * 2;
            g.Size.Y += a * 2;
            return g;
        }

        public double GetArea() => Size.X * Size.Y;

        public bool Equals(Rect2 o) => Position == o.Position && Size == o.Size;
        public override bool Equals(object obj) => obj is Rect2 r && Equals(r);
        public override int GetHashCode() => Position.GetHashCode() * 397 ^ Size.GetHashCode();
        public static bool operator ==(Rect2 a, Rect2 b) => a.Equals(b);
        public static bool operator !=(Rect2 a, Rect2 b) => !a.Equals(b);
        public override string ToString() => $"[P: {Position}, S: {Size}]";
    }
}
