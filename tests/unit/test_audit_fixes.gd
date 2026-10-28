extends GutTest
## The first fixes from the interaction audit (docs/audits/2026-10-22-interaction-audit.md): P0 and P1.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _kind: StringName
var _history: String


func before_each() -> void:
	_kind = Session.practice_kind
	_history = MatchHistory.path
	MatchHistory.path = "user://audit_history_test.cfg"
	MatchHistory.clear()


func after_each() -> void:
	Session.practice_kind = _kind
	MatchHistory.clear()
	MatchHistory.path = _history


func _practice(kind: StringName = &"training") -> PracticeMatch:
	Session.practice_kind = kind
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_the_match_stands_still_while_the_menu_is_open_and_runs_again_after() -> void:
	var practice: PracticeMatch = _practice(&"match")
	for i: int in 10:
		practice.advance(DT)
	practice.hud.toggle_menu()
	practice.advance(DT)  # the one neutral tick on the way in
	var clock: float = practice.sim.match_time
	var tick: int = practice.sim.tick
	for i: int in 30:
		practice.advance(DT)
	assert_eq(practice.sim.tick, tick, "no sim tick while the menu is open")
	assert_eq(practice.sim.match_time, clock)
	practice.hud.menu.close()
	for i: int in 5:
		practice.advance(DT)
	assert_gt(practice.sim.tick, tick, "and it runs again after Resume")


func test_the_first_run_walkthrough_holds_the_match() -> void:
	var practice: PracticeMatch = _practice(&"match")
	practice.hud.coach.start(5.0, 3)
	practice.advance(DT)
	var tick: int = practice.sim.tick
	for i: int in 30:
		practice.advance(DT)
	assert_eq(practice.sim.tick, tick, "the bots wait while the player reads")


func test_leaving_a_practice_match_asks_first_and_counts_as_a_loss() -> void:
	var practice: PracticeMatch = _practice(&"match")
	var menu: MatchMenu = practice.hud.menu
	var exits: Array[bool] = []
	menu.exit_pressed.connect(func() -> void: exits.append(true))
	menu.request_exit()
	assert_eq(exits.size(), 0, "it asks before leaving")
	assert_true(is_instance_valid(menu._confirmation) and menu._confirmation.is_open)
	assert_false(practice.record_if_abandoned(), "a match only just started leaves no trace")
	practice.sim.match_time = PracticeMatch.ABANDON_AFTER + 1.0
	practice.sim.score.points = [1, 0]
	assert_true(practice.record_if_abandoned())
	var entry: Dictionary = MatchHistory.entries()[0]
	assert_false(entry["won"], "it counts as a loss")
	assert_eq(entry["mine"], 1)
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	assert_false(practice.record_if_abandoned(), "a finished match was already recorded")


func test_leaving_training_is_immediate() -> void:
	var practice: PracticeMatch = _practice(&"training")
	var exits: Array[bool] = []
	practice.hud.menu.exit_pressed.connect(func() -> void: exits.append(true))
	practice.hud.menu.request_exit()
	assert_eq(exits.size(), 1, "a sandbox has nothing to lose")


func test_a_loadout_suggestion_slot_keeps_its_height_whether_or_not_the_button_shows() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	screen.open(WeaponPick.new(ids, 10.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya", true)
	await wait_process_frames(2)
	var grid: Control = screen.find_child("Weapons", true, false) as Control
	if grid == null:
		grid = screen._scroll as Control
	var before: float = grid.get_global_rect().position.y
	screen.tap(&"lata")
	screen.tap(&"jacks")
	await wait_process_frames(2)
	assert_almost_eq(grid.get_global_rect().position.y, before, 0.5, "picking two weapons does not move the grid")
	screen.tap(&"jacks")
	await wait_process_frames(2)
	assert_almost_eq(grid.get_global_rect().position.y, before, 0.5, "and neither does un-picking one")


func test_the_hint_is_a_full_width_line_not_a_letter_column() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	screen.open(WeaponPick.new(ids, 10.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya", true)
	await wait_process_frames(2)
	assert_gt(screen._hint.size.x, 400.0, "the caption has the whole line")
	assert_lt(screen._hint.size.y, 80.0, "one line tall, not wrapped letter by letter")


func test_the_first_to_buttons_fit_a_phone_next_to_their_caption() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	phone.add_child(view)
	view.set_anchors_preset(Control.PRESET_TOP_LEFT)
	view.size = phone.size
	view.show_room({"code": "ABC234XY", "host": 1, "team_size": 3, "first_to": 7, "members": []}, 1)
	await wait_process_frames(3)
	var row: Control = view.first_to_buttons[3].get_parent() as Control
	assert_lte(row.get_combined_minimum_size().x, 640.0, "three numbers and a caption in one row")
	assert_eq(view.first_to_buttons[5].text, "5")
	assert_eq(view.first_to_buttons[5].accessibility_name, "First to 5")


func test_cards_pass_touches_on_so_a_drag_that_starts_on_one_scrolls_the_page() -> void:
	var card: PanelContainer = autofree(UiKit.card(Control.new())) as PanelContainer
	assert_eq(card.mouse_filter, Control.MOUSE_FILTER_PASS)


# ---- P2 ----

func test_the_room_status_says_how_many_players_each_team_has() -> void:
	var room: Dictionary = {"host": 1, "team_size": 3, "members": [{"player_id": 1, "team": 0, "ready": true}]}
	assert_eq(HotspotRoomView._readiness(room), "Waiting for players · Blue 1 of 3 · Red 0 of 3")


func test_one_name_for_a_knocked_out_kid() -> void:
	assert_eq(StatusEffects.new().display_names().size(), 0)
	assert_eq(MatchHud.CAST_BLOCK_TAGS[StatusEffects.Type.KNOCKOUT], "OUT")


func test_history_has_a_play_button() -> void:
	var history: HistoryPage = autofree(HistoryPage.new()) as HistoryPage
	add_child(history)
	history.refresh()
	var plays: Array[bool] = []
	history.play_pressed.connect(func() -> void: plays.append(true))
	(history.find_child("PlayButton", true, false) as Button).pressed.emit()
	assert_eq(plays.size(), 1, "an empty History leads to a match")


func test_a_tap_skips_the_opening_show_offline() -> void:
	Session.practice_kind = &"match"
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.advance(DT)
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO)
	assert_string_contains(practice.hud.intro_sub.text, "TAP TO SKIP")
	var tap: InputEventScreenTouch = InputEventScreenTouch.new()
	tap.pressed = true
	practice._input(tap)
	assert_eq(practice.sim.phase, MatchSim.Phase.PLAYING)


func test_clear_empties_the_code_and_its_error() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789")) as UiCodeBoxes
	add_child(boxes)
	boxes.set_text("ABCD2345")
	boxes.set_error(true)
	boxes.set_text("")
	boxes.set_error(false)
	assert_eq(boxes.get_text(), "")
	assert_false(boxes.has_error)
