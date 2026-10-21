extends GutTest
## Who scores when: both teams in each other's base at once, both fully tagged at once, and a base that an enemy is
## still standing in (`capture_contested`). From the lead review: a 5 s capture was shorter than the time it takes
## to tag anyone, and team 0 won every tie.

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


func test_a_lone_base_capture_still_scores_after_the_capture_time() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	_wait(sim, _rules.capture_time - 0.5)
	assert_eq(_scored, [] as Array[int], "not before the time is up")
	_wait(sim, 1.0)
	assert_eq(_scored, [0] as Array[int])
	assert_eq(sim.score.points[0], 1)


func test_team_one_scores_a_lone_base_capture_just_the_same() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 0)
	_rush(sim, 1)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [1] as Array[int], "no team is favoured")


func test_both_teams_in_each_others_base_at_once_scores_for_nobody() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_rush(sim, 1)
	_wait(sim, _rules.capture_time * 2.0)
	assert_eq(_scored, [] as Array[int], "a mirror rush is a tie, not a point for team 0")
	assert_eq(sim.capture_team, -1)


func test_the_tie_rule_holds_with_the_contest_rule_off_too() -> void:
	_rules.capture_contested = false
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_rush(sim, 1)
	_wait(sim, _rules.capture_time * 2.0)
	assert_eq(_scored, [] as Array[int])


func test_the_tie_ends_when_one_team_leaves_and_the_other_then_scores() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_rush(sim, 1)
	_wait(sim, 3.0)
	_away(sim, 1)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [0] as Array[int])


func test_both_teams_fully_tagged_at_once_scores_for_nobody() -> void:
	var sim: MatchSim = _sim()
	for id: int in sim.players:
		sim.kill(id)
	_wait(sim, _rules.capture_time * 2.0)
	assert_eq(_scored, [] as Array[int])


func test_an_enemy_standing_in_the_base_stops_the_capture() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	assert_true(_rules.capture_contested, "on by default")
	_wait(sim, _rules.capture_time * 2.0)
	assert_eq(_scored, [] as Array[int], "a defender at home contests it")
	assert_eq(sim.capture_team, -1)
	assert_true(sim.base_contested_by(0), "the HUD can say CONTESTED")


func test_the_capture_runs_once_the_defender_is_tagged() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	_wait(sim, 2.0)
	sim.kill(20)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [0] as Array[int], "a tagged kid is in the box, not in the base")


func test_the_capture_runs_once_the_defender_walks_out() -> void:
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	_wait(sim, 2.0)
	sim.players[20].position = Vector2(0.0, 0.0)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [0] as Array[int])


func test_with_the_contest_rule_off_a_defender_in_the_base_does_not_matter() -> void:
	_rules.capture_contested = false
	var sim: MatchSim = _sim()
	_rush(sim, 0)
	_away(sim, 1)
	sim.players[20].position = LAYOUT.base_center(MapLayout.SIDE_ENEMY) + Vector2(1.5, 0.0)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [0] as Array[int])


func test_all_enemies_tagged_still_scores_and_is_not_blocked_by_the_contest_rule() -> void:
	var sim: MatchSim = _sim()
	sim.kill(20)
	sim.kill(21)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [0] as Array[int])
	assert_eq(sim.capture_kind, MatchSim.CaptureKind.NONE, "the point ended the capture")


func test_a_tagged_teammate_still_stops_a_base_capture() -> void:
	var sim: MatchSim = _sim()
	_away(sim, 1)
	_rush(sim, 0)
	sim.kill(11)
	_wait(sim, _rules.capture_time + 0.5)
	assert_eq(_scored, [] as Array[int], "every free player must be in; the tagged one needs a rescue first")
