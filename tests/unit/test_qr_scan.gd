extends GutTest
## Joining an Online room by its QR code: the host's room shows the code as a QR, the Join screen's Scan QR reads it with the
## back camera and joins. Every way out of the scanner (no, no camera, the app in the background) ends somewhere clear.

const QR_PICTURE: GDScript = preload("res://tests/fixtures/qr_picture.gd")


## The camera, without a camera: it "sees" `picture`.
class FakeCamera extends IdCamera:
	var allowed: bool = true
	var answer: bool = true
	var cameras: bool = true
	var opened: int = 0
	var closed: int = 0
	var picture: Image

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
		return picture

	func can_open_settings() -> bool:
		return true


var _host: Control


func before_each() -> void:
	_host = autofree(Control.new()) as Control
	_host.size = Vector2(1280.0, 720.0)
	add_child(_host)


func _seeing(text: String) -> FakeCamera:
	var camera: FakeCamera = FakeCamera.new()
	var blank: Image = Image.create(QrScanSheet.LENS_SIDE, QrScanSheet.LENS_SIDE, false, Image.FORMAT_RGB8)
	blank.fill(Color(0.6, 0.6, 0.6))
	camera.picture = blank if text.is_empty() else QR_PICTURE.draw(QrCode.encode(text), QrScanSheet.LENS_SIDE, 0.55, 6.0)
	return camera


func _sheet(camera: FakeCamera, on_code: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = QrScanSheet.build(on_code, camera)
	card.open(_host)
	return card


func _view(card: MenuDialog) -> QrScanSheet:
	return card.body.get_node("QrScan") as QrScanSheet


func test_the_scanner_uses_the_back_camera() -> void:
	var camera: FakeCamera = _seeing("")
	_sheet(camera)
	assert_true(camera.prefer_back, "a QR is read with the back camera, not the selfie one")


func test_a_room_qr_is_read_and_handed_on_with_the_camera_off() -> void:
	var got: Array[String] = []
	var camera: FakeCamera = _seeing(RoomQr.payload("HJK23MNP"))
	var card: MenuDialog = _sheet(camera, func(code: String) -> void: got.append(code))
	var view: QrScanSheet = _view(card)
	assert_eq(view.step, QrScanSheet.Step.LOOKING, "already allowed: straight to the camera")
	assert_true(view.read_now())
	assert_eq(got, ["HJK23MNP"] as Array[String])
	assert_false(card.is_open, "the sheet closes")
	assert_gt(camera.closed, 0, "and the camera is off")


func test_the_camera_is_read_by_itself_a_few_times_a_second() -> void:
	var got: Array[String] = []
	var card: MenuDialog = _sheet(_seeing(RoomQr.payload("ABCD2345")), func(code: String) -> void: got.append(code))
	_view(card)._process(QrScanSheet.READ_EVERY + 0.01)
	assert_eq(got, ["ABCD2345"] as Array[String])


func test_another_qr_code_is_not_taken_for_a_room() -> void:
	var got: Array[String] = []
	var card: MenuDialog = _sheet(_seeing("https://example.com"), func(code: String) -> void: got.append(code))
	var view: QrScanSheet = _view(card)
	assert_false(view.read_now())
	assert_eq(view.message.text, QrScanSheet.NOT_A_ROOM_TEXT)
	assert_true(card.is_open, "keeps looking")
	assert_true(got.is_empty())


func test_nothing_seen_keeps_looking() -> void:
	var card: MenuDialog = _sheet(_seeing(""))
	assert_false(_view(card).read_now())
	assert_eq(_view(card).message.text, QrScanSheet.LOOKING_TEXT)


func test_asks_first_and_saying_no_ends_clearly() -> void:
	var camera: FakeCamera = _seeing("")
	camera.allowed = false
	camera.answer = false
	var view: QrScanSheet = _view(_sheet(camera))
	assert_eq(view.step, QrScanSheet.Step.ASK)
	assert_eq(view.message.text, QrScanSheet.WHY)
	(view.buttons.get_node("Allowcamera") as Button).pressed.emit()
	assert_eq(view.step, QrScanSheet.Step.DENIED)
	assert_not_null(view.buttons.get_node_or_null("Opensettings"))
	assert_false(view.read_now())


func test_a_phone_with_no_camera_says_so() -> void:
	var camera: FakeCamera = _seeing("")
	camera.cameras = false
	var view: QrScanSheet = _view(_sheet(camera))
	assert_eq(view.step, QrScanSheet.Step.NO_CAMERA)
	assert_gt(camera.closed, 0)


func test_the_camera_is_off_in_the_background_and_on_again_after() -> void:
	var camera: FakeCamera = _seeing("")
	var view: QrScanSheet = _view(_sheet(camera))
	var closed: int = camera.closed
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	assert_eq(camera.closed, closed + 1)
	var opened: int = camera.opened
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	assert_eq(camera.opened, opened + 1)
	assert_eq(view.step, QrScanSheet.Step.LOOKING)


func test_closing_the_sheet_turns_the_camera_off() -> void:
	var camera: FakeCamera = _seeing("")
	var card: MenuDialog = _sheet(camera)
	var closed: int = camera.closed
	card.close(false)
	assert_gt(camera.closed, closed)


func test_the_room_qr_dialog_shows_the_code_big_and_crisp() -> void:
	var room: Dictionary = {"code": "ABC234XY", "host": 1, "team_size": 1, "first_to": 5, "members": [], "can_start_host": false}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	_host.add_child(view)
	view.show_room(room, 1)
	assert_null(view.show_qr(), "Hotspot rooms have no code to show")
	view.set_online(true)
	view.show_room(room, 1)
	assert_true(view.qr_button.is_visible_in_tree(), "in the top row, Online only")
	var card: MenuDialog = view.show_qr()
	var qr: QrView = card.body.get_node("QrView") as QrView
	assert_eq(qr.code.version, 1)
	assert_true(qr.custom_minimum_size.x >= 29 * 12, "12 px a module in the 1280x720 landscape design")
	# what the dialog draws reads back as this room
	var n: int = qr.code.size + 8
	var bits: PackedByteArray = PackedByteArray()
	bits.resize(n * n * 16)
	for y: int in n * 4:
		for x: int in n * 4:
			var mx: int = x / 4 - 4
			var my: int = y / 4 - 4
			if mx >= 0 and my >= 0 and mx < qr.code.size and my < qr.code.size and qr.code.is_dark(mx, my):
				bits[y * n * 4 + x] = 1
	assert_eq(RoomQr.code_from(QrReader.read_bits(bits, n * 4, n * 4)), "ABC234XY")
	card.close(false)
