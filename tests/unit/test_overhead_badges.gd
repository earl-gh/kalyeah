extends GutTest
## The kids' overhead marks (owner): every bar says its player's name, every bar has a team badge with the player's
## number (the room's numbering), bars that would overlap stack so every number shows, and a tagged kid on the sidewalk has no ring.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _blind: bool
var _motion: bool


func before_each() -> void:
	_blind = Settings.colorblind
	_motion = Settings.reduced_motion
	Settings.set_colorblind(false)
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Settings.set_colorblind(_blind)
	Settings.set_reduced_motion(_motion)


func _hud(sim: MatchSim, local_id: int = 1) -> OverheadHud:
	var hud: OverheadHud = OverheadHud.new()
	add_child_autofree(hud)
	hud.setup(sim, null, local_id, sim.players[local_id].team)
	return hud


func _three_a_side() -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 2)
	for id: int in [1, 2, 3]:
		sim.add_player(id, 0)
	for id: int in [4, 5, 6]:
		sim.add_player(id, 1)
	return sim


func test_every_bar_says_its_players_name_yours_allies_and_enemies() -> void:
	var sim: MatchSim = _three_a_side()
	sim.assign_characters()
	var hud: OverheadHud = _hud(sim)
	hud.names[1] = "Gabo"
	hud.names[5] = "Mika"
	assert_eq(hud.label_for(sim.players[1]), "Gabo", "yours: your name, not You")
	assert_eq(hud.label_for(sim.players[5]), "Mika", "an enemy with a name shows it")
	for id: int in [2, 3, 4, 6]:
		var character: CharacterDef = null
		for candidate: Variant in RULES.characters:
			if (candidate as CharacterDef).id == sim.players[id].character_id:
				character = candidate as CharacterDef
		assert_not_null(character, "kid %d has a character" % id)
		assert_eq(hud.label_for(sim.players[id]), character.display_name, "a bot is named after its kid")
	hud.names[2] = ""
	assert_ne(hud.label_for(sim.players[2]), "", "an empty name falls back to the kid's")
	assert_false(hud.has_method("_draw_count"), "the health number is gone")


func test_every_kid_has_the_number_the_room_shows_blue_first_then_red() -> void:
	var sim: MatchSim = _three_a_side()
	var hud: OverheadHud = _hud(sim)
	for id: int in [1, 2, 3, 4, 5, 6]:
		assert_eq(hud.player_number(sim.players[id]), id, "kid %d" % id)
		var state: PlayerState = sim.players[id]
		var row: int = (id - 1) % 3
		assert_eq(hud.player_number(state), RoomPlayerCards.slot_number(state.team, row, 3), "the room's own numbering")


func test_numbers_follow_the_larger_team_like_a_room_with_a_free_place() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 2)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	for id: int in [3, 4, 5]:
		sim.add_player(id, 1)
	var hud: OverheadHud = _hud(sim)
	assert_eq([hud.player_number(sim.players[1]), hud.player_number(sim.players[2])], [1, 2])
	assert_eq([hud.player_number(sim.players[3]), hud.player_number(sim.players[4]), hud.player_number(sim.players[5])], [4, 5, 6], "red starts after blue's three places")


func test_the_badge_is_the_teams_colour_blue_yours_red_theirs_and_blue_orange_for_colour_blind() -> void:
	var sim: MatchSim = _three_a_side()
	var hud: OverheadHud = _hud(sim)
	for id: int in [1, 2, 3]:
		assert_eq(hud.badge_color(sim.players[id]), Palette.TEAM_OWN, "you and your team")
	for id: int in [4, 5, 6]:
		assert_eq(hud.badge_color(sim.players[id]), Palette.TEAM_ENEMY, "the other team")
	Settings.set_colorblind(true)
	assert_eq(hud.badge_color(sim.players[2]), TeamColors.BLIND_OWN)
	assert_eq(hud.badge_color(sim.players[4]), TeamColors.BLIND_ENEMY)
	assert_gt(TeamColors.BLIND_OWN.get_luminance(), 0.0)


func test_the_badge_number_is_the_street_text_size_and_taller_than_the_bar() -> void:
	assert_eq(OverheadHud.BADGE_FONT, UiMetrics.WORLD, "the street text size")
	assert_gt(OverheadHud.BADGE_SIZE, OverheadHud.BAR_SIZE.y * 0.75 + OverheadHud.BAR_OUTLINE * 2.0, "a little taller than the bar")
	assert_gte(OverheadHud.BADGE_SIZE, float(OverheadHud.BADGE_FONT), "the number fits in it")


func test_kids_whose_bars_would_overlap_stack_so_every_number_shows() -> void:
	var sim: MatchSim = _three_a_side()
	var hud: OverheadHud = _hud(sim)
	var rect: Rect2 = Rect2(Vector2(300.0, 600.0), OverheadHud.BAR_SIZE * 0.75)
	var first: Rect2 = hud._clear_of_others(rect)
	var second: Rect2 = hud._clear_of_others(rect)
	var third: Rect2 = hud._clear_of_others(Rect2(rect.position + Vector2(30.0, 4.0), rect.size))
	assert_eq(first, rect, "the first bar stays on its head")
	assert_lt(second.end.y, first.position.y + 0.001, "the second sits above it")
	assert_lt(third.end.y, second.position.y + 0.001, "and the third above that")
	var far: Rect2 = hud._clear_of_others(Rect2(rect.position + Vector2(200.0, 0.0), rect.size))
	assert_eq(far, Rect2(rect.position + Vector2(200.0, 0.0), rect.size), "a kid on its own is not moved")


func test_a_stack_never_climbs_under_the_header() -> void:
	var hud: OverheadHud = _hud(_three_a_side())
	hud.top_limit = 120.0
	var rect: Rect2 = Rect2(Vector2(300.0, 150.0), OverheadHud.BAR_SIZE * 0.75)
	for i: int in 4:
		rect = hud._clear_of_others(rect)
	assert_gte(rect.position.y - OverheadHud.BAR_OUTLINE - OverheadHud.LABEL_LINE, 120.0 - 0.001, "the last group stops at the header's edge")


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_a_kid_against_the_side_wall_keeps_its_badge_on_screen() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	var hud: OverheadHud = practice.overhead()
	var width: float = hud.size.x
	var kid: PlayerState = practice.sim.players[practice.local_id]
	for x: float in [-LAYOUT.lane_width / 2.0 + kid.radius, LAYOUT.lane_width / 2.0 - kid.radius]:
		kid.position = Vector2(x, 0.0)
		var rect: Rect2 = hud._bar_rect(kid) as Rect2
		assert_gte(rect.position.x + OverheadHud.BADGE_OVERLAP - OverheadHud.BADGE_SIZE, 0.0, "the badge starts inside the screen at x %s" % x)
		assert_lte(rect.end.x, width, "and the bar ends inside it")
