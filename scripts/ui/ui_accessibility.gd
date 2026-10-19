class_name UiAccessibility
extends RefCounted
## Reading-text scaling uses each control's original font size, never its last scaled size.
## Buttons, portrait captions and drawn HUD counters retain their spatial/touch geometry.

const SCALES: Array[float] = [1.0, 1.2, 1.4]
const BASE_FONT: StringName = &"reading_base_font"
const FIXED: StringName = &"fixed_reading_size"
const FIT_HEIGHT: StringName = &"fit_reading_height"


static func valid_scale(value: Variant) -> float:
	if not (value is int or value is float) or not is_finite(float(value)):
		return 1.0
	var nearest: float = 1.0
	for scale: float in SCALES:
		if absf(float(value) - scale) < absf(float(value) - nearest):
			nearest = scale
	return nearest


static func apply_weak(reference: WeakRef) -> void:
	var node: Node = reference.get_ref() as Node
	if node != null:
		apply_node(node)


static func apply_tree(root: Node) -> void:
	apply_node(root)
	for child: Node in root.get_children():
		apply_tree(child)
	if root is Control:
		fit_height(root as Control)


static func apply_node(node: Node) -> void:
	if not is_instance_valid(node) or node.is_queued_for_deletion():
		return
	if (node is Label or node is LineEdit) and not node.get_meta(FIXED, false):
		var control: Control = node as Control
		if not control.has_meta(BASE_FONT):
			control.set_meta(BASE_FONT, control.get_theme_font_size("font_size"))
		control.add_theme_font_size_override("font_size", roundi(int(control.get_meta(BASE_FONT)) * Settings.text_scale))
	if node is Control and Settings.reduced_motion:
		var entrance: Tween = (node.get_meta(&"entrance_tween") as Tween) if node.has_meta(&"entrance_tween") else null
		if is_instance_valid(entrance):
			entrance.kill()
			(node as Control).scale = Vector2.ONE
			(node as Control).modulate.a = 1.0
	if node is Button and Settings.reduced_motion:
		var button: Button = node as Button
		var tween: Tween = (button.get_meta(&"press_tween") as Tween) if button.has_meta(&"press_tween") else null
		if is_instance_valid(tween):
			tween.kill()
			button.scale = Vector2.ONE
	var parent: Node = node.get_parent()
	while parent is Control:
		fit_height(parent as Control)
		parent = parent.get_parent()


static func fit_height(control: Control) -> void:
	if not control.get_meta(FIT_HEIGHT, false):
		return
	if not control.has_meta(&"reading_base_height"):
		control.set_meta(&"reading_base_height", control.custom_minimum_size.y)
	var height: float = float(control.get_meta(&"reading_base_height"))
	for child: Node in control.get_children():
		if child is Control and (child as Control).visible:
			height = maxf(height, (child as Control).get_combined_minimum_size().y)
	control.custom_minimum_size.y = height
