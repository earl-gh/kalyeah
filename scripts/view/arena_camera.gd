class_name ArenaCamera
extends Camera3D
## The fixed orthographic gameplay camera (see CameraRules): it never moves, so the whole arena is on screen. Own base is
## always at the bottom of the screen; the other team's view is rotated 180 degrees. It only re-frames when the
## screen's shape changes (a window resize, a rotation) or the view flips.

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
var _flip: bool = false
var _aspect: float = -1.0
var _bottom: float = -1.0
var _top: float = -1.0
## (distance, shift along the street) for the aspect framed last.
var _frame: Vector2 = Vector2.ZERO
## The height (UI pixels) of the controller body at the bottom of the screen: the arena stays above it.
var bottom_reserve_px: float = 0.0
## The height (UI pixels) of the header at the top of the screen (the score and settings): the arena stays below it.
var top_reserve_px: float = 0.0


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
## (the mouse aim casts its ray through it).
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
	var target: Vector2 = rules.camera_target(framed.y, flip)
	return Vector3(target.x, 0.0, target.y) + rules.camera_offset(CameraRules.STANDOFF, flip)


## Puts the camera where it frames the whole arena (a no-op when nothing changed).
func place(flip: bool) -> void:
	var aspect: float = screen_aspect()
	if _placed and flip == _flip and is_equal_approx(aspect, _aspect) and is_equal_approx(_bottom, _current_bottom()) and is_equal_approx(_top, _current_top()):
		return
	var framed: Vector2 = _framed()
	position = desired_position(flip)
	rotation_degrees = Vector3(-rules.pitch_degrees, rules.yaw_degrees(flip), 0.0)
	_use_gameplay_lens(framed.x)
	near = maxf(1.0, CameraRules.STANDOFF - NEAR_MARGIN)
	far = CameraRules.STANDOFF + FAR_MARGIN
	_placed = true
	_flip = flip


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
	if not Settings.reduced_motion:
		_shake = maxf(_shake, strength)


func _process(delta: float) -> void:
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


func _framed() -> Vector2:
	var aspect: float = screen_aspect()
	var viewport: Viewport = get_viewport()
	var height: float = viewport.get_visible_rect().size.y if viewport != null else 1280.0
	var bottom: float = maxf(rules.frame_bottom, bottom_reserve_px / maxf(height, 1.0))
	var top: float = _current_top()
	if not is_equal_approx(aspect, _aspect) or not is_equal_approx(bottom, _bottom) or not is_equal_approx(top, _top):
		_aspect = aspect
		_bottom = bottom
		_top = top
		_frame = rules.frame(aspect, Vector2(layout.lane_width, layout.lane_length), bottom, top)
	return _frame
