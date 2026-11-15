using System.Collections.Generic;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// The third-person shoulder camera (docs/GDD.md "Shoulder view"; scripts/view/arena_camera.gd with CameraRules.third_person) on a Unity
    /// Camera. The state and the maths are ShoulderFollow's (engine-free): the look a drag turns, the auto-follow behind a running kid, the
    /// pivot spring, the crouch/jump posture, the FOV kick, the shake, the spring arm against walls and the arena's edge, the half turn when
    /// your side changes. This class only turns its pose into the camera's transform and lens, and reads the crosshair's ray.
    /// </summary>
    public sealed class ShoulderCamera
    {
        /// <summary>The far plane, metres (arena_camera.gd SHOULDER_FAR keeps the whole barangay and its skyline in view).</summary>
        const float Far = 400f;
        /// <summary>The UI's base height: drags are measured in these pixels, as in the Godot game's 1280 x 720 layout.</summary>
        const float BaseHeight = 720f;

        public readonly CameraRulesData Rules;
        public readonly ShoulderFollow Follow;
        IReadOnlyList<GroundRect> _obstacles = System.Array.Empty<GroundRect>();
        float _arenaRadius;

        public ShoulderCamera(CameraRulesData rules, bool flip)
        {
            Rules = rules;
            Follow = new ShoulderFollow(rules) { ReducedMotion = HudPrefs.ReducedMotion };
            Follow.Place(flip);
        }

        /// <summary>The walls the spring arm keeps off (the sim's obstacle rects) and the arena's radius.</summary>
        public void SetWalls(IReadOnlyList<GroundRect> obstacles, float arenaRadius)
        {
            _obstacles = obstacles ?? System.Array.Empty<GroundRect>();
            _arenaRadius = arenaRadius;
        }

        /// <summary>Degrees: 0 looks down the sim's -Z. Pitch is down-positive. (The screenshots set them directly.)</summary>
        public float Yaw { get => Follow.LookYaw; set => Follow.LookYaw = value; }
        public float Pitch { get => Follow.LookPitch; set => Follow.LookPitch = value; }

        /// <summary>A drag of `screenPixels` (Unity screen pixels, y up) turns the view: right turns right, up looks up.</summary>
        public void Look(Vector2 screenPixels, float screenHeight)
        {
            if (screenPixels == Vector2.zero) return;
            float toBase = BaseHeight / Mathf.Max(screenHeight, 1f);
            Follow.Look(new SVector2(screenPixels.x * toBase, -screenPixels.y * toBase));
        }

        /// <summary>The joystick (x right, y up, length 0..1) as a way on the sim's ground for this view.</summary>
        public SVector2 GroundWay(Vector2 stick) => Follow.GroundWay(new SVector2(stick.x, -stick.y));

        /// <summary>
        /// Once a frame: the pivot follows your kid's drawn feet (`snap` after a respawn), the springs and the shake run, and the camera is
        /// placed (the spring arm keeps it off walls).
        /// </summary>
        public void Apply(Camera camera, SVector2 feet, float dt, bool snap = false)
        {
            Follow.ReducedMotion = HudPrefs.ReducedMotion;
            Follow.Follow(feet, snap);
            Follow.Advance(dt);
            Place(camera);
        }

        /// <summary>The screenshots: straight onto `feet` with the current look, no smoothing.</summary>
        public void Apply(Camera camera, SVector2 feet)
        {
            Follow.Follow(feet, snap: true);
            Follow.Advance(0f);
            Place(camera);
        }

        void Place(Camera camera)
        {
            var (eye, pitch, yaw) = Follow.Pose(_obstacles, _arenaRadius);
            Vector3 at = KalyeahResources.U(GodotSpace.World(eye));
            var rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(pitch, yaw)), Vector3.up);
            float shake = HudPrefs.ScreenShake ? Follow.ShakeStrength : 0f;
            if (shake > 0f) at += rotation * new Vector3(Random.Range(-1f, 1f) * shake, Random.Range(-1f, 1f) * shake, 0f);
            camera.transform.SetPositionAndRotation(at, rotation);
            camera.fieldOfView = Follow.Fov;
            camera.nearClipPlane = Rules.ShoulderNear;
            camera.farClipPlane = Far;
        }

        /// <summary>The street point (sim XZ) under the middle of the screen (the crosshair), or null over the horizon.</summary>
        public static SVector2? CrosshairHit(Camera camera)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!new Plane(Vector3.up, 0f).Raycast(ray, out float distance) || distance <= 0f) return null;
            Vector3 hit = ray.GetPoint(distance);
            return GodotSpace.Ground(new SVector3(hit.x, hit.y, hit.z));
        }
    }
}
