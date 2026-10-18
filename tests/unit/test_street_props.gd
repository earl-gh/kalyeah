extends GutTest
## The owner's street models: the nanay outside the play area and the five new barrier things
## (tricycle, e-bike, sacks, sewing machine, NO ID NO ENTRY sign) across both ends.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const NEW_BARRIERS: Array[String] = ["tricycle", "ebike", "sacks", "sewing_machine", "no_id_no_entry", "plank_fence", "roof_wall", "roof_wall_2"]


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	return map


func test_every_new_model_exists_light_and_whole() -> void:
	for id: String in NEW_BARRIERS + ["nanay"]:
		var path: String = PropModel.PATH % id
		assert_true(ResourceLoader.exists(path), "%s is there" % id)
		var counts: Vector2i = MeshAudit.open_edges_and_triangles(path)
		assert_between(counts.y, 2000, 6500, "%s is light for phones" % id)
		assert_lt(float(counts.x) / float(counts.y), 0.35, "%s is a whole surface" % id)


func test_the_new_things_are_in_the_barrier_rows_at_both_ends() -> void:
	var map: StreetMap = _map()
	assert_eq(map.barrier_rows.size(), 2, "a row at each end")
	var seen: Dictionary = {}
	for row: Array in map.barrier_rows:
		assert_gt(row.size(), 5, "a full row")
		for id: Variant in row:
			seen[id] = true
	for id: String in NEW_BARRIERS:
		assert_true(StreetMap.BARRIER_HEIGHTS.has(id), "%s has a height" % id)
		assert_true(seen.has(id), "%s stands in a row" % id)


func test_the_rows_still_fit_the_street_at_true_size() -> void:
	var map: StreetMap = _map()
	for row: Array in map.barrier_rows:
		var used: float = 0.0
		for id: Variant in row:
			used += StreetMap.barrier_width(id as String) + StreetMap.BARRIER_GAP
		assert_lt(used - StreetMap.BARRIER_GAP, map.layout.lane_width + 0.001, "the row fits the street")


func test_the_nanay_stands_on_the_sidewalk_outside_the_play_area_facing_the_street() -> void:
	var map: StreetMap = _map()
	var nanay: Node3D = map.find_child("Nanay", true, false) as Node3D
	assert_not_null(nanay)
	assert_gt(absf(nanay.position.x), map.layout.lane_width / 2.0 + map.layout.boundary_thickness, "outside the play area")
	assert_almost_eq(nanay.position.y, StreetMap.CURB_HEIGHT, 0.001, "on the sidewalk")
	assert_lt(nanay.position.x, 0.0)
	assert_almost_eq(nanay.rotation.y, PI / 2.0, 0.1, "turned toward the street")
	var mesh: MeshInstance3D = nanay.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	assert_almost_eq(mesh.get_aabb().size.y * nanay.scale.y, StreetMap.NANAY_HEIGHT, 0.01)


func test_the_nanay_sways_a_little() -> void:
	var map: StreetMap = _map()
	var nanay: Node3D = map.find_child("Nanay", true, false) as Node3D
	var start: float = nanay.rotation.y
	await wait_seconds(1.0)
	assert_ne(nanay.rotation.y, start)
	assert_lt(absf(nanay.rotation.y - PI / 2.0), deg_to_rad(StreetMap.NANAY_SWAY_DEGREES) + 0.01)


func test_the_main_screen_model_has_no_nanay_or_barriers() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	map.layout = load("res://data/rules/map_layout.tres") as MapLayout
	map.diorama = true
	map.show_overview_camera = false
	add_child(map)
	assert_null(map.find_child("Nanay", true, false))
	assert_eq(map.barrier_rows.size(), 0)


func test_the_jeep_pedicab_and_ice_cream_cart_are_light_and_whole() -> void:
	for id: String in ["jeep", "pedicab", "ice_cream_cart"]:
		var counts: Vector2i = MeshAudit.open_edges_and_triangles(PropModel.PATH % id)
		assert_between(counts.y, 3000, 5500, "%s is light for phones" % id)
		assert_lt(float(counts.x) / float(counts.y), 0.35, "%s is a whole surface" % id)


func test_the_vehicles_are_parked_outside_the_play_area() -> void:
	var map: StreetMap = _map()
	var half_w: float = map.layout.lane_width / 2.0
	var half_l: float = map.layout.lane_length / 2.0
	var jeep: Node3D = map.find_child("Jeep", true, false) as Node3D
	var pedicab: Node3D = map.find_child("Pedicab", true, false) as Node3D
	var cart: Node3D = map.find_child("IceCreamCart", true, false) as Node3D
	assert_not_null(jeep)
	assert_not_null(pedicab)
	assert_not_null(cart)
	assert_lt(jeep.position.z, -half_l - 1.0, "the jeep is behind the top barrier row")
	assert_gt(pedicab.position.z, half_l + 1.0, "the pedicab is behind the bottom barrier row")
	assert_gt(cart.position.x, half_w + map.layout.boundary_thickness, "the cart is on the right sidewalk")
	assert_almost_eq(cart.position.y, StreetMap.CURB_HEIGHT, 0.001)
	var mesh: MeshInstance3D = jeep.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	assert_almost_eq(mesh.get_aabb().size.y * jeep.scale.y, StreetMap.JEEP_HEIGHT, 0.01)


func test_no_vehicle_stands_where_the_kids_play() -> void:
	var map: StreetMap = _map()
	for id: String in ["Jeep", "Pedicab", "IceCreamCart"]:
		var node: Node3D = map.find_child(id, true, false) as Node3D
		var spot: Vector2 = Vector2(node.position.x, node.position.z)
		assert_false(map.layout.lane_rect().grow(0.5).has_point(spot), "%s is outside the play area" % id)


func test_the_tricycle_terminal_stands_in_the_left_row_at_the_top_end() -> void:
	var map: StreetMap = _map()
	var found: Node3D = null
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node != null and node.scene_file_path.get_file().get_basename() == StreetMap.TERMINAL:
			found = node
	assert_not_null(found, "the terminal is in the street")
	var front: float = map.layout.lane_width / 2.0 + map.layout.boundary_thickness + StreetMap.SIDEWALK_WIDTH
	assert_lt(found.position.x, -front, "on the left, in line with the buildings")
	assert_lt(found.position.z, 0.0, "at the top end")
	assert_lt(absf(found.position.z + map.layout.lane_length / 2.0), StreetMap.HOUSE_SPACING, "near the top end line")


func test_the_pets_and_things_lie_on_the_sidewalks() -> void:
	var map: StreetMap = _map()
	var half_w: float = map.layout.lane_width / 2.0 + map.layout.boundary_thickness
	var seen: Dictionary = {}
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node == null:
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		if ["cat", "dog", "chicken_cage", "drainage_cover"].has(id):
			seen[id] = true
			assert_gt(absf(node.position.x), half_w, "%s is off the play area" % id)
			assert_lt(absf(node.position.x), half_w + StreetMap.SIDEWALK_WIDTH + 0.5, "%s is on the sidewalk" % id)
			assert_almost_eq(node.position.y, StreetMap.CURB_HEIGHT, 0.001)
	assert_eq(seen.size(), 4, "all four are there")


func test_what_does_not_fit_in_a_row_stands_on_the_road_beyond() -> void:
	var map: StreetMap = _map()
	var rowed: Dictionary = {}
	for row: Array in map.barrier_rows:
		for id: Variant in row:
			rowed[id] = true
	assert_gt(map.far_props.size(), 0, "something did not fit")
	for id: String in map.far_props:
		assert_true(StreetMap.BARRIER_HEIGHTS.has(id))
	for id: String in StreetMap.BARRIER_HEIGHTS:
		assert_true(rowed.has(id) or map.far_props.has(id), "%s is in a row or on the far road" % id)


func test_only_houses_stand_along_the_street_beyond_the_ends() -> void:
	var map: StreetMap = _map()
	var half_l: float = map.layout.lane_length / 2.0
	var beyond: int = 0
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node == null or absf(node.position.z) <= half_l + StreetMap.HOUSE_SPACING / 2.0:
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		if StreetMap.BUILDINGS.has(id) or id == "tricycle_terminal":
			assert_true(false, "%s is a shop and stands beyond the end" % id)
		if StreetMap.FILLER_HOUSES.has(id):
			beyond += 1
	assert_gt(beyond, 4, "a run of houses beyond the ends")
