extends GutTest
## The owner's street models: the nanay outside the play area and the five new barrier things
## (tricycle, e-bike, sacks, NO ID NO ENTRY sign) across both ends.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const NEW_BARRIERS: Array[String] = ["tricycle", "ebike", "sacks", "no_id_no_entry", "plank_fence"]


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
		assert_gte(row.size(), 4, "a full row (the bigger the things are drawn, the fewer fit across the street; the rest stand on the road beyond)")
		for id: Variant in row:
			seen[id] = true
	for id: String in NEW_BARRIERS:
		assert_true(StreetMap.BARRIER_HEIGHTS.has(id), "%s has a height" % id)
		assert_true(seen.has(id) or map.far_props.has(id), "%s stands in a row or on the road beyond the end" % id)


func test_the_rows_still_fit_the_street_at_true_size() -> void:
	var map: StreetMap = _map()
	for row: Array in map.barrier_rows:
		var used: float = 0.0
		for id: Variant in row:
			used += StreetMap.barrier_width(id as String, map.scenery()) + StreetMap.BARRIER_GAP
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
	assert_almost_eq(mesh.get_aabb().size.y * nanay.scale.y, StreetMap.NANAY_HEIGHT * map.scenery(), 0.01)


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


func test_the_ice_cream_cart_is_light_and_whole() -> void:
	for id: String in ["ice_cream_cart"]:
		var counts: Vector2i = MeshAudit.open_edges_and_triangles(PropModel.PATH % id)
		assert_between(counts.y, 3000, 5500, "%s is light for phones" % id)
		assert_lt(float(counts.x) / float(counts.y), 0.35, "%s is a whole surface" % id)


func test_the_ice_cream_cart_is_parked_on_the_sidewalk_and_the_removed_models_are_gone() -> void:
	var map: StreetMap = _map()
	var half_w: float = map.layout.lane_width / 2.0
	var cart: Node3D = map.find_child("IceCreamCart", true, false) as Node3D
	assert_not_null(cart)
	assert_gt(cart.position.x, half_w + map.layout.boundary_thickness, "the cart is on the right sidewalk")
	assert_almost_eq(cart.position.y, StreetMap.CURB_HEIGHT, 0.001)
	for gone: String in ["Jeep", "Pedicab"]:
		assert_null(map.find_child(gone, true, false), "%s is removed (owner)" % gone)
	for id: String in ["jeep", "pedicab", "fishball_cart", "ironing_board", "sewing_machine", "school_zone"]:
		assert_false(FileAccess.file_exists(PropModel.PATH % id), "the %s model file is deleted (owner)" % id)
		assert_false(StreetMap.BARRIER_HEIGHTS.has(id), "and it is not in the barrier rows")


func test_no_vehicle_stands_where_the_kids_play() -> void:
	var map: StreetMap = _map()
	for id: String in ["IceCreamCart"]:
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


# ---- the top boundary: the owner's three pictures (fishball cart, tin fence, jeepney) ---------------------------------

const CAMERA_RULES: CameraRules = preload("res://data/rules/camera_rules.tres")
const PICTURE_IDS: Array[String] = ["fishball_cart", "tin_fence", "jeepney"]


func _pictures(map: StreetMap, end: float) -> Array[MeshInstance3D]:
	var row: Node3D = map.find_child("BoundaryPictures%s" % ("Far" if end < 0.0 else "Near"), true, false) as Node3D
	var found: Array[MeshInstance3D] = []
	for child: Node in row.get_children():
		if child is MeshInstance3D and String(child.name) in PICTURE_IDS:
			found.append(child as MeshInstance3D)
	return found


func test_the_three_pictures_stand_across_each_end_in_the_owners_order() -> void:
	var map: StreetMap = _map()
	for end: float in [-1.0, 1.0]:
		var pictures: Array[MeshInstance3D] = _pictures(map, end)
		var ids: Array[String] = []
		for picture: MeshInstance3D in pictures:
			ids.append(String(picture.name))
		assert_eq(ids, PICTURE_IDS, "the cart, the fence, the jeepney")
		# left to right on the screen: the camera looks toward -Z at the -Z end (x grows to the right), toward +Z at the +Z end (x shrinks)
		var xs: Array[float] = []
		for picture: MeshInstance3D in pictures:
			xs.append(picture.global_position.x)
		for k: int in range(1, xs.size()):
			assert_true(xs[k] > xs[k - 1] if end < 0.0 else xs[k] < xs[k - 1], "in order on the screen at end %s" % end)


func test_only_the_end_at_the_top_of_the_screen_shows_the_pictures() -> void:
	var map: StreetMap = _map()
	for own_side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		map.set_own_side(own_side)
		var top: float = StreetMap.top_end(own_side)
		assert_eq(top, -1.0 if own_side == MapLayout.SIDE_OWN else 1.0, "the far end for the viewer")
		for end: float in [-1.0, 1.0]:
			var row: Node3D = map.find_child("BoundaryPictures%s" % ("Far" if end < 0.0 else "Near"), true, false) as Node3D
			assert_eq(row.visible, end == top, "pictures at the top end only (own side %d, end %s)" % [own_side, end])
			var props: Array = map._row_props[end]
			assert_gt(props.size(), 3, "the row of props is there")
			for prop: Node3D in props:
				assert_eq(prop.visible, end != top, "the props stand at the bottom end only")


func test_each_picture_stands_on_the_ground_at_its_true_height_in_its_own_shape() -> void:
	var map: StreetMap = _map()
	for end: float in [-1.0, 1.0]:
		for k: int in 3:
			var picture: MeshInstance3D = _pictures(map, end)[k]
			var quad: QuadMesh = picture.mesh as QuadMesh
			var entry: Dictionary = StreetMap.BOUNDARY_PICTURES[k]
			assert_almost_eq(quad.size.y, float(entry["height"]) * map.scenery(), 0.001, "%s: true height times the street's scale" % picture.name)
			var texture: Texture2D = load(StreetMap.BOUNDARY_PATH % picture.name) as Texture2D
			assert_almost_eq(quad.size.x / quad.size.y, texture.get_size().x / texture.get_size().y, 0.001, "%s: never stretched" % picture.name)
			assert_almost_eq(quad.center_offset.y, quad.size.y / 2.0, 0.001, "its bottom edge is the ground line")
			assert_almost_eq(picture.global_position.y, 0.0, 0.001, "on the ground")
			assert_almost_eq(absf(picture.global_position.z), map.layout.lane_length / 2.0 + StreetMap.BARRIER_SETBACK, 0.001, "at the end line")
			assert_lte(absf(picture.global_position.x) + quad.size.x / 2.0, map.layout.lane_width / 2.0 + 0.001, "%s: inside the street's width" % picture.name)


func test_the_pictures_lean_back_to_face_the_camera_whichever_way_it_looks() -> void:
	assert_almost_eq(StreetMap.BOUNDARY_TILT, 90.0 - CAMERA_RULES.pitch_degrees, 0.001, "tilted by what the camera is off straight ahead")
	var map: StreetMap = _map()
	var pitch: float = deg_to_rad(CAMERA_RULES.pitch_degrees)
	for end: float in [-1.0, 1.0]:
		# the camera looks toward -Z over the -Z end and toward +Z over the +Z end, down by its pitch
		var look: Vector3 = Vector3(0.0, -sin(pitch), cos(pitch) * end)
		for picture: MeshInstance3D in _pictures(map, end):
			var normal: Vector3 = picture.global_transform.basis.z.normalized()
			assert_almost_eq(normal.dot(-look), 1.0, 0.001, "%s squarely faces the camera, so it shows undistorted" % picture.name)


func test_the_pictures_are_drawn_as_made_not_shaded_or_tinted_and_lossless() -> void:
	var map: StreetMap = _map()
	for picture: MeshInstance3D in _pictures(map, -1.0):
		var material: StandardMaterial3D = (picture.mesh as QuadMesh).material as StandardMaterial3D
		assert_eq(material.shading_mode, BaseMaterial3D.SHADING_MODE_UNSHADED, "%s: no light on it" % picture.name)
		assert_eq(material.albedo_color, Color.WHITE, "no tint")
		assert_ne(material.transparency, BaseMaterial3D.TRANSPARENCY_DISABLED, "its transparent background shows the street")
		assert_eq(picture.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF)
		var import_text: String = FileAccess.get_file_as_string((StreetMap.BOUNDARY_PATH % picture.name) + ".import")
		assert_true(import_text.contains("compress/mode=0"), "%s imports lossless" % picture.name)
		assert_true(import_text.contains("detect_3d/compress_to=0"), "and is never compressed for 3D")
		assert_true(import_text.contains("mipmaps/generate=true"), "and has mipmaps (it is small on screen)")


func test_corner_wall_pngs_are_centered_and_the_right_texture_is_mirrored() -> void:
	var map: StreetMap = _map()
	for suffix: String in ["Far", "Near"]:
		var row: Node3D = map.find_child("BoundaryPictures%s" % suffix, true, false) as Node3D
		var left: MeshInstance3D = row.get_node("RoofWallLeft") as MeshInstance3D
		var right: MeshInstance3D = row.get_node("RoofWallRight") as MeshInstance3D
		assert_eq(left.position.x, -map.layout.lane_width / 2.0)
		assert_eq(right.position.x, map.layout.lane_width / 2.0)
		assert_eq(left.position.z, right.position.z)
		assert_lt(left.position.z, 0.0, "the wall stays behind the other pictures")
		var left_quad: QuadMesh = left.mesh as QuadMesh
		var right_quad: QuadMesh = right.mesh as QuadMesh
		assert_eq(left_quad.size, right_quad.size)
		var original: StandardMaterial3D = left_quad.material as StandardMaterial3D
		var mirrored: StandardMaterial3D = right_quad.material as StandardMaterial3D
		assert_eq(original.albedo_texture.resource_path, StreetMap.BOUNDARY_PATH % "tin_fence")
		assert_eq(mirrored.albedo_texture, original.albedo_texture)
		assert_eq(original.uv1_scale.x, 1.0)
		assert_eq(mirrored.uv1_scale.x, -1.0)
		assert_eq(mirrored.uv1_offset.x, 1.0)
		assert_eq(original.shading_mode, BaseMaterial3D.SHADING_MODE_UNSHADED)
		assert_almost_eq(left_quad.size.x / left_quad.size.y, original.albedo_texture.get_size().aspect(), 0.001)


func test_roof_wall_models_and_their_textures_are_removed() -> void:
	for id: String in ["roof_wall", "roof_wall_2"]:
		for extension: String in [".glb", ".glb.import", "_prop_base_color.jpg", "_prop_base_color.jpg.import"]:
			assert_false(FileAccess.file_exists("res://assets/models/props/%s%s" % [id, extension]))
		assert_false(StreetMap.BARRIER_HEIGHTS.has(id))
		assert_false(ScaleReference.REAL_HEIGHT.has(id))
