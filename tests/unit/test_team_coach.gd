extends GutTest
## TeamCoach: bots that are real players, a team plan that attacks or defends, allies that back up the human.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


## You (id 1) and two allies (2, 3) against three enemies (4, 5, 6); everyone armed.
func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 12)
	for id: int in [1, 2, 3]:
		sim.add_player(id, 0)
	for id: int in [4, 5, 6]:
		sim.add_player(id, 1)
	for id: int in sim.players:
		sim.set_loadout(id, &"bato_light", &"papel_shield")
	return sim


func _enemies(sim: MatchSim) -> TeamCoach:
	var coach: TeamCoach = TeamCoach.new(1)
	for id: int in [4, 5, 6]:
		coach.add(sim, id, 0.4)
	return coach


func _allies(sim: MatchSim) -> TeamCoach:
	var coach: TeamCoach = TeamCoach.new(0, 1)
	for id: int in [2, 3]:
		coach.add(sim, id, 0.4)
	return coach


func _run(sim: MatchSim, coaches: Array[TeamCoach], seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		for coach: TeamCoach in coaches:
			coach.drive(sim)
		sim.step(DT)


func test_bots_are_real_players_who_count_for_a_capture() -> void:
	var sim: MatchSim = _sim()
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	var base: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	for id: int in [1, 2, 3]:
		sim.players[id].position = base + Vector2(float(id) - 2.0, 0.0)
	for id: int in [4, 5, 6]:
		sim.players[id].position = Vector2(float(id - 5) * 2.0, 0.0)
	for i: int in ceili((_rules.capture_solo_time + 1.0) / DT):
		sim.step(DT)
	assert_eq(scored, [0] as Array[int], "three free players in the enemy base are a capture whether or not a brain drives them")


func test_the_enemy_team_defends_until_the_delay_and_then_attacks() -> void:
	_rules.bot_assault_delay = 10.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	_run(sim, [coach] as Array[TeamCoach], 5.0)
	assert_eq(coach.plan, TeamCoach.Plan.DEFEND)
	_run(sim, [coach] as Array[TeamCoach], 6.0)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)


func test_a_team_with_most_of_it_tagged_falls_back_to_defending() -> void:
	_rules.bot_assault_delay = 0.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	_run(sim, [coach] as Array[TeamCoach], 1.0)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)
	sim.kill(4)
	_run(sim, [coach] as Array[TeamCoach], 0.2)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT, "one of three tagged: still two thirds free")
	sim.kill(5)
	_run(sim, [coach] as Array[TeamCoach], 0.2)
	assert_eq(coach.plan, TeamCoach.Plan.DEFEND)


func test_allies_defend_until_the_human_is_in_the_enemy_half_and_then_back_them_up() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _allies(sim)
	sim.players[1].position = LAYOUT.spawn_center(MapLayout.SIDE_OWN)
	coach.drive(sim)
	assert_eq(coach.plan, TeamCoach.Plan.DEFEND)
	sim.players[1].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	coach.drive(sim)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)
	sim.kill(1)
	coach.drive(sim)
	assert_eq(coach.plan, TeamCoach.Plan.DEFEND, "a tagged human: the allies go back to fetch them")


func test_the_enemy_plan_works_from_the_other_side_of_the_street() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = TeamCoach.new(1, 4)
	coach.add(sim, 5, 0.4)
	sim.players[4].position = LAYOUT.base_center(MapLayout.SIDE_OWN)
	coach.drive(sim)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT, "team 1's enemy half is the +Z end")


func test_driven_bots_walk_and_tagged_ones_are_left_alone() -> void:
	_rules.bot_assault_delay = 0.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	var before: Vector2 = sim.players[4].position
	_run(sim, [coach] as Array[TeamCoach], 2.0)
	assert_ne(sim.players[4].position, before, "the attack is under way")
	sim.kill(6)
	var box: Vector2 = sim.players[6].position
	_run(sim, [coach] as Array[TeamCoach], 1.0)
	assert_eq(sim.players[6].position, box, "a tagged bot stays where it stands")


func test_no_bot_is_driven_outside_play() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	sim.begin_intro()
	var before: Vector2 = sim.players[4].position
	coach.drive(sim)
	sim.step(DT)
	assert_eq(sim.players[4].position, before)


func test_a_bot_match_with_nobody_at_the_controls_ends_with_a_point() -> void:
	_rules.bot_assault_delay = 5.0
	var sim: MatchSim = _sim()
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	var coaches: Array[TeamCoach] = [_enemies(sim), _allies(sim)]
	for i: int in 30 * 150:
		for coach: TeamCoach in coaches:
			coach.drive(sim)
		sim.step(DT)
		if not scored.is_empty():
			break
	assert_false(scored.is_empty(), "within two and a half minutes someone has scored")


# ---- the Practice match vs bots ------------------------------------------------------------

func _bot_match() -> PracticeMatch:
	Session.practice_kind = &"match"
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	Session.practice_kind = &"training"
	return practice


func test_a_practice_match_vs_bots_has_three_real_players_a_side() -> void:
	var practice: PracticeMatch = _bot_match()
	var counts: Array[int] = [0, 0]
	for id: int in practice.sim.players:
		counts[practice.sim.players[id].team] += 1
		assert_false(practice.sim.is_fighter(id), "bots are players of the sim, not training dummies")
	assert_eq(counts, [3, 3] as Array[int])
	assert_eq(practice.local_team, 0)
	for id: int in PracticeMatch.BOT_ENEMY_IDS:
		assert_eq(practice.sim.players[id].team, 1)
		assert_gt(practice.sim.players[id].weapons.size(), 1, "armed")


func test_training_is_still_the_default_practice() -> void:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	assert_true(practice.sim.is_fighter(PracticeMatch.ENEMY_PATROL_ID) or practice.rules.practice_fighters == 0)
	assert_eq(practice.sim.players.size(), 5, "you, an ally dummy, two enemy dummies and the one in its rescue area")


func test_the_bots_move_when_the_match_runs() -> void:
	var practice: PracticeMatch = _bot_match()
	practice.finish_pick()
	practice.skip_intro()
	var start: Vector2 = practice.sim.players[PracticeMatch.BOT_ALLY_IDS[0]].position
	practice.rules.bot_assault_delay = 0.0
	practice.input.move_press(practice.sim.players[practice.local_id].position + Vector2(0.0, -3.0))
	for i: int in 90:
		practice.advance(DT)
	assert_ne(practice.sim.players[PracticeMatch.BOT_ENEMY_IDS[0]].position, practice.sim.players[PracticeMatch.BOT_ENEMY_IDS[0]].spawn_position, "an enemy has set off")
	assert_ne(start, Vector2.INF)


func test_the_enemy_attacks_in_waves_and_rests_between_them() -> void:
	_rules.bot_assault_delay = 10.0
	_rules.bot_assault_window = 8.0
	_rules.bot_rest_time = 6.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	var seen: Dictionary[int, int] = {}
	for second: int in 40:
		_run(sim, [coach] as Array[TeamCoach], 1.0)
		# players are never tagged here: only the clock decides
		seen[second] = coach.plan
	assert_eq(seen[5], TeamCoach.Plan.DEFEND, "waiting")
	assert_eq(seen[12], TeamCoach.Plan.ASSAULT, "first wave (10 to 18 s)")
	assert_eq(seen[20], TeamCoach.Plan.DEFEND, "resting (18 to 24 s)")
	assert_eq(seen[27], TeamCoach.Plan.ASSAULT, "second wave (24 to 32 s)")
	assert_eq(seen[35], TeamCoach.Plan.DEFEND, "resting again")


func test_a_window_of_zero_means_the_enemy_never_stops_attacking() -> void:
	_rules.bot_assault_delay = 0.0
	_rules.bot_assault_window = 0.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	_run(sim, [coach] as Array[TeamCoach], 60.0)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)
