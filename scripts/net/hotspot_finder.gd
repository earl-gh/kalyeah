class_name HotspotFinder
extends RefCounted
## Finds a phone that hosts a Kalyeah room on the Wi-Fi this phone is on, and verifies the room before it says so.
##
## Two things run side by side for `plan.seconds`: a UDP probe sent out of each Wi-Fi address to the broadcast addresses
## (a host answers from whatever address it has), and a WebSocket probe to the plan's fallback addresses. Every phone that
## answers the UDP probe is then asked for its room over the WebSocket (DISCOVER_ROOM): only a host whose room is `ready`
## becomes `url`. Poll it every frame; it never blocks and always ends (`url` set, or `failed`).

const NO_ROOM: String = "No room found. Ask your friend to tap Create room, then tap Try again."
const MESSAGES: Dictionary[String, String] = {
	"no_room": NO_ROOM,
	"full": "That room is full.",
	"in_progress": "That match has already started.",
	"incompatible": NetProtocol.INCOMPATIBLE_TEXT,
	"not_hotspot": NO_ROOM,
}

var url: String = ""
var failed: bool = false
var failure_message: String = NO_ROOM
## What the search did (tests read these): addresses asked over the WebSocket, and phones that answered the UDP probe. Never shown to the player.
var asked: int = 0
var heard: int = 0

var _plan: HotspotNet.Plan
var _searching: bool = false
var _answered: bool = false
var _left: float = 0.0
var _since_probe: float = 0.0
var _scouts: Array[Dictionary] = []
var _probes: Dictionary[String, WebSocketMultiplayerPeer] = {}
var _sent: Dictionary[String, bool] = {}
var _tried: Dictionary[String, bool] = {}
var _heard: Dictionary[String, bool] = {}


func start(plan: HotspotNet.Plan) -> void:
	stop()
	_plan = plan
	url = ""
	failed = false
	failure_message = NO_ROOM
	asked = 0
	heard = 0
	_answered = false
	_left = plan.seconds
	_since_probe = 0.0
	_tried.clear()
	_heard.clear()
	_open_scouts()
	for candidate: String in plan.candidates:
		_open_probe(candidate)
	_searching = true
	_send_probes()
	if _scouts.is_empty() and _probes.is_empty():
		_finish()


func is_searching() -> bool:
	return _searching


func poll(delta: float) -> void:
	if not _searching:
		return
	_left -= delta
	_since_probe += delta
	if _since_probe >= _plan.interval:
		_send_probes()
	_read_scouts()
	_read_probes()
	if not url.is_empty():
		stop()
	elif _left <= 0.0 or (_answered and _probes.is_empty()):
		_finish()


func stop() -> void:
	_searching = false
	for candidate: String in _probes:
		_probes[candidate].close()
	_probes.clear()
	_sent.clear()
	for scout: Dictionary in _scouts:
		(scout["peer"] as PacketPeerUDP).close()
	_scouts.clear()


func _finish() -> void:
	stop()
	failed = url.is_empty()


## One socket for each Wi-Fi address, bound to it so the broadcast leaves by that interface.
func _open_scouts() -> void:
	var routes: Dictionary[String, PackedStringArray] = {}
	for route: Dictionary in _plan.routes:
		var from: String = route["from"] as String
		if not routes.has(from):
			routes[from] = PackedStringArray()
		routes[from].append(route["to"] as String)
	for from: String in routes:
		var peer: PacketPeerUDP = PacketPeerUDP.new()
		peer.set_broadcast_enabled(true)
		if peer.bind(0, from) == OK:
			_scouts.append({"peer": peer, "to": routes[from]})


func _send_probes() -> void:
	_since_probe = 0.0
	var probe: PackedByteArray = HotspotBeacon.PROBE.to_ascii_buffer()
	for scout: Dictionary in _scouts:
		var peer: PacketPeerUDP = scout["peer"] as PacketPeerUDP
		for target: String in scout["to"] as PackedStringArray:
			if peer.set_dest_address(target, _plan.beacon_port) == OK:
				peer.put_packet(probe)


func _read_scouts() -> void:
	for scout: Dictionary in _scouts:
		var peer: PacketPeerUDP = scout["peer"] as PacketPeerUDP
		while peer.get_available_packet_count() > 0:
			var answer: PackedByteArray = peer.get_packet()
			var host: String = peer.get_packet_ip()
			if answer != HotspotBeacon.ANSWER.to_ascii_buffer() or _heard.has(host):
				continue
			_heard[host] = true
			heard = _heard.size()
			_open_probe("ws://%s:%d" % [host, _plan.ws_port])


func _open_probe(candidate: String) -> void:
	if _tried.has(candidate):
		return
	_tried[candidate] = true
	asked = _tried.size()
	var probe: WebSocketMultiplayerPeer = WebSocketMultiplayerPeer.new()
	if probe.create_client(candidate) == OK:
		_probes[candidate] = probe


func _read_probes() -> void:
	for candidate: String in _probes.keys():
		var probe: WebSocketMultiplayerPeer = _probes[candidate]
		probe.poll()
		if probe.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED:
			if not _sent.has(candidate):
				probe.set_target_peer(MultiplayerPeer.TARGET_PEER_SERVER)
				probe.put_packet(NetProtocol.encode({"t": NetProtocol.Msg.DISCOVER_ROOM, "game": "kalyeah", "v": NetProtocol.VERSION}))
				_sent[candidate] = true
			while probe.get_available_packet_count() > 0:
				var answer: Dictionary = NetProtocol.decode(probe.get_packet())
				if answer.get("t", -1) != NetProtocol.Msg.ROOM_DISCOVERED or answer.get("game", "") != "kalyeah":
					continue
				var status: String = "incompatible" if answer.get("v", -1) != NetProtocol.VERSION else String(answer.get("status", "incompatible"))
				if status == "ready":
					url = candidate
					return
				_answered = true
				failure_message = MESSAGES.get(status, MESSAGES["incompatible"]) as String
				probe.close()
		if probe.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			_probes.erase(candidate)
