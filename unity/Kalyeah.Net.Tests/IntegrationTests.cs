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
                ana.PickWeapons("bato_light", "trumpo", "inggo");
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

        [Test]
        public void LangitLupaOnlineBrownoutHidesFarKidsAndMoundsThenRestoresOverWebSockets()
        {
            GameServer server = NewServer(21);
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            server.Attach(transport);
            string url = "ws://127.0.0.1:" + transport.Port;
            var clients = new List<GameClient>
            {
                NewClient("Ana"),
                NewClient("Ben"),
                NewClient("Cy"),
            };
            try
            {
                foreach (GameClient c in clients) Assert.IsTrue(c.ConnectTo(url));
                Assert.IsTrue(PollUntil(() => clients.All(c => c.IsOnline()), server, clients), "all online");

                GameClient host = clients[0];
                host.CreateRoom();
                Assert.IsTrue(PollUntil(() => !host.Room.IsEmpty, server, clients), "room created");
                host.SetMap("langit_lupa");
                Assert.IsTrue(PollUntil(() => host.Room.GetStr("map") == "langit_lupa", server, clients), "host picked Langit Lupa");

                string code = host.Room.GetStr("code");
                foreach (GameClient c in clients.Skip(1)) c.JoinRoom(code);
                Assert.IsTrue(PollUntil(() => host.Room.GetArr("members").Count == 3 && clients.All(c => c.PlayerId > 0), server, clients), "all 3 in room");

                foreach (GameClient c in clients) c.SetReady(true);
                Assert.IsTrue(PollUntil(() => host.Room.GetBool("can_start"), server, clients), "three kids can start");
                host.StartMatch();
                Assert.IsTrue(PollUntil(() => clients.All(c => c.InMatch()), server, clients), "loading");
                foreach (GameClient c in clients) c.Loaded();
                Assert.IsTrue(PollUntil(() => clients.All(c => c.Stage == "play"), server, clients), "straight to play");

                ServerMatch running = server.Matches.Values.First();
                running.Sim.IntroLeft = 0.0;
                Assert.IsTrue(PollUntil(() => running.Sim.Phase == MatchSim.MatchPhase.Playing, server, clients), "playing");

                var game = running.Sim.Mode as LangitLupa;
                Assert.IsNotNull(game, "the room plays Langit Lupa");
                Assert.IsTrue(PollUntil(
                    () => clients.All(c => c.Mirror.Mode is LangitLupa m && m.TayaId == game.TayaId),
                    server,
                    clients), "every phone knows the taya");

                int[] ids = clients.Select(c => c.PlayerId).ToArray();
                GameClient viewer = clients[0];
                int farId = ids[2];

                // Lights out: far kid beyond BrownoutSight on its own mound, near kid within sight.
                game.GraceLeft = 99.0;
                game.TimeLeft = game.Rules.MatchSeconds - game.Rules.BrownoutFirst - 0.5;
                running.Sim.Players[ids[0]].Position = new Vec2(-6.0, 20.0);
                running.Sim.Players[ids[1]].Position = new Vec2(-6.0, 17.0);
                running.Sim.Players[farId].Position = new Vec2(8.0, -20.0);
                game.RaiseMound(running.Sim, running.Sim.Players[farId]);

                Assert.IsTrue(PollUntil(
                    () => viewer.Mirror.Mode is LangitLupa m && m.IsBrownout() && m.Unseen.Contains(farId),
                    server,
                    clients), "viewer mirror knows it may not see far kid");

                var copy = (LangitLupa)viewer.Mirror.Mode;
                Assert.IsFalse(copy.Unseen.Contains(ids[1]), "near kid stays seen");
                Assert.IsFalse(copy.Mounds.ContainsKey(farId), "far kid's mound is stripped for viewer");
                Assert.IsFalse(copy.VisibleTo(viewer.Mirror, viewer.PlayerId, farId));
                Assert.Greater(
                    viewer.Mirror.Players[farId].Position.DistanceTo(new Vec2(8.0, -20.0)),
                    5.0,
                    "viewer mirror never receives far kid's real position");

                GDict redacted = Snapshot.HideFor(running.Sim, viewer.PlayerId, Snapshot.Capture(running.Sim, new OrderedDict<int, int>()));
                bool checkedFarWire = false;
                foreach (object raw in redacted.GetArr("players"))
                {
                    var entry = (GDict)raw;
                    if (entry.GetInt("id", -1) == farId)
                    {
                        checkedFarWire = true;
                        Assert.IsTrue(entry.GetBool("hid"), "wire marks far entry hidden");
                        Assert.AreEqual(Vec2.Zero, entry.GetVec2("pos", new Vec2(99.0, 99.0)), "wire zeroes far player's position");
                    }
                }
                Assert.IsTrue(checkedFarWire);

                Assert.IsTrue(PollUntil(
                    () => clients[2].Mirror.Mode is LangitLupa fm && fm.Mounds.ContainsKey(farId),
                    server,
                    clients), "far kid's own phone sees its mound");

                // Lights return: everyone is seen again and real position syncs.
                game.TimeLeft = game.Rules.MatchSeconds - game.Rules.BrownoutFirst - game.Rules.BrownoutSeconds - 1.0;
                Assert.IsTrue(PollUntil(
                    () => ((LangitLupa)viewer.Mirror.Mode).Unseen.Count == 0,
                    server,
                    clients), "seen again after brownout");
                Assert.IsTrue(PollUntil(
                    () => viewer.Mirror.Players[farId].Position.DistanceTo(running.Sim.Players[farId].Position) < 1.0,
                    server,
                    clients), "viewer mirror catches up to far kid's real position");
            }
            finally
            {
                foreach (GameClient c in clients) c.Close();
                server.Stop();
            }
        }

        [Test]
        public void OpenRoomsRateLimitsBurstHidesPrivateAndStartedRoomsAndRefusesCodeFreeOnlineJoin()
        {
            GameServer server = NewServer(9);
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            server.Attach(transport);
            string url = "ws://127.0.0.1:" + transport.Port;
            GameClient host = NewClient("Ana");
            GameClient guest = NewClient("Ben");
            GameClient late = NewClient("Cy");
            var clients = new List<GameClient> { host, guest, late };
            try
            {
                Assert.IsTrue(host.ConnectTo(url));
                Assert.IsTrue(guest.ConnectTo(url));
                Assert.IsTrue(late.ConnectTo(url));
                Assert.IsTrue(PollUntil(() => clients.All(c => c.IsOnline()), server, clients), "connected");

                host.CreateRoom();
                Assert.IsTrue(PollUntil(() => !host.Room.IsEmpty, server, clients), "host room open");
                string code = host.Room.GetStr("code");

                // Online server refuses code-free join (AutoJoin is false).
                string onlineJoinErr = "";
                late.ErrorReceived += err => onlineJoinErr = err;
                late.JoinRoom("");
                Assert.IsTrue(PollUntil(() => onlineJoinErr.Length > 0, server, clients), "code-free join refused online");
                Assert.IsTrue(late.Room.IsEmpty, "no room joined without its code");

                // Burst of 5 ListRooms calls within RoomListMinGap produces exactly 1 answer.
                var answers = new List<List<GDict>>();
                guest.RoomsListed += rooms => answers.Add(rooms);
                for (int i = 0; i < 5; i++) guest.ListRooms();
                for (int i = 0; i < 15; i++)
                {
                    server.Poll(DT);
                    guest.Poll(DT);
                    Thread.Sleep(2);
                }
                Assert.AreEqual(1, answers.Count, "five asks in a burst, one answer");
                Assert.AreEqual(1, answers[0].Count);
                Assert.AreEqual(code, answers[0][0].GetStr("code"));

                // Private room drops off OpenRooms, yet direct code join still succeeds.
                host.SetOpen(false);
                Assert.IsTrue(PollUntil(() => !host.Room.GetBool("open", true), server, clients), "room marked private");
                for (int i = 0; i < 36; i++)
                {
                    server.Poll(DT);
                    guest.Poll(DT);
                }
                guest.ListRooms();
                Assert.IsTrue(PollUntil(() => answers.Count == 2, server, clients), "second list after min gap");
                Assert.IsEmpty(answers[1], "private room omitted from open list");

                guest.JoinRoom(code);
                Assert.IsTrue(PollUntil(() => !guest.Room.IsEmpty && guest.Room.GetStr("code") == code, server, clients), "private room still joins by code");

                // Reopen and start match: started room drops off list and late joiner gets explicit error.
                host.SetOpen(true);
                Assert.IsTrue(PollUntil(() => host.Room.GetBool("open"), server, clients));
                host.SetReady(true);
                guest.SetReady(true);
                Assert.IsTrue(PollUntil(() => host.Room.GetBool("can_start"), server, clients));
                host.StartMatch();
                Assert.IsTrue(PollUntil(() => host.InMatch() && guest.InMatch(), server, clients), "match started");

                onlineJoinErr = "";
                late.JoinRoom(code);
                Assert.IsTrue(PollUntil(() => onlineJoinErr.Contains("already started"), server, clients), "late joiner told match started");
                Assert.IsTrue(late.Room.IsEmpty);

                var lateLists = new List<List<GDict>>();
                late.RoomsListed += rooms => lateLists.Add(rooms);
                late.ListRooms();
                Assert.IsTrue(PollUntil(() => lateLists.Count == 1, server, clients), "late client gets room list");
                Assert.IsEmpty(lateLists[0], "started room dropped off open rooms list");
            }
            finally
            {
                foreach (GameClient c in clients) c.Close();
                server.Stop();
            }
        }

        [Test]
        public void LoopbackWebSocketMatchRelaysTeammateKidLockWeaponCastsAndQuickChat()
        {
            GameServer server = NewServer(33);
            var transport = WsServerTransport.Listen(0, IPAddress.Loopback);
            server.Attach(transport);
            string url = "ws://127.0.0.1:" + transport.Port;
            GameClient a1 = NewClient("Ally1");
            GameClient a2 = NewClient("Ally2");
            GameClient b1 = NewClient("Rival1");
            var clients = new List<GameClient> { a1, a2, b1 };
            try
            {
                foreach (GameClient c in clients) Assert.IsTrue(c.ConnectTo(url));
                Assert.IsTrue(PollUntil(() => clients.All(c => c.IsOnline()), server, clients), "connected");

                a1.CreateRoom();
                Assert.IsTrue(PollUntil(() => !a1.Room.IsEmpty, server, clients));
                string code = a1.Room.GetStr("code");
                b1.JoinRoom(code);
                Assert.IsTrue(PollUntil(() => b1.PlayerId > 0, server, clients));
                a2.JoinRoom(code);
                Assert.IsTrue(PollUntil(() => a1.Room.GetArr("members").Count == 3 && a2.PlayerId > 0, server, clients));
                Assert.AreEqual(0, a1.MyTeam());
                Assert.AreEqual(0, a2.MyTeam());
                Assert.AreEqual(1, b1.MyTeam());

                foreach (GameClient c in clients) c.SetReady(true);
                Assert.IsTrue(PollUntil(() => a1.Room.GetBool("can_start"), server, clients));
                a1.StartMatch();
                Assert.IsTrue(PollUntil(() => clients.All(c => c.InMatch()), server, clients));
                foreach (GameClient c in clients) c.Loaded();
                Assert.IsTrue(PollUntil(() => clients.All(c => c.Stage == "pick"), server, clients));

                // Ally1 locks "jopay" first; Ally2 learns "jopay" is in TakenKids() and cannot steal it over the wire.
                a1.PickWeapons("bato_light", "lata", "jopay", "papel_shield");
                Assert.IsTrue(PollUntil(() => a2.TakenKids().Contains("jopay"), server, clients), "teammate sees locked kid");
                a2.PickWeapons("bato_light", "lata", "jopay", "papel_shield");
                b1.PickWeapons("jacks", "bola", "jopay", "trumpo");
                Assert.IsTrue(PollUntil(() => clients.All(c => c.Stage == "play"), server, clients), "all entered play");

                Assert.AreEqual("jopay", b1.Mirror.Players[a1.PlayerId].CharacterId);
                Assert.AreNotEqual("jopay", b1.Mirror.Players[a2.PlayerId].CharacterId, "teammate could not steal toni");
                Assert.AreEqual("jopay", b1.Mirror.Players[b1.PlayerId].CharacterId, "opponent could pick toni");

                ServerMatch running = server.Matches.Values.First();
                running.Sim.IntroLeft = 0.0;
                running.Sim.BarrierLeft = 0.0;  // casts are relayed from the first ticks, not after the round-start barrier

                int weaponEvents = 0;
                int stickers = 0;
                int snapshots = 0;
                b1.EventReceived += (target, name, _) => { if (target == "sim" && name == "weapon_cast") weaponEvents++; };
                b1.Said += (_, _) => stickers++;
                b1.SnapshotApplied += () => snapshots++;

                Vec2 start = b1.Mirror.Players[a1.PlayerId].Position;
                int tick = 0;
                Assert.IsTrue(PollUntil(
                    () => snapshots > 15 && weaponEvents > 0 && stickers == 1 && !b1.Mirror.Players[a1.PlayerId].Position.IsEqualApprox(start),
                    server,
                    clients,
                    12.0,
                    () =>
                    {
                        tick++;
                        int btns = (tick == 10 ? PlayerInput.BtnPrimary : 0) | (tick >= 12 && tick <= 14 ? PlayerInput.BtnCard1 : 0);
                        a1.SendInput(PlayerInput.Create(new Vec2(1.0, 0.0), new Vec2(1.0, 0.0), btns, tick), DT);
                        a2.SendInput(PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, tick), DT);
                        b1.SendInput(PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, tick), DT);
                        b1.Interpolate();
                        if (tick == 20) a1.Say(1);
                    }), "snapshots, weapon cast events, chat sticker, and movement over loopback WebSockets");
            }
            finally
            {
                foreach (GameClient c in clients) c.Close();
                server.Stop();
            }
        }
    }
}
