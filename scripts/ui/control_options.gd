class_name ControlOptions
extends VBoxContainer
## How the match controls sit under your thumbs: a left-handed switch, a size and the aim guide. Shared by the
## Settings page and the in-match menu; changes apply to the controls at once.

## The switch (on = stick on the right, weapons on the left).
var hand_toggle: UiToggle
var size_choice: UiSegmented
## On = a thin line and rings while you aim; off = nothing is drawn.
var guide_toggle: UiToggle
## In the match menu the list must stay short enough that Resume and Leave stay in view, so the aim guide is only on the Settings page.
var in_match: bool = false


func _ready() -> void:
	name = "ControlOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	hand_toggle = UiToggle.new()
	hand_toggle.name = "LeftHanded"
	hand_toggle.set_state(Settings.left_handed)
	hand_toggle.toggled.connect(Settings.set_left_handed)
	add_child(UiKit.setting_row("Left-handed", "Stick on the right, weapons on the left", hand_toggle))
	add_child(UiKit.divider())
	size_choice = UiSegmented.new([
		{"id": &"small", "label": "Small"},
		{"id": &"normal", "label": "Normal"},
		{"id": &"large", "label": "Large"},
	], 72.0, UiMetrics.BODY)
	size_choice.name = "ControlSize"
	size_choice.select(size_id(Settings.control_scale), false)
	size_choice.chosen.connect(func(id: StringName) -> void: Settings.set_control_scale(Settings.CONTROL_SIZES[id]))
	add_child(UiKit.setting_row("Control size", "The stick and the weapon buttons", Control.new()))
	add_child(size_choice)
	if in_match:
		Settings.controls_changed.connect(_refresh)
		return
	add_child(UiKit.divider())
	guide_toggle = UiToggle.new()
	guide_toggle.name = "AimGuide"
	guide_toggle.set_state(Settings.aim_guide)
	guide_toggle.toggled.connect(Settings.set_aim_guide)
	add_child(UiKit.setting_row("Aim guide", "A thin line while you aim", guide_toggle))
	Settings.controls_changed.connect(_refresh)


## The name of the size closest to `scale`.
static func size_id(scale: float) -> StringName:
	var wanted: float = Settings.nearest_control_scale(scale)
	for id: StringName in Settings.CONTROL_SIZES:
		if is_equal_approx(Settings.CONTROL_SIZES[id], wanted):
			return id
	return &"normal"


func _refresh() -> void:
	if is_instance_valid(hand_toggle):
		hand_toggle.set_state(Settings.left_handed)
	if is_instance_valid(size_choice):
		size_choice.select(size_id(Settings.control_scale), false)
	if is_instance_valid(guide_toggle):
		guide_toggle.set_state(Settings.aim_guide)
