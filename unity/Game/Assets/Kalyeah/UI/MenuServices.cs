using System;
using System.IO;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kalyeah.UI
{
    /// <summary>
    /// What the Godot game keeps in its autoloads (Net, Session, Settings), made once and kept across scenes: the shipped data, the
    /// player's settings, session and history (JSON files in Application.persistentDataPath), and the NetHub polled every frame by a
    /// hidden object that survives scene loads (so a Local host keeps serving its friends during the match). The match screen reads
    /// them from here too (`MenuServices.Settings`, `.Session`, `.History`, `.Net`).
    /// </summary>
    public static class MenuServices
    {
        /// <summary>Optional TextAsset (Resources/kalyeah_server_url) with the online server's address; the build stamps it.</summary>
        public const string ServerUrlResource = "kalyeah_server_url";

        public static MenuData Data { get; private set; }
        public static SettingsModel Settings { get; private set; }
        public static SessionModel Session { get; private set; }
        public static MatchHistoryStore History { get; private set; }
        public static NetHub Net { get; private set; }
        /// <summary>The app was just opened (the boot loading screen shows once).</summary>
        public static bool FirstLaunch { get; private set; } = true;

        static readonly System.Random Dice = new System.Random();

        public static void Ensure()
        {
            if (Data != null) return;
            var sim = new SimData();
            foreach (TextAsset file in Resources.LoadAll<TextAsset>("Data")) sim.AddFile(file.text);
            Data = new MenuData(sim);
            string folder = Application.persistentDataPath;
            Settings = new SettingsModel(new ConfigStore(Path.Combine(folder, SettingsModel.FileName)));
            TextAsset url = Resources.Load<TextAsset>(ServerUrlResource);
            if (url != null) Settings.ServerUrl = url.text.Trim();
            Session = new SessionModel(new ConfigStore(Path.Combine(folder, SessionModel.FileName)));
            Session.EnsureProfile(n => Dice.Next(n));
            History = new MatchHistoryStore(new ConfigStore(Path.Combine(folder, MatchHistoryStore.FileName)));
            Net = new NetHub(Data);
            var runner = new GameObject("KalyeahServices", typeof(MenuServicesRunner));
            UnityEngine.Object.DontDestroyOnLoad(runner);
            Settings.Changed += _ => Apply();
            Apply();
        }

        public static void MarkLaunched() => FirstLaunch = false;

        public static int Pick(int count) => Dice.Next(Math.Max(1, count));

        public static long Seed() => DateTime.UtcNow.Ticks;

        /// <summary>The engine side of the settings: the frame cap, Turbo's render scale and anti-aliasing, and sound on or off.</summary>
        public static void Apply()
        {
            if (Settings == null) return;
            Application.targetFrameRate = Settings.FrameRate;
            QualitySettings.vSyncCount = 0;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (_msaa < 0) _msaa = urp.msaaSampleCount;
                bool turbo = Settings.Performance == "turbo";
                urp.renderScale = (float)Settings.RenderScale;
                urp.msaaSampleCount = turbo ? 1 : _msaa;
            }
            // one listener, no mixer yet: both switched off is silence; the match's music and effects read Sounds and Music themselves
            AudioListener.volume = Settings.Sounds || Settings.Music ? 1f : 0f;
        }

        static int _msaa = -1;

        /// <summary>A short buzz on a button, when Vibration is on (Android only: 12 ms, like the Godot game).</summary>
        public static void Buzz()
        {
            if (Settings == null || !Settings.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null) vibrator.Call("vibrate", 12L);
                }
            }
            catch (Exception)
            {
                // a phone without a motor (or a refused permission): no buzz
            }
#endif
        }

        /// <summary>Opens the phone's Wi-Fi settings (the Local modals' Network settings button). False where it cannot.</summary>
        public static bool OpenNetworkSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.WIFI_SETTINGS"))
                {
                    activity.Call("startActivity", intent);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

        public static bool CanOpenNetworkSettings =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif
    }

    /// <summary>Polls the network every frame, whatever scene is up (net.gd's _process).</summary>
    public sealed class MenuServicesRunner : MonoBehaviour
    {
        void Update()
        {
            MenuServices.Net?.Poll(Time.unscaledDeltaTime);
        }

        void OnApplicationQuit()
        {
            MenuServices.Net?.Client.Close();
            MenuServices.Net?.StopHost();
        }
    }
}
