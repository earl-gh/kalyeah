extends GutTest
## A real host and a real guest on loopback, through the real finder: the guest finds the room with no address, no code and
## no help, and every way it can fail ends with the right words. (Loopback has no router: what only a phone shows is
## the Wi-Fi's broadcast filtering and the access point's own address; see docs/audits/2026-10-26-hotspot.md.)

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const FRAME: float = 1.0 / 60.0
const LOOPBACK: String = "127.0.0.1"
const SEARCH_SECONDS: float = 3.0

var _servers: Array[GameServer] = []
var _beacons: Array[HotspotBeacon] = []
var _finder: HotspotFinder


func before_each() -> void:
	_finder = HotspotFinder.new()


func after_each() -> void:
	_finder.stop()
	for server: GameServer in _servers:
		server.stop()
	for beacon: HotspotBeacon in _beacons:
		beacon.stop()
	_servers.clear()
	_beacons.clear()


## A host like the one `Net.start_host` runs: the room server and its beacon. `room` makes it a host with a room.
func _host(ws_port: int, udp_port: int, room: bool = true) -> GameServer:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 4)
	server.auto_join = true
	assert_eq(server.listen(ws_port), OK)
	var beacon: HotspotBeacon = HotspotBeacon.new()
	assert_eq(beacon.listen(udp_port, NET.discovery_burst), OK)
	if room:
		server.rooms.create_room(700, "Host", 1)
	_servers.append(server)
	_beacons.append(beacon)
	return server


func _plan(ws_port: int, udp_port: int, routes: Array[String], candidates: Array[String] = []) -> HotspotNet.Plan:
	var plan: HotspotNet.Plan = HotspotNet.Plan.new()
	plan.ws_port = ws_port
	plan.beacon_port = udp_port
	plan.seconds = SEARCH_SECONDS
	plan.interval = 0.5
	for target: String in routes:
		plan.routes.append({"from": LOOPBACK, "to": target})
	for candidate: String in candidates:
		plan.candidates.append(candidate)
	return plan


## Runs the host(s) and the finder until the finder is done; returns the frames it took.
func _search(plan: HotspotNet.Plan) -> int:
	_finder.start(plan)
	var frames: int = 0
	while _finder.is_searching() and frames < 1200:
		for server: GameServer in _servers:
			server.poll(FRAME)
		for beacon: HotspotBeacon in _beacons:
			beacon.poll()
		_finder.poll(FRAME)
		frames += 1
		OS.delay_usec(4000)
	return frames


func test_a_guest_finds_the_room_by_the_limited_broadcast_with_no_address_to_go_on() -> void:
	_host(18951, 18961)
	_search(_plan(18951, 18961, [HotspotNet.LIMITED_BROADCAST]))
	assert_eq(_finder.url, "ws://127.0.0.1:18951", "found by the answer to the broadcast alone: the plan had no candidate")
	assert_false(_finder.failed)
	assert_eq(_finder.heard, 1)


func test_a_guest_finds_the_room_by_the_networks_directed_broadcast() -> void:
	_host(18952, 18962)
	_search(_plan(18952, 18962, ["127.255.255.255"]))
	assert_eq(_finder.url, "ws://127.0.0.1:18952")


func test_the_fallback_addresses_still_find_a_host_that_does_not_answer_the_broadcast() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 4)
	server.auto_join = true
	assert_eq(server.listen(18953), OK)
	server.rooms.create_room(700, "Host", 1)
	_servers.append(server)
	_search(_plan(18953, 18963, [HotspotNet.LIMITED_BROADCAST], ["ws://127.0.0.1:18999", "ws://127.0.0.1:18953"]))
	assert_eq(_finder.url, "ws://127.0.0.1:18953", "no beacon: the second candidate answered")
	assert_eq(_finder.heard, 0)


func test_the_guest_then_joins_the_found_room_with_no_code() -> void:
	var server: GameServer = _host(18954, 18964, false)
	var host_client: GameClient = GameClient.new(RULES, LAYOUT, NET)
	var guest: GameClient = GameClient.new(RULES, LAYOUT, NET)
	assert_eq(host_client.connect_to("ws://127.0.0.1:18954"), OK)
	_pump(server, [host_client, guest], func() -> bool: return host_client.is_online())
	host_client.create_room(1)
	_pump(server, [host_client, guest], func() -> bool: return not host_client.room.is_empty())
	_search(_plan(18954, 18964, [HotspotNet.LIMITED_BROADCAST]))
	assert_eq(_finder.url, "ws://127.0.0.1:18954")
	assert_eq(guest.connect_to(_finder.url), OK)
	_pump(server, [host_client, guest], func() -> bool: return guest.is_online())
	guest.join_room("")
	_pump(server, [host_client, guest], func() -> bool: return (guest.room.get("members", []) as Array).size() == 2)
	assert_eq((guest.room.get("members", []) as Array).size(), 2, "the guest is in the host's room")
	assert_eq(guest.room.get("code", ""), host_client.room.get("code", ""))
	host_client.close()
	guest.close()


func test_no_host_at_all_ends_by_itself_with_the_no_room_text() -> void:
	var frames: int = _search(_plan(18955, 18965, [HotspotNet.LIMITED_BROADCAST], ["ws://127.0.0.1:18955"]))
	assert_true(_finder.failed)
	assert_eq(_finder.url, "")
	assert_eq(_finder.failure_message, HotspotFinder.NO_ROOM)
	assert_lt(frames, int(SEARCH_SECONDS / FRAME) + 5, "it ended at its deadline, it did not hang")
	assert_eq(_finder.asked, 1)


func test_a_host_without_a_room_is_not_a_success_and_ends_early() -> void:
	_host(18956, 18966, false)
	var frames: int = _search(_plan(18956, 18966, [HotspotNet.LIMITED_BROADCAST]))
	assert_true(_finder.failed)
	assert_eq(_finder.url, "")
	assert_eq(_finder.failure_message, HotspotFinder.NO_ROOM)
	assert_lt(frames, int(SEARCH_SECONDS / FRAME), "the host answered, so there is nothing more to wait for")


func test_a_full_room_and_a_started_match_say_so() -> void:
	var server: GameServer = _host(18957, 18967)
	var room: RoomService.Room = server.rooms.rooms.values()[0] as RoomService.Room
	server.rooms.join(room.code, 701, "Friend")
	_search(_plan(18957, 18967, [HotspotNet.LIMITED_BROADCAST]))
	assert_true(_finder.failed)
	assert_eq(_finder.failure_message, "That room is full!")
	room.state = RoomService.State.PLAYING
	_search(_plan(18957, 18967, [HotspotNet.LIMITED_BROADCAST]))
	assert_eq(_finder.failure_message, "That battle has already started!")


func test_a_host_on_another_version_says_to_update() -> void:
	var stub: WebSocketMultiplayerPeer = WebSocketMultiplayerPeer.new()
	assert_eq(stub.create_server(18958), OK)
	_finder.start(_plan(18958, 18968, [], ["ws://127.0.0.1:18958"]))
	var frames: int = 0
	while _finder.is_searching() and frames < 1200:
		stub.poll()
		while stub.get_available_packet_count() > 0:
			var from: int = stub.get_packet_peer()
			stub.get_packet()
			stub.set_target_peer(from)
			stub.put_packet(NetProtocol.encode({"t": NetProtocol.Msg.ROOM_DISCOVERED, "game": "kalyeah", "v": NetProtocol.VERSION + 1, "status": "ready"}))
		_finder.poll(FRAME)
		frames += 1
		OS.delay_usec(4000)
	stub.close()
	assert_true(_finder.failed)
	assert_eq(_finder.failure_message, NetProtocol.INCOMPATIBLE_TEXT)
	assert_eq(_finder.url, "")


func test_a_host_that_stops_while_the_guest_is_looking_ends_with_no_room() -> void:
	var server: GameServer = _host(18959, 18969)
	_finder.start(_plan(18959, 18969, [HotspotNet.LIMITED_BROADCAST]))
	server.stop()
	_beacons[0].stop()
	var frames: int = 0
	while _finder.is_searching() and frames < 1200:
		_finder.poll(FRAME)
		frames += 1
		OS.delay_usec(4000)
	assert_true(_finder.failed)
	assert_eq(_finder.url, "")
	assert_eq(_finder.failure_message, HotspotFinder.NO_ROOM)


func test_with_two_hosts_the_guest_picks_the_one_with_a_ready_room_whichever_answers_first() -> void:
	_host(18970, 18971, false)
	_host(18972, 18973, true)
	var empty: String = "ws://127.0.0.1:18970"
	var ready: String = "ws://127.0.0.1:18972"
	for order: Array[String] in [[empty, ready] as Array[String], [ready, empty] as Array[String]]:
		_search(_plan(18970, 18971, [], order))
		assert_eq(_finder.url, ready, "the empty host is skipped: %s" % [order])


func test_a_host_that_leaves_and_creates_again_is_found_again() -> void:
	for round_number: int in 2:
		var server: GameServer = _host(18974, 18975)
		_search(_plan(18974, 18975, [HotspotNet.LIMITED_BROADCAST]))
		assert_eq(_finder.url, "ws://127.0.0.1:18974", "round %d" % round_number)
		server.stop()
		_beacons[0].stop()
		_servers.clear()
		_beacons.clear()


func test_the_phones_host_starts_stops_and_starts_again_on_the_same_ports() -> void:
	for round_number: int in 3:
		assert_eq(Net.start_host(), OK, "start %d" % round_number)
		assert_true(Net.beacon.is_listening())
		Net.stop_host()
		assert_false(Net.beacon.is_listening())
		assert_null(Net.host)


func test_the_beacon_answers_kalyeah_probes_only_and_never_more_than_its_burst() -> void:
	var beacon: HotspotBeacon = HotspotBeacon.new()
	assert_eq(beacon.listen(18976, 2, LOOPBACK), OK)
	_beacons.append(beacon)
	var sender: PacketPeerUDP = PacketPeerUDP.new()
	assert_eq(sender.bind(0, LOOPBACK), OK)
	sender.set_dest_address(LOOPBACK, 18976)
	sender.put_packet(PackedByteArray([1, 2, 3]))
	sender.put_packet(NetProtocol.encode({"t": NetProtocol.Msg.DISCOVER_ROOM, "game": "kalyeah"}))
	sender.put_packet("kalyeah?x".to_ascii_buffer())
	OS.delay_usec(50000)
	beacon.poll()
	beacon.poll()
	assert_eq(beacon.answered, 0, "junk, a lookalike and another message get no answer, and none is decoded")
	for i: int in 5:
		sender.put_packet(HotspotBeacon.PROBE.to_ascii_buffer())
	OS.delay_usec(50000)
	beacon.poll()
	assert_eq(beacon.answered, 2, "two probes this frame (the burst), the rest wait")
	beacon.poll()
	assert_eq(beacon.answered, 4)
	sender.close()


func _pump(server: GameServer, clients: Array[GameClient], done: Callable, limit: float = 4.0) -> void:
	var waited: float = 0.0
	while waited < limit and not done.call():
		for i: int in 3:
			server.poll(FRAME)
			for client: GameClient in clients:
				client.poll(FRAME)
			OS.delay_usec(2000)
		waited += 0.02
