using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Kalyeah.Net
{
    /// <summary>
    /// The host side of finding a room without an address (port of hotspot_beacon.gd): a UDP socket that answers every Kalyeah probe
    /// with the same fixed text, straight back to the sender. The guest then asks that address for its room over the WebSocket
    /// (HotspotFinder). Nothing about the room is said here, and nothing a stranger sends is decoded: the texts are compared whole.
    /// </summary>
    public sealed class HotspotBeacon
    {
        public const string Probe = "kalyeah?";
        public const string Answer = "kalyeah!";
        public static readonly byte[] ProbeBytes = Encoding.ASCII.GetBytes(Probe);
        public static readonly byte[] AnswerBytes = Encoding.ASCII.GetBytes(Answer);

        UdpClient _socket;
        int _burst;
        /// <summary>Probes answered since `Listen`.</summary>
        public int Answered;

        /// <summary>Starts answering on `port` (`address`: the interface to listen on, every IPv4 one by default). False if it cannot bind.</summary>
        public bool Listen(int port, int burst, string address = "0.0.0.0")
        {
            Stop();
            _burst = burst;
            Answered = 0;
            try
            {
                _socket = new UdpClient(new IPEndPoint(IPAddress.Parse(address), port));
                return true;
            }
            catch (SocketException)
            {
                _socket = null;
                return false;
            }
        }

        public bool IsListening => _socket != null;

        /// <summary>The port it listens on (useful with port 0).</summary>
        public int Port => _socket != null ? ((IPEndPoint)_socket.Client.LocalEndPoint).Port : 0;

        /// <summary>Answers at most `burst` waiting probes (a flood from the network cannot stall the game).</summary>
        public void Poll()
        {
            if (_socket == null) return;
            for (int i = 0; i < _burst; i++)
            {
                if (_socket.Available <= 0) return;
                var from = new IPEndPoint(IPAddress.Any, 0);
                byte[] packet;
                try
                {
                    packet = _socket.Receive(ref from);
                }
                catch (SocketException)
                {
                    continue;
                }
                if (!Same(packet, ProbeBytes)) continue;
                _socket.Send(AnswerBytes, AnswerBytes.Length, from);
                Answered++;
            }
        }

        public void Stop()
        {
            _socket?.Close();
            _socket = null;
        }

        internal static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// Finds every phone that hosts a Kalyeah room on this Wi-Fi and checks each room before listing it (port of hotspot_finder.gd).
    /// For `plan.Seconds` it sends a UDP probe out of each Wi-Fi address to the broadcast addresses and opens a WebSocket to each
    /// fallback address; every phone that answers the probe is asked for its room (DISCOVER_ROOM). Rooms that are `ready` go into
    /// `Rooms` (the player picks one). Poll it every frame; it never blocks and always ends (`Rooms` filled, or `Failed`).
    /// </summary>
    public sealed class HotspotFinder
    {
        public const string NoRoom = "No room found. Ask your friend to tap Create room, then tap Try again.";
        /// <summary>A host's name in the list is at most this long.</summary>
        public const int NameLimit = 16;
        public static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            { "no_room", NoRoom }, { "full", "That room is full!" }, { "in_progress", "That battle has already started!" },
            { "incompatible", NetProtocol.IncompatibleText }, { "not_hotspot", NoRoom },
        };

        public sealed class FoundRoom
        {
            public string Url = "";
            public string Host = "";
            public int Players;
            public int Size;
        }

        /// <summary>The verified rooms, in the order they answered.</summary>
        public readonly List<FoundRoom> Rooms = new List<FoundRoom>();
        /// <summary>The first room found ("" while none).</summary>
        public string Url = "";
        public bool Failed;
        public string FailureMessage = NoRoom;
        /// <summary>Addresses asked over the WebSocket, and phones that answered the probe (tests read these).</summary>
        public int Asked;
        public int Heard;
        /// <summary>Opens the WebSocket probes (a WsClientTransport by default).</summary>
        public Func<string, IClientTransport> ProbeFactory = url => new WsClientTransport(url);

        HotspotNet.Plan _plan;
        bool _searching;
        bool _answered;
        double _left;
        double _sinceProbe;
        readonly List<(UdpClient Peer, List<string> To)> _scouts = new List<(UdpClient, List<string>)>();
        readonly Dictionary<string, IClientTransport> _probes = new Dictionary<string, IClientTransport>();
        readonly HashSet<string> _sent = new HashSet<string>();
        readonly HashSet<string> _tried = new HashSet<string>();
        readonly HashSet<string> _heard = new HashSet<string>();

        public void Start(HotspotNet.Plan plan)
        {
            Stop();
            _plan = plan;
            Url = "";
            Rooms.Clear();
            Failed = false;
            FailureMessage = NoRoom;
            Asked = 0;
            Heard = 0;
            _answered = false;
            _left = plan.Seconds;
            _sinceProbe = 0.0;
            _tried.Clear();
            _heard.Clear();
            OpenScouts();
            foreach (string candidate in plan.Candidates) OpenProbe(candidate);
            _searching = true;
            SendProbes();
            if (_scouts.Count == 0 && _probes.Count == 0) Finish();
        }

        public bool IsSearching => _searching;

        public void Poll(double delta)
        {
            if (!_searching) return;
            _left -= delta;
            _sinceProbe += delta;
            if (_sinceProbe >= _plan.Interval) SendProbes();
            ReadScouts();
            ReadProbes();
            // with a room found, wait out the window: another host may still answer
            if (_left <= 0.0 || (Rooms.Count == 0 && _answered && _probes.Count == 0)) Finish();
        }

        public void Stop()
        {
            _searching = false;
            foreach (IClientTransport probe in _probes.Values) probe.Close();
            _probes.Clear();
            _sent.Clear();
            foreach (var scout in _scouts) scout.Peer.Close();
            _scouts.Clear();
        }

        void Finish()
        {
            Stop();
            Failed = Rooms.Count == 0;
        }

        /// <summary>One socket for each Wi-Fi address, bound to it so the broadcast leaves by that interface.</summary>
        void OpenScouts()
        {
            var routes = new Dictionary<string, List<string>>();
            var order = new List<string>();
            foreach (var route in _plan.Routes)
            {
                if (!routes.ContainsKey(route.From))
                {
                    routes[route.From] = new List<string>();
                    order.Add(route.From);
                }
                routes[route.From].Add(route.To);
            }
            foreach (string from in order)
            {
                try
                {
                    var peer = new UdpClient(new IPEndPoint(IPAddress.Parse(from), 0)) { EnableBroadcast = true };
                    _scouts.Add((peer, routes[from]));
                }
                catch (Exception e) when (e is SocketException || e is FormatException)
                {
                    // that address is gone: skip it
                }
            }
        }

        void SendProbes()
        {
            _sinceProbe = 0.0;
            foreach (var scout in _scouts)
                foreach (string target in scout.To)
                {
                    try
                    {
                        scout.Peer.Send(HotspotBeacon.ProbeBytes, HotspotBeacon.ProbeBytes.Length, new IPEndPoint(IPAddress.Parse(target), _plan.BeaconPort));
                    }
                    catch (Exception e) when (e is SocketException || e is FormatException)
                    {
                        // unreachable right now: the next round tries again
                    }
                }
        }

        void ReadScouts()
        {
            foreach (var scout in _scouts)
            {
                while (scout.Peer.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] answer;
                    try
                    {
                        answer = scout.Peer.Receive(ref from);
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    string host = from.Address.ToString();
                    if (!HotspotBeacon.Same(answer, HotspotBeacon.AnswerBytes) || _heard.Contains(host)) continue;
                    _heard.Add(host);
                    Heard = _heard.Count;
                    OpenProbe("ws://" + host + ":" + _plan.WsPort);
                }
            }
        }

        void OpenProbe(string candidate)
        {
            if (_tried.Contains(candidate)) return;
            _tried.Add(candidate);
            Asked = _tried.Count;
            try
            {
                _probes[candidate] = ProbeFactory(candidate);
            }
            catch (Exception e) when (e is UriFormatException || e is ArgumentException)
            {
                // not an address: nothing to ask
            }
        }

        void ReadProbes()
        {
            foreach (string candidate in new List<string>(_probes.Keys))
            {
                IClientTransport probe = _probes[candidate];
                probe.Poll();
                if (probe.Status == ConnectionStatus.Connected)
                {
                    if (!_sent.Contains(candidate))
                    {
                        probe.Send(NetProtocol.Encode(NetProtocol.Message(NetProtocol.Msg.DiscoverRoom, "game", "kalyeah", "v", (long)NetProtocol.Version)));
                        _sent.Add(candidate);
                    }
                    while (probe.TryGetPacket(out byte[] packet))
                    {
                        GDict answer = NetProtocol.Decode(packet);
                        if (answer.GetLong("t", -1) != (long)NetProtocol.Msg.RoomDiscovered || answer.GetStr("game") != "kalyeah") continue;
                        string status = answer.GetLong("v", -1) != NetProtocol.Version ? "incompatible" : answer.GetStr("status", "incompatible");
                        if (status == "ready")
                        {
                            AddRoom(candidate, answer);
                            probe.Close();
                            break;
                        }
                        _answered = true;
                        FailureMessage = Messages.TryGetValue(status, out string words) ? words : Messages["incompatible"];
                        probe.Close();
                    }
                }
                if (probe.Status == ConnectionStatus.Disconnected) _probes.Remove(candidate);
            }
        }

        void AddRoom(string candidate, GDict answer)
        {
            foreach (FoundRoom room in Rooms)
                if (room.Url == candidate) return;
            // what a phone on the network says is checked for type and cut short: a list row, never a crash
            object host = answer.Get("host", "");
            Rooms.Add(new FoundRoom
            {
                Url = candidate, Host = host is string name ? RoomService.Left(name, NameLimit) : "",
                Players = Count(answer.Get("players", 0L)), Size = Count(answer.Get("size", 0L)),
            });
            if (Url.Length == 0) Url = candidate;
        }

        static int Count(object value) => Wire.IsNumber(value) ? (int)Math.Max(0, Math.Min(99, Wire.TruncateToLong(Wire.Number(value)))) : 0;
    }
}
