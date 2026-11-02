extends GutTest
## Local play without the phone's settings (LocalLink, with a stand-in for the Android plugin): the host starts a game hotspot
## from the Create dialog and its room shows it as a Wi-Fi QR; a friend joins it from the Join dialog by Scan QR or an NFC tap,
## and the room search follows. Saying no, a phone that cannot, and a code that is not a hotspot all end somewhere clear.

const Phones := preload("res://tests/unit/hotspot_phones.gd")
const QR_PICTURE: GDScript = preload("res://tests/fixtures/qr_picture.gd")
const NETWORK: Dictionary = {"ssid": "AndroidShare_4821", "password": "kalye4h;play", "security": "WPA"}


## The plugin, without Android: it answers as told.
class FakeLink extends LocalLink:
	var allowed: bool = true
	var answer: bool = true
	var hotspot_answer: String = ""  # "" starts it, anything else is the failure reason
	var join_answer: String = ""  # "" joins it
	var nfc: String = "on"
	var asked: int = 0
	var joined: Array[Dictionary] = []
	var reading: bool = false
	var left: int = 0

	func available() -> bool:
		return true

	func has_permissions() -> bool:
		return allowed

	func request_permissions() -> void:
		asked += 1
		allowed = answer
		permissions_answered.emit(answer)

	func start_hotspot() -> void:
		if hotspot_answer.is_empty():
			_on_hotspot_started(NETWORK["ssid"], NETWORK["password"], NETWORK["security"])
		else:
			hotspot_failed.emit(hotspot_answer)

	func stop_hotspot() -> void:
		hotspot = {}

	func join_wifi(network: Dictionary) -> void:
		joined.append(network)
		if join_answer.is_empty():
			wifi_joined.emit()
		else:
			wifi_failed.emit(join_answer)

	func leave_wifi() -> void:
		left += 1

	func nfc_state() -> String:
		return nfc

	func nfc_read_on(on: bool) -> void:
		reading = on


## A camera that sees `picture`, always allowed.
class SeeingCamera extends IdCamera:
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


var phone: Array = []
var link: FakeLink
var _host: Control


func before_each() -> void:
	link = FakeLink.new()
	_host = autofree(Control.new()) as Control
	_host.size = Vector2(720.0, 1280.0)
	add_child(_host)


func _interfaces() -> Array:
	return phone


func _shown(card: MenuDialog, node_name: String) -> bool:
	var node: Control = card.find_child(node_name, true, false) as Control
	return node != null and node.is_visible_in_tree()


func _text(card: MenuDialog) -> String:
	return (card.find_child("Message", true, false) as Label).text


func _press(card: MenuDialog, node_name: String) -> void:
	(card.find_child(node_name, true, false) as Button).pressed.emit()


func _tick(card: MenuDialog, frames: int = 2) -> void:
	var driver: HotspotDialogs.Driver = card.find_children("*", "Node", true, false).filter(
		func(node: Node) -> bool: return node is HotspotDialogs.Driver)[0] as HotspotDialogs.Driver
	for _i: int in frames:
		driver.flow.tick(1.0 / 30.0)


func _create(went: Array) -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.create_check(func() -> void: went.append(true), _interfaces, func() -> void: pass, link)
	card.open(_host)
	_tick(card)
	return card


func _join(camera: IdCamera = null) -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.join_check(func(_url: String) -> void: pass, _interfaces, _no_search, link, camera)
	card.open(_host)
	_tick(card)
	return card


## Nowhere to search (the search itself is test_hotspot_dialogs' job): it ends with "no room" at once.
func _no_search(_seen: Array) -> HotspotNet.Plan:
	var plan: HotspotNet.Plan = HotspotNet.Plan.new()
	plan.seconds = 0.05
	plan.interval = 0.05
	return plan


# ---- the Wi-Fi QR text --------------------------------------------------------------------------------

func test_the_wifi_qr_text_is_the_standard_one_and_reads_back() -> void:
	assert_eq(WifiQr.build(NETWORK), "WIFI:T:WPA;S:AndroidShare_4821;P:kalye4h\\;play;;", "a ; in the password is escaped")
	assert_eq(WifiQr.parse(WifiQr.build(NETWORK)), NETWORK)
	var odd: Dictionary = {"ssid": "Ana's \"phone\": 1,2", "password": "back\\slash12", "security": "SAE"}
	assert_eq(WifiQr.parse(WifiQr.build(odd)), odd)


func test_the_wifi_qr_of_android_s_own_hotspot_screen_reads_too() -> void:
	# the order Android's Settings share screen writes (any order, upper or lower case, an H field, WPA2 named as such)
	assert_eq(WifiQr.parse("WIFI:S:Juan's Galaxy;T:WPA2;P:12345678;H:false;;"), {"ssid": "Juan's Galaxy", "password": "12345678", "security": "WPA"})
	assert_eq(WifiQr.parse("wifi:T:nopass;S:Open Net;;"), {"ssid": "Open Net", "password": "", "security": "nopass"})


func test_what_is_not_a_joinable_wifi_qr_is_refused() -> void:
	for text: String in ["", "KALYEAH:ABCD2345", "https://kalyeah-promo.vercel.app", "WIFI:T:WEP;S:old;P:12345;;",
			"WIFI:T:WPA;P:12345678;;", "WIFI:T:WPA;S:short;P:1234567;;"]:
		assert_eq(WifiQr.parse(text), {}, text)


func test_the_wifi_qr_reads_from_a_camera_picture() -> void:
	var text: String = WifiQr.build(NETWORK)
	assert_eq(QrReader.read(QR_PICTURE.draw(QrCode.encode(text), 320, 0.75, 12.0, 0.05)), text)


# ---- host: the game hotspot ----------------------------------------------------------------------------

func test_with_no_network_the_host_can_start_a_game_hotspot_and_the_room_opens() -> void:
	phone = Phones.on_mobile_data_only()
	var went: Array = []
	link.allowed = false
	var card: MenuDialog = _create(went)
	assert_true(_shown(card, "StartHotspot"), "offered when there is no hotspot and no Wi-Fi")
	assert_true(_text(card).contains("Start a game hotspot"))
	_press(card, "StartHotspot")
	assert_eq(link.asked, 1, "Android asks first (Nearby devices, or location on older phones)")
	assert_eq(went, [true], "the room opens on the game hotspot")
	assert_eq(link.share_text(), WifiQr.build(NETWORK), "and the hotspot is there to share")


func test_a_phone_already_on_a_hotspot_or_wifi_goes_straight_on_as_before() -> void:
	phone = Phones.stock_hotspot()
	var went: Array = []
	_create(went)
	assert_eq(went, [true])
	assert_false(link.hotspot_running(), "no game hotspot was needed")


func test_every_reason_the_game_hotspot_does_not_start_says_what_to_do() -> void:
	phone = Phones.offline()
	for reason: String in ["permission", "location_off", "incompatible_mode", "unsupported", "generic"]:
		link.hotspot_answer = reason
		var went: Array = []
		var card: MenuDialog = _create(went)
		_press(card, "StartHotspot")
		assert_eq(went, [], reason)
		var said: String = _text(card)
		assert_eq(said, HotspotDialogs.hotspot_failure(reason), reason)
		assert_true(_shown(card, "StartHotspot") and _shown(card, "TryAgain"), "%s: try again from here" % reason)
		card.close(false)
	assert_true(HotspotDialogs.hotspot_failure("location_off").contains("Location"))


func test_saying_no_to_the_permission_does_not_start_anything() -> void:
	phone = Phones.offline()
	link.allowed = false
	link.answer = false
	var went: Array = []
	var card: MenuDialog = _create(went)
	_press(card, "StartHotspot")
	assert_eq(went, [])
	assert_eq(_text(card), HotspotDialogs.hotspot_failure("permission"))
	assert_false(link.hotspot_running())


func test_without_the_plugin_create_is_as_before() -> void:
	phone = Phones.offline()
	var plain: LocalLink = LocalLink.new()
	assert_false(plain.available(), "no plugin off a phone")
	var card: MenuDialog = HotspotDialogs.create_check(func() -> void: pass, _interfaces, func() -> void: pass, plain)
	card.open(_host)
	_tick(card)
	assert_false(_shown(card, "StartHotspot"))
	assert_eq(_text(card), "Turn on your hotspot or Wi-Fi. Then tap Try again.")


func test_the_host_s_room_shows_the_game_hotspot_as_a_qr_and_says_nfc_works_too() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	_host.add_child(view)
	view.set_online(false)
	view.show_room({"code": "", "host": 1, "team_size": 1, "first_to": 5, "members": [], "can_start_host": false}, 1)
	assert_false(view.qr_button.is_visible_in_tree(), "an ordinary hotspot: nothing to share")
	assert_null(view.show_qr())
	link.start_hotspot()
	view.set_local_share(link.share_text(), true)
	assert_true(view.qr_button.is_visible_in_tree())
	var card: MenuDialog = view.show_qr()
	var qr: QrView = card.body.get_node("QrView") as QrView
	assert_not_null(qr.code)
	assert_true((card.body.get_child(1) as Label).text.contains("back to back"))
	card.close(false)
	view.set_local_share("")
	assert_false(view.qr_button.is_visible_in_tree(), "the hotspot stopped: the QR goes")


# ---- friend: joining it ------------------------------------------------------------------------------

func test_a_friend_not_on_wifi_can_scan_and_the_game_joins_the_hotspot_then_looks_for_the_room() -> void:
	phone = Phones.on_mobile_data_only()
	var camera: SeeingCamera = SeeingCamera.new()
	camera.picture = QR_PICTURE.draw(QrCode.encode(WifiQr.build(NETWORK)), QrScanSheet.LENS_SIDE, 0.7, -8.0)
	var card: MenuDialog = _join(camera)
	assert_true(_shown(card, "ScanQr"))
	assert_true(_text(card).contains("hold your phones back to back"), "NFC is on: the tap is offered too")
	assert_true(link.reading, "and the phone listens for the tap")
	_press(card, "ScanQr")
	var sheet: MenuDialog = _host.find_child("QrScanSheet", true, false) as MenuDialog
	assert_not_null(sheet, "the scanner opens over the dialog")
	assert_true((sheet.body.get_node("QrScan") as QrScanSheet).read_now())
	assert_eq(link.joined, [NETWORK] as Array[Dictionary], "the game joins the friend's hotspot itself")
	assert_false(link.reading, "no NFC while joining")
	assert_eq(_text(card), "Joining your friend's game…", "waiting for the phone's address")
	phone = Phones.guest_on_a_hotspot()
	_tick(card, 20)
	assert_ne(_text(card), "Joining your friend's game…", "on the hotspot now: the room search went on")


func test_holding_the_phones_together_joins_the_same_way() -> void:
	phone = Phones.on_mobile_data_only()
	var card: MenuDialog = _join()
	link.nfc_read.emit(WifiQr.build(NETWORK))
	assert_eq(link.joined, [NETWORK] as Array[Dictionary])
	link.nfc_read.emit("not a hotspot")
	assert_eq(link.joined.size(), 1, "anything else read over NFC is ignored")
	card.close(false)


func test_a_friend_who_says_no_to_android_s_connect_dialog_can_scan_again() -> void:
	phone = Phones.on_mobile_data_only()
	link.join_answer = "declined"
	var card: MenuDialog = _join()
	var flow: HotspotFlow = (card.find_children("*", "Node", true, false).filter(
		func(node: Node) -> bool: return node is HotspotDialogs.Driver)[0] as HotspotDialogs.Driver).flow
	flow.join_network(NETWORK)
	assert_eq(_text(card), HotspotDialogs.join_failure("declined"))
	assert_true(_shown(card, "ScanQr") and _shown(card, "TryAgain"))
	assert_true(link.reading, "listening for a tap again")


func test_a_joined_wifi_whose_address_never_comes_ends_by_itself() -> void:
	phone = Phones.on_mobile_data_only()
	var card: MenuDialog = _join()
	var flow: HotspotFlow = (card.find_children("*", "Node", true, false).filter(
		func(node: Node) -> bool: return node is HotspotDialogs.Driver)[0] as HotspotDialogs.Driver).flow
	flow.join_network(NETWORK)
	_tick(card, ceili(HotspotFlow.RULES.join_wifi_grace * 30.0) + 5)
	assert_eq(flow.step, HotspotFlow.Step.NO_WIFI, "back to scan or Try again, never stuck")


func test_closing_the_join_dialog_stops_listening_and_lets_go_of_the_link() -> void:
	phone = Phones.on_mobile_data_only()
	var card: MenuDialog = _join()
	assert_true(link.reading)
	card.close(false)
	assert_false(link.reading)
	assert_eq(link.nfc_read.get_connections().size(), 0, "the shared link keeps nothing of the closed dialog")
	link.nfc_read.emit(WifiQr.build(NETWORK))
	assert_true(link.joined.is_empty())


func test_without_nfc_only_the_scan_is_offered() -> void:
	phone = Phones.on_mobile_data_only()
	link.nfc = "none"
	var card: MenuDialog = _join()
	assert_true(_shown(card, "ScanQr"))
	assert_false(_text(card).contains("back to back"))
	assert_false(link.reading)
	card.close(false)


func test_home_again_ends_the_local_session() -> void:
	link.start_hotspot()
	link.leave()
	assert_false(link.hotspot_running())
	assert_eq(link.left, 1, "the friend's Wi-Fi is let go")
	assert_false(link.reading)
