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
## The id of the player the preview shows as "you".
const PREVIEW_ME: int = 1
const BIG_FONT: int = 56

var client: GameClient
var intent: Intent = Intent.CREATE
## Capture transport once; deferred callbacks must not read another screen's selection.
var hotspot: bool = false
var _preview_requested: bool = false
## Off in tests: stay on this screen when the match starts loading.
var enter_match_on_start: bool = true
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
var _size_buttons: Array[Button] = []
var _go_button: Button
var _status: Label
var _team_size: int = 1
## The address the lobby connected to (kept so a dropped player can rejoin).
var _connected_url: String = ""
var _finder: HotspotFinder = HotspotFinder.new()
## One room presentation for both transports.
var _room_view: HotspotRoomView
var _announced_character: bool = false
## Preview (web): the made-up room the hotspot screen shows, changed locally by the buttons.
var _preview_room: Dictionary = {}


func _ready() -> void:
	hotspot = Session.lobby_hotspot
	_preview_requested = Session.lobby_preview
	set_process(false)
	if client == null:
		client = Net.client
	_transport_ready = client.is_online()
	_identified_name = client.player_name
	if not _intent_set:
		intent = Session.lobby_intent as Intent
	LobbyWidgets.add_backdrop(self, BACKGROUND)
	if not hotspot:
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


## Tests / callers: choose Create or Join before the screen enters the tree.
func set_intent(value: Intent) -> void:
	intent = value
	_intent_set = true


## Tests: fill in the name and code. `url` stands in for the server the game would ship with.
func set_fields(url: String, player_name: String, code: String = "") -> void:
	if not url.is_empty():
		Settings.server_url = url
	_name_edit.text = player_name
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
		_set_status("Online isn't available yet.")
		return
	_connected_url = url
	if intent == Intent.CREATE:
		_pending = client.create_room.bind(_team_size)
	else:
		var code: String = _code_edit.text.strip_edges().to_upper()
		if code.length() != NET_RULES.passcode_length:
			_code_error.text = "Enter all %d characters." % NET_RULES.passcode_length
			_code_error.show()
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
		_set_status("Connecting...")
		if client.connect_to(url) != OK:
			_set_status("Can't reach that address.")
			if _room_view != null:
				_room_view.show_retry()


## A hotspot room needs no server address and no code. Create starts the room on this phone; Join
## looks for the room on the hotspot or Wi-Fi this phone is on (only the phone that made the
## hotspot can be the host, and everyone else on it can join).
func _submit_hotspot() -> void:
	if not Net.can_host():
		_set_status("Hotspot rooms need the Android app.")
		return
	var addresses: PackedStringArray = IP.get_local_addresses()
	if HotspotNet.local_ip(addresses).is_empty():
		_set_status("Turn on your hotspot or connect to Wi-Fi first.")
		_room_view.show_retry()
		return
	if intent == Intent.CREATE:
		if Net.start_host() != OK:
			_set_status("Can't start the room on this phone.")
			return
		_connected_url = Net.host_url()
		_pending = client.create_room.bind(_team_size)
		_connect_or_send(_connected_url)
		return
	client.close()
	_room_view.clear_room()
	_pending = client.join_room.bind("")
	_set_status("Looking for the room...")
	_finder.start(HotspotNet.candidates(addresses, NET_RULES.default_port))
	set_process(true)


func _process(delta: float) -> void:
	_finder.poll(delta)
	if not _finder.url.is_empty():
		_connected_url = _finder.url
		_finder.url = ""
		set_process(false)
		_connect_or_send(_connected_url)
	elif _finder.failed:
		_finder.failed = false
		set_process(false)
		_set_status(_finder.failure_message)
		_room_view.show_retry()


## Takes our slot in a running match back (after a reload or a long drop).
func rejoin() -> void:
	if Session.reconnect_token.is_empty():
		return
	client.token = Session.reconnect_token
	client.player_name = Session.player_name
	_rejoining = true
	_set_status("Rejoining...")
	if client.connect_to(Session.reconnect_url) != OK:
		_set_status("Can't reach that address.")


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
		_set_status("Connection lost. Try again.")
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
		_set_status("That match has ended.")
		Session.reconnect_token = ""
		Session.save()
		client.token = ""
		if _rejoin_button != null:
			_rejoin_button.visible = false


func _on_error(text: String) -> void:
	_set_status(text)
	if _room_view != null and client.room.is_empty():
		_room_view.show_retry()


func _on_room_changed(room: Dictionary) -> void:
	_show_room(true)
	Session.room_code = room.get("code", "") as String
	Session.reconnect_token = client.token
	Session.reconnect_url = _connected_url
	Session.save()
	_set_status("")
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
	_set_status("Loading...")
	if enter_match_on_start:
		get_tree().change_scene_to_file(MATCH_SCENE)


func _on_leave() -> void:
	_announced_character = false
	client.leave_room()
	if _rejoin_button != null:
		_rejoin_button.visible = false
	Session.reconnect_token = ""
	Session.save()
	_show_room(false)


func _on_back() -> void:
	if not client.room.is_empty():
		client.leave_room()
	_finder.stop()
	client.close()
	Net.stop_host()
	get_tree().change_scene_to_file(TITLE_SCENE)


func _show_room(joined: bool) -> void:
	if _setup_layer != null:
		_setup_layer.visible = not joined
	_room_view.visible = hotspot or joined
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
	_room_view.best_of_chosen.connect(func(count: int) -> void:
		if _preview_room.is_empty():
			client.set_best_of(count)
		else:
			_preview_room["best_of"] = count
			_room_view.show_room(_preview_room, PREVIEW_ME))
	var gate: Node = (load("res://scenes/ui/orientation_gate.tscn") as PackedScene).instantiate()
	add_child(gate)


## Create or Join was pressed on the main screen (its dialog already checked the hotspot): go on.
func _start_hotspot() -> void:
	if not is_inside_tree() or is_queued_for_deletion():
		return
	if _preview_requested:
		LobbyPreview.start(self)
		return
	_set_status("Starting the room..." if intent == Intent.CREATE else "Looking for the room...")
	submit()


func _open_settings() -> void:
	var card: MenuDialog = MenuDialog.create("Settings")
	SettingsPage.fill(card.body)
	var close: Button = MenuDialog.make_button("Done", "blue", 32)
	close.pressed.connect(card.close)
	card.add_buttons([close])
	card.open(self)


func _on_map_chosen(map_id: StringName) -> void:
	if _preview_room.is_empty():
		client.set_map(map_id)
	else:
		_preview_room["map"] = map_id
		_room_view.show_room(_preview_room, PREVIEW_ME)


func _on_size_chosen(size: int) -> void:
	if _preview_room.is_empty():
		client.set_team_size(size)
		return
	_preview_room["team_size"] = size
	LobbyPreview.fit(self, size)
	_room_view.show_room(_preview_room, PREVIEW_ME)


func _on_character_chosen(id: String) -> void:
	if _preview_room.is_empty():
		client.set_character(StringName(id))
		return
	Session.avatar_id = id
	Session.save()
	LobbyPreview.member(self, PREVIEW_ME)["char"] = id
	_room_view.show_room(_preview_room, PREVIEW_ME)


func _on_team_chosen(team: int) -> void:
	if _preview_room.is_empty():
		client.pick_team(team)
		return
	var me: Dictionary = LobbyPreview.member(self, PREVIEW_ME)
	me["team"] = team
	_room_view.show_room(_preview_room, PREVIEW_ME)


## The big button: the host starts, everyone else flips Ready.
func _on_action() -> void:
	if not _preview_room.is_empty():
		_set_status("This is a preview: a real room needs the Android app.")
		return
	if client.is_host():
		client.set_ready(true)
		client.start_match()
		return
	var ready: bool = false
	for member: Dictionary in client.room.get("members", []) as Array:
		if member["player_id"] == client.player_id:
			ready = member["ready"] as bool
	client.set_ready(not ready)


func _current_room() -> Dictionary:
	return _preview_room if not _preview_room.is_empty() else client.room




func _build() -> void:
	LobbyOnlineLayout.new(self).build()


func _set_status(text: String) -> void:
	_status.text = text
	_status.visible = not text.is_empty()


func _exit_tree() -> void:
	_finder.stop()
