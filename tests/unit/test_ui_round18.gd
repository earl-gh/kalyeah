extends GutTest
## Round 18 of the interface plan: depth. Weapon names that follow one scheme, developer tools a player never meets by
## accident, names for a screen reader, and the structure for a second language.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const DT: float = 1.0 / 30.0

var _dev: bool
var _language: String
var _loadout: Array[StringName]


func before_each() -> void:
	_dev = Settings.developer
	_language = Settings.language
	_loadout = Session.saved_loadout.duplicate()
	Settings.set_developer(false)


func after_each() -> void:
	Settings.set_developer(_dev)
	TranslationServer.set_locale("en")
	Settings.language = _language
	Session.saved_loadout = _loadout
	Session.save()


func _rules() -> GameRules:
	return load("res://data/rules/game_rules.tres") as GameRules


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 5:
		practice.advance(DT)
	return practice


func _phone() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


# ---- weapon names --------------------------------------------------------------------------

func test_weapon_names_follow_one_scheme_name_then_role() -> void:
	var pattern: RegEx = RegEx.create_from_string("^[A-Z][a-z]+( · [A-Z][A-Za-z ]+)?$")
	for def: WeaponDef in _rules().weapons:
		assert_not_null(pattern.search(def.display_name), "'%s' is Name or Name · Role" % def.display_name)
		assert_false(def.display_name.contains("("), "%s: no brackets" % def.id)


func test_the_light_and_heavy_of_a_game_are_named_alike() -> void:
	for game: String in ["Bato", "Gunting", "Tsinelas"]:
		var light: WeaponDef = load("res://data/weapons/%s_light.tres" % game.to_lower()) as WeaponDef
		var heavy: WeaponDef = load("res://data/weapons/%s_heavy.tres" % game.to_lower()) as WeaponDef
		assert_eq(light.display_name, "%s · Light" % game)
		assert_eq(heavy.display_name, "%s · Heavy" % game)


func test_no_two_weapons_share_a_name_or_a_short_name() -> void:
	var names: Dictionary[String, bool] = {}
	var shorts: Dictionary[String, bool] = {}
	for def: WeaponDef in _rules().weapons:
		assert_false(names.has(def.display_name), def.display_name)
		assert_false(shorts.has(def.short_name), "the short name %s says which weapon it is" % def.short_name)
		names[def.display_name] = true
		shorts[def.short_name] = true


# ---- the developer switch ------------------------------------------------------------------

func test_the_gallery_is_hidden_from_players_until_the_developer_switch_is_on() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(2)
	assert_false(screen.settings_page.gallery_button.visible, "a player does not see it")
	Settings.set_developer(true)
	assert_true(screen.settings_page.gallery_button.visible)
	Settings.set_developer(false)
	assert_false(screen.settings_page.gallery_button.visible)


func test_seven_quick_taps_on_the_settings_title_switch_it() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(2)
	var header: Control = screen.settings_page.find_child("Header", true, false) as Control
	var tap: InputEventMouseButton = InputEventMouseButton.new()
	tap.pressed = true
	tap.button_index = MOUSE_BUTTON_LEFT
	for i: int in 6:
		header.gui_input.emit(tap)
	assert_false(Settings.developer, "six is not enough")
	header.gui_input.emit(tap)
	assert_true(Settings.developer, "the seventh switches it on")
	assert_true(screen.settings_page.gallery_button.visible)
	for i: int in 7:
		header.gui_input.emit(tap)
	assert_false(Settings.developer, "seven more switch it off")


func test_slow_taps_do_not_count() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(2)
	var header: Control = screen.settings_page.find_child("Header", true, false) as Control
	var tap: InputEventMouseButton = InputEventMouseButton.new()
	tap.pressed = true
	tap.button_index = MOUSE_BUTTON_LEFT
	for i: int in 10:
		screen.settings_page._last_header_tap = Time.get_ticks_msec() - 5000
		header.gui_input.emit(tap)
	assert_false(Settings.developer)


func test_the_developer_switch_is_saved() -> void:
	Settings.set_developer(true)
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	assert_true(file.get_value("dev", "developer") as bool)


# ---- names for a screen reader ---------------------------------------------------------------

func test_a_control_with_no_text_is_not_named_until_it_is_given_a_name() -> void:
	var icon: Button = autofree(Button.new()) as Button
	assert_false(UiA11y.is_named(icon))
	UiA11y.name_it(icon, "Match settings", "Opens the menu")
	assert_true(UiA11y.is_named(icon))
	assert_eq(icon.accessibility_description, "Opens the menu")
	var words: Button = autofree(Button.new()) as Button
	words.text = "Play"
	assert_true(UiA11y.is_named(words), "its own text is read")


func test_the_match_hud_is_named_for_a_screen_reader() -> void:
	var practice: PracticeMatch = _practice()
	var hud: MatchHud = practice.hud
	assert_eq(hud.settings_button.accessibility_name, "Match settings")
	assert_eq(hud.dpad.accessibility_name, "Movement pad")
	assert_true(hud.scoreboard.accessibility_name.begins_with("Score: you 0, them 0"))
	for slot: int in 2:
		var button: AimButton = hud.weapon_buttons[slot]
		assert_true(button.accessibility_name.begins_with("Weapon %d: " % (slot + 1)), button.accessibility_name)
		assert_string_contains(button.accessibility_name, button.sub_text)


func test_a_weapon_button_says_when_it_will_be_ready() -> void:
	var practice: PracticeMatch = _practice()
	var button: AimButton = practice.hud.weapon_buttons[0]
	assert_string_contains(button.accessibility_description, "aim")
	var player: PlayerState = practice.sim.players[practice.local_id]
	player.weapon_cooldowns[0] = 4.5
	practice.advance(DT)
	assert_string_contains(button.accessibility_description, "Ready in 5 seconds")


func test_a_switch_and_a_slider_are_read_by_their_rows_title() -> void:
	var column: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	add_child(column)
	SettingsPage.fill(column)
	var motion: Node = column.find_child("ReducedMotion", true, false)
	assert_eq((motion as Control).accessibility_name, "Reduce motion")
	assert_eq((column.find_child("VolumeSlider", true, false) as Control).accessibility_name, "Sound volume")
	assert_eq((column.find_child("LeftHanded", true, false) as Control).accessibility_name, "Left-handed")


func test_the_home_controls_are_named() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(3)
	assert_eq(screen.carousel.accessibility_name, "Game mode")
	assert_string_contains(screen.profile_chip.accessibility_name, "Your profile")
	for id: StringName in [&"home", &"codex", &"history", &"settings"]:
		assert_false(screen.corner_buttons[id].accessibility_name.is_empty(), "%s tab" % id)


# ---- translation -----------------------------------------------------------------------------

func _translation(locale: String, count: int) -> Translation:
	var translation: Translation = Translation.new()
	translation.locale = locale
	translation.add_message("Stunned", "Nakatulala")
	translation.add_message("First to %d", "Una sa %d")
	for i: int in count:
		translation.add_message("filler %d" % i, "x")
	return translation


func test_drawn_words_go_through_the_translation_and_fall_back_to_english() -> void:
	assert_eq(UiText.t("Stunned"), "Stunned", "no translation: the key itself")
	var fil: Translation = _translation("fil", 5)
	TranslationServer.add_translation(fil)
	TranslationServer.set_locale("fil")
	assert_eq(UiText.t("Stunned"), "Nakatulala")
	assert_eq(UiText.f("First to %d", [3]), "Una sa 3", "the places stay where the translator put them")
	assert_eq(UiText.t("Never translated"), "Never translated")
	TranslationServer.remove_translation(fil)


func test_the_word_over_a_kid_is_translated() -> void:
	var practice: PracticeMatch = _practice()
	var fil: Translation = _translation("fil", 5)
	TranslationServer.add_translation(fil)
	TranslationServer.set_locale("fil")
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].effects.apply(StatusEffects.Type.STUN, 3.0)
	assert_eq(practice.overhead().text_for(PracticeMatch.ENEMY_STAND_ID), "Nakatulala")
	TranslationServer.remove_translation(fil)


func test_a_language_is_offered_only_when_its_translation_has_enough_messages() -> void:
	assert_eq(Settings.available_languages(), ["en"] as Array[String], "English alone until a translation exists")
	var thin: Translation = _translation("fil", 3)
	TranslationServer.add_translation(thin)
	assert_eq(Settings.available_languages(), ["en"] as Array[String], "a few words are not a language")
	TranslationServer.remove_translation(thin)
	var full: Translation = _translation("fil", Settings.MIN_TRANSLATED)
	TranslationServer.add_translation(full)
	assert_eq(Settings.available_languages(), ["en", "fil"] as Array[String])
	TranslationServer.remove_translation(full)


func test_choosing_a_language_changes_the_locale_saves_it_and_refuses_one_that_is_not_offered() -> void:
	var full: Translation = _translation("fil", Settings.MIN_TRANSLATED)
	TranslationServer.add_translation(full)
	watch_signals(Settings)
	Settings.set_language("fil")
	assert_eq(Settings.language, "fil")
	assert_eq(TranslationServer.get_locale(), "fil")
	assert_signal_emit_count(Settings, "language_changed", 1)
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	assert_eq(file.get_value("ui", "language"), "fil")
	Settings.set_language("xx")
	assert_eq(Settings.language, "fil", "not offered: ignored")
	Settings.set_language("en")
	TranslationServer.remove_translation(full)


func test_the_settings_page_shows_a_language_choice_only_when_there_is_a_second_language() -> void:
	var plain: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	add_child(plain)
	SettingsPage.fill(plain)
	assert_null(plain.find_child("LanguageOptions", true, false))
	var full: Translation = _translation("fil", Settings.MIN_TRANSLATED)
	TranslationServer.add_translation(full)
	var offered: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	add_child(offered)
	SettingsPage.fill(offered)
	assert_not_null(offered.find_child("LanguageChoice", true, false))
	TranslationServer.remove_translation(full)


func test_the_string_table_exists_and_covers_the_words_a_player_reads() -> void:
	var text: String = FileAccess.get_file_as_string("res://data/i18n/strings.csv")
	assert_true(text.begins_with("keys,en"), "a header, then one row a string")
	assert_gt(text.split("\n").size(), 200)
	for word: String in ["Play", "Create room", "Join room", "Quit game", "First to %d", "Stunned", "FIRST TO"]:
		assert_true(text.contains("\n%s," % word) or text.contains("\n\"%s\"," % word), "'%s' is a key" % word)
	for def: WeaponDef in _rules().weapons:
		assert_true(text.contains(def.display_name), "%s is a key" % def.display_name)
