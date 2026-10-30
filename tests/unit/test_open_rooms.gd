extends GutTest
## Online open rooms (owner, 2026-10-30): a room is open by default and listed for anyone to join with a tap while it is in the
## lobby and not full, the fullest first; a private room is joined only with its code; only the host switches it.

const NET: NetRules = preload("res://data/rules/net_rules.tres")

var _rooms: RoomService
var _peer: int = 100


func before_each() -> void:
	_rooms = RoomService.new(NET, 5)
	_peer = 100


func _create(size: int, host_name: String = "Ana") -> RoomService.Room:
	_peer += 1
	return _rooms.create_room(_peer, host_name, size)


func _join(room: RoomService.Room) -> int:
	_peer += 1
	assert_eq(_rooms.join(room.code, _peer, "Kid%d" % _peer), RoomService.Result.OK)
	return _peer


func _codes() -> Array[String]:
	var codes: Array[String] = []
	for entry: Dictionary in _rooms.open_rooms(NET.room_list_max):
		codes.append(String(entry["code"]))
	return codes


func test_a_new_room_is_open_and_listed_with_what_a_card_shows() -> void:
	var room: RoomService.Room = _create(2, "Ana")
	assert_true(room.open, "owner: open by default")
	var listed: Array[Dictionary] = _rooms.open_rooms(NET.room_list_max)
	assert_eq(listed.size(), 1)
	assert_eq(listed[0], {"code": room.code, "host_name": "Ana", "team_size": 2, "players": 1, "first_to": room.first_to, "map": room.map_id})
	assert_true(_rooms.describe(room)["open"], "the room screen knows too")


func test_private_started_and_full_rooms_are_not_listed() -> void:
	var private_room: RoomService.Room = _create(1)
	assert_true(_rooms.set_open(_peer, false))
	var started: RoomService.Room = _create(1)
	var started_host: int = _peer
	_join(started)
	started.state = RoomService.State.LOADING
	var full: RoomService.Room = _create(1)
	var guest: int = _join(full)
	var waiting: RoomService.Room = _create(1)
	assert_eq(_codes(), [waiting.code] as Array[String], "only the open one in the lobby with a free place")
	assert_false(_codes().has(private_room.code))
	assert_ne(started_host, 0)
	_rooms.leave(guest)
	assert_true(_codes().has(full.code), "a place freed up: listed again")


func test_the_fullest_room_comes_first_then_the_oldest_and_the_list_is_capped() -> void:
	var older: RoomService.Room = _create(3)
	var newer: RoomService.Room = _create(3)
	var fuller: RoomService.Room = _create(3)
	_join(fuller)
	_join(fuller)
	assert_eq(_codes(), [fuller.code, older.code, newer.code] as Array[String])
	assert_eq(_rooms.open_rooms(2).size(), 2, "never more than the limit")


func test_only_the_host_in_the_lobby_switches_open_or_private() -> void:
	var room: RoomService.Room = _create(2)
	var host: int = _peer
	var guest: int = _join(room)
	assert_false(_rooms.set_open(guest, false), "a guest cannot")
	assert_true(room.open)
	assert_true(_rooms.set_open(host, false))
	assert_false(room.open)
	room.state = RoomService.State.PLAYING
	assert_false(_rooms.set_open(host, true), "not once the match has started")
	assert_false(_rooms.set_open(999, true), "nor someone in no room")


func test_a_private_room_still_joins_with_its_code() -> void:
	var room: RoomService.Room = _create(1)
	_rooms.set_open(_peer, false)
	_peer += 1
	assert_eq(_rooms.join(room.code, _peer, "Ben"), RoomService.Result.OK)


func test_a_card_says_whose_room_the_format_and_the_places() -> void:
	var text: String = OpenRoomsList.card_text({"code": "ABCDEFGH", "host_name": "Ana", "team_size": 2, "players": 3, "first_to": 5})
	assert_eq(text, "Ana's room\n2v2 · 3/4 · First to 5")
	assert_string_starts_with(OpenRoomsList.card_text({"code": "X", "host_name": "", "team_size": 1, "players": 1}), "Open room")
