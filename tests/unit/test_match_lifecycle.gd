extends GutTest

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
var _loadout: Array[StringName]


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout = [&"bato_light", &"lata"]


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	TouchButton.holder = null


func _practice() -> PracticeMatch:
	var view: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(view)
	view.set_process(false)
	view.finish_pick()
	view.skip_intro()
	view.advance(view.rules.tick_dt())
	view.sim.players[view.local_id].position = Vector2(0.0, 2.0)
	view.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, -1.0)
	return view


func test_focus_loss_cancels_keyboard_aim_on_the_first_tick() -> void:
	var view: PracticeMatch = _practice()
	var player: PlayerState = view.sim.players[view.local_id]
	view.input.key_held[0] = true
	view.sim.set_input(view.local_id, PlayerInput.create(Vector2.ZERO, Vector2.UP, PlayerInput.BTN_WEAPON_1, 0))
	view.sim.step(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapon_cooldowns[0], 0.0, "focus loss cancels rather than release-casts")
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)


func test_focus_and_pause_both_need_to_clear_before_fresh_input_works() -> void:
	var view: PracticeMatch = _practice()
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)
	view.set_stick(Vector2.RIGHT)
	assert_eq(view._local_input(10).move, Vector2.ZERO, "focus returning cannot override an outstanding pause")
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.set_stick(Vector2.RIGHT)
	assert_eq(view._local_input(11).move, Vector2.ZERO, "resume cannot override missing focus")
	view._notification(Node.NOTIFICATION_APPLICATION_FOCUS_IN)
	view.set_stick(Vector2.RIGHT)
	assert_gt(view._local_input(12).move.length(), 0.0, "fresh input resumes after both states clear")


func test_interrupting_real_touch_discards_release_and_auto_run_state() -> void:
	var view: PracticeMatch = _practice()
	var stick: VirtualJoystick = view.hud.joystick
	var centre: Vector2 = stick.get_global_transform_with_canvas() * stick.rest_position()
	var touch: InputEventScreenTouch = InputEventScreenTouch.new()
	touch.index = 10
	touch.position = centre
	touch.pressed = true
	stick._input(touch)
	var drag: InputEventScreenDrag = InputEventScreenDrag.new()
	drag.index = 10
	drag.position = centre + Vector2(120.0, 0.0)
	stick._input(drag)
	touch.pressed = false
	stick._input(touch)
	assert_true(stick.auto_run)
	var button: AimButton = view.hud.aim_buttons[0]
	touch.index = 11
	touch.position = button.get_global_transform_with_canvas() * (button.size / 2.0)
	touch.pressed = true
	button._input(touch)
	view.advance(view.rules.tick_dt())
	var player: PlayerState = view.sim.players[view.local_id]
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	assert_false(stick.auto_run)
	assert_false(button.aiming)
	assert_null(TouchButton.holder)
	assert_eq(view.input.stick, Vector2.ZERO)
	var blocked: PlayerInput = view._local_input(20)
	assert_eq(blocked.buttons, PlayerInput.BTN_AIM_CANCEL, "an interruption must not synthesize another held tick")
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapon_cooldowns[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.advance(view.rules.tick_dt())
	assert_eq(player.weapon_cooldowns[0], 0.0)


func test_pause_and_resume_before_the_next_tick_still_cancel_held_aim() -> void:
	var view: PracticeMatch = _practice()
	var player: PlayerState = view.sim.players[view.local_id]
	view.aim_started(0)
	view.advance(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0)
	view._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	view._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	view.advance(view.rules.tick_dt())
	assert_eq(player.aim_hold[0], -1.0)
	assert_eq(player.weapon_cooldowns[0], 0.0)
	view.aim_started(0)
	view.advance(view.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0, "fresh aim works after resume")
	view.aim_released(Vector2.UP, false, 0)
	view.advance(view.rules.tick_dt())
	assert_gt(player.weapon_cooldowns[0], 0.0, "a deliberate release still casts")
