extends GutTest
## The ID photo on the name tag (owner, option A: a school ID): taken with the camera after Kalyeah says why, kept only on this phone,
## and every way out of the camera sheet (no, no camera, the app in the background) ends somewhere clear with the camera off.


## The camera, without a camera: it answers as told.
class FakeCamera extends IdCamera:
	var allowed: bool = false
	var answer: bool = true
	var cameras: bool = true
	var opened: int = 0
	var closed: int = 0
	var settings_opened: bool = false

	func has_permission() -> bool:
		return allowed

	func request_permission(_tree: SceneTree) -> void:
		allowed = answer
		permission_answered.emit(answer)

	func find(_tree: SceneTree) -> void:
		feeds_found.emit(cameras)

	func open(_lens: CanvasItem) -> bool:
		opened += 1
		return cameras

	func has_picture() -> bool:
		return opened > closed

	func close() -> void:
		closed += 1

	func capture(_lens: SubViewport) -> Image:
		var image: Image = Image.create(320, 240, false, Image.FORMAT_RGB8)
		image.fill(Color(0.8, 0.5, 0.3))
		return image

	func can_open_settings() -> bool:
		return true

	func open_settings() -> bool:
		settings_opened = true
		return true


var _saved_path: String
var _host: Control


func before_each() -> void:
	_saved_path = IdPhoto.path
	IdPhoto.path = "user://id_photo_test.png"
	IdPhoto.remove()
	_host = autofree(Control.new()) as Control
	_host.size = Vector2(720.0, 1280.0)
	add_child(_host)


func after_each() -> void:
	IdPhoto.remove()
	IdPhoto.path = _saved_path


func _sheet(camera: FakeCamera, on_saved: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = IdCameraSheet.build(on_saved, camera)
	card.open(_host)
	return card


func _view(card: MenuDialog) -> IdCameraSheet:
	return card.body.get_node("IdCamera") as IdCameraSheet


func _press(view: IdCameraSheet, button: String) -> void:
	(view.buttons.get_node(button) as Button).pressed.emit()


func _labels(view: IdCameraSheet) -> Array[String]:
	var found: Array[String] = []
	for child: Node in view.buttons.get_children():
		if not child.is_queued_for_deletion():
			found.append((child as Button).text)
	return found


# ---- the photo itself ----------------------------------------------------------------

func test_the_photo_is_the_middle_square_kept_on_this_phone() -> void:
	assert_false(IdPhoto.exists())
	var wide: Image = Image.create(400, 200, false, Image.FORMAT_RGB8)
	wide.fill(Color.BLUE)
	wide.fill_rect(Rect2i(100, 0, 200, 200), Color.RED)
	assert_eq(IdPhoto.save(wide), OK)
	assert_true(IdPhoto.path.begins_with("user://"), "on this phone only")
	var kept: Texture2D = IdPhoto.texture()
	assert_eq(kept.get_size(), Vector2(IdPhoto.SIZE, IdPhoto.SIZE))
	assert_almost_eq(kept.get_image().get_pixel(4, 4).r, 1.0, 0.05, "the middle square, not squashed")
	IdPhoto.remove()
	assert_null(IdPhoto.texture())


func test_the_photo_never_goes_into_the_profile_or_the_network_code() -> void:
	for folder: String in ["res://scripts/net", "res://scripts/autoload"]:
		for file: String in DirAccess.get_files_at(folder):
			if file.ends_with(".gd"):
				var text: String = FileAccess.get_file_as_string("%s/%s" % [folder, file])
				assert_false(text.contains("IdPhoto"), "%s does not touch the photo" % file)


# ---- the camera sheet ----------------------------------------------------------------

func test_kalyeah_says_why_before_android_asks() -> void:
	var view: IdCameraSheet = _view(_sheet(FakeCamera.new()))
	assert_eq(view.step, IdCameraSheet.Step.ASK)
	assert_eq(view.message.text, IdCameraSheet.WHY)
	assert_eq(_labels(view), ["Allow camera"] as Array[String])
	assert_false(view.frame.visible, "no camera before the OK")


func test_allow_snap_and_use_keeps_the_photo_and_stops_the_camera() -> void:
	var camera: FakeCamera = FakeCamera.new()
	var saved: Array[bool] = [false]
	var card: MenuDialog = _sheet(camera, func() -> void: saved[0] = true)
	var view: IdCameraSheet = _view(card)
	_press(view, "Allowcamera")
	assert_eq(view.step, IdCameraSheet.Step.LOOKING)
	assert_true(view.frame.visible)
	assert_eq(camera.opened, 1)
	_press(view, "Snap")
	assert_eq(view.step, IdCameraSheet.Step.SNAPPED)
	assert_gt(camera.closed, 0, "the camera is off once the photo is taken")
	assert_eq(_labels(view), ["Retake", "Use"] as Array[String])
	_press(view, "Use")
	assert_true(IdPhoto.exists())
	assert_true(saved[0])
	assert_false(card.is_open)


func test_retake_opens_the_camera_again() -> void:
	var camera: FakeCamera = FakeCamera.new()
	camera.allowed = true
	var view: IdCameraSheet = _view(_sheet(camera))
	assert_eq(view.step, IdCameraSheet.Step.LOOKING, "already allowed: straight to the camera")
	_press(view, "Snap")
	_press(view, "Retake")
	assert_eq(view.step, IdCameraSheet.Step.LOOKING)
	assert_eq(camera.opened, 2)
	assert_false(IdPhoto.exists(), "nothing kept until Use")


func test_saying_no_explains_and_offers_the_settings() -> void:
	var camera: FakeCamera = FakeCamera.new()
	camera.answer = false
	var view: IdCameraSheet = _view(_sheet(camera))
	_press(view, "Allowcamera")
	assert_eq(view.step, IdCameraSheet.Step.DENIED)
	assert_eq(view.message.text, IdCameraSheet.DENIED_TEXT)
	assert_eq(camera.opened, 0)
	_press(view, "Opensettings")
	assert_true(camera.settings_opened)


func test_a_phone_with_no_camera_says_so() -> void:
	var camera: FakeCamera = FakeCamera.new()
	camera.allowed = true
	camera.cameras = false
	var view: IdCameraSheet = _view(_sheet(camera))
	assert_eq(view.step, IdCameraSheet.Step.NO_CAMERA)
	assert_eq(_labels(view), [] as Array[String])


func test_the_camera_is_off_in_the_background_and_when_the_sheet_closes() -> void:
	var camera: FakeCamera = FakeCamera.new()
	camera.allowed = true
	var card: MenuDialog = _sheet(camera)
	var view: IdCameraSheet = _view(card)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	assert_eq(camera.closed, 1)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	assert_eq(camera.opened, 2, "back on when the app returns")
	card.close_button.pressed.emit()
	assert_gt(camera.closed, 1)
	assert_false(card.is_open)


# ---- the name tag and the profile ------------------------------------------------------

func test_the_tag_shows_the_photo_left_and_the_words_beside_it() -> void:
	var tag: NameTag = autofree(NameTag.new()) as NameTag
	_host.add_child(tag)
	assert_true(tag.photo_frame.visible, "the square is always there")
	assert_true(tag.logomark.visible, "the K! logomark until there is a photo")
	assert_eq(tag.logomark.texture, NameTag.LOGOMARK)
	var face: Image = Image.create(64, 64, false, Image.FORMAT_RGB8)
	face.fill(Color.ORANGE)
	IdPhoto.save(face)
	tag.refresh_photo()
	assert_true(tag.photo_frame.visible)
	assert_true(tag.photo.visible)
	assert_false(tag.logomark.visible)
	assert_gte(tag.card.size.x, NameTag.PHOTO_MIN_WIDTH)
	assert_gt(tag.name_label.position.x, tag.photo_frame.position.x + tag.photo_frame.size.x, "the name is beside the photo")
	assert_lte(tag.card.size.y, NameTag.CARD_HEIGHT, "the same height as before: Home keeps its layout")


func test_the_profile_takes_and_removes_the_photo() -> void:
	var asked: Array[bool] = [false]
	var removed: Array[bool] = [false]
	var card: MenuDialog = ProfileSheet.build(Callable(), func() -> void: asked[0] = true, func() -> void: removed[0] = true)
	card.open(_host)
	if not IdPhoto.camera_possible():
		pass_test("no camera on this machine: no photo row")
		return
	(card.find_child("TakePhoto", true, false) as Button).pressed.emit()
	assert_true(asked[0])
	var face: Image = Image.create(64, 64, false, Image.FORMAT_RGB8)
	IdPhoto.save(face)
	var again: MenuDialog = ProfileSheet.build(Callable(), func() -> void: pass, func() -> void: removed[0] = true)
	again.open(_host)
	assert_eq((again.find_child("TakePhoto", true, false) as Button).accessibility_name, "New photo")
	(again.find_child("RemovePhoto", true, false) as Button).pressed.emit()
	assert_false(IdPhoto.exists())
	assert_true(removed[0])


# ---- the camera's turn ---------------------------------------------------------------

## The square's corners land on the picture's four corners (not off it): no row stretched into stripes, at every turn a phone gives.
func test_every_turn_a_phone_gives_maps_the_square_onto_the_whole_picture() -> void:
	var corners: Array[Vector2] = [Vector2(0, 0), Vector2(1, 0), Vector2(0, 1), Vector2(1, 1)]
	for degrees: int in [0, 90, 180, 270]:
		# what camera_android.cpp sets: a bare rotation about the picture's corner
		var turn: Transform2D = Transform2D().rotated(deg_to_rad(degrees))
		for mirror: bool in [false, true]:
			var seen: Array[Vector2] = []
			for corner: Vector2 in corners:
				var point: Vector2 = IdCamera.screen_to_picture(corner, turn, mirror)
				point = Vector2(snappedf(point.x, 0.001), snappedf(point.y, 0.001))
				assert_true(point in corners, "%d°%s: corner %s lands on a corner of the picture, not %s" % [degrees, " mirrored" if mirror else "", corner, point])
				seen.append(point)
			assert_eq(seen.size(), 4)
			for corner: Vector2 in corners:
				assert_true(corner in seen, "%d°: the whole picture is used" % degrees)


func test_the_desktop_flip_reads_the_picture_as_before() -> void:
	var flip: Transform2D = Transform2D(Vector2(1, 0), Vector2(0, -1), Vector2(0, 1))  # CameraFeed's default
	assert_eq(IdCamera.screen_to_picture(Vector2(0.25, 0.1), flip, false), Vector2(0.25, 0.9), "upside down, as the feed's flip asks")
	assert_eq(IdCamera.screen_to_picture(Vector2(0.5, 0.5), Transform2D().rotated(deg_to_rad(270)), true), Vector2(0.5, 0.5), "the middle stays the middle")


func test_a_quarter_turn_reads_a_screen_row_down_a_picture_column() -> void:
	var turn: Transform2D = Transform2D().rotated(deg_to_rad(90))
	var left: Vector2 = IdCamera.screen_to_picture(Vector2(0.0, 0.5), turn, false)
	var right: Vector2 = IdCamera.screen_to_picture(Vector2(1.0, 0.5), turn, false)
	assert_almost_eq(left.x, right.x, 0.001, "a screen row is one picture column")
	assert_almost_eq(absf(left.y - right.y), 1.0, 0.001, "and it spans the whole of it")
