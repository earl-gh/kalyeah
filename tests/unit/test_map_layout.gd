extends GutTest

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")


func test_the_playing_area_is_24_by_48_so_the_sidewalks_can_be_twice_as_wide() -> void:
	assert_eq(LAYOUT.lane_width, 24.0)
	assert_eq(LAYOUT.lane_length, 48.0)
	assert_almost_eq(LAYOUT.lane_width / LAYOUT.lane_length, 1.0 / 2.0, 0.001, "1:2: narrower than the 9:16 it was, so the street and its wider sidewalks fit a portrait screen")
	assert_almost_eq(StreetMap.SIDEWALK_WIDTH, 2.4, 0.0001, "the sidewalks are 2.4 m (1.2 m before): twice as wide")


func test_own_post_bottom_left_enemy_post_top_right() -> void:
	var own: Vector2 = LAYOUT.base_center(MapLayout.SIDE_OWN)
	var enemy: Vector2 = LAYOUT.base_center(MapLayout.SIDE_ENEMY)
	assert_lt(own.x, 0.0, "left")
	assert_gt(own.y, 0.0, "bottom (+Z is the own end)")
	assert_gt(enemy.x, 0.0, "right")
	assert_lt(enemy.y, 0.0, "top")
	assert_eq(enemy, -own, "the street is the same for both teams, turned round")
	assert_true(LAYOUT.lane_rect().has_point(own))


func test_rescue_areas_are_against_the_side_edges_own_top_left_enemy_bottom_right() -> void:
	var lane: Rect2 = LAYOUT.lane_rect()
	var own: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_OWN)
	var enemy: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_ENEMY)
	assert_lt(own.get_center().x, 0.0, "your team's rescue area: left")
	assert_lt(own.get_center().y, 0.0, "your team's rescue area: top")
	assert_gt(enemy.get_center().x, 0.0, "their rescue area: right")
	assert_gt(enemy.get_center().y, 0.0, "their rescue area: bottom")
	assert_eq(own.size, LAYOUT.rescue_size, "one rectangle, its size from the data")
	assert_almost_eq(own.position.x, lane.position.x, 0.001, "yours touches the left edge of the street")
	assert_almost_eq(enemy.end.x, lane.end.x, 0.001, "theirs touches the right edge")
	assert_true(lane.grow(0.001).encloses(own), "inside the play area")
	assert_true(lane.grow(0.001).encloses(enemy))
	assert_almost_eq(enemy.position, -own.end, Vector2(0.001, 0.001), "the street is the same for both teams, turned round")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		for other: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
			assert_false(LAYOUT.in_base(other, LAYOUT.rescue_rect(side).get_center()), "rescue areas are clear of the bases")


func test_the_outer_end_of_each_rescue_area_is_level_with_the_base_line_beside_it() -> void:
	var own_area: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_OWN)
	var enemy_base: Rect2 = LAYOUT.base_rect(MapLayout.SIDE_ENEMY)
	assert_almost_eq(own_area.position.y, enemy_base.end.y, 0.001, "the top line of the top left area is the bottom line of the top right base")
	var enemy_area: Rect2 = LAYOUT.rescue_rect(MapLayout.SIDE_ENEMY)
	var own_base: Rect2 = LAYOUT.base_rect(MapLayout.SIDE_OWN)
	assert_almost_eq(enemy_area.end.y, own_base.position.y, 0.001, "and the bottom line of the bottom right area is the top line of the bottom left base")


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


func test_bases_and_rescue_areas_are_always_blue_for_you_and_red_for_them() -> void:
	Settings.set_colorblind(true)
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var want: Color = StreetMap.OWN_COLOR if side == MapLayout.SIDE_OWN else StreetMap.ENEMY_COLOR
		for colour: Color in map.zone_colors(side):
			assert_true(Color(colour, 1.0).is_equal_approx(want), "fixed team colour, whatever the colour-blind setting")
	Settings.set_colorblind(false)


func test_no_cover_stands_in_the_play_area() -> void:
	assert_eq(LAYOUT.obstacle_rects().size(), 0, "no walls: the street is open (owner removed the cardboard walls)")


func test_tagged_places_are_on_the_sidewalk_beside_the_rescue_area_facing_the_street() -> void:
	var lane: Rect2 = LAYOUT.lane_rect()
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var area: Rect2 = LAYOUT.rescue_rect(side)
		for index: int in LAYOUT.tagged_places:
			var place: Vector2 = LAYOUT.tagged_place(side, index)
			assert_false(lane.has_point(place), "outside the play area, on the sidewalk")
			assert_almost_eq(absf(place.x), lane.end.x + LAYOUT.boundary_thickness, 0.001, "just past the street's edge")
			assert_eq(signf(place.x), signf(area.get_center().x), "on the same side as its rescue area")
			assert_between(place.y, area.position.y, area.end.y, "level with the rescue area")
			assert_eq(LAYOUT.tagged_facing(side).x, -signf(place.x), "facing in")
	assert_eq(LAYOUT.tagged_place(MapLayout.SIDE_OWN, LAYOUT.tagged_places), LAYOUT.tagged_place(MapLayout.SIDE_OWN, 0), "more than the places share the first")


func test_tagged_places_fit_a_whole_team_without_overlapping() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	assert_gte(LAYOUT.tagged_places, RoomService.MAX_TEAM_SIZE, "a place for every kid of the biggest team")
	for index: int in LAYOUT.tagged_places - 1:
		var gap: float = LAYOUT.tagged_place(MapLayout.SIDE_OWN, index + 1).distance_to(LAYOUT.tagged_place(MapLayout.SIDE_OWN, index))
		assert_gte(gap, rules.player_radius * 2.0, "kids %d and %d don't overlap" % [index, index + 1])


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
	assert_gt(geometry.get_child_count(), 60, "street, cover, 2 bases, 2 rescue areas, houses and props")
	assert_eq(map.base_color(MapLayout.SIDE_OWN), StreetMap.OWN_COLOR)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.ENEMY_COLOR)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		for node: Node in map.get_node("%Geometry").find_children("Outline", "MeshInstance3D", true, false):
			fail_test("no outline round the posts (owner), found one at base %d" % side)
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_eq(map.base_color(MapLayout.SIDE_ENEMY), StreetMap.OWN_COLOR, "your post is always blue")
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var zone: Array[Color] = map.zone_colors(side)
		assert_eq(zone.size(), 4, "base and rescue area are tinted: there is no box inside it any more")
		assert_eq(zone[0], zone[2], "the rescue area has the same tape as the base")
		assert_eq(zone[1], zone[3], "and the same faint fill")
		assert_lt(zone[1].a, 0.5, "the fill stays faint")


func test_each_rescue_area_is_a_gently_rounded_rectangle_against_the_street_edge() -> void:
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var area: Rect2 = LAYOUT.rescue_rect(side)
		var outline: Array[Vector2] = StreetMap.rounded_outline(area, StreetMap.RESCUE_CORNER_RADIUS)
		assert_eq(outline.size(), 4 * (StreetMap.ZONE_CORNER_STEPS + 1))
		for point: Vector2 in outline:
			assert_true(area.grow(0.001).has_point(point), "the outline stays inside the area")
		assert_false(outline.has(area.position), "the corners are cut: the sharp corner is not on the outline")
		assert_false(outline.has(area.end))


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
	var limits: Dictionary = {"bench": 4200, "rooster_tent": 4200,
		"basketball_pole": 3700, "truck_wheels": 4200, "crates": 4200,
		"sacks": 4200, "no_id_no_entry": 4200, "ebike": 4700, "tricycle": 5200,
		"ice_cream_cart": 5200, "nanay": 6200,
		"nena_store": 6200, "vulcanizing": 6200, "barber_shop": 6200, "house_1": 6200, "house_2": 6200, "house_3": 6200, "karinderya": 6200, "cat": 3200, "dog": 3200, "chicken_cage": 4200, "drainage_cover": 2700, "tricycle_terminal": 6200, "plank_fence": 4200}
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
	for id: String in ["bench", "rooster_tent"]:
		var footprint: Vector2 = Vector2(2.6, 0.6)
		var model: Node3D = autofree(PropModel.fit(id, footprint, 1.15)) as Node3D
		var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var box: AABB = mesh.mesh.get_aabb()
		assert_almost_eq(box.size.x * model.scale.x, footprint.x, 0.02, "%s fills the length" % id)
		assert_almost_eq(box.size.z * model.scale.z, footprint.y, 0.02, "%s fills the depth" % id)
		assert_lte(box.size.y * model.scale.y, 1.16, "%s is no taller than the cap" % id)
		assert_gt(box.size.y * model.scale.y, 0.5, "%s is tall enough to hide behind" % id)


func test_the_base_posts_are_the_owners_models_blue_for_you_and_red_for_them_facing_the_camera() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var own: Node3D = map._posts[MapLayout.SIDE_OWN]
	var enemy: Node3D = map._posts[MapLayout.SIDE_ENEMY]
	assert_true((own.get_node(StreetMap.POST_BLUE) as Node3D).visible, "your post is the blue model")
	assert_false((own.get_node(StreetMap.POST_RED) as Node3D).visible)
	assert_true((enemy.get_node(StreetMap.POST_RED) as Node3D).visible, "theirs the red one")
	assert_false((enemy.get_node(StreetMap.POST_BLUE) as Node3D).visible)
	for post: Node3D in [own, enemy]:
		for id: String in [StreetMap.POST_BLUE, StreetMap.POST_RED]:
			var model: Node3D = post.get_node(id) as Node3D
			assert_almost_eq(PropModel.size_of(id).y * model.scale.y, map.post_height(), 0.01, "%s is as tall as the post height in the data" % id)
		assert_almost_eq(post.rotation.y, 0.0, 0.001, "its front (the flyers) faces the camera at the +Z end")
		assert_almost_eq((post.get_node(StreetMap.POST_BLUE) as Node3D).rotation.y, StreetMap.BLUE_POST_TURN, 0.001, "the blue pole has its back to the camera")
		assert_almost_eq((post.get_node(StreetMap.POST_RED) as Node3D).rotation.y, 0.0, 0.001, "the red pole's front faces the camera")
	assert_eq(Vector2(own.position.x, own.position.z), LAYOUT.post_position(MapLayout.SIDE_OWN), "in the street's corner")
	map.set_own_side(MapLayout.SIDE_ENEMY)
	assert_true((enemy.get_node(StreetMap.POST_BLUE) as Node3D).visible, "the other team sees their own post blue")
	assert_true((own.get_node(StreetMap.POST_RED) as Node3D).visible)
	assert_false((own.get_node(StreetMap.POST_BLUE) as Node3D).visible)
	for post: Node3D in [own, enemy]:
		assert_almost_eq(absf(post.rotation.y), PI, 0.001, "the camera is turned half way round for that team, so the posts turn with it")
	assert_false(FileAccess.file_exists("res://assets/models/props/electric_pole.glb"), "the old electric pole model stays gone (owner)")
	assert_false(FileAccess.file_exists("res://assets/textures/posts/post_blue.webp"), "the flat post pictures are replaced by the models (owner)")
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



# ---- the tile grid (Clash Royale's arena was 18 x 32 tiles; ours is 16 x 32) -------------------------------

func test_the_street_is_a_16_by_32_tile_grid_of_1_5_metre_tiles() -> void:
	assert_eq(LAYOUT.tiles, Vector2i(16, 32), "two tiles narrower than Clash Royale's arena")
	assert_almost_eq(LAYOUT.tile_size, 1.5, 0.0001)
	assert_almost_eq(LAYOUT.lane_width, 24.0, 0.0001, "16 tiles across")
	assert_almost_eq(LAYOUT.lane_length, 48.0, 0.0001, "32 tiles long")


func test_tiles_and_metres_convert_both_ways() -> void:
	assert_eq(LAYOUT.tile_center(Vector2i(0, 0)), Vector2(-11.25, -23.25), "the top-left tile")
	assert_eq(LAYOUT.tile_center(Vector2i(15, 31)), Vector2(11.25, 23.25), "the bottom-right tile")
	for tile: Vector2i in [Vector2i(0, 0), Vector2i(7, 15), Vector2i(8, 16), Vector2i(15, 31)]:
		assert_eq(LAYOUT.tile_at(LAYOUT.tile_center(tile)), tile, "round trip %s" % tile)
	assert_eq(LAYOUT.to_tiles(Vector2.ZERO), Vector2(8.0, 16.0), "the middle of the street is the corner of four tiles")
	assert_eq(LAYOUT.lane_rect().size, Vector2(24.0, 48.0))


func test_the_posts_and_boxes_sit_where_the_grid_says() -> void:
	var own: Vector2 = LAYOUT.to_tiles(LAYOUT.base_center(MapLayout.SIDE_OWN))
	assert_almost_eq(own.x, 2.5 / 1.5, 0.0001, "your base's centre: 1 2/3 tiles from the left curb")
	assert_almost_eq(own.y, 32.0 - 2.5 / 1.5, 0.0001, "and 1 2/3 tiles from your end")
	assert_almost_eq(LAYOUT.base_radius / LAYOUT.tile_size, 3.0, 0.0001, "the base circle is 3 tiles")


## The box round a prop model's mesh, in the model's own units.
func _model_box(id: String) -> AABB:
	var model: Node3D = autofree(PropModel.make(id)) as Node3D
	return (model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).get_aabb()


func test_each_post_model_stands_with_its_foot_in_the_corner() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var post: Node3D = map._posts[side]
		assert_eq(Vector2(post.position.x, post.position.z), LAYOUT.post_position(side), "the pole stands in the street's corner")
		assert_almost_eq(post.position.y, 0.0, 0.001, "on the ground")
	for id: String in [StreetMap.POST_BLUE, StreetMap.POST_RED]:
		var box: AABB = _model_box(id)
		assert_almost_eq(box.position.y, 0.0, 0.001, "%s: the model's foot is its origin" % id)
		assert_lt(box.position.x, 0.0, "%s: the pole is inside the model's width, with its arms either side" % id)
		assert_gt(box.end.x, 0.0)
		assert_lt(box.position.z, 0.0)
		assert_gt(box.end.z, 0.0)


func test_the_posts_use_their_baked_shading_and_other_props_do_not() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var post: Node3D = map._posts[MapLayout.SIDE_OWN].get_node(StreetMap.POST_BLUE) as Node3D
	var surfaces: int = 0
	for node: Node in post.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		for surface: int in mesh_instance.mesh.get_surface_count():
			surfaces += 1
			var material: StandardMaterial3D = mesh_instance.get_surface_override_material(surface) as StandardMaterial3D
			assert_true(material.vertex_color_use_as_albedo, "the post's baked shading is on (surface %d)" % surface)
			assert_eq(material.specular_mode, BaseMaterial3D.SPECULAR_DISABLED, "and it stays matte, no gloss")
	assert_gt(surfaces, 10, "every surface of the post was checked")
	var other: Node3D = PropModel.make("bench")
	add_child_autofree(other)
	for node: Node in other.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		var material: StandardMaterial3D = mesh_instance.get_surface_override_material(0) as StandardMaterial3D
		assert_false(material.vertex_color_use_as_albedo, "other props keep the plain matte look")
