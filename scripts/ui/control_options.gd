class_name ControlOptions
extends VBoxContainer
## How the match controls sit under your thumbs: a left-handed switch, the aim guide and Tilt to run. Shared by the Settings page and
## the in-match menu; changes apply to the controls at once. (The control size went with the D-pad: the hand of cards
## spans the whole width, so there is nothing left to grow.)

## The switch (on = the NEXT card on the right, the hand toward the left thumb).
var hand_toggle: UiToggle
## On = a thin line and rings while you aim; off = nothing is drawn.
var guide_toggle: UiToggle
## On = lean the phone to run (only on a phone that reports its tilt; Settings page and match menu).
var tilt_toggle: UiToggle
## The Tilt to run row and the line above it: built on a phone and shown once the sensor has reported (`_show_tilt`). Home builds this
## page when the app starts, before the first reading arrives, so it is asked again each time the page shows.
var tilt_row: Control
var _tilt_divider: Control
## In the match menu the list must stay short enough that Leave battle stays in view, so the aim guide is only on the Settings page.
var in_match: bool = false


func _ready() -> void:
	name = "ControlOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	hand_toggle = UiToggle.new()
	hand_toggle.name = "LeftHanded"
	hand_toggle.set_state(Settings.left_handed)
	hand_toggle.toggled.connect(Settings.set_left_handed)
	add_child(UiKit.setting_row("Left-handed", "NEXT card on the right, your cards to the left", hand_toggle))
	if not in_match:
		add_child(UiKit.divider())
		guide_toggle = UiToggle.new()
		guide_toggle.name = "AimGuide"
		guide_toggle.set_state(Settings.aim_guide)
		guide_toggle.toggled.connect(Settings.set_aim_guide)
		add_child(UiKit.setting_row("Aim guide", "A thin line while you aim", guide_toggle))
	# owner: Tilt to run can be switched mid-match too (the match reads it every tick)
	if TiltInput.possible():
		_tilt_divider = UiKit.divider()
		add_child(_tilt_divider)
		tilt_toggle = UiToggle.new()
		tilt_toggle.name = "TiltToRun"
		tilt_toggle.set_state(Settings.tilt_to_run)
		tilt_toggle.toggled.connect(Settings.set_tilt_to_run)
		tilt_row = UiKit.setting_row("Tilt to run", "Lean your phone the way to run. Hold it level to stop.", tilt_toggle)
		add_child(tilt_row)
		_show_tilt()
	Settings.controls_changed.connect(_refresh)


func _notification(what: int) -> void:
	if what == NOTIFICATION_VISIBILITY_CHANGED and is_visible_in_tree():
		_show_tilt()


func _show_tilt() -> void:
	if tilt_row == null:
		return
	var shown: bool = TiltInput.available()
	tilt_row.visible = shown
	_tilt_divider.visible = shown

func _refresh() -> void:
	if is_instance_valid(hand_toggle):
		hand_toggle.set_state(Settings.left_handed)
	if is_instance_valid(guide_toggle):
		guide_toggle.set_state(Settings.aim_guide)
	if is_instance_valid(tilt_toggle):
		tilt_toggle.set_state(Settings.tilt_to_run)
