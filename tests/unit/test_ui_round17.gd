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
