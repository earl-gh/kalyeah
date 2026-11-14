using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Kalyeah.Game
{
    /// <summary>
    /// Everything a match shows, built in code from the data (no hand-made scene): the sim behind SimBridge, the street and kids
    /// (MatchView), the shoulder camera and the HUD. MatchBoot builds it in play; KalyeahBuild.Screenshots builds the same thing in the
    /// editor's edit mode and renders it.
    /// </summary>
    public sealed class MatchWorld
    {
        public readonly DataSource Data;
        public readonly SimBridge Sim;
        public readonly MatchView View;
        public readonly ShoulderCamera Shoulder;
        public readonly Camera Camera;
        public readonly MatchHud Hud;
        readonly List<KidSnapshot> _previous = new List<KidSnapshot>();

        /// <param name="hudCamera">Render the HUD through the camera (true for the screenshots, which capture one camera).</param>
        public MatchWorld(Transform root, DataSource data, long seed, string localCharacter, bool interactive, bool hudCamera)
        {
            Data = data;
            Sim = new SimBridge(data, seed, localCharacter);
            CameraRulesData cameraRules = CameraRulesData.Load(data);
            View = new MatchView(root, MapLayoutData.Load(data), cameraRules);
            Shoulder = new ShoulderCamera(cameraRules, SimBridge.LocalTeam);

            var cameraObject = new GameObject("ShoulderCamera", typeof(Camera));
            cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.Skybox;
            Hud = new MatchHud(root, Sim, hudCamera ? Camera : null, interactive);
            Draw(1f, 0f);
        }

        /// <summary>One sim tick (the previous tick is kept to blend between the two).</summary>
        public void StepTick()
        {
            _previous.Clear();
            _previous.AddRange(Sim.Kids);
            Sim.Step();
        }

        /// <summary>Draws the kids `alpha` of the way from the previous tick to the current one, the camera on yours, the HUD.</summary>
        public void Draw(float alpha, float dt)
        {
            View.Sync(_previous.Count > 0 ? _previous : null, Sim.Kids, alpha, dt);
            KidSnapshot? me = Sim.Find(SimBridge.LocalId);
            if (me.HasValue)
            {
                System.Numerics.Vector2 feet = me.Value.Position;
                foreach (KidSnapshot before in _previous)
                    if (before.Id == SimBridge.LocalId) { feet = System.Numerics.Vector2.Lerp(before.Position, feet, Mathf.Clamp01(alpha)); break; }
                Shoulder.Apply(Camera, feet);
            }
            Hud.Refresh(Sim);
        }
    }

    /// <summary>
    /// The Match scene's one component: a 5v5 practice match against bots, stepped at the sim's fixed 30 Hz tick (never with the frame's
    /// delta), with the joystick's push turned into PlayerInput.move by the camera's yaw. Android Back goes back to the title.
    /// </summary>
    public sealed class MatchBoot : MonoBehaviour
    {
        /// <summary>No more ticks than this in one frame after a hitch (the sim slows down rather than spiralling).</summary>
        const int MaxTicksPerFrame = 5;
        public const string SceneName = "Match";

        [SerializeField] string localCharacter = "junjun";
        MatchWorld _world;
        float _carry;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;
            Ui.EnsureEventSystem();
            long seed = System.DateTime.UtcNow.Ticks;
            _world = new MatchWorld(transform, KalyeahResources.Data(), seed, localCharacter, interactive: true, hudCamera: false);
        }

        void Update()
        {
            if (BackPressed())
            {
                SceneManager.LoadScene(TitleScreen.SceneName);
                return;
            }
            MatchHud hud = _world.Hud;
            if (hud.Look != null) _world.Shoulder.Look(hud.Look.TakeDrag(), Screen.height);
            Vector2 stick = hud.Joystick != null ? hud.Joystick.Value : Vector2.zero;
            _world.Sim.SetLocalInput(_world.Shoulder.GroundWay(stick), 0);

            _carry += Time.deltaTime;
            float dt = _world.Sim.TickDt;
            int ticks = 0;
            while (_carry >= dt && ticks < MaxTicksPerFrame)
            {
                _world.StepTick();
                _carry -= dt;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) _carry = Mathf.Min(_carry, dt);
            _world.Draw(_carry / dt, Time.deltaTime);
        }

        /// <summary>
        /// Android's Back button. The Input System reports it as the Escape key of the phone's keyboard device (there is no desktop
        /// path: Android is the only client).
        /// </summary>
        public static bool BackPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
