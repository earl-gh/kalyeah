extends GutTest
## Tumbang Preso and Patintero (docs/GDD.md "Tumbang Preso and Patintero"): timed street games on the Agawan Base street.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const TUMBANG: StreetModeRules = preload("res://data/rules/tumbang_preso.tres")
const PATINTERO: StreetModeRules = preload("res://data/rules/patintero.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


## Tumbang Preso: `size` kids on one team, ids 1..size; the last is the taya. Patintero: `size` kids a side, team 0 (ids 1..) runs first.
func _sim(mode_rules: StreetModeRules, size: int = 4) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 7)
	if mode_rules.id == &"tumbang_preso":
		for i: int in size:
			sim.add_player(1 + i, 0)
	else:
		for i: int in size:
			sim.add_player(1 + i, 0)
		for i: int in size:
			sim.add_player(4 + i, 1)
	sim.start_mode(StreetMode.create(mode_rules))
	return sim


func _tumbang(size: int = 4) -> MatchSim:
	return _sim(TUMBANG, size)


## Holds the Throw button of `id` for `seconds` aimed along `way`, then lets go (one tick without it).
func _throw(sim: MatchSim, id: int, way: Vector2, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.set_input(id, PlayerInput.create(Vector2.ZERO, way, PlayerInput.BTN_CARD_1, sim.tick))
		sim.step(DT)
	sim.set_input(id, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	sim.set_input(id, null)


## Steps until the slipper of `id` has left the air (or `limit` seconds pass).
func _fly(sim: MatchSim, game: TumbangPreso, id: int, limit: float = 3.0) -> void:
	var left: int = ceili(limit / DT)
	while game.flights.has(id) and left > 0:
		sim.step(DT)
		left -= 1


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func _stand_all(sim: MatchSim) -> void:
	for id: int in sim.players:
		sim.set_input(id, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))


## Everyone but `keep` far from the play, in the throwing area (so nobody can be tagged or in the way).
func _clear_street(sim: MatchSim, keep: Array[int]) -> void:
	var x: float = -10.0
	for id: int in sim.players:
		if not keep.has(id):
			sim.players[id].position = Vector2(x, 20.0)
			x += 2.0


# ---- Tumbang Preso -------------------------------------------------------------------------

func test_tumbang_has_one_taya_and_everyone_else_throws_from_the_throwing_area() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	assert_eq(game.taya_id, 4, "the last kid in is the first taya")
	for state: PlayerState in sim.players.values():
		if state.id == game.taya_id:
			assert_almost_eq(state.position.y, TUMBANG.can_spot.y + TUMBANG.guard_offset, 0.001, "the taya waits by the can")
		else:
			assert_true(game.at_home(state.position), "throwers start in the throwing area")
			assert_true(game.has_slipper(state.id))
	assert_true(game.can_up)
	assert_true(game.is_free_for_all())
	assert_eq(sim.clock_left(), TUMBANG.match_seconds, "five minutes")
	assert_true(sim.counts_down())


func test_the_taya_runs_one_and_a_half_times_as_fast() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	sim.players[1].position = Vector2(-9.0, 20.0)
	game.slippers[1] = Vector2(30.0, 30.0)  # no slipper in hand: free to run anywhere
	sim.players[4].position = Vector2(9.0, 0.0)
	var from_thrower: Vector2 = sim.players[1].position
	var from_taya: Vector2 = sim.players[4].position
	for id: int in [1, 4]:
		sim.set_input(id, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, 0, 0))
	_wait(sim, 0.5)
	assert_almost_eq(sim.players[4].position.distance_to(from_taya) / sim.players[1].position.distance_to(from_thrower), TUMBANG.taya_speed_share, 0.05)


func test_holding_the_throw_button_grows_the_aim_line_and_letting_go_throws() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	assert_eq(game.aim_range(1), 0.0, "no aim until the button is held")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, 0))
	sim.step(DT)
	var short: float = game.aim_range(1)
	assert_almost_eq(short, TUMBANG.throw_min_range + (TUMBANG.throw_max_range - TUMBANG.throw_min_range) * DT / TUMBANG.throw_charge_time, 0.01, "it starts at the short end")
	_wait(sim, 0.5)
	assert_gt(game.aim_range(1), short + 2.0, "the longer you hold, the longer the line")
	_wait(sim, 2.0)
	assert_eq(game.aim_range(1), TUMBANG.throw_max_range, "and it stops at the longest")
	assert_eq(game.aim_dir[1], game.aim_at_can(sim.players[1].position), "it starts aimed at the can")
	var thrown: Array[int] = []
	game.slipper_thrown.connect(func(id: int) -> void: thrown.append(id))
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	sim.step(DT)
	assert_eq(thrown, [1] as Array[int])
	assert_false(game.is_aiming(1))
	assert_false(game.has_slipper(1), "the slipper is in the air")
	assert_eq(sim.weapons.projectiles[0].def.id, &"tsinelas_light", "tsinelas, drawn and animated like Agawan Base's")


func test_the_aim_sensitivity_is_low_when_the_line_is_short_and_high_at_full_length() -> void:
	var game: TumbangPreso = _tumbang().mode as TumbangPreso
	assert_eq(game.sensitivity_at(0.0), TUMBANG.aim_sensitivity_low)
	assert_eq(game.sensitivity_at(1.0), TUMBANG.aim_sensitivity_high)
	assert_gt(game.sensitivity_at(1.0), game.sensitivity_at(0.0) * 2.0, "clearly faster at the limit")
	assert_lt(game.sensitivity_at(0.3), game.sensitivity_at(0.7))


func test_a_thrower_with_its_slipper_in_hand_cannot_enter_the_fetch_area() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [1])
	sim.players[1].position = Vector2(0.0, 14.0)
	sim.set_input(1, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, 0, 0))
	_wait(sim, 1.5)
	assert_almost_eq(sim.players[1].position.y, TUMBANG.home_z, 0.01, "stopped at the throw line")
	sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, 0))
	_wait(sim, 0.5)
	assert_gt(sim.players[1].position.x, 0.5, "free to move along the throwing area")
	# once it has thrown, the slipper is not in hand: it may go out
	game.slippers[1] = Vector2(0.0, 5.0)
	sim.set_input(1, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, 0, 0))
	_wait(sim, 0.5)
	assert_lt(sim.players[1].position.y, TUMBANG.home_z - 1.0, "into the fetch area")


func test_the_taya_stays_in_the_fetch_area() -> void:
	var sim: MatchSim = _tumbang()
	sim.set_input(4, PlayerInput.create(Vector2(0.0, 1.0), Vector2.ZERO, 0, 0))
	_wait(sim, 3.0)
	assert_lt(sim.players[4].position.y, TUMBANG.home_z, "never into the throwing area")


func test_a_cancelled_aim_and_a_throw_from_the_fetch_area_throw_nothing() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, 0))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_AIM_CANCEL, 0))
	sim.step(DT)
	assert_true(game.has_slipper(1), "cancelled")
	game.slippers[2] = Vector2(0.0, 5.0)  # kid 2 has already thrown and stands out in the fetch area
	sim.players[2].position = Vector2(0.0, 8.0)
	_throw(sim, 2, Vector2(0.0, -1.0), 0.3)
	assert_false(game.flights.has(2), "only from the throwing area")


func test_a_hit_flings_the_can_and_the_slipper_apart() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var cans: Array = []
	var slips: Array = []
	game.can_flung.connect(func(from: Vector2, path: Array[Vector2]) -> void: cans.append([from, path]))
	game.slipper_flung.connect(func(id: int, from: Vector2, path: Array[Vector2]) -> void: slips.append([id, from, path]))
	_clear_street(sim, [1, 4])
	sim.players[4].position = Vector2(-9.0, 0.0)  # the taya out of the way
	sim.players[1].position = Vector2(TUMBANG.can_spot.x, 14.0)
	_throw(sim, 1, Vector2(0.0, -1.0), TUMBANG.throw_charge_time)
	_fly(sim, game, 1)
	assert_false(game.can_up, "the can is down")
	assert_eq(cans.size(), 1)
	assert_eq(slips.size(), 1)
	var can_end: Vector2 = (cans[0][1] as Array[Vector2])[-1]
	var slipper_end: Vector2 = (slips[0][2] as Array[Vector2])[-1]
	assert_eq(game.can_position, can_end)
	assert_eq(game.slippers[1], slipper_end, "the slipper lies where it was flung")
	assert_lt(can_end.y, TUMBANG.can_spot.y - 1.0, "a dead-on hit sends the can on along the throw")
	assert_gt(slipper_end.y, TUMBANG.can_spot.y, "and the slipper bounces back")
	assert_gt(game.can_busy_left, 0.0, "neither can be picked up before it lands")
	assert_gt(game.slipper_busy[1], 0.0)


func test_the_aim_guide_shows_the_way_the_can_and_the_slipper_would_go_only_when_the_aim_hits() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	assert_eq(game.aim_guide(sim, 1), {}, "no aim, no guide")
	sim.players[1].position = Vector2(TUMBANG.can_spot.x, 14.0)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_CARD_1, 0))
	_wait(sim, TUMBANG.throw_charge_time)
	var guide: Dictionary = game.aim_guide(sim, 1)
	assert_false(guide.is_empty(), "the line reaches the can")
	assert_almost_eq((guide["can_dir"] as Vector2).y, -1.0, 0.01, "dead on: the can goes straight on")
	assert_gt((guide["slipper_dir"] as Vector2).y, 0.0, "the slipper bounces back")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2(0.4, -1.0), PlayerInput.BTN_CARD_1, 0))
	sim.step(DT)
	assert_eq(game.aim_guide(sim, 1), {}, "aimed wide: nothing to show")
	game.can_up = false
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_CARD_1, 0))
	sim.step(DT)
	assert_eq(game.aim_guide(sim, 1), {}, "nothing standing to hit")


func test_the_real_fling_is_a_little_off_the_guide_so_it_cannot_be_aimed_to_the_other_end() -> void:
	var off: Array[float] = []
	for seed_value: int in 12:
		var sim: MatchSim = MatchSim.new(_rules, LAYOUT, seed_value)
		for i: int in 4:
			sim.add_player(1 + i, 0)
		sim.start_mode(StreetMode.create(TUMBANG))
		var game: TumbangPreso = sim.mode as TumbangPreso
		_clear_street(sim, [1, 4])
		sim.players[4].position = Vector2(-9.0, 0.0)
		sim.players[1].position = Vector2(TUMBANG.can_spot.x, 14.0)
		var cans: Array = []
		game.can_flung.connect(func(from: Vector2, path: Array[Vector2]) -> void: cans.append([from, path]))
		_throw(sim, 1, Vector2(0.0, -1.0), TUMBANG.throw_charge_time)
		_fly(sim, game, 1)
		var way: Vector2 = ((cans[0][1] as Array[Vector2])[0] as Vector2) - (cans[0][0] as Vector2)
		off.append(rad_to_deg(absf(way.angle_to(Vector2(0.0, -1.0)))))
		assert_lte(way.length(), TUMBANG.can_fling_distance * (1.0 + TUMBANG.fling_distance_error) + 0.01, "short")
		assert_lt(game.slippers[1].y, TUMBANG.home_z, "and nowhere near the other end")
	assert_gt(off.max(), 1.0, "sometimes a little off")
	assert_lte(off.max(), TUMBANG.fling_error_degrees + 0.5, "never more than the error")


func test_a_hit_bounces_once_more_with_the_bounce_chance_for_a_longer_trip() -> void:
	for chance: float in [0.0, 1.0]:
		var rules: StreetModeRules = TUMBANG.duplicate() as StreetModeRules
		rules.bounce_chance = chance
		var sim: MatchSim = _sim(rules)
		var game: TumbangPreso = sim.mode as TumbangPreso
		var paths: Array[int] = []
		game.can_flung.connect(func(_from: Vector2, path: Array[Vector2]) -> void: paths.append(path.size()))
		_clear_street(sim, [1, 4])
		sim.players[4].position = Vector2(-9.0, 0.0)
		sim.players[1].position = Vector2(rules.can_spot.x, 14.0)
		_throw(sim, 1, Vector2(0.0, -1.0), rules.throw_charge_time)
		_fly(sim, game, 1)
		assert_eq(paths, [1 if chance == 0.0 else 2] as Array[int], "bounce chance %s" % chance)
		if chance == 1.0:
			assert_almost_eq(game.can_busy_left, rules.fling_time * 2.0, DT * 3.0, "two flings take two landings")


func test_fetch_the_slipper_and_run_to_the_other_end_for_a_point_and_back_to_the_throwing_area() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [1, 4])
	sim.players[4].position = Vector2(-10.0, 5.0)
	sim.players[1].position = Vector2(0.0, 14.0)
	_throw(sim, 1, Vector2(0.0, -1.0), TUMBANG.throw_charge_time)
	_fly(sim, game, 1)
	_wait(sim, 1.2)
	sim.players[1].position = game.slippers[1]
	sim.step(DT)
	assert_false(game.slippers.has(1), "picked up")
	assert_true(game.fetched.has(1))
	assert_true(game.has_slipper(1), "in hand again, and out in the fetch area it may stay")
	sim.players[1].position = Vector2(0.0, TUMBANG.goal_z - 0.5)
	sim.step(DT)
	assert_eq(game.points[1], 1, "a point at the other end")
	assert_true(game.at_home(sim.players[1].position), "and it is back in the throwing area on its own")
	assert_false(game.fetched.has(1))


func test_a_slipper_that_ends_up_at_the_other_end_scores_without_fetching() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	game.slippers[1] = Vector2(0.0, TUMBANG.goal_z - 1.0)
	game.slipper_busy[1] = 0.0
	game._slipper_rests(sim, 1)
	assert_eq(game.points[1], 1)
	assert_true(game.has_slipper(1), "the slipper is back in hand")
	assert_true(game.at_home(sim.players[1].position))


func test_a_thrower_has_five_seconds_outside_the_fetch_area_or_becomes_the_taya() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var changes: Array = []
	game.taya_changed.connect(func(old: int, new: int, why: StringName) -> void: changes.append([old, new, why]))
	_clear_street(sim, [1, 4])
	sim.players[4].position = Vector2(-10.0, 5.0)
	sim.players[1].position = Vector2(0.0, 14.0)
	_throw(sim, 1, Vector2(0.0, -1.0), TUMBANG.throw_charge_time)
	_fly(sim, game, 1)
	_wait(sim, 1.2)
	assert_true(game.scared_left.has(1), "its slipper lies in the fetch area: the clock runs")
	_wait(sim, TUMBANG.scared_seconds - 2.0)
	assert_lt(game.scared_left[1], 2.5)
	# stepping into the fetch area stops the clock; stepping out goes on with what is left
	sim.players[1].position = Vector2(0.0, 9.0)
	var left: float = game.scared_left[1]
	_wait(sim, 3.0)
	assert_almost_eq(game.scared_left[1], left, 0.05, "paused while in the fetch area")
	assert_true(changes.is_empty())
	sim.players[1].position = Vector2(0.0, 15.0)
	_wait(sim, left + 0.5)
	assert_eq(changes, [[4, 1, &"scared"]], "too scared: the thrower is the taya now")
	assert_eq(game.taya_id, 1)
	assert_true(game.is_resetting(), "a pause, then the round resets")
	_wait(sim, TUMBANG.reset_seconds + 0.2)
	assert_true(game.has_slipper(4), "the old taya throws again, slipper in hand")
	assert_true(game.at_home(sim.players[4].position), "from the throwing area")
	assert_false(game.slippers.has(1), "the new taya has no slipper to fetch")


func test_the_taya_cannot_tag_while_the_can_is_down() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [1, 4])
	game.grace_left = 0.0
	game.can_up = false
	game.can_position = Vector2(-8.0, -6.0)
	game.slippers[1] = Vector2(0.0, 5.0)
	sim.players[1].position = Vector2(0.0, 6.0)
	sim.players[4].position = Vector2(0.0, 5.4)
	sim.step(DT)
	assert_eq(game.taya_id, 4, "the can is down and not in its place: nobody is tagged")


func test_moving_into_a_throwers_tag_circle_tags_it_and_the_round_resets_with_everyone_back_in_place() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var casts: Array = []
	sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void: casts.append([id, weapon]))
	_clear_street(sim, [1, 4])
	game.grace_left = 0.0
	game.points[2] = 3
	game.slippers[1] = Vector2(0.0, 5.0)
	sim.players[1].position = Vector2(0.0, 6.0)
	# just outside the circle: nothing
	sim.players[4].position = Vector2(TUMBANG.tag_circle_radius + 0.3, 6.0)
	sim.step(DT)
	assert_eq(game.taya_id, 4, "outside the circle")
	# just inside: the touch
	sim.players[4].position = Vector2(TUMBANG.tag_circle_radius - 0.2, 6.0)
	sim.step(DT)
	assert_eq(game.taya_id, 1, "inside the circle: the thrower is it")
	assert_eq(casts, [[4, StreetMode.TAG_CAST]], "the taya plays its touch")
	assert_true(game.is_resetting())
	# everyone stands still while the touch plays
	var at: Vector2 = sim.players[1].position
	sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, 0))
	_wait(sim, 0.5)
	assert_eq(sim.players[1].position, at, "nobody moves during the pause")
	_wait(sim, TUMBANG.reset_seconds)
	assert_false(game.is_resetting())
	assert_true(game.at_home(sim.players[4].position), "the old taya is a thrower in the throwing area")
	assert_true(game.has_slipper(4))
	assert_almost_eq(sim.players[1].position.y, TUMBANG.can_spot.y + TUMBANG.guard_offset, 0.01, "the new taya is by the can")
	assert_true(game.slippers.is_empty(), "every slipper is back in a hand")
	assert_true(game.can_up, "and the can stands")
	assert_eq(game.points[2], 3, "the points stay")
	assert_gt(game.grace_left, 0.0, "a moment of grace after the reset")


func test_the_taya_fetches_the_fallen_can_and_stands_it_up_again() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [4])
	game.can_up = false
	game.can_position = Vector2(-6.0, -4.0)
	sim.players[4].position = Vector2(-6.0, -3.5)
	sim.step(DT)
	assert_eq(game.can_carrier, 4, "the taya picked it up")
	sim.players[4].position = TUMBANG.can_spot
	sim.step(DT)
	assert_true(game.can_up, "the can is back in its place")


func test_a_flung_can_cannot_be_picked_up_before_it_lands() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [4])
	game.can_up = false
	game.can_position = Vector2(-6.0, -4.0)
	game.can_busy_left = 0.5
	sim.players[4].position = Vector2(-6.0, -3.9)
	sim.step(DT)
	assert_eq(game.can_carrier, -1, "still in the air")
	_wait(sim, 0.6)
	assert_eq(game.can_carrier, 4)


func test_a_thrower_without_its_slipper_can_kick_the_can_once_in_a_random_mid_range_direction() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [1, 4])
	sim.players[4].position = Vector2(-10.0, 6.0)
	game.slippers[1] = Vector2(5.0, 6.0)  # thrown: not in hand
	sim.players[1].position = TUMBANG.can_spot + Vector2(1.0, 0.0)
	var kicks: Array[int] = []
	game.can_kicked.connect(func(id: int) -> void: kicks.append(id))
	var from: Vector2 = game.can_position
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	assert_eq(kicks, [1] as Array[int])
	assert_false(game.can_up, "kicked over")
	var distance: float = game.can_position.distance_to(from)
	assert_gte(distance, TUMBANG.kick_min_distance - 0.5)
	assert_lte(distance, TUMBANG.kick_max_distance + 0.5, "mid range")
	# holding the button does not kick again, and it has to cool down
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	_wait(sim, 1.0)
	assert_eq(kicks.size(), 1)
	assert_gt(game.kick_left[1], 0.0)


func test_a_thrower_with_its_slipper_cannot_kick_and_a_far_one_cannot_reach() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	_clear_street(sim, [1, 4])
	sim.players[1].position = TUMBANG.can_spot + Vector2(1.0, 0.0)
	assert_false(game.can_kick(sim.players[1]), "the slipper is in hand: that is the Throw button")
	game.slippers[1] = Vector2(5.0, 6.0)
	assert_true(game.can_kick(sim.players[1]))
	sim.players[1].position = TUMBANG.can_spot + Vector2(5.0, 0.0)
	assert_false(game.can_kick(sim.players[1]), "too far from the can")


func test_the_kid_with_the_most_points_wins_and_a_tie_plays_on_to_the_next_point() -> void:
	var sim: MatchSim = _tumbang()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var won: Array[int] = []
	sim.match_won.connect(func(team: int) -> void: won.append(team))
	_stand_all(sim)
	game.time_left = DT / 2.0
	sim.step(DT)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING, "0 : 0 plays on")
	game.points[2] = 3
	game.points[3] = 3
	sim.step(DT)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING, "a tie for the lead plays on")
	game.award(sim, sim.players[3])
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_eq(game.winner_id, 3)
	assert_eq(game.local_result(sim, 3), {"won": true, "mine": 4, "theirs": 3})
	assert_eq(game.local_result(sim, 2), {"won": false, "mine": 3, "theirs": 4})
	assert_eq(game.ranking(), [3, 2, 1, 4] as Array[int])
	assert_eq(won.size(), 1)


# ---- Patintero -----------------------------------------------------------------------------

func test_patintero_runners_start_behind_the_home_line_and_one_guard_takes_each_line() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	for id: int in [1, 2, 3]:
		assert_true(lines.at_home(sim.players[id].position), "runners start behind the line")
	assert_eq(lines.guard_axis[4], Patintero.Axis.HORIZONTAL)
	assert_eq(lines.guard_axis[5], Patintero.Axis.HORIZONTAL)
	assert_eq(lines.guard_axis[6], Patintero.Axis.VERTICAL, "the third guard takes the vertical line")
	assert_eq(sim.players[4].position, Vector2(0.0, PATINTERO.line_span))
	assert_eq(sim.players[5].position, Vector2(0.0, -PATINTERO.line_span))
	assert_eq(sim.players[6].position, Vector2.ZERO, "in the middle, where the vertical line crosses")


func test_a_patintero_guard_can_only_move_along_its_own_line() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	_stand_all(sim)
	# a horizontal guard: only sideways
	sim.set_input(4, PlayerInput.create(Vector2(0.6, 0.8), Vector2.ZERO, 0, 0))
	# the vertical guard: only up and down, between the two horizontal lines
	sim.set_input(6, PlayerInput.create(Vector2(0.8, 0.6), Vector2.ZERO, 0, 0))
	for i: int in 30:
		sim.step(DT)
	assert_almost_eq(sim.players[4].position.y, PATINTERO.line_span, 0.0001, "it keeps to its horizontal line")
	assert_gt(sim.players[4].position.x, 0.5, "but moves along it")
	assert_almost_eq(sim.players[6].position.x, 0.0, 0.0001, "it keeps to the vertical line")
	assert_gt(sim.players[6].position.y, 0.5)
	sim.set_input(6, PlayerInput.create(Vector2(0.0, 1.0), Vector2.ZERO, 0, 0))
	for i: int in 120:
		sim.step(DT)
	assert_almost_eq(sim.players[6].position.y, PATINTERO.line_span, 0.01, "and stops at the horizontal line")
	# a tap on the street takes it to the nearest point of its line
	assert_eq((sim.mode as Patintero).move_point(sim.players[4], Vector2(3.0, 2.0)), Vector2(3.0, PATINTERO.line_span))
	assert_eq((sim.mode as Patintero).move_point(sim.players[6], Vector2(3.0, 50.0)), Vector2(0.0, PATINTERO.line_span))


func test_patintero_a_runner_scores_only_after_getting_past_the_last_guard_and_back_to_the_base() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	for id: int in [4, 5, 6]:
		sim.players[id].position = Vector2(11.0, 0.0) if id != 6 else Vector2.ZERO
	# past the first guard only, then back to the base: no point
	sim.players[1].position = Vector2(-9.0, PATINTERO.line_span - 2.0)
	sim.step(DT)
	sim.players[1].position = Vector2(-9.0, PATINTERO.home_z + 0.5)
	sim.step(DT)
	assert_eq(sim.score.points, [0, 0] as Array[int], "back to the base after only the first guard: not counted")
	assert_false(lines.crossed.has(1))
	# past the last guard
	sim.players[1].position = Vector2(-9.0, PATINTERO.goal_z - 0.2)
	sim.step(DT)
	assert_true(lines.crossed.has(1), "past the third guard")
	assert_eq(sim.score.points, [0, 0] as Array[int], "half way is no point")
	# going back to a guard it has passed is allowed (escaping a tag) and does not lose it
	sim.players[1].position = Vector2(-9.0, -PATINTERO.line_span + 3.0)
	sim.step(DT)
	sim.players[1].position = Vector2(-9.0, PATINTERO.goal_z - 0.2)
	sim.step(DT)
	assert_true(lines.crossed.has(1), "it still has passed the last guard")
	sim.players[1].position = Vector2(-9.0, PATINTERO.home_z + 0.5)
	sim.step(DT)
	assert_eq(sim.score.points, [1, 0] as Array[int], "past the last guard and back at the base: the team point")
	assert_false(lines.crossed.has(1))


func test_patintero_the_court_has_no_lines_of_its_own_beyond_the_first_and_last_guard() -> void:
	# the base and the far end sit just past the first and last guard lines and their tag circles; nothing else marks them
	assert_gt(PATINTERO.home_z, PATINTERO.line_span + PATINTERO.tag_circle_radius - 0.01, "the base is clear of the first guard's circle")
	assert_lt(PATINTERO.goal_z, -PATINTERO.line_span - PATINTERO.tag_circle_radius + 0.01, "the far end is past the last guard's circle")
	assert_lt(PATINTERO.home_z, PATINTERO.line_span + PATINTERO.tag_circle_radius + 1.0, "and close to it")


func test_patintero_a_guard_moving_into_a_runners_circle_tags_it_for_a_point_and_the_round_resets() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	lines.grace_left = 0.0
	var casts: Array = []
	sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void: casts.append([id, weapon]))
	var tags: Array = []
	lines.tagged.connect(func(guard: int, runner: int) -> void: tags.append([guard, runner]))
	# just outside the circle of the guard on the near line
	sim.players[1].position = Vector2(PATINTERO.tag_circle_radius + 0.3, PATINTERO.line_span)
	sim.step(DT)
	assert_true(tags.is_empty(), "outside the circle")
	sim.players[1].position = Vector2(PATINTERO.tag_circle_radius - 0.2, PATINTERO.line_span)
	sim.step(DT)
	assert_eq(tags, [[4, 1]])
	assert_eq(casts, [[4, StreetMode.TAG_CAST]], "the guard plays its touch")
	assert_eq(sim.score.points, [0, 1] as Array[int], "the guards' team earns the point")
	assert_eq(sim.point_log[-1], Vector2i(1, MatchSim.CaptureKind.TOUCHED))
	assert_false(sim.players[1].tagged, "nobody waits on a sidewalk")
	assert_true(lines.is_resetting())
	_wait(sim, PATINTERO.reset_seconds + 0.2)
	assert_true(lines.at_home(sim.players[1].position), "the runner is back at the start")
	assert_eq(sim.players[4].position, Vector2(0.0, PATINTERO.line_span), "and the guard on its line")
	assert_eq(sim.mode.attack_team, 0, "the roles stay")


func test_patintero_a_runner_dives_and_the_cooldown_starts_when_it_is_standing_again() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	_stand_all(sim)
	var dives: Array = []
	lines.dived.connect(func(id: int, way: Vector2, time: float, recover: float) -> void: dives.append([id, way, time, recover]))
	sim.players[1].position = Vector2(8.0, 14.0)
	var from: Vector2 = sim.players[1].position
	sim.set_input(1, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	assert_eq(dives, [[1, Vector2(0.0, -1.0), PATINTERO.dive_time, PATINTERO.dive_recover]])
	assert_true(lines.is_diving(1))
	assert_false(lines.can_dive(sim.players[1]))
	sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, 0))
	_wait(sim, PATINTERO.dive_time)
	var distance: float = from.distance_to(sim.players[1].position)
	assert_almost_eq(distance, PATINTERO.dive_speed_share * _rules.move_speed * PATINTERO.dive_time, 0.8, "a dive covers its short burst")
	assert_gt(distance, _rules.move_speed * PATINTERO.dive_time * 1.8, "much faster than a run")
	assert_false(lines.is_diving(1), "the burst is over ...")
	assert_true(lines.is_busy(1), "... but it is still lying and getting up")
	assert_eq(lines.dive_ready_share(1), 0.0, "no cooldown yet")
	var lying_at: Vector2 = sim.players[1].position
	_wait(sim, PATINTERO.dive_recover * 0.6)
	assert_eq(sim.players[1].position, lying_at, "the stick does not steer the get-up")
	assert_eq(lines.dive_ready_share(1), 0.0)
	_wait(sim, PATINTERO.dive_recover * 0.4 + DT * 2.0)
	assert_false(lines.is_busy(1), "standing again")
	assert_true(lines.dive_cool.has(1), "now the cooldown starts")
	assert_almost_eq(lines.dive_cool[1], PATINTERO.dive_cooldown, DT * 4.0)
	# pressing again while it cools down does nothing; after the cooldown (5 s) it dives again
	sim.set_input(1, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	assert_eq(dives.size(), 1)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	_wait(sim, PATINTERO.dive_cooldown * 0.5)
	assert_between(lines.dive_ready_share(1), 0.35, 0.65, "the ring fills while it cools")
	assert_false(lines.can_dive(sim.players[1]))
	_wait(sim, PATINTERO.dive_cooldown * 0.5 + DT * 4.0)
	assert_true(lines.can_dive(sim.players[1]))
	assert_eq(lines.dive_ready_share(1), 1.0)
	assert_eq(PATINTERO.dive_cooldown, 5.0, "five seconds")


func test_patintero_a_guard_dives_off_its_line_then_runs_back_and_cannot_move_or_dive_until_it_is_there() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	_stand_all(sim)
	# running keeps the guard on its line ...
	sim.set_input(4, PlayerInput.create(Vector2(0.6, -0.8), Vector2.ZERO, 0, 0))
	_wait(sim, 0.4)
	assert_almost_eq(sim.players[4].position.y, PATINTERO.line_span, 0.001, "running only along the line")
	sim.players[4].position = Vector2(0.0, PATINTERO.line_span)
	# ... a dive is the one thing that takes it off it, whichever way the stick points
	sim.set_input(4, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	sim.set_input(4, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	_wait(sim, PATINTERO.dive_time)
	var off: float = absf(sim.players[4].position.y - PATINTERO.line_span)
	assert_gt(off, 3.0, "the dive took it well off its line (%.1f m)" % off)
	assert_true(lines.is_busy(4))
	assert_false(lines.is_returning(4), "it is getting up first")
	_wait(sim, PATINTERO.dive_recover + DT)
	assert_true(lines.is_returning(4), "then it runs back")
	assert_eq(lines.dive_ready_share(4), 0.0)
	assert_false(lines.can_dive(sim.players[4]))
	# the stick and the button do nothing on the way back
	var here: Vector2 = sim.players[4].position
	sim.set_input(4, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	var after: Vector2 = sim.players[4].position
	var line_point: Vector2 = lines.nearest_on_line(4, here)
	assert_lt(after.distance_to(line_point), here.distance_to(line_point), "it runs toward the nearest point of its line")
	assert_almost_eq(here.distance_to(after), PATINTERO.dive_return_speed_share * _rules.move_speed * DT, 0.02, "at its own speed")
	assert_eq(lines.dive_cool.get(4, 0.0), 0.0, "the cooldown has not started: it is not back yet")
	for i: int in 120:
		if not lines.is_returning(4):
			break
		sim.step(DT)
	assert_false(lines.is_busy(4), "back on its line")
	assert_almost_eq(sim.players[4].position.y, PATINTERO.line_span, 0.001)
	assert_almost_eq(lines.dive_cool[4], PATINTERO.dive_cooldown, DT * 2.0, "the cooldown starts now")
	assert_false(lines.can_dive(sim.players[4]))
	# the vertical guard goes back to the nearest point of the vertical line
	sim.players[6].position = Vector2(0.0, 0.0)
	sim.set_input(6, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_CARD_2, 0))
	sim.step(DT)
	sim.set_input(6, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	_wait(sim, PATINTERO.dive_time + PATINTERO.dive_recover + 0.3)
	assert_gt(sim.players[6].position.x, 1.0, "diving sideways took it off the vertical line")
	for i: int in 120:
		if not lines.is_busy(6):
			break
		sim.step(DT)
	assert_almost_eq(sim.players[6].position.x, 0.0, 0.001, "and it is back on it")


func test_patintero_the_roles_swap_when_the_turn_runs_out() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	_stand_all(sim)
	for i: int in ceili(PATINTERO.turn_seconds / DT) + 1:
		sim.step(DT)
	assert_eq(sim.mode.attack_team, 1)


func test_patintero_after_five_minutes_the_leader_wins_and_a_tie_plays_on() -> void:
	var sim: MatchSim = _sim(PATINTERO, 3)
	var lines: Patintero = sim.mode as Patintero
	var won: Array[int] = []
	sim.match_won.connect(func(team: int) -> void: won.append(team))
	_stand_all(sim)
	lines.time_left = DT / 2.0
	sim.step(DT)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING, "0 : 0 plays on")
	assert_eq(sim.clock_left(), 0.0)
	for id: int in [4, 5, 6]:
		sim.players[id].position = Vector2(11.0, 0.0)
	sim.players[1].position = Vector2(-10.0, PATINTERO.goal_z - 0.5)
	sim.step(DT)
	sim.players[1].position = Vector2(-10.0, PATINTERO.home_z + 0.5)
	sim.step(DT)
	assert_eq(won, [0] as Array[int], "the golden point wins")
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER)


# ---- both ---------------------------------------------------------------------------------

func test_there_are_no_skills_and_no_damage_in_the_street_games() -> void:
	for mode_rules: StreetModeRules in [TUMBANG, PATINTERO]:
		var sim: MatchSim = _sim(mode_rules, 3)
		sim.damage(1, 500)
		assert_false(sim.players[1].tagged, "%s: damage never tags" % mode_rules.title)
		sim.players[1].elixir = 10.0
		for slot: int in sim.players[1].weapons.size():
			assert_false(sim.weapon_ready(sim.players[1], slot), "%s: no card is ready" % mode_rules.title)
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_CARD_3 | PlayerInput.BTN_CARD_4, 0))
		for i: int in 10:
			sim.step(DT)
		assert_eq(sim.weapons.projectiles.size() + sim.weapons.zones.size() + sim.weapons.shields.size(), 0, "%s: nothing cast" % mode_rules.title)
		assert_false(sim.players[1].has_stamina, "%s: nobody tires" % mode_rules.title)


func test_the_throw_button_is_one_held_bit_and_one_release_like_a_card() -> void:
	var input: MatchInput = MatchInput.new()
	input.throw_press(Vector2(0.0, -1.0))
	var held: PlayerInput = input.build(1)
	assert_true(held.is_pressed(PlayerInput.BTN_CARD_1))
	assert_eq(held.aim, Vector2(0.0, -1.0))
	input.throw_turn(Vector2(1.0, 0.0))
	assert_eq(input.build(2).aim, Vector2(1.0, 0.0), "the aim turns while it is held")
	input.throw_release(false)
	var let_go: PlayerInput = input.build(3)
	assert_false(let_go.is_pressed(PlayerInput.BTN_CARD_1), "the bit goes off: the release")
	assert_false(let_go.is_pressed(PlayerInput.BTN_AIM_CANCEL))
	input.throw_press(Vector2(0.0, -1.0))
	input.build(4)
	input.throw_release(true)
	assert_true(input.build(5).is_pressed(PlayerInput.BTN_AIM_CANCEL), "a cancelled aim says so")
	# a press and a release inside one tick still shows the button held once
	input.throw_press(Vector2(0.0, -1.0))
	input.throw_release(false)
	assert_true(input.build(6).is_pressed(PlayerInput.BTN_CARD_1))
	assert_false(input.build(7).is_pressed(PlayerInput.BTN_CARD_1))


func test_the_knob_and_the_kick_button_ride_the_input() -> void:
	var input: MatchInput = MatchInput.new()
	input.stick = Vector2(0.5, -0.5)
	assert_eq(input.build(1).move, Vector2(0.5, -0.5))
	input.kick_press()
	assert_true(input.build(2).is_pressed(PlayerInput.BTN_CARD_2), "one tick")
	assert_false(input.build(3).is_pressed(PlayerInput.BTN_CARD_2))
	input.clear()
	assert_eq(input.build(4).move, Vector2.ZERO, "a menu lets go of the knob")


func test_bots_play_both_games_and_the_points_and_roles_change_hands() -> void:
	var tumbang: MatchSim = _tumbang(6)
	var patintero: MatchSim = _sim(PATINTERO, 3)
	for sim: MatchSim in [tumbang, patintero]:
		var coach: StreetModeCoach = StreetModeCoach.new()
		for id: int in sim.players:
			coach.add(id)
		var swaps: Array[int] = []
		sim.mode.roles_swapped.connect(func(who: int) -> void: swaps.append(who))
		while sim.phase != MatchSim.Phase.MATCH_OVER and sim.match_time < sim.mode.rules.match_seconds + 120.0:
			coach.drive(sim)
			sim.step(DT)
		var summary: String = str((sim.mode as TumbangPreso).points) if sim.mode is TumbangPreso else str(sim.score.points)
		gut.p("%s: %s after %d changes of roles" % [sim.mode.rules.title, summary, swaps.size()])
		assert_gt(swaps.size(), 1, "%s: the roles change hands" % sim.mode.rules.title)
	var total: int = 0
	for id: int in (tumbang.mode as TumbangPreso).points:
		total += (tumbang.mode as TumbangPreso).points[id]
	assert_gt(total, 2, "Tumbang Preso: the bots score")
	assert_gt(patintero.score.points[0] + patintero.score.points[1], 1, "Patintero: the bots score")
