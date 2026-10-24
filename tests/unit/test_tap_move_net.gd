extends GutTest
## Wire format and trust boundary of tap to move, the hand of 4, elixir and stamina (docs/GDD.md "Controls: tap to move, cards to
## cast" > Data contract). Seams: NetProtocol, Snapshot, ServerMatch, and two MatchSims for determinism.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true


func _wire(message: Dictionary) -> Dictionary:
	return NetProtocol.decode(NetProtocol.encode(message))


func test_the_new_input_fields_survive_the_wire() -> void:
	var input: PlayerInput = PlayerInput.create(Vector2(0.5, 0.0), Vector2.ZERO, PlayerInput.BTN_CARD_3, 12)
	input.with_move_target(Vector2(3.0, -4.0)).with_aim_point(Vector2(-1.0, 2.0))
	var back: PlayerInput = NetProtocol.input_from_dict(_wire(NetProtocol.input_to_dict(input)))
	assert_eq(back.tick, 12)
	assert_eq(back.move, Vector2(0.5, 0.0))
	assert_eq(back.buttons, PlayerInput.BTN_CARD_3 | PlayerInput.BTN_MOVE_TARGET | PlayerInput.BTN_AIM_POINT)
	assert_eq(back.move_target, Vector2(3.0, -4.0))
	assert_eq(back.aim_point, Vector2(-1.0, 2.0))


func test_an_input_without_the_new_fields_has_no_target_and_no_aim_point() -> void:
	var back: PlayerInput = NetProtocol.input_from_dict({"t": NetProtocol.Msg.INPUT, "k": 3, "m": Vector2.ZERO, "a": Vector2.ZERO, "b": PlayerInput.BTN_MOVE_TARGET | PlayerInput.BTN_AIM_POINT})
	assert_false(back.is_pressed(PlayerInput.BTN_MOVE_TARGET), "a flag with no point is dropped")
	assert_false(back.is_pressed(PlayerInput.BTN_AIM_POINT))


func test_a_non_finite_or_mistyped_point_is_dropped() -> void:
	var back: PlayerInput = NetProtocol.input_from_dict({
		"t": NetProtocol.Msg.INPUT, "k": 3, "m": Vector2(NAN, 1.0), "a": "x", "b": PlayerInput.BTN_MOVE_TARGET | PlayerInput.BTN_AIM_POINT,
		"mt": Vector2(INF, 0.0), "ap": "far away",
	})
	assert_eq(back.move, Vector2.ZERO)
	assert_eq(back.aim, Vector2.ZERO)
	assert_false(back.is_pressed(PlayerInput.BTN_MOVE_TARGET))
	assert_false(back.is_pressed(PlayerInput.BTN_AIM_POINT))


func test_unknown_buttons_and_a_mistyped_tick_are_dropped() -> void:
	var back: PlayerInput = NetProtocol.input_from_dict({"t": NetProtocol.Msg.INPUT, "k": "soon", "m": Vector2.ZERO, "b": -1})
	assert_eq(back.buttons & ~PlayerInput.KNOWN_BITS, 0)
	assert_eq(back.tick, 0)


func test_the_protocol_version_was_bumped_for_the_new_controls() -> void:
	assert_eq(NetProtocol.VERSION, 3)


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.equip(1)
	sim.equip(2)
	return sim


func _mirror() -> MatchSim:
	var mirror: MatchSim = MatchSim.new(_rules, LAYOUT, 99)
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	return mirror


func test_a_snapshot_round_trip_carries_hand_next_elixir_stamina_exhaustion_and_your_own_target() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.players[1]
	me.elixir = 7.25
	me.stamina = 42.5
	me.still_time = 0.3
	me.exhaust_left = 1.5
	me.move_target = Vector2(3.0, 4.0)
	me.has_move_target = true
	me.stuck_time = 0.1
	var data: Dictionary = Snapshot.capture(sim, {})
	data.merge(Snapshot.private_for(sim, 1))
	var mirror: MatchSim = _mirror()
	Snapshot.apply(mirror, _wire(data), 1)
	var seen: PlayerState = mirror.players[1]
	assert_eq(seen.weapons, me.weapons)
	assert_eq(seen.weapons.size(), 4)
	assert_eq(seen.next_card, me.next_card)
	assert_eq(seen.elixir, 7.25)
	assert_eq(seen.stamina, 42.5)
	assert_eq(seen.still_time, 0.3)
	assert_eq(seen.exhaust_left, 1.5)
	assert_eq(seen.move_target, Vector2(3.0, 4.0))
	assert_true(seen.has_move_target)
	assert_eq(seen.stuck_time, 0.1)


func test_a_snapshot_carries_the_overclock_the_rescue_and_the_capture_speed() -> void:
	var sim: MatchSim = _sim()
	sim.players[2].overclocked = true
	sim.players[2].exhaust_left = 1.25
	sim.kill(1)
	sim.players[1].tag_order = 4
	sim.rescue_rescuer = [2, -1]
	sim.rescue_elapsed = [3.5, 0.0]
	sim.capture_team = 1
	sim.capture_kind = MatchSim.CaptureKind.BASE
	sim.capture_elapsed = 6.0
	sim.capture_needed = 12.0
	sim.capture_rate = 1.75
	var mirror: MatchSim = _mirror()
	Snapshot.apply(mirror, _wire(Snapshot.capture(sim, {})), 1)
	assert_true(mirror.players[2].overclocked, "everyone can see who is overclocked")
	assert_eq(mirror.players[2].exhaust_left, 1.25)
	assert_eq(mirror.players[1].tag_order, 4)
	assert_eq(mirror.rescue_rescuer, [2, -1] as Array[int])
	assert_eq(mirror.rescue_elapsed, [3.5, 0.0] as Array[float])
	assert_eq(mirror.capture_rate, 1.75)
	assert_almost_eq(mirror.capture_time_left(), 6.0 / 1.75, 0.001, "the countdown the HUD shows follows the speed")


func test_a_snapshot_never_tells_you_where_another_kid_walks_or_its_elixir() -> void:
	var sim: MatchSim = _sim()
	var them: PlayerState = sim.players[2]
	them.elixir = 9.0
	them.stamina = 12.0
	them.exhaust_left = 1.0
	them.move_target = Vector2(-5.0, 5.0)
	them.has_move_target = true
	var data: Dictionary = Snapshot.capture(sim, {})
	data.merge(Snapshot.private_for(sim, 1))
	var mirror: MatchSim = _mirror()
	Snapshot.apply(mirror, _wire(data), 1)
	var seen: PlayerState = mirror.players[2]
	assert_false(seen.has_move_target)
	assert_eq(seen.move_target, Vector2.ZERO)
	assert_ne(seen.elixir, 9.0, "another kid's elixir stays private")
	assert_eq(seen.exhaust_left, 1.0, "everyone sees who is exhausted")
	var text: String = var_to_str(data["players"])
	assert_false(text.contains("-5, 5"), "the enemy's target is not on the wire at all")


func test_a_snapshot_from_an_old_server_leaves_safe_defaults() -> void:
	var sim: MatchSim = _sim()
	var data: Dictionary = Snapshot.capture(sim, {})
	for entry: Dictionary in data["players"] as Array:
		entry.erase("ex")
	var mirror: MatchSim = _mirror()
	mirror.players[1].exhaust_left = 2.0
	mirror.players[1].has_move_target = true
	Snapshot.apply(mirror, data, 1)
	assert_eq(mirror.players[1].exhaust_left, 0.0)
	assert_true(mirror.players[1].has_move_target, "no private part: the local prediction keeps its own target")


func _script(sim: MatchSim, tick: int) -> void:
	# taps to move, casts, a WASD stretch, and enough running to get exhausted
	var input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, tick)
	if tick % 40 == 0:
		input.with_move_target(Vector2(float(tick % 23) - 11.0, float(tick % 37) - 18.0))
	if tick % 50 == 10:
		input.buttons |= PlayerInput.CARD_BITS[(tick / 50) % 4]
	if tick % 50 == 11:
		input.with_aim_point(sim.players[2].position)
	if tick > 300 and tick < 360:
		input.move = Vector2(0.0, 1.0)
	sim.set_input(1, input)
	var other: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2 if tick % 60 == 5 else 0, tick)
	if tick % 30 == 0:
		other.with_move_target(Vector2(float(tick % 17) - 8.0, -float(tick % 29)))
	sim.set_input(2, other)


func _state(sim: MatchSim) -> String:
	var parts: PackedStringArray = []
	for id: int in sim.players:
		var p: PlayerState = sim.players[id]
		parts.append(var_to_str([p.position, p.hp, p.tagged, p.elixir, p.stamina, p.exhaust_left, p.move_target, p.has_move_target, p.weapons, p.next_card, p.deck]))
	parts.append(var_to_str(sim.score.points))
	return "|".join(parts)


func test_the_same_seed_and_inputs_give_the_same_match() -> void:
	var first: MatchSim = _sim()
	var second: MatchSim = _sim()
	var exhausted: Array[int] = []
	first.exhausted.connect(func(id: int) -> void: exhausted.append(id))
	for tick: int in 30 * 40:
		_script(first, tick)
		_script(second, tick)
		first.step(DT)
		second.step(DT)
	assert_eq(_state(first), _state(second))
	assert_false(exhausted.is_empty(), "the script ran someone out of stamina")


class RecordingServer extends GameServer:
	var sent: Array[Dictionary] = []

	func send(peer_id: int, message: Dictionary) -> void:
		var copy: Dictionary = message.duplicate(true)
		copy["to"] = peer_id
		sent.append(copy)


func _running() -> ServerMatch:
	var room: RoomService.Room = RoomService.Room.new()
	for id: int in [1, 2]:
		var member: RoomService.Member = RoomService.Member.new()
		member.player_id = id
		member.peer_id = 10 + id
		member.team = id - 1
		member.connected = true
		room.members.append(member)
	var server: RecordingServer = RecordingServer.new(_rules, LAYOUT, NetRules.new(), 3)
	var net: NetRules = NetRules.new()
	net.snapshot_rate = 30
	net.max_queued_inputs = 8
	var running: ServerMatch = ServerMatch.new(server, room, _rules, LAYOUT, net, 5)
	running.stage = ServerMatch.Stage.PLAYING
	return running


func test_the_server_pulls_a_forged_target_into_the_street() -> void:
	var running: ServerMatch = _running()
	running.queue_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 1).with_move_target(Vector2(1.0e9, -1.0e9)))
	running.poll(DT)
	var state: PlayerState = running.sim.players[1]
	assert_true(LAYOUT.lane_rect().has_point(state.move_target), "the target is inside the street")
	assert_true(LAYOUT.lane_rect().has_point(state.position))


func test_the_server_clamps_a_forged_aim_point_to_the_range() -> void:
	var running: ServerMatch = _running()
	var state: PlayerState = running.sim.players[1]
	state.weapons[0] = &"lata"
	state.elixir = 10.0
	running.queue_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, 1))
	running.queue_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 2).with_aim_point(Vector2(0.0, -1.0e6)))
	running.poll(DT)
	running.poll(DT)
	assert_eq(running.sim.weapons.zones.size(), 1)
	assert_almost_eq(running.sim.weapons.zones[0].center.distance_to(state.position), 7.0, 0.01, "lata lands at most 7 m away")


func test_each_client_gets_only_its_own_private_part() -> void:
	var running: ServerMatch = _running()
	running.sim.players[2].move_target = Vector2(-5.0, 5.0)
	running.sim.players[2].has_move_target = true
	var server: RecordingServer = running._server as RecordingServer
	running.poll(DT)
	var snapshots: int = 0
	for message: Dictionary in server.sent:
		if message["t"] != NetProtocol.Msg.SNAPSHOT:
			continue
		snapshots += 1
		var mine: Dictionary = message["me"] as Dictionary
		assert_eq(mine["id"], (message["to"] as int) - 10, "the private part is the recipient's")
	assert_eq(snapshots, 2)


func test_a_dropped_player_stops_walking() -> void:
	var running: ServerMatch = _running()
	running.queue_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 1).with_move_target(Vector2(10.0, 0.0)))
	running.poll(DT)
	running.room.members[0].connected = false
	running.poll(DT)
	var at: Vector2 = running.sim.players[1].position
	running.poll(DT * 5.0)
	assert_eq(running.sim.players[1].position, at, "a dropped player idles")


func test_a_repeated_input_does_not_give_the_move_order_again() -> void:
	var running: ServerMatch = _running()
	var state: PlayerState = running.sim.players[1]
	running.queue_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 1).with_move_target(Vector2(5.0, 0.0)))
	running.poll(DT)
	running.sim.end_move(state)
	running.poll(DT)
	assert_false(state.has_move_target, "with no new input the last one repeats, but not its tap")


func test_a_mirror_takes_a_dealt_hand_of_four() -> void:
	var client: GameClient = GameClient.new(_rules, LAYOUT, NetRules.new())
	client._handle({"t": NetProtocol.Msg.MATCH_LOADING, "seed": 9, "players": [{"id": 1, "team": 0}]})
	var hand: Array[StringName] = [&"lata", &"jacks", &"bola", &"heal"]
	client._handle({"t": NetProtocol.Msg.MATCH_PLAY, "stage": "play", "time": 0.0, "loadouts": {1: hand}})
	var mine: PlayerState = client.mirror.players[1]
	assert_eq(mine.weapons, hand)
	assert_eq(mine.aim_hold, [-1.0, -1.0, -1.0, -1.0] as Array[float], "every slot idle")
