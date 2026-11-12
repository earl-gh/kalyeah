class_name GameClient
extends RefCounted
## Client side of the network game. Talks to the server over WebSocket, keeps the
## room state, and during a match keeps a mirror MatchSim for the views:
## the local player's movement is predicted and reconciled with the server, other
## players are drawn `interpolation_delay` in the past between two snapshots.
## Drive it with poll(delta); the Net autoload does that every frame.

signal connection_changed(online: bool)
## A connection that never opened (no server there, or it did not wake within `NetRules.connect_timeout`).
signal connect_failed
## The server answered HELLO; `reconnected` = we got our match slot back.
signal welcomed(reconnected: bool)
signal error_received(text: String)
signal room_changed(room: Dictionary)
## The server closed the room for good (a hotspot host left); a reconnect would find nothing.
signal room_closed
## The host started: build the match scene from `info`, then call loaded().
signal match_loading(info: Dictionary)
## "pick" (10 s weapon pick) or "play" (loadouts are final).
signal stage_changed(stage: String)
## Someone confirmed their pick: the kids a team has locked in changed (during the pick).
signal picks_changed
signal snapshot_applied
## Someone in the match (you too) sent a quick chat sticker: their player id and the sticker's number.
signal said(player_id: int, sticker: int)
## The open Online rooms the server listed (`list_rooms`): each {code, host_name, team_size, players, first_to, map}.
signal rooms_listed(rooms: Array[Dictionary])

var player_name: String = ""
## Stable id in the room and the match (-1 before joining a room).
var player_id: int = -1
## Secret to take the slot back after a dropped connection (kept by Session).
var token: String = ""
var room: Dictionary = {}
var match_info: Dictionary = {}
var stage: String = ""
var stage_time: float = 0.0
## The client's copy of the match, read by the views.
var mirror: MatchSim
## The next wire sequence accepted by the authoritative match. Game input may
## restart its own counter after a page reload; send_input maps it forward.
var next_input_sequence: int = 1

var _rules: GameRules
var _layout: MapLayout
var _net: NetRules
var _peer: WebSocketMultiplayerPeer
var _was_online: bool = false
var _pending: Array[PlayerInput] = []
var _clock: float = 0.0
var _connect_started: float = 0.0
## Snapshots of other players' positions and jump heights: {"time": float, "pos": {id: Vector2}, "air": {id: float}}.
var _buffer: Array[Dictionary] = []


func _init(rules: GameRules, layout: MapLayout, net: NetRules) -> void:
	_rules = rules
	_layout = layout
	_net = net


## `patient`: an Online server, which may first have to wake up, gets `NetRules.connect_timeout` to answer (Hotspot: Godot's 3 s).
func connect_to(url: String, patient: bool = false) -> Error:
	close()
	_peer = WebSocketMultiplayerPeer.new()
	if patient:
		_peer.handshake_timeout = _net.connect_timeout
	_connect_started = _clock
	return _peer.create_client(url)


## Seconds this connection has been trying to open (0 once it is open or when there is none).
func connecting_for() -> float:
	return _clock - _connect_started if is_connecting() else 0.0


## Still opening after `NetRules.wake_hint_after`: an Online server that was asleep is waking up.
func waking() -> bool:
	return connecting_for() >= _net.wake_hint_after


func close() -> void:
	if _peer != null:
		_peer.close()
		_peer = null
	if _was_online:
		_was_online = false
		connection_changed.emit(false)


func is_online() -> bool:
	return _peer != null and _peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED


func is_connecting() -> bool:
	return _peer != null and _peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTING


func in_match() -> bool:
	return mirror != null


func poll(delta: float) -> void:
	_clock += delta
	if stage_time > 0.0:
		stage_time = maxf(0.0, stage_time - delta)
	if _peer == null:
		return
	_peer.poll()
	var online: bool = is_online()
	if online != _was_online:
		_was_online = online
		connection_changed.emit(online)
		if online:
			send({"t": NetProtocol.Msg.HELLO, "name": player_name, "token": token, "v": NetProtocol.VERSION})
	if _peer == null:
		return
	if _peer.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
		var never_opened: bool = not _was_online
		close()
		if never_opened:
			connect_failed.emit()
		return
	while _peer != null and _peer.get_available_packet_count() > 0:
		var message: Dictionary = NetProtocol.decode(_peer.get_packet())
		if not message.is_empty():
			_handle(message)


func send(message: Dictionary) -> void:
	if not is_online():
		return
	_peer.set_target_peer(MultiplayerPeer.TARGET_PEER_SERVER)
	_peer.put_packet(NetProtocol.encode(message))


# ---- lobby -----------------------------------------------------------------------

func create_room() -> void:
	send({"t": NetProtocol.Msg.CREATE_ROOM})


func join_room(code: String) -> void:
	send({"t": NetProtocol.Msg.JOIN_ROOM, "code": code})


## Asks for the open rooms; the answer comes as `rooms_listed`.
func list_rooms() -> void:
	send({"t": NetProtocol.Msg.LIST_ROOMS})


## Host only: open (listed for everyone) or private (code only).
func set_open(open: bool) -> void:
	send({"t": NetProtocol.Msg.SET_OPEN, "open": open})


## Host only, Local rooms: the places nobody took are played by bots.
func set_fill_bots(on: bool) -> void:
	send({"t": NetProtocol.Msg.SET_FILL_BOTS, "on": on})


func pick_team(team: int) -> void:
	send({"t": NetProtocol.Msg.PICK_TEAM, "team": team})


## Host only: the match is won by the first team to `count` points (3, 5 or 7).
func set_first_to(count: int) -> void:
	send({"t": NetProtocol.Msg.SET_FIRST_TO, "n": count})


## Host only: the map to play on.
func set_map(map_id: StringName) -> void:
	send({"t": NetProtocol.Msg.SET_MAP, "map": String(map_id)})


## The kid you will play, shown to the others in the room.
func set_character(character: StringName) -> void:
	send({"t": NetProtocol.Msg.SET_CHARACTER, "c": String(character)})


func set_ready(ready: bool) -> void:
	send({"t": NetProtocol.Msg.SET_READY, "ready": ready})


func start_match() -> void:
	send({"t": NetProtocol.Msg.START})


func leave_room() -> void:
	send({"t": NetProtocol.Msg.LEAVE})
	room = {}
	player_id = -1
	token = ""
	mirror = null
	stage = ""


func is_host() -> bool:
	return not room.is_empty() and _int_in(room, "host", -1) == player_id


func my_team() -> int:
	for raw_member: Variant in _arr_in(room, "members"):
		if raw_member is Dictionary and _int_in(raw_member, "player_id", -2) == player_id:
			return _int_in(raw_member, "team", -1)
	return -1


# ---- match -------------------------------------------------------------------------

func loaded() -> void:
	send({"t": NetProtocol.Msg.LOADED})


## The players who confirmed their pick (from the server).
var locked_players: Array[int] = []


## The kids a confirmed teammate plays: this player cannot take them.
func taken_kids() -> Array[StringName]:
	return mirror.teammates_kids(player_id, locked_players) if mirror != null else []


func pick_weapons(first: StringName, second: StringName, character: StringName = &"", third: StringName = &"") -> void:
	var msg: Dictionary = {"t": NetProtocol.Msg.PICK_WEAPONS, "a": first, "b": second, "c": character}
	if third != &"":
		msg["d"] = third
	send(msg)


func swap_weapons(first: StringName, second: StringName, third: StringName = &"") -> void:
	var msg: Dictionary = {"t": NetProtocol.Msg.SWAP_WEAPONS, "a": first, "b": second}
	if third != &"":
		msg["d"] = third
	send(msg)


## Sends a quick chat sticker (its number in `QuickChat.PHRASES`) to everyone in the match: it comes back as `said`, for you too.
func say(sticker: int) -> void:
	if stage == "play":
		send({"t": NetProtocol.Msg.SAY, "s": sticker})


## Sends this tick's input and moves the local player right away (prediction).
func send_input(input: PlayerInput, dt: float) -> void:
	if mirror == null or not is_online():
		return
	var wire_tick: int = maxi(input.tick, next_input_sequence)
	var wire_input: PlayerInput = PlayerInput.create(input.move, input.aim, input.buttons, wire_tick)
	# the tapped street point and the point under a dragged card ride along (the buttons already carry their bits)
	wire_input.move_target = input.move_target
	wire_input.aim_point = input.aim_point
	send(NetProtocol.input_to_dict(wire_input))
	next_input_sequence = wire_tick + 1
	if stage != "play":
		return
	_pending.append(wire_input)
	mirror.predict_move(player_id, wire_input, dt)


## Places the other players between the two snapshots around (now - delay), interpolating XZ position and jump `air_y`.
func interpolate() -> void:
	if mirror == null or _buffer.is_empty():
		return
	var render_time: float = _clock - _net.interpolation_delay
	var older: Dictionary = _buffer[0]
	var newer: Dictionary = _buffer[0]
	for entry: Dictionary in _buffer:
		if _float_in(entry, "time", 0.0) <= render_time:
			older = entry
			newer = entry
		else:
			newer = entry
			break
	var older_time: float = _float_in(older, "time", 0.0)
	var newer_time: float = _float_in(newer, "time", 0.0)
	var span: float = newer_time - older_time
	var weight: float = 0.0 if span <= 0.0 else clampf((render_time - older_time) / span, 0.0, 1.0)
	var from: Dictionary = _dict_in(older, "pos")
	var to: Dictionary = _dict_in(newer, "pos")
	var from_air: Dictionary = _dict_in(older, "air")
	var to_air: Dictionary = _dict_in(newer, "air")
	for id: int in mirror.players:
		if id == player_id or not from.has(id) or not to.has(id):
			continue
		var remote: PlayerState = mirror.players[id]
		remote.position = _vec2_val(from[id]).lerp(_vec2_val(to[id]), weight)
		if from_air.has(id) and to_air.has(id):
			remote.set(&"air_y", lerpf(_float_val(from_air[id]), _float_val(to_air[id]), weight))


func _handle(message: Dictionary) -> void:
	var msg_type: int = _int_in(message, "t", -1)
	match msg_type:
		NetProtocol.Msg.WELCOME:
			if _int_in(message, "v", -1) != NetProtocol.VERSION:
				error_received.emit(NetProtocol.INCOMPATIBLE_TEXT)
				return
			var reconnected: bool = _bool_in(message, "reconnected", false)
			if message.has("input_next"):
				next_input_sequence = maxi(next_input_sequence, _int_in(message, "input_next", next_input_sequence))
			elif not reconnected:
				next_input_sequence = 1
			welcomed.emit(reconnected)
		NetProtocol.Msg.ERROR:
			error_received.emit(_str_in(message, "text", ""))
		NetProtocol.Msg.ROOM:
			room = message
			player_id = _int_in(message, "you", -1)
			token = _str_in(message, "token", "")
			room_changed.emit(room)
		NetProtocol.Msg.ROOM_CLOSED:
			room_closed.emit()
		NetProtocol.Msg.ROOM_LIST:
			var listed: Array[Dictionary] = []
			for entry: Variant in _arr_in(message, "rooms"):
				if entry is Dictionary and entry.get("code") is String:
					listed.append(entry)
			rooms_listed.emit(listed)
		NetProtocol.Msg.MATCH_LOADING:
			_build_mirror(message)
			match_loading.emit(message)
		NetProtocol.Msg.MATCH_PLAY:
			_on_stage(message)
		NetProtocol.Msg.SNAPSHOT:
			_on_snapshot(message)
		NetProtocol.Msg.EVENTS:
			_on_events(_arr_in(message, "list"))
		NetProtocol.Msg.SAID:
			var sender: Variant = message.get("p")
			var sticker: Variant = message.get("s")
			if sender is int and sticker is int and sticker >= 0 and sticker < _net.chat_stickers:
				said.emit(sender, sticker)


func _build_mirror(info: Dictionary) -> void:
	if mirror != null and _int_in(match_info, "seed", -1) == _int_in(info, "seed", -2):
		# reconnected into the same match: keep the mirror the views already use
		_pending.clear()
		_buffer.clear()
		return
	match_info = info
	mirror = MatchSim.new(_rules, _layout, _int_in(info, "seed", 0))
	for raw_entry: Variant in _arr_in(info, "players"):
		if not (raw_entry is Dictionary):
			continue
		var state: PlayerState = mirror.add_player(_int_in(raw_entry, "id", 0), _int_in(raw_entry, "team", 0))
		state.character_id = _sname_in(raw_entry, "char", &"")
	# a street game: the same game on the copy, so the views show it and the local kid's walk is predicted by its rules
	var game: StreetModeRules = StreetMode.rules_for(StringName(_str_in(info, "game", "")))
	if game != null:
		var mode: StreetMode = StreetMode.create(game)
		var raw_runs: Variant = info.get("runs", 0)
		mode.attack_team = clampi(raw_runs if raw_runs is int else 0, 0, 1)
		mirror.start_mode(mode)
	_pending.clear()
	_buffer.clear()
	stage = "loading"


func _on_stage(message: Dictionary) -> void:
	stage = _str_in(message, "stage", "")
	stage_time = _float_in(message, "time", 0.0)
	if mirror != null:
		var loadouts: Dictionary = _dict_in(message, "loadouts")
		for raw_id: Variant in loadouts:
			if not (raw_id is int) or not mirror.players.has(raw_id):
				continue
			var pid: int = raw_id
			var raw_pair: Variant = loadouts[raw_id]
			if not (raw_pair is Array):
				continue
			var pair: Array = raw_pair
			if pair.is_empty() or pair.size() > PlayerInput.CARD_BITS.size():
				continue
			var typed_names: Array[StringName] = []
			var names_ok: bool = true
			for item: Variant in pair:
				if item is StringName:
					typed_names.append(item)
				elif item is String:
					typed_names.append(StringName(item))
				else:
					names_ok = false
					break
			if not names_ok:
				continue
			if mirror.rules.card_cycle:
				# a hand of `hand_size` cards (an empty slot while the old shuffler works); the server dealt it, so take it as it is
				var player: PlayerState = mirror.players[pid]
				player.weapons.assign(typed_names)
				player.cards = true
				player.resize_slots()
			elif typed_names.size() >= 2:
				mirror.set_loadout_list(pid, typed_names)
		# the server settled who plays which kid (a pick may have swapped two players' kids)
		var kids: Dictionary = _dict_in(message, "chars")
		for raw_id: Variant in kids:
			if raw_id is int and mirror.players.has(raw_id):
				mirror.players[raw_id].character_id = _sname_val(kids[raw_id])
		locked_players.clear()
		for raw_id: Variant in _arr_in(message, "locked"):
			if raw_id is int:
				locked_players.append(raw_id)
	stage_changed.emit(stage)
	if stage == "pick":
		picks_changed.emit()


func _on_snapshot(data: Dictionary) -> void:
	if mirror == null:
		return
	Snapshot.apply(mirror, data, player_id)
	var positions: Dictionary = {}
	var airs: Dictionary = {}
	var server_position: Vector2 = Vector2.ZERO
	var have_local: bool = false
	for raw_entry: Variant in _arr_in(data, "players"):
		if not (raw_entry is Dictionary):
			continue
		var id: int = _int_in(raw_entry, "id", -1)
		if id < 0 or _bool_in(raw_entry, "hid", false):
			continue  # out of this phone's sight (a brownout): nothing to draw it toward
		var pos: Vector2 = _vec2_in(raw_entry, "pos", Vector2.ZERO)
		positions[id] = pos
		airs[id] = _float_in(raw_entry, "ay", 0.0)
		if id == player_id:
			server_position = pos
			have_local = true
	_buffer.append({"time": _clock, "pos": positions, "air": airs})
	while _buffer.size() > 4:
		_buffer.pop_front()
	if have_local:
		var acks: Dictionary = _dict_in(data, "acks")
		var ack: int = _int_in(acks, player_id, -1)
		next_input_sequence = maxi(next_input_sequence, ack + 1)
		_reconcile(server_position, ack)
	snapshot_applied.emit()


## Server position as of the last input it used, then the newer inputs replayed.
func _reconcile(server_position: Vector2, ack: int) -> void:
	if not mirror.players.has(player_id):
		return
	var local: PlayerState = mirror.players[player_id]
	while not _pending.is_empty() and _pending[0].tick <= ack:
		_pending.pop_front()
	local.position = server_position
	var dt: float = _rules.tick_dt()
	for input: PlayerInput in _pending:
		mirror.predict_move(player_id, input, dt)


func _on_events(list: Array) -> void:
	if mirror == null:
		return
	var targets: Dictionary[String, Object] = {
		"sim": mirror, "weapons": mirror.weapons,
	}
	if mirror.mode != null:
		targets["mode"] = mirror.mode
	for raw_event: Variant in list:
		if not (raw_event is Array):
			continue
		var event: Array = raw_event
		if event.size() != 3 or not (event[0] is String) or not (event[1] is String or event[1] is StringName) or not (event[2] is Array):
			continue
		var target_key: String = event[0]
		if not targets.has(target_key):
			continue
		var target_obj: Object = targets[target_key]
		var sig_str: String = String(event[1])
		var sig_name: StringName = StringName(sig_str)
		if not target_obj.has_signal(sig_name):
			continue
		var raw_args: Array = event[2]
		var args: Array = raw_args.duplicate()
		if (sig_str == "cone_struck" or sig_str == "cone_winding") and args.size() == 4:
			var wid: StringName = _sname_val(args[1])
			var def: WeaponDef = mirror.weapon_defs[wid] if mirror.weapon_defs.has(wid) else null
			if def == null:
				continue
			args[1] = def
		var call_args: Array = [sig_name]
		call_args.append_array(args)
		target_obj.callv("emit_signal", call_args)


static func _int_in(d: Dictionary, key: Variant, fallback: int = 0) -> int:
	var v: Variant = d.get(key, fallback)
	if v is int:
		return v
	if v is float:
		return int(v)
	return fallback


static func _float_in(d: Dictionary, key: Variant, fallback: float = 0.0) -> float:
	var v: Variant = d.get(key, fallback)
	if v is float:
		return v
	if v is int:
		return float(v)
	return fallback


static func _bool_in(d: Dictionary, key: Variant, fallback: bool = false) -> bool:
	var v: Variant = d.get(key, fallback)
	if v is bool:
		return v
	return fallback


static func _str_in(d: Dictionary, key: Variant, fallback: String = "") -> String:
	var v: Variant = d.get(key, fallback)
	if v is String:
		return v
	if v is StringName:
		return String(v)
	return fallback


static func _sname_in(d: Dictionary, key: Variant, fallback: StringName = &"") -> StringName:
	var v: Variant = d.get(key, fallback)
	return _sname_val(v, fallback)


static func _sname_val(v: Variant, fallback: StringName = &"") -> StringName:
	if v is StringName:
		return v
	if v is String:
		return StringName(v)
	return fallback


static func _vec2_in(d: Dictionary, key: Variant, fallback: Vector2 = Vector2.ZERO) -> Vector2:
	var v: Variant = d.get(key, fallback)
	if v is Vector2:
		return v
	return fallback


static func _vec2_val(v: Variant) -> Vector2:
	if v is Vector2:
		return v
	return Vector2.ZERO


static func _float_val(v: Variant) -> float:
	if v is float:
		return v
	if v is int:
		return float(v)
	return 0.0


static func _dict_in(d: Dictionary, key: Variant) -> Dictionary:
	var v: Variant = d.get(key)
	if v is Dictionary:
		return v
	return {}


static func _arr_in(d: Dictionary, key: Variant) -> Array:
	var v: Variant = d.get(key)
	if v is Array:
		return v
	return []
