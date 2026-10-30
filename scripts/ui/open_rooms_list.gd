class_name OpenRoomsList
extends VBoxContainer
## The Online Join screen's open rooms (owner, 2026-10-30: no code needed): Quick join, then a card for each room the server
## lists (`GameClient.rooms_listed`: open, in the lobby, not full, the fullest first), tap one to join it. With none, a line and a
## Create button. Private rooms are joined with their code below it (LobbyOnlineLayout).

signal room_picked(code: String)
signal create_pressed

const CARD_HEIGHT: float = 104.0
const EMPTY_TEXT: String = "No open rooms yet. Create one!"
const LOOKING_TEXT: String = "Looking for rooms…"

var quick_button: Button
var create_button: Button
var empty_label: Label
## One card a room, newest list first; each card's meta "code" is its room code.
var cards: Array[Button] = []
var _rooms: Array[Dictionary] = []
var _list: VBoxContainer


func _ready() -> void:
	name = "OpenRooms"
	add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(LobbyWidgets.field_label("Open rooms"))
	quick_button = LobbyWidgets.button("Quick join", quick_join, "orange")
	quick_button.name = "QuickJoin"
	quick_button.custom_minimum_size = Vector2(0.0, 96.0)
	quick_button.disabled = true
	add_child(quick_button)
	_list = VBoxContainer.new()
	_list.name = "Rooms"
	_list.add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(_list)
	empty_label = LobbyWidgets.label(LOOKING_TEXT, UiMetrics.BODY, KalyeahTheme.TEXT_MUTED)
	empty_label.name = "Empty"
	empty_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(empty_label)
	create_button = LobbyWidgets.button("Create a room", create_pressed.emit, "grey")
	create_button.name = "CreateRoom"
	create_button.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	create_button.visible = false
	add_child(create_button)


## Shows the server's latest list (replacing the last one).
func show_rooms(rooms: Array[Dictionary]) -> void:
	_rooms = rooms
	for card: Button in cards:
		card.queue_free()
	cards.clear()
	for room: Dictionary in rooms:
		var card: Button = LobbyWidgets.button(card_text(room), room_picked.emit.bind(String(room["code"])), "blue")
		card.custom_minimum_size = Vector2(0.0, CARD_HEIGHT)
		card.set_meta("code", String(room["code"]))
		card.accessibility_name = card_text(room)
		_list.add_child(card)
		cards.append(card)
	quick_button.disabled = rooms.is_empty()
	empty_label.text = EMPTY_TEXT
	empty_label.visible = rooms.is_empty()
	create_button.visible = rooms.is_empty()


## "Ana's room", then "2v2 · 3/4 · First to 3" on a second line (a 12-letter name and the details do not fit one line on a phone).
static func card_text(room: Dictionary) -> String:
	var size: int = int(room.get("team_size", 1))
	var host: String = String(room.get("host_name", ""))
	var title: String = UiText.f("%s's room", [host]) if not host.is_empty() else UiText.t("Open room")
	return "%s\n%dv%d · %d/%d · %s" % [title, size, size, int(room.get("players", 0)), size * 2,
		UiText.f("First to %d", [int(room.get("first_to", 3))])]


## Joins the best listed room: the fullest (the server sends them fullest first), so a match fills and starts sooner.
func quick_join() -> void:
	if not _rooms.is_empty():
		room_picked.emit(String(_rooms[0]["code"]))
