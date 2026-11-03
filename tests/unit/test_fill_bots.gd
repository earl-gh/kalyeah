extends GutTest
## "Fill with bots" in a Local (hotspot) room: the host's switch, the lifted minimums, the bots in the match and the switch on the room screen.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const DT: float = 1.0 / 30.0


func _service(allowed: bool = true) -> RoomService:
	var service: RoomService = RoomService.new(NET, 3)
	service.bots_allowed = allowed
	return service


func _room_for(service: RoomService, map_id: StringName, friends: int) -> RoomService.Room:
	var room: RoomService.Room = service.create_room(1, "Host")
	room.map_id = map_id
	for i: int in friends:
		service.join(room.code, 2 + i, "F%d" % i)
	return room


func test_only_the_host_of_a_hotspot_server_can_switch_it_on() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 1)
	assert_false(service.set_fill_bots(2, true), "a guest cannot")
	assert_false(room.fill_bots)
	assert_true(service.set_fill_bots(1, true))
	assert_true(room.fill_bots)
	assert_true(service.set_fill_bots(1, false))
	assert_false(room.fill_bots)
	var online: RoomService = _service(false)
	var online_room: RoomService.Room = _room_for(online, &"street", 0)
	assert_false(online.set_fill_bots(1, true), "the online server never has bots")
	assert_false(online_room.fill_bots)


func test_the_switch_is_refused_once_the_game_has_started() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 1)
	room.state = RoomService.State.LOADING
	assert_false(service.set_fill_bots(1, true))


func test_the_room_message_carries_the_switch() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 0)
	assert_false(service.describe(room)["fill_bots"])
	assert_true(service.describe(room)["bots_allowed"])
	service.set_fill_bots(1, true)
	assert_true(service.describe(room)["fill_bots"])
	assert_false(_service(false).describe(room)["bots_allowed"])


func test_with_bots_no_game_is_short_of_players() -> void:
	for map_id: StringName in RoomService.PLAYABLE_MAP_IDS:
		var service: RoomService = _service()
		var room: RoomService.Room = _room_for(service, map_id, 0)
		assert_false(service.start_blocker(room, true).is_empty(), "%s alone cannot start without bots" % map_id)
		service.set_fill_bots(1, true)
		assert_eq(service.start_blocker(room, true), "", "%s alone can start with bots" % map_id)
		assert_true(service.can_start(room, true))


func test_with_bots_a_guest_still_has_to_be_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 1)
	service.set_fill_bots(1, true)
	assert_string_contains(service.start_blocker(room, true), "Waiting for 1 player")
	service.set_ready(2, true)
	assert_eq(service.start_blocker(room, true), "")


func test_bots_fill_every_team_up_to_three_and_a_free_for_all_up_to_six() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 1)  # the host is blue, the friend is red
	assert_eq(RoomService.bot_teams(room), [] as Array[int], "off: none")
	service.set_fill_bots(1, true)
	assert_eq(RoomService.bot_teams(room), [0, 0, 1, 1] as Array[int])
	room.map_id = &"patintero"
	assert_eq(RoomService.bot_teams(room), [0, 0, 1, 1] as Array[int])
	room.map_id = &"tumbang_preso"
	assert_eq(RoomService.bot_teams(room).size(), 4, "six kids in all")


func test_a_full_room_gets_no_bots() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 5)
	service.set_fill_bots(1, true)
	assert_eq(RoomService.bot_teams(room).size(), 0)


func test_the_hotspot_server_allows_bots_and_the_online_one_does_not() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 1)
	assert_false(server.rooms.bots_allowed)
	server.auto_join = true
	assert_true(server.rooms.bots_allowed)
	server.stop()


func _match(map_id: StringName, friends: int) -> ServerMatch:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, map_id, friends)
	service.set_fill_bots(1, true)
	return ServerMatch.new(null, room, RULES, LAYOUT, NET, 5)


func test_the_match_has_six_kids_and_the_bots_are_not_members() -> void:
	for map_id: StringName in RoomService.PLAYABLE_MAP_IDS:
		var running: ServerMatch = _match(map_id, 0)
		assert_eq(running.sim.players.size(), 6, "%s: six kids" % map_id)
		assert_eq(running.room.members.size(), 1, "only the host is a member")
		assert_eq(running.bots.size(), 5)
		for id: int in running.bots:
			assert_gte(id, RoomService.BOT_ID_BASE)
			assert_true(running.sim.players.has(id))
		running.dispose()


func test_agawan_teams_are_three_and_three() -> void:
	var running: ServerMatch = _match(&"street", 1)
	var blue: int = 0
	for id: int in running.sim.players:
		if running.sim.players[id].team == 0:
			blue += 1
	assert_eq(blue, 3)
	assert_eq(running.sim.players.size() - blue, 3)
	for id: int in running.bots:
		assert_false(running.sim.players[id].weapons.is_empty(), "a bot is equipped like everyone")
	running.dispose()


func test_the_loading_message_lists_the_bots_for_every_phone() -> void:
	var running: ServerMatch = _match(&"patintero", 0)
	var listed: Array = running.info()["players"] as Array
	assert_eq(listed.size(), 6)
	var names: Array = listed.map(func(entry: Dictionary) -> String: return entry["name"] as String)
	assert_true(names.has("Bot 1"))
	assert_true(names.has("Host"))
	running.dispose()


func test_without_the_switch_nothing_changes() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _room_for(service, &"street", 1)
	var running: ServerMatch = ServerMatch.new(null, room, RULES, LAYOUT, NET, 5)
	assert_eq(running.sim.players.size(), 2)
	assert_eq(running.bots.size(), 0)
	running.dispose()


func _bots_move(map_id: StringName) -> bool:
	var running: ServerMatch = _match(map_id, 0)
	running.stage = ServerMatch.Stage.PLAYING
	running.room.state = RoomService.State.PLAYING
	running.sim.begin_intro()
	var start: Dictionary = {}
	for id: int in running.bots:
		start[id] = running.sim.players[id].position
	var moved: bool = false
	for i: int in 900:
		running.poll(DT)
		for id: int in running.bots:
			if running.sim.phase == MatchSim.Phase.PLAYING and not running.sim.players[id].position.is_equal_approx(start[id] as Vector2):
				moved = true
	running.dispose()
	return moved


func test_the_bots_play_in_every_game() -> void:
	for map_id: StringName in RoomService.PLAYABLE_MAP_IDS:
		assert_true(_bots_move(map_id), "%s: the bots run" % map_id)


func test_the_room_screen_shows_the_switch_to_the_host_of_a_hotspot_room_only() -> void:
	var room: Dictionary = {"code": "", "team_size": 3, "host": 1, "first_to": 3, "state": 0, "can_start": false, "can_start_host": true,
		"map": &"street", "fill_bots": false, "bots_allowed": true,
		"members": [{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"}]}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, 1)
	assert_true(view.bots_row.visible)
	assert_false(view.bots_button.button_pressed)
	room["fill_bots"] = true
	view.show_room(room, 1)
	assert_true(view.bots_button.button_pressed)
	view.show_room(room, 2)
	assert_true(view.bots_row.visible, "a guest reads it as text")
	assert_false(view.bots_row.get_child(1).visible, "but gets no switch")
	room["bots_allowed"] = false
	view.show_room(room, 1)
	assert_false(view.bots_row.visible, "an online room has no bots")
	view.show_room(room, 2)
	assert_false(view.bots_row.visible, "and its guests are not told about them")


func test_pressing_the_switch_tells_the_screen() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	watch_signals(view)
	view.bots_button.button_pressed = true
	assert_signal_emitted(view, "fill_bots_chosen")


func _host_view(map_id: StringName, bots_allowed: bool) -> HotspotRoomView:
	var room: Dictionary = {"code": "", "team_size": 3, "host": 1, "first_to": 3, "state": 0, "can_start": false, "can_start_host": true,
		"map": map_id, "fill_bots": false, "bots_allowed": bots_allowed,
		"members": [{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"}]}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, 1)
	return view


func test_race_to_and_fill_with_bots_share_one_row_each_with_a_small_caption_over_its_buttons() -> void:
	var view: HotspotRoomView = _host_view(&"street", true)
	assert_eq(view.race_group.get_parent(), view.settings_row)
	assert_eq(view.bots_row.get_parent(), view.settings_row, "one row for both")
	for pair: Array in [[view.race_group, "Race to"], [view.bots_row, "Fill with bots"]]:
		var group: Control = pair[0] as Control
		var caption: Label = group.get_child(0) as Label
		assert_eq(caption.text, pair[1])
		assert_eq(caption.get_theme_font_size("font_size"), UiMetrics.MIN_TEXT, "the smallest text size")
		assert_eq(caption.horizontal_alignment, HORIZONTAL_ALIGNMENT_LEFT, "at the top left")
		assert_true(group.get_child(1) is HBoxContainer, "the buttons come under the caption")
	assert_eq(view.first_to_buttons[3].accessibility_name, "Race to 3")


func test_a_street_game_has_no_race_and_a_room_without_bots_has_no_switch() -> void:
	var street: HotspotRoomView = _host_view(&"patintero", true)
	assert_false(street.race_group.visible)
	assert_true(street.bots_row.visible)
	assert_true(street.settings_row.visible)
	var online: HotspotRoomView = _host_view(&"street", false)
	assert_true(online.race_group.visible)
	assert_false(online.bots_row.visible)
	var neither: HotspotRoomView = _host_view(&"patintero", false)
	assert_false(neither.settings_row.visible, "nothing to set, no row")


func test_fill_with_bots_is_a_switch_not_a_button() -> void:
	var view: HotspotRoomView = _host_view(&"street", true)
	assert_true(view.bots_button is UiToggle)
	assert_eq(view.bots_button.button_pressed, false)


func test_the_game_card_has_a_caption_over_it_and_tap_to_change_under_its_title_for_the_host() -> void:
	var host: HotspotRoomView = _host_view(&"street", false)
	var game: Control = host.map_card.get_parent() as Control
	assert_eq((game.get_child(0) as Label).text, "Game")
	assert_eq((game.get_child(0) as Label).get_theme_font_size("font_size"), UiMetrics.MIN_TEXT)
	assert_eq(host.map_hint.text, "Tap to change")
	assert_true(host.map_hint.visible)
	assert_eq(host.map_hint.get_parent(), host.map_card)
	var room: Dictionary = {"code": "", "team_size": 3, "host": 1, "first_to": 3, "state": 0, "can_start": false, "can_start_host": true,
		"map": &"street", "members": [{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"}]}
	host.show_room(room, 2)
	assert_false(host.map_hint.visible, "a guest cannot change the game")


func test_a_guest_reads_the_game_and_the_settings_as_text_in_two_rows() -> void:
	var room: Dictionary = {"code": "", "team_size": 3, "host": 1, "first_to": 5, "state": 0, "can_start": false, "can_start_host": false,
		"map": &"street", "fill_bots": true, "bots_allowed": true,
		"members": [{"player_id": 1, "name": "Ana", "team": 0, "ready": true, "connected": true, "char": "junjun"},
			{"player_id": 2, "name": "Ben", "team": 1, "ready": false, "connected": true, "char": "migo"}]}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, 2)
	# row 1: Game, then the title
	assert_eq(((view.map_title.get_parent() as Control).get_child(0) as Label).text, "Game")
	assert_true(view.map_title.visible)
	assert_eq(view.map_title.text, "Agawan Base")
	assert_false(view.map_card.visible, "no card to tap")
	assert_false(view.map_hint.visible)
	# row 2: Race to and Fill with bots, each its choice as text
	assert_true(view.race_group.visible)
	assert_false(view.race_group.get_child(1).visible, "no buttons")
	assert_eq((view.race_group.get_child(2) as Label).text, "5")
	assert_true(view.bots_row.visible)
	assert_false(view.bots_row.get_child(1).visible, "no switch")
	assert_eq((view.bots_row.get_child(2) as Label).text, "On")
	room["fill_bots"] = false
	view.show_room(room, 2)
	assert_eq((view.bots_row.get_child(2) as Label).text, "Off")
	room["map"] = &"patintero"
	view.show_room(room, 2)
	assert_false(view.race_group.visible, "a timed game has no race")
	assert_eq(view.map_title.text, "Patintero")


func test_tumbang_preso_shows_no_players_and_minutes_line_in_the_room() -> void:
	var room: Dictionary = {"code": "", "team_size": 3, "host": 1, "first_to": 3, "state": 0, "can_start": false, "can_start_host": false,
		"map": &"tumbang_preso", "members": [{"player_id": 1, "name": "Ana", "team": 0, "ready": true, "connected": true, "char": "junjun"},
			{"player_id": 2, "name": "Ben", "team": 0, "ready": false, "connected": true, "char": "migo"}]}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	for me: int in [1, 2]:
		view.show_room(room, me)
		assert_false(view.format_summary.visible, "no '3 to 6 players · 5 min' for player %d" % me)
