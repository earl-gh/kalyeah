class_name AccessibilityOptions
extends VBoxContainer
## Live comfort preferences shared by home, room and match settings: Reduce motion as a switch.

## The switch (on = motion reduced).
var motion_toggle: UiToggle


func _ready() -> void:
	name = "AccessibilityOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	motion_toggle = UiToggle.new()
	motion_toggle.name = "ReducedMotion"
	motion_toggle.set_state(Settings.reduced_motion)
	motion_toggle.toggled.connect(Settings.set_reduced_motion)
	add_child(UiKit.setting_row("Reduce motion", "Calmer menus, no drifting lights", motion_toggle))
	Settings.accessibility_changed.connect(_refresh)


func _refresh() -> void:
	if is_instance_valid(motion_toggle):
		motion_toggle.set_state(Settings.reduced_motion)
