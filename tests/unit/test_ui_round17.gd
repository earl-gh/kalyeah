extends GutTest
## Round 17 of the interface plan: the match at a glance. A minimap that is big enough to read (and a choice of
## three sizes), faces and rings that grow with it, weapon kinds in words, and labels that read over the street.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _size: float
var _loadout: Array[StringName]


func before_each() -> void:
	_size = Settings.minimap_scale
	_loadout = Session.saved_loadout.duplicate()
	Settings.set_minimap_scale(1.3)


func after_each() -> void:
	Settings.set_minimap_scale(_size)
	Session.saved_loadout = _loadout
	Session.save()


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 5:
		practice.advance(DT)
	return practice


# ---- the setting ---------------------------------------------------------------------------

func test_the_minimap_size_snaps_to_three_and_defaults_to_the_middle_one() -> void:
	assert_eq(Settings.nearest_minimap_scale(0.7), 1.0)
	assert_eq(Settings.nearest_minimap_scale(1.35), 1.3)
	assert_eq(Settings.nearest_minimap_scale(9.0), 1.6)
	assert_eq(Settings.nearest_minimap_scale("big"), 1.3, "anything that is not a number is the normal size")
	var fresh: Node = autofree(preload("res://scripts/autoload/settings.gd").new()) as Node
	assert_eq(fresh.get("minimap_scale"), 1.3, "bigger than the old 100 px by default")


func test_a_minimap_size_is_saved_and_announced_once() -> void:
	watch_signals(Settings)
	Settings.set_minimap_scale(1.6)
	Settings.set_minimap_scale(1.6)
	assert_signal_emit_count(Settings, "controls_changed", 1)
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	assert_eq(file.get_value("input", "minimap_scale") as float, 1.6)


func test_the_options_row_follows_the_minimap_setting_both_ways() -> void:
	var options: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(options)
	assert_eq(options.map_choice.current, &"normal")
	options.map_choice.select(&"large")
	assert_eq(Settings.minimap_scale, 1.6)
	Settings.set_minimap_scale(1.0)
	assert_eq(options.map_choice.current, &"small", "a change from elsewhere shows here")


# ---- the layout ----------------------------------------------------------------------------

func test_the_minimap_is_the_base_size_times_the_choice_and_stays_one_to_two() -> void:
	for pair: Array in [[1.0, 100.0], [1.3, 130.0], [1.6, 160.0]]:
		Settings.set_minimap_scale(pair[0] as float)
		var map: Vector2 = MatchHud.minimap_size()
		assert_almost_eq(map.x, pair[1] as float, 0.01)
		assert_almost_eq(map.y / map.x, 2.0, 0.001, "1:2 like the street")


func test_a_live_hud_resizes_its_minimap_and_keeps_the_gear_and_the_feed_clear_of_it() -> void:
	var practice: PracticeMatch = _practice()
	var hud: MatchHud = practice.hud
	var screen_width: float = hud.get_rect().size.x
	for scale_value: float in [1.0, 1.3, 1.6]:
		Settings.set_minimap_scale(scale_value)
		await wait_process_frames(2)
		var map: Rect2 = hud.minimap.get_rect()
		assert_almost_eq(map.size.x, 100.0 * scale_value, 0.5)
		assert_gte(hud.settings_button.get_rect().position.x, map.end.x, "the gear is beside it, not over it")
		assert_gte(hud.feed_box.get_rect().position.y, map.end.y, "the feed is under it")
		assert_lt(map.end.x, screen_width - MatchHud.SCORE_SIZE.x - MatchHud.MARGIN, "clear of the score card")
		assert_almost_eq(hud.arrows.minimap_bottom, map.end.y, 0.5, "the arrows know where it ends")


func test_the_direction_arrows_stay_below_the_minimap_at_every_size() -> void:
	var practice: PracticeMatch = _practice()
	for scale_value: float in [1.0, 1.6]:
		Settings.set_minimap_scale(scale_value)
		await wait_process_frames(2)
		for i: int in 10:
			practice.hud.arrows.tick(DT)
		var bottom: float = practice.hud.minimap.get_rect().end.y
		for arrow: Dictionary in practice.hud.arrows.arrows:
			assert_gte((arrow["point"] as Vector2).y, bottom, "the %s arrow is under the minimap at %s" % [arrow["kind"], scale_value])


func test_faces_rings_and_pings_grow_with_the_minimap() -> void:
	var practice: PracticeMatch = _practice()
	Settings.set_minimap_scale(1.0)
	await wait_process_frames(2)
	assert_almost_eq(practice.hud.minimap.unit(), 1.0, 0.01)
	Settings.set_minimap_scale(1.6)
	await wait_process_frames(2)
	assert_almost_eq(practice.hud.minimap.unit(), 1.6, 0.01)
	assert_gt(LaneMinimap.FACE, 8.0, "faces are a little bigger than they were")


# ---- the words -----------------------------------------------------------------------------

func test_weapon_kinds_are_said_in_words_everywhere() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var seen: Dictionary[String, bool] = {}
	for def: WeaponDef in rules.weapons:
		seen[def.kind_label()] = true
		assert_eq(def.kind_label(), String(WeaponsPage.KIND_NAMES[def.kind]), "%s: the button and the Book say the same word" % def.id)
	assert_eq(seen.keys().size(), 4)
	assert_false(seen.has("CC") or seen.has("ATK") or seen.has("BLK"))


func test_the_longest_kind_word_fits_its_tag_under_a_weapon_button() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	for word: String in ["ATTACK", "CONTROL", "BLOCK", "HEAL"]:
		var width: float = font.get_string_size(word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TouchButton.TAG_FONT_SIZE).x + 20.0
		assert_lte(width, MatchHud.WEAPON_SIZE * 1.05, "%s fits under a %d px button" % [word, MatchHud.WEAPON_SIZE])


func test_the_in_match_buttons_show_the_kind_word() -> void:
	var practice: PracticeMatch = _practice()
	var first: AimButton = practice.hud.weapon_buttons[0]
	assert_true(["Attack", "Control", "Block", "Heal"].has(first.sub_text))


func test_the_direction_label_sits_on_a_pill() -> void:
	assert_gt(DirectionArrows.LABEL_PILL.a, 0.5, "dark enough to read over the street and the kids' bars")
