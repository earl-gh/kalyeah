extends GutTest
## Stamina and overclock (docs/GDD.md "Controls: tap to move, cards to cast" > Stamina): running drains the bar; at 0 the kid is
## overclocked (owner): it only walks until the bar is full again, and the bar blinks red for two seconds before it refills.
## Seam: MatchSim (PlayerInput in, stamina / overclocked / exhaust_left / position of PlayerState and the `exhausted` signal out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.stamina_enabled = true


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(-12.0, 0.0)
	sim.players[2].position = Vector2(12.0, -20.0)
	sim.equip(1)
	sim.equip(2)
	return sim


## `ticks` ticks walking back and forth across the street with WASD (never blocked).
func _walk(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		var way: float = 1.0 if int(i / 60) % 2 == 0 else -1.0
		sim.set_input(1, PlayerInput.create(Vector2(way, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func _stand(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func _exhaust(sim: MatchSim) -> void:
	sim.players[1].stamina = 1.0
	_walk(sim, 4)


func test_a_kid_starts_with_a_full_bar() -> void:
	assert_eq(_sim().players[1].stamina, _rules.stamina_max)


func test_moving_drains_ten_a_second() -> void:
	var sim: MatchSim = _sim()
	_walk(sim, 30)
	assert_almost_eq(sim.players[1].stamina, 90.0, 0.001)


func test_walking_to_a_move_target_drains_too() -> void:
	var sim: MatchSim = _sim()
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(12.0, 0.0)))
	sim.step(DT)
	_stand(sim, 29)
	assert_almost_eq(sim.players[1].stamina, 90.0, 0.001)


func test_standing_still_refills_25_a_second_after_half_a_second() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].stamina = 40.0
	_stand(sim, 15)
	assert_almost_eq(sim.players[1].stamina, 40.0, 0.001, "nothing for the first 0.5 s")
	_stand(sim, 30)
	assert_almost_eq(sim.players[1].stamina, 65.0, 0.001, "then 25 a second")
	_stand(sim, 30 * 3)
	assert_eq(sim.players[1].stamina, _rules.stamina_max, "up to the max")


func test_any_movement_restarts_the_wait() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].stamina = 40.0
	for i: int in 20:
		_stand(sim, 14)
		_walk(sim, 1)
	assert_lt(sim.players[1].stamina, 40.0, "stutter-stepping never refills")


## Metres a kid on its own at `ticks` ticks of walking to a far point covers.
func _walked(sim: MatchSim, ticks: int) -> float:
	var state: PlayerState = sim.players[1]
	var from: Vector2 = state.position
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(12.0, 0.0)))
	for i: int in ticks:
		sim.step(DT)
	return state.position.distance_to(from)


func test_at_zero_the_kid_is_overclocked_and_keeps_walking_at_half_speed() -> void:
	var sim: MatchSim = _sim()
	watch_signals(sim)
	sim.players[1].stamina = 1.0
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(12.0, 0.0)))
	for i: int in 5:
		sim.step(DT)
	var state: PlayerState = sim.players[1]
	assert_signal_emitted_with_parameters(sim, "exhausted", [1])
	assert_true(state.overclocked, "stamina ran out")
	assert_true(state.has_move_target, "it keeps its walk")
	var from: Vector2 = state.position
	sim.step(DT)
	assert_almost_eq(state.position.distance_to(from), _rules.move_speed * _rules.overclock_speed_share * DT, 0.001, "at walking pace")
	assert_eq(_rules.overclock_speed_share, 0.5)


func test_the_bar_stays_empty_and_blinks_for_two_seconds_before_it_refills() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	var state: PlayerState = sim.players[1]
	assert_almost_eq(state.exhaust_left, _rules.exhaust_seconds, 2.0 * DT, "the blink runs two seconds (owner)")
	assert_eq(_rules.exhaust_seconds, 2.0)
	_walk(sim, roundi(_rules.exhaust_seconds / DT) - 3)
	assert_eq(state.stamina, 0.0, "no refill during the blink")
	assert_true(state.overclocked)
	assert_gt(state.exhaust_left, 0.0)
	_walk(sim, 6)
	assert_eq(state.exhaust_left, 0.0, "the blink is over")
	assert_gt(state.stamina, 0.0, "now it refills")
	assert_true(state.overclocked, "and the kid still only walks")


func test_the_kid_walks_until_the_bar_is_full_again_then_runs() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	var state: PlayerState = sim.players[1]
	var ticks: int = 0
	while state.overclocked and ticks < 600:
		_walk(sim, 1)
		ticks += 1
	assert_false(state.overclocked, "back to running")
	assert_eq(state.stamina, _rules.stamina_max, "with a full bar")
	var seconds: float = float(ticks) * DT
	assert_almost_eq(seconds, _rules.exhaust_seconds + _rules.stamina_max / _rules.stamina_regen_per_s, 0.3, "two seconds of blink, then 100 at 25 a second")
	state.position = Vector2(-12.0, 0.0)
	assert_almost_eq(_walked(sim, 10), _rules.move_speed * 10.0 * DT, 0.01, "full run speed again")


func test_walking_while_overclocked_costs_no_stamina() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	var state: PlayerState = sim.players[1]
	var previous: float = state.stamina
	for i: int in 200:
		_walk(sim, 1)
		if not state.overclocked:
			break
		assert_gte(state.stamina, previous - 0.0001, "never drains")
		previous = state.stamina


func test_an_overclocked_kid_can_still_cast() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	var state: PlayerState = sim.players[1]
	state.elixir = _rules.elixir_max
	state.weapons[0] = &"papel_trap"
	watch_signals(sim)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_true(state.overclocked)
	assert_signal_emitted_with_parameters(sim, "weapon_cast", [1, &"papel_trap"])


func test_a_slow_or_stun_never_overwrites_the_overclock() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	var state: PlayerState = sim.players[1]
	var left: float = state.exhaust_left
	sim.apply_effect(1, StatusEffects.Type.SLOW, 5.0, 0.4)
	sim.apply_effect(1, StatusEffects.Type.STUN, 0.2, 0.0)
	assert_eq(state.exhaust_left, left, "statuses do not touch the blink timer")
	assert_true(state.overclocked)
	_stand(sim, 10)
	assert_false(state.effects.has(StatusEffects.Type.STUN), "the stun ran out")
	assert_almost_eq(state.exhaust_left, left - 10.0 * DT, 0.0001, "the blink runs on its own clock")


func test_a_stun_still_stops_an_overclocked_kid() -> void:
	var sim: MatchSim = _sim()
	_exhaust(sim)
	sim.players[1].effects.immunity_length = 0.0
	sim.apply_effect(1, StatusEffects.Type.STUN, 3.0, 0.0)
	var state: PlayerState = sim.players[1]
	var at: Vector2 = state.position
	_walk(sim, 10)
	assert_eq(state.position, at, "stunned")


func test_a_new_round_and_a_rescue_refill_the_bar() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(3, 0)
	sim.equip(3)
	_exhaust(sim)
	sim.kill(1)
	sim.objectives.rescue(sim, sim.players[1], 3)
	assert_eq(sim.players[1].stamina, _rules.stamina_max, "full after a rescue")
	assert_eq(sim.players[1].exhaust_left, 0.0)
	assert_false(sim.players[1].overclocked, "and not overclocked")
	_exhaust(sim)
	sim.kill(2)
	for i: int in 30 * 12:
		sim.step(DT)
		if sim.score.points[0] > 0 and sim.phase == MatchSim.Phase.PLAYING:
			break
	assert_eq(sim.score.points[0], 1)
	assert_eq(sim.players[1].stamina, _rules.stamina_max, "full at a round start")
	assert_eq(sim.players[1].exhaust_left, 0.0)
	assert_false(sim.players[1].overclocked)


func test_training_dummies_have_no_stamina() -> void:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_dummy(9, 1, Vector2(5.0, 0.0), DummyBrain.patrolling(5.0, 3.0, 1.0))
	for i: int in 30 * 30:
		sim.step(DT)
	assert_false(sim.players[9].has_stamina)
	assert_eq(sim.players[9].exhaust_left, 0.0)
	assert_eq(sim.players[9].stamina, _rules.stamina_max, "they patrol for ever")


func test_with_stamina_off_nothing_drains() -> void:
	_rules.stamina_enabled = false
	var sim: MatchSim = _sim()
	_walk(sim, 30 * 12)
	assert_eq(sim.players[1].stamina, _rules.stamina_max)
	assert_eq(sim.players[1].exhaust_left, 0.0)
