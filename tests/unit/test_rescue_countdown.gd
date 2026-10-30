extends GutTest
## The rescue area's countdown (owner, 2026-10-29): each team's rescue area counts down `rescue_cooldown` (10 s) from the start,
## then is ready whether or not anyone waits; a rescue starts it again, and nobody can rescue while it counts. A tagged player is
## only ever freed by a teammate: there is no walking out by waiting.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 23)
	sim.add_player(1, 0)
	sim.add_player(2, 0)
	sim.add_player(3, 1)
	return sim


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func _in_area(sim: MatchSim, id: int) -> void:
	sim.players[id].position = LAYOUT.rescue_rect(sim.side_for_team(sim.players[id].team)).get_center()


func test_the_shipped_countdown_is_ten_seconds_and_runs_from_the_start() -> void:
	assert_eq(RULES.rescue_cooldown, 10.0)
	var sim: MatchSim = _sim()
	assert_eq(sim.rescue_ready_left, [10.0, 10.0] as Array[float], "both areas count from the start")
	_wait(sim, 4.0)
	assert_almost_eq(sim.rescue_ready_left[0], 6.0, DT * 2.0, "it counts with nobody tagged")
	_wait(sim, 6.5)
	assert_eq(sim.rescue_ready_left, [0.0, 0.0] as Array[float], "ready, even with nobody to rescue")


func test_nobody_can_rescue_while_the_area_counts_down() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	_in_area(sim, 2)
	_wait(sim, RULES.rescue_cooldown - 0.5)
	assert_true(sim.players[1].tagged, "the teammate stood there the whole countdown, but it does not count")
	assert_eq(sim.rescue_rescuer[0], -1, "no rescue is under way")
	assert_eq(sim.rescue_elapsed[0], 0.0)
	_wait(sim, 0.5 + RULES.rescue_hold_time + 0.2)
	assert_false(sim.players[1].tagged, "once ready, the usual hold frees them")


func test_a_rescue_starts_the_countdown_again() -> void:
	var sim: MatchSim = _sim()
	_wait(sim, RULES.rescue_cooldown + 0.1)
	sim.kill(1)
	_in_area(sim, 2)
	_wait(sim, RULES.rescue_hold_time + 0.1)
	assert_false(sim.players[1].tagged)
	assert_gt(sim.rescue_ready_left[0], RULES.rescue_cooldown - 0.5, "counting again from the rescue")
	assert_eq(sim.rescue_ready_left[1], 0.0, "the other team's area is not touched")
	sim.kill(1)
	_in_area(sim, 2)
	_wait(sim, RULES.rescue_hold_time + 0.5)
	assert_true(sim.players[1].tagged, "a second rescue has to wait for the countdown")


func test_a_new_round_starts_the_countdown_over() -> void:
	var sim: MatchSim = _sim()
	_wait(sim, RULES.rescue_cooldown + 0.1)
	sim.kill(3)  # the whole other team: a point
	sim.step(DT)
	assert_eq(sim.phase, MatchSim.Phase.POINT_FREEZE)
	_wait(sim, RULES.point_freeze_time + 0.1)
	assert_eq(sim.phase, MatchSim.Phase.PLAYING)
	assert_gt(sim.rescue_ready_left[0], RULES.rescue_cooldown - 0.5, "the next round starts counting from the top")


func test_a_tagged_player_is_never_freed_by_waiting() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	_wait(sim, 90.0)
	assert_true(sim.players[1].tagged, "only a teammate frees a tagged player")


func test_the_countdown_reaches_the_clients() -> void:
	var sim: MatchSim = _sim()
	_wait(sim, 3.0)
	var mirror: MatchSim = _sim()
	Snapshot.apply(mirror, Snapshot.capture(sim, {} as Dictionary[int, int]), 1)
	assert_almost_eq(mirror.rescue_ready_left[0], sim.rescue_ready_left[0], 0.001)
	assert_almost_eq(mirror.rescue_ready_left[1], sim.rescue_ready_left[1], 0.001)


func test_each_rescue_area_shows_its_countdown_then_ready() -> void:
	var sim: MatchSim = _sim()
	var hud: OverheadHud = autofree(OverheadHud.new()) as OverheadHud
	hud.setup(sim, autofree(Camera3D.new()) as Camera3D, 1, 0)
	_wait(sim, 2.5)
	assert_eq(hud.rescue_area_text(0), "8")
	sim.rescue_ready_left[1] = 0.0
	assert_eq(hud.rescue_area_text(1), "", "no READY! word: a ready area is just the team's colour")


func test_the_countdown_number_is_opaque_white_with_an_opaque_outline() -> void:
	assert_eq(OverheadHud.AREA_COUNT_OUTLINE.a, 1.0, "the outline was 0.9 see-through")
	assert_eq(Color(OverheadHud.AREA_COUNT_OUTLINE, 1.0), Color(Icons.OUTLINE, 1.0), "the same dark, only solid")
