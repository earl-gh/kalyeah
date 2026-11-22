using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Kalyeah.Game
{
    /// <summary>
    /// PlayMode update driver attached to <see cref="AudioHub.Root"/> so combat ducking ramps smoothly with zero GC
    /// and without requiring external callers to pump <see cref="AudioHub.Tick"/>.
    /// </summary>
    internal sealed class AudioHubDriver : MonoBehaviour
    {
        AudioHub _hub;

        internal void Bind(AudioHub hub) => _hub = hub;

        void Update()
        {
            if (_hub != null)
                _hub.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            _hub = null;
        }
    }

    /// <summary>
    /// Zero-GC match and menu audio hub: looped BGM ("audio/bgm"), procedural Philippine Barangay street ambience
    /// ("amb_barangay_day"), a dedicated 2D priority confirm/UI/banter pool (never starved by distant bots), a 3D
    /// spatial combat pool (logarithmic 4-38 m rolloff), software bus gains with -4 dB / 0.6 s combat ducking, and
    /// optional <see cref="AudioMixer"/> group routing compatible with headless EditMode.
    /// View-only: reads sim/UI events and settings, never alters simulation state or RNG.
    /// </summary>
    public sealed class AudioHub : IDisposable
    {
        public enum AudioBus
        {
            Bgm = 0,
            Ambience = 1,
            SfxCombat3D = 2,
            SfxConfirm2D = 3,
            Ui = 4,
            Banter = 5,
        }

        public const int Priority2DPoolSize = 6;
        public const int World3DPoolSize = 10;
        public const int SfxPoolSize = Priority2DPoolSize + World3DPoolSize;
        public const float MinPitchJitter = 0.95f;
        public const float MaxPitchJitter = 1.05f;
        public const float DefaultWorldPitchJitter = 0.05f;
        public const float DefaultBgmRulesShare = 0.4f;
        public const float DefaultSfxRulesShare = 0.8f;
        public const float EmptyCanPitch = 1.5f;

        public const float WorldSfxMinDistance = 4f;
        public const float WorldSfxMaxDistance = 38f;

        public const float DuckAttenuationDb = -4f;
        public const float DefaultDuckDurationSeconds = 0.60f;
        public const float DuckAttackSeconds = 0.045f;
        public const float DuckReleaseSeconds = 0.22f;
        public const float AmbienceMixDb = -22f;

        public static readonly float DuckTargetGain = Mathf.Pow(10f, DuckAttenuationDb / 20f);
        public static readonly float AmbienceBaseGain = Mathf.Pow(10f, AmbienceMixDb / 20f);

        public const string AmbienceDayCueId = "amb_barangay_day";
        const string BgmPrefix = "audio/";
        const string SfxPrefix = "audio/sfx/";
        const string DefaultMixerResource = "audio/KalyeahMixer";
        const int SynthSampleRate = 22050;
        const int BusCount = 6;

        // Exact 32 clips in assets/audio/sfx/*.mp3 synced by tools/unity_export/sync_assets.py.
        static readonly string[] KnownSfxIds =
        {
            "bato_heavy_boom",
            "bato_meteor_fall",
            "bola_bounce",
            "bola_throw",
            "card_cancel",
            "card_pick",
            "card_ready",
            "fall_thud",
            "gunting_big_charge",
            "gunting_big_snip",
            "gunting_light",
            "heal",
            "jacks_scatter",
            "lata_hit",
            "lata_poof",
            "lata_toss",
            "laugh_at_fall_thud",
            "paper_block",
            "paper_shield",
            "slowed_sting",
            "soil_block",
            "soil_expand",
            "soil_rise",
            "stunned",
            "trap_place",
            "trap_spring",
            "trumpo",
            "tsinelas_hit",
            "tsinelas_hit_heavy",
            "tsinelas_throw_heavy",
            "tsinelas_throw_light",
            "victory",
        };

        static readonly Dictionary<string, AudioClip> BgmCache = new Dictionary<string, AudioClip>(4, StringComparer.Ordinal);
        static readonly HashSet<string> MissingBgm = new HashSet<string>(StringComparer.Ordinal);
        static readonly Dictionary<string, AudioClip> SfxCache = new Dictionary<string, AudioClip>(40, StringComparer.Ordinal);
        static readonly HashSet<string> MissingSfx = new HashSet<string>(StringComparer.Ordinal);
        static readonly Dictionary<string, AudioClip> SynthCache = new Dictionary<string, AudioClip>(20, StringComparer.Ordinal);
        static bool _defaultMixerChecked;
        static AudioMixer _defaultMixer;

        static readonly string[] SynthCueIds =
        {
            "body_hit",
            "headshot_ding",
            "taya_confirm",
            "reload_clack",
            "water_empty",
            "trap_snap",
            "wall_break",
            "reveal_chime",
            AmbienceDayCueId,
            "footstep_concrete",
            "footstep_water",
            "slide_skid",
            "land_thud",
            "multitag_fanfare",
            "ui_hover_tick",
            "ui_play_confirm",
        };

        public static IReadOnlyList<string> AllSynthCueIds => SynthCueIds;

        public GameObject Root { get; private set; }
        public AudioSource BgmSource => _bgmSource;
        public AudioSource AmbienceSource => _ambienceSource;
        public AudioMixer ActiveMixer => _mixer;
        public int Priority2DPoolCount => _sfx2DSources.Length;
        public int World3DPoolCount => _sfx3DSources.Length;
        public int PooledSfxCount => _sfx2DSources.Length + _sfx3DSources.Length;

        public float MasterVolume { get; private set; } = 1f;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float RulesBgmShare { get; private set; }
        public float RulesSfxShare { get; private set; }
        public bool MusicEnabled { get; private set; } = true;
        public bool SfxEnabled { get; private set; } = true;

        public float EffectiveBgmVolume => MusicEnabled ? Mathf.Clamp01(MasterVolume * BgmVolume * RulesBgmShare) : 0f;
        public float AppliedBgmVolume => Mathf.Clamp01(EffectiveBgmVolume * _busGains[(int)AudioBus.Bgm] * _duckGain);
        public float EffectiveSfxVolume => SfxEnabled ? Mathf.Clamp01(MasterVolume * SfxVolume * RulesSfxShare) : 0f;
        public float EffectiveAmbienceVolume => (MusicEnabled && SfxEnabled)
            ? Mathf.Clamp01(MasterVolume * SfxVolume * AmbienceBaseGain)
            : 0f;
        public float AppliedAmbienceVolume => Mathf.Clamp01(EffectiveAmbienceVolume * _busGains[(int)AudioBus.Ambience] * _duckGain);

        public float DuckGain => _duckGain;
        public float DuckHoldRemaining => _duckHoldRemaining;
        public bool IsDucking => _duckHoldRemaining > 0f || _duckGain < 0.999f;
        public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;
        public bool IsAmbiencePlaying => _ambienceSource != null && _ambienceSource.isPlaying;

        readonly AudioSource _bgmSource;
        readonly AudioSource _ambienceSource;
        readonly AudioSource[] _sfx2DSources = new AudioSource[Priority2DPoolSize];
        readonly AudioSource[] _sfx3DSources = new AudioSource[World3DPoolSize];
        readonly float[] _busGains = new float[BusCount];

        AudioMixer _mixer;
        AudioMixerGroup _confirmGroup;
        AudioMixerGroup _uiGroup;
        AudioMixerGroup _banterGroup;
        int _next2DVoice;
        int _next3DVoice;
        uint _rngState;
        float _duckGain = 1f;
        float _duckTargetGain = 1f;
        float _duckHoldRemaining;
        bool _ambienceDesired;

        public AudioHub(
            Transform parent = null,
            float rulesBgmShare = DefaultBgmRulesShare,
            float rulesSfxShare = DefaultSfxRulesShare,
            bool prewarm = true)
        {
            RulesBgmShare = Mathf.Clamp01(rulesBgmShare);
            RulesSfxShare = Mathf.Clamp01(rulesSfxShare);
            for (int i = 0; i < BusCount; i++) _busGains[i] = 1f;

            _rngState = (uint)Environment.TickCount ^ 0x9E3779B9u;
            if (_rngState == 0u) _rngState = 0x6D2B79F5u;

            Root = new GameObject("AudioHub");
            if (parent != null) Root.transform.SetParent(parent, false);
            AudioHubDriver driver = Root.AddComponent<AudioHubDriver>();
            driver.Bind(this);

            var bgmObject = new GameObject("Bgm");
            bgmObject.transform.SetParent(Root.transform, false);
            _bgmSource = bgmObject.AddComponent<AudioSource>();
            Configure2DSource(_bgmSource, loop: true, priority: 64);
            _bgmSource.volume = AppliedBgmVolume;

            var ambObject = new GameObject("Ambience");
            ambObject.transform.SetParent(Root.transform, false);
            _ambienceSource = ambObject.AddComponent<AudioSource>();
            Configure2DSource(_ambienceSource, loop: true, priority: 80);
            _ambienceSource.volume = AppliedAmbienceVolume;

            for (int i = 0; i < Priority2DPoolSize; i++)
            {
                var voiceObject = new GameObject("Sfx2DVoice" + i);
                voiceObject.transform.SetParent(Root.transform, false);
                AudioSource voice = voiceObject.AddComponent<AudioSource>();
                Configure2DSource(voice, loop: false, priority: 96);
                voice.volume = EffectiveSfxVolume;
                _sfx2DSources[i] = voice;
            }

            for (int i = 0; i < World3DPoolSize; i++)
            {
                var voiceObject = new GameObject("Sfx3DVoice" + i);
                voiceObject.transform.SetParent(Root.transform, false);
                AudioSource voice = voiceObject.AddComponent<AudioSource>();
                Configure3DSource(voice, priority: 160);
                voice.volume = EffectiveSfxVolume;
                _sfx3DSources[i] = voice;
            }

            if (!_defaultMixerChecked)
            {
                _defaultMixerChecked = true;
                _defaultMixer = Resources.Load<AudioMixer>(DefaultMixerResource);
            }
            if (_defaultMixer != null)
                BindMixer(_defaultMixer);

            if (prewarm) Prewarm();
        }

        static void Configure2DSource(AudioSource source, bool loop, int priority)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
        }

        static void Configure3DSource(AudioSource source, int priority)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = WorldSfxMinDistance;
            source.maxDistance = WorldSfxMaxDistance;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.priority = priority;
        }

        public AudioSource GetPriority2DVoice(int index)
        {
            return index >= 0 && index < _sfx2DSources.Length ? _sfx2DSources[index] : null;
        }

        public AudioSource GetWorld3DVoice(int index)
        {
            return index >= 0 && index < _sfx3DSources.Length ? _sfx3DSources[index] : null;
        }

        /// <summary>
        /// Routes all pooled sources into matching groups of <paramref name="mixer"/> using Unity's public
        /// <see cref="AudioMixer.FindMatchingGroups"/> API. Passing <c>null</c> cleanly returns to software bus gain mode.
        /// </summary>
        public void BindMixer(AudioMixer mixer)
        {
            _mixer = mixer;
            if (mixer == null)
            {
                _confirmGroup = null;
                _uiGroup = null;
                _banterGroup = null;
                if (_bgmSource != null) _bgmSource.outputAudioMixerGroup = null;
                if (_ambienceSource != null) _ambienceSource.outputAudioMixerGroup = null;
                for (int i = 0; i < _sfx2DSources.Length; i++)
                {
                    if (_sfx2DSources[i] != null) _sfx2DSources[i].outputAudioMixerGroup = null;
                }
                for (int i = 0; i < _sfx3DSources.Length; i++)
                {
                    if (_sfx3DSources[i] != null) _sfx3DSources[i].outputAudioMixerGroup = null;
                }
                return;
            }

            AudioMixerGroup bgmGroup = FindMixerGroup(mixer, "BGM", "Music");
            AudioMixerGroup ambGroup = FindMixerGroup(mixer, "Ambience", "Ambient");
            AudioMixerGroup combat3DGroup = FindMixerGroup(mixer, "SFX_Combat_3D", "SFX");
            _confirmGroup = FindMixerGroup(mixer, "SFX_Confirm_2D", "SFX");
            _uiGroup = FindMixerGroup(mixer, "UI", "SFX_Confirm_2D", "SFX");
            _banterGroup = FindMixerGroup(mixer, "Banter", "SFX_Confirm_2D", "SFX");

            if (_bgmSource != null) _bgmSource.outputAudioMixerGroup = bgmGroup;
            if (_ambienceSource != null) _ambienceSource.outputAudioMixerGroup = ambGroup;
            for (int i = 0; i < _sfx2DSources.Length; i++)
            {
                if (_sfx2DSources[i] != null) _sfx2DSources[i].outputAudioMixerGroup = _confirmGroup;
            }
            for (int i = 0; i < _sfx3DSources.Length; i++)
            {
                if (_sfx3DSources[i] != null) _sfx3DSources[i].outputAudioMixerGroup = combat3DGroup;
            }

            ApplyVolumes();
        }

        static AudioMixerGroup FindMixerGroup(AudioMixer mixer, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                AudioMixerGroup[] found = mixer.FindMatchingGroups(candidates[i]);
                if (found != null && found.Length > 0 && found[0] != null)
                    return found[0];
            }
            return null;
        }

        /// <summary>Sets a software bus gain multiplier (0..1) without mutating user volume preferences.</summary>
        public void SetBusGain(AudioBus bus, float gain01)
        {
            int idx = (int)bus;
            if (idx < 0 || idx >= BusCount) return;
            _busGains[idx] = Mathf.Clamp01(gain01);
            ApplyVolumes();
        }

        public float GetBusGain(AudioBus bus)
        {
            int idx = (int)bus;
            return idx >= 0 && idx < BusCount ? _busGains[idx] : 1f;
        }

        /// <summary>Loads and caches bgm.mp3, all 32 sfx/*.mp3 clips, and synthesized ambience/foley/feedback cues upfront so mid-round playback allocates 0 B.</summary>
        public void Prewarm()
        {
            LoadBgmClip("bgm");
            for (int i = 0; i < KnownSfxIds.Length; i++) LoadSfxClip(KnownSfxIds[i]);
            for (int i = 0; i < SynthCueIds.Length; i++) GetOrBuildSynthClip(SynthCueIds[i]);
        }

        /// <summary>Reads bgm_volume and sfx_level from rules__game_rules when DataSource is available.</summary>
        public void BindRules(DataSource data)
        {
            if (data == null) return;
            Dictionary<string, object> rules = data.Props("rules__game_rules");
            float bgm = rules.ContainsKey("bgm_volume") ? DataSource.F(rules, "bgm_volume") : DefaultBgmRulesShare;
            float sfx = rules.ContainsKey("sfx_level") ? DataSource.F(rules, "sfx_level") : DefaultSfxRulesShare;
            SetRulesShares(bgm, sfx);
        }

        /// <summary>Updates user volume sliders and Music/Sounds mute toggles immediately across active voices.</summary>
        public void BindVolumes(
            float bgmVolume,
            float sfxVolume,
            bool musicEnabled = true,
            bool sfxEnabled = true,
            float masterVolume = 1f)
        {
            MasterVolume = Mathf.Clamp01(masterVolume);
            BgmVolume = Mathf.Clamp01(bgmVolume);
            SfxVolume = Mathf.Clamp01(sfxVolume);
            MusicEnabled = musicEnabled;
            SfxEnabled = sfxEnabled;
            ApplyVolumes();
        }

        public void SetRulesShares(float bgmShare, float sfxShare)
        {
            RulesBgmShare = Mathf.Clamp01(bgmShare);
            RulesSfxShare = Mathf.Clamp01(sfxShare);
            ApplyVolumes();
        }

        public void SetMasterVolume(float volume01)
        {
            MasterVolume = Mathf.Clamp01(volume01);
            ApplyVolumes();
        }

        public void SetBgmVolume(float volume01)
        {
            BgmVolume = Mathf.Clamp01(volume01);
            ApplyBgmVolume();
        }

        public void SetSfxVolume(float volume01)
        {
            SfxVolume = Mathf.Clamp01(volume01);
            ApplySfxVolumes();
        }

        public void SetMusicEnabled(bool enabled)
        {
            if (MusicEnabled == enabled) return;
            MusicEnabled = enabled;
            ApplyBgmVolume();
        }

        public void SetSfxEnabled(bool enabled)
        {
            if (SfxEnabled == enabled) return;
            SfxEnabled = enabled;
            ApplySfxVolumes();
        }

        /// <summary>
        /// Triggers smooth software combat ducking on BGM and Ambience (default -4 dB for 0.6 s) without mutating
        /// user volume settings or AudioMixer slider parameters.
        /// </summary>
        public void TriggerDuck(float durationSeconds = DefaultDuckDurationSeconds, float attenuationDb = DuckAttenuationDb)
        {
            float clampedDb = Mathf.Min(0f, attenuationDb);
            float target = Mathf.Clamp(Mathf.Pow(10f, clampedDb / 20f), 0.1f, 1f);
            _duckTargetGain = Mathf.Min(_duckTargetGain, target);
            _duckHoldRemaining = Mathf.Max(_duckHoldRemaining, Mathf.Max(0f, durationSeconds));
        }

        /// <summary>Advances smooth BGM and Ambience ducking attack/hold/release envelopes. Zero-GC when idle.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (_duckHoldRemaining <= 0f && _duckGain >= 0.9999f)
                return;

            float remainingDt = deltaTime;
            if (_duckHoldRemaining > 0f)
            {
                float holdStep = Mathf.Min(_duckHoldRemaining, remainingDt);
                _duckHoldRemaining -= holdStep;
                remainingDt -= holdStep;

                float span = Mathf.Max(0.05f, 1f - _duckTargetGain);
                float step = span * (holdStep / DuckAttackSeconds);
                _duckGain = Mathf.MoveTowards(_duckGain, _duckTargetGain, Mathf.Max(0.001f, step));
            }

            if (_duckHoldRemaining <= 0f && remainingDt > 0f && _duckGain < 1f)
            {
                float span = Mathf.Max(0.05f, 1f - _duckTargetGain);
                float step = span * (remainingDt / DuckReleaseSeconds);
                _duckGain = Mathf.MoveTowards(_duckGain, 1f, Mathf.Max(0.001f, step));
                if (_duckGain >= 0.9999f)
                {
                    _duckGain = 1f;
                    _duckTargetGain = 1f;
                }
            }

            ApplyBgmVolume();
        }

        /// <summary>Starts or resumes looped match BGM ("audio/bgm" by default); silent no-op if clip is absent.</summary>
        public bool PlayBgm(string bgmId = "bgm", bool restart = false)
        {
            if (_bgmSource == null || string.IsNullOrEmpty(bgmId)) return false;
            AudioClip clip = LoadBgmClip(bgmId);
            if (clip == null) return false;

            bool changed = _bgmSource.clip != clip;
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.pitch = 1f;
            _bgmSource.volume = AppliedBgmVolume;
            _bgmSource.mute = !MusicEnabled;

            if (restart || changed || !_bgmSource.isPlaying) _bgmSource.Play();

            if (Application.isPlaying)
                PlayAmbience(AmbienceDayCueId);

            return true;
        }

        public void StopBgm()
        {
            if (_bgmSource != null && _bgmSource.isPlaying) _bgmSource.Stop();
            StopAmbience();
        }

        /// <summary>Starts the procedural Philippine Barangay ambient loop (-22 dB, PlayMode only, auto-muted when Sounds or Music are off).</summary>
        public bool PlayAmbience(string ambienceId = AmbienceDayCueId, bool restart = false)
        {
            if (!Application.isPlaying || _ambienceSource == null || string.IsNullOrEmpty(ambienceId))
                return false;

            AudioClip clip = GetOrBuildSynthClip(ambienceId);
            if (clip == null) return false;

            _ambienceDesired = true;
            bool changed = _ambienceSource.clip != clip;
            _ambienceSource.clip = clip;
            _ambienceSource.loop = true;
            _ambienceSource.pitch = 1f;
            ApplyAmbienceVolume();

            if (MusicEnabled && SfxEnabled && (restart || changed || !_ambienceSource.isPlaying))
                _ambienceSource.Play();
            return true;
        }

        public void StopAmbience()
        {
            _ambienceDesired = false;
            if (_ambienceSource != null && _ambienceSource.isPlaying)
                _ambienceSource.Stop();
        }

        /// <summary>Plays a spatialized 3D combat/world SFX or procedural foley clip at <paramref name="worldPos"/> on the dedicated 3D pool.</summary>
        public bool PlayWorldSfx(string clipId, Vector3 worldPos, float volume = 1f, float pitchJitter = DefaultWorldPitchJitter)
        {
            if (!SfxEnabled || string.IsNullOrEmpty(clipId)) return false;
            AudioClip clip = ResolveAnyClip(clipId);
            if (clip == null) return false;
            return PlayWorldClip(clip, clipId, worldPos, volume, pitchScale: 1f, pitchJitter: pitchJitter);
        }

        /// <summary>Plays a weapon's cast SFX spatialized at <paramref name="worldPos"/> on the 3D combat pool.</summary>
        public bool PlayWorldWeaponCast(string weaponId, Vector3 worldPos, float volume = 1f)
        {
            string clipId = ResolveWeaponCastClip(weaponId, out float pitchScale);
            if (string.IsNullOrEmpty(clipId)) return false;
            AudioClip clip = LoadSfxClip(clipId);
            if (clip == null) return false;
            return PlayWorldClip(clip, clipId, worldPos, volume, pitchScale, DefaultWorldPitchJitter);
        }

        /// <summary>Plays a weapon's deferred impact SFX spatialized at <paramref name="worldPos"/> on the 3D combat pool.</summary>
        public bool PlayWorldWeaponImpact(string weaponId, Vector3 worldPos, float volume = 1f)
        {
            string clipId = ResolveWeaponImpactClip(weaponId);
            if (string.IsNullOrEmpty(clipId)) return false;
            AudioClip clip = LoadSfxClip(clipId);
            if (clip == null) return false;
            return PlayWorldClip(clip, clipId, worldPos, volume, pitchScale: 1f, DefaultWorldPitchJitter);
        }

        /// <summary>Plays concrete or estero puddle footstep foley spatialized at <paramref name="worldPos"/>.</summary>
        public bool PlayFootstep(bool inWater, Vector3 worldPos, float volume = 0.72f)
        {
            return PlayWorldSfx(inWater ? "footstep_water" : "footstep_concrete", worldPos, volume, pitchJitter: 0.08f);
        }

        /// <summary>Plays rubber-on-asphalt tactical slide skid foley spatialized at <paramref name="worldPos"/>.</summary>
        public bool PlaySlideSkid(Vector3 worldPos, float volume = 0.78f)
        {
            return PlayWorldSfx("slide_skid", worldPos, volume, pitchJitter: 0.06f);
        }

        /// <summary>Plays grounded jump-landing impact foley spatialized at <paramref name="worldPos"/>.</summary>
        public bool PlayLandThud(Vector3 worldPos, float volume = 0.85f)
        {
            return PlayWorldSfx("land_thud", worldPos, volume, pitchJitter: 0.06f);
        }

        /// <summary>Plays a weapon's cast SFX using the owner's sound-audit mapping and speed adjustments.</summary>
        public bool PlayWeaponCast(string weaponId)
        {
            string clipId = ResolveWeaponCastClip(weaponId, out float pitchScale);
            return PlaySfx(clipId, pitchScale);
        }

        /// <summary>Plays deferred visual-sync impact/snip/wall SFX (meteor boom, can poof, scissors snip, wall block).</summary>
        public bool PlayWeaponImpact(string weaponId)
        {
            string clipId = ResolveWeaponImpactClip(weaponId);
            return !string.IsNullOrEmpty(clipId) && PlaySfx(clipId);
        }

        static string ResolveWeaponImpactClip(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            switch (weaponId)
            {
                case "bato_heavy":
                case "ult_bagsak":
                    return "bato_heavy_boom";
                case "lata":
                    return "lata_poof";
                case "bola":
                    return "bola_bounce";
                case "gunting_heavy":
                    return "gunting_big_snip";
                case "gunting_light":
                    return "gunting_light";
                case "papel_shield":
                    return "paper_block";
                case "papel_trap":
                    return "trap_spring";
                case "langit_lupa":
                    return "soil_block";
                default:
                    return null;
            }
        }

        /// <summary>Plays hit or tag confirmation SFX (never plays laugh_at_fall_thud, which is strictly for random trip).</summary>
        public bool PlayHit(bool isTag) => PlayHit(isTag, null);

        public bool PlayHit(bool isTag, string weaponId)
        {
            if (SfxEnabled && isTag) TriggerDuck();
            string clipId = ResolveHitClip(isTag, weaponId, out float pitchScale);
            return PlaySfx(clipId, pitchScale);
        }

        /// <summary>Plays distinct tactile Valorant/CODM synthesized hit confirmation on the unstarvable 2D priority pool and ducks BGM on headshots/tags.</summary>
        public bool PlayHitConfirm(bool isHeadshot, bool isTag)
        {
            if (SfxEnabled && (isHeadshot || isTag))
                TriggerDuck();

            if (isTag)
            {
                bool ding = isHeadshot && PlaySynthCue("headshot_ding", pitchScale: 1.04f, volumeScale: 0.78f, applyJitter: false);
                bool taya = PlaySynthCue("taya_confirm", pitchScale: 1f, volumeScale: 1f, applyJitter: false);
                return taya || ding;
            }
            if (isHeadshot)
                return PlaySynthCue("headshot_ding", pitchScale: 1f, volumeScale: 0.96f, applyJitter: true);
            return PlaySynthCue("body_hit", pitchScale: 1f, volumeScale: 0.84f, applyJitter: true);
        }

        /// <summary>Plays multi-tag streak fanfare on the 2D priority pool and ducks BGM/Ambience cleanly.</summary>
        public bool PlayMultiTagFanfare(int streakCount = 2)
        {
            if (SfxEnabled) TriggerDuck();
            float lift = 1f + 0.04f * Mathf.Clamp(streakCount - 2, 0, 4);
            return PlaySynthCue("multitag_fanfare", pitchScale: lift, volumeScale: 1f, applyJitter: false);
        }

        /// <summary>Ducks BGM/Ambience for Taglish banter callouts and optionally plays a 2D banter/ping cue.</summary>
        public bool PlayBanter(string clipId = null, float volumeScale = 1f)
        {
            TriggerDuck();
            if (!SfxEnabled) return false;
            if (string.IsNullOrEmpty(clipId)) return true;

            AudioClip clip = ResolveAnyClip(clipId);
            if (clip == null) return true;
            return Play2DClip(clip, clipId, pitchScale: 1f, volumeScale: volumeScale, applyJitter: false, AudioBus.Banter);
        }

        /// <summary>Plays crisp two-stage slingshot pouch reload clack.</summary>
        public bool PlayReloadClack() => PlaySynthCue("reload_clack", pitchScale: 1f, volumeScale: 0.82f, applyJitter: true);

        /// <summary>Plays hollow dry tick when a skill cannot cast due to insufficient Water.</summary>
        public bool PlayWaterEmpty() => PlaySynthCue("water_empty", pitchScale: 1f, volumeScale: 0.76f, applyJitter: false);

        /// <summary>Plays sharp teks-card / wire snap when a hidden trap triggers or breaks.</summary>
        public bool PlayTrapSnap() => PlaySynthCue("trap_snap", pitchScale: 1f, volumeScale: 0.90f, applyJitter: true);

        /// <summary>Plays chunky box/wood shatter when a destructible wall breaks.</summary>
        public bool PlayWallBreak() => PlaySynthCue("wall_break", pitchScale: 1f, volumeScale: 0.94f, applyJitter: true);

        /// <summary>Plays crisp sonar glass chime when a reveal triggers on or by the local player.</summary>
        public bool PlayRevealChime() => PlaySynthCue("reveal_chime", pitchScale: 1f, volumeScale: 0.86f, applyJitter: false);

        /// <summary>Plays a subtle tactile hover micro-tick on the 2D UI pool.</summary>
        public bool PlayUiHoverTick() => PlaySynthCueOnBus("ui_hover_tick", AudioBus.Ui, pitchScale: 1f, volumeScale: 0.72f, applyJitter: true);

        /// <summary>Plays a decisive modern tactical PLAY / lock-in confirmation chime on the 2D UI pool.</summary>
        public bool PlayUiPlayConfirm() => PlaySynthCueOnBus("ui_play_confirm", AudioBus.Ui, pitchScale: 1f, volumeScale: 0.96f, applyJitter: false);

        /// <summary>Plays a cached procedurally-synthesized combat feedback cue on the 2D priority pool with zero per-frame heap allocation.</summary>
        public bool PlaySynthCue(string cueId, float pitchScale = 1f, float volumeScale = 1f, bool applyJitter = true)
        {
            return PlaySynthCueOnBus(cueId, AudioBus.SfxConfirm2D, pitchScale, volumeScale, applyJitter);
        }

        bool PlaySynthCueOnBus(string cueId, AudioBus bus, float pitchScale, float volumeScale, bool applyJitter)
        {
            if (!SfxEnabled || string.IsNullOrEmpty(cueId)) return false;
            AudioClip clip = GetOrBuildSynthClip(cueId);
            if (clip == null) return false;
            return Play2DClip(clip, cueId, pitchScale, volumeScale, applyJitter, bus);
        }

        /// <summary>Plays crisp UI card/button tap or cancel feedback on the 2D UI pool.</summary>
        public bool PlayUiTap(bool isCancel = false)
        {
            return PlaySfxOnBus(isCancel ? "card_cancel" : "card_pick", AudioBus.Ui, pitchScale: 1f, volumeScale: 1f, applyJitter: true);
        }

        public bool PlayUiTap(string role)
        {
            if (string.IsNullOrEmpty(role)) return PlaySfxOnBus("card_pick", AudioBus.Ui, 1f, 1f, true);
            switch (role)
            {
                case "hover":
                    return PlayUiHoverTick();
                case "play":
                case "lock_in":
                    return PlayUiPlayConfirm();
                case "cancel":
                case "back":
                case "deny":
                    return PlaySfxOnBus("card_cancel", AudioBus.Ui, 1f, 1f, true);
                case "ready":
                case "confirm":
                case "chime":
                    return PlaySfxOnBus("card_ready", AudioBus.Ui, 1f, 1f, true);
                default:
                    return PlaySfxOnBus("card_pick", AudioBus.Ui, 1f, 1f, true);
            }
        }

        /// <summary>Plays Agawang Base objective cues (base capture, rescue, merienda pickup, tag toast, match victory) and ducks BGM on major beats.</summary>
        public bool PlayObjectiveEvent(string eventKind = "capture")
        {
            if (string.IsNullOrEmpty(eventKind)) return false;
            switch (eventKind)
            {
                case "victory":
                case "win":
                case "match_won":
                case "round_won":
                    if (SfxEnabled) TriggerDuck(durationSeconds: 0.9f);
                    return PlaySfx("victory", pitchScale: 1f, volumeScale: 1f, applyJitter: false);
                case "merienda":
                case "heal":
                case "buff":
                    return PlaySfx("heal");
                case "tag_in":
                case "tagged":
                    if (SfxEnabled) TriggerDuck();
                    // TagToast.SOUND_IN ("card_pick" at 1.25x speed).
                    return PlaySfx("card_pick", pitchScale: 1.25f);
                case "tag_out":
                    // TagToast.SOUND_OUT ("card_cancel" at 1.1x speed).
                    return PlaySfx("card_cancel", pitchScale: 1.1f);
                case "contested":
                case "alert":
                    return PlaySfx("slowed_sting");
                case "capture":
                case "rescue":
                case "rescued":
                case "point":
                    if (SfxEnabled) TriggerDuck();
                    return PlaySfx("card_ready");
                case "card_ready":
                default:
                    return PlaySfx("card_ready");
            }
        }

        /// <summary>Plays Larong Kalye mode cues (Tumbang Preso can tink/roll, Patintero line/tag, Langit-Lupa mound/platform).</summary>
        public bool PlayStreetEvent(string eventKind = "point")
        {
            if (string.IsNullOrEmpty(eventKind)) return false;
            switch (eventKind)
            {
                case "can_hit":
                case "lata_hit":
                case "kick":
                    return PlaySfx("lata_hit");
                case "can_reset":
                case "lata_poof":
                    return PlaySfx("lata_poof");
                case "mound":
                case "lupa":
                case "soil_rise":
                    return PlaySfx("soil_rise");
                case "soil_expand":
                    return PlaySfx("soil_expand");
                case "platform_break":
                case "soil_block":
                    return PlaySfx("soil_block");
                case "roll_step":
                case "step":
                    return PlaySfx("card_pick");
                case "roll_land":
                case "taya_picked":
                    return PlaySfx("card_ready");
                case "tag":
                case "taya":
                    if (SfxEnabled) TriggerDuck();
                    return PlaySfx("tsinelas_hit_heavy");
                case "trip":
                case "tripped":
                    bool a = PlaySfx("fall_thud");
                    bool b = PlaySfx("laugh_at_fall_thud");
                    return a || b;
                case "victory":
                case "win":
                    if (SfxEnabled) TriggerDuck(durationSeconds: 0.9f);
                    return PlaySfx("victory", pitchScale: 1f, volumeScale: 1f, applyJitter: false);
                case "point":
                case "line_crossed":
                default:
                    if (SfxEnabled) TriggerDuck();
                    return PlaySfx("card_ready");
            }
        }

        /// <summary>Plays status-landing cues matching SfxView.EFFECT (laugh_at_fall_thud reserved strictly for tripped).</summary>
        public void PlayStatusEffect(string statusKind)
        {
            if (string.IsNullOrEmpty(statusKind)) return;
            switch (statusKind)
            {
                case "stun":
                case "stunned":
                    PlaySfx("stunned");
                    break;
                case "slow":
                case "slowed":
                    PlaySfx("slowed_sting");
                    break;
                case "trip":
                case "tripped":
                    PlaySfx("fall_thud");
                    PlaySfx("laugh_at_fall_thud");
                    break;
                case "polymorph":
                case "lata":
                    PlaySfx("lata_hit");
                    break;
                case "root":
                case "rooted":
                    PlaySfx("soil_block");
                    break;
                case "slip":
                case "slipping":
                case "blur":
                case "blurred":
                    PlaySfx("slowed_sting");
                    break;
            }
        }

        /// <summary>
        /// Plays `sfxId` ("assets/audio/sfx/&lt;sfxId&gt;.mp3") on the 2D priority pool with 0.95..1.05 pitch jitter
        /// times the clip's owner-tuned base pitch.
        /// </summary>
        public bool PlaySfx(string sfxId, float pitchScale = 1f, float volumeScale = 1f, bool applyJitter = true)
        {
            return PlaySfxOnBus(sfxId, AudioBus.SfxConfirm2D, pitchScale, volumeScale, applyJitter);
        }

        bool PlaySfxOnBus(string sfxId, AudioBus bus, float pitchScale, float volumeScale, bool applyJitter)
        {
            if (!SfxEnabled || string.IsNullOrEmpty(sfxId)) return false;
            AudioClip clip = LoadSfxClip(sfxId);
            if (clip == null) return false;
            return Play2DClip(clip, sfxId, pitchScale, volumeScale, applyJitter, bus);
        }

        bool Play2DClip(AudioClip clip, string clipId, float pitchScale, float volumeScale, bool applyJitter, AudioBus bus)
        {
            float effectiveVol = EffectiveSfxVolume * _busGains[(int)bus] * Mathf.Clamp01(volumeScale);
            if (effectiveVol <= 0.0001f) return false;

            AudioSource voice = Acquire2DVoice();
            if (voice == null) return false;

            if (_mixer != null)
            {
                voice.outputAudioMixerGroup = bus == AudioBus.Ui
                    ? _uiGroup
                    : (bus == AudioBus.Banter ? _banterGroup : _confirmGroup);
            }

            float jitter = applyJitter ? SamplePitchJitter() : 1f;
            voice.Stop();
            voice.clip = clip;
            voice.mute = false;
            voice.volume = effectiveVol;
            voice.pitch = Mathf.Clamp(BaseClipPitch(clipId) * pitchScale * jitter, 0.5f, 2.5f);
            voice.Play();
            return true;
        }

        bool PlayWorldClip(AudioClip clip, string clipId, Vector3 worldPos, float volume, float pitchScale, float pitchJitter)
        {
            float effectiveVol = EffectiveSfxVolume * _busGains[(int)AudioBus.SfxCombat3D] * Mathf.Clamp01(volume);
            if (effectiveVol <= 0.0001f) return false;

            AudioSource voice = Acquire3DVoice();
            if (voice == null) return false;

            float jitter = pitchJitter > 0f ? SamplePitchJitter(pitchJitter) : 1f;
            voice.Stop();
            voice.transform.position = worldPos;
            voice.clip = clip;
            voice.mute = false;
            voice.volume = effectiveVol;
            voice.pitch = Mathf.Clamp(BaseClipPitch(clipId) * pitchScale * jitter, 0.5f, 2.5f);
            voice.Play();
            return true;
        }

        public static string ResolveWeaponCastClip(string weaponId, out float pitchScale)
        {
            pitchScale = 1f;
            if (string.IsNullOrEmpty(weaponId)) return null;
            switch (weaponId)
            {
                case "bato_light":
                    pitchScale = 1.2f; // SfxView.CAST_SPEED: quicker rock whoosh
                    return "tsinelas_throw_light";
                case "bato_heavy":
                case "ult_bagsak":
                    return "bato_meteor_fall";
                case "bola":
                    return "bola_throw";
                case "gunting_heavy":
                    return "gunting_big_charge";
                case "gunting_light":
                    return "gunting_light";
                case "heal":
                    return "heal";
                case "jacks":
                    return "jacks_scatter";
                case "langit_lupa":
                    return "soil_rise";
                case "lata":
                    return "lata_toss";
                case "papel_shield":
                    return "paper_shield";
                case "papel_trap":
                    return "trap_place";
                case "trumpo":
                    return "trumpo";
                case "tsinelas_heavy":
                    return "tsinelas_throw_heavy";
                case "tsinelas_light":
                case "tirador_bato":
                    return "tsinelas_throw_light";
                case "tirador":
                    pitchScale = 1.18f;
                    return "tsinelas_throw_light";

                // Chunk 2 — 36 Spec Kit Skills (docs/KITS.md)
                case "luksong_tinik":
                    pitchScale = 1.35f;
                    return "tsinelas_throw_light";
                case "tira_holens":
                    pitchScale = 1.25f;
                    return "bola_throw";
                case "tsinelas_bagsik":
                    return "tsinelas_throw_heavy";
                case "syato":
                    pitchScale = 1.25f;
                    return "tsinelas_throw_light";
                case "trumpo_ikot":
                    return "trumpo";
                case "pukpok_palayok":
                    return "bato_meteor_fall";
                case "sipa":
                    pitchScale = 1.15f;
                    return "bola_throw";
                case "tiyakad":
                    pitchScale = 1.20f;
                    return "soil_rise";
                case "bato_execute":
                    return "bato_meteor_fall";
                case "bahay_bahayan":
                    pitchScale = 0.85f;
                    return "paper_shield";
                case "pabitin_heal":
                    return "heal";
                case "palosebo":
                    return "soil_rise";
                case "chinese_garter":
                    pitchScale = 1.25f;
                    return "paper_shield";
                case "sungka":
                    pitchScale = 0.90f;
                    return "jacks_scatter";
                case "langit":
                    pitchScale = 1.15f;
                    return "heal";
                case "sack_race":
                    pitchScale = 1.20f;
                    return "fall_thud";
                case "luksong_baka":
                    pitchScale = 0.90f;
                    return "card_ready";
                case "tinik_wall":
                    pitchScale = 0.90f;
                    return "soil_rise";
                case "teks_tripwire":
                    return "trap_place";
                case "tago":
                    pitchScale = 1.20f;
                    return "lata_poof";
                case "saranggola":
                    pitchScale = 1.35f;
                    return "paper_shield";
                case "yoyo":
                    pitchScale = 0.95f;
                    return "bola_throw";
                case "piko":
                    return "soil_expand";
                case "pogs_slam":
                    pitchScale = 1.10f;
                    return "bato_heavy_boom";
                case "sili_tubig":
                    pitchScale = 1.30f;
                    return "gunting_light";
                case "trumpo_bantay":
                    pitchScale = 1.15f;
                    return "trumpo";
                case "taya":
                    pitchScale = 0.85f;
                    return "card_ready";
                case "jackstone":
                    return "jacks_scatter";
                case "luksong_lubid":
                    pitchScale = 0.95f;
                    return "trap_place";
                case "garter_net":
                    pitchScale = 0.90f;
                    return "trap_spring";
                case "holens_spill":
                    pitchScale = 1.20f;
                    return "jacks_scatter";
                case "lata_morph":
                    return "lata_toss";
                case "patintero_lines":
                    pitchScale = 1.15f;
                    return "soil_expand";
                case "pabitin_snare":
                    pitchScale = 1.25f;
                    return "bato_meteor_fall";
                case "sako_trap":
                    pitchScale = 0.85f;
                    return "trap_place";
                case "lupa_quake":
                    pitchScale = 0.80f;
                    return "soil_rise";
                default:
                    return null;
            }
        }

        public static string ResolveHitClip(bool isTag, string weaponId, out float pitchScale)
        {
            pitchScale = 1f;
            if (isTag) return "tsinelas_hit_heavy";
            if (!string.IsNullOrEmpty(weaponId))
            {
                switch (weaponId)
                {
                    case "bato_light":
                        pitchScale = 0.75f; // SfxView.HIT_SPEED: deeper rock thud
                        return "tsinelas_hit";
                    case "tsinelas_heavy":
                    case "tsinelas_bagsik":
                    case "sack_race":
                        return "tsinelas_hit_heavy";
                    case "bato_heavy":
                    case "ult_bagsak":
                    case "pukpok_palayok":
                    case "bato_execute":
                    case "pogs_slam":
                    case "lupa_quake":
                        return "bato_heavy_boom";
                    case "lata":
                    case "lata_morph":
                    case "syato":
                        return "lata_hit";
                    case "tira_holens":
                    case "sipa":
                        return "bola_bounce";
                    case "sili_tubig":
                        return "slowed_sting";
                    case "pabitin_snare":
                    case "garter_net":
                        return "trap_spring";
                }
            }
            return "tsinelas_hit";
        }

        static float BaseClipPitch(string sfxId)
        {
            // SfxView.PITCH sound-audit speeds (docs/ART.md "Sound audit").
            switch (sfxId)
            {
                case "lata_hit": return EmptyCanPitch;
                case "gunting_light": return 1.1f;
                case "gunting_big_snip": return 0.8f;
                case "bola_throw": return 1.1f;
                case "trap_place": return 1.1f;
                default: return 1f;
            }
        }

        AudioSource Acquire2DVoice()
        {
            return AcquireFromPool(_sfx2DSources, ref _next2DVoice);
        }

        AudioSource Acquire3DVoice()
        {
            return AcquireFromPool(_sfx3DSources, ref _next3DVoice);
        }

        static AudioSource AcquireFromPool(AudioSource[] pool, ref int cursor)
        {
            int count = pool.Length;
            for (int offset = 0; offset < count; offset++)
            {
                int idx = (cursor + offset) % count;
                AudioSource candidate = pool[idx];
                if (candidate != null && !candidate.isPlaying)
                {
                    cursor = (idx + 1) % count;
                    return candidate;
                }
            }
            AudioSource oldest = pool[cursor];
            cursor = (cursor + 1) % count;
            return oldest;
        }

        float SamplePitchJitter() => SamplePitchJitter(0.5f * (MaxPitchJitter - MinPitchJitter));

        float SamplePitchJitter(float halfRange)
        {
            // Zero-GC xorshift32 PRNG strictly for view-only ear variety.
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x;
            float t = (x & 0x00FFFFFFu) * (1f / 16777215f);
            float clamped = Mathf.Clamp(halfRange, 0f, 0.45f);
            return (1f - clamped) + (2f * clamped * t);
        }

        void ApplyVolumes()
        {
            ApplyBgmVolume();
            ApplySfxVolumes();
        }

        void ApplyBgmVolume()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = AppliedBgmVolume;
                _bgmSource.mute = !MusicEnabled;
            }
            ApplyAmbienceVolume();
            SyncMixerSliderParameter("MusicVol", EffectiveBgmVolume);
        }

        void ApplyAmbienceVolume()
        {
            if (_ambienceSource == null) return;
            bool enabled = MusicEnabled && SfxEnabled;
            _ambienceSource.volume = AppliedAmbienceVolume;
            _ambienceSource.mute = !enabled;
            if (!enabled)
            {
                if (_ambienceSource.isPlaying) _ambienceSource.Stop();
            }
            else if (_ambienceDesired && Application.isPlaying && _ambienceSource.clip != null && !_ambienceSource.isPlaying)
            {
                _ambienceSource.Play();
            }
        }

        void ApplySfxVolumes()
        {
            float vol2D = EffectiveSfxVolume * _busGains[(int)AudioBus.SfxConfirm2D];
            for (int i = 0; i < _sfx2DSources.Length; i++)
            {
                AudioSource src = _sfx2DSources[i];
                if (src == null) continue;
                src.volume = vol2D;
                src.mute = !SfxEnabled;
                if (!SfxEnabled && src.isPlaying) src.Stop();
            }

            float vol3D = EffectiveSfxVolume * _busGains[(int)AudioBus.SfxCombat3D];
            for (int i = 0; i < _sfx3DSources.Length; i++)
            {
                AudioSource src = _sfx3DSources[i];
                if (src == null) continue;
                src.volume = vol3D;
                src.mute = !SfxEnabled;
                if (!SfxEnabled && src.isPlaying) src.Stop();
            }

            ApplyAmbienceVolume();
            SyncMixerSliderParameter("SFXVol", EffectiveSfxVolume);
        }

        void SyncMixerSliderParameter(string paramName, float linear01)
        {
            if (_mixer == null) return;
            // Only write exposed user-slider parameters; ducking never writes MusicVol so sliders and ducking coexist.
            if (!_mixer.GetFloat(paramName, out _)) return;
            float db = linear01 <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear01), -80f, 0f);
            _mixer.SetFloat(paramName, db);
        }

        static AudioClip ResolveAnyClip(string clipId)
        {
            if (IsKnownSynthCue(clipId))
                return GetOrBuildSynthClip(clipId);
            AudioClip asset = LoadSfxClip(clipId);
            return asset != null ? asset : GetOrBuildSynthClip(clipId);
        }

        static bool IsKnownSynthCue(string cueId)
        {
            for (int i = 0; i < SynthCueIds.Length; i++)
            {
                if (string.Equals(SynthCueIds[i], cueId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static AudioClip LoadBgmClip(string bgmId)
        {
            if (BgmCache.TryGetValue(bgmId, out AudioClip cached))
            {
                if (cached != null) return cached;
                BgmCache.Remove(bgmId);
            }
            if (MissingBgm.Contains(bgmId)) return null;

            AudioClip loaded = Resources.Load<AudioClip>(BgmPrefix + bgmId);
            if (loaded != null) BgmCache[bgmId] = loaded;
            else MissingBgm.Add(bgmId);
            return loaded;
        }

        static AudioClip LoadSfxClip(string sfxId)
        {
            if (SfxCache.TryGetValue(sfxId, out AudioClip cached))
            {
                if (cached != null) return cached;
                SfxCache.Remove(sfxId);
            }
            if (MissingSfx.Contains(sfxId)) return null;

            AudioClip loaded = Resources.Load<AudioClip>(SfxPrefix + sfxId);
            if (loaded != null) SfxCache[sfxId] = loaded;
            else MissingSfx.Add(sfxId);
            return loaded;
        }

        public static AudioClip GetSynthClip(string cueId) => GetOrBuildSynthClip(cueId);

        static AudioClip GetOrBuildSynthClip(string cueId)
        {
            if (string.IsNullOrEmpty(cueId)) return null;
            if (SynthCache.TryGetValue(cueId, out AudioClip cached))
            {
                if (cached != null) return cached;
                SynthCache.Remove(cueId);
            }

            AudioClip built = BuildSynthClip(cueId);
            if (built != null)
                SynthCache[cueId] = built;
            return built;
        }

        /// <summary>Synthesizes deterministic mono PCM samples for any built-in procedural cue (22050 Hz).</summary>
        public static bool TrySynthesizePcm(string cueId, out float[] pcm, out int sampleRate)
        {
            pcm = null;
            sampleRate = SynthSampleRate;
            if (string.IsNullOrEmpty(cueId)) return false;

            if (string.Equals(cueId, AmbienceDayCueId, StringComparison.Ordinal))
            {
                pcm = SynthesizeBarangayAmbience(SynthSampleRate);
                return pcm != null && pcm.Length > 0;
            }

            float duration;
            switch (cueId)
            {
                case "body_hit": duration = 0.055f; break;
                case "headshot_ding": duration = 0.210f; break;
                case "taya_confirm": duration = 0.280f; break;
                case "reload_clack": duration = 0.145f; break;
                case "water_empty": duration = 0.075f; break;
                case "trap_snap": duration = 0.115f; break;
                case "wall_break": duration = 0.220f; break;
                case "reveal_chime": duration = 0.230f; break;
                case "footstep_concrete": duration = 0.085f; break;
                case "footstep_water": duration = 0.130f; break;
                case "slide_skid": duration = 0.260f; break;
                case "land_thud": duration = 0.140f; break;
                case "multitag_fanfare": duration = 0.520f; break;
                case "ui_hover_tick": duration = 0.024f; break;
                case "ui_play_confirm": duration = 0.195f; break;
                default: return false;
            }

            int samples = Mathf.Max(64, Mathf.RoundToInt(duration * SynthSampleRate));
            float actualDuration = (samples - 1) / (float)SynthSampleRate;
            pcm = new float[samples];
            uint noise = 0x51A7F31Bu ^ ((uint)cueId.Length * 0x9E3779B9u) ^ ((uint)cueId[0] * 0x85EBCA6Bu);
            const float twoPi = Mathf.PI * 2f;

            // One-pole filter states for warm, band-shaped foley and crisp UI clicks.
            float lpLow = 0f;
            float lpMid = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SynthSampleRate;
                float u = i / (float)(samples - 1);
                float n = NextSynthNoise(ref noise);
                lpLow += 0.08f * (n - lpLow);
                lpMid += 0.28f * (n - lpMid);
                float bpMid = lpMid - lpLow;

                float attackSec = cueId == "ui_hover_tick" ? 0.002f : 0.0035f;
                float tailSec = cueId == "ui_hover_tick" ? 0.006f : 0.008f;
                float attack = Mathf.Clamp01(t / attackSec);
                float tail = Mathf.Clamp01((actualDuration - t) / tailSec);
                float v = 0f;

                switch (cueId)
                {
                    case "body_hit":
                    {
                        float env = Mathf.Exp(-t * 58f);
                        float punch = Mathf.Sin(twoPi * (720f - 280f * u) * t) * 0.65f;
                        float tick = Mathf.Sin(twoPi * 1680f * t) * Mathf.Exp(-t * 140f) * 0.45f;
                        v = (punch + tick + n * 0.22f * Mathf.Exp(-t * 110f)) * env;
                        break;
                    }
                    case "headshot_ding":
                    {
                        float bellEnv = Mathf.Exp(-t * 15f);
                        float click = (Mathf.Sin(twoPi * 2900f * t) + n * 0.4f) * Mathf.Exp(-t * 170f) * 0.55f;
                        float f1 = Mathf.Sin(twoPi * 1760f * t) * 0.52f;
                        float f2 = Mathf.Sin(twoPi * 2637f * t) * 0.34f * Mathf.Exp(-t * 7f);
                        float f3 = Mathf.Sin(twoPi * 3520f * t) * 0.20f * Mathf.Exp(-t * 12f);
                        v = (f1 + f2 + f3) * bellEnv + click;
                        break;
                    }
                    case "taya_confirm":
                    {
                        float thump = Mathf.Sin(twoPi * (165f - 90f * Mathf.Clamp01(t / 0.09f)) * t) * Mathf.Exp(-t * 28f) * 0.52f;
                        float note1 = t < 0.11f
                            ? Mathf.Sin(twoPi * 659.25f * t) * Mathf.Exp(-t * 22f) * 0.45f
                            : 0f;
                        float t2 = Mathf.Max(0f, t - 0.065f);
                        float note2 = t >= 0.065f
                            ? (Mathf.Sin(twoPi * 987.77f * t2) * 0.50f + Mathf.Sin(twoPi * 1479.98f * t2) * 0.26f) * Mathf.Exp(-t2 * 13f) * Mathf.Clamp01(t2 / 0.004f)
                            : 0f;
                        v = thump + note1 + note2;
                        break;
                    }
                    case "reload_clack":
                    {
                        float p1 = (Mathf.Sin(twoPi * 960f * t) * 0.45f + n * 0.50f) * Mathf.Exp(-t * 95f);
                        float t2 = t - 0.072f;
                        float p2 = t2 >= 0f
                            ? (Mathf.Sin(twoPi * 1420f * t2) * 0.58f + Mathf.Sin(twoPi * 2180f * t2) * 0.28f + n * 0.25f) * Mathf.Exp(-t2 * 72f) * Mathf.Clamp01(t2 / 0.002f)
                            : 0f;
                        v = p1 * 0.72f + p2 * 0.90f;
                        break;
                    }
                    case "water_empty":
                    {
                        float knock1 = Mathf.Sin(twoPi * (410f - 130f * u) * t) * Mathf.Exp(-t * 68f) * 0.55f;
                        float t2 = t - 0.032f;
                        float knock2 = t2 >= 0f
                            ? Mathf.Sin(twoPi * 290f * t2) * Mathf.Exp(-t2 * 75f) * 0.48f * Mathf.Clamp01(t2 / 0.002f)
                            : 0f;
                        v = knock1 + knock2;
                        break;
                    }
                    case "trap_snap":
                    {
                        float whip = n * Mathf.Exp(-t * 115f) * 0.62f;
                        float twang = Mathf.Sin(twoPi * (2250f - 1350f * u) * t) * Mathf.Exp(-t * 36f) * 0.58f;
                        v = whip + twang;
                        break;
                    }
                    case "wall_break":
                    {
                        float boom = Mathf.Sin(twoPi * (128f - 72f * u) * t) * Mathf.Exp(-t * 16f) * 0.60f;
                        float crack1 = n * Mathf.Exp(-t * 19f) * 0.48f;
                        float t2 = t - 0.055f;
                        float crack2 = t2 >= 0f ? (n * 0.42f + Mathf.Sin(twoPi * 540f * t2) * 0.24f) * Mathf.Exp(-t2 * 24f) : 0f;
                        v = boom + crack1 + crack2;
                        break;
                    }
                    case "reveal_chime":
                    {
                        float ping1 = Mathf.Sin(twoPi * 1046.5f * t) * Mathf.Exp(-t * 14f) * 0.42f;
                        float t2 = Mathf.Max(0f, t - 0.045f);
                        float ping2 = t >= 0.045f
                            ? (Mathf.Sin(twoPi * 1567.98f * t2) * 0.46f + Mathf.Sin(twoPi * 2093f * t2) * 0.22f) * Mathf.Exp(-t2 * 11f) * Mathf.Clamp01(t2 / 0.006f)
                            : 0f;
                        v = ping1 + ping2;
                        break;
                    }
                    case "footstep_concrete":
                    {
                        float heel = Mathf.Sin(twoPi * (155f - 85f * u) * t) * Mathf.Exp(-t * 54f) * 0.58f;
                        float slap = Mathf.Sin(twoPi * 420f * t) * Mathf.Exp(-t * 82f) * 0.24f;
                        float grit = bpMid * Mathf.Exp(-t * 72f) * 0.95f;
                        v = heel + slap + grit;
                        break;
                    }
                    case "footstep_water":
                    {
                        float dip = Mathf.Sin(twoPi * (215f - 95f * u) * t) * Mathf.Exp(-t * 36f) * 0.34f;
                        float sprayEnv = Mathf.Clamp01(t / 0.014f) * Mathf.Exp(-t * 24f);
                        float spray = (bpMid * 1.25f + lpMid * 0.35f) * sprayEnv;
                        float t2 = t - 0.018f;
                        float drop = t2 >= 0f
                            ? Mathf.Sin(twoPi * (680f + 760f * Mathf.Clamp01(t2 / 0.065f)) * t2) * Mathf.Exp(-t2 * 44f) * 0.28f * Mathf.Clamp01(t2 / 0.004f)
                            : 0f;
                        v = dip + spray + drop;
                        break;
                    }
                    case "slide_skid":
                    {
                        float env = Mathf.Clamp01(t / 0.025f) * Mathf.Exp(-t * 8.5f);
                        float gritPulse = 0.78f + 0.22f * Mathf.Sin(twoPi * 38f * t);
                        float scrape = Mathf.Sin(twoPi * (330f - 120f * u) * t) * 0.22f;
                        float friction = (bpMid * 1.45f + lpLow * 0.65f) * gritPulse;
                        v = (friction + scrape) * env;
                        break;
                    }
                    case "land_thud":
                    {
                        float drop1 = Mathf.Sin(twoPi * (134f - 86f * u) * t) * Mathf.Exp(-t * 26f) * 0.72f;
                        float t2 = t - 0.022f;
                        float drop2 = t2 >= 0f
                            ? Mathf.Sin(twoPi * (195f - 100f * Mathf.Clamp01(t2 / 0.09f)) * t2) * Mathf.Exp(-t2 * 46f) * 0.32f * Mathf.Clamp01(t2 / 0.004f)
                            : 0f;
                        float dust = lpLow * Mathf.Exp(-t * 38f) * 0.78f;
                        v = drop1 + drop2 + dust;
                        break;
                    }
                    case "multitag_fanfare":
                    {
                        float sub = Mathf.Sin(twoPi * 130.81f * t) * Mathf.Exp(-t * 9f) * 0.28f;
                        float n1 = WarmBell(t, 0.000f, 523.25f, 11f, 0.36f);
                        float n2 = WarmBell(t, 0.085f, 659.25f, 10f, 0.40f);
                        float n3 = WarmBell(t, 0.170f, 783.99f, 7.5f, 0.46f);
                        float shimmer = WarmBell(t, 0.195f, 1174.66f, 8.5f, 0.22f);
                        v = sub + n1 + n2 + n3 + shimmer;
                        break;
                    }
                    case "ui_hover_tick":
                    {
                        float body = Mathf.Sin(twoPi * (1680f - 440f * u) * t) * Mathf.Exp(-t * 185f) * 0.54f;
                        float micro = bpMid * Mathf.Exp(-t * 230f) * 0.48f;
                        v = body + micro;
                        break;
                    }
                    case "ui_play_confirm":
                    {
                        float click = (Mathf.Sin(twoPi * 1540f * t) * 0.36f + bpMid * 0.35f) * Mathf.Exp(-t * 140f);
                        float low = Mathf.Sin(twoPi * 246.94f * t) * Mathf.Exp(-t * 22f) * 0.36f;
                        float tone1 = WarmBell(t, 0.000f, 493.88f, 17f, 0.34f);
                        float tone2 = WarmBell(t, 0.048f, 739.99f, 12f, 0.46f);
                        float air = WarmBell(t, 0.052f, 1479.98f, 16f, 0.16f);
                        v = click + low + tone1 + tone2 + air;
                        break;
                    }
                }

                pcm[i] = Mathf.Clamp(v * attack * tail * 0.85f, -0.98f, 0.98f);
            }

            return true;
        }

        static float WarmBell(float t, float startSec, float freqHz, float decay, float amp)
        {
            float dt = t - startSec;
            if (dt < 0f) return 0f;
            const float twoPi = Mathf.PI * 2f;
            float env = Mathf.Clamp01(dt / 0.004f) * Mathf.Exp(-dt * decay);
            float fund = Mathf.Sin(twoPi * freqHz * dt);
            float h2 = Mathf.Sin(twoPi * freqHz * 2f * dt) * 0.28f * Mathf.Exp(-dt * 4f);
            float h3 = Mathf.Sin(twoPi * freqHz * 3f * dt) * 0.10f * Mathf.Exp(-dt * 9f);
            return (fund + h2 + h3) * env * amp;
        }

        /// <summary>
        /// Synthesizes a 4.0 s seamless Philippine barangay sunlit ambience bed: gentle tropical breeze,
        /// distant warm barrio air, and faint soft afternoon cicada shimmer with zero loop-boundary click.
        /// </summary>
        static float[] SynthesizeBarangayAmbience(int sampleRate)
        {
            const float duration = 4.0f;
            int samples = sampleRate * 4;
            int fade = sampleRate / 5; // 200 ms equal-power overlap for seamless circular breeze wrap.
            int total = samples + fade;
            float[] rawNoise = new float[total];

            uint state = 0x4B414C59u; // Deterministic seed ("KALY").
            float lpBreeze = 0f;
            float lpWarm = 0f;
            // Warm up filter state so sample 0 starts cleanly on the filtered manifold.
            for (int w = 0; w < 512; w++)
            {
                float n = NextSynthNoise(ref state);
                lpBreeze += 0.045f * (n - lpBreeze);
                lpWarm += 0.012f * (n - lpWarm);
            }

            for (int i = 0; i < total; i++)
            {
                float n = NextSynthNoise(ref state);
                lpBreeze += 0.045f * (n - lpBreeze);
                lpWarm += 0.012f * (n - lpWarm);
                rawNoise[i] = lpBreeze * 0.58f + lpWarm * 1.15f;
            }

            float[] pcm = new float[samples];
            const float twoPi = Mathf.PI * 2f;
            float invDuration = 1f / duration; // 0.25 Hz fundamental -> exact integer cycles wrap cleanly.

            for (int i = 0; i < samples; i++)
            {
                float noiseBed;
                if (i < fade)
                {
                    float w = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * (i / (float)fade));
                    noiseBed = rawNoise[i] * w + rawNoise[samples + i] * (1f - w);
                }
                else
                {
                    noiseBed = rawNoise[i];
                }

                float phase = (i / (float)samples) * twoPi;
                float t = i / (float)sampleRate;

                // Slow tropical breeze swell (1 cycle and 2 cycles per 4 s loop).
                float breezeSwell = 0.72f + 0.20f * Mathf.Sin(phase) + 0.08f * Mathf.Sin(2f * phase + 1.1f);
                float breeze = noiseBed * breezeSwell * 0.85f;

                // Distant warm barrio air (exact integer multiples of 0.25 Hz).
                float barrioAir = (Mathf.Sin(twoPi * 110f * t) * 0.032f
                                 + Mathf.Sin(twoPi * 165f * t + 0.7f) * 0.018f)
                                 * (0.80f + 0.20f * Mathf.Sin(phase + 0.4f));

                // Faint soft tropical cicada shimmer (kuliglig; exact integer cycles of 0.25 Hz).
                float cicadaEnv = (0.55f + 0.45f * Mathf.Sin(2f * phase))
                                * (0.72f + 0.28f * Mathf.Sin(twoPi * (56f * invDuration) * t)); // 14 Hz gentle trill
                float cicada = (Mathf.Sin(twoPi * 4150f * t) * 0.016f
                              + Mathf.Sin(twoPi * 4480f * t + 0.9f) * 0.011f)
                              * cicadaEnv;

                pcm[i] = Mathf.Clamp(breeze + barrioAir + cicada, -0.95f, 0.95f);
            }

            return pcm;
        }

        static AudioClip BuildSynthClip(string cueId)
        {
            if (!TrySynthesizePcm(cueId, out float[] pcm, out int sampleRate) || pcm == null || pcm.Length == 0)
                return null;

            try
            {
                AudioClip clip = AudioClip.Create("KalyeahSynth_" + cueId, pcm.Length, 1, sampleRate, false);
                if (clip != null)
                    clip.SetData(pcm, 0);
                return clip;
            }
            catch
            {
                return null;
            }
        }

        static float NextSynthNoise(ref uint state)
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return ((x & 0x00FFFFFFu) * (2f / 16777215f)) - 1f;
        }

        public void Dispose()
        {
            StopBgm();
            StopAmbience();
            for (int i = 0; i < _sfx2DSources.Length; i++)
            {
                if (_sfx2DSources[i] != null && _sfx2DSources[i].isPlaying) _sfx2DSources[i].Stop();
            }
            for (int i = 0; i < _sfx3DSources.Length; i++)
            {
                if (_sfx3DSources[i] != null && _sfx3DSources[i].isPlaying) _sfx3DSources[i].Stop();
            }
            if (Root != null)
            {
                KalyeahResources.Kill(Root);
                Root = null;
            }
        }
    }
}
