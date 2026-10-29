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
	assert_eq(hud.scoreboard.get_parent(), hud.top, "the score belongs to the top HUD")
	assert_true(header.encloses(hud.scoreboard.get_global_rect()))
	assert_eq(hud.header.mouse_filter, Control.MOUSE_FILTER_IGNORE, "it only draws")


func test_the_scoreboard_has_no_card_of_its_own_on_the_header() -> void:
	var practice: PracticeMatch = await _practice()
	assert_eq(practice.hud.scoreboard.find_children("*", "PanelContainer", true, false).size(), 0)


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
	assert_almost_eq(hud.scoreboard.get_global_rect().position.y, score_before + 48.0, 0.5, "the score follows the header safe area")
	assert_almost_eq(practice._camera.top_reserve_px, reserve_before + 48.0, 0.5)
	hud.set_top_inset(0.0)
	assert_almost_eq(hud.header.size.y, before, 0.5)


func test_the_camera_frames_the_whole_street_below_a_taller_header() -> void:
	var rules: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var arena: Vector2 = Vector2(layout.lane_width, layout.lane_length)
	var normal: Vector2 = rules.frame(720.0 / 1280.0, arena)
	var taller: Vector2 = rules.frame(720.0 / 1280.0, arena, -1.0, 0.2)
	assert_gte(taller.x, normal.x, "a taller header never lets the view shrink")
	assert_lt(taller.y, normal.y, "the street sits lower on the screen to keep clear of a taller header")
	assert_true(rules.fits(taller.x * 1.001, 720.0 / 1280.0, arena, -1.0, 0.2))


func test_the_score_card_has_no_dots_under_the_numbers() -> void:
	var board: Scoreboard = autofree(Scoreboard.new()) as Scoreboard
	assert_false("roster_mine" in board, "the free / tagged dots are gone: they were confusing next to the score (owner)")
	assert_false(board.has_method("show_roster"))


func test_a_status_word_stays_under_the_header_and_inside_the_screen() -> void:
	var overhead: OverheadHud = autofree(OverheadHud.new()) as OverheadHud
	overhead.size = Vector2(720.0, 1280.0)
	overhead.top_limit = 130.0
	add_child(overhead)
	var font: Font = KalyeahTheme.BODY_FONT
	var high: Vector2 = overhead.keep_on_screen(Vector2(360.0, 90.0), font, "Slowed", OverheadHud.STATUS_FONT)
	assert_gt(high.y, 130.0, "a kid at the far end does not push its words under the header")
	var edge: Vector2 = overhead.keep_on_screen(Vector2(2.0, 600.0), font, "Slowed", OverheadHud.STATUS_FONT)
	assert_gt(edge.x, 20.0, "nor past the left side")
	assert_eq(overhead.keep_on_screen(Vector2(360.0, 600.0), font, "Slowed", OverheadHud.STATUS_FONT), Vector2(360.0, 600.0), "and a kid in the middle is untouched")


func test_the_clock_sits_on_the_header_opposite_the_gear_and_counts_up() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var header: Rect2 = hud.header.get_global_rect()
	var chip: Rect2 = hud.clock_chip.get_global_rect()
	assert_true(header.encloses(chip), "the clock stands on the header")
	assert_gt(chip.position.x, hud.size.x / 2.0, "at the right edge of the header")
	assert_almost_eq(chip.position.y, 0.0, 1.0, "flush with the top of the screen, like Clash Royale's Time left box")
	assert_almost_eq(chip.end.x, practice.hud.get_global_rect().end.x, 1.0, "and with its right edge")
	assert_eq(hud.clock_chip.mouse_filter, Control.MOUSE_FILTER_IGNORE)
	hud.clock_chip.show_time(65.4, false)
	assert_eq(hud.clock_chip.text(), "1:05")
	assert_false(hud.clock_chip.overtime)
	hud.clock_chip.show_time(65.4, true)
	assert_true(hud.clock_chip.overtime)
	assert_string_contains(hud.clock_chip.accessibility_name, "overtime")


func test_overtime_is_when_the_round_passes_the_rules_overtime_time() -> void:
	var practice: PracticeMatch = await _practice()
	var sim: MatchSim = practice.sim
	sim.rules = sim.rules.duplicate() as GameRules
	sim.rules.overtime_after = 30.0
	sim.round_time = 29.0
	assert_false(sim.in_overtime())
	sim.round_time = 30.0
	assert_true(sim.in_overtime())
	assert_eq(sim.current_base_capture_time(), minf(sim.rules.capture_solo_time, sim.rules.overtime_capture_time))
	sim.rules.overtime_after = 0.0
	assert_false(sim.in_overtime(), "0 means never")


func test_your_health_bar_turns_amber_then_red_as_it_empties() -> void:
	assert_eq(ConsolePanel.health_color(1.0), TeamColors.bar_self())
	assert_eq(ConsolePanel.health_color(0.5), TeamColors.bar_self(), "half is still healthy")
	assert_eq(ConsolePanel.health_color(0.4), ConsolePanel.HEALTH_AMBER)
	assert_eq(ConsolePanel.health_color(0.2), ConsolePanel.HEALTH_RED)


# ---- the interaction audit's P0 and P1 (docs/audits/2026-10-22-interaction-audit.md) ----

func test_the_clock_says_time_because_it_counts_up_and_a_match_has_no_limit() -> void:
	assert_eq(ClockChip.new().text(), "0:00")
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	assert_eq(rules.overtime_after, 0.0, "no overtime: nothing says time is left")
