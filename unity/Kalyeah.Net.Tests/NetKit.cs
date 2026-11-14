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
        public static MapLayout Layout => Data.MapLayout;
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
