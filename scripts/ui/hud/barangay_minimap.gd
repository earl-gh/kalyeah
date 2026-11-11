class_name BarangayMinimap
extends Control
## 156x156 circular 2D 90s Filipino Komiks x Valorant tactical barangay radar (top-left beside Settings gear).
## Renders the crisp circular barangay perimeter ring + 360° perimeter roof collar, Estero creek clipped to the
## circular arena + 3 wooden/concrete Estero bridge plank crossings, tabi-tabing bahay blocks, Blue/Red bases &
## rescue zones, pulsing base-capture alert ring, and all 10 players with local FOV cone.

const SIZE: Vector2 = Vector2(156.0, 156.0)
const RADAR_R: float = 72.0
const PERIMETER_COLLAR: float = 9.0
const FOV_HALF_RAD: float = 0.48
const FOV_LEN: float = 27.0
const RING_STEPS: int = 56
const COLLAR_SEGMENTS: int = 20

const INK: Color = Color("141016")
const SHADOW: Color = Color(0.08, 0.06, 0.09, 0.84)
const PAPER: Color = Color("f5ebd6")
const GOLD: Color = Color("ffbe2e")
const COBALT: Color = Color("2b7bff")
const CRIMSON: Color = Color("f23838")
const SELF_GREEN: Color = Color("38e864")
const COLLAR_BG: Color = Color("3b2922")
const COLLAR_ROOF: Color = Color("5c3d30")
const GROUND: Color = Color("26222c")
const GROUND_GRID: Color = Color(0.96, 0.92, 0.84, 0.1)
const ARENA_RING: Color = Color("f5d796")
const ESTERO: Color = Color("22748c")
const ESTERO_EDGE: Color = Color("6bd3e8")
const BRIDGE_DECK: Color = Color("ecc686")
const BRIDGE_RAIL: Color = Color("fff3d6")
const HOUSE_FILL: Color = Color("875c47")
const HOUSE_HI: Color = Color("ad7b60")

var _cached_layout: MapLayout = null
var _cached_side: int = 0
var _cached_center: Vector2 = Vector2.ZERO
var _world_r: float = 84.0
var _is_circular: bool = true
var _lane_box: Rect2 = Rect2()
var _water_boxes: Array[Rect2] = []
var _bridge_boxes: Array[Rect2] = []
var _house_boxes: Array[Rect2] = []
var _own_rescue: Rect2 = Rect2()
var _enemy_rescue: Rect2 = Rect2()
var _own_base: Vector2 = Vector2.ZERO
var _enemy_base: Vector2 = Vector2.ZERO
var _base_r_px: float = 7.5

var _has_state: bool = false
var _self_pos: Vector2 = Vector2.ZERO
var _self_tagged: bool = false
var _fov_dir: Vector2 = Vector2.UP
var _dot_pos: Array[Vector2] = []
var _dot_ally: Array[bool] = []
var _dot_tagged: Array[bool] = []
var _dot_count: int = 0
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
	return RADAR_R - PERIMETER_COLLAR


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

	var cap_running: bool = sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING and sim.capture_kind == MatchSim.CaptureKind.BASE
	_capturing_own_target = cap_running and sim.capture_team == local_team
	_capturing_our_base = cap_running and sim.capture_team != local_team
	_pulse_time = sim.match_time
	queue_redraw()


static func _read_arena_radius(layout: MapLayout) -> float:
	var raw: Variant = layout.get(&"arena_radius")
	if raw is float or raw is int:
		return float(raw)
	return 0.0


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
	var chord_half_x: float = sqrt(maxf(max_r * max_r - mid_y * mid_y, 0.0)) - 1.2
	var x0: float = maxf(rect.position.x, center.x - chord_half_x)
	var x1: float = minf(rect.end.x, center.x + chord_half_x)
	if x1 <= x0 + 0.5:
		return Rect2()
	var y0: float = clampf(rect.position.y, center.y - max_r + 1.2, center.y + max_r - 1.2)
	var y1: float = clampf(rect.end.y, center.y - max_r + 1.2, center.y + max_r - 1.2)
	if y1 <= y0 + 0.5:
		return Rect2()
	return Rect2(Vector2(x0, y0), Vector2(x1 - x0, y1 - y0))


func _rebuild_map_cache(layout: MapLayout, own_side: int, center: Vector2) -> void:
	_cached_layout = layout
	_cached_side = own_side
	_cached_center = center
	var arena_r: float = _read_arena_radius(layout)
	_is_circular = arena_r > 0.0
	if _is_circular:
		_world_r = arena_r
	else:
		_world_r = Vector2(layout.lane_width, layout.lane_length).length() * 0.5
	var usable: float = _arena_px()
	var scale_px: float = usable / maxf(_world_r, 1.0)
	_lane_box = _rect_to_radar(layout.lane_rect(), own_side, center)

	var raw_water: Array[Rect2] = []
	for w: Rect2 in layout.water_rects():
		raw_water.append(_rect_to_radar(w, own_side, center))

	_water_boxes.clear()
	for rw: Rect2 in raw_water:
		var clipped_w: Rect2 = _clip_rect_to_circle(rw, center, usable - 1.0) if _is_circular else rw
		if clipped_w.size.x > 0.5:
			_water_boxes.append(clipped_w)
	_rebuild_bridges(raw_water, center, usable)

	_house_boxes.clear()
	for obs: Rect2 in layout.obstacle_rects():
		var r: Rect2 = _rect_to_radar(obs, own_side, center)
		if _is_circular:
			var clipped_h: Rect2 = _clip_rect_to_circle(r, center, usable - 1.8)
			if clipped_h.size.x >= 2.0 and clipped_h.size.y >= 2.0:
				_house_boxes.append(clipped_h)
		elif r.get_center().distance_to(center) <= usable - 2.0:
			_house_boxes.append(r)

	_own_rescue = _rect_to_radar(layout.rescue_rect(own_side), own_side, center)
	_enemy_rescue = _rect_to_radar(layout.rescue_rect(-own_side), own_side, center)
	_own_base = _to_radar(layout.base_center(own_side), own_side, center)
	_enemy_base = _to_radar(layout.base_center(-own_side), own_side, center)
	_base_r_px = clampf(layout.base_radius * scale_px, 6.5, 12.0)


func _rebuild_bridges(raw_water: Array[Rect2], center: Vector2, usable: float) -> void:
	_bridge_boxes.clear()
	if raw_water.is_empty():
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
			var deck_w: float = maxf(gap_x + 3.0, 8.2)
			var deck_h: float = maxf(maxf(left_w.size.y, right_w.size.y) + 4.4, 10.6)
			var bridge: Rect2 = Rect2(Vector2(mid_x - deck_w * 0.5, mid_y - deck_h * 0.5), Vector2(deck_w, deck_h))
			if bridge.get_center().distance_to(center) <= usable - 3.0:
				_bridge_boxes.append(bridge)
	if _bridge_boxes.is_empty():
		var band_y: float = sorted[0].get_center().y
		var bh: float = maxf(sorted[0].size.y + 4.4, 10.6)
		for frac: float in [-0.54, 0.0, 0.54]:
			var bx: float = center.x + frac * usable
			_bridge_boxes.append(Rect2(Vector2(bx - 4.2, band_y - bh * 0.5), Vector2(8.4, bh)))


func _ally_color() -> Color:
	return TeamColors.score_own() if Settings.colorblind else COBALT


func _enemy_color() -> Color:
	return TeamColors.score_enemy() if Settings.colorblind else CRIMSON


func _self_color() -> Color:
	return TeamColors.BLIND_SELF if Settings.colorblind else SELF_GREEN


func _draw() -> void:
	var center: Vector2 = size * 0.5
	var inner_r: float = _arena_px()

	# Outer komiks drop shadow + 360° tabi-tabing bahay perimeter collar + circular playable ground
	draw_circle(center + Vector2(3.0, 4.0), RADAR_R + 2.0, SHADOW)
	draw_circle(center, RADAR_R + 2.0, INK)
	draw_circle(center, RADAR_R - 1.5, COLLAR_BG)
	_draw_perimeter_collar(center, inner_r + 1.5, RADAR_R - 2.0)
	draw_circle(center, inner_r + 1.4, INK)
	draw_circle(center, inner_r, GROUND)

	# Valorant tactical quadrant sightlines
	draw_line(center + Vector2(-inner_r, 0.0), center + Vector2(inner_r, 0.0), GROUND_GRID, 1.0)
	draw_line(center + Vector2(0.0, -inner_r), center + Vector2(0.0, inner_r), GROUND_GRID, 1.0)
	draw_arc(center, inner_r * 0.52, 0.0, TAU, 36, GROUND_GRID, 1.0, true)

	# Estero creek + 3 plank-ticked bridges
	for w: Rect2 in _water_boxes:
		draw_rect(w.grow(0.9), INK, true)
		draw_rect(w, ESTERO, true)
		draw_line(Vector2(w.position.x, w.position.y + 0.8), Vector2(w.end.x, w.position.y + 0.8), ESTERO_EDGE, 1.1)
		draw_line(Vector2(w.position.x, w.end.y - 0.8), Vector2(w.end.x, w.end.y - 0.8), Color(ESTERO_EDGE, 0.65), 1.0)
	for br: Rect2 in _bridge_boxes:
		_draw_bridge_crossing(br)

	# Tabi-tabing bahay interior blocks
	for house: Rect2 in _house_boxes:
		draw_rect(house.grow(1.0), INK, true)
		draw_rect(house, HOUSE_FILL, true)
		if house.size.y >= 3.0:
			draw_rect(Rect2(house.position, Vector2(house.size.x, minf(2.0, house.size.y * 0.42))), HOUSE_HI, true)

	# Crisp Valorant circular barangay perimeter ring
	if _is_circular:
		draw_arc(center, inner_r + 0.8, 0.0, TAU, RING_STEPS, INK, 3.2, true)
		draw_arc(center, inner_r - 0.4, 0.0, TAU, RING_STEPS, ARENA_RING, 2.0, true)
	elif _lane_box.size.x > 0.0:
		draw_rect(_lane_box, ARENA_RING, false, 1.8)

	var ally_col: Color = _ally_color()
	var enemy_col: Color = _enemy_color()

	# Rescue zones
	if _own_rescue.size.x > 0.0:
		draw_rect(_own_rescue, Color(ally_col, 0.3), true)
		draw_rect(_own_rescue, ally_col.lightened(0.28), false, 1.5)
	if _enemy_rescue.size.x > 0.0:
		draw_rect(_enemy_rescue, Color(enemy_col, 0.3), true)
		draw_rect(_enemy_rescue, enemy_col.lightened(0.28), false, 1.5)

	# Blue & Red bases + pulsing ring during active base capture
	if _has_state:
		_draw_base_zone(_own_base, ally_col, _capturing_our_base)
		_draw_base_zone(_enemy_base, enemy_col, _capturing_own_target)

		# Allies & enemies (tagged X or live circle)
		for i: int in _dot_count:
			var col: Color = ally_col if _dot_ally[i] else enemy_col
			if _dot_tagged[i]:
				_draw_x_stamp(_dot_pos[i], 3.8, col)
			else:
				draw_circle(_dot_pos[i], 4.8, INK)
				draw_circle(_dot_pos[i], 3.4, col)
				if _dot_ally[i]:
					draw_circle(_dot_pos[i] + Vector2(-0.8, -0.8), 1.1, PAPER)

		# Local player: camera FOV cone + bright green komiks directional arrow
		if _self_tagged:
			_draw_x_stamp(_self_pos, 4.8, _self_color())
		else:
			_draw_self_fov_and_arrow(_self_pos, _fov_dir, _self_color(), inner_r)

	# Hand-inked komiks outer frame + cardinal N tick + bottom barrio badge
	draw_arc(center, RADAR_R - 1.2, 0.0, TAU, RING_STEPS, PAPER, 1.8, true)
	draw_arc(center, RADAR_R, 0.0, TAU, RING_STEPS, INK, 3.4, true)
	_draw_rim_badge(center)


func _draw_perimeter_collar(center: Vector2, r_in: float, r_out: float) -> void:
	var mid_r: float = (r_in + r_out) * 0.5
	var band_w: float = maxf(r_out - r_in, 2.0)
	for i: int in COLLAR_SEGMENTS:
		var a0: float = TAU * float(i) / float(COLLAR_SEGMENTS)
		var a1: float = TAU * float(i + 1) / float(COLLAR_SEGMENTS) - 0.04
		var roof_col: Color = COLLAR_ROOF if i % 2 == 0 else COLLAR_BG.lightened(0.08)
		draw_arc(center, mid_r, a0, a1, 5, roof_col, band_w, false)
		var spoke_dir: Vector2 = Vector2.from_angle(a0)
		draw_line(center + spoke_dir * r_in, center + spoke_dir * r_out, INK, 1.4)


func _draw_bridge_crossing(br: Rect2) -> void:
	draw_rect(br.grow(1.4), INK, true)
	draw_rect(br, BRIDGE_DECK, true)
	# Side parapet rails along left & right bridge edges
	draw_line(Vector2(br.position.x + 0.8, br.position.y), Vector2(br.position.x + 0.8, br.end.y), BRIDGE_RAIL, 1.5)
	draw_line(Vector2(br.end.x - 0.8, br.position.y), Vector2(br.end.x - 0.8, br.end.y), BRIDGE_RAIL, 1.5)
	# 3 crisp transverse plank ticks across each Estero bridge
	for p_idx: int in 3:
		var py: float = br.position.y + br.size.y * (0.24 + 0.26 * float(p_idx))
		draw_line(Vector2(br.position.x + 1.2, py), Vector2(br.end.x - 1.2, py), INK, 1.4)


func _draw_base_zone(at: Vector2, team_col: Color, capturing: bool) -> void:
	draw_circle(at, _base_r_px + 1.5, INK)
	draw_circle(at, _base_r_px, Color(team_col, 0.38))
	draw_arc(at, _base_r_px, 0.0, TAU, 24, team_col, 2.2, true)
	var d: float = 3.6
	var diamond: PackedVector2Array = PackedVector2Array([
		at + Vector2(0.0, -d),
		at + Vector2(d, 0.0),
		at + Vector2(0.0, d),
		at + Vector2(-d, 0.0),
	])
	draw_colored_polygon(diamond, GOLD if capturing else PAPER)
	if capturing:
		var wave: float = 0.5 + 0.5 * (0.0 if Settings.reduced_motion else sin(_pulse_time * 8.5))
		var pulse_r: float = _base_r_px + 3.0 + wave * 6.5
		draw_arc(at, pulse_r, 0.0, TAU, 28, Color(GOLD, 0.92 - wave * 0.45), 2.6, true)


func _draw_x_stamp(at: Vector2, arm: float, col: Color) -> void:
	var a: Vector2 = Vector2(arm, arm)
	var b: Vector2 = Vector2(arm, -arm)
	draw_line(at - a, at + a, INK, 3.6, true)
	draw_line(at - b, at + b, INK, 3.6, true)
	draw_line(at - a, at + a, col.lightened(0.2), 1.8, true)
	draw_line(at - b, at + b, col.lightened(0.2), 1.8, true)


func _draw_self_fov_and_arrow(at: Vector2, dir: Vector2, self_col: Color, inner_r: float) -> void:
	var base_angle: float = dir.angle()
	var cone: PackedVector2Array = PackedVector2Array([at])
	var steps: int = 8
	var max_cone_r: float = inner_r - 1.5
	for i: int in range(steps + 1):
		var ang: float = base_angle - FOV_HALF_RAD + (FOV_HALF_RAD * 2.0) * float(i) / float(steps)
		var pt: Vector2 = at + Vector2.from_angle(ang) * FOV_LEN
		var from_center: Vector2 = pt - size * 0.5
		if from_center.length() > max_cone_r:
			pt = size * 0.5 + from_center.normalized() * max_cone_r
		cone.append(pt)
	draw_colored_polygon(cone, Color(self_col, 0.22))
	if cone.size() >= 3:
		draw_line(at, cone[1], Color(self_col, 0.54), 1.3, true)
		draw_line(at, cone[cone.size() - 1], Color(self_col, 0.54), 1.3, true)

	var side: Vector2 = Vector2(-dir.y, dir.x)
	var arrow_out: PackedVector2Array = PackedVector2Array([
		at + dir * 8.2,
		at - dir * 5.2 + side * 5.8,
		at - dir * 2.4,
		at - dir * 5.2 - side * 5.8,
	])
	draw_colored_polygon(arrow_out, INK)
	var arrow_in: PackedVector2Array = PackedVector2Array([
		at + dir * 6.2,
		at - dir * 3.8 + side * 4.0,
		at - dir * 1.6,
		at - dir * 3.8 - side * 4.0,
	])
	draw_colored_polygon(arrow_in, self_col)


func _draw_rim_badge(center: Vector2) -> void:
	var plate_w: float = 54.0
	var plate_h: float = 17.0
	var slant: float = 4.0
	var y: float = center.y + RADAR_R - plate_h * 0.58
	var x: float = center.x - plate_w * 0.5
	var poly: PackedVector2Array = PackedVector2Array([
		Vector2(x + slant, y),
		Vector2(x + plate_w, y),
		Vector2(x + plate_w - slant, y + plate_h),
		Vector2(x, y + plate_h),
	])
	draw_colored_polygon(poly, GOLD)
	draw_polyline(PackedVector2Array([poly[0], poly[1], poly[2], poly[3], poly[0]]), INK, 2.2, true)
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = "BRGY"
	var tw: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
	var baseline: Vector2 = Vector2(center.x - tw * 0.5, y + plate_h - 3.0)
	draw_string(font, baseline, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, INK)
