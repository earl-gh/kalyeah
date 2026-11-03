extends GutTest
## The nametag's three ways to a photo (owner, 2026-11-03): pick from the gallery, take one with the camera, pick one of the six avatars.

const DIR: String = "res://tests/fixtures/photos/"
## orientation -> [width, height, then the colour at the top left, top right, bottom left and bottom right of the picture a viewer shows]
## (worked out with an independent library from one stored picture: red / green over blue / yellow, 32 x 16)
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


func before_each() -> void:
	_host = autofree(Control.new()) as Control
	_host.size = Vector2(720.0, 1280.0)
	add_child(_host)
	_path = IdPhoto.path
	IdPhoto.path = "user://photo_options_test.png"
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
	# a JPEG whose name says nothing (an Android content:// address has no extension) is still read, and turned
	var bare: Image = IdPhotoPicker.decode(FileAccess.get_file_as_bytes(DIR + "no_extension_6"))
	assert_eq([bare.get_width(), bare.get_height()], [16, 32])


func test_something_that_is_not_a_picture_is_refused_and_nothing_is_kept() -> void:
	assert_null(IdPhotoPicker.decode(PackedByteArray([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])))
	assert_null(IdPhotoPicker.decode(PackedByteArray()))
	assert_eq(IdPhotoPicker.keep("user://no_such_picture"), ERR_FILE_CANT_READ)
	var junk: FileAccess = FileAccess.open("user://junk_photo", FileAccess.WRITE)
	junk.store_string("not a picture at all, just words")
	junk.close()
	assert_eq(IdPhotoPicker.keep("user://junk_photo"), ERR_FILE_UNRECOGNIZED)
	assert_false(IdPhoto.exists(), "no photo is kept from it")
	DirAccess.remove_absolute(ProjectSettings.globalize_path("user://junk_photo"))


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


func test_the_gallery_is_offered_only_where_the_phone_has_a_picker() -> void:
	for has: bool in [true, false]:
		IdPhotoPicker.available_override = has
		var card: MenuDialog = ProfileSheet.build(Callable(), Callable(), Callable(), func() -> void: pass, func() -> void: pass)
		card.open(_host)
		assert_eq(card.find_child("Gallery", true, false) != null, has, "gallery button when the picker is there: %s" % has)
	assert_eq(IdPhotoPicker.FILTERS, PackedStringArray(["image/*"]), "pictures only")


# ---- the avatars ---------------------------------------------------------------------------

func test_the_six_kids_have_their_avatar_picture_and_it_is_cut_from_inside_the_white_mat() -> void:
	assert_eq(Session.AVATARS.size(), 6)
	for id: String in Session.AVATARS:
		var photo: Image = AvatarPickerSheet.photo_of(id)
		assert_not_null(photo, "%s has a picture" % id)
		assert_eq(photo.get_width(), 512 - AvatarPickerSheet.INSET * 2)
		assert_eq(photo.get_width(), photo.get_height(), "a square")
		for corner: Vector2i in [Vector2i(0, 0), Vector2i(photo.get_width() - 1, 0), Vector2i(0, photo.get_height() - 1), Vector2i(photo.get_width() - 1, photo.get_height() - 1)]:
			var c: Color = photo.get_pixel(corner.x, corner.y)
			assert_lt(minf(c.r, minf(c.g, c.b)), 0.9, "%s: no white mat left at %s" % [id, corner])


func test_the_avatar_picker_shows_six_pictures_in_three_columns_and_a_tap_keeps_one() -> void:
	var saved: Array[bool] = [false]
	var card: MenuDialog = AvatarPickerSheet.build(func() -> void: saved[0] = true)
	card.open(_host)
	var grid: GridContainer = card.find_child("Avatars", true, false) as GridContainer
	assert_eq(grid.columns, 3)
	assert_eq(grid.get_child_count(), 6)
	assert_false(IdPhoto.exists())
	(grid.get_child(2) as Button).pressed.emit()
	assert_true(saved[0], "the sheet says it saved")
	assert_true(IdPhoto.exists(), "the avatar is now the ID photo")
	var kept: Image = IdPhoto.texture().get_image()
	assert_eq([kept.get_width(), kept.get_height()], [IdPhoto.SIZE, IdPhoto.SIZE])
	for pick: Node in grid.get_children():
		assert_gte(minf((pick as Button).custom_minimum_size.x, (pick as Button).custom_minimum_size.y), UiMetrics.MIN_TARGET, "thumb-sized")


# ---- the nametag's row of three -----------------------------------------------------------

func test_the_nametag_has_a_row_of_options_and_each_hands_over() -> void:
	var calls: Array[String] = []
	IdPhotoPicker.available_override = true
	var card: MenuDialog = ProfileSheet.build(Callable(), func() -> void: calls.append("camera"), Callable(), func() -> void: calls.append("gallery"), func() -> void: calls.append("avatar"))
	card.open(_host)
	var row: HBoxContainer = card.find_child("PhotoOptions", true, false) as HBoxContainer
	assert_not_null(row, "the three ways sit in a row under the tag")
	var names: Array[String] = []
	for option: Node in row.get_children():
		names.append(option.name)
		assert_gte((option as Button).custom_minimum_size.y, UiMetrics.MIN_TARGET)
	var want: Array[String] = []
	if IdPhotoPicker.available():
		want.append("Gallery")
	if IdPhoto.camera_possible():
		want.append("Camera")
	want.append("Avatar")
	assert_eq(names, want, "gallery, camera, avatar (a way this phone lacks is not there)")
	for option: Node in row.get_children():
		(option as Button).pressed.emit()
	for way: String in want:
		assert_true(calls.has(way.to_lower()), "%s hands over" % way)


func test_without_callbacks_the_sheet_has_no_row() -> void:
	var card: MenuDialog = ProfileSheet.build(Callable())
	card.open(_host)
	assert_null(card.find_child("PhotoOptions", true, false))
