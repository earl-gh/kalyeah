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
	assert_eq(words["title"], "Agawan Base")
	assert_eq(words["goal"], "Earn a point by capturing the enemy base: stay inside it, or tag all the enemies.")
	assert_eq(words["rows"], [["Move", "Tap to move, or use gyroscope"], ["Attack", "Tap to cast skills, drag to aim"]])


func test_the_street_games_say_their_own_controls() -> void:
	var tumbang: Dictionary = MatchBriefing.words_for(StreetMode.rules_for(&"tumbang_preso"))
	assert_eq(tumbang["title"], "Tumbang Preso")
	assert_eq((tumbang["rows"] as Array)[1][0], "Throw")
	assert_string_contains(tumbang["goal"] as String, "can")
	var patintero: Dictionary = MatchBriefing.words_for(StreetMode.rules_for(&"patintero"))
	assert_eq(patintero["title"], "Patintero")
	assert_eq((patintero["rows"] as Array)[1][0], "Dive")
	assert_string_contains(patintero["goal"] as String, "guard")
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


func test_a_tap_ends_it_at_once_and_does_not_also_skip_the_opening_show() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	practice._input(_touch())
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO, "the tap that ends the briefing does not skip the show")
	briefing._gui_input(_touch())
	assert_false(briefing.running)
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO, "the show still plays after it")


func test_android_back_starts_the_match_instead_of_opening_the_menu() -> void:
	var practice: PracticeMatch = _match(&"street")
	practice._back_pressed()
	assert_false(practice.hud.menu.visible, "back does not open the menu over the briefing")
	practice.hud.briefing.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(1)
	assert_false(practice.hud.briefing.running)


func test_it_is_text_only_with_a_blurred_dimmed_backdrop_and_a_tap_line() -> void:
	var practice: PracticeMatch = _match(&"street")
	var briefing: MatchBriefing = practice.hud.briefing
	assert_eq(briefing.find_children("*", "BaseButton", true, false).size(), 0, "no buttons: a tap anywhere continues")
	var backdrop: ColorRect = briefing.find_child("Backdrop", true, false) as ColorRect
	assert_true((backdrop.material as ShaderMaterial).shader.code.contains("textureLod"), "the match behind is blurred")
	assert_true((backdrop.material as ShaderMaterial).shader.code.contains("dim"), "and dimmed")
	assert_eq(briefing.continue_label.text, "Tap to continue")
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
	assert_false("coach_seen" in Session)


func test_the_taya_roll_waits_for_the_briefing() -> void:
	var practice: PracticeMatch = _match(&"tumbang_preso")
	practice.advance(DT)
	assert_null(practice._presentation._roll, "no roll over the briefing")
	practice.hud.briefing.finish()
	practice.advance(DT)
	assert_not_null(practice._presentation._roll, "it starts when the briefing ends")


func test_the_briefing_draws_over_tumbang_presos_points_column() -> void:
	var practice: PracticeMatch = _match(&"tumbang_preso")
	var hud: MatchHud = practice.hud
	assert_gt(hud.briefing.get_index(), hud.score_column.get_index(), "the column is blurred with the rest")
