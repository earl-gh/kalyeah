class_name CardDrawer
extends Control
## Modal card details cover the navigation. Kids protrude behind the sheet; details are only name and description.
## Okay, the top grabber, the scrim or Android Back closes the drawer.

signal opened
signal closed
## The card shown changed (`index` into the list given to `open_entries`), at opening and at every step.
signal shown(index: int)

const SLIDE_SECONDS: float = 0.25
const MAX_HEIGHT_SHARE: float = 0.7
const SCRIM_COLOR: Color = Color(0.01, 0.025, 0.06, 0.62)
## The handle: a pill on a strip as wide as the sheet (a swipe or a tap anywhere on the strip is the handle's).
const HANDLE_STRIP: float = 56.0
const HANDLE_PILL: Vector2 = Vector2(96.0, 10.0)
## How far a swipe down must go to close, and how far a swipe along the picture must go to step.
const CLOSE_SWIPE: float = 140.0
const STEP_SWIPE: float = 90.0
## A press that moves less than this is a tap.
const TAP_SLOP: float = 16.0
const PICTURE_HEIGHT: float = 240.0
const ARROW_SIZE: Vector2 = Vector2(UiMetrics.MIN_TARGET, UiMetrics.MIN_TARGET + UiMetrics.PADDING)
## Space under Okay, plus the device's home gesture inset.
const BOTTOM_CLEAR: float = UiMetrics.PADDING
const CARD_OVERLAP: float = 44.0
const TOP_RADIUS: int = UiSkin.RADIUS_XL
## The bottom corners are square at the phone edge.
const FLAT: int = 0

var bottom_inset: float = 0.0
var _kid: bool = false
var is_open: bool = false
var entries: Array[Dictionary] = []
var index: int = -1

var scrim: ColorRect
var sheet: PanelContainer
var slider: Control
var handle: Control
var header_row: HBoxContainer
var title_label: Label
var close_button: Button
var previous_button: Button
var next_button: Button
var picture: TextureRect
var picture_row: HBoxContainer
var scroll: ScrollContainer
var content: VBoxContainer
var sections: Array[Control] = []

var _tween: Tween
var _frame_box: VBoxContainer
var _drag_from: Vector2 = Vector2.ZERO
var _dragging: bool = false
var _swiped: bool = false


func _ready() -> void:
	name = "CardDrawer"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	scrim = ColorRect.new()
	scrim.name = "Scrim"
	scrim.color = SCRIM_COLOR
	scrim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scrim.mouse_filter = Control.MOUSE_FILTER_STOP
	scrim.gui_input.connect(_on_scrim_input)
	add_child(scrim)
	slider = Control.new()
	slider.name = "Slider"
	slider.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	slider.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(slider)
	sheet = PanelContainer.new()
	sheet.name = "Sheet"
	sheet.z_index = 1
	sheet.anchor_left = 0.0
	sheet.anchor_right = 1.0
	sheet.anchor_top = 1.0
	sheet.anchor_bottom = 1.0
	sheet.grow_vertical = Control.GROW_DIRECTION_BEGIN
	sheet.mouse_filter = Control.MOUSE_FILTER_STOP
	sheet.add_theme_stylebox_override("panel", _frame_style())
	slider.add_child(sheet)
	_frame_box = VBoxContainer.new()
	_frame_box.name = "FrameBox"
	_frame_box.add_theme_constant_override("separation", int(MenuDialog.HEADER_GAP))
	sheet.add_child(_frame_box)
	_build_handle()
	_build_header()
	_build_body()
	sheet.minimum_size_changed.connect(_fit.call_deferred)
	resized.connect(_fit.call_deferred)
	sheet.resized.connect(_place_picture.call_deferred)


## The modals' frame (same colour, border and shadow, from `KalyeahTheme.modal_frame`) with the top corners rounded and the bottom square.
func _frame_style() -> StyleBoxFlat:
	var box: StyleBoxFlat = KalyeahTheme.modal_frame(MenuDialog.FRAME_RIM)
	box.set_corner_radius(CORNER_BOTTOM_LEFT, FLAT)
	box.set_corner_radius(CORNER_BOTTOM_RIGHT, FLAT)
	box.set_corner_radius(CORNER_TOP_LEFT, TOP_RADIUS)
	box.set_corner_radius(CORNER_TOP_RIGHT, TOP_RADIUS)
	box.set_border_width(SIDE_BOTTOM, 0)
	box.content_margin_bottom = BOTTOM_CLEAR + bottom_inset
	return box


func _build_handle() -> void:
	handle = Control.new()
	handle.name = "Handle"
	handle.custom_minimum_size = Vector2(0.0, HANDLE_STRIP)
	handle.mouse_filter = Control.MOUSE_FILTER_STOP
	handle.accessibility_name = "Close"
	handle.tooltip_text = "Close"
	handle.gui_input.connect(_on_handle_input)
	var pill: Panel = Panel.new()
	pill.name = "Pill"
	pill.custom_minimum_size = HANDLE_PILL
	pill.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	pill.offset_left = -HANDLE_PILL.x / 2.0
	pill.offset_right = HANDLE_PILL.x / 2.0
	pill.offset_top = -HANDLE_PILL.y / 2.0
	pill.offset_bottom = HANDLE_PILL.y / 2.0
	pill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var look: StyleBoxFlat = StyleBoxFlat.new()
	look.bg_color = KalyeahTheme.MODAL_BODY
	look.set_corner_radius_all(UiSkin.round_radius(HANDLE_PILL.y))
	look.anti_aliasing = true
	pill.add_theme_stylebox_override("panel", look)
	handle.add_child(pill)
	_frame_box.add_child(handle)


func _build_header() -> void:
	header_row = HBoxContainer.new()
	header_row.name = "HeaderRow"
	header_row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	header_row.mouse_filter = Control.MOUSE_FILTER_STOP
	header_row.gui_input.connect(_on_header_input)
	var inset: MarginContainer = MarginContainer.new()
	inset.name = "Header"
	for side: String in ["left", "right"]:
		inset.add_theme_constant_override("margin_" + side, MenuDialog.HEADER_INSET)
	inset.add_child(header_row)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.theme_type_variation = &"DisplayLabel"
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	title_label.add_theme_font_size_override("font_size", MenuDialog.TITLE_SIZE)
	title_label.add_theme_color_override("font_color", MenuDialog.TITLE_COLOR)
	title_label.add_theme_color_override("font_outline_color", KalyeahTheme.MODAL_TITLE_OUTLINE)
	title_label.add_theme_constant_override("outline_size", MenuDialog.TITLE_OUTLINE)
	header_row.add_child(title_label)
	_frame_box.add_child(inset)


func _build_body() -> void:
	var inner: PanelContainer = PanelContainer.new()
	inner.name = "Inner"
	inner.size_flags_vertical = Control.SIZE_EXPAND_FILL
	inner.add_theme_stylebox_override("panel", KalyeahTheme.modal_body(UiMetrics.PADDING))
	inner.theme = KalyeahTheme.modal_body_theme()
	_frame_box.add_child(inner)
	scroll = ScrollContainer.new()
	scroll.name = "DrawerScroll"
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	inner.add_child(scroll)
	content = VBoxContainer.new()
	content.name = "Content"
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", MenuDialog.BODY_GAP)
	scroll.add_child(content)
	picture_row = HBoxContainer.new()
	picture_row.name = "PictureRow"
	picture_row.alignment = BoxContainer.ALIGNMENT_CENTER
	picture_row.add_theme_constant_override("separation", UiMetrics.GAP)
	# a drag along the picture steps; a drag up or down still scrolls the details (the row passes it on)
	picture_row.mouse_filter = Control.MOUSE_FILTER_PASS
	picture_row.gui_input.connect(_on_picture_input)
	previous_button = _arrow("<", "Previous card")
	previous_button.pressed.connect(step.bind(-1))
	picture_row.add_child(previous_button)
	picture = TextureRect.new()
	picture.name = "Picture"
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	picture_row.add_child(picture)
	next_button = _arrow(">", "Next card")
	next_button.pressed.connect(step.bind(1))
	picture_row.add_child(next_button)
	content.add_child(picture_row)
	var actions: CenterContainer = CenterContainer.new()
	actions.name = "Actions"
	close_button = MenuDialog.make_button("Okay", "gold", UiMetrics.BODY, Vector2(240.0, UiMetrics.TOUCH))
	close_button.name = "OkayButton"
	close_button.pressed.connect(close)
	actions.add_child(close_button)
	_frame_box.add_child(actions)


func _arrow(symbol: String, label: String) -> Button:
	var button: Button = MenuDialog.make_button(symbol, "grey", UiMetrics.ACTION, ARROW_SIZE)
	button.accessibility_name = label
	button.focus_mode = Control.FOCUS_NONE
	button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	return button


# ---- opening, stepping, closing ---------------------------------------------------------

## Shows the drawer on card `at` of `list` (see `CardsScreen.entries`): it slides up from the bottom.
func open_entries(list: Array[Dictionary], at: int) -> void:
	entries = list
	var was_open: bool = is_open
	is_open = true
	visible = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	_show(at)
	_fit()
	if is_instance_valid(_tween):
		_tween.kill()
	if was_open:
		return
	if Settings.reduced_motion:
		slider.position = Vector2.ZERO
		scrim.modulate.a = 1.0
	else:
		slider.position = Vector2(0.0, size.y)
		scrim.modulate.a = 0.0
		_tween = create_tween().set_parallel(true)
		_tween.tween_property(slider, "position:y", 0.0, SLIDE_SECONDS).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		_tween.tween_property(scrim, "modulate:a", 1.0, SLIDE_SECONDS)
	opened.emit()


## The card before (-1) or after (1) the one shown; the ends wrap round.
func step(by: int) -> void:
	if not is_open or entries.is_empty():
		return
	_show(posmod(index + by, entries.size()))
	_fit.call_deferred()


func _show(at: int) -> void:
	index = clampi(at, 0, entries.size() - 1)
	var card: Dictionary = entries[index]
	title_label.text = String(card["name"])
	_kid = card.get("kind", &"") == &"kid"
	picture_row.visible = not _kid
	if _kid and picture.get_parent() != slider:
		picture.reparent(slider)
		slider.move_child(picture, 0)
	elif not _kid and picture.get_parent() != picture_row:
		picture.reparent(picture_row)
		picture_row.move_child(picture, 1)
	var art: Texture2D = card["picture"] as Texture2D
	picture.texture = art
	var aspect: float = float(art.get_width()) / float(art.get_height()) if art != null and art.get_height() > 0 else 0.7
	picture.custom_minimum_size = Vector2(PICTURE_HEIGHT * aspect, PICTURE_HEIGHT)
	for old: Control in sections:
		content.remove_child(old)
		old.queue_free()
	sections.clear()
	for part: Control in (card["content"] as Callable).call() as Array[Control]:
		content.add_child(part)
		sections.append(part)
	scroll.scroll_vertical = 0
	_place_picture.call_deferred()
	shown.emit(index)


## Closes it: it slides down (at once with Reduce motion). The screen under it hears `closed` straight away.
func close(animate: bool = true) -> void:
	if not is_open:
		return
	is_open = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	if is_instance_valid(_tween):
		_tween.kill()
	closed.emit()
	if not animate or Settings.reduced_motion or not is_inside_tree():
		_hide_now()
		return
	_tween = create_tween().set_parallel(true)
	_tween.tween_property(slider, "position:y", size.y, SLIDE_SECONDS).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	_tween.tween_property(scrim, "modulate:a", 0.0, SLIDE_SECONDS)
	_tween.chain().tween_callback(_hide_now)


func _hide_now() -> void:
	if is_open:
		return
	visible = false
	slider.position = Vector2.ZERO


## The sheet is as tall as its details need but never taller than `MAX_HEIGHT_SHARE` of the drawer; what does not fit scrolls.
func _fit() -> void:
	if not is_inside_tree() or scroll == null:
		return
	scroll.custom_minimum_size.y = 0.0
	var chrome: float = sheet.get_combined_minimum_size().y
	var wanted: float = content.get_combined_minimum_size().y
	scroll.custom_minimum_size.y = clampf(wanted, 0.0, maxf(0.0, size.y * MAX_HEIGHT_SHARE - chrome))
	_place_picture.call_deferred()


func set_bottom_inset(inset: float) -> void:
	bottom_inset = inset
	if sheet != null:
		(sheet.get_theme_stylebox("panel") as StyleBoxFlat).content_margin_bottom = BOTTOM_CLEAR + inset


func _place_picture() -> void:
	if _kid and is_open:
		picture.size = picture.custom_minimum_size
		picture.position = Vector2((size.x - picture.size.x) / 2.0, sheet.position.y - picture.size.y + CARD_OVERLAP)


# ---- touch -------------------------------------------------------------------------------

func _on_scrim_input(event: InputEvent) -> void:
	if _is_press(event):
		close()


func _is_press(event: InputEvent) -> bool:
	return event is InputEventMouseButton and (event as InputEventMouseButton).pressed and (event as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT


func _is_release(event: InputEvent) -> bool:
	return event is InputEventMouseButton and not (event as InputEventMouseButton).pressed and (event as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT


## The handle strip: a tap closes; a swipe down drags the sheet along and closes past `CLOSE_SWIPE`, else it springs back.
func _on_handle_input(event: InputEvent) -> void:
	if _drag_down(event, true):
		handle.accept_event()


func _on_header_input(event: InputEvent) -> void:
	if _drag_down(event, false):
		header_row.accept_event()


## Handles a press, drag and release on the handle or header; true when the event was part of it. `tap_closes`: a press that barely moves closes.
func _drag_down(event: InputEvent, tap_closes: bool) -> bool:
	if _is_press(event):
		_dragging = true
		_drag_from = (event as InputEventMouseButton).global_position
		return true
	if _dragging and event is InputEventMouseMotion:
		var dy: float = maxf(0.0, (event as InputEventMouseMotion).global_position.y - _drag_from.y)
		if not Settings.reduced_motion:
			slider.position.y = dy
		return true
	if _dragging and _is_release(event):
		_dragging = false
		var moved: Vector2 = (event as InputEventMouseButton).global_position - _drag_from
		if moved.y >= CLOSE_SWIPE:
			close()
		elif tap_closes and moved.length() < TAP_SLOP:
			close()
		else:
			_spring_back()
		return true
	return false


func _spring_back() -> void:
	if is_instance_valid(_tween):
		_tween.kill()
	if Settings.reduced_motion:
		slider.position = Vector2.ZERO
		return
	_tween = create_tween()
	_tween.tween_property(slider, "position:y", 0.0, SLIDE_SECONDS * 0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


## A swipe along the picture steps to the neighbouring card (left: the next one).
func _on_picture_input(event: InputEvent) -> void:
	if _is_press(event):
		_drag_from = (event as InputEventMouseButton).global_position
		_swiped = false
	elif event is InputEventMouseMotion and not _swiped and (event as InputEventMouseMotion).button_mask & MOUSE_BUTTON_MASK_LEFT != 0:
		var dx: float = (event as InputEventMouseMotion).global_position.x - _drag_from.x
		var dy: float = (event as InputEventMouseMotion).global_position.y - _drag_from.y
		if absf(dx) >= STEP_SWIPE and absf(dx) > absf(dy) * 1.5:
			_swiped = true
			step(-1 if dx > 0.0 else 1)
