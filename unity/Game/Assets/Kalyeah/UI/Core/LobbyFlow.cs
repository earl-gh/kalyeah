using System;
using System.Collections.Generic;
using Kalyeah.Net;

namespace Kalyeah.UI
{
    /// <summary>
    /// The room screen's behaviour with no widget in it (port of lobby_screen.gd). Online Create opens a room at once; Online Join lists
    /// the open rooms (tap one, Quick join, or type a private room's code); Local Create starts the room server on this phone and joins
    /// it; Local Join goes to the room the Local modal found. Every change goes to the authoritative server through the shared
    /// GameClient; the screen shows what the events say. `Dispose` lets go of the client (which outlives this screen).
    /// </summary>
    public sealed class LobbyFlow : IDisposable
    {
        /// <summary>Seconds before an Online Join screen that lost its connection tries again by itself.</summary>
        public const double BrowseRetry = 3.0;

        public event Action<string> StatusChanged;
        /// <summary>The room changed (null: no room any more).</summary>
        public event Action<RoomViewModel> RoomShown;
        public event Action<List<GDict>> RoomsListed;
        /// <summary>A short message over the screen: the text and its kind ("info", "warn", "error", "ok").</summary>
        public event Action<string, string> Toast;
        /// <summary>Something failed: the screen shows Try again.</summary>
        public event Action RetryShown;
        /// <summary>A refused join: the code boxes turn red with the reason.</summary>
        public event Action<string> CodeRefused;
        /// <summary>The host started: show the loading screen and hand the client to the match (once).</summary>
        public event Action<GDict> MatchLoading;
        /// <summary>The room ended for good (a Local host left).</summary>
        public event Action RoomClosed;

        public readonly bool Hotspot;
        public SessionModel.LobbyIntent Intent { get; private set; }
        public string Status { get; private set; } = "";
        public RoomViewModel Room { get; private set; }
        public bool Loading { get; private set; }
        public string ConnectedUrl { get; private set; } = "";
        /// <summary>The Online Join list is on screen (no room yet).</summary>
        public bool Browsing => !Hotspot && Intent == SessionModel.LobbyIntent.Join && Room == null && !Loading;
        public readonly CodeEntry Code;

        readonly NetHub _net;
        readonly GameClient _client;
        readonly SessionModel _session;
        readonly SettingsModel _settings;
        readonly MenuData _data;
        readonly Func<long> _seed;
        Action _pending;
        bool _transportReady;
        string _identifiedName = "";
        bool _rejoining;
        bool _announcedCharacter;
        int _lastHostId = -1;
        string _lastMap = "";
        double _listLeft;
        bool _disposed;
        bool _leaving;

        public LobbyFlow(NetHub net, SessionModel session, SettingsModel settings, bool hotspot, SessionModel.LobbyIntent intent, Func<long> seed)
        {
            _net = net;
            _client = net.Client;
            _session = session;
            _settings = settings;
            _data = net.Data;
            _seed = seed;
            Hotspot = hotspot;
            Intent = intent;
            Code = new CodeEntry(_data.Net.PasscodeAlphabet, _data.Net.PasscodeLength);
            _transportReady = _client.IsOnline();
            _identifiedName = _client.PlayerName;
            _client.ConnectionChanged += OnConnectionChanged;
            _client.ConnectFailed += OnConnectFailed;
            _client.ErrorReceived += OnError;
            _client.RoomChanged += OnRoomChanged;
            _client.MatchLoading += OnMatchLoading;
            _client.Welcomed += OnWelcomed;
            _client.RoomsListed += OnRoomsListed;
            _client.RoomClosed += OnRoomClosed;
        }

        /// <summary>The screen is up: go on with what the main screen asked for (Godot calls this deferred from _ready).</summary>
        public void Begin()
        {
            if (!_client.Room.IsEmpty)
            {
                OnRoomChanged(_client.Room);
                return;
            }
            if (Hotspot)
            {
                SetStatus(Intent == SessionModel.LobbyIntent.Create ? "Starting the room…" : "Joining the room…");
                Submit();
            }
            else if (Intent == SessionModel.LobbyIntent.Create)
            {
                SetStatus("Creating the room…");
                Submit();
            }
            else StartBrowsing();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.ConnectionChanged -= OnConnectionChanged;
            _client.ConnectFailed -= OnConnectFailed;
            _client.ErrorReceived -= OnError;
            _client.RoomChanged -= OnRoomChanged;
            _client.MatchLoading -= OnMatchLoading;
            _client.Welcomed -= OnWelcomed;
            _client.RoomsListed -= OnRoomsListed;
            _client.RoomClosed -= OnRoomClosed;
        }

        string ServerUrl() => SettingsModel.NormalizeUrl(_settings.ServerUrl);

        /// <summary>Create / Join / Try again: remember the name, connect if needed, then send.</summary>
        public void Submit()
        {
            _client.PlayerName = _session.PlayerName.Length > 0 ? _session.PlayerName : "Player";
            if (Hotspot)
            {
                SubmitHotspot();
                return;
            }
            string url = ServerUrl();
            if (url.Length == 0)
            {
                // the online server is built into the game; players never type it
                SetStatus(MenuCatalog.ComingSoon);
                return;
            }
            ConnectedUrl = url;
            if (Intent == SessionModel.LobbyIntent.Create) _pending = _client.CreateRoom;
            else
            {
                if (!Code.Complete)
                {
                    Code.Refuse(Code.MissingText());
                    CodeRefused?.Invoke(Code.ErrorText);
                    return;
                }
                string code = Code.Text;
                _pending = () => _client.JoinRoom(code);
            }
            ConnectOrSend(url);
        }

        /// <summary>The code boxes' Join: the typed code.</summary>
        public void JoinCode(string typed)
        {
            Code.Set(typed);
            Submit();
        }

        void SubmitHotspot()
        {
            if (Intent == SessionModel.LobbyIntent.Create)
            {
                if (!_net.StartHost(_seed()))
                {
                    SetStatus("Oops! This phone can't start the room.");
                    RetryShown?.Invoke();
                    return;
                }
                ConnectedUrl = _net.HostUrl();
                _pending = _client.CreateRoom;
                ConnectOrSend(ConnectedUrl);
                return;
            }
            if (_session.LobbyUrl.Length == 0)
            {
                SetStatus("No room found. Go back and tap Join room.");
                return;
            }
            _client.Close();
            ShowRoom(null);
            ConnectedUrl = _session.LobbyUrl;
            _pending = () => _client.JoinRoom("");
            ConnectOrSend(ConnectedUrl);
        }

        void ConnectOrSend(string url)
        {
            if (_client.IsOnline() && _identifiedName == _client.PlayerName)
            {
                if (_transportReady) SendPending();
                return;
            }
            SetStatus("Connecting…");
            if (!_client.ConnectTo(url, !Hotspot))
            {
                SetStatus(MenuCatalog.CantReach);
                RetryShown?.Invoke();
            }
        }

        /// <summary>Takes our slot in a running match back (after a restart or a long drop), within the reconnect window.</summary>
        public bool CanRejoin => _session.ReconnectToken.Length > 0 && _session.ReconnectUrl.Length > 0;

        public void Rejoin()
        {
            if (!CanRejoin) return;
            _client.Token = _session.ReconnectToken;
            _client.PlayerName = _session.PlayerName;
            _rejoining = true;
            SetStatus("Rejoining…");
            ConnectedUrl = _session.ReconnectUrl;
            if (!_client.ConnectTo(_session.ReconnectUrl, !_session.LobbyHotspot)) SetStatus(MenuCatalog.CantReach);
        }

        void SendPending()
        {
            if (_pending == null) return;
            SetStatus("");
            Action pending = _pending;
            _pending = null;
            pending();
        }

        /// <summary>Every frame: the waking hint, and the open room list asked again every `room_list_refresh`.</summary>
        public void Tick(double delta)
        {
            if (_disposed) return;
            // a free Online server asleep takes about a minute to answer: say so, so the wait does not look stuck
            if (!Hotspot && _client.Waking() && Status == "Connecting…") SetStatus(MenuCatalog.WakingText);
            if (!Browsing) return;
            _listLeft -= delta;
            if (_listLeft > 0.0) return;
            if (_transportReady)
            {
                _client.ListRooms();
                _listLeft = _data.Net.RoomListRefresh;
            }
            else if (!_client.IsOnline() && !_client.IsConnecting()) StartBrowsing();
        }

        /// <summary>Online Join: connect without sending; the list is asked for once the server says hello.</summary>
        void StartBrowsing()
        {
            _listLeft = BrowseRetry;
            _client.PlayerName = _session.PlayerName.Length > 0 ? _session.PlayerName : "Player";
            string url = ServerUrl();
            if (url.Length == 0)
            {
                SetStatus(MenuCatalog.ComingSoon);
                return;
            }
            ConnectedUrl = url;
            ConnectOrSend(url);
        }

        /// <summary>A room card or Quick join: join that open room.</summary>
        public void JoinListed(string code)
        {
            SetStatus("Joining the room…");
            _pending = () => _client.JoinRoom(code);
            ConnectOrSend(ConnectedUrl.Length > 0 ? ConnectedUrl : ServerUrl());
        }

        /// <summary>No open room to join: make one instead, on the same connection.</summary>
        public void CreateInstead()
        {
            Intent = SessionModel.LobbyIntent.Create;
            ShowRoom(null);
            SetStatus("Creating the room…");
            Submit();
        }

        // ---- the room's buttons -----------------------------------------------------------------------------------------------------

        /// <summary>The big button: the host starts (or is told why not), everyone else flips Ready.</summary>
        public void PressAction()
        {
            if (Room == null) return;
            if (_client.IsHost())
            {
                string note = _client.Room.GetStr("start_note");
                if (note.Length > 0)
                {
                    // the game's minimum is not met yet (or someone is not ready): say why and wait, nothing is sent
                    Toast?.Invoke(note, "warn");
                    return;
                }
                _client.SetReady(true);
                _client.StartMatch();
                return;
            }
            _client.SetReady(!Room.MyReady);
        }

        public void PickTeam(int team) => _client.PickTeam(team);
        public void PickMap(string mapId) => _client.SetMap(mapId);
        public void PickKid(string kid) => _client.SetCharacter(kid);
        public void SetFirstTo(int count) => _client.SetFirstTo(count);
        public void SetOpen(bool open) => _client.SetOpen(open);
        public void SetFillBots(bool on) => _client.SetFillBots(on);

        /// <summary>Leaves the room (and stops hosting); the screen goes back to the main screen.</summary>
        public void Leave()
        {
            _leaving = true;
            _lastHostId = -1;
            _lastMap = "";
            _announcedCharacter = false;
            if (!_client.Room.IsEmpty) _client.LeaveRoom();
            _client.Close();
            _net.StopHost();
            _session.ForgetMatch();
            ShowRoom(null);
        }

        // ---- client events ----------------------------------------------------------------------------------------------------------

        void OnRoomsListed(List<GDict> rooms)
        {
            if (Browsing) RoomsListed?.Invoke(rooms);
        }

        void OnConnectFailed()
        {
            _listLeft = BrowseRetry;
            SetStatus(MenuCatalog.CantReach);
            RetryShown?.Invoke();
        }

        void OnConnectionChanged(bool online)
        {
            _transportReady = false;
            if (Loading || _leaving) return;
            if (online)
            {
                SetStatus("Connecting…");
                return;
            }
            _listLeft = BrowseRetry;
            ShowRoom(null);
            SetStatus(MenuCatalog.ConnectionLost);
            RetryShown?.Invoke();
        }

        void OnWelcomed(bool reconnected)
        {
            _transportReady = true;
            _identifiedName = _client.PlayerName;
            if (Browsing && _pending == null)
            {
                SetStatus("");
                _listLeft = 0.0;
            }
            SendPending();
            if (!_rejoining) return;
            _rejoining = false;
            if (!reconnected)
            {
                SetStatus("That battle is over!");
                _session.ForgetMatch();
                _client.Token = "";
            }
        }

        void OnError(string text)
        {
            SetStatus(text);
            if (Browsing) _listLeft = _data.Net.RoomListMinGap; // a room filled or started under a tap: show the list as it is now
            if (_client.Room.IsEmpty)
            {
                if (!Hotspot && Intent == SessionModel.LobbyIntent.Join && Code.Text.Length > 0)
                {
                    Code.Refuse(text);
                    CodeRefused?.Invoke(text);
                }
                RetryShown?.Invoke();
            }
        }

        void OnRoomClosed()
        {
            SetStatus("The host left. The battle is over.");
            RoomClosed?.Invoke();
        }

        void OnRoomChanged(GDict room)
        {
            _session.RememberRoom(room.GetStr("code"), _client.Token, ConnectedUrl);
            SetStatus("");
            int hostId = room.GetInt("host", -1);
            if (_lastHostId >= 0 && hostId == _client.PlayerId && _lastHostId != _client.PlayerId)
                Toast?.Invoke("The host left. You're the host now!", "info");
            _lastHostId = hostId;
            // everyone hears when the host changes the game
            string mapId = room.GetStr("map", "street");
            if (_lastMap.Length > 0 && mapId != _lastMap)
            {
                string game = MenuCatalog.MapName(mapId);
                Toast?.Invoke(hostId == _client.PlayerId ? "You picked " + game + "!" : RoomViewModel.HostName(room) + " picked " + game + "!", "info");
            }
            _lastMap = mapId;
            if (!_announcedCharacter)
            {
                _announcedCharacter = true;
                if (_session.AvatarId.Length > 0) _client.SetCharacter(_session.AvatarId);
            }
            else
            {
                foreach (object raw in room.GetArr("members"))
                    if (raw is GDict member && member.GetInt("player_id", -2) == _client.PlayerId)
                    {
                        string kid = member.GetStr("char");
                        if (kid.Length > 0 && kid != _session.AvatarId) _session.SetAvatar(kid);
                    }
            }
            ShowRoom(RoomViewModel.From(room, _client.PlayerId, !Hotspot, _data, _session.AvatarId));
        }

        void OnMatchLoading(GDict info)
        {
            if (Loading) return;
            Loading = true;
            SetStatus("Loading…");
            MatchLoading?.Invoke(info);
        }

        void ShowRoom(RoomViewModel room)
        {
            Room = room;
            RoomShown?.Invoke(room);
        }

        void SetStatus(string text)
        {
            Status = text ?? "";
            StatusChanged?.Invoke(Status);
        }

        /// <summary>Words that mark a problem rather than something in progress (ui_status_strip.gd PROBLEM_WORDS).</summary>
        static readonly string[] ProblemWords = { "can't", "cannot", "lost", "no room", "ended", "is over", "full", "already", "isn't", "failed", "enter ", "need", "turn on" };

        /// <summary>The kind of a status line (ui_status_strip.gd kind_of): "wait" for one ending in an ellipsis, "error" for a problem, else "info".</summary>
        public static string KindOf(string text)
        {
            text = text ?? "";
            if (text.EndsWith("...", StringComparison.Ordinal) || text.EndsWith("…", StringComparison.Ordinal)) return "wait";
            string lower = text.ToLowerInvariant();
            foreach (string word in ProblemWords)
                if (lower.Contains(word)) return "error";
            return "info";
        }
    }
}
