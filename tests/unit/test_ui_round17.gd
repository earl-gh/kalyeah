extends GutTest
## Round 17 of the interface plan, what is left of it with the whole street on screen: weapon kinds in words.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()


func after_each() -> void:
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
