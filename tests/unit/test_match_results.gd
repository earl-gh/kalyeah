extends GutTest
## The end-of-match card: what it says, who it takes the touches from, and that it leaves the game alone.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Settings.set_reduced_motion(_motion)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _win_the_match(practice: PracticeMatch) -> void:
	for point: int in practice.sim.score.points_to_win():
		var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
		player.position = LAYOUT.base_center(-practice.own_side())
		for i: int in ceili((practice.sim.rules.capture_solo_time + 0.3) / DT):
			practice.advance(DT)
		for i: int in 100:
			practice.advance(DT)


func test_the_card_is_hidden_while_playing() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	assert_false(practice.hud.results.visible)


func test_winning_shows_victory_the_score_your_kid_and_both_weapons() -> void:
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	var results: MatchResults = practice.hud.results
	assert_eq(practice.sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_true(results.visible)
	assert_eq(results.title.text, "Victory")
	assert_eq(results.mine_label.text, str(practice.sim.score.points_to_win()))
	assert_eq(results.theirs_label.text, "0")
	assert_eq(results.subtitle.text, "Practice (First to 3) (2v3)")
	assert_null(results.find_child("Kid", true, false), "no character panel")
	assert_null(results.find_child("Weapons", true, false), "no loadout filler")
	assert_true(results._card.get_theme_stylebox("panel") is StyleBoxEmpty, "no blue panel")


func test_losing_says_defeat_with_your_score_first() -> void:
	var practice: PracticeMatch = _practice()
	var results: MatchResults = practice.hud.results
	practice.sim.score.points[practice.local_team] = 1
	practice.sim.score.points[1 - practice.local_team] = 2
	practice.sim.score.winner = 1 - practice.local_team
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	assert_eq(results.title.text, "Defeat")
	assert_eq(results.mine_label.text, "1", "yours is on the left")
	assert_eq(results.theirs_label.text, "2")


func test_the_one_main_button_is_the_old_play_again_button_inside_the_card() -> void:
	var practice: PracticeMatch = _practice()
	var again: Button = practice.hud.again_button
	assert_true(practice.hud.results.is_ancestor_of(again), "the card's gold action")
	assert_eq(again.text, "Practice again")
	assert_true(practice.hud.results.home_button.visible, "practice also offers Home")
	var homes: Array[bool] = []
	practice.hud.results.home_pressed.connect(func() -> void: homes.append(true))
	practice.hud.results.home_button.pressed.emit()
	assert_eq(homes.size(), 1)


func test_the_card_blocks_touches_to_the_street_under_it_once_shown() -> void:
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	assert_eq(practice.hud.results.mouse_filter, Control.MOUSE_FILTER_STOP, "no stray drags into the world")


func test_with_motion_on_the_result_is_immediate_and_blocks_touches() -> void:
	Settings.set_reduced_motion(false)
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	var results: MatchResults = practice.hud.results
	assert_eq(results.mouse_filter, Control.MOUSE_FILTER_STOP, "results take touches immediately")
	assert_eq(results.modulate.a, 1.0)
	await wait_seconds(0.3)
	assert_eq(results.modulate.a, 1.0)
	assert_eq(results.mouse_filter, Control.MOUSE_FILTER_STOP)


func test_the_card_clears_when_a_new_match_starts() -> void:
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	practice.sim.phase = MatchSim.Phase.PLAYING
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	assert_false(practice.hud.results.visible)


## A sim of six kids under ids 1..6 (`sim.players` is keyed by player id, not a list index), each with a known pair.
func _six() -> MatchSim:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 6)
	for id: int in range(1, 7):
		var state: PlayerState = sim.add_player(id, 0 if id <= 3 else 1)
		state.character_id = &"junjun"
		sim.set_loadout(id, rules.weapons[0].id, rules.weapons[1].id)
	return sim


func _card_for(sim: MatchSim, local_id: int) -> MatchResults:
	var results: MatchResults = autofree(MatchResults.new()) as MatchResults
	add_child(results)
	var team: int = sim.players[local_id].team if sim.players.has(local_id) else 1
	results.set_over(true, sim, local_id, team, "Practice")
	return results


## The card looks the local kid up by id: the last id (6 of 6 kids) is a player like any other.
func test_result_counts_teams_for_a_high_player_id() -> void:
	var sim: MatchSim = _six()
	var results: MatchResults = _card_for(sim, 6)
	assert_eq(results.subtitle.text, "Practice (First to 3) (3v3)")
	assert_true(results.visible)

func test_result_counts_teams_for_a_middle_player_id() -> void:
	var sim: MatchSim = _six()
	var results: MatchResults = _card_for(sim, 3)
	assert_eq(results.subtitle.text, "Practice (First to 3) (3v3)")
	assert_null(results.find_child("Kid", true, false))

func test_an_id_with_no_player_still_shows_the_team_result() -> void:
	var sim: MatchSim = _six()
	var results: MatchResults = _card_for(sim, 99)
	assert_true(results.visible)
	assert_eq(results.subtitle.text, "Practice (First to 3) (3v3)")
