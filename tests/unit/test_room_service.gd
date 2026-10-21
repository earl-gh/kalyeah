extends GutTest

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const R := RoomService.Result


func _service() -> RoomService:
	return RoomService.new(NET, 7)


func test_passcode_is_eight_unambiguous_alphanumeric_characters() -> void:
	var service: RoomService = _service()
	for peer: int in range(1, 60):
		var room: RoomService.Room = service.create_room(peer, "p", 1)
		assert_eq(room.code.length(), 8)
		for c: String in room.code:
			assert_true(NET.passcode_alphabet.contains(c), c)
			assert_false("ILO01".contains(c), "no ambiguous %s" % c)
	assert_eq(service.rooms.size(), 59, "codes are unique")


func test_create_and_join_fill_the_smaller_team() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(10, "Host", 2)
	assert_eq(room.members[0].team, 0)
	assert_eq(service.join(room.code.to_lower(), 11, "B"), R.OK, "codes are case-insensitive")
	assert_eq(service.join(room.code, 12, "C"), R.OK)
	assert_eq(service.join(room.code, 13, "D"), R.OK)
	assert_eq(room.team_count(0), 2)
	assert_eq(room.team_count(1), 2)
	assert_eq(service.join(room.code, 14, "E"), R.FULL)
	assert_eq(service.join("ZZZZZZZZ", 15, "F"), R.NOT_FOUND)


func test_names_are_trimmed_and_never_empty() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "   ", 1)
	assert_eq(room.members[0].name, "Player")
	service.join(room.code, 2, "A very long name indeed")
	assert_eq(room.members[1].name.length(), NET.max_name_length)


func test_team_slots_and_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	assert_false(service.pick_team(2, 0), "team 0 is full in 1v1")
	assert_true(service.set_ready(2, true))
	assert_true(service.pick_team(1, 1) == false, "and team 1 too")
	assert_true(room.member(room.members[1].player_id).ready)


func test_1v1_needs_both_players_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.set_ready(1, true)
	assert_false(service.can_start(room), "alone")
	service.join(room.code, 2, "B")
	assert_false(service.can_start(room), "B not ready")
	service.set_ready(2, true)
	assert_true(service.can_start(room))
	assert_false(service.start(2), "only the host starts")
	assert_true(service.start(1))
	assert_eq(room.state, RoomService.State.LOADING)
	assert_eq(service.join(room.code, 3, "C"), R.IN_PROGRESS)


func test_3v3_starts_with_one_team_full_and_the_other_one_short() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 3)
	for peer: int in range(2, 6):
		service.join(room.code, peer, "p%d" % peer)
	# 5 players: 3 v 2
	for peer: int in range(1, 6):
		service.set_ready(peer, true)
	assert_eq(room.team_count(0), 3)
	assert_eq(room.team_count(1), 2)
	assert_true(service.can_start(room), "3 v 2 is allowed")
	service.join(room.code, 6, "p6")
	assert_false(service.can_start(room), "the new player is not ready")
	service.leave(6)
	service.leave(5)
	assert_false(service.can_start(room), "3 v 1 is too uneven")


func test_host_leaving_the_lobby_hands_over_and_an_empty_room_closes() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 2)
	service.join(room.code, 2, "B")
	var b_id: int = room.members[1].player_id
	service.disconnected(1)
	assert_eq(room.host_player_id, b_id)
	assert_eq(room.members.size(), 1)
	service.leave(2)
	assert_false(service.rooms.has(room.code))


func test_a_dropped_player_can_reconnect_within_60_s_with_their_token() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	service.set_ready(1, true)
	service.set_ready(2, true)
	service.start(1)
	var b: RoomService.Member = room.members[1]
	var token: String = b.token
	service.disconnected(2)
	assert_false(b.connected)
	assert_eq(room.members.size(), 2, "the slot stays")
	service.step(59.0)
	assert_null(service.reconnect("wrong", 9))
	var back: RoomService.Member = service.reconnect(token, 9)
	assert_eq(back, b)
	assert_eq(b.peer_id, 9)
	assert_eq(service.member_of_peer(9), b)


func test_the_token_expires_after_60_s() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	service.set_ready(1, true)
	service.set_ready(2, true)
	service.start(1)
	var token: String = room.members[1].token
	service.disconnected(2)
	service.step(61.0)
	assert_null(service.reconnect(token, 9))
	assert_true(service.rooms.has(room.code), "the match goes on")


func test_only_the_host_picks_first_to_3_5_or_7_in_the_lobby() -> void:
	var service: RoomService = _service()
	service.first_to_choices = [3, 5, 7] as Array[int]
	service.default_first_to = 3
	var room: RoomService.Room = service.create_room(10, "Host", 1)
	service.join(room.code, 11, "Guest")
	assert_eq(room.first_to, 3, "new rooms start at first to 3")
	assert_false(service.set_first_to(11, 5), "not the guest")
	assert_true(service.set_first_to(10, 7))
	assert_eq(room.first_to, 7)
	assert_false(service.set_first_to(10, 4), "only 3, 5 or 7")
	assert_eq(service.describe(room)["first_to"], 7, "everyone in the room sees it")


func test_the_host_changes_the_team_size_and_everyone_is_unreadied() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	service.set_ready(2, true)
	assert_false(service.set_team_size(2, 2), "only the host")
	assert_true(service.set_team_size(1, 3))
	assert_eq(room.team_size, 3)
	assert_false(room.member(room.members[1].player_id).ready, "the match changed, so Ready is cleared")
	assert_false(service.set_team_size(1, 4), "1v1, 2v2 or 3v3 only")
	assert_false(service.set_team_size(1, 3), "no change is no change")


func test_the_team_size_cannot_shrink_below_the_people_in_the_room() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 3)
	service.join(room.code, 2, "B")
	service.join(room.code, 3, "C")
	assert_false(service.set_team_size(1, 1), "three players do not fit 1v1")
	assert_true(service.set_team_size(1, 2))


func test_the_host_picks_the_map_from_the_list() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	assert_eq(room.map_id, &"street")
	assert_false(service.set_map(2, &"street"), "only the host")
	assert_false(service.set_map(1, &"nowhere"), "only a listed map")
	assert_false(service.set_map(1, &"patintero"), "listed, but not playable yet")
	assert_eq(RoomService.MAP_IDS.size(), 3, "Agawan Base, Tumbang Preso and Patintero")
	assert_true(service.set_map(1, &"street"))
	assert_eq(service.describe(room)["map"], &"street")


func test_a_member_picks_a_kid_and_the_others_see_it() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	assert_true(service.set_character(1, &"toni"))
	assert_false(service.set_character(1, &"nobody"), "only a real kid")
	assert_eq(service.describe(room)["members"][0]["char"], &"toni")


func test_can_start_can_count_the_host_as_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A", 1)
	service.join(room.code, 2, "B")
	service.set_ready(2, true)
	assert_false(service.can_start(room), "the host has not readied")
	assert_true(service.can_start(room, true), "but the one button readies and starts")
	assert_true(service.describe(room)["can_start_host"])
