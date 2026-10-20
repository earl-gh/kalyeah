class_name DirectionArrows
extends Control
## A small chevron on the screen edge that says where to go: one toward the enemy base while you are free and
## playing, and a gold one toward your team's box while a teammate is tagged in it. No words and no distance (the
## minimap has the whole street); it hugs the edge so it stays out of the field, fades out when its target is on
## screen, and never takes a touch. It only reads the sim and the camera.

## Pixels kept clear of the screen edge on each side: left, top (the score row), right, bottom (the controls).
const INSET: Vector4 = Vector4(36.0, 150.0, 36.0, 330.0)
## The minimap and the settings button sit in this corner: an arrow is moved out of it.
const CORNER: Vector2 = Vector2(280.0, 290.0)
## A target counts as on screen this far inside the edge.
const ON_SCREEN_MARGIN: float = 60.0
const ARROW_SIZE: float = 16.0
const LINE_WIDTH: float = 5.0
const FADE_SPEED: float = 6.0
const MAX_ALPHA: float = 0.75
const BASE_COLOR: Color = Color(1.0, 0.5, 0.45)
const RESCUE_COLOR: Color = Color(1.0, 0.82, 0.3)
const TARGET_HEIGHT: float = 0.8

## What is shown now: {kind, label, color, point (on the edge), angle, alpha, metres}. Tests read this.
var arrows: Array[Dictionary] = []

var _sim: MatchSim
var _camera: Camera3D
var _layout: MapLayout
var _local_id: int = -1
var _local_team: int = 0
var _alpha: Dictionary[StringName, float] = {}


func setup(sim: MatchSim, camera: Camera3D, local_id: int, local_team: int, layout: MapLayout) -> void:
	_sim = sim
	_camera = camera
	_layout = layout
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


## The point on the edge of `rect` that a ray from `origin` toward `target` leaves by (the target itself when it is
## inside). `origin` is pulled inside the rect first.
static func edge_point(origin: Vector2, target: Vector2, rect: Rect2) -> Vector2:
	if rect.has_point(target):
		return target
	var start: Vector2 = Vector2(clampf(origin.x, rect.position.x, rect.end.x), clampf(origin.y, rect.position.y, rect.end.y))
	var direction: Vector2 = target - start
	if direction.is_zero_approx():
		return start
	var reach: float = INF
	if not is_zero_approx(direction.x):
		reach = minf(reach, ((rect.end.x if direction.x > 0.0 else rect.position.x) - start.x) / direction.x)
	if not is_zero_approx(direction.y):
		reach = minf(reach, ((rect.end.y if direction.y > 0.0 else rect.position.y) - start.y) / direction.y)
	return start + direction * reach


## `point` moved out of the minimap corner (along whichever way is shorter).
static func clear_of_corner(point: Vector2) -> Vector2:
	if point.x >= CORNER.x or point.y >= CORNER.y:
		return point
	return Vector2(CORNER.x, point.y) if CORNER.x - point.x < CORNER.y - point.y else Vector2(point.x, CORNER.y)


## What should be pointed at now, as {kind, world (x, z), label, color}: nothing unless the match is being played
## and you are free.
func targets() -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	if _sim == null or _sim.phase != MatchSim.Phase.PLAYING or not _sim.players.has(_local_id):
		return found
	var me: PlayerState = _sim.players[_local_id]
	if me.tagged:
		return found
	var own_side: int = _sim.side_for_team(_local_team)
	found.append({"kind": &"base", "world": _layout.base_center(-own_side), "label": "Enemy base", "color": BASE_COLOR})
	for id: int in _sim.players:
		var other: PlayerState = _sim.players[id]
		if id != _local_id and other.team == _local_team and other.tagged:
			found.append({"kind": &"rescue", "world": _layout.jail_rect(own_side).get_center(), "label": "Rescue", "color": RESCUE_COLOR})
			break
	return found


func tick(delta: float) -> void:
	arrows.clear()
	var wanted: Array[Dictionary] = targets()
	var shown: Dictionary[StringName, bool] = {}
	var rect: Rect2 = Rect2(Vector2(INSET.x, INSET.y), size - Vector2(INSET.x + INSET.z, INSET.y + INSET.w))
	if rect.size.x > 0.0 and rect.size.y > 0.0:
		var me: PlayerState = _sim.players[_local_id] if _sim != null and _sim.players.has(_local_id) else null
		for target: Dictionary in wanted:
			var world: Vector2 = target["world"] as Vector2
			var point: Variant = _project(world)
			var origin: Vector2 = (_project(me.position) as Vector2) if me != null and _project(me.position) != null else size / 2.0
			var edge: Vector2 = edge_point(origin, point as Vector2, rect) if point != null else edge_point(origin, origin + Vector2(0.0, 1.0) * size.y, rect)
			edge = clear_of_corner(edge)
			var on_screen: bool = point != null and Rect2(Vector2.ONE * ON_SCREEN_MARGIN, size - Vector2.ONE * ON_SCREEN_MARGIN * 2.0).has_point(point as Vector2)
			var kind: StringName = target["kind"] as StringName
			shown[kind] = true
			var fade_to: float = 0.0 if on_screen else 1.0
			_alpha[kind] = move_toward(_alpha.get(kind, 0.0) as float, fade_to, FADE_SPEED * delta) if delta > 0.0 else fade_to
			var direction: Vector2 = ((point as Vector2) - origin) if point != null else Vector2(0.0, 1.0)
			arrows.append({
				"kind": kind, "label": "%s · %d m" % [target["label"], roundi(((me.position if me != null else world) - world).length())],
				"color": target["color"], "point": edge, "angle": direction.angle(), "alpha": _alpha[kind],
				"metres": roundi(((me.position if me != null else world) - world).length()),
			})
	for kind: StringName in _alpha.keys():
		if not shown.has(kind):
			_alpha.erase(kind)
	queue_redraw()


## Where on screen the ground point `world` (x, z) shows; null when it is behind the camera.
func _project(world: Vector2) -> Variant:
	var point: Vector3 = Vector3(world.x, TARGET_HEIGHT, world.y)
	if _camera == null or _camera.is_position_behind(point):
		return null
	return _camera.screen_position(point) if _camera is FollowCamera else _camera.unproject_position(point)


func _draw() -> void:
	for arrow: Dictionary in arrows:
		var alpha: float = (arrow["alpha"] as float) * MAX_ALPHA
		if alpha <= 0.01:
			continue
		var color: Color = arrow["color"] as Color
		var at: Vector2 = arrow["point"] as Vector2
		var unit: Vector2 = Vector2.from_angle(arrow["angle"] as float)
		var side: Vector2 = unit.rotated(PI / 2.0)
		var chevron: PackedVector2Array = PackedVector2Array([
			at - unit * ARROW_SIZE * 0.5 + side * ARROW_SIZE, at + unit * ARROW_SIZE * 0.5, at - unit * ARROW_SIZE * 0.5 - side * ARROW_SIZE,
		])
		draw_polyline(chevron, Color(0.05, 0.04, 0.1, alpha * 0.45), LINE_WIDTH + 4.0, true)
		draw_polyline(chevron, Color(color, alpha), LINE_WIDTH, true)
