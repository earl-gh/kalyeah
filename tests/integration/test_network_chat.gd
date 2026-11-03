extends GutTest
## Quick chat stickers over the network: a sticker one player sends reaches everyone in the match (the sender too), the server refuses a bad number, a
## sticker outside play and a flood. Two real WebSocket clients against a real server, all in this process.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const PORT: int = 18947
const FRAME: float = 1.0 / 60.0

var server: GameServer
var a: GameClient
var b: GameClient


func before_each() -> void:
	server = GameServer.new(RULES, LAYOUT, NET, 5)
	assert_eq(server.listen(PORT), OK)
	a = GameClient.new(RULES, LAYOUT, NET)
	b = GameClient.new(RULES, LAYOUT, NET)
	a.player_name = "Ana"
	b.player_name = "Ben"


func after_each() -> void:
	a.close()
	b.close()
	server.stop()


## Runs everything for `seconds` of simulated time (real sockets, so it also waits).
func _pump(seconds: float, clients: Array[GameClient] = []) -> void:
	var all: Array[GameClient] = clients.duplicate()
	if all.is_empty():
		all = [a, b]
	for i: int in ceili(seconds / FRAME):
		server.poll(FRAME)
		for client: GameClient in all:
			client.poll(FRAME)
		OS.delay_usec(1000)


func _until(condition: Callable, limit: float = 5.0) -> bool:
	var waited: float = 0.0
	while waited < limit:
		if condition.call():
			return true
		_pump(0.05)
		waited += 0.05
	return condition.call()


func _connect_both() -> void:
	assert_eq(a.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_eq(b.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return a.is_online() and b.is_online()), "both connected")


## Lobby -> loading -> pick -> play for a 1v1.
func _start_1v1() -> void:
	_connect_both()
	a.create_room()
	assert_true(_until(func() -> bool: return not a.room.is_empty()), "room created")
	b.join_room(a.room["code"] as String)
	assert_true(_until(func() -> bool: return (b.room.get("members", []) as Array).size() == 2), "joined")
	a.set_ready(true)
	b.set_ready(true)
	assert_true(_until(func() -> bool: return a.room.get("can_start", false) as bool), "can start")
	a.start_match()
	assert_true(_until(func() -> bool: return a.in_match() and b.in_match()), "loading")
	a.loaded()
	b.loaded()
	assert_true(_until(func() -> bool: return a.stage == "pick" and b.stage == "pick"), "picking")
	a.pick_weapons(&"bato_light", &"lata")
	b.pick_weapons(&"jacks", &"bola")
	assert_true(_until(func() -> bool: return a.stage == "play" and b.stage == "play"), "playing")
	var running: ServerMatch = server.matches.values()[0]
	assert_eq(running.sim.phase, MatchSim.Phase.INTRO, "the server plays the opening show first")
	running.sim.intro_left = 0.0
	assert_true(_until(func() -> bool: return running.sim.phase == MatchSim.Phase.PLAYING), "then play")



func _collect(client: GameClient) -> Array[Array]:
	var got: Array[Array] = []
	client.said.connect(func(player: int, sticker: int) -> void: got.append([player, sticker]))
	return got


func test_a_sticker_reaches_everyone_in_the_match_the_sender_too() -> void:
	_start_1v1()
	var from_a: Array[Array] = _collect(a)
	var from_b: Array[Array] = _collect(b)
	a.say(3)
	assert_true(_until(func() -> bool: return not from_a.is_empty() and not from_b.is_empty()), "both phones get it")
	assert_eq(from_a, [[a.player_id, 3]] as Array[Array], "the sender sees it come back, as itself")
	assert_eq(from_b, [[a.player_id, 3]] as Array[Array], "the other player sees whose it is")
	_pump(NET.chat_min_gap + 0.2)
	b.say(5)
	assert_true(_until(func() -> bool: return from_a.size() == 2 and from_b.size() == 2))
	assert_eq(from_a[1], [b.player_id, 5], "and the other way round")


func test_the_server_refuses_a_bad_sticker_number_or_type() -> void:
	_start_1v1()
	var got: Array[Array] = _collect(b)
	for bad: Variant in [-1, NET.chat_stickers, 99, 1.0, "2", null, [1], {}]:
		a.send({"t": NetProtocol.Msg.SAY, "s": bad})
		_pump(NET.chat_min_gap + 0.1)
	a.send({"t": NetProtocol.Msg.SAY})
	_pump(0.3)
	assert_eq(got.size(), 0, "none of them reached the other player")
	a.say(0)
	assert_true(_until(func() -> bool: return got.size() == 1), "a good one still does (a refused one did not use up the wait)")


func test_one_player_cannot_flood_the_others_with_stickers() -> void:
	_start_1v1()
	var got: Array[Array] = _collect(b)
	for i: int in 5:
		a.say(i % NET.chat_stickers)
	_pump(0.4)
	assert_eq(got.size(), 1, "one every chat_min_gap, the rest dropped")
	_pump(NET.chat_min_gap + 0.1)
	a.say(1)
	assert_true(_until(func() -> bool: return got.size() == 2), "the next after the wait goes through")


func test_a_sticker_before_the_match_is_played_is_not_relayed() -> void:
	_connect_both()
	a.create_room()
	assert_true(_until(func() -> bool: return not a.room.is_empty()))
	b.join_room(a.room["code"] as String)
	assert_true(_until(func() -> bool: return (b.room.get("members", []) as Array).size() == 2))
	a.set_ready(true)
	b.set_ready(true)
	assert_true(_until(func() -> bool: return a.room.get("can_start", false) as bool))
	a.start_match()
	assert_true(_until(func() -> bool: return a.in_match() and b.in_match()), "loading")
	var got: Array[Array] = _collect(b)
	a.send({"t": NetProtocol.Msg.SAY, "s": 1})
	_pump(0.5)
	assert_eq(got.size(), 0, "the server holds it back while loading")
	assert_eq(a.stage, "loading")
	a.say(1)
	_pump(0.3)
	assert_eq(got.size(), 0, "and the client does not even send it")


func test_the_wire_numbers_and_rules_for_stickers() -> void:
	assert_gt(NetProtocol.Msg.SAY, NetProtocol.Msg.SET_FILL_BOTS, "appended: no older message number moved")
	assert_eq(NetProtocol.Msg.SAID, NetProtocol.Msg.SAY + 1)
	assert_eq(NetProtocol.Msg.SET_FILL_BOTS, 28, "the number the older builds know")
	assert_eq(NET.chat_stickers, QuickChat.PHRASES.size(), "the server accepts exactly the stickers the picker has")
	assert_gt(NET.chat_min_gap, 0.0)
