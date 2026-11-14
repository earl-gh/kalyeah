using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kalyeah.Net
{
    /// <summary>
    /// A WebSocket server that Godot's `WebSocketMultiplayerPeer` clients talk to (and that `WsClientTransport` talks to): plain
    /// sockets and the framework's WebSocket over the stream, no HTTP server, so it also runs inside the Unity game on a hotspot host.
    /// Godot's multiplayer layer on top of WebSocket is thin: once the handshake is done, the server sends the new peer its id as one
    /// 4-byte little-endian binary message; after that every binary message is one packet. A plain HTTP request (no Upgrade) gets a
    /// short 200 answer, so a hosting platform's health check sees the server up.
    /// The network runs on background tasks; everything reaches the game only through `Poll` (one thread).
    /// </summary>
    public sealed class WsServerTransport : IServerTransport
    {
        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;

        /// <summary>Seconds a new connection gets to finish its handshake (Godot's default handshake_timeout is 3 s).</summary>
        public static double HandshakeTimeout = 3.0;
        /// <summary>Godot's WebSocketPeer refuses messages over its inbound buffer (64 KiB by default); so does this server.</summary>
        public const int MaxMessage = 1 << 16;

        readonly TcpListener _listener;
        readonly CancellationTokenSource _stop = new CancellationTokenSource();
        readonly ConcurrentQueue<(int Peer, int Kind, byte[] Packet)> _events = new ConcurrentQueue<(int, int, byte[])>();
        readonly ConcurrentDictionary<int, Connection> _peers = new ConcurrentDictionary<int, Connection>();
        readonly System.Collections.Generic.List<(int, byte[])> _packets = new System.Collections.Generic.List<(int, byte[])>();
        readonly Random _ids = new Random();

        const int KindUp = 0, KindDown = 1, KindPacket = 2;

        public int Port { get; }

        WsServerTransport(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>Listens on every IPv4 address (`port` 0 = any free port, read `Port`).</summary>
        public static WsServerTransport Listen(int port, IPAddress address = null)
        {
            var listener = new TcpListener(address ?? IPAddress.Any, port);
            listener.Start();
            var transport = new WsServerTransport(listener);
            _ = transport.AcceptLoop();
            return transport;
        }

        public void Poll()
        {
            while (_events.TryDequeue(out var e))
            {
                switch (e.Kind)
                {
                    case KindUp: PeerConnected?.Invoke(e.Peer); break;
                    case KindDown: PeerDisconnected?.Invoke(e.Peer); break;
                    default: _packets.Add((e.Peer, e.Packet)); break;
                }
            }
        }

        public bool TryGetPacket(out int fromPeer, out byte[] packet)
        {
            fromPeer = 0;
            packet = null;
            if (_packets.Count == 0) return false;
            (fromPeer, packet) = _packets[0];
            _packets.RemoveAt(0);
            return true;
        }

        public void Send(int peerId, byte[] packet)
        {
            if (_peers.TryGetValue(peerId, out Connection c)) c.Enqueue(packet);
        }

        /// <summary>Drops one peer (it gets a close frame).</summary>
        public void Disconnect(int peerId)
        {
            if (_peers.TryGetValue(peerId, out Connection c)) c.Close();
        }

        public void Close()
        {
            _stop.Cancel();
            try { _listener.Stop(); } catch (SocketException) { }
            foreach (Connection c in _peers.Values) c.Close();
        }

        async Task AcceptLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                _ = Serve(tcp);
            }
        }

        int NewPeerId()
        {
            lock (_ids)
            {
                while (true)
                {
                    int id = _ids.Next(2, int.MaxValue); // 1 is the server
                    if (!_peers.ContainsKey(id)) return id;
                }
            }
        }

        async Task Serve(TcpClient tcp)
        {
            tcp.NoDelay = true;
            NetworkStream stream = tcp.GetStream();
            WebSocket socket;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(HandshakeTimeout));
                try
                {
                    socket = await Handshake(stream, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    socket = null;
                }
            }
            if (socket == null)
            {
                tcp.Dispose();
                return;
            }
            int id = NewPeerId();
            var connection = new Connection(socket, tcp);
            _peers[id] = connection;
            connection.Enqueue(BitConverter.GetBytes(id)); // Godot's WebSocketMultiplayerPeer: the peer's id first (little-endian)
            _events.Enqueue((id, KindUp, null));
            _ = connection.SendLoop(_stop.Token);
            try
            {
                var buffer = new byte[MaxMessage];
                while (!_stop.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    int length = 0;
                    WebSocketReceiveResult result;
                    do
                    {
                        if (length >= buffer.Length) throw new IOException("message too big");
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), _stop.Token).ConfigureAwait(false);
                        length += result.Count;
                    } while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var packet = new byte[length];
                    Array.Copy(buffer, packet, length);
                    _events.Enqueue((id, KindPacket, packet));
                }
            }
            catch (Exception)
            {
                // a dropped connection, a bad frame or the server stopping: the peer is gone
            }
            _peers.TryRemove(id, out _);
            connection.Close();
            _events.Enqueue((id, KindDown, null));
        }

        /// <summary>Reads the HTTP upgrade request and answers it (RFC 6455); null after answering a plain HTTP request.</summary>
        static async Task<WebSocket> Handshake(NetworkStream stream, CancellationToken token)
        {
            var request = new StringBuilder();
            var one = new byte[1];
            while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (request.Length > 8192) return null;
                int read = await stream.ReadAsync(one, 0, 1, token).ConfigureAwait(false);
                if (read == 0) return null;
                request.Append((char)one[0]);
            }
            string key = null;
            bool upgrade = false;
            foreach (string line in request.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase)) key = value;
                if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) && value.Equals("websocket", StringComparison.OrdinalIgnoreCase)) upgrade = true;
            }
            if (!upgrade || key == null)
            {
                byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 8\r\nConnection: close\r\n\r\nkalyeah\n");
                await stream.WriteAsync(ok, 0, ok.Length, token).ConfigureAwait(false);
                return null;
            }
            string accept;
            using (SHA1 sha = SHA1.Create())
                accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
                + accept + "\r\n\r\n");
            await stream.WriteAsync(response, 0, response.Length, token).ConfigureAwait(false);
            return WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(20));
        }

        /// <summary>One peer: its socket and an ordered outgoing queue (WebSocket allows one send at a time).</summary>
        internal sealed class Connection
        {
            readonly WebSocket _socket;
            readonly TcpClient _tcp;
            readonly ConcurrentQueue<byte[]> _out = new ConcurrentQueue<byte[]>();
            readonly SemaphoreSlim _ready = new SemaphoreSlim(0);
            int _closed;

            public Connection(WebSocket socket, TcpClient tcp)
            {
                _socket = socket;
                _tcp = tcp;
            }

            public void Enqueue(byte[] packet)
            {
                if (_closed != 0) return;
                _out.Enqueue(packet);
                _ready.Release();
            }

            public async Task SendLoop(CancellationToken token)
            {
                try
                {
                    while (true)
                    {
                        await _ready.WaitAsync(token).ConfigureAwait(false);
                        if (!_out.TryDequeue(out byte[] packet)) continue;
                        if (packet == null) break; // the close marker
                        await _socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
                    }
                    if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // the connection is going away anyway
                }
                finally
                {
                    _tcp.Dispose();
                }
            }

            /// <summary>Sends what is queued, then the close frame.</summary>
            public void Close()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                _out.Enqueue(null);
                _ready.Release();
            }
        }
    }
}
