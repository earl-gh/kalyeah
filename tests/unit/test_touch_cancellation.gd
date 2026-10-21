extends GutTest


func after_each() -> void:
	# The holder is shared across buttons, so a failed assertion must not leak it
	# into the next test.
	TouchButton.holder = null


func _touch(pointer: int, position: Vector2, pressed: bool, cancelled: bool = false) -> InputEventScreenTouch:
	var event: InputEventScreenTouch = InputEventScreenTouch.new()
	event.index = pointer
	event.position = position
	event.pressed = pressed
	event.canceled = cancelled
	return event


func _drag(pointer: int, position: Vector2) -> InputEventScreenDrag:
	var event: InputEventScreenDrag = InputEventScreenDrag.new()
	event.index = pointer
	event.position = position
	return event


func _aim_button() -> AimButton:
	var button: AimButton = autofree(AimButton.new()) as AimButton
	button.size = Vector2(100.0, 100.0)
	add_child(button)
	return button


func _stick() -> DPad:
	var stick: DPad = autofree(DPad.new()) as DPad
	stick.size = Vector2(290.0, 290.0)
	stick.rest_fraction = Vector2(0.5, 0.5)
	add_child(stick)
	return stick


func test_aim_normal_release_still_casts() -> void:
	var button: AimButton = _aim_button()
	watch_signals(button)
	button._input(_touch(3, Vector2(50.0, 50.0), true))
	button._input(_drag(3, Vector2(50.0, 0.0)))
	button._input(_touch(3, Vector2(50.0, 0.0), false))
	assert_signal_emitted_with_parameters(button, "aim_released", [Vector2(0.0, -50.0 / AimButton.DRAG_RADIUS), false])
	assert_false(button.aiming)
	assert_eq(button._pointer, TouchButton.NO_POINTER)
	assert_null(TouchButton.holder)


func test_aim_cancelled_touch_cancels_and_clears_ownership() -> void:
	var button: AimButton = _aim_button()
	watch_signals(button)
	button._input(_touch(4, Vector2(50.0, 50.0), true))
	button._input(_drag(4, Vector2(100.0, 50.0)))
	button._input(_touch(4, Vector2.ZERO, false, true))
	assert_signal_emitted_with_parameters(button, "aim_released", [Vector2(50.0 / AimButton.DRAG_RADIUS, 0.0), true])
	assert_false(button.aiming)
	assert_eq(button.aim, Vector2.ZERO)
	assert_eq(button._pointer, TouchButton.NO_POINTER)
	assert_null(TouchButton.holder)


func test_aim_ignores_cancellation_from_a_non_owner_pointer() -> void:
	var button: AimButton = _aim_button()
	watch_signals(button)
	button._input(_touch(5, Vector2(50.0, 50.0), true))
	button._input(_touch(6, Vector2.ZERO, false, true))
	assert_signal_not_emitted(button, "aim_released")
	assert_true(button.aiming)
	assert_eq(button._pointer, 5)
	assert_eq(TouchButton.holder, button)
	button._input(_touch(5, Vector2(50.0, 50.0), false, true))


func test_locking_while_aiming_cancels_and_unlocking_allows_a_fresh_press() -> void:
	var button: AimButton = _aim_button()
	watch_signals(button)
	button._input(_touch(7, Vector2(50.0, 50.0), true))
	button.set_locked(true)
	assert_signal_emitted_with_parameters(button, "aim_released", [Vector2.ZERO, true])
	assert_false(button.aiming)
	assert_eq(button._pointer, TouchButton.NO_POINTER)
	assert_null(TouchButton.holder)
	button._input(_touch(8, Vector2(50.0, 50.0), true))
	assert_false(button.aiming, "locked buttons ignore new touches")
	button.set_locked(false)
	button._input(_touch(8, Vector2(50.0, 50.0), true))
	assert_true(button.aiming, "unlock requires and accepts a fresh press")
	button._input(_touch(8, Vector2(50.0, 50.0), false, true))


func test_explicit_cancel_ends_an_active_aim_without_casting() -> void:
	var button: AimButton = _aim_button()
	watch_signals(button)
	button._input(_touch(12, Vector2(50.0, 50.0), true))
	button.cancel_aim()
	assert_signal_emitted_with_parameters(button, "aim_released", [Vector2.ZERO, true])
	assert_false(button.aiming)
	assert_eq(button._pointer, TouchButton.NO_POINTER)
	assert_null(TouchButton.holder)


func test_dpad_normal_release_stops_even_past_the_disc() -> void:
	var stick: DPad = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._input(_touch(9, centre, true))
	stick._input(_drag(9, centre + Vector2(DPad.MAX_RADIUS + 20.0, 0.0)))
	assert_eq(stick.value, Vector2.RIGHT, "still running right while held")
	stick._input(_touch(9, centre, false))
	assert_eq(stick.value, Vector2.ZERO, "a D-pad has no auto-run: letting go stops")
	assert_eq(stick._pointer, DPad.NO_POINTER)


func test_dpad_cancelled_touch_stops() -> void:
	var stick: DPad = _stick()
	var centre: Vector2 = stick.rest_position()
	watch_signals(stick)
	stick._input(_touch(10, centre, true))
	stick._input(_drag(10, centre + Vector2(DPad.MAX_RADIUS + 20.0, 0.0)))
	stick._input(_touch(10, Vector2.ZERO, false, true))
	assert_eq(stick.value, Vector2.ZERO)
	assert_eq(stick._pointer, DPad.NO_POINTER)
	assert_signal_emitted_with_parameters(stick, "changed", [Vector2.ZERO])


func test_dpad_ignores_cancellation_from_a_non_owner_pointer() -> void:
	var stick: DPad = _stick()
	var centre: Vector2 = stick.rest_position()
	stick._input(_touch(11, centre, true))
	stick._input(_drag(11, centre + Vector2(50.0, 0.0)))
	stick._input(_touch(12, Vector2.ZERO, false, true))
	assert_eq(stick._pointer, 11)
	assert_eq(stick.value, Vector2.RIGHT)
	stick._input(_touch(11, Vector2.ZERO, false, true))
	assert_eq(stick.value, Vector2.ZERO)
