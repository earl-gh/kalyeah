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


func test_with_the_real_numbers_the_enemies_come_out_right_after_go_and_rest_briefly() -> void:
	# owner: a round used to open with 25 s of nobody moving; they now come out right after GO
	assert_lte(_rules.bot_assault_delay, 3.0, "the data's own numbers (before_each loads them)")
	assert_lte(_rules.bot_rest_time, 8.0)
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	var start: Vector2 = sim.players[4].position
	_run(sim, [coach] as Array[TeamCoach], _rules.bot_assault_delay + 2.0)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT, "attacking a couple of seconds after GO")
	assert_gt(sim.players[4].position.distance_to(start), 2.0, "and out of their base")


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


func test_allies_come_out_on_the_clock_like_the_enemies_while_you_stay_home() -> void:
	# owner: they used to stand still in their own half until you crossed into the enemy's
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _allies(sim)
	var start: Vector2 = sim.players[2].position
	_run(sim, [coach] as Array[TeamCoach], _rules.bot_assault_delay + 3.0)
	assert_true(LAYOUT.in_base(MapLayout.SIDE_OWN, sim.players[1].position), "you never left your base")
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT, "they attack on the same clock as the enemies")
	assert_gt(sim.players[2].position.distance_to(start), 2.0, "and are on their way")


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
	var delay: float = practice.rules.bot_assault_delay  # the scene's rules are the shared resource: put the number back afterwards
	practice.rules.bot_assault_delay = 0.0
	practice.input.move_press(practice.sim.players[practice.local_id].position + Vector2(0.0, -3.0))
	for i: int in 90:
		practice.advance(DT)
	assert_ne(practice.sim.players[PracticeMatch.BOT_ENEMY_IDS[0]].position, practice.sim.players[PracticeMatch.BOT_ENEMY_IDS[0]].spawn_position, "an enemy has set off")
	assert_ne(start, Vector2.INF)
	practice.rules.bot_assault_delay = delay


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


func test_while_attacking_the_set_number_of_attackers_hold_the_enemy_base_and_keep_the_job() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	sim.round_time = _rules.bot_assault_delay + 1.0
	for holders: int in [0, 1, 2]:
		_rules.bot_base_holders = holders
		coach._holders.clear()
		coach.drive(sim)
		assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)
		var held: Array[int] = []
		for id: int in coach.members():
			if coach._brains[id].hold:
				held.append(id)
		assert_eq(held.size(), holders, "%d base holders asked for" % holders)
		for i: int in 60:
			coach.drive(sim)
			sim.step(DT)
			sim.round_time = _rules.bot_assault_delay + 1.0
		var after: Array[int] = []
		for id: int in coach.members():
			if coach._brains[id].hold:
				after.append(id)
		assert_eq(after, held, "the job stays with the same bots")


func test_defenders_hold_nothing() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	sim.round_time = 0.5  # before the assault delay
	coach.drive(sim)
	for id: int in coach.members():
		assert_false(coach._brains[id].hold)


## Both teams of bots play ninety seconds: a bot that holds the enemy base never steps out of it while it is free, and no bot that is
## free to act stands still for a whole second (casting, a status or the capture itself aside).
func test_bots_never_stand_around_and_holders_never_blink_the_capture() -> void:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 31)
	var ours: TeamCoach = TeamCoach.new(0)
	var theirs: TeamCoach = TeamCoach.new(1)
	for id: int in [1, 2, 3]:
		sim.add_player(id, 0)
		ours.add(sim, id, 0.4)
	for id: int in [4, 5, 6]:
		sim.add_player(id, 1)
		theirs.add(sim, id, 0.4)
	sim.assign_characters()
	for id: int in sim.players:
		sim.equip(id, true)
	var still: Dictionary[int, int] = {}
	var longest: int = 0
	var stepped_out: int = 0
	for tick: int in 2700:
		if sim.phase == MatchSim.Phase.MATCH_OVER:
			break
		for coach: TeamCoach in [ours, theirs]:
			coach.drive(sim)
		var before: Dictionary[int, Vector2] = {}
		var held_in: Dictionary[int, bool] = {}
		for id: int in sim.players:
			var state: PlayerState = sim.players[id]
			before[id] = state.position
			var brain: TeamBrain = (ours if state.team == 0 else theirs)._brains[id]
			held_in[id] = brain.hold and not state.tagged and LAYOUT.in_base(-sim.side_for_team(state.team), state.position)
		sim.step(DT)
		if sim.phase != MatchSim.Phase.PLAYING:
			still.clear()
			continue
		for id: int in sim.players:
			var state: PlayerState = sim.players[id]
			var walked: bool = state.position.distance_to(before[id]) < 1.0  # a rescue or a new round puts a kid back at its spawn
			if held_in[id] and walked and not state.tagged and not LAYOUT.in_base(-sim.side_for_team(state.team), state.position):
				stepped_out += 1
			var idle: bool = not state.tagged and state.can_act() and state.cast_lock_left <= 0.0 \
				and state.position.distance_squared_to(before[id]) < 0.000001
			still[id] = (still.get(id, 0) + 1) if idle else 0
			longest = maxi(longest, still[id])
	assert_eq(stepped_out, 0, "a holder stays in the base it holds")
	assert_lt(longest, 45, "no free bot stands still for a second and a half (the longest was %d ticks)" % longest)
