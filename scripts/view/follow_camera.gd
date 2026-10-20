class_name FollowCamera
extends Camera3D
## Team-relative gameplay camera (see CameraRules): a perspective camera above and
## behind the hero at a fixed downward angle, framed for a portrait 9:16 phone. Own
## base is always at the bottom of the screen; the other team's view is rotated 180
## degrees. Eases
## toward its target (CameraRules.follow_rate); snaps on the first frame and when
## the view flips (new set).

const SHAKE_DECAY: float = 1.4
## Anything closer than this to the camera is cut away. The camera is low (about 5 m up), so a
## bigger value slices off props near it (the end barricades, the hoop's pole).
const NEAR: float = 0.4
const FAR: float = 170.0

@export var rules: CameraRules
@export var layout: MapLayout

## Screen-shake strength left (decays quickly); view only.
var _shake: float = 0.0
var _placed: bool = false
var _flip: bool = false
## The camera's turn right now (CameraRules.yaw_for, eased as the hero crosses the street): stick and aim read it.
var _yaw: float = 0.0
## How far the camera has peeked toward an aim (world metres from the hero), eased by CameraRules.peek_rate.
var _peek: Vector2 = Vector2.ZERO


func _ready() -> void:
	projection = Camera3D.PROJECTION_PERSPECTIVE
	keep_aspect = Camera3D.KEEP_HEIGHT
	# the wide curve lens bends this picture (shaders/lens_curve.gdshader): render a wider view for it
	fov = rules.render_vfov_degrees()
	near = NEAR
	far = FAR
	environment = StreetMap.make_environment()


## Current screen aspect (width / height); portrait phones are about 0.46 to 0.56.
func screen_aspect() -> float:
	var viewport: Viewport = get_viewport()
	if viewport == null:
		return 0.5625
	var size: Vector2 = viewport.get_visible_rect().size
	return size.x / size.y if size.y > 0.0 else 0.5625


## Where on screen (pixels) the world point `world` shows, with the wide curve lens applied.
func screen_position(world: Vector3) -> Vector2:
	var flat: Vector2 = unproject_position(world)
	var size: Vector2 = get_viewport().get_visible_rect().size
	return LensCurve.output_px(flat, size, rules.lens_k())


## The lens undone for a screen position (pixels): where the flat camera picture is at `screen`
## (the mouse aim casts its ray through it).
func flat_position(screen: Vector2) -> Vector2:
	var size: Vector2 = get_viewport().get_visible_rect().size
	return LensCurve.source_px(screen, size, rules.lens_k())


## Distance from the hero's ground point to the camera for this screen.
func distance() -> float:
	return rules.distance(screen_aspect())


## The camera's turn (degrees, without the other team's half turn) right now: screen directions follow it.
func yaw_offset() -> float:
	return _yaw


## Where the camera wants to be for a hero at `world_pos`, turned by `yaw` (default: where it should be for that spot).
func desired_position(world_pos: Vector2, flip: bool, yaw: float = NAN) -> Vector3:
	var turn: float = rules.yaw_for(world_pos.x, flip) if is_nan(yaw) else yaw
	var target: Vector2 = rules.camera_target(world_pos, flip, distance(), turn)
	return Vector3(target.x, 0.0, target.y) + rules.camera_offset(distance(), flip, turn)


## The peek the camera has made right now (world metres from the hero).
func peek_offset() -> Vector2:
	return _peek


## `delta` 0 snaps straight to the target. `peek` is where the camera should lean (see CameraRules.peek_for).
func follow(world_pos: Vector2, flip: bool, delta: float = 0.0, peek: Vector2 = Vector2.ZERO) -> void:
	var wanted_yaw: float = rules.yaw_for(world_pos.x, flip)
	var snap: bool = not _placed or flip != _flip or delta <= 0.0
	_yaw = wanted_yaw if snap else lerpf(_yaw, wanted_yaw, rules.follow_weight(delta))
	_peek = peek if snap else _peek.lerp(peek, 1.0 - exp(-rules.peek_rate * delta))
	var desired: Vector3 = desired_position(world_pos + _peek, flip, _yaw)
	position = desired if snap else position.lerp(desired, rules.follow_weight(delta))
	_placed = true
	_flip = flip
	rotation_degrees = Vector3(-rules.pitch_degrees, rules.yaw_degrees(flip, _yaw), 0.0)


## A fixed cinematic shot (the opening show): stand at `eye` looking at `target`.
## The next follow() snaps back to the gameplay view.
func frame_shot(eye: Vector3, target: Vector3) -> void:
	position = eye
	look_at(target, Vector3.UP)
	_placed = false


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
