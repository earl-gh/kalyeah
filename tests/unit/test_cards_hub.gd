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
	assert_false(screen.characters_page.is_visible_in_tree(), "the pages wait behind the cards")
	assert_false(screen.weapons_page.is_visible_in_tree())
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
			assert_null(card.material, "no crop or shader over a picture")
			assert_eq(card.get_child_count(), 0, "nothing printed on it")
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


func test_the_fan_sticks_out_above_the_card_and_its_lower_part_sits_inside() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _hub_screen(height)
		var hub: CardsHub = screen.codex_page.hub
		var header: Control = hub.find_child("Header", true, false) as Control
		for i: int in hub.cards.size():
			var card: CardsHubCard = hub.cards[i]
			var panel: Rect2 = card.get_global_rect()
			var fan: Rect2 = _fan_box(card.fan)
			assert_lt(fan.position.y, panel.position.y - 40.0, "the upper part sticks out above the card's top edge (%d, %d)" % [i, int(height)])
			assert_gt(fan.end.y, panel.position.y + panel.size.y * 0.5, "most of the lower part is over the card's panel")
			assert_lt(fan.end.y, panel.end.y, "the fan's bottom is inside the card")
			assert_lte(fan.end.x, panel.end.x, "and inside it on the right")
			assert_gte(fan.position.y, header.get_global_rect().end.y, "the cards that stick out stay under the page header")
			assert_gte(fan.position.x, panel.position.x, "and on the card's side of the screen")
		var first: Rect2 = hub.cards[0].get_global_rect()
		assert_gte(_fan_box(hub.cards[1].fan).position.y, first.end.y, "the second fan sticks out into the gap, never over the first card")


func test_the_titles_sit_at_the_bottom_left_and_clear_of_the_fan() -> void:
	var screen: TitleScreen = await _hub_screen()
	for card: CardsHubCard in screen.codex_page.hub.cards:
		var panel: Rect2 = card.get_global_rect()
		var title: Rect2 = card.title_label.get_global_rect()
		var detail: Rect2 = card.detail_label.get_global_rect()
		assert_gte(title.position.x, panel.position.x + UiMetrics.PADDING - 1.0, "inset from the left edge")
		assert_lte(detail.end.y, panel.end.y - UiMetrics.PADDING + 1.0, "the detail is the bottom row")
		assert_gt(detail.position.y, panel.position.y + panel.size.y * 0.5, "in the lower half")
		var front_corner: Vector2 = card.fan.get_global_rect().position + card.fan.lower_left()
		assert_lt(card.detail_label.get_global_rect().position.x + card.detail_label.get_minimum_size().x, front_corner.x, "the words stop before the front card")
		assert_gte(card.title_label.get_theme_font_size("font_size"), UiMetrics.HEADLINE, "the title is a headline")
		assert_gte(card.detail_label.get_theme_font_size("font_size"), UiMetrics.DETAIL, "the detail is from the type scale")


func test_the_hub_fits_both_phones_and_never_reaches_the_bottom_bar() -> void:
	for height: float in [SHORT_PHONE, TALL_PHONE]:
		var screen: TitleScreen = await _hub_screen(height)
		var hub: CardsHub = screen.codex_page.hub
		var bar_top: float = screen.tab_bar.get_global_rect().position.y
		var page: Rect2 = screen.codex_page.get_global_rect()
		assert_lte(page.end.y, bar_top + 1.0, "the page ends where the bar starts (%d)" % int(height))
		assert_lte(hub.get_combined_minimum_size().y, page.size.y, "the hub needs no more room than the page has")
		var width: float = 720.0
		for card: CardsHubCard in hub.cards:
			var panel: Rect2 = card.get_global_rect()
			assert_gte(panel.position.x, 0.0)
			assert_lte(panel.end.x, width, "inside the screen")
			assert_lt(panel.end.y, bar_top, "above the bottom bar")
			assert_gte(panel.size.y, UiMetrics.MIN_TARGET, "a big target")
			assert_gte(_fan_box(card.fan).position.y, 0.0, "the fan is on screen")
			assert_lte(_fan_box(card.fan).end.x, width)
			var text_box: Rect2 = card.get_node("Words").get_global_rect() as Rect2
			assert_true(panel.encloses(text_box.grow(-1.0)), "the words are inside the card, nothing clipped")
		assert_lt(hub.cards[1].get_global_rect().end.y, page.end.y, "the cards end inside the page")
		# the pair sits in the middle of what the header leaves, not stuck to the header
		var header_end: float = (hub.find_child("Header", true, false) as Control).get_global_rect().end.y
		var gap_above: float = _fan_box(hub.cards[0].fan).position.y - header_end
		var gap_below: float = page.end.y - hub.cards[1].get_global_rect().end.y
		assert_lt(absf(gap_above - gap_below), 1.5, "centred between the header and the bar")


func test_a_tap_on_the_part_of_the_fan_above_the_card_still_counts() -> void:
	var screen: TitleScreen = await _hub_screen()
	var card: CardsHubCard = screen.codex_page.hub.kids_card
	var above: Vector2 = card.fan.position + Vector2(card.fan.size.x * 0.5, 6.0)
	assert_lt(above.y, 0.0, "that spot is above the panel")
	assert_true(card.taps_at(above))
	assert_true(card.taps_at(card.size / 2.0))
	assert_false(card.taps_at(Vector2(4.0, -30.0)), "empty space above the panel is not the card")
	assert_false(card.taps_at(Vector2(card.size.x + 10.0, 10.0)))


func test_a_press_squeezes_the_card_but_not_with_reduce_motion() -> void:
	var screen: TitleScreen = await _hub_screen()
	var card: CardsHubCard = screen.codex_page.hub.kids_card
	assert_true(card.has_meta(&"feels_press"), "the shared press feel is on the card")
	assert_true(Settings.reduced_motion)
	card.button_down.emit()
	await wait_frames(3)
	assert_eq(card.scale, Vector2.ONE, "Reduce motion: no movement")
	card.button_up.emit()


# ---- going in and coming back ------------------------------------------------------------

func test_tapping_kids_opens_the_characters_page_and_skills_the_weapons_page() -> void:
	var screen: TitleScreen = await _hub_screen()
	var codex: CodexPage = screen.codex_page
	codex.hub.kids_card.pressed.emit()
	assert_eq(codex.current, CodexPage.CHARACTERS)
	assert_true(screen.characters_page.is_visible_in_tree())
	assert_false(screen.weapons_page.is_visible_in_tree())
	assert_false(codex.hub.is_visible_in_tree())
	codex.back_button.pressed.emit()
	assert_eq(codex.current, CodexPage.HUB)
	codex.hub.skills_card.pressed.emit()
	assert_eq(codex.current, CodexPage.WEAPONS)
	assert_true(screen.weapons_page.is_visible_in_tree())
	assert_false(screen.characters_page.is_visible_in_tree())
	assert_false(codex.hub.is_visible_in_tree())


func test_the_pages_have_a_back_button_that_is_a_big_target_and_the_hub_does_not() -> void:
	var screen: TitleScreen = await _hub_screen()
	var codex: CodexPage = screen.codex_page
	assert_false(codex.back_button.is_visible_in_tree(), "nothing to go back to from the hub")
	for view: StringName in [CodexPage.CHARACTERS, CodexPage.WEAPONS]:
		codex.show_view(view)
		await wait_frames(2)
		assert_true(codex.back_button.is_visible_in_tree(), "%s has a Back" % view)
		assert_eq(codex.back_button.text, "Back")
		var rect: Rect2 = codex.back_button.get_global_rect()
		assert_gte(rect.size.y, UiMetrics.MIN_TARGET)
		assert_gte(rect.size.x, UiMetrics.MIN_TARGET)
		assert_lt(rect.end.y, screen.tab_bar.get_global_rect().position.y, "not under the bar")
		var page: Control = screen.characters_page if view == CodexPage.CHARACTERS else screen.weapons_page
		assert_gte(page.get_global_rect().position.y, rect.end.y - 1.0, "the page starts under the Back button")
		assert_lte(page.get_global_rect().end.y, screen.tab_bar.get_global_rect().position.y + 1.0, "and ends above the bar")


func test_the_phones_back_goes_one_step_page_then_hub_then_home_then_asks_to_quit() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.kids_card.pressed.emit()
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_frames(2)
	assert_eq(screen.current_page, &"codex", "back from a page stays on the Cards screen")
	assert_eq(screen.codex_page.current, CodexPage.HUB, "and lands on the hub, never straight out")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_frames(2)
	assert_eq(screen.current_page, &"", "back from the hub goes home")
	assert_null(screen.dialog, "no quit question yet")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_frames(2)
	assert_not_null(screen.dialog, "back at home asks to quit")


func test_back_from_the_skills_page_works_the_same() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.skills_card.pressed.emit()
	screen._go_back()
	assert_eq(screen.current_page, &"codex")
	assert_eq(screen.codex_page.current, CodexPage.HUB)
	screen._go_back()
	assert_eq(screen.current_page, &"")


func test_the_cards_tab_inside_a_page_returns_to_the_hub() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.skills_card.pressed.emit()
	assert_eq(screen.codex_page.current, CodexPage.WEAPONS)
	screen.corner_buttons[&"codex"].pressed.emit()
	await wait_frames(2)
	assert_eq(screen.current_page, &"codex")
	assert_eq(screen.codex_page.current, CodexPage.HUB, "the lit Cards tab is the way back to the hub")
	assert_true(screen.codex_page.hub.is_visible_in_tree())
	assert_eq(screen.tab_bar.active, &"codex")


func test_coming_to_cards_from_elsewhere_always_starts_on_the_hub() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.kids_card.pressed.emit()
	screen.corner_buttons[&"home"].pressed.emit()
	await wait_frames(2)
	screen.corner_buttons[&"codex"].pressed.emit()
	await wait_frames(2)
	assert_eq(screen.codex_page.current, CodexPage.HUB, "not left inside the page it was closed on")
	assert_true(screen.codex_page.hub.is_visible_in_tree())


func test_the_detail_sheets_still_open_from_the_pages() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.kids_card.pressed.emit()
	await wait_frames(2)
	screen.characters_page.cards["toni"].pressed.emit()
	assert_not_null(screen.dialog)
	assert_true(screen.dialog.is_open, "a kid's sheet")
	screen.dialog.close(false)
	screen.codex_page.show_view(CodexPage.WEAPONS)
	await wait_frames(2)
	screen.weapons_page.tiles[0].pressed.emit()
	var sheet: MenuDialog = screen.dialog
	assert_true(sheet.is_open, "a skill's sheet")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_frames(2)
	assert_true(not is_instance_valid(sheet) or not sheet.is_open, "back closes the sheet first")
	assert_eq(screen.codex_page.current, CodexPage.WEAPONS, "and the page is still there")


func test_the_pages_still_scroll_by_dragging_inside_the_hubs_page() -> void:
	var screen: TitleScreen = await _hub_screen()
	screen.codex_page.hub.kids_card.pressed.emit()
	await wait_frames(2)
	var scroll: ScrollContainer = screen.characters_page.get_node("Scroll") as ScrollContainer
	assert_gt(scroll.get_v_scroll_bar().max_value, scroll.size.y, "the grid is taller than its window, so it scrolls")
	for card: Button in screen.characters_page.cards.values():
		assert_eq(card.mouse_filter, Control.MOUSE_FILTER_PASS, "a drag that starts on a card scrolls the list")
