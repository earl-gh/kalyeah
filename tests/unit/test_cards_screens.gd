extends GutTest
## The two screens behind the Cards hub (Kids: 2 columns by 3 rows; Skills: 4 columns) and the bottom drawer a card opens: counts, columns,
## names, tags, the drawer's content and every way to close it, stepping to the next card, the order Back goes in, and both phone sizes.
## Facts (positions, sizes, visibility, order), not pixels.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const RULES_PATH: String = "res://data/rules/game_rules.tres"
const SHORT_PHONE: float = 1280.0
const TALL_PHONE: float = 1558.0

var _saved_motion: bool
var _was_emulating: bool


func before_each() -> void:
	_saved_motion = Settings.reduced_motion
	_was_emulating = Input.emulate_touch_from_mouse


func after_each() -> void:
	Settings.set_reduced_motion(_saved_motion)
	Input.emulate_touch_from_mouse = _was_emulating


func _rules() -> GameRules:
	return load(RULES_PATH) as GameRules


func _phone(height: float = SHORT_PHONE, still: bool = true) -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, height)
	phone.size = Vector2(720.0, height)
	add_child(phone)
	# a page slides in over a quarter of a second: measured with Reduce motion on, it is where it ends up
	Settings.set_reduced_motion(still)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


## The title screen on the Kids (`view` CHARACTERS) or Skills (WEAPONS) screen.
func _open(view: StringName, height: float = SHORT_PHONE, still: bool = true) -> TitleScreen:
	var screen: TitleScreen = _phone(height, still)
	screen.open_page(&"codex")
	await wait_frames(2)
	screen.codex_page.show_view(view)
	await wait_frames(3)
	return screen


func _press(at: Vector2, pressed: bool = true) -> InputEventMouseButton:
	var event: InputEventMouseButton = InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	event.position = at
	event.global_position = at
	return event


func _move(at: Vector2) -> InputEventMouseMotion:
	var event: InputEventMouseMotion = InputEventMouseMotion.new()
	event.position = at
	event.global_position = at
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	return event


func _back_key() -> void:
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_frames(2)


func test_dragging_real_and_unknown_cards_scrolls_the_inline_collection_without_opening_a_drawer() -> void:
	Input.emulate_touch_from_mouse = true
	Settings.set_reduced_motion(true)
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	viewport.size = Vector2i(720, 1280)
	add_child(viewport)
	var screen: TitleScreen = TITLE_SCENE.instantiate() as TitleScreen
	viewport.add_child(screen)
	screen.open_page(&"codex")
	await wait_frames(5)
	for index: int in [0, 7]:
		var before: int = screen.codex_page.hub.scroll.scroll_vertical
		var at: Vector2 = screen.codex_page.kids_screen.cells[index].get_global_rect().get_center()
		viewport.push_input(TouchHelper.click(at, true))
		for move: int in 6:
			at.y -= 25.0
			var event: InputEventMouseMotion = TouchHelper.motion(at)
			event.relative = Vector2(0.0, -25.0)
			viewport.push_input(event)
		viewport.push_input(TouchHelper.click(at, false))
		await wait_frames(3)
		assert_gt(screen.codex_page.hub.scroll.scroll_vertical, before, "a card drag reaches the shared scroll")
		assert_false(screen.card_drawer.is_open)


func test_an_open_drawer_intercepts_touches_over_the_navigation() -> void:
	Settings.set_reduced_motion(true)
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	viewport.size = Vector2i(720, 1280)
	add_child(viewport)
	var screen: TitleScreen = TITLE_SCENE.instantiate() as TitleScreen
	viewport.add_child(screen)
	screen.open_page(&"codex")
	await wait_frames(4)
	screen.codex_page.kids_screen.cells[0].pressed.emit()
	await wait_frames(4)
	var at: Vector2 = screen.corner_buttons[&"settings"].get_global_rect().get_center()
	viewport.push_input(TouchHelper.click(at, true))
	viewport.push_input(TouchHelper.click(at, false))
	await wait_frames(2)
	assert_true(screen.card_drawer.is_open)
	assert_eq(screen.current_page, &"codex", "the covered navigation cannot switch tabs")
	screen.card_drawer.set_bottom_inset(40.0)
	await wait_frames(3)
	assert_lte(screen.card_drawer.handle.get_global_rect().end.y, 1240.0, "the grabber stays above the home inset")


# ---- the grids ---------------------------------------------------------------------------

func test_kids_have_two_rows_of_five_with_four_unknown_junjun_cards() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	assert_eq(kids.cells.size(), 10)
	assert_eq(kids.grid.columns, 5)
	var xs: Dictionary[int, bool] = {}
	var ys: Dictionary[int, bool] = {}
	for i: int in kids.cells.size():
		var cell: CardCell = kids.cells[i]
		xs[int(cell.position.x)] = true
		ys[int(cell.position.y)] = true
		assert_false(cell.name_label.visible)
		assert_false(cell.tag.visible)
		if i < Session.AVATARS.size():
			assert_false(cell.disabled)
			assert_eq(cell.picture.texture, CharactersPage.card_picture(StringName(Session.AVATARS[i])))
		else:
			assert_true(cell.disabled)
			assert_eq(cell.picture.texture, CharactersPage.card_picture(&"junjun"))
			assert_lt(cell.picture.modulate.v, 0.2)
			assert_eq((cell.find_child("Unknown", true, false) as Label).text, "Coming\nSoon")
	assert_eq(xs.size(), 5)
	assert_eq(ys.size(), 2)


func test_skills_have_three_rows_of_five_with_one_unknown_bato_light_card() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var skills: SkillsScreen = screen.codex_page.skills_screen
	assert_eq(skills.cells.size(), 15)
	assert_eq(skills.grid.columns, 5)
	var ys: Dictionary[int, bool] = {}
	for i: int in skills.cells.size():
		var cell: CardCell = skills.cells[i]
		ys[int(cell.position.y)] = true
		assert_false(cell.name_label.visible)
		assert_false(cell.tag.visible)
		if i < _rules().weapons.size():
			assert_eq(cell.picture.texture, CardStyle.picture(_rules().weapons[i].id))
			assert_false(cell.disabled)
		else:
			assert_true(cell.disabled)
			assert_not_null(CardStyle.picture(&"bato_light"), "the light rock has a card picture")
			assert_eq(cell.picture.texture, CardStyle.picture(&"bato_light"), "the locked skill card is drawn from the light rock's card")
			assert_ne(cell.picture.texture, CharactersPage.card_picture(&"junjun"), "not a kid's card")
			assert_not_null(cell.find_child("Unknown", true, false))
	assert_eq(ys.size(), 3)
	watch_signals(screen.card_drawer)
	skills.cells[14].pressed.emit()
	screen.codex_page.kids_screen.cells[9].pressed.emit()
	assert_false(screen.card_drawer.is_open, "unknown cards do not open fabricated details")


func test_both_inline_grids_keep_their_pictures_and_touch_targets_inside_the_screen() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _open(CodexPage.HUB, height)
		for grid: CardsScreen in [screen.codex_page.kids_screen, screen.codex_page.skills_screen]:
			assert_eq(grid.scroll, screen.codex_page.hub.scroll, "one scroll for both categories")
			assert_null(grid.find_child("BackButton", true, false), "no inner-page Back button")
			var seen: Array[Rect2] = []
			for cell: CardCell in grid.cells:
				var rect: Rect2 = cell.get_global_rect()
				assert_gte(rect.size.x, UiMetrics.MIN_TARGET)
				assert_gte(rect.size.y, UiMetrics.MIN_TARGET)
				assert_gte(rect.position.x, 0.0)
				assert_lte(rect.end.x, 720.0)
				assert_true(rect.encloses(cell.picture.get_global_rect().grow(-0.5)))
				for other: Rect2 in seen:
					assert_false(rect.grow(-1.0).intersects(other.grow(-1.0)))
				seen.append(rect)




func test_cards_have_names_focus_rings_and_the_press_feel() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var toni: CardCell = screen.codex_page.kids_screen.cells[Session.AVATARS.find("toni")]
	assert_eq(toni.accessibility_name, "Toni: read about this kid")
	assert_eq(toni.mouse_filter, Control.MOUSE_FILTER_PASS, "a drag that starts on a card scrolls the grid")
	assert_true(toni.has_theme_stylebox_override("focus"), "a focus ring")
	assert_true(toni.has_meta(&"feels_press"), "the shared squeeze")
	for cell: CardCell in screen.codex_page.skills_screen.cells:
		assert_false(cell.accessibility_name.is_empty())
		if not cell.disabled:
			assert_string_contains(cell.accessibility_name, cell.name_label.text)
	toni.button_down.emit()
	assert_ne(toni.picture.modulate, Color.WHITE, "a press dims the picture, so Reduce motion still shows the touch")
	toni.button_up.emit()
	assert_eq(toni.picture.modulate, Color.WHITE)


# ---- the drawer opens -----------------------------------------------------------------------

func test_tapping_a_kid_opens_its_name_and_description_with_the_card_behind_the_sheet() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	var drawer: CardDrawer = screen.card_drawer
	assert_false(drawer.is_open)
	assert_false(drawer.visible)
	for i: int in Session.AVATARS.size():
		var id: String = Session.AVATARS[i]
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		kids.cells[i].pressed.emit()
		await wait_frames(2)
		assert_true(drawer.is_open, "%s opens the drawer" % id)
		assert_eq(drawer.index, i)
		assert_eq(drawer.title_label.text, definition.display_name)
		assert_eq(drawer.picture.texture, CharactersPage.card_picture(StringName(id)), "the card, bare")
		assert_eq((drawer.find_child("About", true, false) as Label).text, definition.description)
		assert_null(drawer.find_child("Palette", true, false), "only the short description remains")
		assert_lt(drawer.picture.z_index, drawer.sheet.z_index)
		assert_eq(drawer.picture.get_parent(), drawer.slider)
		assert_lt(drawer.picture.get_global_rect().position.y, drawer.sheet.get_global_rect().position.y)
		assert_gt(drawer.picture.get_global_rect().end.y, drawer.sheet.get_global_rect().position.y)
		assert_null(drawer.find_child("Close", true, false), "the drawer has no footer Close button")
		assert_eq(drawer.content.get_children().filter(func(node: Node) -> bool: return node.name == "Picture").size(), 0, "no second picture")
		drawer.close(false)


func test_tapping_a_skill_opens_its_picture_name_and_short_description_only() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var drawer: CardDrawer = screen.card_drawer
	for i: int in _rules().weapons.size():
		var weapon: WeaponDef = _rules().weapons[i]
		screen.codex_page.skills_screen.cells[i].pressed.emit()
		await wait_frames(3)
		assert_true(drawer.is_open)
		assert_eq(drawer.title_label.text, weapon.display_name)
		assert_eq(drawer.picture.texture, CardStyle.picture(weapon.id))
		assert_eq((drawer.find_child("About", true, false) as Label).text, weapon.description)
		assert_null(drawer.find_child("Figures", true, false))
		assert_null(drawer.find_child("GameBlurb", true, false))
		assert_eq(drawer.sections.size(), 1)
		assert_null(drawer.find_child("CloseButton", true, false))
		assert_null(drawer.find_child("OkayButton", true, false))
		drawer.close(false)


func test_drawer_details_are_one_description_without_stats_or_palette() -> void:
	for parts: Array[Control] in [DrawerContent.of_weapon(_rules().weapons[2]), DrawerContent.of_kid(load("res://data/characters/toni.tres") as CharacterDef)]:
		assert_eq(parts.size(), 1)
		assert_eq(parts[0].name, &"About")
		parts[0].queue_free()
	assert_eq(DrawerContent.of_kid(null).size(), 0)


func test_an_open_card_wears_the_gold_silhouette_edge_while_its_drawer_is_open() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	for cell: CardCell in kids.cells:
		assert_false(cell.edge.visible, "no edge before a tap")
	kids.cells[2].pressed.emit()
	await wait_frames(2)
	for i: int in kids.cells.size():
		assert_eq(kids.cells[i].edge.visible, i == 2, "only the open card has the edge")
	var edge: CardCell = kids.cells[2]
	assert_eq(edge.edge.material, HandCard.silhouette_material(), "drawn with silhouette.gdshader, as the hand's lift cue is")
	assert_eq(kids.selected, 2)
	screen.card_drawer.close(false)
	assert_eq(kids.selected, -1)
	for cell: CardCell in kids.cells:
		assert_false(cell.edge.visible, "the edge goes when the drawer does")


# ---- the drawer closes ----------------------------------------------------------------------

func test_the_drawer_closes_by_scrim_handle_and_the_phones_back() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var drawer: CardDrawer = screen.card_drawer
	var opened: Callable = func() -> void:
		screen.codex_page.kids_screen.cells[1].pressed.emit()
		await wait_frames(2)
		assert_true(drawer.is_open)
	await opened.call()
	drawer.scrim.gui_input.emit(_press(Vector2(100.0, 100.0)))
	assert_false(drawer.is_open, "a tap on the scrim")
	await opened.call()
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 20.0)))
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 20.0), false))
	assert_false(drawer.is_open, "a tap on the handle")
	await opened.call()
	await _back_key()
	assert_false(drawer.is_open, "the phone's Back")
	assert_true(screen.codex_page.hub.is_visible_in_tree(), "the inline collection stays underneath")


func test_a_swipe_down_on_the_handle_or_the_title_closes_and_a_short_one_springs_back() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var drawer: CardDrawer = screen.card_drawer
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	await wait_frames(2)
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 300.0)))
	drawer.handle.gui_input.emit(_move(Vector2(300.0, 340.0)))
	assert_almost_eq(drawer.slider.position.y, 0.0, 0.01, "with Reduce motion the sheet does not follow the finger")
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 340.0), false))
	assert_true(drawer.is_open, "a short swipe does not close it")
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 300.0)))
	drawer.handle.gui_input.emit(_move(Vector2(300.0, 400.0)))
	drawer.handle.gui_input.emit(_press(Vector2(300.0, 300.0 + CardDrawer.CLOSE_SWIPE + 20.0), false))
	assert_false(drawer.is_open, "a long swipe down closes it")
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	await wait_frames(2)
	drawer.header_row.gui_input.emit(_press(Vector2(200.0, 500.0)))
	drawer.header_row.gui_input.emit(_press(Vector2(200.0, 500.0 + CardDrawer.CLOSE_SWIPE + 20.0), false))
	assert_false(drawer.is_open, "and so does a swipe down from the title row")


func test_the_drawer_slides_up_unless_reduce_motion_is_on() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS, SHORT_PHONE, true)
	var drawer: CardDrawer = screen.card_drawer
	screen.codex_page.kids_screen.cells[0].pressed.emit()
	assert_eq(drawer.slider.position, Vector2.ZERO, "Reduce motion: it is simply there")
	assert_eq(drawer.scrim.modulate.a, 1.0)
	drawer.close()
	assert_false(drawer.visible, "and simply gone")
	Settings.set_reduced_motion(false)
	screen.codex_page.kids_screen.cells[0].pressed.emit()
	assert_gt(drawer.slider.position.y, 100.0, "it starts below the screen")
	await wait_seconds(CardDrawer.SLIDE_SECONDS + 0.25)
	assert_almost_eq(drawer.slider.position.y, 0.0, 0.5, "and arrives in about a quarter of a second")
	assert_eq(drawer.scrim.modulate.a, 1.0)
	drawer.close()
	assert_false(drawer.is_open, "closed at once as far as the screen is concerned")
	assert_true(drawer.visible, "while it slides down")
	await wait_seconds(CardDrawer.SLIDE_SECONDS + 0.25)
	assert_false(drawer.visible)


# ---- stepping to the neighbour ---------------------------------------------------------------

func test_the_drawer_steps_to_the_next_and_previous_card_and_wraps() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	var drawer: CardDrawer = screen.card_drawer
	kids.cells[0].pressed.emit()
	await wait_frames(2)
	drawer.next_button.pressed.emit()
	assert_eq(drawer.index, 1)
	assert_eq(drawer.title_label.text, kids.entries[1]["name"], "the details follow")
	assert_eq(drawer.picture.texture, kids.entries[1]["picture"])
	assert_true(kids.cells[1].edge.visible, "the gold edge moves with it")
	assert_false(kids.cells[0].edge.visible)
	assert_eq(kids.selected, 1)
	drawer.previous_button.pressed.emit()
	drawer.previous_button.pressed.emit()
	assert_eq(drawer.index, Session.AVATARS.size() - 1, "unknown placeholders are excluded from drawer stepping")
	drawer.next_button.pressed.emit()
	assert_eq(drawer.index, 0, "after the last is the first")
	for button: Button in [drawer.previous_button, drawer.next_button]:
		assert_gte(button.get_global_rect().size.x, UiMetrics.MIN_TARGET, "the arrows are big targets")
		assert_gte(button.get_global_rect().size.y, UiMetrics.MIN_TARGET)
		assert_false(button.accessibility_name.is_empty())


func test_a_swipe_along_the_picture_steps_and_a_vertical_one_does_not() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var skills: SkillsScreen = screen.codex_page.skills_screen
	var drawer: CardDrawer = screen.card_drawer
	skills.cells[3].pressed.emit()
	await wait_frames(2)
	drawer.picture_row.gui_input.emit(_press(Vector2(360.0, 600.0)))
	drawer.picture_row.gui_input.emit(_move(Vector2(360.0 - CardDrawer.STEP_SWIPE - 10.0, 605.0)))
	assert_eq(drawer.index, 4, "a swipe left shows the next card")
	drawer.picture_row.gui_input.emit(_press(Vector2(360.0, 600.0), false))
	drawer.picture_row.gui_input.emit(_press(Vector2(360.0, 600.0)))
	drawer.picture_row.gui_input.emit(_move(Vector2(360.0 + CardDrawer.STEP_SWIPE + 10.0, 600.0)))
	assert_eq(drawer.index, 3, "a swipe right shows the one before")
	drawer.picture_row.gui_input.emit(_press(Vector2(360.0, 600.0), false))
	drawer.picture_row.gui_input.emit(_press(Vector2(360.0, 600.0)))
	drawer.picture_row.gui_input.emit(_move(Vector2(365.0, 600.0 - 300.0)))
	assert_eq(drawer.index, 3, "a drag up or down is the details scrolling, not a step")


# ---- where the drawer sits ----------------------------------------------------------------

func test_the_drawer_is_the_modals_slate_with_rounded_top_corners_and_a_handle() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var drawer: CardDrawer = screen.card_drawer
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	await wait_frames(2)
	var box: StyleBoxFlat = drawer.sheet.get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(box.bg_color, KalyeahTheme.MODAL_FRAME, "the modals' frame colour")
	assert_eq(box.border_color, KalyeahTheme.MODAL_FRAME_EDGE)
	assert_gt(box.corner_radius_top_left, 0, "rounded at the top")
	assert_gt(box.corner_radius_top_right, 0)
	assert_eq(box.corner_radius_bottom_left, 0, "square at the bottom, where it meets the bar")
	var dialog: MenuDialog = autofree(MenuDialog.create("x")) as MenuDialog
	assert_eq(box.bg_color, (dialog.panel.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, "the same as a modal's frame")
	var inner: StyleBoxFlat = drawer.find_child("Inner", true, false).get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(inner.bg_color, KalyeahTheme.MODAL_BODY, "with the modals' light body")
	var pill: Control = drawer.handle.find_child("Pill", true, false) as Control
	assert_not_null(pill, "a grab handle")
	assert_gt(pill.get_global_rect().size.x, 60.0)
	assert_gte(drawer.handle.get_global_rect().size.x, UiMetrics.MIN_TARGET, "the grabber spans a broad target")
	assert_gte(drawer.handle.get_global_rect().size.y, CardDrawer.HANDLE_STRIP)


func test_the_drawer_covers_navigation_and_keeps_the_grabber_reachable_on_both_phones() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _open(CodexPage.HUB, height)
		var drawer: CardDrawer = screen.card_drawer
		for grid: CardsScreen in [screen.codex_page.kids_screen, screen.codex_page.skills_screen]:
			for i: int in (Session.AVATARS.size() if grid is KidsScreen else _rules().weapons.size()):
				grid.cells[i].pressed.emit()
				await wait_frames(3)
				var sheet: Rect2 = drawer.sheet.get_global_rect()
				assert_almost_eq(sheet.end.y, height, 1.0, "the drawer reaches the phone bottom")
				assert_almost_eq(sheet.size.x, 720.0, 1.0)
				assert_lte(sheet.size.y, height * CardDrawer.MAX_HEIGHT_SHARE + 1.0)
				assert_gt(drawer.z_index, screen.tab_bar.z_index)
				assert_true(sheet.encloses(drawer.handle.get_global_rect()))
				assert_gte(drawer.handle.size.y, CardDrawer.HANDLE_STRIP)
				drawer.close(false)


func test_long_details_scroll_without_pushing_the_grabber_off_the_sheet() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var drawer: CardDrawer = screen.card_drawer
	var long: Dictionary = screen.codex_page.skills_screen.entries[0].duplicate()
	long["content"] = func() -> Array[Control]:
		var label: Label = UiKit.text("A long description. ".repeat(200), UiMetrics.BODY)
		return [label] as Array[Control]
	drawer.open_entries([long, screen.codex_page.skills_screen.entries[1]] as Array[Dictionary], 0)
	await wait_frames(5)
	drawer.scroll.scroll_vertical = 100000
	await wait_frames(2)
	assert_gt(drawer.scroll.scroll_vertical, 0)
	assert_true(drawer.sheet.get_global_rect().encloses(drawer.handle.get_global_rect()))
	drawer.step(1)
	assert_eq(drawer.scroll.scroll_vertical, 0)


func test_back_closes_the_drawer_then_returns_home_without_an_inner_page() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	await wait_frames(2)
	await _back_key()
	assert_false(screen.card_drawer.is_open)
	assert_eq(screen.current_page, &"codex")
	await _back_key()
	assert_eq(screen.current_page, &"")
	assert_null(screen.dialog)
	await _back_key()
	assert_not_null(screen.dialog)


func test_the_back_button_the_cards_tab_and_other_tabs_close_the_drawer_too() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	screen.codex_page.kids_screen.cells[0].pressed.emit()
	await wait_frames(2)
	screen.corner_buttons[&"codex"].pressed.emit()
	await wait_frames(2)
	assert_false(screen.card_drawer.is_open, "the lit Cards tab closes the drawer")
	assert_eq(screen.codex_page.current, CodexPage.HUB, "and goes to the hub")
	screen.codex_page.show_view(CodexPage.WEAPONS)
	screen.codex_page.skills_screen.cells[1].pressed.emit()
	await wait_frames(2)
	screen.corner_buttons[&"history"].pressed.emit()
	await wait_frames(2)
	assert_false(screen.card_drawer.is_open, "another tab closes it as well")


func test_two_taps_from_home_open_card_details_and_the_grabber_closes() -> void:
	var screen: TitleScreen = _phone()
	await wait_frames(3)
	var taps: int = 0
	screen.corner_buttons[&"codex"].pressed.emit()
	taps += 1
	await wait_frames(2)
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	taps += 1
	await wait_frames(2)
	assert_true(screen.card_drawer.is_open)
	assert_eq(taps, 2, "Cards tab, then a card")
	screen.card_drawer.handle.gui_input.emit(_press(Vector2(300.0, 20.0)))
	screen.card_drawer.handle.gui_input.emit(_press(Vector2(300.0, 20.0), false))
	assert_false(screen.card_drawer.is_open, "one tap to close")


func test_the_drawer_and_the_screens_work_on_the_tall_phone_too() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS, TALL_PHONE)
	var kids: KidsScreen = screen.codex_page.kids_screen
	assert_gt(kids.cells[5].get_global_rect().end.y, 0.0)
	kids.cells[5].pressed.emit()
	await wait_frames(3)
	var drawer: CardDrawer = screen.card_drawer
	assert_true(drawer.is_open)
	var sheet: Rect2 = drawer.sheet.get_global_rect()
	assert_gt(sheet.position.y, TALL_PHONE * 0.25, "a 70 percent sheet at most")
	assert_true(kids.scroll.get_global_rect().encloses(kids.cells[5].get_global_rect().grow(-2.0)) or kids.scroll.scroll_vertical > 0, "the opened card is scrolled into view")
