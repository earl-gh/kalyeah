extends GutTest
## Rules that keep a round moving: overtime (the capture time drops once a round has dragged on), off by default (`overtime_after`
## 0). There is no timed release from the box (owner, 2026-10-29): only a teammate frees a tagged player.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 4)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	sim.add_player(3, 1)
	sim.add_player(4, 1)
	return sim


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func test_the_shipped_rules_give_a_round_a_time_so_the_clock_can_count_down() -> void:
	assert_eq(_rules.overtime_after, 180.0, "three minutes, then overtime (owner: Time left counts down)")
	assert_lt(_rules.overtime_capture_time, _rules.capture_solo_time, "overtime makes a capture shorter")


func test_without_overtime_the_capture_time_never_changes() -> void:
	_rules.overtime_after = 0.0
	var sim: MatchSim = _sim()
	_wait(sim, 300.0)
	assert_eq(sim.capture_needed, _rules.capture_solo_time)
	assert_eq(sim.current_base_capture_time(), _rules.capture_solo_time)


func test_overtime_shortens_the_capture_after_the_set_time_and_a_new_round_resets_it() -> void:
	_rules.overtime_after = 20.0
	_rules.overtime_capture_time = 2.0
	var sim: MatchSim = _sim()
	_wait(sim, 19.0)
	assert_eq(sim.capture_needed, _rules.capture_solo_time)
	_wait(sim, 2.0)
	assert_eq(sim.capture_needed, 2.0)
	sim.objectives.end_freeze(sim)
	assert_eq(sim.round_time, 0.0, "a new round starts a new clock")
	assert_eq(sim.capture_needed, _rules.capture_solo_time)


func test_overtime_never_makes_the_capture_longer() -> void:
	_rules.overtime_after = 1.0
	_rules.overtime_capture_time = 99.0
	var sim: MatchSim = _sim()
	_wait(sim, 2.0)
	assert_eq(sim.capture_needed, _rules.capture_solo_time)


func test_a_capture_in_overtime_scores_after_the_short_time() -> void:
	_rules.overtime_after = 5.0
	_rules.overtime_capture_time = 2.0
	var sim: MatchSim = _sim()
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	_wait(sim, 6.0)
	sim.players[1].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	sim.players[3].position = Vector2.ZERO
	sim.players[4].position = Vector2(2.0, 0.0)
	_wait(sim, 1.5)
	assert_eq(scored, [] as Array[int])
	_wait(sim, 1.0)
	assert_eq(scored, [0] as Array[int], "2 s, not 5")


func test_the_needed_time_travels_in_the_snapshot() -> void:
	_rules.overtime_after = 1.0
	_rules.overtime_capture_time = 2.0
	var sim: MatchSim = _sim()
	_wait(sim, 2.0)
	var mirror: MatchSim = _sim()
	Snapshot.apply(mirror, Snapshot.capture(sim, {} as Dictionary[int, int]), 1)
	assert_eq(mirror.capture_needed, 2.0)


func test_a_tagged_player_never_walks_out_alone() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	_wait(sim, 120.0)
	assert_true(sim.players[1].tagged)


func test_the_hud_shows_capture_progress_with_a_defender_present() -> void:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	var enemy_base: Vector2 = practice.layout.base_center(-practice.own_side())
	practice.sim.players[practice.local_id].position = enemy_base
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = enemy_base + Vector2(1.5, 0.0)
	practice.advance(DT)
	assert_eq(practice.sim.capture_team, practice.local_team)
	assert_true(practice.hud.capture_label.visible)
	assert_string_contains(practice.hud.capture_label.text, "HOLD THE BASE!")
	assert_false(practice.hud.capture_label.text.contains("CONTESTED"))


func test_the_match_remembers_how_every_point_was_won() -> void:
	var sim: MatchSim = _sim()
	sim.kill(3)
	sim.kill(4)
	_wait(sim, _rules.capture_solo_time + 0.5)
	assert_eq(sim.point_log, [Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED)] as Array[Vector2i])
	sim.objectives.end_freeze(sim)
	var base: Vector2 = LAYOUT.base_center(MapLayout.SIDE_OWN)
	sim.players[3].position = base
	sim.players[4].position = base + Vector2(1.0, 0.0)
	sim.players[1].position = Vector2(0.0, 0.0)
	sim.players[2].position = Vector2(2.0, 0.0)
	_wait(sim, _rules.capture_solo_time + 0.5)
	assert_eq(sim.point_log.size(), 2)
	assert_eq(sim.point_log[1], Vector2i(1, MatchSim.CaptureKind.BASE))


func test_the_point_log_travels_in_the_snapshot() -> void:
	var sim: MatchSim = _sim()
	sim.point_log = [Vector2i(0, 1), Vector2i(1, 0)] as Array[Vector2i]
	var mirror: MatchSim = _sim()
	Snapshot.apply(mirror, Snapshot.capture(sim, {} as Dictionary[int, int]), 1)
	assert_eq(mirror.point_log, sim.point_log)


func test_the_results_card_says_how_each_point_was_won() -> void:
	var sim: MatchSim = _sim()
	sim.point_log = [Vector2i(0, MatchSim.CaptureKind.BASE), Vector2i(1, MatchSim.CaptureKind.ALL_TAGGED), Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED)] as Array[Vector2i]
	sim.score.award_point(0)
	sim.score.award_point(1)
	sim.score.award_point(0)
	var card: MatchResults = autofree(MatchResults.new()) as MatchResults
	add_child(card)
	card.set_over(true, sim, 1, 0, "Practice")
	assert_null(card.find_child("How", true, false), "the final result keeps only the summary")
	assert_eq(MatchResults.how_text(true, MatchSim.CaptureKind.BASE), "Held their base")
	assert_eq(MatchResults.how_text(false, MatchSim.CaptureKind.ALL_TAGGED), "Tagged you all")
	card.set_over(false, sim, 1, 0, "Practice")
	var none: MatchSim = _sim()
	card.set_over(true, none, 1, 0, "Practice")
	assert_null(card.find_child("How", true, false))
