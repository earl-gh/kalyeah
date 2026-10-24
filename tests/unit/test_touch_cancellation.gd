extends GutTest
## A touch the system takes back (notification shade, an OS gesture, a call) never casts the held card, another finger's cancel never
## drops it, and switching the controls off drops it without a cast.


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


func _hand() -> CardHand:
	var hand: CardHand = CardHand.new()
	hand.size = Vector2(720.0, MatchHud.CONSOLE_HEIGHT)
	add_child_autofree(hand)
	var def: WeaponDef = WeaponDef.new()
	def.id = &"test"
	def.short_name = "TEST"
	for slot: int in CardHand.SLOTS:
		hand.defs[slot] = def
		hand.pickable[slot] = true
	hand.active = true
	return hand


func _on_card(hand: CardHand) -> Vector2:
	return hand.card_rects[0].get_center()


func test_a_normal_release_over_the_street_casts() -> void:
	var hand: CardHand = _hand()
	watch_signals(hand)
	hand._input(_touch(3, _on_card(hand), true))
	hand._input(_drag(3, Vector2(300.0, -400.0)))
	hand._input(_touch(3, Vector2(300.0, -400.0), false))
	assert_signal_emitted_with_parameters(hand, "card_released", [0, Vector2(300.0, -400.0), false, false])
	assert_signal_not_emitted(hand, "card_cancelled")
	assert_eq(hand._pointer, CardHand.NO_POINTER)


func test_a_cancelled_touch_drops_the_card_and_frees_the_pointer() -> void:
	var hand: CardHand = _hand()
	watch_signals(hand)
	hand._input(_touch(4, _on_card(hand), true))
	hand._input(_drag(4, Vector2(300.0, -400.0)))
	hand._input(_touch(4, Vector2.ZERO, false, true))
	assert_signal_emitted_with_parameters(hand, "card_cancelled", [0])
	assert_signal_not_emitted(hand, "card_released")
	assert_eq(hand._pointer, CardHand.NO_POINTER)
	assert_false(hand.holding())


func test_the_browser_cancel_counts_too() -> void:
	var hand: CardHand = _hand()
	watch_signals(hand)
	hand._input(_touch(5, _on_card(hand), true))
	WebTouchCancel.note()
	hand._input(_touch(5, Vector2(300.0, -400.0), false))
	assert_signal_emitted(hand, "card_cancelled", "the web turns touchcancel into a plain release")
	assert_signal_not_emitted(hand, "card_released")
	WebTouchCancel.clear()


func test_another_fingers_cancel_does_not_drop_the_card() -> void:
	var hand: CardHand = _hand()
	watch_signals(hand)
	hand._input(_touch(5, _on_card(hand), true))
	hand._input(_touch(6, Vector2.ZERO, false, true))
	assert_signal_not_emitted(hand, "card_cancelled")
	assert_eq(hand._pointer, 5)
	assert_true(hand.holding())


func test_switching_the_controls_off_while_holding_drops_the_card() -> void:
	var hud: MatchHud = MatchHud.new()
	add_child_autofree(hud)
	var hand: CardHand = hud.hand
	var def: WeaponDef = WeaponDef.new()
	def.id = &"test"
	hand.defs[0] = def
	hand.pickable[0] = true
	hand.active = true
	hand.visible = true
	watch_signals(hand)
	hand.press_at(7, hand.get_global_rect().position + hand.card_rects[0].get_center())
	assert_true(hand.holding())
	hud.set_controls_active(false)
	assert_signal_emitted_with_parameters(hand, "card_cancelled", [0])
	assert_false(hand.holding())
	assert_false(hud.controls_active())
