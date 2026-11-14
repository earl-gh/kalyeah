using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>Circle-vs-axis-aligned-rectangle collision in the XZ plane (port of collision.gd).</summary>
    public static class Collision
    {
        public const int DefaultPasses = 3;

        /// <summary>Returns `center`, moved the shortest way out of `rect` if the circle overlaps it.</summary>
        public static Vec2 PushOut(Vec2 center, double radius, Rect2 rect)
        {
            Vec2 end = rect.End;
            Vec2 closest = new Vec2(GMath.Clampf(center.x, rect.Position.x, end.x), GMath.Clampf(center.y, rect.Position.y, end.y));
            Vec2 delta = center - closest;
            double distanceSquared = delta.LengthSquared();
            if (distanceSquared > 0.0)
            {
                if (distanceSquared >= radius * radius) return center;
                return closest + delta / Math.Sqrt(distanceSquared) * radius;
            }
            double toLeft = center.x - rect.Position.x;
            double toRight = end.x - center.x;
            double toTop = center.y - rect.Position.y;
            double toBottom = end.y - center.y;
            double nearest = GMath.Minf(GMath.Minf(toLeft, toRight), GMath.Minf(toTop, toBottom));
            if (nearest == toLeft) return new Vec2(rect.Position.x - radius, center.y);
            if (nearest == toRight) return new Vec2(end.x + radius, center.y);
            if (nearest == toTop) return new Vec2(center.x, rect.Position.y - radius);
            return new Vec2(center.x, end.y + radius);
        }

        /// <summary>Pushes the circle out of every rect; a few passes settle corners and gaps.</summary>
        public static Vec2 Resolve(Vec2 center, double radius, IReadOnlyList<Rect2> rects)
        {
            Vec2 result = center;
            for (int pass = 0; pass < DefaultPasses; pass++)
                for (int i = 0; i < rects.Count; i++)
                    result = PushOut(result, radius, rects[i]);
            return result;
        }
    }

    /// <summary>How a wall moves and grows with its age (port of wall_motion.gd), as pure functions of the weapon's data.</summary>
    public static class WallMotion
    {
        /// <summary>Metres the wall has moved along its facing `age` seconds after it was cast.</summary>
        public static double Push(WeaponDef def, double age)
        {
            if (def == null || def.PushDistance == 0.0 || def.PushTime <= 0.0) return 0.0;
            double left = 1.0 - GMath.Clampf(age / def.PushTime, 0.0, 1.0);
            return def.PushDistance * (1.0 - left * left * left);
        }

        /// <summary>The wall's width as a multiple of the weapon's `width`.</summary>
        public static double WidthScale(WeaponDef def, double age)
        {
            if (def == null || def.ExpandFactor == 1.0 || def.ExpandTime <= 0.0) return 1.0;
            double t = GMath.Clampf((age - def.ExpandDelay) / def.ExpandTime, 0.0, 1.0);
            return GMath.Lerpf(1.0, def.ExpandFactor, t * t * (3.0 - 2.0 * t));
        }
    }

    /// <summary>One-tier scoring (port of match_score.gd): first to `FirstTo` points wins.</summary>
    public sealed class MatchScore
    {
        public enum Result { Point, Match }

        /// <summary>Points per team (0, 1).</summary>
        public int[] Points = { 0, 0 };
        public int FirstTo = 3;
        /// <summary>Team that won the match, or -1.</summary>
        public int Winner = -1;

        public MatchScore(int firstTo) { FirstTo = firstTo; }

        public int PointsToWin() => FirstTo;

        public Result AwardPoint(int team)
        {
            if (Winner >= 0) return Result.Match;
            Points[team] += 1;
            if (Points[team] >= PointsToWin())
            {
                Winner = team;
                return Result.Match;
            }
            return Result.Point;
        }
    }
}
