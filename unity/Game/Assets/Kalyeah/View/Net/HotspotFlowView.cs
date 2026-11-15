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

        static readonly Color CardBg = new Color(0.08f, 0.12f, 0.23f, 0.96f);
        static readonly Color SubCardBg = new Color(0.11f, 0.17f, 0.31f, 0.96f);
        static readonly Color GoldCta = new Color(0.98f, 0.78f, 0.18f, 1f);
        static readonly Color TealCta = new Color(0.12f, 0.62f, 0.55f, 1f);
        static readonly Color CoralCta = new Color(0.89f, 0.33f, 0.24f, 1f);
        static readonly Color MutedBtn = new Color(0.18f, 0.25f, 0.42f, 1f);
        static readonly Color InkDark = new Color(0.14f, 0.09f, 0.04f, 1f);
        static readonly Color SubtleText = new Color(0.78f, 0.85f, 0.96f, 1f);
        static readonly Color WarnText = new Color(1f, 0.80f, 0.36f, 1f);
        static readonly Color OkText = new Color(0.46f, 0.95f, 0.70f, 1f);

        static Sprite _fallbackQrSprite;

        readonly DataSource _data;
        readonly Canvas _canvas;
        readonly NetRules _netRules;

        readonly RectTransform _choosePanel;
        readonly RectTransform _hostPanel;
        readonly RectTransform _guestPanel;

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
            Image dim = Ui.MakeImage("Backdrop", Root, new Color(0.04f, 0.07f, 0.15f, 0.94f), raycast: true);
            Stretch(dim.rectTransform);

            RectTransform frame = Ui.Panel(
                "Frame",
                Root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1120f, 648f));
            StyleCard(frame, CardBg);

            // Top bar
            RectTransform topBar = Ui.Panel("TopBar", frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-36f, 56f));
            MakeActionButton("BackButton", topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(132f, 50f), "BACK", MutedBtn, Ui.Cream, 22, () => HandleBack());

            _headerTitle = Ui.MakeText("HeaderTitle", topBar, "LOCAL HOTSPOT PLAY", 32, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Stretch(_headerTitle.rectTransform, 150f, 0f, 260f, 0f);

            _networkBadge = Ui.MakeText("NetworkBadge", topBar, "", 18, OkText, TextAnchor.MiddleRight);
            _networkBadge.rectTransform.anchorMin = new Vector2(1f, 0f);
            _networkBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
            _networkBadge.rectTransform.pivot = new Vector2(1f, 0.5f);
            _networkBadge.rectTransform.anchoredPosition = Vector2.zero;
            _networkBadge.rectTransform.sizeDelta = new Vector2(250f, 0f);

            // Step 1: Choose Role
            _choosePanel = Ui.Panel("Step1ChooseRole", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(-44f, -92f));
            BuildChoosePanel(_choosePanel);

            // Step 2A: Host Wizard
            _hostPanel = Ui.Panel("Step2HostWizard", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(-44f, -92f));
            BuildHostPanel(_hostPanel, out _hostUrlLabel, out _hostStatusLabel, out _hostPlayersLabel, out _qrImage, out _qrCaption);

            // Step 2B: Guest Wizard
            _guestPanel = Ui.Panel("Step2GuestWizard", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(-44f, -92f));
            BuildGuestPanel(_guestPanel, out _guestStatusLabel, out _manualUrlInput, out _emptyListLabel);

            var driver = Root.gameObject.AddComponent<HotspotFlowDriver>();
            driver.Bind(this);

            SubscribeNativeLink(true);
            ApplyResponsiveMetrics(frame);
            ShowChooseRole();
        }

        public void Show()
        {
            SubscribeNativeLink(true);
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
            ApplyResponsiveMetrics(Root.Find("Frame") as RectTransform);
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
            SubscribeNativeLink(false);
            AndroidLocalLink.SetNfcReading(false);
            BindClient(null);
            ReleaseOwnedQrSprite();
            _lastQrPayload = "";
            _lastNfcPayload = "";
        }

        void BuildChoosePanel(RectTransform parent)
        {
            Text subtitle = Ui.MakeText(
                "Subtitle",
                parent,
                "Play 5v5 Agawang Base or Larong Kalye together over phone Hotspot or shared Wi-Fi — zero mobile data needed!",
                20,
                SubtleText,
                TextAnchor.MiddleCenter);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.rectTransform.anchorMin = new Vector2(0.05f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 52f);

            RectTransform hostCard = Ui.Panel("HostRoleCard", parent, new Vector2(0f, 0.14f), new Vector2(0.485f, 0.86f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(hostCard, SubCardBg);

            Text hostHeader = Ui.MakeText("HostTitle", hostCard, "1. HOST ROOM", 30, GoldCta, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            hostHeader.rectTransform.anchorMin = new Vector2(0.06f, 0.72f);
            hostHeader.rectTransform.anchorMax = new Vector2(0.94f, 0.93f);
            hostHeader.rectTransform.sizeDelta = Vector2.zero;

            Text hostDesc = Ui.MakeText(
                "HostDesc",
                hostCard,
                "Turn on Personal Hotspot or Wi-Fi on this phone.\nStarts the 30 Hz authoritative room server + UDP beacon & QR code.",
                19,
                Ui.Cream,
                TextAnchor.MiddleCenter);
            hostDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            hostDesc.rectTransform.anchorMin = new Vector2(0.08f, 0.30f);
            hostDesc.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
            hostDesc.rectTransform.sizeDelta = Vector2.zero;

            MakeActionButton(
                "StartHostWizardButton",
                hostCard,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 22f),
                new Vector2(300f, 60f),
                "HOST HOTSPOT",
                GoldCta,
                InkDark,
                24,
                () => StartHostWizard());

            RectTransform guestCard = Ui.Panel("GuestRoleCard", parent, new Vector2(0.515f, 0.14f), new Vector2(1f, 0.86f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(guestCard, SubCardBg);

            Text guestHeader = Ui.MakeText("GuestTitle", guestCard, "2. JOIN FRIEND", 30, new Color(0.45f, 0.95f, 0.88f, 1f), TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            guestHeader.rectTransform.anchorMin = new Vector2(0.06f, 0.72f);
            guestHeader.rectTransform.anchorMax = new Vector2(0.94f, 0.93f);
            guestHeader.rectTransform.sizeDelta = Vector2.zero;

            Text guestDesc = Ui.MakeText(
                "GuestDesc",
                guestCard,
                "Connect to your friend's Hotspot or same Wi-Fi.\nAuto-discovers nearby Kalyeah rooms, or join via QR / ws:// IP.",
                19,
                Ui.Cream,
                TextAnchor.MiddleCenter);
            guestDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            guestDesc.rectTransform.anchorMin = new Vector2(0.08f, 0.30f);
            guestDesc.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
            guestDesc.rectTransform.sizeDelta = Vector2.zero;

            MakeActionButton(
                "StartGuestWizardButton",
                guestCard,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 22f),
                new Vector2(300f, 60f),
                "FIND ROOMS",
                TealCta,
                Ui.Cream,
                24,
                () => StartGuestWizard());

            MakeActionButton(
                "RecheckNetButton",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 6f),
                new Vector2(240f, 48f),
                "RECHECK WI-FI",
                MutedBtn,
                Ui.Cream,
                18,
                RefreshNetworkBadge);
        }

        void BuildHostPanel(
            RectTransform parent,
            out Text urlLabel,
            out Text statusLabel,
            out Text playersLabel,
            out Image qrImage,
            out Text qrCaption)
        {
            RectTransform leftCol = Ui.Panel("HostInfoCard", parent, new Vector2(0f, 0.04f), new Vector2(0.60f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(leftCol, SubCardBg);

            Text stepTag = Ui.MakeText("HostStepTitle", leftCol, "SHARE YOUR LAN ROOM", 28, GoldCta, TextAnchor.UpperLeft, KalyeahResources.DisplayFont());
            stepTag.rectTransform.anchorMin = new Vector2(0.05f, 0.82f);
            stepTag.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            stepTag.rectTransform.sizeDelta = Vector2.zero;

            statusLabel = Ui.MakeText("HostStatus", leftCol, "Starting local server...", 19, SubtleText, TextAnchor.UpperLeft);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.rectTransform.anchorMin = new Vector2(0.05f, 0.64f);
            statusLabel.rectTransform.anchorMax = new Vector2(0.95f, 0.82f);
            statusLabel.rectTransform.sizeDelta = Vector2.zero;

            RectTransform urlBox = Ui.Panel("UrlBox", leftCol, new Vector2(0.05f, 0.43f), new Vector2(0.95f, 0.61f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(urlBox, new Color(0.05f, 0.08f, 0.16f, 1f));

            urlLabel = Ui.MakeText("LanUrlText", urlBox, "ws://192.168.43.1:8080", 24, OkText, TextAnchor.MiddleLeft);
            Stretch(urlLabel.rectTransform, 16f, 0f, 148f, 0f);

            MakeActionButton(
                "CopyUrlButton",
                urlBox,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f),
                new Vector2(126f, 48f),
                "COPY URL",
                MutedBtn,
                Ui.Cream,
                18,
                CopyShareUrl);

            playersLabel = Ui.MakeText("HostPlayers", leftCol, "Room preparing...", 20, Ui.Cream, TextAnchor.MiddleLeft);
            playersLabel.rectTransform.anchorMin = new Vector2(0.05f, 0.25f);
            playersLabel.rectTransform.anchorMax = new Vector2(0.95f, 0.40f);
            playersLabel.rectTransform.sizeDelta = Vector2.zero;

            MakeActionButton(
                "RetryHostButton",
                leftCol,
                new Vector2(0.05f, 0f),
                new Vector2(0.05f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 20f),
                new Vector2(190f, 56f),
                "RETRY HOST",
                MutedBtn,
                Ui.Cream,
                20,
                () => StartHostWizard(direct: _openedDirectly));

            MakeActionButton(
                "EnterLobbyButton",
                leftCol,
                new Vector2(0.95f, 0f),
                new Vector2(0.95f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 20f),
                new Vector2(340f, 58f),
                "OPEN ROOM LOBBY",
                GoldCta,
                InkDark,
                24,
                EnterHostLobby);

            RectTransform rightCol = Ui.Panel("QrCard", parent, new Vector2(0.62f, 0.04f), new Vector2(1f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(rightCol, SubCardBg);

            Text qrHeader = Ui.MakeText("QrTitle", rightCol, "SCAN TO JOIN", 24, Ui.Cream, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            qrHeader.rectTransform.anchorMin = new Vector2(0.06f, 0.84f);
            qrHeader.rectTransform.anchorMax = new Vector2(0.94f, 0.95f);
            qrHeader.rectTransform.sizeDelta = Vector2.zero;

            RectTransform qrPlate = Ui.Panel("QrWhitePlate", rightCol, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(268f, 268f));
            Ui.MakeImage("WhiteBg", qrPlate, Color.white);
            Stretch((RectTransform)qrPlate.GetChild(0));

            qrImage = Ui.MakeImage("QrSprite", qrPlate, Color.white);
            qrImage.preserveAspect = true;
            Stretch(qrImage.rectTransform, 12f, 12f, 12f, 12f);

            qrCaption = Ui.MakeText("QrCaption", rightCol, "Friends on your Hotspot or Wi-Fi scan this QR or tap Find Rooms.", 17, SubtleText, TextAnchor.MiddleCenter);
            qrCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
            qrCaption.rectTransform.anchorMin = new Vector2(0.06f, 0.04f);
            qrCaption.rectTransform.anchorMax = new Vector2(0.94f, 0.20f);
            qrCaption.rectTransform.sizeDelta = Vector2.zero;
        }

        void BuildGuestPanel(
            RectTransform parent,
            out Text statusLabel,
            out InputField manualInput,
            out Text emptyLabel)
        {
            RectTransform listCard = Ui.Panel("DiscoveredHostsCard", parent, new Vector2(0f, 0.24f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(listCard, SubCardBg);

            statusLabel = Ui.MakeText("GuestStatus", listCard, "Scanning nearby Wi-Fi...", 20, GoldCta, TextAnchor.MiddleLeft);
            statusLabel.rectTransform.anchorMin = new Vector2(0.03f, 0.82f);
            statusLabel.rectTransform.anchorMax = new Vector2(0.64f, 0.97f);
            statusLabel.rectTransform.sizeDelta = Vector2.zero;

            MakeActionButton(
                "ScanQrButton",
                listCard,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-190f, -10f),
                new Vector2(164f, 48f),
                "SCAN QR",
                CoralCta,
                Ui.Cream,
                19,
                RequestQrScan);

            MakeActionButton(
                "RescanButton",
                listCard,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-14f, -10f),
                new Vector2(164f, 48f),
                "SCAN AGAIN",
                TealCta,
                Ui.Cream,
                19,
                RescanHosts);

            RectTransform rowsArea = Ui.Panel("RowsArea", listCard, new Vector2(0.025f, 0.04f), new Vector2(0.975f, 0.79f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            emptyLabel = Ui.MakeText(
                "EmptyRoomsHint",
                rowsArea,
                "Looking for nearby Kalyeah Hotspot rooms...\nAsk the host to stay on Step 2 or inside the Room Lobby.",
                20,
                SubtleText,
                TextAnchor.MiddleCenter);
            emptyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(emptyLabel.rectTransform, 24f, 12f, 24f, 12f);

            const float rowGap = 6f;
            float slice = 1f / MaxDiscoveredRows;
            for (int i = 0; i < MaxDiscoveredRows; i++)
            {
                int captured = i;
                float yMax = 1f - i * slice;
                float yMin = yMax - slice;
                RectTransform rowRect = Ui.Panel(
                    "HostRow_" + i.ToString(CultureInfo.InvariantCulture),
                    rowsArea,
                    new Vector2(0f, yMin),
                    new Vector2(1f, yMax),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(0f, -rowGap));
                Image bg = Ui.MakeImage("RowBg", rowRect, new Color(0.14f, 0.23f, 0.42f, 1f), raycast: true);
                Stretch(bg.rectTransform);
                Button btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnDiscoveredRowClicked(captured));
                _touchTargets.Add(rowRect);

                Text rowTitle = Ui.MakeText("HostName", rowRect, "", 21, Ui.Cream, TextAnchor.MiddleLeft);
                Stretch(rowTitle.rectTransform, 18f, 0f, 340f, 0f);

                Text rowUrl = Ui.MakeText("HostUrl", rowRect, "", 18, OkText, TextAnchor.MiddleRight);
                Stretch(rowUrl.rectTransform, 420f, 0f, 18f, 0f);

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
            RectTransform manualBar = Ui.Panel("ManualUrlBar", parent, new Vector2(0f, 0.02f), new Vector2(1f, 0.20f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StyleCard(manualBar, SubCardBg);

            Text manualLabel = Ui.MakeText("ManualPrompt", manualBar, "DIRECT LAN URL:", 19, SubtleText, TextAnchor.MiddleLeft);
            manualLabel.rectTransform.anchorMin = new Vector2(0.025f, 0f);
            manualLabel.rectTransform.anchorMax = new Vector2(0.21f, 1f);
            manualLabel.rectTransform.sizeDelta = Vector2.zero;

            manualInput = MakeInputField(
                "ManualWsInput",
                manualBar,
                new Vector2(0.22f, 0.14f),
                new Vector2(0.67f, 0.86f),
                "ws://192.168.43.1:8080",
                21);

            MakeActionButton(
                "PasteUrlButton",
                manualBar,
                new Vector2(0.685f, 0.5f),
                new Vector2(0.685f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(128f, 50f),
                "PASTE",
                MutedBtn,
                Ui.Cream,
                19,
                PasteIntoManualUrl);

            InputField capturedInput = manualInput;
            MakeActionButton(
                "ManualJoinButton",
                manualBar,
                new Vector2(0.98f, 0.5f),
                new Vector2(0.98f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(180f, 52f),
                "JOIN URL",
                GoldCta,
                InkDark,
                21,
                () => JoinHostUrl(capturedInput != null ? capturedInput.text : ""));
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
            if (frame != null)
            {
                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float w = Mathf.Clamp(safe.x - 24f, 680f, 1140f);
                float h = Mathf.Clamp(safe.y - 20f, 520f, 668f);
                frame.sizeDelta = new Vector2(w, h);
            }

            for (int i = 0; i < _touchTargets.Count; i++)
            {
                RectTransform rt = _touchTargets[i];
                if (rt == null) continue;
                if (rt.anchorMin.x == rt.anchorMax.x && rt.anchorMin.y == rt.anchorMax.y)
                    rt.sizeDelta = ScreenFitMetrics.ClampTouchSize(rt.sizeDelta, ScreenFitMetrics.MinTouchDp, _canvas);
            }
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
            UnityEngine.Events.UnityAction onClick)
        {
            Vector2 clamped = ScreenFitMetrics.ClampTouchSize(size, ScreenFitMetrics.MinTouchDp, _canvas);
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, pos, clamped);
            Image bg = Ui.MakeImage("Bg", rt, fill, raycast: true);
            Stretch(bg.rectTransform);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.05f, 0.12f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = Ui.MakeText("Label", rt, caption, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            if (textColor == InkDark)
                label.GetComponent<Outline>().enabled = false;
            Stretch(label.rectTransform, 8f, 4f, 8f, 4f);

            Button btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            _touchTargets.Add(rt);
            return btn;
        }

        InputField MakeInputField(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string placeholderText, int fontSize)
        {
            RectTransform rt = Ui.Panel(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image bg = Ui.MakeImage("InputBg", rt, new Color(0.04f, 0.07f, 0.14f, 1f), raycast: true);
            Stretch(bg.rectTransform);

            Text textComp = Ui.MakeText("Text", rt, "", fontSize, Ui.Cream, TextAnchor.MiddleLeft);
            textComp.supportRichText = false;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(textComp.rectTransform, 14f, 6f, 14f, 6f);

            Text placeholder = Ui.MakeText("Placeholder", rt, placeholderText, fontSize, new Color(0.55f, 0.64f, 0.78f, 0.75f), TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 14f, 6f, 14f, 6f);

            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = textComp;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            return field;
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
                name = "KalyeahQrStandIn",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[n * n];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(16, 22, 38, 255);
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

        sealed class HotspotFlowDriver : MonoBehaviour
        {
            HotspotFlowView _owner;

            public void Bind(HotspotFlowView owner) => _owner = owner;

            void Update() => _owner?.Tick(Time.unscaledDeltaTime);

            void OnRectTransformDimensionsChange()
            {
                if (_owner != null && _owner.Root != null)
                    _owner.ApplyResponsiveMetrics(_owner.Root.Find("Frame") as RectTransform);
            }

            void OnDestroy() => _owner?.Dispose();
        }
    }
}
