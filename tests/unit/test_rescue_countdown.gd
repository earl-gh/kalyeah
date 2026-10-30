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


const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")


func _map(own_team_side: int = MapLayout.SIDE_OWN) -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	map.set_own_side(own_team_side)
	return map


func test_each_rescue_area_shows_its_countdown_then_ready() -> void:
	var sim: MatchSim = _sim()
	var map: StreetMap = _map()
	_wait(sim, 2.5)
	map.sync_rescue(sim)
	assert_eq(map.rescue_count_text(sim.side_for_team(0)), "8")
	assert_eq(map.rescue_count_text(sim.side_for_team(1)), "8", "both teams' boxes")
	sim.rescue_ready_left[1] = 0.0
	map.sync_rescue(sim)
	assert_eq(map.rescue_count_text(sim.side_for_team(1)), "", "no READY! word: a ready area is just the team's colour")
	assert_eq(map.rescue_count_text(sim.side_for_team(0)), "8", "the other box keeps counting")
	sim.rescue_ready_left[0] = 0.3
	map.sync_rescue(sim)
	assert_eq(map.rescue_count_text(sim.side_for_team(0)), "1", "the last fraction of a second still reads 1")


func test_the_countdown_is_a_flat_mark_on_the_ground_not_a_screen_overlay() -> void:
	assert_false((autofree(OverheadHud.new()) as OverheadHud).has_method("rescue_area_text"), "the overhead HUD no longer draws the number")
	var sim: MatchSim = _sim()
	var map: StreetMap = _map()
	map.sync_rescue(sim)
	for team: int in [0, 1]:
		var side: int = sim.side_for_team(team)
		var label: Label3D = map.rescue_count(side)
		var middle: Vector2 = LAYOUT.rescue_rect(side).get_center()
		assert_true(label.is_inside_tree() and label.visible)
		assert_almost_eq(label.global_position.x, middle.x, 0.001, "at the area's middle")
		assert_almost_eq(label.global_position.z, middle.y, 0.001)
		assert_between(label.global_position.y, StreetMap.RING_HEIGHT, 0.1, "lying just above the tint and the outline")
		assert_true(label.global_transform.basis.orthonormalized().z.is_equal_approx(Vector3.UP), "flat on the street, facing up")
		assert_true(label.global_transform.basis.orthonormalized().y.is_equal_approx(Vector3.FORWARD), "its top toward the top of the screen (-Z, the camera's view)")
		assert_false(label.shaded, "unshaded like the other zone marks")
		assert_false(label.no_depth_test, "depth tested: a kid standing on it covers it")
		assert_eq(label.alpha_cut, Label3D.ALPHA_CUT_DISABLED, "blended, not cut")


func test_the_countdown_draws_over_the_areas_tint_and_under_every_other_see_through_mark() -> void:
	# transparent things draw in render_priority order: the tint (-100) and the outline, then the number, then the kids' rings and shadows
	# (0 and up: FxKit.PRIO_*), so those draw over it; the kids themselves are solid and cover it by depth
	var map: StreetMap = _map()
	var label: Label3D = map.rescue_count(MapLayout.SIDE_OWN)
	var materials: Array = map._base_materials[MapLayout.SIDE_OWN]
	var tint: int = (materials[3] as StandardMaterial3D).render_priority
	var line: int = (materials[2] as StandardMaterial3D).render_priority
	assert_gt(label.render_priority, tint, "over the tint")
	assert_gt(label.render_priority, line, "and its outline")
	assert_lt(label.render_priority, FxKit.PRIO_FILL, "under the skill areas")
	assert_lt(label.render_priority, FxKit.PRIO_SHADOW, "and the kids' shadows")
	assert_lt(label.render_priority, 0, "and the kids' rings (the default priority)")


func test_the_countdown_is_white_at_half_alpha_and_the_box_turns_the_teams_colour_when_ready() -> void:
	var sim: MatchSim = _sim()
	var map: StreetMap = _map()
	map.sync_rescue(sim)
	var own: int = sim.side_for_team(0)
	var label: Label3D = map.rescue_count(own)
	assert_eq(label.modulate, Color(1.0, 1.0, 1.0, 0.5), "white, half see-through (owner, 2026-10-30)")
	assert_eq(label.outline_size, 0, "no outline: it would blend twice over the half see-through white")
	assert_true(Color(map.zone_colors(own)[2], 1.0).is_equal_approx(Color.WHITE), "the box is white while it counts")
	sim.rescue_ready_left[0] = 0.0
	map.sync_rescue(sim)
	assert_false(label.visible, "nothing to count once ready")
	assert_true(Color(map.zone_colors(own)[2], 1.0).is_equal_approx(StreetMap.OWN_COLOR), "the exact blue of yours")
	assert_true(Color(map.zone_colors(sim.side_for_team(1))[2], 1.0).is_equal_approx(StreetMap.ENEMY_COLOR) or sim.rescue_ready_left[1] > 0.0)


func test_the_flipped_view_turns_the_number_so_it_is_still_upright() -> void:
	var sim: MatchSim = _sim()
	var map: StreetMap = _map(MapLayout.SIDE_ENEMY)
	map.sync_rescue(sim)
	var label: Label3D = map.rescue_count(MapLayout.SIDE_OWN)
	assert_true(label.global_transform.basis.orthonormalized().z.is_equal_approx(Vector3.UP), "still lying flat")
	assert_true(label.global_transform.basis.orthonormalized().y.is_equal_approx(Vector3.BACK), "its top toward the top of the screen (+Z when the camera is turned round)")


func test_the_number_is_hidden_while_a_point_is_shown() -> void:
	var sim: MatchSim = _sim()
	var map: StreetMap = _map()
	map.sync_rescue(sim)
	assert_true(map.rescue_count(MapLayout.SIDE_OWN).visible)
	sim.phase = MatchSim.Phase.POINT_FREEZE
	map.sync_rescue(sim)
	assert_false(map.rescue_count(MapLayout.SIDE_OWN).visible)
