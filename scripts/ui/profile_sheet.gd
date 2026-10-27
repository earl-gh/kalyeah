class_name ProfileSheet
extends RefCounted
## Who you are, in one sheet: your ID photo (taken with the camera, kept only on this phone: `IdPhoto`) and your name (12 letters at
## most, kept only on Save). There is no face to pick here (owner): the kid you play is chosen in the room and before every match.

## The photo's size on this sheet.
const PHOTO_SIDE: float = 112.0


## `on_saved` is called after a successful Save, once Session holds the new name. `on_take_photo` opens the camera and `on_photo_removed`
## follows Remove photo; without `on_take_photo` there is no photo row.
static func build(on_saved: Callable, on_take_photo: Callable = Callable(), on_photo_removed: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your profile", MenuDialog.WIDTH_SHEET)
	card.name = "ProfileSheet"
	if on_take_photo.is_valid() and (IdPhoto.exists() or IdPhoto.camera_possible()):
		card.body.add_child(_photo_row(card, on_take_photo, on_photo_removed))
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
		count.call())
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


## The ID photo (or an empty square) with Take photo, and Remove photo when there is one. Both leave this sheet.
static func _photo_row(card: MenuDialog, on_take_photo: Callable, on_photo_removed: Callable) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "PhotoRow"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var frame: PanelContainer = PanelContainer.new()
	frame.custom_minimum_size = Vector2(PHOTO_SIDE, PHOTO_SIDE)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = NameTag.PHOTO_EMPTY_BACK
	box.border_color = NameTag.CARD_EDGE
	box.set_border_width_all(3)
	box.set_corner_radius_all(int(NameTag.PHOTO_RADIUS))
	box.anti_aliasing = true
	frame.add_theme_stylebox_override("panel", box)
	var picture: TextureRect = TextureRect.new()
	picture.name = "Photo"
	picture.texture = IdPhoto.texture()
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	frame.add_child(picture)
	row.add_child(frame)
	var actions: VBoxContainer = VBoxContainer.new()
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	var take: Button = MenuDialog.make_button("New photo" if IdPhoto.exists() else "Take photo", "blue", UiMetrics.BODY, Vector2(240.0, UiMetrics.TOUCH))
	take.name = "TakePhoto"
	take.pressed.connect(func() -> void:
		card.close(false)
		on_take_photo.call())
	actions.add_child(take)
	if IdPhoto.exists():
		var remove: Button = MenuDialog.make_button("Remove photo", "grey", UiMetrics.BODY, Vector2(240.0, UiMetrics.TOUCH))
		remove.name = "RemovePhoto"
		remove.pressed.connect(func() -> void:
			IdPhoto.remove()
			card.close()
			if on_photo_removed.is_valid():
				on_photo_removed.call())
		actions.add_child(remove)
	row.add_child(actions)
	return row
