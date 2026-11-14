using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// RoomService replays the sequences the Godot game ran (net_rooms.json): create, join, teams, ready, start blockers, bots, maps,
    /// leave, reconnect, AFK, open-room lists, codes, names. Every result, every ROOM description (bytes) and every member's private
    /// state (ids, peers, tokens, timers) must match.
    /// </summary>
    public class RoomGoldenTests
    {
        public static IEnumerable<string> Sequences()
        {
            using JsonDocument doc = NetKit.Golden("net_rooms.json");
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        }

        [Test]
        public void EverySequenceIsRecorded() =>
            CollectionAssert.IsSupersetOf(Sequences(), new[] { "basic", "maps", "bots", "leave", "reconnect_afk", "open_rooms", "codes", "names_ranks_chars" });

        [TestCaseSource(nameof(Sequences))]
        public void ReplaysLikeGodot(string name)
        {
            using JsonDocument doc = NetKit.Golden("net_rooms.json");
            RoomService service = null;
            var created = new List<RoomService.Room>();
            int index = 0;
            foreach (JsonElement step in doc.RootElement.GetProperty(name).EnumerateArray())
            {
                JsonElement[] op = step.GetProperty("op").EnumerateArray().ToArray();
                string where = name + " #" + index++ + " " + step.GetProperty("op");
                if (op[0].GetString() == "service")
                {
                    service = NetKit.Service(op[1].GetInt64());
                    service.BotsAllowed = op[2].GetBoolean();
                    service.FirstToChoices = op[3].EnumerateArray().Select(e => e.GetInt32()).ToList();
                    service.DefaultFirstTo = op[4].GetInt32();
                    continue;
                }
                object result = Run(service, created, op);
                AssertResult(step.GetProperty("r"), result, where);
                if (step.TryGetProperty("rooms", out JsonElement rooms)) AssertRooms(service, rooms, where);
            }
        }

        static object Run(RoomService s, List<RoomService.Room> created, JsonElement[] op)
        {
            switch (op[0].GetString())
            {
                case "create":
                {
                    RoomService.Room room = s.CreateRoom(op[1].GetInt32(), op[2].GetString(), op[3].GetInt64());
                    if (room == null) return null;
                    created.Add(room);
                    return room.Code;
                }
                case "join": return (long)s.Join(created[op[1].GetInt32()].Code, op[2].GetInt32(), op[3].GetString(), op.Length > 4 ? op[4].GetInt64() : 0);
                case "join_lower": return (long)s.Join(created[op[1].GetInt32()].Code.ToLowerInvariant(), op[2].GetInt32(), op[3].GetString());
                case "join_code": return (long)s.Join(op[1].GetString(), op[2].GetInt32(), op[3].GetString());
                case "pick_team": return s.PickTeam(op[1].GetInt32(), op[2].GetInt64());
                case "set_ready": return s.SetReady(op[1].GetInt32(), op[2].GetBoolean());
                case "set_first_to": return s.SetFirstTo(op[1].GetInt32(), op[2].GetInt64());
                case "set_map": return s.SetMap(op[1].GetInt32(), op[2].GetString());
                case "set_fill_bots": return s.SetFillBots(op[1].GetInt32(), op[2].GetBoolean());
                case "set_open": return s.SetOpen(op[1].GetInt32(), op[2].GetBoolean());
                case "set_character": return s.SetCharacter(op[1].GetInt32(), op[2].GetString());
                case "set_rank": return s.SetRank(op[1].GetInt32(), op[2].GetInt64());
                case "start": return s.Start(op[1].GetInt32());
                case "playing":
                    created[op[1].GetInt32()].State = RoomService.State.Playing;
                    return null;
                case "leave": return s.Leave(op[1].GetInt32())?.Code;
                case "disconnected": return s.Disconnected(op[1].GetInt32())?.Code;
                case "reconnect":
                {
                    string token = created[op[1].GetInt32()].Members[op[2].GetInt32()].Token;
                    RoomService.Member back = s.Reconnect(token, op[3].GetInt32());
                    return back == null ? null : (object)(long)back.PlayerId;
                }
                case "reconnect_literal":
                {
                    RoomService.Member back = s.Reconnect(op[1].GetString(), op[2].GetInt32());
                    return back == null ? null : (object)(long)back.PlayerId;
                }
                case "step": return s.Step(op[1].GetDouble()).Select(r => (object)r.Code).ToList();
                case "blocker": return s.StartBlocker(created[op[1].GetInt32()], op[2].GetBoolean());
                case "bot_teams": return s.BotTeams(created[op[1].GetInt32()]).Select(t => (object)(long)t).ToList();
                case "open_rooms": return NetKit.Hex(s.OpenRooms(op[1].GetInt32()));
                case "clean_name": return s.CleanName(op[1].GetString());
            }
            Assert.Fail("unknown op " + op[0]);
            return null;
        }

        static void AssertResult(JsonElement expected, object actual, string where)
        {
            switch (expected.ValueKind)
            {
                case JsonValueKind.Null: Assert.IsNull(actual, where); break;
                case JsonValueKind.True:
                case JsonValueKind.False: Assert.AreEqual(expected.GetBoolean(), actual, where); break;
                case JsonValueKind.String: Assert.AreEqual(expected.GetString(), actual, where); break;
                case JsonValueKind.Number: Assert.AreEqual(expected.GetInt64(), actual, where); break;
                case JsonValueKind.Array:
                    var list = (List<object>)actual;
                    JsonElement[] items = expected.EnumerateArray().ToArray();
                    Assert.AreEqual(items.Length, list.Count, where);
                    for (int i = 0; i < items.Length; i++) AssertResult(items[i], list[i], where);
                    break;
            }
        }

        static void AssertRooms(RoomService service, JsonElement rooms, string where)
        {
            var actual = service.Rooms.Values.ToList();
            JsonElement[] expected = rooms.EnumerateArray().ToArray();
            Assert.AreEqual(expected.Length, actual.Count, where + ": room count");
            for (int r = 0; r < expected.Length; r++)
            {
                Assert.AreEqual(expected[r].GetProperty("hex").GetString(), NetKit.Hex(service.Describe(actual[r])), where + ": ROOM bytes");
                JsonElement[] priv = expected[r].GetProperty("priv").EnumerateArray().ToArray();
                Assert.AreEqual(priv.Length, actual[r].Members.Count, where);
                for (int m = 0; m < priv.Length; m++)
                {
                    JsonElement[] p = priv[m].EnumerateArray().ToArray();
                    RoomService.Member member = actual[r].Members[m];
                    Assert.AreEqual(p[0].GetInt32(), member.PlayerId, where);
                    Assert.AreEqual(p[1].GetInt32(), member.PeerId, where);
                    Assert.AreEqual(p[2].GetString(), member.Token, where);
                    Assert.AreEqual(p[3].GetBoolean(), member.Connected, where);
                    Assert.AreEqual(p[4].GetDouble(), member.ReconnectLeft, 1e-9, where);
                    Assert.AreEqual(p[5].GetDouble(), member.GoneFor, 1e-9, where);
                    Assert.AreEqual(p[6].GetBoolean(), member.Afk, where);
                }
            }
        }
    }
}
