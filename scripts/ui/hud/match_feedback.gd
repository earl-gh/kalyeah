class_name MatchFeedback
extends Control
## Persistent transport state and contextual control guidance; never changes sim state.

signal return_home

var connection: VBoxContainer
var message: Label
var leave: Button
var hint: Label


## Keeps the hint line just above the console, whatever its height.
func set_console_height(height: float) -> void:
	hint.offset_top = -(height + 130.0)
	hint.offset_bottom = -(height + 10.0)


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	connection = VBoxContainer.new()
	connection.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	connection.offset_left = -270.0
	connection.offset_right = 270.0
	connection.offset_top = -100.0
	connection.add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(connection)
	message = Label.new()
	message.theme_type_variation = &"HudPill"
	message.add_theme_font_size_override("font_size", UiMetrics.BODY)
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	connection.add_child(message)
	leave = Button.new()
	leave.text = "Return home"
	leave.custom_minimum_size.y = 88.0
	KalyeahTheme.style_button(leave, "orange")
	leave.pressed.connect(return_home.emit)
	connection.add_child(leave)
	connection.hide()
	hint = Label.new()
	hint.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	set_console_height(MatchHud.CONSOLE_HEIGHT)
	hint.offset_left = 20.0
	hint.offset_right = -20.0
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	hint.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	hint.add_theme_color_override("font_outline_color", Color(0.03, 0.04, 0.08))
	hint.add_theme_constant_override("outline_size", 8)
	hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(hint)


func show_connection(text: String, terminal: bool) -> void:
	connection.visible = not text.is_empty()
	message.text = text
	leave.visible = terminal
	hint.hide()


func sync_hint(player: PlayerState, buttons: Array[AimButton], playing: bool) -> void:
	hint.text = ""
	if not playing or connection.visible:
		hint.hide()
		return
	if not player.effects.can_cast():
		hint.text = "%s · weapons unavailable" % ", ".join(player.effects.display_names())
	else:
		for button: AimButton in buttons:
			if button.aiming:
				hint.text = "Release to cancel" if button.over_cancel else "Release to cast · drag to cancel"
				break
	hint.visible = not hint.text.is_empty()
