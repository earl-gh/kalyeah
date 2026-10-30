extends GutTest
## The Cards screen of the main menu: a hub of two wide cards (Kids, Skills), each with a fan of three bare card pictures sticking out
## of its top edge, and the two pages behind them with a Back to the hub. These tests pin facts (positions, sizes, visibility, the order
## Back goes in), not pixels.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const RULES_PATH: String = "res://data/rules/game_rules.tres"
## 720 x 1280 is the design phone; 390 x 844 is the same width on a taller phone (720 x 1558 in UI pixels).
const SHORT_PHONE: float = 1280.0
const TALL_PHONE: float = 1558.0

var _saved_motion: bool


func before_each() -> void:
	_saved_motion = Settings.reduced_motion


func after_each() -> void:
	Settings.set_reduced_motion(_saved_motion)


func _phone(height: float = SHORT_PHONE) -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, height)
	phone.size = Vector2(720.0, height)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _hub_screen(height: float = SHORT_PHONE) -> TitleScreen:
	var screen: TitleScreen = _phone(height)
	# the page slides in over a quarter of a second: measured with the slide off, the page is where it ends up
	Settings.set_reduced_motion(true)
	screen.open_page(&"codex")
	await wait_frames(3)
	return screen


## The box a fan card really covers on screen (turned), in global coordinates.
func _card_box(fan: CardFan, index: int) -> Rect2:
	var box: Rect2 = fan.card_box(index)
	return Rect2(fan.get_global_rect().position + box.position, box.size)


func _fan_box(fan: CardFan) -> Rect2:
	var box: Rect2 = _card_box(fan, 0)
	for i: int in range(1, fan.cards.size()):
		box = box.merge(_card_box(fan, i))
	return box


# ---- what the hub shows -----------------------------------------------------------------

func test_the_hub_is_two_wide_cards_titled_kids_and_skills_each_with_a_detail_line() -> void:
	var screen: TitleScreen = await _hub_screen()
	var hub: CardsHub = screen.codex_page.hub
	assert_true(hub.is_visible_in_tree(), "Cards opens on the hub")
	assert_eq(screen.codex_page.current, CodexPage.HUB)
	assert_eq(hub.cards.size(), 2, "exactly two cards")
	var titles: Array[String] = []
	for card: CardsHubCard in hub.cards:
		titles.append(card.title_label.text)
		assert_false(card.detail_label.text.is_empty(), "%s has a detail line" % card.title_label.text)
		assert_lt(card.title_label.get_global_rect().position.y, card.detail_label.get_global_rect().position.y, "row 1 is the title, row 2 the detail")
		assert_eq(card.detail_label.get_line_count(), 1, "the detail is one row")
	assert_eq(titles, ["Kids", "Skills"] as Array[String])
	var first: Rect2 = hub.cards[0].get_global_rect()
	var second: Rect2 = hub.cards[1].get_global_rect()
	assert_lt(first.end.y, second.position.y, "one above the other")
	assert_almost_eq(first.size.x, second.size.x, 1.0, "the same width")
	assert_gt(first.size.x, first.size.y * 2.5, "wide, not tall")
	assert_true(screen.codex_page.kids_screen.is_visible_in_tree(), "the kid grid is inline")
	assert_true(screen.codex_page.skills_screen.is_visible_in_tree())
	for node: Node in hub.find_children("*", "", true, false):
		assert_false(node is UiSegmented, "the tab switcher is gone from the hub")


func test_the_counts_in_the_detail_lines_come_from_the_data() -> void:
	var screen: TitleScreen = await _hub_screen()
	var rules: GameRules = load(RULES_PATH) as GameRules
	var hub: CardsHub = screen.codex_page.hub
	assert_eq(hub.kids_card.detail_label.text, CardsHub.kids_detail(Session.AVATARS.size()))
	assert_eq(hub.skills_card.detail_label.text, CardsHub.skills_detail(rules.weapons.size()))
	assert_eq(CardsHub.kids_detail(6), "Meet the six kids")
	assert_eq(CardsHub.kids_detail(5), "Meet the five kids", "a count in words, so a new kid changes the line without an edit")
	assert_eq(CardsHub.kids_detail(40), "Meet the 40 kids", "a count with no word is shown as a number")
	assert_eq(CardsHub.skills_detail(14), "14 skills to throw")
	assert_string_contains(hub.skills_card.detail_label.text, str(rules.weapons.size()))


func test_each_fan_is_three_bare_pictures_of_the_owners_cards() -> void:
	var screen: TitleScreen = await _hub_screen()
	var hub: CardsHub = screen.codex_page.hub
	var kid_fan: CardFan = hub.kids_card.fan
	var skill_fan: CardFan = hub.skills_card.fan
	assert_eq(kid_fan.cards.size(), 3)
	assert_eq(skill_fan.cards.size(), 3)
	for i: int in 3:
		assert_eq(kid_fan.cards[i].texture, CharactersPage.card_picture(CardsHub.KIDS_FAN[i]), "kid fan card %d is that kid's card picture" % i)
		assert_eq(skill_fan.cards[i].texture, CardStyle.picture(CardsHub.SKILLS_FAN[i]), "skill fan card %d is that skill's card picture" % i)
	for fan: CardFan in [kid_fan, skill_fan]:
		for card: TextureRect in fan.cards:
			assert_not_null(card.texture)
			assert_eq(card.material, fan.mask_material, "nothing over a picture but the pocket mask")
			assert_eq(card.get_child_count(), 0, "no plate or crop behind or over it")
			assert_eq(card.mouse_filter, Control.MOUSE_FILTER_IGNORE, "a picture never takes the touch from the button")
		var aspect: float = float(fan.cards[0].texture.get_width()) / float(fan.cards[0].texture.get_height())
		assert_almost_eq(fan.cards[0].size.x / fan.cards[0].size.y, aspect, 0.01, "a picture keeps its own shape")
	for id: StringName in CardsHub.KIDS_FAN:
		assert_true(Session.AVATARS.has(String(id)), "%s is a real kid" % id)
	var ids: Array[StringName] = []
	for weapon: WeaponDef in (load(RULES_PATH) as GameRules).weapons:
		ids.append(weapon.id)
	for id: StringName in CardsHub.SKILLS_FAN:
		assert_true(ids.has(id), "%s is a real skill" % id)


func test_the_fan_is_drawn_like_the_cards_icon_front_card_last_tilted_left_the_others_peeking_right() -> void:
	var screen: TitleScreen = await _hub_screen()
	for card: CardsHubCard in screen.codex_page.hub.cards:
		var fan: CardFan = card.fan
		assert_eq(fan.get_child(fan.get_child_count() - 1), fan.cards[0], "the front card is the last child, so it is drawn on top")
		assert_lt(fan.cards[0].rotation_degrees, -10.0, "the front card leans left")
		assert_gt(fan.cards[2].rotation_degrees, 10.0, "the back card leans the other way")
		var centres: Array[float] = []
		for i: int in 3:
			centres.append(_card_box(fan, i).get_center().x)
		assert_lt(centres[0], centres[1], "each card peeks out a little further to the right")
		assert_lt(centres[1], centres[2])
		assert_gt(_card_box(fan, 0).intersection(_card_box(fan, 1)).get_area(), 0.0, "the cards overlap")
		assert_gt(_card_box(fan, 1).intersection(_card_box(fan, 2)).get_area(), 0.0)
		assert_true(fan.get_global_rect().encloses(_fan_box(fan).grow(-0.5)), "the fan's own box is exactly what the cards cover")


func test_the_fan_sticks_out_above_the_card_and_is_tucked_into_it_below() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _hub_screen(height)
		var hub: CardsHub = screen.codex_page.hub
		var header: Control = hub.find_child("Header", true, false) as Control
		for i: int in hub.cards.size():
			var card: CardsHubCard = hub.cards[i]
			var panel: Rect2 = card.get_global_rect()
			var fan: Rect2 = _fan_box(card.fan)
			var out: float = panel.position.y - fan.position.y
			assert_gte(out, 56.0 - card.fan_drop, "the upper part sticks out above the card's top edge (%d, %d)" % [i, int(height)])
			assert_lte(out, 80.0 - card.fan_drop, "by about 60 to 70 px: the rest is in the card")
			assert_gt(fan.end.y, panel.position.y + panel.size.y * 0.9, "the fan fills the category height")
			assert_gt(panel.end.y - maxf(fan.position.y, panel.position.y), panel.size.y * 0.9, "so the fan covers the panel's whole height")
			assert_lte(fan.end.x, 720.0, "the shifted fan remains on screen")
			assert_gte(fan.position.y, header.get_global_rect().end.y, "the cards that stick out stay under the page header")
			assert_gte(fan.position.x, panel.position.x, "and on the card's side of the screen")
		var first: Rect2 = hub.cards[0].get_global_rect()
		assert_gte(_fan_box(hub.cards[1].fan).position.y, first.end.y, "the second fan sticks out into the gap, never over the first card")


func test_the_fan_is_masked_by_the_cards_own_rounded_shape_and_the_mask_follows_the_card() -> void:
	var screen: TitleScreen = await _hub_screen()
	var card: CardsHubCard = screen.codex_page.hub.kids_card
	var material: ShaderMaterial = card.fan.mask_material
	assert_not_null(material)
	assert_eq(material.shader.resource_path, CardFan.MASK_SHADER)
	for picture: TextureRect in card.fan.cards:
		assert_eq(picture.material, material, "every card of the fan is cut by the same mask")
	assert_eq(material.get_shader_parameter("card_size"), card.size, "the mask is the card's own size")
	assert_eq(material.get_shader_parameter("radius"), float(KalyeahTheme.MODAL_FRAME_RADIUS), "and its own corner radius")
	assert_eq(material.get_shader_parameter("inset"), float(card.panel_box.get_border_width(SIDE_LEFT)), "stopping at the border's inner edge")
	var to_card: Projection = material.get_shader_parameter("to_card") as Projection
	var corner: Vector3 = Vector3(card.get_global_rect().position.x, card.get_global_rect().position.y, 0.0)
	var local: Vector4 = to_card * Vector4(corner.x, corner.y, 0.0, 1.0)
	assert_almost_eq(local.x, 0.0, 0.01, "the card's top left maps to (0, 0)")
	assert_almost_eq(local.y, 0.0, 0.01)
	# the page slides, a press squeezes: the mask is told, with no pop
	var page: Control = screen.codex_page
	page.position += Vector2(40.0, 25.0)
	await wait_frames(2)
	to_card = material.get_shader_parameter("to_card") as Projection
	local = to_card * Vector4(corner.x + 40.0, corner.y + 25.0, 0.0, 1.0)
	assert_almost_eq(local.x, 0.0, 0.01, "moving the page moves the mask with it")
	assert_almost_eq(local.y, 0.0, 0.01)
	card.scale = Vector2(0.9, 0.9)
	await wait_frames(2)
	to_card = material.get_shader_parameter("to_card") as Projection
	var inside: Vector4 = to_card * Vector4(card.get_global_rect().get_center().x, card.get_global_rect().get_center().y, 0.0, 1.0)
	assert_almost_eq(inside.x, card.size.x / 2.0, 1.0, "a squeezed card keeps its mask in its own pixels")


func test_the_hub_cards_are_blue_in_the_modals_frame_shape() -> void:
	var screen: TitleScreen = await _hub_screen()
	for card: CardsHubCard in screen.codex_page.hub.cards:
		var box: StyleBoxFlat = card.get_theme_stylebox("normal") as StyleBoxFlat
		assert_eq(box.bg_color, CardsHubCard.PANEL_COLOR, "bright blue, not grey")
		assert_gt(box.bg_color.b, box.bg_color.r + 0.3, "clearly blue")
		assert_eq(box.border_color, CardsHubCard.PANEL_EDGE)
		assert_eq(box.corner_radius_top_left, KalyeahTheme.MODAL_FRAME_RADIUS)
		assert_gt(box.shadow_size, 0, "a shadow, so it reads against the blue backdrop")
		assert_gte(box.get_border_width(SIDE_LEFT), 2, "and a border")
		var lit: StyleBoxFlat = card.get_theme_stylebox("pressed") as StyleBoxFlat
		assert_ne(lit.bg_color, box.bg_color, "a press shows")
		assert_gte(UiKit.contrast(card.title_label.get_theme_color("font_color"), box.bg_color), 4.5, "the title reads on the frame")
		assert_gte(UiKit.contrast(card.detail_label.get_theme_color("font_color"), box.bg_color), 4.5, "and the detail line")


func test_the_collection_scrolls_inside_the_page_on_both_phones() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _hub_screen(height)
		var hub: CardsHub = screen.codex_page.hub
		assert_lte(hub.get_global_rect().end.y, screen.tab_bar.get_global_rect().position.y)
		assert_gt(hub.scroll.get_v_scroll_bar().max_value, hub.scroll.size.y)
		hub.scroll.scroll_vertical = 100000
		await wait_frames(3)
		assert_gt(hub.scroll.scroll_vertical, 0)
		assert_lte(screen.codex_page.skills_screen.cells[-1].get_global_rect().end.y, hub.scroll.get_global_rect().end.y + 1.0)
		for card: CardsHubCard in hub.cards:
			assert_eq(card.mouse_filter, Control.MOUSE_FILTER_IGNORE, "category banners do not open pages")
		assert_almost_eq(hub.kids_card.fan.cards[0].size.x, hub.skills_card.fan.cards[0].size.x, 1.0, "both fans share the kid card's visual width")


func test_a_tap_on_the_part_of_the_fan_above_the_card_still_counts() -> void:
	var screen: TitleScreen = await _hub_screen()
	var card: CardsHubCard = screen.codex_page.hub.kids_card
	var above: Vector2 = card.fan.position + Vector2(card.fan.size.x * 0.5, 6.0)
	assert_lt(above.y, 0.0, "that spot is above the panel")
	assert_true(card.taps_at(above))
	assert_true(card.taps_at(card.size / 2.0))
	assert_false(card.taps_at(Vector2(4.0, -30.0)), "empty space above the panel is not the card")
	assert_false(card.taps_at(Vector2(card.size.x + 64.0, 10.0)))


func test_a_press_squeezes_the_card_but_not_with_reduce_motion() -> void:
	var screen: TitleScreen = await _hub_screen()
	var card: CardsHubCard = screen.codex_page.hub.kids_card
	assert_true(card.has_meta(&"feels_press"), "the shared press feel is on the card")
	assert_true(Settings.reduced_motion)
	card.button_down.emit()
	await wait_frames(3)
	assert_eq(card.scale, Vector2.ONE, "Reduce motion: no movement")
	card.button_up.emit()
