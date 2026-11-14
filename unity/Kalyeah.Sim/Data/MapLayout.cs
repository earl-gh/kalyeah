using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Geometry of the playground barangay, in metres (port of map_layout.gd). Pure data + math. With <see cref="ArenaRadius"/> &gt; 0 the
    /// playable area is a circle; otherwise the legacy rounded street rectangle. Values come from data/rules/map_layout.tres.
    /// </summary>
    [Serializable]
    public sealed class MapLayout
    {
        public const int SideOwn = 1;
        public const int SideEnemy = -1;

        public Vec2I Tiles = new Vec2I(16, 32);
        public double TileSize = 1.5;
        public double ArenaRadius;
        public double BaseInset;
        public double BaseRadius;
        public double BaseCorner;
        public double SpawnOffset;
        public Vec2 RescueSize = Vec2.Zero;
        public int TaggedPlaces = 1;
        public List<Vec2> PowerupSpots = new List<Vec2>
        {
            new Vec2(-31.5, 4.8), new Vec2(5.8, 6.4), new Vec2(-5.8, -6.4), new Vec2(31.5, -4.8),
        };
        public List<Rect2> Obstacles = new List<Rect2>();
        public List<float> ObstacleYawsDeg = new List<float>();
        public List<float> ObstacleScales = new List<float>();
        public List<string> ObstacleProps = new List<string>();
        public List<Rect2> WaterObstacles = new List<Rect2>();
        public double BoundaryThickness;
        public double SceneryScale = 1.0;
        public double CoverHeight = 1.15;
        public double PostHeight = 8.5;
        public double PostWidthShare = 1.0;
        public double OverviewHeight;
        public double OverviewSize;

        /// <summary>Playable bounding span along X (from the grid).</summary>
        public double LaneWidth => Tiles.X * TileSize;
        /// <summary>Playable bounding span along Z (from the grid).</summary>
        public double LaneLength => Tiles.Y * TileSize;

        public static double SmoothBand(double edge0, double edge1, double val)
        {
            double span = GMath.Maxf(edge1 - edge0, 0.0001);
            double t = GMath.Clampf((val - edge0) / span, 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        public static double CanalZ(double x) => 2.4 * Math.Sin(x * 0.046) + 0.8 * Math.Sin(x * 0.105);

        public static double LaneCenterX(double z)
        {
            double taper = 1.0 - SmoothBand(50.0, 62.0, Math.Abs(z));
            return 4.2 * Math.Sin(z * 0.048) * taper;
        }

        public static double LaneEastX(double z)
        {
            double taper = 1.0 - SmoothBand(56.0, 74.0, Math.Abs(z));
            return 34.5 + (3.8 * Math.Sin(z * 0.052 + 0.55) - 0.055 * Math.Abs(z)) * taper;
        }

        public static double LaneWestX(double z) => -LaneEastX(-z);

        public static double EskinitaSouthZ(double x) => 28.0 + 3.0 * Math.Sin(x * 0.058);

        public static double EskinitaNorthZ(double x) => -EskinitaSouthZ(-x);

        public static double LaneCenterHalfWidth(double z)
        {
            double az = Math.Abs(z);
            double dzEsk = (az - 28.0) / 8.5;
            double dzFord = z / 8.5;
            double dzPlaza = (az - 62.0) / 10.0;
            return 4.45 + 2.15 * Math.Exp(-dzEsk * dzEsk) + 1.85 * Math.Exp(-dzFord * dzFord) + 2.40 * Math.Exp(-dzPlaza * dzPlaza);
        }

        public static double LaneSideHalfWidth(double z)
        {
            double az = Math.Abs(z);
            double dzEsk = (az - 28.0) / 9.0;
            double dzFord = z / 8.5;
            return 4.25 + 1.95 * Math.Exp(-dzEsk * dzEsk) + 1.65 * Math.Exp(-dzFord * dzFord);
        }

        public static double EskinitaHalfWidth(double x)
        {
            double ax = Math.Abs(x);
            double dxMid = ax / 9.0;
            double dxSide = (ax - 34.5) / 8.5;
            return 3.55 + 1.55 * Math.Exp(-dxMid * dxMid) + 1.45 * Math.Exp(-dxSide * dxSide);
        }

        public Vec2 TileCenter(Vec2I tile) =>
            new Vec2((tile.X + 0.5 - Tiles.X / 2.0) * TileSize, (tile.Y + 0.5 - Tiles.Y / 2.0) * TileSize);

        public Vec2I TileAt(Vec2 position) =>
            new Vec2I(GMath.Floori(position.x / TileSize + Tiles.X / 2.0), GMath.Floori(position.y / TileSize + Tiles.Y / 2.0));

        public Vec2 ToTiles(Vec2 position) => new Vec2(position.x / TileSize + Tiles.X / 2.0, position.y / TileSize + Tiles.Y / 2.0);

        public bool IsCircular() => ArenaRadius > 0.0;

        public bool InArena(Vec2 point, double pad = 0.0)
        {
            if (ArenaRadius > 0.0)
            {
                double limit = GMath.Maxf(ArenaRadius - pad, 0.0);
                return point.LengthSquared() <= limit * limit;
            }
            return RoundedHasPoint(LaneRect().Grow(-pad), GMath.Maxf(BaseCorner - pad, 0.0), point);
        }

        public Vec2 ClampToArena(Vec2 point, double pad = 0.0)
        {
            if (ArenaRadius > 0.0)
            {
                double limit = GMath.Maxf(ArenaRadius - pad, 0.0);
                double lenSq = point.LengthSquared();
                if (lenSq > limit * limit && lenSq > 0.0) return point * (limit / Math.Sqrt(lenSq));
                return point;
            }
            return KeepInRectStreet(point, pad);
        }

        public Vec2 BaseCenter(int side)
        {
            if (ArenaRadius > 0.0) return new Vec2(0.0, side * GMath.Maxf(ArenaRadius - BaseInset, 0.0));
            return new Vec2(-side * (LaneWidth / 2.0 - BaseInset), side * (LaneLength / 2.0 - BaseInset));
        }

        public Vec2 PostPosition(int side)
        {
            if (ArenaRadius > 0.0)
            {
                double zCenter = GMath.Maxf(ArenaRadius - BaseInset, 0.0);
                double postShift = GMath.Minf(GMath.Maxf(BaseCorner, 3.5), GMath.Maxf(BaseRadius - 2.0, 0.0));
                return new Vec2(0.0, side * (zCenter + postShift));
            }
            return new Vec2(-side * (LaneWidth / 2.0 - BaseCorner), side * (LaneLength / 2.0 - BaseCorner));
        }

        public Rect2 BaseRect(int side)
        {
            Vec2 reach = Vec2.One * BaseRadius;
            return new Rect2(BaseCenter(side) - reach, reach * 2.0).Intersection(LaneRect());
        }

        public bool InBase(int side, Vec2 position) => RoundedHasPoint(BaseRect(side), BaseCorner, position);

        public static bool RoundedHasPoint(Rect2 rect, double radius, Vec2 position)
        {
            Vec2 half = rect.Size / 2.0;
            Vec2 apart = (position - rect.GetCenter()).Abs();
            Vec2 straight = half - Vec2.One * radius;
            return new Vec2(GMath.Maxf(apart.x - straight.x, 0.0), GMath.Maxf(apart.y - straight.y, 0.0)).Length() <= radius;
        }

        public Vec2 SpawnCenter(int side)
        {
            if (ArenaRadius > 0.0) return BaseCenter(side) - new Vec2(0.0, side * SpawnOffset);
            return BaseCenter(side) + new Vec2(side, -side) * SpawnOffset;
        }

        public Rect2 RescueRect(int side)
        {
            if (ArenaRadius > 0.0)
            {
                double zMid = -(double)side * (ArenaRadius * 0.375);
                double zFar = Math.Abs(zMid) + RescueSize.y * 0.5;
                double innerR = GMath.Maxf(ArenaRadius - 2.2, RescueSize.x);
                double xEdge = Math.Sqrt(GMath.Maxf(innerR * innerR - zFar * zFar, 0.0));
                double leftC = side == SideOwn ? -xEdge : xEdge - RescueSize.x;
                return new Rect2(leftC, zMid - RescueSize.y * 0.5, RescueSize.x, RescueSize.y);
            }
            Rect2 otherBase = BaseRect(-side);
            double edge = side == SideOwn ? otherBase.End.y : otherBase.Position.y;
            double top = side == SideOwn ? edge : edge - RescueSize.y;
            double left = side == SideOwn ? -LaneWidth / 2.0 : LaneWidth / 2.0 - RescueSize.x;
            return new Rect2(left, top, RescueSize.x, RescueSize.y);
        }

        public Vec2 TaggedPlace(int side, int index)
        {
            int slots = GMath.Maxi(TaggedPlaces, 1);
            Rect2 area = RescueRect(side);
            long row = GMath.Posmod(index, slots);
            double y = area.Position.y + area.Size.y * (row + 0.5) / slots;
            if (ArenaRadius > 0.0)
            {
                double curbR = ArenaRadius + GMath.Maxf(BoundaryThickness, 1.0);
                double xCurb = -(double)side * Math.Sqrt(GMath.Maxf(curbR * curbR - y * y, 0.0));
                return new Vec2(xCurb, y);
            }
            return new Vec2(-side * (LaneWidth / 2.0 + BoundaryThickness), y);
        }

        public Vec2 TaggedSpot(int side, int slot) => TaggedPlace(side, slot);

        public Vec2 TaggedFacing(int side) => new Vec2(side, 0.0);

        public Rect2 LaneRect() => new Rect2(-LaneWidth / 2.0, -LaneLength / 2.0, LaneWidth, LaneLength);

        public Vec2 KeepInStreet(Vec2 point, double radius)
        {
            if (ArenaRadius > 0.0) return ClampToArena(point, radius);
            return KeepInRectStreet(point, radius);
        }

        Vec2 KeepInRectStreet(Vec2 point, double radius)
        {
            Rect2 inner = LaneRect().Grow(-radius);
            double curve = GMath.Maxf(BaseCorner - radius, 0.0);
            Vec2 inside = point.Clamp(inner.Position, inner.End);
            Vec2 straight = inner.Size / 2.0 - Vec2.One * curve;
            Vec2 fromCentre = inside - inner.GetCenter();
            Vec2 arcCentre = fromCentre.Clamp(-straight, straight);
            Vec2 outward = fromCentre - arcCentre;
            if (outward.Length() <= curve) return inside;
            return inner.GetCenter() + arcCentre + outward.Normalized() * curve;
        }

        public bool HasOrientedParcels() => ObstacleYawsDeg.Count > 0 && ObstacleYawsDeg.Count == Obstacles.Count;

        public int ParcelCount() => Obstacles.Count * 2;

        public Rect2 ParcelRect(int index)
        {
            int half = Obstacles.Count;
            if (half == 0) return new Rect2();
            if (index < half) return Obstacles[index];
            Rect2 src = Obstacles[index - half];
            return new Rect2(-src.End, src.Size);
        }

        public double ParcelYawRad(int index)
        {
            int half = Obstacles.Count;
            if (half == 0 || ObstacleYawsDeg.Count == 0) return 0.0;
            int baseIdx = index % half;
            double deg = baseIdx < ObstacleYawsDeg.Count ? ObstacleYawsDeg[baseIdx] : 0.0;
            double rad = GMath.DegToRad(deg);
            if (index >= half) rad = GMath.Wrapf(rad + Math.PI, -Math.PI, Math.PI);
            return rad;
        }

        public double ParcelScale(int index)
        {
            int half = Obstacles.Count;
            if (half == 0 || ObstacleScales.Count == 0) return 1.0;
            int baseIdx = index % half;
            return baseIdx < ObstacleScales.Count ? ObstacleScales[baseIdx] : 1.0;
        }

        public string ParcelProp(int index) => ObstacleProp(index);

        /// <summary>Every solid obstacle: own-half pieces plus point-symmetric enemy-half mirrors.</summary>
        public List<Rect2> ObstacleRects()
        {
            var result = new List<Rect2>();
            foreach (Rect2 rect in Obstacles) result.Add(rect);
            foreach (Rect2 rect in Obstacles) result.Add(new Rect2(-rect.End, rect.Size));
            return result;
        }

        public List<Rect2> WaterRects()
        {
            var result = new List<Rect2>();
            foreach (Rect2 rect in WaterObstacles) result.Add(rect);
            foreach (Rect2 rect in WaterObstacles) result.Add(new Rect2(-rect.End, rect.Size));
            return result;
        }

        public List<Rect2> WalkObstacleRects()
        {
            List<Rect2> result = ObstacleRects();
            result.AddRange(WaterRects());
            return result;
        }

        public string ObstacleProp(int index)
        {
            if (ObstacleProps.Count == 0 || Obstacles.Count == 0) return "bench";
            return ObstacleProps[(int)(GMath.Posmod(index, Obstacles.Count) % ObstacleProps.Count)];
        }

        public List<Rect2> BoundaryRects()
        {
            double t = BoundaryThickness;
            return new List<Rect2>
            {
                new Rect2(-LaneWidth / 2.0 - t, -LaneLength / 2.0, t, LaneLength),
                new Rect2(LaneWidth / 2.0, -LaneLength / 2.0, t, LaneLength),
                new Rect2(-LaneWidth / 2.0 - t, -LaneLength / 2.0 - t, LaneWidth + 2.0 * t, t),
                new Rect2(-LaneWidth / 2.0 - t, LaneLength / 2.0, LaneWidth + 2.0 * t, t),
            };
        }
    }
}
