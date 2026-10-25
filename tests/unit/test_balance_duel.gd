extends GutTest
## The bot-versus-bot duel behind the balance numbers (BalanceDuel).

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")

var _hurts: Array[StringName] = [&"bato_light", &"tsinelas_light"]
var _harmless: Array[StringName] = [&"papel_shield", &"papel_trap"]


func test_every_two_different_weapons_make_one_loadout() -> void:
	var count: int = RULES.weapons.size()
	var loadouts: Array[Array] = BalanceDuel.all_loadouts(RULES)
	assert_eq(loadouts.size(), count * (count - 1) / 2)
	var seen: Dictionary[String, bool] = {}
	for pair: Array in loadouts:
		assert_ne(pair[0], pair[1])
		var key: String = "%s+%s" % [pair[0], pair[1]]
		assert_false(seen.has(key), "no loadout twice")
		seen[key] = true


func test_the_arena_has_no_cover_and_leaves_the_real_street_alone() -> void:
	var walled: MapLayout = LAYOUT.duplicate() as MapLayout
	walled.obstacles = [Rect2(-13.5, 10.5, 3.0, 0.5)] as Array[Rect2]
	var open: MapLayout = BalanceDuel.arena(walled)
	assert_true(open.obstacles.is_empty())
	assert_false(walled.obstacles.is_empty(), "the street it was made from keeps its cover")
	assert_eq(open.lane_length, LAYOUT.lane_length)


func test_a_duel_is_the_same_every_time_for_one_seed() -> void:
	var first: Dictionary = BalanceDuel.run(RULES, LAYOUT, _hurts, [&"gunting_heavy", &"jacks"], 5, 30.0)
	var second: Dictionary = BalanceDuel.run(RULES, LAYOUT, _hurts, [&"gunting_heavy", &"jacks"], 5, 30.0)
	assert_eq(first, second)


func test_a_loadout_that_hurts_beats_one_that_cannot() -> void:
	var result: Dictionary = BalanceDuel.run(RULES, LAYOUT, _hurts, _harmless, 1)
	assert_eq(result["winner"], 0)
	assert_gt(result["hp"][0] as int, 0)
	assert_eq(result["hp"][1], 0, "the other is the one tagged")


func test_the_winner_is_the_same_loadout_with_the_sides_swapped() -> void:
	var swapped: Dictionary = BalanceDuel.run(RULES, LAYOUT, _harmless, _hurts, 1)
	assert_eq(swapped["winner"], 1, "now the second team's loadout wins")


func test_two_loadouts_that_cannot_hurt_draw_at_the_time_limit() -> void:
	var result: Dictionary = BalanceDuel.run(RULES, LAYOUT, _harmless, [&"langit_lupa", &"papel_shield"], 1, 8.0)
	assert_eq(result["winner"], BalanceDuel.NO_WINNER)
	assert_almost_eq(result["seconds"] as float, 8.0, 0.1)
	assert_eq(result["hp"], [RULES.player_max_hp, RULES.player_max_hp])


func test_a_decided_duel_ends_when_someone_is_tagged() -> void:
	var result: Dictionary = BalanceDuel.run(RULES, LAYOUT, _hurts, _harmless, 2)
	assert_lt(result["seconds"] as float, BalanceDuel.MAX_SECONDS)
