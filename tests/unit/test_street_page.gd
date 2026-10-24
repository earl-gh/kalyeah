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


func test_every_game_is_a_street_and_only_agawan_base_is_open() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"street")
	await wait_frames(2)
	assert_eq(screen.street_page.cards.size(), 3, "Agawan Base, Tumbang Preso, Patintero")
	assert_true(StreetPage.playable(&"street"))
	assert_false(StreetPage.playable(&"tumbang_preso"))
	assert_false(StreetPage.playable(&"patintero"))
	assert_not_null(screen.street_page.find_child("Street_street", true, false))


func test_the_mechanics_come_from_the_rules_so_they_cannot_go_stale() -> void:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	var joined: String = "\n".join(StreetPage.mechanics_for(&"street", rules))
	assert_string_contains(joined, "%d s" % ceili(rules.rescue_hold_time), "the rescue time")
	assert_string_contains(joined, "%d s" % ceili(rules.capture_solo_time), "the solo capture time")
	assert_string_contains(joined, "%d s" % ceili(rules.capture_time), "the all-tagged hold time")


func test_a_street_that_is_not_built_says_so() -> void:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	assert_eq(StreetPage.mechanics_for(&"patintero", rules), [StreetPage.SOON_LINE] as Array[String])


func test_the_book_and_history_open_from_settings_and_keep_its_tab_lit() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"settings")
	await wait_frames(2)
	assert_not_null(screen.settings_page.find_child("BookButton", true, false))
	screen.settings_page.book_pressed.emit()
	await wait_frames(2)
	assert_eq(screen.current_page, &"codex")
	assert_eq(screen.tab_bar.active, &"settings", "the Book sits behind Settings")
	screen.open_page(&"settings")
	screen.settings_page.history_pressed.emit()
	await wait_frames(2)
	assert_eq(screen.current_page, &"history")
	assert_eq(screen.tab_bar.active, &"settings")


func test_the_bottom_bar_runs_edge_to_edge_with_no_margin_around_it() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var rect: Rect2 = screen.tab_bar.get_global_rect()
	var whole: Rect2 = screen.get_global_rect()
	assert_almost_eq(rect.position.x, whole.position.x, 0.5, "flush with the left edge")
	assert_almost_eq(rect.end.x, whole.end.x, 0.5, "flush with the right edge")
	assert_almost_eq(rect.end.y, whole.end.y, 0.5, "flush with the bottom edge")
