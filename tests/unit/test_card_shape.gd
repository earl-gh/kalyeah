extends GutTest
## The mode card drawn as a thin, straight card floating over the table (CardShape, CardFace, CardRim).

const SIZE: Vector2 = Vector2(345.0, 552.0)


func test_the_card_is_straight_and_flat() -> void:
	for point: Vector2 in CardShape.outline(SIZE, 24.0):
		assert_true(Rect2(Vector2.ZERO, SIZE).grow(0.001).has_point(point), "%s is on the card, not pushed out or in" % point)
	var points: PackedVector2Array = CardShape.outline(SIZE, 24.0)
	assert_eq(points.size(), 4 * (CardShape.ARC_STEPS + 1))
	var top: float = 9999.0
	for point: Vector2 in points:
		top = minf(top, point.y)
	assert_almost_eq(top, 0.0, 0.5, "the top edge is at the top of the card")
	var face: CardFace = autofree(CardFace.new()) as CardFace
	assert_false((face.get_script() as GDScript).get_script_constant_map().has("THICKNESS"), "no thickness (owner)")


func test_the_mode_cards_have_square_corners_and_a_power_line_up_the_bottom_of_the_right_edge() -> void:
	assert_eq(CardFace.RADIUS, 0.0, "no border radius (owner: like a teks card)")
	var square: PackedVector2Array = CardShape.outline(SIZE, CardFace.RADIUS)
	assert_eq(square.size(), 4, "a plain rectangle")
	assert_eq(square[0], Vector2.ZERO)
	assert_eq(square[2], SIZE)
	assert_eq(UiText.f(CardFace.POWER_FORMAT, [9999]), "POWER 9999")
	assert_eq(CardFace.POWER_COLOR, Color.WHITE, "white letters")
	assert_eq(CardFace.POWER_OUTLINE, Color.BLACK, "with a black outline")
	var powers: Dictionary = {}
	for mode: Dictionary in TitleScreen.MODES:
		powers[int(mode["power"])] = true
	assert_eq(powers.size(), TitleScreen.MODES.size(), "not always 9999: each mode has its own power")
	var card: Button = autofree(ChoiceCard.make_card("", "Local", "blue", SIZE, "hotspot")) as Button
	add_child(card)
	card.size = SIZE
	await wait_frames(2)
	assert_not_null(card.get_node("Face"), "it draws the power line with the card")


func test_an_inset_outline_sits_inside_the_edge() -> void:
	var inset: PackedVector2Array = CardShape.outline(SIZE, 24.0, 3.0)
	for point: Vector2 in inset:
		assert_true(Rect2(Vector2(3.0, 3.0) - Vector2(0.001, 0.001), SIZE - Vector2(6.0, 6.0) + Vector2(0.002, 0.002)).has_point(point))


func test_a_mode_card_is_drawn_by_a_face_with_no_coloured_outline() -> void:
	var card: Button = autofree(ChoiceCard.make_card("", "Local", "blue", SIZE, "hotspot")) as Button
	add_child(card)
	card.size = SIZE
	assert_not_null(card.get_node_or_null("Face"), "the picture is drawn as a card of its own")
	assert_false((card.get_node("Art") as TextureRect).visible, "the file's node only holds the picture")
	assert_true(card.get_theme_stylebox("normal") is StyleBoxEmpty, "the button draws nothing behind the card")
	var strip: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	strip.show_rim = false
	strip.size = Vector2(720.0, 700.0)
	add_child(strip)
	strip.setup("", [{"id": "hotspot", "title": "Local", "color": "blue", "accent": "4aa0ff"}] as Array[Dictionary], 0)
	assert_null(strip.cards[0].get_node_or_null("Frame"), "no coloured rim round the chosen card (owner)")


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


func test_the_card_group_is_centred_between_the_logo_and_the_bar() -> void:
	var screen: TitleScreen = autofree(load("res://scenes/ui/title_screen.tscn").instantiate()) as TitleScreen
	var holder: SubViewport = autofree(SubViewport.new()) as SubViewport
	holder.size = Vector2i(720, 1280)
	add_child(holder)
	holder.add_child(screen)
	await wait_frames(4)
	var logo: Rect2 = (screen.find_child("Logo", true, false) as Control).get_global_rect()
	var card: Rect2 = screen.carousel.cards[screen.carousel.index].get_global_rect()
	var buttons: Rect2 = screen.primary_button.get_global_rect()
	var bar_top: float = screen.tab_bar.get_global_rect().position.y
	var above: float = card.position.y - logo.end.y
	var below: float = bar_top - buttons.end.y
	assert_almost_eq(above, below, 24.0, "as much room above the card as under the buttons (%.0f above, %.0f below)" % [above, below])


func test_the_mode_cards_are_old_teks_cards_worn_at_the_corners_each_differently() -> void:
	var strip: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	strip.show_rim = false
	strip.size = Vector2(720.0, 700.0)
	add_child(strip)
	var options: Array[Dictionary] = [
		{"id": "online", "title": "Online", "color": "orange", "accent": "ffa63a", "power": 7450},
		{"id": "hotspot", "title": "Local", "color": "blue", "accent": "4aa0ff", "power": 9999},
		{"id": "practice", "title": "Practice", "color": "green", "accent": "45dd78", "power": 1200},
	]
	strip.setup("", options, 1)
	await wait_frames(2)
	var seeds: Dictionary = {}
	for card: Button in strip.cards:
		var face: CardFace = card.get_node("Face") as CardFace
		var picture: TextureRect = face.get_node("Picture") as TextureRect
		assert_true(picture.material is ShaderMaterial, "the picture is worn by a shader (brown paper at the corners, grain)")
		assert_eq((picture.material as ShaderMaterial).shader.resource_path, "res://shaders/card_picture.gdshader")
		assert_eq(picture.texture, face.texture)
		assert_eq((picture.material as ShaderMaterial).get_shader_parameter("rect_size"), face.size, "the shader knows the card's size")
		seeds[(picture.material as ShaderMaterial).get_shader_parameter("seed")] = true
		assert_not_null(face.get_node("Shade"), "the coloured shade stops at the worn corners")
		assert_eq((face.get_node("PowerLine") as CardPowerLine).text, "POWER %d" % face.power)
	assert_eq(seeds.size(), 3, "every card wears differently")
	var order: Array[String] = []
	for child: Node in strip.cards[1].get_node("Face").get_children():
		order.append(child.name)
	assert_eq(order, ["Picture", "PowerLine", "Shade", "Tint"] as Array[String], "picture, then the power line, then the shade over it, then the yellowish tint over all")
	var tint: ColorRect = strip.cards[1].get_node("Face/Tint") as ColorRect
	assert_lte(tint.color.a, 0.15, "a subtle tint")
	assert_gt(tint.color.r + tint.color.g, tint.color.b * 2.0, "yellowish")
