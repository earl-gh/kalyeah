extends GutTest
## Toni, Popoy and Inday are the owner's authored models (CharacterDef.authored_model): their GLB keeps its own material
## and clips, a cast starts at the hand's release so it lines up with the sim's cast tick, and a tagged kid shrinks away
## and grows back in the tagged box (no fall, no throw clip, no layered upper body).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const DT: float = 1.0 / 30.0
const STEP: float = 1.0 / 60.0
const AUTHORED: Array[StringName] = [&"toni", &"popoy", &"inday"]
const OLD: Array[StringName] = [&"junjun", &"ligaya", &"migo"]
## The part of each clip that is the cast gesture (seconds): Cast_OneHand's swing from the wind-up to the hand's arrival, and
## Cast_Run's raise (after the run cycle's own arm swing). The release is the time the hand has covered half of its travel.
const WINDOW: Dictionary[String, Vector2] = {"Cast_OneHand": Vector2(0.5, 0.9), "Cast_Run": Vector2(0.85, 1.15)}


func _def(id: StringName) -> CharacterDef:
	for def: CharacterDef in RULES.characters:
		if def.id == id:
			return def
	return null


func _kid(id: StringName) -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(_def(id))
	add_child(kid)
	return kid


func _walk(kid: KidModel, speed: float, frames: int = 12) -> void:
	for i: int in frames:
		kid.animate(DT, speed, PlayerState.new())


func test_only_the_three_authored_kids_are_marked() -> void:
	for id: StringName in AUTHORED:
		assert_true(_def(id).authored_model, "%s is authored" % id)
		assert_true(_def(id).cast_release_standing > 0.0 and _def(id).cast_release_moving > 0.0, "%s has release times" % id)
	for id: StringName in OLD:
		assert_false(_def(id).authored_model, "%s keeps the old path" % id)


func test_their_old_baked_textures_are_gone() -> void:
	for id: StringName in AUTHORED:
		assert_false(FileAccess.file_exists("res://assets/models/characters/%s_kid_base_color.jpg" % id), id)
		assert_true(ResourceLoader.exists("res://assets/models/characters/%s_0.png" % id), "%s keeps its authored textures" % id)


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
		assert_eq(material.albedo_texture.get_size(), Vector2(2048, 2048), "full size texture")
		kid.animate(DT, 0.0, PlayerState.new())
		assert_eq(material.albedo_color, Color.WHITE, "no tint on the authored colours")
		assert_almost_eq(mesh.get_aabb().size.y * (kid.find_child("Model", true, false) as Node3D).scale.y, KidModel.KID_HEIGHT, 0.01)


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
		for spell: bool in [false, true]:
			var standing: KidModel = _kid(id)
			standing.play_cast(0.5, spell)
			standing.animate(0.0, 0.0, PlayerState.new())
			assert_eq(standing.pose, "cast")
			assert_eq(standing._player.current_animation, "cast", "%s standing" % id)
			assert_eq(standing._player.get_animation("cast"), standing._player.get_animation("Cast_OneHand"))
			var moving: KidModel = _kid(id)
			_walk(moving, 5.0)
			moving.play_cast(0.5, spell)
			moving.animate(0.0, 5.0, PlayerState.new())
			assert_eq(moving._player.current_animation, "cast_run", "%s moving" % id)
			assert_eq(moving._player.get_animation("cast_run"), moving._player.get_animation("Cast_Run"))
			assert_almost_eq(moving._player.speed_scale, 1.0, 0.0001, "unsqueezed")


func test_a_cast_ends_when_the_kid_starts_or_stops_moving() -> void:
	var kid: KidModel = _kid(&"toni")
	kid.play_cast(0.5)
	kid.animate(0.0, 0.0, PlayerState.new())
	_walk(kid, 6.0)
	assert_eq(kid._player.current_animation, "run", "running leaves the standing cast")
	kid.play_cast(0.5)
	kid.animate(0.0, 6.0, PlayerState.new())
	assert_eq(kid._player.current_animation, "cast_run")
	for i: int in 30:
		kid.animate(DT, 0.0, PlayerState.new())
	assert_eq(kid._player.current_animation, "idle", "stopping leaves the moving cast")


## The release measured on the model itself (not read from the data): the time the right hand has covered half of the
## distance it travels over the gesture, sampled at 1/60 s from the skeleton's bone poses.
func _measured_release(id: StringName, clip: String) -> float:
	var root: Node3D = autofree((load("res://assets/models/characters/%s.glb" % id) as PackedScene).instantiate()) as Node3D
	add_child(root)
	await wait_process_frames(1)
	var player: AnimationPlayer = root.find_child("AnimationPlayer", true, false) as AnimationPlayer
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	var skeleton: Skeleton3D = root.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
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
			kid.play_cast(0.5)  # the sim's weapon_cast signal: this is the cast tick
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
	kid.play_cast(0.5)
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


func test_the_other_kids_still_fall_and_are_never_shrunk() -> void:
	var kid: KidModel = _kid(&"junjun")
	var state: PlayerState = PlayerState.new()
	kid.animate(DT, 0.0, state)
	state.tagged = true
	for i: int in 10:
		kid.animate(1.0 / 60.0, 0.0, state)
	assert_eq(kid._body.scale, Vector3.ONE)
	state.tagged = false
	state.effects.apply(StatusEffects.Type.KNOCKOUT, 2.0)
	kid.animate(DT, 0.0, state)
	assert_eq(kid.pose, "ko")


func test_in_a_match_a_tagged_authored_kid_reappears_at_the_tagged_place() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	var id: int = PracticeMatch.LOCAL_ID
	practice.sim.players[id].character_id = &"toni"
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
