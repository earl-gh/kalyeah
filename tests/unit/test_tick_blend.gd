extends GutTest
## Movement blending (view only): the sim steps at 30 Hz, the screen draws every frame, so kids and flying things glide between
## their last two ticks instead of stepping. Seam: TickBlend (record after each tick, set_progress each frame, kid / shot out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(-10.0, 0.0)
	sim.players[2].position = Vector2(10.0, -20.0)
	return sim


func test_a_kid_nobody_recorded_is_drawn_where_the_sim_has_it() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	assert_eq(blend.kid(sim.players[1]), sim.players[1].position)


func test_between_two_ticks_the_kid_is_drawn_between_them() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	blend.record(sim)
	sim.players[1].position += Vector2(0.2, 0.0)
	blend.record(sim)
	var now: Vector2 = sim.players[1].position
	blend.set_progress(0.0, DT)
	assert_almost_eq(blend.kid(sim.players[1]).x, now.x - 0.2, 0.0001, "just after a tick: the older one")
	blend.set_progress(DT * 0.5, DT)
	assert_almost_eq(blend.kid(sim.players[1]).x, now.x - 0.1, 0.0001, "halfway")
	blend.set_progress(DT, DT)
	assert_almost_eq(blend.kid(sim.players[1]).x, now.x, 0.0001, "the next tick is due: the newer one")
	blend.set_progress(DT * 3.0, DT)
	assert_eq(blend.weight, 1.0, "never past the newer tick")


func test_a_jump_is_a_change_of_place_not_a_run() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	blend.record(sim)
	sim.players[1].position = Vector2(-10.0, 14.0)  # tagged, rescued or a new round
	blend.record(sim)
	blend.set_progress(0.0, DT)
	assert_eq(blend.kid(sim.players[1]), sim.players[1].position, "drawn where it is, not slid across the street")


func test_a_kid_moved_outside_a_tick_is_drawn_as_it_is() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	blend.record(sim)
	sim.players[1].position += Vector2(0.1, 0.0)
	blend.record(sim)
	sim.players[1].position += Vector2(0.05, 0.0)  # an online snapshot placing the kid every frame
	blend.set_progress(0.0, DT)
	assert_eq(blend.kid(sim.players[1]), sim.players[1].position)


func test_at_60_fps_the_kid_moves_on_every_frame_not_every_second_one() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	var accumulator: float = 0.0
	var drawn: Array[float] = []
	var plain: Array[float] = []
	for frame: int in 40:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(10.0, 0.0)))
		accumulator += 1.0 / 60.0
		while accumulator >= DT - 0.00001:
			sim.step(DT)
			blend.record(sim)
			accumulator -= DT
		blend.set_progress(maxf(accumulator, 0.0), DT)
		drawn.append(blend.kid(sim.players[1]).x)
		plain.append(sim.players[1].position.x)
	var still_plain: int = 0
	for i: int in range(6, 40):
		assert_gt(drawn[i], drawn[i - 1], "frame %d is further along than the last" % i)
		if plain[i] == plain[i - 1]:
			still_plain += 1
	assert_gt(still_plain, 10, "without the blend every second frame stands still")
	var step: float = _rules.move_speed / 60.0
	for i: int in range(8, 40):
		assert_almost_eq(drawn[i] - drawn[i - 1], step, step * 0.3, "an even stride of a 60th of a second")


func test_a_flying_thing_is_blended_by_its_id_and_forgotten_when_it_lands() -> void:
	var blend: TickBlend = TickBlend.new()
	var sim: MatchSim = _sim()
	var shot: WeaponSystem.Projectile = WeaponSystem.Projectile.new()
	shot.id = 7
	shot.position = Vector2(0.0, 0.0)
	sim.weapons.projectiles.append(shot)
	blend.record(sim)
	shot.position = Vector2(0.5, 0.0)
	blend.record(sim)
	blend.set_progress(DT * 0.5, DT)
	assert_almost_eq(blend.shot(shot).x, 0.25, 0.0001)
	sim.weapons.projectiles.clear()
	blend.record(sim)
	assert_eq(blend.shot(shot), shot.position, "gone from the sim: nothing is kept")
