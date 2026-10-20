extends GutTest
## The main screen: logo and profile on top, the hero mode carousel, low main actions and a tab bar.

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
	assert_not_null(screen.find_child("TopBar", true, false), "a top bar with the logo and who you are")
	assert_not_null(screen.find_child("Logo", true, false), "the game's logo")
	assert_not_null(screen.profile_chip, "your profile")
	assert_not_null(screen.carousel, "the mode carousel")
	assert_eq(screen.mode_name_label.text, "Hotspot", "hotspot is the default mode")
	assert_null(screen.find_child("MapButton", true, false), "no map button")
	assert_eq(screen.primary_button.text, "Create room")
	assert_eq(screen.secondary_button.text, "Join room")
	assert_false(screen.primary_button.get_parent().is_ancestor_of(screen.settings_page), "quit stays out of the main action area")



func test_the_tab_bar_holds_play_book_history_and_settings_along_the_bottom() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var ids: Array[StringName] = [&"home", &"codex", &"history", &"settings"]
	var last_x: float = -1.0
	for id: StringName in ids:
		var tab: Rect2 = screen.corner_buttons[id].get_global_rect()
		assert_gt(tab.position.x, last_x, "%s is right of the one before it" % id)
		last_x = tab.position.x
		assert_gt(tab.position.y, 1280.0 * 0.85, "%s is at the bottom, under the thumb" % id)
		assert_gte(tab.size.y, 88.0, "%s is tall enough for a thumb" % id)
		assert_gte(tab.size.x, 120.0, "%s is wide enough for a thumb" % id)
	assert_eq(screen.tab_bar.active, &"home", "Play is the page you start on")
	assert_gt(screen.carousel.get_global_rect().position.y, screen.profile_chip.get_global_rect().position.y, "the cards are under the top bar")
	assert_null(screen.find_child("BottomNav", true, false), "the old bottom nav is gone")



func test_the_centre_card_stays_centred_on_the_screen() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	for index: int in 3:
		strip.select(index, false)
		assert_almost_eq(strip.cards[index].get_global_rect().get_center().x, 360.0, 2.0, "the chosen card is centred on the screen")
	strip.select(0, false)
	assert_gt(strip.cards[0].get_global_rect().size.y, 540.0, "the centre card is large")



func test_the_cards_show_the_owners_pictures_and_no_text() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	for card: Button in screen.carousel.cards:
		var art: TextureRect = card.find_child("Art", true, false) as TextureRect
		assert_not_null(art, "%s has its picture" % ChoiceCard.choice_of(card))
		assert_gte(art.texture.get_size().x / art.texture.get_size().y, 0.625, "the picture is at least as wide as the card and is cropped to fit")
		assert_eq(art.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_COVERED)
		assert_false((card.get_node("Choice") as Label).visible, "no name on the card")
		assert_false((card.get_node("Heading") as Label).visible, "no heading on the card")
		assert_true(art.material is ShaderMaterial, "the picture follows the card's rounded corners")
	for id: String in ["hotspot", "online", "practice"]:
		assert_true(ResourceLoader.exists("res://assets/ui/cards/%s.webp" % id), id)


func test_the_cards_are_large_and_every_one_is_always_visible_stacked_behind_the_chosen_one() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	var chosen: Rect2 = strip.cards[0].get_global_rect()
	assert_almost_eq(chosen.size.x / chosen.size.y, 2.5 / 4.0, 0.01, "2.5:4")
	assert_gt(chosen.size.y, 540.0, "large")
	for index: int in 3:
		strip.select(index, false)
		for card: Button in strip.cards:
			assert_gt(card.modulate.a, 0.8, "%s is only slightly see-through" % ChoiceCard.choice_of(card))
			assert_gt(card.modulate.r, 0.25, "and never too dark to see")
			assert_gt(card.scale.x, 0.6)
		var middle: Rect2 = strip.cards[index].get_global_rect()
		assert_almost_eq(middle.get_center().x, 360.0, 2.0, "the chosen card is in the middle of the screen")
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
	assert_gt(screen.carousel.get_global_rect().position.y, screen.profile_chip.get_global_rect().position.y, "the cards are under the top bar")
	assert_gt(create.size.x + join.size.x, 600.0)
	var title: Rect2 = screen.mode_name_label.get_global_rect()
	var phrase: Rect2 = screen.mode_text_label.get_global_rect()
	var cards_bottom: float = screen.carousel.cards[0].get_global_rect().end.y
	assert_lte(title.position.y - cards_bottom, 96.0, "mode title belongs with its illustration (a little air for the glow)")
	assert_gt(title.position.y, cards_bottom, "under the cards")
	assert_lt(phrase.end.y, create.position.y - 10.0, "and clear of the buttons")
	assert_almost_eq(title.get_center().x, 360.0, 2.0, "centred left to right")
	assert_almost_eq(phrase.get_center().x, 360.0, 2.0)


func test_a_tab_opens_its_page_and_play_returns_home() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	for id: StringName in [&"codex", &"history", &"settings"]:
		screen.corner_buttons[id].pressed.emit()
		assert_eq(screen.current_page, id)
		assert_true(screen.pages[id].visible, "%s page is open" % id)
		assert_false(screen.carousel.is_visible_in_tree(), "over the home page")
		for other: StringName in screen.pages:
			assert_eq(screen.pages[other].visible, other == id)
		assert_eq(screen.tab_bar.active, id, "its tab is lit")
		screen.corner_buttons[&"home"].pressed.emit()
		assert_eq(screen.current_page, &"")
		assert_true(screen.carousel.is_visible_in_tree(), "back on the home page")



func test_the_logo_and_profile_show_on_play_only_so_pages_get_the_height() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var top: Control = screen.find_child("TopBar", true, false) as Control
	assert_true(top.is_visible_in_tree(), "Play shows the logo and who you are")
	var play_height: float = screen.get_node("Safe").get_global_rect().size.y
	screen.open_page(&"history")
	await wait_frames(2)
	assert_false(top.is_visible_in_tree(), "a page has its own title")
	assert_gte(screen.pages[&"history"].get_global_rect().position.y, 0.0)
	assert_lt(screen.pages[&"history"].get_global_rect().position.y, 60.0, "and starts near the top of the screen")
	screen.close_page()
	await wait_frames(2)
	assert_true(top.is_visible_in_tree(), "Play brings them back")
	assert_gt(play_height, 0.0)


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
	assert_eq(ChoiceCard.choice_of(strip.cards[0]), "Hotspot", "hotspot first, not online")
	assert_eq(ChoiceCard.choice_of(strip.cards[1]), "Online")
	assert_eq(ChoiceCard.choice_of(strip.cards[2]), "Practice")
	var middle: Button = strip.cards[0]
	var next: Button = strip.cards[1]
	assert_almost_eq(middle.size.x / middle.size.y, 2.5 / 4.0, 0.01, "2.5:4 cards")
	assert_almost_eq(middle.get_global_rect().get_center().x, 360.0, 2.0, "the chosen card is in the middle of the screen")
	assert_eq(middle.modulate.a, 1.0)
	assert_lt(next.modulate.r, middle.modulate.r - 0.2, "the next card is clearly darker: depth")
	assert_lt(next.modulate.a, middle.modulate.a, "and slightly see-through too")
	assert_lt(next.position.y, middle.position.y, "and sits higher")
	assert_lt(next.scale.x, middle.scale.x, "and smaller")
	assert_lt(next.z_index, middle.z_index, "and behind")
	assert_gt(next.get_global_rect().get_center().x, middle.get_global_rect().get_center().x, "and to its right")
	strip.select(1, false)
	assert_eq(strip.cards[1].modulate.r, 1.0, "the chosen card comes forward at full brightness")
	assert_lt(strip.cards[0].modulate.r, 1.0)
	assert_lt(strip.cards[0].get_global_rect().get_center().x, strip.cards[1].get_global_rect().get_center().x, "the previous card is on the left")
	assert_eq(screen.mode_name_label.text, "Online", "the details follow the chosen card")
	assert_eq(screen.primary_button.text, "Create room")
	strip.select(2, false)
	assert_eq(screen.mode_name_label.text, "Practice")
	assert_eq(screen.primary_button.text, "Start practice", "practice just starts")
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
	tap.position = Vector2(strip.cards[1].get_global_rect().position.x - strip.get_global_rect().position.x + 12.0, strip.size.y / 2.0)
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
	assert_eq(screen.mode_text_label.get_line_count(), 1, "the larger text still fits one line")


func test_the_screen_shows_no_version_online_caption_or_server_line() -> void:
	var screen: TitleScreen = _screen()
	var text: String = _all_text(screen)
	assert_false(text.contains("Server"), "no server line")
	assert_false(text.contains("No server"), "no server hint")
	assert_eq(text.count("v0."), 0, "no version")
	assert_null(screen.find_child("VersionLabel", true, false))
	assert_null(screen.find_child("Wordmark", true, false), "no text wordmark: the logo is the owner's picture")


func _all_text(root: Node) -> String:
	var text: String = ""
	for label: Node in root.find_children("*", "Label", true, false):
		if (label as Label).is_visible_in_tree():
			text += (label as Label).text + " "
	return text


func test_the_background_is_the_owners_diamond_picture_covering_the_screen() -> void:
	var screen: TitleScreen = _screen()
	var picture: TextureRect = _find(screen.backdrop, "Picture") as TextureRect
	assert_eq(picture.texture, UiBackdrop.PICTURE)
	assert_eq(picture.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_COVERED, "cropped, never stretched")
	assert_eq(UiBackdrop.PICTURE.get_size(), Vector2(941.0, 1672.0))



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
	assert_eq((_find(page, "QuitButton") as Button).text, "Quit game")
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
	assert_eq(stay.text, "Cancel")
	assert_eq(leave.text, "Quit")
	stay.pressed.emit()
	assert_eq(quit_calls[0], 0, "Cancel keeps playing")
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


func test_hotspot_create_opens_the_checking_dialog() -> void:
	var screen: TitleScreen = _screen()
	screen.primary_button.pressed.emit()
	assert_not_null(screen.dialog, "a dialog")
	assert_eq(screen.dialog.name, "HotspotCheckDialog")
	assert_true(screen.dialog.is_open)


func test_hotspot_join_never_asks_for_a_code() -> void:
	var screen: TitleScreen = _screen()
	screen.open_lobby_action = func(_intent: int) -> void: pass
	screen.secondary_button.pressed.emit()
	assert_eq(screen.dialog.name, "HotspotJoinDialog")
	assert_eq(screen.dialog.find_children("*", "LineEdit", true, false).size(), 0)


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


func test_modals_and_tints_draw_above_every_component_of_the_main_screen() -> void:
	var screen: TitleScreen = _screen()
	var highest_card: int = -1000
	for card: Button in screen.carousel.cards:
		highest_card = maxi(highest_card, card.z_index)
	assert_gt(MenuDialog.MODAL_Z, highest_card + 100, "far above the carousel cards")
	screen.secondary_button.pressed.emit()
	assert_eq(screen.dialog.z_index, MenuDialog.MODAL_Z, "a dialog (and its dim) is on top")
	screen.dialog.close(false)
	screen._show_loading("GETTING READY")
	assert_eq((screen.find_child("Loading", true, false) as Control).z_index, MenuDialog.MODAL_Z, "so is the loading tint")
