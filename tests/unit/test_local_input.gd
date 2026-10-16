extends GutTest


func test_combine_caps_length_at_one() -> void:
	assert_eq(LocalInput.combine(Vector2(1.0, 0.0), Vector2(1.0, 0.0)), Vector2(1.0, 0.0))
	assert_almost_eq(LocalInput.combine(Vector2(1.0, 0.0), Vector2(0.0, -1.0)).length(), 1.0, 0.0001)
	assert_eq(LocalInput.combine(Vector2(0.2, 0.0), Vector2.ZERO), Vector2(0.2, 0.0))


func test_other_team_stick_is_rotated_back() -> void:
	assert_eq(LocalInput.to_world(Vector2(0.5, -1.0), false), Vector2(0.5, -1.0))
	assert_eq(LocalInput.to_world(Vector2(0.5, -1.0), true), Vector2(-0.5, 1.0))


func test_keyboard_vector_is_zero_without_keys() -> void:
	assert_eq(LocalInput.keyboard_vector(), Vector2.ZERO)


func test_keyboard_buttons_is_zero_without_keys() -> void:
	assert_eq(LocalInput.keyboard_buttons(), 0)


func test_touch_button_hit_area_is_round() -> void:
	var size: Vector2 = Vector2(128.0, 128.0)
	assert_true(TouchButton.hit_test(Vector2(64.0, 64.0), size))
	assert_true(TouchButton.hit_test(Vector2(64.0, 5.0), size), "near the top edge")
	assert_false(TouchButton.hit_test(Vector2(2.0, 2.0), size), "corner is outside the circle")
	assert_false(TouchButton.hit_test(Vector2(300.0, 64.0), size))


func test_touch_button_cooldown_fraction() -> void:
	var button: TouchButton = autofree(TouchButton.new()) as TouchButton
	button.set_cooldown(4.0, 8.0)
	assert_eq(button.cooldown_fraction, 0.5)
	button.set_cooldown(0.0, 8.0)
	assert_eq(button.cooldown_fraction, 0.0)
	button.set_cooldown(9.0, 8.0)
	assert_eq(button.cooldown_fraction, 1.0)


func test_holding_one_skill_button_locks_every_other_one() -> void:
	var held: AimButton = autofree(AimButton.new()) as AimButton
	var other: AimButton = autofree(AimButton.new()) as AimButton
	var plain: TouchButton = autofree(TouchButton.new()) as TouchButton
	add_child(held)
	add_child(other)
	add_child(plain)
	assert_true(other.can_press())
	held._begin(Vector2.ZERO)
	assert_true(held.can_press(), "the held button itself stays usable")
	assert_false(other.can_press(), "no second weapon")
	assert_false(plain.can_press(), "no other button either")
	held._finish(false)
	assert_true(other.can_press(), "free again after the release")
	assert_true(plain.can_press())


func test_stick_from_offset_scales_and_caps() -> void:
	assert_eq(VirtualJoystick.stick_from_offset(Vector2(45.0, 0.0), 90.0, 0.1), Vector2(0.5, 0.0))
	assert_eq(VirtualJoystick.stick_from_offset(Vector2(0.0, -300.0), 90.0, 0.1), Vector2(0.0, -1.0))
	assert_eq(VirtualJoystick.stick_from_offset(Vector2(5.0, 0.0), 90.0, 0.1), Vector2.ZERO, "dead zone")
	assert_eq(VirtualJoystick.stick_from_offset(Vector2.ZERO, 90.0, 0.1), Vector2.ZERO)


func test_the_stick_moves_and_the_other_team_is_turned_around() -> void:
	var input: MatchInput = MatchInput.new()
	input.set_stick(Vector2(0.0, -1.0))
	var built: PlayerInput = input.build(false, func(_slot: int) -> Vector2: return Vector2.ZERO, 1)
	assert_almost_eq(built.move.y, -1.0, 0.001, "stick up: run up the screen")
	input.build(true, func(_slot: int) -> Vector2: return Vector2.ZERO, 2)
	built = input.build(true, func(_slot: int) -> Vector2: return Vector2.ZERO, 3)
	assert_almost_eq(built.move.y, 1.0, 0.001, "the other team's view is turned around")


func test_the_settings_menu_pauses_the_controls() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false(hud.menu.visible)
	assert_not_null(hud.settings_button.texture_normal, "painted gear")
	hud.toggle_menu()
	assert_true(hud.menu.visible)
	assert_false(hud.controls_active(), "no casting through the menu")
	hud.toggle_menu()
	assert_true(hud.controls_active())


func test_the_stick_sits_in_the_middle_with_a_skill_on_each_side() -> void:
	var screen_root: Control = autofree(Control.new()) as Control
	screen_root.size = Vector2(720.0, 1280.0)
	add_child(screen_root)
	var hud: MatchHud = MatchHud.new()
	screen_root.add_child(hud)
	hud.layout_controls()
	assert_eq(hud.aim_buttons.size(), 2, "weapon 1 and weapon 2")
	var screen: Vector2 = hud.get_rect().size
	var stick: Rect2 = hud.joystick.get_rect()
	var left: Rect2 = hud.weapon_buttons[0].get_rect()
	var right: Rect2 = hud.weapon_buttons[1].get_rect()
	assert_lt(stick.get_center().x, screen.x / 2.0, "the stick is on the left")
	assert_gt(stick.get_center().y, screen.y * 0.8, "at the bottom")
	assert_gte(stick.position.x, 0.0, "on screen")
	for button: Rect2 in [left, right]:
		assert_almost_eq(button.get_center().y, stick.get_center().y, 0.5, "one row")
		assert_false(button.intersects(stick), "the skills do not cover the stick")
		assert_gt(button.get_center().x, screen.x / 2.0, "the skills are on the right")
		assert_gte(button.position.x, 0.0, "on screen")
		assert_lte(button.end.x, screen.x, "on screen")
		assert_lt(button.end.y, screen.y, "on screen")
	assert_lt(left.get_center().x, right.get_center().x, "weapon 1 left of weapon 2")
	assert_false(left.intersects(right), "a gap between the two skills")
	assert_gte(left.size.x, 84.0, "a thumb-sized skill (44 pt)")
	assert_almost_eq(screen.x - right.end.x, MatchHud.WEAPON_MARGIN, 0.5, "the second against the right margin")
	var cancel: Rect2 = hud.cancel_zone.get_rect()
	assert_gt(cancel.get_center().x, screen.x / 2.0, "the cancel zone is above the skills")
	assert_lt(cancel.end.y, left.position.y, "clear of the skills")


func test_hiding_the_controls_lets_go_of_the_stick() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	watch_signals(hud.joystick)
	hud.joystick._begin(-2, hud.joystick.get_global_rect().get_center() + Vector2(0.0, -80.0))
	assert_ne(hud.joystick.value, Vector2.ZERO)
	hud.set_controls_active(false)
	assert_eq(hud.joystick.value, Vector2.ZERO, "no running on by itself under a menu")


func test_the_score_sits_at_the_top_right() -> void:
	var screen_root: Control = autofree(Control.new()) as Control
	screen_root.size = Vector2(720.0, 1280.0)
	add_child(screen_root)
	var hud: MatchHud = MatchHud.new()
	screen_root.add_child(hud)
	var score: Rect2 = hud.scoreboard.get_global_rect()
	assert_gt(score.position.x, 720.0 / 2.0, "right half")
	assert_almost_eq(score.end.x, 720.0 - MatchHud.MARGIN, 1.0, "against the right edge")
	assert_lt(score.position.y, 200.0, "at the top")


func test_the_menu_has_no_handedness_switch() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false("left_button" in hud.menu, "controls are fixed")
	assert_false("left_handed" in Settings)


func test_the_stick_follows_the_turned_camera() -> void:
	# camera behind-right (45 degrees): screen up heads forward-left in the world
	var up: Vector2 = LocalInput.to_world(Vector2(0.0, -1.0), false, 45.0)
	assert_almost_eq(up.x, -0.7071, 0.001)
	assert_almost_eq(up.y, -0.7071, 0.001)
	for stick: Vector2 in [Vector2(0.3, -0.8), Vector2(-1.0, 0.0), Vector2(0.6, 0.6)]:
		for flip: bool in [false, true]:
			var back: Vector2 = LocalInput.to_screen(LocalInput.to_world(stick, flip, 45.0), flip, 45.0)
			assert_almost_eq(back.x, stick.x, 0.0001)
			assert_almost_eq(back.y, stick.y, 0.0001)


func _stick() -> VirtualJoystick:
	var stick: VirtualJoystick = autofree(VirtualJoystick.new()) as VirtualJoystick
	stick.size = Vector2(290.0, 290.0)
	stick.rest_fraction = Vector2(0.5, 0.5)
	add_child(stick)
	return stick


func test_letting_go_past_the_inner_ring_keeps_running() -> void:
	var stick: VirtualJoystick = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(VirtualJoystick.MOUSE_POINTER, centre + Vector2(0.0, -50.0))
	assert_false(stick.outer_lit(), "inside the inner ring: a normal stick")
	var colors: Array[Color] = stick.ring_colors()
	assert_gt(colors[0].a, colors[1].a, "not lit: the outer ring is the dimmer one")
	assert_eq(Color(colors[1], 1.0), Color.WHITE, "and white")
	stick._update(centre + Vector2(VirtualJoystick.MAX_RADIUS + 10.0, 0.0))
	assert_true(stick.outer_lit(), "most of the knob past the inner ring lights the outer ring")
	assert_gt(stick.ring_colors()[1].a, stick.ring_colors()[0].a, "lit: the outer ring is the bright one")
	stick._end()
	assert_true(stick.auto_run)
	assert_eq(stick.value, Vector2(1.0, 0.0), "keeps running that way at full speed")
	assert_true(stick.outer_lit(), "the outer ring stays lit while auto-running")


func test_letting_go_inside_the_inner_ring_stops() -> void:
	var stick: VirtualJoystick = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(VirtualJoystick.MOUSE_POINTER, centre + Vector2(VirtualJoystick.MAX_RADIUS + 20.0, 0.0))
	stick._update(centre + Vector2(VirtualJoystick.MAX_RADIUS - 10.0, 0.0))
	assert_false(stick.outer_lit(), "back inside: not lit")
	stick._end()
	assert_false(stick.auto_run)
	assert_eq(stick.value, Vector2.ZERO, "idle")


func test_a_touch_grabs_the_knob_at_once_out_of_auto_run() -> void:
	var stick: VirtualJoystick = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(VirtualJoystick.MOUSE_POINTER, centre + Vector2(0.0, -120.0))
	stick._end()
	assert_true(stick.auto_run)
	# a new touch to the left: the very first event steers left, no unlock delay
	var press: InputEventMouseButton = InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	press.position = stick.get_global_rect().position + centre + Vector2(-60.0, 0.0)
	stick._input(press)
	assert_false(stick.auto_run, "grabbed")
	assert_almost_eq(stick.value.x, -60.0 / VirtualJoystick.MAX_RADIUS, 0.01, "follows the finger on the same touch")
	assert_almost_eq(stick.value.y, 0.0, 0.01)


func test_release_stops_auto_run() -> void:
	var stick: VirtualJoystick = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(VirtualJoystick.MOUSE_POINTER, centre + Vector2(0.0, 120.0))
	stick._end()
	assert_true(stick.auto_run)
	stick.release()
	assert_false(stick.auto_run)
	assert_eq(stick.value, Vector2.ZERO)


func _press(stick: VirtualJoystick, offset: Vector2) -> void:
	var press: InputEventMouseButton = InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	press.position = stick.get_global_rect().position + stick.rest_position() + offset
	stick._input(press)


func test_the_outer_ring_appears_only_once_the_knob_is_held() -> void:
	var stick: VirtualJoystick = _stick()
	assert_false(stick.outer_visible(), "at rest: the knob and the inner ring only")
	_press(stick, Vector2(VirtualJoystick.MAX_RADIUS + 15.0, 0.0))
	assert_eq(stick.value, Vector2.ZERO, "a touch in the (hidden) outer ring does not grab")
	assert_false(stick.outer_visible())
	_press(stick, Vector2(30.0, 0.0))
	assert_ne(stick.value, Vector2.ZERO, "a touch inside the inner ring grabs")
	assert_true(stick.outer_visible(), "the outer ring shows while held")


func test_while_auto_running_a_touch_in_the_outer_ring_grabs_too() -> void:
	var stick: VirtualJoystick = _stick()
	_press(stick, Vector2(0.0, -20.0))
	stick._update(stick.get_global_rect().position + stick.rest_position() + Vector2(0.0, -120.0))
	stick._end()
	assert_true(stick.auto_run)
	assert_true(stick.outer_visible(), "showing while auto-running")
	_press(stick, Vector2(VirtualJoystick.MAX_RADIUS + 15.0, 0.0))
	assert_false(stick.auto_run, "grabbed from the outer ring at once")
	assert_gt(stick.value.x, 0.9, "steering right on the same touch")
