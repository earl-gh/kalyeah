extends GutTest
## Round 9 of the interface plan: a skin that builds fast and ahead of time, a Home that fits every phone (the hero,
## mode facts, a last-match strip), pages that slide from their tab's side, a tab plate that glides, and idle motion
## that rests under a cover.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const BOOT_SCENE: PackedScene = preload("res://scenes/ui/boot_screen.tscn")

var _motion: bool
var _history_path: String
var _name: String


func before_each() -> void:
	_motion = Settings.reduced_motion
	_history_path = MatchHistory.path
	MatchHistory.path = "user://history_r9.cfg"
	MatchHistory.clear()
	Settings.set_reduced_motion(true)


func after_each() -> void:
	MatchHistory.clear()
	MatchHistory.path = _history_path
	Settings.set_reduced_motion(_motion)


func _phone(height: float = 1280.0) -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, height)
	phone.size = Vector2(720.0, height)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


# ---- the skin --------------------------------------------------------------------------

func test_every_texture_is_the_same_left_and_right_so_drawing_half_is_exact() -> void:
	for variant: String in ["gold", "slate", "disabled"]:
		for state: String in ["normal", "pressed"]:
			var image: Image = UiSkin.button(variant, state).texture.get_image()
			for y: int in range(0, image.get_height(), 7):
				for x: int in image.get_width() / 2:
					assert_eq(image.get_pixel(x, y), image.get_pixel(image.get_width() - 1 - x, y), "%s/%s at %d,%d" % [variant, state, x, y])
	for kind: String in UiSkin.PLATES:
		var plate: Image = UiSkin.plate(kind).texture.get_image()
		for y: int in range(0, plate.get_height(), 7):
			for x: int in plate.get_width() / 2:
				assert_eq(plate.get_pixel(x, y), plate.get_pixel(plate.get_width() - 1 - x, y), "%s plate at %d,%d" % [kind, x, y])


func test_every_button_has_straight_sides_in_its_stretched_middle_band() -> void:
	# a nine-slice stretches the rows between the top and bottom texture margins: if a corner arc reaches into them the
	# side is a stretched sliver of the arc and reads slanted, so the outline column must be one pixel on every band row
	for radius: int in [UiSkin.BUTTON_RADIUS, UiSkin.RADIUS_M, UiSkin.RADIUS_XL]:
		for variant: String in UiSkin.BUTTONS:
			for state: String in ["normal", "hover", "pressed"]:
				var box: StyleBoxTexture = UiSkin.button(variant, state, radius)
				var image: Image = box.texture.get_image()
				var what: String = "%s/%s r%d" % [variant, state, radius]
				var from: int = int(box.texture_margin_top)
				var to: int = image.get_height() - int(box.texture_margin_bottom)
				assert_gt(to, from, "%s: the stretched middle band is not empty" % what)
				for x: int in [UiSkin.BUTTON_PAD, image.get_width() - 1 - UiSkin.BUTTON_PAD]:
					var side: Color = image.get_pixel(x, from)
					assert_gt(side.a, 0.0, "%s: the outline column %d is drawn" % [what, x])
					var bent: Array[int] = []
					for y: int in range(from, to):
						if image.get_pixel(x, y) != side:
							bent.append(y)
					assert_eq(bent, [] as Array[int], "%s: column %d is the same on every band row %d-%d" % [what, x, from, to - 1])


func test_a_button_has_no_shadow() -> void:
	# owner: buttons carry no drop shadow; the padding round the outline stays fully see-through
	for variant: String in UiSkin.BUTTONS:
		for state: String in ["normal", "hover", "pressed"]:
			var image: Image = UiSkin.button(variant, state).texture.get_image()
			for y: int in image.get_height():
				for x: int in image.get_width():
					if x < UiSkin.BUTTON_PAD or x >= image.get_width() - UiSkin.BUTTON_PAD or y >= image.get_height() - UiSkin.BUTTON_PAD:
						assert_eq(image.get_pixel(x, y).a, 0.0, "%s/%s has no shadow at %d,%d" % [variant, state, x, y])


func test_the_faster_drawing_gives_the_same_pixels_as_before() -> void:
	# recorded from the per-pixel drawing before the half-and-mirror change; the face and plate colours re-recorded for the
	# visual pass (a 12 px lip, a brighter lit edge, a stronger side light; again for the reference gold and the squarer corner)
	var gold: Image = UiSkin.button("gold", "normal").texture.get_image()
	assert_almost_eq(gold.get_pixel(32, 30).g, 0.7529, 0.002, "the face")
	assert_almost_eq(gold.get_pixel(32, 30).b, 0.1765, 0.002)
	assert_eq(gold.get_pixel(32, 30).a, 1.0)
	var glass: Image = UiSkin.plate("glass", 0.0, 30).texture.get_image()
	assert_almost_eq(glass.get_pixel(55, 56).r, 0.1333, 0.002)
	assert_almost_eq(glass.get_pixel(55, 56).b, 0.3804, 0.002)
	assert_almost_eq(glass.get_pixel(21, 56).a, 0.5451, 0.002, "the plate's shadow")
	var picked: Image = UiSkin.plate("picked", 0.0, 22).texture.get_image()
	assert_almost_eq(picked.get_pixel(47, 48).b, 0.4627, 0.002)


func test_warming_builds_one_texture_per_call_and_ends_with_everything_built() -> void:
	UiSkin._cache.clear()
	UiSkin._warmed = 0
	assert_eq(UiSkin.warm_total(), UiSkin.BUTTONS.size() * 3 + UiSkin.WARM_PLATES.size())
	assert_eq(UiSkin.warm_fraction(), 0.0)
	var before: int = UiSkin._cache.size()
	UiSkin.warm_next()
	assert_eq(UiSkin._cache.size(), before + 1, "one texture per call")
	var calls: int = 1
	while not UiSkin.warm_next():
		calls += 1
		assert_lt(calls, 200, "it ends")
	assert_eq(UiSkin.warm_fraction(), 1.0)
	assert_eq(UiSkin._cache.size(), UiSkin.warm_total(), "every texture built exactly once")
	assert_true(UiSkin.warm_next(), "asking again does nothing and says it is done")


func test_after_warming_the_first_screens_draw_nothing_new() -> void:
	UiSkin._cache.clear()
	UiSkin._warmed = 0
	while not UiSkin.warm_next():
		pass
	var warmed: Array = UiSkin._cache.keys()
	var screen: TitleScreen = _phone()
	await wait_frames(3)
	for id: StringName in [&"codex", &"history", &"settings", &"howto"]:
		screen.open_page(id)
	screen.codex_page.show_tab(&"weapons")
	screen.weapons_page.tiles[0].pressed.emit()
	screen.dialog.close(false)
	screen.codex_page.show_tab(&"characters")
	screen.characters_page.cards["toni"].pressed.emit()
	screen.dialog.close(false)
	screen.open_name_editor()
	screen.dialog.close(false)
	screen.open_quit_confirm()
	screen.dialog.close(false)
	var results: MatchResults = autofree(MatchResults.new()) as MatchResults
	add_child(results)
	var extra: Array[String] = []
	for key: String in UiSkin._cache.keys():
		if not warmed.has(key):
			extra.append(key)
	assert_eq(extra, [] as Array[String], "these were drawn on first use instead of during loading: add them to WARM_PLATES")


func test_the_boot_screen_warms_the_skin_one_texture_a_frame_and_counts_it_in_the_bar() -> void:
	UiSkin._cache.clear()
	UiSkin._warmed = 0
	var boot: BootScreen = autofree(BOOT_SCENE.instantiate()) as BootScreen
	add_child(boot)
	boot.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	boot.set_process(false)
	boot._process(0.016)
	assert_eq(UiSkin._cache.size(), 1, "one texture in the first frame")
	assert_gt(UiSkin.warm_fraction(), 0.0)
	for i: int in UiSkin.warm_total():
		boot._process(0.016)
	assert_eq(UiSkin.warm_fraction(), 1.0)
	assert_gte(boot._loaded, BootScreen.SKIN_SHARE - 0.001, "the skin is its share of the bar")
	assert_lte(boot._loaded, 1.0)


# ---- the hero -------------------------------------------------------------------------

func test_the_hero_fills_a_tall_phone_and_stays_2_5_to_4() -> void:
	var standard: TitleScreen = _phone(1280.0)
	await wait_frames(3)
	var standard_card: Vector2 = standard.carousel.card_size
	var tall: TitleScreen = _phone(1558.0)
	await wait_frames(3)
	var tall_card: Vector2 = tall.carousel.card_size
	assert_gt(tall_card.y, standard_card.y + 60.0, "a taller phone gets a bigger card")
	for card: Vector2 in [standard_card, tall_card]:
		assert_almost_eq(card.x / card.y, TitleScreen.HERO_RATIO, 0.01, "1.75:2.5")
		assert_gte(card.y, TitleScreen.HERO_MIN_HEIGHT)
		assert_lte(card.y, TitleScreen.HERO_MAX_HEIGHT)
	var stage: Control = tall.carousel.get_parent() as Control
	assert_lte(tall_card.y + TitleScreen.HERO_AIR, stage.size.y + 1.0, "it fits its stage with room for the glow")
	assert_eq(tall.carousel.cards[0].size, tall_card, "the cards themselves are that size")


func test_the_chosen_mode_survives_the_hero_being_resized() -> void:
	var screen: TitleScreen = _phone(1558.0)
	await wait_frames(3)
	screen.carousel.select(2, false)
	var wide: Control = screen.carousel.get_parent() as Control
	wide.size = Vector2(wide.size.x, wide.size.y - 140.0)
	await wait_frames(3)
	assert_eq(screen.carousel.index, 2, "still on Practice")
	assert_eq(screen.mode_name_label.text, "Practice")


# ---- mode facts -----------------------------------------------------------------------

func test_the_mode_facts_come_from_the_rules() -> void:
	var screen: TitleScreen = _phone()
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	assert_eq(rules.first_to_choices, [3, 5, 7] as Array[int], "the numbers below follow this")
	assert_eq(screen.mode_facts("hotspot"), ["2 to 6 players", "First to 3, 5 or 7"] as Array[String])
	assert_eq(screen.mode_facts("online"), ["2 to 6 players", "Join with a code"] as Array[String])
	assert_eq(screen.mode_facts("practice"), ["Solo", "3 v 3 against bots"] as Array[String], "you and two allies against three")
	assert_eq(RoomService.MAX_TEAM_SIZE * 2, 6)


func test_the_chips_follow_the_mode_and_step_aside_on_a_short_stage() -> void:
	var tall: TitleScreen = _phone(1558.0)
	await wait_frames(3)
	assert_true(tall.mode_chips.visible, "room for them on a tall phone")
	assert_eq(tall.mode_chips.get_child_count(), 2)
	tall.carousel.select(2, false)
	assert_eq((tall.mode_chips.get_child(0).get_node("Text") as Label).text, "Solo")
	var short: TitleScreen = _phone(1280.0)
	await wait_frames(3)
	assert_false(short.mode_chips.visible, "on a 16:9 phone the hero keeps its size")


# ---- slides ---------------------------------------------------------------------------

func test_a_page_slides_in_from_the_side_its_tab_is_on() -> void:
	Settings.set_reduced_motion(false)
	var screen: TitleScreen = _phone()
	await wait_frames(3)
	screen.open_page(&"settings")
	assert_gt(screen.pages[&"settings"].position.x, 10.0, "from the right: Settings is right of Play")
	await wait_seconds(0.4)
	assert_eq(screen.pages[&"settings"].position, Vector2.ZERO, "and it settles")
	screen.open_page(&"street")
	assert_lt(screen.pages[&"street"].position.x, -10.0, "from the left: Street is left of Settings")
	await wait_seconds(0.4)
	screen.close_page()
	assert_gt(screen._home.position.x, 10.0, "Play comes back from the right when you leave Street")
	await wait_seconds(0.4)
	screen.open_page(&"settings")
	await wait_seconds(0.4)
	screen.close_page()
	assert_lt(screen._home.position.x, -10.0, "and from the left when you leave Settings")


func test_with_reduce_motion_pages_just_appear() -> void:
	var screen: TitleScreen = _phone()
	screen.open_page(&"settings")
	assert_eq(screen.pages[&"settings"].position, Vector2.ZERO)
	assert_eq(screen.pages[&"settings"].modulate.a, 1.0)


# ---- the bottom bar ---------------------------------------------------------------------

func test_the_open_tab_is_bigger_and_shows_its_name_and_its_arrows() -> void:
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	bar.size = Vector2(720.0, UiTabBar.HEIGHT)
	await wait_frames(3)
	bar.set_active(&"home")
	var open: TextureRect = bar.tabs[&"home"].get_node("Picture") as TextureRect
	var idle: TextureRect = bar.tabs[&"street"].get_node("Picture") as TextureRect
	assert_gt(open.size.x, idle.size.x * 1.2, "the open tab's picture is bigger")
	assert_gt(bar.tabs[&"home"].size.x, bar.tabs[&"street"].size.x, "the open tab is the wide one (room for its arrows)")
	assert_almost_eq(bar.tabs[&"street"].position.x, UiTabBar.SIDE_MARGIN, 0.5, "the strip starts a margin in from the left")
	assert_almost_eq(bar.tabs[&"settings"].position.x + bar.tabs[&"settings"].size.x, 720.0 - UiTabBar.SIDE_MARGIN, 0.5, "and ends a margin in from the right")
	assert_lt(open.global_position.y, bar.global_position.y + UiTabBar.BAR_TOP, "the open picture rises above the bar (owner: like the reference)")
	assert_gt(idle.global_position.y, bar.global_position.y + UiTabBar.BAR_TOP, "an idle picture sits inside it")
	assert_almost_eq(open.global_position.y + open.size.y / 2.0, bar.global_position.y + UiTabBar.BAR_TOP, 1.0, "the open picture is centred on the bar's top edge (owner)")
	assert_true((bar.tabs[&"home"].get_node("Name") as Label).visible, "it shows its name")
	assert_false((bar.tabs[&"street"].get_node("Name") as Label).visible, "the others do not")
	assert_true(bar.arrow_left.visible and bar.arrow_right.visible, "Play has an arrow on each side")
	var picture_bottom: float = open.global_position.y + open.size.y
	for arrow: Button in [bar.arrow_left, bar.arrow_right]:
		var drawn: Control = arrow.get_node("Arrow") as Control
		assert_almost_eq(drawn.global_position.y + drawn.size.y, picture_bottom, 1.0, "the bottom of each arrow is level with the bottom of the picture (owner)")
	var bar_middle: float = bar.global_position.y + (UiTabBar.BAR_TOP + UiTabBar.HEIGHT) / 2.0
	assert_almost_eq(idle.global_position.y + idle.size.y / 2.0, bar_middle, 1.0, "an idle picture is centred in the bar (owner)")
	var left_arrow: Control = bar.arrow_left.get_node("Arrow") as Control
	var right_arrow: Control = bar.arrow_right.get_node("Arrow") as Control
	assert_lt(left_arrow.global_position.x + left_arrow.size.x, open.global_position.x, "one on its left, clear of the picture")
	assert_gt(right_arrow.global_position.x, open.global_position.x + open.size.x, "one on its right, clear of the picture")
	assert_lte(left_arrow.size.x, UiMetrics.MIN_TEXT * 1.5, "and small")


func test_the_ends_have_only_the_arrow_that_leads_somewhere() -> void:
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	bar.size = Vector2(720.0, UiTabBar.HEIGHT)
	await wait_frames(3)
	bar.set_active(&"street")
	assert_false(bar.arrow_left.visible, "Street is the first tab: nothing to its left")
	assert_true(bar.arrow_right.visible)
	bar.set_active(&"settings")
	assert_true(bar.arrow_left.visible)
	assert_false(bar.arrow_right.visible, "Settings is the last tab: nothing to its right")


func test_an_arrow_goes_to_the_neighbouring_tab() -> void:
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	bar.size = Vector2(720.0, UiTabBar.HEIGHT)
	await wait_frames(2)
	var heard: Array[StringName] = []
	bar.tab_pressed.connect(func(id: StringName) -> void: heard.append(id))
	bar.set_active(&"home")
	bar.arrow_left.pressed.emit()
	bar.arrow_right.pressed.emit()
	assert_eq(heard, [&"codex", &"history"] as Array[StringName], "left of Play is Cards, right of it History")
	assert_gte(bar.arrow_left.size.x, UiMetrics.MIN_TARGET, "an arrow is a thumb-sized target")
	assert_gte(bar.arrow_left.size.y, UiMetrics.MIN_TARGET)


func test_with_motion_the_open_tab_grows_over_a_moment_and_with_reduce_motion_at_once() -> void:
	Settings.set_reduced_motion(false)
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	bar.size = Vector2(720.0, UiTabBar.HEIGHT)
	await wait_frames(3)
	bar.set_active(&"home")
	await wait_seconds(0.35)
	var big: float = (bar.tabs[&"home"].get_node("Picture") as TextureRect).size.x
	var small: float = (bar.tabs[&"settings"].get_node("Picture") as TextureRect).size.x
	assert_gt(big, small, "Play is open")
	bar.set_active(&"settings")
	await wait_seconds(0.05)
	var home_now: float = (bar.tabs[&"home"].get_node("Picture") as TextureRect).size.x
	var settings_now: float = (bar.tabs[&"settings"].get_node("Picture") as TextureRect).size.x
	assert_lt(home_now, big, "Play is shrinking")
	assert_gt(home_now, small, "but still on its way")
	assert_gt(settings_now, small, "Settings is growing")
	assert_lt(settings_now, big, "but not as big as it will be")
	await wait_seconds(0.35)
	assert_almost_eq((bar.tabs[&"settings"].get_node("Picture") as TextureRect).size.x, big, 1.0, "arrived")
	Settings.set_reduced_motion(true)
	bar.set_active(&"street")
	assert_almost_eq((bar.tabs[&"street"].get_node("Picture") as TextureRect).size.x, big, 1.0, "with Reduce motion it is there at once")


# ---- idle motion ----------------------------------------------------------------------

func test_idle_motion_rests_under_a_page_and_a_sheet_and_returns() -> void:
	Settings.set_reduced_motion(false)
	var screen: TitleScreen = _phone()
	await wait_frames(3)
	assert_eq(screen.carousel.bob, TitleScreen.AMBIENT_BOB)
	assert_true(screen.backdrop.ambient)
	screen.open_page(&"history")
	assert_eq(screen.carousel.bob, 0.0, "nothing to bob under a page")
	assert_false(screen.backdrop.ambient)
	assert_false(screen.backdrop.motes.emitting, "the lights rest too")
	screen.close_page()
	assert_eq(screen.carousel.bob, TitleScreen.AMBIENT_BOB)
	assert_true(screen.backdrop.motes.emitting)
	var card: MenuDialog = screen.open_quit_confirm()
	assert_eq(screen.carousel.bob, 0.0, "and under a sheet")
	card.close(false)
	await wait_process_frames(2)
	assert_eq(screen.carousel.bob, TitleScreen.AMBIENT_BOB, "back when the sheet closes")
