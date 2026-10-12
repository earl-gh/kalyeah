class_name KidModel
extends Node3D
## One of the six kids (docs/PROJECT.md 3.2) as a real 3D model: a rigged GLB with idle, run,
## cast, stun and knock-out animations (tools/blender/build_junjun_model.py), lit with the
## shared matte look (ModelLook). Visual only: the hitbox and every number come from the sim.
## The run cycle is paced by the ground speed so the feet do not skate, a cast plays at the
## weapon's cast time, and a knock-out falls over backwards. Origin at the feet, the kid
## faces -Z (like a camera). Kids with no model of their own yet use FALLBACK_SET.

const MODEL_PATH: String = "res://assets/models/characters/%s.glb"
const PORTRAIT_PATH: String = "res://assets/portraits/%s.png"
const FALLBACK_SET: StringName = &"junjun"
## How tall the kid stands in the world, in metres (a little taller than life so the kids
## read through the wide lens).
const KID_HEIGHT: float = 1.85
const GHOST_COLOR: Color = Color(0.55, 0.55, 0.62)
const HIT_COLOR: Color = Color(1.0, 0.55, 0.55)
## Ground covered by one full run cycle (two steps).
const STRIDE: float = 1.6
const CAST_TIME: float = 0.38
## How long the knockout fall plays before the kid lies down (seconds).
const KO_FALL_TIME: float = 0.35
const HIT_TIME: float = 0.28
const SHADOW_SIZE: float = 1.1
const BLEND: float = 0.14
## The sim steps at 30 Hz but the screen draws faster: with no new position for this
## long the kid is standing still.
const STILL_AFTER: float = 0.2
const LOOPING: Array[String] = ["idle", "run", "stun"]
const STAR_COUNT: int = 5

static var _scenes: Dictionary[String, PackedScene] = {}
static var _portraits: Dictionary[String, Texture2D] = {}
static var _shadow_texture: GradientTexture2D

var character: CharacterDef
var team_color: Color = Color.WHITE
## A foot hit the ground (run cycle), for footstep sounds.
signal footstep

var _set: StringName = FALLBACK_SET
var _body: Node3D  # leans and falls over (pivot at the feet)
var _model: Node3D
var _player: AnimationPlayer
var _materials: Array[StandardMaterial3D] = []
var _shadow: MeshInstance3D
var _stars: Node3D
var _can: MeshInstance3D
var _facing: Vector2 = Vector2(0.0, 1.0)
var _phase: float = 0.0
var _clock: float = 0.0
var _ghost: bool = false
var _cast_left: float = 0.0
var _cast_total: float = CAST_TIME
var _cast_serial: int = 0
var _cast_played: int = 0
var _hit_left: float = 0.0
var _ko_time: float = 0.0
var _lie: float = 0.0  # 0 standing, 1 lying on the back
var _move_blend: float = 0.0
var _last_step: int = 0
var _anim: String = "idle"
var _playing: String = ""
var _tracked_position: Vector2 = Vector2.INF
var _since_move: float = 0.0
var _ground_speed: float = 0.0
## Last pose name, for tests and debugging ("idle", "run", "cast", "ko", ...).
var pose: String = "idle"


## `team` is kept for callers; teams are told apart by the health bar, not the kid.
func setup(def: CharacterDef, team: Color, _ring_radius: float) -> void:
	character = def
	team_color = team
	_set = def.id if ResourceLoader.exists(MODEL_PATH % def.id) else FALLBACK_SET
	_build()
	_play("idle", 1.0)


## Turns the kid to look along `facing` (world x/z).
func face(facing: Vector2) -> void:
	if facing.length_squared() > 0.0001:
		_facing = facing.normalized()
		rotation.y = atan2(-facing.x, -facing.y)


## Throwing arm swing (a weapon was cast).
func play_cast(duration: float = CAST_TIME) -> void:
	_cast_total = maxf(duration, CAST_TIME)
	_cast_left = _cast_total
	_cast_serial += 1


## Flinch (took damage).
func play_hit() -> void:
	_hit_left = HIT_TIME


func is_can() -> bool:
	return _can.visible


func is_ghost() -> bool:
	return _ghost


func set_ghost(on: bool) -> void:
	_ghost = on


## Which animation is showing ("idle", "run", "cast", "stun", "ko_stagger", "ko_lying").
func current_animation() -> String:
	return _anim


## The ground speed in m/s from the kid's world position, held between sim ticks
## (the sim moves in 30 Hz steps, so most drawn frames see no movement). Feed it to
## `animate()` every frame.
func ground_speed(world_position: Vector2, delta: float) -> float:
	if _tracked_position == Vector2.INF:
		_tracked_position = world_position
	_since_move += delta
	if world_position != _tracked_position:
		_ground_speed = _tracked_position.distance_to(world_position) / maxf(_since_move, 0.001)
		_tracked_position = world_position
		_since_move = 0.0
	elif _since_move > STILL_AFTER:
		_ground_speed = 0.0
	return _ground_speed


## Per frame: `speed` is the ground speed in m/s.
func animate(delta: float, speed: float, state: PlayerState) -> void:
	_clock += delta
	_cast_left = maxf(0.0, _cast_left - delta)
	_hit_left = maxf(0.0, _hit_left - delta)
	var polymorphed: bool = state.effects.has(StatusEffects.Type.POLYMORPH)
	_model.visible = not polymorphed
	_shadow.visible = true
	_can.visible = polymorphed
	set_ghost(state.tagged)
	if polymorphed:
		pose = "can"
		_stars.visible = false
		_can.rotation.z = sin(_clock * 10.0) * 0.1 * clampf(speed, 0.0, 1.0)
		_can.position.y = absf(sin(_clock * 10.0)) * 0.08 * clampf(speed, 0.0, 1.0)
		return
	_move_blend = move_toward(_move_blend, 1.0 if speed > 0.3 else 0.0, delta * 6.0)
	if speed > 0.3:
		_phase += delta * speed / STRIDE
		var step_index: int = int(floor(_phase * 2.0))
		if step_index != _last_step:
			_last_step = step_index
			footstep.emit()
	var knocked_out: bool = state.effects.has(StatusEffects.Type.KNOCKOUT)
	_ko_time = _ko_time + delta if knocked_out else 0.0
	var anim: String = "idle"
	var play_speed: float = 1.0
	if state.tagged:
		# waiting in the box: just standing there
		pose = "idle"
	elif knocked_out:
		pose = "ko"
		anim = "ko_stagger" if _ko_time < KO_FALL_TIME else "ko_lying"
	elif state.effects.has(StatusEffects.Type.AIRBORNE) or state.effects.has(StatusEffects.Type.BOUNCE):
		pose = "airborne"
		anim = "stun"
		play_speed = 1.6
	elif state.effects.has(StatusEffects.Type.STUN):
		pose = "stunned"
		anim = "stun"
	elif _cast_left > 0.0:
		pose = "cast"
		anim = "cast"
		play_speed = _clip_length("cast") / _cast_total
	elif _move_blend > 0.5:
		pose = "run"
		anim = "run"
		# one cycle per STRIDE metres: the feet keep up with the ground
		play_speed = clampf(speed / STRIDE * _clip_length("run"), 0.5, 2.2)
	else:
		pose = "idle"
	_anim = anim
	_drive(anim, play_speed)
	_pose_body(delta, knocked_out)
	_stars.visible = anim == "stun"
	_stars.rotation.y = _clock * 2.6
	_set_tint(_tint())


func _tint() -> Color:
	if _ghost:
		return GHOST_COLOR
	if _hit_left > 0.0:
		return Color.WHITE.lerp(HIT_COLOR, _hit_left / HIT_TIME)
	return Color.WHITE


func _set_tint(tint: Color) -> void:
	var color: Color = ModelLook.tinted(tint)
	for material: StandardMaterial3D in _materials:
		material.albedo_color = color


## Starts the clip for `anim` (blended from whatever was playing) and sets its speed.
func _drive(anim: String, play_speed: float) -> void:
	var clip: String = "ko" if anim.begins_with("ko") else anim
	if clip != _playing or (clip == "cast" and _cast_played != _cast_serial):
		_cast_played = _cast_serial
		_play(clip, play_speed)
	_player.speed_scale = play_speed


func _play(clip: String, play_speed: float) -> void:
	_playing = clip
	_player.play(clip, BLEND, play_speed)
	if clip == "cast" or clip == "ko":
		_player.seek(0.0, true)


func _clip_length(clip: String) -> float:
	return _player.get_animation(clip).length


## The whole body tips over backwards in a knockout (and gets back up), and flinches back
## a little when hit.
func _pose_body(delta: float, knocked_out: bool) -> void:
	if knocked_out:
		_lie = move_toward(_lie, 1.0, delta / KO_FALL_TIME)
	else:
		_lie = move_toward(_lie, 0.0, delta * 5.0)
	var fall: float = _lie * _lie * (3.0 - 2.0 * _lie)
	var flinch: float = sin((1.0 - _hit_left / HIT_TIME) * PI) if _hit_left > 0.0 else 0.0
	_body.rotation.x = fall * PI * 0.5 + flinch * 0.16
	_body.position = Vector3(0.0, fall * 0.16, flinch * 0.06)


## A kid's standing portrait for HUD cards (falls back to FALLBACK_SET).
static func portrait(character_id: StringName) -> Texture2D:
	var set_id: String = String(character_id)
	if not ResourceLoader.exists(PORTRAIT_PATH % set_id):
		set_id = String(FALLBACK_SET)
	if not _portraits.has(set_id):
		_portraits[set_id] = load(PORTRAIT_PATH % set_id) as Texture2D
	return _portraits[set_id]


static func _scene(set_id: StringName) -> PackedScene:
	var path: String = MODEL_PATH % set_id
	if not _scenes.has(path):
		_scenes[path] = load(path) as PackedScene
	return _scenes[path]


func _build() -> void:
	_shadow = MeshInstance3D.new()
	_shadow.name = "Shadow"
	var quad: QuadMesh = QuadMesh.new()
	quad.size = Vector2.ONE * SHADOW_SIZE
	quad.orientation = PlaneMesh.FACE_Y
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_texture = _blob_texture()
	quad.material = material
	_shadow.mesh = quad
	_shadow.position.y = 0.04
	_shadow.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_shadow)
	_body = Node3D.new()
	_body.name = "Body"
	add_child(_body)
	_model = _scene(_set).instantiate() as Node3D
	_model.name = "Model"
	_model.rotation.y = PI  # glTF models face +Z, the kid faces -Z
	_body.add_child(_model)
	var mesh: MeshInstance3D = _model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	_model.scale = Vector3.ONE * (KID_HEIGHT / mesh.get_aabb().size.y)
	_materials = ModelLook.apply(_model)
	_player = _model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	for clip: String in _player.get_animation_list():
		var animation: Animation = _player.get_animation(clip)
		if clip in LOOPING:
			animation.loop_mode = Animation.LOOP_LINEAR
			_close_loop(animation)
		else:
			animation.loop_mode = Animation.LOOP_NONE
	_stars = _build_stars()
	add_child(_stars)
	_can = MeshInstance3D.new()
	_can.mesh = Props.can_body()
	_can.scale = Vector3.ONE * 1.3
	_can.visible = false
	add_child(_can)
	LowPoly.add_outline(_can, 0.02)


## The clips end one frame before they start again: the loop is one frame longer, so the last
## pose blends into the first instead of jumping (and no frame is held twice).
static func _close_loop(animation: Animation) -> void:
	# the imported clips are shared between every kid: do it once
	if animation.has_meta("loop_closed"):
		return
	animation.set_meta("loop_closed", true)
	var keys: int = animation.track_get_key_count(0)
	if keys > 1:
		animation.length += animation.length / float(keys - 1)


## A ring of little yellow stars over the head while stunned.
func _build_stars() -> Node3D:
	var ring: Node3D = Node3D.new()
	ring.name = "Stars"
	ring.position.y = KID_HEIGHT + 0.12
	ring.visible = false
	var mesh: SphereMesh = SphereMesh.new()
	mesh.radius = 0.08
	mesh.height = 0.08
	mesh.radial_segments = 6
	mesh.rings = 3
	var star: StandardMaterial3D = StandardMaterial3D.new()
	star.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	star.albedo_color = Color(1.0, 0.82, 0.15)
	mesh.material = star
	for i: int in STAR_COUNT:
		var piece: MeshInstance3D = MeshInstance3D.new()
		piece.mesh = mesh
		var angle: float = TAU * float(i) / float(STAR_COUNT)
		piece.position = Vector3(cos(angle), 0.0, sin(angle)) * 0.3
		piece.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		ring.add_child(piece)
	return ring


static func _blob_texture() -> GradientTexture2D:
	if _shadow_texture == null:
		var gradient: Gradient = Gradient.new()
		gradient.colors = PackedColorArray([Color(0.0, 0.0, 0.0, 0.45), Color(0.0, 0.0, 0.0, 0.0)])
		gradient.offsets = PackedFloat32Array([0.0, 1.0])
		_shadow_texture = GradientTexture2D.new()
		_shadow_texture.gradient = gradient
		_shadow_texture.fill = GradientTexture2D.FILL_RADIAL
		_shadow_texture.fill_from = Vector2(0.5, 0.5)
		_shadow_texture.fill_to = Vector2(1.0, 0.5)
		_shadow_texture.width = 64
		_shadow_texture.height = 64
	return _shadow_texture
