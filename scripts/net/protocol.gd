class_name NetProtocol
extends RefCounted
## Message format between clients and the server. Every message is a Dictionary
## with "t" = Msg type, sent as var_to_bytes (no objects, so decoding is safe).

## Protocol revision, sent in HELLO and WELCOME; both ends refuse another one. Bump it when a field an older peer
## reads changes meaning (2: hand slots can be empty, the card cycle; 3: tap to move, a hand of 4 paid with elixir, stamina).
## New fields keep a default for older peers.
const VERSION: int = 3
## What a player sees when the two phones run different versions.
const INCOMPATIBLE_TEXT: String = "Update Kalyeah on both phones, then try again!"

enum Msg {
	# client -> server
	HELLO, CREATE_ROOM, JOIN_ROOM, PICK_TEAM, SET_READY, START, LEAVE, LOADED,
	PICK_WEAPONS, SWAP_WEAPONS, INPUT, SET_FIRST_TO,
	# server -> client
	WELCOME, ERROR, ROOM, MATCH_LOADING, MATCH_PLAY, SNAPSHOT, EVENTS,
	# client -> server, added later (appended so the numbers above stay the same). SET_TEAM_SIZE is retired (every room is 3v3): nobody sends
	# it and the server does nothing with it, but it keeps its number so the ones after it do not move.
	SET_TEAM_SIZE, SET_MAP, SET_CHARACTER,
	DISCOVER_ROOM, ROOM_DISCOVERED,
	# server -> client: the server is going away (a hotspot host left), so there is nothing to reconnect to
	ROOM_CLOSED,
	# Online open rooms: client -> server LIST_ROOMS and SET_OPEN (host), server -> client ROOM_LIST
	LIST_ROOMS, ROOM_LIST, SET_OPEN,
	# Local rooms: client -> server, the host fills the empty places with bots (or not)
	SET_FILL_BOTS,
	# Quick chat stickers during a match: client -> server SAY {"s": sticker index}; server -> every connected player of the room SAID
	# {"p": the sender's player id, "s": sticker index}. Appended, so no older number moves (a peer that does not know them ignores them).
	SAY, SAID,
}


static func encode(message: Dictionary) -> PackedByteArray:
	return var_to_bytes(message)


## Returns an empty Dictionary for anything that is not a valid message.
static func decode(bytes: PackedByteArray) -> Dictionary:
	var value: Variant = bytes_to_var(bytes)
	if value is Dictionary:
		var msg_type: Variant = value.get("t")
		if msg_type is int:
			return value
	return {}


static func snap_vec(v: Vector2, step: float = 0.01) -> Vector2:
	return Vector2(snappedf(v.x, step), snappedf(v.y, step))


static func input_to_dict(input: PlayerInput) -> Dictionary:
	var message: Dictionary = {"t": Msg.INPUT, "k": input.tick}
	if input.move != Vector2.ZERO:
		# 0.001 precision on unit-disc analog stick preserves the 0.62 walk/run boundary cleanly.
		message["m"] = snap_vec(input.move.limit_length(1.0), 0.001)
	if input.aim != Vector2.ZERO:
		message["a"] = snap_vec(input.aim.limit_length(1.0), 0.001)
	if input.buttons != 0:
		message["b"] = input.buttons
	if input.is_pressed(PlayerInput.BTN_MOVE_TARGET):
		message["mt"] = snap_vec(input.move_target)
	if input.is_pressed(PlayerInput.BTN_AIM_POINT):
		message["ap"] = snap_vec(input.aim_point)
	return message


## Untrusted client input: a field of the wrong type or not finite becomes its default, unknown buttons are dropped, and a point flag
## with no valid point is cleared. Points are only checked here; the sim pulls them into the street and to the range itself.
static func input_from_dict(message: Dictionary) -> PlayerInput:
	var buttons: int = _strict_int(message, "b") & PlayerInput.KNOWN_BITS
	var tick: int = _strict_int(message, "k")
	var input: PlayerInput = PlayerInput.create(
		_vector(message, "m").limit_length(1.0),
		_vector(message, "a").limit_length(1.0),
		buttons,
		tick
	)
	if _has_vector(message, "mt"):
		input.move_target = _vector(message, "mt")
	else:
		input.buttons &= ~PlayerInput.BTN_MOVE_TARGET
	if _has_vector(message, "ap"):
		input.aim_point = _vector(message, "ap")
	else:
		input.buttons &= ~PlayerInput.BTN_AIM_POINT
	return input


static func _strict_int(message: Dictionary, key: String) -> int:
	var v: Variant = message.get(key, 0)
	if v is int:
		return v
	return 0


static func _has_vector(message: Dictionary, key: String) -> bool:
	var v: Variant = message.get(key)
	if v is Vector2:
		return v.is_finite()
	return false


static func _vector(message: Dictionary, key: String) -> Vector2:
	var v: Variant = message.get(key)
	if v is Vector2 and v.is_finite():
		return v
	return Vector2.ZERO
