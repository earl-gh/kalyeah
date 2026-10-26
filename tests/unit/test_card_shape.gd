extends GutTest
## The mode card drawn as a thin, straight card floating over the table (CardShape, CardFace, CardRim).

const SIZE: Vector2 = Vector2(345.0, 552.0)


func test_the_card_is_straight_and_almost_paper_thin() -> void:
	for point: Vector2 in CardShape.outline(SIZE, 24.0):
		assert_true(Rect2(Vector2.ZERO, SIZE).grow(0.001).has_point(point), "%s is on the card, not pushed out or in" % point)
	var points: PackedVector2Array = CardShape.outline(SIZE, 24.0)
	assert_eq(points.size(), 4 * (CardShape.ARC_STEPS + 1))
	var top: float = 9999.0
	for point: Vector2 in points:
		top = minf(top, point.y)
	assert_almost_eq(top, 0.0, 0.5, "the top edge is at the top of the card")
	assert_lte(CardFace.THICKNESS.x, 5.0, "the card is thin")
	assert_lte(CardFace.THICKNESS.y, 6.0)
	assert_gt(CardFace.THICKNESS.length(), 3.0, "but its edge shows")


func test_the_mode_cards_have_square_corners_and_a_power_line_up_the_right_edge() -> void:
	assert_eq(CardFace.RADIUS, 0.0, "no border radius (owner: like a teks card)")
	var square: PackedVector2Array = CardShape.outline(SIZE, CardFace.RADIUS)
	assert_eq(square.size(), 4, "a plain rectangle")
	assert_eq(square[0], Vector2.ZERO)
	assert_eq(square[2], SIZE)
	assert_eq(CardFace.POWER_TEXT, "POWER 9999")
	var card: Button = autofree(ChoiceCard.make_card("", "Local", "blue", SIZE, "hotspot")) as Button
	add_child(card)
	card.size = SIZE
	await wait_frames(2)
	assert_not_null(card.get_node("Face"), "it draws the power line with the card")


func test_an_inset_outline_sits_inside_the_edge() -> void:
	var inset: PackedVector2Array = CardShape.outline(SIZE, 24.0, 3.0)
	for point: Vector2 in inset:
		assert_true(Rect2(Vector2(3.0, 3.0) - Vector2(0.001, 0.001), SIZE - Vector2(6.0, 6.0) + Vector2(0.002, 0.002)).has_point(point))


func test_a_mode_card_is_drawn_by_a_face_and_a_rim() -> void:
	var card: Button = autofree(ChoiceCard.make_card("", "Local", "blue", SIZE, "hotspot")) as Button
	add_child(card)
	card.size = SIZE
	assert_not_null(card.get_node_or_null("Face"), "the picture is drawn as a card of its own")
	assert_false((card.get_node("Art") as TextureRect).visible, "the file's node only holds the picture")
	assert_true(card.get_theme_stylebox("normal") is StyleBoxEmpty, "the button draws nothing behind the card")


func test_the_middle_card_floats_slowly_and_its_shadow_follows_and_rests_with_reduce_motion() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.reduced_motion = false
	var strip: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	strip.size = Vector2(720.0, 700.0)
	strip.bob = TitleScreen.AMBIENT_BOB
	add_child(strip)
	var options: Array[Dictionary] = [
		{"id": "online", "title": "Online", "color": "orange", "accent": "ffa63a"},
		{"id": "hotspot", "title": "Local", "color": "blue", "accent": "4aa0ff"},
		{"id": "practice", "title": "Practice", "color": "green", "accent": "45dd78"},
	]
	strip.setup("", options, 1)
	strip._layout()
	var middle: CardFace = strip.cards[1].get_node("Face") as CardFace
	var side: CardFace = strip.cards[0].get_node("Face") as CardFace
	assert_eq(side.lift, 0.0, "the cards behind it hold still")
	var seen: Dictionary = {}
	var start_y: float = strip.cards[1].position.y
	for i: int in 6:
		await wait_seconds(0.5)
		strip._layout()
		seen[snappedf(middle.lift, 0.05)] = true
		assert_lte(absf(strip.cards[1].position.y - start_y), TitleScreen.AMBIENT_BOB * 2.0 + 0.1, "it drifts a few pixels, no more")
	assert_gt(seen.size(), 1, "the middle card floats up and down")
	Settings.reduced_motion = true
	strip._layout()
	assert_eq(middle.lift, 0.0, "with Reduce motion the card rests")
	Settings.reduced_motion = motion
