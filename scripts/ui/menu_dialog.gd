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

## The widths every modal uses (design pixels): a short question, or a sheet with some content. Nothing in between.
const WIDTH_SMALL: float = 560.0
const WIDTH_SHEET: float = 620.0
## A modal is never taller than this share of the screen (owner); what does not fit scrolls.
const MAX_HEIGHT_SHARE: float = 0.75
## The header icon's size (a picture left of the title), and the gap between the body's rows.
const ICON_SIZE: float = 64.0
const BODY_GAP: int = 14

const POP_SECONDS: float = 0.22
const DIM: Color = Color(0.01, 0.025, 0.06, 0.76)
const TITLE_COLOR: Color = KalyeahTheme.MODAL_TITLE
## The slate frame is this wide round the body, and the header sits in it above the body.
const FRAME_RIM: float = 14.0
const HEADER_GAP: float = 6.0
const HEADER_INSET: int = 10
const TITLE_OUTLINE: int = 8
const TITLE_SIZE: int = UiMetrics.TITLE
const TEXT_SIZE: int = UiMetrics.BODY
const HINT_COLOR: Color = KalyeahTheme.MODAL_TEXT_MUTED
const TEXT_COLOR: Color = KalyeahTheme.MODAL_TEXT
const DISMISS_ACTIONS: Array[String] = ["Cancel", "Close", "Got it!", "Got it", "Done", "Okay", "Resume"]

var panel: PanelContainer
var body: VBoxContainer
var inner: PanelContainer
var close_button: Button
var title_label: Label
var is_open: bool = true
## HUD menus retain wired controls across repeated opens. Ordinary dialogs free on close.
var persistent: bool = false

var _width: float = WIDTH_SMALL
var _header: MarginContainer
var _safe: MarginContainer
var _animation: Tween
var _previous_focus: WeakRef


## A dialog with `title`, `width` px wide (in the 720 px design width: `WIDTH_SMALL` or `WIDTH_SHEET`) and an optional `icon` before the title.
static func create(title: String, width: float = WIDTH_SMALL, icon: Texture2D = null) -> MenuDialog:
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
	dialog._safe = MarginContainer.new()
	dialog._safe.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dialog.add_child(dialog._safe)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dialog._safe.add_child(center)
	dialog.panel = PanelContainer.new()
	dialog.panel.custom_minimum_size = Vector2(width, 0.0)
	dialog.panel.add_theme_stylebox_override("panel", KalyeahTheme.modal_frame(FRAME_RIM))
	center.add_child(dialog.panel)
	var frame_box: VBoxContainer = VBoxContainer.new()
	frame_box.name = "FrameBox"
	frame_box.add_theme_constant_override("separation", int(HEADER_GAP))
	dialog.panel.add_child(frame_box)
	# the header lives on the light gray frame, above the dark body
	dialog.title_label = Label.new()
	dialog.title_label.text = title
	dialog.title_label.name = "Title"
	dialog.title_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	dialog.title_label.theme_type_variation = &"DisplayLabel"
	dialog.title_label.add_theme_font_size_override("font_size", TITLE_SIZE)
	dialog.title_label.add_theme_color_override("font_color", TITLE_COLOR)
	dialog.title_label.add_theme_color_override("font_outline_color", KalyeahTheme.MODAL_TITLE_OUTLINE)
	dialog.title_label.add_theme_constant_override("outline_size", TITLE_OUTLINE)
	dialog.title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var header: MarginContainer = MarginContainer.new()
	header.name = "Header"
	for side: String in ["left", "right"]:
		header.add_theme_constant_override("margin_" + side, HEADER_INSET)
	header.add_theme_constant_override("margin_top", 2)
	header.add_theme_constant_override("margin_bottom", 4)
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "HeaderRow"
	row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	dialog.title_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if icon != null:
		var picture: TextureRect = TextureRect.new()
		picture.name = "Icon"
		picture.texture = icon
		picture.custom_minimum_size = Vector2.ONE * ICON_SIZE
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		picture.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(picture)
	row.add_child(dialog.title_label)
	# a red close button at the top right; a tap outside the modal closes it too
	dialog.close_button = make_button("X", "red", UiMetrics.ACTION, Vector2.ONE * UiMetrics.MIN_TARGET)
	dialog.close_button.name = "CloseButton"
	dialog.close_button.accessibility_name = "Close"
	dialog.close_button.focus_mode = Control.FOCUS_NONE
	dialog.close_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	dialog.close_button.pressed.connect(dialog.close)
	row.add_child(dialog.close_button)
	header.add_child(row)
	frame_box.add_child(header)
	dim.gui_input.connect(dialog._on_dim_input)
	dialog._header = header
	dialog.inner = PanelContainer.new()
	dialog.inner.name = "Inner"
	dialog.inner.size_flags_vertical = Control.SIZE_EXPAND_FILL
	dialog.inner.add_theme_stylebox_override("panel", KalyeahTheme.modal_body(UiMetrics.PADDING))
	dialog.inner.theme = KalyeahTheme.modal_body_theme()
	frame_box.add_child(dialog.inner)
	dialog.body = VBoxContainer.new()
	dialog.body.add_theme_constant_override("separation", BODY_GAP)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "DialogScroll"
	scroll.follow_focus = true
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	dialog.inner.add_child(scroll)
	dialog.body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(dialog.body)
	dialog.body.minimum_size_changed.connect(dialog._fit.call_deferred)
	dialog.resized.connect(dialog._fit.call_deferred)
	return dialog


## Shows the dialog over `parent` with a short pop.
func open(parent: Node) -> void:
	parent.add_child(self)
	_fit()
	is_open = true
	_previous_focus = weakref(get_viewport().gui_get_focus_owner()) if get_viewport().gui_get_focus_owner() != null else null
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	if Settings.reduced_motion:
		panel.scale = Vector2.ONE
		modulate.a = 1.0
	else:
		panel.pivot_offset = panel.size / 2.0
		panel.scale = Vector2.ONE * 0.88
		modulate.a = 0.0
		_animation = create_tween().set_parallel(true)
		_animation.tween_property(self, "modulate:a", 1.0, POP_SECONDS)
		_animation.tween_property(panel, "scale", Vector2.ONE, POP_SECONDS).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_pivot_later.call_deferred()
	_focus_first.call_deferred()
	opened.emit()


func _pivot_later() -> void:
	if is_instance_valid(panel):
		panel.pivot_offset = panel.size / 2.0


## Closes it (with a short fade unless `animate` is false) and tells the screen.
func close(animate: bool = true) -> void:
	if not is_open:
		return
	is_open = false
	if is_instance_valid(_animation):
		_animation.kill()
	UiFocus.restore.call_deferred(_previous_focus)
	closed.emit()
	if persistent:
		hide()
		return
	if animate and not Settings.reduced_motion and is_inside_tree():
		_animation = create_tween().set_parallel(true)
		_animation.tween_property(self, "modulate:a", 0.0, POP_SECONDS * 0.7)
		_animation.tween_property(panel, "scale", Vector2.ONE * 0.94, POP_SECONDS * 0.7)
		_animation.chain().tween_callback(queue_free)
	else:
		queue_free()


func add_label(text: String, size: int = TEXT_SIZE, color: Color = TEXT_COLOR, align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_LEFT) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = align
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_constant_override("outline_size", 0)
	body.add_child(label)
	return label


func add_hint(text: String) -> Label:
	var label: Label = add_label(text, UiMetrics.DETAIL, HINT_COLOR)
	label.theme_type_variation = &"MutedLabel"
	label.add_theme_constant_override("outline_size", 0)
	return label


## A row of equally wide buttons at the bottom of the card.
func add_buttons(buttons: Array[Button]) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	for button: Button in buttons:
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(button)
	body.add_child(row)
	return row


## A painted button of `color` (orange, green, red, blue, grey or gold); the theme gives it the shared press feel.
static func make_button(text: String, color: String, font_size: int = UiMetrics.ACTION, min_size: Vector2 = Vector2(0.0, 88.0)) -> Button:
	var button: Button = Button.new()
	button.text = text
	button.custom_minimum_size = min_size
	button.add_theme_font_size_override("font_size", font_size)
	KalyeahTheme.style_button(button, color)
	return button


## A tap on the dimmed screen outside the card closes the dialog (the emulated left click of a touch).
func _on_dim_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and (event as InputEventMouseButton).pressed and (event as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT:
		close()


## The phone's back button closes the open dialog. Only a dialog that is open when the press arrives closes, and after the
## notification has walked the whole tree, so the screen under it still sees it open and leaves the press to it.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST and is_open and is_visible_in_tree():
		close.call_deferred()


func _fit() -> void:
	if not is_inside_tree() or is_queued_for_deletion():
		return
	# A footer Cancel/Close already provides the dismiss action.
	var footer_closes: bool = false
	for node: Node in body.find_children("*", "Button", true, false):
		var button: Button = node as Button
		if button.is_visible_in_tree() and button.text in DISMISS_ACTIONS:
			footer_closes = true
			break
	close_button.visible = not footer_closes
	LobbyWidgets.apply_safe_area(_safe, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN)
	var available: Vector2 = size - Vector2(
		_safe.get_theme_constant("margin_left") + _safe.get_theme_constant("margin_right"),
		_safe.get_theme_constant("margin_top") + _safe.get_theme_constant("margin_bottom"))
	var header_height: float = _header.get_combined_minimum_size().y + HEADER_GAP if _header.visible else 0.0
	var wanted: float = body.get_combined_minimum_size().y + UiMetrics.PADDING * 2.0 + header_height + FRAME_RIM * 2.0
	# never taller than 75 % of the screen (owner): what does not fit scrolls inside the body
	var tallest: float = minf(available.y, size.y * MAX_HEIGHT_SHARE)
	panel.custom_minimum_size = Vector2(minf(_width, available.x), minf(wanted, tallest))


func reopen() -> void:
	_previous_focus = weakref(get_viewport().gui_get_focus_owner()) if get_viewport().gui_get_focus_owner() != null else null
	is_open = true
	show()
	panel.scale = Vector2.ONE
	modulate.a = 1.0
	_fit()
	_focus_first.call_deferred()


func _focus_first() -> void:
	if is_open and is_visible_in_tree():
		UiFocus.first(self)


func _on_accessibility_changed() -> void:
	_fit.call_deferred()
	if not Settings.reduced_motion:
		return
	if is_instance_valid(_animation):
		_animation.kill()
	panel.scale = Vector2.ONE
	modulate.a = 1.0
	if not is_open and not persistent:
		queue_free()
