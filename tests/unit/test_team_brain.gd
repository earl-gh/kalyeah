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


func test_a_rusher_casts_on_the_way_and_then_goes_on() -> void:
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
	var next: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(next.move.length(), 0.5, "then it walks on toward their base")


func test_the_lowest_numbered_free_rusher_fetches_a_tagged_teammate_and_the_other_keeps_going() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var box: Vector2 = LAYOUT.jail_rect(sim.side_for_team(0)).get_center()
	var first: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var second: TeamBrain = TeamBrain.rusher(sim.players[2].spawn_position, _spot(sim))
	var one: PlayerInput = first.think(sim.players[1], sim)
	var two: PlayerInput = second.think(sim.players[2], sim)
	assert_gt(one.move.dot((box - sim.players[1].position).normalized()), 0.99, "number 1 goes to the box")
	assert_gt(two.move.dot((_spot(sim) - sim.players[2].position).normalized()), 0.99, "number 2 keeps rushing")
