class_name UiAccessibility
extends RefCounted
## Apply reduced motion to existing and newly created UI controls.


static func apply_weak(reference: WeakRef) -> void:
	var node: Node = reference.get_ref() as Node
	if node != null:
		apply_node(node)


static func apply_tree(root: Node) -> void:
	apply_node(root)
	for child: Node in root.get_children():
		apply_tree(child)


static func apply_node(node: Node) -> void:
	if not is_instance_valid(node) or node.is_queued_for_deletion():
		return
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
