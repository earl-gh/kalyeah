extends GutTest
## The jobs a TeamCoach gives single bots (owner, 2026-10-29): a rescuer picked among the bots (the nearest to the rescue area, even
## while the human is free), timed to the rescue area's countdown and holding the area once there; a guard sent home while the base is
## being taken; a pusher that attacks alone once it is safe. And how a bot picks whom to fight: the one taking its base, then the one
## rescuing, then a hurt one, then the nearest.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


## You (id 1) and two allies (2, 3) against three enemies (4, 5, 6); everyone armed.
func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 31)
	for id: int in [1, 2, 3]:
		sim.add_player(id, 0)
	for id: int in [4, 5, 6]:
		sim.add_player(id, 1)
	for id: int in sim.players:
		sim.set_loadout(id, &"bato_light", &"papel_shield")
	return sim


func _allies(sim: MatchSim) -> TeamCoach:
	var coach: TeamCoach = TeamCoach.new(0, 1)
	for id: int in [2, 3]:
		coach.add(sim, id, 0.4)
	return coach


func _enemies(sim: MatchSim) -> TeamCoach:
	var coach: TeamCoach = TeamCoach.new(1)
	for id: int in [4, 5, 6]:
		coach.add(sim, id, 0.4)
	return coach


func _run(sim: MatchSim, coaches: Array[TeamCoach], seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		for coach: TeamCoach in coaches:
			coach.drive(sim)
		sim.step(DT)


func _area(sim: MatchSim, team: int) -> Vector2:
	return LAYOUT.rescue_rect(sim.side_for_team(team)).get_center()


## The enemies far away at their own end, so nothing interrupts.
func _enemies_away(sim: MatchSim) -> void:
	for id: int in [4, 5, 6]:
		sim.players[id].position = LAYOUT.base_center(sim.side_for_team(1))


# ---- the rescuer ----------------------------------------------------------------------------

func test_an_ally_bot_rescues_a_tagged_ally_while_you_are_free() -> void:
	var sim: MatchSim = _sim()
	sim.rescue_ready_left = [0.0, 0.0]
	_enemies_away(sim)
	var coach: TeamCoach = _allies(sim)
	sim.kill(3)
	sim.players[2].position = _area(sim, 0) + Vector2(0.0, 4.0)
	assert_eq(coach.rescuer_id(sim), 2, "a bot, though you (id 1) are free and numbered lower")
	_run(sim, [coach] as Array[TeamCoach], 12.0)
	assert_false(sim.players[3].tagged, "bot 2 walked over and freed bot 3")


func test_the_rescuer_is_the_free_bot_nearest_the_rescue_area() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	sim.kill(4)
	sim.players[5].position = _area(sim, 1) + Vector2(0.0, 10.0)
	sim.players[6].position = _area(sim, 1) + Vector2(0.0, 2.0)
	assert_eq(coach.rescuer_id(sim), 6, "not the lowest number: the nearest")
	sim.objectives.rescue(sim, sim.players[4], 6)
	assert_eq(coach.rescuer_id(sim), -1, "nobody tagged, no rescuer")


func test_the_rescuer_waits_for_the_countdown_and_sets_off_to_arrive_as_it_ends() -> void:
	var sim: MatchSim = _sim()
	_enemies_away(sim)
	var coach: TeamCoach = _allies(sim)
	sim.kill(3)
	var start: Vector2 = _area(sim, 0) + Vector2(0.0, 6.0)
	sim.players[2].position = start
	sim.rescue_ready_left = [9.0, 9.0]
	coach.drive(sim)
	sim.step(DT)
	assert_true(sim.players[2].position.distance_to(_area(sim, 0)) >= start.distance_to(_area(sim, 0)) - 0.01,
		"9 s left and 1.2 s of walk: it does not go and stand in the area yet")
	sim.rescue_ready_left = [1.5, 1.5]
	_run(sim, [coach] as Array[TeamCoach], 0.5)
	assert_lt(sim.players[2].position.distance_to(_area(sim, 0)), start.distance_to(_area(sim, 0)), "now it sets off")


func test_a_rescuer_in_the_area_holds_it_when_an_enemy_comes_near() -> void:
	var sim: MatchSim = _sim()
	sim.rescue_ready_left = [0.0, 0.0]
	_enemies_away(sim)
	var coach: TeamCoach = _allies(sim)
	sim.kill(3)
	sim.players[2].position = _area(sim, 0)
	_run(sim, [coach] as Array[TeamCoach], 1.0)
	sim.players[4].position = _area(sim, 0) + Vector2(2.0, 0.0)  # right beside it: it cannot cast here anyway
	_run(sim, [coach] as Array[TeamCoach], 1.0)
	assert_true(sim.objectives.in_rescue_area(sim, sim.players[2]), "it stays in the area instead of stepping out to fight")


# ---- the guard and the pusher ------------------------------------------------------------------

func test_a_guard_goes_home_while_the_base_is_taken_during_an_attack() -> void:
	_rules.bot_assault_delay = 0.0
	_rules.bot_assault_window = 0.0
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	_run(sim, [coach] as Array[TeamCoach], 1.0)
	assert_eq(coach.plan, TeamCoach.Plan.ASSAULT)
	assert_eq(coach.guard_id(sim, -1), -1, "nobody is taking their base")
	sim.players[1].position = LAYOUT.base_center(sim.side_for_team(1))  # you walk into their base
	sim.step(DT)
	assert_eq(sim.capture_team, 0)
	var guard: int = coach.guard_id(sim, -1)
	assert_gt(guard, 0, "one of them is sent home")
	coach.drive(sim)
	assert_eq((coach._brains[guard] as TeamBrain).role, TeamBrain.Role.DEFEND)
	for id: int in [4, 5, 6]:
		if id != guard:
			assert_eq((coach._brains[id] as TeamBrain).role, TeamBrain.Role.RUSH, "the others keep attacking")


func test_an_ally_pushes_alone_once_most_enemies_are_tagged() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _allies(sim)
	coach.drive(sim)
	assert_eq(coach.plan, TeamCoach.Plan.DEFEND, "you are at home, so they defend")
	assert_eq(coach.pusher_id(sim, -1), -1, "every enemy is free: nobody pushes")
	sim.kill(4)
	assert_eq(coach.pusher_id(sim, -1), -1, "two of three free is not safe yet")
	sim.kill(5)
	var pusher: int = coach.pusher_id(sim, -1)
	assert_true(pusher == 2 or pusher == 3, "one ally goes")
	coach.drive(sim)
	assert_eq((coach._brains[pusher] as TeamBrain).role, TeamBrain.Role.RUSH)
	var other: int = 5 - pusher
	assert_eq((coach._brains[other] as TeamBrain).role, TeamBrain.Role.DEFEND, "the other stays home")


func test_a_team_without_a_human_never_has_a_pusher() -> void:
	var sim: MatchSim = _sim()
	var coach: TeamCoach = _enemies(sim)
	sim.kill(1)
	sim.kill(2)
	coach.drive(sim)
	assert_eq(coach.pusher_id(sim, -1), -1, "the enemies plan their attacks with the clock")


# ---- whom to fight ------------------------------------------------------------------------------

func _picker(sim: MatchSim, id: int) -> PlayerState:
	var brain: FightBrain = FightBrain.fighter(sim.players[id].position)
	return brain._pick_enemy(sim.players[id], sim, _rules.bot_aggro_range)


func test_the_enemy_taking_the_base_is_fought_before_a_nearer_one() -> void:
	var sim: MatchSim = _sim()
	var home: Vector2 = LAYOUT.base_center(sim.side_for_team(1))
	# the bot just outside its base, ally 2 right beside it (outside too), you in the middle of the base
	sim.players[4].position = home + Vector2(0.0, (LAYOUT.base_radius + 2.0) * float(-sim.side_for_team(1)))
	sim.players[2].position = sim.players[4].position + Vector2(0.0, 1.5 * float(-sim.side_for_team(1)))
	sim.players[1].position = home
	assert_false(LAYOUT.in_base(sim.side_for_team(1), sim.players[2].position))
	for id: int in [3, 5, 6]:
		sim.players[id].position = Vector2(100.0, 0.0)
	assert_eq(_picker(sim, 4).id, 1, "you, in their base, before ally 2 standing right beside them")


func test_a_rescuer_then_a_hurt_enemy_come_before_the_nearest() -> void:
	var sim: MatchSim = _sim()
	for id: int in [2, 3, 5, 6]:
		sim.players[id].position = Vector2(100.0, 0.0)
	var me: PlayerState = sim.players[4]
	me.position = Vector2(0.0, 0.0)
	sim.players[1].position = Vector2(2.0, 0.0)
	sim.players[2].position = Vector2(6.0, 0.0)
	sim.players[3].position = Vector2(-8.0, 0.0)
	assert_eq(_picker(sim, 4).id, 1, "nobody special: the nearest")
	sim.players[2].hp = 20
	assert_eq(_picker(sim, 4).id, 2, "a hurt one before the nearest")
	sim.rescue_rescuer[0] = 3
	assert_eq(_picker(sim, 4).id, 3, "the one rescuing before a hurt one")
