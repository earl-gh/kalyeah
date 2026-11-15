using System.Collections.Generic;
using System.Linq;
using Kalyeah.Net;
using NUnit.Framework;

namespace Kalyeah.UI.Tests
{
    public class RoomViewModelTests
    {
        static GDict Room(int host, params GDict[] members) => GDict.Of(
            "code", "ABCD2345", "team_size", 5L, "host", (long)host, "first_to", 5L, "members", GArray.Of(members), "can_start_host", false,
            "start_note", "Waiting for 1 player to ready up.", "map", new StringName("street"), "open", false, "fill_bots", false, "bots_allowed", false);

        static GDict Member(int id, string name, int team, bool ready, string kid) =>
            GDict.Of("player_id", (long)id, "name", name, "team", (long)team, "ready", ready, "connected", true, "char", new StringName(kid));

        [Test]
        public void TheHostSeesStartAndTheReasonItIsBlocked()
        {
            GDict room = Room(1, Member(1, "Ana", 0, false, "junjun"), Member(2, "Ben", 1, true, "junjun"), Member(3, "Cy", 0, false, "migo"));
            RoomViewModel host = RoomViewModel.From(room, 1, true, UiKit.Menu);
            Assert.IsTrue(host.IsHost);
            Assert.AreEqual("ABCD2345", host.Code);
            Assert.AreEqual("Start battle", host.ActionText);
            Assert.IsFalse(host.ActionHot, "grey until the game can start");
            Assert.AreEqual("Waiting for 1 player to ready up.", host.Readiness);
            Assert.IsTrue(host.ShowRace);
            Assert.AreEqual(5, host.FirstTo);
            Assert.IsTrue(host.ShowOpenSwitch);
            Assert.AreEqual("", host.Summary);
            Assert.AreEqual(5, host.Teams[0].Count);
            Assert.AreEqual("You · Host", host.Teams[0][0].Tag);
            Assert.AreEqual(1, host.Teams[0][0].Number);
            Assert.AreEqual("Cy", host.Teams[0][1].Name);
            Assert.AreEqual("Ready", host.Teams[1][0].Tag);
            Assert.AreEqual(6, host.Teams[1][0].Number);
            Assert.IsTrue(host.Teams[1][1].IsOpen);
            Assert.IsTrue(host.Teams[1][1].CanJoin, "an open slot on the other team moves you there");
            Assert.IsFalse(host.Teams[0][2].CanJoin);
            Assert.IsTrue(host.Kids.Single(k => k.Id == "migo").Taken, "a teammate plays Migo");
            Assert.IsFalse(host.Kids.Single(k => k.Id == "junjun").Taken, "only the other team plays Junjun");
            Assert.IsTrue(host.Kids.Single(k => k.Id == "junjun").Picked);
        }

        [Test]
        public void AGuestSeesReadyAndTheRoomsSummary()
        {
            GDict room = Room(1, Member(1, "Ana", 0, false, "junjun"), Member(2, "Ben", 1, true, "toni"));
            RoomViewModel guest = RoomViewModel.From(room, 2, true, UiKit.Menu);
            Assert.IsFalse(guest.IsHost);
            Assert.AreEqual("Cancel ready", guest.ActionText);
            Assert.IsFalse(guest.ActionHot);
            Assert.AreEqual("Private", guest.Summary);
            Assert.IsFalse(guest.ShowOpenSwitch);
            RoomViewModel local = RoomViewModel.From(room, 2, false, UiKit.Menu);
            Assert.AreEqual("", local.Code, "a Local room has no code");
            Assert.AreEqual("", local.Summary);
        }

        [Test]
        public void AStreetGameHasNoRaceAndTumbangPresoNoTeams()
        {
            GDict room = Room(1, Member(1, "Ana", 0, false, "junjun"));
            room["map"] = new StringName("tumbang_preso");
            RoomViewModel view = RoomViewModel.From(room, 1, true, UiKit.Menu);
            Assert.IsFalse(view.ShowRace);
            Assert.IsTrue(view.FreeForAll);
            Assert.AreEqual("Tumbang Preso", view.MapName);
            room["map"] = new StringName("patintero");
            Assert.IsFalse(RoomViewModel.From(room, 1, true, UiKit.Menu).FreeForAll);
        }

        [Test]
        public void OpenRoomCardsReadLikeTheGodotList()
        {
            GDict room = GDict.Of("code", "X", "host_name", "Ana", "team_size", 5L, "players", 3L, "first_to", 3L, "map", new StringName("street"));
            Assert.AreEqual("Ana's room\n5v5 · 3/10 · Race to 3", MenuCatalog.OpenRoomText(room, UiKit.Menu));
            room["map"] = new StringName("tumbang_preso");
            room["host_name"] = "";
            Assert.AreEqual("Open room\n3/10 · Tumbang Preso", MenuCatalog.OpenRoomText(room, UiKit.Menu));
            Assert.AreEqual("Gabo's room  1/6", MenuCatalog.LocalRoomText(new HotspotFinder.FoundRoom { Host = "Gabo", Players = 1, Size = 6 }));
            Assert.AreEqual("A room", MenuCatalog.LocalRoomText(new HotspotFinder.FoundRoom()));
        }

        [Test]
        public void PracticeWordsFollowTheGame()
        {
            Assert.AreEqual("5 v 5 against Barangay bots! Ready to rumble?", MenuCatalog.PracticeWords(UiKit.Menu, "street"));
            StringAssert.StartsWith("You against 9 bots.", MenuCatalog.PracticeWords(UiKit.Menu, "tumbang_preso"));
            StringAssert.StartsWith("5 v 5 against bots. 5 minutes", MenuCatalog.PracticeWords(UiKit.Menu, "patintero"));
            Assert.AreEqual("Patintero", MenuCatalog.PracticeTitle(UiKit.Menu, "patintero"));
            Assert.AreEqual("5v5 · Teams · Race to 3", MenuCatalog.MapInfo(UiKit.Menu, "street", 5, 3));
            StringAssert.Contains("2 or more a team", MenuCatalog.MapInfo(UiKit.Menu, "patintero", 5, 3));
        }

        [Test]
        public void TheDataGivesKidsCardsAndDifficulties()
        {
            Assert.AreEqual("Junjun", UiKit.Menu.KidName("junjun"));
            Assert.AreEqual("Nobody", UiKit.Menu.KidName("nobody"));
            Assert.Greater(UiKit.Menu.Cards().Count, 5);
            Assert.AreEqual(3, UiKit.Menu.Rules.BotLevels.Count);
            Assert.AreEqual(UiKit.Menu.Rules.BotLevelDefault, UiKit.Menu.BotLevel("").Id, "none chosen yet: the default");
            Assert.AreEqual("hard", UiKit.Menu.BotLevel("hard").Id);
            foreach (string kid in SessionModel.Avatars) Assert.IsTrue(MenuCatalog.Kids.ContainsKey(kid), kid);
        }
    }

    public class CodeEntryTests
    {
        [Test]
        public void TheBoxesTakeOnlyTheCodeAlphabet()
        {
            var code = new CodeEntry(UiKit.Menu.Net.PasscodeAlphabet, UiKit.Menu.Net.PasscodeLength);
            Assert.AreEqual(8, code.Length);
            code.Set("ab-cd 2345xyz");
            Assert.AreEqual(code.Clean("ABCD2345XYZ"), code.Text);
            Assert.LessOrEqual(code.Text.Length, 8);
            Assert.AreEqual("Nothing to paste yet. Copy the room code first!", code.Paste("!!"));
            code.Set("");
            Assert.AreEqual("Enter all 8 characters!", code.MissingText());
            Assert.AreEqual(0, code.Cursor);
            StringAssert.StartsWith("Pasted ", code.Paste(code.Alphabet.Substring(0, 3)));
            Assert.AreEqual(3, code.Cursor);
            code.Refuse("That room doesn't exist!");
            Assert.IsTrue(code.Error);
            code.Set(code.Text + code.Alphabet[0]);
            Assert.IsFalse(code.Error, "editing clears the red");
            Assert.AreEqual("Code pasted!", code.Paste(code.Alphabet.Substring(0, 8).ToLowerInvariant()));
            Assert.IsTrue(code.Complete);
            Assert.AreEqual(7, code.Cursor);
        }
    }

    public class LobbyFlowTests
    {
        static SessionModel Session(string name, string kid)
        {
            var session = new SessionModel(new ConfigStore(null)) { PlayerName = name, AvatarId = kid };
            return session;
        }

        [Test]
        public void ALocalHostOpensItsRoomAndAGuestJoinsAndTheMatchStarts()
        {
            var rig = new LocalRig();
            SessionModel session = Session("Ana", "tala");
            var flow = new LobbyFlow(rig.HostPhone, session, new SettingsModel(new ConfigStore(null)), true, SessionModel.LobbyIntent.Create, () => 7);
            var statuses = new List<string>();
            flow.StatusChanged += statuses.Add;
            GDict started = null;
            flow.MatchLoading += info => started = info;
            flow.Begin();
            Assert.IsTrue(rig.HostPhone.IsHosting);
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room != null, () => { rig.Poll(); flow.Tick(1.0 / 30.0); }));
            CollectionAssert.Contains(statuses, "Starting the room…");
            Assert.IsTrue(flow.Room.IsHost);
            Assert.AreEqual("", flow.Room.Code, "Local rooms have no code");
            Assert.IsTrue(flow.Room.ShowBots, "a Local room may fill with bots");
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.MyKid == "tala", () => { rig.Poll(); flow.Tick(1.0 / 30.0); }), "the kid is announced");

            GameClient guest = rig.Guest("Ben");
            Assert.IsTrue(guest.ConnectTo(rig.HostPhone.HostUrl()));
            Assert.IsTrue(UiKit.PollUntil(() => guest.IsOnline(), rig.Poll));
            guest.JoinRoom("");
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.Teams[1][0].Name == "Ben", () => { rig.Poll(); flow.Tick(1.0 / 30.0); }));

            var toasts = new List<string>();
            flow.Toast += (text, kind) => toasts.Add(text);
            flow.PressAction();
            Assert.AreEqual(1, toasts.Count, "Ben is not ready: the host is told why and nothing is sent");
            Assert.IsNull(started);

            guest.SetReady(true);
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.StartNote.Length == 0, () => { rig.Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.IsTrue(flow.Room.ActionHot);
            flow.PressAction();
            Assert.IsTrue(UiKit.PollUntil(() => started != null, () => { rig.Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.IsTrue(flow.Loading);
            Assert.IsTrue(rig.HostPhone.Client.InMatch(), "the client's mirror is ready for the match screen");
            Assert.IsNotEmpty(session.ReconnectToken, "the slot's token is kept to rejoin");
            flow.Dispose();
        }

        [Test]
        public void TheHostChangesTheGameAndEveryoneHearsIt()
        {
            var rig = new LocalRig();
            var flow = new LobbyFlow(rig.HostPhone, Session("Ana", "tala"), new SettingsModel(new ConfigStore(null)), true, SessionModel.LobbyIntent.Create, () => 7);
            var toasts = new List<string>();
            flow.Toast += (text, kind) => toasts.Add(text);
            flow.Begin();
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room != null, rig.Poll));
            flow.PickMap("patintero");
            flow.SetFirstTo(7);
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.MapId == "patintero", rig.Poll));
            CollectionAssert.Contains(toasts, "You picked Patintero!");
            Assert.IsFalse(flow.Room.ShowRace);
            flow.PickMap("street");
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.MapId == "street" && flow.Room.FirstTo == 7, rig.Poll));
            flow.SetFillBots(true);
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.FillBots, rig.Poll));
            flow.Leave();
            Assert.IsFalse(rig.HostPhone.IsHosting);
            Assert.IsNull(flow.Room);
            Assert.AreNotEqual(MenuCatalog.ConnectionLost, flow.Status, "leaving is not a lost connection");
            flow.Dispose();
        }

        [Test]
        public void OnlineJoinListsOpenRoomsAndJoinsOneWithATap()
        {
            var hub = new MemoryHub();
            var server = new GameServer(UiKit.Menu.Rules, UiKit.Menu.Layout, UiKit.Menu.Net, 3, UiKit.Menu.Games);
            server.Attach(hub);
            int peer = 50;
            var settings = new SettingsModel(new ConfigStore(null)) { ServerUrl = "kalyeah.example.com" };

            GameClient ana = UiKit.Menu.NewClient();
            ana.PlayerName = "Ana";
            ana.TransportFactory = (url, t) => hub.Connect(peer++);
            Assert.IsTrue(ana.ConnectTo("wss://kalyeah.example.com"));
            var net = new NetHub(UiKit.Menu) { UseBeacon = false };
            string asked = "";
            net.Client.TransportFactory = (url, t) =>
            {
                asked = url;
                return hub.Connect(peer++);
            };
            void Poll()
            {
                server.Poll(1.0 / 30.0);
                ana.Poll(1.0 / 30.0);
                net.Poll(1.0 / 30.0);
            }
            Assert.IsTrue(UiKit.PollUntil(ana.IsOnline, Poll));
            ana.CreateRoom();
            Assert.IsTrue(UiKit.PollUntil(() => !ana.Room.IsEmpty, Poll));

            var flow = new LobbyFlow(net, Session("Ben", "migo"), settings, false, SessionModel.LobbyIntent.Join, () => 1);
            List<GDict> listed = null;
            flow.RoomsListed += rooms => listed = rooms;
            flow.Begin();
            Assert.AreEqual("wss://kalyeah.example.com", asked, "the build's server, normalised");
            Assert.IsTrue(flow.Browsing);
            Assert.IsTrue(UiKit.PollUntil(() => listed != null && listed.Count == 1, () => { Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.AreEqual("Ana's room\n5v5 · 1/10 · Race to 3", MenuCatalog.OpenRoomText(listed[0], UiKit.Menu));
            flow.JoinListed(listed[0].GetStr("code"));
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room != null, () => { Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.IsFalse(flow.Browsing);
            Assert.AreEqual(ana.Room.GetStr("code"), flow.Room.Code);
            Assert.AreEqual("Ready", flow.Room.ActionText);
            flow.PressAction();
            Assert.IsTrue(UiKit.PollUntil(() => flow.Room.MyReady, () => { Poll(); flow.Tick(1.0 / 30.0); }));
            Assert.AreEqual("Cancel ready", flow.Room.ActionText);
            flow.Dispose();
        }

        [Test]
        public void AWrongCodeTurnsTheBoxesRed()
        {
            var hub = new MemoryHub();
            var server = new GameServer(UiKit.Menu.Rules, UiKit.Menu.Layout, UiKit.Menu.Net, 3, UiKit.Menu.Games);
            server.Attach(hub);
            var net = new NetHub(UiKit.Menu) { UseBeacon = false };
            int peer = 70;
            net.Client.TransportFactory = (url, t) => hub.Connect(peer++);
            var settings = new SettingsModel(new ConfigStore(null)) { ServerUrl = "wss://x" };
            var flow = new LobbyFlow(net, Session("Ben", "migo"), settings, false, SessionModel.LobbyIntent.Join, () => 1);
            string refused = null;
            flow.CodeRefused += text => refused = text;
            flow.Begin();
            flow.JoinCode("abc");
            Assert.AreEqual("Enter all 8 characters!", refused);
            refused = null;
            string code = new string(UiKit.Menu.Net.PasscodeAlphabet[0], 8);
            flow.JoinCode(code);
            Assert.IsTrue(UiKit.PollUntil(() => refused != null, () => { server.Poll(1.0 / 30.0); net.Poll(1.0 / 30.0); flow.Tick(1.0 / 30.0); }));
            Assert.IsTrue(flow.Code.Error);
            Assert.AreEqual(flow.Code.ErrorText, flow.Status, "the server's reason shows under the boxes");
            flow.Dispose();
        }

        [Test]
        public void WithNoServerOnlineIsComingSoon()
        {
            var flow = new LobbyFlow(new NetHub(UiKit.Menu), Session("Ben", "migo"), new SettingsModel(new ConfigStore(null)), false,
                SessionModel.LobbyIntent.Create, () => 1);
            flow.Begin();
            Assert.AreEqual(MenuCatalog.ComingSoon, flow.Status);
            flow.Dispose();
        }

        [TestCase("Connecting…", "wait")]
        [TestCase("Connection lost. Try again!", "error")]
        [TestCase("That room is full!", "error")]
        [TestCase("Ready to play", "info")]
        public void StatusKinds(string text, string kind) => Assert.AreEqual(kind, LobbyFlow.KindOf(text));
    }
}
