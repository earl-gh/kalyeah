class_name ArenaCamera
extends Camera3D
## The orthographic gameplay camera (see CameraRules). With `CameraRules.follow` (the landscape match, Wild Rift style) it keeps your kid in
## the middle of the screen and the street scrolls (`follow()`), held back at the street's ends. Otherwise it is fixed and the whole arena is on
## screen. Own base is at the left (the turned landscape camera) or the bottom (the fixed one); the other team's view is rotated 180 degrees. It only re-frames when the
## screen's shape changes (a window resize, a rotation) or the view flips. A smooth flip (`place(flip, true)`) swings the whole camera
## half way round the arena's centre over `CameraRules.turn_seconds`, eased in and out.

const SHAKE_DECAY: float = 1.4
## The follow camera closes all but e^-FOLLOW_CLOSE (about 5%) of the gap to your kid in `CameraRules.follow_lag` seconds.
const FOLLOW_CLOSE: float = 3.0
## The depth range is cut tight around the arena (the camera stands CameraRules.STANDOFF away), for depth precision and for the shadows.
const NEAR_MARGIN: float = 70.0
const FAR_MARGIN: float = 100.0
## The opening show flies close to the post with a perspective lens: a wide lens and a long depth range.
const CINEMATIC_FOV: float = 60.0
const CINEMATIC_NEAR: float = 0.4
const CINEMATIC_FAR: float = 400.0

@export var rules: CameraRules
@export var layout: MapLayout

## Screen-shake strength left (decays quickly); view only.
var _shake: float = 0.0
var _placed: bool = false
## The view the camera is placed for or turning to.
var _goal_flip: bool = false
## How far round the camera is: 0 = the own view, 1 = the flipped view (between them while it turns).
var _turn: float = 0.0
var _aspect: float = -1.0
var _bottom: float = -1.0
var _top: float = -1.0
## (distance, shift along the street) for the aspect framed last.
var _frame: Vector2 = Vector2.ZERO
var _aim_x: float = 0.0
var _left: float = -1.0
var _right: float = -1.0
## The height (UI pixels) of the controller body at the bottom of the screen: the arena stays above it.
var bottom_reserve_px: float = 0.0
## The height (UI pixels) of the header at the top of the screen (the score and settings): the arena stays below it.
var top_reserve_px: float = 0.0
## The width (UI pixels) of a panel along one side of the screen (the cards of Settings > Cards tray = Sides), and whether it is the left one:
## the street is framed in what is left, pushed to the other side at the size that fits (the same shape, a smaller picture).
var side_reserve_px: float = 0.0
var side_reserve_left: bool = false
## Follow camera: the point (sim XZ) the view keeps in the middle, and where it aims now (catching up over `CameraRules.follow_lag`).
var follow_point: Vector2 = Vector2.ZERO
var _aim: Vector2 = Vector2.ZERO
var _aim_set: bool = false
## Shoulder camera (`CameraRules.third_person`): where the view looks (degrees: yaw 0 = down -Z, pitch down positive), turned by `look()`.
var look_yaw: float = 0.0
var look_pitch: float = 0.0
## The far plane of the shoulder camera's lens (metres; the near one is `CameraRules.shoulder_near`).
const SHOULDER_FAR: float = 400.0
## Sprint / Luksong-Tinik dash lens kick: degrees added to `shoulder_fov`, and smoothing speed.
const SPEED_BOOST_FOV: float = 6.5
const SPEED_BOOST_LERP: float = 8.0

var _speed_boosting: bool = false
var _fov_boost: float = 0.0
var _obstacles_layout: MapLayout = null
var _obstacles_count: int = -1
var _obstacles_cache: Array[Rect2] = []


func _ready() -> void:
	_use_gameplay_lens(1.0)
	keep_aspect = Camera3D.KEEP_HEIGHT
	environment = StreetMap.make_environment()


## Current screen aspect (width / height).
func screen_aspect() -> float:
	var viewport: Viewport = get_viewport()
	if viewport == null:
		return CameraRules.DESIGN_ASPECT
	var size: Vector2 = viewport.get_visible_rect().size
	return size.x / size.y if size.y > 0.0 else CameraRules.DESIGN_ASPECT


## Where on screen (pixels) the world point `world` shows, with the lens applied.
func screen_position(world: Vector3) -> Vector2:
	var flat: Vector2 = unproject_position(world)
	var size: Vector2 = get_viewport().get_visible_rect().size
	return LensCurve.output_px(flat, size, rules.lens_k())


## The lens undone for a screen position (pixels): where the flat camera picture is at `screen`
## (the street point under a finger casts its ray through it).
func flat_position(screen: Vector2) -> Vector2:
	var size: Vector2 = get_viewport().get_visible_rect().size
	return LensCurve.source_px(screen, size, rules.lens_k())


## How many metres tall the view is for this screen (the orthographic size that fits the whole arena).
func view_size() -> float:
	if rules.third_person:
		return rules.follow_view_size
	if rules.follow:
		return rules.follow_view_size
	return _framed().x


## Distance from the aim point to the camera (fixed: an orthographic camera looks the same from any distance).
func distance() -> float:
	return CameraRules.STANDOFF


## Where the camera wants to be for the team that flips (or not).
func desired_position(flip: bool) -> Vector3:
	var framed: Vector2 = _framed()
	var target: Vector2 = rules.camera_target(framed.y, flip, _aim_x)
	return Vector3(target.x, 0.0, target.y) + rules.camera_offset(CameraRules.STANDOFF, flip)


## Puts the camera where it frames the whole arena (a no-op when nothing changed). With `smooth` a change of `flip` swings the
## camera round over `CameraRules.turn_seconds` (see `_process`) instead of cutting; the first placing and Reduce motion always cut.
func place(flip: bool, smooth: bool = false) -> void:
	if rules.third_person:
		_place_shoulder(flip)
		return
	if rules.follow:
		_place_following(flip, smooth)
		return
	var aspect: float = screen_aspect()
	var sides: Vector2 = _current_sides()
	var reframe: bool = not _placed or not is_equal_approx(aspect, _aspect) or not is_equal_approx(_bottom, _current_bottom()) \
			or not is_equal_approx(_top, _current_top()) or not is_equal_approx(_left, sides.x) or not is_equal_approx(_right, sides.y)
	if flip != _goal_flip or not _placed:
		_goal_flip = flip
		if not smooth or not _placed or Settings.reduced_motion:
			_turn = 1.0 if flip else 0.0
		reframe = true
	if reframe:
		_apply_pose()


## The view the screen shows now: the flipped one once a turn toward it is half done (the controls follow this).
func shown_flip() -> bool:
	return _turn >= 0.5


## True while the camera swings from one view to the other.
func is_turning() -> bool:
	return _turn != (1.0 if _goal_flip else 0.0)


## How far round (degrees) the camera is shown: 0 for the own view, 180 for the flipped one, eased between them.
func turn_degrees() -> float:
	return 180.0 * smoothstep(0.0, 1.0, _turn)


## Follow camera: keep `point` (sim XZ, your kid) in the middle of the screen. `snap` jumps there at once (a new match, a respawn).
func follow(point: Vector2, snap: bool = false) -> void:
	follow_point = point
	if rules.third_person:
		_aim = point  # the shoulder camera sticks to the kid: a lag would let the kid slide across the crosshair
		_aim_set = true
	elif snap or not _aim_set:
		_aim = _follow_goal()
		_aim_set = true


## Where the followed view wants to aim now: your kid, held back at the street's ends.
func _follow_goal() -> Vector2:
	return rules.follow_aim(follow_point, Vector2(layout.lane_width, layout.lane_length), rules.follow_view_size, screen_aspect(), rules.yaw_degrees(false) + turn_degrees())


## Shoulder camera: the first placing and a change of view (Tumbang Preso's taya, Patintero's roles) look down the street from your end again.
func _place_shoulder(flip: bool) -> void:
	if flip != _goal_flip or not _placed:
		_goal_flip = flip
		_turn = 1.0 if flip else 0.0
		reset_look()
	_apply_pose()


## Shoulder camera: look straight down the street toward the other end, tilted the rules' way down.
func reset_look() -> void:
	look_yaw = 180.0 if _goal_flip else 0.0
	look_pitch = rules.look_pitch_degrees


## Shoulder camera: a finger dragged `drag` UI pixels: right turns the view right, up looks up (within the pitch limits).
func look(drag: Vector2) -> void:
	if not rules.third_person:
		return
	look_yaw = fposmod(look_yaw - drag.x * rules.look_sensitivity, 360.0)
	look_pitch = rules.clamp_pitch(look_pitch + drag.y * rules.look_sensitivity)
	if _placed:
		_apply_pose()


## Shoulder camera: the crosshair, in the middle of the screen (viewport pixels).
func crosshair() -> Vector2:
	var viewport: Viewport = get_viewport()
	return viewport.get_visible_rect().size / 2.0 if viewport != null else Vector2.ZERO


## Follow camera: the same turn as the fixed camera (a flip swings it round, here round the point it aims at), placed every frame.
func _place_following(flip: bool, smooth: bool) -> void:
	if flip != _goal_flip or not _placed:
		_goal_flip = flip
		if not smooth or not _placed or Settings.reduced_motion:
			_turn = 1.0 if flip else 0.0
	_apply_pose()


## Smoothly widens perspective FOV by +6.5 degrees during sprint/dash when Reduce motion is off.
func set_speed_boost(boosting: bool) -> void:
	_speed_boosting = boosting
	if Settings.reduced_motion and _fov_boost != 0.0:
		_fov_boost = 0.0
		if _placed and rules != null and rules.third_person:
			fov = rules.shoulder_fov


func _step_fov_boost(delta: float) -> void:
	var target: float = SPEED_BOOST_FOV if (_speed_boosting and not Settings.reduced_motion) else 0.0
	if Settings.reduced_motion:
		_fov_boost = 0.0
		return
	if is_equal_approx(_fov_boost, target):
		_fov_boost = target
		return
	if delta > 0.0:
		_fov_boost = lerpf(_fov_boost, target, 1.0 - exp(-delta * SPEED_BOOST_LERP))


func _obstacle_rects() -> Array[Rect2]:
	if layout == null:
		return []
	if layout != _obstacles_layout or layout.obstacles.size() != _obstacles_count:
		_obstacles_layout = layout
		_obstacles_count = layout.obstacles.size()
		_obstacles_cache = layout.obstacle_rects()
	return _obstacles_cache


## Follow camera: moves the aim toward the goal (it catches up over `follow_lag`; at once with Reduce motion or no lag).
func advance_follow(delta: float) -> void:
	_step_fov_boost(delta)
	if not (rules.follow or rules.third_person) or not _placed:
		return
	var goal: Vector2 = follow_point if rules.third_person else _follow_goal()
	if rules.third_person:
		_aim = goal
		_apply_pose()
		return
	if Settings.reduced_motion or rules.follow_lag <= 0.0:
		_aim = goal
	else:
		_aim = _aim.lerp(goal, 1.0 - exp(-delta * FOLLOW_CLOSE / rules.follow_lag))
	_apply_pose()


## Follow camera: where the view aims now (sim XZ: the middle of the screen).
func aim_point() -> Vector2:
	return _aim


## The side of the street the camera stands over (-1: the -X side, 1: the +X side), or 0 when it looks along the street (the fixed camera).
func near_side() -> int:
	if rules.third_person:
		return 0  # the camera stands in the street: every house stays
	var back: Vector3 = rules.camera_offset(CameraRules.STANDOFF, false).rotated(Vector3.UP, deg_to_rad(turn_degrees()))
	if absf(back.x) <= absf(back.z):
		return 0
	return 1 if back.x > 0.0 else -1


## The ground ways (sim XZ) that point right on the screen and up the screen now.
func ground_axes() -> Array[Vector2]:
	if rules.third_person:
		return CameraRules.ground_axes(look_yaw)
	return CameraRules.ground_axes(rules.yaw_degrees(false) + turn_degrees())


## A way on the screen (x right, y down: a joystick) as a way on the ground (sim XZ), whichever way the camera is turned. While the camera swings
## round the controls swap at halfway (`shown_flip`), as the map does.
func ground_way(screen_way: Vector2) -> Vector2:
	return CameraRules.ground_way(screen_way, _control_yaw())


## A way on the ground (sim XZ) as a way on the screen (x right, y down): the other way round from `ground_way`.
func screen_way(ground: Vector2) -> Vector2:
	var axes: Array[Vector2] = CameraRules.ground_axes(_control_yaw())
	return Vector2(ground.dot(axes[0]), -ground.dot(axes[1]))


## The turn the controls follow: where the shoulder camera looks, or the shown view of the others.
func _control_yaw() -> float:
	return look_yaw if rules.third_person else rules.yaw_degrees(shown_flip())


## The own view's pose turned `turn_degrees()` round the arena's centre (at 180 it is exactly the flipped pose). The follow camera turns round
## the point it aims at instead.
func _apply_pose() -> void:
	if rules.third_person:
		var pose: Array[Vector3] = rules.shoulder_pose(_aim, look_yaw, look_pitch)
		var eye: Vector3 = pose[0]
		if layout != null:
			var pivot: Vector3 = Vector3(_aim.x, rules.shoulder_height, _aim.y)
			var arena_r: Variant = layout.get(&"arena_radius")
			var radius: float = float(arena_r) if (arena_r is float or arena_r is int) else 0.0
			eye = rules.clamp_shoulder_eye(pivot, eye, _obstacle_rects(), radius)
		position = eye
		rotation_degrees = pose[1]
		projection = Camera3D.PROJECTION_PERSPECTIVE
		fov = rules.shoulder_fov + (0.0 if Settings.reduced_motion else _fov_boost)
		near = rules.shoulder_near
		far = SHOULDER_FAR
		_placed = true
		return
	if rules.follow:
		var turned: float = turn_degrees()
		position = Vector3(_aim.x, 0.0, _aim.y) + rules.camera_offset(CameraRules.STANDOFF, false).rotated(Vector3.UP, deg_to_rad(turned))
		rotation_degrees = Vector3(-rules.pitch_degrees, rules.yaw_degrees(false) + turned, 0.0)
		_use_gameplay_lens(rules.follow_view_size)
		near = maxf(1.0, CameraRules.STANDOFF - NEAR_MARGIN)
		far = CameraRules.STANDOFF + FAR_MARGIN
		_placed = true
		return
	var framed: Vector2 = _framed()
	var angle: float = turn_degrees()
	position = desired_position(false).rotated(Vector3.UP, deg_to_rad(angle))
	rotation_degrees = Vector3(-rules.pitch_degrees, rules.yaw_degrees(false) + angle, 0.0)
	_use_gameplay_lens(framed.x)
	near = maxf(1.0, CameraRules.STANDOFF - NEAR_MARGIN)
	far = CameraRules.STANDOFF + FAR_MARGIN
	_placed = true


## Advances a smooth turn (`place(flip, true)`); Reduce motion switched on mid-turn finishes it at once.
func advance_turn(delta: float) -> void:
	if not _placed or not is_turning():
		return
	var goal: float = 1.0 if _goal_flip else 0.0
	_turn = goal if Settings.reduced_motion else move_toward(_turn, goal, delta / maxf(rules.turn_seconds, 0.001))
	_apply_pose()


## A fixed cinematic shot (the opening show): stand at `eye` looking at `target`. The next place() snaps back to
## the gameplay view.
func frame_shot(eye: Vector3, target: Vector3) -> void:
	position = eye
	look_at(target, Vector3.UP)
	projection = Camera3D.PROJECTION_PERSPECTIVE
	fov = CINEMATIC_FOV
	near = CINEMATIC_NEAR
	far = CINEMATIC_FAR
	_placed = false


## The gameplay lens: orthographic, `size` metres tall.
func _use_gameplay_lens(size_metres: float) -> void:
	projection = Camera3D.PROJECTION_ORTHOGONAL
	size = size_metres


## Kick the camera (a hit on you, a wall breaking). `strength` ~0.1 small, 0.4 big.
func shake(strength: float) -> void:
	if Settings.screen_shake and not Settings.reduced_motion:
		_shake = maxf(_shake, strength)


func _process(delta: float) -> void:
	advance_turn(delta)
	advance_follow(delta)
	if Settings.reduced_motion or _shake <= 0.001:
		_shake = 0.0
		h_offset = 0.0
		v_offset = 0.0
		return
	h_offset = randf_range(-1.0, 1.0) * _shake
	v_offset = randf_range(-1.0, 1.0) * _shake
	_shake = maxf(0.0, _shake - delta * SHAKE_DECAY)


## The bottom margin the current screen needs (a fraction of its height).
func _current_bottom() -> float:
	var viewport: Viewport = get_viewport()
	var height: float = viewport.get_visible_rect().size.y if viewport != null else 1280.0
	return maxf(rules.frame_bottom, bottom_reserve_px / maxf(height, 1.0))


## The top margin the current screen needs (a fraction of its height).
func _current_top() -> float:
	var viewport: Viewport = get_viewport()
	var height: float = viewport.get_visible_rect().size.y if viewport != null else 1280.0
	return maxf(rules.frame_top, top_reserve_px / maxf(height, 1.0))


## The left and right margins the current screen needs (fractions of its width): the street's own side margin, or the side panel's width.
func _current_sides() -> Vector2:
	var viewport: Viewport = get_viewport()
	var width: float = viewport.get_visible_rect().size.x if viewport != null else 720.0
	var panel: float = side_reserve_px / maxf(width, 1.0)
	return Vector2(maxf(rules.frame_side, panel if side_reserve_left else 0.0), maxf(rules.frame_side, 0.0 if side_reserve_left else panel))


func _framed() -> Vector2:
	var aspect: float = screen_aspect()
	var viewport: Viewport = get_viewport()
	var height: float = viewport.get_visible_rect().size.y if viewport != null else 1280.0
	var bottom: float = maxf(rules.frame_bottom, bottom_reserve_px / maxf(height, 1.0))
	var top: float = _current_top()
	var sides: Vector2 = _current_sides()
	if not is_equal_approx(aspect, _aspect) or not is_equal_approx(bottom, _bottom) or not is_equal_approx(top, _top) \
			or not is_equal_approx(sides.x, _left) or not is_equal_approx(sides.y, _right):
		_aspect = aspect
		_bottom = bottom
		_top = top
		_left = sides.x
		_right = sides.y
		_frame = rules.frame(aspect, Vector2(layout.lane_width, layout.lane_length), bottom, top, sides.x, sides.y)
		_aim_x = rules.centring_shift_x(_frame.x, aspect, sides.x, sides.y)
	return _frame
