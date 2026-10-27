class_name TiltInput
extends RefCounted
## How the phone leans, from its gravity sensor (`Input.get_gravity()`, on with `input_devices/sensors/enable_gravity`), measured from a
## "level" taken when it starts, so it works however the phone is held (lying down, sitting up). Tilt to run reads a screen direction
## from it (`direction`: only past a dead zone, and only ever a direction: the kid runs at its one speed); Home's parallax reads a lean
## (`lean`) whose level drifts back to the phone's posture, so it only answers a change.
##
## Gravity comes in the phone's own axes (x to the right edge, y to the top edge, z out of the screen) and points toward the ground.
## Lowering one edge of the phone sends gravity toward that edge: that edge is the way to run, like a marble rolling to it.

## Lean less than this from level and the kid stands still (a hand is never perfectly still).
const DEAD_ZONE_DEGREES: float = 8.0
## Gravity weaker than this means there is no sensor (a computer, or the setting off): no tilt at all.
const MIN_GRAVITY: float = 1.0
## Home's lean is full at this many degrees from level, and its level follows the phone's posture at this rate (per second).
const LEAN_FULL_DEGREES: float = 12.0
const LEAN_FOLLOW: float = 0.6
## +1: Godot reports gravity toward the ground. If a phone reports it the other way, -1 here turns every direction round.
const GRAVITY_SIGN: float = 1.0

## Tests: a gravity to use instead of the sensor's (null: the sensor).
static var test_gravity: Variant = null

## The phone's posture taken as level (unit length; zero: take the next reading as level).
var level: Vector3 = Vector3.ZERO


static func gravity() -> Vector3:
	if test_gravity is Vector3:
		return test_gravity as Vector3
	return Input.get_gravity() * GRAVITY_SIGN


## Whether this device reports its tilt (the switch for Tilt to run shows only then).
static func available() -> bool:
	return gravity().length() >= MIN_GRAVITY


## The next reading becomes level.
func reset() -> void:
	level = Vector3.ZERO


## How far the phone leans from level, on the screen (x right, y down; length = sin of the lean), or zero with no sensor. The first
## reading after `reset` is taken as level.
func offset(reading: Vector3) -> Vector2:
	if reading.length() < MIN_GRAVITY:
		return Vector2.ZERO
	var now: Vector3 = reading.normalized()
	if level == Vector3.ZERO:
		level = now
	var change: Vector3 = now - level
	# the phone's y points to its top edge, the screen's y down
	return Vector2(change.x, -change.y)


## Tilt to run: the screen direction to run (unit length), or zero while the lean from level is under the dead zone.
func direction(reading: Vector3) -> Vector2:
	var lean: Vector2 = offset(reading)
	if level == Vector3.ZERO or reading.length() < MIN_GRAVITY:
		return Vector2.ZERO
	var degrees: float = rad_to_deg(level.angle_to(reading.normalized()))
	if degrees < DEAD_ZONE_DEGREES or lean.length_squared() < 0.000001:
		return Vector2.ZERO
	return lean.normalized()


## Home's parallax: the lean as -1..1 on each axis (full at `LEAN_FULL_DEGREES`). Level follows the phone over a couple of seconds,
## so holding it still returns everything to rest.
func lean(reading: Vector3, delta: float) -> Vector2:
	var shift: Vector2 = offset(reading)
	if level == Vector3.ZERO:
		return Vector2.ZERO
	level = level.slerp(reading.normalized(), clampf(LEAN_FOLLOW * delta, 0.0, 1.0)).normalized()
	var full: float = sin(deg_to_rad(LEAN_FULL_DEGREES))
	return Vector2(clampf(shift.x / full, -1.0, 1.0), clampf(shift.y / full, -1.0, 1.0))
