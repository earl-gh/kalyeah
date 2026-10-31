class_name RoomService
extends RefCounted
## Online rooms (docs/GDD.md "Lobby rules"): open (listed, joined with a tap) or private (passcode only). Pure logic, no sockets:
## the GameServer feeds it peer ids and sends out whatever changed.
##
## A member keeps one stable `player_id` for the whole match (it is their id in
## the MatchSim); `peer_id` is their current connection and changes on reconnect.

enum Result { OK, NOT_FOUND, FULL, IN_PROGRESS, NOT_ALLOWED }
enum State { LOBBY, LOADING, PLAYING, ENDED }


class Member extends RefCounted:
	var player_id: int
	var peer_id: int
	var name: String
	## -1 = no team slot picked yet.
	var team: int = -1
	var ready: bool = false
	var connected: bool = true
	## Seconds left to reconnect after dropping out of a running match.
	var reconnect_left: float = 0.0
	## Seconds since the member dropped out of a running match (0 while connected).
	var gone_for: float = 0.0
	## Gone for `afk_after` seconds or more: away (AFK) until they come back.
	var afk: bool = false
	## Secret handed to the client so it can take its slot back after a drop.
	var token: String = ""
	## The kid the member picked in the room (cosmetic, shown to the others); &"" = not picked.
	var character: StringName = &""


class Room extends RefCounted:
	var code: String
	var team_size: int
	var host_player_id: int
	## The first team to this many points wins the match (the host picks 3, 5 or 7 in the lobby).
	var first_to: int = 0
	## The street the match is played on (the host picks it; one so far).
	var map_id: StringName = &"street"
	## Listed for anyone browsing Online (owner: the default); private rooms are joined only with their code.
	var open: bool = true
	var state: State = State.LOBBY
	var members: Array[Member] = []

	func member(player_id: int) -> Member:
		for m: Member in members:
			if m.player_id == player_id:
				return m
		return null

	func team_count(team: int) -> int:
		var count: int = 0
		for m: Member in members:
			if m.team == team:
				count += 1
		return count


## The street games a host sees in the map list, and the ones that can be played so far (the other
## two are listed as coming soon).
## The biggest team: 3v3 (a room is 2 to 6 players).
const MAX_TEAM_SIZE: int = 3
const MAP_IDS: Array[StringName] = [&"street", &"tumbang_preso", &"patintero"]
const PLAYABLE_MAP_IDS: Array[StringName] = [&"street", &"patintero"]

var rooms: Dictionary[String, Room] = {}
## What a host may pick, and what a new room starts with (GameRules.first_to*).
var first_to_choices: Array[int] = [3]
var default_first_to: int = 3

var _rules: NetRules
var _rng: RandomNumberGenerator = RandomNumberGenerator.new()
var _next_player_id: int = 1
## peer id -> room code, for connected members.
var _peer_rooms: Dictionary[int, String] = {}


func _init(rules: NetRules, seed_value: int) -> void:
	_rules = rules
	_rng.seed = seed_value


func clean_name(raw: String) -> String:
	var text: String = raw.strip_edges().left(_rules.max_name_length)
	return text if not text.is_empty() else "Player"


func room_of_peer(peer_id: int) -> Room:
	var code: String = _peer_rooms.get(peer_id, "") as String
	return rooms.get(code) as Room


func member_of_peer(peer_id: int) -> Member:
	var room: Room = room_of_peer(peer_id)
	if room == null:
		return null
	for m: Member in room.members:
		if m.peer_id == peer_id and m.connected:
			return m
	return null


## Creates a room for 1v1 (team_size 1), 2v2 or 3v3; the creator joins as host.
func create_room(peer_id: int, player_name: String, team_size: int) -> Room:
	if room_of_peer(peer_id) != null or team_size < 1 or team_size > MAX_TEAM_SIZE:
		return null
	var room: Room = Room.new()
	room.code = _new_code()
	room.team_size = team_size
	room.first_to = default_first_to
	rooms[room.code] = room
	var host: Member = _add_member(room, peer_id, player_name)
	room.host_player_id = host.player_id
	return room


func join(code: String, peer_id: int, player_name: String) -> Result:
	var room: Room = rooms.get(code.strip_edges().to_upper()) as Room
	if room == null:
		return Result.NOT_FOUND
	if room.state != State.LOBBY:
		return Result.IN_PROGRESS
	if room.members.size() >= room.team_size * 2:
		return Result.FULL
	if room_of_peer(peer_id) != null:
		return Result.NOT_ALLOWED
	_add_member(room, peer_id, player_name)
	return Result.OK


## Moves a member to a team slot (0 or 1) if it has room. Un-readies them.
func pick_team(peer_id: int, team: int) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or room.state != State.LOBBY or team < 0 or team > 1 or m.team == team:
		return false
	if room.team_count(team) >= room.team_size:
		return false
	for other: Member in room.members:
		if other.team == team and m.character != &"" and other.character == m.character:
			return false
	m.team = team
	m.ready = false
	return true


func set_ready(peer_id: int, ready: bool) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or room.state != State.LOBBY or (ready and m.team < 0):
		return false
	m.ready = ready
	return true


## GDD: every present player is Ready and has a team; one team has N players and
## the other at least N-1 (1v1 needs both players).
## `host_ready`: count the host as ready (the room screen's one button readies and starts at once).
func can_start(room: Room, host_ready: bool = false) -> bool:
	if room.state != State.LOBBY:
		return false
	for m: Member in room.members:
		var is_ready: bool = m.ready or (host_ready and m.player_id == room.host_player_id)
		if not is_ready or m.team < 0:
			return false
	var a: int = room.team_count(0)
	var b: int = room.team_count(1)
	var n: int = room.team_size
	if n == 1:
		return a == 1 and b == 1
	return (a == n and b >= n - 1) or (b == n and a >= n - 1)


## Host only, in the lobby: first to 3, 5 or 7 (one of `first_to_choices`).
func set_first_to(peer_id: int, count: int) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or m.player_id != room.host_player_id or room.state != State.LOBBY:
		return false
	if not count in first_to_choices:
		return false
	room.first_to = count
	return true


## Host only, in the lobby: 1v1, 2v2 or 3v3, if everyone already in the room still fits. Un-readies
## everyone (the match they agreed to changed).
func set_team_size(peer_id: int, size: int) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or m.player_id != room.host_player_id or room.state != State.LOBBY:
		return false
	if size < 1 or size > 3 or size == room.team_size:
		return false
	if room.team_count(0) > size or room.team_count(1) > size or room.members.size() > size * 2:
		return false
	room.team_size = size
	for other: Member in room.members:
		other.ready = false
		# a member with no team slot yet takes a free one if there is room
		if other.team < 0:
			var team: int = 0 if room.team_count(0) <= room.team_count(1) else 1
			if room.team_count(team) < size:
				other.team = team
	return true


## Host only, in the lobby: the map the match is played on (one of `PLAYABLE_MAP_IDS`).
func set_map(peer_id: int, map_id: StringName) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or m.player_id != room.host_player_id or room.state != State.LOBBY:
		return false
	if not map_id in PLAYABLE_MAP_IDS:
		return false
	room.map_id = map_id
	return true


## Host only, in the lobby: open (listed) or private (code only).
func set_open(peer_id: int, open: bool) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or m.player_id != room.host_player_id or room.state != State.LOBBY:
		return false
	room.open = open
	return true


## The rooms anyone can join with a tap: open, still in the lobby, not full; the fullest first (then the oldest), at most `limit`.
func open_rooms(limit: int) -> Array[Dictionary]:
	var found: Array[Room] = []
	for room: Room in rooms.values():
		if room.open and room.state == State.LOBBY and room.members.size() < room.team_size * 2:
			found.append(room)
	# rooms keep their creation order, so a stable sort by size puts the oldest first among equals
	found.sort_custom(func(a: Room, b: Room) -> bool: return a.members.size() > b.members.size())
	var listed: Array[Dictionary] = []
	for room: Room in found.slice(0, maxi(limit, 0)):
		var host: Member = room.member(room.host_player_id)
		listed.append({
			"code": room.code, "host_name": host.name if host != null else "", "team_size": room.team_size,
			"players": room.members.size(), "first_to": room.first_to, "map": room.map_id,
		})
	return listed


## Any member, in the lobby: the kid they will play (one of Session.AVATARS), shown to the others.
func set_character(peer_id: int, character: StringName) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or room.state != State.LOBBY or not String(character) in Session.AVATARS:
		return false
	for other: Member in room.members:
		if other != m and other.team == m.team and other.character == character:
			return false
	m.character = character
	m.ready = false
	return true


## Host only. The room moves to LOADING.
func start(peer_id: int) -> bool:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	if m == null or m.player_id != room.host_player_id or not can_start(room):
		return false
	room.state = State.LOADING
	return true


## Leaving the lobby (or a lost connection there). The next member becomes host;
## an empty room is closed. Returns the room (or null if it closed).
func leave(peer_id: int) -> Room:
	var room: Room = room_of_peer(peer_id)
	var m: Member = member_of_peer(peer_id)
	_peer_rooms.erase(peer_id)
	if room == null or m == null:
		return null
	room.members.erase(m)
	if room.members.is_empty():
		rooms.erase(room.code)
		return null
	if room.host_player_id == m.player_id:
		room.host_player_id = room.members[0].player_id
	return room


## A connection dropped. In the lobby that is a leave; in a running match the slot
## is kept and the player has `reconnect_window` seconds to come back.
func disconnected(peer_id: int) -> Room:
	var room: Room = room_of_peer(peer_id)
	if room == null:
		return null
	if room.state == State.LOBBY:
		return leave(peer_id)
	var m: Member = member_of_peer(peer_id)
	_peer_rooms.erase(peer_id)
	if m != null:
		m.connected = false
		m.reconnect_left = _rules.reconnect_window
		m.gone_for = 0.0
	return room


## Takes a dropped slot back with its token. Returns the member, or null.
func reconnect(token: String, peer_id: int) -> Member:
	if token.is_empty() or room_of_peer(peer_id) != null:
		return null
	for code: String in rooms:
		for m: Member in rooms[code].members:
			if m.token == token and not m.connected and m.reconnect_left > 0.0:
				m.connected = true
				m.peer_id = peer_id
				m.reconnect_left = 0.0
				m.gone_for = 0.0
				m.afk = false
				_peer_rooms[peer_id] = code
				return m
	return null


## Counts reconnect windows down. A slot whose window ran out stays in the match
## (the player just stands still); its token stops working. Returns the rooms where someone just turned away (AFK).
func step(dt: float) -> Array[Room]:
	var changed: Array[Room] = []
	for code: String in rooms.keys():
		var room: Room = rooms[code]
		var anyone: bool = false
		for m: Member in room.members:
			if not m.connected and m.reconnect_left > 0.0:
				m.reconnect_left = maxf(0.0, m.reconnect_left - dt)
			if not m.connected and room.state != State.LOBBY:
				m.gone_for += dt
				if not m.afk and m.gone_for >= _rules.afk_after:
					m.afk = true
					if not changed.has(room):
						changed.append(room)
			if m.connected or m.reconnect_left > 0.0:
				anyone = true
		if not anyone:
			rooms.erase(code)
	return changed


## Plain data for the ROOM message.
func describe(room: Room) -> Dictionary:
	var members: Array[Dictionary] = []
	for m: Member in room.members:
		members.append({
			"player_id": m.player_id, "name": m.name, "team": m.team,
			"ready": m.ready, "connected": m.connected, "afk": m.afk, "char": m.character,
		})
	return {
		"code": room.code, "team_size": room.team_size, "host": room.host_player_id, "first_to": room.first_to,
		"state": room.state, "members": members, "can_start": can_start(room),
		"can_start_host": can_start(room, true), "map": room.map_id, "open": room.open,
	}


func _add_member(room: Room, peer_id: int, player_name: String) -> Member:
	var m: Member = Member.new()
	m.player_id = _next_player_id
	_next_player_id += 1
	m.peer_id = peer_id
	m.name = clean_name(player_name)
	m.token = "%08x%08x" % [_rng.randi(), _rng.randi()]
	# drop into the smaller team (ties go to team 0)
	var team: int = 0 if room.team_count(0) <= room.team_count(1) else 1
	if room.team_count(team) < room.team_size:
		m.team = team
	for avatar: String in Session.AVATARS:
		if not room.members.any(func(other: Member) -> bool: return other.team == m.team and other.character == StringName(avatar)):
			m.character = StringName(avatar)
			break
	room.members.append(m)
	_peer_rooms[peer_id] = room.code
	return m


func _new_code() -> String:
	while true:
		var code: String = ""
		for i: int in _rules.passcode_length:
			code += _rules.passcode_alphabet[_rng.randi_range(0, _rules.passcode_alphabet.length() - 1)]
		if not rooms.has(code):
			return code
	return ""
