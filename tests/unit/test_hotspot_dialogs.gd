extends GutTest
## The two modals in front of a hotspot room, in every state: what they say, what they offer, and that nothing runs or calls
## back once they are closed. Create only goes on when a hotspot is on; Join checks the Wi-Fi, then finds a real room.

const NET: NetRules = preload("res://data/rules/net_rules.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const Phones := preload("res://tests/unit/hotspot_phones.gd")
const WS_PORT: int = 18981
const UDP_PORT: int = 18982
const SEARCH_SECONDS: float = 1.0

## The phone the dialogs read; a test swaps it to turn the hotspot on or connect the Wi-Fi.
var phone: Array = []
var _server: GameServer
var _beacon: HotspotBeacon


func before_each() -> void:
	phone = Phones.on_home_wifi()


func after_each() -> void:
	if _server != null:
		_server.stop()
		_server = null
	if _beacon != null:
		_beacon.stop()
		_beacon = null


func _interfaces() -> Array:
	return phone


func _open(card: MenuDialog) -> MenuDialog:
	var host: Control = autofree(Control.new()) as Control
	add_child(host)
	card.open(host)
	return card


func _text(card: MenuDialog, node_name: String) -> String:
	return (card.find_child(node_name, true, false) as Label).text


func _shown(card: MenuDialog, node_name: String) -> bool:
	var node: Control = card.find_child(node_name, true, false) as Control
	return node != null and node.is_visible_in_tree()


func _create(went: Array, open_settings: Callable = Callable()) -> MenuDialog:
	return _open(HotspotDialogs.create_check(func() -> void: went.append(true), _interfaces, open_settings))


func _join(found: Array) -> MenuDialog:
	return _open(HotspotDialogs.join_check(func(url: String) -> void: found.append(url), _interfaces, _loopback_plan))


func _loopback_plan(_interfaces_seen: Array) -> HotspotNet.Plan:
	var plan: HotspotNet.Plan = HotspotNet.Plan.new()
	plan.ws_port = WS_PORT
	plan.beacon_port = UDP_PORT
	plan.seconds = SEARCH_SECONDS
	plan.interval = 0.3
	plan.routes.append({"from": "127.0.0.1", "to": HotspotNet.LIMITED_BROADCAST})
	return plan


func _host(room: bool) -> void:
	_server = GameServer.new(RULES, LAYOUT, NET, 4)
	_server.auto_join = true
	assert_eq(_server.listen(WS_PORT), OK)
	_beacon = HotspotBeacon.new()
	assert_eq(_beacon.listen(UDP_PORT, NET.discovery_burst), OK)
	if room:
		_server.rooms.create_room(700, "Host", 1)


## Frames until `done` (the host is polled in between), at most `seconds`.
func _until(done: Callable, seconds: float = 4.0) -> bool:
	var waited: float = 0.0
	while waited < seconds and not done.call():
		if _server != null:
			_server.poll(0.016)
			_beacon.poll()
		await wait_process_frames(1)
		waited += 0.016
	return done.call()


# ---- Create ------------------------------------------------------------------------------------

func test_create_shows_checking_first_and_cancel_is_always_there() -> void:
	var went: Array = []
	var card: MenuDialog = _create(went)
	assert_eq(_text(card, "Message"), "Checking your network…")
	assert_true(_shown(card, "Cancel"))
	assert_false(_shown(card, "TryAgain"))
	assert_true(went.is_empty())


func test_create_says_plainly_when_there_is_no_hotspot_and_no_wifi_and_never_goes_on() -> void:
	for off: Array in [Phones.on_mobile_data_only(), Phones.offline(), Phones.wifi_without_address(), Phones.wifi_direct_only()]:
		phone = off
		var went: Array = []
		var card: MenuDialog = _create(went)
		await wait_process_frames(3)
		assert_eq(_text(card, "Message"), "Turn on your hotspot or connect to Wi-Fi, then tap Try again.", str(off))
		assert_true(_shown(card, "TryAgain"))
		assert_true(_shown(card, "Cancel"))
		assert_true(went.is_empty(), "no room without a hotspot or a Wi-Fi")
		assert_true(card.is_open)
		card.close(false)


func test_create_goes_on_by_itself_on_a_hotspot_or_on_a_wifi() -> void:
	for ok: Array in [Phones.stock_hotspot(), Phones.samsung_hotspot(), Phones.unknown_vendor_hotspot(), Phones.hotspot_and_wifi(), Phones.on_home_wifi(), Phones.guest_on_a_hotspot(), Phones.vpn_over_wifi()]:
		phone = ok
		var went: Array = []
		var card: MenuDialog = _create(went)
		await wait_process_frames(3)
		assert_eq(went.size(), 1, "the host has two ways to make a room, a hotspot or a Wi-Fi: %s" % str(ok))
		assert_false(is_instance_valid(card) and card.is_open, "and the modal closes")


func test_the_modals_never_show_hardware_or_network_details_to_the_player() -> void:
	var ip: RegEx = RegEx.create_from_string("\\d+\\.\\d+\\.\\d+\\.\\d+")
	# Create stays open (and so can be read) only without a network; Join is read on every kind of phone
	for combo: Array in [["create", Phones.on_mobile_data_only()], ["create", Phones.offline()], ["join", Phones.on_home_wifi()], ["join", Phones.guest_on_a_hotspot()], ["join", Phones.on_mobile_data_only()], ["join", Phones.offline()]]:
		var mode: String = combo[0] as String
		phone = combo[1] as Array
		var card: MenuDialog = _create([]) if mode == "create" else _join([])
		await wait_process_frames(3)
		assert_not_null(card.find_child("Message", true, false), "%s: the modal is open" % mode)
		assert_null(card.find_child("Detail", true, false), "%s: no detail line" % mode)
		for label: Node in card.find_children("*", "Label", true, false):
			var text: String = (label as Label).text
			assert_false(text.contains("wlan") or text.contains("rmnet") or text.contains("This phone") or text.contains("Asked"), "%s: no interface names: %s" % [mode, text])
			assert_null(ip.search(text), "%s: no IP address: %s" % [mode, text])
		card.close(false)


func test_try_again_looks_again_and_goes_on_once_a_network_is_there() -> void:
	phone = Phones.offline()
	var went: Array = []
	var card: MenuDialog = _create(went)
	await wait_process_frames(3)
	assert_true(went.is_empty())
	phone = Phones.stock_hotspot()
	(card.find_child("TryAgain", true, false) as Button).pressed.emit()
	assert_eq(_text(card, "Message"), "Checking your network…", "it says it is looking again")
	await wait_process_frames(3)
	assert_eq(went.size(), 1)


func test_coming_back_from_the_phones_settings_checks_again_by_itself() -> void:
	phone = Phones.offline()
	var went: Array = []
	var card: MenuDialog = _create(went)
	await wait_process_frames(3)
	phone = Phones.samsung_hotspot()
	card.propagate_notification(NOTIFICATION_APPLICATION_RESUMED)
	await wait_process_frames(3)
	assert_eq(went.size(), 1)


func test_resuming_while_there_is_still_no_network_keeps_the_dialog_open() -> void:
	phone = Phones.offline()
	var went: Array = []
	var card: MenuDialog = _create(went)
	await wait_process_frames(3)
	card.propagate_notification(NOTIFICATION_APPLICATION_RESUMED)
	await wait_process_frames(3)
	assert_true(went.is_empty())
	assert_eq(_text(card, "Message"), "Turn on your hotspot or connect to Wi-Fi, then tap Try again.")


func test_the_settings_button_is_offered_only_when_off_and_where_android_can_open_them() -> void:
	phone = Phones.offline()
	var opened: Array = []
	var card: MenuDialog = _create([], func() -> void: opened.append(true))
	assert_false(_shown(card, "Settings"), "not while checking")
	await wait_process_frames(3)
	assert_true(_shown(card, "Settings"))
	(card.find_child("Settings", true, false) as Button).pressed.emit()
	assert_eq(opened.size(), 1)
	card.close(false)
	var plain: MenuDialog = _create([])
	await wait_process_frames(3)
	assert_false(HotspotSettings.available(), "this build is not Android")
	assert_false(_shown(plain, "Settings"), "no button that does nothing")
	assert_false(HotspotSettings.open(), "and opening them off a phone is a quiet no")


func test_canceling_create_destroys_the_pending_check_safely() -> void:
	phone = Phones.offline()
	var went: Array = []
	var card: MenuDialog = _create(went)
	card.close(false)
	phone = Phones.stock_hotspot()
	await wait_process_frames(5)
	assert_true(went.is_empty(), "cancel cannot open a room later")


func test_cancel_and_back_both_close_the_no_network_dialog() -> void:
	phone = Phones.offline()
	var first: MenuDialog = _create([])
	await wait_process_frames(3)
	(first.find_child("Cancel", true, false) as Button).pressed.emit()
	assert_false(first.is_open)
	var second: MenuDialog = _create([])
	await wait_process_frames(3)
	second.notification(NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_false(second.is_open)


# ---- Join --------------------------------------------------------------------------------------

func test_join_without_a_wifi_says_to_connect_and_does_not_search() -> void:
	for lonely: Array in [Phones.stock_hotspot(), Phones.on_mobile_data_only(), Phones.wifi_without_address(), Phones.offline()]:
		phone = lonely
		var found: Array = []
		var card: MenuDialog = _join(found)
		assert_eq(_text(card, "Message"), "Checking your Wi-Fi…")
		await wait_process_frames(3)
		assert_eq(_text(card, "Message"), "Connect to the same Wi-Fi or hotspot as your friend, then tap Try again.", str(lonely))
		assert_true(_shown(card, "TryAgain"))
		assert_true(_shown(card, "Cancel"))
		assert_false(_shown(card, "Settings"), "that button is for Create")
		assert_true(found.is_empty())
		card.close(false)


func test_join_looks_for_a_room_and_joins_the_verified_one() -> void:
	_host(true)
	var found: Array = []
	var card: MenuDialog = _join(found)
	await wait_process_frames(2)
	assert_eq(_text(card, "Message"), "Looking for a room…")
	assert_true(_shown(card, "Cancel"))
	assert_false(_shown(card, "TryAgain"))
	assert_true(await _until(func() -> bool: return not found.is_empty()))
	assert_eq(found, ["ws://127.0.0.1:%d" % WS_PORT])
	assert_false(is_instance_valid(card) and card.is_open)


func test_join_says_when_there_is_no_room_and_try_again_finds_it_once_the_host_creates_it() -> void:
	_host(false)
	var found: Array = []
	var card: MenuDialog = _join(found)
	assert_true(await _until(func() -> bool: return _shown(card, "TryAgain")))
	assert_eq(_text(card, "Message"), HotspotFinder.NO_ROOM)
	assert_true(found.is_empty())
	_server.rooms.create_room(700, "Host", 1)
	(card.find_child("TryAgain", true, false) as Button).pressed.emit()
	assert_true(await _until(func() -> bool: return not found.is_empty()))
	assert_eq(found.size(), 1)


func test_join_with_no_host_at_all_ends_with_no_room_instead_of_hanging() -> void:
	var found: Array = []
	var card: MenuDialog = _join(found)
	assert_true(await _until(func() -> bool: return _shown(card, "TryAgain"), SEARCH_SECONDS + 2.0))
	assert_eq(_text(card, "Message"), HotspotFinder.NO_ROOM)
	assert_true(found.is_empty())
	assert_true(card.is_open, "it waits for Try again or Cancel")


func test_join_says_when_the_room_is_full_or_the_match_started() -> void:
	_host(true)
	var room: RoomService.Room = _server.rooms.rooms.values()[0] as RoomService.Room
	_server.rooms.join(room.code, 701, "Friend")
	var card: MenuDialog = _join([])
	assert_true(await _until(func() -> bool: return _shown(card, "TryAgain")))
	assert_eq(_text(card, "Message"), "That room is full!")
	card.close(false)
	room.state = RoomService.State.PLAYING
	var again: MenuDialog = _join([])
	assert_true(await _until(func() -> bool: return _shown(again, "TryAgain")))
	assert_eq(_text(again, "Message"), "That battle has already started!")


func test_canceling_join_while_searching_stops_the_search_and_never_calls_back() -> void:
	_host(true)
	var found: Array = []
	var card: MenuDialog = _join(found)
	await wait_process_frames(2)
	(card.find_child("Cancel", true, false) as Button).pressed.emit()
	assert_false(card.is_open)
	await _until(func() -> bool: return false, 1.5)
	assert_true(found.is_empty(), "a room found after Cancel is not joined")


func test_closing_join_before_the_check_runs_never_calls_back() -> void:
	_host(true)
	var found: Array = []
	var card: MenuDialog = _join(found)
	card.close(false)
	await _until(func() -> bool: return false, 1.0)
	assert_true(found.is_empty())


func test_coming_back_from_wifi_settings_searches_again_by_itself() -> void:
	_host(true)
	phone = Phones.offline()
	var found: Array = []
	var card: MenuDialog = _join(found)
	await wait_process_frames(3)
	assert_true(found.is_empty())
	phone = Phones.on_home_wifi()
	card.propagate_notification(NOTIFICATION_APPLICATION_RESUMED)
	assert_true(await _until(func() -> bool: return not found.is_empty()))


# ---- the flow without a widget -----------------------------------------------------------------

func test_the_flow_walks_the_states_in_order_and_says_nothing_after_it_is_cancelled() -> void:
	phone = Phones.offline()
	var steps: Array = []
	var flow: HotspotFlow = HotspotFlow.new(HotspotFlow.Mode.CREATE, _interfaces)
	flow.step_changed.connect(func(step: HotspotFlow.Step) -> void: steps.append(step))
	flow.begin()
	flow.tick(0.016)
	assert_eq(steps, [HotspotFlow.Step.CHECKING, HotspotFlow.Step.NO_NETWORK])
	phone = Phones.stock_hotspot()
	flow.cancel()
	flow.begin()
	flow.tick(0.016)
	flow.resumed()
	assert_eq(steps.size(), 2, "nothing more after cancel")


func test_the_live_phone_is_read_when_no_interfaces_are_given() -> void:
	var flow: HotspotFlow = HotspotFlow.new(HotspotFlow.Mode.CREATE)
	flow.begin()
	flow.tick(0.016)
	assert_true(flow.step in [HotspotFlow.Step.NO_NETWORK, HotspotFlow.Step.NETWORK_OK], "the real interface list gave a verdict")
	flow.cancel()
	var joining: HotspotFlow = HotspotFlow.new(HotspotFlow.Mode.JOIN)
	joining.begin()
	joining.tick(0.016)
	assert_true(joining.step in [HotspotFlow.Step.NO_WIFI, HotspotFlow.Step.SEARCHING, HotspotFlow.Step.NO_ROOM], "so did Join")
	joining.cancel()
