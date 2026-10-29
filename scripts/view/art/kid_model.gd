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
const HIT_TIME: float = 0.28
const SHADOW_SIZE: float = 1.1
const BLEND: float = 0.14
## The sim steps at 30 Hz but the screen draws faster: with no new position for this
## long the kid is standing still.
const STILL_AFTER: float = 0.2
## The game's state names -> the clips of the GLBs (their own names). The state names are added to the model's clip library
## as aliases of the same animations.
const CLIP_FOR: Dictionary[String, String] = {
	"idle": "Idle", "run": "Run", "tired": "Exhausted_Run", "stun": "Stunned", "airborne": "Airborne",
	"cast": "Cast_OneHand", "cast_run": "Cast_Run",
}
const LOOPING: Array[String] = ["idle", "run", "tired", "stun", "airborne"]
## A tagged kid shrinks to nothing where it was tagged, then grows back in the tagged box (seconds each).
const SHRINK_TIME: float = 0.17
const GROW_TIME: float = 0.17
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
static var _shadow_texture: GradientTexture2D

var character: CharacterDef
## A foot hit the ground (run cycle).
signal footstep

var _set: StringName = FALLBACK_SET
var _body: Node3D  # flinches (pivot at the feet) and is what shrinks
var _model: Node3D
var _player: AnimationPlayer
var _shadow: MeshInstance3D
var _stars: Node3D
var _can: Node3D
var _facing: Vector2 = Vector2(0.0, 1.0)
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
var _cast_serial: int = 0
var _cast_played: int = 0
var _hit_left: float = 0.0
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


## Turns the kid to look along `facing` (world x/z).
func face(facing: Vector2) -> void:
	if facing.length_squared() > 0.0001:
		_facing = facing.normalized()
		rotation.y = atan2(-facing.x, -facing.y)


## A weapon was cast (the sim's weapon_cast, this tick): standing it is Cast_OneHand, moving Cast_Run, either way from the
## hand's release to the end of the clip, at native speed. A delayed strike (the big snip) passes its delay as `lead`: the
## clip starts that much before the release (never before its start), so the hand comes down on the strike, not the cast.
func play_cast(lead: float = 0.0) -> void:
	_cast_clip = "cast_run" if _move_blend > 0.5 else "cast"
	_cast_lead = maxf(lead, 0.0)
	_cast_total = maxf(_clip_length(_cast_clip) - _cast_start(_cast_clip), 0.01)
	_cast_left = _cast_total
	_cast_serial += 1


## Seconds into a cast clip ("cast" or "cast_run") where the hand releases: the cast starts there, so the release lands on
## the sim's cast tick. Measured on the GLB (CharacterDef.cast_release_standing / _moving).
func cast_release(clip: String) -> float:
	return character.cast_release_moving if clip == "cast_run" else character.cast_release_standing


## Flinch (took damage).
func play_hit() -> void:
	_hit_left = HIT_TIME


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
	_tired = state.overclocked
	_step_vanish(delta, state.tagged)
	# a cast belongs to the clip it began with: when the kid starts or stops moving it is over
	if _cast_left > 0.0 and (_move_blend > 0.5) != (_cast_clip == "cast_run"):
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
	_flinch()
	_stars.visible = anim == "stun" or pose == "airborne"
	_stars.rotation.y = _clock * 2.6


## A tagged kid shrinks away (super fast, smooth) where it stood, then grows back to full size in
## the tagged box, where the sim has already put it. View only: the sim never sees the scale. A kid that is
## rescued (or created) while tagged just stands there at full size.
func _step_vanish(delta: float, tagged: bool) -> void:
	if tagged and not _was_tagged and _last_position != Vector3.INF:
		_vanish = Vanish.SHRINK
		_vanish_time = 0.0
		_vanish_at = _last_position
		_cast_left = 0.0
	elif not tagged:
		_vanish = Vanish.NONE
	_was_tagged = tagged
	var size: float = 1.0
	if _vanish == Vanish.SHRINK:
		_vanish_time += delta / SHRINK_TIME
		position = _vanish_at
		size = 1.0 - _smooth(_vanish_time)
		if _vanish_time >= 1.0:
			_vanish = Vanish.GROW
			_vanish_time = 0.0
			size = 0.0
	elif _vanish == Vanish.GROW:
		_vanish_time += delta / GROW_TIME
		size = _smooth(_vanish_time)
		if _vanish_time >= 1.0:
			_vanish = Vanish.NONE
			size = 1.0
	_body.scale = Vector3.ONE if size >= 1.0 else Vector3.ONE * maxf(size, GONE_SCALE)
	_shadow.scale = _body.scale
	_last_position = position


## True while the tagged shrink or grow is playing.
func is_vanishing() -> bool:
	return _vanish != Vanish.NONE


static func _smooth(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	return x * x * (3.0 - 2.0 * x)


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


## The body flinches back a little when hit.
func _flinch() -> void:
	var flinch: float = sin((1.0 - _hit_left / HIT_TIME) * PI) if _hit_left > 0.0 else 0.0
	_body.rotation.x = flinch * 0.16
	_body.position = Vector3(0.0, 0.0, flinch * 0.06)


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
