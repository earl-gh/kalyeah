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
	assert_string_contains((menu._confirmation.body.get_child(0) as Label).text, "closes for everyone")
	menu._confirmation.close(false)
	assert_true(menu.visible, "Cancel keeps the match menu open")
	menu.set_practice(true)
	menu.leave_button.pressed.emit()
	assert_eq(exits.size(), 1, "Practice can exit directly")
	assert_gte(menu._dialog.close_button.custom_minimum_size.y, UiMetrics.MIN_TARGET)
	menu._dialog.close_button.pressed.emit()
	assert_false(menu.visible, "X returns to gameplay")


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


func test_a_modal_is_a_slate_frame_holding_the_header_around_a_light_blue_white_body() -> void:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var dialog: MenuDialog = MenuDialog.create("Hotspot")
	dialog.open(phone)
	await wait_process_frames(2)
	var frame: StyleBoxFlat = dialog.panel.get_theme_stylebox("panel") as StyleBoxFlat
	var body: StyleBoxFlat = dialog.inner.get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(frame.bg_color, KalyeahTheme.MODAL_FRAME, "the frame is slate blue-gray")
	assert_eq(body.bg_color, KalyeahTheme.MODAL_BODY, "the body is light blue-white")
	assert_lt(frame.bg_color.get_luminance(), 0.5, "a darker slate")
	assert_gt(body.bg_color.get_luminance(), 0.7, "light")
	assert_true(dialog.panel.get_global_rect().encloses(dialog.title_label.get_global_rect()), "the header is inside the frame")
	assert_lte(dialog.title_label.get_global_rect().end.y, dialog.inner.get_global_rect().position.y + 1.0, "above the body, on the frame")
	assert_false(dialog.body.is_ancestor_of(dialog.title_label), "not inside the light body")
	assert_gt(dialog.title_label.get_theme_color("font_color").get_luminance(), 0.9, "white header text on the slate frame (owner)")
	assert_lt(dialog.title_label.get_theme_color("font_outline_color").get_luminance(), 0.2, "with a dark outline")
	dialog.close(false)


func test_a_modal_has_a_red_close_button_top_right_and_closes_on_a_tap_outside() -> void:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var dialog: MenuDialog = MenuDialog.create("Hotspot")
	dialog.open(phone)
	await wait_process_frames(2)
	var close_button: Button = dialog.find_child("CloseButton", true, false) as Button
	assert_not_null(close_button, "a close button")
	var frame: Rect2 = dialog.panel.get_global_rect()
	var at: Rect2 = close_button.get_global_rect()
	assert_true(frame.encloses(at), "inside the frame")
	assert_gt(at.get_center().x, frame.get_center().x, "at the right")
	assert_lt(at.get_center().y, dialog.inner.get_global_rect().position.y, "in the header, above the body")
	assert_gte(minf(at.size.x, at.size.y), UiMetrics.MIN_TARGET, "thumb-sized")
	var red: StyleBox = UiSkin.button(KalyeahTheme.skin_variant("red"))
	assert_eq((close_button.get_theme_stylebox("normal") as StyleBoxTexture).texture, (red as StyleBoxTexture).texture, "red")
	assert_false(dialog.body.is_ancestor_of(close_button), "on the frame, not in the dark body")
	var inside: InputEventMouseButton = InputEventMouseButton.new()
	inside.button_index = MOUSE_BUTTON_LEFT
	inside.pressed = true
	dialog.panel.gui_input.emit(inside)
	assert_true(dialog.is_open, "a tap on the card does not close it")
	var outside: InputEventMouseButton = InputEventMouseButton.new()
	outside.button_index = MOUSE_BUTTON_LEFT
	outside.pressed = true
	dialog.get_child(0).gui_input.emit(outside)
	assert_false(dialog.is_open, "a tap outside closes it")
	var again: MenuDialog = MenuDialog.create("Hotspot")
	again.open(phone)
	await wait_process_frames(2)
	(again.find_child("CloseButton", true, false) as Button).pressed.emit()
	assert_false(again.is_open, "the red button closes it")
