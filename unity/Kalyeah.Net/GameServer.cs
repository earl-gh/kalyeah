using System;
using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// The headless authoritative server (port of game_server.gd): a packet pipe (Godot-compatible WebSocket, `Listen`), the room
    /// service and one ServerMatch per started room. Drive it with `Poll(delta)` from one thread (the dedicated server's loop, or a
    /// hotspot host's game loop).
    /// </summary>
    public sealed class GameServer : IMatchOutput
    {
        public event Action<string> LogMessage;

        public readonly RoomService Rooms;
        public readonly OrderedDict<string, ServerMatch> Matches = new OrderedDict<string, ServerMatch>();

        readonly GameRules _rules;
        readonly MapLayout _layout;
        readonly NetRules _net;
        IServerTransport _transport;
        readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        /// <summary>When each peer last got the open room list (server seconds), for `room_list_min_gap`.</summary>
        readonly Dictionary<int, double> _listedAt = new Dictionary<int, double>();
        /// <summary>When each peer last sent a quick chat sticker, for `chat_min_gap`.</summary>
        readonly Dictionary<int, double> _saidAt = new Dictionary<int, double>();
        double _clock;
        readonly GodotRng _rng;
        bool _autoJoin;

        public GameServer(GameRules rules, MapLayout layout, NetRules net, long seed, StreetCatalog games)
        {
            _rules = rules;
            _layout = layout;
            _net = net;
            _rng = new GodotRng(seed);
            Rooms = new RoomService(net, games, seed)
            {
                FirstToChoices = new List<int>(rules.FirstToChoices),
                DefaultFirstTo = rules.FirstTo,
            };
        }

        /// <summary>
        /// A hotspot host's server has one room and nobody types a code: a JOIN with no code joins it, and its rooms may fill with bots.
        /// Only the server's owner sets this, never a client message.
        /// </summary>
        public bool AutoJoin
        {
            get => _autoJoin;
            set
            {
                _autoJoin = value;
                Rooms.BotsAllowed = value; // bots are for a friends' room on a hotspot, not for the online server
            }
        }

        public bool IsListening => _transport != null;

        /// <summary>Starts a Godot-compatible WebSocket server on `port` (every IPv4 address).</summary>
        public void Listen(int port) => Attach(WsServerTransport.Listen(port));

        /// <summary>Serves over `transport` (a WsServerTransport, or a MemoryHub in tests and same-process play).</summary>
        public void Attach(IServerTransport transport)
        {
            _transport = transport;
            transport.PeerConnected += id => LogMessage?.Invoke("peer " + id + " connected");
            transport.PeerDisconnected += OnPeerDisconnected;
            if (transport is WsServerTransport ws) LogMessage?.Invoke("listening on " + ws.Port);
        }

        /// <summary>
        /// Tells every player in a room that the server is going away (a hotspot host left). Keep polling a moment before `Stop`: a
        /// client drops the messages that arrive together with the close.
        /// </summary>
        public void CloseRooms()
        {
            foreach (var pair in Rooms.Rooms)
                foreach (RoomService.Member m in pair.Value.Members)
                    if (m.Connected) Send(m.PeerId, NetProtocol.Message(NetProtocol.Msg.RoomClosed));
        }

        public void Stop()
        {
            _transport?.Close();
            _transport = null;
            foreach (ServerMatch running in Matches.Values) running.Dispose();
            Matches.Clear();
        }

        public void Poll(double delta)
        {
            if (_transport == null) return;
            _clock += delta;
            _transport.Poll();
            while (_transport != null && _transport.TryGetPacket(out int from, out byte[] packet))
            {
                GDict message = NetProtocol.Decode(packet);
                if (!message.IsEmpty) Handle(from, message);
            }
            Step(delta);
        }

        /// <summary>The part of `Poll` after the packets: reconnect windows, then the running matches.</summary>
        void Step(double delta)
        {
            foreach (RoomService.Room room in Rooms.Step(delta)) SendRoom(room);
            foreach (string code in Matches.KeyList())
            {
                if (!Rooms.Rooms.ContainsKey(code))
                {
                    Matches[code].Dispose();
                    Matches.Remove(code);
                    continue;
                }
                Matches[code].Poll(delta);
            }
        }

        public void Send(int peerId, GDict message) => _transport?.Send(peerId, NetProtocol.Encode(message));

        /// <summary>One decoded client message (what `Poll` does for every packet).</summary>
        public void Handle(int from, GDict message)
        {
            switch ((NetProtocol.Msg)NetProtocol.TypeOf(message))
            {
                case NetProtocol.Msg.DiscoverRoom:
                {
                    string status = "incompatible";
                    if (Same(message.Get("game", ""), "kalyeah") && Same(message.Get("v", -1L), (long)NetProtocol.Version)) status = DiscoveryStatus();
                    GDict answer = NetProtocol.Message(NetProtocol.Msg.RoomDiscovered, "game", "kalyeah", "v", (long)NetProtocol.Version, "status", status);
                    if (status == "ready")
                    {
                        // what the guest's list shows, so a player with several hosts nearby picks the right one
                        RoomService.Room room = FirstRoom();
                        RoomService.Member host = room.Member(room.HostPlayerId);
                        answer["host"] = host != null ? host.Name : "";
                        answer["players"] = (long)room.Members.Count;
                        answer["size"] = (long)(RoomService.MaxTeamSize * 2);
                    }
                    Send(from, answer);
                    break;
                }
                case NetProtocol.Msg.Hello:
                {
                    // an older or newer build reads the match differently (an old one has no "v"): refuse it before it joins anything
                    if (!Same(message.Get("v", -1L), (long)NetProtocol.Version))
                    {
                        Error(from, NetProtocol.IncompatibleText);
                        return;
                    }
                    _names[from] = Rooms.CleanName(AsString(message.Get("name", "")));
                    RoomService.Member member = Rooms.Reconnect(AsString(message.Get("token", "")), from);
                    GDict welcome = NetProtocol.Message(NetProtocol.Msg.Welcome, "v", (long)NetProtocol.Version, "reconnected", member != null);
                    if (member != null && Matches.TryGetValue(Rooms.RoomOfPeer(from).Code, out ServerMatch memberMatch))
                        welcome["input_next"] = (long)memberMatch.NextInputSequence(member.PlayerId);
                    Send(from, welcome);
                    if (member != null)
                    {
                        RoomService.Room room = Rooms.RoomOfPeer(from);
                        SendRoom(room);
                        if (Matches.TryGetValue(room.Code, out ServerMatch running))
                        {
                            Send(from, running.Info());
                            Send(from, running.StageMessage());
                        }
                    }
                    break;
                }
                case NetProtocol.Msg.CreateRoom:
                {
                    RoomService.Room room = Rooms.CreateRoom(from, NameOf(from));
                    if (room == null) Error(from, "Oops! Can't create a room right now.");
                    else SendRoom(room);
                    break;
                }
                case NetProtocol.Msg.JoinRoom:
                {
                    string wanted = AsString(message.Get("code", ""));
                    if (_autoJoin && wanted.Length == 0 && Rooms.Rooms.Count == 1) wanted = Rooms.Rooms.Keys[0];
                    switch (Rooms.Join(wanted, from, NameOf(from)))
                    {
                        case RoomService.Result.Ok: SendRoom(Rooms.RoomOfPeer(from)); break;
                        case RoomService.Result.NotFound: Error(from, "No room with that code. Check it and try again!"); break;
                        case RoomService.Result.Full: Error(from, "That room is full!"); break;
                        case RoomService.Result.InProgress: Error(from, "That battle has already started!"); break;
                        default: Error(from, "Leave your room first!"); break;
                    }
                    break;
                }
                case NetProtocol.Msg.ListRooms:
                    if (_clock - (_listedAt.TryGetValue(from, out double listed) ? listed : double.NegativeInfinity) < _net.RoomListMinGap) return;
                    _listedAt[from] = _clock;
                    Send(from, NetProtocol.Message(NetProtocol.Msg.RoomList, "rooms", Rooms.OpenRooms(_net.RoomListMax)));
                    break;
                case NetProtocol.Msg.SetOpen:
                    if (message.Get("open") is bool open && Rooms.SetOpen(from, open)) SendRoom(Rooms.RoomOfPeer(from));
                    break;
                case NetProtocol.Msg.PickTeam:
                    if (Rooms.PickTeam(from, AsInt(message.Get("team", 0L)))) SendRoom(Rooms.RoomOfPeer(from));
                    else Error(from, "That team is full or already has your kid. Choose another kid first.");
                    break;
                case NetProtocol.Msg.SetReady:
                    if (Rooms.SetReady(from, AsBool(message.Get("ready", false)))) SendRoom(Rooms.RoomOfPeer(from));
                    break;
                case NetProtocol.Msg.SetFillBots:
                    if (message.Get("on") is bool fill && Rooms.SetFillBots(from, fill)) SendRoom(Rooms.RoomOfPeer(from));
                    else Error(from, "Only the host of a Local room can fill places with bots.");
                    break;
                case NetProtocol.Msg.SetFirstTo:
                    if (Rooms.SetFirstTo(from, AsInt(message.Get("n", 0L)))) SendRoom(Rooms.RoomOfPeer(from));
                    break;
                case NetProtocol.Msg.SetMap:
                    if (Rooms.SetMap(from, AsString(message.Get("map", "")))) SendRoom(Rooms.RoomOfPeer(from));
                    // never silent: an older server, a guest or a started room refuses, and the host must see why
                    else Error(from, "Can't change the game right now.");
                    break;
                case NetProtocol.Msg.SetCharacter:
                    if (Rooms.SetCharacter(from, AsString(message.Get("c", "")))) SendRoom(Rooms.RoomOfPeer(from));
                    else Error(from, "Choose an available kid. A teammate may already have that one.");
                    break;
                case NetProtocol.Msg.Start:
                    HandleStart(from);
                    break;
                case NetProtocol.Msg.Leave:
                {
                    RoomService.Room left = Rooms.Disconnected(from);
                    if (left != null) SendRoom(left);
                    break;
                }
                case NetProtocol.Msg.Loaded:
                case NetProtocol.Msg.PickWeapons:
                case NetProtocol.Msg.SwapWeapons:
                case NetProtocol.Msg.Input:
                case NetProtocol.Msg.Say:
                    HandleMatch(from, message);
                    break;
            }
        }

        void HandleStart(int from)
        {
            RoomService.Room held = Rooms.RoomOfPeer(from);
            if (Rooms.Start(from))
            {
                RoomService.Room room = Rooms.RoomOfPeer(from);
                var started = new ServerMatch(this, Rooms, room, _rules, _layout, _net, _rng.Randi());
                Matches[room.Code] = started;
                SendRoom(room);
                foreach (RoomService.Member m in room.Members) Send(m.PeerId, started.Info());
                return;
            }
            // never silent: the host reads why the game cannot start yet (each game has its own minimum)
            RoomService.Member member = Rooms.MemberOfPeer(from);
            string why = held != null && member != null && held.HostPlayerId == member.PlayerId ? Rooms.StartBlocker(held, true) : "";
            Error(from, why.Length > 0 ? why : "Only the host can start the game.");
        }

        /// <summary>Discovery never joins or reveals a room code. Only a phone host advertises a room.</summary>
        string DiscoveryStatus()
        {
            if (!_autoJoin) return "not_hotspot";
            if (Rooms.Rooms.Count != 1) return "no_room";
            RoomService.Room room = FirstRoom();
            if (room.State != RoomService.State.Lobby) return "in_progress";
            if (room.Members.Count >= RoomService.MaxTeamSize * 2) return "full";
            return "ready";
        }

        RoomService.Room FirstRoom()
        {
            foreach (RoomService.Room room in Rooms.Rooms.Values) return room;
            return null;
        }

        void HandleMatch(int from, GDict message)
        {
            RoomService.Member member = Rooms.MemberOfPeer(from);
            RoomService.Room room = Rooms.RoomOfPeer(from);
            if (member == null || room == null || !Matches.TryGetValue(room.Code, out ServerMatch running)) return;
            switch ((NetProtocol.Msg)NetProtocol.TypeOf(message))
            {
                case NetProtocol.Msg.Loaded:
                    running.Loaded(member.PlayerId);
                    break;
                case NetProtocol.Msg.PickWeapons:
                    running.Pick(member.PlayerId, Name(message, "a"), Name(message, "b"), Name(message, "c"), Name(message, "d"));
                    break;
                case NetProtocol.Msg.SwapWeapons:
                    running.Swap(member.PlayerId, Name(message, "a"), Name(message, "b"), Name(message, "d"));
                    break;
                case NetProtocol.Msg.Input:
                    running.QueueInput(member.PlayerId, NetProtocol.InputFromDict(message));
                    break;
                case NetProtocol.Msg.Say:
                {
                    // untrusted: a whole number inside the set, only while the match is played, at most one every `chat_min_gap`
                    object sticker = message.Get("s");
                    if (!Wire.IsInt(sticker) || Wire.AsLong(sticker) < 0 || Wire.AsLong(sticker) >= _net.ChatStickers
                        || running.CurrentStage != ServerMatch.Stage.Playing) return;
                    if (_clock - (_saidAt.TryGetValue(from, out double said) ? said : double.NegativeInfinity) < _net.ChatMinGap) return;
                    _saidAt[from] = _clock;
                    running.Say(member.PlayerId, Wire.AsLong(sticker));
                    break;
                }
            }
        }

        /// <summary>ROOM goes to every connected member, each with their own id and reconnect token.</summary>
        void SendRoom(RoomService.Room room)
        {
            if (room == null) return;
            GDict description = Rooms.Describe(room);
            foreach (RoomService.Member m in room.Members)
            {
                if (!m.Connected) continue;
                GDict message = description.Duplicate();
                message["t"] = (long)NetProtocol.Msg.Room;
                message["you"] = (long)m.PlayerId;
                message["token"] = m.Token;
                Send(m.PeerId, message);
            }
        }

        void Error(int peerId, string text) => Send(peerId, NetProtocol.Message(NetProtocol.Msg.Error, "text", text));

        /// <summary>A peer's connection closed (also what tests call to drop a peer).</summary>
        public void OnPeerDisconnected(int id)
        {
            LogMessage?.Invoke("peer " + id + " disconnected");
            _names.Remove(id);
            _listedAt.Remove(id);
            _saidAt.Remove(id);
            RoomService.Room room = Rooms.Disconnected(id);
            if (room != null) SendRoom(room);
        }

        string NameOf(int peer) => _names.TryGetValue(peer, out string name) ? name : "";

        /// <summary>GDScript `a == b` between two Variants: numbers by value, String and StringName by text.</summary>
        static bool Same(object a, object b)
        {
            if (Wire.IsNumber(a) && Wire.IsNumber(b)) return Wire.Number(a) == Wire.Number(b);
            if (Wire.IsStringLike(a) && Wire.IsStringLike(b)) return Wire.Text(a) == Wire.Text(b);
            return Equals(a, b);
        }

        /// <summary>GDScript `v as String` for the values a client may send (anything else reads as "").</summary>
        static string AsString(object v) => Wire.Text(v) ?? "";

        /// <summary>GDScript `v as int`.</summary>
        static long AsInt(object v) => Wire.IsInt(v) ? Wire.AsLong(v) : v is double d ? Wire.TruncateToLong(d) : v is bool b ? (b ? 1 : 0) : 0;

        /// <summary>GDScript `v as bool`.</summary>
        static bool AsBool(object v) => v is bool b ? b : Wire.IsNumber(v) ? Wire.Number(v) != 0.0 : false;

        static string Name(GDict message, string key) => Wire.SName(message.Get(key)).Value;
    }
}
