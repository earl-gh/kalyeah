extends GutTest
## Tap to move and cards to cast, as the PlayerInput the sim receives (docs/GDD.md "Controls: tap to move, cards to cast"):
## a street tap is a move target, a held drag follows the finger, a card dragged onto the street casts at that point, a quick tap on
## the card auto-aims (no point), a drop back on the hand or a long press cancels, and a finger on a card never blocks another finger
## on the street.

const NOW: int = 10000
const POINT: Vector2 = Vector2(1.5, -4.0)


func _make() -> MatchInput:
	var input: MatchInput = MatchInput.new()
	input.tap_time = 0.3
	return input


func _tick(input: MatchInput, tick: int = 1) -> PlayerInput:
	return input.build(tick)


# ---- moving ---------------------------------------------------------------------------

func test_a_street_tap_sends_a_move_target_once() -> void:
	var input: MatchInput = _make()
	input.follow_hold = false
	input.move_press(POINT)
	input.move_release()
	var built: PlayerInput = _tick(input)
	assert_true(built.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_eq(built.move_target, POINT, "the street point under the finger")
	assert_eq(built.move, Vector2.ZERO, "no stick")
	assert_false(_tick(input, 2).is_pressed(PlayerInput.BTN_MOVE_TARGET), "sent once; the kid walks on by itself")


func test_a_held_finger_drags_the_target_every_tick_and_letting_go_keeps_the_last_point() -> void:
	var input: MatchInput = _make()
	input.move_press(POINT)
	assert_eq(_tick(input, 1).move_target, POINT)
	input.move_drag(Vector2(2.0, -5.0))
	var built: PlayerInput = _tick(input, 2)
	assert_true(built.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_eq(built.move_target, Vector2(2.0, -5.0), "the target follows the finger")
	assert_true(_tick(input, 3).is_pressed(PlayerInput.BTN_MOVE_TARGET), "held still: still sent (the sim ignores the same point)")
	input.move_release()
	assert_false(_tick(input, 4).is_pressed(PlayerInput.BTN_MOVE_TARGET), "let go: the kid finishes its walk to the last point")


func test_without_follow_hold_a_drag_does_not_move_the_target() -> void:
	var input: MatchInput = _make()
	input.follow_hold = false
	input.move_press(POINT)
	_tick(input, 1)
	input.move_drag(Vector2(9.0, 9.0))
	assert_false(_tick(input, 2).is_pressed(PlayerInput.BTN_MOVE_TARGET))


# ---- cards ----------------------------------------------------------------------------

func test_a_quick_tap_on_a_card_is_a_held_tick_then_a_plain_release() -> void:
	var input: MatchInput = _make()
	assert_true(input.card_press(2, NOW))
	var held: PlayerInput = _tick(input, 1)
	assert_true(held.is_pressed(PlayerInput.BTN_CARD_3), "slot 3 held")
	assert_false(held.is_pressed(PlayerInput.BTN_AIM_POINT), "no point while it is on its card")
	assert_eq(input.card_release(NOW + 200), MatchInput.Release.TAP)
	var release: PlayerInput = _tick(input, 2)
	assert_eq(release.buttons & (PlayerInput.CARD_BITS[2] | PlayerInput.BTN_AIM_POINT | PlayerInput.BTN_AIM_CANCEL), 0,
		"bit off, no point, no cancel: the sim auto-aims")


func test_a_tap_inside_one_tick_still_shows_the_sim_one_held_tick() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	input.card_release(NOW + 20)
	assert_true(_tick(input, 1).is_pressed(PlayerInput.BTN_CARD_1), "held once")
	assert_false(_tick(input, 2).is_pressed(PlayerInput.BTN_CARD_1), "then released")
	assert_eq(_tick(input, 3).buttons, 0, "and nothing after")


func test_a_card_dragged_onto_the_street_sends_the_point_each_tick_and_casts_there() -> void:
	var input: MatchInput = _make()
	input.card_press(1, NOW)
	input.card_drag(false, Vector2(0.5, -2.0))
	var held: PlayerInput = _tick(input, 1)
	assert_true(held.is_pressed(PlayerInput.BTN_CARD_2))
	assert_true(held.is_pressed(PlayerInput.BTN_AIM_POINT))
	assert_eq(held.aim_point, Vector2(0.5, -2.0))
	input.card_drag(false, POINT)
	assert_eq(_tick(input, 2).aim_point, POINT, "the point follows the finger")
	assert_eq(input.card_release(NOW + 900), MatchInput.Release.CAST, "held long but over the street: still a cast")
	var release: PlayerInput = _tick(input, 3)
	assert_false(release.is_pressed(PlayerInput.BTN_CARD_2), "the bit goes off: the release")
	assert_true(release.is_pressed(PlayerInput.BTN_AIM_POINT))
	assert_eq(release.aim_point, POINT, "cast at the release point")
	assert_false(release.is_pressed(PlayerInput.BTN_AIM_CANCEL))


func test_dropping_the_card_back_on_the_hand_cancels() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	input.card_drag(false, POINT)
	_tick(input, 1)
	input.card_drag(false, null)  # back over the hand, beside its card
	assert_eq(input.release_kind(NOW + 100), MatchInput.Release.CANCEL)
	input.card_release(NOW + 100)
	var release: PlayerInput = _tick(input, 2)
	assert_true(release.is_pressed(PlayerInput.BTN_AIM_CANCEL), "nothing cast, nothing spent")
	assert_false(release.is_pressed(PlayerInput.BTN_AIM_POINT))


func test_dragging_back_onto_its_own_card_after_leaving_it_is_a_cancel_not_a_tap() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	input.card_drag(false, null)
	input.card_drag(true, null)
	assert_eq(input.card_release(NOW + 50), MatchInput.Release.CANCEL)


func test_a_long_press_on_the_card_without_dragging_does_nothing() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	_tick(input, 1)
	assert_eq(input.card_release(NOW + 301), MatchInput.Release.CANCEL, "past card_tap_time: a look, not a cast")
	assert_true(_tick(input, 2).is_pressed(PlayerInput.BTN_AIM_CANCEL))


func test_one_card_at_a_time() -> void:
	var input: MatchInput = _make()
	assert_true(input.card_press(0, NOW))
	assert_false(input.card_press(3, NOW), "a second card cannot be picked up while one is held")
	var built: PlayerInput = _tick(input, 1)
	assert_eq(built.buttons & PlayerInput.BTN_CARD_4, 0)


func test_a_cancelled_hold_sends_cancel() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	input.card_drag(false, POINT)
	_tick(input, 1)
	input.card_cancel()
	var release: PlayerInput = _tick(input, 2)
	assert_true(release.is_pressed(PlayerInput.BTN_AIM_CANCEL))
	assert_false(release.is_pressed(PlayerInput.BTN_CARD_1))


func test_a_finger_on_a_card_and_a_finger_on_the_street_go_out_in_the_same_tick() -> void:
	var input: MatchInput = _make()
	input.card_press(1, NOW)
	input.card_drag(false, Vector2(3.0, -3.0))
	input.move_press(POINT)
	var built: PlayerInput = _tick(input, 1)
	assert_true(built.is_pressed(PlayerInput.BTN_CARD_2), "the card is held")
	assert_true(built.is_pressed(PlayerInput.BTN_AIM_POINT))
	assert_true(built.is_pressed(PlayerInput.BTN_MOVE_TARGET), "and the kid walks")
	assert_eq(built.move_target, POINT)
	assert_eq(built.aim_point, Vector2(3.0, -3.0))


func test_clear_forgets_everything() -> void:
	var input: MatchInput = _make()
	input.card_press(0, NOW)
	input.move_press(POINT)
	input.clear()
	assert_eq(_tick(input, 1).buttons, 0)
	assert_false(input.card_held())


# ---- the hand (touches on the cards) ---------------------------------------------------

func _hand() -> CardHand:
	var hand: CardHand = CardHand.new()
	hand.size = Vector2(720.0, MatchHud.CONSOLE_HEIGHT)
	hand.position = Vector2(0.0, 1280.0 - MatchHud.CONSOLE_HEIGHT)
	add_child_autofree(hand)
	var def: WeaponDef = WeaponDef.new()
	def.id = &"test"
	def.short_name = "TEST"
	def.elixir_cost = 3
	for slot: int in CardHand.SLOTS:
		hand.defs[slot] = def
		hand.pickable[slot] = true
	hand.active = true
	return hand


func _touch(index: int, at: Vector2, pressed: bool) -> InputEventScreenTouch:
	var event: InputEventScreenTouch = InputEventScreenTouch.new()
	event.index = index
	event.position = at
	event.pressed = pressed
	return event


func _drag(index: int, at: Vector2) -> InputEventScreenDrag:
	var event: InputEventScreenDrag = InputEventScreenDrag.new()
	event.index = index
	event.position = at
	return event


func _card_centre(hand: CardHand, slot: int) -> Vector2:
	return hand.position + hand.card_rects[slot].get_center()


func test_pressing_a_ready_card_picks_it_up_and_a_release_over_the_street_says_so() -> void:
	var hand: CardHand = _hand()
	watch_signals(hand)
	hand._input(_touch(0, _card_centre(hand, 1), true))
	assert_signal_emitted_with_parameters(hand, "card_pressed", [1])
	assert_eq(hand.held_slot, 1)
	hand._input(_drag(0, Vector2(360.0, 500.0)))
	assert_false(hand.over_hand, "over the street")
	assert_false(hand.on_card)
	hand._input(_touch(0, Vector2(360.0, 500.0), false))
	assert_signal_emitted_with_parameters(hand, "card_released", [1, Vector2(360.0, 500.0), false, false])
	assert_eq(hand.held_slot, -1)


func test_a_card_that_is_not_ready_cannot_be_picked_up_and_shakes() -> void:
	var hand: CardHand = _hand()
	hand.pickable[2] = false
	watch_signals(hand)
	hand._input(_touch(0, _card_centre(hand, 2), true))
	assert_signal_not_emitted(hand, "card_pressed")
	assert_signal_emitted_with_parameters(hand, "card_denied", [2])
	assert_true(hand.denied(2), "it flashes")
	assert_eq(hand.held_slot, -1)


func test_reduce_motion_keeps_a_denied_card_still() -> void:
	var motion: bool = Settings.reduced_motion
	var hand: CardHand = _hand()
	hand.pickable[0] = false
	hand.deny(0)
	hand._deny[0] = CardHand.DENY_TIME * 0.9
	Settings.set_reduced_motion(false)
	assert_ne(hand.deny_offset(0), 0.0, "it shakes")
	Settings.set_reduced_motion(true)
	assert_eq(hand.deny_offset(0), 0.0, "Reduce motion: only the flash")
	Settings.set_reduced_motion(motion)


func test_a_second_finger_cannot_pick_up_a_second_card() -> void:
	var hand: CardHand = _hand()
	hand._input(_touch(0, _card_centre(hand, 0), true))
	watch_signals(hand)
	hand._input(_touch(1, _card_centre(hand, 3), true))
	assert_signal_not_emitted(hand, "card_pressed")
	assert_eq(hand.held_slot, 0, "still the first card")
	hand._input(_touch(1, _card_centre(hand, 3), false))
	assert_eq(hand.held_slot, 0, "the other finger letting go changes nothing")


func test_a_system_cancelled_touch_drops_the_card() -> void:
	var hand: CardHand = _hand()
	hand._input(_touch(0, _card_centre(hand, 0), true))
	watch_signals(hand)
	var cancel: InputEventScreenTouch = _touch(0, Vector2(360.0, 400.0), false)
	cancel.canceled = true
	hand._input(cancel)
	assert_signal_emitted_with_parameters(hand, "card_cancelled", [0])
	assert_signal_not_emitted(hand, "card_released")


func test_the_mouse_events_a_touch_makes_are_ignored_by_the_hand() -> void:
	var hand: CardHand = _hand()
	hand._input(TouchHelper.click(_card_centre(hand, 2), true))
	assert_eq(hand.held_slot, -1, "the touch itself picks the card up, once")


func test_an_inactive_hand_takes_no_card() -> void:
	var hand: CardHand = _hand()
	hand.active = false
	hand._input(_touch(0, _card_centre(hand, 0), true))
	assert_eq(hand.held_slot, -1)


# ---- the street (move taps) -------------------------------------------------------------

func test_the_street_follows_its_own_finger_while_another_holds_a_card() -> void:
	var street: StreetTouch = StreetTouch.new()
	add_child_autofree(street)
	street.active = true
	var pressed: Array[Vector2] = []
	var dragged: Array[Vector2] = []
	street.move_pressed.connect(func(at: Vector2) -> void: pressed.append(at))
	street.move_dragged.connect(func(at: Vector2) -> void: dragged.append(at))
	# finger 0 is on a card (the hand took it, so the street never saw its press); finger 1 taps the street
	assert_false(street.handle(_drag(0, Vector2(100.0, 100.0))), "the card's finger is not the street's")
	assert_true(street.handle(_touch(1, Vector2(200.0, 300.0), true)))
	assert_true(street.handle(_drag(1, Vector2(210.0, 320.0))))
	assert_eq(pressed, [Vector2(200.0, 300.0)] as Array[Vector2])
	assert_eq(dragged, [Vector2(210.0, 320.0)] as Array[Vector2])
	watch_signals(street)
	street.handle(_touch(0, Vector2(100.0, 100.0), false))
	assert_signal_not_emitted(street, "move_released", "the card's finger letting go is not the street's")
	street.handle(_touch(1, Vector2(210.0, 320.0), false))
	assert_signal_emitted(street, "move_released")


func test_the_mouse_events_a_touch_makes_do_not_order_a_second_move() -> void:
	var street: StreetTouch = StreetTouch.new()
	add_child_autofree(street)
	street.active = true
	watch_signals(street)
	assert_false(street.handle(TouchHelper.click(Vector2(50.0, 60.0), true)))
	assert_false(street.handle(TouchHelper.motion(Vector2(55.0, 60.0))))
	assert_signal_not_emitted(street, "move_pressed")
	assert_true(street.handle(TouchHelper.touch(0, Vector2(50.0, 60.0), true)))
	assert_signal_emitted_with_parameters(street, "move_pressed", [Vector2(50.0, 60.0)])


func test_an_inactive_street_ignores_taps() -> void:
	var street: StreetTouch = StreetTouch.new()
	add_child_autofree(street)
	watch_signals(street)
	assert_false(street.handle(_touch(0, Vector2(10.0, 10.0), true)))
	assert_signal_not_emitted(street, "move_pressed")
