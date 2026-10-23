class_name DPad
extends Control
## The on-screen D-pad, shaped like the Xbox Series one: a round dark dish with a raised cross in it, an arrow notch on each arm and a
## shallow dish in the middle. Up, down, left and right are the four arms and the diagonals press two neighbouring arms. Put a thumb
## anywhere on the disc and the kid runs the way it points; slide the thumb to turn, lift it to stop. It is digital: eight directions
## only, always a full-speed run (owner: no slow walk) and no auto-run: the kid runs only while a thumb is on the pad (owner: it is a
## D-pad now). Sliding past the disc keeps the direction. Put it over the area it should listen in.
## Handles real touches (multi-touch safe) and the real mouse; ignores mouse events emulated from touch.

signal changed(value: Vector2)
## One of the eight directions was entered by the thumb (or the pad let go): the match gives a short tick on a phone.
signal direction_changed(value: Vector2)

## The disc.
const MAX_RADIUS: float = 100.0
## Inside this share of the disc's radius (the middle button) nothing is pressed.
const DEADZONE: float = 0.22
## Eight directions, 45 degrees apart.
const STEP: float = PI / 4.0
const NO_POINTER: int = -1
const MOUSE_POINTER: int = -2
## The dish the cross sits in (graphite, a little lighter inside the rim), the cross, its arrow notches and the middle dish.
const BODY_COLOR: Color = Color(0.035, 0.1, 0.27)
const DISH_COLOR: Color = Color(0.07, 0.22, 0.55)
const RIM_COLOR: Color = Color(1.0, 0.8, 0.25)
const CROSS_COLOR: Color = Color(0.93, 0.95, 1.0)
const ARROW_COLOR: Color = Color(0.2, 0.36, 0.75)
const PRESSED_COLOR: Color = Color(1.0, 0.72, 0.12)
const CENTER_COLOR: Color = Color(0.74, 0.8, 0.95)
## The crafted cross (visual pass): the cross is a raised block, `CROSS_DEPTH` px of darker side under its face, the face
## shaded from `CROSS_COLOR` at the top to `CROSS_SHADE` at the bottom with a light edge along its upper sides; a pressed
## arm sinks `PRESS_SINK` px into the side and is tinted.
const CROSS_SHADE: Color = Color(0.72, 0.78, 0.92)
const CROSS_SIDE: Color = Color(0.36, 0.44, 0.66)
const CROSS_LIGHT: Color = Color(1.0, 1.0, 1.0, 0.85)
const CROSS_LIGHT_WIDTH: float = 3.0
const CROSS_DEPTH: float = 7.0
const PRESS_SINK: float = 5.0
## The cross: each arm is this wide and reaches this far, as shares of the disc's radius.
const ARM_WIDTH: float = 0.62
const ARM_REACH: float = 0.93
## A direction counts as pressing an arm when that axis of the stick is at least this much (a diagonal is 0.71 on both).
const ARM_PRESS: float = 0.38

var value: Vector2 = Vector2.ZERO
## Resting spot of the pad inside the zone (0..1 of its size).
var rest_fraction: Vector2 = Vector2(0.5, 0.5)


var _pointer: int = NO_POINTER
## The direction the last tick was given for (so only a change of direction buzzes).
var _last_direction: Vector2 = Vector2.ZERO
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


## Stops the pad dead: lets go of a thumb (the controls were hidden, or a new round began).
func release() -> void:
	var moving: bool = _pointer != NO_POINTER or value != Vector2.ZERO
	_pointer = NO_POINTER
	value = Vector2.ZERO
	_last_direction = Vector2.ZERO
	if moving:
		changed.emit(value)
	queue_redraw()


## Where the pad rests (always visible).
func rest_position() -> Vector2:
	return size * rest_fraction


func _draw() -> void:
	var center: Vector2 = rest_position()
	var radius: float = MAX_RADIUS
	_draw_dish(center, radius)
	var pressed: Vector2 = value
	var arms: Array[bool] = pressed_arms(pressed)
	_draw_cross(center, radius, arms)
	# the middle dish leans a little toward what is pressed, like the real pad's
	var middle: Vector2 = center + pressed * radius * 0.04
	draw_circle(middle, radius * 0.3, CENTER_COLOR)
	draw_arc(middle, radius * 0.3, 0.0, TAU, 32, BODY_COLOR, 4.0, true)


## Which of the four arms (right, down, left, up: the angles 0, 90, 180, 270 degrees) a stick direction presses.
static func pressed_arms(direction: Vector2) -> Array[bool]:
	return [direction.x >= ARM_PRESS, direction.y >= ARM_PRESS, direction.x <= -ARM_PRESS, direction.y <= -ARM_PRESS]


## The round dish: a navy body, a gold rim and a royal blue floor.
func _draw_dish(center: Vector2, radius: float) -> void:
	draw_circle(center, radius, BODY_COLOR)
	draw_arc(center, radius - 4.0, 0.0, TAU, 64, RIM_COLOR, 6.0, true)
	draw_arc(center, radius - 9.0, PI * 1.05, PI * 1.95, 40, Color(0.0, 0.0, 0.0, 0.3), 3.0, true)
	draw_circle(center, radius - 9.0, DISH_COLOR)
	# a soft shadow on the upper left of the floor and a little light on the lower right: the dish is concave
	draw_circle(center + Vector2(-5.0, -7.0), radius - 22.0, Color(0.0, 0.0, 0.0, 0.2))
	draw_circle(center + Vector2(3.0, 5.0), radius - 26.0, DISH_COLOR)


## The raised cross: a dark shadow, the darker side, the shaded face with a lit upper edge and a notch at the end of every
## arm; a pressed arm sinks into the side, tinted, its notch lit.
func _draw_cross(center: Vector2, radius: float, arms: Array[bool]) -> void:
	var half: float = radius * ARM_WIDTH / 2.0
	var reach: float = radius * ARM_REACH
	var outline: PackedVector2Array = cross_outline(center, half, reach)
	var down: Vector2 = Vector2(0.0, CROSS_DEPTH)
	draw_colored_polygon(_moved(outline, down + Vector2(0.0, 4.0)), Color(0.0, 0.0, 0.0, 0.45))
	draw_colored_polygon(_moved(outline, down), CROSS_SIDE)
	_shaded(outline, center.y - reach, center.y + reach)
	var closed: PackedVector2Array = outline.duplicate()
	closed.append(outline[0])
	# the lit edge along the face's upper and left sides (the light comes from the top left)
	for index: int in outline.size():
		var from: Vector2 = outline[index]
		var to: Vector2 = outline[(index + 1) % outline.size()]
		# the outward normal of this edge (the corners run clockwise on screen)
		var outward: Vector2 = (to - from).orthogonal().normalized()
		if outward.y < -0.5 or outward.x < -0.5:
			draw_line(from - outward * CROSS_LIGHT_WIDTH, to - outward * CROSS_LIGHT_WIDTH, CROSS_LIGHT, CROSS_LIGHT_WIDTH, true)
	for index: int in 4:
		var direction: Vector2 = Vector2.from_angle(float(index) * PI / 2.0)
		var side: Vector2 = direction.orthogonal()
		var lit: bool = arms[index]
		var sink: Vector2 = Vector2(0.0, PRESS_SINK) if lit else Vector2.ZERO
		if lit:
			var near: Vector2 = center + direction * half
			var far: Vector2 = center + direction * reach
			var arm: PackedVector2Array = PackedVector2Array([near + side * half, far + side * half, far - side * half, near - side * half])
			draw_colored_polygon(_moved(arm, sink), Color(PRESSED_COLOR, 0.9))
		# the notch: a small triangle pointing out, near the end of the arm
		var tip: Vector2 = center + direction * (reach - 12.0) + sink
		var base: Vector2 = center + direction * (reach - 32.0) + sink
		var wing: float = half * 0.45
		draw_colored_polygon(PackedVector2Array([tip, base + side * wing, base - side * wing]), PRESSED_COLOR.darkened(0.35) if lit else ARROW_COLOR)
	draw_polyline(closed, BODY_COLOR, 4.0, true)


## The cross's twelve corners round `center`: arms `half` wide on each side, reaching `reach`.
static func cross_outline(center: Vector2, half: float, reach: float) -> PackedVector2Array:
	return PackedVector2Array([
		center + Vector2(half, -reach), center + Vector2(half, -half), center + Vector2(reach, -half), center + Vector2(reach, half),
		center + Vector2(half, half), center + Vector2(half, reach), center + Vector2(-half, reach), center + Vector2(-half, half),
		center + Vector2(-reach, half), center + Vector2(-reach, -half), center + Vector2(-half, -half), center + Vector2(-half, -reach)])


static func _moved(points: PackedVector2Array, by: Vector2) -> PackedVector2Array:
	var moved: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in points:
		moved.append(point + by)
	return moved


## The cross's face, shaded from light at `top` to darker at `bottom` (one colour per corner).
func _shaded(outline: PackedVector2Array, top: float, bottom: float) -> void:
	var colors: PackedColorArray = PackedColorArray()
	for point: Vector2 in outline:
		colors.append(CROSS_COLOR.lerp(CROSS_SHADE, clampf((point.y - top) / maxf(bottom - top, 1.0), 0.0, 1.0)))
	draw_polygon(outline, colors)


func _to_local(viewport_position: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * viewport_position


## Where a touch grabs the pad: on the disc.
func _in_zone(viewport_position: Vector2) -> bool:
	return _to_local(viewport_position).distance_to(rest_position()) <= MAX_RADIUS


func _begin(pointer: int, viewport_position: Vector2) -> void:
	_pointer = pointer
	_origin = rest_position()
	_update(viewport_position)


func _update(viewport_position: Vector2) -> void:
	var offset: Vector2 = _to_local(viewport_position) - _origin
	value = stick_from_offset(offset, MAX_RADIUS, DEADZONE)
	if not value.is_equal_approx(_last_direction):
		_last_direction = value
		direction_changed.emit(value)
	changed.emit(value)
	queue_redraw()


func _end() -> void:
	_pointer = NO_POINTER
	value = Vector2.ZERO
	_last_direction = Vector2.ZERO
	changed.emit(value)
	queue_redraw()
