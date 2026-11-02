class_name QrScanSheet
extends VBoxContainer
## Join an Online room by scanning the host's QR code (`RoomQr`) instead of typing it. Says why before Android asks for the camera,
## shows the back camera in a square and reads it a few times a second (`QrReader`); a Kalyeah room's code closes the sheet and
## joins. Saying no, a phone with no camera and the app going to the background end somewhere clear; the camera is off whenever
## this sheet is not showing it. Nothing the camera sees is kept.

enum Step { ASK, WAITING, LOOKING, DENIED, NO_CAMERA }

const WHY: String = "Point the camera at the room's QR code on your friend's phone. Nothing is saved."
const WAITING_TEXT: String = "Waiting for your OK…"
const LOOKING_TEXT: String = "Fit the QR code in the square."
const NOT_A_ROOM_TEXT: String = "That QR code isn't a Kalyeah room. Scan the one on the host's room screen."
const DENIED_TEXT: String = "Kalyeah can't use the camera. You can allow it in the app's settings, or type the code."
const NO_CAMERA_TEXT: String = "No camera found on this phone. Type the room code instead."
const FRAME_SIDE: float = 440.0
## The camera's picture is read at this side, in pixels: plenty for a room's 21 x 21 code filling part of the square.
const LENS_SIDE: int = 320
## Seconds between reads: often enough to feel instant, rarely enough for a budget phone.
const READ_EVERY: float = 0.3

var step: Step = Step.ASK
var message: Label
var frame: PanelContainer
var preview: TextureRect
var buttons: HBoxContainer
var lens: SubViewport
var lens_rect: ColorRect
var camera: IdCamera
## The room code read, once one is.
var found_code: String = ""

var _dialog: MenuDialog
var _on_code: Callable
var _wait: float = 0.0


## The sheet in a dialog. `on_code` gets the room code once it is read (the sheet has closed by then). `with_camera` replaces the
## real camera (tests).
static func build(on_code: Callable, with_camera: IdCamera = null) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Scan the room QR", MenuDialog.WIDTH_SHEET)
	card.name = "QrScanSheet"
	var sheet: QrScanSheet = QrScanSheet.new()
	sheet.camera = with_camera if with_camera != null else IdCamera.new()
	sheet.camera.prefer_back = true
	sheet._dialog = card
	sheet._on_code = on_code
	card.body.add_child(sheet)
	card.closed.connect(sheet.stop)
	return card


func _init() -> void:
	name = "QrScan"
	add_theme_constant_override("separation", UiMetrics.GAP)
	message = Label.new()
	message.name = "Message"
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	message.add_theme_font_size_override("font_size", UiMetrics.BODY)
	message.add_theme_color_override("font_color", KalyeahTheme.MODAL_TEXT)
	message.add_theme_constant_override("outline_size", 0)
	add_child(message)
	frame = PanelContainer.new()
	frame.name = "Frame"
	frame.custom_minimum_size = Vector2(FRAME_SIDE, FRAME_SIDE)
	frame.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	var edge: StyleBoxFlat = StyleBoxFlat.new()
	edge.bg_color = Color("1b1f2a")
	edge.border_color = NameTag.CARD_EDGE
	edge.set_border_width_all(4)
	edge.set_corner_radius_all(UiSkin.RADIUS_M)
	edge.anti_aliasing = true
	frame.add_theme_stylebox_override("panel", edge)
	add_child(frame)
	preview = TextureRect.new()
	preview.name = "Preview"
	preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	frame.add_child(preview)
	lens = SubViewport.new()
	lens.name = "Lens"
	lens.size = Vector2i(LENS_SIDE, LENS_SIDE)
	lens.render_target_update_mode = SubViewport.UPDATE_DISABLED
	lens.disable_3d = true
	add_child(lens)
	lens_rect = ColorRect.new()
	lens_rect.size = Vector2(LENS_SIDE, LENS_SIDE)
	lens.add_child(lens_rect)
	buttons = HBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	add_child(buttons)


func _ready() -> void:
	begin()


## Straight to the camera when it is already allowed, else the reason first.
func begin() -> void:
	if camera.has_permission():
		_look()
	else:
		_show(Step.ASK)


func _show(next: Step) -> void:
	step = next
	for child: Node in buttons.get_children():
		buttons.remove_child(child)
		child.queue_free()
	frame.visible = next == Step.LOOKING
	lens.render_target_update_mode = SubViewport.UPDATE_ALWAYS if next == Step.LOOKING else SubViewport.UPDATE_DISABLED
	match next:
		Step.ASK:
			message.text = WHY
			_button("Allow camera", "orange", _ask)
		Step.WAITING:
			message.text = WAITING_TEXT
		Step.LOOKING:
			message.text = LOOKING_TEXT
			preview.texture = lens.get_texture()
			_wait = READ_EVERY
		Step.DENIED:
			message.text = DENIED_TEXT
			if camera.can_open_settings():
				_button("Open settings", "blue", camera.open_settings)
		Step.NO_CAMERA:
			message.text = NO_CAMERA_TEXT


func _button(text: String, color: String, action: Callable) -> Button:
	var button: Button = MenuDialog.make_button(text, color, UiMetrics.BODY)
	button.name = text.replace(" ", "")
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.pressed.connect(func() -> void: action.call())
	buttons.add_child(button)
	return button


func _ask() -> void:
	_show(Step.WAITING)
	if not camera.permission_answered.is_connected(_on_permission):
		camera.permission_answered.connect(_on_permission, CONNECT_ONE_SHOT)
	camera.request_permission(get_tree())


func _on_permission(granted: bool) -> void:
	if step != Step.WAITING:
		return
	if granted:
		_look()
	else:
		_show(Step.DENIED)


func _look() -> void:
	_show(Step.WAITING)
	if not camera.feeds_found.is_connected(_on_feeds):
		camera.feeds_found.connect(_on_feeds, CONNECT_ONE_SHOT)
	camera.find(get_tree())


func _on_feeds(found: bool) -> void:
	if step != Step.WAITING:
		return
	if found and camera.open(lens_rect):
		_show(Step.LOOKING)
	else:
		camera.close()
		_show(Step.NO_CAMERA)


func _process(delta: float) -> void:
	if step != Step.LOOKING:
		return
	_wait -= delta
	if _wait <= 0.0:
		_wait = READ_EVERY
		read_now()


## Reads what the camera shows now: a room's code closes the sheet and hands the code on. True when it did.
func read_now() -> bool:
	if step != Step.LOOKING or not camera.has_picture():
		return false
	var text: String = QrReader.read(camera.capture(lens))
	if text.is_empty():
		return false
	var code: String = RoomQr.code_from(text)
	if code.is_empty():
		message.text = NOT_A_ROOM_TEXT
		return false
	found_code = code
	close()
	if _on_code.is_valid():
		_on_code.call(code)
	return true


func close() -> void:
	stop()
	if _dialog != null and is_instance_valid(_dialog) and _dialog.is_open:
		_dialog.close()


func stop() -> void:
	if step == Step.LOOKING:
		step = Step.WAITING
	camera.close()
	lens.render_target_update_mode = SubViewport.UPDATE_DISABLED


## The camera is off while the app is in the background, and back on when it returns.
func _notification(what: int) -> void:
	match what:
		NOTIFICATION_APPLICATION_PAUSED:
			if step == Step.LOOKING:
				camera.close()
		NOTIFICATION_APPLICATION_RESUMED:
			if step == Step.LOOKING:
				_look()
		NOTIFICATION_EXIT_TREE:
			camera.close()
