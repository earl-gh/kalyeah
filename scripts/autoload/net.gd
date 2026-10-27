extends Node
## Network connection for the whole app: owns the GameClient and polls it every
## frame so it survives scene changes (lobby -> loading -> match).

signal connected
signal disconnected

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")
const CLOSE_GRACE: float = 0.5

var client: GameClient = GameClient.new(RULES, LAYOUT, NET_RULES)
var is_online: bool = false
## The server this phone runs for a hotspot room it hosts (Android only), or null.
var host: GameServer
## Answers the guests' "who is hosting?" probe while `host` runs (UDP, no room details).
var beacon: HotspotBeacon = HotspotBeacon.new()
## A host that just stopped keeps running for `CLOSE_GRACE` seconds, so its ROOM_CLOSED reaches the players.
var _closing: GameServer
var _closing_left: float = 0.0


func _ready() -> void:
	client.connection_changed.connect(_on_connection_changed)


func _process(delta: float) -> void:
	client.poll(delta)
	if host != null:
		host.poll(delta)
		beacon.poll()
	if _closing != null:
		_closing.poll(delta)
		_closing_left -= delta
		if _closing_left <= 0.0:
			_finish_closing()


## Starts a room server on this phone for friends on its hotspot or Wi-Fi: they find it by
## themselves and need no code. A beacon that cannot start (its port is taken) only costs the broadcast:
## guests can still find the room at the fallback addresses.
func start_host() -> Error:
	stop_host()
	# the port is needed again now
	_finish_closing()
	host = GameServer.new(RULES, LAYOUT, NET_RULES, Time.get_ticks_usec())
	host.auto_join = true
	var error: Error = host.listen(NET_RULES.default_port)
	if error != OK:
		host = null
		return error
	beacon.listen(NET_RULES.discovery_port, NET_RULES.discovery_burst)
	return OK


func stop_host() -> void:
	beacon.stop()
	if host != null:
		_finish_closing()
		host.close_rooms()
		_closing = host
		_closing_left = CLOSE_GRACE
		host = null


func _finish_closing() -> void:
	if _closing != null:
		_closing.stop()
		_closing = null


## Where this phone's own client connects to reach the server it hosts.
func host_url() -> String:
	return "ws://127.0.0.1:%d" % NET_RULES.default_port


func _on_connection_changed(online: bool) -> void:
	is_online = online
	if online:
		connected.emit()
	else:
		disconnected.emit()
