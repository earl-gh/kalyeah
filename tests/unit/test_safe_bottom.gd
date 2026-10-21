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


# ---- your health in the middle of the console --------------------------------------------

func test_the_console_shows_your_health_between_the_pad_and_the_buttons() -> void:
	var practice: PracticeMatch = await _practice()
	practice.finish_pick()
	practice.skip_intro()
	practice.advance(1.0 / 30.0)
	var console: ConsolePanel = practice.hud.console
	assert_true(console.health_shown)
	assert_eq(console.health_text, str(practice.rules.player_max_hp))
	assert_eq(console.health_fraction, 1.0)
	var free: Vector2 = console.gap()
	assert_gt(free.y, 90.0, "there is room between the pad and the weapons")
	var pad: Rect2 = practice.hud.dpad.get_global_rect()
	var weapon: Rect2 = practice.hud.weapon_buttons[0].get_global_rect()
	assert_gt(free.x + practice.hud.console.get_global_rect().position.x, pad.get_center().x, "right of the pad")
	assert_lt(free.x + practice.hud.console.get_global_rect().position.x, weapon.get_center().x, "left of the weapons")
	practice.sim.damage(practice.local_id, 40)
	practice.advance(1.0 / 30.0)
	assert_eq(console.health_text, str(practice.rules.player_max_hp - 40))
	assert_almost_eq(console.health_fraction, 0.6, 0.001)
	practice.sim.kill(practice.local_id)
	practice.advance(1.0 / 30.0)
	assert_false(console.health_shown, "tagged: nothing to show")


func test_the_health_gap_follows_a_left_handed_layout() -> void:
	Settings.set_left_handed(true)
	var practice: PracticeMatch = await _practice()
	var free: Vector2 = practice.hud.console.gap()
	var x: float = free.x + practice.hud.console.get_global_rect().position.x
	var pad: float = practice.hud.dpad.get_global_rect().get_center().x
	var weapon: float = practice.hud.weapon_buttons[0].get_global_rect().get_center().x
	Settings.set_left_handed(false)
	assert_gt(free.y, 90.0)
	assert_lt(x, pad, "left of the pad, which is on the right now")
	assert_gt(x, weapon, "right of the weapons")
