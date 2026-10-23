extends GutTest
## Audit 4 of the card tray and the tagged screen: the cancel mark is out of every normal aim, the tray backing wraps the cards, a
## release with nobody in range says so, a touch the system cancels keeps the card, the box has one clear marker, the kid names fit and
## the results card stays clean.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
const PHONE: Vector2 = Vector2(720.0, 1280.0)

var _rules: GameRules
var _was: bool
var _left: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_rules.card_cycle = true
	_left = Settings.left_handed
	WebTouchCancel.clear()


func after_each() -> void:
	_rules.card_cycle = _was
	Settings.left_handed = _left
	TouchButton.holder = null
	WebTouchCancel.clear()


func _hud(left_handed: bool = false) -> MatchHud:
	Settings.left_handed = left_handed
	var phone: Control = autofree(Control.new()) as Control
	phone.size = PHONE
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.set_card_mode(true)
	return hud


## Player 1 holds a hand whose first card is TOSS (it needs a target), player 2 (the enemy) stands `distance` metres away.
func _sim(distance: float) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.deal_cards(1)
	sim.deal_cards(2)
	var hand: PlayerState = sim.players[1]
	hand.weapons = [&"bato_light", &"heal"]
	hand.position = Vector2.ZERO
	sim.players[2].position = Vector2(0.0, distance)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	return sim


static func _gap(a: Rect2, b: Rect2) -> float:
	var dx: float = maxf(0.0, maxf(a.position.x - b.end.x, b.position.x - a.end.x))
	var dy: float = maxf(0.0, maxf(a.position.y - b.end.y, b.position.y - a.end.y))
	return Vector2(dx, dy).length()


# ---- 1: the cancel mark ---------------------------------------------------------------------

func test_no_full_aim_from_anywhere_on_a_card_reaches_the_cancel_mark() -> void:
	for left: bool in [false, true]:
		var hud: MatchHud = _hud(left)
		var cancel: Rect2 = hud.cancel_zone.get_global_rect()
		for button: AimButton in hud.weapon_buttons:
			var touch_area: Rect2 = button.get_global_rect().grow(TouchButton.CARD_SLACK)
			assert_gt(_gap(touch_area, cancel), AimButton.DRAG_RADIUS, "%s, left-handed %s: a full aim ends short of the mark" % [button.name, left])


func test_the_cancel_mark_is_on_screen_and_still_cancels_on_purpose() -> void:
	for left: bool in [false, true]:
		var hud: MatchHud = _hud(left)
		var cancel: Rect2 = hud.cancel_zone.get_global_rect()
		assert_true(Rect2(Vector2.ZERO, PHONE).encloses(cancel), "left-handed %s: the mark is on the screen" % left)
		assert_lt(cancel.end.y, hud.weapon_buttons[0].get_global_rect().position.y, "it sits above the cards")
		var card: AimButton = hud.weapon_buttons[0]
		card._begin(card.get_global_rect().get_center())
		hud.cancel_zone.visible = true  # the match shows the mark while a card is held
		card._drag(cancel.get_center())
		assert_true(card.over_cancel, "dragging the card onto the mark still cancels")
		card._finish(true)


# ---- 6: the tray backing --------------------------------------------------------------------

func test_the_tray_backing_sits_under_the_cards_and_the_window() -> void:
	for left: bool in [false, true]:
		var hud: MatchHud = _hud(left)
		var origin: Vector2 = hud.console.get_global_rect().position
		for slot: int in 2:
			# hand_rects runs from the inner card (slot 0) to the outer one
			var card: Rect2 = hud.weapon_buttons[slot].get_global_rect()
			var drawn: Rect2 = hud.console.hand_rects[slot]
			assert_almost_eq(drawn.position.x + origin.x, card.position.x, 0.5, "left-handed %s: card %d's well is under it" % [left, slot + 1])
		var window: Rect2 = hud.shuffler.get_global_rect()
		assert_almost_eq(hud.console.shuffler_rect.position.x + origin.x, window.position.x, 0.5, "left-handed %s: the window's well" % left)


# ---- 2: nobody in range ---------------------------------------------------------------------

func test_aiming_a_card_with_nobody_in_range_says_so() -> void:
	var sim: MatchSim = _sim(100.0)
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	var toss: AimButton = hud.weapon_buttons[0]
	toss._begin(toss.get_global_rect().get_center())
	hud.sync_player(sim, player, false)
	assert_string_contains(hud.feedback.hint.text, "Nobody in range")
	toss._finish(false)
	hud.sync_player(sim, player, false)
	assert_eq(toss.notice_text, MatchHud.NO_TARGET_TAG, "the card says why nothing was thrown")
	assert_eq(player.weapons[0], &"bato_light", "and stays in the hand")


func test_aiming_a_card_with_someone_in_range_still_says_release_to_cast() -> void:
	var sim: MatchSim = _sim(2.0)
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	var toss: AimButton = hud.weapon_buttons[0]
	toss._begin(toss.get_global_rect().get_center())
	hud.sync_player(sim, player, false)
	assert_string_contains(hud.feedback.hint.text, "Release to cast")
	toss._finish(false)
	hud.sync_player(sim, player, false)
	assert_eq(toss.notice_text, "")


func test_a_cancelled_aim_with_nobody_in_range_shows_no_tag() -> void:
	var sim: MatchSim = _sim(100.0)
	var hud: MatchHud = _hud()
	hud.sync_player(sim, sim.players[1], false)
	var toss: AimButton = hud.weapon_buttons[0]
	toss._begin(toss.get_global_rect().get_center())
	toss._finish(true)
	hud.sync_player(sim, sim.players[1], false)
	assert_eq(toss.notice_text, "", "a cancel is the player's own choice: nothing to explain")


func test_a_card_that_needs_no_target_never_lacks_one() -> void:
	var sim: MatchSim = _sim(100.0)
	assert_true(sim.lacks_target(sim.players[1], 0), "TOSS with nobody in range")
	assert_false(sim.lacks_target(sim.players[1], 1), "HEAL needs nobody")
	sim.players[2].position = Vector2(0.0, 2.0)
	assert_false(sim.lacks_target(sim.players[1], 0), "TOSS with the enemy close")


# ---- 9: a touch the browser cancels -----------------------------------------------------------

func _touch(button: AimButton, pressed: bool) -> void:
	var touch: InputEventScreenTouch = InputEventScreenTouch.new()
	touch.index = 0
	touch.pressed = pressed
	touch.position = button.get_global_rect().get_center() + Vector2(0.0, -120.0 if not pressed else 0.0)
	button._input(touch)


func test_a_touch_the_browser_cancels_keeps_the_card() -> void:
	var hud: MatchHud = _hud()
	var card: AimButton = hud.weapon_buttons[0]
	watch_signals(card)
	_touch(card, true)
	assert_true(card.aiming)
	WebTouchCancel.note()  # what the window's touchcancel listener does before Godot's release arrives
	_touch(card, false)
	var released: Array = get_signal_parameters(card, "aim_released") as Array
	assert_true(released[1] as bool, "the release is a cancel: nothing is thrown")


func test_an_old_browser_cancel_does_not_cancel_a_later_throw() -> void:
	var hud: MatchHud = _hud()
	var card: AimButton = hud.weapon_buttons[0]
	watch_signals(card)
	WebTouchCancel._noted_at = Time.get_ticks_msec() - WebTouchCancel.WINDOW_MS - 1
	_touch(card, true)
	_touch(card, false)
	var released: Array = get_signal_parameters(card, "aim_released") as Array
	assert_false(released[1] as bool, "the release throws")


# ---- 7: the tagged box ----------------------------------------------------------------------

func test_two_tagged_kids_have_no_words_over_them() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	var tagged: int = 0
	for id: int in practice.sim.players:
		if practice.sim.players[id].team == practice.sim.players[practice.local_id].team and tagged < 2:
			practice.sim.kill(id)
			tagged += 1
	for id: int in practice.sim.players:
		if practice.sim.players[id].tagged:
			assert_eq(practice.overhead().text_for(id), "", "the box says tagged: no word over each kid")


func test_the_rescue_marker_and_the_feed_sit_under_the_box() -> void:
	var window_was: Vector2i = get_window().size
	for shape: Vector2 in [PHONE, Vector2(720.0, 1558.0)]:
		get_window().size = Vector2i(shape)
		var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
		add_child(practice)
		await wait_process_frames(2)
		practice.finish_pick()
		practice.skip_intro()
		practice.advance(DT)
		var overhead: OverheadHud = practice.overhead()
		var box: Rect2 = overhead.own_box_on_screen()
		assert_gt(box.size.x, 0.0, "%s: the box is on screen" % shape)
		var pill: Rect2 = overhead.rescue_pill_rect()
		assert_gt(pill.position.y, box.end.y, "%s: the RESCUE pill is under the box, off the waiting kids" % shape)
		var feed_y: float = practice.hud.feed_box.get_global_rect().position.y
		assert_gte(feed_y, overhead.box_clear_y(), "%s: the feed starts under the box and its marker" % shape)
		practice.hud.set_top_inset(UiMetrics.TOUCH)
		await wait_process_frames(2)  # the camera re-frames for the taller header
		practice.advance(DT)
		feed_y = practice.hud.feed_box.get_global_rect().position.y
		var expected: float = maxf(practice.hud.header_height() + MatchHud.FEED_DROP, overhead.box_clear_y() + MatchHud.MARGIN)
		assert_almost_eq(feed_y, expected, 0.5, "%s with a notch: just under the box or the header, not a notch lower" % shape)
		practice.queue_free()
		await wait_process_frames(1)
	get_window().size = window_was
	await wait_process_frames(2)


# ---- 12: names and descriptions -----------------------------------------------------------

func test_every_kid_name_fits_its_tile() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	var screen: WeaponPickScreen = practice.get_node("%PickScreen") as WeaponPickScreen
	for card: Node in screen.find_children("Profile_*", "Button", true, false):
		for child: Node in card.get_children():
			if child is Label and (child as Label).visible:
				var label: Label = child as Label
				var width: float = label.get_theme_font("font").get_string_size(label.text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, label.get_theme_font_size("font_size")).x
				assert_lte(width, WeaponPickScreen.PROFILE_SIZE.x, "%s fits" % label.text)
				assert_gte(label.get_theme_font_size("font_size"), UiMetrics.CAPTION)


func test_a_room_kid_name_fits_its_card() -> void:
	for id: String in Session.AVATARS:
		var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
		var width: float = KalyeahTheme.BODY_FONT.get_string_size(definition.display_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION).x
		assert_lte(width, HotspotRoomView.KID_CARD.x, "%s fits its card, so two names never run together" % definition.display_name)


func test_a_card_tells_a_screen_reader_how_to_use_it() -> void:
	var sim: MatchSim = _sim(2.0)
	var hud: MatchHud = _hud()
	hud.sync_player(sim, sim.players[1], false)
	for button: AimButton in hud.weapon_buttons:
		assert_ne(button.accessibility_description, "", "%s has a description" % button.accessibility_name)


# ---- 13: results ----------------------------------------------------------------------------

func test_confetti_falls_behind_the_results_card() -> void:
	var results: MatchResults = MatchResults.new()
	add_child_autofree(results)
	var card_holder: int = results._card.get_parent().get_index()
	assert_lt(results.confetti.get_index(), card_holder, "drawn before the card, so never over its buttons")


func test_the_tagged_panel_hides_when_the_match_is_over() -> void:
	var sim: MatchSim = _sim(2.0)
	var hud: MatchHud = _hud()
	sim.players[1].tagged = true
	hud.sync_player(sim, sim.players[1], false)
	assert_true(hud.tagged_panel.visible)
	sim.phase = MatchSim.Phase.MATCH_OVER
	hud.sync_player(sim, sim.players[1], false)
	assert_false(hud.tagged_panel.visible, "the results card is the only thing on the dimmed street")
