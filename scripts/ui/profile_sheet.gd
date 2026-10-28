class_name ProfileSheet
extends RefCounted
## Who you are, in one sheet: your ID photo (taken with the camera, kept only on this phone: `IdPhoto`) and your name (kept only on Save). There is no face to pick here (owner): the kid you play is chosen in the room and before every match.

## `on_saved` is called after a successful Save, once Session holds the new name. `on_take_photo` opens the camera and `on_photo_removed`
## follows Remove photo; without `on_take_photo` the preview photo is read-only.
static func build(on_saved: Callable, on_take_photo: Callable = Callable(), on_photo_removed: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your nametag", MenuDialog.WIDTH_SHEET)
	card.name = "ProfileSheet"
	var holder: Control = Control.new()
	holder.name = "TagPreview"
	holder.custom_minimum_size.y = NameTag.CARD_HEIGHT + UiMetrics.PADDING
	card.body.add_child(holder)
	var tag: NameTag = NameTag.new()
	tag.name = "Preview"
	tag.swing = false
	tag.strap.hide()
	tag.card.z_as_relative = true
	tag.card.z_index = 0
	tag.tap_button.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.tap_button.focus_mode = Control.FOCUS_NONE
	tag.name_label.hide()
	tag.hint_label.hide()
	holder.add_child(tag)
	var field: LineEdit = LineEdit.new()
	field.name = "NameField"
	field.text = Session.player_name
	field.placeholder_text = "Your name"
	field.max_length = Session.MAX_NAME_LENGTH
	field.alignment = HORIZONTAL_ALIGNMENT_CENTER
	field.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	field.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.TRANSPARENT
	box.border_color = Color.TRANSPARENT
	box.set_border_width_all(3)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.content_margin_left = 0.0
	box.content_margin_right = 0.0
	box.anti_aliasing = true
	for state: String in ["normal", "read_only"]:
		field.add_theme_stylebox_override(state, box)
	var focused: StyleBoxFlat = box.duplicate() as StyleBoxFlat
	focused.border_color = KalyeahTheme.BLUE
	field.add_theme_stylebox_override("focus", focused)
	field.add_theme_color_override("font_color", KalyeahTheme.MODAL_TEXT)
	field.add_theme_color_override("font_placeholder_color", KalyeahTheme.MODAL_TEXT_MUTED)
	field.add_theme_color_override("caret_color", KalyeahTheme.MODAL_TEXT)
	tag.card.add_child(field)
	var fit_preview: Callable = func() -> void:
		tag.set_player_name(field.text)
		var fitted: float = minf(1.0, holder.size.x / tag.card.size.x) if holder.size.x > 0.0 else 1.0
		tag.hang(Vector2(holder.size.x / 2.0, NameTag.SLOT_MIDDLE + UiMetrics.COMPACT_GAP), 0.0, fitted)
		field.position = tag.name_label.position - Vector2(0.0, UiMetrics.GAP)
		field.size = Vector2(tag.name_label.size.x, UiMetrics.TOUCH)
		field.alignment = tag.name_label.horizontal_alignment
		field.add_theme_font_override("font", tag.name_label.get_theme_font("font"))
		field.add_theme_font_size_override("font_size", tag.name_label.get_theme_font_size("font_size"))
	holder.resized.connect(fit_preview)
	card.opened.connect(fit_preview)
	if on_take_photo.is_valid() and (IdPhoto.exists() or IdPhoto.camera_possible()):
		var take: Button = Button.new()
		take.name = "TakePhoto"
		take.accessibility_name = "New photo" if IdPhoto.exists() else "Take photo"
		take.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			take.add_theme_stylebox_override(state, StyleBoxEmpty.new())
		take.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_M))
		tag.photo_frame.add_child(take)
		take.pressed.connect(func() -> void:
			card.close(false)
			on_take_photo.call())
	var hint: Label = card.add_hint("")
	var count: Callable = func() -> void:
		hint.text = "%d / %d letters" % [field.text.length(), Session.MAX_NAME_LENGTH]
		hint.remove_theme_color_override("font_color")
	count.call()
	# only the letters a name can have, live: anything else typed or pasted is dropped
	field.text_changed.connect(func(typed: String) -> void:
		var clean: String = Session.filter_name(typed)
		if clean != typed:
			var caret: int = field.caret_column - (typed.length() - clean.length())
			field.text = clean
			field.caret_column = clampi(caret, 0, clean.length())
		count.call()
		fit_preview.call())
	var save: Button = MenuDialog.make_button("Save", "orange", UiMetrics.BODY)
	save.name = "Save"
	var actions: Array[Button] = []
	if IdPhoto.exists() and on_photo_removed.is_valid():
		var remove: Button = MenuDialog.make_button("Remove photo", "grey", UiMetrics.BODY)
		remove.name = "RemovePhoto"
		remove.pressed.connect(func() -> void:
			IdPhoto.remove()
			card.close()
			on_photo_removed.call())
		actions.append(remove)
	actions.append(save)
	card.add_buttons(actions)
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
	card.opened.connect(field.grab_focus.call_deferred)
	return card
