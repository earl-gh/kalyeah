extends GutTest
## Langit Lupa in a room (docs/GDD.md "Langit Lupa"): three real WebSocket clients and a real server in this process. Every phone plays the
## same game, and in a brownout a phone is never told where the kids out of its sight are, nor where their mounds stand.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const PORT: int = 18973
const FRAME: float = 1.0 / 60.0

var server: GameServer
var clients: Array[GameClient] = []


func before_each() -> void:
	server = GameServer.new(RULES, LAYOUT, NET, 21)
	assert_eq(server.listen(PORT), OK)
	clients.clear()
	for player_name: String in ["Ana", "Ben", "Cy"]:
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
		OS.delay_usec(1000)


func _until(condition: Callable, limit: float = 6.0) -> bool:
	var waited: float = 0.0
	while waited < limit and not condition.call():
		_pump(0.05)
		waited += 0.05
	return condition.call()


func _start() -> ServerMatch:
	for client: GameClient in clients:
		assert_eq(client.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.is_online())), "all online")
	var host: GameClient = clients[0]
	host.create_room()
	assert_true(_until(func() -> bool: return not host.room.is_empty()), "room")
	host.set_map(&"langit_lupa")
	assert_true(_until(func() -> bool: return host.room.get("map", "") == "langit_lupa"), "the host picked Langit Lupa")
	for client: GameClient in clients.slice(1):
		client.join_room(host.room["code"] as String)
	assert_true(_until(func() -> bool: return (host.room.get("members", []) as Array).size() == 3), "all in the room")
	for client: GameClient in clients:
		client.set_ready(true)
	assert_true(_until(func() -> bool: return host.room.get("can_start", false) as bool), "three kids can start")
	host.start_match()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.in_match())), "loading")
	for client: GameClient in clients:
		client.loaded()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.stage == "play")), "straight to play")
	var running: ServerMatch = server.matches.values()[0]
	running.sim.intro_left = 0.0
	assert_true(_until(func() -> bool: return running.sim.phase == MatchSim.Phase.PLAYING), "playing")
	return running


func test_every_phone_plays_the_same_langit_lupa_and_a_brownout_hides_the_far_kids_and_their_mounds() -> void:
	var running: ServerMatch = _start()
	var game: LangitLupa = running.sim.mode as LangitLupa
	assert_not_null(game, "the room plays Langit Lupa")
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool:
		return c.mirror.mode is LangitLupa and (c.mirror.mode as LangitLupa).taya_id == game.taya_id)), "every phone knows the taya")
	var ids: Array[int] = []
	for c: GameClient in clients:
		ids.append(c.player_id)
	var viewer: GameClient = clients[0]
	var far_id: int = ids[2]
	# lights out, the far kid out of the viewer's sight, the near one within it, the far one on its own mound
	game.grace_left = 99.0
	game.time_left = game.rules.match_seconds - game.rules.brownout_first - 0.5
	running.sim.players[ids[0]].position = Vector2(-6.0, 20.0)
	running.sim.players[ids[1]].position = Vector2(-6.0, 17.0)
	running.sim.players[far_id].position = Vector2(8.0, -20.0)
	game.raise_mound(running.sim, running.sim.players[far_id])
	var hidden: Callable = func() -> bool:
		var copy: LangitLupa = viewer.mirror.mode as LangitLupa
		return copy.is_brownout() and copy.unseen.has(far_id)
	assert_true(_until(hidden), "the viewer's copy knows it may not see the far kid")
	var copy: LangitLupa = viewer.mirror.mode as LangitLupa
	assert_false(copy.unseen.has(ids[1]), "the near kid is seen")
	assert_false(copy.mounds.has(far_id), "nor where the far kid's mound is")
	assert_false(copy.visible_to(viewer.mirror, viewer.player_id, far_id))
	assert_true(viewer.mirror.players[far_id].position.distance_to(Vector2(8.0, -20.0)) > 5.0, "nor where it stands")
	var snapshot: Dictionary = Snapshot.hide_for(running.sim, viewer.player_id, Snapshot.capture(running.sim, {}))
	for entry: Dictionary in snapshot["players"] as Array:
		if entry["id"] == far_id:
			assert_eq(entry["pos"], Vector2.ZERO, "the message itself carries no position")
	var far_copy: LangitLupa = clients[2].mirror.mode as LangitLupa
	assert_true(far_copy.mounds.has(far_id), "the far kid's own phone sees its mound")
	# the lights come back: everyone is seen again
	game.time_left = game.rules.match_seconds - game.rules.brownout_first - game.rules.brownout_seconds - 1.0
	assert_true(_until(func() -> bool: return (viewer.mirror.mode as LangitLupa).unseen.is_empty()), "seen again after the brownout")
	assert_true(_until(func() -> bool: return viewer.mirror.players[far_id].position.distance_to(running.sim.players[far_id].position) < 1.0),
		"and where it really is")
