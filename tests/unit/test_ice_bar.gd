extends GutTest
## The ice water in the hand (owner: the cost drop hangs out of the card and looks stretched, the bar looks cheap): the cost drop sits
## fully inside its card at the bottom, drawn narrower than its picture; the bar marks the cheapest card in white, lets go of its dividers
## while full, pops a segment as it completes and sleeps (no redraws) when nothing moves.

const DT: float = 1.0 / 60.0

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func _def(cost: int) -> WeaponDef:
	var def: WeaponDef = WeaponDef.new()
	def.elixir_cost = cost
	return def


## A hand on its own (720 wide), its first look at the sim already taken: `elixir` points, cards costing `costs` (0 = an empty slot).
func _hand(elixir: float, costs: Array[int] = [2, 3, 4, 5]) -> CardHand:
	var hand: CardHand = CardHand.new()
	hand.size = Vector2(720.0, 300.0)
	add_child_autofree(hand)
	hand.set_process(false)
	for slot: int in costs.size():
		hand.defs[slot] = _def(costs[slot]) if costs[slot] > 0 else null
	hand.uses_elixir = true
	hand.elixir = elixir
	hand.step(DT)
	return hand


func _run(hand: CardHand, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		hand.step(DT)


# ---- the cost drop -------------------------------------------------------------

func test_the_cost_drop_sits_fully_inside_its_card_bottom_aligned() -> void:
	for width: float in [720.0, 540.0]:
		var card: Vector2 = CardHand.card_size(width)
		var rect: Rect2 = Rect2(Vector2(156.0, 28.0), card)
		var drop: Rect2 = CardHand.cost_drop_rect(rect)
		assert_true(rect.encloses(drop), "%.0f: the drop is all inside the card (it used to hang over the bottom edge)" % width)
		assert_almost_eq(drop.get_center().x, rect.get_center().x, 0.001, "%.0f: in the middle" % width)
		var room: float = rect.end.y - drop.end.y
		assert_gt(room, 0.0, "%.0f: a little room under it" % width)
		assert_lt(room, card.x * 0.1, "%.0f: bottom aligned, close to the card's bottom edge" % width)
		assert_between(drop.size.x / card.x, 0.30, 0.36, "%.0f: small enough not to cover the art" % width)


func test_the_held_card_keeps_its_drop_inside_at_both_sizes() -> void:
	var hand: CardHand = _hand(10.0)
	hand.active = true
	hand.held_slot = 1
	hand.over_hand = true
	hand.finger = hand.card_rects[1].get_center()
	var lifted: Rect2 = hand.held_card_rect()
	assert_true(lifted.encloses(CardHand.cost_drop_rect(lifted)), "lifted over the hand")
	hand.over_hand = false
	var small: Rect2 = hand.held_card_rect()
	assert_lt(small.size.x, lifted.size.x, "shrunk over the street")
	var drop: Rect2 = CardHand.cost_drop_rect(small)
	assert_true(small.encloses(drop), "and its drop shrinks with it, still inside")
	assert_almost_eq(drop.size.x / small.size.x, CardHand.COST_SHARE, 0.0001, "in the same proportion")
	hand.held_slot = -1


func test_the_drop_is_drawn_narrower_than_its_picture_and_the_bar_starts_after_the_drawn_width() -> void:
	assert_lt(HandArt.ICE_DRAW_ASPECT, HandArt.ICE_ASPECT, "narrower than the picture's own ratio (it looked stretched sideways)")
	assert_almost_eq(HandArt.ICE_DRAW_ASPECT, 0.80, 0.0001)
	assert_gt(HandArt.ICE_DRAW_ASPECT, HandArt.ICE_ASPECT * 0.85, "but only slightly, the snowflake stays right")
	var rect: Rect2 = HandArt.ice_rect(Vector2(100.0, 100.0), 60.0)
	assert_almost_eq(rect.size.x / rect.size.y, HandArt.ICE_DRAW_ASPECT, 0.0001)
	assert_eq(rect.get_center(), Vector2(100.0, 100.0))
	var hand: CardHand = _hand(0.0)
	var cards_x: float = hand.card_rects[0].position.x
	assert_almost_eq(hand.bar_rect.position.x - cards_x, CardHand.BAR_BAG * HandArt.ICE_DRAW_ASPECT / 2.0, 0.001, "the bar starts at the middle of the drawn drop")


# ---- the cheapest card's marker ------------------------------------------------

func test_the_cheapest_cost_of_the_hand_ignores_empty_slots() -> void:
	assert_eq(_hand(5.0, [4, 2, 5, 3]).cheapest_cost(), 2)
	assert_eq(_hand(5.0, [5, 5, 5, 5]).cheapest_cost(), 5)
	assert_eq(_hand(5.0, [0, 0, 4, 0]).cheapest_cost(), 4, "empty slots are not free cards")
	assert_eq(_hand(5.0, [0, 0, 0, 0]).cheapest_cost(), 0, "an empty hand costs nothing")


func test_the_marker_runs_from_the_bars_start_to_the_end_of_the_cheapest_cost_in_segments() -> void:
	var hand: CardHand = _hand(5.0, [4, 2, 5, 3])
	var marker: Rect2 = hand.marker_rect()
	assert_eq(marker.position, hand.bar_rect.position, "from the bar's start")
	assert_eq(marker.size.y, hand.bar_rect.size.y, "as tall as the bar")
	assert_almost_eq(marker.end.x - float(CardHand.MARKER_WIDTH) / 2.0, hand.boundary_x(2), 0.001, "its line is centred on the end of segment 2")
	var wider: Rect2 = _hand(5.0, [4, 5, 5, 3]).marker_rect()
	assert_almost_eq(wider.end.x - marker.end.x, hand.boundary_x(3) - hand.boundary_x(2), 0.001, "one more cost, one more segment")
	assert_gte(CardHand.MARKER_WIDTH, 2, "about two pixels")
	assert_eq(CardHand.MARKER_COLOR, Color.WHITE, "white")


func test_the_marker_follows_the_hand_and_hides_when_there_is_nothing_to_mark() -> void:
	var hand: CardHand = _hand(5.0, [4, 2, 5, 3])
	var before: float = hand.marker_rect().end.x
	hand.defs[1] = _def(5)
	assert_gt(hand.marker_rect().end.x, before, "a new hand moves it")
	for slot: int in CardHand.SLOTS:
		hand.defs[slot] = null
	assert_eq(hand.marker_rect(), Rect2(), "an empty hand shows none")
	hand.defs[0] = _def(3)
	assert_gt(hand.marker_rect().size.x, 0.0)
	hand.uses_elixir = false
	assert_eq(hand.marker_rect(), Rect2(), "none with elixir off")
	hand.uses_elixir = true
	hand.defs[0] = _def(30)
	assert_almost_eq(hand.marker_rect().end.x, hand.bar_rect.end.x, 0.001, "a cost past the bar ends on the bar")


# ---- the dividers --------------------------------------------------------------

func test_the_full_bar_keeps_its_dark_blue_partitions() -> void:
	var hand: CardHand = _hand(10.0)
	assert_eq(hand.divider_visibility(), 1.0)
	assert_eq(CardHand.DIVIDER, CardHand.BAR_EDGE)
	assert_lt(CardHand.DIVIDER.v, 0.5)


func test_partition_positions_stop_at_the_refilled_part() -> void:
	var hand: CardHand = _hand(3.5)
	var positions: PackedFloat32Array = hand.divider_positions()
	assert_eq(positions.size(), 3)
	for x: float in positions:
		assert_lte(x, hand.boundary_x(3))
	hand.shown_elixir = 0.0
	assert_eq(hand.divider_positions().size(), 0)
	hand.shown_elixir = 10.0
	assert_eq(hand.divider_positions().size(), 9)


func _popping(hand: CardHand) -> Array[int]:
	var found: Array[int] = []
	for segment: int in hand.bar_segments():
		if hand.pop_progress(segment) >= 0.0:
			found.append(segment)
	return found


func test_a_segment_pops_when_it_completes_and_is_done_after_its_time() -> void:
	var hand: CardHand = _hand(3.0)
	assert_eq(_popping(hand), [] as Array[int], "no pop on the first look")
	hand.elixir = 4.0
	for i: int in 3:
		hand.step(DT)
	assert_eq(_popping(hand), [] as Array[int], "no pop while the segment is still filling")
	for i: int in 20:
		hand.step(DT)
		if not _popping(hand).is_empty():
			break
	assert_eq(_popping(hand), [3] as Array[int], "segment 4 (index 3) popped as it completed")
	assert_between(CardHand.POP_TIME, 0.18, 0.25, "a fast pop")
	var early: float = hand.pop_progress(3)
	hand.step(DT)
	assert_gt(hand.pop_progress(3), early, "it advances")
	_run(hand, CardHand.POP_TIME)
	assert_eq(_popping(hand), [] as Array[int], "and is over")


func test_every_segment_the_bar_crosses_pops_once() -> void:
	var hand: CardHand = _hand(2.0)
	hand.elixir = 5.0
	var popped: Array[int] = []
	for i: int in 40:
		hand.step(DT)
		for segment: int in _popping(hand):
			if segment not in popped:
				popped.append(segment)
	assert_eq(popped, [2, 3, 4] as Array[int], "the three segments the bar crossed")


func test_spending_pops_nothing_and_cancels_a_pop_in_flight() -> void:
	var hand: CardHand = _hand(6.0)
	hand.elixir = 2.0
	for i: int in 30:
		hand.step(DT)
		assert_eq(_popping(hand), [] as Array[int], "a bar jumping down pops nothing")
	hand.elixir = 4.0
	var popped: bool = false
	for i: int in 30:
		hand.step(DT)
		popped = popped or not _popping(hand).is_empty()
	assert_true(popped, "regrowing past segments pops them")
	hand.elixir = 0.0
	_run(hand, 1.0)
	assert_eq(_popping(hand), [] as Array[int])


func test_reduce_motion_pops_nothing() -> void:
	Settings.reduced_motion = true
	var hand: CardHand = _hand(3.0)
	hand.elixir = 6.0
	hand.step(DT)
	assert_eq(_popping(hand), [] as Array[int], "no pop at all")
	assert_eq(hand.shown_elixir, 6.0, "the bar just shows the value")


func test_the_pop_grows_with_an_eased_overshoot_and_lands_on_one() -> void:
	assert_almost_eq(CardHand.ease_out_back(0.0), 0.0, 0.0001)
	assert_almost_eq(CardHand.ease_out_back(1.0), 1.0, 0.0001)
	var peak: float = 0.0
	for i: int in 101:
		peak = maxf(peak, CardHand.ease_out_back(float(i) / 100.0))
	assert_gt(peak, 1.0, "it overshoots")
	assert_lt(peak, 1.2, "slightly")
	assert_between(CardHand.POP_FROM, 0.3, 0.9, "it grows from part of the bar's height")


# ---- sleeping ------------------------------------------------------------------

func test_the_hand_only_asks_for_a_redraw_while_something_moves() -> void:
	var hand: CardHand = _hand(3.0)
	assert_false(hand.step(DT), "a still bar needs no redraw")
	hand.elixir = 4.5
	assert_true(hand.step(DT), "a bar that moves does")
	var moving: int = 0
	for i: int in 120:
		if hand.step(DT):
			moving += 1
	assert_lt(moving, 120, "and it goes back to sleep once the pop is over")
	assert_false(hand.step(DT))
	hand.deny(0)
	assert_true(hand.step(DT), "a shake does too")
	_run(hand, CardHand.DENY_TIME + 0.1)
	assert_false(hand.step(DT), "and sleeps after it")


func test_the_bar_draws_without_errors_in_every_state() -> void:
	var hand: CardHand = _hand(0.0)
	hand.visible = true
	for value: float in [0.0, 2.5, 3.0, 9.99, 10.0, 4.0]:
		hand.elixir = value
		hand.step(DT)
		hand.queue_redraw()
		await wait_frames(2)
	hand.uses_elixir = false
	await wait_frames(2)
	assert_true(is_instance_valid(hand))
