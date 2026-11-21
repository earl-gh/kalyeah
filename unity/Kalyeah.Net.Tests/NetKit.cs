using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>Shared test helpers: the shipped data, the Godot recordings (unity/Golden/Net) and small fakes.</summary>
    public static class NetKit
    {
        static string Root => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..");
        static SimData _data;

        public static SimData Data => _data ??= SimData.Load(Path.Combine(Root, "Data"));
        public static NetRules Net => NetRules.FromData(Data);
        public static GameRules Rules => Data.GameRules;

        /// <summary>The 10-kid roster the Godot net goldens were recorded with (the game now plays the 12 kids of docs/SPEC.md §7).</summary>
        public static readonly string[] RecordedRoster =
        {
            "junjun", "ligaya", "migo", "toni", "popoy",
            "inday", "kiko", "nene", "buknoy", "tala",
        };

        /// <summary>`rules` dealing from the recorded roster (so AssignCharacters draws the RNG as the recording did) and playing the recorded rules.</summary>
        public static GameRules WithRecordedRoster(GameRules rules)
        {
            var characters = new List<CharacterDef>();
            foreach (string id in RecordedRoster) characters.Add(new CharacterDef { Id = id, DisplayName = id, Description = id });
            rules.Characters = characters;
            // and the pre-spec Agawang Base rules the recordings played (docs/SPEC.md §5 replaced them)
            rules.SpecRules = false;
            // pre-spec movement (docs/SPEC.md §7 replaced it): one run speed in proportion to the stick, no running, old crouch and slide
            rules.MoveSpeed = 6.4;
            rules.SprintEnabled = false;
            rules.SprintDelay = 1.2;
            rules.SprintMultiplier = 1.32;
            rules.CrouchSpeedShare = 0.55;
            rules.SlideSpeed = 10.2;
            rules.SlideDuration = 0.46;
            rules.SlideCooldown = 3.2;
            rules.RescueHoldTime = 4.0;
            rules.RescueCooldown = 8.0;
            rules.NoCastInRescueArea = true;
            rules.RescueCheckpoint = 0.0;
            rules.RevengeReleaseSeconds = 0.0;
            rules.RevengeShieldSeconds = 0.0;
            rules.CaptureDrainShare = 0.0;
            rules.BarrierSeconds = 0.0;
            rules.CaptureSoloTime = 12.0;
            rules.CaptureAllyBonus = 0.65;
            rules.OvertimeAfter = 240.0;
            return rules;
        }
        public static MapLayout Layout => Data.MapLayout;

        /// <summary>The map the Godot server recordings were made on: today's layout without the low covers added since.</summary>
        public static MapLayout RecordedLayout()
        {
            MapLayout recorded = Layout.Duplicate();
            recorded.LowCovers = new System.Collections.Generic.List<Rect2>();
            recorded.LowCoverHeights = new System.Collections.Generic.List<float>();
            return recorded;
        }
        public static StreetCatalog Games => new StreetCatalog(Data);

        public static RoomService Service(long seed = 7) => new RoomService(Net, Games, seed);

        public static JsonDocument Golden(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "Golden", "Net", name)));

        public static string Hex(object value) => Wire.Hex(VariantCodec.Encode(value));

        public static GDict Decode(string hex)
        {
            Assert.IsTrue(VariantCodec.TryDecode(Wire.FromHex(hex), out object value), "decodes");
            return (GDict)value;
        }
    }

    /// <summary>A server transport that records what is sent and delivers nothing (the golden sessions feed messages directly).</summary>
    public sealed class RecordingTransport : IServerTransport
    {
        public readonly List<(int Peer, byte[] Packet)> Sent = new List<(int, byte[])>();
#pragma warning disable 67
        public event System.Action<int> PeerConnected;
        public event System.Action<int> PeerDisconnected;
#pragma warning restore 67
        public void Poll() { }
        public bool TryGetPacket(out int fromPeer, out byte[] packet)
        {
            fromPeer = 0;
            packet = null;
            return false;
        }
        public void Send(int peerId, byte[] packet) => Sent.Add((peerId, packet));
        public void Close() { }
    }

    /// <summary>A client transport that is always connected and records what is sent.</summary>
    public sealed class RecordingClientTransport : IClientTransport
    {
        public readonly List<byte[]> Sent = new List<byte[]>();
        public readonly List<byte[]> Incoming = new List<byte[]>();
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Connected;
        public void Poll() { }
        public bool TryGetPacket(out byte[] packet)
        {
            packet = null;
            if (Incoming.Count == 0) return false;
            packet = Incoming[0];
            Incoming.RemoveAt(0);
            return true;
        }
        public void Send(byte[] packet) => Sent.Add(packet);
        public void Close() => Status = ConnectionStatus.Disconnected;
    }
}
