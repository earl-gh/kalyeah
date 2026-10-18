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
	return {"code": "K7M2Q9XA", "team_size": team_size, "host": host, "best_of": 3, "state": 0, "members": members,
		"can_start": false, "can_start_host": true, "map": &"street"}


func _view(room: Dictionary, my_id: int = 1) -> HotspotRoomView:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, my_id)
	return view


func test_the_top_row_has_a_text_only_back_button_the_code_and_a_bare_settings_icon() -> void:
	var view: HotspotRoomView = _view(_room(2))
	assert_eq(view.back_button.text, "BACK", "text only, no chevron")
	assert_eq(view.code_label.text, "K7M2Q9XA")
	assert_eq(view.code_label.text.length(), 8)
	assert_true(view.settings_button.flat, "no button container round the icon")
	assert_eq(view.settings_button.text, "")
	assert_not_null(view.settings_button.find_children("*", "NavIcon", true, false)[0])


func test_the_map_card_is_three_to_four_and_only_the_host_changes_it() -> void:
	var host: HotspotRoomView = _view(_room(2))
	assert_almost_eq(host.map_card.custom_minimum_size.x / host.map_card.custom_minimum_size.y, 0.75, 0.01, "3:4")
	assert_true(host.map_previous.visible and host.map_next.visible, "the host has the arrows")
	var guest: HotspotRoomView = _view(_room(2), 2)
	assert_false(guest.map_previous.visible, "a guest only sees the map")
	for choice: int in [1, 2, 3]:
		assert_true(host.size_buttons[choice].disabled == false)
		assert_true(guest.size_buttons[choice].disabled, "and cannot change the size")
	assert_true(host.size_buttons[2].button_pressed, "2v2 is the chosen size")


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
	assert_eq(host.action_button.text, "START")
	assert_false(host.action_button.disabled)
	var blocked: Dictionary = _room(2)
	blocked["can_start_host"] = false
	host.show_room(blocked, 1)
	assert_true(host.action_button.disabled, "not until the others are ready")
	var guest: HotspotRoomView = _view(_room(2), 2)
	assert_eq(guest.action_button.text, "READY!", "Ben is already ready")
	var guest_two: HotspotRoomView = _view(_room(2), 1)
	var other: Dictionary = _room(2, 2)
	guest_two.show_room(other, 1)
	assert_eq(guest_two.action_button.text, "READY", "Ana is not")
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
	assert_eq(HotspotRoomView.slot_number(0, 0), 1)
	assert_eq(HotspotRoomView.slot_number(0, 2), 3)
	assert_eq(HotspotRoomView.slot_number(1, 0), 4)
	assert_eq(HotspotRoomView.slot_number(1, 2), 6)
	var ana: Control = view.find_child("Player1", true, false) as Control
	var ben: Control = view.find_child("Player2", true, false) as Control
	var bar: StyleBoxFlat = ana.get_node("Bar").get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(bar.bg_color, Color.WHITE, "a white bar")
	# row 0: Ana (blue, number 1 at the left of the bar) and Ben (red, number 4 at the right)
	var ana_number: Control = ana.find_child("Number", true, false) as Control
	var ben_number: Control = ben.find_child("Number", true, false) as Control
	assert_eq((ana_number.get_child(0) as Label).text, "1")
	assert_eq((ben_number.get_child(0) as Label).text, "4")
	assert_eq(ana_number.get_parent().get_child(0), ana_number, "blue: the number block comes first")
	assert_eq(ben_number.get_parent().get_child(ben_number.get_parent().get_child_count() - 1), ben_number, "red: the number block comes last")
	assert_eq((ana_number.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, HotspotRoomView.TEAM_BLOCKS[0])
	assert_eq((ben_number.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, HotspotRoomView.TEAM_BLOCKS[1])
	assert_eq((ana.find_child("Name", true, false) as Label).text, "Ana")
	assert_eq((ben.find_child("Name", true, false) as Label).horizontal_alignment, HORIZONTAL_ALIGNMENT_RIGHT, "red names are right-aligned")


func test_the_host_and_ready_tags_sit_in_the_far_corner() -> void:
	var view: HotspotRoomView = _view(_room(2))
	var ana_tag: Control = view.find_child("Player1", true, false).find_child("Tag", true, false) as Control
	assert_eq((ana_tag.get_child(0) as Label).text, "Host", "the host's tag")
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
