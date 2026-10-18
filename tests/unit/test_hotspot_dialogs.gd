extends GutTest
## Hotspot platform checks and automatic code-free joining.

const PHONE_HOTSPOT: PackedStringArray = ["127.0.0.1", "192.168.43.1"]


func _open(card: MenuDialog) -> MenuDialog:
	var host: Control = autofree(Control.new()) as Control
	add_child(host)
	card.open(host)
	return card


func test_the_state_needs_the_android_app_and_a_network_address() -> void:
	assert_eq(HotspotDialogs.state(false, PHONE_HOTSPOT), HotspotDialogs.State.UNSUPPORTED)
	assert_eq(HotspotDialogs.state(true, PackedStringArray(["127.0.0.1"])), HotspotDialogs.State.OFF)
	assert_eq(HotspotDialogs.state(true, PHONE_HOTSPOT), HotspotDialogs.State.ON)


func test_create_checks_then_goes_to_the_room_when_the_hotspot_is_on() -> void:
	var went: Array = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: went.append(true), func() -> void: pass, true, PHONE_HOTSPOT))
	assert_eq((card.find_child("Message", true, false) as Label).text, "Checking local connection...", "it shows the loading state first")
	assert_true(went.is_empty())
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_eq(went.size(), 1, "and goes to the room")


func test_create_says_when_the_hotspot_is_off_and_offers_another_try() -> void:
	var went: Array = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: went.append(true), func() -> void: pass, true, PackedStringArray(["127.0.0.1"])))
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_true(went.is_empty(), "no room while the hotspot is off")
	assert_string_contains((card.find_child("Message", true, false) as Label).text, "No local network")
	assert_not_null(card.find_child("TryAgain", true, false))
	assert_not_null(card.find_child("Cancel", true, false))


func test_create_without_the_android_app_says_so() -> void:
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: pass, func() -> void: pass, false, PHONE_HOTSPOT))
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_string_contains((card.find_child("Message", true, false) as Label).text, "Android")


func test_join_on_android_opens_the_room_search_without_a_keyboard() -> void:
	var codes: Array[String] = []
	var card: MenuDialog = _open(HotspotDialogs.join(func(code: String) -> void: codes.append(code), true))
	assert_eq(card.find_children("*", "LineEdit", true, false).size(), 0)
	assert_eq(codes, [""] as Array[String])
	assert_false(card.is_open)


func test_join_on_web_explains_the_android_requirement_without_searching() -> void:
	var calls: Array[String] = []
	var card: MenuDialog = _open(HotspotDialogs.join(func(code: String) -> void: calls.append(code), false))
	assert_true(calls.is_empty())
	assert_eq(card.find_children("*", "LineEdit", true, false).size(), 0)
	assert_true(card.is_open)


func test_canceling_create_destroys_the_pending_check_safely() -> void:
	var calls: Array[bool] = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: calls.append(true), func() -> void: pass, true, PHONE_HOTSPOT))
	card.close(false)
	await wait_seconds(0.1)
	assert_true(calls.is_empty(), "cancel cannot open a room later")


func test_cancel_closes_the_join_dialog_without_joining() -> void:
	var codes: Array[String] = []
	var card: MenuDialog = _open(HotspotDialogs.join(func(code: String) -> void: codes.append(code), false))
	(card.find_child("Cancel", true, false) as Button).pressed.emit()
	assert_false(card.is_open)
	assert_true(codes.is_empty())
