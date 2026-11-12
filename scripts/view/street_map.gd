class_name StreetMap
extends Node3D
## Circular 90s Philippine Provincial Barangay 3D map (view only; reads MapLayout, holds no sim rules).
## Features 3 organic S-curved North-South pathways, 2 meandering East-West eskinita alleyways,
## a sunken walkable *Tabing-Ilog* creek (`Y = -0.30 m` bed, `-0.12 m` olive-jade water, stone-cobbled
## shallow causeway fords, descending river steps & washing stones), 16+16 true-scale (`0.95..1.02x`)
## rotated shophouse/house parcels on 3D raised concrete curb pads, cohesive TODA terminal + talyer +
## talipapa bays, 4 dry bridgehead Merienda stands, 3D soil tree planters, Deadlock painterly daylight
## without comic hull outlines on architecture, and 100% two-ended anchored `SpaghettiWires`.

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY

const GROUND_SHADER: Shader = preload("res://shaders/barangay_ground.gdshader")
const STREET_SHADER: Shader = GROUND_SHADER
const WATER_SHADER: Shader = preload("res://shaders/estero_water.gdshader")
const SKY_SHADER: Shader = preload("res://shaders/tropical_sky.gdshader")
const CHALK_SHADER: Shader = preload("res://shaders/chalk_line.gdshader")
const INK_OUTLINE_PATH: String = "res://shaders/arcane_ink_outline.gdshader"

const BARANGAY_DIR: String = "res://assets/models/barangay/%s.glb"
const BARANGAY_NORMAL_PATH: String = "res://assets/models/barangay/BarangayAtlas_normal.png"
const PROVINCE_NORMAL_PATH: String = "res://assets/models/barangay/ProvincePropsAtlas_normal.png"

## Deadlock-inspired golden-afternoon barangay lighting constants (delegated to ArenaEnvironment).
const SUN_ANGLES: Vector3 = ArenaEnvironment.SUN_ANGLES
const SUN_COLOR: Color = ArenaEnvironment.SUN_COLOR
const SUN_ENERGY: float = ArenaEnvironment.SUN_ENERGY
const SHADOW_DISTANCE: float = ArenaEnvironment.SHADOW_DISTANCE
const FILL_ANGLES: Vector3 = ArenaEnvironment.FILL_ANGLES
const FILL_COLOR: Color = ArenaEnvironment.FILL_COLOR
const FILL_ENERGY: float = ArenaEnvironment.FILL_ENERGY
const AMBIENT_COLOR: Color = ArenaEnvironment.AMBIENT_COLOR
const AMBIENT_ENERGY: float = ArenaEnvironment.AMBIENT_ENERGY

const SIDEWALK_WIDTH: float = 2.4
const CURB_HEIGHT: float = 0.0
const BARRIER_SETBACK: float = 0.5
const BOUNDARY_TILT: float = 15.0
const FOG_MARGIN: float = 4.0
const HOUSE_SPACING: float = 16.0
const TERMINAL: String = "brgy_waiting_shed"
const NANAY_HEIGHT: float = 1.65
const NANAY_SWAY_DEGREES: float = 4.0
const BOUNDARY_PATH: String = "res://assets/ui/boundary/%s.png"
const BOTTOM_WALL_SHARE: float = 0.5
const BOTTOM_PICTURE_SHARE: float = 0.5
const CORNER_WALL: Dictionary = {"id": "tin_fence", "height": 2.0}
const BARRIER_HEIGHTS: Dictionary[String, float] = {}
const BOTTOM_PICTURES: Array[Dictionary] = []
const BOUNDARY_PICTURES: Array[Dictionary] = []
const BUILDINGS: Array[String] = []
const FILLER_HOUSES: Array[String] = ["brgy_tabi_cluster_a", "brgy_tabi_cluster_b", "brgy_house_two_storey", "brgy_shophouse_row"]

const RING_HEIGHT: float = 0.04
const ZONE_CHALK_WIDTH: float = 0.32
const ZONE_CHALK_BREAKS: float = 0.3
const CHALK_ALPHA: float = 0.96
const RESCUE_WHITE_ALPHA: float = 0.78
const RESCUE_COUNT_HEIGHT: float = 2.2
const RESCUE_COUNT_FONT: int = 96
const RESCUE_COUNT_LIFT: float = RING_HEIGHT + 0.03
const COUNT_ALPHA: float = 0.72
const RESCUE_COUNT_PRIORITY: int = -98
const RESCUE_OUTLINE_PRIORITY: int = -99
const COUNT_PITCH_DEGREES: float = 45.0
const COUNT_SPIN: float = PI / 2.0
const RESCUE_CORNER_RADIUS: float = 1.2
const ZONE_CORNER_STEPS: int = 6

const PLINTH: Color = Color(0.34, 0.37, 0.46)
const PLINTH_HEIGHT: float = 5.0
const PLATFORM: Color = Color(0.08, 0.16, 0.36)
const PLATFORM_HEIGHT: float = 7.0
const PLATFORM_MARGIN: float = 4.0
const CORNER_RADIUS: float = 3.0

const COVER_HEIGHT: float = 1.35
const POST_HEIGHT: float = 10.5
const POST_BLUE: String = "brgy_post_blue"
const POST_RED: String = "brgy_post_red"
const BLUE_POST_TURN: float = PI
const OUTER_GROUND_MARGIN: float = 1200.0

const OBSTACLE_KIT_MAP: Dictionary[StringName, String] = {
	&"tabi_cluster_a": "brgy_tabi_cluster_a",
	&"tabi_cluster_b": "brgy_tabi_cluster_b",
	&"poso_well": "brgy_poso_well",
	&"sari_sari": "brgy_sari_sari",
	&"bakery_oven": "brgy_bakery_oven",
	&"kapilya": "brgy_kapilya",
	&"house_two_storey": "brgy_house_two_storey",
	&"shophouse_row": "brgy_shophouse_row",
	&"palengke_stall": "brgy_palengke_stall",
	&"waiting_shed": "brgy_waiting_shed",
	&"toda_shed": "brgy_toda_shed",
	&"jeepney": "brgy_jeepney",
	&"owner_jeep": "brgy_owner_jeep",
	&"passenger_jeepney": "brgy_passenger_jeepney",
	&"tricycle": "brgy_tricycle",
	&"cover_wall": "brgy_cover_wall",
	&"bench": "brgy_cover_wall",
	&"tent": "brgy_palengke_stall",
}

const KIT_FALLBACK_MAP: Dictionary[String, String] = {
	"brgy_tabi_cluster_a": "brgy_shophouse_row",
	"brgy_tabi_cluster_b": "brgy_house_two_storey",
	"brgy_poso_well": "brgy_cover_wall",
	"brgy_bakery_oven": "brgy_sari_sari",
	"brgy_kapilya": "brgy_hall_plaza",
	"brgy_owner_jeep": "brgy_jeepney",
	"brgy_passenger_jeepney": "brgy_jeepney",
	"brgy_merienda_stand": "brgy_powerup_stand",
	"brgy_toda_shed": "brgy_waiting_shed",
	"brgy_barangay_hall": "brgy_hall_plaza",
	"brgy_estero_bridge": "brgy_footbridge",
	"brgy_electric_post": "brgy_meralco_post",
}

const PROCEDURAL_ONLY_KITS: Array[String] = [
	"brgy_wooden_hoop",
	"brgy_chb_wall",
	"brgy_mango_tree",
	"brgy_banana_clump",
	"brgy_coconut_palm",
	"brgy_sampayan_3d",
	"brgy_powerup_stand",
	"brgy_poster_cluster",
]

const BUILTIN_SLAB_KITS: Array[String] = [
	"brgy_toda_shed",
	"brgy_palengke_stall",
	"brgy_bakery_oven",
	"brgy_kapilya",
	"brgy_waiting_shed",
	"brgy_cover_wall",
]

@export var layout: MapLayout
@export var diorama: bool = false
@export var show_overview_camera: bool = true

var far_props: Array[String] = []
var house_rows: Dictionary[int, Array] = {-1: [], 1: []}

var _base_materials: Dictionary[int, Array] = {}
var _side_tint: Dictionary[int, Color] = {}
var _rescue_ready: Dictionary[int, bool] = {}
var _posts: Dictionary[int, Node3D] = {}
var _counts: Dictionary[int, Label3D] = {}
var _base_marks: Array[Node3D] = []
var _occluders: Array[Node3D] = []
var _occluder_boxes: Array[AABB] = []
var _hidden_props: Array[Node3D] = []
var _placed_buildings: Array[Dictionary] = []
var _interior_parcels: Array[Dictionary] = []
var _tree_planter_spots: Array[Vector3] = []
var _ground_mat: ShaderMaterial = null
var _creek_bed_mat: ShaderMaterial = null

static var _scene_cache: Dictionary[String, PackedScene] = {}
static var _bounds_cache: Dictionary[String, AABB] = {}
static var _arch_mat_cache: Dictionary[int, StandardMaterial3D] = {}

@onready var _geometry: Node3D = %Geometry
@onready var _camera: Camera3D = %Camera
@onready var _sun: DirectionalLight3D = %Sun
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	_build_map()
	if show_overview_camera:
		_setup_camera()
	_light_scene(self, _sun)
	_back_button.pressed.connect(_on_back_pressed)


static func make_environment() -> Environment:
	return ArenaEnvironment.make_environment()


static func top_end(own_side: int) -> float:
	return -1.0 if own_side == MapLayout.SIDE_OWN else 1.0


static func tallest_boundary() -> float:
	return 12.0


static func boundary_width(entry: Dictionary, scale_value: float) -> float:
	var h: float = float(entry.get("height", 2.0))
	return h * scale_value


func bottom_foot(entry: Dictionary, share: float) -> float:
	var h: float = float(entry.get("height", 2.0))
	return layout.lane_length * 0.5 + BARRIER_SETBACK + h * share


func bottom_reach() -> float:
	return 62.0


func cement_share() -> Vector2:
	return Vector2.ONE


static func _light_scene(parent: Node, sun: DirectionalLight3D) -> void:
	ArenaEnvironment.light_scene(parent, sun)


func set_own_side(own_side: int) -> void:
	_turn_posts(own_side)
	for side: int in _counts:
		_turn_count(_counts[side], side, own_side)
	_rescue_ready.clear()
	for side: int in _base_materials:
		var tint: Color = OWN_COLOR if side == own_side else ENEMY_COLOR
		_side_tint[side] = tint
		for material: ShaderMaterial in _base_materials[side]:
			var color: Color = tint
			color.a = (material.get_shader_parameter("chalk") as Color).a
			material.set_shader_parameter("chalk", color)


func hide_near_houses(side: int) -> void:
	for row: int in house_rows:
		for house: Node3D in house_rows[row]:
			house.visible = row != side


func show_bases(on: bool) -> void:
	for mark: Node3D in _base_marks:
		mark.visible = on


func bases_shown() -> bool:
	return _base_marks.is_empty() or _base_marks[0].visible


func visible_posts() -> int:
	var count: int = 0
	for post: Node3D in _posts.values():
		if post.is_visible_in_tree():
			count += 1
	return count


func sync_rescue(sim: MatchSim, hide_counts: bool = false) -> void:
	if sim.mode != null:
		return
	var covered: bool = sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]
	for team: int in [0, 1]:
		var side: int = sim.side_for_team(team)
		if not _base_materials.has(side):
			continue
		var left: float = sim.rescue_ready_left[team]
		_show_count(_counts[side], left if not (covered or hide_counts) else 0.0)
		var ready: bool = left <= 0.0
		if _rescue_ready.get(side, not ready) == ready:
			continue
		_rescue_ready[side] = ready
		var tint: Color = _side_tint[side] if ready else Color.WHITE
		var materials: Array = _base_materials[side]
		(materials[1] as ShaderMaterial).set_shader_parameter("chalk", Color(tint, CHALK_ALPHA if ready else RESCUE_WHITE_ALPHA))


func rescue_count_text(side: int) -> String:
	var label: Label3D = _counts.get(side) as Label3D
	return label.text if label != null and label.visible else ""


func rescue_count(side: int) -> Label3D:
	return _counts[side]


func base_color(side: int) -> Color:
	return _side_tint[side]


func zone_colors(side: int) -> Array[Color]:
	var colors: Array[Color] = []
	for material: ShaderMaterial in _base_materials[side]:
		colors.append(material.get_shader_parameter("chalk") as Color)
	return colors


func scenery() -> float:
	return 1.0 if diorama else maxf(layout.scenery_scale, 0.5)


func cover_height() -> float:
	return COVER_HEIGHT if diorama else layout.cover_height


func post_height() -> float:
	return POST_HEIGHT if diorama else layout.post_height


func fog_distance() -> float:
	return 76.0


func fog_far_start() -> float:
	return 70.0


func _build_map() -> void:
	_placed_buildings.clear()
	_interior_parcels.clear()
	_tree_planter_spots.clear()
	_build_ground()
	if diorama:
		_build_diorama_plinth()
		_posts[MapLayout.SIDE_ENEMY] = _add_post(layout.post_position(MapLayout.SIDE_ENEMY))
		_turn_posts(MapLayout.SIDE_OWN)
		return

	_build_estero_creek_and_bridges()
	_build_district_landmarks()
	var rects: Array[Rect2] = layout.obstacle_rects()
	for index: int in rects.size():
		_add_obstacle(index)
	_sync_ground_parcel_uniforms()

	var first_mark: int = _geometry.get_child_count()
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var color: Color = OWN_COLOR if side == MapLayout.SIDE_OWN else ENEMY_COLOR
		_posts[side] = _add_post(layout.post_position(side))
		var base_mat: ShaderMaterial = _add_marked_zone(layout.base_rect(side), layout.base_corner, color, 1.0 + float(side))
		var rescue_mat: ShaderMaterial = _add_marked_zone(layout.rescue_rect(side), RESCUE_CORNER_RADIUS, color, 3.0 + float(side), true)
		_base_materials[side] = [base_mat, rescue_mat]
		_counts[side] = _add_count(layout.rescue_rect(side).get_center())
		_turn_count(_counts[side], side, MapLayout.SIDE_OWN)
		_side_tint[side] = color

	for index: int in range(first_mark, _geometry.get_child_count()):
		var mark: Node3D = _geometry.get_child(index) as Node3D
		if not _posts.values().has(mark):
			_base_marks.append(mark)
	_turn_posts(MapLayout.SIDE_OWN)

	_build_perimeter_skyline()
	_build_spaghetti_wiring_and_poles()


func _build_ground() -> void:
	var pad: float = 0.0 if diorama else OUTER_GROUND_MARGIN
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = Vector2(layout.lane_width + pad * 2.0, layout.lane_length + pad * 2.0)
	plane.subdivide_width = 64
	plane.subdivide_depth = 64
	var mat: ShaderMaterial = ShaderMaterial.new()
	mat.shader = GROUND_SHADER
	if layout.arena_radius > 0.0:
		mat.set_shader_parameter("arena_radius", layout.arena_radius)
	mat.set_shader_parameter("arena_half", Vector2(layout.lane_width * 0.5, layout.lane_length * 0.5))
	mat.set_shader_parameter("own_base_center", layout.base_center(MapLayout.SIDE_OWN))
	mat.set_shader_parameter("enemy_base_center", layout.base_center(MapLayout.SIDE_ENEMY))
	mat.set_shader_parameter("wear", 0.35 if diorama else 1.0)
	var carve_slot: bool = (not diorama) and layout.arena_radius > 0.0 and layout.water_rects().is_empty()
	mat.set_shader_parameter("carve_creek_slot", carve_slot)
	mat.set_shader_parameter("is_creek_bed", false)
	_ground_mat = mat
	plane.material = mat
	var ground: MeshInstance3D = _add(plane, Vector3.ZERO)
	ground.name = "BarangayGround"
	ground.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _sync_ground_parcel_uniforms() -> void:
	var count: int = mini(_interior_parcels.size(), 32)
	var poses: PackedVector4Array = PackedVector4Array()
	var halves: PackedVector4Array = PackedVector4Array()
	poses.resize(32)
	halves.resize(32)
	for i: int in 32:
		if i < count:
			var p: Dictionary = _interior_parcels[i]
			var c: Vector2 = p["center"] as Vector2
			var h: Vector2 = p["half"] as Vector2
			var y: float = float(p["yaw"])
			poses[i] = Vector4(c.x, c.y, cos(y), sin(y))
			halves[i] = Vector4(h.x, h.y, 0.0, 0.0)
		else:
			poses[i] = Vector4.ZERO
			halves[i] = Vector4.ZERO
	for smat: ShaderMaterial in [_ground_mat, _creek_bed_mat]:
		if smat != null:
			smat.set_shader_parameter("parcel_count", count)
			smat.set_shader_parameter("parcel_pose", poses)
			smat.set_shader_parameter("parcel_half", halves)


func _build_diorama_plinth() -> void:
	var kit: LowPoly = LowPoly.new()
	var w: float = layout.lane_width
	var l: float = layout.lane_length
	kit.rounded_slab(Vector3(0.0, -PLINTH_HEIGHT * 0.5 - 0.05, 0.0), Vector3(w, PLINTH_HEIGHT, l), CORNER_RADIUS, PLINTH, PLINTH)
	kit.rounded_slab(
		Vector3(0.0, -PLINTH_HEIGHT - PLATFORM_HEIGHT * 0.5 - 0.05, 0.0),
		Vector3(w + PLATFORM_MARGIN * 2.0, PLATFORM_HEIGHT, l + PLATFORM_MARGIN * 2.0),
		CORNER_RADIUS + PLATFORM_MARGIN,
		PLATFORM,
		PLATFORM
	)
	var matte: StandardMaterial3D = StandardMaterial3D.new()
	matte.vertex_color_use_as_albedo = true
	matte.roughness = 0.95
	_add(kit.commit(), Vector3.ZERO).material_override = matte
	var mini_court: Node3D = _instantiate_kit("brgy_covered_court")
	mini_court.scale = Vector3.ONE * 1.12
	var own_c: Vector2 = layout.base_center(MapLayout.SIDE_OWN)
	_place_walkthrough(mini_court, Vector3(own_c.x, 0.0, own_c.y), 0.0)


func _build_estero_creek_and_bridges() -> void:
	var water_rects: Array[Rect2] = layout.water_rects()
	if water_rects.is_empty() and layout.arena_radius <= 0.0:
		return

	var water_mat: ShaderMaterial = ShaderMaterial.new()
	water_mat.shader = WATER_SHADER
	water_mat.set_shader_parameter("creek_half_width", 3.75)

	if not water_rects.is_empty():
		water_mat.set_shader_parameter("curve_scale", 0.0)
		for rect: Rect2 in water_rects:
			var custom_plane: PlaneMesh = PlaneMesh.new()
			custom_plane.size = Vector2(rect.size.x, maxf(rect.size.y - 0.25, 1.0))
			custom_plane.subdivide_width = maxi(4, int(rect.size.x * 0.5))
			custom_plane.subdivide_depth = 6
			custom_plane.material = water_mat
			var c_pt: Vector2 = rect.get_center()
			var cw_mesh: MeshInstance3D = _add(custom_plane, Vector3(c_pt.x, 0.02, c_pt.y))
			cw_mesh.name = "EsteroWater"
			cw_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		return

	# 1. Sunken `-0.30 m` Tabing-Ilog Creek Bed (`PlaneMesh` seamlessly stitched into `BarangayGround` at `Y = 0`)
	var canal_len: float = layout.arena_radius * 2.0 + 6.0
	var bed_plane: PlaneMesh = PlaneMesh.new()
	bed_plane.size = Vector2(canal_len, 9.4)
	bed_plane.subdivide_width = 176
	bed_plane.subdivide_depth = 32
	var bed_mat: ShaderMaterial = _ground_mat.duplicate() as ShaderMaterial
	bed_mat.set_shader_parameter("carve_creek_slot", false)
	bed_mat.set_shader_parameter("is_creek_bed", true)
	_creek_bed_mat = bed_mat
	bed_plane.material = bed_mat
	var bed_inst: MeshInstance3D = _add(bed_plane, Vector3.ZERO)
	bed_inst.name = "TabingIlogChannelBed"
	bed_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# 2. Sunken Olive-Jade Creek Water (`Y = -0.12 m` deep, `-0.055 m` over cobbled causeway fords — NEVER above Y = 0!)
	water_mat.set_shader_parameter("curve_scale", 1.0)
	var water_plane: PlaneMesh = PlaneMesh.new()
	water_plane.size = Vector2(canal_len, 7.5)
	water_plane.subdivide_width = 148
	water_plane.subdivide_depth = 10
	water_plane.material = water_mat
	var w_mesh: MeshInstance3D = _add(water_plane, Vector3(0.0, -0.12, 0.0))
	w_mesh.name = "EsteroWater"
	w_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# 3. Sculpted 3D Mossy Volcanic-Tuff Riprap Creek Embankments, Descending River Steps & Basalt Ford Boulders!
	var bank_kit: LowPoly = LowPoly.new()
	var stone_lit: Color = Color(0.36, 0.34, 0.29)
	var stone_side: Color = Color(0.25, 0.24, 0.21)
	var moss_top: Color = Color(0.29, 0.33, 0.24)
	var cobble_warm: Color = Color(0.34, 0.31, 0.27)
	var cobble_wet: Color = Color(0.24, 0.25, 0.23)
	var basalt_rim: Color = Color(0.18, 0.19, 0.17)
	var batya_red: Color = Color(0.74, 0.24, 0.20)
	var batya_blue: Color = Color(0.20, 0.44, 0.68)

	# Curved non-ford embankment spans leaving wide open ramped causeway fords at West (-36.5), Center (0), East (+36.5)
	var has_ilog_steps_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_ilog_steps")
	var has_laundry_stones_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_laundry_stones")
	var bank_spans: Array[Vector2] = [
		Vector2(-82.5, -43.5),
		Vector2(-29.2, -8.0),
		Vector2(8.0, 29.2),
		Vector2(43.5, 82.5),
	]
	for span_x: Vector2 in bank_spans:
		var total_w: float = span_x.y - span_x.x
		var seg_count: int = maxi(4, int(ceil(total_w / 3.8)))
		var seg_len: float = total_w / float(seg_count)
		for s_i: int in seg_count:
			var x0: float = span_x.x + float(s_i) * seg_len
			var x1: float = x0 + seg_len
			var xm: float = (x0 + x1) * 0.5
			var zm0: float = MapLayout.canal_z(x0)
			var zm1: float = MapLayout.canal_z(x1)
			var zm: float = MapLayout.canal_z(xm)
			var seg_yaw: float = atan2(-(zm1 - zm0), x1 - x0)
			var end_taper: float = minf(
				clampf((xm - span_x.x) / 2.8, 0.25, 1.0),
				clampf((span_x.y - xm) / 2.8, 0.25, 1.0)
			)
			var crest_y: float = 0.095 * end_taper
			for bank_sign: float in [-1.0, 1.0]:
				var step_landing_x: float = -19.2 * bank_sign
				if has_ilog_steps_glb and absf(xm - step_landing_x) < 1.95:
					continue
				# Smooth organic weathered river-stone & olive moss gradient (s <= 0.28, ZERO alternating stripes!)
				var moss_blend: float = 0.5 + 0.5 * sin(xm * 0.11 + bank_sign * 1.15) * cos(xm * 0.047 - 0.4)
				var slope_top_col: Color = stone_lit.lerp(moss_top, moss_blend * 0.72)
				var coping_top_col: Color = stone_lit.lerp(moss_top, (1.0 - moss_blend) * 0.55).darkened(0.04 * absf(sin(xm * 0.29)))
				var bank_b: Basis = Basis(Vector3.UP, seg_yaw) * Basis(Vector3.RIGHT, bank_sign * 0.24)
				_kit_oriented_box(
					bank_kit,
					bank_b,
					Vector3(xm, -0.145, zm + bank_sign * 3.85),
					Vector3(seg_len * 1.06, 0.33, 1.24),
					slope_top_col,
					stone_side
				)
				bank_kit.set_transform(Transform3D(Basis(Vector3.UP, seg_yaw), Vector3(xm, crest_y * 0.5 - 0.01, zm + bank_sign * 4.38)))
				bank_kit.rounded_slab(
					Vector3.ZERO,
					Vector3(seg_len * 1.04, 0.12 + crest_y * 0.5, 0.60),
					0.16,
					stone_side,
					coping_top_col,
					4
				)
				bank_kit.reset_transform()

				var toe_hash: float = sin(float(s_i) * 2.73 + xm * 0.19 + bank_sign * 1.7)
				if toe_hash > -0.25:
					var rad_r: float = 0.34 + 0.20 * absf(cos(float(s_i) * 3.1 + bank_sign))
					var r_col: Color = cobble_wet.lerp(cobble_warm, 0.5 + 0.5 * toe_hash)
					var toe_yaw: float = seg_yaw + 0.65 * toe_hash
					bank_kit.set_transform(Transform3D(Basis(Vector3.UP, toe_yaw), Vector3(xm + 0.45 * toe_hash, -0.135, zm + bank_sign * (3.26 + 0.18 * toe_hash))))
					bank_kit.rounded_slab(
						Vector3.ZERO,
						Vector3(rad_r * 2.1, 0.14, rad_r * 1.55),
						rad_r * 0.46,
						basalt_rim,
						r_col,
						4
					)
					bank_kit.reset_transform()

	# Descending *Tabing-Ilog* Stone Steps + Wet *Batuhan* Laundry Stones & *Batya* Basins
	for step_side: float in [-1.0, 1.0]:
		var sx: float = -19.2 * step_side
		var sz_c: float = MapLayout.canal_z(sx)
		var slope_dz: float = MapLayout.canal_z(sx + 1.0) - MapLayout.canal_z(sx - 1.0)
		var bank_yaw: float = atan2(-slope_dz, 2.0)
		var step_b: Basis = Basis(Vector3.UP, bank_yaw)
		var b_sign: float = step_side
		if has_ilog_steps_glb:
			var ilog_steps: Node3D = _instantiate_kit("brgy_ilog_steps")
			ilog_steps.name = "TabingIlogStepsSouth" if step_side > 0.0 else "TabingIlogStepsNorth"
			# Top step (`+0.40m * 0.95 = +0.38m` above `-0.28m` base -> `Y = +0.10m`) meets bank crest flush (`0.00m` cliff)!
			ilog_steps.scale = Vector3(0.95, 0.95, 0.95)
			var steps_yaw: float = bank_yaw + (PI if step_side > 0.0 else 0.0)
			_place_walkthrough(ilog_steps, Vector3(sx, -0.28, sz_c + b_sign * 3.65), steps_yaw)
		else:
			for r_k: int in 4:
				var step_y: float = lerpf(0.06, -0.14, float(r_k) / 3.0)
				var step_z_off: float = b_sign * lerpf(4.42, 3.18, float(r_k) / 3.0)
				bank_kit.set_transform(Transform3D(step_b, Vector3(sx, step_y, sz_c + step_z_off)))
				bank_kit.rounded_slab(Vector3.ZERO, Vector3(2.85, 0.14, 0.52), 0.08, stone_side, stone_lit, 4)
				bank_kit.reset_transform()
			for cheek_x: float in [-1.52, 1.52]:
				var cheek_p: Vector3 = Vector3(sx, -0.02, sz_c + b_sign * 3.82) + step_b * Vector3(cheek_x, 0.0, 0.0)
				_kit_oriented_box(bank_kit, step_b * Basis(Vector3.RIGHT, b_sign * 0.14), cheek_p, Vector3(0.28, 0.34, 1.65), moss_top, stone_side)

		if has_laundry_stones_glb:
			var l_stones: Node3D = _instantiate_kit("brgy_laundry_stones")
			l_stones.name = "TabingIlogLaundryStonesSouth" if step_side > 0.0 else "TabingIlogLaundryStonesNorth"
			l_stones.scale = Vector3.ONE * 0.96
			var lx_glb: float = sx + 3.15 * step_side
			var lz_glb: float = MapLayout.canal_z(lx_glb) + b_sign * 3.02
			_place_walkthrough(l_stones, Vector3(lx_glb, -0.16, lz_glb), bank_yaw + (PI if step_side > 0.0 else 0.0))
		else:
			for st_j: int in 3:
				var lx: float = sx + (float(st_j) - 1.0) * 1.05
				var lz: float = MapLayout.canal_z(lx) + b_sign * (2.82 + 0.16 * float(st_j % 2))
				bank_kit.rounded_slab(Vector3(lx, -0.11, lz), Vector3(0.78, 0.14, 0.62), 0.22, cobble_wet.darkened(0.2), cobble_wet, 4)
			var basin_col: Color = batya_red if step_side > 0.0 else batya_blue
			bank_kit.cylinder(Vector3(sx + 1.35, -0.05, sz_c + b_sign * 3.15), 0.26, 0.32, 0.18, 10, basin_col, basin_col.darkened(0.2))

	# 13 irregular broad flat wet basalt river boulders clustered naturally per shallow causeway ford (deeply seated!)
	var ford_xs: PackedFloat32Array = PackedFloat32Array([MapLayout.lane_west_x(0.0), 0.0, MapLayout.lane_east_x(0.0)])
	for f_idx: int in ford_xs.size():
		var ford_x: float = ford_xs[f_idx]
		for b_i: int in 13:
			var seed_a: float = float(b_i) * 2.39996 + float(f_idx) * 1.71
			var shoulder_sign: float = -1.0 if (b_i % 2 == 0) else 1.0
			var bx_off: float = shoulder_sign * (3.65 + 2.35 * absf(sin(seed_a * 1.37))) + 0.85 * cos(seed_a * 2.11)
			if b_i >= 10:
				# 3 wide ultra-low submerged flat stepping stones nestled inside the shallow wade itself
				bx_off = (float(b_i - 11)) * 2.35 + 0.65 * sin(seed_a * 1.9)
			var bz_off: float = 3.05 * sin(seed_a * 1.73 + 0.4)
			var world_bx: float = ford_x + bx_off
			var world_bz: float = MapLayout.canal_z(world_bx) + bz_off
			var bw: float = 0.58 + 0.46 * absf(cos(seed_a * 1.29 + 0.3))
			var bd: float = 0.48 + 0.40 * absf(sin(seed_a * 1.61 + 0.8))
			var bh: float = 0.095 + 0.032 * absf(sin(seed_a * 2.4))
			var by: float = -0.136 + 0.026 * absf(cos(seed_a * 1.83))
			if b_i >= 10:
				by = -0.138
				bh = 0.085
			var b_yaw: float = seed_a * 1.15
			var stone_top_c: Color = cobble_wet.lerp(cobble_warm, absf(sin(seed_a * 1.47)) * 0.72)
			bank_kit.set_transform(Transform3D(Basis(Vector3.UP, b_yaw), Vector3(world_bx, by, world_bz)))
			bank_kit.rounded_slab(
				Vector3.ZERO,
				Vector3(bw, bh, bd),
				minf(bw, bd) * 0.38,
				basalt_rim,
				stone_top_c,
				4
			)
			bank_kit.reset_transform()

	var bank_mesh: ArrayMesh = bank_kit.commit()
	var bank_mat: StandardMaterial3D = StandardMaterial3D.new()
	bank_mat.vertex_color_use_as_albedo = true
	bank_mat.roughness = 0.90
	bank_mat.next_pass = null
	bank_mesh.surface_set_material(0, bank_mat)
	var bank_inst: MeshInstance3D = _add(bank_mesh, Vector3.ZERO)
	bank_inst.name = "TabingIlogBanks"
	bank_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# Scenic outer river-mouth footbridges seated cleanly into the embankment abutments outside the 3 pathways
	for mouth_sign: float in [-1.0, 1.0]:
		var mx: float = mouth_sign * 81.2
		var mz: float = MapLayout.canal_z(mx)
		var m_slope: float = MapLayout.canal_z(mx + 1.0) - MapLayout.canal_z(mx - 1.0)
		var bridge_id: String = "brgy_estero_bridge" if ResourceLoader.exists(BARANGAY_DIR % "brgy_estero_bridge") else "brgy_footbridge"
		var bridge: Node3D = _instantiate_kit(bridge_id)
		bridge.name = "RiverMouthFootbridgeWest" if mouth_sign < 0.0 else "RiverMouthFootbridgeEast"
		var nat: Vector3 = _kit_bounds(bridge_id).size
		bridge.scale = Vector3(4.2 / maxf(nat.x, 0.1), 0.92, 8.6 / maxf(nat.z, 0.1))
		_place_walkthrough(bridge, Vector3(mx, -0.06, mz), atan2(-m_slope, 2.0))


func _build_district_landmarks() -> void:
	# 1. South (+Z) Own Base Landmark: Covered Barangay Basketball Court framing the South Outdoor Liga Plaza (`Z = +95.5`, zero roof over `post_blue`!)
	var own_c: Vector2 = layout.base_center(MapLayout.SIDE_OWN)
	var court: Node3D = _instantiate_kit("brgy_covered_court")
	court.name = "CoveredBasketballCourt"
	court.scale = Vector3(1.12, 1.04, 1.08)
	var court_z: float = (layout.arena_radius + 11.5) if layout.arena_radius > 0.0 else (layout.lane_length * 0.5 + 11.5)
	var court_yaw: float = PI + deg_to_rad(3.5)
	_add_prop(court, Vector3(own_c.x, 0.0, court_z), court_yaw)
	_record_building_footprint(Vector2(own_c.x, court_z), _kit_bounds("brgy_covered_court").size * court.scale, court_yaw, false)

	# 2. North (-Z) Enemy Base Landmark: Municipal Barangay Hall framing the -Z Base Forecourt (`Z = -89.2`)
	var enemy_c: Vector2 = layout.base_center(MapLayout.SIDE_ENEMY)
	var hall_id: String = "brgy_barangay_hall" if ResourceLoader.exists(BARANGAY_DIR % "brgy_barangay_hall") else "brgy_hall_plaza"
	var hall: Node3D = _instantiate_kit(hall_id)
	hall.name = "BarangayHall"
	hall.scale = Vector3.ONE * 1.08
	var hall_z: float = -(layout.arena_radius + 5.2) if layout.arena_radius > 0.0 else (-layout.lane_length * 0.5 - 7.5)
	_add_prop(hall, Vector3(enemy_c.x, 0.0, hall_z), deg_to_rad(-4.5))
	_record_building_footprint(Vector2(enemy_c.x, hall_z), _kit_bounds(hall_id).size * 1.08, deg_to_rad(-4.5), false, hall_id, hall.transform)

	# 2b. Barrio Chapel (`brgy_kapilya`) Landmarks framing the South-East & North-West Plaza Horizon (`r > 84m`)
	if layout.arena_radius > 0.0 and ResourceLoader.exists(BARANGAY_DIR % "brgy_kapilya"):
		for kap_sign: float in [1.0, -1.0]:
			var kap_xz: Vector2 = Vector2(21.5 * kap_sign, 88.2 * kap_sign)
			var kap_yaw: float = atan2(-kap_xz.x, -kap_xz.y) + deg_to_rad(6.0 * kap_sign)
			var kapilya: Node3D = _instantiate_kit("brgy_kapilya")
			kapilya.name = "BarrioKapilyaSouth" if kap_sign > 0.0 else "BarrioKapilyaNorth"
			kapilya.scale = Vector3.ONE * 1.02
			_add_prop(kapilya, Vector3(kap_xz.x, 0.0, kap_xz.y), kap_yaw)
			_record_building_footprint(kap_xz, _kit_bounds("brgy_kapilya").size * 1.02, kap_yaw, false, "brgy_kapilya", kapilya.transform)
			var kap_row: int = -1 if kap_xz.x < 0.0 else 1
			house_rows[kap_row].append(kapilya)
			far_props.append("brgy_kapilya")

	# 3. Makeshift 90s Provincial Wooden Basketball Posts (`brgy_wooden_hoop`) court-side at both plazas
	for hoop_sign: float in [1.0, -1.0]:
		var hoop: Node3D = _instantiate_kit("brgy_wooden_hoop")
		hoop.name = "WoodenBasketballPostSouth" if hoop_sign > 0.0 else "WoodenBasketballPostNorth"
		hoop.scale = Vector3.ONE * 0.98
		var hoop_yaw: float = -PI * 0.46 if hoop_sign > 0.0 else PI * 0.54
		_place_walkthrough(hoop, Vector3(hoop_sign * 10.6, 0.0, hoop_sign * 56.4), hoop_yaw)

	# 4. NW & SE Rescue Patio Waiting Sheds framing the 5-slot tagged-player sidewalks
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var r_rect: Rect2 = layout.rescue_rect(side)
		var tag_mid: Vector2 = layout.tagged_place(side, 2)
		var shed: Node3D = _instantiate_kit("brgy_waiting_shed")
		shed.name = "RescueWaitingShedOwn" if side == MapLayout.SIDE_OWN else "RescueWaitingShedEnemy"
		shed.scale = Vector3(1.06, 1.02, 1.04)
		var outward_x: float = -float(side) * 2.15
		var sx: float = tag_mid.x + outward_x
		var yaw: float = PI * 0.5 if side == MapLayout.SIDE_OWN else -PI * 0.5
		_place_walkthrough(shed, Vector3(sx, 0.0, r_rect.get_center().y), yaw)

	# 5. 4 Dry Bridgehead Merienda Power-Up Stands (`brgy_powerup_stand` & `brgy_merienda_stand`) overlooking the crossings!
	var has_merienda_alt: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_merienda_stand")
	for pu_idx: int in layout.powerup_spots.size():
		var pu_spot: Vector2 = layout.powerup_spots[pu_idx]
		var stand_kit: String = "brgy_merienda_stand" if (has_merienda_alt and (pu_idx == 1 or pu_idx == 2)) else "brgy_powerup_stand"
		var stand: Node3D = _instantiate_kit(stand_kit)
		stand.name = "PowerupStand_%d" % pu_idx
		stand.scale = Vector3.ONE * 0.96
		# Offset onto flat dry bridgehead sidewalk concrete (`>= 7.15 m` clear of `canal_z`, zero overhang onto sloped creek banks!)
		var near_lane_x: float = MapLayout.lane_center_x(pu_spot.y)
		if absf(pu_spot.x) > 18.0:
			near_lane_x = MapLayout.lane_west_x(pu_spot.y) if pu_spot.x < 0.0 else MapLayout.lane_east_x(pu_spot.y)
		var away_sign: float = 1.0 if pu_spot.x >= near_lane_x else -1.0
		var stand_x: float = pu_spot.x + away_sign * 1.55
		var cz_stand: float = MapLayout.canal_z(stand_x)
		var bank_dir: float = 1.0 if pu_spot.y >= cz_stand else -1.0
		var stand_z: float = pu_spot.y + bank_dir * 1.15
		if absf(stand_z - cz_stand) < 7.15:
			stand_z = cz_stand + bank_dir * 7.15
		for r_obs: Rect2 in layout.obstacle_rects():
			var grown_obs: Rect2 = r_obs.grow(1.25)
			if grown_obs.has_point(Vector2(stand_x, stand_z)):
				stand_x = (grown_obs.position.x - 0.15) if stand_x < grown_obs.get_center().x else (grown_obs.end.x + 0.15)
				cz_stand = MapLayout.canal_z(stand_x)
				if absf(stand_z - cz_stand) < 7.15:
					stand_z = cz_stand + bank_dir * 7.15
		var stand_pos: Vector3 = Vector3(stand_x, 0.0, stand_z)
		var face_vec: Vector2 = Vector2(pu_spot.x - stand_pos.x, pu_spot.y - stand_pos.z)
		_place_walkthrough(stand, stand_pos, atan2(face_vec.x, face_vec.y))

	# 6. Cohesive TODA Tricycle Terminal Queue (parked right beside `P07` `brgy_toda_shed` at `(-43.6, 17.6)`!)
	var trike_specs: Array[Vector4] = [
		Vector4(-42.1, 13.4, deg_to_rad(72.0), 0.96),
		Vector4(-41.6, 10.6, deg_to_rad(84.0), 0.96),
		Vector4(42.1, -13.4, deg_to_rad(-108.0), 0.96),
		Vector4(41.6, -10.6, deg_to_rad(-96.0), 0.96),
		Vector4(14.8, 46.8, deg_to_rad(-54.0), 0.96),
		Vector4(-14.8, -46.8, deg_to_rad(126.0), 0.96),
	]
	for t_i: int in trike_specs.size():
		var ts: Vector4 = trike_specs[t_i]
		var trike: Node3D = _instantiate_kit("brgy_tricycle")
		trike.name = "TodaTricycle_%d" % t_i
		trike.scale = Vector3.ONE * ts.w
		_place_walkthrough(trike, Vector3(ts.x, 0.0, ts.y), ts.z)

	# 7. Vulcanizing Bay Owner-Type Jeeps (`brgy_owner_jeep`), Talipapa Passenger Jeepneys (`brgy_passenger_jeepney`) & Plaza Jeepneys!
	var owner_kit: String = "brgy_owner_jeep" if ResourceLoader.exists(BARANGAY_DIR % "brgy_owner_jeep") else "brgy_jeepney"
	var pass_kit: String = "brgy_passenger_jeepney" if ResourceLoader.exists(BARANGAY_DIR % "brgy_passenger_jeepney") else "brgy_jeepney"
	var jeep_entries: Array[Dictionary] = [
		{"kit": owner_kit, "pose": Vector4(42.2, 15.2, deg_to_rad(-114.0), 0.98)},   # Vulcanizing Shop apron (`P11`)
		{"kit": owner_kit, "pose": Vector4(-42.2, -15.2, deg_to_rad(66.0), 0.98)},   # Mirrored North-West talyer bay
		{"kit": pass_kit, "pose": Vector4(41.2, 40.2, deg_to_rad(-169.0), 0.96)},    # Curbside lay-by beside Talipapa Stalls (`P12/P13`)
		{"kit": pass_kit, "pose": Vector4(-41.2, -40.2, deg_to_rad(11.0), 0.96)},    # Mirrored North-West curbside lay-by
		{"kit": "brgy_jeepney", "pose": Vector4(-22.8, 72.4, deg_to_rad(62.0), 0.96)},  # South-West Plaza outer curb
		{"kit": "brgy_jeepney", "pose": Vector4(22.8, -72.4, deg_to_rad(-118.0), 0.96)}, # North-East Plaza outer curb
	]
	for j_idx: int in jeep_entries.size():
		var j_ent: Dictionary = jeep_entries[j_idx]
		var js: Vector4 = j_ent["pose"] as Vector4
		var jeep: Node3D = _instantiate_kit(String(j_ent["kit"]))
		jeep.name = "ProvincialJeepney_%d" % j_idx
		jeep.scale = Vector3.ONE * js.w
		_place_walkthrough(jeep, Vector3(js.x, 0.0, js.y), js.z)

	# 7b. Non-Military Breeze-Block *Ventanilla* & Bougainvillea Planter Walls (`brgy_cover_wall`) framing Plaza Gardens
	for cw_sign: float in [1.0, -1.0]:
		var cw_node: Node3D = _instantiate_kit("brgy_cover_wall")
		cw_node.name = "PlazaBougainvilleaWallSouth" if cw_sign > 0.0 else "PlazaBougainvilleaWallNorth"
		cw_node.scale = Vector3.ONE * 0.98
		_place_walkthrough(
			cw_node,
			Vector3(14.2 * cw_sign, 0.0, 77.4 * cw_sign),
			deg_to_rad(-18.0 if cw_sign > 0.0 else 162.0)
		)

	# 8. Shaded Plaza *Mamang Sorbetero* Carts (parked right under Mango Trees!) & Looban Barrio Poso Wells
	for sign_f: float in [1.0, -1.0]:
		var poso_at: Vector3 = Vector3(-16.2 * sign_f, 0.0, 22.4 * sign_f)
		_place_poso_landmark(poso_at, deg_to_rad(28.0 if sign_f > 0.0 else -152.0))

		var shade_tree_pos: Vector3 = Vector3(15.2 * sign_f, 0.0, 62.8 * sign_f)
		var plaza_tree: Node3D = _instantiate_kit("brgy_mango_tree")
		plaza_tree.name = "PlazaShadeMangoSouth" if sign_f > 0.0 else "PlazaShadeMangoNorth"
		plaza_tree.scale = Vector3.ONE * 1.04
		_place_walkthrough(plaza_tree, shade_tree_pos, 0.75 * sign_f)
		_tree_planter_spots.append(shade_tree_pos)

		var cart: Node3D = _instantiate_kit("brgy_sorbetes_cart")
		cart.name = "SorbetesCartSouth" if sign_f > 0.0 else "SorbetesCartNorth"
		cart.scale = Vector3.ONE * 0.96
		_place_walkthrough(
			cart,
			Vector3(12.8 * sign_f, 0.0, 61.6 * sign_f),
			deg_to_rad(-72.0 if sign_f > 0.0 else 108.0)
		)

	# 9. Standalone Street-Corner Wayfinding Notice Posts (`brgy_poster_cluster`, NEVER overlapping electric poles!)
	if ResourceLoader.exists(BARANGAY_DIR % "brgy_poster_cluster"):
		var corner_signs: Array[Vector3] = [
			Vector3(-8.4, 0.0, 23.2),
			Vector3(8.4, 0.0, -23.2),
			Vector3(41.8, 0.0, 24.4),
			Vector3(-41.8, 0.0, -24.4),
		]
		for cs_i: int in corner_signs.size():
			var cs: Vector3 = corner_signs[cs_i]
			var sign_post: Node3D = _instantiate_kit("brgy_poster_cluster")
			sign_post.name = "CornerWayfindingSign_%d" % cs_i
			sign_post.scale = Vector3.ONE * 0.95
			_place_walkthrough(sign_post, cs, atan2(-cs.x, -cs.z) + deg_to_rad(18.0))


func _place_poso_landmark(at: Vector3, yaw: float) -> void:
	var poso_glb: String = BARANGAY_DIR % "brgy_poso_well"
	if ResourceLoader.exists(poso_glb):
		var model: Node3D = _instantiate_kit("brgy_poso_well")
		model.name = "BarrioPoso"
		model.scale = Vector3.ONE * 1.02
		_place_walkthrough(model, at, yaw)
		return
	var kit: LowPoly = LowPoly.new()
	var slab_top: Color = Color(0.42, 0.41, 0.38)
	var slab_side: Color = Color(0.28, 0.27, 0.25)
	var iron_top: Color = Color(0.20, 0.40, 0.26)
	var iron_side: Color = Color(0.12, 0.26, 0.18)
	var handle_col: Color = Color(0.58, 0.22, 0.18)
	var bucket_col: Color = Color(0.20, 0.38, 0.62)
	kit.rounded_slab(Vector3(0.0, 0.06, 0.0), Vector3(1.55, 0.12, 1.55), 0.28, slab_side, slab_top)
	kit.cylinder(Vector3(0.0, 0.12, 0.0), 0.15, 0.11, 0.88, 8, iron_top, iron_side)
	kit.cylinder(Vector3(0.0, 0.98, 0.0), 0.19, 0.17, 0.22, 8, iron_top, iron_side)
	kit.box(Vector3(0.22, 0.84, 0.0), Vector3(0.32, 0.09, 0.09), iron_top, iron_side)
	_kit_oriented_box(
		kit,
		Basis.from_euler(Vector3(0.0, 0.0, -0.46)),
		Vector3(-0.32, 1.12, 0.0),
		Vector3(0.72, 0.06, 0.06),
		handle_col,
		handle_col * 0.72
	)
	kit.cylinder(Vector3(0.40, 0.12, 0.0), 0.16, 0.20, 0.32, 8, bucket_col, bucket_col * 0.72)
	var mesh: ArrayMesh = kit.commit()
	var mat: StandardMaterial3D = StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.roughness = 0.86
	mat.next_pass = null
	mesh.surface_set_material(0, mat)
	var inst: MeshInstance3D = _add(mesh, at, yaw)
	inst.name = "BarrioPoso"


func _record_building_footprint(
	center_xz: Vector2,
	scaled_size: Vector3,
	yaw: float,
	is_interior: bool = false,
	kit_id: String = "",
	bldg_transform: Transform3D = Transform3D.IDENTITY
) -> void:
	var rec: Dictionary = {
		"center": center_xz,
		"half": Vector2(scaled_size.x * 0.5, scaled_size.z * 0.5),
		"yaw": yaw,
		"height": scaled_size.y,
		"kit_id": kit_id,
	}
	if kit_id != "":
		rec["transform"] = bldg_transform
	_placed_buildings.append(rec)
	if is_interior:
		_interior_parcels.append(rec)


func _add_obstacle(index: int) -> void:
	var rect: Rect2 = layout.obstacle_rects()[index]
	var prop_key: StringName = layout.obstacle_prop(index)
	var kit_id: String = OBSTACLE_KIT_MAP.get(prop_key, "brgy_cover_wall")
	var model: Node3D = _instantiate_kit(kit_id)
	var bounds: AABB = _kit_bounds(kit_id)
	var nat: Vector3 = bounds.size
	var center: Vector2 = rect.get_center()

	if layout.has_oriented_parcels():
		# Overhaul v3: True-scale (`0.95..1.02x`) rotated parcels aligned to curved streets (ZERO 2.5x stretching!)
		var yaw_oriented: float = layout.parcel_yaw_rad(index)
		var u_scale: float = clampf(layout.parcel_scale(index), 0.90, 1.08)
		var sy_var: float = u_scale * (0.97 + 0.05 * sin(float(index) * 2.3 + 0.5))
		if prop_key == &"cover_wall":
			sy_var = cover_height() / maxf(nat.y, 0.1)
		model.scale = Vector3(u_scale, sy_var, u_scale)
		var b_center_o: Vector3 = bounds.get_center() * model.scale
		var shift_o: Vector3 = Basis(Vector3.UP, yaw_oriented) * Vector3(b_center_o.x, 0.0, b_center_o.z)
		var plinth_lift: float = 0.0 if BUILTIN_SLAB_KITS.has(kit_id) else 0.045
		var ground_y: float = -bounds.position.y * model.scale.y + plinth_lift
		_add_prop(model, Vector3(center.x, ground_y, center.y) - shift_o, yaw_oriented)
		_record_building_footprint(center, nat * model.scale, yaw_oriented, true, kit_id, model.transform)
		return

	# Legacy unrotated fallback for custom unit-test MapLayout instances without `obstacle_yaws_deg`
	var turn_quarter: bool = (rect.size.y > rect.size.x * 1.18) and (kit_id != "brgy_jeepney" and kit_id != "brgy_tricycle")
	var yaw_legacy: float = PI * 0.5 if (turn_quarter and center.x < 0.0) else (-PI * 0.5 if turn_quarter else (PI if center.y > 0.0 else 0.0))
	var target_w: float = rect.size.y if turn_quarter else rect.size.x
	var target_d: float = rect.size.x if turn_quarter else rect.size.y
	var sx: float = clampf(target_w / maxf(nat.x, 0.1), 0.85, 1.25)
	var sz: float = clampf(target_d / maxf(nat.z, 0.1), 0.85, 1.25)
	var sy: float = cover_height() / maxf(nat.y, 0.1) if prop_key == &"cover_wall" else clampf((sx + sz) * 0.5, 0.85, 1.18)
	model.scale = Vector3(sx, sy, sz)
	var b_center: Vector3 = bounds.get_center() * model.scale
	var shift: Vector3 = Basis(Vector3.UP, yaw_legacy) * Vector3(b_center_x_safe(b_center), 0.0, b_center.z)
	_add_prop(model, Vector3(center.x, -bounds.position.y * model.scale.y, center.y) - shift, yaw_legacy)
	_record_building_footprint(center, nat * model.scale, yaw_legacy, true, kit_id, model.transform)


static func b_center_x_safe(v: Vector3) -> float:
	return v.x


func _build_perimeter_skyline() -> void:
	if layout.arena_radius <= 0.0 and layout.lane_width < 40.0:
		return
	var perimeter_kits: Array[String] = [
		"brgy_tabi_cluster_a",
		"brgy_shophouse_row",
		"brgy_tabi_cluster_b",
		"brgy_house_two_storey",
		"brgy_sari_sari",
		"brgy_tabi_cluster_a",
		"brgy_house_two_storey",
	]
	var tree_kits: Array[String] = [
		"brgy_mango_tree",
		"brgy_coconut_palm",
		"brgy_banana_clump",
	]
	var ring_radius: float = layout.arena_radius if layout.arena_radius > 0.0 else minf(layout.lane_width, layout.lane_length) * 0.5

	# 1. Seamless Perimeter Compound CHB Wall (`r = 85.15 m`) + Lush Trees in Soil Planters + Non-Grid Skyline Houses!
	var inner_slots: int = 48
	var wall_r: float = ring_radius + 1.15
	var house_inner_r: float = ring_radius + 6.8
	var wall_chord: float = (TAU * wall_r / float(inner_slots)) * 1.05
	var r_own: Rect2 = layout.rescue_rect(MapLayout.SIDE_OWN)
	var r_ene: Rect2 = layout.rescue_rect(MapLayout.SIDE_ENEMY)

	for i: int in inner_slots:
		var angle: float = TAU * float(i) / float(inner_slots)
		var dir: Vector2 = Vector2(cos(angle), sin(angle))
		var p_wall: Vector2 = dir * wall_r
		var in_canal_mouth: bool = absf(p_wall.y - MapLayout.canal_z(p_wall.x)) < 5.4
		var in_own_shed: bool = p_wall.x < 0.0 and p_wall.y >= r_own.position.y - 1.5 and p_wall.y <= r_own.end.y + 1.5
		var in_ene_shed: bool = p_wall.x > 0.0 and p_wall.y >= r_ene.position.y - 1.5 and p_wall.y <= r_ene.end.y + 1.5
		var in_plaza_forecourt: bool = absf(p_wall.x) < 10.5 and absf(p_wall.y) > 76.0
		var inward_yaw: float = atan2(-dir.x, -dir.y)

		if not (in_canal_mouth or in_own_shed or in_ene_shed or in_plaza_forecourt):
			var chb: Node3D = _instantiate_kit("brgy_chb_wall")
			chb.name = "ChbPerimeterWall_%d" % i
			var chb_nat: Vector3 = _kit_bounds("brgy_chb_wall").size
			chb.scale = Vector3(clampf(wall_chord / maxf(chb_nat.x, 2.0), 0.96, 1.88), clampf(2.25 / maxf(chb_nat.y, 1.0), 0.92, 1.15), 1.0)
			_place_walkthrough(chb, Vector3(p_wall.x, 0.0, p_wall.y), inward_yaw)

			if i % 2 == 0:
				var t_id: String = tree_kits[(i / 2) % tree_kits.size()]
				var tree_node: Node3D = _instantiate_kit(t_id)
				tree_node.name = "PerimeterTree_%d" % i
				var t_scale: float = 0.94 + 0.24 * absf(sin(float(i) * 2.3))
				tree_node.scale = Vector3.ONE * t_scale
				var p_tree: Vector2 = dir * (ring_radius + 3.9 + 1.8 * absf(cos(float(i) * 1.9)))
				var tree_pos: Vector3 = Vector3(p_tree.x, 0.0, p_tree.y)
				_place_walkthrough(tree_node, tree_pos, float(i) * 1.1)
				_tree_planter_spots.append(tree_pos)

		# True-scale inner perimeter houses (`r = 90.8..98.5 m`, strictly `> arena_radius = 84.0 m`, rotated 6..15 deg!)
		if i % 4 != 2:
			var local_r: float = house_inner_r + 2.2 * sin(float(i) * 2.9 + 0.4)
			if absf(dir.y) > 0.88:
				local_r += 23.5
			elif in_canal_mouth or in_own_shed or in_ene_shed:
				local_r += 4.8
			var pos2: Vector2 = dir * local_r
			var near_kapilya: bool = pos2.distance_to(Vector2(21.5, 88.2)) < 9.2 or pos2.distance_to(Vector2(-21.5, -88.2)) < 9.2
			if near_kapilya:
				pos2 = dir * (local_r + 9.6)
			var kit_id: String = perimeter_kits[i % perimeter_kits.size()]
			var house: Node3D = _instantiate_kit(kit_id)
			var nat: Vector3 = _kit_bounds(kit_id).size
			var u_sc: float = 0.94 + 0.14 * absf(sin(float(i) * 2.13 + 0.3))
			house.scale = Vector3.ONE * u_sc
			var h_yaw: float = inward_yaw + deg_to_rad(11.5 * sin(float(i) * 3.7 + 0.9))
			_add_prop(house, Vector3(pos2.x, 0.0, pos2.y), h_yaw)
			_record_building_footprint(pos2, nat * house.scale, h_yaw, false, kit_id, house.transform)
			var row_key: int = -1 if pos2.x < 0.0 else 1
			house_rows[row_key].append(house)
			far_props.append(kit_id)

	# 2. Outer Provincial Barangay Roof & Coconut/Mango Grove Tier (`r = ring_radius + 19..25 m`)
	var outer_count: int = 40
	var outer_r: float = ring_radius + 19.6
	for j: int in outer_count:
		var angle_o: float = TAU * (float(j) + 0.5) / float(outer_count) + 0.018 * cos(float(j) * 3.1)
		var dir_o: Vector2 = Vector2(cos(angle_o), sin(angle_o))
		var r_o: float = outer_r + 2.8 * sin(float(j) * 3.3 + 1.1)
		if absf(dir_o.y) > 0.88:
			r_o += 16.0
		var pos_o: Vector2 = dir_o * r_o
		var kit_o: String = perimeter_kits[(j + 2) % perimeter_kits.size()]
		var bldg: Node3D = _instantiate_kit(kit_o)
		var u_so: float = 0.96 + 0.16 * absf(cos(float(j) * 2.3 + 0.7))
		bldg.scale = Vector3.ONE * u_so
		var yaw_o: float = atan2(-dir_o.x, -dir_o.y) + deg_to_rad(12.5 * cos(float(j) * 2.9))
		_add_prop(bldg, Vector3(pos_o.x, 0.0, pos_o.y), yaw_o)
		var row_o: int = -1 if pos_o.x < 0.0 else 1
		house_rows[row_o].append(bldg)
		far_props.append(kit_o)

		if j % 2 == 0:
			var grove_id: String = tree_kits[(j + 1) % tree_kits.size()]
			var grove_tree: Node3D = _instantiate_kit(grove_id)
			grove_tree.scale = Vector3.ONE * (1.04 + 0.22 * absf(sin(float(j) * 1.7)))
			var pos_g: Vector2 = dir_o * (r_o + 8.5)
			_place_walkthrough(grove_tree, Vector3(pos_g.x, 0.0, pos_g.y), float(j) * 1.4)


func _curb_pole_spec(lane_id: int, z_val: float, side_sign: float) -> Vector4:
	var z1: float = z_val + 1.0
	var z0: float = z_val - 1.0
	var x_c: float = 0.0
	var hw: float = 4.6
	var dx: float = 0.0
	if lane_id == 0:
		x_c = MapLayout.lane_center_x(z_val)
		hw = MapLayout.lane_center_half_width(z_val)
		dx = MapLayout.lane_center_x(z1) - MapLayout.lane_center_x(z0)
	elif lane_id < 0:
		x_c = MapLayout.lane_west_x(z_val)
		hw = MapLayout.lane_side_half_width(z_val)
		dx = MapLayout.lane_west_x(z1) - MapLayout.lane_west_x(z0)
	else:
		x_c = MapLayout.lane_east_x(z_val)
		hw = MapLayout.lane_side_half_width(z_val)
		dx = MapLayout.lane_east_x(z1) - MapLayout.lane_east_x(z0)
	var curb_x: float = x_c + side_sign * (hw + 0.72)
	var tangent_yaw: float = atan2(dx, 2.0)
	# Nudge clear if close to any solid obstacle collider
	var cand: Vector2 = Vector2(curb_x, z_val)
	for r_obs: Rect2 in layout.obstacle_rects():
		var grown: Rect2 = r_obs.grow(0.75)
		if grown.has_point(cand):
			cand.x = x_c + side_sign * (hw + 0.42)
	return Vector4(cand.x, cand.y, tangent_yaw, 1.0)


func _build_spaghetti_wiring_and_poles() -> void:
	if layout.arena_radius <= 0.0 and layout.lane_width < 80.0:
		return
	var ring_r: float = (layout.arena_radius if layout.arena_radius > 0.0 else 84.0) - 0.45

	# 1. Curb-Aligned Meralco Electric Poles following the 3 S-curved pathways + outer perimeter ring!
	#    Indices:
	#      0..5   = West Feeder Road West-curb chain (z = -52, -31, -9, +9, +31, +52)
	#      6..11  = Center Avenue West/East curb chain (z = -48, -24, -8, +8, +24, +48)
	#      12..17 = East Feeder Road East-curb chain (z = -52, -31, -9, +9, +31, +52)
	#      18..23 = Opposite-curb Banner/Cross-Street Partner Poles (paired 1:1 for exact 2-ended Banderitas!)
	#      24..39 = 16 Perimeter Ring Utility Poles around `r = 83.55 m`
	var pole_specs: Array[Vector4] = []
	var west_zs: Array[float] = [-52.0, -31.0, -8.8, 8.8, 31.0, 52.0]
	for wz: float in west_zs:
		pole_specs.append(_curb_pole_spec(-1, wz, -1.0))

	var center_zs: Array[float] = [-47.5, -23.5, -8.2, 8.2, 23.5, 47.5]
	for cz_i: int in center_zs.size():
		var cz_v: float = center_zs[cz_i]
		var c_side: float = -1.0 if cz_v > 0.0 else 1.0
		pole_specs.append(_curb_pole_spec(0, cz_v, c_side))

	for ez: float in west_zs:
		pole_specs.append(_curb_pole_spec(1, ez, 1.0))

	# Opposite-curb partner poles (18..23) creating exact cross-lane anchor pairs for Banderitas & Eskinita trunks!
	pole_specs.append(_curb_pole_spec(-1, 31.0, 1.0))   # 18 pairs across West Road with pole 4
	pole_specs.append(_curb_pole_spec(0, 47.5, 1.0))    # 19 pairs across Center Plaza throat with pole 11
	pole_specs.append(_curb_pole_spec(1, 31.0, -1.0))   # 20 pairs across East Road with pole 16
	pole_specs.append(_curb_pole_spec(-1, -31.0, 1.0))  # 21 pairs across West Road with pole 1
	pole_specs.append(_curb_pole_spec(0, -47.5, -1.0))  # 22 pairs across North Center Plaza throat with pole 6
	pole_specs.append(_curb_pole_spec(1, -31.0, -1.0))  # 23 pairs across East Road with pole 13

	for p_ring_i: int in 16:
		var ang: float = TAU * (float(p_ring_i) + 0.35) / 16.0
		var px: float = cos(ang) * ring_r
		var pz: float = sin(ang) * ring_r
		pole_specs.append(Vector4(px, pz, ang, 0.90))

	var pole_kit_id: String = "brgy_electric_post" if ResourceLoader.exists(BARANGAY_DIR % "brgy_electric_post") else "brgy_meralco_post"
	var nat_pole_h: float = maxf(_kit_bounds(pole_kit_id).size.y, 6.0)
	var primary_scale_y: float = 9.4 / nat_pole_h
	var secondary_scale_y: float = 8.4 / nat_pole_h

	var pole_nodes: Array[Node3D] = []
	var pole_pin_sets: Array[PackedVector3Array] = []
	var pole_shaft_tops: Array[Vector3] = []

	for idx: int in pole_specs.size():
		var spec: Vector4 = pole_specs[idx]
		var spot: Vector3 = Vector3(spec.x, 0.0, spec.y)
		var is_ring: bool = idx >= 24
		var pole: Node3D = _instantiate_kit(pole_kit_id)
		pole.name = "ElectricPole_%d" % idx
		var sy_p: float = secondary_scale_y if is_ring else primary_scale_y
		var sxz_p: float = 0.80 if is_ring else 0.88
		pole.scale = Vector3(sxz_p, sy_p, sxz_p)
		var lean_x: float = deg_to_rad(1.6 * sin(float(idx) * 2.1 + 0.4))
		var lean_z: float = deg_to_rad(1.6 * cos(float(idx) * 1.7 - 0.3))
		pole.position = spot
		pole.rotation = Vector3(lean_x, spec.z, lean_z)
		_geometry.add_child(pole)
		pole_nodes.append(pole)
		pole_pin_sets.append(SpaghettiWires.compute_pole_pins(pole))
		pole_shaft_tops.append(pole.transform * Vector3(0.0, 10.15, -0.510))

	# 2. Exact Two-Ended Pole-to-Pole Fiesta Banderitas (`0.00 m` air gap at both ends on the `-0.510` pole trunk!)
	var dressing_kit: LowPoly = LowPoly.new()
	var banner_pairs: Array[Vector2i] = [
		Vector2i(4, 18),   # Across South-West Feeder Road
		Vector2i(11, 19),  # Across South Center Plaza Throat
		Vector2i(20, 16),  # Across South-East Feeder Road
		Vector2i(1, 21),   # Across North-West Feeder Road
		Vector2i(22, 6),   # Across North Center Plaza Throat
		Vector2i(23, 13),  # Across North-East Feeder Road
	]
	var collar_steel: Color = Color(0.38, 0.40, 0.43)
	for b_idx: int in banner_pairs.size():
		var bp: Vector2i = banner_pairs[b_idx]
		var pa_node: Node3D = pole_nodes[bp.x]
		var pb_node: Node3D = pole_nodes[bp.y]
		var anc_a: Vector3 = pa_node.transform * Vector3(0.0, 8.15, -0.510)
		var anc_b: Vector3 = pb_node.transform * Vector3(0.0, 8.15, -0.510)
		var y_anchor: float = (anc_a.y + anc_b.y) * 0.5
		var span_xz: Vector2 = Vector2(anc_b.x - anc_a.x, anc_b.z - anc_a.z)
		var span_len: float = span_xz.length()
		if span_len > 4.0 and span_len < 22.0:
			var bunting: Node3D = _instantiate_kit("brgy_banderitas")
			bunting.name = "FiestaBanderitas_%d" % b_idx
			_disable_shadows_recursive(bunting)
			# `brgy_banderitas.glb` spans local X in `[-5.900, +5.900]` (`11.800 m`) at local Y = `5.378 m`
			var b_yaw: float = atan2(-span_xz.y, span_xz.x)
			bunting.scale = Vector3(span_len / 11.800, 1.05, 1.0)
			var mid_x: float = (anc_a.x + anc_b.x) * 0.5
			var mid_z: float = (anc_a.z + anc_b.z) * 0.5
			_place_walkthrough(bunting, Vector3(mid_x, y_anchor - 5.378 * 1.05, mid_z), b_yaw)
			# Wrap steel tie-collars + cross-bars on BOTH pole trunks at exact `y_anchor`!
			for end_anc: Vector3 in [Vector3(anc_a.x, y_anchor, anc_a.z), Vector3(anc_b.x, y_anchor, anc_b.z)]:
				dressing_kit.cylinder(end_anc - Vector3(0.0, 0.03, 0.0), 0.17, 0.17, 0.06, 8, collar_steel, collar_steel.darkened(0.2))
				_kit_oriented_box(dressing_kit, Basis(Vector3.UP, b_yaw), end_anc, Vector3(0.10, 0.06, 0.96), collar_steel, collar_steel.darkened(0.18))

	# 3. Pole-to-Pole Trunk Spans (along curved street curbs + eskinitas + perimeter ring, every span <= 46 m!)
	var spans: Array[Vector2i] = [
		# West Feeder Road chain (0..5)
		Vector2i(0, 1), Vector2i(1, 2), Vector2i(2, 3), Vector2i(3, 4), Vector2i(4, 5),
		# Center Avenue chain (6..11)
		Vector2i(6, 7), Vector2i(7, 8), Vector2i(8, 9), Vector2i(9, 10), Vector2i(10, 11),
		# East Feeder Road chain (12..17)
		Vector2i(12, 13), Vector2i(13, 14), Vector2i(14, 15), Vector2i(15, 16), Vector2i(16, 17),
		# Cross-Eskinita & Opposite-Curb Partner links
		Vector2i(4, 18), Vector2i(18, 10), Vector2i(10, 20), Vector2i(20, 16),
		Vector2i(1, 21), Vector2i(21, 7), Vector2i(7, 23), Vector2i(23, 13),
		Vector2i(11, 19), Vector2i(6, 22),
		Vector2i(3, 18), Vector2i(15, 20), Vector2i(2, 21), Vector2i(14, 23),
		# 16-pole Outer Perimeter Ring (24..39)
		Vector2i(24, 25), Vector2i(25, 26), Vector2i(26, 27), Vector2i(27, 28),
		Vector2i(28, 29), Vector2i(29, 30), Vector2i(30, 31), Vector2i(31, 32),
		Vector2i(32, 33), Vector2i(33, 34), Vector2i(34, 35), Vector2i(35, 36),
		Vector2i(36, 37), Vector2i(37, 38), Vector2i(38, 39), Vector2i(39, 24),
		# Radial tie-ins from perimeter ring to the 3 Feeder/Avenue chains
		Vector2i(5, 29), Vector2i(11, 28), Vector2i(19, 27), Vector2i(17, 26),
		Vector2i(0, 34), Vector2i(6, 36), Vector2i(22, 35), Vector2i(12, 37),
		Vector2i(16, 25), Vector2i(4, 30), Vector2i(1, 33), Vector2i(13, 38),
	]

	# Build physically anchored overhead wire mesh AND populate 3D side-wall weatherheads into `dressing_kit`!
	var wire_mesh: ArrayMesh = SpaghettiWires.build_anchored_wire_network(
		pole_pin_sets,
		pole_shaft_tops,
		spans,
		_placed_buildings,
		dressing_kit
	)
	var wire_inst: MeshInstance3D = _add(wire_mesh, Vector3.ZERO)
	wire_inst.name = "SpaghettiWiring"
	wire_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# 4. Batched `BarangayMicroDressing`: 3D Raised Concrete Sidewalks/Plinths + Grounded Courtyard Sampayan +
	#    Interior Trees in 3D Mossy Soil Planter Curbs + Weatherhead Hardware!
	_build_micro_dressing_and_curbs(dressing_kit)


func _build_micro_dressing_and_curbs(kit: LowPoly) -> void:
	# 1. Single Flush Sidewalk-Matched Foundation Plinths under walled buildings (`BUILTIN_SLAB_KITS` sit directly on their `.glb` slab!)
	var curb_top: Color = Color(0.41, 0.39, 0.35)
	var curb_side: Color = Color(0.30, 0.28, 0.25)
	for parcel: Dictionary in _interior_parcels:
		var p_kit: String = String(parcel.get("kit_id", ""))
		if BUILTIN_SLAB_KITS.has(p_kit):
			continue
		var pc: Vector2 = parcel["center"] as Vector2
		var ph: Vector2 = parcel["half"] as Vector2
		var pyaw: float = float(parcel["yaw"])
		var pb: Basis = Basis(Vector3.UP, pyaw)
		kit.set_transform(Transform3D(pb, Vector3(pc.x, 0.005, pc.y)))
		kit.rounded_slab(
			Vector3.ZERO,
			Vector3(ph.x * 2.0 + 0.68, 0.10, ph.y * 2.0 + 0.68),
			0.30,
			curb_side,
			curb_top,
			4
		)
		kit.reset_transform()

	# 2. Grounded Courtyard / Side-Yard `brgy_sampayan_3d` Clotheslines (`Y = 0.0`, standing on built-in T-posts!)
	var courtyard_sampayan: Array[Vector3] = [
		Vector3(-56.2, 0.0, 21.6),
		Vector3(54.8, 0.0, 25.4),
		Vector3(-21.2, 0.0, 23.6),
		Vector3(24.8, 0.0, 22.8),
	]
	var has_sampayan_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_sampayan_3d")
	var cloth_palette: Array[Color] = [
		Color(0.76, 0.26, 0.22),
		Color(0.22, 0.50, 0.72),
		Color(0.80, 0.62, 0.20),
		Color(0.26, 0.58, 0.36),
	]
	var trim_palette: Array[Color] = [Color(0.90, 0.86, 0.78), Color(0.84, 0.64, 0.18)]
	for side_mul: float in [1.0, -1.0]:
		for s_idx: int in courtyard_sampayan.size():
			var base_s: Vector3 = courtyard_sampayan[s_idx]
			var s_pos: Vector3 = Vector3(base_s.x * side_mul, 0.0, base_s.z * side_mul)
			var s_yaw: float = deg_to_rad(24.0 + float(s_idx) * 38.0) + (0.0 if side_mul > 0.0 else PI)
			if has_sampayan_glb:
				var glb_line: Node3D = _instantiate_kit("brgy_sampayan_3d")
				glb_line.name = "GroundedSampayan_%d_%s" % [s_idx, "S" if side_mul > 0.0 else "N"]
				glb_line.scale = Vector3.ONE * 0.98
				_place_walkthrough(glb_line, s_pos, s_yaw)
			else:
				var d_end: Vector3 = Basis(Vector3.UP, s_yaw) * Vector3(0.0, 0.0, 1.45)
				SpaghettiWires.append_grounded_sampayan_with_posts(
					kit,
					s_pos - d_end,
					s_pos + d_end,
					cloth_palette,
					trim_palette,
					Color(0.52, 0.42, 0.26),
					Color(0.66, 0.52, 0.30),
					s_idx + (0 if side_mul > 0.0 else 7)
				)

	# 3. Interior *Tabing-Ilog* & Courtyard Provincial Trees seated inside 3D Mossy Soil Planter Curbs (NO random blue drums!)
	var interior_tree_spots: Array[Vector3] = [
		Vector3(-70.5, 0.0, 6.8),
		Vector3(70.5, 0.0, -6.8),
		Vector3(-68.2, 0.0, -6.4),
		Vector3(68.2, 0.0, 6.4),
		Vector3(-53.2, 0.0, 26.2),
		Vector3(53.2, 0.0, -26.2),
		Vector3(-60.5, 0.0, 48.2),
		Vector3(60.5, 0.0, -48.2),
		Vector3(-27.2, 0.0, 66.2),
		Vector3(27.2, 0.0, -66.2),
		Vector3(27.2, 0.0, 66.2),
		Vector3(-27.2, 0.0, -66.2),
	]
	var tree_ids: Array[String] = ["brgy_banana_clump", "brgy_mango_tree", "brgy_coconut_palm"]
	for t_i: int in interior_tree_spots.size():
		var tp: Vector3 = interior_tree_spots[t_i]
		var tid: String = tree_ids[t_i % tree_ids.size()]
		var t_inst: Node3D = _instantiate_kit(tid)
		t_inst.name = "BarangayTree_%d" % t_i
		t_inst.scale = Vector3.ONE * (0.90 + 0.18 * absf(sin(float(t_i) * 2.1)))
		_place_walkthrough(t_inst, tp, float(t_i) * 1.3)
		_tree_planter_spots.append(tp)

	# Build 3D octagonal mossy stone/CHB soil planter curbs under every interior, plaza, & perimeter tree!
	var planter_wall: Color = Color(0.46, 0.44, 0.40)
	var planter_moss: Color = Color(0.30, 0.40, 0.24)
	var dark_soil: Color = Color(0.24, 0.18, 0.13)
	for p_tree_pos: Vector3 in _tree_planter_spots:
		kit.cylinder(p_tree_pos - Vector3(0.0, 0.04, 0.0), 1.18, 1.12, 0.24, 8, planter_moss, planter_wall)
		kit.cylinder(p_tree_pos + Vector3(0.0, 0.02, 0.0), 0.96, 0.96, 0.19, 8, dark_soil, dark_soil.darkened(0.15))

	var dressing_mesh: ArrayMesh = kit.commit()
	var dressing_mat: StandardMaterial3D = StandardMaterial3D.new()
	dressing_mat.vertex_color_use_as_albedo = true
	dressing_mat.roughness = 0.88
	dressing_mat.next_pass = null
	dressing_mesh.surface_set_material(0, dressing_mat)
	var dressing_inst: MeshInstance3D = _add(dressing_mesh, Vector3.ZERO)
	dressing_inst.name = "BarangayMicroDressing"
	dressing_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


static func _kit_oriented_box(kit: LowPoly, b: Basis, pos: Vector3, sz: Vector3, top_c: Color, side_c: Color) -> void:
	kit.set_transform(Transform3D(b, pos))
	kit.plain_box(Vector3.ZERO, sz, side_c, top_c)
	kit.reset_transform()


func _apply_architectural_materials(root: Node3D) -> void:
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
			var bmat: BaseMaterial3D = base_mat as BaseMaterial3D
			if bmat == null:
				continue
			var key: int = bmat.get_instance_id()
			if not _arch_mat_cache.has(key):
				var copy_mat: StandardMaterial3D = null
				if bmat is StandardMaterial3D:
					copy_mat = bmat.duplicate() as StandardMaterial3D
				else:
					copy_mat = StandardMaterial3D.new()
					copy_mat.albedo_color = bmat.albedo_color
					copy_mat.vertex_color_use_as_albedo = bmat.vertex_color_use_as_albedo
					copy_mat.vertex_color_is_srgb = bmat.vertex_color_is_srgb
					copy_mat.cull_mode = bmat.cull_mode
					copy_mat.transparency = bmat.transparency
					copy_mat.alpha_scissor_threshold = bmat.alpha_scissor_threshold
					copy_mat.normal_enabled = bmat.normal_enabled
					copy_mat.normal_texture = bmat.normal_texture
					copy_mat.normal_scale = bmat.normal_scale
					if bmat is ORMMaterial3D:
						var orm_mat: ORMMaterial3D = bmat as ORMMaterial3D
						var orm_tex: Texture2D = orm_mat.orm_texture
						if orm_tex != null:
							copy_mat.ao_enabled = true
							copy_mat.ao_texture = orm_tex
							copy_mat.ao_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
							copy_mat.ao_light_affect = bmat.ao_light_affect
							copy_mat.roughness = 1.0
							copy_mat.roughness_texture = orm_tex
							copy_mat.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_GREEN
							copy_mat.metallic = 1.0
							copy_mat.metallic_texture = orm_tex
							copy_mat.metallic_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_BLUE
				copy_mat.albedo_texture = bmat.albedo_texture
				if bmat.normal_enabled and bmat.normal_texture != null:
					copy_mat.normal_enabled = true
					copy_mat.normal_texture = bmat.normal_texture
					copy_mat.normal_scale = bmat.normal_scale
				elif bmat.albedo_texture != null:
					var tex_path: String = bmat.albedo_texture.resource_path
					if tex_path.ends_with("BarangayAtlas.jpg") and ResourceLoader.exists(BARANGAY_NORMAL_PATH):
						copy_mat.normal_enabled = true
						copy_mat.normal_texture = load(BARANGAY_NORMAL_PATH) as Texture2D
						copy_mat.normal_scale = 0.82
					elif tex_path.ends_with("ProvincePropsAtlas.jpg") and ResourceLoader.exists(PROVINCE_NORMAL_PATH):
						copy_mat.normal_enabled = true
						copy_mat.normal_texture = load(PROVINCE_NORMAL_PATH) as Texture2D
						copy_mat.normal_scale = 0.82
				copy_mat.diffuse_mode = BaseMaterial3D.DIFFUSE_BURLEY
				copy_mat.specular_mode = BaseMaterial3D.SPECULAR_SCHLICK_GGX
				if copy_mat.roughness_texture != null:
					copy_mat.roughness = clampf(copy_mat.roughness, 0.85, 1.0)
					copy_mat.metallic = clampf(copy_mat.metallic, 0.0, 1.0) if copy_mat.metallic_texture != null else 0.0
				else:
					copy_mat.metallic = 0.0
					copy_mat.roughness = clampf(bmat.roughness, 0.72, 0.95)
				copy_mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
				copy_mat.next_pass = null
				_arch_mat_cache[key] = copy_mat
			mi.set_surface_override_material(s_idx, _arch_mat_cache[key])


static func _disable_shadows_recursive(root: Node3D) -> void:
	var geos: Array[Node] = root.find_children("*", "GeometryInstance3D", true, false)
	if root is GeometryInstance3D:
		geos.append(root)
	for node: Node in geos:
		var gi: GeometryInstance3D = node as GeometryInstance3D
		if gi != null:
			gi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


static func _resolve_kit_path(kit_id: String) -> String:
	var primary: String = BARANGAY_DIR % kit_id
	if ResourceLoader.exists(primary):
		return primary
	if KIT_FALLBACK_MAP.has(kit_id):
		var fallback: String = BARANGAY_DIR % KIT_FALLBACK_MAP[kit_id]
		if ResourceLoader.exists(fallback):
			return fallback
	if PROCEDURAL_ONLY_KITS.has(kit_id):
		return ""
	var cover: String = BARANGAY_DIR % "brgy_cover_wall"
	return cover if ResourceLoader.exists(cover) else ""


func _instantiate_kit(kit_id: String) -> Node3D:
	if not _scene_cache.has(kit_id):
		var path: String = _resolve_kit_path(kit_id)
		if path != "":
			_scene_cache[kit_id] = load(path) as PackedScene
	if _scene_cache.has(kit_id) and _scene_cache[kit_id] != null:
		var inst: Node3D = _scene_cache[kit_id].instantiate() as Node3D
		_apply_architectural_materials(inst)
		return inst
	return _build_procedural_kit_fallback(kit_id)


func _build_procedural_kit_fallback(kit_id: String) -> Node3D:
	var kit: LowPoly = LowPoly.new()
	if kit_id == "brgy_wooden_hoop":
		var coco_wood: Color = Color(0.46, 0.32, 0.19)
		var board_white: Color = Color(0.90, 0.88, 0.82)
		var rim_orange: Color = Color(0.84, 0.34, 0.14)
		kit.rounded_slab(Vector3(0.0, 0.18, 0.0), Vector3(0.85, 0.36, 0.85), 0.18, Color(0.44, 0.43, 0.40), Color(0.52, 0.51, 0.48), 4)
		kit.cylinder(Vector3(0.0, 0.36, 0.0), 0.12, 0.10, 2.95, 8, coco_wood, coco_wood.darkened(0.15))
		_kit_oriented_box(kit, Basis(Vector3.RIGHT, -0.35), Vector3(0.0, 3.05, 0.35), Vector3(0.12, 0.12, 0.82), coco_wood, coco_wood)
		kit.box(Vector3(0.0, 3.22, 0.72), Vector3(1.28, 0.88, 0.06), board_white, board_white)
		kit.box(Vector3(0.0, 3.12, 0.76), Vector3(0.46, 0.36, 0.03), rim_orange, rim_orange)
		kit.cylinder(Vector3(0.0, 2.92, 0.98), 0.23, 0.23, 0.045, 10, rim_orange, rim_orange)
	elif kit_id == "brgy_chb_wall":
		var chb_grey: Color = Color(0.54, 0.53, 0.50)
		var moss_green: Color = Color(0.32, 0.44, 0.25)
		var pillar_col: Color = Color(0.60, 0.58, 0.54)
		kit.box(Vector3(0.0, 0.28, 0.0), Vector3(6.0, 0.56, 0.34), moss_green, chb_grey)
		kit.box(Vector3(0.0, 1.32, 0.0), Vector3(6.0, 1.54, 0.28), chb_grey, pillar_col)
		kit.box(Vector3(0.0, 2.15, 0.0), Vector3(6.1, 0.14, 0.36), pillar_col, moss_green)
		for px: float in [-2.85, 0.0, 2.85]:
			kit.box(Vector3(px, 1.15, 0.0), Vector3(0.38, 2.30, 0.40), pillar_col, pillar_col.lightened(0.08))
	elif kit_id == "brgy_mango_tree":
		var bark: Color = Color(0.34, 0.25, 0.18)
		var leaf_a: Color = Color(0.26, 0.48, 0.22)
		var leaf_b: Color = Color(0.34, 0.56, 0.24)
		kit.cylinder(Vector3.ZERO, 0.38, 0.26, 3.4, 8, bark, bark.darkened(0.15))
		for b_i: int in 3:
			var byaw: float = TAU * float(b_i) / 3.0 + 0.4
			var b_dir: Basis = Basis(Vector3.UP, byaw) * Basis(Vector3.RIGHT, 0.52)
			_kit_oriented_box(kit, b_dir, Vector3(cos(byaw) * 0.55, 3.55, sin(byaw) * 0.55), Vector3(0.22, 1.45, 0.22), bark, bark)
		kit.sphere(Vector3(0.0, 4.9, 0.0), 2.35, leaf_b, 9, 6)
		kit.sphere(Vector3(1.25, 4.35, 0.75), 1.75, leaf_a, 8, 5)
		kit.sphere(Vector3(-1.15, 4.45, -0.65), 1.65, leaf_b.darkened(0.06), 8, 5)
		kit.sphere(Vector3(-0.35, 5.45, 0.85), 1.45, leaf_b.lightened(0.05), 8, 5)
	elif kit_id == "brgy_coconut_palm":
		var palm_bark: Color = Color(0.44, 0.36, 0.27)
		var frond_green: Color = Color(0.28, 0.52, 0.22)
		var coco_nut: Color = Color(0.42, 0.50, 0.18)
		var cur_p: Vector3 = Vector3.ZERO
		for seg_k: int in 6:
			var next_p: Vector3 = cur_p + Vector3(0.14 * float(seg_k) * 0.35, 1.08, 0.06 * float(seg_k) * 0.25)
			var r0: float = lerpf(0.26, 0.15, float(seg_k) / 6.0)
			var r1: float = lerpf(0.26, 0.15, float(seg_k + 1) / 6.0)
			SpaghettiWires.append_3d_tapered_tube(kit, cur_p, next_p, r0, r1, 7, palm_bark)
			cur_p = next_p
		for nut_i: int in 4:
			var na: float = TAU * float(nut_i) / 4.0
			kit.sphere(cur_p + Vector3(cos(na) * 0.22, -0.12, sin(na) * 0.22), 0.14, coco_nut, 6, 4)
		for f_idx: int in 8:
			var fyaw: float = TAU * float(f_idx) / 8.0
			var f_b: Basis = Basis(Vector3.UP, fyaw) * Basis(Vector3.RIGHT, -0.38)
			var f_mid: Vector3 = cur_p + Basis(Vector3.UP, fyaw) * Vector3(0.0, 0.22, 1.25)
			_kit_oriented_box(kit, f_b, f_mid, Vector3(0.52, 0.08, 2.45), frond_green, frond_green.darkened(0.18))
	elif kit_id == "brgy_banana_clump":
		var stem_green: Color = Color(0.36, 0.52, 0.24)
		var leaf_bright: Color = Color(0.34, 0.58, 0.22)
		for st_k: int in 3:
			var off: Vector3 = Vector3(float(st_k - 1) * 0.42, 0.0, (0.28 if st_k == 1 else -0.18))
			var h_st: float = 2.65 + 0.35 * float(st_k % 2)
			kit.cylinder(off, 0.16, 0.12, h_st, 7, stem_green, stem_green.darkened(0.15))
			for lf_i: int in 5:
				var lyaw: float = TAU * float(lf_i) / 5.0 + float(st_k) * 0.5
				var l_b: Basis = Basis(Vector3.UP, lyaw) * Basis(Vector3.RIGHT, -0.44)
				var l_p: Vector3 = off + Vector3(0.0, h_st, 0.0) + Basis(Vector3.UP, lyaw) * Vector3(0.0, 0.14, 0.98)
				_kit_oriented_box(kit, l_b, l_p, Vector3(0.56, 0.06, 1.95), leaf_bright, stem_green.darkened(0.12))
	elif kit_id == "brgy_powerup_stand":
		var table_wood: Color = Color(0.54, 0.38, 0.22)
		var jug_orange: Color = Color(0.90, 0.54, 0.16)
		var canopy_c: Color = Color(0.86, 0.28, 0.22)
		kit.rounded_slab(Vector3(0.0, 0.06, 0.0), Vector3(1.55, 0.12, 1.25), 0.22, Color(0.44, 0.42, 0.39), Color(0.54, 0.52, 0.48), 4)
		kit.box(Vector3(0.0, 0.52, 0.0), Vector3(1.22, 0.80, 0.72), table_wood, table_wood.lightened(0.12))
		kit.cylinder(Vector3(-0.26, 0.92, 0.0), 0.18, 0.18, 0.42, 8, jug_orange, Color(0.94, 0.92, 0.88))
		kit.box(Vector3(0.24, 0.98, 0.0), Vector3(0.38, 0.12, 0.32), Color(0.84, 0.68, 0.24), Color(0.92, 0.78, 0.34))
		for post_x: float in [-0.54, 0.54]:
			kit.cylinder(Vector3(post_x, 0.92, -0.26), 0.035, 0.035, 1.15, 6, table_wood.darkened(0.15), table_wood)
		kit.gable(Vector3(0.0, 2.02, 0.0), 1.36, 0.96, 0.34, canopy_c, 0.12)
	else:
		kit.box(Vector3(0.0, 2.6, 0.0), Vector3(7.2, 5.2, 7.2), Color(0.58, 0.48, 0.36), Color(0.64, 0.32, 0.24))

	var fallback_root: Node3D = Node3D.new()
	var mi: MeshInstance3D = MeshInstance3D.new()
	var mesh: ArrayMesh = kit.commit()
	var mat: StandardMaterial3D = StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.roughness = 0.88
	mat.next_pass = null
	mesh.surface_set_material(0, mat)
	mi.mesh = mesh
	fallback_root.add_child(mi)
	return fallback_root


func _kit_bounds(kit_id: String) -> AABB:
	if not _bounds_cache.has(kit_id):
		var temp: Node3D = _instantiate_kit(kit_id)
		var box: AABB = AABB()
		var first: bool = true
		var meshes: Array[Node] = temp.find_children("*", "MeshInstance3D", true, false)
		if temp is MeshInstance3D:
			meshes.append(temp)
		for node: Node in meshes:
			var mi: MeshInstance3D = node as MeshInstance3D
			if mi.mesh == null:
				continue
			var part: AABB = mi.transform * mi.mesh.get_aabb()
			box = part if first else box.merge(part)
			first = false
		temp.free()
		_bounds_cache[kit_id] = box
	return _bounds_cache[kit_id]


func _add_post(center: Vector2) -> Node3D:
	var post: Node3D = Node3D.new()
	post.name = "Post"
	for id: String in [POST_BLUE, POST_RED]:
		var model: Node3D = _instantiate_kit(id)
		var nat_h: float = maxf(_kit_bounds(id).size.y, 0.1)
		var s: float = post_height() / nat_h
		model.scale = Vector3(s * layout.post_width_share, s, s * layout.post_width_share)
		model.name = id
		model.rotation.y = BLUE_POST_TURN if id == POST_BLUE else 0.0
		post.add_child(model)
	_dress_post(post, true, false)
	_place_walkthrough(post, Vector3(center.x, 0.0, center.y), 0.0)
	return post


func _dress_post(post: Node3D, own: bool, flipped: bool) -> void:
	(post.get_node(POST_BLUE) as Node3D).visible = own
	(post.get_node(POST_RED) as Node3D).visible = not own
	post.rotation.y = PI if flipped else 0.0


func _turn_posts(own_side: int) -> void:
	for side: int in _posts:
		_dress_post(_posts[side], side == own_side, own_side == MapLayout.SIDE_ENEMY)


func _add_marked_zone(rect: Rect2, corner: float, color: Color, seed_offset: float, under_count: bool = false) -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = CHALK_SHADER
	material.set_shader_parameter("chalk", Color(color, CHALK_ALPHA))
	material.set_shader_parameter("breaks", ZONE_CHALK_BREAKS)
	material.set_shader_parameter("seed", seed_offset)
	if under_count:
		material.render_priority = RESCUE_OUTLINE_PRIORITY
	_add_chalk_loop(rect, corner, material)
	return material


func _add_count(center: Vector2) -> Label3D:
	var label: Label3D = Label3D.new()
	label.name = "RescueCount"
	label.font = ThemeDB.fallback_font
	label.font_size = RESCUE_COUNT_FONT
	label.pixel_size = RESCUE_COUNT_HEIGHT / float(RESCUE_COUNT_FONT)
	label.scale.y = 1.0 / sin(deg_to_rad(COUNT_PITCH_DEGREES))
	label.modulate = Color(1.0, 1.0, 1.0, COUNT_ALPHA)
	label.outline_size = 0
	label.shaded = false
	label.double_sided = false
	label.alpha_cut = Label3D.ALPHA_CUT_DISABLED
	label.render_priority = RESCUE_COUNT_PRIORITY
	label.rotation.x = -PI * 0.5
	label.position = Vector3(center.x, RESCUE_COUNT_LIFT, center.y)
	label.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	label.visible = false
	_geometry.add_child(label)
	return label


func _turn_count(label: Label3D, side: int, own_side: int) -> void:
	var toward: float = 0.0 if own_side == MapLayout.SIDE_OWN else PI
	label.rotation.y = toward + float(side * own_side) * COUNT_SPIN


func _show_count(label: Label3D, left: float) -> void:
	label.visible = left > 0.0
	if label.visible:
		var seconds: String = str(ceili(left))
		if label.text != seconds:
			label.text = seconds


static func rounded_outline(rect: Rect2, radius: float) -> Array[Vector2]:
	var points: Array[Vector2] = []
	var r: float = minf(radius, minf(rect.size.x, rect.size.y) * 0.48)
	var corners: Array[Vector2] = [
		Vector2(rect.end.x - r, rect.position.y + r),
		Vector2(rect.end.x - r, rect.end.y - r),
		Vector2(rect.position.x + r, rect.end.y - r),
		Vector2(rect.position.x + r, rect.position.y + r),
	]
	for corner: int in 4:
		for step: int in ZONE_CORNER_STEPS + 1:
			var angle: float = -PI * 0.5 + (float(corner) + float(step) / float(ZONE_CORNER_STEPS)) * PI * 0.5
			points.append(corners[corner] + Vector2(cos(angle), sin(angle)) * r)
	return points


func _add_chalk_loop(rect: Rect2, corner: float, material: ShaderMaterial) -> void:
	var half: float = ZONE_CHALK_WIDTH * 0.5
	var path: Array[Vector2] = rounded_outline(rect.grow(-half), maxf(corner - half, 0.0))
	var count: int = path.size()
	var along: Array[float] = [0.0]
	for i: int in count:
		along.append(along[i] + path[i].distance_to(path[(i + 1) % count]))
	material.set_shader_parameter("length_m", along[count])
	var tool: SurfaceTool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_normal(Vector3.UP)
	var height: float = RING_HEIGHT + 0.006
	for i: int in count:
		var j: int = (i + 1) % count
		var edge: Array[Vector3] = []
		var end_indices: PackedInt32Array = PackedInt32Array([i, j])
		var end_alongs: PackedFloat32Array = PackedFloat32Array([along[i], along[i + 1]])
		for step_k: int in 2:
			var at: int = end_indices[step_k]
			var direction: Vector2 = (path[(at + 1) % count] - path[(at - 1 + count) % count]).normalized()
			var side: Vector2 = Vector2(direction.y, -direction.x) * half
			var u: float = end_alongs[step_k] / maxf(along[count], 0.001)
			edge.append(Vector3(path[at].x + side.x, u, path[at].y + side.y))
			edge.append(Vector3(path[at].x - side.x, u, path[at].y - side.y))
		for corner_index: int in [0, 2, 3, 0, 3, 1]:
			var vertex: Vector3 = edge[corner_index]
			tool.set_uv(Vector2(vertex.y, 0.0 if corner_index % 2 == 0 else 1.0))
			tool.add_vertex(Vector3(vertex.x, height, vertex.z))
	var mesh: ArrayMesh = tool.commit()
	mesh.surface_set_material(0, material)
	_add(mesh, Vector3.ZERO).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _add(mesh: Mesh, at: Vector3, yaw: float = 0.0) -> MeshInstance3D:
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.mesh = mesh
	instance.position = at
	instance.rotation.y = yaw
	_geometry.add_child(instance)
	return instance


func _place_walkthrough(model: Node3D, at: Vector3, yaw: float = 0.0) -> Node3D:
	model.position = at
	model.rotation.y = yaw
	_geometry.add_child(model)
	return model


func _add_prop(model: Node3D, at: Vector3, yaw: float = 0.0) -> Node3D:
	model.position = at
	model.rotation.y = yaw
	_geometry.add_child(model)
	_occluders.append(model)
	_occluder_boxes.clear()
	return model


func hide_between(eye: Vector3, feet: Vector3, height: float, clear: float = 0.0) -> void:
	if _occluder_boxes.size() != _occluders.size():
		_occluder_boxes.clear()
		for prop: Node3D in _occluders:
			_occluder_boxes.append(world_box(prop))
	var targets: Array[Vector3] = [feet + Vector3.UP * height * 0.15, feet + Vector3.UP * height * 0.5, feet + Vector3.UP * height]
	var hidden: Array[Node3D] = []
	for i: int in _occluders.size():
		var box: AABB = _occluder_boxes[i]
		if (eye.clamp(box.position, box.end) - eye).length() < clear:
			hidden.append(_occluders[i])
			continue
		for target: Vector3 in targets:
			if segment_hits_box(eye, target, box):
				hidden.append(_occluders[i])
				break
	for prop: Node3D in _hidden_props:
		if not hidden.has(prop):
			prop.visible = true
	for prop: Node3D in hidden:
		prop.visible = false
	_hidden_props = hidden


func hidden_props() -> Array[Node3D]:
	return _hidden_props


static func world_box(root: Node3D) -> AABB:
	var box: AABB = AABB()
	var first: bool = true
	var parts: Array[Node] = root.find_children("*", "VisualInstance3D", true, false)
	if root is VisualInstance3D:
		parts.append(root)
	for node: Node in parts:
		var shape: VisualInstance3D = node as VisualInstance3D
		var part: AABB = shape.global_transform * shape.get_aabb()
		box = part if first else box.merge(part)
		first = false
	return box


static func segment_hits_box(from: Vector3, to: Vector3, box: AABB) -> bool:
	var way: Vector3 = to - from
	var enter: float = 0.0
	var leave: float = 1.0
	for axis: int in 3:
		var lo: float = box.position[axis]
		var hi: float = box.end[axis]
		if absf(way[axis]) < 1e-9:
			if from[axis] < lo or from[axis] > hi:
				return false
			continue
		var t0: float = (lo - from[axis]) / way[axis]
		var t1: float = (hi - from[axis]) / way[axis]
		enter = maxf(enter, minf(t0, t1))
		leave = minf(leave, maxf(t0, t1))
		if enter > leave:
			return false
	return true


func _setup_camera() -> void:
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.keep_aspect = Camera3D.KEEP_HEIGHT
	_camera.size = layout.overview_size
	_camera.rotation_degrees = Vector3(-90.0, 0.0, 0.0)
	_camera.position = Vector3(0.0, layout.overview_height, 0.0)
	_camera.environment = make_environment()


func show_back_button(visible_now: bool) -> void:
	_back_button.visible = visible_now


func _on_back_pressed() -> void:
	get_tree().change_scene_to_file("res://scenes/ui/title_screen.tscn")
