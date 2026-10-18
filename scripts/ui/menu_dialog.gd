class_name MenuDialog
extends Control
## A centred card over a dimmed screen (settings, rename, quit). Typography,
## spacing and focus states come from KalyeahTheme so every modal reads as one system.

## The draw order of every modal and its tint: above any component of the screen (the engine's
## highest is 4096). Use it for each full-screen overlay so a card, a button or a page never shows
## through or over it.
const MODAL_Z: int = 4000

signal closed
## Emitted once the dialog is in the tree and showing.
signal opened

const POP_SECONDS: float = 0.22
const DIM: Color = Color(0.01, 0.025, 0.06, 0.76)
const TITLE_COLOR: Color = KalyeahTheme.CREAM
const TITLE_SIZE: int = 44
const TEXT_SIZE: int = 28
const HINT_COLOR: Color = KalyeahTheme.TEXT_MUTED

var panel: PanelContainer
var body: VBoxContainer
var title_label: Label
var is_open: bool = true

var _width: float = 600.0


## A dialog with `title`, `width` px wide (in the 720 px design width).
static func create(title: String, width: float = 600.0) -> MenuDialog:
	var dialog: MenuDialog = MenuDialog.new()
	dialog._width = width
	dialog.name = "MenuDialog"
	# above everything on the screen, including the main screen's carousel cards (z_index up to 10)
	dialog.z_index = MODAL_Z
	dialog.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dialog.mouse_filter = Control.MOUSE_FILTER_STOP
	var dim: ColorRect = ColorRect.new()
	dim.color = DIM
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_STOP
	dialog.add_child(dim)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dialog.add_child(center)
	dialog.panel = PanelContainer.new()
	dialog.panel.custom_minimum_size = Vector2(width, 0.0)
	dialog.panel.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(36.0))
	center.add_child(dialog.panel)
	dialog.body = VBoxContainer.new()
	dialog.body.add_theme_constant_override("separation", 20)
	dialog.panel.add_child(dialog.body)
	dialog.title_label = dialog.add_label(title, TITLE_SIZE, TITLE_COLOR, HORIZONTAL_ALIGNMENT_LEFT)
	dialog.title_label.theme_type_variation = &"DisplayLabel"
	return dialog


## Shows the dialog over `parent` with a short pop.
func open(parent: Node) -> void:
	parent.add_child(self)
	is_open = true
	panel.pivot_offset = panel.size / 2.0
	panel.scale = Vector2.ONE * 0.88
	modulate.a = 0.0
	var tween: Tween = create_tween().set_parallel(true)
	tween.tween_property(self, "modulate:a", 1.0, POP_SECONDS)
	tween.tween_property(panel, "scale", Vector2.ONE, POP_SECONDS).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	# the card is centred once it has its size: pivot again next frame
	_pivot_later.call_deferred()
	opened.emit()


func _pivot_later() -> void:
	if is_instance_valid(panel):
		panel.pivot_offset = panel.size / 2.0


## Closes it (with a short fade unless `animate` is false) and tells the screen.
func close(animate: bool = true) -> void:
	if not is_open:
		return
	is_open = false
	closed.emit()
	if animate and is_inside_tree():
		var tween: Tween = create_tween().set_parallel(true)
		tween.tween_property(self, "modulate:a", 0.0, POP_SECONDS * 0.7)
		tween.tween_property(panel, "scale", Vector2.ONE * 0.94, POP_SECONDS * 0.7)
		tween.chain().tween_callback(queue_free)
	else:
		queue_free()


func add_label(text: String, size: int = TEXT_SIZE, color: Color = Color.WHITE, align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_CENTER) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = align
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_constant_override("outline_size", 0)
	body.add_child(label)
	return label


func add_hint(text: String) -> Label:
	var label: Label = add_label(text, 22, HINT_COLOR)
	label.theme_type_variation = &"MutedLabel"
	label.add_theme_constant_override("outline_size", 0)
	return label


## A row of equally wide buttons at the bottom of the card.
func add_buttons(buttons: Array[Button]) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	for button: Button in buttons:
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(button)
	body.add_child(row)
	return row


## A painted button of `color` (orange, green, red, blue, grey or gold) with press feedback.
static func make_button(text: String, color: String, font_size: int = 32, min_size: Vector2 = Vector2(0.0, 88.0)) -> Button:
	var button: Button = Button.new()
	button.text = text
	button.custom_minimum_size = min_size
	button.add_theme_font_size_override("font_size", font_size)
	KalyeahTheme.style_button(button, color)
	feel_press(button)
	return button


## The button sinks a little while it is held (a quick scale), like a real key.
static func feel_press(button: Button) -> void:
	button.button_down.connect(func() -> void: _squish(button, 0.955))
	button.button_up.connect(func() -> void: _squish(button, 1.0))


static func _squish(button: Button, target: float) -> void:
	if not button.is_inside_tree():
		return
	button.pivot_offset = button.size / 2.0
	var tween: Tween = button.create_tween()
	tween.tween_property(button, "scale", Vector2.ONE * target, 0.07).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
