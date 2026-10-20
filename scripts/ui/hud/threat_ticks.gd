class_name ThreatTicks
extends Control
## A small tick on the screen edge for every enemy that could hit you right now but is off screen: within the
## reach of your longest weapon plus a little. A portrait screen is narrow, so a throw from the side or from behind
## can come from beyond the edge; the tick says which way to look. It never takes a touch, shows nothing for an
## enemy you can already see, and hides while you are tagged or the match is not being played. It only reads the
## sim and the camera.

## How far past the weapon's reach an enemy still counts (metres): time to see it coming.
const REACH_PAD: float = 2.0
## Never fewer than this reach (metres), for a loadout of short weapons.
const MIN_REACH: float = 8.0
## At most this many ticks (the nearest).
const MAX_TICKS: int = 4
## An enemy counts as on screen this far inside the edge.
const ON_SCREEN_MARGIN: float = 24.0
## Pixels kept clear of the edge for the tick itself.
const EDGE_INSET: float = 22.0
const SIZE: float = 11.0
const COLOR: Color = Color(1.0, 0.35, 0.3)
const UNDER: Color = Color(0.05, 0.04, 0.1, 0.5)
const HEIGHT: float = 1.0

## What is shown now: {id, point (on the edge), angle, metres}. Tests read this.
var ticks: Array[Dictionary] = []

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1


func setup(sim: MatchSim, camera: Camera3D, local_id: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


## The reach (metres) inside which an off-screen enemy gets a tick: your longest weapon plus the pad.
func reach_for(player: PlayerState) -> float:
	var reach: float = MIN_REACH
	for weapon: StringName in player.weapons:
		var def: WeaponDef = _sim.weapon_defs.get(weapon) as WeaponDef
		if def != null:
			reach = maxf(reach, def.max_range)
	return reach + REACH_PAD


## Where on screen (pixels) the ground point of `state` shows, or null when it is behind the camera.
func _project(state: PlayerState) -> Variant:
	var point: Vector3 = Vector3(state.position.x, HEIGHT, state.position.y)
	if _camera == null or _camera.is_position_behind(point):
		return null
	return _camera.screen_position(point) if _camera is FollowCamera else _camera.unproject_position(point)


func tick() -> void:
	ticks.clear()
	if _sim != null and _sim.phase == MatchSim.Phase.PLAYING and _sim.players.has(_local_id) and size.x > 0.0:
		var me: PlayerState = _sim.players[_local_id]
		if not me.tagged:
			_collect(me)
	queue_redraw()


func _collect(me: PlayerState) -> void:
	var reach: float = reach_for(me)
	var screen: Rect2 = Rect2(Vector2.ONE * ON_SCREEN_MARGIN, size - Vector2.ONE * ON_SCREEN_MARGIN * 2.0)
	var edge: Rect2 = Rect2(Vector2.ONE * EDGE_INSET, size - Vector2.ONE * EDGE_INSET * 2.0)
	var mine: Variant = _project(me)
	var origin: Vector2 = (mine as Vector2) if mine != null else size / 2.0
	var found: Array[Dictionary] = []
	for other: PlayerState in _sim.enemies_of(me.team):
		var metres: float = me.position.distance_to(other.position)
		if metres > reach or _sim.hides_in_box(other, me.team):
			continue
		var at: Variant = _project(other)
		if at != null and screen.has_point(at as Vector2):
			continue
		# behind the camera: the opposite way from where the world says it is, which is straight down the screen
		var toward: Vector2 = ((at as Vector2) - origin) if at != null else Vector2(0.0, 1.0)
		var point: Vector2 = DirectionArrows.edge_point(origin, origin + toward * 10000.0, edge)
		found.append({"id": other.id, "point": point, "angle": toward.angle(), "metres": metres})
	found.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return (a["metres"] as float) < (b["metres"] as float))
	ticks = found.slice(0, MAX_TICKS)


func _draw() -> void:
	for entry: Dictionary in ticks:
		var at: Vector2 = entry["point"] as Vector2
		var unit: Vector2 = Vector2.from_angle(entry["angle"] as float)
		var side: Vector2 = unit.rotated(PI / 2.0)
		var triangle: PackedVector2Array = PackedVector2Array([at + unit * SIZE, at - unit * SIZE * 0.6 + side * SIZE * 0.8, at - unit * SIZE * 0.6 - side * SIZE * 0.8])
		draw_colored_polygon(triangle, COLOR)
		draw_polyline(PackedVector2Array([triangle[0], triangle[1], triangle[2], triangle[0]]), UNDER, 3.0, true)
