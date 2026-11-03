extends GutTest
## The briefing a Practice match opens with: the game's name, how to score, how to move and act, a countdown, and the match standing
## still behind it (replaces the first-run walkthrough).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _kind: StringName
var _game: StringName
var _pending: bool
var _motion: bool


func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game
	_pending = Session.briefing_pending
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game
	Session.briefing_pending = _pending
	Settings.set_reduced_motion(_motion)


## A Practice match against bots whose pick is over, so it opens as the game does.
func _match(game: StringName, pending: bool = true) -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = game
	Session.briefing_pending = pending
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	return practice


func _touch() -> InputEventScreenTouch:
	var event: InputEventScreenTouch = InputEventScreenTouch.new()
	event.pressed = true
	return event


func _texts(node: Node) -> Array[String]:
	var found: Array[String] = []
	for label: Node in node.find_children("*", "Label", true, false):
		found.append((label as Label).text)
	return found


func test_agawan_base_says_its_name_how_to_score_and_the_two_controls() -> void:
	var words: Dictionary = MatchBriefing.words_for(null)
	assert_eq(words["title"], "Agawang Base")
	assert_eq(words["goal"], [["", "Capture the enemy base or tag all enemies to score. Don't forget to rescue your teammates."]])
	assert_eq(words["rows"], [["Move", "Tap to move, or use gyroscope"], ["Attack", "Tap to cast skills, drag to aim"]])


func test_the_street_games_say_their_own_controls() -> void:
	var tumbang: Dictionary = MatchBriefing.words_for(StreetMode.rules_for(&"tumbang_preso"))
	assert_eq(tumbang["title"], "Tumbang Preso")
	assert_eq((tumbang["rows"] as Array)[1][0], "Throw")
	assert_eq((tumbang["rows"] as Array)[2], ["Kick", "Tap beside the can to kick it"], "Kick comes right below Throw")
	assert_eq(tumbang["goal"], [["", "Knock the can over, fetch your slipper and run to the other end to score. Don't get caught."]])
	var patintero: Dictionary = MatchBriefing.words_for(StreetMode.rules_for(&"patintero"))
	assert_eq(patintero["title"], "Patintero")
	assert_eq((patintero["rows"] as Array)[1][0], "Dive")
	assert_eq(patintero["goal"], [["Runners", "Get past every guard and back to base to score. Don't get caught."], ["Guards", "Tag all three runners to score."]])
	for words: Dictionary in [tumbang, patintero]:
		assert_eq((words["rows"] as Array)[0][1], "Push the knob to run", "the street games are run with the knob, not by tapping the street")


func test_a_match_against_bots_opens_with_the_briefing_in_every_game() -> void:
	for game: StringName in [&"street", &"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _match(game)
		var briefing: MatchBriefing = practice.hud.briefing
		assert_true(briefing.running, "%s opens with it" % game)
		assert_true(briefing.visible)
		assert_false(Session.briefing_pending, "it opens once")
		assert_false(practice.hud.controls_active(), "no stray touch reaches the match behind it")
		assert_eq(briefing.title_label.text, MatchBriefing.words_for(StreetMode.rules_for(game))["title"])


func test_without_the_flag_a_match_starts_clean() -> void:
	var practice: PracticeMatch = _match(&"street", false)
	assert_false(practice.hud.briefing.running)
	assert_false(practice.hud.briefing.visible)


func test_the_match_stands_still_behind_it_and_runs_when_it_ends() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice.advance(DT)  # (a held match takes one neutral tick on the way in)
	var tick: int = practice.sim.tick
	for i: int in 60:
		practice.advance(DT)
	assert_eq(practice.sim.tick, tick, "nothing moves while the briefing is up")
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO, "the opening show waits for it")
	practice.hud.briefing.finish()
	for i: int in 5:
		practice.advance(DT)
	assert_gt(practice.sim.tick, tick, "and the match runs once it is over")
	assert_true(practice.hud.controls_active())


func test_it_counts_ten_seconds_down_in_whole_numbers_and_ends_by_itself() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(practice.rules.briefing_seconds, 10.0, "ten seconds, from the rules data")
	assert_eq(briefing.shown_seconds(), 10)
	briefing._process(1.0)
	assert_eq(briefing.shown_seconds(), 9)
	briefing._process(0.5)
	assert_eq(briefing.shown_seconds(), 9, "a number holds until its second is over")
	briefing._process(7.6)
	assert_eq(briefing.shown_seconds(), 1)
	assert_true(briefing.running)
	var ended: Array[bool] = []
	briefing.finished.connect(func() -> void: ended.append(true))
	briefing._process(1.0)
	assert_eq(ended.size(), 1, "at zero it ends by itself")
	assert_false(briefing.visible)
	assert_true(practice.hud.controls_active())






func test_it_is_text_only_with_a_dimmed_unblurred_backdrop_and_a_smaller_block() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.find_children("*", "BaseButton", true, false).size(), 0, "no buttons")
	var backdrop: ColorRect = briefing.find_child("Backdrop", true, false) as ColorRect
	assert_null(backdrop.material, "the match behind is not blurred")
	assert_gt(backdrop.color.a, 0.5, "only dimmed")
	assert_eq((briefing.find_child("Column", true, false) as Control).scale, Vector2.ONE * 0.8, "the block is drawn at 0.8 of its size")
	assert_eq(briefing.mouse_filter, Control.MOUSE_FILTER_STOP)
	var texts: Array[String] = _texts(briefing)
	assert_true(texts.has("MOVE") and texts.has("ATTACK"))
	assert_true(texts.has("Tap to move, or use gyroscope"))
	for label: Node in briefing.find_children("*", "Label", true, false):
		assert_gte((label as Label).get_theme_font_size("font_size"), UiMetrics.MIN_TEXT, (label as Label).text)


func test_online_matches_never_open_it() -> void:
	Session.briefing_pending = true
	var source: String = FileAccess.get_file_as_string("res://scripts/view/match_loadout_flow.gd")
	assert_true(source.contains("Session.briefing_pending and not owner.online"), "a server does not wait for a screen")


func test_the_first_run_walkthrough_is_gone() -> void:
	assert_false(ResourceLoader.exists("res://scripts/ui/hud/coach_marks.gd"))
	assert_false(ResourceLoader.exists("res://shaders/coach_spot.gdshader"))
	assert_false(ResourceLoader.exists("res://shaders/briefing_blur.gdshader"), "and no blur shader either")
	assert_false("coach_seen" in Session)


func test_the_taya_roll_waits_for_the_briefing() -> void:
	var practice: PracticeMatch = _match(&"tumbang_preso")
	practice.advance(DT)
	assert_null(practice._presentation._roll, "no roll over the briefing")
	practice.hud.briefing.finish()
	practice.advance(DT)
	assert_not_null(practice._presentation._roll, "it starts when the briefing ends")


func test_a_tap_never_skips_tumbang_presos_roll_but_still_skips_patinteros_opening_show() -> void:
	var tap: InputEventScreenTouch = InputEventScreenTouch.new()
	tap.pressed = true
	var tumbang: PracticeMatch = _match(&"tumbang_preso")
	tumbang.hud.briefing.finish()
	tumbang.advance(DT)
	assert_eq(tumbang.sim.phase, MatchSim.Phase.INTRO)
	tumbang._input(tap)
	tumbang.advance(DT)
	assert_eq(tumbang.sim.phase, MatchSim.Phase.INTRO, "the roll plays on: a tap does not skip it (owner)")
	assert_not_null(tumbang._presentation._roll)
	var patintero: PracticeMatch = _match(&"patintero")
	patintero.hud.briefing.finish()
	patintero.advance(DT)
	assert_eq(patintero.sim.phase, MatchSim.Phase.INTRO)
	patintero._input(tap)
	assert_eq(patintero.sim.phase, MatchSim.Phase.PLAYING, "Patintero's opening show still skips on a tap")


func test_the_briefing_draws_over_tumbang_presos_points_column() -> void:
	var practice: PracticeMatch = _match(&"tumbang_preso")
	var hud: MatchHud = practice.hud
	assert_gt(hud.briefing.get_index(), hud.score_column.get_index(), "the column is blurred with the rest")




func test_the_headings_are_between_the_title_and_the_plain_text_in_size() -> void:
	var practice: PracticeMatch = _match(&"patintero")
	var briefing: MatchBriefing = practice.hud.briefing
	var title_size: int = briefing.title_label.get_theme_font_size("font_size")
	var found: Array[String] = []
	for label: Node in briefing.find_children("*", "Label", true, false):
		var text: String = (label as Label).text
		if text in ["MOVE", "DIVE", "RUNNERS", "GUARDS"]:
			found.append(text)
			var size: int = (label as Label).get_theme_font_size("font_size")
			assert_lt(size, title_size, "%s is smaller than the title" % text)
			assert_gt(size, UiMetrics.TITLE, "%s is bigger than the plain text" % text)
	assert_eq(found.size(), 4, "Patintero has two descriptions with headings, Runners and Guards, and two controls")


func test_the_title_and_its_description_stand_close_together() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	var column: Control = briefing.find_child("Column", true, false) as Control
	assert_lt(briefing.head.get_theme_constant("separation"), column.get_theme_constant("separation") / 4, "much closer than the blocks are")
	assert_eq(briefing.title_row.get_parent(), briefing.head)
	assert_eq(briefing.goal.get_parent(), briefing.head)




func test_letting_the_countdown_run_out_leaves_nothing_in_the_header() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice.hud.briefing._process(11.0)
	assert_false(practice.hud.briefing.running)


func test_agawang_base_is_spelled_with_the_g_everywhere_a_player_reads_it() -> void:
	assert_eq(RoomService.BASE_TITLE, "Agawang Base")
	assert_eq(HotspotRoomView.MAP_NAMES[&"street"], "Agawang Base")
	assert_eq(MatchBriefing.words_for(null)["title"], "Agawang Base")


func test_tap_to_continue_starts_the_match_at_once_and_leaves_nothing_in_the_header() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.continue_label.text, "Tap to continue")
	assert_true(briefing.continue_label.is_visible_in_tree())
	assert_eq(briefing.mouse_filter, Control.MOUSE_FILTER_STOP)
	practice._input(_touch())
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO, "the tap that ends the briefing does not also skip the opening show")
	briefing._process(3.0)
	var ended: Array[bool] = []
	briefing.finished.connect(func() -> void: ended.append(true))
	briefing._gui_input(_touch())
	assert_eq(ended.size(), 1, "a tap ends it at once, with seconds left")
	assert_false(briefing.running)
	assert_false(briefing.visible)
	assert_true(practice.hud.controls_active(), "the controls are back")
	assert_null(practice.hud.get("skip_timer"), "and no countdown carries on between the scores")
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO, "the opening show still plays after it")


func test_android_back_starts_the_match_instead_of_opening_the_menu() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice._back_pressed()
	assert_false(practice.hud.menu.visible, "back does not open the menu over the briefing")
	practice.hud.briefing.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(1)
	assert_false(practice.hud.briefing.running)


func test_the_countdown_still_ends_it_by_itself() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice.hud.briefing._process(10.5)
	assert_false(practice.hud.briefing.running)
	assert_true(practice.hud.controls_active())



func test_the_title_is_left_and_the_countdown_is_a_plain_number_at_the_right_of_the_same_row() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.title_label.get_parent(), briefing.title_row)
	assert_eq(briefing.countdown.get_parent(), briefing.title_row, "one row")
	assert_lt(briefing.title_label.get_index(), briefing.countdown.get_index(), "the title first, the countdown last")
	assert_eq(briefing.title_label.horizontal_alignment, HORIZONTAL_ALIGNMENT_LEFT)
	assert_eq(briefing.countdown.horizontal_alignment, HORIZONTAL_ALIGNMENT_RIGHT, "at the right-most end")
	assert_true((briefing.title_label.size_flags_horizontal & Control.SIZE_EXPAND) != 0, "the title takes the room, so the number is pushed right")
	assert_eq(briefing.countdown.text, "10")
	assert_eq(briefing.find_children("*", "CountdownCircle", true, false).size(), 0, "no circle")
	briefing._process(2.2)
	assert_eq(briefing.countdown.text, "8", "whole seconds counting down")


func test_a_thin_white_line_between_the_title_row_and_the_details_empties_with_the_countdown() -> void:
	var practice: PracticeMatch = _match(&"patintero")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.bar.get_parent(), briefing.head)
	assert_eq(briefing.bar.get_index(), briefing.title_row.get_index() + 1, "right under row 1")
	assert_lt(briefing.bar.get_index(), briefing.goal.get_index(), "and over the details")
	var spacer: Control = briefing.head.get_child(briefing.bar.get_index() + 1) as Control
	assert_gt(spacer.custom_minimum_size.y + briefing.head.get_theme_constant("separation"), 16.0, "room between the hairline and the description")
	assert_eq(briefing.head.get_child(briefing.bar.get_index() + 2), briefing.goal)
	assert_lte(briefing.bar.custom_minimum_size.y, 8.0, "thin")
	assert_almost_eq(briefing.bar.share(), 1.0, 0.001)
	briefing._process(5.0)
	assert_almost_eq(briefing.bar.share(), 0.5, 0.001)


func test_the_block_is_centred_and_every_text_is_left_aligned_except_the_countdown() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	var column: Control = briefing.find_child("Column", true, false) as Control
	assert_eq(column.grow_horizontal, Control.GROW_DIRECTION_BOTH, "the block stays centred")
	assert_almost_eq(column.anchor_left, 0.5, 0.001)
	assert_almost_eq(column.anchor_top, 0.5, 0.001)
	for label: Node in briefing.find_children("*", "Label", true, false):
		if label != briefing.countdown:
			assert_eq((label as Label).horizontal_alignment, HORIZONTAL_ALIGNMENT_LEFT, (label as Label).text)


func test_the_headings_are_the_titles_colour_not_blue() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	var title_colour: Color = briefing.title_label.get_theme_color("font_color")
	var found: int = 0
	for label: Node in briefing.find_children("*", "Label", true, false):
		if (label as Label).text in ["MOVE", "ATTACK"]:
			found += 1
			assert_eq((label as Label).get_theme_color("font_color"), title_colour, (label as Label).text)
	assert_eq(found, 2)


func test_runners_and_guards_are_spaced_like_move_and_dive() -> void:
	var practice: PracticeMatch = _match(&"patintero")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.goal.get_theme_constant("separation"), briefing.rows.get_theme_constant("separation"))
	await wait_process_frames(2)
	var goal_blocks: Array[Node] = briefing.goal.get_children()
	var row_blocks: Array[Node] = briefing.rows.get_children()
	var goal_gap: float = (goal_blocks[1] as Control).position.y - ((goal_blocks[0] as Control).position.y + (goal_blocks[0] as Control).size.y)
	var row_gap: float = (row_blocks[1] as Control).position.y - ((row_blocks[0] as Control).position.y + (row_blocks[0] as Control).size.y)
	assert_almost_eq(goal_gap, row_gap, 0.5, "the same space between the two blocks in each pair")


func test_the_briefing_hides_the_match_hud_and_the_kids_and_they_come_back_after_it() -> void:
	for game: StringName in [&"street", &"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _match(game)
		var hud: MatchHud = practice.hud
		practice.advance(DT)
		assert_true(hud.briefing.visible)
		assert_false(practice._actors.visible, "%s: no kids over the empty street" % game)
		for item: CanvasItem in [hud.header, hud.top, hud.scoreboard, hud.overhead, hud.hand, hud.street_pad, hud.chat, hud.intro_bar, hud.capture_label, hud.banner]:
			assert_false(item.visible, "%s: %s is hidden under the briefing" % [game, item.name])
		if hud.score_column != null:
			assert_false(hud.score_column.visible, "%s: nor the points column" % game)
		hud.briefing.finish()
		practice.advance(DT)
		assert_true(practice._actors.visible, "%s: the kids are back" % game)
		assert_true(hud.header.visible and hud.top.visible and hud.overhead.visible, "%s: and the header and the names" % game)
		if game == &"street":
			assert_true(hud.hand.visible, "the hand is back in Agawang Base")
		else:
			assert_true(hud.street_pad.visible, "the knob and the button are back in %s" % game)


func test_the_rescue_areas_show_no_countdown_number_under_the_briefing() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice.advance(DT)
	var map: StreetMap = practice._map
	for side: int in [-1, 1]:
		assert_false(map.rescue_count(side).visible, "side %d: no number on the rescue area under the briefing" % side)
		assert_eq(map.rescue_count_text(side), "")
	practice.hud.briefing.finish()
	practice.advance(DT)
	var shown: int = 0
	for side: int in [-1, 1]:
		if map.rescue_count(side).visible:
			shown += 1
	assert_gt(shown, 0, "the numbers are back once the match runs (the rescue countdown is still counting)")
