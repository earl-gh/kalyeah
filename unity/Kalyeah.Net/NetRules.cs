using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Networking and lobby numbers (port of net_rules.gd; docs/GDD.md "Lobby rules"). The values live in data/rules/net_rules.tres,
    /// exported to unity/Data/rules__net_rules.json; `FromData` reads them through `SimData.Raw`.
    /// </summary>
    public sealed class NetRules
    {
        public const string Source = "res://data/rules/net_rules.tres";

        public int PasscodeLength;
        public string PasscodeAlphabet = "";
        public double ReconnectWindow;
        public double AfkAfter;
        public int SnapshotRate;
        public double InterpolationDelay;
        public double LoadTimeout;
        public double PickGrace;
        public int MaxQueuedInputs;
        public int MaxNameLength;
        public int DefaultPort;
        public double ConnectTimeout;
        public double WakeHintAfter;
        public int RoomListMax;
        public int BaseRoomMinTeam = 1;
        public int ChatStickers;
        public double ChatMinGap;
        public double RoomListMinGap;
        public double RoomListRefresh;
        public int DiscoveryPort;
        public double DiscoverySeconds;
        public double DiscoveryInterval;
        public int DiscoveryBurst;
        public List<string> IgnoredInterfacePrefixes = new List<string>();
        public List<string> ClientInterfaceNames = new List<string>();
        public List<string> FallbackHosts = new List<string>();
        public double JoinWifiGrace;
        public double JoinWifiRecheck;

        public static NetRules FromData(SimData data) => FromRaw(data.Raw(Source));

        public static NetRules FromRaw(OrderedDict<string, object> raw)
        {
            var r = new NetRules();
            r.PasscodeLength = Int(raw, "passcode_length", r.PasscodeLength);
            r.PasscodeAlphabet = raw.GetOrDefault("passcode_alphabet") as string ?? "";
            r.ReconnectWindow = Num(raw, "reconnect_window");
            r.AfkAfter = Num(raw, "afk_after");
            r.SnapshotRate = Int(raw, "snapshot_rate", 0);
            r.InterpolationDelay = Num(raw, "interpolation_delay");
            r.LoadTimeout = Num(raw, "load_timeout");
            r.PickGrace = Num(raw, "pick_grace");
            r.MaxQueuedInputs = Int(raw, "max_queued_inputs", 0);
            r.MaxNameLength = Int(raw, "max_name_length", 0);
            r.DefaultPort = Int(raw, "default_port", 0);
            r.ConnectTimeout = Num(raw, "connect_timeout");
            r.WakeHintAfter = Num(raw, "wake_hint_after");
            r.RoomListMax = Int(raw, "room_list_max", 0);
            r.BaseRoomMinTeam = Int(raw, "base_room_min_team", 1);
            r.ChatStickers = Int(raw, "chat_stickers", 0);
            r.ChatMinGap = Num(raw, "chat_min_gap");
            r.RoomListMinGap = Num(raw, "room_list_min_gap");
            r.RoomListRefresh = Num(raw, "room_list_refresh");
            r.DiscoveryPort = Int(raw, "discovery_port", 0);
            r.DiscoverySeconds = Num(raw, "discovery_seconds");
            r.DiscoveryInterval = Num(raw, "discovery_interval");
            r.DiscoveryBurst = Int(raw, "discovery_burst", 0);
            r.IgnoredInterfacePrefixes = Strings(raw, "ignored_interface_prefixes");
            r.ClientInterfaceNames = Strings(raw, "client_interface_names");
            r.FallbackHosts = Strings(raw, "fallback_hosts");
            r.JoinWifiGrace = Num(raw, "join_wifi_grace");
            r.JoinWifiRecheck = Num(raw, "join_wifi_recheck");
            return r;
        }

        static int Int(OrderedDict<string, object> raw, string key, int fallback) =>
            raw.TryGetValue(key, out object v) && v != null ? (int)MiniJson.Num(v) : fallback;

        static double Num(OrderedDict<string, object> raw, string key) =>
            raw.TryGetValue(key, out object v) && v != null ? MiniJson.Num(v) : 0.0;

        static List<string> Strings(OrderedDict<string, object> raw, string key)
        {
            var list = new List<string>();
            if (raw.TryGetValue(key, out object v) && v is List<object> items)
                foreach (object item in items) list.Add(item as string ?? "");
            return list;
        }
    }
}
