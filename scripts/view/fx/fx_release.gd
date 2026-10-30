class_name FxRelease
extends RefCounted
## The thrower's release streak (owner: "use smear frames properly for exaggeration"): the authored cast clip stays as it is, so
## the exaggeration is drawn in the world. On the frame a thrown object is first seen (the sim's cast tick) a tapered white
## trail, lighter toward the hand, runs from the thrower along the aim for `FxSquash.TRAIL_FRAMES` frames, as long as the
## object travels in `SMEAR_FRAMES + CLEAR_FRAMES` frames (its speed over 30 times the frames; `DEFAULT_LENGTH` for a toss with no
## speed). The thrower is drawn twice life size, so up the screen the first metres of the trail are behind its body: the extra
## frames let the trail's bright head come out past the kid's head, where the stretched object is by then. Reduce motion: none.

## The trail starts this far ahead of the thrower's middle (at the hand, never over the body) and is this wide across at its middle (metres).
const HAND_CLEAR: float = 0.5
const WIDTH: float = 0.9
## A trail is a warm white, never flat white, so it reads as light and not as a scratch.
const TINT: Color = Color(1.0, 0.96, 0.82)
## Its length bounds and the length of a toss that has no speed (metres).
const MIN_LENGTH: float = 0.6
const MAX_LENGTH: float = 4.0
const DEFAULT_LENGTH: float = 1.5
const CLEAR_FRAMES: int = 2


## How long the trail of an object thrown at `speed` m/s is.
static func length_for(speed: float) -> float:
	if speed <= 0.0:
		return DEFAULT_LENGTH
	return clampf(speed * FxSquash.FRAME * float(FxSquash.SMEAR_FRAMES + CLEAR_FRAMES), MIN_LENGTH, MAX_LENGTH)


## Draws the release streak of a throw from `from` (the thrower's ground point) along `dir` at `height` metres. False when
## nothing was drawn (Reduce motion, no direction, no free smear).
static func play(ctx: FxContext, from: Vector2, dir: Vector2, speed: float, height: float) -> bool:
	if ctx.reduced or dir.length_squared() < 0.0001:
		return false
	var aim: Vector2 = dir.normalized()
	var start: Vector2 = from + aim * HAND_CLEAR
	var end: Vector2 = start + aim * length_for(speed)
	return ctx.ground.trail(Vector3(start.x, height, start.y), Vector3(end.x, height, end.y), WIDTH, TINT, FxSquash.TRAIL_FRAMES * FxSquash.FRAME)


## Draws the trail of a fast thing whose head is at `head` (world) moving along `dir` at `speed` m/s: it runs back along the path,
## as long as the thing travels in the smear frames. False when nothing was drawn.
static func behind(ctx: FxContext, head: Vector3, dir: Vector3, speed: float, frames: int = FxSquash.TRAIL_FRAMES, width: float = WIDTH) -> bool:
	if ctx.reduced or dir.length_squared() < 0.0001:
		return false
	var tail: Vector3 = head - dir.normalized() * length_for(speed)
	return ctx.ground.trail(tail, head, width, TINT, frames * FxSquash.FRAME)
