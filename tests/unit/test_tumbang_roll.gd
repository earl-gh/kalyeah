extends GutTest
## The roll for Tumbang Preso's first taya (owner): the sim draws the taya when the game starts (the match's seeded generator, nobody picks it
## in the lobby or the room) and the opening show plays a roll on the kid carousel that stops on that kid. Seam: TumbangPreso.setup,
## MatchSim.intro_total, TumbangRoll (the plan, the carousel on the taya at the end, the sounds).

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")


func _sim(seed_value: int, players: int = 4) -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, seed_value)
	for id: int in range(1, players + 1):
		sim.add_player(id, 0)
	sim.assign_characters()
	sim.start_mode(StreetMode.create(StreetMode.rules_for(&"tumbang_preso")))
	return sim


func _taya(sim: MatchSim) -> int:
	return (sim.mode as TumbangPreso).taya_id


func test_the_first_taya_is_drawn_from_everyone_and_the_same_seed_draws_the_same_kid() -> void:
	var seen: Dictionary = {}
	for seed_value: int in 60:
		var taya: int = _taya(_sim(seed_value))
		assert_true(taya >= 1 and taya <= 4, "one of the four")
		seen[taya] = true
		assert_eq(taya, _taya(_sim(seed_value)), "the same seed, the same taya")
	assert_eq(seen.size(), 4, "over many matches everybody is drawn, not only the last kid to join")


func test_the_opening_show_is_longer_by_the_roll_in_tumbang_preso_only() -> void:
	var game: StreetModeRules = StreetMode.rules_for(&"tumbang_preso")
	assert_gt(game.roll_seconds, 0.0)
	var sim: MatchSim = _sim(1)
	assert_almost_eq(sim.intro_total(), RULES.intro_time + game.roll_seconds, 0.0001)
	sim.begin_intro()
	assert_almost_eq(sim.intro_left, sim.intro_total(), 0.0001)
	assert_eq(StreetMode.rules_for(&"patintero").roll_seconds, 0.0, "Patintero has no roll")
	var base: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	assert_eq(base.intro_total(), RULES.intro_time, "nor Agawan Base")


func test_the_plan_goes_round_the_ring_slowing_down_and_ends_on_the_taya() -> void:
	for count: int in [3, 4, 5, 6]:
		for final: int in count:
			var steps: Array[Dictionary] = TumbangRoll.plan(count, final, 2.7)
			assert_eq(steps.size(), TumbangRoll.LAPS * count + final, "%d cards, stop on %d" % [count, final] )
			assert_eq(steps[steps.size() - 1]["index"], final, "the last step is the taya's card")
			assert_almost_eq(steps[steps.size() - 1]["at"] as float, 2.7, 0.0001, "ends on time")
			var gap: float = -1.0
			var last: float = 0.0
			for step: Dictionary in steps:
				var now_gap: float = (step["at"] as float) - last
				assert_gte(now_gap, gap - 0.0001, "each step takes at least as long as the one before")
				gap = now_gap
				last = step["at"] as float
	assert_true(TumbangRoll.plan(1, 0, 2.7).is_empty(), "nothing to roll with one kid")


func _roll(sim: MatchSim) -> TumbangRoll:
	var roll: TumbangRoll = autofree(TumbangRoll.new()) as TumbangRoll
	add_child(roll)
	var game: TumbangPreso = sim.mode as TumbangPreso
	roll.setup(sim, game.taya_id, 1, game.rules.roll_seconds, func(state: PlayerState) -> String: return "P%d" % state.id)
	return roll


func test_the_roll_spins_then_stops_on_the_taya_and_says_who() -> void:
	var sim: MatchSim = _sim(3)
	var roll: TumbangRoll = _roll(sim)
	var stepped: Array[int] = [0]
	var landed: Array[int] = [0]
	roll.stepped.connect(func() -> void: stepped[0] += 1)
	roll.landed.connect(func() -> void: landed[0] += 1)
	var finish: float = (sim.mode as TumbangPreso).rules.roll_seconds * TumbangRoll.FINISH_SHARE
	roll.show_at(0.0)
	assert_false(roll.is_finished())
	roll.show_at(finish * 0.5)
	assert_gt(stepped[0], 0, "cards went by")
	assert_eq(landed[0], 0)
	assert_false(roll.is_finished())
	roll.show_at(finish + 0.1)
	assert_true(roll.is_finished())
	assert_eq(landed[0], 1, "one sound for the stop")
	var taya: PlayerState = sim.players[(sim.mode as TumbangPreso).taya_id]
	assert_eq(roll.carousel.current_id(), taya.character_id, "the middle card is the taya's kid")
	assert_eq(roll.name_label.text, "P%d IS IT!" % taya.id if taya.id != 1 else "YOU'RE IT!")
	roll.show_at(finish + 1.0)
	assert_eq(landed[0], 1, "only once")


func test_the_roll_is_called_mataya_taya() -> void:
	var roll: TumbangRoll = _roll(_sim(2))
	assert_eq(roll.title_label.text, "Mataya-taya!")


func test_a_phone_that_sees_the_show_late_gets_the_finished_roll_and_reduce_motion_does_not_spin() -> void:
	var sim: MatchSim = _sim(5)
	var late: TumbangRoll = _roll(sim)
	late.show_at(4.0)
	assert_true(late.is_finished())
	assert_eq(late.carousel.current_id(), sim.players[(sim.mode as TumbangPreso).taya_id].character_id)
	var saved: bool = Settings.reduced_motion
	Settings.reduced_motion = true
	var calm: TumbangRoll = _roll(sim)
	calm.show_at(0.2)
	assert_true(calm.is_finished(), "no spin")
	Settings.reduced_motion = saved


func test_the_roll_reuses_two_of_the_matchs_sounds() -> void:
	for sound: String in [TumbangRoll.STEP_SOUND, TumbangRoll.LANDED_SOUND]:
		assert_true(ResourceLoader.exists(SfxView.DIR + sound + ".mp3"), sound)
