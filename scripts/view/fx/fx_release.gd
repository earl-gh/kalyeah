class_name FxRelease
extends RefCounted
## The thrower's release streak (owner: "use smear frames properly for exaggeration"): the authored cast clip stays as it is, so
## the exaggeration is drawn in the world. On the frame a thrown object is first seen (the sim's cast tick) a layered
## smear (a coloured band, hot core, speed lines, fading tail: shaders/fx_smear.gdshader) runs from the thrower along the aim for `FxSquash.TRAIL_FRAMES` frames, as long as the
## object travels in `SMEAR_FRAMES + CLEAR_FRAMES` frames (its speed over 30 times the frames; `DEFAULT_LENGTH` for a toss with no
## speed). The thrower is drawn twice life size and a throw up the screen flies behind its body (the renders showed the old trail,
## which began 0.5 m out, almost entirely hidden by the kid and its health bar): so the trail starts `HAND_CLEAR` m out, clear of the
## body, and its bright head comes out past the kid's head where the stretched object is by then. Reduce motion: none.

## The trail starts this far ahead of the thrower's middle (clear of the body) and is this wide across at its widest (metres).
const HAND_CLEAR: float = 1.4
const WIDTH: float = 1.4
## A trail is a warm white, never flat white, so it reads as light and not as a scratch: the band colour of a throw with no
## colour of its own. Every skill that smears has its own (`tint_for`); the core and the speed lines are near white whatever it is,
## and none is a team red or blue.
const TINT: Color = Color(1.0, 0.96, 0.82)
const TINT_SLIPPER: Color = Color(1.0, 0.64, 0.4)
const TINT_ROCK: Color = Color(0.62, 0.45, 0.3)
const TINT_METEOR: Color = Color(1.0, 0.72, 0.2)
const TINT_CAN: Color = Color(0.4, 0.9, 0.85)
const TINT_MARBLE: Color = Color(0.55, 0.8, 1.0)
const TINT_SCISSORS: Color = Color(0.8, 0.9, 1.0)
const TINT_TOP: Color = Color(1.0, 0.8, 0.3)
const TINT_JACKS: Color = Color(0.95, 0.35, 0.8)
const TINT_PAPER: Color = Color(1.0, 0.97, 0.88)
## Weapon id -> the band colour of its smears (a weapon not listed uses `TINT`; the heal's is the guava green).
const TINTS: Dictionary[StringName, Color] = {
	&"tsinelas_light": TINT_SLIPPER,
	&"tsinelas_heavy": TINT_SLIPPER,
	&"bato_light": TINT_ROCK,
	&"bato_heavy": TINT_METEOR,
	&"lata": TINT_CAN,
	&"bola": TINT_MARBLE,
	&"gunting_light": TINT_SCISSORS,
	&"gunting_heavy": TINT_SCISSORS,
	&"trumpo": TINT_TOP,
	&"jacks": TINT_JACKS,
	&"papel_trap": TINT_PAPER,
	&"papel_shield": TINT_PAPER,
	&"heal": Palette.GUAVA,
}
## Its length bounds and the length of a toss that has no speed (metres).
const MIN_LENGTH: float = 0.6
const MAX_LENGTH: float = 4.0
const DEFAULT_LENGTH: float = 1.5
const CLEAR_FRAMES: int = 3


## The band colour of weapon `weapon`'s smears.
static func tint_for(weapon: StringName) -> Color:
	return TINTS.get(weapon, TINT)


## How long the trail of an object thrown at `speed` m/s is.
static func length_for(speed: float) -> float:
	if speed <= 0.0:
		return DEFAULT_LENGTH
	return clampf(speed * FxSquash.FRAME * float(FxSquash.SMEAR_FRAMES + CLEAR_FRAMES), MIN_LENGTH, MAX_LENGTH)


## Draws the release streak of a throw from `from` (the thrower's ground point) along `dir` at `height` metres. False when
## nothing was drawn (Reduce motion, no direction, no free smear).
static func play(ctx: FxContext, from: Vector2, dir: Vector2, speed: float, height: float, tint: Color = TINT) -> bool:
	if ctx.reduced or dir.length_squared() < 0.0001:
		return false
	var aim: Vector2 = dir.normalized()
	var start: Vector2 = from + aim * HAND_CLEAR
	var end: Vector2 = start + aim * length_for(speed)
	return ctx.ground.trail(Vector3(start.x, height, start.y), Vector3(end.x, height, end.y), WIDTH, tint, FxSquash.TRAIL_FRAMES * FxSquash.FRAME)


## Draws the trail of a fast thing whose head is at `head` (world) moving along `dir` at `speed` m/s: it runs back along the path,
## as long as the thing travels in the smear frames. False when nothing was drawn.
static func behind(ctx: FxContext, head: Vector3, dir: Vector3, speed: float, frames: int = FxSquash.TRAIL_FRAMES, width: float = WIDTH, tint: Color = TINT) -> bool:
	if ctx.reduced or dir.length_squared() < 0.0001:
		return false
	var tail: Vector3 = head - dir.normalized() * length_for(speed)
	return ctx.ground.trail(tail, head, width, tint, frames * FxSquash.FRAME)
