using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// The third-person shoulder camera (docs/GDD.md "Shoulder view"; scripts/view/arena_camera.gd with CameraRules.third_person):
    /// shoulder_distance behind your kid along the view, shoulder_offset to its right, the line of sight shoulder_height above its feet,
    /// shoulder_fov (vertical, as Godot's) and shoulder_near. A drag on the right of the screen turns it (look_sensitivity degrees per
    /// UI pixel at the 720 px tall base, pitch kept in [look_pitch_min, look_pitch_max]). A match starts looking down the street toward
    /// the other team's end at look_pitch_degrees.
    /// Not ported yet: the auto-follow behind a moving kid, the spring arm that pulls in at walls, hiding props in the way, the FOV kick.
    /// </summary>
    public sealed class ShoulderCamera
    {
        /// <summary>The far plane, metres (arena_camera.gd keeps the whole barangay and its skyline in view).</summary>
        const float Far = 450f;
        /// <summary>The UI's base height: drags are measured in these pixels, as in the Godot game's 1280 x 720 layout.</summary>
        const float BaseHeight = 720f;

        public readonly CameraRulesData Rules;
        /// <summary>Degrees: 0 looks down the sim's -Z. Pitch is down-positive.</summary>
        public float Yaw, Pitch;

        public ShoulderCamera(CameraRulesData rules, int team)
        {
            Rules = rules;
            Yaw = team == SimBridge.LocalTeam ? 0f : 180f;
            Pitch = rules.LookPitchDegrees;
        }

        /// <summary>A drag of `screenPixels` (Unity screen pixels, y up) turns the view: right turns right, up looks up.</summary>
        public void Look(Vector2 screenPixels, float screenHeight)
        {
            float toBase = BaseHeight / Mathf.Max(screenHeight, 1f);
            Yaw = Mathf.Repeat(Yaw - screenPixels.x * toBase * Rules.LookSensitivity, 360f);
            Pitch = Rules.ClampPitch(Pitch - screenPixels.y * toBase * Rules.LookSensitivity);
        }

        /// <summary>The joystick (x right, y up, length 0..1) as a way on the sim's ground for this view.</summary>
        public SVector2 GroundWay(Vector2 stick) => GodotSpace.GroundWay(new SVector2(stick.x, -stick.y), Yaw);

        public void Apply(Camera camera, SVector2 feet)
        {
            var (eye, forward) = ShoulderRig.Pose(Rules, feet, Yaw, Pitch);
            camera.transform.SetPositionAndRotation(KalyeahResources.U(eye), Quaternion.LookRotation(KalyeahResources.U(forward), Vector3.up));
            camera.fieldOfView = Rules.ShoulderFov;
            camera.nearClipPlane = Rules.ShoulderNear;
            camera.farClipPlane = Far;
        }
    }
}
