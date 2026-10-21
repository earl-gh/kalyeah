extends GutTest
## Round 13 of the interface plan: how a match feels. A red glow when you are hit or tagged, a weapon button that
## pops when it is ready and says why it cannot be used, and controls that fit your hands (left-handed, small to large).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]
var _motion: bool
var _left: bool
var _size: float


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	_motion = Settings.reduced_motion
	_left = Settings.left_handed
	_size = Settings.control_scale
	Settings.set_reduced_motion(false)
	Settings.set_left_handed(false)
	Settings.set_control_scale(1.0)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)
	Settings.set_left_handed(_left)
	Settings.set_control_scale(_size)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _hud() -> MatchHud:
	var screen_root: Control = autofree(Control.new()) as Control
	screen_root.size = Vector2(720.0, 1280.0)
	add_child(screen_root)
	var hud: MatchHud = MatchHud.new()
	screen_root.add_child(hud)
	hud.layout_controls()
	return hud


# ---- the settings ----------------------------------------------------------------------

func test_a_control_size_snaps_to_the_nearest_of_three() -> void:
	assert_eq(Settings.nearest_control_scale(0.5), 0.85)
	assert_eq(Settings.nearest_control_scale(1.04), 1.0)
	assert_eq(Settings.nearest_control_scale(1.5), 1.2)
	assert_eq(Settings.nearest_control_scale("big"), 1.0, "anything that is not a number is the normal size")


func test_the_hand_and_the_size_are_saved_and_announced_once() -> void:
	watch_signals(Settings)
	Settings.set_left_handed(true)
	Settings.set_left_handed(true)
	Settings.set_control_scale(1.2)
	Settings.set_control_scale(1.2)
	assert_signal_emit_count(Settings, "controls_changed", 2, "a change is announced, a repeat is not")
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	assert_true(file.get_value("input", "left_handed") as bool)
	assert_eq(file.get_value("input", "control_scale") as float, 1.2)


func test_the_options_row_follows_the_settings_both_ways() -> void:
	var options: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(options)
	options.hand_toggle.button_pressed = true
	assert_true(Settings.left_handed, "the switch changes the setting")
	options.size_choice.select(&"small")
	assert_eq(Settings.control_scale, 0.85, "so does the size")
	Settings.set_control_scale(1.2)
	assert_eq(options.size_choice.current, &"large", "a change from elsewhere shows here")
	Settings.set_left_handed(false)
	assert_false(options.hand_toggle.button_pressed)


func test_the_settings_page_has_the_control_options() -> void:
	var column: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	add_child(column)
	SettingsPage.fill(column)
	assert_not_null(column.find_child("ControlOptions", true, false))


# ---- the layout -------------------------------------------------------------------------

func test_left_handed_swaps_the_sides_and_keeps_everything_on_screen() -> void:
	Settings.set_left_handed(true)
	var hud: MatchHud = _hud()
	var screen: Vector2 = hud.get_rect().size
	var stick: Rect2 = hud.dpad.get_rect()
	var first: Rect2 = hud.weapon_buttons[0].get_rect()
	var second: Rect2 = hud.weapon_buttons[1].get_rect()
	assert_gt(stick.get_center().x, screen.x / 2.0, "the stick is on the right")
	for button: Rect2 in [first, second]:
		assert_lt(button.get_center().x, screen.x / 2.0, "the weapons are on the left")
		assert_gte(button.position.x, 0.0)
		assert_false(button.intersects(stick))
	assert_lt(second.get_center().x, first.get_center().x, "weapon 2 is against the edge, as it is on the right")
	assert_almost_eq(second.position.x, MatchHud.WEAPON_MARGIN, 0.5, "against the left margin")
	assert_lt(hud.cancel_zone.get_rect().get_center().x, screen.x / 2.0, "the cancel zone follows the weapons")


func test_changing_the_setting_moves_a_live_hud() -> void:
	var hud: MatchHud = _hud()
	var before: float = hud.dpad.get_rect().get_center().x
	Settings.set_left_handed(true)
	assert_ne(hud.dpad.get_rect().get_center().x, before, "moved without rebuilding")
	Settings.set_left_handed(false)
	assert_eq(hud.dpad.get_rect().get_center().x, before, "and back")


func test_large_controls_are_bigger_and_still_fit() -> void:
	Settings.set_control_scale(1.2)
	var hud: MatchHud = _hud()
	var screen: Vector2 = hud.get_rect().size
	var button: Rect2 = hud.weapon_buttons[0].get_rect()
	assert_almost_eq(button.size.x, MatchHud.WEAPON_SIZE * 1.2, 0.5)
	var stick: Rect2 = hud.dpad.get_rect()
	assert_almost_eq(hud.dpad.scale.x, 1.2, 0.001)
	assert_almost_eq(stick.size.x, MatchHud.DPAD_ZONE * 1.2, 0.5, "a rect is the size it is drawn at")
	var ring: float = DPad.MAX_RADIUS * 1.2
	assert_gte(stick.get_center().x - ring, 0.0, "the disc stays on screen")
	for each: AimButton in hud.weapon_buttons:
		assert_lte(each.get_rect().end.x, screen.x)
		assert_gt(each.get_rect().position.x, stick.get_center().x + ring, "the buttons clear the outer ring")
	assert_false(hud.weapon_buttons[0].get_rect().intersects(hud.weapon_buttons[1].get_rect()))


func test_small_controls_are_still_thumb_sized() -> void:
	Settings.set_control_scale(0.85)
	var hud: MatchHud = _hud()
	assert_gte(hud.weapon_buttons[0].get_rect().size.x, 84.0, "44 pt")


func test_a_larger_stick_grabs_from_further_out() -> void:
	var hud: MatchHud = _hud()
	var centre: Vector2 = hud.dpad.get_global_rect().get_center()
	var probe: Vector2 = centre + Vector2(DPad.MAX_RADIUS * 1.1, 0.0)
	assert_false(hud.dpad._in_zone(probe), "just outside the normal ring")
	Settings.set_control_scale(1.2)
	assert_true(hud.dpad._in_zone(probe), "inside the larger ring")


# ---- the glow ----------------------------------------------------------------------------

func _pulse() -> HurtPulse:
	var pulse: HurtPulse = autofree(HurtPulse.new()) as HurtPulse
	add_child(pulse)
	return pulse


func _run(pulse: HurtPulse, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		pulse.tick(DT)


func test_a_hit_glows_and_fades_away() -> void:
	var pulse: HurtPulse = _pulse()
	assert_false(pulse.active())
	pulse.hit(10, 100)
	assert_true(pulse.active())
	var start: float = pulse.strength
	_run(pulse, HurtPulse.HIT_TIME * 0.5)
	assert_lt(pulse.strength, start, "it fades")
	_run(pulse, HurtPulse.HIT_TIME)
	assert_false(pulse.active(), "and is gone")


func test_a_harder_hit_glows_harder_and_a_tag_hardest() -> void:
	var light: HurtPulse = _pulse()
	light.hit(5, 100)
	var heavy: HurtPulse = _pulse()
	heavy.hit(40, 100)
	var tag: HurtPulse = _pulse()
	tag.tagged()
	assert_gt(heavy.strength, light.strength)
	assert_gt(tag.strength, heavy.strength)
	assert_almost_eq(heavy.strength, HurtPulse.HIT_HEAVY, 0.001, "a hit of a third of your health or more is the heavy glow")
	_run(heavy, HurtPulse.HIT_TIME + 0.05)
	_run(tag, HurtPulse.HIT_TIME + 0.05)
	assert_false(heavy.active())
	assert_true(tag.active(), "a tag lingers longer than a hit")


func test_a_light_hit_does_not_cut_a_tag_short() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.tagged()
	pulse.hit(3, 100)
	assert_almost_eq(pulse.strength, HurtPulse.TAG_PEAK, 0.001)
	_run(pulse, HurtPulse.HIT_TIME + 0.05)
	assert_true(pulse.active(), "still glowing from the tag")


func test_reduce_motion_makes_it_fainter_and_shorter() -> void:
	Settings.set_reduced_motion(true)
	var pulse: HurtPulse = _pulse()
	pulse.tagged()
	assert_almost_eq(pulse.strength, HurtPulse.TAG_PEAK * HurtPulse.REDUCED_PEAK, 0.001)
	_run(pulse, HurtPulse.REDUCED_TIME + 0.05)
	assert_false(pulse.active(), "shorter")


func test_buzzes_are_spaced_out_but_a_tag_always_buzzes() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.hit(5, 100)
	pulse.hit(5, 100)
	pulse.hit(5, 100)
	assert_eq(pulse.buzzes, 1, "a burning field does not buzz without end")
	pulse.tagged()
	assert_eq(pulse.buzzes, 2, "a tag buzzes at once")
	_run(pulse, HurtPulse.BUZZ_GAP + 0.05)
	pulse.hit(5, 100)
	assert_eq(pulse.buzzes, 3, "and after the gap a hit buzzes again")


func test_the_glow_never_takes_a_touch() -> void:
	assert_eq(_pulse().mouse_filter, Control.MOUSE_FILTER_IGNORE)


func test_being_hit_in_a_match_glows_but_a_teammate_being_hit_does_not() -> void:
	var practice: PracticeMatch = _practice()
	var pulse: HurtPulse = practice.hud.pulse
	for id: int in practice.sim.players:
		var state: PlayerState = practice.sim.players[id]
		if state.team == practice.local_team and id != practice.local_id:
			practice.sim.damage(id, 10)
	assert_false(pulse.active(), "an ally's trouble is not yours")
	practice.sim.damage(practice.local_id, 10)
	assert_true(pulse.active())


func test_being_tagged_in_a_match_glows_hardest() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.damage(practice.local_id, 9999)
	assert_almost_eq(practice.hud.pulse.strength, HurtPulse.TAG_PEAK, 0.001)


# ---- the weapon buttons -----------------------------------------------------------------

func test_a_button_pops_when_it_is_ready_and_settles() -> void:
	var button: TouchButton = autofree(TouchButton.new()) as TouchButton
	button.size = Vector2(124.0, 124.0)
	add_child(button)
	button.set_cooldown(2.0, 3.0)
	button.set_cooldown(0.0, 3.0)
	button._process(TouchButton.READY_FLASH_TIME * 0.5)
	assert_gt(button.scale.x, 1.05, "swollen at the middle of the pop")
	button._process(TouchButton.READY_FLASH_TIME)
	assert_eq(button.scale, Vector2.ONE, "back to its size")


func test_reduce_motion_keeps_the_button_still_when_ready() -> void:
	Settings.set_reduced_motion(true)
	var button: TouchButton = autofree(TouchButton.new()) as TouchButton
	button.size = Vector2(124.0, 124.0)
	add_child(button)
	button.set_cooldown(2.0, 3.0)
	button.set_cooldown(0.0, 3.0)
	button._process(TouchButton.READY_FLASH_TIME * 0.5)
	assert_eq(button.scale, Vector2.ONE)


func test_a_button_says_why_it_cannot_be_used() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[practice.local_id]
	practice.advance(DT)
	assert_true(practice.hud.weapon_buttons[0].visible, "in play")
	assert_eq(practice.hud.weapon_buttons[0].blocked_text, "", "nothing in the way")
	player.effects.apply(StatusEffects.Type.STUN, 2.0)
	practice.advance(DT)
	for button: AimButton in practice.hud.weapon_buttons:
		assert_eq(button.blocked_text, "STUNNED")
		assert_true(button.locked)
	player.effects.apply(StatusEffects.Type.POLYMORPH, 2.0)
	practice.advance(DT)
	assert_eq(practice.hud.weapon_buttons[0].blocked_text, "CAN", "polymorphed into a can")
	player.effects.clear()
	practice.advance(DT)
	assert_eq(practice.hud.weapon_buttons[0].blocked_text, "", "gone with the status")


func test_a_slow_does_not_block_a_button() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.players[practice.local_id].effects.apply(StatusEffects.Type.SLOW, 2.0, 0.5)
	practice.advance(DT)
	assert_eq(practice.hud.weapon_buttons[0].blocked_text, "", "slowed, but you can still cast")


func test_every_blocking_status_has_a_short_word() -> void:
	for type: StatusEffects.Type in StatusEffects.HARD_CC:
		assert_true(MatchHud.CAST_BLOCK_TAGS.has(type), "%d" % type)
	assert_true(MatchHud.CAST_BLOCK_TAGS.has(StatusEffects.Type.POLYMORPH))
	for word: String in MatchHud.CAST_BLOCK_TAGS.values():
		assert_lte(word.length(), 8, "%s fits under a weapon button" % word)
