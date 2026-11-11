extends GutTest
## The nametag's photo grid: two rows of four tiles; 1 takes a photo with the camera, 2 picks one from the gallery, 3 to 8
## are the six ID-card sticker avatars (`Session.AVATARS.slice(0, 6)`) while `Session.AVATARS` carries all 10 Barangay kids.

const DIR: String = "res://tests/fixtures/photos/"
const SHOWN: Dictionary[int, Array] = {
	1: [32, 16, "red", "green", "blue", "yellow"],
	2: [32, 16, "green", "red", "yellow", "blue"],
	3: [32, 16, "yellow", "blue", "green", "red"],
	4: [32, 16, "blue", "yellow", "red", "green"],
	5: [16, 32, "red", "blue", "green", "yellow"],
	6: [16, 32, "blue", "red", "yellow", "green"],
	7: [16, 32, "yellow", "green", "blue", "red"],
	8: [16, 32, "green", "yellow", "red", "blue"],
}

var _host: Control
var _path: String


static func _temp_file(file_name: String) -> String:
	var probe: FileAccess = FileAccess.open("user://_test_tmp_photo_probe", FileAccess.WRITE)
	if probe != null:
		probe.close()
		DirAccess.remove_absolute(ProjectSettings.globalize_path("user://_test_tmp_photo_probe"))
		return "user://" + file_name
	return "res://.godot/_test_tmp_" + file_name


func before_each() -> void:
	_host = autofree(Control.new()) as Control
	_host.size = Vector2(1280.0, 720.0)
	add_child(_host)
	_path = IdPhoto.path
	IdPhoto.path = _temp_file("photo_options_test.png")
	IdPhoto.remove()


func after_each() -> void:
	IdPhotoPicker.available_override = null
	IdPhoto.remove()
	IdPhoto.path = _path


func _colour(image: Image, x: int, y: int) -> String:
	var c: Color = image.get_pixel(x, y)
	if c.r > 0.7 and c.g < 0.4:
		return "red"
	if c.g > 0.6 and c.r < 0.4:
		return "green"
	if c.b > 0.7:
		return "blue"
	return "yellow"


# ---- the gallery: reading what the phone hands back -----------------------------------------

func test_a_phone_photo_is_turned_upright_by_its_exif_orientation_in_all_eight_cases() -> void:
	for orientation: int in SHOWN:
		var bytes: PackedByteArray = FileAccess.get_file_as_bytes("%sexif_%d.jpg" % [DIR, orientation])
		assert_eq(IdPhotoPicker.exif_orientation(bytes), orientation, "orientation %d is read" % orientation)
		var image: Image = IdPhotoPicker.decode(bytes)
		var want: Array = SHOWN[orientation]
		assert_eq([image.get_width(), image.get_height()], [want[0], want[1]], "orientation %d: the size a viewer shows" % orientation)
		var w: int = image.get_width()
		var h: int = image.get_height()
		assert_eq([_colour(image, 2, 2), _colour(image, w - 3, 2), _colour(image, 2, h - 3), _colour(image, w - 3, h - 3)], want.slice(2), "orientation %d: upright" % orientation)


func test_png_and_webp_are_read_and_the_format_comes_from_the_bytes_not_a_name() -> void:
	for file: String in ["plain.png", "plain.webp"]:
		var image: Image = IdPhotoPicker.decode(FileAccess.get_file_as_bytes(DIR + file))
		assert_not_null(image, file)
		assert_eq(_colour(image, 2, 2), "red", file)
	var bare: Image = IdPhotoPicker.decode(FileAccess.get_file_as_bytes(DIR + "no_extension_6"))
	assert_eq([bare.get_width(), bare.get_height()], [16, 32])


func test_something_that_is_not_a_picture_is_refused_and_nothing_is_kept() -> void:
	assert_null(IdPhotoPicker.decode(PackedByteArray([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])))
	assert_null(IdPhotoPicker.decode(PackedByteArray()))
	var missing_path: String = _temp_file("no_such_picture")
	var junk_path: String = _temp_file("junk_photo")
	assert_eq(IdPhotoPicker.keep(missing_path), ERR_FILE_CANT_READ)
	var junk: FileAccess = FileAccess.open(junk_path, FileAccess.WRITE)
	assert_not_null(junk)
	junk.store_string("not a picture at all, just words")
	junk.close()
	assert_eq(IdPhotoPicker.keep(junk_path), ERR_FILE_UNRECOGNIZED)
	assert_false(IdPhoto.exists(), "no photo is kept from it")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(junk_path))


func test_a_picked_picture_is_kept_as_the_id_photo_its_middle_square_at_256() -> void:
	assert_eq(IdPhotoPicker.keep(DIR + "exif_6.jpg"), OK)
	assert_true(IdPhoto.exists())
	var kept: Image = IdPhoto.texture().get_image()
	assert_eq([kept.get_width(), kept.get_height()], [IdPhoto.SIZE, IdPhoto.SIZE])


func test_damaged_exif_never_breaks_a_picture() -> void:
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(DIR + "exif_6.jpg")
	for cut: int in [3, 10, 14, 20, 30]:
		assert_eq(IdPhotoPicker.exif_orientation(bytes.slice(0, cut)), 1, "cut at %d: as stored" % cut)
	assert_eq(IdPhotoPicker.exif_orientation(PackedByteArray([0xFF, 0xD8])), 1)


func test_the_gallery_tile_is_greyed_out_where_the_phone_has_no_picker() -> void:
	for has: bool in [true, false]:
		IdPhotoPicker.available_override = has
		var card: MenuDialog = ProfileSheet.build(Callable(), Callable(), Callable(), func() -> void: pass, func() -> void: pass)
		card.open(_host)
		var gallery: Button = card.find_child("Gallery", true, false) as Button
		assert_not_null(gallery, "tile 2 is always there so the avatars keep their places")
		assert_eq(gallery.disabled, not has, "gallery works when the picker is there: %s" % has)
	assert_eq(IdPhotoPicker.FILTERS, PackedStringArray(["image/*"]), "pictures only")


# ---- the avatars ---------------------------------------------------------------------------

func test_the_six_kids_have_their_avatar_picture_and_it_is_cut_from_inside_the_white_mat() -> void:
	assert_eq(Session.AVATARS.size(), 10, "all 10 Filipino Barangay kids are registered in Session.AVATARS")
	var sticker_ids: PackedStringArray = Session.AVATARS.slice(0, 6)
	for id: String in sticker_ids:
		var photo: Image = AvatarPhotos.photo_of(id)
		assert_not_null(photo, "%s has a picture" % id)
		assert_eq(photo.get_width(), 512 - AvatarPhotos.INSET * 2)
		assert_eq(photo.get_width(), photo.get_height(), "a square")
		for corner: Vector2i in [Vector2i(0, 0), Vector2i(photo.get_width() - 1, 0), Vector2i(0, photo.get_height() - 1), Vector2i(photo.get_width() - 1, photo.get_height() - 1)]:
			var c: Color = photo.get_pixel(corner.x, corner.y)
			assert_lt(minf(c.r, minf(c.g, c.b)), 0.9, "%s: no white mat left at %s" % [id, corner])


# ---- the grid: two rows of four -------------------------------------------------------------

func _open_sheet(calls: Array[String] = []) -> MenuDialog:
	IdPhotoPicker.available_override = true
	var card: MenuDialog = ProfileSheet.build(Callable(), func() -> void: calls.append("camera"), func() -> void: calls.append("removed"), func() -> void: calls.append("gallery"), func() -> void: calls.append("avatar"))
	card.open(_host)
	return card


func test_the_grid_is_two_rows_of_four_camera_gallery_then_the_six_avatars() -> void:
	var card: MenuDialog = _open_sheet()
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	assert_not_null(grid)
	assert_eq(grid.columns, 4)
	assert_eq(grid.get_child_count(), 8, "two rows of four")
	var names: Array[String] = []
	for tile: Node in grid.get_children():
		names.append(String(tile.name))
		assert_gte(minf((tile as Button).custom_minimum_size.x, (tile as Button).custom_minimum_size.y), UiMetrics.MIN_TARGET, "thumb-sized")
	var want: Array[String] = ["Camera", "Gallery"]
	for id: String in Session.AVATARS.slice(0, 6):
		want.append(id.capitalize())
	assert_eq(names, want)


func test_the_default_avatar_is_tile_3_and_the_tag_wears_it_with_no_photo() -> void:
	var card: MenuDialog = _open_sheet()
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	assert_eq(String(grid.get_child(2).name), Session.AVATARS[0].capitalize(), "tile 3 is the first avatar")
	assert_false(IdPhoto.exists())
	assert_eq(AvatarPhotos.current(), Session.AVATARS[0])
	for i: int in grid.get_child_count():
		if i >= 2:
			assert_eq((grid.get_child(i).get_node("Ring") as Control).visible, i == 2, "only tile 3 is ringed")
	var tag: NameTag = card.find_child("Preview", true, false) as NameTag
	assert_true(tag.photo.visible, "the tag shows a picture, not the empty placeholder")
	assert_false(tag.add_photo.visible)
	assert_eq((card.find_child("RemovePhoto", true, false) as Control).visible, false, "nothing to remove")


func test_tapping_an_avatar_keeps_it_rings_it_and_leaves_the_sheet_open() -> void:
	var calls: Array[String] = []
	var card: MenuDialog = _open_sheet(calls)
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	(grid.get_child(4) as Button).pressed.emit()
	assert_true(IdPhoto.exists(), "the avatar is now the ID photo")
	var kept: Image = IdPhoto.texture().get_image()
	assert_eq([kept.get_width(), kept.get_height()], [IdPhoto.SIZE, IdPhoto.SIZE])
	assert_eq(AvatarPhotos.current(), Session.AVATARS[2])
	for i: int in range(2, 8):
		assert_eq((grid.get_child(i).get_node("Ring") as Control).visible, i == 4, "tile %d" % (i + 1))
	assert_true(card.is_open, "the sheet stays so the name can still be typed")
	assert_eq(calls, ["avatar"] as Array[String])
	assert_true((card.find_child("RemovePhoto", true, false) as Control).visible, "a kept photo can be removed")


func test_remove_photo_goes_back_to_tile_3() -> void:
	var calls: Array[String] = []
	var card: MenuDialog = _open_sheet(calls)
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	(grid.get_child(6) as Button).pressed.emit()
	assert_eq(AvatarPhotos.current(), Session.AVATARS[4])
	(card.find_child("RemovePhoto", true, false) as Button).pressed.emit()
	assert_false(IdPhoto.exists())
	assert_eq(AvatarPhotos.current(), Session.AVATARS[0])
	assert_true((grid.get_child(2).get_node("Ring") as Control).visible, "tile 3 is ringed again")
	assert_false((grid.get_child(6).get_node("Ring") as Control).visible)
	assert_eq(calls, ["avatar", "removed"] as Array[String])


func test_a_photo_of_your_own_rings_no_tile() -> void:
	var face: Image = Image.create(64, 64, false, Image.FORMAT_RGB8)
	face.fill(Color.ORANGE)
	IdPhoto.save(face)
	assert_eq(AvatarPhotos.current(), "")
	var card: MenuDialog = _open_sheet()
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	for i: int in range(2, 8):
		assert_false((grid.get_child(i).get_node("Ring") as Control).visible)


func test_camera_and_gallery_tiles_close_the_sheet_and_hand_over() -> void:
	var calls: Array[String] = []
	var card: MenuDialog = _open_sheet(calls)
	var grid: GridContainer = card.find_child("PhotoOptions", true, false) as GridContainer
	var camera: Button = grid.get_child(0) as Button
	var gallery: Button = grid.get_child(1) as Button
	assert_eq(String(camera.name), "Camera")
	assert_eq(String(gallery.name), "Gallery")
	if not camera.disabled:
		camera.pressed.emit()
	gallery.pressed.emit()
	assert_true(calls.has("gallery"))
	assert_eq(calls.has("camera"), IdPhoto.camera_possible())


func test_without_callbacks_the_sheet_has_no_grid() -> void:
	var card: MenuDialog = ProfileSheet.build(Callable())
	card.open(_host)
	assert_null(card.find_child("PhotoOptions", true, false))


# ---- the name field says it can be tapped -----------------------------------------------------

func test_the_idle_name_field_has_a_grey_outline_and_a_light_tint_and_focus_turns_blue() -> void:
	var card: MenuDialog = ProfileSheet.build(Callable())
	card.open(_host)
	var field: LineEdit = card.find_child("NameField", true, false) as LineEdit
	var idle: StyleBoxFlat = field.get_theme_stylebox("normal") as StyleBoxFlat
	assert_gt(idle.border_color.a, 0.0, "a visible outline")
	assert_almost_eq(idle.border_color.r, idle.border_color.g, 0.25, "grey, not a colour")
	assert_gt(idle.bg_color.a, 0.0, "a tint")
	assert_lt(idle.bg_color.a, 0.3, "a light one")
	assert_gt(idle.border_width_left, 0)
	var focused: StyleBoxFlat = field.get_theme_stylebox("focus") as StyleBoxFlat
	assert_eq(focused.border_color, KalyeahTheme.BLUE, "active: blue")
	assert_eq(focused.bg_color.a, 0.0, "active: no tint")
