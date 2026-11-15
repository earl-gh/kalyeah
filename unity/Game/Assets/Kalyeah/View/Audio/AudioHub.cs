using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// Zero-GC match and menu audio hub: 1 looped BGM AudioSource ("audio/bgm") plus 12 pooled 2D SFX AudioSources
    /// ("audio/sfx/*"), matching scripts/view/sfx_view.gd, scripts/view/match_music.gd, and docs/ART.md ("Audio").
    /// View-only: reads sim/UI events and settings, never alters simulation state or RNG.
    /// </summary>
    public sealed class AudioHub : IDisposable
    {
        public const int SfxPoolSize = 12;
        public const float MinPitchJitter = 0.95f;
        public const float MaxPitchJitter = 1.05f;
        public const float DefaultBgmRulesShare = 0.4f;
        public const float DefaultSfxRulesShare = 0.8f;
        public const float EmptyCanPitch = 1.5f;

        const string BgmPrefix = "audio/";
        const string SfxPrefix = "audio/sfx/";

        // Exact 32 clips in assets/audio/sfx/*.mp3 synced by tools/unity_export/sync_assets.sh.
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

        public GameObject Root { get; private set; }
        public AudioSource BgmSource => _bgmSource;
        public int PooledSfxCount => _sfxSources.Length;

        public float MasterVolume { get; private set; } = 1f;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float RulesBgmShare { get; private set; }
        public float RulesSfxShare { get; private set; }
        public bool MusicEnabled { get; private set; } = true;
        public bool SfxEnabled { get; private set; } = true;

        public float EffectiveBgmVolume => MusicEnabled ? Mathf.Clamp01(MasterVolume * BgmVolume * RulesBgmShare) : 0f;
        public float EffectiveSfxVolume => SfxEnabled ? Mathf.Clamp01(MasterVolume * SfxVolume * RulesSfxShare) : 0f;
        public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;

        readonly AudioSource _bgmSource;
        readonly AudioSource[] _sfxSources = new AudioSource[SfxPoolSize];
        int _nextVoice;
        uint _rngState;

        public AudioHub(
            Transform parent = null,
            float rulesBgmShare = DefaultBgmRulesShare,
            float rulesSfxShare = DefaultSfxRulesShare,
            bool prewarm = true)
        {
            RulesBgmShare = Mathf.Clamp01(rulesBgmShare);
            RulesSfxShare = Mathf.Clamp01(rulesSfxShare);
            _rngState = (uint)Environment.TickCount ^ 0x9E3779B9u;
            if (_rngState == 0u) _rngState = 0x6D2B79F5u;

            Root = new GameObject("AudioHub");
            if (parent != null) Root.transform.SetParent(parent, false);

            var bgmObject = new GameObject("Bgm");
            bgmObject.transform.SetParent(Root.transform, false);
            _bgmSource = bgmObject.AddComponent<AudioSource>();
            ConfigureSource(_bgmSource, loop: true, priority: 64);
            _bgmSource.volume = EffectiveBgmVolume;

            for (int i = 0; i < SfxPoolSize; i++)
            {
                var voiceObject = new GameObject("SfxVoice" + i);
                voiceObject.transform.SetParent(Root.transform, false);
                AudioSource voice = voiceObject.AddComponent<AudioSource>();
                ConfigureSource(voice, loop: false, priority: 128);
                voice.volume = EffectiveSfxVolume;
                _sfxSources[i] = voice;
            }

            if (prewarm) Prewarm();
        }

        static void ConfigureSource(AudioSource source, bool loop, int priority)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
        }

        /// <summary>Loads and caches bgm.mp3 and all 32 sfx/*.mp3 clips upfront so mid-round playback allocates 0 B.</summary>
        public void Prewarm()
        {
            LoadBgmClip("bgm");
            for (int i = 0; i < KnownSfxIds.Length; i++) LoadSfxClip(KnownSfxIds[i]);
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
            _bgmSource.volume = EffectiveBgmVolume;
            _bgmSource.mute = !MusicEnabled;

            if (restart || changed || !_bgmSource.isPlaying) _bgmSource.Play();
            return true;
        }

        public void StopBgm()
        {
            if (_bgmSource != null && _bgmSource.isPlaying) _bgmSource.Stop();
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
            if (string.IsNullOrEmpty(weaponId)) return false;
            switch (weaponId)
            {
                case "bato_heavy":
                case "ult_bagsak":
                    return PlaySfx("bato_heavy_boom");
                case "lata":
                    return PlaySfx("lata_poof");
                case "bola":
                    return PlaySfx("bola_bounce");
                case "gunting_heavy":
                    return PlaySfx("gunting_big_snip");
                case "gunting_light":
                    return PlaySfx("gunting_light");
                case "papel_shield":
                    return PlaySfx("paper_block");
                case "papel_trap":
                    return PlaySfx("trap_spring");
                case "langit_lupa":
                    return PlaySfx("soil_block");
                default:
                    return false;
            }
        }

        /// <summary>Plays hit or tag confirmation SFX (never plays laugh_at_fall_thud, which is strictly for random trip).</summary>
        public bool PlayHit(bool isTag) => PlayHit(isTag, null);

        public bool PlayHit(bool isTag, string weaponId)
        {
            string clipId = ResolveHitClip(isTag, weaponId, out float pitchScale);
            return PlaySfx(clipId, pitchScale);
        }

        /// <summary>Plays crisp UI card/button tap or cancel feedback on the SFX pool.</summary>
        public bool PlayUiTap(bool isCancel = false)
        {
            return PlaySfx(isCancel ? "card_cancel" : "card_pick", pitchScale: 1f, volumeScale: 1f);
        }

        public bool PlayUiTap(string role)
        {
            if (string.IsNullOrEmpty(role)) return PlaySfx("card_pick");
            switch (role)
            {
                case "cancel":
                case "back":
                case "deny":
                    return PlaySfx("card_cancel");
                case "ready":
                case "confirm":
                case "chime":
                    return PlaySfx("card_ready");
                default:
                    return PlaySfx("card_pick");
            }
        }

        /// <summary>Plays Agawang Base objective cues (base capture, rescue, merienda pickup, tag toast, match victory).</summary>
        public bool PlayObjectiveEvent(string eventKind = "capture")
        {
            if (string.IsNullOrEmpty(eventKind)) return false;
            switch (eventKind)
            {
                case "victory":
                case "win":
                case "match_won":
                case "round_won":
                    return PlaySfx("victory", pitchScale: 1f, volumeScale: 1f, applyJitter: false);
                case "merienda":
                case "heal":
                case "buff":
                    return PlaySfx("heal");
                case "tag_in":
                case "tagged":
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
                case "card_ready":
                case "point":
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
                    return PlaySfx("tsinelas_hit_heavy");
                case "trip":
                case "tripped":
                    bool a = PlaySfx("fall_thud");
                    bool b = PlaySfx("laugh_at_fall_thud");
                    return a || b;
                case "victory":
                case "win":
                    return PlaySfx("victory", pitchScale: 1f, volumeScale: 1f, applyJitter: false);
                case "point":
                case "line_crossed":
                default:
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
            }
        }

        /// <summary>
        /// Plays `sfxId` ("assets/audio/sfx/&lt;sfxId&gt;.mp3") on the next free pooled voice (or reuses the oldest of the 12)
        /// with 0.95..1.05 pitch jitter times the clip's owner-tuned base pitch.
        /// </summary>
        public bool PlaySfx(string sfxId, float pitchScale = 1f, float volumeScale = 1f, bool applyJitter = true)
        {
            if (!SfxEnabled || string.IsNullOrEmpty(sfxId)) return false;
            float effectiveVol = EffectiveSfxVolume * Mathf.Clamp01(volumeScale);
            if (effectiveVol <= 0.0001f) return false;

            AudioClip clip = LoadSfxClip(sfxId);
            if (clip == null) return false;

            AudioSource voice = AcquireVoice();
            if (voice == null) return false;

            float jitter = applyJitter ? SamplePitchJitter() : 1f;
            voice.Stop();
            voice.clip = clip;
            voice.mute = false;
            voice.volume = effectiveVol;
            voice.pitch = Mathf.Clamp(BaseClipPitch(sfxId) * pitchScale * jitter, 0.5f, 2.5f);
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
                        return "tsinelas_hit_heavy";
                    case "bato_heavy":
                    case "ult_bagsak":
                        return "bato_heavy_boom";
                    case "lata":
                        return "lata_hit";
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

        AudioSource AcquireVoice()
        {
            for (int offset = 0; offset < SfxPoolSize; offset++)
            {
                int idx = (_nextVoice + offset) % SfxPoolSize;
                AudioSource candidate = _sfxSources[idx];
                if (candidate != null && !candidate.isPlaying)
                {
                    _nextVoice = (idx + 1) % SfxPoolSize;
                    return candidate;
                }
            }
            AudioSource oldest = _sfxSources[_nextVoice];
            _nextVoice = (_nextVoice + 1) % SfxPoolSize;
            return oldest;
        }

        float SamplePitchJitter()
        {
            // Zero-GC xorshift32 PRNG strictly for view-only ear variety in [0.95, 1.05].
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x;
            float t = (x & 0x00FFFFFFu) * (1f / 16777215f);
            return MinPitchJitter + (MaxPitchJitter - MinPitchJitter) * t;
        }

        void ApplyVolumes()
        {
            ApplyBgmVolume();
            ApplySfxVolumes();
        }

        void ApplyBgmVolume()
        {
            if (_bgmSource == null) return;
            _bgmSource.volume = EffectiveBgmVolume;
            _bgmSource.mute = !MusicEnabled;
        }

        void ApplySfxVolumes()
        {
            float vol = EffectiveSfxVolume;
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                AudioSource src = _sfxSources[i];
                if (src == null) continue;
                src.volume = vol;
                src.mute = !SfxEnabled;
                if (!SfxEnabled && src.isPlaying) src.Stop();
            }
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

        public void Dispose()
        {
            StopBgm();
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (_sfxSources[i] != null && _sfxSources[i].isPlaying) _sfxSources[i].Stop();
            }
            if (Root != null)
            {
                KalyeahResources.Kill(Root);
                Root = null;
            }
        }
    }
}
