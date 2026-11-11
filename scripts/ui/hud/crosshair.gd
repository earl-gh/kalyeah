class_name Crosshair
extends Control
## Valorant-crisp 2D 90s Filipino Komiks shoulder reticle:
## 4-tick inked reticle + center dot, dynamic bloom when sprinting/firing, barrio-gold lock-on when holding a superpower,
## animated crimson/gold `X` hitmarker (`flash_hit`, `kick_recoil`), and subtle peripheral Arcane/komiks speed-ink lines when sprinting.

const RADIUS: float = 14.0
const DOT: float = 3.0
const WIDTH: float = 3.0
const COLOR: Color = Color("f5ebd6")
const HELD: Color = Color("ffbe2e")
const SHADOW: Color = Color("141016")

const INK: Color = Color("141016")
const PAPER: Color = Color("f5ebd6")
const GOLD: Color = Color("ffbe2e")
const CRIMSON: Color = Color("f23838")

const BASE_GAP: float = 5.0
const TICK_LEN: float = 8.0
const HIT_TIME_NORMAL: float = 0.22
const HIT_TIME_LETHAL: float = 0.36

## A superpower is held: reticle locks on in barrio gold.
var held: bool = false:
	set(value):
		if value != held:
			held = value
			_wake()

## Local player is moving at Athletic Street Sprint speed.
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
var _sprint_alpha: float = 0.0
var _speed_phase: float = 0.0


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
	_recoil = clampf(_recoil + maxf(amount, 0.0) * 0.65, 0.0, 1.6)
	_wake()


func _wake() -> void:
	set_process(true)
	queue_redraw()


func _target_bloom() -> float:
	var target: float = (5.5 if sprinting else 0.0) + _recoil * 8.5 - (1.5 if held else 0.0)
	return clampf(target, -1.5, 16.0)


func _process(delta: float) -> void:
	var target_b: float = _target_bloom()
	if Settings.reduced_motion:
		_bloom = target_b
		_recoil = maxf(0.0, _recoil - delta * 6.0)
		_sprint_alpha = 0.0
	else:
		_bloom = move_toward(_bloom, target_b, delta * 54.0)
		_recoil = maxf(0.0, _recoil - delta * 4.8)
		var want_lines: float = 1.0 if (sprinting and Settings.performance != &"turbo") else 0.0
		_sprint_alpha = move_toward(_sprint_alpha, want_lines, delta * 5.5)
		if _sprint_alpha > 0.01:
			_speed_phase = fposmod(_speed_phase + delta * 9.0, TAU)

	if _hit_timer > 0.0:
		_hit_timer = maxf(0.0, _hit_timer - delta)

	queue_redraw()
	var settled: bool = is_equal_approx(_bloom, target_b) and _recoil <= 0.001 and _hit_timer <= 0.001
	var lines_idle: bool = (not sprinting and _sprint_alpha <= 0.001) or Settings.reduced_motion or Settings.performance == &"turbo"
	if settled and lines_idle:
		set_process(false)


func _draw() -> void:
	if _sprint_alpha > 0.01 and size.x > 200.0 and size.y > 200.0:
		_draw_peripheral_speed_ink()

	var middle: Vector2 = size * 0.5
	var core_col: Color = HELD if held else COLOR
	var gap: float = maxf(BASE_GAP + _bloom, 3.0)

	# 4-tick Valorant-crisp inked reticle
	for dir: Vector2 in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT]:
		var inner_pt: Vector2 = middle + dir * gap
		var outer_pt: Vector2 = middle + dir * (gap + TICK_LEN)
		draw_line(inner_pt - dir * 1.2, outer_pt + dir * 1.2, INK, WIDTH + 2.4, true)
		draw_line(inner_pt, outer_pt, core_col, WIDTH - 0.6, true)

	# Center dot
	draw_circle(middle, DOT + 1.2, INK)
	draw_circle(middle, DOT - 0.6, core_col)

	# Barrio-gold komiks lock-on brackets while holding a superpower
	if held:
		var lock_r: float = RADIUS + 3.0 + maxf(_bloom * 0.35, 0.0)
		var arm: float = 5.5
		for corner_dir: Vector2 in [Vector2(-1.0, -1.0), Vector2(1.0, -1.0), Vector2(1.0, 1.0), Vector2(-1.0, 1.0)]:
			var c_pt: Vector2 = middle + corner_dir * lock_r
			var h_pt: Vector2 = c_pt + Vector2(-corner_dir.x * arm, 0.0)
			var v_pt: Vector2 = c_pt + Vector2(0.0, -corner_dir.y * arm)
			draw_line(c_pt, h_pt, INK, 4.2, true)
			draw_line(c_pt, v_pt, INK, 4.2, true)
			draw_line(c_pt, h_pt, GOLD, 2.2, true)
			draw_line(c_pt, v_pt, GOLD, 2.2, true)

	# Animated crimson / barrio-gold `X` hitmarker
	if _hit_timer > 0.0:
		_draw_hitmarker(middle)


func _draw_hitmarker(middle: Vector2) -> void:
	var progress: float = 1.0 - clampf(_hit_timer / maxf(_hit_duration, 0.001), 0.0, 1.0)
	var alpha: float = clampf(1.0 - progress * progress, 0.0, 1.0)
	var spread: float = 0.0 if Settings.reduced_motion else sin(progress * PI) * 3.5
	var inner_d: float = 5.5 + spread * 0.5
	var outer_d: float = (16.5 if _hit_lethal else 12.5) + spread
	var hit_col: Color = GOLD if _hit_lethal else (TeamColors.score_enemy() if Settings.colorblind else CRIMSON)
	var accent_col: Color = Color(hit_col, alpha)
	var ink_col: Color = Color(INK, alpha)
	var stroke: float = 3.2 if _hit_lethal else 2.4

	for diag: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0), Vector2(-1.0, 1.0), Vector2(-1.0, -1.0)]:
		var u: Vector2 = diag.normalized()
		var p0: Vector2 = middle + u * inner_d
		var p1: Vector2 = middle + u * outer_d
		draw_line(p0 - u * 1.0, p1 + u * 1.0, ink_col, stroke + 2.4, true)
		draw_line(p0, p1, accent_col, stroke, true)
		if _hit_lethal:
			var side: Vector2 = Vector2(-u.y, u.x) * 2.6
			draw_line(p0 + side, p1 + side, Color(CRIMSON, alpha * 0.85), 1.6, true)


func _draw_peripheral_speed_ink() -> void:
	var center: Vector2 = size * 0.5
	var corners: Array[Vector2] = [
		Vector2(0.0, 0.0),
		Vector2(size.x, 0.0),
		Vector2(size.x, size.y),
		Vector2(0.0, size.y),
	]
	var ink_a: Color = Color(INK, 0.42 * _sprint_alpha)
	var cream_a: Color = Color(PAPER, 0.28 * _sprint_alpha)
	for c_idx: int in corners.size():
		var corner_pt: Vector2 = corners[c_idx]
		var to_center: Vector2 = (center - corner_pt).normalized()
		var perp: Vector2 = Vector2(-to_center.y, to_center.x)
		for s_idx: int in range(-2, 3):
			var flutter: float = sin(_speed_phase + float(c_idx) * 1.7 + float(s_idx) * 1.3)
			var lateral: float = float(s_idx) * 26.0 + flutter * 4.0
			var start_pt: Vector2 = corner_pt + perp * lateral + to_center * (10.0 + absf(float(s_idx)) * 8.0)
			var len_px: float = 68.0 - absf(float(s_idx)) * 10.0 + flutter * 10.0
			var end_pt: Vector2 = start_pt + to_center * len_px
			var tip_side: Vector2 = perp * 2.2
			var slash_ink: PackedVector2Array = PackedVector2Array([
				start_pt - tip_side,
				start_pt + tip_side,
				end_pt,
			])
			draw_colored_polygon(slash_ink, ink_a)
			if s_idx % 2 == 0:
				draw_line(start_pt, start_pt + to_center * (len_px * 0.72), cream_a, 1.4, true)
