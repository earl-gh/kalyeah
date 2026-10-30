class_name NameTag
extends Control
## The name tag on the main screen: a white card that hangs from a black lanyard, with a punched slot at the top, "Hi! I'm" in small
## letters and the player's name big under it. Tapping the card opens the profile sheet to change the name. The lanyard is two straps
## of one loop, one in front of the card and one behind it, meeting at the slot and running up out of the top of the screen (a V that
## opens upward, like a lanyard round a neck). The whole tag swings very slowly from side to side, like a soft wind, with the card
## trailing a little behind its straps (it rests with Reduce motion).
##
## The node's own position is the middle of the slot.

signal tapped

const CARD_HEIGHT: float = 178.0
const CARD_MIN_WIDTH: float = 300.0
const CARD_MAX_WIDTH: float = 560.0
const CARD_SIDE_PAD: float = 36.0
## The slot: its size, and how far its middle is below the top of the card.
const SLOT_SIZE: Vector2 = Vector2(64.0, 16.0)
const SLOT_MIDDLE: float = 22.0
const STRAP_WIDTH: float = 22.0
## The two straps lean away from each other going up, this many degrees each, and meet at the slot.
const STRAP_SPREAD: float = 7.0
## The two straps are plain black webbing (`lanyard.gdshader` gives it a soft round, a fine weave and a stitch); the front one is a touch
## lighter, so it reads as the layer on top of the other.
const STRAP_SHADER: Shader = preload("res://shaders/lanyard.gdshader")
const PAPER_SHADER: Shader = preload("res://shaders/name_tag_paper.gdshader")
const STRAP_FRONT_COLOR: Color = Color("0f0f14")
const STRAP_BACK_COLOR: Color = Color("060608")
const CARD_COLOR: Color = Color.WHITE
const CARD_EDGE: Color = Color("cfd6e6")
const SLOT_RIM: Color = Color("b7c0d4")
const INK: Color = KalyeahTheme.MODAL_TEXT
const INK_MUTED: Color = KalyeahTheme.MODAL_TEXT_MUTED
## The small line under the name: light, and smaller than row 1.
const INK_LIGHT: Color = Color("7f8ba6")
const GREETING: String = "Hi! I'm"
const HINT: String = "Tap to change"
## Where the rows sit: the name's baseline (down from the top of the card), and the height of a capital as a share of the letter size
## (the rows are all capitals, so their visible top is a capital's height above their baseline).
const NAME_BASELINE: float = 124.0
const CAP_RATIO: float = 0.72
## The hint's letter size (smaller than row 1) and the room between the name's baseline and the top of its capitals.
const HINT_SIZE: int = UiMetrics.TAG_HINT
const HINT_GAP: float = 14.0
## The name's letters, from the largest down to the smallest that still reads: a longer name takes a smaller size.
const NAME_SIZES: Array[int] = [UiMetrics.HEADLINE - 4, UiMetrics.TITLE - 2, UiMetrics.BODY]
## Depths: the straps run behind the card and the logo (the logo is above all of them, `LOGO_Z`); the card is above the backdrop's lights and
## the mode models' glow, below the chosen model; the end of the front strap lies over the card.
const STRAP_Z: int = 1
const CARD_Z: int = 9
const FRONT_Z: int = 10
## The logo is drawn above every part of the lanyard (owner: the lanyard is layered under the logo).
const LOGO_Z: int = FRONT_Z + 1
## How long the front strap's end over the card is: the slot is 22 px down, so this reaches just above the card's top edge (the rest of the
## strap is behind the logo).
const CAP_LENGTH: float = 46.0
## The swing: the whole tag turns about the top of its straps by at most this many degrees, once every `SWAY_SECONDS` (very slow), and
## the card trails its straps by `LAG` radians of that cycle with `LAG_DEGREES` of its own turn about the slot.
const SWAY_DEGREES: float = 1.5
const SWAY_SECONDS: float = 9.0
const LAG: float = 0.7
const LAG_DEGREES: float = 0.9
## The card shrinks a little while it is pressed.
const PRESS_SCALE: float = 0.97
## The ID photo (owner, like a school ID): a square on the left of the card, under the slot's height, the words beside it. Shown when
## there is a photo or this device can take one (`IdPhoto`); until then it holds the avatar placeholder (a head and shoulders, no words).
const PHOTO_SIDE: float = 118.0
const PHOTO_LEFT: float = 22.0
const PHOTO_TOP: float = 44.0
const PHOTO_GAP: float = 18.0
const PHOTO_RADIUS: float = 14.0
const PHOTO_BACK: Color = Color("dfe8f7")
const PHOTO_EMPTY_BACK: Color = Color("f2f5fb")
const PHOTO_EMPTY_EDGE: Color = Color("9fb0d6")
## With a photo the card is never narrower than this (the photo, the words and the slot between them), and may grow to this so the
## widest twelve letters fit beside the photo (inside a 720 px screen).
const PHOTO_MIN_WIDTH: float = 420.0
const PHOTO_MAX_WIDTH: float = 640.0

## The tag swings while the screen idles; the screen turns it off under a page or a sheet.
var swing: bool = true:
	set(value):
		swing = value
		if not value:
			_rest()
## Editing uses a smaller amplitude so the caret stays easy to follow.
var sway_scale: float = 1.0
var card: Control
var greeting_label: Label
var name_label: Label
## "Tap to change": the small light line under the name.
var hint_label: Label
## The whole card is the button: a tap on it opens the profile sheet.
var tap_button: Button
var strap: Control
var front_strap: ColorRect
var back_strap: ColorRect
## The short end of the front strap that lies over the card (the rest of it is behind the logo).
var front_cap: ColorRect
## Marks where the slot is (the hole itself is cut by the paper's shader).
var slot: Control
## The ID photo's square, the picture in it, and the avatar placeholder shown while there is none.
var photo_frame: Panel
var photo: TextureRect
var add_photo: Control
var _paper_material: ShaderMaterial
var _name: String = ""
var _base_scale: float = 1.0
var _press: float = 1.0
var _press_tween: Tween


func _init() -> void:
	name = "NameTag"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the straps: the back one behind the card, the front one over it, both ending at the middle of the slot
	strap = Control.new()
	strap.name = "Lanyard"
	strap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(strap)
	back_strap = _strap_piece("BackStrap", STRAP_BACK_COLOR, STRAP_Z, false)
	strap.add_child(back_strap)
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
	# the slot is a real hole: the shader cuts it out of the card and its shadow, so what is behind shows through it
	_paper_material = ShaderMaterial.new()
	_paper_material.shader = PAPER_SHADER
	_paper_material.set_shader_parameter("rim_colour", SLOT_RIM)
	paper.material = _paper_material
	card.add_child(paper)
	slot = Control.new()
	slot.name = "Slot"
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(slot)
	photo_frame = Panel.new()
	photo_frame.name = "Photo"
	photo_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	photo_frame.position = Vector2(PHOTO_LEFT, PHOTO_TOP)
	photo_frame.size = Vector2(PHOTO_SIDE, PHOTO_SIDE)
	card.add_child(photo_frame)
	photo = TextureRect.new()
	photo.name = "Picture"
	photo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	photo.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	photo.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	photo.position = Vector2(3.0, 3.0)
	photo.size = Vector2(PHOTO_SIDE - 6.0, PHOTO_SIDE - 6.0)
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = UiKit.CROP
	crop.set_shader_parameter("rect_size", photo.size)
	crop.set_shader_parameter("radius", PHOTO_RADIUS - 3.0)
	photo.material = crop
	photo_frame.add_child(photo)
	add_photo = AvatarMark.new()
	add_photo.name = "AvatarPlaceholder"
	add_photo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_photo.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	photo_frame.add_child(add_photo)
	# the two rows are placed by hand (see `_place_text`): row 1 sits exactly between the slot and the name
	var words: Control = Control.new()
	words.name = "Words"
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
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
	name_label = Label.new()
	name_label.name = "PlayerName"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.add_theme_color_override("font_color", INK)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.add_child(name_label)
	hint_label = Label.new()
	hint_label.name = "Hint"
	hint_label.text = HINT
	hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint_label.theme_type_variation = &"DisplayLabel"
	hint_label.add_theme_font_size_override("font_size", HINT_SIZE)
	hint_label.add_theme_color_override("font_color", INK_LIGHT)
	hint_label.add_theme_constant_override("outline_size", 0)
	hint_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hint_label.set_meta(UiMetrics.BELOW_FLOOR_BY_OWNER, true)
	words.add_child(hint_label)
	# the front strap goes over the top edge of the card down into the slot
	front_strap = _strap_piece("FrontStrap", STRAP_FRONT_COLOR, STRAP_Z, true)
	strap.add_child(front_strap)
	front_cap = _strap_piece("FrontCap", STRAP_FRONT_COLOR, FRONT_Z, true)
	strap.add_child(front_cap)
	# the whole card is the button (over the words, under the front strap's edge)
	tap_button = Button.new()
	tap_button.name = "TapButton"
	tap_button.flat = true
	# a finger needs no focus, and a focus ring left on the card after the editor closes read as a yellow outline
	tap_button.focus_mode = Control.FOCUS_NONE
	tap_button.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		tap_button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	tap_button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	tap_button.button_down.connect(_press_to.bind(PRESS_SCALE, 0.08))
	tap_button.button_up.connect(_press_to.bind(1.0, 0.16))
	tap_button.pressed.connect(tapped.emit)
	card.add_child(tap_button)
	refresh_photo()
	set_player_name(Session.player_name if not Session.player_name.is_empty() else Session.DEFAULT_NAME)


## Shows the saved ID photo, the avatar placeholder while there is none, or no square at all on a device with no camera and no photo.
func refresh_photo() -> void:
	var picture: Texture2D = IdPhoto.texture()
	photo.texture = picture
	photo.visible = picture != null
	add_photo.visible = picture == null
	photo_frame.visible = picture != null or IdPhoto.camera_possible()
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = PHOTO_BACK if picture != null else PHOTO_EMPTY_BACK
	box.border_color = CARD_EDGE if picture != null else PHOTO_EMPTY_EDGE
	box.set_border_width_all(3)
	box.set_corner_radius_all(int(PHOTO_RADIUS))
	box.anti_aliasing = true
	photo_frame.add_theme_stylebox_override("panel", box)
	if not _name.is_empty():
		set_player_name(_name)


## The narrowest and widest the card may be now (wider with the photo square).
func min_width() -> float:
	return PHOTO_MIN_WIDTH if photo_frame.visible else CARD_MIN_WIDTH


func max_width() -> float:
	return PHOTO_MAX_WIDTH if photo_frame.visible else CARD_MAX_WIDTH


## Where the words start: beside the photo when there is one, else the card's own margin.
func _text_left() -> float:
	return PHOTO_LEFT + PHOTO_SIDE + PHOTO_GAP if photo_frame.visible else CARD_SIDE_PAD


## One strap of the lanyard: a black band with its bottom end at the middle of the slot (this node's origin), leaning `STRAP_SPREAD` degrees
## to one side. `front` is the one that leans left and is a little lighter.
func _strap_piece(piece_name: String, color: Color, depth: int, front: bool) -> ColorRect:
	var band: ColorRect = ColorRect.new()
	band.name = piece_name
	band.color = Color.WHITE
	band.mouse_filter = Control.MOUSE_FILTER_IGNORE
	band.z_as_relative = false
	band.z_index = depth
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = STRAP_SHADER
	material.set_shader_parameter("base_colour", color)
	if not front:
		material.set_shader_parameter("stitch_alpha", 0.08)
		material.set_shader_parameter("edge_alpha", 0.06)
	band.material = material
	return band


func _process(_delta: float) -> void:
	if not swing or Settings.reduced_motion or not is_visible_in_tree():
		_rest()
		return
	var phase: float = float(Time.get_ticks_msec()) / 1000.0 * TAU / SWAY_SECONDS
	rotation = deg_to_rad(SWAY_DEGREES * sway_scale) * sin(phase)
	card.rotation = deg_to_rad(LAG_DEGREES * sway_scale) * sin(phase - LAG)


func _rest() -> void:
	if card != null and (rotation != 0.0 or card.rotation != 0.0):
		rotation = 0.0
		card.rotation = 0.0


func _press_to(target: float, seconds: float) -> void:
	if is_instance_valid(_press_tween):
		_press_tween.kill()
	if Settings.reduced_motion or not is_inside_tree():
		_press = target
		_apply_scale()
		return
	_press_tween = create_tween()
	_press_tween.tween_method(func(value: float) -> void:
		_press = value
		_apply_scale(), _press, target, seconds)


func _apply_scale() -> void:
	card.scale = Vector2.ONE * _base_scale * _press


## Shows `text` as the name (in capitals, like a printed tag; empty shows the default name).
func set_player_name(text: String) -> void:
	_name = text.strip_edges()
	if _name.is_empty():
		_name = Session.DEFAULT_NAME
	var shown: String = _name.to_upper()
	name_label.text = shown
	accessibility_name = "Hi! I'm %s" % _name
	tap_button.accessibility_name = "Hi! I'm %s" % _name
	tap_button.accessibility_description = HINT
	# the biggest letters whose name still fits the widest card
	var room: float = max_width() - _text_left() - CARD_SIDE_PAD
	var letters: int = UiMetrics.BODY  # the smallest of NAME_SIZES, for a name too long for any
	var font: Font = name_label.get_theme_font("font")
	for candidate: int in NAME_SIZES:
		if font.get_string_size(shown, HORIZONTAL_ALIGNMENT_LEFT, -1.0, candidate).x <= room:
			letters = candidate
			break
	name_label.add_theme_font_size_override("font_size", letters)
	var name_width: float = font.get_string_size(shown, HORIZONTAL_ALIGNMENT_LEFT, -1.0, letters).x
	_fit(clampf(name_width + _text_left() + CARD_SIDE_PAD, min_width(), max_width()))
	_place_text(letters)


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
	_paper_material.set_shader_parameter("rect_size", card.size)
	_paper_material.set_shader_parameter("slot_centre", Vector2(width / 2.0, SLOT_MIDDLE))
	_paper_material.set_shader_parameter("slot_half", SLOT_SIZE / 2.0)


## Places the rows: the name on its baseline, "Tap to change" under it, and "Hi! I'm" exactly between the bottom of the slot and the top of the name's capitals
## (the same room above it as under it).
func _place_text(letters: int) -> void:
	var left: float = _text_left()
	var inner: float = card.size.x - left - CARD_SIDE_PAD
	var align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_LEFT if photo_frame.visible else HORIZONTAL_ALIGNMENT_CENTER
	for label: Label in [greeting_label, name_label, hint_label]:
		label.horizontal_alignment = align
	var name_font: Font = name_label.get_theme_font("font")
	var name_height: float = name_font.get_height(letters)
	name_label.size = Vector2(inner, name_height)
	name_label.position = Vector2(left, NAME_BASELINE - name_font.get_ascent(letters))
	var greeting_size: int = UiMetrics.DETAIL
	var greeting_font: Font = greeting_label.get_theme_font("font")
	var hole_bottom: float = SLOT_MIDDLE + SLOT_SIZE.y / 2.0
	var name_top: float = NAME_BASELINE - CAP_RATIO * float(letters)
	var cap: float = CAP_RATIO * float(greeting_size)
	var greeting_baseline: float = (hole_bottom + name_top) / 2.0 + cap / 2.0
	greeting_label.size = Vector2(inner, greeting_font.get_height(greeting_size))
	greeting_label.position = Vector2(left, greeting_baseline - greeting_font.get_ascent(greeting_size))
	# "Tap to change" under the name
	var hint_font: Font = hint_label.get_theme_font("font")
	var hint_baseline: float = NAME_BASELINE + HINT_GAP + CAP_RATIO * float(HINT_SIZE)
	hint_label.size = Vector2(inner, hint_font.get_height(HINT_SIZE))
	hint_label.position = Vector2(left, hint_baseline - hint_font.get_ascent(HINT_SIZE))


## Hangs the card with the middle of its slot at `hole` (this node's parent's pixels), and the lanyard up to `top` (the same pixels,
## above the screen). `card_scale` shrinks the card on a short phone; the lanyard keeps its width. The whole tag swings about the top
## of the straps.
func hang(hole: Vector2, top: float, card_scale: float = 1.0) -> void:
	position = hole
	_base_scale = card_scale
	_apply_scale()
	var length: float = maxf(hole.y - top, 1.0)
	pivot_offset = Vector2(0.0, -length)
	# each strap is long enough to leave the screen after leaning
	var reach: float = length / cos(deg_to_rad(STRAP_SPREAD)) + STRAP_WIDTH
	for piece: ColorRect in [front_strap, back_strap]:
		_lay_strap(piece, reach)
	# the front strap's end, over the card: from the slot to a little above the card's top edge
	_lay_strap(front_cap, minf(CAP_LENGTH * card_scale, reach))
	front_strap.rotation = deg_to_rad(-STRAP_SPREAD)
	front_cap.rotation = front_strap.rotation
	back_strap.rotation = deg_to_rad(STRAP_SPREAD)


## A strap `length` long standing on the middle of the slot (its bottom end), before it leans.
func _lay_strap(piece: ColorRect, length: float) -> void:
	piece.size = Vector2(STRAP_WIDTH, length)
	piece.position = Vector2(-STRAP_WIDTH / 2.0, -length)
	piece.pivot_offset = Vector2(STRAP_WIDTH / 2.0, length)
	(piece.material as ShaderMaterial).set_shader_parameter("rect_size", piece.size)


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


## The avatar placeholder drawn in code: a round head over a pair of shoulders, in the light blue of the square's edge.
class AvatarMark extends Control:
	const COLOR: Color = Color("b9c6e4")

	func _draw() -> void:
		var w: float = size.x
		var h: float = size.y
		draw_circle(Vector2(w * 0.5, h * 0.37), h * 0.175, COLOR)
		# the shoulders: the top half of a wide ellipse, cut flat at the bottom
		var points: PackedVector2Array = PackedVector2Array()
		for i: int in 25:
			var angle: float = PI + PI * float(i) / 24.0
			points.append(Vector2(w * 0.5 + cos(angle) * w * 0.33, h * 0.9 + sin(angle) * h * 0.3))
		draw_colored_polygon(points, COLOR)
