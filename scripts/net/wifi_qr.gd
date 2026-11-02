class_name WifiQr
extends RefCounted
## The standard Wi-Fi QR text (`WIFI:T:WPA;S:<name>;P:<password>;;`, the one phone cameras and Android's own hotspot "Share"
## screen use), written for the game hotspot and read back from a scan or an NFC tap. `\`, `;`, `,`, `:` and `"` in a name or
## password are escaped with `\`.
##
## A network is a Dictionary: {"ssid": String, "password": String, "security": "WPA" | "SAE" | "nopass"}. WPA covers WPA2 and
## WPA2/WPA3 transition (what a phone hotspot is); SAE is WPA3 only. WEP is refused: Android cannot join it from an app.

const PREFIX: String = "WIFI:"
const SPECIAL: String = "\\;,:\""


static func build(network: Dictionary) -> String:
	var security: String = String(network.get("security", "WPA"))
	var text: String = PREFIX + "T:%s;S:%s;" % [security, _escape(String(network.get("ssid", "")))]
	if security != "nopass":
		text += "P:%s;" % _escape(String(network.get("password", "")))
	return text + ";"


## The network in a scanned or tapped text, or {} when it is not a Wi-Fi code Kalyeah can join.
static func parse(text: String) -> Dictionary:
	var clean: String = text.strip_edges()
	if not clean.to_upper().begins_with(PREFIX):
		return {}
	var fields: Dictionary = {}
	for field: String in _split(clean.substr(PREFIX.length()), ";"):
		var colon: int = _unescaped_colon(field)
		if colon <= 0:
			continue
		fields[field.substr(0, colon).to_upper()] = _unescape(field.substr(colon + 1))
	var ssid: String = String(fields.get("S", ""))
	if ssid.is_empty():
		return {}
	var password: String = String(fields.get("P", ""))
	var kind: String = String(fields.get("T", "")).to_upper()
	var security: String = ""
	match kind:
		"WPA", "WPA2", "WPA/WPA2", "WPA2/WPA3":
			security = "WPA"
		"SAE", "WPA3":
			security = "SAE"
		"NOPASS", "":
			security = "nopass" if password.is_empty() else "WPA"
		_:
			return {}
	if security != "nopass" and (password.length() < 8 or password.length() > 63):
		return {}
	return {"ssid": ssid, "password": password if security != "nopass" else "", "security": security}


static func _escape(value: String) -> String:
	var out: String = ""
	for character: String in value:
		if SPECIAL.contains(character):
			out += "\\"
		out += character
	return out


static func _unescape(value: String) -> String:
	var out: String = ""
	var escaped: bool = false
	for character: String in value:
		if escaped:
			out += character
			escaped = false
		elif character == "\\":
			escaped = true
		else:
			out += character
	return out


## `text` cut at each `separator` that is not escaped (the escapes are kept, for `_unescape`).
static func _split(text: String, separator: String) -> PackedStringArray:
	var parts: PackedStringArray = PackedStringArray()
	var current: String = ""
	var escaped: bool = false
	for character: String in text:
		if escaped:
			current += character
			escaped = false
		elif character == "\\":
			current += character
			escaped = true
		elif character == separator:
			parts.append(current)
			current = ""
		else:
			current += character
	if not current.is_empty():
		parts.append(current)
	return parts


static func _unescaped_colon(field: String) -> int:
	var escaped: bool = false
	for index: int in field.length():
		if escaped:
			escaped = false
		elif field[index] == "\\":
			escaped = true
		elif field[index] == ":":
			return index
	return -1
