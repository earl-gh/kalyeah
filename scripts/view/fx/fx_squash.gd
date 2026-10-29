class_name FxSquash
extends RefCounted
## Squash and stretch for the skill objects, as plain maths (no nodes). Every squash keeps the object's volume: squashed
## to `s` along one axis it grows 1/sqrt(s) along the other two, so a hit never makes a rock bigger, only flatter. The
## axis factor stays inside [MIN, MAX] and a pop-in overshoots once to `POP_PEAK` (docs/ART.md "Skill effects").

## The flattest and the longest an object gets along its squash axis.
const MIN: float = 0.65
const MAX: float = 1.35
## A fast object stretches along its travel by its speed over `STRETCH_SPEED` m/s, at most `STRETCH_MAX` (a rock at
## 14 m/s is 1.30 long, the heavy slipper at 9 m/s 1.20).
const STRETCH_MAX: float = 0.3
const STRETCH_SPEED: float = 45.0
## Pop-in: 0 to `POP_PEAK` at `POP_PEAK_AT` of the way, then back to 1, over `POP_FRAMES` frames.
const POP_PEAK: float = 1.15
const POP_PEAK_AT: float = 0.6
const POP_FRAMES: int = 4
## The contact squash (the axis factor) and how many frames it holds.
const IMPACT_S: float = 0.7
const IMPACT_FRAMES: int = 2
## One frame of the 30 Hz sim, in seconds.
const FRAME: float = 1.0 / 30.0


## The scale of an object squashed to `s` along Y (its up), the volume kept: (1/sqrt s, s, 1/sqrt s).
static func along(s: float) -> Vector3:
	var axis: float = clampf(s, MIN, MAX)
	var side: float = 1.0 / sqrt(axis)
	return Vector3(side, axis, side)


## How long a thing flying at `speed` m/s is drawn along its travel (1 when still).
static func velocity_stretch(speed: float) -> float:
	return 1.0 + minf(STRETCH_MAX, absf(speed) / STRETCH_SPEED)


## A basis that stretches by `s` along `dir` and by 1/sqrt(s) across it (volume kept). Identity for a zero direction.
static func stretch_basis(dir: Vector3, s: float) -> Basis:
	if dir.length_squared() < 0.000001:
		return Basis.IDENTITY
	var scale: Vector3 = along(s)
	var y: Vector3 = dir.normalized()
	var helper: Vector3 = Vector3.UP if absf(y.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	var x: Vector3 = helper.cross(y).normalized()
	var z: Vector3 = x.cross(y).normalized()
	var frame: Basis = Basis(x, y, z)
	return frame * Basis.from_scale(scale) * frame.transposed()


## Pop-in at `t` (0..1 of `POP_FRAMES`): 0 -> `POP_PEAK` -> 1.
static func pop(t: float) -> float:
	return overshoot(t, POP_PEAK)


## A rise from 0 that overshoots once to `peak` at `POP_PEAK_AT` of the way and settles on 1 at the end.
static func overshoot(t: float, peak: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	if x < POP_PEAK_AT:
		return peak * sin(x / POP_PEAK_AT * PI / 2.0)
	var settle: float = (x - POP_PEAK_AT) / (1.0 - POP_PEAK_AT)
	return lerpf(peak, 1.0, settle * settle * (3.0 - 2.0 * settle))


## `seconds` floored to whole 2-frame steps: decoration animates on 2s for a hand-made feel (sim travel stays smooth).
static func step2(seconds: float) -> float:
	return floorf(seconds / (2.0 * FRAME) + 0.0001) * 2.0 * FRAME
