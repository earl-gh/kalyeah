extends GutTest
## The match screen copies Clash Royale's look (owner): the tray behind the cards is drawn in code (no plate picture), the clock says
## "Time left:" in a see-through box with a black outline, the score blocks are a vivid blue and red, the word over the next card is
## "Next:", and the sidewalks are twice as wide.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")


func test_the_tray_behind_the_cards_is_drawn_in_code_not_a_cardboard_picture() -> void:
	assert_false(FileAccess.file_exists("res://assets/ui/hand/hand_plate.webp"), "the plate picture is gone")
	assert_false(FileAccess.file_exists("res://assets/ui/hand/hand_plate.webp.import"), "nor its import")
	var panel: ConsolePanel = autofree(ConsolePanel.new()) as ConsolePanel
	panel.size = Vector2(720.0, 260.0)
	add_child(panel)
	panel.set_column(116.0, true)
	await wait_process_frames(2)
	assert_gt(ConsolePanel.TRAY_BODY.b, ConsolePanel.TRAY_BODY.r * 3.0, "a bright blue body")
	assert_gt(ConsolePanel.BEVEL_LIGHT.b, ConsolePanel.TRAY_BODY.b - 0.001, "with a lighter bevel along its top")
	assert_gt(ConsolePanel.BEVEL_LIGHT.g, ConsolePanel.TRAY_BODY.g, "lighter, not just bluer")
	assert_gt(ConsolePanel.TRAY_TOP.g, ConsolePanel.TRAY_BODY.g, "the body lightens a little toward its top")
	assert_lt(ConsolePanel.TRAY_BOTTOM.g, ConsolePanel.TRAY_BODY.g, "and darkens toward the bottom of the screen")
	assert_gt(ConsolePanel.TRAY_BOTTOM.b, ConsolePanel.TRAY_BOTTOM.r * 3.0, "still a blue, not a black")
	var constants: Dictionary = (ConsolePanel as GDScript).get_script_constant_map()
	for constant_name: String in constants:
		assert_false(constant_name.contains("OUTLINE"), "the tray has no outline colour (%s)" % constant_name)


func test_the_clock_says_time_left_in_a_see_through_box_with_a_black_outline() -> void:
	var chip: ClockChip = autofree(ClockChip.new()) as ClockChip
	assert_eq(chip.caption_text(), "Time left:")
	chip.show_time(75.0, MatchSim.ClockStage.EXTENDED, true)
	assert_eq(chip.caption_text(), "Extended")
	assert_eq(chip.caption_color(), Color.WHITE)
	chip.show_time(75.0, MatchSim.ClockStage.OVERTIME, true)
	assert_eq(chip.caption_text(), "Overtime", "overtime keeps its word")
	assert_eq(chip.caption_color(), RoyaleStyle.GOLD, "and its gold")
	assert_between(ClockChip.BOX_COLOR.a, 0.3, 0.7, "semi-transparent: the street shows through")
	assert_gt(ClockChip.BOX_OUTLINE, 1, "a black outline you can see")


func test_the_score_blocks_are_a_vivid_natural_blue_and_red() -> void:
	Settings.colorblind = false
	var blue: Color = TeamColors.score_own()
	var red: Color = TeamColors.score_enemy()
	assert_gt(blue.s, 0.8, "a saturated blue")
	assert_gt(blue.v, 0.95, "bright")
	assert_between(blue.h, 0.55, 0.62, "a natural blue, not purple or teal")
	assert_gt(red.s, 0.7, "a saturated red")
	assert_gt(red.v, 0.95, "bright")
	assert_true(red.h < 0.03 or red.h > 0.97, "a natural red, not pink or orange")
	assert_gt(blue.v, TeamColors.ui_own().v + 0.15, "livelier (brighter) than the dull UI blue")
	assert_gt(red.v, TeamColors.ui_enemy().v + 0.15, "and than the dull UI red")
	Settings.colorblind = true
	assert_eq(TeamColors.score_own(), TeamColors.BLIND_OWN, "blue and orange for Colour-blind mode")
	assert_eq(TeamColors.score_enemy(), TeamColors.BLIND_ENEMY)
	Settings.colorblind = false


func test_the_word_over_the_next_card_is_next_with_a_colon() -> void:
	var hand: CardHand = autofree(CardHand.new()) as CardHand
	assert_eq(hand.next_label(), "Next:")


func test_the_sidewalks_are_twice_as_wide_and_the_play_area_two_tiles_narrower() -> void:
	assert_almost_eq(StreetMap.SIDEWALK_WIDTH, 2.4, 0.0001, "1.2 m before")
	assert_almost_eq(LAYOUT.lane_width, 24.0, 0.0001, "27 m before")
	# the whole street (play area, boundary and both sidewalks) is about as wide as before
	var before: float = 27.0 + 2.0 * (LAYOUT.boundary_thickness + 1.2)
	var now: float = LAYOUT.lane_width + 2.0 * (LAYOUT.boundary_thickness + StreetMap.SIDEWALK_WIDTH)
	assert_almost_eq(now, before, 0.8)


func test_the_floor_picture_is_cropped_not_stretched_on_the_narrower_play_area() -> void:
	var map: StreetMap = autofree((load("res://scenes/map/street_map.tscn") as PackedScene).instantiate()) as StreetMap
	add_child(map)
	assert_almost_eq(map.cement_share(), 16.0 / 18.0, 0.0001, "the middle 16 of the picture's 18 tile columns")


func test_the_ice_water_icon_is_the_owners_drop_with_a_snowflake_and_lossless() -> void:
	assert_true(FileAccess.file_exists("res://assets/ui/hand/ice_water.png"), "the owner's drop")
	assert_false(FileAccess.file_exists("res://assets/ui/hand/ice_water.webp"), "the old bag is gone")
	var drop: Texture2D = load("res://assets/ui/hand/ice_water.png") as Texture2D
	assert_almost_eq(drop.get_size().x / drop.get_size().y, HandArt.ICE_ASPECT, 0.002, "the aspect is the picture's own (trimmed to its art)")
	var atlas: AtlasTexture = HandArt.ice()
	assert_eq(atlas.region.size, drop.get_size(), "all of the picture is drawn")
	var drawn: Rect2 = HandArt.ice_rect(Vector2.ZERO, 100.0)
	assert_almost_eq(drawn.size.x / drawn.size.y, HandArt.ICE_DRAW_ASPECT, 0.0001, "drawn at the narrower ratio, not the picture's own")
	assert_lt(drawn.size.x / drawn.size.y, drop.get_size().x / drop.get_size().y, "narrower than the picture")
	var import_text: String = FileAccess.get_file_as_string("res://assets/ui/hand/ice_water.png.import")
	assert_true(import_text.contains("compress/mode=0"), "lossless")
	assert_true(import_text.contains("mipmaps/generate=true"), "with mipmaps, so it stays smooth drawn small")


func test_the_rescue_area_looks_square_on_screen_and_is_as_tall_as_it_was() -> void:
	var camera: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	assert_eq(LAYOUT.rescue_size, Vector2(4.25, 6.0), "widened from 3 m to 4.25 m, the height unchanged")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var rect: Rect2 = LAYOUT.rescue_rect(side)
		assert_almost_eq(rect.size.y, 6.0, 0.0001)
		# the camera looks down at its pitch: a metre along the street shows as sin(pitch) of a metre on screen
		var across: float = rect.size.x
		var down: float = rect.size.y * sin(deg_to_rad(camera.pitch_degrees))
		assert_almost_eq(across / down, 1.0, 0.01, "as wide as it is tall on screen (side %d)" % side)
		assert_true(LAYOUT.lane_rect().encloses(rect), "inside the street")
