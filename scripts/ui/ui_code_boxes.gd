class_name UiCodeBoxes
extends Control
## A room code as eight boxes (the length comes from the rules). Editable: a real LineEdit sits invisibly over the
## boxes, so the phone keyboard, paste and a tap anywhere all work, and the boxes show what it holds with the next
## box lit in gold. Read-only: the same boxes just show a code (the room's header). Letters are made capitals and
## anything outside the code alphabet is dropped, so "abc 234-xy" becomes ABC234XY.

signal changed(text: String)
signal completed(text: String)

const BOX_HEIGHT: float = 104.0
const BOX_FILL: Color = Color("060d24")
const BOX_LINE: Color = Color("2b4072")
const BOX_FILLED: Color = Color("6f8cc9")
const NEXT_LINE: Color = Color("ffc233")
const ERROR_LINE: Color = Color("ff7a70")

## The real input (editable boxes only). Screens that already knew a LineEdit keep using this.
var edit: LineEdit
var length: int = 8
var alphabet: String = ""
var editable: bool = true
var has_error: bool = false

var _boxes: Array[PanelContainer] = []
var _letters: Array[Label] = []
var _shown: String = "\u0001"
var _pending: String = ""


func _init(p_length: int = 8, p_alphabet: String = "", p_editable: bool = true) -> void:
	length = p_length
	alphabet = p_alphabet
	editable = p_editable
	name = "CodeBoxes"
	custom_minimum_size = Vector2(0.0, BOX_HEIGHT)
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Row"
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	row.add_theme_constant_override("separation", 8)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(row)
	for i: int in length:
		var box: PanelContainer = PanelContainer.new()
		box.name = "Box%d" % i
		box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		box.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var letter: Label = Label.new()
		letter.name = "Letter"
		letter.theme_type_variation = &"DisplayLabel"
		letter.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		letter.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		letter.add_theme_font_size_override("font_size", 52)
		letter.add_theme_color_override("font_color", KalyeahTheme.CREAM)
		letter.add_theme_constant_override("outline_size", 6)
		letter.mouse_filter = Control.MOUSE_FILTER_IGNORE
		box.add_child(letter)
		row.add_child(box)
		_boxes.append(box)
		_letters.append(letter)
	if editable:
		_build_input()
	_paint()


func _build_input() -> void:
	edit = LineEdit.new()
	edit.name = "Input"
	edit.accessibility_name = "Room code"
	edit.accessibility_description = "Eight letters and numbers"
	edit.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	edit.max_length = length
	edit.caret_blink = false
	edit.context_menu_enabled = true
	edit.virtual_keyboard_type = LineEdit.KEYBOARD_TYPE_DEFAULT
	edit.keep_editing_on_text_submit = false
	# invisible but alive: it takes the focus, the keyboard and the paste
	edit.modulate.a = 0.0
	for state: String in ["normal", "focus", "read_only"]:
		edit.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	edit.text_changed.connect(_on_typed)
	edit.focus_entered.connect(_paint)
	edit.focus_exited.connect(_paint)
	# a tap on the boxes always lands after the last letter: typing then appends instead of inserting into the middle
	edit.gui_input.connect(func(event: InputEvent) -> void:
		var pressed: bool = (event is InputEventMouseButton and (event as InputEventMouseButton).pressed) \
			or (event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed)
		if pressed:
			_caret_to_end.call_deferred())
	add_child(edit)


func _caret_to_end() -> void:
	if edit != null:
		edit.deselect()
		edit.caret_column = edit.text.length()


func _process(_delta: float) -> void:
	# a text set from code (a test, Paste) does not emit text_changed
	if editable and edit != null and edit.text != _shown:
		_paint()


## The capitals of `raw` that are in the alphabet, at most `length` of them.
func clean(raw: String) -> String:
	var kept: String = ""
	for character: String in raw.to_upper():
		if alphabet.is_empty() or alphabet.contains(character):
			kept += character
	return kept.left(length)


## What the boxes hold.
func get_text() -> String:
	return edit.text if editable and edit != null else _pending


## Puts `raw` (cleaned) in the boxes.
func set_text(raw: String) -> void:
	var value: String = clean(raw)
	if editable and edit != null:
		edit.text = value
		edit.caret_column = value.length()
	else:
		_pending = value
	_paint()



## Flags the code as wrong (red boxes) until it is edited.
func set_error(on: bool) -> void:
	has_error = on
	_paint()


func focus_input() -> void:
	if edit != null:
		edit.grab_focus()


func _on_typed(raw: String) -> void:
	var value: String = clean(raw)
	if value != raw:
		edit.text = value
	edit.caret_column = value.length()
	if has_error:
		has_error = false
	_paint()
	changed.emit(value)
	if value.length() == length:
		completed.emit(value)


func _paint() -> void:
	var value: String = edit.text if editable and edit != null else _pending
	_shown = value
	var next: int = mini(value.length(), length - 1)
	var focused: bool = editable and edit != null and edit.has_focus()
	for i: int in length:
		var filled: bool = i < value.length()
		_letters[i].text = value[i] if filled else ""
		var line: Color = BOX_FILLED if filled else BOX_LINE
		var width: int = 2
		if has_error:
			line = ERROR_LINE
			width = 3
		elif focused and i == next and value.length() < length:
			line = NEXT_LINE
			width = 4
		var box: StyleBoxFlat = StyleBoxFlat.new()
		box.bg_color = BOX_FILL
		box.border_color = line
		box.set_border_width_all(width)
		box.set_corner_radius_all(14)
		box.anti_aliasing = true
		_boxes[i].add_theme_stylebox_override("panel", box)
