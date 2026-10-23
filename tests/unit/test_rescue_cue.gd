extends GutTest
## The RESCUE marker over your team's tagged box: shown while a teammate waits there and you are free to run to them.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")


func _setup() -> Array:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	sim.add_player(3, 1)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	var overhead: OverheadHud = OverheadHud.new()
	add_child_autofree(overhead)
	overhead.setup(sim, null, 1, 0)
	return [sim, overhead]


func test_no_marker_while_nobody_is_tagged() -> void:
	var parts: Array = _setup()
	assert_false((parts[1] as OverheadHud).rescue_cue_visible())


func test_a_tagged_teammate_shows_the_marker_to_a_free_player() -> void:
	var parts: Array = _setup()
	(parts[0] as MatchSim).kill(2)
	assert_true((parts[1] as OverheadHud).rescue_cue_visible())


func test_a_tagged_enemy_does_not() -> void:
	var parts: Array = _setup()
	(parts[0] as MatchSim).kill(3)
	assert_false((parts[1] as OverheadHud).rescue_cue_visible(), "it is for rescuing your own team")


func test_a_tagged_player_has_nobody_to_run_with() -> void:
	var parts: Array = _setup()
	(parts[0] as MatchSim).kill(2)
	(parts[0] as MatchSim).kill(1)
	assert_false((parts[1] as OverheadHud).rescue_cue_visible(), "you are in the box yourself: the tagged panel shows")


func test_no_marker_outside_play() -> void:
	var parts: Array = _setup()
	(parts[0] as MatchSim).kill(2)
	(parts[0] as MatchSim).phase = MatchSim.Phase.INTRO
	assert_false((parts[1] as OverheadHud).rescue_cue_visible())
