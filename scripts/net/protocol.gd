class_name NetProtocol
extends RefCounted
## Message format between clients and the server. Every message is a Dictionary
## with "t" = Msg type, sent as var_to_bytes (no objects, so decoding is safe).

## Protocol revision, sent in HELLO and WELCOME; both ends refuse another one. Bump it when a field an older peer
## reads changes meaning (2: hand slots can be empty, the card cycle; 3: tap to move, a hand of 4 paid with elixir, stamina).
## New fields keep a default for older peers.
const VERSION: int = 3
## What a player sees when the two phones run different versions.
const INCOMPATIBLE_TEXT: String = "Update both phones to the same game version."

enum Msg {
	# client -> server
	HELLO, CREATE_ROOM, JOIN_ROOM, PICK_TEAM, SET_READY, START, LEAVE, LOADED,
	PICK_WEAPONS, SWAP_WEAPONS, INPUT, SET_FIRST_TO,
	# server -> client
	WELCOME, ERROR, ROOM, MATCH_LOADING, MATCH_PLAY, SNAPSHOT, EVENTS,
	# client -> server, added later (appended so the numbers above stay the same)
	SET_TEAM_SIZE, SET_MAP, SET_CHARACTER,
	DISCOVER_ROOM, ROOM_DISCOVERED,
}


static func encode(message: Dictionary) -> PackedByteArray:
	return var_to_bytes(message)


## Returns an empty Dictionary for anything that is not a valid message.
static func decode(bytes: PackedByteArray) -> Dictionary:
	var value: Variant = bytes_to_var(bytes)
	if value is Dictionary:
		var message: Dictionary = value as Dictionary
		if message.has("t") and message["t"] is int:
			return message
	return {}


static func input_to_dict(input: PlayerInput) -> Dictionary:
	var message: Dictionary = {"t": Msg.INPUT, "k": input.tick, "m": input.move, "a": input.aim, "b": input.buttons}
	if input.is_pressed(PlayerInput.BTN_MOVE_TARGET):
		message["mt"] = input.move_target
	if input.is_pressed(PlayerInput.BTN_AIM_POINT):
		message["ap"] = input.aim_point
	return message


## Untrusted client input: a field of the wrong type or not finite becomes its default, unknown buttons are dropped, and a point flag
## with no valid point is cleared. Points are only checked here; the sim pulls them into the street and to the range itself.
static func input_from_dict(message: Dictionary) -> PlayerInput:
	var buttons: int = (message.get("b", 0) as int if message.get("b", 0) is int else 0) & PlayerInput.KNOWN_BITS
	var tick: int = message.get("k", 0) as int if message.get("k", 0) is int else 0
	var input: PlayerInput = PlayerInput.create(_vector(message, "m").limit_length(1.0), _vector(message, "a").limit_length(1.0), buttons, tick)
	if _has_vector(message, "mt"):
		input.move_target = message["mt"] as Vector2
	else:
		input.buttons &= ~PlayerInput.BTN_MOVE_TARGET
	if _has_vector(message, "ap"):
		input.aim_point = message["ap"] as Vector2
	else:
		input.buttons &= ~PlayerInput.BTN_AIM_POINT
	return input


static func _has_vector(message: Dictionary, key: String) -> bool:
	return message.get(key) is Vector2 and (message[key] as Vector2).is_finite()


static func _vector(message: Dictionary, key: String) -> Vector2:
	return message[key] as Vector2 if _has_vector(message, key) else Vector2.ZERO
