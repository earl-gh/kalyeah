extends GutTest
## The health, stamina and ice water bars: rounded rectangles (not pills), flat empty colours, ice water that fills one segment at a time.


func test_a_bar_is_a_rounded_rectangle_not_a_pill() -> void:
	for height: float in [9.0, 15.0, 26.0, 40.0]:
		var radius: int = UiSkin.bar_radius(height)
		assert_gte(radius, 1, "a %.0f px bar still has rounded corners" % height)
		assert_lt(float(radius), height / 2.0, "but they are not a pill's half-height ends (%.0f px)" % height)
		assert_lte(radius, UiSkin.RADIUS_S, "and never rounder than the smallest step of the scale")


func test_the_empty_part_of_the_health_and_stamina_bars_is_a_flat_dark_gray() -> void:
	var gray: Color = OverheadHud.EMPTY_BAR
	assert_eq(gray.a, 1.0, "opaque: just a colour")
	assert_lt(absf(gray.r - gray.g), 0.05, "gray: no hue")
	assert_lt(absf(gray.g - gray.b), 0.05)
	assert_lt(gray.get_luminance(), 0.3, "dark")
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	sim.add_player(1, 0)
	var overhead: OverheadHud = OverheadHud.new()
	add_child_autofree(overhead)
	overhead.setup(sim, null, 1, 0)
	assert_eq(overhead._bar_back.bg_color, gray, "the bar's back is that gray for both bars")


func test_the_empty_part_of_the_ice_water_bar_is_a_flat_dark_blue() -> void:
	var blue: Color = CardHand.ICE_EMPTY
	assert_eq(blue.a, 1.0, "opaque: just a colour")
	assert_gt(blue.b, blue.r * 2.0, "blue")
	assert_lt(blue.get_luminance(), 0.3, "dark")


func test_the_ice_water_bar_fills_one_segment_at_a_time() -> void:
	var none: PackedFloat32Array = CardHand.segment_fills(0.0, 10)
	assert_eq(none.size(), 10)
	assert_eq(none.count(0.0), 10, "empty")
	var part: PackedFloat32Array = CardHand.segment_fills(3.4, 10)
	assert_eq(part.slice(0, 3), PackedFloat32Array([1.0, 1.0, 1.0]), "three whole segments are solid")
	assert_almost_eq(part[3], 0.4, 0.0001, "the next one shows its progress")
	assert_eq(part.slice(4), PackedFloat32Array([0.0, 0.0, 0.0, 0.0, 0.0, 0.0]), "the rest are empty")
	var full: PackedFloat32Array = CardHand.segment_fills(10.0, 10)
	assert_eq(full.count(1.0), 10, "full: every segment solid")
	assert_eq(CardHand.segment_fills(7.99995, 10)[7], 1.0, "float rounding just under a whole number does not leave a gap")
	assert_gt(CardHand.REFILL_WHITE.a, 0.0)
	assert_lt(CardHand.REFILL_WHITE.a, 0.7, "the filling segment is see-through white")
	assert_eq(Color(CardHand.REFILL_WHITE, 1.0), Color.WHITE)


func test_the_max_caption_is_centred_on_the_bars_bottom_outline() -> void:
	var bar: Rect2 = Rect2(Vector2(100.0, 200.0), Vector2(400.0, 26.0))
	var middle: float = CardHand.max_caption_baseline(bar) - float(CardHand.MAX_FONT) * 0.36
	assert_almost_eq(middle, bar.end.y - float(CardHand.BAR_OUTLINE) / 2.0, 0.001, "the caption's middle is the outline's middle")


func test_the_outlines_of_all_three_bars_are_thin() -> void:
	assert_lte(OverheadHud.BAR_OUTLINE, 2.0, "health and stamina")
	assert_lte(float(CardHand.BAR_OUTLINE), 2.0, "ice water")
	assert_lt(float(CardHand.BAR_OUTLINE), float(RoyaleStyle.OUTLINE), "thinner than the chrome's outline")


func test_the_colours_make_sense_ice_water_and_stamina_are_blue_and_nothing_here_is_yellow_when_full() -> void:
	var full: Color = CardHand.ICE_WATER_FULL_EDGE
	assert_gt(full.b, full.r, "a full ice water bar's edge is blue, not gold")
	assert_gt(full.b, full.g - 0.001, "light ice blue")
	var stamina: Color = OverheadHud.STAMINA_COLOR
	assert_gt(stamina.b, stamina.r + 0.4, "stamina is blue")
	assert_gt(stamina.b, stamina.g, "blue, not cyan or white")
	assert_gt(OverheadHud.EXHAUSTED_COLOR.r, OverheadHud.EXHAUSTED_COLOR.b + 0.4, "running out is still red")
	assert_gt(OverheadHud.STAMINA_LOW_COLOR.r, OverheadHud.STAMINA_LOW_COLOR.b + 0.4, "and a warning is still amber")


func test_the_health_number_is_centred_on_the_bars_top_outline() -> void:
	var bar: Rect2 = Rect2(Vector2(300.0, 400.0), Vector2(48.0, 9.0))
	var middle: float = OverheadHud.count_baseline(bar) - float(OverheadHud.COUNT_FONT) * 0.36
	assert_almost_eq(middle, bar.position.y - OverheadHud.BAR_OUTLINE / 2.0, 0.001, "the number's middle is the middle of the top outline")


func test_the_stamina_bar_touches_the_health_bar_with_no_gap() -> void:
	var bar: Rect2 = Rect2(Vector2(300.0, 400.0), Vector2(48.0, 9.0))
	var health_outline_bottom: float = bar.end.y + OverheadHud.BAR_OUTLINE
	var stamina_outline_top: float = OverheadHud.stamina_top(bar) - OverheadHud.BAR_OUTLINE
	assert_lte(stamina_outline_top, health_outline_bottom, "no gap: the outlines meet (they share the line between the bars)")
	assert_gte(stamina_outline_top, bar.end.y, "and the stamina bar does not climb into the health bar")


func test_a_stamina_bar_in_play_is_blue_and_low_is_amber() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	var me: PlayerState = sim.add_player(1, 0)
	var overhead: OverheadHud = OverheadHud.new()
	add_child_autofree(overhead)
	overhead.setup(sim, null, 1, 0)
	me.stamina = rules.stamina_max * 0.6
	assert_eq(overhead.stamina_fill(me)[1], OverheadHud.STAMINA_COLOR, "blue while it is fine")
	me.stamina = rules.stamina_max * 0.1
	assert_eq(overhead.stamina_fill(me)[1], OverheadHud.STAMINA_LOW_COLOR, "amber when low")


func test_the_small_street_numbers_are_smaller_than_the_reading_size_and_max_is_smaller_than_the_counter() -> void:
	assert_lt(UiMetrics.WORLD, UiMetrics.MIN_TEXT, "below the reading floor on purpose: they are glanced at")
	assert_gte(UiMetrics.WORLD, 16, "but still legible")
	assert_lt(OverheadHud.COUNT_FONT, UiMetrics.WORLD, "the health number is the smallest street size (owner: smaller)")
	assert_lt(CardHand.MAX_FONT, CardHand.BAR_FONT, "\"Max: 10\" is smaller than the ice water counter")
	assert_lt(float(CardHand.MAX_FONT), float(CardHand.BAR_FONT) * 0.8, "clearly smaller, not nearly the same")
	assert_gt(UiMetrics.WORLD, 0)
