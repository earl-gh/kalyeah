extends GutTest
## The main screen: logo and profile on top, the hero mode carousel, low main actions and a tab bar.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const Phones := preload("res://tests/unit/hotspot_phones.gd")

var _saved_name: String
var _saved_avatar: String
var _saved_url: String
var _saved_vibration: bool


func before_each() -> void:
	# an earlier test that left a match changed the scene: its title screen would breathe beside ours
	var left_over: Node = get_tree().current_scene
	if left_over != null:
		get_tree().current_scene = null
		left_over.free()
	_saved_name = Session.player_name
	_saved_avatar = Session.avatar_id
	_saved_url = Settings.server_url
	_saved_vibration = Settings.vibration


func after_each() -> void:
	Session.player_name = _saved_name
	Session.avatar_id = _saved_avatar
	Session.save()
	Settings.set_vibration(_saved_vibration)
	Settings.server_url = _saved_url
	Session.lobby_hotspot = false
	Session.lobby_url = ""
	Session.menu_mode = 1


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
	assert_not_null(screen.find_child("TopBar", true, false), "a top bar with the logo")
	assert_not_null(screen.find_child("Logo", true, false), "the game's logo")
	assert_null(screen.find_child("ProfileChip", true, false), "no profile chip: the profile is edited in Settings (owner)")
	assert_not_null(screen.carousel, "the mode carousel")
	assert_eq(screen.mode_name_label.text, "Local", "hotspot is the default mode")
	assert_null(screen.find_child("MapButton", true, false), "no map button")
	assert_eq(screen.primary_button.text, "Create")
	assert_eq(screen.secondary_button.text, "Join")
	assert_null(screen.find_child("Dots", true, false), "no dot indicators under the subtitle (owner)")
	assert_false(screen.primary_button.get_parent().is_ancestor_of(screen.settings_page), "quit stays out of the main action area")



func test_the_tab_bar_holds_its_five_tabs_along_the_bottom() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var ids: Array[StringName] = [&"street", &"codex", &"home", &"history", &"settings"]
	var last_x: float = -1.0
	for id: StringName in ids:
		var tab: Rect2 = screen.corner_buttons[id].get_global_rect()
		assert_gt(tab.position.x, last_x, "%s is right of the one before it" % id)
		last_x = tab.position.x
		assert_gt(tab.position.y, 1280.0 * 0.8, "%s is at the bottom, under the thumb" % id)
		assert_gte(tab.size.y, 88.0, "%s is tall enough for a thumb" % id)
		assert_gte(tab.size.x, UiMetrics.MIN_TARGET, "%s is wide enough for a thumb" % id)
	assert_eq(screen.tab_bar.active, &"home", "Play is the page you start on")
	assert_gt(screen.carousel.get_global_rect().position.y, (screen.find_child("TopBar", true, false) as Control).get_global_rect().position.y, "the cards are under the top bar")
	assert_null(screen.find_child("BottomNav", true, false), "the old bottom nav is gone")



func test_the_centre_card_stays_centred_on_the_screen() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	for index: int in 3:
		strip.select(index, false)
		assert_almost_eq(strip.cards[index].get_global_rect().get_center().x, 360.0, 2.0, "the chosen card is centred on the screen")
	strip.select(0, false)
	assert_gt(strip.cards[0].get_global_rect().size.y, 400.0, "the centre card is large (86 % of the hero room, and the card bobs a little)")



func test_the_cards_show_the_owners_pictures_and_no_text() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	for card: Button in screen.carousel.cards:
		var art: TextureRect = card.find_child("Art", true, false) as TextureRect
		assert_not_null(art, "%s has its picture" % ChoiceCard.choice_of(card))
		assert_gte(art.texture.get_size().x / art.texture.get_size().y, 0.7, "the picture is at least as wide as the card and is cropped to fit")
		assert_eq(art.stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_COVERED)
		assert_false(art.visible, "the file is held by the picture node and drawn by the card's face")
		var face: CardFace = card.find_child("Face", true, false) as CardFace
		assert_not_null(face, "%s is drawn as a tipped card" % ChoiceCard.choice_of(card))
		assert_eq(face.texture, art.texture)
		assert_false((card.get_node("Choice") as Label).visible, "no name on the card")
		assert_false((card.get_node("Heading") as Label).visible, "no heading on the card")
	for id: String in ["hotspot", "online", "practice"]:
		assert_true(ResourceLoader.exists("res://assets/ui/cards/%s.webp" % id), id)


func test_the_cards_are_large_and_every_one_is_always_visible_stacked_behind_the_chosen_one() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	var chosen: Rect2 = strip.cards[0].get_global_rect()
	assert_almost_eq(chosen.size.x / chosen.size.y, 1.75 / 2.5, 0.01, "1.75:2.5")
	assert_gt(chosen.size.y, 360.0, "large (86 % of the room that is left beside the tall buttons)")
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
			if absi(other - index) == 1:
				assert_gt(behind.get_center().x - middle.get_center().x if other > index else middle.get_center().x - behind.get_center().x, 0.0, "the next cards are on their own side: later on the right, earlier on the left")
			else:
				# two layers only (owner): the third card is horizontally centred behind the chosen one, not seen
				assert_almost_eq(behind.get_center().x, middle.get_center().x, 2.0, "the third card is centred behind the chosen card")
				for near: int in 3:
					if absi(near - index) == 1:
						assert_lt(strip.cards[other].z_index, strip.cards[near].z_index, "and behind the other card of the back layer")
			assert_gte(behind.position.x, -5.0, "inside the screen")
			assert_lte(behind.end.x, 725.0)


func test_the_actions_sit_low_on_the_screen_but_not_at_the_very_bottom() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var create: Rect2 = screen.primary_button.get_global_rect()
	var join: Rect2 = screen.secondary_button.get_global_rect()
	assert_almost_eq(create.position.y, join.position.y, 1.0, "create and join are in one row")
	assert_gt(create.get_center().y, 1280.0 * 0.7, "in the bottom part")
	assert_lt(create.end.y, 1280.0 - 60.0, "but with room under them")
	assert_lt(screen.carousel.get_global_rect().end.y, create.position.y, "below the cards")
	assert_gt(screen.carousel.get_global_rect().position.y, (screen.find_child("TopBar", true, false) as Control).get_global_rect().position.y, "the cards are under the top bar")
	assert_lt(create.size.x, 400.0, "a centred pair sized to its words, not a full-width row")
	assert_almost_eq(create.size.y, TitleScreen.CTA_HEIGHT, 1.0, "0.75 x 156")
	assert_almost_eq((create.position.x + join.end.x) / 2.0, 360.0, 2.0, "centred left to right")
	assert_almost_eq(create.size.x, join.size.x, 1.0, "the pair is the same size")
	var title: Rect2 = screen.mode_name_label.get_global_rect()
	var phrase: Rect2 = screen.mode_text_label.get_global_rect()
	var cards_bottom: float = screen.carousel.cards[screen.carousel.index].get_global_rect().end.y
	assert_almost_eq(title.get_center().y, cards_bottom, 20.0, "the mode title hangs on the bottom edge of its card")
	assert_gt(phrase.position.y, cards_bottom, "and the phrase is under the card")
	assert_lt(phrase.end.y, create.position.y - 10.0, "and clear of the buttons")
	assert_almost_eq(title.get_center().x, 360.0, 2.0, "centred left to right")
	assert_almost_eq(phrase.get_center().x, 360.0, 2.0)


func test_a_tab_opens_its_page_and_play_returns_home() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	for id: StringName in [&"street", &"codex", &"history", &"settings"]:
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


func test_the_logo_is_centred_and_dropped_from_the_top() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var logo: Control = screen.find_child("Logo", true, false) as Control
	var rect: Rect2 = logo.get_global_rect()
	assert_almost_eq(rect.get_center().x, screen.get_global_rect().get_center().x, 1.0, "centred")
	assert_gt(rect.position.y, TitleScreen.TOP_MARGIN + TitleScreen.LOGO_DROP - 1.0, "dropped from the top edge")


func test_the_logo_shows_on_play_only_so_pages_get_the_height() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var top: Control = screen.find_child("TopBar", true, false) as Control
	assert_true(top.is_visible_in_tree(), "Play shows the logo")
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


func test_the_carousel_is_online_local_practice_with_local_chosen_in_front() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	assert_eq(strip.cards.size(), 3)
	assert_eq(ChoiceCard.choice_of(strip.cards[0]), "Online")
	assert_eq(ChoiceCard.choice_of(strip.cards[1]), "Local", "Local sits between Online and Practice")
	assert_eq(ChoiceCard.choice_of(strip.cards[2]), "Practice")
	assert_eq(strip.index, 1, "and is the one chosen to start with")
	var middle: Button = strip.cards[1]
	var next: Button = strip.cards[2]
	assert_almost_eq(middle.size.x / middle.size.y, 1.75 / 2.5, 0.01, "1.75:2.5 cards")
	assert_almost_eq(middle.get_global_rect().get_center().x, 360.0, 2.0, "the chosen card is in the middle of the screen")
	assert_eq(middle.modulate.a, 1.0)
	assert_lt(next.modulate.r, middle.modulate.r - 0.2, "the next card is clearly darker: depth")
	assert_lt(next.modulate.a, middle.modulate.a, "and slightly see-through too")
	assert_almost_eq(next.get_global_rect().get_center().y, middle.get_global_rect().get_center().y, 8.0, "on the same vertical centre (the middle card bobs a few px)")
	assert_lt(next.scale.x, middle.scale.x, "and smaller")
	assert_lt(next.z_index, middle.z_index, "and behind")
	assert_gt(next.get_global_rect().get_center().x, middle.get_global_rect().get_center().x, "and to its right")
	assert_lt(strip.cards[0].get_global_rect().get_center().x, middle.get_global_rect().get_center().x, "Online is on the left")
	strip.select(0, false)
	assert_eq(strip.cards[0].modulate.r, 1.0, "the chosen card comes forward at full brightness")
	assert_lt(strip.cards[1].modulate.r, 1.0)
	assert_eq(screen.mode_name_label.text, "Online", "the details follow the chosen card")
	assert_eq(screen.primary_button.text, "Create")
	strip.select(2, false)
	assert_eq(screen.mode_name_label.text, "Practice")
	assert_eq(screen.primary_button.text, "Play", "practice is one button")
	assert_false(screen.secondary_button.visible, "no second button in practice")


func test_swiping_and_tapping_the_carousel_changes_the_card() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var strip: MenuCarousel = screen.carousel
	var from: Vector2 = Vector2(strip.size.x / 2.0 + 200.0, 100.0)
	strip._gui_input(TouchHelper.click(from, true))
	var to: Vector2 = Vector2(from.x - MenuCarousel.STEP * 1.1, from.y)
	strip._gui_input(TouchHelper.motion(to))
	strip._gui_input(TouchHelper.click(to, false))
	assert_eq(strip.index, 2, "swiping left scrolls on to the next card")
	await wait_seconds(0.4)
	var back_from: Vector2 = Vector2(strip.size.x / 2.0 - 200.0, 100.0)
	strip._gui_input(TouchHelper.click(back_from, true))
	strip._gui_input(TouchHelper.motion(Vector2(back_from.x + MenuCarousel.STEP * 1.1, back_from.y)))
	strip._gui_input(TouchHelper.click(Vector2(back_from.x + MenuCarousel.STEP * 1.1, back_from.y), false))
	assert_eq(strip.index, 1, "swiping right goes back to the previous card")
	await wait_seconds(0.4)
	var tap: Vector2 = Vector2(strip.cards[0].get_global_rect().position.x - strip.get_global_rect().position.x + 12.0, strip.size.y / 2.0)
	strip._gui_input(TouchHelper.click(tap, true))
	strip._gui_input(TouchHelper.click(tap, false))
	assert_eq(strip.index, 0, "tapping the card on the left brings it to the middle")


func test_create_and_join_share_one_row_and_the_texts_are_one_line() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var create: Rect2 = screen.primary_button.get_global_rect()
	var join: Rect2 = screen.secondary_button.get_global_rect()
	assert_almost_eq(create.position.y, join.position.y, 1.0, "one row")
	assert_lt(create.end.x, join.position.x, "create then join")
	assert_false(screen.primary_button.text.to_lower().contains("room"), "no word room")
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
	assert_false(is_instance_valid(card) and card.is_open)
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



func test_vibration_is_only_offered_on_phones() -> void:
	var screen: TitleScreen = _screen()
	var page: SettingsPage = _settings_open(screen)
	assert_eq(_find(page, "VibrationToggle") != null, Settings.can_vibrate())
	assert_false(Settings.can_vibrate(), "tests do not run on a phone")


## The answer buttons of a dialog, left to right (not its red close button).
func _answers(card: MenuDialog) -> Array[Node]:
	return card.find_children("*", "Button", true, false).filter(func(node: Node) -> bool: return node.name != "CloseButton")


func test_quit_asks_first_and_only_quits_on_yes() -> void:
	var screen: TitleScreen = _screen()
	var quit_calls: Array[int] = [0]
	screen.quit_action = func() -> void: quit_calls[0] += 1
	var card: MenuDialog = screen.open_quit_confirm()
	assert_eq(quit_calls[0], 0, "opening the question does not quit")
	var buttons: Array[Node] = _answers(card)
	var stay: Button = buttons[0] as Button
	var leave: Button = buttons[1] as Button
	assert_eq(stay.text, "Cancel")
	assert_eq(leave.text, "Quit")
	stay.pressed.emit()
	assert_eq(quit_calls[0], 0, "Cancel keeps playing")
	assert_false(is_instance_valid(card) and card.is_open)
	var again: MenuDialog = screen.open_quit_confirm()
	(_answers(again)[1] as Button).pressed.emit()
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


func test_hotspot_join_opens_the_checking_dialog_and_asks_for_no_code() -> void:
	var screen: TitleScreen = _screen()
	screen.secondary_button.pressed.emit()
	assert_not_null(screen.dialog, "a dialog")
	assert_eq(screen.dialog.name, "HotspotCheckDialog")
	assert_true(screen.dialog.is_open)
	assert_eq(screen.dialog.find_children("*", "LineEdit", true, false).size(), 0, "no code, no address, no keyboard")


func test_hotspot_join_opens_the_lobby_with_the_room_the_dialog_found() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 5)
	server.auto_join = true
	assert_eq(server.listen(18983), OK)
	server.rooms.create_room(700, "Host", 1)
	var screen: TitleScreen = _screen()
	var opened: Array = []
	screen.open_lobby_action = func(intent: int) -> void: opened.append(intent)
	var plan: HotspotNet.Plan = HotspotNet.Plan.new()
	plan.candidates = PackedStringArray(["ws://127.0.0.1:18983"])
	plan.seconds = 3.0
	plan.interval = 1.0
	var card: MenuDialog = screen.open_hotspot_join(func() -> Array: return Phones.on_home_wifi(), func(_seen: Array) -> HotspotNet.Plan: return plan)
	for i: int in 300:
		if not opened.is_empty():
			break
		server.poll(0.016)
		await wait_process_frames(1)
	server.stop()
	assert_eq(opened, [LobbyScreen.Intent.JOIN])
	assert_eq(Session.lobby_url, "ws://127.0.0.1:18983")
	assert_true(Session.lobby_hotspot)
	assert_false(is_instance_valid(card) and card.is_open)


func test_online_opens_the_lobby_on_the_game_server() -> void:
	var screen: TitleScreen = _screen()
	var opened: Array = []
	screen.open_lobby_action = func(intent: int) -> void: opened.append(intent)
	screen.carousel.select(0, false)
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
	screen.open_quit_confirm()
	assert_eq(screen.dialog.z_index, MenuDialog.MODAL_Z, "a dialog (and its dim) is on top")
	screen.dialog.close(false)
	screen._show_loading("GETTING READY")
	assert_eq((screen.find_child("Loading", true, false) as Control).z_index, MenuDialog.MODAL_Z, "so is the loading tint")


## Every running tween that loops for ever (the main action's breath is the only one Home starts).
func _breathing_loops() -> int:
	var loops: int = 0
	for tween: Tween in get_tree().get_processed_tweens():
		if tween.is_valid() and tween.is_running() and tween.get_loops_left() == -1:
			loops += 1
	return loops


## Two quick taps on the main action before its breath has started (a 1.2 s delay) must not start two breaths that fight
## each other on the same scale: once everything settles there is exactly one.
func test_two_quick_taps_leave_the_main_action_one_breath() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(false)
	var screen: TitleScreen = _screen()
	await wait_seconds(0.1)
	for i: int in 2:
		screen.primary_button.button_down.emit()
		await wait_seconds(0.05)
		screen.primary_button.button_up.emit()
		await wait_seconds(0.05)
	await wait_seconds(3.0)
	assert_eq(_breathing_loops(), 1, "one breath on the main action")
	assert_true(is_instance_valid(screen._pulse) and screen._pulse.is_running(), "and it is the screen's breath")
	Settings.set_reduced_motion(motion)


## While the main action is held it stays sunk: the breath does not swell it back under the thumb. Let go, it breathes again.
func test_holding_the_main_action_stops_its_breath_fighting_the_press() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(false)
	var screen: TitleScreen = _screen()
	await wait_seconds(1.8)
	var button: Button = screen.primary_button
	button.button_down.emit()
	await wait_seconds(0.3)
	var lowest: float = button.scale.x
	var highest: float = button.scale.x
	for i: int in 20:
		await wait_seconds(0.1)
		lowest = minf(lowest, button.scale.x)
		highest = maxf(highest, button.scale.x)
	assert_lt(highest, 1.0, "held through a whole breath, it never swells back to full size")
	assert_lt(highest - lowest, 0.005, "and it holds still")
	button.button_up.emit()
	var breathed: bool = false
	for i: int in 40:
		await wait_seconds(0.1)
		breathed = breathed or button.scale.x > 1.005
	assert_true(breathed, "let go, the main action breathes again")
	assert_eq(_breathing_loops(), 1, "with one breath")
	Settings.set_reduced_motion(motion)


## The same race through Settings: Reduce motion switched on and off again before the breath has started must not leave a
## second breath behind (each restart waited out its own delay and started its own loop).
func test_reduce_motion_toggled_quickly_leaves_the_main_action_one_breath() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(false)
	var screen: TitleScreen = _screen()
	await wait_seconds(0.1)
	Settings.set_reduced_motion(true)
	Settings.set_reduced_motion(false)
	await wait_seconds(3.0)
	assert_eq(_breathing_loops(), 1, "one breath on the main action")
	assert_true(is_instance_valid(screen._pulse) and screen._pulse.is_running(), "and it is the screen's breath")
	Settings.set_reduced_motion(motion)


func test_play_in_practice_asks_first_then_shows_the_loading_screen() -> void:
	var screen: TitleScreen = _screen()
	screen.carousel.select(2, false)
	screen.primary_button.pressed.emit()
	assert_not_null(screen.dialog, "a confirmation")
	assert_true(screen.dialog.is_open)
	assert_null(screen.find_child("Loading", true, false), "nothing loads before the player confirms")
	screen.dialog.close()
	assert_null(screen.find_child("Loading", true, false), "cancelling starts nothing")
	assert_false(screen._loading)


func test_the_buttons_are_as_wide_as_their_words_and_equal() -> void:
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	assert_almost_eq(screen.primary_button.custom_minimum_size.x, screen.secondary_button.custom_minimum_size.x, 0.5)
	assert_gte(screen.primary_button.custom_minimum_size.x, TitleScreen.CTA_MIN_WIDTH)
