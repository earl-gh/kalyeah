using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Kalyeah.Net;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
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

        static readonly Color FrameBg = new Color(0.055f, 0.075f, 0.102f, 0.96f);
        static readonly Color PanelBg = new Color(0.075f, 0.102f, 0.138f, 0.94f);
        static readonly Color SurfaceDark = new Color(0.045f, 0.062f, 0.088f, 0.94f);
        static readonly Color PaperWhite = MenuArt.PaperWhite;
        static readonly Color SignalVermilion = new Color(1.00f, 0.275f, 0.333f, 1f);
        static readonly Color SignalVermilionDeep = new Color(0.83f, 0.19f, 0.25f, 1f);
        static readonly Color TitaniumBorder = new Color(0.835f, 0.875f, 0.914f, 0.28f);
        static readonly Color BlueHeader = MenuArt.AllyBlue;
        static readonly Color RedHeader = MenuArt.EnemyRed;
        static readonly Color BlueSlotFill = new Color(0.068f, 0.140f, 0.205f, 0.94f);
        static readonly Color RedSlotFill = new Color(0.195f, 0.078f, 0.098f, 0.94f);
        static readonly Color EmptySlotFill = new Color(0.055f, 0.075f, 0.102f, 0.86f);
        static readonly Color GoldCta = MenuArt.CoralGold;
        static readonly Color GreenReady = MenuArt.SuccessGreen;
        static readonly Color MutedBtn = new Color(0.115f, 0.145f, 0.190f, 0.96f);
        static readonly Color DisabledPlate = new Color(0.082f, 0.104f, 0.138f, 0.92f);
        static readonly Color GuestRulePlate = new Color(0.068f, 0.090f, 0.120f, 0.94f);
        static readonly Color InkDark = MenuArt.Ink;
        static readonly Color SubtleText = MenuArt.MutedText;
        static readonly Color WarnText = MenuArt.CoralGold;
        static readonly Color ErrorText = new Color(1.00f, 0.275f, 0.333f, 1f);

        static Sprite _fallbackQrSprite;

        readonly DataSource _data;
        Canvas _canvas;
        readonly NetRules _netRules;
        readonly string[] _allWeaponIds;

        readonly RectTransform _safeRoot;
        readonly RectTransform _frame;
        readonly RectTransform _connectPanel;
        readonly RectTransform _roomPanel;
        readonly RectTransform _qrOverlay;
        readonly Image _qrModalImage;
        readonly Text _qrModalCaption;

        // Top bar
        RectTransform _topBar;
        RectTransform _backBtnRect;
        RectTransform _titleBlockRect;
        RectTransform _roomRightCluster;
        RectTransform _copyCodeBtnRect;
        RectTransform _qrModalBtnRect;
        readonly Text _modeTitleLabel;
        readonly Text _statusLabel;
        readonly RectTransform _roomCodePill;
        readonly Text _roomCodeLabel;

        // Online Connect / Code Join panel
        RectTransform _setupCardRect;
        RectTransform _urlCaptionRect;
        RectTransform _serverInputRect;
        RectTransform _createRoomBtnRect;
        RectTransform _browseOpenBtnRect;
        RectTransform _codeCaptionRect;
        RectTransform _codeInputRect;
        RectTransform _pasteCodeBtnRect;
        RectTransform _joinByCodeBtnRect;
        RectTransform _scanCodeQrBtnRect;
        RectTransform _openRoomsCardRect;
        RectTransform _openRoomsHeaderRect;
        RectTransform _openRoomsScrollRect;
        readonly LayoutElement[] _openRoomLayouts = new LayoutElement[MaxOpenRoomRows];
        readonly InputField _serverUrlInput;
        readonly InputField _joinCodeInput;
        readonly Text _openRoomsEmptyLabel;
        readonly OpenRoomRow[] _openRoomRows = new OpenRoomRow[MaxOpenRoomRows];

        // 10-slot 5v5 Blue vs Red grid
        RectTransform _blueColRect;
        RectTransform _blueJoinBtnRect;
        RectTransform _blueSlotsRect;
        RectTransform _redColRect;
        RectTransform _redJoinBtnRect;
        RectTransform _redSlotsRect;
        readonly Text _blueHeaderLabel;
        readonly Text _redHeaderLabel;
        readonly SlotWidgets[] _blueSlots = new SlotWidgets[SlotsPerTeam];
        readonly SlotWidgets[] _redSlots = new SlotWidgets[SlotsPerTeam];

        // Bottom controls
        RectTransform _bottomBarRect;
        RectTransform _pickKidBtnRect;
        RectTransform _myKidThumbFrame;
        RectTransform _myKidTextBlock;
        RectTransform _swapTeamBtnRect;
        Button _mapButton;
        Image _mapBtnBg;
        RectTransform _mapBtnRect;
        Button _firstToButton;
        Image _firstToBtnBg;
        RectTransform _firstToBtnRect;
        Image _botsBtnBg;
        RectTransform _botsBtnRect;
        Image _openBtnBg;
        RectTransform _openBtnRect;
        RectTransform _readyBtnRect;
        RectTransform _startBtnRect;

        // QR overlay
        RectTransform _qrCardRect;
        RectTransform _qrCardTitleRect;
        RectTransform _qrCardPlateRect;
        RectTransform _qrCardCaptionRect;
        RectTransform _qrCloseBtnRect;

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
        Material _startCtaMat;

        GameClient _boundClient;
        Sprite _ownedQrSprite;
        Action _pendingAfterWelcome;
        bool _transportWelcomed;
        bool _announcedCharacter;
        bool _appliedHomeFirstTo;
        bool _matchStartFired;
        bool _offlineFallbackArmed;

        /// <summary>
        /// Raised once when PLAY's online attempt (online mode, create intent) can't reach the server, so the home screen can
        /// offer to play offline instead of leaving the player on a failed connection.
        /// </summary>
        public event Action ConnectionUnavailable;
        bool _viewportSubscribed;
        bool _inLayout;
        float _listRefreshLeft;
        string _lastAnnouncedChar = "";

        sealed class RoomQrScrimDismiss : MonoBehaviour, IPointerClickHandler
        {
            [NonSerialized] public Action OnDismiss;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData != null &&
                    eventData.pointerCurrentRaycast.gameObject != null &&
                    eventData.pointerCurrentRaycast.gameObject != gameObject)
                {
                    return;
                }
                OnDismiss?.Invoke();
            }
        }

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
            Image backdrop = Ui.MakeImage("Backdrop", Root, new Color(0.035f, 0.050f, 0.072f, 0.96f), raycast: true);
            Stretch(backdrop.rectTransform);

            _safeRoot = Ui.Fill("SafeRoot", Root);
            _safeRoot.gameObject.AddComponent<SafeArea>();

            _frame = Ui.Panel(
                "LobbyFrame",
                _safeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1240f, 668f));
            StyleCard(_frame, FrameBg);

            BuildTopBar(_frame, out _modeTitleLabel, out _statusLabel, out _roomCodePill, out _roomCodeLabel);

            _connectPanel = Ui.Panel("OnlineConnectPanel", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildConnectPanel(_connectPanel, out _serverUrlInput, out _joinCodeInput, out _openRoomsEmptyLabel);

            _roomPanel = Ui.Panel("RoomPanel", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
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

            _qrOverlay = BuildQrModal(Root, out _qrModalImage, out _qrModalCaption);
            _qrOverlay.gameObject.SetActive(false);

            var driver = Root.gameObject.AddComponent<LobbyViewDriver>();
            driver.Bind(this);

            SubscribeViewport(true);
            ApplyResponsiveMetrics();
            Open();
        }

        public void Show()
        {
            SubscribeViewport(true);
            Root.gameObject.SetActive(true);
            OnShow();
        }

        public void Hide() => Root.gameObject.SetActive(false);

        public void Open() => OnShow();

        public void OnShow()
        {
            _matchStartFired = false;
            _offlineFallbackArmed = false;
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
                _offlineFallbackArmed = true;
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
            _appliedHomeFirstTo = false;
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
                FallBackOffline();
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
            _appliedHomeFirstTo = false;
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
            SubscribeViewport(false);
            BindClient(null);
            ReleaseOwnedQrSprite();
            if (_startCtaMat != null && _startCtaMat != Graphic.defaultGraphicMaterial)
                DestroyOwnedObject(_startCtaMat);
            _startCtaMat = null;
        }

        void SubscribeViewport(bool subscribe)
        {
            if (subscribe == _viewportSubscribed) return;
            _viewportSubscribed = subscribe;
            if (subscribe)
                ScreenFitMetrics.SimulatedViewportChanged += OnSimulatedViewportChanged;
            else
                ScreenFitMetrics.SimulatedViewportChanged -= OnSimulatedViewportChanged;
        }

        void OnSimulatedViewportChanged() => ApplyResponsiveMetrics();

        bool IsBrowsingOpenRooms() =>
            AppSession.Current.Mode == GameMode.Online &&
            _connectPanel != null &&
            _connectPanel.gameObject.activeSelf &&
            (_boundClient == null || _boundClient.Room.IsEmpty);

        void QueueOrRunRoomAction(Action action, string connectingMessage)
        {
            _announcedCharacter = false;
            _appliedHomeFirstTo = false;
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
            FallBackOffline();
        }

        void FallBackOffline()
        {
            if (!_offlineFallbackArmed) return;
            _offlineFallbackArmed = false;
            ConnectionUnavailable?.Invoke();
        }

        void OnClientWelcomed(bool reconnected)
        {
            _transportWelcomed = true;
            _offlineFallbackArmed = false;
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
            // the host's room starts on the mode picked on Home (Blitz first to 3, Quick first to 5)
            if (!_appliedHomeFirstTo && _boundClient != null && _boundClient.IsHost())
            {
                _appliedHomeFirstTo = true;
                int wanted = session.FirstTo;
                if ((wanted == 3 || wanted == 5 || wanted == 7) && room.GetInt("first_to", wanted) != wanted)
                    _boundClient.SetFirstTo(wanted);
            }
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

            Color blueAccent = SettingsStore.Current.ColorblindMode == 3 ? new Color(0.11f, 0.42f, 0.56f, 0.94f) : BlueSlotFill;
            Color redAccent = SettingsStore.Current.ColorblindMode > 0 ? new Color(0.32f, 0.16f, 0.09f, 0.94f) : RedSlotFill;

            PopulateTeamSlots(_blueSlots, blueMembers, myId, hostId, fillBots, blueAccent, BlueHeader, "Blue");
            PopulateTeamSlots(_redSlots, redMembers, myId, hostId, fillBots, redAccent, RedHeader, "Red");

            Color ruleFill = amHost ? MutedBtn : GuestRulePlate;
            Color ruleText = amHost ? PaperWhite : SubtleText;
            Color ruleTick = amHost ? TitaniumBorder : Color.clear;

            if (_mapButton != null) _mapButton.interactable = amHost;
            ApplyGlassSurface(_mapBtnBg, ruleFill, TitaniumBorder, ruleTick, 4f, amHost ? 2f : 0f);
            _mapBtnLabel.color = ruleText;
            SyncOutlineForColor(_mapBtnLabel, ruleText);
            _mapBtnLabel.text = "GAME:\n" + MapTitle(mapId).ToUpperInvariant();

            if (_firstToButton != null) _firstToButton.interactable = amHost;
            ApplyGlassSurface(_firstToBtnBg, ruleFill, TitaniumBorder, ruleTick, 4f, amHost ? 2f : 0f);
            _firstToBtnLabel.color = ruleText;
            SyncOutlineForColor(_firstToBtnLabel, ruleText);
            _firstToBtnLabel.text = string.Format(CultureInfo.InvariantCulture, "FIRST\nTO {0}", firstTo);

            _botsButton.gameObject.SetActive(botsAllowed || isHotspot);
            bool canBots = amHost && botsAllowed;
            _botsButton.interactable = canBots;
            ApplyGlassSurface(_botsBtnBg, canBots ? MutedBtn : GuestRulePlate, TitaniumBorder, canBots ? TitaniumBorder : Color.clear, 4f, canBots ? 2f : 0f);
            Color botsText = canBots ? PaperWhite : SubtleText;
            _botsBtnLabel.color = botsText;
            SyncOutlineForColor(_botsBtnLabel, botsText);
            _botsBtnLabel.text = fillBots ? "BOTS:\nON" : "BOTS:\nOFF";

            _openButton.gameObject.SetActive(!isHotspot);
            bool canOpen = amHost && !isHotspot;
            _openButton.interactable = canOpen;
            ApplyGlassSurface(_openBtnBg, canOpen ? MutedBtn : GuestRulePlate, TitaniumBorder, canOpen ? TitaniumBorder : Color.clear, 4f, canOpen ? 2f : 0f);
            Color openText = canOpen ? PaperWhite : SubtleText;
            _openBtnLabel.color = openText;
            SyncOutlineForColor(_openBtnLabel, openText);
            _openBtnLabel.text = openRoom ? "PUBLIC\nROOM" : "CODE\nONLY";

            ApplyGlassSurface(_readyBtnBg, myReady ? GreenReady : PaperWhite, TitaniumBorder, myReady ? PaperWhite : SignalVermilion, 4f, 3f);
            _readyBtnLabel.color = InkDark;
            SyncOutlineForColor(_readyBtnLabel, InkDark);
            _readyBtnLabel.text = myReady ? "READY!\nUNREADY" : "TAP\nREADY";

            _startButton.gameObject.SetActive(true);
            _startButton.interactable = amHost && canStartHost;
            if (amHost && canStartHost)
            {
                ApplyCachedStartCta(_startBtnBg);
                _startBtnLabel.color = PaperWhite;
                _startBtnLabel.text = "START\nMATCH!";
            }
            else if (amHost)
            {
                ApplyGlassSurface(_startBtnBg, DisabledPlate, TitaniumBorder, Color.clear, 4f, 0f);
                _startBtnLabel.color = SubtleText;
                _startBtnLabel.text = "START\n(WAIT)";
            }
            else
            {
                ApplyGlassSurface(_startBtnBg, DisabledPlate, TitaniumBorder, Color.clear, 4f, 0f);
                _startBtnLabel.color = SubtleText;
                _startBtnLabel.text = "HOST\nSTARTS";
            }
            SyncOutlineForColor(_startBtnLabel, _startBtnLabel.color);

            ApplyResponsiveMetrics();

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
            Color teamAccent,
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
                    string charId = m.GetStr("char", "narda");
                    string rankTier = m.GetStr("rank_tier", "Kahoy");
                    bool ready = m.GetBool("ready", false);
                    bool connected = m.GetBool("connected", true);
                    bool afk = m.GetBool("afk", false);

                    var tags = new StringBuilder(pName);
                    if (pid == myId) tags.Append(" (YOU)");
                    if (pid == hostId) tags.Append(" [HOST]");

                    ApplyGlassSurface(slot.Bg, teamFill, TitaniumBorder, pid == myId ? PaperWhite : teamAccent, 3f, 3f);
                    slot.NameLabel.text = tags.ToString();
                    slot.NameLabel.color = PaperWhite;
                    SyncOutlineForColor(slot.NameLabel, PaperWhite);
                    slot.SubLabel.text = PrettyKidName(charId) + "  •  " + rankTier;

                    Texture2D tex = LoadPortrait(charId);
                    ApplyBustPortrait(slot.Portrait, tex, Color.white, new Color(1f, 1f, 1f, 0.14f));

                    if (!connected)
                    {
                        ApplyGlassSurface(slot.BadgeBg, ErrorText, TitaniumBorder, Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = PaperWhite;
                        slot.BadgeLabel.text = "DROPPED";
                    }
                    else if (afk)
                    {
                        ApplyGlassSurface(slot.BadgeBg, WarnText, TitaniumBorder, Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = InkDark;
                        slot.BadgeLabel.text = "AFK";
                    }
                    else if (ready || pid == hostId)
                    {
                        ApplyGlassSurface(slot.BadgeBg, GreenReady, TitaniumBorder, Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = InkDark;
                        slot.BadgeLabel.text = pid == hostId && !ready ? "HOST" : "READY";
                    }
                    else
                    {
                        ApplyGlassSurface(slot.BadgeBg, MutedBtn, TitaniumBorder, Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = PaperWhite;
                        slot.BadgeLabel.text = "WAITING";
                    }
                    SyncOutlineForColor(slot.BadgeLabel, slot.BadgeLabel.color);
                }
                else
                {
                    ApplyGlassSurface(slot.Bg, EmptySlotFill, new Color(0.835f, 0.875f, 0.914f, 0.14f), Color.clear, 3f, 0f);
                    ApplyBustPortrait(slot.Portrait, null, Color.white, new Color(1f, 1f, 1f, 0.06f));
                    if (fillBots)
                    {
                        slot.NameLabel.text = string.Format(CultureInfo.InvariantCulture, "{0} Bot #{1}", teamLabel, i + 1);
                        slot.NameLabel.color = SubtleText;
                        SyncOutlineForColor(slot.NameLabel, SubtleText);
                        slot.SubLabel.text = "AI Squadmate (Auto-fill)";
                        ApplyGlassSurface(slot.BadgeBg, MutedBtn, TitaniumBorder, Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = PaperWhite;
                        slot.BadgeLabel.text = "BOT";
                    }
                    else
                    {
                        slot.NameLabel.text = "Open Slot";
                        slot.NameLabel.color = SubtleText;
                        SyncOutlineForColor(slot.NameLabel, SubtleText);
                        slot.SubLabel.text = "Tap " + teamLabel + " header to take slot";
                        ApplyGlassSurface(slot.BadgeBg, SurfaceDark, new Color(0.835f, 0.875f, 0.914f, 0.16f), Color.clear, 3f, 0f);
                        slot.BadgeLabel.color = SubtleText;
                        slot.BadgeLabel.text = "EMPTY";
                    }
                    SyncOutlineForColor(slot.BadgeLabel, slot.BadgeLabel.color);
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
            _topBar = Ui.Panel("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-32f, 108f));

            Button backBtn = MakeButton("BackBtn", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(124f, 96f), "BACK", MutedBtn, PaperWhite, 20, () => HandleBack());
            _backBtnRect = backBtn.GetComponent<RectTransform>();

            _titleBlockRect = Ui.Panel("TitleBlock", _topBar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            modeTitle = Ui.MakeText("LobbyModeTitle", _titleBlockRect, "5v5 ROOM LOBBY", 25, PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            modeTitle.resizeTextForBestFit = true;
            modeTitle.resizeTextMinSize = 16;
            modeTitle.resizeTextMaxSize = 26;
            modeTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            modeTitle.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(modeTitle, PaperWhite);
            modeTitle.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            modeTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            modeTitle.rectTransform.offsetMin = new Vector2(0f, 2f);
            modeTitle.rectTransform.offsetMax = new Vector2(0f, -4f);

            statusLabel = Ui.MakeText("LobbyStatus", _titleBlockRect, "Ready", 18, SubtleText, TextAnchor.MiddleLeft);
            statusLabel.resizeTextForBestFit = true;
            statusLabel.resizeTextMinSize = 15;
            statusLabel.resizeTextMaxSize = 19;
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(statusLabel, SubtleText);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusLabel.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            statusLabel.rectTransform.offsetMin = new Vector2(0f, 4f);
            statusLabel.rectTransform.offsetMax = new Vector2(0f, -2f);

            _roomRightCluster = Ui.Panel("RoomRightCluster", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(460f, 96f));

            codePill = Ui.Panel("RoomCodePill", _roomRightCluster, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(236f, 60f));
            StyleCard(codePill, SurfaceDark, PaperWhite);

            codeLabel = Ui.MakeText("RoomCodeText", codePill, "CODE: --------", 19, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            codeLabel.resizeTextForBestFit = true;
            codeLabel.resizeTextMinSize = 14;
            codeLabel.resizeTextMaxSize = 20;
            codeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            codeLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(codeLabel, PaperWhite);
            Stretch(codeLabel.rectTransform, 12f, 4f, 12f, 4f);

            Button copyBtn = MakeButton("CopyCodeBtn", _roomRightCluster, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 96f), "COPY", MutedBtn, PaperWhite, 18, CopyRoomShare);
            _copyCodeBtnRect = copyBtn.GetComponent<RectTransform>();

            Button qrBtn = MakeButton("QrModalBtn", _roomRightCluster, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(96f, 96f), "QR", PaperWhite, InkDark, 19, OpenRoomQrModal);
            _qrModalBtnRect = qrBtn.GetComponent<RectTransform>();
        }

        void BuildConnectPanel(
            RectTransform parent,
            out InputField serverInput,
            out InputField codeInput,
            out Text emptyRoomsLabel)
        {
            // Left card: Server URL + Create / Code Join
            _setupCardRect = Ui.Panel("SetupCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_setupCardRect, PanelBg, SignalVermilion);

            Text urlCaption = Ui.MakeText("UrlCaption", _setupCardRect, "ONLINE SERVER URL", 19, PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            urlCaption.resizeTextForBestFit = true;
            urlCaption.resizeTextMinSize = 15;
            urlCaption.resizeTextMaxSize = 20;
            SyncOutlineForColor(urlCaption, PaperWhite);
            _urlCaptionRect = urlCaption.rectTransform;

            serverInput = MakeInputField("ServerUrlInput", _setupCardRect, "wss://play.kalyeah.example or ws://127.0.0.1:8080", 19, 128);
            _serverInputRect = serverInput.GetComponent<RectTransform>();

            Button createBtn = MakeButton("CreateOnlineRoomBtn", _setupCardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(240f, 96f), "CREATE 5v5 ROOM", SignalVermilion, PaperWhite, 20, () => ConnectAndCreateOnlineRoom(), primaryCta: true);
            _createRoomBtnRect = createBtn.GetComponent<RectTransform>();

            Button browseBtn = MakeButton("BrowseOpenBtn", _setupCardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(160f, 96f), "REFRESH LIST", MutedBtn, PaperWhite, 18, () => ConnectForOnlineBrowse());
            _browseOpenBtnRect = browseBtn.GetComponent<RectTransform>();

            Text codeCaption = Ui.MakeText("CodeCaption", _setupCardRect, "JOIN BY 8-CHAR ROOM CODE", 19, GoldCta, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            codeCaption.resizeTextForBestFit = true;
            codeCaption.resizeTextMinSize = 15;
            codeCaption.resizeTextMaxSize = 20;
            SyncOutlineForColor(codeCaption, GoldCta);
            _codeCaptionRect = codeCaption.rectTransform;

            codeInput = MakeInputField("RoomCodeInput", _setupCardRect, "ABCD2345", 22, 8);
            _codeInputRect = codeInput.GetComponent<RectTransform>();
            InputField capturedCode = codeInput;
            codeInput.onValueChanged.AddListener(raw =>
            {
                string cleaned = CleanRoomCode(raw);
                if (!string.Equals(raw, cleaned, StringComparison.Ordinal))
                    capturedCode.SetTextWithoutNotify(cleaned);
            });

            Button pasteBtn = MakeButton("PasteCodeBtn", _setupCardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(140f, 96f), "PASTE", MutedBtn, PaperWhite, 19, PasteCodeFromClipboard);
            _pasteCodeBtnRect = pasteBtn.GetComponent<RectTransform>();

            Button joinCodeBtn = MakeButton("JoinByCodeBtn", _setupCardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(240f, 96f), "JOIN WITH CODE", PaperWhite, InkDark, 20, () => JoinByCode());
            _joinByCodeBtnRect = joinCodeBtn.GetComponent<RectTransform>();

            Button scanQrBtn = MakeButton("ScanCodeQrBtn", _setupCardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(140f, 96f), "SCAN QR", MutedBtn, PaperWhite, 19, RequestScanRoomCode);
            _scanCodeQrBtnRect = scanQrBtn.GetComponent<RectTransform>();

            // Right card: Open Rooms browser
            _openRoomsCardRect = Ui.Panel("OpenRoomsCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_openRoomsCardRect, PanelBg, PaperWhite);

            Text listHeader = Ui.MakeText("OpenRoomsHeader", _openRoomsCardRect, "OPEN ROOMS (TAP TO JOIN)", 21, PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            listHeader.resizeTextForBestFit = true;
            listHeader.resizeTextMinSize = 16;
            listHeader.resizeTextMaxSize = 22;
            SyncOutlineForColor(listHeader, PaperWhite);
            _openRoomsHeaderRect = listHeader.rectTransform;

            _openRoomsScrollRect = Ui.Panel("OpenRoomsListArea", _openRoomsCardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image listHit = _openRoomsScrollRect.gameObject.AddComponent<Image>();
            ApplyGlassSurface(listHit, SurfaceDark, new Color(0.835f, 0.875f, 0.914f, 0.16f), Color.clear, 4f, 0f);
            listHit.raycastTarget = true;
            _openRoomsScrollRect.gameObject.AddComponent<RectMask2D>();

            var scroll = _openRoomsScrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = _openRoomsScrollRect;

            RectTransform content = Ui.Panel("Content", _openRoomsScrollRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            emptyRoomsLabel = Ui.MakeText("EmptyOpenRooms", _openRoomsScrollRect, "Connect to a server to list open rooms, or create one on the left!", 19, SubtleText, TextAnchor.MiddleCenter);
            emptyRoomsLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            emptyRoomsLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(emptyRoomsLabel, SubtleText);
            Stretch(emptyRoomsLabel.rectTransform, 18f, 12f, 18f, 12f);

            for (int i = 0; i < MaxOpenRoomRows; i++)
            {
                int idx = i;
                RectTransform row = Ui.Panel(
                    "OpenRoom_" + i.ToString(CultureInfo.InvariantCulture),
                    content,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(0f, 96f));
                Image bg = row.gameObject.AddComponent<Image>();
                ApplyGlassSurface(bg, MutedBtn, TitaniumBorder, PaperWhite, 4f, 3f);
                bg.raycastTarget = true;

                Button btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    string code = _openRoomRows[idx].Code;
                    if (!string.IsNullOrEmpty(code)) JoinByCode(code);
                });
                UiShaders.AttachPressSpring(btn, 0.96f, 2f);

                var le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 96f;
                le.preferredHeight = 96f;
                le.flexibleWidth = 1f;
                _openRoomLayouts[i] = le;

                Text title = Ui.MakeText("Title", row, "", 19, PaperWhite, TextAnchor.MiddleLeft);
                title.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(title, PaperWhite);
                title.rectTransform.anchorMin = new Vector2(0f, 0f);
                title.rectTransform.anchorMax = new Vector2(0.54f, 1f);
                title.rectTransform.offsetMin = new Vector2(16f, 4f);
                title.rectTransform.offsetMax = new Vector2(-6f, -4f);

                Text meta = Ui.MakeText("Meta", row, "", 18, GoldCta, TextAnchor.MiddleRight);
                meta.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(meta, GoldCta);
                meta.rectTransform.anchorMin = new Vector2(0.56f, 0f);
                meta.rectTransform.anchorMax = new Vector2(1f, 1f);
                meta.rectTransform.offsetMin = new Vector2(6f, 4f);
                meta.rectTransform.offsetMax = new Vector2(-16f, -4f);

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
            // Blue Squad Column (5 read-only squad slots under >= 48 dp JoinBlueHeaderBtn)
            _blueColRect = Ui.Panel("BlueTeamColumn", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_blueColRect, PanelBg, BlueHeader);

            Button blueJoinBtn = MakeButton("JoinBlueHeaderBtn", _blueColRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 96f), "BLUE SQUAD (0/5) — TAP TO JOIN", BlueHeader, PaperWhite, 20, () => PickTeam(0));
            _blueJoinBtnRect = blueJoinBtn.GetComponent<RectTransform>();
            blueHeader = _blueJoinBtnRect.Find("Label").GetComponent<Text>();

            _blueSlotsRect = Ui.Panel("BlueSlots", _blueColRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildTeamSlotRows(_blueSlotsRect, _blueSlots, 0);

            // Red Squad Column (5 read-only squad slots under >= 48 dp JoinRedHeaderBtn)
            _redColRect = Ui.Panel("RedTeamColumn", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_redColRect, PanelBg, RedHeader);

            Button redJoinBtn = MakeButton("JoinRedHeaderBtn", _redColRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 96f), "RED SQUAD (0/5) — TAP TO JOIN", RedHeader, PaperWhite, 20, () => PickTeam(1));
            _redJoinBtnRect = redJoinBtn.GetComponent<RectTransform>();
            redHeader = _redJoinBtnRect.Find("Label").GetComponent<Text>();

            _redSlotsRect = Ui.Panel("RedSlots", _redColRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildTeamSlotRows(_redSlotsRect, _redSlots, 1);

            // Bottom Controls Bar: single non-overlapping >= 48 dp action deck
            _bottomBarRect = Ui.Panel("BottomControls", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 112f));
            StyleCard(_bottomBarRect, PanelBg);

            // 1. Kid picker card button (portrait bust + kid name + PICK KID hint)
            Button pickKidBtn = MakeButton("PickScreenBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(190f, 96f), "", MutedBtn, PaperWhite, 18, InvokePickKidShortcut);
            _pickKidBtnRect = pickKidBtn.GetComponent<RectTransform>();
            Transform defaultPickLabel = _pickKidBtnRect.Find("Label");
            if (defaultPickLabel != null)
                defaultPickLabel.gameObject.SetActive(false);

            _myKidThumbFrame = Ui.Panel("MyKidThumbFrame", _pickKidBtnRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(52f, 52f));
            Image thumbPlate = _myKidThumbFrame.gameObject.AddComponent<Image>();
            ApplyGlassSurface(thumbPlate, SurfaceDark, TitaniumBorder, Color.clear, 3f, 0f);
            thumbPlate.raycastTarget = false;
            _myKidThumbFrame.gameObject.AddComponent<RectMask2D>();

            var thumbGo = new GameObject("MyKidThumb", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            thumbGo.transform.SetParent(_myKidThumbFrame, false);
            myKidThumb = thumbGo.GetComponent<RawImage>();
            myKidThumb.raycastTarget = false;
            var thumbFit = thumbGo.GetComponent<AspectRatioFitter>();
            thumbFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            thumbFit.aspectRatio = 1f;

            _myKidTextBlock = Ui.Panel("MyKidTextBlock", _pickKidBtnRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            myKidLabel = Ui.MakeText("MyKidName", _myKidTextBlock, "KARDING", 18, PaperWhite, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            myKidLabel.raycastTarget = false;
            myKidLabel.resizeTextForBestFit = true;
            myKidLabel.resizeTextMinSize = 14;
            myKidLabel.resizeTextMaxSize = 19;
            myKidLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(myKidLabel, PaperWhite);
            myKidLabel.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            myKidLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            myKidLabel.rectTransform.offsetMin = new Vector2(4f, 0f);
            myKidLabel.rectTransform.offsetMax = new Vector2(-4f, -4f);

            Text pickSub = Ui.MakeText("PickKidHint", _myKidTextBlock, "PICK KID", 15, GoldCta, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            pickSub.raycastTarget = false;
            pickSub.resizeTextForBestFit = true;
            pickSub.resizeTextMinSize = 13;
            pickSub.resizeTextMaxSize = 16;
            pickSub.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(pickSub, GoldCta);
            pickSub.rectTransform.anchorMin = new Vector2(0f, 0f);
            pickSub.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            pickSub.rectTransform.offsetMin = new Vector2(4f, 4f);
            pickSub.rectTransform.offsetMax = new Vector2(-4f, 0f);

            // 2. Swap Team + Host/Room Rule Chips
            Button swapBtn = MakeButton("SwapTeamBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(114f, 96f), "SWAP\nTEAM", MutedBtn, PaperWhite, 18, SwapTeam);
            _swapTeamBtnRect = swapBtn.GetComponent<RectTransform>();

            _mapButton = MakeButton("MapCycleBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(156f, 96f), "GAME:\nAGAWANG BASE", MutedBtn, PaperWhite, 17, CycleMap);
            _mapBtnBg = (Image)_mapButton.targetGraphic;
            _mapBtnRect = _mapButton.GetComponent<RectTransform>();
            mapLabel = _mapBtnRect.Find("Label").GetComponent<Text>();

            _firstToButton = MakeButton("FirstToBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(112f, 96f), "FIRST\nTO 3", MutedBtn, PaperWhite, 17, CycleFirstTo);
            _firstToBtnBg = (Image)_firstToButton.targetGraphic;
            _firstToBtnRect = _firstToButton.GetComponent<RectTransform>();
            firstToLabel = _firstToBtnRect.Find("Label").GetComponent<Text>();

            botsBtn = MakeButton("BotFillBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(112f, 96f), "BOTS:\nOFF", MutedBtn, PaperWhite, 17, ToggleBotFill);
            _botsBtnBg = (Image)botsBtn.targetGraphic;
            _botsBtnRect = botsBtn.GetComponent<RectTransform>();
            botsLabel = _botsBtnRect.Find("Label").GetComponent<Text>();

            openBtn = MakeButton("OpenPrivacyBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(114f, 96f), "PUBLIC\nROOM", MutedBtn, PaperWhite, 17, ToggleOpenPrivacy);
            _openBtnBg = (Image)openBtn.targetGraphic;
            _openBtnRect = openBtn.GetComponent<RectTransform>();
            openLabel = _openBtnRect.Find("Label").GetComponent<Text>();

            // 3. Ready toggle + Always-visible Start Match plate
            Button readyBtn = MakeButton("ReadyToggleBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(140f, 96f), "TAP\nREADY", PaperWhite, InkDark, 18, PressReady);
            readyBg = (Image)readyBtn.targetGraphic;
            _readyBtnRect = readyBtn.GetComponent<RectTransform>();
            readyLabel = _readyBtnRect.Find("Label").GetComponent<Text>();

            startBtn = MakeButton("StartMatchBtn", _bottomBarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 96f), "START\nMATCH!", SignalVermilion, PaperWhite, 19, PressStart, primaryCta: true);
            startBg = (Image)startBtn.targetGraphic;
            _startBtnRect = startBtn.GetComponent<RectTransform>();
            startLabel = _startBtnRect.Find("Label").GetComponent<Text>();
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
                    new Vector2(0f, -4f));

                // Read-only squad member card (JoinBlueHeaderBtn / JoinRedHeaderBtn above handle >= 48 dp team picking)
                Image bg = Ui.MakeImage("SlotBg", row, EmptySlotFill, raycast: false);
                ApplyGlassSurface(bg, EmptySlotFill, new Color(0.835f, 0.875f, 0.914f, 0.14f), Color.clear, 3f, 0f);
                Stretch(bg.rectTransform);

                RectTransform portraitFrame = Ui.Panel("PortraitFrame", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(44f, 44f));
                Image pFrameBg = portraitFrame.gameObject.AddComponent<Image>();
                ApplyGlassSurface(pFrameBg, SurfaceDark, TitaniumBorder, Color.clear, 3f, 0f);
                pFrameBg.raycastTarget = false;
                portraitFrame.gameObject.AddComponent<RectMask2D>();

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                portraitGo.transform.SetParent(portraitFrame, false);
                var portrait = portraitGo.GetComponent<RawImage>();
                portrait.raycastTarget = false;
                var fitter = portraitGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 1f;

                Text nameText = Ui.MakeText("PlayerName", row, "Open Slot", 18, PaperWhite, TextAnchor.LowerLeft);
                nameText.raycastTarget = false;
                nameText.resizeTextForBestFit = true;
                nameText.resizeTextMinSize = 14;
                nameText.resizeTextMaxSize = 19;
                nameText.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(nameText, PaperWhite);
                nameText.rectTransform.anchorMin = new Vector2(0f, 0.48f);
                nameText.rectTransform.anchorMax = new Vector2(1f, 0.96f);
                nameText.rectTransform.offsetMin = new Vector2(60f, 0f);
                nameText.rectTransform.offsetMax = new Vector2(-110f, 0f);

                Text subText = Ui.MakeText("SubInfo", row, "", 15, SubtleText, TextAnchor.UpperLeft);
                subText.raycastTarget = false;
                subText.resizeTextForBestFit = true;
                subText.resizeTextMinSize = 13;
                subText.resizeTextMaxSize = 16;
                subText.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(subText, SubtleText);
                subText.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                subText.rectTransform.anchorMax = new Vector2(1f, 0.48f);
                subText.rectTransform.offsetMin = new Vector2(60f, 0f);
                subText.rectTransform.offsetMax = new Vector2(-110f, 0f);

                RectTransform badge = Ui.Panel("ReadyBadge", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(96f, 34f));
                Image badgeBg = Ui.MakeImage("Bg", badge, MutedBtn, raycast: false);
                ApplyGlassSurface(badgeBg, MutedBtn, TitaniumBorder, Color.clear, 3f, 0f);
                Stretch(badgeBg.rectTransform);
                Text badgeText = Ui.MakeText("Text", badge, "EMPTY", 15, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
                badgeText.raycastTarget = false;
                badgeText.resizeTextForBestFit = true;
                badgeText.resizeTextMinSize = 12;
                badgeText.resizeTextMaxSize = 16;
                SyncOutlineForColor(badgeText, PaperWhite);
                Stretch(badgeText.rectTransform, 6f, 2f, 6f, 2f);

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

        RectTransform BuildQrModal(RectTransform fullBleedParent, out Image qrImg, out Text qrText)
        {
            RectTransform overlay = Ui.Fill("RoomQrModal", fullBleedParent);

            RectTransform scrim = Ui.Fill("Dim", overlay);
            Image dim = scrim.gameObject.AddComponent<Image>();
            dim.color = new Color(0.020f, 0.028f, 0.040f, 0.88f);
            dim.raycastTarget = true;
            var dismiss = scrim.gameObject.AddComponent<RoomQrScrimDismiss>();
            dismiss.OnDismiss = () => overlay.gameObject.SetActive(false);

            RectTransform safe = Ui.Fill("Safe", overlay);
            safe.gameObject.AddComponent<SafeArea>();

            _qrCardRect = Ui.Panel("QrCard", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 540f));
            StyleCard(_qrCardRect, PanelBg, SignalVermilion);
            Image cardBlocker = _qrCardRect.GetComponentInChildren<Image>();
            if (cardBlocker != null) cardBlocker.raycastTarget = true;

            Text title = Ui.MakeText("Title", _qrCardRect, "SCAN TO JOIN ROOM", 24, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 26;
            SyncOutlineForColor(title, PaperWhite);
            _qrCardTitleRect = title.rectTransform;

            _qrCardPlateRect = Ui.Panel("WhitePlate", _qrCardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 260f));
            Image white = Ui.MakeImage("Bg", _qrCardPlateRect, Color.white);
            ApplySlicedPlate(white, Color.white);
            Stretch(white.rectTransform);

            qrImg = Ui.MakeImage("QrCodeImage", _qrCardPlateRect, Color.white);
            qrImg.preserveAspect = true;
            Stretch(qrImg.rectTransform, 12f, 12f, 12f, 12f);

            qrText = Ui.MakeText("Caption", _qrCardRect, "", 18, PaperWhite, TextAnchor.MiddleCenter);
            qrText.horizontalOverflow = HorizontalWrapMode.Wrap;
            qrText.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(qrText, PaperWhite);
            _qrCardCaptionRect = qrText.rectTransform;

            Button closeBtn = MakeButton("CloseQrBtn", _qrCardRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(188f, 96f), "CLOSE", MutedBtn, PaperWhite, 20, () => overlay.gameObject.SetActive(false));
            _qrCloseBtnRect = closeBtn.GetComponent<RectTransform>();
            return overlay;
        }

        void ShowRoomView(bool inRoom)
        {
            _connectPanel.gameObject.SetActive(!inRoom);
            _roomPanel.gameObject.SetActive(inRoom);
            if (_roomRightCluster != null)
                _roomRightCluster.gameObject.SetActive(inRoom);
            else if (_roomCodePill != null)
                _roomCodePill.gameObject.SetActive(inRoom);
            ApplyResponsiveMetrics();
        }

        void RefreshMyKidChip()
        {
            string kid = AppSession.Current.SelectedCharacter;
            if (_myKidLabel != null)
                _myKidLabel.text = PrettyKidName(kid).ToUpperInvariant();
            if (_myKidThumb != null)
            {
                Texture2D tex = LoadPortrait(kid);
                ApplyBustPortrait(_myKidThumb, tex, Color.white, new Color(1f, 1f, 1f, 0.2f));
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
            ApplyResponsiveMetrics();
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
            Texture2D loaded = KalyeahResources.KidBust(characterId);
            if (loaded == null)
                loaded = KalyeahResources.Texture("portraits/" + characterId);
            _portraitCache[characterId] = loaded;
            return loaded;
        }

        static void ApplyBustPortrait(RawImage img, Texture2D tex, Color loadedTint, Color emptyTint)
        {
            if (img == null) return;
            img.texture = tex;
            img.color = tex != null ? loadedTint : emptyTint;
            img.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        void SetStatus(string text, Color color)
        {
            StatusText = text ?? "";
            if (_statusLabel != null)
            {
                _statusLabel.text = StatusText;
                _statusLabel.color = color;
                SyncOutlineForColor(_statusLabel, color);
            }
        }

        void ApplyResponsiveMetrics()
        {
            if (_inLayout || _frame == null) return;
            _inLayout = true;
            try
            {
                if (_canvas == null && Root != null)
                    _canvas = Root.GetComponentInParent<Canvas>();

                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float minTouch = ScreenFitMetrics.ClampTouchSize(48f, ScreenFitMetrics.MinTouchDp, _canvas);
                float gutter = Mathf.Max(14f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));

                float frameW = Mathf.Clamp(safe.x - 16f, Mathf.Min(safe.x - 8f, 640f), 1320f);
                float frameH = Mathf.Clamp(safe.y - 16f, Mathf.Min(safe.y - 8f, 520f), 670f);
                _frame.sizeDelta = new Vector2(frameW, frameH);

                float padX = 14f;
                float topH = Mathf.Max(108f, minTouch + 12f);
                if (_topBar != null)
                {
                    _topBar.anchorMin = new Vector2(0f, 1f);
                    _topBar.anchorMax = new Vector2(1f, 1f);
                    _topBar.pivot = new Vector2(0.5f, 1f);
                    _topBar.offsetMin = new Vector2(padX, -(6f + topH));
                    _topBar.offsetMax = new Vector2(-padX, -6f);
                }

                float barW = Mathf.Max(320f, frameW - padX * 2f);
                float backW = Mathf.Max(minTouch, 124f);
                if (_backBtnRect != null)
                    _backBtnRect.sizeDelta = new Vector2(backW, minTouch);

                bool rightActive = _roomRightCluster != null && _roomRightCluster.gameObject.activeSelf;
                float rightClusterW = 0f;
                if (_roomRightCluster != null)
                {
                    float qrW = minTouch;
                    float copyW = Mathf.Max(minTouch, 100f);
                    float pillW = Mathf.Clamp(barW * 0.22f, 168f, 256f);
                    rightClusterW = pillW + gutter + copyW + gutter + qrW;
                    _roomRightCluster.sizeDelta = new Vector2(rightClusterW, minTouch);

                    if (_qrModalBtnRect != null)
                    {
                        _qrModalBtnRect.anchoredPosition = Vector2.zero;
                        _qrModalBtnRect.sizeDelta = new Vector2(qrW, minTouch);
                    }
                    if (_copyCodeBtnRect != null)
                    {
                        _copyCodeBtnRect.anchoredPosition = new Vector2(-(qrW + gutter), 0f);
                        _copyCodeBtnRect.sizeDelta = new Vector2(copyW, minTouch);
                    }
                    if (_roomCodePill != null)
                    {
                        _roomCodePill.anchoredPosition = new Vector2(-(qrW + gutter + copyW + gutter), 0f);
                        _roomCodePill.sizeDelta = new Vector2(pillW, Mathf.Min(topH - 16f, 62f));
                    }
                }
                if (_titleBlockRect != null)
                    Stretch(_titleBlockRect, backW + gutter, 4f, rightActive ? rightClusterW + gutter : 8f, 4f);

                float contentTop = topH + 12f;
                float contentBottom = 12f;
                float contentW = Mathf.Max(320f, frameW - padX * 2f);
                float contentH = Mathf.Max(300f, frameH - contentTop - contentBottom);

                SetPanelBounds(_connectPanel, padX, contentBottom, contentTop);
                SetPanelBounds(_roomPanel, padX, contentBottom, contentTop);

                // OnlineConnectPanel layout
                float setupW = Mathf.Round((contentW - gutter) * 0.50f);
                float openW = Mathf.Max(240f, contentW - gutter - setupW);
                if (_setupCardRect != null)
                {
                    _setupCardRect.anchorMin = new Vector2(0f, 0f);
                    _setupCardRect.anchorMax = new Vector2(0f, 1f);
                    _setupCardRect.pivot = new Vector2(0f, 0.5f);
                    _setupCardRect.offsetMin = new Vector2(0f, 0f);
                    _setupCardRect.offsetMax = new Vector2(setupW, 0f);
                }
                float cardPad = 16f;
                float innerSetupW = Mathf.Max(minTouch * 2f + gutter, setupW - cardPad * 2f);
                float freeSetupH = Mathf.Max(20f, contentH - minTouch * 4f);
                float capH = Mathf.Clamp(freeSetupH * 0.20f, 22f, 30f);
                float rowGap = Mathf.Clamp(freeSetupH * 0.08f, 6f, 12f);
                float sectionGap = Mathf.Clamp(freeSetupH * 0.12f, 10f, 18f);
                float sy = Mathf.Clamp((contentH - (capH * 2f + minTouch * 4f + rowGap * 2f + sectionGap + 8f)) * 0.5f, 6f, 18f);

                SetTopRow(_urlCaptionRect, cardPad, sy, innerSetupW, capH);
                sy += capH + 4f;
                SetTopRow(_serverInputRect, cardPad, sy, innerSetupW, minTouch);
                sy += minTouch + rowGap;

                float pairAvail = Mathf.Max(minTouch * 2f, innerSetupW - gutter);
                float createW = Mathf.Max(minTouch, Mathf.Round(pairAvail * 0.58f));
                float browseW = Mathf.Max(minTouch, pairAvail - createW);
                createW = Mathf.Max(minTouch, pairAvail - browseW);
                SetTopLeftBox(_createRoomBtnRect, cardPad, sy, createW, minTouch);
                SetTopLeftBox(_browseOpenBtnRect, cardPad + createW + gutter, sy, browseW, minTouch);
                sy += minTouch + sectionGap;

                SetTopRow(_codeCaptionRect, cardPad, sy, innerSetupW, capH);
                sy += capH + 4f;

                float codeW = Mathf.Max(minTouch, Mathf.Round(pairAvail * 0.62f));
                float sideW = Mathf.Max(minTouch, pairAvail - codeW);
                codeW = Mathf.Max(minTouch, pairAvail - sideW);
                SetTopLeftBox(_codeInputRect, cardPad, sy, codeW, minTouch);
                SetTopLeftBox(_pasteCodeBtnRect, cardPad + codeW + gutter, sy, sideW, minTouch);
                sy += minTouch + rowGap;

                SetTopLeftBox(_joinByCodeBtnRect, cardPad, sy, codeW, minTouch);
                SetTopLeftBox(_scanCodeQrBtnRect, cardPad + codeW + gutter, sy, sideW, minTouch);

                if (_openRoomsCardRect != null)
                {
                    _openRoomsCardRect.anchorMin = new Vector2(0f, 0f);
                    _openRoomsCardRect.anchorMax = new Vector2(0f, 1f);
                    _openRoomsCardRect.pivot = new Vector2(0f, 0.5f);
                    _openRoomsCardRect.offsetMin = new Vector2(setupW + gutter, 0f);
                    _openRoomsCardRect.offsetMax = new Vector2(contentW, 0f);
                }
                if (_openRoomsHeaderRect != null)
                {
                    _openRoomsHeaderRect.anchorMin = new Vector2(0f, 1f);
                    _openRoomsHeaderRect.anchorMax = new Vector2(1f, 1f);
                    _openRoomsHeaderRect.pivot = new Vector2(0.5f, 1f);
                    _openRoomsHeaderRect.offsetMin = new Vector2(16f, -48f);
                    _openRoomsHeaderRect.offsetMax = new Vector2(-16f, -12f);
                }
                if (_openRoomsScrollRect != null)
                {
                    _openRoomsScrollRect.anchorMin = Vector2.zero;
                    _openRoomsScrollRect.anchorMax = Vector2.one;
                    _openRoomsScrollRect.offsetMin = new Vector2(16f, 14f);
                    _openRoomsScrollRect.offsetMax = new Vector2(-16f, -54f);
                }
                for (int i = 0; i < MaxOpenRoomRows; i++)
                {
                    if (_openRoomLayouts[i] != null)
                    {
                        _openRoomLayouts[i].minHeight = minTouch;
                        _openRoomLayouts[i].preferredHeight = minTouch;
                    }
                }

                // RoomPanel layout
                float bottomBarH = minTouch + 16f;
                if (_bottomBarRect != null)
                {
                    _bottomBarRect.anchorMin = new Vector2(0f, 0f);
                    _bottomBarRect.anchorMax = new Vector2(1f, 0f);
                    _bottomBarRect.pivot = new Vector2(0.5f, 0f);
                    _bottomBarRect.anchoredPosition = Vector2.zero;
                    _bottomBarRect.sizeDelta = new Vector2(0f, bottomBarH);
                }

                float colW = Mathf.Max(220f, (contentW - gutter) * 0.5f);
                float colBottom = bottomBarH + 10f;
                LayoutTeamColumn(_blueColRect, _blueJoinBtnRect, _blueSlotsRect, 0f, colW, colBottom, minTouch);
                LayoutTeamColumn(_redColRect, _redJoinBtnRect, _redSlotsRect, colW + gutter, colW, colBottom, minTouch);

                LayoutBottomControls(contentW, minTouch, gutter);
                LayoutQrOverlay(safe, minTouch);
            }
            finally
            {
                _inLayout = false;
            }
        }

        void LayoutBottomControls(float contentW, float minTouch, float gutter)
        {
            if (_bottomBarRect == null) return;
            const float barPad = 10f;
            float innerW = Mathf.Max(minTouch * 4f, contentW - barPad * 2f);

            RectTransform[] items =
            {
                _pickKidBtnRect,
                _swapTeamBtnRect,
                _mapBtnRect,
                _firstToBtnRect,
                _botsBtnRect,
                _openBtnRect,
                _readyBtnRect,
                _startBtnRect,
            };
            float[] weights = { 2.15f, 1.00f, 1.55f, 1.00f, 1.00f, 1.05f, 1.35f, 1.50f };

            int activeCount = 0;
            float extraWeightSum = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null || !items[i].gameObject.activeSelf) continue;
                activeCount++;
                extraWeightSum += Mathf.Max(0.05f, weights[i] - 0.85f);
            }
            if (activeCount == 0) return;

            float maxAllowedGap = activeCount > 1
                ? Mathf.Max(4f, (innerW - activeCount * minTouch) / (activeCount - 1))
                : 0f;
            float gap = activeCount > 1 ? Mathf.Min(gutter, maxAllowedGap) : 0f;
            float surplus = Mathf.Max(0f, innerW - gap * (activeCount - 1) - activeCount * minTouch);

            float cursorX = barPad;
            for (int i = 0; i < items.Length; i++)
            {
                RectTransform rt = items[i];
                if (rt == null || !rt.gameObject.activeSelf) continue;
                float share = extraWeightSum > 0f ? Mathf.Max(0.05f, weights[i] - 0.85f) / extraWeightSum : (1f / activeCount);
                float btnW = Mathf.Max(minTouch, minTouch + surplus * share);

                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(cursorX, 0f);
                rt.sizeDelta = new Vector2(btnW, minTouch);
                cursorX += btnW + gap;

                if (ReferenceEquals(rt, _pickKidBtnRect))
                {
                    bool showThumb = btnW >= 148f;
                    if (_myKidThumbFrame != null)
                    {
                        _myKidThumbFrame.gameObject.SetActive(showThumb);
                        float thumbSz = Mathf.Clamp(minTouch - 26f, 40f, 56f);
                        _myKidThumbFrame.anchoredPosition = new Vector2(10f, 0f);
                        _myKidThumbFrame.sizeDelta = new Vector2(thumbSz, thumbSz);
                        if (_myKidTextBlock != null)
                            Stretch(_myKidTextBlock, showThumb ? (14f + thumbSz) : 6f, 4f, 6f, 4f);
                    }
                }
            }
        }

        void LayoutQrOverlay(Vector2 safe, float minTouch)
        {
            if (_qrCardRect == null) return;
            float cardW = Mathf.Clamp(safe.x - 24f, 320f, 480f);
            float cardH = Mathf.Clamp(safe.y - 20f, 420f, 560f);
            _qrCardRect.sizeDelta = new Vector2(cardW, cardH);

            if (_qrCardTitleRect != null)
            {
                _qrCardTitleRect.anchorMin = new Vector2(0f, 1f);
                _qrCardTitleRect.anchorMax = new Vector2(1f, 1f);
                _qrCardTitleRect.pivot = new Vector2(0.5f, 1f);
                _qrCardTitleRect.offsetMin = new Vector2(18f, -56f);
                _qrCardTitleRect.offsetMax = new Vector2(-18f, -14f);
            }
            if (_qrCloseBtnRect != null)
            {
                _qrCloseBtnRect.anchoredPosition = new Vector2(0f, 14f);
                _qrCloseBtnRect.sizeDelta = new Vector2(Mathf.Max(minTouch, 196f), minTouch);
            }
            float capBottom = 14f + minTouch + 10f;
            float capTop = capBottom + 48f;
            if (_qrCardCaptionRect != null)
            {
                _qrCardCaptionRect.anchorMin = new Vector2(0f, 0f);
                _qrCardCaptionRect.anchorMax = new Vector2(1f, 0f);
                _qrCardCaptionRect.pivot = new Vector2(0.5f, 0f);
                _qrCardCaptionRect.offsetMin = new Vector2(18f, capBottom);
                _qrCardCaptionRect.offsetMax = new Vector2(-18f, capTop);
            }
            if (_qrCardPlateRect != null)
            {
                float topUsed = 62f;
                float bottomUsed = capTop + 8f;
                float availH = Mathf.Max(140f, cardH - topUsed - bottomUsed);
                float qrSz = Mathf.Clamp(Mathf.Min(cardW - 48f, availH), 148f, 268f);
                float centerOffsetY = (bottomUsed - topUsed) * 0.5f;
                _qrCardPlateRect.anchoredPosition = new Vector2(0f, centerOffsetY);
                _qrCardPlateRect.sizeDelta = new Vector2(qrSz, qrSz);
            }
        }

        static void LayoutTeamColumn(
            RectTransform col,
            RectTransform headerBtn,
            RectTransform slotsContainer,
            float x,
            float w,
            float bottom,
            float minTouch)
        {
            if (col == null) return;
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.offsetMin = new Vector2(x, bottom);
            col.offsetMax = new Vector2(x + w, 0f);

            if (headerBtn != null)
            {
                headerBtn.anchorMin = new Vector2(0f, 1f);
                headerBtn.anchorMax = new Vector2(1f, 1f);
                headerBtn.pivot = new Vector2(0.5f, 1f);
                headerBtn.offsetMin = new Vector2(10f, -(8f + minTouch));
                headerBtn.offsetMax = new Vector2(-10f, -8f);
            }
            if (slotsContainer != null)
            {
                slotsContainer.anchorMin = Vector2.zero;
                slotsContainer.anchorMax = Vector2.one;
                slotsContainer.offsetMin = new Vector2(10f, 8f);
                slotsContainer.offsetMax = new Vector2(-10f, -(minTouch + 16f));
            }
        }

        static void SetPanelBounds(RectTransform panel, float padX, float bottom, float top)
        {
            if (panel == null) return;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = new Vector2(padX, bottom);
            panel.offsetMax = new Vector2(-padX, -top);
        }

        static void SetTopRow(RectTransform rt, float padX, float topY, float width, float height)
        {
            SetTopLeftBox(rt, padX, topY, width, height);
        }

        static void SetTopLeftBox(RectTransform rt, float x, float topY, float width, float height)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -topY);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void ApplyGlassSurface(
            Image img,
            Color fill,
            Color border,
            Color leftAccent,
            float cornerCut = 4f,
            float accentW = 3f)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            Material mat = UiShaders.CreateTacticalGlass(fill, border, leftAccent, cornerCut, leftAccent.a > 0.001f ? accentW : 0f);
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = fill;
            }
        }

        void ApplyCachedStartCta(Image img)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            if (_startCtaMat == null)
                _startCtaMat = UiShaders.CreateMetallicCta(SignalVermilion, SignalVermilionDeep, 8f);
            if (_startCtaMat != null && _startCtaMat != Graphic.defaultGraphicMaterial)
            {
                img.material = _startCtaMat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = SignalVermilion;
            }
        }

        static void ApplyMetallicCtaSurface(Image img, Color top, Color bottom, float cornerCut = 8f)
        {
            if (img == null) return;
            img.sprite = null;
            img.type = Image.Type.Simple;
            Material mat = UiShaders.CreateMetallicCta(top, bottom, cornerCut);
            if (mat != null && mat != Graphic.defaultGraphicMaterial)
            {
                img.material = mat;
                img.color = Color.white;
            }
            else
            {
                img.material = null;
                img.color = top;
            }
        }

        static void ApplySlicedPlate(Image img, Color color)
        {
            if (img == null) return;
            Sprite rounded = MenuStack.RoundedSprite();
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
        }

        static void SyncOutlineForColor(Text label, Color c)
        {
            if (label == null) return;
            Outline ol = label.GetComponent<Outline>();
            if (ol != null)
                ol.enabled = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) >= 0.25f;
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
            UnityEngine.Events.UnityAction onClick,
            bool primaryCta = false)
        {
            Vector2 effectiveSize = (anchorMin.x == anchorMax.x && anchorMin.y == anchorMax.y)
                ? ScreenFitMetrics.ClampTouchSize(size, ScreenFitMetrics.MinTouchDp, _canvas)
                : new Vector2(size.x, ScreenFitMetrics.ClampTouchSize(size.y, ScreenFitMetrics.MinTouchDp, _canvas));
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, effectiveSize);
            Image bg = rt.gameObject.AddComponent<Image>();
            if (primaryCta)
                ApplyMetallicCtaSurface(bg, SignalVermilion, SignalVermilionDeep, 8f);
            else
                ApplyGlassSurface(bg, fill, TitaniumBorder, Color.clear, 4f, 0f);
            bg.raycastTarget = true;

            Text label = Ui.MakeText("Label", rt, text, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(label, textColor);
            Stretch(label.rectTransform, 10f, 4f, 10f, 4f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            btn.colors = cb;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            UiShaders.AttachPressSpring(btn, 0.95f, 2f);
            _touchTargets.Add(rt);
            return btn;
        }

        InputField MakeInputField(string name, Transform parent, string placeholderText, int fontSize, int charLimit)
        {
            float touchH = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform rt = Ui.Panel(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(240f, touchH));
            Image bg = rt.gameObject.AddComponent<Image>();
            ApplyGlassSurface(bg, SurfaceDark, TitaniumBorder, PaperWhite, 3f, 2f);
            bg.raycastTarget = true;

            Text textComp = Ui.MakeText("Text", rt, "", fontSize, PaperWhite, TextAnchor.MiddleLeft);
            textComp.raycastTarget = false;
            textComp.supportRichText = false;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(textComp, PaperWhite);
            Stretch(textComp.rectTransform, 14f, 6f, 14f, 6f);

            Text placeholder = Ui.MakeText("Placeholder", rt, placeholderText, Mathf.Max(17, fontSize - 2), SubtleText, TextAnchor.MiddleLeft);
            placeholder.raycastTarget = false;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(placeholder, placeholder.color);
            Stretch(placeholder.rectTransform, 14f, 6f, 14f, 6f);

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
            if (string.IsNullOrWhiteSpace(id)) return "Karding";
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

        static void StyleCard(RectTransform card, Color bg, Color? leftAccent = null)
        {
            Image fill = Ui.MakeImage("CardBg", card, bg, raycast: false);
            Color accent = leftAccent ?? Color.clear;
            ApplyGlassSurface(fill, bg, TitaniumBorder, accent, 5f, accent.a > 0.001f ? 3f : 0f);
            Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();
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
            var black = new Color32(16, 21, 28, 255);
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

        [ExecuteAlways]
        sealed class LobbyViewDriver : MonoBehaviour
        {
            LobbyScreenView _owner;

            public void Bind(LobbyScreenView owner) => _owner = owner;

            void OnEnable()
            {
                if (_owner != null)
                {
                    _owner.SubscribeViewport(true);
                    _owner.ApplyResponsiveMetrics();
                }
            }

            void Update()
            {
                if (Application.isPlaying)
                    _owner?.Tick(Time.unscaledDeltaTime);
            }

            void OnRectTransformDimensionsChange() => _owner?.ApplyResponsiveMetrics();

            void OnDestroy() => _owner?.Dispose();
        }
    }
}
