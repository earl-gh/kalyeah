using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Coordinate-space helpers converting glTF right-handed Y-up (<c>gltf_yup</c>: +X left, +Y up, +Z forward)
    /// into glTFast's imported Unity left-handed space (<c>(-x, y, z)</c> for positions/directions, <c>(x, -y, -z)</c> for rotation axes).
    /// </summary>
    public static class RigSpaceConvert
    {
        public static Vector3 PositionOrDirection(Vector3 v, bool isGltfYup)
        {
            return isGltfYup ? new Vector3(-v.x, v.y, v.z) : v;
        }

        public static Vector3 RotationAxis(Vector3 axis, bool isGltfYup)
        {
            Vector3 mapped = isGltfYup ? new Vector3(axis.x, -axis.y, -axis.z) : axis;
            float sqr = mapped.sqrMagnitude;
            return sqr > 1e-8f ? mapped / Mathf.Sqrt(sqr) : Vector3.right;
        }
    }

    public readonly struct JointLimitEntry
    {
        public readonly string Bone;
        public readonly Vector3 FlexAxis;
        public readonly Vector3 AbductAxis;
        public readonly Vector3 TwistAxis;
        public readonly float FlexMinDeg;
        public readonly float FlexMaxDeg;
        public readonly float AbductMinDeg;
        public readonly float AbductMaxDeg;
        public readonly float TwistMinDeg;
        public readonly float TwistMaxDeg;
        public readonly bool IsHinge;
        /// <summary>Minimum flex angle (deg) before twist is allowed (e.g. knee Leg: 20 deg).</summary>
        public readonly float TwistMinFlexDeg;

        public JointLimitEntry(
            string bone,
            Vector3 flexAxis,
            Vector3 abductAxis,
            Vector3 twistAxis,
            float flexMinDeg,
            float flexMaxDeg,
            float abductMinDeg,
            float abductMaxDeg,
            float twistMinDeg,
            float twistMaxDeg,
            bool isHinge,
            float twistMinFlexDeg = 0f)
        {
            Bone = bone ?? "";
            FlexAxis = flexAxis.sqrMagnitude > 1e-8f ? flexAxis.normalized : Vector3.right;
            TwistAxis = twistAxis.sqrMagnitude > 1e-8f ? twistAxis.normalized : Vector3.up;
            // Gram-Schmidt orthonormalize AbductAxis perpendicular to FlexAxis & TwistAxis so projections never drift.
            Vector3 abd = abductAxis.sqrMagnitude > 1e-8f ? abductAxis.normalized : Vector3.Cross(TwistAxis, FlexAxis);
            abd -= TwistAxis * Vector3.Dot(abd, TwistAxis);
            abd -= FlexAxis * Vector3.Dot(abd, FlexAxis);
            if (abd.sqrMagnitude < 1e-6f) abd = Vector3.Cross(TwistAxis, FlexAxis);
            AbductAxis = abd.sqrMagnitude > 1e-8f ? abd.normalized : Vector3.forward;

            FlexMinDeg = Mathf.Min(flexMinDeg, flexMaxDeg);
            FlexMaxDeg = Mathf.Max(flexMinDeg, flexMaxDeg);
            AbductMinDeg = Mathf.Min(abductMinDeg, abductMaxDeg);
            AbductMaxDeg = Mathf.Max(abductMinDeg, abductMaxDeg);
            TwistMinDeg = Mathf.Min(twistMinDeg, twistMaxDeg);
            TwistMaxDeg = Mathf.Max(twistMinDeg, twistMaxDeg);
            IsHinge = isHinge || (Mathf.Abs(AbductMinDeg) < 0.001f && Mathf.Abs(AbductMaxDeg) < 0.001f);
            TwistMinFlexDeg = Mathf.Max(0f, twistMinFlexDeg);
        }

        /// <summary>Per-kid overrides strictly narrow ranges and never widen anatomical limits (apex-rig-forge R2).</summary>
        public JointLimitEntry NarrowWith(
            float? flexMin, float? flexMax,
            float? abductMin, float? abductMax,
            float? twistMin, float? twistMax)
        {
            float fMin = flexMin.HasValue ? Mathf.Max(FlexMinDeg, flexMin.Value) : FlexMinDeg;
            float fMax = flexMax.HasValue ? Mathf.Min(FlexMaxDeg, flexMax.Value) : FlexMaxDeg;
            if (fMin > fMax) { float mid = 0.5f * (fMin + fMax); fMin = mid; fMax = mid; }

            float aMin = abductMin.HasValue ? Mathf.Max(AbductMinDeg, abductMin.Value) : AbductMinDeg;
            float aMax = abductMax.HasValue ? Mathf.Min(AbductMaxDeg, abductMax.Value) : AbductMaxDeg;
            if (aMin > aMax) { float mid = 0.5f * (aMin + aMax); aMin = mid; aMax = mid; }

            float tMin = twistMin.HasValue ? Mathf.Max(TwistMinDeg, twistMin.Value) : TwistMinDeg;
            float tMax = twistMax.HasValue ? Mathf.Min(TwistMaxDeg, twistMax.Value) : TwistMaxDeg;
            if (tMin > tMax) { float mid = 0.5f * (tMin + tMax); tMin = mid; tMax = mid; }

            return new JointLimitEntry(
                Bone, FlexAxis, AbductAxis, TwistAxis,
                fMin, fMax, aMin, aMax, tMin, tMax,
                IsHinge, TwistMinFlexDeg);
        }
    }

    /// <summary>
    /// Parsed <c>unity/Data/rig__joint_limits.json</c> with R2 anatomical defaults, per-kid narrowing overrides,
    /// and zero-GC swing-twist decomposition &amp; ellipse clamping.
    /// </summary>
    public sealed class RigJointLimitsData
    {
        public string CoordSpace = "gltf_yup";
        public float ReferenceLegLengthM = 0.6957f;
        readonly Dictionary<string, JointLimitEntry> _baseJoints = new Dictionary<string, JointLimitEntry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Dictionary<string, JointLimitEntry>> _kidOverrides =
            new Dictionary<string, Dictionary<string, JointLimitEntry>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, float> _kidLegRatios = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public int JointCount => _baseJoints.Count;

        public bool TryGetKidLegLengthRatio(string kidId, out float ratio)
        {
            if (!string.IsNullOrEmpty(kidId) && _kidLegRatios.TryGetValue(kidId.Trim().ToLowerInvariant(), out ratio))
                return true;
            ratio = 1f;
            return false;
        }

        public bool TryGetLimit(string boneShortName, string kidId, out JointLimitEntry entry)
        {
            string clean = StripMixamoPrefix(boneShortName);
            if (!string.IsNullOrEmpty(kidId)
                && _kidOverrides.TryGetValue(kidId, out var perKid)
                && (perKid.TryGetValue(clean, out entry) || perKid.TryGetValue(StripSidePrefix(clean), out entry)))
                return true;

            if (_baseJoints.TryGetValue(clean, out entry)) return true;
            string sideStripped = StripSidePrefix(clean);
            return _baseJoints.TryGetValue(sideStripped, out entry);
        }

        public static string StripMixamoPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            const string prefix = "mixamorig:";
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(prefix.Length)
                : name;
        }

        public static string StripSidePrefix(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return "";
            if (shortName.StartsWith("Left", StringComparison.OrdinalIgnoreCase)) return shortName.Substring(4);
            if (shortName.StartsWith("Right", StringComparison.OrdinalIgnoreCase)) return shortName.Substring(5);
            return shortName;
        }

        /// <summary>
        /// Decomposes a local delta rotation <paramref name="delta"/> into <c>swing * twist</c> about <paramref name="twistAxis"/>
        /// with shortest-arc signed twist angle in degrees (<c>[-180, +180]</c>). Zero heap allocations.
        /// </summary>
        public static void DecomposeSwingTwist(
            Quaternion delta,
            Vector3 twistAxis,
            out Quaternion swing,
            out Quaternion twist,
            out float twistAngleDeg)
        {
            if (delta.w < 0f)
                delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

            Vector3 axis = twistAxis.sqrMagnitude > 1e-8f ? twistAxis.normalized : Vector3.up;
            Vector3 v = new Vector3(delta.x, delta.y, delta.z);
            float proj = Vector3.Dot(v, axis);
            Vector3 p = axis * proj;

            float twistLenSq = p.x * p.x + p.y * p.y + p.z * p.z + delta.w * delta.w;
            if (twistLenSq > 1e-10f)
            {
                float inv = 1f / Mathf.Sqrt(twistLenSq);
                twist = new Quaternion(p.x * inv, p.y * inv, p.z * inv, delta.w * inv);
                if (twist.w < 0f)
                    twist = new Quaternion(-twist.x, -twist.y, -twist.z, -twist.w);
                float sinHalf = Vector3.Dot(new Vector3(twist.x, twist.y, twist.z), axis);
                twistAngleDeg = 2f * Mathf.Atan2(sinHalf, twist.w) * Mathf.Rad2Deg;
            }
            else
            {
                twist = Quaternion.identity;
                twistAngleDeg = 0f;
            }

            swing = delta * Quaternion.Inverse(twist);
            if (swing.w < 0f)
                swing = new Quaternion(-swing.x, -swing.y, -swing.z, -swing.w);
            swing = NormalizeSafe(swing);
        }

        /// <summary>
        /// Clamps a local delta rotation relative to rest pose using swing-twist decomposition and a flex x abduct ellipse
        /// (never separate Euler channels; hinges strictly zero abduct). Zero heap allocations.
        /// </summary>
        public static Quaternion ClampSwingTwist(Quaternion deltaFromRest, in JointLimitEntry limit)
        {
            DecomposeSwingTwist(deltaFromRest, limit.TwistAxis, out Quaternion swing, out _, out float twistDeg);

            Vector3 sv = new Vector3(swing.x, swing.y, swing.z);
            float sinHalf = sv.magnitude;
            float flexDeg = 0f;
            float abductDeg = 0f;
            if (sinHalf > 1e-6f)
            {
                float swingAngleDeg = 2f * Mathf.Atan2(sinHalf, swing.w) * Mathf.Rad2Deg;
                Vector3 rotVec = (sv / sinHalf) * swingAngleDeg;
                flexDeg = Vector3.Dot(rotVec, limit.FlexAxis);
                abductDeg = Vector3.Dot(rotVec, limit.AbductAxis);
            }

            if (limit.IsHinge)
            {
                abductDeg = 0f;
                flexDeg = Mathf.Clamp(flexDeg, limit.FlexMinDeg, limit.FlexMaxDeg);
            }
            else
            {
                ClampEllipse(ref flexDeg, ref abductDeg, limit.FlexMinDeg, limit.FlexMaxDeg, limit.AbductMinDeg, limit.AbductMaxDeg);
            }

            Vector3 clampedSwingVec = limit.FlexAxis * flexDeg + limit.AbductAxis * abductDeg;
            float clampedSwingAngle = clampedSwingVec.magnitude;
            Quaternion clampedSwing = clampedSwingAngle > 1e-5f
                ? Quaternion.AngleAxis(clampedSwingAngle, clampedSwingVec / clampedSwingAngle)
                : Quaternion.identity;

            float twistGate = limit.TwistMinFlexDeg > 0f
                ? Mathf.Clamp01((flexDeg - limit.TwistMinFlexDeg) / 10f)
                : 1f;
            float clampedTwistDeg = Mathf.Clamp(twistDeg, limit.TwistMinDeg * twistGate, limit.TwistMaxDeg * twistGate);
            Quaternion clampedTwist = Mathf.Abs(clampedTwistDeg) > 1e-5f
                ? Quaternion.AngleAxis(clampedTwistDeg, limit.TwistAxis)
                : Quaternion.identity;

            return NormalizeSafe(clampedSwing * clampedTwist);
        }

        static void ClampEllipse(ref float x, ref float y, float minX, float maxX, float minY, float maxY)
        {
            bool originInside = minX <= 0f && maxX >= 0f && minY <= 0f && maxY >= 0f;
            if (originInside)
            {
                float rx = x >= 0f ? maxX : -minX;
                float ry = y >= 0f ? maxY : -minY;
                if (rx <= 1e-4f) x = 0f;
                if (ry <= 1e-4f) y = 0f;
                float nx = rx > 1e-4f ? x / rx : 0f;
                float ny = ry > 1e-4f ? y / ry : 0f;
                float r2 = nx * nx + ny * ny;
                if (r2 > 1f)
                {
                    float inv = 1f / Mathf.Sqrt(r2);
                    x *= inv;
                    y *= inv;
                }
                return;
            }

            float cx = 0.5f * (minX + maxX);
            float cy = 0.5f * (minY + maxY);
            float hx = 0.5f * (maxX - minX);
            float hy = 0.5f * (maxY - minY);
            float dx = x - cx;
            float dy = y - cy;
            if (hx <= 1e-4f) dx = 0f;
            if (hy <= 1e-4f) dy = 0f;
            float ex = hx > 1e-4f ? dx / hx : 0f;
            float ey = hy > 1e-4f ? dy / hy : 0f;
            float d2 = ex * ex + ey * ey;
            if (d2 > 1f)
            {
                float inv = 1f / Mathf.Sqrt(d2);
                dx *= inv;
                dy *= inv;
            }
            x = cx + dx;
            y = cy + dy;
        }

        static Quaternion NormalizeSafe(Quaternion q)
        {
            float m = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (m < 1e-10f) return Quaternion.identity;
            float inv = 1f / Mathf.Sqrt(m);
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        public static RigJointLimitsData CreateDefaultR2()
        {
            var data = new RigJointLimitsData { CoordSpace = "unity" };
            Vector3 fx = Vector3.right;
            Vector3 az = Vector3.forward;
            Vector3 ty = Vector3.up;

            data.AddBase(new JointLimitEntry("Spine", fx, az, ty, -15f, 25f, -15f, 15f, -20f, 20f, false));
            data.AddBase(new JointLimitEntry("Spine1", fx, az, ty, -15f, 25f, -15f, 15f, -20f, 20f, false));
            data.AddBase(new JointLimitEntry("Spine2", fx, az, ty, -15f, 25f, -15f, 15f, -20f, 20f, false));
            data.AddBase(new JointLimitEntry("Neck", fx, az, ty, -40f, 40f, -30f, 30f, -50f, 50f, false));
            data.AddBase(new JointLimitEntry("Head", fx, az, ty, -25f, 25f, -15f, 15f, -30f, 30f, false));
            data.AddBase(new JointLimitEntry("Shoulder", fx, az, ty, -20f, 20f, -10f, 30f, 0f, 0f, false));
            data.AddBase(new JointLimitEntry("Arm", fx, az, ty, -50f, 160f, -30f, 140f, -80f, 80f, false));
            data.AddBase(new JointLimitEntry("ForeArm", fx, az, ty, -5f, 145f, 0f, 0f, -85f, 85f, true));
            data.AddBase(new JointLimitEntry("Hand", fx, az, ty, -60f, 70f, -20f, 35f, -80f, 80f, false));
            data.AddBase(new JointLimitEntry("UpLeg", fx, az, ty, -20f, 125f, -25f, 45f, -40f, 40f, false));
            data.AddBase(new JointLimitEntry("Leg", fx, az, ty, -3f, 140f, 0f, 0f, -10f, 10f, true, twistMinFlexDeg: 20f));
            data.AddBase(new JointLimitEntry("Foot", fx, az, ty, -20f, 45f, -30f, 30f, 0f, 0f, false));
            data.AddBase(new JointLimitEntry("ToeBase", fx, az, ty, -30f, 50f, 0f, 0f, 0f, 0f, true));
            return data;
        }

        void AddBase(JointLimitEntry entry) => _baseJoints[entry.Bone] = entry;

        public static RigJointLimitsData ParseJson(string json)
        {
            RigJointLimitsData fallback = CreateDefaultR2();
            if (string.IsNullOrWhiteSpace(json)) return fallback;

            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch
            {
                return fallback;
            }
            if (root == null) return fallback;

            if (root.TryGetValue("data", out object wrapped) && wrapped is Dictionary<string, object> inner)
                root = inner;

            var result = CreateDefaultR2();
            string coordSpace = JsonRead.Str(root, "coordSpace", JsonRead.Str(root, "coord_space", "gltf_yup"));
            result.CoordSpace = coordSpace;
            result.ReferenceLegLengthM = JsonRead.Float(root, "referenceLegLengthM", result.ReferenceLegLengthM);
            bool isGltf = !string.Equals(coordSpace, "unity", StringComparison.OrdinalIgnoreCase);

            Dictionary<string, object> jointsDict = JsonRead.Obj(root, "joints")
                ?? JsonRead.Obj(root, "bones")
                ?? JsonRead.Obj(root, "limits");

            if (jointsDict != null)
            {
                foreach (var kv in jointsDict)
                {
                    if (!(kv.Value is Dictionary<string, object> jd)) continue;
                    string bone = StripMixamoPrefix(kv.Key);
                    JointLimitEntry baseDef = result._baseJoints.TryGetValue(bone, out JointLimitEntry existing)
                        ? existing
                        : (result._baseJoints.TryGetValue(StripSidePrefix(bone), out JointLimitEntry sideDef)
                            ? sideDef
                            : new JointLimitEntry(bone, Vector3.right, Vector3.forward, Vector3.up, -45f, 45f, -30f, 30f, -30f, 30f, false));

                    JointLimitEntry parsed = ParseJointEntry(bone, jd, baseDef, isGltf);
                    result._baseJoints[bone] = parsed;
                }
            }

            Dictionary<string, object> capsulesDict = JsonRead.Obj(root, "capsules");
            if (capsulesDict != null)
            {
                foreach (var kidKv in capsulesDict)
                {
                    if (!(kidKv.Value is Dictionary<string, object> kd)) continue;
                    float legRatio = JsonRead.Float(kd, "legLengthRatio", 0f);
                    if (legRatio > 0.25f)
                        result._kidLegRatios[kidKv.Key.Trim().ToLowerInvariant()] = Mathf.Clamp(legRatio, 0.65f, 1.45f);
                }
            }

            Dictionary<string, object> overridesDict = JsonRead.Obj(root, "overrides");
            if (overridesDict != null)
            {
                foreach (var kidKv in overridesDict)
                {
                    if (!(kidKv.Value is Dictionary<string, object> kidMap)) continue;
                    string kidKey = kidKv.Key.Trim().ToLowerInvariant();
                    Dictionary<string, object> ovJoints = JsonRead.Obj(kidMap, "joints") ?? kidMap;
                    var perKid = new Dictionary<string, JointLimitEntry>(StringComparer.OrdinalIgnoreCase);

                    foreach (var jKv in ovJoints)
                    {
                        if (!(jKv.Value is Dictionary<string, object> ovJd)) continue;
                        string bone = StripMixamoPrefix(jKv.Key);
                        if (!result.TryGetLimit(bone, null, out JointLimitEntry baseLimit)) continue;

                        ReadRange(ovJd, "flex", "flexDeg", out float? fMin, out float? fMax);
                        ReadRange(ovJd, "abduct", "abductDeg", out float? aMin, out float? aMax);
                        ReadRange(ovJd, "twist", "twistDeg", out float? tMin, out float? tMax);
                        perKid[bone] = baseLimit.NarrowWith(fMin, fMax, aMin, aMax, tMin, tMax);
                    }

                    if (perKid.Count > 0)
                        result._kidOverrides[kidKey] = perKid;
                }
            }

            return result;
        }

        static JointLimitEntry ParseJointEntry(string bone, Dictionary<string, object> jd, JointLimitEntry fallback, bool isGltf)
        {
            Dictionary<string, object> axesRoot = JsonRead.Obj(jd, "axes") ?? JsonRead.Obj(jd, "restAxes");
            Dictionary<string, object> localUnityAxes = JsonRead.Obj(axesRoot, "local_unity");
            Dictionary<string, object> localBlenderAxes = JsonRead.Obj(axesRoot, "local_blender");

            Vector3 flexAxis;
            Vector3 abductAxis;
            Vector3 twistAxis;

            if (localUnityAxes != null)
            {
                // Pre-converted glTFast bone rest-local axes (q_rel = Inverse(rest) * local): use verbatim.
                flexAxis = JsonRead.TryVec3(localUnityAxes, "flex", out Vector3 fa) ? fa.normalized : fallback.FlexAxis;
                abductAxis = JsonRead.TryVec3(localUnityAxes, "abduct", out Vector3 aa) ? aa.normalized : fallback.AbductAxis;
                twistAxis = JsonRead.TryVec3(localUnityAxes, "twist", out Vector3 ta) ? ta.normalized : fallback.TwistAxis;
            }
            else if (localBlenderAxes != null)
            {
                flexAxis = JsonRead.TryVec3(localBlenderAxes, "flex", out Vector3 fa) ? RigSpaceConvert.RotationAxis(fa, isGltfYup: true) : fallback.FlexAxis;
                abductAxis = JsonRead.TryVec3(localBlenderAxes, "abduct", out Vector3 aa) ? RigSpaceConvert.RotationAxis(aa, isGltfYup: true) : fallback.AbductAxis;
                twistAxis = JsonRead.TryVec3(localBlenderAxes, "twist", out Vector3 ta) ? RigSpaceConvert.RotationAxis(ta, isGltfYup: true) : fallback.TwistAxis;
            }
            else
            {
                Dictionary<string, object> axesObj = axesRoot ?? jd;
                flexAxis = JsonRead.TryVec3(axesObj, "flexAxis", out Vector3 fa) || JsonRead.TryVec3(axesObj, "flex_axis", out fa) || (axesObj != jd && JsonRead.TryVec3(axesObj, "flex", out fa))
                    ? RigSpaceConvert.RotationAxis(fa, isGltf) : fallback.FlexAxis;
                abductAxis = JsonRead.TryVec3(axesObj, "abductAxis", out Vector3 aa) || JsonRead.TryVec3(axesObj, "abduct_axis", out aa) || (axesObj != jd && JsonRead.TryVec3(axesObj, "abduct", out aa))
                    ? RigSpaceConvert.RotationAxis(aa, isGltf) : fallback.AbductAxis;
                twistAxis = JsonRead.TryVec3(axesObj, "twistAxis", out Vector3 ta) || JsonRead.TryVec3(axesObj, "twist_axis", out ta) || (axesObj != jd && JsonRead.TryVec3(axesObj, "twist", out ta))
                    ? RigSpaceConvert.RotationAxis(ta, isGltf) : fallback.TwistAxis;
            }

            Dictionary<string, object> rangeObj = JsonRead.Obj(jd, "ranges") ?? JsonRead.Obj(jd, "limits") ?? jd;
            ReadRange(rangeObj, "flex", "flexDeg", out float? fMin, out float? fMax);
            ReadRange(rangeObj, "abduct", "abductDeg", out float? aMin, out float? aMax);
            ReadRange(rangeObj, "twist", "twistDeg", out float? tMin, out float? tMax);

            string kindStr = JsonRead.Str(jd, "kind", "");
            bool isHinge = string.Equals(kindStr, "hinge", StringComparison.OrdinalIgnoreCase)
                || JsonRead.Bool(jd, "hinge", JsonRead.Bool(jd, "isHinge", fallback.IsHinge));
            float twistMinFlex = JsonRead.Float(
                jd,
                "twistRequiresFlexDeg",
                JsonRead.Float(jd, "twistMinFlexDeg", JsonRead.Float(jd, "twist_min_flex_deg", fallback.TwistMinFlexDeg)));

            return new JointLimitEntry(
                bone,
                flexAxis,
                abductAxis,
                twistAxis,
                fMin ?? fallback.FlexMinDeg,
                fMax ?? fallback.FlexMaxDeg,
                aMin ?? fallback.AbductMinDeg,
                aMax ?? fallback.AbductMaxDeg,
                tMin ?? fallback.TwistMinDeg,
                tMax ?? fallback.TwistMaxDeg,
                isHinge,
                twistMinFlex);
        }

        static void ReadRange(Dictionary<string, object> d, string keyA, string keyB, out float? min, out float? max)
        {
            min = null;
            max = null;
            if (d == null) return;
            object raw = null;
            if (!d.TryGetValue(keyA, out raw) && !d.TryGetValue(keyB, out raw)) return;

            if (raw is List<object> arr && arr.Count >= 2)
            {
                min = JsonRead.ToFloat(arr[0], 0f);
                max = JsonRead.ToFloat(arr[1], 0f);
            }
            else if (raw is Dictionary<string, object> obj)
            {
                if (obj.TryGetValue("min", out object mn)) min = JsonRead.ToFloat(mn, 0f);
                if (obj.TryGetValue("max", out object mx)) max = JsonRead.ToFloat(mx, 0f);
            }
            else if (raw is double || raw is float || raw is int || raw is long)
            {
                float v = Mathf.Abs(JsonRead.ToFloat(raw, 0f));
                min = -v;
                max = v;
            }
        }
    }

    public readonly struct ClipMetricEntry
    {
        public readonly string Name;
        public readonly float DurationSec;
        public readonly int FrameCount;
        public readonly float Fps;
        public readonly bool Loop;
        public readonly float StrideDistanceM;
        public readonly float StrideSpeedMps;
        public readonly float ReleaseTimeSec;
        public readonly int ReleaseFrame;
        public readonly float CrossfadeInSec;
        public readonly string Archetype;
        public readonly bool UpperBodyMask;
        public readonly string SyncGroup;
        public readonly float MaxFootSlideM;

        public ClipMetricEntry(
            string name, float durationSec, int frameCount, float fps, bool loop,
            float strideDistanceM, float strideSpeedMps, float releaseTimeSec, int releaseFrame,
            float crossfadeInSec, string archetype, bool upperBodyMask, string syncGroup, float maxFootSlideM)
        {
            Name = name ?? "";
            DurationSec = durationSec;
            FrameCount = frameCount;
            Fps = fps > 0f ? fps : 30f;
            Loop = loop;
            StrideDistanceM = strideDistanceM;
            StrideSpeedMps = strideSpeedMps;
            ReleaseTimeSec = releaseTimeSec;
            ReleaseFrame = releaseFrame;
            CrossfadeInSec = crossfadeInSec;
            Archetype = archetype ?? "";
            UpperBodyMask = upperBodyMask;
            SyncGroup = syncGroup ?? "none";
            MaxFootSlideM = maxFootSlideM;
        }
    }

    public readonly struct KidAnimAdaptation
    {
        public readonly float LegLengthM;
        public readonly float LegLengthRatio;
        public readonly float GirthArmAbductDeg;
        public readonly float GirthHipSpreadDeg;
        public readonly float AimYawMaxDeg;
        public readonly float AimPitchMaxDeg;

        public static readonly KidAnimAdaptation Default = new KidAnimAdaptation(0.70f, 1f, 0f, 0f, 55f, 35f);

        public KidAnimAdaptation(
            float legLengthM, float legLengthRatio,
            float girthArmAbductDeg, float girthHipSpreadDeg,
            float aimYawMaxDeg, float aimPitchMaxDeg)
        {
            LegLengthM = legLengthM > 0.05f ? legLengthM : 0.70f;
            LegLengthRatio = legLengthRatio > 0.25f ? Mathf.Clamp(legLengthRatio, 0.65f, 1.45f) : 1f;
            GirthArmAbductDeg = Mathf.Clamp(girthArmAbductDeg, 0f, 25f);
            GirthHipSpreadDeg = Mathf.Clamp(girthHipSpreadDeg, 0f, 18f);
            AimYawMaxDeg = aimYawMaxDeg > 1f ? aimYawMaxDeg : 55f;
            AimPitchMaxDeg = aimPitchMaxDeg > 1f ? aimPitchMaxDeg : 35f;
        }
    }

    /// <summary>
    /// Parsed <c>unity/Data/anim__clip_metrics.json</c> (A4 speed-matching stride speeds, A6 crossfade matrix,
    /// cast release times, A3 per-kid leg-length ratio and girth offsets).
    /// </summary>
    public sealed class AnimClipMetricsData
    {
        public float SpeedMatchMin = 0.70f;
        public float SpeedMatchMax = 1.35f;
        public float ReferenceLegLengthM = 0.6957f;

        // A6 canonical crossfade matrix (seconds)
        public float FadeLocomotion = 0.15f;
        public float FadeIntoCast = 0.05f;
        public float FadeIntoHit = 0.03f;
        public float FadeOutOfHitOrStun = 0.20f;
        public float FadeIntoAir = 0.08f;
        public float FadeDefault = 0.14f;

        readonly Dictionary<string, ClipMetricEntry> _clips = new Dictionary<string, ClipMetricEntry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _standIns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, KidAnimAdaptation> _kids = new Dictionary<string, KidAnimAdaptation>(StringComparer.OrdinalIgnoreCase);

        public int ClipCount => _clips.Count;
        public int KidCount => _kids.Count;

        public bool TryGetClip(string clipName, out ClipMetricEntry entry)
        {
            if (string.IsNullOrEmpty(clipName)) { entry = default; return false; }
            return _clips.TryGetValue(clipName, out entry);
        }

        public bool TryGetStandIn(string requestedClip, out string standIn)
        {
            if (string.IsNullOrEmpty(requestedClip)) { standIn = null; return false; }
            return _standIns.TryGetValue(requestedClip, out standIn);
        }

        public KidAnimAdaptation GetKidAdaptation(string kidId)
        {
            if (!string.IsNullOrEmpty(kidId) && _kids.TryGetValue(kidId, out KidAnimAdaptation adapt))
                return adapt;
            return KidAnimAdaptation.Default;
        }

        public static AnimClipMetricsData CreateDefault()
        {
            var data = new AnimClipMetricsData();
            // Defaults aligned to rules__game_rules.json (walk 4.5 m/s, run 5.25 m/s, sprint 6.0 m/s)
            data._clips["walk"] = new ClipMetricEntry("walk", 1.0f, 30, 30f, true, 3.6f, 3.6f, 0f, 0, 0.15f, "locomotion", false, "loco", 0f);
            data._clips["Run"] = new ClipMetricEntry("Run", 0.60f, 18, 30f, true, 3.15f, 5.25f, 0f, 0, 0.15f, "locomotion", false, "loco", 0f);
            data._clips["sprint"] = new ClipMetricEntry("sprint", 0.53f, 16, 30f, true, 3.2f, 6.0f, 0f, 0, 0.15f, "locomotion", false, "loco", 0f);
            data._clips["Exhausted_Run"] = new ClipMetricEntry("Exhausted_Run", 0.70f, 21, 30f, true, 2.8f, 4.0f, 0f, 0, 0.15f, "locomotion", false, "loco", 0f);
            data._clips["Cast_OneHand"] = new ClipMetricEntry("Cast_OneHand", 0.90f, 27, 30f, false, 0f, 0f, 0.30f, 9, 0.05f, "cast", true, "none", 0f);
            data._clips["Cast_Run"] = new ClipMetricEntry("Cast_Run", 0.90f, 27, 30f, false, 0f, 0f, 0.30f, 9, 0.05f, "cast", true, "none", 0f);
            data._clips["throw"] = new ClipMetricEntry("throw", 0.80f, 24, 30f, false, 0f, 0f, 0.27f, 8, 0.05f, "cast", true, "none", 0f);
            return data;
        }

        public static AnimClipMetricsData ParseJson(string json)
        {
            AnimClipMetricsData fallback = CreateDefault();
            if (string.IsNullOrWhiteSpace(json)) return fallback;

            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch
            {
                return fallback;
            }
            if (root == null) return fallback;

            if (root.TryGetValue("data", out object wrapped) && wrapped is Dictionary<string, object> inner)
                root = inner;

            var result = CreateDefault();
            result.ReferenceLegLengthM = JsonRead.Float(root, "referenceLegLengthM", result.ReferenceLegLengthM);

            Dictionary<string, object> clamp = JsonRead.Obj(root, "speedMatchClamp");
            if (clamp != null)
            {
                result.SpeedMatchMin = Mathf.Clamp(JsonRead.Float(clamp, "min", 0.70f), 0.35f, 1.0f);
                result.SpeedMatchMax = Mathf.Clamp(JsonRead.Float(clamp, "max", 1.35f), 1.0f, 2.0f);
            }

            Dictionary<string, object> fades = JsonRead.Obj(root, "crossfadeMatrixSec");
            if (fades != null)
            {
                result.FadeLocomotion = JsonRead.Float(fades, "locomotionToLocomotion", 0.15f);
                result.FadeIntoCast = JsonRead.Float(fades, "intoCast", 0.05f);
                result.FadeIntoHit = JsonRead.Float(fades, "intoHit", 0.03f);
                result.FadeOutOfHitOrStun = JsonRead.Float(fades, "outOfHitOrStun", 0.20f);
                result.FadeIntoAir = JsonRead.Float(fades, "intoAir", 0.08f);
                result.FadeDefault = JsonRead.Float(fades, "default", 0.14f);
            }

            Dictionary<string, object> clipsObj = JsonRead.Obj(root, "clips");
            if (clipsObj != null)
            {
                foreach (var kv in clipsObj)
                {
                    if (!(kv.Value is Dictionary<string, object> cd)) continue;
                    string clipName = JsonRead.Str(cd, "name", kv.Key);
                    result._clips[clipName] = new ClipMetricEntry(
                        clipName,
                        JsonRead.Float(cd, "durationSec", 0f),
                        JsonRead.Int(cd, "frameCount", 0),
                        JsonRead.Float(cd, "fps", 30f),
                        JsonRead.Bool(cd, "loop", true),
                        JsonRead.Float(cd, "strideDistanceM", 0f),
                        JsonRead.Float(cd, "strideSpeedMps", 0f),
                        JsonRead.Float(cd, "releaseTimeSec", 0f),
                        JsonRead.Int(cd, "releaseFrame", 0),
                        JsonRead.Float(cd, "crossfadeInSec", result.FadeDefault),
                        JsonRead.Str(cd, "archetype", ""),
                        JsonRead.Bool(cd, "upperBodyMask", false),
                        JsonRead.Str(cd, "syncGroup", "none"),
                        JsonRead.Float(cd, "maxFootSlideM", 0f));
                }
            }

            Dictionary<string, object> standInsObj = JsonRead.Obj(root, "standIns");
            if (standInsObj != null)
            {
                foreach (var kv in standInsObj)
                {
                    if (kv.Value is string target && !string.IsNullOrEmpty(target))
                        result._standIns[kv.Key] = target;
                }
            }

            Dictionary<string, object> kidsObj = JsonRead.Obj(root, "kids");
            if (kidsObj != null)
            {
                foreach (var kv in kidsObj)
                {
                    if (!(kv.Value is Dictionary<string, object> kd)) continue;
                    result._kids[kv.Key.Trim().ToLowerInvariant()] = new KidAnimAdaptation(
                        JsonRead.Float(kd, "legLengthM", result.ReferenceLegLengthM),
                        JsonRead.Float(kd, "legLengthRatio", 1f),
                        JsonRead.Float(kd, "girthArmAbductDeg", 0f),
                        JsonRead.Float(kd, "girthHipSpreadDeg", 0f),
                        JsonRead.Float(kd, "aimYawMaxDeg", 55f),
                        JsonRead.Float(kd, "aimPitchMaxDeg", 35f));
                }
            }

            return result;
        }
    }

    public readonly struct RigCapsuleDef
    {
        public readonly string Name;
        public readonly string Bone;
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly float Radius;

        public RigCapsuleDef(string name, string bone, Vector3 a, Vector3 b, float radius)
        {
            Name = name ?? "";
            Bone = bone ?? "";
            A = a;
            B = b;
            Radius = Mathf.Max(0.01f, radius);
        }
    }

    public readonly struct RigTwistBoneDef
    {
        public readonly string Bone;
        public readonly string Source;
        public readonly float Share;

        public RigTwistBoneDef(string bone, string source, float share)
        {
            Bone = bone ?? "";
            Source = source ?? "";
            Share = share;
        }
    }

    public readonly struct RigHelperBoneDef
    {
        public readonly string Bone;
        public readonly string Source;
        public readonly float Share;

        public RigHelperBoneDef(string bone, string source, float share)
        {
            Bone = bone ?? "";
            Source = source ?? "";
            Share = share;
        }
    }

    public sealed class RigSpringChainDef
    {
        public string Name;
        public string Kind;
        public string RootParent;
        public string[] Bones;
        public float Stiffness;
        public float Damping;
        public float Gravity;
        public string[] Colliders;
    }

    public sealed class CorrectiveDriverDef
    {
        public string Shape;
        /// <summary>"hinge", "cone", "squash", or "combo".</summary>
        public string Kind;
        public string Bone;
        public string Parent;
        public Vector3 Axis;
        public Vector3 TargetDir;
        public Vector3 BoneRestDir;
        public float RestAngleDeg;
        public float StartAngleDeg;
        public float FullAngleDeg;
        public string[] Drivers;
    }

    /// <summary>
    /// Parsed per-kid <c>&lt;kid&gt;_rig.json</c> (capsules, girth offsets, leg-length ratio, twist/helper rules,
    /// spring chains, pose-space corrective drivers, texture paths).
    /// </summary>
    public sealed class CharacterRigData
    {
        public int SchemaVersion = 1;
        public string KidId = "";
        public string CoordSpace = "gltf_yup";
        public float HeightM = 1.52f;
        public float LegLengthM = 0.70f;
        public float RefLegLengthM = 0.70f;
        public float LegLengthRatio = 1f;
        public float TorsoRadiusM = 0.14f;
        public float ArmRadiusM = 0.045f;
        public float ThighRadiusM = 0.075f;
        public float ArmAbductOffsetDeg;
        public float HipSpreadOffsetDeg;
        public string NormalTexturePath = "";
        public string OrmTexturePath = "";

        public readonly Dictionary<string, RigCapsuleDef> Capsules = new Dictionary<string, RigCapsuleDef>(StringComparer.OrdinalIgnoreCase);
        public RigTwistBoneDef[] TwistBones = Array.Empty<RigTwistBoneDef>();
        public RigHelperBoneDef[] HelperBones = Array.Empty<RigHelperBoneDef>();
        public RigSpringChainDef[] SpringChains = Array.Empty<RigSpringChainDef>();
        public CorrectiveDriverDef[] Correctives = Array.Empty<CorrectiveDriverDef>();

        public static CharacterRigData ParseJson(string json, string fallbackKidId = "")
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch
            {
                return null;
            }
            if (root == null) return null;

            if (root.TryGetValue("data", out object wrapped) && wrapped is Dictionary<string, object> inner)
                root = inner;

            string coordSpace = JsonRead.Str(root, "coordSpace", "gltf_yup");
            bool isGltf = !string.Equals(coordSpace, "unity", StringComparison.OrdinalIgnoreCase);
            string kidId = JsonRead.Str(root, "kid", fallbackKidId ?? "").Trim().ToLowerInvariant();

            var rig = new CharacterRigData
            {
                SchemaVersion = JsonRead.Int(root, "schemaVersion", 1),
                KidId = kidId,
                CoordSpace = coordSpace,
                HeightM = JsonRead.Float(root, "heightM", 1.52f),
                LegLengthM = JsonRead.Float(root, "legLengthM", 0.70f),
                RefLegLengthM = JsonRead.Float(root, "refLegLengthM", 0.70f),
                LegLengthRatio = Mathf.Clamp(JsonRead.Float(root, "legLengthRatio", 1f), 0.65f, 1.45f),
            };

            Dictionary<string, object> girth = JsonRead.Obj(root, "girth");
            if (girth != null)
            {
                rig.TorsoRadiusM = JsonRead.Float(girth, "torsoRadiusM", rig.TorsoRadiusM);
                rig.ArmRadiusM = JsonRead.Float(girth, "armRadiusM", rig.ArmRadiusM);
                rig.ThighRadiusM = JsonRead.Float(girth, "thighRadiusM", rig.ThighRadiusM);
                rig.ArmAbductOffsetDeg = JsonRead.Float(girth, "armAbductOffsetDeg", 0f);
                rig.HipSpreadOffsetDeg = JsonRead.Float(girth, "hipSpreadOffsetDeg", 0f);
            }

            Dictionary<string, object> textures = JsonRead.Obj(root, "textures");
            if (textures != null)
            {
                rig.NormalTexturePath = JsonRead.Str(textures, "normal", "");
                rig.OrmTexturePath = JsonRead.Str(textures, "orm", "");
            }

            Dictionary<string, object> capsules = JsonRead.Obj(root, "capsules");
            if (capsules != null)
            {
                foreach (var kv in capsules)
                {
                    if (!(kv.Value is Dictionary<string, object> cd)) continue;
                    Vector3 a = JsonRead.TryVec3(cd, "a", out Vector3 va) ? RigSpaceConvert.PositionOrDirection(va, isGltf) : Vector3.zero;
                    Vector3 b = JsonRead.TryVec3(cd, "b", out Vector3 vb) ? RigSpaceConvert.PositionOrDirection(vb, isGltf) : Vector3.up * 0.2f;
                    rig.Capsules[kv.Key] = new RigCapsuleDef(
                        kv.Key,
                        RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(cd, "bone", kv.Key)),
                        a, b, JsonRead.Float(cd, "radius", 0.06f));
                }
            }

            List<object> twistList = JsonRead.Arr(root, "twistBones");
            if (twistList != null && twistList.Count > 0)
            {
                var arr = new List<RigTwistBoneDef>(twistList.Count);
                for (int i = 0; i < twistList.Count; i++)
                {
                    if (!(twistList[i] is Dictionary<string, object> td)) continue;
                    arr.Add(new RigTwistBoneDef(
                        RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(td, "bone", "")),
                        RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(td, "source", "")),
                        JsonRead.Float(td, "share", 0.5f)));
                }
                rig.TwistBones = arr.ToArray();
            }

            List<object> helperList = JsonRead.Arr(root, "helperBones");
            if (helperList != null && helperList.Count > 0)
            {
                var arr = new List<RigHelperBoneDef>(helperList.Count);
                for (int i = 0; i < helperList.Count; i++)
                {
                    if (!(helperList[i] is Dictionary<string, object> hd)) continue;
                    arr.Add(new RigHelperBoneDef(
                        RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(hd, "bone", "")),
                        RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(hd, "source", "")),
                        JsonRead.Float(hd, "share", 0.5f)));
                }
                rig.HelperBones = arr.ToArray();
            }

            List<object> springList = JsonRead.Arr(root, "springChains");
            if (springList != null && springList.Count > 0)
            {
                var arr = new List<RigSpringChainDef>(springList.Count);
                for (int i = 0; i < springList.Count; i++)
                {
                    if (!(springList[i] is Dictionary<string, object> sd)) continue;
                    string[] bones = JsonRead.StrArray(sd, "bones");
                    if (bones.Length == 0) continue;
                    for (int b = 0; b < bones.Length; b++) bones[b] = RigJointLimitsData.StripMixamoPrefix(bones[b]);
                    arr.Add(new RigSpringChainDef
                    {
                        Name = JsonRead.Str(sd, "name", "spring_" + i),
                        Kind = JsonRead.Str(sd, "kind", "cloth"),
                        RootParent = RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(sd, "rootParent", "")),
                        Bones = bones,
                        Stiffness = Mathf.Clamp(JsonRead.Float(sd, "stiffness", 0.35f), 0.05f, 0.95f),
                        Damping = Mathf.Clamp(JsonRead.Float(sd, "damping", 0.18f), 0.02f, 0.90f),
                        Gravity = JsonRead.Float(sd, "gravity", 0.45f),
                        Colliders = JsonRead.StrArray(sd, "colliders"),
                    });
                }
                rig.SpringChains = arr.ToArray();
            }

            List<object> corrList = JsonRead.Arr(root, "correctives");
            if (corrList != null && corrList.Count > 0)
            {
                var arr = new List<CorrectiveDriverDef>(corrList.Count);
                for (int i = 0; i < corrList.Count; i++)
                {
                    if (!(corrList[i] is Dictionary<string, object> cd)) continue;
                    string shape = JsonRead.Str(cd, "shape", "");
                    if (string.IsNullOrEmpty(shape)) continue;
                    Vector3 axis = JsonRead.TryVec3(cd, "axis", out Vector3 va)
                        ? RigSpaceConvert.RotationAxis(va, isGltf) : Vector3.right;
                    Vector3 targetDir = JsonRead.TryVec3(cd, "targetDir", out Vector3 vt)
                        ? RigSpaceConvert.PositionOrDirection(vt, isGltf).normalized : Vector3.up;
                    Vector3 restDir = JsonRead.TryVec3(cd, "boneRestDir", out Vector3 vr)
                        ? RigSpaceConvert.PositionOrDirection(vr, isGltf).normalized : Vector3.down;

                    arr.Add(new CorrectiveDriverDef
                    {
                        Shape = shape,
                        Kind = JsonRead.Str(cd, "kind", "hinge").ToLowerInvariant(),
                        Bone = RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(cd, "bone", "")),
                        Parent = RigJointLimitsData.StripMixamoPrefix(JsonRead.Str(cd, "parent", "")),
                        Axis = axis,
                        TargetDir = targetDir,
                        BoneRestDir = restDir,
                        RestAngleDeg = JsonRead.Float(cd, "restAngleDeg", 0f),
                        StartAngleDeg = JsonRead.Float(cd, "startAngleDeg", 35f),
                        FullAngleDeg = JsonRead.Float(cd, "fullAngleDeg", 120f),
                        Drivers = JsonRead.StrArray(cd, "drivers"),
                    });
                }
                rig.Correctives = arr.ToArray();
            }

            return rig;
        }
    }

    internal static class JsonRead
    {
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is Dictionary<string, object> obj)
                return obj;
            return null;
        }

        public static List<object> Arr(Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is List<object> list)
                return list;
            return null;
        }

        public static string Str(Dictionary<string, object> d, string key, string fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is string s)
                return s;
            return fallback;
        }

        public static float Float(Dictionary<string, object> d, string key, float fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
                return ToFloat(v, fallback);
            return fallback;
        }

        public static int Int(Dictionary<string, object> d, string key, int fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { }
            }
            return fallback;
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is bool b)
                return b;
            return fallback;
        }

        public static float ToFloat(object raw, float fallback)
        {
            if (raw == null) return fallback;
            try
            {
                float f = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return float.IsNaN(f) || float.IsInfinity(f) ? fallback : f;
            }
            catch
            {
                return fallback;
            }
        }

        public static bool TryVec3(Dictionary<string, object> d, string key, out Vector3 vec)
        {
            vec = Vector3.zero;
            if (d == null || !d.TryGetValue(key, out object raw) || raw == null) return false;
            if (raw is List<object> list && list.Count >= 3)
            {
                vec = new Vector3(ToFloat(list[0], 0f), ToFloat(list[1], 0f), ToFloat(list[2], 0f));
                return true;
            }
            if (raw is Dictionary<string, object> obj)
            {
                vec = new Vector3(Float(obj, "x", 0f), Float(obj, "y", 0f), Float(obj, "z", 0f));
                return true;
            }
            return false;
        }

        public static string[] StrArray(Dictionary<string, object> d, string key)
        {
            List<object> list = Arr(d, key);
            if (list == null || list.Count == 0) return Array.Empty<string>();
            var res = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
                if (list[i] is string s && !string.IsNullOrEmpty(s)) res.Add(s);
            return res.Count > 0 ? res.ToArray() : Array.Empty<string>();
        }
    }

    /// <summary>
    /// Where the game finds what tools/unity_export/sync_assets.sh copied in (Assets/Imported/Resources/...) and what KalyeahBuild.Setup
    /// generated (Assets/Kalyeah/Generated/Resources/KalyeahMaterials). Paths mirror the Godot project's assets/ folder.
    /// </summary>
    public static class KalyeahResources
    {
        sealed class ReferenceMaterialComparer : IEqualityComparer<Material>
        {
            public static readonly ReferenceMaterialComparer Instance = new ReferenceMaterialComparer();
            public bool Equals(Material x, Material y) => ReferenceEquals(x, y);
            public int GetHashCode(Material obj) => RuntimeHelpers.GetHashCode(obj);
        }

        static readonly Dictionary<Material, Material> SanitizedMaterialCache =
            new Dictionary<Material, Material>(64, ReferenceMaterialComparer.Instance);
        static readonly Dictionary<string, Material> KidMaterialCache =
            new Dictionary<string, Material>(32, StringComparer.Ordinal);
        static readonly Dictionary<string, CharacterRigData> RigCache =
            new Dictionary<string, CharacterRigData>(16, StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Texture2D> CharacterTexCache =
            new Dictionary<string, Texture2D>(32, StringComparer.OrdinalIgnoreCase);
        static RigJointLimitsData s_jointLimitsCache;
        static AnimClipMetricsData s_clipMetricsCache;
        static Material s_nullFallbackMaterial;
        static Material s_blobShadowMaterial;
        static Texture2D s_blobShadowTexture;

        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        static readonly int GltfBaseTexId = Shader.PropertyToID("baseColorTexture");
        static readonly int GltfBaseTexStId = Shader.PropertyToID("baseColorTexture_ST");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GltfNormalTexId = Shader.PropertyToID("normalTexture");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int HasNormalMapId = Shader.PropertyToID("_HasNormalMap");
        static readonly int NormalIsRgbId = Shader.PropertyToID("_NormalIsRgb");
        static readonly int GltfOrmTexId = Shader.PropertyToID("metallicRoughnessTexture");
        static readonly int OrmMapId = Shader.PropertyToID("_OrmMap");
        static readonly int HasOrmMapId = Shader.PropertyToID("_HasOrmMap");
        static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        static readonly int GltfOccTexId = Shader.PropertyToID("occlusionTexture");
        static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        static readonly int GltfEmissiveTexId = Shader.PropertyToID("emissiveTexture");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int GltfEmissiveColorId = Shader.PropertyToID("emissiveFactor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int CullModeId = Shader.PropertyToID("_CullMode");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int AmbientMinId = Shader.PropertyToID("_AmbientMin");
        static readonly int AmbientMaxId = Shader.PropertyToID("_AmbientMax");
        static readonly int EnemyHighlightId = Shader.PropertyToID("_EnemyHighlight");
        static readonly int EnemyHighlightColorId = Shader.PropertyToID("_EnemyHighlightColor");
        static readonly int AllyFresnelColorId = Shader.PropertyToID("_AllyFresnelColor");
        static readonly int AllyFresnelStrengthId = Shader.PropertyToID("_AllyFresnelStrength");
        static readonly int DepthStartId = Shader.PropertyToID("_DepthStart");
        static readonly int DepthEndId = Shader.PropertyToID("_DepthEnd");
        static readonly int DepthBrightenMaxId = Shader.PropertyToID("_DepthBrightenMax");
        static readonly int DepthFresnelBoostId = Shader.PropertyToID("_DepthFresnelBoost");
        static readonly int SkinUnderglowStrengthId = Shader.PropertyToID("_SkinUnderglowStrength");
        static readonly int FresnelUpWeightId = Shader.PropertyToID("_FresnelUpWeight");

        /// <summary>Subtle neutral-blue VALORANT ally Fresnel rim color shared by <c>Kalyeah/Kid</c> and <see cref="KidView"/>.</summary>
        public static readonly Color DefaultAllyFresnelColor = new Color(0.58f, 0.80f, 1.00f, 1.0f);
        public const float DefaultAllyFresnelStrength = 0.28f;

        /// <summary>unity/Data/*.json as the view and the sim read it.</summary>
        public static DataSource Data() => new DataSource(name =>
        {
            TextAsset text = Resources.Load<TextAsset>("Data/" + name);
            return text != null ? text.text : null;
        });

        /// <summary>A .glb that glTFast imported (folder "characters", "barangay", "props" or "weapons"), or null.</summary>
        public static GameObject Model(string folder, string id) => Resources.Load<GameObject>($"models/{folder}/{id}");

        /// <summary>
        /// Loads a character LOD prefab: <c>0 =&gt; &lt;kid&gt;.glb</c>, <c>1 =&gt; &lt;kid&gt;_lod1.glb</c>,
        /// <c>2 =&gt; &lt;kid&gt;_lod2.glb</c>. Returns null cleanly when optional LOD1/LOD2 assets are not synced yet.
        /// </summary>
        public static GameObject CharacterModel(string kidId, int lodIndex = 0)
        {
            if (string.IsNullOrEmpty(kidId)) return null;
            string clean = kidId.Trim().ToLowerInvariant();
            if (lodIndex <= 0) return Model("characters", clean);
            string suffix = lodIndex == 1 ? "_lod1" : "_lod2";
            return Model("characters", clean + suffix);
        }

        public static Texture2D CharacterNormalMap(string kidId, string explicitPath = null)
        {
            string key = !string.IsNullOrEmpty(explicitPath) ? explicitPath : $"models/characters/{kidId}_2";
            return CachedCharacterTexture(key);
        }

        public static Texture2D CharacterOrmMap(string kidId, string explicitPath = null)
        {
            string key = !string.IsNullOrEmpty(explicitPath) ? explicitPath : $"models/characters/{kidId}_orm";
            return CachedCharacterTexture(key);
        }

        static Texture2D CachedCharacterTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (CharacterTexCache.TryGetValue(path, out Texture2D cached))
                return cached;
            Texture2D loaded = Resources.Load<Texture2D>(path);
            CharacterTexCache[path] = loaded;
            return loaded;
        }

        /// <summary>Loads &amp; caches optional <c>models/characters/&lt;kid&gt;_rig.json</c> (returns null safely if absent).</summary>
        public static CharacterRigData CharacterRig(string kidId)
        {
            if (string.IsNullOrEmpty(kidId)) return null;
            string clean = kidId.Trim().ToLowerInvariant();
            if (RigCache.TryGetValue(clean, out CharacterRigData cached))
                return cached;

            TextAsset asset = Resources.Load<TextAsset>($"models/characters/{clean}_rig");
            CharacterRigData parsed = asset != null ? CharacterRigData.ParseJson(asset.text, clean) : null;
            RigCache[clean] = parsed;
            return parsed;
        }

        /// <summary>Loads &amp; caches <c>Data/rig__joint_limits.json</c>, falling back to canonical R2 limits if absent.</summary>
        public static RigJointLimitsData JointLimits(DataSource data = null)
        {
            if (s_jointLimitsCache != null) return s_jointLimitsCache;
            string raw = TryReadOptionalDataText("rig__joint_limits", data);
            s_jointLimitsCache = RigJointLimitsData.ParseJson(raw);
            return s_jointLimitsCache;
        }

        /// <summary>Loads &amp; caches <c>Data/anim__clip_metrics.json</c>, falling back to A4/A6 defaults if absent.</summary>
        public static AnimClipMetricsData ClipMetrics(DataSource data = null)
        {
            if (s_clipMetricsCache != null) return s_clipMetricsCache;
            string raw = TryReadOptionalDataText("anim__clip_metrics", data);
            s_clipMetricsCache = AnimClipMetricsData.ParseJson(raw);
            return s_clipMetricsCache;
        }

        static string TryReadOptionalDataText(string name, DataSource data)
        {
            if (data != null)
            {
                try { return data.Text(name); }
                catch { }
            }
            TextAsset asset = Resources.Load<TextAsset>("Data/" + name);
            return asset != null ? asset.text : null;
        }

        /// <summary>Valorant-style colorblind-safe enemy highlight color (Red / Yellow / Purple per docs/SPEC.md §10).</summary>
        public static Color ResolveEnemyHighlightColor(int highlightMode)
        {
            switch (highlightMode)
            {
                case SettingsStore.EnemyHighlightYellow: return new Color(1.00f, 0.88f, 0.16f, 1.0f);
                case SettingsStore.EnemyHighlightPurple: return new Color(0.76f, 0.34f, 1.00f, 1.0f);
                default: return new Color(1.00f, 0.22f, 0.24f, 1.0f);
            }
        }

        /// <summary>
        /// Cached soft neutral drop-shadow material for Low preset blob shadows and mid-jump height readability
        /// (<c>docs/SPEC.md</c> §10 &amp; <c>docs/GRAPHICS.md</c> L207). Zero per-kid allocations.
        /// </summary>
        public static Material SharedBlobShadowMaterial()
        {
            if (s_blobShadowMaterial != null && s_blobShadowMaterial.shader != null && s_blobShadowMaterial.shader.isSupported)
                return s_blobShadowMaterial;

            Material baseMat = Material("TransparentUnlit", "Universal Render Pipeline/Unlit");
            if (baseMat == null) return null;

            if (s_blobShadowTexture == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: false)
                {
                    name = "KalyeahBlobShadowTex",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    float dy = (y - half) / half;
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - half) / half;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * 0.42f;
                        pixels[y * size + x] = new Color32(14, 18, 28, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                s_blobShadowTexture = tex;
            }

            var mat = new Material(baseMat)
            {
                name = "KalyeahBlobShadowMat",
                hideFlags = HideFlags.DontSave,
            };
            if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, s_blobShadowTexture);
            if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, s_blobShadowTexture);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, Color.white);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            s_blobShadowMaterial = mat;
            return s_blobShadowMaterial;
        }

        /// <summary>
        /// Upgrades a kid hierarchy's materials to the SRP-batched <c>Kalyeah/Kid</c> Valorant soft-painted shader
        /// (wiring base color, optional <c>&lt;kid&gt;_2.png</c> normal map, and <c>&lt;kid&gt;_orm.png</c> AO/Roughness/Metallic)
        /// with caching and graceful fallback to <see cref="SanitizeModelMaterials"/> when the shader is unavailable.
        /// </summary>
        public static void UpgradeKidMaterials(GameObject root, string kidId, CharacterRigData rigData = null)
        {
            if (root == null) return;
            Shader kidShader = ResolveKidShader();
            if (kidShader == null)
            {
                SanitizeModelMaterials(root);
                return;
            }

            string cleanKid = string.IsNullOrEmpty(kidId) ? "" : kidId.Trim().ToLowerInvariant();
            Texture2D extNormal = !string.IsNullOrEmpty(cleanKid)
                ? CharacterNormalMap(cleanKid, rigData != null ? rigData.NormalTexturePath : null)
                : null;
            Texture2D extOrm = !string.IsNullOrEmpty(cleanKid)
                ? CharacterOrmMap(cleanKid, rigData != null ? rigData.OrmTexturePath : null)
                : null;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src != null && src.shader == kidShader) continue;
                    Material upgraded = GetOrCreateKidMaterial(src, cleanKid, kidShader, extNormal, extOrm);
                    if (upgraded != null && !ReferenceEquals(upgraded, src))
                    {
                        mats[i] = upgraded;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        static Shader ResolveKidShader()
        {
            Material preset = Resources.Load<Material>("KalyeahMaterials/Kid");
            if (preset != null && preset.shader != null && preset.shader.isSupported)
                return preset.shader;
            Shader found = Shader.Find("Kalyeah/Kid");
            return found != null && found.isSupported ? found : null;
        }

        static Material GetOrCreateKidMaterial(
            Material src,
            string kidId,
            Shader kidShader,
            Texture2D extNormal,
            Texture2D extOrm)
        {
            int srcHash = src != null ? RuntimeHelpers.GetHashCode(src) : 0;
            string cacheKey = $"{kidId}:{srcHash}";
            if (KidMaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null && cached.shader == kidShader)
                return cached;

            var mat = new Material(kidShader)
            {
                name = (src != null ? src.name : kidId) + "_KalyeahKid",
                hideFlags = HideFlags.DontSave,
            };

            if (src != null)
            {
                CopyPbrProperties(src, mat);
            }
            else
            {
                mat.SetColor(BaseColorId, Color.white);
            }

            // Wire tangent normal map (prefer embedded glTF normal if present, else external <kid>_2.png OpenGL RGB normal).
            Texture embeddedNormal = src != null ? FirstTexture(src, GltfNormalTexId, BumpMapId, 0) : null;
            Texture chosenNormal = embeddedNormal != null ? embeddedNormal : extNormal;
            if (chosenNormal != null)
            {
                mat.SetTexture(BumpMapId, chosenNormal);
                mat.SetFloat(HasNormalMapId, 1f);
                // External Resources/<kid>_2.png is imported as TextureImporterType.Default (raw OpenGL RGB normal).
                bool isExternalRgb = ReferenceEquals(chosenNormal, extNormal);
                mat.SetFloat(NormalIsRgbId, isExternalRgb ? 1f : 0f);
            }
            else
            {
                mat.SetFloat(HasNormalMapId, 0f);
            }

            // Wire packed ORM map (R=AO, G=Roughness, B=Metallic).
            Texture embeddedOrm = src != null ? FirstTexture(src, GltfOrmTexId, OrmMapId, 0) : null;
            Texture chosenOrm = extOrm != null ? (Texture)extOrm : embeddedOrm;
            if (chosenOrm != null)
            {
                mat.SetTexture(OrmMapId, chosenOrm);
                mat.SetFloat(HasOrmMapId, 1f);
            }
            else
            {
                mat.SetFloat(HasOrmMapId, 0f);
            }

            // Guarantee clean VALORANT clarity defaults so clean allies use the zero-MPB SRP Batcher fast path.
            if (mat.HasProperty(AmbientMinId)) mat.SetFloat(AmbientMinId, 0.28f);
            if (mat.HasProperty(AmbientMaxId)) mat.SetFloat(AmbientMaxId, 0.82f);
            if (mat.HasProperty(EnemyHighlightId)) mat.SetFloat(EnemyHighlightId, 0f);
            if (mat.HasProperty(EnemyHighlightColorId)) mat.SetColor(EnemyHighlightColorId, Color.clear);
            if (mat.HasProperty(AllyFresnelColorId)) mat.SetColor(AllyFresnelColorId, DefaultAllyFresnelColor);
            if (mat.HasProperty(AllyFresnelStrengthId)) mat.SetFloat(AllyFresnelStrengthId, DefaultAllyFresnelStrength);
            if (mat.HasProperty(DepthStartId)) mat.SetFloat(DepthStartId, 12f);
            if (mat.HasProperty(DepthEndId)) mat.SetFloat(DepthEndId, 45f);
            if (mat.HasProperty(DepthBrightenMaxId)) mat.SetFloat(DepthBrightenMaxId, 0.26f);
            if (mat.HasProperty(DepthFresnelBoostId)) mat.SetFloat(DepthFresnelBoostId, 0.72f);
            if (mat.HasProperty(SkinUnderglowStrengthId)) mat.SetFloat(SkinUnderglowStrengthId, 0.28f);
            if (mat.HasProperty(FresnelUpWeightId)) mat.SetFloat(FresnelUpWeightId, 0.65f);

            KidMaterialCache[cacheKey] = mat;
            return mat;
        }

        public static Texture2D Texture(string path) => Resources.Load<Texture2D>(path);

        public static Font UiFont() => FontOr("fonts/brand/KalyeahSans-Variable");

        /// <summary>Barlow Condensed Bold, the owner's pick from the home plan (font A); the old signboard face if it is missing.</summary>
        public static Font DisplayFont() => Resources.Load<Font>("fonts/brand/BarlowCondensed-Bold") ?? FontOr("fonts/brand/KalyeahDisplay-Regular");

        static Font FontOr(string path)
        {
            Font font = Resources.Load<Font>(path);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A material KalyeahBuild.Setup made (so its shader is in the build); falls back to a fresh one in the editor.</summary>
        public static Material Material(string name, string shader)
        {
            Material made = Resources.Load<Material>("KalyeahMaterials/" + name);
            if (made != null) return made;
            Shader found = Shader.Find(shader);
            return found != null ? new Material(found) : null;
        }

        public static Vector3 U(SVector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static SVector2 S(Vector2 v) => new SVector2(v.x, v.y);

        /// <summary>A sim ground point (x, z) as a Unity world point (GodotSpace.World).</summary>
        public static Vector3 World(SVector2 ground, float y = 0f) => U(GodotSpace.World(ground, y));

        /// <summary>Object.Destroy in play, DestroyImmediate in the editor's edit mode (the screenshots build the match there).</summary>
        public static void Kill(Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        /// <summary>The world bounds of every renderer under `root` (its size at identity is the model's own size).</summary>
        public static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Ensures every Renderer under `root` uses a supported URP / glTFast ShaderGraph material, replacing any null, unsupported,
        /// InternalErrorShader, Built-In Standard, or Built-In glTF/* material while preserving baseColorTexture, UV ST, and baseColorFactor.
        /// Cached by source Material instance ID so repeated model spawns share materials and allocate zero per-instance duplicates.
        /// </summary>
        public static void SanitizeModelMaterials(GameObject root)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src != null && !NeedsMaterialSanitize(src.shader)) continue;
                    Material fixedMat = GetOrCreateSanitizedMaterial(src);
                    if (fixedMat != null && !ReferenceEquals(fixedMat, src))
                    {
                        mats[i] = fixedMat;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        static bool NeedsMaterialSanitize(Shader shader)
        {
            if (shader == null || !shader.isSupported) return true;
            string n = shader.name;
            if (string.IsNullOrEmpty(n)) return true;
            return n == "Standard"
                || n.StartsWith("Legacy Shaders/", StringComparison.Ordinal)
                || n.StartsWith("glTF/", StringComparison.Ordinal)
                || n.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static Material GetOrCreateSanitizedMaterial(Material src)
        {
            if (src == null)
            {
                if (s_nullFallbackMaterial != null && !NeedsMaterialSanitize(s_nullFallbackMaterial.shader))
                    return s_nullFallbackMaterial;
                Shader fallbackShader = ResolveValidPbrShader();
                if (fallbackShader == null) return null;
                s_nullFallbackMaterial = new Material(fallbackShader)
                {
                    name = "KalyeahSanitizedDefault",
                    hideFlags = HideFlags.DontSave,
                };
                if (s_nullFallbackMaterial.HasProperty(BaseColorId)) s_nullFallbackMaterial.SetColor(BaseColorId, Color.white);
                if (s_nullFallbackMaterial.HasProperty(GltfBaseColorId)) s_nullFallbackMaterial.SetColor(GltfBaseColorId, Color.white);
                if (s_nullFallbackMaterial.HasProperty(SmoothnessId)) s_nullFallbackMaterial.SetFloat(SmoothnessId, 0.12f);
                if (s_nullFallbackMaterial.HasProperty(CullId)) s_nullFallbackMaterial.SetFloat(CullId, 0f);
                return s_nullFallbackMaterial;
            }

            if (SanitizedMaterialCache.TryGetValue(src, out Material cached) && cached != null && !NeedsMaterialSanitize(cached.shader))
                return cached;

            Shader targetShader = ResolveValidPbrShader();
            if (targetShader == null) return src;

            bool isGltfShaderGraph = targetShader.name != null && targetShader.name.StartsWith("Shader Graphs/glTF-", StringComparison.Ordinal);
            Material replacement = isGltfShaderGraph ? new Material(src) { shader = targetShader } : new Material(targetShader);
            replacement.name = src.name + "_URP";
            replacement.hideFlags = HideFlags.DontSave;
            CopyPbrProperties(src, replacement);
            SanitizedMaterialCache[src] = replacement;
            return replacement;
        }

        static Shader ResolveValidPbrShader()
        {
            Material gltfMat = Resources.Load<Material>("KalyeahMaterials/GltfPbr");
            if (gltfMat != null && !NeedsMaterialSanitize(gltfMat.shader)) return gltfMat.shader;

            Shader sg = Shader.Find("Shader Graphs/glTF-pbrMetallicRoughness");
            if (!NeedsMaterialSanitize(sg)) return sg;

            Material groundMat = Resources.Load<Material>("KalyeahMaterials/Ground");
            if (groundMat != null && !NeedsMaterialSanitize(groundMat.shader)) return groundMat.shader;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (!NeedsMaterialSanitize(urpLit)) return urpLit;

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            return !NeedsMaterialSanitize(urpUnlit) ? urpUnlit : null;
        }

        static void CopyPbrProperties(Material src, Material dst)
        {
            Color baseCol = FirstColor(src, GltfBaseColorId, BaseColorId, LegacyColorId, Color.white);
            if (dst.HasProperty(GltfBaseColorId)) dst.SetColor(GltfBaseColorId, baseCol);
            if (dst.HasProperty(BaseColorId)) dst.SetColor(BaseColorId, baseCol);
            if (dst.HasProperty(LegacyColorId)) dst.SetColor(LegacyColorId, baseCol);

            Texture baseTex = FirstTexture(src, GltfBaseTexId, BaseMapId, MainTexId);
            Vector4 st = ResolveBaseUvSt(src);

            if (baseTex != null)
            {
                if (dst.HasProperty(GltfBaseTexId))
                {
                    dst.SetTexture(GltfBaseTexId, baseTex);
                    if (dst.HasProperty(GltfBaseTexStId)) dst.SetVector(GltfBaseTexStId, st);
                }
                if (dst.HasProperty(BaseMapId))
                {
                    dst.SetTexture(BaseMapId, baseTex);
                    dst.SetTextureScale(BaseMapId, new Vector2(st.x, st.y));
                    dst.SetTextureOffset(BaseMapId, new Vector2(st.z, st.w));
                }
                if (dst.HasProperty(MainTexId))
                {
                    dst.SetTexture(MainTexId, baseTex);
                    dst.SetTextureScale(MainTexId, new Vector2(st.x, st.y));
                    dst.SetTextureOffset(MainTexId, new Vector2(st.z, st.w));
                }
            }

            Texture normalTex = FirstTexture(src, GltfNormalTexId, BumpMapId, 0);
            if (normalTex != null)
            {
                if (dst.HasProperty(GltfNormalTexId)) dst.SetTexture(GltfNormalTexId, normalTex);
                if (dst.HasProperty(BumpMapId)) dst.SetTexture(BumpMapId, normalTex);
            }

            Texture ormTex = FirstTexture(src, GltfOrmTexId, MetallicGlossMapId, 0);
            if (ormTex != null && dst.HasProperty(GltfOrmTexId))
                dst.SetTexture(GltfOrmTexId, ormTex);

            Texture occTex = FirstTexture(src, GltfOccTexId, OcclusionMapId, 0);
            if (occTex != null)
            {
                if (dst.HasProperty(GltfOccTexId)) dst.SetTexture(GltfOccTexId, occTex);
                if (dst.HasProperty(OcclusionMapId)) dst.SetTexture(OcclusionMapId, occTex);
            }

            Texture emiTex = FirstTexture(src, GltfEmissiveTexId, EmissionMapId, 0);
            Color emiCol = FirstColor(src, GltfEmissiveColorId, EmissionColorId, 0, Color.black);
            if (emiTex != null)
            {
                if (dst.HasProperty(GltfEmissiveTexId)) dst.SetTexture(GltfEmissiveTexId, emiTex);
                if (dst.HasProperty(EmissionMapId)) dst.SetTexture(EmissionMapId, emiTex);
            }
            if (emiCol.maxColorComponent > 0.001f)
            {
                if (dst.HasProperty(GltfEmissiveColorId)) dst.SetColor(GltfEmissiveColorId, emiCol);
                if (dst.HasProperty(EmissionColorId)) dst.SetColor(EmissionColorId, emiCol);
            }

            if (dst.HasProperty(CullId))
            {
                float cull = src.HasProperty(CullId)
                    ? src.GetFloat(CullId)
                    : (src.HasProperty(CullModeId) ? src.GetFloat(CullModeId) : 0f);
                dst.SetFloat(CullId, cull);
            }
            if (dst.HasProperty(SmoothnessId) && !dst.HasProperty(GltfBaseTexId))
                dst.SetFloat(SmoothnessId, 0.14f);
            if (dst.HasProperty(MetallicId) && !dst.HasProperty(GltfBaseTexId))
                dst.SetFloat(MetallicId, 0f);
        }

        static Vector4 ResolveBaseUvSt(Material src)
        {
            if (src.HasProperty(GltfBaseTexStId))
            {
                Vector4 st = src.GetVector(GltfBaseTexStId);
                if (Mathf.Abs(st.x) > 0.0001f || Mathf.Abs(st.y) > 0.0001f) return st;
            }
            if (src.HasProperty(GltfBaseTexId))
            {
                Vector2 sc = src.GetTextureScale(GltfBaseTexId);
                Vector2 of = src.GetTextureOffset(GltfBaseTexId);
                if (Mathf.Abs(sc.x) > 0.0001f || Mathf.Abs(sc.y) > 0.0001f)
                    return new Vector4(sc.x, sc.y, of.x, of.y);
            }
            bool fromGltfOrBroken = src.shader == null
                || src.shader.name.StartsWith("glTF/", StringComparison.Ordinal)
                || src.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
            return fromGltfOrBroken ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
        }

        static Texture FirstTexture(Material m, int a, int b, int c)
        {
            if (a != 0 && m.HasProperty(a)) { Texture t = m.GetTexture(a); if (t != null) return t; }
            if (b != 0 && m.HasProperty(b)) { Texture t = m.GetTexture(b); if (t != null) return t; }
            if (c != 0 && m.HasProperty(c)) { Texture t = m.GetTexture(c); if (t != null) return t; }

            int[] savedIds = m.GetTexturePropertyNameIDs();
            for (int i = 0; i < savedIds.Length; i++)
            {
                int id = savedIds[i];
                if (id != a && id != b && id != c) continue;
                Texture t = m.GetTexture(id);
                if (t != null) return t;
            }
            return null;
        }

        static Color FirstColor(Material m, int a, int b, int c, Color fallback)
        {
            if (a != 0 && m.HasProperty(a)) return m.GetColor(a);
            if (b != 0 && m.HasProperty(b)) return m.GetColor(b);
            if (c != 0 && m.HasProperty(c)) return m.GetColor(c);
            return fallback;
        }
    }
}
