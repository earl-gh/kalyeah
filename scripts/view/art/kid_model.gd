class_name KidModel
extends Node3D
## One of the six kids (docs/PROJECT.md 3.2) as a real 3D model: a rigged GLB with its own
## clips (tools/blender/build_kid_game_model.py), lit with the shared matte look (ModelLook).
## Visual only: the hitbox and every number come from the sim. The run cycle is paced by the
## ground speed so the feet do not skate, a cast plays at the weapon's cast time (a throw, or
## a one-hand spell for heals, shields and traps), a cast while running only moves the upper
## body so the legs keep running, and a knock-out falls over backwards. Origin at the feet,
## the kid faces -Z (like a camera). Kids with no model of their own yet use FALLBACK_SET.
## The owner's authored kids (CharacterDef.authored_model: Toni, Popoy, Inday) are different: their GLB keeps its own
## material (no ModelLook) and plays its own clips (AUTHORED_CLIP_FOR) at their own speed, with no layering and no
## fall; every cast is Cast_OneHand (standing) or Cast_Run (moving), started at the hand's release moment so it
## lines up with the sim's cast tick; a tagged kid shrinks away, then grows back in the tagged box.

const MODEL_PATH: String = "res://assets/models/characters/%s.glb"
const PORTRAIT_PATH: String = "res://assets/portraits/%s.png"
## A kid's face cropped from the boot screen key art (tools/art/crop_minimap_faces.py), for the minimap.
const FACE_PATH: String = "res://assets/portraits/faces/%s.png"
const CARD_PATH: String = "res://assets/portraits/cards/%s.webp"
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
## Game state -> the clip that shows it in the rigged kids' GLBs. The game names are added
## as aliases, so a model that already has a clip under the game name keeps it.
const CLIP_FOR: Dictionary[String, String] = {
	"idle": "idle_alert", "cast": "throw", "spell": "cast_one_hand", "stun": "stunned",
	"airborne": "airborne_fall", "ko": "knockdown",
}
## Upper-body clips played over the run while casting on the move (legs keep running).
const LAYER_FOR: Dictionary[String, String] = {"cast": "throw_run", "spell": "cast_run"}
## Bones a layered upper-body clip may move: the rest belongs to the run underneath.
const UPPER_BONES: Array[String] = ["spine", "chest", "neck", "head", "shoulder", "upper_arm", "forearm",
	"hand", "palm", "thumb", "index", "middle", "ring", "pinky", "spring_hair", "spring_pigtail"]
## Blend shapes a layered clip leaves to the run (knee and hip correctives).
const LOWER_SHAPES: Array[String] = ["knee_flex", "hip_flex"]
const LOOPING: Array[String] = ["idle", "run", "stun", "airborne"]
## The authored kids' clips (their own names in the GLB) under the game's state names.
const AUTHORED_CLIP_FOR: Dictionary[String, String] = {
	"idle": "Idle", "run": "Run", "tired": "Exhausted_Run", "stun": "Stunned", "airborne": "Airborne",
	"cast": "Cast_OneHand", "cast_run": "Cast_Run",
}
const AUTHORED_LOOPING: Array[String] = ["idle", "run", "tired", "stun", "airborne"]
## A tagged authored kid shrinks to nothing where it was tagged, then grows back in the tagged box (seconds each).
const SHRINK_TIME: float = 0.17
const GROW_TIME: float = 0.17
## The scale at the bottom of the shrink (a true zero breaks the skinned mesh's transform).
const GONE_SCALE: float = 0.001
## A spell starts where the charge-up of cast_one_hand ends (seconds into the clip).
const SPELL_FROM: float = 0.6
## Blend out of a knockout (getting back up) is slower than other changes.
const GET_UP_BLEND: float = 0.3
const STAR_COUNT: int = 5

static var _scenes: Dictionary[String, PackedScene] = {}
static var _layers: Dictionary[String, AnimationLibrary] = {}
static var _portraits: Dictionary[String, Texture2D] = {}
static var _faces: Dictionary[String, Texture2D] = {}
static var _shadow_texture: GradientTexture2D

var character: CharacterDef
## A foot hit the ground (run cycle).
signal footstep

var _set: StringName = FALLBACK_SET
var _body: Node3D  # leans and falls over (pivot at the feet)
var _model: Node3D
var _player: AnimationPlayer
var _upper: AnimationPlayer  # the upper-body layer over the run
var _own_fall: bool = false  # the model has its own knock-down clip (no whole-body tip)
var _spell: bool = false
var _layer_played: int = 0
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
## The model plays its own clips at their own speed (CharacterDef.native_animation).
var _native: bool = false
## The owner's authored model (CharacterDef.authored_model): own material, own clips, shrink when tagged.
var _authored: bool = false
var _cast_clip: String = "cast"  # authored: Cast_OneHand (standing) or Cast_Run (moving), chosen when the cast starts
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


## Builds the kid from its character. The kid wears no team colour and has no ring: the bar over its head says whose it is.
func setup(def: CharacterDef) -> void:
	character = def
	_authored = def.authored_model
	_native = def.native_animation or _authored
	_set = def.id if ResourceLoader.exists(MODEL_PATH % def.id) else FALLBACK_SET
	_build()
	_play("idle", 1.0)


## Turns the kid to look along `facing` (world x/z).
func face(facing: Vector2) -> void:
	if facing.length_squared() > 0.0001:
		_facing = facing.normalized()
		rotation.y = atan2(-facing.x, -facing.y)


## Throwing arm swing, or a one-hand spell when `spell` (a weapon was cast).
func play_cast(duration: float = CAST_TIME, spell: bool = false) -> void:
	_spell = spell
	_cast_total = maxf(duration, CAST_TIME)
	if _authored:
		# standing: Cast_OneHand, moving: Cast_Run, either way from the hand's release to the end of the clip
		_cast_clip = "cast_run" if _move_blend > 0.5 else "cast"
		_cast_total = maxf(_clip_length(_cast_clip) - cast_release(_cast_clip), 0.01)
	elif _native:
		# the clip decides how long the cast shows: its own length, never squeezed
		var own: String = "spell" if spell and _player.has_animation("spell") else "cast"
		_cast_total = maxf(_clip_length(own), 0.01)
	_cast_left = _cast_total
	_cast_serial += 1


## Seconds into an authored kid's cast clip ("cast" or "cast_run") where the hand releases: the cast starts there, so the
## release lands on the sim's cast tick. Measured on the GLB (CharacterDef.cast_release_standing / _moving).
func cast_release(clip: String) -> float:
	return character.cast_release_moving if clip == "cast_run" else character.cast_release_standing


## Heals, shields and traps are cast with an open hand; everything else is thrown.
static func is_spell(weapon: WeaponDef) -> bool:
	return weapon.kind == WeaponDef.Kind.HEAL or weapon.kind == WeaponDef.Kind.BLOCK \
		or weapon.shape == WeaponDef.Shape.TRAP


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
	_tired = _authored and state.overclocked
	if _authored:
		_step_vanish(delta, state.tagged)
		# a cast belongs to the clip it began with: when the kid starts or stops moving it is over
		if _cast_left > 0.0 and (_move_blend > 0.5) != (_cast_clip == "cast_run"):
			_cast_left = 0.0
	if speed > 0.3:
		# a native run steps with its own cycle, the others one cycle per STRIDE metres
		_phase += delta / maxf(_clip_length("tired" if _tired else "run"), 0.01) if _native else delta * speed / STRIDE
		var step_index: int = int(floor(_phase * 2.0))
		if step_index != _last_step:
			_last_step = step_index
			footstep.emit()
	var knocked_out: bool = state.effects.has(StatusEffects.Type.KNOCKOUT) and not _authored  # authored kids have no fall
	_ko_time = _ko_time + delta if knocked_out else 0.0
	var airborne: bool = state.effects.has(StatusEffects.Type.AIRBORNE) or state.effects.has(StatusEffects.Type.BOUNCE)
	var casting: bool = _cast_left > 0.0
	var cast_clip: String = "spell" if _spell and _player.has_animation("spell") else "cast"
	var anim: String = "idle"
	var layer: String = ""
	var play_speed: float = 1.0
	if state.tagged:
		# waiting in the box: just standing there
		pose = "idle"
	elif knocked_out:
		pose = "ko"
		anim = "ko_stagger" if _ko_time < KO_FALL_TIME else "ko_lying"
	elif airborne:
		pose = "airborne"
		anim = "airborne" if _player.has_animation("airborne") else "stun"
		play_speed = 1.0 if anim == "airborne" else 1.6
	elif state.effects.has(StatusEffects.Type.STUN) or (_authored and state.effects.has(StatusEffects.Type.KNOCKOUT)):
		pose = "stunned"
		anim = "stun"
	elif casting and _authored:
		pose = "cast"
		anim = _cast_clip
	elif casting and _move_blend > 0.5 and _upper != null:
		# casting on the move: the legs keep running, the upper body throws or casts
		pose = "cast"
		anim = "run"
		layer = cast_clip
		play_speed = _run_speed(speed)
	elif casting:
		pose = "cast"
		anim = cast_clip
		play_speed = 1.0 if _native else (_clip_length(anim) - _clip_start(anim)) / _cast_total
	elif _move_blend > 0.5:
		pose = "run"
		anim = "tired" if _tired else "run"
		play_speed = _run_speed(speed)
	else:
		pose = "idle"
	_anim = anim
	_drive(anim, play_speed)
	_drive_layer(layer)
	_pose_body(delta, knocked_out)
	_stars.visible = anim == "stun" or pose == "airborne"
	_stars.rotation.y = _clock * 2.6
	_set_tint(_tint())


## An authored kid that is tagged shrinks away (super fast, smooth) where it stood, then grows back to full size in
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


## One run cycle per STRIDE metres: the feet keep up with the ground.
func _run_speed(speed: float) -> float:
	if _native:
		return 1.0
	return clampf(speed / STRIDE * _clip_length("run"), 0.5, 2.2)


## Starts the clip for `anim` (blended from whatever was playing) and sets its speed.
func _drive(anim: String, play_speed: float) -> void:
	var clip: String = "ko" if anim.begins_with("ko") else anim
	var is_cast: bool = clip == "cast" or clip == "spell" or clip == "cast_run"
	if clip != _playing or (is_cast and _cast_played != _cast_serial):
		_cast_played = _cast_serial
		_play(clip, play_speed)
	_player.speed_scale = play_speed


func _play(clip: String, play_speed: float) -> void:
	var blend: float = GET_UP_BLEND if _playing == "ko" else BLEND
	if _authored and clip in ["cast", "cast_run"]:
		blend = 0.0  # a cut: the hand must be at its release on the very tick the effect appears
	_playing = clip
	_player.play(clip, blend, play_speed)
	if clip in ["cast", "spell", "ko", "cast_run"]:
		_player.seek(_clip_start(clip), true)


## Plays (or stops) the upper-body clip over the run; one start per cast.
func _drive_layer(layer: String) -> void:
	if _upper == null:
		return
	if layer == "":
		if _upper.is_playing():
			_upper.stop()
		return
	if _layer_played != _cast_serial:
		_layer_played = _cast_serial
		_upper.play(layer)
		_upper.seek(0.0, true)
	_upper.speed_scale = 1.0 if _native else _upper.get_animation(layer).length / _cast_total


## Where a clip starts when played as a one-off (a spell skips the long charge-up).
func _clip_start(clip: String) -> float:
	if _authored:
		return cast_release(clip) if clip == "cast" or clip == "cast_run" else 0.0
	if _native:
		return 0.0
	return SPELL_FROM if clip == "spell" and _clip_length(clip) > SPELL_FROM * 2.0 else 0.0


func _clip_length(clip: String) -> float:
	return _player.get_animation(clip).length


## The whole body tips over backwards in a knockout (and gets back up), and flinches back
## a little when hit.
func _pose_body(delta: float, knocked_out: bool) -> void:
	if knocked_out:
		_lie = move_toward(_lie, 1.0, delta / KO_FALL_TIME)
	else:
		_lie = move_toward(_lie, 0.0, delta * 5.0)
	var fall: float = 0.0 if _own_fall else _lie * _lie * (3.0 - 2.0 * _lie)  # a knock-down clip falls by itself
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
	var mesh: MeshInstance3D = _model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	_model.scale = Vector3.ONE * (KID_HEIGHT / mesh.get_aabb().size.y)
	if not _authored:
		_materials = ModelLook.apply(_model)  # an authored model keeps its material exactly as authored
	_player = _model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var library: AnimationLibrary = _player.get_animation_library("")
	if _authored:
		for game_name: String in AUTHORED_CLIP_FOR:
			var animation: Animation = library.get_animation(AUTHORED_CLIP_FOR[game_name])
			if not library.has_animation(game_name):  # the library is shared by every instance of the kid
				library.add_animation(game_name, animation)
			animation.loop_mode = Animation.LOOP_LINEAR if game_name in AUTHORED_LOOPING else Animation.LOOP_NONE
	else:
		for game_name: String in CLIP_FOR:
			if not library.has_animation(game_name) and library.has_animation(CLIP_FOR[game_name]):
				library.add_animation(game_name, library.get_animation(CLIP_FOR[game_name]))
		_own_fall = library.has_animation(CLIP_FOR["ko"])
		for clip: String in ["idle", "run", "cast", "spell", "stun", "airborne", "ko"]:
			if library.has_animation(clip):
				var animation: Animation = library.get_animation(clip)
				animation.loop_mode = Animation.LOOP_LINEAR if clip in LOOPING else Animation.LOOP_NONE
				if clip in LOOPING:
					_close_loop(animation)
		if library.has_animation(LAYER_FOR["cast"]) and library.has_animation(LAYER_FOR["spell"]):
			_upper = AnimationPlayer.new()
			_upper.name = "UpperPlayer"
			_upper.deterministic = false  # touches only its own (upper body) tracks
			add_child(_upper)  # after Body: it plays after the model's own player each frame
			_upper.root_node = _upper.get_path_to(_player.get_node(_player.root_node))
			_upper.add_animation_library("", _layer_library(String(_set), library))
	_stars = _build_stars()
	add_child(_stars)
	_can = MeshInstance3D.new()
	_can.mesh = Props.can_body()
	_can.scale = Vector3.ONE * 1.3
	_can.visible = false
	add_child(_can)
	LowPoly.add_outline(_can, 0.02)


## The imported loops end on their last pose instead of coming back to the first (Junjun's run
## ends 22 degrees off at the knee, so every cycle snapped back: the legs seemed to vibrate). The
## loop is made one key interval longer so the last pose blends into the first. The clips are
## shared between every instance of a kid, so it is done once per clip.
static func _close_loop(animation: Animation) -> void:
	if animation.has_meta("loop_closed"):
		return
	animation.set_meta("loop_closed", true)
	var keys: int = 0
	for track: int in animation.get_track_count():
		keys = maxi(keys, animation.track_get_key_count(track))
	if keys > 1:
		animation.length += animation.length / float(keys - 1)


## The upper-body clips for one kid, with every leg, hip and root track taken out (and the
## knee / hip correctives) so the run underneath keeps those.
static func _layer_library(set_id: String, library: AnimationLibrary) -> AnimationLibrary:
	if not _layers.has(set_id):
		var layers: AnimationLibrary = AnimationLibrary.new()
		for game_name: String in LAYER_FOR:
			var anim: Animation = library.get_animation(LAYER_FOR[game_name]).duplicate(true) as Animation
			for track: int in range(anim.get_track_count() - 1, -1, -1):
				if not _is_upper_track(anim, track):
					anim.remove_track(track)
			anim.loop_mode = Animation.LOOP_NONE
			layers.add_animation(game_name, anim)
		_layers[set_id] = layers
	return _layers[set_id]


static func _is_upper_track(anim: Animation, track: int) -> bool:
	var path: NodePath = anim.track_get_path(track)
	if path.get_subname_count() == 0:
		return false
	var part: String = String(path.get_subname(0))
	if anim.track_get_type(track) == Animation.TYPE_BLEND_SHAPE:
		return not LOWER_SHAPES.any(func(lower: String) -> bool: return part.begins_with(lower))
	return UPPER_BONES.any(func(upper: String) -> bool: return part.begins_with(upper))


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
