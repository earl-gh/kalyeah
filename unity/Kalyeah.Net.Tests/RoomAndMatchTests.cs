using System.Collections.Generic;
using System.Linq;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>Ports of tests/unit/test_room_service.gd, test_open_rooms.gd, test_fill_bots.gd, test_afk.gd and test_net_hardening.gd.</summary>
    public class RoomAndMatchTests
    {
        const double DT = 1.0 / 30.0;

        static bool Move(RoomService service, int peer, int team)
        {
            foreach (string avatar in Lobby.Avatars)
                if (service.SetCharacter(peer, avatar) && service.PickTeam(peer, team)) return true;
            return false;
        }

        // ---- test_room_service.gd ----

        [Test]
        public void PasscodeIsEightUnambiguousCharactersAndUnique()
        {
            RoomService service = NetKit.Service();
            for (int peer = 1; peer < 60; peer++)
            {
                RoomService.Room room = service.CreateRoom(peer, "p");
                Assert.AreEqual(8, room.Code.Length);
                foreach (char c in room.Code)
                {
                    StringAssert.Contains(c.ToString(), NetKit.Net.PasscodeAlphabet);
                    Assert.IsFalse("ILO01".Contains(c));
                }
            }
            Assert.AreEqual(59, service.Rooms.Count);
        }

        [Test]
        public void JoinersFillTheSmallerTeamUpToFiveAndCodesAreCaseInsensitive()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(10, "Host");
            Assert.AreEqual(RoomService.Result.Ok, service.Join(room.Code.ToLowerInvariant(), 11, "B"));
            for (int peer = 12; peer < 20; peer++) Assert.AreEqual(RoomService.Result.Ok, service.Join(room.Code, peer, "P" + peer));
            Assert.AreEqual(5, room.TeamCount(0));
            Assert.AreEqual(5, room.TeamCount(1));
            Assert.AreEqual(RoomService.Result.Full, service.Join(room.Code, 20, "K"));
            Assert.AreEqual(RoomService.Result.NotFound, service.Join("ZZZZZZZZ", 21, "L"));
            Assert.AreEqual(5L, service.Describe(room)["team_size"]);
        }

        [Test]
        public void NamesAreTrimmedAndNeverEmpty()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(1, "   ");
            Assert.AreEqual("Player", room.Members[0].Name);
            service.Join(room.Code, 2, "A very long name indeed");
            Assert.AreEqual(NetKit.Net.MaxNameLength, room.Members[1].Name.Length);
        }

        [Test]
        public void AgawangBaseNeedsSomeoneOnEachTeamAndOnlyTheHostStarts()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(1, "A");
            service.SetReady(1, true);
            Assert.AreEqual("Agawang Base needs at least 1 player on each team.", service.StartBlocker(room));
            service.Join(room.Code, 2, "B");
            service.SetReady(2, true);
            Assert.IsTrue(service.CanStart(room));
            Assert.IsFalse(service.Start(2));
            Assert.IsTrue(service.Start(1));
            Assert.AreEqual(RoomService.State.Loading, room.State);
            Assert.AreEqual(RoomService.Result.InProgress, service.Join(room.Code, 3, "C"));
        }

        [Test]
        public void TumbangPresoNeedsThreeKidsAndPatinteroTwoATeam()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(1, "A");
            service.SetMap(1, "tumbang_preso");
            service.SetReady(1, true);
            Assert.AreEqual("Tumbang Preso needs at least 3 players.", service.StartBlocker(room));
            service.Join(room.Code, 2, "B");
            service.Join(room.Code, 3, "C");
            service.SetReady(2, true);
            Assert.AreEqual("Waiting for 1 player to ready up.", service.StartBlocker(room));
            service.SetMap(1, "patintero");
            Assert.AreEqual("Patintero needs at least 2 players on each team.", service.StartBlocker(room));
        }

        [Test]
        public void TheHostLeavingHandsOverAndAnEmptyRoomCloses()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(1, "A");
            service.Join(room.Code, 2, "B");
            int b = room.Members[1].PlayerId;
            service.Disconnected(1);
            Assert.AreEqual(b, room.HostPlayerId);
            service.Leave(2);
            Assert.IsFalse(service.Rooms.ContainsKey(room.Code));
        }

        static RoomService.Room Started(RoomService service)
        {
            RoomService.Room room = service.CreateRoom(1, "A");
            service.Join(room.Code, 2, "B");
            service.SetReady(1, true);
            service.SetReady(2, true);
            Assert.IsTrue(service.Start(1));
            return room;
        }

        [Test]
        public void ADroppedPlayerReconnectsWithinTheWindowWithTheirToken()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = Started(service);
            RoomService.Member b = room.Members[1];
            service.Disconnected(2);
            Assert.IsFalse(b.Connected);
            service.Step(59.0);
            Assert.IsNull(service.Reconnect("wrong", 9));
            Assert.AreSame(b, service.Reconnect(b.Token, 9));
            Assert.AreSame(b, service.MemberOfPeer(9));
        }

        [Test]
        public void TheTokenExpiresButTheMatchGoesOn()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = Started(service);
            string token = room.Members[1].Token;
            service.Disconnected(2);
            service.Step(61.0);
            Assert.IsNull(service.Reconnect(token, 9));
            Assert.IsTrue(service.Rooms.ContainsKey(room.Code));
        }

        [Test]
        public void OnlyTheHostPicksFirstToAndTheMap()
        {
            RoomService service = NetKit.Service();
            service.FirstToChoices = new List<int> { 3, 5, 7 };
            RoomService.Room room = service.CreateRoom(10, "Host");
            service.Join(room.Code, 11, "Guest");
            Assert.IsFalse(service.SetFirstTo(11, 5));
            Assert.IsTrue(service.SetFirstTo(10, 7));
            Assert.IsFalse(service.SetFirstTo(10, 4));
            Assert.IsFalse(service.SetMap(11, "street"));
            Assert.IsFalse(service.SetMap(10, "nowhere"));
            foreach (string map in RoomService.PlayableMapIds) Assert.IsTrue(service.SetMap(10, map), map);
            Assert.AreEqual(new StringName("langit_lupa"), service.Describe(room)["map"]);
        }

        [Test]
        public void TeammatesCannotShareAKidAndChangingIt_unreadies()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = service.CreateRoom(1, "A");
            service.Join(room.Code, 2, "B");
            service.Join(room.Code, 3, "C");
            Assert.IsTrue(service.SetCharacter(1, "toni"));
            Assert.IsFalse(service.SetCharacter(3, "toni"), "C is on A's team");
            Assert.IsTrue(service.SetCharacter(2, "toni"), "the other team may");
            service.SetReady(2, true);
            Assert.IsTrue(service.SetCharacter(2, "nene"));
            Assert.IsFalse(room.Members[1].Ready);
            Assert.IsFalse(service.SetCharacter(1, "nobody"));
        }

        // ---- test_open_rooms.gd ----

        [Test]
        public void OpenRoomsListTheFullestFirstAndLeaveOutPrivateStartedAndFull()
        {
            RoomService service = NetKit.Service();
            RoomService.Room a = service.CreateRoom(1, "A");
            RoomService.Room b = service.CreateRoom(2, "B");
            service.Join(b.Code, 3, "b2");
            RoomService.Room c = service.CreateRoom(4, "C");
            service.SetOpen(4, false);
            GArray listed = service.OpenRooms(30);
            CollectionAssert.AreEqual(new[] { b.Code, a.Code }, listed.Cast<GDict>().Select(r => r.GetStr("code")).ToArray());
            GDict card = (GDict)listed[0];
            Assert.AreEqual("B", card.GetStr("host_name"));
            Assert.AreEqual(2, card.GetInt("players"));
            Assert.AreEqual(1, service.OpenRooms(1).Count);
            Assert.IsFalse(service.SetOpen(3, true), "a guest cannot open or close");
            Assert.AreEqual(RoomService.Result.Ok, service.Join(c.Code, 5, "private still joins by code"));
        }

        // ---- test_fill_bots.gd ----

        static RoomService BotService(bool allowed = true)
        {
            RoomService service = NetKit.Service(3);
            service.BotsAllowed = allowed;
            return service;
        }

        static RoomService.Room RoomFor(RoomService service, string map, int friends)
        {
            RoomService.Room room = service.CreateRoom(1, "Host");
            room.MapId = map;
            for (int i = 0; i < friends; i++) service.Join(room.Code, 2 + i, "F" + i);
            return room;
        }

        [Test]
        public void OnlyTheHostOfAHotspotServerSwitchesBotsOn()
        {
            RoomService service = BotService();
            RoomService.Room room = RoomFor(service, "street", 1);
            Assert.IsFalse(service.SetFillBots(2, true));
            Assert.IsTrue(service.SetFillBots(1, true));
            Assert.IsTrue(room.FillBots);
            RoomService online = BotService(false);
            RoomFor(online, "street", 0);
            Assert.IsFalse(online.SetFillBots(1, true));
            var server = new GameServer(NetKit.Rules, NetKit.Layout, NetKit.Net, 1, NetKit.Games);
            Assert.IsFalse(server.Rooms.BotsAllowed);
            server.AutoJoin = true;
            Assert.IsTrue(server.Rooms.BotsAllowed);
        }

        [Test]
        public void WithBotsNoGameIsShortOfPlayersAndTeamsFillToFive()
        {
            foreach (string map in RoomService.PlayableMapIds)
            {
                RoomService service = BotService();
                RoomService.Room room = RoomFor(service, map, 0);
                Assert.IsNotEmpty(service.StartBlocker(room, true), map);
                service.SetFillBots(1, true);
                Assert.AreEqual("", service.StartBlocker(room, true), map);
            }
            RoomService s = BotService();
            RoomService.Room r = RoomFor(s, "street", 1);
            CollectionAssert.IsEmpty(s.BotTeams(r));
            s.SetFillBots(1, true);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, s.BotTeams(r));
            r.MapId = "tumbang_preso";
            Assert.AreEqual(8, s.BotTeams(r).Count);
        }

        static ServerMatch BotMatch(string map, int friends)
        {
            RoomService service = BotService();
            RoomService.Room room = RoomFor(service, map, friends);
            service.SetFillBots(1, true);
            return new ServerMatch(null, service, room, NetKit.Rules, NetKit.Layout, NetKit.Net, 5);
        }

        [Test]
        public void TheMatchHasTenKidsTheBotsAreNotMembersAndTheyPlayEveryGame()
        {
            foreach (string map in RoomService.PlayableMapIds)
            {
                ServerMatch running = BotMatch(map, 0);
                Assert.AreEqual(10, running.Sim.Players.Count, map);
                Assert.AreEqual(1, running.Room.Members.Count);
                Assert.AreEqual(9, running.Bots.Count);
                Assert.IsTrue(running.Bots.Keys.All(id => id >= RoomService.BotIdBase));
                var names = running.Info().GetArr("players").Cast<GDict>().Select(p => p.GetStr("name")).ToList();
                CollectionAssert.Contains(names, "Bot 1");
                CollectionAssert.Contains(names, "Host");
                running.CurrentStage = ServerMatch.Stage.Playing;
                running.Room.State = RoomService.State.Playing;
                running.Sim.BeginIntro();
                var start = running.Bots.Keys.ToDictionary(id => id, id => running.Sim.Players[id].Position);
                bool moved = false;
                for (int i = 0; i < 900 && !moved; i++)
                {
                    running.Poll(DT);
                    moved = running.Sim.Phase == MatchSim.MatchPhase.Playing
                        && running.Bots.Keys.Any(id => !running.Sim.Players[id].Position.IsEqualApprox(start[id]));
                }
                Assert.IsTrue(moved, map + ": the bots run");
                running.Dispose();
            }
        }

        // ---- test_afk.gd ----

        [Test]
        public void AWholeTeamAwayForfeitsTheMatch()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = Started(service);
            var running = new ServerMatch(null, service, room, NetKit.Rules, NetKit.Layout, NetKit.Net, 9);
            running.CurrentStage = ServerMatch.Stage.Playing;
            service.Disconnected(2);
            Assert.AreEqual(0, service.Step(NetKit.Net.AfkAfter - 1.0).Count);
            Assert.AreEqual(1, service.Step(1.0).Count, "away once");
            Assert.AreEqual(0, service.Step(1.0).Count);
            running.Poll(DT);
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, running.Sim.Phase);
            Assert.AreEqual(0, running.Sim.Score.Winner);
        }

        // ---- test_net_hardening.gd ----

        [Test]
        public void HelloFromAnotherVersionIsRefusedWithTheUpdateText()
        {
            var server = new GameServer(NetKit.Rules, NetKit.Layout, NetKit.Net, 1, NetKit.Games);
            var transport = new RecordingTransport();
            server.Attach(transport);
            server.Handle(5, NetProtocol.Message(NetProtocol.Msg.Hello, "name", "Old", "v", 2L));
            GDict answer = NetProtocol.Decode(transport.Sent.Single().Packet);
            Assert.AreEqual((long)NetProtocol.Msg.Error, answer["t"]);
            Assert.AreEqual(NetProtocol.IncompatibleText, answer["text"]);
            transport.Sent.Clear();
            server.Handle(5, NetProtocol.Message(NetProtocol.Msg.Hello, "name", "New", "v", (long)NetProtocol.Version));
            Assert.AreEqual((long)NetProtocol.Msg.Welcome, NetProtocol.Decode(transport.Sent.Single().Packet)["t"]);
        }

        [Test]
        public void APickDuringPlayChangesNothing()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = Started(service);
            var running = new ServerMatch(null, service, room, NetKit.Rules, NetKit.Layout, NetKit.Net, 9);
            running.CurrentStage = ServerMatch.Stage.Playing;
            int id = room.Members[0].PlayerId;
            var before = running.Sim.Players[id].Weapons.ToList();
            running.Pick(id, "heal", "lata");
            CollectionAssert.AreEqual(before, running.Sim.Players[id].Weapons);
        }

        [Test]
        public void AWelcomeFromAnotherVersionShowsTheUpdateTextAndDoesNotWelcome()
        {
            var client = new GameClient(NetKit.Rules, NetKit.Layout, NetKit.Net, NetKit.Games);
            string error = null;
            bool welcomed = false;
            client.ErrorReceived += text => error = text;
            client.Welcomed += _ => welcomed = true;
            client.Handle(NetProtocol.Message(NetProtocol.Msg.Welcome, "v", 2L, "reconnected", false));
            Assert.AreEqual(NetProtocol.IncompatibleText, error);
            Assert.IsFalse(welcomed);
        }

        // ---- test_tap_move_net.gd ----

        [Test]
        public void ARepeatedInputDoesNotGiveTheMoveOrderAgainAndADroppedPlayerStands()
        {
            RoomService service = NetKit.Service();
            RoomService.Room room = Started(service);
            var running = new ServerMatch(null, service, room, NetKit.Rules, NetKit.Layout, NetKit.Net, 9);
            running.CurrentStage = ServerMatch.Stage.Playing;
            int id = room.Members[0].PlayerId;
            running.QueueInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnDash | PlayerInput.BtnPrimary, 1));
            running.Poll(DT);
            running.Poll(DT);
            PlayerInput repeated = running.Sim.Inputs[id];
            Assert.IsFalse(repeated.IsPressed(PlayerInput.BtnDash), "one-shot order not repeated");
            Assert.IsTrue(repeated.IsPressed(PlayerInput.BtnPrimary), "held button stays held");
            running.QueueInput(id, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 1));
            Assert.AreEqual(2, running.NextInputSequence(id), "an old input is not queued again");
        }
    }
}
