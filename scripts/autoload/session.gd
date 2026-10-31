extends Node
## Local player / room state that outlives scenes: the name to show, what the
## lobby should open with, and the reconnect token (saved, so a restarted app can
## take its slot back within the reconnect window).

const SAVE_PATH: String = "user://session.cfg"

var player_name: String = ""
## The character whose face is your avatar on the main screen (picked once, saved).
var avatar_id: String = ""
var saved_loadout: Array[StringName] = []
var room_code: String = ""
## LobbyScreen.Intent (CREATE or JOIN) chosen on the title screen.
var lobby_intent: int = 0
## True when the lobby was opened from the HOTSPOT button (it uses the hotspot address).
var lobby_hotspot: bool = false
## The room a Hotspot Join found and verified (a `ws://` address), for the lobby to join.
var lobby_url: String = ""
## The mode card chosen on the main screen (an index into TitleScreen.MODES); Local, the middle card, by default.
var menu_mode: int = 1
## True once the first-run walkthrough has been finished or skipped (saved).
var coach_seen: bool = false
## Set (not saved) when the next practice match should open with the walkthrough: the first one, or a replay.
var coach_pending: bool = false
## Which Practice the next match is: `training` (a sandbox with dummies and one sparring partner) or `match` (a full 3v3 against
## bots). Not saved; the Practice card sets it just before the match loads.
var practice_kind: StringName = &"training"
## The street game Practice plays (a RoomService.MAP_IDS id): Agawan Base (`street`), Tumbang Preso or Patintero. Picked on the main
## screen's Practice row; saved.
var practice_game: StringName = &"street"
var reconnect_token: String = ""
var reconnect_url: String = ""


func _ready() -> void:
	var file: ConfigFile = ConfigFile.new()
	if file.load(SAVE_PATH) == OK:
		player_name = clean_name(file.get_value("player", "name", "") as String)
		avatar_id = file.get_value("player", "avatar", "") as String
		coach_seen = file.get_value("player", "coach_seen", false) as bool
		var game: StringName = StringName(file.get_value("player", "practice_game", "street") as String)
		practice_game = game if game in RoomService.MAP_IDS else &"street"
		var stored: Variant = file.get_value("player", "loadout", [])
		if stored is Array or stored is PackedStringArray:
			for id: Variant in stored:
				if id is String or id is StringName:
					saved_loadout.append(StringName(id))
		reconnect_token = file.get_value("match", "token", "") as String
		reconnect_url = file.get_value("match", "url", "") as String


func save() -> void:
	var file: ConfigFile = ConfigFile.new()
	file.set_value("player", "name", player_name)
	file.set_value("player", "avatar", avatar_id)
	file.set_value("player", "coach_seen", coach_seen)
	file.set_value("player", "practice_game", String(practice_game))
	file.set_value("player", "loadout", saved_loadout)
	file.set_value("match", "token", reconnect_token)
	file.set_value("match", "url", reconnect_url)
	file.save(SAVE_PATH)


const AVATARS: Array[String] = ["junjun", "ligaya", "migo", "toni", "popoy", "inday"]
## Names share the server's twelve-character limit; the tag fits its lettering within the card.
const MAX_NAME_LENGTH: int = 12
## What a name is made of: letters (any language, so Ñ and the rest), digits, and a space, apostrophe, dot, underscore or hyphen.
const NAME_CHARACTERS: String = "[^\\p{L}\\p{N} '._-]"
## The name a new player starts with (they can change it on the name tag).
const DEFAULT_NAME: String = "GABO"


## A new player gets the name GABO and a random character face; both can be changed.
func ensure_profile() -> void:
	var changed: bool = false
	var dice: RandomNumberGenerator = RandomNumberGenerator.new()
	dice.randomize()
	if player_name.strip_edges().is_empty():
		player_name = DEFAULT_NAME
		changed = true
	if not AVATARS.has(avatar_id):
		avatar_id = AVATARS[dice.randi_range(0, AVATARS.size() - 1)]
		changed = true
	if changed:
		save()


## The name as the game will use it: only the characters of `NAME_CHARACTERS`, single spaces, trimmed and at most MAX_NAME_LENGTH long.
static func clean_name(raw: String) -> String:
	return filter_name(raw).strip_edges().left(MAX_NAME_LENGTH).strip_edges()


## `raw` without the characters a name cannot have, and with one space where there were several (not trimmed or cut: for typing).
static func filter_name(raw: String) -> String:
	var kept: RegEx = RegEx.create_from_string(NAME_CHARACTERS)
	var spaced: RegEx = RegEx.create_from_string(" {2,}")
	return spaced.sub(kept.sub(raw, "", true), " ", true)


func loadout_for(available: Array[StringName]) -> Array[StringName]:
	var valid: Array[StringName] = []
	for id: StringName in saved_loadout:
		if available.has(id) and not valid.has(id) and valid.size() < WeaponPick.SLOTS:
			valid.append(id)
	return valid


func remember_loadout(picks: Array[StringName]) -> void:
	if picks.size() == WeaponPick.SLOTS and picks[0] != picks[1]:
		saved_loadout = picks.duplicate()
		save()
