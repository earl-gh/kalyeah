class_name HotspotCodeInput
extends HBoxContainer
## The room code as one box per character (8): type and the cursor moves on, backspace on an empty
## box goes back, a pasted code fills every box. Letters are made capitals; only letters and digits
## are taken.

signal changed(complete: bool)

const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")

var fields: Array[LineEdit] = []


func _init() -> void:
	name = "CodeInput"
	alignment = BoxContainer.ALIGNMENT_CENTER
	add_theme_constant_override("separation", 6)
	for i: int in NET_RULES.passcode_length:
		var field: LineEdit = LineEdit.new()
		field.name = "Char%d" % i
		field.max_length = 1
		field.alignment = HORIZONTAL_ALIGNMENT_CENTER
		field.custom_minimum_size = Vector2(0.0, 84.0)
		field.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		# a LineEdit is four characters wide by default: one is all this box holds
		field.add_theme_constant_override("minimum_character_width", 1)
		field.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
		field.add_theme_font_size_override("font_size", 40)
		field.virtual_keyboard_type = LineEdit.KEYBOARD_TYPE_DEFAULT
		field.text_changed.connect(_on_text_changed.bind(i))
		field.gui_input.connect(_on_gui_input.bind(i))
		add_child(field)
		fields.append(field)


func code() -> String:
	var text: String = ""
	for field: LineEdit in fields:
		text += field.text
	return text


func is_complete() -> bool:
	return code().length() == fields.size()


## Fills the boxes from `text` (anything that is not a letter or digit is skipped).
func set_code(text: String) -> void:
	var clean: String = clean_text(text)
	for i: int in fields.size():
		fields[i].text = clean[i] if i < clean.length() else ""
	changed.emit(is_complete())


static func clean_text(text: String) -> String:
	var result: String = ""
	for character: String in text.to_upper():
		if (character >= "A" and character <= "Z") or (character >= "0" and character <= "9"):
			result += character
	return result


func focus_first() -> void:
	fields[0].grab_focus()


func _on_text_changed(text: String, index: int) -> void:
	var clean: String = clean_text(text)
	if clean != text:
		fields[index].text = clean
		fields[index].caret_column = clean.length()
	if not clean.is_empty() and index < fields.size() - 1:
		fields[index + 1].grab_focus()
	changed.emit(is_complete())


func _on_gui_input(event: InputEvent, index: int) -> void:
	var key: InputEventKey = event as InputEventKey
	if key == null or not key.pressed:
		return
	if key.keycode == KEY_BACKSPACE and fields[index].text.is_empty() and index > 0:
		fields[index - 1].text = ""
		fields[index - 1].grab_focus()
		changed.emit(false)
		accept_event()
	elif key.keycode == KEY_V and (key.ctrl_pressed or key.meta_pressed):
		set_code(DisplayServer.clipboard_get())
		fields[mini(code().length(), fields.size() - 1)].grab_focus()
		accept_event()
