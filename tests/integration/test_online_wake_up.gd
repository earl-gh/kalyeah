extends GutTest
## A free Online server goes to sleep and takes about a minute to wake (Render's free tier). An Online connection waits
## `NetRules.connect_timeout` for it instead of Godot's 3 s, the lobby says the server is waking up after `wake_hint_after`, and a
## connection that never opens ends with "Can't reach the server" and Retry instead of "Connecting…" for ever.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const LOBBY: PackedScene = preload("res://scenes/ui/lobby.tscn")
## Accepts the TCP connection and never answers the WebSocket handshake: a server still waking up.
const SILENT_PORT: int = 18941
## Nothing listens here.
const CLOSED_PORT: int = 18942
const FRAME: float = 1.0 / 60.0

var _silent: TCPServer
var _held: Array[StreamPeerTCP] = []
var _saved_url: String


func before_each() -> void:
	_saved_url = Settings.server_url
	_silent = TCPServer.new()
	assert_eq(_silent.listen(SILENT_PORT, "127.0.0.1"), OK)
	_held.clear()


func after_each() -> void:
	Settings.server_url = _saved_url
	_silent.stop()
	_held.clear()


## `net`: the rules the lobby's client runs on (a copy with a shorter hint wait, so the test does not wait 4 s).
func _lobby(net: NetRules = NET) -> LobbyScreen:
	var lobby: LobbyScreen = autofree(LOBBY.instantiate()) as LobbyScreen
	lobby.client = GameClient.new(RULES, LAYOUT, net)
	lobby.enter_match_on_start = false
	lobby.set_intent(LobbyScreen.Intent.CREATE)
	add_child(lobby)
	return lobby


func _pump(lobby: LobbyScreen, seconds: float) -> void:
	for i: int in ceili(seconds / FRAME):
		if _silent.is_connection_available():
			_held.append(_silent.take_connection())  # keep it open, say nothing
		lobby.client.poll(FRAME)
		lobby._process(FRAME)
		OS.delay_usec(1000)


func test_an_online_connection_waits_for_a_server_to_wake_and_hotspot_keeps_the_short_wait() -> void:
	assert_gte(NET.connect_timeout, 60.0, "a sleeping free server takes about a minute")
	var client: GameClient = GameClient.new(RULES, LAYOUT, NET)
	client.connect_to("ws://127.0.0.1:%d" % SILENT_PORT, true)
	assert_eq(client._peer.handshake_timeout, NET.connect_timeout)
	client.connect_to("ws://127.0.0.1:%d" % SILENT_PORT)
	assert_eq(client._peer.handshake_timeout, WebSocketMultiplayerPeer.new().handshake_timeout, "Hotspot: Godot's default")
	client.close()


func test_the_lobby_says_the_server_is_waking_up_while_it_waits() -> void:
	var quick: NetRules = NET.duplicate() as NetRules
	quick.wake_hint_after = 0.3
	var lobby: LobbyScreen = _lobby(quick)
	lobby.set_fields("ws://127.0.0.1:%d" % SILENT_PORT, "Ana")
	lobby.submit()
	_pump(lobby, 0.1)
	assert_eq(lobby._status.text, "Connecting…")
	_pump(lobby, 0.5)
	assert_true(lobby.client.is_connecting(), "still waiting for the server, well past Godot's own 3 s soon")
	assert_eq(lobby._status.text, LobbyScreen.WAKING_TEXT)
	lobby.client.close()


func test_a_connection_that_never_opens_says_so_with_a_retry() -> void:
	var lobby: LobbyScreen = _lobby()
	var failed: Array[bool] = []
	lobby.client.connect_failed.connect(func() -> void: failed.append(true))
	lobby.set_fields("ws://127.0.0.1:%d" % CLOSED_PORT, "Ana")
	lobby.submit()
	var waited: float = 0.0
	while failed.is_empty() and waited < 5.0:
		_pump(lobby, 0.1)
		waited += 0.1
	# macOS logs this engine error for a refused connection (Linux does not); it is the closed port, not the game
	for error: Variant in get_errors():
		if str(error.code).contains("TCP no delay") or str(error.rationale).contains("TCP no delay"):
			error.handled = true
	assert_eq(failed.size(), 1, "the client reports it")
	assert_string_contains(lobby._status.text, "Can't reach the server", "not Connecting… for ever")
