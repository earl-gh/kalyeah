extends GutTest
## The hotspot room screen: top row, map and size, two columns of players, six kid cards, one button.

var _saved_avatar: String


func before_each() -> void:
	_saved_avatar = Session.avatar_id


func after_each() -> void:
	Session.avatar_id = _saved_avatar


func _room(team_size: int, host: int = 1) -> Dictionary:
	var members: Array = [
		{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"},
		{"player_id": 2, "name": "Ben", "team": 1, "ready": true, "connected": true, "char": "migo"},
	]
	return {"code": "K7M2Q9XA", "team_size": team_size, "host": host, "first_to": 3, "state": 0, "members": members,
		"can_start": false, "can_start_host": true, "map": &"street"}


func _view(room: Dictionary, my_id: int = 1) -> HotspotRoomView:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, my_id)
	return view


func test_hotspot_header_has_back_lobby_settings_and_no_code() -> void:
	var view: HotspotRoomView = _view(_room(2))
	assert_eq(view.back_button.text, "Back", "a text-only back button, no chevron")
	assert_eq((view.find_child("Title", true, false) as Label).text, "Room", "then the text Room | code")
	assert_null(view.find_child("Divider", true, false), "spacing separates header elements")
	assert_eq(view.code_label.text, "", "hotspot has no code")
	assert_false(view.code_row.visible)
	assert_true(view.settings_button.flat, "no button container round the icon")
	assert_eq(view.settings_button.text, "")
	assert_not_null(view.settings_button.find_children("*", "NavIcon", true, false)[0])


func test_map_header_is_compact_and_only_the_host_can_change_it() -> void:
	var host: HotspotRoomView = _view(_room(2))
	assert_lte(host.map_card.custom_minimum_size.y, 120.0, "a compact map header")
	var section: Node = host.find_child("MapSection", true, false)
	assert_eq(section.get_child(0), host.map_card, "the map card is the first row")
	assert_eq(section.get_child(1).name, "Sizes", "the size choice is the one row under it")
	assert_null(host.find_child("MapPrevious", true, false), "no arrows")
	assert_false(host.map_card.disabled, "the host can open it")
	assert_true(_view(_room(2), 2).map_card.disabled, "a guest cannot")


func test_the_size_choice_is_one_row_of_three_and_only_the_host_changes_it() -> void:
	var host: HotspotRoomView = _view(_room(2))
	var guest: HotspotRoomView = _view(_room(2), 2)
	var row: Node = host.find_child("Sizes", true, false)
	assert_true(row is HBoxContainer, "one row")
	assert_eq(row.get_child_count(), 3)
	for choice: int in [1, 2, 3]:
		assert_false(host.size_buttons[choice].disabled)
		assert_true(guest.size_buttons[choice].disabled, "a guest cannot change the size")
	assert_true(host.size_buttons[2].button_pressed, "2v2 is the chosen size")


func test_tapping_the_map_card_opens_the_carousel_over_a_tint_and_picking_a_map_emits() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var picked: Array[StringName] = []
	view.map_chosen.connect(func(id: StringName) -> void: picked.append(id))
	view.map_card.pressed.emit()
	assert_not_null(view.map_carousel, "the carousel opens")
	assert_not_null(view.find_child("Tint", true, false), "over a tinted background")
	assert_eq(view.map_carousel.cards.size(), RoomService.MAP_IDS.size())
	view.close_map_carousel()
	assert_null(view.map_carousel)
	var guest: HotspotRoomView = _view(_room(2), 2)
	guest.map_card.pressed.emit()
	assert_null(guest.map_carousel, "a guest cannot open it")


func test_players_are_two_columns_with_one_row_per_team_slot_and_three_at_most() -> void:
	for size: int in [1, 2, 3]:
		var view: HotspotRoomView = _view(_room(size))
		assert_eq(view.player_grid.columns, 2, "two columns")
		assert_eq(view.player_cards.size(), size * 2, "%dv%d: a card for every slot" % [size, size])
	var full: HotspotRoomView = _view(_room(3))
	assert_lte(full.player_cards.size() / 2, HotspotRoomView.MAX_ROWS)


func test_open_slots_can_be_tapped_to_join_that_team() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var taps: Array[int] = []
	view.team_chosen.connect(func(team: int) -> void: taps.append(team))
	var other: Button = view.find_child("OpenSlot1", true, false) as Button
	assert_not_null(other)
	other.pressed.emit()
	assert_eq(taps, [1] as Array[int])
	assert_true((view.find_child("OpenSlot0", true, false) as Button).disabled, "your own team's slot is not a button to press")


func test_six_kid_cards_in_one_row_all_three_to_four() -> void:
	var view: HotspotRoomView = _view(_room(2))
	assert_eq(view.kid_cards.size(), 6)
	var row: Node = view.find_child("Kids", true, false)
	assert_eq(row.get_child_count(), 6, "one row")
	for id: String in view.kid_cards:
		var size: Vector2 = view.kid_cards[id].custom_minimum_size
		assert_almost_eq(size.x / size.y, 0.75, 0.01, "%s is 3:4" % id)


func test_choosing_a_kid_emits_and_the_room_picks_are_marked() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var picks: Array[String] = []
	view.character_chosen.connect(func(id: String) -> void: picks.append(id))
	view.kid_cards["toni"].pressed.emit()
	assert_eq(picks, ["toni"] as Array[String])


func test_the_big_button_starts_for_the_host_and_readies_for_everyone_else() -> void:
	var host: HotspotRoomView = _view(_room(2))
	assert_eq(host.action_button.text, "Start battle")
	assert_false(host.action_button.disabled)
	var blocked: Dictionary = _room(2)
	blocked["can_start_host"] = false
	host.show_room(blocked, 1)
	assert_true(host.action_button.disabled, "not until the others are ready")
	var guest: HotspotRoomView = _view(_room(2), 2)
	assert_eq(guest.action_button.text, "Cancel ready", "the action explains how to undo readiness")
	var guest_two: HotspotRoomView = _view(_room(2), 1)
	var other: Dictionary = _room(2, 2)
	guest_two.show_room(other, 1)
	assert_eq(guest_two.action_button.text, "Ready", "Ana is not")
	var pressed: Array[bool] = []
	guest_two.action_pressed.connect(func() -> void: pressed.append(true))
	guest_two.action_button.pressed.emit()
	assert_eq(pressed.size(), 1)


func test_nothing_but_the_top_row_shows_before_a_room_exists() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	assert_false(view.has_room())
	view.show_room(_room(1), 1)
	assert_true(view.has_room())


func test_player_cards_are_white_bars_with_no_picture_and_numbered_blocks() -> void:
	var view: HotspotRoomView = _view(_room(3))
	for card: Control in view.player_cards:
		assert_eq(card.find_children("*", "TextureRect", true, false).size(), 0, "no character image on a player card")
	assert_eq(HotspotRoomView.slot_number(0, 0, 3), 1)
	assert_eq(HotspotRoomView.slot_number(0, 2, 3), 3)
	assert_eq(HotspotRoomView.slot_number(1, 0, 3), 4)
	assert_eq(HotspotRoomView.slot_number(1, 2, 3), 6)
	assert_eq(HotspotRoomView.slot_number(1, 0, 2), 3, "in a 2v2 the red column starts at 3: the numbers count the places in the room")
	assert_eq(HotspotRoomView.slot_number(1, 0, 1), 2, "and in a 1v1 at 2")
	var ana: Control = view.find_child("Player1", true, false) as Control
	var ben: Control = view.find_child("Player2", true, false) as Control
	var bar: StyleBoxFlat = ana.get_node("Bar").get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(bar.bg_color, RoomPlayerCards.BAR_COLOR, "a dark glass bar")
	# row 0: Ana (blue, number 1 at the left of the bar) and Ben (red, number 4 at the right)
	var ana_number: Control = ana.find_child("Number", true, false) as Control
	var ben_number: Control = ben.find_child("Number", true, false) as Control
	assert_eq((ana_number.get_child(0) as Label).text, "1")
	assert_eq((ben_number.get_child(0) as Label).text, "4", "3v3: the red column runs 4 to 6")
	assert_eq(ana_number.get_parent().get_child(0), ana_number, "blue: the number block comes first")
	assert_eq(ben_number.get_parent().get_child(ben_number.get_parent().get_child_count() - 1), ben_number, "red: the number block comes last")
	assert_eq((ana_number.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, HotspotRoomView.TEAM_BLOCKS[0])
	assert_eq((ben_number.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, HotspotRoomView.TEAM_BLOCKS[1])
	assert_eq((ana.find_child("Name", true, false) as Label).text, "Ana")
	assert_eq((ben.find_child("Name", true, false) as Label).horizontal_alignment, HORIZONTAL_ALIGNMENT_RIGHT, "red names are right-aligned")


func test_the_host_and_ready_tags_sit_in_the_far_corner() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var ana_tag: Control = view.find_child("Player1", true, false).find_child("Tag", true, false) as Control
	assert_eq((ana_tag.get_child(0) as Label).text, "You · Host", "identity stays outside the truncated name")
	var ben_tag: Control = view.find_child("Player2", true, false).find_child("Tag", true, false) as Control
	assert_eq((ben_tag.get_child(0) as Label).text, "Ready")
	assert_almost_eq(ana_tag.anchor_left, 1.0, 0.001, "blue: the top right corner")
	assert_almost_eq(ben_tag.anchor_left, 0.0, 0.001, "red: the top left corner")
	var waiting: Dictionary = _room(2)
	(waiting["members"][1] as Dictionary)["ready"] = false
	view.show_room(waiting, 1)
	assert_null(view.find_child("Player2", true, false).find_child("Tag", true, false), "no tag until Ben is ready")


func test_the_kid_cards_use_the_faces_cropped_from_the_loading_screen_art() -> void:
	var view: HotspotRoomView = _view(_room(2))
	for id: String in Session.AVATARS:
		var face: Texture2D = KidModel.card_face(StringName(id))
		assert_not_null(face, "%s has a cropped face" % id)
		assert_almost_eq(float(face.get_width()) / float(face.get_height()), 0.75, 0.01, "%s is cropped 3:4" % id)
		var picture: TextureRect = view.kid_cards[id].get_child(0) as TextureRect
		assert_eq(picture.texture, face, "%s shows it, not a screenshot of its 3D model" % id)


func test_my_chosen_kid_has_the_amber_frame() -> void:
	var room: Dictionary = _room(2)
	var view: HotspotRoomView = _view(room)
	assert_true((view.kid_cards["junjun"].get_node("Chosen") as Panel).visible, "Ana is junjun")
	assert_false((view.kid_cards["toni"].get_node("Chosen") as Panel).visible)


func test_an_empty_slot_is_a_frosted_card_with_its_number_and_a_hint() -> void:
	var view: HotspotRoomView = _view(_room(3))
	var open: Button = view.find_child("OpenSlot1", true, false) as Button
	assert_not_null(open)
	var box: StyleBoxFlat = open.get_theme_stylebox("normal") as StyleBoxFlat
	assert_gt(box.bg_color.a, 0.0, "a faint fill, not just an outline")
	assert_lt(box.bg_color.a, 0.2, "but still see-through")
	var number: Control = open.find_child("Number", true, false) as Control
	assert_lt((number.get_theme_stylebox("panel") as StyleBoxFlat).bg_color.a, 0.5, "the number block is tinted, not solid")
	assert_eq((open.find_child("Hint", true, false) as Label).text, "Join team")
	assert_eq((view.find_child("OpenSlot0", true, false).find_child("Hint", true, false) as Label).text, "Open slot", "your own team's open slot just waits")
	assert_eq(open.find_children("*", "TextureRect", true, false).size(), 0)
	var two: HotspotRoomView = _view(_room(2))
	assert_eq((two.find_child("Player2", true, false).find_child("Number", true, false).get_child(0) as Label).text, "3", "a 2v2: Ben is number 3")


func test_the_map_card_has_no_map_word_and_is_agawan_base_and_the_carousel_cards_are_three_to_four() -> void:
	var view: HotspotRoomView = _view(_room(2))
	assert_false((view.map_card.get_node("Heading") as Label).visible, "no \"Map\" word on the card")
	assert_eq(ChoiceCard.choice_of(view.map_card), "Agawan Base")
	view.map_card.pressed.emit()
	var card: Button = view.map_carousel.cards[0]
	assert_almost_eq(card.size.x / card.size.y, 0.75, 0.01, "3:4")
	view.close_map_carousel()


func test_the_kid_cards_are_rounded_with_a_shadow() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var box: StyleBoxFlat = view.kid_cards["toni"].get_theme_stylebox("normal") as StyleBoxFlat
	assert_gt(box.corner_radius_top_left, 8, "rounded corners")
	assert_gt(box.shadow_size, 0, "a shadow")
	var picture: TextureRect = view.kid_cards["toni"].get_child(0) as TextureRect
	assert_not_null(picture.material, "the picture is cropped to the same corners")


func test_the_map_carousel_lists_all_three_street_games_ready_to_pick() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var picked: Array[StringName] = []
	view.map_chosen.connect(func(id: StringName) -> void: picked.append(id))
	view.map_card.pressed.emit()
	assert_eq(view.map_carousel.cards.size(), 3, "three street games")
	assert_eq(ChoiceCard.choice_of(view.map_carousel.cards[0]), "Agawan Base")
	assert_eq(ChoiceCard.choice_of(view.map_carousel.cards[1]), "Tumbang Preso")
	assert_eq(ChoiceCard.choice_of(view.map_carousel.cards[2]), "Patintero")
	for card: Button in view.map_carousel.cards:
		assert_ne((card.get_node("Heading") as Label).text, "Coming soon", "none says Coming soon: all three play in rooms (owner, 2026-10-31)")
	for i: int in RoomService.MAP_IDS.size():
		var picture: TextureRect = view.map_carousel.cards[i].get_node("Picture") as TextureRect
		assert_eq(picture.texture, StreetPage.picture_for(RoomService.MAP_IDS[i]), "the Games page's picture on each card (owner, 2026-11-01)")
	view.map_carousel.select(1, false)  # what a swipe does: the next card comes to the middle
	assert_true(picked.is_empty(), "a swipe only looks: nothing is picked (owner: swiping closed the picker)")
	assert_not_null(view.map_carousel, "and the picker stays open")
	var middle: Button = view.map_carousel.cards[1]
	var at: Vector2 = middle.position + view.map_carousel.card_size / 2.0
	for pressed: bool in [true, false]:
		var click: InputEventMouseButton = InputEventMouseButton.new()
		click.button_index = MOUSE_BUTTON_LEFT
		click.pressed = pressed
		click.position = at
		view.map_carousel._gui_input(click)
	var picker: RoomMapPicker = view.map_picker()
	assert_true(picked.is_empty(), "Tumbang Preso turns this 2v2 of two kids into six places: it asks first")
	assert_true(is_instance_valid(picker.confirm) and picker.confirm.is_open)
	(picker.confirm.find_children("*", "Button", true, false).filter(func(b: Node) -> bool: return (b as Button).text == "Pick it")[0] as Button).pressed.emit()
	assert_eq(picked, [&"tumbang_preso"] as Array[StringName], "a tap on the middle card, then Pick it, picks Tumbang Preso")
	view.close_map_carousel()


func test_the_picker_says_how_each_game_plays_and_picks_with_its_button() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var picked: Array[StringName] = []
	view.map_chosen.connect(func(id: StringName) -> void: picked.append(id))
	view.open_map_carousel()
	var picker: RoomMapPicker = view.map_picker()
	assert_eq((view.find_child("Tint", true, false) as ColorRect).color, RoomMapPicker.TINT, "a see-through blue (owner)")
	assert_gt(RoomMapPicker.TINT.b, RoomMapPicker.TINT.r, "blue")
	assert_lt(RoomMapPicker.TINT.a, 1.0, "see-through")
	var name_label: Label = view.map_carousel.cards[0].get_node("Choice") as Label
	assert_eq(name_label.get_theme_color("font_color"), RoomMapPicker.NAME_COLOR, "the name in yellow (owner)")
	# 3. the room's game is tagged; 2. the line says how it plays; 4. a dot for each game; 1. the button
	var tag: Label = view.map_carousel.cards[0].get_node("Heading") as Label
	assert_true(tag.visible and tag.text == RoomMapPicker.PLAYING_NOW, "Agawan Base is playing now")
	assert_false((view.map_carousel.cards[2].get_node("Heading") as Label).visible)
	assert_eq(picker.info_label.text, "2v2 · Teams · First to 3")
	assert_eq(picker.dots_label.text, "●  ○  ○")
	assert_eq(picker.pick_button.text, "Keep Agawan Base")
	view.map_carousel.select(2, false)
	assert_eq(picker.info_label.text, "2v2 · Teams · 5 min", "Patintero is timed")
	assert_eq(picker.dots_label.text, "○  ○  ●")
	assert_eq(picker.pick_button.text, "Pick Patintero")
	view.map_carousel.select(1, false)
	assert_eq(picker.info_label.text, "3 to 6 kids · Every kid for itself · 5 min")
	view.map_carousel.select(2, false)
	picker.pick_button.pressed.emit()
	assert_eq(picked, [&"patintero"] as Array[StringName], "the Pick button picks the game in the middle, no question for a team game")
	view.close_map_carousel()


func test_the_picker_closes_with_its_x_and_keeping_the_game_picks_nothing() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var picked: Array[StringName] = []
	view.map_chosen.connect(func(id: StringName) -> void: picked.append(id))
	view.open_map_carousel()
	assert_true(view.map_open())
	view.map_picker().pick_button.pressed.emit()  # "Keep Agawan Base"
	assert_true(picked.is_empty(), "the room's own game is not sent again")
	assert_false(view.map_open(), "it just closes")
	view.open_map_carousel()
	view.map_picker().close_button.pressed.emit()
	assert_false(view.map_open(), "the red X closes it")


func test_tumbang_preso_asks_only_when_it_changes_the_room() -> void:
	var view: HotspotRoomView = _view(_room(3))
	view.open_map_carousel()
	assert_false(view.map_picker().needs_confirm(&"tumbang_preso"), "a room of six places already: nothing changes")
	view.close_map_carousel()
	var alone: Dictionary = _room(1)
	alone["members"] = [(alone["members"] as Array)[0]]
	view.show_room(alone, 1)
	view.open_map_carousel()
	assert_false(view.map_picker().needs_confirm(&"tumbang_preso"), "nobody else is in the room")
	assert_false(view.map_picker().needs_confirm(&"patintero"), "a team game keeps the room as it is")
	view.close_map_carousel()


func test_the_map_carousel_overlay_is_above_everything() -> void:
	var view: HotspotRoomView = _view(_room(2))
	view.map_card.pressed.emit()
	assert_eq((view.find_child("MapOverlay", true, false) as Control).z_index, MenuDialog.MODAL_Z)
	view.close_map_carousel()


func test_the_map_card_shows_the_chosen_games_picture() -> void:
	var view: HotspotRoomView = _view(_room(2))
	assert_eq(view.map_picture.texture, StreetPage.picture_for(&"street"), "Agawan Base's picture to begin with")
	var room: Dictionary = _room(2)
	room["map"] = "tumbang_preso"
	view.show_room(room, 1)
	assert_eq(view.map_picture.texture, StreetPage.picture_for(&"tumbang_preso"), "the game the host picked")
