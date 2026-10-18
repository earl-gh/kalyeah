extends Node
## Local player / room state that outlives scenes: the name to show, what the
## lobby should open with, and the reconnect token (saved, so a reloaded page can
## take its slot back within the reconnect window).

const SAVE_PATH: String = "user://session.cfg"

var player_name: String = ""
## The character whose face is your avatar on the main screen (picked once, saved).
var avatar_id: String = ""
var room_code: String = ""
## LobbyScreen.Intent (CREATE or JOIN) chosen on the title screen.
var lobby_intent: int = 0
## True when the lobby was opened from the HOTSPOT button (it uses the hotspot address).
var lobby_hotspot: bool = false
## The room code typed in the Join dialog (8 characters), for the hotspot lobby to join with.
var lobby_code: String = ""
## Web builds only: show the hotspot room screen with made-up players (the web cannot host a room).
var lobby_preview: bool = false
## The mode card chosen on the main screen (an index into TitleScreen.MODES); Hotspot by default.
var menu_mode: int = 0
var reconnect_token: String = ""
var reconnect_url: String = ""


func _ready() -> void:
	var file: ConfigFile = ConfigFile.new()
	if file.load(SAVE_PATH) == OK:
		player_name = file.get_value("player", "name", "") as String
		avatar_id = file.get_value("player", "avatar", "") as String
		reconnect_token = file.get_value("match", "token", "") as String
		reconnect_url = file.get_value("match", "url", "") as String


func save() -> void:
	var file: ConfigFile = ConfigFile.new()
	file.set_value("player", "name", player_name)
	file.set_value("player", "avatar", avatar_id)
	file.set_value("match", "token", reconnect_token)
	file.set_value("match", "url", reconnect_url)
	file.save(SAVE_PATH)


const AVATARS: Array[String] = ["junjun", "ligaya", "migo", "toni", "popoy", "inday"]
const MAX_NAME_LENGTH: int = 12


## A new player gets a name ("Kalye482") and a random character face; both can be changed.
func ensure_profile() -> void:
	var changed: bool = false
	var dice: RandomNumberGenerator = RandomNumberGenerator.new()
	dice.randomize()
	if player_name.strip_edges().is_empty():
		player_name = "Kalye%d" % dice.randi_range(100, 999)
		changed = true
	if not AVATARS.has(avatar_id):
		avatar_id = AVATARS[dice.randi_range(0, AVATARS.size() - 1)]
		changed = true
	if changed:
		save()


## The name as the game will use it: trimmed and at most MAX_NAME_LENGTH characters.
static func clean_name(raw: String) -> String:
	return raw.strip_edges().left(MAX_NAME_LENGTH).strip_edges()
