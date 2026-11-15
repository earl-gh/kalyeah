using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// The wire format against unity/Golden/Net/net_protocol.json (recorded by tools/unity_export/export_net.gd from the Godot game):
    /// every message number, every client message byte for byte, the input encoding both ways, bad packets, the codec on every type.
    /// </summary>
    public class ProtocolGoldenTests
    {
        static JsonElement Root;

        [OneTimeSetUp]
        public void Load() => Root = NetKit.Golden("net_protocol.json").RootElement;

        [Test]
        public void VersionTextAndEveryMessageNumberMatchGodot()
        {
            Assert.AreEqual(Root.GetProperty("version").GetInt32(), NetProtocol.Version);
            Assert.AreEqual(Root.GetProperty("incompatible").GetString(), NetProtocol.IncompatibleText);
            int count = 0;
            foreach (JsonProperty msg in Root.GetProperty("msg").EnumerateObject())
            {
                string pascal = string.Concat(msg.Name.Split('_').Select(p => p.Substring(0, 1) + p.Substring(1).ToLowerInvariant()));
                Assert.IsTrue(Enum.TryParse(pascal, out NetProtocol.Msg value), msg.Name);
                Assert.AreEqual(msg.Value.GetInt32(), (int)value, msg.Name);
                count++;
            }
            Assert.AreEqual(Enum.GetValues(typeof(NetProtocol.Msg)).Length, count);
        }

        [Test]
        public void EveryClientCallSendsTheSameBytesAsGodot()
        {
            var transport = new RecordingClientTransport();
            var client = new GameClient(NetKit.Rules, NetKit.Layout, NetKit.Net, NetKit.Games);
            client.Attach(transport);
            client.CreateRoom();
            client.JoinRoom("abcd2345");
            client.ListRooms();
            client.SetOpen(false);
            client.SetFillBots(true);
            client.PickTeam(1);
            client.SetFirstTo(5);
            client.SetMap("patintero");
            client.SetCharacter("toni");
            client.SetReady(true);
            client.StartMatch();
            client.Loaded();
            client.PickWeapons("bato_light", "trumpo", "migo", "lata");
            client.PickWeapons("bato_light", "trumpo");
            client.SwapWeapons("jacks", "bola", "heal");
            client.SwapWeapons("jacks", "bola");
            client.Say(3); // not playing: nothing
            client.Stage = "play";
            client.Say(3);
            client.Mirror = new MatchSim(NetKit.Rules, NetKit.Layout, 5);
            client.Mirror.AddPlayer(1, 0);
            client.PlayerId = 1;
            client.NextInputSequence = 40;
            client.SendInput(PlayerInput.Create(new Vec2(0.3, -0.25), new Vec2(-1.5, 2.0), PlayerInput.BtnDash, 12), 1.0 / 30.0);
            client.SendInput(PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, 100).WithMoveTarget(new Vec2(3.456, -7.891)), 1.0 / 30.0);
            client.LeaveRoom();
            var expected = Root.GetProperty("client_sent").EnumerateArray().Select(e => e.GetString()).ToList();
            CollectionAssert.AreEqual(expected, transport.Sent.Select(Wire.Hex).ToList());
            Assert.AreEqual(Root.GetProperty("client_next_input_sequence").GetInt32(), client.NextInputSequence);
        }

        [Test]
        public void TheServerOnlyMessagesEncodeLikeGodot()
        {
            JsonElement literal = Root.GetProperty("literal");
            var built = new Dictionary<string, GDict>
            {
                ["hello"] = NetProtocol.Message(NetProtocol.Msg.Hello, "name", "Ana", "token", "", "v", (long)NetProtocol.Version),
                ["hello_token"] = NetProtocol.Message(NetProtocol.Msg.Hello, "name", "Ana", "token", "0123abcd4567ef89", "v", (long)NetProtocol.Version),
                ["welcome"] = NetProtocol.Message(NetProtocol.Msg.Welcome, "v", (long)NetProtocol.Version, "reconnected", true, "input_next", 57L),
                ["error"] = NetProtocol.Message(NetProtocol.Msg.Error, "text", NetProtocol.IncompatibleText),
                ["room_closed"] = NetProtocol.Message(NetProtocol.Msg.RoomClosed),
                ["discover"] = NetProtocol.Message(NetProtocol.Msg.DiscoverRoom, "game", "kalyeah", "v", (long)NetProtocol.Version),
                ["said"] = NetProtocol.Message(NetProtocol.Msg.Said, "p", 4L, "s", 2L),
            };
            foreach (var pair in built) Assert.AreEqual(literal.GetProperty(pair.Key).GetString(), NetKit.Hex(pair.Value), pair.Key);
        }

        static float F(JsonElement e) => (float)e.GetDouble();

        [Test]
        public void InputToDictMatchesGodot()
        {
            int checkedCount = 0;
            foreach (JsonElement c in Root.GetProperty("input_to_dict").EnumerateArray())
            {
                JsonElement[] f = c.GetProperty("in").EnumerateArray().ToArray();
                long tick = f[5].GetInt64();
                if (tick > int.MaxValue) continue; // PlayerInput.Tick is an int in the port (Godot: 64-bit); such ticks never happen
                var input = PlayerInput.Create(new Vec2(F(f[0]), F(f[1])), new Vec2(F(f[2]), F(f[3])), f[4].GetInt32(), (int)tick);
                input.MoveTarget = new Vec2(F(f[6]), F(f[7]));
                input.AimPoint = new Vec2(F(f[8]), F(f[9]));
                Assert.AreEqual(c.GetProperty("hex").GetString(), NetKit.Hex(NetProtocol.InputToDict(input)), string.Join(",", f));
                checkedCount++;
            }
            Assert.GreaterOrEqual(checkedCount, 6);
        }

        [Test]
        public void InputFromDictCleansUntrustedInputLikeGodot()
        {
            foreach (JsonElement c in Root.GetProperty("input_from_dict").EnumerateArray())
            {
                GDict message = NetKit.Decode(c.GetProperty("hex").GetString());
                PlayerInput input = NetProtocol.InputFromDict(message);
                JsonElement[] o = c.GetProperty("out").EnumerateArray().ToArray();
                string what = c.GetProperty("hex").GetString();
                Assert.AreEqual(F(o[0]), input.Move.X, what);
                Assert.AreEqual(F(o[1]), input.Move.Y, what);
                Assert.AreEqual(F(o[2]), input.Aim.X, what);
                Assert.AreEqual(F(o[3]), input.Aim.Y, what);
                Assert.AreEqual(o[4].GetInt32(), input.Buttons, what);
                Assert.AreEqual(unchecked((int)o[5].GetInt64()), input.Tick, what);
                Assert.AreEqual(F(o[6]), input.MoveTarget.X, what);
                Assert.AreEqual(F(o[7]), input.MoveTarget.Y, what);
                Assert.AreEqual(F(o[8]), input.AimPoint.X, what);
                Assert.AreEqual(F(o[9]), input.AimPoint.Y, what);
            }
        }

        [Test]
        public void DecodingAcceptsAndRefusesWhatGodotDoesAndReencodesTheSameBytes()
        {
            foreach (JsonElement c in Root.GetProperty("decode").EnumerateArray())
            {
                string hex = c.GetProperty("hex").GetString();
                GDict message = NetProtocol.Decode(Wire.FromHex(hex));
                Assert.AreEqual(c.GetProperty("ok").GetBoolean(), !message.IsEmpty, hex);
                if (!message.IsEmpty) Assert.AreEqual(c.GetProperty("again").GetString(), NetKit.Hex(message), hex);
            }
        }

        [Test]
        public void SnappedfIsGodots()
        {
            foreach (JsonElement c in Root.GetProperty("snapped").EnumerateArray())
            {
                JsonElement[] v = c.EnumerateArray().ToArray();
                Assert.AreEqual(v[2].GetDouble(), Wire.Snapped(v[0].GetDouble(), v[1].GetDouble()));
            }
        }

        [Test]
        public void AvatarsAndRankTiersAreTheGames()
        {
            CollectionAssert.AreEqual(Root.GetProperty("avatars").EnumerateArray().Select(e => e.GetString()).ToArray(), Lobby.Avatars);
            foreach (JsonElement c in Root.GetProperty("tiers").EnumerateArray())
                Assert.AreEqual(c[1].GetString(), Lobby.TierName(c[0].GetInt64()));
        }
    }
}
