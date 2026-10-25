extends GutTest
## Tap to move (docs/GDD.md "Controls: tap to move, cards to cast" > Moving: tap a point, the kid walks there).
## Seam: MatchSim (PlayerInput with a move target in, position / move target of PlayerState out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
## The own-half cover wall in data/rules/map_layout.tres: x -13.5..-10.5, z 10.5..11.0.
const COVER: Rect2 = Rect2(-13.5, 10.5, 3.0, 0.5)

## The shipped street has no cover any more (owner removed the cardboard walls), so the cover rules are tested on a copy with one wall.
static func _cover_street() -> MapLayout:
	var street: MapLayout = LAYOUT.duplicate() as MapLayout
	street.obstacles = [Rect2(-13.5, 10.5, 3.0, 0.5)] as Array[Rect2]
	return street


var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.stamina_enabled = false


func _sim(at: Vector2 = Vector2.ZERO) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, _cover_street(), 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = at
	sim.players[2].position = Vector2(12.0, -20.0)
	sim.equip(1)
	sim.equip(2)
	return sim


func _tap_to(sim: MatchSim, point: Vector2) -> void:
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(point))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))


func _steps(sim: MatchSim, ticks: int) -> void:
	for i: int in ticks:
		sim.step(DT)


func test_the_kid_runs_to_the_target_in_a_straight_line_at_move_speed() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(8.0, 6.0))
	_steps(sim, 29)
	var state: PlayerState = sim.players[1]
	assert_almost_eq(state.position.length(), 5.0, 0.001, "5 m in one second")
	assert_almost_eq(state.position.normalized().dot(Vector2(0.8, 0.6)), 1.0, 0.0001, "on the straight line")
	assert_almost_eq(state.facing.dot(Vector2(0.8, 0.6)), 1.0, 0.0001, "facing the way it walks")


func test_the_kid_stops_on_the_target_without_overshooting() -> void:
	var sim: MatchSim = _sim()
	var target: Vector2 = Vector2(1.03, 0.0)
	_tap_to(sim, target)
	var state: PlayerState = sim.players[1]
	for i: int in 30:
		assert_lte(state.position.x, target.x + 0.0001, "never past the point")
		sim.step(DT)
	assert_false(state.has_move_target, "arrived")
	assert_lte(state.position.distance_to(target), _rules.move_arrival_radius)
	var stopped: Vector2 = state.position
	_steps(sim, 10)
	assert_eq(state.position, stopped, "no jitter once there")
	assert_almost_eq(state.facing.dot(Vector2.RIGHT), 1.0, 0.0001, "standing still keeps the last facing")


func test_a_new_tap_replaces_the_target_at_once() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	_steps(sim, 5)
	_tap_to(sim, Vector2(sim.players[1].position.x, -10.0))
	var before: Vector2 = sim.players[1].position
	sim.step(DT)
	var moved: Vector2 = sim.players[1].position - before
	assert_almost_eq(moved.x, 0.0, 0.0001)
	assert_lt(moved.y, 0.0, "walking to the new point")


func test_a_point_past_the_street_edge_is_pulled_in_where_the_kid_fits() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(100.0, -100.0))
	var r: float = _rules.player_radius
	assert_almost_eq(sim.players[1].move_target, Vector2(LAYOUT.lane_width / 2.0 - r, -LAYOUT.lane_length / 2.0 + r), Vector2(0.0001, 0.0001))


func test_a_point_inside_the_cover_wall_is_pushed_out() -> void:
	var sim: MatchSim = _sim()
	for point: Vector2 in [COVER.get_center(), Vector2(-13.4, 10.6), Vector2(-10.6, 10.9)]:
		var clamped: Vector2 = sim.clamp_move_target(point, _rules.player_radius)
		assert_false(COVER.grow(_rules.player_radius - 0.001).has_point(clamped), "%s is out of the cover" % clamped)
		assert_true(LAYOUT.lane_rect().grow(-_rules.player_radius + 0.001).has_point(clamped), "%s is in the street" % clamped)


func test_walking_head_on_into_the_cover_ends_the_move_within_the_stuck_time() -> void:
	var sim: MatchSim = _sim(Vector2(-12.0, 12.5))
	_tap_to(sim, Vector2(-12.0, 8.0))
	var state: PlayerState = sim.players[1]
	var first_blocked: int = -1
	for i: int in 60:
		var before: Vector2 = state.position
		sim.step(DT)
		if first_blocked < 0 and state.position == before:
			first_blocked = i
		if not state.has_move_target:
			assert_gte(first_blocked, 0)
			assert_lte(i - first_blocked + 1, roundi(_rules.move_stuck_time / DT) + 1, "ended within 0.2 s plus a tick")
			return
	fail_test("the kid kept pushing into the wall")


func test_a_shallow_angle_slides_along_the_cover_and_arrives() -> void:
	var sim: MatchSim = _sim(Vector2(-13.0, 12.0))
	var target: Vector2 = Vector2(-6.0, 10.0)
	_tap_to(sim, target)
	_steps(sim, 60)
	assert_false(sim.players[1].has_move_target)
	assert_lte(sim.players[1].position.distance_to(target), _rules.move_arrival_radius)


func test_wasd_moves_directly_and_clears_the_target() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.set_input(1, PlayerInput.create(Vector2.UP, Vector2.ZERO, 0, sim.tick))
	var before: Vector2 = sim.players[1].position
	sim.step(DT)
	assert_false(sim.players[1].has_move_target)
	assert_almost_eq(sim.players[1].position.x, before.x, 0.0001)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	before = sim.players[1].position
	sim.step(DT)
	assert_eq(sim.players[1].position, before, "letting go of the keys stops")


func test_a_stun_ends_the_move() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.apply_effect(1, StatusEffects.Type.STUN, 0.2, 0.0)
	assert_false(sim.players[1].has_move_target)


func test_a_slow_or_polymorph_does_not_end_the_move() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.apply_effect(1, StatusEffects.Type.SLOW, 2.0, 0.4)
	assert_true(sim.players[1].has_move_target)
	var before: Vector2 = sim.players[1].position
	sim.step(DT)
	assert_almost_eq(sim.players[1].position.x - before.x, _rules.move_speed * 0.6 * DT, 0.0001, "walks on, slower")
	sim.apply_effect(1, StatusEffects.Type.POLYMORPH, 2.0, 0.0)
	assert_true(sim.players[1].has_move_target)


func test_a_tag_ends_the_move() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.kill(1)
	assert_false(sim.players[1].has_move_target)


func test_a_heavy_cast_ends_the_move_and_a_light_one_does_not() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	state.elixir = _rules.elixir_max
	state.weapons[0] = &"papel_trap"
	state.weapons[1] = &"bato_heavy"
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_true(state.has_move_target, "a light cast keeps the kid walking")
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_gt(state.cast_lock_left, 0.0, "the heavy rock roots")
	assert_false(state.has_move_target, "and ends the move")


func test_a_tap_while_stunned_is_kept_and_walked_when_it_ends() -> void:
	var sim: MatchSim = _sim()
	sim.apply_effect(1, StatusEffects.Type.STUN, 0.5, 0.0)
	_tap_to(sim, Vector2(10.0, 0.0))
	assert_true(sim.players[1].has_move_target)
	assert_eq(sim.players[1].position, Vector2.ZERO, "cannot move while stunned")
	_steps(sim, 30)
	assert_gt(sim.players[1].position.x, 1.0, "walks once the stun is over")


func test_a_new_round_ends_the_move() -> void:
	var sim: MatchSim = _sim()
	_tap_to(sim, Vector2(10.0, 0.0))
	sim.kill(2)
	var cleared_at_point: Array[bool] = []
	sim.point_scored.connect(func(_team: int, _how: int) -> void: cleared_at_point.append(not sim.players[1].has_move_target))
	for i: int in 30 * 12:
		# keeps walking back and forth, so it never arrives on its own
		var x: float = 13.0 if i % 2 == 0 else -13.0
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(x, 3.0)))
		sim.step(DT)
		if sim.score.points[0] > 0 and sim.phase == MatchSim.Phase.PLAYING:
			break
	assert_eq(cleared_at_point, [true] as Array[bool], "the point freeze ends the move")
	assert_false(sim.players[1].has_move_target, "and the new round starts standing")


func test_a_finger_held_on_a_point_past_the_wall_still_ends_the_push() -> void:
	var sim: MatchSim = _sim(Vector2(-12.0, 12.5))
	var state: PlayerState = sim.players[1]
	for i: int in 60:
		# the held follow sends the same point every tick
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(-12.0, 8.0)))
		sim.step(DT)
		if not state.has_move_target:
			pass_test("stuck for 0.2 s ended it although the point kept coming")
			return
	fail_test("the same point re-sent every tick kept the kid pushing into the wall")
