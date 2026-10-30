extends GutTest
## The kid pick at the start of a match is a carousel of the owner's six character card pictures: five cards in view, the chosen kid's card
## in the middle, bare, two kids on each side stepping down in size and brightness, one step a swipe, endless; the kid's name and a short
## description sit under the carousel. A taken kid stays in the ring with a "Taken" tag and is stepped over. Facts about positions, sizes,
## nodes and signals, not pixels.

const PHONES: Array[Vector2] = [Vector2(720.0, 1280.0), Vector2(720.0, 1558.0), Vector2(390.0, 844.0)]

var _rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
var _motion: bool
var _changed: Array[StringName] = []


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_changed.clear()


func after_each() -> void:
	Settings.reduced_motion = _motion


func _open(size: Vector2 = PHONES[0], current: StringName = &"toni", taken: Array[StringName] = [], cards_mode: bool = true) -> WeaponPickScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = size
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = size
	screen.cards_mode = cards_mode
	var ids: Array[StringName] = []
	for weapon: WeaponDef in _rules.weapons:
		ids.append(weapon.id)
	screen.open(WeaponPick.new(ids, 10.0), _rules.weapons, "", RandomNumberGenerator.new(), _rules.characters, current, true)
	screen.set_taken(taken)
	screen.character_changed.connect(func(id: StringName) -> void: _changed.append(id))
	await wait_process_frames(3)
	return screen


## A finger down in the middle of the carousel, dragged `dx` pixels sideways and lifted.
func _swipe(screen: WeaponPickScreen, dx: float) -> void:
	var strip: KidCarousel = screen._carousel
	var from: Vector2 = strip.size / 2.0
	strip._gui_input(TouchHelper.click(from, true))
	strip._gui_input(TouchHelper.motion(from + Vector2(dx, 0.0)))
	strip._gui_input(TouchHelper.click(from + Vector2(dx, 0.0), false))


## One step's worth of finger travel, a little more than the threshold.
func _step(screen: WeaponPickScreen) -> float:
	return MenuCarousel.STEP * screen._carousel.spread_scale * 0.6


func _tap(screen: WeaponPickScreen, at: Vector2) -> void:
	screen._carousel._gui_input(TouchHelper.click(at, true))
	screen._carousel._gui_input(TouchHelper.click(at, false))


func _ids(screen: WeaponPickScreen) -> Array[StringName]:
	var found: Array[StringName] = []
	for character: CharacterDef in screen._characters:
		found.append(character.id)
	return found


## Where a card really is on the screen (a card behind the chosen one is scaled about its middle).
func _card_rect(strip: MenuCarousel, card: Button) -> Rect2:
	var half: Vector2 = strip.card_size * card.scale / 2.0
	var middle: Vector2 = strip.get_global_rect().position + card.position + strip.card_size / 2.0
	return Rect2(middle - half, half * 2.0)


func test_the_carousel_has_the_six_card_pictures_bare() -> void:
	var screen: WeaponPickScreen = await _open()
	var strip: KidCarousel = screen._carousel
	assert_true(strip.visible)
	assert_eq(strip.cards.size(), 6, "one card for each kid")
	assert_eq(strip.cards.size(), Session.AVATARS.size())
	for i: int in strip.cards.size():
		var id: StringName = strip.ids[i]
		var picture: TextureRect = strip.cards[i].get_node("Picture") as TextureRect
		assert_eq(picture.texture, CharactersPage.card_picture(id), "%s shows its own card picture" % id)
		assert_null(picture.material, "%s: no crop over it" % id)
		assert_eq(picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_CENTERED, "%s: never stretched" % id)
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			assert_true(strip.cards[i].get_theme_stylebox(state) is StyleBoxEmpty, "%s: no plate behind it (%s)" % [id, state])
		assert_almost_eq(strip.card_size.x / strip.card_size.y, CharactersPage.CARD.x / CharactersPage.CARD.y, 0.01, "the picture's own shape")
	assert_false(screen._character_row.visible, "the row of small tiles is gone in cards mode")


func test_the_kid_you_last_played_starts_in_the_middle() -> void:
	for id: StringName in [&"junjun", &"popoy", &"inday"]:
		var screen: WeaponPickScreen = await _open(PHONES[0], id)
		var strip: KidCarousel = screen._carousel
		assert_eq(strip.current_id(), id)
		assert_eq(screen.character_id, id)
		var chosen: Button = strip.cards[strip.index]
		var rect: Rect2 = _card_rect(strip, chosen)
		assert_almost_eq(rect.get_center().x, strip.get_global_rect().get_center().x, 1.0, "%s: centred" % id)
		assert_eq(chosen.scale, Vector2.ONE, "full size")
		for card: Button in strip.cards:
			if card != chosen:
				assert_gt(chosen.z_index, card.z_index, "the others are behind it")
				assert_lt(card.scale.x, 1.0, "and smaller")
				assert_lt(card.modulate.r, 1.0, "and darker")
		assert_eq(screen._hero_name.text, screen.hero_name(), "its name is under it")


func test_five_cards_are_in_view_and_step_down_from_the_middle() -> void:
	var screen: WeaponPickScreen = await _open()
	var strip: KidCarousel = screen._carousel
	assert_eq(KidCarousel.VISIBLE, 5)
	var chosen: Button = strip.cards[strip.index]
	var shown: int = 0
	for card: Button in strip.cards:
		shown += 1 if card.visible else 0
	assert_eq(shown, 5, "five of the six kids are in view")
	for offset: int in [-1, 1]:
		var next: Button = strip.cards[posmod(strip.index + offset, 6)]
		assert_almost_eq(next.scale.x, KidCarousel.LEVEL_SCALE[1], 0.01, "the kids beside the chosen one")
		assert_true(_card_rect(strip, next).intersects(_card_rect(strip, chosen)), "tucked a little behind the chosen card")
	for offset: int in [-2, 2]:
		var outer: Button = strip.cards[posmod(strip.index + offset, 6)]
		var inner: Button = strip.cards[posmod(strip.index + signi(offset), 6)]
		assert_true(outer.visible)
		assert_almost_eq(outer.scale.x, KidCarousel.LEVEL_SCALE[2], 0.01, "the outer two are smaller still")
		assert_almost_eq(outer.modulate.r, 1.0 - MenuCarousel.SHADE_LOSS, 0.01, "dimmed like the mode models on the Play tab")
		assert_almost_eq(inner.modulate.r, outer.modulate.r, 0.01, "one dim for every card behind the chosen one")
		assert_almost_eq(inner.modulate.a, MenuCarousel.BACK_ALPHA, 0.01, "and a little see-through, the same")
		assert_lt(outer.z_index, inner.z_index, "and further back")
		assert_gt(absf(_card_rect(strip, outer).get_center().x - chosen.get_rect().get_center().x), absf(_card_rect(strip, inner).get_center().x - chosen.get_rect().get_center().x), "and further out")
	var far: Button = strip.cards[posmod(strip.index + 3, 6)]
	assert_false(far.visible, "the sixth kid waits out of sight")
	assert_true(strip.wrap, "endless")


func test_the_cards_are_small_enough_for_five() -> void:
	for phone: Vector2 in PHONES:
		var screen: WeaponPickScreen = await _open(phone)
		var strip: KidCarousel = screen._carousel
		var chosen: Rect2 = _card_rect(strip, strip.cards[strip.index])
		assert_lt(chosen.size.x, 0.32 * phone.x, "%s: the chosen card is a fraction of the width, not the 420 px it was" % phone)
		assert_gt(chosen.size.x, 0.2 * phone.x, "%s: but still big enough to read" % phone)
		var left: Rect2 = _card_rect(strip, strip.cards[posmod(strip.index - 2, 6)])
		var right: Rect2 = _card_rect(strip, strip.cards[posmod(strip.index + 2, 6)])
		assert_gte(left.position.x, strip.get_global_rect().position.x - 0.5, "%s: the outer left card is inside" % phone)
		assert_lte(right.end.x, strip.get_global_rect().end.x + 0.5, "%s: and the outer right one" % phone)


func test_the_name_and_description_are_under_the_chosen_card_like_the_cards_drawer() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var kid: CharacterDef = null
	for character: CharacterDef in screen._characters:
		if character.id == &"toni":
			kid = character
	assert_eq(screen._hero_name.text, kid.display_name)
	assert_eq(screen._hero_about.text, kid.description, "the same short description the drawer shows")
	assert_false(screen._hero_about.text.is_empty())
	assert_true(screen._hero_about.is_visible_in_tree())
	assert_eq(screen._hero_about.get_theme_font_size("font_size"), UiMetrics.BODY, "the drawer's body text")
	assert_true(screen._hero_about.get_parent() is PanelContainer, "in the drawer's light body panel")
	var about: Rect2 = screen._hero_about.get_global_rect()
	assert_lte(screen._carousel.get_global_rect().end.y, screen._hero_name.get_global_rect().position.y + 1.0, "the name is under the cards")
	assert_lte(screen._hero_name.get_global_rect().end.y, about.position.y + 1.0, "and the description under the name")
	assert_lte(about.end.y, screen._ready_button.get_global_rect().position.y, "above Play")
	var other: StringName = &"ligaya"
	screen.pick_character(other)
	for character: CharacterDef in screen._characters:
		if character.id == other:
			assert_eq(screen._hero_about.text, character.description, "the description follows the pick")
	assert_eq(screen.hero_about(), screen._hero_about.text)


func test_the_cards_do_not_move_when_a_longer_description_comes() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var y: float = screen._carousel.get_global_rect().position.y
	var panel: float = screen._hero_about.get_parent().size.y
	for character: CharacterDef in screen._characters:
		screen.pick_character(character.id) if not screen.is_taken(character.id) else null
		await wait_process_frames(2)
		assert_almost_eq(screen._carousel.get_global_rect().position.y, y, 0.5, "%s: the cards stay put" % character.id)
		assert_almost_eq((screen._hero_about.get_parent() as Control).size.y, panel, 0.5, "%s: and so does the panel" % character.id)


func test_a_swipe_moves_one_kid_and_that_kid_is_the_pick() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var ids: Array[StringName] = _ids(screen)
	var at: int = ids.find(&"toni")
	_swipe(screen, -_step(screen))
	assert_eq(screen.character_id, ids[at + 1], "a swipe left brings the next kid")
	assert_eq(screen._carousel.current_id(), ids[at + 1])
	assert_eq(_changed, [ids[at + 1]] as Array[StringName], "character_changed fired once")
	await wait_seconds(0.5)
	_swipe(screen, _step(screen))
	assert_eq(screen.character_id, ids[at], "and a swipe right goes back")
	assert_eq(_changed, [ids[at + 1], ids[at]] as Array[StringName])
	assert_eq(screen._hero_name.text, screen.hero_name(), "the name follows")


func test_a_very_long_swipe_is_still_one_step_and_a_tiny_one_none() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var ids: Array[StringName] = _ids(screen)
	_swipe(screen, -900.0)
	assert_eq(screen.character_id, ids[ids.find(&"toni") + 1], "one step however far the finger goes")
	await wait_seconds(0.5)
	_swipe(screen, 6.0)
	assert_eq(_changed.size(), 1, "a touch that barely moves is a tap on the middle card")


func test_the_carousel_is_endless() -> void:
	var first: WeaponPickScreen = await _open(PHONES[0], _rules.characters[0].id)
	var ids: Array[StringName] = _ids(first)
	_swipe(first, _step(first))
	assert_eq(first.character_id, ids[ids.size() - 1], "right of the first is the last")
	var last: WeaponPickScreen = await _open(PHONES[0], ids[ids.size() - 1])
	_swipe(last, -_step(last))
	assert_eq(last.character_id, ids[0], "left of the last is the first")
	await wait_seconds(0.6)
	assert_eq(last._carousel._position, 0.0, "the position is the plain index again once it settles")


func test_tapping_a_neighbour_brings_it_to_the_middle() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var strip: KidCarousel = screen._carousel
	var ids: Array[StringName] = _ids(screen)
	var at: int = ids.find(&"toni")
	var out: float = strip.card_size.x * 0.65
	_tap(screen, Vector2(strip.size.x / 2.0 - out, strip.size.y / 2.0))
	assert_eq(screen.character_id, ids[at - 1], "the card on the left")
	assert_eq(_changed, [ids[at - 1]] as Array[StringName])
	await wait_seconds(0.5)
	_tap(screen, Vector2(strip.size.x / 2.0 + out, strip.size.y / 2.0))
	assert_eq(screen.character_id, ids[at], "and back from the right")
	await wait_seconds(0.5)
	_tap(screen, strip.size / 2.0)
	assert_eq(_changed.size(), 2, "a tap on the middle card changes nothing")


func test_a_pick_made_in_code_moves_the_carousel_too() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	screen.pick_character(&"ligaya")
	assert_eq(screen._carousel.current_id(), &"ligaya")
	assert_eq(_changed, [&"ligaya"] as Array[StringName], "once, not twice")


func test_a_practice_match_opens_on_the_carousel_with_the_kid_you_played_last() -> void:
	var was: bool = _rules.card_cycle
	var avatar: String = Session.avatar_id
	_rules.card_cycle = true
	Session.avatar_id = "popoy"
	var practice: PracticeMatch = autofree((load("res://scenes/match/practice.tscn") as PackedScene).instantiate()) as PracticeMatch
	add_child(practice)
	var screen: WeaponPickScreen = practice.get_node("%PickScreen") as WeaponPickScreen
	_rules.card_cycle = was
	Session.avatar_id = avatar
	assert_true(screen.cards_mode)
	assert_true(screen._carousel.visible)
	assert_eq(screen._carousel.cards.size(), 6)
	assert_eq(screen._carousel.current_id(), &"popoy")
	_swipe(screen, -_step(screen))
	assert_ne(screen.character_id, &"popoy")
	screen.press_ready()
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].character_id, screen.character_id, "the swiped kid is the kid you play")
	Session.avatar_id = avatar  # Play saves the kid as your face on the main screen: put it back
	Session.save()


# ---- a kid a teammate has taken ----

func test_a_taken_kid_is_dimmed_and_tagged_and_the_others_are_not() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [&"migo", &"popoy"] as Array[StringName])
	var strip: KidCarousel = screen._carousel
	for i: int in strip.cards.size():
		var id: StringName = strip.ids[i]
		var taken: bool = id == &"migo" or id == &"popoy"
		var tag: Label = strip.cards[i].get_node("Taken") as Label
		assert_eq(tag.visible, taken, "%s: the tag shows only when taken" % id)
		assert_eq(tag.text, "Taken")
		assert_gte(tag.get_theme_font_size("font_size"), UiMetrics.CAPTION, "readable")
		var picture: TextureRect = strip.cards[i].get_node("Picture") as TextureRect
		assert_eq(picture.modulate.r < 0.6, taken, "%s: dark only when taken" % id)
		assert_eq(strip.cards[i].accessibility_name.contains("taken"), taken, "%s: a screen reader says so" % id)
		assert_eq(strip.is_blocked(id), taken)
		assert_eq(screen.is_taken(id), taken)


func test_the_taken_tag_sits_in_the_middle_of_its_card_where_it_shows() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [&"migo", &"popoy"] as Array[StringName])
	var strip: KidCarousel = screen._carousel
	var chosen: Rect2 = _card_rect(strip, strip.cards[strip.index])
	for id: StringName in [&"migo", &"popoy"]:
		var card: Button = strip.cards[strip.ids.find(id)]
		var tag: Label = card.get_node("Taken") as Label
		assert_true(Rect2(Vector2.ZERO, card.size).encloses(Rect2(tag.position, tag.size)), "%s: the tag is on its card" % id)
		var middle: Vector2 = card.get_global_transform() * (tag.position + tag.size / 2.0)
		assert_gt(absf(middle.x - chosen.get_center().x), chosen.size.x / 2.0, "%s: the tag is not under the chosen card" % id)
	var own: Label = strip.cards[strip.index].get_node("Taken") as Label
	assert_false(own.visible, "the chosen kid is never taken")


func test_a_swipe_steps_over_taken_kids() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [&"popoy", &"inday"] as Array[StringName])
	var ids: Array[StringName] = _ids(screen)
	_swipe(screen, -_step(screen))
	assert_eq(screen.character_id, ids[0], "left of toni: popoy and inday are taken, so the next free kid is the first (endless)")
	assert_eq(_changed, [ids[0]] as Array[StringName], "one change, to the free kid")
	assert_false(screen.is_taken(screen.character_id))
	await wait_seconds(0.8)
	assert_eq(screen._carousel._position, 0.0, "it settled on the free kid")
	_swipe(screen, _step(screen))
	assert_eq(screen.character_id, ids[ids.find(&"toni")], "and back right it steps over them again to toni")


func test_a_swipe_right_steps_over_taken_kids_the_other_way() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [&"migo"] as Array[StringName])
	var ids: Array[StringName] = _ids(screen)
	_swipe(screen, _step(screen))
	assert_eq(screen.character_id, ids[ids.find(&"migo") - 1], "right of toni is migo (taken): the kid before it")
	assert_ne(screen.character_id, &"migo")


func test_a_tap_on_a_taken_card_is_refused() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [&"popoy"] as Array[StringName])
	var strip: KidCarousel = screen._carousel
	_tap(screen, Vector2(strip.size.x / 2.0 + strip.card_size.x * 0.65, strip.size.y / 2.0))
	assert_eq(screen.character_id, &"toni", "toni stays")
	assert_eq(strip.current_id(), &"toni")
	assert_true(_changed.is_empty(), "nothing changed, nothing emitted")
	screen.pick_character(&"popoy")
	assert_eq(screen.character_id, &"toni", "and the code cannot pick it either")


func test_a_swipe_with_every_other_kid_taken_stays_put() -> void:
	var taken: Array[StringName] = []
	for id: StringName in _ids(await _open()):
		if id != &"toni":
			taken.append(id)
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", taken)
	_swipe(screen, -_step(screen))
	assert_eq(screen.character_id, &"toni")
	assert_true(_changed.is_empty())
	await wait_seconds(0.5)
	assert_eq(screen._carousel._position, float(screen._carousel.index), "the cards came back to rest")


func test_when_your_kid_becomes_taken_the_next_free_one_is_chosen() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var ids: Array[StringName] = _ids(screen)
	screen.set_taken([&"toni", &"popoy"] as Array[StringName])
	assert_eq(screen.character_id, ids[ids.find(&"popoy") + 1], "toni and popoy are taken: the next free kid after toni")
	assert_eq(screen._carousel.current_id(), screen.character_id, "the carousel follows")
	screen.set_taken([] as Array[StringName])
	for card: Button in screen._carousel.cards:
		assert_false((card.get_node("Taken") as Label).visible, "no tags once nobody is taken")
	assert_true(screen._carousel.blocked.is_empty())


# ---- motion ----

func test_a_step_slides_but_reduce_motion_jumps() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	_swipe(screen, -_step(screen))
	assert_ne(screen._carousel._position, float(screen._carousel.index), "it is still sliding")
	await wait_seconds(0.6)
	assert_eq(screen._carousel._position, float(screen._carousel.index), "and settles")
	Settings.reduced_motion = true
	_swipe(screen, -_step(screen))
	assert_eq(screen._carousel._position, float(screen._carousel.index), "with Reduce motion the cards are there at once")
	assert_true(screen._carousel._tween == null or not screen._carousel._tween.is_running(), "no tween runs")


func test_reduce_motion_stops_the_bob() -> void:
	Settings.reduced_motion = true
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var chosen: Button = screen._carousel.cards[screen._carousel.index]
	var y: float = chosen.position.y
	await wait_seconds(0.5)
	assert_eq(chosen.position.y, y, "the middle card holds still")
	Settings.reduced_motion = false
	var moving: bool = false
	for i: int in 20:
		await wait_process_frames(5)
		moving = moving or not is_equal_approx(chosen.position.y, y)
	assert_true(moving, "and floats a little otherwise")


# ---- what the screen still does ----

func test_play_still_starts_with_the_swiped_kid() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni")
	var picked: Array = []
	screen.confirmed.connect(func(picks: Array[StringName]) -> void: picked.append(picks))
	_swipe(screen, -_step(screen))
	var expected: StringName = screen.character_id
	assert_eq(screen._ready_button.text, "Play")
	assert_false(screen._ready_button.disabled)
	screen._ready_button.pressed.emit()
	assert_eq(picked.size(), 1, "Play closes the pick")
	assert_eq(screen.character_id, expected, "with the kid the carousel showed")
	assert_false(screen.visible)


func test_the_heading_and_back_are_unchanged() -> void:
	var screen: WeaponPickScreen = await _open()
	assert_eq(screen._title.text, "Choose your kid")
	assert_true(screen.back_button.is_visible_in_tree())
	assert_gte(screen.back_button.size.y, UiMetrics.MIN_TARGET)


func test_the_weapons_screen_keeps_its_row_of_kid_tiles() -> void:
	var screen: WeaponPickScreen = await _open(PHONES[0], &"toni", [] as Array[StringName], false)
	assert_false(screen._carousel.visible, "no carousel in weapons mode")
	assert_true(screen._character_row.visible)
	assert_eq(screen._profiles.size(), 6)
	assert_true(screen.find_child("Weapons", true, false).is_visible_in_tree(), "the weapon grid is there")
	var tops: Dictionary = {}
	for tile: Button in screen._profiles.values():
		tops[snappedf(tile.global_position.y, 1.0)] = true
		assert_almost_eq(tile.custom_minimum_size.x / tile.custom_minimum_size.y, 0.75, 0.001, "3:4")
	assert_eq(tops.size(), 1, "one row")
	screen.pick_character(&"ligaya")
	assert_true(screen._profiles[&"ligaya"].button_pressed)
	assert_eq(_changed, [&"ligaya"] as Array[StringName])
	screen.set_taken([&"inday"] as Array[StringName])
	assert_true(screen._profiles[&"inday"].disabled, "a teammate's kid is greyed out as before")


func test_a_kid_without_a_picture_keeps_the_face_plate() -> void:
	var made: CharacterDef = (_rules.characters[0] as CharacterDef).duplicate() as CharacterDef
	made.id = &"nobody"
	made.display_name = "Nobody"
	var card: Button = KidCarousel.kid_card(made, Vector2(300.0, 460.0))
	autofree(card)
	var picture: TextureRect = card.get_node("Picture") as TextureRect
	assert_not_null(picture.texture, "a face crop or the portrait")
	assert_not_null(picture.material, "cropped to the plate")
	assert_not_null(card.get_node_or_null("Taken"), "and it can be taken too")


# ---- layout ----

func test_the_carousel_fits_every_phone_inside_the_safe_areas_and_clear_of_play() -> void:
	for phone: Vector2 in PHONES:
		var screen: WeaponPickScreen = await _open(phone, &"toni", [&"popoy"] as Array[StringName])
		# a notch on top and a home bar below: the margins the safe-area code would add
		screen._safe_margin.add_theme_constant_override("margin_top", 120)
		screen._safe_margin.add_theme_constant_override("margin_bottom", 100)
		screen._safe_margin.add_theme_constant_override("margin_left", 48)
		screen._safe_margin.add_theme_constant_override("margin_right", 48)
		await wait_process_frames(4)
		var strip: KidCarousel = screen._carousel
		var safe: Rect2 = Rect2(Vector2(48.0, 120.0), phone - Vector2(96.0, 220.0))
		var play: Rect2 = screen._ready_button.get_global_rect()
		var label: String = "%sx%s" % [phone.x, phone.y]
		assert_true(safe.encloses(strip.get_global_rect()), "%s: the carousel is inside the safe area" % label)
		assert_true(safe.encloses(play), "%s: so is Play" % label)
		assert_true(safe.encloses(screen._hero_name.get_global_rect()), "%s: and the name" % label)
		assert_lte(strip.get_global_rect().end.y, screen._hero_name.get_global_rect().position.y + 1.0, "%s: the name is under the cards" % label)
		assert_lte(screen._hero_name.get_global_rect().end.y, play.position.y, "%s: nothing over Play" % label)
		assert_gte(strip.get_global_rect().position.y, screen.back_button.get_global_rect().end.y, "%s: clear of the heading" % label)
		assert_gte(play.size.y, UiMetrics.MIN_TARGET, "%s: Play is a big target" % label)
		assert_true(safe.encloses(screen._hero_about.get_global_rect()), "%s: and the description" % label)
		assert_lte(screen._hero_about.get_global_rect().end.y, play.position.y, "%s: nothing over Play" % label)
		var bounds: Rect2 = strip.get_global_rect()
		for card: Button in strip.cards:
			if not card.visible:
				continue
			var rect: Rect2 = _card_rect(strip, card)
			assert_gte(rect.position.x, bounds.position.x - 0.5, "%s: %s is not cut off on the left" % [label, card.name])
			assert_lte(rect.end.x, bounds.end.x + 0.5, "%s: %s is not cut off on the right" % [label, card.name])
		var chosen: Rect2 = _card_rect(strip, strip.cards[strip.index])
		assert_gt(chosen.size.x, 0.15 * phone.x, "%s: the chosen card is big enough to read" % label)
		assert_gte(chosen.position.y, bounds.position.y, "%s: not cut off on top (with its bob)" % label)
		assert_lte(chosen.end.y, bounds.end.y, "%s: nor below" % label)


func test_the_name_is_large_and_follows_the_pick() -> void:
	var screen: WeaponPickScreen = await _open()
	assert_gte(screen._hero_name.get_theme_font_size("font_size"), UiMetrics.DISPLAY)
	_swipe(screen, _step(screen))
	assert_eq(screen._hero_name.text, screen.hero_name())
	assert_false(screen._hero_name.text.is_empty())
