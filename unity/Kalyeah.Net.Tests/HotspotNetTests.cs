using System.Collections.Generic;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// Ports of tests/unit/test_hotspot_net.gd and tests/unit/hotspot_phones.gd: Android vendor hotspot / tether interface
    /// classification, client vs ignored vs tether roles, RFC 1918 private IPv4 filtering, and discovery broadcast / fallback plans.
    /// </summary>
    public class HotspotNetTests
    {
        static NetInterface Nic(string name, params string[] addresses) => new NetInterface(name, addresses);

        static readonly NetInterface[] RealContainer =
        {
            Nic("eth0", "192.0.2.2"),
            Nic("lo", "127.0.0.1"),
        };

        static NetInterface[] OnHomeWifi() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan0", "192.168.1.37", "fe80::1c2a:3bff:fe4d:5e6f"),
            Nic("rmnet_data0", "10.58.2.14"),
        };

        static NetInterface[] OnMobileDataOnly() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("ccmni1", "10.58.2.14"),
        };

        static NetInterface[] QualcommMobileDataOnly() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("rmnet0", "10.20.30.40"),
        };

        static NetInterface[] StockHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan1", "192.168.31.167"),
            Nic("rmnet_data1", "10.1.2.3"),
        };

        static NetInterface[] SamsungHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("swlan0", "192.168.43.1"),
            Nic("rmnet_data0", "10.20.30.40"),
        };

        static NetInterface[] MediatekHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("ap0", "192.168.43.1"),
            Nic("ccmni1", "10.58.2.14"),
        };

        static NetInterface[] QualcommSoftapHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("softap0", "192.168.43.77"),
            Nic("rmnet_data0", "10.9.8.7"),
        };

        static NetInterface[] TenNetHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("ap0", "10.45.12.1"),
        };

        static NetInterface[] UnknownVendorHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("vendor_ap1", "192.168.7.1"),
            Nic("rmnet_data0", "10.1.1.1"),
        };

        static NetInterface[] HotspotAndWifi() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan0", "192.168.1.5"),
            Nic("ap0", "192.168.43.1"),
        };

        static NetInterface[] UsbTether() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("rndis0", "192.168.42.129"),
        };

        static NetInterface[] BluetoothTether() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("bt-pan", "192.168.44.1"),
        };

        static NetInterface[] GuestOnAHotspot() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan0", "192.168.43.57"),
            Nic("rmnet_data0", "10.1.2.3"),
        };

        static NetInterface[] VpnOverWifi() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan0", "192.168.1.5"),
            Nic("tun0", "10.8.0.2"),
        };

        static NetInterface[] WifiDirectOnly() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("p2p-wlan0-0", "192.168.49.1"),
        };

        static NetInterface[] WifiWithoutAddress() => new[]
        {
            Nic("lo", "127.0.0.1"),
            Nic("wlan0", "169.254.12.9", "fe80::1"),
        };

        static NetInterface[] Offline() => new[]
        {
            Nic("lo", "127.0.0.1"),
        };

        static (bool Hotspot, bool Wifi) Verdict(IEnumerable<NetInterface> interfaces) =>
            (HotspotNet.HotspotOn(interfaces, NetKit.Net), HotspotNet.WifiConnected(interfaces, NetKit.Net));

        [Test]
        public void OrdinaryWifiGuestAndVpnOverWifiAreWifiClientsNotHotspotHosts()
        {
            Assert.AreEqual((false, true), Verdict(OnHomeWifi()));
            Assert.AreEqual((false, true), Verdict(GuestOnAHotspot()), "192.168.43.x on wlan0 is a guest, not a host");
            Assert.AreEqual((false, true), Verdict(VpnOverWifi()), "a VPN's private address on tun0 is not a hotspot");
        }

        [Test]
        public void MobileDataWifiDirectLinkLocalAndDocumentationAddressesAreNeitherHotspotNorWifi()
        {
            Assert.AreEqual((false, false), Verdict(OnMobileDataOnly()), "MediaTek ccmni 10.x carrier address");
            Assert.AreEqual((false, false), Verdict(QualcommMobileDataOnly()), "Qualcomm rmnet 10.x carrier address");
            Assert.AreEqual((false, false), Verdict(WifiDirectOnly()), "p2p Wi-Fi Direct group");
            Assert.AreEqual((false, false), Verdict(WifiWithoutAddress()), "169.254.x link-local before DHCP");
            Assert.AreEqual((false, false), Verdict(Offline()));
            Assert.AreEqual((false, false), Verdict(RealContainer), "eth0 holds TEST-NET-1 192.0.2.2, lo is ignored");
        }

        [Test]
        public void EveryAndroidVendorAccessPointUsbAndBluetoothTetherCountsAsAHotspot()
        {
            NetInterface[][] tetherPhones =
            {
                StockHotspot(),
                SamsungHotspot(),
                MediatekHotspot(),
                QualcommSoftapHotspot(),
                TenNetHotspot(),
                UnknownVendorHotspot(),
                UsbTether(),
                BluetoothTether(),
            };
            foreach (NetInterface[] phone in tetherPhones)
                Assert.AreEqual((true, false), Verdict(phone), phone[1].Name);
        }

        [Test]
        public void HotspotWhileConnectedToWifiCountsAsBothAndSearchesOnlyFromClientAddress()
        {
            Assert.AreEqual((true, true), Verdict(HotspotAndWifi()));
            CollectionAssert.AreEqual(
                new[] { "192.168.1.5" },
                HotspotNet.AddressesOf(HotspotAndWifi(), HotspotNet.Role.Client, NetKit.Net),
                "only the Wi-Fi client address is searched from");
        }

        [Test]
        public void InterfaceRolesMatchShippedNetRules()
        {
            NetRules rules = NetKit.Net;
            Assert.AreEqual(HotspotNet.Role.Client, HotspotNet.RoleOf("wlan0", rules));
            Assert.AreEqual(HotspotNet.Role.Client, HotspotNet.RoleOf("eth0", rules));
            Assert.AreEqual(HotspotNet.Role.Ignored, HotspotNet.RoleOf("rmnet_data3", rules));
            Assert.AreEqual(HotspotNet.Role.Ignored, HotspotNet.RoleOf("v4-rmnet_data0", rules));
            Assert.AreEqual(HotspotNet.Role.Ignored, HotspotNet.RoleOf("ccmni1", rules));
            Assert.AreEqual(HotspotNet.Role.Ignored, HotspotNet.RoleOf("tun0", rules));
            Assert.AreEqual(HotspotNet.Role.Ignored, HotspotNet.RoleOf("p2p-wlan0-0", rules));
            Assert.AreEqual(HotspotNet.Role.Tether, HotspotNet.RoleOf("wlan1", rules));
            Assert.AreEqual(HotspotNet.Role.Tether, HotspotNet.RoleOf("swlan0", rules));
            Assert.AreEqual(HotspotNet.Role.Tether, HotspotNet.RoleOf("ap0", rules));
            Assert.AreEqual(HotspotNet.Role.Tether, HotspotNet.RoleOf("rndis0", rules));
            Assert.AreEqual(HotspotNet.Role.Tether, HotspotNet.RoleOf("bt-pan", rules));
        }

        [Test]
        public void OnlyRfc1918PrivateIpv4AddressesCount()
        {
            foreach (string ip in new[] { "10.0.0.7", "172.16.0.1", "172.31.255.254", "192.168.43.1" })
                Assert.IsTrue(HotspotNet.IsPrivate(ip), ip);

            foreach (string ip in new[] { "8.8.8.8", "172.32.0.1", "127.0.0.1", "192.169.0.1", "169.254.1.1", "192.0.2.2", "abc", "1.2.3", "300.1.1.1", "fe80::1" })
                Assert.IsFalse(HotspotNet.IsPrivate(ip), ip);
        }

        [Test]
        public void SearchPlanUsesBroadcastRoutesAndDeduplicatesDotOneWithFallbackTethers()
        {
            NetRules rules = NetKit.Net;
            HotspotNet.Plan guestPlan = HotspotNet.SearchPlan(GuestOnAHotspot(), rules);
            CollectionAssert.AreEqual(
                new[] { ("192.168.43.57", "255.255.255.255"), ("192.168.43.57", "192.168.43.255") },
                guestPlan.Routes);
            Assert.AreEqual(rules.DiscoveryPort, guestPlan.BeaconPort);
            Assert.AreEqual(rules.DefaultPort, guestPlan.WsPort);
            Assert.AreEqual(rules.DiscoverySeconds, guestPlan.Seconds);
            Assert.AreEqual(2, guestPlan.Candidates.Count, "192.168.43.1 appears once, not twice");
            CollectionAssert.AreEqual(
                new[] { "ws://192.168.43.1:8080", "ws://172.20.10.1:8080" },
                guestPlan.Candidates);

            HotspotNet.Plan homePlan = HotspotNet.SearchPlan(OnHomeWifi(), rules);
            Assert.AreEqual("ws://192.168.1.1:8080", homePlan.Candidates[0], "the router of the Wi-Fi we are on");
            CollectionAssert.Contains(homePlan.Candidates, "ws://192.168.43.1:8080");
            CollectionAssert.Contains(homePlan.Candidates, "ws://172.20.10.1:8080");
            CollectionAssert.DoesNotContain(homePlan.Candidates, "ws://10.58.2.1:8080", "mobile data is never searched");
        }
    }
}
