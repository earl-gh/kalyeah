extends GutTest
## Bots play by the players' rules (docs/GDD.md "Controls: tap to move, cards to cast" > Bots and the coach play by the same rules):
## move targets, only affordable cards from a dealt hand of 4, released at a street point, and a stamina rest when no enemy is near.
## Seam: TeamBrain.think / MatchSim.step.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true
	_rules.stamina_enabled = true


## Bot 1 (team 0, a rusher) and enemy 2 far away unless moved; both real players with a dealt hand.
func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 9)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.equip(1)
	sim.equip(2)
	sim.players[2].position = Vector2(12.0, -22.0)
	return sim


func _rusher(sim: MatchSim) -> TeamBrain:
	return TeamBrain.rusher(sim.players[1].spawn_position, LAYOUT.spawn_center(-sim.side_for_team(0)))


func test_a_bot_walks_by_move_targets_not_a_stick() -> void:
	var sim: MatchSim = _sim()
	var input: PlayerInput = _rusher(sim).think(sim.players[1], sim)
	assert_true(input.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_eq(input.move, Vector2.ZERO)


func test_a_tired_bot_with_no_enemy_near_stands_to_refill_then_walks_on() -> void:
	var sim: MatchSim = _sim()
	var brain: TeamBrain = _rusher(sim)
	var state: PlayerState = sim.players[1]
	state.position = Vector2(0.0, 5.0)
	state.stamina = _rules.stamina_max * (_rules.bot_stamina_rest - 0.05)
	var resting: PlayerInput = brain.think(state, sim)
	assert_eq(resting.move_target, state.position, "below the rest share it stands")
	state.stamina = _rules.stamina_max * (_rules.bot_stamina_resume - 0.05)
	assert_eq(brain.think(state, sim).move_target, state.position, "and keeps standing until the resume share")
	state.stamina = _rules.stamina_max * _rules.bot_stamina_resume
	assert_ne(brain.think(state, sim).move_target, state.position, "then walks on")


func test_a_tired_bot_still_fights_an_enemy_in_reach() -> void:
	var sim: MatchSim = _sim()
	var brain: TeamBrain = _rusher(sim)
	var state: PlayerState = sim.players[1]
	state.position = Vector2(0.0, 5.0)
	state.stamina = 1.0
	state.elixir = _rules.elixir_max
	sim.players[2].position = Vector2(0.0, 1.0)
	var cards: int = 0
	for i: int in 4:
		cards |= brain.think(state, sim).buttons & (PlayerInput.BTN_CARD_1 | PlayerInput.BTN_CARD_2 | PlayerInput.BTN_CARD_3 | PlayerInput.BTN_CARD_4)
	assert_ne(cards, 0, "an enemy within bot_aggro_range is fought, tired or not")


func test_a_bot_never_picks_up_a_card_it_cannot_afford() -> void:
	var sim: MatchSim = _sim()
	var brain: TeamBrain = _rusher(sim)
	var state: PlayerState = sim.players[1]
	state.position = Vector2(0.0, 5.0)
	sim.players[2].position = Vector2(0.0, 2.0)
	state.elixir = 1.0
	_rules.elixir_regen_per_s = 0.0
	var casts: Array[int] = []
	sim.weapon_cast.connect(func(id: int, _weapon: StringName) -> void: casts.append(id))
	for i: int in 60:
		sim.set_input(1, brain.think(state, sim))
		sim.step(DT)
	assert_false(casts.has(1), "1 elixir buys no card (the cheapest costs 2)")


func test_a_bot_releases_a_ground_card_at_a_street_point() -> void:
	var sim: MatchSim = _sim()
	var brain: TeamBrain = _rusher(sim)
	var state: PlayerState = sim.players[1]
	state.position = Vector2(0.0, 5.0)
	state.elixir = _rules.elixir_max
	state.weapons.assign([&"lata", &"lata", &"lata", &"lata"] as Array[StringName])
	sim.players[2].position = Vector2(0.0, 1.0)
	var release: PlayerInput = null
	for i: int in 6:
		var input: PlayerInput = brain.think(state, sim)
		if input.is_pressed(PlayerInput.BTN_AIM_POINT):
			release = input
			break
	assert_not_null(release, "the release carries an aim point")
	if release != null:
		assert_lt(release.aim_point.distance_to(sim.players[2].position), 2.0, "near the enemy")
