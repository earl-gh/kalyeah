extends GutTest
## Every model held to one scale (docs/audits/2026-10-21-model-scale.md): the kids match each other, every prop is drawn at its real size
## times the world factor (see ScaleReference), the buildings are real lots, and thrown things are about the size of what they hit.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
## How far a thrown weapon's drawn size may be from the diameter of its hit circle.
const THROWN_RANGE: Vector2 = Vector2(0.8, 2.8)


func _map() -> StreetMap:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	map.show_overview_camera = false
	add_child(map)
	return map


func _props(map: StreetMap) -> Dictionary[String, Vector3]:
	var found: Dictionary[String, Vector3] = {}
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node == null or not node.scene_file_path.begins_with("res://assets/models/props/"):
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		var drawn: Vector3 = PropModel.size_of(id) * node.scale
		if not found.has(id) or drawn.y > found[id].y:
			found[id] = drawn
	return found


func test_the_world_factor_follows_the_kid() -> void:
	assert_almost_eq(ScaleReference.world_factor(CAMERA.kid_view_scale), KidModel.KID_HEIGHT * CAMERA.kid_view_scale / ScaleReference.REAL_KID, 0.0001)
	assert_gt(ScaleReference.world_factor(CAMERA.kid_view_scale), 1.5)


func test_the_six_kid_models_are_the_same_height() -> void:
	var heights: Array[float] = []
	for character: CharacterDef in RULES.characters:
		var root: Node = (load("res://assets/models/characters/%s.glb" % character.id) as PackedScene).instantiate()
		var mesh: MeshInstance3D = root.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		heights.append(mesh.mesh.get_aabb().size.y)
		root.free()
	assert_lt((heights.max() as float) / (heights.min() as float), 1.02, "one KID_HEIGHT scale gives every kid the same height: %s" % str(heights))


func test_every_prop_is_one_mesh_at_the_origin_so_its_measured_size_is_true() -> void:
	var dir: DirAccess = DirAccess.open("res://assets/models/props")
	for file: String in dir.get_files():
		if not file.ends_with(".glb"):
			continue
		var root: Node = (load("res://assets/models/props/" + file) as PackedScene).instantiate()
		var meshes: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
		assert_eq(meshes.size(), 1, "%s is one mesh (PropModel.size_of reads the first)" % file)
		if not meshes.is_empty():
			assert_true((meshes[0] as Node3D).transform.is_equal_approx(Transform3D.IDENTITY), "%s: its mesh has no offset" % file)
		root.free()


func test_every_prop_is_drawn_at_its_real_size_times_the_world_factor() -> void:
	var factor: float = ScaleReference.world_factor(CAMERA.kid_view_scale)
	var map: StreetMap = _map()
	var props: Dictionary[String, Vector3] = _props(map)
	assert_gt(props.size(), 25, "the street has its props")
	for id: String in props:
		var drawn: Vector3 = props[id]
		if ScaleReference.BUILDINGS.has(id):
			continue
		assert_true(ScaleReference.REAL_HEIGHT.has(id), "%s has a real size in ScaleReference" % id)
		var size: float = drawn.y
		if id == "basketball_pole":
			size = maxf(drawn.x, maxf(drawn.y, drawn.z))  # it lies across the row
		if id == "drainage_cover":
			size = maxf(drawn.x, drawn.z)  # a flat lid: its width
		var ratio: float = size / (ScaleReference.REAL_HEIGHT[id] * factor)
		var range: Vector2 = ScaleReference.allowed(id)
		assert_between(ratio, range.x, range.y, "%s is drawn %.2fx its true proportion to the kids (allowed %.2f to %.2f)" % [id, ratio, range.x, range.y])


func test_the_buildings_are_real_lots_one_to_two_storeys_tall() -> void:
	var factor: float = ScaleReference.world_factor(CAMERA.kid_view_scale)
	var props: Dictionary[String, Vector3] = _props(_map())
	var seen: int = 0
	for id: String in ScaleReference.BUILDINGS:
		if not props.has(id):
			continue
		seen += 1
		var width: float = props[id].x / factor
		var high: float = props[id].y / factor
		assert_almost_eq(width / ScaleReference.REAL_BUILDING_WIDTH, 1.0, ScaleReference.BUILDING_WIDTH_TOLERANCE, "%s is %.1f m wide at true scale" % [id, width])
		assert_between(high, ScaleReference.REAL_BUILDING_HEIGHT.x, ScaleReference.REAL_BUILDING_HEIGHT.y, "%s is %.1f m tall at true scale" % [id, high])
	assert_gt(seen, 6, "the street has its houses and shops")


func test_a_house_is_a_few_kids_tall_not_one() -> void:
	var props: Dictionary[String, Vector3] = _props(_map())
	var kid: float = KidModel.KID_HEIGHT * CAMERA.kid_view_scale
	for id: String in ["house_1", "house_2", "house_3", "nena_store", "vulcanizing", "barber_shop"]:
		assert_between(props[id].y / kid, 2.8, 4.8, "%s is about 3 to 4 kids tall" % id)


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
