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


# ---- the grids ---------------------------------------------------------------------------

func test_kids_are_six_bare_card_pictures_in_two_columns_and_three_rows() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	assert_true(kids.is_visible_in_tree())
	assert_eq(kids.cells.size(), Session.AVATARS.size(), "one card per kid")
	assert_eq(kids.cells.size(), 6)
	assert_eq(kids.grid.columns, 2)
	var xs: Dictionary[int, bool] = {}
	var ys: Dictionary[int, bool] = {}
	for i: int in kids.cells.size():
		var cell: CardCell = kids.cells[i]
		var id: String = Session.AVATARS[i]
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		assert_eq(cell.picture.texture, CharactersPage.card_picture(StringName(id)), "%s shows the owner's card" % id)
		assert_null(cell.picture.material, "bare: no crop or shader over it")
		assert_eq(cell.get_theme_stylebox("normal").get_class(), "StyleBoxEmpty", "no plate behind it")
		assert_eq(cell.name_label.text, definition.display_name, "the name is under the card")
		assert_gt(cell.name_label.get_global_rect().position.y, cell.picture.get_global_rect().end.y - 1.0)
		assert_false(cell.tag.visible, "kids carry no kind tag")
		xs[int(cell.get_global_rect().position.x)] = true
		ys[int(cell.get_global_rect().position.y)] = true
	assert_eq(xs.size(), 2, "two columns")
	assert_eq(ys.size(), 3, "three rows")
	assert_eq(kids.header.find_child("Title", true, false).text, "Kids")


func test_skills_are_fourteen_cards_in_four_columns_with_the_last_row_from_the_left() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var skills: SkillsScreen = screen.codex_page.skills_screen
	var weapons: Array[WeaponDef] = _rules().weapons
	assert_eq(skills.cells.size(), weapons.size(), "one card per skill")
	assert_eq(skills.cells.size(), 14)
	assert_eq(skills.grid.columns, 4)
	var rows: Dictionary[int, Array] = {}
	for i: int in skills.cells.size():
		var cell: CardCell = skills.cells[i]
		var weapon: WeaponDef = weapons[i]
		assert_eq(cell.picture.texture, CardStyle.picture(weapon.id), "%s shows the owner's card" % weapon.id)
		assert_null(cell.picture.material)
		assert_eq(cell.get_theme_stylebox("normal").get_class(), "StyleBoxEmpty", "no plate behind it")
		assert_eq(cell.name_label.text, weapon.display_name)
		assert_gte(cell.name_label.get_theme_font_size("font_size"), UiMetrics.MIN_TEXT, "the small name still obeys the text floor")
		assert_lte(cell.name_label.get_line_count(), 2, "at most two rows of name")
		assert_true(cell.tag.visible, "a kind tag")
		var look: StyleBoxFlat = cell.tag.get_theme_stylebox("panel") as StyleBoxFlat
		assert_eq(look.bg_color, WeaponsPage.KIND_COLORS[weapon.kind], "%s: the tag is its kind's colour" % weapon.id)
		assert_lt(cell.tag.get_global_rect().size.y, 12.0, "thin: a tag, not a plate")
		assert_gt(cell.tag.get_global_rect().position.y, cell.picture.get_global_rect().end.y - 1.0, "under the picture")
		var row: int = int(cell.get_global_rect().position.y)
		if not rows.has(row):
			rows[row] = []
		(rows[row] as Array).append(int(cell.get_global_rect().position.x))
	var sizes: Array[int] = []
	var ordered: Array = rows.keys()
	ordered.sort()
	for row: int in ordered:
		sizes.append((rows[row] as Array).size())
	assert_eq(sizes, [4, 4, 4, 2] as Array[int], "4 + 4 + 4 + 2")
	var first_row: Array = rows[ordered[0]]
	var last_row: Array = rows[ordered[3]]
	assert_eq(last_row[0], first_row[0], "the last row starts under the first column")
	assert_eq(last_row[1], first_row[1], "and the second card under the second")
	assert_eq(skills.header.find_child("Title", true, false).text, "Skills")


func test_the_names_and_tags_are_whole_and_inside_their_cards_on_both_phones() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		for view: StringName in [CodexPage.CHARACTERS, CodexPage.WEAPONS]:
			var screen: TitleScreen = await _open(view, height)
			var current: CardsScreen = screen.codex_page.screen_shown()
			var bar_top: float = screen.tab_bar.get_global_rect().position.y
			assert_lte(current.get_global_rect().end.y, bar_top + 1.0, "the screen ends above the bar (%s %d)" % [view, int(height)])
			var seen: Array[Rect2] = []
			for cell: CardCell in current.cells:
				var rect: Rect2 = cell.get_global_rect()
				assert_gte(rect.size.x, UiMetrics.MIN_TARGET, "a big target")
				assert_gte(rect.size.y, UiMetrics.MIN_TARGET)
				assert_gte(rect.position.x, 0.0)
				assert_lte(rect.end.x, 720.0, "inside the screen")
				assert_true(rect.encloses(cell.name_label.get_global_rect().grow(-0.5)), "%s: the name is inside its cell" % cell.name)
				assert_true(rect.encloses(cell.picture.get_global_rect().grow(-0.5)))
				assert_lte(cell.name_label.get_line_count(), int(cell.entry["name_lines"]), "%s: the name fits its rows" % cell.name)
				for other: Rect2 in seen:
					assert_false(rect.grow(-1.0).intersects(other.grow(-1.0)), "no two cards overlap")
				seen.append(rect)
			assert_gte(current.back_button.get_global_rect().size.y, UiMetrics.MIN_TARGET, "Back is a big target")
			var top: Rect2 = current.back_button.get_global_rect()
			assert_gte(top.position.y, 0.0)
			assert_lt(top.end.y, current.scroll.get_global_rect().position.y + 1.0, "the header is above the grid")


func test_a_grid_scrolls_and_a_drag_that_starts_on_a_card_scrolls_without_opening_it() -> void:
	Input.emulate_touch_from_mouse = true
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	viewport.size = Vector2i(720, 900)
	add_child(viewport)
	var skills: SkillsScreen = SkillsScreen.new()
	skills.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	viewport.add_child(skills)
	await wait_process_frames(3)
	assert_gt(skills.scroll.get_v_scroll_bar().max_value, skills.scroll.size.y, "fourteen cards are taller than the screen, so it scrolls")
	watch_signals(skills)
	var from: Vector2 = skills.cells[5].get_global_rect().get_center()
	viewport.push_input(TouchHelper.click(from, true))
	var at: Vector2 = from
	for i: int in 12:
		at += Vector2(0.0, -25.0)
		var moved: InputEventMouseMotion = TouchHelper.motion(at)
		moved.relative = Vector2(0.0, -25.0)
		viewport.push_input(moved)
	viewport.push_input(TouchHelper.click(at, false))
	await wait_process_frames(2)
	assert_gt(skills.scroll.scroll_vertical, 100, "a drag that starts on a card scrolls the grid")
	assert_signal_not_emitted(skills, "drawer_requested", "scrolling is not a tap")


func test_cards_have_names_focus_rings_and_the_press_feel() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var toni: CardCell = screen.codex_page.kids_screen.cells[Session.AVATARS.find("toni")]
	assert_eq(toni.accessibility_name, "Toni: read about this kid")
	assert_eq(toni.mouse_filter, Control.MOUSE_FILTER_PASS, "a drag that starts on a card scrolls the grid")
	assert_true(toni.has_theme_stylebox_override("focus"), "a focus ring")
	assert_true(toni.has_meta(&"feels_press"), "the shared squeeze")
	for cell: CardCell in screen.codex_page.skills_screen.cells:
		assert_false(cell.accessibility_name.is_empty())
		assert_string_contains(cell.accessibility_name, cell.name_label.text)
	toni.button_down.emit()
	assert_ne(toni.picture.modulate, Color.WHITE, "a press dims the picture, so Reduce motion still shows the touch")
	toni.button_up.emit()
	assert_eq(toni.picture.modulate, Color.WHITE)


# ---- the drawer opens -----------------------------------------------------------------------

func test_tapping_a_kid_opens_a_drawer_with_the_card_the_name_the_words_and_the_palette() -> void:
	var screen: TitleScreen = await _open(CodexPage.CHARACTERS)
	var kids: KidsScreen = screen.codex_page.kids_screen
	var drawer: CardDrawer = screen.card_drawer
	assert_false(drawer.is_open)
	assert_false(drawer.visible)
	for i: int in kids.cells.size():
		var id: String = Session.AVATARS[i]
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		kids.cells[i].pressed.emit()
		await wait_frames(2)
		assert_true(drawer.is_open, "%s opens the drawer" % id)
		assert_eq(drawer.index, i)
		assert_eq(drawer.title_label.text, definition.display_name)
		assert_eq(drawer.picture.texture, CharactersPage.card_picture(StringName(id)), "the card, bare")
		assert_eq((drawer.find_child("About", true, false) as Label).text, definition.description)
		assert_not_null(drawer.find_child("Palette", true, false), "the palette")
		for swatch: Dictionary in CharactersPage.palette_of(definition):
			assert_not_null(drawer.find_child("Swatch_%s" % String(swatch["label"]), true, false))
		assert_null(drawer.find_child("Close", true, false), "the old sheet's Close button is not copied: the drawer has its own X")
		assert_eq(drawer.content.get_children().filter(func(node: Node) -> bool: return node.name == "Picture").size(), 0, "no second picture")
		drawer.close(false)


func test_tapping_a_skill_opens_a_drawer_with_its_card_figures_and_game() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var skills: SkillsScreen = screen.codex_page.skills_screen
	var drawer: CardDrawer = screen.card_drawer
	var weapons: Array[WeaponDef] = _rules().weapons
	var maxima: Dictionary = WeaponsPage.stat_maxima(weapons)
	for i: int in weapons.size():
		var weapon: WeaponDef = weapons[i]
		skills.cells[i].pressed.emit()
		await wait_frames(2)
		assert_true(drawer.is_open, "%s opens the drawer" % weapon.id)
		assert_eq(drawer.title_label.text, weapon.display_name)
		assert_eq(drawer.picture.texture, CardStyle.picture(weapon.id), "its card, bare")
		assert_eq((drawer.find_child("About", true, false) as Label).text, weapon.description)
		var figures: Control = drawer.find_child("Figures", true, false)
		var expected: Array[Dictionary] = WeaponsPage.figures_of(weapon)
		assert_eq(figures.get_child_count(), expected.size(), "%s: the same figures as the old sheet" % weapon.id)
		for k: int in expected.size():
			var row: Control = figures.get_child(k)
			assert_eq((row.find_child("Value", true, false) as Label).text, expected[k]["value"])
			assert_almost_eq((row.find_child("Meter", true, false) as ProgressBar).value, WeaponsPage.fraction_of(String(expected[k]["caption"]), weapon, maxima), 0.002)
		assert_eq((drawer.find_child("GameBlurb", true, false) as Label).text, WeaponsPage.street_games().blurb_of(weapon.street_game))
		var chips: Array[String] = []
		for label: Node in drawer.content.find_children("*", "Label", true, false):
			chips.append((label as Label).text)
		assert_true(chips.has(weapon.kind_label()), "%s: its kind is written, not only coloured" % weapon.id)
		drawer.close(false)


func test_the_drawer_content_is_the_old_sheets_own_sections() -> void:
	var weapon: WeaponDef = _rules().weapons[2]
	var sheet: MenuDialog = autofree(WeaponsPage.detail(weapon)) as MenuDialog
	var borrowed: Array[Control] = DrawerContent.of_weapon(weapon)
	var names: Array[String] = []
	for part: Control in borrowed:
		names.append(String(part.name))
		part.queue_free()
	for need: String in ["About"]:
		assert_true(names.has(need), need)
	assert_gte(sheet.body.get_child_count(), borrowed.size() + 1, "everything but the sheet's picture and its Close row")
	assert_eq(DrawerContent.of_kid(null).size(), 0, "no kid, no sections")
	var kid: Array[Control] = DrawerContent.of_kid(load("res://data/characters/toni.tres") as CharacterDef)
	assert_gte(kid.size(), 3, "about, palette, note")
	for part: Control in kid:
		part.queue_free()


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

func test_the_drawer_closes_by_scrim_handle_x_and_the_phones_back() -> void:
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
	drawer.close_button.pressed.emit()
	assert_false(drawer.is_open, "the X")
	await opened.call()
	await _back_key()
	assert_false(drawer.is_open, "the phone's Back")
	assert_eq(screen.codex_page.current, CodexPage.CHARACTERS, "and the screen under it stays")


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
	assert_eq(drawer.index, kids.cells.size() - 1, "before the first is the last")
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
	assert_gte(drawer.close_button.get_global_rect().size.x, UiMetrics.MIN_TARGET, "the X is a big target")
	assert_gte(drawer.close_button.get_global_rect().size.y, UiMetrics.MIN_TARGET)


func test_the_drawer_never_covers_the_bar_is_at_most_seventy_percent_tall_and_scrolls_when_taller() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _open(CodexPage.WEAPONS, height)
		var drawer: CardDrawer = screen.card_drawer
		var bar: Rect2 = screen.tab_bar.get_global_rect()
		var scrolled: int = 0
		for i: int in screen.codex_page.skills_screen.cells.size():
			screen.codex_page.skills_screen.cells[i].pressed.emit()
			await wait_frames(3)
			var sheet: Rect2 = drawer.sheet.get_global_rect()
			var room: Rect2 = drawer.get_global_rect()
			assert_lte(sheet.end.y, bar.position.y + UiTabBar.BAR_TOP + 1.0, "the sheet stops where the bar starts (%d)" % int(height))
			assert_lte(sheet.size.y, room.size.y * CardDrawer.MAX_HEIGHT_SHARE + 1.0, "at most 70 percent of the room")
			assert_almost_eq(sheet.size.x, 720.0, 1.0, "edge to edge")
			assert_gte(sheet.position.y, 0.0)
			assert_lte(room.end.y, bar.position.y + UiTabBar.BAR_TOP + 1.0, "the drawer itself ends above the bar")
			var content: float = drawer.content.get_combined_minimum_size().y
			if content > drawer.scroll.size.y + 1.0:
				scrolled += 1
				assert_gt(drawer.scroll.get_v_scroll_bar().max_value, drawer.scroll.size.y, "taller than its room, so it scrolls")
			else:
				assert_almost_eq(drawer.scroll.size.y, content, 1.5, "no more room than it needs")
			for label: Node in drawer.content.find_children("*", "Label", true, false):
				if (label as Label).is_visible_in_tree():
					assert_gte((label as Label).get_theme_font_size("font_size"), UiMetrics.MIN_TEXT, "no text under the floor")
			drawer.close(false)
		if height == SHORT_PHONE:
			assert_gt(scrolled, 0, "on the short phone some skill needs the scroll")


func test_the_drawer_content_scrolls_when_it_is_taller_than_the_room() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	var drawer: CardDrawer = screen.card_drawer
	screen.codex_page.skills_screen.cells[3].pressed.emit()
	await wait_frames(3)
	drawer.scroll.scroll_vertical = 100000
	await wait_frames(2)
	assert_gt(drawer.scroll.scroll_vertical, 0)
	drawer.step(1)
	assert_eq(drawer.scroll.scroll_vertical, 0, "the next card is read from its top")


# ---- the way back and the taps -----------------------------------------------------------------

func test_back_goes_drawer_then_screen_then_hub_then_home_then_the_quit_question() -> void:
	var screen: TitleScreen = await _open(CodexPage.WEAPONS)
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	await wait_frames(2)
	await _back_key()
	assert_false(screen.card_drawer.is_open, "first the drawer")
	assert_eq(screen.codex_page.current, CodexPage.WEAPONS)
	await _back_key()
	assert_eq(screen.current_page, &"codex", "then the screen goes back to the hub")
	assert_eq(screen.codex_page.current, CodexPage.HUB)
	await _back_key()
	assert_eq(screen.current_page, &"", "then home")
	assert_null(screen.dialog)
	await _back_key()
	assert_not_null(screen.dialog, "and then it asks to quit")


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
	screen.corner_buttons[&"codex"].pressed.emit()
	screen.codex_page.show_view(CodexPage.WEAPONS)
	screen.codex_page.skills_screen.back_button.pressed.emit()
	assert_eq(screen.codex_page.current, CodexPage.HUB, "the Back button")
	screen.codex_page.show_view(CodexPage.CHARACTERS)
	screen.codex_page.kids_screen.back_button.pressed.emit()
	assert_eq(screen.codex_page.current, CodexPage.HUB)


func test_three_taps_from_the_main_screen_to_a_cards_details_and_one_to_close() -> void:
	var screen: TitleScreen = _phone()
	await wait_frames(3)
	var taps: int = 0
	screen.corner_buttons[&"codex"].pressed.emit()
	taps += 1
	await wait_frames(2)
	screen.codex_page.hub.skills_card.pressed.emit()
	taps += 1
	await wait_frames(2)
	screen.codex_page.skills_screen.cells[0].pressed.emit()
	taps += 1
	await wait_frames(2)
	assert_true(screen.card_drawer.is_open)
	assert_eq(taps, 3, "Cards, the Skills card, a card")
	screen.card_drawer.close_button.pressed.emit()
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
