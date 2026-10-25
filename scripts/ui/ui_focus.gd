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


## Focuses the text field of `root` (typing needs one); a finger needs no focus ring on a button.
static func first(root: Control) -> void:
	for control: Control in controls(root):
		if control is LineEdit:
			control.grab_focus()
			return


static func restore(previous: WeakRef) -> void:
	if previous == null:
		return
	var control: Control = previous.get_ref() as Control
	if control != null and control.is_inside_tree() and control.is_visible_in_tree():
		control.grab_focus()
