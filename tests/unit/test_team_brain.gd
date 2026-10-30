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


## Where a bot's input walks it, as a unit direction (zero: it stands). Bots walk by move targets now, not a stick
## (docs/GDD.md "Controls: tap to move, cards to cast" > Bots and the coach play by the same rules).
func _way(input: PlayerInput, state: PlayerState) -> Vector2:
	var offset: Vector2 = input.move_target - state.position if input.is_pressed(PlayerInput.BTN_MOVE_TARGET) else input.move
	return offset.normalized() if offset.length() > 0.001 else Vector2.ZERO


## Any hand-slot bit (a card held).
func _card_bits(input: PlayerInput) -> int:
	return input.buttons & (PlayerInput.BTN_CARD_1 | PlayerInput.BTN_CARD_2 | PlayerInput.BTN_CARD_3 | PlayerInput.BTN_CARD_4)


func _spot(sim: MatchSim) -> Vector2:
	return LAYOUT.spawn_center(-sim.side_for_team(0))


func test_a_rusher_walks_toward_the_enemy_base() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	var way: Vector2 = (_spot(sim) - sim.players[1].position).normalized()
	assert_gt(_way(input, sim.players[1]).dot(way), 0.99)


func test_a_rusher_that_has_arrived_walks_about_in_the_enemy_base_instead_of_standing() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.players[1].position = _spot(sim)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_true(input.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_true(LAYOUT.in_base(-sim.side_for_team(0), input.move_target), "its next spot is in the enemy base too")


func test_a_defender_holds_its_post_until_an_enemy_comes() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.players[1].position = sim.players[1].spawn_position + Vector2(3.0, 0.0)
	var brain: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_lt(_way(input, sim.players[1]).dot(Vector2(1.0, 0.0)), -0.99, "it walks back to its post")


func test_a_rusher_that_meets_an_enemy_stops_and_fights_it_then_goes_on() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].position = Vector2.ZERO
	sim.players[3].position = Vector2(0.0, -4.0)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var pressed: bool = false
	for i: int in 6:
		var input: PlayerInput = brain.think(sim.players[1], sim)
		if _card_bits(input) != 0:
			pressed = true
			break
	assert_true(pressed, "an enemy in reach is thrown at")
	var after: PlayerInput = brain.think(sim.players[1], sim)
	assert_eq(_card_bits(after), 0, "the second half of the tap lets go")
	sim.players[3].position = Vector2(100.0, 100.0)
	var next: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(_way(next, sim.players[1]).dot((_spot(sim) - sim.players[1].position).normalized()), 0.99, "with the enemy gone it goes on toward their base")


func test_priority_one_an_enemy_in_reach_is_fought_before_the_base_and_before_a_rescue() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	# player 1 stands just outside the enemy base, a teammate is tagged, and an enemy stands 10 m further out
	var centre: Vector2 = LAYOUT.base_center(-sim.side_for_team(0))
	var out: Vector2 = (Vector2.ZERO - centre).normalized()
	sim.players[1].position = centre + out * (LAYOUT.base_radius + 1.5)
	var behind: Vector2 = sim.players[1].position + out * 10.0
	sim.players[3].position = behind
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(_way(input, sim.players[1]).dot(out), 0.9, "it closes on the enemy instead of holding the base or leaving for the box")


func test_in_the_enemy_base_nothing_can_be_cast_so_a_rusher_steps_out_to_fight() -> void:
	var sim: MatchSim = _sim()
	var centre: Vector2 = LAYOUT.base_center(-sim.side_for_team(0))
	var out: Vector2 = (Vector2.ZERO - centre).normalized()
	sim.players[1].position = centre + out * 1.0
	sim.players[3].position = centre + out * 9.0
	assert_true(sim.casting_blocked(sim.players[1]), "inside the base it cannot cast")
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(_way(input, sim.players[1]).dot(out), 0.9, "it walks out of the base toward the fight")


func test_a_tired_bot_in_a_fight_keeps_moving_it_never_stands_to_refill() -> void:
	var sim: MatchSim = _sim()
	var brain: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	sim.players[1].position = Vector2(0.0, 6.0)
	sim.players[1].elixir = 0.0
	sim.players[1].weapon_cooldowns.fill(99.0)  # nothing to cast: it holds its distance
	var standoff: float = brain._standoff(sim.players[1], sim)
	sim.players[3].position = Vector2(0.0, 6.0 - standoff)
	sim.players[1].stamina = 1.0
	var tired: PlayerInput = brain.think(sim.players[1], sim)
	assert_true(tired.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_gt(tired.move_target.distance_to(sim.players[1].position), 0.5, "low on stamina it still strafes (running out only slows it to a walk)")


func test_priority_two_the_base_comes_before_a_rescue_so_a_rusher_not_there_yet_keeps_going() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(_way(input, sim.players[1]).dot((_spot(sim) - sim.players[1].position).normalized()), 0.99, "to their base first, though a teammate waits")


func test_priority_three_from_the_enemy_base_the_lowest_numbered_free_rusher_fetches_a_tagged_teammate() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var area: Vector2 = LAYOUT.rescue_rect(sim.side_for_team(0)).get_center()
	sim.players[1].position = _spot(sim)
	sim.players[2].position = _spot(sim)
	var first: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	var second: TeamBrain = TeamBrain.rusher(sim.players[2].spawn_position, _spot(sim))
	var one: PlayerInput = first.think(sim.players[1], sim)
	var two: PlayerInput = second.think(sim.players[2], sim)
	assert_gt(_way(one, sim.players[1]).dot((area - sim.players[1].position).normalized()), 0.99, "number 1 goes to the rescue area")
	assert_true(LAYOUT.in_base(-sim.side_for_team(0), two.move_target), "number 2 stays in the base, walking about in it")


func test_a_rescue_that_has_started_is_not_called_off_by_leaving_the_base() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var area: Vector2 = LAYOUT.rescue_rect(sim.side_for_team(0)).get_center()
	sim.players[1].position = _spot(sim)
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	brain.think(sim.players[1], sim)
	sim.players[1].position = (_spot(sim) + area) / 2.0
	var input: PlayerInput = brain.think(sim.players[1], sim)
	assert_gt(_way(input, sim.players[1]).dot((area - sim.players[1].position).normalized()), 0.99, "it keeps going to the rescue area")


func test_a_defender_at_its_post_fetches_a_tagged_teammate_only_from_the_post() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var area: Vector2 = LAYOUT.rescue_rect(sim.side_for_team(0)).get_center()
	var away: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	sim.players[1].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	var wandering: PlayerInput = away.think(sim.players[1], sim)
	assert_gt(_way(wandering, sim.players[1]).dot((sim.players[1].spawn_position - sim.players[1].position).normalized()), 0.99, "a defender out of its base goes home first")
	var home: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	sim.players[1].position = sim.players[1].spawn_position
	var input: PlayerInput = home.think(sim.players[1], sim)
	assert_gt(_way(input, sim.players[1]).dot((area - sim.players[1].position).normalized()), 0.99, "from its post it goes for the rescue area")


func test_bots_walk_to_the_rescue_area_and_free_a_tagged_teammate() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	sim.add_player(4, 0)
	sim.set_loadout(4, &"bato_light", &"papel_shield")
	sim.kill(4)
	var brains: Dictionary[int, TeamBrain] = {
		1: TeamBrain.defender(sim.players[1].spawn_position), 2: TeamBrain.defender(sim.players[2].spawn_position),
	}
	var freed: bool = false
	for i: int in 30 * 40:
		for id: int in brains:
			sim.set_input(id, brains[id].think(sim.players[id], sim))
		sim.step(DT)
		if not sim.players[4].tagged:
			freed = true
			break
	assert_true(freed, "a bot stood in the rescue area for the hold time and freed the kid on the sidewalk")


func test_a_bot_waiting_at_its_post_walks_about_in_its_base_and_never_stands() -> void:
	var sim: MatchSim = _sim()
	sim.players[3].position = Vector2(100.0, 100.0)
	var home: int = sim.side_for_team(0)
	var brain: TeamBrain = TeamBrain.defender(sim.players[1].spawn_position)
	var state: PlayerState = sim.players[1]
	state.position = LAYOUT.base_center(home)
	var spots: Dictionary[Vector2, bool] = {}
	for i: int in 240:
		var input: PlayerInput = brain.think(state, sim)
		assert_true(input.is_pressed(PlayerInput.BTN_MOVE_TARGET))
		assert_gt(input.move_target.distance_to(state.position), 0.05, "tick %d: it has somewhere to go" % i)
		assert_true(LAYOUT.in_base(home, input.move_target), "and stays in its own base")
		spots[input.move_target] = true
		state.position = state.position.move_toward(input.move_target, 5.0 * DT)
	assert_gt(spots.size(), 2, "over eight seconds it walks to several places")


func test_a_base_holder_stays_in_the_enemy_base_even_with_an_enemy_next_to_it() -> void:
	var sim: MatchSim = _sim()
	var enemy_side: int = -sim.side_for_team(0)
	var centre: Vector2 = LAYOUT.base_center(enemy_side)
	var out: Vector2 = (Vector2.ZERO - centre).normalized()
	sim.players[1].position = centre
	sim.players[3].position = centre + out * 5.0
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	brain.hold = true
	for i: int in 90:
		var input: PlayerInput = brain.think(sim.players[1], sim)
		assert_true(LAYOUT.in_base(enemy_side, input.move_target), "tick %d: it never sets off out of the base" % i)
		sim.players[1].position = sim.players[1].position.move_toward(input.move_target, 5.0 * DT)
		assert_true(LAYOUT.in_base(enemy_side, sim.players[1].position))


func test_a_fight_does_not_walk_into_the_enemy_base_it_goes_along_its_edge() -> void:
	var sim: MatchSim = _sim()
	var enemy_side: int = -sim.side_for_team(0)
	var centre: Vector2 = LAYOUT.base_center(enemy_side)
	var out: Vector2 = (Vector2.ZERO - centre).normalized()
	# the enemy stands inside the base, 3 m in; the rusher stands at the edge with nothing to cast
	var edge: Vector2 = centre + out * (LAYOUT.base_radius + 0.3)
	sim.players[1].position = edge
	sim.players[1].elixir = 0.0
	sim.players[1].weapon_cooldowns.fill(99.0)
	sim.players[3].position = centre
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	for i: int in 60:
		var input: PlayerInput = brain.think(sim.players[1], sim)
		var step: Vector2 = input.move_target - sim.players[1].position
		sim.players[1].position += step.limit_length(5.0 * DT)
		assert_false(sim.casting_blocked(sim.players[1]), "tick %d: it stays out of the place it cannot cast in" % i)


func test_after_stepping_out_to_fight_a_rusher_waits_outside_before_going_back_in() -> void:
	var sim: MatchSim = _sim()
	var enemy_side: int = -sim.side_for_team(0)
	var centre: Vector2 = LAYOUT.base_center(enemy_side)
	var out: Vector2 = (Vector2.ZERO - centre).normalized()
	sim.players[1].position = centre + out * 1.0
	sim.players[3].position = centre + out * 9.0
	var brain: TeamBrain = TeamBrain.rusher(sim.players[1].spawn_position, _spot(sim))
	brain.think(sim.players[1], sim)  # steps out
	sim.players[1].position = centre + out * (LAYOUT.base_radius + 1.0)
	sim.players[3].position = Vector2(100.0, 100.0)  # the enemy is gone
	for i: int in 20:  # well under REENTER_DELAY
		var input: PlayerInput = brain.think(sim.players[1], sim)
		assert_false(LAYOUT.in_base(enemy_side, input.move_target), "tick %d: it does not walk straight back into the base" % i)
	for i: int in 90:
		brain.think(sim.players[1], sim)
	assert_true(LAYOUT.in_base(enemy_side, brain.think(sim.players[1], sim).move_target), "later it goes back in")
