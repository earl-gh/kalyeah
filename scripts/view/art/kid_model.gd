class_name KidModel
extends Node3D
## One of the six kids (docs/PROJECT.md 3.2) as the owner's own 3D model: a skinned GLB (assets/models/characters/<kid>.glb)
## that keeps its material exactly as authored (no ModelLook, no tint) and plays its own clips (CLIP_FOR) at their own
## speed. Visual only: the hitbox and every number come from the sim. Origin at the feet, the kid faces -Z (like a
## camera). Every cast is Cast_OneHand (standing) or Cast_Run (moving), started at the hand's release moment so the
## release lands on the sim's cast tick; a knock-out shows as Stunned (no kid has a fall); a tagged kid shrinks away
## where it stood, then grows back in the tagged box. A character with no model of its own yet uses FALLBACK_SET.

const MODEL_PATH: String = "res://assets/models/characters/%s.glb"
const PORTRAIT_PATH: String = "res://assets/portraits/%s.png"
## A kid's face cropped from the boot screen key art (tools/art/crop_minimap_faces.py), for the minimap.
const FACE_PATH: String = "res://assets/portraits/faces/%s.png"
const CARD_PATH: String = "res://assets/portraits/cards/%s.webp"
const FALLBACK_SET: StringName = &"junjun"
## How tall the kid stands in the world, in metres (a little taller than life so the kids
## read through the wide lens).
const KID_HEIGHT: float = 1.85
## A polymorphed kid is the owner's can (`assets/models/weapons/can.glb`, the model Lata throws), standing this tall in metres.
const POLYMORPH_CAN_MODEL: String = "can"
const POLYMORPH_CAN_HEIGHT: float = 1.3
const BLEND: float = 0.14
## The dive: how far the body leans forward at its flattest (radians), how high the leap lifts it and how high it skims after (m).
const DIVE_LEAN: float = 1.35
const DIVE_LIFT: float = 0.45
const DIVE_SKIM: float = 0.18
## The sim steps at 30 Hz but the screen draws faster: with no new position for this
## long the kid is standing still.
const STILL_AFTER: float = 0.2
## How fast a kid turns to look along a new facing (radians a second): a flip to the opposite way takes about 0.14 s, so it reads as
## a quick turn and never as the model popping round in one frame (owner: models blinked on sharp turns), and a facing that is
## only there for a tick is a small wiggle, not a flip.
const TURN_RATE: float = 22.0
## A kid that was running keeps its run for this long (seconds) after the measured speed drops to a slow but not zero speed. The speed
## is measured from the sim's 30 Hz steps, and a short step (the last one into a move target, a step cut by a wall, the tick a new tap
## swaps the target) reads as nearly standing: without this the kid flashed into idle for a few frames and back (owner: blinking). A
## speed of exactly 0 is a kid that has stood still (`STILL_AFTER`): no grace, it goes idle at once.
const RUN_GRACE: float = 0.15
## The game's state names -> the clips of the GLBs (their own names). The state names are added to the model's clip library
## as aliases of the same animations.
const CLIP_FOR: Dictionary[String, String] = {
	"idle": "Idle", "run": "Run", "tired": "Exhausted_Run", "stun": "Stunned", "airborne": "Airborne",
	"cast": "Cast_OneHand", "cast_run": "Cast_Run",
}
const LOOPING: Array[String] = ["idle", "run", "tired", "stun", "airborne"]
## The pop (owner): the kid, the can and the stun stars grow in with a little overshoot and shrink out with a small wind-up, in this many
## seconds. A tagged kid pops out where it was tagged and pops in again in the tagged box; a morph pops the kid out and then the can in
## (and back); the stars pop in and out the same way.
const POP_IN_TIME: float = 0.24
const POP_OUT_TIME: float = 0.18
const POP_BACK: float = 1.70158
## The scale at the bottom of the shrink (a true zero breaks the skinned mesh's transform).
const GONE_SCALE: float = 0.001
const STAR_COUNT: int = 5
## The team outline (owner, 2026-10-29): blue round your team, red round the other, this many metres wide.
const OUTLINE_SHADER: Shader = preload("res://shaders/team_outline.gdshader")
const OUTLINE_WIDTH: float = 0.035
## The stencil value a kid's pixels are marked with; the outline draws only where it is not (shaders/team_outline.gdshader).
const STENCIL: int = 1

static var _scenes: Dictionary[String, PackedScene] = {}
static var _portraits: Dictionary[String, Texture2D] = {}
static var _faces: Dictionary[String, Texture2D] = {}

var character: CharacterDef
## A foot hit the ground (run cycle).
signal footstep

var _set: StringName = FALLBACK_SET
var _body: Node3D  # holds the model (pivot at the feet) and is what pops
var _model: Node3D
var _player: AnimationPlayer
var _stars: Node3D
var _can: Node3D
var _facing: Vector2 = Vector2(0.0, 1.0)
var _yaw_target: float = 0.0
var _yaw_known: bool = false
var _still_for: float = 0.0
var _phase: float = 0.0
var _clock: float = 0.0
var _ghost: bool = false
var _cast_left: float = 0.0
var _cast_total: float = 0.0
var _cast_clip: String = "cast"  # Cast_OneHand (standing) or Cast_Run (moving), chosen when the cast starts
var _cast_lead: float = 0.0
## Seconds into the cast clip the last cast started at (tests).
var cast_seek: float = 0.0
var _tired: bool = false
enum Vanish { NONE, SHRINK, GROW }
var _vanish: Vanish = Vanish.NONE
var _vanish_time: float = 0.0
var _vanish_at: Vector3 = Vector3.ZERO  # where the kid was tagged (it shrinks there)
var _last_position: Vector3 = Vector3.INF
var _was_tagged: bool = false
## A touch (the taya or a guard tagging) keeps its run-cast clip to the end even though the kid stops at once.
var _cast_keep: bool = false
## The dive (Patintero): seconds left, its length, and the dive's progress (0 to 1) now.
var _dive_left: float = 0.0
var _dive_total: float = 0.0
var _cast_serial: int = 0
var _cast_played: int = 0
## How far each of these has popped in (0 gone, 1 whole) and which way it is going (up: popping in).
var _kid_p: float = 1.0
var _kid_up: bool = true
var _can_p: float = 0.0
var _can_up: bool = true
var _stars_p: float = 0.0
var _stars_up: bool = true
## The tag pop's own scale (1 when the kid is not being tagged).
var _tag_size: float = 1.0
var _move_blend: float = 0.0
var _last_step: int = 0
var _anim: String = "idle"
var _playing: String = ""
var _tracked_position: Vector2 = Vector2.INF
var _since_move: float = 0.0
var _ground_speed: float = 0.0
## The stencil-marking copies of the kid's materials (`set_team_outline`), held here too so they outlive the meshes when the
## match is freed at once (a mesh dropping the last reference while it is being freed leaves the renderer a dead material).
var _marked: Array[Material] = []
## Last pose name, for tests and debugging ("idle", "run", "cast", "stunned", "airborne").
var pose: String = "idle"


## Builds the kid from its character. Its clothes stay its own and it has no ring: the outline (`set_team_outline`) and the bar
## over its head say whose it is.
func setup(def: CharacterDef) -> void:
	character = def
	_set = def.id if ResourceLoader.exists(MODEL_PATH % def.id) else FALLBACK_SET
	_build()
	_play("idle")


## Sets the way the kid looks, along `facing` (world x/z): it is at once the first time (a new kid just stands that way) and turns
## there at `TURN_RATE` after that (`animate()` does the turning every frame).
func face(facing: Vector2) -> void:
	if facing.length_squared() > 0.0001:
		_facing = facing.normalized()
		_yaw_target = atan2(-facing.x, -facing.y)
		if not _yaw_known:
			_yaw_known = true
			rotation.y = _yaw_target


## One frame of turning toward the facing. Exactly opposite is always turned the same way (positive), so a flip never hesitates.
func _turn(delta: float) -> void:
	var gap: float = angle_difference(rotation.y, _yaw_target)
	if absf(gap) > PI - 0.0001:
		gap = PI
	rotation.y = wrapf(rotation.y + clampf(gap, -TURN_RATE * delta, TURN_RATE * delta), -PI, PI)


## A weapon was cast (the sim's weapon_cast, this tick): standing it is Cast_OneHand, moving Cast_Run, either way from the
## hand's release to the end of the clip, at native speed. A delayed strike (the big snip) passes its delay as `lead`: the
## clip starts that much before the release (never before its start), so the hand comes down on the strike, not the cast.
func play_cast(lead: float = 0.0) -> void:
	_cast_clip = "cast_run" if _move_blend > 0.5 else "cast"
	_cast_lead = maxf(lead, 0.0)
	_cast_total = maxf(_clip_length(_cast_clip) - _cast_start(_cast_clip), 0.01)
	_cast_left = _cast_total
	_cast_serial += 1


## The kid touched somebody (a tag): the run-cast clip plays out to its end, standing still or not, as if it reached out and touched.
func play_touch() -> void:
	play_cast()
	_cast_clip = "cast_run"
	_cast_total = maxf(_clip_length(_cast_clip) - _cast_start(_cast_clip), 0.01)
	_cast_left = _cast_total
	_cast_keep = true


## The kid dives along its facing for `time` s (Patintero): it crouches, throws itself forward to a flat dive with the arms out, slides and
## gets back up with a little squash. View only: the sim moves the kid.
func play_dive(time: float) -> void:
	_dive_total = maxf(time, 0.01)
	_dive_left = _dive_total


## Seconds left of the dive (0 when it is not diving).
func dive_left() -> float:
	return _dive_left


## How far into the dive the kid is, 0 to 1 (0 when it is not diving).
func dive_progress() -> float:
	return 1.0 - _dive_left / _dive_total if _dive_left > 0.0 else 0.0


## The dive's pose at `progress` (0 to 1): how far the body leans forward (radians, negative is forward), how high it is lifted (m) and its
## squash (the height's share). A crouch (0 to 0.15), the leap to a flat dive (to 0.45), the slide (to 0.8) and the stand-up.
static func dive_pose(progress: float) -> Dictionary:
	var lean: float = 0.0
	var lift: float = 0.0
	var squash: float = 1.0
	if progress < 0.15:
		squash = lerpf(1.0, 0.82, progress / 0.15)
	elif progress < 0.45:
		var t: float = (progress - 0.15) / 0.3
		lean = -DIVE_LEAN * sin(t * PI / 2.0)
		lift = DIVE_LIFT * sin(t * PI)
		squash = lerpf(0.82, 1.0, t)
	elif progress < 0.8:
		lean = -DIVE_LEAN
		lift = DIVE_SKIM
	else:
		var t: float = (progress - 0.8) / 0.2
		lean = -DIVE_LEAN * (1.0 - t * t)
		lift = DIVE_SKIM * (1.0 - t)
		squash = 1.0 - 0.12 * sin(t * PI)
	return {"lean": lean, "lift": lift, "squash": squash}


## Seconds into a cast clip ("cast" or "cast_run") where the hand releases: the cast starts there, so the release lands on
## the sim's cast tick. Measured on the GLB (CharacterDef.cast_release_standing / _moving).
func cast_release(clip: String) -> float:
	return character.cast_release_moving if clip == "cast_run" else character.cast_release_standing


## Draws a `color` outline round the kid: its material (a copy of the authored one, every setting kept) also marks the kid's
## pixels in the stencil, and a shell over it draws only outside them.
func set_team_outline(color: Color) -> void:
	var outline: ShaderMaterial = ShaderMaterial.new()
	outline.shader = OUTLINE_SHADER
	outline.set_shader_parameter("color", color)
	outline.set_shader_parameter("width", OUTLINE_WIDTH)
	for node: Node in _model.find_children("*", "MeshInstance3D", true, false):
		var mesh: MeshInstance3D = node as MeshInstance3D
		for surface: int in mesh.mesh.get_surface_count():
			var marked: BaseMaterial3D = mesh.get_active_material(surface).duplicate() as BaseMaterial3D
			marked.stencil_mode = BaseMaterial3D.STENCIL_MODE_CUSTOM
			marked.stencil_flags = BaseMaterial3D.STENCIL_FLAG_WRITE
			marked.stencil_compare = BaseMaterial3D.STENCIL_COMPARE_ALWAYS
			marked.stencil_reference = STENCIL
			_marked.append(marked)
			mesh.set_surface_override_material(surface, marked)
		mesh.material_overlay = outline


func is_can() -> bool:
	return _can.visible


func is_ghost() -> bool:
	return _ghost


func set_ghost(on: bool) -> void:
	_ghost = on


## Which animation is showing ("idle", "run", "tired", "cast", "cast_run", "stun", "airborne").
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
	_turn(delta)
	_cast_left = maxf(0.0, _cast_left - delta)
	if _cast_left <= 0.0:
		_cast_keep = false
	_dive_left = maxf(0.0, _dive_left - delta)
	var polymorphed: bool = state.effects.has(StatusEffects.Type.POLYMORPH)
	set_ghost(state.tagged)
	_step_morph(delta, polymorphed)
	if polymorphed:
		pose = "can"
		_pop_stars(delta, false)
		_can.rotation.z = sin(_clock * 10.0) * 0.1 * clampf(speed, 0.0, 1.0)
		_can.position.y = absf(sin(_clock * 10.0)) * 0.08 * clampf(speed, 0.0, 1.0)
		_apply_pops()
		return
	var moving: bool = speed > 0.3
	_still_for = 0.0 if moving else (_still_for + delta if speed > 0.0 else RUN_GRACE + delta)
	var running: bool = moving or (_move_blend > 0.5 and _still_for <= RUN_GRACE)
	_move_blend = move_toward(_move_blend, 1.0 if running else 0.0, delta * 6.0)
	_tired = state.overclocked
	_step_vanish(delta, state.tagged)
	# a cast belongs to the clip it began with: when the kid starts or stops moving it is over
	if _cast_left > 0.0 and not _cast_keep and (_move_blend > 0.5) != (_cast_clip == "cast_run"):
		_cast_left = 0.0
	if speed > 0.3:
		# the feet step with the run clip's own cycle, two steps a cycle
		_phase += delta / maxf(_clip_length("tired" if _tired else "run"), 0.01)
		var step_index: int = int(floor(_phase * 2.0))
		if step_index != _last_step:
			_last_step = step_index
			footstep.emit()
	var anim: String = "idle"
	if state.tagged:
		# waiting in the box: just standing there
		pose = "idle"
	elif state.effects.has(StatusEffects.Type.AIRBORNE) or state.effects.has(StatusEffects.Type.BOUNCE):
		pose = "airborne"  # the clip leaves the ground by itself
		anim = "airborne"
	elif state.effects.has(StatusEffects.Type.STUN) or state.effects.has(StatusEffects.Type.KNOCKOUT):
		pose = "stunned"  # no fall: a knock-out shows as stunned too
		anim = "stun"
	elif _dive_left > 0.0:
		pose = "dive"
		anim = "airborne"  # the arms out; the body's lean and lift are drawn in `_apply_dive`
	elif _cast_left > 0.0:
		pose = "cast"
		anim = _cast_clip
	elif _move_blend > 0.5:
		pose = "run"
		anim = "tired" if _tired else "run"
	else:
		pose = "idle"
	_anim = anim
	_drive(anim)
	_pop_stars(delta, anim == "stun" or pose == "airborne")
	_stars.rotation.y = _clock * 2.6
	_apply_pops()
	_apply_dive()


## The body leans, lifts and squashes along the dive (and is upright and level again when it is over).
func _apply_dive() -> void:
	if _dive_left <= 0.0 and _body.rotation.x == 0.0 and _body.position.y == 0.0:
		return
	var pose_now: Dictionary = dive_pose(dive_progress())
	_body.rotation.x = pose_now["lean"] as float
	_body.position.y = pose_now["lift"] as float
	_body.scale.y *= pose_now["squash"] as float


## A tagged kid pops out where it stood, then pops in at full size in the tagged box, where the sim has already put it. View only: the
## sim never sees the scale. A kid that is rescued (or created) while tagged just stands there at full size.
func _step_vanish(delta: float, tagged: bool) -> void:
	if tagged and not _was_tagged and _last_position != Vector3.INF:
		_vanish = Vanish.SHRINK
		_vanish_time = 0.0
		_vanish_at = _last_position
		_cast_left = 0.0
	elif not tagged:
		_vanish = Vanish.NONE
	_was_tagged = tagged
	_tag_size = 1.0
	if _vanish == Vanish.SHRINK:
		_vanish_time += delta / POP_OUT_TIME
		position = _vanish_at
		_tag_size = pop_scale(1.0 - _vanish_time, false)
		if _vanish_time >= 1.0:
			_vanish = Vanish.GROW
			_vanish_time = 0.0
			_tag_size = 0.0
	elif _vanish == Vanish.GROW:
		_vanish_time += delta / POP_IN_TIME
		_tag_size = pop_scale(_vanish_time, true)
		if _vanish_time >= 1.0:
			_vanish = Vanish.NONE
			_tag_size = 1.0
	_last_position = position


## The morph swap: the kid pops out, and only then the can pops in; when the morph ends the can pops out first, then the kid pops in.
func _step_morph(delta: float, polymorphed: bool) -> void:
	var kid_target: float = 1.0 if not polymorphed and _can_p <= 0.0 else 0.0
	var can_target: float = 1.0 if polymorphed and _kid_p <= 0.0 else 0.0
	_kid_up = kid_target > _kid_p
	_kid_p = _move_pop(_kid_p, kid_target, delta)
	_can_up = can_target > _can_p
	_can_p = _move_pop(_can_p, can_target, delta)


## The stun stars pop in while the kid is stunned or airborne, and out when it stops.
func _pop_stars(delta: float, show: bool) -> void:
	var target: float = 1.0 if show else 0.0
	_stars_up = target > _stars_p
	_stars_p = _move_pop(_stars_p, target, delta)


## Puts the pops on the nodes: the kid's body (the tag pop times the morph pop), the can and the stars. Nothing is drawn at zero.
func _apply_pops() -> void:
	var kid: float = _tag_size * pop_scale(_kid_p, _kid_up)
	_body.scale = Vector3.ONE * maxf(kid, GONE_SCALE)
	_model.visible = _kid_p > 0.0
	var can: float = pop_scale(_can_p, _can_up)
	_can.scale = Vector3.ONE * maxf(can, GONE_SCALE)
	_can.visible = _can_p > 0.0
	var stars: float = pop_scale(_stars_p, _stars_up)
	_stars.scale = Vector3.ONE * maxf(stars, GONE_SCALE)
	_stars.visible = _stars_p > 0.0


## `p` moved toward `target` (0 or 1): up takes `POP_IN_TIME`, down `POP_OUT_TIME`.
static func _move_pop(p: float, target: float, delta: float) -> float:
	if target > p:
		return minf(p + delta / POP_IN_TIME, target)
	return maxf(p - delta / POP_OUT_TIME, target)


## The scale of something `p` (0 to 1) of the way into popping in (`up`: overshoots a little and settles) or out (a small wind-up, then it
## shrinks away).
static func pop_scale(p: float, up: bool) -> float:
	var x: float = clampf(p, 0.0, 1.0)
	var c3: float = POP_BACK + 1.0
	if up:
		return 1.0 + c3 * pow(x - 1.0, 3.0) + POP_BACK * pow(x - 1.0, 2.0)
	var t: float = 1.0 - x
	return 1.0 - (c3 * t * t * t - POP_BACK * t * t)


## True while the tagged shrink or grow is playing.
func is_vanishing() -> bool:
	return _vanish != Vanish.NONE


## Starts the clip for `anim` (blended from whatever was playing) when it changes, or again for a new cast.
func _drive(anim: String) -> void:
	var is_cast: bool = anim == "cast" or anim == "cast_run"
	if anim != _playing or (is_cast and _cast_played != _cast_serial):
		_cast_played = _cast_serial
		_play(anim)


func _play(clip: String) -> void:
	var is_cast: bool = clip == "cast" or clip == "cast_run"
	_playing = clip
	# a cast is a cut: the hand must be at its release on the very tick the effect appears
	_player.play(clip, 0.0 if is_cast else BLEND)
	if is_cast:
		cast_seek = _cast_start(clip)
		_player.seek(cast_seek, true)


## Where a cast clip starts: the hand's release, less the strike's lead.
func _cast_start(clip: String) -> float:
	return maxf(cast_release(clip) - _cast_lead, 0.0)


func _clip_length(clip: String) -> float:
	return _player.get_animation(clip).length


## A kid's standing portrait for HUD cards (falls back to FALLBACK_SET).
static func portrait(character_id: StringName) -> Texture2D:
	var set_id: String = String(character_id)
	if not ResourceLoader.exists(PORTRAIT_PATH % set_id):
		set_id = String(FALLBACK_SET)
	if not _portraits.has(set_id):
		_portraits[set_id] = load(PORTRAIT_PATH % set_id) as Texture2D
	return _portraits[set_id]


## A character's 3:4 profile card picture: the face cropped out of the boot screen key art at its own
## zoom (the owner's own 3:4 pictures, assets/portraits/cards/<id>.webp), or null when there is none.
static func card_face(character_id: StringName) -> Texture2D:
	var path: String = CARD_PATH % String(character_id)
	if not ResourceLoader.exists(path):
		return null
	if not _faces.has(path):
		_faces[path] = load(path) as Texture2D
	return _faces[path]


## The square face of a character from the boot screen art, or null when there is none.
static func minimap_face(character_id: StringName) -> Texture2D:
	var path: String = FACE_PATH % String(character_id)
	if not ResourceLoader.exists(path):
		return null
	if not _faces.has(path):
		_faces[path] = load(path) as Texture2D
	return _faces[path]


static func _scene(set_id: StringName) -> PackedScene:
	var path: String = MODEL_PATH % set_id
	if not _scenes.has(path):
		_scenes[path] = load(path) as PackedScene
	return _scenes[path]


func _build() -> void:
	_body = Node3D.new()
	_body.name = "Body"
	add_child(_body)
	_model = _scene(_set).instantiate() as Node3D
	_model.name = "Model"
	_model.rotation.y = PI  # glTF models face +Z, the kid faces -Z
	_body.add_child(_model)
	_model.scale = Vector3.ONE * (KID_HEIGHT / model_height(_model))
	# the material stays exactly as authored: no ModelLook, no override
	_player = _model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var library: AnimationLibrary = _player.get_animation_library("")
	for game_name: String in CLIP_FOR:
		var animation: Animation = library.get_animation(CLIP_FOR[game_name])
		if not library.has_animation(game_name):  # the library is shared by every instance of the kid
			library.add_animation(game_name, animation)
		animation.loop_mode = Animation.LOOP_LINEAR if game_name in LOOPING else Animation.LOOP_NONE
	_stars = _build_stars()
	add_child(_stars)
	# the owner's can model (the one Lata throws), not a code-drawn stand-in
	_can = WeaponModel.standing(POLYMORPH_CAN_MODEL, POLYMORPH_CAN_HEIGHT)
	_can.visible = false
	add_child(_can)


## How tall a kid's normalized model stands in its rest pose, in the GLB's own units (the game scales it to KID_HEIGHT).
static func model_height(model: Node3D) -> float:
	var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	return mesh.get_aabb().size.y


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
