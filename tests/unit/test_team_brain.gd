extends GutTest
## TeamBrain: a rusher walks into the enemy base and fetches a tagged teammate when it is the one to do so; a defender holds its post.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 9)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	sim.add_player(3, 1)
	sim.set_loadout(1, &"bato_light", &"papel_shield")
	sim.set_loadout(2, &"bato_light", &"papel_shield")
	sim.set_loadout(3, &"bato_light", &"papel_shield")
	return sim


func _spot(sim: MatchSim) -> Vector2:
	return LAYOUT.spawn_center(-sim.side_for_team(0))


func test_a_rusher_walks_toward_the_enemy_base() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	var way: Vector2 = (_spot(sim) - sim.players[1].position).normalized()
	assert_gt(input.move.dot(way), 0.99)


func test_a_rusher_that_has_arrived_stands_still() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.players[1].position = _spot(sim)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	assert_eq(brain.think(sim.players[1], sim).move, Vector2.ZERO)


func test_a_defender_holds_its_post_until_an_enemy_comes() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.players[1].position = sim.players[1].spawn_position + Vector2(3.0, 0.0)
	var brain: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_lt(input.move.dot(Vector2(1.0, 0.0)), -0.99, "it walks back to its post")


func test_a_rusher_that_meets_an_enemy_stops_and_fights_it_then_goes_on() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].position = Vector2.ZERO
	sim.players[3].position = Vector2(0.0, -4.0)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var pressed: bool = false
	for i: int in 6:
		var input: PlayerInput = brain.think(sim.players[1], sim)
		if input.buttons != 0:
			pressed = true
			break
	assert_true(pressed, "an enemy in reach is thrown at")
	var after: PlayerInput = brain.think(sim.players[1], sim)
	assert_eq(after.buttons, 0, "the second half of the tap lets go")
	sim.players[3].position = Vector2(100.0, 100.0)
	var next: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(next.move.dot((_spot(sim) - sim.players[1].position).normalized()), 0.99, "with the enemy gone it goes on toward their base")


func test_priority_one_an_enemy_in_reach_is_fought_before_the_base_and_before_a_rescue() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	# player 1 stands in the enemy base (its post), a teammate is tagged, and an enemy stands 10 m behind it
	sim.players[1].position = _spot(sim)
	var box: Vector2 = LAYOUT.jail_rect(sim.side_for_team(0)).get_center()
	var behind: Vector2 = sim.players[1].position - (box - sim.players[1].position).normalized() * 10.0
	sim.players[3].position = behind
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	var way: Vector2 = (behind - sim.players[1].position).normalized()
	assert_gt(input.move.dot(way), 0.9, "it closes on the enemy instead of holding the base or leaving for the box")


func test_priority_two_the_base_comes_before_a_rescue_so_a_rusher_not_there_yet_keeps_going() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(input.move.dot((_spot(sim) - sim.players[1].position).normalized()), 0.99, "to their base first, though a teammate waits")


func test_priority_three_from_the_enemy_base_the_lowest_numbered_free_rusher_fetches_a_tagged_teammate() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var box: Vector2 = LAYOUT.jail_rect(sim.side_for_team(0)).get_center()
	sim.players[1].position = _spot(sim)
	sim.players[2].position = _spot(sim)
	var first: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var second: TeamBrain = TeamBrain.rusher(sim.players[2].spawn_position, _spot(sim))
	var one: PlayerInput = first.think(sim.players[1], sim)
	var two: PlayerInput = second.think(sim.players[2], sim)
	assert_gt(one.move.dot((box - sim.players[1].position).normalized()), 0.99, "number 1 goes to the box")
	assert_lt(two.move.length(), 0.5, "number 2 stays holding the base")


func test_a_rescue_that_has_started_is_not_called_off_by_leaving_the_base() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var box: Vector2 = LAYOUT.jail_rect(sim.side_for_team(0)).get_center()
	sim.players[1].position = _spot(sim)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	brain.think(sim.players[1], sim)
	sim.players[1].position = (_spot(sim) + box) / 2.0
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(input.move.dot((box - sim.players[1].position).normalized()), 0.99, "it keeps going to the box")


func test_a_defender_at_its_post_fetches_a_tagged_teammate_only_from_the_post() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var box: Vector2 = LAYOUT.jail_rect(sim.side_for_team(0)).get_center()
	var away: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	sim.players[1].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	var wandering: PlayerInput = away.think(sim.players[1], sim)
	assert_gt(wandering.move.dot((sim.players[1].spawn_position - sim.players[1].position).normalized()), 0.99, "a defender out of its base goes home first")
	var home: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	sim.players[1].position = sim.players[1].spawn_position
	var input: PlayerInput = home.think(sim.players[1], sim)
	assert_gt(input.move.dot((box - sim.players[1].position).normalized()), 0.99, "from its post it goes for the box")
