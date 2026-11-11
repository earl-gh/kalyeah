extends GutTest

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")


func test_the_playing_area_is_a_circular_84m_barangay_on_a_58_by_58_grid() -> void:
	assert_true(LAYOUT.is_circular(), "circular Barangay arena")
	assert_almost_eq(LAYOUT.arena_radius, 84.0, 0.001, "84 m circular barangay radius (168 m diameter)")
	assert_eq(LAYOUT.lane_width, 174.0)
	assert_eq(LAYOUT.lane_length, 174.0)
	assert_almost_eq(StreetMap.SIDEWALK_WIDTH, 2.4, 0.0001)


func test_own_base_south_court_and_enemy_base_north_hall_are_symmetric() -> void:
	var own: Vector2 = LAYOUT.base_center(MapLayout.SIDE_OWN)
	var enemy: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_almost_eq(own.x, 0.0, 0.001, "centered on X")
	assert_gt(own.y, 0.0, "South (+Z) Basketball Court is SIDE_OWN")
	assert_almost_eq(enemy.x, 0.0, 0.001)
	assert_lt(enemy.y, 0.0, "North (-Z) Barangay Hall is SIDE_ENEMY")
	assert_eq(enemy, -own, "180-degree point-symmetric bases")
	assert_true(LAYOUT.in_arena(own))
	assert_true(LAYOUT.in_arena(enemy))


func test_rescue_areas_are_point_symmetric_inside_the_circular_arena_and_clear_of_bases() -> void:
	var own: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_OWN)
	var enemy: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_ENEMY)
	assert_lt(own.get_center().x, 0.0, "own rescue patio in NW")
	assert_lt(own.get_center().y, 0.0)
	assert_gt(enemy.get_center().x, 0.0, "enemy rescue patio in SE")
	assert_gt(enemy.get_center().y, 0.0)
	assert_eq(own.size, LAYOUT.rescue_size)
	assert_almost_eq(enemy.position, -own.end, Vector2(0.001, 0.001), "180-degree point-symmetric rescue patios")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var r_rect: Rect2 = LAYOUT.rescue_rect(side)
		assert_true(LAYOUT.in_arena(r_rect.get_center()), "rescue center inside circular arena")
		for other: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
			assert_false(LAYOUT.in_base(other, r_rect.get_center()), "rescue areas are clear of bases")


func test_a_base_is_a_rounded_rectangle_inside_the_arena_the_other_team_captures_in() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var base: Rect2 = LAYOUT.base_rect(side)
		assert_true(LAYOUT.in_arena(base.get_center()))
		assert_true(LAYOUT.in_base(side, base.get_center()))
		assert_true(LAYOUT.in_base(side, base.position + Vector2(LAYOUT.base_corner, 0.05)), "straight edge inside base")
		assert_false(LAYOUT.in_base(side, base.position + Vector2(0.05, 0.05)), "rounded corners trimmed")
		assert_false(LAYOUT.in_base(side, base.end - Vector2(0.05, 0.05)))
		assert_false(LAYOUT.in_base(side, base.position + Vector2(base.size.x + 0.1, 0.0)))
	assert_gt(LAYOUT.base_corner, 0.0)
	assert_lt(LAYOUT.base_corner, LAYOUT.base_radius / 3.0, "gently rounded")


func test_bases_and_rescue_areas_are_always_blue_for_you_and_red_for_them() -> void:
	Settings.set_colorblind(true)
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var want: Color = StreetMap.OWN_COLOR if side == MapLayout.SIDE_OWN else StreetMap.ENEMY_COLOR
		for colour: Color in map.zone_colors(side):
			assert_true(Color(colour, 1.0).is_equal_approx(want), "fixed team colour regardless of colorblind setting")
	Settings.set_colorblind(false)


func test_tight_tabi_tabing_bahay_obstacles_and_estero_water_are_point_symmetric() -> void:
	var rects: Array[Rect2] = LAYOUT.obstacle_rects()
	var count: int = LAYOUT.obstacles.size()
	assert_eq(count, 18, "18 half-arena interior bahay & cover blocks")
	assert_eq(rects.size(), 36, "36 mirrored interior blocks creating tight 3.8m-5.8m eskinitas")
	for i: int in count:
		assert_true(rects[i + count].is_equal_approx(Rect2(-rects[i].end, rects[i].size)), "obstacle %d mirrored around origin" % i)
	var water: Array[Rect2] = LAYOUT.water_rects()
	assert_eq(water.size(), 4, "Estero Creek split into 4 segments framing 3 bridges")
	assert_eq(LAYOUT.walk_obstacle_rects().size(), rects.size() + water.size(), "water blocks walking while allowing projectiles")


func test_tagged_places_are_outside_the_circular_curb_beside_each_rescue_area() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var area: Rect2 = LAYOUT.rescue_rect(side)
		for index: int in LAYOUT.tagged_places:
			var place: Vector2 = LAYOUT.tagged_place(side, index)
			assert_false(LAYOUT.in_arena(place), "tagged kid stands outside playable circle on perimeter sidewalk")
			assert_eq(signf(place.x), signf(area.get_center().x), "on the same side as its rescue area")
			assert_between(place.y, area.position.y - 2.0, area.end.y + 2.0, "aligned with rescue waiting shed")
			assert_eq(LAYOUT.tagged_facing(side).x, -signf(place.x), "facing inward toward arena")
	assert_eq(LAYOUT.tagged_place(MapLayout.SIDE_OWN, LAYOUT.tagged_places), LAYOUT.tagged_place(MapLayout.SIDE_OWN, 0))


func test_tagged_places_fit_a_whole_5v5_team_without_overlapping() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	assert_eq(RoomService.MAX_TEAM_SIZE, 5)
	assert_gte(LAYOUT.tagged_places, RoomService.MAX_TEAM_SIZE, "5 slots for a full 5v5 team")
	for index: int in LAYOUT.tagged_places - 1:
		var gap: float = LAYOUT.tagged_place(MapLayout.SIDE_OWN, index + 1).distance_to(LAYOUT.tagged_place(MapLayout.SIDE_OWN, index))
		assert_gte(gap, rules.player_radius * 2.0, "kids %d and %d don't overlap" % [index, index + 1])


func test_base_radius_surrounds_base_center_and_spawns() -> void:
	var post: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_true(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post))
	assert_true(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post + Vector2(0.0, LAYOUT.base_radius - 0.1)))
	assert_false(LAYOUT.in_base(MapLayout.SIDE_ENEMY, post + Vector2(0.0, LAYOUT.base_radius + 0.1)))
	assert_false(LAYOUT.in_base(MapLayout.SIDE_OWN, post))
	assert_true(LAYOUT.in_base(MapLayout.SIDE_OWN, LAYOUT.spawn_center(MapLayout.SIDE_OWN)), "players start inside their base")


func test_boundary_walls_enclose_bounding_lane_without_overlapping_it() -> void:
	var lane: Rect2 = LAYOUT.lane_rect()
	var boundaries: Array[Rect2] = LAYOUT.boundary_rects()
	assert_eq(boundaries.size(), 4)
	for rect: Rect2 in boundaries:
		assert_false(rect.intersects(lane))


func test_street_scene_builds_bases_boxes_cover_and_dressing() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var geometry: Node3D = map.get_node("%Geometry") as Node3D
	assert_gt(geometry.get_child_count(), 120, "circular barangay ground, creek, 3 bridges, 36 interior blocks, 40 poles, skyline & chalk zones")
	assert_eq(map.base_color(MapLayout.SIDE_OWN), StreetMap.OWN_COLOR)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.ENEMY_COLOR)
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.OWN_COLOR, "your post is always blue")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var zone: Array[Color] = map.zone_colors(side)
		assert_eq(zone.size(), 2, "a base and a rescue area: chalk outlines only, no fill")
		assert_eq(Color(zone[0], 1.0), Color(zone[1], 1.0))


func test_a_rescue_areas_chalk_is_white_while_it_counts_down_and_the_teams_colour_once_ready() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	map.set_own_side(sim.side_for_team(0))
	var own_side: int = sim.side_for_team(0)
	var enemy_side: int = sim.side_for_team(1)
	sim.rescue_ready_left[0] = 5.0
	sim.rescue_ready_left[1] = 0.0
	map.sync_rescue(sim)
	assert_true(Color(map.zone_colors(own_side)[1], 1.0).is_equal_approx(Color.WHITE), "counting down: white chalk")
	assert_true(Color(map.zone_colors(enemy_side)[1], 1.0).is_equal_approx(StreetMap.ENEMY_COLOR), "ready: red chalk")
	assert_true(Color(map.zone_colors(own_side)[0], 1.0).is_equal_approx(StreetMap.OWN_COLOR))
	assert_almost_eq(map.zone_colors(own_side)[1].a, StreetMap.RESCUE_WHITE_ALPHA, 0.0001)
	assert_almost_eq(map.zone_colors(enemy_side)[1].a, StreetMap.CHALK_ALPHA, 0.0001)


func test_each_rescue_area_is_a_gently_rounded_rectangle() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var area: Rect2 = LAYOUT.rescue_rect(side)
		var outline: Array[Vector2] = StreetMap.rounded_outline(area, StreetMap.RESCUE_CORNER_RADIUS)
		assert_eq(outline.size(), 4 * (StreetMap.ZONE_CORNER_STEPS + 1))
		for point: Vector2 in outline:
			assert_true(area.grow(0.001).has_point(point))
		assert_false(outline.has(area.position))
		assert_false(outline.has(area.end))


func test_props_stay_low_poly() -> void:
	var budgets: Dictionary[String, ArrayMesh] = {
		"house": Props.house(0),
		"store": Props.sari_sari(),
	}
	for prop_name: String in budgets:
		var mesh: ArrayMesh = budgets[prop_name]
		var vertices: int = mesh.surface_get_array_len(0)
		assert_lt(vertices, 3000, "%s has %d vertices" % [prop_name, vertices])
		assert_eq(mesh.surface_get_material(0), LowPoly.material())


func test_the_base_posts_are_blue_for_you_and_red_for_them_scaled_to_layout_post_height() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var own: Node3D = map._posts[MapLayout.SIDE_OWN]
	var enemy: Node3D = map._posts[MapLayout.SIDE_ENEMY]
	assert_true((own.get_node(StreetMap.POST_BLUE) as Node3D).visible, "your post is blue")
	assert_false((own.get_node(StreetMap.POST_RED) as Node3D).visible)
	assert_true((enemy.get_node(StreetMap.POST_RED) as Node3D).visible, "enemy post is red")
	assert_false((enemy.get_node(StreetMap.POST_BLUE) as Node3D).visible)
	for post: Node3D in [own, enemy]:
		for id: String in [StreetMap.POST_BLUE, StreetMap.POST_RED]:
			var model: Node3D = post.get_node(id) as Node3D
			var nat_h: float = map._kit_bounds(id).size.y
			assert_almost_eq(nat_h * model.scale.y, map.post_height(), 0.02, "%s matches post_height" % id)
			var even: float = map.post_height() / nat_h
			assert_almost_eq(model.scale.x, even * LAYOUT.post_width_share, 0.001)
			assert_almost_eq(model.scale.z, even * LAYOUT.post_width_share, 0.001)
		assert_almost_eq(post.rotation.y, 0.0, 0.001)
		assert_almost_eq((post.get_node(StreetMap.POST_BLUE) as Node3D).rotation.y, StreetMap.BLUE_POST_TURN, 0.001)
		assert_almost_eq((post.get_node(StreetMap.POST_RED) as Node3D).rotation.y, 0.0, 0.001)
	assert_eq(Vector2(own.position.x, own.position.z), LAYOUT.post_position(MapLayout.SIDE_OWN))
	assert_almost_eq(own.position.y, 0.0, 0.001)
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_true((enemy.get_node(StreetMap.POST_BLUE) as Node3D).visible)
	assert_true((own.get_node(StreetMap.POST_RED) as Node3D).visible)
	assert_false((own.get_node(StreetMap.POST_BLUE) as Node3D).visible)
	for post: Node3D in [own, enemy]:
		assert_almost_eq(absf(post.rotation.y), PI, 0.001)


func test_the_street_slabs_are_plain_boxes_without_a_light_edge_line() -> void:
	var kit: LowPoly = LowPoly.new()
	var colour: Color = Color(0.3, 0.3, 0.31)
	kit.plain_box(Vector3.ZERO, Vector3(24.0, 0.1, 80.0), colour, colour)
	assert_eq(kit.triangle_count(), 12)
	var mesh: ArrayMesh = kit.commit()
	var colors: PackedColorArray = mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray
	for vertex_colour: Color in colors:
		assert_lte(vertex_colour.get_luminance(), colour.get_luminance() + 0.001)


func test_the_ground_plane_is_flat_and_uses_the_barangay_ground_shader() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var ground: MeshInstance3D = map.find_child("BarangayGround", true, false) as MeshInstance3D
	assert_not_null(ground)
	var box: AABB = ground.mesh.get_aabb()
	assert_almost_eq(box.end.y, 0.0, 0.001, "ground plane sits cleanly at y = 0")
	assert_eq(StreetMap.CURB_HEIGHT, 0.0)
	var mat: ShaderMaterial = (ground.mesh as PlaneMesh).material as ShaderMaterial
	assert_not_null(mat)
	assert_eq(mat.shader, StreetMap.GROUND_SHADER)
	assert_almost_eq(float(mat.get_shader_parameter("arena_radius")), LAYOUT.arena_radius, 0.001)
	assert_false(ResourceLoader.exists("res://assets/textures/cement_generated.png"))


func test_the_grid_is_58_by_58_of_3_metre_tiles_and_converts_both_ways() -> void:
	assert_eq(LAYOUT.tiles, Vector2i(58, 58))
	assert_almost_eq(LAYOUT.tile_size, 3.0, 0.0001)
	assert_almost_eq(LAYOUT.lane_width, 174.0, 0.0001)
	assert_almost_eq(LAYOUT.lane_length, 174.0, 0.0001)
	assert_eq(LAYOUT.tile_center(Vector2i(0, 0)), Vector2(-85.5, -85.5))
	assert_eq(LAYOUT.tile_center(Vector2i(57, 57)), Vector2(85.5, 85.5))
	for tile: Vector2i in [Vector2i(0, 0), Vector2i(28, 28), Vector2i(29, 29), Vector2i(57, 57)]:
		assert_eq(LAYOUT.tile_at(LAYOUT.tile_center(tile)), tile, "round trip %s" % tile)
	assert_eq(LAYOUT.to_tiles(Vector2.ZERO), Vector2(29.0, 29.0))
	assert_eq(LAYOUT.lane_rect().size, Vector2(174.0, 174.0))


func test_the_post_proportions_stay_slender_and_readable() -> void:
	assert_almost_eq(LAYOUT.post_height, 11.8, 0.001)
	assert_almost_eq(LAYOUT.post_width_share, 0.72, 0.001)
	assert_lt(LAYOUT.post_width_share, 1.0)
	assert_gt(LAYOUT.post_width_share, 0.5)
