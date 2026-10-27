class_name IdCamera
extends RefCounted
## The phone's camera for the ID photo, through Godot's `CameraServer` (4.6 reads Android's cameras itself: no plugin). Everything that
## touches the real camera or Android is here, so `IdCameraSheet` can be tested with a stand-in that overrides these methods.

## Android answered the camera prompt (`granted`), or there was nothing to ask.
signal permission_answered(granted: bool)
## The cameras were looked for: `found` is false when this phone has none (or none showed up in `FIND_SECONDS`).
signal feeds_found(found: bool)

const PERMISSION: String = "android.permission.CAMERA"
const SHADER: Shader = preload("res://shaders/id_camera.gdshader")
## How long to wait for the cameras to be listed before saying there is none.
const FIND_SECONDS: float = 3.0
## The smallest side the camera's picture should have: the photo is 256 px, so this is plenty and keeps the frames light.
const MIN_SIDE: int = 480

var feed: CameraFeed
var _material: ShaderMaterial
var _tree: SceneTree


## Off Android there is no prompt to pass (the computer asks by itself, if it asks at all).
func has_permission() -> bool:
	if OS.get_name() != "Android":
		return true
	return OS.get_granted_permissions().has(PERMISSION)


## Asks Android for the camera with its own prompt; `permission_answered` follows. A player who chose "Don't ask again" is answered
## at once with false.
func request_permission(tree: SceneTree) -> void:
	_tree = tree
	if has_permission():
		permission_answered.emit.call_deferred(true)
		return
	if not tree.on_request_permissions_result.is_connected(_on_permission_result):
		tree.on_request_permissions_result.connect(_on_permission_result)
	if OS.request_permission("CAMERA"):
		permission_answered.emit.call_deferred(true)


func _on_permission_result(permission: String, granted: bool) -> void:
	if not permission.ends_with("CAMERA"):
		return
	if _tree != null and _tree.on_request_permissions_result.is_connected(_on_permission_result):
		_tree.on_request_permissions_result.disconnect(_on_permission_result)
	permission_answered.emit(granted)


## Lists the cameras; `feeds_found` follows.
func find(tree: SceneTree) -> void:
	_tree = tree
	CameraServer.monitoring_feeds = true
	if CameraServer.get_feed_count() > 0:
		feeds_found.emit.call_deferred(true)
		return
	var done: Array[bool] = [false]
	var answer: Callable = func(found: bool) -> void:
		if done[0]:
			return
		done[0] = true
		feeds_found.emit(found)
	CameraServer.camera_feeds_updated.connect(func() -> void: answer.call(CameraServer.get_feed_count() > 0), CONNECT_ONE_SHOT)
	tree.create_timer(FIND_SECONDS).timeout.connect(func() -> void: answer.call(CameraServer.get_feed_count() > 0))


## Starts the front camera (else the first one) and draws it into `lens` (the `SHADER` on a rect). False when it would not start.
func open(lens: CanvasItem) -> bool:
	feed = null
	for index: int in CameraServer.get_feed_count():
		var candidate: CameraFeed = CameraServer.get_feed(index)
		if feed == null or candidate.get_position() == CameraFeed.FEED_FRONT:
			feed = candidate
		if candidate.get_position() == CameraFeed.FEED_FRONT:
			break
	if feed == null:
		return false
	var format: int = _format(feed.get_formats())
	if format >= 0 and not feed.set_format(format, {}):
		return false
	_material = ShaderMaterial.new()
	_material.shader = SHADER
	for which: int in 2:
		var texture: CameraTexture = CameraTexture.new()
		texture.camera_feed_id = feed.get_id()
		texture.which_feed = which as CameraServer.FeedImage
		_material.set_shader_parameter("luma" if which == 0 else "chroma", texture)
	_material.set_shader_parameter("mirror", feed.get_position() == CameraFeed.FEED_FRONT)
	lens.material = _material
	if not feed.frame_changed.is_connected(_on_frame):
		feed.frame_changed.connect(_on_frame)
	feed.feed_is_active = true
	return feed.feed_is_active


## The format whose shorter side is at least `MIN_SIDE` with the fewest pixels (else the biggest), or -1 when the feed lists none.
static func _format(formats: Array) -> int:
	var best: int = -1
	var best_pixels: int = 0
	var biggest: int = -1
	var biggest_pixels: int = 0
	for index: int in formats.size():
		var entry: Dictionary = formats[index] as Dictionary
		var width: int = entry.get("width", 0) as int
		var height: int = entry.get("height", 0) as int
		var pixels: int = width * height
		if mini(width, height) >= MIN_SIDE and (best < 0 or pixels < best_pixels):
			best = index
			best_pixels = pixels
		if pixels > biggest_pixels:
			biggest = index
			biggest_pixels = pixels
	return best if best >= 0 else biggest


## Each frame: the picture's kind (RGB or two planes), its turn on this phone and its size, which can change while it runs.
func _on_frame() -> void:
	if feed == null or _material == null:
		return
	_material.set_shader_parameter("separate", feed.get_datatype() == CameraFeed.FEED_YCBCR_SEP)
	var back: Transform2D = picture_turn(feed.feed_transform)
	_material.set_shader_parameter("turn_x", back.x)
	_material.set_shader_parameter("turn_y", back.y)
	var luma: Texture2D = _material.get_shader_parameter("luma") as Texture2D
	if luma != null and luma.get_width() > 0:
		_material.set_shader_parameter("picture_size", Vector2(luma.get_size()))


## The turn from the screen square back to the camera's picture: the inverse of the feed's turn, without its origin. Android sets the
## feed's transform to a bare rotation about the picture's corner (`Transform2D().rotated(...)` in camera_android.cpp), the desktop
## default is a flip with an origin that keeps it on the square; taking only the turn and applying it about the middle (as the shader
## does) is right for both. Used as is, Android's turn sent every point but one edge off the picture: a preview of stripes.
static func picture_turn(feed_turn: Transform2D) -> Transform2D:
	return Transform2D(feed_turn.x, feed_turn.y, Vector2.ZERO).affine_inverse()


## Where the shader reads the camera's picture for a point of the screen square (`uv` 0..1), as `id_camera.gdshader` does before
## its crop: for the tests, which cannot run the shader.
static func screen_to_picture(uv: Vector2, feed_turn: Transform2D, mirror: bool) -> Vector2:
	var point: Vector2 = Vector2(1.0 - uv.x if mirror else uv.x, uv.y)
	var back: Transform2D = picture_turn(feed_turn)
	return back.x * (point.x - 0.5) + back.y * (point.y - 0.5) + Vector2(0.5, 0.5)


## Whether a picture has arrived yet (until then Snap waits).
func has_picture() -> bool:
	return feed != null and feed.feed_is_active and feed.get_datatype() != CameraFeed.FEED_NOIMAGE


## Stops the camera (the sheet closed, a photo was taken, or the app went to the background).
func close() -> void:
	if feed != null:
		if feed.frame_changed.is_connected(_on_frame):
			feed.frame_changed.disconnect(_on_frame)
		feed.feed_is_active = false
	CameraServer.monitoring_feeds = false


## What the lens shows now, as a picture.
func capture(lens: SubViewport) -> Image:
	return lens.get_texture().get_image()


## Android's page for this app, where a refused camera can be allowed again.
func can_open_settings() -> bool:
	return HotspotSettings.available()


func open_settings() -> bool:
	if not can_open_settings():
		return false
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var activity: Object = runtime.call("getActivity") as Object
	if activity == null:
		return false
	var uri: Object = (wrapper.call("wrap", "android.net.Uri") as Object).call("parse", "package:%s" % activity.call("getPackageName")) as Object
	var intent: Object = (wrapper.call("wrap", "android.content.Intent") as Object).call("Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri) as Object
	if intent == null:
		return false
	activity.call("startActivity", intent)
	return wrapper.call("get_exception") == null
