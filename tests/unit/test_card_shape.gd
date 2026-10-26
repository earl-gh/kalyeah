extends GutTest
## The mode card tipped like a real card floating over the table (CardShape, CardFace, CardRim).

const SIZE: Vector2 = Vector2(345.0, 552.0)


func test_the_top_left_corner_is_pushed_back_and_the_bottom_right_lifted() -> void:
	var move: float = CardShape.TILT * SIZE.x
	var top_left: Vector2 = CardShape.warp(Vector2.ZERO, SIZE)
	var bottom_right: Vector2 = CardShape.warp(SIZE, SIZE)
	assert_gt(top_left.x, 0.0, "the far corner moves in")
	assert_gt(top_left.y, 0.0)
	assert_gt(bottom_right.x, SIZE.x, "the near corner moves out")
	assert_gt(bottom_right.y, SIZE.y)
	assert_almost_eq(bottom_right.x - SIZE.x, move / 2.0, 0.01)
	assert_almost_eq(top_left.x, move / 2.0, 0.01)


func test_the_tipped_card_keeps_its_middle_and_its_size_almost_paper_thin() -> void:
	assert_almost_eq(CardShape.warp(SIZE / 2.0, SIZE).distance_to(SIZE / 2.0), 0.0, 0.01, "the middle stays where the flat card's middle was")
	assert_lt(CardShape.TILT, 0.05, "a gentle tip, not a flip")
	assert_lte(CardFace.THICKNESS.x, 5.0, "the card is thin")
	assert_lte(CardFace.THICKNESS.y, 6.0)
	assert_gt(CardFace.THICKNESS.length(), 3.0, "but its edge shows")


func test_the_outline_is_a_closed_rounded_shape_inside_its_bounds() -> void:
	var flat: PackedVector2Array = CardShape.flat_outline(SIZE, 24.0)
	assert_eq(flat.size(), 4 * (CardShape.ARC_STEPS + 1))
	var bounds: Rect2 = Rect2(Vector2(-10.0, -10.0), SIZE + Vector2(20.0, 20.0))
	for point: Vector2 in CardShape.outline(SIZE, 24.0):
		assert_true(bounds.has_point(point), "%s stays near the card" % point)
	var inset: PackedVector2Array = CardShape.flat_outline(SIZE, 24.0, 3.0)
	assert_gte(inset[0].x, 3.0 - 0.001, "an inset outline sits inside the edge")


func test_a_mode_card_is_drawn_by_a_face_and_a_rim() -> void:
	var card: Button = autofree(ChoiceCard.make_card("", "Local", "blue", SIZE, "hotspot")) as Button
	add_child(card)
	card.size = SIZE
	assert_not_null(card.get_node_or_null("Face"), "the picture is drawn as a tipped card")
	assert_false((card.get_node("Art") as TextureRect).visible, "the file's node only holds the picture")
	assert_true(card.get_theme_stylebox("normal") is StyleBoxEmpty, "the button draws nothing behind the tipped card")
