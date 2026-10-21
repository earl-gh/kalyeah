class_name UiA11y
extends RefCounted
## Names for assistive technology (a screen reader reads `accessibility_name`): every control that has no words of its own
## (an icon, a drawn button, a switch, a slider, a stick) is given a short name and, where it helps, a hint.
## tests/unit/test_ui_lint.gd fails on a visible control that has neither text nor a name.


## Gives `control` its name (and hint); returns it so it can be used in a chain.
static func name_it(control: Control, label: String, hint: String = "") -> Control:
	control.accessibility_name = label
	if not hint.is_empty():
		control.accessibility_description = hint
	return control


## True when something would be read for `control`: its own text, or a name.
static func is_named(control: Control) -> bool:
	if not control.accessibility_name.is_empty():
		return true
	if control is Button:
		return not (control as Button).text.is_empty()
	if control is Label:
		return not (control as Label).text.is_empty()
	if control is LineEdit:
		return not (control as LineEdit).placeholder_text.is_empty() or not (control as LineEdit).text.is_empty()
	return false
