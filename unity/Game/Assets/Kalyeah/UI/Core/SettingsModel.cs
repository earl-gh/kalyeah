using System;
using System.Collections.Generic;

namespace Kalyeah.UI
{
    /// <summary>
    /// The player's settings (port of scripts/autoload/settings.gd): the same fields, the same sections and keys in the saved file, the
    /// same clamping of a hand-edited file, and an event for each kind of change. The online server address is not a setting the
    /// player sees: it comes with the build (`ServerUrl`). Applying a setting to the engine (frame cap, Turbo, sound) is the Unity
    /// side's job (`MenuSettingsApplier`), on `Changed`.
    /// </summary>
    public sealed class SettingsModel
    {
        public enum Change { Accessibility, Controls, Language, Audio, Graphics, Practice, Vibration }

        public static readonly IReadOnlyDictionary<string, double> ControlSizes = new Dictionary<string, double>
        {
            { "small", 0.85 }, { "normal", 1.0 }, { "large", 1.2 },
        };
        /// <summary>Languages the game can show, the code to the language's own name (one is offered once its translation exists).</summary>
        public static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string> { { "en", "English" }, { "fil", "Filipino" } };
        public static readonly string[] CardTrays = { "bottom", "sides" };
        public static readonly int[] FrameRates = { 30, 60 };
        public static readonly string[] Performances = { "standard", "turbo" };
        /// <summary>Turbo draws the 3D street at this share of the screen's resolution.</summary>
        public const double TurboScale = 0.75;
        public const string FileName = "settings.json";

        /// <summary>Fired after a setter changed something (not on load).</summary>
        public event Action<Change> Changed;

        readonly ConfigStore _store;

        /// <summary>The online server (wss://...), stamped into the build; "" until a server is deployed ("Online is coming soon!").</summary>
        public string ServerUrl = "";
        public bool Vibration = true;
        public int FrameRate = 60;
        public string Performance = "standard";
        public bool ScreenShake = true;
        public bool ReducedMotion;
        public bool Colorblind;
        public bool LeftHanded;
        public string CardsTray = "bottom";
        public string Language = "en";
        public bool AimGuide = true;
        public bool TiltToRun;
        public bool Sounds = true;
        public bool Music = true;
        public double ControlScale = 1.0;
        /// <summary>The Practice difficulty chosen last ("" until one is chosen: the rules' default is used).</summary>
        public string BotLevel = "";
        /// <summary>The languages with a translation in this build ("en" always). Set by the Unity side when it finds more.</summary>
        public readonly List<string> AvailableLanguages = new List<string> { "en" };

        public SettingsModel(ConfigStore store)
        {
            _store = store ?? new ConfigStore(null);
            Load();
        }

        public void Load()
        {
            Vibration = _store.GetBool("input", "vibration", true);
            FrameRate = NearestFrameRate(_store.GetNumber("graphics", "frame_rate"));
            string mode = _store.GetString("graphics", "performance", "standard");
            Performance = Array.IndexOf(Performances, mode) >= 0 ? mode : "standard";
            ScreenShake = _store.GetBool("graphics", "screen_shake", true);
            ReducedMotion = _store.GetBool("accessibility", "reduced_motion", false);
            Colorblind = _store.GetBool("accessibility", "colorblind", false);
            LeftHanded = _store.GetBool("input", "left_handed", false);
            string tray = _store.GetString("input", "cards_tray", "bottom");
            CardsTray = Array.IndexOf(CardTrays, tray) >= 0 ? tray : "bottom";
            ControlScale = NearestControlScale(_store.GetNumber("input", "control_scale"));
            Language = _store.GetString("ui", "language", "en");
            AimGuide = _store.GetBool("input", "aim_guide", true);
            TiltToRun = _store.GetBool("input", "tilt_to_run", false);
            Sounds = _store.GetBool("audio", "sounds", true);
            Music = _store.GetBool("audio", "music", true);
            BotLevel = _store.GetString("practice", "bot_level", "");
            if (!AvailableLanguages.Contains(Language)) Language = "en";
        }

        // ---- the rules of settings.gd ----------------------------------------------------------------------------------------------

        /// <summary>A bare host becomes wss://host (ws:// for localhost and a private address).</summary>
        public static string NormalizeUrl(string raw)
        {
            string url = (raw ?? "").Trim();
            if (url.Length == 0 || url.StartsWith("ws://", StringComparison.Ordinal) || url.StartsWith("wss://", StringComparison.Ordinal)) return url;
            if (url.StartsWith("https://", StringComparison.Ordinal)) url = url.Substring(8);
            else if (url.StartsWith("http://", StringComparison.Ordinal)) url = url.Substring(7);
            if (url.StartsWith("localhost", StringComparison.Ordinal) || IsLocalAddress(url)) return "ws://" + url;
            return "wss://" + url;
        }

        /// <summary>True for a private network address (a phone hotspot or home Wi-Fi), which has no https.</summary>
        public static bool IsLocalAddress(string address)
        {
            if (address.StartsWith("127.", StringComparison.Ordinal) || address.StartsWith("10.", StringComparison.Ordinal)
                || address.StartsWith("192.168.", StringComparison.Ordinal)) return true;
            if (address.StartsWith("172.", StringComparison.Ordinal))
            {
                string[] parts = address.Split('.');
                return parts.Length > 1 && int.TryParse(parts[1], out int second) && second >= 16 && second <= 31;
            }
            return false;
        }

        /// <summary>The allowed control size closest to `value` (no number: the normal size).</summary>
        public static double NearestControlScale(double? value)
        {
            if (!value.HasValue) return 1.0;
            double best = 1.0;
            foreach (double option in ControlSizes.Values)
                if (Math.Abs(option - value.Value) < Math.Abs(best - value.Value)) best = option;
            return best;
        }

        /// <summary>The listed frame rate nearest to `value` (no number: 60).</summary>
        public static int NearestFrameRate(double? value)
        {
            if (!value.HasValue) return 60;
            int best = 60;
            double bestGap = double.PositiveInfinity;
            foreach (int rate in FrameRates)
            {
                double gap = Math.Abs(rate - value.Value);
                if (gap < bestGap)
                {
                    best = rate;
                    bestGap = gap;
                }
            }
            return best;
        }

        /// <summary>The share of the screen's resolution the 3D street is drawn at (Turbo: `TurboScale`).</summary>
        public double RenderScale => Performance == "turbo" ? TurboScale : 1.0;

        /// <summary>Screen shake is on only when it is switched on and Reduce motion is off (docs/GDD.md "Accessibility preferences").</summary>
        public bool ShakeAllowed => ScreenShake && !ReducedMotion;

        // ---- setters (each saves at once, like `_save_value`) -----------------------------------------------------------------------

        public void SetLanguage(string code)
        {
            if (code == Language || !AvailableLanguages.Contains(code)) return;
            Language = code;
            _store.Put("ui", "language", code);
            Changed?.Invoke(Change.Language);
        }

        public void SetLeftHanded(bool on)
        {
            if (on == LeftHanded) return;
            LeftHanded = on;
            _store.Put("input", "left_handed", on);
            Changed?.Invoke(Change.Controls);
        }

        public void SetCardsTray(string tray)
        {
            if (Array.IndexOf(CardTrays, tray) < 0 || tray == CardsTray) return;
            CardsTray = tray;
            _store.Put("input", "cards_tray", tray);
            Changed?.Invoke(Change.Controls);
        }

        public void SetTiltToRun(bool on)
        {
            if (on == TiltToRun) return;
            TiltToRun = on;
            _store.Put("input", "tilt_to_run", on);
            Changed?.Invoke(Change.Controls);
        }

        public void SetAimGuide(bool on)
        {
            if (on == AimGuide) return;
            AimGuide = on;
            _store.Put("input", "aim_guide", on);
            Changed?.Invoke(Change.Controls);
        }

        public void SetControlScale(double value)
        {
            double chosen = NearestControlScale(value);
            if (Math.Abs(chosen - ControlScale) < 1e-6) return;
            ControlScale = chosen;
            _store.Put("input", "control_scale", chosen);
            Changed?.Invoke(Change.Controls);
        }

        public void SetBotLevel(string id)
        {
            if (id == BotLevel) return;
            BotLevel = id ?? "";
            _store.Put("practice", "bot_level", BotLevel);
            Changed?.Invoke(Change.Practice);
        }

        public void SetVibration(bool on)
        {
            Vibration = on;
            _store.Put("input", "vibration", on);
            Changed?.Invoke(Change.Vibration);
        }

        public void SetColorblind(bool on)
        {
            if (on == Colorblind) return;
            Colorblind = on;
            _store.Put("accessibility", "colorblind", on);
            Changed?.Invoke(Change.Accessibility);
        }

        public void SetReducedMotion(bool on)
        {
            ReducedMotion = on;
            _store.Put("accessibility", "reduced_motion", on);
            Changed?.Invoke(Change.Accessibility);
        }

        public void SetFrameRate(int rate)
        {
            int chosen = NearestFrameRate(rate);
            if (chosen == FrameRate) return;
            FrameRate = chosen;
            _store.Put("graphics", "frame_rate", (long)chosen);
            Changed?.Invoke(Change.Graphics);
        }

        public void SetPerformance(string mode)
        {
            if (Array.IndexOf(Performances, mode) < 0 || mode == Performance) return;
            Performance = mode;
            _store.Put("graphics", "performance", mode);
            Changed?.Invoke(Change.Graphics);
        }

        public void SetScreenShake(bool on)
        {
            if (on == ScreenShake) return;
            ScreenShake = on;
            _store.Put("graphics", "screen_shake", on);
            Changed?.Invoke(Change.Graphics);
        }

        public void SetSounds(bool on)
        {
            if (on == Sounds) return;
            Sounds = on;
            _store.Put("audio", "sounds", on);
            Changed?.Invoke(Change.Audio);
        }

        public void SetMusic(bool on)
        {
            if (on == Music) return;
            Music = on;
            _store.Put("audio", "music", on);
            Changed?.Invoke(Change.Audio);
        }
    }
}
