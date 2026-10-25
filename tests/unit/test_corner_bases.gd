extends GutTest
## Rounded street corners and posts in the corner (owner): the street's corners have the base's radius, kids are held on the curve,
## and each post stands at the centre of its corner's curve.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const DT: float = 1.0 / 30.0
const SIDES: Array[int] = [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]


## The street's outer corner at a side's end, and the direction from the centre of its curve out to it.
func _corner(side: int) -> Vector2:
	return Vector2(-side * LAYOUT.lane_width / 2.0, side * LAYOUT.lane_length / 2.0)


func _outward(side: int) -> Vector2:
	return (_corner(side) - LAYOUT.post_position(side)).normalized()


# ---- the street's corners and the base's -------------------------------------------------------------------------

func test_the_streets_corners_are_rounded_with_the_bases_radius() -> void:
	var street: Rect2 = LAYOUT.lane_rect()
	for side: int in SIDES:
		var centre: Vector2 = LAYOUT.post_position(side)
		var inside: Vector2 = centre + _outward(side) * LAYOUT.base_corner * 0.99
		var outside: Vector2 = centre + _outward(side) * LAYOUT.base_corner * 1.02
		assert_true(MapLayout.rounded_has_point(street, LAYOUT.base_corner, inside), "side %d: the curve's inside is street" % side)
		assert_false(MapLayout.rounded_has_point(street, LAYOUT.base_corner, outside), "side %d: past the curve is not" % side)
		assert_true(LAYOUT.in_base(side, inside), "side %d: the base fills the same curve" % side)
		assert_false(LAYOUT.in_base(side, outside), "side %d: and stops at it" % side)


func test_a_base_fits_its_corner_flush() -> void:
	var street: Rect2 = LAYOUT.lane_rect()
	var own: Rect2 = LAYOUT.base_rect(MapLayout.SIDE_OWN)
	assert_eq(own.position.x, street.position.x, "your base touches the street's left edge")
	assert_eq(own.end.y, street.end.y, "and your end")
	var theirs: Rect2 = LAYOUT.base_rect(MapLayout.SIDE_ENEMY)
	assert_eq(theirs.end.x, street.end.x, "theirs the right edge")
	assert_eq(theirs.position.y, street.position.y, "and the far end")


func test_the_post_stands_at_the_centre_of_its_corners_curve() -> void:
	for side: int in SIDES:
		var post: Vector2 = LAYOUT.post_position(side)
		assert_almost_eq(absf(post.x), LAYOUT.lane_width / 2.0 - LAYOUT.base_corner, 0.0001, "side %d: one curve radius in from the side edge" % side)
		assert_almost_eq(absf(post.y), LAYOUT.lane_length / 2.0 - LAYOUT.base_corner, 0.0001, "and from the end")
		assert_true(LAYOUT.lane_rect().has_point(post))
		assert_true(LAYOUT.in_base(side, post), "the post is inside its own base")
	assert_eq(LAYOUT.post_position(MapLayout.SIDE_OWN), -LAYOUT.post_position(MapLayout.SIDE_ENEMY), "the street is point-symmetric")
	assert_lt(LAYOUT.post_position(MapLayout.SIDE_OWN).x, 0.0, "your post is bottom left")
	assert_gt(LAYOUT.post_position(MapLayout.SIDE_OWN).y, 0.0)


func test_a_point_pushed_into_a_corner_is_held_on_the_curve() -> void:
	for radius: float in [0.0, RULES.player_radius]:
		for side: int in SIDES:
			var held: Vector2 = LAYOUT.keep_in_street(_corner(side) * 4.0, radius)
			var expected: Vector2 = LAYOUT.post_position(side) + _outward(side) * (LAYOUT.base_corner - radius)
			assert_almost_eq(held, expected, Vector2(0.0001, 0.0001), "side %d, radius %s: on the curve, a kid's radius inside it" % [side, radius])


func test_points_inside_or_along_a_straight_edge_are_left_alone() -> void:
	var r: float = RULES.player_radius
	var edge: float = LAYOUT.lane_width / 2.0 - r
	for point: Vector2 in [Vector2.ZERO, Vector2(-edge, 0.0), Vector2(edge, 10.0), Vector2(3.0, LAYOUT.lane_length / 2.0 - r)]:
		assert_eq(LAYOUT.keep_in_street(point, r), point, "%s stays where it is" % point)
	assert_eq(LAYOUT.keep_in_street(Vector2(100.0, 0.0), r), Vector2(edge, 0.0), "past a straight edge it comes in square")


# ---- the sim keeps kids in the rounded street --------------------------------------------------------------------

func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[2].position = Vector2(12.0, -20.0)
	return sim


func _walk(sim: MatchSim, id: int, move: Vector2, ticks: int) -> void:
	for i: int in ticks:
		sim.set_input(id, PlayerInput.create(move, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)


func _fits(position: Vector2, radius: float) -> bool:
	return MapLayout.rounded_has_point(LAYOUT.lane_rect().grow(0.001 - radius), maxf(LAYOUT.base_corner - radius, 0.0) + 0.001, position)


func test_walking_along_the_end_wall_into_a_corner_slides_round_the_curve_and_does_not_stick() -> void:
	var sim: MatchSim = _sim()
	var kid: PlayerState = sim.players[1]
	kid.position = Vector2(-8.0, LAYOUT.lane_length / 2.0 - kid.radius)
	_walk(sim, 1, Vector2(-1.0, 0.0), 300)
	assert_almost_eq(kid.position.x, -LAYOUT.lane_width / 2.0 + kid.radius, 0.05, "it reached the side wall instead of stopping in the curve")
	assert_almost_eq(kid.position.y, LAYOUT.lane_length / 2.0 - LAYOUT.base_corner, 0.05, "having slid up the curve to where it meets the wall")
	assert_true(_fits(kid.position, kid.radius))


func test_a_kid_cannot_leave_the_street_through_a_corner() -> void:
	for side: int in SIDES:
		var sim: MatchSim = _sim()
		var kid: PlayerState = sim.players[1]
		kid.position = LAYOUT.post_position(side)
		for direction: Vector2 in [Vector2(-1.0, 1.0), Vector2(1.0, 1.0), Vector2(1.0, -1.0), Vector2(-1.0, -1.0)]:
			_walk(sim, 1, direction.normalized(), 120)
			assert_true(_fits(kid.position, kid.radius), "side %d, heading %s: still in the rounded street at %s" % [side, direction, kid.position])


func test_a_tap_in_the_corner_walks_to_the_curve_and_stops_there() -> void:
	var sim: MatchSim = _sim()
	var kid: PlayerState = sim.players[1]
	kid.position = Vector2(-9.0, 20.0)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(Vector2(-14.0, 25.0)))
	sim.step(DT)
	var target: Vector2 = kid.move_target
	assert_true(_fits(target, kid.radius), "the target is pulled onto the curve")
	assert_almost_eq(target, LAYOUT.post_position(MapLayout.SIDE_OWN) + _outward(MapLayout.SIDE_OWN) * (LAYOUT.base_corner - kid.radius), Vector2(0.001, 0.001))
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	for i: int in 120:
		sim.step(DT)
	assert_false(kid.has_move_target, "it arrived")
	assert_lt(kid.position.distance_to(target), RULES.move_arrival_radius + 0.001)


func test_the_base_can_still_be_held_from_the_corner() -> void:
	var sim: MatchSim = _sim()
	var kid: PlayerState = sim.players[1]
	var base: int = MapLayout.SIDE_ENEMY
	kid.position = LAYOUT.keep_in_street(LAYOUT.post_position(base) + _outward(base) * 5.0, kid.radius)
	assert_true(LAYOUT.in_base(base, kid.position), "standing in the very corner is standing in the base")


# ---- the post in the street ---------------------------------------------------------------------------------------

func test_the_posts_stand_in_the_corners_and_the_blue_one_keeps_its_back_to_the_camera() -> void:
	var map: StreetMap = autofree(preload("res://scenes/map/street_map.tscn").instantiate()) as StreetMap
	add_child(map)
	for side: int in SIDES:
		var post: Node3D = map._posts[side]
		assert_eq(Vector2(post.position.x, post.position.z), LAYOUT.post_position(side), "side %d: at the corner's curve" % side)
		assert_almost_eq((post.get_node(StreetMap.POST_BLUE) as Node3D).rotation.y, StreetMap.BLUE_POST_TURN, 0.001, "the blue pole is still turned its back to the camera")


func test_the_play_floor_is_cut_to_the_rounded_corners() -> void:
	var map: StreetMap = autofree(preload("res://scenes/map/street_map.tscn").instantiate()) as StreetMap
	add_child(map)
	var floor_patch: MeshInstance3D = map.get_node("%Geometry").find_child("CementArt", true, false) as MeshInstance3D
	var corners_cut: int = 0
	var vertices: PackedVector3Array = floor_patch.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array
	for vertex: Vector3 in vertices:
		assert_true(_fits(Vector2(vertex.x, vertex.z), 0.0), "%s is on the rounded floor" % vertex)
		if absf(vertex.x) > LAYOUT.lane_width / 2.0 - LAYOUT.base_corner and absf(vertex.z) > LAYOUT.lane_length / 2.0 - LAYOUT.base_corner:
			corners_cut += 1
	assert_gt(corners_cut, 0, "the corner curves are in the mesh")
	var sharp: Vector2 = _corner(MapLayout.SIDE_OWN)
	for vertex: Vector3 in vertices:
		assert_gt(Vector2(vertex.x, vertex.z).distance_to(sharp), 0.1, "nothing reaches the old square corner")
