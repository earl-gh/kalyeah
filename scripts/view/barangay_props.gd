class_name BarangayProps
extends RefCounted
## Registry and loader for handcrafted 90s Philippine Provincial Barangay 3D kit models
## (`res://assets/models/barangay/brgy_*.glb`). Pure view helper: holds no simulation rules.

const BARANGAY_DIR: String = "res://assets/models/barangay/%s.glb"
const PROVINCE_ATLAS_PATH: String = "res://assets/models/barangay/ProvincePropsAtlas.jpg"
const BARANGAY_ATLAS_PATH: String = "res://assets/models/barangay/BarangayAtlas.jpg"
const INK_OUTLINE_PATH: String = "res://shaders/arcane_ink_outline.gdshader"

## All 10 Overhaul v2 90s Philippine Province props (vehicles, terminal, kalye hoop, mossy CHB wall,
## realistic provincial trees, true 3D volumetric sampayan clothes, and street power-up stand).
const PROVINCE_90S_KEYS: Array[StringName] = [
	&"jeepney",
	&"tricycle",
	&"toda_shed",
	&"wooden_hoop",
	&"chb_wall",
	&"mango_tree",
	&"banana_clump",
	&"coconut_palm",
	&"sampayan_3d",
	&"powerup_stand",
]

## Maps gameplay/landmark `StringName` prop IDs to `assets/models/barangay/<kit>.glb` basenames.
const PROP_KIT_MAP: Dictionary[StringName, String] = {
	# Overhaul v2 90s Province Vehicles, Trees, Walls, Volumetric Sampayan & Power-Up Stand
	&"jeepney": "brgy_jeepney",
	&"tricycle": "brgy_tricycle",
	&"toda_shed": "brgy_toda_shed",
	&"wooden_hoop": "brgy_wooden_hoop",
	&"chb_wall": "brgy_chb_wall",
	&"mango_tree": "brgy_mango_tree",
	&"banana_clump": "brgy_banana_clump",
	&"coconut_palm": "brgy_coconut_palm",
	&"sampayan_3d": "brgy_sampayan_3d",
	&"powerup_stand": "brgy_powerup_stand",
	# Existing Barangay Architectural & Street Landmarks
	&"tabi_cluster_a": "brgy_tabi_cluster_a",
	&"tabi_cluster_b": "brgy_tabi_cluster_b",
	&"poso_well": "brgy_poso_well",
	&"sari_sari": "brgy_sari_sari",
	&"house_two_storey": "brgy_house_two_storey",
	&"shophouse_row": "brgy_shophouse_row",
	&"palengke_stall": "brgy_palengke_stall",
	&"waiting_shed": "brgy_waiting_shed",
	&"covered_court": "brgy_covered_court",
	&"hall_plaza": "brgy_hall_plaza",
	&"barangay_hall": "brgy_hall_plaza",
	&"footbridge": "brgy_footbridge",
	&"estero_bridge": "brgy_footbridge",
	&"meralco_post": "brgy_meralco_post",
	&"electric_post": "brgy_meralco_post",
	&"poster_cluster": "brgy_poster_cluster",
	&"post_blue": "brgy_post_blue",
	&"post_red": "brgy_post_red",
	&"sorbetes_cart": "brgy_sorbetes_cart",
	&"banderitas": "brgy_banderitas",
	&"cover_wall": "brgy_cover_wall",
	&"bench": "brgy_cover_wall",
	&"tent": "brgy_palengke_stall",
}

## Graceful fallback chain if an optional `.glb` has not yet been imported.
const KIT_FALLBACK_MAP: Dictionary[String, String] = {
	"brgy_toda_shed": "brgy_waiting_shed",
	"brgy_chb_wall": "brgy_cover_wall",
	"brgy_powerup_stand": "brgy_sorbetes_cart",
	"brgy_tabi_cluster_a": "brgy_shophouse_row",
	"brgy_tabi_cluster_b": "brgy_house_two_storey",
	"brgy_poso_well": "brgy_cover_wall",
}

static var _scene_cache: Dictionary[String, PackedScene] = {}
static var _bounds_cache: Dictionary[String, AABB] = {}
static var _ink_outline_mat: Material = null
static var _outlined_mat_cache: Dictionary[int, Material] = {}


static func has_prop(key: StringName) -> bool:
	return PROP_KIT_MAP.has(key)


static func all_keys() -> Array[StringName]:
	var keys: Array[StringName] = []
	for key: StringName in PROP_KIT_MAP:
		keys.append(key)
	return keys


static func kit_id_for(key: StringName) -> String:
	if PROP_KIT_MAP.has(key):
		return PROP_KIT_MAP[key]
	var raw: String = String(key)
	if raw.begins_with("brgy_"):
		return raw
	return "brgy_" + raw


static func resolve_path(key: StringName) -> String:
	return resolve_kit_path(kit_id_for(key))


static func resolve_kit_path(kit_id: String) -> String:
	var primary: String = BARANGAY_DIR % kit_id
	if ResourceLoader.exists(primary):
		return primary
	if KIT_FALLBACK_MAP.has(kit_id):
		var fallback: String = BARANGAY_DIR % KIT_FALLBACK_MAP[kit_id]
		if ResourceLoader.exists(fallback):
			return fallback
	var cover: String = BARANGAY_DIR % "brgy_cover_wall"
	return cover if ResourceLoader.exists(cover) else ""


static func make(key: StringName, scale_vec: Vector3 = Vector3.ONE, with_outline: bool = true) -> Node3D:
	return make_kit(kit_id_for(key), scale_vec, with_outline)


static func make_kit(kit_id: String, scale_vec: Vector3 = Vector3.ONE, with_outline: bool = true) -> Node3D:
	if not _scene_cache.has(kit_id):
		var path: String = resolve_kit_path(kit_id)
		if path != "":
			var loaded: PackedScene = load(path) as PackedScene
			if loaded != null:
				_scene_cache[kit_id] = loaded
	if _scene_cache.has(kit_id):
		var scene: PackedScene = _scene_cache[kit_id]
		var inst: Node3D = scene.instantiate() as Node3D
		if with_outline:
			apply_ink_outlines(inst)
		inst.scale = scale_vec
		return inst
	var fallback_root: Node3D = Node3D.new()
	var mi: MeshInstance3D = MeshInstance3D.new()
	var box: BoxMesh = BoxMesh.new()
	box.size = Vector3(2.0, 1.8, 2.0)
	var mat: StandardMaterial3D = StandardMaterial3D.new()
	mat.albedo_color = Color(0.52, 0.44, 0.34)
	mat.roughness = 0.88
	box.material = mat
	mi.mesh = box
	mi.position.y = 0.9
	fallback_root.add_child(mi)
	fallback_root.scale = scale_vec
	return fallback_root


static func bounds_of(key: StringName) -> AABB:
	return kit_bounds(kit_id_for(key))


static func kit_bounds(kit_id: String) -> AABB:
	if not _bounds_cache.has(kit_id):
		var probe: Node3D = make_kit(kit_id, Vector3.ONE, false)
		var box: AABB = AABB()
		var first: bool = true
		var meshes: Array[Node] = probe.find_children("*", "MeshInstance3D", true, false)
		if probe is MeshInstance3D:
			meshes.append(probe)
		for node: Node in meshes:
			var mi: MeshInstance3D = node as MeshInstance3D
			if mi.mesh == null:
				continue
			var placed: Transform3D = Transform3D.IDENTITY
			var cur: Node = mi
			while cur != null and cur != probe:
				if cur is Node3D:
					placed = (cur as Node3D).transform * placed
				cur = cur.get_parent()
			var part: AABB = placed * mi.mesh.get_aabb()
			box = part if first else box.merge(part)
			first = false
		probe.free()
		_bounds_cache[kit_id] = box
	return _bounds_cache[kit_id]


static func size_of(key: StringName) -> Vector3:
	return bounds_of(key).size


static func by_height(key: StringName, target_height: float, with_outline: bool = true) -> Node3D:
	var nat: Vector3 = size_of(key)
	var factor: float = target_height / maxf(nat.y, 0.05)
	return make(key, Vector3.ONE * factor, with_outline)


static func fit(key: StringName, footprint: Vector2, max_height: float, exact: bool = false, with_outline: bool = true) -> Node3D:
	var nat: Vector3 = size_of(key)
	var sx: float = footprint.x / maxf(nat.x, 0.05)
	var sz: float = footprint.y / maxf(nat.z, 0.05)
	var h_cap: float = max_height / maxf(nat.y, 0.05)
	var sy: float = h_cap if exact else minf(sqrt(sx * sz), h_cap)
	return make(key, Vector3(sx, sy, sz), with_outline)


static func outline_material() -> Material:
	if Settings.performance == &"turbo":
		return null
	if _ink_outline_mat == null:
		if ResourceLoader.exists(INK_OUTLINE_PATH):
			var shader: Shader = load(INK_OUTLINE_PATH) as Shader
			if shader != null:
				var smat: ShaderMaterial = ShaderMaterial.new()
				smat.shader = shader
				smat.set_shader_parameter("ink_width", 0.008)
				smat.set_shader_parameter("distance_comp", 0.52)
				smat.set_shader_parameter("max_distance", 148.0)
				_ink_outline_mat = smat
		if _ink_outline_mat == null:
			_ink_outline_mat = LowPoly.outline_material()
	return _ink_outline_mat


static func apply_ink_outlines(root: Node3D) -> void:
	var outline: Material = outline_material()
	if outline == null:
		return
	var meshes: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
	if root is MeshInstance3D:
		meshes.append(root)
	for node: Node in meshes:
		var mi: MeshInstance3D = node as MeshInstance3D
		if mi.mesh == null:
			continue
		var surf_count: int = mi.mesh.get_surface_count()
		for s_idx: int in surf_count:
			var base_mat: Material = mi.get_active_material(s_idx)
			if base_mat == null or base_mat is ShaderMaterial:
				continue
			var std: StandardMaterial3D = base_mat as StandardMaterial3D
			if std != null and std.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
				continue
			var key: int = base_mat.get_instance_id()
			if not _outlined_mat_cache.has(key):
				var copy_mat: Material = base_mat.duplicate() as Material
				copy_mat.next_pass = outline
				_outlined_mat_cache[key] = copy_mat
			mi.set_surface_override_material(s_idx, _outlined_mat_cache[key])
