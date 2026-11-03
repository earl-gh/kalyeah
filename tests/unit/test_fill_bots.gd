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
	assert_eq(view.bots_button.text, "Off")
	room["fill_bots"] = true
	view.show_room(room, 1)
	assert_eq(view.bots_button.text, "On")
	assert_true(view.bots_button.button_pressed)
	view.show_room(room, 2)
	assert_false(view.bots_row.visible, "a guest does not see it")
	room["bots_allowed"] = false
	view.show_room(room, 1)
	assert_false(view.bots_row.visible, "an online room has no bots")


func test_pressing_the_switch_tells_the_screen() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	watch_signals(view)
	view.bots_button.button_pressed = true
	assert_signal_emitted(view, "fill_bots_chosen")
