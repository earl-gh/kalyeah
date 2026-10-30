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


func test_in_sides_the_four_cards_stand_in_one_column_on_the_right_hand_side_top_to_bottom() -> void:
	var hand: CardHand = _hand(true)
	var rects: Array[Rect2] = hand.card_rects
	assert_eq(rects.size(), 4)
	for slot: int in 4:
		assert_eq(rects[slot].position.x, rects[0].position.x, "one column")
		assert_gte(rects[slot].position.x, 720.0 - CardHand.SIDES_COLUMN, "on the right (right-handed)")
		assert_lte(rects[slot].end.x, 720.0)
		assert_lte(rects[slot].end.y, 0.0, "above the bar's strip")
		assert_eq(rects[slot].size.x, CardHand.SIDES_CARD_WIDTH)
		if slot > 0:
			assert_gt(rects[slot].position.y, rects[slot - 1].position.y, "slot 0 on top")
			assert_false(rects[slot].intersects(rects[slot - 1]), "no two cards overlap")
	assert_true(hand.bar_rect.size.x > 400.0 and Rect2(Vector2.ZERO, hand.size).encloses(hand.bar_rect), "the bar runs along the strip")
	assert_lt(hand.next_rect.end.y, rects[0].position.y, "NEXT above the cards")
	assert_gte(hand.next_rect.position.x, 720.0 - CardHand.SIDES_COLUMN, "NEXT in the column")
	assert_true(hand.column_rect().encloses(rects[3]) and hand.column_rect().encloses(hand.next_rect))


func test_a_left_handed_player_gets_the_column_on_the_left() -> void:
	var left: CardHand = _hand(true, true)
	for rect: Rect2 in left.card_rects:
		assert_lte(rect.end.x, CardHand.SIDES_COLUMN, "on the left (left-handed)")
		assert_gte(rect.position.x, 0.0)
	assert_lte(left.next_rect.end.x, CardHand.SIDES_COLUMN)
	assert_eq(left.column_rect().position.x, 0.0)


func test_on_a_short_screen_the_cards_get_smaller_to_keep_the_column_under_the_header() -> void:
	var hand: CardHand = _hand(true)
	var screen_height: float = hand.get_viewport_rect().size.y
	var top: float = screen_height - hand.size.y + hand.column_rect().position.y
	assert_gte(top, 0.0, "the column starts on the screen")


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
	var other: Vector2 = hand.position + hand.card_rects[3].get_center()  # another card of the column
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
	assert_eq(practice._camera.bottom_reserve_px, CardHand.SIDES_TRAY, "the street gets the room under it")
	assert_eq(practice._camera.side_reserve_px, CardHand.SIDES_COLUMN, "and gives the column its side")
	assert_false(practice._camera.side_reserve_left, "the right for a right-handed player")
	hud.hand.visible = true  # playing, not picking or waiting
	hud.layout_controls()
	assert_almost_eq(hud.console.get_rect().size.y, CardHand.SIDES_TRAY, 0.5, "the plate is the bar's strip")
	hud.set_bottom_inset(40.0)
	assert_eq(hud.console_height(), CardHand.SIDES_TRAY + 40.0)
	hud.set_bottom_inset(0.0)
	Settings.set_cards_tray(&"bottom")
	assert_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT)
	assert_eq(practice._camera.side_reserve_px, 0.0, "the bottom tray reserves no side")
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
	assert_eq(hud.console.column_width, CardHand.SIDES_COLUMN, "while you play, the plate leaves the cards' column (and the chat button) outside it")
	hud.hand.visible = false
	hud.layout_controls()
	assert_almost_eq(hud.console.get_rect().size.y, MatchHud.CONSOLE_HEIGHT, 0.5, "the full plate while you wait")
	assert_eq(hud.console.column_width, 0.0, "edge to edge: no column while the hand is away")
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


## Where the street's corners fall on the screen (pixels) for `camera`.
func _street_rect(camera: ArenaCamera, layout: MapLayout) -> Rect2:
	var points: Array[Vector2] = []
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			points.append(camera.screen_position(Vector3(sx * layout.lane_width / 2.0, 0.0, sz * layout.lane_length / 2.0)))
	var rect: Rect2 = Rect2(points[0], Vector2.ZERO)
	for point: Vector2 in points:
		rect = rect.expand(point)
	return rect


## A phone-shaped window for a test (the headless one is landscape), put back by the caller.
func _phone_window() -> Vector2i:
	var window: Window = get_window()
	var saved: Vector2i = window.size
	window.size = Vector2i(720, 1280)
	return saved


func test_sides_pushes_the_street_away_from_the_cards_at_the_same_shape_and_a_smaller_size() -> void:
	var saved_size: Vector2i = _phone_window()
	var practice: PracticeMatch = await _practice()
	var camera: ArenaCamera = practice._camera
	camera.place(false)
	var before: Rect2 = _street_rect(camera, practice.layout)
	Settings.set_cards_tray(&"sides")
	camera.place(false)
	var right_handed: Rect2 = _street_rect(camera, practice.layout)
	var width: float = practice.get_viewport().get_visible_rect().size.x
	assert_lte(right_handed.end.x, width - CardHand.SIDES_COLUMN + 1.0, "the street ends where the column starts")
	assert_gte(right_handed.position.x, 0.0, "and is all on the screen")
	assert_lt(right_handed.size.x, before.size.x, "smaller (before %s, after %s)" % [before, right_handed])
	assert_almost_eq(right_handed.size.x / right_handed.size.y, before.size.x / before.size.y, 0.01, "the same shape")
	Settings.set_left_handed(true)
	camera.place(false)
	var left_handed: Rect2 = _street_rect(camera, practice.layout)
	assert_gte(left_handed.position.x, CardHand.SIDES_COLUMN - 1.0, "left-handed: the street starts where the column ends")
	assert_lte(left_handed.end.x, width + 0.5)
	assert_almost_eq(left_handed.size.x, right_handed.size.x, 1.0, "the same size either way")
	Settings.set_left_handed(false)
	Settings.set_cards_tray(&"bottom")
	camera.place(false)
	var back: Rect2 = _street_rect(camera, practice.layout)
	assert_almost_eq(back.position.x, before.position.x, 0.5, "back to Bottom, the street is where it was")
	assert_almost_eq(back.size.x, before.size.x, 0.5)
	get_window().size = saved_size


func test_the_flipped_view_pushes_the_street_the_same_way() -> void:
	var saved_size: Vector2i = _phone_window()
	var practice: PracticeMatch = await _practice()
	var camera: ArenaCamera = practice._camera
	Settings.set_cards_tray(&"sides")
	camera.place(true)
	var rect: Rect2 = _street_rect(camera, practice.layout)
	var width: float = practice.get_viewport().get_visible_rect().size.x
	assert_lte(rect.end.x, width - CardHand.SIDES_COLUMN + 1.0, "the other team's view too")
	get_window().size = saved_size


## The group the ice water bar draws: the drop (over the bar's start), the bar and the "Max: 10" caption's outline, as one rect.
func _ice_group(hand: CardHand) -> Rect2:
	var drop: Rect2 = HandArt.ice_rect(Vector2(hand.bar_rect.position.x, hand.bar_rect.get_center().y - CardHand.BAR_BAG * CardHand.BAG_RISE), CardHand.BAR_BAG)
	var caption_bottom: float = CardHand.max_caption_baseline(hand.bar_rect) + float(CardHand.MAX_OUTLINE) / 2.0
	var group: Rect2 = drop.merge(hand.bar_rect)
	return group.expand(Vector2(group.position.x, caption_bottom))


## The blue plate in the Sides layout (hand pixels): the strip less the cards' column, which is on the right for a right-handed player.
func _plate(hand: CardHand) -> Rect2:
	var left: float = CardHand.SIDES_COLUMN if hand.mirrored else 0.0
	return Rect2(Vector2(left, 0.0), Vector2(hand.size.x - CardHand.SIDES_COLUMN, hand.size.y))


func test_in_sides_the_chat_button_stands_outside_the_plate_at_the_foot_of_the_cards_column() -> void:
	# owner, 2026-10-30: the chat button stays OUTSIDE the chamfered plate, as in Bottom where it stands in the column beside the plate
	for mirrored: bool in [false, true]:
		var hand: CardHand = _hand(true, mirrored)
		var strip: Rect2 = Rect2(Vector2.ZERO, hand.size)
		var plate: Rect2 = _plate(hand)
		var chat: Rect2 = hand.chat_rect
		assert_true(strip.encloses(chat), "the chat button is on the screen, in the bottom strip's height (mirrored %s): %s" % [mirrored, chat])
		assert_false(chat.intersects(plate), "outside the plate (%s), mirrored %s" % [plate, mirrored])
		assert_true(hand.column_rect().position.x <= chat.position.x and hand.column_rect().end.x >= chat.end.x, "in the cards' column")
		assert_almost_eq(chat.get_center().x, hand.next_rect.get_center().x, 0.6, "straight under NEXT and the cards")
		assert_almost_eq(strip.end.y - chat.end.y, CardHand.SIDES_PAD, 0.001, "in the bottom corner, SIDES_PAD from the screen's bottom")
		assert_false(chat.intersects(hand.bar_rect), "no overlap with the bar")
		assert_false(chat.intersects(_ice_group(hand)), "nor with the drop or the Max caption")
		assert_eq(chat.size, Vector2(CardHand.CHAT_HEIGHT * HandArt.CHAT_ASPECT, CardHand.CHAT_HEIGHT), "the 80 px touch target")
		assert_gte(minf(chat.size.x, chat.size.y), UiMetrics.MIN_TARGET, "a full touch target")
		for rect: Rect2 in hand.card_rects:
			assert_false(chat.intersects(rect), "and not over a card")
		assert_false(chat.intersects(hand.next_rect))
		assert_lt(hand.next_rect.end.y, hand.card_rects[0].position.y, "NEXT stays above the cards")
		assert_lt(hand.card_rects[3].end.y, chat.position.y, "the chat button is under the bottom card")


func test_the_ice_water_group_keeps_an_even_padding_inside_the_narrower_plate() -> void:
	for mirrored: bool in [false, true]:
		var hand: CardHand = _hand(true, mirrored)
		var plate: Rect2 = _plate(hand)
		var group: Rect2 = _ice_group(hand)
		var inner: Rect2 = hand.sides_inner()
		assert_almost_eq(inner.position.y, ConsolePanel.BEVEL + CardHand.SIDES_PAD, 0.001, "the padding starts under the plate's chamfer")
		assert_almost_eq(inner.position.x - plate.position.x, CardHand.SIDES_PAD, 0.001, "12 px from the plate's left edge")
		assert_almost_eq(plate.end.x - inner.end.x, CardHand.SIDES_PAD, 0.001, "12 px from its right edge")
		assert_gte(group.position.y - ConsolePanel.BEVEL, CardHand.SIDES_PAD - 0.01, "top: the padding under the chamfer (mirrored %s)" % mirrored)
		assert_gte(hand.size.y - group.end.y, CardHand.SIDES_PAD - 0.01, "bottom padding")
		assert_gte(group.position.x - plate.position.x, CardHand.SIDES_PAD - 0.01, "left padding")
		assert_lte(group.end.x + 0.01, plate.end.x - CardHand.SIDES_PAD + 0.01, "right padding")
		assert_lte(absf((group.position.y - ConsolePanel.BEVEL) - (hand.size.y - group.end.y)), 1.0, "the top and bottom paddings are even (within a pixel)")
		assert_almost_eq(hand.bar_rect.end.x, inner.end.x, 0.001, "the bar is re-fitted: it runs to the padding of the narrower plate")
		assert_true(plate.encloses(hand.bar_rect), "inside the plate")
		assert_gt(hand.bar_rect.size.x, 300.0, "the bar still runs along the plate")
		assert_almost_eq(hand.bar_rect.size.y, CardHand.BAR_HEIGHT, 0.001, "at full size")


func test_the_bottom_tray_layout_is_what_it_was() -> void:
	var hand: CardHand = _hand(false)
	hand.size = Vector2(720.0, MatchHud.CONSOLE_HEIGHT)
	hand._layout()
	assert_eq(hand.bar_rect, Rect2(Vector2(CardHand.COLUMN + CardHand.SIDE + CardHand.BAR_BAG * HandArt.ICE_DRAW_ASPECT / 2.0, CardHand.ROW_TOP + CardHand.card_size(720.0).y + CardHand.BAR_GAP), Vector2(hand.bar_rect.size.x, CardHand.BAR_HEIGHT)))
	assert_eq(hand.chat_rect.size, Vector2(CardHand.CHAT_HEIGHT * HandArt.CHAT_ASPECT, CardHand.CHAT_HEIGHT))
	assert_lt(hand.chat_rect.end.y, hand.next_rect.position.y, "Bottom: the chat button above NEXT, outside the tray")


func test_the_chat_button_node_stands_beside_the_plate_at_the_bottom_at_both_phone_sizes_and_both_hands() -> void:
	var saved_size: Vector2i = get_window().size
	Settings.set_cards_tray(&"sides")
	for window_size: Vector2i in [Vector2i(720, 1280), Vector2i(390, 844)]:
		get_window().size = window_size
		for left_handed: bool in [false, true]:
			Settings.set_left_handed(left_handed)
			var practice: PracticeMatch = await _practice()
			var hud: MatchHud = practice.hud
			hud.hand.visible = true
			hud.chat.visible = true
			for inset: float in [0.0, 40.0]:
				hud.set_bottom_inset(inset)
				hud.layout_controls()
				var chat: Rect2 = hud.chat.get_global_rect()
				var console: Rect2 = hud.console.get_global_rect()
				var plate_left: float = console.position.x + (hud.console.column_width if hud.console.column_left else 0.0)
				var plate_right: float = console.end.x - (0.0 if hud.console.column_left else hud.console.column_width)
				var plate: Rect2 = Rect2(Vector2(plate_left, console.position.y), Vector2(plate_right - plate_left, console.size.y))
				var label: String = "%s, left-handed %s, inset %d" % [window_size, left_handed, inset]
				assert_eq(hud.console.column_width, CardHand.SIDES_COLUMN, "%s: the plate leaves the cards' column free" % label)
				assert_eq(hud.console.column_left, left_handed, "%s: on the writing hand's side" % label)
				assert_false(chat.intersects(plate), "%s: the chat button %s is outside the plate %s" % [label, chat, plate])
				assert_false(chat.intersects(hud.hand.get_global_rect().intersection(plate)), "%s: and clear of the plate's strip" % label)
				assert_false(chat.intersects(Rect2(hud.hand.get_global_rect().position + hud.hand.bar_rect.position, hud.hand.bar_rect.size)), "%s: the bar" % label)
				for rect: Rect2 in hud.hand.card_rects:
					assert_false(chat.intersects(Rect2(hud.hand.get_global_rect().position + rect.position, rect.size)), "%s: the cards" % label)
				assert_false(chat.intersects(Rect2(hud.hand.get_global_rect().position + hud.hand.next_rect.position, hud.hand.next_rect.size)), "%s: NEXT" % label)
				assert_almost_eq(chat.size.y, CardHand.CHAT_HEIGHT, 0.5, "%s: 80 px" % label)
				assert_almost_eq(chat.get_center().x, hud.hand.get_global_rect().position.x + hud.hand.next_rect.get_center().x, 0.6, "%s: under NEXT's column" % label)
				assert_almost_eq(hud.size.y - inset - chat.end.y, CardHand.SIDES_PAD, 0.5, "%s: at the bottom, just above the home bar" % label)
				assert_gte(chat.position.x, 0.0)
				assert_lte(chat.end.x, hud.size.x, "%s: on the screen" % label)
			hud.set_bottom_inset(0.0)
	Settings.set_left_handed(false)
	get_window().size = saved_size
