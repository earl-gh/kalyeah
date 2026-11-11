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


func test_each_is_drawn_with_arcane_ink_outline_over_the_shared_atlas() -> void:
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
		assert_not_null(over, "%s receives outlined override material" % landmark_name)
		var base_mat: BaseMaterial3D = mi.mesh.surface_get_material(0) as BaseMaterial3D
		assert_same(over.albedo_texture, base_mat.albedo_texture, "%s preserves shared BarangayAtlas texture" % landmark_name)
		assert_not_null(over.next_pass, "%s carries 2D Arcane ink outline next_pass" % landmark_name)
		var ink_mat: ShaderMaterial = over.next_pass as ShaderMaterial
		assert_not_null(ink_mat)
		assert_eq(ink_mat.shader.resource_path, StreetMap.INK_OUTLINE_PATH)
	Settings.performance = saved_perf
