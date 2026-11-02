class_name RoomMapPicker
extends RefCounted
## Owns the room's game picker (the host's map overlay) and its lifetime, separate from team selection (owner, 2026-11-01):
## over the dark backdrop, a swipe carousel of the games (each card a see-through blue with its picture from the Games page and its name
## in yellow), the
## room's current game tagged "Playing now"; under it one line on how the game plays, a dot for each game and a big Pick button; a red
## X at the top right. A swipe only looks; the Pick button or a tap on the middle card picks. A pick that would turn the room into six
## places (Tumbang Preso from a smaller room) asks first.

## The dark backdrop the cards float on (as every modal's).
const TINT: Color = Color(0.01, 0.025, 0.06, 0.82)
## Every card's face: the see-through blue (owner: "the background of the actual picker cards") as it shows over the dark backdrop, with
## a lighter blue rim. It is drawn solid: really see-through, the cards stacked behind the middle one showed through it (their pictures
## and names over its own).
const CARD_GLASS: Color = Color(0.16, 0.45, 0.95, 0.42)
const CARD_FILL: Color = Color(
	CARD_GLASS.r * CARD_GLASS.a + TINT.r * TINT.a * (1.0 - CARD_GLASS.a),
	CARD_GLASS.g * CARD_GLASS.a + TINT.g * TINT.a * (1.0 - CARD_GLASS.a),
	CARD_GLASS.b * CARD_GLASS.a + TINT.b * TINT.a * (1.0 - CARD_GLASS.a))
const CARD_RIM: Color = Color(0.55, 0.78, 1.0, 0.9)
const CARD_RIM_WIDTH: int = 3
## The game's name on its card (owner: yellow), with a dark outline so it reads on every card colour.
const NAME_COLOR: Color = Color(1.0, 0.85, 0.2)
const NAME_OUTLINE: int = 8
const PLAYING_NOW: String = "Playing now"
const DOT_ON: String = "●"
const DOT_OFF: String = "○"
## The Pick button under the card.
const PICK_SIZE: Vector2 = Vector2(440.0, 104.0)

var view: HotspotRoomView
## The line on how the game in the middle plays, the dots, the Pick button and the X (built with the overlay).
var info_label: Label
var dots_label: Label
var pick_button: Button
var close_button: Button


func _init(p_view: HotspotRoomView) -> void:
	view = p_view


func open_map_carousel() -> void:
	if view._map_overlay != null and is_instance_valid(view._map_overlay):
		return
	if not view._is_host():
		return
	view._map_overlay = Control.new()
	view._map_overlay.name = "MapOverlay"
	view._map_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	view._map_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	view._map_overlay.z_index = MenuDialog.MODAL_Z
	view.add_child(view._map_overlay)
	var tint: ColorRect = ColorRect.new()
	tint.name = "Tint"
	tint.color = TINT
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.mouse_filter = Control.MOUSE_FILTER_STOP
	tint.gui_input.connect(_on_tint_input)
	view._map_overlay.add_child(tint)
	var current: int = maxi(RoomService.MAP_IDS.find(_room_map()), 0)
	view.map_carousel = MenuCarousel.new()
	view.map_carousel.name = "MapCarousel"
	view.map_carousel.card_size = view.MAP_PICK_CARD
	view.map_carousel.custom_minimum_size = Vector2(0.0, view.MAP_PICK_CARD.y + 24.0)
	# the full width, a little above the middle so the line, the dots and the Pick button fit under it
	view.map_carousel.anchor_left = 0.0
	view.map_carousel.anchor_right = 1.0
	view.map_carousel.anchor_top = 0.5
	view.map_carousel.anchor_bottom = 0.5
	view.map_carousel.offset_left = 0.0
	view.map_carousel.offset_right = 0.0
	view.map_carousel.offset_top = -(view.MAP_PICK_CARD.y + 24.0) / 2.0 - _lift()
	view.map_carousel.offset_bottom = (view.MAP_PICK_CARD.y + 24.0) / 2.0 - _lift()
	view._map_overlay.add_child(view.map_carousel)
	var made: Array[Button] = []
	for id: StringName in RoomService.MAP_IDS:
		made.append(_card(id))
	view.map_carousel.setup_cards(made, current)
	# a swipe only looks through the games; the Pick button or a tap on the middle card picks (a swipe used to pick and close)
	view.map_carousel.selected.connect(func(_index: int) -> void: _sync())
	view.map_carousel.tapped.connect(pick)
	_build_under()
	_build_close()
	_sync()


func close_map_carousel() -> void:
	if not is_instance_valid(view):
		return
	if view._map_overlay != null and is_instance_valid(view._map_overlay):
		view._map_overlay.queue_free()
	view._map_overlay = null
	view.map_carousel = null


func is_open() -> bool:
	return is_instance_valid(view) and view._map_overlay != null and is_instance_valid(view._map_overlay)


## Picks the game at `index` (the Pick button, or a tap on the middle card): the room's game already closes the picker.
func pick(index: int) -> void:
	var id: StringName = RoomService.MAP_IDS[index]
	if id == _room_map():
		close_map_carousel()
		return
	_choose(id)


## How the game `id` plays in this room, in one line ("3v3 · Teams · First to 3", "2 to 6 kids · Every kid for itself · 5 min").
func info_for(id: StringName) -> String:
	var size: int = int(view._room.get("team_size", RoomService.MAX_TEAM_SIZE))
	var game: StreetModeRules = StreetMode.rules_for(id)
	if game == null:
		return UiText.f("%dv%d · Teams · First to %d", [size, size, int(view._room.get("first_to", 3))])
	var minutes: int = roundi(game.match_seconds / 60.0)
	if RoomService.free_for_all(id):
		return UiText.f("%d to %d kids · Every kid for itself · %d min", [game.room_min_players, RoomService.MAX_TEAM_SIZE * 2, minutes])
	if game.room_min_team > 1:
		return UiText.f("%dv%d · Teams · %d or more a team · %d min", [size, size, game.room_min_team, minutes])
	return UiText.f("%dv%d · Teams · %d min", [size, size, minutes])


func _choose(id: StringName) -> void:
	view.map_chosen.emit(id)
	# a moment to see the card come forward, then back to the room
	view.get_tree().create_timer(0.35).timeout.connect(close_map_carousel)


func _room_map() -> StringName:
	return StringName(view._room.get("map", "street") as String)


## How far above the middle the cards sit, so the line, the dots and the button fit under them.
func _lift() -> float:
	return PICK_SIZE.y / 2.0 + 24.0


## A game's card: its picture and its name in yellow; the room's current game is tagged "Playing now".
func _card(id: StringName) -> Button:
	var title: String = view.MAP_NAMES.get(id, String(id).to_upper()) as String
	var card: Button = ChoiceCard.make_picture_card(title, view.MAP_COLORS.get(id, "blue") as String, view.MAP_PICK_CARD, StreetPage.picture_for(id))
	card.name = "Map_%s" % id
	card.accessibility_name = title
	_glass(card)
	var name_label: Label = card.get_node("Choice") as Label
	name_label.add_theme_color_override("font_color", NAME_COLOR)
	name_label.add_theme_color_override("font_outline_color", RoyaleStyle.NAVY)
	name_label.add_theme_constant_override("outline_size", NAME_OUTLINE)
	var heading: Label = card.get_node("Heading") as Label
	heading.text = PLAYING_NOW if id == _room_map() else ""
	heading.visible = id == _room_map()
	heading.modulate.a = 1.0
	heading.add_theme_color_override("font_color", Color.WHITE)
	heading.add_theme_color_override("font_outline_color", RoyaleStyle.NAVY)
	heading.add_theme_constant_override("outline_size", 6)
	return card


## A card's face made a see-through blue for every state (it replaces the painted blue, orange or green button face).
static func _glass(card: Button) -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = CARD_FILL
	box.border_color = CARD_RIM
	box.set_border_width_all(CARD_RIM_WIDTH)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.anti_aliasing = true
	for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
		card.add_theme_stylebox_override(state, box)


## Under the cards: the line on how the game plays, the dots, and the Pick button.
func _build_under() -> void:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Under"
	column.alignment = BoxContainer.ALIGNMENT_BEGIN
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.anchor_left = 0.0
	column.anchor_right = 1.0
	column.anchor_top = 0.5
	column.anchor_bottom = 0.5
	column.offset_left = view.MARGIN
	column.offset_right = -view.MARGIN
	column.offset_top = (view.MAP_PICK_CARD.y + 24.0) / 2.0 - _lift() + 8.0
	column.offset_bottom = column.offset_top + 260.0
	view._map_overlay.add_child(column)
	info_label = _label("Info", UiMetrics.DETAIL)
	column.add_child(info_label)
	dots_label = _label("Dots", UiMetrics.BODY)
	column.add_child(dots_label)
	# the old hint line stays for anything the picker has to say (kept as `view._map_hint` for the room view)
	view._map_hint = _label("MapHint", UiMetrics.CAPTION)
	view._map_hint.visible = false
	column.add_child(view._map_hint)
	pick_button = MenuDialog.make_button("", "gold", UiMetrics.ACTION, PICK_SIZE)
	pick_button.name = "PickButton"
	pick_button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	pick_button.pressed.connect(func() -> void: pick(view.map_carousel.index))
	column.add_child(pick_button)


func _build_close() -> void:
	close_button = MenuDialog.make_button("X", "red", UiMetrics.ACTION, Vector2.ONE * UiMetrics.MIN_TARGET)
	close_button.name = "CloseButton"
	close_button.accessibility_name = "Close"
	close_button.focus_mode = Control.FOCUS_NONE
	close_button.anchor_left = 1.0
	close_button.anchor_right = 1.0
	close_button.offset_left = -view.MARGIN - UiMetrics.MIN_TARGET
	close_button.offset_right = -view.MARGIN
	close_button.offset_top = view.safe_top()
	close_button.offset_bottom = close_button.offset_top + UiMetrics.MIN_TARGET
	close_button.pressed.connect(close_map_carousel)
	view._map_overlay.add_child(close_button)


func _label(label_name: String, font_size: int) -> Label:
	var label: Label = Label.new()
	label.name = label_name
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_color_override("font_outline_color", RoyaleStyle.NAVY)
	label.add_theme_constant_override("outline_size", 6)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## The line, the dots and the button follow the game in the middle.
func _sync() -> void:
	if view.map_carousel == null:
		return
	var index: int = view.map_carousel.index
	var id: StringName = RoomService.MAP_IDS[index]
	info_label.text = info_for(id)
	var dots: PackedStringArray = PackedStringArray()
	for i: int in RoomService.MAP_IDS.size():
		dots.append(DOT_ON if i == index else DOT_OFF)
	dots_label.text = "  ".join(dots)
	var title: String = view.MAP_NAMES.get(id, String(id)) as String
	pick_button.text = UiText.f("Keep %s", [title]) if id == _room_map() else UiText.f("Pick %s", [title])


func _on_tint_input(event: InputEvent) -> void:
	var click: InputEventMouseButton = event as InputEventMouseButton
	if click != null and click.pressed and click.button_index == MOUSE_BUTTON_LEFT:
		close_map_carousel()
