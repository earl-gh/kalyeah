class_name RoomQr
extends RefCounted
## What an Online room's QR code says: `KALYEAH:` and the room code. Short and capitals-only, so it fits the smallest QR code
## (21 x 21 modules, the easiest for a camera to read), and nothing else scanned is taken for a room.

const PREFIX: String = "KALYEAH:"
const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")


static func payload(code: String) -> String:
	return PREFIX + code


## The room code in a scanned text, or "" when it is not a Kalyeah room's.
static func code_from(text: String) -> String:
	var clean: String = text.strip_edges().to_upper()
	if not clean.begins_with(PREFIX):
		return ""
	var code: String = clean.substr(PREFIX.length())
	if code.length() != NET_RULES.passcode_length:
		return ""
	for character: String in code:
		if NET_RULES.passcode_alphabet.find(character) < 0:
			return ""
	return code
