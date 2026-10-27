extends GutTest
## The Game of the month (calendar icon) and the Donate sheet (envelope icon).

const GAMES: LarongKalye = preload("res://data/rules/larong_kalye.tres")
const TEXT_FIELDS: Array[String] = ["name", "also", "time", "place", "goal", "win", "twist"]
const LIST_FIELDS: Array[String] = ["items", "setup", "how", "rules", "safety"]


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
		assert_gt((game["safety"] as Array).size(), 0, "%s: stay safe" % game["name"])


func test_the_sheet_shows_the_games_title_players_items_goal_and_mechanics() -> void:
	for month: int in [1, 5, 12]:
		var game: Dictionary = GAMES.for_month(month)
		var card: MenuDialog = GameOfMonthSheet.build(month)
		add_child(card)
		await wait_process_frames(2)
		assert_eq(card.title_label.text, game["name"])
		var texts: String = ""
		for label: Node in card.find_children("*", "Label", true, false):
			texts += (label as Label).text + "\n"
		for chip: Node in card.find_children("Text", "Label", true, false):
			texts += (chip as Label).text + "\n"
		assert_true(texts.contains(LarongKalye.month_name(month)), "the month")
		assert_true(texts.contains("%d to %d players" % [game["players_min"], game["players_max"]]), "the players")
		assert_true(texts.contains(String((game["items"] as Array)[0])), "what you need")
		assert_true(texts.contains(String(game["goal"])), "the goal")
		assert_true(texts.contains(String((game["how"] as Array)[0])), "how to play")
		for heading: String in ["WHAT YOU NEED", "THE GOAL", "SET UP", "HOW TO PLAY", "RULES", "HOW TO WIN", "STAY SAFE"]:
			assert_true(texts.contains(heading), heading)
		card.queue_free()


func test_without_a_month_the_sheet_uses_the_phones_current_month() -> void:
	var month: int = int(Time.get_date_dict_from_system()["month"])
	var card: MenuDialog = GameOfMonthSheet.build()
	add_child(card)
	assert_eq(card.title_label.text, String(GAMES.for_month(month)["name"]))
	card.queue_free()


func test_the_donate_sheet_has_no_link_until_the_owner_sets_one() -> void:
	var info: DonateInfo = load(DonateSheet.INFO_PATH) as DonateInfo
	assert_false(info.thanks.is_empty())
	assert_false(info.grown_ups.is_empty(), "a note for grown-ups")
	var empty: DonateInfo = DonateInfo.new()
	empty.thanks = "Thanks!"
	var card: MenuDialog = DonateSheet.build(empty)
	add_child(card)
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
