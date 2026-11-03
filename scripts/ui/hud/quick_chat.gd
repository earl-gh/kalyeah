class_name QuickChat
extends Control
## The quick chat button (the owner's speech bubble picture, like Clash Royale's), fixed at the top right of the match whatever the tray does
## (owner, 2026-11-03). A tap opens a white rounded panel under it, also at the top right, with six stickers in a grid of two columns and three
## rows; picking one says it, and the match shows it in a bubble over your kid for a moment. Nothing is typed. Each sticker is the owner's picture
## (`assets/ui/stickers/<phrase id>.webp`, a kid's face with a white sticker border) with its words under it.

## What you can say, one sticker each (kept short so each fits a bubble and a sticker's caption).
const PHRASES: Array[String] = ["Haha!", "Easy!", "Grrr!", "Love it!", "Huhu!", "Yum!"]
const COLUMNS: int = 2
## A bubble over a kid lasts this long (seconds).
const SAY_TIME: float = 2.0
## The bubble is drawn this much smaller than its button (empty space round the art, a share of its size on each side); the touch area stays full size.
const ART_PAD: float = 0.12
const STICKER_PATH: String = "res://assets/ui/stickers/%s.webp"
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


## The picture of a sticker: the owner's file.
func _art(phrase: String) -> Control:
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = load(STICKER_PATH % slug(phrase)) as Texture2D
	picture.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.custom_minimum_size = Vector2.ONE * FACE
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return picture


## "Love it!" -> "love_it": the sticker's name and its picture's file name.
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
