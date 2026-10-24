extends GutTest

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0


func _sim() -> MatchSim:
	return MatchSim.new(RULES, LAYOUT, 3)


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


## The enemy post of `player`'s team (inside their base).
func _enemy_post(sim: MatchSim, player: PlayerState) -> Vector2:
	return LAYOUT.base_center(-sim.side_for_team(player.team))


## Puts `player` on the enemy post and waits for the point.
func _score(sim: MatchSim, player: PlayerState) -> void:
	player.position = _enemy_post(sim, player)
	_wait(sim, RULES.capture_solo_time + 0.1)


func _finish_freeze(sim: MatchSim) -> void:
	_wait(sim, RULES.point_freeze_time + 0.1)


# ---- MatchScore: one tier, first to 3 / 5 / 7 -------------------------------------

func test_scoring_numbers_come_from_data() -> void:
	assert_eq(RULES.capture_time, 5.0)
	assert_eq(RULES.point_freeze_time, 3.0)
	assert_eq(RULES.first_to, 3, "practice and new rooms: first to 3")
	assert_eq(RULES.first_to_choices, [3, 5, 7] as Array[int], "what the host can pick")


func test_first_to_three_is_won_with_three_points() -> void:
	var score: MatchScore = MatchScore.new(3)
	assert_eq(score.points_to_win(), 3)
	assert_eq(score.award_point(0), MatchScore.Result.POINT)
	assert_eq(score.award_point(1), MatchScore.Result.POINT, "1-1")
	assert_eq(score.award_point(1), MatchScore.Result.POINT, "1-2: two points are not enough any more")
	assert_eq(score.award_point(1), MatchScore.Result.MATCH)
	assert_eq(score.winner, 1)
	assert_eq(score.award_point(0), MatchScore.Result.MATCH, "nothing changes after the match")
	assert_eq(score.points, [1, 3] as Array[int])


func test_first_to_five_and_seven_need_five_and_seven() -> void:
	assert_eq(MatchScore.new(5).points_to_win(), 5)
	assert_eq(MatchScore.new(7).points_to_win(), 7)
	var score: MatchScore = MatchScore.new(7)
	for i: int in 6:
		assert_eq(score.award_point(0), MatchScore.Result.POINT)
	assert_eq(score.award_point(0), MatchScore.Result.MATCH)


func test_the_sim_takes_the_hosts_choice_and_ignores_anything_else() -> void:
	var sim: MatchSim = _sim()
	assert_eq(sim.score.first_to, 3)
	sim.set_first_to(7)
	assert_eq(sim.score.first_to, 7)
	sim.set_first_to(4)
	assert_eq(sim.score.first_to, 7, "only 3, 5 or 7")


# ---- capture: free players standing in the enemy base -----------------------------

func test_standing_alone_in_the_enemy_base_for_the_solo_time_scores() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	watch_signals(sim)
	me.position = _enemy_post(sim, me) + Vector2(0.0, 1.5)
	_wait(sim, RULES.capture_solo_time - 0.2)
	assert_eq(sim.score.points[0], 0, "not yet")
	assert_eq(sim.capture_team, 0)
	assert_eq(sim.capture_kind, MatchSim.CaptureKind.BASE)
	assert_almost_eq(sim.capture_time_left(), 0.2, 0.05)
	_wait(sim, 0.4)
	assert_eq(sim.score.points[0], 1)
	assert_signal_emitted_with_parameters(sim, "point_scored", [0, MatchSim.CaptureKind.BASE])
	assert_eq(sim.phase, MatchSim.Phase.POINT_FREEZE)


func test_one_teammate_in_the_base_is_enough_and_the_one_outside_does_not_stop_it() -> void:
	var sim: MatchSim = _sim()
	var a: PlayerState = sim.add_player(1, 0)
	var b: PlayerState = sim.add_player(2, 0)
	a.position = _enemy_post(sim, a)
	b.position = Vector2(0.0, 0.0)
	_wait(sim, 1.0)
	assert_eq(sim.capture_team, 0, "the count runs with the first teammate in")
	_wait(sim, RULES.capture_solo_time)
	assert_eq(sim.score.points[0], 1)


func test_a_tagged_teammate_does_not_stop_the_capture() -> void:
	var sim: MatchSim = _sim()
	var a: PlayerState = sim.add_player(1, 0)
	var b: PlayerState = sim.add_player(2, 0)
	a.position = _enemy_post(sim, a)
	b.position = _enemy_post(sim, b)
	sim.kill(2)
	_wait(sim, RULES.capture_solo_time + 0.5)
	assert_eq(sim.score.points[0], 1, "the free one captures alone")


func test_stepping_out_restarts_the_countdown() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var post: Vector2 = _enemy_post(sim, me)
	me.position = post
	_wait(sim, RULES.capture_solo_time - 0.5)
	me.position = Vector2.ZERO
	sim.step(DT)
	assert_eq(sim.capture_team, -1)
	me.position = post
	_wait(sim, RULES.capture_solo_time - 0.5)
	assert_eq(sim.score.points[0], 0)


func test_own_base_does_not_score() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(1, 0)
	_wait(sim, RULES.capture_time + 1.0)
	assert_eq(sim.score.points, [0, 0] as Array[int])


func test_training_dummies_do_not_count_for_a_capture() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	sim.add_dummy(2, 0, Vector2(0.0, 10.0), DummyBrain.standing())
	me.position = _enemy_post(sim, me)
	_wait(sim, RULES.capture_solo_time + 0.1)
	assert_eq(sim.score.points[0], 1, "the ally dummy stays home, you still score")


# ---- capture: every enemy tagged ---------------------------------------------------

func test_tagging_the_whole_other_team_scores_after_the_countdown() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.add_player(3, 1)
	watch_signals(sim)
	sim.kill(2)
	_wait(sim, 1.0)
	assert_eq(sim.capture_team, -1, "one of them is still free")
	sim.kill(3)
	sim.step(DT)
	assert_eq(sim.capture_team, 0)
	assert_eq(sim.capture_kind, MatchSim.CaptureKind.ALL_TAGGED)
	_wait(sim, RULES.capture_time)
	assert_eq(sim.score.points[0], 1)
	assert_signal_emitted_with_parameters(sim, "point_scored", [0, MatchSim.CaptureKind.ALL_TAGGED])


# ---- freeze and reset ----------------------------------------------------------

func test_freeze_then_everyone_is_reset_at_full_hp_out_of_the_boxes() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var them: PlayerState = sim.add_player(2, 1)
	sim.set_loadout(1, &"bato_light", &"lata")
	me.weapon_cooldowns = [3.0, 9.0] as Array[float]
	sim.apply_effect(2, StatusEffects.Type.SLOW, 10.0, 0.4)
	sim.kill(2)
	_score(sim, me)
	var frozen_at: Vector2 = me.position
	_wait(sim, 2.0)
	assert_eq(me.position, frozen_at, "nobody moves in the freeze")
	assert_eq(sim.phase, MatchSim.Phase.POINT_FREEZE)
	watch_signals(sim)
	_wait(sim, 1.2)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING)
	assert_signal_emitted(sim, "point_reset")
	assert_eq(me.position, me.spawn_position)
	assert_eq(them.position, them.spawn_position)
	assert_false(them.tagged, "the tagged come out of the box too")
	assert_eq(them.hp, 100)
	assert_false(them.effects.has(StatusEffects.Type.SLOW))
	assert_eq(me.weapon_cooldowns, [0.0, 0.0] as Array[float])
	assert_eq(sim.capture_team, -1)


func test_first_to_five_ends_after_five_points_and_the_ends_never_switch() -> void:
	var sim: MatchSim = _sim()
	sim.set_first_to(5)
	var me: PlayerState = sim.add_player(1, 0)
	var start: Vector2 = me.spawn_position
	watch_signals(sim)
	for i: int in 4:
		_score(sim, me)
		_finish_freeze(sim)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING, "4 of 5 is not enough")
	assert_eq(me.spawn_position, start, "same end all match")
	_score(sim, me)
	_finish_freeze(sim)
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_signal_emitted_with_parameters(sim, "match_won", [0])
	var at: Vector2 = me.position
	_wait(sim, 5.0)
	assert_eq(me.position, at, "nothing runs after the match")


func test_a_team_of_training_dummies_never_scores() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(1, 0)
	sim.add_dummy(2, 1, Vector2(0.0, -10.0), DummyBrain.standing())
	sim.kill(1)
	_wait(sim, RULES.capture_time + 0.5)
	assert_eq(sim.score.points, [0, 0] as Array[int], "practice: being tagged gives the dummies no point")


func test_the_intro_holds_everyone_then_the_clock_runs_only_in_play() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	sim.begin_intro()
	watch_signals(sim)
	var start: Vector2 = me.position
	for i: int in ceili((RULES.intro_time - 0.2) / DT):
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_eq(me.position, start, "nobody moves in the intro")
	assert_eq(sim.match_time, 0.0)
	_wait(sim, 0.4)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING)
	assert_signal_emitted(sim, "intro_finished")
	_wait(sim, 2.0)
	assert_between(sim.match_time, 2.0, 2.5, "counts up during play")
	_score(sim, me)
	var at: float = sim.match_time
	_wait(sim, 1.0)
	assert_eq(sim.match_time, at, "stops during the freeze")
