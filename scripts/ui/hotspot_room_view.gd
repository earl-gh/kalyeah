class_name HotspotRoomView
extends Control
## The hotspot room screen (portrait), a pure view fed by the room's plain data (`show_room`):
##   top row     LOBBY (the text-only back button) | the 8-character room code at the left, settings (icon only) at the right
##   section 1   the map card (2:1, a row to itself, no buttons: the host taps it to open the map carousel),
##               and under it one row with the 1v1 / 2v2 / 3v3 choice (host only)
##   section 2   the players: two columns (the two teams), up to three rows, one card each, no picture:
##               white bars like the owner's mock-up: blue number block then the name (1 to 3) at the left,
##               the name then a red number block (4 to 6) at the right, a Host / Ready tag in the far corner
##   section 3   one row of the six kids as 3:4 profile cards, tap one to be it
##   bottom      the host's START, or everyone else's READY
## It never talks to the network: LobbyScreen listens to the signals and sends what they ask for.

signal back_pressed
signal settings_pressed
## The host picked a map in the carousel.
signal map_chosen(map_id: StringName)
signal size_chosen(size: int)
signal character_chosen(character_id: String)
signal team_chosen(team: int)
## The big button: START for the host, READY (on or off) for everyone else.
signal action_pressed

const MAP_NAMES: Dictionary[StringName, String] = {&"street": "AGAWAN BASE", &"tumbang_preso": "TUMBANG PRESO", &"patintero": "PATINTERO"}
const MAP_COLORS: Dictionary[StringName, String] = {&"street": "blue", &"tumbang_preso": "orange", &"patintero": "green"}
## The cards in the map carousel are 3:4.
const MAP_PICK_CARD: Vector2 = Vector2(414.0, 552.0)
const TEAM_COLORS: Array[Color] = [Color(0.45, 0.65, 1.0), Color(1.0, 0.45, 0.45)]
## Spacing and type scale of the screen: one set of values so it reads as one system.
const MARGIN: float = 24.0
const GAP: float = 8.0
const SECTION_GAP: int = 14
## The map card is 2:1 (width:height) and fills a row; the six character cards are 3:4.
const MAP_CARD: Vector2 = Vector2(672.0, 336.0)
const KID_CARD: Vector2 = Vector2(105.0, 140.0)
const KID_RADIUS: int = 16
const BAR_HEIGHT: float = 58.0
const NUMBER_WIDTH: float = 56.0
const NAME_SIZE: int = 26
const NUMBER_SIZE: int = 32
const TAG_SIZE: int = 15
## The solid number blocks and tags of the mock-up, and the navy of the names on the white bars.
const TEAM_BLOCKS: Array[Color] = [Color(0.18, 0.17, 0.82), Color(0.94, 0.14, 0.2)]
const NAME_COLOR: Color = Color(0.12, 0.27, 0.52)
const MAX_ROWS: int = 3

var code_label: Label
var back_button: Button
var settings_button: Button
var map_card: Button
var map_carousel: MenuCarousel
var _map_overlay: Control
var _map_hint: Label
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
	margin.add_theme_constant_override("margin_top", 20)
	margin.add_theme_constant_override("margin_bottom", 20)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(margin)
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
	column.add_child(_content)
	_content.add_child(_build_map_section())
	_content.add_child(_build_player_section())
	_content.add_child(_build_kid_section())
	var spacer: Control = Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_content.add_child(spacer)
	action_button = MenuDialog.make_button("READY", "orange", 32, Vector2(0.0, 80.0))
	action_button.name = "ActionButton"
	action_button.pressed.connect(action_pressed.emit)
	_content.add_child(action_button)
	status_label = Label.new()
	status_label.name = "Status"
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.add_theme_font_size_override("font_size", 20)
	status_label.add_theme_color_override("font_color", Color("ffb4a8"))
	status_label.custom_minimum_size = Vector2(0.0, 26.0)
	column.add_child(status_label)


func _build_top_row() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopRow"
	row.add_theme_constant_override("separation", 12)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	back_button = MenuDialog.make_button("LOBBY", "grey", 22, Vector2(124.0, 56.0))
	back_button.name = "BackButton"
	back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	back_button.pressed.connect(back_pressed.emit)
	row.add_child(back_button)
	var bar: Label = Label.new()
	bar.name = "Divider"
	bar.text = "|"
	bar.theme_type_variation = &"DisplayLabel"
	bar.add_theme_font_size_override("font_size", 38)
	bar.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0, 0.45))
	bar.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(bar)
	code_label = Label.new()
	code_label.name = "Code"
	code_label.theme_type_variation = &"DisplayLabel"
	code_label.add_theme_font_size_override("font_size", 38)
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
	settings_button.custom_minimum_size = Vector2(56.0, 56.0)
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


## Section 1: the map card, 2:1, filling its own row, no buttons. The host taps it to open the
## carousel of maps. Under it, one row: 1v1, 2v2, 3v3.
func _build_map_section() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "MapSection"
	column.add_theme_constant_override("separation", int(GAP) + 4)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	map_card = ChoiceCard.make_card("", "AGAWAN BASE", "blue", MAP_CARD)
	map_card.name = "MapCard"
	map_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_card.focus_mode = Control.FOCUS_NONE
	map_card.pressed.connect(open_map_carousel)
	column.add_child(map_card)
	var sizes: HBoxContainer = HBoxContainer.new()
	sizes.name = "Sizes"
	sizes.add_theme_constant_override("separation", int(GAP) + 2)
	sizes.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(sizes)
	for size: int in [1, 2, 3]:
		var button: Button = LobbyWidgets.button("%dv%d" % [size, size], size_chosen.emit.bind(size), "grey", 28)
		LobbyWidgets.make_toggle(button)
		button.name = "Size%d" % size
		button.custom_minimum_size = Vector2(0.0, 60.0)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		sizes.add_child(button)
		size_buttons[size] = button
	return column


## The host's map carousel: the same horizontal carousel as the main screen, over a tinted
## background. Tap a map to pick it; tap the tint to close.
func open_map_carousel() -> void:
	if _map_overlay != null and is_instance_valid(_map_overlay):
		return
	if not _is_host():
		return
	_map_overlay = Control.new()
	_map_overlay.name = "MapOverlay"
	_map_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_map_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_map_overlay.z_index = 100
	add_child(_map_overlay)
	var tint: ColorRect = ColorRect.new()
	tint.name = "Tint"
	tint.color = Color(0.01, 0.025, 0.06, 0.82)
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.mouse_filter = Control.MOUSE_FILTER_STOP
	tint.gui_input.connect(_on_tint_input)
	_map_overlay.add_child(tint)
	var maps: Array[Dictionary] = []
	var current: int = 0
	var chosen: StringName = StringName(_room.get("map", "street") as String)
	for id: StringName in RoomService.MAP_IDS:
		if id == chosen:
			current = maps.size()
		maps.append({"id": String(id), "title": MAP_NAMES.get(id, String(id).to_upper()), "color": MAP_COLORS.get(id, "blue"),
			"heading": "" if id in RoomService.PLAYABLE_MAP_IDS else "COMING SOON"})
	map_carousel = MenuCarousel.new()
	map_carousel.name = "MapCarousel"
	map_carousel.card_size = MAP_PICK_CARD
	map_carousel.custom_minimum_size = Vector2(0.0, MAP_PICK_CARD.y + 24.0)
	# the full width, centred up and down
	map_carousel.anchor_left = 0.0
	map_carousel.anchor_right = 1.0
	map_carousel.anchor_top = 0.5
	map_carousel.anchor_bottom = 0.5
	map_carousel.offset_left = 0.0
	map_carousel.offset_right = 0.0
	map_carousel.offset_top = -(MAP_PICK_CARD.y + 24.0) / 2.0
	map_carousel.offset_bottom = (MAP_PICK_CARD.y + 24.0) / 2.0
	_map_overlay.add_child(map_carousel)
	_map_hint = Label.new()
	_map_hint.name = "MapHint"
	_map_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_map_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_map_hint.add_theme_font_size_override("font_size", 22)
	_map_hint.add_theme_color_override("font_color", KalyeahTheme.CREAM)
	# just under the carousel (which is centred up and down)
	_map_hint.anchor_left = 0.0
	_map_hint.anchor_right = 1.0
	_map_hint.anchor_top = 0.5
	_map_hint.anchor_bottom = 0.5
	_map_hint.offset_left = MARGIN
	_map_hint.offset_right = -MARGIN
	_map_hint.offset_top = MAP_PICK_CARD.y / 2.0 + 40.0
	_map_hint.offset_bottom = MAP_PICK_CARD.y / 2.0 + 110.0
	_map_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_map_overlay.add_child(_map_hint)
	map_carousel.setup("", maps, current)
	map_carousel.selected.connect(_on_map_carousel_selected)


func close_map_carousel() -> void:
	if _map_overlay != null and is_instance_valid(_map_overlay):
		_map_overlay.queue_free()
	_map_overlay = null
	map_carousel = null


func _on_tint_input(event: InputEvent) -> void:
	var click: InputEventMouseButton = event as InputEventMouseButton
	if click != null and click.pressed and click.button_index == MOUSE_BUTTON_LEFT:
		close_map_carousel()


func _on_map_carousel_selected(index: int) -> void:
	var id: StringName = RoomService.MAP_IDS[index]
	if not id in RoomService.PLAYABLE_MAP_IDS:
		# listed, but not playable yet: say so and keep the room on its map
		_map_hint.text = "%s is coming soon. Agawan Base is the only game to play for now." % MAP_NAMES.get(id, String(id).to_upper())
		return
	_map_hint.text = ""
	map_chosen.emit(id)
	# a moment to see the card come forward, then back to the room
	get_tree().create_timer(0.35).timeout.connect(close_map_carousel)


func _is_host() -> bool:
	return _room.get("host", -1) == _my_id


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
	MenuDialog.feel_press(card)
	card.pressed.connect(character_chosen.emit.bind(id))
	var picture: TextureRect = TextureRect.new()
	picture.texture = KidModel.card_face(StringName(id))
	if picture.texture == null:
		picture.texture = KidModel.portrait(StringName(id))
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the picture follows the card's rounded corners
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = ChoiceCard.CROP_SHADER
	crop.set_shader_parameter("rect_size", KID_CARD)
	crop.set_shader_parameter("radius", float(KID_RADIUS))
	picture.material = crop
	card.add_child(picture)
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	var label: Label = Label.new()
	label.theme_type_variation = &"DisplayLabel"
	label.text = (definition.display_name if definition != null else id).to_upper()
	label.add_theme_font_size_override("font_size", 15)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	label.offset_top = -30.0
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(label)
	_style_kid_card(card, false)
	return card


func _style_kid_card(card: Button, chosen: bool) -> void:
	# rounded corners and a soft drop shadow
	var box: StyleBoxFlat = KalyeahTheme.surface_box(KID_RADIUS, 0, 0.0)
	box.bg_color = Color(KalyeahTheme.INK, 0.8)
	box.shadow_color = Color(0.0, 0.0, 0.0, 0.5)
	box.shadow_size = 8
	box.shadow_offset = Vector2(0.0, 4.0)
	for state: String in ["normal", "hover", "pressed", "focus", "disabled"]:
		card.add_theme_stylebox_override(state, box)
	# the amber frame is an overlay (the picture covers the button's own border)
	var frame: Panel = card.get_node_or_null("Chosen") as Panel
	if frame == null:
		frame = Panel.new()
		frame.name = "Chosen"
		frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var ring: StyleBoxFlat = KalyeahTheme.surface_box(KID_RADIUS, 5, 0.0)
		ring.draw_center = false
		ring.shadow_size = 0
		ring.border_color = KalyeahTheme.AMBER
		frame.add_theme_stylebox_override("panel", ring)
		card.add_child(frame)
	frame.visible = chosen
	var picture: Control = card.get_child(0) as Control
	picture.modulate = Color.WHITE if chosen else Color(0.78, 0.78, 0.82)


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
	ChoiceCard.set_card(map_card, "", MAP_NAMES.get(map_id, String(map_id).to_upper()) as String, "blue")
	# only the host opens the map carousel
	map_card.disabled = not host
	var size: int = room.get("team_size", 1) as int
	for choice: int in size_buttons:
		size_buttons[choice].set_pressed_no_signal(choice == size)
		size_buttons[choice].disabled = not host
	_fill_players(members, size)
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
				card = _player_card(teams[team][row] as Dictionary, team)
			else:
				card = _open_slot(team, row, my_team != team)
			player_grid.add_child(card)
			player_cards.append(card)


## A player card, like the owner's mock-up: a white bar with the name in bold navy and the slot
## number in a solid team-coloured block at the outer edge (blue at the left of the left column,
## red at the right of the right column), and a small Host / Ready tag in the far top corner.
func _player_card(member: Dictionary, team: int) -> Control:
	var card: Control = Control.new()
	card.name = "Player%d" % (member["player_id"] as int)
	card.custom_minimum_size = Vector2(0.0, BAR_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var bar: Panel = Panel.new()
	bar.name = "Bar"
	bar.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.WHITE
	box.border_color = Color(1.0, 1.0, 1.0, 0.55)
	box.set_border_width_all(1)
	bar.add_theme_stylebox_override("panel", box)
	card.add_child(bar)
	var row: HBoxContainer = HBoxContainer.new()
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	row.add_theme_constant_override("separation", 0)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(row)
	var name_label: Label = Label.new()
	name_label.name = "Name"
	name_label.text = member["name"] as String
	name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT if team == 0 else HORIZONTAL_ALIGNMENT_RIGHT
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	name_label.add_theme_font_size_override("font_size", NAME_SIZE)
	name_label.add_theme_color_override("font_color", NAME_COLOR)
	name_label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var number: Control = _number_block(slot_number(team, _row_of(member, team), _room.get("team_size", 1) as int), team, false)
	if team == 0:
		name_label.add_theme_constant_override("line_spacing", 0)
		row.add_child(number)
		row.add_child(_padded(name_label, 14.0, 6.0))
	else:
		row.add_child(_padded(name_label, 6.0, 14.0))
		row.add_child(number)
	var tag_text: String = ""
	if member["player_id"] == _room.get("host", -1):
		tag_text = "Host"
	elif member["ready"]:
		tag_text = "Ready"
	if not tag_text.is_empty():
		card.add_child(_tag(tag_text, team))
	return card


## The slot number: the places in the room counted down the left column (blue) and then the right
## (red): 1 to 3 and 4 to 6 in a 3v3, 1 to 2 and 3 to 4 in a 2v2, 1 and 2 in a 1v1.
static func slot_number(team: int, row: int, size: int) -> int:
	return team * size + row + 1


func _row_of(member: Dictionary, team: int) -> int:
	var row: int = 0
	for other: Dictionary in _room.get("members", []) as Array:
		if other["team"] != team:
			continue
		if other["player_id"] == member["player_id"]:
			return row
		row += 1
	return row


## The number in a solid team-coloured block (`hollow`: only the outline and the number, for a slot
## nobody has taken yet).
func _number_block(number: int, team: int, hollow: bool) -> Control:
	var block: PanelContainer = PanelContainer.new()
	block.name = "Number"
	block.custom_minimum_size = Vector2(NUMBER_WIDTH, 0.0)
	block.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	if hollow:
		box.draw_center = false
		box.border_color = TEAM_BLOCKS[team]
		box.set_border_width_all(3)
	else:
		box.bg_color = TEAM_BLOCKS[team]
	block.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.text = str(number)
	label.theme_type_variation = &"DisplayLabel"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", NUMBER_SIZE)
	label.add_theme_color_override("font_color", TEAM_BLOCKS[team].lightened(0.35) if hollow else Color.WHITE)
	label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	label.add_theme_constant_override("outline_size", 0)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	block.add_child(label)
	return block


## A control with a margin each side (so the name does not touch the number block).
func _padded(child: Control, left: float, right: float) -> Control:
	var margin: MarginContainer = MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_theme_constant_override("margin_left", int(left))
	margin.add_theme_constant_override("margin_right", int(right))
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(child)
	return margin


## The small Host / Ready tag in the far top corner (the right for blue, the left for red).
func _tag(text: String, team: int) -> Control:
	var tag: PanelContainer = PanelContainer.new()
	tag.name = "Tag"
	tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = TEAM_BLOCKS[team]
	box.content_margin_left = 8.0
	box.content_margin_right = 8.0
	box.content_margin_top = 1.0
	box.content_margin_bottom = 1.0
	tag.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", TAG_SIZE)
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	label.add_theme_constant_override("outline_size", 0)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.add_child(label)
	# pinned to the top corner and left to size itself to its text
	var right: float = 1.0 if team == 0 else 0.0
	tag.anchor_left = right
	tag.anchor_right = right
	tag.anchor_top = 0.0
	tag.anchor_bottom = 0.0
	tag.offset_left = 0.0
	tag.offset_right = 0.0
	tag.offset_top = 0.0
	tag.offset_bottom = 0.0
	tag.grow_horizontal = Control.GROW_DIRECTION_BEGIN if team == 0 else Control.GROW_DIRECTION_END
	tag.grow_vertical = Control.GROW_DIRECTION_END
	return tag


## A slot nobody has taken yet: a hollow card (only its outline and its number, no fill, no
## name). Tap it to join that team.
func _open_slot(team: int, row: int, can_join: bool) -> Control:
	var card: Button = Button.new()
	card.name = "OpenSlot%d" % team
	card.custom_minimum_size = Vector2(0.0, BAR_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.focus_mode = Control.FOCUS_NONE
	card.disabled = not can_join
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.draw_center = false
	box.border_color = Color(1.0, 1.0, 1.0, 0.6)
	box.set_border_width_all(2)
	for state: String in ["normal", "hover", "pressed", "focus", "disabled"]:
		card.add_theme_stylebox_override(state, box)
	card.pressed.connect(team_chosen.emit.bind(team))
	var inner: HBoxContainer = HBoxContainer.new()
	inner.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	inner.add_theme_constant_override("separation", 0)
	inner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(inner)
	var space: Control = Control.new()
	space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	space.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var number: Control = _number_block(slot_number(team, row, _room.get("team_size", 1) as int), team, true)
	if team == 0:
		inner.add_child(number)
		inner.add_child(space)
	else:
		inner.add_child(space)
		inner.add_child(number)
	return card


func set_status(text: String) -> void:
	status_label.text = text


## True once a room has been shown (before that only the top row and the status are visible).
func has_room() -> bool:
	return _content.visible
