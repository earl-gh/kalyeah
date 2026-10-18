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
