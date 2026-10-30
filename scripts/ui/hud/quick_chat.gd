class_name QuickChat
extends Control
## The quick chat button (the owner's speech bubble picture, like Clash Royale's): it sits outside the tray, above the NEXT card (in the Sides layout, in the tray's strip beside the ice water bar). A tap opens
## two rows of four phrases over the skills and ice water bar; picking one says it, and the match shows it in a bubble over your kid for a moment. Nothing is typed.

## What you can say (kept short so each fits a bubble and a button).
const PHRASES: Array[String] = ["Good luck!", "Nice one!", "Oops!", "Thanks!", "Help me!", "Wow!", "Attack!", "Defend!"]
const COLUMNS: int = 4
## A bubble over a kid lasts this long (seconds).
const SAY_TIME: float = 2.6
## The bubble is drawn this much smaller than its button (empty space round the art, a share of its size on each side); the touch area stays full size.
const ART_PAD: float = 0.12

signal said(text: String)
signal opened_changed(opened: bool)

var button: TextureButton
var menu: GridContainer
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
	menu = GridContainer.new()
	menu.name = "ChatGrid"
	menu.columns = COLUMNS
	menu.visible = false
	menu.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	menu.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	for phrase: String in PHRASES:
		var pick: Button = Button.new()
		pick.text = UiText.t(phrase)
		pick.name = "Say_%s" % phrase.to_lower().replace(" ", "_").replace("!", "")
		pick.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
		pick.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
		pick.add_theme_font_override("font", KalyeahTheme.ACTION_FONT)
		pick.pressed.connect(say.bind(phrase))
		pick.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		pick.size_flags_vertical = Control.SIZE_EXPAND_FILL
		for state: String in ["normal", "hover", "pressed", "focus"]:
			var style: StyleBoxFlat = StyleBoxFlat.new()
			style.bg_color = Color("dfe8f2") if state == "pressed" else Color.WHITE
			style.border_color = Color.BLACK
			style.set_border_width_all(3)
			style.set_corner_radius_all(UiSkin.RADIUS_S)
			pick.add_theme_stylebox_override(state, style)
		pick.add_theme_color_override("font_color", Color.BLACK)
		pick.add_theme_color_override("font_hover_color", Color.BLACK)
		pick.add_theme_color_override("font_pressed_color", Color.BLACK)
		pick.add_theme_constant_override("outline_size", 0)
		menu.add_child(pick)
		phrase_buttons.append(pick)
	add_child(menu)


func _ready() -> void:
	set_process_input(false)


## Opens or closes the grid over the hand.
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


## Says `phrase` (the list closes).
func say(phrase: String) -> void:
	set_open(false)
	said.emit(phrase)


## A tap anywhere outside the list closes it.
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


## The chat grid covers the four skills and ice water strip, while the icon stays above NEXT.
func place_over_hand(rect: Rect2) -> void:
	menu.position = rect.position - position
	menu.size = rect.size
