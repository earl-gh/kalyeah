extends GutTest
## Who scores when: free players standing in the enemy base (one alone takes `capture_solo_time`, each teammate in it
## speeds the count up), both teams in each other's base at once, both fully tagged at once, and a base that an enemy is
## still standing in. Defenders never block capture, and a team wipe scores immediately.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _scored: Array[int] = []


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_scored = []


func _sim(size: int = 2) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	for slot: int in size:
		sim.add_player(10 + slot, 0)
		sim.add_player(20 + slot, 1)
	sim.point_scored.connect(func(team: int, _how: int) -> void: _scored.append(team))
	return sim


## Seconds a base capture takes with `n` free players in the base (docs/GDD.md, Capture).
func _base_time(n: int) -> float:
	return _rules.capture_solo_time / (1.0 + float(n - 1) * _rules.capture_ally_bonus)


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


## Everyone of `team` stands in the enemy base, spread a little.
func _rush(sim: MatchSim, team: int) -> void:
	var centre: Vector2 = LAYOUT.base_center(-sim.side_for_team(team))
	var index: int = 0
	for id: int in sim.players:
		if sim.players[id].team == team:
			sim.players[id].position = centre + Vector2(float(index) - 0.5, 0.0)
			index += 1


## Everyone of `team` out in the middle of the street, nowhere near either base.
func _away(sim: MatchSim, team: int) -> void:
	var index: int = 0
	for id: int in sim.players:
		if sim.players[id].team == team:
			sim.players[id].position = Vector2(float(index) * 2.0 - 1.0, 0.0)
			index += 1


func test_a_base_capture_scores_after_the_time_for_the_players_in_the_base() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	_wait(sim, _base_time(2) - 0.5)
	assert_eq(_scored, [] as Array[int], "not before the time is up")
	_wait(sim, 1.0)
	assert_eq(_scored, [0] as Array[int])
	assert_eq(sim.score.points[0], 1)


func test_one_player_alone_takes_the_solo_time_which_is_long_but_not_too_long() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_away(sim, 0)
	sim.players[10].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_eq(_rules.capture_solo_time, 12.0)
	assert_gt(_base_time(1), _base_time(3), "longer than with the team")
	assert_lt(_rules.capture_solo_time, 15.0, "but not endless")
	_wait(sim, _rules.capture_solo_time - 0.5)
	assert_eq(_scored, [] as Array[int])
	_wait(sim, 1.0)
	assert_eq(_scored, [0] as Array[int])


func test_every_teammate_in_the_base_makes_the_capture_faster() -> void:
	var times: Array[float] = [_base_time(1), _base_time(2), _base_time(3)]
	assert_gt(times[0], times[1])
	assert_gt(times[1], times[2])
	assert_almost_eq(times[2], 4.8, 0.05, "the whole team of three is about the old five seconds")
	_rules.capture_ally_bonus = 1.0
	assert_almost_eq(_base_time(2), _rules.capture_solo_time / 2.0, 0.001, "a bonus of 1 is linear")
	_rules.capture_ally_bonus = 0.0
	assert_almost_eq(_base_time(3), _rules.capture_solo_time, 0.001, "a bonus of 0 means no help")


func test_a_teammate_leaving_slows_the_count_without_losing_the_progress() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	_wait(sim, _base_time(2) / 2.0)
	var half: float = sim.capture_elapsed
	assert_almost_eq(half / sim.capture_needed, 0.5, 0.05, "half way")
	sim.players[11].position = Vector2(0.0, 0.0)
	sim.step(DT)
	assert_almost_eq(sim.capture_rate, 1.0, 0.001, "now one player's speed")
	assert_gt(sim.capture_elapsed, half, "the progress is kept")
	_wait(sim, _rules.capture_solo_time * 0.5 + 0.5)
	assert_eq(_scored, [0] as Array[int], "the rest took the solo speed")


func test_the_time_left_follows_the_speed_of_the_count() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	sim.step(DT)
	assert_almost_eq(sim.capture_time_left(), _base_time(2) - DT, 0.05, "the HUD counts real seconds, not units")


func test_team_one_scores_a_lone_base_capture_just_the_same() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 0)
	_rush(sim, 1)
	_wait(sim, _base_time(2) + 0.5)
	assert_eq(_scored, [1] as Array[int], "no team is favoured")


func test_both_teams_in_each_others_base_at_once_scores_for_nobody() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_rush(sim, 1)
	_wait(sim, _rules.capture_solo_time)
	assert_eq(_scored, [] as Array[int], "a mirror rush is a tie, not a point for team 0")
	assert_eq(sim.capture_team, -1)





func test_the_tie_ends_when_one_team_leaves_and_the_other_then_scores() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_rush(sim, 1)
	_wait(sim, 3.0)
	_away(sim, 1)
	_wait(sim, _base_time(2) + 0.5)
	assert_eq(_scored, [0] as Array[int])


func test_both_teams_fully_tagged_at_once_scores_for_nobody() -> void:
	var sim: MatchSim = _sim()
	for id: int in sim.players:
		sim.kill(id)
	_wait(sim, _rules.capture_solo_time * 2.0)
	assert_eq(_scored, [] as Array[int])


func test_a_defender_in_the_base_does_not_block_or_reset_capture() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	_wait(sim, 2.0)
	var progress: float = sim.capture_elapsed
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	sim.step(DT)
	assert_eq(sim.capture_team, 0)
	assert_gt(sim.capture_elapsed, progress, "a defender cannot reset progress")
	_wait(sim, _base_time(2))
	assert_eq(_scored, [0] as Array[int])


func test_the_capture_runs_once_the_defender_is_tagged() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	_wait(sim, 2.0)
	sim.kill(20)
	_wait(sim, _base_time(2) + 0.5)
	assert_eq(_scored, [0] as Array[int], "a tagged kid is on the sidewalk, not in the base")


func test_the_capture_runs_once_the_defender_walks_out() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	_wait(sim, 2.0)
	sim.players[20].position = Vector2(0.0, 0.0)
	_wait(sim, _base_time(2) + 0.5)
	assert_eq(_scored, [0] as Array[int])





func test_all_enemies_tagged_scores_immediately() -> void:
	var sim: MatchSim = _sim()
	sim.kill(20)
	sim.kill(21)
	sim.step(DT)
	assert_eq(_scored, [0] as Array[int])
	assert_eq(sim.capture_kind, MatchSim.CaptureKind.NONE, "the point ended the capture")


func test_a_tagged_teammate_does_not_stop_the_free_one_capturing_alone() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	sim.kill(11)
	_wait(sim, _rules.capture_solo_time - 0.5)
	assert_eq(_scored, [] as Array[int], "alone it takes the solo time")
	_wait(sim, 1.0)
	assert_eq(_scored, [0] as Array[int])
