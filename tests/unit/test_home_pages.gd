extends GutTest
## The pages behind the main screen's section buttons: characters, weapons, history, settings.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _saved_avatar: String
var _saved_history: String


func before_each() -> void:
	_saved_avatar = Session.avatar_id
	_saved_history = MatchHistory.path
	MatchHistory.path = "user://history_test.cfg"
	MatchHistory.clear()


func after_each() -> void:
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


func test_the_characters_page_picks_your_face() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"codex")
	await wait_frames(2)
	var page: CharactersPage = screen.characters_page
	assert_eq(page.cards.size(), Session.AVATARS.size())
	page.cards["toni"].pressed.emit()
	assert_eq(Session.avatar_id, "toni")
	page.cards["popoy"].pressed.emit()
	assert_eq(Session.avatar_id, "popoy")


func test_the_book_has_a_characters_tab_and_a_weapons_tab() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"codex")
	await wait_frames(2)
	assert_true(screen.characters_page.visible)
	assert_false(screen.weapons_page.visible)
	screen.codex_page.tab_buttons[&"weapons"].pressed.emit()
	assert_false(screen.characters_page.visible)
	assert_true(screen.weapons_page.visible)
	screen.codex_page.tab_buttons[&"characters"].pressed.emit()
	assert_true(screen.characters_page.visible)


func test_the_weapons_page_lists_every_weapon_with_its_text() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"codex")
	screen.codex_page.show_tab(&"weapons")
	await wait_frames(2)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var page: WeaponsPage = screen.weapons_page
	assert_eq(page.rows.size(), rules.weapons.size())
	for i: int in rules.weapons.size():
		assert_eq((page.rows[i].find_child("Name", true, false) as Label).text, rules.weapons[i].display_name.to_upper())
		assert_eq((page.rows[i].find_child("About", true, false) as Label).text, rules.weapons[i].description)


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
	for label: Node in screen.history_page.find_children("Result", "Label", true, false):
		if not label.is_queued_for_deletion():
			results.append((label as Label).text)
	assert_eq(results, ["DEFEAT", "VICTORY"], "the newest battle is first")


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


func test_the_settings_page_has_profile_sound_and_quit_but_no_server_address() -> void:
	var screen: TitleScreen = _phone_screen()
	screen.open_page(&"settings")
	await wait_frames(2)
	var page: SettingsPage = screen.settings_page
	assert_true(page.visible)
	assert_not_null(page.find_child("VolumeSlider", true, false), "sound")
	assert_null(page.find_child("ServerField", true, false), "no server address: it is built into the game")
	assert_not_null(page.profile_button, "profile")
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
