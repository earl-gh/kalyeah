class_name IdCameraSheet
extends VBoxContainer
## The sheet that takes the ID photo (owner). It says why before Android asks for the camera, shows the front camera mirrored like a
## selfie in the tag's square, and keeps the photo only on this phone (`IdPhoto`). Saying no, a phone with no camera and the app going
## to the background all end somewhere clear. The camera is off whenever this sheet is not showing it.

enum Step { ASK, WAITING, LOOKING, SNAPPED, DENIED, NO_CAMERA }

const WHY: String = "Smile! Kalyeah uses the camera for your ID photo. It stays on this phone."
const WAITING_TEXT: String = "Waiting for your OK…"
const LOOKING_TEXT: String = "Fit your face in the square."
const SNAPPED_TEXT: String = "Looking good! Use this photo?"
const DENIED_TEXT: String = "Kalyeah can't use the camera. You can allow it in the app's settings."
const NO_CAMERA_TEXT: String = "No camera found on this phone."
## The square the camera is shown in, on the sheet.
const FRAME_SIDE: float = 360.0

var step: Step = Step.ASK
var message: Label
var frame: PanelContainer
var preview: TextureRect
var buttons: HBoxContainer
## The camera's picture, drawn by `IdCamera.SHADER` at the photo's size; the preview shows it and Snap reads it.
var lens: SubViewport
var lens_rect: ColorRect
var camera: IdCamera
var taken: Image

var _dialog: MenuDialog
var _on_saved: Callable


## The sheet in a dialog. `on_saved` runs after Use, once the photo is kept. `with_camera` replaces the real camera (tests).
static func build(on_saved: Callable, with_camera: IdCamera = null) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Your ID photo", MenuDialog.WIDTH_SHEET)
	card.name = "IdCameraSheet"
	var sheet: IdCameraSheet = IdCameraSheet.new()
	sheet.camera = with_camera if with_camera != null else IdCamera.new()
	sheet._dialog = card
	sheet._on_saved = on_saved
	card.body.add_child(sheet)
	card.closed.connect(sheet.stop)
	return card


func _init() -> void:
	name = "IdCamera"
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
	edge.bg_color = Color("dfe8f7")
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
	lens.size = Vector2i(IdPhoto.SIZE, IdPhoto.SIZE)
	lens.render_target_update_mode = SubViewport.UPDATE_DISABLED
	lens.disable_3d = true
	add_child(lens)
	lens_rect = ColorRect.new()
	lens_rect.size = Vector2(IdPhoto.SIZE, IdPhoto.SIZE)
	lens.add_child(lens_rect)
	buttons = HBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	add_child(buttons)


func _ready() -> void:
	begin()


## Starts where the player is: straight to the camera when it is already allowed, else the reason first.
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
	frame.visible = next == Step.LOOKING or next == Step.SNAPPED
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
			_button("Snap!", "orange", snap)
		Step.SNAPPED:
			message.text = SNAPPED_TEXT
			_button("Retake", "grey", retake)
			_button("Use", "orange", use)
		Step.DENIED:
			message.text = DENIED_TEXT
			if camera.can_open_settings():
				_button("Open settings", "blue", camera.open_settings)
		Step.NO_CAMERA:
			message.text = NO_CAMERA_TEXT


func _button(text: String, color: String, action: Callable) -> Button:
	var button: Button = MenuDialog.make_button(text, color, UiMetrics.BODY)
	button.name = text.replace("!", "").replace(" ", "")
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


## Keeps what the camera shows now and stops the camera (nothing happens before the first picture arrives).
func snap() -> void:
	if step != Step.LOOKING or not camera.has_picture():
		return
	var picture: Image = camera.capture(lens)
	if picture == null or picture.is_empty():
		return
	taken = picture
	camera.close()
	_show(Step.SNAPPED)
	preview.texture = ImageTexture.create_from_image(taken)


func retake() -> void:
	taken = null
	_look()


## Keeps the photo on this phone and closes.
func use() -> void:
	if taken == null or IdPhoto.save(taken) != OK:
		return
	close()
	if _on_saved.is_valid():
		_on_saved.call()


func close() -> void:
	stop()
	if _dialog != null and is_instance_valid(_dialog) and _dialog.is_open:
		_dialog.close()


func stop() -> void:
	camera.close()
	lens.render_target_update_mode = SubViewport.UPDATE_DISABLED


## The camera is off while the app is in the background, and back on when it returns to the preview.
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
