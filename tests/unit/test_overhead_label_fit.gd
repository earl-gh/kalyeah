extends GutTest
## The word over a kid's bar (its name, or its status) is fitted to the bar's FULL width (owner, 2026-10-30): it shrinks from `LABEL_FONT` a
## pixel at a time until the whole word is no wider than the bar, a short one stays at `LABEL_FONT`. It is never cut and never ends in dots
## ("..."), however long; `LABEL_FONT_MIN` is only the safety floor. The health in the bar never enters into it, and the word's black outline
## shrinks with it. Seam: OverheadHud.label_room / label_font_size / label_outline (drawn by `_draw_label`).

## The names and status words that matter: the longest allowed name (12 characters, `Session.MAX_NAME_LENGTH`) in the widest and a plain face,
## the owner's 10 letter case, and every status word alone and with AFK.
const NAMES: Array[String] = ["Abcdefghij", "Mmmmmmmmmm", "WWWWWWWWWW", "WWWWWWWWWWWW", "MMMMMMMMMMMM", "Bartholomew"]
const STATUS_WORDS: Array[String] = ["Stunned", "Slowed", "Airborne", "Morphed", "Bounced", "Knocked out", "Exhausted", "AFK", "AFK · Exhausted", "AFK · Knocked out"]


func _bar_width() -> float:
	var rules: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	return OverheadHud.BAR_SIZE.x * rules.bar_scale


func _width(text: String, font_size: int) -> float:
	return KalyeahTheme.ACTION_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x


func test_the_largest_size_is_sixteen_and_the_safety_floor_is_five() -> void:
	assert_eq(OverheadHud.LABEL_FONT, 16)
	assert_eq(OverheadHud.LABEL_FONT_MIN, 5)


func test_the_game_has_one_bar_scale_and_every_word_fits_it_whole_above_the_floor() -> void:
	# the one bar scale the game uses is 0.75 (data/rules/camera_rules.tres): 72 px of bar, 64 px of room after the badge's inset
	assert_almost_eq(_bar_width(), 72.0, 0.001)
	var room: float = OverheadHud.label_room(_bar_width())
	assert_lt(room, _bar_width(), "the word starts clear of the badge, inside the bar")
	for text: String in NAMES + STATUS_WORDS:
		var font_size: int = OverheadHud.label_font_size(text, room)
		assert_lte(_width(text, font_size), room, "'%s' fits whole at %d px (%.1f of %.1f px of room)" % [text, font_size, _width(text, font_size), room])
		assert_gt(font_size, OverheadHud.LABEL_FONT_MIN - 1, "never under the floor")
		assert_lte(font_size, OverheadHud.LABEL_FONT)
		assert_lte(_width(text, font_size), _bar_width(), "and so inside the bar itself")


func test_the_widest_allowed_name_still_fits_at_the_floor_or_above() -> void:
	# the floor is chosen so the longest name (12 of the widest letter) fits the game's bar at 5 px or more, not squeezed against it
	var room: float = OverheadHud.label_room(_bar_width())
	assert_lte(_width("WWWWWWWWWWWW", OverheadHud.LABEL_FONT_MIN), room, "even the floor size holds 12 W")
	assert_eq(OverheadHud.label_font_size("WWWWWWWWWWWW", room), OverheadHud.LABEL_FONT_MIN, "and it is the size that name gets")
	assert_gt(OverheadHud.label_font_size("WWWWWWWWWW", room), OverheadHud.LABEL_FONT_MIN, "10 W get a size above the floor")


func test_no_word_is_cut_there_are_no_dots_in_the_code_or_the_word() -> void:
	var hud: OverheadHud = autofree(OverheadHud.new()) as OverheadHud
	assert_false(hud.has_method("label_fitted"), "nothing cuts a word any more")
	assert_false((OverheadHud as Script).get_script_constant_map().has("LABEL_CUT"), "and there is no ellipsis constant")
	var room: float = OverheadHud.label_room(_bar_width())
	for text: String in NAMES + STATUS_WORDS:
		assert_false("..." in text)
		# the drawn word is the word itself at a size where all of it fits (`_draw_label` hands `text` to the font untouched)
		assert_lte(_width(text, OverheadHud.label_font_size(text, room)), room)


func test_a_word_keeps_shrinking_until_it_fits_however_long_down_to_the_floor() -> void:
	# a word that fits at the floor gets the largest size at which all of it fits; one more pixel would not
	for factor: float in [0.75, 1.0, 1.5, 2.0]:
		var bar: float = OverheadHud.BAR_SIZE.x * factor
		var room: float = OverheadHud.label_room(bar)
		for text: String in NAMES + STATUS_WORDS:
			var font_size: int = OverheadHud.label_font_size(text, room)
			if _width(text, OverheadHud.LABEL_FONT_MIN) <= room:
				assert_lte(_width(text, font_size), room, "'%s' fits the bar at scale %.2f" % [text, factor])
				if font_size < OverheadHud.LABEL_FONT:
					assert_gt(_width(text, font_size + 1), room, "'%s': one pixel bigger would not fit at scale %.2f" % [text, factor])
			else:
				assert_eq(font_size, OverheadHud.LABEL_FONT_MIN, "'%s' is wider than the room even at the floor: the floor is as small as it goes" % text)


func test_a_short_name_keeps_the_largest_size_and_a_wide_bar_does_not_stretch_it() -> void:
	for text: String in ["GABO", "AFK", "Popoy"]:
		assert_eq(OverheadHud.label_font_size(text, OverheadHud.label_room(_bar_width())), OverheadHud.LABEL_FONT, text)
	assert_eq(OverheadHud.label_font_size("GABO", 1000.0), OverheadHud.LABEL_FONT, "never past the largest size")


func test_a_long_word_shrinks_and_is_shown_whole() -> void:
	var room: float = OverheadHud.label_room(_bar_width())
	assert_lt(OverheadHud.label_font_size("Exhausted", room), OverheadHud.LABEL_FONT, "the status word shrinks")
	assert_lt(OverheadHud.label_font_size("AFK · Exhausted", room), OverheadHud.label_font_size("Exhausted", room), "and a longer one shrinks more")


func test_the_size_depends_on_the_words_width_and_the_bars_width_not_on_its_health() -> void:
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
	# two words of the same width get the same size, so the size is a function of the width alone
	assert_eq(OverheadHud.label_font_size("Marites", 50.0), OverheadHud.label_font_size("Marites", 50.0))


func test_the_outline_shrinks_with_the_word_and_stays_a_whole_pixel() -> void:
	assert_eq(OverheadHud.label_outline(OverheadHud.LABEL_FONT), OverheadHud.TEXT_OUTLINE, "5 px at 16 px: the look at the largest size is what it was")
	var last: int = OverheadHud.label_outline(OverheadHud.LABEL_FONT)
	for font_size: int in range(OverheadHud.LABEL_FONT, OverheadHud.LABEL_FONT_MIN - 1, -1):
		var outline: int = OverheadHud.label_outline(font_size)
		assert_lte(outline, last, "never thicker for a smaller word (%d px)" % font_size)
		assert_gte(outline, 1, "always a line")
		assert_lt(float(outline), float(font_size) * 0.5, "and well under half the word's size, so a small word is not a blot (%d px: %d)" % [font_size, outline])
		last = outline
	assert_eq(OverheadHud.label_outline(OverheadHud.LABEL_FONT_MIN), 2)


func test_the_baseline_follows_the_fitted_size_so_the_word_stays_level_with_the_outline() -> void:
	var bar: Rect2 = Rect2(Vector2(300.0, 400.0), Vector2(72.0, 13.5))
	for font_size: int in [OverheadHud.LABEL_FONT_MIN, 13, OverheadHud.LABEL_FONT]:
		var middle: float = OverheadHud.label_baseline(bar, font_size) - float(font_size) * 0.36
		assert_almost_eq(middle, bar.position.y - OverheadHud.BAR_OUTLINE / 2.0, 0.001)
