using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// Ports of tests/integration (test_network_match.gd, test_network_resume_snapshot.gd, test_network_chat.gd,
    /// test_hotspot_discovery.gd, test_street_games_online.gd): a real server and real clients in one process, over WebSockets on
    /// localhost (and an in-memory hub), polled like the game polls them.
    /// </summary>
    public class IntegrationTests
    {
        const double DT = 1.0 / 30.0;

        static GameServer NewServer(long seed = 11) => new GameServer(NetKit.Rules, NetKit.Layout, NetKit.Net, seed, NetKit.Games);
        static GameClient NewClient(string name) => new GameClient(NetKit.Rules, NetKit.Layout, NetKit.Net, NetKit.Games) { PlayerName = name };

        /// <summary>Polls everything (with a short real sleep for the sockets) until `done` or `seconds` of wall time.</summary>
        static bool PollUntil(Func<bool> done, GameServer server, IEnumerable<GameClient> clients, double seconds = 10.0, Action perTick = null)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                server.Poll(DT);
                foreach (GameClient c in clients) c.Poll(DT);
                perTick?.Invoke();
                if (done()) return true;
                Thread.Sleep(2);
            }
            return done();
        }

        [Test]
        public void TwoClientsPlayAMatchOverRealWebSocketsAndOneReconnects()
        {
            GameServer server = NewServer();
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            server.Attach(transport);
            string url = "ws://127.0.0.1:" + transport.Port;
            GameClient ana = NewClient("Ana"), ben = NewClient("Ben");
            var clients = new List<GameClient> { ana, ben };
            try
            {
                int welcomed = 0;
                ana.Welcomed += _ => welcomed++;
                ben.Welcomed += _ => welcomed++;
                Assert.IsTrue(ana.ConnectTo(url));
                Assert.IsTrue(ben.ConnectTo(url));
                Assert.IsTrue(PollUntil(() => welcomed == 2, server, clients), "both welcomed");
                ana.CreateRoom();
                Assert.IsTrue(PollUntil(() => ana.Room.GetStr("code") != "", server, clients), "room created");
                Assert.IsTrue(ana.IsHost());
                ben.JoinRoom(ana.Room.GetStr("code"));
                Assert.IsTrue(PollUntil(() => ana.Room.GetArr("members").Count == 2 && ben.PlayerId > 0, server, clients), "joined");
                Assert.AreEqual(1, ben.MyTeam());
                ben.SetReady(true);
                ana.SetReady(true);
                Assert.IsTrue(PollUntil(() => ana.Room.GetBool("can_start"), server, clients));
                ana.StartMatch();
                Assert.IsTrue(PollUntil(() => ana.InMatch() && ben.InMatch(), server, clients), "both build their mirror");
                Assert.AreEqual(2, ana.Mirror.Players.Count);
                ana.Loaded();
                ben.Loaded();
                Assert.IsTrue(PollUntil(() => ana.Stage == "pick", server, clients), "the pick");
                ana.PickWeapons("bato_light", "trumpo", "migo");
                ben.PickWeapons("jacks", "bola");
                Assert.IsTrue(PollUntil(() => ana.Stage == "play" && ben.Stage == "play", server, clients), "play");
                Vec2 start = ben.Mirror.Players[ana.PlayerId].Position;
                int snapshots = 0;
                ben.SnapshotApplied += () => snapshots++;
                int tick = 0;
                Assert.IsTrue(PollUntil(() => snapshots > 20 && !ben.Mirror.Players[ana.PlayerId].Position.IsEqualApprox(start), server, clients, 15.0, () =>
                {
                    ana.SendInput(PlayerInput.Create(new Vec2(1.0, 0.0), Vec2.Zero, 0, ++tick), DT);
                    ben.SendInput(PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, tick), DT);
                    ben.Interpolate();
                }), "Ben sees Ana walk");

                // chat: a sticker comes back to everyone
                var said = new List<(int, int)>();
                ben.Said += (p, s) => said.Add((p, s));
                ana.Say(2);
                Assert.IsTrue(PollUntil(() => said.Count == 1, server, clients), "sticker");
                Assert.AreEqual((ana.PlayerId, 2), said[0]);

                // Ben drops and comes back on a new connection with his token
                string token = ben.Token;
                int benId = ben.PlayerId;
                ben.Close();
                Assert.IsTrue(PollUntil(() => !server.Rooms.Rooms.Values.First().Members[1].Connected, server, clients), "the server sees the drop");
                GameClient back = NewClient("Ben");
                back.Token = token;
                bool reconnected = false;
                back.Welcomed += r => reconnected = r;
                back.ConnectTo(url);
                clients[1] = back;
                Assert.IsTrue(PollUntil(() => reconnected && back.Stage == "play" && back.InMatch(), server, clients), "back in the match");
                Assert.AreEqual(benId, back.PlayerId);
                Assert.Greater(back.NextInputSequence, 1, "input numbering resumes after the old ones");
            }
            finally
            {
                foreach (GameClient c in clients) c.Close();
                server.Stop();
            }
        }

        [Test]
        public void AConnectionToNowhereFails()
        {
            GameClient client = NewClient("x");
            bool failed = false;
            client.ConnectFailed += () => failed = true;
            client.ConnectTo("ws://127.0.0.1:1");
            var clock = Stopwatch.StartNew();
            while (!failed && clock.Elapsed.TotalSeconds < 6)
            {
                client.Poll(DT);
                Thread.Sleep(5);
            }
            Assert.IsTrue(failed);
        }

        [Test]
        public void APlainHttpRequestGetsAHealthAnswer()
        {
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            try
            {
                using var http = new System.Net.Http.HttpClient();
                string body = http.GetStringAsync("http://127.0.0.1:" + transport.Port + "/").Result;
                Assert.AreEqual("kalyeah\n", body);
            }
            finally
            {
                transport.Close();
            }
        }

        [Test]
        public void AHotspotHostIsFoundByItsBeaconAndJoinedWithoutACode()
        {
            GameServer host = NewServer(5);
            host.AutoJoin = true;
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            host.Attach(transport);
            var beacon = new HotspotBeacon();
            Assert.IsTrue(beacon.Listen(0, 8, "127.0.0.1"));
            GameClient owner = NewClient("Host");
            var clients = new List<GameClient> { owner };
            try
            {
                owner.ConnectTo("ws://127.0.0.1:" + transport.Port);
                Assert.IsTrue(PollUntil(owner.IsOnline, host, clients));
                owner.CreateRoom();
                Assert.IsTrue(PollUntil(() => owner.PlayerId > 0, host, clients));
                var plan = new HotspotNet.Plan { BeaconPort = beacon.Port, WsPort = transport.Port, Seconds = 5.0, Interval = 0.5 };
                plan.Routes.Add(("127.0.0.1", "127.0.0.1"));
                var finder = new HotspotFinder();
                finder.Start(plan);
                Assert.IsTrue(PollUntil(() => { beacon.Poll(); finder.Poll(DT); return finder.Rooms.Count > 0; }, host, clients), "found");
                Assert.AreEqual(1, finder.Heard);
                Assert.AreEqual("Host", finder.Rooms[0].Host);
                Assert.AreEqual(1, finder.Rooms[0].Players);
                Assert.AreEqual(10, finder.Rooms[0].Size);
                finder.Stop();
                GameClient guest = NewClient("Guest");
                clients.Add(guest);
                guest.ConnectTo(finder.Rooms[0].Url);
                Assert.IsTrue(PollUntil(guest.IsOnline, host, clients));
                guest.JoinRoom("");
                Assert.IsTrue(PollUntil(() => guest.PlayerId > 0 && owner.Room.GetArr("members").Count == 2, host, clients), "joined without a code");
            }
            finally
            {
                beacon.Stop();
                foreach (GameClient c in clients) c.Close();
                host.Stop();
            }
        }

        [Test]
        public void AnOnlineServerTellsTheFinderItIsNotAHotspot()
        {
            GameServer online = NewServer(5);
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            online.Attach(transport);
            try
            {
                var plan = new HotspotNet.Plan { WsPort = transport.Port, Seconds = 5.0, Interval = 1.0 };
                plan.Candidates.Add("ws://127.0.0.1:" + transport.Port);
                var finder = new HotspotFinder();
                finder.Start(plan);
                Assert.IsTrue(PollUntil(() => { finder.Poll(DT); return !finder.IsSearching; }, online, new GameClient[0]));
                Assert.IsTrue(finder.Failed);
                Assert.AreEqual(HotspotFinder.NoRoom, finder.FailureMessage);
            }
            finally
            {
                online.Stop();
            }
        }

        [TestCase("patintero", 4)]
        [TestCase("tumbang_preso", 3)]
        [TestCase("langit_lupa", 3)]
        public void AStreetGameIsPlayedThroughTheServerAndItsEventsReachTheMirror(string map, int players)
        {
            GameServer server = NewServer(21);
            var hub = new MemoryHub();
            server.Attach(hub);
            var clients = new List<GameClient>();
            for (int i = 0; i < players; i++)
            {
                GameClient c = NewClient("Kid" + i);
                c.Attach(hub.Connect(100 + i));
                clients.Add(c);
            }
            Assert.IsTrue(PollUntil(() => clients.All(c => c.IsOnline()), server, clients));
            clients[0].CreateRoom();
            Assert.IsTrue(PollUntil(() => clients[0].PlayerId > 0, server, clients));
            foreach (GameClient c in clients.Skip(1)) c.JoinRoom(clients[0].Room.GetStr("code"));
            Assert.IsTrue(PollUntil(() => clients.All(c => c.PlayerId > 0), server, clients));
            clients[0].SetMap(map);
            foreach (GameClient c in clients) c.SetReady(true);
            Assert.IsTrue(PollUntil(() => clients[0].Room.GetBool("can_start") && clients[0].Room.GetStr("map") == map, server, clients), "can start");
            clients[0].StartMatch();
            Assert.IsTrue(PollUntil(() => clients.All(c => c.InMatch()), server, clients));
            Assert.IsInstanceOf<StreetMode>(clients[1].Mirror.Mode);
            foreach (GameClient c in clients) c.Loaded();
            Assert.IsTrue(PollUntil(() => clients.All(c => c.Stage == "play"), server, clients), "no weapon pick in a street game");
            var modeEvents = new List<string>();
            clients[1].EventReceived += (target, name, args) => { if (target == "mode") modeEvents.Add(name); };
            int tick = 0;
            var mirrorMode = (StreetMode)clients[1].Mirror.Mode;
            var serverMode = (StreetMode)server.Matches.Values.First().Sim.Mode;
            PollUntil(() => false, server, clients, 5.0, () =>
            {
                tick++;
                for (int i = 0; i < clients.Count; i++)
                    clients[i].SendInput(SnapshotGoldenTests.ScriptedInput(clients[i].PlayerId, tick), DT);
            });
            Assert.Greater(tick, 30);
            Assert.AreEqual(serverMode.AttackTeam, mirrorMode.AttackTeam);
            Assert.Less(Math.Abs(serverMode.TimeLeft - mirrorMode.TimeLeft), 0.5, "the mirror's game clock follows the server");
            if (map == "patintero") Assert.IsNotEmpty(modeEvents, "the game's own signals are relayed");
        }

        [Test]
        public void HotspotRolesAndTheSearchPlanComeFromTheData()
        {
            NetRules rules = NetKit.Net;
            var wifiOnly = new[] { new NetInterface("wlan0", "192.168.1.23"), new NetInterface("lo", "127.0.0.1"), new NetInterface("rmnet0", "10.0.0.4") };
            Assert.IsFalse(HotspotNet.HotspotOn(wifiOnly, rules));
            Assert.IsTrue(HotspotNet.WifiConnected(wifiOnly, rules));
            Assert.IsTrue(HotspotNet.HotspotOn(new[] { new NetInterface("swlan0", "192.168.43.1") }, rules));
            Assert.IsFalse(HotspotNet.IsPrivate("8.8.8.8"));
            Assert.IsTrue(HotspotNet.IsPrivate("172.20.10.1"));
            HotspotNet.Plan plan = HotspotNet.SearchPlan(wifiOnly, rules);
            CollectionAssert.AreEqual(new[] { ("192.168.1.23", "255.255.255.255"), ("192.168.1.23", "192.168.1.255") }, plan.Routes);
            CollectionAssert.AreEqual(new[] { "ws://192.168.1.1:8080", "ws://192.168.43.1:8080", "ws://172.20.10.1:8080" }, plan.Candidates);
        }

        [Test]
        public void WifiQrRoundTripsAndRefusesWhatCannotBeJoined()
        {
            var network = new WifiQr.Network { Ssid = "Kalye;Ah:1", Password = "pass\\word", Security = "WPA" };
            string text = WifiQr.Build(network);
            Assert.AreEqual("WIFI:T:WPA;S:Kalye\\;Ah\\:1;P:pass\\\\word;;", text);
            WifiQr.Network back = WifiQr.Parse(text);
            Assert.AreEqual(network.Ssid, back.Ssid);
            Assert.AreEqual(network.Password, back.Password);
            Assert.IsNull(WifiQr.Parse("WIFI:T:WEP;S:x;P:12345678;;"));
            Assert.IsNull(WifiQr.Parse("WIFI:T:WPA;S:x;P:short;;"));
            Assert.AreEqual("nopass", WifiQr.Parse("wifi:S:open;;").Security);
        }

        [Test]
        public void TheCodecRefusesObjectsAndTruncatedData()
        {
            Assert.IsFalse(VariantCodec.TryDecode(new byte[] { 24, 0, 0, 0, 0, 0, 0, 0 }, out _));
            byte[] good = VariantCodec.Encode(GDict.Of("t", 1L, "s", "héllo"));
            Assert.IsTrue(VariantCodec.TryDecode(good, out _));
            Assert.IsFalse(VariantCodec.TryDecode(good.Take(good.Length - 3).ToArray(), out _));
        }
    }
}
