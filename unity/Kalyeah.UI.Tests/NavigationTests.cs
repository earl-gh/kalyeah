using System.Collections.Generic;
using Kalyeah.Net;
using NUnit.Framework;

namespace Kalyeah.UI.Tests
{
    public class MenuNavigationTests
    {
        [Test]
        public void BackClosesTheDialogThenThePageThenAsksToQuit()
        {
            var nav = new MenuNavigation();
            var closed = new List<string>();
            nav.Push(MenuNavigation.Kind.Page, "settings", () => closed.Add("settings"));
            nav.Push(MenuNavigation.Kind.Dialog, "confirm", () => closed.Add("confirm"));
            Assert.AreEqual("settings", nav.CurrentPage);
            Assert.AreEqual(MenuNavigation.BackResult.Closed, nav.Back());
            Assert.AreEqual(MenuNavigation.BackResult.Closed, nav.Back());
            CollectionAssert.AreEqual(new[] { "confirm", "settings" }, closed);
            Assert.AreEqual(MenuNavigation.BackResult.AskQuit, nav.Back());
            Assert.AreEqual("", nav.CurrentPage);
        }

        [Test]
        public void OnePageAtATimeAndBusyOrStickyLayersIgnoreBack()
        {
            var nav = new MenuNavigation();
            var closed = new List<string>();
            nav.Push(MenuNavigation.Kind.Page, "kids", () => closed.Add("kids"));
            nav.Push(MenuNavigation.Kind.Page, "cards", () => closed.Add("cards"));
            CollectionAssert.AreEqual(new[] { "kids" }, closed, "opening a page closes the one under it");
            Assert.AreEqual(1, nav.Count);
            nav.Busy = true;
            Assert.AreEqual(MenuNavigation.BackResult.Nothing, nav.Back());
            nav.Busy = false;
            nav.Push(MenuNavigation.Kind.Dialog, "kidpick", null, sticky: true);
            Assert.AreEqual(MenuNavigation.BackResult.Nothing, nav.Back());
            nav.Remove("kidpick");
            Assert.IsFalse(nav.AnyDialog);
            nav.Clear();
            CollectionAssert.AreEqual(new[] { "kids", "cards" }, closed);
            Assert.AreEqual(0, nav.Count);
        }
    }

    public class LoadingAndChatTests
    {
        [Test]
        public void TheBarNeverFillsFasterThanItsCap()
        {
            var bar = new LoadingProgress { Loaded = 1.0 };
            Assert.IsFalse(bar.Advance(1.0));
            Assert.AreEqual(70, bar.Percent);
            Assert.IsTrue(bar.Advance(1.0));
            Assert.AreEqual(100, bar.Percent);
            Assert.IsFalse(bar.Advance(1.0), "full once");
            var waiting = new LoadingProgress { Loaded = 0.4 };
            waiting.Advance(5.0);
            Assert.AreEqual(40, waiting.Percent, "it waits for what has really loaded");
            Assert.AreEqual(MenuCatalog.Tips[2], LoadingProgress.Tip(n => 2));
        }

        [Test]
        public void QuickChatKeepsTheServersGap()
        {
            var chat = new QuickChat(UiKit.Menu.Net.ChatMinGap, UiKit.Menu.Net.ChatStickers);
            Assert.AreEqual(6, QuickChat.Stickers.Length);
            Assert.IsTrue(chat.TrySend(0));
            Assert.IsFalse(chat.TrySend(1), "within the gap");
            chat.Tick(UiKit.Menu.Net.ChatMinGap);
            Assert.IsTrue(chat.TrySend(5));
            chat.Tick(10);
            Assert.IsFalse(chat.TrySend(6));
            Assert.IsFalse(chat.TrySend(-1));
        }
    }

    public class HotspotFlowTests
    {
        static List<NetInterface> Wifi() => new List<NetInterface> { new NetInterface("lo", "127.0.0.1"), new NetInterface("wlan0", "192.168.1.23") };
        static List<NetInterface> Tether() => new List<NetInterface> { new NetInterface("ap0", "192.168.77.1") };
        static List<NetInterface> Nothing() => new List<NetInterface> { new NetInterface("lo", "127.0.0.1"), new NetInterface("rmnet0", "10.20.30.40") };

        [Test]
        public void CreateGoesOnWithAHotspotOrAWifiAndWaitsWithNeither()
        {
            foreach (var interfaces in new[] { Wifi(), Tether() })
            {
                var flow = new HotspotFlow(HotspotFlow.FlowMode.Create, UiKit.Menu.Net, () => interfaces);
                string done = null;
                flow.Finished += url => done = url;
                flow.Begin();
                Assert.AreEqual("Checking your network…", flow.Message());
                flow.Tick(0.1);
                Assert.AreEqual("", done);
                Assert.IsTrue(flow.Ended);
            }
            List<NetInterface> now = Nothing();
            var waiting = new HotspotFlow(HotspotFlow.FlowMode.Create, UiKit.Menu.Net, () => now);
            bool finished = false;
            waiting.Finished += _ => finished = true;
            waiting.Begin();
            waiting.Tick(0.1);
            Assert.AreEqual(HotspotFlow.Step.NoNetwork, waiting.Current);
            Assert.AreEqual("Turn on your hotspot or Wi-Fi. Then tap Try again.", waiting.Message());
            Assert.IsTrue(waiting.ShowTryAgain);
            now = Wifi();
            waiting.Resumed();
            waiting.Tick(0.1);
            Assert.IsTrue(finished, "back from the phone's settings it looks again");
        }

        [Test]
        public void JoinNeedsAWifiAndListsRoomsWithoutJoiningByItself()
        {
            var noWifi = new HotspotFlow(HotspotFlow.FlowMode.Join, UiKit.Menu.Net, Tether);
            noWifi.Begin();
            noWifi.Tick(0.1);
            Assert.AreEqual(HotspotFlow.Step.NoWifi, noWifi.Current, "a phone's own hotspot is not a Wi-Fi to look on");

            // a Local host over a MemoryHub answers the finder's probe like a phone on the Wi-Fi
            var rig = new LocalRig();
            var hostFlow = new LobbyFlow(rig.HostPhone, new SessionModel(new ConfigStore(null)) { PlayerName = "Gabo", AvatarId = "tala" },
                new SettingsModel(new ConfigStore(null)), true, SessionModel.LobbyIntent.Create, () => 5);
            hostFlow.Begin();
            Assert.IsTrue(UiKit.PollUntil(() => hostFlow.Room != null, rig.Poll));

            var finder = new HotspotFinder { ProbeFactory = url => url == "ws://192.168.1.1:" + UiKit.Menu.Net.DefaultPort ? (IClientTransport)rig.Hub.Connect(900) : new DeadProbe() };
            var plan = new HotspotNet.Plan { Seconds = 1.0, Interval = 0.5, WsPort = UiKit.Menu.Net.DefaultPort };
            plan.Candidates.Add("ws://192.168.1.1:" + UiKit.Menu.Net.DefaultPort);
            plan.Candidates.Add("ws://192.168.43.1:" + UiKit.Menu.Net.DefaultPort);
            var flow = new HotspotFlow(HotspotFlow.FlowMode.Join, UiKit.Menu.Net, Wifi, _ => plan, finder);
            string joined = null;
            List<HotspotFinder.FoundRoom> rooms = null;
            flow.Finished += url => joined = url;
            flow.RoomsChanged += list => rooms = list;
            flow.Begin();
            Assert.IsTrue(UiKit.PollUntil(() => flow.Current == HotspotFlow.Step.Found, () => { rig.Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.IsNull(joined, "nothing is joined by itself");
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual("Gabo's room  1/10", MenuCatalog.LocalRoomText(rooms[0]));
            Assert.AreEqual("Tap a room to join.", flow.Message());
            flow.Choose("ws://nowhere");
            Assert.IsNull(joined);
            flow.Choose(rooms[0].Url);
            Assert.AreEqual(rooms[0].Url, joined);
            hostFlow.Dispose();
        }

        [Test]
        public void JoinWithNoHostSaysNoRoomFound()
        {
            var plan = new HotspotNet.Plan { Seconds = 0.5, Interval = 0.25 };
            plan.Candidates.Add("ws://192.168.1.1:8080");
            var flow = new HotspotFlow(HotspotFlow.FlowMode.Join, UiKit.Menu.Net, Wifi, _ => plan, new HotspotFinder { ProbeFactory = _ => new DeadProbe() });
            flow.Begin();
            Assert.IsTrue(UiKit.PollUntil(() => flow.Current == HotspotFlow.Step.NoRoom, () => flow.Tick(1.0 / 30.0)));
            Assert.AreEqual(HotspotFinder.NoRoom, flow.Message());
            flow.Cancel();
            flow.Begin();
            Assert.AreEqual(HotspotFlow.Step.NoRoom, flow.Current, "nothing runs after Cancel");
        }

        /// <summary>An address where nobody answers: it stays connecting until the finder's time is up.</summary>
        sealed class DeadProbe : IClientTransport
        {
            public ConnectionStatus Status { get; private set; } = ConnectionStatus.Connecting;
            public void Poll() { }
            public bool TryGetPacket(out byte[] packet)
            {
                packet = null;
                return false;
            }
            public void Send(byte[] packet) { }
            public void Close() => Status = ConnectionStatus.Disconnected;
        }
    }
}
