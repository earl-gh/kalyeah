class_name HurtPulse
extends Control
## A red glow round the screen edge when you are hit, stronger when you are tagged, with a short buzz on a
## phone. The street shakes already; this says it in the corner of the eye, where the stick and the buttons are.
## Reduce motion keeps it but fainter and shorter. It only listens; it never takes a touch.

## How long the glow lasts, in seconds, and how deep it reaches in from each edge.
const HIT_TIME: float = 0.4
const TAG_TIME: float = 0.9
const DEPTH: float = 150.0
## Peak opacity of a light hit and of a heavy one (a hit that takes this share of health or more is heavy).
const HIT_LIGHT: float = 0.2
const HIT_HEAVY: float = 0.48
const HEAVY_SHARE: float = 0.3
const TAG_PEAK: float = 0.7
## Reduce motion: fainter and shorter, with no ramp.
const REDUCED_PEAK: float = 0.5
const REDUCED_TIME: float = 0.6
const COLOR: Color = Color(0.92, 0.1, 0.08)
## Phone buzz lengths in milliseconds, and the least gap between two buzzes (a burning field would never stop).
const BUZZ_HIT_MS: int = 35
const BUZZ_TAG_MS: int = 140
const BUZZ_GAP: float = 0.25

## Opacity at the edge right now (0 = nothing shown). Tests read this.
var strength: float = 0.0

## How many buzzes were asked of the phone (tests; the phone itself only buzzes where it can).
var buzzes: int = 0

var _peak: float = 0.0
var _left: float = 0.0
var _time: float = HIT_TIME
var _buzz_wait: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## You took `amount` damage out of `max_health`.
func hit(amount: int, max_health: int) -> void:
	var share: float = clampf(float(amount) / float(maxi(max_health, 1)) / HEAVY_SHARE, 0.0, 1.0)
	_flash(lerpf(HIT_LIGHT, HIT_HEAVY, share), HIT_TIME, BUZZ_HIT_MS)


## You were tagged and sent to the box.
func tagged() -> void:
	_flash(TAG_PEAK, TAG_TIME, BUZZ_TAG_MS)


## True while a glow is showing.
func active() -> bool:
	return strength > 0.0


func tick(delta: float) -> void:
	_buzz_wait = maxf(0.0, _buzz_wait - delta)
	if _left <= 0.0:
		return
	_left = maxf(0.0, _left - delta)
	strength = _peak if Settings.reduced_motion and _left > 0.0 else _peak * _left / _time
	queue_redraw()


func _process(delta: float) -> void:
	tick(delta)


func _flash(peak: float, time: float, buzz_ms: int) -> void:
	if Settings.reduced_motion:
		peak *= REDUCED_PEAK
		time = minf(time, REDUCED_TIME)
	# a lighter hit never cuts a stronger glow short
	if peak >= strength:
		_peak = peak
		_time = time
		_left = time
		strength = peak
		queue_redraw()
	if _buzz_wait <= 0.0 or buzz_ms >= BUZZ_TAG_MS:
		buzzes += 1
		Settings.vibrate(buzz_ms)
		_buzz_wait = BUZZ_GAP


func _draw() -> void:
	if strength <= 0.0:
		return
	var edge: Color = Color(COLOR, strength)
	var clear: Color = Color(COLOR, 0.0)
	var w: float = size.x
	var h: float = size.y
	var d: float = DEPTH
	var colors: PackedColorArray = PackedColorArray([edge, edge, clear, clear])
	draw_polygon(PackedVector2Array([Vector2(0, 0), Vector2(w, 0), Vector2(w, d), Vector2(0, d)]), colors)
	draw_polygon(PackedVector2Array([Vector2(0, h), Vector2(w, h), Vector2(w, h - d), Vector2(0, h - d)]), colors)
	draw_polygon(PackedVector2Array([Vector2(0, 0), Vector2(0, h), Vector2(d, h), Vector2(d, 0)]), colors)
	draw_polygon(PackedVector2Array([Vector2(w, 0), Vector2(w, h), Vector2(w - d, h), Vector2(w - d, 0)]), colors)
