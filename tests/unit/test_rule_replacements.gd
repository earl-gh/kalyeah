extends GutTest

const DT: float = 1.0 / 30.0


func test_last_tag_scores_on_the_next_simulation_step_for_either_team() -> void:
	for winner: int in [0, 1]:
		var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres"), load("res://data/rules/map_layout.tres"), 17)
		for slot: int in 3:
			sim.add_player(10 + slot, winner)
			sim.add_player(20 + slot, 1 - winner)
		sim.kill(20)
		sim.kill(21)
		sim.step(DT)
		assert_eq(sim.score.points[winner], 0, "one opponent is still free")
		sim.kill(22)
		sim.step(DT)
		assert_eq(sim.score.points[winner], 1, "no capture hold after the last tag")
		assert_eq(sim.phase, MatchSim.Phase.POINT_FREEZE)
		assert_eq(sim.point_log, [Vector2i(winner, MatchSim.CaptureKind.ALL_TAGGED)] as Array[Vector2i])
		sim.step(DT)
		assert_eq(sim.score.points[winner], 1, "the freeze must not score again")


func test_reapplying_a_status_uses_the_new_duration_and_magnitude() -> void:
	var effects: StatusEffects = StatusEffects.new()
	for type: StatusEffects.Type in [StatusEffects.Type.STUN, StatusEffects.Type.SLOW, StatusEffects.Type.AIRBORNE, StatusEffects.Type.POLYMORPH, StatusEffects.Type.BOUNCE, StatusEffects.Type.KNOCKOUT]:
		effects.clear()
		effects.apply(type, 5.0, 0.8)
		effects.step(0.25)
		effects.apply(type, 1.0, 0.2)
		assert_almost_eq(effects.time_left(type), 1.0, 0.0001, "the old timer is replaced")
		assert_eq(effects.active_names().size(), 1)
		if type == StatusEffects.Type.SLOW:
			assert_almost_eq(effects.speed_multiplier(0.5), 0.8, 0.0001)
		effects.step(1.01)
		assert_false(effects.has(type))


func test_a_team_wipe_scores_immediately_even_during_a_base_capture() -> void:
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres"), load("res://data/rules/map_layout.tres"), 18)
	var attacker: PlayerState = sim.add_player(1, 0)
	sim.add_player(2, 1)
	attacker.position = sim.layout.base_center(-sim.side_for_team(0))
	sim.step(DT)
	assert_eq(sim.capture_kind, MatchSim.CaptureKind.BASE)
	sim.kill(2)
	sim.step(DT)
	assert_eq(sim.score.points[0], 1)
	assert_eq(sim.point_log[0], Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED))
	var mirror: MatchSim = MatchSim.new(sim.rules, sim.layout, 18)
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	Snapshot.apply(mirror, Snapshot.capture(sim, {} as Dictionary[int, int]), 1)
	assert_eq(mirror.phase, MatchSim.Phase.POINT_FREEZE)
	assert_eq(mirror.score.points[0], 1, "remote clients see the awarded point")


func test_a_different_status_starts_fresh_and_only_it_survives_in_snapshots() -> void:
	var effects: StatusEffects = StatusEffects.new()
	effects.apply(StatusEffects.Type.STUN, 1.0)
	effects.step(0.9)
	effects.apply(StatusEffects.Type.POLYMORPH, 3.0)
	assert_false(effects.has(StatusEffects.Type.STUN))
	assert_almost_eq(effects.time_left(StatusEffects.Type.POLYMORPH), 3.0, 0.0001)
	var mirror: StatusEffects = StatusEffects.new()
	mirror.restore(effects.snapshot())
	assert_eq(mirror.active_names(), PackedStringArray(["POLYMORPH"]))
	mirror.step(2.9)
	assert_true(mirror.has(StatusEffects.Type.POLYMORPH), "the old stun's 0.1 s cannot shorten polymorph")
