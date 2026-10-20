class_name DirectionArrows
extends Control
## Soft arrows on the screen edge that say where to go: one toward the enemy base while you are free and playing, and
## one toward your team's box (marked "Rescue") while a teammate is tagged in it. Each shows its distance in
## metres and fades out when its target is already on screen. They sit inside a margin that keeps them off the
## minimap, the score and the controls, and never take a touch. It only reads the sim and the camera.

## Pixels kept clear of the screen edge on each side: left, top (minimap and score row), right, bottom (controls).
const INSET: Vector4 = Vector4(70.0, 250.0, 70.0, 380.0)
## A label sits on a dark pill so it reads over the street and over the kids' bars.
const LABEL_PILL: Color = Color(0.05, 0.07, 0.14, 0.78)
const LABEL_PAD: Vector2 = Vector2(14.0, 6.0)
## A target counts as on screen this far inside the edge.
const ON_SCREEN_MARGIN: float = 60.0
const ARROW_SIZE: float = 30.0
const LABEL_FONT: int = 26
const FADE_SPEED: float = 6.0
const BASE_COLOR: Color = Color(1.0, 0.45, 0.4)
const RESCUE_COLOR: Color = Color(1.0, 0.82, 0.3)
const TARGET_HEIGHT: float = 0.8
## The rescue marker: when the box is on screen the arrow gives way to a bobbing marker standing over it.
const MARKER_LIFT: float = 150.0
const MARKER_SIZE: float = 22.0
const MARKER_FONT: int = 24
const MARKER_BOB: float = 7.0

## What is shown now: {kind, label, color, point (on the edge), angle, alpha, metres, marker (0 to 1: how much the
## marker over the target is shown), target (where the target is on screen)}. Tests read this.
var arrows: Array[Dictionary] = []
## Where the minimap ends (the hud sets it): arrows stay below it as well as below the INSET.
var minimap_bottom: float = 0.0

var _sim: MatchSim
var _camera: Camera3D
var _layout: MapLayout
var _local_id: int = -1
var _local_team: int = 0
var _alpha: Dictionary[StringName, float] = {}
var _marker: Dictionary[StringName, float] = {}


func setup(sim: MatchSim, camera: Camera3D, local_id: int, local_team: int, layout: MapLayout) -> void:
	_sim = sim
	_camera = camera
	_layout = layout
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


## The point on the edge of `rect` that a ray from `origin` toward `target` leaves by (the target itself when it is
## inside). `origin` is pulled inside the rect first.
static func edge_point(origin: Vector2, target: Vector2, rect: Rect2) -> Vector2:
	if rect.has_point(target):
		return target
	var start: Vector2 = Vector2(clampf(origin.x, rect.position.x, rect.end.x), clampf(origin.y, rect.position.y, rect.end.y))
	var direction: Vector2 = target - start
	if direction.is_zero_approx():
		return start
	var reach: float = INF
	if not is_zero_approx(direction.x):
		reach = minf(reach, ((rect.end.x if direction.x > 0.0 else rect.position.x) - start.x) / direction.x)
	if not is_zero_approx(direction.y):
		reach = minf(reach, ((rect.end.y if direction.y > 0.0 else rect.position.y) - start.y) / direction.y)
	return start + direction * reach


## What should be pointed at now, as {kind, world (x, z), label, color}: nothing unless the match is being played
## and you are free.
func targets() -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	if _sim == null or _sim.phase != MatchSim.Phase.PLAYING or not _sim.players.has(_local_id):
		return found
	var me: PlayerState = _sim.players[_local_id]
	if me.tagged:
		return found
	var own_side: int = _sim.side_for_team(_local_team)
	found.append({"kind": &"base", "world": _layout.base_center(-own_side), "label": "Enemy base", "color": BASE_COLOR})
	for id: int in _sim.players:
		var other: PlayerState = _sim.players[id]
		if id != _local_id and other.team == _local_team and other.tagged:
			found.append({"kind": &"rescue", "world": _layout.jail_rect(own_side).get_center(), "label": "Rescue", "color": RESCUE_COLOR})
			break
	return found


func tick(delta: float) -> void:
	arrows.clear()
	var wanted: Array[Dictionary] = targets()
	var shown: Dictionary[StringName, bool] = {}
	var top: float = maxf(INSET.y, minimap_bottom + 12.0)
	var rect: Rect2 = Rect2(Vector2(INSET.x, top), size - Vector2(INSET.x + INSET.z, top + INSET.w))
	if rect.size.x > 0.0 and rect.size.y > 0.0:
		var me: PlayerState = _sim.players[_local_id] if _sim != null and _sim.players.has(_local_id) else null
		for target: Dictionary in wanted:
			var world: Vector2 = target["world"] as Vector2
			var point: Variant = _project(world)
			var origin: Vector2 = (_project(me.position) as Vector2) if me != null and _project(me.position) != null else size / 2.0
			var edge: Vector2 = edge_point(origin, point as Vector2, rect) if point != null else edge_point(origin, origin + Vector2(0.0, 1.0) * size.y, rect)
			var on_screen: bool = point != null and Rect2(Vector2.ONE * ON_SCREEN_MARGIN, size - Vector2.ONE * ON_SCREEN_MARGIN * 2.0).has_point(point as Vector2)
			var kind: StringName = target["kind"] as StringName
			shown[kind] = true
			var fade_to: float = 0.0 if on_screen else 1.0
			_alpha[kind] = move_toward(_alpha.get(kind, 0.0) as float, fade_to, FADE_SPEED * delta) if delta > 0.0 else fade_to
			var direction: Vector2 = ((point as Vector2) - origin) if point != null else Vector2(0.0, 1.0)
			var marker_to: float = 1.0 if on_screen and kind == &"rescue" else 0.0
			_marker[kind] = move_toward(_marker.get(kind, 0.0) as float, marker_to, FADE_SPEED * delta) if delta > 0.0 else marker_to
			arrows.append({
				"kind": kind, "label": "%s · %d m" % [target["label"], roundi(((me.position if me != null else world) - world).length())],
				"color": target["color"], "point": edge, "angle": direction.angle(), "alpha": _alpha[kind],
				"marker": _marker[kind], "target": point if point != null else Vector2.ZERO,
				"metres": roundi(((me.position if me != null else world) - world).length()),
			})
	for kind: StringName in _alpha.keys():
		if not shown.has(kind):
			_alpha.erase(kind)
			_marker.erase(kind)
	queue_redraw()


## Where on screen the ground point `world` (x, z) shows; null when it is behind the camera.
func _project(world: Vector2) -> Variant:
	var point: Vector3 = Vector3(world.x, TARGET_HEIGHT, world.y)
	if _camera == null or _camera.is_position_behind(point):
		return null
	return _camera.screen_position(point) if _camera is FollowCamera else _camera.unproject_position(point)


func _draw() -> void:
	var beat: float = 1.0 if Settings.reduced_motion else 1.0 + 0.08 * sin(float(Time.get_ticks_msec()) / 1000.0 * TAU * 1.2)
	var font: Font = KalyeahTheme.BUTTON_FONT
	for arrow: Dictionary in arrows:
		_draw_marker(arrow, font)
		var alpha: float = arrow["alpha"] as float
		if alpha <= 0.01:
			continue
		var color: Color = arrow["color"] as Color
		var at: Vector2 = arrow["point"] as Vector2
		var angle: float = arrow["angle"] as float
		var unit: Vector2 = Vector2.from_angle(angle)
		var side: Vector2 = unit.rotated(PI / 2.0)
		var tip: float = ARROW_SIZE * beat
		var points: PackedVector2Array = PackedVector2Array([at + unit * tip, at - unit * tip * 0.6 + side * tip * 0.75, at - unit * tip * 0.2, at - unit * tip * 0.6 - side * tip * 0.75])
		draw_colored_polygon(points, Color(color, 0.92 * alpha))
		draw_polyline(PackedVector2Array([points[0], points[1], points[2], points[3], points[0]]), Color(0.08, 0.05, 0.1, 0.85 * alpha), 4.0, true)
		var text: String = arrow["label"] as String
		var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, LABEL_FONT).x
		var inward: Vector2 = (size / 2.0 - at).normalized()
		var anchor: Vector2 = at + inward * (ARROW_SIZE + 30.0) - Vector2(width / 2.0, -LABEL_FONT * 0.35)
		anchor.x = clampf(anchor.x, 16.0, size.x - width - 16.0)
		var pill: StyleBoxFlat = StyleBoxFlat.new()
		pill.bg_color = Color(LABEL_PILL, LABEL_PILL.a * alpha)
		pill.set_corner_radius_all(18)
		pill.anti_aliasing = true
		draw_style_box(pill, Rect2(anchor + Vector2(-LABEL_PAD.x, -LABEL_FONT - LABEL_PAD.y + 4.0), Vector2(width + LABEL_PAD.x * 2.0, LABEL_FONT + LABEL_PAD.y * 2.0 + 4.0)))
		draw_string_outline(font, anchor, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, LABEL_FONT, 8, Color(0.08, 0.05, 0.1, alpha))
		draw_string(font, anchor, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, LABEL_FONT, Color(1.0, 1.0, 1.0, alpha))


## The marker over the rescue box once the box is on screen: a gold pointer bobbing above it, with "Rescue".
func _draw_marker(arrow: Dictionary, font: Font) -> void:
	var alpha: float = arrow["marker"] as float
	if alpha <= 0.01:
		return
	var bob: float = 0.0 if Settings.reduced_motion else sin(float(Time.get_ticks_msec()) / 1000.0 * TAU * 1.4) * MARKER_BOB
	var anchor: Vector2 = (arrow["target"] as Vector2) + Vector2(0.0, -MARKER_LIFT + bob)
	anchor = anchor.clamp(Vector2(60.0, maxf(INSET.y, minimap_bottom + 12.0)), size - Vector2(60.0, INSET.w))
	var color: Color = arrow["color"] as Color
	var points: PackedVector2Array = PackedVector2Array([anchor + Vector2(0.0, MARKER_SIZE), anchor + Vector2(-MARKER_SIZE, -MARKER_SIZE * 0.6), anchor + Vector2(MARKER_SIZE, -MARKER_SIZE * 0.6)])
	draw_colored_polygon(points, Color(color, 0.95 * alpha))
	draw_polyline(PackedVector2Array([points[0], points[1], points[2], points[0]]), Color(0.08, 0.05, 0.1, 0.85 * alpha), 4.0, true)
	var text: String = arrow["label"] as String
	text = text.get_slice(" · ", 0)
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, MARKER_FONT).x
	var origin: Vector2 = anchor + Vector2(-width / 2.0, -MARKER_SIZE - 12.0)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, MARKER_FONT, 8, Color(0.08, 0.05, 0.1, alpha))
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, MARKER_FONT, Color(1.0, 1.0, 1.0, alpha))
