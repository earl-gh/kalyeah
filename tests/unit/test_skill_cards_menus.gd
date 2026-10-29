extends GutTest
## The owner's full-card skill pictures (`CardStyle.picture`) are what a skill looks like on the menu screens too, not only in the match hand:
## the Weapons tab of the Book (tiles and detail sheet), the weapon pick / swap screen's tiles and its slots. Each shows the picture BARE
## (no holder, plate or mask behind it), with its own proportions, fitted and centred; a weapon without a picture keeps its painted icon;
## a picked tile still reads as picked (gold edge along the picture's silhouette, the slot badge, the picked plate); names, kinds, taps,
## the sheet's description and meters, scrolling and the portrait layout (720x1280 and 390x844, which is 720x1558 once stretched) still work.

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The two phones, as the UI sees them: 720x1280, and 390x844 stretched ("expand") to the 720 base width.
const PHONES: Array[Vector2] = [Vector2(720.0, 1280.0), Vector2(720.0, 1558.0)]

var _rules: GameRules
var _forced_null: Array[StringName] = []
var _saved_history: String


func before_each() -> void:
	_rules = load(RULES_PATH) as GameRules
	_saved_history = MatchHistory.path
	MatchHistory.path = "user://history_test.cfg"


func after_each() -> void:
	for id: StringName in _forced_null:
		CardStyle._pictures.erase(id)
	_forced_null.clear()
	MatchHistory.path = _saved_history


## Pretends the owner has no picture for every weapon (the lookup answers null), the way a future weapon without one would be.
func _no_pictures() -> void:
	for weapon: WeaponDef in _rules.weapons:
		CardStyle._pictures[weapon.id] = null
		_forced_null.append(weapon.id)


func _phone(size: Vector2) -> Control:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = size
	phone.size = size
	add_child(phone)
	return phone


func _weapons_page(size: Vector2) -> WeaponsPage:
	var phone: Control = _phone(size)
	var page: WeaponsPage = autofree(WeaponsPage.new()) as WeaponsPage
	phone.add_child(page)
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	page.offset_left = UiMetrics.PAGE_MARGIN
	page.offset_right = -UiMetrics.PAGE_MARGIN
	page.offset_bottom = -200.0
	return page


func _pick_screen(size: Vector2, swap: bool = false) -> WeaponPickScreen:
	var phone: Control = _phone(size)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = size
	var ids: Array[StringName] = []
	for weapon: WeaponDef in _rules.weapons:
		ids.append(weapon.id)
	if swap:
		screen.open_swap(WeaponPick.new(ids, 10.0, [&"gunting_heavy", &"heal"] as Array[StringName]), _rules.weapons, "")
	else:
		screen.open(WeaponPick.new(ids, 10.0), _rules.weapons, "", RandomNumberGenerator.new(), [] as Array[CharacterDef], &"", true)
	return screen


func _art_of(node: Node) -> SkillCardArt:
	return node.find_child("Art", true, false) as SkillCardArt


## The picture inside `art` is drawn bare: its own texture, never stretched, centred, nothing styled around or over it.
## `shaped`: the cell has the picture's own shape (tiles and sheets), so the picture fills it; a slot's cell is wider than the card.
func _assert_bare_card(art: SkillCardArt, id: StringName, label: String, shaped: bool = true) -> void:
	var texture: Texture2D = CardStyle.picture(id)
	assert_not_null(texture, "%s has an owner picture" % label)
	assert_true(art.is_card(), "%s: it is the card picture" % label)
	assert_eq(art.picture.texture, texture, "%s: the weapon's own picture" % label)
	assert_eq(art.picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_CENTERED, "%s: fitted, never stretched" % label)
	assert_null(art.picture.material, "%s: no shader on the picture" % label)
	assert_eq(art.picture.self_modulate, Color.WHITE, "%s: not tinted" % label)
	var shown: Rect2 = art.shown_rect()
	assert_almost_eq(shown.size.x / shown.size.y, float(texture.get_width()) / float(texture.get_height()), 0.002, "%s: its own proportions" % label)
	assert_almost_eq(shown.get_center(), art.size / 2.0, Vector2(0.6, 0.6), "%s: centred" % label)
	assert_true(Rect2(Vector2.ZERO, art.size).grow(0.6).encloses(shown), "%s: inside its cell" % label)
	assert_gte(maxf(shown.size.x / art.size.x, shown.size.y / art.size.y), 0.99, "%s: as big as the cell lets it be" % label)
	if shaped:
		assert_almost_eq(art.size.x / art.size.y, SkillCardArt.CARD_ASPECT, 0.01, "%s: the cell has the picture's shape, so it fills it" % label)
	for child: Node in art.get_children():
		assert_false(child is Panel or child is PanelContainer, "%s: no plate, holder or mask behind it" % label)
	assert_eq(art.find_children("*", "PanelContainer", true, false).size(), 0, "%s: no holder" % label)


# ---- the shared picture ------------------------------------------------------------

func test_the_card_proportions_constant_is_the_pictures_shape() -> void:
	for weapon: WeaponDef in _rules.weapons:
		var texture: Texture2D = CardStyle.picture(weapon.id)
		assert_almost_eq(float(texture.get_width()) / float(texture.get_height()), SkillCardArt.CARD_ASPECT, 0.01, "%s" % weapon.id)
	var size: Vector2 = SkillCardArt.size_for_height(190.0)
	assert_almost_eq(size.x / size.y, SkillCardArt.CARD_ASPECT, 0.005)


func test_a_skill_card_art_shows_the_picture_bare_then_the_icon_when_there_is_no_picture() -> void:
	var art: SkillCardArt = autofree(SkillCardArt.new()) as SkillCardArt
	add_child(art)
	art.size = SkillCardArt.size_for_height(200.0)
	art.show_weapon(&"lata")
	_assert_bare_card(art, &"lata", "lata")
	_no_pictures()
	art.show_weapon(&"lata")
	assert_false(art.is_card(), "no picture: not the card")
	assert_eq(art.picture.texture, Icons.skill(&"lata"), "the painted icon stays as the fallback")
	art.show_weapon(&"")
	assert_null(art.picture.texture, "an empty slot shows nothing")
	assert_false(art.is_card())


func test_the_gold_edge_is_the_pictures_silhouette_behind_it_and_only_when_selected() -> void:
	var art: SkillCardArt = autofree(SkillCardArt.new()) as SkillCardArt
	add_child(art)
	art.size = SkillCardArt.size_for_height(200.0)
	art.show_weapon(&"bato_heavy")
	var edge: Control = art.get_node("Edge") as Control
	assert_lt(edge.get_index(), art.picture.get_index(), "the edge is drawn BEHIND the picture")
	assert_eq((edge.material as ShaderMaterial).shader.resource_path, "res://shaders/silhouette.gdshader", "the hand's silhouette shader: it follows the torn edge, not a box")
	assert_false(art.selected)
	art.set_selected(true)
	assert_true(art.selected)
	assert_gte(SkillCardArt.EDGE_WIDTH, 3.0, "thick enough to read")
	art.set_selected(false)
	assert_false(art.selected)


func test_a_card_art_follows_its_toggle_button() -> void:
	var button: Button = autofree(Button.new()) as Button
	button.toggle_mode = true
	add_child(button)
	var art: SkillCardArt = SkillCardArt.new()
	button.add_child(art)
	art.show_weapon(&"jacks")
	art.follow(button)
	assert_false(art.selected)
	button.set_pressed_no_signal(true)
	await wait_frames(2)
	assert_true(art.selected, "pressed in: selected")
	button.set_pressed_no_signal(false)
	await wait_frames(2)
	assert_false(art.selected)


# ---- the Weapons tab ---------------------------------------------------------------

func test_every_weapon_tile_shows_its_card_bare_with_its_name_and_kind() -> void:
	for size: Vector2 in PHONES:
		var page: WeaponsPage = _weapons_page(size)
		await wait_frames(3)
		assert_eq(page.tiles.size(), _rules.weapons.size())
		for i: int in page.tiles.size():
			var tile: Button = page.tiles[i]
			var weapon: WeaponDef = _rules.weapons[i]
			var label: String = "%s at %s" % [weapon.id, size]
			_assert_bare_card(_art_of(tile), weapon.id, label)
			assert_null(tile.find_child("Holder", true, false), "%s: the round holder is gone" % label)
			assert_eq((tile.find_child("Name", true, false) as Label).text, weapon.display_name, "%s: name" % label)
			assert_true(tile.accessibility_name.contains(weapon.display_name), "%s: the screen-reader name" % label)
			assert_true(tile.focus_mode == Control.FOCUS_ALL, "%s: still focusable" % label)
		page.queue_free()


func test_a_weapon_tile_without_a_picture_keeps_the_painted_icon() -> void:
	_no_pictures()
	var page: WeaponsPage = _weapons_page(PHONES[0])
	await wait_frames(3)
	for i: int in page.tiles.size():
		var art: SkillCardArt = _art_of(page.tiles[i])
		assert_false(art.is_card(), "%s" % _rules.weapons[i].id)
		assert_eq(art.picture.texture, Icons.skill(_rules.weapons[i].id), "%s: the painted icon" % _rules.weapons[i].id)
		assert_not_null(art.picture.texture)


func test_the_tiles_fit_the_page_in_two_columns_and_nothing_overlaps() -> void:
	for size: Vector2 in PHONES:
		for grouping: StringName in [WeaponsPage.BY_GAME, WeaponsPage.BY_TYPE]:
			var page: WeaponsPage = _weapons_page(size)
			await wait_frames(2)
			page.show_grouping(grouping)
			await wait_frames(3)
			var scroll: ScrollContainer = page.find_child("Scroll", true, false) as ScrollContainer
			var rects: Array[Rect2] = []
			for tile: Button in page.tiles:
				var rect: Rect2 = tile.get_global_rect()
				var label: String = "%s at %s %s" % [tile.name, size, grouping]
				assert_gte(rect.size.y, WeaponsPage.TILE_HEIGHT - 0.5, "%s: tall enough for the card, the name and the chip" % label)
				assert_gte(rect.size.x, UiMetrics.MIN_TARGET, "%s: a big target" % label)
				assert_gte(rect.position.x, scroll.get_global_rect().position.x - 0.5, "%s: inside the page, left" % label)
				assert_lte(rect.end.x, scroll.get_global_rect().end.x + 0.5, "%s: inside the page, right" % label)
				var art: SkillCardArt = _art_of(tile)
				var name_label: Label = tile.find_child("Name", true, false) as Label
				assert_lte(art.get_global_rect().end.y, name_label.get_global_rect().position.y + 0.5, "%s: the card sits above its name" % label)
				assert_true(rect.encloses(art.get_global_rect()), "%s: the card is inside its tile" % label)
				assert_true(rect.encloses(name_label.get_global_rect()), "%s: and so is the name" % label)
				for chip: Node in tile.find_children("*", "PanelContainer", true, false):
					if (chip as Control).visible:
						assert_gte((chip as Control).get_global_rect().position.y, name_label.get_global_rect().end.y - 0.5, "%s: the chip is under the name" % label)
						assert_true(rect.encloses((chip as Control).get_global_rect()), "%s: the chip is inside" % label)
				for other: Rect2 in rects:
					assert_false(rect.grow(-1.0).intersects(other.grow(-1.0)), "%s: no tile covers another" % label)
				rects.append(rect)
			var content: float = scroll.get_v_scroll_bar().max_value
			assert_lt(content, 3600.0, "%s %s: the list is still a short scroll (%s px)" % [size, grouping, content])
			page.queue_free()
			await wait_frames(1)


func test_a_tile_tap_still_asks_for_the_sheet() -> void:
	var page: WeaponsPage = _weapons_page(PHONES[0])
	await wait_frames(2)
	var asked: Array[StringName] = []
	page.weapon_opened.connect(func(weapon: WeaponDef) -> void: asked.append(weapon.id))
	for i: int in page.tiles.size():
		page.tiles[i].pressed.emit()
	var expected: Array[StringName] = []
	for weapon: WeaponDef in _rules.weapons:
		expected.append(weapon.id)
	assert_eq(asked, expected, "each tile asks for its own weapon")


func test_the_detail_sheet_shows_the_card_bare_above_the_description_and_the_meters() -> void:
	for size: Vector2 in PHONES:
		var phone: Control = _phone(size)
		# the Cards screens no longer host the Weapons page (`SkillsScreen` replaced it), so the old page and its sheet are hosted alone
		var page: WeaponsPage = WeaponsPage.new()
		var harness: PageHarness = autofree(PageHarness.new(page)) as PageHarness
		phone.add_child(harness)
		await wait_frames(3)
		for i: int in _rules.weapons.size():
			var weapon: WeaponDef = _rules.weapons[i]
			page.tiles[i].pressed.emit()
			await wait_frames(2)
			var sheet: MenuDialog = harness.dialog
			var label: String = "%s at %s" % [weapon.id, size]
			assert_not_null(sheet, "%s: a tap opens the sheet" % label)
			var art: SkillCardArt = _art_of(sheet)
			_assert_bare_card(art, weapon.id, label)
			assert_almost_eq(art.size.y, WeaponsPage.DETAIL_CARD_HEIGHT, 0.6, "%s: the sheet's card is bigger than a tile's" % label)
			assert_null(sheet.find_child("Holder", true, false), "%s: no round holder" % label)
			assert_eq((sheet.find_child("About", true, false) as Label).text, weapon.description, "%s: the description" % label)
			assert_eq(sheet.find_child("Figures", true, false).get_child_count(), WeaponsPage.figures_of(weapon).size(), "%s: the meters" % label)
			assert_eq(sheet.title_label.text, weapon.display_name, "%s: the name" % label)
			assert_lte(art.get_global_rect().end.y, (sheet.find_child("About", true, false) as Label).get_global_rect().position.y, "%s: the card is above the words" % label)
			var close: Button = sheet.find_child("CloseButton", true, false) as Button
			assert_true(phone.get_global_rect().encloses(sheet.get_global_rect()), "%s: the sheet fits the screen" % label)
			assert_true(sheet.get_global_rect().encloses(close.get_global_rect()), "%s: Close is inside the sheet and on screen" % label)
			assert_gte(close.size.y, UiMetrics.MIN_TARGET, "%s: Close is a target" % label)
			close.pressed.emit()
		phone.queue_free()
		await wait_frames(1)


func test_the_detail_sheet_of_a_weapon_without_a_picture_shows_its_icon() -> void:
	_no_pictures()
	var sheet: MenuDialog = autofree(WeaponsPage.detail(_rules.weapons[0])) as MenuDialog
	add_child(sheet)
	var art: SkillCardArt = _art_of(sheet)
	assert_false(art.is_card())
	assert_eq(art.picture.texture, Icons.skill(_rules.weapons[0].id))


# ---- the weapon pick / swap screen -------------------------------------------------

func test_pick_tiles_show_the_card_bare_with_name_and_kind_and_fit_at_both_phones() -> void:
	for size: Vector2 in PHONES:
		for swap: bool in [false, true]:
			var screen: WeaponPickScreen = _pick_screen(size, swap)
			await wait_process_frames(6)
			var scroll: ScrollContainer = screen.find_child("Weapons", true, false) as ScrollContainer
			var tiles: Array[Rect2] = []
			for weapon: WeaponDef in _rules.weapons:
				var tile: Button = screen.find_child("Weapon_" + String(weapon.id), true, false) as Button
				var label: String = "%s at %s swap=%s" % [weapon.id, size, swap]
				var art: SkillCardArt = _art_of(tile)
				_assert_bare_card(art, weapon.id, label)
				var name_label: Label = tile.find_child("WeaponName", true, false) as Label
				var kind: Label = tile.find_child("Kind", true, false) as Label
				assert_eq(name_label.text, weapon.display_name, "%s: the name" % label)
				assert_eq(kind.text, String(WeaponsPage.KIND_NAMES[weapon.kind]), "%s: the kind" % label)
				var rect: Rect2 = tile.get_global_rect()
				assert_gte(rect.size.y, LoadoutCards.TILE_HEIGHT - 0.5, "%s: tall enough for the card and the words" % label)
				assert_gte(rect.size.x, UiMetrics.MIN_TARGET, "%s: a target" % label)
				assert_lte(rect.end.x, scroll.get_global_rect().end.x + 0.5, "%s: inside the grid" % label)
				assert_true(rect.encloses(art.get_global_rect()), "%s: card inside its tile" % label)
				assert_lte(art.get_global_rect().end.y, name_label.get_global_rect().position.y + 0.5, "%s: card above the name" % label)
				assert_lte(name_label.get_global_rect().end.y, kind.get_global_rect().position.y + 0.5, "%s: the name's lines never reach the kind" % label)
				assert_true(rect.encloses(kind.get_global_rect()), "%s: the kind is inside" % label)
				for other: Rect2 in tiles:
					assert_false(rect.grow(-1.0).intersects(other.grow(-1.0)), "%s: no tile covers another" % label)
				tiles.append(rect)
				assert_eq(tile.accessibility_name, "%s, %s" % [weapon.display_name, weapon.kind_label()], "%s: the screen-reader name" % label)
			assert_gte(scroll.size.y, LoadoutCards.TILE_HEIGHT, "%s swap=%s: a whole row shows without scrolling" % [size, swap])
			assert_lte(screen._ready_button.get_global_rect().end.y, screen.get_global_rect().end.y, "Ready stays on screen")
			assert_lte((screen.find_child("Stats", true, false) as Label).get_global_rect().end.y, screen._ready_button.get_global_rect().position.y, "the info panel stays above Ready")
			assert_false(scroll.is_ancestor_of(screen._ready_button), "Ready is not inside the scroll")
			screen.get_parent().queue_free()
			await wait_frames(1)


func test_a_pick_tile_without_a_picture_keeps_the_painted_icon() -> void:
	_no_pictures()
	var screen: WeaponPickScreen = _pick_screen(PHONES[0])
	await wait_process_frames(4)
	for weapon: WeaponDef in _rules.weapons:
		var art: SkillCardArt = _art_of(screen.find_child("Weapon_" + String(weapon.id), true, false))
		assert_false(art.is_card(), "%s" % weapon.id)
		assert_eq(art.picture.texture, Icons.skill(weapon.id), "%s: the painted icon" % weapon.id)
	assert_not_null(_art_of(screen._sockets[0]), "the slot too")


func test_a_picked_tile_reads_as_picked_and_a_tap_still_picks() -> void:
	var screen: WeaponPickScreen = _pick_screen(PHONES[0])
	await wait_process_frames(4)
	var first: Button = screen.find_child("Weapon_bato_heavy", true, false) as Button
	var second: Button = screen.find_child("Weapon_langit_lupa", true, false) as Button
	var other: Button = screen.find_child("Weapon_lata", true, false) as Button
	assert_false(_art_of(first).selected, "nothing is picked yet")
	first.pressed.emit()
	second.pressed.emit()
	await wait_process_frames(4)
	assert_eq(screen.pick.picks, [&"bato_heavy", &"langit_lupa"] as Array[StringName], "the taps pick, as before")
	assert_true(first.button_pressed and second.button_pressed, "the picked plate is on")
	assert_true(_art_of(first).selected, "first: the gold edge along the picture")
	assert_true(_art_of(second).selected, "second: the gold edge along the picture")
	assert_false(_art_of(other).selected, "an unpicked card has none")
	var badge1: Label = screen._badges[&"bato_heavy"]
	var badge2: Label = screen._badges[&"langit_lupa"]
	assert_true(badge1.visible and badge2.visible, "the slot badges show")
	assert_eq([badge1.text, badge2.text], ["1", "2"], "which slot each fills")
	assert_false(screen._badges[&"lata"].visible)
	# a third replaces one: the edge follows the pick, not the tap
	screen.replacement_slot = 0
	other.pressed.emit()
	await wait_process_frames(4)
	assert_true(_art_of(other).selected, "the new pick is edged")
	assert_false(_art_of(first).selected, "the one it replaced is not")
	assert_true(_art_of(second).selected)
	first.pressed.emit()
	await wait_process_frames(4)
	assert_true(screen.pick.picks.has(&"bato_heavy"), "tapping it again picks it again")


func test_the_swap_screen_applies_a_pair_of_taps() -> void:
	var screen: WeaponPickScreen = _pick_screen(PHONES[0], true)
	await wait_process_frames(4)
	var swapped: Array[Array] = []
	screen.swapped.connect(func(picks: Array[StringName]) -> void: swapped.append(picks))
	(screen.find_child("Weapon_lata", true, false) as Button).pressed.emit()
	await wait_process_frames(2)
	assert_eq(swapped.size(), 1, "a tap applies at once")
	assert_true(screen.pick.picks.has(&"lata"))


# ---- the slots ---------------------------------------------------------------------

func test_the_slots_show_the_picked_cards_bare_inside_their_plate() -> void:
	for size: Vector2 in PHONES:
		var screen: WeaponPickScreen = _pick_screen(size, true)
		await wait_process_frames(6)
		var expected: Array[StringName] = [&"gunting_heavy", &"heal"]
		for slot: int in 2:
			var socket: LoadoutSocket = screen._sockets[slot]
			var label: String = "slot %d at %s" % [slot + 1, size]
			assert_eq(socket.weapon.id, expected[slot], label)
			var art: SkillCardArt = socket.art
			_assert_bare_card(art, expected[slot], label, false)
			assert_true(socket.get_global_rect().encloses(art.get_global_rect()), "%s: the card is inside the slot" % label)
			assert_gte(socket.size.y, UiMetrics.MIN_TARGET, "%s: still a target" % label)
			assert_gte(art.get_global_rect().position.y - socket.get_global_rect().position.y, 4.0, "%s: room above for the gold edge" % label)
			assert_lt(art.position.x + art.size.x, LoadoutSocket.TEXT_LEFT, "%s: the card ends before the words begin" % label)
			assert_gte(art.shown_rect().size.y, 70.0, "%s: big enough to tell the cards apart" % label)
			assert_true(socket.accessibility_name.contains(socket.weapon.display_name), "%s: the screen-reader name" % label)
		assert_true(screen._sockets[0].active, "slot 1 is the one the next tap fills")
		screen.select_slot(1)
		await wait_process_frames(2)
		assert_true(screen._sockets[1].active and not screen._sockets[0].active, "tapping a slot still chooses it")
		screen.get_parent().queue_free()
		await wait_frames(1)


func test_an_empty_slot_shows_no_picture_and_a_slot_without_a_picture_shows_the_icon() -> void:
	var screen: WeaponPickScreen = _pick_screen(PHONES[0])
	await wait_process_frames(4)
	assert_null(screen._sockets[0].art.picture.texture, "an empty slot draws no card")
	screen.tap(&"lata")
	await wait_process_frames(2)
	assert_true(screen._sockets[0].art.is_card(), "picked: its card")
	_no_pictures()
	screen._sockets[0].show_weapon(_rules.weapons[0])
	assert_false(screen._sockets[0].art.is_card())
	assert_eq(screen._sockets[0].art.picture.texture, Icons.skill(_rules.weapons[0].id), "the painted icon in the slot")


func test_the_slot_and_tile_art_take_no_touch() -> void:
	var screen: WeaponPickScreen = _pick_screen(PHONES[0])
	await wait_process_frames(4)
	for node: Node in screen.find_children("*", "SkillCardArt", true, false):
		for part: Node in [node] + node.get_children():
			assert_eq((part as Control).mouse_filter, Control.MOUSE_FILTER_IGNORE, "%s takes no touch: the button under it does" % part.get_path())
