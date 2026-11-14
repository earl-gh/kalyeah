using System;
using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// The client side of the network game (port of game_client.gd). Talks to the server, keeps the room state, and during a match keeps
    /// a mirror MatchSim for the views: the local player's movement is predicted and reconciled with the server, the other players are
    /// drawn `interpolation_delay` in the past between two snapshots. Drive it with `Poll(delta)` every frame (one thread), call
    /// `SendInput` every sim tick and `Interpolate` before drawing.
    /// </summary>
    public sealed class GameClient
    {
        public event Action<bool> ConnectionChanged;
        /// <summary>A connection that never opened (no server there, or it did not wake within `connect_timeout`).</summary>
        public event Action ConnectFailed;
        /// <summary>The server answered HELLO; true = we got our match slot back.</summary>
        public event Action<bool> Welcomed;
        public event Action<string> ErrorReceived;
        public event Action<GDict> RoomChanged;
        /// <summary>The server closed the room for good (a hotspot host left); a reconnect would find nothing.</summary>
        public event Action RoomClosed;
        /// <summary>The host started: build the match scene from the info (Mirror is ready), then call `Loaded()`.</summary>
        public event Action<GDict> MatchLoading;
        /// <summary>"pick" (the weapon pick) or "play" (loadouts are final).</summary>
        public event Action<string> StageChanged;
        /// <summary>Someone confirmed their pick: the kids a team has locked in changed.</summary>
        public event Action PicksChanged;
        public event Action SnapshotApplied;
        /// <summary>Someone in the match (you too) sent a quick chat sticker: their player id and the sticker's number.</summary>
        public event Action<int, int> Said;
        /// <summary>The open Online rooms (`ListRooms`): each {code, host_name, team_size, players, first_to, map}.</summary>
        public event Action<List<GDict>> RoomsListed;
        /// <summary>Every relayed match event ("sim", "weapons" or "mode", the snake_case signal, its wire args), before it is played on the mirror.</summary>
        public event Action<string, string, GArray> EventReceived;

        public string PlayerName = "";
        /// <summary>Stable id in the room and the match (-1 before joining a room).</summary>
        public int PlayerId = -1;
        /// <summary>Secret to take the slot back after a dropped connection (keep it with the session).</summary>
        public string Token = "";
        public GDict Room = new GDict();
        public GDict MatchInfo = new GDict();
        public string Stage = "";
        public double StageTime;
        /// <summary>The client's copy of the match, read by the views.</summary>
        public MatchSim Mirror;
        /// <summary>The next input number the authoritative match accepts; `SendInput` maps the game's own counter forward.</summary>
        public int NextInputSequence = 1;
        /// <summary>The players who confirmed their pick (from the server).</summary>
        public readonly List<int> LockedPlayers = new List<int>();
        /// <summary>Makes the connection (a WsClientTransport by default): url, handshake timeout in seconds.</summary>
        public Func<string, double, IClientTransport> TransportFactory = (url, timeout) => new WsClientTransport(url, timeout);

        readonly GameRules _rules;
        readonly MapLayout _layout;
        readonly NetRules _net;
        readonly StreetCatalog _games;
        IClientTransport _peer;
        bool _wasOnline;
        readonly List<PlayerInput> _pending = new List<PlayerInput>();
        double _clock;
        double _connectStarted;
        /// <summary>Other players' positions and jump heights at each snapshot.</summary>
        readonly List<Sample> _buffer = new List<Sample>();

        sealed class Sample
        {
            public double Time;
            public readonly Dictionary<int, Vec2> Pos = new Dictionary<int, Vec2>();
            public readonly Dictionary<int, double> Air = new Dictionary<int, double>();
        }

        public GameClient(GameRules rules, MapLayout layout, NetRules net, StreetCatalog games)
        {
            _rules = rules;
            _layout = layout;
            _net = net;
            _games = games;
        }

        /// <summary>`patient`: an Online server, which may first have to wake up, gets `connect_timeout` to answer (Hotspot: 3 s).</summary>
        public bool ConnectTo(string url, bool patient = false)
        {
            Close();
            try
            {
                _peer = TransportFactory(url, patient ? _net.ConnectTimeout : WsClientTransport.DefaultHandshakeTimeout);
            }
            catch (Exception e) when (e is UriFormatException || e is ArgumentException || e is NotSupportedException)
            {
                _peer = null;
                return false;
            }
            _connectStarted = _clock;
            return true;
        }

        /// <summary>Uses an already open connection (a MemoryHub client, for instance).</summary>
        public void Attach(IClientTransport transport)
        {
            Close();
            _peer = transport;
            _connectStarted = _clock;
        }

        /// <summary>Seconds this connection has been trying to open (0 once open, or with none).</summary>
        public double ConnectingFor() => IsConnecting() ? _clock - _connectStarted : 0.0;

        /// <summary>Still opening after `wake_hint_after`: an Online server that was asleep is waking up.</summary>
        public bool Waking() => ConnectingFor() >= _net.WakeHintAfter;

        public void Close()
        {
            if (_peer != null)
            {
                _peer.Close();
                _peer = null;
            }
            if (_wasOnline)
            {
                _wasOnline = false;
                ConnectionChanged?.Invoke(false);
            }
        }

        public bool IsOnline() => _peer != null && _peer.Status == ConnectionStatus.Connected;
        public bool IsConnecting() => _peer != null && _peer.Status == ConnectionStatus.Connecting;
        public bool InMatch() => Mirror != null;

        public void Poll(double delta)
        {
            _clock += delta;
            if (StageTime > 0.0) StageTime = Math.Max(0.0, StageTime - delta);
            if (_peer == null) return;
            _peer.Poll();
            bool online = IsOnline();
            if (online != _wasOnline)
            {
                _wasOnline = online;
                ConnectionChanged?.Invoke(online);
                if (online) Send(NetProtocol.Message(NetProtocol.Msg.Hello, "name", PlayerName, "token", Token, "v", (long)NetProtocol.Version));
            }
            if (_peer == null) return;
            if (_peer.Status == ConnectionStatus.Disconnected)
            {
                bool neverOpened = !_wasOnline;
                Close();
                if (neverOpened) ConnectFailed?.Invoke();
                return;
            }
            while (_peer != null && _peer.TryGetPacket(out byte[] packet))
            {
                GDict message = NetProtocol.Decode(packet);
                if (!message.IsEmpty) Handle(message);
            }
        }

        public void Send(GDict message)
        {
            if (!IsOnline()) return;
            _peer.Send(NetProtocol.Encode(message));
        }

        // ---- lobby ----------------------------------------------------------------------------------------------------------

        public void CreateRoom() => Send(NetProtocol.Message(NetProtocol.Msg.CreateRoom));
        public void JoinRoom(string code) => Send(NetProtocol.Message(NetProtocol.Msg.JoinRoom, "code", code));
        /// <summary>Asks for the open rooms; the answer comes as `RoomsListed`.</summary>
        public void ListRooms() => Send(NetProtocol.Message(NetProtocol.Msg.ListRooms));
        /// <summary>Host only: open (listed for everyone) or private (code only).</summary>
        public void SetOpen(bool open) => Send(NetProtocol.Message(NetProtocol.Msg.SetOpen, "open", open));
        /// <summary>Host only, Local rooms: the places nobody took are played by bots.</summary>
        public void SetFillBots(bool on) => Send(NetProtocol.Message(NetProtocol.Msg.SetFillBots, "on", on));
        public void PickTeam(int team) => Send(NetProtocol.Message(NetProtocol.Msg.PickTeam, "team", (long)team));
        /// <summary>Host only: the match is won by the first team to `count` points.</summary>
        public void SetFirstTo(int count) => Send(NetProtocol.Message(NetProtocol.Msg.SetFirstTo, "n", (long)count));
        /// <summary>Host only: the map (game) to play.</summary>
        public void SetMap(string mapId) => Send(NetProtocol.Message(NetProtocol.Msg.SetMap, "map", mapId));
        /// <summary>The kid you will play, shown to the others in the room.</summary>
        public void SetCharacter(string character) => Send(NetProtocol.Message(NetProtocol.Msg.SetCharacter, "c", character));
        public void SetReady(bool ready) => Send(NetProtocol.Message(NetProtocol.Msg.SetReady, "ready", ready));
        public void StartMatch() => Send(NetProtocol.Message(NetProtocol.Msg.Start));

        public void LeaveRoom()
        {
            Send(NetProtocol.Message(NetProtocol.Msg.Leave));
            Room = new GDict();
            PlayerId = -1;
            Token = "";
            Mirror = null;
            Stage = "";
        }

        public bool IsHost() => !Room.IsEmpty && Room.GetInt("host", -1) == PlayerId;

        public int MyTeam()
        {
            foreach (object raw in Room.GetArr("members"))
                if (raw is GDict member && member.GetInt("player_id", -2) == PlayerId) return member.GetInt("team", -1);
            return -1;
        }

        // ---- match -------------------------------------------------------------------------------------------------------------

        public void Loaded() => Send(NetProtocol.Message(NetProtocol.Msg.Loaded));

        /// <summary>The kids a confirmed teammate plays: this player cannot take them.</summary>
        public List<string> TakenKids() => Mirror != null ? Mirror.TeammatesKids(PlayerId, LockedPlayers) : new List<string>();

        public void PickWeapons(string first, string second, string character = "", string third = "")
        {
            GDict message = NetProtocol.Message(NetProtocol.Msg.PickWeapons, "a", new StringName(first), "b", new StringName(second), "c", new StringName(character));
            if (third != "") message["d"] = new StringName(third);
            Send(message);
        }

        public void SwapWeapons(string first, string second, string third = "")
        {
            GDict message = NetProtocol.Message(NetProtocol.Msg.SwapWeapons, "a", new StringName(first), "b", new StringName(second));
            if (third != "") message["d"] = new StringName(third);
            Send(message);
        }

        /// <summary>Sends a quick chat sticker to everyone in the match; it comes back as `Said`, for you too.</summary>
        public void Say(int sticker)
        {
            if (Stage == "play") Send(NetProtocol.Message(NetProtocol.Msg.Say, "s", (long)sticker));
        }

        /// <summary>Sends this tick's input and moves the local player right away (prediction).</summary>
        public void SendInput(PlayerInput input, double dt)
        {
            if (Mirror == null || !IsOnline()) return;
            int wireTick = Math.Max(input.Tick, NextInputSequence);
            PlayerInput wire = PlayerInput.Create(input.Move, input.Aim, input.Buttons, wireTick);
            // the tapped street point and the point under a dragged card ride along (the buttons already carry their bits)
            wire.MoveTarget = input.MoveTarget;
            wire.AimPoint = input.AimPoint;
            Send(NetProtocol.InputToDict(wire));
            NextInputSequence = wireTick + 1;
            if (Stage != "play") return;
            _pending.Add(wire);
            Mirror.PredictMove(PlayerId, wire, dt);
        }

        /// <summary>Places the other players between the two snapshots around (now - delay): XZ position and jump height.</summary>
        public void Interpolate()
        {
            if (Mirror == null || _buffer.Count == 0) return;
            double renderTime = _clock - _net.InterpolationDelay;
            Sample older = _buffer[0];
            Sample newer = _buffer[0];
            foreach (Sample entry in _buffer)
            {
                if (entry.Time <= renderTime)
                {
                    older = entry;
                    newer = entry;
                }
                else
                {
                    newer = entry;
                    break;
                }
            }
            double span = newer.Time - older.Time;
            double weight = span <= 0.0 ? 0.0 : GMath.Clampf((renderTime - older.Time) / span, 0.0, 1.0);
            foreach (var pair in Mirror.Players)
            {
                int id = pair.Key;
                if (id == PlayerId || !older.Pos.ContainsKey(id) || !newer.Pos.ContainsKey(id)) continue;
                pair.Value.Position = older.Pos[id].Lerp(newer.Pos[id], weight);
                if (older.Air.ContainsKey(id) && newer.Air.ContainsKey(id)) pair.Value.AirY = GMath.Lerpf(older.Air[id], newer.Air[id], weight);
            }
        }

        /// <summary>One decoded server message (what `Poll` does for every packet).</summary>
        public void Handle(GDict message)
        {
            switch ((NetProtocol.Msg)message.GetInt("t", -1))
            {
                case NetProtocol.Msg.Welcome:
                {
                    if (message.GetInt("v", -1) != NetProtocol.Version)
                    {
                        ErrorReceived?.Invoke(NetProtocol.IncompatibleText);
                        return;
                    }
                    bool reconnected = message.GetBool("reconnected");
                    if (message.Has("input_next")) NextInputSequence = Math.Max(NextInputSequence, message.GetInt("input_next", NextInputSequence));
                    else if (!reconnected) NextInputSequence = 1;
                    Welcomed?.Invoke(reconnected);
                    break;
                }
                case NetProtocol.Msg.Error:
                    ErrorReceived?.Invoke(message.GetStr("text"));
                    break;
                case NetProtocol.Msg.Room:
                    Room = message;
                    PlayerId = message.GetInt("you", -1);
                    Token = message.GetStr("token");
                    RoomChanged?.Invoke(Room);
                    break;
                case NetProtocol.Msg.RoomClosed:
                    RoomClosed?.Invoke();
                    break;
                case NetProtocol.Msg.RoomList:
                {
                    var listed = new List<GDict>();
                    foreach (object entry in message.GetArr("rooms"))
                        if (entry is GDict room && room.Get("code") is string) listed.Add(room);
                    RoomsListed?.Invoke(listed);
                    break;
                }
                case NetProtocol.Msg.MatchLoading:
                    BuildMirror(message);
                    MatchLoading?.Invoke(message);
                    break;
                case NetProtocol.Msg.MatchPlay:
                    OnStage(message);
                    break;
                case NetProtocol.Msg.Snapshot:
                    OnSnapshot(message);
                    break;
                case NetProtocol.Msg.Events:
                    OnEvents(message.GetArr("list"));
                    break;
                case NetProtocol.Msg.Said:
                {
                    object sender = message.Get("p");
                    object sticker = message.Get("s");
                    if (Wire.IsInt(sender) && Wire.IsInt(sticker) && Wire.AsLong(sticker) >= 0 && Wire.AsLong(sticker) < _net.ChatStickers)
                        Said?.Invoke((int)Wire.AsLong(sender), (int)Wire.AsLong(sticker));
                    break;
                }
            }
        }

        void BuildMirror(GDict info)
        {
            if (Mirror != null && MatchInfo.GetLong("seed", -1) == info.GetLong("seed", -2))
            {
                // reconnected into the same match: keep the mirror the views already use
                _pending.Clear();
                _buffer.Clear();
                return;
            }
            MatchInfo = info;
            Mirror = new MatchSim(_rules, _layout, info.GetLong("seed"));
            foreach (object raw in info.GetArr("players"))
            {
                if (!(raw is GDict entry)) continue;
                PlayerState state = Mirror.AddPlayer(entry.GetInt("id"), entry.GetInt("team"));
                state.CharacterId = Wire.SName(entry.Get("char")).Value;
            }
            // a street game: the same game on the copy, so the views show it and the local kid's walk is predicted by its rules
            StreetModeRules game = _games.RulesFor(info.GetStr("game"));
            if (game != null)
            {
                StreetMode mode = _games.Create(game);
                object runs = info.Get("runs", 0L);
                mode.AttackTeam = GMath.Clampi(Wire.IsInt(runs) ? Wire.AsLong(runs) : 0, 0, 1);
                Mirror.StartMode(mode);
            }
            _pending.Clear();
            _buffer.Clear();
            Stage = "loading";
        }

        void OnStage(GDict message)
        {
            Stage = message.GetStr("stage");
            StageTime = message.GetFloat("time");
            if (Mirror != null)
            {
                foreach (var pair in message.GetDict("loadouts"))
                {
                    if (!Wire.IsInt(pair.Key)) continue;
                    int pid = (int)Wire.AsLong(pair.Key);
                    if (!Mirror.Players.ContainsKey(pid) || !(pair.Value is GArray hand)) continue;
                    if (hand.Count == 0 || hand.Count > PlayerInput.CardBits.Length) continue;
                    var names = new List<string>();
                    bool ok = true;
                    foreach (object item in hand)
                    {
                        if (Wire.IsStringLike(item)) names.Add(Wire.Text(item));
                        else
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (!ok) continue;
                    if (Mirror.Rules.CardCycle)
                    {
                        // a hand dealt by the server (an empty slot while the old shuffler works): take it as it is
                        PlayerState player = Mirror.Players[pid];
                        player.Weapons.Clear();
                        player.Weapons.AddRange(names);
                        player.Cards = true;
                        player.ResizeSlots();
                    }
                    else if (names.Count >= 2)
                    {
                        Mirror.SetLoadoutList(pid, names);
                    }
                }
                // the server settled who plays which kid (a pick may have swapped two players' kids)
                foreach (var pair in message.GetDict("chars"))
                    if (Wire.IsInt(pair.Key) && Mirror.Players.TryGetValue((int)Wire.AsLong(pair.Key), out PlayerState kid))
                        kid.CharacterId = Wire.SName(pair.Value).Value;
                LockedPlayers.Clear();
                foreach (object raw in message.GetArr("locked"))
                    if (Wire.IsInt(raw)) LockedPlayers.Add((int)Wire.AsLong(raw));
            }
            StageChanged?.Invoke(Stage);
            if (Stage == "pick") PicksChanged?.Invoke();
        }

        void OnSnapshot(GDict data)
        {
            if (Mirror == null) return;
            Snapshot.Apply(Mirror, data, PlayerId);
            var sample = new Sample { Time = _clock };
            Vec2 serverPosition = Vec2.Zero;
            bool haveLocal = false;
            foreach (object raw in data.GetArr("players"))
            {
                if (!(raw is GDict entry)) continue;
                int id = entry.GetInt("id", -1);
                if (id < 0 || entry.GetBool("hid")) continue; // out of this phone's sight (a brownout)
                Vec2 pos = entry.GetVec2("pos", Vec2.Zero);
                sample.Pos[id] = pos;
                sample.Air[id] = entry.GetFloat("ay");
                if (id == PlayerId)
                {
                    serverPosition = pos;
                    haveLocal = true;
                }
            }
            _buffer.Add(sample);
            while (_buffer.Count > 4) _buffer.RemoveAt(0);
            if (haveLocal)
            {
                int ack = data.GetDict("acks").GetInt(PlayerId, -1);
                NextInputSequence = Math.Max(NextInputSequence, ack + 1);
                Reconcile(serverPosition, ack);
            }
            SnapshotApplied?.Invoke();
        }

        /// <summary>The server position as of the last input it used, then the newer inputs replayed.</summary>
        void Reconcile(Vec2 serverPosition, int ack)
        {
            if (!Mirror.Players.TryGetValue(PlayerId, out PlayerState local)) return;
            while (_pending.Count > 0 && _pending[0].Tick <= ack) _pending.RemoveAt(0);
            local.Position = serverPosition;
            double dt = _rules.TickDt();
            foreach (PlayerInput input in _pending) Mirror.PredictMove(PlayerId, input, dt);
        }

        /// <summary>Plays the server's relayed signals on the mirror (MatchSim, WeaponSystem and the street game raise their events).</summary>
        void OnEvents(GArray list)
        {
            if (Mirror == null) return;
            foreach (object rawEvent in list)
            {
                if (!(rawEvent is GArray e) || e.Count != 3 || !(e[0] is string target) || !Wire.IsStringLike(e[1]) || !(e[2] is GArray args)) continue;
                string signal = Wire.Text(e[1]);
                object source = target == "sim" ? Mirror : target == "weapons" ? (object)Mirror.Weapons : target == "mode" ? Mirror.Mode : null;
                if (source == null) continue;
                EventReceived?.Invoke(target, signal, args);
                string name = EventRaiser.Pascal(signal);
                Type[] types = EventRaiser.ParameterTypes(source, name);
                if (types == null || types.Length != args.Count) continue;
                var call = new object[args.Count];
                bool ok = true;
                for (int i = 0; i < args.Count && ok; i++) ok = Convert(args[i], types[i], out call[i]);
                if (ok) EventRaiser.Raise(source, name, call);
            }
        }

        /// <summary>A wire argument as the event's parameter type (a weapon id becomes the mirror's WeaponDef).</summary>
        bool Convert(object value, Type type, out object result)
        {
            result = null;
            if (type == typeof(int) && Wire.IsInt(value)) result = (int)Wire.AsLong(value);
            else if (type == typeof(string) && Wire.IsStringLike(value)) result = Wire.Text(value);
            else if (type == typeof(StatusEffects.Type) && Wire.IsInt(value)) result = (StatusEffects.Type)Wire.AsLong(value);
            else if (type == typeof(Vec2) && value is Vec2) result = value;
            else if (type == typeof(WeaponDef) && Wire.IsStringLike(value) && Mirror.WeaponDefs.TryGetValue(Wire.Text(value), out WeaponDef def)) result = def;
            else if (type == typeof(double) && Wire.IsNumber(value)) result = Wire.Number(value);
            else if (type == typeof(bool) && value is bool) result = value;
            else if (type == typeof(List<Vec2>) && value is GArray points)
            {
                var path = new List<Vec2>();
                foreach (object point in points)
                    if (point is Vec2 v) path.Add(v);
                    else return false;
                result = path;
            }
            return result != null;
        }
    }
}
