extends GutTest
## The home bar / gesture area at the bottom of a phone: the tray grows by it, the hand and hints ride above it,
## and the camera keeps the taller tray clear. With no inset nothing changes.

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


func test_an_inset_lifts_the_hand_and_grows_the_tray_and_the_camera_reserve() -> void:
	var practice: PracticeMatch = await _practice()
	var hud: MatchHud = practice.hud
	var card_before: float = hud.hand.get_global_rect().end.y
	var hint_before: float = practice.hud.feedback.hint.offset_bottom
	hud.set_bottom_inset(40.0)
	assert_eq(hud.console_height(), MatchHud.CONSOLE_HEIGHT + 40.0)
	assert_almost_eq(hud.hand.get_global_rect().end.y, card_before - 40.0, 0.5, "the hand rides above the bar")
	assert_almost_eq(hud.console.get_rect().size.y, MatchHud.CONSOLE_HEIGHT + 40.0, 0.5, "the tray goes to the screen edge")
	assert_eq(practice._camera.bottom_reserve_px, MatchHud.CONSOLE_HEIGHT + 40.0, "the camera keeps it clear")
	assert_almost_eq(practice.hud.feedback.hint.offset_bottom, hint_before - 40.0, 0.5, "the hint stays above the tray")
	hud.set_bottom_inset(0.0)
	assert_almost_eq(hud.hand.get_global_rect().end.y, card_before, 0.5, "and back")


func test_a_huge_inset_is_capped() -> void:
	var practice: PracticeMatch = await _practice()
	practice.hud.set_bottom_inset(500.0)
	assert_eq(practice.hud.bottom_inset, MatchHud.MAX_BOTTOM_INSET)


func test_the_intro_follows_the_inset() -> void:
	var practice: PracticeMatch = await _practice()
	var intro_before: float = practice.hud.intro_bar.offset_bottom
	practice.hud.set_bottom_inset(30.0)
	assert_almost_eq(practice.hud.intro_bar.offset_bottom, intro_before - 30.0, 0.5)


# ---- no health in the tray ---------------------------------------------------------------------

func test_the_tray_has_no_health_and_hides_with_the_hand_when_you_are_tagged() -> void:
	var practice: PracticeMatch = await _practice()
	practice.finish_pick()
	practice.skip_intro()
	practice.advance(1.0 / 30.0)
	var hand: CardHand = practice.hud.hand
	assert_true(hand.visible)
	assert_false("health_rect" in hand, "your health is over your kid, not in the tray (owner)")
	assert_false(practice.hud.chat.visible, "no chat button in offline practice (owner): there is nobody to talk to")
	assert_gt(practice.hud.console.column_width, 0.0, "the cardboard leaves the column beside it")
	practice.sim.kill(practice.local_id)
	practice.advance(1.0 / 30.0)
	assert_false(hand.visible, "tagged: the tray carries the box's message instead")
	practice.hud.set_chat_enabled(true)
	assert_false(practice.hud.chat.visible, "and the chat button waits")
	assert_eq(practice.hud.console.column_width, 0.0, "the cardboard runs the whole width then")
