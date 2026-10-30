extends GutTest
## The spinning top's shape (owner: "its shape must be exactly like this", the photo of a bare, unpainted wooden top on a thin steel nail):
## `trumpo.glb` is the owner's own model with its vertices moved onto that photo's outline (same triangles, UVs, material and node). The
## numbers below were measured on the photo (478 x 720 px: body 22 to 455 px from the top, widest at 190 px, 444 px wide, steel pin to 705 px)
## and are read here off the mesh, as fractions, so a model dropped in later is held to the same outline.

const PATH: String = "res://assets/models/weapons/trumpo.glb"
## The model as the owner made it: the same counts must stay (the shape changed, the topology did not).
const VERTICES: int = 1640
const TRIANGLES: int = 2199
## Photo: the wooden body ends 0.366 of the whole height above the pin's point (250 of 683 px are pin).
const PIN_LENGTH: float = 0.366
## Photo: whole width over whole height (445 / 683), the widest point 39% of the body's height below its top, body width over body height 1.02.
const TOTAL_ASPECT: float = 0.65
const WIDEST_FROM_TOP: float = 0.39
const BODY_ASPECT: float = 1.02


func _arrays() -> Array:
	return WeaponModel.mesh_of("trumpo").surface_get_arrays(0)


func _vertices() -> PackedVector3Array:
	return _arrays()[Mesh.ARRAY_VERTEX] as PackedVector3Array


func _height() -> float:
	return (WeaponModel.mesh_of("trumpo") as Mesh).get_aabb().size.y


func _radius(vertex: Vector3) -> float:
	return Vector2(vertex.x, vertex.z).length()


## The widest radius of the vertices between heights `low` and `high` (fractions of the whole height); 0 where there are none.
func _widest_between(low: float, high: float) -> float:
	var height: float = _height()
	var widest: float = 0.0
	for vertex: Vector3 in _vertices():
		var at: float = vertex.y / height
		if at >= low and at <= high:
			widest = maxf(widest, _radius(vertex))
	return widest


func _max_radius() -> float:
	return _widest_between(0.0, 1.0)


## Radius of the outline at `depth` of the body's height below its top (the outline is the widest vertex in a thin band round that height).
func _outline_at_depth(depth: float) -> float:
	var body: float = 1.0 - PIN_LENGTH
	var at: float = 1.0 - depth * body
	return _widest_between(at - 0.006, at + 0.006)


func test_the_topology_is_the_owners_only_the_positions_moved() -> void:
	var arrays: Array = _arrays()
	assert_eq((arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size(), VERTICES, "same vertices")
	assert_eq((arrays[Mesh.ARRAY_INDEX] as PackedInt32Array).size(), TRIANGLES * 3, "same triangles")
	assert_eq((arrays[Mesh.ARRAY_TEX_UV] as PackedVector2Array).size(), VERTICES, "same UVs")
	for normal: Vector3 in arrays[Mesh.ARRAY_NORMAL] as PackedVector3Array:
		assert_almost_eq(normal.length(), 1.0, 0.01, "every normal is a unit vector")


func test_it_stands_on_its_pin_centred_on_its_axis_and_taller_than_wide() -> void:
	var box: AABB = (WeaponModel.mesh_of("trumpo") as Mesh).get_aabb()
	assert_almost_eq(box.position.y, 0.0, 0.01, "the pin's point is on the ground")
	assert_gt(box.size.y, box.size.x, "the model is tallest up and down: OBJECTS' longest side is its height")
	assert_gt(box.size.y, box.size.z)
	assert_almost_eq(box.position.x + box.size.x / 2.0, 0.0, 0.01, "centred on x")
	assert_almost_eq(box.position.z + box.size.z / 2.0, 0.0, 0.01, "centred on z")
	var node: Node3D = autofree(WeaponModel.make(&"trumpo", false))
	var drawn: Vector3 = box.size * (node.get_child(0) as Node3D).scale
	assert_almost_eq(drawn.y, WeaponModel.OBJECTS[&"trumpo"][1] as float, 0.01, "still 1.12 m long")


func test_the_whole_top_is_about_two_thirds_as_wide_as_it_is_tall() -> void:
	var aspect: float = _max_radius() * 2.0 / _height()
	assert_almost_eq(aspect, TOTAL_ASPECT, 0.025, "width over height with the pin: %.3f" % aspect)


func test_the_body_is_as_wide_as_it_is_tall_and_widest_39_percent_down() -> void:
	var body: float = _height() * (1.0 - PIN_LENGTH)
	assert_almost_eq(_max_radius() * 2.0 / body, BODY_ASPECT, 0.07, "the wooden body is as wide as tall, not a flat dome nor a tall cone")
	var widest_at: float = 0.0
	var widest: float = 0.0
	for step: int in range(int(PIN_LENGTH * 100.0), 100):
		var radius: float = _widest_between(float(step) / 100.0, float(step + 1) / 100.0)
		if radius > widest:
			widest = radius
			widest_at = float(step) / 100.0 + 0.005
	var from_top: float = (1.0 - widest_at) / (1.0 - PIN_LENGTH)
	assert_almost_eq(from_top, WIDEST_FROM_TOP, 0.05, "the widest point is %.2f of the body's height below its top" % from_top)


func test_the_top_is_a_rounded_shoulder_with_a_flat_top_over_half_the_width() -> void:
	var widest: float = _max_radius()
	assert_almost_eq(_outline_at_depth(0.074) / widest, 0.57, 0.07, "7% of the body down the outline is 55 to 60% of the widest")
	assert_lt(_outline_at_depth(0.02) / widest, 0.4, "the very top is a small flat, a shallow lip")
	# the shoulder is convex: the outline never narrows going down until the widest point
	var previous: float = 0.0
	for step: int in range(0, 38):
		var radius: float = _outline_at_depth(float(step) / 100.0 + 0.02)
		assert_gte(radius, previous - 0.004, "the shoulder keeps widening to the widest point (depth %d%%)" % (step + 2))
		previous = maxf(previous, radius)


func test_below_the_widest_point_the_sides_run_in_a_nearly_straight_cone_to_a_blunt_tip() -> void:
	var widest: float = _max_radius()
	var tip: float = _outline_at_depth(1.0)
	var widest_depth: float = WIDEST_FROM_TOP
	for depth: float in [0.55, 0.65, 0.75, 0.85, 0.92]:
		var line: float = lerpf(widest, tip, (depth - widest_depth) / (1.0 - widest_depth))
		var radius: float = _outline_at_depth(depth)
		assert_lte(radius, line + widest * 0.18, "depth %.2f: %.3f is gently convex over the straight cone %.3f, not bulging" % [depth, radius, line])
		assert_gte(radius, line - widest * 0.03, "and never bellied in below it")
	assert_almost_eq(tip * 2.0 / (widest * 2.0), 0.09, 0.05, "the wooden tip is about 9%% of the widest width (%.3f)" % (tip / widest))
	assert_gt(tip, 0.015, "blunt, not a needle")


func test_a_thin_straight_steel_pin_of_a_third_of_the_height_ends_in_a_point() -> void:
	var widest: float = _max_radius()
	var vertices: PackedVector3Array = _vertices()
	var height: float = _height()
	var top_of_pin: float = height
	var ring: float = 0.0
	var rings: int = 0
	for vertex: Vector3 in vertices:
		var at: float = vertex.y / height
		if at > 0.045 and at < PIN_LENGTH - 0.004:
			assert_true(false, "no vertex between the pin's two rings: it is a straight shaft (found one at %.3f)" % at)
			break
		if at >= 0.028 and at <= 0.04:
			ring = maxf(ring, _radius(vertex))
			rings += 1
		if at >= 0.045 and _radius(vertex) > widest * 0.045:
			top_of_pin = minf(top_of_pin, vertex.y)
	assert_gt(rings, 4, "a ring of vertices at the foot of the shaft")
	assert_almost_eq(ring * 2.0 / (widest * 2.0), 0.07, 0.025, "the pin is about 6 to 7%% of the widest width (%.3f)" % (ring / widest))
	assert_almost_eq(top_of_pin / height, PIN_LENGTH, 0.015, "the pin is 37%% of the whole height (%.3f)" % (top_of_pin / height))
	var point: float = 1.0
	for vertex: Vector3 in vertices:
		if vertex.y / height < 0.004:
			point = minf(point, _radius(vertex))
	assert_lt(point, 0.006, "the tip ends in a point")
	assert_lt(_widest_between(0.0, 0.012), ring + 0.001, "chamfered: nothing at the point is wider than the shaft")
