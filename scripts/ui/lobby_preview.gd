class_name LobbyPreview
extends RefCounted
## Browser-only sample data. Never sends commands or presents a working network room.


static func start(screen: LobbyScreen) -> void:
	screen._set_status("")
	var names: Array[String] = [Session.player_name, "Migo", "Ligaya", "Popoy"]
	var kids: Array[String] = [Session.avatar_id, "migo", "ligaya", "popoy"]
	var members: Array = []
	for i: int in names.size():
		members.append({"player_id": i + 1, "name": names[i], "team": i % 2, "ready": i > 0, "connected": true, "char": kids[i]})
	screen._preview_room = {"code": "K7M2Q9XA", "team_size": 2, "host": LobbyScreen.PREVIEW_ME, "first_to": 3, "state": 0, "members": members,
		"can_start": false, "can_start_host": true, "map": &"street", "preview": true}
	screen._room_view.show_room(screen._preview_room, LobbyScreen.PREVIEW_ME)


static func member(screen: LobbyScreen, player_id: int) -> Dictionary:
	for member: Dictionary in screen._preview_room["members"] as Array:
		if member["player_id"] == player_id:
			return member
	return {}


## Keeps the made-up players inside the chosen team size (the extras step out).
static func fit(screen: LobbyScreen, size: int) -> void:
	var kept: Array = []
	var counts: Array[int] = [0, 0]
	for member: Dictionary in screen._preview_room["members"] as Array:
		var team: int = member["team"] as int
		if counts[team] < size:
			counts[team] += 1
			kept.append(member)
	screen._preview_room["members"] = kept
