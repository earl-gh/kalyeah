class_name NameTag
extends Control
## The name tag on the main screen: a white card that hangs from a black lanyard, with a punched slot at the top, "Hi! I'm" in small
## letters and the player's name big under it, and a round edit button (a pencil) beside the name that opens the profile sheet. The
## lanyard runs up out of the top of the screen. The card sways a little while the screen idles (it rests with Reduce motion).
##
## The node's own position is the middle of the slot. The lanyard is two pieces of one black strap: the long upper one, behind the logo,
## and a short end on the card that follows its swing.

signal edit_pressed

const CARD_HEIGHT: float = 156.0
const CARD_MIN_WIDTH: float = 300.0
const CARD_MAX_WIDTH: float = 560.0
const CARD_SIDE_PAD: float = 28.0
## The slot: its size, and how far its middle is below the top of the card.
const SLOT_SIZE: Vector2 = Vector2(64.0, 16.0)
const SLOT_MIDDLE: float = 22.0
const STRAP_WIDTH: float = 22.0
const STRAP_COLOR: Color = Color("0b0b10")
const CARD_COLOR: Color = Color.WHITE
const CARD_EDGE: Color = Color("cfd6e6")
const SLOT_COLOR: Color = Color("232838")
const SLOT_RIM: Color = Color("b7c0d4")
const INK: Color = KalyeahTheme.MODAL_TEXT
const INK_MUTED: Color = KalyeahTheme.MODAL_TEXT_MUTED
const GREETING: String = "Hi! I'm"
## The name's letters, from the largest down to the smallest that still reads: a longer name takes a smaller size.
const NAME_SIZES: Array[int] = [UiMetrics.HEADLINE, UiMetrics.TITLE, UiMetrics.BODY]
## The edit button: how big it is to the thumb, and how big its pencil badge is drawn.
const EDIT_HIT: float = UiMetrics.TOUCH
const EDIT_DRAW: float = 56.0
## Depths: above the backdrop's lights and the mode models' glow, below the chosen model; the upper strap is behind the logo.
const CARD_Z: int = 9
const STRAP_Z: int = 1
## The sway: how far (degrees), how fast, and the part of the card (its slot) it swings about.
const SWAY_DEGREES: float = 1.4
const SWAY_SPEED: float = 0.9

## The card swings while the screen idles; the screen turns it off under a page or a sheet.
var swing: bool = true:
	set(value):
		swing = value
		if not value and card != null:
			card.rotation = 0.0
var card: Control
var greeting_label: Label
var name_label: Label
var edit_button: Button
var strap: ColorRect
var slot: Panel
var _strap_end: ColorRect
var _name: String = ""


func _init() -> void:
	name = "NameTag"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	strap = ColorRect.new()
	strap.name = "Lanyard"
	strap.color = STRAP_COLOR
	strap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	strap.z_as_relative = false
	strap.z_index = STRAP_Z
	add_child(strap)
	card = Control.new()
	card.name = "Card"
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.z_as_relative = false
	card.z_index = CARD_Z
	add_child(card)
	var paper: Panel = Panel.new()
	paper.name = "Paper"
	paper.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	paper.add_theme_stylebox_override("panel", _paper_style())
	card.add_child(paper)
	slot = Panel.new()
	slot.name = "Slot"
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slot.add_theme_stylebox_override("panel", _slot_style())
	card.add_child(slot)
	# the end of the strap goes into the slot: on the card, so it swings with it
	_strap_end = ColorRect.new()
	_strap_end.name = "StrapEnd"
	_strap_end.color = STRAP_COLOR
	_strap_end.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(_strap_end)
	var words: VBoxContainer = VBoxContainer.new()
	words.name = "Words"
	words.add_theme_constant_override("separation", 0)
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	words.offset_top = SLOT_MIDDLE + SLOT_SIZE.y / 2.0 + 8.0
	words.offset_bottom = -6.0
	words.offset_left = CARD_SIDE_PAD
	words.offset_right = -CARD_SIDE_PAD
	card.add_child(words)
	greeting_label = Label.new()
	greeting_label.name = "Greeting"
	greeting_label.text = GREETING
	greeting_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	greeting_label.theme_type_variation = &"DisplayLabel"
	greeting_label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	greeting_label.add_theme_color_override("font_color", INK_MUTED)
	greeting_label.add_theme_constant_override("outline_size", 0)
	greeting_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.add_child(greeting_label)
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "NameRow"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 6)
	row.size_flags_vertical = Control.SIZE_EXPAND_FILL
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.add_child(row)
	name_label = Label.new()
	name_label.name = "PlayerName"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_color_override("font_color", INK)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(name_label)
	edit_button = Button.new()
	edit_button.name = "EditButton"
	edit_button.accessibility_name = "Edit name"
	edit_button.flat = true
	edit_button.focus_mode = Control.FOCUS_ALL
	edit_button.custom_minimum_size = Vector2.ONE * EDIT_HIT
	edit_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		edit_button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	edit_button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	var pencil: PencilIcon = PencilIcon.new()
	pencil.name = "Pencil"
	pencil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pencil.size = Vector2.ONE * EDIT_DRAW
	pencil.position = (Vector2.ONE * EDIT_HIT - pencil.size) / 2.0
	edit_button.add_child(pencil)
	UiKit.feel_press(edit_button)
	edit_button.pressed.connect(edit_pressed.emit)
	row.add_child(edit_button)
	set_player_name(Session.player_name if not Session.player_name.is_empty() else Session.DEFAULT_NAME)


func _process(_delta: float) -> void:
	if not swing or Settings.reduced_motion or not is_visible_in_tree():
		if card.rotation != 0.0:
			card.rotation = 0.0
		return
	card.rotation = deg_to_rad(SWAY_DEGREES) * sin(float(Time.get_ticks_msec()) / 1000.0 * SWAY_SPEED * TAU / 2.0)


## Shows `text` as the name (in capitals, like a printed tag; empty shows the default name).
func set_player_name(text: String) -> void:
	_name = text.strip_edges()
	if _name.is_empty():
		_name = Session.DEFAULT_NAME
	var shown: String = _name.to_upper()
	name_label.text = shown
	accessibility_name = "Hi! I'm %s" % _name
	# the biggest letters whose name still fits the widest card
	var room: float = CARD_MAX_WIDTH - CARD_SIDE_PAD * 2.0 - EDIT_HIT - 6.0
	var letters: int = UiMetrics.BODY  # the smallest of NAME_SIZES, for a name too long for any
	var font: Font = name_label.get_theme_font("font")
	for candidate: int in NAME_SIZES:
		if font.get_string_size(shown, HORIZONTAL_ALIGNMENT_LEFT, -1.0, candidate).x <= room:
			letters = candidate
			break
	name_label.add_theme_font_size_override("font_size", letters)
	var name_width: float = font.get_string_size(shown, HORIZONTAL_ALIGNMENT_LEFT, -1.0, letters).x
	var width: float = clampf(name_width + EDIT_HIT + 6.0 + CARD_SIDE_PAD * 2.0, CARD_MIN_WIDTH, CARD_MAX_WIDTH)
	_fit(width)


## The name shown now (as typed, not in capitals).
func player_name() -> String:
	return _name


## Sizes and places the card and the slot for a card `width` wide: the node's origin is the middle of the slot.
func _fit(width: float) -> void:
	card.size = Vector2(width, CARD_HEIGHT)
	card.position = Vector2(-width / 2.0, -SLOT_MIDDLE)
	card.pivot_offset = Vector2(width / 2.0, SLOT_MIDDLE)
	slot.size = SLOT_SIZE
	slot.position = Vector2((width - SLOT_SIZE.x) / 2.0, SLOT_MIDDLE - SLOT_SIZE.y / 2.0)
	# the strap's end enters the slot from the top edge of the card down to the middle of the slot
	_strap_end.size = Vector2(STRAP_WIDTH, SLOT_MIDDLE)
	_strap_end.position = Vector2((width - STRAP_WIDTH) / 2.0, 0.0)


## Hangs the card with the middle of its slot at `hole` (this node's parent's pixels), and the lanyard up to `top` (the same pixels,
## above the screen). `card_scale` shrinks the card on a short phone; the lanyard keeps its width.
func hang(hole: Vector2, top: float, card_scale: float = 1.0) -> void:
	position = hole
	card.scale = Vector2.ONE * card_scale
	var length: float = hole.y - top
	strap.size = Vector2(STRAP_WIDTH, maxf(length, 0.0))
	strap.position = Vector2(-STRAP_WIDTH / 2.0, -length)


static func _paper_style() -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = CARD_COLOR
	box.set_corner_radius_all(UiSkin.RADIUS_L)
	box.set_border_width_all(3)
	box.border_color = CARD_EDGE
	box.shadow_color = Color(0.0, 0.02, 0.1, 0.42)
	box.shadow_size = 16
	box.shadow_offset = Vector2(0.0, 8.0)
	box.anti_aliasing = true
	return box


static func _slot_style() -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = SLOT_COLOR
	box.set_corner_radius_all(UiSkin.RADIUS_S)
	box.set_border_width_all(3)
	box.border_color = SLOT_RIM
	box.anti_aliasing = true
	return box
