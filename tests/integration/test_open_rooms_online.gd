extends GutTest
## Online open rooms end to end against a real in-process server (owner, 2026-10-30): the Join screen lists open rooms with no
## code to type, a tap or Quick join gets you in, a private room is not listed, a started room drops off the list, and the server
## answers one phone's list at most once a `room_list_min_gap`.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const LOBBY: PackedScene = preload("res://scenes/ui/lobby.tscn")
const PORT: int = 18951
const URL: String = "ws://127.0.0.1:18951"
const FRAME: float = 1.0 / 60.0

var server: GameServer
var screens: Array[LobbyScreen] = []
var _saved_url: String
var _saved_name: String
var _saved_hotspot: bool
## The lobby saves your room and rejoin token as it goes: put the player's own back afterwards.
var _saved_session: Dictionary = {}


func before_each() -> void:
	_saved_url = Settings.server_url
	_saved_name = Session.player_name
	_saved_hotspot = Session.lobby_hotspot
	_saved_session = {"room_code": Session.room_code, "reconnect_token": Session.reconnect_token,
		"reconnect_url": Session.reconnect_url, "avatar_id": Session.avatar_id}
	Session.lobby_hotspot = false
	Settings.server_url = URL
	server = GameServer.new(RULES, LAYOUT, NET, 9)
	assert_eq(server.listen(PORT), OK)
	screens.clear()


func after_each() -> void:
	for screen: LobbyScreen in screens:
		screen.client.close()
	server.stop()
	Settings.server_url = _saved_url
	Session.player_name = _saved_name
	Session.lobby_hotspot = _saved_hotspot
	for key: String in _saved_session:
		Session.set(key, _saved_session[key])
	Session.save()
	await wait_process_frames(2)


func _lobby(intent: LobbyScreen.Intent, player_name: String) -> LobbyScreen:
	Session.player_name = player_name
	var lobby: LobbyScreen = autofree(LOBBY.instantiate()) as LobbyScreen
	lobby.client = GameClient.new(RULES, LAYOUT, NET)
	lobby.client.player_name = player_name
	lobby.enter_match_on_start = false
	lobby.set_intent(intent)
	add_child(lobby)
	screens.append(lobby)
	return lobby


## Runs the server, the clients and the screens' own frames (the Join screen asks for the list in `_process`).
func _pump(seconds: float) -> void:
	for i: int in ceili(seconds / FRAME):
		server.poll(FRAME)
		for screen: LobbyScreen in screens:
			screen.client.poll(FRAME)
			screen._process(FRAME)
		OS.delay_usec(1000)


func _until(condition: Callable, limit: float = 6.0) -> bool:
	var waited: float = 0.0
	while waited < limit and not condition.call():
		_pump(0.05)
		waited += 0.05
	return condition.call()


func _host(player_name: String = "Ana") -> LobbyScreen:
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE, player_name)
	await wait_process_frames(1)  # Create starts itself, deferred
	assert_true(_until(func() -> bool: return host.in_room()), "%s's room is open" % player_name)
	return host


func test_a_guest_sees_the_open_room_and_joins_it_with_one_tap_no_code() -> void:
	var host: LobbyScreen = await _host("Ana")
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Ben")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return guest.open_rooms.cards.size() == 1), "the room is listed")
	assert_string_starts_with(guest.open_rooms.cards[0].text, "Ana's room")
	assert_false(guest.code_section.visible, "no code to type")
	guest.open_rooms.cards[0].pressed.emit()
	assert_true(_until(func() -> bool: return guest.in_room()), "one tap and Ben is in")
	assert_eq(guest.client.room["code"], host.client.room["code"])


func test_quick_join_takes_the_fullest_open_room() -> void:
	var small: LobbyScreen = await _host("Ana")
	var busy: LobbyScreen = await _host("Cy")
	busy.client.set_team_size(3)
	var friend: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Dee")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return friend.open_rooms.cards.size() == 2))
	friend.open_rooms.cards[friend.open_rooms.cards.size() - 1].pressed.emit()  # Cy's room (listed last, both have one player)
	assert_true(_until(func() -> bool: return friend.in_room()))
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Ben")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return guest.open_rooms.cards.size() == 2 and not guest.open_rooms.quick_button.disabled))
	guest.open_rooms.quick_button.pressed.emit()
	assert_true(_until(func() -> bool: return guest.in_room()))
	assert_eq(guest.client.room["code"], busy.client.room["code"], "the room with two already in it")
	assert_ne(guest.client.room["code"], small.client.room["code"])


func test_a_private_room_is_not_listed_and_the_screen_offers_to_create_one() -> void:
	var host: LobbyScreen = await _host("Ana")
	host._room_view.open_buttons[false].pressed.emit()  # the host taps Private
	assert_true(_until(func() -> bool: return host.client.room.get("open", true) == false), "the server made it private")
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Ben")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return guest.open_rooms.empty_label.text == OpenRoomsList.EMPTY_TEXT))
	assert_eq(guest.open_rooms.cards.size(), 0)
	assert_true(guest.open_rooms.create_button.visible, "Create one!")
	assert_true(guest.open_rooms.quick_button.disabled)
	guest.toggle_code()
	assert_true(guest.code_section.visible, "the code still gets you into a private room")
	guest.set_fields("", "Ben", host.client.room["code"] as String)
	guest.submit()
	assert_true(_until(func() -> bool: return guest.in_room()))


func test_create_from_the_empty_list_opens_a_room_on_the_same_connection() -> void:
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Ben")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return guest.open_rooms.create_button.visible))
	guest.open_rooms.create_button.pressed.emit()
	assert_true(_until(func() -> bool: return guest.in_room()), "Ben's own room")
	assert_true(guest.client.is_host())


func test_a_started_room_drops_off_the_list_and_a_late_tap_is_turned_away() -> void:
	var host: LobbyScreen = await _host("Ana")
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN, "Ben")
	await wait_process_frames(1)
	assert_true(_until(func() -> bool: return guest.open_rooms.cards.size() == 1))
	var stale: Button = guest.open_rooms.cards[0]
	var room: RoomService.Room = server.rooms.rooms[host.client.room["code"] as String]
	room.state = RoomService.State.LOADING  # the host started
	stale.pressed.emit()
	assert_true(_until(func() -> bool: return guest.status_text().contains("already started")), "told, not stuck")
	assert_true(_until(func() -> bool: return guest.open_rooms.cards.is_empty()), "and the list no longer shows it")
	assert_false(guest.in_room())


func test_the_server_answers_one_phone_at_most_once_a_gap() -> void:
	var client: GameClient = GameClient.new(RULES, LAYOUT, NET)
	client.player_name = "Ben"
	var answers: Array[int] = []
	client.rooms_listed.connect(func(rooms: Array[Dictionary]) -> void: answers.append(rooms.size()))
	client.connect_to(URL)
	var welcomed: Array[bool] = []
	client.welcomed.connect(func(_r: bool) -> void: welcomed.append(true))
	for i: int in 120:
		server.poll(FRAME)
		client.poll(FRAME)
		OS.delay_usec(1000)
		if not welcomed.is_empty():
			break
	for i: int in 5:
		client.list_rooms()
	for i: int in 30:
		server.poll(FRAME)
		client.poll(FRAME)
		OS.delay_usec(1000)
	assert_eq(answers.size(), 1, "five asks in a burst, one answer")
	client.close()
