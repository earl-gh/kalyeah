class_name MoveMarker
extends Control
## Where your kid is walking (docs/GDD.md "Moving": only you see it), drawn through the match camera so it lies flat on the street.
## A fresh tap shows at once where you tapped (online the server's answer comes a moment later) as a medium sized circle that
## smoothly shrinks, with nothing tinted inside it, until it is a small white dot (owner), and the dot stays as the target; with
## Reduce motion it is just the dot. It goes
## when the kid arrives or the walk ends. Code-drawn stand-in (no asset); it never takes a touch.

## A fresh tap's circle starts this big (radius on the ground, metres: about two kids across) and shrinks to the dot's radius.
const START_RADIUS: float = 1.4
const DOT_RADIUS: float = 0.22
## How long the circle takes to shrink into the dot (seconds): a quadratic ease-out, quick to start and settling softly.
const SHRINK_TIME: float = 0.45
## The ring's line width (px) and the colours: the circle is only a white line (no tint inside) that thickens shut as it reaches the dot.
const WIDTH: float = 4.0
const COLOR: Color = Color(1.0, 1.0, 1.0, 0.85)
const DOT_COLOR: Color = Color(1.0, 1.0, 1.0, 1.0)
const SHADOW: Color = Color(0.0, 0.02, 0.08, 0.5)
## A tap is drawn at its own point until the sim has the target (within this many metres of it), or for `TAP_HOLD` seconds.
const SYNC_RADIUS: float = 0.42
const TAP_HOLD: float = 0.5
const POINTS: int = 28
## The line starts closing up (becoming the solid dot) once the shrink is this far along (0..1).
const CLOSE_FROM: float = 0.6

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1
var _tap: Vector2 = Vector2.ZERO
var _tap_left: float = 0.0
## Seconds of the shrink still to play (0: it is the dot).
var _pop_left: float = 0.0


func _init() -> void:
	name = "MoveMarker"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func setup(sim: MatchSim, camera: Camera3D, local_id: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id


## A move order at street `point` was just given: a fresh tap plays the shrink, the finger dragging on (`fresh` false) only moves the
## dot, so the big circle does not come back at every touch event.
func ping(point: Vector2, fresh: bool = true) -> void:
	_tap = point
	_tap_left = TAP_HOLD
	if fresh:
		_pop_left = 0.0 if Settings.reduced_motion else SHRINK_TIME
	queue_redraw()


## How far the circle has become the dot: 0 on the tap, 1 once it is the dot (a quadratic ease-out, so it never steps).
func progress() -> float:
	var left: float = clampf(_pop_left / SHRINK_TIME, 0.0, 1.0)
	return 1.0 - left * left


## How shut the ring's line is (0: a thin line round an empty circle, 1: a solid dot): it only closes at the end of the shrink.
func closed() -> float:
	return smoothstep(CLOSE_FROM, 1.0, progress())


## The circle's radius on the ground now (metres): `START_RADIUS` on the tap, `DOT_RADIUS` from then on.
func radius() -> float:
	return lerpf(START_RADIUS, DOT_RADIUS, progress())


## The street point the ring is on, or null when there is none to show.
func target() -> Variant:
	if _sim == null:
		return null
	var me: PlayerState = _sim.players.get(_local_id) as PlayerState
	if me == null or me.tagged or _sim.phase != MatchSim.Phase.PLAYING:
		return null
	if _tap_left > 0.0:
		return _tap
	return me.move_target if me.has_move_target else null


func tick(delta: float) -> void:
	_tap_left = maxf(0.0, _tap_left - delta)
	_pop_left = maxf(0.0, _pop_left - delta)
	var me: PlayerState = _sim.players.get(_local_id) as PlayerState if _sim != null else null
	if me != null and me.has_move_target and me.move_target.distance_to(_tap) < SYNC_RADIUS:
		_tap_left = 0.0  # the sim has it: follow the sim's (clamped) point from now on
	queue_redraw()


func _draw() -> void:
	var at: Variant = target()
	if at == null or _camera == null:
		return
	var ring: PackedVector2Array = PackedVector2Array()
	for i: int in POINTS + 1:
		var ground: Vector2 = (at as Vector2) + Vector2.from_angle(TAU * float(i) / float(POINTS)) * radius()
		var world: Vector3 = Vector3(ground.x, 0.02, ground.y)
		if _camera.is_position_behind(world):
			return
		ring.append(_camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world))
	var shut: float = closed()
	if shut >= 1.0:
		ring.remove_at(ring.size() - 1)  # a polygon is not closed by repeating its first point
		draw_colored_polygon(ring, DOT_COLOR)
		return
	# Only a line, nothing inside: as the circle gets small the line thickens until it has no hole left (the dot).
	var centre: Vector2 = Vector2.ZERO
	for point: Vector2 in ring:
		centre += point
	centre /= float(ring.size())
	var half: float = INF
	for point: Vector2 in ring:
		half = minf(half, point.distance_to(centre))
	var width: float = lerpf(WIDTH, maxf(WIDTH, half * 2.0), shut)
	draw_polyline(ring, Color(SHADOW, SHADOW.a * (1.0 - shut)), width + 3.0, true)
	draw_polyline(ring, Color(COLOR, lerpf(COLOR.a, DOT_COLOR.a, shut)), width, true)
