class_name ProfileSheet
extends RefCounted
## Who you are, in one sheet: your name (12 letters at most), kept only on Save. There is no face to pick here
## (owner): the kid you play is chosen in the room and before every match, and the face shown next to your name
## is the one you played last.


## `on_saved` is called after a successful Save, once Session holds the new name.
static func build(on_saved: Callable) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your profile", MenuDialog.WIDTH_SHEET)
	card.name = "ProfileSheet"
	var field: LineEdit = LineEdit.new()
	field.name = "NameField"
	field.text = Session.player_name
	field.placeholder_text = "Your name"
	field.max_length = Session.MAX_NAME_LENGTH
	field.alignment = HORIZONTAL_ALIGNMENT_CENTER
	field.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	field.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.WHITE
	box.border_color = KalyeahTheme.MODAL_BODY_EDGE
	box.set_border_width_all(3)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.content_margin_left = 16.0
	box.content_margin_right = 16.0
	box.anti_aliasing = true
	for state: String in ["normal", "read_only"]:
		field.add_theme_stylebox_override(state, box)
	var focused: StyleBoxFlat = box.duplicate() as StyleBoxFlat
	focused.border_color = KalyeahTheme.BLUE
	field.add_theme_stylebox_override("focus", focused)
	field.add_theme_color_override("font_color", KalyeahTheme.MODAL_TEXT)
	field.add_theme_color_override("font_placeholder_color", KalyeahTheme.MODAL_TEXT_MUTED)
	field.add_theme_color_override("caret_color", KalyeahTheme.MODAL_TEXT)
	card.body.add_child(field)
	var hint: Label = card.add_hint("%d characters max." % Session.MAX_NAME_LENGTH)
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.BODY)
	cancel.name = "Cancel"
	var save: Button = MenuDialog.make_button("Save", "orange", UiMetrics.BODY)
	save.name = "Save"
	card.add_buttons([cancel, save])
	var apply: Callable = func() -> void:
		var clean: String = Session.clean_name(field.text)
		if clean.is_empty():
			hint.text = "Pick a name!"
			hint.add_theme_color_override("font_color", Color("b3261e"))  # a dark red that reads on the light body
			return
		Session.player_name = clean
		Session.save()
		card.close()
		if on_saved.is_valid():
			on_saved.call()
	save.pressed.connect(apply)
	field.text_submitted.connect(func(_text: String) -> void: apply.call())
	cancel.pressed.connect(card.close)
	card.opened.connect(field.grab_focus.call_deferred)
	return card
