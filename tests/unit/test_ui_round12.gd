extends GutTest
## Round 12 of the interface plan: arrows that say where to go, a results card with a moment, and a hint for
## the tagged.

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


# ---- the edge point --------------------------------------------------------------------

func test_a_target_on_screen_is_where_it_is() -> void:
	var rect: Rect2 = Rect2(70.0, 250.0, 580.0, 650.0)
	assert_eq(DirectionArrows.edge_point(Vector2(360.0, 600.0), Vector2(300.0, 400.0), rect), Vector2(300.0, 400.0))


func test_a_target_off_the_side_gets_an_arrow_on_that_edge_along_the_line_to_it() -> void:
	var rect: Rect2 = Rect2(70.0, 250.0, 580.0, 650.0)
	var origin: Vector2 = Vector2(360.0, 600.0)
	var point: Vector2 = DirectionArrows.edge_point(origin, Vector2(1360.0, 600.0), rect)
	assert_almost_eq(point.x, rect.end.x, 0.01, "the right edge")
	assert_almost_eq(point.y, 600.0, 0.01, "straight across")
	var diagonal: Vector2 = DirectionArrows.edge_point(origin, Vector2(-640.0, -400.0), rect)
	assert_almost_eq(diagonal.x, rect.position.x, 0.01, "this one leaves by the left: it is wider than it is tall")
	assert_between(diagonal.y, rect.position.y, rect.end.y)
	var slope_target: float = (-400.0 - 600.0) / (-640.0 - 360.0)
	assert_almost_eq((diagonal.y - origin.y) / (diagonal.x - origin.x), slope_target, 0.001, "on the line from you to it")
	var up: Vector2 = DirectionArrows.edge_point(origin, Vector2(360.0, -1000.0), rect)
	assert_almost_eq(up.y, rect.position.y, 0.01, "straight up leaves by the top, below the minimap row")


func test_an_origin_outside_the_rect_is_pulled_inside_first_and_a_zero_direction_is_safe() -> void:
	var rect: Rect2 = Rect2(70.0, 250.0, 580.0, 650.0)
	var point: Vector2 = DirectionArrows.edge_point(Vector2(0.0, 0.0), Vector2(2000.0, 2000.0), rect)
	assert_true(rect.grow(0.5).has_point(point), "never outside the safe area")
	assert_eq(DirectionArrows.edge_point(Vector2(100.0, 300.0), Vector2(100.0, 300.0), rect), Vector2(100.0, 300.0))


# ---- what is pointed at -----------------------------------------------------------------

func test_while_playing_and_free_the_enemy_base_is_pointed_at() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var found: Array[Dictionary] = practice.hud.arrows.targets()
	assert_eq(found.size(), 1)
	assert_eq(found[0]["kind"], &"base")
	assert_eq(found[0]["world"], practice.layout.base_center(-practice.own_side()), "their post, not yours")


func test_a_tagged_teammate_adds_a_rescue_arrow_toward_your_own_box_and_a_tagged_enemy_does_not() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9999)
	_run(practice, 3)
	assert_eq(practice.hud.arrows.targets().size(), 1, "an enemy tagged is not something to rescue")
	_tag_an_ally(practice)
	_run(practice, 3)
	var found: Array[Dictionary] = practice.hud.arrows.targets()
	assert_eq(found.size(), 2)
	assert_eq(found[1]["kind"], &"rescue")
	assert_eq(found[1]["world"], practice.layout.jail_rect(practice.own_side()).get_center())


func test_no_arrows_when_you_are_tagged() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.damage(PracticeMatch.LOCAL_ID, 9999)
	_run(practice, 3)
	assert_eq(practice.hud.arrows.targets().size(), 0, "tagged: the tagged sheet says what to do")


# ---- what is drawn ----------------------------------------------------------------------

func test_the_arrow_names_its_target_and_the_distance_and_stays_in_the_safe_area() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.hud.arrows.tick(DT)
	var arrow: Dictionary = practice.hud.arrows.arrows[0]
	var me: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var enemy_post: Vector2 = practice.layout.base_center(-practice.own_side())
	assert_eq(arrow["metres"], roundi(me.position.distance_to(enemy_post)))
	assert_eq(arrow["label"], "Enemy base · %d m" % arrow["metres"])
	var hud: Control = practice.hud.arrows
	var safe: Rect2 = Rect2(Vector2(DirectionArrows.INSET.x, DirectionArrows.INSET.y), hud.size - Vector2(DirectionArrows.INSET.x + DirectionArrows.INSET.z, DirectionArrows.INSET.y + DirectionArrows.INSET.w))
	assert_true(safe.grow(1.0).has_point(arrow["point"] as Vector2), "clear of the minimap, the score and the controls")
	assert_eq(hud.mouse_filter, Control.MOUSE_FILTER_IGNORE, "never takes a touch")


func test_the_arrow_fades_out_when_its_target_is_on_screen() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	for i: int in 30:
		practice.hud.arrows.tick(DT)
	assert_between(practice.hud.arrows.arrows[0]["alpha"] as float, 0.0, 1.0)
	var me: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	me.position = practice.layout.base_center(-practice.own_side()) + Vector2(-1.0, 3.0)
	practice._snap_camera = true
	_run(practice, 6)
	for i: int in 30:
		practice.hud.arrows.tick(DT)
	assert_eq(practice.hud.arrows.arrows[0]["alpha"], 0.0, "at the base the arrow is gone")


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
