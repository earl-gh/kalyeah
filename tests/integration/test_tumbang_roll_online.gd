extends GutTest
## The Mataya-taya! roll of Tumbang Preso's opening show in Local and Online rooms (owner: it must show in Practice, Local and Online): three
## real WebSocket clients and a real server in this process, each phone with its match screen. Every phone plays the roll while the server's
## opening show runs and it stops on the kid the server drew as the taya. Local rooms run this same server inside the host's phone.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const ONLINE: PackedScene = preload("res://scenes/match/online.tscn")
const PORT: int = 18983
const FRAME: float = 1.0 / 60.0

var server: GameServer
var clients: Array[GameClient] = []
var views: Array[PracticeMatch] = []


func before_each() -> void:
	views.clear()
	server = GameServer.new(RULES, LAYOUT, NET, 21)
	assert_eq(server.listen(PORT), OK)
	clients.clear()
	for player_name: String in ["Ana", "Ben", "Cy", "Dee"]:
		var client: GameClient = GameClient.new(RULES, LAYOUT, NET)
		client.player_name = player_name
		clients.append(client)


func after_each() -> void:
	for client: GameClient in clients:
		client.close()
	server.stop()


func _pump(seconds: float) -> void:
	for i: int in ceili(seconds / FRAME):
		server.poll(FRAME)
		for client: GameClient in clients:
			client.poll(FRAME)
		for view: PracticeMatch in views:
			if is_instance_valid(view):
				view.advance(FRAME)
		OS.delay_usec(1000)


func _until(condition: Callable, limit: float = 6.0) -> bool:
	var waited: float = 0.0
	while waited < limit and not condition.call():
		_pump(0.05)
		waited += 0.05
	return condition.call()


## A room of three on Tumbang Preso, everyone ready and started, the weapon pick skipped; returns the server's match as soon as it plays (the
## opening show is NOT skipped).
func _start_with_intro() -> ServerMatch:
	clients = clients.slice(0, 3)
	for client: GameClient in clients:
		assert_eq(client.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.is_online())), "all online")
	var host: GameClient = clients[0]
	host.create_room()
	assert_true(_until(func() -> bool: return not host.room.is_empty()), "room")
	host.set_map(&"tumbang_preso")
	assert_true(_until(func() -> bool: return host.room.get("map", "") == "tumbang_preso"), "the host picked Tumbang Preso")
	for client: GameClient in clients.slice(1):
		client.join_room(host.room["code"] as String)
	assert_true(_until(func() -> bool: return (host.room.get("members", []) as Array).size() == 3), "all in the room")
	for client: GameClient in clients:
		client.set_ready(true)
	assert_true(_until(func() -> bool: return host.room.get("can_start", false) as bool), "can start")
	host.start_match()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.in_match())), "loading")
	for client: GameClient in clients:
		client.loaded()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.stage == "play")), "straight to play: no pick")
	return server.matches.values()[0]


func test_every_phone_plays_the_mataya_taya_roll_and_it_stops_on_the_servers_taya() -> void:
	var running: ServerMatch = _start_with_intro()
	assert_eq(running.sim.phase, MatchSim.Phase.INTRO, "the server plays the opening show")
	var taya: int = (running.sim.mode as TumbangPreso).taya_id
	for client: GameClient in clients:
		var view: PracticeMatch = autofree(ONLINE.instantiate()) as PracticeMatch
		view.client = client
		add_child(view)
		views.append(view)
	_pump(0.8)
	assert_eq(running.sim.phase, MatchSim.Phase.INTRO, "still in the opening show")
	for view: PracticeMatch in views:
		var roll: TumbangRoll = view._presentation._roll
		assert_not_null(roll, "%s's phone shows the roll" % view.local_id)
		assert_true(roll.visible, "and it is on screen")
		assert_eq(roll.title_label.text, TumbangRoll.TITLE, "Mataya-taya!")
	# the roll ends on the same kid everywhere: the taya's
	_until(func() -> bool: return running.sim.intro_left < 2.0, 6.0)
	for view: PracticeMatch in views:
		var roll: TumbangRoll = view._presentation._roll
		assert_not_null(roll, "still rolling or showing the result")
		assert_eq(roll._kids[roll._final], view.sim.players[taya].character_id, "it lands on the taya's kid")
	_until(func() -> bool: return running.sim.phase == MatchSim.Phase.PLAYING, 8.0)
	_pump(0.3)
	for view: PracticeMatch in views:
		assert_null(view._presentation._roll, "the roll is gone when play starts")
