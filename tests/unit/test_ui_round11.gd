extends GutTest
## Round 11 of the interface plan: the in-match cards on the skin, stat bars and a suggested pair on the loadout,
## and a bigger countdown ring.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")

var _loadout: Array[StringName]
var _motion: bool


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout.clear()
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)


func _rules() -> GameRules:
	return load("res://data/rules/game_rules.tres") as GameRules


func _pick_screen(pick_seconds: float = 10.0) -> WeaponPickScreen:
	var rules: GameRules = _rules()
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	screen.open(WeaponPick.new(ids, pick_seconds), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya", true)
	return screen


# ---- the suggested pair -----------------------------------------------------------------

func test_the_suggestion_is_a_homing_attack_and_a_block() -> void:
	var rules: GameRules = _rules()
	var found: Array[StringName] = WeaponPick.suggestion(rules.weapons)
	assert_eq(found.size(), 2)
	assert_ne(found[0], found[1])
	var by_id: Dictionary[StringName, WeaponDef] = {}
	for weapon: WeaponDef in rules.weapons:
		by_id[weapon.id] = weapon
	assert_eq(by_id[found[0]].kind, WeaponDef.Kind.ATTACK)
	assert_eq(by_id[found[0]].shape, WeaponDef.Shape.TARGETED, "an attack that aims itself")
	assert_eq(by_id[found[1]].kind, WeaponDef.Kind.BLOCK)


func test_without_a_block_the_suggestion_falls_back_to_a_crowd_control_and_without_an_attack_it_is_empty() -> void:
	var attack: WeaponDef = WeaponDef.new()
	attack.id = &"a"
	attack.kind = WeaponDef.Kind.ATTACK
	attack.shape = WeaponDef.Shape.TARGETED
	var control: WeaponDef = WeaponDef.new()
	control.id = &"c"
	control.kind = WeaponDef.Kind.CROWD_CONTROL
	var blocker: WeaponDef = WeaponDef.new()
	blocker.id = &"b"
	blocker.kind = WeaponDef.Kind.BLOCK
	assert_eq(WeaponPick.suggestion([attack, control] as Array[WeaponDef]), [&"a", &"c"] as Array[StringName])
	assert_eq(WeaponPick.suggestion([attack, control, blocker] as Array[WeaponDef]), [&"a", &"b"] as Array[StringName], "a block beats a control")
	assert_eq(WeaponPick.suggestion([control, blocker] as Array[WeaponDef]), [] as Array[StringName], "no attack, no suggestion")
	assert_eq(WeaponPick.suggestion([] as Array[WeaponDef]), [] as Array[StringName])


func test_the_suggestion_button_shows_for_a_new_pick_names_the_pair_and_takes_it() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	var button: Button = screen.find_child("SuggestButton", true, false) as Button
	assert_not_null(button)
	assert_true(button.visible, "nothing picked yet")
	for id: StringName in screen.suggestion:
		assert_true(button.text.contains((screen._defs[id] as WeaponDef).short_name), "names %s" % id)
	assert_gte(button.custom_minimum_size.y, 64.0)
	button.pressed.emit()
	assert_eq(screen.pick.picks, screen.suggestion, "exactly the pair")
	assert_false(button.visible, "nothing more to suggest")
	assert_false(screen._ready_button.disabled, "ready to go")


func test_taking_the_suggestion_replaces_a_half_made_pick() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	screen.tap(&"lata")
	assert_true((screen.find_child("SuggestButton", true, false) as Button).visible, "still one slot free")
	screen.use_suggestion()
	assert_eq(screen.pick.picks.size(), 2)
	assert_true(screen.pick.picks.has(screen.suggestion[0]))
	assert_true(screen.pick.picks.has(screen.suggestion[1]))
	assert_false(screen.pick.picks.has(&"lata"))


func test_the_suggestion_is_not_offered_when_swapping_weapons() -> void:
	var rules: GameRules = _rules()
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	screen.open_swap(WeaponPick.new(ids, 1.0), rules.weapons, "")
	await wait_process_frames(2)
	assert_false((screen.find_child("SuggestButton", true, false) as Button).visible)
	screen.use_suggestion()
	assert_eq(screen.pick.picks.size(), 0, "and it does nothing")


# ---- bars on the cards ------------------------------------------------------------------

func test_the_info_panel_has_meters_for_the_weapons_stats_and_keeps_its_exact_numbers() -> void:
	var rules: GameRules = _rules()
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	var maxima: Dictionary = WeaponsPage.stat_maxima(rules.weapons)
	for weapon: WeaponDef in rules.weapons:
		screen.show_info(weapon.id)
		var bars: Control = screen.find_child("Bars", true, false)
		var figures: Array[Dictionary] = WeaponsPage.figures_of(weapon)
		assert_eq(bars.get_child_count(), figures.size(), weapon.id)
		for i: int in figures.size():
			var meter: ProgressBar = bars.get_child(i).find_child("Meter", true, false) as ProgressBar
			assert_almost_eq(meter.value, WeaponsPage.fraction_of(String(figures[i]["caption"]), weapon, maxima), 0.002)
		assert_eq((screen.find_child("Stats", true, false) as Label).text, LoadoutCards.stat_line(weapon), "the exact numbers, with their per hit or per tick wording")


func test_showing_a_weapon_replaces_the_meters_instead_of_adding_to_them() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	screen.show_info(&"bato_light")
	screen.show_info(&"jacks")
	await wait_process_frames(2)
	assert_eq(screen.find_children("Bars", "", true, false).size(), 1)


func test_a_tile_taps_through_to_the_pick_and_shows_the_weapon() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	screen.tap(&"bato_light")
	assert_eq(screen.pick.picks, [&"bato_light"] as Array[StringName])
	assert_eq(screen.focused, &"bato_light")
	screen.tap(&"jacks")
	assert_eq(screen.focused, &"jacks", "the panel follows the last tile touched")


func test_a_slot_names_its_weapon_the_way_its_tile_does() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	screen.tap(&"tsinelas_heavy")
	assert_eq(screen._sockets[0].weapon.display_name, "Tsinelas · Heavy")
	assert_eq((screen.find_child("Weapon_tsinelas_heavy", true, false).find_child("WeaponName", true, false) as Label).text, "Tsinelas · Heavy")


# ---- the countdown ring ------------------------------------------------------------------

func test_the_loadout_ring_is_big_and_pulses_only_in_the_last_seconds_with_motion_on() -> void:
	var screen: WeaponPickScreen = _pick_screen()
	await wait_process_frames(2)
	assert_gte(screen._ring.custom_minimum_size.x, 100.0)
	var ring: CountdownRing = autofree(CountdownRing.new()) as CountdownRing
	add_child(ring)
	ring.show_time(8.0, 10.0)
	assert_false(ring.is_urgent())
	assert_false(ring.is_processing())
	ring.show_time(2.0, 10.0)
	assert_true(ring.is_urgent())
	assert_false(ring.is_processing(), "Reduce motion: a steady ring")
	Settings.set_reduced_motion(false)
	ring.show_time(2.0, 10.0)
	assert_true(ring.is_processing(), "it pulses")
	ring.show_time(0.0, 10.0)
	assert_false(ring.is_urgent(), "and stops when time is up")
	assert_false(ring.is_processing())


# ---- the in-match cards on the skin ------------------------------------------------------

func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_the_status_pill_and_the_tagged_sheet_wear_the_skin_and_the_opening_words_have_no_card() -> void:
	var practice: PracticeMatch = _practice()
	var pill: Label = practice.hud.feedback.message
	assert_true(pill.get_theme_stylebox("normal") is StyleBoxTexture, "status messages are night pills")
	assert_false(practice.hud.intro_bar is Panel, "the opening words stand on the street: no card behind them")
	var band: PanelContainer = practice.hud.tagged_panel.find_children("*", "PanelContainer", true, false)[0] as PanelContainer
	assert_true(band.get_theme_stylebox("panel") is StyleBoxTexture, "and the tagged sheet")


func test_a_pill_is_tall_enough_for_its_corners_and_the_scoreboard_for_its_frame() -> void:
	var box: StyleBoxTexture = UiSkin.pill()
	assert_eq(box.content_margin_left, 18.0)
	var label: Label = autofree(Label.new()) as Label
	label.theme_type_variation = &"HudPill"
	label.text = "HOLD THE BASE! 4"
	label.add_theme_font_size_override("font_size", 34)
	add_child(label)
	assert_gte(label.get_combined_minimum_size().y, 2.0 * 20.0, "taller than twice the corner radius, so the corners never squeeze")
	assert_gte(MatchHud.SCORE_SIZE.y, 2.0 * (18.0 + UiSkin.PLATE_PAD) - 2.0 * UiSkin.PLATE_PAD, "the scoreboard is tall enough for its radius")


func test_the_scoreboard_still_reads_the_same() -> void:
	var board: Scoreboard = autofree(Scoreboard.new()) as Scoreboard
	add_child(board)
	var score: MatchScore = MatchScore.new(3)
	score.points = [2, 1]
	board.show_score(score, 0, 75.0)
	assert_eq(board.summary(), "YOU 2 - 1 THEM   1:15")
