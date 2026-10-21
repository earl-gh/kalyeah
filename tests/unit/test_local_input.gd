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


func test_any_drag_past_the_dead_zone_is_a_full_speed_run() -> void:
	assert_eq(DPad.stick_from_offset(Vector2(45.0, 0.0), 90.0, 0.1), Vector2(1.0, 0.0), "half way is already full speed")
	assert_eq(DPad.stick_from_offset(Vector2(10.0, 0.0), 90.0, 0.1), Vector2(1.0, 0.0), "a slight drag runs too")
	assert_almost_eq(DPad.stick_from_offset(Vector2(20.0, 20.0), 90.0, 0.1).length(), 1.0, 0.0001, "diagonals too")
	assert_true(DPad.stick_from_offset(Vector2(50.0, 20.0), 90.0, 0.1).is_equal_approx(Vector2.RIGHT), "a pad has eight directions: this is still right")
	assert_true(DPad.stick_from_offset(Vector2(50.0, 40.0), 90.0, 0.1).is_equal_approx(Vector2(1.0, 1.0).normalized()), "and this one is down-right")
	assert_true(DPad.stick_from_offset(Vector2(0.0, -300.0), 90.0, 0.1).is_equal_approx(Vector2(0.0, -1.0)))
	assert_eq(DPad.stick_from_offset(Vector2(5.0, 0.0), 90.0, 0.1), Vector2.ZERO, "dead zone")
	assert_eq(DPad.stick_from_offset(Vector2.ZERO, 90.0, 0.1), Vector2.ZERO)


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
	var stick: Rect2 = hud.dpad.get_rect()
	var left: Rect2 = hud.weapon_buttons[0].get_rect()
	var right: Rect2 = hud.weapon_buttons[1].get_rect()
	assert_lt(stick.get_center().x, screen.x / 2.0, "the stick is on the left")
	assert_gt(stick.get_center().y, screen.y * 0.8, "at the bottom")
	assert_gte(stick.position.x, 0.0, "on screen")
	for button: Rect2 in [left, right]:
		assert_lt(absf(button.get_center().y - stick.get_center().y), MatchHud.ROW_HEIGHT * 0.5, "the buttons sit in the same band as the pad")
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
	watch_signals(hud.dpad)
	hud.dpad._begin(-2, hud.dpad.get_global_rect().get_center() + Vector2(0.0, -80.0))
	assert_ne(hud.dpad.value, Vector2.ZERO)
	hud.set_controls_active(false)
	assert_eq(hud.dpad.value, Vector2.ZERO, "no running on by itself under a menu")


func test_hiding_the_controls_cancels_an_active_aim() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	var button: AimButton = hud.aim_buttons[0]
	watch_signals(button)
	button._begin(button.get_global_rect().get_center())
	hud.set_controls_active(false)
	assert_signal_emitted_with_parameters(button, "aim_released", [Vector2.ZERO, true])
	assert_false(button.aiming)
	assert_null(TouchButton.holder)


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


func test_the_match_menu_has_no_hand_buttons_and_right_handed_is_the_default() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false("left_button" in hud.menu, "the hand is chosen in Settings, not with buttons in the match menu")
	var fresh: Node = autofree(preload("res://scripts/autoload/settings.gd").new()) as Node
	assert_false(fresh.get("left_handed") as bool, "right-handed until the player says otherwise")


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


func _stick() -> DPad:
	var stick: DPad = autofree(DPad.new()) as DPad
	stick.size = Vector2(290.0, 290.0)
	stick.rest_fraction = Vector2(0.5, 0.5)
	add_child(stick)
	return stick


func _press(stick: DPad, offset: Vector2) -> void:
	var press: InputEventMouseButton = InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	press.position = stick.get_global_rect().position + stick.rest_position() + offset
	stick._input(press)


func test_letting_go_always_stops_even_past_the_disc() -> void:
	var stick: DPad = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(DPad.MOUSE_POINTER, centre + Vector2(0.0, -50.0))
	assert_true(stick.value.is_equal_approx(Vector2.UP), "running up while held")
	stick._update(centre + Vector2(DPad.MAX_RADIUS + 60.0, 0.0))
	assert_eq(stick.value, Vector2(1.0, 0.0), "sliding far past the disc keeps the direction")
	stick._end()
	assert_eq(stick.value, Vector2.ZERO, "and letting go there stops: there is no auto-run on a D-pad")


func test_a_touch_outside_the_disc_does_not_grab_the_pad() -> void:
	var stick: DPad = _stick()
	_press(stick, Vector2(DPad.MAX_RADIUS + 15.0, 0.0))
	assert_eq(stick.value, Vector2.ZERO, "only the disc grabs")
	_press(stick, Vector2(30.0, 0.0))
	assert_ne(stick.value, Vector2.ZERO, "a touch on the disc does")


func test_release_stops_the_pad() -> void:
	var stick: DPad = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._begin(DPad.MOUSE_POINTER, centre + Vector2(0.0, 80.0))
	assert_ne(stick.value, Vector2.ZERO)
	stick.release()
	assert_eq(stick.value, Vector2.ZERO)


func test_the_pad_says_when_the_thumb_enters_a_new_direction_and_only_then() -> void:
	var pad: DPad = autofree(DPad.new()) as DPad
	add_child(pad)
	pad.size = Vector2(DPad.MAX_RADIUS * 2.0, DPad.MAX_RADIUS * 2.0)
	var seen: Array[Vector2] = []
	pad.direction_changed.connect(func(direction: Vector2) -> void: seen.append(direction))
	pad._begin(-2, pad.get_global_rect().get_center() + Vector2(40.0, 0.0))
	pad._update(pad.get_global_rect().get_center() + Vector2(50.0, 2.0))
	assert_eq(seen.size(), 1, "right, still right: one tick")
	pad._update(pad.get_global_rect().get_center() + Vector2(0.0, -50.0))
	assert_eq(seen.size(), 2, "up: a second tick")
	pad._update(pad.get_global_rect().get_center() + Vector2(1.0, 0.0))
	assert_eq(seen.size(), 3, "back to the middle is a change too (the match only buzzes for a direction)")
	assert_eq(seen[2], Vector2.ZERO)


func test_the_pad_presses_one_arm_for_a_direction_and_two_for_a_diagonal() -> void:
	# arms in the order right, down, left, up
	assert_eq(DPad.pressed_arms(Vector2.RIGHT), [true, false, false, false] as Array[bool])
	assert_eq(DPad.pressed_arms(Vector2.DOWN), [false, true, false, false] as Array[bool])
	assert_eq(DPad.pressed_arms(Vector2.LEFT), [false, false, true, false] as Array[bool])
	assert_eq(DPad.pressed_arms(Vector2.UP), [false, false, false, true] as Array[bool])
	assert_eq(DPad.pressed_arms(Vector2(1.0, -1.0).normalized()), [true, false, false, true] as Array[bool], "up-right presses two arms")
	assert_eq(DPad.pressed_arms(Vector2(-1.0, 1.0).normalized()), [false, true, true, false] as Array[bool], "down-left too")
	assert_eq(DPad.pressed_arms(Vector2.ZERO), [false, false, false, false] as Array[bool])
