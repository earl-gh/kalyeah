class_name HotspotBeacon
extends RefCounted
## The host side of finding a room without an address: a UDP socket that answers every Kalyeah probe with the same
## message, straight back to whoever sent it. The guest then asks that address for its room over the WebSocket
## (HotspotFinder), which is where the room's state, the full room and the other version are told apart. Nothing about
## the room is said here, so a stranger on the network learns only that a Kalyeah phone is there. The two messages are fixed
## text compared whole: nothing a stranger sends is ever decoded.

const PROBE: String = "kalyeah?"
const ANSWER: String = "kalyeah!"

var _socket: PacketPeerUDP
var _burst: int = 0
## Probes answered since `listen` (read by tests and by the failure line).
var answered: int = 0


## Starts answering on `port`. `address` is the interface to listen on: every IPv4 one by default.
func listen(port: int, burst: int, address: String = "0.0.0.0") -> Error:
	stop()
	_burst = burst
	answered = 0
	var socket: PacketPeerUDP = PacketPeerUDP.new()
	var error: Error = socket.bind(port, address)
	if error != OK:
		return error
	_socket = socket
	return OK


func is_listening() -> bool:
	return _socket != null


func poll() -> void:
	if _socket == null:
		return
	for i: int in _burst:
		if _socket.get_available_packet_count() <= 0:
			return
		if _socket.get_packet() != PROBE.to_ascii_buffer():
			continue
		_socket.set_dest_address(_socket.get_packet_ip(), _socket.get_packet_port())
		_socket.put_packet(ANSWER.to_ascii_buffer())
		answered += 1


func stop() -> void:
	if _socket != null:
		_socket.close()
		_socket = null
