class_name LobbyScreen
extends Control
## Online lobby (portrait): server address and name, then Create (1v1 / 2v2 / 3v3)
## or Join (6-letter code), then the room: two team columns, best of 3 / 5 / 7
## (the host picks, everyone sees it), Ready, Start (host) and Leave. When the host starts, it switches to the online match scene.
## Built in code; all networking goes through the GameClient in `Net`.

enum Intent { CREATE, JOIN }

const MATCH_SCENE: String = "res://scenes/match/online.tscn"
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
const TEAM_NAMES: Array[String] = ["Blue team", "Red team"]
const TEAM_COLORS: Array[Color] = [Color(0.45, 0.65, 1.0), Color(1.0, 0.45, 0.45)]
const FONT: int = Hig.BODY
const BIG_FONT: int = 64

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
var _best_of_rail: Control
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
		_status.text = "Enter the game server address first\\n(see server/README.md)."
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
		if code.length() != 6:
			_status.text = "Room codes have 6 letters."
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
	_code_label.text = "%s   (%dv%d)" % [Session.room_code, size, size]
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
			if member["player_id"] == room.get("host", -1):
				line += " (host)"
			if member["player_id"] == client.player_id:
				line += " - you"
			line += "   READY" if member["ready"] else "   ..."
			list.add_child(_member_row(line, TEAM_COLORS[team]))
		for empty: int in size - count:
			list.add_child(_member_row("Open slot", Hig.SECONDARY))
		_team_buttons[team].disabled = client.my_team() == team or count >= size
	var me_ready: bool = false
	for member: Dictionary in members:
		if member["player_id"] == client.player_id:
			me_ready = member["ready"] as bool
	_ready_button.button_pressed = me_ready
	_ready_button.text = "READY!" if me_ready else "Tap when you're ready"
	var best_of: int = room.get("best_of", 3) as int
	_best_of_label.text = "Best of %d - first to %d points wins" % [best_of, best_of / 2 + 1]
	for count: int in _best_of_buttons:
		var button: Button = _best_of_buttons[count]
		button.set_pressed_no_signal(count == best_of)
	_best_of_rail.visible = client.is_host()
	_start_button.visible = client.is_host()
	_start_button.disabled = not (room.get("can_start", false) as bool)


func _on_match_loading(_info: Dictionary) -> void:
	_status.text = "Loading the match..."
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
	_build_background()
	var inset: Vector4 = Hig.safe_insets(get_viewport())
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", int(Hig.MARGIN + inset.x))
	margin.add_theme_constant_override("margin_right", int(Hig.MARGIN + inset.z))
	margin.add_theme_constant_override("margin_top", int(Hig.MARGIN + maxf(inset.y, 24.0)))
	margin.add_theme_constant_override("margin_bottom", int(Hig.MARGIN + inset.w))
	add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", int(Hig.GAP))
	margin.add_child(column)
	column.add_child(Hig.nav_bar("Create room" if intent == Intent.CREATE else "Join room", _on_back))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var body: VBoxContainer = VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", int(Hig.GAP * 1.5))
	scroll.add_child(body)
	_setup = VBoxContainer.new()
	_setup.add_theme_constant_override("separation", int(Hig.GAP * 1.5))
	body.add_child(_setup)
	_build_setup()
	_room_box = VBoxContainer.new()
	_room_box.add_theme_constant_override("separation", int(Hig.GAP * 1.5))
	body.add_child(_room_box)
	_build_room()
	_status = Hig.footnote("", Color(1.0, 0.78, 0.5))
	column.add_child(_status)
	var gate: Node = (load("res://scenes/ui/orientation_gate.tscn") as PackedScene).instantiate()
	add_child(gate)


## The same blue diamond picture as the main screen, calmed down so the forms read first.
func _build_background() -> void:
	var picture: TextureRect = TextureRect.new()
	picture.name = "Background"
	picture.texture = TitleScreen.BACKGROUND
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(picture)
	var calm: ColorRect = ColorRect.new()
	calm.color = Color(0.02, 0.04, 0.13, 0.7)
	calm.set_anchors_preset(Control.PRESET_FULL_RECT)
	calm.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(calm)


## Before the room: server, name, then Create's mode or Join's code, and the one primary button.
func _build_setup() -> void:
	_setup.add_child(_section("Hotspot address" if Session.lobby_hotspot else "Game server"))
	var server: PanelContainer = Hig.group()
	_url_edit = _edit(Settings.lobby_url(Session.lobby_hotspot), "192.168.43.1:9080" if Session.lobby_hotspot else "wss://your-server.example.com")
	Hig.rows(server).add_child(_url_edit)
	_setup.add_child(server)
	_setup.add_child(_section("Your name"))
	var who: PanelContainer = Hig.group()
	_name_edit = _edit(Session.player_name, "Player")
	_name_edit.max_length = 12
	Hig.rows(who).add_child(_name_edit)
	_setup.add_child(who)
	if intent == Intent.CREATE:
		_setup.add_child(_section("Mode"))
		for size: int in [1, 2, 3]:
			var button: Button = _button("%dv%d" % [size, size], choose_size.bind(size))
			_size_buttons.append(button)
		_setup.add_child(Hig.segmented(_size_buttons))
		choose_size(1)
	else:
		_setup.add_child(_section("Room code"))
		var code: PanelContainer = Hig.group()
		_code_edit = _edit("", "ABC234")
		_code_edit.max_length = 6
		_code_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
		_code_edit.add_theme_font_size_override("font_size", BIG_FONT)
		_code_edit.custom_minimum_size = Vector2(0.0, 110.0)
		_code_edit.text_changed.connect(func(text: String) -> void:
			var caret: int = _code_edit.caret_column
			_code_edit.text = text.to_upper()
			_code_edit.caret_column = caret)
		Hig.rows(code).add_child(_code_edit)
		_setup.add_child(code)
		_setup.add_child(Hig.footnote("Six letters or numbers from the friend who made the room."))
	_go_button = _primary("Create" if intent == Intent.CREATE else "Join", submit)
	_setup.add_child(_go_button)
	_rejoin_button = Hig.text_button("Rejoin last match")
	_rejoin_button.pressed.connect(rejoin)
	_rejoin_button.visible = not Session.reconnect_token.is_empty()
	_setup.add_child(_rejoin_button)


## In the room: the code to share, the two teams (a group each), best of, then Ready / Start.
func _build_room() -> void:
	_room_box.add_child(_section("Room code"))
	var code_card: PanelContainer = Hig.group()
	_code_label = Hig.label("", BIG_FONT, Color(1.0, 0.86, 0.3), HORIZONTAL_ALIGNMENT_CENTER)
	Hig.rows(code_card).add_child(_code_label)
	_room_box.add_child(code_card)
	_room_box.add_child(Hig.footnote("Share it with your squad."))
	for team: int in 2:
		_room_box.add_child(_section(TEAM_NAMES[team], TEAM_COLORS[team]))
		var card: PanelContainer = Hig.group()
		var list: VBoxContainer = VBoxContainer.new()
		list.add_theme_constant_override("separation", 4)
		Hig.rows(card).add_child(list)
		_team_lists.append(list)
		var join_team: Button = Hig.text_button("Join %s" % TEAM_NAMES[team].to_lower(), TEAM_COLORS[team])
		join_team.pressed.connect(client.pick_team.bind(team))
		Hig.rows(card).add_child(join_team)
		_team_buttons.append(join_team)
		_room_box.add_child(card)
	_room_box.add_child(_section("Match length"))
	var counts: Array[Button] = []
	for count: int in [3, 5, 7]:
		var choice: Button = _button("Best of %d" % count, client.set_best_of.bind(count))
		_best_of_buttons[count] = choice
		counts.append(choice)
	_best_of_rail = Hig.segmented(counts)
	_room_box.add_child(_best_of_rail)
	_best_of_label = Hig.footnote("")
	_room_box.add_child(_best_of_label)
	_ready_button = _primary("Tap when you're ready", func() -> void: client.set_ready(_ready_button.button_pressed))
	_make_toggle(_ready_button)
	_room_box.add_child(_ready_button)
	_start_button = _primary("Start match", client.start_match)
	_room_box.add_child(_start_button)
	var leave: Button = Hig.text_button("Leave room", Hig.DESTRUCTIVE)
	leave.pressed.connect(_on_leave)
	_room_box.add_child(leave)


func _section(text: String, color: Color = Hig.SECONDARY) -> Label:
	var header: Label = Hig.section_header(text)
	header.add_theme_color_override("font_color", color)
	return header


## A person in a team's group.
func _member_row(text: String, color: Color) -> Label:
	var row: Label = Hig.label(text, Hig.BODY, color)
	row.custom_minimum_size = Vector2(0.0, 64.0)
	row.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	return row


func _edit(text: String, placeholder: String) -> LineEdit:
	var edit: LineEdit = LineEdit.new()
	edit.text = text
	edit.placeholder_text = placeholder
	edit.custom_minimum_size = Vector2(0.0, Hig.MIN_TAP + 12.0)
	edit.add_theme_font_size_override("font_size", Hig.BODY)
	var field: StyleBoxFlat = StyleBoxFlat.new()
	field.bg_color = Color(1.0, 1.0, 1.0, 0.1)
	field.set_corner_radius_all(Hig.RADIUS - 8)
	field.set_content_margin_all(Hig.GAP)
	edit.add_theme_stylebox_override("normal", field)
	var editing: StyleBoxFlat = field.duplicate() as StyleBoxFlat
	editing.border_color = Hig.TINT
	editing.set_border_width_all(3)
	edit.add_theme_stylebox_override("focus", editing)
	edit.add_theme_color_override("font_color", Hig.LABEL)
	edit.add_theme_color_override("caret_color", Hig.TINT)
	edit.add_theme_color_override("font_placeholder_color", Color(Hig.SECONDARY, 0.55))
	return edit


## The ready toggle lights up yellow when on.
func _make_toggle(button: Button) -> void:
	button.toggle_mode = true
	var on: StyleBoxFlat = KalyeahTheme.button_box(WeaponPickScreen.PICKED_COLOR, true)
	button.add_theme_stylebox_override("pressed", on)
	button.add_theme_stylebox_override("hover_pressed", on)
	for state: String in ["font_pressed_color", "font_hover_pressed_color"]:
		button.add_theme_color_override(state, Color.BLACK)


## The screen's one primary action: the painted game button, big.
func _primary(text: String, action: Callable) -> Button:
	var button: Button = Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(0.0, 104.0)
	button.add_theme_font_size_override("font_size", 38)
	button.pressed.connect(action)
	return button


## A segment of a segmented control (see Hig.segmented).
func _button(text: String, action: Callable) -> Button:
	var button: Button = Button.new()
	button.text = text
	button.pressed.connect(action)
	return button
