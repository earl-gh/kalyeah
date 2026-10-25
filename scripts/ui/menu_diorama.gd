class_name MenuDiorama
extends SubViewportContainer
## The main screen's map model: the top half of the play area (the far end, a 1:1 square like the
## Clash Royale arena on its main screen) on a chunky stone base. Only the corner props (electric posts); no kids,
## team colours, sidewalks or movement. It lives
## in its own 3D world inside a transparent SubViewport, so it floats over the screen background.

const CAMERA_FOV: float = 34.0
const CAMERA_PITCH: float = 50.0
## How much of the frame the model may fill (1 = touching the edges).
const FRAME_FILL: float = 0.98

signal framed

var viewport: SubViewport
var camera: Camera3D
var map: StreetMap


func _ready() -> void:
	stretch = true
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	viewport = SubViewport.new()
	viewport.own_world_3d = true
	viewport.transparent_bg = true
	viewport.msaa_3d = Viewport.MSAA_4X
	viewport.handle_input_locally = false
	viewport.gui_disable_input = true
	add_child(viewport)
	_build_map()
	_build_camera()


func _build_map() -> void:
	map = (load("res://scenes/map/street_map.tscn") as PackedScene).instantiate() as StreetMap
	map.layout = load("res://data/rules/map_layout.tres") as MapLayout
	map.diorama = true
	map.show_overview_camera = false
	map.get_node("Hud").queue_free()
	viewport.add_child(map)


## A transparent camera over the model, looking down at it from the front.
func _build_camera() -> void:
	camera = Camera3D.new()
	camera.fov = CAMERA_FOV
	camera.keep_aspect = Camera3D.KEEP_WIDTH
	var env: Environment = StreetMap.make_environment()
	env.background_mode = Environment.BG_CLEAR_COLOR
	env.glow_enabled = false
	env.fog_enabled = false
	camera.environment = env
	camera.near = 1.0
	camera.far = 400.0
	viewport.add_child(camera)
	camera.make_current()
	frame()
	resized.connect(frame)


## Places the camera so every corner of the model (cement and stone base) fits the viewport
## with a margin, looking at its middle from the front.
func frame() -> void:
	if camera == null or map == null:
		return
	var pitch: float = deg_to_rad(CAMERA_PITCH)
	var half_x: float = map.layout.lane_width / 2.0 + StreetMap.PLATFORM_MARGIN
	var half_z: float = map.layout.lane_length / 2.0
	# only the far half is shown: from the far end to the middle line
	var middle: Vector3 = Vector3(0.0, 0.0, -half_z / 2.0)
	var aspect: float = maxf(0.2, size.x / maxf(size.y, 1.0))
	var tan_h: float = tan(deg_to_rad(CAMERA_FOV) / 2.0)
	var tan_v: float = tan_h / aspect
	var forward: Vector3 = Vector3(0.0, -sin(pitch), -cos(pitch))
	var right: Vector3 = Vector3.RIGHT
	var up: Vector3 = right.cross(forward).normalized()
	var distance: float = 10.0
	# the smallest distance at which all eight corners are inside the frame (with a margin)
	for step: int in 60:
		var eye: Vector3 = middle - forward * distance
		var fits: bool = true
		for cx: float in [-half_x, half_x]:
			for cy: float in [-StreetMap.PLINTH_HEIGHT - StreetMap.PLATFORM_HEIGHT - 0.1, StreetMap.POST_HEIGHT]:
				for cz: float in [-half_z - StreetMap.PLATFORM_MARGIN, StreetMap.PLATFORM_MARGIN]:
					var rel: Vector3 = Vector3(cx, cy, cz) - eye
					var depth: float = rel.dot(forward)
					if absf(rel.dot(right)) > depth * tan_h * FRAME_FILL or absf(rel.dot(up)) > depth * tan_v * FRAME_FILL:
						fits = false
		if fits:
			break
		distance *= 1.06
	camera.position = middle - forward * distance
	camera.rotation_degrees = Vector3(-CAMERA_PITCH, 0.0, 0.0)
	framed.emit()


## Where the middle of the blue platform's front face shows, in this control's pixels (the map
## title is laid there).
func platform_front_point() -> Vector2:
	var depth: float = -0.1 - StreetMap.PLINTH_HEIGHT - StreetMap.PLATFORM_HEIGHT / 2.0
	var shown: Vector2 = camera.unproject_position(Vector3(0.0, depth, StreetMap.PLATFORM_MARGIN))
	# the picture may still be one frame behind the control's size: scale to the control
	return shown * size / Vector2(maxf(viewport.size.x, 1.0), maxf(viewport.size.y, 1.0))
