class_name HotspotRoomView
extends Control
## Shared Online/Hotspot room: arena, teams, character selection and readiness.
## Presentation only; LobbyScreen sends changes to the authoritative server.

signal retry_pressed
signal first_to_chosen(count: int)
signal back_pressed
signal settings_pressed
## The host picked a map in the carousel.
signal map_chosen(map_id: StringName)
signal size_chosen(size: int)
signal character_chosen(character_id: String)
signal team_chosen(team: int)
## The big button: START for the host, READY (on or off) for everyone else.
signal action_pressed

const MAP_NAMES: Dictionary[StringName, String] = {&"street": "Agawan Base", &"tumbang_preso": "Tumbang Preso", &"patintero": "Patintero"}
const MAP_COLORS: Dictionary[StringName, String] = {&"street": "blue", &"tumbang_preso": "orange", &"patintero": "green"}
## The cards in the map carousel are 3:4.
const MAP_PICK_CARD: Vector2 = Vector2(414.0, 552.0)
const TEAM_COLORS: Array[Color] = [Color(0.45, 0.65, 1.0), Color(1.0, 0.45, 0.45)]
## Spacing and type scale of the screen: one set of values so it reads as one system.
const MARGIN: float = UiMetrics.PAGE_MARGIN
const GAP: float = 16.0
const SECTION_GAP: int = UiMetrics.SECTION_GAP
## The compact arena header fills a row; character cards keep a 3:4 portrait ratio.
const MAP_CARD: Vector2 = Vector2(0.0, 108.0)
const KID_CARD: Vector2 = Vector2(96.0, 128.0)
const KID_RADIUS: int = 16
const BAR_HEIGHT: float = 96.0
const NUMBER_WIDTH: float = 56.0
const NAME_SIZE: int = 26
const NUMBER_SIZE: int = 32
const TAG_SIZE: int = 22
## The solid number blocks and tags of the mock-up, and the navy of the names on the white bars.
const TEAM_BLOCKS: Array[Color] = [KalyeahTheme.TEAM_BLUE, KalyeahTheme.TEAM_RED]
const NAME_COLOR: Color = Color(0.12, 0.27, 0.52)
const MAX_ROWS: int = 3

var online_room: bool = false
var code_label: Label
var code_boxes: UiCodeBoxes
var code_row: HBoxContainer
var copy_button: Button
var _copy_timer: Timer
var back_button: Button
var settings_button: Button
var map_card: Button
var map_carousel: MenuCarousel
var _map_overlay: Control
var _map_hint: Label
var _map_picker: RoomMapPicker
var size_buttons: Dictionary[int, Button] = {}
var player_grid: GridContainer
var player_cards: Array[Control] = []
var kid_cards: Dictionary[String, Button] = {}
var first_to_buttons: Dictionary[int, Button] = {}
var retry_button: Button
var readiness_label: Label
var format_summary: Label
var _kid_scroll: ScrollContainer
var _last_kid: String = ""
var action_button: Button
var status_label: Label

var _room: Dictionary = {}
var _my_id: int = -1
var _content: Control
var _safe_margin: MarginContainer


func _init() -> void:
	name = "HotspotRoomView"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()


func _ready() -> void:
	LobbyWidgets.apply_safe_area(_safe_margin, MARGIN, 20.0)


func _build() -> void:
	var margin: MarginContainer = MarginContainer.new()
	_safe_margin = margin
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right"]:
		margin.add_theme_constant_override("margin_" + side, int(MARGIN))
	margin.add_theme_constant_override("margin_top", 20)
	margin.add_theme_constant_override("margin_bottom", 20)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(margin)
	margin.resized.connect(LobbyWidgets.apply_safe_area.bind(margin, MARGIN, 20.0))
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", SECTION_GAP)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(column)
	column.add_child(_build_top_row())
	_content = VBoxContainer.new()
	_content.name = "Content"
	_content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", SECTION_GAP)
	_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_content.visible = false
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "RoomScroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_content)
	code_row = RoomHeader.code_row(self)
	_content.add_child(code_row)
	_content.add_child(LobbyWidgets.group(_build_map_section()))
	_content.add_child(LobbyWidgets.group(_build_player_section()))
	_content.add_child(LobbyWidgets.group(_build_kid_section()))
	readiness_label = Label.new()
	readiness_label.name = "Readiness"
	readiness_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	readiness_label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	readiness_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	readiness_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	column.add_child(readiness_label)
	action_button = MenuDialog.make_button("Ready", "orange", 32, Vector2(0.0, 88.0))
	action_button.name = "ActionButton"
	action_button.pressed.connect(action_pressed.emit)
	action_button.visible = false
	column.add_child(action_button)
	status_label = Label.new()
	status_label.name = "Status"
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	status_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	status_label.add_theme_color_override("font_color", KalyeahTheme.ERROR)
	status_label.visible = false
	column.add_child(status_label)
	retry_button = MenuDialog.make_button("Try again", "blue", 30)
	retry_button.name = "Retry"
	retry_button.visible = false
	retry_button.pressed.connect(func() -> void:
		retry_button.visible = false
		retry_pressed.emit())
	column.add_child(retry_button)


func _build_top_row() -> Control:
	return RoomHeader.build(self)


func _build_map_section() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "MapSection"
	column.add_theme_constant_override("separation", int(GAP) + 4)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	map_card = ChoiceCard.make_card("", "Agawan Base", "blue", MAP_CARD)
	map_card.name = "MapCard"
	map_card.accessibility_name = "Map: Agawan Base"
	map_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_card.focus_mode = Control.FOCUS_NONE
	map_card.pressed.connect(open_map_carousel)
	(map_card.get_node("Choice") as Label).vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	(map_card.get_node("Choice") as Label).offset_top = 12.0
	column.add_child(map_card)
	var sizes: HBoxContainer = HBoxContainer.new()
	sizes.name = "Sizes"
	sizes.add_theme_constant_override("separation", int(GAP) + 2)
	sizes.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(sizes)
	for size: int in [1, 2, 3]:
		var button: Button = LobbyWidgets.button("%dv%d" % [size, size], size_chosen.emit.bind(size), "grey", UiMetrics.ACTION)
		LobbyWidgets.make_toggle(button)
		button.name = "Size%d" % size
		button.custom_minimum_size = Vector2(0.0, 88.0)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		sizes.add_child(button)
		size_buttons[size] = button
	var formats: HBoxContainer = HBoxContainer.new()
	formats.name = "FirstTo"
	formats.add_theme_constant_override("separation", int(GAP) + 2)
	column.add_child(formats)
	for count: int in [3, 5, 7]:
		var button: Button = MenuDialog.make_button("First to %d" % count, "grey", UiMetrics.ACTION, Vector2(0.0, 88.0))
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		LobbyWidgets.make_toggle(button)
		button.pressed.connect(first_to_chosen.emit.bind(count))
		formats.add_child(button)
		first_to_buttons[count] = button
	format_summary = LobbyWidgets.label("", UiMetrics.BODY, KalyeahTheme.TEXT)
	format_summary.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	column.add_child(format_summary)
	return column


## The host's map carousel: the same horizontal carousel as the main screen, over a tinted
## background. Tap a map to pick it; tap the tint to close.
func open_map_carousel() -> void:
	if _map_picker == null:
		_map_picker = RoomMapPicker.new(self)
	_map_picker.open_map_carousel()


func close_map_carousel() -> void:
	if _map_picker != null:
		_map_picker.close_map_carousel()


func _is_host() -> bool:
	return _room.get("host", -1) == _my_id


func _build_player_section() -> Control:
	player_grid = GridContainer.new()
	player_grid.name = "Players"
	player_grid.columns = 2
	player_grid.add_theme_constant_override("h_separation", int(GAP))
	player_grid.add_theme_constant_override("v_separation", int(GAP))
	player_grid.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	var headers: HBoxContainer = HBoxContainer.new()
	headers.add_theme_constant_override("separation", int(GAP))
	column.add_child(headers)
	for team_name: String in ["Blue team", "Red team"]:
		var label: Label = LobbyWidgets.label(team_name, UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED)
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		headers.add_child(label)
	column.add_child(player_grid)
	return column


func _build_kid_section() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Kids"
	row.add_theme_constant_override("separation", 8)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for id: String in Session.AVATARS:
		var card: Button = RoomCharacterCards.build(self, id)
		row.add_child(card)
		kid_cards[id] = card
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	var scroll: ScrollContainer = ScrollContainer.new()
	_kid_scroll = scroll
	scroll.name = "KidScroll"
	scroll.custom_minimum_size.y = KID_CARD.y + 12.0
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.follow_focus = true
	scroll.add_child(row)
	column.add_child(scroll)
	return column


## Shows the room (plain data from the server) for the player `my_id`.
func show_room(room: Dictionary, my_id: int) -> void:
	_room = room.duplicate()
	_room["you"] = my_id
	_my_id = my_id
	_content.visible = true
	var host: bool = room.get("host", -1) == my_id
	var members: Array = room.get("members", []) as Array
	var me: Dictionary = {}
	for member: Dictionary in members:
		if member["player_id"] == my_id:
			me = member
	code_label.text = String(room.get("code", "")) if online_room else ""
	code_boxes.set_text(code_label.text)
	code_row.visible = online_room
	copy_button.text = "Copy"
	_copy_timer.stop()
	readiness_label.text = _readiness(room)
	action_button.visible = true
	retry_button.visible = false
	var count: int = room.get("first_to", 3) as int
	for choice: int in first_to_buttons:
		first_to_buttons[choice].set_pressed_no_signal(choice == count)
		first_to_buttons[choice].disabled = not host
	# section 1: the map and the team size (the host changes them, everyone sees them)
	var map_id: StringName = StringName(room.get("map", "street") as String)
	ChoiceCard.set_card(map_card, "", MAP_NAMES.get(map_id, String(map_id).to_upper()) as String, "blue")
	# only the host opens the map carousel
	map_card.disabled = not host
	var size: int = room.get("team_size", 1) as int
	for choice: int in size_buttons:
		size_buttons[choice].set_pressed_no_signal(choice == size)
		size_buttons[choice].disabled = not host
	(size_buttons[1].get_parent() as Control).visible = host
	(first_to_buttons[3].get_parent() as Control).visible = host
	format_summary.text = "%dv%d · First to %d" % [size, size, count]
	format_summary.visible = not host
	_fill_players(members, size)
	var picked: String = String(me.get("char", ""))
	if picked.is_empty():
		picked = Session.avatar_id
	if picked != _last_kid and kid_cards.has(picked):
		_last_kid = picked
		_kid_scroll.ensure_control_visible.call_deferred(kid_cards[picked])
	for id: String in kid_cards:
		var taken: bool = members.any(func(m: Dictionary) -> bool: return m.get("player_id", -1) != my_id and m.get("team", -1) == me.get("team", -2) and String(m.get("char", "")) == id)
		kid_cards[id].disabled = taken
		kid_cards[id].tooltip_text = "Selected by a teammate" if taken else id.capitalize()
		RoomCharacterCards.style(kid_cards[id], id == picked)
		if taken:
			kid_cards[id].modulate = Color(0.5, 0.5, 0.55)
		else:
			kid_cards[id].modulate = Color.WHITE
	# the one button
	if host:
		action_button.text = "Preview only" if room.get("preview", false) else "Start match"
		action_button.disabled = bool(room.get("preview", false)) or not (room.get("can_start_host", false) as bool)
		KalyeahTheme.style_button(action_button, "orange")
	else:
		var ready: bool = me.get("ready", false) as bool
		action_button.text = "Cancel ready" if ready else "Ready"
		action_button.disabled = false
		KalyeahTheme.style_button(action_button, "grey" if ready else "orange")


## Two columns (blue team, red team) of at most three rows: a card for each player, an open slot
## (tap it to join that team) for each place still free.
func _fill_players(members: Array, size: int) -> void:
	for card: Control in player_cards:
		card.queue_free()
		player_grid.remove_child(card)
	player_cards.clear()
	var my_team: int = -1
	var teams: Array[Array] = [[], []]
	for member: Dictionary in members:
		var team: int = member["team"] as int
		if team >= 0 and team < 2:
			teams[team].append(member)
		if member["player_id"] == _my_id:
			my_team = team
	for row: int in mini(size, MAX_ROWS):
		for team: int in 2:
			var card: Control
			if row < teams[team].size():
				card = RoomPlayerCards.player_card(teams[team][row] as Dictionary, team, _room)
			else:
				card = RoomPlayerCards.open_slot(team, row, my_team != team, _room, team_chosen.emit)
			player_grid.add_child(card)
			player_cards.append(card)


## A player card, like the owner's mock-up: a white bar with the name in bold navy and the slot
## number in a solid team-coloured block at the outer edge (blue at the left of the left column,
## red at the right of the right column), and a small Host / Ready tag in the far top corner.
static func slot_number(team: int, row: int, size: int) -> int:
	return RoomPlayerCards.slot_number(team, row, size)


func clear_room() -> void:
	_room = {}
	_content.visible = false
	action_button.visible = false
	readiness_label.text = ""
	code_label.text = ""
	code_boxes.set_text("")
	code_row.hide()
	_copy_timer.stop()


func show_retry() -> void:
	retry_button.visible = true


static func _readiness(room: Dictionary) -> String:
	if room.get("preview", false):
		return "Layout preview · Android required for real rooms"
	var members: Array = room.get("members", []) as Array
	var n: int = room.get("team_size", 1) as int
	var teams: Array[int] = [0, 0]
	var unready: int = 0
	for member: Dictionary in members:
		var team: int = member.get("team", -1) as int
		if team in [0, 1]:
			teams[team] += 1
		if member.get("player_id", -1) != room.get("host", -2) and not member.get("ready", false):
			unready += 1
	if room.get("can_start_host", false):
		return "Ready to play"
	if maxi(teams[0], teams[1]) < n or mini(teams[0], teams[1]) < maxi(1, n - 1):
		return "Waiting for players"
	if unready > 0:
		return "Waiting for %d player%s to ready up" % [unready, "" if unready == 1 else "s"]
	return "Choose a team to continue"


func set_status(text: String) -> void:
	status_label.text = text
	status_label.visible = not text.is_empty()


## True once a room has been shown (before that only the top row and the status are visible).
func has_room() -> bool:
	return _content.visible


func set_online(enabled: bool) -> void:
	online_room = enabled
	code_row.visible = enabled and has_room()


func copy_code() -> void:
	if not online_room or not has_room() or code_label.text.is_empty():
		return
	DisplayServer.clipboard_set(code_label.text)
	UiToast.say(self, "Room code copied")
	copy_button.text = "Copied"
	_copy_timer.start()
