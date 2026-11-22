using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Enforces Kalyeah's Android audio memory and CPU import budgets (<c>optimize-audio</c>):
/// streams BGM and long clips (&gt; 10 s) as Vorbis at quality 0.70 to avoid ~48 MB PCM expansion,
/// forces short SFX (&lt;= 10 s under <c>sfx/</c>) to mono DecompressOnLoad ADPCM (PCM for ultra-short
/// &lt; 0.5 s UI clicks) with Android 22050 Hz override, and provides an Editor DSP buffer helper (512 Good Latency).
/// </summary>
public sealed class KalyeahAudioPostprocessor : AssetPostprocessor
{
    public enum AudioImportCategory
    {
        Ignore = 0,
        StreamingMusic = 1,
        UltraShortSfx = 2,
        ShortSfx = 3,
    }

    public const float LongClipThresholdSeconds = 10f;
    public const float UltraShortSfxThresholdSeconds = 0.5f;
    public const float BgmVorbisQuality = 0.70f;
    public const uint AndroidSfxSampleRate = 22050u;
    public const int MobileDspBufferSize = 512;
    public const string AndroidPlatformName = "Android";

    const long LongCompressedByteThreshold = 240000L;
    const long UltraShortCompressedByteThreshold = 12000L;

    void OnPreprocessAudio()
    {
        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null || string.IsNullOrEmpty(assetPath))
            return;

        long fileBytes = TryReadFileSize(assetPath);
        float knownDuration = TryReadAudioDurationSeconds(assetPath);
        AudioImportCategory category = ClassifyClip(assetPath, knownDuration, fileBytes);
        if (category == AudioImportCategory.Ignore)
            return;

        AudioImporterSampleSettings baseDefault = importer.defaultSampleSettings;
        AudioImporterSampleSettings baseAndroid = importer.GetOverrideSampleSettings(AndroidPlatformName);

        BuildSampleSettings(
            category,
            baseDefault,
            baseAndroid,
            out bool forceToMono,
            out AudioImporterSampleSettings defaultSettings,
            out AudioImporterSampleSettings androidSettings);

        importer.forceToMono = forceToMono;
        importer.loadInBackground = false;
        importer.defaultSampleSettings = defaultSettings;
        importer.SetOverrideSampleSettings(AndroidPlatformName, androidSettings);
    }

    /// <summary>Classifies an audio asset path into StreamingMusic, UltraShortSfx, ShortSfx, or Ignore.</summary>
    public static AudioImportCategory ClassifyClip(string assetPath, float knownDurationSeconds = -1f, long fileBytes = -1L)
    {
        if (string.IsNullOrEmpty(assetPath))
            return AudioImportCategory.Ignore;

        string normalized = assetPath.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return AudioImportCategory.Ignore;

        if (normalized.IndexOf("/audio/", StringComparison.OrdinalIgnoreCase) < 0)
            return AudioImportCategory.Ignore;

        string ext = Path.GetExtension(normalized);
        if (!IsSupportedAudioExtension(ext))
            return AudioImportCategory.Ignore;

        string fileName = Path.GetFileNameWithoutExtension(normalized);
        bool isMusicPath = fileName.StartsWith("bgm", StringComparison.OrdinalIgnoreCase)
                        || fileName.StartsWith("amb_", StringComparison.OrdinalIgnoreCase)
                        || fileName.StartsWith("music_", StringComparison.OrdinalIgnoreCase)
                        || normalized.IndexOf("/bgm/", StringComparison.OrdinalIgnoreCase) >= 0
                        || normalized.IndexOf("/music/", StringComparison.OrdinalIgnoreCase) >= 0
                        || normalized.IndexOf("/ambience/", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isMusicPath
            || knownDurationSeconds > LongClipThresholdSeconds
            || (knownDurationSeconds <= 0f && fileBytes >= LongCompressedByteThreshold))
        {
            return AudioImportCategory.StreamingMusic;
        }

        bool isUnderSfx = normalized.IndexOf("/sfx/", StringComparison.OrdinalIgnoreCase) >= 0
                       || normalized.IndexOf("/ui/", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!isUnderSfx && knownDurationSeconds <= 0f && fileBytes <= 0L)
            return AudioImportCategory.ShortSfx;

        bool isUiClickName = fileName.StartsWith("ui_", StringComparison.OrdinalIgnoreCase)
                          || fileName.StartsWith("card_", StringComparison.OrdinalIgnoreCase)
                          || fileName.IndexOf("click", StringComparison.OrdinalIgnoreCase) >= 0
                          || fileName.IndexOf("tick", StringComparison.OrdinalIgnoreCase) >= 0
                          || fileName.IndexOf("tap", StringComparison.OrdinalIgnoreCase) >= 0;

        bool isUltraShortDuration = knownDurationSeconds > 0f
            ? knownDurationSeconds < UltraShortSfxThresholdSeconds
            : (fileBytes > 0L && fileBytes <= UltraShortCompressedByteThreshold);

        if (isUiClickName && isUltraShortDuration)
            return AudioImportCategory.UltraShortSfx;

        return AudioImportCategory.ShortSfx;
    }

    /// <summary>Builds default and Android sample settings matching optimize-audio guidelines for <paramref name="category"/>.</summary>
    public static void BuildSampleSettings(
        AudioImportCategory category,
        AudioImporterSampleSettings baseDefault,
        AudioImporterSampleSettings baseAndroid,
        out bool forceToMono,
        out AudioImporterSampleSettings defaultSettings,
        out AudioImporterSampleSettings androidSettings)
    {
        defaultSettings = baseDefault;
        androidSettings = baseAndroid;

        switch (category)
        {
            case AudioImportCategory.StreamingMusic:
                forceToMono = false;
                defaultSettings.loadType = AudioClipLoadType.Streaming;
                defaultSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                defaultSettings.quality = BgmVorbisQuality;
                defaultSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

                androidSettings.loadType = AudioClipLoadType.Streaming;
                androidSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                androidSettings.quality = BgmVorbisQuality;
                androidSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                break;

            case AudioImportCategory.UltraShortSfx:
                forceToMono = true;
                defaultSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                defaultSettings.compressionFormat = AudioCompressionFormat.PCM;
                defaultSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

                androidSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                androidSettings.compressionFormat = AudioCompressionFormat.PCM;
                androidSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                androidSettings.sampleRateOverride = AndroidSfxSampleRate;
                break;

            default:
                forceToMono = true;
                defaultSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                defaultSettings.compressionFormat = AudioCompressionFormat.ADPCM;
                defaultSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

                androidSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                androidSettings.compressionFormat = AudioCompressionFormat.ADPCM;
                androidSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                androidSettings.sampleRateOverride = AndroidSfxSampleRate;
                break;
        }
    }

    /// <summary>Clamps/normalizes requested mobile DSP buffer size into Unity's safe [256..1024] sample range (defaulting to 512 Good Latency).</summary>
    public static int NormalizeDspBufferSize(int requestedSamples)
    {
        if (requestedSamples < 256) return MobileDspBufferSize;
        if (requestedSamples > 1024) return 1024;
        return requestedSamples;
    }

    /// <summary>
    /// Ensures Unity's mobile DSP buffer size is pinned to safe Good Latency (512 samples) in
    /// <c>ProjectSettings/AudioManager.asset</c> and (when not in headless batchmode) the active
    /// <see cref="AudioSettings"/> configuration.
    /// </summary>
    [MenuItem("Kalyeah/Audio/Configure Mobile DSP Buffer (512)")]
    static void ConfigureMobileDspBufferMenu()
    {
        ConfigureMobileDspBuffer(MobileDspBufferSize);
    }

    public static bool ConfigureMobileDspBuffer(int dspBufferSize = MobileDspBufferSize)
    {
        int safeBuffer = NormalizeDspBufferSize(dspBufferSize);
        bool persisted = false;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
        if (assets != null && assets.Length > 0 && assets[0] != null)
        {
            SerializedObject audioManager = new SerializedObject(assets[0]);
            SerializedProperty dspProp = audioManager.FindProperty("m_DSPBufferSize");
            if (dspProp != null)
            {
                if (dspProp.intValue != safeBuffer)
                {
                    dspProp.intValue = safeBuffer;
                    audioManager.ApplyModifiedPropertiesWithoutUndo();
                }
                persisted = true;
            }
        }

        if (!Application.isBatchMode)
        {
            try
            {
                AudioConfiguration config = AudioSettings.GetConfiguration();
                if (config.dspBufferSize != safeBuffer)
                {
                    config.dspBufferSize = safeBuffer;
                    AudioSettings.Reset(config);
                }
            }
            catch
            {
                // Audio backend may disallow live DSP reset on headless or muted hosts.
            }
        }

        return persisted;
    }

    static bool IsSupportedAudioExtension(string ext)
    {
        return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aiff", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);
    }

    static long TryReadFileSize(string assetPath)
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return -1L;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) ? new FileInfo(fullPath).Length : -1L;
        }
        catch
        {
            return -1L;
        }
    }

    static float TryReadAudioDurationSeconds(string assetPath)
    {
        // Pure filesystem RIFF/WAV header parse — never calls AssetDatabase.LoadAssetAtPath mid-import (Import Worker V2 safe).
        if (assetPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return TryReadWavDurationSeconds(assetPath);
        return -1f;
    }

    static float TryReadWavDurationSeconds(string assetPath)
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return -1f;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) return -1f;

            using (FileStream fs = File.OpenRead(fullPath))
            using (BinaryReader br = new BinaryReader(fs))
            {
                if (fs.Length < 44L) return -1f;
                if (br.ReadUInt32() != 0x46464952u) return -1f; // "RIFF"
                br.ReadUInt32();
                if (br.ReadUInt32() != 0x45564157u) return -1f; // "WAVE"

                int byteRate = 0;
                int dataBytes = 0;
                while (fs.Position + 8L <= fs.Length)
                {
                    uint chunkId = br.ReadUInt32();
                    int chunkSize = br.ReadInt32();
                    if (chunkSize < 0) break;

                    int paddedSize = chunkSize + (chunkSize & 1);
                    if (chunkId == 0x20746D66u && chunkSize >= 16) // "fmt "
                    {
                        br.ReadUInt16(); // format
                        br.ReadUInt16(); // channels
                        br.ReadUInt32(); // sampleRate
                        byteRate = br.ReadInt32();
                        fs.Seek(paddedSize - 12, SeekOrigin.Current);
                    }
                    else if (chunkId == 0x61746164u) // "data"
                    {
                        dataBytes = chunkSize;
                        break;
                    }
                    else
                    {
                        fs.Seek(paddedSize, SeekOrigin.Current);
                    }
                }

                if (byteRate > 0 && dataBytes > 0)
                    return dataBytes / (float)byteRate;
            }
        }
        catch
        {
            return -1f;
        }
        return -1f;
    }
}
