extends GutTest

var _saved_loadout: Array[StringName] = []
## The lobby and the online match screen against a real in-process server.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const LOBBY: PackedScene = preload("res://scenes/ui/lobby.tscn")
const ONLINE: PackedScene = preload("res://scenes/match/online.tscn")
const PORT: int = 18932
const URL: String = "ws://127.0.0.1:18932"
const QR_PICTURE: GDScript = preload("res://tests/fixtures/qr_picture.gd")
const FRAME: float = 1.0 / 60.0

var server: GameServer
var clients: Array[GameClient] = []
var screens: Array[Node] = []


func before_each() -> void:
	_saved_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout.clear()
	server = GameServer.new(RULES, LAYOUT, NET, 9)
	assert_eq(server.listen(PORT), OK)
	clients.clear()
	screens.clear()


func after_each() -> void:
	Session.saved_loadout = _saved_loadout
	Session.save()
	for client: GameClient in clients:
		client.close()
	server.stop()
	await wait_process_frames(2) # Flush room cards queued during synchronous network pumping.


func _pump(seconds: float) -> void:
	for i: int in ceili(seconds / FRAME):
		server.poll(FRAME)
		for client: GameClient in clients:
			client.poll(FRAME)
		for screen: Node in screens:
			if screen is PracticeMatch and is_instance_valid(screen):
				(screen as PracticeMatch).advance(FRAME)
		OS.delay_usec(1000)


func _until(condition: Callable, limit: float = 5.0) -> bool:
	var waited: float = 0.0
	while waited < limit and not condition.call():
		_pump(0.05)
		waited += 0.05
	return condition.call()


func _lobby(intent: LobbyScreen.Intent) -> LobbyScreen:
	var client: GameClient = GameClient.new(RULES, LAYOUT, NET)
	clients.append(client)
	var lobby: LobbyScreen = autofree(LOBBY.instantiate()) as LobbyScreen
	lobby.client = client
	lobby.enter_match_on_start = false
	lobby.set_intent(intent)
	add_child(lobby)
	return lobby


func _two_lobbies_ready() -> Array[LobbyScreen]:
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()), "host is in a room")
	var code: String = host.client.room["code"] as String
	assert_string_starts_with(host.code_text(), code)
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	guest.set_fields(URL, "Ben", code.to_lower())
	guest.submit()
	assert_true(_until(func() -> bool: return guest.in_room()), "guest joined")
	host.press_ready()
	guest.press_ready()
	assert_true(_until(func() -> bool: return host.client.room.get("can_start_host", false) as bool), "the room is ready to start")
	return [host, guest]


func _toast_text(node: Node) -> String:
	var layer: Node = node.get_tree().root.get_node_or_null(UiToast.LAYER_NAME)
	if layer == null:
		return ""
	for toast: Node in layer.get_children():
		if toast is UiToast and not toast.is_queued_for_deletion():
			return (toast as UiToast).text
	return ""


func test_a_host_who_cannot_start_yet_taps_start_and_is_told_why() -> void:
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()), "host is in a room")
	assert_true(host.start_enabled(), "Start is always pressable for the host: pressing it says why not")
	assert_false(host.client.room.get("can_start_host", true) as bool, "alone, Agawan Base has an empty team")
	host.press_start()
	await wait_process_frames(3)
	assert_eq(_toast_text(host), "Agawan Base needs at least 1 player on each team.", "the host reads it in a toast")
	assert_eq(server.rooms.rooms.values()[0].state, RoomService.State.LOBBY, "and nothing started")


func test_the_server_says_why_a_start_is_refused() -> void:
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()))
	var code: String = host.client.room["code"] as String
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	guest.set_fields(URL, "Ben", code)
	guest.submit()
	assert_true(_until(func() -> bool: return guest.in_room()))
	host.client.set_map(&"patintero")
	assert_true(_until(func() -> bool: return host.client.room.get("map", "") == "patintero"))
	var said: Array[String] = []
	host.client.error_received.connect(func(text: String) -> void: said.append(text))
	host.client.set_ready(true)
	host.client.start_match()
	assert_true(_until(func() -> bool: return not said.is_empty()))
	assert_eq(said[0], "Patintero needs at least 2 players on each team.", "1 v 1 is not enough for Patintero")
	var guest_said: Array[String] = []
	guest.client.error_received.connect(func(text: String) -> void: guest_said.append(text))
	guest.client.start_match()
	assert_true(_until(func() -> bool: return not guest_said.is_empty()))
	assert_eq(guest_said[0], "Only the host can start the game.")


func test_lobby_create_join_ready_start() -> void:
	var pair: Array[LobbyScreen] = _two_lobbies_ready()
	assert_false(pair[1].start_enabled(), "only the host has Start")
	var room: RoomService.Room = server.rooms.rooms.values()[0]
	assert_eq(room.member(pair[0].client.player_id).name, "Ana", "Create waits for HELLO/WELCOME so the chosen name is preserved")
	assert_eq(room.member(pair[1].client.player_id).name, "Ben", "Join also preserves the chosen name")
	pair[0].press_start()
	assert_true(_until(func() -> bool: return pair[0].client.in_match() and pair[1].client.in_match()))
	assert_eq(pair[1].status_text(), "Loading…")


func test_join_with_a_bad_code_shows_why() -> void:
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	guest.set_fields(URL, "Ben", "ABC")
	guest.submit()
	assert_eq(guest.status_text(), "Enter all 8 characters!")
	guest.set_fields(URL, "Ben", "ZZZZZZZZ")
	guest.submit()
	assert_true(_until(func() -> bool: return guest.status_text() == "No room with that code. Check it and try again!"))
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()))
	guest.set_fields(URL, "Carlo", host.code_text())
	guest.submit()
	assert_true(_until(func() -> bool: return guest.in_room()))
	var room: RoomService.Room = server.rooms.rooms.values()[0]
	assert_eq(room.member(guest.client.player_id).name, "Carlo", "correcting a failed join also refreshes an edited name")


func test_online_match_screen_picks_then_plays_against_the_server() -> void:
	var pair: Array[LobbyScreen] = _two_lobbies_ready()
	pair[0].press_start()
	assert_true(_until(func() -> bool: return pair[0].client.in_match() and pair[1].client.in_match()))
	var views: Array[PracticeMatch] = []
	for lobby: LobbyScreen in pair:
		var view: PracticeMatch = autofree(ONLINE.instantiate()) as PracticeMatch
		view.client = lobby.client
		var profile_before: String = Session.avatar_id
		var lobby_kid: StringName = lobby.client.mirror.players[lobby.client.player_id].character_id
		Session.avatar_id = "inday" if lobby_kid != &"inday" else "toni"
		add_child(view)
		assert_eq((view.get_node("%PickScreen") as WeaponPickScreen).character_id, lobby_kid, "the server's room choice overrides a different profile preference")
		Session.avatar_id = profile_before
		views.append(view)
		screens.append(view)
		assert_true(view.is_picking(), "weapon pick first")
	var pick: WeaponPickScreen = views[0].get_node("%PickScreen") as WeaponPickScreen
	pick.pick_character(&"popoy")
	pick.tap(&"bato_light")
	pick.tap(&"lata")
	pick.press_ready()
	(views[1].get_node("%PickScreen") as WeaponPickScreen).pick_character(&"popoy")
	(views[1].get_node("%PickScreen") as WeaponPickScreen).tap(&"jacks")
	(views[1].get_node("%PickScreen") as WeaponPickScreen).tap(&"bola")
	(views[1].get_node("%PickScreen") as WeaponPickScreen).press_ready()
	assert_true(_until(func() -> bool: return not views[0].is_picking() and not views[1].is_picking()), "both playing")
	assert_eq(views[0].hud.again_button.text, "Return home")
	var server_sim: MatchSim = server.matches.values()[0].sim
	assert_eq(server_sim.phase, MatchSim.Phase.INTRO, "the opening show plays first online too")
	server_sim.intro_left = 0.0
	_pump(0.2)
	assert_eq(server_sim.phase, MatchSim.Phase.PLAYING)
	var me: int = views[0].local_id
	assert_eq(server_sim.players[me].weapons, [&"bato_light", &"lata"] as Array[StringName])
	assert_eq(server_sim.players[me].character_id, &"popoy", "the server plays the kid we picked")
	assert_eq(server_sim.players[views[1].local_id].character_id, &"popoy", "the other team may play the same kid")
	assert_eq(views[1].sim.players[me].character_id, &"popoy", "the other player sees our kid")
	assert_eq((views[1]._views[me] as KidModel).character.id, &"popoy", "and its 3D model")
	var start: Vector2 = server_sim.players[me].position
	var goal: Vector2 = start + Vector2(0.0, 4.0 * -float(server_sim.side_for_team(server_sim.players[me].team)))
	views[0].input.move_press(goal)
	views[0].input.move_release()
	_pump(1.5)
	var walked: float = start.distance_to(server_sim.players[me].position)
	assert_gt(walked, 3.0, "the server moved us")
	# the tapped street point crosses the wire (it once did not: every tap walked toward (0, 0))
	assert_almost_eq(server_sim.players[me].position.distance_to(goal), 0.0, 0.3, "the server walked us to the tapped point")
	assert_almost_eq(views[0].sim.players[me].position.distance_to(server_sim.players[me].position), 0.0, 0.3, "our screen agrees")
	assert_almost_eq(views[1].sim.players[me].position.distance_to(server_sim.players[me].position), 0.0, 0.3, "and theirs")


func test_the_match_screen_reconnects_by_itself_after_a_drop() -> void:
	var pair: Array[LobbyScreen] = _two_lobbies_ready()
	pair[0].press_start()
	assert_true(_until(func() -> bool: return pair[0].client.in_match() and pair[1].client.in_match()))
	var guest: GameClient = pair[1].client
	Session.reconnect_url = URL
	var view: PracticeMatch = autofree(ONLINE.instantiate()) as PracticeMatch
	view.client = guest
	add_child(view)
	screens.append(view)
	var mirror: MatchSim = guest.mirror
	guest.close()
	assert_true(view.hud.connection_blocked)
	assert_true(view.hud.feedback.connection.visible)
	assert_false(view.controls_active())
	assert_true(_until(func() -> bool: return guest.is_online() and guest.stage != "", 8.0), "back online")
	if view._awaiting_reconnect_welcome:
		var before: int = view._input_tick
		view._step_once(FRAME)
		assert_eq(view._input_tick, before, "socket recovery cannot send input before WELCOME")
	assert_true(_until(func() -> bool: return not view._awaiting_reconnect_welcome), "server confirms restored slot")
	assert_false(view.hud.connection_blocked)
	assert_false(view.hud.feedback.connection.visible)
	assert_false(view.controls_active(), "still selecting weapons after reconnect")
	assert_eq(guest.mirror, mirror, "same mirror, so the screen keeps working")
	var member: RoomService.Member = server.rooms.rooms.values()[0].member(guest.player_id)
	assert_true(_until(func() -> bool: return member.connected, 3.0), "the server gave the slot back")
	assert_eq(view.banner_text(), "Back online!", "success is shown only after the slot is reclaimed")
	Session.reconnect_url = ""


func test_a_hotspot_lobby_is_the_room_screen_with_no_form() -> void:
	Session.lobby_hotspot = true
	var joining: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	assert_null(joining.find_child("ServerField", true, false), "no address to type on a hotspot")
	assert_null(joining.find_child("LobbyCard", true, false), "no form: the room screen is all there is")
	assert_not_null(joining._room_view, "the hotspot room screen")
	assert_false(joining._room_view.has_room(), "nothing to show until a room exists")
	Session.lobby_hotspot = false
	assert_true(joining.hotspot, "a deferred hotspot connection retains its original transport")


func test_a_hotspot_guest_joins_the_room_the_dialog_found_with_no_code() -> void:
	server.auto_join = true
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()), "the host is in its room")
	Session.lobby_hotspot = true
	Session.lobby_url = URL
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	await wait_process_frames(2)
	assert_true(_until(func() -> bool: return guest.in_room()), "the guest went to the found room")
	assert_eq(guest.client.room["code"], host.client.room["code"])
	assert_eq((guest.client.room["members"] as Array).size(), 2)
	Session.lobby_hotspot = false
	Session.lobby_url = ""


func test_a_hotspot_create_on_a_busy_port_says_so_and_offers_try_again() -> void:
	var blocker: TCPServer = TCPServer.new()
	assert_eq(blocker.listen(NET.default_port), OK)
	Session.lobby_hotspot = true
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	await wait_process_frames(2)
	assert_eq(host.status_text(), "Oops! This phone can't start the room.")
	assert_true(host._room_view.retry_button.visible, "Try again is there")
	assert_null(Net.host, "no half-started server")
	blocker.stop()
	host._room_view.retry_pressed.emit()
	await wait_process_frames(2)
	assert_not_null(Net.host, "after the port is free, Try again starts the room")
	Net.stop_host()
	Session.lobby_hotspot = false


func test_a_hotspot_join_with_no_room_found_says_so_instead_of_connecting() -> void:
	Session.lobby_hotspot = true
	Session.lobby_url = ""
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	await wait_process_frames(2)
	assert_eq(guest.status_text(), "No room found. Go back and tap Join room.")
	assert_false(guest.in_room())
	Session.lobby_hotspot = false


func test_the_online_lobby_has_no_server_field_and_says_so_when_none_is_built_in() -> void:
	Session.lobby_hotspot = false
	var saved: String = Settings.server_url
	Settings.server_url = ""
	var lobby: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	lobby.set_fields("", "Ana")
	lobby.submit()
	assert_eq(lobby.status_text(), "Online is coming soon!")
	assert_eq(lobby.find_children("*", "LineEdit", true, false).size(), 0, "Create has no form: no name box either (the name is the profile's)")
	Settings.server_url = saved


func test_online_uses_shared_room_controls_and_server_confirmed_choices() -> void:
	var pair: Array[LobbyScreen] = _two_lobbies_ready()
	var host: LobbyScreen = pair[0]
	var guest: LobbyScreen = pair[1]
	assert_true(host._room_view.visible)
	assert_null(host._setup_layer, "Create has no form to hide")
	assert_true(host._room_view.code_row.visible)
	assert_eq(guest.code_text(), host.code_text())
	assert_eq(guest._room_view.action_button.text, "Cancel ready")
	host._room_view.first_to_chosen.emit(5)
	assert_true(_until(func() -> bool: return guest._room_view.format_summary.text.contains("First to 5")))
	assert_true(guest._room_view.format_summary.text.begins_with("3v3"), "every room is 3v3: no size to pick")
	guest._room_view.character_chosen.emit("toni")
	assert_true(_until(func() -> bool: return (guest._room_view.kid_cards["toni"].get_node("Chosen") as Control).visible))
	assert_eq(guest._room_view.action_button.text, "Ready", "changing a character requires confirmation again")
	guest._on_leave()
	assert_false(guest.in_room())
	assert_true(guest._setup_layer.visible)
	assert_eq(guest.code_text(), "")


func test_backgrounding_stops_server_input_without_another_view_frame() -> void:
	var pair: Array[LobbyScreen] = _two_lobbies_ready()
	pair[0].press_start()
	assert_true(_until(func() -> bool: return pair[0].client.in_match() and pair[1].client.in_match()))
	var view: PracticeMatch = autofree(ONLINE.instantiate()) as PracticeMatch
	view.client = pair[0].client
	add_child(view)
	view.set_process(false)
	screens.append(view)
	pair[1].client.loaded()
	assert_true(_until(func() -> bool: return view.client.stage == "pick"))
	view.finish_pick()
	pair[1].client.pick_weapons(&"bato_light", &"lata")
	assert_true(_until(func() -> bool: return not view.is_picking()))
	var server_sim: MatchSim = server.matches.values()[0].sim
	server_sim.intro_left = 0.0
	_pump(0.2)
	var player: PlayerState = server_sim.players[view.local_id]
	player.elixir = server_sim.rules.elixir_max  # every card affordable
	var held: StringName = player.weapons[0]
	view.input.card_press(0, Time.get_ticks_msec())
	_pump(0.2)
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	screens.erase(view) # Android may stop rendering before another advance() call.
	clients.erase(view.client) # Its Net autoload also stops polling when backgrounded.
	_pump(0.4)
	assert_eq(player.aim_hold[0], -1.0, "server must receive cancel without waiting for a rendered frame")
	assert_eq(player.weapons[0], held, "nothing cast")
	var stopped: Vector2 = player.position
	_pump(0.3)
	assert_eq(player.position, stopped, "server must not repeat pre-pause movement")
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	clients.append(view.client)
	screens.append(view)
	view.input.move_press(stopped + Vector2(2.0, 0.0))
	_pump(0.3)
	assert_ne(player.position, stopped, "fresh input works without rebuilding the connection")


func test_a_friend_joins_by_scanning_the_host_room_qr() -> void:
	var host: LobbyScreen = _lobby(LobbyScreen.Intent.CREATE)
	host.set_fields(URL, "Ana")
	host.submit()
	assert_true(_until(func() -> bool: return host.in_room()))
	var card: MenuDialog = host._room_view.show_qr()
	var shown: QrCode = (card.body.get_node("QrView") as QrView).code
	card.close(false)
	var guest: LobbyScreen = _lobby(LobbyScreen.Intent.JOIN)
	guest.set_fields(URL, "Ben")
	var camera: QrCamera = QrCamera.new()
	camera.picture = QR_PICTURE.draw(shown, QrScanSheet.LENS_SIDE, 0.6, -15.0, 0.05, 0.4)
	guest.scan_camera = camera
	var scanner: MenuDialog = guest.open_scanner()
	assert_true((scanner.body.get_node("QrScan") as QrScanSheet).read_now(), "the host's QR is read")
	assert_true(_until(func() -> bool: return guest.in_room()), "and the friend is in the room, no typing")
	assert_eq(guest.client.room["code"], host.client.room["code"])


## A camera that sees `picture`, always allowed.
class QrCamera extends IdCamera:
	var picture: Image
	var on: bool = false

	func has_permission() -> bool:
		return true

	func find(_tree: SceneTree) -> void:
		feeds_found.emit(true)

	func open(_lens: CanvasItem) -> bool:
		on = true
		return true

	func has_picture() -> bool:
		return on

	func close() -> void:
		on = false

	func capture(_lens: SubViewport) -> Image:
		return picture
