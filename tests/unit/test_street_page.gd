extends GutTest
## The Street page (left tab) and the Settings entries that took over the Book and History.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _saved_history: String


func before_each() -> void:
	_saved_history = MatchHistory.path
	MatchHistory.path = "user://history_test.cfg"
	MatchHistory.clear()


func after_each() -> void:
	MatchHistory.clear()
	MatchHistory.path = _saved_history


func _phone_screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func test_every_game_has_its_name_and_picture_and_only_agawan_base_is_open() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"street")
	await wait_frames(2)
	assert_eq(screen.street_page.cards.size(), 3, "Agawan Base, Tumbang Preso, Patintero")
	assert_true(StreetPage.playable(&"street"))
	assert_false(StreetPage.playable(&"tumbang_preso"))
	assert_false(StreetPage.playable(&"patintero"))
	assert_not_null(screen.street_page.find_child("Street_street", true, false))
	var names: Array[String] = []
	for card: Control in screen.street_page.cards.values():
		var title: Label = card.find_child("Name", true, false) as Label
		names.append(title.text)
		assert_not_null((card.find_child("Picture", true, false) as TextureRect).texture, "%s has its picture" % title.text)
		assert_null(card.find_child("Game", true, false), "no second name line")
	assert_eq(names, ["Agawan Base", "Tumbang Preso", "Patintero"] as Array[String], "the games are named, not the streets")


func test_the_mechanics_come_from_the_rules_so_they_cannot_go_stale() -> void:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	var joined: String = "\n".join(StreetPage.mechanics_for(&"street", rules))
	assert_string_contains(joined, "%d s" % ceili(rules.rescue_hold_time), "the rescue time")
	assert_string_contains(joined, "%d s" % ceili(rules.capture_solo_time), "the solo capture time")
	assert_string_contains(joined, "score immediately", "a team wipe has no hold timer")


func test_tumbang_preso_and_patintero_are_practice_games_with_their_rules() -> void:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	for id: StringName in [&"tumbang_preso", &"patintero"]:
		assert_true(StreetPage.practice_only(id))
		var lines: Array[String] = StreetPage.mechanics_for(id, rules)
		assert_false(StreetPage.SOON_LINE in lines)
		assert_string_contains(lines[0], "5 minutes")
	assert_false(StreetPage.practice_only(&"street"), "Agawan Base is open everywhere")
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"street")
	await wait_frames(2)
	var chip: Control = screen.street_page.cards[&"patintero"].find_child("Status", true, false) as Control
	var words: Array[String] = []
	for label: Node in chip.find_children("*", "Label", true, false):
		words.append((label as Label).text)
	assert_has(words, "Practice", "the chip says where it can be played")


func test_the_book_and_history_have_tabs_of_their_own_and_settings_has_no_buttons_for_them() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"settings")
	await wait_frames(2)
	assert_null(screen.settings_page.find_child("BookButton", true, false), "the Cards tab opens the Book")
	assert_null(screen.settings_page.find_child("HistoryButton", true, false), "the History tab opens History")
	screen.corner_buttons[&"codex"].pressed.emit()
	await wait_frames(2)
	assert_eq(screen.current_page, &"codex")
	assert_eq(screen.tab_bar.active, &"codex", "the Cards tab is lit under the Book")
	screen.corner_buttons[&"history"].pressed.emit()
	await wait_frames(2)
	assert_eq(screen.current_page, &"history")
	assert_eq(screen.tab_bar.active, &"history")


func test_the_bottom_bar_runs_edge_to_edge_with_no_margin_around_it() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var rect: Rect2 = screen.tab_bar.get_global_rect()
	var whole: Rect2 = screen.get_global_rect()
	assert_almost_eq(rect.position.x, whole.position.x, 0.5, "flush with the left edge")
	assert_almost_eq(rect.end.x, whole.end.x, 0.5, "flush with the right edge")
	assert_almost_eq(rect.end.y, whole.end.y, 0.5, "flush with the bottom edge")
