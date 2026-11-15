using System;
using System.Collections.Generic;
using Kalyeah.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// The Room page for Online and Local rooms (lobby_screen.gd + hotspot_room_view.gd), landscape: a top row (Leave, the room code
    /// with Copy for Online, Settings), the game and the host's settings at the left (Race to, Fill with bots for Local, Open / Private
    /// for Online; a guest reads them as text), the two team columns at the right (a card for each player, an open slot to switch
    /// team), the kid strip along the bottom and the one big button (Start battle for the host, Ready for everyone else). Online Join
    /// first shows the open rooms (tap one, or Quick join) beside the code boxes for a private room. Everything goes through LobbyFlow;
    /// this only draws it. Back closes a dialog first, then leaves the room.
    /// </summary>
    public sealed class RoomScreen : MonoBehaviour
    {
        public const string PageId = "room";

        LobbyFlow _flow;
        MenuHost _host;
        Action<GDict> _matchLoading;
        Action _closed;
        bool _closing;

        RectTransform _root;
        RectTransform _browse;
        RectTransform _room;
        Text _status;
        Button _retry;
        Text _title;
        RectTransform _codeRow;
        CodeBoxes _roomCode;
        RectTransform _left;
        RectTransform _teams;
        RectTransform _kids;
        Text _readiness;
        Button _action;
        RectTransform _openList;
        Text _openEmpty;
        Button _quickJoin;
        Button _createInstead;
        CodeBoxes _joinCode;
        Text _codeError;
        string _firstListed = "";

        /// <summary>Opens the room page under `parent` (a page slot); `matchLoading` takes over when the host starts.</summary>
        public static RoomScreen Open(MenuHost host, RectTransform parent, bool hotspot, SessionModel.LobbyIntent intent, Action<GDict> matchLoading, Action closed)
        {
            RectTransform root = MenuKit.Fill("RoomScreen", parent);
            var screen = root.gameObject.AddComponent<RoomScreen>();
            screen._host = host;
            screen._root = root;
            screen._matchLoading = matchLoading;
            screen._closed = closed;
            MenuServices.Session.Intent = intent;
            MenuServices.Session.LobbyHotspot = hotspot;
            screen._flow = new LobbyFlow(MenuServices.Net, MenuServices.Session, MenuServices.Settings, hotspot, intent, MenuServices.Seed);
            screen.Build();
            host.Nav.Push(MenuNavigation.Kind.Page, PageId, screen.LeaveAndClose);
            screen._flow.Begin();
            return screen;
        }

        void Build()
        {
            Image backdrop = MenuKit.Box("Backdrop", _root, new Color(MenuKit.Night.r, MenuKit.Night.g, MenuKit.Night.b, 0.96f), raycast: true, rounded: false);
            MenuKit.Stretch(backdrop.rectTransform);
            RectTransform safe = MenuKit.Fill("SafeArea", _root);
            safe.gameObject.AddComponent<MenuSafeArea>().Apply();

            // top row
            Button leave = MenuKit.Button("Leave", safe, "Leave", "grey", () => _host.Nav.Back(), MenuKit.Body);
            MenuKit.Place((RectTransform)leave.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(170f, 72f));
            _title = MenuKit.Title("Title", safe, _flow.Hotspot ? "LOCAL ROOM" : "ONLINE ROOM", MenuKit.Headline, MenuKit.Gold, TextAnchor.MiddleLeft);
            MenuKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -14f), new Vector2(360f, 72f));
            _codeRow = MenuKit.Place(MenuKit.Rect("Code", safe), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-60f, -18f), new Vector2(560f, 64f));
            _roomCode = new CodeBoxes(_codeRow, _flow.Code.Length, 44f, null);
            Button copy = MenuKit.Button("Copy", _codeRow, "Copy", "blue", CopyCode, MenuKit.Detail + 2);
            MenuKit.Place((RectTransform)copy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 64f));
            _codeRow.gameObject.SetActive(false);
            Button settings = MenuKit.Button("Settings", safe, "Settings", "grey", OpenSettings, MenuKit.Body);
            MenuKit.Place((RectTransform)settings.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(190f, 72f));

            _status = MenuKit.Label("Status", safe, "", MenuKit.Body, MenuKit.Cream, TextAnchor.MiddleCenter, wrap: true);
            MenuKit.Place(_status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 120f));
            _retry = MenuKit.Button("TryAgain", safe, "Try again", "orange", () => _flow.Submit(), MenuKit.Body);
            MenuKit.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(320f, 88f));
            _retry.gameObject.SetActive(false);

            _room = MenuKit.Stretch(MenuKit.Rect("Room", safe), 0f, 0f, 0f, 96f);
            BuildRoom();
            _room.gameObject.SetActive(false);
            if (_flow.Browsing)
            {
                _browse = MenuKit.Stretch(MenuKit.Rect("Browse", safe), 0f, 0f, 0f, 96f);
                BuildBrowse();
            }

            _flow.StatusChanged += OnStatus;
            _flow.RoomShown += OnRoom;
            _flow.RoomsListed += OnRoomsListed;
            _flow.Toast += (text, kind) => _host.Toast(text, kind);
            _flow.RetryShown += () => _retry.gameObject.SetActive(_flow.Room == null && !_flow.Browsing);
            _flow.CodeRefused += OnCodeRefused;
            _flow.MatchLoading += OnMatchLoading;
            _flow.RoomClosed += OnRoomClosed;
        }

        // ---- Online Join: the open rooms and the code ------------------------------------------------------------------------------

        void BuildBrowse()
        {
            RectTransform left = MenuKit.Stretch(MenuKit.Rect("OpenRooms", _browse), 24f, 24f, 660f, 0f);
            Text heading = MenuKit.Title("Heading", left, "OPEN ROOMS", MenuKit.Body, MenuKit.Amber, TextAnchor.UpperLeft);
            MenuKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(590f, 44f));
            _quickJoin = MenuKit.Button("QuickJoin", left, "Quick join", "orange", () =>
            {
                if (_firstListed.Length > 0) _flow.JoinListed(_firstListed);
            }, MenuKit.Body);
            MenuKit.Place((RectTransform)_quickJoin.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -52f), new Vector2(590f, 88f));
            _quickJoin.interactable = false;
            RectTransform listArea = MenuKit.Stretch(MenuKit.Rect("ListArea", left), 0f, 0f, 0f, 152f);
            _openList = MenuKit.Scroll("Rooms", listArea, 10f);
            _openEmpty = MenuKit.Label("Empty", _openList, MenuCatalog.LookingRooms, MenuKit.Body, MenuKit.Muted, TextAnchor.MiddleCenter, wrap: true);
            MenuKit.Size(_openEmpty, -1f, 80f);
            _createInstead = MenuKit.Button("CreateRoom", _openList, "Create a room", "grey", () => _flow.CreateInstead(), MenuKit.Body);
            MenuKit.Size(_createInstead, -1f, 88f);
            _createInstead.gameObject.SetActive(false);

            RectTransform right = MenuKit.Stretch(MenuKit.Rect("CodeSide", _browse), 660f, 24f, 24f, 0f);
            Text have = MenuKit.Title("HaveACode", right, "HAVE A CODE?", MenuKit.Body, MenuKit.Amber, TextAnchor.MiddleLeft);
            MenuKit.Place(have.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 44f));
            RectTransform boxes = MenuKit.Place(MenuKit.Rect("Boxes", right), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(570f, 84f));
            _joinCode = new CodeBoxes(boxes, _flow.Code.Length, 64f, _flow.Code);
            _codeError = MenuKit.Label("CodeError", right, "", MenuKit.Detail, MenuKit.Coral, TextAnchor.UpperLeft, wrap: true);
            MenuKit.Place(_codeError.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -148f), new Vector2(570f, 60f));
            Button paste = MenuKit.Button("Paste", right, "Paste the code", "blue", () =>
            {
                _host.Toast(_flow.Code.Paste(GUIUtility.systemCopyBuffer), _flow.Code.Complete ? "ok" : "warn");
                _joinCode.Refresh();
                _codeError.text = "";
            }, MenuKit.Body);
            MenuKit.Place((RectTransform)paste.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -214f), new Vector2(278f, 88f));
            Button join = MenuKit.Button("Join", right, "Join", "orange", () => _flow.JoinCode(_flow.Code.Text), MenuKit.Body);
            MenuKit.Place((RectTransform)join.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(292f, -214f), new Vector2(278f, 88f));
            if (_flow.CanRejoin)
            {
                Button rejoin = MenuKit.Button("Rejoin", right, "Rejoin last match", "green", () => _flow.Rejoin(), MenuKit.Body);
                MenuKit.Place((RectTransform)rejoin.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -318f), new Vector2(570f, 88f));
            }
            _status.rectTransform.anchorMin = _status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _status.rectTransform.pivot = new Vector2(0.5f, 0f);
            _status.rectTransform.anchoredPosition = new Vector2(330f, 20f);
            _status.rectTransform.sizeDelta = new Vector2(560f, 100f);
        }

        void OnRoomsListed(List<GDict> rooms)
        {
            if (_openList == null) return;
            for (int i = _openList.childCount - 1; i >= 0; i--)
            {
                Transform child = _openList.GetChild(i);
                if (child != _openEmpty.transform && child != _createInstead.transform) Destroy(child.gameObject);
            }
            _firstListed = rooms.Count > 0 ? rooms[0].GetStr("code") : "";
            foreach (GDict room in rooms)
            {
                string code = room.GetStr("code");
                Button card = MenuKit.Button("Room_" + code, _openList, MenuCatalog.OpenRoomText(room, MenuServices.Data), "blue", () => _flow.JoinListed(code), MenuKit.Body - 2);
                MenuKit.Size(card, -1f, 104f);
                card.transform.SetSiblingIndex(0);
            }
            _quickJoin.interactable = rooms.Count > 0;
            _openEmpty.text = MenuCatalog.EmptyRooms;
            _openEmpty.gameObject.SetActive(rooms.Count == 0);
            _createInstead.gameObject.SetActive(rooms.Count == 0);
        }

        void OnCodeRefused(string reason)
        {
            if (_codeError != null) _codeError.text = reason;
            _joinCode?.Refresh();
        }

        // ---- the room ----------------------------------------------------------------------------------------------------------------

        void BuildRoom()
        {
            _left = MenuKit.Stretch(MenuKit.Rect("Settings", _room), 24f, 196f, 780f, 0f);
            MenuKit.Column(_left, 10f, 0, TextAnchor.UpperLeft);
            _teams = MenuKit.Stretch(MenuKit.Rect("Teams", _room), 524f, 196f, 24f, 0f);
            // along the bottom, left of the big button: 160 high
            RectTransform strip = MenuKit.Rect("KidStrip", _room);
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.offsetMin = new Vector2(24f, 20f);
            strip.offsetMax = new Vector2(-440f, 180f);
            _kids = MenuKit.Scroll("Kids", strip, 10f, 0, horizontal: true);
            _readiness = MenuKit.Label("Readiness", _room, "", MenuKit.Detail, MenuKit.Muted, TextAnchor.LowerCenter, wrap: true);
            MenuKit.Place(_readiness.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 116f), new Vector2(392f, 64f));
            _action = MenuKit.Button("Action", _room, "Ready", "orange", () => _flow.PressAction(), MenuKit.Headline);
            MenuKit.Place((RectTransform)_action.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(392f, 92f));
        }

        void OnRoom(RoomViewModel view)
        {
            bool inRoom = view != null;
            _room.gameObject.SetActive(inRoom);
            if (_browse != null) _browse.gameObject.SetActive(!inRoom && _flow.Browsing);
            if (inRoom) _retry.gameObject.SetActive(false);
            _codeRow.gameObject.SetActive(inRoom && view.Online && view.Code.Length > 0);
            if (!inRoom) return;
            _roomCode.Show(view.Code);
            if (_status.rectTransform.anchorMin.y == 0f && _browse != null)
            {
                // the status line moves from under the code boxes to the middle of the top row
                MenuKit.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(700f, 40f));
            }
            FillSettings(view);
            FillTeams(view);
            FillKids(view);
            _readiness.text = view.IsHost ? view.Readiness : view.MyReady ? "Waiting for the host to start" : "Tap Ready when you are set";
            MenuKit.Restyle(_action, view.ActionHot ? "orange" : "grey", view.ActionText);
        }

        void FillSettings(RoomViewModel view)
        {
            MenuKit.Clear(_left);
            Caption(_left, "GAME");
            if (view.IsHost)
            {
                Button game = MenuKit.Button("Game", _left, view.MapName + "\nTap to change", "blue", () => OpenMapPicker(view), MenuKit.Body);
                MenuKit.Size(game, -1f, 104f);
            }
            else Value(_left, view.MapName, 52f);

            if (view.ShowRace)
            {
                Caption(_left, "RACE TO");
                if (view.IsHost)
                {
                    var options = new List<(string, string)>();
                    foreach (int count in MenuServices.Data.Rules.FirstToChoices) options.Add((count.ToString(), count.ToString()));
                    var race = new MenuKit.Segmented(_left, options, view.FirstTo.ToString(), id => _flow.SetFirstTo(int.Parse(id)));
                    MenuKit.Size(race.Root, -1f, 64f);
                }
                else Value(_left, view.FirstTo.ToString(), 44f);
            }
            if (view.ShowBots)
            {
                Caption(_left, "FILL WITH BOTS");
                if (view.IsHost)
                {
                    RectTransform holder = MenuKit.Rect("BotsHolder", _left);
                    MenuKit.Size(holder, -1f, 64f);
                    var toggle = new MenuKit.Switch(holder, view.FillBots, on => _flow.SetFillBots(on));
                    MenuKit.Place((RectTransform)toggle.Button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(112f, 64f));
                }
                else Value(_left, view.FillBots ? "On" : "Off", 44f);
            }
            if (view.ShowOpenSwitch)
            {
                Caption(_left, "WHO CAN JOIN");
                var open = new MenuKit.Segmented(_left, new[] { ("open", "Open"), ("private", "Private") }, view.Open ? "open" : "private",
                    id => _flow.SetOpen(id == "open"));
                MenuKit.Size(open.Root, -1f, 64f);
            }
            else if (view.Summary.Length > 0) Value(_left, view.Summary, 44f);
        }

        static void Caption(Transform parent, string text)
        {
            Text label = MenuKit.Label("Caption", parent, text, MenuKit.Caption, MenuKit.Muted, TextAnchor.LowerLeft, display: true);
            MenuKit.Size(label, -1f, 26f);
        }

        static void Value(Transform parent, string text, float height)
        {
            Text label = MenuKit.Title("Value", parent, text, MenuKit.Body + 2, MenuKit.Cream, TextAnchor.MiddleLeft);
            MenuKit.Size(label, -1f, height);
        }

        void FillTeams(RoomViewModel view)
        {
            MenuKit.Clear(_teams);
            bool colorblind = MenuServices.Settings.Colorblind;
            const float rowHeight = 62f, gap = 8f, header = 40f;
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? 0f : 0.5f;
                RectTransform column = MenuKit.Rect("Team" + team, _teams);
                column.anchorMin = new Vector2(x, 0f);
                column.anchorMax = new Vector2(x + 0.5f, 1f);
                column.offsetMin = new Vector2(team == 0 ? 0f : 8f, 0f);
                column.offsetMax = new Vector2(team == 0 ? -8f : 0f, 0f);
                string name = view.FreeForAll ? "PLAYERS" : team == 0 ? "BLUE TEAM" : colorblind ? "ORANGE TEAM" : "RED TEAM";
                Text heading = MenuKit.Title("Header", column, name, MenuKit.Detail + 2, view.FreeForAll ? MenuKit.Cream : MenuKit.TeamColor(team, colorblind),
                    TextAnchor.MiddleLeft);
                MenuKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(360f, header));
                for (int row = 0; row < view.Teams[team].Count; row++)
                {
                    RoomViewModel.Slot slot = view.Teams[team][row];
                    float y = -header - row * (rowHeight + gap);
                    RectTransform card = Slot(column, slot, team, colorblind);
                    card.anchorMin = new Vector2(0f, 1f);
                    card.anchorMax = new Vector2(1f, 1f);
                    card.pivot = new Vector2(0.5f, 1f);
                    card.anchoredPosition = new Vector2(0f, y);
                    card.sizeDelta = new Vector2(0f, rowHeight);
                }
            }
        }

        RectTransform Slot(Transform column, RoomViewModel.Slot slot, int team, bool colorblind)
        {
            Color teamColor = MenuKit.TeamColor(team, colorblind);
            if (slot.IsOpen)
            {
                Button open = MenuKit.Button("OpenSlot", column, slot.CanJoin ? "+  Join team" : "Open slot", "glass", slot.CanJoin ? () => _flow.PickTeam(team) : (UnityEngine.Events.UnityAction)null,
                    MenuKit.Detail + 2);
                open.interactable = slot.CanJoin;
                AddNumber((RectTransform)open.transform, slot.Number, teamColor, hollow: true);
                return (RectTransform)open.transform;
            }
            Image bar = MenuKit.Box("Player" + slot.PlayerId, column, MenuKit.Hex("14234f"), trim: true);
            AddNumber(bar.rectTransform, slot.Number, teamColor, hollow: false);
            Texture2D face = MenuKit.Picture("portraits/" + slot.Kid);
            if (face != null)
            {
                RawImage portrait = MenuKit.Art("Face", bar.rectTransform, face);
                MenuKit.Place(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(44f, 58f));
            }
            Text name = MenuKit.Label("Name", bar.rectTransform, slot.Name, MenuKit.Body - 2, MenuKit.Cream, TextAnchor.MiddleLeft, display: true);
            MenuKit.Stretch(name.rectTransform, 118f, 0f, 150f, 0f);
            if (slot.Tag.Length > 0)
            {
                Text tag = MenuKit.Label("Tag", bar.rectTransform, slot.Tag, MenuKit.Caption, slot.Ready ? MenuKit.Jade : MenuKit.Amber, TextAnchor.MiddleRight, display: true);
                MenuKit.Stretch(tag.rectTransform, 300f, 0f, 12f, 0f);
            }
            return bar.rectTransform;
        }

        static void AddNumber(RectTransform card, int number, Color team, bool hollow)
        {
            Image block = MenuKit.Box("Number", card, hollow ? new Color(team.r, team.g, team.b, 0.25f) : team);
            MenuKit.Place(block.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(52f, 52f));
            Text label = MenuKit.Title("Value", block.rectTransform, number.ToString(), MenuKit.Body, MenuKit.Cream, TextAnchor.MiddleCenter);
            MenuKit.Stretch(label.rectTransform);
        }

        void FillKids(RoomViewModel view)
        {
            MenuKit.Clear(_kids);
            foreach (RoomViewModel.KidChoice kid in view.Kids)
            {
                string id = kid.Id;
                Image card = MenuKit.Box("Kid_" + id, _kids, kid.Picked ? MenuKit.Gold : MenuKit.Cream, raycast: true, trim: true);
                MenuKit.Size(card, 104f, 140f);
                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.interactable = !kid.Taken;
                if (!kid.Taken && !kid.Picked) button.onClick.AddListener(() => _flow.PickKid(id));
                button.onClick.AddListener(MenuServices.Buzz);
                Texture2D face = MenuKit.Picture("portraits/" + id);
                RawImage portrait = MenuKit.Art("Face", card.rectTransform, face);
                MenuKit.Stretch(portrait.rectTransform, 6f, 30f, 6f, 6f);
                if (kid.Taken) portrait.color = new Color(0.68f, 0.64f, 0.58f);
                Text name = MenuKit.Label("Name", card.rectTransform, MenuServices.Data.KidName(id), MenuKit.Caption, MenuKit.Ink, TextAnchor.LowerCenter, display: true);
                MenuKit.Stretch(name.rectTransform, 2f, 4f, 2f, 0f);
            }
        }

        // ---- dialogs -----------------------------------------------------------------------------------------------------------------

        /// <summary>The host's game picker (room_map_picker.gd): the four games, each with how it plays; the room's one says Keep.</summary>
        void OpenMapPicker(RoomViewModel view)
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Pick a game", MenuDialog.WidthSheet);
            foreach (string id in RoomService.MapIds)
            {
                string map = id;
                RectTransform row = dialog.AddRow(96f);
                Button pick = MenuKit.Button("Pick_" + map, row, (map == view.MapId ? "Keep " : "Pick ") + MenuCatalog.MapName(map), map == view.MapId ? "grey" : "blue", () =>
                {
                    if (map != view.MapId) _flow.PickMap(map);
                    dialog.Close();
                }, MenuKit.Body);
                MenuKit.Size(pick, 360f, 88f);
                Text info = MenuKit.Label("Info", row, MenuCatalog.MapInfo(MenuServices.Data, map, view.TeamSize, view.FirstTo), MenuKit.Detail, MenuKit.ModalText,
                    TextAnchor.MiddleLeft, wrap: true);
                MenuKit.Size(info, -1f, 88f, 1f);
            }
        }

        void OpenSettings()
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Settings", MenuDialog.WidthSheet);
            RectTransform area = dialog.AddBlock(440f);
            Image dark = MenuKit.Box("Dark", area, MenuKit.Ink);
            MenuKit.Stretch(dark.rectTransform);
            RectTransform body = MenuKit.Scroll("Scroll", area, 12f, 12);
            SettingsPage.Fill(body, null);
        }

        void CopyCode()
        {
            if (_flow.Room == null || _flow.Room.Code.Length == 0) return;
            GUIUtility.systemCopyBuffer = _flow.Room.Code;
            _host.Toast("Room code copied!");
        }

        // ---- flow events -------------------------------------------------------------------------------------------------------------

        void OnStatus(string text)
        {
            _status.text = text;
            _status.gameObject.SetActive(text.Length > 0);
            string kind = LobbyFlow.KindOf(text);
            _status.color = kind == "error" ? MenuKit.Hex("ff9f8e") : kind == "wait" ? MenuKit.Hex("ffc233") : MenuKit.Cream;
        }

        void OnMatchLoading(GDict info)
        {
            _host.Nav.Busy = true;
            _matchLoading?.Invoke(info);
        }

        void OnRoomClosed()
        {
            MenuDialog dialog = MenuDialog.Open(_host, "Room closed", MenuDialog.WidthSmall, sticky: true);
            dialog.AddText("The host left. The battle is over.");
            dialog.AddButton("OK", "orange", () =>
            {
                dialog.Close();
                _host.Nav.Remove(PageId);
                LeaveAndClose();
            });
        }

        void Update()
        {
            _flow?.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Back, or Leave: out of the room (and stop hosting), back to the main screen.</summary>
        void LeaveAndClose()
        {
            if (_closing) return;
            _closing = true;
            _flow.Leave();
            Close();
        }

        /// <summary>The match took over: let go of the client without leaving the room.</summary>
        public void HandOver()
        {
            _closing = true;
            _host.Nav.Remove(PageId);
            Close();
        }

        void Close()
        {
            _flow.Dispose();
            if (this != null) Destroy(gameObject);
            _closed?.Invoke();
        }

        void OnDestroy() => _flow?.Dispose();
    }

    /// <summary>
    /// The room code as boxes (ui_code_boxes.gd): one box a character, the next empty one lit in gold, red when the server refused it.
    /// With an entry it is typed into: a see-through field over the boxes takes the phone's keyboard and keeps only code characters.
    /// </summary>
    public sealed class CodeBoxes
    {
        readonly List<Image> _boxes = new List<Image>();
        readonly List<Text> _letters = new List<Text>();
        readonly CodeEntry _entry;
        readonly InputField _field;
        string _shown = "";

        public CodeBoxes(RectTransform parent, int length, float side, CodeEntry entry)
        {
            _entry = entry;
            float gap = 6f;
            for (int i = 0; i < length; i++)
            {
                Image box = MenuKit.Box("Box" + i, parent, MenuKit.Cream, trim: true);
                MenuKit.Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (side + gap), 0f), new Vector2(side, side * 1.2f));
                Text letter = MenuKit.Title("Letter", box.rectTransform, "", (int)(side * 0.62f), MenuKit.Ink, TextAnchor.MiddleCenter);
                letter.GetComponent<Outline>().enabled = false;
                MenuKit.Stretch(letter.rectTransform);
                _boxes.Add(box);
                _letters.Add(letter);
            }
            if (entry == null) return;
            RectTransform cover = MenuKit.Place(MenuKit.Rect("Typing", parent), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(length * (side + gap), side * 1.2f));
            Image hit = cover.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            _field = cover.gameObject.AddComponent<InputField>();
            Text hidden = MenuKit.Label("Hidden", cover, "", 10, new Color(0f, 0f, 0f, 0f));
            _field.textComponent = hidden;
            _field.targetGraphic = hit;
            _field.characterLimit = length;
            _field.customCaretColor = true;
            _field.caretColor = new Color(0f, 0f, 0f, 0f);
            _field.selectionColor = new Color(0f, 0f, 0f, 0f);
            _field.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            _field.onValidateInput = (text, index, c) =>
            {
                char upper = char.ToUpperInvariant(c);
                return entry.Alphabet.IndexOf(upper) >= 0 ? upper : '\0';
            };
            _field.onValueChanged.AddListener(value =>
            {
                entry.Set(value);
                if (value != entry.Text) _field.SetTextWithoutNotify(entry.Text);
                Refresh();
            });
            Refresh();
        }

        /// <summary>A room's own code (not typed).</summary>
        public void Show(string code)
        {
            _shown = code ?? "";
            for (int i = 0; i < _letters.Count; i++)
            {
                _letters[i].text = i < _shown.Length ? _shown[i].ToString() : "";
                _boxes[i].color = MenuKit.Cream;
            }
        }

        public void Refresh()
        {
            if (_entry == null) return;
            if (_field != null && _field.text != _entry.Text) _field.SetTextWithoutNotify(_entry.Text);
            for (int i = 0; i < _letters.Count; i++)
            {
                _letters[i].text = _entry.Box(i);
                _boxes[i].color = _entry.Error ? MenuKit.Hex("ffb3a8") : i == _entry.Cursor && !_entry.Complete ? MenuKit.Gold : MenuKit.Cream;
            }
        }
    }
}
