extends GutTest
## The kids play the animation clips of their own 3D models at their own speed (the details are in test_authored_kids.gd).
## And a kid hit by a skill plays the animation it causes (stunned, airborne; a knock-out shows as stunned).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
## Long enough to watch an animation.
const STATUS_TIME: float = 3.0
const KIDS: Array[StringName] = [&"junjun", &"ligaya", &"migo", &"toni", &"popoy", &"inday"]


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


func test_every_kid_runs_at_its_own_clip_speed_whatever_the_ground_speed() -> void:
	for id: StringName in KIDS:
		var kid: KidModel = _kid(id)
		for speed: float in [2.0, 5.0, 9.0]:
			for i: int in 12:
				kid.animate(DT, speed, PlayerState.new())
			assert_eq(kid.current_animation(), "run", id)
			assert_almost_eq(kid._player.speed_scale, 1.0, 0.0001, "%s: the run clip plays at its own speed at %.0f m/s" % [id, speed])


func test_a_cast_shows_the_clip_at_its_own_speed_and_for_the_rest_of_its_length() -> void:
	for id: StringName in KIDS:
		var kid: KidModel = _kid(id)
		kid.play_cast()
		kid.animate(DT, 0.0, PlayerState.new())
		assert_eq(kid.pose, "cast", id)
		assert_almost_eq(kid._player.speed_scale, 1.0, 0.0001, "%s: not squeezed into the 0.38 s cast time" % id)
		assert_almost_eq(kid._cast_total, kid._clip_length("cast") - kid.cast_release("cast"), 0.001, id)
		assert_gt(kid._cast_total, 1.0, "%s: the clip is a real, long one" % id)


func test_the_footsteps_follow_the_run_clip() -> void:
	for id: StringName in KIDS:
		var kid: KidModel = _kid(id)
		var steps: Array[int] = [0]
		kid.footstep.connect(func() -> void: steps[0] += 1)
		var state: PlayerState = PlayerState.new()
		var seconds: float = kid._clip_length("run") * 4.0
		for i: int in int(seconds / DT):
			kid.animate(DT, 6.0, state)
		assert_between(steps[0], 6, 9, "%s: two steps per cycle of the run clip, four cycles" % id)


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
		StatusEffects.Type.STUN: "stunned", StatusEffects.Type.AIRBORNE: "airborne", StatusEffects.Type.KNOCKOUT: "stunned",
	}
	for type: int in expect:
		player.effects.apply(type as StatusEffects.Type, STATUS_TIME)
		assert_true(player.effects.has(type as StatusEffects.Type), "the status is on")
		for i: int in 3:
			practice.advance(DT)
		var view: KidModel = practice._views[PracticeMatch.LOCAL_ID] as KidModel
		# no kid has a fall: a knock-out shows as stunned
		assert_eq(view.pose, expect[type], "the kid plays the %s animation" % expect[type])
		for i: int in int(STATUS_TIME * 30.0) + 60:
			practice.advance(DT)
		assert_false(player.effects.has(type as StatusEffects.Type), "it wears off")


# ---- the loops must not snap at the end of each cycle (it looked like vibrating) -----------

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
	for id: StringName in KIDS:
		var kid: KidModel = _kid(id)
		var library: AnimationLibrary = kid._player.get_animation_library("")
		for clip: String in KidModel.LOOPING:
			var animation: Animation = library.get_animation(clip)
			assert_eq(animation.loop_mode, Animation.LOOP_LINEAR, "%s %s loops" % [id, clip])
			assert_lt(_worst_gap(animation, animation.length - 0.002), 0.2, "%s %s loops without a snap" % [id, clip])
