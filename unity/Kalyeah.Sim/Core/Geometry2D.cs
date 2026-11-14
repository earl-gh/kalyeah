using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>The Godot 4.6 Geometry2D functions the sim uses (core/math/geometry_2d.h, float maths), plus FightBrain's segment test.</summary>
    public static class Geometry2D
    {
        /// <summary>Godot's segment_intersects_segment; returns false when they do not cross (GDScript's null).</summary>
        public static bool SegmentIntersectsSegment(Vec2 fromA, Vec2 toA, Vec2 fromB, Vec2 toB, out Vec2 result)
        {
            result = Vec2.Zero;
            Vec2 b = toA - fromA;
            Vec2 c = fromB - fromA;
            Vec2 d = toB - fromA;
            float abLen = b.X * b.X + b.Y * b.Y;
            if (abLen <= 0f) return false;
            Vec2 bn = new Vec2(b.X / abLen, b.Y / abLen);
            c = new Vec2(c.X * bn.X + c.Y * bn.Y, c.Y * bn.X - c.X * bn.Y);
            d = new Vec2(d.X * bn.X + d.Y * bn.Y, d.Y * bn.X - d.X * bn.Y);
            float eps = GMath.CmpEpsilonF;
            if ((c.Y < -eps && d.Y < -eps) || (c.Y > eps && d.Y > eps)) return false;
            if (GMath.IsEqualApproxF(c.Y, d.Y)) return false;
            float abPos = d.X + (c.X - d.X) * d.Y / (d.Y - c.Y);
            if (abPos < 0f || abPos > 1f) return false;
            result = fromA + b * abPos;
            return true;
        }

        public static bool SegmentIntersectsSegment(Vec2 fromA, Vec2 toA, Vec2 fromB, Vec2 toB) => SegmentIntersectsSegment(fromA, toA, fromB, toB, out _);

        /// <summary>Godot's is_point_in_polygon.</summary>
        public static bool IsPointInPolygon(Vec2 point, IReadOnlyList<Vec2> polygon)
        {
            int c = polygon.Count;
            if (c < 3) return false;
            Vec2 further = new Vec2(-1e20, -1e20);
            Vec2 opposite = new Vec2(1e20, 1e20);
            for (int i = 0; i < c; i++)
            {
                further = further.Max(polygon[i]);
                opposite = opposite.Min(polygon[i]);
            }
            further += (further - opposite) * new Vec2(1.221313, 1.512312);
            int intersections = 0;
            for (int i = 0; i < c; i++)
            {
                Vec2 v1 = polygon[i];
                Vec2 v2 = polygon[(i + 1) % c];
                if (SegmentIntersectsSegment(v1, v2, point, further, out Vec2 res))
                {
                    intersections++;
                    if (res.IsEqualApprox(point)) return true;
                }
            }
            return (intersections & 1) != 0;
        }

        /// <summary>Godot's get_closest_point_to_segment.</summary>
        public static Vec2 GetClosestPointToSegment(Vec2 point, Vec2 a, Vec2 b)
        {
            Vec2 p = point - a;
            Vec2 n = b - a;
            float l2 = n.X * n.X + n.Y * n.Y;
            if (l2 < 1e-20f) return a;
            float d = (n.X * p.X + n.Y * p.Y) / l2;
            if (d <= 0f) return a;
            if (d >= 1f) return b;
            return a + n * d;
        }

        /// <summary>FightBrain.segment_hits_rect: true when segment a-b touches `rect` (slab test, GDScript doubles).</summary>
        public static bool SegmentHitsRect(Vec2 a, Vec2 b, Rect2 rect)
        {
            Vec2 direction = b - a;
            double near = 0.0;
            double far = 1.0;
            Vec2 end = rect.End;
            for (int axis = 0; axis < 2; axis++)
            {
                double origin = a[axis];
                double step = direction[axis];
                double low = rect.Position[axis];
                double high = end[axis];
                if (GMath.IsZeroApprox(step))
                {
                    if (origin < low || origin > high) return false;
                    continue;
                }
                double t1 = (low - origin) / step;
                double t2 = (high - origin) / step;
                near = GMath.Maxf(near, GMath.Minf(t1, t2));
                far = GMath.Minf(far, GMath.Maxf(t1, t2));
                if (near > far) return false;
            }
            return true;
        }
    }
}
