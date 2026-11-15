using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Kalyeah.Net;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// 10-slot 5v5 Blue vs Red Online &amp; Hotspot Room Lobby wired to AppSession.Current.Client / HostServer (PollNetwork(dt)):
    /// 8-char Room Code display/copy/join, Server URL box + Open Rooms browser, Blue/Red Team swap, Ready toggle, Pick Kid shortcut
    /// callback (+ quick in-room kid cycle), Host Bot-Fill / Map / First-To / Open toggles, and Start Match button firing onMatchStarted.
    /// </summary>
    public sealed class LobbyScreenView : IDisposable
    {
        const int SlotsPerTeam = RoomService.MaxTeamSize; // 5
        const int MaxOpenRoomRows = 4;
        const float BrowseRetrySeconds = 3f;
        const string WakingHint = "Waking up the server. This can take a minute...";

        static readonly Color FrameBg = new Color(0.07f, 0.11f, 0.22f, 0.96f);
        static readonly Color PanelBg = new Color(0.11f, 0.16f, 0.30f, 0.96f);
        static readonly Color BlueHeader = new Color(0.12f, 0.56f, 0.78f, 1f);
        static readonly Color RedHeader = new Color(0.86f, 0.31f, 0.25f, 1f);
        static readonly Color BlueSlotFill = new Color(0.10f, 0.24f, 0.38f, 0.95f);
        static readonly Color RedSlotFill = new Color(0.34f, 0.15f, 0.20f, 0.95f);
        static readonly Color EmptySlotFill = new Color(0.08f, 0.12f, 0.22f, 0.86f);
        static readonly Color GoldCta = new Color(0.98f, 0.78f, 0.18f, 1f);
        static readonly Color GreenReady = new Color(0.22f, 0.78f, 0.48f, 1f);
        static readonly Color MutedBtn = new Color(0.17f, 0.25f, 0.42f, 1f);
        static readonly Color InkDark = new Color(0.14f, 0.09f, 0.04f, 1f);
        static readonly Color SubtleText = new Color(0.76f, 0.84f, 0.96f, 1f);
        static readonly Color WarnText = new Color(1f, 0.78f, 0.34f, 1f);
        static readonly Color ErrorText = new Color(1f, 0.48f, 0.44f, 1f);

        static Sprite _fallbackQrSprite;

        readonly DataSource _data;
        readonly Canvas _canvas;
        readonly NetRules _netRules;
        readonly string[] _allWeaponIds;

        readonly RectTransform _frame;
        readonly RectTransform _connectPanel;
        readonly RectTransform _roomPanel;
        readonly RectTransform _qrOverlay;
        readonly Image _qrModalImage;
        readonly Text _qrModalCaption;

        // Top bar
        readonly Text _modeTitleLabel;
        readonly Text _statusLabel;
        readonly RectTransform _roomCodePill;
        readonly Text _roomCodeLabel;

        // Online Connect / Code Join panel
        readonly InputField _serverUrlInput;
        readonly InputField _joinCodeInput;
        readonly Text _openRoomsEmptyLabel;
        readonly OpenRoomRow[] _openRoomRows = new OpenRoomRow[MaxOpenRoomRows];

        // 10-slot 5v5 Blue vs Red grid
        readonly Text _blueHeaderLabel;
        readonly Text _redHeaderLabel;
        readonly SlotWidgets[] _blueSlots = new SlotWidgets[SlotsPerTeam];
        readonly SlotWidgets[] _redSlots = new SlotWidgets[SlotsPerTeam];

        // Bottom controls
        readonly RawImage _myKidThumb;
        readonly Text _myKidLabel;
        readonly Text _mapBtnLabel;
        readonly Text _firstToBtnLabel;
        readonly Text _botsBtnLabel;
        readonly Button _botsButton;
        readonly Text _openBtnLabel;
        readonly Button _openButton;
        readonly Image _readyBtnBg;
        readonly Text _readyBtnLabel;
        readonly Button _startButton;
        readonly Image _startBtnBg;
        readonly Text _startBtnLabel;

        readonly List<RectTransform> _touchTargets = new List<RectTransform>(32);
        readonly Dictionary<string, Texture2D> _portraitCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        GameClient _boundClient;
        Sprite _ownedQrSprite;
        Action _pendingAfterWelcome;
        bool _transportWelcomed;
        bool _announcedCharacter;
        bool _matchStartFired;
        float _listRefreshLeft;
        string _lastAnnouncedChar = "";

        struct SlotWidgets
        {
            public RectTransform Root;
            public Image Bg;
            public RawImage Portrait;
            public Text NameLabel;
            public Text SubLabel;
            public Image BadgeBg;
            public Text BadgeLabel;
        }

        struct OpenRoomRow
        {
            public RectTransform Root;
            public Text Label;
            public Text Meta;
            public string Code;
        }

        public RectTransform Root { get; }
        public bool AutoPollNetwork { get; set; } = true;
        public string StatusText { get; private set; } = "";

        public bool InRoom => _boundClient != null && !_boundClient.Room.IsEmpty;
        public string RoomCodeText => _boundClient != null ? _boundClient.Room.GetStr("code", "") : "";

        public Action OnBack { get; set; }
        public Action OnMatchStarted { get; set; }
        public Action OnPickKid { get; set; }
        public Func<string, Sprite> QrSpriteProvider { get; set; }
        public Func<string> LanIpProvider { get; set; }
        public Action<Action<string>> QrScanRequester { get; set; }

        public LobbyScreenView(
            Transform parent,
            DataSource data = null,
            Action onBack = null,
            Action onMatchStarted = null,
            Action onPickKid = null,
            Func<string, Sprite> qrSpriteProvider = null,
            Func<string> lanIpProvider = null,
            Action<Action<string>> qrScanRequester = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data ?? KalyeahResources.Data();
            OnBack = onBack;
            OnMatchStarted = onMatchStarted;
            OnPickKid = onPickKid;
            QrSpriteProvider = qrSpriteProvider;
            LanIpProvider = lanIpProvider;
            QrScanRequester = qrScanRequester;

            SimData simData = AppSession.Current.EnsureSimData(_data);
            _netRules = NetRules.FromData(simData);
            _allWeaponIds = ExtractDeckWeaponIds(simData);
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("LobbyScreenView", parent);
            Image backdrop = Ui.MakeImage("Backdrop", Root, new Color(0.04f, 0.07f, 0.15f, 0.95f), raycast: true);
            Stretch(backdrop.rectTransform);

            _frame = Ui.Panel(
                "LobbyFrame",
                Root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1180f, 668f));
            StyleCard(_frame, FrameBg);

            BuildTopBar(_frame, out _modeTitleLabel, out _statusLabel, out _roomCodePill, out _roomCodeLabel);

            _connectPanel = Ui.Panel("OnlineConnectPanel", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-36f, -84f));
            BuildConnectPanel(_connectPanel, out _serverUrlInput, out _joinCodeInput, out _openRoomsEmptyLabel);

            _roomPanel = Ui.Panel("RoomPanel", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-32f, -84f));
            BuildRoomPanel(
                _roomPanel,
                out _blueHeaderLabel,
                out _redHeaderLabel,
                out _myKidThumb,
                out _myKidLabel,
                out _mapBtnLabel,
                out _firstToBtnLabel,
                out _botsButton,
                out _botsBtnLabel,
                out _openButton,
                out _openBtnLabel,
                out _readyBtnBg,
                out _readyBtnLabel,
                out _startButton,
                out _startBtnBg,
                out _startBtnLabel);

            _qrOverlay = BuildQrModal(_frame, out _qrModalImage, out _qrModalCaption);
            _qrOverlay.gameObject.SetActive(false);

            var driver = Root.gameObject.AddComponent<LobbyViewDriver>();
            driver.Bind(this);

            ApplyResponsiveMetrics();
            Open();
        }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            OnShow();
        }

        public void Hide() => Root.gameObject.SetActive(false);

        public void Open() => OnShow();

        public void OnShow()
        {
            _matchStartFired = false;
            ApplyResponsiveMetrics();

            AppSession session = AppSession.Current;
            if (_serverUrlInput != null && string.IsNullOrWhiteSpace(_serverUrlInput.text))
            {
                _serverUrlInput.text = !string.IsNullOrWhiteSpace(session.ServerUrl)
                    ? session.ServerUrl
                    : "ws://127.0.0.1:" + _netRules.DefaultPort.ToString(CultureInfo.InvariantCulture);
            }

            GameClient client = session.EnsureClient(_data);
            client.PlayerName = session.PlayerName;
            BindClient(client);
            RefreshMyKidChip();

            if (!client.Room.IsEmpty)
            {
                _transportWelcomed = client.IsOnline();
                SyncCharacterToRoomIfChanged();
                ShowRoomView(true);
                RenderRoom(client.Room);
                return;
            }

            if (session.Mode == GameMode.HotspotHost)
            {
                _modeTitleLabel.text = "HOTSPOT ROOM LOBBY (HOST)";
                ShowRoomView(true);
                if (!session.IsHosting && !session.StartHotspotHost(_data))
                {
                    SetStatus("Could not start Hotspot server. Check Wi-Fi / Hotspot and retry.", WarnText);
                    return;
                }
                BindClient(session.Client);
                QueueOrRunRoomAction(() => _boundClient?.CreateRoom(), "Starting Hotspot room...");
                return;
            }

            if (session.Mode == GameMode.HotspotClient)
            {
                _modeTitleLabel.text = "HOTSPOT ROOM LOBBY (GUEST)";
                ShowRoomView(true);
                if (!client.IsOnline() && !client.IsConnecting() && !string.IsNullOrWhiteSpace(session.HostEndpoint))
                    session.ConnectHotspotGuest(_data, session.HostEndpoint);
                BindClient(session.Client);
                QueueOrRunRoomAction(() => _boundClient?.JoinRoom(""), "Joining Hotspot room...");
                return;
            }

            // Online mode
            _modeTitleLabel.text = "ONLINE 5v5 ROOM LOBBY";
            if (session.Intent == LobbyIntent.Create && !string.IsNullOrWhiteSpace(session.ServerUrl))
            {
                ShowRoomView(false);
                ConnectAndCreateOnlineRoom(session.ServerUrl);
            }
            else
            {
                ShowRoomView(false);
                SetStatus("Enter Server URL + 8-char Room Code, or browse Open Rooms.", SubtleText);
                if (!string.IsNullOrWhiteSpace(session.ServerUrl))
                    ConnectForOnlineBrowse(session.ServerUrl);
            }
        }

        public void SetFields(string serverUrl, string playerName, string roomCode = "")
        {
            if (!string.IsNullOrWhiteSpace(playerName))
                AppSession.Current.PlayerName = playerName;
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                AppSession.Current.ServerUrl = SettingsStore.NormalizeServerUrl(serverUrl);
                if (_serverUrlInput != null)
                    _serverUrlInput.text = AppSession.Current.ServerUrl;
            }
            if (_joinCodeInput != null && roomCode != null)
                _joinCodeInput.text = CleanRoomCode(roomCode);
        }

        public void ConnectAndCreateOnlineRoom(string rawUrl = null)
        {
            string url = ResolveInputServerUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
            {
                SetStatus("Enter a valid WebSocket server URL first.", WarnText);
                return;
            }

            AppSession session = AppSession.Current;
            session.Mode = GameMode.Online;
            session.Intent = LobbyIntent.Create;
            _announcedCharacter = false;
            _pendingAfterWelcome = () => _boundClient?.CreateRoom();

            if (_boundClient != null && _boundClient.IsOnline() && _transportWelcomed &&
                string.Equals(session.ServerUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Creating room...", SubtleText);
                RunPendingWelcomeAction();
                return;
            }

            _transportWelcomed = false;
            if (!session.StartOnlineClient(_data, url, patient: true))
            {
                SetStatus("Can't reach " + url + ". Check the Server URL and try again.", ErrorText);
                return;
            }
            BindClient(session.Client);
            SetStatus("Connecting to " + url + "...", SubtleText);
            SaveSessionPrefs();
        }

        public void JoinByCode(string rawCode = null, string rawUrl = null)
        {
            string code = CleanRoomCode(rawCode ?? (_joinCodeInput != null ? _joinCodeInput.text : ""));
            if (_joinCodeInput != null)
                _joinCodeInput.text = code;

            if (code.Length != _netRules.PasscodeLength)
            {
                SetStatus(
                    string.Format(CultureInfo.InvariantCulture, "Enter all {0} room code characters!", _netRules.PasscodeLength),
                    WarnText);
                return;
            }

            string url = ResolveInputServerUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
            {
                SetStatus("Enter a valid Server URL first.", WarnText);
                return;
            }

            AppSession session = AppSession.Current;
            session.Mode = GameMode.Online;
            session.Intent = LobbyIntent.Join;
            _announcedCharacter = false;
            _pendingAfterWelcome = () => _boundClient?.JoinRoom(code);

            if (_boundClient != null && _boundClient.IsOnline() && _transportWelcomed &&
                string.Equals(session.ServerUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Joining room " + code + "...", SubtleText);
                RunPendingWelcomeAction();
                return;
            }

            _transportWelcomed = false;
            if (!session.StartOnlineClient(_data, url, patient: true))
            {
                SetStatus("Can't reach " + url + ". Check your connection and try again.", ErrorText);
                return;
            }
            BindClient(session.Client);
            SetStatus("Connecting to join " + code + "...", SubtleText);
            SaveSessionPrefs();
        }

        public void ConnectForOnlineBrowse(string rawUrl = null)
        {
            string url = ResolveInputServerUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
            {
                SetStatus("Enter a Server URL to browse open rooms.", WarnText);
                return;
            }

            AppSession session = AppSession.Current;
            session.Mode = GameMode.Online;
            session.Intent = LobbyIntent.Join;

            if (_boundClient != null && _boundClient.IsOnline() && _transportWelcomed &&
                string.Equals(session.ServerUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                _boundClient.ListRooms();
                _listRefreshLeft = Mathf.Max(1f, (float)_netRules.RoomListRefresh);
                SetStatus("Refreshing open rooms...", SubtleText);
                return;
            }

            _pendingAfterWelcome = () =>
            {
                _boundClient?.ListRooms();
                _listRefreshLeft = Mathf.Max(1f, (float)_netRules.RoomListRefresh);
            };
            _transportWelcomed = false;
            if (!session.StartOnlineClient(_data, url, patient: true))
            {
                SetStatus("Can't reach " + url + ".", ErrorText);
                return;
            }
            BindClient(session.Client);
            SetStatus("Connecting to server...", SubtleText);
        }

        public void SwapTeam()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty) return;
            int myTeam = _boundClient.MyTeam();
            int nextTeam = myTeam == 0 ? 1 : 0;
            _boundClient.PickTeam(nextTeam);
        }

        public void PickTeam(int team)
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty) return;
            if (team == 0 || team == 1)
                _boundClient.PickTeam(team);
        }

        public void PressReady()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty) return;
            bool myReady = IsLocalMemberReady(_boundClient.Room, _boundClient.PlayerId);
            _boundClient.SetReady(!myReady);
        }

        public void ToggleBotFill()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty || !_boundClient.IsHost()) return;
            bool nowFill = _boundClient.Room.GetBool("fill_bots", false);
            _boundClient.SetFillBots(!nowFill);
        }

        public void CycleMap()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty || !_boundClient.IsHost()) return;
            string current = _boundClient.Room.GetStr("map", "street");
            int idx = Array.IndexOf(RoomService.PlayableMapIds, current);
            string next = RoomService.PlayableMapIds[(idx + 1 + RoomService.PlayableMapIds.Length) % RoomService.PlayableMapIds.Length];
            _boundClient.SetMap(next);
        }

        public void CycleFirstTo()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty || !_boundClient.IsHost()) return;
            int[] choices = { 3, 5, 7 };
            int current = _boundClient.Room.GetInt("first_to", 3);
            int idx = Array.IndexOf(choices, current);
            int next = choices[(idx + 1) % choices.Length];
            _boundClient.SetFirstTo(next);
        }

        public void ToggleOpenPrivacy()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty || !_boundClient.IsHost()) return;
            bool open = _boundClient.Room.GetBool("open", true);
            _boundClient.SetOpen(!open);
        }

        public void CycleKid(int delta)
        {
            string[] roster = AppSession.DefaultRoster;
            int idx = Array.IndexOf(roster, AppSession.Current.SelectedCharacter);
            if (idx < 0) idx = 0;
            string next = roster[(idx + delta + roster.Length) % roster.Length];
            AppSession.Current.SelectedCharacter = next;
            RefreshMyKidChip();
            if (_boundClient != null && !_boundClient.Room.IsEmpty)
            {
                _lastAnnouncedChar = next;
                _boundClient.SetCharacter(next);
            }
            SaveSessionPrefs();
        }

        public void PressStart()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty) return;
            if (!_boundClient.IsHost())
            {
                PressReady();
                return;
            }

            string blocker = _boundClient.Room.GetStr("start_note", "");
            if (!string.IsNullOrEmpty(blocker))
            {
                SetStatus(blocker, WarnText);
                return;
            }

            _boundClient.SetReady(true);
            _boundClient.StartMatch();
        }

        public void LeaveRoomToSetup()
        {
            if (_boundClient != null && !_boundClient.Room.IsEmpty)
                _boundClient.LeaveRoom();

            AppSession session = AppSession.Current;
            session.RoomCode = "";
            session.ReconnectToken = "";
            SaveSessionPrefs();

            if (session.Mode == GameMode.HotspotHost || session.Mode == GameMode.HotspotClient)
            {
                session.StopAllNetwork();
                OnBack?.Invoke();
                return;
            }

            ShowRoomView(false);
            SetStatus("Left the room.", SubtleText);
        }

        public bool HandleBack()
        {
            if (_qrOverlay != null && _qrOverlay.gameObject.activeSelf)
            {
                _qrOverlay.gameObject.SetActive(false);
                return true;
            }
            if (_boundClient != null && _boundClient.InMatch())
                return true;

            if (InRoom && AppSession.Current.Mode == GameMode.Online)
            {
                LeaveRoomToSetup();
                return true;
            }

            if (_boundClient != null && !_boundClient.Room.IsEmpty)
                _boundClient.LeaveRoom();
            AppSession.Current.StopAllNetwork();
            OnBack?.Invoke();
            return true;
        }

        public void Tick(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (AutoPollNetwork)
                AppSession.Current.PollNetwork(dt);

            GameClient client = AppSession.Current.Client;
            if (!ReferenceEquals(client, _boundClient))
                BindClient(client);

            if (_boundClient != null && AppSession.Current.Mode == GameMode.Online && _boundClient.Waking() &&
                StatusText.StartsWith("Connecting", StringComparison.Ordinal))
            {
                SetStatus(WakingHint, WarnText);
            }

            if (IsBrowsingOpenRooms())
            {
                _listRefreshLeft -= dt;
                if (_listRefreshLeft <= 0f)
                {
                    if (_boundClient != null && _boundClient.IsOnline() && _transportWelcomed)
                    {
                        _boundClient.ListRooms();
                        _listRefreshLeft = Mathf.Max(1.5f, (float)_netRules.RoomListRefresh);
                    }
                    else
                    {
                        _listRefreshLeft = BrowseRetrySeconds;
                    }
                }
            }
        }

        public void Dispose()
        {
            BindClient(null);
            ReleaseOwnedQrSprite();
        }

        bool IsBrowsingOpenRooms() =>
            AppSession.Current.Mode == GameMode.Online &&
            _connectPanel != null &&
            _connectPanel.gameObject.activeSelf &&
            (_boundClient == null || _boundClient.Room.IsEmpty);

        void QueueOrRunRoomAction(Action action, string connectingMessage)
        {
            _announcedCharacter = false;
            _pendingAfterWelcome = action;
            if (_boundClient != null && _boundClient.IsOnline())
            {
                _transportWelcomed = true;
                RunPendingWelcomeAction();
            }
            else
            {
                SetStatus(connectingMessage, SubtleText);
            }
        }

        void RunPendingWelcomeAction()
        {
            Action work = _pendingAfterWelcome;
            _pendingAfterWelcome = null;
            work?.Invoke();
        }

        void BindClient(GameClient client)
        {
            if (ReferenceEquals(_boundClient, client)) return;
            if (_boundClient != null)
            {
                _boundClient.ConnectionChanged -= OnClientConnectionChanged;
                _boundClient.ConnectFailed -= OnClientConnectFailed;
                _boundClient.Welcomed -= OnClientWelcomed;
                _boundClient.ErrorReceived -= OnClientError;
                _boundClient.RoomChanged -= OnClientRoomChanged;
                _boundClient.RoomClosed -= OnClientRoomClosed;
                _boundClient.RoomsListed -= OnClientRoomsListed;
                _boundClient.MatchLoading -= OnClientMatchLoading;
            }
            _boundClient = client;
            if (_boundClient != null)
            {
                _boundClient.ConnectionChanged += OnClientConnectionChanged;
                _boundClient.ConnectFailed += OnClientConnectFailed;
                _boundClient.Welcomed += OnClientWelcomed;
                _boundClient.ErrorReceived += OnClientError;
                _boundClient.RoomChanged += OnClientRoomChanged;
                _boundClient.RoomClosed += OnClientRoomClosed;
                _boundClient.RoomsListed += OnClientRoomsListed;
                _boundClient.MatchLoading += OnClientMatchLoading;
            }
        }

        void OnClientConnectionChanged(bool online)
        {
            _transportWelcomed = false;
            if (online)
            {
                SetStatus("Connected — shaking hands...", SubtleText);
            }
            else
            {
                _listRefreshLeft = BrowseRetrySeconds;
                if (AppSession.Current.Mode == GameMode.Online)
                    ShowRoomView(false);
                SetStatus("Connection lost. Check network and try again.", WarnText);
            }
        }

        void OnClientConnectFailed()
        {
            _listRefreshLeft = BrowseRetrySeconds;
            SetStatus("Can't reach the server. Check the URL or Wi-Fi and try again.", ErrorText);
        }

        void OnClientWelcomed(bool reconnected)
        {
            _transportWelcomed = true;
            if (IsBrowsingOpenRooms() && _pendingAfterWelcome == null)
            {
                SetStatus("Connected! Pick an open room or enter an 8-char code.", SubtleText);
                _listRefreshLeft = 0f;
            }
            RunPendingWelcomeAction();
        }

        void OnClientError(string text)
        {
            if (!string.IsNullOrEmpty(text))
                SetStatus(text, ErrorText);
            if (IsBrowsingOpenRooms())
                _listRefreshLeft = Mathf.Max(1f, (float)_netRules.RoomListMinGap);
        }

        void OnClientRoomClosed()
        {
            SetStatus("The host closed the room.", WarnText);
            if (AppSession.Current.Mode == GameMode.Online)
                ShowRoomView(false);
            else
                OnBack?.Invoke();
        }

        void OnClientRoomsListed(List<GDict> rooms)
        {
            int count = rooms != null ? rooms.Count : 0;
            _openRoomsEmptyLabel.gameObject.SetActive(count == 0);
            if (count == 0)
                _openRoomsEmptyLabel.text = "No open public rooms yet — tap CREATE ROOM or enter a friend's 8-char code!";

            for (int i = 0; i < MaxOpenRoomRows; i++)
            {
                if (i < count)
                {
                    GDict r = rooms[i];
                    string code = r.GetStr("code", "");
                    string host = r.GetStr("host_name", "Host");
                    int players = r.GetInt("players", 1);
                    int cap = Math.Max(2, r.GetInt("team_size", 5) * 2);
                    string mapId = r.GetStr("map", "street");
                    int firstTo = r.GetInt("first_to", 3);

                    _openRoomRows[i].Code = code;
                    _openRoomRows[i].Label.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}'s Room   [{1}]",
                        string.IsNullOrWhiteSpace(host) ? "Host" : host,
                        code);
                    _openRoomRows[i].Meta.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} • FT{1} • {2}/{3}   [JOIN]",
                        MapTitle(mapId),
                        firstTo,
                        players,
                        cap);
                    _openRoomRows[i].Root.gameObject.SetActive(true);
                }
                else
                {
                    _openRoomRows[i].Code = "";
                    _openRoomRows[i].Root.gameObject.SetActive(false);
                }
            }
        }

        void OnClientRoomChanged(GDict room)
        {
            if (room == null || room.IsEmpty)
            {
                ShowRoomView(false);
                return;
            }

            ShowRoomView(true);

            AppSession session = AppSession.Current;
            session.RoomCode = room.GetStr("code", "");
            session.FirstTo = room.GetInt("first_to", session.FirstTo);
            if (_boundClient != null)
            {
                session.ReconnectToken = _boundClient.Token ?? "";
                if (_boundClient.PlayerId > 0)
                    session.LocalPeerId = _boundClient.PlayerId;
            }
            SaveSessionPrefs();

            if (!_announcedCharacter && _boundClient != null)
            {
                _announcedCharacter = true;
                _lastAnnouncedChar = session.SelectedCharacter;
                _boundClient.SetCharacter(session.SelectedCharacter);
            }
            else if (_boundClient != null)
            {
                foreach (object raw in room.GetArr("members"))
                {
                    if (raw is GDict m && m.GetInt("player_id", -1) == _boundClient.PlayerId)
                    {
                        string serverChar = m.GetStr("char", "");
                        if (!string.IsNullOrEmpty(serverChar) && AppSession.IsKnownAvatar(serverChar))
                        {
                            session.SelectedCharacter = serverChar;
                            _lastAnnouncedChar = serverChar;
                            RefreshMyKidChip();
                        }
                        break;
                    }
                }
            }

            RenderRoom(room);
        }

        void OnClientMatchLoading(GDict info)
        {
            if (_boundClient == null) return;

            AppSession session = AppSession.Current;
            if (!_boundClient.Room.IsEmpty)
                session.FirstTo = _boundClient.Room.GetInt("first_to", session.FirstTo);

            // Confirm scene readiness and lock local superpower loadout so ServerMatch transitions straight into Playing.
            _boundClient.Loaded();
            string[] picks = session.ValidLoadoutFor(_allWeaponIds, 3);
            if (picks.Length >= 2)
            {
                string third = picks.Length >= 3 ? picks[2] : "";
                _boundClient.PickWeapons(picks[0], picks[1], session.SelectedCharacter, third);
            }

            SaveSessionPrefs();
            SetStatus("Match starting! Entering Barangay arena...", GreenReady);

            if (!_matchStartFired)
            {
                _matchStartFired = true;
                OnMatchStarted?.Invoke();
            }
        }

        void SyncCharacterToRoomIfChanged()
        {
            if (_boundClient == null || _boundClient.Room.IsEmpty) return;
            string selected = AppSession.Current.SelectedCharacter;
            if (!string.Equals(selected, _lastAnnouncedChar, StringComparison.Ordinal))
            {
                _lastAnnouncedChar = selected;
                _boundClient.SetCharacter(selected);
            }
        }

        void RenderRoom(GDict room)
        {
            int myId = _boundClient != null ? _boundClient.PlayerId : -1;
            int hostId = room.GetInt("host", -1);
            bool amHost = myId >= 0 && myId == hostId;
            string code = room.GetStr("code", "");
            string mapId = room.GetStr("map", "street");
            int firstTo = room.GetInt("first_to", 3);
            bool fillBots = room.GetBool("fill_bots", false);
            bool botsAllowed = room.GetBool("bots_allowed", false);
            bool openRoom = room.GetBool("open", true);
            bool canStartHost = room.GetBool("can_start_host", false);
            string startNote = room.GetStr("start_note", "");

            bool isHotspot = AppSession.Current.Mode == GameMode.HotspotHost || AppSession.Current.Mode == GameMode.HotspotClient;
            if (isHotspot)
            {
                string endpoint = AppSession.Current.Mode == GameMode.HotspotHost
                    ? "ws://" + ResolveShareLanIp() + ":" + _netRules.DefaultPort.ToString(CultureInfo.InvariantCulture)
                    : AppSession.Current.HostEndpoint;
                _roomCodeLabel.text = string.IsNullOrEmpty(code) ? endpoint : (code + "  •  " + endpoint);
            }
            else
            {
                _roomCodeLabel.text = "CODE: " + code;
            }

            GArray members = room.GetArr("members");
            var blueMembers = new List<GDict>(SlotsPerTeam);
            var redMembers = new List<GDict>(SlotsPerTeam);
            bool myReady = false;

            foreach (object raw in members)
            {
                if (!(raw is GDict m)) continue;
                int pid = m.GetInt("player_id", -1);
                if (pid == myId) myReady = m.GetBool("ready", false);
                int team = m.GetInt("team", 0);
                if (team == 1 && redMembers.Count < SlotsPerTeam)
                    redMembers.Add(m);
                else if (blueMembers.Count < SlotsPerTeam)
                    blueMembers.Add(m);
                else if (redMembers.Count < SlotsPerTeam)
                    redMembers.Add(m);
            }

            _blueHeaderLabel.text = string.Format(CultureInfo.InvariantCulture, "BLUE SQUAD ({0}/5) — TAP TO JOIN", blueMembers.Count);
            _redHeaderLabel.text = string.Format(CultureInfo.InvariantCulture, "RED SQUAD ({0}/5) — TAP TO JOIN", redMembers.Count);

            Color blueAccent = SettingsStore.Current.ColorblindMode == 3 ? new Color(0.16f, 0.66f, 0.88f, 1f) : BlueSlotFill;
            Color redAccent = SettingsStore.Current.ColorblindMode > 0 ? new Color(0.46f, 0.22f, 0.12f, 1f) : RedSlotFill;

            PopulateTeamSlots(_blueSlots, blueMembers, myId, hostId, fillBots, blueAccent, "Blue");
            PopulateTeamSlots(_redSlots, redMembers, myId, hostId, fillBots, redAccent, "Red");

            _mapBtnLabel.text = "GAME: " + MapTitle(mapId).ToUpperInvariant() + (amHost ? "  [CHANGE]" : "");
            _firstToBtnLabel.text = string.Format(CultureInfo.InvariantCulture, "FIRST TO {0}{1}", firstTo, amHost ? "  [+]" : "");

            _botsButton.gameObject.SetActive(botsAllowed || isHotspot);
            _botsButton.interactable = amHost && botsAllowed;
            _botsBtnLabel.text = fillBots ? "BOT-FILL: ON" : "BOT-FILL: OFF";

            _openButton.gameObject.SetActive(!isHotspot);
            _openButton.interactable = amHost && !isHotspot;
            _openBtnLabel.text = openRoom ? "PUBLIC ROOM" : "CODE ONLY";

            _readyBtnBg.color = myReady ? GreenReady : MutedBtn;
            _readyBtnLabel.text = myReady ? "READY! (TAP UNREADY)" : "TAP READY";

            _startButton.gameObject.SetActive(amHost);
            _startBtnBg.color = canStartHost ? GoldCta : new Color(0.36f, 0.34f, 0.26f, 1f);
            _startBtnLabel.color = canStartHost ? InkDark : Ui.Cream;
            _startBtnLabel.text = canStartHost ? "START MATCH!" : "START (WAITING)";

            if (string.IsNullOrEmpty(startNote))
                SetStatus(amHost ? "All players ready — tap START MATCH!" : "All players ready — waiting for Host to start!", GreenReady);
            else
                SetStatus(startNote, SubtleText);
        }

        void PopulateTeamSlots(
            SlotWidgets[] slots,
            List<GDict> teamMembers,
            int myId,
            int hostId,
            bool fillBots,
            Color teamFill,
            string teamLabel)
        {
            for (int i = 0; i < SlotsPerTeam; i++)
            {
                SlotWidgets slot = slots[i];
                if (i < teamMembers.Count)
                {
                    GDict m = teamMembers[i];
                    int pid = m.GetInt("player_id", -1);
                    string pName = m.GetStr("name", "Player");
                    string charId = m.GetStr("char", "junjun");
                    string rankTier = m.GetStr("rank_tier", "Kahoy");
                    bool ready = m.GetBool("ready", false);
                    bool connected = m.GetBool("connected", true);
                    bool afk = m.GetBool("afk", false);

                    var tags = new StringBuilder(pName);
                    if (pid == myId) tags.Append(" (YOU)");
                    if (pid == hostId) tags.Append(" [HOST]");

                    slot.Bg.color = teamFill;
                    slot.NameLabel.text = tags.ToString();
                    slot.NameLabel.color = Ui.Cream;
                    slot.SubLabel.text = PrettyKidName(charId) + "  •  " + rankTier;

                    Texture2D tex = LoadPortrait(charId);
                    slot.Portrait.texture = tex;
                    slot.Portrait.color = tex != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);

                    if (!connected)
                    {
                        slot.BadgeBg.color = ErrorText;
                        slot.BadgeLabel.text = "DROPPED";
                    }
                    else if (afk)
                    {
                        slot.BadgeBg.color = WarnText;
                        slot.BadgeLabel.text = "AFK";
                    }
                    else if (ready || pid == hostId)
                    {
                        slot.BadgeBg.color = GreenReady;
                        slot.BadgeLabel.text = pid == hostId && !ready ? "HOST" : "READY";
                    }
                    else
                    {
                        slot.BadgeBg.color = MutedBtn;
                        slot.BadgeLabel.text = "WAITING";
                    }
                }
                else
                {
                    slot.Bg.color = EmptySlotFill;
                    slot.Portrait.texture = null;
                    slot.Portrait.color = new Color(1f, 1f, 1f, 0.08f);
                    if (fillBots)
                    {
                        slot.NameLabel.text = string.Format(CultureInfo.InvariantCulture, "{0} Bot #{1}", teamLabel, i + 1);
                        slot.NameLabel.color = SubtleText;
                        slot.SubLabel.text = "AI Squadmate (Auto-fill)";
                        slot.BadgeBg.color = new Color(0.22f, 0.36f, 0.52f, 1f);
                        slot.BadgeLabel.text = "BOT";
                    }
                    else
                    {
                        slot.NameLabel.text = "Open Slot";
                        slot.NameLabel.color = new Color(0.62f, 0.70f, 0.84f, 0.85f);
                        slot.SubLabel.text = "Tap " + teamLabel + " header to take slot";
                        slot.BadgeBg.color = new Color(0.14f, 0.18f, 0.28f, 0.8f);
                        slot.BadgeLabel.text = "EMPTY";
                    }
                }
            }
        }

        void BuildTopBar(
            RectTransform parent,
            out Text modeTitle,
            out Text statusLabel,
            out RectTransform codePill,
            out Text codeLabel)
        {
            RectTransform bar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 56f));

            MakeButton("BackBtn", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(124f, 50f), "BACK", MutedBtn, Ui.Cream, 20, () => HandleBack());

            modeTitle = Ui.MakeText("LobbyModeTitle", bar, "5v5 ROOM LOBBY", 28, GoldCta, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
            modeTitle.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            modeTitle.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            modeTitle.rectTransform.offsetMin = new Vector2(138f, 0f);
            modeTitle.rectTransform.offsetMax = Vector2.zero;

            statusLabel = Ui.MakeText("LobbyStatus", bar, "Ready", 17, SubtleText, TextAnchor.LowerLeft);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusLabel.rectTransform.anchorMax = new Vector2(0.58f, 0.50f);
            statusLabel.rectTransform.offsetMin = new Vector2(138f, 0f);
            statusLabel.rectTransform.offsetMax = Vector2.zero;

            codePill = Ui.Panel("RoomCodePill", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(476f, 50f));
            StyleCard(codePill, PanelBg);

            codeLabel = Ui.MakeText("RoomCodeText", codePill, "CODE: --------", 20, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Stretch(codeLabel.rectTransform, 14f, 0f, 206f, 0f);

            MakeButton("CopyCodeBtn", codePill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-102f, 0f), new Vector2(92f, 48f), "COPY", MutedBtn, Ui.Cream, 17, CopyRoomShare);
            MakeButton("QrModalBtn", codePill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(90f, 48f), "QR", GoldCta, InkDark, 18, OpenRoomQrModal);
        }

        void BuildConnectPanel(
            RectTransform parent,
            out InputField serverInput,
            out InputField codeInput,
            out Text emptyRoomsLabel)
        {
            // Left card: Server URL + Create / Code Join
            RectTransform left = Ui.Panel("SetupCard", parent, new Vector2(0f, 0.02f), new Vector2(0.49f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(left, PanelBg);

            Text urlCaption = Ui.MakeText("UrlCaption", left, "ONLINE SERVER URL", 20, GoldCta, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            urlCaption.rectTransform.anchorMin = new Vector2(0.05f, 0.86f);
            urlCaption.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            urlCaption.rectTransform.sizeDelta = Vector2.zero;

            serverInput = MakeInputField("ServerUrlInput", left, new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.85f), "wss://play.kalyeah.example or ws://127.0.0.1:8080", 19, 128);

            MakeButton("CreateOnlineRoomBtn", left, new Vector2(0.05f, 0.57f), new Vector2(0.58f, 0.69f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "CREATE 5v5 ROOM", GoldCta, InkDark, 21, () => ConnectAndCreateOnlineRoom());
            MakeButton("BrowseOpenBtn", left, new Vector2(0.61f, 0.57f), new Vector2(0.95f, 0.69f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "REFRESH LIST", MutedBtn, Ui.Cream, 18, () => ConnectForOnlineBrowse());

            Text codeCaption = Ui.MakeText("CodeCaption", left, "JOIN BY 8-CHAR ROOM CODE", 20, new Color(0.46f, 0.94f, 0.86f, 1f), TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            codeCaption.rectTransform.anchorMin = new Vector2(0.05f, 0.41f);
            codeCaption.rectTransform.anchorMax = new Vector2(0.95f, 0.51f);
            codeCaption.rectTransform.sizeDelta = Vector2.zero;

            codeInput = MakeInputField("RoomCodeInput", left, new Vector2(0.05f, 0.27f), new Vector2(0.64f, 0.40f), "ABCD2345", 24, 8);
            InputField capturedCode = codeInput;
            codeInput.onValueChanged.AddListener(raw =>
            {
                string cleaned = CleanRoomCode(raw);
                if (!string.Equals(raw, cleaned, StringComparison.Ordinal))
                    capturedCode.SetTextWithoutNotify(cleaned);
            });

            MakeButton("PasteCodeBtn", left, new Vector2(0.66f, 0.27f), new Vector2(0.95f, 0.40f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "PASTE", MutedBtn, Ui.Cream, 19, PasteCodeFromClipboard);

            MakeButton("JoinByCodeBtn", left, new Vector2(0.05f, 0.08f), new Vector2(0.64f, 0.21f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "JOIN WITH CODE", GreenReady, InkDark, 21, () => JoinByCode());
            MakeButton("ScanCodeQrBtn", left, new Vector2(0.66f, 0.08f), new Vector2(0.95f, 0.21f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "SCAN QR", MutedBtn, Ui.Cream, 19, RequestScanRoomCode);

            // Right card: Open Rooms browser
            RectTransform right = Ui.Panel("OpenRoomsCard", parent, new Vector2(0.51f, 0.02f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(right, PanelBg);

            Text listHeader = Ui.MakeText("OpenRoomsHeader", right, "OPEN ROOMS (TAP TO JOIN)", 22, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            listHeader.rectTransform.anchorMin = new Vector2(0.04f, 0.86f);
            listHeader.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
            listHeader.rectTransform.sizeDelta = Vector2.zero;

            RectTransform listArea = Ui.Panel("OpenRoomsListArea", right, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            emptyRoomsLabel = Ui.MakeText("EmptyOpenRooms", listArea, "Connect to a server to list open rooms, or create one on the left!", 19, SubtleText, TextAnchor.MiddleCenter);
            emptyRoomsLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(emptyRoomsLabel.rectTransform, 18f, 12f, 18f, 12f);

            float slice = 1f / MaxOpenRoomRows;
            for (int i = 0; i < MaxOpenRoomRows; i++)
            {
                int idx = i;
                float yMax = 1f - i * slice;
                float yMin = yMax - slice;
                RectTransform row = Ui.Panel("OpenRoom_" + i.ToString(CultureInfo.InvariantCulture), listArea, new Vector2(0f, yMin), new Vector2(1f, yMax), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, -8f));
                Image bg = Ui.MakeImage("Bg", row, new Color(0.15f, 0.24f, 0.44f, 1f), raycast: true);
                Stretch(bg.rectTransform);
                Button btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    string code = _openRoomRows[idx].Code;
                    if (!string.IsNullOrEmpty(code)) JoinByCode(code);
                });
                _touchTargets.Add(row);

                Text title = Ui.MakeText("Title", row, "", 20, Ui.Cream, TextAnchor.MiddleLeft);
                Stretch(title.rectTransform, 14f, 0f, 250f, 0f);
                Text meta = Ui.MakeText("Meta", row, "", 17, GoldCta, TextAnchor.MiddleRight);
                Stretch(meta.rectTransform, 240f, 0f, 14f, 0f);

                row.gameObject.SetActive(false);
                _openRoomRows[i] = new OpenRoomRow { Root = row, Label = title, Meta = meta, Code = "" };
            }
        }

        void BuildRoomPanel(
            RectTransform parent,
            out Text blueHeader,
            out Text redHeader,
            out RawImage myKidThumb,
            out Text myKidLabel,
            out Text mapLabel,
            out Text firstToLabel,
            out Button botsBtn,
            out Text botsLabel,
            out Button openBtn,
            out Text openLabel,
            out Image readyBg,
            out Text readyLabel,
            out Button startBtn,
            out Image startBg,
            out Text startLabel)
        {
            // Blue Squad Column (5 slots)
            RectTransform blueCol = Ui.Panel("BlueTeamColumn", parent, new Vector2(0f, 0.21f), new Vector2(0.492f, 0.99f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(blueCol, PanelBg);

            Button blueJoinBtn = MakeButton("JoinBlueHeaderBtn", blueCol, new Vector2(0.02f, 0.855f), new Vector2(0.98f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "BLUE SQUAD (0/5) — TAP TO JOIN", BlueHeader, Ui.Cream, 20, () => PickTeam(0));
            blueHeader = blueJoinBtn.transform.parent.Find("Label").GetComponent<Text>();

            RectTransform blueList = Ui.Panel("BlueSlots", blueCol, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildTeamSlotRows(blueList, _blueSlots, 0);

            // Red Squad Column (5 slots)
            RectTransform redCol = Ui.Panel("RedTeamColumn", parent, new Vector2(0.508f, 0.21f), new Vector2(1f, 0.99f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(redCol, PanelBg);

            Button redJoinBtn = MakeButton("JoinRedHeaderBtn", redCol, new Vector2(0.02f, 0.855f), new Vector2(0.98f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "RED SQUAD (0/5) — TAP TO JOIN", RedHeader, Ui.Cream, 20, () => PickTeam(1));
            redHeader = redJoinBtn.transform.parent.Find("Label").GetComponent<Text>();

            RectTransform redList = Ui.Panel("RedSlots", redCol, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildTeamSlotRows(redList, _redSlots, 1);

            // Bottom Controls Bar (2 rows inside bottom 19% strip so every button >= 48 dp with zero overlap)
            RectTransform bottomBar = Ui.Panel("BottomControls", parent, new Vector2(0f, 0f), new Vector2(1f, 0.195f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(bottomBar, PanelBg);

            // Kid picker shortcut cluster on left
            RectTransform kidChip = Ui.Panel("KidPickerCluster", bottomBar, new Vector2(0.01f, 0.10f), new Vector2(0.27f, 0.90f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(kidChip, new Color(0.07f, 0.11f, 0.21f, 1f));

            var thumbGo = new GameObject("MyKidThumb", typeof(RectTransform), typeof(RawImage));
            thumbGo.transform.SetParent(kidChip, false);
            myKidThumb = thumbGo.GetComponent<RawImage>();
            myKidThumb.raycastTarget = false;
            myKidThumb.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            myKidThumb.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            myKidThumb.rectTransform.pivot = new Vector2(0f, 0.5f);
            myKidThumb.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            myKidThumb.rectTransform.sizeDelta = new Vector2(54f, 54f);

            myKidLabel = Ui.MakeText("MyKidName", kidChip, "JUNJUN", 18, Ui.Cream, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
            myKidLabel.rectTransform.anchorMin = new Vector2(0f, 0.50f);
            myKidLabel.rectTransform.anchorMax = new Vector2(1f, 0.95f);
            myKidLabel.rectTransform.offsetMin = new Vector2(68f, 0f);
            myKidLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            MakeButton("PrevKidBtn", kidChip, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(66f, 6f), new Vector2(48f, 48f), "<", MutedBtn, Ui.Cream, 18, () => CycleKid(-1));
            MakeButton("PickScreenBtn", kidChip, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(118f, 6f), new Vector2(118f, 48f), "PICK KID", GoldCta, InkDark, 17, InvokePickKidShortcut);
            MakeButton("NextKidBtn", kidChip, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(240f, 6f), new Vector2(48f, 48f), ">", MutedBtn, Ui.Cream, 18, () => CycleKid(1));

            // Middle: Team Swap + Host settings (Game, FirstTo, Bot-Fill, Open/Private)
            MakeButton("SwapTeamBtn", bottomBar, new Vector2(0.28f, 0.52f), new Vector2(0.43f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "SWAP TEAM", MutedBtn, Ui.Cream, 17, SwapTeam);

            Button mapBtn = MakeButton("MapCycleBtn", bottomBar, new Vector2(0.44f, 0.52f), new Vector2(0.68f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "GAME: AGAWANG BASE", MutedBtn, Ui.Cream, 16, CycleMap);
            mapLabel = mapBtn.transform.parent.Find("Label").GetComponent<Text>();

            Button ftBtn = MakeButton("FirstToBtn", bottomBar, new Vector2(0.28f, 0.06f), new Vector2(0.43f, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "FIRST TO 3", MutedBtn, Ui.Cream, 16, CycleFirstTo);
            firstToLabel = ftBtn.transform.parent.Find("Label").GetComponent<Text>();

            botsBtn = MakeButton("BotFillBtn", bottomBar, new Vector2(0.44f, 0.06f), new Vector2(0.56f, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "BOT-FILL: OFF", MutedBtn, Ui.Cream, 15, ToggleBotFill);
            botsLabel = botsBtn.transform.parent.Find("Label").GetComponent<Text>();

            openBtn = MakeButton("OpenPrivacyBtn", bottomBar, new Vector2(0.565f, 0.06f), new Vector2(0.68f, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "PUBLIC ROOM", MutedBtn, Ui.Cream, 15, ToggleOpenPrivacy);
            openLabel = openBtn.transform.parent.Find("Label").GetComponent<Text>();

            // Right: Ready toggle + Host Start Match button
            Button readyBtn = MakeButton("ReadyToggleBtn", bottomBar, new Vector2(0.69f, 0.12f), new Vector2(0.835f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "TAP READY", MutedBtn, Ui.Cream, 19, PressReady);
            readyBg = (Image)readyBtn.targetGraphic;
            readyLabel = readyBtn.transform.parent.Find("Label").GetComponent<Text>();

            startBtn = MakeButton("StartMatchBtn", bottomBar, new Vector2(0.845f, 0.12f), new Vector2(0.99f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "START MATCH!", GoldCta, InkDark, 20, PressStart);
            startBg = (Image)startBtn.targetGraphic;
            startLabel = startBtn.transform.parent.Find("Label").GetComponent<Text>();
        }

        void BuildTeamSlotRows(RectTransform container, SlotWidgets[] targetArray, int teamIndex)
        {
            float slice = 1f / SlotsPerTeam;
            for (int i = 0; i < SlotsPerTeam; i++)
            {
                float yMax = 1f - i * slice;
                float yMin = yMax - slice;
                RectTransform row = Ui.Panel(
                    string.Format(CultureInfo.InvariantCulture, "Team{0}_Slot{1}", teamIndex, i),
                    container,
                    new Vector2(0f, yMin),
                    new Vector2(1f, yMax),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(0f, -5f));

                Image bg = Ui.MakeImage("SlotBg", row, EmptySlotFill, raycast: true);
                Stretch(bg.rectTransform);
                int capturedTeam = teamIndex;
                Button rowBtn = bg.gameObject.AddComponent<Button>();
                rowBtn.targetGraphic = bg;
                rowBtn.onClick.AddListener(() => PickTeam(capturedTeam));

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
                portraitGo.transform.SetParent(row, false);
                var portrait = portraitGo.GetComponent<RawImage>();
                portrait.raycastTarget = false;
                portrait.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                portrait.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                portrait.rectTransform.pivot = new Vector2(0f, 0.5f);
                portrait.rectTransform.anchoredPosition = new Vector2(8f, 0f);
                portrait.rectTransform.sizeDelta = new Vector2(46f, 46f);

                Text nameText = Ui.MakeText("PlayerName", row, "Open Slot", 19, Ui.Cream, TextAnchor.UpperLeft);
                nameText.rectTransform.anchorMin = new Vector2(0f, 0.46f);
                nameText.rectTransform.anchorMax = new Vector2(1f, 0.96f);
                nameText.rectTransform.offsetMin = new Vector2(62f, 0f);
                nameText.rectTransform.offsetMax = new Vector2(-118f, 0f);

                Text subText = Ui.MakeText("SubInfo", row, "", 15, SubtleText, TextAnchor.LowerLeft);
                subText.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                subText.rectTransform.anchorMax = new Vector2(1f, 0.50f);
                subText.rectTransform.offsetMin = new Vector2(62f, 0f);
                subText.rectTransform.offsetMax = new Vector2(-118f, 0f);

                RectTransform badge = Ui.Panel("ReadyBadge", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(102f, 34f));
                Image badgeBg = Ui.MakeImage("Bg", badge, MutedBtn, raycast: false);
                Stretch(badgeBg.rectTransform);
                Text badgeText = Ui.MakeText("Text", badge, "EMPTY", 15, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                Stretch(badgeText.rectTransform, 4f, 2f, 4f, 2f);

                targetArray[i] = new SlotWidgets
                {
                    Root = row,
                    Bg = bg,
                    Portrait = portrait,
                    NameLabel = nameText,
                    SubLabel = subText,
                    BadgeBg = badgeBg,
                    BadgeLabel = badgeText,
                };
            }
        }

        RectTransform BuildQrModal(RectTransform parent, out Image qrImg, out Text qrText)
        {
            RectTransform overlay = Ui.Fill("RoomQrModal", parent);
            Image dim = Ui.MakeImage("Dim", overlay, new Color(0.02f, 0.04f, 0.10f, 0.88f), raycast: true);
            Stretch(dim.rectTransform);
            Button closeBg = dim.gameObject.AddComponent<Button>();
            closeBg.targetGraphic = dim;
            closeBg.onClick.AddListener(() => overlay.gameObject.SetActive(false));

            RectTransform card = Ui.Panel("QrCard", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 480f));
            StyleCard(card, PanelBg);

            Text title = Ui.MakeText("Title", card, "SCAN TO JOIN ROOM", 26, GoldCta, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = new Vector2(0.06f, 0.86f);
            title.rectTransform.anchorMax = new Vector2(0.94f, 0.97f);
            title.rectTransform.sizeDelta = Vector2.zero;

            RectTransform plate = Ui.Panel("WhitePlate", card, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 280f));
            Image white = Ui.MakeImage("Bg", plate, Color.white);
            Stretch(white.rectTransform);

            qrImg = Ui.MakeImage("QrCodeImage", plate, Color.white);
            qrImg.preserveAspect = true;
            Stretch(qrImg.rectTransform, 12f, 12f, 12f, 12f);

            qrText = Ui.MakeText("Caption", card, "", 18, Ui.Cream, TextAnchor.MiddleCenter);
            qrText.horizontalOverflow = HorizontalWrapMode.Wrap;
            qrText.rectTransform.anchorMin = new Vector2(0.06f, 0.14f);
            qrText.rectTransform.anchorMax = new Vector2(0.94f, 0.24f);
            qrText.rectTransform.sizeDelta = Vector2.zero;

            MakeButton("CloseQrBtn", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(180f, 50f), "CLOSE", MutedBtn, Ui.Cream, 20, () => overlay.gameObject.SetActive(false));
            return overlay;
        }

        void ShowRoomView(bool inRoom)
        {
            _connectPanel.gameObject.SetActive(!inRoom);
            _roomPanel.gameObject.SetActive(inRoom);
            _roomCodePill.gameObject.SetActive(inRoom);
        }

        void RefreshMyKidChip()
        {
            string kid = AppSession.Current.SelectedCharacter;
            if (_myKidLabel != null)
                _myKidLabel.text = PrettyKidName(kid).ToUpperInvariant();
            if (_myKidThumb != null)
            {
                Texture2D tex = LoadPortrait(kid);
                _myKidThumb.texture = tex;
                _myKidThumb.color = tex != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            }
        }

        void InvokePickKidShortcut()
        {
            if (OnPickKid != null)
                OnPickKid.Invoke();
            else
                CycleKid(1);
        }

        void CopyRoomShare()
        {
            string payload = CurrentSharePayload();
            if (string.IsNullOrEmpty(payload)) return;
            GUIUtility.systemCopyBuffer = payload;
            SetStatus("Copied \"" + payload + "\" to clipboard!", GreenReady);
        }

        void OpenRoomQrModal()
        {
            string payload = CurrentSharePayload();
            if (string.IsNullOrEmpty(payload)) return;
            ReleaseOwnedQrSprite();
            Sprite customQr = QrSpriteProvider?.Invoke(payload);
            if (customQr != null && !ReferenceEquals(customQr, _fallbackQrSprite))
                _ownedQrSprite = customQr;
            _qrModalImage.sprite = customQr != null ? customQr : FallbackQrSprite();
            _qrModalCaption.text = payload;
            _qrOverlay.gameObject.SetActive(true);
        }

        string CurrentSharePayload()
        {
            bool isHotspot = AppSession.Current.Mode == GameMode.HotspotHost || AppSession.Current.Mode == GameMode.HotspotClient;
            if (isHotspot)
            {
                return AppSession.Current.Mode == GameMode.HotspotHost
                    ? "ws://" + ResolveShareLanIp() + ":" + _netRules.DefaultPort.ToString(CultureInfo.InvariantCulture)
                    : AppSession.Current.HostEndpoint;
            }
            return RoomCodeText;
        }

        void PasteCodeFromClipboard()
        {
            string raw = GUIUtility.systemCopyBuffer ?? "";
            if (raw.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                if (_serverUrlInput != null)
                    _serverUrlInput.text = raw.Trim();
                SetStatus("Server URL pasted!", SubtleText);
                return;
            }

            string clean = CleanRoomCode(raw);
            if (string.IsNullOrEmpty(clean))
            {
                SetStatus("Clipboard has no valid room code characters.", WarnText);
                return;
            }
            if (_joinCodeInput != null)
                _joinCodeInput.text = clean;
            SetStatus(
                clean.Length == _netRules.PasscodeLength
                    ? "Room code pasted!"
                    : string.Format(CultureInfo.InvariantCulture, "Pasted {0}/{1} code characters.", clean.Length, _netRules.PasscodeLength),
                SubtleText);
        }

        void RequestScanRoomCode()
        {
            if (QrScanRequester != null)
            {
                QrScanRequester.Invoke(scanned =>
                {
                    if (string.IsNullOrWhiteSpace(scanned)) return;
                    string clean = CleanRoomCode(scanned);
                    if (clean.Length == _netRules.PasscodeLength)
                        JoinByCode(clean);
                });
                return;
            }
            PasteCodeFromClipboard();
        }

        string ResolveInputServerUrl(string overrideUrl)
        {
            string candidate = !string.IsNullOrWhiteSpace(overrideUrl)
                ? overrideUrl
                : (_serverUrlInput != null && !string.IsNullOrWhiteSpace(_serverUrlInput.text)
                    ? _serverUrlInput.text
                    : AppSession.Current.ServerUrl);
            string normalized = SettingsStore.NormalizeServerUrl(candidate);
            if (!string.IsNullOrEmpty(normalized))
            {
                AppSession.Current.ServerUrl = normalized;
                if (_serverUrlInput != null)
                    _serverUrlInput.text = normalized;
            }
            return normalized;
        }

        string CleanRoomCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string alphabet = string.IsNullOrEmpty(_netRules.PasscodeAlphabet)
                ? "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
                : _netRules.PasscodeAlphabet;
            int maxLen = _netRules.PasscodeLength > 0 ? _netRules.PasscodeLength : 8;
            var sb = new StringBuilder(maxLen);
            for (int i = 0; i < raw.Length && sb.Length < maxLen; i++)
            {
                char upper = char.ToUpperInvariant(raw[i]);
                if (alphabet.IndexOf(upper) >= 0)
                    sb.Append(upper);
            }
            return sb.ToString();
        }

        string ResolveShareLanIp()
        {
            string custom = LanIpProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(custom))
            {
                string trimmed = custom.Trim();
                if (!string.Equals(trimmed, "127.0.0.1", StringComparison.Ordinal))
                    return trimmed;
            }

            string lanIp = AndroidLocalLink.GetLanIpv4(_netRules);
            if (!string.IsNullOrEmpty(lanIp) && !string.Equals(lanIp, "127.0.0.1", StringComparison.Ordinal))
                return lanIp;

            return _netRules.FallbackHosts.Count > 0 ? _netRules.FallbackHosts[0] : "192.168.43.1";
        }

        void ReleaseOwnedQrSprite()
        {
            if (_ownedQrSprite == null) return;
            Sprite sprite = _ownedQrSprite;
            _ownedQrSprite = null;
            if (ReferenceEquals(sprite, _fallbackQrSprite)) return;
            if (_qrModalImage != null && ReferenceEquals(_qrModalImage.sprite, sprite))
                _qrModalImage.sprite = null;
            Texture2D tex = sprite.texture;
            DestroyOwnedObject(sprite);
            if (tex != null && (_fallbackQrSprite == null || !ReferenceEquals(tex, _fallbackQrSprite.texture)))
                DestroyOwnedObject(tex);
        }

        static void DestroyOwnedObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        Texture2D LoadPortrait(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (_portraitCache.TryGetValue(characterId, out Texture2D cached))
                return cached;
            Texture2D loaded = KalyeahResources.Texture("portraits/" + characterId);
            _portraitCache[characterId] = loaded;
            return loaded;
        }

        void SetStatus(string text, Color color)
        {
            StatusText = text ?? "";
            if (_statusLabel != null)
            {
                _statusLabel.text = StatusText;
                _statusLabel.color = color;
            }
        }

        void ApplyResponsiveMetrics()
        {
            if (_frame != null)
            {
                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float w = Mathf.Clamp(safe.x - 20f, 700f, 1188f);
                float h = Mathf.Clamp(safe.y - 16f, 540f, 684f);
                _frame.sizeDelta = new Vector2(w, h);
            }

            for (int i = 0; i < _touchTargets.Count; i++)
            {
                RectTransform rt = _touchTargets[i];
                if (rt == null) continue;
                if (rt.anchorMin.x == rt.anchorMax.x && rt.anchorMin.y == rt.anchorMax.y)
                    rt.sizeDelta = ScreenFitMetrics.ClampTouchSize(rt.sizeDelta, ScreenFitMetrics.MinTouchDp, _canvas);
            }
        }

        Button MakeButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            string text,
            Color fill,
            Color textColor,
            int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            Vector2 effectiveSize = (anchorMin.x == anchorMax.x && anchorMin.y == anchorMax.y)
                ? ScreenFitMetrics.ClampTouchSize(size, ScreenFitMetrics.MinTouchDp, _canvas)
                : size;
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, effectiveSize);
            Image bg = Ui.MakeImage("Bg", rt, fill, raycast: true);
            Stretch(bg.rectTransform);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.05f, 0.12f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = Ui.MakeText("Label", rt, text, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            if (textColor == InkDark)
                label.GetComponent<Outline>().enabled = false;
            Stretch(label.rectTransform, 6f, 2f, 6f, 2f);

            Button btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            _touchTargets.Add(rt);
            return btn;
        }

        static InputField MakeInputField(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string placeholderText, int fontSize, int charLimit)
        {
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image bg = Ui.MakeImage("InputBg", rt, new Color(0.04f, 0.07f, 0.14f, 1f), raycast: true);
            Stretch(bg.rectTransform);

            Text textComp = Ui.MakeText("Text", rt, "", fontSize, Ui.Cream, TextAnchor.MiddleLeft);
            textComp.supportRichText = false;
            Stretch(textComp.rectTransform, 12f, 4f, 12f, 4f);

            Text placeholder = Ui.MakeText("Placeholder", rt, placeholderText, fontSize - 2, new Color(0.55f, 0.64f, 0.78f, 0.72f), TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 12f, 4f, 12f, 4f);

            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = textComp;
            field.placeholder = placeholder;
            field.characterLimit = charLimit;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        static bool IsLocalMemberReady(GDict room, int myId)
        {
            foreach (object raw in room.GetArr("members"))
                if (raw is GDict m && m.GetInt("player_id", -1) == myId)
                    return m.GetBool("ready", false);
            return false;
        }

        static string MapTitle(string mapId)
        {
            switch (mapId)
            {
                case "patintero": return "Patintero";
                case "tumbang_preso": return "Tumbang Preso";
                case "langit_lupa": return "Langit Lupa";
                default: return RoomService.BaseTitle;
            }
        }

        static string PrettyKidName(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Junjun";
            return char.ToUpperInvariant(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        static string[] ExtractDeckWeaponIds(SimData simData)
        {
            if (simData?.GameRules?.Weapons == null || simData.GameRules.Weapons.Count == 0)
                return AppSession.DefaultSuperpowers;
            var ids = new List<string>(simData.GameRules.Weapons.Count);
            for (int i = 0; i < simData.GameRules.Weapons.Count; i++)
            {
                WeaponDef w = simData.GameRules.Weapons[i];
                if (w != null && !string.IsNullOrEmpty(w.Id))
                    ids.Add(w.Id);
            }
            return ids.Count > 0 ? ids.ToArray() : AppSession.DefaultSuperpowers;
        }

        static void StyleCard(RectTransform card, Color bg)
        {
            Image fill = Ui.MakeImage("CardBg", card, bg, raycast: false);
            Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();
            var border = fill.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.02f, 0.04f, 0.10f, 0.95f);
            border.effectDistance = new Vector2(3f, -3f);
        }

        static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void SaveSessionPrefs()
        {
            PlayerPrefs.SetString(AppSession.PlayerPrefsKey, AppSession.Current.ToJson());
            PlayerPrefs.Save();
        }

        static Sprite FallbackQrSprite()
        {
            if (_fallbackQrSprite != null) return _fallbackQrSprite;
            const int n = 25;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "KalyeahLobbyQrStandIn",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[n * n];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(16, 22, 38, 255);
            for (int i = 0; i < px.Length; i++) px[i] = white;
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    bool on = i == 0 || i == 6 || j == 0 || j == 6 || (i >= 2 && i <= 4 && j >= 2 && j <= 4);
                    if (!on) continue;
                    px[(1 + j) * n + (1 + i)] = black;
                    px[(1 + j) * n + (n - 8 + i)] = black;
                    px[(n - 8 + j) * n + (1 + i)] = black;
                }
            }
            for (int y = 9; y < n - 9; y++)
                for (int x = 9; x < n - 9; x++)
                    if (((x * 11 + y * 5) & 3) == 0) px[y * n + x] = black;
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _fallbackQrSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            return _fallbackQrSprite;
        }

        sealed class LobbyViewDriver : MonoBehaviour
        {
            LobbyScreenView _owner;

            public void Bind(LobbyScreenView owner) => _owner = owner;

            void Update() => _owner?.Tick(Time.unscaledDeltaTime);

            void OnRectTransformDimensionsChange() => _owner?.ApplyResponsiveMetrics();

            void OnDestroy() => _owner?.Dispose();
        }
    }
}
