using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kalyeah.Game
{
    /// <summary>
    /// Engine-free player settings and JSON persistence contract (port of scripts/autoload/settings.gd).
    /// View/HUD/Audio scripts read and mutate <see cref="Current"/> and persist <see cref="ToJson"/> via PlayerPrefs or file I/O.
    /// </summary>
    public sealed class SettingsStore
    {
        public const string PlayerPrefsKey = "kalyeah.settings.v1";

        public const float ScaleSmall = 0.85f;
        public const float ScaleNormal = 1.0f;
        public const float ScaleLarge = 1.2f;
        public const float TurboRenderScale = 0.75f;

        public static readonly float[] ControlScales = { ScaleSmall, ScaleNormal, ScaleLarge };
        public static readonly int[] SupportedFrameRates = { 30, 60 };

        public static SettingsStore Current { get; } = new SettingsStore();
        public static SettingsStore Instance => Current;

        public event Action Changed;

        bool _suppressNotify;

        float _masterVolume = 1f;
        float _musicVolume = 0.4f;
        float _sfxVolume = 0.8f;
        bool _soundsEnabled = true;
        bool _musicEnabled = true;

        bool _haptics = true;
        bool _screenShake = true;
        bool _reduceMotion;
        bool _leftHanded;
        bool _tiltToRun;
        bool _aimGuide = true;
        bool _cardsTrayMode;
        string _cardsTrayPlacement = "bottom";
        float _controlScale = ScaleNormal;

        int _colorblindMode;
        int _graphicsQuality = 1;
        int _targetFrameRate = 60;
        string _language = "en";
        string _botDifficulty = "medium";

        public float MasterVolume
        {
            get => _masterVolume;
            set => SetFloat(ref _masterVolume, Clamp01(value));
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set => SetFloat(ref _musicVolume, Clamp01(value));
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => SetFloat(ref _sfxVolume, Clamp01(value));
        }

        public bool SoundsEnabled
        {
            get => _soundsEnabled;
            set => SetBool(ref _soundsEnabled, value);
        }

        public bool MusicEnabled
        {
            get => _musicEnabled;
            set => SetBool(ref _musicEnabled, value);
        }

        public float EffectiveMusicVolume => _musicEnabled ? _masterVolume * _musicVolume : 0f;
        public float EffectiveSfxVolume => _soundsEnabled ? _masterVolume * _sfxVolume : 0f;

        public bool Haptics
        {
            get => _haptics;
            set => SetBool(ref _haptics, value);
        }

        public bool ScreenShake
        {
            get => _screenShake;
            set => SetBool(ref _screenShake, value);
        }

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => SetBool(ref _reduceMotion, value);
        }

        public bool LeftHanded
        {
            get => _leftHanded;
            set => SetBool(ref _leftHanded, value);
        }

        public bool TiltToRun
        {
            get => _tiltToRun;
            set => SetBool(ref _tiltToRun, value);
        }

        public bool AimGuide
        {
            get => _aimGuide;
            set => SetBool(ref _aimGuide, value);
        }

        /// <summary>False = radial superpower arc (default); True = 4-card cycle tray.</summary>
        public bool CardsTrayMode
        {
            get => _cardsTrayMode;
            set => SetBool(ref _cardsTrayMode, value);
        }

        /// <summary>Card tray placement when <see cref="CardsTrayMode"/> is active: "bottom" or "sides".</summary>
        public string CardsTrayPlacement
        {
            get => _cardsTrayPlacement;
            set => SetString(ref _cardsTrayPlacement, value == "sides" ? "sides" : "bottom");
        }

        public float ControlScale
        {
            get => _controlScale;
            set => SetFloat(ref _controlScale, SnapControlScale(value));
        }

        /// <summary>0 = None, 1 = Protanopia, 2 = Deuteranopia, 3 = Tritanopia.</summary>
        public int ColorblindMode
        {
            get => _colorblindMode;
            set => SetInt(ref _colorblindMode, Math.Clamp(value, 0, 3));
        }

        public bool IsColorblind => _colorblindMode != 0;

        /// <summary>0 = Low / Turbo (0.75x 3D scale, MSAA off), 1 = Medium / Standard, 2 = High.</summary>
        public int GraphicsQuality
        {
            get => _graphicsQuality;
            set => SetInt(ref _graphicsQuality, Math.Clamp(value, 0, 2));
        }

        public bool IsTurboGraphics => _graphicsQuality == 0;

        /// <summary>Mobile frame rate cap: 30 or 60 FPS.</summary>
        public int TargetFrameRate
        {
            get => _targetFrameRate;
            set => SetInt(ref _targetFrameRate, SnapFrameRate(value));
        }

        /// <summary>Locale code: "en" (English) or "fil" (Filipino).</summary>
        public string Language
        {
            get => _language;
            set => SetString(ref _language, value == "fil" ? "fil" : "en");
        }

        /// <summary>Last chosen Practice bot difficulty ("easy", "medium", "hard").</summary>
        public string BotDifficulty
        {
            get => _botDifficulty;
            set => SetString(ref _botDifficulty, NormalizeDifficulty(value));
        }

        public void NotifyChanged()
        {
            if (!_suppressNotify) Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            _suppressNotify = true;
            try
            {
                _masterVolume = 1f;
                _musicVolume = 0.4f;
                _sfxVolume = 0.8f;
                _soundsEnabled = true;
                _musicEnabled = true;
                _haptics = true;
                _screenShake = true;
                _reduceMotion = false;
                _leftHanded = false;
                _tiltToRun = false;
                _aimGuide = true;
                _cardsTrayMode = false;
                _cardsTrayPlacement = "bottom";
                _controlScale = ScaleNormal;
                _colorblindMode = 0;
                _graphicsQuality = 1;
                _targetFrameRate = 60;
                _language = "en";
                _botDifficulty = "medium";
            }
            finally
            {
                _suppressNotify = false;
            }
            NotifyChanged();
        }

        public string ToJson()
        {
            var sb = new StringBuilder(384);
            sb.Append('{');
            AppendFloat(sb, "master_volume", _masterVolume, true);
            AppendFloat(sb, "music_volume", _musicVolume, false);
            AppendFloat(sb, "sfx_volume", _sfxVolume, false);
            AppendBool(sb, "sounds_enabled", _soundsEnabled);
            AppendBool(sb, "music_enabled", _musicEnabled);
            AppendBool(sb, "haptics", _haptics);
            AppendBool(sb, "screen_shake", _screenShake);
            AppendBool(sb, "reduce_motion", _reduceMotion);
            AppendBool(sb, "left_handed", _leftHanded);
            AppendBool(sb, "tilt_to_run", _tiltToRun);
            AppendBool(sb, "aim_guide", _aimGuide);
            AppendBool(sb, "cards_tray_mode", _cardsTrayMode);
            AppendString(sb, "cards_tray_placement", _cardsTrayPlacement);
            AppendFloat(sb, "control_scale", _controlScale, false);
            AppendInt(sb, "colorblind_mode", _colorblindMode);
            AppendInt(sb, "graphics_quality", _graphicsQuality);
            AppendInt(sb, "target_frame_rate", _targetFrameRate);
            AppendString(sb, "language", _language);
            AppendString(sb, "bot_difficulty", _botDifficulty);
            sb.Append('}');
            return sb.ToString();
        }

        public bool LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            Dictionary<string, object> d;
            try
            {
                d = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch
            {
                return false;
            }
            if (d == null) return false;

            _suppressNotify = true;
            try
            {
                if (TryFloat(d, "master_volume", out float mv)) MasterVolume = mv;
                if (TryFloat(d, "music_volume", out float muv)) MusicVolume = muv;
                if (TryFloat(d, "sfx_volume", out float sv)) SfxVolume = sv;
                if (TryBool(d, "sounds_enabled", out bool se)) SoundsEnabled = se;
                if (TryBool(d, "music_enabled", out bool me)) MusicEnabled = me;
                if (TryBool(d, "haptics", out bool hap)) Haptics = hap;
                if (TryBool(d, "screen_shake", out bool ss)) ScreenShake = ss;
                if (TryBool(d, "reduce_motion", out bool rm)) ReduceMotion = rm;
                if (TryBool(d, "left_handed", out bool lh)) LeftHanded = lh;
                if (TryBool(d, "tilt_to_run", out bool tr)) TiltToRun = tr;
                if (TryBool(d, "aim_guide", out bool ag)) AimGuide = ag;
                if (TryBool(d, "cards_tray_mode", out bool ctm)) CardsTrayMode = ctm;
                if (TryString(d, "cards_tray_placement", out string ctp)) CardsTrayPlacement = ctp;
                if (TryFloat(d, "control_scale", out float cs)) ControlScale = cs;
                if (TryInt(d, "colorblind_mode", out int cb)) ColorblindMode = cb;
                if (TryInt(d, "graphics_quality", out int gq)) GraphicsQuality = gq;
                if (TryInt(d, "target_frame_rate", out int fps)) TargetFrameRate = fps;
                if (TryString(d, "language", out string lang)) Language = lang;
                if (TryString(d, "bot_difficulty", out string diff)) BotDifficulty = diff;
            }
            finally
            {
                _suppressNotify = false;
            }
            NotifyChanged();
            return true;
        }

        public static SettingsStore FromJson(string json)
        {
            var store = new SettingsStore();
            store.LoadJson(json);
            return store;
        }

        public static float SnapControlScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return ScaleNormal;
            float best = ScaleNormal;
            float bestGap = MathF.Abs(best - value);
            for (int i = 0; i < ControlScales.Length; i++)
            {
                float gap = MathF.Abs(ControlScales[i] - value);
                if (gap < bestGap)
                {
                    best = ControlScales[i];
                    bestGap = gap;
                }
            }
            return best;
        }

        public static int SnapFrameRate(int rate) => Math.Abs(rate - 30) < Math.Abs(rate - 60) ? 30 : 60;

        public static string NormalizeDifficulty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "medium";
            string lower = raw.Trim().ToLowerInvariant();
            if (lower == "easy" || lower == "hard") return lower;
            return "medium";
        }

        /// <summary>Port of settings.gd normalize_url: bare host becomes wss://host (or ws:// for localhost / private LAN).</summary>
        public static string NormalizeServerUrl(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string url = raw.Trim();
            if (url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return url;
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = url.Substring("https://".Length);
            else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = url.Substring("http://".Length);
            if (url.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || IsLocalAddress(url))
                return "ws://" + url;
            return "wss://" + url;
        }

        public static bool IsLocalAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            if (address.StartsWith("127.", StringComparison.Ordinal) ||
                address.StartsWith("10.", StringComparison.Ordinal) ||
                address.StartsWith("192.168.", StringComparison.Ordinal))
                return true;
            if (address.StartsWith("172.", StringComparison.Ordinal))
            {
                int dot1 = address.IndexOf('.');
                if (dot1 >= 0)
                {
                    int dot2 = address.IndexOf('.', dot1 + 1);
                    string seg = dot2 > dot1 ? address.Substring(dot1 + 1, dot2 - dot1 - 1) : address.Substring(dot1 + 1);
                    if (int.TryParse(seg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int second))
                        return second >= 16 && second <= 31;
                }
            }
            return false;
        }

        static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            return Math.Clamp(v, 0f, 1f);
        }

        void SetFloat(ref float field, float next)
        {
            if (MathF.Abs(field - next) <= 0.0001f) return;
            field = next;
            NotifyChanged();
        }

        void SetBool(ref bool field, bool next)
        {
            if (field == next) return;
            field = next;
            NotifyChanged();
        }

        void SetInt(ref int field, int next)
        {
            if (field == next) return;
            field = next;
            NotifyChanged();
        }

        void SetString(ref string field, string next)
        {
            string clean = next ?? "";
            if (string.Equals(field, clean, StringComparison.Ordinal)) return;
            field = clean;
            NotifyChanged();
        }

        static void AppendFloat(StringBuilder sb, string key, float val, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(val.ToString("0.####", CultureInfo.InvariantCulture));
        }

        static void AppendInt(StringBuilder sb, string key, int val)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(val.ToString(CultureInfo.InvariantCulture));
        }

        static void AppendBool(StringBuilder sb, string key, bool val)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(val ? "true" : "false");
        }

        static void AppendString(StringBuilder sb, string key, string val)
        {
            sb.Append(",\"").Append(key).Append("\":\"");
            EscapeJsonString(sb, val ?? "");
            sb.Append('"');
        }

        internal static void EscapeJsonString(StringBuilder sb, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
        }

        internal static bool TryFloat(Dictionary<string, object> d, string key, out float value)
        {
            if (d.TryGetValue(key, out object raw) && raw != null)
            {
                try { value = Convert.ToSingle(raw, CultureInfo.InvariantCulture); return true; }
                catch { }
            }
            value = 0f;
            return false;
        }

        internal static bool TryInt(Dictionary<string, object> d, string key, out int value)
        {
            if (d.TryGetValue(key, out object raw) && raw != null)
            {
                try { value = Convert.ToInt32(raw, CultureInfo.InvariantCulture); return true; }
                catch { }
            }
            value = 0;
            return false;
        }

        internal static bool TryLong(Dictionary<string, object> d, string key, out long value)
        {
            if (d.TryGetValue(key, out object raw) && raw != null)
            {
                try { value = Convert.ToInt64(raw, CultureInfo.InvariantCulture); return true; }
                catch { }
            }
            value = 0L;
            return false;
        }

        internal static bool TryBool(Dictionary<string, object> d, string key, out bool value)
        {
            if (d.TryGetValue(key, out object raw) && raw is bool b)
            {
                value = b;
                return true;
            }
            value = false;
            return false;
        }

        internal static bool TryString(Dictionary<string, object> d, string key, out string value)
        {
            if (d.TryGetValue(key, out object raw) && raw is string s)
            {
                value = s;
                return true;
            }
            value = null;
            return false;
        }
    }
}
