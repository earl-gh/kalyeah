extends GutTest
## Street games in rooms (owner, 2026-10-31: Local and Online too): four real WebSocket clients and a real server in this process play a
## 2v2 Patintero. The host picks the game in the room, the match skips the weapon pick, every phone's copy plays the same game (who
## runs, the guards' lines, the clock), and the game's moments (a tag) reach every phone. Local rooms run this same server inside the
## host's phone.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const ONLINE: PackedScene = preload("res://scenes/match/online.tscn")
const PORT: int = 18971
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


## A room of `count` players on `game` (a 2v2 for a team game), everyone ready and started; returns the server's match once it plays.
func _start(game: StringName, count: int = 4) -> ServerMatch:
	clients = clients.slice(0, count)
	for client: GameClient in clients:
		assert_eq(client.connect_to("ws://127.0.0.1:%d" % PORT), OK)
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.is_online())), "all online")
	var host: GameClient = clients[0]
	host.create_room(2)
	assert_true(_until(func() -> bool: return not host.room.is_empty()), "room")
	host.set_map(game)
	assert_true(_until(func() -> bool: return host.room.get("map", "") == String(game)), "the host picked %s" % game)
	for client: GameClient in clients.slice(1):
		client.join_room(host.room["code"] as String)
	assert_true(_until(func() -> bool: return (host.room.get("members", []) as Array).size() == count), "all in the room")
	for client: GameClient in clients:
		client.set_ready(true)
	assert_true(_until(func() -> bool: return host.room.get("can_start", false) as bool), "can start")
	host.start_match()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.in_match())), "loading")
	for client: GameClient in clients:
		client.loaded()
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return c.stage == "play")), "straight to play: no pick")
	var running: ServerMatch = server.matches.values()[0]
	running.sim.intro_left = 0.0
	assert_true(_until(func() -> bool: return running.sim.phase == MatchSim.Phase.PLAYING), "playing")
	return running


func test_a_room_plays_patintero_with_no_pick_and_every_phone_runs_the_same_game() -> void:
	var running: ServerMatch = _start(&"patintero")
	assert_true(running.sim.mode is Patintero, "the server plays Patintero")
	for id: int in running.sim.players:
		assert_true(running.sim.players[id].weapons.all(func(w: StringName) -> bool: return w == &""), "no hand of cards")
	_pump(0.5)
	var game: Patintero = running.sim.mode as Patintero
	for client: GameClient in clients:
		assert_true(client.mirror.mode is Patintero, "%s's phone plays it too" % client.player_name)
		var copy: Patintero = client.mirror.mode as Patintero
		assert_eq(copy.attack_team, game.attack_team, "the same team runs")
		assert_eq(copy.guard_axis, game.guard_axis, "the same guards on the same lines")
		assert_eq(copy.guard_line, game.guard_line)
		assert_almost_eq(copy.time_left, game.time_left, 0.5, "the same clock")


func test_a_tag_on_the_server_reaches_every_phone() -> void:
	var running: ServerMatch = _start(&"patintero")
	var game: Patintero = running.sim.mode as Patintero
	var seen: Array[int] = []
	for client: GameClient in clients:
		client.mirror.mode.tagged.connect(func(_tagger: int, target: int) -> void: seen.append(target))
	game.grace_left = 0.0
	var guard: PlayerState = null
	var runner: PlayerState = null
	for id: int in running.sim.players:
		var state: PlayerState = running.sim.players[id]
		if game.is_attacker(state) and runner == null:
			runner = state
		elif not game.is_attacker(state) and game.guard_axis.get(id, -1) == Patintero.Axis.HORIZONTAL and guard == null:
			guard = state
	assert_not_null(guard)
	assert_not_null(runner)
	# the runner steps onto the guard's line, right beside it, out in the court
	runner.position = guard.position + Vector2(0.3, 0.0)
	assert_true(_until(func() -> bool: return seen.size() == clients.size(), 3.0), "every phone saw the tag")
	assert_true(seen.all(func(target: int) -> bool: return target == runner.id), "of that runner")


func test_the_room_shows_a_street_game_as_timed() -> void:
	var rooms: RoomService = RoomService.new(NET, 3)
	var room: RoomService.Room = rooms.create_room(7, "Ana", 2)
	assert_true(rooms.set_map(7, &"patintero"))
	var listed: Dictionary = rooms.open_rooms(10)[0]
	assert_string_ends_with(OpenRoomsList.card_text(listed), "· Patintero", "the list names the game, not first to")
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.set_online(true)
	var info: Dictionary = rooms.describe(room)
	view.show_room(info, 99)  # a guest
	assert_eq(view.format_summary.text, "2v2 · 5 min · Open", "timed, so no first to")
	view.show_room(info, room.host_player_id)
	assert_false((view.first_to_buttons[3].get_parent() as Control).visible, "the host has no first to to pick")


func test_the_match_screen_plays_patintero_online_with_the_street_pad_and_moves_on_the_server() -> void:
	var running: ServerMatch = _start(&"patintero")
	var me: GameClient = clients[0]
	var view: PracticeMatch = autofree(ONLINE.instantiate()) as PracticeMatch
	view.client = me
	add_child(view)
	views.append(view)
	_pump(0.3)
	assert_false(view.is_picking(), "no weapon pick in a street game")
	assert_true(view.hud.street_game, "the street game's controls")
	assert_true(view.hud.street_pad.visible, "the knob and the action button")
	assert_false(view.hud.hand.visible, "no hand of cards")
	assert_not_null(view.mode_view, "the street game's own view (the lines)")
	var start: Vector2 = running.sim.players[me.player_id].position
	var dt: float = RULES.tick_dt()
	for i: int in 20:
		me.send_input(PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, 1000 + i), dt)
		_pump(dt)
	_pump(0.3)
	assert_gt(running.sim.players[me.player_id].position.distance_to(start), 0.5, "the kid moved on the server")


# ---- Tumbang Preso ---------------------------------------------------------------------

func test_a_tumbang_preso_room_has_six_places_and_needs_three_ready_kids() -> void:
	var rooms: RoomService = RoomService.new(NET, 3)
	var room: RoomService.Room = rooms.create_room(7, "Ana", 1)
	assert_true(rooms.set_map(7, &"tumbang_preso"), "open in rooms (owner, 2026-10-31)")
	assert_eq(room.team_size, 3, "six places, whatever the size was")
	assert_false(rooms.set_team_size(7, 1), "and no size to pick")
	rooms.join(room.code, 8, "Ben")
	for m: RoomService.Member in room.members:
		m.ready = true
	assert_false(rooms.can_start(room), "two kids are not enough")
	rooms.join(room.code, 9, "Cy")
	room.members[2].ready = true
	assert_true(rooms.can_start(room), "three ready kids can start, whatever their columns")
	var card: String = OpenRoomsList.card_text(rooms.describe(room).merged({"host_name": "Ana", "players": 3}))
	assert_string_ends_with(card, "3/6 · Tumbang Preso", "no NvN: every kid for itself")


func test_a_room_plays_tumbang_preso_and_a_throw_flies_and_lands_on_every_phone() -> void:
	var running: ServerMatch = _start(&"tumbang_preso", 3)
	assert_true(running.sim.mode is TumbangPreso)
	var game: TumbangPreso = running.sim.mode as TumbangPreso
	for id: int in running.sim.players:
		assert_eq(running.sim.players[id].team, 0, "every kid for itself: one team in the sim")
	_pump(0.4)
	for client: GameClient in clients:
		var copy: TumbangPreso = client.mirror.mode as TumbangPreso
		assert_not_null(copy, "%s plays Tumbang Preso" % client.player_name)
		assert_eq(copy.taya_id, game.taya_id, "the same taya")
		assert_eq(client.mirror.players[client.player_id].team, 0)
	var thrower: GameClient = null
	for client: GameClient in clients:
		if client.player_id != game.taya_id:
			thrower = client
			break
	var landed: Array[int] = []
	for client: GameClient in clients:
		(client.mirror.mode as TumbangPreso).slipper_landed.connect(func(id: int, _at: Vector2, _hit: bool) -> void: landed.append(id))
	game.grace_left = 0.0
	var dt: float = RULES.tick_dt()
	# hold Throw for half a second aiming up the street, then let go: the real input, over the network
	for i: int in 15:
		thrower.send_input(PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_CARD_1, 2000 + i), dt)
		_pump(dt)
	thrower.send_input(PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 2015), dt)
	assert_true(_until(func() -> bool: return game.flights.has(thrower.player_id) or game.slippers.has(thrower.player_id), 2.0), "the server threw it")
	if game.flights.has(thrower.player_id):
		assert_true(_until(func() -> bool: return clients.any(func(c: GameClient) -> bool: return (c.mirror.mode as TumbangPreso).flights.has(thrower.player_id)), 1.0)
			or game.slippers.has(thrower.player_id), "a phone sees it in the air, linked to its own projectile")
	assert_true(_until(func() -> bool: return landed.size() == clients.size(), 4.0), "every phone saw it land")
	for client: GameClient in clients:
		assert_false((client.mirror.mode as TumbangPreso).has_slipper(thrower.player_id), "no slipper in hand on any phone")


func test_a_point_in_tumbang_preso_reaches_every_phone() -> void:
	var running: ServerMatch = _start(&"tumbang_preso", 3)
	var game: TumbangPreso = running.sim.mode as TumbangPreso
	var scorer: int = -1
	for id: int in running.sim.players:
		if id != game.taya_id:
			scorer = id
			break
	game.award(running.sim, running.sim.players[scorer])
	assert_true(_until(func() -> bool: return clients.all(func(c: GameClient) -> bool: return (c.mirror.mode as TumbangPreso).points.get(scorer, 0) == 1), 2.0),
		"every phone counts the point")
