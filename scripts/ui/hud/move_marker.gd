class_name MoveMarker
extends Control
## Where your kid is walking (docs/GDD.md "Moving": only you see it): a thin ring on the ground at your move target, drawn through the
## match camera so it lies flat on the street. A fresh tap shows at once where you tapped (online the server's answer comes a moment
## later) and pops in from a larger ring; with Reduce motion it just appears. It goes when the kid arrives or the walk ends.
## Code-drawn stand-in (no asset); it never takes a touch.

## The ring's radius on the ground (metres), its line width (px) and its colour; a fresh tap starts this much bigger.
const RADIUS: float = 0.42
const WIDTH: float = 4.0
const COLOR: Color = Color(1.0, 1.0, 1.0, 0.85)
const SHADOW: Color = Color(0.0, 0.02, 0.08, 0.5)
const POP_FROM: float = 1.8
const POP_TIME: float = 0.22
## A tap is drawn at its own point until the sim has the target, or for this long.
const TAP_HOLD: float = 0.5
const POINTS: int = 28

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1
var _tap: Vector2 = Vector2.ZERO
var _tap_left: float = 0.0
var _pop_left: float = 0.0


func _init() -> void:
	name = "MoveMarker"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func setup(sim: MatchSim, camera: Camera3D, local_id: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id


## A move order at street `point` was just given.
func ping(point: Vector2) -> void:
	_tap = point
	_tap_left = TAP_HOLD
	_pop_left = 0.0 if Settings.reduced_motion else POP_TIME
	queue_redraw()


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
	if me != null and me.has_move_target and me.move_target.distance_to(_tap) < RADIUS:
		_tap_left = 0.0  # the sim has it: follow the sim's (clamped) point from now on
	queue_redraw()


func _draw() -> void:
	var at: Variant = target()
	if at == null or _camera == null:
		return
	var grow: float = 1.0 + (POP_FROM - 1.0) * (_pop_left / POP_TIME) if _pop_left > 0.0 else 1.0
	var ring: PackedVector2Array = PackedVector2Array()
	for i: int in POINTS + 1:
		var ground: Vector2 = (at as Vector2) + Vector2.from_angle(TAU * float(i) / float(POINTS)) * RADIUS * grow
		var world: Vector3 = Vector3(ground.x, 0.02, ground.y)
		if _camera.is_position_behind(world):
			return
		ring.append(_camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world))
	draw_polyline(ring, SHADOW, WIDTH + 3.0, true)
	draw_polyline(ring, COLOR, WIDTH, true)
