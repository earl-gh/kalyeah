extends GutTest
## The main screen: the K! logo and settings on top, a vertical map carousel on the left, the map's details and the action buttons on the right.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _saved_name: String
var _saved_avatar: String
var _saved_volume: float
var _saved_url: String
var _saved_vibration: bool


func before_each() -> void:
	_saved_name = Session.player_name
	_saved_avatar = Session.avatar_id
	_saved_volume = Settings.volume
	_saved_url = Settings.server_url
	_saved_vibration = Settings.vibration


func after_each() -> void:
	Session.player_name = _saved_name
	Session.avatar_id = _saved_avatar
	Session.save()
	Settings.set_volume(_saved_volume)
	Settings.set_vibration(_saved_vibration)
	Settings.server_url = _saved_url
	Session.lobby_hotspot = false
	Session.menu_mode = 0


func _screen() -> TitleScreen:
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	add_child(screen)
	return screen


func _find(root: Node, node_name: String) -> Node:
	return root.find_child(node_name, true, false)


func test_a_normal_run_is_not_the_server() -> void:
	assert_false(TitleScreen.is_server_run(), "tests run without --server or the dedicated_server feature")


func test_server_scene_and_lobby_scene_exist() -> void:
	assert_true(ResourceLoader.exists(TitleScreen.SERVER_SCENE))
	assert_true(ResourceLoader.exists(TitleScreen.LOBBY_SCENE))
	assert_true(ResourceLoader.exists(TitleScreen.PRACTICE_SCENE))


func _phone_screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func test_the_screen_has_everything_the_owner_asked_for() -> void:
	var screen: TitleScreen = _screen()
	assert_null(screen.find_child("TopBar", true, false), "no top row: no K! and no settings icon")
	assert_not_null(screen.carousel, "the mode carousel")
	assert_eq(screen.mode_name_label.text, "HOTSPOT", "hotspot is the default mode")
	assert_null(screen.find_child("MapButton", true, false), "no map button")
	assert_eq(screen.primary_button.text, "CREATE")
	assert_eq(screen.secondary_button.text, "JOIN")
	assert_null(screen.quit_button, "quit stays out of the main action area")


func test_the_top_row_has_the_book_left_the_logo_in_the_middle_and_history_and_settings_right() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var book: Rect2 = screen.corner_buttons[&"codex"].get_global_rect()
	var history: Rect2 = screen.corner_buttons[&"history"].get_global_rect()
	var settings: Rect2 = screen.corner_buttons[&"settings"].get_global_rect()
	var mark: Rect2 = screen.logo.get_global_rect()
	assert_lt(book.end.x, mark.position.x, "the book is left of the logo")
	assert_gt(history.position.x, mark.end.x - 1.0, "history is right of the logo")
	assert_lt(history.position.x, settings.position.x, "history, then settings")
	assert_almost_eq(book.position.x, TitleScreen.SIDE_MARGIN, 1.0, "the book is at the top left")
	assert_almost_eq(settings.end.x, 720.0 - TitleScreen.SIDE_MARGIN, 1.0, "settings is at the top right")
	assert_almost_eq(book.get_center().y, mark.get_center().y, 3.0, "the buttons are level with the logo")
	assert_almost_eq(history.get_center().y, mark.get_center().y, 3.0)
	assert_almost_eq(settings.get_center().y, mark.get_center().y, 3.0)
	for id: StringName in screen.corner_buttons:
		assert_gte(screen.corner_buttons[id].get_global_rect().size.y, 80.0, "%s is tall enough for a thumb" % id)
	assert_almost_eq(mark.get_center().x, 360.0, 2.0, "the logo is at the centre")
	assert_gt(mark.position.y, 40.0, "lowered from the very top")
	assert_eq(screen.logo.texture, load("res://assets/ui/kalyeah_logo.png"), "the owner's KALYEAH! logo")
	var shown_width: float = minf(mark.size.x, mark.size.y * 1000.0 / 357.0)
	assert_lt(shown_width, 330.0, "the picture itself is a bit smaller than before")
	assert_gt(shown_width, 220.0, "but still clear")
	assert_null(screen.find_child("BottomNav", true, false), "no bottom nav bar")


func test_the_cards_are_large_and_every_one_is_always_visible_stacked_behind_the_chosen_one() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	var chosen: Rect2 = strip.cards[0].get_global_rect()
	assert_almost_eq(chosen.size.x / chosen.size.y, 0.75, 0.01, "3:4")
	assert_gt(chosen.size.x, 290.0, "large")
	for index: int in 3:
		strip.select(index, false)
		for card: Button in strip.cards:
			assert_gt(card.modulate.a, 0.4, "%s is always visible" % ChoiceCard.choice_of(card))
			assert_gt(card.scale.x, 0.6)
		var middle: Rect2 = strip.cards[index].get_global_rect()
		assert_almost_eq(middle.get_center().x, 360.0, 2.0, "the chosen card is in the middle")
		for other: int in 3:
			if other == index:
				continue
			var behind: Rect2 = strip.cards[other].get_global_rect()
			assert_lt(strip.cards[other].z_index, strip.cards[index].z_index, "behind")
			assert_lt(absf(behind.get_center().x - middle.get_center().x), 260.0, "pushed only a little to the side, mostly behind")
			assert_gt(behind.get_center().x - middle.get_center().x if other > index else middle.get_center().x - behind.get_center().x, 0.0, "later cards on the right, earlier on the left")
			assert_gte(behind.position.x, -5.0, "inside the screen")
			assert_lte(behind.end.x, 725.0)


func test_the_actions_sit_low_on_the_screen_but_not_at_the_very_bottom() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var create: Rect2 = screen.primary_button.get_global_rect()
	var join: Rect2 = screen.secondary_button.get_global_rect()
	assert_almost_eq(create.position.y, join.position.y, 1.0, "create and join are in one row")
	assert_gt(create.position.y, 1280.0 * 0.75, "in the bottom part")
	assert_lt(create.end.y, 1280.0 - 60.0, "but with room under them")
	assert_lt(screen.carousel.get_global_rect().end.y, create.position.y, "below the cards")
	assert_gt(screen.carousel.get_global_rect().position.y, screen.logo.get_global_rect().end.y - 1.0, "the cards are under the top row")
	assert_gt(create.size.x + join.size.x, 600.0)


func test_a_corner_button_opens_its_page_and_back_returns_home() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	for corner: Dictionary in TitleScreen.CORNERS:
		var id: StringName = corner["id"]
		screen.corner_buttons[id].pressed.emit()
		assert_eq(screen.current_page, id)
		assert_true(screen.pages[id].visible, "%s page is open" % id)
		assert_false(screen.carousel.is_visible_in_tree(), "over the home page")
		for other: StringName in screen.pages:
			assert_eq(screen.pages[other].visible, other == id)
		screen.back_button.pressed.emit()
		assert_eq(screen.current_page, &"")
		assert_true(screen.carousel.is_visible_in_tree(), "back on the home page")


func test_android_back_closes_a_page_before_asking_to_quit() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"codex")
	screen._go_back()
	assert_eq(screen.current_page, &"")
	assert_null(screen.dialog, "no quit question yet")
	screen._go_back()
	assert_not_null(screen.dialog, "from the home page it asks to quit")


func test_the_carousel_is_hotspot_online_practice_with_the_chosen_card_in_front() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	assert_eq(strip.cards.size(), 3)
	assert_eq(ChoiceCard.choice_of(strip.cards[0]), "HOTSPOT", "hotspot first, not online")
	assert_eq(ChoiceCard.choice_of(strip.cards[1]), "ONLINE")
	assert_eq(ChoiceCard.choice_of(strip.cards[2]), "PRACTICE")
	var middle: Button = strip.cards[0]
	var next: Button = strip.cards[1]
	assert_almost_eq(middle.size.x / middle.size.y, 0.75, 0.01, "3:4 cards")
	assert_almost_eq(middle.get_global_rect().get_center().x, 360.0, 2.0, "the chosen card is in the middle")
	assert_eq(middle.modulate.a, 1.0)
	assert_lt(next.modulate.a, 1.0, "the next card is see-through")
	assert_lt(next.scale.x, middle.scale.x, "and smaller")
	assert_lt(next.z_index, middle.z_index, "and behind")
	assert_gt(next.get_global_rect().get_center().x, middle.get_global_rect().get_center().x, "and to its right")
	strip.select(1, false)
	assert_eq(strip.cards[1].modulate.a, 1.0, "the chosen card comes forward")
	assert_lt(strip.cards[0].modulate.a, 1.0)
	assert_lt(strip.cards[0].get_global_rect().get_center().x, strip.cards[1].get_global_rect().get_center().x, "the previous card is on the left")
	assert_eq(screen.mode_name_label.text, "ONLINE", "the details follow the chosen card")
	assert_eq(screen.primary_button.text, "CREATE")
	strip.select(2, false)
	assert_eq(screen.mode_name_label.text, "PRACTICE")
	assert_eq(screen.primary_button.text, "START", "practice just starts")
	assert_false(screen.secondary_button.visible, "and has nothing to join")


func test_swiping_and_tapping_the_carousel_changes_the_card() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	var wheel: InputEventMouseButton = InputEventMouseButton.new()
	wheel.button_index = MOUSE_BUTTON_WHEEL_DOWN
	wheel.pressed = true
	strip._gui_input(wheel)
	assert_eq(strip.index, 1, "the wheel moves to the next card")
	await wait_seconds(0.4)
	var press: InputEventMouseButton = InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.position = Vector2(strip.size.x / 2.0 + 200.0, 100.0)
	press.pressed = true
	strip._gui_input(press)
	var drag: InputEventMouseMotion = InputEventMouseMotion.new()
	drag.position = Vector2(strip.size.x / 2.0 + 200.0 - MenuCarousel.STEP * 1.1, 100.0)
	strip._gui_input(drag)
	press.pressed = false
	press.position = drag.position
	strip._gui_input(press)
	assert_eq(strip.index, 2, "swiping left scrolls on to the third card")
	await wait_seconds(0.4)
	var tap: InputEventMouseButton = InputEventMouseButton.new()
	tap.button_index = MOUSE_BUTTON_LEFT
	tap.position = Vector2(strip.size.x / 2.0 - 200.0, strip.size.y / 2.0)
	tap.pressed = true
	strip._gui_input(tap)
	tap.pressed = false
	strip._gui_input(tap)
	assert_eq(strip.index, 1, "tapping the card on the left brings it to the middle")


func test_create_and_join_share_one_row_and_the_texts_are_one_line() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var create: Rect2 = screen.primary_button.get_global_rect()
	var join: Rect2 = screen.secondary_button.get_global_rect()
	assert_almost_eq(create.position.y, join.position.y, 1.0, "one row")
	assert_lt(create.end.x, join.position.x, "create then join")
	assert_false(screen.primary_button.text.contains("ROOM"), "no word room")
	assert_null(_find(screen, "ModeLabel"))
	assert_false(_all_text(screen).contains("Game mode"), "no 'Game mode' caption")
	for card: Button in screen.carousel.cards:
		assert_false((card.get_node("Heading") as Label).visible, "no 'Mode' on the cards")
	for mode: Dictionary in TitleScreen.MODES:
		assert_lt(String(mode["text"]).length(), 34, "one short line")
	assert_lt(screen.mode_text_label.get_minimum_size().y, 40.0, "it stays on one line")


func test_the_screen_shows_no_version_online_caption_or_server_line() -> void:
	var screen: TitleScreen = _screen()
	var text: String = _all_text(screen)
	assert_false(text.contains("Server"), "no server line")
	assert_false(text.contains("No server"), "no server hint")
	assert_eq(text.count("v0."), 0, "no version")
	assert_null(screen.find_child("VersionLabel", true, false))
	assert_null(screen.find_child("Wordmark", true, false), "the game name was removed")


func _all_text(root: Node) -> String:
	var text: String = ""
	for label: Node in root.find_children("*", "Label", true, false):
		if (label as Label).is_visible_in_tree():
			text += (label as Label).text + " "
	return text


func test_the_background_is_the_owners_diamond_picture_covering_the_screen() -> void:
	var screen: TitleScreen = _screen()
	var background: TextureRect = _find(screen, "Background") as TextureRect
	assert_eq(background.texture, TitleScreen.BACKGROUND)
	assert_eq(background.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_COVERED, "cropped, never stretched")
	assert_eq(TitleScreen.BACKGROUND.get_size(), Vector2(941.0, 1672.0))


func test_every_tap_target_is_big_enough_for_a_thumb() -> void:
	var screen: TitleScreen = _screen()
	# 720 px design width is about 390 dp on a phone: 88 px is about 48 dp
	for button: Button in [screen.primary_button, screen.secondary_button]:
		assert_gte(button.custom_minimum_size.y, 88.0, "%s is tall enough" % button.name)


func test_a_new_player_gets_a_name_and_a_face() -> void:
	Session.player_name = ""
	Session.avatar_id = ""
	Session.ensure_profile()
	assert_false(Session.player_name.is_empty())
	assert_lte(Session.player_name.length(), Session.MAX_NAME_LENGTH)
	assert_true(Session.AVATARS.has(Session.avatar_id))
	var name_now: String = Session.player_name
	Session.ensure_profile()
	assert_eq(Session.player_name, name_now, "an existing profile is kept")


func test_every_character_has_a_face_for_the_avatar() -> void:
	for kid: String in Session.AVATARS:
		assert_not_null(KidModel.minimap_face(StringName(kid)), "%s has a face for the avatar" % kid)


func test_renaming_saves_a_clean_name_and_refuses_an_empty_one() -> void:
	var screen: TitleScreen = _screen()
	var card: MenuDialog = screen.open_name_editor()
	var field: LineEdit = _find(card, "NameField") as LineEdit
	assert_eq(field.max_length, Session.MAX_NAME_LENGTH)
	field.text = "   "
	field.text_submitted.emit(field.text)
	assert_true(card.is_open, "an empty name is refused and the card stays open")
	field.text = "  Maria  "
	field.text_submitted.emit(field.text)
	assert_eq(Session.player_name, "Maria")
	assert_false(card.is_open)
	assert_eq(Session.clean_name("A very long name indeed"), "A very long", "cut to 12 letters, no trailing space")


func test_cancelling_the_rename_keeps_the_old_name() -> void:
	Session.player_name = "Pedro"
	var screen: TitleScreen = _screen()
	var card: MenuDialog = screen.open_name_editor()
	(_find(card, "NameField") as LineEdit).text = "Changed"
	card.close(false)
	assert_eq(Session.player_name, "Pedro")


func _settings_open(screen: TitleScreen) -> SettingsPage:
	screen.open_page(&"settings")
	return screen.settings_page


func test_settings_change_the_volume() -> void:
	var screen: TitleScreen = _screen()
	var page: SettingsPage = _settings_open(screen)
	var slider: HSlider = _find(page, "VolumeSlider") as HSlider
	slider.value = 0.4
	assert_almost_eq(Settings.volume, 0.4, 0.001)
	assert_almost_eq(AudioServer.get_bus_volume_db(0), linear_to_db(0.4), 0.01, "the master volume follows")
	assert_null(_find(page, "ServerField"), "no server address to type")
	assert_eq((_find(page, "QuitButton") as Button).text, "QUIT GAME")
	slider.value = 0.0
	assert_true(AudioServer.is_bus_mute(0), "zero is silent")


func test_vibration_is_only_offered_on_phones() -> void:
	var screen: TitleScreen = _screen()
	var page: SettingsPage = _settings_open(screen)
	assert_eq(_find(page, "VibrationToggle") != null, Settings.can_vibrate())
	assert_false(Settings.can_vibrate(), "tests do not run on a phone")


func test_quit_asks_first_and_only_quits_on_yes() -> void:
	var screen: TitleScreen = _screen()
	var quit_calls: Array[int] = [0]
	screen.quit_action = func() -> void: quit_calls[0] += 1
	var card: MenuDialog = screen.open_quit_confirm()
	assert_eq(quit_calls[0], 0, "opening the question does not quit")
	var buttons: Array[Node] = card.find_children("*", "Button", true, false)
	var stay: Button = buttons[0] as Button
	var leave: Button = buttons[1] as Button
	assert_eq(stay.text, "CANCEL")
	assert_eq(leave.text, "QUIT")
	stay.pressed.emit()
	assert_eq(quit_calls[0], 0, "CANCEL keeps playing")
	assert_false(card.is_open)
	var again: MenuDialog = screen.open_quit_confirm()
	(again.find_children("*", "Button", true, false)[1] as Button).pressed.emit()
	assert_eq(quit_calls[0], 1, "QUIT quits")


func test_only_one_dialog_is_open_at_a_time() -> void:
	var screen: TitleScreen = _screen()
	var first: MenuDialog = screen.open_name_editor()
	var second: MenuDialog = screen.open_quit_confirm()
	assert_false(first.is_open)
	assert_true(second.is_open)
	assert_eq(screen.dialog, second)


func test_safe_insets_are_never_negative_or_huge() -> void:
	var screen: TitleScreen = _screen()
	var inset: Vector4 = screen.safe_insets()
	for value: float in [inset.x, inset.y, inset.z, inset.w]:
		assert_between(value, 0.0, 120.0)


func test_hotspot_creates_and_joins_without_an_address_or_code() -> void:
	var screen: TitleScreen = _screen()
	var opened: Array = []
	screen.open_lobby_action = func(intent: int) -> void: opened.append(intent)
	screen.primary_button.pressed.emit()
	assert_true(Session.lobby_hotspot, "the lobby is in hotspot mode")
	assert_eq(Session.lobby_intent, LobbyScreen.Intent.CREATE)
	screen.secondary_button.pressed.emit()
	assert_eq(Session.lobby_intent, LobbyScreen.Intent.JOIN)
	assert_eq(opened.size(), 2)


func test_online_opens_the_lobby_on_the_game_server() -> void:
	var screen: TitleScreen = _screen()
	var opened: Array = []
	screen.open_lobby_action = func(intent: int) -> void: opened.append(intent)
	screen.carousel.select(1, false)
	screen.primary_button.pressed.emit()
	assert_false(Session.lobby_hotspot)
	assert_eq(Session.lobby_intent, LobbyScreen.Intent.CREATE)
	screen.secondary_button.pressed.emit()
	assert_eq(Session.lobby_intent, LobbyScreen.Intent.JOIN)
	assert_eq(opened.size(), 2)


func test_private_addresses_use_plain_ws_and_public_ones_wss() -> void:
	assert_eq(Settings.normalize_url("192.168.43.1:9080"), "ws://192.168.43.1:9080")
	assert_eq(Settings.normalize_url("10.0.0.5:9080"), "ws://10.0.0.5:9080")
	assert_eq(Settings.normalize_url("172.20.10.1:9080"), "ws://172.20.10.1:9080", "an iPhone hotspot")
	assert_eq(Settings.normalize_url("172.32.0.1"), "wss://172.32.0.1", "not a private range")
	assert_eq(Settings.normalize_url("example.org"), "wss://example.org")
	assert_eq(Settings.normalize_url("wss://x"), "wss://x")
