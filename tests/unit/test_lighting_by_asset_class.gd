extends GutTest
## Light and colour by asset class (owner): the floor is the owner's picture with no light, tint or grading on it; the zone marks are the
## exact team blue and red; the post pictures are unshaded; only the 3D models take the lights, and they are matte. The Environment
## carries no tonemap curve, saturation or contrast that would bend any of them.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const PROPS: Array[String] = ["bench", "vulcanizing", "tricycle", "nanay", "cat"]


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	return map


func test_the_environment_does_not_grade_the_picture() -> void:
	var env: Environment = StreetMap.make_environment()
	assert_false(env.adjustment_enabled, "no saturation, contrast or brightness over the whole screen")
	assert_eq(env.tonemap_mode, Environment.TONE_MAPPER_LINEAR, "no tonemap curve")
	assert_eq(env.tonemap_exposure, 1.0, "no exposure change")
	assert_false(env.glow_enabled)
	assert_false(env.fog_enabled)


func test_the_floor_is_the_owners_picture_unshaded_and_untinted() -> void:
	var map: StreetMap = _map()
	var floor_patch: MeshInstance3D = map.get_node("%Geometry").find_child("CementArt", true, false) as MeshInstance3D
	assert_not_null(floor_patch, "the floor patch is laid")
	var material: StandardMaterial3D = (floor_patch.mesh as PlaneMesh).material as StandardMaterial3D
	assert_eq(material.shading_mode, BaseMaterial3D.SHADING_MODE_UNSHADED, "no light on the floor")
	assert_eq(material.albedo_color, Color.WHITE, "no tint on the floor")
	assert_eq(material.albedo_texture, StreetArt.texture("cement"), "the owner's picture")
	assert_eq(material.vertex_color_use_as_albedo, false)


func test_the_zone_marks_are_unshaded_in_the_exact_team_colours() -> void:
	var map: StreetMap = _map()
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var want: Color = Palette.TEAM_OWN if side == MapLayout.SIDE_OWN else Palette.TEAM_ENEMY
		var materials: Array = map._base_materials[side]
		assert_eq(materials.size(), 4)
		for index: int in materials.size():
			var material: StandardMaterial3D = materials[index] as StandardMaterial3D
			assert_eq(material.shading_mode, BaseMaterial3D.SHADING_MODE_UNSHADED, "zone material %d is not lit" % index)
			assert_eq(Color(material.albedo_color, 1.0), want, "zone material %d is exactly the palette colour, not softened" % index)
		assert_eq((materials[0] as StandardMaterial3D).albedo_color.a, 1.0, "the line is solid")
		assert_almost_eq((materials[1] as StandardMaterial3D).albedo_color.a, StreetMap.ZONE_FILL_ALPHA, 0.0001, "the fill is the palette colour at its alpha only")
	map.set_own_side(MapLayout.SIDE_ENEMY)
	for material: StandardMaterial3D in map._base_materials[MapLayout.SIDE_ENEMY]:
		assert_eq(Color(material.albedo_color, 1.0), Palette.TEAM_OWN, "still exact after the teams swap ends")


func test_the_post_pictures_are_unshaded_and_not_modulated() -> void:
	var map: StreetMap = _map()
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var post: Sprite3D = map._posts[side] as Sprite3D
		assert_false(post.shaded, "no light on a picture")
		assert_eq(post.modulate, Color.WHITE, "drawn as the owner made it")


func test_every_3d_model_is_matte() -> void:
	for id: String in PROPS:
		var model: Node3D = autofree(PropModel.make(id))
		var seen: int = 0
		for node: Node in model.find_children("*", "MeshInstance3D", true, false):
			var mesh: MeshInstance3D = node as MeshInstance3D
			for surface: int in mesh.mesh.get_surface_count():
				var material: StandardMaterial3D = mesh.get_surface_override_material(surface) as StandardMaterial3D
				assert_not_null(material, "%s surface %d has the model look" % [id, surface])
				if material == null:
					continue
				seen += 1
				assert_eq(material.specular_mode, BaseMaterial3D.SPECULAR_DISABLED, "%s has no specular highlight" % id)
				assert_eq(material.roughness, 1.0, "%s is fully rough" % id)
				assert_eq(material.metallic, 0.0, "%s is not metal" % id)
				assert_false(material.normal_enabled, "%s has no normal map" % id)
				assert_false(material.emission_enabled, "%s glows by nothing" % id)
		assert_gt(seen, 0, "%s has surfaces" % id)


func test_the_lights_leave_a_models_colour_at_about_its_own() -> void:
	assert_eq(ModelLook.ALBEDO_GAIN, 1.0, "no darkening to make up for a bright light")
	var sun: DirectionalLight3D = autofree(DirectionalLight3D.new()) as DirectionalLight3D
	StreetMap._light_scene(sun, sun)
	var up: float = sun.light_energy * sin(deg_to_rad(-sun.rotation_degrees.x)) + StreetMap.AMBIENT_ENERGY
	assert_almost_eq(up, 1.0, 0.1, "an upward face is lit to about 1.0 of its albedo")


func test_the_pole_is_two_to_three_times_a_kid_on_screen() -> void:
	var camera: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	# the post picture faces the camera (its whole height shows); a standing kid shows cos(pitch) of its height
	var kid_on_screen: float = KidModel.KID_HEIGHT * camera.kid_view_scale * cos(deg_to_rad(camera.pitch_degrees))
	var ratio: float = layout.post_height / kid_on_screen
	assert_gt(ratio, 2.0, "taller than a kid, clearly")
	assert_lt(ratio, 3.0, "but proportionate, not a tower over the street")


func test_the_ground_outside_the_play_area_is_slightly_darker_than_the_play_area() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var inside: MeshInstance3D = map.get_node("%Geometry").find_child("CementArt", true, false) as MeshInstance3D
	var outside: MeshInstance3D = map.get_node("%Geometry").find_child("OutsideArt", true, false) as MeshInstance3D
	assert_not_null(outside, "the outside patch is laid")
	assert_eq(((inside.mesh as PlaneMesh).material as StandardMaterial3D).albedo_color, Color.WHITE, "the play area is the picture itself")
	var shade: Color = ((outside.mesh as PlaneMesh).material as StandardMaterial3D).albedo_color
	assert_lt(shade.v, 0.9, "outside is darker")
	assert_gt(shade.v, 0.6, "but only slightly")
	var size: Vector2 = (inside.mesh as PlaneMesh).size
	assert_almost_eq(size.x, map.layout.lane_width, 0.001, "the play area patch is exactly the play area")
	assert_almost_eq(size.y, map.layout.lane_length, 0.001, "end to end")
