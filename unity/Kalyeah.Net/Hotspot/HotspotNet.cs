using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Kalyeah.Net
{
    /// <summary>One network interface: its name and its IPv4 addresses (Godot's `IP.get_local_interfaces()` entry).</summary>
    public sealed class NetInterface
    {
        public string Name = "";
        public List<string> Addresses = new List<string>();

        public NetInterface() { }
        public NetInterface(string name, params string[] addresses)
        {
            Name = name;
            Addresses.AddRange(addresses);
        }

        /// <summary>This device's interfaces with their IPv4 addresses (what the Unity game passes to HotspotNet).</summary>
        public static List<NetInterface> FromSystem()
        {
            var list = new List<NetInterface>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var entry = new NetInterface { Name = nic.Name };
                foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork) entry.Addresses.Add(address.Address.ToString());
                list.Add(entry);
            }
            return list;
        }
    }

    /// <summary>
    /// What the phone's network interfaces say about a hotspot, without anyone typing an address or a code (port of hotspot_net.gd).
    /// An interface is a CLIENT (Wi-Fi or Ethernet of somebody else's network), IGNORED (loopback, mobile data, VPN, Wi-Fi Direct) or
    /// a TETHER (everything else: `ap0`, `swlan0`, `wlan1`..., USB, Bluetooth); the names are data (NetRules). The host's address is not
    /// guessed: the guest asks the network with a broadcast and the host answers (HotspotBeacon, HotspotFinder); the ".1" of each
    /// network and the classic tether addresses stay as a fallback.
    /// </summary>
    public static class HotspotNet
    {
        public enum Role { Ignored, Client, Tether }

        public const string LimitedBroadcast = "255.255.255.255";
        public const int BroadcastHost = 255;
        public const int GatewayHost = 1;

        /// <summary>Everything a guest needs to look for a host.</summary>
        public sealed class Plan
        {
            /// <summary>`ws://` addresses to try without being asked (the fallback).</summary>
            public List<string> Candidates = new List<string>();
            /// <summary>Where to send the probe: (this phone's address, a broadcast address).</summary>
            public List<(string From, string To)> Routes = new List<(string, string)>();
            public int BeaconPort;
            public int WsPort;
            public double Seconds;
            public double Interval;
        }

        public static Role RoleOf(string interfaceName, NetRules rules)
        {
            if (rules.ClientInterfaceNames.Contains(interfaceName)) return Role.Client;
            foreach (string prefix in rules.IgnoredInterfacePrefixes)
                if (interfaceName.StartsWith(prefix, StringComparison.Ordinal)) return Role.Ignored;
            return Role.Tether;
        }

        /// <summary>The private IPv4 addresses of the interfaces that have `role`.</summary>
        public static List<string> AddressesOf(IEnumerable<NetInterface> interfaces, Role role, NetRules rules)
        {
            var found = new List<string>();
            foreach (NetInterface nic in interfaces)
            {
                if (RoleOf(nic.Name, rules) != role) continue;
                foreach (string address in nic.Addresses)
                    if (IsPrivate(address) && !found.Contains(address)) found.Add(address);
            }
            return found;
        }

        /// <summary>True when this phone is handing out a network (a hotspot, or a USB or Bluetooth tether).</summary>
        public static bool HotspotOn(IEnumerable<NetInterface> interfaces, NetRules rules) => AddressesOf(interfaces, Role.Tether, rules).Count > 0;

        /// <summary>True when this phone is a client of a Wi-Fi network (not its own hotspot).</summary>
        public static bool WifiConnected(IEnumerable<NetInterface> interfaces, NetRules rules) => AddressesOf(interfaces, Role.Client, rules).Count > 0;

        /// <summary>
        /// How to look for a host from the Wi-Fi this phone is on: the probe leaves by each client address to the limited broadcast and
        /// to the ".255" of the /24; then the ".1" of each and the classic tether addresses as WebSocket fallbacks.
        /// </summary>
        public static Plan SearchPlan(IEnumerable<NetInterface> interfaces, NetRules rules)
        {
            var plan = new Plan
            {
                BeaconPort = rules.DiscoveryPort, WsPort = rules.DefaultPort, Seconds = rules.DiscoverySeconds, Interval = rules.DiscoveryInterval,
            };
            var hosts = new List<string>();
            foreach (string address in AddressesOf(interfaces, Role.Client, rules))
            {
                plan.Routes.Add((address, LimitedBroadcast));
                plan.Routes.Add((address, InNetwork(address, BroadcastHost)));
                hosts.Add(InNetwork(address, GatewayHost));
            }
            hosts.AddRange(rules.FallbackHosts);
            foreach (string host in hosts)
            {
                string url = "ws://" + host + ":" + rules.DefaultPort;
                if (!plan.Candidates.Contains(url)) plan.Candidates.Add(url);
            }
            return plan;
        }

        /// <summary>True for 10.x.x.x, 172.16-31.x.x and 192.168.x.x.</summary>
        public static bool IsPrivate(string ip)
        {
            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;
            var n = new int[4];
            for (int i = 0; i < 4; i++)
                if (!IsValidInt(parts[i], out n[i]) || n[i] < 0 || n[i] > 255) return false;
            return n[0] == 10 || (n[0] == 172 && n[1] >= 16 && n[1] <= 31) || (n[0] == 192 && n[1] == 168);
        }

        /// <summary>Godot's `is_valid_int()` then `to_int()` (an optional sign, then digits only).</summary>
        static bool IsValidInt(string s, out int value)
        {
            value = 0;
            if (s.Length == 0) return false;
            int start = s[0] == '+' || s[0] == '-' ? 1 : 0;
            if (start == s.Length) return false;
            for (int i = start; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return int.TryParse(s, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>`address` with its last number replaced: the ".1" or ".255" of its /24.</summary>
        static string InNetwork(string address, int last)
        {
            string[] parts = address.Split('.');
            return parts[0] + "." + parts[1] + "." + parts[2] + "." + last;
        }
    }
}
