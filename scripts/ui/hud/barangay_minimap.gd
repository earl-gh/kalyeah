class_name BarangayMinimap
extends Control
## Top-left 152x152 minimalist circular barangay tactical radar (`image_1` / `HudStyle` white-rim aesthetic):
## Renders the circular 84m barangay arena, 3 N–S pathways + 2 E–W alleyways, walkable shallow mid canal
## (never synthesizing a fake mid bridge), interior building blocks, small Power-Up objective dots,
## Blue/Red bases & rescue zones, and all 10 players with local camera FOV cone.

const SIZE: Vector2 = Vector2(152.0, 152.0)
const RADAR_R: float = 70.0
const INNER_MARGIN: float = 5.0
const FOV_HALF_RAD: float = 0.46
const FOV_LEN: float = 25.0
const RING_STEPS: int = 52

const BACK_GLASS: Color = Color(0.06, 0.08, 0.10, 0.56)
const PATH_GUIDE: Color = Color(1.0, 1.0, 1.0, 0.07)
const ARENA_RING: Color = Color(1.0, 1.0, 1.0, 0.34)
const CANAL_WATER: Color = Color(0.20, 0.74, 0.78, 0.34)
const CANAL_BANK: Color = Color(0.72, 0.94, 0.96, 0.28)
const BRIDGE_DECK: Color = Color(0.92, 0.86, 0.74, 0.52)
const HOUSE_FILL: Color = Color(0.84, 0.78, 0.70, 0.30)
const HOUSE_EDGE: Color = Color(1.0, 1.0, 1.0, 0.20)
const SELF_MINT: Color = Color(0.38, 0.96, 0.62, 0.96)
const ALLY_SKY: Color = Color(0.32, 0.64, 1.0, 0.95)
const ENEMY_CORAL: Color = Color(0.95, 0.34, 0.28, 0.95)
const CAPTURE_GOLD: Color = Color(1.0, 0.82, 0.32, 0.92)
const PUP_PALAMIG: Color = Color(0.28, 0.88, 0.98, 0.95)
const PUP_TEKS: Color = Color(1.0, 0.76, 0.24, 0.95)
const PUP_TURON: Color = Color(0.34, 0.92, 0.58, 0.95)

var _cached_layout: MapLayout = null
var _cached_side: int = 0
var _cached_center: Vector2 = Vector2.ZERO
var _world_r: float = 84.0
var _is_circular: bool = true
var _lane_box: Rect2 = Rect2()
var _water_boxes: Array[Rect2] = []
var _walkable_canal_box: Rect2 = Rect2()
var _bridge_boxes: Array[Rect2] = []
var _pathway_strips: Array[Rect2] = []
var _house_boxes: Array[Rect2] = []
var _fallback_pup_pos: Array[Vector2] = []
var _own_rescue: Rect2 = Rect2()
var _enemy_rescue: Rect2 = Rect2()
var _own_base: Vector2 = Vector2.ZERO
var _enemy_base: Vector2 = Vector2.ZERO
var _base_r_px: float = 7.0

var _has_state: bool = false
var _self_pos: Vector2 = Vector2.ZERO
var _self_tagged: bool = false
var _fov_dir: Vector2 = Vector2.UP
var _dot_pos: Array[Vector2] = []
var _dot_ally: Array[bool] = []
var _dot_tagged: Array[bool] = []
var _dot_count: int = 0
var _pup_pos: Array[Vector2] = []
var _pup_kind: Array[int] = []
var _pup_active: Array[bool] = []
var _pup_count: int = 0
var _capturing_own_target: bool = false
var _capturing_our_base: bool = false
var _pulse_time: float = 0.0


func _init() -> void:
	name = "BarangayMinimap"
	custom_minimum_size = SIZE
	size = SIZE
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(_invalidate_map_cache)


func _invalidate_map_cache() -> void:
	_cached_layout = null
	queue_redraw()


func _arena_px() -> float:
	return RADAR_R - INNER_MARGIN


func sync_radar(sim: MatchSim, player: PlayerState, local_team: int, cam_yaw_deg: float) -> void:
	if sim == null or sim.layout == null or player == null:
		return
	var own_side: int = sim.side_for_team(local_team)
	var center: Vector2 = size * 0.5
	if _cached_layout != sim.layout or _cached_side != own_side or _cached_center != center:
		_rebuild_map_cache(sim.layout, own_side, center)
	_has_state = true
	_self_pos = _to_radar(player.position, own_side, center)
	_self_tagged = player.tagged
	var yaw_rad: float = deg_to_rad(cam_yaw_deg)
	var world_fwd: Vector2 = Vector2(-sin(yaw_rad), -cos(yaw_rad))
	var radar_fwd: Vector2 = world_fwd * float(own_side)
	_fov_dir = radar_fwd.normalized() if radar_fwd.length_squared() > 0.0001 else Vector2.UP

	var needed: int = maxi(sim.players.size(), 0)
	if _dot_pos.size() < needed:
		_dot_pos.resize(needed)
		_dot_ally.resize(needed)
		_dot_tagged.resize(needed)
	var idx: int = 0
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other == null or other.id == player.id:
			continue
		_dot_pos[idx] = _to_radar(other.position, own_side, center)
		_dot_ally[idx] = other.team == local_team
		_dot_tagged[idx] = other.tagged
		idx += 1
	_dot_count = idx

	_sync_powerups(sim, own_side, center)

	var cap_running: bool = sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING and sim.capture_kind == MatchSim.CaptureKind.BASE
	_capturing_own_target = cap_running and sim.capture_team == local_team
	_capturing_our_base = cap_running and sim.capture_team != local_team
	_pulse_time = sim.match_time
	queue_redraw()


func _sync_powerups(sim: MatchSim, own_side: int, center: Vector2) -> void:
	var arr: Array[Dictionary] = sim.powerups
	if not arr.is_empty():
		var count: int = arr.size()
		if _pup_pos.size() < count:
			_pup_pos.resize(count)
			_pup_kind.resize(count)
			_pup_active.resize(count)
		var out_i: int = 0
		for d: Dictionary in arr:
			var pos_v: Variant = d.get("pos", Vector2.ZERO)
			if pos_v is Vector2:
				var pt: Vector2 = pos_v
				_pup_pos[out_i] = _to_radar(pt, own_side, center)
				var kind_val: int = 1
				var kind_v: Variant = d.get("kind", 1)
				if kind_v is int:
					kind_val = kind_v
				elif kind_v is float:
					var kf: float = kind_v
					kind_val = int(kf)
				_pup_kind[out_i] = kind_val
				var act_val: bool = true
				var act_v: Variant = d.get("active", true)
				if act_v is bool:
					act_val = act_v
				_pup_active[out_i] = act_val
				out_i += 1
		_pup_count = out_i
		return
	# Fallback to layout spots before MatchSim populates `powerups`
	var fb_n: int = _fallback_pup_pos.size()
	if _pup_pos.size() < fb_n:
		_pup_pos.resize(fb_n)
		_pup_kind.resize(fb_n)
		_pup_active.resize(fb_n)
	for k: int in fb_n:
		_pup_pos[k] = _fallback_pup_pos[k]
		_pup_kind[k] = (k % 3) + 1
		_pup_active[k] = true
	_pup_count = fb_n


static func _read_arena_radius(layout: MapLayout) -> float:
	return layout.arena_radius if layout != null else 0.0


func _to_radar(world_xz: Vector2, own_side: int, center: Vector2) -> Vector2:
	var usable: float = _arena_px() - 2.0
	var scale_px: float = _arena_px() / maxf(_world_r, 1.0)
	var offset: Vector2 = world_xz * float(own_side) * scale_px
	if offset.length() > usable:
		offset = offset.normalized() * usable
	return center + offset


func _rect_to_radar(world_rect: Rect2, own_side: int, center: Vector2) -> Rect2:
	var scale_px: float = _arena_px() / maxf(_world_r, 1.0)
	var a: Vector2 = center + world_rect.position * float(own_side) * scale_px
	var b: Vector2 = center + world_rect.end * float(own_side) * scale_px
	var top_left: Vector2 = Vector2(minf(a.x, b.x), minf(a.y, b.y))
	var bottom_right: Vector2 = Vector2(maxf(a.x, b.x), maxf(a.y, b.y))
	return Rect2(top_left, bottom_right - top_left)


func _clip_rect_to_circle(rect: Rect2, center: Vector2, max_r: float) -> Rect2:
	var mid_y: float = rect.get_center().y - center.y
	if absf(mid_y) >= max_r - 1.0:
		return Rect2()
	var chord_half_x: float = sqrt(maxf(max_r * max_r - mid_y * mid_y, 0.0)) - 1.0
	var x0: float = maxf(rect.position.x, center.x - chord_half_x)
	var x1: float = minf(rect.end.x, center.x + chord_half_x)
	if x1 <= x0 + 0.5:
		return Rect2()
	var y0: float = clampf(rect.position.y, center.y - max_r + 1.0, center.y + max_r - 1.0)
	var y1: float = clampf(rect.end.y, center.y - max_r + 1.0, center.y + max_r - 1.0)
	if y1 <= y0 + 0.5:
		return Rect2()
	return Rect2(Vector2(x0, y0), Vector2(x1 - x0, y1 - y0))


func _rebuild_map_cache(layout: MapLayout, own_side: int, center: Vector2) -> void:
	_cached_layout = layout
	_cached_side = own_side
	_cached_center = center
	var arena_r: float = _read_arena_radius(layout)
	_is_circular = arena_r > 0.0
	_world_r = arena_r if _is_circular else (Vector2(layout.lane_width, layout.lane_length).length() * 0.5)
	var usable: float = _arena_px()
	var scale_px: float = usable / maxf(_world_r, 1.0)
	_lane_box = _rect_to_radar(layout.lane_rect(), own_side, center)

	# 3 wider N–S pathways (`X = -34, 0, +34`) + 2 E–W alleyways (`Z = -26, +26`)
	_pathway_strips.clear()
	if _is_circular:
		for lane_x: float in [-34.0, 0.0, 34.0]:
			var ns_rect: Rect2 = _rect_to_radar(Rect2(Vector2(lane_x - 5.5, -72.0), Vector2(11.0, 144.0)), own_side, center)
			var c_ns: Rect2 = _clip_rect_to_circle(ns_rect, center, usable - 2.0)
			if c_ns.size.x > 0.5 and c_ns.size.y > 0.5:
				_pathway_strips.append(c_ns)
		for alley_z: float in [-26.0, 26.0]:
			var ew_rect: Rect2 = _rect_to_radar(Rect2(Vector2(-68.0, alley_z - 4.5), Vector2(136.0, 9.0)), own_side, center)
			var c_ew: Rect2 = _clip_rect_to_circle(ew_rect, center, usable - 2.0)
			if c_ew.size.x > 0.5 and c_ew.size.y > 0.5:
				_pathway_strips.append(c_ew)

	var raw_water: Array[Rect2] = []
	for w: Rect2 in layout.water_rects():
		raw_water.append(_rect_to_radar(w, own_side, center))

	_water_boxes.clear()
	for rw: Rect2 in raw_water:
		var clipped_w: Rect2 = _clip_rect_to_circle(rw, center, usable - 1.0) if _is_circular else rw
		if clipped_w.size.x > 0.5:
			_water_boxes.append(clipped_w)
	_rebuild_bridges(raw_water, center, usable)

	# When `water_rects()` is empty on the circular barangay, render the walkable shallow mid canal (NO mid bridge!)
	if _is_circular and _water_boxes.is_empty():
		var shallow_world: Rect2 = Rect2(Vector2(-82.0, -3.8), Vector2(164.0, 7.6))
		_walkable_canal_box = _clip_rect_to_circle(_rect_to_radar(shallow_world, own_side, center), center, usable - 1.2)
	else:
		_walkable_canal_box = Rect2()

	_house_boxes.clear()
	for obs: Rect2 in layout.obstacle_rects():
		var r: Rect2 = _rect_to_radar(obs, own_side, center)
		if _is_circular:
			var clipped_h: Rect2 = _clip_rect_to_circle(r, center, usable - 1.6)
			if clipped_h.size.x >= 1.8 and clipped_h.size.y >= 1.8:
				_house_boxes.append(clipped_h)
		elif r.get_center().distance_to(center) <= usable - 2.0:
			_house_boxes.append(r)

	_fallback_pup_pos.clear()
	if _is_circular:
		for sp_pt: Vector2 in layout.powerup_spots:
			_fallback_pup_pos.append(_to_radar(sp_pt, own_side, center))

	_own_rescue = _rect_to_radar(layout.rescue_rect(own_side), own_side, center)
	_enemy_rescue = _rect_to_radar(layout.rescue_rect(-own_side), own_side, center)
	_own_base = _to_radar(layout.base_center(own_side), own_side, center)
	_enemy_base = _to_radar(layout.base_center(-own_side), own_side, center)
	_base_r_px = clampf(layout.base_radius * scale_px, 6.0, 11.0)


func _rebuild_bridges(raw_water: Array[Rect2], center: Vector2, usable: float) -> void:
	_bridge_boxes.clear()
	if raw_water.size() < 2:
		return
	var sorted: Array[Rect2] = raw_water.duplicate()
	sorted.sort_custom(func(a: Rect2, b: Rect2) -> bool: return a.position.x < b.position.x)
	for i: int in range(sorted.size() - 1):
		var left_w: Rect2 = sorted[i]
		var right_w: Rect2 = sorted[i + 1]
		var gap_x: float = right_w.position.x - left_w.end.x
		var y_diff: float = absf(left_w.get_center().y - right_w.get_center().y)
		if gap_x >= 1.2 and gap_x <= 32.0 and y_diff <= 18.0:
			var mid_x: float = (left_w.end.x + right_w.position.x) * 0.5
			var mid_y: float = (left_w.get_center().y + right_w.get_center().y) * 0.5
			var deck_w: float = maxf(gap_x + 2.4, 7.2)
			var deck_h: float = maxf(maxf(left_w.size.y, right_w.size.y) + 3.2, 8.8)
			var bridge: Rect2 = Rect2(Vector2(mid_x - deck_w * 0.5, mid_y - deck_h * 0.5), Vector2(deck_w, deck_h))
			if bridge.get_center().distance_to(center) <= usable - 3.0:
				_bridge_boxes.append(bridge)


func _ally_color() -> Color:
	return TeamColors.score_own() if Settings.colorblind else ALLY_SKY


func _enemy_color() -> Color:
	return TeamColors.score_enemy() if Settings.colorblind else ENEMY_CORAL


func _self_color() -> Color:
	return TeamColors.BLIND_SELF if Settings.colorblind else SELF_MINT


func _pup_color(kind: int) -> Color:
	if kind == 1:
		return PUP_PALAMIG
	if kind == 2:
		return PUP_TEKS
	return PUP_TURON


func _draw() -> void:
	var center: Vector2 = size * 0.5
	var inner_r: float = _arena_px()

	# Clean translucent glass disc + white outer rim
	draw_circle(center, RADAR_R, BACK_GLASS)

	# 3 N–S pathways + 2 E–W cross-alleys faint tactical guides
	for strip: Rect2 in _pathway_strips:
		draw_rect(strip, PATH_GUIDE, true)

	# Walkable shallow mid canal (no mid bridge) or custom water_rects + actual gap bridges
	if _walkable_canal_box.size.x > 0.5:
		draw_rect(_walkable_canal_box, CANAL_WATER, true)
		draw_line(Vector2(_walkable_canal_box.position.x, _walkable_canal_box.position.y), Vector2(_walkable_canal_box.end.x, _walkable_canal_box.position.y), CANAL_BANK, 1.0)
		draw_line(Vector2(_walkable_canal_box.position.x, _walkable_canal_box.end.y), Vector2(_walkable_canal_box.end.x, _walkable_canal_box.end.y), CANAL_BANK, 1.0)
	for w: Rect2 in _water_boxes:
		draw_rect(w, CANAL_WATER, true)
		draw_line(Vector2(w.position.x, w.position.y), Vector2(w.end.x, w.position.y), CANAL_BANK, 1.0)
		draw_line(Vector2(w.position.x, w.end.y), Vector2(w.end.x, w.end.y), CANAL_BANK, 1.0)
	for br: Rect2 in _bridge_boxes:
		draw_rect(br, BRIDGE_DECK, true)

	# Interior building cover blocks
	for house: Rect2 in _house_boxes:
		draw_rect(house, HOUSE_FILL, true)
		draw_rect(house, HOUSE_EDGE, false, 1.0)

	# Subtle circular barangay arena ring
	if _is_circular:
		draw_arc(center, inner_r, 0.0, TAU, RING_STEPS, ARENA_RING, 1.4, true)
	elif _lane_box.size.x > 0.0:
		draw_rect(_lane_box, ARENA_RING, false, 1.2)

	var ally_col: Color = _ally_color()
	var enemy_col: Color = _enemy_color()

	# Rescue zones
	if _own_rescue.size.x > 0.0:
		draw_rect(_own_rescue, Color(ally_col.r, ally_col.g, ally_col.b, 0.20), true)
		draw_rect(_own_rescue, Color(ally_col.r, ally_col.g, ally_col.b, 0.55), false, 1.2)
	if _enemy_rescue.size.x > 0.0:
		draw_rect(_enemy_rescue, Color(enemy_col.r, enemy_col.g, enemy_col.b, 0.20), true)
		draw_rect(_enemy_rescue, Color(enemy_col.r, enemy_col.g, enemy_col.b, 0.55), false, 1.2)

	# Small map Power-Up objective dots
	for p_i: int in _pup_count:
		var pt: Vector2 = _pup_pos[p_i]
		var pcol: Color = _pup_color(_pup_kind[p_i])
		if _pup_active[p_i]:
			var d: float = 3.2
			draw_colored_polygon(PackedVector2Array([
				pt + Vector2(0.0, -d),
				pt + Vector2(d, 0.0),
				pt + Vector2(0.0, d),
				pt + Vector2(-d, 0.0),
			]), pcol)
		else:
			draw_arc(pt, 2.8, 0.0, TAU, 14, Color(pcol.r, pcol.g, pcol.b, 0.36), 1.2, true)

	if _has_state:
		_draw_base_zone(_own_base, ally_col, _capturing_our_base)
		_draw_base_zone(_enemy_base, enemy_col, _capturing_own_target)

		for i: int in _dot_count:
			var col: Color = ally_col if _dot_ally[i] else enemy_col
			if _dot_tagged[i]:
				_draw_x_stamp(_dot_pos[i], 3.2, col)
			else:
				draw_circle(_dot_pos[i], 3.8, Color(0.0, 0.0, 0.0, 0.55))
				draw_circle(_dot_pos[i], 2.9, col)

		if _self_tagged:
			_draw_x_stamp(_self_pos, 4.0, _self_color())
		else:
			_draw_self_fov_and_arrow(_self_pos, _fov_dir, _self_color(), inner_r)

	# Crisp thin white outer rim matching `MoveStick` and `AbilityArc`
	draw_arc(center, RADAR_R, 0.0, TAU, RING_STEPS, HudStyle.WHITE_RIM, 2.2, true)


func _draw_base_zone(at: Vector2, team_col: Color, capturing: bool) -> void:
	draw_circle(at, _base_r_px, Color(team_col.r, team_col.g, team_col.b, 0.26))
	draw_arc(at, _base_r_px, 0.0, TAU, 24, team_col, 1.8, true)
	draw_circle(at, 2.2, CAPTURE_GOLD if capturing else Color(1.0, 1.0, 1.0, 0.85))
	if capturing:
		var wave: float = 0.5 + 0.5 * (0.0 if Settings.reduced_motion else sin(_pulse_time * 7.5))
		var pulse_r: float = _base_r_px + 2.5 + wave * 5.5
		draw_arc(at, pulse_r, 0.0, TAU, 28, Color(CAPTURE_GOLD.r, CAPTURE_GOLD.g, CAPTURE_GOLD.b, 0.85 - wave * 0.45), 2.0, true)


func _draw_x_stamp(at: Vector2, arm: float, col: Color) -> void:
	var a: Vector2 = Vector2(arm, arm)
	var b: Vector2 = Vector2(arm, -arm)
	draw_line(at - a, at + a, Color(0.0, 0.0, 0.0, 0.55), 2.8, true)
	draw_line(at - b, at + b, Color(0.0, 0.0, 0.0, 0.55), 2.8, true)
	draw_line(at - a, at + a, col, 1.6, true)
	draw_line(at - b, at + b, col, 1.6, true)


func _draw_self_fov_and_arrow(at: Vector2, dir: Vector2, self_col: Color, inner_r: float) -> void:
	var base_angle: float = dir.angle()
	var cone: PackedVector2Array = PackedVector2Array([at])
	var steps: int = 8
	var max_cone_r: float = inner_r - 1.0
	for i: int in range(steps + 1):
		var ang: float = base_angle - FOV_HALF_RAD + (FOV_HALF_RAD * 2.0) * float(i) / float(steps)
		var pt: Vector2 = at + Vector2.from_angle(ang) * FOV_LEN
		var from_center: Vector2 = pt - size * 0.5
		if from_center.length() > max_cone_r:
			pt = size * 0.5 + from_center.normalized() * max_cone_r
		cone.append(pt)
	draw_colored_polygon(cone, Color(1.0, 1.0, 1.0, 0.14))
	if cone.size() >= 3:
		draw_line(at, cone[1], Color(1.0, 1.0, 1.0, 0.34), 1.1, true)
		draw_line(at, cone[cone.size() - 1], Color(1.0, 1.0, 1.0, 0.34), 1.1, true)

	var side: Vector2 = Vector2(-dir.y, dir.x)
	var arrow_out: PackedVector2Array = PackedVector2Array([
		at + dir * 7.2,
		at - dir * 4.4 + side * 4.8,
		at - dir * 2.0,
		at - dir * 4.4 - side * 4.8,
	])
	draw_colored_polygon(arrow_out, Color(0.0, 0.0, 0.0, 0.55))
	var arrow_in: PackedVector2Array = PackedVector2Array([
		at + dir * 5.6,
		at - dir * 3.4 + side * 3.5,
		at - dir * 1.4,
		at - dir * 3.4 - side * 3.5,
	])
	draw_colored_polygon(arrow_in, self_col)
