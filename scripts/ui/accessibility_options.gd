class_name AccessibilityOptions
extends VBoxContainer
## Live preferences shared by home, room and match settings.

var motion_button: Button


func _ready() -> void:
	name = "AccessibilityOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var label: Label = LobbyWidgets.label("Reduce motion", UiMetrics.BODY)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(label)
	motion_button = MenuDialog.make_button("OFF", "grey", UiMetrics.ACTION, Vector2(140.0, UiMetrics.TOUCH))
	motion_button.name = "ReducedMotion"
	motion_button.pressed.connect(func() -> void: Settings.set_reduced_motion(not Settings.reduced_motion))
	row.add_child(motion_button)
	add_child(row)
	Settings.accessibility_changed.connect(_refresh)
	_refresh()


func _refresh() -> void:
	motion_button.text = "ON" if Settings.reduced_motion else "OFF"
	KalyeahTheme.style_button(motion_button, "green" if Settings.reduced_motion else "grey")
