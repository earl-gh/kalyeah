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
		for i: int in ceili((practice.sim.rules.capture_time + 0.3) / DT):
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
	assert_eq(results.title.text, "VICTORY!")
	assert_eq(results.mine_label.text, str(practice.sim.score.points_to_win()))
	assert_eq(results.theirs_label.text, "0")
	assert_eq(results.subtitle.text, "Practice · First to 3")
	assert_eq(results.kid_holder.get_child_count(), 1, "your kid's face")
	assert_eq(results.weapons_row.get_child_count(), practice.sim.players[PracticeMatch.LOCAL_ID].weapons.size(), "the weapons you carried")
	assert_true(results.time_label.text.contains(":"), "the match time as m:ss")


func test_losing_says_defeat_with_your_score_first() -> void:
	var practice: PracticeMatch = _practice()
	var results: MatchResults = practice.hud.results
	practice.sim.score.points[practice.local_team] = 1
	practice.sim.score.points[1 - practice.local_team] = 2
	practice.sim.score.winner = 1 - practice.local_team
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	assert_eq(results.title.text, "DEFEAT")
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


func test_with_motion_on_the_card_waits_a_beat_for_the_street_banner_then_takes_touches() -> void:
	Settings.set_reduced_motion(false)
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	var results: MatchResults = practice.hud.results
	assert_eq(results.mouse_filter, Control.MOUSE_FILTER_IGNORE, "the banner is seen first")
	assert_lt(results.modulate.a, 1.0)
	await wait_seconds(MatchResults.DELAY + MatchResults.POP + 0.3)
	assert_eq(results.modulate.a, 1.0)
	assert_eq(results.mouse_filter, Control.MOUSE_FILTER_STOP)


func test_the_card_clears_when_a_new_match_starts() -> void:
	var practice: PracticeMatch = _practice()
	_win_the_match(practice)
	practice.sim.phase = MatchSim.Phase.PLAYING
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	assert_false(practice.hud.results.visible)
