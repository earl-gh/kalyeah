class_name ProfileSheet
extends RefCounted
## Who you are, in one sheet: your ID photo (kept only on this phone: `IdPhoto`) and your name (kept only on Save). The photo is picked from a grid
## of two rows of four tiles (owner, 2026-11-03): 1 takes a photo, 2 picks one from the gallery, 3 to 8 are the six avatars (3 is the default).
## There is no kid to play to pick here: that is chosen in the room and before every match.

## The field's inner room on every side (the left and right are the same as the top and bottom), and its border's width.
const FIELD_PAD: float = 4.0
const FIELD_BORDER: float = 3.0
## The letter count at the top right of the tag.
const COUNT_WIDTH: float = 140.0
const COUNT_RIGHT: float = 24.0
const COUNT_TOP: float = 14.0
## The name field shows it can be tapped before it is: a grey outline and a light grey tint (the focus ring turns blue and the tint goes).
const FIELD_EDGE: Color = Color(KalyeahTheme.MODAL_TEXT_MUTED, 0.6)
const FIELD_TINT: Color = Color(KalyeahTheme.MODAL_TEXT_MUTED, 0.14)
## The photo grid: four tiles a row, each a square this wide.
const PHOTO_COLUMNS: int = 4
const PHOTO_TILE: float = 116.0
## The camera and gallery tiles' side padding, small so their words fit.
const TILE_PAD: float = 4.0


## `on_saved` is called after a successful Save, once Session holds the new name. The photo grid sits under the tag: `on_take_photo` (tile 1,
## the camera, only on a device that can have one) and `on_pick_gallery` (tile 2, only where the phone has a picker) close the sheet and hand
## over; a tap on an avatar keeps it at once, the sheet stays, and `on_avatar_picked` is called; `on_photo_removed` follows Remove photo (back
## to the default avatar). Without any of the three the sheet has no grid and the photo is read-only.
static func build(on_saved: Callable, on_take_photo: Callable = Callable(), on_photo_removed: Callable = Callable(), on_pick_gallery: Callable = Callable(), on_avatar_picked: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your nametag", MenuDialog.WIDTH_SHEET)
	card.name = "ProfileSheet"
	# The tag itself is the foreground surface; no second card surrounds it.
	card.panel.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	card.inner.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	card.find_child("Header", true, false).hide()  # The tag dismisses through the scrim or Back.
	var holder: Control = Control.new()
	holder.name = "TagPreview"
	holder.custom_minimum_size.y = NameTag.CARD_HEIGHT * 1.15 + UiMetrics.PADDING
	card.body.add_child(holder)
	var tag: NameTag = NameTag.new()
	tag.name = "Preview"
	# the editing tag is the card alone: no lanyard, and it does not swing (owner)
	tag.swing = false
	tag.strap.hide()
	(card.find_child("DialogScroll", true, false) as ScrollContainer).clip_contents = false
	tag.card.z_as_relative = true
	tag.card.z_index = 1
	tag.tap_button.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.tap_button.focus_mode = Control.FOCUS_NONE
	tag.name_label.hide()
	tag.hint_label.hide()
	holder.add_child(tag)
	var field: LineEdit = LineEdit.new()
	field.name = "NameField"
	field.text = Session.player_name.to_upper()
	field.placeholder_text = "Your name"
	field.max_length = Session.MAX_NAME_LENGTH
	field.alignment = HORIZONTAL_ALIGNMENT_CENTER
	field.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	field.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = FIELD_TINT
	box.border_color = FIELD_EDGE
	box.set_border_width_all(int(FIELD_BORDER))
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.content_margin_left = FIELD_PAD
	box.content_margin_right = FIELD_PAD
	box.content_margin_top = FIELD_PAD
	box.content_margin_bottom = FIELD_PAD
	box.anti_aliasing = true
	for state: String in ["normal", "read_only"]:
		field.add_theme_stylebox_override(state, box)
	var focused: StyleBoxFlat = box.duplicate() as StyleBoxFlat
	focused.border_color = KalyeahTheme.BLUE
	focused.bg_color = Color.TRANSPARENT
	field.add_theme_stylebox_override("focus", focused)
	field.add_theme_color_override("font_color", KalyeahTheme.MODAL_TEXT)
	field.add_theme_color_override("font_placeholder_color", KalyeahTheme.MODAL_TEXT_MUTED)
	field.add_theme_color_override("caret_color", KalyeahTheme.MODAL_TEXT)
	tag.card.add_child(field)
	# the letter count, like 12/12, at the top right of the tag
	var count_label: Label = Label.new()
	count_label.name = "LetterCount"
	count_label.theme_type_variation = &"DisplayLabel"
	count_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	count_label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	count_label.add_theme_color_override("font_color", NameTag.INK_MUTED)
	count_label.add_theme_constant_override("outline_size", 0)
	count_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.card.add_child(count_label)
	var fit_preview: Callable = func() -> void:
		tag.set_player_name(field.text)
		var fitted: float = minf(1.15, holder.size.x / tag.card.size.x) if holder.size.x > 0.0 else 1.0
		tag.hang(Vector2(holder.size.x / 2.0, NameTag.SLOT_MIDDLE * fitted + UiMetrics.COMPACT_GAP), -card.size.y, fitted)
		# the field's text starts where the name's letters do: the border and the inner room stand out beyond them
		var beyond: float = FIELD_BORDER + FIELD_PAD
		field.position = tag.name_label.position - Vector2(beyond, UiMetrics.GAP)
		field.size = Vector2(tag.name_label.size.x + beyond * 2.0, UiMetrics.TOUCH)
		count_label.size = Vector2(COUNT_WIDTH, count_label.get_minimum_size().y)
		count_label.position = Vector2(tag.card.size.x - COUNT_WIDTH - COUNT_RIGHT, COUNT_TOP)
		field.alignment = tag.name_label.horizontal_alignment
		field.add_theme_font_override("font", tag.name_label.get_theme_font("font"))
		field.add_theme_font_size_override("font_size", tag.name_label.get_theme_font_size("font_size"))
	holder.resized.connect(fit_preview)
	card.opened.connect(fit_preview)
	# the photo grid: 1 camera, 2 gallery, 3 to 8 the avatars, two rows of four, under the tag
	var avatar_tiles: Dictionary[String, Button] = {}
	var show_selection: Callable = func() -> void:
		var chosen: String = AvatarPhotos.current()
		for id: String in avatar_tiles:
			avatar_tiles[id].get_node("Ring").visible = id == chosen
	var show_photo: Callable = func() -> void:
		tag.refresh_photo()
		fit_preview.call()
		show_selection.call()
	if on_take_photo.is_valid() or on_pick_gallery.is_valid() or on_avatar_picked.is_valid():
		var grid: GridContainer = GridContainer.new()
		grid.name = "PhotoOptions"
		grid.columns = PHOTO_COLUMNS
		grid.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
		grid.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
		grid.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		var ways: Array[Dictionary] = [
			{"name": "Camera", "text": "Camera", "say": "Take a photo with the camera", "act": on_take_photo, "can": IdPhoto.camera_possible()},
			{"name": "Gallery", "text": "Gallery", "say": "Pick a photo from the gallery", "act": on_pick_gallery, "can": IdPhotoPicker.available()},
		]
		for way: Dictionary in ways:
			var option: Button = MenuDialog.make_button(way["text"] as String, "blue", UiMetrics.CAPTION, Vector2.ONE * PHOTO_TILE)
			option.name = way["name"] as String
			option.accessibility_name = way["say"] as String
			option.clip_text = true  # the tile stays as wide as the avatars...
			for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
				var look: StyleBoxTexture = option.get_theme_stylebox(state).duplicate() as StyleBoxTexture  # a copy: the skin shares its own
				look.content_margin_left = TILE_PAD  # ...and the words get the room the button's wide padding took
				look.content_margin_right = TILE_PAD
				option.add_theme_stylebox_override(state, look)
			var act: Callable = way["act"] as Callable
			# a way this phone lacks stays in its place, greyed out, so the avatars keep theirs
			option.disabled = not (act.is_valid() and bool(way["can"]))
			option.pressed.connect(func() -> void:
				card.close(false)
				act.call())
			grid.add_child(option)
		for id: String in Session.AVATARS:
			var pick: Button = AvatarPhotos.tile(id, PHOTO_TILE)
			if pick == null:
				continue
			UiKit.feel_press(pick, &"select")
			avatar_tiles[id] = pick
			pick.pressed.connect(func() -> void:
				var picture: Image = AvatarPhotos.photo_of(id)
				if IdPhoto.save(picture) != OK:
					return
				show_photo.call()
				if on_avatar_picked.is_valid():
					on_avatar_picked.call())
			grid.add_child(pick)
		card.body.add_child(grid)
		show_selection.call()
	# the hint line is only for the "Pick a name!" answer
	var hint: Label = card.add_hint("")
	hint.hide()
	hint.add_theme_color_override("font_color", Color("ffb4ab"))  # readable against the dimmed background
	var count: Callable = func() -> void:
		count_label.text = "%d/%d" % [field.text.length(), Session.MAX_NAME_LENGTH]
	count.call()
	# only the letters a name can have, always in capitals, live: anything else typed or pasted is dropped; the caret stays where it was
	field.text_changed.connect(func(typed: String) -> void:
		var clean: String = Session.filter_name(typed).to_upper()
		if clean != typed:
			var caret: int = field.caret_column - (typed.length() - clean.length())
			field.text = clean
			field.caret_column = clampi(caret, 0, clean.length())
		hint.hide()
		count.call()
		fit_preview.call())
	# a tap on the field puts the caret at the end of the name
	field.focus_entered.connect(func() -> void: field.set_deferred("caret_column", field.text.length()))
	var save: Button = MenuDialog.make_button("Save", "orange", UiMetrics.BODY)
	save.name = "Save"
	var actions: Array[Button] = []
	if on_photo_removed.is_valid():
		var remove: Button = MenuDialog.make_button("Remove photo", "grey", UiMetrics.BODY)
		remove.name = "RemovePhoto"
		remove.visible = IdPhoto.exists()
		remove.pressed.connect(func() -> void:
			IdPhoto.remove()
			remove.hide()
			show_photo.call()  # back to the default avatar (tile 3)
			on_photo_removed.call())
		# a photo kept from a tile shows the button too
		var show_remove: Callable = func() -> void: remove.visible = IdPhoto.exists()
		for tile: Button in avatar_tiles.values():
			tile.pressed.connect(show_remove)
		actions.append(remove)
	actions.append(save)
	card.add_buttons(actions)
	var apply: Callable = func() -> void:
		var clean: String = Session.clean_name(field.text.to_upper())
		if clean.is_empty():
			hint.text = "Pick a name!"
			hint.show()
			return
		Session.player_name = clean
		Session.save()
		card.close()
		if on_saved.is_valid():
			on_saved.call()
	save.pressed.connect(apply)
	field.text_submitted.connect(func(_text: String) -> void: apply.call())
	# nothing opens the phone's keyboard until the field is tapped, and the sheet stays above it once it is
	card.focus_text_on_open = false
	card.keyboard_aware = true
	return card
