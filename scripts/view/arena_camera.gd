class_name ArenaCamera
extends Camera3D
## The fixed orthographic gameplay camera (see CameraRules): it never moves, so the whole arena is on screen. Own base is
## always at the bottom of the screen; the other team's view is rotated 180 degrees. It only re-frames when the
## screen's shape changes (a window resize, a rotation) or the view flips. A smooth flip (`place(flip, true)`) swings the whole camera
## half way round the arena's centre over `CameraRules.turn_seconds`, eased in and out.

const SHAKE_DECAY: float = 1.4
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


## The own view's pose turned `turn_degrees()` round the arena's centre (at 180 it is exactly the flipped pose).
func _apply_pose() -> void:
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
