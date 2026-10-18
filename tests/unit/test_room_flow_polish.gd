extends GutTest

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")


func test_lobby_kids_survive_match_initialization_for_every_team_size() -> void:
	for size: int in [1, 2, 3]:
		for seed_value: int in [1, 2, 3]:
			var server: GameServer = GameServer.new(RULES, LAYOUT, NET, seed_value)
			var room: RoomService.Room = server.rooms.create_room(1, "Host", size)
			for peer: int in range(2, size * 2 + 1):
				server.rooms.join(room.code, peer, "Friend")
			for member: RoomService.Member in room.members:
				var choice: StringName = StringName(Session.AVATARS[(member.player_id + 2) % Session.AVATARS.size()])
				assert_true(server.rooms.set_character(member.peer_id, choice))
			var running: ServerMatch = ServerMatch.new(server, room, RULES, LAYOUT, NET, seed_value)
			for member: RoomService.Member in room.members:
				assert_eq(running.sim.players[member.player_id].character_id, member.character)
			running.dispose()
			server.stop()


func test_teammate_choice_cannot_be_stolen_or_duplicated_by_switching_teams() -> void:
	var service: RoomService = RoomService.new(NET, 1)
	var room: RoomService.Room = service.create_room(1, "A", 2)
	service.join(room.code, 2, "B")
	service.join(room.code, 3, "C")
	assert_true(service.set_character(1, &"toni"))
	assert_false(service.set_character(3, &"toni"))
	assert_true(service.set_character(2, &"toni"), "other team may use the same kid")
	service.pick_team(3, 1)
	assert_false(service.pick_team(2, 0), "switching cannot create a duplicate")
	assert_eq(room.member(1).character, &"toni")


func test_changing_a_character_requires_ready_confirmation_again() -> void:
	var service: RoomService = RoomService.new(NET, 1)
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.set_ready(1, true)
	service.set_character(1, &"toni")
	assert_false(room.member(1).ready)


func test_discovery_rejects_online_empty_full_and_started_rooms() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 1)
	assert_eq(server._discovery_status(), "not_hotspot")
	server.auto_join = true
	assert_eq(server._discovery_status(), "no_room")
	var room: RoomService.Room = server.rooms.create_room(1, "Host", 1)
	assert_eq(server._discovery_status(), "ready")
	server.rooms.join(room.code, 2, "Friend")
	assert_eq(server._discovery_status(), "full")
	room.state = RoomService.State.PLAYING
	assert_eq(server._discovery_status(), "in_progress")
	server.stop()


func test_open_server_without_a_room_is_not_a_discovery_success() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 1)
	server.auto_join = true
	assert_eq(server.listen(18947), OK)
	var finder: HotspotFinder = HotspotFinder.new()
	finder.start(PackedStringArray(["ws://127.0.0.1:18947"]))
	for i: int in 300:
		server.poll(1.0 / 60.0)
		finder.poll(1.0 / 60.0)
		if finder.failed:
			break
		OS.delay_usec(2000)
	assert_true(finder.failed)
	assert_eq(finder.url, "")
	assert_string_contains(finder.failure_message, "no room")
	finder.stop()
	server.stop()


func test_room_controls_stay_accessible_and_readiness_explains_blocked_start() -> void:
	var room: Dictionary = {"team_size": 3, "host": 1, "members": [], "best_of": 5}
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.show_room(room, 1)
	assert_string_contains(view.readiness_label.text, "Waiting for players")
	assert_gte(view.back_button.custom_minimum_size.y, 88.0)
	assert_gte(view.settings_button.custom_minimum_size.y, 88.0)
	assert_gte(view.size_buttons[1].custom_minimum_size.y, 88.0)
	var scroll: ScrollContainer = view.find_child("RoomScroll", true, false) as ScrollContainer
	assert_not_null(scroll, "room content can scroll without moving Start off-screen")
	assert_false(scroll.is_ancestor_of(view.action_button))
	assert_eq(view.best_of_buttons.size(), 3)
