using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// Whole GameServer sessions recorded from the Godot server (net_server.json): the same packets in (byte for byte), the same polls
    /// and drops, and every packet out must be Godot's: lobby, errors, discovery, the match loading, the pick, snapshots, events, chat,
    /// a reconnect; a hotspot room filled with bots (TeamCoach); Patintero, Tumbang Preso and Langit Lupa (bots too). The long sessions
    /// keep the md5 of each packet's hex instead of the hex.
    /// </summary>
    public class ServerGoldenTests
    {
        public static IEnumerable<string> Sessions()
        {
            using JsonDocument doc = NetKit.Golden("net_server.json");
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        }

        [Test]
        public void EverySessionIsRecorded() =>
            CollectionAssert.IsSupersetOf(Sessions(), new[] { "online", "hotspot_bots", "patintero", "tumbang_preso", "langit_lupa" });

        static string Head(string hex) => hex.Substring(0, System.Math.Min(48, hex.Length));

        static string Md5(string text)
        {
            using MD5 md5 = MD5.Create();
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }

        [TestCaseSource(nameof(Sessions))]
        public void ServesLikeGodot(string name)
        {
            using JsonDocument doc = NetKit.Golden("net_server.json");
            JsonElement session = doc.RootElement.GetProperty(name);
            bool hashed = session.TryGetProperty("hashed", out JsonElement h) && h.GetBoolean();
            var server = new GameServer(NetKit.Rules, NetKit.Layout, NetKit.Net, session.GetProperty("seed").GetInt64(), NetKit.Games)
            {
                AutoJoin = session.GetProperty("auto_join").GetBoolean(),
            };
            var transport = new RecordingTransport();
            server.Attach(transport);
            int index = 0;
            foreach (JsonElement step in session.GetProperty("steps").EnumerateArray())
            {
                transport.Sent.Clear();
                string where;
                if (step.TryGetProperty("from", out JsonElement from))
                {
                    where = name + " #" + index + " from " + from.GetInt32() + " " + Head(step.GetProperty("hex").GetString());
                    GDict message = NetProtocol.Decode(Wire.FromHex(step.GetProperty("hex").GetString()));
                    if (!message.IsEmpty) server.Handle(from.GetInt32(), message);
                }
                else if (step.TryGetProperty("poll", out JsonElement delta))
                {
                    where = name + " #" + index + " poll";
                    server.Poll(delta.GetDouble());
                }
                else
                {
                    where = name + " #" + index + " drop";
                    server.OnPeerDisconnected(step.GetProperty("drop").GetInt32());
                }
                var expected = step.TryGetProperty("sent", out JsonElement sent) ? sent.EnumerateArray().ToList() : new List<JsonElement>();
                Assert.AreEqual(expected.Count, transport.Sent.Count, where + ": packets out");
                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.AreEqual(expected[i][0].GetInt32(), transport.Sent[i].Peer, where + ": to");
                    string hex = Wire.Hex(transport.Sent[i].Packet);
                    string expect = expected[i][1].GetString();
                    if (hashed) Assert.AreEqual(expect, Md5(hex), where + ": packet " + i + " " + Head(hex));
                    else Assert.AreEqual(expect, hex, where + ": packet " + i);
                }
                index++;
            }
        }
    }
}
