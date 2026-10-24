extends GutTest
## Elixir pays for cards (docs/GDD.md "Controls: tap to move, cards to cast" > Elixir; costs in the Weapons table).
## Seam: MatchSim (PlayerInput in, PlayerState and signals out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
## The GDD Weapons table, "Elixir" column (owner-approved transcription, audit 2026-10-24).
const COSTS: Dictionary[StringName, int] = {
	&"bato_light": 2, &"bato_heavy": 4, &"gunting_light": 2, &"gunting_heavy": 3, &"papel_trap": 4, &"papel_shield": 5,
	&"tsinelas_light": 3, &"tsinelas_heavy": 4, &"lata": 5, &"jacks": 4, &"bola": 4, &"trumpo": 5, &"heal": 4, &"langit_lupa": 5,
}

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(0.0, 5.0)
	sim.players[2].position = Vector2(0.0, -5.0)
	sim.deal_cards(1)
	sim.deal_cards(2)
	return sim


## Puts `weapon` in slot 0 of player 1's hand.
func _hold(sim: MatchSim, weapon: StringName) -> void:
	var state: PlayerState = sim.players[1]
	var at: int = state.weapons.find(weapon)
	if at >= 0:
		state.weapons[at] = state.weapons[0]
	elif state.next_card == weapon:
		state.next_card = state.weapons[0]
	else:
		state.deck[state.deck.find(weapon)] = state.weapons[0]
	state.weapons[0] = weapon


## Press slot 0 for one tick, then release (with `release_bits` on the release tick).
func _tap(sim: MatchSim, release_bits: int = 0) -> void:
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, release_bits, sim.tick))
	sim.step(DT)


func _steps(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		sim.step(DT)


func test_every_weapon_costs_its_gdd_elixir_from_one_to_the_bar() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	assert_eq(rules.weapons.size(), COSTS.size())
	for def: WeaponDef in rules.weapons:
		assert_between(def.elixir_cost, 1, int(rules.elixir_max), "%s costs 1..%d" % [def.id, int(rules.elixir_max)])
		assert_eq(def.elixir_cost, COSTS[def.id], "%s costs what the GDD table says" % def.id)


func test_a_player_starts_with_five_elixir() -> void:
	assert_almost_eq(_sim().players[1].elixir, 5.0, 0.0001)


## One second of fill, from the rules (tuned in bot rounds: docs/audits/2026-10-24-tap-move-elixir-stamina.md), not a pinned number.
func test_elixir_fills_at_the_rules_rate() -> void:
	var sim: MatchSim = _sim()
	_steps(sim, 30)
	assert_almost_eq(sim.players[1].elixir, 5.0 + _rules.elixir_regen_per_s, 0.001)


func test_elixir_stops_at_ten() -> void:
	var sim: MatchSim = _sim()
	_steps(sim, 30 * 20)
	assert_almost_eq(sim.players[1].elixir, 10.0, 0.0001, "the extra is lost")


func test_elixir_does_not_fill_during_the_opening_show() -> void:
	var sim: MatchSim = _sim()
	sim.begin_intro()
	_steps(sim, 30)
	assert_almost_eq(sim.players[1].elixir, 5.0, 0.0001)


func test_a_cast_spends_the_cards_cost() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"lata")
	sim.players[1].elixir = 6.0
	_tap(sim)
	assert_almost_eq(sim.players[1].elixir, 1.0 + _rules.elixir_regen_per_s * 2.0 * DT, 0.001, "lata costs 5 (plus two ticks of fill)")


func test_a_card_above_the_bar_cannot_be_picked_up_or_cast() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"lata")
	var state: PlayerState = sim.players[1]
	state.elixir = 4.0
	watch_signals(sim)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	assert_lt(state.aim_hold[0], 0.0, "an unaffordable card is not picked up")
	assert_false(sim.can_afford(state, 0))
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_signal_not_emitted(sim, "weapon_cast")
	assert_eq(state.weapons[0], &"lata", "the card keeps its slot")


func test_holding_an_unaffordable_card_while_the_bar_fills_casts_nothing() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"lata")
	var state: PlayerState = sim.players[1]
	state.elixir = 4.9
	watch_signals(sim)
	for i: int in 30:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
		sim.step(DT)
	assert_gt(state.elixir, 5.0, "the bar reached the cost while the card was held")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_signal_not_emitted(sim, "weapon_cast", "no precasting: it needs a fresh touch")


func test_a_cancel_spends_nothing() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"lata")
	watch_signals(sim)
	_tap(sim, PlayerInput.BTN_AIM_CANCEL)
	assert_signal_not_emitted(sim, "weapon_cast")
	assert_almost_eq(sim.players[1].elixir, 5.0 + 2.0 * 0.6 / 30.0, 0.0001)
	assert_eq(sim.players[1].weapons[0], &"lata")


func test_a_release_that_casts_nothing_spends_nothing() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"bato_light")
	sim.players[2].position = Vector2(0.0, -20.0)
	watch_signals(sim)
	_tap(sim)
	assert_signal_not_emitted(sim, "weapon_cast", "a homing rock with nobody in range throws nothing")
	assert_almost_eq(sim.players[1].elixir, 5.0 + 2.0 * 0.6 / 30.0, 0.0001)
	assert_eq(sim.players[1].weapons[0], &"bato_light")


func test_with_elixir_no_card_plays_a_cooldown() -> void:
	var sim: MatchSim = _sim()
	_hold(sim, &"bato_heavy")
	_tap(sim)
	for cooldown: float in sim.players[1].weapon_cooldowns:
		assert_eq(cooldown, 0.0)
	assert_eq(sim.players[1].shuffle_card, &"", "no shuffler")


func test_elixir_keeps_filling_while_tagged_and_a_rescue_keeps_it() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(3, 0)
	sim.deal_cards(3)
	sim.players[1].elixir = 2.0
	sim.kill(1)
	_steps(sim, 30)
	var filled: float = 2.0 + _rules.elixir_regen_per_s
	assert_almost_eq(sim.players[1].elixir, filled, 0.001)
	sim.objectives.rescue(sim, sim.players[1], 3)
	assert_almost_eq(sim.players[1].elixir, filled, 0.001, "a rescue keeps what you have")


func _play_a_point(sim: MatchSim) -> void:
	sim.kill(2)
	for i: int in 30 * 12:
		sim.step(DT)
		if sim.phase == MatchSim.Phase.PLAYING and sim.score.points[0] > 0:
			return


func test_a_new_round_starts_everyone_at_five_elixir() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].elixir = 9.0
	_play_a_point(sim)
	assert_eq(sim.score.points[0], 1)
	assert_almost_eq(sim.players[1].elixir, 5.0, 0.05)


func test_with_the_round_reset_off_a_new_round_keeps_the_elixir() -> void:
	_rules.elixir_reset_on_round = false
	var sim: MatchSim = _sim()
	sim.players[1].elixir = 9.0
	_play_a_point(sim)
	assert_gt(sim.players[1].elixir, 9.0)
