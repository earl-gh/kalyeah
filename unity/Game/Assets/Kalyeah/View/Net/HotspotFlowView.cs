using System;
using System.Collections.Generic;
using System.Globalization;
using Kalyeah.Net;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Two-step LAN Hotspot Host / Join wizard on the 1280x720 landscape canvas:
    /// Step 1 chooses Host vs Join (with live Wi-Fi / Hotspot interface check);
    /// Step 2A starts AppSession.Current.StartHotspotHost, creates the room so UDP/WS discovery sees it, displays the shareable LAN
    /// ws:// URL + QR sprite (via optional qrSpriteProvider delegate), and enters LobbyScreenView;
    /// Step 2B starts AppSession.Current.StartHotspotDiscovery, lists verified HotspotFinder rooms, and supports QR scan or manual ws:// join.
    /// </summary>
    public sealed class HotspotFlowView : IDisposable
    {
        public enum FlowRole
        {
            ChooseRole = 0,
            Host = 1,
            Guest = 2,
        }

        const int MaxDiscoveredRows = 5;

        static readonly Color CardBg = new Color(0.055f, 0.075f, 0.102f, 0.96f);
        static readonly Color SubCardBg = new Color(0.075f, 0.102f, 0.138f, 0.94f);
        static readonly Color SurfaceDark = new Color(0.045f, 0.062f, 0.088f, 0.94f);
        static readonly Color PaperWhite = MenuArt.PaperWhite;
        static readonly Color SignalVermilion = new Color(1.00f, 0.275f, 0.333f, 1f);
        static readonly Color SignalVermilionDeep = new Color(0.83f, 0.19f, 0.25f, 1f);
        static readonly Color TitaniumBorder = new Color(0.835f, 0.875f, 0.914f, 0.28f);
        static readonly Color GoldCta = MenuArt.CoralGold;
        static readonly Color MutedBtn = new Color(0.115f, 0.145f, 0.190f, 0.96f);
        static readonly Color InkDark = MenuArt.Ink;
        static readonly Color SubtleText = MenuArt.MutedText;
        static readonly Color WarnText = MenuArt.CoralGold;
        static readonly Color OkText = MenuArt.SuccessGreen;

        static Sprite _fallbackQrSprite;

        readonly DataSource _data;
        Canvas _canvas;
        readonly NetRules _netRules;

        readonly RectTransform _safeRoot;
        readonly RectTransform _frame;
        readonly RectTransform _topBar;
        readonly RectTransform _backBtnRect;
        readonly RectTransform _netBadgePlate;
        readonly RectTransform _choosePanel;
        readonly RectTransform _hostPanel;
        readonly RectTransform _guestPanel;

        RectTransform _chooseSubtitleRect;
        RectTransform _hostRoleCard;
        RectTransform _hostRoleDescRect;
        RectTransform _startHostBtnRect;
        RectTransform _guestRoleCard;
        RectTransform _guestRoleDescRect;
        RectTransform _startGuestBtnRect;
        RectTransform _recheckBtnRect;

        RectTransform _hostInfoCard;
        RectTransform _hostStepTitleRect;
        RectTransform _hostStatusRect;
        RectTransform _urlBoxRect;
        RectTransform _hostUrlRect;
        RectTransform _copyUrlBtnRect;
        RectTransform _hostPlayersRect;
        RectTransform _retryHostBtnRect;
        RectTransform _enterLobbyBtnRect;
        RectTransform _qrCard;
        RectTransform _qrTitleRect;
        RectTransform _qrPlateRect;
        RectTransform _qrCaptionRect;

        RectTransform _guestListCard;
        RectTransform _guestStatusRect;
        RectTransform _scanQrBtnRect;
        RectTransform _rescanBtnRect;
        RectTransform _rowsScrollRect;
        readonly LayoutElement[] _discoveredRowLayouts = new LayoutElement[MaxDiscoveredRows];
        RectTransform _manualBarRect;
        RectTransform _manualPromptRect;
        RectTransform _manualInputRect;
        RectTransform _pasteUrlBtnRect;
        RectTransform _manualJoinBtnRect;

        readonly Text _headerTitle;
        readonly Text _networkBadge;
        readonly Text _hostUrlLabel;
        readonly Text _hostStatusLabel;
        readonly Text _hostPlayersLabel;
        readonly Image _qrImage;
        readonly Text _qrCaption;

        readonly Text _guestStatusLabel;
        readonly InputField _manualUrlInput;
        readonly DiscoveredRow[] _discoveredRows = new DiscoveredRow[MaxDiscoveredRows];
        readonly Text _emptyListLabel;

        readonly List<RectTransform> _touchTargets = new List<RectTransform>(16);

        GameClient _boundClient;
        Sprite _ownedQrSprite;
        bool _openedDirectly;
        bool _hostRoomRequested;
        bool _nativeSubscribed;
        bool _viewportSubscribed;
        bool _inLayout;
        string _lastQrPayload = "";
        string _lastNfcPayload = "";
        int _lastRenderedRoomCount = -1;
        string _lastRenderedFirstUrl = "";

        struct DiscoveredRow
        {
            public RectTransform Root;
            public Button Button;
            public Text Title;
            public Text UrlLabel;
            public string TargetUrl;
        }

        public RectTransform Root { get; }
        public FlowRole CurrentRole { get; private set; } = FlowRole.ChooseRole;
        public string ShareUrl { get; private set; } = "";
        public string StatusText { get; private set; } = "";

        public Action OnBack { get; set; }
        public Action OnEnterLobby { get; set; }
        public Func<string, Sprite> QrSpriteProvider { get; set; }
        public Func<string> LanIpProvider { get; set; }
        public Action<Action<string>> QrScanRequester { get; set; }
        public Action<WifiQr.Network> WifiJoinRequester { get; set; }

        public HotspotFlowView(
            Transform parent,
            DataSource data = null,
            Action onBack = null,
            Action onEnterLobby = null,
            Func<string, Sprite> qrSpriteProvider = null,
            Func<string> lanIpProvider = null,
            Action<Action<string>> qrScanRequester = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data ?? KalyeahResources.Data();
            OnBack = onBack;
            OnEnterLobby = onEnterLobby;
            QrSpriteProvider = qrSpriteProvider;
            LanIpProvider = lanIpProvider;
            QrScanRequester = qrScanRequester;

            SimData simData = AppSession.Current.EnsureSimData(_data);
            _netRules = NetRules.FromData(simData);
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("HotspotFlowView", parent);
            Image dim = Ui.MakeImage("Backdrop", Root, new Color(0.035f, 0.050f, 0.072f, 0.96f), raycast: true);
            Stretch(dim.rectTransform);

            _safeRoot = Ui.Fill("SafeRoot", Root);
            _safeRoot.gameObject.AddComponent<SafeArea>();

            _frame = Ui.Panel(
                "Frame",
                _safeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1180f, 660f));
            StyleCard(_frame, CardBg);

            // Top bar (>= 108 px tall)
            _topBar = Ui.Panel("TopBar", _frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-36f, 108f));
            Button backBtn = MakeActionButton("BackButton", _topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(136f, 96f), "BACK", MutedBtn, PaperWhite, 21, () => HandleBack());
            _backBtnRect = backBtn.GetComponent<RectTransform>();

            _headerTitle = Ui.MakeText("HeaderTitle", _topBar, "LOCAL HOTSPOT PLAY", 28, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _headerTitle.resizeTextForBestFit = true;
            _headerTitle.resizeTextMinSize = 18;
            _headerTitle.resizeTextMaxSize = 30;
            _headerTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            _headerTitle.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(_headerTitle, PaperWhite);
            Stretch(_headerTitle.rectTransform, 156f, 6f, 240f, 6f);

            _netBadgePlate = Ui.Panel("NetworkBadgePlate", _topBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(224f, 56f));
            StyleCard(_netBadgePlate, SurfaceDark, OkText);
            _networkBadge = Ui.MakeText("NetworkBadge", _netBadgePlate, "", 18, OkText, TextAnchor.MiddleCenter);
            _networkBadge.resizeTextForBestFit = true;
            _networkBadge.resizeTextMinSize = 16;
            _networkBadge.resizeTextMaxSize = 18;
            _networkBadge.horizontalOverflow = HorizontalWrapMode.Wrap;
            _networkBadge.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(_networkBadge, OkText);
            Stretch(_networkBadge.rectTransform, 10f, 4f, 10f, 4f);

            // Step 1: Choose Role
            _choosePanel = Ui.Panel("Step1ChooseRole", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildChoosePanel(_choosePanel);

            // Step 2A: Host Wizard
            _hostPanel = Ui.Panel("Step2HostWizard", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildHostPanel(_hostPanel, out _hostUrlLabel, out _hostStatusLabel, out _hostPlayersLabel, out _qrImage, out _qrCaption);

            // Step 2B: Guest Wizard
            _guestPanel = Ui.Panel("Step2GuestWizard", _frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildGuestPanel(_guestPanel, out _guestStatusLabel, out _manualUrlInput, out _emptyListLabel);

            var driver = Root.gameObject.AddComponent<HotspotFlowDriver>();
            driver.Bind(this);

            SubscribeNativeLink(true);
            SubscribeViewport(true);
            ApplyResponsiveMetrics(_frame);
            ShowChooseRole();
        }

        public void Show()
        {
            SubscribeNativeLink(true);
            SubscribeViewport(true);
            Root.gameObject.SetActive(true);
            OnShow();
        }

        public void Hide()
        {
            AndroidLocalLink.SetNfcReading(false);
            SubscribeNativeLink(false);
            Root.gameObject.SetActive(false);
        }

        public void OnShow()
        {
            ApplyResponsiveMetrics(_frame);
            if (AppSession.Current.Mode == GameMode.HotspotHost && AppSession.Current.Intent == LobbyIntent.Create && CurrentRole == FlowRole.Host)
            {
                RefreshNetworkBadge();
                RefreshHostUi();
                return;
            }
            if (CurrentRole == FlowRole.ChooseRole)
                RefreshNetworkBadge();
        }

        public void ShowChooseRole()
        {
            _openedDirectly = false;
            CurrentRole = FlowRole.ChooseRole;
            AndroidLocalLink.SetNfcReading(false);
            _headerTitle.text = "LOCAL HOTSPOT — STEP 1 OF 2";
            _choosePanel.gameObject.SetActive(true);
            _hostPanel.gameObject.SetActive(false);
            _guestPanel.gameObject.SetActive(false);
            RefreshNetworkBadge();
            SetStatus("Pick Host on one phone, and Join on nearby friends' phones.");
        }

        public void StartHostWizard(int port = 0, long seed = 0, bool direct = false)
        {
            _openedDirectly = direct;
            CurrentRole = FlowRole.Host;
            SubscribeNativeLink(true);
            AndroidLocalLink.SetNfcReading(false);
            AndroidLocalLink.AcquireMulticastLock();
            _headerTitle.text = "HOST HOTSPOT ROOM — STEP 2 OF 2";
            _choosePanel.gameObject.SetActive(false);
            _hostPanel.gameObject.SetActive(true);
            _guestPanel.gameObject.SetActive(false);

            if (AndroidLocalLink.IsNativePluginAvailable &&
                !AndroidLocalLink.IsHotspotActive(_netRules) &&
                !AndroidLocalLink.IsWifiConnected(_netRules))
            {
                AndroidLocalLink.StartHotspot();
            }

            AppSession session = AppSession.Current;
            session.StopDiscovery();
            session.Mode = GameMode.HotspotHost;
            session.Intent = LobbyIntent.Create;

            bool ok = session.IsHosting || session.StartHotspotHost(_data, port, seed);
            BindClient(session.Client);
            _hostRoomRequested = _boundClient != null && !_boundClient.Room.IsEmpty;

            RefreshNetworkBadge();
            if (!ok)
            {
                ShareUrl = "";
                SetStatus("Could not bind LAN port. Check Wi-Fi / Hotspot and tap Retry Host.");
                _hostUrlLabel.text = "Server offline";
                _hostPlayersLabel.text = "Tap Retry Host once Hotspot or Wi-Fi is active.";
                return;
            }

            if (_boundClient != null && _boundClient.IsOnline() && _boundClient.Room.IsEmpty && !_hostRoomRequested)
            {
                _hostRoomRequested = true;
                _boundClient.CreateRoom();
            }

            SetStatus(AndroidLocalLink.HasActiveHotspot
                ? "Game Hotspot \"" + AndroidLocalLink.ActiveHotspot.Ssid + "\" active! Scan QR or tap phones (NFC)."
                : "Hosting on LAN! Have friends scan the QR, tap phones (NFC), or tap your room in Join.");
            RefreshHostUi();
        }

        public void StartGuestWizard(bool direct = false)
        {
            _openedDirectly = direct;
            CurrentRole = FlowRole.Guest;
            SubscribeNativeLink(true);
            AndroidLocalLink.SetNfcReading(true);
            _headerTitle.text = "JOIN NEARBY HOTSPOT — STEP 2 OF 2";
            _choosePanel.gameObject.SetActive(false);
            _hostPanel.gameObject.SetActive(false);
            _guestPanel.gameObject.SetActive(true);

            AppSession session = AppSession.Current;
            session.StopHost();
            session.Mode = GameMode.HotspotClient;
            session.Intent = LobbyIntent.Join;

            _lastRenderedRoomCount = -1;
            _lastRenderedFirstUrl = "";
            if (_manualUrlInput != null && string.IsNullOrWhiteSpace(_manualUrlInput.text))
            {
                string fallbackHost = _netRules.FallbackHosts.Count > 0 ? _netRules.FallbackHosts[0] : "192.168.43.1";
                _manualUrlInput.text = "ws://" + fallbackHost + ":" + _netRules.DefaultPort.ToString(CultureInfo.InvariantCulture);
            }

            RefreshNetworkBadge();
            RescanHosts();
        }

        public void RescanHosts()
        {
            if (CurrentRole != FlowRole.Guest)
                CurrentRole = FlowRole.Guest;
            _lastRenderedRoomCount = -1;
            _lastRenderedFirstUrl = "";
            AppSession.Current.StartHotspotDiscovery(_data, AndroidLocalLink.GetInterfaces());
            HotspotFinder finder = AppSession.Current.Finder;
            bool nfcOn = string.Equals(AndroidLocalLink.GetNfcState(), "on", StringComparison.Ordinal);
            SetStatus(finder != null && finder.IsSearching
                ? (nfcOn ? "Scanning Wi-Fi / Hotspot for hosts (or tap phones via NFC)..." : "Scanning Wi-Fi / Hotspot for Kalyeah hosts...")
                : HotspotFinder.NoRoom);
            RefreshGuestUi(force: true);
        }

        public bool JoinHostUrl(string rawUrl)
        {
            string cleaned = NormalizeLanUrl(rawUrl, _netRules.DefaultPort);
            if (string.IsNullOrEmpty(cleaned))
            {
                SetStatus("Enter a valid LAN host (for example ws://192.168.43.1:8080).");
                return false;
            }

            if (!AppSession.Current.ConnectHotspotGuest(_data, cleaned))
            {
                SetStatus("Cannot connect to " + cleaned + ". Check Wi-Fi and try again.");
                return false;
            }

            AndroidLocalLink.SetNfcReading(false);
            BindClient(AppSession.Current.Client);
            SetStatus("Connecting to " + cleaned + "...");
            SaveSessionPrefs();
            OnEnterLobby?.Invoke();
            return true;
        }

        public bool AcceptScannedPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetStatus("Empty QR code. Try scanning again or enter ws:// URL.");
                return false;
            }

            string trimmed = payload.Trim();
            WifiQr.Network wifi = WifiQr.Parse(trimmed);
            if (wifi != null)
            {
                if (WifiJoinRequester != null)
                {
                    SetStatus("Joining Wi-Fi \"" + wifi.Ssid + "\"...");
                    WifiJoinRequester.Invoke(wifi);
                    return true;
                }
                if (AndroidLocalLink.IsNativePluginAvailable)
                {
                    SetStatus("Joining Wi-Fi \"" + wifi.Ssid + "\"...");
                    AndroidLocalLink.JoinWifi(wifi);
                    return true;
                }
                SetStatus("Wi-Fi \"" + wifi.Ssid + "\" read — connect to it in Settings, then tap Scan Again.");
                return false;
            }

            if (_manualUrlInput != null)
                _manualUrlInput.text = trimmed;
            return JoinHostUrl(trimmed);
        }

        public void Tick(float dt)
        {
            if (dt < 0f) dt = 0f;
            AndroidLocalLink.PumpPendingCallbacks();
            AppSession.Current.PollNetwork(dt);

            if (CurrentRole == FlowRole.Host)
            {
                GameClient client = AppSession.Current.Client;
                if (client != _boundClient) BindClient(client);
                if (client != null && client.IsOnline() && client.Room.IsEmpty && !_hostRoomRequested)
                {
                    _hostRoomRequested = true;
                    client.CreateRoom();
                }
                RefreshHostUi();
            }
            else if (CurrentRole == FlowRole.Guest)
            {
                RefreshGuestUi(force: false);
            }
        }

        public bool HandleBack()
        {
            if (CurrentRole != FlowRole.ChooseRole && !_openedDirectly)
            {
                if (CurrentRole == FlowRole.Guest)
                {
                    AndroidLocalLink.SetNfcReading(false);
                    AndroidLocalLink.LeaveWifi();
                    AppSession.Current.StopDiscovery();
                }
                else if (CurrentRole == FlowRole.Host)
                {
                    AndroidLocalLink.StopHotspot();
                    AndroidLocalLink.ReleaseMulticastLock();
                    AppSession.Current.StopAllNetwork();
                }
                ShowChooseRole();
                return true;
            }

            if (CurrentRole == FlowRole.Guest)
            {
                AndroidLocalLink.LeaveLocalSession();
                AppSession.Current.StopDiscovery();
            }
            else if (CurrentRole == FlowRole.Host)
            {
                AndroidLocalLink.LeaveLocalSession();
                AppSession.Current.StopAllNetwork();
            }

            OnBack?.Invoke();
            return true;
        }

        public void Dispose()
        {
            SubscribeViewport(false);
            SubscribeNativeLink(false);
            AndroidLocalLink.SetNfcReading(false);
            BindClient(null);
            ReleaseOwnedQrSprite();
            _lastQrPayload = "";
            _lastNfcPayload = "";
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

        void OnSimulatedViewportChanged() => ApplyResponsiveMetrics(_frame);

        void BuildChoosePanel(RectTransform parent)
        {
            Text subtitle = Ui.MakeText(
                "Subtitle",
                parent,
                "Play 5v5 Agawang Base or Larong Kalye together over phone Hotspot or shared Wi-Fi — zero mobile data needed!",
                19,
                SubtleText,
                TextAnchor.MiddleCenter);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.verticalOverflow = VerticalWrapMode.Truncate;
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 12;
            subtitle.resizeTextMaxSize = 20;
            SyncOutlineForColor(subtitle, SubtleText);
            _chooseSubtitleRect = subtitle.rectTransform;

            _hostRoleCard = Ui.Panel("HostRoleCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_hostRoleCard, SubCardBg, SignalVermilion);

            Text hostHeader = Ui.MakeText("HostTitle", _hostRoleCard, "1. HOST ROOM", 28, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            hostHeader.resizeTextForBestFit = true;
            hostHeader.resizeTextMinSize = 18;
            hostHeader.resizeTextMaxSize = 30;
            SyncOutlineForColor(hostHeader, PaperWhite);
            hostHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            hostHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            hostHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            hostHeader.rectTransform.offsetMin = new Vector2(16f, -62f);
            hostHeader.rectTransform.offsetMax = new Vector2(-16f, -14f);

            Text hostDesc = Ui.MakeText(
                "HostDesc",
                _hostRoleCard,
                "Turn on Personal Hotspot or Wi-Fi on this phone.\nStarts the 30 Hz authoritative room server + UDP beacon & QR code.",
                19,
                PaperWhite,
                TextAnchor.MiddleCenter);
            hostDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            hostDesc.verticalOverflow = VerticalWrapMode.Truncate;
            hostDesc.resizeTextForBestFit = true;
            hostDesc.resizeTextMinSize = 12;
            hostDesc.resizeTextMaxSize = 20;
            SyncOutlineForColor(hostDesc, PaperWhite);
            _hostRoleDescRect = hostDesc.rectTransform;

            Button startHostBtn = MakeActionButton(
                "StartHostWizardButton",
                _hostRoleCard,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(300f, 96f),
                "HOST HOTSPOT",
                SignalVermilion,
                PaperWhite,
                22,
                () => StartHostWizard(),
                primaryCta: true);
            _startHostBtnRect = startHostBtn.GetComponent<RectTransform>();

            _guestRoleCard = Ui.Panel("GuestRoleCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_guestRoleCard, SubCardBg, PaperWhite);

            Text guestHeader = Ui.MakeText("GuestTitle", _guestRoleCard, "2. JOIN FRIEND", 28, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            guestHeader.resizeTextForBestFit = true;
            guestHeader.resizeTextMinSize = 18;
            guestHeader.resizeTextMaxSize = 30;
            SyncOutlineForColor(guestHeader, PaperWhite);
            guestHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            guestHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            guestHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            guestHeader.rectTransform.offsetMin = new Vector2(16f, -62f);
            guestHeader.rectTransform.offsetMax = new Vector2(-16f, -14f);

            Text guestDesc = Ui.MakeText(
                "GuestDesc",
                _guestRoleCard,
                "Connect to your friend's Hotspot or same Wi-Fi.\nAuto-discovers nearby Kalyeah rooms, or join via QR / ws:// IP.",
                19,
                PaperWhite,
                TextAnchor.MiddleCenter);
            guestDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            guestDesc.verticalOverflow = VerticalWrapMode.Truncate;
            guestDesc.resizeTextForBestFit = true;
            guestDesc.resizeTextMinSize = 12;
            guestDesc.resizeTextMaxSize = 20;
            SyncOutlineForColor(guestDesc, PaperWhite);
            _guestRoleDescRect = guestDesc.rectTransform;

            Button startGuestBtn = MakeActionButton(
                "StartGuestWizardButton",
                _guestRoleCard,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(300f, 96f),
                "FIND ROOMS",
                PaperWhite,
                InkDark,
                22,
                () => StartGuestWizard());
            _startGuestBtnRect = startGuestBtn.GetComponent<RectTransform>();

            Button recheckBtn = MakeActionButton(
                "RecheckNetButton",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 2f),
                new Vector2(256f, 96f),
                "RECHECK WI-FI",
                MutedBtn,
                PaperWhite,
                19,
                RefreshNetworkBadge);
            _recheckBtnRect = recheckBtn.GetComponent<RectTransform>();
        }

        void BuildHostPanel(
            RectTransform parent,
            out Text urlLabel,
            out Text statusLabel,
            out Text playersLabel,
            out Image qrImage,
            out Text qrCaption)
        {
            _hostInfoCard = Ui.Panel("HostInfoCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_hostInfoCard, SubCardBg, SignalVermilion);

            Text stepTag = Ui.MakeText("HostStepTitle", _hostInfoCard, "SHARE YOUR LAN ROOM", 26, PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            stepTag.resizeTextForBestFit = true;
            stepTag.resizeTextMinSize = 18;
            stepTag.resizeTextMaxSize = 28;
            SyncOutlineForColor(stepTag, PaperWhite);
            _hostStepTitleRect = stepTag.rectTransform;

            statusLabel = Ui.MakeText("HostStatus", _hostInfoCard, "Starting local server...", 19, SubtleText, TextAnchor.UpperLeft);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            statusLabel.resizeTextForBestFit = true;
            statusLabel.resizeTextMinSize = 12;
            statusLabel.resizeTextMaxSize = 19;
            SyncOutlineForColor(statusLabel, SubtleText);
            _hostStatusRect = statusLabel.rectTransform;

            _urlBoxRect = Ui.Panel("UrlBox", _hostInfoCard, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 112f));
            StyleCard(_urlBoxRect, SurfaceDark, OkText);

            urlLabel = Ui.MakeText("LanUrlText", _urlBoxRect, "ws://192.168.43.1:8080", 22, OkText, TextAnchor.MiddleLeft);
            urlLabel.resizeTextForBestFit = true;
            urlLabel.resizeTextMinSize = 12;
            urlLabel.resizeTextMaxSize = 24;
            urlLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            urlLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(urlLabel, OkText);
            _hostUrlRect = urlLabel.rectTransform;

            Button copyBtn = MakeActionButton(
                "CopyUrlButton",
                _urlBoxRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f),
                new Vector2(140f, 96f),
                "COPY URL",
                MutedBtn,
                PaperWhite,
                19,
                CopyShareUrl);
            _copyUrlBtnRect = copyBtn.GetComponent<RectTransform>();

            playersLabel = Ui.MakeText("HostPlayers", _hostInfoCard, "Room preparing...", 19, PaperWhite, TextAnchor.MiddleLeft);
            playersLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            playersLabel.verticalOverflow = VerticalWrapMode.Truncate;
            playersLabel.resizeTextForBestFit = true;
            playersLabel.resizeTextMinSize = 12;
            playersLabel.resizeTextMaxSize = 19;
            SyncOutlineForColor(playersLabel, PaperWhite);
            _hostPlayersRect = playersLabel.rectTransform;

            Button retryBtn = MakeActionButton(
                "RetryHostButton",
                _hostInfoCard,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(20f, 16f),
                new Vector2(180f, 96f),
                "RETRY HOST",
                MutedBtn,
                PaperWhite,
                20,
                () => StartHostWizard(direct: _openedDirectly));
            _retryHostBtnRect = retryBtn.GetComponent<RectTransform>();

            Button enterBtn = MakeActionButton(
                "EnterLobbyButton",
                _hostInfoCard,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-20f, 16f),
                new Vector2(300f, 96f),
                "OPEN ROOM LOBBY",
                SignalVermilion,
                PaperWhite,
                21,
                EnterHostLobby,
                primaryCta: true);
            _enterLobbyBtnRect = enterBtn.GetComponent<RectTransform>();

            _qrCard = Ui.Panel("QrCard", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_qrCard, SubCardBg, PaperWhite);

            Text qrHeader = Ui.MakeText("QrTitle", _qrCard, "SCAN TO JOIN", 24, PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            qrHeader.resizeTextForBestFit = true;
            qrHeader.resizeTextMinSize = 18;
            qrHeader.resizeTextMaxSize = 26;
            SyncOutlineForColor(qrHeader, PaperWhite);
            _qrTitleRect = qrHeader.rectTransform;

            _qrPlateRect = Ui.Panel("QrWhitePlate", _qrCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(248f, 248f));
            Image whiteBg = Ui.MakeImage("WhiteBg", _qrPlateRect, Color.white);
            ApplySlicedPlate(whiteBg, Color.white);
            Stretch(whiteBg.rectTransform);

            qrImage = Ui.MakeImage("QrSprite", _qrPlateRect, Color.white);
            qrImage.preserveAspect = true;
            Stretch(qrImage.rectTransform, 12f, 12f, 12f, 12f);

            qrCaption = Ui.MakeText("QrCaption", _qrCard, "Friends on your Hotspot or Wi-Fi scan this QR or tap Find Rooms.", 18, SubtleText, TextAnchor.MiddleCenter);
            qrCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
            qrCaption.verticalOverflow = VerticalWrapMode.Truncate;
            qrCaption.resizeTextForBestFit = true;
            qrCaption.resizeTextMinSize = 12;
            qrCaption.resizeTextMaxSize = 18;
            SyncOutlineForColor(qrCaption, SubtleText);
            _qrCaptionRect = qrCaption.rectTransform;
        }

        void BuildGuestPanel(
            RectTransform parent,
            out Text statusLabel,
            out InputField manualInput,
            out Text emptyLabel)
        {
            _guestListCard = Ui.Panel("DiscoveredHostsCard", parent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(_guestListCard, SubCardBg, PaperWhite);

            statusLabel = Ui.MakeText("GuestStatus", _guestListCard, "Scanning nearby Wi-Fi...", 20, GoldCta, TextAnchor.MiddleLeft);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            statusLabel.resizeTextForBestFit = true;
            statusLabel.resizeTextMinSize = 12;
            statusLabel.resizeTextMaxSize = 21;
            SyncOutlineForColor(statusLabel, GoldCta);
            _guestStatusRect = statusLabel.rectTransform;

            Button scanQrBtn = MakeActionButton(
                "ScanQrButton",
                _guestListCard,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-198f, -10f),
                new Vector2(156f, 96f),
                "SCAN QR",
                SignalVermilion,
                PaperWhite,
                19,
                RequestQrScan,
                primaryCta: true);
            _scanQrBtnRect = scanQrBtn.GetComponent<RectTransform>();

            Button rescanBtn = MakeActionButton(
                "RescanButton",
                _guestListCard,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -10f),
                new Vector2(168f, 96f),
                "SCAN AGAIN",
                PaperWhite,
                InkDark,
                19,
                RescanHosts);
            _rescanBtnRect = rescanBtn.GetComponent<RectTransform>();

            _rowsScrollRect = Ui.Panel("RowsArea", _guestListCard, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image scrollHit = _rowsScrollRect.gameObject.AddComponent<Image>();
            ApplyGlassSurface(scrollHit, SurfaceDark, new Color(0.835f, 0.875f, 0.914f, 0.16f), Color.clear, 4f, 0f);
            scrollHit.raycastTarget = true;
            _rowsScrollRect.gameObject.AddComponent<RectMask2D>();

            var scroll = _rowsScrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = _rowsScrollRect;

            RectTransform content = Ui.Panel("Content", _rowsScrollRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
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

            emptyLabel = Ui.MakeText(
                "EmptyRoomsHint",
                _rowsScrollRect,
                "Looking for nearby Kalyeah Hotspot rooms...\nAsk the host to stay on Step 2 or inside the Room Lobby.",
                20,
                SubtleText,
                TextAnchor.MiddleCenter);
            emptyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            emptyLabel.verticalOverflow = VerticalWrapMode.Truncate;
            emptyLabel.resizeTextForBestFit = true;
            emptyLabel.resizeTextMinSize = 12;
            emptyLabel.resizeTextMaxSize = 20;
            SyncOutlineForColor(emptyLabel, SubtleText);
            Stretch(emptyLabel.rectTransform, 24f, 12f, 24f, 12f);

            for (int i = 0; i < MaxDiscoveredRows; i++)
            {
                int captured = i;
                RectTransform rowRect = Ui.Panel(
                    "HostRow_" + i.ToString(CultureInfo.InvariantCulture),
                    content,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(0f, 96f));
                Image bg = rowRect.gameObject.AddComponent<Image>();
                ApplyGlassSurface(bg, MutedBtn, TitaniumBorder, OkText, 4f, 3f);
                bg.raycastTarget = true;

                Button btn = rowRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnDiscoveredRowClicked(captured));
                UiShaders.AttachPressSpring(btn, 0.96f, 2f);

                var le = rowRect.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 96f;
                le.preferredHeight = 96f;
                le.flexibleWidth = 1f;
                _discoveredRowLayouts[i] = le;

                Text rowTitle = Ui.MakeText("HostName", rowRect, "", 20, PaperWhite, TextAnchor.MiddleLeft);
                rowTitle.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(rowTitle, PaperWhite);
                rowTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
                rowTitle.rectTransform.anchorMax = new Vector2(0.56f, 1f);
                rowTitle.rectTransform.offsetMin = new Vector2(18f, 4f);
                rowTitle.rectTransform.offsetMax = new Vector2(-8f, -4f);

                Text rowUrl = Ui.MakeText("HostUrl", rowRect, "", 18, OkText, TextAnchor.MiddleRight);
                rowUrl.verticalOverflow = VerticalWrapMode.Truncate;
                SyncOutlineForColor(rowUrl, OkText);
                rowUrl.rectTransform.anchorMin = new Vector2(0.58f, 0f);
                rowUrl.rectTransform.anchorMax = new Vector2(1f, 1f);
                rowUrl.rectTransform.offsetMin = new Vector2(8f, 4f);
                rowUrl.rectTransform.offsetMax = new Vector2(-18f, -4f);

                rowRect.gameObject.SetActive(false);
                _discoveredRows[i] = new DiscoveredRow
                {
                    Root = rowRect,
                    Button = btn,
                    Title = rowTitle,
                    UrlLabel = rowUrl,
                    TargetUrl = "",
                };
            }

            // Bottom manual ws:// join bar
            _manualBarRect = Ui.Panel("ManualUrlBar", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 116f));
            StyleCard(_manualBarRect, SubCardBg);

            Text manualLabel = Ui.MakeText("ManualPrompt", _manualBarRect, "DIRECT URL:", 18, SubtleText, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            manualLabel.resizeTextForBestFit = true;
            manualLabel.resizeTextMinSize = 15;
            manualLabel.resizeTextMaxSize = 19;
            SyncOutlineForColor(manualLabel, SubtleText);
            _manualPromptRect = manualLabel.rectTransform;

            manualInput = MakeInputField(
                "ManualWsInput",
                _manualBarRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                "ws://192.168.43.1:8080",
                20);
            _manualInputRect = manualInput.GetComponent<RectTransform>();

            Button pasteBtn = MakeActionButton(
                "PasteUrlButton",
                _manualBarRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(128f, 96f),
                "PASTE",
                MutedBtn,
                PaperWhite,
                19,
                PasteIntoManualUrl);
            _pasteUrlBtnRect = pasteBtn.GetComponent<RectTransform>();

            InputField capturedInput = manualInput;
            Button joinBtn = MakeActionButton(
                "ManualJoinButton",
                _manualBarRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(168f, 96f),
                "JOIN URL",
                SignalVermilion,
                PaperWhite,
                20,
                () => JoinHostUrl(capturedInput != null ? capturedInput.text : ""),
                primaryCta: true);
            _manualJoinBtnRect = joinBtn.GetComponent<RectTransform>();
        }

        void RefreshNetworkBadge()
        {
            List<NetInterface> interfaces = AndroidLocalLink.GetInterfaces();

            bool tether = HotspotNet.HotspotOn(interfaces, _netRules) || AndroidLocalLink.HasActiveHotspot;
            bool wifi = HotspotNet.WifiConnected(interfaces, _netRules);
            if (tether)
            {
                _networkBadge.text = "HOTSPOT ACTIVE";
                _networkBadge.color = OkText;
            }
            else if (wifi)
            {
                _networkBadge.text = "WI-FI CONNECTED";
                _networkBadge.color = OkText;
            }
            else
            {
                string customIp = LanIpProvider?.Invoke();
                string trimmedIp = customIp != null ? customIp.Trim() : "";
                if (trimmedIp.Length > 0 && !string.Equals(trimmedIp, "127.0.0.1", StringComparison.Ordinal))
                {
                    _networkBadge.text = "LAN: " + trimmedIp;
                    _networkBadge.color = OkText;
                }
                else
                {
                    _networkBadge.text = "CHECK WI-FI / HOTSPOT";
                    _networkBadge.color = WarnText;
                }
            }
        }

        void RefreshHostUi()
        {
            AppSession session = AppSession.Current;
            int port = ResolvePort(session.HostEndpoint, _netRules.DefaultPort);
            string lanIp = ResolveShareLanIp();
            ShareUrl = "ws://" + lanIp + ":" + port.ToString(CultureInfo.InvariantCulture);
            _hostUrlLabel.text = ShareUrl;

            GameClient client = session.Client;
            if (client != null && !client.Room.IsEmpty)
            {
                int count = client.Room.GetArr("members").Count;
                string code = client.Room.GetStr("code", "");
                _hostPlayersLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Room Ready ({0}/10 players{1}) — friends can join now!",
                    count,
                    string.IsNullOrEmpty(code) ? "" : " • " + code);
            }
            else if (session.IsHosting)
            {
                _hostPlayersLabel.text = "Starting local room...";
            }

            string qrPayload = AndroidLocalLink.HasActiveHotspot ? AndroidLocalLink.ShareWifiQrText() : ShareUrl;
            if (!string.Equals(_lastNfcPayload, qrPayload, StringComparison.Ordinal))
            {
                _lastNfcPayload = qrPayload;
                AndroidLocalLink.NfcShare(qrPayload);
            }

            if (!string.Equals(_lastQrPayload, qrPayload, StringComparison.Ordinal))
            {
                _lastQrPayload = qrPayload;
                ReleaseOwnedQrSprite();
                Sprite customQr = QrSpriteProvider?.Invoke(qrPayload);
                if (customQr != null)
                {
                    if (!ReferenceEquals(customQr, _fallbackQrSprite))
                        _ownedQrSprite = customQr;
                    _qrImage.sprite = customQr;
                    _qrCaption.text = AndroidLocalLink.HasActiveHotspot
                        ? "Scan QR or tap phones (NFC) to join \"" + AndroidLocalLink.ActiveHotspot.Ssid + "\""
                        : "Scan with Kalyeah QR camera to join " + ShareUrl;
                }
                else
                {
                    _qrImage.sprite = FallbackQrSprite();
                    _qrCaption.text = AndroidLocalLink.HasActiveHotspot
                        ? "Hotspot \"" + AndroidLocalLink.ActiveHotspot.Ssid + "\" ready (scan QR or tap NFC)"
                        : "LAN URL ready: " + ShareUrl + " (tap Copy URL or Find Rooms)";
                }
            }
        }

        void RefreshGuestUi(bool force)
        {
            HotspotFinder finder = AppSession.Current.Finder;
            if (finder == null)
            {
                if (force)
                {
                    _emptyListLabel.gameObject.SetActive(true);
                    _emptyListLabel.text = HotspotFinder.NoRoom;
                    for (int i = 0; i < MaxDiscoveredRows; i++)
                        _discoveredRows[i].Root.gameObject.SetActive(false);
                }
                return;
            }

            int count = finder.Rooms.Count;
            string firstUrl = count > 0 ? finder.Rooms[0].Url : "";
            if (finder.IsSearching)
            {
                SetStatus(count > 0 ? "Tap a host room to join (still scanning...)" : "Scanning Wi-Fi / Hotspot for Kalyeah hosts...");
            }
            else if (count > 0)
            {
                SetStatus("Tap a discovered LAN host below to enter the room!");
            }
            else if (finder.Failed)
            {
                SetStatus(string.IsNullOrEmpty(finder.FailureMessage) ? HotspotFinder.NoRoom : finder.FailureMessage);
            }

            if (!force && count == _lastRenderedRoomCount && string.Equals(firstUrl, _lastRenderedFirstUrl, StringComparison.Ordinal))
                return;

            _lastRenderedRoomCount = count;
            _lastRenderedFirstUrl = firstUrl;
            _emptyListLabel.gameObject.SetActive(count == 0);
            if (count == 0)
            {
                _emptyListLabel.text = finder.IsSearching
                    ? "Looking for nearby Kalyeah Hotspot rooms...\nOr scan the host's QR / enter ws:// URL below."
                    : (string.IsNullOrEmpty(finder.FailureMessage) ? HotspotFinder.NoRoom : finder.FailureMessage);
            }

            for (int i = 0; i < MaxDiscoveredRows; i++)
            {
                if (i < count)
                {
                    HotspotFinder.FoundRoom found = finder.Rooms[i];
                    string hostName = string.IsNullOrWhiteSpace(found.Host) ? "Kalyeah Host" : found.Host;
                    int cap = found.Size > 0 ? found.Size : RoomService.MaxTeamSize * 2;
                    _discoveredRows[i].TargetUrl = found.Url;
                    _discoveredRows[i].Title.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}'s Room   ({1}/{2} Players)",
                        hostName,
                        found.Players,
                        cap);
                    _discoveredRows[i].UrlLabel.text = found.Url + "   [JOIN ->]";
                    _discoveredRows[i].Root.gameObject.SetActive(true);
                }
                else
                {
                    _discoveredRows[i].TargetUrl = "";
                    _discoveredRows[i].Root.gameObject.SetActive(false);
                }
            }
        }

        void OnDiscoveredRowClicked(int index)
        {
            if (index < 0 || index >= MaxDiscoveredRows) return;
            string url = _discoveredRows[index].TargetUrl;
            if (!string.IsNullOrEmpty(url))
                JoinHostUrl(url);
        }

        void EnterHostLobby()
        {
            AppSession session = AppSession.Current;
            if (!session.IsHosting)
            {
                if (!session.StartHotspotHost(_data))
                {
                    SetStatus("Start Hotspot or connect Wi-Fi first, then tap Retry Host.");
                    return;
                }
                BindClient(session.Client);
            }
            session.Mode = GameMode.HotspotHost;
            session.Intent = LobbyIntent.Create;
            SaveSessionPrefs();
            OnEnterLobby?.Invoke();
        }

        void CopyShareUrl()
        {
            if (string.IsNullOrEmpty(ShareUrl)) return;
            GUIUtility.systemCopyBuffer = ShareUrl;
            SetStatus("Copied " + ShareUrl + " to clipboard!");
        }

        void PasteIntoManualUrl()
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip))
            {
                SetStatus("Clipboard is empty. Copy the host's ws:// URL first.");
                return;
            }
            AcceptScannedPayload(clip);
        }

        void RequestQrScan()
        {
            if (QrScanRequester != null)
            {
                QrScanRequester.Invoke(payload => AcceptScannedPayload(payload));
                return;
            }
            PasteIntoManualUrl();
        }

        void SubscribeNativeLink(bool subscribe)
        {
            if (subscribe == _nativeSubscribed) return;
            _nativeSubscribed = subscribe;
            if (subscribe)
            {
                AndroidLocalLink.HotspotStarted += OnNativeHotspotStarted;
                AndroidLocalLink.HotspotFailed += OnNativeHotspotFailed;
                AndroidLocalLink.HotspotStopped += OnNativeHotspotStopped;
                AndroidLocalLink.WifiJoined += OnNativeWifiJoined;
                AndroidLocalLink.WifiFailed += OnNativeWifiFailed;
                AndroidLocalLink.WifiLost += OnNativeWifiLost;
                AndroidLocalLink.NfcRead += OnNativeNfcRead;
            }
            else
            {
                AndroidLocalLink.HotspotStarted -= OnNativeHotspotStarted;
                AndroidLocalLink.HotspotFailed -= OnNativeHotspotFailed;
                AndroidLocalLink.HotspotStopped -= OnNativeHotspotStopped;
                AndroidLocalLink.WifiJoined -= OnNativeWifiJoined;
                AndroidLocalLink.WifiFailed -= OnNativeWifiFailed;
                AndroidLocalLink.WifiLost -= OnNativeWifiLost;
                AndroidLocalLink.NfcRead -= OnNativeNfcRead;
            }
        }

        void OnNativeHotspotStarted(WifiQr.Network net)
        {
            if (CurrentRole != FlowRole.Host) return;
            AppSession session = AppSession.Current;
            if (!session.IsHosting && session.StartHotspotHost(_data))
            {
                BindClient(session.Client);
                _hostRoomRequested = _boundClient != null && !_boundClient.Room.IsEmpty;
                if (_boundClient != null && _boundClient.IsOnline() && _boundClient.Room.IsEmpty && !_hostRoomRequested)
                {
                    _hostRoomRequested = true;
                    _boundClient.CreateRoom();
                }
            }
            RefreshNetworkBadge();
            SetStatus("Game Hotspot \"" + (net != null ? net.Ssid : "") + "\" active! Scan QR or tap phones (NFC).");
            RefreshHostUi();
        }

        void OnNativeHotspotFailed(string reason)
        {
            if (CurrentRole != FlowRole.Host) return;
            RefreshNetworkBadge();
            if (!AndroidLocalLink.IsWifiConnected(_netRules))
                SetStatus(AndroidLocalLink.DescribeHotspotFailure(reason));
        }

        void OnNativeHotspotStopped()
        {
            if (CurrentRole != FlowRole.Host) return;
            RefreshNetworkBadge();
            RefreshHostUi();
        }

        void OnNativeWifiJoined()
        {
            if (CurrentRole != FlowRole.Guest) return;
            RefreshNetworkBadge();
            SetStatus("Joined Hotspot! Scanning for host room...");
            RescanHosts();
        }

        void OnNativeWifiFailed(string reason)
        {
            if (CurrentRole != FlowRole.Guest) return;
            SetStatus(AndroidLocalLink.DescribeWifiFailure(reason));
        }

        void OnNativeWifiLost()
        {
            if (CurrentRole != FlowRole.Guest) return;
            RefreshNetworkBadge();
            SetStatus("Friend's Hotspot disconnected — scan QR or tap Scan Again.");
        }

        void OnNativeNfcRead(string payload)
        {
            if (CurrentRole != FlowRole.Guest || string.IsNullOrWhiteSpace(payload)) return;
            AcceptScannedPayload(payload);
        }

        void BindClient(GameClient client)
        {
            if (ReferenceEquals(_boundClient, client)) return;
            if (_boundClient != null)
            {
                _boundClient.Welcomed -= OnClientWelcomed;
                _boundClient.RoomChanged -= OnClientRoomChanged;
                _boundClient.ErrorReceived -= OnClientError;
            }
            _boundClient = client;
            if (_boundClient != null)
            {
                _boundClient.Welcomed += OnClientWelcomed;
                _boundClient.RoomChanged += OnClientRoomChanged;
                _boundClient.ErrorReceived += OnClientError;
            }
        }

        void OnClientWelcomed(bool reconnected)
        {
            if (CurrentRole == FlowRole.Host && _boundClient != null && _boundClient.Room.IsEmpty && !_hostRoomRequested)
            {
                _hostRoomRequested = true;
                _boundClient.CreateRoom();
            }
        }

        void OnClientRoomChanged(GDict room)
        {
            if (CurrentRole == FlowRole.Host)
                RefreshHostUi();
        }

        void OnClientError(string err)
        {
            if (!string.IsNullOrEmpty(err))
                SetStatus(err);
        }

        void SetStatus(string message)
        {
            StatusText = message ?? "";
            if (_hostStatusLabel != null && CurrentRole == FlowRole.Host)
                _hostStatusLabel.text = StatusText;
            if (_guestStatusLabel != null && CurrentRole == FlowRole.Guest)
                _guestStatusLabel.text = StatusText;
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
            if (_qrImage != null && ReferenceEquals(_qrImage.sprite, sprite))
                _qrImage.sprite = null;
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

        static int ResolvePort(string wsEndpoint, int fallbackPort)
        {
            if (!string.IsNullOrWhiteSpace(wsEndpoint) && Uri.TryCreate(wsEndpoint, UriKind.Absolute, out Uri uri) && uri.Port > 0)
                return uri.Port;
            return fallbackPort > 0 ? fallbackPort : 8080;
        }

        static string NormalizeLanUrl(string raw, int defaultPort)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string trimmed = raw.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                trimmed = "ws://" + trimmed.Substring("http://".Length);
            else if (!trimmed.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                     !trimmed.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                trimmed = "ws://" + trimmed;

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri parsed))
            {
                if (parsed.Port <= 0 || trimmed.IndexOf(':', parsed.Scheme.Length + 3) < 0)
                    return parsed.Scheme + "://" + parsed.Host + ":" + (defaultPort > 0 ? defaultPort : 8080).ToString(CultureInfo.InvariantCulture);
                return trimmed;
            }
            return "";
        }

        void ApplyResponsiveMetrics(RectTransform frame)
        {
            if (_inLayout || frame == null) return;
            _inLayout = true;
            try
            {
                if (_canvas == null && Root != null)
                    _canvas = Root.GetComponentInParent<Canvas>();

                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float minTouch = ScreenFitMetrics.ClampTouchSize(48f, ScreenFitMetrics.MinTouchDp, _canvas);
                float gutter = Mathf.Max(14f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));

                float frameW = Mathf.Clamp(safe.x - 24f, Mathf.Min(safe.x - 12f, 620f), 1220f);
                float frameH = Mathf.Clamp(safe.y - 16f, Mathf.Min(safe.y - 8f, 500f), 668f);
                frame.sizeDelta = new Vector2(frameW, frameH);

                float padX = 18f;
                float topH = Mathf.Max(108f, minTouch + 12f);
                if (_topBar != null)
                {
                    _topBar.anchorMin = new Vector2(0f, 1f);
                    _topBar.anchorMax = new Vector2(1f, 1f);
                    _topBar.pivot = new Vector2(0.5f, 1f);
                    _topBar.offsetMin = new Vector2(padX, -(6f + topH));
                    _topBar.offsetMax = new Vector2(-padX, -6f);
                }

                float backW = Mathf.Max(minTouch, 136f);
                if (_backBtnRect != null)
                    _backBtnRect.sizeDelta = new Vector2(backW, minTouch);

                float badgeW = Mathf.Clamp((frameW - padX * 2f) * 0.23f, 176f, 232f);
                if (_netBadgePlate != null)
                    _netBadgePlate.sizeDelta = new Vector2(badgeW, Mathf.Min(topH - 16f, 58f));
                if (_headerTitle != null)
                    Stretch(_headerTitle.rectTransform, backW + gutter, 6f, badgeW + gutter, 6f);

                float contentTop = topH + 14f;
                float contentBottom = 16f;
                float contentW = Mathf.Max(320f, frameW - padX * 2f);
                float contentH = Mathf.Max(260f, frameH - contentTop - contentBottom);

                SetContentPanelBounds(_choosePanel, padX, contentBottom, contentTop);
                SetContentPanelBounds(_hostPanel, padX, contentBottom, contentTop);
                SetContentPanelBounds(_guestPanel, padX, contentBottom, contentTop);

                // Step 1: Choose Role
                if (_chooseSubtitleRect != null)
                {
                    _chooseSubtitleRect.anchorMin = new Vector2(0f, 1f);
                    _chooseSubtitleRect.anchorMax = new Vector2(1f, 1f);
                    _chooseSubtitleRect.pivot = new Vector2(0.5f, 1f);
                    _chooseSubtitleRect.offsetMin = new Vector2(16f, -50f);
                    _chooseSubtitleRect.offsetMax = new Vector2(-16f, -2f);
                }
                if (_recheckBtnRect != null)
                {
                    _recheckBtnRect.anchoredPosition = new Vector2(0f, 2f);
                    _recheckBtnRect.sizeDelta = new Vector2(Mathf.Max(minTouch, 256f), minTouch);
                }
                float roleCardW = Mathf.Max(minTouch + 40f, (contentW - gutter) * 0.5f);
                float roleCardBottom = minTouch + 16f;
                float roleCardTop = 56f;
                LayoutRoleCard(_hostRoleCard, _hostRoleDescRect, _startHostBtnRect, 0f, roleCardW, roleCardBottom, roleCardTop, minTouch);
                LayoutRoleCard(_guestRoleCard, _guestRoleDescRect, _startGuestBtnRect, roleCardW + gutter, roleCardW, roleCardBottom, roleCardTop, minTouch);

                // Step 2A: Host Wizard
                float hostLeftW = Mathf.Round((contentW - gutter) * 0.58f);
                float hostRightW = Mathf.Max(240f, contentW - gutter - hostLeftW);
                if (_hostInfoCard != null)
                {
                    _hostInfoCard.anchorMin = new Vector2(0f, 0f);
                    _hostInfoCard.anchorMax = new Vector2(0f, 1f);
                    _hostInfoCard.pivot = new Vector2(0f, 0.5f);
                    _hostInfoCard.offsetMin = new Vector2(0f, 0f);
                    _hostInfoCard.offsetMax = new Vector2(hostLeftW, 0f);
                }
                float innerLeftPad = 20f;
                float innerLeftW = Mathf.Max(220f, hostLeftW - innerLeftPad * 2f);
                if (_hostStepTitleRect != null)
                {
                    _hostStepTitleRect.anchorMin = new Vector2(0f, 1f);
                    _hostStepTitleRect.anchorMax = new Vector2(1f, 1f);
                    _hostStepTitleRect.pivot = new Vector2(0.5f, 1f);
                    _hostStepTitleRect.offsetMin = new Vector2(innerLeftPad, -56f);
                    _hostStepTitleRect.offsetMax = new Vector2(-innerLeftPad, -14f);
                }
                if (_hostStatusRect != null)
                {
                    _hostStatusRect.anchorMin = new Vector2(0f, 1f);
                    _hostStatusRect.anchorMax = new Vector2(1f, 1f);
                    _hostStatusRect.pivot = new Vector2(0.5f, 1f);
                    _hostStatusRect.offsetMin = new Vector2(innerLeftPad, -124f);
                    _hostStatusRect.offsetMax = new Vector2(-innerLeftPad, -60f);
                }
                float urlBoxH = minTouch + 16f;
                if (_urlBoxRect != null)
                {
                    _urlBoxRect.anchorMin = new Vector2(0f, 1f);
                    _urlBoxRect.anchorMax = new Vector2(1f, 1f);
                    _urlBoxRect.pivot = new Vector2(0.5f, 1f);
                    _urlBoxRect.offsetMin = new Vector2(innerLeftPad, -(132f + urlBoxH));
                    _urlBoxRect.offsetMax = new Vector2(-innerLeftPad, -132f);
                }
                float copyUrlW = Mathf.Max(minTouch, 140f);
                if (_copyUrlBtnRect != null)
                {
                    _copyUrlBtnRect.anchoredPosition = new Vector2(-8f, 0f);
                    _copyUrlBtnRect.sizeDelta = new Vector2(copyUrlW, minTouch);
                }
                if (_hostUrlRect != null)
                    Stretch(_hostUrlRect, 14f, 6f, copyUrlW + 8f + gutter, 6f);
                if (_hostPlayersRect != null)
                {
                    _hostPlayersRect.anchorMin = new Vector2(0f, 0f);
                    _hostPlayersRect.anchorMax = new Vector2(1f, 1f);
                    _hostPlayersRect.pivot = new Vector2(0.5f, 0.5f);
                    _hostPlayersRect.offsetMin = new Vector2(innerLeftPad, minTouch + 28f);
                    _hostPlayersRect.offsetMax = new Vector2(-innerLeftPad, -(132f + urlBoxH + 10f));
                }
                float availHostActions = Mathf.Max(minTouch * 2f, innerLeftW - gutter);
                float retryW = Mathf.Max(minTouch, Mathf.Round(availHostActions * 0.37f));
                float enterW = Mathf.Max(minTouch, availHostActions - retryW);
                retryW = Mathf.Max(minTouch, availHostActions - enterW);
                if (_retryHostBtnRect != null)
                {
                    _retryHostBtnRect.anchoredPosition = new Vector2(innerLeftPad, 16f);
                    _retryHostBtnRect.sizeDelta = new Vector2(retryW, minTouch);
                }
                if (_enterLobbyBtnRect != null)
                {
                    _enterLobbyBtnRect.anchoredPosition = new Vector2(-innerLeftPad, 16f);
                    _enterLobbyBtnRect.sizeDelta = new Vector2(enterW, minTouch);
                }

                if (_qrCard != null)
                {
                    _qrCard.anchorMin = new Vector2(0f, 0f);
                    _qrCard.anchorMax = new Vector2(0f, 1f);
                    _qrCard.pivot = new Vector2(0f, 0.5f);
                    _qrCard.offsetMin = new Vector2(hostLeftW + gutter, 0f);
                    _qrCard.offsetMax = new Vector2(contentW, 0f);
                }
                if (_qrTitleRect != null)
                {
                    _qrTitleRect.anchorMin = new Vector2(0f, 1f);
                    _qrTitleRect.anchorMax = new Vector2(1f, 1f);
                    _qrTitleRect.pivot = new Vector2(0.5f, 1f);
                    _qrTitleRect.offsetMin = new Vector2(14f, -54f);
                    _qrTitleRect.offsetMax = new Vector2(-14f, -14f);
                }
                if (_qrCaptionRect != null)
                {
                    _qrCaptionRect.anchorMin = new Vector2(0f, 0f);
                    _qrCaptionRect.anchorMax = new Vector2(1f, 0f);
                    _qrCaptionRect.pivot = new Vector2(0.5f, 0f);
                    _qrCaptionRect.offsetMin = new Vector2(14f, 12f);
                    _qrCaptionRect.offsetMax = new Vector2(-14f, 92f);
                }
                if (_qrPlateRect != null)
                {
                    float qrAvailH = Mathf.Max(140f, contentH - 60f - 98f);
                    float qrSize = Mathf.Clamp(Mathf.Min(hostRightW - 36f, qrAvailH - 8f), 148f, 284f);
                    float centerOffsetY = (98f - 60f) * 0.5f;
                    _qrPlateRect.anchoredPosition = new Vector2(0f, centerOffsetY);
                    _qrPlateRect.sizeDelta = new Vector2(qrSize, qrSize);
                }

                // Step 2B: Guest Wizard
                float manualBarH = minTouch + 20f;
                if (_manualBarRect != null)
                {
                    _manualBarRect.anchorMin = new Vector2(0f, 0f);
                    _manualBarRect.anchorMax = new Vector2(1f, 0f);
                    _manualBarRect.pivot = new Vector2(0.5f, 0f);
                    _manualBarRect.anchoredPosition = Vector2.zero;
                    _manualBarRect.sizeDelta = new Vector2(0f, manualBarH);
                }
                float barPad = 16f;
                float joinBtnW = Mathf.Max(minTouch, 164f);
                float pasteBtnW = Mathf.Max(minTouch, 124f);
                float promptW = 136f;
                if (_manualJoinBtnRect != null)
                {
                    _manualJoinBtnRect.anchoredPosition = new Vector2(-barPad, 0f);
                    _manualJoinBtnRect.sizeDelta = new Vector2(joinBtnW, minTouch);
                }
                if (_pasteUrlBtnRect != null)
                {
                    _pasteUrlBtnRect.anchoredPosition = new Vector2(-(barPad + joinBtnW + gutter), 0f);
                    _pasteUrlBtnRect.sizeDelta = new Vector2(pasteBtnW, minTouch);
                }
                if (_manualPromptRect != null)
                {
                    _manualPromptRect.anchorMin = new Vector2(0f, 0f);
                    _manualPromptRect.anchorMax = new Vector2(0f, 1f);
                    _manualPromptRect.pivot = new Vector2(0f, 0.5f);
                    _manualPromptRect.offsetMin = new Vector2(barPad, 6f);
                    _manualPromptRect.offsetMax = new Vector2(barPad + promptW, -6f);
                }
                if (_manualInputRect != null)
                {
                    float inputLeft = barPad + promptW + 10f;
                    float inputRight = barPad + joinBtnW + gutter + pasteBtnW + gutter;
                    float inputW = Mathf.Max(minTouch, contentW - inputLeft - inputRight);
                    _manualInputRect.anchorMin = new Vector2(0f, 0.5f);
                    _manualInputRect.anchorMax = new Vector2(0f, 0.5f);
                    _manualInputRect.pivot = new Vector2(0f, 0.5f);
                    _manualInputRect.anchoredPosition = new Vector2(inputLeft, 0f);
                    _manualInputRect.sizeDelta = new Vector2(inputW, minTouch);
                }

                if (_guestListCard != null)
                {
                    _guestListCard.anchorMin = new Vector2(0f, 0f);
                    _guestListCard.anchorMax = new Vector2(1f, 1f);
                    _guestListCard.pivot = new Vector2(0.5f, 0.5f);
                    _guestListCard.offsetMin = new Vector2(0f, manualBarH + 14f);
                    _guestListCard.offsetMax = Vector2.zero;
                }
                float guestHdrH = minTouch + 16f;
                float rescanW = Mathf.Max(minTouch, 168f);
                float scanQrW = Mathf.Max(minTouch, 152f);
                if (_rescanBtnRect != null)
                {
                    _rescanBtnRect.anchoredPosition = new Vector2(-16f, -8f);
                    _rescanBtnRect.sizeDelta = new Vector2(rescanW, minTouch);
                }
                if (_scanQrBtnRect != null)
                {
                    _scanQrBtnRect.anchoredPosition = new Vector2(-(16f + rescanW + gutter), -8f);
                    _scanQrBtnRect.sizeDelta = new Vector2(scanQrW, minTouch);
                }
                if (_guestStatusRect != null)
                {
                    _guestStatusRect.anchorMin = new Vector2(0f, 1f);
                    _guestStatusRect.anchorMax = new Vector2(1f, 1f);
                    _guestStatusRect.pivot = new Vector2(0.5f, 1f);
                    _guestStatusRect.offsetMin = new Vector2(20f, -guestHdrH);
                    _guestStatusRect.offsetMax = new Vector2(-(16f + rescanW + gutter + scanQrW + gutter), -6f);
                }
                if (_rowsScrollRect != null)
                {
                    _rowsScrollRect.anchorMin = Vector2.zero;
                    _rowsScrollRect.anchorMax = Vector2.one;
                    _rowsScrollRect.offsetMin = new Vector2(16f, 12f);
                    _rowsScrollRect.offsetMax = new Vector2(-16f, -(guestHdrH + 6f));
                }
                for (int i = 0; i < MaxDiscoveredRows; i++)
                {
                    if (_discoveredRowLayouts[i] != null)
                    {
                        _discoveredRowLayouts[i].minHeight = minTouch;
                        _discoveredRowLayouts[i].preferredHeight = minTouch;
                    }
                }
            }
            finally
            {
                _inLayout = false;
            }
        }

        static void SetContentPanelBounds(RectTransform panel, float padX, float bottom, float top)
        {
            if (panel == null) return;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = new Vector2(padX, bottom);
            panel.offsetMax = new Vector2(-padX, -top);
        }

        static void LayoutRoleCard(
            RectTransform card,
            RectTransform descRect,
            RectTransform btnRect,
            float x,
            float w,
            float bottom,
            float top,
            float minTouch)
        {
            if (card == null) return;
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 0.5f);
            card.offsetMin = new Vector2(x, bottom);
            card.offsetMax = new Vector2(x + w, -top);

            if (descRect != null)
            {
                descRect.anchorMin = Vector2.zero;
                descRect.anchorMax = Vector2.one;
                descRect.offsetMin = new Vector2(20f, minTouch + 28f);
                descRect.offsetMax = new Vector2(-20f, -66f);
            }
            if (btnRect != null)
            {
                btnRect.anchoredPosition = new Vector2(0f, 16f);
                btnRect.sizeDelta = new Vector2(Mathf.Clamp(w - 36f, minTouch, 320f), minTouch);
            }
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

        Button MakeActionButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            string caption,
            Color fill,
            Color textColor,
            int fontSize,
            UnityEngine.Events.UnityAction onClick,
            bool primaryCta = false)
        {
            Vector2 clamped = ScreenFitMetrics.ClampTouchSize(size, ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, clamped);
            Image bg = rt.gameObject.AddComponent<Image>();
            if (primaryCta)
                ApplyMetallicCtaSurface(bg, SignalVermilion, SignalVermilionDeep, 8f);
            else
                ApplyGlassSurface(bg, fill, TitaniumBorder, Color.clear, 4f, 0f);
            bg.raycastTarget = true;

            Text label = Ui.MakeText("Label", rt, caption, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(label, textColor);
            Stretch(label.rectTransform, 12f, 4f, 12f, 4f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            btn.colors = cb;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            UiShaders.AttachPressSpring(btn, 0.95f, 2f);
            _touchTargets.Add(rt);
            return btn;
        }

        InputField MakeInputField(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string placeholderText, int fontSize)
        {
            float touchH = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, touchH));
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

            Text placeholder = Ui.MakeText("Placeholder", rt, placeholderText, Mathf.Max(18, fontSize - 2), SubtleText, TextAnchor.MiddleLeft);
            placeholder.raycastTarget = false;
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            placeholder.resizeTextForBestFit = true;
            placeholder.resizeTextMinSize = 12;
            placeholder.resizeTextMaxSize = Mathf.Max(18, fontSize - 2);
            SyncOutlineForColor(placeholder, placeholder.color);
            Stretch(placeholder.rectTransform, 14f, 6f, 14f, 6f);

            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = textComp;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            return field;
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
                name = "KalyeahQrStandIn",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[n * n];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(16, 21, 28, 255);
            for (int i = 0; i < px.Length; i++) px[i] = white;

            DrawFinderBox(px, n, 1, 1);
            DrawFinderBox(px, n, n - 8, 1);
            DrawFinderBox(px, n, 1, n - 8);

            for (int y = 9; y < n - 9; y++)
            {
                for (int x = 9; x < n - 9; x++)
                {
                    if (((x * 7 + y * 13) & 3) == 0)
                        px[y * n + x] = black;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _fallbackQrSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            return _fallbackQrSprite;
        }

        static void DrawFinderBox(Color32[] px, int stride, int ox, int oy)
        {
            var black = new Color32(16, 22, 38, 255);
            for (int dy = 0; dy < 7; dy++)
            {
                for (int dx = 0; dx < 7; dx++)
                {
                    bool ring = dx == 0 || dx == 6 || dy == 0 || dy == 6;
                    bool core = dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4;
                    if (ring || core)
                        px[(oy + dy) * stride + (ox + dx)] = black;
                }
            }
        }

        [ExecuteAlways]
        sealed class HotspotFlowDriver : MonoBehaviour
        {
            HotspotFlowView _owner;

            public void Bind(HotspotFlowView owner) => _owner = owner;

            void OnEnable()
            {
                if (_owner != null)
                {
                    _owner.SubscribeViewport(true);
                    _owner.ApplyResponsiveMetrics(_owner._frame);
                }
            }

            void Update()
            {
                if (Application.isPlaying)
                    _owner?.Tick(Time.unscaledDeltaTime);
            }

            void OnRectTransformDimensionsChange()
            {
                if (_owner != null && _owner.Root != null)
                    _owner.ApplyResponsiveMetrics(_owner._frame);
            }

            void OnDestroy() => _owner?.Dispose();
        }
    }
}
