using System;
using System.Collections.Generic;

namespace Kalyeah.Net
{
    /// <summary>A connection's state (Godot's MultiplayerPeer.ConnectionStatus).</summary>
    public enum ConnectionStatus { Disconnected, Connecting, Connected }

    /// <summary>
    /// The server side of a packet pipe (what game_server.gd uses WebSocketMultiplayerPeer for, with no RPC). Single-threaded use:
    /// `Poll` raises the connection events and queues what arrived; the server then reads the packets.
    /// </summary>
    public interface IServerTransport
    {
        event Action<int> PeerConnected;
        event Action<int> PeerDisconnected;
        void Poll();
        bool TryGetPacket(out int fromPeer, out byte[] packet);
        void Send(int peerId, byte[] packet);
        void Close();
    }

    /// <summary>The client side of the pipe (game_client.gd's WebSocketMultiplayerPeer, always talking to the server).</summary>
    public interface IClientTransport
    {
        ConnectionStatus Status { get; }
        void Poll();
        bool TryGetPacket(out byte[] packet);
        void Send(byte[] packet);
        void Close();
    }

    /// <summary>
    /// An in-memory server and its clients (no sockets): packets move on `Poll`, like the real transports. For tests and for running
    /// a server inside the same process as its client.
    /// </summary>
    public sealed class MemoryHub : IServerTransport
    {
        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;

        readonly List<(int Peer, byte[] Packet)> _inbox = new List<(int, byte[])>();
        readonly List<(int Peer, bool Up)> _changes = new List<(int, bool)>();
        readonly Dictionary<int, Client> _clients = new Dictionary<int, Client>();
        bool _closed;

        /// <summary>Opens a client connection with peer id `peerId` (it is connected from the next Poll of both ends).</summary>
        public Client Connect(int peerId)
        {
            var client = new Client(this, peerId);
            _clients[peerId] = client;
            _changes.Add((peerId, true));
            return client;
        }

        public void Poll()
        {
            var changes = _changes.ToArray();
            _changes.Clear();
            foreach (var change in changes)
            {
                if (change.Up) PeerConnected?.Invoke(change.Peer);
                else PeerDisconnected?.Invoke(change.Peer);
            }
        }

        public bool TryGetPacket(out int fromPeer, out byte[] packet)
        {
            fromPeer = 0;
            packet = null;
            if (_inbox.Count == 0) return false;
            (fromPeer, packet) = _inbox[0];
            _inbox.RemoveAt(0);
            return true;
        }

        public void Send(int peerId, byte[] packet)
        {
            if (_clients.TryGetValue(peerId, out Client client) && client.Status != ConnectionStatus.Disconnected) client.Inbox.Add(packet);
        }

        public void Close()
        {
            _closed = true;
            foreach (Client client in _clients.Values) client.Status = ConnectionStatus.Disconnected;
            _clients.Clear();
        }

        public sealed class Client : IClientTransport
        {
            readonly MemoryHub _hub;
            public readonly int PeerId;
            internal readonly List<byte[]> Inbox = new List<byte[]>();

            internal Client(MemoryHub hub, int peerId)
            {
                _hub = hub;
                PeerId = peerId;
                Status = ConnectionStatus.Connecting;
            }

            public ConnectionStatus Status { get; internal set; }

            public void Poll()
            {
                if (Status == ConnectionStatus.Connecting) Status = _hub._closed ? ConnectionStatus.Disconnected : ConnectionStatus.Connected;
            }

            public bool TryGetPacket(out byte[] packet)
            {
                packet = null;
                if (Inbox.Count == 0) return false;
                packet = Inbox[0];
                Inbox.RemoveAt(0);
                return true;
            }

            public void Send(byte[] packet)
            {
                if (Status == ConnectionStatus.Connected) _hub._inbox.Add((PeerId, packet));
            }

            public void Close()
            {
                if (Status == ConnectionStatus.Disconnected) return;
                Status = ConnectionStatus.Disconnected;
                _hub._clients.Remove(PeerId);
                _hub._changes.Add((PeerId, false));
            }
        }
    }
}
