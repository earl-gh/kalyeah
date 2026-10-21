extends GutTest
## The home bar / gesture area at the bottom of a phone: the console grows by it, the controls and hints ride above it,
## and the camera keeps the taller console clear. With no inset nothing changes.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	await wait_process_frames(2)
	return practice


func test_without_an_inset_the_console_is_its_normal_height() -> void:
	var practice: PracticeMatch = await _practice()
	assert_eq(practice.hud.bottom_inset, 0.0)
	assert_eq(practice.hud.console_height(), MatchHud.CONSOLE_HEIGHT)
	assert_eq(practice._camera.bottom_reserve_px, MatchHud.CONSOLE_HEIGHT)


func test_an_inset_lifts_the_controls_and_grows_the_console_and_the_camera_reserve() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var pad_before: float = hud.dpad.get_rect().get_center().y
	var weapon_before: float = hud.weapon_buttons[0].get_rect().get_center().y
	var hint_before: float = practice.hud.feedback.hint.offset_bottom
	hud.set_bottom_inset(40.0)
	assert_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT + 40.0)
	assert_almost_eq(hud.dpad.get_rect().get_center().y, pad_before - 40.0, 0.5, "the pad rides above the bar")
	assert_almost_eq(hud.weapon_buttons[0].get_rect().get_center().y, weapon_before - 40.0, 0.5)
	assert_almost_eq(hud.console.get_rect().size.y, MatchHud.CONSOLE_HEIGHT + 40.0, 0.5, "the grey shell goes to the screen edge")
	assert_eq(practice._camera.bottom_reserve_px, MatchHud.CONSOLE_HEIGHT + 40.0, "the camera keeps it clear")
	assert_almost_eq(practice.hud.feedback.hint.offset_bottom, hint_before - 40.0, 0.5, "the hint stays above the console")
	hud.set_bottom_inset(0.0)
	assert_almost_eq(hud.dpad.get_rect().get_center().y, pad_before, 0.5, "and back")


func test_a_huge_inset_is_capped() -> void:
	var practice: PracticeMatch = await _practice()
	practice.hud.set_bottom_inset(500.0)
	assert_eq(practice.hud.bottom_inset, MatchHud.MAX_BOTTOM_INSET)


func test_the_cancel_zone_and_intro_follow_the_inset() -> void:
	var practice: PracticeMatch = await _practice()
	var cancel_before: float = practice.hud.cancel_zone.get_rect().get_center().y
	var intro_before: float = practice.hud.intro_bar.offset_bottom
	practice.hud.set_bottom_inset(30.0)
	assert_almost_eq(practice.hud.cancel_zone.get_rect().get_center().y, cancel_before - 30.0, 0.5)
	assert_almost_eq(practice.hud.intro_bar.offset_bottom, intro_before - 30.0, 0.5)
