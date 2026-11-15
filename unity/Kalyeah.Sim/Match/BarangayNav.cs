using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Deterministic waypoint A* navigation over MapLayout's barangay (port of barangay_nav.gd). Pure data + math. The scores are kept in
    /// 32-bit floats like the GDScript's PackedFloat32Array, so ties break the same way.
    /// </summary>
    public sealed class BarangayNav
    {
        public const double CornerMargin = 0.95;
        public const double ClearanceMargin = 0.16;
        public const double EdgeExtraMargin = 0.08;
        public const double MaxEdgeLength = 72.0;
        public const double MinWaypointDistSq = 3.24;

        public readonly List<Vec2> Waypoints = new List<Vec2>();
        readonly List<List<int>> _neighbors = new List<List<int>>();
        readonly List<List<float>> _weights = new List<List<float>>();
        readonly List<Rect2> _paddedRects = new List<Rect2>();
        readonly List<Rect2> _edgeRects = new List<Rect2>();
        readonly MapLayout _layout;
        readonly Rect2 _bounds;
        readonly double _agentPad = 0.7;
        readonly bool _hasObstacles;

        public BarangayNav(MapLayout layout, double agentRadius = 0.45)
        {
            _layout = layout;
            _agentPad = agentRadius + 0.25;
            List<Rect2> rawRects = layout.WalkObstacleRects();
            _bounds = layout.LaneRect().Grow(-_agentPad);
            _hasObstacles = rawRects.Count > 0;
            if (!_hasObstacles) return;
            double linePad = agentRadius + ClearanceMargin;
            double edgePad = linePad + EdgeExtraMargin;
            foreach (Rect2 rect in rawRects)
            {
                _paddedRects.Add(rect.Grow(linePad));
                _edgeRects.Add(rect.Grow(edgePad));
            }
            BuildWaypoints(layout, rawRects, agentRadius + CornerMargin);
            BuildEdges();
        }

        /// <summary>`goal` (clamped inside the arena) when unobstructed, else the furthest unobstructed waypoint along the A* path.</summary>
        public Vec2 NextWaypoint(Vec2 from, Vec2 goal)
        {
            Vec2 clampedGoal = ClampWalkable(goal);
            if (!_hasObstacles || Waypoints.Count == 0) return clampedGoal;
            if (IsClear(from, clampedGoal)) return clampedGoal;
            List<int> path = FindPath(from, clampedGoal);
            if (path.Count == 0) return clampedGoal;
            Vec2 chosen = Waypoints[path[0]];
            for (int idx = 1; idx < path.Count; idx++)
            {
                Vec2 candidate = Waypoints[path[idx]];
                if (IsClear(from, candidate)) chosen = candidate;
                else break;
            }
            if (path.Count >= 2 && from.DistanceSquaredTo(chosen) < 0.36 && chosen == Waypoints[path[0]]) chosen = Waypoints[path[1]];
            return chosen;
        }

        /// <summary>True when the straight segment a-b stays inside the arena and clears every walk obstacle.</summary>
        public bool IsClear(Vec2 a, Vec2 b)
        {
            if (_layout != null && _layout.ArenaRadius > 0.0)
            {
                if (!_layout.InArena(a, 0.05) || !_layout.InArena(b, 0.05)) return false;
            }
            foreach (Rect2 rect in _paddedRects)
                if (Geometry2D.SegmentHitsRect(a, b, rect)) return false;
            return true;
        }

        bool IsEdgeClear(Vec2 a, Vec2 b)
        {
            foreach (Rect2 rect in _edgeRects)
                if (Geometry2D.SegmentHitsRect(a, b, rect)) return false;
            return true;
        }

        Vec2 ClampWalkable(Vec2 point)
        {
            Vec2 result = _layout != null ? _layout.ClampToArena(point, _agentPad) : point.Clamp(_bounds.Position, _bounds.End);
            for (int pass = 0; pass < 2; pass++)
            {
                bool moved = false;
                foreach (Rect2 rect in _paddedRects)
                {
                    if (!rect.HasPoint(result)) continue;
                    Vec2 end = rect.End;
                    Vec2[] exits =
                    {
                        new Vec2(rect.Position.x - 0.08, result.y),
                        new Vec2(end.x + 0.08, result.y),
                        new Vec2(result.x, rect.Position.y - 0.08),
                        new Vec2(result.x, end.y + 0.08),
                    };
                    Vec2 best = result;
                    double bestD = double.PositiveInfinity;
                    foreach (Vec2 ex in exits)
                    {
                        if (IsWalkablePoint(ex))
                        {
                            double d = ex.DistanceSquaredTo(result);
                            if (d < bestD)
                            {
                                bestD = d;
                                best = ex;
                            }
                        }
                    }
                    if (best != result)
                    {
                        result = best;
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            return result;
        }

        bool IsWalkablePoint(Vec2 point)
        {
            if (_layout != null && _layout.ArenaRadius > 0.0)
            {
                if (!_layout.InArena(point, _agentPad)) return false;
            }
            else if (!_bounds.HasPoint(point)) return false;
            foreach (Rect2 rect in _paddedRects)
                if (rect.HasPoint(point)) return false;
            return true;
        }

        void BuildWaypoints(MapLayout layout, List<Rect2> rawRects, double cornerPad)
        {
            var candidates = new List<Vec2>();
            foreach (Vec2 spot in layout.PowerupSpots) candidates.Add(spot);
            foreach (double canalX in new[] { -74.0, -56.0, -34.0, -17.0, 0.0, 17.0, 34.0, 56.0, 74.0 })
            {
                double cz = layout.ArenaRadius > 0.0 ? MapLayout.CanalZ(canalX) : 0.0;
                candidates.Add(new Vec2(canalX, cz));
                candidates.Add(new Vec2(canalX, cz + 5.6));
                candidates.Add(new Vec2(canalX, cz - 5.6));
            }
            if (layout.ArenaRadius > 0.0)
            {
                foreach (double z in new[] { -64.0, -52.0, -40.0, -28.0, -16.0, -8.0, 0.0, 8.0, 16.0, 28.0, 40.0, 52.0, 64.0 })
                {
                    candidates.Add(new Vec2(MapLayout.LaneCenterX(z), z));
                    candidates.Add(new Vec2(MapLayout.LaneWestX(z), z));
                    candidates.Add(new Vec2(MapLayout.LaneEastX(z), z));
                }
                foreach (double x in new[] { -72.0, -56.0, -41.0, -31.0, -18.0, -4.0, 4.0, 18.0, 31.0, 41.0, 56.0, 72.0 })
                {
                    candidates.Add(new Vec2(x, MapLayout.EskinitaSouthZ(x)));
                    candidates.Add(new Vec2(x, MapLayout.EskinitaNorthZ(x)));
                }
            }
            foreach (int side in new[] { MapLayout.SideOwn, MapLayout.SideEnemy })
            {
                double s = side;
                candidates.Add(layout.BaseCenter(side));
                candidates.Add(layout.SpawnCenter(side));
                Rect2 rescue = layout.RescueRect(side);
                candidates.Add(rescue.GetCenter());
                candidates.Add(new Vec2(rescue.GetCenter().x, rescue.Position.y - 2.4));
                candidates.Add(new Vec2(rescue.GetCenter().x, rescue.End.y + 2.4));
                double inwardRx = side == MapLayout.SideOwn ? rescue.End.x + 2.6 : rescue.Position.x - 2.6;
                candidates.Add(new Vec2(inwardRx, rescue.GetCenter().y));
                foreach (double px in new[] { -54.0, -33.0, -15.5, 0.0, 15.5, 33.0, 54.0 }) candidates.Add(new Vec2(px * s, 49.0 * s));
                foreach (double cx in new[] { -32.0, -15.2, 0.0, 15.2, 32.0 }) candidates.Add(new Vec2(cx * s, 57.5 * s));
                foreach (double rx in new[] { -27.0, -14.5, 14.5, 27.0 }) candidates.Add(new Vec2(rx * s, 69.0 * s));
                candidates.Add(new Vec2(0.0, 78.5 * s));
            }
            if (layout.ArenaRadius > 10.0)
            {
                double ringR = layout.ArenaRadius - 3.6;
                int ringSteps = 24;
                for (int i = 0; i < ringSteps; i++)
                {
                    double angle = GMath.Tau * i / ringSteps;
                    candidates.Add(new Vec2(Math.Cos(angle) * ringR, Math.Sin(angle) * ringR));
                }
            }
            foreach (Rect2 rect in rawRects)
            {
                Rect2 grown = rect.Grow(cornerPad);
                Vec2 end = grown.End;
                candidates.Add(grown.Position);
                candidates.Add(new Vec2(end.x, grown.Position.y));
                candidates.Add(new Vec2(grown.Position.x, end.y));
                candidates.Add(end);
            }
            foreach (Vec2 pt in candidates)
            {
                if (!IsWalkablePoint(pt)) continue;
                bool duplicate = false;
                foreach (Vec2 existing in Waypoints)
                {
                    if (existing.DistanceSquaredTo(pt) < MinWaypointDistSq)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) Waypoints.Add(pt);
            }
        }

        void BuildEdges()
        {
            int count = Waypoints.Count;
            for (int i = 0; i < count; i++)
            {
                _neighbors.Add(new List<int>());
                _weights.Add(new List<float>());
            }
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    double dist = Waypoints[i].DistanceTo(Waypoints[j]);
                    if (dist > MaxEdgeLength) continue;
                    if (IsEdgeClear(Waypoints[i], Waypoints[j]))
                    {
                        _neighbors[i].Add(j);
                        _weights[i].Add((float)dist);
                        _neighbors[j].Add(i);
                        _weights[j].Add((float)dist);
                    }
                }
            }
        }

        List<int> FindPath(Vec2 from, Vec2 goal)
        {
            int count = Waypoints.Count;
            var gScore = new float[count];
            var fScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];
            var goalLink = new bool[count];
            var open = new List<int>();
            int nearestStart = -1;
            double nearestStartD = double.PositiveInfinity;
            int nearestGoal = -1;
            double nearestGoalD = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                fScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
                double dStart = from.DistanceTo(Waypoints[i]);
                if (dStart < nearestStartD)
                {
                    nearestStartD = dStart;
                    nearestStart = i;
                }
                if (dStart <= MaxEdgeLength && IsClear(from, Waypoints[i]))
                {
                    gScore[i] = (float)dStart;
                    fScore[i] = (float)(dStart + Waypoints[i].DistanceTo(goal));
                    open.Add(i);
                }
                double dGoal = goal.DistanceTo(Waypoints[i]);
                if (dGoal < nearestGoalD)
                {
                    nearestGoalD = dGoal;
                    nearestGoal = i;
                }
                goalLink[i] = dGoal <= MaxEdgeLength && IsClear(Waypoints[i], goal);
            }
            if (open.Count == 0 && nearestStart >= 0)
            {
                gScore[nearestStart] = (float)nearestStartD;
                fScore[nearestStart] = (float)(nearestStartD + Waypoints[nearestStart].DistanceTo(goal));
                open.Add(nearestStart);
            }
            if (nearestGoal >= 0)
            {
                bool anyGoal = false;
                for (int i = 0; i < count; i++)
                {
                    if (goalLink[i])
                    {
                        anyGoal = true;
                        break;
                    }
                }
                if (!anyGoal) goalLink[nearestGoal] = true;
            }
            int bestEnd = -1;
            double bestTotal = double.PositiveInfinity;
            while (open.Count > 0)
            {
                int pickIdx = 0;
                int current = open[0];
                double currentF = fScore[current];
                for (int k = 1; k < open.Count; k++)
                {
                    int nodeId = open[k];
                    if (fScore[nodeId] < currentF)
                    {
                        currentF = fScore[nodeId];
                        current = nodeId;
                        pickIdx = k;
                    }
                }
                if (currentF >= bestTotal) break;
                open[pickIdx] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                if (closed[current]) continue;
                closed[current] = true;
                if (goalLink[current])
                {
                    double total = gScore[current] + Waypoints[current].DistanceTo(goal);
                    if (total < bestTotal)
                    {
                        bestTotal = total;
                        bestEnd = current;
                    }
                }
                List<int> nIds = _neighbors[current];
                List<float> nWeights = _weights[current];
                for (int k = 0; k < nIds.Count; k++)
                {
                    int nxt = nIds[k];
                    if (closed[nxt]) continue;
                    double tentative = (double)gScore[current] + nWeights[k];
                    if (tentative < gScore[nxt])
                    {
                        cameFrom[nxt] = current;
                        gScore[nxt] = (float)tentative;
                        fScore[nxt] = (float)(tentative + Waypoints[nxt].DistanceTo(goal));
                        if (!open.Contains(nxt)) open.Add(nxt);
                    }
                }
            }
            var result = new List<int>();
            if (bestEnd < 0) return result;
            int walk = bestEnd;
            while (walk >= 0)
            {
                result.Add(walk);
                walk = cameFrom[walk];
            }
            result.Reverse();
            return result;
        }
    }
}
