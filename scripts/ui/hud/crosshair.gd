class_name Crosshair
extends Control
## Delicate Deadlock (`image_1`) shoulder reticle:
## Thin translucent white circle + 4 fine hairlines + tiny center dot, subtle movement/recoil bloom,
## clean lock-on tighten when holding an ability, and crisp minimal `X` hitmarker (`flash_hit`, `kick_recoil`).

const RADIUS: float = 13.0
const DOT: float = 1.6
const WIDTH: float = 1.4
const COLOR: Color = Color(1.0, 1.0, 1.0, 0.78)
const RING_COLOR: Color = Color(1.0, 1.0, 1.0, 0.44)
const HELD: Color = Color(1.0, 0.92, 0.68, 0.92)
const SHADOW: Color = Color(0.0, 0.0, 0.0, 0.36)

const HIT_CORAL: Color = Color(0.95, 0.34, 0.26, 0.92)
const HIT_GOLD: Color = Color(1.0, 0.84, 0.34, 0.96)

const BASE_GAP: float = 5.5
const TICK_LEN: float = 5.5
const HIT_TIME_NORMAL: float = 0.20
const HIT_TIME_LETHAL: float = 0.32

## An ability slot is held: reticle tightens slightly.
var held: bool = false:
	set(value):
		if value != held:
			held = value
			_wake()

## Local player is moving fast / running.
var sprinting: bool = false:
	set(value):
		if value != sprinting:
			sprinting = value
			_wake()

var _bloom: float = 0.0
var _recoil: float = 0.0
var _hit_timer: float = 0.0
var _hit_duration: float = HIT_TIME_NORMAL
var _hit_lethal: bool = false


func _ready() -> void:
	name = "Crosshair"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(queue_redraw)
	set_process(false)


func flash_hit(lethal: bool = false) -> void:
	_hit_lethal = lethal
	_hit_duration = HIT_TIME_LETHAL if lethal else HIT_TIME_NORMAL
	_hit_timer = _hit_duration
	_wake()


func kick_recoil(amount: float = 1.0) -> void:
	_recoil = clampf(_recoil + maxf(amount, 0.0) * 0.55, 0.0, 1.4)
	_wake()


func _wake() -> void:
	set_process(true)
	queue_redraw()


func _target_bloom() -> float:
	var target: float = (3.0 if sprinting else 0.0) + _recoil * 6.5 - (1.5 if held else 0.0)
	return clampf(target, -1.5, 12.0)


func _process(delta: float) -> void:
	var target_b: float = _target_bloom()
	if Settings.reduced_motion:
		_bloom = target_b
		_recoil = maxf(0.0, _recoil - delta * 6.0)
	else:
		_bloom = move_toward(_bloom, target_b, delta * 48.0)
		_recoil = maxf(0.0, _recoil - delta * 5.0)

	if _hit_timer > 0.0:
		_hit_timer = maxf(0.0, _hit_timer - delta)

	queue_redraw()
	if is_equal_approx(_bloom, target_b) and _recoil <= 0.001 and _hit_timer <= 0.001:
		set_process(false)


func _draw() -> void:
	var middle: Vector2 = size * 0.5
	var core_col: Color = HELD if held else COLOR
	var ring_r: float = maxf(RADIUS + _bloom * 0.65, 9.0)
	var gap: float = maxf(BASE_GAP + _bloom * 0.55, 3.5)

	# Delicate thin Deadlock outer circle (`image_1`)
	draw_arc(middle, ring_r, 0.0, TAU, 40, SHADOW, WIDTH + 1.0, true)
	draw_arc(middle, ring_r, 0.0, TAU, 40, HELD if held else RING_COLOR, WIDTH, true)

	# 4 delicate cardinal hairlines bridging the circle
	for dir: Vector2 in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT]:
		var inner_pt: Vector2 = middle + dir * gap
		var outer_pt: Vector2 = middle + dir * (gap + TICK_LEN)
		draw_line(inner_pt, outer_pt, SHADOW, WIDTH + 1.2, true)
		draw_line(inner_pt, outer_pt, core_col, WIDTH, true)

	# Tiny crisp center dot
	draw_circle(middle, DOT + 0.6, SHADOW)
	draw_circle(middle, DOT, core_col)

	if _hit_timer > 0.0:
		_draw_hitmarker(middle)


func _draw_hitmarker(middle: Vector2) -> void:
	var progress: float = 1.0 - clampf(_hit_timer / maxf(_hit_duration, 0.001), 0.0, 1.0)
	var alpha: float = clampf(1.0 - progress * progress, 0.0, 1.0)
	var spread: float = 0.0 if Settings.reduced_motion else sin(progress * PI) * 2.5
	var inner_d: float = 5.0 + spread * 0.4
	var outer_d: float = (13.5 if _hit_lethal else 10.5) + spread
	var base_hit: Color = HIT_GOLD if _hit_lethal else (TeamColors.score_enemy() if Settings.colorblind else HIT_CORAL)
	var accent_col: Color = Color(base_hit.r, base_hit.g, base_hit.b, alpha)
	var sh_col: Color = Color(0.0, 0.0, 0.0, alpha * 0.45)
	var stroke: float = 2.0 if _hit_lethal else 1.6

	for diag: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0), Vector2(-1.0, 1.0), Vector2(-1.0, -1.0)]:
		var u: Vector2 = diag.normalized()
		var p0: Vector2 = middle + u * inner_d
		var p1: Vector2 = middle + u * outer_d
		draw_line(p0, p1, sh_col, stroke + 1.4, true)
		draw_line(p0, p1, accent_col, stroke, true)
