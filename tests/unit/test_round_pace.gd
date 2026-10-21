extends GutTest
## Rules that keep a round moving: overtime (the capture time drops once a round has dragged on) and a timed release from the box.
## Both are off by default (`overtime_after` 0, `tag_release_time` 0); Practice releases after `practice_tag_release_time`.

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


func test_both_are_off_in_the_shipped_rules() -> void:
	assert_eq(_rules.overtime_after, 0.0)
	assert_eq(_rules.tag_release_time, 0.0)


func test_without_overtime_the_capture_time_never_changes() -> void:
	var sim: MatchSim = _sim()
	_wait(sim, 300.0)
	assert_eq(sim.capture_needed, _rules.capture_time)
	assert_eq(sim.current_capture_time(), _rules.capture_time)


func test_overtime_shortens_the_capture_after_the_set_time_and_a_new_round_resets_it() -> void:
	_rules.overtime_after = 20.0
	_rules.overtime_capture_time = 2.0
	var sim: MatchSim = _sim()
	_wait(sim, 19.0)
	assert_eq(sim.capture_needed, _rules.capture_time)
	_wait(sim, 2.0)
	assert_eq(sim.capture_needed, 2.0)
	sim.objectives.end_freeze(sim)
	assert_eq(sim.round_time, 0.0, "a new round starts a new clock")
	assert_eq(sim.capture_needed, _rules.capture_time)


func test_overtime_never_makes_the_capture_longer() -> void:
	_rules.overtime_after = 1.0
	_rules.overtime_capture_time = 99.0
	var sim: MatchSim = _sim()
	_wait(sim, 2.0)
	assert_eq(sim.capture_needed, _rules.capture_time)


func test_a_capture_in_overtime_scores_after_the_short_time() -> void:
	_rules.overtime_after = 5.0
	_rules.overtime_capture_time = 2.0
	var sim: MatchSim = _sim()
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	_wait(sim, 6.0)
	sim.kill(3)
	sim.kill(4)
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


func test_a_tagged_player_waits_for_a_rescue_when_the_release_is_off() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	_wait(sim, 120.0)
	assert_true(sim.players[1].tagged)


func test_a_tagged_player_walks_out_alone_after_the_release_time() -> void:
	_rules.tag_release_time = 8.0
	var sim: MatchSim = _sim()
	var freed: Array[int] = []
	sim.player_rescued.connect(func(id: int, by: int) -> void: freed.append(by * 100 + id))
	sim.kill(1)
	_wait(sim, 7.0)
	assert_true(sim.players[1].tagged)
	_wait(sim, 1.5)
	assert_false(sim.players[1].tagged)
	assert_eq(sim.players[1].hp, _rules.player_max_hp, "back at the post at full health")
	assert_eq(sim.players[1].position, sim.players[1].spawn_position)
	assert_eq(freed, [MatchSim.RELEASED_ALONE * 100 + 1])


func test_practice_uses_its_own_release_time() -> void:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	assert_eq(practice.sim.tag_release_time, practice.rules.practice_tag_release_time)
	assert_gt(practice.rules.practice_tag_release_time, 0.0)
