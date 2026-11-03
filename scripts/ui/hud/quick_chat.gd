class_name QuickChat
extends Control
## The quick chat button (the owner's speech bubble picture, like Clash Royale's), fixed at the top right of the match whatever the tray does
## (owner, 2026-11-03). A tap opens a white rounded panel under it, also at the top right, with six stickers in a grid of two columns and three
## rows; picking one says it, and the match shows it in a bubble over your kid for a moment. Nothing is typed. The stickers are drawn in code
## (round faces, `StickerFace`) until the owner's pictures are added as `assets/ui/stickers/<phrase id>.png`, which then take their place.

## What you can say, one sticker each (kept short so each fits a bubble and a sticker's caption).
const PHRASES: Array[String] = ["Good luck!", "Nice one!", "Oops!", "Thanks!", "Help me!", "Wow!"]
const COLUMNS: int = 2
## A bubble over a kid lasts this long (seconds).
const SAY_TIME: float = 2.6
## The bubble is drawn this much smaller than its button (empty space round the art, a share of its size on each side); the touch area stays full size.
const ART_PAD: float = 0.12
const STICKER_DIR: String = "res://assets/ui/stickers/"
## One sticker's box, and the panel's room round the grid.
const STICKER_SIZE: Vector2 = Vector2(130.0, 140.0)
const PANEL_PAD: float = 16.0
const GRID_GAP: int = 12
const FACE: float = 84.0
const PANEL_COLOR: Color = Color.WHITE
const INK: Color = Color("2b3350")

signal said(text: String)
signal opened_changed(opened: bool)

var button: TextureButton
## The white rounded panel with the stickers (`grid`).
var menu: PanelContainer
var grid: GridContainer
var phrase_buttons: Array[Button] = []


func _init() -> void:
	name = "QuickChat"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	button = TextureButton.new()
	button.name = "ChatButton"
	button.texture_normal = HandArt.chat_padded(ART_PAD)
	button.ignore_texture_size = true
	button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	button.tooltip_text = "Quick chat"
	button.accessibility_name = "Quick chat"
	button.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	button.pressed.connect(toggle)
	UiKit.feel_press(button)
	add_child(button)
	menu = PanelContainer.new()
	menu.name = "ChatPanel"
	menu.visible = false
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = PANEL_COLOR
	box.set_corner_radius_all(UiSkin.RADIUS_XL)
	box.set_content_margin_all(PANEL_PAD)
	box.shadow_color = Color(0.0, 0.0, 0.0, 0.4)
	box.shadow_size = 10
	box.anti_aliasing = true
	menu.add_theme_stylebox_override("panel", box)
	grid = GridContainer.new()
	grid.name = "ChatGrid"
	grid.columns = COLUMNS
	grid.add_theme_constant_override("h_separation", GRID_GAP)
	grid.add_theme_constant_override("v_separation", GRID_GAP)
	menu.add_child(grid)
	for phrase: String in PHRASES:
		var pick: Button = _sticker(phrase)
		grid.add_child(pick)
		phrase_buttons.append(pick)
	add_child(menu)


## One sticker: a flat button holding its picture and its words under it.
func _sticker(phrase: String) -> Button:
	var pick: Button = Button.new()
	pick.name = "Say_%s" % slug(phrase)
	pick.custom_minimum_size = STICKER_SIZE
	pick.accessibility_name = UiText.t(phrase)
	pick.focus_mode = Control.FOCUS_ALL
	for state: String in ["normal", "hover", "focus"]:
		var flat: StyleBoxFlat = StyleBoxFlat.new()
		flat.bg_color = Color(0.0, 0.0, 0.0, 0.0)
		flat.set_corner_radius_all(UiSkin.RADIUS_M)
		pick.add_theme_stylebox_override(state, flat)
	var pressed: StyleBoxFlat = StyleBoxFlat.new()
	pressed.bg_color = Color("dfe8f2")
	pressed.set_corner_radius_all(UiSkin.RADIUS_M)
	pick.add_theme_stylebox_override("pressed", pressed)
	pick.pressed.connect(say.bind(phrase))
	var column: VBoxContainer = VBoxContainer.new()
	column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 4)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pick.add_child(column)
	var art: Control = _art(phrase)
	art.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	column.add_child(art)
	var words: Label = Label.new()
	words.name = "Words"
	words.text = UiText.t(phrase)
	words.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	words.add_theme_font_override("font", KalyeahTheme.ACTION_FONT)
	words.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
	words.add_theme_color_override("font_color", INK)
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(words)
	return pick


## The picture of a sticker: the owner's file when it is in the project, else the code-drawn face.
func _art(phrase: String) -> Control:
	var path: String = "%s%s.png" % [STICKER_DIR, slug(phrase)]
	if ResourceLoader.exists(path):
		var picture: TextureRect = TextureRect.new()
		picture.name = "Picture"
		picture.texture = load(path) as Texture2D
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		picture.custom_minimum_size = Vector2.ONE * FACE
		picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
		return picture
	var face: StickerFace = StickerFace.new(phrase)
	face.custom_minimum_size = Vector2.ONE * FACE
	return face


## "Good luck!" -> "good_luck": the sticker's name and its picture's file name.
static func slug(phrase: String) -> String:
	return phrase.to_lower().replace(" ", "_").replace("!", "")


func _ready() -> void:
	set_process_input(false)


## Opens or closes the sticker panel.
func toggle() -> void:
	set_open(not menu.visible)


func set_open(value: bool) -> void:
	if menu.visible == value:
		return
	menu.visible = value
	set_process_input(value)
	opened_changed.emit(value)


func is_open() -> bool:
	return menu.visible


## Says `phrase` (the panel closes).
func say(phrase: String) -> void:
	set_open(false)
	said.emit(phrase)


## A tap anywhere outside the panel closes it.
func _input(event: InputEvent) -> void:
	if not menu.visible:
		return
	var at: Variant = null
	if event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed:
		at = (event as InputEventScreenTouch).position
	elif event is InputEventMouseButton and (event as InputEventMouseButton).pressed:
		at = (event as InputEventMouseButton).position
	if at != null and not menu.get_global_rect().has_point(at as Vector2) and not button.get_global_rect().has_point(at as Vector2):
		get_viewport().set_input_as_handled()
		set_open(false)


## The panel's size: two columns of stickers and three rows, with its room round them.
static func panel_size() -> Vector2:
	var rows: int = ceili(float(PHRASES.size()) / float(COLUMNS))
	return Vector2(
		float(COLUMNS) * STICKER_SIZE.x + float(COLUMNS - 1) * GRID_GAP + PANEL_PAD * 2.0,
		float(rows) * STICKER_SIZE.y + float(rows - 1) * GRID_GAP + PANEL_PAD * 2.0)


## Puts the panel at `rect` (in the HUD's coordinates), the button's own position taken off.
func place_panel(rect: Rect2) -> void:
	menu.position = rect.position - position
	menu.size = rect.size


## A round face drawn in code, one look for each thing you can say.
class StickerFace extends Control:
	const SKIN: Color = Color("ffd34e")
	const SKIN_EDGE: Color = Color("c98a12")
	const FEATURE: Color = Color("3b2a14")
	const BLUSH: Color = Color(1.0, 0.45, 0.45, 0.7)
	const TEAR: Color = Color("4aa8ff")

	var phrase: String

	func _init(p_phrase: String) -> void:
		phrase = p_phrase
		name = "Face"
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var middle: Vector2 = size / 2.0
		var radius: float = minf(size.x, size.y) / 2.0 - 3.0
		draw_circle(middle, radius, SKIN_EDGE)
		draw_circle(middle, radius - 3.0, SKIN)
		var eye_y: float = middle.y - radius * 0.18
		var eye_dx: float = radius * 0.36
		var eye: float = radius * 0.11
		match QuickChat.slug(phrase):
			"good_luck":
				# a wink and a wide smile
				draw_circle(Vector2(middle.x - eye_dx, eye_y), eye, FEATURE)
				draw_arc(Vector2(middle.x + eye_dx, eye_y), eye * 1.6, PI, TAU, 12, FEATURE, 4.0, true)
				_smile(middle, radius, 0.5)
			"nice_one":
				# star-struck: big shiny eyes and a grin
				for side: float in [-1.0, 1.0]:
					draw_circle(Vector2(middle.x + side * eye_dx, eye_y), eye * 1.8, FEATURE)
					draw_circle(Vector2(middle.x + side * eye_dx - eye * 0.5, eye_y - eye * 0.6), eye * 0.6, Color.WHITE)
				_smile(middle, radius, 0.62)
			"oops":
				# an awkward face: one eyebrow up, a wobbly mouth and a drop of sweat
				for side: float in [-1.0, 1.0]:
					draw_circle(Vector2(middle.x + side * eye_dx, eye_y), eye, FEATURE)
				draw_line(Vector2(middle.x - eye_dx - eye * 1.5, eye_y - eye * 3.0), Vector2(middle.x - eye_dx + eye * 1.5, eye_y - eye * 2.2), FEATURE, 4.0, true)
				var mouth: PackedVector2Array = PackedVector2Array()
				for i: int in 9:
					var t: float = float(i) / 8.0
					mouth.append(Vector2(middle.x - radius * 0.38 + radius * 0.76 * t, middle.y + radius * 0.42 + sin(t * TAU * 1.5) * radius * 0.06))
				draw_polyline(mouth, FEATURE, 4.0, true)
				draw_circle(Vector2(middle.x + radius * 0.68, middle.y - radius * 0.5), eye * 1.2, TEAR)
			"thanks":
				# happy closed eyes, blushing cheeks and a small smile
				for side: float in [-1.0, 1.0]:
					draw_arc(Vector2(middle.x + side * eye_dx, eye_y + eye), eye * 1.7, PI, TAU, 12, FEATURE, 4.0, true)
					draw_circle(Vector2(middle.x + side * eye_dx * 1.55, middle.y + radius * 0.2), eye * 1.5, BLUSH)
				_smile(middle, radius, 0.38)
			"help_me":
				# worried: raised brows, wide eyes, an open mouth and a tear
				for side: float in [-1.0, 1.0]:
					draw_circle(Vector2(middle.x + side * eye_dx, eye_y), eye * 1.3, FEATURE)
					draw_line(Vector2(middle.x + side * eye_dx - side * eye * 1.6, eye_y - eye * 3.2), Vector2(middle.x + side * eye_dx + side * eye * 1.6, eye_y - eye * 2.2), FEATURE, 4.0, true)
				draw_circle(Vector2(middle.x, middle.y + radius * 0.48), radius * 0.2, FEATURE)
				draw_circle(Vector2(middle.x - eye_dx - eye, eye_y + eye * 4.0), eye * 1.1, TEAR)
			_:
				# wow: round eyes and a round open mouth
				for side: float in [-1.0, 1.0]:
					draw_circle(Vector2(middle.x + side * eye_dx, eye_y), eye * 1.6, Color.WHITE)
					draw_circle(Vector2(middle.x + side * eye_dx, eye_y), eye * 0.9, FEATURE)
				draw_circle(Vector2(middle.x, middle.y + radius * 0.45), radius * 0.24, FEATURE)

	func _smile(middle: Vector2, radius: float, depth: float) -> void:
		draw_arc(Vector2(middle.x, middle.y + radius * 0.18), radius * depth, 0.25, PI - 0.25, 16, FEATURE, 4.0, true)
