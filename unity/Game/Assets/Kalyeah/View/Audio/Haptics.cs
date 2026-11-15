#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;
#endif

namespace Kalyeah.Game
{
    /// <summary>
    /// Mobile tactile feedback via Android Vibrator + VibrationEffect JNI on device, with safe zero-cost no-ops in the
    /// Unity Editor, EditMode/PlayMode desktop runners, and Linux64 Dedicated Server. Respects the user's Vibration toggle.
    /// </summary>
    public static class Haptics
    {
        /// <summary>User preference bound from SettingsStore ("vibration" switch, default true).</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>True only on physical Android devices that report a hardware vibrator.</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                EnsureInitialized();
                return s_supported;
#else
                return false;
#endif
            }
        }

        /// <summary>Crisp 10 ms subtle tick for button presses, card picks, and radial drag entry.</summary>
        public static void LightTap()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Enabled) return;
            EnsureInitialized();
            Trigger(s_lightTapEffect, 10L);
#endif
        }

        /// <summary>Snappy 22 ms tactile pulse confirming a landed hit on an opponent.</summary>
        public static void HitConfirm()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Enabled) return;
            EnsureInitialized();
            Trigger(s_hitConfirmEffect, 22L);
#endif
        }

        /// <summary>Full-amplitude 42 ms impact for heavy slams (Bagsak, Bato meteor, heavy Tsinelas, getting tagged).</summary>
        public static void HeavyImpact()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Enabled) return;
            EnsureInitialized();
            Trigger(s_heavyImpactEffect, 42L);
#endif
        }

        /// <summary>Distinct 16 ms medium tick when cancelling an ability drag or tapping an unavailable slot.</summary>
        public static void CancelTick()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Enabled) return;
            EnsureInitialized();
            Trigger(s_cancelTickEffect, 16L);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool s_initialized;
        static bool s_supported;
        static AndroidJavaObject s_vibrator;
        static IntPtr s_vibrateEffectMethod;
        static IntPtr s_vibrateMillisMethod;
        static AndroidJavaObject s_lightTapEffect;
        static AndroidJavaObject s_hitConfirmEffect;
        static AndroidJavaObject s_heavyImpactEffect;
        static AndroidJavaObject s_cancelTickEffect;
        static readonly jvalue[] s_oneArg = new jvalue[1];

        static void EnsureInitialized()
        {
            if (s_initialized) return;
            s_initialized = true;

            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) return;
                    s_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (s_vibrator == null || !s_vibrator.Call<bool>("hasVibrator")) return;
                }

                // Resolve exact JNI method IDs on android.os.Vibrator directly so Android 12+ VibrationEffect$Composed
                // subclasses never fail signature lookup and mid-match triggers allocate 0 B.
                IntPtr vibratorClass = AndroidJNI.FindClass("android/os/Vibrator");
                if (vibratorClass != IntPtr.Zero)
                {
                    s_vibrateEffectMethod = AndroidJNI.GetMethodID(vibratorClass, "vibrate", "(Landroid/os/VibrationEffect;)V");
                    ClearJniException();
                    s_vibrateMillisMethod = AndroidJNI.GetMethodID(vibratorClass, "vibrate", "(J)V");
                    ClearJniException();
                    AndroidJNI.DeleteLocalRef(vibratorClass);
                }

                using (var buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = buildVersion.GetStatic<int>("SDK_INT");
                    if (sdkInt >= 26 && s_vibrateEffectMethod != IntPtr.Zero)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        {
                            s_lightTapEffect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 10L, 70);
                            s_cancelTickEffect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 16L, 115);
                            s_hitConfirmEffect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 22L, 170);
                            s_heavyImpactEffect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 42L, 255);
                        }
                    }
                }

                s_supported = s_vibrateEffectMethod != IntPtr.Zero || s_vibrateMillisMethod != IntPtr.Zero;
            }
            catch
            {
                ClearJniException();
                s_supported = false;
            }
        }

        static void Trigger(AndroidJavaObject effect, long fallbackMs)
        {
            if (!s_supported || s_vibrator == null) return;
            IntPtr vibratorPtr = s_vibrator.GetRawObject();
            if (vibratorPtr == IntPtr.Zero) return;

            if (effect != null && s_vibrateEffectMethod != IntPtr.Zero)
            {
                IntPtr effectPtr = effect.GetRawObject();
                if (effectPtr != IntPtr.Zero)
                {
                    s_oneArg[0].l = effectPtr;
                    AndroidJNI.CallVoidMethod(vibratorPtr, s_vibrateEffectMethod, s_oneArg);
                    if (!ClearJniException()) return;
                }
            }

            if (s_vibrateMillisMethod != IntPtr.Zero)
            {
                s_oneArg[0].j = fallbackMs;
                AndroidJNI.CallVoidMethod(vibratorPtr, s_vibrateMillisMethod, s_oneArg);
                ClearJniException();
            }
        }

        static bool ClearJniException()
        {
            IntPtr ex = AndroidJNI.ExceptionOccurred();
            if (ex == IntPtr.Zero) return false;
            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(ex);
            return true;
        }
#endif
    }
}
