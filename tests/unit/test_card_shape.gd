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


func test_a_mode_card_is_drawn_by_a_face_of_its_own() -> void:
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
		assert_null(face.get_node_or_null("Shade"), "no coloured shade over the picture (owner)")
		assert_eq((face.get_node("PowerLine") as CardPowerLine).text, "POWER %d" % face.power)
	assert_eq(seeds.size(), 3, "every card wears differently")
	var radii: Dictionary = {}
	var every: Dictionary = {}
	for card: Button in strip.cards:
		var face: CardFace = card.get_node("Face") as CardFace
		var round: Vector4 = face.corner_radii()
		for value: float in [round.x, round.y, round.z, round.w]:
			assert_between(value, CardFace.CORNER_RADIUS_MIN, CardFace.CORNER_RADIUS_MAX, "a slight radius, not sharp and not very round")
			every[snappedf(value, 0.01)] = true
		radii[round] = true
		var material: ShaderMaterial = (face.get_node("Picture") as TextureRect).material as ShaderMaterial
		assert_eq(material.get_shader_parameter("radii"), round, "the shader rounds the same corners")
	assert_eq(radii.size(), 3, "the corners differ from card to card")
	assert_gt(every.size(), 9, "and every corner has its own radius")
	var outline: PackedVector2Array = CardShape.outline_corners(Vector2(385.0, 550.0), Vector4(20.0, 0.0, 12.0, 8.0))
	assert_true(outline.has(Vector2(385.0, 0.0)), "a corner with no radius stays square")
	assert_false(outline.has(Vector2.ZERO), "the top left is rounded")
	assert_false(outline.has(Vector2(0.0, 550.0)), "and so is the bottom left")
	var order: Array[String] = []
	for child: Node in strip.cards[1].get_node("Face").get_children():
		order.append(child.name)
	assert_eq(order, ["Picture", "PowerLine", "Tint"] as Array[String], "picture, then the power line, then the yellowish tint over all")
	var tint: ColorRect = strip.cards[1].get_node("Face/Tint") as ColorRect
	assert_lte(tint.color.a, 0.15, "a subtle tint")
	assert_gt(tint.color.r + tint.color.g, tint.color.b * 2.0, "yellowish")


func test_the_mode_name_and_its_phrase_are_white_with_a_black_outline() -> void:
	var screen: TitleScreen = autofree(load("res://scenes/ui/title_screen.tscn").instantiate()) as TitleScreen
	add_child(screen)
	await wait_frames(2)
	for label: Label in [screen.mode_name_label, screen.mode_text_label]:
		assert_eq(label.get_theme_color("font_color"), Color.WHITE, "%s is white" % label.name)
		assert_eq(label.get_theme_color("font_outline_color"), Color.BLACK, "%s has a black outline" % label.name)
		assert_gt(label.get_theme_constant("outline_size"), 3, "%s: a real outline" % label.name)


func test_the_two_worn_layers_have_the_owners_lightness_grades() -> void:
	# lightness = perceived brightness of the base colour, 0 black to 100 white: layer 1 (thin paper) 64, layer 2 (core) 48
	var source: String = FileAccess.get_file_as_string("res://shaders/card_picture.gdshader")
	var grades: Dictionary = {"TAN": 64.0, "CORE": 48.0}
	for layer: String in grades:
		var found: RegExMatch = RegEx.create_from_string("const vec3 %s = vec3\\(([0-9.]+), ([0-9.]+), ([0-9.]+)\\)" % layer).search(source)
		assert_not_null(found, "%s is in the shader" % layer)
		var luma: float = 0.2126 * float(found.get_string(1)) + 0.7152 * float(found.get_string(2)) + 0.0722 * float(found.get_string(3))
		assert_almost_eq(luma * 100.0, grades[layer], 0.6, "%s has lightness grade %d" % [layer, int(grades[layer])])


func test_only_the_chosen_card_has_a_thin_glow_from_behind_and_a_shadow_and_the_rest_are_black_and_white() -> void:
	var strip: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	strip.size = Vector2(720.0, 700.0)
	add_child(strip)
	var options: Array[Dictionary] = [
		{"id": "online", "title": "Online", "color": "orange", "accent": "ffa63a", "power": 7450},
		{"id": "hotspot", "title": "Local", "color": "blue", "accent": "4aa0ff", "power": 9999},
		{"id": "practice", "title": "Practice", "color": "green", "accent": "45dd78", "power": 1200},
	]
	strip.setup("", options, 1)
	strip._layout()
	for index: int in 3:
		var card: Button = strip.cards[index]
		var face: CardFace = card.get_node("Face") as CardFace
		assert_null(card.get_node_or_null("Frame"), "the glow is drawn by the face behind the picture, not over it")
		assert_eq(face.accent, Color(String(options[index]["accent"])), "the glow is in the mode's colour")
		var picture_material: ShaderMaterial = (face.get_node("Picture") as TextureRect).material as ShaderMaterial
		var chosen: float = 1.0 if index == 1 else 0.0
		assert_eq(face.chosen, chosen, "only the chosen card has its glow and shadow")
		assert_eq(picture_material.get_shader_parameter("dim"), 1.0 - chosen, "a card that is not chosen is only dimmed with a dark tint, the chosen one is not")
		var age: ColorRect = face.get_node("Tint") as ColorRect
		assert_almost_eq(((age.material as ShaderMaterial).get_shader_parameter("tint") as Color).a, CardFace.AGE_TINT.a * chosen, 0.001, "and only the chosen card has the yellowish old tint")
	var sample: CardFace = autofree(CardFace.new()) as CardFace
	assert_false((sample.get_script() as GDScript).get_script_constant_map().has("OUTLINE_WIDTH"), "a glow only: no solid outline")
	assert_lte(CardFace.GLOW_WIDTHS[0] / 2.0, 14.0, "with a spread of about 12 px")
	strip.select(2, false)
	assert_eq((strip.cards[2].get_node("Face") as CardFace).chosen, 1.0, "the glow moves to the card that is chosen")
	assert_eq((strip.cards[1].get_node("Face") as CardFace).chosen, 0.0)


func test_the_main_menu_backdrop_is_backlit_behind_the_chosen_card() -> void:
	var screen: TitleScreen = autofree(load("res://scenes/ui/title_screen.tscn").instantiate()) as TitleScreen
	add_child(screen)
	await wait_frames(12)
	var glow: TextureRect = screen.backdrop.glow
	assert_eq((glow.material as CanvasItemMaterial).blend_mode, CanvasItemMaterial.BLEND_MODE_ADD, "the backlight is added light")
	var card: Rect2 = screen.carousel.cards[screen.carousel.index].get_global_rect()
	assert_almost_eq(glow.get_global_rect().get_center().x, card.get_center().x, 4.0, "its bright middle is behind the card (across)")
	assert_almost_eq(glow.get_global_rect().get_center().y, card.get_center().y, 6.0, "and down")
	assert_gt(glow.size.x, card.size.x, "and it reaches out past the card")
	var gradient: Gradient = (glow.texture as GradientTexture2D).gradient
	assert_gt(gradient.colors[0].a, gradient.colors[gradient.colors.size() - 1].a, "a radial gradient: bright in the middle, gone at the edge")


func test_the_mode_name_leans_back_in_perspective() -> void:
	var screen: TitleScreen = autofree(load("res://scenes/ui/title_screen.tscn").instantiate()) as TitleScreen
	add_child(screen)
	await wait_frames(4)
	var material: ShaderMaterial = screen.mode_name_label.material as ShaderMaterial
	assert_not_null(material, "the name is drawn through the perspective shader")
	assert_eq(material.shader.resource_path, "res://shaders/text_perspective.gdshader")
	assert_lt(float(material.get_shader_parameter("top_scale")), 1.0, "its upper part is pushed back: narrower than its lower part")
	assert_eq(material.get_shader_parameter("rect_size"), screen.mode_name_label.size, "the shader knows the label's size")
