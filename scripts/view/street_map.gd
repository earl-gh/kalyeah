class_name StreetMap
extends Node3D
## Circular Tight Provincial Barangay 3D map (view only; reads MapLayout, holds no sim rules).
## Features an unbroken 360-degree ring of tabi-tabing mga bahay around `arena_radius = 84m`,
## claustrophobic 3.8m-5.8m eskinitas with overhead sampayan laundry lines, 3 tight Estero Creek
## bridges, a single batched Chaotic Spaghetti Wiring ArrayMesh across 24 leaning Meralco poles,
## and Valorant x Arcane x 90s Komiks ink outlines (`arcane_ink_outline.gdshader`).

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY

const GROUND_SHADER: Shader = preload("res://shaders/barangay_ground.gdshader")
const STREET_SHADER: Shader = GROUND_SHADER
const WATER_SHADER: Shader = preload("res://shaders/estero_water.gdshader")
const SKY_SHADER: Shader = preload("res://shaders/tropical_sky.gdshader")
const CHALK_SHADER: Shader = preload("res://shaders/chalk_line.gdshader")
const INK_OUTLINE_PATH: String = "res://shaders/arcane_ink_outline.gdshader"

const BARANGAY_DIR: String = "res://assets/models/barangay/%s.glb"

## Balanced golden-hour ("alas-kwatro") lighting: rich warm contrast with zero white blowout.
const SUN_ANGLES: Vector3 = Vector3(-41.0, 36.0, 0.0)
const SUN_COLOR: Color = Color(1.0, 0.91, 0.77)
const SUN_ENERGY: float = 0.88
const SHADOW_DISTANCE: float = 185.0
const FILL_ANGLES: Vector3 = Vector3(-34.0, 216.0, 0.0)
const FILL_COLOR: Color = Color(0.58, 0.74, 0.96)
const FILL_ENERGY: float = 0.24
const AMBIENT_COLOR: Color = Color(0.92, 0.86, 0.80)
const AMBIENT_ENERGY: float = 0.42

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

const COVER_HEIGHT: float = 1.45
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
}

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
	env.tonemap_exposure = 0.96
	env.glow_enabled = false
	env.fog_enabled = true
	env.fog_light_color = Color(0.90, 0.78, 0.64)
	env.fog_light_energy = 0.36
	env.fog_density = 0.0012
	env.fog_sky_affect = 0.16
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
	return 68.0


func fog_far_start() -> float:
	return 64.0


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
	_build_micro_dressing_and_sampayan()
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
	if water_rects.is_empty():
		return
	var water_mat: ShaderMaterial = ShaderMaterial.new()
	water_mat.shader = WATER_SHADER
	water_mat.set_shader_parameter("creek_half_width", 4.0)
	var embankment_mat: StandardMaterial3D = StandardMaterial3D.new()
	embankment_mat.albedo_color = Color(0.39, 0.37, 0.34)
	embankment_mat.roughness = 0.92
	var outline: Material = _outline_material()
	if outline != null:
		embankment_mat.next_pass = outline

	for rect: Rect2 in water_rects:
		var water_plane: PlaneMesh = PlaneMesh.new()
		water_plane.size = Vector2(rect.size.x, maxf(rect.size.y - 0.35, 1.0))
		water_plane.subdivide_width = maxi(4, int(rect.size.x * 0.5))
		water_plane.subdivide_depth = 6
		water_plane.material = water_mat
		var center: Vector2 = rect.get_center()
		var w_mesh: MeshInstance3D = _add(water_plane, Vector3(center.x, 0.07, center.y))
		w_mesh.name = "EsteroWater"
		w_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

		for bank_sign: float in [-1.0, 1.0]:
			var curb: BoxMesh = BoxMesh.new()
			curb.size = Vector3(rect.size.x, 0.44, 0.44)
			curb.material = embankment_mat
			_add(curb, Vector3(center.x, 0.22, center.y + bank_sign * (rect.size.y * 0.5 - 0.14)))

	# Three tight tactical crossings: West Talipapa (-45.7), Central Poso/Rotonda (0.0), East Toda (+45.7)
	var bridge_specs: Array[Vector2] = [
		Vector2(-45.7, 5.8),
		Vector2(0.0, 6.4),
		Vector2(45.7, 5.8),
	]
	for spec: Vector2 in bridge_specs:
		var bx: float = spec.x
		var width_x: float = spec.y
		var bridge: Node3D = _instantiate_kit("brgy_footbridge")
		var nat: Vector3 = _kit_bounds("brgy_footbridge").size
		bridge.scale = Vector3(width_x / maxf(nat.x, 0.1), 0.92, 10.2 / maxf(nat.z, 0.1))
		_place_walkthrough(bridge, Vector3(bx, 0.0, 0.0), 0.0)


func _build_district_landmarks() -> void:
	# 1. South (+Z) Own Base Landmark: Open-Air Covered Barangay Basketball Court at (0, +64)
	var own_c: Vector2 = layout.base_center(MapLayout.SIDE_OWN)
	var court: Node3D = _instantiate_kit("brgy_covered_court")
	court.name = "CoveredBasketballCourt"
	court.scale = Vector3(1.28, 1.18, 1.18)
	_place_walkthrough(court, Vector3(own_c.x, 0.0, own_c.y), 0.0)

	# 2. North (-Z) Enemy Base Landmark: Municipal Barangay Hall framing the -Z Base Plaza
	var enemy_c: Vector2 = layout.base_center(MapLayout.SIDE_ENEMY)
	var hall: Node3D = _instantiate_kit("brgy_hall_plaza")
	hall.name = "BarangayHall"
	hall.scale = Vector3.ONE * 1.24
	var hall_z: float = -(layout.arena_radius + 5.8) if layout.arena_radius > 0.0 else (-layout.lane_length * 0.5 - 7.5)
	_place_walkthrough(hall, Vector3(enemy_c.x, 0.0, hall_z), 0.0)

	# 3. NW & SE Rescue Patio Waiting Sheds framing the 5-slot tagged-player sidewalks
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var r_rect: Rect2 = layout.rescue_rect(side)
		var tag_mid: Vector2 = layout.tagged_place(side, 2)
		var shed: Node3D = _instantiate_kit("brgy_waiting_shed")
		shed.scale = Vector3(1.42, 1.18, 1.25)
		var outward_x: float = -float(side) * 2.2
		var sx: float = tag_mid.x + outward_x
		var yaw: float = PI * 0.5 if side == MapLayout.SIDE_OWN else -PI * 0.5
		_place_walkthrough(shed, Vector3(sx, 0.0, r_rect.get_center().y), yaw)

	# 4. Central Barrio Poso (cast-iron hand pump + wet concrete apron) beside the Central Bridge
	for sign_f: float in [-1.0, 1.0]:
		_place_poso_landmark(Vector3(sign_f * 4.4, 0.0, sign_f * 6.4), 0.0 if sign_f > 0.0 else PI)

	# 5. Parked TODA Tricycles at East/West Toda pockets & Sorbetes carts near base plazas
	var trike_spots: Array[Vector3] = [
		Vector3(51.5, 0.0, 6.6),
		Vector3(-51.5, 0.0, -6.6),
		Vector3(-15.2, 0.0, 48.0),
		Vector3(15.2, 0.0, -48.0),
	]
	for idx: int in trike_spots.size():
		var spot: Vector3 = trike_spots[idx]
		var trike: Node3D = _instantiate_kit("brgy_tricycle")
		trike.scale = Vector3.ONE * 1.08
		var t_yaw: float = PI * 0.5 if spot.x < 0.0 else -PI * 0.5
		_place_walkthrough(trike, spot, t_yaw)

	for side_sign: float in [-1.0, 1.0]:
		var cart: Node3D = _instantiate_kit("brgy_sorbetes_cart")
		cart.scale = Vector3.ONE * 1.08
		_place_walkthrough(cart, Vector3(side_sign * 14.8, 0.0, side_sign * 63.0), -side_sign * PI * 0.5)

	# 6. Fiesta Banderitas across the 3 bridges and base approaches
	var banderita_spans: Array[Vector3] = [
		Vector3(-45.7, 0.0, 0.0),
		Vector3(0.0, 0.0, 0.0),
		Vector3(45.7, 0.0, 0.0),
		Vector3(0.0, 0.0, 46.5),
		Vector3(0.0, 0.0, -46.5),
	]
	for span_at: Vector3 in banderita_spans:
		var bunting: Node3D = _instantiate_kit("brgy_banderitas")
		bunting.scale = Vector3(0.85, 1.05, 0.95)
		_place_walkthrough(bunting, span_at, 0.0)


func _place_poso_landmark(at: Vector3, yaw: float) -> void:
	var poso_glb: String = BARANGAY_DIR % "brgy_poso_well"
	if ResourceLoader.exists(poso_glb):
		var model: Node3D = _instantiate_kit("brgy_poso_well")
		model.scale = Vector3.ONE * 1.15
		_place_walkthrough(model, at, yaw)
		return
	var kit: LowPoly = LowPoly.new()
	var slab_top: Color = Color(0.36, 0.35, 0.33)
	var slab_side: Color = Color(0.24, 0.23, 0.22)
	var iron_top: Color = Color(0.18, 0.36, 0.24)
	var iron_side: Color = Color(0.11, 0.23, 0.16)
	var handle_col: Color = Color(0.54, 0.20, 0.16)
	var bucket_col: Color = Color(0.18, 0.34, 0.56)
	kit.rounded_slab(Vector3(0.0, 0.06, 0.0), Vector3(1.65, 0.12, 1.65), 0.28, slab_top, slab_side)
	kit.cylinder(Vector3(0.0, 0.12, 0.0), 0.16, 0.12, 0.92, 8, iron_top, iron_side)
	kit.cylinder(Vector3(0.0, 1.02, 0.0), 0.20, 0.18, 0.24, 8, iron_top, iron_side)
	kit.box(Vector3(0.24, 0.88, 0.0), Vector3(0.34, 0.10, 0.10), iron_top, iron_side)
	_kit_oriented_box(
		kit,
		Basis.from_euler(Vector3(0.0, 0.0, -0.46)),
		Vector3(-0.34, 1.18, 0.0),
		Vector3(0.76, 0.06, 0.06),
		handle_col,
		handle_col * 0.72
	)
	kit.cylinder(Vector3(0.42, 0.12, 0.0), 0.17, 0.21, 0.34, 8, bucket_col, bucket_col * 0.72)
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

	# Subtle provincial looban yaw jitter (+-3.2 deg) & micro-offset so blocks read as organic construction, never a rigid grid
	var jitter_deg: float = (1.2 if prop_key == &"cover_wall" else 3.2) * sin(float(index) * 4.13 + 1.17)
	yaw += deg_to_rad(jitter_deg)
	var jitter_off: Vector3 = Vector3(
		0.18 * cos(float(index) * 3.71 + 0.4),
		0.0,
		0.20 * sin(float(index) * 2.89 + 0.9)
	)

	var target_w: float = rect.size.y if turn_quarter else rect.size.x
	var target_d: float = rect.size.x if turn_quarter else rect.size.y
	var overhang: float = 0.96 if prop_key == &"cover_wall" else 1.005
	var sx: float = (target_w * overhang) / maxf(nat.x, 0.1)
	var sz: float = (target_d * overhang) / maxf(nat.z, 0.1)
	var h_var: float = 0.88 + 0.34 * absf(sin(float(index) * 2.73 + 0.65))
	var sy: float = clampf((sx + sz) * 0.50 * h_var, 0.88, 1.72)
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
	var ring_radius: float = layout.arena_radius if layout.arena_radius > 0.0 else minf(layout.lane_width, layout.lane_length) * 0.5

	# Ring 1: Unbroken 360-degree wall-to-wall inner ring with organic radial pockets & +-3.2 deg looban yaw jitter
	var inner_count: int = 40
	var inner_r: float = ring_radius + 6.5
	var chord_w: float = (TAU * inner_r / float(inner_count)) * 1.15
	for i: int in inner_count:
		var angle: float = TAU * float(i) / float(inner_count) + 0.012 * sin(float(i) * 2.7)
		var dir: Vector2 = Vector2(cos(angle), sin(angle))
		var local_r: float = inner_r + 1.55 * sin(float(i) * 2.9 + 0.4) + 0.75 * cos(float(i) * 5.1)
		# Set back slightly behind the North Barangay Hall, Estero outlets, and Rescue Sheds so landmarks breathe while sealing horizon
		if dir.y < -0.94:
			local_r += 7.5
		elif absf(dir.y) < 0.09:
			local_r += 4.2
		elif (dir.x < -0.75 and dir.y < -0.20 and dir.y > -0.55) or (dir.x > 0.75 and dir.y > 0.20 and dir.y < 0.55):
			local_r += 4.5
		var pos2: Vector2 = dir * local_r
		var kit_id: String = perimeter_kits[i % perimeter_kits.size()]
		var house: Node3D = _instantiate_kit(kit_id)
		var nat: Vector3 = _kit_bounds(kit_id).size
		var sx: float = chord_w / maxf(nat.x, 4.0)
		var sz: float = clampf((10.8 + 2.2 * absf(cos(float(i) * 1.7))) / maxf(nat.z, 4.0), 0.92, 1.52)
		var sy: float = clampf(0.96 + 0.46 * absf(sin(float(i) * 2.13 + 0.3)), 0.96, 1.54)
		house.scale = Vector3(sx, sy, sz)
		var inward_yaw: float = atan2(-dir.x, -dir.y) + deg_to_rad(3.2 * sin(float(i) * 3.7 + 0.9))
		_add_prop(house, Vector3(pos2.x, 0.0, pos2.y), inward_yaw)
		var row_key: int = -1 if pos2.x < 0.0 else 1
		house_rows[row_key].append(house)
		far_props.append(kit_id)

	# Ring 2: Staggered irregular outer barangay roof tier (`r = ring_radius + 16..22m`)
	var outer_count: int = 32
	var outer_r: float = ring_radius + 18.6
	var outer_chord: float = (TAU * outer_r / float(outer_count)) * 1.16
	for j: int in outer_count:
		var angle_o: float = TAU * (float(j) + 0.5) / float(outer_count) + 0.018 * cos(float(j) * 3.1)
		var dir_o: Vector2 = Vector2(cos(angle_o), sin(angle_o))
		var r_o: float = outer_r + 2.6 * sin(float(j) * 3.3 + 1.1) + 1.3 * cos(float(j) * 4.7)
		var pos_o: Vector2 = dir_o * r_o
		var kit_o: String = perimeter_kits[(j + 2) % perimeter_kits.size()]
		var bldg: Node3D = _instantiate_kit(kit_o)
		var nat_o: Vector3 = _kit_bounds(kit_o).size
		var sx_o: float = outer_chord / maxf(nat_o.x, 4.0)
		var sy_o: float = 1.12 + 0.44 * absf(cos(float(j) * 2.3 + 0.7))
		bldg.scale = Vector3(sx_o, sy_o, clampf(12.5 / maxf(nat_o.z, 4.0), 0.98, 1.54))
		var yaw_o: float = atan2(-dir_o.x, -dir_o.y) + deg_to_rad(4.8 * cos(float(j) * 2.9))
		_add_prop(bldg, Vector3(pos_o.x, 0.0, pos_o.y), yaw_o)
		var row_o: int = -1 if pos_o.x < 0.0 else 1
		house_rows[row_o].append(bldg)
		far_props.append(kit_o)

	# Ring 3: Scattered provincial homestead clusters (`r = ring_radius + 28..38m`) feathering the outer barangay silhouette
	var outlier_count: int = 18
	for k: int in outlier_count:
		var ang_k: float = TAU * (float(k) + 0.25) / float(outlier_count) + 0.09 * sin(float(k) * 2.4 + 0.6)
		var dir_k: Vector2 = Vector2(cos(ang_k), sin(ang_k))
		if absf(dir_k.y) < 0.11:
			continue
		var r_k: float = ring_radius + 31.5 + 4.8 * sin(float(k) * 3.7 + 0.5)
		var pos_k: Vector2 = dir_k * r_k
		var kit_k: String = perimeter_kits[(k * 3 + 1) % perimeter_kits.size()]
		var out_house: Node3D = _instantiate_kit(kit_k)
		var nat_k: Vector3 = _kit_bounds(kit_k).size
		var sx_k: float = (13.5 + 3.0 * absf(sin(float(k) * 1.9))) / maxf(nat_k.x, 4.0)
		var sy_k: float = 0.94 + 0.36 * absf(cos(float(k) * 2.7))
		out_house.scale = Vector3(sx_k, sy_k, clampf(11.0 / maxf(nat_k.z, 4.0), 0.92, 1.38))
		var yaw_k: float = atan2(-dir_k.x, -dir_k.y) + deg_to_rad(8.5 * sin(float(k) * 4.1))
		_place_walkthrough(out_house, Vector3(pos_k.x, 0.0, pos_k.y), yaw_k)


func _build_micro_dressing_and_sampayan() -> void:
	if layout.arena_radius <= 0.0 and layout.lane_width < 80.0:
		return
	var kit: LowPoly = LowPoly.new()
	var ring_r: float = layout.arena_radius if layout.arena_radius > 0.0 else minf(layout.lane_width, layout.lane_length) * 0.5

	# 1. Circular 360-degree weathered hollow-block & concrete curb along `r = ring_r + 0.65m`
	var curb_top: Color = Color(0.42, 0.39, 0.35)
	var curb_side: Color = Color(0.28, 0.26, 0.24)
	var post_accent: Color = Color(0.56, 0.44, 0.18)
	var segs: int = 72
	var curb_r: float = ring_r + 0.65
	var seg_len: float = (TAU * curb_r / float(segs)) * 1.03
	var r_own: Rect2 = layout.rescue_rect(MapLayout.SIDE_OWN)
	var r_ene: Rect2 = layout.rescue_rect(MapLayout.SIDE_ENEMY)
	for i: int in segs:
		var a: float = TAU * (float(i) + 0.5) / float(segs)
		var p: Vector2 = Vector2(cos(a), sin(a)) * curb_r
		# Leave clean walk-in bays at the 2 Rescue Waiting Sheds and 2 Estero Creek outlets
		var in_estero: bool = absf(p.y) < 4.4
		var in_own_shed: bool = p.x < 0.0 and p.y >= r_own.position.y - 1.0 and p.y <= r_own.end.y + 1.0
		var in_ene_shed: bool = p.x > 0.0 and p.y >= r_ene.position.y - 1.0 and p.y <= r_ene.end.y + 1.0
		if in_estero or in_own_shed or in_ene_shed:
			continue
		var tangent_yaw: float = atan2(-cos(a), sin(a))
		var basis_c: Basis = Basis(Vector3.UP, tangent_yaw)
		_kit_oriented_box(kit, basis_c, Vector3(p.x, 0.36, p.y), Vector3(seg_len, 0.72, 0.42), curb_top, curb_side)
		if i % 2 == 0:
			kit.cylinder(Vector3(p.x, 0.72, p.y), 0.16, 0.15, 0.46, 6, post_accent, curb_side)

	# 2. Overhead Sampayan (bamboo clotheslines + recognizable T-shirts, sando jerseys & puruntong shorts!)
	var sampayan_spans: PackedVector3Array = PackedVector3Array([
		Vector3(-49.0, 5.15, 12.0), Vector3(-43.8, 5.30, 12.5),
		Vector3(-49.0, 4.95, 18.5), Vector3(-43.8, 5.05, 18.0),
		Vector3(-25.6, 5.20, 11.5), Vector3(-21.6, 5.05, 12.1),
		Vector3(-25.6, 5.55, 15.6), Vector3(-21.6, 5.40, 16.2),
		Vector3(-25.6, 4.95, 19.8), Vector3(-21.6, 5.15, 19.3),
		Vector3(21.6, 5.05, 13.0), Vector3(25.4, 5.30, 12.6),
		Vector3(21.6, 5.35, 19.2), Vector3(25.4, 5.10, 18.7),
		Vector3(43.0, 5.15, 14.5), Vector3(48.4, 4.95, 15.0),
		Vector3(-50.6, 5.20, 33.0), Vector3(-46.4, 5.05, 33.6),
		Vector3(-28.2, 5.05, 31.8), Vector3(-24.4, 5.25, 32.3),
		Vector3(-28.2, 5.35, 39.2), Vector3(-24.4, 5.10, 38.7),
		Vector3(-13.4, 5.30, 32.5), Vector3(-9.4, 5.10, 33.0),
		Vector3(9.4, 5.10, 35.0), Vector3(13.4, 5.30, 34.4),
		Vector3(24.4, 5.05, 36.5), Vector3(28.2, 4.90, 36.0),
		Vector3(-34.3, 5.15, 54.5), Vector3(-30.3, 5.30, 55.0),
		Vector3(30.3, 5.20, 55.0), Vector3(34.3, 5.05, 54.4),
	])
	var cloth_palette: Array[Color] = [
		Color(0.66, 0.21, 0.19),
		Color(0.19, 0.44, 0.62),
		Color(0.64, 0.50, 0.16),
		Color(0.22, 0.50, 0.30),
		Color(0.56, 0.30, 0.48),
		Color(0.62, 0.57, 0.49),
	]
	var trim_palette: Array[Color] = [
		Color(0.68, 0.64, 0.56),
		Color(0.64, 0.48, 0.16),
		Color(0.16, 0.22, 0.34),
		Color(0.62, 0.22, 0.18),
	]
	var bamboo_col: Color = Color(0.46, 0.38, 0.22)
	var pin_col: Color = Color(0.58, 0.46, 0.26)
	var pair_count: int = sampayan_spans.size() / 2
	for side_mul: float in [1.0, -1.0]:
		for s_idx: int in pair_count:
			var a_pt: Vector3 = sampayan_spans[s_idx * 2] * Vector3(side_mul, 1.0, side_mul)
			var b_pt: Vector3 = sampayan_spans[s_idx * 2 + 1] * Vector3(side_mul, 1.0, side_mul)
			var span_vec: Vector3 = b_pt - a_pt
			var span_len: float = span_vec.length()
			if span_len < 0.5:
				continue
			var dir_s: Vector3 = span_vec / span_len
			var yaw_s: float = atan2(dir_s.x, dir_s.z)
			var pitch_s: float = -asin(clampf(dir_s.y, -0.5, 0.5))
			var pole_basis: Basis = Basis(Vector3.UP, yaw_s) * Basis(Vector3.RIGHT, pitch_s)
			var mid_s: Vector3 = (a_pt + b_pt) * 0.5 - Vector3(0.0, 0.06, 0.0)
			_kit_oriented_box(kit, pole_basis, mid_s, Vector3(0.048, 0.048, span_len), bamboo_col, bamboo_col * 0.74)
			var pieces: int = 5
			for p_i: int in pieces:
				var t: float = (float(p_i) + 0.65) / float(pieces + 0.3)
				var hang_top: Vector3 = a_pt.lerp(b_pt, t) - Vector3(0.0, 0.06, 0.0)
				var c_col: Color = cloth_palette[(s_idx * 2 + p_i) % cloth_palette.size()]
				var tr_col: Color = trim_palette[(s_idx + p_i) % trim_palette.size()]
				var kind: int = (p_i + s_idx) % 3
				var sway_rad: float = deg_to_rad(5.5 * sin(float(s_idx * 5 + p_i) * 2.1 + side_mul))
				_append_sampayan_garment(kit, yaw_s, hang_top, c_col, tr_col, pin_col, kind, sway_rad)

	# 3. Tight-corner Tropical Trees (Banana "Saging" clumps & Mango/Acacia canopies) + Blue Water Drums
	var tree_spots: Array[Vector3] = [
		Vector3(-73.8, 0.0, 23.4), Vector3(73.8, 0.0, -23.4),
		Vector3(-50.2, 0.0, 23.2), Vector3(50.2, 0.0, -23.2),
		Vector3(50.2, 0.0, 21.2), Vector3(-50.2, 0.0, -21.2),
		Vector3(-66.5, 0.0, 44.2), Vector3(66.5, 0.0, -44.2),
		Vector3(-51.2, 0.0, 62.8), Vector3(51.2, 0.0, -62.8),
		Vector3(51.2, 0.0, 62.8), Vector3(-51.2, 0.0, -62.8),
		Vector3(-17.8, 0.0, 69.2), Vector3(17.8, 0.0, -69.2),
		Vector3(17.8, 0.0, 69.2), Vector3(-17.8, 0.0, -69.2),
		Vector3(-82.5, 0.0, -8.5), Vector3(82.5, 0.0, 8.5),
		Vector3(-82.5, 0.0, 8.5), Vector3(82.5, 0.0, -8.5),
	]
	var trunk_col: Color = Color(0.28, 0.20, 0.14)
	var leaf_lit: Color = Color(0.24, 0.44, 0.20)
	var leaf_dark: Color = Color(0.15, 0.30, 0.14)
	var banana_lit: Color = Color(0.30, 0.48, 0.20)
	var drum_blue: Color = Color(0.14, 0.34, 0.58)
	for t_i: int in tree_spots.size():
		var tp: Vector3 = tree_spots[t_i]
		if t_i % 2 == 0:
			# Banana (Saging) clump: slender pseudostem + 5 arching broad tropical fronds
			kit.cylinder(tp, 0.18, 0.13, 3.2, 6, banana_lit * 0.72, trunk_col)
			for f_i: int in 5:
				var fyaw: float = TAU * float(f_i) / 5.0 + float(t_i) * 0.4
				var fbasis: Basis = Basis(Vector3.UP, fyaw) * Basis(Vector3.RIGHT, -0.48)
				var fpos: Vector3 = tp + Vector3(0.0, 3.15, 0.0) + Basis(Vector3.UP, fyaw) * Vector3(0.0, 0.15, 1.05)
				_kit_oriented_box(kit, fbasis, fpos, Vector3(0.58, 0.06, 2.15), banana_lit, leaf_dark)
		else:
			# Provincial Mango / Acacia shade tree: sturdy dark trunk + layered painterly crown
			kit.cylinder(tp, 0.34, 0.24, 3.8, 7, trunk_col, trunk_col * 0.75)
			kit.sphere(tp + Vector3(0.0, 4.7, 0.0), 2.35, leaf_lit, 8, 5)
			kit.sphere(tp + Vector3(1.15, 4.2, 0.65), 1.65, leaf_lit * 0.94, 8, 5)
			kit.sphere(tp + Vector3(-1.05, 4.3, -0.55), 1.55, banana_lit * 0.92, 8, 5)
		# Blue barrio plastic water drum tucked beside house corner
		var d_off: Vector3 = Vector3(0.85 if tp.x < 0.0 else -0.85, 0.0, 0.65)
		kit.cylinder(tp + d_off, 0.29, 0.29, 0.92, 8, drum_blue, drum_blue * 0.72)

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


static func _append_sampayan_garment(
	kit: LowPoly,
	yaw_s: float,
	hang_top: Vector3,
	c_col: Color,
	trim_col: Color,
	pin_col: Color,
	kind: int,
	sway_rad: float
) -> void:
	var g_basis: Basis = Basis(Vector3.UP, yaw_s) * Basis(Vector3.FORWARD, sway_rad)
	var dark_c: Color = c_col * 0.76
	# Two wooden sipit clothespins gripping the bamboo pole
	for pin_sign: float in [-1.0, 1.0]:
		var pin_pos: Vector3 = hang_top + g_basis * Vector3(0.0, 0.03, pin_sign * 0.14)
		_kit_oriented_box(kit, g_basis, pin_pos, Vector3(0.035, 0.09, 0.032), pin_col, pin_col * 0.72)

	if kind == 0:
		# Classic Pinoy T-Shirt (kamiseta): torso body + ringer collar + two angled sleeves!
		var body_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.32, 0.0)
		_kit_oriented_box(kit, g_basis, body_pos, Vector3(0.038, 0.58, 0.42), c_col, dark_c)
		var collar_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.06, 0.0)
		_kit_oriented_box(kit, g_basis, collar_pos, Vector3(0.044, 0.07, 0.16), trim_col, trim_col * 0.76)
		for side_s: float in [-1.0, 1.0]:
			var slv_basis: Basis = g_basis * Basis(Vector3.RIGHT, side_s * 0.62)
			var slv_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.15, side_s * 0.27)
			_kit_oriented_box(kit, slv_basis, slv_pos, Vector3(0.036, 0.17, 0.23), c_col, dark_c)
			var cuff_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.21, side_s * 0.35)
			_kit_oriented_box(kit, slv_basis, cuff_pos, Vector3(0.040, 0.175, 0.045), trim_col, trim_col * 0.76)
	elif kind == 1:
		# Basketball Shorts (puruntong): elastic waistband + two clearly separated legs with hem stripes!
		var waist_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.07, 0.0)
		_kit_oriented_box(kit, g_basis, waist_pos, Vector3(0.042, 0.11, 0.44), trim_col, trim_col * 0.76)
		for leg_s: float in [-1.0, 1.0]:
			var leg_basis: Basis = g_basis * Basis(Vector3.RIGHT, -leg_s * 0.09)
			var leg_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.34, leg_s * 0.13)
			_kit_oriented_box(kit, leg_basis, leg_pos, Vector3(0.038, 0.45, 0.17), c_col, dark_c)
			var hem_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.54, leg_s * 0.15)
			_kit_oriented_box(kit, leg_basis, hem_pos, Vector3(0.042, 0.055, 0.175), trim_col, trim_col * 0.76)
	else:
		# Barrio Basketball Jersey / Sando: two shoulder straps with open neck scoop + striped chest!
		for strap_s: float in [-1.0, 1.0]:
			var strap_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.10, strap_s * 0.135)
			_kit_oriented_box(kit, g_basis, strap_pos, Vector3(0.038, 0.17, 0.082), c_col, dark_c)
		var chest_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.41, 0.0)
		_kit_oriented_box(kit, g_basis, chest_pos, Vector3(0.038, 0.47, 0.40), c_col, dark_c)
		var stripe_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.33, 0.0)
		_kit_oriented_box(kit, g_basis, stripe_pos, Vector3(0.044, 0.10, 0.405), trim_col, trim_col * 0.76)


static func _kit_oriented_box(kit: LowPoly, b: Basis, pos: Vector3, sz: Vector3, top_c: Color, side_c: Color) -> void:
	kit.set_transform(Transform3D(b, pos))
	kit.plain_box(Vector3.ZERO, sz, side_c, top_c)
	kit.reset_transform()


func _build_spaghetti_wiring_and_poles() -> void:
	if layout.arena_radius <= 0.0 and layout.lane_width < 80.0:
		return
	var ring_r: float = (layout.arena_radius if layout.arena_radius > 0.0 else 84.0) - 0.35

	# 1. 24 Primary Leaning Meralco Posts (0..11 inner bridge/plaza poles + 12..23 perimeter ring poles)
	var inner_poles: Array[Vector3] = [
		Vector3(-49.0, 0.0, 7.6),
		Vector3(-3.4, 0.0, 7.6),
		Vector3(48.8, 0.0, 7.6),
		Vector3(49.0, 0.0, -7.6),
		Vector3(3.4, 0.0, -7.6),
		Vector3(-48.8, 0.0, -7.6),
		Vector3(-24.8, 0.0, 27.4),
		Vector3(24.8, 0.0, 27.4),
		Vector3(24.8, 0.0, -27.4),
		Vector3(-24.8, 0.0, -27.4),
		Vector3(-15.8, 0.0, 50.2),
		Vector3(15.8, 0.0, -50.2),
	]
	var outer_poles: Array[Vector3] = []
	for i: int in 12:
		var ang: float = TAU * (float(i) + 0.35) / 12.0
		outer_poles.append(Vector3(cos(ang) * ring_r, 0.0, sin(ang) * ring_r))

	# 16 Mid-Eskinita Utility Corner Poles / Wall-Bracket Masts (24..39) tucked flush along narrow Z = +-12..44m alleys!
	var eskinita_half_poles: Array[Vector3] = [
		Vector3(-25.65, 0.0, 13.6),
		Vector3(-21.55, 0.0, 20.6),
		Vector3(-28.25, 0.0, 35.4),
		Vector3(-43.75, 0.0, 15.4),
		Vector3(-13.45, 0.0, 35.6),
		Vector3(13.45, 0.0, 35.6),
		Vector3(21.55, 0.0, 15.4),
		Vector3(43.00, 0.0, 15.4),
	]
	var eskinita_poles: Array[Vector3] = []
	for ep: Vector3 in eskinita_half_poles:
		eskinita_poles.append(ep)
	for ep_neg: Vector3 in eskinita_half_poles:
		eskinita_poles.append(Vector3(-ep_neg.x, 0.0, -ep_neg.z))

	var all_poles: Array[Vector3] = []
	all_poles.append_array(inner_poles)
	all_poles.append_array(outer_poles)
	all_poles.append_array(eskinita_poles)

	var pole_tops: Array[Vector3] = []
	var nat_pole_h: float = maxf(_kit_bounds("brgy_meralco_post").size.y, 6.0)
	var pole_scale_y: float = 9.4 / nat_pole_h
	var alley_pole_scale_y: float = 6.9 / nat_pole_h
	for idx: int in all_poles.size():
		var spot: Vector3 = all_poles[idx]
		var is_alley_mast: bool = idx >= 24
		var pole: Node3D = _instantiate_kit("brgy_meralco_post")
		if is_alley_mast:
			pole.scale = Vector3(0.66, alley_pole_scale_y, 0.66)
		else:
			pole.scale = Vector3(0.88, pole_scale_y, 0.88)
		var yaw: float = (PI * 0.5 if spot.x < 0.0 else -PI * 0.5) if is_alley_mast else atan2(-spot.x, -spot.z)
		var lean_amp: float = 4.2 if is_alley_mast else 2.6
		var lean_x: float = deg_to_rad(lean_amp * sin(float(idx) * 2.1 + 0.4))
		var lean_z: float = deg_to_rad(lean_amp * cos(float(idx) * 1.7 - 0.3))
		pole.position = spot
		pole.rotation = Vector3(lean_x, yaw, lean_z)
		_geometry.add_child(pole)
		var anchor_h: float = 6.20 if is_alley_mast else 8.40
		var top_offset: Vector3 = pole.transform.basis.orthonormalized() * Vector3(0.0, anchor_h, 0.0)
		pole_tops.append(spot + top_offset)

	# Single batched ArrayMesh for the entire 2.5x Chaotic Pinoy Spaghetti Wiring Network!
	var st: SurfaceTool = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	# 2. Pole-to-pole trunk & mid-eskinita overhead spans (indices into `pole_tops` 0..39)
	var spans: Array[Vector2i] = [
		# Outer 12-pole perimeter ring loop (12..23)
		Vector2i(12, 13), Vector2i(13, 14), Vector2i(14, 15), Vector2i(15, 16),
		Vector2i(16, 17), Vector2i(17, 18), Vector2i(18, 19), Vector2i(19, 20),
		Vector2i(20, 21), Vector2i(21, 22), Vector2i(22, 23), Vector2i(23, 12),
		# Inner creek & bridge trunk web
		Vector2i(0, 1), Vector2i(1, 2), Vector2i(3, 4), Vector2i(4, 5),
		Vector2i(0, 5), Vector2i(1, 4), Vector2i(2, 3),
		# Inner mid-barangay & base web
		Vector2i(0, 6), Vector2i(1, 6), Vector2i(1, 7), Vector2i(2, 7),
		Vector2i(5, 9), Vector2i(4, 9), Vector2i(4, 8), Vector2i(3, 8),
		Vector2i(6, 10), Vector2i(7, 10), Vector2i(8, 11), Vector2i(9, 11),
		# Radial feeders from perimeter ring into inner barangay poles
		Vector2i(12, 2), Vector2i(14, 7), Vector2i(15, 10), Vector2i(17, 6),
		Vector2i(18, 0), Vector2i(20, 9), Vector2i(21, 11), Vector2i(23, 8),
		# +Z Mid-eskinita low-hanging spaghetti chains (24..31) along narrow Z = 12..44m alleys!
		Vector2i(0, 27), Vector2i(27, 24), Vector2i(27, 6),
		Vector2i(1, 24), Vector2i(24, 25), Vector2i(25, 6), Vector2i(6, 26), Vector2i(26, 10),
		Vector2i(25, 28), Vector2i(28, 10), Vector2i(28, 29), Vector2i(29, 7),
		Vector2i(1, 30), Vector2i(30, 7), Vector2i(30, 31), Vector2i(2, 31), Vector2i(31, 7),
		# -Z Symmetric mid-eskinita low-hanging spaghetti chains (32..39)!
		Vector2i(3, 35), Vector2i(35, 32), Vector2i(35, 8),
		Vector2i(4, 32), Vector2i(32, 33), Vector2i(33, 8), Vector2i(8, 34), Vector2i(34, 11),
		Vector2i(33, 36), Vector2i(36, 11), Vector2i(36, 37), Vector2i(37, 9),
		Vector2i(4, 38), Vector2i(38, 9), Vector2i(38, 39), Vector2i(5, 39), Vector2i(39, 9),
	]

	for s_idx: int in spans.size():
		var pair: Vector2i = spans[s_idx]
		var p_a: Vector3 = pole_tops[pair.x]
		var p_b: Vector3 = pole_tops[pair.y]
		var horiz: Vector3 = Vector3(p_b.x - p_a.x, 0.0, p_b.z - p_a.z)
		var dist: float = horiz.length()
		if dist < 1.0:
			continue
		var side_dir: Vector3 = Vector3(-horiz.z, 0.0, horiz.x) / dist
		var is_eskinita_span: bool = pair.x >= 24 or pair.y >= 24
		var strand_count: int = 6 if is_eskinita_span else 5
		for strand: int in strand_count:
			var f_st: float = float(strand)
			var spread: float = (f_st - 2.2) * 0.22 + 0.08 * sin(float(s_idx * 5 + strand) * 1.9)
			var y_drop: float = -0.14 * f_st + 0.05 * cos(float(s_idx + strand) * 2.3)
			var start_p: Vector3 = p_a + side_dir * spread + Vector3(0.0, y_drop, 0.0)
			var end_p: Vector3 = p_b + side_dir * (spread * 0.82) + Vector3(0.0, y_drop - 0.08 * sin(f_st), 0.0)
			var sag_cap: float = 0.78 if is_eskinita_span else 1.25
			var sag: float = clampf(dist * (0.016 + 0.005 * f_st) + 0.14 * absf(sin(float(s_idx * 3 + strand))), 0.30, sag_cap)
			var radius_w: float = 0.036 if strand == 0 else 0.026
			_append_catenary_wire(st, start_p, end_p, sag, radius_w, 7)
			if is_eskinita_span and strand == 1 and s_idx % 2 == 0:
				var mid_sag_pt: Vector3 = start_p.lerp(end_p, 0.5) - Vector3(0.0, sag + 0.08, 0.0)
				_append_wire_coil(st, mid_sag_pt, 0.24, 0.30, atan2(horiz.x, horiz.z))

	# 3. Dense Crisscrossing Overhead Eskinita Web (`Y = 4.65..6.75m`) across all narrow Z = +-9..44m alleys!
	# Each Vector4 encodes `(x_west_wall, x_east_wall, z_start, z_end)` for a +Z narrow eskinita (mirrored to -Z).
	var alley_corridors: Array[Vector4] = [
		Vector4(-49.1, -43.7, 9.4, 21.4),
		Vector4(-25.7, -21.5, 8.9, 22.0),
		Vector4(-2.7, 2.7, 9.2, 21.6),
		Vector4(21.5, 25.5, 9.4, 21.6),
		Vector4(42.9, 48.5, 9.4, 19.6),
		Vector4(-50.7, -46.3, 28.4, 41.8),
		Vector4(-28.3, -24.3, 28.5, 44.0),
		Vector4(-13.5, -9.3, 28.8, 42.4),
		Vector4(9.3, 13.5, 28.8, 42.4),
		Vector4(24.3, 28.3, 28.5, 44.0),
		Vector4(46.3, 50.7, 28.4, 41.8),
		Vector4(-34.4, -30.2, 50.4, 60.6),
		Vector4(30.2, 34.4, 50.4, 60.6),
	]
	for side_z: float in [1.0, -1.0]:
		for c_idx: int in alley_corridors.size():
			var corr: Vector4 = alley_corridors[c_idx]
			var x_l: float = corr.x * side_z
			var x_r: float = corr.y * side_z
			var stations: int = 5 if (c_idx == 1 or c_idx == 6) else 3
			for st_i: int in stations:
				var frac: float = (float(st_i) + 0.45) / float(stations)
				var z_base: float = lerpf(corr.z, corr.w, frac) * side_z
				var z_diag: float = lerpf(corr.z, corr.w, clampf(frac + 0.24, 0.05, 0.98)) * side_z
				var y_l: float = 5.55 + 1.05 * absf(sin(float(c_idx * 7 + st_i * 3) + side_z * 0.5))
				var y_r: float = 5.60 + 1.00 * absf(cos(float(c_idx * 5 + st_i * 4) - side_z * 0.3))
				var anchor_l: Vector3 = Vector3(x_l, y_l, z_base)
				var anchor_r: Vector3 = Vector3(x_r, y_r, z_base + 0.7 * side_z)
				var anchor_r_diag: Vector3 = Vector3(x_r, clampf(y_r - 0.28, 5.30, 6.7), z_diag)
				var anchor_l_diag: Vector3 = Vector3(x_l, clampf(y_l - 0.24, 5.30, 6.7), z_diag)
				# Wall-mounted steel utility standoffs jutting 0.24m out from opposing bahay walls
				_append_wall_bracket(st, anchor_l, signf(x_r - x_l) * 0.24)
				_append_wall_bracket(st, anchor_r, signf(x_l - x_r) * 0.24)
				# 3-strand sagging cross-eskinita bundle + 2 crisscrossing X-drop wires (`Y = 4.65..6.65m`)
				for w_k: int in 3:
					var wy: float = -0.14 * float(w_k)
					var wz: float = (float(w_k) - 1.0) * 0.22 * side_z
					var sag_c: float = 0.30 + 0.11 * float(w_k) + 0.08 * absf(sin(float(st_i + w_k)))
					_append_catenary_wire(
						st,
						anchor_l + Vector3(0.0, wy, wz),
						anchor_r + Vector3(0.0, wy - 0.06, -wz),
						sag_c,
						0.026 if w_k == 0 else 0.020,
						6
					)
				_append_catenary_wire(st, anchor_l, anchor_r_diag, 0.46, 0.022, 6)
				_append_catenary_wire(st, anchor_r, anchor_l_diag, 0.50, 0.022, 6)
				# Suspended figure-8 telecom slack loop silhouette directly overhead in the narrow alley!
				if (st_i + c_idx) % 2 == 0:
					var loop_pt: Vector3 = anchor_l.lerp(anchor_r, 0.36) - Vector3(0.0, 0.36, 0.0)
					_append_wire_coil(st, loop_pt, 0.22, 0.30, float(st_i) * 0.9 + 0.4)

	# 4. Tangled Telecom Slack Coils ("ikot ng kable") on all 40 poles (0..39), strictly overhead (`Y >= 4.7m`)
	for p_i: int in pole_tops.size():
		var top_p: Vector3 = pole_tops[p_i]
		var coil_drop: float = 0.62 if p_i >= 24 else 1.10
		var coil_center: Vector3 = top_p - Vector3(0.0, coil_drop, 0.0)
		for loop_k: int in 4:
			var loop_yaw: float = float(p_i) * 1.1 + float(loop_k) * 0.85
			var loop_r_x: float = 0.28 + 0.06 * float(loop_k)
			var loop_r_y: float = 0.34 + 0.07 * float(loop_k)
			_append_wire_coil(st, coil_center + Vector3(0.0, -0.09 * float(loop_k), 0.0), loop_r_x, loop_r_y, loop_yaw)

	# 5. Diagonal Service Drop-Wires fanning from poles into nearby interior & perimeter house eaves!
	var obs_rects: Array[Rect2] = layout.obstacle_rects()
	for p_i: int in pole_tops.size():
		var junction_p: Vector3 = pole_tops[p_i] - Vector3(0.0, 0.45 if p_i >= 24 else 0.95, 0.0)
		var max_drops: int = 5 if p_i >= 24 else 3
		var drops_added: int = 0
		for o_i: int in obs_rects.size():
			if drops_added >= max_drops:
				break
			var r_obs: Rect2 = obs_rects[(o_i + p_i * 3) % obs_rects.size()]
			var corner_2d: Vector2 = Vector2(
				clampf(junction_p.x, r_obs.position.x + 0.35, r_obs.end.x - 0.35),
				clampf(junction_p.z, r_obs.position.y + 0.35, r_obs.end.y - 0.35)
			)
			var d2: float = Vector2(junction_p.x, junction_p.z).distance_to(corner_2d)
			if d2 > 1.2 and d2 < 24.0:
				var eave_h: float = 5.15 + 0.85 * absf(sin(float(o_i + p_i) * 1.7))
				var eave_pt: Vector3 = Vector3(corner_2d.x, eave_h, corner_2d.y)
				_append_catenary_wire(st, junction_p, eave_pt, clampf(0.24 + d2 * 0.012, 0.26, 0.56), 0.021, 5)
				drops_added += 1
		# Outer poles (12..23) also drop service wires into the perimeter house ring behind them
		if p_i >= 12 and p_i < 24:
			var rad_dir: Vector2 = Vector2(junction_p.x, junction_p.z).normalized()
			for branch: float in [-3.8, 3.8]:
				var tang: Vector2 = Vector2(-rad_dir.y, rad_dir.x) * branch
				var eave_out: Vector2 = rad_dir * (ring_r + 5.6) + tang
				_append_catenary_wire(st, junction_p, Vector3(eave_out.x, 5.25, eave_out.y), 0.32, 0.020, 4)

	st.generate_normals()
	var wire_mesh: ArrayMesh = st.commit()
	var wire_mat: StandardMaterial3D = StandardMaterial3D.new()
	wire_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	wire_mat.albedo_color = Color(0.08, 0.07, 0.10)
	wire_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wire_mesh.surface_set_material(0, wire_mat)
	var wire_inst: MeshInstance3D = _add(wire_mesh, Vector3.ZERO)
	wire_inst.name = "SpaghettiWiring"
	wire_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


static func _append_wall_bracket(st: SurfaceTool, wall_pt: Vector3, out_x: float) -> void:
	var tip: Vector3 = wall_pt + Vector3(out_x, 0.0, 0.0)
	var brace_low: Vector3 = wall_pt + Vector3(0.0, -0.22, 0.0)
	var bar_z: Vector3 = Vector3(0.0, 0.0, 0.22)
	var thick: Vector3 = Vector3(0.0, 0.032, 0.0)
	_append_quad(st, wall_pt - thick, wall_pt + thick, tip + thick, tip - thick)
	_append_quad(st, brace_low - thick, brace_low + thick, tip + thick, tip - thick)
	_append_quad(st, tip - bar_z - thick, tip - bar_z + thick, tip + bar_z + thick, tip + bar_z - thick)


static func _append_catenary_wire(st: SurfaceTool, a: Vector3, b: Vector3, sag: float, radius: float, segments: int) -> void:
	var chord: Vector3 = b - a
	var horiz: Vector3 = Vector3(chord.x, 0.0, chord.z)
	var h_len: float = horiz.length()
	var side: Vector3 = Vector3(-horiz.z / h_len, 0.0, horiz.x / h_len) * radius if h_len > 0.001 else Vector3(radius, 0.0, 0.0)
	var up: Vector3 = Vector3(0.0, radius * 1.15, 0.0)
	for i: int in segments:
		var t0: float = float(i) / float(segments)
		var t1: float = float(i + 1) / float(segments)
		var p0: Vector3 = a.lerp(b, t0) - Vector3(0.0, 4.0 * sag * t0 * (1.0 - t0), 0.0)
		var p1: Vector3 = a.lerp(b, t1) - Vector3(0.0, 4.0 * sag * t1 * (1.0 - t1), 0.0)
		p0.y = maxf(p0.y, 4.50)
		p1.y = maxf(p1.y, 4.50)
		# Crisp crossed ribbon (vertical + horizontal fins) reads from both shoulder-level and overview angles at minimal triangles!
		_append_quad(st, p0 - up, p0 + up, p1 + up, p1 - up)
		_append_quad(st, p0 - side, p0 + side, p1 + side, p1 - side)


static func _append_wire_coil(st: SurfaceTool, center: Vector3, rx: float, ry: float, yaw: float) -> void:
	var steps: int = 10
	var basis: Basis = Basis(Vector3.UP, yaw)
	var thick: Vector3 = Vector3(0.0, 0.032, 0.0)
	var wide: Vector3 = basis * Vector3(0.028, 0.0, 0.0)
	for i: int in steps:
		var a0: float = TAU * float(i) / float(steps)
		var a1: float = TAU * float(i + 1) / float(steps)
		var p0: Vector3 = center + basis * Vector3(cos(a0) * rx, sin(a0) * ry, 0.06 * sin(a0 * 2.0))
		var p1: Vector3 = center + basis * Vector3(cos(a1) * rx, sin(a1) * ry, 0.06 * sin(a1 * 2.0))
		p0.y = maxf(p0.y, 4.50)
		p1.y = maxf(p1.y, 4.50)
		_append_quad(st, p0 - thick, p0 + thick, p1 + thick, p1 - thick)
		_append_quad(st, p0 - wide, p0 + wide, p1 + wide, p1 - wide)


static func _append_quad(st: SurfaceTool, v0: Vector3, v1: Vector3, v2: Vector3, v3: Vector3) -> void:
	st.add_vertex(v0)
	st.add_vertex(v1)
	st.add_vertex(v2)
	st.add_vertex(v0)
	st.add_vertex(v2)
	st.add_vertex(v3)


func _outline_material() -> Material:
	if Settings.performance == &"turbo":
		return null
	if _ink_outline_mat == null:
		if ResourceLoader.exists(INK_OUTLINE_PATH):
			var shader: Shader = load(INK_OUTLINE_PATH) as Shader
			if shader != null:
				var smat: ShaderMaterial = ShaderMaterial.new()
				smat.shader = shader
				smat.set_shader_parameter("ink_width", 0.019)
				smat.set_shader_parameter("distance_comp", 0.56)
				smat.set_shader_parameter("max_distance", 148.0)
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
	# Graceful procedural fallback if `.glb` has not been imported yet
	var fallback_root: Node3D = Node3D.new()
	var mi: MeshInstance3D = MeshInstance3D.new()
	var box: BoxMesh = BoxMesh.new()
	box.size = Vector3(8.0, 6.0, 8.0)
	var mat: StandardMaterial3D = StandardMaterial3D.new()
	mat.albedo_color = Color(0.46, 0.36, 0.28)
	mat.roughness = 0.9
	var outline: Material = _outline_material()
	if outline != null:
		mat.next_pass = outline
	box.material = mat
	mi.mesh = box
	mi.position.y = 3.0
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
