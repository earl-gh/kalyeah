class_name HotspotFinder
extends RefCounted
## Discovers a compatible phone host and verifies its room before reporting success.

const TIMEOUT: float = 3.0
const NO_HOST: String = "No host found. Connect to your friend's hotspot, then retry."
const MESSAGES: Dictionary[String, String] = {
	"no_room": "The host has no room. Ask them to tap Create, then retry.",
	"full": "The host's room is full.",
	"in_progress": "The host's match has already started.",
	"incompatible": "Update both phones to the same game version.",
	"not_hotspot": "No hotspot room found on this network.",
}

var url: String = ""
var failed: bool = false
var failure_message: String = NO_HOST
var _probes: Dictionary[String, WebSocketMultiplayerPeer] = {}
var _sent: Dictionary[String, bool] = {}
var _left: float = TIMEOUT


func start(candidates: PackedStringArray) -> void:
	stop()
	url = ""
	failed = false
	failure_message = NO_HOST
	_left = TIMEOUT
	for candidate: String in candidates:
		var probe: WebSocketMultiplayerPeer = WebSocketMultiplayerPeer.new()
		if probe.create_client(candidate) == OK:
			_probes[candidate] = probe
	if _probes.is_empty():
		failed = true


func is_searching() -> bool:
	return not _probes.is_empty()


func poll(delta: float) -> void:
	if _probes.is_empty():
		return
	_left -= delta
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
					stop()
					return
				failure_message = MESSAGES.get(status, MESSAGES["incompatible"]) as String
				probe.close()
		if probe.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			_probes.erase(candidate)
	if _probes.is_empty() or _left <= 0.0:
		stop()
		failed = url.is_empty()


func stop() -> void:
	for candidate: String in _probes:
		_probes[candidate].close()
	_probes.clear()
	_sent.clear()
