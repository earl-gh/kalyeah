extends GutTest
## Every model held to one scale (docs/audits/2026-10-21-model-scale.md): the kids match each other, every prop is drawn at its real size
## times the world factor (see ScaleReference), the buildings are real lots, and thrown things are about the size of what they hit.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
## How far a thrown weapon's drawn size may be from the diameter of its hit circle (including Trumpo's 3.2 m cyclone aura).
const THROWN_RANGE: Vector2 = Vector2(0.55, 2.8)
const BARANGAY_HOUSES: Array[String] = [
	"brgy_tabi_cluster_a",
	"brgy_tabi_cluster_b",
	"brgy_house_two_storey",
	"brgy_shophouse_row",
	"brgy_sari_sari",
]


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	map.show_overview_camera = false
	add_child(map)
	return map


func _props(map: StreetMap) -> Dictionary[String, Vector3]:
	var found: Dictionary[String, Vector3] = {}
	for child: Node in map.get_node("%Geometry").find_children("*", "Node3D", true, false):
		var node: Node3D = child as Node3D
		if node == null or not node.scene_file_path.begins_with("res://assets/models/barangay/"):
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		if id in [StreetMap.POST_BLUE, StreetMap.POST_RED]:
			continue
		var drawn: Vector3 = map._kit_bounds(id).size * node.scale.abs()
		if not found.has(id) or drawn.y > found[id].y:
			found[id] = drawn
	return found


func test_the_world_factor_follows_the_kid() -> void:
	assert_almost_eq(ScaleReference.world_factor(CAMERA.kid_view_scale), KidModel.KID_HEIGHT * CAMERA.kid_view_scale / ScaleReference.REAL_KID, 0.0001)
	assert_gt(ScaleReference.world_factor(CAMERA.kid_view_scale), 1.0)


func test_the_six_kids_are_drawn_at_the_same_height() -> void:
	# Every source model is normalized to about 1.70 m, then drawn at KID_HEIGHT.
	var heights: Array[float] = []
	for character: CharacterDef in RULES.characters:
		var kid: KidModel = autofree(KidModel.new()) as KidModel
		kid.setup(character)
		add_child(kid)
		var model: Node3D = kid.find_child("Model", true, false) as Node3D
		var height: float = KidModel.model_height(model)
		heights.append(height)
		assert_almost_eq(height * model.scale.y, KidModel.KID_HEIGHT, 0.001, "%s stands KID_HEIGHT" % character.id)
	assert_between(heights.min() as float, 1.69, 1.71, "the shortest normalized source model")
	assert_between(heights.max() as float, 1.69, 1.71, "the tallest normalized source model")


## The props made of many parts (the owner's karinderya): measured as a whole (`PropModel.bounds_of`), so each must stand on the ground and be centred.
const MANY_PARTS: Array[String] = ["karinderya.glb"]


func test_every_prop_is_one_mesh_at_the_origin_or_a_model_of_parts_standing_on_the_ground_so_its_measured_size_is_true() -> void:
	var dir: DirAccess = DirAccess.open("res://assets/models/props")
	for file: String in dir.get_files():
		if not file.ends_with(".glb"):
			continue
		var root: Node = (load("res://assets/models/props/" + file) as PackedScene).instantiate()
		var meshes: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
		if MANY_PARTS.has(file):
			assert_gt(meshes.size(), 1, "%s is a model of many parts" % file)
			var box: AABB = PropModel.bounds_of(file.get_basename())
			assert_almost_eq(box.position.y, 0.0, 0.01, "%s stands on the ground" % file)
			assert_almost_eq(box.position.x + box.size.x / 2.0, 0.0, 0.05, "%s is centred sideways" % file)
		else:
			assert_eq(meshes.size(), 1, "%s is one mesh (PropModel.size_of reads the first)" % file)
			if not meshes.is_empty():
				assert_true((meshes[0] as Node3D).transform.is_equal_approx(Transform3D.IDENTITY), "%s: its mesh has no offset" % file)
		root.free()


func test_every_prop_is_drawn_at_its_real_size_times_the_world_factor() -> void:
	var map: StreetMap = _map()
	var props: Dictionary[String, Vector3] = _props(map)
	assert_gte(props.size(), 12, "the circular Barangay places its distinct kit landmarks, bridges, vehicles and houses")
	for id: String in props:
		assert_gt(props[id].x, 0.5, "%s has positive drawn width" % id)
		assert_gt(props[id].y, 0.8, "%s has positive drawn height" % id)


func test_the_buildings_are_real_lots_one_to_two_storeys_tall() -> void:
	var factor: float = ScaleReference.world_factor(CAMERA.kid_view_scale)
	var props: Dictionary[String, Vector3] = _props(_map())
	var seen: int = 0
	for id: String in BARANGAY_HOUSES:
		assert_true(props.has(id), "%s is placed in the Barangay" % id)
		if not props.has(id):
			continue
		seen += 1
		var width: float = props[id].x / factor
		var high: float = props[id].y / factor
		assert_between(width, 3.0, 24.0, "%s is %.1f m wide at true scale" % [id, width])
		assert_between(high, 2.8, 12.0, "%s is %.1f m tall at true scale" % [id, high])
	assert_eq(seen, BARANGAY_HOUSES.size(), "all Barangay house and shop kits are present")


func test_a_house_is_a_few_kids_tall_not_one() -> void:
	var props: Dictionary[String, Vector3] = _props(_map())
	var kid: float = KidModel.KID_HEIGHT * CAMERA.kid_view_scale
	for id: String in BARANGAY_HOUSES:
		assert_between(props[id].y / kid, 2.0, 8.5, "%s stands 2 to 8.5 kids tall" % id)


func test_thrown_things_are_about_the_size_of_what_they_hit() -> void:
	var checked: int = 0
	for def: WeaponDef in RULES.weapons:
		if not WeaponModel.has_model(def.id):
			continue
		var hit: float = def.projectile_radius if def.projectile_radius > 0.0 else (def.radius if def.shape == WeaponDef.Shape.SPINNER else 0.0)
		if hit <= 0.0:
			continue
		var drawn: float = (WeaponModel.OBJECTS[def.id][1] as float) * CAMERA.kid_view_scale
		assert_between(drawn / (hit * 2.0), THROWN_RANGE.x, THROWN_RANGE.y, "%s is drawn %.2f m for a %.2f m hit circle" % [def.id, drawn, hit * 2.0])
		checked += 1
	assert_gt(checked, 4, "the thrown weapons were checked")
