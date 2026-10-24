extends GutTest
## The bottom of the match screen (owner: "make the bottom part exactly like clash royale"), pinned as layout facts at 720x1280 and at
## a 390x844 phone (720x1558 in UI pixels): NEXT small at the left, four upright cards in a row, the elixir bar along the bottom with
## your health at its end, every card thumb-sized, in the bottom of the screen, above the home bar, and the street camera keeping
## the tray's height clear.

const SHAPES: Array[Vector2] = [Vector2(720.0, 1280.0), Vector2(720.0, 1558.0)]

var _left: bool


func before_each() -> void:
	_left = Settings.left_handed
	Settings.left_handed = false


func after_each() -> void:
	Settings.left_handed = _left


func _hud(shape: Vector2, inset: float = 0.0) -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = shape
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.set_bottom_inset(inset)
	hud.layout_controls()
	return hud


func _on_screen(hud: MatchHud, rect: Rect2) -> Rect2:
	return Rect2(hud.hand.get_global_rect().position + rect.position, rect.size)


func test_the_tray_fits_its_cards_exactly() -> void:
	assert_eq(CardHand.tray_height(720.0), MatchHud.CONSOLE_HEIGHT, "the camera reserves what the hand needs, no more")


func test_next_then_four_upright_cards_then_the_bar_under_them() -> void:
	for shape: Vector2 in SHAPES:
		var hud: MatchHud = _hud(shape)
		var hand: CardHand = hud.hand
		assert_eq(hand.card_rects.size(), 4, "%s: a hand of four" % shape)
		var next: Rect2 = _on_screen(hud, hand.next_rect)
		var cards: Array[Rect2] = []
		for card: Rect2 in hand.card_rects:
			cards.append(_on_screen(hud, card))
		assert_lt(next.end.y, cards[0].position.y, "%s: NEXT above the hand, outside the tray (owner)" % shape)
		assert_lt(next.position.x, shape.x / 2.0, "%s: at the left" % shape)
		assert_lt(next.size.x, cards[0].size.x, "%s: and smaller" % shape)
		assert_true(Rect2(Vector2.ZERO, shape).encloses(next), "%s: on screen" % shape)
		var chat: Rect2 = _on_screen(hud, hand.chat_rect)
		assert_lt(chat.end.y, next.position.y, "%s: the chat button above NEXT" % shape)
		assert_gte(minf(chat.size.x, chat.size.y), UiMetrics.MIN_TARGET, "%s: thumb-sized" % shape)
		for slot: int in cards.size():
			var card: Rect2 = cards[slot]
			assert_gt(card.size.y, card.size.x, "%s: upright cards" % shape)
			assert_gte(minf(card.size.x, card.size.y), UiMetrics.MIN_TARGET, "%s: card %d is thumb-sized" % [shape, slot + 1])
			assert_true(Rect2(Vector2.ZERO, shape).encloses(card), "%s: card %d on screen" % [shape, slot + 1])
			assert_gt(card.position.y, shape.y * 0.75, "%s: card %d in the bottom quarter, under the thumb" % [shape, slot + 1])
			if slot > 0:
				assert_gt(card.position.x - cards[slot - 1].end.x, 0.0, "%s: a gap between cards %d and %d" % [shape, slot, slot + 1])
		var bar: Rect2 = _on_screen(hud, hand.bar_rect)
		var health: Rect2 = _on_screen(hud, hand.health_rect)
		assert_gt(bar.position.y, cards[0].end.y, "%s: the elixir bar under the cards" % shape)
		assert_gt(bar.size.x, shape.x * 0.6, "%s: running along the bottom" % shape)
		assert_gt(health.position.x, bar.end.x, "%s: your health at its right end" % shape)
		assert_lte(health.end.x, shape.x, "%s: on screen" % shape)
		assert_lte(bar.end.y, shape.y, "%s: on screen" % shape)


func test_the_hand_rides_above_the_home_bar() -> void:
	var plain: MatchHud = _hud(SHAPES[0])
	var card_y: float = _on_screen(plain, plain.hand.card_rects[0]).end.y
	var hud: MatchHud = _hud(SHAPES[0], 40.0)
	assert_almost_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT + 40.0, 0.01, "the tray grows by the inset")
	assert_almost_eq(_on_screen(hud, hud.hand.card_rects[0]).end.y, card_y - 40.0, 0.5, "the cards ride above the bar")
	assert_lte(_on_screen(hud, hud.hand.bar_rect).end.y, 1280.0 - 40.0, "the elixir bar too")


func test_a_finger_on_the_tray_is_the_hand_and_never_the_street() -> void:
	var hud: MatchHud = _hud(SHAPES[0])
	assert_eq(hud.hand.mouse_filter, Control.MOUSE_FILTER_STOP, "the tray takes its touches, so a tap on it never walks the kid")
	assert_eq(hud.marker.mouse_filter, Control.MOUSE_FILTER_IGNORE, "the ring takes none")
	assert_eq(hud.overhead.mouse_filter, Control.MOUSE_FILTER_IGNORE, "nor do the bars over the kids")


func test_the_street_camera_keeps_the_tray_clear() -> void:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	practice.advance(1.0 / 30.0)
	assert_almost_eq(practice._camera.bottom_reserve_px, practice.hud.console_height(), 0.5)
