extends GutTest
## Tripping while running (owner: "20% of tripping while the players are running, to add realism"): every `trip_interval` seconds of
## continuous running is one roll at `trip_chance`; a kid that trips falls for `trip_seconds` (cannot move or cast), then gets up.
## Same in Agawan Base, Tumbang Preso and Patintero (one MatchSim rule, the match's own seeded RNG). Seam: MatchSim in, PlayerState out.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


func _sim(seed_value: int = 5) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, seed_value)
	sim.add_player(1, 0)
	sim.players[1].position = Vector2(-6.0, 0.0)
	sim.equip(1)
	return sim


## `ticks` ticks of running back and forth in one second legs (a wall would stop the kid, and stopping restarts the count).
func _run(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		var way: float = 1.0 if int(i / 30) % 2 == 0 else -1.0
		sim.set_input(1, PlayerInput.create(Vector2(way, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func _stand(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func _ticks(seconds: float) -> int:
	return int(ceil(seconds / DT)) + 1


func test_the_data_says_twenty_percent_every_five_seconds_for_a_second_and_a_bit() -> void:
	assert_almost_eq(_rules.trip_chance, 0.2, 0.0001)
	assert_almost_eq(_rules.trip_interval, 5.0, 0.0001)
	assert_almost_eq(_rules.trip_seconds, 1.2, 0.0001)


func test_a_sure_trip_drops_the_runner_after_the_interval_and_not_before() -> void:
	_rules.trip_chance = 1.0
	var sim: MatchSim = _sim()
	_run(sim, _ticks(_rules.trip_interval) - 3)
	assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED), "no roll before the interval is up")
	_run(sim, 6)
	assert_true(sim.players[1].effects.has(StatusEffects.Type.TRIPPED), "the roll came up")


func test_a_tripped_kid_cannot_move_then_gets_up() -> void:
	_rules.trip_chance = 1.0
	var sim: MatchSim = _sim()
	_run(sim, _ticks(_rules.trip_interval))
	var fallen_at: Vector2 = sim.players[1].position
	_run(sim, _ticks(_rules.trip_seconds) - 3)
	assert_true(sim.players[1].effects.has(StatusEffects.Type.TRIPPED))
	assert_eq(sim.players[1].position, fallen_at, "lying there")
	assert_false(sim.players[1].can_act(), "no casting while down")
	_run(sim, 6)
	assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED), "up again")
	_run(sim, 3)
	assert_ne(sim.players[1].position, fallen_at, "running again")


func test_zero_chance_never_trips() -> void:
	_rules.trip_chance = 0.0
	var sim: MatchSim = _sim()
	for second: int in 60:
		_run(sim, 30)
		assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED))
		sim.players[1].stamina = _rules.stamina_max


func test_standing_still_never_trips_and_stopping_restarts_the_count() -> void:
	_rules.trip_chance = 1.0
	var sim: MatchSim = _sim()
	_stand(sim, 30 * 20)
	assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED), "twenty seconds standing")
	_run(sim, _ticks(_rules.trip_interval) - 10)
	_stand(sim, 1)
	_run(sim, _ticks(_rules.trip_interval) - 10)
	assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED), "two shorter runs are not one long one")



func test_about_one_in_five_runners_trips_on_the_first_roll() -> void:
	var tripped: int = 0
	var runs: int = 400
	for seed_value: int in runs:
		var sim: MatchSim = _sim(seed_value)
		_run(sim, _ticks(_rules.trip_interval))
		if sim.players[1].effects.has(StatusEffects.Type.TRIPPED):
			tripped += 1
	assert_almost_eq(float(tripped) / float(runs), _rules.trip_chance, 0.07, "%d of %d tripped" % [tripped, runs])


func test_the_trip_roll_leaves_the_matchs_other_random_numbers_alone() -> void:
	var with_trips: MatchSim = _sim(9)
	_run(with_trips, 300)
	_rules.trip_chance = 0.0
	var without: MatchSim = _sim(9)
	_run(without, 300)
	assert_eq(with_trips.rng.randi(), without.rng.randi(), "the match's own rng is not drawn from for trips")


func test_the_same_seed_trips_at_the_same_moment() -> void:
	var first: MatchSim = _sim(21)
	var second: MatchSim = _sim(21)
	for i: int in 600:
		_run(first, 1)
		_run(second, 1)
		assert_eq(first.players[1].effects.has(StatusEffects.Type.TRIPPED), second.players[1].effects.has(StatusEffects.Type.TRIPPED))


func test_a_trip_says_tripped_and_stops_casting() -> void:
	var effects: StatusEffects = StatusEffects.new()
	assert_true(effects.apply(StatusEffects.Type.TRIPPED, 1.2))
	assert_eq(effects.display_names(), PackedStringArray(["Tripped"]))
	assert_false(effects.can_move())
	assert_false(effects.can_cast())


func test_training_dummies_do_not_trip() -> void:
	_rules.trip_chance = 1.0
	var sim: MatchSim = _sim()
	var dummy: PlayerState = sim.add_dummy(7, 1, Vector2(12.0, -20.0), DummyBrain.new())
	assert_false(dummy.trips)


func test_a_kid_that_trips_plays_the_fall_and_gets_up_when_the_status_ends() -> void:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(_rules.characters[0])
	add_child(kid)
	var state: PlayerState = PlayerState.new()
	kid.animate(DT, 3.0, state)
	assert_false(kid.is_tripping(), "running")
	state.effects.apply(StatusEffects.Type.TRIPPED, _rules.trip_seconds)
	kid.animate(DT, 0.0, state)
	assert_true(kid.is_tripping(), "it falls")
	assert_eq(kid.pose, "tripped")
	for i: int in ceili(_rules.trip_seconds / DT) + 4:
		state.effects.step(DT)
		kid.animate(DT, 0.0, state)
	assert_false(kid.is_tripping(), "and is up again")
	kid.animate(DT, 0.0, state)
	assert_false(kid.is_tripping(), "no second fall from the one trip")


func test_a_worn_out_kid_only_walks_and_walking_does_not_trip() -> void:
	_rules.trip_chance = 1.0
	var sim: MatchSim = _sim()
	sim.players[1].overclocked = true
	_run(sim, _ticks(_rules.trip_interval) * 2)
	assert_false(sim.players[1].effects.has(StatusEffects.Type.TRIPPED))
