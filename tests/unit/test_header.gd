extends GutTest
## The header at the top of the match screen: the console's deck turned over, the settings button and the score on it, the camera keeping
## the arena below it, and a notch's inset making it taller.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	await wait_process_frames(2)
	return practice


func test_the_header_spans_the_top_and_holds_the_gear_and_the_score() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var header: Rect2 = hud.header.get_global_rect()
	assert_eq(header.position.y, 0.0, "from the very top")
	assert_almost_eq(header.size.y, hud.header_height(), 0.5)
	assert_almost_eq(header.size.x, hud.get_global_rect().size.x, 0.5, "the whole width, like the console")
	assert_true(header.encloses(hud.settings_button.get_global_rect()), "the gear sits on it")
	assert_true(header.encloses(hud.scoreboard.get_global_rect()), "and the score")
	assert_eq(hud.header.mouse_filter, Control.MOUSE_FILTER_IGNORE, "it only draws")


func test_the_scoreboard_has_no_card_of_its_own_on_the_header() -> void:
	var practice: PracticeMatch = await _practice()
	assert_eq(practice.hud.scoreboard.find_children("*", "PanelContainer", true, false).size(), 0)
	assert_eq(practice.hud.feed_box.get_global_rect().position.y > practice.hud.header_height(), true, "the TAGGED! feed starts below the header")


func test_the_camera_keeps_the_arena_below_the_header() -> void:
	var practice: PracticeMatch = await _practice()
	assert_gte(practice._camera.top_reserve_px, practice.hud.header_height(), "it reserves at least the header")


func test_a_notch_makes_the_header_taller_and_pushes_the_arena_down() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var before: float = hud.header.size.y
	var reserve_before: float = practice._camera.top_reserve_px
	var score_before: float = hud.scoreboard.get_global_rect().position.y
	hud.set_top_inset(48.0)
	assert_almost_eq(hud.header.size.y, before + 48.0, 0.5)
	assert_almost_eq(hud.scoreboard.get_global_rect().position.y, score_before + 48.0, 0.5, "the content stays below the notch")
	assert_almost_eq(practice._camera.top_reserve_px, reserve_before + 48.0, 0.5)
	hud.set_top_inset(0.0)
	assert_almost_eq(hud.header.size.y, before, 0.5)


func test_the_camera_frames_the_whole_street_below_a_taller_header() -> void:
	var rules: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var arena: Vector2 = Vector2(layout.lane_width, layout.lane_length)
	var normal: Vector2 = rules.frame(720.0 / 1280.0, arena)
	var taller: Vector2 = rules.frame(720.0 / 1280.0, arena, -1.0, 0.2)
	assert_gt(taller.x, normal.x, "a taller header needs the camera further back to keep the whole street clear of it")
	assert_true(rules.fits(taller.x * 1.001, 720.0 / 1280.0, arena, -1.0, 0.2))
