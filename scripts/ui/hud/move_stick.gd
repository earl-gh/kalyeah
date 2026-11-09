class_name MoveStick
extends Control
## The movement joystick of the landscape match (owner, 2026-11-09: Wild Rift style; docs/GDD.md "Landscape layout"), under the left thumb.
## It floats: a finger that lands anywhere in the left part of the screen (`ZONE_SHARE` of the width, below the header) puts the base under
## it and the thumb follows the finger; let go and it goes back to its resting place at the bottom left. It reports the way it points
## (`knob_moved`, each axis -1 to 1, up the screen -1); the match screen turns that into a way on the street. One finger at a time, by its
## index, so a second finger on a card is never blocked. Built in code; it never decides a rule.

signal knob_moved(way: Vector2)

## The base's radius and the thumb's, and where the base rests (from the screen's bottom-left corner, before the safe area).
const BASE_RADIUS: float = 92.0
const THUMB_RADIUS: float = 40.0
const REST_MARGIN: Vector2 = Vector2(150.0, 150.0)
## The left share of the screen whose touches move the stick, and the top band (the header, the minimap) it leaves alone.
const ZONE_SHARE: float = 0.42
const ZONE_TOP: float = 200.0
## A push of less than this share of the radius does not move the kid.
const DEADZONE: float = 0.12
const BASE: Color = Color(0.04, 0.12, 0.3, 0.42)
const RIM: Color = Color(1.0, 1.0, 1.0, 0.55)
const THUMB: Color = Color(1.0, 1.0, 1.0, 0.85)
const RIM_WIDTH: float = 5.0
const NO_POINTER: int = -1

## Touches are taken (false under a menu, while tagged, during the opening show).
var active: bool = false:
	set(value):
		if value != active:
			active = value
			queue_redraw()
## The safe area's left and bottom insets (UI pixels): the resting base keeps clear of a notch or the home bar.
var inset: Vector2 = Vector2.ZERO
## Where the base stands now (own pixels) and the way the stick points.
var base_center: Vector2 = Vector2.ZERO
var way: Vector2 = Vector2.ZERO
var _pointer: int = NO_POINTER


func _ready() -> void:
	name = "MoveStick"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(_rest)
	_rest()


## Where the base rests: the bottom-left corner, clear of the safe area.
func rest_center() -> Vector2:
	return Vector2(inset.x + REST_MARGIN.x, size.y - inset.y - REST_MARGIN.y)


## The part of the screen (own pixels) whose touches move the stick.
func zone() -> Rect2:
	return Rect2(Vector2(0.0, ZONE_TOP), Vector2(size.x * ZONE_SHARE, maxf(0.0, size.y - ZONE_TOP)))


func holding() -> bool:
	return _pointer != NO_POINTER


## Lets go of the stick (a menu opened, the controls were switched off).
func cancel() -> void:
	_pointer = NO_POINTER
	_rest()
	_set_way(Vector2.ZERO)


func _rest() -> void:
	if _pointer == NO_POINTER:
		base_center = rest_center()
	queue_redraw()


func _input(event: InputEvent) -> void:
	if handle(event):
		get_viewport().set_input_as_handled()


## One input event (tests call it directly). Returns true when the stick took it.
func handle(event: InputEvent) -> bool:
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		var at: Vector2 = _local(touch.position)
		if touch.pressed:
			if _pointer != NO_POINTER or not active or not is_visible_in_tree() or not zone().has_point(at):
				return false
			_pointer = touch.index
			# the base comes under the finger, kept whole on the screen
			base_center = Vector2(maxf(at.x, inset.x + BASE_RADIUS), clampf(at.y, BASE_RADIUS, size.y - inset.y - BASE_RADIUS))
			_set_way(way_for(at))
			return true
		if touch.index == _pointer:
			cancel()
			return true
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _pointer:
			_set_way(way_for(_local(drag.position)))
			return true
	return false


## The way a finger at `at` (own pixels) pushes the stick: its offset from the base over the radius, at most 1 long, zero inside the deadzone.
func way_for(at: Vector2) -> Vector2:
	var push: Vector2 = ((at - base_center) / BASE_RADIUS).limit_length(1.0)
	return Vector2.ZERO if push.length() < DEADZONE else push


func _set_way(value: Vector2) -> void:
	if value == way:
		queue_redraw()
		return
	way = value
	knob_moved.emit(way)
	queue_redraw()


func _local(viewport_position: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * viewport_position


func _draw() -> void:
	if not active:
		return
	draw_circle(base_center, BASE_RADIUS, BASE)
	draw_arc(base_center, BASE_RADIUS, 0.0, TAU, 48, RIM, RIM_WIDTH, true)
	draw_circle(base_center + way * BASE_RADIUS, THUMB_RADIUS, THUMB)
