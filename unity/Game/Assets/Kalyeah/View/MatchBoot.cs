using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Everything a match shows, built in code from the data (no hand-made scene): the match behind SimBridge, the street and kids
    /// (MatchView), the shoulder camera and the HUD. MatchBoot builds it in play; KalyeahBuild.Screenshots builds the same thing in the
    /// editor's edit mode and renders it.
    /// </summary>
    public sealed class MatchWorld
    {
        public readonly DataSource Data;
        public readonly SimBridge Sim;
        public readonly MatchView View;
        public readonly ShoulderCamera Shoulder;
        public readonly CameraRulesData CameraRules;
        public readonly Camera Camera;
        public readonly MatchHud Hud;
        readonly List<KidSnapshot> _previous = new List<KidSnapshot>();
        readonly List<KidSnapshot> _drawn = new List<KidSnapshot>();
        bool _placed;

        /// <summary>A Practice match against bots (the screenshots).</summary>
        public MatchWorld(Transform root, DataSource data, long seed, string localCharacter, bool interactive, bool hudCamera)
            : this(root, data, new SimBridge(data, seed, localCharacter), interactive, hudCamera) { }

        /// <param name="hudCamera">Render the HUD through the camera (true for the screenshots, which capture one camera).</param>
        public MatchWorld(Transform root, DataSource data, SimBridge sim, bool interactive, bool hudCamera)
        {
            Data = data;
            Sim = sim;
            CameraRules = CameraRulesData.Load(data);
            View = new MatchView(root, MapLayoutData.Load(data), CameraRules) { OwnTeam = sim.MyTeam };
            Shoulder = new ShoulderCamera(CameraRules, sim.Flip);
            Shoulder.SetWalls(sim.Obstacles, sim.ArenaRadius);
            sim.Shake += strength => { if (HudPrefs.ScreenShake) Shoulder.Follow.Shake(strength); };

            var cameraObject = new GameObject("ShoulderCamera", typeof(Camera));
            cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.Skybox;
            Hud = new MatchHud(root, sim, Shoulder.Follow, CameraRules, hudCamera ? Camera : null, interactive);
            Draw(1f, 0f, SVector2.Zero);
        }

        /// <summary>One sim tick (the previous tick is kept to blend between the two; online the mirror moves on its own).</summary>
        public void StepTick()
        {
            _previous.Clear();
            if (!Sim.IsOnline) _previous.AddRange(Sim.Kids);
            Sim.Step();
        }

        /// <summary>
        /// Draws the kids `alpha` of the way from the previous tick to the current one, drives the camera behind yours (`screenMove`: the
        /// joystick's push on screen, x right, y down), hides the buildings in its way, and refreshes the HUD.
        /// </summary>
        public void Draw(float alpha, float dt, SVector2 screenMove)
        {
            View.Sync(_previous.Count > 0 ? _previous : null, Sim.Kids, alpha, dt);
            _drawn.Clear();
            SVector2 feet = SVector2.Zero;
            bool haveMe = false;
            foreach (KidSnapshot kid in Sim.Kids)
            {
                KidSnapshot shown = kid;
                foreach (KidSnapshot before in _previous)
                    if (before.Id == kid.Id) { shown.Position = SVector2.Lerp(before.Position, kid.Position, Mathf.Clamp01(alpha)); break; }
                _drawn.Add(shown);
                if (kid.IsLocal) { feet = shown.Position; haveMe = true; }
            }
            Shoulder.Follow.Place(Sim.Flip);
            if (haveMe)
            {
                Sim.DriveCamera(Shoulder.Follow, screenMove, dt);
                Shoulder.Apply(Camera, feet, dt, snap: !_placed);
                _placed = true;
                View.HideBetween(Camera.transform.position, feet, KidView.KidHeight * CameraRules.KidViewScale, CameraRules.ShoulderClearReach);
            }
            Hud.Refresh(dt, Camera, _drawn);
        }

        /// <summary>The street point under the crosshair, for the next gestures (call once a frame before them).</summary>
        public void AimCrosshair() =>
            Sim.SetCrosshair(ShoulderCamera.CrosshairHit(Camera), Shoulder.Follow.Forward, CameraRules.ShoulderBoundsEdge, CameraRules.AimReach);
    }

    /// <summary>
    /// The Match scene's one component: the match MatchLaunch set up (a Practice match against bots when none was), stepped at the sim's
    /// fixed 30 Hz tick (never with the frame's delta). The joystick's push becomes PlayerInput.move through the camera's yaw; Android Back
    /// opens the match menu, which pauses a match offline. Leaving goes back to MatchLaunch.ReturnScene.
    /// </summary>
    public sealed class MatchBoot : MonoBehaviour
    {
        /// <summary>No more ticks than this in one frame after a hitch (practice_match.gd MAX_STEPS_PER_FRAME).</summary>
        const int MaxTicksPerFrame = 8;
        public const string SceneName = "Match";

        MatchWorld _world;
        float _carry;
        bool _focused = true, _reported, _left;

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
            DataSource data = KalyeahResources.Data();
            SimBridge sim = MatchLaunch.CreateBridge(data);
            _world = new MatchWorld(transform, data, sim, interactive: true, hudCamera: false);
            sim.MatchOver += result =>
            {
                if (_reported) return;
                _reported = true;
                MatchLaunch.OnFinished?.Invoke(result);
            };
            _world.Hud.LeavePressed += Leave;
            _world.Hud.HomePressed += Leave;
            _world.Hud.AgainPressed += () =>
            {
                if (sim.IsOnline) Leave();
                else SceneManager.LoadScene(SceneName); // the same options, a fresh seed
            };
        }

        void Update()
        {
            SimBridge sim = _world.Sim;
            MatchHud hud = _world.Hud;
            if (BackPressed()) hud.ToggleMenu();
            sim.Paused = !sim.IsOnline && hud.MenuOpen;
            bool controls = _focused && !hud.MenuOpen;
            if (!controls && sim.ControlsActive) hud.CancelControls();
            sim.ControlsActive = controls;

            float dt = Time.deltaTime;
            if (hud.Look != null) _world.Shoulder.Look(hud.Look.TakeDrag(), Screen.height);
            Vector2 stick = hud.Joystick != null && controls ? hud.Joystick.Value : Vector2.zero;
            var screenMove = new SVector2(stick.x, -stick.y);
            sim.SetStick(_world.Shoulder.GroundWay(stick));
            _world.AimCrosshair();
            if (sim.Phase == MatchPhaseView.Intro && TouchedThisFrame()) sim.SkipIntro();

            sim.Frame(dt);
            _carry += dt;
            float tick = sim.TickDt;
            int ticks = 0;
            while (_carry >= tick && ticks < MaxTicksPerFrame)
            {
                _world.StepTick();
                _carry -= tick;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) _carry = 0f;
            _world.Draw(sim.IsOnline ? 1f : _carry / tick, dt, screenMove);
        }

        void Leave()
        {
            if (_left) return;
            _left = true;
            bool abandoned = _world.Sim.Leave();
            if (!_reported) MatchLaunch.OnLeft?.Invoke(abandoned);
            MatchLaunch.Client = null;
            SceneManager.LoadScene(MatchLaunch.ReturnScene);
        }

        /// <summary>The app went to the background or lost focus: everything held is let go (a held aim cancels instead of casting).</summary>
        void OnApplicationFocus(bool focus) => _focused = focus;

        void OnApplicationPause(bool paused)
        {
            _focused = !paused;
            if (paused && _world != null)
            {
                _world.Hud.CancelControls();
                _world.Sim.ControlsActive = false;
            }
        }

        void OnDestroy()
        {
            // online the room slot goes back to the server however the scene ends (practice_match.gd _exit_tree)
            if (_world != null && _world.Sim.IsOnline && !_left) _world.Sim.Leave();
        }

        static bool TouchedThisFrame() => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        /// <summary>
        /// Android's Back button. The Input System reports it as the Escape key of the phone's keyboard device (there is no desktop
        /// path: Android is the only client).
        /// </summary>
        public static bool BackPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
