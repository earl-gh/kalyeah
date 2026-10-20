class_name MatchMenu
extends Control
## Reuses the same dialog, type scale and sound controls as the home and room menus.

signal closed
signal exit_pressed
signal hurt_pressed
signal status_pressed(type: int)

const STATUS_TESTS: Array[Array] = [["Stunned", StatusEffects.Type.STUN], ["Airborne", StatusEffects.Type.AIRBORNE], ["Knocked down", StatusEffects.Type.KNOCKOUT]]

var hurt_button: Button
var status_buttons: Dictionary[int, Button] = {}
var resume_button: Button
var leave_button: Button
var _dialog: MenuDialog
var tools_button: Button
var _details: VBoxContainer
var _tools: VBoxContainer
var _networked: bool = false
var _hosting: bool = false
var _confirmation: MenuDialog


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	_dialog = MenuDialog.create("Match settings")
	_dialog.persistent = true
	SettingsPage.fill(_dialog.body)
	_tools = VBoxContainer.new()
	_tools.add_theme_constant_override("separation", UiMetrics.GAP)
	_dialog.body.add_child(_tools)
	tools_button = _button("Show practice tools", "grey", _tools)
	tools_button.tooltip_text = "Show practice tools"
	_details = VBoxContainer.new()
	_details.add_theme_constant_override("separation", UiMetrics.GAP)
	_details.hide()
	_tools.add_child(_details)
	tools_button.pressed.connect(func() -> void:
		_details.visible = not _details.visible
		tools_button.text = "Hide practice tools" if _details.visible else "Show practice tools")
	hurt_button = _button("Take 30 damage", "grey", _details)
	hurt_button.pressed.connect(hurt_pressed.emit)
	for test: Array in STATUS_TESTS:
		var play: Button = _button(test[0] as String, "grey", _details)
		play.pressed.connect(status_pressed.emit.bind(test[1] as int))
		status_buttons[test[1] as int] = play
	resume_button = _button("Resume", "orange", _dialog.body)
	resume_button.pressed.connect(close)
	leave_button = _button("Leave match", "red", _dialog.body)
	leave_button.pressed.connect(request_exit)
	_dialog.open(self)
	_dialog.close(false)
	_dialog.closed.connect(close)
	visible = false


func set_practice(on: bool, hosting: bool = false) -> void:
	_networked = not on
	_hosting = hosting
	_tools.visible = on
	# Keep these flags truthful for callers, even while the entire group is hidden.
	hurt_button.visible = on
	for button: Button in status_buttons.values():
		button.visible = on


func open() -> void:
	visible = true
	_dialog.reopen()


func close() -> void:
	if not visible:
		return
	visible = false
	if is_instance_valid(_confirmation):
		_confirmation.close(false)
	_dialog.close(false)
	closed.emit()


func request_exit() -> void:
	if not _networked:
		exit_pressed.emit()
		return
	if is_instance_valid(_confirmation) and _confirmation.is_open:
		return
	_confirmation = DialogButtons.leave_match(_hosting, exit_pressed.emit)
	_confirmation.open(self)


func _button(text: String, paint: String, parent: Control) -> Button:
	var button: Button = MenuDialog.make_button(text, paint, UiMetrics.ACTION)
	parent.add_child(button)
	return button
