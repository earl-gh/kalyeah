extends GutTest
## Where a kid cannot cast (docs/GDD.md, Rescue and Capture): standing in the enemy base (capturing it), or in its own team's rescue
## area while a teammate waits in the box (rescuing). Nowhere else. Both are data toggles.
## Seam: MatchSim (PlayerInput in, weapon_cast signal and elixir out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	sim.add_player(3, 1)
	sim.players[1].position = Vector2(0.0, 5.0)
	sim.players[2].position = Vector2(8.0, 10.0)
	sim.players[3].position = Vector2(0.0, -8.0)
	for id: int in [1, 2, 3]:
		sim.deal_cards(id)
		sim.players[id].elixir = _rules.elixir_max
	sim.players[1].weapons[0] = &"papel_trap"
	return sim


## Press card 1 for a tick, then release it: one cast attempt.
func _tap(sim: MatchSim) -> void:
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)


func _in_enemy_base(sim: MatchSim) -> void:
	sim.players[1].position = LAYOUT.base_center(-sim.side_for_team(0)) + Vector2(1.0, 0.0)


func _in_rescue_area(sim: MatchSim) -> void:
	var area: Rect2 = LAYOUT.rescue_rect(sim.side_for_team(0))
	var box: Rect2 = LAYOUT.jail_rect(sim.side_for_team(0))
	sim.players[1].position = Vector2((box.end.x + area.end.x) / 2.0, box.get_center().y)


func test_a_card_casts_in_the_open_street() -> void:
	var sim: MatchSim = _sim()
	watch_signals(sim)
	_tap(sim)
	assert_signal_emitted_with_parameters(sim, "weapon_cast", [1, &"papel_trap"])
	assert_false(sim.casting_blocked(sim.players[1]))


func test_a_kid_standing_in_the_enemy_base_cannot_cast_and_pays_nothing() -> void:
	var sim: MatchSim = _sim()
	_in_enemy_base(sim)
	watch_signals(sim)
	var before: float = sim.players[1].elixir
	assert_true(sim.casting_blocked(sim.players[1]))
	assert_false(sim.weapon_ready(sim.players[1], 0), "the card shows as not ready")
	_tap(sim)
	assert_signal_not_emitted(sim, "weapon_cast")
	assert_almost_eq(sim.players[1].elixir, minf(_rules.elixir_max, before), 0.001, "nothing spent")


func test_leaving_the_enemy_base_gives_the_cast_back() -> void:
	var sim: MatchSim = _sim()
	_in_enemy_base(sim)
	sim.step(DT)
	sim.players[1].position = Vector2(0.0, 5.0)
	watch_signals(sim)
	_tap(sim)
	assert_signal_emitted(sim, "weapon_cast")


func test_a_defender_at_home_can_cast_in_its_own_base() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].position = LAYOUT.base_center(sim.side_for_team(0)) + Vector2(1.0, 0.0)
	assert_false(sim.casting_blocked(sim.players[1]), "only the base you are invading blocks it")


func test_a_kid_in_its_rescue_area_cannot_cast_while_a_teammate_waits() -> void:
	var sim: MatchSim = _sim()
	sim.kill(2)
	_in_rescue_area(sim)
	watch_signals(sim)
	assert_true(sim.casting_blocked(sim.players[1]))
	_tap(sim)
	assert_signal_not_emitted(sim, "weapon_cast")


func test_the_rescue_area_is_only_a_rescue_area_while_someone_waits() -> void:
	var sim: MatchSim = _sim()
	_in_rescue_area(sim)
	assert_false(sim.casting_blocked(sim.players[1]), "nobody tagged: just ground")
	watch_signals(sim)
	_tap(sim)
	assert_signal_emitted(sim, "weapon_cast")


func test_the_enemy_standing_in_your_rescue_area_is_not_blocked() -> void:
	var sim: MatchSim = _sim()
	sim.kill(2)
	var area: Rect2 = LAYOUT.rescue_rect(sim.side_for_team(0))
	sim.players[3].position = area.get_center()
	assert_false(sim.casting_blocked(sim.players[3]), "they are hunting the rescuer")


func test_a_card_held_when_the_kid_walks_into_the_base_is_dropped() -> void:
	var sim: MatchSim = _sim()
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	assert_gte(sim.players[1].aim_hold[0], 0.0, "holding the card")
	_in_enemy_base(sim)
	watch_signals(sim)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	assert_lt(sim.players[1].aim_hold[0], 0.0, "the hold ends")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_signal_not_emitted(sim, "weapon_cast")


func test_with_the_toggles_off_both_places_let_the_kid_cast() -> void:
	_rules.no_cast_in_enemy_base = false
	_rules.no_cast_in_rescue_area = false
	var sim: MatchSim = _sim()
	sim.kill(2)
	_in_enemy_base(sim)
	assert_false(sim.casting_blocked(sim.players[1]))
	_in_rescue_area(sim)
	assert_false(sim.casting_blocked(sim.players[1]))
