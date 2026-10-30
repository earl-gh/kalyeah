extends GutTest
## An enemy taking your base: a warning triangle in the middle of the scoreboard and two lines under its row, instead of big words in the
## middle of the screen. Your own capture keeps its words in the middle.

const DT: float = 1.0 / 30.0


var _kind: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	# a match against bots: the enemies are real players of the sim (training dummies never capture)
	Session.practice_kind = &"match"


func after_each() -> void:
	Session.practice_kind = _kind


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _enemy_in_our_base(practice: PracticeMatch) -> void:
	var own_base: Vector2 = practice.layout.base_center(practice.own_side())
	for id: int in practice.sim.players:
		if practice.sim.players[id].team != practice.local_team:
			practice.sim.players[id].position = own_base
			return


func test_an_enemy_in_your_base_shows_the_triangle_and_the_two_lines_not_the_middle_words() -> void:
	var practice: PracticeMatch = _practice()
	_enemy_in_our_base(practice)
	practice.advance(DT)
	practice.advance(DT)
	var alert: CaptureAlert = practice.hud.capture_alert
	assert_ne(practice.sim.capture_team, practice.local_team)
	assert_true(alert.visible, "the warning shows")
	assert_false(practice.hud.capture_label.visible, "and the big words in the middle of the screen are gone")
	assert_eq(alert.small_label.text, "Enemy capturing base")
	assert_eq(alert.big_label.text, "DEFEND THE BASE!")
	assert_gt(alert.big_label.get_theme_font_size("font_size"), alert.small_label.get_theme_font_size("font_size"), "line 2 is the large one")
	await wait_process_frames(2)
	var score: Rect2 = practice.hud.scoreboard.get_global_rect()
	var icon: Rect2 = alert.icon.get_global_rect()
	assert_almost_eq(icon.get_center().x, score.get_center().x, 1.5, "the triangle is in the middle of the scoreboard")
	assert_almost_eq(icon.get_center().y, score.get_center().y, 1.5, "on its row")
	assert_gte(alert.small_label.get_global_rect().position.y, score.end.y, "the words are below the scoreboard row")
	assert_gt(alert.big_label.get_global_rect().position.y, alert.small_label.get_global_rect().position.y, "line 1 over line 2")
	assert_eq(alert.mouse_filter, Control.MOUSE_FILTER_IGNORE)


func test_the_warning_goes_when_the_enemy_leaves_and_your_own_capture_keeps_its_middle_words() -> void:
	var practice: PracticeMatch = _practice()
	_enemy_in_our_base(practice)
	practice.advance(DT)
	practice.advance(DT)
	assert_true(practice.hud.capture_alert.visible)
	var enemy_base: Vector2 = practice.layout.base_center(-practice.own_side())
	for id: int in practice.sim.players:
		if practice.sim.players[id].team != practice.local_team:
			practice.sim.players[id].position = Vector2(0.0, 0.0)
	practice.sim.players[practice.local_id].position = enemy_base
	practice.advance(DT)
	practice.advance(DT)
	assert_false(practice.hud.capture_alert.visible, "nobody is taking your base now")
	assert_eq(practice.sim.capture_team, practice.local_team)
	assert_true(practice.hud.capture_label.visible, "your own capture keeps its words in the middle")
	assert_string_contains(practice.hud.capture_label.text, "HOLD THE BASE!")


func test_a_capture_that_stops_for_a_moment_keeps_its_words_and_warning_so_they_do_not_blink() -> void:
	var practice: PracticeMatch = _practice()
	_enemy_in_our_base(practice)
	practice.advance(DT)
	practice.advance(DT)
	assert_true(practice.hud.capture_alert.visible)
	var base_spot: Vector2 = practice.layout.base_center(practice.own_side())
	var capturers: Array[int] = []
	for id: int in practice.sim.players:
		if practice.sim.players[id].team != practice.local_team and practice.layout.in_base(practice.own_side(), practice.sim.players[id].position):
			capturers.append(id)
			practice.sim.players[id].position = Vector2.ZERO  # steps out for a moment
	practice.advance(DT)
	assert_eq(practice.sim.capture_team, -1, "the capture really stopped")
	assert_true(practice.hud.capture_alert.visible, "the warning stays up through a short gap")
	for id: int in capturers:
		practice.sim.players[id].position = base_spot  # and back in
	practice.advance(DT)
	assert_true(practice.hud.capture_alert.visible, "and never went out")
	for id: int in capturers:
		practice.sim.players[id].position = Vector2.ZERO
	for i: int in 30:  # a long gap: nobody is taking the base
		practice.advance(DT)
	assert_false(practice.hud.capture_alert.visible, "after the gap the warning goes")
