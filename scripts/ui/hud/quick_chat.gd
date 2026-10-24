class_name QuickChat
extends Control
## The quick chat button (the owner's speech bubble picture, like Clash Royale's): it sits outside the tray, above the NEXT card. A tap opens
## a short list of phrases; picking one says it, and the match shows it in a bubble over your kid for a moment. Nothing is typed.

## What you can say (kept short so each fits a bubble and a button).
const PHRASES: Array[String] = ["Good luck!", "Nice one!", "Oops!", "Thanks!", "Help me!", "Wow!"]
const COLUMNS: int = 2
## A bubble over a kid lasts this long (seconds).
const SAY_TIME: float = 2.6

signal said(text: String)

var button: TextureButton
var menu: PanelContainer
var phrase_buttons: Array[Button] = []


func _init() -> void:
	name = "QuickChat"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	button = TextureButton.new()
	button.name = "ChatButton"
	button.texture_normal = HandArt.chat()
	button.ignore_texture_size = true
	button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	button.tooltip_text = "Quick chat"
	button.accessibility_name = "Quick chat"
	button.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	button.pressed.connect(toggle)
	UiKit.feel_press(button)
	add_child(button)
	menu = PanelContainer.new()
	menu.name = "ChatMenu"
	menu.visible = false
	var grid: GridContainer = GridContainer.new()
	grid.columns = COLUMNS
	grid.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	grid.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	menu.add_child(grid)
	for phrase: String in PHRASES:
		var pick: Button = Button.new()
		pick.text = UiText.t(phrase)
		pick.name = "Say_%s" % phrase.to_lower().replace(" ", "_").replace("!", "")
		pick.custom_minimum_size = Vector2(UiMetrics.TOUCH * 2.1, UiMetrics.TOUCH * 0.8)
		pick.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
		pick.pressed.connect(say.bind(phrase))
		grid.add_child(pick)
		phrase_buttons.append(pick)
	add_child(menu)


func _ready() -> void:
	set_process_input(false)


## Opens or closes the list of phrases (it opens above the button).
func toggle() -> void:
	set_open(not menu.visible)


func set_open(value: bool) -> void:
	menu.visible = value
	set_process_input(value)
	if value:
		menu.reset_size()
		menu.position = Vector2(0.0, -menu.size.y - UiMetrics.COMPACT_GAP)
		if menu.get_global_rect().end.x > get_viewport_rect().size.x:
			menu.position.x -= menu.get_global_rect().end.x - get_viewport_rect().size.x + UiMetrics.COMPACT_GAP


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
		set_open(false)
