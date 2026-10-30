extends GutTest
## The stage word: "Extended" / "Overtime" flashes in the middle when the clock's stage changes, then shrinks and flies to the caption
## of the clock at the top right, where it becomes the caption.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const CAPTION_AT: Vector2 = Vector2(620.0, 50.0)
const DT: float = 1.0 / 30.0
const REGULAR: MatchSim.ClockStage = MatchSim.ClockStage.REGULAR
const EXTENDED: MatchSim.ClockStage = MatchSim.ClockStage.EXTENDED
const OVERTIME: MatchSim.ClockStage = MatchSim.ClockStage.OVERTIME

var _flash: ClockStageFlash


func before_each() -> void:
	Settings.reduced_motion = false
	var screen: Control = autofree(Control.new()) as Control
	screen.size = Vector2(720.0, 1280.0)
	add_child(screen)
	_flash = ClockStageFlash.new()
	screen.add_child(_flash)


func after_each() -> void:
	Settings.reduced_motion = false


func _sync(stage: MatchSim.ClockStage, delta: float = 0.0, into: float = 0.0) -> void:
	_flash.sync(stage, into, delta, CAPTION_AT, Color.WHITE)


## Runs the flash for `seconds` of match time in sim ticks.
func _run(stage: MatchSim.ClockStage, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		_sync(stage, DT, 1.0)


func _word() -> Label:
	return _flash.get_child(0) as Label


func test_a_stage_that_changes_flashes_its_word_in_the_middle() -> void:
	_sync(REGULAR)
	assert_false(_flash.flying())
	_sync(EXTENDED)
	assert_true(_flash.flying())
	assert_eq(_flash.word_text(), "Extended")
	_run(EXTENDED, ClockStageFlash.POP_TIME)
	var centre: Vector2 = _word().position + _word().size / 2.0
	assert_almost_eq(centre.x, 360.0, 1.0, "across the middle of the screen")
	assert_almost_eq(centre.y, 1280.0 * ClockStageFlash.CENTER_Y, 1.0)
	assert_almost_eq(_word().scale.x, 1.0, 0.05, "at full size")


func test_it_flies_to_the_caption_and_ends_there_as_small_as_the_caption() -> void:
	_sync(REGULAR)
	_sync(EXTENDED)
	_run(EXTENDED, ClockStageFlash.HOLD_TIME + ClockStageFlash.FLY_TIME * 0.5)
	var half: Vector2 = _word().position + _word().size / 2.0
	assert_gt(half.x, 360.0, "on its way to the right")
	assert_lt(half.y, 1280.0 * ClockStageFlash.CENTER_Y, "and up")
	assert_lt(_word().scale.x, 1.0, "and shrinking")
	_run(EXTENDED, ClockStageFlash.FLY_TIME * 0.5 - DT * 2.0)
	var last: Vector2 = _word().position + _word().size / 2.0
	assert_lt(last.distance_to(CAPTION_AT), 20.0, "just short of the caption spot")
	assert_almost_eq(_word().scale.x, ClockStageFlash.END_SCALE, 0.05, "as small as the caption")
	_run(EXTENDED, DT * 4.0)
	assert_false(_flash.flying(), "the flight is over: the word is the caption now")
	assert_eq(_flash.word_text(), "")


func test_the_whole_flight_takes_about_a_second_and_a_half_the_move_under_a_second() -> void:
	assert_between(ClockStageFlash.FLY_TIME, 0.6, 0.9)
	assert_lt(ClockStageFlash.HOLD_TIME + ClockStageFlash.FLY_TIME, 1.6)


func test_overtime_flashes_the_same_way_in_its_word() -> void:
	_sync(EXTENDED)
	_sync(OVERTIME)
	assert_true(_flash.flying())
	assert_eq(_flash.word_text(), "Overtime")


func test_it_flashes_once_per_stage_change() -> void:
	_sync(REGULAR)
	_sync(EXTENDED)
	_run(EXTENDED, 3.0)
	assert_false(_flash.flying())
	_run(EXTENDED, 1.0)
	assert_false(_flash.flying(), "the same stage again does not flash")


func test_a_stage_already_running_when_first_seen_does_not_flash() -> void:
	_sync(EXTENDED)
	assert_false(_flash.flying(), "a client that joins mid-Extended: no flash")
	_run(EXTENDED, 1.0)
	assert_false(_flash.flying())


func test_a_stage_long_under_way_when_seen_change_does_not_flash() -> void:
	_sync(REGULAR)
	_sync(OVERTIME, 0.0, ClockStageFlash.FRESH_WINDOW + 1.0)
	assert_false(_flash.flying(), "a reconnect that skips ahead shows the right caption but no late flash")


func test_a_new_round_going_back_to_regular_cancels_it_and_does_not_flash() -> void:
	_sync(REGULAR)
	_sync(EXTENDED)
	_run(EXTENDED, 0.3)
	_sync(REGULAR)
	assert_false(_flash.flying())
	assert_eq(_flash.word_text(), "")


func test_reduced_motion_shows_no_flight() -> void:
	Settings.reduced_motion = true
	_sync(REGULAR)
	_sync(EXTENDED)
	assert_false(_flash.flying(), "the caption just changes")
	assert_eq(_flash.word_text(), "")


func test_the_flash_waits_while_the_match_stands_still() -> void:
	_sync(REGULAR)
	_sync(EXTENDED)
	_run(EXTENDED, 0.3)
	var at: Vector2 = _word().position
	for i: int in 300:
		_sync(EXTENDED, 0.0, 1.0)
	assert_true(_flash.flying())
	assert_eq(_word().position, at, "no match time passed: nothing moved")


func test_the_flash_only_draws_and_never_takes_a_touch() -> void:
	assert_eq(_flash.mouse_filter, Control.MOUSE_FILTER_IGNORE)
	assert_eq(_word().mouse_filter, Control.MOUSE_FILTER_IGNORE)


func test_the_big_word_is_lettered_at_the_hero_size_like_go() -> void:
	assert_eq(ClockStageFlash.FONT, UiMetrics.HERO)
	assert_eq(_word().get_theme_font_size("font_size"), UiMetrics.HERO)


# ---- in the HUD, from the sim ---------------------------------------------------------------

func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	await wait_process_frames(2)
	practice.finish_pick()
	practice.skip_intro()
	practice.sim.rules = practice.sim.rules.duplicate() as GameRules
	return practice


func test_the_hud_flashes_extended_when_the_first_minutes_end_and_the_caption_follows() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var sim: MatchSim = practice.sim
	sim.round_time = sim.rules.overtime_after - 1.0
	practice.advance(DT)
	assert_false(hud.clock_flash.flying())
	assert_eq(hud.clock_chip.caption_text(), "Time left:")
	sim.round_time = sim.rules.overtime_after
	practice.advance(DT)
	assert_true(hud.clock_flash.flying())
	assert_eq(hud.clock_flash.word_text(), "Extended")
	assert_true(hud.clock_chip.caption_hidden, "the caption waits empty for the word")
	assert_eq(hud.clock_chip.text(), "1:00", "the timer is back at a minute")
	for i: int in ceili((ClockStageFlash.HOLD_TIME + ClockStageFlash.FLY_TIME) / DT) + 2:
		sim.round_time = sim.rules.overtime_after
		practice.advance(DT)
	assert_false(hud.clock_flash.flying())
	assert_false(hud.clock_chip.caption_hidden)
	assert_eq(hud.clock_chip.caption_text(), "Extended")


func test_the_word_lands_on_the_clocks_caption() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var sim: MatchSim = practice.sim
	sim.round_time = sim.rules.overtime_after - 1.0
	practice.advance(DT)
	sim.round_time = sim.rules.overtime_after
	var lands: int = ceili((ClockStageFlash.HOLD_TIME + ClockStageFlash.FLY_TIME) / DT) - 2
	for i: int in lands:
		practice.advance(DT)
	var word: Label = hud.clock_flash.get_child(0) as Label
	var centre: Vector2 = word.get_global_rect().get_center()
	var caption: Vector2 = hud.clock_chip.get_global_transform() * hud.clock_chip.caption_center()
	assert_lt(centre.distance_to(caption), 40.0, "ends on the caption spot of the top right panel")
	assert_true(hud.clock_chip.get_global_rect().grow(20.0).has_point(centre))


func test_the_hud_flash_holds_still_behind_the_menu() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var sim: MatchSim = practice.sim
	sim.round_time = sim.rules.overtime_after - 1.0
	practice.advance(DT)
	sim.round_time = sim.rules.overtime_after
	practice.advance(DT)
	hud.menu.open()
	var word: Label = hud.clock_flash.get_child(0) as Label
	var at: Vector2 = word.position
	for i: int in 120:
		practice.advance(DT)
	assert_true(hud.clock_flash.flying())
	assert_eq(word.position, at, "the menu stops the match, and the word with it")
	hud.menu.close()


func test_the_hud_flash_never_blocks_a_touch() -> void:
	var practice: PracticeMatch = await _practice()
	assert_eq(practice.hud.clock_flash.mouse_filter, Control.MOUSE_FILTER_IGNORE)
	assert_eq(practice.hud.clock_flash.get_child(0).mouse_filter, Control.MOUSE_FILTER_IGNORE)


func test_a_point_ends_the_flash_and_the_clock_restarts_at_regular() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var sim: MatchSim = practice.sim
	sim.round_time = sim.rules.overtime_after - 1.0
	practice.advance(DT)
	sim.round_time = sim.rules.overtime_after
	practice.advance(DT)
	assert_true(hud.clock_flash.flying())
	sim.round_time = 0.0
	practice.advance(DT)
	assert_false(hud.clock_flash.flying())
	assert_eq(hud.clock_chip.caption_text(), "Time left:")
	assert_false(hud.clock_chip.caption_hidden)
