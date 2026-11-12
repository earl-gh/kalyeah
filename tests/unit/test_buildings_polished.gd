extends GutTest
## Cellular-detail Provincial Barangay architectural buildings (`assets/models/barangay/*.glb` + shared
## `BarangayAtlas.jpg`) and Valorant x Arcane x 90s Komiks ink outlines in `StreetMap`.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const ATLAS_PATH: String = "res://assets/models/barangay/BarangayAtlas.jpg"
const BARANGAY_BUILDINGS: Array[String] = [
	"brgy_tabi_cluster_a",
	"brgy_tabi_cluster_b",
	"brgy_house_two_storey",
	"brgy_shophouse_row",
	"brgy_sari_sari",
	"brgy_covered_court",
	"brgy_hall_plaza",
	"brgy_waiting_shed",
	"brgy_palengke_stall",
]


func test_every_building_on_the_street_uses_the_barangay_kit_and_shared_atlas() -> void:
	assert_true(ResourceLoader.exists(ATLAS_PATH), "shared BarangayAtlas.jpg exists")
	var atlas_tex: Texture2D = load(ATLAS_PATH) as Texture2D
	assert_not_null(atlas_tex)
	assert_eq(atlas_tex.get_size(), Vector2(2048.0, 2048.0), "2048x2048 shared painterly barangay atlas")
	var import_text: String = FileAccess.get_file_as_string(ATLAS_PATH + ".import")
	assert_true(import_text.contains("compress/mode=2"), "BarangayAtlas.jpg is VRAM-compressed")

	var configured: Array[String] = []
	configured.append_array(StreetMap.FILLER_HOUSES)
	configured.append(StreetMap.TERMINAL)
	for id: String in configured:
		assert_true(BARANGAY_BUILDINGS.has(id), "%s is part of the barangay architectural kit" % id)
		assert_true(ResourceLoader.exists(StreetMap.BARANGAY_DIR % id), "%s.glb exists" % id)


func test_each_keeps_its_shape_its_triangles_and_one_material() -> void:
	for id: String in BARANGAY_BUILDINGS:
		var path: String = StreetMap.BARANGAY_DIR % id
		var scene: PackedScene = load(path) as PackedScene
		assert_not_null(scene, "%s loads as PackedScene" % id)
		var model: Node3D = autofree(scene.instantiate()) as Node3D
		var meshes: Array[Node] = model.find_children("*", "MeshInstance3D", true, false)
		if model is MeshInstance3D:
			meshes.append(model)
		assert_eq(meshes.size(), 1, "%s: single combined MeshInstance3D" % id)
		var mi: MeshInstance3D = meshes[0] as MeshInstance3D
		var mesh: Mesh = mi.mesh
		assert_eq(mesh.get_surface_count(), 1, "%s: single draw-call material surface" % id)
		var box: AABB = mi.transform * mesh.get_aabb()
		assert_between(box.position.y, -0.35, 0.06, "%s: grounded plinth sits on y = 0" % id)
		assert_lt(absf(box.get_center().x), 1.5, "%s: horizontally centered" % id)
		var arrays: Array = mesh.surface_get_arrays(0)
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] as PackedInt32Array
		var triangles: int = indices.size() / 3
		assert_between(triangles, 80, 18000, "%s: cellular 3D silhouette within mobile triangle budget (%d tris)" % [id, triangles])


func test_each_is_drawn_with_deadlock_pbr_shading_without_comic_hull_outlines() -> void:
	var saved_perf: StringName = Settings.performance
	Settings.performance = &"standard"
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for landmark_name: String in ["CoveredBasketballCourt", "BarangayHall"]:
		var node: Node3D = map.find_child(landmark_name, true, false) as Node3D
		assert_not_null(node, "%s landmark placed in StreetMap" % landmark_name)
		var meshes: Array[Node] = node.find_children("*", "MeshInstance3D", true, false)
		assert_gt(meshes.size(), 0)
		var mi: MeshInstance3D = meshes[0] as MeshInstance3D
		var over: StandardMaterial3D = mi.get_surface_override_material(0) as StandardMaterial3D
		assert_not_null(over, "%s receives PBR surface override material" % landmark_name)
		var base_mat: BaseMaterial3D = mi.mesh.surface_get_material(0) as BaseMaterial3D
		assert_same(over.albedo_texture, base_mat.albedo_texture, "%s preserves shared BarangayAtlas texture" % landmark_name)
		assert_true(over.normal_enabled, "%s enables normal map relief" % landmark_name)
		assert_not_null(over.normal_texture, "%s binds shared PBR normal map" % landmark_name)
		assert_not_null(over.roughness_texture, "%s binds shared ORM roughness channel" % landmark_name)
		assert_true(over.ao_enabled, "%s enables ORM ambient occlusion" % landmark_name)
		assert_null(over.next_pass, "%s omits comic black hull outline next_pass for Deadlock architectural readability" % landmark_name)
	Settings.performance = saved_perf


func test_overhaul_v3_pbr_atlases_and_new_barangay_kits_have_vertex_ao_and_vram_compression() -> void:
	var pbr_textures: Array[String] = [
		"res://assets/models/barangay/BarangayAtlas.jpg",
		"res://assets/models/barangay/BarangayAtlas_normal.png",
		"res://assets/models/barangay/BarangayAtlas_orm.png",
		"res://assets/models/barangay/ProvincePropsAtlas.jpg",
		"res://assets/models/barangay/ProvincePropsAtlas_normal.png",
		"res://assets/models/barangay/ProvincePropsAtlas_orm.png",
	]
	for tex_path: String in pbr_textures:
		assert_true(ResourceLoader.exists(tex_path), "%s exists" % tex_path)
		var tex: Texture2D = load(tex_path) as Texture2D
		assert_not_null(tex)
		assert_eq(tex.get_size(), Vector2(2048.0, 2048.0), "%s is 2048x2048" % tex_path)
		var imp: String = FileAccess.get_file_as_string(tex_path + ".import")
		assert_true(imp.contains("compress/mode=2"), "%s uses VRAM compression" % tex_path)

	var v3_kits: Array[String] = [
		"brgy_kapilya",
		"brgy_bakery_oven",
		"brgy_ilog_steps",
		"brgy_laundry_stones",
		"brgy_merienda_stand",
		"brgy_owner_jeep",
		"brgy_passenger_jeepney",
		"brgy_water_drum",
	]
	for kit_id: String in v3_kits:
		var kit_path: String = StreetMap.BARANGAY_DIR % kit_id
		assert_true(ResourceLoader.exists(kit_path), "%s exists" % kit_path)
		var scene: PackedScene = load(kit_path) as PackedScene
		assert_not_null(scene)
		var inst: Node3D = autofree(scene.instantiate()) as Node3D
		var meshes: Array[Node] = inst.find_children("*", "MeshInstance3D", true, false)
		if inst is MeshInstance3D:
			meshes.append(inst)
		assert_eq(meshes.size(), 1, "%s single combined MeshInstance3D" % kit_id)
		var mi: MeshInstance3D = meshes[0] as MeshInstance3D
		var arrays: Array = mi.mesh.surface_get_arrays(0)
		var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] as PackedColorArray
		assert_gt(colors.size(), 0, "%s carries baked vertex AO in COLOR_0" % kit_id)
