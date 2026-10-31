class_name ServerMatch
extends RefCounted
## One running match on the server: loading sync, the 10 s weapon pick, then the
## authoritative MatchSim stepped at 30 Hz from queued client inputs, with
## snapshots at `snapshot_rate` and the sim's signals relayed as EVENTS.
## A room on a street game (Patintero; `RoomService.PLAYABLE_MAP_IDS`) plays that game instead: no cards and no weapon pick (the kid
## was chosen in the room), a random team runs first, and every signal of the game is relayed too ("mode").

enum Stage { LOADING, PICKING, PLAYING }

## Sim signals relayed to clients ("weapons" or "sim" + name).
## Side-switch signals are not relayed: clients derive them from snapshots.
const RELAYED: Dictionary[String, Array] = {
	"sim": [
		"player_tagged", "player_rescued",
		"point_scored", "match_won", "point_reset", "weapon_cast", "player_damaged", "player_healed", "effect_applied",
		"exhausted",
	],
	"weapons": ["cone_struck", "cone_winding"],
}

var room: RoomService.Room
var sim: MatchSim
var stage: Stage = Stage.LOADING
var stage_left: float = 0.0
var seed_value: int = 0

var _server: GameServer
var _net: NetRules
var _loaded: Dictionary[int, bool] = {}
var _picked: Dictionary[int, bool] = {}
var _queues: Dictionary[int, Array] = {}
var _last_input: Dictionary[int, PlayerInput] = {}
var _acks: Dictionary[int, int] = {}
var _events: Array[Array] = []
var _accumulator: float = 0.0
var _snapshot_accumulator: float = 0.0


func _init(server: GameServer, p_room: RoomService.Room, rules: GameRules, layout: MapLayout, net: NetRules, p_seed: int) -> void:
	_server = server
	room = p_room
	_net = net
	seed_value = p_seed
	sim = MatchSim.new(rules, layout, p_seed)
	sim.set_first_to(room.first_to)
	for m: RoomService.Member in room.members:
		sim.add_player(m.player_id, m.team)
		_queues[m.player_id] = []
		_acks[m.player_id] = -1
	sim.assign_characters()
	# Apply explicit lobby choices after seeded defaults. set_character swaps a teammate's
	# random default away, so a later unpicked member cannot displace the chosen kid.
	for m: RoomService.Member in room.members:
		if m.character != &"":
			sim.set_character(m.player_id, m.character)
	var game: StreetModeRules = StreetMode.rules_for(room.map_id)
	if game != null:
		var mode: StreetMode = StreetMode.create(game)
		mode.attack_team = sim.rng.randi_range(0, 1)  # owner: in a room, a random team runs first
		sim.start_mode(mode)  # no skills in these games: nobody is dealt a hand
	else:
		for id: int in sim.players:
			sim.equip(id)
	stage_left = net.load_timeout
	_relay_signals()


## Breaks the sim -> match signal links and the server back-reference so the
## match can be freed (RefCounted cycles would leak).
func dispose() -> void:
	var sources: Array[Object] = [sim, sim.weapons]
	if sim.mode != null:
		sources.append(sim.mode)
	for source: Object in sources:
		for info: Dictionary in source.get_signal_list():
			for connection: Dictionary in source.get_signal_connection_list(info["name"] as StringName):
				var callable: Callable = connection["callable"] as Callable
				if callable.get_object() == self:
					source.disconnect(info["name"] as StringName, callable)
	_server = null


## MATCH_LOADING: everything a client needs to build its mirror sim.
func info() -> Dictionary:
	var players: Array[Dictionary] = []
	for m: RoomService.Member in room.members:
		players.append({
			"id": m.player_id, "team": m.team, "name": m.name,
			"char": sim.players[m.player_id].character_id,
		})
	return {"t": NetProtocol.Msg.MATCH_LOADING, "seed": seed_value, "team_size": room.team_size, "players": players, "game": String(room.map_id),
		"runs": sim.mode.attack_team if sim.mode != null else 0}


## MATCH_PLAY: the current stage (pick or play) for a client joining or catching up.
func stage_message() -> Dictionary:
	var loadouts: Dictionary = {}
	var chars: Dictionary = {}
	var locked: Array[int] = []
	for id: int in _picked:
		locked.append(id)
	for id: int in sim.players:
		loadouts[id] = sim.players[id].weapons.duplicate()
		chars[id] = sim.players[id].character_id
	var name: String = "play" if stage == Stage.PLAYING else "pick"
	return {"t": NetProtocol.Msg.MATCH_PLAY, "stage": name, "time": stage_left, "loadouts": loadouts, "chars": chars, "locked": locked}


func loaded(player_id: int) -> void:
	_loaded[player_id] = true


## First sequence number a reconnected client can safely send. Inputs received
## just before its socket dropped may still be queued, so include those too.
func next_input_sequence(player_id: int) -> int:
	var last: int = _acks.get(player_id, -1) as int
	for queued: PlayerInput in _queues.get(player_id, []) as Array:
		last = maxi(last, queued.tick)
	return last + 1


## Picks are taken during loading too (a fast client may pick before a slow one loads).
## `character` is the kid the player chose (empty keeps the random one). A kid a teammate has
## already confirmed is not taken from them (the other team may have the same kid).
func pick(player_id: int, first: StringName, second: StringName, character: StringName = &"") -> void:
	if stage == Stage.PLAYING:
		return  # the pick is over: a late or forged PICK must not change weapons or clear cooldowns
	# cards mode: nobody picks weapons, the hand was dealt at the start; only the kid comes with the confirmation
	var accepted: bool = sim.rules.card_cycle or sim.set_loadout(player_id, first, second)
	if accepted:
		_picked[player_id] = true
		if character != &"" and not taken_kids(player_id).has(character):
			sim.set_character(player_id, character)
		# everyone learns who is locked in, so their select screens grey those kids out
		_broadcast(stage_message())


## The kids `player_id` cannot take: the ones its confirmed teammates play.
func taken_kids(player_id: int) -> Array[StringName]:
	var confirmed: Array[int] = []
	for id: int in _picked:
		confirmed.append(id)
	return sim.teammates_kids(player_id, confirmed)


func swap(player_id: int, first: StringName, second: StringName) -> void:
	if stage == Stage.PLAYING:
		sim.swap_loadout(player_id, first, second)


func queue_input(player_id: int, input: PlayerInput) -> void:
	if not _queues.has(player_id) or input.tick <= _acks[player_id]:
		return
	var queue: Array = _queues[player_id]
	queue.append(input)
	while queue.size() > _net.max_queued_inputs:
		queue.pop_front()


func poll(delta: float) -> void:
	match stage:
		Stage.LOADING:
			stage_left -= delta
			if stage_left <= 0.0 or _everyone_in(_loaded):
				if sim.mode != null:
					_start_play()  # a street game has no weapon pick
					return
				stage = Stage.PICKING
				stage_left = sim.rules.weapon_pick_time + _net.pick_grace
				_broadcast(stage_message())
		Stage.PICKING:
			stage_left -= delta
			if stage_left <= 0.0 or _everyone_in(_picked):
				_start_play()
		Stage.PLAYING:
			_play(delta)


func _start_play() -> void:
	stage = Stage.PLAYING
	room.state = RoomService.State.PLAYING
	sim.begin_intro()
	_broadcast(stage_message())


func _play(delta: float) -> void:
	var away: int = _away_team()
	if away >= 0 and sim.phase != MatchSim.Phase.MATCH_OVER:
		sim.forfeit(away)
	var dt: float = sim.rules.tick_dt()
	_accumulator += delta
	while _accumulator >= dt:
		_accumulator -= dt
		for m: RoomService.Member in room.members:
			sim.set_input(m.player_id, _next_input(m))
		sim.step(dt)
	_snapshot_accumulator += delta
	if not _events.is_empty():
		_broadcast({"t": NetProtocol.Msg.EVENTS, "list": _events.duplicate()})
		_events.clear()
	if _snapshot_accumulator >= 1.0 / _net.snapshot_rate:
		_snapshot_accumulator = 0.0
		_send_snapshots(Snapshot.capture(sim, _acks))


## The team whose every player is away (AFK), or -1. In a 1v1 that is the one player who left.
func _away_team() -> int:
	for team: int in 2:
		var players: int = 0
		var away: int = 0
		for m: RoomService.Member in room.members:
			if m.team == team:
				players += 1
				if m.afk:
					away += 1
		if players > 0 and away == players:
			return team
	return -1


## The same public snapshot to everyone, each with only its own private part (elixir, stamina, move target).
func _send_snapshots(public: Dictionary) -> void:
	if _server == null:
		return
	for m: RoomService.Member in room.members:
		if m.connected:
			var message: Dictionary = public.duplicate()
			message.merge(Snapshot.private_for(sim, m.player_id))
			_server.send(m.peer_id, message)


## One input per tick per player, oldest first, so taps are never lost. With none
## queued the last one repeats (held buttons stay held, but a move order is not given twice); a dropped player idles (stands still).
func _next_input(m: RoomService.Member) -> PlayerInput:
	if not m.connected:
		if sim.players.has(m.player_id):
			sim.end_move(sim.players[m.player_id])
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick)
	var queue: Array = _queues[m.player_id]
	if not queue.is_empty():
		var input: PlayerInput = queue.pop_front() as PlayerInput
		_last_input[m.player_id] = input
		_acks[m.player_id] = input.tick
		return input
	if _last_input.has(m.player_id):
		var repeated: PlayerInput = _last_input[m.player_id]
		repeated.buttons &= ~PlayerInput.BTN_MOVE_TARGET
		return repeated
	return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick)


func _everyone_in(flags: Dictionary[int, bool]) -> bool:
	for m: RoomService.Member in room.members:
		if m.connected and not flags.has(m.player_id):
			return false
	return true


func _broadcast(message: Dictionary) -> void:
	if _server == null:
		return
	for m: RoomService.Member in room.members:
		if m.connected:
			_server.send(m.peer_id, message)


func _relay_signals() -> void:
	var sources: Dictionary[String, Object] = {"sim": sim, "weapons": sim.weapons}
	var relayed: Dictionary[String, Array] = RELAYED.duplicate()
	if sim.mode != null:
		# every signal the street game declares (tags, points, role swaps, dives...): the views of each phone play them
		sources["mode"] = sim.mode
		var names: Array[String] = []
		for info: Dictionary in (sim.mode.get_script() as Script).get_script_signal_list():
			if not names.has(info["name"] as String):
				names.append(info["name"] as String)
		relayed["mode"] = names
	for target: String in relayed:
		var source: Object = sources[target]
		for signal_name: String in relayed[target]:
			var arity: int = 0
			for info: Dictionary in source.get_signal_list():
				if info["name"] == signal_name:
					arity = (info["args"] as Array).size()
			var handlers: Array[Callable] = [_relay0, _relay1, _relay2, _relay3, _relay4]
			source.connect(signal_name, handlers[arity].bind(target, signal_name))


func _push_event(target: String, signal_name: String, args: Array) -> void:
	var clean: Array = []
	for arg: Variant in args:
		clean.append((arg as WeaponDef).id if arg is WeaponDef else arg)
	_events.append([target, signal_name, clean])


func _relay0(target: String, signal_name: String) -> void:
	_push_event(target, signal_name, [])


func _relay1(a: Variant, target: String, signal_name: String) -> void:
	_push_event(target, signal_name, [a])


func _relay2(a: Variant, b: Variant, target: String, signal_name: String) -> void:
	_push_event(target, signal_name, [a, b])


func _relay3(a: Variant, b: Variant, c: Variant, target: String, signal_name: String) -> void:
	_push_event(target, signal_name, [a, b, c])


func _relay4(a: Variant, b: Variant, c: Variant, d: Variant, target: String, signal_name: String) -> void:
	_push_event(target, signal_name, [a, b, c, d])
