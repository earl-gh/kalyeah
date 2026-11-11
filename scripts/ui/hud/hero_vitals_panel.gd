class_name HeroVitalsPanel
extends Control
## Bottom-left (next to MoveStick) 2D 90s Filipino Komiks hero card:
## KidModel.minimap_face portrait, Kid Name, bold `100 / 100 HP` bar with white damage-decay trail, and SPRINT / status badge.

const SIZE: Vector2 = Vector2(252.0, 84.0)
const SLANT: float = 8.0
const PORTRAIT_SIZE: float = 64.0
const TRAIL_HOLD: float = 0.18
const TRAIL_DECAY_PER_S: float = 115.0

const INK: Color = Color("141016")
const SHADOW: Color = Color(0.08, 0.06, 0.09, 0.84)
const PAPER: Color = Color("f5ebd6")
const GOLD: Color = Color("ffbe2e")
const COBALT: Color = Color("2b7bff")
const CRIMSON: Color = Color("f23838")
const CYAN: Color = Color("2ce6ff")
const HP_GREEN: Color = Color("2ee068")
const TRAIL_WHITE: Color = Color("fffdf7")
const BAR_EMPTY: Color = Color("221c26")

var _synced: bool = false
var _char_id: StringName = &""
var _hero_name: String = "KID"
var _face: Texture2D = null
var _hp: int = 100
var _max_hp: int = 100
var _trail_hp: float = 100.0
var _trail_hold_left: float = 0.0
var _badge_text: String = ""
var _badge_fill: Color = GOLD
var _tagged: bool = false
var _pulse: float = 0.0


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

	if not _synced or _char_id != player.character_id:
		_char_id = player.character_id
		_face = KidModel.minimap_face(_char_id)
		if _face == null:
			_face = KidModel.portrait(_char_id)
		_hero_name = _resolve_kid_name(sim.rules, _char_id)

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

	_tagged = player.tagged
	_pulse = sim.match_time
	_update_badge(player)
	queue_redraw()


static func _resolve_kid_name(rules: GameRules, char_id: StringName) -> String:
	for def: CharacterDef in rules.characters:
		if def != null and def.id == char_id and not def.display_name.is_empty():
			return def.display_name.to_upper()
	var fallback: String = String(char_id).strip_edges().to_upper()
	return fallback if not fallback.is_empty() else "BIDA"


func _update_badge(player: PlayerState) -> void:
	if player.tagged:
		_badge_text = "TAGGED"
		_badge_fill = TeamColors.score_enemy() if Settings.colorblind else CRIMSON
		return
	if not player.effects.can_cast():
		_badge_text = "STUN!"
		_badge_fill = GOLD
		return
	if player.dash_left > 0.0:
		_badge_text = "TINIK!"
		_badge_fill = CYAN
		return
	if player.sprinting:
		_badge_text = "SPRINT"
		_badge_fill = GOLD
		return
	_badge_text = ""


func _hp_fill_color(share: float) -> Color:
	if Settings.colorblind:
		return TeamColors.BLIND_OWN if share > 0.3 else TeamColors.BLIND_ENEMY
	if share <= 0.25:
		var flash: float = 0.0 if Settings.reduced_motion else (0.5 + 0.5 * sin(_pulse * 10.0))
		return CRIMSON.lightened(flash * 0.22)
	if share <= 0.55:
		return GOLD
	return HP_GREEN


func _draw() -> void:
	var w: float = size.x - 4.0
	var h: float = size.y - 4.0
	var font: Font = KalyeahTheme.ACTION_FONT

	# Slanted komiks card shadow + warm newsprint plate
	var sh: PackedVector2Array = PackedVector2Array([
		Vector2(SLANT + 3.0, 4.0),
		Vector2(w + 3.0, 4.0),
		Vector2(w - SLANT + 3.0, h + 4.0),
		Vector2(3.0, h + 4.0),
	])
	draw_colored_polygon(sh, SHADOW)

	var plate: PackedVector2Array = PackedVector2Array([
		Vector2(SLANT, 0.0),
		Vector2(w, 0.0),
		Vector2(w - SLANT, h),
		Vector2(0.0, h),
	])
	draw_colored_polygon(plate, PAPER)
	draw_polyline(PackedVector2Array([plate[0], plate[1], plate[2], plate[3], plate[0]]), INK, 3.0, true)

	# Portrait frame on left (`KidModel.minimap_face` + barrio gold backing)
	var px: float = 10.0
	var py: float = (h - PORTRAIT_SIZE) * 0.5
	var p_rect: Rect2 = Rect2(Vector2(px, py), Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE))
	draw_rect(p_rect.grow(2.5), INK, true)
	draw_rect(p_rect, GOLD if not _tagged else BAR_EMPTY, true)
	var team_col: Color = TeamColors.score_own() if Settings.colorblind else COBALT
	draw_colored_polygon(PackedVector2Array([
		p_rect.position,
		p_rect.position + Vector2(24.0, 0.0),
		p_rect.position + Vector2(0.0, 24.0),
	]), team_col)
	if _face != null:
		var mod: Color = Color(0.45, 0.45, 0.48, 0.9) if _tagged else Color.WHITE
		draw_texture_rect(_face, p_rect.grow(-1.5), false, mod)
	else:
		var initial: String = _hero_name.substr(0, 1)
		var iw: float = font.get_string_size(initial, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE).x
		var ib: Vector2 = Vector2(p_rect.get_center().x - iw * 0.5, p_rect.get_center().y + 12.0)
		draw_string_outline(font, ib, initial, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, 5, INK)
		draw_string(font, ib, initial, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, PAPER)
	draw_rect(p_rect, INK, false, 2.5)

	# Kid Name + SPRINT / Status badge on top row
	var right_x: float = p_rect.end.x + 10.0
	var content_w: float = (w - SLANT - 8.0) - right_x
	var name_max_w: float = content_w - (70.0 if not _badge_text.is_empty() else 0.0)
	draw_string(font, Vector2(right_x, 28.0), _hero_name, HORIZONTAL_ALIGNMENT_LEFT, maxf(name_max_w, 48.0), HudStyle.LABEL, INK)

	if not _badge_text.is_empty():
		var bw: float = font.get_string_size(_badge_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x + 12.0
		var bx: float = w - SLANT - bw - 6.0
		var by: float = 8.0
		var bh: float = 21.0
		var bs: float = 4.0
		var badge_poly: PackedVector2Array = PackedVector2Array([
			Vector2(bx + bs, by),
			Vector2(bx + bw, by),
			Vector2(bx + bw - bs, by + bh),
			Vector2(bx, by + bh),
		])
		draw_colored_polygon(badge_poly, _badge_fill)
		draw_polyline(PackedVector2Array([badge_poly[0], badge_poly[1], badge_poly[2], badge_poly[3], badge_poly[0]]), INK, 2.0, true)
		var badge_text_col: Color = PAPER if _badge_fill == CRIMSON else INK
		draw_string(font, Vector2(bx + 6.0, by + bh - 4.0), _badge_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, badge_text_col)

	# Slanted HP bar with white damage-decay trail + bold `100 / 100 HP` readout
	var bar_x: float = right_x
	var bar_y: float = 38.0
	var bar_w: float = maxf(content_w, 60.0)
	var bar_h: float = 28.0
	var bar_slant: float = 5.0
	var track: PackedVector2Array = PackedVector2Array([
		Vector2(bar_x + bar_slant, bar_y),
		Vector2(bar_x + bar_w, bar_y),
		Vector2(bar_x + bar_w - bar_slant, bar_y + bar_h),
		Vector2(bar_x, bar_y + bar_h),
	])
	draw_colored_polygon(track, BAR_EMPTY)

	var max_f: float = maxf(float(_max_hp), 1.0)
	var live_share: float = clampf(float(_hp) / max_f, 0.0, 1.0)
	var trail_share: float = clampf(_trail_hp / max_f, live_share, 1.0)
	var inner_w: float = bar_w - bar_slant

	if trail_share > live_share + 0.004:
		var tw_px: float = inner_w * trail_share
		var trail_poly: PackedVector2Array = PackedVector2Array([
			Vector2(bar_x + bar_slant, bar_y + 2.0),
			Vector2(bar_x + bar_slant + tw_px, bar_y + 2.0),
			Vector2(bar_x + tw_px, bar_y + bar_h - 2.0),
			Vector2(bar_x, bar_y + bar_h - 2.0),
		])
		draw_colored_polygon(trail_poly, TRAIL_WHITE)

	if live_share > 0.002:
		var fw_px: float = inner_w * live_share
		var fill_col: Color = _hp_fill_color(live_share)
		var fill_poly: PackedVector2Array = PackedVector2Array([
			Vector2(bar_x + bar_slant, bar_y + 2.0),
			Vector2(bar_x + bar_slant + fw_px, bar_y + 2.0),
			Vector2(bar_x + fw_px, bar_y + bar_h - 2.0),
			Vector2(bar_x, bar_y + bar_h - 2.0),
		])
		draw_colored_polygon(fill_poly, fill_col)
		var gloss_h: float = 7.0
		var gloss_poly: PackedVector2Array = PackedVector2Array([
			Vector2(bar_x + bar_slant, bar_y + 2.0),
			Vector2(bar_x + bar_slant + fw_px, bar_y + 2.0),
			Vector2(bar_x + bar_slant + fw_px - 1.5, bar_y + 2.0 + gloss_h),
			Vector2(bar_x + bar_slant - 1.5, bar_y + 2.0 + gloss_h),
		])
		draw_colored_polygon(gloss_poly, Color(1.0, 1.0, 1.0, 0.28))

	for step: int in range(1, 4):
		var sx: float = bar_x + inner_w * (float(step) * 0.25)
		draw_line(Vector2(sx + bar_slant, bar_y + 2.0), Vector2(sx, bar_y + bar_h - 2.0), Color(INK, 0.55), 1.5)

	draw_polyline(PackedVector2Array([track[0], track[1], track[2], track[3], track[0]]), INK, 2.6, true)

	var hp_label: String = "%d / %d HP" % [_hp, _max_hp]
	var lw: float = font.get_string_size(hp_label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
	var lpos: Vector2 = Vector2(bar_x + (bar_w - lw) * 0.5, bar_y + bar_h - 6.0)
	draw_string_outline(font, lpos, hp_label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 5, INK)
	draw_string(font, lpos, hp_label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, TRAIL_WHITE)
