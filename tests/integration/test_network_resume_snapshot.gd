extends GutTest
## Reconnect sequence continuity and the snapshot state needed by match views.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const PORT: int = 18934
const FRAME: float = 1.0 / 60.0

var server: GameServer
var clients: Array[GameClient] = []


func before_each() -> void:
	server = null


func after_each() -> void:
	for client: GameClient in clients:
		client.close()
	clients.clear()
	if server != null:
		server.stop()


func _client(name: String) -> GameClient:
	var client: GameClient = GameClient.new(RULES, LAYOUT, NET)
	client.player_name = name
	clients.append(client)
	return client


func _pump(seconds: float) -> void:
	for _frame: int in ceili(seconds / FRAME):
		server.poll(FRAME)
		for client: GameClient in clients:
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


func _start_1v1(a: GameClient, b: GameClient) -> ServerMatch:
	assert_eq(a.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_eq(b.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return a.is_online() and b.is_online()), "connected")
	a.create_room()
	assert_true(_until(func() -> bool: return not a.room.is_empty()), "room created")
	b.join_room(a.room["code"] as String)
	assert_true(_until(func() -> bool: return (b.room.get("members", []) as Array).size() == 2), "joined")
	a.set_ready(true)
	b.set_ready(true)
	assert_true(_until(func() -> bool: return a.room.get("can_start", false) as bool), "ready")
	a.start_match()
	assert_true(_until(func() -> bool: return a.in_match() and b.in_match()), "match loading")
	a.loaded()
	b.loaded()
	assert_true(_until(func() -> bool: return a.stage == "pick" and b.stage == "pick"), "weapon pick")
	a.pick_weapons(&"bato_light", &"lata")
	b.pick_weapons(&"jacks", &"bola")
	assert_true(_until(func() -> bool: return a.stage == "play" and b.stage == "play"), "match playing")
	var running: ServerMatch = server.matches.values()[0]
	running.sim.intro_left = 0.0
	assert_true(_until(func() -> bool: return running.sim.phase == MatchSim.Phase.PLAYING), "intro ended")
	return running


func test_fresh_client_counter_resumes_after_the_server_ack() -> void:
	server = GameServer.new(RULES, LAYOUT, NET, 37)
	assert_eq(server.listen(PORT), OK)
	var a: GameClient = _client("Ana")
	var b: GameClient = _client("Ben")
	var running: ServerMatch = _start_1v1(a, b)
	var dt: float = RULES.tick_dt()
	for sequence: int in range(1, 91):
		a.send_input(PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sequence), dt)
		_pump(dt)
	assert_eq(running.next_input_sequence(a.player_id), 91, "the server acknowledged sequence 90")

	var token: String = a.token
	var player_id: int = a.player_id
	a.close()
	_pump(0.2)
	var resumed: GameClient = _client("Ana")
	resumed.token = token
	assert_eq(resumed.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return resumed.in_match() and resumed.stage == "play"), "slot reclaimed")
	assert_eq(resumed.player_id, player_id)
	assert_eq(resumed.next_input_sequence, 91, "WELCOME supplies the first acceptable sequence")

	resumed.send_input(PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, 1), dt)
	assert_eq(resumed.next_input_sequence, 92, "a reset source counter maps onto the resumed sequence")
	assert_true(_until(func() -> bool: return running.next_input_sequence(player_id) == 92), "the first resumed input was accepted")


func test_snapshot_preserves_cast_lock_and_shield_presentation_state() -> void:
	var source: MatchSim = MatchSim.new(RULES, LAYOUT, 7)
	var source_player: PlayerState = source.add_player(1, 0)
	source_player.cast_lock_left = 0.37
	var shield: WeaponSystem.Shield = WeaponSystem.Shield.new()
	shield.id = 42
	shield.def = source.weapon_defs[&"langit_lupa"]
	shield.team = 0
	shield.center = Vector2(2.0, -3.0)
	shield.along = Vector2.RIGHT
	shield.half_width = 0.6
	shield.time_left = 1.75
	source.weapons.shields.append(shield)

	var mirror: MatchSim = MatchSim.new(RULES, LAYOUT, 7)
	mirror.add_player(1, 0)
	Snapshot.apply(mirror, Snapshot.capture(source, {1: 12}), 1)

	assert_almost_eq(mirror.players[1].cast_lock_left, 0.37, 0.0001, "the local mirror stays rooted during a heavy cast")
	assert_eq(mirror.weapons.shields.size(), 1)
	var restored: WeaponSystem.Shield = mirror.weapons.shields[0]
	assert_eq(restored.def.id, &"langit_lupa", "the view can choose the soil wall model")
	assert_almost_eq(restored.time_left, 1.75, 0.0001, "the rise and collapse animation keeps its phase")
	assert_eq(restored.center, shield.center)
	assert_eq(restored.along, shield.along)


func test_new_snapshot_reader_accepts_the_previous_optional_shape() -> void:
	var source: MatchSim = MatchSim.new(RULES, LAYOUT, 11)
	source.add_player(1, 0)
	var shield: WeaponSystem.Shield = WeaponSystem.Shield.new()
	shield.id = 8
	shield.def = source.weapon_defs[&"papel_shield"]
	shield.team = 0
	shield.center = Vector2.ZERO
	shield.along = Vector2.RIGHT
	shield.half_width = 0.5
	shield.time_left = 2.0
	source.weapons.shields.append(shield)
	var legacy: Dictionary = Snapshot.capture(source, {})
	(legacy["players"] as Array)[0].erase("cl")
	(legacy["shields"] as Array)[0].erase("w")
	(legacy["shields"] as Array)[0].erase("tl")

	var mirror: MatchSim = MatchSim.new(RULES, LAYOUT, 11)
	mirror.add_player(1, 0)
	Snapshot.apply(mirror, legacy, 1)
	assert_eq(mirror.weapons.shields.size(), 1, "additive fields stay compatible with an older server")
	assert_null(mirror.weapons.shields[0].def)
