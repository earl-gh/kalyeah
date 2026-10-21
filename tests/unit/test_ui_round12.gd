extends GutTest
## Round 12 of the interface plan: a results card with a moment, and a hint for the tagged (the arrows are gone: the
## whole street is always on screen).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]
var _motion: bool


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(true)


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


func _tag_an_ally(practice: PracticeMatch) -> void:
	for id: int in practice.sim.players:
		var player: PlayerState = practice.sim.players[id]
		if player.team == practice.local_team and id != practice.local_id:
			practice.sim.damage(id, 9999)
			return


# ---- the results moment -----------------------------------------------------------------

func _finish(practice: PracticeMatch, win: bool) -> void:
	var team: int = practice.local_team
	practice.sim.score.points = [0, 0]
	practice.sim.score.points[team] = 2 if win else 1
	practice.sim.score.points[1 - team] = 1 if win else 2
	practice.sim.score.winner = team if win else 1 - team
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	practice.hud.sync_score(practice.sim, team, DT, practice.local_id)


func test_a_win_throws_confetti_after_the_beat_and_a_loss_never_does() -> void:
	Settings.set_reduced_motion(false)
	var win: PracticeMatch = _practice()
	_finish(win, true)
	assert_false(win.hud.results.confetti.emitting, "the street's own banner comes first")
	await wait_seconds(MatchResults.DELAY + 0.35)
	assert_true(win.hud.results.confetti.emitting, "then the confetti")
	var loss: PracticeMatch = _practice()
	_finish(loss, false)
	await wait_seconds(MatchResults.DELAY + 0.35)
	assert_false(loss.hud.results.confetti.emitting, "a loss gets no confetti")


func test_the_scores_count_up_to_the_final_numbers_and_with_reduce_motion_just_show_them() -> void:
	Settings.set_reduced_motion(true)
	var calm: PracticeMatch = _practice()
	_finish(calm, true)
	assert_eq(calm.hud.results.mine_label.text, "2")
	assert_eq(calm.hud.results.theirs_label.text, "1")
	assert_false(calm.hud.results.confetti.emitting, "no confetti with Reduce motion")
	Settings.set_reduced_motion(false)
	var lively: PracticeMatch = _practice()
	_finish(lively, true)
	assert_eq(lively.hud.results.mine_label.text, "0", "they start at zero")
	await wait_seconds(MatchResults.DELAY + MatchResults.POP + MatchResults.COUNT_SECONDS + 0.4)
	assert_eq(lively.hud.results.mine_label.text, "2")
	assert_eq(lively.hud.results.theirs_label.text, "1")


func test_clearing_the_card_stops_the_confetti() -> void:
	Settings.set_reduced_motion(false)
	var practice: PracticeMatch = _practice()
	_finish(practice, true)
	await wait_seconds(MatchResults.DELAY + 0.35)
	practice.sim.phase = MatchSim.Phase.PLAYING
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	assert_false(practice.hud.results.confetti.emitting)
	assert_false(practice.hud.results.visible)


# ---- the tagged hint --------------------------------------------------------------------

func test_the_tagged_sheet_says_how_it_ends_and_still_fits() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.damage(PracticeMatch.LOCAL_ID, 9999)
	_run(practice, 6)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	assert_eq(panel.hint_label.text, TaggedPanel.HINT_TEXT)
	assert_eq(panel.wait_label.text, TaggedPanel.WAIT_TEXT, "the title is unchanged")
	await wait_process_frames(3)
	assert_eq(panel.hint_label.get_line_count(), 1, "one short line, no orphan word")
	var band: Control = panel.find_children("*", "PanelContainer", true, false)[0] as Control
	assert_gte(band.size.y, band.get_combined_minimum_size().y - 1.0, "the sheet is tall enough for its contents")
