using System;
using System.Collections.Generic;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// The Title scene's one component: the whole main screen and its pages, built in code (title_screen.gd, landscape 1280 x 720).
    /// Behind everything your kid in 3D on a backlit floor (turn it with a drag); a top header (your profile capsule, the logo,
    /// Settings); a left navigation rail (LARO, MGA BATA, ARMAS, RANGGO); a lower-left callout with your kid's street name and arrows to
    /// change kid; and the play cluster at the bottom right: the mode selector (Online, Local, Practice), the game, a second action
    /// (Join / Sumali / Training) and the big MAGLARO button. The boot loading screen shows once when the app opens.
    /// The phone's Back closes a dialog, then a page or the room, then asks "Quit game?".
    /// </summary>
    public sealed class MenuRoot : MonoBehaviour
    {
        public const string SceneName = "Title";
        const float RailWidth = 176f;
        const float Inset = 22f;

        readonly MenuHost _host = new MenuHost();
        KidShowcase _showcase;
        RectTransform _safe;
        RectTransform _home;
        RectTransform _pageSlot;
        RectTransform _roomLayer;
        Text _profileName;
        RawImage _profileFace;
        Text _kidEpithet;
        Text _kidName;
        Text _kidRole;
        Text _status;
        Text _modeText;
        Button _gameButton;
        Button _subButton;
        Button _cta;
        readonly List<Button> _modePills = new List<Button>();
        readonly Dictionary<string, Button> _rail = new Dictionary<string, Button>();
        RoomScreen _roomScreen;
        bool _starting;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            MenuServices.Ensure();
            MenuKit.EnsureEventSystem();
            ResetNetwork();
            Build();
            if (MenuServices.FirstLaunch)
            {
                MenuServices.MarkLaunched();
                _host.Nav.Busy = true;
                LoadingOverlay boot = null;
                boot = LoadingOverlay.Show(transform, () =>
                {
                    _host.Nav.Busy = false;
                    boot.Hide();
                });
            }
        }

        /// <summary>Back on the main screen from a match (or a dropped room): no room is left open and this phone hosts nothing.</summary>
        static void ResetNetwork()
        {
            NetHub net = MenuServices.Net;
            if (net.Client.IsOnline() && !net.Client.Room.IsEmpty) net.Client.LeaveRoom();
            net.Client.Close();
            net.StopHost();
        }

        void Build()
        {
            _showcase = KidShowcase.Create(transform);
            _showcase.Show(MenuServices.Session.AvatarId);

            // the backdrop sits behind the kid: a canvas drawn by the menu camera, far away
            var back = new GameObject("Backdrop", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            back.transform.SetParent(transform, false);
            var backCanvas = back.GetComponent<Canvas>();
            backCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backCanvas.worldCamera = _showcase.Camera;
            backCanvas.planeDistance = 30f;
            var backScaler = back.GetComponent<CanvasScaler>();
            backScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            backScaler.referenceResolution = MenuKit.Base;
            backScaler.matchWidthOrHeight = 1f;
            var sky = new GameObject("Sky", typeof(RectTransform), typeof(RawImage));
            sky.transform.SetParent(back.transform, false);
            var skyImage = sky.GetComponent<RawImage>();
            skyImage.texture = MenuKit.Gradient(MenuKit.Dusk, MenuKit.Night);
            skyImage.raycastTarget = false;
            MenuKit.Stretch((RectTransform)sky.transform);

            Canvas front = MenuKit.Canvas("Menu", transform, 10);
            _safe = MenuKit.Fill("SafeArea", front.transform);
            _safe.gameObject.AddComponent<MenuSafeArea>().Apply();

            _home = MenuKit.Fill("Home", _safe);
            BuildStageDrag();
            BuildHeader();
            BuildRail();
            BuildCallout();
            BuildCluster();
            _pageSlot = MenuKit.Stretch(MenuKit.Rect("Pages", _safe), RailWidth + Inset * 2f, 16f, Inset, 96f);

            Canvas rooms = MenuKit.Canvas("Rooms", transform, 20);
            _roomLayer = MenuKit.Fill("RoomLayer", rooms.transform);
            Canvas dialogs = MenuKit.Canvas("Dialogs", transform, 30);
            _host.DialogLayer = MenuKit.Fill("DialogLayer", dialogs.transform);
            Canvas toasts = MenuKit.Canvas("Toasts", transform, 40);
            _host.ToastLayer = MenuKit.Fill("ToastLayer", toasts.transform);
            toasts.GetComponent<GraphicRaycaster>().enabled = false;
            HighlightRail("play");
            RefreshAll();
        }

        // ---- the main screen ---------------------------------------------------------------------------------------------------------

        void BuildStageDrag()
        {
            Image area = MenuKit.Box("Stage", _home, new Color(0f, 0f, 0f, 0f), raycast: true, rounded: false);
            MenuKit.Stretch(area.rectTransform, RailWidth + Inset * 2f, 180f, 460f, 96f);
            area.gameObject.AddComponent<StageDrag>().Showcase = _showcase;
        }

        void BuildHeader()
        {
            Image capsule = MenuKit.Box("Profile", _home, MenuKit.Glass, raycast: true);
            MenuKit.Place(capsule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Inset, -8f), new Vector2(360f, 76f));
            Button edit = capsule.gameObject.AddComponent<Button>();
            edit.targetGraphic = capsule;
            edit.onClick.AddListener(OpenProfile);
            Image ring = MenuKit.Disc("Ring", capsule.rectTransform, MenuKit.Amber);
            MenuKit.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(64f, 64f));
            _profileFace = MenuKit.Art("Face", ring.rectTransform, null);
            MenuKit.Stretch(_profileFace.rectTransform, 4f, 4f, 4f, 4f);
            _profileName = MenuKit.Title("Name", capsule.rectTransform, "", MenuKit.Body, MenuKit.Cream, TextAnchor.UpperLeft);
            MenuKit.Stretch(_profileName.rectTransform, 84f, 30f, 12f, 8f);
            Text hint = MenuKit.Label("Hint", capsule.rectTransform, "Tap to change your name", MenuKit.Caption, MenuKit.Muted, TextAnchor.LowerLeft);
            MenuKit.Stretch(hint.rectTransform, 84f, 8f, 12f, 40f);

            Texture2D logo = MenuKit.Picture("ui/kalyeah_logo");
            RectTransform logoSlot = MenuKit.Place(MenuKit.Rect("Logo", _home), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(260f, 84f));
            if (logo != null) MenuKit.Stretch(MenuKit.Art("Logo", logoSlot, logo).rectTransform);
            else MenuKit.Stretch(MenuKit.Title("Logo", logoSlot, "KALYEAH", MenuKit.Display, MenuKit.Gold, TextAnchor.MiddleCenter).rectTransform);

            Button settings = MenuKit.Button("Settings", _home, "Settings", "glass", () => OpenPage("settings"), MenuKit.Body);
            MenuKit.Place((RectTransform)settings.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Inset, -8f), new Vector2(200f, 76f));
        }

        void BuildRail()
        {
            RectTransform rail = MenuKit.Place(MenuKit.Rect("Rail", _safe), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Inset, -100f),
                new Vector2(RailWidth, 4f * 86f));
            MenuKit.Column(rail, 10f);
            foreach (MenuCatalog.RailItem item in MenuCatalog.Rail)
            {
                string id = item.Id;
                Button button = MenuKit.Button("Rail_" + id, rail, item.Title + "\n" + item.Sub, "glass", () => OnRail(id), MenuKit.Detail + 2);
                MenuKit.Size(button, -1f, 76f);
                _rail[id] = button;
            }
        }

        void BuildCallout()
        {
            RectTransform callout = MenuKit.Place(MenuKit.Rect("KidCallout", _home), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Inset, 16f),
                new Vector2(420f, 160f));
            _kidEpithet = MenuKit.Label("Epithet", callout, "", MenuKit.Detail + 2, MenuKit.Amber, TextAnchor.LowerLeft, display: true);
            MenuKit.Stretch(_kidEpithet.rectTransform, 96f, 104f, 0f, 20f);
            _kidName = MenuKit.Title("Name", callout, "", MenuKit.Display, MenuKit.Cream, TextAnchor.MiddleLeft);
            MenuKit.Stretch(_kidName.rectTransform, 96f, 40f, 0f, 56f);
            _kidRole = MenuKit.Label("Role", callout, "", MenuKit.Caption + 2, MenuKit.Muted, TextAnchor.UpperLeft, display: true);
            MenuKit.Stretch(_kidRole.rectTransform, 96f, 0f, 0f, 120f);
            Button previous = MenuKit.Button("PreviousKid", callout, "<", "glass", () => CycleKid(-1), MenuKit.Headline);
            MenuKit.Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80f, 88f));
            Button next = MenuKit.Button("NextKid", callout, ">", "glass", () => CycleKid(1), MenuKit.Headline);
            MenuKit.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(80f, 88f));
        }

        void BuildCluster()
        {
            RectTransform cluster = MenuKit.Place(MenuKit.Rect("PlayCluster", _home), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Inset, 16f),
                new Vector2(430f, 380f));
            _status = MenuKit.Label("Status", cluster, "", MenuKit.Caption + 2, MenuKit.Jade, TextAnchor.LowerRight, display: true);
            MenuKit.Place(_status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(430f, 30f));
            _modeText = MenuKit.Label("ModeText", cluster, "", MenuKit.Detail, MenuKit.Cream, TextAnchor.MiddleRight);
            MenuKit.Place(_modeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(430f, 34f));

            RectTransform pills = MenuKit.Place(MenuKit.Rect("Modes", cluster), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), new Vector2(430f, 64f));
            MenuKit.Row(pills, 8f);
            for (int i = 0; i < MenuCatalog.Modes.Length; i++)
            {
                int index = i;
                Button pill = MenuKit.Button("Mode_" + MenuCatalog.Modes[i].Id, pills, MenuCatalog.Modes[i].Title, "glass", () => SelectMode(index), MenuKit.Detail + 2);
                _modePills.Add(pill);
            }
            _gameButton = MenuKit.Button("Game", cluster, "", "glass", OpenGamePicker, MenuKit.Detail + 2);
            MenuKit.Place((RectTransform)_gameButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -146f), new Vector2(430f, 76f));
            _subButton = MenuKit.Button("Sub", cluster, "", "blue", OnSub, MenuKit.Body);
            MenuKit.Place((RectTransform)_subButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 112f));
            _cta = MenuKit.Button("Maglaro", cluster, "MAGLARO", "orange", OnCta, MenuKit.Display);
            MenuKit.Place((RectTransform)_cta.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(268f, 112f));
        }

        void RefreshAll()
        {
            SessionModel session = MenuServices.Session;
            _profileName.text = session.PlayerName;
            Texture2D face = MenuKit.Picture("portraits/" + session.AvatarId);
            _profileFace.texture = face;
            _profileFace.enabled = face != null;
            if (face != null) _profileFace.GetComponent<AspectRatioFitter>().aspectRatio = (float)face.width / Mathf.Max(1, face.height);
            MenuCatalog.KidMeta meta = MenuCatalog.Kids.TryGetValue(session.AvatarId, out MenuCatalog.KidMeta found) ? found : null;
            _kidEpithet.text = meta != null ? meta.Epithet : "";
            _kidName.text = MenuServices.Data.KidName(session.AvatarId).ToUpperInvariant();
            _kidRole.text = meta != null ? meta.Role : "";
            if (meta != null) _kidEpithet.color = MenuKit.Hex(meta.Accent);
            _showcase.Show(session.AvatarId);
            RefreshMode();
        }

        void RefreshMode()
        {
            SessionModel session = MenuServices.Session;
            int mode = Mathf.Clamp(session.MenuMode, 0, MenuCatalog.Modes.Length - 1);
            MenuCatalog.Mode current = MenuCatalog.Modes[mode];
            for (int i = 0; i < _modePills.Count; i++) MenuKit.Restyle(_modePills[i], i == mode ? "gold" : "glass");
            bool practice = current.Id == "practice";
            string game = practice ? session.PracticeGame : "street";
            string format = MenuCatalog.FormatTag(MenuServices.Data, game);
            _status.text = practice ? current.Status + " · " + format : current.Status;
            _modeText.text = current.Text;
            MenuKit.SetText(_gameButton, MenuCatalog.MapName(game).ToUpperInvariant() + " · " + format + (practice ? "\nTap to change" : ""));
            _gameButton.interactable = practice;
            MenuKit.SetText(_subButton, current.Sub);
            MenuKit.SetText(_cta, "MAGLARO");
        }

        void SelectMode(int index)
        {
            MenuServices.Session.MenuMode = index;
            RefreshMode();
        }

        void CycleKid(int way)
        {
            string[] kids = SessionModel.Avatars;
            int at = Math.Max(0, Array.IndexOf(kids, MenuServices.Session.AvatarId));
            MenuServices.Session.SetAvatar(kids[((at + way) % kids.Length + kids.Length) % kids.Length]);
            RefreshAll();
        }

        // ---- actions -----------------------------------------------------------------------------------------------------------------

        void OnCta()
        {
            switch (MenuCatalog.Modes[MenuServices.Session.MenuMode].Id)
            {
                case "practice": OpenPracticeConfirm(); break;
                case "online": OpenRoom(false, SessionModel.LobbyIntent.Create); break;
                default: HotspotDialog.Create(_host, () => OpenRoom(true, SessionModel.LobbyIntent.Create)); break;
            }
        }

        void OnSub()
        {
            switch (MenuCatalog.Modes[MenuServices.Session.MenuMode].Id)
            {
                case "practice": StartPractice("training"); break;
                case "online": OpenRoom(false, SessionModel.LobbyIntent.Join); break;
                default:
                    HotspotDialog.Join(_host, url =>
                    {
                        MenuServices.Session.LobbyUrl = url;
                        OpenRoom(true, SessionModel.LobbyIntent.Join);
                    });
                    break;
            }
        }

        /// <summary>Practice's confirm (title_screen.gd open_practice_confirm): the game, what it is, the difficulty for Agawang Base, Play.</summary>
        void OpenPracticeConfirm()
        {
            string game = MenuServices.Session.PracticeGame;
            MenuDialog dialog = MenuDialog.Open(_host, MenuCatalog.PracticeTitle(MenuServices.Data, game));
            dialog.AddText(MenuCatalog.PracticeWords(MenuServices.Data, game));
            if (MenuServices.Data.StreetRules(game) == null && MenuServices.Data.Rules.BotLevels.Count > 0)
            {
                var options = new List<(string, string)>();
                foreach (BotLevel level in MenuServices.Data.Rules.BotLevels) options.Add((level.Id, level.Label));
                BotLevel chosen = MenuServices.Data.BotLevel(MenuServices.Settings.BotLevel);
                Text blurb = null;
                var choice = new MenuKit.Segmented(dialog.Body, options, chosen != null ? chosen.Id : "", id =>
                {
                    MenuServices.Settings.SetBotLevel(id);
                    BotLevel level = MenuServices.Data.BotLevel(id);
                    if (blurb != null && level != null) blurb.text = level.Blurb;
                }, 72f);
                blurb = dialog.AddText(chosen != null ? chosen.Blurb : "", MenuKit.Detail, TextAnchor.MiddleCenter, MenuKit.ModalMuted);
                MenuKit.Size(choice.Root, -1f, 72f);
            }
            dialog.AddButton("Play", "gold", () =>
            {
                dialog.Close();
                StartPractice("match");
            });
        }

        void OpenGamePicker()
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Pick a game", MenuDialog.WidthSheet);
            foreach (string id in SessionModel.PracticeGames)
            {
                string game = id;
                RectTransform row = dialog.AddRow(104f);
                bool current = game == MenuServices.Session.PracticeGame;
                Button pick = MenuKit.Button("Game_" + game, row, MenuCatalog.MapName(game), current ? "gold" : "blue", () =>
                {
                    MenuServices.Session.SetPracticeGame(game);
                    dialog.Close();
                    RefreshMode();
                }, MenuKit.Body);
                MenuKit.Size(pick, 300f, 96f);
                Text blurb = MenuKit.Label("Blurb", row, MenuCatalog.MapBlurbs[game], MenuKit.Caption, MenuKit.ModalText, TextAnchor.MiddleLeft, wrap: true);
                MenuKit.Size(blurb, -1f, 96f, 1f);
            }
        }

        /// <summary>Practice: `kind` "match" (against bots, with the briefing) or "training" (the sandbox).</summary>
        void StartPractice(string kind)
        {
            if (_starting) return;
            _starting = true;
            SessionModel session = MenuServices.Session;
            session.PracticeKind = kind;
            session.BriefingPending = kind == "match";
            string game = kind == "training" ? MatchLaunchBridge.Training : session.PracticeGame;
            string level = MenuServices.Data.BotLevel(MenuServices.Settings.BotLevel)?.Id ?? "";
            string avatar = session.AvatarId;
            _host.Nav.Clear();
            _host.Nav.Busy = true;
            LoadingOverlay.Show(transform, () => MatchLaunchBridge.StartPractice(game, level, avatar));
        }

        void OpenRoom(bool hotspot, SessionModel.LobbyIntent intent)
        {
            if (_roomScreen != null || _starting) return;
            ClosePage();
            _showcase.SetVisible(false);
            _roomScreen = RoomScreen.Open(_host, _roomLayer, hotspot, intent, OnMatchLoading, () =>
            {
                _roomScreen = null;
                if (!_starting) _showcase.SetVisible(true);
                RefreshAll();
            });
        }

        void OnMatchLoading(Kalyeah.Net.GDict info)
        {
            if (_starting) return;
            _starting = true;
            MenuDialog.CloseCurrent();
            LoadingOverlay.Show(transform, () =>
            {
                if (_roomScreen != null) _roomScreen.HandOver();
                MatchLaunchBridge.StartOnline(MenuServices.Net.Client);
            });
        }

        // ---- pages -------------------------------------------------------------------------------------------------------------------

        void OnRail(string id)
        {
            if (id == "play") ClosePage();
            else OpenPage(id);
        }

        void OpenPage(string id)
        {
            if (_host.Nav.CurrentPage == id) return;
            MenuDialog.CloseCurrent();
            RectTransform page = MenuKit.Fill("Page_" + id, _pageSlot);
            _host.Nav.Push(MenuNavigation.Kind.Page, id, () =>
            {
                if (page != null) Destroy(page.gameObject);
                ShowHome(true);
            });
            switch (id)
            {
                case "kids":
                    new KidsPage(page, "Mga Bata", _ => RefreshAll());
                    break;
                case "cards":
                    CardsPage.Build(page);
                    break;
                case "history":
                    new HistoryPage(page);
                    break;
                default:
                    SettingsPage.Build(page, OpenQuit);
                    break;
            }
            ShowHome(false);
            HighlightRail(id);
        }

        void ClosePage()
        {
            string current = _host.Nav.CurrentPage;
            if (current.Length == 0 || current == RoomScreen.PageId) return;
            while (_host.Nav.CurrentPage == current && _host.Nav.Back() == MenuNavigation.BackResult.Closed) { }
        }

        void ShowHome(bool home)
        {
            foreach (Transform child in _home)
                if (child.name != "Profile" && child.name != "Logo" && child.name != "Settings") child.gameObject.SetActive(home);
            _showcase.SetVisible(home && _roomScreen == null);
            if (home)
            {
                HighlightRail("play");
                RefreshAll();
            }
        }

        void HighlightRail(string id)
        {
            foreach (var pair in _rail) MenuKit.Restyle(pair.Value, pair.Key == id ? "gold" : "glass");
        }

        void OpenProfile()
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Your name");
            InputField field = null;
            field = MenuKit.Field("Name", dialog.Body, MenuServices.Session.PlayerName, SessionModel.MaxNameLength, value =>
            {
                string kept = SessionModel.FilterName(value);
                if (kept != value) field.SetTextWithoutNotify(kept);
            });
            MenuKit.Size(field, -1f, 88f);
            dialog.AddText("Up to " + SessionModel.MaxNameLength + " letters. Everyone in your rooms sees it.", MenuKit.Detail, TextAnchor.MiddleCenter, MenuKit.ModalMuted);
            dialog.AddButton("Save", "gold", () =>
            {
                bool changed = MenuServices.Session.Rename(field.text);
                dialog.Close();
                RefreshAll();
                _host.Toast(changed ? "Profile saved!" : "Name not changed", changed ? "ok" : "info");
            });
        }

        void OpenQuit()
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Quit game?");
            dialog.AddButton("Quit", "red", () =>
            {
                dialog.Close();
                Application.Quit();
            });
        }

        // ---- Back ----------------------------------------------------------------------------------------------------------------------

        void Update()
        {
            if (!BackPressed()) return;
            if (_host.Nav.Back() == MenuNavigation.BackResult.AskQuit) OpenQuit();
        }

        /// <summary>
        /// Android's Back button: the Input System reports it as the Escape key of the phone's keyboard device (there is no desktop path:
        /// Android is the only client).
        /// </summary>
        public static bool BackPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        /// <summary>A drag on the stage turns the kid.</summary>
        sealed class StageDrag : MonoBehaviour, IDragHandler
        {
            public KidShowcase Showcase;

            public void OnDrag(PointerEventData eventData)
            {
                float scale = Screen.height > 0 ? MenuKit.Base.y / Screen.height : 1f;
                if (Showcase != null) Showcase.Turn(eventData.delta.x * scale);
            }
        }
    }
}
