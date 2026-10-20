class_name LaneMinimap
extends Control
## Minimap in the top left corner (docs/GDD.md "HUD", no frame): the whole playing area at
## exactly its own 1:1 shape, seen from above with your end at the bottom. Your base
## is the blue corner at the bottom left, theirs the red corner at the top right;
## the taped boxes are where tagged kids wait (yours top left, theirs bottom right).
## Obstacles are dark blocks; every player is the face of their character in a circle, with one
## thin ring in the team colour. The drawing (MinimapCanvas) is rendered at SUPERSAMPLE times the
## size in a SubViewport and shown scaled down, so every element is smooth, then cropped to rounded
## corners by a shader.

const ROAD: Color = Color(0.33, 0.33, 0.36, 0.55)
const OBSTACLE: Color = Color(0.16, 0.15, 0.18)
const OWN: Color = Color(0.3, 0.6, 1.0)
const ENEMY: Color = Color(1.0, 0.35, 0.35)
const BASE_ALPHA: float = 0.55
const JAIL_ALPHA: float = 0.75
## The street fills the whole minimap (no panel or frame round it). Everything drawn is cropped
## to rounded corners of this radius (px) by shaders/minimap_crop.gdshader, with a smooth edge.
const CORNER: float = 12.0
const CROP_SHADER: Shader = preload("res://shaders/minimap_crop.gdshader")
## A player's face circle: its radius, how thick the one team-colour ring on its edge is, and how
## much bigger you are than the others (px).
const FACE: float = 9.0
## The width the constants below are written for; a bigger minimap scales them all (`unit()`).
const BASE_WIDTH: float = 100.0
const RING: float = 0.6
const SELF_SCALE: float = 1.25
## The minimap is drawn this many times bigger and scaled down, which smooths every edge.
const SUPERSAMPLE: int = 3
const FACE_STEPS: int = 40
## A tagged kid's face is dimmed.
const TAGGED_DIM: float = 0.5
const BASE_STEPS: int = 48
## A ping is a ring that grows from the spot and fades, over PING_TIME seconds: gold where a teammate was sent to the
## box, in the scoring team's colour round the base that was captured. (Start radius, end radius) in minimap pixels.
const PING_TIME: float = 1.0
const PING_RADII: Vector2 = Vector2(FACE, 30.0)
const BASE_PING_RADII: Vector2 = Vector2(16.0, 46.0)
const PING_ALERT: Color = Color(1.0, 0.82, 0.3)
const PING_WIDTH: float = 1.6
## With Reduce motion a ping does not grow: it stays at this share of its full size and only fades.
const PING_STILL: float = 0.6

var _sim: MatchSim
var _local_id: int = -1
var _local_team: int = 0
var _view: SubViewport
var _canvas: MinimapCanvas
var _shown: TextureRect
var _pings: Array[Dictionary] = []


func setup(sim: MatchSim, local_id: int, local_team: int) -> void:
	_sim = sim
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the drawing is rendered big in its own viewport (smooth: no pixelated edges) ...
	_view = SubViewport.new()
	_view.transparent_bg = true
	_view.disable_3d = true
	_view.gui_disable_input = true
	_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_view)
	_canvas = MinimapCanvas.new()
	_canvas.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_canvas.scale = Vector2.ONE * float(SUPERSAMPLE)
	_view.add_child(_canvas)
	_canvas.setup(self, sim, local_id, local_team)
	# ... and shown scaled down, cropped to rounded corners with a smooth edge
	_shown = TextureRect.new()
	_shown.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shown.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_shown.stretch_mode = TextureRect.STRETCH_SCALE
	_shown.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_shown.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_shown.texture = _view.get_texture()
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = CROP_SHADER
	crop.set_shader_parameter("radius", CORNER)
	_shown.material = crop
	add_child(_shown)
	resized.connect(_update_size)
	_update_size()
	sim.player_tagged.connect(_on_player_tagged)
	sim.point_scored.connect(_on_point_scored)


## How much bigger than the 100 px base this minimap is, for faces, rings and pings.
func unit() -> float:
	return maxf(size.x, 1.0) / BASE_WIDTH


func _update_size() -> void:
	var pixels: Vector2i = Vector2i(maxi(1, ceili(size.x * SUPERSAMPLE)), maxi(1, ceili(size.y * SUPERSAMPLE)))
	_view.size = pixels
	_canvas.size = size
	(_shown.material as ShaderMaterial).set_shader_parameter("rect_size", size)


func _process(delta: float) -> void:
	var alive: Array[Dictionary] = []
	for ping_data: Dictionary in _pings:
		ping_data["left"] = (ping_data["left"] as float) - delta
		if (ping_data["left"] as float) > 0.0:
			alive.append(ping_data)
	_pings = alive
	if _canvas != null:
		_canvas.queue_redraw()


## Starts a ring at street position `world` (x, z).
func ping(world: Vector2, color: Color, radii: Vector2 = PING_RADII) -> void:
	_pings.append({"at": world, "color": color, "radii": radii, "left": PING_TIME})


## The rings alive now (tests).
func pings() -> Array[Dictionary]:
	return _pings


## A ring's current radius and opacity: it grows quickly then slows, and fades out.
func ring_of(ping_data: Dictionary) -> Dictionary:
	var progress: float = 1.0 - clampf((ping_data["left"] as float) / PING_TIME, 0.0, 1.0)
	var radii: Vector2 = ping_data["radii"] as Vector2
	var grown: float = lerpf(radii.x, radii.y, 1.0 - pow(1.0 - progress, 2.0))
	if Settings.reduced_motion:
		grown = lerpf(radii.x, radii.y, PING_STILL)
	return {"radius": grown, "alpha": 1.0 - progress}


## A teammate was sent to the box: ring the box. (An enemy's tag is not pinged: that would show where they are.)
func _on_player_tagged(id: int) -> void:
	var state: PlayerState = _sim.players.get(id) as PlayerState
	if state != null and state.team == _local_team:
		ping(state.position, PING_ALERT)


## A point was scored: ring the base that was captured, in the scoring team's colour.
func _on_point_scored(team: int, _how: int) -> void:
	var base: Vector2 = _sim.layout.base_center(-_sim.side_for_team(team))
	ping(base, OWN if team == _local_team else ENEMY, BASE_PING_RADII)


## Street x/z to minimap pixels, turned so the local team's end is at the bottom.
func to_map(world: Vector2) -> Vector2:
	var layout: MapLayout = _sim.layout
	var view: Vector2 = world * float(_sim.side_for_team(_local_team))
	var inner: Rect2 = street_rect()
	return inner.get_center() + Vector2(
		view.x / layout.lane_width * inner.size.x, view.y / layout.lane_length * inner.size.y
	)


## The street's rectangle: as big as fits, at exactly the street's ratio.
func street_rect() -> Rect2:
	var room: Vector2 = size
	var ratio: float = _sim.layout.lane_width / _sim.layout.lane_length
	var fit: Vector2 = Vector2(room.y * ratio, room.y) if room.x / room.y > ratio else Vector2(room.x, room.x / ratio)
	return Rect2((size - fit) / 2.0, fit)


## The corners of a circle of `radius` round `center`.
static func face_points(center: Vector2, radius: float) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	for i: int in FACE_STEPS:
		var angle: float = TAU * float(i) / float(FACE_STEPS)
		points.append(center + Vector2(cos(angle), sin(angle)) * radius)
	return points


## The texture coordinates for those corners: a square face fills the circle; for a tall
## portrait it is the square at the top (the face and shoulders).
static func face_uvs(texture: Texture2D) -> PackedVector2Array:
	var height: float = minf(1.0, float(texture.get_width()) / float(texture.get_height()))
	var uvs: PackedVector2Array = PackedVector2Array()
	for i: int in FACE_STEPS:
		var angle: float = TAU * float(i) / float(FACE_STEPS)
		uvs.append(Vector2(0.5 + 0.5 * cos(angle), height * (0.5 + 0.5 * sin(angle))))
	return uvs
