class_name WorldWaypoints
extends Control
## Projects 3D tactical objectives (`CAPTURE BASE`, `DEFEND BASE!`, `RESCUE ALLY!`) through Camera3D
## onto screen/edge 90s Filipino Komiks chevrons with live distance in meters.
## Keeps objective callouts elevated above overhead combat health bars and softens alpha to 0.65 near screen center.

const EDGE_MARGIN_X: float = 84.0
const EDGE_MARGIN_TOP: float = 96.0
const EDGE_MARGIN_BOTTOM: float = 108.0
const CENTER_ALPHA: float = 0.65
const EDGE_ALPHA: float = 0.95
const WORLD_LIFT_Y: float = 5.4

const INK: Color = Color("141016")
const SHADOW: Color = Color(0.08, 0.06, 0.09, 0.78)
const PAPER: Color = Color("f5ebd6")
const GOLD: Color = Color("ffbe2e")
const COBALT: Color = Color("2b7bff")
const CRIMSON: Color = Color("f23838")
const RESCUE_GREEN: Color = Color("38e864")

enum Kind { CAPTURE, DEFEND, RESCUE }

var _count: int = 0
var _pos: Array[Vector2] = [Vector2.ZERO, Vector2.ZERO, Vector2.ZERO]
var _clamped: Array[bool] = [false, false, false]
var _dir: Array[Vector2] = [Vector2.DOWN, Vector2.DOWN, Vector2.DOWN]
var _alpha: Array[float] = [EDGE_ALPHA, EDGE_ALPHA, EDGE_ALPHA]
var _meters: Array[int] = [0, 0, 0]
var _kind: Array[int] = [Kind.CAPTURE, Kind.DEFEND, Kind.RESCUE]
var _urgent: Array[bool] = [false, false, false]
var _pulse: float = 0.0


func _init() -> void:
	name = "WorldWaypoints"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func sync_waypoints(sim: MatchSim, camera: Camera3D, player: PlayerState, local_team: int) -> void:
	if sim == null or sim.layout == null or camera == null or player == null or size.x <= 4.0 or size.y <= 4.0:
		if _count > 0:
			_count = 0
			queue_redraw()
		return
	if sim.phase != MatchSim.Phase.PLAYING or player.tagged:
		if _count > 0:
			_count = 0
			queue_redraw()
		return

	_pulse = sim.match_time
	var own_side: int = sim.side_for_team(local_team)
	var idx: int = 0

	# 1. CAPTURE BASE (enemy base, anchored high above street health bars)
	var enemy_base: Vector2 = sim.layout.base_center(-own_side)
	var taking_enemy: bool = sim.capture_team == local_team and sim.capture_kind == MatchSim.CaptureKind.BASE
	_project_slot(idx, camera, Vector3(enemy_base.x, WORLD_LIFT_Y, enemy_base.y), player.position.distance_to(enemy_base), Kind.CAPTURE, taking_enemy)
	idx += 1

	# 2. DEFEND BASE! (own base under threat)
	var own_base: Vector2 = sim.layout.base_center(own_side)
	var enemy_in_base: bool = sim.capture_team >= 0 and sim.capture_team != local_team and sim.capture_kind == MatchSim.CaptureKind.BASE
	if not enemy_in_base:
		for id: int in sim.players:
			var other: PlayerState = sim.players[id]
			if other != null and other.team != local_team and not other.tagged and sim.layout.in_base(own_side, other.position):
				enemy_in_base = true
				break
	if enemy_in_base:
		_project_slot(idx, camera, Vector3(own_base.x, WORLD_LIFT_Y, own_base.y), player.position.distance_to(own_base), Kind.DEFEND, true)
		idx += 1

	# 3. RESCUE ALLY! (any teammate tagged on sidewalk)
	var ally_tagged: bool = false
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other != null and other.team == local_team and other.id != player.id and other.tagged:
			ally_tagged = true
			break
	if ally_tagged:
		var rescue_pt: Vector2 = sim.layout.rescue_rect(own_side).get_center()
		_project_slot(idx, camera, Vector3(rescue_pt.x, WORLD_LIFT_Y - 0.6, rescue_pt.y), player.position.distance_to(rescue_pt), Kind.RESCUE, true)
		idx += 1

	_count = idx
	queue_redraw()


func _project_slot(slot: int, camera: Camera3D, world_pos: Vector3, dist_m: float, kind: int, urgent: bool) -> void:
	_kind[slot] = kind
	_urgent[slot] = urgent
	_meters[slot] = maxi(1, roundi(dist_m))
	var center: Vector2 = size * 0.5
	var cam_local: Vector3 = camera.global_transform.affine_inverse() * world_pos
	var safe: Rect2 = Rect2(
		Vector2(EDGE_MARGIN_X, EDGE_MARGIN_TOP),
		Vector2(maxf(size.x - EDGE_MARGIN_X * 2.0, 20.0), maxf(size.y - EDGE_MARGIN_TOP - EDGE_MARGIN_BOTTOM, 20.0))
	)
	if cam_local.z < -0.15:
		var raw_screen_pt: Vector2 = camera.unproject_position(world_pos)
		if safe.has_point(raw_screen_pt):
			var screen_pt: Vector2 = raw_screen_pt
			# Keep centered and off-center waypoints elevated above the street combat / overhead health-bar band
			var combat_band_top: float = maxf(EDGE_MARGIN_TOP + 24.0, size.y * 0.20)
			var combat_band_bot: float = size.y * 0.66
			if absf(screen_pt.x - center.x) < size.x * 0.44 and screen_pt.y > combat_band_top and screen_pt.y < combat_band_bot:
				screen_pt.y = combat_band_top
			if screen_pt.x < 332.0 and screen_pt.y < 256.0:
				screen_pt.y = 256.0
			_pos[slot] = screen_pt
			_clamped[slot] = false
			_dir[slot] = Vector2.DOWN
			var horiz_off: float = absf(raw_screen_pt.x - center.x) / maxf(size.x * 0.5, 1.0)
			var center_t: float = clampf((horiz_off - 0.24) / 0.52, 0.0, 1.0)
			_alpha[slot] = lerpf(CENTER_ALPHA, EDGE_ALPHA, center_t)
			return
		var way_front: Vector2 = raw_screen_pt - center
		_clamp_to_safe(slot, center, safe, way_front)
		return
	var way_behind: Vector2 = Vector2(cam_local.x, -cam_local.y)
	if way_behind.length_squared() < 0.0001:
		way_behind = Vector2.DOWN
	_clamp_to_safe(slot, center, safe, way_behind)


func _clamp_to_safe(slot: int, center: Vector2, safe: Rect2, raw_way: Vector2) -> void:
	var dir: Vector2 = raw_way.normalized() if raw_way.length_squared() > 0.0001 else Vector2.DOWN
	var half_x: float = maxf((center.x - safe.position.x) if dir.x < 0.0 else (safe.end.x - center.x), 10.0)
	var half_y: float = maxf((center.y - safe.position.y) if dir.y < 0.0 else (safe.end.y - center.y), 10.0)
	var tx: float = half_x / maxf(absf(dir.x), 0.0001)
	var ty: float = half_y / maxf(absf(dir.y), 0.0001)
	var t: float = minf(tx, ty)
	var clamped_pt: Vector2 = (center + dir * t).clamp(safe.position, safe.end)
	if clamped_pt.x < 332.0 and clamped_pt.y < 256.0:
		clamped_pt.y = 256.0
	_pos[slot] = clamped_pt
	_clamped[slot] = true
	_dir[slot] = dir
	_alpha[slot] = EDGE_ALPHA


func _color_for(kind: int) -> Color:
	if kind == Kind.DEFEND:
		return TeamColors.score_enemy() if Settings.colorblind else CRIMSON
	if kind == Kind.RESCUE:
		return GOLD if Settings.colorblind else RESCUE_GREEN
	return TeamColors.score_own() if Settings.colorblind else GOLD


func _title_for(kind: int) -> String:
	if kind == Kind.DEFEND:
		return "DEFEND BASE!"
	if kind == Kind.RESCUE:
		return "RESCUE ALLY!"
	return "CAPTURE BASE"


func _glyph_for(kind: int) -> String:
	if kind == Kind.DEFEND:
		return "!"
	if kind == Kind.RESCUE:
		return "+"
	return "B"


func _draw() -> void:
	if _count <= 0:
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	for i: int in _count:
		var at: Vector2 = _pos[i]
		var a: float = _alpha[i]
		var col: Color = Color(_color_for(_kind[i]), a)
		var ink_a: Color = Color(INK, a)
		var paper_a: Color = Color(PAPER, a)
		var shadow_a: Color = Color(SHADOW, SHADOW.a * a)
		var wave: float = 0.0 if Settings.reduced_motion else sin(_pulse * 7.5 + float(i))
		var scale_pop: float = 1.0 + (0.08 * wave if _urgent[i] else 0.0)
		var r: float = 14.0 * scale_pop

		# Directional chevron pointer (points down when in-world, outward when clamped to edge)
		var pointer_dir: Vector2 = _dir[i] if _clamped[i] else Vector2.DOWN
		var side: Vector2 = Vector2(-pointer_dir.y, pointer_dir.x)
		var tip: Vector2 = at + pointer_dir * (r + 9.0)
		var chev_out: PackedVector2Array = PackedVector2Array([
			tip,
			at + pointer_dir * (r + 1.0) + side * 7.0,
			at + pointer_dir * (r + 1.0) - side * 7.0,
		])
		draw_colored_polygon(chev_out, ink_a)
		var chev_in: PackedVector2Array = PackedVector2Array([
			at + pointer_dir * (r + 6.8),
			at + pointer_dir * (r + 2.0) + side * 4.5,
			at + pointer_dir * (r + 2.0) - side * 4.5,
		])
		draw_colored_polygon(chev_in, col)

		# 90s Komiks diamond badge
		var diamond_shadow: PackedVector2Array = PackedVector2Array([
			at + Vector2(2.0, -r + 3.0),
			at + Vector2(r + 2.0, 3.0),
			at + Vector2(2.0, r + 3.0),
			at + Vector2(-r + 2.0, 3.0),
		])
		draw_colored_polygon(diamond_shadow, shadow_a)
		var diamond_ink: PackedVector2Array = PackedVector2Array([
			at + Vector2(0.0, -(r + 2.8)),
			at + Vector2(r + 2.8, 0.0),
			at + Vector2(0.0, r + 2.8),
			at + Vector2(-(r + 2.8), 0.0),
		])
		draw_colored_polygon(diamond_ink, ink_a)
		var diamond_fill: PackedVector2Array = PackedVector2Array([
			at + Vector2(0.0, -r),
			at + Vector2(r, 0.0),
			at + Vector2(0.0, r),
			at + Vector2(-r, 0.0),
		])
		draw_colored_polygon(diamond_fill, col)

		var glyph: String = _glyph_for(_kind[i])
		var gw: float = font.get_string_size(glyph, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
		var g_pos: Vector2 = Vector2(at.x - gw * 0.5, at.y + 6.0)
		draw_string_outline(font, g_pos, glyph, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 4, ink_a)
		draw_string(font, g_pos, glyph, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, paper_a)

		# Slanted komiks callout strip with objective + meters (placed above badge so it never dips into combat bars)
		var dist_text: String = "%dm" % _meters[i]
		var show_full: bool = _clamped[i] or _urgent[i] or _meters[i] >= 22
		var callout: String = "%s · %s" % [_title_for(_kind[i]), dist_text] if show_full else dist_text
		var tw: float = font.get_string_size(callout, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
		var pad_x: float = 9.0
		var box_w: float = tw + pad_x * 2.0
		var box_h: float = 20.0
		var slant: float = 4.0
		var box_x: float = clampf(at.x - box_w * 0.5, 8.0, maxf(size.x - box_w - 8.0, 8.0))
		var box_y: float = at.y - r - box_h - 5.0
		if _clamped[i] and _dir[i].y < -0.5:
			box_y = at.y + r + 8.0
		if box_y < 220.0 and box_x < 230.0:
			box_x = 230.0
		var plate: PackedVector2Array = PackedVector2Array([
			Vector2(box_x + slant, box_y),
			Vector2(box_x + box_w, box_y),
			Vector2(box_x + box_w - slant, box_y + box_h),
			Vector2(box_x, box_y + box_h),
		])
		draw_colored_polygon(plate, paper_a)
		draw_polyline(PackedVector2Array([plate[0], plate[1], plate[2], plate[3], plate[0]]), ink_a, 2.2, true)
		var text_at: Vector2 = Vector2(box_x + pad_x, box_y + box_h - 4.0)
		draw_string(font, text_at, callout, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, ink_a)
