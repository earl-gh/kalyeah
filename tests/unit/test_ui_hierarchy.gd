extends GutTest
## Boundary checks for readable colors, short screens, retained menu controls and room semantics.


func after_each() -> void:
	await wait_process_frames(2)


func _luminance(color: Color) -> float:
	var linear: Color = color.srgb_to_linear()
	return 0.2126 * linear.r + 0.7152 * linear.g + 0.0722 * linear.b


func _contrast(a: Color, b: Color) -> float:
	var first: float = _luminance(a)
	var second: float = _luminance(b)
	return (maxf(first, second) + 0.05) / (minf(first, second) + 0.05)


func test_active_action_colors_meet_small_text_contrast_in_every_state() -> void:
	for role: String in ["orange", "blue", "green", "red", "grey"]:
		var text: Color = KalyeahTheme.semantic_text_color(role)
		for state: StyleBoxFlat in [KalyeahTheme.painted_button(role), KalyeahTheme.painted_button(role, true), KalyeahTheme.painted_button(role, false, Color(1.06, 1.06, 1.06))]:
			assert_gte(_contrast(text, state.bg_color), 4.5, "%s active text stays readable" % role)
	for team: Color in [KalyeahTheme.TEAM_BLUE, KalyeahTheme.TEAM_RED]:
		assert_gte(_contrast(KalyeahTheme.TEXT, team), 4.5)
	for surface: Color in [KalyeahTheme.INK_DEEP, KalyeahTheme.SURFACE, KalyeahTheme.SURFACE_RAISED]:
		assert_gte(_contrast(KalyeahTheme.TEXT_MUTED, surface), 4.5)


func test_tall_dialog_scrolls_inside_the_viewport() -> void:
	var dialog: MenuDialog = MenuDialog.create("DETAILS")
	for i: int in 20:
		dialog.add_label("A readable line with room to wrap.")
	dialog.add_buttons([MenuDialog.make_button("DONE", "orange")])
	dialog.open(self)
	await wait_process_frames(5)
	var bounds: Rect2 = dialog.panel.get_global_rect()
	assert_gte(bounds.position.y, 0.0)
	assert_lte(bounds.end.y, get_viewport().get_visible_rect().end.y)
	var scroll: ScrollContainer = dialog.find_child("DialogScroll", true, false) as ScrollContainer
	assert_gt(scroll.get_v_scroll_bar().max_value, scroll.get_v_scroll_bar().page, "long content scrolls instead of hiding actions below the screen")
	dialog.close(false)


func test_match_menu_retains_controls_and_requires_confirmation_only_online() -> void:
	var menu: MatchMenu = autofree(MatchMenu.new()) as MatchMenu
	add_child(menu)
	var leave: Button = menu.leave_button
	var exits: Array[bool] = []
	menu.exit_pressed.connect(func() -> void: exits.append(true))
	for i: int in 3:
		menu.open()
		menu.close()
	await wait_process_frames(2)
	assert_true(is_instance_valid(leave))
	assert_eq(menu.leave_button, leave, "signal wiring survives repeated opens")
	menu.open()
	menu.set_practice(false, true)
	menu.leave_button.pressed.emit()
	assert_eq(exits.size(), 0)
	assert_true(menu._confirmation.is_open)
	assert_string_contains((menu._confirmation.body.get_child(0) as Label).text, "close for everyone")
	menu._confirmation.close(false)
	assert_true(menu.visible, "Cancel keeps the match menu open")
	menu.set_practice(true)
	menu.leave_button.pressed.emit()
	assert_eq(exits.size(), 1, "Practice can exit directly")
	assert_gte(menu.resume_button.custom_minimum_size.y, 88.0)


func test_room_code_and_read_only_format_are_mode_specific() -> void:
	var room: Dictionary = {"code": "ABC234XY", "host": 1, "team_size": 2, "first_to": 5, "members": [], "can_start_host": false}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, 2)
	assert_false(view.code_row.visible, "Hotspot never asks for or exposes a code")
	assert_eq(view.code_label.text, "")
	view.set_online(true)
	view.show_room(room, 2)
	assert_true(view.code_row.visible)
	assert_eq(view.code_label.text, "ABC234XY")
	assert_true(view.format_summary.visible)
	assert_eq(view.format_summary.text, "2v2 · First to 5")
	assert_false((view.size_buttons[1].get_parent() as Control).visible, "guests see a summary instead of disabled controls")
	view.show_room(room, 1)
	assert_false(view.format_summary.visible)
	assert_true((view.size_buttons[1].get_parent() as Control).visible)
	view.clear_room()
	assert_eq(view.code_label.text, "", "leaving removes stale room codes")


func test_back_can_leave_practice_but_online_confirmation_does_not_pause_the_pick_timer() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var screen: WeaponPickScreen = autofree(WeaponPickScreen.new()) as WeaponPickScreen
	add_child(screen)
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	var exits: Array[bool] = []
	screen.exit_requested.connect(func() -> void: exits.append(true))
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	screen.open(WeaponPick.new(ids, 1.0), rules.weapons, "", rng, [], &"", true)
	screen.back_button.pressed.emit()
	assert_eq(exits.size(), 1, "Practice has a direct Back action")
	screen.open(WeaponPick.new(ids, 1.0), rules.weapons, "", rng)
	screen.back_button.pressed.emit()
	assert_eq(exits.size(), 1, "Online requires confirmation")
	assert_true(screen._exit_dialog.is_open)
	screen.tick(2.0)
	assert_false(screen.visible)
	assert_false(screen._exit_dialog.is_open, "timeout closes the stale leave dialog")
	assert_eq(screen.pick.picks.size(), 2)


func test_a_modal_is_a_light_gray_frame_holding_the_header_around_a_dark_blue_gray_body() -> void:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var dialog: MenuDialog = MenuDialog.create("Hotspot")
	dialog.open(phone)
	await wait_process_frames(2)
	var frame: StyleBoxFlat = dialog.panel.get_theme_stylebox("panel") as StyleBoxFlat
	var body: StyleBoxFlat = dialog.inner.get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(frame.bg_color, KalyeahTheme.MODAL_FRAME, "the frame is light gray")
	assert_eq(body.bg_color, KalyeahTheme.MODAL_BODY, "the body is dark blue-gray")
	assert_gt(frame.bg_color.get_luminance(), 0.5, "light")
	assert_lt(body.bg_color.get_luminance(), 0.3, "dark")
	assert_true(dialog.panel.get_global_rect().encloses(dialog.title_label.get_global_rect()), "the header is inside the frame")
	assert_lte(dialog.title_label.get_global_rect().end.y, dialog.inner.get_global_rect().position.y + 1.0, "above the body, on the frame")
	assert_false(dialog.body.is_ancestor_of(dialog.title_label), "not inside the dark body")
	assert_lt(dialog.title_label.get_theme_color("font_color").get_luminance(), 0.1, "dark header text on the light frame")
	dialog.close(false)
