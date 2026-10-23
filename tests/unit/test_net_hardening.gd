extends GutTest
## Trust boundary and wire format of the card hand (audit 4): the protocol version is checked on both ends, a pick during play
## changes nothing, and the shuffler queue and an empty hand slot survive the trip to a client.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _was: bool


## Keeps what the server would have sent instead of writing it to a socket.
class RecordingServer extends GameServer:
	var sent: Array[Dictionary] = []

	func send(_peer_id: int, message: Dictionary) -> void:
		sent.append(message)


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle


func after_each() -> void:
	_rules.card_cycle = _was


func _server() -> RecordingServer:
	return RecordingServer.new(_rules, LAYOUT, NetRules.new(), 3)


func test_hello_from_another_version_is_refused_with_the_update_text() -> void:
	var server: RecordingServer = _server()
	server._handle(7, {"t": NetProtocol.Msg.HELLO, "name": "Old", "token": ""})
	assert_eq(server.sent.size(), 1)
	assert_eq(server.sent[0]["t"], NetProtocol.Msg.ERROR, "an old client (no version) gets an error, not a welcome")
	assert_eq(server.sent[0]["text"], NetProtocol.INCOMPATIBLE_TEXT)


func test_hello_with_this_version_is_welcomed() -> void:
	var server: RecordingServer = _server()
	server._handle(7, {"t": NetProtocol.Msg.HELLO, "name": "New", "token": "", "v": NetProtocol.VERSION})
	assert_eq(server.sent[0]["t"], NetProtocol.Msg.WELCOME)


func test_the_card_hand_bumped_the_protocol() -> void:
	assert_gt(NetProtocol.VERSION, 1, "hand slots can be empty since the card cycle: version 1 clients cannot read them")


func test_a_welcome_from_another_version_shows_the_update_text_and_does_not_welcome() -> void:
	var client: GameClient = GameClient.new(_rules, LAYOUT, NetRules.new())
	watch_signals(client)
	client._handle({"t": NetProtocol.Msg.WELCOME, "v": NetProtocol.VERSION - 1, "reconnected": false})
	assert_signal_emitted_with_parameters(client, "error_received", [NetProtocol.INCOMPATIBLE_TEXT])
	assert_signal_not_emitted(client, "welcomed")


func test_a_pick_during_play_changes_nothing() -> void:
	_rules.card_cycle = false
	var room: RoomService.Room = RoomService.Room.new()
	var member: RoomService.Member = RoomService.Member.new()
	member.player_id = 1
	room.members.append(member)
	var running: ServerMatch = ServerMatch.new(null, room, _rules, LAYOUT, NetRules.new(), 5)
	running.sim.set_loadout(1, &"langit_lupa", &"trumpo")
	running.stage = ServerMatch.Stage.PLAYING
	var state: PlayerState = running.sim.players[1]
	state.weapon_cooldowns = [6.0, 6.0]
	running.pick(1, &"bato_heavy", &"heal")
	assert_eq(state.weapons, [&"langit_lupa", &"trumpo"] as Array[StringName])
	assert_eq(state.weapon_cooldowns, [6.0, 6.0] as Array[float])


func _card_sim() -> MatchSim:
	_rules.card_cycle = true
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.deal_cards(1)
	sim.begin_intro()
	sim.intro_left = 0.0
	return sim


## Uses both cards: the first goes to the shuffler, the second waits in its queue.
func _use_both(sim: MatchSim) -> void:
	sim.cycle_used(sim.players[1], 0)
	sim.cycle_used(sim.players[1], 1)
	sim.step(DT)


func _mirror() -> MatchSim:
	var mirror: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	mirror.add_player(1, 0)
	return mirror


func test_the_snapshot_carries_the_shuffler_queue_over_the_wire() -> void:
	var sim: MatchSim = _card_sim()
	_use_both(sim)
	assert_false(sim.players[1].incoming.is_empty(), "the second card waits while the shuffler is busy")
	var wire: Dictionary = bytes_to_var(NetProtocol.encode(Snapshot.capture(sim, {}))) as Dictionary
	var mirror: MatchSim = _mirror()
	Snapshot.apply(mirror, wire, 1)
	assert_eq(mirror.players[1].incoming, sim.players[1].incoming)


func test_a_snapshot_from_an_old_server_leaves_the_queue_empty() -> void:
	var sim: MatchSim = _card_sim()
	_use_both(sim)
	var data: Dictionary = Snapshot.capture(sim, {})
	for entry: Dictionary in data["players"] as Array:
		entry.erase("in")
	var mirror: MatchSim = _mirror()
	mirror.players[1].incoming.append(&"jacks")
	Snapshot.apply(mirror, data, 1)
	assert_eq(mirror.players[1].incoming, [] as Array[StringName])


func test_a_reconnecting_mirror_takes_a_hand_with_an_empty_slot() -> void:
	_rules.card_cycle = true
	var client: GameClient = GameClient.new(_rules, LAYOUT, NetRules.new())
	client._handle({"t": NetProtocol.Msg.MATCH_LOADING, "seed": 9, "players": [{"id": 1, "team": 0}]})
	client._handle({"t": NetProtocol.Msg.MATCH_PLAY, "stage": "play", "time": 0.0, "loadouts": {1: [&"lata", &""]}})
	assert_eq(client.mirror.players[1].weapons, [&"lata", &""] as Array[StringName])


func test_a_loadout_match_play_still_validates_the_pair() -> void:
	_rules.card_cycle = false
	var client: GameClient = GameClient.new(_rules, LAYOUT, NetRules.new())
	client._handle({"t": NetProtocol.Msg.MATCH_LOADING, "seed": 9, "players": [{"id": 1, "team": 0}]})
	client._handle({"t": NetProtocol.Msg.MATCH_PLAY, "stage": "play", "time": 0.0, "loadouts": {1: [&"lata", &"nonsense"]}})
	assert_ne(client.mirror.players[1].weapons, [&"lata", &"nonsense"] as Array[StringName])
