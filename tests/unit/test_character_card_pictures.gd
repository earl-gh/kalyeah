extends GutTest
## The Characters tab shows the owner's card picture of each kid as the whole card, bare (assets/ui/character_cards/<id>.webp),
## and keeps the glass plate with the face for a kid that has no picture. Facts about positions, sizes and nodes, not pixels.


var _phone: SubViewport


func _page() -> CharactersPage:
	_phone = autofree(SubViewport.new()) as SubViewport
	_phone.size = Vector2i(720, 1280)
	add_child(_phone)
	var page: CharactersPage = CharactersPage.new()
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_phone.add_child(page)
	return page


func _picture_ids() -> Array[String]:
	var found: Array[String] = []
	for file: String in DirAccess.get_files_at(CharactersPage.CARD_PICTURE_DIR):
		# a build lists the imported files with a suffix; the editor lists the source files
		var stem: String = file.replace(".import", "").replace(".remap", "")
		if stem.ends_with(".webp") and not found.has(stem.get_basename()):
			found.append(stem.get_basename())
	return found


func test_every_picture_in_the_folder_belongs_to_a_kid() -> void:
	var ids: Array[String] = _picture_ids()
	assert_gt(ids.size(), 0, "the folder has the owner's pictures")
	for id: String in ids:
		assert_true(Session.AVATARS.has(id), "%s.webp is a character id" % id)
		assert_not_null(load("res://data/characters/%s.tres" % id), "%s has a character" % id)
		assert_not_null(CharactersPage.card_picture(StringName(id)), "%s loads" % id)


func test_every_kid_has_a_picture_and_all_pictures_fit_the_cell() -> void:
	for id: String in Session.AVATARS:
		var art: Texture2D = CharactersPage.card_picture(StringName(id))
		assert_not_null(art, "%s has a card picture" % id)
		if art == null:
			continue
		assert_almost_eq(float(art.get_width()) / float(art.get_height()), CharactersPage.CARD.x / CharactersPage.CARD.y, 0.01, "%s: the picture has the cell's shape, so nothing is cut or stretched" % id)
		assert_lte(art.get_width(), 520, "%s: downscaled for the phone" % id)


func test_a_kid_with_a_picture_shows_it_bare() -> void:
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	for id: String in Session.AVATARS:
		var card: Button = page.cards[id]
		var picture: TextureRect = card.get_node("Picture") as TextureRect
		assert_eq(picture.texture, CharactersPage.card_picture(StringName(id)), "%s shows its own card picture" % id)
		assert_null(picture.material, "%s: no rounded crop over it" % id)
		assert_eq(picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_CENTERED, "%s: its own shape, centred, never stretched" % id)
		assert_null(card.find_child("Fade", true, false), "%s: no dark fade over it" % id)
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			assert_true(card.get_theme_stylebox(state) is StyleBoxEmpty, "%s: no plate behind it (%s)" % [id, state])
		assert_eq(picture.get_global_rect().size, card.get_global_rect().size, "%s: the picture fills the cell and the art is fitted inside it" % id)


func test_every_card_is_the_same_size_and_the_grid_is_aligned() -> void:
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	var left: Array[float] = []
	var right: Array[float] = []
	var index: int = 0
	for id: String in Session.AVATARS:
		var rect: Rect2 = page.cards[id].get_global_rect()
		assert_almost_eq(rect.size.y, CharactersPage.CARD.y, 0.5, "%s: the cell height" % id)
		assert_almost_eq(rect.size.x, page.cards["junjun"].get_global_rect().size.x, 0.5, "%s: the same cell width" % id)
		(left if index % 2 == 0 else right).append(rect.position.x)
		index += 1
	for column: Array[float] in [left, right]:
		for x: float in column:
			assert_almost_eq(x, column[0], 0.5, "a column lines up")


func test_the_name_is_on_the_card_and_readable() -> void:
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	for id: String in Session.AVATARS:
		var card: Button = page.cards[id]
		var label: Label = card.get_node("Name") as Label
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		assert_eq(label.text, definition.display_name.to_upper(), id)
		assert_true(label.is_visible_in_tree(), "%s: the name shows" % id)
		assert_gte(label.get_theme_font_size("font_size"), UiMetrics.CAPTION, "%s: text size" % id)
		assert_gt(label.get_theme_constant("outline_size"), 0, "%s: an outline, so it reads on paper and on art" % id)
		var inside: Rect2 = card.get_global_rect().intersection(label.get_global_rect())
		assert_eq(inside.size, label.get_global_rect().size, "%s: the name sits on its card" % id)
		assert_gt(label.get_global_rect().get_center().y, card.get_global_rect().position.y + card.size.y * 0.8, "%s: on the bottom edge" % id)
		assert_eq(label.mouse_filter, Control.MOUSE_FILTER_IGNORE, "%s: a touch on the name is a touch on the card" % id)


func test_a_kid_without_a_picture_keeps_the_plate_and_the_face() -> void:
	var page: CharactersPage = _page()
	assert_null(CharactersPage.card_picture(&"nobody"), "no file, no picture")
	var card: Button = page._card("nobody")
	autofree(card)
	page.add_child(card)
	var picture: TextureRect = card.get_node("Picture") as TextureRect
	assert_not_null(picture.texture, "the face stands in")
	assert_not_null(picture.material, "cropped to the plate as before")
	assert_eq(picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_COVERED)
	assert_not_null(card.find_child("Fade", true, false), "the fade under the name")
	assert_false(card.get_theme_stylebox("normal") is StyleBoxEmpty, "the glass plate")
	assert_eq((card.get_node("Name") as Label).text, "NOBODY")
	assert_eq(card.custom_minimum_size.y, CharactersPage.CARD.y, "the same cell height as the picture cards")


func test_a_tap_on_a_picture_card_opens_its_sheet_and_chooses_nothing() -> void:
	Session.avatar_id = "toni"
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	var opened: Array[CharacterDef] = []
	page.character_opened.connect(func(definition: CharacterDef) -> void: opened.append(definition))
	var card: Button = page.cards["ligaya"]
	var at: Vector2 = card.get_global_rect().get_center()
	_phone.push_input(TouchHelper.click(at, true))
	_phone.push_input(TouchHelper.click(at, false))
	await wait_process_frames(1)
	assert_eq(opened.size(), 1)
	assert_eq(opened[0].id, &"ligaya")
	assert_eq(Session.avatar_id, "toni", "reading is not choosing")


func test_press_feedback_stays_with_reduce_motion() -> void:
	var was: bool = Settings.reduced_motion
	Settings.reduced_motion = true
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	var card: Button = page.cards["junjun"]
	var picture: TextureRect = card.get_node("Picture") as TextureRect
	card.button_down.emit()
	assert_lt(picture.modulate.r, 1.0, "a pressed card dims, with or without movement")
	assert_eq(card.scale, Vector2.ONE, "Reduce motion: no squeeze")
	card.button_up.emit()
	assert_eq(picture.modulate, Color.WHITE, "it lets go")
	Settings.reduced_motion = was


func test_a_card_keeps_its_accessible_name_and_focus_ring() -> void:
	var page: CharactersPage = _page()
	await wait_process_frames(3)
	for id: String in Session.AVATARS:
		var card: Button = page.cards[id]
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		assert_eq(card.accessibility_name, "%s: read about this kid" % definition.display_name)
		assert_true(card.has_theme_stylebox_override("focus"), "%s: a focus ring" % id)
		assert_gte(card.get_global_rect().size.y, UiMetrics.MIN_TARGET, "%s: a big target" % id)


func test_the_sheet_shows_the_card_bare_and_a_plain_face_without_one() -> void:
	var with_art: CharacterDef = load("res://data/characters/popoy.tres") as CharacterDef
	var sheet: MenuDialog = autofree(CharactersPage.detail(with_art)) as MenuDialog
	add_child(sheet)
	var picture: TextureRect = sheet.find_child("Picture", true, false) as TextureRect
	assert_eq(picture.texture, CharactersPage.card_picture(&"popoy"))
	assert_null(picture.material, "no crop over the card")
	assert_eq(picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_CENTERED)
	assert_almost_eq(picture.custom_minimum_size.x / picture.custom_minimum_size.y, CharactersPage.CARD.x / CharactersPage.CARD.y, 0.01, "the sheet's picture has the card's shape")
	var pretend: CharacterDef = load("res://data/characters/popoy.tres").duplicate() as CharacterDef
	pretend.id = &"nobody"
	var plain: MenuDialog = autofree(CharactersPage.detail(pretend)) as MenuDialog
	add_child(plain)
	var face: TextureRect = plain.find_child("Picture", true, false) as TextureRect
	assert_not_null(face.texture)
	assert_not_null(face.material, "the face keeps its rounded crop")
	assert_eq(face.custom_minimum_size, CharactersPage.DETAIL_PICTURE)
