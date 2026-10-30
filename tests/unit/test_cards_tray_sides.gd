extends GutTest
## Cards tray (Settings > Controls > Cards tray, docs/GDD.md "Controls: tap to move, cards to cast"): Bottom is the tray along the bottom,
## Sides puts two cards on each side of the street and leaves only the ice water bar in a strip along the bottom.
## Seam: Settings.cards_tray, CardHand (layout and touches), MatchHud (tray heights, camera reserve), ControlOptions.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")

var _saved: StringName
var _saved_left: bool


func before_each() -> void:
	_saved = Settings.cards_tray
	_saved_left = Settings.left_handed
	Settings.set_left_handed(false)
	Settings.set_cards_tray(&"bottom")


func after_each() -> void:
	Settings.set_cards_tray(_saved)
	Settings.set_left_handed(_saved_left)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	await wait_process_frames(2)
	return practice


func _hand(sides: bool, mirrored: bool = false) -> CardHand:
	var hand: CardHand = CardHand.new()
	hand.size = Vector2(720.0, CardHand.SIDES_TRAY)
	hand.position = Vector2(0.0, 1280.0 - CardHand.SIDES_TRAY)
	add_child_autofree(hand)
	hand.set_mirrored(mirrored)
	hand.set_sides(sides)
	var def: WeaponDef = WeaponDef.new()
	def.id = &"test"
	def.short_name = "TEST"
	def.elixir_cost = 3
	for slot: int in CardHand.SLOTS:
		hand.defs[slot] = def
		hand.pickable[slot] = true
	hand.active = true
	return hand


func _touch(at: Vector2, pressed: bool) -> InputEventScreenTouch:
	var event: InputEventScreenTouch = InputEventScreenTouch.new()
	event.position = at
	event.pressed = pressed
	return event


func _drag(at: Vector2) -> InputEventScreenDrag:
	var event: InputEventScreenDrag = InputEventScreenDrag.new()
	event.position = at
	return event


func test_the_default_is_the_bottom_tray_and_the_choice_is_saved() -> void:
	assert_eq(Settings.CARD_TRAYS, [&"bottom", &"sides"] as Array[StringName])
	assert_eq(Settings.cards_tray, &"bottom")
	Settings.set_cards_tray(&"sides")
	assert_eq(Settings.cards_tray, &"sides")
	var file: ConfigFile = ConfigFile.new()
	assert_eq(file.load(Settings.SAVE_PATH), OK)
	assert_eq(String(file.get_value("input", "cards_tray", "")), "sides")
	Settings.set_cards_tray(&"nonsense")
	assert_eq(Settings.cards_tray, &"sides", "an unknown tray changes nothing")


func test_in_sides_two_cards_stand_on_each_side_the_upper_one_first_and_all_inside_the_screen() -> void:
	var hand: CardHand = _hand(true)
	var rects: Array[Rect2] = hand.card_rects
	assert_eq(rects.size(), 4)
	assert_eq(rects[0].position.x, rects[1].position.x, "slots 0 and 1 share a side")
	assert_eq(rects[2].position.x, rects[3].position.x, "slots 2 and 3 share the other")
	assert_lt(rects[0].position.x, rects[2].position.x, "the left side first")
	assert_lt(rects[0].position.y, rects[1].position.y, "the upper card first")
	assert_lt(rects[2].position.y, rects[3].position.y)
	for rect: Rect2 in rects:
		assert_gte(rect.position.x, 0.0)
		assert_lte(rect.end.x, 720.0)
		assert_lte(rect.end.y, 0.0, "the cards stand above the bar's strip")
		assert_eq(rect.size.x, CardHand.SIDES_CARD_WIDTH)
	assert_false(rects[0].intersects(rects[1]), "no two cards overlap")
	assert_true(hand.bar_rect.size.x > 400.0 and Rect2(Vector2.ZERO, hand.size).encloses(hand.bar_rect), "the bar runs along the strip")
	assert_almost_eq(hand.next_rect.get_center().x, 360.0, 0.5, "NEXT in the middle")
	assert_lt(hand.chat_rect.position.y, hand.next_rect.position.y, "the chat button above it")


func test_a_left_handed_player_gets_the_stacks_the_other_way_round() -> void:
	var right: CardHand = _hand(true)
	var left: CardHand = _hand(true, true)
	assert_gt(left.card_rects[0].position.x, left.card_rects[2].position.x, "slot 0 on the right")
	assert_eq(left.card_rects[0].position.x, right.card_rects[2].position.x)


func test_a_card_above_the_strip_is_picked_up_dragged_and_thrown_like_any_other() -> void:
	var hand: CardHand = _hand(true)
	watch_signals(hand)
	var on_card: Vector2 = hand.position + hand.card_rects[0].get_center()
	hand._input(_touch(on_card, true))
	assert_signal_emitted_with_parameters(hand, "card_pressed", [0])
	hand._input(_drag(on_card + Vector2(0.0, -4.0)))
	assert_true(hand.over_hand, "still on its own stack")
	hand._input(_drag(Vector2(360.0, 500.0)))
	assert_false(hand.over_hand, "over the street")
	hand._input(_touch(Vector2(360.0, 500.0), false))
	assert_signal_emitted_with_parameters(hand, "card_released", [0, Vector2(360.0, 500.0), false, false])


func test_a_card_let_go_back_on_a_stack_is_a_cancel_not_a_throw() -> void:
	var hand: CardHand = _hand(true)
	watch_signals(hand)
	hand._input(_touch(hand.position + hand.card_rects[1].get_center(), true))
	var other: Vector2 = hand.position + hand.card_rects[3].get_center()  # the other side's stack
	hand._input(_drag(other))
	assert_true(hand.over_hand, "over the hand: it will be put back")
	hand._input(_touch(other, false))
	assert_signal_emitted_with_parameters(hand, "card_released", [1, other, false, true])


func test_the_bottom_tray_still_lays_the_cards_in_one_row() -> void:
	var hand: CardHand = _hand(false)
	hand.size = Vector2(720.0, MatchHud.CONSOLE_HEIGHT)
	hand._layout()
	for slot: int in range(1, 4):
		assert_eq(hand.card_rects[slot].position.y, hand.card_rects[0].position.y, "one row")
		assert_gt(hand.card_rects[slot].position.x, hand.card_rects[slot - 1].position.x)
	assert_gte(hand.card_rects[0].position.y, 0.0, "inside the tray")


func test_sides_shrinks_the_tray_the_camera_keeps_clear_and_the_bottom_is_unchanged() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	assert_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT)
	assert_false(hud.hand.sides)
	Settings.set_cards_tray(&"sides")
	assert_true(hud.hand.sides)
	assert_eq(hud.console_height(), CardHand.SIDES_TRAY)
	assert_eq(practice._camera.bottom_reserve_px, CardHand.SIDES_TRAY, "the street gets the room")
	hud.hand.visible = true  # playing, not picking or waiting
	hud.layout_controls()
	assert_almost_eq(hud.console.get_rect().size.y, CardHand.SIDES_TRAY, 0.5, "the plate is the bar's strip")
	hud.set_bottom_inset(40.0)
	assert_eq(hud.console_height(), CardHand.SIDES_TRAY + 40.0)
	hud.set_bottom_inset(0.0)
	Settings.set_cards_tray(&"bottom")
	assert_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT)
	assert_false(hud.hand.sides)


func test_while_tagged_the_plate_grows_to_the_whole_tray_so_the_tagged_panel_has_room() -> void:
	Settings.set_cards_tray(&"sides")
	var practice: PracticeMatch = await _practice()
	practice.finish_pick()
	practice.skip_intro()
	var hud: MatchHud = practice.hud
	hud.hand.visible = true
	hud.layout_controls()
	assert_almost_eq(hud.console.get_rect().size.y, CardHand.SIDES_TRAY, 0.5)
	hud.hand.visible = false
	hud.layout_controls()
	assert_almost_eq(hud.console.get_rect().size.y, MatchHud.CONSOLE_HEIGHT, 0.5, "the full plate while you wait")
	assert_almost_eq(hud.tagged_panel.get_rect().size.y, MatchHud.CONSOLE_HEIGHT, 0.5)
	assert_eq(hud.console_height(), CardHand.SIDES_TRAY, "but the street stays framed the same")


func test_the_settings_page_has_the_choice_and_the_match_menu_does_not() -> void:
	var page: SettingsPage = autofree(SettingsPage.new()) as SettingsPage
	add_child(page)
	await wait_frames(2)
	var options: ControlOptions = page.find_child("ControlOptions", true, false) as ControlOptions
	assert_not_null(options.tray_choice, "Cards tray on the Settings page")
	assert_eq(options.tray_choice.current, &"bottom")
	options.tray_choice.buttons[&"sides"].pressed.emit()
	assert_eq(Settings.cards_tray, &"sides")
	var in_match: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	in_match.in_match = true
	add_child(in_match)
	assert_null(in_match.tray_choice, "the match menu's list stays short")
