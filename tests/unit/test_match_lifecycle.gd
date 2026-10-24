extends GutTest

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
var _loadout: Array[StringName]


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout = [&"bato_light", &"lata"]


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()


func _practice() -> PracticeMatch:
	var view: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(view)
	view.set_process(false)
	view.finish_pick()
	view.skip_intro()
	view.advance(view.rules.tick_dt())
	view.sim.players[view.local_id].position = Vector2(0.0, 2.0)
	view.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, -1.0)
	var player: PlayerState = view.sim.players[view.local_id]
	player.elixir = view.rules.elixir_max  # every card affordable
	player.weapons[0] = &"gunting_light"  # a cone always casts (a homing card or a heal may find nobody)
	view.advance(view.rules.tick_dt())
	return view


func _now() -> int:
	return Time.get_ticks_msec()


func test_focus_loss_cancels_a_held_card_on_the_first_tick() -> void:
	var view: PracticeMatch = _practice()
	var player: PlayerState = view.sim.players[view.local_id]
	var card: StringName = player.weapons[0]
	view.input.card_press(0, _now())
	view.input.card_drag(false, Vector2(0.0, -1.0))
	view.advance(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapons[0], card, "focus loss cancels rather than release-casts: the card stays")
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)


func test_focus_and_pause_both_need_to_clear_before_fresh_input_works() -> void:
	var view: PracticeMatch = _practice()
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)
	view.input.move_press(Vector2(3.0, 0.0))
	assert_false(view._local_input(10).is_pressed(PlayerInput.BTN_MOVE_TARGET), "focus returning cannot override an outstanding pause")
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.input.move_press(Vector2(3.0, 0.0))
	assert_false(view._local_input(11).is_pressed(PlayerInput.BTN_MOVE_TARGET), "resume cannot override missing focus")
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)
	view.input.move_press(Vector2(3.0, 0.0))
	assert_true(view._local_input(12).is_pressed(PlayerInput.BTN_MOVE_TARGET), "fresh input resumes after both states clear")


func test_interrupting_real_touch_lets_go_of_the_street_and_drops_the_card() -> void:
	var view: PracticeMatch = _practice()
	var street: StreetTouch = view.hud.street
	var touch: InputEventScreenTouch = InputEventScreenTouch.new()
	touch.index = 10
	touch.position = Vector2(360.0, 500.0)
	touch.pressed = true
	assert_true(street.handle(touch), "one finger holds the street")
	assert_true(view.input.move_held)
	var hand: CardHand = view.hud.hand
	var card: InputEventScreenTouch = InputEventScreenTouch.new()
	card.index = 11
	card.position = hand.get_global_rect().position + hand.card_rects[0].get_center()
	card.pressed = true
	hand._input(card)
	view.advance(view.rules.tick_dt())
	var player: PlayerState = view.sim.players[view.local_id]
	assert_gte(player.aim_hold[0], 0.0, "another holds a card")
	var held: StringName = player.weapons[0]
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	assert_false(street.holding(), "an interruption lets go of the street")
	assert_false(view.input.move_held)
	assert_false(hand.holding(), "and drops the card")
	assert_false(view.input.card_held())
	var blocked: PlayerInput = view._local_input(20)
	assert_eq(blocked.buttons, PlayerInput.BTN_AIM_CANCEL, "an interruption must not synthesize another held tick")
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapons[0], held, "nothing was cast")
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.advance(view.rules.tick_dt())
	assert_eq(player.weapons[0], held)


func test_pause_and_resume_before_the_next_tick_still_cancel_a_held_card() -> void:
	var view: PracticeMatch = _practice()
	var player: PlayerState = view.sim.players[view.local_id]
	var held: StringName = player.weapons[0]
	view.card_pressed(0)
	view.advance(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapons[0], held)
	view.input.card_press(0, _now())
	view.advance(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0, "a fresh hold works after resume")
	view.input.card_drag(false, player.position + Vector2(0.0, -2.0))
	view.input.card_release(_now())
	view.advance(view.rules.tick_dt())
	view.advance(view.rules.tick_dt())
	assert_true(player.weapons[0] != held or player.weapon_cooldowns[0] > 0.0, "a deliberate release still casts")
