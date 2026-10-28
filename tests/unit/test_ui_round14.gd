extends GutTest
## Round 14 of the interface plan: awareness. A breath on the screen edge when you are low, a bar on the score card
## while a capture runs.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]
var _motion: bool


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(false)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _run(practice: PracticeMatch, frames: int) -> void:
	for i: int in frames:
		practice.advance(DT)


func _pulse() -> HurtPulse:
	var pulse: HurtPulse = autofree(HurtPulse.new()) as HurtPulse
	add_child(pulse)
	return pulse


func _tick(pulse: HurtPulse, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		pulse.tick(DT)


# ---- low health --------------------------------------------------------------------------

func test_below_a_third_of_your_health_the_edge_breathes_until_you_are_healed() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(30, 100, true)
	_tick(pulse, 1.0)
	assert_true(pulse.low_active(), "30 of 100 is low")
	var seen: Array[float] = []
	for i: int in int(HurtPulse.LOW_PERIOD / DT):
		pulse.tick(DT)
		seen.append(pulse.low)
	assert_lt(seen.min(), HurtPulse.LOW_MAX * 0.7, "it breathes out")
	assert_gt(seen.max(), HurtPulse.LOW_MAX * 0.9, "and in")
	assert_lte(seen.max(), HurtPulse.LOW_MAX + 0.001)
	assert_gte(seen.min(), HurtPulse.LOW_MIN - 0.001)
	pulse.sync_health(80, 100, true)
	_tick(pulse, 0.5)
	assert_false(pulse.low_active(), "healed: gone")


func test_a_healthy_player_has_no_breath() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(31, 100, true)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active())


func test_no_breath_when_tagged_or_out_of_play() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(0, 100, true)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active(), "tagged kids have no health")
	pulse.sync_health(10, 100, false)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active(), "not while the match is not being played")


func test_reduce_motion_makes_the_breath_a_steady_faint_edge() -> void:
	Settings.set_reduced_motion(true)
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(10, 100, true)
	_tick(pulse, 1.0)
	for i: int in 20:
		pulse.tick(DT)
		assert_almost_eq(pulse.low, HurtPulse.LOW_STILL, 0.001)


func test_a_hit_over_the_breath_shows_the_stronger_of_the_two() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(10, 100, true)
	_tick(pulse, 1.0)
	pulse.hit(40, 100)
	assert_gt(pulse.strength, pulse.low, "the hit glow is the stronger")
	_tick(pulse, HurtPulse.HIT_TIME + 0.1)
	assert_false(pulse.active())
	assert_true(pulse.low_active(), "and the breath is still there when it fades")


func test_the_match_turns_the_breath_on_at_low_health() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var hp: int = practice.sim.players[practice.local_id].hp
	practice.sim.damage(practice.local_id, hp - 10)
	_run(practice, 3)
	await wait_process_frames(30)
	assert_true(practice.hud.pulse.low_active())


# ---- the capture bar ---------------------------------------------------------------------

func test_the_score_card_fills_while_a_capture_runs_blue_for_you_red_for_them() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var card: Scoreboard = practice.hud.scoreboard
	assert_eq(card.capture, -1.0, "nothing is being captured")
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = practice.sim.rules.capture_solo_time / 2.0
	card.show_capture(practice.sim, practice.local_team)
	assert_almost_eq(card.capture, 0.5, 0.021)
	assert_true(card.capture_ours)
	practice.sim.capture_team = 1 - practice.local_team
	card.show_capture(practice.sim, practice.local_team)
	assert_false(card.capture_ours, "theirs is red")
	practice.sim.capture_team = -1
	card.show_capture(practice.sim, practice.local_team)
	assert_eq(card.capture, -1.0, "gone when it ends")


func test_the_capture_bar_is_not_shown_in_a_freeze() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = 2.0
	practice.sim.phase = MatchSim.Phase.POINT_FREEZE
	practice.hud.scoreboard.show_capture(practice.sim, practice.local_team)
	assert_eq(practice.hud.scoreboard.capture, -1.0)


func test_the_capture_bar_does_not_change_the_cards_size_or_put_anything_under_it() -> void:
	var practice: PracticeMatch = _practice()
	var card: Scoreboard = practice.hud.scoreboard
	var before: Vector2 = card.size
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = 2.0
	card.show_capture(practice.sim, practice.local_team)
	assert_eq(card.size, before, "the bar is inside the card (nothing sits under it)")


# ---- the skin warm list covers a match too ------------------------------------------------

func test_after_warming_the_match_screens_draw_nothing_new() -> void:
	UiSkin._cache.clear()
	UiSkin._warmed = 0
	while not UiSkin.warm_next():
		pass
	var warmed: Array = UiSkin._cache.keys()
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.damage(practice.local_id, 9999)
	_run(practice, 6)
	practice.hud.show_banner("GO!")
	practice.hud.toggle_menu()
	await wait_process_frames(3)
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	await wait_process_frames(3)
	var extra: Array[String] = []
	for key: String in UiSkin._cache.keys():
		if not warmed.has(key):
			extra.append(key)
	assert_eq(extra, [] as Array[String], "these were drawn when the match opened instead of during loading: add them to WARM_PLATES")
