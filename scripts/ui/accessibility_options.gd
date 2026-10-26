class_name AccessibilityOptions
extends VBoxContainer
## Live comfort preferences shared by home, room and match settings: Reduce motion as a switch.

## The switch (on = motion reduced).
var motion_toggle: UiToggle
## The colour-blind palette switch.
var colorblind_toggle: UiToggle


func _ready() -> void:
	name = "AccessibilityOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	motion_toggle = UiToggle.new()
	motion_toggle.name = "ReducedMotion"
	motion_toggle.set_state(Settings.reduced_motion)
	motion_toggle.toggled.connect(Settings.set_reduced_motion)
	add_child(UiKit.setting_row("Reduce motion", "Calmer menus, no drifting lights", motion_toggle))
	add_child(UiKit.divider())
	colorblind_toggle = UiToggle.new()
	colorblind_toggle.name = "Colorblind"
	colorblind_toggle.set_state(Settings.colorblind)
	colorblind_toggle.toggled.connect(Settings.set_colorblind)
	add_child(UiKit.setting_row("Colour-blind colours", "Blue and orange teams. The street changes next battle!", colorblind_toggle))
	Settings.accessibility_changed.connect(_refresh)


func _refresh() -> void:
	if is_instance_valid(motion_toggle):
		motion_toggle.set_state(Settings.reduced_motion)
	if is_instance_valid(colorblind_toggle):
		colorblind_toggle.set_state(Settings.colorblind)
