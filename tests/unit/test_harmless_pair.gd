extends GutTest
## A pair of weapons that cannot hurt anyone (traps, shields, heals) is allowed (D10), but the picker says so.

var _rules: GameRules


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules


func _defs() -> Dictionary[StringName, WeaponDef]:
	var defs: Dictionary[StringName, WeaponDef] = {}
	for def: WeaponDef in _rules.weapons:
		defs[def.id] = def
	return defs


func test_a_pair_with_no_damage_is_found() -> void:
	var defs: Dictionary[StringName, WeaponDef] = _defs()
	assert_true(WeaponPick.deals_no_damage([&"papel_trap", &"papel_shield"] as Array[StringName], defs))
	assert_true(WeaponPick.deals_no_damage([&"heal", &"langit_lupa"] as Array[StringName], defs))
	assert_false(WeaponPick.deals_no_damage([&"papel_shield", &"bato_light"] as Array[StringName], defs), "one that hurts is enough")
	assert_false(WeaponPick.deals_no_damage([&"jacks", &"papel_trap"] as Array[StringName], defs), "jacks tick for damage")
	assert_false(WeaponPick.deals_no_damage([] as Array[StringName], defs), "nothing picked is not a harmless pair")
	assert_false(WeaponPick.deals_no_damage([&"nonsense"] as Array[StringName], defs), "unknown ids are not claimed harmless")


func test_the_picker_says_so_only_for_a_full_harmless_pair() -> void:
	var screen: WeaponPickScreen = autofree(WeaponPickScreen.new()) as WeaponPickScreen
	add_child(screen)
	var ids: Array[StringName] = []
	for def: WeaponDef in _rules.weapons:
		ids.append(def.id)
	var pick: WeaponPick = WeaponPick.new(ids, 30.0)
	screen.open(pick, _rules.weapons, "", RandomNumberGenerator.new())
	pick.toggle(&"papel_trap")
	assert_ne(screen.selection_hint(), "This pair deals no damage", "one weapon is not a pair yet")
	pick.toggle(&"papel_shield")
	assert_eq(screen.selection_hint(), "This pair deals no damage")
	pick.toggle(&"papel_shield")
	pick.toggle(&"bato_light")
	assert_ne(screen.selection_hint(), "This pair deals no damage")
