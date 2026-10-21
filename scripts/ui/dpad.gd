class_name DPad
extends Control
## The on-screen D-pad, like a Game Boy's but round: a dark disc with eight arrows (up, down, left, right and the four
## in between). Put a thumb anywhere on the disc and the kid runs the way the arrow under it points; slide the thumb
## to turn, lift it to stop. It is digital: eight directions only, always a full-speed run (owner: no slow walk).
## Slide the thumb out past the disc (the outer ring lights up) and let go there: the kid keeps running that way on
## his own (auto-run). A touch on the pad always takes over at once. Put it over the area it should listen in.
## Handles real touches (multi-touch safe) and the real mouse; ignores mouse events emulated from touch.

signal changed(value: Vector2)

## The disc.
const MAX_RADIUS: float = 100.0
## The outer ring: the thumb can slide out to it; past the disc a release locks the run.
const OUTER_RADIUS: float = 140.0
## Inside this share of the disc's radius (the middle button) nothing is pressed.
const DEADZONE: float = 0.22
## Eight directions, 45 degrees apart.
const STEP: float = PI / 4.0
const NO_POINTER: int = -1
const MOUSE_POINTER: int = -2
const BODY_COLOR: Color = Color(0.13, 0.14, 0.21)
const RIM_COLOR: Color = Color(0.6, 0.63, 0.75)
const ARROW_COLOR: Color = Color(0.8, 0.82, 0.9)
const PRESSED_COLOR: Color = Color(1.0, 0.86, 0.3)
const CENTER_COLOR: Color = Color(0.2, 0.21, 0.31)
const RING_COLOR: Color = Color(1.0, 1.0, 1.0, 0.6)
const RING_WIDTH: float = 4.0
## The ring that is not "active" is the same white, dimmer: the outer one until it lights up (auto-run armed).
const DIM_RING_COLOR: Color = Color(1.0, 1.0, 1.0, 0.25)
## While the outer ring is lit, the band between the disc and the ring is tinted too.
const BAND_COLOR: Color = Color(1.0, 1.0, 1.0, 0.16)

var value: Vector2 = Vector2.ZERO
## Resting spot of the pad inside the zone (0..1 of its size).
var rest_fraction: Vector2 = Vector2(0.5, 0.5)

## Running on its own after a release past the disc (value stays at full speed).
var auto_run: bool = false

var _pointer: int = NO_POINTER
var _armed: bool = false  # the thumb is past the disc: a release locks the run
var _origin: Vector2 = Vector2.ZERO


## Offset from the pad's centre to the direction pressed: one of eight unit vectors (up, down, left, right and the
## four in between), or zero in the middle button. It only picks the direction (owner: no slow walk).
static func stick_from_offset(offset: Vector2, radius: float, deadzone: float) -> Vector2:
	if offset.length() / radius < deadzone:
		return Vector2.ZERO
	return Vector2.from_angle(roundf(offset.angle() / STEP) * STEP)


func _input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		if touch.pressed:
			if _pointer == NO_POINTER and _in_zone(touch.position):
				_begin(touch.index, touch.position)
		elif touch.index == _pointer:
			if touch.canceled:
				release()
			else:
				_end()
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _pointer:
			_update(drag.position)
	elif event is InputEventMouseButton:
		var click: InputEventMouseButton = event as InputEventMouseButton
		if click.device == InputEvent.DEVICE_ID_EMULATION or click.button_index != MOUSE_BUTTON_LEFT:
			return
		if click.pressed:
			if _pointer == NO_POINTER and _in_zone(click.position):
				_begin(MOUSE_POINTER, click.position)
		elif _pointer == MOUSE_POINTER:
			_end()
	elif event is InputEventMouseMotion:
		var motion: InputEventMouseMotion = event as InputEventMouseMotion
		if motion.device != InputEvent.DEVICE_ID_EMULATION and _pointer == MOUSE_POINTER:
			_update(motion.position)


## Stops the pad dead: lets go of a thumb and ends auto-run (the controls were hidden, or a new round began).
func release() -> void:
	var moving: bool = _pointer != NO_POINTER or auto_run or value != Vector2.ZERO
	_pointer = NO_POINTER
	_armed = false
	auto_run = false
	value = Vector2.ZERO
	if moving:
		changed.emit(value)
	queue_redraw()


## The outer ring is lit: letting go now (or already let go) keeps running.
func outer_lit() -> bool:
	return auto_run or (_pointer != NO_POINTER and _armed)


## The outer ring shows only while a thumb is on the pad or it is auto-running.
func outer_visible() -> bool:
	return _pointer != NO_POINTER or auto_run


## [disc rim, outer ring] colours: both white and as thick; the lit one is the brighter.
func ring_colors() -> Array[Color]:
	if outer_lit():
		return [DIM_RING_COLOR, RING_COLOR]
	return [RING_COLOR, DIM_RING_COLOR]


## Where the pad rests (always visible).
func rest_position() -> Vector2:
	return size * rest_fraction


func _draw() -> void:
	var center: Vector2 = rest_position()
	var radius: float = MAX_RADIUS
	if outer_visible():
		if outer_lit():
			var band: float = OUTER_RADIUS - MAX_RADIUS
			draw_arc(center, MAX_RADIUS + band / 2.0, 0.0, TAU, 72, BAND_COLOR, band, true)
		draw_arc(center, OUTER_RADIUS, 0.0, TAU, 72, ring_colors()[1], RING_WIDTH, true)
	# the disc, with a lit rim and a soft bevel
	draw_circle(center, radius, BODY_COLOR)
	draw_arc(center, radius - 2.0, 0.0, TAU, 64, RIM_COLOR, 3.0, true)
	draw_arc(center, radius * 0.6, 0.0, TAU, 64, Color(1.0, 1.0, 1.0, 0.07), 2.0, true)
	var pressed: Vector2 = value
	# the pressed direction lights up as a wedge
	if pressed != Vector2.ZERO:
		var angle: float = pressed.angle()
		var wedge: PackedVector2Array = PackedVector2Array([center])
		for i: int in 9:
			wedge.append(center + Vector2.from_angle(angle - STEP / 2.0 + STEP * float(i) / 8.0) * (radius - 3.0))
		draw_colored_polygon(wedge, Color(PRESSED_COLOR, 0.3))
	# the eight arrows
	for i: int in 8:
		var direction: Vector2 = Vector2.from_angle(STEP * float(i))
		var side: Vector2 = direction.orthogonal()
		var lit: bool = pressed != Vector2.ZERO and pressed.is_equal_approx(direction)
		var tip: Vector2 = center + direction * radius * 0.88
		var base: Vector2 = center + direction * radius * 0.58
		var half: float = radius * (0.17 if i % 2 == 0 else 0.13)
		draw_colored_polygon(PackedVector2Array([tip, base + side * half, base - side * half]), PRESSED_COLOR if lit else ARROW_COLOR)
	# the middle button leans a little toward the pressed arrow, like a real pad
	var middle: Vector2 = center + pressed * radius * 0.05
	draw_circle(middle, radius * 0.22, CENTER_COLOR)
	draw_arc(middle, radius * 0.22, 0.0, TAU, 32, Color(1.0, 1.0, 1.0, 0.18), 2.0, true)


func _to_local(viewport_position: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * viewport_position


## Where a touch grabs the pad: on the disc, or anywhere in the outer ring while auto-running (it is showing then).
func _in_zone(viewport_position: Vector2) -> bool:
	var reach: float = OUTER_RADIUS if auto_run else MAX_RADIUS
	return _to_local(viewport_position).distance_to(rest_position()) <= reach


func _begin(pointer: int, viewport_position: Vector2) -> void:
	# grabs at once, also out of auto-run: the thumb takes over on this very touch
	auto_run = false
	_pointer = pointer
	_origin = rest_position()
	_update(viewport_position)


func _update(viewport_position: Vector2) -> void:
	var offset: Vector2 = _to_local(viewport_position) - _origin
	value = stick_from_offset(offset, MAX_RADIUS, DEADZONE)
	_armed = offset.length() > MAX_RADIUS
	changed.emit(value)
	queue_redraw()


func _end() -> void:
	_pointer = NO_POINTER
	if _armed and value != Vector2.ZERO:
		# let go past the disc: keep running that way at full speed
		auto_run = true
	else:
		auto_run = false
		value = Vector2.ZERO
	_armed = false
	changed.emit(value)
	queue_redraw()
