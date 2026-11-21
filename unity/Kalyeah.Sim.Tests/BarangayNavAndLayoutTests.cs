using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Ports of test_barangay_5v5.gd (sections 1-2), test_map_layout.gd, and test_corner_bases.gd:
    /// circular 84 m Barangay geometry, 58x58 3 m grid round-trip, 32 point-symmetric parcels,
    /// dry Merienda bridgehead stands, creek walk-block vs projectile pass-through, legacy rectangular
    /// rounded-corner street fallback, and deterministic BarangayNav A* routing.
    /// </summary>
    public class BarangayNavAndLayoutTests
    {
        static readonly int[] Sides = { MapLayout.SideOwn, MapLayout.SideEnemy };

        static GameRules NoTripRules()
        {
            GameRules copy = Rules();
            copy.TripChance = 0.0;
            return copy;
        }

        static MapLayout OpenCircularLayout() => new MapLayout
        {
            Tiles = new Vec2I(58, 58),
            TileSize = 3.0,
            ArenaRadius = 84.0,
            BaseInset = 16.0,
            BaseRadius = 12.5,
            BaseCorner = 3.0,
            SpawnOffset = 6.0,
            RescueSize = new Vec2(8.0, 14.0),
            TaggedPlaces = 5,
            BoundaryThickness = 2.0,
            PowerupSpots = new List<Vec2>
            {
                new Vec2(-31.5, 4.8),
                new Vec2(5.8, 6.4),
                new Vec2(-5.8, -6.4),
                new Vec2(31.5, -4.8),
            },
        };

        static MapLayout RectCornerLayout() => new MapLayout
        {
            Tiles = new Vec2I(16, 32),
            TileSize = 3.0,
            ArenaRadius = 0.0,
            BaseInset = 6.0,
            BaseRadius = 6.0,
            BaseCorner = 4.0,
            SpawnOffset = 1.5,
            RescueSize = new Vec2(6.0, 9.0),
            TaggedPlaces = 3,
            Obstacles = new List<Rect2>(),
            WaterObstacles = new List<Rect2>(),
        };

        static bool RectApprox(Rect2 a, Rect2 b) =>
            a.Position.IsEqualApprox(b.Position) && a.Size.IsEqualApprox(b.Size);

        [Test]
        public void CircularBarangayArenaRadiusInArenaClampAndGridRoundTrip()
        {
            Assert.IsTrue(Layout.IsCircular(), "circular Barangay arena");
            Assert.AreEqual(84.0, Layout.ArenaRadius, 0.001);
            Assert.AreEqual(new Vec2I(58, 58), Layout.Tiles);
            Assert.AreEqual(3.0, Layout.TileSize, 1e-4);
            Assert.AreEqual(174.0, Layout.LaneWidth, 1e-4);
            Assert.AreEqual(174.0, Layout.LaneLength, 1e-4);
            Assert.AreEqual(new Vec2(174.0, 174.0), Layout.LaneRect().Size);

            Assert.AreEqual(new Vec2(-85.5, -85.5), Layout.TileCenter(new Vec2I(0, 0)));
            Assert.AreEqual(new Vec2(85.5, 85.5), Layout.TileCenter(new Vec2I(57, 57)));
            foreach (Vec2I tile in new[] { new Vec2I(0, 0), new Vec2I(28, 28), new Vec2I(29, 29), new Vec2I(57, 57) })
                Assert.AreEqual(tile, Layout.TileAt(Layout.TileCenter(tile)), $"round trip {tile}");
            Assert.AreEqual(new Vec2(29.0, 29.0), Layout.ToTiles(Vec2.Zero));

            Assert.IsTrue(Layout.InArena(Vec2.Zero));
            Assert.IsTrue(Layout.InArena(new Vec2(83.5, 0.0)));
            Assert.IsTrue(Layout.InArena(new Vec2(0.0, -83.5)));
            Assert.IsTrue(Layout.InArena(new Vec2(58.0, 58.0)));
            Assert.IsFalse(Layout.InArena(new Vec2(84.5, 0.0)));
            Assert.IsFalse(Layout.InArena(new Vec2(70.0, 70.0)));
            Assert.IsFalse(Layout.InArena(new Vec2(-Layout.LaneWidth * 0.5, Layout.LaneLength * 0.5)));

            double pad = Data.GameRules.PlayerRadius;
            var inside = new Vec2(24.0, -18.0);
            Assert.IsTrue(Layout.ClampToArena(inside, pad).IsEqualApprox(inside));

            Vec2 clampedEast = Layout.ClampToArena(new Vec2(120.0, 0.0), pad);
            Assert.AreEqual(84.0 - pad, clampedEast.Length(), 0.01);
            Assert.AreEqual(0.0, clampedEast.y, 0.001);

            Vec2 clampedDiag = Layout.ClampToArena(new Vec2(80.0, 80.0), pad);
            Assert.AreEqual(84.0 - pad, clampedDiag.Length(), 0.01);

            var sim = new MatchSim(Kit.Rules(), OpenCircularLayout(), 101);
            PlayerState runner = sim.AddPlayer(1, 0);
            runner.Position = new Vec2(82.0, 0.0);
            Run(sim, 1, new Vec2(1.0, 0.0), 45);
            Assert.LessOrEqual(runner.Position.Length(), 84.0 - runner.Radius + 0.02);

            Rect2 lane = Layout.LaneRect();
            List<Rect2> boundaries = Layout.BoundaryRects();
            Assert.AreEqual(4, boundaries.Count);
            foreach (Rect2 rect in boundaries)
                Assert.IsFalse(rect.Intersects(lane));

            Assert.AreEqual(11.8, Layout.PostHeight, 0.001);
            Assert.AreEqual(0.72, Layout.PostWidthShare, 0.001);
            foreach (int side in Sides)
                Assert.IsTrue(Layout.InBase(side, Layout.PostPosition(side)));
        }

        [Test]
        public void BasesRescueAreasAndFiveVsFiveTaggedSidewalkPlacesAreSymmetricAndNonOverlapping()
        {
            Vec2 ownBase = Layout.BaseCenter(MapLayout.SideOwn);
            Vec2 enemyBase = Layout.BaseCenter(MapLayout.SideEnemy);
            Assert.AreEqual(0.0, ownBase.x, 0.001);
            Assert.Greater(ownBase.y, 0.0);
            Assert.AreEqual(0.0, enemyBase.x, 0.001);
            Assert.Less(enemyBase.y, 0.0);
            Assert.AreEqual(-ownBase, enemyBase);
            Assert.IsTrue(Layout.InArena(ownBase));
            Assert.IsTrue(Layout.InArena(enemyBase));

            foreach (int side in Sides)
            {
                Rect2 baseRect = Layout.BaseRect(side);
                Assert.IsTrue(Layout.InArena(baseRect.GetCenter()));
                Assert.IsTrue(Layout.InBase(side, baseRect.GetCenter()));
                Assert.IsTrue(Layout.InBase(side, baseRect.Position + new Vec2(Layout.BaseCorner, 0.05)));
                Assert.IsFalse(Layout.InBase(side, baseRect.Position + new Vec2(0.05, 0.05)));
                Assert.IsFalse(Layout.InBase(side, baseRect.End - new Vec2(0.05, 0.05)));
                Assert.IsFalse(Layout.InBase(side, baseRect.Position + new Vec2(baseRect.Size.x + 0.1, 0.0)));
            }
            Assert.Greater(Layout.BaseCorner, 0.0);
            Assert.Less(Layout.BaseCorner, Layout.BaseRadius / 3.0);
            Assert.IsTrue(Layout.InBase(MapLayout.SideEnemy, enemyBase + new Vec2(0.0, Layout.BaseRadius - 0.1)));
            Assert.IsFalse(Layout.InBase(MapLayout.SideEnemy, enemyBase + new Vec2(0.0, Layout.BaseRadius + 0.1)));
            Assert.IsFalse(Layout.InBase(MapLayout.SideOwn, enemyBase));
            Assert.IsTrue(Layout.InBase(MapLayout.SideOwn, Layout.SpawnCenter(MapLayout.SideOwn)));

            Rect2 ownRescue = Layout.RescueRect(MapLayout.SideOwn);
            Rect2 enemyRescue = Layout.RescueRect(MapLayout.SideEnemy);
            Assert.Less(ownRescue.GetCenter().x, 0.0);
            Assert.Less(ownRescue.GetCenter().y, 0.0);
            Assert.Greater(enemyRescue.GetCenter().x, 0.0);
            Assert.Greater(enemyRescue.GetCenter().y, 0.0);
            Assert.AreEqual(Layout.RescueSize, ownRescue.Size);
            Assert.AreEqual(-ownRescue.End.x, enemyRescue.Position.x, 0.001);
            Assert.AreEqual(-ownRescue.End.y, enemyRescue.Position.y, 0.001);
            foreach (int side in Sides)
            {
                Rect2 rRect = Layout.RescueRect(side);
                Assert.IsTrue(Layout.InArena(rRect.GetCenter()));
                foreach (int other in Sides)
                    Assert.IsFalse(Layout.InBase(other, rRect.GetCenter()));
            }

            Assert.GreaterOrEqual(Layout.TaggedPlaces, 5);
            foreach (int side in Sides)
            {
                Rect2 area = Layout.RescueRect(side);
                for (int index = 0; index < Layout.TaggedPlaces; index++)
                {
                    Vec2 place = Layout.TaggedPlace(side, index);
                    Assert.IsFalse(Layout.InArena(place));
                    Assert.AreEqual(Math.Sign(area.GetCenter().x), Math.Sign(place.x));
                    Assert.That(place.y, Is.InRange(area.Position.y - 2.0, area.End.y + 2.0));
                    Assert.AreEqual(-Math.Sign(place.x), Math.Sign(Layout.TaggedFacing(side).x));
                }
            }
            Assert.AreEqual(Layout.TaggedPlace(MapLayout.SideOwn, 0), Layout.TaggedPlace(MapLayout.SideOwn, Layout.TaggedPlaces));
            for (int index = 0; index < Layout.TaggedPlaces - 1; index++)
            {
                double gap = Layout.TaggedPlace(MapLayout.SideOwn, index + 1).DistanceTo(Layout.TaggedPlace(MapLayout.SideOwn, index));
                Assert.GreaterOrEqual(gap, Data.GameRules.PlayerRadius * 2.0, $"tagged places {index} and {index + 1} do not overlap");
            }
        }

        [Test]
        public void SymmetricObstaclesMeriendaStandsAndWaterBlockWalkButNotProjectiles()
        {
            List<Rect2> obs = Layout.ObstacleRects();
            int halfObs = Layout.Obstacles.Count;
            Assert.AreEqual(16, halfObs);
            Assert.AreEqual(32, obs.Count);
            Assert.AreEqual(32, Layout.ParcelCount());
            Assert.IsTrue(Layout.HasOrientedParcels());
            Assert.AreEqual(halfObs, Layout.ObstacleYawsDeg.Count);
            Assert.AreEqual(halfObs, Layout.ObstacleScales.Count);

            int organicRotated = 0;
            for (int i = 0; i < halfObs; i++)
            {
                var expectedMirror = new Rect2(-obs[i].End, obs[i].Size);
                Assert.IsTrue(RectApprox(obs[i + halfObs], expectedMirror), $"obstacle {i} mirrors around origin");
                Assert.IsTrue(RectApprox(Layout.ParcelRect(i + halfObs), expectedMirror));
                Assert.That(Layout.ObstacleScales[i], Is.InRange(0.90f, 1.10f));
                Assert.AreEqual(Layout.ParcelScale(i), Layout.ParcelScale(i + halfObs), 1e-6);
                double modYaw = GMath.Fposmod(Math.Abs(Layout.ObstacleYawsDeg[i]), 90.0);
                if (modYaw > 5.0 && modYaw < 85.0) organicRotated++;
            }
            Assert.GreaterOrEqual(organicRotated, 8);

            Assert.AreEqual(0, Layout.WaterObstacles.Count);
            Assert.AreEqual(0, Layout.WaterRects().Count);
            Assert.AreEqual(obs.Count + Layout.LowCoverRects().Count, Layout.WalkObstacleRects().Count, "buildings and low covers stop walking");
            Assert.AreNotEqual(0.0, MapLayout.LaneCenterX(20.0));
            Assert.AreEqual(-MapLayout.CanalZ(24.0), MapLayout.CanalZ(-24.0), 1e-4);
            Assert.AreEqual(-MapLayout.LaneEastX(-18.0), MapLayout.LaneWestX(18.0), 1e-4);
            Assert.AreEqual(-MapLayout.EskinitaSouthZ(-18.0), MapLayout.EskinitaNorthZ(18.0), 1e-4);

            CollectionAssert.AreEqual(
                new[] { new Vec2(-31.5, 4.8), new Vec2(5.8, 6.4), new Vec2(-5.8, -6.4), new Vec2(31.5, -4.8) },
                Layout.PowerupSpots);
            foreach (Vec2 spot in Layout.PowerupSpots)
                Assert.Greater(Math.Abs(spot.y - MapLayout.CanalZ(spot.x)), 3.5, $"dry Merienda bank at {spot}");

            MapLayout creekLayout = OpenCircularLayout();
            creekLayout.WaterObstacles = new List<Rect2> { new Rect2(-12.0, 1.0, 24.0, 2.0) };
            var sim = new MatchSim(NoTripRules(), creekLayout, 202);
            PlayerState shooter = sim.AddPlayer(1, 0);
            PlayerState target = sim.AddPlayer(2, 1);
            shooter.Position = new Vec2(0.0, 5.5);
            target.Position = new Vec2(0.0, -5.5);

            Run(sim, 1, new Vec2(0.0, -1.0), 35);
            Assert.GreaterOrEqual(shooter.Position.y, 3.0 + shooter.Radius - 0.05, "creek channel blocks walking");

            int hpBefore = target.Hp;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnPrimary, sim.Tick).WithAimPoint(target.Position));
            sim.Step(DT);
            Assert.Greater(sim.Weapons.Projectiles.Count, 0);
            Run(sim, 1, Vec2.Zero, 30);
            Assert.Less(target.Hp, hpBefore, "Tirador projectile crosses water_obstacles unimpeded");
        }

        [Test]
        public void BarangayNavRoutesDeterministicallyAcrossAllThreeCanalFordsAndDetoursDeepWater()
        {
            var navA = new BarangayNav(Layout, Data.GameRules.PlayerRadius);
            var navB = new BarangayNav(Layout, Data.GameRules.PlayerRadius);
            Assert.Greater(navA.Waypoints.Count, 8);
            CollectionAssert.AreEqual(navA.Waypoints, navB.Waypoints);

            List<Rect2> walkRects = Layout.WalkObstacleRects();
            foreach (Vec2 wp in navA.Waypoints)
            {
                Assert.IsTrue(Layout.InArena(wp), $"waypoint {wp} inside arena");
                foreach (Rect2 rect in walkRects)
                    Assert.IsFalse(rect.HasPoint(wp), $"waypoint {wp} clear of {rect}");
            }

            (Vec2 south, Vec2 north)[] fordPairs =
            {
                (new Vec2(MapLayout.LaneWestX(10.0), 10.0), new Vec2(MapLayout.LaneWestX(-10.0), -10.0)),
                (new Vec2(MapLayout.LaneCenterX(10.0), 10.0), new Vec2(MapLayout.LaneCenterX(-10.0), -10.0)),
                (new Vec2(MapLayout.LaneEastX(10.0), 10.0), new Vec2(MapLayout.LaneEastX(-10.0), -10.0)),
            };
            foreach (var (south, north) in fordPairs)
                Assert.IsTrue(navA.IsClear(south, north), $"ford {south} -> {north}");

            Vec2 start = Layout.SpawnCenter(MapLayout.SideOwn);
            Vec2 goal = Layout.SpawnCenter(MapLayout.SideEnemy);
            Vec2 pos = start;
            const double stepDist = 2.0;
            bool reached = false;
            for (int stepIdx = 0; stepIdx < 220; stepIdx++)
            {
                Vec2 wpA = navA.NextWaypoint(pos, goal);
                Vec2 wpB = navB.NextWaypoint(pos, goal);
                Assert.IsTrue(wpA.IsEqualApprox(wpB), $"deterministic A* at {pos}");
                Vec2 dir = wpA - pos;
                pos = dir.Length() <= stepDist ? wpA : pos + dir.Normalized() * stepDist;
                if (pos.DistanceTo(goal) <= 2.5)
                {
                    reached = true;
                    break;
                }
            }
            Assert.IsTrue(reached, "routes from South spawn to North spawn across Barangay pathways");

            MapLayout creekLayout = OpenCircularLayout();
            creekLayout.WaterObstacles = new List<Rect2> { new Rect2(-18.0, -2.0, 36.0, 4.0) };
            var creekNav = new BarangayNav(creekLayout, Data.GameRules.PlayerRadius);
            var southBank = new Vec2(0.0, 5.0);
            var northBank = new Vec2(0.0, -5.0);
            Assert.IsFalse(creekNav.IsClear(southBank, northBank));
            Vec2 detour = creekNav.NextWaypoint(southBank, northBank);
            Assert.IsFalse(detour.IsEqualApprox(northBank));
        }

        [Test]
        public void ZeroArenaRadiusPreservesRoundedRectangularStreetAndCornerSlide()
        {
            MapLayout rectLayout = RectCornerLayout();
            Assert.IsFalse(rectLayout.IsCircular());
            Assert.IsTrue(rectLayout.InArena(new Vec2(20.0, 42.0)));

            Rect2 street = rectLayout.LaneRect();
            Vec2 Corner(int s) => new Vec2(-s * rectLayout.LaneWidth / 2.0, s * rectLayout.LaneLength / 2.0);
            Vec2 Outward(int s) => (Corner(s) - rectLayout.PostPosition(s)).Normalized();
            bool Fits(Vec2 p, double r) =>
                MapLayout.RoundedHasPoint(street.Grow(0.001 - r), GMath.Maxf(rectLayout.BaseCorner - r, 0.0) + 0.001, p);

            foreach (int side in Sides)
            {
                Vec2 centre = rectLayout.PostPosition(side);
                Vec2 inside = centre + Outward(side) * rectLayout.BaseCorner * 0.99;
                Vec2 outside = centre + Outward(side) * rectLayout.BaseCorner * 1.02;
                Assert.IsTrue(MapLayout.RoundedHasPoint(street, rectLayout.BaseCorner, inside));
                Assert.IsFalse(MapLayout.RoundedHasPoint(street, rectLayout.BaseCorner, outside));
                Assert.IsTrue(rectLayout.InBase(side, inside));
                Assert.IsFalse(rectLayout.InBase(side, outside));
                Assert.AreEqual(rectLayout.LaneWidth / 2.0 - rectLayout.BaseCorner, Math.Abs(centre.x), 1e-4);
                Assert.AreEqual(rectLayout.LaneLength / 2.0 - rectLayout.BaseCorner, Math.Abs(centre.y), 1e-4);
                Assert.IsTrue(rectLayout.InBase(side, centre));
            }
            Assert.AreEqual(-rectLayout.PostPosition(MapLayout.SideEnemy), rectLayout.PostPosition(MapLayout.SideOwn));

            Rect2 ownBase = rectLayout.BaseRect(MapLayout.SideOwn);
            Rect2 enemyBase = rectLayout.BaseRect(MapLayout.SideEnemy);
            Assert.AreEqual(street.Position.x, ownBase.Position.x);
            Assert.AreEqual(street.End.y, ownBase.End.y);
            Assert.AreEqual(street.End.x, enemyBase.End.x);
            Assert.AreEqual(street.Position.y, enemyBase.Position.y);

            foreach (double radius in new[] { 0.0, Data.GameRules.PlayerRadius })
            {
                foreach (int side in Sides)
                {
                    Vec2 held = rectLayout.KeepInStreet(Corner(side) * 4.0, radius);
                    Vec2 expected = rectLayout.PostPosition(side) + Outward(side) * (rectLayout.BaseCorner - radius);
                    Assert.AreEqual(expected.x, held.x, 1e-3);
                    Assert.AreEqual(expected.y, held.y, 1e-3);
                }
            }

            double rad = Data.GameRules.PlayerRadius;
            double edge = rectLayout.LaneWidth / 2.0 - rad;
            foreach (Vec2 pt in new[] { Vec2.Zero, new Vec2(-edge, 0.0), new Vec2(edge, 10.0), new Vec2(3.0, rectLayout.LaneLength / 2.0 - rad) })
                Assert.AreEqual(pt, rectLayout.KeepInStreet(pt, rad));
            Assert.AreEqual(new Vec2(edge, 0.0), rectLayout.KeepInStreet(new Vec2(100.0, 0.0), rad));

            var sim = new MatchSim(NoTripRules(), rectLayout, 1);
            PlayerState kid = sim.AddPlayer(1, 0);
            PlayerState foe = sim.AddPlayer(2, 1);
            foe.Position = new Vec2(12.0, -20.0);

            kid.Position = new Vec2(-rectLayout.LaneWidth / 2.0 + 4.0, rectLayout.LaneLength / 2.0 - kid.Radius);
            Run(sim, 1, new Vec2(-1.0, 0.0), 560);
            Assert.AreEqual(-rectLayout.LaneWidth / 2.0 + kid.Radius, kid.Position.x, 0.05);
            Assert.AreEqual(rectLayout.LaneLength / 2.0 - rectLayout.BaseCorner, kid.Position.y, rectLayout.BaseCorner * 0.05);
            Assert.IsTrue(Fits(kid.Position, kid.Radius));

            foreach (int side in Sides)
            {
                kid.Position = rectLayout.PostPosition(side);
                foreach (Vec2 dir in new[] { new Vec2(-1.0, 1.0), new Vec2(1.0, 1.0), new Vec2(1.0, -1.0), new Vec2(-1.0, -1.0) })
                {
                    Run(sim, 1, dir.Normalized(), 120);
                    Assert.IsTrue(Fits(kid.Position, kid.Radius));
                }
            }

            kid.Position = new Vec2(-rectLayout.LaneWidth / 2.0 + 6.0, rectLayout.LaneLength / 2.0 - 8.0);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick)
                .WithMoveTarget(new Vec2(-rectLayout.LaneWidth / 2.0 - 2.0, rectLayout.LaneLength / 2.0 + 1.0)));
            sim.Step(DT);
            Vec2 moveTarget = kid.MoveTarget;
            Assert.IsTrue(Fits(moveTarget, kid.Radius));
            Vec2 expectedCurve = rectLayout.PostPosition(MapLayout.SideOwn) + Outward(MapLayout.SideOwn) * (rectLayout.BaseCorner - kid.Radius);
            Assert.AreEqual(expectedCurve.x, moveTarget.x, 1e-3);
            Assert.AreEqual(expectedCurve.y, moveTarget.y, 1e-3);
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick));
            Steps(sim, 240);
            Assert.IsFalse(kid.HasMoveTarget);
            Assert.Less(kid.Position.DistanceTo(moveTarget), Data.GameRules.MoveArrivalRadius + 0.001);

            kid.Position = rectLayout.KeepInStreet(rectLayout.PostPosition(MapLayout.SideEnemy) + Outward(MapLayout.SideEnemy) * 5.0, kid.Radius);
            Assert.IsTrue(rectLayout.InBase(MapLayout.SideEnemy, kid.Position));
        }
    }
}
