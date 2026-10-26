extends GutTest
## Junjun plays the animation clips of his own 3D model at their own speed (CharacterDef
## .native_animation); the other kids keep the game's pacing. And a kid hit by a skill plays the animation
## it causes (stunned, airborne, knocked down).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
## Long enough to watch an animation.
const STATUS_TIME: float = 3.0


func _def(id: StringName) -> CharacterDef:
	for def: CharacterDef in (load("res://data/rules/game_rules.tres") as GameRules).characters:
		if def.id == id:
			return def
	return null


func _kid(id: StringName) -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(_def(id))
	add_child(kid)
	return kid


func _run(kid: KidModel, speed: float) -> void:
	var state: PlayerState = PlayerState.new()
	for i: int in 12:
		kid.animate(DT, speed, state)


func test_only_junjun_is_marked_native() -> void:
	assert_true(_def(&"junjun").native_animation)
	for id: StringName in [&"toni", &"ligaya", &"migo", &"popoy", &"inday"]:
		assert_false(_def(id).native_animation, "%s keeps the game's pacing" % id)


func test_junjun_runs_at_his_own_clip_speed_whatever_the_ground_speed() -> void:
	var kid: KidModel = _kid(&"junjun")
	for speed: float in [2.0, 5.0, 9.0]:
		_run(kid, speed)
		assert_eq(kid.current_animation(), "run")
		assert_almost_eq(kid._player.speed_scale, 1.0, 0.0001, "his run clip plays at its own speed at %.0f m/s" % speed)


func test_the_other_kids_run_is_still_paced_to_the_ground() -> void:
	var kid: KidModel = _kid(&"toni")
	_run(kid, 2.0)
	var slow: float = kid._player.speed_scale
	_run(kid, 7.0)
	assert_gt(kid._player.speed_scale, slow, "the faster they go the faster the cycle")


func test_a_cast_shows_his_clip_at_its_own_speed_and_for_its_own_length() -> void:
	var kid: KidModel = _kid(&"junjun")
	var state: PlayerState = PlayerState.new()
	kid.play_cast(0.38, false)
	kid.animate(DT, 0.0, state)
	assert_eq(kid.pose, "cast")
	assert_almost_eq(kid._player.speed_scale, 1.0, 0.0001, "not squeezed into the 0.38 s cast time")
	assert_almost_eq(kid._cast_total, kid._clip_length("cast"), 0.001, "the cast shows for the clip's own length")
	assert_gt(kid._cast_total, 1.0, "his throw is a real, long clip")
	var toni: KidModel = _kid(&"toni")
	toni.play_cast(0.38, false)
	toni.animate(DT, 0.0, state)
	assert_gt(toni._player.speed_scale, 1.5, "the others are still squeezed into the cast time")


func test_a_spell_starts_at_the_beginning_of_his_clip() -> void:
	var kid: KidModel = _kid(&"junjun")
	assert_eq(kid._clip_start("spell"), 0.0)
	assert_gt(_kid(&"toni")._clip_start("spell"), 0.0, "the others skip their charge-up")


func test_his_footsteps_follow_his_run_clip() -> void:
	var kid: KidModel = _kid(&"junjun")
	var steps: Array[int] = [0]
	kid.footstep.connect(func() -> void: steps[0] += 1)
	var state: PlayerState = PlayerState.new()
	var seconds: float = kid._clip_length("run") * 4.0
	for i: int in int(seconds / DT):
		kid.animate(DT, 6.0, state)
	assert_between(steps[0], 6, 9, "two steps per cycle of his run clip, four cycles")


# ---- the animations a skill hit causes ------------------------------------------------------

func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_each_status_plays_its_animation_on_your_kid_until_it_wears_off() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var expect: Dictionary = {
		StatusEffects.Type.STUN: "stunned", StatusEffects.Type.AIRBORNE: "airborne", StatusEffects.Type.KNOCKOUT: "ko",
	}
	for type: int in expect:
		player.effects.apply(type as StatusEffects.Type, STATUS_TIME)
		assert_true(player.effects.has(type as StatusEffects.Type), "the status is on")
		for i: int in 3:
			practice.advance(DT)
		var view: KidModel = practice._views[PracticeMatch.LOCAL_ID] as KidModel
		assert_eq(view.pose, expect[type], "the kid plays the %s animation" % expect[type])
		for i: int in int(STATUS_TIME * 30.0) + 60:
			practice.advance(DT)
		assert_false(player.effects.has(type as StatusEffects.Type), "it wears off")


# ---- the run must not snap at the end of each cycle (it looked like vibrating) -----------

func _worst_gap(animation: Animation, at_end: float) -> float:
	var worst: float = 0.0
	for track: int in animation.get_track_count():
		if animation.track_get_type(track) != Animation.TYPE_ROTATION_3D:
			continue
		var start: Quaternion = animation.rotation_track_interpolate(track, 0.0)
		var end: Quaternion = animation.rotation_track_interpolate(track, at_end)
		worst = maxf(worst, start.angle_to(end))
	return worst


func test_every_looping_clip_comes_back_to_its_first_pose() -> void:
	for id: StringName in [&"junjun", &"toni", &"ligaya"]:
		var kid: KidModel = _kid(id)
		var library: AnimationLibrary = kid._player.get_animation_library("")
		for clip: String in KidModel.LOOPING:
			if not library.has_animation(clip):
				continue
			var animation: Animation = library.get_animation(clip)
			# one frame before the loop point the pose is within a frame of the first pose
			var frame: float = animation.length / float(maxi(1, animation.track_get_key_count(0)))
			assert_lt(_worst_gap(animation, animation.length - 0.002), 0.2, "%s %s loops without a snap" % [id, clip])
			assert_gt(frame, 0.0)


func test_junjuns_run_loop_is_one_key_longer_than_the_imported_clip() -> void:
	var kid: KidModel = _kid(&"junjun")
	var run: Animation = kid._player.get_animation("run")
	assert_true(run.has_meta("loop_closed"))
	var last_key: float = 0.0
	for track: int in run.get_track_count():
		last_key = maxf(last_key, run.track_get_key_time(track, run.track_get_key_count(track) - 1))
	assert_gt(run.length, last_key + 0.01, "room to blend the last pose back into the first")
	assert_lt(_worst_gap(run, last_key), 0.6, "sanity")
	var again: KidModel = _kid(&"junjun")
	assert_almost_eq(again._player.get_animation("run").length, run.length, 0.0001, "closed once, not once per kid")
