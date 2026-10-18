extends GutTest

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")


func test_the_phone_picks_its_hotspot_address() -> void:
	assert_eq(HotspotNet.local_ip(PackedStringArray(["127.0.0.1", "fe80::1", "10.1.2.3", "192.168.43.1"])), "192.168.43.1", "the Android hotspot first")
	assert_eq(HotspotNet.local_ip(PackedStringArray(["192.168.1.5", "172.20.10.1"])), "172.20.10.1", "then the iPhone hotspot")
	assert_eq(HotspotNet.local_ip(PackedStringArray(["10.0.0.4", "192.168.1.5"])), "192.168.1.5")
	assert_eq(HotspotNet.local_ip(PackedStringArray(["127.0.0.1", "169.254.3.3", "8.8.8.8"])), "", "no hotspot or Wi-Fi")


func test_only_private_addresses_are_dialled() -> void:
	for ip: String in ["10.0.0.7", "172.16.0.1", "172.31.255.254", "192.168.43.1"]:
		assert_true(HotspotNet.is_private(ip), ip)
	for ip: String in ["8.8.8.8", "172.32.0.1", "127.0.0.1", "192.169.0.1", "abc", "1.2.3", "300.1.1.1"]:
		assert_false(HotspotNet.is_private(ip), ip)


func test_the_hosts_are_the_dot_one_of_each_network_then_the_hotspot_defaults() -> void:
	var found: PackedStringArray = HotspotNet.candidates(PackedStringArray(["192.168.1.37", "fe80::1", "8.8.8.8"]), 8080)
	assert_eq(found[0], "ws://192.168.1.1:8080", "the router of the Wi-Fi we are on")
	assert_true(found.has("ws://192.168.43.1:8080"))
	assert_true(found.has("ws://172.20.10.1:8080"))
	assert_eq(HotspotNet.candidates(PackedStringArray(["192.168.43.7"]), 8080).size(), 2, "no duplicates")


func test_a_hotspot_host_has_one_room_and_a_friend_joins_without_a_code() -> void:
	var host: GameServer = GameServer.new(RULES, LAYOUT, NET, 4)
	host.auto_join = true
	assert_eq(host.listen(18941), OK)
	var creator: GameClient = GameClient.new(RULES, LAYOUT, NET)
	var joiner: GameClient = GameClient.new(RULES, LAYOUT, NET)
	assert_eq(creator.connect_to("ws://127.0.0.1:18941"), OK)
	_pump(host, [creator, joiner], func() -> bool: return creator.is_online())
	creator.create_room(1)
	assert_eq(joiner.connect_to("ws://127.0.0.1:18941"), OK)
	_pump(host, [creator, joiner], func() -> bool: return joiner.is_online())
	joiner.join_room("")
	_pump(host, [creator, joiner], func() -> bool: return (joiner.room.get("members", []) as Array).size() == 2)
	assert_eq((joiner.room.get("members", []) as Array).size(), 2, "the friend is in the host's room")
	assert_eq(joiner.room.get("code", ""), creator.room.get("code", ""))
	creator.close()
	joiner.close()
	host.stop()


func test_an_online_server_never_joins_a_room_without_its_code() -> void:
	var server: GameServer = GameServer.new(RULES, LAYOUT, NET, 4)
	assert_eq(server.listen(18942), OK)
	var creator: GameClient = GameClient.new(RULES, LAYOUT, NET)
	var stranger: GameClient = GameClient.new(RULES, LAYOUT, NET)
	assert_eq(creator.connect_to("ws://127.0.0.1:18942"), OK)
	_pump(server, [creator, stranger], func() -> bool: return creator.is_online())
	creator.create_room(1)
	assert_eq(stranger.connect_to("ws://127.0.0.1:18942"), OK)
	_pump(server, [creator, stranger], func() -> bool: return stranger.is_online())
	stranger.join_room("")
	_pump(server, [creator, stranger], func() -> bool: return false, 0.5)
	assert_true(stranger.room.is_empty(), "no room without its code")
	creator.close()
	stranger.close()
	server.stop()


func test_the_finder_finds_the_room_server_among_dead_addresses() -> void:
	var host: GameServer = GameServer.new(RULES, LAYOUT, NET, 4)
	assert_eq(host.listen(18943), OK)
	host.auto_join = true
	host.rooms.create_room(700, "Host", 1)
	var finder: HotspotFinder = HotspotFinder.new()
	finder.start(PackedStringArray(["ws://127.0.0.1:18999", "ws://127.0.0.1:18943"]))
	for i: int in 300:
		host.poll(1.0 / 60.0)
		finder.poll(1.0 / 60.0)
		if not finder.url.is_empty():
			break
		OS.delay_usec(3000)
	assert_eq(finder.url, "ws://127.0.0.1:18943")
	finder.stop()
	host.stop()


func test_the_finder_gives_up_when_nothing_answers() -> void:
	var finder: HotspotFinder = HotspotFinder.new()
	finder.start(PackedStringArray(["ws://127.0.0.1:18998"]))
	for i: int in 400:
		finder.poll(1.0 / 60.0)
		if finder.failed:
			break
		OS.delay_usec(2000)
	assert_true(finder.failed)
	assert_eq(finder.url, "")


func _pump(server: GameServer, clients: Array[GameClient], done: Callable, limit: float = 4.0) -> void:
	var waited: float = 0.0
	while waited < limit and not done.call():
		for i: int in 3:
			server.poll(1.0 / 60.0)
			for client: GameClient in clients:
				client.poll(1.0 / 60.0)
			OS.delay_usec(2000)
		waited += 0.02
