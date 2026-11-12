extends GutTest
## Langit Lupa (docs/GDD.md "Langit Lupa"): one taya; a kid on a langit (a platform or its own mound) is safe for a while; the platforms
## wear out; a langit never refills stamina; brownouts leave each kid seeing only the kids near it.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const LANGIT: StreetModeRules = preload("res://data/rules/langit_lupa.tres")
const DT: float = 1.0 / 30.0
const TAYA: int = 4

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.stamina_enabled = true


## Four kids, ids 1..4, kid 4 the taya, every kid far from the others on the street (none on a langit).
func _sim(rules: StreetModeRules = LANGIT) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 7)
	for i: int in 4:
		sim.add_player(1 + i, 0)
	sim.start_mode(StreetMode.create(rules))
	var game: LangitLupa = sim.mode as LangitLupa
	game.taya_id = TAYA
	game.place_teams(sim)
	game.grace_left = 0.0
	_spread(sim)
	return sim


func _game(sim: MatchSim) -> LangitLupa:
	return sim.mode as LangitLupa


## Everyone on open street, far apart (no langit under anyone).
func _spread(sim: MatchSim) -> void:
	var spots: Array[Vector2] = [Vector2(-10.0, 22.0), Vector2(10.0, 22.0), Vector2(-10.0, -22.0), Vector2(10.0, -22.0)]
	for i: int in 4:
		sim.players[1 + i].position = spots[i]
		sim.end_move(sim.players[1 + i])
	for id: int in sim.players:
		assert_false(_game(sim).on_langit(sim.players[id]), "kid %d starts on the street" % id)


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT - 0.001):
		sim.step(DT)


## The middle of platform `index`.
func _spot(index: int) -> Vector2:
	return LANGIT.langit_spots[index].get_center()


func _rules_with(changes: Dictionary) -> StreetModeRules:
	var rules: StreetModeRules = LANGIT.duplicate() as StreetModeRules
	for key: String in changes:
		rules.set(key, changes[key])
	return rules


func test_langit_lupa_is_a_street_game_of_its_own_with_one_taya_and_points_per_kid() -> void:
	var rules: StreetModeRules = StreetMode.rules_for(&"langit_lupa")
	assert_eq(rules.title, "Langit Lupa")
	assert_true(StreetMode.create(rules) is LangitLupa)
	assert_true(StreetMode.create(rules) is TayaGame, "it shares the one-taya scoring with Tumbang Preso")
	assert_true(StreetMode.create(StreetMode.rules_for(&"tumbang_preso")) is TayaGame)
	assert_gt(rules.langit_spots.size(), 3, "a few platforms on the street")
	for spot: Rect2 in rules.langit_spots:
		assert_true(LAYOUT.lane_rect().encloses(spot), "every platform is on the street: %s" % spot)
	var sim: MatchSim = _sim()
	assert_true(sim.mode.is_free_for_all())
	assert_eq(_game(sim).points.size(), 4)


func test_the_taya_starts_in_the_middle_and_the_others_on_a_ring_and_only_the_others_tire() -> void:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
	for i: int in 4:
		sim.add_player(1 + i, 0)
	sim.start_mode(StreetMode.create(LANGIT))
	var game: LangitLupa = _game(sim)
	assert_true(sim.players.has(game.taya_id), "the first taya is one of the kids, drawn at random")
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		if id == game.taya_id:
			assert_eq(state.position, Vector2.ZERO)
			assert_false(state.has_stamina, "the taya never tires")
		else:
			assert_almost_eq(state.position.length(), LANGIT.ring_radius, 0.01)
			assert_true(state.has_stamina, "everyone else has stamina")


func test_the_taya_tags_a_kid_on_the_street_who_becomes_the_taya_and_the_round_resets() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	var changes: Array = []
	game.taya_changed.connect(func(old: int, now: int) -> void: changes.append([old, now]))
	sim.players[1].position = Vector2(3.0, 3.0)
	sim.players[TAYA].position = Vector2(3.0, 4.0)
	sim.step(DT)
	assert_eq(changes, [[TAYA, 1]], "kid 1 is the taya now")
	assert_eq(game.points[TAYA], LANGIT.tag_points, "the tagger's bonus")
	assert_true(game.is_resetting())
	assert_true(sim.players[TAYA].has_stamina == false, "nothing changes until the round resets")
	_wait(sim, LANGIT.reset_seconds + DT)
	assert_false(game.is_resetting())
	assert_eq(sim.players[1].position, Vector2.ZERO, "the new taya starts in the middle")
	assert_false(sim.players[1].has_stamina)
	assert_true(sim.players[TAYA].has_stamina, "the old taya tires like everyone else now")


func test_a_kid_on_a_platform_is_safe_until_its_safe_time_runs_out_and_the_street_refills_it() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	var kid: PlayerState = sim.players[1]
	kid.position = _spot(2)
	sim.players[TAYA].position = _spot(2) + Vector2(0.0, 1.0)
	_wait(sim, LANGIT.langit_seconds - 0.2)
	assert_eq(game.taya_id, TAYA, "safe on the langit")
	assert_true(game.is_safe(kid))
	_wait(sim, 0.3)
	assert_eq(game.taya_id, 1, "its safe time ran out: the langit no longer saves it")
	# the safe time comes back on the street
	var other: MatchSim = _sim()
	var g: LangitLupa = _game(other)
	g.safe_left[2] = 0.0
	other.players[2].position = Vector2(-4.0, 10.0)
	_wait(other, 1.0)
	assert_almost_eq(g.safe_left[2], LANGIT.langit_refill_per_s * 1.0, 0.05)


func test_a_platform_wears_out_under_the_kids_and_breaks_until_the_next_round() -> void:
	var sim: MatchSim = _sim(_rules_with({"langit_seconds": 100.0}))
	var game: LangitLupa = _game(sim)
	var broke: Array[int] = []
	game.spot_broke.connect(func(index: int) -> void: broke.append(index))
	sim.players[1].position = _spot(0) + Vector2(-0.5, 0.0)
	sim.players[2].position = _spot(0) + Vector2(0.5, 0.0)
	_wait(sim, LANGIT.langit_wear_seconds / 2.0 - 0.2)
	assert_true(game.spot_standing(0), "two kids wear it twice as fast, not yet broken")
	assert_gt(game.wear_share(0), 0.9)
	_wait(sim, 0.3)
	assert_eq(broke, [0] as Array[int])
	assert_false(game.spot_standing(0))
	assert_false(game.on_langit(sim.players[1]), "a broken platform is just street")
	game.place_teams(sim)
	assert_true(game.spot_standing(0), "back for the next round")


func test_the_lupa_button_raises_a_mound_under_the_kid_that_saves_it_and_sinks_and_waits() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	var kid: PlayerState = sim.players[1]
	kid.position = Vector2(4.0, 12.0)
	assert_true(game.can_raise(kid))
	assert_false(game.can_raise(sim.players[TAYA]), "the taya raises none")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, sim.tick))
	sim.step(DT)
	assert_true(game.mounds.has(1))
	assert_true(game.on_langit(kid))
	assert_false(game.can_raise(kid), "one at a time: it waits `mound_cooldown`")
	sim.step(DT)
	assert_almost_eq(game.mound_wait[1], LANGIT.mound_cooldown - DT * 2.0, 0.001, "a held button raises no second mound")
	sim.set_input(1, null)
	sim.players[TAYA].position = kid.position + Vector2(0.0, 1.0)
	_wait(sim, 1.0)
	assert_eq(game.taya_id, TAYA, "safe on its mound")
	sim.players[TAYA].position = Vector2(10.0, -22.0)
	_wait(sim, LANGIT.mound_seconds)
	assert_false(game.mounds.has(1), "the mound sank")
	assert_false(game.on_langit(kid))
	_wait(sim, LANGIT.mound_cooldown - LANGIT.mound_seconds)
	assert_true(game.can_raise(kid), "ready again")


func test_the_lupa_button_waits_while_the_safe_time_is_used_up_so_no_mound_is_wasted() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	var kid: PlayerState = sim.players[1]
	game.safe_left[1] = 0.0
	assert_false(game.can_raise(kid), "no safe time: a mound would save nothing")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, sim.tick))
	sim.step(DT)
	assert_false(game.mounds.has(1), "the press raises nothing")
	assert_eq(game.mound_wait.get(1, 0.0), 0.0, "and the wait does not start")
	sim.set_input(1, null)
	_wait(sim, 0.5)
	assert_true(game.can_raise(kid), "the street gives some back: ready again")


func test_standing_on_a_langit_never_refills_stamina_but_the_street_does() -> void:
	var sim: MatchSim = _sim(_rules_with({"langit_seconds": 100.0}))
	var kid: PlayerState = sim.players[1]
	kid.position = _spot(5)
	kid.stamina = 20.0
	_wait(sim, 3.0)
	assert_eq(kid.stamina, 20.0, "no rest in heaven")
	kid.position = Vector2(-4.0, 12.0)
	_wait(sim, 3.0)
	assert_gt(kid.stamina, 20.0, "the street refills it")
	var off: MatchSim = _sim(_rules_with({"langit_seconds": 100.0, "langit_blocks_rest": false}))
	off.players[1].position = _spot(5)
	off.players[1].stamina = 20.0
	_wait(off, 3.0)
	assert_gt(off.players[1].stamina, 20.0, "the data toggle turns it off")


func test_every_kid_but_the_taya_earns_a_point_for_each_stretch_it_stays_free() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	_wait(sim, LANGIT.survive_seconds * 2.0 + DT)
	for id: int in [1, 2, 3]:
		assert_eq(game.points[id], 2, "kid %d" % id)
	assert_eq(game.points[TAYA], 0, "the taya earns none")


func test_after_the_time_the_kid_with_the_most_points_wins() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	game.points[2] = 5
	game.time_left = DT
	sim.step(DT)
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_eq(game.winner_id, 2)
	assert_true(game.local_result(sim, 2)["won"])


func test_brownouts_come_on_a_fixed_clock_and_each_kid_sees_only_the_kids_near_it() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	game.time_left = LANGIT.match_seconds - (LANGIT.brownout_first - 1.0)
	assert_false(game.is_brownout())
	game.time_left = LANGIT.match_seconds - (LANGIT.brownout_first + 1.0)
	assert_true(game.is_brownout())
	assert_almost_eq(game.brownout_left(), LANGIT.brownout_seconds - 1.0, 0.001)
	game.time_left = LANGIT.match_seconds - (LANGIT.brownout_first + LANGIT.brownout_seconds + 1.0)
	assert_false(game.is_brownout(), "the lights come back")
	game.time_left = LANGIT.match_seconds - (LANGIT.brownout_first + LANGIT.brownout_every + 1.0)
	assert_true(game.is_brownout(), "and go out again a while later")
	sim.players[1].position = Vector2(0.0, 0.0)
	sim.players[2].position = Vector2(LANGIT.brownout_sight - 0.5, 0.0)
	sim.players[3].position = Vector2(-LANGIT.brownout_sight - 0.5, 0.0)
	assert_true(game.visible_to(sim, 1, 1))
	assert_true(game.visible_to(sim, 1, 2), "near: seen")
	assert_false(game.visible_to(sim, 1, 3), "far: not seen")
	assert_eq(game.hidden_from(sim, 1), [3, TAYA] as Array[int])
	game.time_left = LANGIT.match_seconds
	assert_eq(game.hidden_from(sim, 1), [] as Array[int], "with the lights on everyone sees everyone")


func test_a_phone_that_cannot_see_a_kid_is_not_told_where_its_mound_is() -> void:
	var sim: MatchSim = _sim()
	var game: LangitLupa = _game(sim)
	game.raise_mound(sim, sim.players[3])
	game.raise_mound(sim, sim.players[2])
	var data: Dictionary = game.filter_for(game.snapshot(), [3] as Array[int])
	assert_false((data["mounds"] as Dictionary).has(3))
	assert_true((data["mounds"] as Dictionary).has(2))
	assert_true(game.mounds.has(3), "the server's own game keeps it")
	var copy: LangitLupa = StreetMode.create(LANGIT) as LangitLupa
	data["unseen"] = [3]
	copy.restore(data)
	assert_true(copy.unseen.has(3))
	copy.restore(game.snapshot())
	assert_true(copy.unseen.is_empty(), "a snapshot without the list: everyone is seen")


func test_bots_play_a_whole_match_the_taya_changes_hands_and_they_use_the_langit_and_mounds() -> void:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 11)
	for i: int in 6:
		sim.add_player(1 + i, 0)
	sim.start_mode(StreetMode.create(LANGIT))
	var game: LangitLupa = _game(sim)
	var coach: StreetModeCoach = StreetModeCoach.new()
	for id: int in sim.players:
		coach.add(id)
	var swaps: Array[int] = []
	var raised: Array[int] = []
	var safe_ticks: int = 0
	game.roles_swapped.connect(func(who: int) -> void: swaps.append(who))
	game.mound_raised.connect(func(id: int) -> void: raised.append(id))
	while sim.phase != MatchSim.Phase.MATCH_OVER and sim.match_time < LANGIT.match_seconds + 120.0:
		coach.drive(sim)
		sim.step(DT)
		for state: PlayerState in sim.players.values():
			if game.is_safe(state):
				safe_ticks += 1
	gut.p("Langit Lupa bots: %s points, %d changes of taya, %d mounds, %.0f kid-seconds safe" % [game.points, swaps.size(), raised.size(), safe_ticks * DT])
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER, "the match ends")
	assert_gt(swaps.size(), 3, "the taya changes hands")
	assert_gt(raised.size(), 0, "the bots raise mounds")
	assert_gt(safe_ticks * DT, 30.0, "the bots climb the langit")
