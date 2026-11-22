using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class AudioArchitectureTests
    {
        static readonly string[] NewProceduralCueIds =
        {
            AudioHub.AmbienceDayCueId,
            "footstep_concrete",
            "footstep_water",
            "slide_skid",
            "land_thud",
            "multitag_fanfare",
            "ui_hover_tick",
            "ui_play_confirm",
        };

        readonly List<AudioHub> _hubs = new List<AudioHub>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _hubs.Count - 1; i >= 0; i--)
                _hubs[i]?.Dispose();
            _hubs.Clear();
        }

        AudioHub CreateHub(bool prewarm = false)
        {
            var hub = new AudioHub(prewarm: prewarm);
            _hubs.Add(hub);
            return hub;
        }

        static Type FindEditorType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type t = assemblies[i].GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        [Test]
        public void VoicePools_SplitInto3DSpatialCombatAndUnstarvable2DPriority()
        {
            AudioHub hub = CreateHub(prewarm: false);

            Assert.AreEqual(AudioHub.Priority2DPoolSize, hub.Priority2DPoolCount);
            Assert.AreEqual(AudioHub.World3DPoolSize, hub.World3DPoolCount);
            Assert.AreEqual(AudioHub.Priority2DPoolSize + AudioHub.World3DPoolSize, hub.PooledSfxCount);
            Assert.IsNotNull(hub.BgmSource);
            Assert.IsNotNull(hub.AmbienceSource);
            Assert.AreEqual(0f, hub.BgmSource.spatialBlend, 0.0001f);
            Assert.AreEqual(0f, hub.AmbienceSource.spatialBlend, 0.0001f);

            for (int i = 0; i < hub.Priority2DPoolCount; i++)
            {
                AudioSource v2d = hub.GetPriority2DVoice(i);
                Assert.IsNotNull(v2d, $"2D voice {i} missing");
                Assert.AreEqual(0f, v2d.spatialBlend, 0.0001f, $"2D voice {i} must be flat 2D");
                Assert.AreEqual(0f, v2d.dopplerLevel, 0.0001f);
            }

            for (int i = 0; i < hub.World3DPoolCount; i++)
            {
                AudioSource v3d = hub.GetWorld3DVoice(i);
                Assert.IsNotNull(v3d, $"3D voice {i} missing");
                Assert.AreEqual(1f, v3d.spatialBlend, 0.0001f, $"3D voice {i} must be full 3D");
                Assert.AreEqual(AudioRolloffMode.Logarithmic, v3d.rolloffMode);
                Assert.AreEqual(4f, v3d.minDistance, 0.0001f);
                Assert.AreEqual(38f, v3d.maxDistance, 0.0001f);
                Assert.AreEqual(0f, v3d.dopplerLevel, 0.0001f);
                Assert.AreEqual(0f, v3d.spread, 0.0001f);
            }

            // Flood the 3D world pool with distant bot combat and verify 2D priority pool is untouched.
            Vector3 farBotPos = new Vector3(60f, 1.2f, -45f);
            for (int i = 0; i < AudioHub.World3DPoolSize * 2; i++)
            {
                bool played3D = hub.PlayWorldSfx("footstep_concrete", farBotPos + new Vector3(i, 0f, 0f), volume: 0.9f, pitchJitter: 0.05f);
                Assert.IsTrue(played3D, "PlayWorldSfx should resolve procedural foley on the 3D spatial pool");
            }

            for (int i = 0; i < hub.Priority2DPoolCount; i++)
                Assert.IsNull(hub.GetPriority2DVoice(i).clip, "Distant 3D world SFX must never allocate or starve 2D priority voices");

            bool used3DVoice = false;
            for (int i = 0; i < hub.World3DPoolCount; i++)
            {
                AudioSource v3d = hub.GetWorld3DVoice(i);
                if (v3d.clip != null)
                {
                    used3DVoice = true;
                    Assert.GreaterOrEqual(v3d.transform.position.x, 60f);
                }
            }
            Assert.IsTrue(used3DVoice, "At least one 3D voice must hold the played world clip and world position");

            // Local hit-confirm routes strictly onto the 2D priority pool.
            Assert.IsTrue(hub.PlayHitConfirm(isHeadshot: true, isTag: false));
            Assert.IsNotNull(hub.GetPriority2DVoice(0).clip, "Hit-confirm must play on the 2D priority pool");
            Assert.AreEqual(0f, hub.GetPriority2DVoice(0).spatialBlend, 0.0001f);
        }

        [Test]
        public void CombatDucking_SmoothlyAttenuatesBgmAndAmbienceWithoutMutatingUserSettings()
        {
            AudioHub hub = CreateHub(prewarm: false);
            hub.BindVolumes(bgmVolume: 0.85f, sfxVolume: 0.90f, musicEnabled: true, sfxEnabled: true, masterVolume: 0.80f);
            hub.BindMixer(null); // Headless EditMode null-mixer safety check.

            float initialBgmSetting = hub.BgmVolume;
            float initialMasterSetting = hub.MasterVolume;
            float initialSfxSetting = hub.SfxVolume;
            float unduckedBgm = hub.EffectiveBgmVolume;
            float unduckedAmb = hub.EffectiveAmbienceVolume;

            Assert.AreEqual(1f, hub.DuckGain, 0.0001f);
            Assert.AreEqual(unduckedBgm, hub.BgmSource.volume, 0.0001f);
            Assert.AreEqual(unduckedAmb, hub.AmbienceSource.volume, 0.0001f);
            Assert.IsFalse(hub.IsDucking);

            // Trigger via major combat beat (headshot/tag, multitag, capture, banter).
            Assert.IsTrue(hub.PlayHitConfirm(isHeadshot: true, isTag: false));
            Assert.IsTrue(hub.IsDucking);
            Assert.AreEqual(AudioHub.DefaultDuckDurationSeconds, hub.DuckHoldRemaining, 0.001f);

            // Ramp attack (0.05 s) reaches ~-4 dB target without mutating user settings.
            hub.Tick(0.05f);
            Assert.AreEqual(AudioHub.DuckTargetGain, hub.DuckGain, 0.01f, "Duck gain should reach -4 dB (~0.631)");
            Assert.AreEqual(unduckedBgm * hub.DuckGain, hub.BgmSource.volume, 0.001f);
            Assert.AreEqual(unduckedAmb * hub.DuckGain, hub.AmbienceSource.volume, 0.001f);
            Assert.AreEqual(initialBgmSetting, hub.BgmVolume, 0.0001f, "Ducking must never mutate user BgmVolume");
            Assert.AreEqual(initialMasterSetting, hub.MasterVolume, 0.0001f, "Ducking must never mutate user MasterVolume");
            Assert.AreEqual(initialSfxSetting, hub.SfxVolume, 0.0001f, "Ducking must never mutate user SfxVolume");
            Assert.IsTrue(hub.MusicEnabled);

            // Hold stays ducked through remaining 0.6 s window.
            hub.Tick(0.45f);
            Assert.AreEqual(AudioHub.DuckTargetGain, hub.DuckGain, 0.01f);

            // Release smoothly returns to 1.0 after hold completes.
            hub.Tick(0.20f); // 0.10 s finishes hold + 0.10 s partial release
            Assert.Greater(hub.DuckGain, AudioHub.DuckTargetGain);
            Assert.Less(hub.DuckGain, 1f);

            hub.Tick(0.25f);
            Assert.AreEqual(1f, hub.DuckGain, 0.0001f);
            Assert.IsFalse(hub.IsDucking);
            Assert.AreEqual(unduckedBgm, hub.BgmSource.volume, 0.0001f);
            Assert.AreEqual(unduckedAmb, hub.AmbienceSource.volume, 0.0001f);

            // Verify MultiTag, Capture, and Banter also trigger combat ducking.
            Assert.IsTrue(hub.PlayMultiTagFanfare(streakCount: 3));
            Assert.IsTrue(hub.IsDucking);
            hub.Tick(1.2f);
            Assert.IsFalse(hub.IsDucking);

            Assert.IsTrue(hub.PlayBanter());
            Assert.IsTrue(hub.IsDucking);
            hub.Tick(1.2f);
            Assert.IsFalse(hub.IsDucking);

            hub.PlayObjectiveEvent("capture");
            Assert.IsTrue(hub.IsDucking);
        }

        [Test]
        public void BarangayAmbience_PlayModeOnlyAndAutoMutedWhenMusicOrSoundsOff()
        {
            AudioHub hub = CreateHub(prewarm: false);

            // EditMode guard: PlayAmbience must be a PlayMode-only no-op in headless EditMode.
            Assert.IsFalse(hub.PlayAmbience(), "Ambience playback must remain PlayMode-only");
            Assert.AreEqual(AudioHub.AmbienceBaseGain, hub.EffectiveAmbienceVolume, 0.0001f, "Ambience defaults to -22 dB");
            Assert.IsFalse(hub.AmbienceSource.mute);

            hub.SetSfxEnabled(false);
            Assert.AreEqual(0f, hub.EffectiveAmbienceVolume, 0.0001f);
            Assert.IsTrue(hub.AmbienceSource.mute, "Ambience must auto-mute when Sounds are off");

            hub.SetSfxEnabled(true);
            Assert.IsFalse(hub.AmbienceSource.mute);

            hub.SetMusicEnabled(false);
            Assert.AreEqual(0f, hub.EffectiveAmbienceVolume, 0.0001f);
            Assert.IsTrue(hub.AmbienceSource.mute, "Ambience must auto-mute when Music is off");

            hub.SetMusicEnabled(true);
            Assert.IsFalse(hub.AmbienceSource.mute);
        }

        [Test]
        public void AllProceduralFoleyUiAndAmbienceCues_GenerateValidFiniteNonSilentSamples()
        {
            for (int c = 0; c < NewProceduralCueIds.Length; c++)
            {
                string cueId = NewProceduralCueIds[c];
                Assert.IsTrue(AudioHub.TrySynthesizePcm(cueId, out float[] pcm, out int sampleRate), $"Failed to synthesize {cueId}");
                Assert.AreEqual(22050, sampleRate);
                Assert.IsNotNull(pcm);
                Assert.Greater(pcm.Length, 64, $"{cueId} sample buffer too short");

                float peak = 0f;
                double sumSq = 0.0;
                for (int i = 0; i < pcm.Length; i++)
                {
                    float s = pcm[i];
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), $"{cueId} has non-finite sample at {i}");
                    float abs = Mathf.Abs(s);
                    Assert.LessOrEqual(abs, 1f, $"{cueId} clipped beyond [-1, 1] at {i}");
                    if (abs > peak) peak = abs;
                    sumSq += s * (double)s;
                }

                float rms = (float)Math.Sqrt(sumSq / pcm.Length);
                Assert.Greater(peak, 0.05f, $"{cueId} peak amplitude is too quiet / silent ({peak})");
                Assert.Greater(rms, 0.005f, $"{cueId} RMS energy is too quiet / silent ({rms})");

                if (cueId == AudioHub.AmbienceDayCueId)
                {
                    // Seamless 4 s loop continuity check: boundary jump must not click.
                    Assert.AreEqual(22050 * 4, pcm.Length, "Barangay ambience loop must be 4 seconds");
                    float boundaryDelta = Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]);
                    Assert.Less(boundaryDelta, 0.05f, $"Barangay ambience loop boundary discontinuity too large ({boundaryDelta})");
                }
                else
                {
                    // Fade-in/fade-out click guard on one-shot cues.
                    Assert.AreEqual(0f, pcm[0], 0.0001f, $"{cueId} must fade in from 0");
                    Assert.Less(Mathf.Abs(pcm[pcm.Length - 1]), 0.05f, $"{cueId} must fade out cleanly");
                }

                AudioClip clip = AudioHub.GetSynthClip(cueId);
                Assert.IsNotNull(clip, $"AudioClip.Create failed for {cueId}");
                Assert.AreEqual(pcm.Length, clip.samples, $"{cueId} AudioClip sample count mismatch");
                Assert.Greater(clip.length, 0f, $"{cueId} AudioClip has zero length");
            }

            // UI hover tick & play confirm convenience methods work on the 2D UI bus.
            AudioHub hub = CreateHub(prewarm: false);
            Assert.IsTrue(hub.PlayUiHoverTick());
            Assert.IsTrue(hub.PlayUiPlayConfirm());
            Assert.IsTrue(hub.PlayUiTap("hover"));
            Assert.IsTrue(hub.PlayUiTap("play"));
            Assert.IsTrue(hub.PlayFootstep(inWater: false, new Vector3(4f, 0f, 2f)));
            Assert.IsTrue(hub.PlayFootstep(inWater: true, new Vector3(6f, 0f, -3f)));
            Assert.IsTrue(hub.PlaySlideSkid(new Vector3(1f, 0f, 5f)));
            Assert.IsTrue(hub.PlayLandThud(new Vector3(2f, 0f, 1f)));
        }

        [Test]
        public void ProjectileViews_DoesNotDuplicateWeaponCastSfx()
        {
            string path = Path.Combine(ProjectRoot(), "Assets", "Kalyeah", "View", "Combat", "ProjectileViews.cs");
            Assert.IsTrue(File.Exists(path), "ProjectileViews.cs not found at " + path);
            string src = File.ReadAllText(path);

            Assert.AreEqual(
                0,
                Regex.Matches(src, @"PlayWeaponCast\s*\(").Count,
                "ProjectileViews.cs must not call Audio.PlayWeaponCast (MatchBoot.cs owns cast SFX; calling twice burns 2 pooled voices)");
        }

        [Test]
        public void AudioPostprocessor_EnforcesStreamingBgmAndMonoAndroid22kSfxAndDspHelper()
        {
            Type ppType = FindEditorType("KalyeahAudioPostprocessor");
            Assert.IsNotNull(ppType, "KalyeahAudioPostprocessor must compile in Kalyeah.Editor");
            Assert.IsTrue(typeof(AssetPostprocessor).IsAssignableFrom(ppType), "KalyeahAudioPostprocessor must inherit AssetPostprocessor");

            MethodInfo classify = ppType.GetMethod("ClassifyClip", BindingFlags.Static | BindingFlags.Public);
            MethodInfo buildSettings = ppType.GetMethod("BuildSampleSettings", BindingFlags.Static | BindingFlags.Public);
            MethodInfo normalizeDsp = ppType.GetMethod("NormalizeDspBufferSize", BindingFlags.Static | BindingFlags.Public);
            MethodInfo configureDsp = ppType.GetMethod("ConfigureMobileDspBuffer", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(classify);
            Assert.IsNotNull(buildSettings);
            Assert.IsNotNull(normalizeDsp);
            Assert.IsNotNull(configureDsp);

            // BGM and >10 s clips -> Streaming Vorbis 0.70, stereo preserved.
            object bgmCat = classify.Invoke(null, new object[] { "Assets/Imported/Resources/audio/bgm.mp3", -1f, 5443759L });
            Assert.AreEqual("StreamingMusic", bgmCat.ToString());

            object[] bgmArgs = { bgmCat, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings), false, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings) };
            buildSettings.Invoke(null, bgmArgs);
            Assert.IsFalse((bool)bgmArgs[3], "BGM must remain stereo (forceToMono=false)");
            var bgmAndroid = (AudioImporterSampleSettings)bgmArgs[5];
            Assert.AreEqual(AudioClipLoadType.Streaming, bgmAndroid.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, bgmAndroid.compressionFormat);
            Assert.AreEqual(0.70f, bgmAndroid.quality, 0.001f);

            // Short SFX under sfx/ -> Mono DecompressOnLoad ADPCM + Android 22050 Hz override.
            object sfxCat = classify.Invoke(null, new object[] { "Assets/Imported/Resources/audio/sfx/tsinelas_hit.mp3", 0.7f, 9454L });
            Assert.AreEqual("ShortSfx", sfxCat.ToString());

            object[] sfxArgs = { sfxCat, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings), false, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings) };
            buildSettings.Invoke(null, sfxArgs);
            Assert.IsTrue((bool)sfxArgs[3], "Short 3D SFX must forceToMono=true");
            var sfxAndroid = (AudioImporterSampleSettings)sfxArgs[5];
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, sfxAndroid.loadType);
            Assert.AreEqual(AudioCompressionFormat.ADPCM, sfxAndroid.compressionFormat);
            Assert.AreEqual(AudioSampleRateSetting.OverrideSampleRate, sfxAndroid.sampleRateSetting);
            Assert.AreEqual(22050u, sfxAndroid.sampleRateOverride);

            // Ultra-short (<0.5 s) UI clicks -> Mono DecompressOnLoad PCM + Android 22050 Hz override.
            object uiCat = classify.Invoke(null, new object[] { "Assets/Imported/Resources/audio/sfx/ui_click.wav", 0.12f, 5200L });
            Assert.AreEqual("UltraShortSfx", uiCat.ToString());

            object[] uiArgs = { uiCat, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings), false, default(AudioImporterSampleSettings), default(AudioImporterSampleSettings) };
            buildSettings.Invoke(null, uiArgs);
            Assert.IsTrue((bool)uiArgs[3]);
            var uiAndroid = (AudioImporterSampleSettings)uiArgs[5];
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, uiAndroid.loadType);
            Assert.AreEqual(AudioCompressionFormat.PCM, uiAndroid.compressionFormat);
            Assert.AreEqual(AudioSampleRateSetting.OverrideSampleRate, uiAndroid.sampleRateSetting);
            Assert.AreEqual(22050u, uiAndroid.sampleRateOverride);

            // Non-audio paths are ignored.
            object ignoredCat = classify.Invoke(null, new object[] { "Assets/Imported/Resources/ui/main_bg.png", -1f, 1024L });
            Assert.AreEqual("Ignore", ignoredCat.ToString());

            // Pure mobile DSP normalization check (avoids mutating AudioManager.asset or resetting FMOD mid-EditMode test run).
            Assert.AreEqual(512, (int)normalizeDsp.Invoke(null, new object[] { 0 }));
            Assert.AreEqual(512, (int)normalizeDsp.Invoke(null, new object[] { 128 }));
            Assert.AreEqual(256, (int)normalizeDsp.Invoke(null, new object[] { 256 }));
            Assert.AreEqual(512, (int)normalizeDsp.Invoke(null, new object[] { 512 }));
            Assert.AreEqual(1024, (int)normalizeDsp.Invoke(null, new object[] { 1024 }));
            Assert.AreEqual(1024, (int)normalizeDsp.Invoke(null, new object[] { 4096 }));
            Assert.AreEqual(typeof(bool), configureDsp.ReturnType);
        }

        [Test]
        public void AudioOwnedFiles_HaveLfLineEndingsAndUniqueMetaGuids()
        {
            string root = ProjectRoot();
            string[] ownedRelative =
            {
                "Assets/Kalyeah/View/Audio/AudioHub.cs",
                "Assets/Kalyeah/View/Combat/ProjectileViews.cs",
                "Assets/Kalyeah/Editor/KalyeahAudioPostprocessor.cs",
                "Assets/Kalyeah/Editor/KalyeahAudioPostprocessor.cs.meta",
                "Assets/Kalyeah/Tests/EditMode/AudioArchitectureTests.cs",
                "Assets/Kalyeah/Tests/EditMode/AudioArchitectureTests.cs.meta",
            };

            for (int i = 0; i < ownedRelative.Length; i++)
            {
                string fullPath = Path.Combine(root, ownedRelative[i].Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(fullPath), "Expected file missing: " + ownedRelative[i]);
                byte[] bytes = File.ReadAllBytes(fullPath);
                for (int b = 0; b < bytes.Length; b++)
                    Assert.AreNotEqual((byte)'\r', bytes[b], "CRLF byte found in " + ownedRelative[i]);
            }

            // Verify zero GUID collisions across all Assets/**/*.meta files.
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string assetsDir = Path.Combine(root, "Assets");
            string[] metas = Directory.GetFiles(assetsDir, "*.meta", SearchOption.AllDirectories);
            var guidRx = new Regex(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);
            for (int i = 0; i < metas.Length; i++)
            {
                Match m = guidRx.Match(File.ReadAllText(metas[i]));
                if (!m.Success) continue;
                string guid = m.Groups[1].Value;
                if (seen.TryGetValue(guid, out string existing))
                    Assert.Fail($"Duplicate .meta GUID {guid}: {existing} and {metas[i]}");
                seen[guid] = metas[i];
            }
        }
    }
}
