class_name ProfileSheet
extends RefCounted
## Who you are, in one sheet: your name (12 letters at most) and your face (one of the six kids). Both are
## kept only on Save; Cancel leaves them as they were. The face is the one on the main screen, in the
## room and next to your name; the kid you play is still picked in the room and before every match.

const FACE: float = 104.0
const RING: Color = Color("ffc233")


## `on_saved` is called after a successful Save, once Session holds the new name and face.
static func build(on_saved: Callable) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your profile", 620.0)
	card.name = "ProfileSheet"
	var chosen: Array[String] = [Session.avatar_id]
	var group: ButtonGroup = ButtonGroup.new()
	var grid: GridContainer = GridContainer.new()
	grid.name = "Faces"
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", UiMetrics.GAP)
	grid.add_theme_constant_override("v_separation", UiMetrics.GAP)
	grid.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	card.body.add_child(grid)
	for id: String in Session.AVATARS:
		grid.add_child(_face(id, group, chosen))
	var field: LineEdit = LineEdit.new()
	field.name = "NameField"
	field.text = Session.player_name
	field.placeholder_text = "Your name"
	field.max_length = Session.MAX_NAME_LENGTH
	field.alignment = HORIZONTAL_ALIGNMENT_CENTER
	field.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	field.add_theme_font_size_override("font_size", 36)
	card.body.add_child(field)
	var hint: Label = card.add_hint("%d characters max." % Session.MAX_NAME_LENGTH)
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", 30)
	cancel.name = "Cancel"
	var save: Button = MenuDialog.make_button("Save", "orange", 30)
	save.name = "Save"
	card.add_buttons([cancel, save])
	var apply: Callable = func() -> void:
		var clean: String = Session.clean_name(field.text)
		if clean.is_empty():
			hint.text = "Enter a name."
			hint.add_theme_color_override("font_color", KalyeahTheme.ERROR)
			return
		Session.player_name = clean
		Session.avatar_id = chosen[0]
		Session.save()
		card.close()
		if on_saved.is_valid():
			on_saved.call()
	save.pressed.connect(apply)
	field.text_submitted.connect(func(_text: String) -> void: apply.call())
	cancel.pressed.connect(card.close)
	card.opened.connect(field.grab_focus.call_deferred)
	return card


## One face as a round toggle: a gold ring while it is the one chosen.
static func _face(id: String, group: ButtonGroup, chosen: Array[String]) -> Button:
	var button: Button = Button.new()
	button.name = "Face_%s" % id
	button.toggle_mode = true
	button.button_group = group
	button.custom_minimum_size = Vector2(FACE + 24.0, FACE + 24.0)
	button.focus_mode = Control.FOCUS_ALL
	button.tooltip_text = id.capitalize()
	button.accessibility_name = "Face: %s" % id.capitalize()
	var ring: StyleBoxFlat = StyleBoxFlat.new()
	ring.draw_center = false
	ring.set_corner_radius_all(int((FACE + 24.0) / 2.0))
	ring.set_border_width_all(7)
	ring.border_color = RING
	ring.shadow_color = Color(RING, 0.45)
	ring.shadow_size = 12
	ring.anti_aliasing = true
	for state: String in ["normal", "hover"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	for state: String in ["pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, ring)
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(int((FACE + 24.0) / 2.0)))
	var picture: TextureRect = UiKit.avatar(id, FACE)
	picture.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	picture.offset_left = -FACE / 2.0
	picture.offset_right = FACE / 2.0
	picture.offset_top = -FACE / 2.0
	picture.offset_bottom = FACE / 2.0
	button.add_child(picture)
	button.set_pressed_no_signal(id == chosen[0])
	button.pressed.connect(func() -> void: chosen[0] = id)
	MenuDialog.feel_press(button)
	return button
