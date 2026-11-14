using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kalyeah.Net
{
    /// <summary>
    /// A client of a Godot `WebSocketMultiplayerPeer` server (the Godot game's server, or `WsServerTransport`): connects to `ws://...`,
    /// counts as connected once the server's first message (this peer's 4-byte id) arrives, then every binary message is one packet.
    /// The network runs on background tasks; `Poll` (one thread) moves what arrived into the packet queue and updates `Status`.
    /// </summary>
    public sealed class WsClientTransport : IClientTransport
    {
        /// <summary>Godot's WebSocketMultiplayerPeer default handshake_timeout.</summary>
        public const double DefaultHandshakeTimeout = 3.0;

        readonly ClientWebSocket _socket = new ClientWebSocket();
        readonly CancellationTokenSource _stop = new CancellationTokenSource();
        readonly ConcurrentQueue<byte[]> _in = new ConcurrentQueue<byte[]>();
        readonly ConcurrentQueue<byte[]> _out = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim _outReady = new SemaphoreSlim(0);
        readonly System.Collections.Generic.List<byte[]> _packets = new System.Collections.Generic.List<byte[]>();
        volatile int _state = (int)ConnectionStatus.Connecting;
        volatile bool _gotId;

        /// <summary>The id the server gave this peer (0 until connected).</summary>
        public int UniqueId { get; private set; }

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Connecting;

        /// <summary>Starts connecting to `url`; it fails (Disconnected) if not open within `handshakeTimeout` seconds.</summary>
        public WsClientTransport(string url, double handshakeTimeout = DefaultHandshakeTimeout)
        {
            _ = Run(new Uri(url), handshakeTimeout);
        }

        async Task Run(Uri url, double handshakeTimeout)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(handshakeTimeout));
                    await _socket.ConnectAsync(url, timeout.Token).ConfigureAwait(false);
                    _ = SendLoop();
                    byte[] first = await Receive(timeout.Token).ConfigureAwait(false);
                    if (first == null || first.Length != 4) throw new IOException("no peer id");
                    UniqueId = BitConverter.ToInt32(first, 0);
                    _gotId = true;
                }
                while (true)
                {
                    byte[] packet = await Receive(_stop.Token).ConfigureAwait(false);
                    if (packet == null) break;
                    _in.Enqueue(packet);
                }
            }
            catch (Exception)
            {
                // refused, timed out, dropped or closed: the connection is over
            }
            _state = (int)ConnectionStatus.Disconnected;
            _outReady.Release();
        }

        async Task<byte[]> Receive(CancellationToken token)
        {
            var buffer = new byte[WsServerTransport.MaxMessage];
            int length = 0;
            WebSocketReceiveResult result;
            do
            {
                if (length >= buffer.Length) throw new IOException("message too big");
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                length += result.Count;
            } while (!result.EndOfMessage);
            var packet = new byte[length];
            Array.Copy(buffer, packet, length);
            return packet;
        }

        async Task SendLoop()
        {
            try
            {
                while (_state != (int)ConnectionStatus.Disconnected)
                {
                    await _outReady.WaitAsync(_stop.Token).ConfigureAwait(false);
                    if (!_out.TryDequeue(out byte[] packet)) continue;
                    if (packet == null) break;
                    await _socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, _stop.Token).ConfigureAwait(false);
                }
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // closing anyway
            }
        }

        public void Poll()
        {
            if (Status == ConnectionStatus.Disconnected) return;
            while (_in.TryDequeue(out byte[] packet)) _packets.Add(packet);
            if (_state == (int)ConnectionStatus.Disconnected) Status = ConnectionStatus.Disconnected;
            else if (_gotId) Status = ConnectionStatus.Connected;
        }

        public bool TryGetPacket(out byte[] packet)
        {
            packet = null;
            if (_packets.Count == 0) return false;
            packet = _packets[0];
            _packets.RemoveAt(0);
            return true;
        }

        public void Send(byte[] packet)
        {
            if (Status != ConnectionStatus.Connected) return;
            _out.Enqueue(packet);
            _outReady.Release();
        }

        /// <summary>Sends what is queued, then closes.</summary>
        public void Close()
        {
            if (Status == ConnectionStatus.Disconnected) return;
            Status = ConnectionStatus.Disconnected;
            _out.Enqueue(null);
            _outReady.Release();
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                _stop.Cancel();
                _socket.Dispose();
            }, TaskScheduler.Default);
        }
    }
}
