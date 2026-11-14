using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kalyeah.Game
{
    /// <summary>
    /// Opt-in phone tilt-to-run and menu parallax helper driven by InputSystem's GravitySensor (ported from scripts/input/tilt_input.gd).
    /// Calibrates "level" on first valid sample so any resting posture works, enforces an 8-degree dead zone before emitting a unit
    /// screen-space run direction (x right, y up), and safely falls back to Vector2.zero when no GravitySensor is present.
    /// </summary>
    public sealed class TiltInput
    {
        public const float DeadZoneDegrees = 8f;
        public const float LeanFullDegrees = 12f;
        public const float LeanFollowRate = 0.6f;
        public const float MinGravity = 0.25f;

        ScreenOrientation _calibratedOrientation;

        /// <summary>Deterministic gravity override for EditMode tests (screen-aligned: x right, y up, z out of screen). Null reads GravitySensor.current.</summary>
        public static Vector3? TestGravity { get; set; }

        /// <summary>True when a test override is set or InputSystem reports a GravitySensor device on this hardware.</summary>
        public static bool IsSensorSupported
        {
            get
            {
                if (TestGravity.HasValue) return true;
                try { return GravitySensor.current != null; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>True when a live gravity vector of meaningful magnitude is currently readable.</summary>
        public static bool IsSensorAvailable
        {
            get
            {
                if (TestGravity.HasValue)
                    return TestGravity.Value.sqrMagnitude >= MinGravity * MinGravity;
                try
                {
                    GravitySensor sensor = GravitySensor.current;
                    if (sensor == null || !sensor.enabled) return false;
                    return sensor.gravity.ReadValue().sqrMagnitude >= MinGravity * MinGravity;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public bool Enabled { get; private set; }

        /// <summary>Unit posture vector taken as level (Vector3.zero means calibrate on the next valid sample).</summary>
        public Vector3 Level { get; private set; }

        public TiltInput(bool enabled = false)
        {
            if (enabled) SetEnabled(true);
        }

        public static void SetSensorEnabled(bool enabled)
        {
            try
            {
                GravitySensor sensor = GravitySensor.current;
                if (sensor == null) return;
                if (enabled && !sensor.enabled)
                    InputSystem.EnableDevice(sensor);
                else if (!enabled && sensor.enabled)
                    InputSystem.DisableDevice(sensor);
            }
            catch (Exception)
            {
                // Headless CI, desktop, or devices without sensor fusion fall back cleanly to zero tilt.
            }
        }

        /// <summary>Opt-in toggle: enables/disables hardware GravitySensor polling and resets posture calibration.</summary>
        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            Level = Vector3.zero;
            SetSensorEnabled(enabled);
        }

        /// <summary>Re-calibrates resting posture on the next valid gravity sample.</summary>
        public void Reset() => Level = Vector3.zero;

        /// <summary>Screen-space tilt shift from calibrated level (x right, y up), or zero if reading is below MinGravity.</summary>
        public Vector2 Offset(Vector3 reading)
        {
            if (reading.sqrMagnitude < MinGravity * MinGravity)
                return Vector2.zero;

            Vector3 now = reading.normalized;
            if (Level == Vector3.zero)
                Level = now;

            Vector3 change = now - Level;
            return new Vector2(change.x, change.y);
        }

        /// <summary>
        /// Pure tilt-to-run evaluator on a screen-aligned gravity vector (x right, y up): returns Vector2.zero inside the 8-degree dead
        /// zone or on the initial calibration sample, and a unit direction once tilted >= 8 degrees from level.
        /// </summary>
        public Vector2 Direction(Vector3 reading)
        {
            if (reading.sqrMagnitude < MinGravity * MinGravity)
                return Vector2.zero;

            Vector3 now = reading.normalized;
            if (Level == Vector3.zero)
            {
                Level = now;
                return Vector2.zero;
            }

            float degrees = Vector3.Angle(Level, now);
            Vector2 shift = new Vector2(now.x - Level.x, now.y - Level.y);
            if (degrees < DeadZoneDegrees || shift.sqrMagnitude < 0.000001f)
                return Vector2.zero;

            return shift.normalized;
        }

        /// <summary>Menu/home parallax lean in [-1..1] per axis (full at 12 degrees) whose level gently follows posture.</summary>
        public Vector2 Lean(Vector3 reading, float dt)
        {
            Vector2 shift = Offset(reading);
            if (Level == Vector3.zero)
                return Vector2.zero;

            Level = Vector3.Slerp(Level, reading.normalized, Mathf.Clamp01(LeanFollowRate * Mathf.Max(0f, dt))).normalized;
            float full = Mathf.Sin(LeanFullDegrees * Mathf.Deg2Rad);
            return new Vector2(
                Mathf.Clamp(shift.x / full, -1f, 1f),
                Mathf.Clamp(shift.y / full, -1f, 1f));
        }

        /// <summary>Samples hardware GravitySensor (or TestGravity) when Enabled is true; returns Vector2.zero when disabled or absent.</summary>
        public Vector2 SampleDirection()
        {
            if (!Enabled) return Vector2.zero;
            return Direction(ReadScreenAlignedGravity());
        }

        /// <summary>Samples gentle parallax lean when Enabled is true; returns Vector2.zero when disabled or absent.</summary>
        public Vector2 SampleLean(float dt)
        {
            if (!Enabled) return Vector2.zero;
            return Lean(ReadScreenAlignedGravity(), dt);
        }

        Vector3 ReadScreenAlignedGravity()
        {
            if (TestGravity.HasValue)
                return TestGravity.Value;

            try
            {
                GravitySensor sensor = GravitySensor.current;
                if (sensor == null) return Vector3.zero;
                if (!sensor.enabled) InputSystem.EnableDevice(sensor);

                ScreenOrientation orientation = ScreenFitMetrics.EffectiveOrientation;
                if (orientation != _calibratedOrientation)
                {
                    _calibratedOrientation = orientation;
                    Level = Vector3.zero;
                }

                Vector3 raw = sensor.gravity.ReadValue();
                return AlignSensorToScreen(raw, orientation);
            }
            catch (Exception)
            {
                return Vector3.zero;
            }
        }

        public static Vector3 AlignSensorToScreen(Vector3 raw, ScreenOrientation orientation)
        {
            switch (orientation)
            {
                case ScreenOrientation.LandscapeLeft:
                    return new Vector3(-raw.y, raw.x, raw.z);
                case ScreenOrientation.LandscapeRight:
                    return new Vector3(raw.y, -raw.x, raw.z);
                case ScreenOrientation.PortraitUpsideDown:
                    return new Vector3(-raw.x, -raw.y, raw.z);
                default:
                    return raw;
            }
        }
    }
}
