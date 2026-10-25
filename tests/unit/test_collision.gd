extends GutTest

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")

## The shipped street has no cover any more (owner removed the cardboard walls), so the cover rules are tested on a copy with one wall.
static func _cover_street() -> MapLayout:
	var street: MapLayout = LAYOUT.duplicate() as MapLayout
	street.obstacles = [Rect2(-13.5, 10.5, 3.0, 0.5)] as Array[Rect2]
	return street

const DT: float = 1.0 / 30.0
const RECT: Rect2 = Rect2(0.0, 0.0, 4.0, 2.0)


func _sim() -> MatchSim:
	return MatchSim.new(RULES, _cover_street(), 1)


func _run(sim: MatchSim, id: int, move: Vector2, ticks: int) -> void:
	for i: int in ticks:
		sim.set_input(id, PlayerInput.create(move, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func test_push_out_leaves_a_free_circle_alone() -> void:
	assert_eq(Collision.push_out(Vector2(-3.0, 1.0), 0.5, RECT), Vector2(-3.0, 1.0))
	assert_eq(Collision.push_out(Vector2(-0.5, 1.0), 0.5, RECT), Vector2(-0.5, 1.0), "just touching")


func test_push_out_from_a_face() -> void:
	var result: Vector2 = Collision.push_out(Vector2(-0.2, 1.0), 0.5, RECT)
	assert_almost_eq(result.x, -0.5, 0.0001)
	assert_almost_eq(result.y, 1.0, 0.0001)


func test_push_out_from_a_corner_is_diagonal() -> void:
	var result: Vector2 = Collision.push_out(Vector2(-0.1, -0.1), 0.5, RECT)
	assert_almost_eq(result.distance_to(Vector2(0.0, 0.0)), 0.5, 0.0001)
	assert_lt(result.x, 0.0)
	assert_lt(result.y, 0.0)


func test_push_out_when_center_is_inside() -> void:
	# nearest face is the top (z = 0): 0.3 away vs 1.7 / 3.7 / 3.7
	var result: Vector2 = Collision.push_out(Vector2(2.0, 0.3), 0.5, RECT)
	assert_almost_eq(result.x, 2.0, 0.0001)
	assert_almost_eq(result.y, -0.5, 0.0001)


## The first own-half obstacle (a bench) and a spot just below it.
func _cover() -> Rect2:
	return _cover_street().obstacle_rects()[0]


func test_player_stops_at_an_obstacle() -> void:
	var sim: MatchSim = _sim()
	var p: PlayerState = sim.add_player(1, 0)
	p.position = Vector2(_cover().get_center().x, _cover().end.y + 2.0)
	_run(sim, 1, Vector2(0.0, -1.0), 60)
	assert_almost_eq(p.position.y, _cover().end.y + p.radius, 0.05)


func test_obstacles_block_both_teams_the_same() -> void:
	for team: int in [0, 1]:
		var sim: MatchSim = _sim()
		var p: PlayerState = sim.add_player(1, team)
		p.position = Vector2(_cover().get_center().x, _cover().position.y - 2.0)
		_run(sim, 1, Vector2(0.0, 1.0), 60)
		assert_almost_eq(p.position.y, _cover().position.y - p.radius, 0.05, "team %d held back" % team)


func test_player_slides_along_an_obstacle() -> void:
	var sim: MatchSim = _sim()
	var p: PlayerState = sim.add_player(1, 0)
	var start: Vector2 = Vector2(_cover().get_center().x, _cover().end.y + 0.5)
	p.position = start
	_run(sim, 1, Vector2(1.0, -1.0), 10)
	assert_almost_eq(p.position.y, _cover().end.y + p.radius, 0.05, "held back by the bench")
	assert_gt(p.position.x, start.x + 0.5, "but slid sideways along it")


func test_player_cannot_leave_the_street_sideways() -> void:
	var sim: MatchSim = _sim()
	var p: PlayerState = sim.add_player(1, 0)
	p.position = Vector2(0.0, -8.0)
	_run(sim, 1, Vector2(1.0, 0.0), 900)
	assert_almost_eq(p.position.x, LAYOUT.lane_width / 2.0 - p.radius, 0.05)
	_run(sim, 1, Vector2(-1.0, 0.0), 1200)
	assert_almost_eq(p.position.x, -LAYOUT.lane_width / 2.0 + p.radius, 0.05)


func test_player_cannot_leave_through_the_end() -> void:
	var sim: MatchSim = _sim()
	var p: PlayerState = sim.add_player(1, 0)
	_run(sim, 1, Vector2(0.0, 1.0), 900)
	assert_almost_eq(p.position.y, LAYOUT.lane_length / 2.0 - p.radius, 0.05)
