extends GutTest
## Audit 4 of the card tray and the tagged screen: the tray's well wraps the cards, a release with nobody in range says so, a touch
## the system cancels keeps the card, the box has one clear marker, the kid names fit and the results card stays clean. (The cancel
## mark is gone: a card dropped back on the hand cancels.)

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
	WebTouchCancel.clear()


func _hud(left_handed: bool = false) -> MatchHud:
	Settings.left_handed = left_handed
	var phone: Control = autofree(Control.new()) as Control
	phone.size = PHONE
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
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


# ---- 6: the tray's well -----------------------------------------------------------------------

func test_the_well_sits_under_next_and_the_four_cards() -> void:
	for left: bool in [false, true]:
		var hud: MatchHud = _hud(left)
		var well: Rect2 = hud.hand.row_rect()
		assert_true(well.encloses(hud.hand.next_rect), "left-handed %s: NEXT is in the well" % left)
		for card: Rect2 in hud.hand.card_rects:
			assert_true(well.encloses(card), "left-handed %s: every card is in the well" % left)


# ---- 2: nobody in range ---------------------------------------------------------------------

func test_aiming_a_card_with_nobody_in_range_says_so() -> void:
	var sim: MatchSim = _sim(100.0)
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	hud.sync_hold_hint(player, true, MatchInput.Release.TAP, sim.lacks_target(player, 0))
	assert_string_contains(hud.feedback.hint.text, "Nobody in range")
	hud.note_released(0, false)
	hud.sync_player(sim, player, false)
	assert_eq(hud.hand.notices[0], MatchHud.NO_TARGET_TAG, "the card says why nothing was thrown")
	assert_eq(player.weapons[0], &"bato_light", "and stays in the hand")


func test_aiming_a_card_with_someone_in_range_says_how_it_goes() -> void:
	var sim: MatchSim = _sim(2.0)
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	hud.sync_hold_hint(player, true, MatchInput.Release.TAP, sim.lacks_target(player, 0))
	assert_string_contains(hud.feedback.hint.text, "auto-aim")
	hud.note_released(0, false)
	hud.sync_player(sim, player, false)
	assert_eq(hud.hand.notices[0], "")


func test_a_cancelled_card_with_nobody_in_range_shows_no_tag() -> void:
	var sim: MatchSim = _sim(100.0)
	var hud: MatchHud = _hud()
	hud.sync_player(sim, sim.players[1], false)
	hud.note_released(0, true)
	hud.sync_player(sim, sim.players[1], false)
	assert_eq(hud.hand.notices[0], "", "a cancel is the player's own choice: nothing to explain")


func test_a_card_that_needs_no_target_never_lacks_one() -> void:
	var sim: MatchSim = _sim(100.0)
	assert_true(sim.lacks_target(sim.players[1], 0), "TOSS with nobody in range")
	assert_false(sim.lacks_target(sim.players[1], 1), "HEAL needs nobody")
	sim.players[2].position = Vector2(0.0, 2.0)
	assert_false(sim.lacks_target(sim.players[1], 0), "TOSS with the enemy close")


# ---- 9: a touch the browser cancels -----------------------------------------------------------

func _ready_hand(hud: MatchHud) -> CardHand:
	var hand: CardHand = hud.hand
	var def: WeaponDef = WeaponDef.new()
	def.id = &"test"
	hand.defs[0] = def
	hand.pickable[0] = true
	hand.active = true
	hand.visible = true
	return hand


func _touch(hand: CardHand, pressed: bool) -> void:
	var touch: InputEventScreenTouch = InputEventScreenTouch.new()
	touch.index = 0
	touch.pressed = pressed
	touch.position = hand.get_global_rect().position + hand.card_rects[0].get_center() + Vector2(0.0, -400.0 if not pressed else 0.0)
	hand._input(touch)


func test_a_touch_the_browser_cancels_keeps_the_card() -> void:
	var hand: CardHand = _ready_hand(_hud())
	watch_signals(hand)
	_touch(hand, true)
	assert_true(hand.holding())
	WebTouchCancel.note()  # what the window's touchcancel listener does before Godot's release arrives
	_touch(hand, false)
	assert_signal_emitted(hand, "card_cancelled", "the release is a cancel: nothing is thrown")
	assert_signal_not_emitted(hand, "card_released")


func test_an_old_browser_cancel_does_not_cancel_a_later_throw() -> void:
	var hand: CardHand = _ready_hand(_hud())
	watch_signals(hand)
	WebTouchCancel._noted_at = Time.get_ticks_msec() - WebTouchCancel.WINDOW_MS - 1
	_touch(hand, true)
	_touch(hand, false)
	assert_signal_emitted(hand, "card_released", "the release throws")
	assert_signal_not_emitted(hand, "card_cancelled")


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
	assert_eq(hud.hand.accessibility_name, "Your hand")
	assert_string_contains(hud.hand.accessibility_description, "Card 1: ", "each card is read out")
	assert_string_contains(hud.hand.accessibility_description, "elixir", "with its cost")


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
