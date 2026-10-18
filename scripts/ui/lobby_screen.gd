class_name LobbyScreen
extends Control
## Online lobby (portrait): server address and name, then Create (1v1 / 2v2 / 3v3)
## or Join (8-character alphanumeric code), then the room: two team columns, best of 3 / 5 / 7
## (the host picks, everyone sees it), Ready, Start (host) and Leave. When the host starts, it switches to the online match scene.
## Built in code; all networking goes through the GameClient in `Net`.

enum Intent { CREATE, JOIN }

const MATCH_SCENE: String = "res://scenes/match/online.tscn"
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
const BACKGROUND: Texture2D = preload("res://assets/ui/main_bg.webp")
const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")
const TEAM_NAMES: Array[String] = ["BLUE TEAM", "RED TEAM"]
const TEAM_COLORS: Array[Color] = [Color(0.45, 0.65, 1.0), Color(1.0, 0.45, 0.45)]
const FONT: int = 28
const BIG_FONT: int = 56

var client: GameClient
var intent: Intent = Intent.CREATE
## Off in tests: stay on this screen when the match starts loading.
var enter_match_on_start: bool = true
var _intent_set: bool = false
var _rejoining: bool = false
var _rejoin_button: Button
## The action to send once the connection is up.
var _pending: Callable = Callable()

var _setup: VBoxContainer
var _room_box: VBoxContainer
var _url_edit: LineEdit
var _name_edit: LineEdit
var _code_edit: LineEdit
var _size_buttons: Array[Button] = []
var _go_button: Button
var _status: Label
var _code_label: Label
var _team_lists: Array[VBoxContainer] = []
var _team_buttons: Array[Button] = []
var _ready_button: Button
var _start_button: Button
var _best_of_label: Label
var _best_of_buttons: Dictionary[int, Button] = {}
var _team_size: int = 1


func _ready() -> void:
	if client == null:
		client = Net.client
	if not _intent_set:
		intent = Session.lobby_intent as Intent
	_build()
	client.connection_changed.connect(_on_connection_changed)
	client.error_received.connect(_on_error)
	client.room_changed.connect(_on_room_changed)
	client.match_loading.connect(_on_match_loading)
	client.welcomed.connect(_on_welcomed)
	_show_room(not client.room.is_empty())
	if not client.room.is_empty():
		_on_room_changed(client.room)


## Tests / callers: choose Create or Join before the screen enters the tree.
func set_intent(value: Intent) -> void:
	intent = value
	_intent_set = true


func set_fields(url: String, player_name: String, code: String = "") -> void:
	_url_edit.text = url
	_name_edit.text = player_name
	if _code_edit != null:
		_code_edit.text = code


func code_text() -> String:
	return _code_label.text


func in_room() -> bool:
	return _room_box.visible


func press_ready() -> void:
	_ready_button.button_pressed = not _ready_button.button_pressed
	client.set_ready(_ready_button.button_pressed)


func press_start() -> void:
	if _start_button.visible and not _start_button.disabled:
		client.start_match()


func start_enabled() -> bool:
	return _start_button.visible and not _start_button.disabled


func status_text() -> String:
	return _status.text


## Create / Join pressed: remember the name and server, connect if needed, then send.
func submit() -> void:
	var url: String = Settings.normalize_url(_url_edit.text)
	if url.is_empty():
		_status.text = "Enter a server address."
		return
	if Session.lobby_hotspot:
		Settings.save_hotspot_url(url)
	else:
		Settings.save_server_url(url)
	Session.player_name = _name_edit.text.strip_edges()
	Session.save()
	client.player_name = Session.player_name
	if intent == Intent.CREATE:
		_pending = client.create_room.bind(_team_size)
	else:
		var code: String = _code_edit.text.strip_edges().to_upper()
		if code.length() != NET_RULES.passcode_length:
			_status.text = "Room codes have %d characters." % NET_RULES.passcode_length
			return
		_pending = client.join_room.bind(code)
	if client.is_online():
		_send_pending()
	else:
		_status.text = "Connecting..."
		if client.connect_to(url) != OK:
			_status.text = "Can't reach that address."


## Takes our slot in a running match back (after a reload or a long drop).
func rejoin() -> void:
	if Session.reconnect_token.is_empty():
		return
	client.token = Session.reconnect_token
	client.player_name = Session.player_name
	_rejoining = true
	_status.text = "Rejoining..."
	if client.connect_to(Session.reconnect_url) != OK:
		_status.text = "Can't reach that address."


func choose_size(size: int) -> void:
	_team_size = size
	for i: int in _size_buttons.size():
		_size_buttons[i].button_pressed = i + 1 == size


func _send_pending() -> void:
	if _pending.is_valid():
		_status.text = ""
		_pending.call()
		_pending = Callable()


func _on_connection_changed(online: bool) -> void:
	if online:
		_send_pending()
	else:
		_status.text = "Disconnected from the server."
		_show_room(false)


func _on_welcomed(reconnected: bool) -> void:
	if not _rejoining:
		return
	_rejoining = false
	if not reconnected:
		_status.text = "That match has ended."
		Session.reconnect_token = ""
		Session.save()
		client.token = ""
		_rejoin_button.visible = false


func _on_error(text: String) -> void:
	_status.text = text


func _on_room_changed(room: Dictionary) -> void:
	_show_room(true)
	Session.room_code = room.get("code", "") as String
	Session.reconnect_token = client.token
	Session.reconnect_url = Settings.lobby_url(Session.lobby_hotspot)
	Session.save()
	var size: int = room.get("team_size", 1) as int
	_code_label.text = Session.room_code
	var members: Array = room.get("members", []) as Array
	for team: int in 2:
		var list: VBoxContainer = _team_lists[team]
		for child: Node in list.get_children():
			child.free()
		var count: int = 0
		for member: Dictionary in members:
			if member["team"] != team:
				continue
			count += 1
			var line: String = member["name"] as String
			var tags: PackedStringArray = PackedStringArray()
			if member["player_id"] == client.player_id:
				tags.append("YOU")
			if member["player_id"] == room.get("host", -1):
				tags.append("HOST")
			tags.append("READY" if member["ready"] else "WAITING")
			line += "  •  " + "  •  ".join(tags)
			list.add_child(LobbyWidgets.label(line, 24, TEAM_COLORS[team]))
		for empty: int in size - count:
			list.add_child(LobbyWidgets.label("OPEN SLOT", 22, Color(1, 1, 1, 0.42)))
		_team_buttons[team].disabled = client.my_team() == team or count >= size
	var me_ready: bool = false
	for member: Dictionary in members:
		if member["player_id"] == client.player_id:
			me_ready = member["ready"] as bool
	_ready_button.button_pressed = me_ready
	_ready_button.text = "READY" if me_ready else "READY UP"
	var best_of: int = room.get("best_of", 3) as int
	_best_of_label.text = "BEST OF %d  •  FIRST TO %d" % [best_of, best_of / 2 + 1]
	for count: int in _best_of_buttons:
		var button: Button = _best_of_buttons[count]
		button.visible = client.is_host()
		button.set_pressed_no_signal(count == best_of)
	_start_button.visible = client.is_host()
	_start_button.disabled = not (room.get("can_start", false) as bool)


func _on_match_loading(_info: Dictionary) -> void:
	_status.text = "Loading..."
	if enter_match_on_start:
		get_tree().change_scene_to_file(MATCH_SCENE)


func _on_leave() -> void:
	client.leave_room()
	_rejoin_button.visible = false
	Session.reconnect_token = ""
	Session.save()
	_show_room(false)


func _on_back() -> void:
	if not client.room.is_empty():
		client.leave_room()
	client.close()
	get_tree().change_scene_to_file(TITLE_SCENE)


func _show_room(in_room: bool) -> void:
	_setup.visible = not in_room
	_room_box.visible = in_room


func _build() -> void:
	LobbyWidgets.add_backdrop(self, BACKGROUND)
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_right", 20)
	margin.add_theme_constant_override("margin_top", 28)
	margin.add_theme_constant_override("margin_bottom", 24)
	add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 16)
	margin.add_child(column)
	column.add_child(LobbyWidgets.header("CREATE ROOM" if intent == Intent.CREATE else "JOIN ROOM", _on_back))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "LobbyScroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var card: PanelContainer = PanelContainer.new()
	card.name = "LobbyCard"
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(20.0))
	scroll.add_child(card)
	var content: VBoxContainer = VBoxContainer.new()
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", 16)
	card.add_child(content)
	_build_setup(content)
	_build_room(content)
	_status = LobbyWidgets.label("", 24, Color("ffb4a8"))
	_status.name = "Status"
	_status.custom_minimum_size = Vector2(0.0, 30.0)
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(_status)
	var gate: Node = (load("res://scenes/ui/orientation_gate.tscn") as PackedScene).instantiate()
	add_child(gate)


func _build_setup(parent: Control) -> void:
	_setup = VBoxContainer.new()
	_setup.add_theme_constant_override("separation", 10)
	parent.add_child(_setup)
	_setup.add_child(LobbyWidgets.field_label("ADDRESS" if Session.lobby_hotspot else "SERVER"))
	_url_edit = LobbyWidgets.edit(Settings.lobby_url(Session.lobby_hotspot), "192.168.43.1:9080" if Session.lobby_hotspot else "wss://your-server.example.com", FONT)
	_setup.add_child(_url_edit)
	_setup.add_child(LobbyWidgets.field_label("NAME"))
	_name_edit = LobbyWidgets.edit(Session.player_name, "Player", FONT)
	_name_edit.max_length = 12
	_setup.add_child(_name_edit)
	if intent == Intent.CREATE:
		_setup.add_child(LobbyWidgets.field_label("TEAM SIZE"))
		var sizes: HBoxContainer = HBoxContainer.new()
		sizes.add_theme_constant_override("separation", 8)
		_setup.add_child(sizes)
		for size: int in [1, 2, 3]:
			var button: Button = LobbyWidgets.button("%dv%d" % [size, size], choose_size.bind(size), "grey")
			LobbyWidgets.make_toggle(button)
			button.custom_minimum_size = Vector2(0.0, 66.0)
			button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			sizes.add_child(button)
			_size_buttons.append(button)
		choose_size(1)
	else:
		_setup.add_child(LobbyWidgets.field_label("ROOM CODE"))
		_code_edit = LobbyWidgets.edit("", "ABC234XY", FONT)
		_code_edit.max_length = NET_RULES.passcode_length
		_code_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
		_code_edit.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
		_code_edit.add_theme_font_size_override("font_size", BIG_FONT)
		_code_edit.text_changed.connect(func(text: String) -> void:
			var caret: int = _code_edit.caret_column
			_code_edit.text = text.to_upper()
			_code_edit.caret_column = caret)
		_setup.add_child(_code_edit)
	_go_button = LobbyWidgets.button("CREATE" if intent == Intent.CREATE else "JOIN", submit, "orange")
	_go_button.custom_minimum_size = Vector2(0, 96)
	_setup.add_child(_go_button)
	_rejoin_button = LobbyWidgets.button("REJOIN", rejoin, "blue")
	_rejoin_button.visible = not Session.reconnect_token.is_empty()
	_setup.add_child(_rejoin_button)


func _build_room(parent: Control) -> void:
	_room_box = VBoxContainer.new()
	_room_box.add_theme_constant_override("separation", 12)
	parent.add_child(_room_box)
	var code_hint: Label = LobbyWidgets.label("SHARE WITH FRIENDS", 20, KalyeahTheme.TEXT_MUTED)
	code_hint.theme_type_variation = &"MutedLabel"
	_room_box.add_child(code_hint)
	_code_label = LobbyWidgets.label("", BIG_FONT, KalyeahTheme.AMBER)
	_code_label.theme_type_variation = &"DisplayLabel"
	_room_box.add_child(_code_label)
	for team: int in 2:
		var team_card: PanelContainer = PanelContainer.new()
		var team_style: StyleBoxFlat = KalyeahTheme.surface_box(16, 2, 14.0)
		team_style.bg_color = Color(KalyeahTheme.INK, 0.72)
		team_style.border_color = Color(TEAM_COLORS[team], 0.72)
		team_style.shadow_size = 3
		team_card.add_theme_stylebox_override("panel", team_style)
		_room_box.add_child(team_card)
		var team_column: VBoxContainer = VBoxContainer.new()
		team_column.add_theme_constant_override("separation", 8)
		team_card.add_child(team_column)
		var header: HBoxContainer = HBoxContainer.new()
		team_column.add_child(header)
		var name_label: Label = LobbyWidgets.label(TEAM_NAMES[team], FONT, TEAM_COLORS[team])
		name_label.theme_type_variation = &"SectionLabel"
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
		header.add_child(name_label)
		var join_team: Button = LobbyWidgets.button("JOIN", client.pick_team.bind(team), "blue" if team == 0 else "red")
		join_team.custom_minimum_size = Vector2(104.0, 56.0)
		header.add_child(join_team)
		_team_buttons.append(join_team)
		var list: VBoxContainer = VBoxContainer.new()
		list.add_theme_constant_override("separation", 4)
		team_column.add_child(list)
		_team_lists.append(list)
	_best_of_label = LobbyWidgets.label("", FONT, KalyeahTheme.CREAM)
	_room_box.add_child(_best_of_label)
	var counts: HBoxContainer = HBoxContainer.new()
	counts.add_theme_constant_override("separation", 8)
	_room_box.add_child(counts)
	for count: int in [3, 5, 7]:
		var choice: Button = LobbyWidgets.button("BO%d" % count, client.set_best_of.bind(count), "grey", 25)
		LobbyWidgets.make_toggle(choice)
		choice.custom_minimum_size = Vector2(0.0, 62.0)
		choice.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		counts.add_child(choice)
		_best_of_buttons[count] = choice
	_ready_button = LobbyWidgets.button("READY UP", func() -> void: client.set_ready(_ready_button.button_pressed), "grey")
	LobbyWidgets.make_toggle(_ready_button)
	_ready_button.custom_minimum_size = Vector2(0, 96)
	_room_box.add_child(_ready_button)
	_start_button = LobbyWidgets.button("START MATCH", client.start_match, "orange")
	_start_button.custom_minimum_size = Vector2(0, 96)
	_room_box.add_child(_start_button)
	_room_box.add_child(LobbyWidgets.button("LEAVE ROOM", _on_leave, "red"))
