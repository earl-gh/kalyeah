extends GutTest
## The word over a kid's bar (its name, or its status) is fitted to the bar's FULL width (owner, 2026-10-30): a long one shrinks from
## `LABEL_FONT` toward `LABEL_FONT_MIN` until it is no wider than the bar, a short one stays at `LABEL_FONT`. The health in the bar never
## enters into it. Seam: OverheadHud.label_room / label_font_size / label_fitted (drawn by `_draw_label`).

## The names and status words that matter: the longest name (10 letters, the owner's case) in a narrow and a wide face, and every status word.
const NAMES: Array[String] = ["Abcdefghij", "Mmmmmmmmmm", "WWWWWWWWWW", "Bartholomew"]
const STATUS_WORDS: Array[String] = ["Stunned", "Slowed", "Exhausted", "AFK", "AFK · Exhausted"]


func _bar_width() -> float:
	var rules: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	return OverheadHud.BAR_SIZE.x * rules.bar_scale


func _width(text: String, font_size: int) -> float:
	return KalyeahTheme.ACTION_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x


func test_the_smallest_size_is_ten_and_the_largest_is_the_old_sixteen() -> void:
	assert_eq(OverheadHud.LABEL_FONT, 16)
	assert_eq(OverheadHud.LABEL_FONT_MIN, 10)


func test_no_word_is_wider_than_the_bar_at_the_game_bar_scale() -> void:
	var room: float = OverheadHud.label_room(_bar_width())
	assert_lt(room, _bar_width(), "the word starts clear of the badge, inside the bar")
	for text: String in NAMES + STATUS_WORDS:
		var shown: String = OverheadHud.label_fitted(text, room)
		var font_size: int = OverheadHud.label_font_size(text, room)
		assert_lte(_width(shown, font_size), _bar_width(), "'%s' as '%s' at %d px fits the bar (%.1f px)" % [text, shown, font_size, _bar_width()])
		assert_gte(font_size, OverheadHud.LABEL_FONT_MIN)
		assert_lte(font_size, OverheadHud.LABEL_FONT)


func test_no_word_is_wider_than_the_bar_at_any_bar_scale() -> void:
	for factor: float in [0.5, 0.75, 1.0, 1.5]:
		var bar: float = OverheadHud.BAR_SIZE.x * factor
		for text: String in NAMES + STATUS_WORDS:
			var shown: String = OverheadHud.label_fitted(text, OverheadHud.label_room(bar))
			assert_lte(_width(shown, OverheadHud.label_font_size(text, OverheadHud.label_room(bar))), bar, "'%s' at bar scale %.2f" % [text, factor])


func test_a_short_name_keeps_the_largest_size_and_a_wide_bar_does_not_stretch_it() -> void:
	for text: String in ["GABO", "AFK", "Popoy"]:
		assert_eq(OverheadHud.label_font_size(text, OverheadHud.label_room(_bar_width())), OverheadHud.LABEL_FONT, text)
		assert_eq(OverheadHud.label_fitted(text, OverheadHud.label_room(_bar_width())), text, "whole")
	assert_eq(OverheadHud.label_font_size("GABO", 1000.0), OverheadHud.LABEL_FONT, "never past the largest size")


func test_a_long_word_shrinks_before_it_is_cut_and_a_fitting_word_is_never_cut() -> void:
	var room: float = OverheadHud.label_room(_bar_width())
	assert_lt(OverheadHud.label_font_size("Exhausted", room), OverheadHud.LABEL_FONT, "the status word shrinks")
	assert_eq(OverheadHud.label_fitted("Exhausted", room), "Exhausted", "and is shown whole")
	assert_eq(OverheadHud.label_fitted("Abcdefghij", room), "Abcdefghij", "a plain 10-letter name is whole")
	var wide: String = OverheadHud.label_fitted("WWWWWWWWWW", room)
	assert_eq(OverheadHud.label_font_size("WWWWWWWWWW", room), OverheadHud.LABEL_FONT_MIN, "a very wide name reaches the smallest size")
	assert_true(wide.ends_with(OverheadHud.LABEL_CUT) and wide.length() < 10, "and is cut (%s)" % wide)


func test_the_fit_depends_on_the_bars_full_width_not_on_its_health() -> void:
	# `_draw_label` is handed the bar's rect, whose width is the same at any health; the fill never reaches it
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	sim.add_player(1, 0)
	var hud: OverheadHud = OverheadHud.new()
	add_child_autofree(hud)
	hud.setup(sim, null, 1, 0)
	hud.names[1] = "Marites"
	var sizes: Array[int] = []
	for hp: int in [rules.player_max_hp, rules.player_max_hp / 2, 1]:
		sim.players[1].hp = hp
		sizes.append(OverheadHud.label_font_size(hud.label_for(sim.players[1]), OverheadHud.label_room(_bar_width())))
	assert_eq(sizes[0], sizes[1], "full and half health: the same size")
	assert_eq(sizes[1], sizes[2], "and one point left")
	var narrow: int = OverheadHud.label_font_size("Marites", OverheadHud.label_room(_bar_width() * 0.7))
	assert_lt(narrow, sizes[0], "a narrower bar makes it smaller")


func test_the_baseline_follows_the_fitted_size_so_the_word_stays_level_with_the_outline() -> void:
	var bar: Rect2 = Rect2(Vector2(300.0, 400.0), Vector2(72.0, 13.5))
	for font_size: int in [OverheadHud.LABEL_FONT_MIN, 13, OverheadHud.LABEL_FONT]:
		var middle: float = OverheadHud.label_baseline(bar, font_size) - float(font_size) * 0.36
		assert_almost_eq(middle, bar.position.y - OverheadHud.BAR_OUTLINE / 2.0, 0.001)
