class_name WorldWaypoints
extends Control
## Projects 3D tactical objectives (`CAPTURE BASE`, `DEFEND BASE`, `RESCUE ALLY`) through Camera3D
## onto soft, airy translucent chevrons + compact distance callouts (`alpha <= 0.44` backing,
## `0.48` near screen center, elevated clear of combat health bars and `BarangayMinimap`).

const EDGE_MARGIN_X: float = 76.0
const EDGE_MARGIN_TOP: float = 88.0
const EDGE_MARGIN_BOTTOM: float = 104.0
const CENTER_ALPHA: float = 0.48
const EDGE_ALPHA: float = 0.78
const WORLD_LIFT_Y: float = 5.4

const PILL_BG: Color = Color(0.07, 0.09, 0.12, 0.40)
const PILL_RIM: Color = Color(1.0, 1.0, 1.0, 0.24)
const CAPTURE_AMBER: Color = Color(1.0, 0.84, 0.44)
const DEFEND_CORAL: Color = Color(0.96, 0.38, 0.32)
const RESCUE_MINT: Color = Color(0.38, 0.94, 0.62)

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
var _pill_box: StyleBoxFlat


func _init() -> void:
	name = "WorldWaypoints"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pill_box = StyleBoxFlat.new()
	_pill_box.bg_color = PILL_BG
	_pill_box.border_color = PILL_RIM
	_pill_box.set_border_width_all(1)
	_pill_box.set_corner_radius_all(UiSkin.RADIUS_S)
	_pill_box.anti_aliasing = true


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

	var enemy_base: Vector2 = sim.layout.base_center(-own_side)
	var taking_enemy: bool = sim.capture_team == local_team and sim.capture_kind == MatchSim.CaptureKind.BASE
	_project_slot(idx, camera, Vector3(enemy_base.x, WORLD_LIFT_Y, enemy_base.y), player.position.distance_to(enemy_base), Kind.CAPTURE, taking_enemy)
	idx += 1

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
			var combat_band_top: float = maxf(EDGE_MARGIN_TOP + 20.0, size.y * 0.19)
			var combat_band_bot: float = size.y * 0.66
			if absf(screen_pt.x - center.x) < size.x * 0.44 and screen_pt.y > combat_band_top and screen_pt.y < combat_band_bot:
				screen_pt.y = combat_band_top
			if screen_pt.x < 264.0 and screen_pt.y < 236.0:
				screen_pt.y = 236.0
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
	if clamped_pt.x < 264.0 and clamped_pt.y < 236.0:
		clamped_pt.y = 236.0
	_pos[slot] = clamped_pt
	_clamped[slot] = true
	_dir[slot] = dir
	_alpha[slot] = EDGE_ALPHA


func _color_for(kind: int) -> Color:
	if kind == Kind.DEFEND:
		return TeamColors.score_enemy() if Settings.colorblind else DEFEND_CORAL
	if kind == Kind.RESCUE:
		return CAPTURE_AMBER if Settings.colorblind else RESCUE_MINT
	return TeamColors.score_own() if Settings.colorblind else CAPTURE_AMBER


func _title_for(kind: int) -> String:
	if kind == Kind.DEFEND:
		return "DEFEND"
	if kind == Kind.RESCUE:
		return "RESCUE"
	return "BASE"


func _draw() -> void:
	if _count <= 0:
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	for i: int in _count:
		var at: Vector2 = _pos[i]
		var a: float = _alpha[i]
		var col: Color = Color(_color_for(_kind[i]), a)
		var white_a: Color = Color(1.0, 1.0, 1.0, a * 0.92)
		var sh_a: Color = Color(0.0, 0.0, 0.0, 0.42 * a)
		var wave: float = 0.0 if Settings.reduced_motion else sin(_pulse * 6.5 + float(i))
		var r: float = 8.5 * (1.0 + (0.08 * wave if _urgent[i] else 0.0))

		# Delicate directional chevron
		var pointer_dir: Vector2 = _dir[i] if _clamped[i] else Vector2.DOWN
		var side: Vector2 = Vector2(-pointer_dir.y, pointer_dir.x)
		var tip: Vector2 = at + pointer_dir * (r + 5.5)
		draw_polyline(PackedVector2Array([
			at + pointer_dir * (r + 1.0) - side * 4.2,
			tip,
			at + pointer_dir * (r + 1.0) + side * 4.2,
		]), col, 2.0, true)

		# Soft translucent diamond pip with thin white rim
		var diamond: PackedVector2Array = PackedVector2Array([
			at + Vector2(0.0, -r),
			at + Vector2(r, 0.0),
			at + Vector2(0.0, r),
			at + Vector2(-r, 0.0),
		])
		draw_colored_polygon(diamond, Color(col.r, col.g, col.b, a * 0.42))
		draw_polyline(PackedVector2Array([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]]), Color(1.0, 1.0, 1.0, a * 0.72), 1.5, true)

		# Minimal translucent pill callout above pip
		var dist_text: String = "%dm" % _meters[i]
		var show_full: bool = _clamped[i] or _urgent[i] or _meters[i] >= 24
		var callout: String = "%s · %s" % [_title_for(_kind[i]), dist_text] if show_full else dist_text
		var tw: float = font.get_string_size(callout, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
		var pad_x: float = 7.0
		var box_w: float = tw + pad_x * 2.0
		var box_h: float = 18.0
		var box_x: float = clampf(at.x - box_w * 0.5, 8.0, maxf(size.x - box_w - 8.0, 8.0))
		var box_y: float = at.y - r - box_h - 4.0
		if _clamped[i] and _dir[i].y < -0.5:
			box_y = at.y + r + 7.0
		if box_y < 210.0 and box_x < 210.0:
			box_x = 210.0
		_pill_box.bg_color = Color(PILL_BG.r, PILL_BG.g, PILL_BG.b, PILL_BG.a * a)
		_pill_box.border_color = Color(1.0, 1.0, 1.0, PILL_RIM.a * a)
		draw_style_box(_pill_box, Rect2(Vector2(box_x, box_y), Vector2(box_w, box_h)))
		var text_at: Vector2 = Vector2(box_x + pad_x, box_y + box_h - 4.0)
		draw_string_outline(font, text_at, callout, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, 2, sh_a)
		draw_string(font, text_at, callout, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, white_a)
