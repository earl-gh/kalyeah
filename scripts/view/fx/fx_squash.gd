class_name FxSquash
extends RefCounted
## Squash and stretch for the skill objects, as plain maths (no nodes). Every squash keeps the object's volume: squashed
## to `s` along one axis it grows 1/sqrt(s) along the other two, so a hit never makes a rock bigger, only flatter. The
## axis factor stays inside [MIN, MAX] and a pop-in overshoots once to `POP_PEAK` (docs/ART.md "Skill effects").

## The flattest and the longest an object gets along its squash axis.
const MIN: float = 0.65
const MAX: float = 1.35
## Pop-in: 0 to `POP_PEAK` at `POP_PEAK_AT` of the way, then back to 1, over `POP_FRAMES` frames.
const POP_PEAK: float = 1.15
const POP_PEAK_AT: float = 0.6
const POP_FRAMES: int = 4
## The contact squash (the axis factor) and how many frames it holds.
const IMPACT_S: float = 0.7
const IMPACT_FRAMES: int = 2
## Smear frames: a thing at its fastest is drawn stretched along its path for `SMEAR_FRAMES` frames (full for the first two,
## then fading out), by about the distance it travels in a frame (its speed over 30, relative to its own length), at most
## `SMEAR_MAX` times as long (thin across, volume kept); every other frame it is crisp (scale exactly 1).
const SMEAR_FRAMES: int = 3
const SMEAR_MAX: float = 2.2
## After a contact squash (held `IMPACT_FRAMES` frames) the scale springs back through a small overshoot to exactly 1 over
## `SETTLE_FRAMES` frames: squash, overshoot, settle.
const SETTLE_FRAMES: int = 3
## One frame of the 30 Hz sim, in seconds.
const FRAME: float = 1.0 / 30.0


## The scale of an object squashed to `s` along Y (its up), the volume kept: (1/sqrt s, s, 1/sqrt s).
static func along(s: float) -> Vector3:
	var axis: float = clampf(s, MIN, MAX)
	var side: float = 1.0 / sqrt(axis)
	return Vector3(side, axis, side)


## A basis that stretches by `s` along `dir` and by 1/sqrt(s) across it (volume kept). Identity for a zero direction.
static func stretch_basis(dir: Vector3, s: float) -> Basis:
	if dir.length_squared() < 0.000001:
		return Basis.IDENTITY
	return _along_dir(dir, along(s))


## `scale` (x, y, z of a frame whose y runs along `dir`) as a basis in world axes.
static func _along_dir(dir: Vector3, scale: Vector3) -> Basis:
	var y: Vector3 = dir.normalized()
	var helper: Vector3 = Vector3.UP if absf(y.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	var x: Vector3 = helper.cross(y).normalized()
	var z: Vector3 = x.cross(y).normalized()
	var frame: Basis = Basis(x, y, z)
	return frame * Basis.from_scale(scale) * frame.transposed()


## How long `speed` m/s stretches an object `length` metres long on a fast frame, `frames` frames after the motion began (or
## before the contact it ends in): one frame's travel added to its length, at most `SMEAR_MAX`, fading out over
## `SMEAR_FRAMES` frames, and exactly 1 on every later (slow) frame.
static func smear_stretch(speed: float, length: float, frames: float) -> float:
	var fade: float = clampf(1.0 - maxf(frames - 1.0, 0.0) / float(SMEAR_FRAMES), 0.0, 1.0)
	var peak: float = minf(absf(speed) * FRAME / maxf(length, 0.01), SMEAR_MAX - 1.0)
	return 1.0 + peak * fade


## A basis that smears by `s` (1 to `SMEAR_MAX`) along `dir` and by 1/sqrt(s) across it (volume kept). Same as
## `stretch_basis` but not held to the squash bounds. Identity for a zero direction or `s` of 1.
static func smear_basis(dir: Vector3, s: float) -> Basis:
	if dir.length_squared() < 0.000001 or absf(s - 1.0) < 0.0001:
		return Basis.IDENTITY
	return _along_dir(dir, smear_scale(s))


## The scale of an object smeared by `s` along its Y (its up): (1/sqrt s, s, 1/sqrt s), `s` held to 1/`SMEAR_MAX`..`SMEAR_MAX`.
static func smear_scale(s: float) -> Vector3:
	var axis: float = clampf(s, 1.0 / SMEAR_MAX, SMEAR_MAX)
	return Vector3(1.0 / sqrt(axis), axis, 1.0 / sqrt(axis))


## The axis factor `frames` frames after a contact that squashed (or stretched) to `s`: `s` while held (`hold` frames), then
## a damped spring through an overshoot on the other side of 1 and exactly 1 once settled. (0.7 gives 0.7, 1.12, 0.98, 1.)
static func landing(s: float, frames: float, hold: int = IMPACT_FRAMES) -> float:
	var t: float = (frames - float(hold)) / float(SETTLE_FRAMES)
	if t >= 1.0:
		return 1.0
	if t <= 0.0:
		return s
	return 1.0 + (s - 1.0) * cos(2.5 * PI * t) * (1.0 - t) * (1.0 - t)


## Frames a landing at `hold` frames lasts until it is exactly 1 again.
static func landing_frames(hold: int = IMPACT_FRAMES) -> int:
	return hold + SETTLE_FRAMES


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
