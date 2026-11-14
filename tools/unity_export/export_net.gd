extends SceneTree
## Records the Godot game's network layer for the Unity port (unity/Kalyeah.Net), so the C# side can be checked byte for byte:
##   unity/Golden/Net/net_protocol.json   every message a client sends, input encodings both ways, decoding of bad packets, the codec
##   unity/Golden/Net/net_rooms.json      RoomService sequences (create/join/teams/ready/start blockers/bots/maps/leave/reconnect/lists)
##   unity/Golden/Net/net_snapshots.json  snapshots of a scripted 5v5 match and of a client's mirror after applying them
##   unity/Golden/Net/net_server.json     whole GameServer sessions: every packet in and every packet out
## Packets are `var_to_bytes` (what NetProtocol.encode sends), written as hex.
## (A sub-folder: unity/Kalyeah.Sim.Tests replays every unity/Golden/*.json as a match recording.)
## Run: godot --headless --path . -s tools/unity_export/export_net.gd

const OUT: String = "res://unity/Golden/Net/"
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")


## A GameClient whose packets are kept instead of sent.
class RecordingClient extends GameClient:
	var sent: Array[Dictionary] = []

	func send(message: Dictionary) -> void:
		sent.append(message)

	func is_online() -> bool:
		return true


## A GameServer whose packets are kept instead of sent, stepped without a socket.
class RecordingServer extends GameServer:
	var out: Array = []
	## Keep only the md5 of each packet's hex (the long sessions), not the hex itself.
	var hashed: bool = false

	func send(peer_id: int, message: Dictionary) -> void:
		var hex: String = var_to_bytes(message).hex_encode()
		out.append([peer_id, hex.md5_text() if hashed else hex])

	## GameServer.poll without the socket part.
	func fake_poll(delta: float) -> void:
		_clock += delta
		for room: RoomService.Room in rooms.step(delta):
			_send_room(room)
		for code: String in matches.keys():
			if not rooms.rooms.has(code):
				matches[code].dispose()
				matches.erase(code)
				continue
			matches[code].poll(delta)


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	_export_protocol()
	_export_rooms()
	_export_snapshots()
	_export_server()
	print("net goldens written")
	quit()


static func _hex(v: Variant) -> String:
	return var_to_bytes(v).hex_encode()


func _write(path: String, data: Variant, indent: String = " ") -> void:
	var f: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	f.store_string(JSON.stringify(data, indent, false, true))
	f.close()


# ---- protocol ------------------------------------------------------------------------------------------------------------

func _export_protocol() -> void:
	var msg_names: Dictionary = {}
	for key: String in NetProtocol.Msg.keys():
		msg_names[key] = NetProtocol.Msg[key]
	var client: RecordingClient = RecordingClient.new(RULES, LAYOUT, NET)
	var calls: Array[String] = []
	var record: Callable = func(name: String) -> void: calls.append(name)
	client.create_room(); record.call("create_room")
	client.join_room("abcd2345"); record.call("join_room")
	client.list_rooms(); record.call("list_rooms")
	client.set_open(false); record.call("set_open")
	client.set_fill_bots(true); record.call("set_fill_bots")
	client.pick_team(1); record.call("pick_team")
	client.set_first_to(5); record.call("set_first_to")
	client.set_map(&"patintero"); record.call("set_map")
	client.set_character(&"toni"); record.call("set_character")
	client.set_ready(true); record.call("set_ready")
	client.start_match(); record.call("start_match")
	client.loaded(); record.call("loaded")
	client.pick_weapons(&"bato_light", &"trumpo", &"migo", &"lata"); record.call("pick_weapons_4")
	client.pick_weapons(&"bato_light", &"trumpo"); record.call("pick_weapons_2")
	client.swap_weapons(&"jacks", &"bola", &"heal"); record.call("swap_weapons_3")
	client.swap_weapons(&"jacks", &"bola"); record.call("swap_weapons_2")
	client.say(3); record.call("say_outside_play")  # sends nothing: not playing
	client.stage = "play"
	client.say(3); record.call("say")
	client.mirror = MatchSim.new(RULES, LAYOUT, 5)
	client.mirror.add_player(1, 0)
	client.player_id = 1
	client.next_input_sequence = 40
	client.send_input(PlayerInput.create(Vector2(0.3, -0.25), Vector2(-1.5, 2.0), PlayerInput.BTN_DASH, 12), 1.0 / 30.0)
	record.call("send_input_mapped_forward")
	client.send_input(PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 100).with_move_target(Vector2(3.456, -7.891)), 1.0 / 30.0)
	record.call("send_input_target")
	client.leave_room(); record.call("leave_room")
	var sent: Array = []
	var index: int = 0
	for message: Dictionary in client.sent:
		sent.append(_hex(message))
	# the server's own messages that the session goldens may not reach
	var literal: Dictionary = {
		"hello": {"t": NetProtocol.Msg.HELLO, "name": "Ana", "token": "", "v": NetProtocol.VERSION},
		"hello_token": {"t": NetProtocol.Msg.HELLO, "name": "Ana", "token": "0123abcd4567ef89", "v": NetProtocol.VERSION},
		"welcome": {"t": NetProtocol.Msg.WELCOME, "v": NetProtocol.VERSION, "reconnected": true, "input_next": 57},
		"error": {"t": NetProtocol.Msg.ERROR, "text": NetProtocol.INCOMPATIBLE_TEXT},
		"room_closed": {"t": NetProtocol.Msg.ROOM_CLOSED},
		"discover": {"t": NetProtocol.Msg.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION},
		"said": {"t": NetProtocol.Msg.SAID, "p": 4, "s": 2},
	}
	var literal_hex: Dictionary = {}
	for key: String in literal:
		literal_hex[key] = _hex(literal[key])
	var to_dict: Array = []
	var inputs: Array[PlayerInput] = [
		PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0),
		PlayerInput.create(Vector2(0.3, -0.71234), Vector2.ZERO, 0, 7),
		PlayerInput.create(Vector2(2.0, 1.0), Vector2(0.0001, -0.9996), PlayerInput.BTN_CARD_1 | PlayerInput.BTN_PRIMARY, 2147483647),
		PlayerInput.create(Vector2(0.61999, 0.0), Vector2(-3.0, -4.0), 1 << 20, 3000000000),
		PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_JUMP, 9).with_move_target(Vector2(-12.345678, 99.995)),
		PlayerInput.create(Vector2(0.5, 0.5), Vector2.ZERO, PlayerInput.BTN_CARD_2, 10).with_aim_point(Vector2(0.004999, -0.005)),
		PlayerInput.create(Vector2(-0.0004, 0.0006), Vector2(0.7071, 0.7071), PlayerInput.BTN_SLIDE | PlayerInput.BTN_CROUCH, 11).with_move_target(Vector2(1, 2)).with_aim_point(Vector2(3, 4)),
	]
	for input: PlayerInput in inputs:
		to_dict.append({"in": _input_fields(input), "hex": _hex(NetProtocol.input_to_dict(input))})
	var from_dict: Array = []
	var dicts: Array[Dictionary] = [
		{"t": NetProtocol.Msg.INPUT, "k": 5, "m": Vector2(0.5, 0.5), "a": Vector2(0.0, -1.0), "b": 3},
		{"t": NetProtocol.Msg.INPUT},
		{"t": NetProtocol.Msg.INPUT, "k": 5.0, "b": 1.0, "m": "fast"},
		{"t": NetProtocol.Msg.INPUT, "k": 6, "b": PlayerInput.KNOWN_BITS | (1 << 4) | (1 << 30), "m": Vector2(3.0, 4.0), "a": Vector2(INF, 0.0)},
		{"t": NetProtocol.Msg.INPUT, "k": 7, "b": PlayerInput.BTN_MOVE_TARGET | PlayerInput.BTN_AIM_POINT, "mt": Vector2(NAN, 1.0), "ap": Vector2(1.0, 2.0)},
		{"t": NetProtocol.Msg.INPUT, "k": 8, "b": PlayerInput.BTN_MOVE_TARGET, "mt": Vector2(-500.0, 900.0)},
		{"t": NetProtocol.Msg.INPUT, "k": 9, "mt": Vector2(1.0, 1.0), "ap": Vector2i(1, 1)},
		{"t": NetProtocol.Msg.INPUT, "k": -4, "b": -1},
		{"t": NetProtocol.Msg.INPUT, "k": 5000000000, "m": Vector2(0.0, -2.0)},
	]
	for d: Dictionary in dicts:
		from_dict.append({"hex": _hex(d), "out": _input_fields(NetProtocol.input_from_dict(d))})
	var decode: Array = []
	var good: PackedByteArray = var_to_bytes({"t": 3, "x": [1, 2]})
	var payloads: Array[PackedByteArray] = [
		good,
		var_to_bytes([1, 2, 3]),
		var_to_bytes({"x": 1}),
		var_to_bytes({"t": 1.0}),
		var_to_bytes({"t": "1"}),
		good.slice(0, good.size() - 3),
		PackedByteArray([0xff, 0xff, 0xff, 0xff]),
		PackedByteArray(),
		PackedByteArray([0x18, 0, 0, 0, 0, 0, 0, 0]),
		var_to_bytes({"t": 9, "big": 5000000000, "neg": -7, "f": 0.1, "f32": 0.5, "nan": NAN, "s": "héllo ✓", "sn": &"sn", "v": Vector2(1.5, -2.25),
			"vi": Vector2i(-3, 4), "r": Rect2(1, 2, 3, 4), "v3": Vector3(1, 2, 3), "c": Color(0.1, 0.2, 0.3, 0.4),
			"pb": PackedByteArray([1, 2, 3]), "pi": PackedInt32Array([1, -2]), "pl": PackedInt64Array([5000000000]),
			"pf": PackedFloat32Array([0.5, 0.1]), "pd": PackedFloat64Array([0.1]), "ps": PackedStringArray(["a", "bé"]),
			"pv": PackedVector2Array([Vector2(1, 2)]), "nil": null, "yes": true, "no": false,
			"ta": [1, 2] as Array[int], "tn": [&"a"] as Array[StringName], "td": _typed_dict(), "nested": {1: [[{}], []], &"k": {"deep": [1.5]}}}),
	]
	for bytes: PackedByteArray in payloads:
		var message: Dictionary = NetProtocol.decode(bytes)
		decode.append({"hex": bytes.hex_encode(), "ok": not message.is_empty(), "again": _hex(message) if not message.is_empty() else ""})
	var snap: Array = []
	for pair: Array in [[0.0, 0.01], [0.005, 0.01], [-0.005, 0.01], [1.23456, 0.01], [-7.891, 0.001], [0.62, 0.001], [123456.785, 0.01], [0.015, 0.01], [2.675, 0.01]]:
		snap.append([pair[0], pair[1], snappedf(pair[0], pair[1])])
	_write(OUT + "net_protocol.json", {
		"version": NetProtocol.VERSION, "incompatible": NetProtocol.INCOMPATIBLE_TEXT, "msg": msg_names,
		"client_calls": calls, "client_sent": sent, "client_next_input_sequence": client.next_input_sequence,
		"literal": literal_hex, "input_to_dict": to_dict, "input_from_dict": from_dict, "decode": decode, "snapped": snap,
		"avatars": Session.AVATARS, "tiers": _tiers(),
	})


func _typed_dict() -> Dictionary[String, float]:
	var d: Dictionary[String, float] = {"a": 1.0, "b": 0.1}
	return d


func _tiers() -> Array:
	var out: Array = []
	for lp: int in [-5, 0, 199, 200, 399, 400, 599, 600, 849, 850, 1149, 1150, 1499, 1500, 99999]:
		out.append([lp, RankedSystem.tier_name(lp)])
	return out


static func _input_fields(input: PlayerInput) -> Array:
	return [input.move.x, input.move.y, input.aim.x, input.aim.y, input.buttons, input.tick,
		input.move_target.x, input.move_target.y, input.aim_point.x, input.aim_point.y]


# ---- rooms -----------------------------------------------------------------------------------------------------------------

## Each sequence is a list of ops run on one RoomService (see `_run_op`); the room of an op is its index in creation order.
func _sequences() -> Dictionary:
	var seqs: Dictionary = {}
	var basic: Array = [["service", 7, false, [3], 3], ["create", 10, "Host", 0], ["join_lower", 0, 11, "B"]]
	for peer: int in range(12, 20):
		basic.append(["join", 0, peer, "P%d" % peer])
	basic.append_array([
		["join", 0, 20, "K"], ["join_code", "ZZZZZZZZ", 21, "L"], ["join", 0, 11, "again"],
		["pick_team", 11, 0], ["pick_team", 10, 1], ["set_ready", 11, true], ["set_ready", 99, true], ["leave", 14],
		["set_character", 11, "toni"], ["pick_team", 11, 0], ["pick_team", 11, 0], ["pick_team", 11, 1], ["pick_team", 11, 2],
		["blocker", 0, false], ["blocker", 0, true], ["start", 11], ["start", 10],
	])
	for peer: int in [10, 11, 12, 13, 15, 16, 17, 18, 19]:
		basic.append(["set_ready", peer, true])
	basic.append_array([["blocker", 0, false], ["set_ready", 11, false], ["set_ready", 11, true], ["start", 11], ["start", 10], ["join", 0, 30, "late"],
		["set_ready", 10, false], ["pick_team", 10, 1], ["set_character", 10, "migo"], ["set_map", 10, "patintero"], ["blocker", 0, false]])
	seqs["basic"] = basic
	seqs["alone_and_one_team"] = [["service", 7, false, [3], 3], ["create", 1, "A", 0], ["set_ready", 1, true], ["blocker", 0, false],
		["join", 0, 2, "B"], ["set_ready", 2, true], ["blocker", 0, false], ["join", 0, 3, "C"], ["set_character", 2, "kiko"],
		["pick_team", 2, 0], ["set_ready", 2, true], ["set_ready", 3, true], ["blocker", 0, false], ["leave", 3], ["blocker", 0, false],
		["blocker", 0, true], ["set_ready", 1, false], ["blocker", 0, true], ["blocker", 0, false]]
	seqs["maps"] = [["service", 3, false, [3, 5, 7], 3], ["create", 1, "A", 0], ["join", 0, 2, "B"], ["set_map", 2, "street"],
		["set_map", 1, "nowhere"], ["set_map", 1, "tumbang_preso"], ["blocker", 0, true], ["join", 0, 3, "C"], ["blocker", 0, true],
		["set_ready", 2, true], ["set_ready", 3, true], ["blocker", 0, false], ["blocker", 0, true], ["set_map", 1, "patintero"], ["blocker", 0, true],
		["join", 0, 4, "D"], ["set_ready", 4, true], ["blocker", 0, true], ["set_character", 4, "nene"], ["pick_team", 4, 0], ["blocker", 0, true],
		["set_map", 1, "langit_lupa"], ["blocker", 0, true], ["set_map", 1, "street"], ["set_first_to", 2, 5], ["set_first_to", 1, 4],
		["set_first_to", 1, 7], ["set_open", 2, false], ["set_open", 1, false], ["set_fill_bots", 1, true], ["bot_teams", 0]]
	var ffa: Array = [["service", 11, false, [3], 3], ["create", 1, "A", 0], ["join", 0, 2, "B"], ["join", 0, 3, "C"], ["join", 0, 4, "D"],
		["leave", 1], ["create", 1, "A2", 0], ["leave", 3], ["set_map", 2, "tumbang_preso"], ["join", 0, 5, "E"], ["set_map", 1, "langit_lupa"]]
	seqs["free_for_all_places"] = ffa
	var bots: Array = [["service", 5, true, [3], 3], ["create", 1, "Host", 0], ["join", 0, 2, "Guest"], ["set_fill_bots", 2, true],
		["set_fill_bots", 1, true], ["bot_teams", 0], ["blocker", 0, true], ["set_ready", 2, true], ["blocker", 0, true],
		["set_map", 1, "tumbang_preso"], ["bot_teams", 0], ["blocker", 0, true], ["set_map", 1, "patintero"], ["bot_teams", 0],
		["set_map", 1, "langit_lupa"], ["bot_teams", 0], ["set_map", 1, "street"], ["set_fill_bots", 1, false], ["bot_teams", 0],
		["blocker", 0, true], ["set_fill_bots", 1, true], ["start", 1], ["set_fill_bots", 1, false]]
	for peer: int in range(3, 11):
		bots.insert(bots.size() - 2, ["join", 0, peer, "p%d" % peer])
	bots.insert(bots.size() - 2, ["bot_teams", 0])
	seqs["bots"] = bots
	seqs["bots_not_allowed"] = [["service", 5, false, [3], 3], ["create", 1, "Host", 0], ["set_fill_bots", 1, true], ["bot_teams", 0]]
	seqs["leave"] = [["service", 9, false, [3], 3], ["create", 1, "A", 0], ["join", 0, 2, "B"], ["join", 0, 3, "C"], ["disconnected", 1],
		["leave", 1], ["leave", 3], ["create", 3, "C2", 0], ["leave", 2], ["leave", 3], ["disconnected", 50], ["create", 4, "D", 0],
		["create", 4, "D", 0], ["join", 1, 5, "E"], ["leave", 4], ["leave", 5]]
	seqs["reconnect_afk"] = [["service", 13, false, [3], 3], ["create", 1, "A", 0], ["join", 0, 2, "B"], ["join", 0, 3, "C"],
		["set_ready", 2, true], ["set_ready", 3, true], ["set_ready", 1, true], ["start", 1], ["playing", 0], ["disconnected", 2], ["step", 29.0], ["step", 1.5],
		["reconnect_literal", "wrong", 9], ["reconnect", 0, 1, 9], ["reconnect", 0, 1, 10], ["disconnected", 9], ["step", 61.0],
		["reconnect", 0, 1, 11], ["disconnected", 3], ["step", 31.0], ["disconnected", 1], ["step", 10.0], ["step", 60.0], ["reconnect", 0, 0, 12]]
	seqs["loading_drop"] = [["service", 17, false, [3], 3], ["create", 1, "A", 0], ["join", 0, 2, "B"], ["set_ready", 2, true], ["start", 1],
		["set_ready", 1, true], ["start", 1], ["disconnected", 2], ["step", 40.0], ["reconnect", 0, 1, 7], ["reconnect_literal", "", 8], ["create", 7, "X", 0]]
	var open: Array = [["service", 21, false, [3], 3]]
	for i: int in 40:
		open.append(["create", 100 + i * 20, "Host%d" % i, 0])
		for j: int in (i * 7) % 10:
			open.append(["join", i, 100 + i * 20 + j + 1, "g"])
	open.append_array([["set_open", 120, false], ["set_ready", 140, true], ["set_ready", 141, true], ["start", 140], ["leave", 100],
		["open_rooms", 30], ["open_rooms", 5], ["open_rooms", 0], ["open_rooms", -1]])
	seqs["open_rooms"] = open
	var codes: Array = [["service", 7, false, [3], 3]]
	for peer: int in range(1, 60):
		codes.append(["create", peer, "p", 0])
	seqs["codes"] = codes
	seqs["names_ranks_chars"] = [["service", 2, false, [3], 3], ["create", 1, "   ", 0], ["join", 0, 2, "A very long name indeed", 450],
		["join", 0, 3, "\t Ñiño José ✓ long long \n", 1500], ["join", 0, 4, "ab", -30], ["set_rank", 4, 851], ["set_rank", 4, -1], ["set_rank", 77, 5],
		["set_character", 1, "nobody"], ["set_character", 1, "toni"], ["set_character", 3, "toni"], ["set_character", 2, "toni"],
		["set_character", 2, "junjun"], ["clean_name", "  x  "], ["clean_name", " nbsp "], ["clean_name", "1234567890123456"],
		["clean_name", "😀😀😀😀😀😀😀😀😀😀😀😀😀"], ["clean_name", ""]]
	for peer: int in range(5, 11):
		seqs["names_ranks_chars"].append(["join", 0, peer, "k%d" % peer])
	seqs["names_ranks_chars"].append_array([["set_ready", 1, true], ["set_ready", 2, true], ["set_ready", 5, true], ["start", 1], ["set_rank", 1, 99]])
	return seqs


func _export_rooms() -> void:
	var out: Dictionary = {}
	var seqs: Dictionary = _sequences()
	for name: String in seqs:
		var service: RoomService = null
		var created: Array[RoomService.Room] = []
		var steps: Array = []
		var ops: Array = seqs[name]
		for index: int in ops.size():
			var op: Array = ops[index]
			if op[0] == "service":
				service = RoomService.new(NET, op[1])
				service.bots_allowed = op[2]
				service.first_to_choices.assign(op[3])
				service.default_first_to = op[4]
				steps.append({"op": op})
				continue
			var result: Variant = _run_op(service, created, op)
			var step: Dictionary = {"op": op, "r": result}
			# the room state after every op (only at the end of the long sequences, to keep the file small)
			if service.rooms.size() <= 4 or index == ops.size() - 1:
				step["rooms"] = _room_state(service)
			steps.append(step)
		out[name] = steps
	_write(OUT + "net_rooms.json", out, "")


func _run_op(service: RoomService, created: Array[RoomService.Room], op: Array) -> Variant:
	match op[0]:
		"create":
			var room: RoomService.Room = service.create_room(op[1], op[2], op[3])
			if room == null:
				return null
			created.append(room)
			return room.code
		"join":
			return service.join(created[op[1]].code, op[2], op[3], op[4] if op.size() > 4 else 0)
		"join_lower":
			return service.join(created[op[1]].code.to_lower(), op[2], op[3])
		"join_code":
			return service.join(op[1], op[2], op[3])
		"pick_team":
			return service.pick_team(op[1], op[2])
		"set_ready":
			return service.set_ready(op[1], op[2])
		"set_first_to":
			return service.set_first_to(op[1], op[2])
		"set_map":
			return service.set_map(op[1], StringName(op[2]))
		"set_fill_bots":
			return service.set_fill_bots(op[1], op[2])
		"set_open":
			return service.set_open(op[1], op[2])
		"set_character":
			return service.set_character(op[1], StringName(op[2]))
		"set_rank":
			return service.set_rank(op[1], op[2])
		"start":
			return service.start(op[1])
		"playing":
			created[op[1]].state = RoomService.State.PLAYING
			return null
		"leave":
			var left: RoomService.Room = service.leave(op[1])
			return left.code if left != null else null
		"disconnected":
			var dropped: RoomService.Room = service.disconnected(op[1])
			return dropped.code if dropped != null else null
		"reconnect":
			var token: String = created[op[1]].members[op[2]].token
			var back: RoomService.Member = service.reconnect(token, op[3])
			return back.player_id if back != null else null
		"reconnect_literal":
			var again: RoomService.Member = service.reconnect(op[1], op[2])
			return again.player_id if again != null else null
		"step":
			var codes: Array = []
			for room: RoomService.Room in service.step(op[1]):
				codes.append(room.code)
			return codes
		"blocker":
			return service.start_blocker(created[op[1]], op[2])
		"bot_teams":
			return RoomService.bot_teams(created[op[1]])
		"open_rooms":
			return _hex(service.open_rooms(op[1]))
		"clean_name":
			return service.clean_name(op[1])
	push_error("unknown op %s" % op[0])
	return null


func _room_state(service: RoomService) -> Array:
	var rooms: Array = []
	for code: String in service.rooms:
		var room: RoomService.Room = service.rooms[code]
		var priv: Array = []
		for m: RoomService.Member in room.members:
			priv.append([m.player_id, m.peer_id, m.token, m.connected, m.reconnect_left, m.gone_for, m.afk])
		rooms.append({"hex": _hex(service.describe(room)), "priv": priv})
	return rooms


# ---- snapshots ---------------------------------------------------------------------------------------------------------------

## Close 5v5 combat driven by a formula of the tick only (the C# test computes the same inputs): stick, aim, cards held and
## released, primary, dash, jump, slide, crouch, ultimate, street points.
static func scripted_input(id: int, t: int) -> PlayerInput:
	var move: Vector2 = Vector2(float((t * 7 + id * 3) % 11 - 5) / 5.0, float((t * 5 + id) % 13 - 6) / 6.0)
	if (t / 30 + id) % 4 == 0:
		move = Vector2.ZERO
	var aim: Vector2 = Vector2(float(id % 3 - 1) * 0.3, -0.6 if id <= 5 else 0.6)
	var buttons: int = 0
	var phase_t: int = (t + id * 5) % 40
	if phase_t <= (id % 3) * 4:
		buttons |= PlayerInput.CARD_BITS[((t + id * 5) / 40 + id) % 2]
	if (t + id) % 9 == 0:
		buttons |= PlayerInput.BTN_PRIMARY
	if (t + id * 13) % 150 == 0:
		buttons |= PlayerInput.BTN_DASH
	if (t + id * 11) % 70 == 0:
		buttons |= PlayerInput.BTN_JUMP
	if (t + id * 17) % 110 == 0:
		buttons |= PlayerInput.BTN_SLIDE
	if (t + id * 19) % 130 < 10:
		buttons |= PlayerInput.BTN_CROUCH
	if (t + id * 3) % 60 == 0:
		buttons |= PlayerInput.BTN_ULTIMATE
	var input: PlayerInput = PlayerInput.create(move, aim, buttons, t)
	if (t + id) % 47 == 0:
		input.with_move_target(Vector2(float(id) - 5.5, float(t % 17) - 8.0))
	if id % 2 == 0 and phase_t == (id % 3) * 4 + 1:
		input.with_aim_point(Vector2(float(id) - 5.0, -6.0 if id <= 5 else 6.0))
	return input


func _export_snapshots() -> void:
	var scenarios: Array = []
	for config: Dictionary in [{"seed": 21, "ticks": 420, "every": 35}, {"seed": 4, "ticks": 300, "every": 60, "cards": true}]:
		var rules: GameRules = RULES.duplicate() as GameRules
		if config.get("cards", false):
			rules.card_cycle = true
			rules.elixir_enabled = true
			rules.stamina_enabled = true
			rules.hand_size = 4
		var sim: MatchSim = MatchSim.new(rules, LAYOUT, config["seed"])
		for id: int in range(1, 11):
			sim.add_player(id, 0 if id <= 5 else 1)
		sim.assign_characters()
		for id: int in range(1, 11):
			sim.equip(id, false)
			var row: int = (id - 1) % 5
			sim.players[id].position = Vector2(-8.0 + 4.0 * row, 6.0 if id <= 5 else -6.0)
		var mirror: MatchSim = MatchSim.new(rules, LAYOUT, config["seed"])
		for id: int in range(1, 11):
			mirror.add_player(id, 0 if id <= 5 else 1)
		var frames: Array = []
		var acks: Dictionary[int, int] = {}
		for t: int in config["ticks"]:
			for id: int in range(1, 11):
				sim.set_input(id, scripted_input(id, t))
				acks[id] = t
			sim.step(1.0 / 30.0)
			if t % int(config["every"]) == 0 or t == int(config["ticks"]) - 1:
				var public: Dictionary = Snapshot.capture(sim, acks)
				var message: Dictionary = public.duplicate()
				message.merge(Snapshot.private_for(sim, 3))
				Snapshot.apply(mirror, message, 3)
				var mirror_acks: Dictionary[int, int] = {}
				frames.append({"tick": sim.tick, "public": _hex(public), "private": _hex(Snapshot.private_for(sim, 3)),
					"mirror": _hex(Snapshot.capture(mirror, mirror_acks)), "mirror_private": _hex(Snapshot.private_for(mirror, 3)),
					"mirror_local_pos": [mirror.players[3].position.x, mirror.players[3].position.y],
					"mirror_powerups": _hex(mirror.powerups)})
		scenarios.append({"seed": config["seed"], "cards": config.get("cards", false), "ticks": config["ticks"], "frames": frames})
	_write(OUT + "net_snapshots.json", {"scenarios": scenarios}, "")


# ---- server sessions -----------------------------------------------------------------------------------------------------

var _steps: Array = []


func _msg(server: RecordingServer, from: int, message: Dictionary) -> void:
	_raw(server, from, var_to_bytes(message))


func _raw(server: RecordingServer, from: int, bytes: PackedByteArray) -> void:
	server.out.clear()
	var decoded: Dictionary = NetProtocol.decode(bytes)
	if not decoded.is_empty():
		server._handle(from, decoded)
	var step: Dictionary = {"from": from, "hex": bytes.hex_encode(), "sent": server.out.duplicate()}
	if server.out.is_empty():
		step.erase("sent")
	_steps.append(step)


func _poll(server: RecordingServer, delta: float) -> void:
	server.out.clear()
	server.fake_poll(delta)
	var step: Dictionary = {"poll": delta, "sent": server.out.duplicate()}
	if server.out.is_empty():
		step.erase("sent")
	_steps.append(step)


func _drop(server: RecordingServer, peer: int) -> void:
	server.out.clear()
	server._on_peer_disconnected(peer)
	_steps.append({"drop": peer, "sent": server.out.duplicate()})


## `ticks` of play: every peer sends its scripted input, then the server polls one tick.
func _play(server: RecordingServer, ids: Dictionary, ticks: int) -> void:
	var seq: int = 1
	for t: int in ticks:
		for peer: int in ids:
			var input: PlayerInput = scripted_input(ids[peer], t)
			input.tick = seq
			_msg(server, peer, NetProtocol.input_to_dict(input))
		seq += 1
		_poll(server, 1.0 / 30.0)


func _hello(name: String, token: String = "", version: int = NetProtocol.VERSION) -> Dictionary:
	return {"t": NetProtocol.Msg.HELLO, "name": name, "token": token, "v": version}


func _export_server() -> void:
	var M := NetProtocol.Msg
	var sessions: Dictionary = {}
	# --- online: the whole lobby, a match played, a drop and a reconnect
	_steps = []
	var server: RecordingServer = RecordingServer.new(RULES, LAYOUT, NET, 99)
	_msg(server, 909, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION})
	_msg(server, 909, _hello("Old", "", 2))
	_msg(server, 909, {"t": M.HELLO, "name": "NoVersion"})
	_raw(server, 909, PackedByteArray([1, 2, 3]))
	_msg(server, 101, _hello("Ana"))
	_msg(server, 202, _hello("  Ben  "))
	_msg(server, 303, _hello("Cyrus the long named"))
	_msg(server, 101, {"t": M.CREATE_ROOM})
	_msg(server, 101, {"t": M.CREATE_ROOM})
	var code: String = server.rooms.rooms.keys()[0]
	_msg(server, 202, {"t": M.JOIN_ROOM, "code": code.to_lower()})
	_msg(server, 303, {"t": M.JOIN_ROOM, "code": "NOPE2345"})
	_msg(server, 303, {"t": M.JOIN_ROOM})
	_msg(server, 303, {"t": M.LIST_ROOMS})
	_msg(server, 303, {"t": M.JOIN_ROOM, "code": code})
	_msg(server, 303, {"t": M.LIST_ROOMS})
	_poll(server, 0.5)
	_msg(server, 303, {"t": M.LIST_ROOMS})
	_poll(server, 0.6)
	_msg(server, 303, {"t": M.LIST_ROOMS})
	_msg(server, 202, {"t": M.SET_OPEN, "open": false})
	_msg(server, 101, {"t": M.SET_OPEN, "open": 0})
	_msg(server, 101, {"t": M.SET_OPEN, "open": false})
	_msg(server, 202, {"t": M.SET_MAP, "map": "patintero"})
	_msg(server, 101, {"t": M.SET_MAP, "map": "patintero"})
	_msg(server, 101, {"t": M.SET_MAP, "map": &"street"})
	_msg(server, 202, {"t": M.SET_CHARACTER, "c": "toni"})
	_msg(server, 303, {"t": M.SET_CHARACTER, "c": "nobody"})
	_msg(server, 303, {"t": M.PICK_TEAM, "team": 0})
	_msg(server, 303, {"t": M.PICK_TEAM, "team": 1})
	_msg(server, 101, {"t": M.SET_FIRST_TO, "n": 5})
	_msg(server, 101, {"t": M.SET_FIRST_TO, "n": 4})
	_msg(server, 101, {"t": M.SET_FILL_BOTS, "on": true})
	_msg(server, 202, {"t": M.START})
	_msg(server, 101, {"t": M.START})
	_msg(server, 202, {"t": M.SET_READY, "ready": true})
	_msg(server, 303, {"t": M.SET_READY, "ready": true})
	_msg(server, 303, {"t": M.LEAVE})
	_msg(server, 101, {"t": M.LOADED})
	_msg(server, 101, {"t": M.START})
	_msg(server, 101, {"t": M.SET_READY, "ready": true})
	_msg(server, 101, {"t": M.SET_TEAM_SIZE, "n": 2})
	_msg(server, 101, {"t": 999})
	_msg(server, 101, {"t": M.START})
	_poll(server, 1.0 / 30.0)
	_msg(server, 101, {"t": M.LOADED})
	_poll(server, 1.0 / 30.0)
	_msg(server, 202, {"t": M.LOADED})
	_poll(server, 1.0 / 30.0)
	_msg(server, 101, {"t": M.PICK_WEAPONS, "a": &"bato_light", "b": &"trumpo", "c": &"migo"})
	_msg(server, 202, {"t": M.SAY, "s": 1})
	_poll(server, 1.0 / 30.0)
	_msg(server, 202, {"t": M.PICK_WEAPONS, "a": &"jacks", "b": &"bola", "c": &""})
	_poll(server, 1.0 / 30.0)
	var peers: Dictionary = {101: server.rooms.member_of_peer(101).player_id, 202: server.rooms.member_of_peer(202).player_id}
	var seq: Dictionary = {101: 1, 202: 1}
	for t: int in 200:
		if t == 90:
			_drop(server, 202)
		if t == 120:
			var token: String = server.rooms.rooms[code].members[1].token
			_msg(server, 505, _hello("Ben", token))
			peers[505] = peers[202]
			peers.erase(202)
			seq[505] = 1000
		for peer: int in peers:
			var input: PlayerInput = scripted_input(peers[peer], t)
			input.tick = seq[peer]
			seq[peer] += 1
			_msg(server, peer, NetProtocol.input_to_dict(input))
		if t == 30:
			_msg(server, 101, {"t": M.SAY, "s": 2})
			_msg(server, 101, {"t": M.SAY, "s": 3})
			_msg(server, 101, {"t": M.SAY, "s": 6})
			_msg(server, 101, {"t": M.SAY, "s": 1.0})
		if t == 70:
			_msg(server, 101, {"t": M.SWAP_WEAPONS, "a": &"lata", "b": &"heal"})
			_msg(server, 101, {"t": M.PICK_WEAPONS, "a": &"jacks", "b": &"bola"})
		if t == 150:
			_msg(server, 101, {"t": M.INPUT, "k": 3, "m": Vector2(1, 0)})
		_poll(server, 1.0 / 30.0)
	_msg(server, 909, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION})
	_drop(server, 101)
	_drop(server, 505)
	_poll(server, 1.0)
	sessions["online"] = {"seed": 99, "auto_join": false, "steps": _steps}
	# --- a hotspot host: discovery, a join without a code, fill with bots, the bots in the match's loading message
	_steps = []
	var host: RecordingServer = RecordingServer.new(RULES, LAYOUT, NET, 4242)
	host.hashed = true
	host.auto_join = true
	_msg(host, 7, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION})
	_msg(host, 7, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": 2})
	_msg(host, 1, _hello("Host"))
	_msg(host, 1, {"t": M.CREATE_ROOM})
	_msg(host, 7, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION})
	_msg(host, 7, {"t": M.DISCOVER_ROOM, "game": "other", "v": NetProtocol.VERSION})
	_msg(host, 2, _hello("Guest"))
	_msg(host, 2, {"t": M.JOIN_ROOM})
	_msg(host, 2, {"t": M.SET_FILL_BOTS, "on": true})
	_msg(host, 1, {"t": M.SET_FILL_BOTS, "on": "yes"})
	_msg(host, 1, {"t": M.SET_FILL_BOTS, "on": true})
	_msg(host, 2, {"t": M.SET_READY, "ready": true})
	_msg(host, 1, {"t": M.SET_READY, "ready": true})
	_msg(host, 1, {"t": M.START})
	_msg(host, 7, {"t": M.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION})
	_msg(host, 1, {"t": M.LOADED})
	_msg(host, 2, {"t": M.LOADED})
	_poll(host, 1.0 / 30.0)
	_msg(host, 1, {"t": M.PICK_WEAPONS, "a": &"bato_light", "b": &"trumpo", "c": &""})
	_msg(host, 2, {"t": M.PICK_WEAPONS, "a": &"jacks", "b": &"bola", "c": &"tala"})
	_play(host, {1: 1, 2: 2}, 240)
	sessions["hotspot_bots"] = {"seed": 4242, "auto_join": true, "hashed": true, "steps": _steps}
	# --- the street games: Patintero with four people, Tumbang Preso and Langit Lupa filled with bots
	for game: Array in [["patintero", 4, false, 300, 77], ["tumbang_preso", 2, true, 300, 78], ["langit_lupa", 3, true, 300, 79]]:
		_steps = []
		var street: RecordingServer = RecordingServer.new(RULES, LAYOUT, NET, game[4])
		street.hashed = true
		street.auto_join = game[2]
		var ids: Dictionary = {}
		for peer: int in range(1, int(game[1]) + 1):
			_msg(street, peer, _hello("Kid%d" % peer))
			_msg(street, peer, {"t": M.CREATE_ROOM} if peer == 1 else {"t": M.JOIN_ROOM, "code": street.rooms.rooms.keys()[0]})
		_msg(street, 1, {"t": M.SET_MAP, "map": game[0]})
		if game[2]:
			_msg(street, 1, {"t": M.SET_FILL_BOTS, "on": true})
		for peer: int in range(1, int(game[1]) + 1):
			_msg(street, peer, {"t": M.SET_READY, "ready": true})
		_msg(street, 1, {"t": M.START})
		for peer: int in range(1, int(game[1]) + 1):
			_msg(street, peer, {"t": M.LOADED})
			ids[peer] = street.rooms.member_of_peer(peer).player_id
		_poll(street, 1.0 / 30.0)
		_play(street, ids, game[3])
		sessions[game[0]] = {"seed": game[4], "auto_join": game[2], "hashed": true, "steps": _steps}
	_write(OUT + "net_server.json", sessions, "")
