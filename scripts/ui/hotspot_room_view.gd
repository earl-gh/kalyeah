class_name HotspotRoomView
extends Control
## The hotspot room screen (portrait), a pure view fed by the room's plain data (`show_room`):
##   top row     BACK (text only) and the 8-character room code at the left, settings (icon only) at the right
##   section 1   the map card (3:4; only the host changes it) and the 1v1 / 2v2 / 3v3 choice (host only)
##   section 2   the players: two columns (the two teams), up to three rows, one card each
##   section 3   one row of the six kids as 3:4 profile cards, tap one to be it
##   bottom      the host's START, or everyone else's READY
## It never talks to the network: LobbyScreen listens to the signals and sends what they ask for.

signal back_pressed
signal settings_pressed
## -1 / +1: the previous / next map.
signal map_step(direction: int)
signal size_chosen(size: int)
signal character_chosen(character_id: String)
signal team_chosen(team: int)
## The big button: START for the host, READY (on or off) for everyone else.
signal action_pressed

const MAP_NAMES: Dictionary[StringName, String] = {&"street": "KALYEAH STREET"}
const TEAM_COLORS: Array[Color] = [Color(0.45, 0.65, 1.0), Color(1.0, 0.45, 0.45)]
const MARGIN: float = 28.0
const GAP: float = 10.0
## The map card and the six character cards are exactly 3:4 (width:height).
const MAP_CARD: Vector2 = Vector2(210.0, 280.0)
const KID_CARD: Vector2 = Vector2(104.0, 139.0)
const PLAYER_CARD_HEIGHT: float = 122.0
const MAX_ROWS: int = 3

var code_label: Label
var back_button: Button
var settings_button: Button
var map_card: Button
var map_previous: Button
var map_next: Button
var size_buttons: Dictionary[int, Button] = {}
var player_grid: GridContainer
var player_cards: Array[Control] = []
var kid_cards: Dictionary[String, Button] = {}
var action_button: Button
var status_label: Label

var _room: Dictionary = {}
var _my_id: int = -1
var _content: Control


func _init() -> void:
	name = "HotspotRoomView"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()


func _build() -> void:
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right"]:
		margin.add_theme_constant_override("margin_" + side, int(MARGIN))
	margin.add_theme_constant_override("margin_top", 24)
	margin.add_theme_constant_override("margin_bottom", 24)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 18)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(column)
	column.add_child(_build_top_row())
	_content = VBoxContainer.new()
	_content.name = "Content"
	_content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", 18)
	_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_content.visible = false
	column.add_child(_content)
	_content.add_child(_build_map_section())
	_content.add_child(_build_player_section())
	_content.add_child(_build_kid_section())
	var spacer: Control = Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_content.add_child(spacer)
	action_button = MenuDialog.make_button("READY", "orange", 36, Vector2(0.0, 96.0))
	action_button.name = "ActionButton"
	action_button.pressed.connect(action_pressed.emit)
	_content.add_child(action_button)
	status_label = Label.new()
	status_label.name = "Status"
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.add_theme_font_size_override("font_size", 24)
	status_label.add_theme_color_override("font_color", Color("ffb4a8"))
	status_label.custom_minimum_size = Vector2(0.0, 30.0)
	column.add_child(status_label)


func _build_top_row() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopRow"
	row.add_theme_constant_override("separation", 16)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	back_button = MenuDialog.make_button("BACK", "grey", 26, Vector2(140.0, 72.0))
	back_button.name = "BackButton"
	back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	back_button.pressed.connect(back_pressed.emit)
	row.add_child(back_button)
	code_label = Label.new()
	code_label.name = "Code"
	code_label.theme_type_variation = &"DisplayLabel"
	code_label.add_theme_font_size_override("font_size", 46)
	code_label.add_theme_color_override("font_color", KalyeahTheme.AMBER)
	code_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	code_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	code_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	code_label.text = "--------"
	row.add_child(code_label)
	# settings: the icon alone, no button container
	settings_button = Button.new()
	settings_button.name = "SettingsButton"
	settings_button.flat = true
	settings_button.focus_mode = Control.FOCUS_NONE
	settings_button.custom_minimum_size = Vector2(72.0, 72.0)
	settings_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	for state: String in ["normal", "hover", "pressed", "focus"]:
		settings_button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var centre: CenterContainer = CenterContainer.new()
	centre.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	centre.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var icon: NavIcon = NavIcon.new(&"settings")
	icon.color = KalyeahTheme.CREAM
	centre.add_child(icon)
	settings_button.add_child(centre)
	settings_button.pressed.connect(settings_pressed.emit)
	row.add_child(settings_button)
	return row


func _build_map_section() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "MapSection"
	row.add_theme_constant_override("separation", 20)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var left: VBoxContainer = VBoxContainer.new()
	left.add_theme_constant_override("separation", 8)
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(left)
	map_card = ChoiceCard.make_card("MAP", "KALYEAH STREET", "blue", MAP_CARD)
	map_card.name = "MapCard"
	map_card.disabled = true
	map_card.focus_mode = Control.FOCUS_NONE
	var map_name: Label = map_card.get_node("Choice") as Label
	map_name.add_theme_font_size_override("font_size", 26)
	map_name.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	left.add_child(map_card)
	var arrows: HBoxContainer = HBoxContainer.new()
	arrows.add_theme_constant_override("separation", 8)
	left.add_child(arrows)
	map_previous = MenuDialog.make_button("<", "grey", 30, Vector2(0.0, 56.0))
	map_previous.name = "MapPrevious"
	map_previous.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_previous.pressed.connect(map_step.emit.bind(-1))
	arrows.add_child(map_previous)
	map_next = MenuDialog.make_button(">", "grey", 30, Vector2(0.0, 56.0))
	map_next.name = "MapNext"
	map_next.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_next.pressed.connect(map_step.emit.bind(1))
	arrows.add_child(map_next)
	var sizes: VBoxContainer = VBoxContainer.new()
	sizes.name = "Sizes"
	sizes.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	sizes.alignment = BoxContainer.ALIGNMENT_CENTER
	sizes.add_theme_constant_override("separation", 12)
	sizes.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(sizes)
	var hint: Label = LobbyWidgets.field_label("PLAYERS PER TEAM")
	sizes.add_child(hint)
	for size: int in [1, 2, 3]:
		var button: Button = LobbyWidgets.button("%dv%d" % [size, size], size_chosen.emit.bind(size), "grey", 34)
		LobbyWidgets.make_toggle(button)
		button.name = "Size%d" % size
		button.custom_minimum_size = Vector2(0.0, 84.0)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		sizes.add_child(button)
		size_buttons[size] = button
	return row


func _build_player_section() -> Control:
	player_grid = GridContainer.new()
	player_grid.name = "Players"
	player_grid.columns = 2
	player_grid.add_theme_constant_override("h_separation", int(GAP))
	player_grid.add_theme_constant_override("v_separation", int(GAP))
	player_grid.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return player_grid


func _build_kid_section() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Kids"
	row.add_theme_constant_override("separation", 8)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for id: String in Session.AVATARS:
		var card: Button = _kid_card(id)
		row.add_child(card)
		kid_cards[id] = card
	return row


## One 3:4 profile card: the kid's picture, the name along the bottom.
func _kid_card(id: String) -> Button:
	var card: Button = Button.new()
	card.name = id.capitalize()
	card.custom_minimum_size = KID_CARD
	card.focus_mode = Control.FOCUS_NONE
	card.clip_contents = true
	MenuDialog.feel_press(card)
	card.pressed.connect(character_chosen.emit.bind(id))
	var picture: TextureRect = TextureRect.new()
	picture.texture = KidModel.portrait(StringName(id))
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(picture)
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	var label: Label = Label.new()
	label.theme_type_variation = &"DisplayLabel"
	label.text = (definition.display_name if definition != null else id).to_upper()
	label.add_theme_font_size_override("font_size", 17)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	label.offset_top = -30.0
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(label)
	_style_kid_card(card, false)
	return card


func _style_kid_card(card: Button, chosen: bool) -> void:
	var box: StyleBoxFlat = KalyeahTheme.surface_box(14, 6 if chosen else 2, 0.0)
	box.bg_color = Color(KalyeahTheme.INK, 0.8)
	box.border_color = KalyeahTheme.AMBER if chosen else Color(1.0, 1.0, 1.0, 0.2)
	for state: String in ["normal", "hover", "pressed", "focus", "disabled"]:
		card.add_theme_stylebox_override(state, box)


## Shows the room (plain data from the server) for the player `my_id`.
func show_room(room: Dictionary, my_id: int) -> void:
	_room = room
	_my_id = my_id
	_content.visible = true
	var host: bool = room.get("host", -1) == my_id
	var members: Array = room.get("members", []) as Array
	var me: Dictionary = {}
	for member: Dictionary in members:
		if member["player_id"] == my_id:
			me = member
	code_label.text = String(room.get("code", "--------"))
	# section 1: the map and the team size (the host changes them, everyone sees them)
	var map_id: StringName = StringName(room.get("map", "street") as String)
	ChoiceCard.set_card(map_card, "MAP", MAP_NAMES.get(map_id, String(map_id).to_upper()) as String, "blue")
	var maps: Array = RoomService.MAP_IDS
	map_previous.visible = host
	map_next.visible = host
	map_previous.disabled = maps.size() < 2
	map_next.disabled = maps.size() < 2
	var size: int = room.get("team_size", 1) as int
	for choice: int in size_buttons:
		size_buttons[choice].set_pressed_no_signal(choice == size)
		size_buttons[choice].disabled = not host
	_fill_players(members, size, host)
	var picked: String = String(me.get("char", ""))
	if picked.is_empty():
		picked = Session.avatar_id
	for id: String in kid_cards:
		_style_kid_card(kid_cards[id], id == picked)
	# the one button
	if host:
		action_button.text = "START"
		action_button.disabled = not (room.get("can_start_host", false) as bool)
		KalyeahTheme.style_button(action_button, "orange")
	else:
		var ready: bool = me.get("ready", false) as bool
		action_button.text = "READY!" if ready else "READY"
		action_button.disabled = false
		KalyeahTheme.style_button(action_button, "green" if ready else "orange")


## Two columns (blue team, red team) of at most three rows: a card for each player, an open slot
## (tap it to join that team) for each place still free.
func _fill_players(members: Array, size: int, host: bool) -> void:
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
				card = _player_card(teams[team][row] as Dictionary, team, host)
			else:
				card = _open_slot(team, my_team != team)
			player_grid.add_child(card)
			player_cards.append(card)


func _player_card(member: Dictionary, team: int, _host: bool) -> Control:
	var card: PanelContainer = PanelContainer.new()
	card.name = "Player%d" % (member["player_id"] as int)
	card.custom_minimum_size = Vector2(0.0, PLAYER_CARD_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var box: StyleBoxFlat = KalyeahTheme.surface_box(16, 3, 10.0)
	box.bg_color = Color(KalyeahTheme.INK, 0.78)
	box.border_color = TEAM_COLORS[team]
	card.add_theme_stylebox_override("panel", box)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	card.add_child(row)
	var face: TextureRect = TextureRect.new()
	var kid: String = String(member.get("char", ""))
	if kid.is_empty():
		kid = Session.AVATARS[(member["player_id"] as int) % Session.AVATARS.size()]
	face.texture = KidModel.minimap_face(StringName(kid))
	if face.texture == null:
		face.texture = KidModel.portrait(StringName(kid))
	face.custom_minimum_size = Vector2(88.0, 88.0)
	face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(face)
	var text: VBoxContainer = VBoxContainer.new()
	text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	text.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_child(text)
	var name_label: Label = LobbyWidgets.label(member["name"] as String, 26)
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	text.add_child(name_label)
	var tags: PackedStringArray = PackedStringArray()
	if member["player_id"] == _room.get("host", -1):
		tags.append("HOST")
	if member["player_id"] == _my_id:
		tags.append("YOU")
	if member["ready"] or member["player_id"] == _room.get("host", -1):
		tags.append("READY")
	var tag_label: Label = LobbyWidgets.label("  ".join(tags), 19, TEAM_COLORS[team])
	tag_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	text.add_child(tag_label)
	return card


func _open_slot(team: int, can_join: bool) -> Control:
	var card: Button = Button.new()
	card.name = "OpenSlot%d" % team
	card.custom_minimum_size = Vector2(0.0, PLAYER_CARD_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.focus_mode = Control.FOCUS_NONE
	card.text = "JOIN" if can_join else "OPEN"
	card.disabled = not can_join
	card.add_theme_font_size_override("font_size", 24)
	var box: StyleBoxFlat = KalyeahTheme.surface_box(16, 2, 10.0)
	box.bg_color = Color(KalyeahTheme.INK, 0.4)
	box.border_color = Color(TEAM_COLORS[team], 0.5)
	for state: String in ["normal", "hover", "pressed", "focus", "disabled"]:
		card.add_theme_stylebox_override(state, box)
	card.add_theme_color_override("font_color", Color(TEAM_COLORS[team], 0.8))
	card.add_theme_color_override("font_disabled_color", Color(1.0, 1.0, 1.0, 0.4))
	card.pressed.connect(team_chosen.emit.bind(team))
	return card


func set_status(text: String) -> void:
	status_label.text = text


## True once a room has been shown (before that only the top row and the status are visible).
func has_room() -> bool:
	return _content.visible
