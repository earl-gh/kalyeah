class_name UiSegmented
extends HBoxContainer
## A row of two to four big choices of which one is on (Characters | Weapons). The chosen one is a
## gold game button, the rest are quiet slate ones; every segment shares the width.

signal chosen(id: StringName)

var buttons: Dictionary[StringName, Button] = {}
var current: StringName = &""


## `options`: {id, label} in order.
func _init(options: Array[Dictionary] = []) -> void:
	name = "Segmented"
	add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	for option: Dictionary in options:
		var id: StringName = option["id"] as StringName
		var button: Button = MenuDialog.make_button(String(option["label"]), "grey", UiMetrics.ACTION, Vector2(0.0, 80.0))
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(select.bind(id))
		add_child(button)
		buttons[id] = button


## Turns `id` on (and tells the listener when it is a change).
func select(id: StringName, announce: bool = true) -> void:
	var changed: bool = id != current
	current = id
	for other: StringName in buttons:
		KalyeahTheme.style_button(buttons[other], "orange" if other == id else "grey")
	if changed and announce:
		chosen.emit(id)
