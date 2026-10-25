extends GutTest
## The hotspot check before a room is made: a network address is needed, and cancelling leaves nothing behind.

const PHONE_HOTSPOT: PackedStringArray = ["127.0.0.1", "192.168.43.1"]


func _open(card: MenuDialog) -> MenuDialog:
	var host: Control = autofree(Control.new()) as Control
	add_child(host)
	card.open(host)
	return card


func test_the_state_needs_a_network_address() -> void:
	assert_eq(HotspotDialogs.state(PackedStringArray(["127.0.0.1"])), HotspotDialogs.State.OFF)
	assert_eq(HotspotDialogs.state(PHONE_HOTSPOT), HotspotDialogs.State.ON)


func test_create_checks_then_goes_to_the_room_when_the_hotspot_is_on() -> void:
	var went: Array = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: went.append(true), PHONE_HOTSPOT))
	assert_eq((card.find_child("Message", true, false) as Label).text, "Checking local connection…", "it shows the loading state first")
	assert_true(went.is_empty())
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_eq(went.size(), 1, "and goes to the room")


func test_create_says_when_the_hotspot_is_off_and_offers_another_try() -> void:
	var went: Array = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: went.append(true), PackedStringArray(["127.0.0.1"])))
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_true(went.is_empty(), "no room while the hotspot is off")
	assert_string_contains((card.find_child("Message", true, false) as Label).text, "No local network")
	assert_not_null(card.find_child("TryAgain", true, false))
	assert_not_null(card.find_child("Cancel", true, false))


func test_canceling_create_destroys_the_pending_check_safely() -> void:
	var calls: Array[bool] = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: calls.append(true), PHONE_HOTSPOT))
	card.close(false)
	await wait_seconds(0.1)
	assert_true(calls.is_empty(), "cancel cannot open a room later")
