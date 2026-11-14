using System;
using System.Numerics;

namespace Kalyeah.Game
{
    /// <summary>
    /// From the Godot game's space to Unity's. Both are Y-up, but Godot is right-handed (a camera looks down -Z) and Unity is
    /// left-handed (a camera looks down +Z). The sim's ground point (x, z) is drawn at Unity (x, 0, -z), so the street keeps its shape
    /// and is not mirrored. A Godot yaw of θ (counter-clockwise from above) is a Unity yaw of 180° - θ for a model imported by glTFast:
    /// glTFast turns a glTF point (x, y, z) into Unity (-x, y, z), Godot keeps it as is, and the two differ by exactly half a turn.
    /// Every placement number taken from scripts/view/*.gd goes through here.
    /// </summary>
    public static class GodotSpace
    {
        const float Deg = 180f / MathF.PI;

        /// <summary>A sim ground point (x, z) at height y, in Unity's world.</summary>
        public static Vector3 World(Vector2 ground, float y = 0f) => new Vector3(ground.X, y, -ground.Y);

        /// <summary>A Godot world point in Unity's world.</summary>
        public static Vector3 World(Vector3 godot) => new Vector3(godot.X, godot.Y, -godot.Z);

        /// <summary>A Unity world point back on the sim's ground (x, z).</summary>
        public static Vector2 Ground(Vector3 unity) => new Vector2(unity.X, -unity.Z);

        /// <summary>The Unity yaw (degrees) of a glTFast model that Godot turned `godotYawRad` round Y.</summary>
        public static float ModelYawDegrees(float godotYawRad) => 180f - godotYawRad * Deg;

        /// <summary>The yaw (radians) the Godot game gives a kid that faces `facing` on the ground (kid_model.gd face()).</summary>
        public static float KidYawRad(Vector2 facing) => MathF.Atan2(-facing.X, -facing.Y);

        /// <summary>
        /// Where a Godot node turned (-pitchDown, yaw, 0) degrees looks (its -Z axis), as a Unity direction: the shoulder camera and the sun.
        /// </summary>
        public static Vector3 Forward(float pitchDownDegrees, float yawDegrees)
        {
            float p = pitchDownDegrees / Deg;
            float y = yawDegrees / Deg;
            var godot = new Vector3(-MathF.Sin(y) * MathF.Cos(p), -MathF.Sin(p), -MathF.Cos(y) * MathF.Cos(p));
            return World(godot);
        }

        /// <summary>
        /// The ground axes of a view turned `yawDegrees` (camera_rules.gd ground_axes): [0] is screen right, [1] is screen down, both on the
        /// sim's ground (x, z).
        /// </summary>
        public static (Vector2 right, Vector2 down) GroundAxes(float yawDegrees)
        {
            float t = yawDegrees / Deg;
            return (Snap(new Vector2(MathF.Cos(t), -MathF.Sin(t))), Snap(new Vector2(-MathF.Sin(t), -MathF.Cos(t))));
        }

        /// <summary>
        /// A way on the screen (x right, y down, as a joystick reads) as a way on the sim's ground for a view turned `yawDegrees`
        /// (camera_rules.gd ground_way): what the joystick feeds PlayerInput.move.
        /// </summary>
        public static Vector2 GroundWay(Vector2 screenWay, float yawDegrees)
        {
            var (right, down) = GroundAxes(yawDegrees);
            return right * screenWay.X - down * screenWay.Y;
        }

        static Vector2 Snap(Vector2 v)
        {
            const float step = 0.000001f;
            return new Vector2(MathF.Round(v.X / step) * step, MathF.Round(v.Y / step) * step);
        }
    }

    /// <summary>
    /// The shoulder camera's pose (camera_rules.gd shoulder_pose), in Unity's world: behind your kid along the view, `shoulder_offset` to
    /// its right, risen so the line of sight passes `shoulder_height` above the feet. Yaw 0 looks down the sim's -Z (toward the enemy end
    /// for team 0); pitch is down-positive.
    /// </summary>
    public static class ShoulderRig
    {
        /// <summary>The eye position and the look direction (both Unity world) for the kid's feet at `feet` (sim XZ).</summary>
        public static (Vector3 eye, Vector3 forward) Pose(CameraRulesData rules, Vector2 feet, float yawDegrees, float pitchDegrees)
        {
            const float deg = MathF.PI / 180f;
            float y = yawDegrees * deg;
            // Godot basis from euler (-pitch, yaw, 0): its z axis (backwards) on the ground, and its x axis (right)
            var back = new Vector3(MathF.Sin(y), 0f, MathF.Cos(y));
            var right = new Vector3(MathF.Cos(y), 0f, -MathF.Sin(y));
            var pivot = new Vector3(feet.X, rules.ShoulderHeight, feet.Y);
            float rise = rules.ShoulderDistance * MathF.Tan(Math.Clamp(pitchDegrees, -80f, 80f) * deg);
            Vector3 at = pivot + back * rules.ShoulderDistance + Vector3.UnitY * rise + right * rules.ShoulderOffset;
            at.Y = MathF.Max(at.Y, 0.5f);
            return (GodotSpace.World(at), GodotSpace.Forward(pitchDegrees, yawDegrees));
        }

        /// <summary>The yaw that looks from `from` toward `to` (sim XZ), in the shoulder camera's convention.</summary>
        public static float YawToward(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float yaw = MathF.Atan2(-d.X, -d.Y) * 180f / MathF.PI;
            return yaw < 0f ? yaw + 360f : yaw;
        }
    }
}
