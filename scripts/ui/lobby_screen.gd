class_name LobbyScreen
extends Control
## Shared multiplayer lobby. Online joins by code; Hotspot discovers the host room.
## Both use one room view; GameClient and the server own room state.
## Built in code; all networking goes through the GameClient in `Net`.

enum Intent { CREATE, JOIN }

const MATCH_SCENE: String = "res://scenes/match/online.tscn"
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
const BACKGROUND: Texture2D = preload("res://assets/ui/main_bg.webp")
const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")
const FONT: int = UiMetrics.BODY
const BIG_FONT: int = UiMetrics.DISPLAY

var client: GameClient
var intent: Intent = Intent.CREATE
## Capture transport once; deferred callbacks must not read another screen's selection.
var hotspot: bool = false
## Off in tests: stay on this screen when the match starts loading.
var enter_match_on_start: bool = true
## The loading screen laid over the room once the server starts the match (the boot screen's look, `BootScreen.for_scene`): the match
## loads in the background, so the screen and the connection keep running, then it opens.
var loading_screen: BootScreen
var _intent_set: bool = false
var _rejoining: bool = false
var _transport_ready: bool = false
var _identified_name: String = ""
var _rejoin_button: Button
## The action to send once the connection is up.
var _pending: Callable = Callable()

var _setup: VBoxContainer
var _setup_layer: Control
var _setup_status: Label
var _code_error: Label
var _name_edit: LineEdit
var _code_edit: LineEdit
var _code_boxes: UiCodeBoxes
var _size_buttons: Array[Button] = []
var _go_button: Button
var _status: Label
var _team_size: int = 1
## The address the lobby connected to (kept so a dropped player can rejoin).
var _connected_url: String = ""
## One room presentation for both transports.
var _room_view: HotspotRoomView
var _announced_character: bool = false
## Who hosted the room at the last update (-1 before the first), to tell a player they became the host.
var _last_host_id: int = -1


func _ready() -> void:
	hotspot = Session.lobby_hotspot
	if client == null:
		client = Net.client
	_transport_ready = client.is_online()
	_identified_name = client.player_name
	if not _intent_set:
		intent = Session.lobby_intent as Intent
	LobbyWidgets.add_backdrop(self, BACKGROUND)
	# Create needs no form: the room opens at once (the name is your profile's, team size and first to are chosen in the room).
	# Only Join asks for something, the room code.
	if not hotspot and intent == Intent.JOIN:
		_build()
	_build_room_view()
	client.connection_changed.connect(_on_connection_changed)
	client.error_received.connect(_on_error)
	client.room_changed.connect(_on_room_changed)
	client.match_loading.connect(_on_match_loading)
	client.welcomed.connect(_on_welcomed)
	_show_room(not client.room.is_empty())
	if not client.room.is_empty():
		_on_room_changed(client.room)
	elif hotspot:
		_start_hotspot.call_deferred()
	elif intent == Intent.CREATE:
		_start_online_create.call_deferred()


## Tests / callers: choose Create or Join before the screen enters the tree.
func set_intent(value: Intent) -> void:
	intent = value
	_intent_set = true


## Tests: fill in the name and code. `url` stands in for the server the game would ship with.
func set_fields(url: String, player_name: String, code: String = "") -> void:
	if not url.is_empty():
		Settings.server_url = url
	Session.player_name = player_name
	if _code_edit != null:
		_code_edit.text = code


func code_text() -> String:
	return _room_view.code_label.text


func in_room() -> bool:
	return _room_view.visible and _room_view.has_room()


func press_ready() -> void:
	for member: Dictionary in client.room.get("members", []) as Array:
		if member.get("player_id", -1) == client.player_id:
			client.set_ready(not bool(member.get("ready", false)))
			return


func press_start() -> void:
	if start_enabled():
		_on_action()


func start_enabled() -> bool:
	return client.is_host() and in_room() and not _room_view.action_button.disabled


func status_text() -> String:
	return _code_error.text if _code_error != null and _code_error.visible else _status.text


## Create / Join pressed: remember the name and server, connect if needed, then send.
func submit() -> void:
	if _name_edit != null:
		Session.player_name = Session.clean_name(_name_edit.text)
		if Session.player_name.is_empty():
			Session.player_name = "Player"
		Session.save()
	client.player_name = Session.player_name
	if hotspot:
		_submit_hotspot()
		return
	var url: String = Settings.normalize_url(Settings.server_url)
	if url.is_empty():
		# the online server is built into the game; players never type it
		_set_status("Online is coming soon!")
		return
	_connected_url = url
	if intent == Intent.CREATE:
		_pending = client.create_room.bind(_team_size)
	else:
		var code: String = _code_boxes.clean(_code_edit.text)
		if code.length() != NET_RULES.passcode_length:
			_code_error.text = "Enter all %d characters!" % NET_RULES.passcode_length
			_code_error.show()
			UiStatusStrip.style(_code_error, "error")
			_code_boxes.set_error(true)
			_code_edit.grab_focus()
			return
		_code_error.hide()
		_pending = client.join_room.bind(code)
	_connect_or_send(url)


func _connect_or_send(url: String) -> void:
	if client.is_online() and _identified_name == client.player_name:
		if _transport_ready:
			_send_pending()
	else:
		_set_status("Connecting…")
		if client.connect_to(url) != OK:
			_set_status("Can't reach the server. Check your connection and try again.")
			if _room_view != null:
				_room_view.show_retry()


## A hotspot room needs no server address and no code, and the dialog on the main screen has already checked the hotspot
## or the Wi-Fi. Create starts the room on this phone; Join goes to the room that dialog found and verified.
func _submit_hotspot() -> void:
	if intent == Intent.CREATE:
		if Net.start_host() != OK:
			_set_status("Oops! This phone can't start the room.")
			_room_view.show_retry()
			return
		_connected_url = Net.host_url()
		_pending = client.create_room.bind(_team_size)
		_connect_or_send(_connected_url)
		return
	if Session.lobby_url.is_empty():
		_set_status("No room found. Go back and tap Join room.")
		return
	client.close()
	_room_view.clear_room()
	_connected_url = Session.lobby_url
	_pending = client.join_room.bind("")
	_connect_or_send(_connected_url)


## Takes our slot in a running match back (after a reload or a long drop).
func rejoin() -> void:
	if Session.reconnect_token.is_empty():
		return
	client.token = Session.reconnect_token
	client.player_name = Session.player_name
	_rejoining = true
	_set_status("Rejoining…")
	if client.connect_to(Session.reconnect_url) != OK:
		_set_status("Can't reach the server. Check your connection and try again.")


func choose_size(size: int) -> void:
	_team_size = size
	for i: int in _size_buttons.size():
		_size_buttons[i].button_pressed = i + 1 == size


func _send_pending() -> void:
	if _pending.is_valid():
		_set_status("")
		_pending.call()
		_pending = Callable()


func _on_connection_changed(online: bool) -> void:
	_transport_ready = false
	if online:
		_set_status("Connecting…")
	else:
		_show_room(false)
		_set_status("Connection lost. Try again!")
		if _room_view != null:
			_room_view.show_retry()


func _on_welcomed(reconnected: bool) -> void:
	_transport_ready = true
	_identified_name = client.player_name
	_send_pending()
	if not _rejoining:
		return
	_rejoining = false
	if not reconnected:
		_set_status("That battle is over!")
		Session.reconnect_token = ""
		Session.save()
		client.token = ""
		if _rejoin_button != null:
			_rejoin_button.visible = false


func _on_error(text: String) -> void:
	_set_status(text)
	# a refused join marks the code itself: the boxes turn red until it is edited
	if _code_boxes != null and client.room.is_empty():
		_code_boxes.set_error(true)
		_code_error.text = text
		_code_error.show()
		UiStatusStrip.style(_code_error, UiStatusStrip.kind_of(text))
		if _status == _setup_status:
			_status.text = ""
			_status.visible = false
	if _room_view != null and client.room.is_empty():
		_room_view.show_retry()


func _on_room_changed(room: Dictionary) -> void:
	_show_room(true)
	Session.room_code = room.get("code", "") as String
	Session.reconnect_token = client.token
	Session.reconnect_url = _connected_url
	Session.save()
	_set_status("")
	var host_id: int = room.get("host", -1) as int
	if _last_host_id >= 0 and host_id == client.player_id and _last_host_id != client.player_id:
		UiToast.say(self, "The host left. You're the host now!", "info")
	_last_host_id = host_id
	_room_view.show_room(room, client.player_id)
	if not _announced_character:
		_announced_character = true
		client.set_character(StringName(Session.avatar_id))
	else:
		for member: Dictionary in room.get("members", []) as Array:
			if member.get("player_id", -1) == client.player_id:
				Session.avatar_id = String(member.get("char", Session.avatar_id))
				Session.save()


func _on_match_loading(_info: Dictionary) -> void:
	_set_status("Loading…")
	if enter_match_on_start and loading_screen == null:
		loading_screen = BootScreen.for_scene(MATCH_SCENE)
		add_child(loading_screen)
		loading_screen.finished.connect(func() -> void: get_tree().change_scene_to_packed(loading_screen.scene()))


func _on_leave() -> void:
	_last_host_id = -1
	_announced_character = false
	client.leave_room()
	if _rejoin_button != null:
		_rejoin_button.visible = false
	Session.reconnect_token = ""
	Session.save()
	_show_room(false)


## The phone's back button leaves the lobby, unless a dialog is open (it closes itself). Deferred: see MenuDialog.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		_back_pressed.call_deferred()


func _back_pressed() -> void:
	# the match is already starting for everyone: Back waits for it (it then opens the match's own menu)
	if loading_screen != null:
		return
	if find_children("*", "MenuDialog", true, false).any(func(node: Node) -> bool: return (node as MenuDialog).is_open):
		return
	_on_back()


func _on_back() -> void:
	if not client.room.is_empty():
		client.leave_room()
	client.close()
	Net.stop_host()
	get_tree().change_scene_to_file(TITLE_SCENE)


func _show_room(joined: bool) -> void:
	if _setup_layer != null:
		_setup_layer.visible = not joined
	# a hotspot or an online Create has no form: the room view carries the status until the room opens
	_room_view.visible = hotspot or joined or intent == Intent.CREATE
	_status = _room_view.status_label if _room_view.visible else _setup_status
	if not joined:
		_room_view.clear_room()


## Shared room view: only Online exposes its room code.
func _build_room_view() -> void:
	_room_view = HotspotRoomView.new()
	add_child(_room_view)
	_room_view.set_online(not hotspot)
	_status = _room_view.status_label
	_room_view.back_pressed.connect(_on_back)
	_room_view.settings_pressed.connect(_open_settings)
	_room_view.map_chosen.connect(_on_map_chosen)
	_room_view.size_chosen.connect(_on_size_chosen)
	_room_view.character_chosen.connect(_on_character_chosen)
	_room_view.team_chosen.connect(_on_team_chosen)
	_room_view.action_pressed.connect(_on_action)
	_room_view.retry_pressed.connect(submit)
	_room_view.first_to_chosen.connect(client.set_first_to)


## Create or Join was pressed on the main screen (its dialog already checked the hotspot): go on.
func _start_online_create() -> void:
	if not is_inside_tree() or is_queued_for_deletion():
		return
	_set_status("Creating the room…")
	submit()


func _start_hotspot() -> void:
	if not is_inside_tree() or is_queued_for_deletion():
		return
	_set_status("Starting the room…" if intent == Intent.CREATE else "Joining the room…")
	submit()


func _open_settings() -> void:
	var card: MenuDialog = MenuDialog.create("Settings", MenuDialog.WIDTH_SHEET, UiTabBar.picture(&"settings"))
	SettingsPage.fill(card.body)
	card.open(self)


func _on_map_chosen(map_id: StringName) -> void:
	client.set_map(map_id)


func _on_size_chosen(size: int) -> void:
	client.set_team_size(size)


func _on_character_chosen(id: String) -> void:
	client.set_character(StringName(id))


func _on_team_chosen(team: int) -> void:
	client.pick_team(team)


## The big button: the host starts, everyone else flips Ready.
func _on_action() -> void:
	if client.is_host():
		client.set_ready(true)
		client.start_match()
		return
	var ready: bool = false
	for member: Dictionary in client.room.get("members", []) as Array:
		if member["player_id"] == client.player_id:
			ready = member["ready"] as bool
	client.set_ready(not ready)


func _build() -> void:
	LobbyOnlineLayout.new(self).build()


## Empties the code boxes (and their error), to start the code again.
func clear_code() -> void:
	if _code_boxes == null:
		return
	_code_boxes.set_text("")
	_code_boxes.set_error(false)
	if _code_error != null:
		_code_error.hide()
	_code_edit.grab_focus()


## Fills the boxes from the clipboard (the code a friend sent): capitals only, the code alphabet only. `clipboard`
## replaces the real clipboard (tests).
func paste_code(clipboard: Variant = null) -> void:
	if _code_boxes == null:
		return
	var raw: String = clipboard as String if clipboard is String else DisplayServer.clipboard_get()
	var value: String = _code_boxes.clean(raw)
	if value.is_empty():
		UiToast.say(self, "Nothing to paste yet. Copy the room code first!", "info")
		return
	_code_boxes.set_text(value)
	_code_boxes.set_error(false)
	_code_error.hide()
	if value.length() == NET_RULES.passcode_length:
		UiToast.say(self, "Code pasted!")
	else:
		UiToast.say(self, "Pasted %d of %d characters" % [value.length(), NET_RULES.passcode_length], "warn")


func _set_status(text: String) -> void:
	_status.text = text
	_status.visible = not text.is_empty()
	if not text.is_empty():
		UiStatusStrip.style(_status, UiStatusStrip.kind_of(text))
