extends GutTest
## The Game of the month (calendar icon) and the Donate sheet (envelope icon).

const GAMES: LarongKalye = preload("res://data/rules/larong_kalye.tres")
const TEXT_FIELDS: Array[String] = ["name", "time", "place", "goal", "safety"]
const LIST_FIELDS: Array[String] = ["items", "setup", "how", "rules"]
## The copy stays short (Clash style): no line is a paragraph.
const MAX_LINE: int = 62


func test_there_is_one_game_for_every_month() -> void:
	assert_eq(GAMES.games.size(), 12)
	var names: Dictionary = {}
	for month: int in range(1, 13):
		var game: Dictionary = GAMES.for_month(month)
		assert_false(game.is_empty(), "month %d has a game" % month)
		assert_false(names.has(game["name"]), "%s is used once" % game["name"])
		names[game["name"]] = true
	assert_true(GAMES.for_month(13).is_empty())


func test_every_game_teaches_everything_a_real_game_needs() -> void:
	for game: Dictionary in GAMES.games:
		for field: String in TEXT_FIELDS:
			assert_false(String(game[field]).strip_edges().is_empty(), "%s has %s" % [game["name"], field])
		for field: String in LIST_FIELDS:
			var lines: Array = game[field] as Array
			assert_gt(lines.size(), 0, "%s has %s" % [game["name"], field])
			for line: Variant in lines:
				assert_false(String(line).strip_edges().is_empty())
		assert_gte(int(game["players_min"]), 1, "%s: players" % game["name"])
		assert_lte(int(game["players_min"]), int(game["players_max"]))
		assert_gte((game["how"] as Array).size(), 3, "%s: the mechanics are in steps" % game["name"])
		assert_lte((game["how"] as Array).size(), 4, "%s: not too many steps" % game["name"])


func test_the_words_are_short_like_clash_not_a_wall_of_text() -> void:
	for game: Dictionary in GAMES.games:
		var lines: Array[String] = [String(game["goal"]), String(game["safety"])]
		for field: String in ["setup", "how", "rules"]:
			for line: Variant in game[field] as Array:
				lines.append(String(line))
		var words: int = 0
		for line: String in lines:
			assert_lte(line.length(), MAX_LINE, "%s: '%s' is short" % [game["name"], line])
			words += line.split(" ").size()
		assert_lte(words, 110, "%s: about a hundred words in all" % game["name"])


func test_the_sheet_shows_the_games_title_players_items_goal_and_mechanics() -> void:
	for month: int in [1, 5, 12]:
		var game: Dictionary = GAMES.for_month(month)
		var card: MenuDialog = GameOfMonthSheet.build(month)
		add_child(card)
		await wait_process_frames(2)
		assert_eq(card.title_label.text, "Game of the Month", "the modal's title")
		var texts: String = ""
		for label: Node in card.find_children("*", "Label", true, false):
			texts += (label as Label).text + "\n"
		for chip: Node in card.find_children("Text", "Label", true, false):
			texts += (chip as Label).text + "\n"
		assert_true(texts.contains(LarongKalye.month_name(month).to_upper()), "the month")
		assert_true(texts.contains(String(game["name"])), "the game's name is the headline")
		assert_true(texts.contains("%d to %d players" % [game["players_min"], game["players_max"]]), "the players")
		assert_true(texts.contains(String((game["items"] as Array)[0])), "what you need")
		assert_true(texts.contains(String(game["goal"])), "the goal")
		assert_true(texts.contains(String((game["how"] as Array)[0])), "how to play")
		for heading: String in ["YOU NEED", "GOAL", "SET UP", "HOW TO PLAY", "RULES", "STAY SAFE"]:
			assert_true(texts.contains(heading), heading)
		card.queue_free()


func test_without_a_month_the_sheet_uses_the_phones_current_month() -> void:
	var month: int = int(Time.get_date_dict_from_system()["month"])
	var card: MenuDialog = GameOfMonthSheet.build()
	add_child(card)
	assert_eq(card.title_label.text, "Game of the Month")
	assert_eq((card.find_child("GameName", true, false) as Label).text, String(GAMES.for_month(month)["name"]), "this month's game")
	card.queue_free()


func test_the_donate_sheet_has_no_link_until_the_owner_sets_one() -> void:
	var info: DonateInfo = load(DonateSheet.INFO_PATH) as DonateInfo
	assert_false(info.thanks.is_empty())
	assert_false(info.grown_ups.is_empty(), "a note for grown-ups")
	var empty: DonateInfo = DonateInfo.new()
	empty.thanks = "Thanks!"
	var card: MenuDialog = DonateSheet.build(empty)
	add_child(card)
	assert_eq(card.title_label.text, "Donate", "the modal's title")
	assert_null(card.find_child("DonateButton", true, false), "no link, no donate button")
	assert_not_null(card.find_child("Soon", true, false))
	card.queue_free()


func test_the_donate_button_opens_the_owners_link_and_only_https() -> void:
	var opened: Array[String] = []
	var original: Callable = DonateSheet.open_url
	DonateSheet.open_url = func(url: String) -> void: opened.append(url)
	var info: DonateInfo = DonateInfo.new()
	info.thanks = "Thanks!"
	info.url = "http://example.com/pay"
	assert_false(info.has_link(), "plain http is not accepted")
	info.url = "https://example.com/donate"
	assert_true(info.has_link())
	var card: MenuDialog = DonateSheet.build(info)
	add_child(card)
	var button: Button = card.find_child("DonateButton", true, false) as Button
	assert_not_null(button)
	button.pressed.emit()
	assert_eq(opened, ["https://example.com/donate"] as Array[String])
	DonateSheet.open_url = original
	card.queue_free()


func test_no_modal_is_taller_than_three_quarters_of_the_screen() -> void:
	var holder: SubViewport = autofree(SubViewport.new()) as SubViewport
	holder.size = Vector2i(720, 1280)
	add_child(holder)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sheets: Array[MenuDialog] = [GameOfMonthSheet.build(2), DonateSheet.build(), WeaponsPage.detail(rules.weapons[0]),
		DialogButtons.leave_match(false, func() -> void: pass, true), ProfileSheet.build(func() -> void: pass)]
	for card: MenuDialog in sheets:
		card.open(holder)
		await wait_process_frames(3)
		assert_lte(card.panel.size.y, 1280.0 * MenuDialog.MAX_HEIGHT_SHARE + 1.0, "%s fits in 75 %% of the screen (%.0f px)" % [card.name, card.panel.size.y])
		assert_lte(card.panel.size.x, MenuDialog.WIDTH_SHEET + 1.0, "%s is no wider than a sheet" % card.name)
		card.close(false)
	# a long one reaches the cap and scrolls inside
	var long: MenuDialog = GameOfMonthSheet.build(2)
	long.open(holder)
	await wait_process_frames(3)
	assert_almost_eq(long.panel.size.y, 1280.0 * MenuDialog.MAX_HEIGHT_SHARE, 2.0, "a long sheet stops at 75 % and scrolls")
	long.close(false)


func test_the_two_new_sheets_have_a_picture_before_their_title() -> void:
	for card: MenuDialog in [GameOfMonthSheet.build(1), DonateSheet.build()]:
		add_child(card)
		var icon: TextureRect = card.find_child("Icon", true, false) as TextureRect
		assert_not_null(icon, "%s has a header icon" % card.name)
		assert_almost_eq(icon.custom_minimum_size.x, MenuDialog.ICON_SIZE, 0.1)
		card.queue_free()
