class_name StreetMap
extends Node3D
## Circular 90s Philippine Provincial Barangay 3D map (view only; reads MapLayout, holds no sim rules).
## Features 3 wider main North-South pathways (`X = -34, 0, +34`), 2 connecting East-West alleyways
## (`Z = +-26`), a walkable shallow mid canal (`Z = 0`, NO mid bridge) with sloped *Tabing-Ilog*
## stone wading ramps, dynamic perimeter mossy CHB hollow-block walls + provincial trees + houses,
## authentic 90s province landmarks (Jeepneys, TODA Tricycle Terminal, Nena's Sari-Sari Store,
## Karinderya, Barberya, Vulcanizing Shop, Wooden Basketball Post, Power-Up Stands, Pole Posters,
## NO ENTRY & BAWAL UMIHI DITO signs), 3D volumetric *sampayan* clothes, and batched `SpaghettiWires`.

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY

const GROUND_SHADER: Shader = preload("res://shaders/barangay_ground.gdshader")
const STREET_SHADER: Shader = GROUND_SHADER
const WATER_SHADER: Shader = preload("res://shaders/estero_water.gdshader")
const SKY_SHADER: Shader = preload("res://shaders/tropical_sky.gdshader")
const CHALK_SHADER: Shader = preload("res://shaders/chalk_line.gdshader")
const SIGNBOARD_FONT: Font = preload("res://assets/fonts/brand/KalyeahDisplay-Regular.ttf")
const INK_OUTLINE_PATH: String = "res://shaders/arcane_ink_outline.gdshader"

const BARANGAY_DIR: String = "res://assets/models/barangay/%s.glb"

## Lively, warm, soft sunlit 90s provincial daytime lighting (zero white blowout).
const SUN_ANGLES: Vector3 = Vector3(-44.0, 38.0, 0.0)
const SUN_COLOR: Color = Color(1.0, 0.94, 0.83)
const SUN_ENERGY: float = 0.84
const SHADOW_DISTANCE: float = 185.0
const FILL_ANGLES: Vector3 = Vector3(-36.0, 218.0, 0.0)
const FILL_COLOR: Color = Color(0.66, 0.82, 0.98)
const FILL_ENERGY: float = 0.30
const AMBIENT_COLOR: Color = Color(0.95, 0.91, 0.85)
const AMBIENT_ENERGY: float = 0.46

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
	&"house_two_storey": "brgy_house_two_storey",
	&"shophouse_row": "brgy_shophouse_row",
	&"palengke_stall": "brgy_palengke_stall",
	&"waiting_shed": "brgy_waiting_shed",
	&"toda_shed": "brgy_toda_shed",
	&"jeepney": "brgy_jeepney",
	&"tricycle": "brgy_tricycle",
	&"cover_wall": "brgy_cover_wall",
	&"bench": "brgy_cover_wall",
	&"tent": "brgy_palengke_stall",
}

const KIT_FALLBACK_MAP: Dictionary[String, String] = {
	"brgy_tabi_cluster_a": "brgy_shophouse_row",
	"brgy_tabi_cluster_b": "brgy_house_two_storey",
	"brgy_poso_well": "brgy_cover_wall",
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

static var _scene_cache: Dictionary[String, PackedScene] = {}
static var _bounds_cache: Dictionary[String, AABB] = {}
static var _ink_outline_mat: Material = null
static var _outlined_mat_cache: Dictionary[int, Material] = {}

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
	var env: Environment = Environment.new()
	var sky_mat: ShaderMaterial = ShaderMaterial.new()
	sky_mat.shader = SKY_SHADER
	var sky: Sky = Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = AMBIENT_COLOR
	env.ambient_light_energy = AMBIENT_ENERGY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.0
	env.glow_enabled = false
	env.fog_enabled = true
	env.fog_light_color = Color(0.94, 0.88, 0.78)
	env.fog_light_energy = 0.28
	env.fog_density = 0.00085
	env.fog_sky_affect = 0.12
	return env


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
	sun.rotation_degrees = SUN_ANGLES
	sun.light_color = SUN_COLOR
	sun.light_energy = SUN_ENERGY
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	sun.directional_shadow_max_distance = SHADOW_DISTANCE
	sun.shadow_opacity = 0.66
	sun.shadow_bias = 0.04
	sun.shadow_normal_bias = 1.4
	if parent.get_node_or_null("SkyFill") == null:
		var fill: DirectionalLight3D = DirectionalLight3D.new()
		fill.name = "SkyFill"
		fill.rotation_degrees = FILL_ANGLES
		fill.light_color = FILL_COLOR
		fill.light_energy = FILL_ENERGY
		fill.shadow_enabled = false
		parent.add_child(fill)


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
	plane.subdivide_width = 56
	plane.subdivide_depth = 56
	var mat: ShaderMaterial = ShaderMaterial.new()
	mat.shader = GROUND_SHADER
	if layout.arena_radius > 0.0:
		mat.set_shader_parameter("arena_radius", layout.arena_radius)
	mat.set_shader_parameter("arena_half", Vector2(layout.lane_width * 0.5, layout.lane_length * 0.5))
	mat.set_shader_parameter("own_base_center", layout.base_center(MapLayout.SIDE_OWN))
	mat.set_shader_parameter("enemy_base_center", layout.base_center(MapLayout.SIDE_ENEMY))
	mat.set_shader_parameter("wear", 0.35 if diorama else 1.0)
	plane.material = mat
	var ground: MeshInstance3D = _add(plane, Vector3.ZERO)
	ground.name = "BarangayGround"
	ground.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


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
	water_mat.set_shader_parameter("creek_half_width", 3.7)
	water_mat.set_shader_parameter("shallow_color", Color(0.22, 0.44, 0.37, 0.80))
	water_mat.set_shader_parameter("deep_color", Color(0.12, 0.29, 0.28, 0.86))

	if not water_rects.is_empty():
		for rect: Rect2 in water_rects:
			var custom_plane: PlaneMesh = PlaneMesh.new()
			custom_plane.size = Vector2(rect.size.x, maxf(rect.size.y - 0.30, 1.0))
			custom_plane.subdivide_width = maxi(4, int(rect.size.x * 0.5))
			custom_plane.subdivide_depth = 6
			custom_plane.material = water_mat
			var c_pt: Vector2 = rect.get_center()
			var cw_mesh: MeshInstance3D = _add(custom_plane, Vector3(c_pt.x, 0.055, c_pt.y))
			cw_mesh.name = "EsteroWater"
			cw_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		return

	# Walkable Shallow Mid Canal (`Z in [-3.7, +3.7]`, NO mid bridge at `X = 0`!)
	var canal_len: float = layout.arena_radius * 2.0 + 4.0
	var water_plane: PlaneMesh = PlaneMesh.new()
	water_plane.size = Vector2(canal_len, 7.4)
	water_plane.subdivide_width = 72
	water_plane.subdivide_depth = 8
	water_plane.material = water_mat
	var w_mesh: MeshInstance3D = _add(water_plane, Vector3(0.0, 0.052, 0.0))
	w_mesh.name = "EsteroWater"
	w_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# Sloped *Tabing-Ilog* Stone & Mossy Concrete Riverbank + 3 Walkable Wading Ramps (`X = -34, 0, +34`)
	var bank_kit: LowPoly = LowPoly.new()
	var stone_lit: Color = Color(0.49, 0.47, 0.43)
	var stone_side: Color = Color(0.36, 0.35, 0.32)
	var moss_top: Color = Color(0.30, 0.42, 0.24)
	var cobble_warm: Color = Color(0.46, 0.42, 0.36)
	var cobble_cool: Color = Color(0.38, 0.40, 0.38)

	# Non-ford mossy sloped concrete & river-stone embankments (leaving wide open wading ramps at X = -34, 0, +34!)
	var bank_spans: Array[Vector2] = [
		Vector2(-82.5, -40.8),
		Vector2(-27.2, -7.2),
		Vector2(7.2, 27.2),
		Vector2(40.8, 82.5),
	]
	for span_x: Vector2 in bank_spans:
		var seg_w: float = span_x.y - span_x.x
		var mid_x: float = (span_x.x + span_x.y) * 0.5
		for bank_sign: float in [-1.0, 1.0]:
			var slope_basis: Basis = Basis(Vector3.RIGHT, bank_sign * 0.16)
			_kit_oriented_box(
				bank_kit,
				slope_basis,
				Vector3(mid_x, -0.04, bank_sign * 4.05),
				Vector3(seg_w, 0.34, 1.45),
				moss_top if span_x.x < 0.0 else stone_lit,
				stone_side
			)
			# Low beveled river coping with mossy patches
			bank_kit.rounded_slab(
				Vector3(mid_x, 0.08, bank_sign * 4.55),
				Vector3(seg_w, 0.18, 0.68),
				0.18,
				stone_side,
				moss_top if int(absf(mid_x)) % 2 == 0 else stone_lit,
				4
			)
			# Natural *Tabing-Ilog* river boulders & flat washing stones (*batuhan*) along the West & mid banks
			var rock_count: int = maxi(3, int(seg_w / 5.5))
			for r_i: int in rock_count:
				var rx: float = lerpf(span_x.x + 1.2, span_x.y - 1.2, (float(r_i) + 0.5) / float(rock_count))
				var rz: float = bank_sign * (3.55 + 0.32 * sin(float(r_i) * 2.7 + mid_x))
				var rad_r: float = 0.38 + 0.16 * absf(cos(float(r_i) * 3.1 + bank_sign))
				var r_col: Color = cobble_warm if r_i % 2 == 0 else cobble_cool
				bank_kit.rounded_slab(Vector3(rx, 0.05, rz), Vector3(rad_r * 2.1, 0.16, rad_r * 1.6), rad_r * 0.6, r_col.darkened(0.15), r_col, 4)

	# 3 Wide Sloped Stone Wading Ramps at West (-34m), Center (0m — NO bridge!), and East (+34m) Pathways
	for ford_x: float in [-34.0, 0.0, 34.0]:
		var ford_w: float = 13.0 if is_zero_approx(ford_x) else 12.2
		for bank_sign: float in [-1.0, 1.0]:
			var ramp_basis: Basis = Basis(Vector3.RIGHT, bank_sign * 0.055)
			_kit_oriented_box(
				bank_kit,
				ramp_basis,
				Vector3(ford_x, 0.015, bank_sign * 4.35),
				Vector3(ford_w, 0.14, 2.2),
				stone_lit,
				stone_side
			)
		# Submerged/ankle-level river flagstone ford markers guiding players straight through the shallow water
		var stone_cols: int = 4
		for col_i: int in stone_cols:
			var sx: float = ford_x + (float(col_i) - 1.5) * 2.6
			for step_k: int in 3:
				var sz: float = (float(step_k) - 1.0) * 2.0 + 0.25 * sin(float(col_i * 3 + step_k))
				var fw: float = 1.45 + 0.25 * absf(cos(float(col_i + step_k)))
				bank_kit.rounded_slab(
					Vector3(sx, 0.032, sz),
					Vector3(fw, 0.06, 1.15),
					0.32,
					stone_side,
					cobble_warm if (col_i + step_k) % 2 == 0 else stone_lit,
					4
				)

	var bank_mesh: ArrayMesh = bank_kit.commit()
	var bank_mat: StandardMaterial3D = StandardMaterial3D.new()
	bank_mat.vertex_color_use_as_albedo = true
	bank_mat.roughness = 0.90
	var outline: Material = _outline_material()
	if outline != null:
		bank_mat.next_pass = outline
	bank_mesh.surface_set_material(0, bank_mat)
	var bank_inst: MeshInstance3D = _add(bank_mesh, Vector3.ZERO)
	bank_inst.name = "TabingIlogBanks"

	# Scenic outer river-mouth wooden footbridges ONLY outside the playable pathways (`X = +-81.2m`)
	for mouth_sign: float in [-1.0, 1.0]:
		var bridge_id: String = "brgy_estero_bridge" if ResourceLoader.exists(BARANGAY_DIR % "brgy_estero_bridge") else "brgy_footbridge"
		var bridge: Node3D = _instantiate_kit(bridge_id)
		bridge.name = "RiverMouthFootbridgeWest" if mouth_sign < 0.0 else "RiverMouthFootbridgeEast"
		var nat: Vector3 = _kit_bounds(bridge_id).size
		bridge.scale = Vector3(4.2 / maxf(nat.x, 0.1), 0.85, 8.8 / maxf(nat.z, 0.1))
		_place_walkthrough(bridge, Vector3(mouth_sign * 81.2, 0.0, 0.0), 0.0)


func _build_district_landmarks() -> void:
	# 1. South (+Z) Own Base Landmark: Open-Air Covered Barangay Basketball Court at (0, +64)
	var own_c: Vector2 = layout.base_center(MapLayout.SIDE_OWN)
	var court: Node3D = _instantiate_kit("brgy_covered_court")
	court.name = "CoveredBasketballCourt"
	court.scale = Vector3(1.18, 1.06, 1.12)
	_place_walkthrough(court, Vector3(own_c.x, 0.0, own_c.y), 0.0)

	# 2. North (-Z) Enemy Base Landmark: Municipal Barangay Hall framing the -Z Base Plaza
	var enemy_c: Vector2 = layout.base_center(MapLayout.SIDE_ENEMY)
	var hall_id: String = "brgy_barangay_hall" if ResourceLoader.exists(BARANGAY_DIR % "brgy_barangay_hall") else "brgy_hall_plaza"
	var hall: Node3D = _instantiate_kit(hall_id)
	hall.name = "BarangayHall"
	hall.scale = Vector3.ONE * 1.14
	var hall_z: float = -(layout.arena_radius + 5.6) if layout.arena_radius > 0.0 else (-layout.lane_length * 0.5 - 7.5)
	_place_walkthrough(hall, Vector3(enemy_c.x, 0.0, hall_z), 0.0)

	# 3. Makeshift 90s Provincial Wooden Basketball Posts (`brgy_wooden_hoop`) court-side at both plazas
	for hoop_sign: float in [1.0, -1.0]:
		var hoop: Node3D = _instantiate_kit("brgy_wooden_hoop")
		hoop.name = "WoodenBasketballPostSouth" if hoop_sign > 0.0 else "WoodenBasketballPostNorth"
		hoop.scale = Vector3.ONE * 0.96
		var hoop_yaw: float = -PI * 0.5 if hoop_sign > 0.0 else PI * 0.5
		_place_walkthrough(hoop, Vector3(hoop_sign * 10.6, 0.0, hoop_sign * 56.4), hoop_yaw)

	# 4. NW & SE Rescue Patio Waiting Sheds framing the 5-slot tagged-player sidewalks
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var r_rect: Rect2 = layout.rescue_rect(side)
		var tag_mid: Vector2 = layout.tagged_place(side, 2)
		var shed: Node3D = _instantiate_kit("brgy_waiting_shed")
		shed.name = "RescueWaitingShedOwn" if side == MapLayout.SIDE_OWN else "RescueWaitingShedEnemy"
		shed.scale = Vector3(1.28, 1.06, 1.15)
		var outward_x: float = -float(side) * 2.2
		var sx: float = tag_mid.x + outward_x
		var yaw: float = PI * 0.5 if side == MapLayout.SIDE_OWN else -PI * 0.5
		_place_walkthrough(shed, Vector3(sx, 0.0, r_rect.get_center().y), yaw)

	# 5. 3 Small Map Power-Up Objective Stands (`brgy_powerup_stand`: Palamig, Teks, Turon) beside `layout.powerup_spots`
	for pu_idx: int in layout.powerup_spots.size():
		var pu_spot: Vector2 = layout.powerup_spots[pu_idx]
		var stand: Node3D = _instantiate_kit("brgy_powerup_stand")
		stand.name = "PowerupStand_%d" % pu_idx
		stand.scale = Vector3.ONE * 0.92
		var z_off: float = 2.05 if pu_idx != 1 else -2.15
		var stand_yaw: float = PI if z_off > 0.0 else 0.0
		_place_walkthrough(stand, Vector3(pu_spot.x + (1.65 if pu_idx == 1 else 0.0), 0.0, pu_spot.y + z_off), stand_yaw)

	# 6. Iconic 90s Provincial Jeepneys (`brgy_jeepney`) parked in East/West TODA bays & perimeter road gates
	var jeep_spots: Array[Vector4] = [
		Vector4(69.5, 14.5, -PI * 0.38, 1.0),
		Vector4(-69.5, -14.5, PI * 0.62, 1.0),
		Vector4(34.0, 86.8, PI * 0.5, 1.0),
		Vector4(-34.0, -86.8, -PI * 0.5, 1.0),
	]
	for j_idx: int in jeep_spots.size():
		var j_spec: Vector4 = jeep_spots[j_idx]
		var jeep: Node3D = _instantiate_kit("brgy_jeepney")
		jeep.name = "ProvincialJeepney_%d" % j_idx
		jeep.scale = Vector3.ONE * 0.96
		_place_walkthrough(jeep, Vector3(j_spec.x, 0.0, j_spec.y), j_spec.z)

	# 7. TODA Tricycle Terminal Canopies & Queued Tricycles (`brgy_toda_shed` + `brgy_tricycle`)
	var has_toda_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_toda_shed")
	for t_sign: float in [1.0, -1.0]:
		var toda_canopy: Node3D = _instantiate_kit("brgy_toda_shed")
		toda_canopy.name = "TodaTerminalEast" if t_sign > 0.0 else "TodaTerminalWest"
		toda_canopy.scale = Vector3.ONE * 0.95
		_place_walkthrough(toda_canopy, Vector3(t_sign * 53.5, 0.0, t_sign * 21.6), PI if t_sign > 0.0 else 0.0)
		if not has_toda_glb:
			_add_signboard_label(
				Vector3(t_sign * 40.48, 2.85, t_sign * 14.2),
				-t_sign * PI * 0.5,
				"BRGY. TODA TERMINAL",
				Color(0.98, 0.86, 0.24)
			)

	var trike_spots: Array[Vector3] = [
		Vector3(53.4, 0.0, 5.8),
		Vector3(57.4, 0.0, 5.8),
		Vector3(-53.4, 0.0, -5.8),
		Vector3(-57.4, 0.0, -5.8),
		Vector3(15.6, 0.0, 49.4),
		Vector3(-15.6, 0.0, -49.4),
	]
	for idx: int in trike_spots.size():
		var spot: Vector3 = trike_spots[idx]
		var trike: Node3D = _instantiate_kit("brgy_tricycle")
		trike.name = "TodaTricycle_%d" % idx
		trike.scale = Vector3.ONE * 0.94
		var t_yaw: float = PI * 0.5 if spot.x < 0.0 else -PI * 0.5
		_place_walkthrough(trike, spot, t_yaw)

	# 8. Barrio Poso hand-pumps & Sorbetes carts beside the plazas
	for sign_f: float in [-1.0, 1.0]:
		_place_poso_landmark(Vector3(sign_f * 8.4, 0.0, sign_f * 6.2), 0.0 if sign_f > 0.0 else PI)
		var cart: Node3D = _instantiate_kit("brgy_sorbetes_cart")
		cart.name = "SorbetesCartSouth" if sign_f > 0.0 else "SorbetesCartNorth"
		cart.scale = Vector3.ONE * 0.96
		_place_walkthrough(cart, Vector3(sign_f * 14.8, 0.0, sign_f * 63.0), -sign_f * PI * 0.5)

	# 9. Authentic 90s Shopfront Vignettes along the 3 Main Pathways:
	#    Nena's Sari-Sari Store, Karinderya/Eatery, Barbershop (Barberya), Vulcanizing Shop, & NO ENTRY Gates!
	_build_90s_shopfront_vignettes_and_gates()

	# 10. Fiesta Banderitas high overhead (`Y >= 5.2m`) across pathway entrances
	var banderita_spans: Array[Vector3] = [
		Vector3(-34.0, 0.6, 26.0),
		Vector3(0.0, 0.6, 48.0),
		Vector3(34.0, 0.6, -26.0),
		Vector3(0.0, 0.6, -48.0),
	]
	for span_at: Vector3 in banderita_spans:
		var bunting: Node3D = _instantiate_kit("brgy_banderitas")
		bunting.scale = Vector3(0.95, 1.02, 0.95)
		_place_walkthrough(bunting, span_at, 0.0)


func _build_90s_shopfront_vignettes_and_gates() -> void:
	var kit: LowPoly = LowPoly.new()
	var wood_warm: Color = Color(0.48, 0.34, 0.21)
	var wood_dark: Color = Color(0.32, 0.22, 0.14)
	var steel_col: Color = Color(0.56, 0.58, 0.60)
	var tire_black: Color = Color(0.14, 0.14, 0.16)
	var tire_rim: Color = Color(0.46, 0.47, 0.49)
	var barber_red: Color = Color(0.78, 0.20, 0.18)
	var barber_blue: Color = Color(0.18, 0.36, 0.72)
	var barber_white: Color = Color(0.90, 0.88, 0.84)
	var tank_orange: Color = Color(0.76, 0.38, 0.16)
	var cooler_blue: Color = Color(0.18, 0.44, 0.68)
	var sign_cream: Color = Color(0.88, 0.83, 0.70)
	var sign_red: Color = Color(0.74, 0.18, 0.16)

	for s_mul: float in [1.0, -1.0]:
		var inward_w_yaw: float = -s_mul * PI * 0.5
		var inward_e_yaw: float = s_mul * PI * 0.5

		# A. Tindahan ni Aling Nena (Nena's Sari-Sari Store) at `(-27.55, 12.6)` facing West `-34m` Pathway
		var nena_pos: Vector3 = Vector3(-27.55 * s_mul, 0.0, 12.6 * s_mul)
		var nena_b: Basis = Basis(Vector3.UP, inward_w_yaw)
		_kit_oriented_box(kit, nena_b, nena_pos + nena_b * Vector3(0.0, 0.44, 0.42), Vector3(2.2, 0.46, 0.42), wood_warm, wood_dark)
		_kit_oriented_box(kit, nena_b, nena_pos + nena_b * Vector3(-1.35, 0.38, 0.46), Vector3(0.72, 0.68, 0.52), cooler_blue, cooler_blue.darkened(0.18))
		_kit_oriented_box(kit, nena_b, nena_pos + nena_b * Vector3(0.0, 2.72, 0.16), Vector3(3.1, 0.62, 0.08), sign_red, sign_cream)
		_add_signboard_label(nena_pos + nena_b * Vector3(0.0, 2.72, 0.22), inward_w_yaw, "TINDAHAN NI ALING NENA", Color(0.72, 0.14, 0.12))

		# B. Karinderya / Lutong Bahay Eatery at `(-40.48, 14.2)` facing East into West `-34m` Tabing-Ilog Pathway
		var karin_pos: Vector3 = Vector3(-40.48 * s_mul, 0.0, 14.2 * s_mul)
		var karin_b: Basis = Basis(Vector3.UP, inward_e_yaw)
		_kit_oriented_box(kit, karin_b, karin_pos + karin_b * Vector3(0.0, 0.48, 0.48), Vector3(2.8, 0.92, 0.64), sign_cream, wood_warm)
		for pot_i: int in 3:
			var pot_p: Vector3 = karin_pos + karin_b * Vector3((float(pot_i) - 1.0) * 0.78, 0.96, 0.48)
			kit.cylinder(pot_p, 0.22, 0.24, 0.26, 8, steel_col, steel_col.lightened(0.12))
		_kit_oriented_box(kit, karin_b, karin_pos + karin_b * Vector3(0.0, 0.42, 1.18), Vector3(2.4, 0.44, 0.36), wood_warm, wood_dark)
		_kit_oriented_box(kit, karin_b, karin_pos + karin_b * Vector3(0.0, 2.78, 0.18), Vector3(3.4, 0.62, 0.08), wood_dark, Color(0.92, 0.74, 0.22))
		_add_signboard_label(karin_pos + karin_b * Vector3(0.0, 2.78, 0.24), inward_e_yaw, "KARINDERYA LUTONG BAHAY", Color(0.18, 0.12, 0.08))

		# C. Barangayan Barbershop ("BARBERYA") + 3D Striped Barber Pole at `(27.55, 11.2)` facing East `+34m` Pathway
		var barb_pos: Vector3 = Vector3(27.55 * s_mul, 0.0, 11.2 * s_mul)
		var barb_b: Basis = Basis(Vector3.UP, inward_e_yaw)
		var pole_base: Vector3 = barb_pos + barb_b * Vector3(-1.25, 1.45, 0.34)
		_kit_oriented_box(kit, barb_b, pole_base + barb_b * Vector3(0.0, 0.42, -0.18), Vector3(0.08, 0.08, 0.32), steel_col, steel_col)
		kit.sphere(pole_base + Vector3(0.0, 0.92, 0.0), 0.13, steel_col, 8, 5)
		kit.sphere(pole_base + Vector3(0.0, 0.0, 0.0), 0.13, steel_col, 8, 5)
		for band_i: int in 6:
			var band_c: Color = barber_red if band_i % 3 == 0 else (barber_white if band_i % 3 == 1 else barber_blue)
			kit.cylinder(pole_base + Vector3(0.0, 0.06 + float(band_i) * 0.13, 0.0), 0.11, 0.11, 0.13, 8, band_c, band_c)
		_kit_oriented_box(kit, barb_b, barb_pos + barb_b * Vector3(0.2, 0.42, 0.44), Vector3(1.8, 0.45, 0.40), wood_warm, wood_dark)
		_kit_oriented_box(kit, barb_b, barb_pos + barb_b * Vector3(0.0, 2.72, 0.16), Vector3(2.8, 0.58, 0.08), barber_blue, barber_white)
		_add_signboard_label(barb_pos + barb_b * Vector3(0.0, 2.72, 0.22), inward_e_yaw, "BARBERYA GUPIT BINATA", barber_blue.darkened(0.2))

		# D. Vulcanizing Shop (3D Stacked & Hanging Rubber Tires + Steel Air Compressor Tank) at `(27.55, 17.6)`
		var vulc_pos: Vector3 = Vector3(27.55 * s_mul, 0.0, 17.6 * s_mul)
		var vulc_b: Basis = Basis(Vector3.UP, inward_e_yaw)
		# Stacked rubber tires on sidewalk apron
		for t_stack: int in 3:
			var tp: Vector3 = vulc_pos + vulc_b * Vector3(-1.15, float(t_stack) * 0.26, 0.55)
			kit.cylinder(tp, 0.42, 0.42, 0.25, 10, tire_black, tire_rim)
		# Upright leaning spare tire + horizontal painted air receiver tank
		kit.set_transform(Transform3D(vulc_b * Basis(Vector3.RIGHT, PI * 0.5), vulc_pos + vulc_b * Vector3(-0.35, 0.42, 0.52)))
		kit.cylinder(Vector3(0.0, -0.12, 0.0), 0.42, 0.42, 0.24, 10, tire_black, tire_rim)
		kit.reset_transform()
		kit.set_transform(Transform3D(vulc_b * Basis(Vector3.FORWARD, PI * 0.5), vulc_pos + vulc_b * Vector3(0.85, 0.46, 0.52)))
		kit.cylinder(Vector3(0.0, -0.55, 0.0), 0.24, 0.24, 1.10, 8, tank_orange, tank_orange.darkened(0.2))
		kit.reset_transform()
		_kit_oriented_box(kit, vulc_b, vulc_pos + vulc_b * Vector3(0.0, 2.72, 0.16), Vector3(3.0, 0.58, 0.08), tire_black, Color(0.92, 0.80, 0.20))
		_add_signboard_label(vulc_pos + vulc_b * Vector3(0.0, 2.72, 0.22), inward_e_yaw, "VULCANIZING SHOP", Color(0.12, 0.12, 0.14))

	# E. Perimeter "NO ENTRY" Provincial Street Barriers (`r > 84.4m` at road mouths)
	var gate_specs: Array[Vector3] = [
		Vector3(-34.0, 77.8, PI),
		Vector3(34.0, 77.8, PI),
		Vector3(-34.0, -77.8, 0.0),
		Vector3(34.0, -77.8, 0.0),
		Vector3(-80.6, 26.0, PI * 0.5),
		Vector3(80.6, -26.0, -PI * 0.5),
	]
	for g_spec: Vector3 in gate_specs:
		var g_pos: Vector3 = Vector3(g_spec.x, 0.0, g_spec.y)
		var g_yaw: float = g_spec.z
		var g_b: Basis = Basis(Vector3.UP, g_yaw)
		for leg_s: float in [-1.4, 1.4]:
			_kit_oriented_box(kit, g_b, g_pos + g_b * Vector3(leg_s, 0.55, 0.0), Vector3(0.12, 1.10, 0.65), wood_dark, wood_warm)
		_kit_oriented_box(kit, g_b, g_pos + g_b * Vector3(0.0, 0.92, 0.0), Vector3(3.4, 0.24, 0.10), sign_red, barber_white)
		_kit_oriented_box(kit, g_b, g_pos + g_b * Vector3(0.0, 1.52, 0.04), Vector3(1.55, 0.52, 0.08), barber_white, sign_red)
		_add_signboard_label(g_pos + g_b * Vector3(0.0, 1.52, 0.10), g_yaw, "NO ENTRY", Color(0.98, 0.96, 0.92))

	var shop_mesh: ArrayMesh = kit.commit()
	var shop_mat: StandardMaterial3D = StandardMaterial3D.new()
	shop_mat.vertex_color_use_as_albedo = true
	shop_mat.roughness = 0.86
	var outline: Material = _outline_material()
	if outline != null:
		shop_mat.next_pass = outline
	shop_mesh.surface_set_material(0, shop_mat)
	var shop_inst: MeshInstance3D = _add(shop_mesh, Vector3.ZERO)
	shop_inst.name = "Province90sShopfrontsAndGates"


func _add_signboard_label(pos: Vector3, yaw: float, text_str: String, tint: Color) -> void:
	var lbl: Label3D = Label3D.new()
	lbl.text = text_str
	lbl.font = SIGNBOARD_FONT
	lbl.font_size = 44
	lbl.pixel_size = 0.0055
	lbl.modulate = tint
	lbl.outline_size = 4
	lbl.outline_modulate = Color(0.96, 0.94, 0.88, 0.65) if tint.v < 0.45 else Color(0.12, 0.09, 0.08, 0.85)
	lbl.shaded = true
	lbl.double_sided = false
	lbl.position = pos
	lbl.rotation.y = yaw
	lbl.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_geometry.add_child(lbl)


func _place_poso_landmark(at: Vector3, yaw: float) -> void:
	var poso_glb: String = BARANGAY_DIR % "brgy_poso_well"
	if ResourceLoader.exists(poso_glb):
		var model: Node3D = _instantiate_kit("brgy_poso_well")
		model.name = "BarrioPoso"
		model.scale = Vector3.ONE * 1.05
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
	var outline: Material = _outline_material()
	if outline != null:
		mat.next_pass = outline
	mesh.surface_set_material(0, mat)
	var inst: MeshInstance3D = _add(mesh, at, yaw)
	inst.name = "BarrioPoso"


func _add_obstacle(index: int) -> void:
	var rect: Rect2 = layout.obstacle_rects()[index]
	var prop_key: StringName = layout.obstacle_prop(index)
	var kit_id: String = OBSTACLE_KIT_MAP.get(prop_key, "brgy_cover_wall")
	var model: Node3D = _instantiate_kit(kit_id)
	var bounds: AABB = _kit_bounds(kit_id)
	var nat: Vector3 = bounds.size

	var center: Vector2 = rect.get_center()
	var turn_quarter: bool = (rect.size.y > rect.size.x * 1.18) and (kit_id != "brgy_jeepney" and kit_id != "brgy_tricycle")
	var yaw: float = 0.0
	if kit_id == "brgy_jeepney" or kit_id == "brgy_tricycle":
		yaw = PI if center.y > 0.0 else 0.0
	elif turn_quarter:
		yaw = PI * 0.5 if center.x < 0.0 else -PI * 0.5
	else:
		yaw = PI if center.y > 0.0 else 0.0
		if index % 3 == 1:
			yaw += PI

	var jitter_deg: float = (1.0 if prop_key == &"cover_wall" else 2.2) * sin(float(index) * 4.13 + 1.17)
	yaw += deg_to_rad(jitter_deg)
	var jitter_off: Vector3 = Vector3(
		0.12 * cos(float(index) * 3.71 + 0.4),
		0.0,
		0.14 * sin(float(index) * 2.89 + 0.9)
	)

	var target_w: float = rect.size.y if turn_quarter else rect.size.x
	var target_d: float = rect.size.x if turn_quarter else rect.size.y
	var overhang: float = 0.96 if prop_key == &"cover_wall" else 0.995
	var sx: float = (target_w * overhang) / maxf(nat.x, 0.1)
	var sz: float = (target_d * overhang) / maxf(nat.z, 0.1)
	var h_var: float = 0.86 + 0.26 * absf(sin(float(index) * 2.73 + 0.65))
	var sy: float = clampf((sx + sz) * 0.40 * h_var, 0.78, 1.24)
	if prop_key == &"cover_wall":
		sy = cover_height() / maxf(nat.y, 0.1)
	model.scale = Vector3(sx, sy, sz)

	var b_center: Vector3 = bounds.get_center() * model.scale
	var shift: Vector3 = Basis(Vector3.UP, yaw) * Vector3(b_center.x, 0.0, b_center.z)
	_add_prop(model, Vector3(center.x, 0.0, center.y) + jitter_off - shift, yaw)


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

	# 1. Dynamic Inner Boundary (`r = 85.4..92.0m`): alternates Mossy CHB Hollow-Block Walls + Lush Provincial Trees
	#    with Cozy Sunlit Provincial Houses, plus stenciled "BAWAL UMIHI DITO" compound walls!
	var inner_slots: int = 48
	var wall_r: float = ring_radius + 1.45
	var house_inner_r: float = ring_radius + 6.4
	var wall_chord: float = (TAU * wall_r / float(inner_slots)) * 1.06
	var house_chord: float = (TAU * house_inner_r / float(inner_slots)) * 1.38
	var r_own: Rect2 = layout.rescue_rect(MapLayout.SIDE_OWN)
	var r_ene: Rect2 = layout.rescue_rect(MapLayout.SIDE_ENEMY)

	for i: int in inner_slots:
		var angle: float = TAU * float(i) / float(inner_slots)
		var dir: Vector2 = Vector2(cos(angle), sin(angle))
		var p_wall: Vector2 = dir * wall_r
		var in_canal_mouth: bool = absf(p_wall.y) < 5.2
		var in_own_shed: bool = p_wall.x < 0.0 and p_wall.y >= r_own.position.y - 1.5 and p_wall.y <= r_own.end.y + 1.5
		var in_ene_shed: bool = p_wall.x > 0.0 and p_wall.y >= r_ene.position.y - 1.5 and p_wall.y <= r_ene.end.y + 1.5
		var in_road_gate: bool = (absf(absf(p_wall.x) - 34.0) < 4.5 and absf(p_wall.y) > 70.0) or (absf(absf(p_wall.y) - 26.0) < 4.2 and absf(p_wall.x) > 74.0)
		var inward_yaw: float = atan2(-dir.x, -dir.y)

		if not (in_canal_mouth or in_own_shed or in_ene_shed or in_road_gate):
			# Alternate CHB hollow-block garden walls + tropical trees vs street-fronting provincial houses!
			if i % 3 != 1:
				var chb: Node3D = _instantiate_kit("brgy_chb_wall")
				chb.name = "ChbPerimeterWall_%d" % i
				var chb_nat: Vector3 = _kit_bounds("brgy_chb_wall").size
				chb.scale = Vector3(clampf(wall_chord / maxf(chb_nat.x, 2.0), 0.95, 1.85), clampf(2.25 / maxf(chb_nat.y, 1.0), 0.85, 1.25), 1.0)
				_place_walkthrough(chb, Vector3(p_wall.x, 0.0, p_wall.y), inward_yaw)
				if i % 6 == 0 and not ResourceLoader.exists(BARANGAY_DIR % "brgy_chb_wall"):
					_add_signboard_label(
						Vector3(p_wall.x, 1.48, p_wall.y) - Vector3(dir.x, 0.0, dir.y) * 0.28,
						inward_yaw,
						"BAWAL UMIHI DITO",
						Color(0.76, 0.16, 0.14)
					)
				# Lush Philippine provincial tree rising just behind the mossy CHB wall (`r > 87.5m`)
				var t_id: String = tree_kits[i % tree_kits.size()]
				var tree_node: Node3D = _instantiate_kit(t_id)
				tree_node.name = "PerimeterTree_%d" % i
				var t_scale: float = 0.92 + 0.28 * absf(sin(float(i) * 2.3))
				tree_node.scale = Vector3.ONE * t_scale
				var p_tree: Vector2 = dir * (ring_radius + 4.2 + 2.4 * absf(cos(float(i) * 1.9)))
				_place_walkthrough(tree_node, Vector3(p_tree.x, 0.0, p_tree.y), float(i) * 1.1)

		# Provincial house tier (`r = 90.0..97.0m`, strictly `> arena_radius = 84.0m`)
		if i % 4 != 2:
			var local_r: float = house_inner_r + 1.8 * sin(float(i) * 2.9 + 0.4)
			if dir.y < -0.93:
				local_r += 7.5
			elif in_canal_mouth or in_own_shed or in_ene_shed:
				local_r += 4.6
			elif i % 3 != 1:
				local_r += 2.6
			var pos2: Vector2 = dir * local_r
			var kit_id: String = perimeter_kits[i % perimeter_kits.size()]
			var house: Node3D = _instantiate_kit(kit_id)
			var nat: Vector3 = _kit_bounds(kit_id).size
			var sx: float = house_chord / maxf(nat.x, 4.0)
			var sz: float = clampf((9.6 + 1.8 * absf(cos(float(i) * 1.7))) / maxf(nat.z, 4.0), 0.84, 1.28)
			var sy: float = clampf(0.80 + 0.34 * absf(sin(float(i) * 2.13 + 0.3)), 0.80, 1.18)
			house.scale = Vector3(sx, sy, sz)
			var h_yaw: float = inward_yaw + deg_to_rad(3.2 * sin(float(i) * 3.7 + 0.9))
			_add_prop(house, Vector3(pos2.x, 0.0, pos2.y), h_yaw)
			var row_key: int = -1 if pos2.x < 0.0 else 1
			house_rows[row_key].append(house)
			far_props.append(kit_id)

	# 2. Outer Provincial Barangay Roof & Coconut/Mango Grove Tier (`r = ring_radius + 18..25m`)
	var outer_count: int = 40
	var outer_r: float = ring_radius + 19.2
	var outer_chord: float = (TAU * outer_r / float(outer_count)) * 1.12
	for j: int in outer_count:
		var angle_o: float = TAU * (float(j) + 0.5) / float(outer_count) + 0.018 * cos(float(j) * 3.1)
		var dir_o: Vector2 = Vector2(cos(angle_o), sin(angle_o))
		var r_o: float = outer_r + 2.8 * sin(float(j) * 3.3 + 1.1)
		var pos_o: Vector2 = dir_o * r_o
		var kit_o: String = perimeter_kits[(j + 2) % perimeter_kits.size()]
		var bldg: Node3D = _instantiate_kit(kit_o)
		var nat_o: Vector3 = _kit_bounds(kit_o).size
		var sx_o: float = outer_chord / maxf(nat_o.x, 4.0)
		var sy_o: float = 0.88 + 0.32 * absf(cos(float(j) * 2.3 + 0.7))
		bldg.scale = Vector3(sx_o, sy_o, clampf(10.8 / maxf(nat_o.z, 4.0), 0.86, 1.28))
		var yaw_o: float = atan2(-dir_o.x, -dir_o.y) + deg_to_rad(4.8 * cos(float(j) * 2.9))
		_add_prop(bldg, Vector3(pos_o.x, 0.0, pos_o.y), yaw_o)
		var row_o: int = -1 if pos_o.x < 0.0 else 1
		house_rows[row_o].append(bldg)
		far_props.append(kit_o)

		if j % 2 == 0:
			var grove_id: String = tree_kits[(j + 1) % tree_kits.size()]
			var grove_tree: Node3D = _instantiate_kit(grove_id)
			grove_tree.scale = Vector3.ONE * (1.05 + 0.25 * absf(sin(float(j) * 1.7)))
			var pos_g: Vector2 = dir_o * (r_o + 8.5)
			_place_walkthrough(grove_tree, Vector3(pos_g.x, 0.0, pos_g.y), float(j) * 1.4)


func _build_spaghetti_wiring_and_poles() -> void:
	if layout.arena_radius <= 0.0 and layout.lane_width < 80.0:
		return
	var ring_r: float = (layout.arena_radius if layout.arena_radius > 0.0 else 84.0) - 0.35

	# 1. Primary Leaning Electric Posts (`brgy_electric_post` / `brgy_meralco_post`) tucked cleanly onto
	#    building sidewalk corners along the 3 Main Pathways (`X = -34, 0, +34`) & `83.65m` perimeter!
	var inner_poles: Array[Vector3] = [
		Vector3(-40.8, 0.0, 7.2),
		Vector3(-7.2, 0.0, 7.2),
		Vector3(40.8, 0.0, 7.2),
		Vector3(40.8, 0.0, -7.2),
		Vector3(7.2, 0.0, -7.2),
		Vector3(-40.8, 0.0, -7.2),
		Vector3(-27.2, 0.0, 21.4),
		Vector3(27.2, 0.0, 21.4),
		Vector3(27.2, 0.0, -21.4),
		Vector3(-27.2, 0.0, -21.4),
		Vector3(-18.2, 0.0, 51.8),
		Vector3(18.2, 0.0, -51.8),
	]
	var outer_poles: Array[Vector3] = []
	for i: int in 12:
		var ang: float = TAU * (float(i) + 0.35) / 12.0
		outer_poles.append(Vector3(cos(ang) * ring_r, 0.0, sin(ang) * ring_r))

	# 16 Pathway/Alley Corner Utility Masts (24..39) flush against building corners (never inside lanes!)
	var corner_half_poles: Array[Vector3] = [
		Vector3(-27.3, 0.0, 14.0),
		Vector3(-7.1, 0.0, 20.6),
		Vector3(-27.3, 0.0, 38.6),
		Vector3(-40.7, 0.0, 14.4),
		Vector3(-7.1, 0.0, 38.6),
		Vector3(7.1, 0.0, 38.6),
		Vector3(27.3, 0.0, 14.4),
		Vector3(40.7, 0.0, 38.6),
	]
	var eskinita_poles: Array[Vector3] = []
	for ep: Vector3 in corner_half_poles:
		eskinita_poles.append(ep)
	for ep_neg: Vector3 in corner_half_poles:
		eskinita_poles.append(Vector3(-ep_neg.x, 0.0, -ep_neg.z))

	var all_poles: Array[Vector3] = []
	all_poles.append_array(inner_poles)
	all_poles.append_array(outer_poles)
	all_poles.append_array(eskinita_poles)

	var pole_kit_id: String = "brgy_electric_post" if ResourceLoader.exists(BARANGAY_DIR % "brgy_electric_post") else "brgy_meralco_post"
	var has_poster_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_poster_cluster")
	var pole_tops: Array[Vector3] = []
	var nat_pole_h: float = maxf(_kit_bounds(pole_kit_id).size.y, 6.0)
	var pole_scale_y: float = 9.0 / nat_pole_h
	var alley_pole_scale_y: float = 6.8 / nat_pole_h

	for idx: int in all_poles.size():
		var spot: Vector3 = all_poles[idx]
		var is_alley_mast: bool = idx >= 24
		var pole: Node3D = _instantiate_kit(pole_kit_id)
		pole.name = "ElectricPole_%d" % idx
		if is_alley_mast:
			pole.scale = Vector3(0.66, alley_pole_scale_y, 0.66)
		else:
			pole.scale = Vector3(0.86, pole_scale_y, 0.86)
		var yaw: float = (PI * 0.5 if spot.x < 0.0 else -PI * 0.5) if is_alley_mast else atan2(-spot.x, -spot.z)
		var lean_amp: float = 3.2 if is_alley_mast else 2.2
		var lean_x: float = deg_to_rad(lean_amp * sin(float(idx) * 2.1 + 0.4))
		var lean_z: float = deg_to_rad(lean_amp * cos(float(idx) * 1.7 - 0.3))
		pole.position = spot
		pole.rotation = Vector3(lean_x, yaw, lean_z)
		_geometry.add_child(pole)

		if idx < 16 and has_poster_glb:
			var poster_node: Node3D = _instantiate_kit("brgy_poster_cluster")
			poster_node.name = "PolePosterCluster_%d" % idx
			_place_walkthrough(poster_node, spot, yaw)

		var anchor_h: float = 6.25 if is_alley_mast else 8.20
		var top_offset: Vector3 = pole.transform.basis.orthonormalized() * Vector3(0.0, anchor_h, 0.0)
		pole_tops.append(spot + top_offset)

	var spans: Array[Vector2i] = [
		Vector2i(12, 13), Vector2i(13, 14), Vector2i(14, 15), Vector2i(15, 16),
		Vector2i(16, 17), Vector2i(17, 18), Vector2i(18, 19), Vector2i(19, 20),
		Vector2i(20, 21), Vector2i(21, 22), Vector2i(22, 23), Vector2i(23, 12),
		Vector2i(0, 1), Vector2i(1, 2), Vector2i(3, 4), Vector2i(4, 5),
		Vector2i(0, 5), Vector2i(1, 4), Vector2i(2, 3),
		Vector2i(0, 6), Vector2i(1, 6), Vector2i(1, 7), Vector2i(2, 7),
		Vector2i(5, 9), Vector2i(4, 9), Vector2i(4, 8), Vector2i(3, 8),
		Vector2i(6, 10), Vector2i(7, 10), Vector2i(8, 11), Vector2i(9, 11),
		Vector2i(12, 2), Vector2i(14, 7), Vector2i(15, 10), Vector2i(17, 6),
		Vector2i(18, 0), Vector2i(20, 9), Vector2i(21, 11), Vector2i(23, 8),
		Vector2i(0, 27), Vector2i(27, 24), Vector2i(27, 6),
		Vector2i(1, 24), Vector2i(24, 25), Vector2i(25, 6), Vector2i(6, 26), Vector2i(26, 10),
		Vector2i(25, 28), Vector2i(28, 10), Vector2i(28, 29), Vector2i(29, 7),
		Vector2i(1, 30), Vector2i(30, 7), Vector2i(30, 31), Vector2i(2, 31),
		Vector2i(3, 35), Vector2i(35, 32), Vector2i(35, 8),
		Vector2i(4, 32), Vector2i(32, 33), Vector2i(33, 8), Vector2i(8, 34), Vector2i(34, 11),
		Vector2i(33, 36), Vector2i(36, 11), Vector2i(36, 37), Vector2i(37, 9),
		Vector2i(4, 38), Vector2i(38, 9), Vector2i(38, 39), Vector2i(5, 39),
	]

	var alley_corridors: Array[Vector4] = [
		Vector4(-40.6, -27.4, 9.0, 20.0),
		Vector4(-7.0, 7.0, 9.2, 20.0),
		Vector4(27.4, 40.6, 9.0, 20.0),
		Vector4(-40.6, -27.4, 32.2, 45.2),
		Vector4(-7.0, 7.0, 32.2, 45.2),
		Vector4(27.4, 40.6, 32.2, 45.2),
	]

	var wire_mesh: ArrayMesh = SpaghettiWires.build_wire_network_mesh(
		pole_tops,
		spans,
		alley_corridors,
		layout.obstacle_rects(),
		ring_r
	)
	var wire_inst: MeshInstance3D = _add(wire_mesh, Vector3.ZERO)
	wire_inst.name = "SpaghettiWiring"
	wire_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	# 2. Batched `BarangayMicroDressing`: True 3D Volumetric Sampayan Clotheslines + Pole Election/Malabanan
	#    Poster Boards + Interior Tabing-Ilog Trees & Barrio Water Drums!
	_build_micro_dressing_and_volumetric_sampayan(inner_poles)


func _build_micro_dressing_and_volumetric_sampayan(poster_poles: Array[Vector3]) -> void:
	var kit: LowPoly = LowPoly.new()

	# 1. Election ("VOTE!") & Siphoning Service ("MALABANAN SIPSIP POSO NEGRO") 3D notices on poles
	if not ResourceLoader.exists(BARANGAY_DIR % "brgy_poster_cluster"):
		SpaghettiWires.append_pole_posters(kit, poster_poles)
		for p_idx: int in range(0, poster_poles.size(), 2):
			var p_spot: Vector3 = poster_poles[p_idx]
			var f_yaw: float = atan2(-p_spot.x, -p_spot.z)
			_add_signboard_label(
				p_spot + Vector3(0.0, 2.12, 0.0) + Basis(Vector3.UP, f_yaw) * Vector3(0.0, 0.0, 0.23),
				f_yaw,
				"MALABANAN\nSIPSIP POSO",
				Color(0.10, 0.20, 0.48)
			)
			var v_yaw: float = f_yaw + PI * 0.55
			_add_signboard_label(
				p_spot + Vector3(0.0, 3.06, 0.0) + Basis(Vector3.UP, v_yaw) * Vector3(0.0, 0.0, 0.22),
				v_yaw,
				"VOTE!",
				Color(0.98, 0.94, 0.84)
			)

	# 2. True 3D Volumetric Hanging Clothes (`brgy_sampayan_3d.glb` when exported; `SpaghettiWires` fallback otherwise)
	var sampayan_spans: PackedVector3Array = PackedVector3Array([
		Vector3(-55.6, 5.15, 11.2), Vector3(-53.4, 5.30, 11.8),
		Vector3(52.4, 5.10, 12.0), Vector3(54.6, 5.25, 11.4),
		Vector3(-39.6, 5.15, 10.6), Vector3(-39.6, 5.20, 14.0),
		Vector3(28.4, 5.15, 13.8), Vector3(28.4, 5.20, 17.2),
		Vector3(-39.6, 5.10, 35.6), Vector3(-39.6, 5.18, 39.0),
		Vector3(28.4, 5.10, 36.6), Vector3(28.4, 5.18, 40.0),
		Vector3(-26.8, 5.20, 21.0), Vector3(-23.2, 5.30, 21.0),
		Vector3(23.2, 5.25, 21.0), Vector3(26.8, 5.15, 21.0),
	])
	var cloth_palette: Array[Color] = [
		Color(0.76, 0.26, 0.22),
		Color(0.22, 0.50, 0.72),
		Color(0.80, 0.62, 0.20),
		Color(0.26, 0.58, 0.36),
		Color(0.68, 0.36, 0.56),
		Color(0.82, 0.78, 0.70),
	]
	var trim_palette: Array[Color] = [
		Color(0.90, 0.86, 0.78),
		Color(0.84, 0.64, 0.18),
		Color(0.18, 0.28, 0.46),
		Color(0.74, 0.24, 0.20),
	]
	var cord_col: Color = Color(0.52, 0.42, 0.26)
	var pin_col: Color = Color(0.66, 0.52, 0.30)
	var has_sampayan_glb: bool = ResourceLoader.exists(BARANGAY_DIR % "brgy_sampayan_3d")
	var pair_count: int = sampayan_spans.size() / 2

	for side_mul: float in [1.0, -1.0]:
		for s_idx: int in pair_count:
			var a_pt: Vector3 = sampayan_spans[s_idx * 2] * Vector3(side_mul, 1.0, side_mul)
			var b_pt: Vector3 = sampayan_spans[s_idx * 2 + 1] * Vector3(side_mul, 1.0, side_mul)
			var d_vec: Vector3 = b_pt - a_pt
			var span_len: float = d_vec.length()
			if has_sampayan_glb:
				var glb_line: Node3D = _instantiate_kit("brgy_sampayan_3d")
				glb_line.name = "VolumetricSampayan_%d_%s" % [s_idx, "S" if side_mul > 0.0 else "N"]
				var nat_sz: Vector3 = _kit_bounds("brgy_sampayan_3d").size
				glb_line.scale = Vector3(1.0, 1.0, clampf(span_len / maxf(nat_sz.z, 1.0), 0.82, 1.28))
				var mid_pt: Vector3 = (a_pt + b_pt) * 0.5
				mid_pt.y = maxf(minf(a_pt.y, b_pt.y) - 0.45, 4.65)
				_place_walkthrough(glb_line, mid_pt, atan2(d_vec.x, d_vec.z))
				continue
			SpaghettiWires.append_volumetric_sampayan_span(
				kit,
				a_pt,
				b_pt,
				cloth_palette,
				trim_palette,
				cord_col,
				pin_col,
				s_idx + (0 if side_mul > 0.0 else 11)
			)

	# 3. Interior *Tabing-Ilog* Riverbank & Courtyard Banana / Mango / Coconut Trees (clear of all 3 pathways!)
	var interior_tree_spots: Array[Vector3] = [
		Vector3(-70.5, 0.0, 5.8),
		Vector3(70.5, 0.0, -5.8),
		Vector3(-68.2, 0.0, -5.8),
		Vector3(68.2, 0.0, 5.8),
		Vector3(-54.8, 0.0, 6.2),
		Vector3(54.8, 0.0, -6.2),
		Vector3(-60.5, 0.0, 47.6),
		Vector3(60.5, 0.0, -47.6),
		Vector3(-25.8, 0.0, 66.5),
		Vector3(25.8, 0.0, -66.5),
		Vector3(25.8, 0.0, 66.5),
		Vector3(-25.8, 0.0, -66.5),
	]
	var tree_ids: Array[String] = ["brgy_banana_clump", "brgy_mango_tree", "brgy_coconut_palm"]
	var drum_blue: Color = Color(0.18, 0.42, 0.68)
	for t_i: int in interior_tree_spots.size():
		var tp: Vector3 = interior_tree_spots[t_i]
		var tid: String = tree_ids[t_i % tree_ids.size()]
		var t_inst: Node3D = _instantiate_kit(tid)
		t_inst.name = "BarangayTree_%d" % t_i
		t_inst.scale = Vector3.ONE * (0.88 + 0.22 * absf(sin(float(t_i) * 2.1)))
		_place_walkthrough(t_inst, tp, float(t_i) * 1.3)
		var d_off: Vector3 = Vector3(1.15 if tp.x < 0.0 else -1.15, 0.0, 0.75)
		kit.cylinder(tp + d_off, 0.27, 0.27, 0.86, 8, drum_blue, drum_blue * 0.76)

	var dressing_mesh: ArrayMesh = kit.commit()
	var dressing_mat: StandardMaterial3D = StandardMaterial3D.new()
	dressing_mat.vertex_color_use_as_albedo = true
	dressing_mat.roughness = 0.88
	var outline: Material = _outline_material()
	if outline != null:
		dressing_mat.next_pass = outline
	dressing_mesh.surface_set_material(0, dressing_mat)
	var dressing_inst: MeshInstance3D = _add(dressing_mesh, Vector3.ZERO)
	dressing_inst.name = "BarangayMicroDressing"


static func _kit_oriented_box(kit: LowPoly, b: Basis, pos: Vector3, sz: Vector3, top_c: Color, side_c: Color) -> void:
	kit.set_transform(Transform3D(b, pos))
	kit.plain_box(Vector3.ZERO, sz, side_c, top_c)
	kit.reset_transform()


func _outline_material() -> Material:
	if Settings.performance == &"turbo":
		return null
	if _ink_outline_mat == null:
		if ResourceLoader.exists(INK_OUTLINE_PATH):
			var shader: Shader = load(INK_OUTLINE_PATH) as Shader
			if shader != null:
				var smat: ShaderMaterial = ShaderMaterial.new()
				smat.shader = shader
				smat.set_shader_parameter("ink_width", 0.008)
				smat.set_shader_parameter("distance_comp", 0.50)
				smat.set_shader_parameter("max_distance", 155.0)
				_ink_outline_mat = smat
		if _ink_outline_mat == null:
			_ink_outline_mat = LowPoly.outline_material()
	return _ink_outline_mat


func _apply_ink_outlines(root: Node3D) -> void:
	var outline: Material = _outline_material()
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
		_apply_ink_outlines(inst)
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
	var outline: Material = _outline_material()
	if outline != null:
		mat.next_pass = outline
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
