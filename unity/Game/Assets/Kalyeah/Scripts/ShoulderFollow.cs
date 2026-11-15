using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kalyeah.Game
{
    /// <summary>
    /// The engine-free maths of the shoulder camera (scripts/view/camera_rules.gd's static helpers and clamp_shoulder_eye, and
    /// street_map.gd's segment_hits_box). Angles are degrees in the Godot game's convention (yaw 0 looks down the sim's -Z).
    /// </summary>
    public static class CameraMath
    {
        const float Tau = MathF.PI * 2f;
        /// <summary>camera_rules.gd SHOULDER_WALL_PAD / SHOULDER_ARENA_PAD: the spring arm keeps this far off walls and the arena's edge.</summary>
        public const float WallPad = 0.48f;
        public const float ArenaPad = 0.6f;

        /// <summary>Godot fposmod.</summary>
        public static float PosMod(float x, float y)
        {
            float value = x % y;
            if ((value < 0f && y > 0f) || (value > 0f && y < 0f)) value += y;
            return value;
        }

        /// <summary>Godot angle_difference(from, to) in radians: the shortest signed turn.</summary>
        public static float AngleDifference(float from, float to)
        {
            float difference = (to - from) % Tau;
            return (2f * difference) % Tau - difference;
        }

        /// <summary>Godot smoothstep (works with from &gt; to).</summary>
        public static float SmoothStep(float from, float to, float x)
        {
            if (MathF.Abs(from - to) < 0.00001f) return from;
            float s = Math.Clamp((x - from) / (to - from), 0f, 1f);
            return s * s * (3f - 2f * s);
        }

        public static float MoveToward(float from, float to, float delta) =>
            MathF.Abs(to - from) <= delta ? to : from + MathF.Sign(to - from) * delta;

        /// <summary>Critically-damped scalar spring (camera_rules.gd smooth_damp_scalar): (next value, next velocity).</summary>
        public static (float value, float velocity) SmoothDamp(float current, float target, float velocity, float smoothTime, float maxSpeed, float dt)
        {
            if (dt <= 0.000001f) return (current, velocity);
            float st = MathF.Max(0.0001f, smoothTime);
            float omega = 2f / st;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            if (maxSpeed > 0f)
            {
                float maxChange = maxSpeed * st;
                change = Math.Clamp(change, -maxChange, maxChange);
            }
            float clampedTarget = current - change;
            float temp = (velocity + omega * change) * dt;
            float nextVel = (velocity - omega * temp) * exp;
            float output = clampedTarget + (change + temp) * exp;
            if ((originalTo - current > 0f) == (output > originalTo))
            {
                output = originalTo;
                nextVel = 0f;
            }
            return (output, nextVel);
        }

        /// <summary>The angular spring in degrees, across the 0/360 seam (smooth_damp_angle_step): (next yaw in [0, 360), next velocity).</summary>
        public static (float yaw, float velocity) SmoothDampAngle(float currentDeg, float targetDeg, float velocity, float smoothTime, float maxSpeed, float dt)
        {
            float delta = AngleDifference(currentDeg * MathF.PI / 180f, targetDeg * MathF.PI / 180f) * 180f / MathF.PI;
            var (value, next) = SmoothDamp(currentDeg, currentDeg + delta, velocity, smoothTime, maxSpeed, dt);
            return (PosMod(value, 360f), next);
        }

        /// <summary>The 2D spring for the pivot (smooth_damp_vec2): (next point, next velocity).</summary>
        public static (Vector2 value, Vector2 velocity) SmoothDamp(Vector2 current, Vector2 target, Vector2 velocity, float smoothTime, float maxSpeed, float dt)
        {
            if (dt <= 0.000001f) return (current, velocity);
            float st = MathF.Max(0.0001f, smoothTime);
            float omega = 2f / st;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            Vector2 change = current - target;
            Vector2 originalTo = target;
            if (maxSpeed > 0f)
            {
                float maxChange = maxSpeed * st;
                if (change.LengthSquared() > maxChange * maxChange) change = Vector2.Normalize(change) * maxChange;
            }
            Vector2 clampedTarget = current - change;
            Vector2 temp = (velocity + change * omega) * dt;
            Vector2 nextVel = (velocity - temp * omega) * exp;
            Vector2 output = clampedTarget + (change + temp) * exp;
            if (Vector2.Dot(originalTo - current, output - originalTo) > 0f)
            {
                output = originalTo;
                nextVel = Vector2.Zero;
            }
            return (output, nextVel);
        }

        /// <summary>
        /// How much the view glides behind a moving kid (lateral_orbit_weight, 0..1.25): |sin| of the turn between the view and the run,
        /// faded out near a straight backpedal so running at the camera never whips it round.
        /// </summary>
        public static float LateralOrbitWeight(float lookYawDeg, float moveYawDeg, float speedRatio = 1f)
        {
            float diff = AngleDifference(lookYawDeg * MathF.PI / 180f, moveYawDeg * MathF.PI / 180f);
            float gate = SmoothStep(176f * MathF.PI / 180f, 142f * MathF.PI / 180f, MathF.Abs(diff));
            return MathF.Abs(MathF.Sin(diff)) * gate * Math.Clamp(speedRatio, 0f, 1.25f);
        }

        /// <summary>
        /// The spring arm (clamp_shoulder_eye), in the Godot game's space: pulls `desiredEye` toward `pivot` when the ground line between
        /// them meets an obstacle (grown by WallPad), and keeps the eye inside `arenaRadius - ArenaPad`.
        /// </summary>
        public static Vector3 ClampShoulderEye(Vector3 pivot, Vector3 desiredEye, IReadOnlyList<GroundRect> obstacles, float arenaRadius)
        {
            var p0 = new Vector2(pivot.X, pivot.Z);
            var p1 = new Vector2(desiredEye.X, desiredEye.Z);
            Vector2 delta = p1 - p0;
            float best = 1f;
            if (delta.LengthSquared() > 0.000001f)
            {
                if (obstacles != null)
                    foreach (GroundRect rect in obstacles)
                    {
                        var box = new GroundRect(rect.X - WallPad, rect.Y - WallPad, rect.W + WallPad * 2f, rect.H + WallPad * 2f);
                        Vector2 hit = SlabRayRect(p0, delta, box);
                        if (hit.X > hit.Y || hit.Y <= 0f || hit.X >= best) continue;
                        if (hit.X >= 0f) best = hit.X;
                        else
                        {
                            Vector2 solid = SlabRayRect(p0, delta, rect);
                            if (hit.Y >= best || (solid.X <= solid.Y && solid.Y > 0f && solid.X < best)) best = 0f;
                        }
                    }
                if (arenaRadius > 0f)
                {
                    float maxR = MathF.Max(0f, arenaRadius - ArenaPad);
                    Vector2 endAt = p0 + delta * best;
                    if (endAt.LengthSquared() > maxR * maxR)
                    {
                        float a = delta.LengthSquared();
                        float b = Vector2.Dot(p0, delta);
                        float c = p0.LengthSquared() - maxR * maxR;
                        if (c <= 0f)
                        {
                            float disc = MathF.Max(0f, b * b - a * c);
                            best = MathF.Min(best, Math.Clamp((-b + MathF.Sqrt(disc)) / a, 0f, 1f));
                        }
                        else best = 0f;
                    }
                }
            }
            Vector3 eye = Vector3.Lerp(pivot, desiredEye, best);
            if (arenaRadius > 0f)
            {
                float maxR = MathF.Max(0f, arenaRadius - ArenaPad);
                var xz = new Vector2(eye.X, eye.Z);
                if (xz.LengthSquared() > maxR * maxR)
                {
                    xz = Vector2.Normalize(xz) * maxR;
                    eye.X = xz.X;
                    eye.Z = xz.Y;
                }
            }
            eye.Y = MathF.Max(eye.Y, 0.5f);
            return eye;
        }

        /// <summary>The slab test of a ray `origin + t * delta` against `box`: (t enter, t leave); enter &gt; leave when it misses.</summary>
        public static Vector2 SlabRayRect(Vector2 origin, Vector2 delta, GroundRect box)
        {
            float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
            if (MathF.Abs(delta.X) <= 0.000001f)
            {
                if (origin.X < box.X || origin.X > box.X + box.W) return new Vector2(1f, -1f);
            }
            else
            {
                float inv = 1f / delta.X;
                float t1 = (box.X - origin.X) * inv, t2 = (box.X + box.W - origin.X) * inv;
                tMin = MathF.Max(tMin, MathF.Min(t1, t2));
                tMax = MathF.Min(tMax, MathF.Max(t1, t2));
            }
            if (MathF.Abs(delta.Y) <= 0.000001f)
            {
                if (origin.Y < box.Y || origin.Y > box.Y + box.H) return new Vector2(1f, -1f);
            }
            else
            {
                float inv = 1f / delta.Y;
                float t1 = (box.Y - origin.Y) * inv, t2 = (box.Y + box.H - origin.Y) * inv;
                tMin = MathF.Max(tMin, MathF.Min(t1, t2));
                tMax = MathF.Min(tMax, MathF.Max(t1, t2));
            }
            return new Vector2(tMin, tMax);
        }

        /// <summary>street_map.gd segment_hits_box: does the segment from `from` to `to` cross the box [min, max]? (any one space)</summary>
        public static bool SegmentHitsBox(Vector3 from, Vector3 to, Vector3 min, Vector3 max)
        {
            Vector3 way = to - from;
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float f = Axis(from, axis), w = Axis(way, axis), lo = Axis(min, axis), hi = Axis(max, axis);
                if (MathF.Abs(w) < 1e-9f)
                {
                    if (f < lo || f > hi) return false;
                    continue;
                }
                float t0 = (lo - f) / w, t1 = (hi - f) / w;
                enter = MathF.Max(enter, MathF.Min(t0, t1));
                leave = MathF.Min(leave, MathF.Max(t0, t1));
                if (enter > leave) return false;
            }
            return true;
        }

        /// <summary>
        /// street_map.gd hide_between: true when a prop whose bounds are [min, max] must be hidden, because it stands within `clear` metres of
        /// the eye or crosses the line from the eye to the kid's shins, middle or head (`feet` and `height`, all in one space, y up).
        /// </summary>
        public static bool Blocks(Vector3 eye, Vector3 feet, float height, float clear, Vector3 min, Vector3 max)
        {
            if ((Vector3.Clamp(eye, min, max) - eye).Length() < clear) return true;
            foreach (float share in new[] { 0.15f, 0.5f, 1f })
                if (SegmentHitsBox(eye, feet + Vector3.UnitY * height * share, min, max)) return true;
            return false;
        }

        static float Axis(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
    }

    /// <summary>
    /// The shoulder camera's state (port of scripts/view/arena_camera.gd with CameraRules.third_person): the look (a finger drag turns it,
    /// pitch within the rules' limits), the movement auto-follow that glides the view behind a running kid (manual look holds it off for
    /// `manual_override_cooldown`, then it eases back in over `manual_override_blend_time`), the pivot spring on your kid, the crouch/jump
    /// posture, the FOV kick while dashing or sliding, the shake, the spring arm against walls and the arena's edge, and the half turn when
    /// your side changes (Patintero's roles, Tumbang Preso's taya). Engine-free: the view turns `Pose` into a Unity camera.
    /// </summary>
    public sealed class ShoulderFollow
    {
        /// <summary>arena_camera.gd constants.</summary>
        public const float SpeedBoostFov = 6.5f, SpeedBoostLerp = 8f, CrouchPivotOffset = -0.42f, AirPivotShare = 0.65f, PosturePivotLerp = 14f;
        public const float AutoBehindLerp = 4.6f, ShakeDecay = 1.4f;
        /// <summary>TickBlend.SNAP_DISTANCE: a jump this far (a respawn) snaps the pivot.</summary>
        public const float SnapDistance = 3f;

        public readonly CameraRulesData Rules;
        /// <summary>Settings.reduced_motion: no glide, no shake, no FOV kick.</summary>
        public bool ReducedMotion;
        /// <summary>Degrees. Yaw 0 looks down the sim's -Z; pitch is down-positive.</summary>
        public float LookYaw, LookPitch;
        /// <summary>The pivot (sim XZ): your kid, smoothed.</summary>
        public Vector2 Aim { get; private set; }
        public Vector2 FollowPoint { get; private set; }
        public float VerticalOffset { get; private set; }
        public float FovBoost { get; private set; }
        public float ShakeStrength { get; private set; }
        public bool Placed { get; private set; }

        float _manualHold, _manualBlend = 1f, _yawVelocity, _smoothedMoveYaw, _moveYawVelocity, _pendingPivotDt;
        bool _moveYawReady, _aimSet, _speedBoosting, _goalFlip;
        Vector2 _pivotVelocity, _lastFollowInput;
        float _turn;

        public ShoulderFollow(CameraRulesData rules)
        {
            Rules = rules;
            LookPitch = rules.LookPitchDegrees;
        }

        public float YawVelocity => _yawVelocity;
        public bool ManualHolding => _manualHold > 0f;

        /// <summary>The first placing and a change of side (`flip`: your side is the -Z end now) look down the street from your end again.</summary>
        public void Place(bool flip)
        {
            if (flip != _goalFlip || !Placed)
            {
                _goalFlip = flip;
                _turn = flip ? 1f : 0f;
                ResetLook();
            }
            Placed = true;
        }

        public void ResetLook()
        {
            LookYaw = _goalFlip ? 180f : 0f;
            LookPitch = Rules.LookPitchDegrees;
            _manualHold = 0f;
            _manualBlend = 1f;
            _yawVelocity = 0f;
            _moveYawVelocity = 0f;
            _moveYawReady = false;
        }

        /// <summary>A finger dragged `drag` UI pixels on the 1280 x 720 base (x right, y DOWN, as Godot): right turns right, up looks up.</summary>
        public void Look(Vector2 drag)
        {
            if (drag != Vector2.Zero)
            {
                _manualHold = Rules.ManualOverrideCooldown;
                _manualBlend = 0f;
                _yawVelocity = 0f;
                _moveYawReady = false;
            }
            LookYaw = CameraMath.PosMod(LookYaw - drag.X * Rules.LookSensitivity, 360f);
            LookPitch = Rules.ClampPitch(LookPitch + drag.Y * Rules.LookSensitivity);
        }

        /// <summary>
        /// Glides the view behind a kid running along `facing` (sim XZ): an angular spring toward the run's yaw, weighted by how sideways the
        /// run is (CameraMath.LateralOrbitWeight), held off after a manual look.
        /// </summary>
        public void SyncBehindCharacter(Vector2 facing, bool moving, float dt, float speedRatio = 1f)
        {
            float step = MathF.Max(dt, 0f);
            if (_manualHold > 0f)
            {
                _manualHold = MathF.Max(0f, _manualHold - step);
                _manualBlend = 0f;
                _yawVelocity = 0f;
                return;
            }
            if (step <= 0f) return;
            if (!moving || facing.LengthSquared() <= 0.0001f)
            {
                _moveYawReady = false;
                _moveYawVelocity = 0f;
                Coast(step);
                return;
            }
            float window = MathF.Max(Rules.ManualOverrideBlendTime, 0.001f);
            _manualBlend = ReducedMotion ? 1f : CameraMath.MoveToward(_manualBlend, 1f, step / window);
            float ease = CameraMath.SmoothStep(0f, 1f, _manualBlend);
            float rawMoveYaw = CameraMath.PosMod(MathF.Atan2(-facing.X, -facing.Y) * 180f / MathF.PI, 360f);
            if (!_moveYawReady || ReducedMotion || Rules.FacingSmoothTime <= 0f)
            {
                _smoothedMoveYaw = rawMoveYaw;
                _moveYawVelocity = 0f;
                _moveYawReady = true;
            }
            else
            {
                (_smoothedMoveYaw, _moveYawVelocity) = CameraMath.SmoothDampAngle(_smoothedMoveYaw, rawMoveYaw, _moveYawVelocity, Rules.FacingSmoothTime, 720f, step);
            }
            float weight = CameraMath.LateralOrbitWeight(LookYaw, _smoothedMoveYaw, speedRatio) * Rules.AutoFollowLateralGain * ease;
            if (weight <= 0.0005f)
            {
                Coast(step);
                return;
            }
            if (ReducedMotion)
            {
                _yawVelocity = 0f;
                float rate = AutoBehindLerp * Math.Clamp(weight, 0.15f, 1f);
                float turn = CameraMath.AngleDifference(LookYaw * MathF.PI / 180f, _smoothedMoveYaw * MathF.PI / 180f) * (1f - MathF.Exp(-step * rate));
                LookYaw = CameraMath.PosMod(LookYaw + turn * 180f / MathF.PI, 360f);
                return;
            }
            float maxSpeed = MathF.Max(4f, Rules.AutoFollowMaxSpeed * weight);
            float smooth = Lerp(Rules.AutoFollowSmoothTime * 1.55f, Rules.AutoFollowSmoothTime, Math.Clamp(weight, 0f, 1f));
            (LookYaw, _yawVelocity) = CameraMath.SmoothDampAngle(LookYaw, _smoothedMoveYaw, _yawVelocity, smooth, maxSpeed, step);
        }

        /// <summary>No glide goal: the last glide's speed runs out.</summary>
        void Coast(float step)
        {
            if (MathF.Abs(_yawVelocity) > 0.01f && !ReducedMotion)
                (LookYaw, _yawVelocity) = CameraMath.SmoothDampAngle(LookYaw, LookYaw, _yawVelocity, Rules.AutoFollowSmoothTime, Rules.AutoFollowMaxSpeed, step);
            else _yawVelocity = 0f;
        }

        /// <summary>While aiming or backpedalling the view stays where the finger put it (the hold still runs out).</summary>
        public void TickManualLook(float dt)
        {
            float step = MathF.Max(dt, 0f);
            if (_manualHold > 0f) _manualHold = MathF.Max(0f, _manualHold - step);
            if (MathF.Abs(_yawVelocity) > 0.001f) _yawVelocity = CameraMath.MoveToward(_yawVelocity, 0f, step * 360f);
        }

        /// <summary>The pivot drops for a crouch or slide and rises with a jump (`airY` metres).</summary>
        public void SyncPosture(bool crouchingOrSliding, float airY, float dt)
        {
            _pendingPivotDt = MathF.Max(dt, 0f);
            float target = (crouchingOrSliding ? CrouchPivotOffset : 0f) + MathF.Max(airY, 0f) * AirPivotShare;
            if (ReducedMotion || dt <= 0f || MathF.Abs(VerticalOffset - target) < 0.00001f) VerticalOffset = target;
            else VerticalOffset = Lerp(VerticalOffset, target, 1f - MathF.Exp(-dt * PosturePivotLerp));
        }

        /// <summary>Keeps `point` (your kid, sim XZ) at the pivot; `snap` jumps there at once (a new match, a respawn).</summary>
        public void Follow(Vector2 point, bool snap = false)
        {
            bool still = _aimSet && Vector2.DistanceSquared(point, _lastFollowInput) < 1e-10f;
            _lastFollowInput = point;
            FollowPoint = point;
            float step = _pendingPivotDt;
            _pendingPivotDt = 0f;
            float smooth = Rules.PivotSmoothTime;
            if (snap || !_aimSet || ReducedMotion || step <= 0f || smooth <= 0f || Vector2.Distance(point, Aim) > SnapDistance)
            {
                Aim = point;
                _pivotVelocity = Vector2.Zero;
            }
            else if (still && Vector2.Distance(point, Aim) < 0.025f)
            {
                Aim = point;
                _pivotVelocity = Vector2.Zero;
            }
            else
            {
                (Vector2 next, Vector2 velocity) = CameraMath.SmoothDamp(Aim, point, _pivotVelocity, smooth, 0f, step);
                Aim = next;
                _pivotVelocity = velocity;
            }
            _aimSet = true;
        }

        /// <summary>Once a frame after Follow: the pivot catches up with the follow point, the FOV kick and the shake run, a turn advances.</summary>
        public void Advance(float dt)
        {
            float boost = _speedBoosting && !ReducedMotion ? SpeedBoostFov : 0f;
            if (ReducedMotion) FovBoost = 0f;
            else if (MathF.Abs(FovBoost - boost) < 0.00001f) FovBoost = boost;
            else if (dt > 0f) FovBoost = Lerp(FovBoost, boost, 1f - MathF.Exp(-dt * SpeedBoostLerp));

            float smooth = Rules.PivotSmoothTime;
            if (ReducedMotion || dt <= 0f || smooth <= 0f || Vector2.Distance(FollowPoint, Aim) > SnapDistance)
            {
                Aim = FollowPoint;
                _pivotVelocity = Vector2.Zero;
            }
            else if (Vector2.Distance(FollowPoint, Aim) > 0.00001f)
            {
                (Vector2 next, Vector2 velocity) = CameraMath.SmoothDamp(Aim, FollowPoint, _pivotVelocity, smooth, 0f, dt);
                Aim = next;
                _pivotVelocity = velocity;
            }
            else
            {
                Aim = FollowPoint;
                _pivotVelocity = Vector2.Zero;
            }
            _pendingPivotDt = 0f;

            float goal = _goalFlip ? 1f : 0f;
            if (_turn != goal) _turn = ReducedMotion ? goal : CameraMath.MoveToward(_turn, goal, dt / MathF.Max(Rules.TurnSeconds, 0.001f));

            if (ReducedMotion || ShakeStrength <= 0.001f) ShakeStrength = 0f;
            else ShakeStrength = MathF.Max(0f, ShakeStrength - dt * ShakeDecay);
        }

        /// <summary>The kick of a sprint, dash or slide: the lens widens a little.</summary>
        public void SetSpeedBoost(bool boosting)
        {
            _speedBoosting = boosting;
            if (ReducedMotion) FovBoost = 0f;
        }

        /// <summary>A hit on you, a tag: ~0.1 small, 0.4 big (Settings.screen_shake off: the caller does not call).</summary>
        public void Shake(float strength)
        {
            if (!ReducedMotion) ShakeStrength = MathF.Max(ShakeStrength, strength);
        }

        public float Fov => Rules.ShoulderFov + (ReducedMotion ? 0f : FovBoost);

        /// <summary>
        /// The camera now, in the Godot game's space (y up, sim (x, z) on the ground): the eye after the posture and the spring arm, and the
        /// look's pitch (down-positive) and yaw in degrees.
        /// </summary>
        public (Vector3 eye, float pitch, float yaw) Pose(IReadOnlyList<GroundRect> obstacles, float arenaRadius)
        {
            float y = LookYaw * MathF.PI / 180f;
            var back = new Vector3(MathF.Sin(y), 0f, MathF.Cos(y));
            var right = new Vector3(MathF.Cos(y), 0f, -MathF.Sin(y));
            var basePivot = new Vector3(Aim.X, Rules.ShoulderHeight, Aim.Y);
            float rise = Rules.ShoulderDistance * MathF.Tan(Math.Clamp(LookPitch, -80f, 80f) * MathF.PI / 180f);
            Vector3 eye = basePivot + back * Rules.ShoulderDistance + Vector3.UnitY * rise + right * Rules.ShoulderOffset;
            eye.Y = MathF.Max(eye.Y, 0.5f);
            float pivotY = MathF.Max(0.35f, Rules.ShoulderHeight + VerticalOffset);
            eye.Y = MathF.Max(0.5f, eye.Y + (pivotY - Rules.ShoulderHeight));
            eye = CameraMath.ClampShoulderEye(new Vector3(Aim.X, pivotY, Aim.Y), eye, obstacles, arenaRadius);
            return (eye, LookPitch, LookYaw);
        }

        /// <summary>A way on the screen (x right, y DOWN, as a joystick reads) as a way on the ground (sim XZ) for this view.</summary>
        public Vector2 GroundWay(Vector2 screenWay) => GodotSpace.GroundWay(screenWay, LookYaw);

        /// <summary>The ground way (sim XZ, unit) the view looks along: up the screen (camera_rules.gd ground_axes()[1]).</summary>
        public Vector2 Forward => GodotSpace.GroundAxes(LookYaw).down;

        /// <summary>A way on the ground as a way on the screen (x right, y down; arena_camera.gd screen_way).</summary>
        public Vector2 ScreenWay(Vector2 ground)
        {
            var (right, up) = GodotSpace.GroundAxes(LookYaw);
            return new Vector2(Vector2.Dot(ground, right), -Vector2.Dot(ground, up));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
