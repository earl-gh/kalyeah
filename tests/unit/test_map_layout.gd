extends GutTest

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")


func test_the_playing_area_is_nine_by_sixteen_like_a_phone_screen() -> void:
	assert_eq(LAYOUT.lane_width, 27.0)
	assert_eq(LAYOUT.lane_length, 48.0)
	assert_almost_eq(LAYOUT.lane_width / LAYOUT.lane_length, 9.0 / 16.0, 0.001, "9:16 (width:length), so the whole street fits a portrait screen")


func test_own_post_bottom_left_enemy_post_top_right() -> void:
	var own: Vector2 = LAYOUT.base_center(MapLayout.SIDE_OWN)
	var enemy: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_lt(own.x, 0.0, "left")
	assert_gt(own.y, 0.0, "bottom (+Z is the own end)")
	assert_gt(enemy.x, 0.0, "right")
	assert_lt(enemy.y, 0.0, "top")
	assert_eq(enemy, -own, "the street is the same for both teams, turned round")
	assert_true(LAYOUT.lane_rect().has_point(own))


func test_tagged_boxes_own_top_left_enemy_bottom_right() -> void:
	var own: Rect2 = LAYOUT.jail_rect(MapLayout.SIDE_OWN)
	var enemy: Rect2 = LAYOUT.jail_rect(MapLayout.SIDE_ENEMY)
	assert_lt(own.get_center().x, 0.0, "your team's box: left")
	assert_lt(own.get_center().y, 0.0, "your team's box: top")
	assert_gt(enemy.get_center().x, 0.0, "their box: right")
	assert_gt(enemy.get_center().y, 0.0, "their box: bottom")
	assert_true(LAYOUT.lane_rect().grow(0.001).encloses(own))
	assert_true(LAYOUT.lane_rect().grow(0.001).encloses(enemy))
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		for other: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
			assert_false(LAYOUT.in_base(other, LAYOUT.jail_rect(side).get_center()), "boxes are clear of the bases")


func test_each_tagged_box_sits_in_a_corner_of_the_play_area_a_little_in_from_the_edges() -> void:
	var lane: Rect2 = LAYOUT.lane_rect()
	var own: Rect2 = LAYOUT.jail_rect(MapLayout.SIDE_OWN)
	var enemy: Rect2 = LAYOUT.jail_rect(MapLayout.SIDE_ENEMY)
	assert_true(own.position.x > lane.position.x and own.position.y > lane.position.y, "yours: the top left corner, set in from both edges")
	assert_true(enemy.end.x < lane.end.x and enemy.end.y < lane.end.y, "theirs: the bottom right corner, set in from both edges")
	assert_almost_eq(own.position - lane.position, lane.end - enemy.end, Vector2(0.001, 0.001), "the same distance in for both teams")
	assert_gt(own.size.y, own.size.x, "a portrait box, taller than wide (owner)")
	assert_lt(own.size.x / own.size.y, 0.9, "clearly portrait, not nearly square")
	assert_true(lane.grow(0.001).encloses(own))
	assert_true(lane.grow(0.001).encloses(enemy))


func test_the_rescue_area_is_an_even_ring_round_the_box_inside_the_street() -> void:
	var lane: Rect2 = LAYOUT.lane_rect().grow(0.001)
	var ring: float = LAYOUT.jail_size.x * LAYOUT.rescue_scale
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var box: Rect2 = LAYOUT.jail_rect(side)
		var area: Rect2 = LAYOUT.rescue_rect(side)
		assert_true(area.grow(0.001).encloses(box), "the area holds the box")
		assert_true(lane.encloses(area), "and stays in the play area")
		assert_almost_eq(area.size.x, box.size.x + ring * 2.0, 0.001, "the same ring on every side: the box is the inside, the ring the outside")
		assert_almost_eq(area.size.y, box.size.y + ring * 2.0, 0.001)
		assert_almost_eq(box.position.x - area.position.x, ring, 0.001, "the ring is there on the street's edge side too (the street does not cut it)")


func test_a_base_is_a_rounded_rectangle_inside_the_street_the_other_team_captures_in() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var base: Rect2 = LAYOUT.base_rect(side)
		assert_true(LAYOUT.lane_rect().grow(0.001).encloses(base), "wholly inside the street")
		assert_true(LAYOUT.in_base(side, base.get_center()))
		assert_true(LAYOUT.in_base(side, base.position + Vector2(LAYOUT.base_corner, 0.05)), "a straight side")
		assert_false(LAYOUT.in_base(side, base.position + Vector2(0.05, 0.05)), "every corner is rounded off, the one in the street's corner too")
		assert_false(LAYOUT.in_base(side, base.end - Vector2(0.05, 0.05)))
		assert_false(LAYOUT.in_base(side, base.position + Vector2(base.size.x + 0.1, 0.0)))
	assert_gt(LAYOUT.base_corner, 0.0)
	assert_lt(LAYOUT.base_corner, 1.5, "gently rounded")


func test_bases_boxes_and_rescue_areas_are_always_blue_for_you_and_red_for_them() -> void:
	Settings.set_colorblind(true)
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var want: Color = StreetMap.OWN_COLOR if side == MapLayout.SIDE_OWN else StreetMap.ENEMY_COLOR
		for colour: Color in map.zone_colors(side):
			assert_true(Color(colour, 1.0).is_equal_approx(want), "fixed team colour, whatever the colour-blind setting")
	Settings.set_colorblind(false)


func test_only_two_cardboard_walls_stand_in_the_play_area_one_by_each_base() -> void:
	var walls: Array[Rect2] = LAYOUT.obstacle_rects()
	assert_eq(walls.size(), 2, "one each way round (owner: only these two)")
	var lane: Rect2 = LAYOUT.lane_rect()
	var own_base: Vector2 = LAYOUT.base_center(MapLayout.SIDE_OWN)
	var enemy_base: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	# the left-most wall: against the left curb, 6 m above (toward the enemy end) the bottom-left base circle
	var left: Rect2 = walls[0] if walls[0].position.x < 0.0 else walls[1]
	assert_almost_eq(left.position.x, lane.position.x, 0.001, "against the left curb")
	assert_almost_eq(left.end.y, own_base.y - LAYOUT.base_radius - 6.0, 0.001, "6 m above the bottom-left base")
	# the right-most wall: against the right curb, 6 m below the top-right base circle
	var right: Rect2 = walls[1] if walls[0].position.x < 0.0 else walls[0]
	assert_almost_eq(right.end.x, lane.end.x, 0.001, "against the right curb")
	assert_almost_eq(right.position.y, enemy_base.y + LAYOUT.base_radius + 6.0, 0.001, "6 m below the top-right base")


func test_jail_slots_stay_inside_the_box() -> void:
	for index: int in 6:
		assert_true(LAYOUT.jail_rect(MapLayout.SIDE_OWN).has_point(LAYOUT.jail_slot(MapLayout.SIDE_OWN, index)))
	assert_ne(LAYOUT.jail_slot(MapLayout.SIDE_OWN, 0), LAYOUT.jail_slot(MapLayout.SIDE_OWN, 1), "kids don't stand on each other")


func test_the_box_fits_four_kids_and_no_more_room() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var kid: float = rules.player_radius * 2.0
	assert_eq(LAYOUT.jail_columns * LAYOUT.jail_rows, 4, "four slots")
	var box: Rect2 = LAYOUT.jail_rect(MapLayout.SIDE_OWN)
	var slots: Array[Vector2] = []
	for index: int in 4:
		var slot: Vector2 = LAYOUT.jail_slot(MapLayout.SIDE_OWN, index)
		assert_true(box.grow(-rules.player_radius).has_point(slot), "kid %d fits inside the tape" % index)
		for other: Vector2 in slots:
			assert_gte(slot.distance_to(other), kid, "kids don't overlap")
		slots.append(slot)
	assert_lte(box.size.y, kid * LAYOUT.jail_rows * 2.5, "the box is not much bigger than four padded kids")


func test_base_is_a_circle_round_the_post() -> void:
	var post: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_true(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post))
	assert_true(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post + Vector2(0.0, LAYOUT.base_radius - 0.1)))
	assert_false(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post + Vector2(0.0, LAYOUT.base_radius + 0.1)))
	assert_false(LAYOUT.in_base(MapLayout.SIDE_OWN, post))
	assert_true(LAYOUT.in_base(MapLayout.SIDE_OWN, LAYOUT.spawn_center(MapLayout.SIDE_OWN)), "players start at their post")


func test_fewer_obstacles_mirrored_for_both_halves() -> void:
	var rects: Array[Rect2] = LAYOUT.obstacle_rects()
	var count: int = LAYOUT.obstacles.size()
	assert_eq(rects.size(), count * 2)
	assert_lte(rects.size(), 6, "only a few cover pieces (owner: reduced twice)")
	for i: int in count:
		assert_true(rects[i + count].is_equal_approx(Rect2(-rects[i].end, rects[i].size)), "turned round the center")


func test_obstacles_inside_the_street_and_clear_of_bases_boxes_and_each_other() -> void:
	var rects: Array[Rect2] = LAYOUT.obstacle_rects()
	var street: Rect2 = LAYOUT.lane_rect()
	for i: int in rects.size():
		var rect: Rect2 = rects[i]
		assert_true(street.encloses(rect), "obstacle %d inside the street" % i)
		for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
			assert_false(rect.intersects(LAYOUT.jail_rect(side).grow(1.0)), "obstacle %d clear of a box" % i)
			var post: Vector2 = LAYOUT.base_center(side)
			var closest: Vector2 = post.clamp(rect.position, rect.end)
			assert_gt(closest.distance_to(post), LAYOUT.base_radius, "obstacle %d clear of a base" % i)
		for j: int in range(i + 1, rects.size()):
			assert_false(rect.grow(0.8).intersects(rects[j]), "obstacles %d and %d leave room to pass" % [i, j])


func test_every_obstacle_is_a_cardboard_wall() -> void:
	for index: int in LAYOUT.obstacle_rects().size():
		assert_eq(LAYOUT.obstacle_prop(index), &"cardboard_wall", "inside the play area: cardboard walls only (owner)")


func test_boundary_walls_enclose_lane_without_overlapping_it() -> void:
	var lane: Rect2 = LAYOUT.lane_rect()
	var boundaries: Array[Rect2] = LAYOUT.boundary_rects()
	assert_eq(boundaries.size(), 4)
	for rect: Rect2 in boundaries:
		assert_false(rect.intersects(lane))


func test_street_scene_builds_bases_boxes_cover_and_dressing() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var geometry: Node3D = map.get_node("%Geometry") as Node3D
	assert_gt(geometry.get_child_count(), 60, "street, cover, 2 bases, 2 boxes, houses and props")
	assert_eq(map.base_color(MapLayout.SIDE_OWN), StreetMap.OWN_COLOR)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.ENEMY_COLOR)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		for node: Node in map.get_node("%Geometry").find_children("Outline", "MeshInstance3D", true, false):
			fail_test("no outline round the posts (owner), found one at base %d" % side)
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.OWN_COLOR, "your post is always blue")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var zone: Array[Color] = map.zone_colors(side)
		assert_eq(zone.size(), 4, "base and rescue area are tinted: the box inside has no colour")
		assert_eq(zone[0], zone[2], "the rescue area has the same tape as the base")
		assert_eq(zone[1], zone[3], "and the same faint fill")
		assert_lt(zone[1].a, 0.5, "the fill stays faint")


func test_each_tagged_box_is_a_gently_rounded_portrait_rectangle_with_no_cardboard_and_sits_in_from_the_edges() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var box: Rect2 = LAYOUT.jail_rect(side)
		var outline: Array[Vector2] = StreetMap.rounded_outline(box, StreetMap.JAIL_CORNER_RADIUS)
		assert_eq(outline.size(), 4 * (StreetMap.ZONE_CORNER_STEPS + 1))
		for point: Vector2 in outline:
			assert_true(box.grow(0.001).has_point(point), "the outline stays inside the box")
		assert_false(outline.has(box.position), "the corners are cut: the sharp corner is not on the outline")
		assert_false(outline.has(box.end))
		var street: Rect2 = LAYOUT.lane_rect()
		assert_gt(box.position.x - street.position.x, 1.0, "set in from the street's edge")
		assert_gt(street.end.x - box.end.x, 1.0)
		assert_gt(box.position.y - street.position.y, 1.0, "and from its end")
		assert_gt(street.end.y - box.end.y, 1.0)


func test_props_stay_low_poly() -> void:
	var budgets: Dictionary = {
		"house": Props.house(0), "store": Props.sari_sari(),
	}
	for name: String in budgets:
		var mesh: ArrayMesh = budgets[name]
		var vertices: int = mesh.surface_get_array_len(0)
		assert_lt(vertices, 3000, "%s has %d vertices" % [name, vertices])
		assert_eq(mesh.surface_get_material(0), LowPoly.material(), "%s uses the one shared material" % name)


func test_the_model_props_are_light_and_standing_on_the_ground() -> void:
	var limits: Dictionary = {"electric_pole": 3200, "cardboard_wall": 4200, "bench": 4200, "school_zone": 4200, "rooster_tent": 4200,
		"ironing_board": 4200, "fishball_cart": 4700, "basketball_pole": 3700, "truck_wheels": 4200, "crates": 4200,
		"sacks": 4200, "sewing_machine": 4200, "no_id_no_entry": 4200, "ebike": 4700, "tricycle": 5200, "jeep": 5700,
		"pedicab": 5200, "ice_cream_cart": 5200, "nanay": 6200,
		"nena_store": 6200, "vulcanizing": 6200, "barber_shop": 6200, "house_1": 6200, "house_2": 6200, "house_3": 6200, "karinderya": 6200, "cat": 3200, "dog": 3200, "chicken_cage": 4200, "drainage_cover": 2700, "tricycle_terminal": 6200, "plank_fence": 4200, "roof_wall": 4200, "roof_wall_2": 4200}
	for id: String in limits:
		var model: Node3D = autofree(PropModel.make(id)) as Node3D
		var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var vertices: int = mesh.mesh.surface_get_array_len(0)
		assert_lt(vertices, limits[id] * 3, "%s has %d vertices" % [id, vertices])
		var box: AABB = mesh.mesh.get_aabb()
		assert_almost_eq(box.position.y, 0.0, 0.01, "%s stands on the ground" % id)
		assert_almost_eq(box.position.x + box.size.x / 2.0, 0.0, 0.01, "%s is centred" % id)
		var material: StandardMaterial3D = mesh.get_surface_override_material(0) as StandardMaterial3D
		assert_eq(material.roughness, 1.0, "%s is matte" % id)


func test_cover_models_fill_their_footprint_and_stay_about_a_metre_high() -> void:
	for id: String in ["bench", "cardboard_wall", "school_zone", "rooster_tent"]:
		var footprint: Vector2 = Vector2(2.6, 0.6)
		var model: Node3D = autofree(PropModel.fit(id, footprint, 1.15)) as Node3D
		var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var box: AABB = mesh.mesh.get_aabb()
		assert_almost_eq(box.size.x * model.scale.x, footprint.x, 0.02, "%s fills the length" % id)
		assert_almost_eq(box.size.z * model.scale.z, footprint.y, 0.02, "%s fills the depth" % id)
		assert_lte(box.size.y * model.scale.y, 1.16, "%s is no taller than the cap" % id)
		assert_gt(box.size.y * model.scale.y, 0.5, "%s is tall enough to hide behind" % id)


func _post_front(map: StreetMap, side: int) -> float:
	var center: Vector2 = LAYOUT.base_center(side)
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node != null and node.scene_file_path.ends_with("electric_pole.glb") and Vector2(node.position.x, node.position.z).is_equal_approx(center):
			var front: Vector3 = node.global_basis.z.normalized()
			return front.dot(Vector3(-center.x, 0.0, -center.y).normalized())
	fail_test("no post at base %d" % side)
	return 0.0


func test_the_enemy_post_faces_the_middle_and_your_own_is_turned_round() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	assert_gt(_post_front(map, MapLayout.SIDE_ENEMY), 0.99, "their flyers face the play area")
	assert_lt(_post_front(map, MapLayout.SIDE_OWN), -0.99, "your (blue) post is turned 180 degrees")
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_lt(_post_front(map, MapLayout.SIDE_ENEMY), -0.99, "the other team: their own post is the turned one")
	assert_gt(_post_front(map, MapLayout.SIDE_OWN), 0.99)


func test_the_end_rows_mix_every_street_thing_and_stay_out_of_the_play_area() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var seen: Dictionary = {}
	var half_l: float = LAYOUT.lane_length / 2.0
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node == null or not node.scene_file_path.begins_with("res://assets/models/props/"):
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		if not StreetMap.BARRIER_HEIGHTS.has(id) or absf(node.position.z) < half_l:
			continue
		seen[id] = int(seen.get(id, 0)) + 1
		var mesh: MeshInstance3D = node.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var depth: float = mesh.mesh.get_aabb().size.z * node.scale.z
		assert_gt(absf(node.position.z) - depth / 2.0, half_l, "%s stays behind the end line" % id)
		assert_lte(absf(node.position.x), LAYOUT.lane_width / 2.0, "%s across the street" % id)
	for id: String in StreetMap.BARRIER_HEIGHTS:
		assert_true(seen.has(id), "%s is in an end row" % id)
	assert_lte(int(seen.get("rooster_tent", 0)), 4, "at most two A-frames per row, not a row of them")


func test_the_street_slabs_are_plain_boxes_without_a_light_edge_line() -> void:
	# `box` rounds its edges and lightens them: seen edge-on that is a thin white line along the street
	var kit: LowPoly = LowPoly.new()
	var colour: Color = Color(0.3, 0.3, 0.31)
	kit.plain_box(Vector3.ZERO, Vector3(24.0, 0.1, 80.0), colour, colour)
	assert_eq(kit.triangle_count(), 12, "a plain box is just six faces")
	var mesh: ArrayMesh = kit.commit()
	var colors: PackedColorArray = mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR]
	for vertex_colour: Color in colors:
		assert_lte(vertex_colour.get_luminance(), colour.get_luminance() + 0.001, "no face is lighter than the slab")


## The sidewalks are flat (owner: no raised curb): the street and both sidewalks are one slab whose top is the ground.
func test_the_sidewalks_are_flat_with_the_street() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var street: MeshInstance3D = map.get_node("%Geometry").get_child(0) as MeshInstance3D
	var box: AABB = street.mesh.get_aabb()
	assert_almost_eq(box.end.y, 0.0, 0.001, "nothing stands above the street")
	var reach: float = LAYOUT.boundary_thickness + StreetMap.SIDEWALK_WIDTH
	assert_almost_eq(box.size.x, LAYOUT.lane_width + reach * 2.0, 0.01, "one slab under the street and both sidewalks")
	assert_eq(StreetMap.CURB_HEIGHT, 0.0)


func test_the_street_and_sidewalks_use_the_code_drawn_cement() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var street: MeshInstance3D = map.get_node("%Geometry").get_child(0) as MeshInstance3D
	var material: ShaderMaterial = street.material_override as ShaderMaterial
	assert_not_null(material, "the street slab and sidewalks are drawn by the cement shader")
	assert_eq(material.shader, StreetMap.STREET_SHADER)
	assert_almost_eq(float(material.get_shader_parameter("lane_half_width")), LAYOUT.lane_width / 2.0, 0.001, "it knows where the sidewalks start")
	assert_false(ResourceLoader.exists("res://assets/textures/cement_generated.png"), "no generated image files")



# ---- the tile grid (Clash Royale's arena: 18 x 32 tiles) -------------------------------

func test_the_street_is_an_18_by_32_tile_grid_of_1_5_metre_tiles() -> void:
	assert_eq(LAYOUT.tiles, Vector2i(18, 32), "like Clash Royale's arena")
	assert_almost_eq(LAYOUT.tile_size, 1.5, 0.0001)
	assert_almost_eq(LAYOUT.lane_width, 27.0, 0.0001, "18 tiles across")
	assert_almost_eq(LAYOUT.lane_length, 48.0, 0.0001, "32 tiles long")


func test_tiles_and_metres_convert_both_ways() -> void:
	assert_eq(LAYOUT.tile_center(Vector2i(0, 0)), Vector2(-12.75, -23.25), "the top-left tile")
	assert_eq(LAYOUT.tile_center(Vector2i(17, 31)), Vector2(12.75, 23.25), "the bottom-right tile")
	for tile: Vector2i in [Vector2i(0, 0), Vector2i(8, 15), Vector2i(9, 16), Vector2i(17, 31)]:
		assert_eq(LAYOUT.tile_at(LAYOUT.tile_center(tile)), tile, "round trip %s" % tile)
	assert_eq(LAYOUT.to_tiles(Vector2.ZERO), Vector2(9.0, 16.0), "the middle of the street is the corner of four tiles")
	assert_eq(LAYOUT.lane_rect().size, Vector2(27.0, 48.0))


func test_the_posts_and_boxes_sit_where_the_grid_says() -> void:
	var own: Vector2 = LAYOUT.to_tiles(LAYOUT.base_center(MapLayout.SIDE_OWN))
	assert_almost_eq(own.x, 2.5 / 1.5, 0.0001, "your post: 1 2/3 tiles from the left curb")
	assert_almost_eq(own.y, 32.0 - 2.5 / 1.5, 0.0001, "and 1 2/3 tiles from your end")
	assert_almost_eq(LAYOUT.base_radius / LAYOUT.tile_size, 3.0, 0.0001, "the base circle is 3 tiles")
	var wall: Rect2 = LAYOUT.obstacle_rects()[0]
	assert_almost_eq(wall.position.x, -LAYOUT.lane_width / 2.0, 0.0001, "the cover wall is against the left curb")
	assert_almost_eq(wall.size.x / LAYOUT.tile_size, 2.0, 0.0001, "two tiles long")
