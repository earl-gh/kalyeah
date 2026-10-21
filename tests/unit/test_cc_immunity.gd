extends GutTest
## After a hard crowd control ends (stun, airborne, bounce, knock-out, polymorph) the player is immune to a new one for
## `cc_immunity` seconds, so a team cannot lock one kid out of the round with back-to-back stuns. Slows are not affected,
## and damage still lands.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 6)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	return sim


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func test_the_shipped_immunity_is_one_second() -> void:
	assert_eq(_rules.cc_immunity, 1.0)


func test_a_second_stun_right_after_the_first_ends_is_ignored() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 1.0, 0.0)
	_wait(sim, 1.1)
	assert_false(sim.players[2].effects.has(StatusEffects.Type.STUN))
	sim.apply_effect(2, StatusEffects.Type.STUN, 2.0, 0.0)
	assert_false(sim.players[2].effects.has(StatusEffects.Type.STUN), "immune for a second")


func test_a_different_hard_cc_is_ignored_too_and_the_signal_does_not_fire() -> void:
	var sim: MatchSim = _sim()
	var seen: Array[int] = []
	sim.effect_applied.connect(func(_id: int, type: int) -> void: seen.append(type))
	sim.apply_effect(2, StatusEffects.Type.AIRBORNE, 0.5, 0.0)
	_wait(sim, 0.6)
	sim.apply_effect(2, StatusEffects.Type.POLYMORPH, 2.0, 0.0)
	assert_false(sim.players[2].effects.has(StatusEffects.Type.POLYMORPH))
	assert_eq(seen, [StatusEffects.Type.AIRBORNE] as Array[int], "only the first one was announced")


func test_the_immunity_wears_off() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 0.5, 0.0)
	_wait(sim, 0.6 + _rules.cc_immunity + 0.1)
	sim.apply_effect(2, StatusEffects.Type.STUN, 2.0, 0.0)
	assert_true(sim.players[2].effects.has(StatusEffects.Type.STUN))


func test_a_slow_is_never_blocked_by_the_immunity() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 0.5, 0.0)
	_wait(sim, 0.6)
	sim.apply_effect(2, StatusEffects.Type.SLOW, 2.0, 0.4)
	assert_true(sim.players[2].effects.has(StatusEffects.Type.SLOW))


func test_reapplying_the_same_cc_while_it_is_still_on_still_extends_it() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 1.0, 0.0)
	_wait(sim, 0.5)
	sim.apply_effect(2, StatusEffects.Type.STUN, 2.0, 0.0)
	assert_almost_eq(sim.players[2].effects.time_left(StatusEffects.Type.STUN), 2.0, 0.05, "the rule is about a new one after it ends")


func test_with_the_immunity_set_to_zero_nothing_changes() -> void:
	_rules.cc_immunity = 0.0
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 0.5, 0.0)
	_wait(sim, 0.6)
	sim.apply_effect(2, StatusEffects.Type.STUN, 1.0, 0.0)
	assert_true(sim.players[2].effects.has(StatusEffects.Type.STUN))


func test_a_rescue_clears_the_immunity() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 0.5, 0.0)
	_wait(sim, 0.6)
	sim.kill(2)
	sim.objectives.rescue(sim, sim.players[2], 1)
	sim.apply_effect(2, StatusEffects.Type.STUN, 1.0, 0.0)
	assert_true(sim.players[2].effects.has(StatusEffects.Type.STUN), "a fresh start after the box")


func test_damage_from_a_weapon_still_lands_while_the_cc_is_ignored() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(2, StatusEffects.Type.STUN, 0.5, 0.0)
	_wait(sim, 0.6)
	sim.damage(2, 10)
	assert_eq(sim.players[2].hp, _rules.player_max_hp - 10)
