class_name MatchMenu
extends Control
## Reuses the same dialog, type scale and setting controls as the home and room menus.

signal closed
signal exit_pressed

var resume_button: Button
var leave_button: Button
var _dialog: MenuDialog
var _networked: bool = false
var _hosting: bool = false
## Leaving asks first (a Practice match against bots: it counts as a loss). Training leaves at once.
var _confirm_practice: bool = false
var _confirmation: MenuDialog


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	_dialog = MenuDialog.create("Match settings", MenuDialog.WIDTH_SHEET, UiTabBar.picture(&"settings"))
	_dialog.persistent = true
	SettingsPage.fill(_dialog.body, true)
	resume_button = _button("Resume", "orange", _dialog.body)
	resume_button.pressed.connect(close)
	leave_button = _button("Leave battle", "red", _dialog.body)
	leave_button.pressed.connect(request_exit)
	_dialog.open(self)
	_dialog.close(false)
	_dialog.closed.connect(close)
	visible = false


func set_practice(on: bool, hosting: bool = false, confirm_leave: bool = false) -> void:
	_networked = not on
	_hosting = hosting
	_confirm_practice = on and confirm_leave


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
	if not _networked and not _confirm_practice:
		exit_pressed.emit()
		return
	if is_instance_valid(_confirmation) and _confirmation.is_open:
		return
	_confirmation = DialogButtons.leave_match(_hosting, exit_pressed.emit, _confirm_practice)
	_confirmation.open(self)


func _button(text: String, paint: String, parent: Control) -> Button:
	var button: Button = MenuDialog.make_button(text, paint, UiMetrics.BODY)
	parent.add_child(button)
	return button
