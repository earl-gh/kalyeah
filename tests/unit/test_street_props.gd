extends GutTest
## Circular Barangay 3D prop kit (`assets/models/barangay/*.glb`), Estero Creek + 3 tight bridges,
## single-draw-call Chaotic Spaghetti Wiring & Overhead Sampayan micro-dressing, diorama plinth mode,
## and deletion guardrails for removed legacy street files.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const BARANGAY_PROPS: Array[String] = [
	"brgy_jeepney",
	"brgy_tricycle",
	"brgy_sorbetes_cart",
	"brgy_poso_well",
	"brgy_footbridge",
	"brgy_meralco_post",
	"brgy_banderitas",
	"brgy_cover_wall",
	"brgy_post_blue",
	"brgy_post_red",
]


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	return map


func test_every_barangay_prop_model_exists_light_and_single_surface() -> void:
	for id: String in BARANGAY_PROPS:
		var path: String = StreetMap.BARANGAY_DIR % id
		assert_true(ResourceLoader.exists(path), "%s exists in barangay kit" % id)
		var scene: PackedScene = load(path) as PackedScene
		assert_not_null(scene)
		var root: Node3D = autofree(scene.instantiate()) as Node3D
		var meshes: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
		if root is MeshInstance3D:
			meshes.append(root)
		assert_eq(meshes.size(), 1, "%s: single MeshInstance3D" % id)
		var mesh: Mesh = (meshes[0] as MeshInstance3D).mesh
		assert_eq(mesh.get_surface_count(), 1, "%s: single draw-call surface" % id)
		var indices: PackedInt32Array = mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX] as PackedInt32Array
		var tris: int = indices.size() / 3
		assert_between(tris, 40, 6500, "%s stays light for mobile (%d tris)" % [id, tris])


func test_removed_legacy_bloated_models_stay_deleted() -> void:
	for id: String in ["ebike", "truck_wheels", "roof_wall", "roof_wall_2"]:
		for extension: String in [".glb", ".glb.import", "_prop_base_color.jpg", "_prop_base_color.jpg.import"]:
			assert_false(FileAccess.file_exists("res://assets/models/props/%s%s" % [id, extension]), "%s%s stays deleted" % [id, extension])
		assert_false(StreetMap.BARRIER_HEIGHTS.has(id))
		assert_false(ScaleReference.REAL_HEIGHT.has(id))
	for gone_prop: String in ["jeep", "pedicab", "fishball_cart", "ironing_board", "sewing_machine", "school_zone", "electric_pole"]:
		assert_false(FileAccess.file_exists(PropModel.PATH % gone_prop), "%s.glb stays deleted" % gone_prop)


func test_circular_perimeter_skyline_surrounds_arena_outside_play_circle() -> void:
	var map: StreetMap = _map()
	assert_gte(map.far_props.size(), 70, "unbroken 360-degree inner + outer tabi-tabing bahay rings")
	for side_key: int in [-1, 1]:
		var row_nodes: Array = map.house_rows[side_key]
		assert_gt(row_nodes.size(), 20, "perimeter houses populate side %d" % side_key)
		for item: Variant in row_nodes:
			var house: Node3D = item as Node3D
			var radial_dist: float = Vector2(house.position.x, house.position.z).length()
			assert_gt(radial_dist, map.layout.arena_radius, "perimeter skyline house stands outside playable r = 84m arena")


func test_estero_creek_bridges_spaghetti_wiring_and_sampayan_are_batched() -> void:
	var map: StreetMap = _map()
	var geometry: Node3D = map.get_node("%Geometry") as Node3D
	var water_nodes: Array[Node] = geometry.find_children("EsteroWater*", "MeshInstance3D", false, false)
	assert_eq(water_nodes.size(), map.layout.water_rects().size(), "Estero water plane for each creek channel segment")
	for wn: Node in water_nodes:
		var w_mi: MeshInstance3D = wn as MeshInstance3D
		assert_eq(((w_mi.mesh as PlaneMesh).material as ShaderMaterial).shader, StreetMap.WATER_SHADER)

	var wiring: MeshInstance3D = geometry.get_node_or_null("SpaghettiWiring") as MeshInstance3D
	assert_not_null(wiring, "single batched SpaghettiWiring ArrayMesh exists")
	assert_eq(wiring.mesh.get_surface_count(), 1, "entire overhead spaghetti wire web is 1 draw call")
	assert_eq(wiring.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF)
	var wire_verts: int = wiring.mesh.surface_get_array_len(0)
	assert_gt(wire_verts, 6000, "dense 2.5x crisscrossing eskinita wiring + slack coils")

	var dressing: MeshInstance3D = geometry.get_node_or_null("BarangayMicroDressing") as MeshInstance3D
	assert_not_null(dressing, "single batched BarangayMicroDressing ArrayMesh exists")
	assert_eq(dressing.mesh.get_surface_count(), 1, "curb + sampayan T-shirts/shorts/sandos + tropical trees in 1 draw call")


func test_diorama_mode_builds_clean_plinth_without_perimeter_or_wiring() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	map.layout = load("res://data/rules/map_layout.tres") as MapLayout
	map.diorama = true
	map.show_overview_camera = false
	add_child(map)
	assert_eq(map.far_props.size(), 0, "no perimeter skyline in carousel diorama")
	assert_null(map.find_child("SpaghettiWiring", true, false), "no overhead wire web in carousel diorama")
	assert_null(map.find_child("EsteroWater", true, false), "no creek segments in carousel diorama")
	assert_not_null(map.find_child("BarangayGround", true, false), "ground slab present in diorama")


func test_hide_near_houses_toggles_side_rows_cleanly() -> void:
	var map: StreetMap = _map()
	map.hide_near_houses(-1)
	for item_neg: Variant in map.house_rows[-1]:
		assert_false((item_neg as Node3D).visible)
	for item_pos: Variant in map.house_rows[1]:
		assert_true((item_pos as Node3D).visible)
	map.hide_near_houses(0)
	for item_all: Variant in map.house_rows[-1]:
		assert_true((item_all as Node3D).visible)
