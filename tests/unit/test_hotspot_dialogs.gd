extends GutTest
## The hotspot Create dialog (checks the hotspot is on) and Join dialog (eight boxes for the code).

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
	assert_eq((card.find_child("Message", true, false) as Label).text, "Checking your hotspot...", "it shows the loading state first")
	assert_true(went.is_empty())
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_eq(went.size(), 1, "and goes to the room")


func test_create_says_when_the_hotspot_is_off_and_offers_another_try() -> void:
	var went: Array = []
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: went.append(true), func() -> void: pass, true, PackedStringArray(["127.0.0.1"])))
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_true(went.is_empty(), "no room while the hotspot is off")
	assert_string_contains((card.find_child("Message", true, false) as Label).text, "off")
	assert_not_null(card.find_child("TryAgain", true, false))
	assert_not_null(card.find_child("Cancel", true, false))


func test_create_without_the_android_app_says_so() -> void:
	var card: MenuDialog = _open(HotspotDialogs.create_check(func() -> void: pass, func() -> void: pass, false, PHONE_HOTSPOT))
	await wait_seconds(HotspotDialogs.CHECK_SECONDS + 0.4)
	assert_string_contains((card.find_child("Message", true, false) as Label).text, "Android")


func test_join_has_eight_boxes_a_join_and_a_cancel_button() -> void:
	var codes: Array[String] = []
	var card: MenuDialog = _open(HotspotDialogs.join(func(code: String) -> void: codes.append(code)))
	var input: HotspotCodeInput = card.get_meta("input") as HotspotCodeInput
	var join: Button = card.find_child("Join", true, false) as Button
	assert_eq(input.fields.size(), 8, "one box for each character")
	for field: LineEdit in input.fields:
		assert_eq(field.max_length, 1)
	assert_not_null(card.find_child("Cancel", true, false))
	assert_true(join.disabled, "not until all eight are filled")
	input.set_code("ABC234")
	assert_true(join.disabled)
	input.set_code("abc234xy")
	assert_false(join.disabled)
	join.pressed.emit()
	assert_eq(codes, ["ABC234XY"] as Array[String], "the code comes out in capitals")


func test_typing_moves_the_cursor_on_and_only_letters_and_digits_are_taken() -> void:
	var input: HotspotCodeInput = autofree(HotspotCodeInput.new()) as HotspotCodeInput
	add_child(input)
	input.fields[0].text = "a"
	input.fields[0].text_changed.emit("a")
	assert_eq(input.fields[0].text, "A")
	input.fields[1].text = "-"
	input.fields[1].text_changed.emit("-")
	assert_eq(input.fields[1].text, "", "a dash is not taken")
	input.set_code("  a-b c1  ")
	assert_eq(input.code(), "ABC1")
	assert_false(input.is_complete())


func test_cancel_closes_the_join_dialog_without_joining() -> void:
	var codes: Array[String] = []
	var card: MenuDialog = _open(HotspotDialogs.join(func(code: String) -> void: codes.append(code)))
	(card.find_child("Cancel", true, false) as Button).pressed.emit()
	assert_false(card.is_open)
	assert_true(codes.is_empty())
