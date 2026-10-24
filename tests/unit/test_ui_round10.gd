extends GutTest
## Round 10 of the interface plan: the Book with depth (the real street game behind each weapon, stat bars, outfit
## swatches) and a History with a summary and a mode filter.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _history_path: String


func before_each() -> void:
	_history_path = MatchHistory.path
	MatchHistory.path = "user://history_r10.cfg"
	MatchHistory.clear()


func after_each() -> void:
	MatchHistory.clear()
	MatchHistory.path = _history_path


func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _rules() -> GameRules:
	return load("res://data/rules/game_rules.tres") as GameRules


func _entry(mode: String, won: bool, mine: int = 2, theirs: int = 1) -> Dictionary:
	return {"mode": mode, "won": won, "mine": mine, "theirs": theirs, "at": int(Time.get_unix_time_from_system())}


# ---- the game behind the weapon ----------------------------------------------------------

func test_every_street_game_has_one_plain_sentence() -> void:
	var games: StreetGames = WeaponsPage.street_games()
	var seen: Dictionary[String, bool] = {}
	for weapon: WeaponDef in _rules().weapons:
		seen[weapon.street_game] = true
	assert_gte(seen.size(), 5)
	for game: String in seen:
		var blurb: String = games.blurb_of(game)
		assert_false(blurb.is_empty(), "%s has a sentence" % game)
		assert_true(blurb.ends_with("."), game)
		assert_lt(blurb.length(), 140, "one line, not a paragraph: %s" % game)
	assert_eq(games.blurb_of("No such game"), "")


func test_by_game_sections_carry_their_games_sentence_and_by_type_sections_do_not() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"codex")
	screen.codex_page.show_tab(&"weapons")
	await wait_frames(2)
	var page: WeaponsPage = screen.weapons_page
	var games: StreetGames = WeaponsPage.street_games()
	for section: Control in page.sections:
		var blurb: Label = section.find_child("Blurb", true, false) as Label
		assert_not_null(blurb, "%s has its sentence" % section.name)
		var header: String = (section.find_child("Header", true, false) as Label).text
		var game: String = header.substr(0, header.rfind(" ·"))
		for known: String in games.blurbs:
			if known.to_upper() == game:
				assert_eq(blurb.text, games.blurbs[known])
	page.show_grouping(WeaponsPage.BY_TYPE)
	for section: Control in page.sections:
		assert_null(section.find_child("Blurb", true, false), "a type is not a game")


# ---- stat bars ---------------------------------------------------------------------------

func test_a_full_bar_means_the_biggest_number_of_any_weapon() -> void:
	var weapons: Array[WeaponDef] = _rules().weapons
	var maxima: Dictionary = WeaponsPage.stat_maxima(weapons)
	var top_power: float = 0.0
	for weapon: WeaponDef in weapons:
		top_power = maxf(top_power, float(maxi(weapon.damage, weapon.heal)))
	assert_eq(maxima["power"], top_power)
	var any_full: Dictionary[String, bool] = {"Damage": false, "Elixir": false, "Range": false}
	for weapon: WeaponDef in weapons:
		for figure: Dictionary in WeaponsPage.figures_of(weapon):
			var caption: String = String(figure["caption"])
			var fraction: float = WeaponsPage.fraction_of(caption, weapon, maxima)
			assert_between(fraction, 0.0, 1.0, "%s %s" % [weapon.id, caption])
			assert_gt(fraction, 0.0, "a stat that is shown is never an empty bar: %s %s" % [weapon.id, caption])
			if is_equal_approx(fraction, 1.0):
				any_full[caption if caption != "Heal" else "Damage"] = true
	for caption: String in any_full:
		assert_true(any_full[caption], "some weapon fills the %s bar" % caption)


func test_a_missing_stat_is_an_empty_bar_not_a_crash() -> void:
	var none: WeaponDef = WeaponDef.new()
	assert_eq(WeaponsPage.fraction_of("Damage", none, {"power": 0.0, "elixir": 0.0, "range": 0.0}), 0.0)
	assert_eq(WeaponsPage.fraction_of("Nonsense", none, {"power": 10.0, "elixir": 1.0, "range": 1.0}), 0.0)


func test_the_sheet_shows_one_bar_per_stat_with_its_exact_number_and_the_game() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"codex")
	screen.codex_page.show_tab(&"weapons")
	await wait_frames(2)
	var weapons: Array[WeaponDef] = _rules().weapons
	var maxima: Dictionary = WeaponsPage.stat_maxima(weapons)
	for i: int in weapons.size():
		var weapon: WeaponDef = weapons[i]
		screen.weapons_page.tiles[i].pressed.emit()
		var figures: Control = screen.dialog.find_child("Figures", true, false)
		var expected: Array[Dictionary] = WeaponsPage.figures_of(weapon)
		assert_eq(figures.get_child_count(), expected.size(), weapon.id)
		for k: int in expected.size():
			var row: Control = figures.get_child(k)
			assert_eq((row.find_child("Value", true, false) as Label).text, expected[k]["value"], "the exact number stays")
			var meter: ProgressBar = row.find_child("Meter", true, false) as ProgressBar
			assert_almost_eq(meter.value, WeaponsPage.fraction_of(String(expected[k]["caption"]), weapon, maxima), 0.002)
		assert_eq((screen.dialog.find_child("GameBlurb", true, false) as Label).text, WeaponsPage.street_games().blurb_of(weapon.street_game))
		screen.dialog.close(false)


func test_heal_has_a_green_bar_and_damage_a_gold_one() -> void:
	assert_eq(WeaponsPage.BAR_COLORS["Heal"], Color("45dd78"))
	assert_ne(WeaponsPage.BAR_COLORS["Damage"], WeaponsPage.BAR_COLORS["Heal"])
	assert_eq(WeaponsPage.BAR_COLORS.size(), 4)


# ---- outfit swatches ---------------------------------------------------------------------

func test_each_kid_has_a_palette_of_clothes_never_skin() -> void:
	for id: String in Session.AVATARS:
		var kid: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		var labels: Array[String] = []
		for swatch: Dictionary in CharactersPage.palette_of(kid):
			labels.append(String(swatch["label"]))
		assert_eq(labels.slice(0, 3), ["Hair", "Top", "Accent"] as Array[String], id)
		assert_true(labels.has("Footwear"), id)
		assert_false(labels.has("Skin"), "%s: the palette is about clothes and style, not skin tone" % id)
		assert_eq(labels.has("Bottom"), kid.bottom != CharacterDef.Bottom.NONE, "%s: a bottom only when they wear one" % id)
		assert_eq(labels.has("Extra"), kid.extra != CharacterDef.Extra.NONE, "%s: an extra only when they have one" % id)
		var palette: Array[Dictionary] = CharactersPage.palette_of(kid)
		assert_eq(palette[0]["color"], kid.hair_color)
		assert_eq(palette[1]["color"], kid.tint)


func test_the_kid_sheet_shows_the_swatches() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"codex")
	await wait_frames(2)
	screen.characters_page.cards["migo"].pressed.emit()
	var kid: CharacterDef = load("res://data/characters/migo.tres") as CharacterDef
	for swatch: Dictionary in CharactersPage.palette_of(kid):
		var dot: Panel = screen.dialog.find_child("Swatch_%s" % String(swatch["label"]), true, false) as Panel
		assert_not_null(dot, String(swatch["label"]))
		assert_eq((dot.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, swatch["color"])
	assert_not_null(screen.dialog.find_child("Palette", true, false))


# ---- history numbers ---------------------------------------------------------------------

func test_the_summary_counts_played_won_rate_and_streak_from_the_newest() -> void:
	assert_eq(MatchHistory.summary([] as Array[Dictionary]), {"played": 0, "wins": 0, "rate": 0, "streak": 0})
	var list: Array[Dictionary] = [_entry("practice", true), _entry("hotspot", true), _entry("online", false), _entry("hotspot", true)]
	assert_eq(MatchHistory.summary(list), {"played": 4, "wins": 3, "rate": 75, "streak": 2})
	var lost_last: Array[Dictionary] = [_entry("practice", false), _entry("practice", true)]
	assert_eq(MatchHistory.summary(lost_last)["streak"], 0, "a loss now ends the streak")
	var thirds: Array[Dictionary] = [_entry("a", true), _entry("a", true), _entry("a", false)]
	assert_eq(MatchHistory.summary(thirds)["rate"], 67, "rounded to a whole percent")


func test_a_mode_filter_keeps_only_that_mode_and_empty_keeps_all() -> void:
	var list: Array[Dictionary] = [_entry("practice", true), _entry("hotspot", true), _entry("hotspot", false)]
	assert_eq(MatchHistory.of_mode(list, "hotspot").size(), 2)
	assert_eq(MatchHistory.of_mode(list, "online").size(), 0)
	assert_eq(MatchHistory.of_mode(list, "").size(), 3)


# ---- history page ------------------------------------------------------------------------

func test_the_history_page_shows_the_figures_and_filters_the_list() -> void:
	MatchHistory.record("hotspot", false, 0, 2)
	MatchHistory.record("online", false, 1, 2)
	MatchHistory.record("hotspot", true, 2, 1)
	MatchHistory.record("practice", true, 2, 0)
	var screen: TitleScreen = _screen()
	screen.open_page(&"history")
	await wait_frames(2)
	var page: HistoryPage = screen.history_page
	assert_true(page.summary_card.is_visible_in_tree())
	assert_eq(page.played_label.text, "4")
	assert_eq(page.rate_label.text, "50%")
	assert_eq(page.streak_label.text, "2", "the two newest were wins")
	assert_eq(page.find_children("Result", "PanelContainer", true, false).size(), 4)
	page.filter_bar.buttons[&"hotspot"].pressed.emit()
	await wait_frames(2)
	assert_eq(page.filter, &"hotspot")
	assert_eq(page.played_label.text, "2")
	assert_eq(page.rate_label.text, "50%")
	assert_eq(page.streak_label.text, "1", "the newest hotspot match was a win, the one before a loss")
	var shown: int = 0
	for badge: Node in page.find_children("Result", "PanelContainer", true, false):
		if not badge.is_queued_for_deletion():
			shown += 1
	assert_eq(shown, 2)


func test_a_mode_with_no_battles_says_so_and_all_brings_them_back() -> void:
	MatchHistory.record("practice", true, 2, 0)
	var screen: TitleScreen = _screen()
	screen.open_page(&"history")
	await wait_frames(2)
	var page: HistoryPage = screen.history_page
	page.show_filter(&"online")
	await wait_frames(2)
	assert_not_null(page.find_child("Empty", true, false))
	assert_eq(page.played_label.text, "0")
	assert_eq(page.rate_label.text, "-", "no rate without a match")
	page.show_filter(&"")
	await wait_frames(2)
	assert_eq(page.played_label.text, "1")


func test_with_no_history_at_all_the_figures_and_filters_stay_hidden() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"history")
	await wait_frames(2)
	assert_false(screen.history_page.summary_card.is_visible_in_tree())
	assert_false(screen.history_page.filter_bar.is_visible_in_tree())
	assert_not_null(screen.history_page.find_child("Empty", true, false))


func test_the_four_filters_fit_the_page_and_are_thumb_sized() -> void:
	MatchHistory.record("practice", true, 2, 0)
	var screen: TitleScreen = _screen()
	screen.open_page(&"history")
	await wait_frames(3)
	var bar: UiSegmented = screen.history_page.filter_bar
	assert_eq(bar.buttons.size(), 4)
	assert_lte(bar.get_global_rect().end.x, screen.history_page.get_global_rect().end.x + 1.0, "never wider than the page")
	for button: Button in bar.buttons.values():
		assert_gte(button.get_global_rect().size.y, 64.0)
