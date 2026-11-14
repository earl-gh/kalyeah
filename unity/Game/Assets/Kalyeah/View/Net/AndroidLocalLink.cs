using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Kalyeah.Net;
using Kalyeah.Sim;
using UnityEngine;

namespace Kalyeah.Game
{
    /// <summary>
    /// Android JNI Wi-Fi/Hotspot IPv4, multicast lock, permission, and link helper (port of scripts/net/local_link.gd)
    /// with engine-free <see cref="HotspotNet"/> fallback so Editor and Desktop return LAN IPv4 addresses cleanly.
    /// </summary>
    public static class AndroidLocalLink
    {
        public const int DefaultWsPort = 8080;
        public const int DefaultDiscoveryPort = 8081;
        public const string RoomQrPrefix = "KALYEAH:";
        public const int DefaultPasscodeLength = 8;
        public const string DefaultPasscodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        static readonly string[] DefaultIgnoredPrefixes =
        {
            "lo", "rmnet", "ccmni", "pdp", "wwan", "seth", "v4-",
            "ppp", "tun", "tap", "wg", "ipsec", "dummy", "p2p",
        };

        static readonly string[] DefaultClientInterfaces =
        {
            "wlan0", "eth0",
        };

        static readonly string[] DefaultFallbackHosts =
        {
            "192.168.43.1", "172.20.10.1",
        };

        public static event Action<bool> PermissionsAnswered;
        public static event Action<WifiQr.Network> HotspotChanged;

        /// <summary>Optional test override for network interface discovery.</summary>
        public static Func<IReadOnlyList<NetInterface>> InterfaceProvider { get; set; }

        /// <summary>Delegate matching <c>Func&lt;string&gt;</c> for UI screens needing the local LAN/Hotspot IPv4.</summary>
        public static Func<string> DefaultLanIpProvider { get; } = GetLanIpv4;

        public static bool IsAndroidDevice
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        static WifiQr.Network s_activeHotspot;

        /// <summary>Known active game hotspot credentials when shared via Wi-Fi QR, or null.</summary>
        public static WifiQr.Network ActiveHotspot
        {
            get => s_activeHotspot;
            set
            {
                s_activeHotspot = value;
                HotspotChanged?.Invoke(s_activeHotspot);
            }
        }

        public static bool HasActiveHotspot =>
            s_activeHotspot != null && !string.IsNullOrEmpty(s_activeHotspot.Ssid);

        /// <summary>Builds a default <see cref="NetRules"/> matching <c>data/rules/net_rules.tres</c>.</summary>
        public static NetRules DefaultNetRules()
        {
            var rules = new NetRules
            {
                PasscodeLength = DefaultPasscodeLength,
                PasscodeAlphabet = DefaultPasscodeAlphabet,
                ReconnectWindow = 60.0,
                AfkAfter = 30.0,
                SnapshotRate = 20,
                InterpolationDelay = 0.1,
                LoadTimeout = 15.0,
                PickGrace = 1.0,
                MaxQueuedInputs = 6,
                MaxNameLength = 12,
                DefaultPort = DefaultWsPort,
                ConnectTimeout = 75.0,
                WakeHintAfter = 4.0,
                RoomListMax = 30,
                BaseRoomMinTeam = 1,
                ChatStickers = 6,
                ChatMinGap = 1.0,
                RoomListMinGap = 1.0,
                RoomListRefresh = 2.0,
                DiscoveryPort = DefaultDiscoveryPort,
                DiscoverySeconds = 5.0,
                DiscoveryInterval = 1.0,
                DiscoveryBurst = 8,
                JoinWifiGrace = 5.0,
                JoinWifiRecheck = 0.5,
            };
            rules.IgnoredInterfacePrefixes.AddRange(DefaultIgnoredPrefixes);
            rules.ClientInterfaceNames.AddRange(DefaultClientInterfaces);
            rules.FallbackHosts.AddRange(DefaultFallbackHosts);
            return rules;
        }

        /// <summary>Loads <see cref="NetRules"/> from <paramref name="data"/> when available, or returns <see cref="DefaultNetRules"/>.</summary>
        public static NetRules LoadNetRules(DataSource data)
        {
            if (data == null) return DefaultNetRules();
            try
            {
                SimData simData = AppSession.Current.EnsureSimData(data);
                NetRules loaded = NetRules.FromData(simData);
                if (loaded.DefaultPort > 0) return loaded;
            }
            catch
            {
                // Fall back to canonical built-in net_rules constants if JSON resource is absent in standalone tests.
            }
            return DefaultNetRules();
        }

        /// <summary>Enumerates active IPv4 interfaces via Android JNI on device, with <see cref="NetInterface.FromSystem"/> fallback.</summary>
        public static List<NetInterface> GetInterfaces()
        {
            if (InterfaceProvider != null)
            {
                IReadOnlyList<NetInterface> custom = InterfaceProvider();
                var copy = new List<NetInterface>(custom != null ? custom.Count : 0);
                if (custom != null)
                {
                    for (int i = 0; i < custom.Count; i++)
                        if (custom[i] != null) copy.Add(custom[i]);
                }
                return copy;
            }

            var combined = new List<NetInterface>();
#if UNITY_ANDROID && !UNITY_EDITOR
            TryCollectAndroidJniInterfaces(combined);
#endif
            try
            {
                List<NetInterface> sys = NetInterface.FromSystem();
                for (int i = 0; i < sys.Count; i++)
                    MergeInterface(combined, sys[i].Name, sys[i].Addresses);
            }
            catch
            {
                // Android SELinux or restricted sandbox may block .NET netlink enumeration; JNI results remain intact.
            }
            return combined;
        }

        /// <summary>Returns the best private LAN or Hotspot tether IPv4 address, or <c>"127.0.0.1"</c> if offline.</summary>
        public static string GetLanIpv4() => GetLanIpv4(DefaultNetRules());

        public static string GetLanIpv4(DataSource data) => GetLanIpv4(LoadNetRules(data));

        public static string GetLanIpv4(NetRules rules)
        {
            NetRules activeRules = rules ?? DefaultNetRules();
            List<NetInterface> interfaces = GetInterfaces();

            // 1. Prefer active tether/hotspot interfaces (e.g., ap0, swlan0, wlan1 on Android, or active LAN NICs on Desktop).
            List<string> tetherIps = HotspotNet.AddressesOf(interfaces, HotspotNet.Role.Tether, activeRules);
            if (tetherIps.Count > 0) return tetherIps[0];

            // 2. Next, prefer connected Wi-Fi/Ethernet client interfaces (wlan0, eth0).
            List<string> clientIps = HotspotNet.AddressesOf(interfaces, HotspotNet.Role.Client, activeRules);
            if (clientIps.Count > 0) return clientIps[0];

            // 3. Any remaining private IPv4 address across non-loopback interfaces.
            for (int i = 0; i < interfaces.Count; i++)
            {
                NetInterface nic = interfaces[i];
                if (nic == null || string.Equals(nic.Name, "lo", StringComparison.OrdinalIgnoreCase)) continue;
                for (int j = 0; j < nic.Addresses.Count; j++)
                {
                    string ip = nic.Addresses[j];
                    if (!string.IsNullOrEmpty(ip) && HotspotNet.IsPrivate(ip))
                        return ip;
                }
            }

            return "127.0.0.1";
        }

        /// <summary>Formats the full LAN WebSocket URL (<c>ws://&lt;lan-ip&gt;:&lt;port&gt;</c>) for Hotspot/LAN hosts.</summary>
        public static string GetHostWsUrl(int port = DefaultWsPort)
        {
            int actualPort = port > 0 ? port : DefaultWsPort;
            return "ws://" + GetLanIpv4() + ":" + actualPort.ToString(CultureInfo.InvariantCulture);
        }

        public static string GetHostWsUrl(DataSource data, int port = 0)
        {
            NetRules rules = LoadNetRules(data);
            int actualPort = port > 0 ? port : (rules.DefaultPort > 0 ? rules.DefaultPort : DefaultWsPort);
            return "ws://" + GetLanIpv4(rules) + ":" + actualPort.ToString(CultureInfo.InvariantCulture);
        }

        public static bool IsHotspotActive(NetRules rules = null) =>
            HotspotNet.HotspotOn(GetInterfaces(), rules ?? DefaultNetRules());

        public static bool IsWifiConnected(NetRules rules = null) =>
            HotspotNet.WifiConnected(GetInterfaces(), rules ?? DefaultNetRules());

        public static HotspotNet.Plan BuildDiscoveryPlan(DataSource data = null) =>
            HotspotNet.SearchPlan(GetInterfaces(), LoadNetRules(data));

        public static string ShareWifiQrText() =>
            HasActiveHotspot ? WifiQr.Build(s_activeHotspot) : "";

        public static string BuildWifiQr(string ssid, string password, string security = "WPA")
        {
            if (string.IsNullOrWhiteSpace(ssid)) return "";
            return WifiQr.Build(new WifiQr.Network
            {
                Ssid = ssid.Trim(),
                Password = password ?? "",
                Security = string.IsNullOrWhiteSpace(security) ? "WPA" : security.Trim(),
            });
        }

        public static WifiQr.Network ParseWifiQr(string text) => WifiQr.Parse(text);

        public static string BuildRoomQr(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return "";
            return RoomQrPrefix + RoomService.StripEdges(roomCode).ToUpperInvariant();
        }

        /// <summary>Extracts an 8-char room code from <c>KALYEAH:XXXXXXXX</c> or a bare room code string, or <c>""</c>.</summary>
        public static string ParseRoomCode(string text, NetRules rules = null)
        {
            string clean = RoomService.StripEdges(text ?? "").ToUpperInvariant();
            if (clean.Length == 0) return "";
            if (clean.StartsWith(RoomQrPrefix, StringComparison.Ordinal))
                clean = RoomService.StripEdges(clean.Substring(RoomQrPrefix.Length));

            NetRules r = rules ?? DefaultNetRules();
            int expectedLen = r.PasscodeLength > 0 ? r.PasscodeLength : DefaultPasscodeLength;
            string alphabet = !string.IsNullOrEmpty(r.PasscodeAlphabet) ? r.PasscodeAlphabet : DefaultPasscodeAlphabet;
            if (clean.Length != expectedLen) return "";
            for (int i = 0; i < clean.Length; i++)
                if (alphabet.IndexOf(clean[i]) < 0) return "";
            return clean;
        }

        /// <summary>
        /// Classifies a scanned QR or manually pasted string into a Wi-Fi QR network, a room code, or a normalized <c>ws://</c>/<c>wss://</c> URL.
        /// </summary>
        public static bool TryClassifyJoinPayload(
            string raw,
            out string wsUrl,
            out string roomCode,
            out WifiQr.Network wifiNetwork,
            int defaultPort = DefaultWsPort)
        {
            wsUrl = "";
            roomCode = "";
            wifiNetwork = null;

            string trimmed = RoomService.StripEdges(raw ?? "");
            if (trimmed.Length == 0) return false;

            WifiQr.Network parsedWifi = WifiQr.Parse(trimmed);
            if (parsedWifi != null)
            {
                wifiNetwork = parsedWifi;
                return true;
            }

            string code = ParseRoomCode(trimmed);
            if (!string.IsNullOrEmpty(code))
            {
                roomCode = code;
                return true;
            }

            if (LooksLikeEndpoint(trimmed))
            {
                string candidate = trimmed;
                if (HotspotNet.IsPrivate(candidate))
                    candidate = candidate + ":" + (defaultPort > 0 ? defaultPort : DefaultWsPort).ToString(CultureInfo.InvariantCulture);
                string normalized = SettingsStore.NormalizeServerUrl(candidate);
                if (!string.IsNullOrEmpty(normalized))
                {
                    wsUrl = normalized;
                    return true;
                }
            }

            return false;
        }

        static bool LooksLikeEndpoint(string value)
        {
            if (value.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
                return true;
            return value.IndexOf('.') >= 0 || value.IndexOf(':') >= 0;
        }

        /// <summary>Acquires Android's <c>WifiManager.MulticastLock</c> so Hotspot UDP discovery packets arrive reliably.</summary>
        public static bool AcquireMulticastLock(string tag = "kalyeah_hotspot")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                ReleaseMulticastLock();
                using (AndroidJavaObject activity = GetCurrentActivity())
                {
                    if (activity == null) return false;
                    using (AndroidJavaObject appCtx = activity.Call<AndroidJavaObject>("getApplicationContext"))
                    using (AndroidJavaObject wifi = (appCtx ?? activity).Call<AndroidJavaObject>("getSystemService", "wifi"))
                    {
                        if (wifi == null) return false;
                        s_multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", string.IsNullOrEmpty(tag) ? "kalyeah_hotspot" : tag);
                        if (s_multicastLock == null) return false;
                        s_multicastLock.Call("setReferenceCounted", false);
                        s_multicastLock.Call("acquire");
                        return true;
                    }
                }
            }
            catch
            {
                s_multicastLock = null;
                return false;
            }
#else
            _ = tag;
            return false;
#endif
        }

        public static void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_multicastLock == null) return;
            try
            {
                if (s_multicastLock.Call<bool>("isHeld"))
                    s_multicastLock.Call("release");
            }
            catch
            {
                // Ignore release errors during app teardown.
            }
            finally
            {
                s_multicastLock.Dispose();
                s_multicastLock = null;
            }
#endif
        }

        public static bool HasCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CheckAndroidPermission("android.permission.CAMERA");
#else
            return true;
#endif
        }

        public static void RequestCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            RequestAndroidPermissions(new[] { "android.permission.CAMERA" });
#else
            PermissionsAnswered?.Invoke(true);
#endif
        }

        public static bool HasNearbyWifiPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int sdk = GetAndroidSdkInt();
            if (sdk >= 33)
                return CheckAndroidPermission("android.permission.NEARBY_WIFI_DEVICES");
            return CheckAndroidPermission("android.permission.ACCESS_FINE_LOCATION") &&
                   CheckAndroidPermission("android.permission.ACCESS_COARSE_LOCATION");
#else
            return true;
#endif
        }

        public static void RequestNearbyWifiPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int sdk = GetAndroidSdkInt();
            string[] perms = sdk >= 33
                ? new[] { "android.permission.NEARBY_WIFI_DEVICES" }
                : new[] { "android.permission.ACCESS_COARSE_LOCATION", "android.permission.ACCESS_FINE_LOCATION" };
            RequestAndroidPermissions(perms);
#else
            PermissionsAnswered?.Invoke(true);
#endif
        }

        public static bool OpenHotspotSettings() => OpenAndroidAction("android.settings.TETHER_SETTINGS") || OpenAndroidAction("android.settings.WIRELESS_SETTINGS");

        public static bool OpenWifiSettings() => OpenAndroidAction("android.settings.WIFI_SETTINGS");

        public static bool OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaObject activity = GetCurrentActivity())
                {
                    if (activity == null) return false;
                    string pkg = activity.Call<string>("getPackageName");
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", pkg, null))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                    {
                        activity.Call("startActivity", intent);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        static bool OpenAndroidAction(string action)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaObject activity = GetCurrentActivity())
                using (var intent = new AndroidJavaObject("android.content.Intent", action))
                {
                    if (activity == null) return false;
                    activity.Call("startActivity", intent);
                    return true;
                }
            }
            catch
            {
                return false;
            }
#else
            _ = action;
            return false;
#endif
        }

        static void MergeInterface(List<NetInterface> target, string name, IList<string> addresses)
        {
            if (string.IsNullOrEmpty(name) || addresses == null || addresses.Count == 0) return;
            NetInterface existing = null;
            for (int i = 0; i < target.Count; i++)
            {
                if (string.Equals(target[i].Name, name, StringComparison.Ordinal))
                {
                    existing = target[i];
                    break;
                }
            }
            if (existing == null)
            {
                existing = new NetInterface { Name = name };
                target.Add(existing);
            }
            for (int i = 0; i < addresses.Count; i++)
            {
                string ip = addresses[i];
                if (!string.IsNullOrEmpty(ip) && !existing.Addresses.Contains(ip))
                    existing.Addresses.Add(ip);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject s_multicastLock;

        static AndroidJavaObject GetCurrentActivity()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        static int GetAndroidSdkInt()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    return version.GetStatic<int>("SDK_INT");
            }
            catch
            {
                return 0;
            }
        }

        static bool CheckAndroidPermission(string permission)
        {
            try
            {
                if (GetAndroidSdkInt() < 23) return true;
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
            }
            catch
            {
                return false;
            }
        }

        static bool AllPermissionsGranted(string[] permissions)
        {
            for (int i = 0; i < permissions.Length; i++)
            {
                if (!CheckAndroidPermission(permissions[i]))
                    return false;
            }
            return true;
        }

        static void RequestAndroidPermissions(string[] permissions)
        {
            if (permissions == null || permissions.Length == 0 || GetAndroidSdkInt() < 23 || AllPermissionsGranted(permissions))
            {
                PermissionsAnswered?.Invoke(true);
                return;
            }

            try
            {
                int remaining = permissions.Length;
                bool allGranted = true;
                bool completed = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();

                void OnResult(bool granted)
                {
                    if (completed) return;
                    if (!granted) allGranted = false;
                    remaining--;
                    if (remaining <= 0)
                    {
                        completed = true;
                        PermissionsAnswered?.Invoke(allGranted);
                    }
                }

                callbacks.PermissionGranted += _ => OnResult(true);
                callbacks.PermissionDenied += _ => OnResult(false);
                UnityEngine.Android.Permission.RequestUserPermissions(permissions, callbacks);
            }
            catch
            {
                PermissionsAnswered?.Invoke(false);
            }
        }

        static void TryCollectAndroidJniInterfaces(List<NetInterface> output)
        {
            // 1. Enumerate java.net.NetworkInterface (captures ap0, swlan0, wlan1, wlan0, rndis0 on Android).
            try
            {
                using (var nicClass = new AndroidJavaClass("java.net.NetworkInterface"))
                using (AndroidJavaObject enumeration = nicClass.CallStatic<AndroidJavaObject>("getNetworkInterfaces"))
                {
                    while (enumeration != null && enumeration.Call<bool>("hasMoreElements"))
                    {
                        using (AndroidJavaObject nic = enumeration.Call<AndroidJavaObject>("nextElement"))
                        {
                            if (nic == null || nic.Call<bool>("isLoopback") || !nic.Call<bool>("isUp")) continue;
                            string name = nic.Call<string>("getName") ?? "";
                            if (name.Length == 0) continue;

                            var ips = new List<string>(2);
                            using (AndroidJavaObject addrEnum = nic.Call<AndroidJavaObject>("getInetAddresses"))
                            {
                                while (addrEnum != null && addrEnum.Call<bool>("hasMoreElements"))
                                {
                                    using (AndroidJavaObject addr = addrEnum.Call<AndroidJavaObject>("nextElement"))
                                    {
                                        if (addr == null || addr.Call<bool>("isLoopbackAddress")) continue;
                                        string host = addr.Call<string>("getHostAddress") ?? "";
                                        if (host.IndexOf(':') < 0 && host.IndexOf('.') > 0)
                                            ips.Add(host);
                                    }
                                }
                            }
                            MergeInterface(output, name, ips);
                        }
                    }
                }
            }
            catch
            {
                // Continue to ConnectivityManager / WifiManager fallbacks below.
            }

            // 2. Supplement from ConnectivityManager active LinkProperties (Android 10+ idiomatic link query).
            try
            {
                using (AndroidJavaObject activity = GetCurrentActivity())
                {
                    if (activity != null)
                    {
                        using (AndroidJavaObject cm = activity.Call<AndroidJavaObject>("getSystemService", "connectivity"))
                        using (AndroidJavaObject activeNet = cm?.Call<AndroidJavaObject>("getActiveNetwork"))
                        using (AndroidJavaObject linkProps = activeNet != null ? cm.Call<AndroidJavaObject>("getLinkProperties", activeNet) : null)
                        {
                            if (linkProps != null)
                            {
                                string ifName = linkProps.Call<string>("getInterfaceName") ?? "wlan0";
                                using (AndroidJavaObject linkAddresses = linkProps.Call<AndroidJavaObject>("getLinkAddresses"))
                                {
                                    int count = linkAddresses != null ? linkAddresses.Call<int>("size") : 0;
                                    var ips = new List<string>(count);
                                    for (int i = 0; i < count; i++)
                                    {
                                        using (AndroidJavaObject la = linkAddresses.Call<AndroidJavaObject>("get", i))
                                        using (AndroidJavaObject inet = la?.Call<AndroidJavaObject>("getAddress"))
                                        {
                                            if (inet == null || inet.Call<bool>("isLoopbackAddress")) continue;
                                            string host = inet.Call<string>("getHostAddress") ?? "";
                                            if (host.IndexOf(':') < 0 && host.IndexOf('.') > 0)
                                                ips.Add(host);
                                        }
                                    }
                                    MergeInterface(output, ifName, ips);
                                }
                            }
                        }

                        // 3. Classic WifiManager connectionInfo integer IPv4 fallback when Wi-Fi client IP is active.
                        using (AndroidJavaObject appCtx = activity.Call<AndroidJavaObject>("getApplicationContext"))
                        using (AndroidJavaObject wifi = (appCtx ?? activity).Call<AndroidJavaObject>("getSystemService", "wifi"))
                        using (AndroidJavaObject info = wifi?.Call<AndroidJavaObject>("getConnectionInfo"))
                        {
                            if (info != null)
                            {
                                int rawIp = info.Call<int>("getIpAddress");
                                if (rawIp != 0)
                                {
                                    string dotted = string.Format(
                                        CultureInfo.InvariantCulture,
                                        "{0}.{1}.{2}.{3}",
                                        rawIp & 0xFF,
                                        (rawIp >> 8) & 0xFF,
                                        (rawIp >> 16) & 0xFF,
                                        (rawIp >> 24) & 0xFF);
                                    MergeInterface(output, "wlan0", new[] { dotted });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore if Wi-Fi service is unavailable.
            }
        }
#endif
    }

    /// <summary>
    /// Lifecycle guard obeying <c>apex-net-frame</c>: flushes a zeroed stop + <see cref="InputButtons.AimCancel"/> frame
    /// immediately on <c>OnApplicationPause(true)</c> (and optional focus loss) so held abilities never auto-fire or run into walls,
    /// then resets latched bits and performs clean network/hotspot resume on return.
    /// </summary>
    public sealed class NetworkPauseGuard : MonoBehaviour
    {
        public static NetworkPauseGuard Current { get; private set; }

        public double ReconnectWindowSeconds { get; set; } = 60.0;
        public bool FlushOnFocusLoss { get; set; } = true;
        public bool IsApplicationPaused { get; private set; }
        public int FlushedCancelFrames { get; private set; }
        public double LastPausedSeconds { get; private set; }

        public event Action<bool> PauseStateChanged;
        public event Action AimCancelFlushed;
        public event Action Resumed;

        SimBridge _sim;
        StreetBridge _street;
        long _pausedTimestamp;
        bool _flushedSinceLastActive;

        /// <summary>Attaches or reuses a <see cref="NetworkPauseGuard"/> and optionally binds active match bridges.</summary>
        public static NetworkPauseGuard Attach(GameObject host = null, SimBridge sim = null, StreetBridge street = null)
        {
            NetworkPauseGuard guard = Current;
            if (guard == null)
            {
                if (host != null)
                {
                    guard = host.GetComponent<NetworkPauseGuard>();
                    if (guard == null) guard = host.AddComponent<NetworkPauseGuard>();
                }
                else
                {
                    var go = new GameObject("[KalyeahNetworkPauseGuard]");
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                    guard = go.AddComponent<NetworkPauseGuard>();
                }
            }

            if (sim != null) guard.Bind(sim);
            if (street != null) guard.BindStreet(street);
            return guard;
        }

        public void Bind(SimBridge sim) => _sim = sim;

        public void BindStreet(StreetBridge street) => _street = street;

        public void Unbind()
        {
            _sim = null;
            _street = null;
        }

        void Awake()
        {
            if (Current == null)
                Current = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(Current, this))
                Current = null;
            AndroidLocalLink.ReleaseMulticastLock();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (IsApplicationPaused) return;
                IsApplicationPaused = true;
                _pausedTimestamp = Stopwatch.GetTimestamp();
                FlushAimCancel();
                _flushedSinceLastActive = true;
                PauseStateChanged?.Invoke(true);
            }
            else
            {
                if (!IsApplicationPaused) return;
                IsApplicationPaused = false;
                double elapsed = 0.0;
                if (_pausedTimestamp > 0)
                {
                    long deltaTicks = Math.Max(0L, Stopwatch.GetTimestamp() - _pausedTimestamp);
                    elapsed = (double)deltaTicks / Stopwatch.Frequency;
                }
                _pausedTimestamp = 0;
                _flushedSinceLastActive = false;
                HandleResume(elapsed);
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (FlushOnFocusLoss && !IsApplicationPaused && !_flushedSinceLastActive)
                {
                    FlushAimCancel();
                    _flushedSinceLastActive = true;
                }
            }
            else
            {
                if (!IsApplicationPaused && _flushedSinceLastActive)
                {
                    _flushedSinceLastActive = false;
                    ClearLatchedInput();
                }
            }
        }

        /// <summary>Immediately zeroes movement/aim and flushes <see cref="InputButtons.AimCancel"/> locally and upstream.</summary>
        public void FlushAimCancel()
        {
            var zero = System.Numerics.Vector2.Zero;
            _sim?.SetLocalInput(zero, zero, InputButtons.AimCancel);
            _street?.SetLocalInput(zero, zero, primaryHeld: false, secondaryPressed: false, cancelAim: true, moveTarget: null);

            AppSession session = AppSession.Current;
            GameClient client = session.Client;
            if (client != null && client.IsOnline())
            {
                double dt = _sim != null ? _sim.TickDt : (_street != null ? _street.TickDt : (1.0 / 30.0));
                PlayerInput cancelFrame = PlayerInput.Create(Vec2.Zero, Vec2.Zero, InputButtons.AimCancel, client.NextInputSequence);
                client.SendInput(cancelFrame, dt);
                session.PollNetwork(0.0);
            }

            FlushedCancelFrames += 1;
            AimCancelFlushed?.Invoke();
        }

        /// <summary>Clears the temporary cancel latch and drains/reconnects active network sessions after a background pause.</summary>
        public void HandleResume(double pausedSeconds)
        {
            LastPausedSeconds = Math.Max(0.0, pausedSeconds);
            ClearLatchedInput();

            AppSession session = AppSession.Current;
            if (session.IsHosting)
                AndroidLocalLink.AcquireMulticastLock();

            session.PollNetwork(0.0);

            GameClient client = session.Client;
            if (session.IsNetworkedMode &&
                client != null &&
                !client.IsOnline() &&
                !client.IsConnecting() &&
                !string.IsNullOrEmpty(session.ReconnectUrl) &&
                LastPausedSeconds <= ReconnectWindowSeconds)
            {
                client.Token = session.ReconnectToken ?? "";
                bool patient = session.Mode == GameMode.Online;
                if (client.ConnectTo(session.ReconnectUrl, patient))
                    session.PollNetwork(0.0);
            }

            Resumed?.Invoke();
            PauseStateChanged?.Invoke(false);
        }

        void ClearLatchedInput()
        {
            var zero = System.Numerics.Vector2.Zero;
            _sim?.SetLocalInput(zero, zero, 0);
            _street?.SetRawInput(zero, zero, 0, null);
        }
    }
}
