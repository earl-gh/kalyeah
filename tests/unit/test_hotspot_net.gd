extends GutTest
## The rule that says whether a phone hosts a hotspot or sits on a Wi-Fi, from its interface list (pure functions).

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const Phones := preload("res://tests/unit/hotspot_phones.gd")


func _verdict(interfaces: Array) -> Array[bool]:
	return [HotspotNet.hotspot_on(interfaces, NET), HotspotNet.wifi_connected(interfaces, NET)]


func test_a_phone_on_an_ordinary_wifi_is_not_a_hotspot_host() -> void:
	assert_eq(_verdict(Phones.on_home_wifi()), [false, true])
	assert_eq(_verdict(Phones.guest_on_a_hotspot()), [false, true], "a 192.168.43.x address on wlan0 is a guest, not a host")
	assert_eq(_verdict(Phones.vpn_over_wifi()), [false, true], "a VPN's private address is not a hotspot")


func test_mobile_data_and_wifi_direct_are_neither_a_hotspot_nor_a_wifi() -> void:
	assert_eq(_verdict(Phones.on_mobile_data_only()), [false, false], "carriers hand out private 10.x addresses")
	assert_eq(_verdict(Phones.wifi_direct_only()), [false, false])
	assert_eq(_verdict(Phones.wifi_without_address()), [false, false], "link-local only: no address from the router yet")
	assert_eq(_verdict(Phones.offline()), [false, false])
	assert_eq(_verdict(Phones.REAL_CONTAINER), [false, false], "a real interface list: eth0 holds a documentation address, lo is ignored")


func test_every_vendor_access_point_name_counts_as_a_hotspot() -> void:
	for phone: Array in [Phones.stock_hotspot(), Phones.samsung_hotspot(), Phones.mediatek_hotspot(), Phones.qualcomm_softap_hotspot(),
			Phones.ten_net_hotspot(), Phones.unknown_vendor_hotspot(), Phones.usb_tether(), Phones.bluetooth_tether()]:
		assert_eq(_verdict(phone), [true, false], str(phone))


func test_a_hotspot_while_on_a_wifi_is_both() -> void:
	assert_eq(_verdict(Phones.hotspot_and_wifi()), [true, true])
	assert_eq(HotspotNet.addresses_of(Phones.hotspot_and_wifi(), HotspotNet.Role.CLIENT, NET), PackedStringArray(["192.168.1.5"]), "only the Wi-Fi address is searched from")


func test_the_roles_come_from_the_data() -> void:
	assert_eq(HotspotNet.role_of("wlan0", NET), HotspotNet.Role.CLIENT)
	assert_eq(HotspotNet.role_of("rmnet_data3", NET), HotspotNet.Role.IGNORED)
	assert_eq(HotspotNet.role_of("v4-rmnet_data0", NET), HotspotNet.Role.IGNORED)
	assert_eq(HotspotNet.role_of("wlan1", NET), HotspotNet.Role.TETHER)
	assert_eq(HotspotNet.role_of("swlan0", NET), HotspotNet.Role.TETHER)


func test_only_private_addresses_count() -> void:
	for ip: String in ["10.0.0.7", "172.16.0.1", "172.31.255.254", "192.168.43.1"]:
		assert_true(HotspotNet.is_private(ip), ip)
	for ip: String in ["8.8.8.8", "172.32.0.1", "127.0.0.1", "192.169.0.1", "169.254.1.1", "abc", "1.2.3", "300.1.1.1", "fe80::1"]:
		assert_false(HotspotNet.is_private(ip), ip)


func test_the_search_leaves_by_the_wifi_address_to_both_broadcast_addresses() -> void:
	var plan: HotspotNet.Plan = HotspotNet.search_plan(Phones.guest_on_a_hotspot(), NET)
	assert_eq(plan.routes, [{"from": "192.168.43.57", "to": "255.255.255.255"}, {"from": "192.168.43.57", "to": "192.168.43.255"}])
	assert_eq(plan.beacon_port, NET.discovery_port)
	assert_eq(plan.ws_port, NET.default_port)
	assert_eq(plan.seconds, NET.discovery_seconds)


func test_the_fallback_is_the_dot_one_of_the_wifi_then_the_classic_tether_addresses() -> void:
	var plan: HotspotNet.Plan = HotspotNet.search_plan(Phones.on_home_wifi(), NET)
	assert_eq(plan.candidates[0], "ws://192.168.1.1:8080", "the router of the Wi-Fi we are on")
	assert_true(plan.candidates.has("ws://192.168.43.1:8080"))
	assert_true(plan.candidates.has("ws://172.20.10.1:8080"))
	assert_eq(HotspotNet.search_plan(Phones.guest_on_a_hotspot(), NET).candidates.size(), 2, "192.168.43.1 once, not twice")
	assert_false(HotspotNet.search_plan(Phones.on_home_wifi(), NET).candidates.has("ws://10.58.2.1:8080"), "mobile data is not searched")


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


func _pump(server: GameServer, clients: Array[GameClient], done: Callable, limit: float = 4.0) -> void:
	var waited: float = 0.0
	while waited < limit and not done.call():
		for i: int in 3:
			server.poll(1.0 / 60.0)
			for client: GameClient in clients:
				client.poll(1.0 / 60.0)
			OS.delay_usec(2000)
		waited += 0.02
