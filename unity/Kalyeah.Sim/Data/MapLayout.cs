using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>Short CODM/Valorant-style map callout region (docs/MAPS.md).</summary>
    public readonly struct CalloutRegion
    {
        public readonly string Name;
        public readonly string Half;
        public readonly Rect2 Bounds;
        public readonly Vec2 LabelPos;

        public CalloutRegion(string name, string half, Rect2 bounds, Vec2 labelPos)
        {
            Name = name ?? "";
            Half = half ?? "";
            Bounds = bounds;
            LabelPos = labelPos;
        }
    }

    /// <summary>High-ground climbable spot (docs/MAPS.md: 3 per map).</summary>
    public readonly struct ClimbSpot
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Half;
        public readonly Vec2 Position;
        public readonly double Height;
        public readonly double Radius;

        public ClimbSpot(string id, string label, string half, Vec2 position, double height, double radius)
        {
            Id = id ?? "";
            Label = label ?? "";
            Half = half ?? "";
            Position = position;
            Height = height;
            Radius = radius;
        }
    }

    /// <summary>Fixed untargetable healing vendor cart (docs/SPEC.md §6, docs/MAPS.md).</summary>
    public readonly struct VendorSpot
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Lane;
        public readonly Vec2 Position;
        public readonly double Radius;
        public readonly int HealAmount;
        public readonly double EatSeconds;
        public readonly double EatSlowMult;
        public readonly double RestockSeconds;

        public VendorSpot(
            string id,
            string name,
            string lane,
            Vec2 position,
            double radius,
            int healAmount,
            double eatSeconds,
            double eatSlowMult,
            double restockSeconds)
        {
            Id = id ?? "";
            Name = name ?? "";
            Lane = lane ?? "";
            Position = position;
            Radius = radius;
            HealAmount = healAmount;
            EatSeconds = eatSeconds;
            EatSlowMult = eatSlowMult;
            RestockSeconds = restockSeconds;
        }
    }

    /// <summary>
    /// Geometry of the playground barangay, in metres (port of map_layout.gd). Pure data + math. With <see cref="ArenaRadius"/> &gt; 0 the
    /// playable area is a circle; otherwise the legacy rounded street rectangle. Values come from data/rules/map_layout.tres.
    /// </summary>
    [Serializable]
    public sealed class MapLayout
    {
        public const int SideOwn = 1;
        public const int SideEnemy = -1;
        public const int HalfRiverside = SideOwn;
        public const int HalfUptown = SideEnemy;

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

        // Asymmetric Barangay Living World geometry (docs/MAPS.md, docs/SPEC.md §6) — strictly additive
        public Vec2 UptownBaseCenter = new Vec2(0.0, -64.0);
        public Vec2 UptownJailCenter = new Vec2(-15.5, -66.5);
        public Vec2 RiversideBaseCenter = new Vec2(0.0, 64.0);
        public Vec2 RiversideJailCenter = new Vec2(15.5, 66.5);
        public double AsymmetricBaseRadius = 4.5;
        public double JailRadius = 2.2;
        public List<OrderedDict<string, object>> CalloutRegions = new List<OrderedDict<string, object>>();
        public List<OrderedDict<string, object>> ClimbSpots = new List<OrderedDict<string, object>>();
        public List<Rect2> TallCoverZones = new List<Rect2>();
        public double TallCoverRevealRadius = 6.0;
        public double ShallowWaterHalfWidth = 3.75;
        public double ShallowWaterSpeedMult = 0.85;
        public double BridgeClearHalfWidth = 4.8;
        public List<OrderedDict<string, object>> Vendors = new List<OrderedDict<string, object>>();
        public List<Vec2> SelectaWaypoints = new List<Vec2>();
        public Vec2 DragonSpawn = new Vec2(-7.0, 0.0);
        public Vec2 BaronSpawn = new Vec2(54.0, -3.8);

        CalloutRegion[] _cachedCallouts;
        int _cachedCalloutsCount = -1;
        ClimbSpot[] _cachedClimbSpots;
        int _cachedClimbSpotsCount = -1;
        VendorSpot[] _cachedVendors;
        int _cachedVendorsCount = -1;

        static readonly CalloutRegion[] DefaultCallouts =
        {
            new CalloutRegion("HALL", "uptown", new Rect2(-22.0, -80.0, 44.0, 28.0), new Vec2(0.0, -68.0)),
            new CalloutRegion("COURT", "uptown", new Rect2(-46.0, -74.0, 24.0, 30.0), new Vec2(-28.0, -58.0)),
            new CalloutRegion("BRIDGE", "mid", new Rect2(-18.0, -14.0, 36.0, 28.0), new Vec2(0.0, 0.0)),
            new CalloutRegion("CHAPEL", "riverside", new Rect2(-22.0, 52.0, 44.0, 28.0), new Vec2(0.0, 66.0)),
            new CalloutRegion("SARI-SARI", "riverside", new Rect2(-26.0, 14.0, 32.0, 34.0), new Vec2(-12.0, 30.0)),
            new CalloutRegion("CREEK", "mid", new Rect2(22.0, -24.0, 48.0, 48.0), new Vec2(42.0, 0.0)),
        };

        static readonly ClimbSpot[] DefaultClimbSpots =
        {
            new ClimbSpot("backboard_stand", "BACKBOARD", "uptown", new Vec2(-10.6, -56.4), 2.6, 2.2),
            new ClimbSpot("sari_sari_roof", "SARI-SARI ROOF", "riverside", new Vec2(-9.8, 16.4), 3.1, 3.8),
            new ClimbSpot("bridge_jeepney", "JEEPNEY", "mid", new Vec2(5.5, 12.0), 2.3, 2.6),
        };

        static readonly VendorSpot[] DefaultVendors =
        {
            new VendorSpot("mang_taho", "Mang Taho", "left_alley", new Vec2(-42.5, 2.6), 2.2, 40, 2.0, 0.70, 30.0),
            new VendorSpot("fishball_cart", "Fishball Cart", "creek_bank", new Vec2(42.5, -2.6), 2.2, 40, 2.0, 0.70, 30.0),
        };

        static readonly Rect2[] DefaultTallCover =
        {
            new Rect2(-37.0, -29.0, 5.0, 6.0),
            new Rect2(-33.0, 18.0, 5.0, 6.0),
            new Rect2(28.0, -24.0, 5.0, 6.0),
            new Rect2(32.0, 23.0, 5.0, 6.0),
            new Rect2(-15.5, -8.5, 4.5, 4.0),
            new Rect2(11.0, 4.5, 4.5, 4.0),
        };

        static readonly Vec2[] DefaultSelectaLoop =
        {
            new Vec2(-36.5, -6.2), new Vec2(-24.0, -6.2), new Vec2(-12.0, -5.8), new Vec2(0.0, -5.8),
            new Vec2(12.0, -5.2), new Vec2(24.0, -4.2), new Vec2(36.5, -4.2), new Vec2(36.5, 6.2),
            new Vec2(24.0, 6.2), new Vec2(12.0, 5.8), new Vec2(0.0, 5.8), new Vec2(-12.0, 5.2),
            new Vec2(-24.0, 4.2), new Vec2(-36.5, 4.2),
        };

        /// <summary>A copy (Godot's Resource.duplicate()): the lists are new, so a test or a street game can change them.</summary>
        public MapLayout Duplicate()
        {
            var copy = (MapLayout)MemberwiseClone();
            copy.PowerupSpots = new List<Vec2>(PowerupSpots);
            copy.Obstacles = new List<Rect2>(Obstacles);
            copy.ObstacleYawsDeg = new List<float>(ObstacleYawsDeg);
            copy.ObstacleScales = new List<float>(ObstacleScales);
            copy.ObstacleProps = new List<string>(ObstacleProps);
            copy.WaterObstacles = new List<Rect2>(WaterObstacles);
            copy.CalloutRegions = new List<OrderedDict<string, object>>(CalloutRegions);
            copy.ClimbSpots = new List<OrderedDict<string, object>>(ClimbSpots);
            copy.TallCoverZones = new List<Rect2>(TallCoverZones);
            copy.Vendors = new List<OrderedDict<string, object>>(Vendors);
            copy.SelectaWaypoints = new List<Vec2>(SelectaWaypoints);
            copy._cachedCallouts = null;
            copy._cachedCalloutsCount = -1;
            copy._cachedClimbSpots = null;
            copy._cachedClimbSpotsCount = -1;
            copy._cachedVendors = null;
            copy._cachedVendorsCount = -1;
            return copy;
        }

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

        // ---- Asymmetric Barangay Living World Queries (docs/MAPS.md, docs/SPEC.md §6) -------------------------------------------

        /// <summary>Base center for <see cref="HalfUptown"/> (-1: Barangay Hall steps) or <see cref="HalfRiverside"/> (+1: Chapel yard).</summary>
        public Vec2 AsymmetricBaseCenter(int half) => half == HalfUptown ? UptownBaseCenter : RiversideBaseCenter;

        /// <summary>Offset jail center (~15 m off to the side of the half's base: Uptown covered court vs Riverside sari-sari back lot).</summary>
        public Vec2 AsymmetricJailCenter(int half) => half == HalfUptown ? UptownJailCenter : RiversideJailCenter;

        /// <summary>Name of the map half ("UPTOWN" for -1, "RIVERSIDE" for +1).</summary>
        public static string HalfDisplayName(int half) => half == HalfUptown ? "UPTOWN" : "RIVERSIDE";

        /// <summary>Canonical short CODM-style callout regions (loaded from JSON, falling back to Barangay defaults).</summary>
        public IReadOnlyList<CalloutRegion> GetCalloutRegions()
        {
            int count = CalloutRegions != null ? CalloutRegions.Count : 0;
            if (count == 0) return DefaultCallouts;
            if (_cachedCallouts != null && _cachedCalloutsCount == count) return _cachedCallouts;

            var parsed = new CalloutRegion[count];
            for (int i = 0; i < count; i++)
            {
                OrderedDict<string, object> o = CalloutRegions[i];
                string name = DictStr(o, "name", "");
                string half = DictStr(o, "half", "mid");
                double x = DictNum(o, "x", 0.0);
                double y = DictNum(o, "y", 0.0);
                double w = DictNum(o, "w", 0.0);
                double h = DictNum(o, "h", 0.0);
                var bounds = new Rect2(x, y, w, h);
                Vec2 center = bounds.GetCenter();
                double lx = DictNum(o, "label_x", center.x);
                double ly = DictNum(o, "label_y", center.y);
                parsed[i] = new CalloutRegion(name, half, bounds, new Vec2(lx, ly));
            }
            _cachedCallouts = parsed;
            _cachedCalloutsCount = count;
            return _cachedCallouts;
        }

        /// <summary>Returns the short callout name containing <paramref name="pos"/> (or nearest within 14 m), else "".</summary>
        public string CalloutAt(Vec2 pos)
        {
            IReadOnlyList<CalloutRegion> regions = GetCalloutRegions();
            for (int i = 0; i < regions.Count; i++)
                if (regions[i].Bounds.HasPoint(pos)) return regions[i].Name;

            string nearest = "";
            double bestSq = 14.0 * 14.0;
            for (int i = 0; i < regions.Count; i++)
            {
                double dSq = pos.DistanceSquaredTo(regions[i].LabelPos);
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    nearest = regions[i].Name;
                }
            }
            return nearest;
        }

        /// <summary>The 3 high-ground climb spots on Barangay (backboard stand, sari-sari roof, parked jeepney by bridge).</summary>
        public IReadOnlyList<ClimbSpot> GetClimbSpots()
        {
            int count = ClimbSpots != null ? ClimbSpots.Count : 0;
            if (count == 0) return DefaultClimbSpots;
            if (_cachedClimbSpots != null && _cachedClimbSpotsCount == count) return _cachedClimbSpots;

            var parsed = new ClimbSpot[count];
            for (int i = 0; i < count; i++)
            {
                OrderedDict<string, object> o = ClimbSpots[i];
                parsed[i] = new ClimbSpot(
                    DictStr(o, "id", "climb_" + i),
                    DictStr(o, "label", "CLIMB"),
                    DictStr(o, "half", "mid"),
                    new Vec2(DictNum(o, "x", 0.0), DictNum(o, "y", 0.0)),
                    DictNum(o, "height", 2.4),
                    DictNum(o, "radius", 2.2));
            }
            _cachedClimbSpots = parsed;
            _cachedClimbSpotsCount = count;
            return _cachedClimbSpots;
        }

        /// <summary>True when <paramref name="pos"/> is within climb reach of a climbable spot.</summary>
        public bool TryGetClimbSpotAt(Vec2 pos, out ClimbSpot spot)
        {
            IReadOnlyList<ClimbSpot> spots = GetClimbSpots();
            for (int i = 0; i < spots.Count; i++)
            {
                double r = spots[i].Radius;
                if (pos.DistanceSquaredTo(spots[i].Position) <= r * r)
                {
                    spot = spots[i];
                    return true;
                }
            }
            spot = default;
            return false;
        }

        /// <summary>All tall-cover stealth zones (sampayan laundry / cane clumps), falling back to Barangay defaults.</summary>
        public IReadOnlyList<Rect2> GetTallCoverZones() =>
            (TallCoverZones != null && TallCoverZones.Count > 0) ? (IReadOnlyList<Rect2>)TallCoverZones : DefaultTallCover;

        /// <summary>True when <paramref name="pos"/> stands inside any tall-cover stealth zone.</summary>
        public bool IsInTallCover(Vec2 pos)
        {
            IReadOnlyList<Rect2> zones = GetTallCoverZones();
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].HasPoint(pos)) return true;
            return false;
        }

        /// <summary>True when <paramref name="pos"/> stands on one of the 3 dry Estero avenue bridges (West, Center, East).</summary>
        public bool IsOnBridge(Vec2 pos)
        {
            double hw = BridgeClearHalfWidth > 0.0 ? BridgeClearHalfWidth : 4.8;
            double z = pos.y;
            if (Math.Abs(pos.x - LaneCenterX(z)) <= hw) return true;
            if (Math.Abs(pos.x - LaneWestX(z)) <= hw) return true;
            if (Math.Abs(pos.x - LaneEastX(z)) <= hw) return true;
            return false;
        }

        /// <summary>True when <paramref name="pos"/> is wading in the shallow provincial river off the 3 dry bridges.</summary>
        public bool IsInShallowWater(Vec2 pos)
        {
            if (ArenaRadius <= 0.0) return false;
            double halfW = ShallowWaterHalfWidth > 0.0 ? ShallowWaterHalfWidth : 3.75;
            if (Math.Abs(pos.y - CanalZ(pos.x)) > halfW) return false;
            return !IsOnBridge(pos);
        }

        /// <summary>Terrain movement multiplier at <paramref name="pos"/> (0.85 in shallow river water, 1.0 on bridges &amp; streets).</summary>
        public double GetTerrainSpeedMultiplier(Vec2 pos)
        {
            if (!IsInShallowWater(pos)) return 1.0;
            return ShallowWaterSpeedMult > 0.0 && ShallowWaterSpeedMult < 1.0 ? ShallowWaterSpeedMult : 0.85;
        }

        /// <summary>Fixed untargetable healing vendors (Mang Taho &amp; Fishball Cart), falling back to Barangay defaults.</summary>
        public IReadOnlyList<VendorSpot> GetVendorSpots()
        {
            int count = Vendors != null ? Vendors.Count : 0;
            if (count == 0) return DefaultVendors;
            if (_cachedVendors != null && _cachedVendorsCount == count) return _cachedVendors;

            var parsed = new VendorSpot[count];
            for (int i = 0; i < count; i++)
            {
                OrderedDict<string, object> o = Vendors[i];
                parsed[i] = new VendorSpot(
                    DictStr(o, "id", "vendor_" + i),
                    DictStr(o, "name", "Vendor"),
                    DictStr(o, "lane", ""),
                    new Vec2(DictNum(o, "x", 0.0), DictNum(o, "y", 0.0)),
                    DictNum(o, "radius", 2.2),
                    (int)DictNum(o, "heal_amount", 40.0),
                    DictNum(o, "eat_seconds", 2.0),
                    DictNum(o, "eat_slow_mult", 0.70),
                    DictNum(o, "restock_seconds", 30.0));
            }
            _cachedVendors = parsed;
            _cachedVendorsCount = count;
            return _cachedVendors;
        }

        /// <summary>River-road patrol loop waypoints for the Selecta ice-cream bike.</summary>
        public IReadOnlyList<Vec2> GetSelectaWaypoints() =>
            (SelectaWaypoints != null && SelectaWaypoints.Count > 0) ? (IReadOnlyList<Vec2>)SelectaWaypoints : DefaultSelectaLoop;

        /// <summary>Static canonical defaults for views that hold <c>MapLayoutData</c> without a <see cref="MapLayout"/> instance.</summary>
        public static IReadOnlyList<CalloutRegion> DefaultBarangayCallouts() => DefaultCallouts;
        public static IReadOnlyList<ClimbSpot> DefaultBarangayClimbSpots() => DefaultClimbSpots;
        public static IReadOnlyList<VendorSpot> DefaultBarangayVendors() => DefaultVendors;
        public static IReadOnlyList<Rect2> DefaultBarangayTallCoverZones() => DefaultTallCover;
        public static IReadOnlyList<Vec2> DefaultBarangaySelectaWaypoints() => DefaultSelectaLoop;

        static string DictStr(OrderedDict<string, object> dict, string key, string fallback)
        {
            if (dict != null && dict.TryGetValue(key, out object v) && v is string s) return s;
            return fallback;
        }

        static double DictNum(OrderedDict<string, object> dict, string key, double fallback)
        {
            if (dict != null && dict.TryGetValue(key, out object v) && v != null) return MiniJson.Num(v);
            return fallback;
        }
    }
}
