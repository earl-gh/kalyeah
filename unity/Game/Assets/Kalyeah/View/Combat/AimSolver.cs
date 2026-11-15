using System;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Result of dual-ray shoulder-camera aim reconciliation: couples the Unity 3D world aim point
    /// (for muzzle VFX, projectiles, and aim telegraphs) with the authoritative sim XZ ground point
    /// fed into PlayerInput.AimPoint.
    /// </summary>
    public readonly struct AimSolution
    {
        /// <summary>Reconciled 3D target in Unity world space (clamped to maxRange and muzzle line-of-sight).</summary>
        public readonly Vector3 WorldTarget;
        /// <summary>3D hand/chest cast origin in Unity world space.</summary>
        public readonly Vector3 WorldOrigin;
        /// <summary>Authoritative ground aim point in sim XZ space (for PlayerInput.AimPoint).</summary>
        public readonly SVector2 SimAimPoint;
        /// <summary>Unit ground aim direction in sim XZ space (falls back to kid facing inside 0.1 m).</summary>
        public readonly SVector2 SimAimDirection;
        /// <summary>Primary camera crosshair intersection in Unity world space prior to muzzle obstruction clamp.</summary>
        public readonly Vector3 CameraRayPoint;
        /// <summary>True when cover between the kid's hand/chest origin and the crosshair point intercepted the shot.</summary>
        public readonly bool WasObstructed;

        public AimSolution(
            Vector3 worldTarget,
            Vector3 worldOrigin,
            SVector2 simAimPoint,
            SVector2 simAimDirection,
            Vector3 cameraRayPoint,
            bool wasObstructed)
        {
            WorldTarget = worldTarget;
            WorldOrigin = worldOrigin;
            SimAimPoint = simAimPoint;
            SimAimDirection = simAimDirection;
            CameraRayPoint = cameraRayPoint;
            WasObstructed = wasObstructed;
        }
    }

    /// <summary>
    /// Dual-ray 3P parallax aim solver (.claude/skills/apex-combat-cam/SKILL.md):
    /// 1. Casts a primary ray through the normalized viewport crosshair (0.5, 0.54) to locate the world/ground target.
    /// 2. Clamps the XZ target to WeaponDef.max_range (and optional arena rim) from the kid's authoritative XZ.
    /// 3. Sweeps a secondary ray from the kid's hand/chest origin to the target so shots never clip corner cover beside the camera.
    /// 4. Falls back cleanly to the kid's facing vector inside aim_no_direction_radius (0.1 m).
    /// </summary>
    public static class AimSolver
    {
        public static readonly Vector2 CrosshairViewport = new Vector2(0.5f, 0.54f);
        public const float DefaultOriginHeight = 1.05f;
        public const float CrouchedOriginHeight = 0.68f;
        public const float DefaultNoDirectionRadius = 0.1f;
        public const float DefaultFallbackReach = 25f;
        public const float DefaultPlayerRadius = 0.45f;

        const float MaxPrimaryRayDistance = 240f;
        const float DefaultBuildingHeight = 3.6f;
        const float MuzzleProbeRadius = 0.08f;

        static readonly RaycastHit[] RayHits = new RaycastHit[8];

        public static AimSolution Solve(
            Camera camera,
            SVector2 kidFeetSim,
            SVector2 kidFacingSim,
            float maxRange,
            MapLayoutData layout = null,
            float originHeight = DefaultOriginHeight,
            float groundY = 0f,
            int collisionMask = Physics.DefaultRaycastLayers,
            Transform ignoreRoot = null,
            Vector2? crosshairViewport = null,
            float noDirectionRadius = DefaultNoDirectionRadius)
        {
            if (camera == null)
            {
                SVector2 facing = SafeFacing(kidFacingSim, new SVector2(0f, -1f));
                float reach = maxRange > 0f ? maxRange : DefaultFallbackReach;
                SVector2 fallbackSim = ClampArena(kidFeetSim + facing * reach, layout);
                Vector3 origin = KalyeahResources.World(kidFeetSim, groundY + Mathf.Max(0.25f, originHeight));
                Vector3 target = KalyeahResources.World(fallbackSim, groundY);
                return new AimSolution(target, origin, fallbackSim, facing, target, false);
            }

            Vector2 vp = crosshairViewport ?? CrosshairViewport;
            Ray primaryRay = camera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
            return SolveRay(
                primaryRay,
                kidFeetSim,
                kidFacingSim,
                maxRange,
                layout,
                originHeight,
                groundY,
                collisionMask,
                ignoreRoot,
                noDirectionRadius);
        }

        public static AimSolution SolveRay(
            Ray primaryRay,
            SVector2 kidFeetSim,
            SVector2 kidFacingSim,
            float maxRange,
            MapLayoutData layout = null,
            float originHeight = DefaultOriginHeight,
            float groundY = 0f,
            int collisionMask = Physics.DefaultRaycastLayers,
            Transform ignoreRoot = null,
            float noDirectionRadius = DefaultNoDirectionRadius)
        {
            Vector3 dir = primaryRay.direction.sqrMagnitude > 1e-8f ? primaryRay.direction.normalized : Vector3.forward;
            var ray = new Ray(primaryRay.origin, dir);

            SVector2 rayForwardSim = GodotSpace.Ground(new SVector3(dir.x, dir.y, dir.z));
            SVector2 fallbackDir = rayForwardSim.LengthSquared() > 1e-6f
                ? SVector2.Normalize(rayForwardSim)
                : new SVector2(0f, -1f);
            SVector2 safeFacing = SafeFacing(kidFacingSim, fallbackDir);

            Vector3 worldOrigin = KalyeahResources.World(kidFeetSim, groundY + Mathf.Max(0.25f, originHeight));
            float effectiveRange = maxRange > 0f ? maxRange : DefaultFallbackReach;

            // Start searching past the kid's shoulder plane so geometry behind the kid never pulls shots backward.
            float minRayDist = Mathf.Max(0f, Vector3.Dot(worldOrigin - ray.origin, dir) - 0.15f);

            Vector3 cameraHitWorld = IntersectPrimaryWorld(ray, minRayDist, kidFeetSim, fallbackDir, effectiveRange, groundY, layout, collisionMask, ignoreRoot);

            // Clamp ground XZ target to maxRange (and arena perimeter when layout is present) from authoritative kid XZ.
            SVector2 aimSim = GodotSpace.Ground(new SVector3(cameraHitWorld.x, cameraHitWorld.y, cameraHitWorld.z));
            SVector2 offsetSim = aimSim - kidFeetSim;
            float distSim = offsetSim.Length();
            float targetY = cameraHitWorld.y;

            if (distSim > effectiveRange && distSim > 1e-6f)
            {
                float ratio = effectiveRange / distSim;
                offsetSim *= ratio;
                aimSim = kidFeetSim + offsetSim;
                targetY = Mathf.Max(groundY, Mathf.Lerp(worldOrigin.y, cameraHitWorld.y, ratio));
            }

            aimSim = ClampArena(aimSim, layout);
            Vector3 targetWorld = KalyeahResources.World(aimSim, Mathf.Max(groundY, targetY));

            // Secondary hand/chest-to-target check prevents corner-clipping into cover beside the camera.
            bool obstructed = ClampMuzzleObstruction(
                worldOrigin,
                kidFeetSim,
                groundY,
                layout,
                collisionMask,
                ignoreRoot,
                ref targetWorld,
                ref aimSim);

            SVector2 finalOffset = aimSim - kidFeetSim;
            float finalLen = finalOffset.Length();
            SVector2 aimDirSim = finalLen <= Mathf.Max(0f, noDirectionRadius)
                ? safeFacing
                : finalOffset / finalLen;

            return new AimSolution(targetWorld, worldOrigin, aimSim, aimDirSim, cameraHitWorld, obstructed);
        }

        /// <summary>Reconciles a radial AbilityArc thumb drag (0..1 screen vector) through the same range, arena, and cover checks.</summary>
        public static AimSolution SolveArcDrag(
            ShoulderCamera shoulder,
            SVector2 kidFeetSim,
            SVector2 kidFacingSim,
            Vector2 dragWay,
            float maxRange,
            MapLayoutData layout = null,
            float originHeight = DefaultOriginHeight,
            float groundY = 0f,
            int collisionMask = Physics.DefaultRaycastLayers,
            Transform ignoreRoot = null,
            float noDirectionRadius = DefaultNoDirectionRadius)
        {
            SVector2 safeFacing = SafeFacing(kidFacingSim, new SVector2(0f, -1f));
            float effectiveRange = maxRange > 0f ? maxRange : DefaultFallbackReach;
            Vector2 clampedStick = Vector2.ClampMagnitude(dragWay, 1f);
            SVector2 groundOffset = shoulder != null
                ? shoulder.GroundWay(clampedStick) * effectiveRange
                : new SVector2(clampedStick.x, -clampedStick.y) * effectiveRange;

            SVector2 aimSim = ClampArena(kidFeetSim + groundOffset, layout);
            Vector3 worldOrigin = KalyeahResources.World(kidFeetSim, groundY + Mathf.Max(0.25f, originHeight));
            Vector3 rawWorld = KalyeahResources.World(aimSim, groundY);
            Vector3 targetWorld = rawWorld;

            bool obstructed = ClampMuzzleObstruction(
                worldOrigin,
                kidFeetSim,
                groundY,
                layout,
                collisionMask,
                ignoreRoot,
                ref targetWorld,
                ref aimSim);

            SVector2 finalOffset = aimSim - kidFeetSim;
            float finalLen = finalOffset.Length();
            SVector2 aimDirSim = finalLen <= Mathf.Max(0f, noDirectionRadius)
                ? safeFacing
                : finalOffset / finalLen;

            return new AimSolution(targetWorld, worldOrigin, aimSim, aimDirSim, rawWorld, obstructed);
        }

        static Vector3 IntersectPrimaryWorld(
            Ray ray,
            float minDist,
            SVector2 kidFeetSim,
            SVector2 fallbackDirSim,
            float fallbackReach,
            float groundY,
            MapLayoutData layout,
            int collisionMask,
            Transform ignoreRoot)
        {
            float maxDist = MaxPrimaryRayDistance;
            Vector3 bestPoint;

            if (ray.direction.y < -0.001f)
            {
                float tGround = (groundY - ray.origin.y) / ray.direction.y;
                if (tGround >= minDist && tGround <= MaxPrimaryRayDistance)
                {
                    maxDist = tGround;
                    bestPoint = ray.GetPoint(tGround);
                    bestPoint.y = groundY;
                }
                else
                {
                    bestPoint = HorizonFallbackPoint(kidFeetSim, fallbackDirSim, fallbackReach, groundY);
                }
            }
            else
            {
                bestPoint = HorizonFallbackPoint(kidFeetSim, fallbackDirSim, fallbackReach, groundY);
            }

            float searchLen = maxDist - minDist;
            if (searchLen <= 0.001f) return bestPoint;

            Vector3 searchStart = ray.GetPoint(minDist);
            float bestHitDist = searchLen + 1f;
            Vector3 bestHitPoint = bestPoint;

            if (collisionMask != 0)
            {
                int count = Physics.RaycastNonAlloc(searchStart, ray.direction, RayHits, searchLen, collisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Transform t = RayHits[i].transform;
                    if (t == null) continue;
                    if (ignoreRoot != null && (t == ignoreRoot || t.IsChildOf(ignoreRoot))) continue;
                    if (RayHits[i].distance < bestHitDist)
                    {
                        bestHitDist = RayHits[i].distance;
                        bestHitPoint = RayHits[i].point;
                    }
                }
            }

            if (layout != null)
            {
                float limit = bestHitDist <= searchLen ? bestHitDist : searchLen;
                if (TryHitAnalyticParcels(searchStart, searchStart + ray.direction * limit, groundY, layout, out float tParcel))
                {
                    float parcelDist = tParcel * limit;
                    if (parcelDist < bestHitDist)
                    {
                        bestHitDist = parcelDist;
                        bestHitPoint = searchStart + ray.direction * parcelDist;
                    }
                }
            }

            return bestHitDist <= searchLen ? bestHitPoint : bestPoint;
        }

        static bool ClampMuzzleObstruction(
            Vector3 worldOrigin,
            SVector2 kidFeetSim,
            float groundY,
            MapLayoutData layout,
            int collisionMask,
            Transform ignoreRoot,
            ref Vector3 targetWorld,
            ref SVector2 aimSim)
        {
            Vector3 seg = targetWorld - worldOrigin;
            float segLen = seg.magnitude;
            if (segLen <= 0.05f) return false;

            Vector3 segDir = seg / segLen;
            float bestT = 1f;
            bool obstructed = false;

            if (collisionMask != 0)
            {
                float castLen = Mathf.Max(0.01f, segLen - 0.02f);
                int count = Physics.SphereCastNonAlloc(worldOrigin, MuzzleProbeRadius, segDir, RayHits, castLen, collisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Transform t = RayHits[i].transform;
                    if (t == null) continue;
                    if (ignoreRoot != null && (t == ignoreRoot || t.IsChildOf(ignoreRoot))) continue;
                    // Landing on the horizontal street surface right at the target point is floor contact, not wall cover.
                    if (RayHits[i].normal.y > 0.85f && RayHits[i].distance > segLen - 0.30f) continue;

                    float frac = Mathf.Clamp01((RayHits[i].distance - 0.03f) / segLen);
                    if (frac < bestT)
                    {
                        bestT = frac;
                        obstructed = true;
                    }
                }
            }

            if (layout != null && TryHitAnalyticParcels(worldOrigin, targetWorld, groundY, layout, out float tAnalytic))
            {
                float pulled = Mathf.Max(0f, tAnalytic - 0.03f / segLen);
                if (pulled < bestT)
                {
                    bestT = pulled;
                    obstructed = true;
                }
            }

            if (!obstructed) return false;

            targetWorld = worldOrigin + seg * bestT;
            targetWorld.y = Mathf.Max(groundY, targetWorld.y);
            aimSim = ClampArena(GodotSpace.Ground(new SVector3(targetWorld.x, targetWorld.y, targetWorld.z)), layout);
            if (SVector2.DistanceSquared(aimSim, kidFeetSim) < 1e-8f)
                aimSim = kidFeetSim;
            return true;
        }

        static bool TryHitAnalyticParcels(Vector3 fromWorld, Vector3 toWorld, float groundY, MapLayoutData layout, out float bestT)
        {
            bestT = 1f;
            bool any = false;
            var fromSim = new SVector3(fromWorld.x, fromWorld.y, -fromWorld.z);
            var toSim = new SVector3(toWorld.x, toWorld.y, -toWorld.z);
            SVector3 way = toSim - fromSim;

            int count = layout.ParcelCount;
            for (int i = 0; i < count; i++)
            {
                GroundRect r = layout.ParcelRect(i);
                string prop = layout.ParcelProp(i);
                float height = (prop == "cover_wall" || prop == "bench")
                    ? Mathf.Max(0.8f, layout.CoverHeight)
                    : DefaultBuildingHeight;

                // Ignore parcels whose footprint already contains the ray start.
                if (fromSim.X >= r.X && fromSim.X <= r.X + r.W &&
                    fromSim.Z >= r.Y && fromSim.Z <= r.Y + r.H &&
                    fromSim.Y <= groundY + height)
                {
                    continue;
                }

                if (SegmentEntryBox(fromSim, way, r.X, groundY, r.Y, r.X + r.W, groundY + height, r.Y + r.H, out float enter)
                    && enter >= 0f && enter < bestT)
                {
                    bestT = enter;
                    any = true;
                }
            }
            return any;
        }

        static bool SegmentEntryBox(
            SVector3 origin,
            SVector3 way,
            float minX, float minY, float minZ,
            float maxX, float maxY, float maxZ,
            out float enter)
        {
            enter = 0f;
            float leave = 1f;

            if (!AxisSlab(origin.X, way.X, minX, maxX, ref enter, ref leave)) return false;
            if (!AxisSlab(origin.Y, way.Y, minY, maxY, ref enter, ref leave)) return false;
            if (!AxisSlab(origin.Z, way.Z, minZ, maxZ, ref enter, ref leave)) return false;
            return enter <= leave;
        }

        static bool AxisSlab(float o, float d, float lo, float hi, ref float enter, ref float leave)
        {
            if (MathF.Abs(d) < 1e-7f) return o >= lo && o <= hi;
            float inv = 1f / d;
            float t0 = (lo - o) * inv;
            float t1 = (hi - o) * inv;
            enter = MathF.Max(enter, MathF.Min(t0, t1));
            leave = MathF.Min(leave, MathF.Max(t0, t1));
            return enter <= leave;
        }

        static Vector3 HorizonFallbackPoint(SVector2 kidFeetSim, SVector2 dirSim, float reach, float groundY)
        {
            SVector2 pt = kidFeetSim + dirSim * reach;
            return KalyeahResources.World(pt, groundY);
        }

        static SVector2 ClampArena(SVector2 point, MapLayoutData layout)
        {
            if (layout == null || layout.ArenaRadius <= 0f) return point;
            float maxR = MathF.Max(0f, layout.ArenaRadius - DefaultPlayerRadius);
            float lenSq = point.LengthSquared();
            if (lenSq > maxR * maxR && lenSq > 1e-8f)
                return point * (maxR / MathF.Sqrt(lenSq));
            return point;
        }

        static SVector2 SafeFacing(SVector2 facing, SVector2 fallback)
        {
            float lenSq = facing.LengthSquared();
            if (lenSq > 1e-6f) return facing / MathF.Sqrt(lenSq);
            float fbSq = fallback.LengthSquared();
            return fbSq > 1e-6f ? fallback / MathF.Sqrt(fbSq) : new SVector2(0f, -1f);
        }
    }
}
