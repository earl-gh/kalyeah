class_name AccessibilitySection
extends VBoxContainer
## The Settings page's Accessibility card: what lets a player use the game despite a limit of sight, motion sensitivity, handedness or
## aiming. Reduce motion and Colour-blind colours (`AccessibilityOptions`), Screen shake, then Left-handed and Aim guide
## (`ControlOptions` part ACCESSIBLE). Every row works at once and is saved like the same switch anywhere else.

var shake_toggle: UiToggle


func _ready() -> void:
	name = "AccessibilitySection"
	add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(AccessibilityOptions.new())
	add_child(UiKit.divider())
	shake_toggle = GraphicsOptions.shake_toggle_new()
	add_child(GraphicsOptions.shake_row(shake_toggle))
	add_child(UiKit.divider())
	var controls: ControlOptions = ControlOptions.new()
	controls.part = ControlOptions.Part.ACCESSIBLE
	add_child(controls)
