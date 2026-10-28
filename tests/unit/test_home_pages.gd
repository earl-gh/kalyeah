extends GutTest

var _saved_loadout: Array[StringName] = []
## The pages behind the main screen's section buttons: characters, weapons, history, settings.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _saved_avatar: String
var _saved_history: String


func before_each() -> void:
	_saved_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout.clear()
	_saved_avatar = Session.avatar_id
	_saved_history = MatchHistory.path
	MatchHistory.path = "user://history_test.cfg"
	MatchHistory.clear()


func after_each() -> void:
	Session.saved_loadout = _saved_loadout
	MatchHistory.clear()
	MatchHistory.path = _saved_history
	Session.avatar_id = _saved_avatar
	Session.save()


func _phone_screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _hosted(page: Control) -> PageHarness:
	var harness: PageHarness = autofree(PageHarness.new(page)) as PageHarness
	add_child(harness)
	return harness


func test_the_characters_page_only_shows_and_describes_it_never_changes_your_face() -> void:
	Session.avatar_id = "toni"
	var screen: PageHarness = _hosted(CharactersPage.new())
	await wait_frames(2)
	var page: CharactersPage = screen.page as CharactersPage
	assert_eq(page.cards.size(), Session.AVATARS.size())
	for id: String in Session.AVATARS:
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		assert_false(definition.description.is_empty(), "%s has a description" % id)
		page.cards[id].pressed.emit()
		assert_not_null(screen.dialog, "%s opens a sheet" % id)
		assert_eq(screen.dialog.title_label.text, definition.display_name)
		assert_eq((screen.dialog.find_child("About", true, false) as Label).text, definition.description)
		assert_eq(Session.avatar_id, "toni", "reading about %s does not choose them" % id)
	assert_null(page.find_child("Mine", true, false), "no 'You' tag: nothing here is selected")
	assert_false(page.find_child("Scroll", true, false).get_child(0).get_children().any(func(card: Node) -> bool: return card.find_child("Frame", true, false) != null), "no selection frame")
	screen.dialog.find_child("Close", true, false).pressed.emit()
	assert_false(screen.dialog.is_open)


func test_the_character_sheet_says_the_look_changes_nothing_about_play() -> void:
	var definition: CharacterDef = load("res://data/characters/popoy.tres") as CharacterDef
	var card: MenuDialog = autofree(CharactersPage.detail(definition)) as MenuDialog
	add_child(card)
	var text: String = ""
	for label: Node in card.find_children("*", "Label", true, false):
		text += (label as Label).text + " "
	assert_true(text.contains("same stats"), "cosmetic only, as the design says")


func test_cards_opens_both_grids_and_only_grid_cards_open_the_drawer() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"codex")
	await wait_frames(3)
	assert_true(screen.codex_page.kids_screen.is_visible_in_tree())
	assert_true(screen.codex_page.skills_screen.is_visible_in_tree())
	screen.codex_page.hub.skills_card.pressed.emit()
	assert_false(screen.card_drawer.is_open)
	screen.codex_page.kids_screen.cells[0].pressed.emit()
	assert_true(screen.card_drawer.is_open)


func test_the_weapons_page_is_a_grid_with_a_tile_for_every_weapon() -> void:
	var screen: PageHarness = _hosted(WeaponsPage.new())
	await wait_frames(2)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var page: WeaponsPage = screen.page as WeaponsPage
	assert_eq(page.tiles.size(), rules.weapons.size())
	var columns: Dictionary[int, bool] = {}
	for i: int in rules.weapons.size():
		assert_eq((page.tiles[i].find_child("Name", true, false) as Label).text, rules.weapons[i].display_name)
		assert_gte(page.tiles[i].get_global_rect().size.y, 88.0, "a tile is a big touch target")
		columns[int(page.tiles[i].get_global_rect().position.x)] = true
	assert_eq(columns.size(), WeaponsPage.COLUMNS, "two columns, not one long list")


func test_weapons_group_by_street_game_or_by_type_and_every_weapon_is_in_exactly_one_group() -> void:
	var screen: PageHarness = _hosted(WeaponsPage.new())
	await wait_frames(2)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var page: WeaponsPage = screen.page as WeaponsPage
	assert_eq(page.grouping, WeaponsPage.BY_GAME, "inspired-by-game first")
	var games: Array[String] = []
	for weapon: WeaponDef in rules.weapons:
		if not games.has(weapon.street_game):
			games.append(weapon.street_game)
	assert_eq(page.sections.size(), games.size(), "one section per street game")
	for grouping: StringName in [WeaponsPage.BY_GAME, WeaponsPage.BY_TYPE]:
		page.segmented.buttons[grouping].pressed.emit()
		assert_eq(page.grouping, grouping)
		var seen: int = 0
		for section: Control in page.sections:
			var grid: GridContainer = section.find_child("Grid", true, false) as GridContainer
			var header: String = (section.find_child("Header", true, false) as Label).text
			assert_true(header.ends_with(" · %d" % grid.get_child_count()), "the header counts its tiles: %s" % header)
			seen += grid.get_child_count()
			for tile: Node in grid.get_children():
				var weapon: WeaponDef = rules.weapons[page.tiles.find(tile as Button)]
				var key: String = weapon.street_game if grouping == WeaponsPage.BY_GAME else String(WeaponsPage.KIND_NAMES[weapon.kind])
				assert_true(header.to_upper().begins_with(key.to_upper()), "%s sits under %s" % [weapon.id, header])
		assert_eq(seen, rules.weapons.size(), "no weapon is lost or doubled")
	assert_eq(page.sections.size(), WeaponsPage.KIND_NAMES.size(), "one section per type")


func test_switching_the_grouping_starts_again_at_the_top() -> void:
	var screen: PageHarness = _hosted(WeaponsPage.new())
	await wait_frames(2)
	var scroll: ScrollContainer = screen.page.find_child("Scroll", true, false) as ScrollContainer
	scroll.scroll_vertical = 600
	await wait_frames(1)
	(screen.page as WeaponsPage).segmented.buttons[WeaponsPage.BY_TYPE].pressed.emit()
	assert_eq(scroll.scroll_vertical, 0, "a new grouping is read from its first section")


func test_a_tile_shows_the_other_axis_as_its_chip() -> void:
	var screen: PageHarness = _hosted(WeaponsPage.new())
	await wait_frames(2)
	var page: WeaponsPage = screen.page as WeaponsPage
	var weapon: WeaponDef = (load("res://data/rules/game_rules.tres") as GameRules).weapons[0]
	var texts: Callable = func() -> Array[String]:
		var found: Array[String] = []
		for chip: Node in page.tiles[0].find_children("*", "PanelContainer", true, false):
			if (chip as Control).visible and chip.has_node("Text"):
				found.append((chip.get_node("Text") as Label).text)
		return found
	assert_eq(texts.call(), [WeaponsPage.KIND_NAMES[weapon.kind]] as Array[String], "under a game: the type")
	page.show_grouping(WeaponsPage.BY_TYPE)
	assert_eq(texts.call(), [weapon.street_game] as Array[String], "under a type: the game")


func test_tapping_a_tile_opens_the_detail_sheet_with_the_text_and_figures() -> void:
	var screen: PageHarness = _hosted(WeaponsPage.new())
	await wait_frames(2)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	for i: int in rules.weapons.size():
		var weapon: WeaponDef = rules.weapons[i]
		(screen.page as WeaponsPage).tiles[i].pressed.emit()
		assert_not_null(screen.dialog, "%s opens a sheet" % weapon.id)
		assert_not_null(screen.dialog.find_child("Figures", true, false), "the sheet shows its figures")
		assert_eq((screen.dialog.find_child("About", true, false) as Label).text, weapon.description)
		assert_eq(screen.dialog.title_label.text, weapon.display_name)
		var figures: Control = screen.dialog.find_child("Figures", true, false)
		assert_eq(figures.get_child_count(), WeaponsPage.figures_of(weapon).size())
		assert_gt(figures.get_child_count(), 0, "every weapon has a figure to show")
	screen.dialog.find_child("Close", true, false).pressed.emit()
	assert_false(screen.dialog.is_open)


func test_the_figures_name_damage_or_healing_elixir_and_range() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var saw_heal: bool = false
	for weapon: WeaponDef in rules.weapons:
		var captions: Array[String] = []
		for figure: Dictionary in WeaponsPage.figures_of(weapon):
			captions.append(String(figure["caption"]))
		assert_eq(captions.has("Heal"), weapon.heal > 0, "%s: healing is named as healing" % weapon.id)
		assert_eq(captions.has("Damage"), weapon.heal <= 0 and weapon.damage > 0, "%s: damage" % weapon.id)
		assert_eq(captions.has("Ice water"), weapon.elixir_cost > 0, "%s: the Book shows the ice water cost, not the cooldown" % weapon.id)
		assert_false(captions.has("Cooldown"))
		assert_eq(captions.has("Range"), weapon.max_range > 0.0)
		saw_heal = saw_heal or weapon.heal > 0
	assert_true(saw_heal, "the healing weapon is among them")


func test_history_starts_empty_and_lists_battles_newest_first() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"history")
	await wait_frames(2)
	assert_not_null(screen.history_page.find_child("Empty", true, false), "nothing played yet")
	MatchHistory.record("practice", true, 2, 1)
	MatchHistory.record("hotspot", false, 0, 2)
	screen.history_page.refresh()
	await wait_frames(2)
	var results: Array[String] = []
	for badge: Node in screen.history_page.find_children("Result", "PanelContainer", true, false):
		if not badge.is_queued_for_deletion():
			results.append((badge.get_node("Text") as Label).text)
	assert_eq(results, ["Defeat", "Victory"], "the newest battle is first")


func test_the_history_keeps_the_last_thirty_and_words_the_age() -> void:
	for i: int in MatchHistory.MAX_ENTRIES + 5:
		MatchHistory.record("online", i % 2 == 0, i, 0)
	var found: Array[Dictionary] = MatchHistory.entries()
	assert_eq(found.size(), MatchHistory.MAX_ENTRIES)
	assert_eq(int(found[0]["mine"]), MatchHistory.MAX_ENTRIES + 4, "newest first")
	assert_eq(MatchHistory.ago(100, 130), "just now")
	assert_eq(MatchHistory.ago(0, 300), "5 min ago")
	assert_eq(MatchHistory.ago(0, 7300), "2 h ago")
	assert_eq(MatchHistory.ago(0, 200000), "2 days ago")


func test_the_settings_page_has_comfort_and_quit_but_no_profile_how_to_play_or_server_address() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"settings")
	await wait_frames(2)
	var page: SettingsPage = screen.settings_page
	assert_true(page.visible)
	assert_null(page.find_child("ServerField", true, false), "no server address: it is built into the game")
	assert_null(page.find_child("ProfileButton", true, false), "no profile card (owner): the name tag on Home changes the name and photo")
	assert_null(page.find_child("HowToButton", true, false), "no How to play (owner)")
	assert_not_null(page.find_child("ControlOptions", true, false), "the comfort and control switches")
	page.quit_button.pressed.emit()
	assert_not_null(screen.dialog, "quit asks first")


func test_winning_a_battle_is_written_to_the_history() -> void:
	var practice: PracticeMatch = autofree((load("res://scenes/match/practice.tscn") as PackedScene).instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	practice.sim.match_won.emit(practice.local_team)
	var found: Array[Dictionary] = MatchHistory.entries()
	assert_eq(found.size(), 1)
	assert_true(bool(found[0]["won"]))
	assert_eq(found[0]["mode"], "practice")
