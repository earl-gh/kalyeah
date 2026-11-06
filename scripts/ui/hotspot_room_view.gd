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
## Online, host only: open (listed for everyone to join with a tap) or private (code only).
signal open_chosen(open: bool)
## Local, host only: the places nobody took are played by bots (on) or left empty (off).
signal fill_bots_chosen(on: bool)
signal character_chosen(character_id: String)
signal team_chosen(team: int)
## The big button: START for the host, READY (on or off) for everyone else.
signal action_pressed

const MAP_NAMES: Dictionary[StringName, String] = {&"street": "Agawang Base", &"tumbang_preso": "Tumbang Preso", &"patintero": "Patintero",
	&"langit_lupa": "Langit Lupa"}
const MAP_COLORS: Dictionary[StringName, String] = {&"street": "blue", &"tumbang_preso": "orange", &"patintero": "green", &"langit_lupa": "red"}
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
const KID_RADIUS: int = UiSkin.RADIUS_M
## The room QR dialog's code, a side (29 modules with its margin: 15 px each in the 720 px design width).
const QR_SIDE: float = 435.0
const BAR_HEIGHT: float = 96.0
const NUMBER_WIDTH: float = 56.0
## The solid number blocks and tags of the mock-up, and the navy of the names on the white bars.
const TEAM_BLOCKS: Array[Color] = [KalyeahTheme.TEAM_BLUE, KalyeahTheme.TEAM_RED]
const NAME_COLOR: Color = Color(0.12, 0.27, 0.52)
const MAX_ROWS: int = 3

## Width of each of the three first-to number buttons.
const FIRST_TO_WIDTH: float = 120.0
var online_room: bool = false
var code_label: Label
var code_boxes: UiCodeBoxes
var code_row: HBoxContainer
var copy_button: Button
var qr_button: Button
## Local, host on its game hotspot: the hotspot as a Wi-Fi QR text (LocalLink.share_text), shown by QR; "" otherwise.
var local_share: String = ""
## Whether this phone can hand the hotspot over by NFC too (said under the QR).
var local_nfc: bool = false
var _copy_timer: Timer
var back_button: Button
var settings_button: Button
var map_card: Button
## "Tap to change" under the game's name (the host only).
var map_hint: Label
## A guest's game: the name as text (the host has the card).
var map_title: Label
## The chosen game's picture at the right end of the map card (the Games page's picture).
var map_picture: TextureRect
var map_carousel: MenuCarousel
var _map_overlay: Control
var _map_hint: Label
var _map_picker: RoomMapPicker
## Online, host only: Open and Private (owner: open by default).
var open_buttons: Dictionary[bool, Button] = {}
var player_grid: GridContainer
## "Blue team" / "Red team" over the two columns (hidden in a game where every kid plays for itself: the columns are just places).
var team_headers: HBoxContainer
var player_cards: Array[Control] = []
var kid_cards: Dictionary[String, Button] = {}
var first_to_buttons: Dictionary[int, Button] = {}
## The host's two settings in one row: "Race to" (3 5 7) and, in a Local room, "Fill with bots" (On / Off), each a caption over its buttons.
var settings_row: HBoxContainer
var race_group: VBoxContainer
var bots_row: VBoxContainer
var settings_gap: Control
var bots_button: UiToggle
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
	action_button = MenuDialog.make_button("Ready", "orange", UiMetrics.BODY, Vector2(0.0, 88.0))
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
	retry_button = MenuDialog.make_button("Try again", "blue", UiMetrics.BODY)
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
	map_card = ChoiceCard.make_card("", "Agawang Base", "blue", MAP_CARD)
	map_card.name = "MapCard"
	map_card.accessibility_name = "Map: Agawang Base"
	map_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_card.focus_mode = Control.FOCUS_NONE
	map_card.pressed.connect(open_map_carousel)
	(map_card.get_node("Choice") as Label).vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	(map_card.get_node("Choice") as Label).offset_top = 12.0
	map_picture = TextureRect.new()
	map_picture.name = "MapPicture"
	map_picture.texture = StreetPage.picture_for(&"street")
	map_picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	map_picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	map_picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	map_picture.anchor_left = 1.0
	map_picture.anchor_right = 1.0
	map_picture.anchor_top = 0.0
	map_picture.anchor_bottom = 1.0
	map_picture.offset_left = -(MAP_CARD.y - 16.0) - 12.0
	map_picture.offset_right = -12.0
	map_picture.offset_top = 8.0
	map_picture.offset_bottom = -8.0
	map_card.add_child(map_picture)
	# the title at the top, "Tap to change" under it (the host only: a guest cannot change the game)
	var title: Label = map_card.get_node("Choice") as Label
	title.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	title.offset_top = 10.0
	map_hint = LobbyWidgets.label("Tap to change", UiMetrics.MIN_TEXT, KalyeahTheme.semantic_text_color("blue"))
	map_hint.name = "Hint"
	map_hint.modulate.a = 0.75
	map_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	map_hint.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	map_hint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	map_hint.offset_left = ChoiceCard.PAD
	map_hint.offset_bottom = -12.0
	map_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	map_card.add_child(map_hint)
	# a small caption at the top left of the card, like the ones over the settings below it
	var game: VBoxContainer = VBoxContainer.new()
	game.name = "Game"
	game.add_theme_constant_override("separation", 4)
	game.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var caption: Label = LobbyWidgets.label("Game", UiMetrics.MIN_TEXT, KalyeahTheme.TEXT_MUTED)
	caption.name = "Caption"
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	game.add_child(caption)
	game.add_child(map_card)
	# a guest cannot change the game: it reads the name as text, no card
	map_title = LobbyWidgets.label("", UiMetrics.TITLE, KalyeahTheme.TEXT)
	map_title.name = "GameTitle"
	map_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	game.add_child(map_title)
	column.add_child(game)
	var listing: HBoxContainer = HBoxContainer.new()
	listing.name = "Listing"
	listing.add_theme_constant_override("separation", int(GAP) + 2)
	listing.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(listing)
	for open: bool in [true, false]:
		var choice: Button = LobbyWidgets.button("Open" if open else "Private", open_chosen.emit.bind(open), "grey", UiMetrics.ACTION)
		LobbyWidgets.make_toggle(choice)
		choice.name = "Open" if open else "Private"
		choice.accessibility_name = "Open room: anyone can join" if open else "Private room: code only"
		choice.custom_minimum_size = Vector2(0.0, 88.0)
		choice.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		listing.add_child(choice)
		open_buttons[open] = choice
	# one row for the host's two settings, each a small caption at the top left of its buttons: "Race to" 3 5 7, and "Fill with bots"
	settings_row = HBoxContainer.new()
	settings_row.name = "RoomOptions"
	settings_row.add_theme_constant_override("separation", int(GAP) + 2)
	column.add_child(settings_row)
	race_group = _setting("Race to", "RaceTo")
	var race_buttons: HBoxContainer = race_group.get_child(1) as HBoxContainer
	for count: int in [3, 5, 7]:
		var button: Button = MenuDialog.make_button("%d" % count, "grey", UiMetrics.TITLE, Vector2(FIRST_TO_WIDTH, 88.0))
		button.accessibility_name = "Race to %d" % count
		LobbyWidgets.make_toggle(button)
		button.pressed.connect(first_to_chosen.emit.bind(count))
		race_buttons.add_child(button)
		first_to_buttons[count] = button
	settings_gap = Control.new()
	settings_gap.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	settings_gap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	settings_row.add_child(settings_gap)
	bots_row = _setting("Fill with bots", "FillBots")
	bots_button = UiToggle.new()
	bots_button.accessibility_name = "Fill the empty places with bots"
	bots_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	bots_button.toggled.connect(fill_bots_chosen.emit)
	(bots_row.get_child(1) as HBoxContainer).add_child(bots_button)
	format_summary = LobbyWidgets.label("", UiMetrics.BODY, KalyeahTheme.TEXT)
	format_summary.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	column.add_child(format_summary)
	return column


## One setting of the host's row: a small caption at the top left, the buttons under it (`get_child(1)`; a guest sees the choice as the text
## `get_child(2)` instead).
func _setting(caption: String, group_name: String) -> VBoxContainer:
	var group: VBoxContainer = VBoxContainer.new()
	group.name = group_name
	group.add_theme_constant_override("separation", 4)
	group.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var label: Label = LobbyWidgets.label(caption, UiMetrics.MIN_TEXT, KalyeahTheme.TEXT_MUTED)
	label.name = "Caption"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	group.add_child(label)
	var buttons: HBoxContainer = HBoxContainer.new()
	buttons.add_theme_constant_override("separation", int(GAP) + 2)
	buttons.mouse_filter = Control.MOUSE_FILTER_IGNORE
	group.add_child(buttons)
	# a guest reads the host's choice as text instead of buttons
	var value: Label = LobbyWidgets.label("", UiMetrics.TITLE, KalyeahTheme.TEXT)
	value.name = "Value"
	value.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	group.add_child(value)
	settings_row.add_child(group)
	return group


## The host's game picker (`RoomMapPicker`): the games' cards over a see-through blue; swipe to look, Pick (or tap the middle card) to
## choose; the X, a tap on the blue or Android Back closes it.
func open_map_carousel() -> void:
	if _map_picker == null:
		_map_picker = RoomMapPicker.new(self)
	_map_picker.open_map_carousel()


func close_map_carousel() -> void:
	if _map_picker != null:
		_map_picker.close_map_carousel()


## True while the game picker is open (Android Back closes it before it leaves the room).
func map_open() -> bool:
	return _map_picker != null and _map_picker.is_open()


func map_picker() -> RoomMapPicker:
	return _map_picker


## The top of the room's content under the notch (the safe area's top margin).
func safe_top() -> float:
	return float(_safe_margin.get_theme_constant("margin_top")) if _safe_margin != null else MARGIN


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
	team_headers = headers
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
	_refresh_qr()
	copy_button.text = "Copy"
	_copy_timer.stop()
	readiness_label.text = _readiness(room)
	action_button.visible = true
	retry_button.visible = false
	var count: int = room.get("first_to", 3) as int
	for choice: int in first_to_buttons:
		first_to_buttons[choice].set_pressed_no_signal(choice == count)
		first_to_buttons[choice].disabled = not host
	# section 1: the map (the host changes it, everyone sees it); every room is 3v3
	var map_id: StringName = StringName(room.get("map", "street") as String)
	ChoiceCard.set_card(map_card, "", MAP_NAMES.get(map_id, String(map_id).to_upper()) as String, "blue")
	map_picture.texture = StreetPage.picture_for(map_id)
	# only the host opens the map carousel
	map_card.disabled = not host
	map_hint.visible = host
	map_card.visible = host
	map_title.visible = not host
	map_title.text = MAP_NAMES.get(map_id, String(map_id).to_upper()) as String
	var size: int = room.get("team_size", RoomService.MAX_TEAM_SIZE) as int
	# a street game is timed (most points win), so it has no "race to"
	var game: StreetModeRules = StreetMode.rules_for(map_id)
	var every_kid: bool = RoomService.free_for_all(map_id)
	team_headers.visible = not every_kid
	race_group.visible = game == null
	race_group.get_child(1).visible = host
	(race_group.get_child(2) as Label).visible = not host
	(race_group.get_child(2) as Label).text = "%d" % count
	var open: bool = room.get("open", true) as bool
	for choice: bool in open_buttons:
		open_buttons[choice].set_pressed_no_signal(choice == open)
	(open_buttons[true].get_parent() as Control).visible = host and online_room
	var bots_on: bool = room.get("fill_bots", false) as bool
	bots_row.visible = room.get("bots_allowed", false) as bool
	bots_row.get_child(1).visible = host
	(bots_row.get_child(2) as Label).visible = not host
	(bots_row.get_child(2) as Label).text = "On" if bots_on else "Off"
	settings_gap.visible = race_group.visible and bots_row.visible
	settings_row.visible = race_group.visible or bots_row.visible
	bots_button.set_state(bots_on)
	# the summary line says only whether an Online room is listed: the race and the game are in the rows above, every room is 3v3, and the
	# minutes of a street game are not shown in the room (owner)
	format_summary.text = ""
	if online_room:
		format_summary.text = "Open" if open else "Private"
	format_summary.visible = not host and not format_summary.text.is_empty()
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
		# always pressable: when the game cannot start yet, pressing it says why (a toast, `start_note`)
		action_button.text = "Start battle"
		action_button.disabled = false
		KalyeahTheme.style_button(action_button, "orange" if room.get("can_start_host", false) else "grey")
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
	_refresh_qr()
	_copy_timer.stop()


func show_retry() -> void:
	retry_button.visible = true


static func _readiness(room: Dictionary) -> String:
	var note: String = room.get("start_note", "") as String
	return note if not note.is_empty() else "Ready to play"


func set_status(text: String) -> void:
	status_label.text = text
	status_label.visible = not text.is_empty()


## True once a room has been shown (before that only the top row and the status are visible).
func has_room() -> bool:
	return _content.visible


func set_online(enabled: bool) -> void:
	online_room = enabled
	code_row.visible = enabled and has_room()
	_refresh_qr()


func copy_code() -> void:
	if not online_room or not has_room() or code_label.text.is_empty():
		return
	DisplayServer.clipboard_set(code_label.text)
	UiToast.say(self, "Room code copied!")
	copy_button.text = "Copied!"
	_copy_timer.start()


## The room code as a big QR code (`RoomQr`): a friend taps Join room, then Scan QR, and points their camera at it.
func show_qr() -> MenuDialog:
	var text: String = ""
	var caption: String = ""
	if online_room and has_room() and not code_label.text.is_empty():
		text = RoomQr.payload(code_label.text)
		caption = "Friends: tap Join room, then Scan QR, and point the camera here."
	elif has_room() and not local_share.is_empty():
		# the game hotspot's Wi-Fi QR: Kalyeah joins it and finds the room; a phone's own camera can join the Wi-Fi with it too
		text = local_share
		caption = "Friends: tap Local, Join room, then Scan QR%s." % (", or hold your phones back to back" if local_nfc else "")
	if text.is_empty():
		return null
	var card: MenuDialog = MenuDialog.create("Room QR", MenuDialog.WIDTH_SHEET)
	card.name = "RoomQrDialog"
	var qr: QrView = QrView.new(text)
	qr.custom_minimum_size = Vector2(QR_SIDE, QR_SIDE)
	qr.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	card.body.add_child(qr)
	card.add_label(caption, UiMetrics.BODY, MenuDialog.TEXT_COLOR, HORIZONTAL_ALIGNMENT_CENTER)
	var done: Button = MenuDialog.make_button("Done", "orange", UiMetrics.BODY)
	done.name = "Done"
	done.pressed.connect(card.close.bind(true))
	card.add_buttons([done])
	card.open(self)
	return card


## Local host: the game hotspot to share ("" when it stopped or this phone is on an ordinary hotspot or Wi-Fi).
func set_local_share(text: String, nfc: bool = false) -> void:
	local_share = text
	local_nfc = nfc
	_refresh_qr()


## QR is there when there is something to scan: an Online room's code, or the Local host's game hotspot.
func _refresh_qr() -> void:
	qr_button.visible = has_room() and ((online_room and not code_label.text.is_empty()) or not local_share.is_empty())
