class_name UiFocus
extends RefCounted
## Modal focus stays in visible, enabled controls and returns to its opener on dismissal.


static func controls(root: Node) -> Array[Control]:
	var result: Array[Control] = []
	for child: Node in root.get_children():
		if child is Control:
			var control: Control = child as Control
			if not control.is_visible_in_tree():
				continue
			var enabled: bool = not (control is BaseButton and (control as BaseButton).disabled)
			if control.focus_mode == Control.FOCUS_ALL and enabled:
				result.append(control)
		result.append_array(controls(child))
	return result


## Focuses a text field (typing needs one) and, when `buttons_too`, the first control otherwise.
static func first(root: Control, buttons_too: bool = true) -> void:
	var choices: Array[Control] = controls(root)
	for control: Control in choices:
		if control is LineEdit:
			control.grab_focus()
			return
	if buttons_too and not choices.is_empty():
		choices[0].grab_focus()


static func advance(root: Control, backwards: bool) -> void:
	var choices: Array[Control] = controls(root)
	if choices.is_empty():
		return
	var current: Control = root.get_viewport().gui_get_focus_owner()
	var at: int = choices.find(current)
	var next: int = wrapi(at + (-1 if backwards else 1), 0, choices.size())
	choices[next].grab_focus()


static func restore(previous: WeakRef) -> void:
	if previous == null:
		return
	var control: Control = previous.get_ref() as Control
	if control != null and control.is_inside_tree() and control.is_visible_in_tree():
		control.grab_focus()
