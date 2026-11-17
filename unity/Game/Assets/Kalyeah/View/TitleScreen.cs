using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Landscape Title &amp; Multiplayer Hub: boots EnhancedTouch, loads persisted session/settings, starts BGM via AudioHub,
    /// and registers all 7 menu screens (Home, Pick, Codex, History, Settings, Hotspot, Lobby) on MenuStack.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string SceneName = "Title";
        static readonly Color Dusk = new Color(0.06f, 0.09f, 0.18f, 1f);

        DataSource _data;
        Canvas _canvas;
        AudioHub _audio;
        MenuStack _stack;
        HomeScreenView _home;
        PickScreenView _pick;
        CodexScreenView _codex;
        HistoryScreenView _history;
        SettingsScreenView _settings;
        HotspotFlowView _hotspot;
        LobbyScreenView _lobby;
        NetworkPauseGuard _pauseGuard;
        bool _settingsSubscribed;

        public Canvas Canvas => _canvas;
        public MenuStack Stack => _stack;
        public AudioHub Audio => _audio;
        public HomeScreenView Home => _home;
        public PickScreenView Pick => _pick;
        public CodexScreenView Codex => _codex;
        public HistoryScreenView History => _history;
        public SettingsScreenView Settings => _settings;
        public HotspotFlowView Hotspot => _hotspot;
        public LobbyScreenView Lobby => _lobby;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            ScreenFitMetrics.EnsureEnhancedTouch();
            Ui.EnsureEventSystem();

            var cameraGo = new GameObject("TitleCamera", typeof(Camera));
            cameraGo.transform.SetParent(transform, false);
            if (FindAnyObjectByType<AudioListener>() == null)
                cameraGo.AddComponent<AudioListener>();
            var camera = cameraGo.GetComponent<Camera>();
            ConfigureCamera(camera);

            DataSource data = KalyeahResources.Data();
            _data = data;
            MatchBoot.ApplyBootResourcesAndGraphics(data);
            _canvas = Ui.MakeCanvas("Title", transform, camera);

            _audio = new AudioHub(transform);
            _audio.BindRules(data);

            _stack = new MenuStack(_canvas.transform, null, _audio);
            _stack.LoadPersistedState();
            MatchBoot.ApplyDeviceQualityAndFrameRate();
            ResetStaleSessionMode();

            BindSettings();
            _audio.PlayBgm();

            RegisterHubPages(data, LaunchMatchScene);
            _pauseGuard = NetworkPauseGuard.Attach(gameObject);
            _stack.Push(MenuStack.HomeId);
        }

        void Start() => LoadingScreenView.CompleteAndHide();

        void Update()
        {
            if (MatchBoot.BackPressed())
            {
                QrCodeView qrModal = _canvas != null ? _canvas.GetComponentInChildren<QrCodeView>(false) : null;
                if (qrModal != null)
                {
                    qrModal.Close();
                    return;
                }

                _stack?.HandleBack();
                return;
            }

            float dt = Time.deltaTime;
            string activeId = _stack?.CurrentId;
            if (activeId != MenuStack.HotspotId && activeId != MenuStack.LobbyId)
                AppSession.Current.PollNetwork(dt);
        }

        void OnDestroy()
        {
            if (_pauseGuard != null)
            {
                if (Application.isPlaying)
                    Destroy(_pauseGuard);
                else
                    DestroyImmediate(_pauseGuard);
                _pauseGuard = null;
            }

            if (_settingsSubscribed)
            {
                _settingsSubscribed = false;
                SettingsStore.Current.Changed -= ApplyAudioSettings;
            }

            _lobby?.Dispose();
            _lobby = null;

            _hotspot?.Dispose();
            _hotspot = null;

            _settings?.Dispose();
            _settings = null;

            _history?.Dispose();
            _history = null;

            _codex?.Dispose();
            _codex = null;

            _home?.Dispose();
            _home = null;

            object pickObj = _pick;
            (pickObj as IDisposable)?.Dispose();
            _pick = null;

            _stack?.Dispose();
            _stack = null;

            _audio?.Dispose();
            _audio = null;
        }

        /// <summary>
        /// Builds the Home Hub under <paramref name="parent"/> drawn by <paramref name="camera"/>.
        /// When <paramref name="play"/> is null (screenshot capture), raycasting is disabled while keeping full normal visuals.
        /// </summary>
        public static Canvas Build(Transform parent, Camera camera, UnityEngine.Events.UnityAction play, Action<HomeScreenView> stage = null)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            ConfigureCamera(camera);

            Canvas canvas = Ui.MakeCanvas("Title", parent, camera);
            if (play == null)
            {
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }

            DataSource data = KalyeahResources.Data();
            MatchBoot.ApplyBootResourcesAndGraphics(data);
            var home = new HomeScreenView(
                canvas.transform,
                data,
                onNavigate: null,
                onLaunchMatch: mode =>
                {
                    AppSession.Current.Mode = mode;
                    MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
                    play?.Invoke();
                });
            home.Refresh();
            home.RefreshLayout();
            stage?.Invoke(home);
            return canvas;
        }

        public void BindSettings()
        {
            if (!_settingsSubscribed)
            {
                _settingsSubscribed = true;
                SettingsStore.Current.Changed += ApplyAudioSettings;
            }
            ApplyAudioSettings();
        }

        void ApplyAudioSettings()
        {
            SettingsStore s = SettingsStore.Current;
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
            _audio?.BindVolumes(s.MusicVolume, s.SfxVolume, s.MusicEnabled, s.SoundsEnabled, s.MasterVolume);
            MatchBoot.ApplyDeviceQualityAndFrameRate();
        }

        void RegisterHubPages(DataSource data, Action<GameMode> onLaunchMatch)
        {
            Transform pageRoot = _canvas.transform;
            Action<Action<string>> scanRequester = QrCodeView.MakeScanRequester(pageRoot);

            _home = new HomeScreenView(
                pageRoot,
                data,
                onNavigate: _stack.Push,
                onLaunchMatch: onLaunchMatch,
                audio: _audio);

            _pick = new PickScreenView(
                pageRoot,
                data,
                onBack: _stack.Pop,
                onConfirmed: _stack.Pop)
            {
                Audio = _audio,
            };

            _codex = new CodexScreenView(pageRoot, data, onBack: _stack.Pop);
            _history = new HistoryScreenView(pageRoot, data, onBack: _stack.Pop);
            _settings = new SettingsScreenView(pageRoot, data, onBack: _stack.Pop);

            _hotspot = new HotspotFlowView(
                pageRoot,
                data,
                onBack: _stack.Pop,
                onEnterLobby: () => _stack.Push(MenuStack.LobbyId),
                qrSpriteProvider: QrCodeView.EncodeSprite,
                lanIpProvider: AndroidLocalLink.GetLanIpv4,
                qrScanRequester: scanRequester);

            _lobby = new LobbyScreenView(
                pageRoot,
                data,
                onBack: _stack.Pop,
                onMatchStarted: () =>
                {
                    _stack.SavePersistedState();
                    LoadingScreenView.LoadScene(
                        MatchBoot.SceneName,
                        _data,
                        LoadingScreenView.ResolveSessionModeTitle(AppSession.Current));
                },
                onPickKid: () => _stack.Push(MenuStack.PickId),
                qrSpriteProvider: QrCodeView.EncodeSprite,
                lanIpProvider: AndroidLocalLink.GetLanIpv4,
                qrScanRequester: scanRequester);

            // PLAY is online first: when the lobby can't reach the server, return home and offer offline play
            _lobby.ConnectionUnavailable += () =>
            {
                _stack.Pop();
                _home.ShowOfflinePrompt();
            };

            _stack.Register(
                MenuStack.HomeId,
                _home.Root,
                onShow: () =>
                {
                    _home.Refresh();
                    _home.RefreshLayout();
                },
                onHide: null,
                onBackOverride: _home.HandleBack);

            _stack.Register(
                MenuStack.PickId,
                _pick.Root,
                onShow: () =>
                {
                    _pick.RefreshFromSession();
                    _pick.RefreshResponsiveLayout();
                });

            _stack.Register(MenuStack.CodexId, _codex.Root, onShow: _codex.Show, onHide: _codex.Hide);
            _stack.Register(MenuStack.HistoryId, _history.Root, onShow: _history.Show, onHide: _history.Hide);
            _stack.Register(MenuStack.SettingsId, _settings.Root, onShow: _settings.Show, onHide: _settings.Hide);

            _stack.Register(
                MenuStack.HotspotId,
                _hotspot.Root,
                onShow: OnHotspotShown,
                onHide: null,
                onBackOverride: _hotspot.HandleBack);

            _stack.Register(
                MenuStack.LobbyId,
                _lobby.Root,
                onShow: _lobby.OnShow,
                onHide: null,
                onBackOverride: _lobby.HandleBack);
        }

        void OnHotspotShown()
        {
            if (_hotspot == null) return;
            if (_hotspot.CurrentRole == HotspotFlowView.FlowRole.ChooseRole)
            {
                AppSession session = AppSession.Current;
                if (session.Mode == GameMode.HotspotHost && session.Intent == LobbyIntent.Create)
                {
                    _hotspot.StartHostWizard();
                    return;
                }
                if (session.Mode == GameMode.HotspotClient && session.Intent == LobbyIntent.Join)
                {
                    _hotspot.StartGuestWizard();
                    return;
                }
            }
            _hotspot.OnShow();
        }

        void LaunchMatchScene(GameMode mode)
        {
            AppSession.Current.Mode = mode;
            _stack?.SavePersistedState();
            LoadingScreenView.LoadScene(
                MatchBoot.SceneName,
                _data,
                LoadingScreenView.ResolveSessionModeTitle(AppSession.Current));
        }

        static void ResetStaleSessionMode()
        {
            AppSession session = AppSession.Current;
            bool inActiveRoom = session.IsHosting || (session.Client != null && !session.Client.Room.IsEmpty);
            if (!inActiveRoom && (session.Mode == GameMode.Online || session.Mode == GameMode.HotspotHost || session.Mode == GameMode.HotspotClient))
                session.Mode = GameMode.Practice;
        }

        static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Dusk;
            camera.orthographic = true;
        }
    }
}
