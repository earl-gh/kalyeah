extends GutTest
## All six kids are the owner's authored models: their GLB keeps its own material and clips,
## a cast starts at the hand's release so it lines up with the sim's cast tick, and a tagged kid shrinks away and grows
## back in the tagged box (no fall, no throw clip, no layered upper body). Ligaya and Migo come with their armature's 0.8
## scale left on the armature node and the Hips: they are measured true and stood on the ground (KidModel.model_height,
## _fix_scaled_rig).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const DT: float = 1.0 / 30.0
const STEP: float = 1.0 / 60.0
const AUTHORED: Array[StringName] = [&"junjun", &"ligaya", &"migo", &"toni", &"popoy", &"inday"]
## The two whose rig keeps a 0.8 scale on the Hips (the other four have 1.0).
const SCALED_RIG: Array[StringName] = [&"ligaya", &"migo"]
## Height of the Hips at the first key of Idle as the GLB files store it (read from the files).
## How tall each GLB's rest pose is drawn (m), measured on a rendered silhouette in the sandbox (1/200 of 3 m a pixel).
const RENDERED_REST_HEIGHT: Dictionary[StringName, float] = {&"junjun": 1.705, &"ligaya": 1.090, &"migo": 1.095, &"toni": 1.7, &"popoy": 1.7, &"inday": 1.7}
const IDLE_HIPS_Y_IN_THE_FILE: Dictionary[StringName, float] = {&"ligaya": 0.71805, &"migo": 0.740568}
## Junjun's roughness/metal map (`junjun_0.png`) is 4096 px; every other texture of the six is 2048 px.
const ROUGH_METAL_SIZE: Dictionary[StringName, int] = {&"junjun": 4096, &"ligaya": 2048, &"migo": 2048, &"toni": 2048, &"popoy": 2048, &"inday": 2048}
## The part of each clip that is the cast gesture (seconds): Cast_OneHand's swing from the wind-up to the hand's arrival, and
## Cast_Run's raise (after the run cycle's own arm swing). The release is the time the hand has covered half of its travel.
const WINDOW: Dictionary[String, Vector2] = {"Cast_OneHand": Vector2(0.5, 0.9), "Cast_Run": Vector2(0.85, 1.15)}


func _def(id: StringName) -> CharacterDef:
	for def: CharacterDef in RULES.characters:
		if def.id == id:
			return def
	return null


func _kid(id: StringName) -> KidModel:
	return _kid_from(_def(id))


func _kid_from(def: CharacterDef) -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(def)
	add_child(kid)
	return kid


func _walk(kid: KidModel, speed: float, frames: int = 12) -> void:
	for i: int in frames:
		kid.animate(DT, speed, PlayerState.new())


func test_all_six_kids_have_cast_release_times() -> void:
	assert_eq(RULES.characters.size(), AUTHORED.size())
	for id: StringName in AUTHORED:
		assert_true(_def(id).cast_release_standing > 0.0 and _def(id).cast_release_moving > 0.0, "%s has release times" % id)


func test_their_old_baked_textures_are_gone() -> void:
	for id: StringName in AUTHORED:
		assert_false(FileAccess.file_exists("res://assets/models/characters/%s_kid_base_color.jpg" % id), id)
		for n: int in 3:
			assert_true(ResourceLoader.exists("res://assets/models/characters/%s_%d.png" % [id, n]), "%s keeps its authored texture %d" % [id, n])


func test_the_material_stays_exactly_as_authored() -> void:
	for id: StringName in AUTHORED:
		var kid: KidModel = _kid(id)
		var mesh: MeshInstance3D = kid.find_child("Model", true, false).find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		assert_null(mesh.get_surface_override_material(0), "%s has no override (no ModelLook)" % id)
		assert_null(mesh.material_override, id)
		var material: BaseMaterial3D = mesh.get_active_material(0) as BaseMaterial3D
		assert_eq(material.resource_name, "Original", id)
		assert_eq(material.roughness, 1.0)
		assert_true(material.normal_enabled, "the authored normal map is on")
		var size: float = ROUGH_METAL_SIZE[id]
		assert_eq(material.albedo_texture.get_size(), Vector2(2048, 2048), "%s: full size base colour" % id)
		assert_eq(material.normal_texture.get_size(), Vector2(2048, 2048), "%s: full size normal map" % id)
		assert_eq(material.roughness_texture.get_size(), Vector2(size, size), "%s: full size roughness/metal map, not shrunk" % id)
		kid.animate(DT, 0.0, PlayerState.new())
		assert_eq(material.albedo_color, Color.WHITE, "no tint on the authored colours")


# ---- height and ground: the skinned model, measured on the CPU ----------------------------------------------------------

## The skinned mesh's bounds in the model's own units (the armature node's scale included), from the skeleton's current pose
## (every `stride`th vertex).
func _skinned_bounds(model: Node3D, stride: int = 3) -> AABB:
	var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var skeleton: Skeleton3D = model.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var arrays: Array = mesh.mesh.surface_get_arrays(0)
	var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var bones: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
	var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
	var per: int = bones.size() / verts.size()
	var matrices: Array[Transform3D] = []
	for i: int in mesh.skin.get_bind_count():
		matrices.append(skeleton.get_bone_global_pose(skeleton.find_bone(mesh.skin.get_bind_name(i))) * mesh.skin.get_bind_pose(i))
	var low: Vector3 = Vector3.INF
	var high: Vector3 = -Vector3.INF
	for v: int in range(0, verts.size(), stride):
		var skinned: Vector3 = Vector3.ZERO
		for k: int in per:
			var weight: float = weights[v * per + k]
			if weight > 0.0:
				skinned += (matrices[bones[v * per + k]] * verts[v]) * weight
		low = low.min(skinned)
		high = high.max(skinned)
	var armature: float = KidModel.armature_scale(skeleton, model)
	return AABB(low * armature, (high - low) * armature)


func test_model_height_is_what_the_skinned_model_really_stands() -> void:
	for id: StringName in AUTHORED:
		var root: Node3D = autofree((load("res://assets/models/characters/%s.glb" % id) as PackedScene).instantiate()) as Node3D
		add_child(root)
		var bounds: AABB = _skinned_bounds(root)
		assert_almost_eq(bounds.size.y, KidModel.model_height(root), 0.02, "%s: the rest pose stands as high as model_height says" % id)
		# what a rendered silhouette of the rest pose measured (see RENDERED_REST_HEIGHT)
		assert_almost_eq(KidModel.model_height(root), RENDERED_REST_HEIGHT[id], 0.02, "%s: and as high as it is drawn" % id)
		assert_almost_eq(bounds.position.y, 0.0, 0.02, "%s: the soles are at the origin" % id)
		var hips: float = (root.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D).get_bone_rest(0).basis.get_scale().x
		if id in SCALED_RIG:
			assert_almost_eq(hips, 0.8, 0.01, "%s keeps its armature's 0.8 on the Hips" % id)
			assert_lt(KidModel.model_height(root), 1.2, "%s: its vertices are 2.1 tall but it stands 1.09" % id)
			assert_almost_eq(KidModel.armature_scale(root.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D, root), 0.8, 0.01, "and its armature node carries 0.8 too")
		else:
			assert_almost_eq(hips, 1.0, 0.001, id)
			assert_almost_eq(KidModel.model_height(root), 1.7, 0.01, id)


func test_every_kid_is_drawn_at_the_one_kid_height() -> void:
	for id: StringName in AUTHORED:
		var kid: KidModel = _kid(id)
		var model: Node3D = kid.find_child("Model", true, false) as Node3D
		assert_almost_eq(_skinned_bounds(model).size.y * model.scale.y, KidModel.KID_HEIGHT, 0.04, "%s stands %.2f m tall" % [id, KidModel.KID_HEIGHT])


func test_the_kids_stand_on_the_ground_in_every_clip_not_above_it() -> void:
	for id: StringName in AUTHORED:
		var kid: KidModel = _kid(id)
		var model: Node3D = kid.find_child("Model", true, false) as Node3D
		var player: AnimationPlayer = kid._player
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		for clip: String in ["Idle", "Run", "Stunned"]:
			# the lowest sole over the clip (a run has a foot in the air at any one moment)
			var sole: float = INF
			player.play(clip)
			for at: float in [0.1, 0.35, 0.6, 0.85]:
				player.seek(player.get_animation(clip).length * at, true)
				player.advance(0.0)
				sole = minf(sole, _skinned_bounds(model, 6).position.y * model.scale.y)
			assert_between(sole, -0.15, 0.05, "%s in %s: the lowest foot is at the ground (%.3f m)" % [id, clip, sole])


func test_a_scaled_rig_gets_its_hips_moves_scaled_and_no_finger_tracks_only_for_ligaya_and_migo() -> void:
	for id: StringName in AUTHORED:
		var animation: Animation = _kid(id)._player.get_animation("Idle")
		var fingers: int = 0
		var hips_y: float = 0.0
		for track: int in animation.get_track_count():
			var bone: String = animation.track_get_path(track).get_concatenated_subnames()
			if KidModel.FINGER_BONES.any(func(finger: String) -> bool: return bone.contains(finger)):
				fingers += 1
			if bone == KidModel.HIPS_BONE and animation.track_get_type(track) == Animation.TYPE_POSITION_3D:
				hips_y = (animation.track_get_key_value(track, 0) as Vector3).y
		if id in SCALED_RIG:
			assert_eq(fingers, 0, "%s: its finger tracks (they fold the hands into sheets) are left out" % id)
		else:
			assert_gt(fingers, 10, "%s keeps its finger animation" % id)
		assert_gt(hips_y, 0.5, "%s has Hips moves" % id)
		if id in SCALED_RIG:
			# the file's first Idle key (Ligaya 0.71805 m, Migo 0.740568 m) times the Hips' 0.7994
			assert_almost_eq(hips_y, IDLE_HIPS_Y_IN_THE_FILE[id] * 0.7994, 0.002, "%s: the Hips move is scaled like the rig" % id)


func test_the_fix_is_applied_once_whatever_the_number_of_kids() -> void:
	var first: KidModel = _kid(&"migo")
	var animation: Animation = first._player.get_animation("Run")
	var hips_before: float = 0.0
	for track: int in animation.get_track_count():
		if animation.track_get_path(track).get_concatenated_subnames() == KidModel.HIPS_BONE and animation.track_get_type(track) == Animation.TYPE_POSITION_3D:
			hips_before = (animation.track_get_key_value(track, 0) as Vector3).y
	var second: KidModel = _kid(&"migo")
	assert_eq(second._player.get_animation("Run"), animation, "the clips are shared")
	for track: int in animation.get_track_count():
		if animation.track_get_path(track).get_concatenated_subnames() == KidModel.HIPS_BONE and animation.track_get_type(track) == Animation.TYPE_POSITION_3D:
			assert_eq((animation.track_get_key_value(track, 0) as Vector3).y, hips_before, "a second kid does not scale the Hips again")


func test_a_character_without_a_model_shows_junjun_without_crashing() -> void:
	var def: CharacterDef = CharacterDef.new()
	def.id = &"nobody_yet"
	var kid: KidModel = _kid_from(def)
	for i: int in 5:
		kid.animate(DT, 4.0, PlayerState.new())
	assert_eq(kid.current_animation(), "run")
	kid.play_cast()
	kid.animate(0.0, 4.0, PlayerState.new())
	assert_eq(kid.pose, "cast")


func test_the_game_states_play_the_models_own_clips() -> void:
	for id: StringName in AUTHORED:
		var kid: KidModel = _kid(id)
		var player: AnimationPlayer = kid._player
		for pair: Array in [["idle", "Idle"], ["run", "Run"], ["tired", "Exhausted_Run"], ["stun", "Stunned"],
				["airborne", "Airborne"], ["cast", "Cast_OneHand"], ["cast_run", "Cast_Run"]]:
			assert_eq(player.get_animation(pair[0] as String), player.get_animation(pair[1] as String), "%s %s" % [id, pair[0]])
		assert_null(kid.find_child("UpperPlayer", true, false), "no layered upper body")
		assert_eq(kid._player.get_animation("run").loop_mode, Animation.LOOP_LINEAR)
		kid.animate(DT, 0.0, PlayerState.new())
		assert_eq(player.current_animation, "idle")
		_walk(kid, 6.0)
		assert_eq(player.current_animation, "run")
		assert_almost_eq(player.speed_scale, 1.0, 0.0001, "native speed whatever the ground speed")
		var tired: PlayerState = PlayerState.new()
		tired.overclocked = true
		for i: int in 3:
			kid.animate(DT, 3.0, tired)
		assert_eq(player.current_animation, "tired", "Exhausted_Run while overclocked")
		var hurt: PlayerState = PlayerState.new()
		hurt.effects.apply(StatusEffects.Type.STUN, 2.0)
		kid.animate(DT, 0.0, hurt)
		assert_eq(player.current_animation, "stun")
		hurt.effects.clear()
		hurt.effects.apply(StatusEffects.Type.AIRBORNE, 2.0)
		kid.animate(DT, 0.0, hurt)
		assert_eq(player.current_animation, "airborne")
		hurt.effects.clear()
		hurt.effects.apply(StatusEffects.Type.KNOCKOUT, 2.0)
		kid.animate(DT, 0.0, hurt)
		assert_eq(player.current_animation, "stun", "no fall: a knock-out shows as stunned")


func test_every_cast_standing_is_cast_onehand_and_moving_is_cast_run() -> void:
	for id: StringName in AUTHORED:
		var standing: KidModel = _kid(id)
		standing.play_cast()
		standing.animate(0.0, 0.0, PlayerState.new())
		assert_eq(standing.pose, "cast")
		assert_eq(standing._player.current_animation, "cast", "%s standing" % id)
		assert_eq(standing._player.get_animation("cast"), standing._player.get_animation("Cast_OneHand"))
		var moving: KidModel = _kid(id)
		_walk(moving, 5.0)
		moving.play_cast()
		moving.animate(0.0, 5.0, PlayerState.new())
		assert_eq(moving._player.current_animation, "cast_run", "%s moving" % id)
		assert_eq(moving._player.get_animation("cast_run"), moving._player.get_animation("Cast_Run"))
		assert_almost_eq(moving._player.speed_scale, 1.0, 0.0001, "native speed")


func test_a_cast_ends_when_the_kid_starts_or_stops_moving() -> void:
	var kid: KidModel = _kid(&"toni")
	kid.play_cast()
	kid.animate(0.0, 0.0, PlayerState.new())
	_walk(kid, 6.0)
	assert_eq(kid._player.current_animation, "run", "running leaves the standing cast")
	kid.play_cast()
	kid.animate(0.0, 6.0, PlayerState.new())
	assert_eq(kid._player.current_animation, "cast_run")
	for i: int in 30:
		kid.animate(DT, 0.0, PlayerState.new())
	assert_eq(kid._player.current_animation, "idle", "stopping leaves the moving cast")


## The release measured on the model itself (not read from the data): the time the right hand has covered half of the
## distance it travels over the gesture, sampled at 1/60 s from the skeleton's bone poses.
func _measured_release(id: StringName, clip: String) -> float:
	var kid: KidModel = _kid(id)  # the kid as the game builds it (the clips of Ligaya and Migo are fixed, see KidModel)
	await wait_process_frames(1)
	var player: AnimationPlayer = kid._player
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var skeleton: Skeleton3D = kid.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var hand: int = skeleton.find_bone("mixamorig_RightHand")
	assert_gt(hand, 0, "the hand bone")
	player.play(clip)
	player.advance(0.0)
	await wait_process_frames(1)
	var window: Vector2 = WINDOW[clip]
	var last: Vector3 = skeleton.get_bone_global_pose(hand).origin
	var times: Array[float] = []
	var steps: Array[float] = []  # metres covered in each step
	var time: float = 0.0
	while time < window.y:
		player.advance(STEP)
		time += STEP
		await wait_process_frames(1)
		var now: Vector3 = skeleton.get_bone_global_pose(hand).origin
		if time - STEP / 2.0 >= window.x:
			times.append(time - STEP / 2.0)
			steps.append(now.distance_to(last))
		last = now
	var total: float = 0.0
	for metres: float in steps:
		total += metres
	assert_gt(total, 0.5, "the hand really travels")
	var covered: float = 0.0
	for i: int in steps.size():
		covered += steps[i]
		if covered >= total / 2.0:
			return times[i]
	return window.y


func test_the_cast_release_times_are_what_the_clips_do() -> void:
	for id: StringName in AUTHORED:
		var def: CharacterDef = _def(id)
		assert_almost_eq(await _measured_release(id, "Cast_OneHand"), def.cast_release_standing, 1.5 * STEP, "%s Cast_OneHand" % id)
		assert_almost_eq(await _measured_release(id, "Cast_Run"), def.cast_release_moving, 1.5 * STEP, "%s Cast_Run" % id)


func test_the_hand_releases_on_the_sim_cast_tick() -> void:
	for id: StringName in AUTHORED:
		var def: CharacterDef = _def(id)
		for moving: bool in [false, true]:
			var kid: KidModel = _kid(id)
			var speed: float = 5.0 if moving else 0.0
			if moving:
				_walk(kid, speed)
			kid.play_cast()  # the sim's weapon_cast signal: this is the cast tick
			kid.animate(0.0, speed, PlayerState.new())
			var release: float = def.cast_release_moving if moving else def.cast_release_standing
			var at: float = kid._player.current_animation_position
			assert_lt(absf(at - release), DT, "%s %s: the clip is at its release on the cast tick (%.3f vs %.3f)" % [id, "moving" if moving else "standing", at, release])
			# and one sim tick later the clip has moved on by one tick, not a different amount
			kid._player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
			kid._player.advance(DT)
			assert_almost_eq(kid._player.current_animation_position, at + DT, 0.001)


func test_a_cast_shows_from_its_release_to_the_end_of_the_clip() -> void:
	var kid: KidModel = _kid(&"inday")
	kid.play_cast()
	assert_almost_eq(kid._cast_total, kid._clip_length("cast") - kid.cast_release("cast"), 0.001)


# ---- tagged: shrink away, grow back in the tagged box -------------------------------------------------------------

func test_a_tagged_kid_shrinks_away_where_it_stood_then_grows_back_in_the_box() -> void:
	for id: StringName in AUTHORED:
		var kid: KidModel = _kid(id)
		var state: PlayerState = PlayerState.new()
		var here: Vector3 = Vector3(2.0, 0.0, 3.0)
		var box: Vector3 = Vector3(-8.0, 0.0, -5.0)
		kid.position = here
		for i: int in 3:
			kid.animate(DT, 0.0, state)
		assert_eq(kid._body.scale, Vector3.ONE)
		# the sim tags the kid: it is already in the box, the view shrinks it where it stood
		state.tagged = true
		kid.position = box
		var smallest: float = 1.0
		var last: float = 1.0
		var shrink_frames: int = 0
		while (shrink_frames == 0 or kid._vanish == KidModel.Vanish.SHRINK) and shrink_frames < 60:
			kid.animate(1.0 / 60.0, 0.0, state)
			assert_eq(kid.position, here, "held where it was tagged while it shrinks")
			kid.position = box  # the presentation puts it in the box every frame
			shrink_frames += 1
			var size: float = kid._body.scale.x
			assert_lte(size, last + 0.0001, "only shrinks")
			last = size
			smallest = minf(smallest, size)
		assert_lt(shrink_frames, 14, "super fast: under a quarter of a second")
		assert_almost_eq(smallest, KidModel.GONE_SCALE, 0.0001, "it disappears")
		assert_eq(kid._shadow.scale.x, kid._body.scale.x, "the shadow goes with it")
		assert_true(kid.is_vanishing())
		assert_eq(kid.pose, "idle", "no fall animation")
		# then it is in the box and grows back
		var grown: float = 0.0
		for i: int in 40:
			kid.position = box
			kid.animate(1.0 / 60.0, 0.0, state)
			assert_eq(kid.position, box, "in the tagged box")
			assert_gte(kid._body.scale.x, grown - 0.0001, "only grows")
			grown = kid._body.scale.x
		assert_eq(kid._body.scale, Vector3.ONE, "exactly full size again")
		assert_eq(kid._shadow.scale, Vector3.ONE)
		assert_false(kid.is_vanishing())
		assert_eq(kid._player.current_animation, "idle", "standing there with Idle")


func test_the_shrink_holds_the_old_spot_until_it_is_gone() -> void:
	var kid: KidModel = _kid(&"toni")
	var state: PlayerState = PlayerState.new()
	kid.position = Vector3(2.0, 0.0, 3.0)
	kid.animate(DT, 0.0, state)
	state.tagged = true
	kid.position = Vector3(-8.0, 0.0, -5.0)  # what the presentation does the moment the sim tags it
	kid.animate(1.0 / 60.0, 0.0, state)
	assert_eq(kid.position, Vector3(2.0, 0.0, 3.0), "still shrinking where it was tagged")
	assert_lt(kid._body.scale.x, 1.0)


func test_a_rescue_leaves_no_leftover_scale() -> void:
	var kid: KidModel = _kid(&"popoy")
	var state: PlayerState = PlayerState.new()
	kid.animate(DT, 0.0, state)
	state.tagged = true
	kid.animate(1.0 / 60.0, 0.0, state)
	kid.animate(1.0 / 60.0, 0.0, state)
	assert_lt(kid._body.scale.x, 1.0, "mid shrink")
	state.tagged = false  # rescued at once
	kid.animate(1.0 / 60.0, 0.0, state)
	assert_eq(kid._body.scale, Vector3.ONE)
	assert_eq(kid._shadow.scale, Vector3.ONE)
	assert_false(kid.is_vanishing())
	# a full tag, wait in the box, then the rescue
	state.tagged = true
	for i: int in 60:
		kid.animate(1.0 / 60.0, 0.0, state)
	state.tagged = false
	kid.animate(1.0 / 60.0, 0.0, state)
	assert_eq(kid._body.scale, Vector3.ONE)


func test_in_a_match_a_tagged_authored_kid_reappears_at_the_tagged_place() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	var id: int = PracticeMatch.LOCAL_ID
	practice.sim.players[id].character_id = &"ligaya"
	practice._presentation.refresh_kids()
	var view: KidModel = practice._views[id] as KidModel
	var state: PlayerState = practice.sim.players[id]
	var before: Vector3 = view.position
	practice.sim.damage(id, 5000)
	assert_true(state.tagged)
	practice.advance(DT)
	assert_eq(view.position, before, "shrinking where it was tagged (the sim already moved it)")
	var hp_before: int = state.hp
	for i: int in 12:
		practice.advance(DT)
	assert_eq(view.position.x, state.position.x, "then in the tagged box")
	assert_eq(view.position.z, state.position.y)
	assert_eq(view._body.scale, Vector3.ONE, "full size again")
	assert_eq(state.hp, hp_before, "the sim never knew")
	assert_false(practice.sim.layout.lane_rect().has_point(state.position), "outside the play area")
