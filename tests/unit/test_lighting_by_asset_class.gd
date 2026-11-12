extends GutTest
## Light and colour by asset class (owner): the floor is the owner's picture with no light, tint or grading on it; the zone marks are the
## exact team blue and red; only the 3D models (the posts too) take the lights, and they are matte. The Environment
## carries no tonemap curve, saturation or contrast that would bend any of them.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const PROPS: Array[String] = ["bench", "vulcanizing", "tricycle", "nanay", "cat", "post_blue", "post_red"]


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	return map


func test_the_environment_does_not_grade_the_picture() -> void:
	var env: Environment = StreetMap.make_environment()
	assert_false(env.adjustment_enabled, "no saturation, contrast or brightness over the whole screen")
	assert_eq(env.tonemap_mode, Environment.TONE_MAPPER_ACES, "golden-hour ACES tonemap")
	assert_almost_eq(env.tonemap_exposure, 1.0, 0.05, "clamped warm sunlit exposure without blowout")
	assert_false(env.glow_enabled)
	assert_true(env.fog_enabled)


func test_the_floor_is_the_owners_picture_unshaded_and_untinted() -> void:
	var map: StreetMap = _map()
	var floor_patch: MeshInstance3D = map.get_node("%Geometry").find_child("BarangayGround", true, false) as MeshInstance3D
	assert_not_null(floor_patch, "the barangay ground plane is laid")
	var material: ShaderMaterial = (floor_patch.mesh as PlaneMesh).material as ShaderMaterial
	assert_not_null(material)
	assert_eq(material.shader, StreetMap.GROUND_SHADER)
	assert_true(material.shader.code.contains("METALLIC = 0.0;"), "matte ground")
	assert_true(material.shader.code.contains("clamp(col, vec3(0.08), vec3(0.65))"), "clamped warm albedo with zero blowout")


func test_the_zone_marks_are_unshaded_chalk_in_the_exact_team_colours() -> void:
	var map: StreetMap = _map()
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var want: Color = Palette.TEAM_OWN if side == MapLayout.SIDE_OWN else Palette.TEAM_ENEMY
		var materials: Array = map._base_materials[side]
		assert_eq(materials.size(), 2, "a base and a rescue area: chalk only, no fill")
		for index: int in materials.size():
			var material: ShaderMaterial = materials[index] as ShaderMaterial
			assert_true(material.shader.code.contains("unshaded"), "zone chalk %d is not lit" % index)
			assert_eq(Color(material.get_shader_parameter("chalk") as Color, 1.0), want, "zone chalk %d is exactly the palette colour, not softened" % index)
		assert_almost_eq(((materials[0] as ShaderMaterial).get_shader_parameter("chalk") as Color).a, StreetMap.CHALK_ALPHA, 0.0001, "the chalk is the palette colour at its strength only")
	map.set_own_side(MapLayout.SIDE_ENEMY)
	for material: ShaderMaterial in map._base_materials[MapLayout.SIDE_ENEMY]:
		assert_eq(Color(material.get_shader_parameter("chalk") as Color, 1.0), Palette.TEAM_OWN, "still exact after the teams swap ends")


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
				if PropModel.POLISHED.has(id):
					# the buildings' polished look (docs/ART.md): toon light, smoother, a slight gloss
					assert_eq(material.specular_mode, BaseMaterial3D.SPECULAR_TOON, "%s has its slight toon gloss" % id)
					assert_almost_eq(material.roughness, ModelLook.POLISHED_ROUGHNESS, 0.0001, id)
				else:
					assert_eq(material.specular_mode, BaseMaterial3D.SPECULAR_DISABLED, "%s has no specular highlight" % id)
					assert_eq(material.roughness, 1.0, "%s is fully rough" % id)
				assert_eq(material.metallic, 0.0, "%s is not metal" % id)
				assert_false(material.normal_enabled, "%s has no normal map" % id)
				assert_false(material.emission_enabled, "%s glows by nothing" % id)
				assert_eq(material.texture_filter, BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC, "%s keeps its textures sharp at an angle" % id)
		assert_gt(seen, 0, "%s has surfaces" % id)


func test_the_lights_leave_a_models_colour_at_about_its_own() -> void:
	assert_eq(ModelLook.ALBEDO_GAIN, 1.0, "no darkening to make up for a bright light")
	var sun: DirectionalLight3D = autofree(DirectionalLight3D.new()) as DirectionalLight3D
	StreetMap._light_scene(sun, sun)
	var up: float = sun.light_energy * sin(deg_to_rad(-sun.rotation_degrees.x)) + StreetMap.AMBIENT_ENERGY
	assert_almost_eq(up, 1.0, 0.1, "an upward face is lit to about 1.0 of its albedo")


func test_the_pole_is_about_four_times_a_kid_on_screen() -> void:
	var camera: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	# the post is a standing model like the kids: the tilt shortens both by the same cos(pitch), so only their drawn heights count
	var kid_drawn: float = KidModel.KID_HEIGHT * camera.kid_view_scale
	var ratio: float = layout.post_height / kid_drawn
	# owner, 2026-11-09: a more realistic, longer pole (a real one is about 5 times a child); it was 2 to 3 times
	assert_gt(ratio, 3.5, "a real electric pole's height over the kids")
	assert_lt(ratio, 9.0, "realistic provincial Meralco pole height (~7.8x Deadlock-scale kid)")


func test_the_ground_outside_the_play_area_is_the_floor_picture_with_no_dark_tint() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var ground: MeshInstance3D = map.get_node("%Geometry").find_child("BarangayGround", true, false) as MeshInstance3D
	assert_not_null(ground, "the seamless barangay ground plane covers the arena and horizon")
	var material: ShaderMaterial = (ground.mesh as PlaneMesh).material as ShaderMaterial
	assert_eq(material.shader, StreetMap.GROUND_SHADER)
	assert_almost_eq(material.get_shader_parameter("arena_radius") as float, map.layout.arena_radius, 0.001)
	var size: Vector3 = ground.mesh.get_aabb().size
	assert_almost_eq(size.x, map.layout.lane_width + StreetMap.OUTER_GROUND_MARGIN * 2.0, 0.01, "extends cleanly past the circular arena")
	assert_almost_eq(size.z, map.layout.lane_length + StreetMap.OUTER_GROUND_MARGIN * 2.0, 0.01, "end to end")


func test_the_pole_meshes_are_not_simplified_when_small_on_screen() -> void:
	for id: String in ["post_blue", "post_red"]:
		var config: ConfigFile = ConfigFile.new()
		assert_eq(config.load("res://assets/models/props/%s.glb.import" % id), OK)
		assert_false(config.get_value("params", "meshes/generate_lods", true), "%s keeps its full mesh" % id)
	assert_true(ResourceLoader.exists("res://assets/models/barangay/brgy_post_blue.glb"))
	assert_true(ResourceLoader.exists("res://assets/models/barangay/brgy_post_red.glb"))
