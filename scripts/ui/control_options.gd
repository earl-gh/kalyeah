class_name ControlOptions
extends VBoxContainer
## How the match controls sit under your thumbs: a left-handed switch and the aim guide. Shared by the Settings page and
## the in-match menu; changes apply to the controls at once. (The control size went with the D-pad: the hand of cards
## spans the whole width, so there is nothing left to grow.)

## The switch (on = the NEXT card on the right, the hand toward the left thumb).
var hand_toggle: UiToggle
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
	add_child(UiKit.setting_row("Left-handed", "NEXT card on the right, your cards to the left", hand_toggle))
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


func _refresh() -> void:
	if is_instance_valid(hand_toggle):
		hand_toggle.set_state(Settings.left_handed)
	if is_instance_valid(guide_toggle):
		guide_toggle.set_state(Settings.aim_guide)
