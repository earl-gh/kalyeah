class_name HeroVitalsPanel
extends Control
## Deadlock (`image_1`) waist-left vertical tapered HP bar placed right beside the player's left waist
## (`x ≈ 212..300, y ≈ 430..628` on 1280x720, cleanly above/right of `MoveStick` and left of the kid):
## ~15° CCW-tilted tapered vertical wedge, translucent dark backing, crisp white current-HP fill from bottom,
## coral-red damage/missing-HP band directly above it, bold white numeric HP breaking the right edge + `/100`,
## and compact status / power-up buff pips.

const SIZE: Vector2 = Vector2(88.0, 198.0)
const TRAIL_HOLD: float = 0.16
const TRAIL_DECAY_PER_S: float = 120.0

## Tapered ~15° CCW-leaning vertical wedge coordinates inside `88x198`
const TOP_L: Vector2 = Vector2(5.0, 18.0)
const TOP_R: Vector2 = Vector2(40.0, 10.0)
const BOT_L: Vector2 = Vector2(36.0, 186.0)
const BOT_R: Vector2 = Vector2(49.0, 183.0)

const BACK_FILL: Color = Color(0.06, 0.08, 0.07, 0.68)
const NUM_TAB_FILL: Color = Color(0.05, 0.07, 0.06, 0.74)
const BAR_RIM_DARK: Color = Color(0.08, 0.10, 0.09, 0.82)
const BAR_RIM_LIGHT: Color = Color(1.0, 1.0, 1.0, 0.28)
const HP_WHITE: Color = Color(0.98, 0.99, 1.0, 0.96)
const MISSING_CORAL: Color = Color(0.89, 0.24, 0.17, 0.88)
const TRAIL_AMBER: Color = Color(1.0, 0.78, 0.62, 0.92)
const REGEN_MINT: Color = Color(0.28, 0.92, 0.56, 0.92)
const MUTED_MAX: Color = Color(0.82, 0.86, 0.84, 0.72)

const BUFF_PALAMIG: Color = Color(0.26, 0.88, 0.96, 0.92)
const BUFF_TEKS: Color = Color(0.98, 0.74, 0.22, 0.92)
const BUFF_TURON: Color = Color(0.32, 0.90, 0.56, 0.92)

var _synced: bool = false
var _hp: int = 100
var _max_hp: int = 100
var _trail_hp: float = 100.0
var _trail_hold_left: float = 0.0
var _buff_type: int = 0
var _buff_left: float = 0.0
var _status_label: String = ""
var _status_col: Color = Color.WHITE
var _regen_active: bool = false


func _init() -> void:
	name = "HeroVitalsPanel"
	custom_minimum_size = SIZE
	size = SIZE
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func sync_vitals(sim: MatchSim, player: PlayerState, delta: float) -> void:
	if sim == null or sim.rules == null or player == null:
		return
	var max_hp: int = maxi(sim.rules.player_max_hp, 1)
	var cur_hp: int = clampi(player.hp, 0, max_hp)

	if not _synced:
		_synced = true
		_hp = cur_hp
		_max_hp = max_hp
		_trail_hp = float(cur_hp)
		_trail_hold_left = 0.0
	else:
		if cur_hp < _hp:
			_trail_hold_left = TRAIL_HOLD
		elif cur_hp > int(_trail_hp):
			_trail_hp = float(cur_hp)
		_hp = cur_hp
		_max_hp = max_hp
		if Settings.reduced_motion:
			_trail_hp = float(cur_hp)
			_trail_hold_left = 0.0
		elif delta > 0.0:
			if _trail_hold_left > 0.0:
				_trail_hold_left = maxf(0.0, _trail_hold_left - delta)
			elif _trail_hp > float(cur_hp):
				_trail_hp = maxf(float(cur_hp), _trail_hp - TRAIL_DECAY_PER_S * delta)

	_sync_buff_and_status(player)
	queue_redraw()


func _sync_buff_and_status(player: PlayerState) -> void:
	_buff_type = player.buff_type
	_buff_left = player.buff_left
	if _buff_left <= 0.01:
		_buff_type = 0
	_regen_active = _buff_type == PlayerState.BUFF_TURON

	var slide_left_v: float = player.slide_left
	var crouching_v: bool = player.crouching

	if player.tagged:
		_status_label = "OUT"
		_status_col = TeamColors.score_enemy() if Settings.colorblind else MISSING_CORAL
	elif not player.effects.can_cast():
		_status_label = "STUN"
		_status_col = BUFF_TEKS
	elif slide_left_v > 0.01:
		_status_label = "SLIDE"
		_status_col = BUFF_PALAMIG
	elif player.dash_left > 0.01:
		_status_label = "VAULT"
		_status_col = BUFF_PALAMIG
	elif crouching_v:
		_status_label = "LOW"
		_status_col = Color(1.0, 1.0, 1.0, 0.85)
	else:
		_status_label = ""


static func _slice_quad(t0: float, t1: float) -> PackedVector2Array:
	var a0: float = clampf(t0, 0.0, 1.0)
	var a1: float = clampf(t1, 0.0, 1.0)
	return PackedVector2Array([
		BOT_L.lerp(TOP_L, a1),
		BOT_R.lerp(TOP_R, a1),
		BOT_R.lerp(TOP_R, a0),
		BOT_L.lerp(TOP_L, a0),
	])


func _buff_color() -> Color:
	if _buff_type == 1:
		return BUFF_PALAMIG
	if _buff_type == 2:
		return BUFF_TEKS
	return BUFF_TURON


func _buff_short_name() -> String:
	if _buff_type == 1:
		return "PALAMIG"
	if _buff_type == 2:
		return "TEKS"
	return "TURON"


func _draw() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var max_f: float = maxf(float(_max_hp), 1.0)
	var live_share: float = clampf(float(_hp) / max_f, 0.0, 1.0)
	var trail_share: float = clampf(_trail_hp / max_f, live_share, 1.0)

	# 1. Translucent dark tapered backing wedge
	var full_wedge: PackedVector2Array = _slice_quad(0.0, 1.0)
	draw_colored_polygon(full_wedge, BACK_FILL)

	# 2. Coral-red missing/damage-taken band above current HP (Deadlock `image_1` signature)
	if live_share < 0.998:
		var missing_col: Color = TeamColors.BLIND_ENEMY if Settings.colorblind else MISSING_CORAL
		draw_colored_polygon(_slice_quad(live_share, 1.0), missing_col)

	# 3. Brief warm-white hit-decay band between live HP and trailing HP
	if trail_share > live_share + 0.004:
		draw_colored_polygon(_slice_quad(live_share, trail_share), TRAIL_AMBER)

	# 4. Crisp pure-white current-HP fill rising from bottom tip
	if live_share > 0.002:
		draw_colored_polygon(_slice_quad(0.0, live_share), HP_WHITE)

	# 5. Subtle emerald regen cap strip at top of wedge when Turon regen is active (like `+50` in `image_1`)
	if _regen_active:
		var cap_top_l: Vector2 = TOP_L + Vector2(-1.4, -7.0)
		var cap_top_r: Vector2 = TOP_R + Vector2(-1.4, -7.0)
		draw_colored_polygon(PackedVector2Array([cap_top_l, cap_top_r, TOP_R, TOP_L]), Color(0.05, 0.08, 0.06, 0.78))
		draw_line(TOP_L + Vector2(0.0, -1.5), TOP_R + Vector2(0.0, -1.5), REGEN_MINT, 2.4, true)
		draw_string(font, TOP_L + Vector2(3.0, -2.0), "+4", HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, REGEN_MINT)

	# 6. Delicate outer rim around the tapered wedge
	var closed: PackedVector2Array = PackedVector2Array([full_wedge[0], full_wedge[1], full_wedge[2], full_wedge[3], full_wedge[0]])
	draw_polyline(closed, BAR_RIM_DARK, 2.4, true)
	draw_polyline(closed, BAR_RIM_LIGHT, 1.1, true)

	# 7. Bold white numeric HP breaking across the right edge + muted `/100` underneath (`image_1` layout)
	var mid_r: Vector2 = BOT_R.lerp(TOP_R, 0.54)
	var hp_str: String = str(_hp)
	var hp_w: float = font.get_string_size(hp_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE).x
	var max_str: String = "/%d" % _max_hp
	var max_w: float = font.get_string_size(max_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
	var tab_w: float = maxf(hp_w, max_w) + 14.0
	var tab_x: float = mid_r.x - 10.0
	var tab_y: float = mid_r.y - 18.0
	var tab_h: float = 38.0
	var tab_poly: PackedVector2Array = PackedVector2Array([
		Vector2(tab_x, tab_y + 3.0),
		Vector2(tab_x + tab_w, tab_y),
		Vector2(tab_x + tab_w + 3.0, tab_y + tab_h - 3.0),
		Vector2(tab_x + 3.0, tab_y + tab_h),
	])
	draw_colored_polygon(tab_poly, NUM_TAB_FILL)

	var num_pos: Vector2 = Vector2(tab_x + 5.0, tab_y + 23.0)
	draw_string_outline(font, num_pos, hp_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, 3, Color(0.0, 0.0, 0.0, 0.65))
	draw_string(font, num_pos, hp_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, HP_WHITE)

	var max_pos: Vector2 = Vector2(tab_x + tab_w - max_w - 3.0, tab_y + tab_h - 2.0)
	draw_string(font, max_pos, max_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, MUTED_MAX)

	# 8. Compact status & power-up pips tucked along lower-right edge (`image_1` stat pips)
	var pip_y: float = tab_y + tab_h + 12.0
	var pip_x: float = BOT_R.lerp(TOP_R, 0.28).x + 5.0
	if _buff_type > 0:
		var b_col: Color = _buff_color()
		draw_circle(Vector2(pip_x + 4.0, pip_y - 4.0), 4.2, b_col)
		var b_txt: String = "%s %ds" % [_buff_short_name(), ceili(_buff_left)]
		draw_string_outline(font, Vector2(pip_x + 11.0, pip_y), b_txt, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, 2, Color(0.0, 0.0, 0.0, 0.6))
		draw_string(font, Vector2(pip_x + 11.0, pip_y), b_txt, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, b_col)
		pip_y += 16.0

	if not _status_label.is_empty():
		draw_circle(Vector2(pip_x + 4.0, pip_y - 4.0), 3.4, _status_col)
		draw_string_outline(font, Vector2(pip_x + 11.0, pip_y), _status_label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, 2, Color(0.0, 0.0, 0.0, 0.6))
		draw_string(font, Vector2(pip_x + 11.0, pip_y), _status_label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, _status_col)
