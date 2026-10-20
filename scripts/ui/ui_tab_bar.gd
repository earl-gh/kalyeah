class_name UiTabBar
extends PanelContainer
## The main screen's bottom navigation: a night-glass bar with one big tab per destination (an icon
## over a label). The open tab turns light blue (gold is kept for the one main action) and a raised plate glides
## under it from the tab it left (it jumps with Reduce motion); the others stay quiet.
## Tabs are at least a thumb tall and share the width equally.

signal tab_pressed(id: StringName)

const HEIGHT: float = 128.0
const ICON_SIZE: float = 52.0
const ACTIVE: Color = Color("8fd0ff")
const IDLE: Color = Color("9fb0d6")

var tabs: Dictionary[StringName, Button] = {}
var active: StringName = &""
var _icons: Dictionary[StringName, NavIcon] = {}
var _labels: Dictionary[StringName, Label] = {}
## The raised plate under the open tab.
var glide: Panel
var _row: HBoxContainer
var _stack: Control
var _glide_tween: Tween


## `items`: {id, label, icon} in order, left to right.
func _init(items: Array[Dictionary] = []) -> void:
	name = "TabBar"
	add_theme_stylebox_override("panel", UiSkin.plate("night", 8.0, 34))
	custom_minimum_size = Vector2(0.0, HEIGHT)
	# a plain Control holds the plate and the row, so the plate can be placed by hand
	_stack = Control.new()
	_stack.name = "Stack"
	_stack.custom_minimum_size = Vector2(0.0, HEIGHT - 16.0)
	add_child(_stack)
	glide = Panel.new()
	glide.name = "Glide"
	glide.mouse_filter = Control.MOUSE_FILTER_IGNORE
	glide.visible = false
	glide.add_theme_stylebox_override("panel", UiSkin.plate("raised", 0.0, 26))
	_stack.add_child(glide)
	_row = HBoxContainer.new()
	_row.add_theme_constant_override("separation", 6)
	_row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_stack.add_child(_row)
	_stack.resized.connect(_place_glide.bind(false))
	for item: Dictionary in items:
		_add_tab(item["id"] as StringName, String(item["label"]), item["icon"] as StringName)


func _add_tab(id: StringName, text: String, icon_kind: StringName) -> void:
	var button: Button = Button.new()
	button.name = "Tab%s" % String(id).capitalize()
	button.accessibility_name = text
	button.focus_mode = Control.FOCUS_ALL
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.custom_minimum_size = Vector2(0.0, HEIGHT - 16.0)
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(26))
	button.pressed.connect(tab_pressed.emit.bind(id))
	MenuDialog.feel_press(button)
	var stack: VBoxContainer = VBoxContainer.new()
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stack.alignment = BoxContainer.ALIGNMENT_CENTER
	stack.add_theme_constant_override("separation", 2)
	stack.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(stack)
	var holder: CenterContainer = CenterContainer.new()
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var icon: NavIcon = NavIcon.new(icon_kind)
	icon.custom_minimum_size = Vector2(ICON_SIZE, ICON_SIZE)
	icon.color = IDLE
	holder.add_child(icon)
	stack.add_child(holder)
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	label.add_theme_color_override("font_color", IDLE)
	label.add_theme_constant_override("outline_size", 0)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stack.add_child(label)
	_row.add_child(button)
	tabs[id] = button
	_icons[id] = icon
	_labels[id] = label
	_paint(id, false)


## Lifts `id` and quiets the rest.
func set_active(id: StringName) -> void:
	active = id
	for other: StringName in tabs:
		_paint(other, other == id)
	_place_glide(true)


func _paint(id: StringName, on: bool) -> void:
	var button: Button = tabs[id]
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var color: Color = ACTIVE if on else IDLE
	_icons[id].color = color
	_labels[id].add_theme_color_override("font_color", color)


## Puts the plate under the open tab (`animate`: it glides there from where it was).
func _place_glide(animate: bool) -> void:
	if not tabs.has(active):
		glide.visible = false
		return
	var button: Button = tabs[active]
	if button.size.x <= 0.0 or _stack.size.x <= 0.0:
		# not laid out yet: the stack's resized signal places it
		return
	var target: Vector2 = button.global_position - _stack.global_position
	var was_visible: bool = glide.visible
	glide.visible = true
	glide.size = button.size
	if is_instance_valid(_glide_tween):
		_glide_tween.kill()
	if not animate or not was_visible or Settings.reduced_motion or not is_inside_tree():
		glide.position = target
		return
	_glide_tween = create_tween()
	_glide_tween.tween_property(glide, "position", target, 0.2).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
