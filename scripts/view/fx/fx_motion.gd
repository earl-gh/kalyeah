class_name FxMotion
extends RefCounted
## The pose curves of the skill objects, as plain maths (no nodes): the meteor's fall, a toss arc, the marble's bounces, the
## spinning top's wobble, the scissors' snips, the paper wall's unfold. Each is a function of progress or frames
## so the same curve plays offline and online (docs/ART.md "Skill effects").

## The meteor starts this far off its circle, up and behind (it never crosses in front of a face).
const METEOR_FROM: Vector3 = Vector3(1.5, 12.0, -1.5)
## The marble: each bounce arcs this high (metres) over its segment (low enough that the ball's top stays under the kids'
## faces, 2.4 m; owner's call, was 1.8); the throw leaves the hand at `BOLA_HAND_Y`.
const BOLA_ARC: float = 1.2
const BOLA_HAND_Y: float = 2.0
## The spinning top leans this much (degrees) and its lean goes round `TOP_PRECESS` times a second; a hit kicks the lean
## up by `TOP_SPIKE` degrees, dying away over `TOP_SPIKE_FRAMES` frames.
const TOP_TILT: float = 8.0
const TOP_PRECESS: float = 3.0
const TOP_SPIKE: float = 10.0
const TOP_SPIKE_FRAMES: int = 6
## A new scissors is launched every `SNIP_FRAMES / n` frames (between `SNIP_MIN_PERIOD` and `SNIP_MAX_PERIOD`): all snips fit in ~half a second.
const SNIP_FRAMES: int = 16
const SNIP_MIN_PERIOD: int = 2
const SNIP_MAX_PERIOD: int = 5
## A small scissors flies from over the caster's shoulder to the strike point in this many frames (the sim's snip hits at the cast,
## so this is the least lateness the view can show; ~0.1 s).
const SNIP_FLIGHT_FRAMES: int = 3
## The paper wall unfolds from `UNFOLD_FROM` tall and `UNFOLD_WIDTH_FROM` wide, overshooting to `UNFOLD_PEAK` tall.
const UNFOLD_FROM: float = 0.05
const UNFOLD_WIDTH_FROM: float = 0.85
const UNFOLD_PEAK: float = 1.12
const FRAME: float = 1.0 / 30.0


## The meteor's offset from its circle at fall progress `p` (0 cast, 1 landed): an ease-in, so it speeds up.
static func meteor_offset(p: float) -> Vector3:
	var x: float = clampf(p, 0.0, 1.0)
	return METEOR_FROM * (1.0 - x * x)


## A toss from `from` to `to` at progress `p`, arcing `peak` metres above the straight line at its middle.
static func toss(from: Vector3, to: Vector3, peak: float, p: float) -> Vector3:
	var x: float = clampf(p, 0.0, 1.0)
	return from.lerp(to, x) + Vector3(0.0, peak * 4.0 * x * (1.0 - x), 0.0)


## The marble's centre height on bounce segment `seg` at progress `p` (0..1) for a marble of radius `r`: every bounce arcs
## `BOLA_ARC` over its segment; the first leaves the hand at `y0` (never above a bounce's top, so the ball stays under the
## kids' faces) and falls to its first bounce; every segment touches the street (centre at `r`) at its end.
static func bola_height(seg: int, p: float, y0: float, r: float) -> float:
	var x: float = clampf(p, 0.0, 1.0)
	if seg <= 0:
		return r + (minf(y0, r + BOLA_ARC) - r) * (1.0 - x * x)
	return r + BOLA_ARC * 4.0 * x * (1.0 - x)


## How fast the marble's height changes per unit of segment progress at `p` (for its stretch along its velocity).
static func bola_rise(seg: int, p: float, y0: float, r: float) -> float:
	var x: float = clampf(p, 0.0, 1.0)
	if seg <= 0:
		return -(minf(y0, r + BOLA_ARC) - r) * 2.0 * x
	return BOLA_ARC * 4.0 * (1.0 - 2.0 * x)


## The spinning top's lean at view time `t` (seconds), `spike_age` seconds after its last hit (negative: none). The lean
## axis goes round the top; the tip stays where it is (the caller pivots at the tip).
static func top_tilt(t: float, spike_age: float) -> Basis:
	var lean: float = TOP_TILT
	if spike_age >= 0.0 and spike_age < TOP_SPIKE_FRAMES * FRAME:
		lean += TOP_SPIKE * (1.0 - spike_age / (TOP_SPIKE_FRAMES * FRAME))
	var a: float = TAU * TOP_PRECESS * t
	return Basis(Vector3(cos(a), 0.0, sin(a)), deg_to_rad(lean))


## Frames per snip for `n` snips.
static func snip_period(n: int) -> int:
	return clampi(floori(float(SNIP_FRAMES) / float(maxi(n, 1))), SNIP_MIN_PERIOD, SNIP_MAX_PERIOD)


## The blade angle (degrees) of a small scissors `t` (0..1) of the way through its flight: open wide at the launch, shutting faster
## and faster (ease-in, so it snaps shut as it arrives) and shut on arrival: the cut.
static func cut_angle(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	return lerpf(ScissorsRig.WIDE, ScissorsRig.CLOSED, x * x)


## Frames from the cast to the last scissors of `n` arriving shut (the first is launched at the cast, each next one `snip_period`
## frames after the one before; each flies `SNIP_FLIGHT_FRAMES`).
static func snip_frames(n: int) -> int:
	return SNIP_FLIGHT_FRAMES + (n - 1) * snip_period(n)


## The paper wall's (width, height) scale `t` (0..1) through its unfold: low and narrow, up past its height, settled.
static func unfold(t: float) -> Vector2:
	var x: float = clampf(t, 0.0, 1.0)
	var rise: float = FxSquash.overshoot(x, UNFOLD_PEAK)
	return Vector2(lerpf(UNFOLD_WIDTH_FROM, 1.0, minf(x * 1.5, 1.0)), lerpf(UNFOLD_FROM, 1.0, rise))
