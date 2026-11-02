extends GutTest

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const R := RoomService.Result


func _service() -> RoomService:
	return RoomService.new(NET, 7)


func test_passcode_is_eight_unambiguous_alphanumeric_characters() -> void:
	var service: RoomService = _service()
	for peer: int in range(1, 60):
		var room: RoomService.Room = service.create_room(peer, "p")
		assert_eq(room.code.length(), 8)
		for c: String in room.code:
			assert_true(NET.passcode_alphabet.contains(c), c)
			assert_false("ILO01".contains(c), "no ambiguous %s" % c)
	assert_eq(service.rooms.size(), 59, "codes are unique")


func test_every_room_has_six_places_three_a_team_and_joiners_fill_the_smaller_team() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(10, "Host")
	assert_eq(room.members[0].team, 0)
	assert_eq(service.join(room.code.to_lower(), 11, "B"), R.OK, "codes are case-insensitive")
	assert_eq(service.join(room.code, 12, "C"), R.OK)
	assert_eq(service.join(room.code, 13, "D"), R.OK)
	assert_eq(room.team_count(0), 2)
	assert_eq(room.team_count(1), 2)
	assert_eq(service.join(room.code, 14, "E"), R.OK)
	assert_eq(service.join(room.code, 15, "F"), R.OK)
	assert_eq(room.team_count(0), 3)
	assert_eq(room.team_count(1), 3)
	assert_eq(service.join(room.code, 16, "G"), R.FULL, "three a team, six in all")
	assert_eq(service.join("ZZZZZZZZ", 17, "H"), R.NOT_FOUND)
	assert_eq(service.describe(room)["team_size"], 3, "everyone is told the room is 3v3")


func test_names_are_trimmed_and_never_empty() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "   ")
	assert_eq(room.members[0].name, "Player")
	service.join(room.code, 2, "A very long name indeed")
	assert_eq(room.members[1].name.length(), NET.max_name_length)


func test_team_slots_and_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	for peer: int in range(2, 7):
		service.join(room.code, peer, "p%d" % peer)
	assert_false(service.pick_team(2, 0), "team 0 has its three")
	assert_false(service.pick_team(1, 1), "and team 1 too")
	assert_true(service.set_ready(2, true))
	assert_true(room.member(room.members[1].player_id).ready)
	service.leave(5)
	assert_true(_move(service, 2, 0), "a place opened on team 0")
	assert_false(room.member(room.members[1].player_id).ready, "moving clears Ready")


func _fill(service: RoomService, room: RoomService.Room, peers: int) -> void:
	for peer: int in range(2, peers + 1):
		service.join(room.code, peer, "p%d" % peer)


## Moves `peer` to `team` with a kid nobody there has (two teammates cannot have the same kid).
func _move(service: RoomService, peer: int, team: int) -> bool:
	for avatar: String in Session.AVATARS:
		if service.set_character(peer, StringName(avatar)) and service.pick_team(peer, team):
			return true
	return false


func _everyone_ready(service: RoomService, peers: int) -> void:
	for peer: int in range(1, peers + 1):
		service.set_ready(peer, true)


func test_agawan_base_starts_with_one_a_side_or_any_mix_but_never_with_an_empty_team() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.set_ready(1, true)
	assert_false(service.can_start(room), "alone: the other team is empty")
	assert_eq(service.start_blocker(room), "Agawan Base needs at least 1 player on each team.")
	service.join(room.code, 2, "B")
	service.set_ready(2, true)
	assert_true(service.can_start(room), "1 v 1")
	assert_false(service.start(2), "only the host starts")
	assert_true(service.start(1))
	assert_eq(room.state, RoomService.State.LOADING)
	assert_eq(service.join(room.code, 3, "C"), R.IN_PROGRESS)


func test_agawan_base_with_everyone_on_one_team_cannot_start() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	_fill(service, room, 3)
	assert_true(_move(service, 2, 0))
	_everyone_ready(service, 3)
	assert_eq(room.team_count(1), 0)
	assert_false(service.can_start(room), "3 v 0")
	assert_true(_move(service, 3, 1))
	service.set_ready(3, true)
	assert_true(service.can_start(room), "2 v 1 is fine: the teams do not have to match")


func test_tumbang_preso_has_no_teams_and_needs_two_players() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.set_map(1, &"tumbang_preso")
	service.set_ready(1, true)
	assert_false(service.can_start(room), "one player alone")
	assert_eq(service.start_blocker(room), "Tumbang Preso needs at least 2 players.")
	service.join(room.code, 2, "B")
	assert_eq(service.start_blocker(room), "Waiting for 1 player to ready up.")
	service.set_ready(2, true)
	assert_true(service.can_start(room), "two kids, whatever their places")
	_fill(service, room, 6)
	_everyone_ready(service, 6)
	assert_true(service.can_start(room), "up to six")


func test_patintero_needs_two_players_on_each_team() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.set_map(1, &"patintero")
	_fill(service, room, 2)
	_everyone_ready(service, 2)
	assert_false(service.can_start(room), "1 v 1")
	assert_eq(service.start_blocker(room), "Patintero needs at least 2 players on each team.")
	_fill(service, room, 4)
	_everyone_ready(service, 4)
	assert_true(service.can_start(room), "2 v 2")
	assert_true(_move(service, 4, 0))
	service.set_ready(4, true)
	assert_eq(room.team_count(0), 3)
	assert_eq(room.team_count(1), 1)
	assert_false(service.can_start(room), "3 v 1: one team is short")
	_fill(service, room, 6)
	_everyone_ready(service, 6)
	assert_true(service.can_start(room), "3 v 3")


func test_the_headcount_is_said_before_who_is_not_ready_and_the_host_sees_it_in_the_room() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.set_map(1, &"patintero")
	_fill(service, room, 2)
	assert_eq(service.describe(room)["start_note"], "Patintero needs at least 2 players on each team.", "the count first")
	_fill(service, room, 4)
	assert_eq(service.describe(room)["start_note"], "Waiting for 3 players to ready up.", "the host counts as ready: 3 others are not")
	_everyone_ready(service, 4)
	assert_eq(service.describe(room)["start_note"], "")
	assert_true(service.describe(room)["can_start_host"])


func test_host_leaving_the_lobby_hands_over_and_an_empty_room_closes() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.join(room.code, 2, "B")
	var b_id: int = room.members[1].player_id
	service.disconnected(1)
	assert_eq(room.host_player_id, b_id)
	assert_eq(room.members.size(), 1)
	service.leave(2)
	assert_false(service.rooms.has(room.code))


func test_a_dropped_player_can_reconnect_within_60_s_with_their_token() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
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
	var room: RoomService.Room = service.create_room(1, "A")
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
	var room: RoomService.Room = service.create_room(10, "Host")
	service.join(room.code, 11, "Guest")
	assert_eq(room.first_to, 3, "new rooms start at first to 3")
	assert_false(service.set_first_to(11, 5), "not the guest")
	assert_true(service.set_first_to(10, 7))
	assert_eq(room.first_to, 7)
	assert_false(service.set_first_to(10, 4), "only 3, 5 or 7")
	assert_eq(service.describe(room)["first_to"], 7, "everyone in the room sees it")


func test_the_host_picks_the_map_from_the_list() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.join(room.code, 2, "B")
	assert_eq(room.map_id, &"street")
	assert_false(service.set_map(2, &"street"), "only the host")
	assert_false(service.set_map(1, &"nowhere"), "only a listed map")
	assert_true(service.set_map(1, &"patintero"), "the street games play in rooms (owner, 2026-10-31)")
	assert_true(service.set_map(1, &"tumbang_preso"))
	assert_eq(RoomService.MAP_IDS.size(), 3, "Agawan Base, Tumbang Preso and Patintero")
	assert_true(service.set_map(1, &"street"))
	assert_eq(service.describe(room)["map"], &"street")


func test_a_member_picks_a_kid_and_the_others_see_it() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	assert_true(service.set_character(1, &"toni"))
	assert_false(service.set_character(1, &"nobody"), "only a real kid")
	assert_eq(service.describe(room)["members"][0]["char"], &"toni")


func test_can_start_can_count_the_host_as_ready() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = service.create_room(1, "A")
	service.join(room.code, 2, "B")
	service.set_ready(2, true)
	assert_false(service.can_start(room), "the host has not readied")
	assert_true(service.can_start(room, true), "but the one button readies and starts")
	assert_true(service.describe(room)["can_start_host"])
