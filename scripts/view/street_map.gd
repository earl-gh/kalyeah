class_name StreetMap
extends Node3D
## The barangay street the game is played on (view only; reads MapLayout, holds no
## rules). The play area is the owner's tiled floor (cement.png) over a flat cement
## slab (no markings) with curbs and sidewalks, house fronts, a sari-sari store with an
## umbrella around it; the few cover
## pieces kids hide behind (cardboard walls); each team's electric post in its corner with a chalk base
## circle; and the taped box where each team's tagged kids wait.

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY
const SKY_COLOR: Color = Color(0.74, 0.8, 0.9)
# afternoon with a cool tone (owner): a higher, neutral-cool sun from the front right, a clear blue sky
const SKY_TOP: Color = Color(0.27, 0.54, 0.95)
const SKY_HORIZON: Color = Color(0.78, 0.88, 1.0)
const SUN_ANGLES: Vector3 = Vector3(-46.0, 52.0, 0.0)
const SUN_COLOR: Color = Color(0.9, 0.95, 1.0)
const SUN_ENERGY: float = 1.3
const SHADOW_OPACITY: float = 0.7
## Soft shadow edges (visual pass: 1.6 read as hard dark patches beside the buildings).
const SHADOW_BLUR: float = 2.2
const FILL_ANGLES: Vector3 = Vector3(-30.0, 258.0, 0.0)
const FILL_COLOR: Color = Color(0.64, 0.76, 1.0)
const FILL_ENERGY: float = 0.45
const AMBIENT_COLOR: Color = Color(0.85, 0.91, 1.0)
## Raised from 0.82 (visual pass): the shadow side of the street was muddy; the sun, its colour and the shadow opacity stay the owner's.
const AMBIENT_ENERGY: float = 0.95
const EXPOSURE: float = 1.0
const TONEMAP_WHITE: float = 4.0
const SATURATION: float = 1.1
const CONTRAST: float = 1.03
const BRIGHTNESS: float = 1.0
const RING_HEIGHT: float = 0.03
## The base chalk and the box tape are tinted toward a soft grey so they are calm, not neon (how far: TeamColors.base_soften()).
const SOFT_GREY: Color = Color(0.62, 0.64, 0.7)
## The base circle and the tagged box share one look: a faint team fill and team tape.
## Visual pass: the bases and rescue areas were the faintest marks on the street (fill 0.22, tape 0.16 m); a stronger fill
## that still lets the cement show and a thick chalk edge make the objective read second, after the kids.
const ZONE_FILL_ALPHA: float = 0.34
const TAPE_WIDTH: float = 0.3
const BASE_STEPS: int = 36
## The tagged box has no tint and no tape: cardboard walls close its sides.
const JAIL_WALL_THICKNESS: float = 0.5
## The sidewalks are flat, level with the street (owner: no raised curb).
const CURB_HEIGHT: float = 0.0
const SIDEWALK_WIDTH: float = 1.2
## The street goes on past both ends of the play area (behind a barricade).
## The road, the sidewalks and the ground go on this far past each end of the play area (m); the
## buildings only part of the way (HOUSE_REACH), then the empty road carries on into the haze.
const END_REACH: float = 70.0
const HOUSE_REACH: float = 30.0
## The main screen shows the play area alone as a small model, the game's exact 1:2 shape: the
## cement on a chunky stone base with only the corner props (the electric posts and the cardboard
## walls of the tagged boxes). No sidewalks, no team colours and no street past the ends.
const PLINTH: Color = Color(0.4, 0.43, 0.54)
const PLINTH_HEIGHT: float = 4.0
## The main-screen model sits on a thicker blue platform that reaches this far past it.
const PLATFORM: Color = Color(0.07, 0.16, 0.4)
const PLATFORM_HEIGHT: float = 6.0
const PLATFORM_MARGIN: float = 2.0
## Corner radius of the main-screen model's slab and stone base (the platform's is wider by the margin).
const CORNER_RADIUS: float = 1.8
const HOUSE_SPACING: float = 6.4
## The owner's shopfront models, in the order they repeat, and how wide each is on the street.
const BUILDINGS: Array[String] = ["vulcanizing", "barber_shop", "nena_store"]
## The two houses that fill in between the shopfronts (shop, house, shop, house, ...).
const FILLER_HOUSES: Array[String] = ["house_1", "house_2", "house_3", "karinderya"]
const BUILDING_WIDTH: float = 6.0
## The tricycle terminal stands in the left row of buildings at the top end of the play area.
const TERMINAL: String = "tricycle_terminal"
## Sidewalk pets and things (heights in metres, in proportion to the kids): the cat and the dog lie
## about, the chicken cage stands by a building, the drainage cover (a metre wide) is set in the walk.
const CAT_HEIGHT: float = 0.3
const DOG_HEIGHT: float = 0.62
const CHICKEN_CAGE_HEIGHT: float = 0.9
const DRAINAGE_COVER_WIDTH: float = 0.75
const SHADOW_DISTANCE: float = 400.0
## Painted ground sits just above the road slab.
const PAVER_TILE: float = 2.4
## The slab under the owner's tiled floor (and the street past both ends): dark grey cement.
const CEMENT: Color = Color(0.3, 0.3, 0.31)
## Optional realistic cement art (owner-made 2D image, see docs/ART.md): when this
## file exists it is laid ONCE over the whole play area (stretched, never tiled).
const CEMENT_ART: String = "res://assets/textures/cement.png"
const CEMENT_LIFT: float = 0.004
const STREET_SHADER: Shader = preload("res://shaders/street_cement.gdshader")
## Paving around the whole map, so the street never ends in sky.
const OUTER_MARGIN: float = 40.0
const OUTER_DROP: float = -0.12
const OUTER_TINT: Color = Color(0.62, 0.58, 0.54)
const SARI_SIGN_HEIGHT: float = 1.15
## Which model each cover piece (MapLayout.obstacle_props) is drawn as.
const OBSTACLE_MODELS: Dictionary[StringName, String] = {
	&"bench": "bench", &"cardboard_wall": "cardboard_wall", &"school_zone": "school_zone", &"tent": "rooster_tent",
}
const COVER_HEIGHT: float = 1.15
const POST_HEIGHT: float = 8.5
## The barrier rows across both ends: every street thing at its real height (m), in proportion to
## the kids (who stand 1.85 m, so a real thing is about 1.4 times its true size), side by side
## in this order (the far end starts half way along, so the two rows differ).
const BARRIER_HEIGHTS: Dictionary[String, float] = {
	"fishball_cart": 2.1, "tricycle": 2.4, "bench": 1.1, "crates": 1.1, "sacks": 1.0, "school_zone": 3.0,
	"basketball_pole": 6.0, "ebike": 1.5, "truck_wheels": 1.4, "sewing_machine": 1.3, "ironing_board": 1.2,
	"no_id_no_entry": 1.7, "rooster_tent": 1.4, "plank_fence": 1.8, "roof_wall": 2.9, "roof_wall_2": 2.9,
}
## Things whose long side is their depth: they are turned a quarter so the long side faces the play area.
const BARRIER_TURNED: Array[String] = ["tricycle"]
## Things laid down on their side across the street (the basketball pole, so its whole length is
## the boundary): their BARRIER_HEIGHTS entry is their length along the row.
const BARRIER_LYING: Array[String] = ["basketball_pole"]
## The nanay on the sidewalk by the sari-sari store, outside the play area, facing the street.
## Parked beyond the barrier rows and on the sidewalk, outside the play area (heights in metres).
const JEEP_HEIGHT: float = 2.8
const PEDICAB_HEIGHT: float = 2.4
const ICE_CREAM_CART_HEIGHT: float = 2.5
const NANAY_HEIGHT: float = 2.2
const NANAY_SWAY_DEGREES: float = 4.0
const NANAY_SWAY_SECONDS: float = 3.5
## The smallest gap between two things in a row; the rest of the room is shared out unevenly.
## Things that do not fit in a row stand on the road beyond the end: how far out the first is (m past
## the end line) and how far apart they are.
const FAR_PROPS_START: float = 10.0
const FAR_PROPS_SPACING: float = 6.0
const BARRIER_GAP: float = 0.7
## How far a thing may sit back from the line (m) and turn (degrees), so the row looks set down by hand.
const BARRIER_JITTER: float = 0.5
const BARRIER_TURN: float = 12.0
const BARRIER_SEED: int = 7
## The front of each row sits this far behind the end line of the play area.
const BARRIER_SETBACK: float = 0.15

@export var layout: MapLayout
## The main-screen map model: the play area's cement on a stone base, with the corner props.
@export var diorama: bool = false
## Off when a match scene supplies its own (follow) camera.
@export var show_overview_camera: bool = true
## What stands in each end's barrier row (index 0 = the far end), for tests and the minimap.
var barrier_rows: Array[Array] = []
## The things that did not fit in a row and stand on the road beyond the ends instead.
var far_props: Array[String] = []
## side -> [base tape, base fill, rescue tape, rescue fill] materials (recoloured per viewer).
var _base_materials: Dictionary[int, Array] = {}
## side -> that base's team colour as the viewer sees it (blue yours, red theirs).
var _side_tint: Dictionary[int, Color] = {}
## side -> that base's electric post.
var _posts: Dictionary[int, Node3D] = {}

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


## Afternoon in a cool tone (owner): a high, neutral-cool sun (shorter shadows), a blue fill from the
## other side and a cool blue ambient so the shadows stay blue, a clear blue sky and a pale blue haze
## into the distance. No bloom.
static func make_environment() -> Environment:
	var env: Environment = Environment.new()
	var sky_material: ProceduralSkyMaterial = ProceduralSkyMaterial.new()
	sky_material.sky_top_color = SKY_TOP
	sky_material.sky_horizon_color = SKY_HORIZON
	sky_material.ground_horizon_color = SKY_HORIZON
	sky_material.ground_bottom_color = SKY_HORIZON
	sky_material.sky_curve = 0.25
	sky_material.sun_angle_max = 0.0
	var sky: Sky = Sky.new()
	sky.sky_material = sky_material
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = AMBIENT_COLOR
	env.ambient_light_energy = AMBIENT_ENERGY
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.tonemap_exposure = EXPOSURE
	env.tonemap_white = TONEMAP_WHITE
	# no bloom: it is a blur laid over the bright parts (owner: nothing blurred during gameplay)
	env.glow_enabled = false
	env.adjustment_enabled = true
	env.adjustment_saturation = SATURATION
	env.adjustment_contrast = CONTRAST
	env.adjustment_brightness = BRIGHTNESS
	# no haze: the camera is far above the whole street, and fog by distance would wash all of it out
	env.fog_enabled = false
	return env


## The key sun and the sky fill for a scene (`sun` is reused; the fill is added).
static func _light_scene(parent: Node, sun: DirectionalLight3D) -> void:
	sun.rotation_degrees = SUN_ANGLES
	sun.light_color = SUN_COLOR
	sun.light_energy = SUN_ENERGY
	# real shadows give the toy-town depth; one cheap orthogonal map
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = SHADOW_DISTANCE
	sun.shadow_opacity = SHADOW_OPACITY
	sun.shadow_blur = SHADOW_BLUR
	var fill: DirectionalLight3D = DirectionalLight3D.new()
	fill.name = "SkyFill"
	fill.rotation_degrees = FILL_ANGLES
	fill.light_color = FILL_COLOR
	fill.light_energy = FILL_ENERGY
	fill.shadow_enabled = false
	parent.add_child(fill)


## Colors the bases and boxes for the viewer: `own_side` is blue, the other red
## (teams switch ends every set).
func set_own_side(own_side: int) -> void:
	_turn_posts(own_side)
	for side: int in _base_materials:
		var tint: Color = TeamColors.world_own() if side == own_side else TeamColors.world_enemy()
		_side_tint[side] = tint
		for material: StandardMaterial3D in _base_materials[side]:
			var color: Color = tint.lerp(SOFT_GREY, TeamColors.base_soften())
			color.a = material.albedo_color.a
			material.albedo_color = color


## The post's front (the side with the flyers, the model's +Z): the enemy post faces the middle
## of the play area; your own (blue) post is turned the other way round (owner), so from your
## end you see its flyers too.
func _turn_posts(own_side: int) -> void:
	for side: int in _posts:
		var center: Vector2 = layout.base_center(side)
		var inward: float = atan2(-center.x, -center.y)
		_posts[side].rotation.y = inward + (PI if side == own_side else 0.0)


## The team colour of base `side` as the viewer sees it: blue for yours, red for theirs.
func base_color(side: int) -> Color:
	return _side_tint[side]


## [base tape, base fill, rescue tape, rescue fill] colours for `side` (the tagged box itself has no tint).
func zone_colors(side: int) -> Array[Color]:
	var colors: Array[Color] = []
	for material: StandardMaterial3D in _base_materials[side]:
		colors.append(material.albedo_color)
	return colors


## How far the street goes on past each end of the play area.
func _reach() -> float:
	return 0.0 if diorama else END_REACH


## The dark, matte blue platform under the main-screen model (concentric with its rounded corners).
func _platform() -> ArrayMesh:
	var kit: LowPoly = LowPoly.new()
	var half: float = layout.lane_length / 2.0
	var under: float = -0.1 - PLINTH_HEIGHT
	kit.rounded_slab(Vector3(0.0, under - PLATFORM_HEIGHT / 2.0, -half / 2.0), Vector3(layout.lane_width + PLATFORM_MARGIN * 2.0, PLATFORM_HEIGHT, half + PLATFORM_MARGIN * 2.0), CORNER_RADIUS + PLATFORM_MARGIN, PLATFORM, PLATFORM)
	return kit.commit()


## The scenery's size next to the kids (`MapLayout.scenery_scale`), the cover walls' and the posts' drawn heights. The main-screen model
## (`diorama`) keeps the true sizes: it frames its own little street.
func scenery() -> float:
	return 1.0 if diorama else layout.scenery_scale


func cover_height() -> float:
	return COVER_HEIGHT if diorama else layout.cover_height


func post_height() -> float:
	return POST_HEIGHT if diorama else layout.post_height


func _build_map() -> void:
	_add(_street(), Vector3.ZERO).material_override = street_material()
	if diorama:
		var matte: StandardMaterial3D = StandardMaterial3D.new()
		matte.vertex_color_use_as_albedo = true
		matte.roughness = 1.0
		matte.metallic = 0.0
		matte.metallic_specular = 0.0
		_add(_platform(), Vector3.ZERO).material_override = matte
		_add_corner_props()
		return
	_add_ground_textures()
	for index: int in layout.obstacle_rects().size():
		_add_obstacle(index)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var color: Color = TeamColors.world_own() if side == MapLayout.SIDE_OWN else TeamColors.world_enemy()
		var center: Vector2 = layout.base_center(side)
		var post: Node3D = _add_prop(PropModel.by_height("electric_pole", post_height()), Vector3(center.x, 0.0, center.y))
		_posts[side] = post
		var base: Array[StandardMaterial3D] = _add_base_zone(side, color)
		_add_jail(side)
		var rescue: Array[StandardMaterial3D] = _add_rescue_area(side, color)
		_base_materials[side] = [base[0], base[1], rescue[0], rescue[1]]
		_side_tint[side] = color
	_turn_posts(MapLayout.SIDE_OWN)
	_add_barricades()
	_add_houses()
	_add_details()


## The main-screen model's props: each base's electric post and each tagged box's cardboard walls.
func _add_corner_props() -> void:
	# only the far half of the street is modelled: the enemy post (top right) and your box (top left)
	var center: Vector2 = layout.base_center(MapLayout.SIDE_ENEMY)
	_posts[MapLayout.SIDE_ENEMY] = _add_prop(PropModel.by_height("electric_pole", post_height()), Vector3(center.x, 0.0, center.y))
	_add_jail(MapLayout.SIDE_OWN)
	_turn_posts(MapLayout.SIDE_OWN)


## The look of the street and the sidewalks: code-drawn cement (shaders/street_cement.gdshader)
## over their vertex colours, no image files (owner rule: no generated assets).
func street_material() -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = STREET_SHADER
	material.set_shader_parameter("lane_half_width", layout.lane_width / 2.0)
	material.set_shader_parameter("wear", 0.3 if diorama else 1.0)
	return material


## The cement street (past both ends of the play area) and the sidewalks. There is no white curb
## strip between them (owner): the sidewalk runs right up to the street edge (the sim still stops
## kids at the edge, in the strip `boundary_thickness` wide).
func _street() -> ArrayMesh:
	var kit: LowPoly = LowPoly.new()
	var length: float = layout.lane_length + _reach() * 2.0
	if diorama:
		# the main-screen model is the far half only: the exact lane_width x lane_length / 2 area
		var half: float = layout.lane_length / 2.0
		kit.rounded_slab(Vector3(0.0, -0.05, -half / 2.0), Vector3(layout.lane_width, 0.1, half), CORNER_RADIUS, CEMENT, CEMENT)
		# the chunky stone base the map model stands on
		kit.rounded_slab(Vector3(0.0, -0.1 - PLINTH_HEIGHT / 2.0, -half / 2.0), Vector3(layout.lane_width, PLINTH_HEIGHT, half), CORNER_RADIUS, PLINTH, PLINTH)
		return kit.commit()
	# one flat slab under the street and both sidewalks (owner: no raised curb)
	var reach: float = layout.boundary_thickness + SIDEWALK_WIDTH
	kit.plain_box(Vector3(0.0, -0.05, 0.0), Vector3(layout.lane_width + reach * 2.0, 0.1, length), CEMENT, CEMENT)
	return kit.commit()


## The owner's floor if there is one and paving all around.
func _add_ground_textures() -> void:
	var length: float = layout.lane_length + _reach() * 2.0
	_add_cement_art()
	var outer: MeshInstance3D = StreetArt.ground("pavers", Vector2(layout.lane_width + OUTER_MARGIN * 2.0, length + OUTER_MARGIN * 2.0), PAVER_TILE, OUTER_TINT)
	outer.position = Vector3(0.0, OUTER_DROP, 0.0)
	_geometry.add_child(outer)


## Lays the owner's floor image over all the ground the camera sees (owner): one whole picture on the play area, repeated
## past both ends and under the sides, lined up so the play area holds exactly one copy.
func _add_cement_art() -> void:
	if not ResourceLoader.exists(CEMENT_ART):
		return
	var size: Vector2 = Vector2(layout.lane_width, layout.lane_length + _reach() * 2.0) + Vector2.ONE * OUTER_MARGIN * 2.0
	var art: MeshInstance3D = _floor_patch("CementArt", size, Vector2.ZERO)
	art.position.y = CEMENT_LIFT
	_geometry.add_child(art)


## A flat patch of the owner's floor `size` metres (x, z) centred on `centre` (x, z), its picture repeated every play-area
## size and lined up with the play area.
func _floor_patch(patch_name: String, size: Vector2, centre: Vector2) -> MeshInstance3D:
	var period: Vector2 = Vector2(layout.lane_width, layout.lane_length)
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = size
	var material: StandardMaterial3D = StreetArt.material("cement", size / period)
	material.albedo_color = StreetArt.CEMENT_TINT
	# the picture's edges fall on the play area's edges
	var start: Vector2 = (centre - size / 2.0 + period / 2.0) / period
	material.uv1_offset = Vector3(start.x - floorf(start.x), start.y - floorf(start.y), 0.0)
	plane.material = material
	var patch: MeshInstance3D = MeshInstance3D.new()
	patch.name = patch_name
	patch.mesh = plane
	patch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return patch


## The cover piece `index` (MapLayout.obstacle_rects order), drawn as its prop.
func _add_obstacle(index: int) -> void:
	var rect: Rect2 = layout.obstacle_rects()[index]
	var center: Vector2 = rect.get_center()
	var at: Vector3 = Vector3(center.x, 0.0, center.y)
	# the long side lies along X; a rect that is longer in Z turns the model a quarter
	var turned: bool = rect.size.y > rect.size.x
	var footprint: Vector2 = Vector2(maxf(rect.size.x, rect.size.y), minf(rect.size.x, rect.size.y))
	var id: String = String(OBSTACLE_MODELS.get(layout.obstacle_prop(index), "cardboard_wall"))
	_add_prop(PropModel.fit(id, footprint, cover_height(), not diorama), at, PI / 2.0 if turned else 0.0)


## The base: a faint team-tinted circle round the post, cut off by the street edges,
## with team tape on its curved rim (the same look as the tagged box).
## Returns [tape, fill] materials (recoloured per viewer).
func _add_base_zone(side: int, color: Color) -> Array[StandardMaterial3D]:
	var center: Vector2 = layout.base_center(side)
	var street: Rect2 = layout.lane_rect()
	var rim: Array[Vector2] = []
	var inner: Array[Vector2] = []
	for i: int in BASE_STEPS:
		var angle: float = TAU * float(i) / float(BASE_STEPS)
		var direction: Vector2 = Vector2(cos(angle), sin(angle))
		rim.append((center + direction * layout.base_radius).clamp(street.position, street.end))
		inner.append(center + direction * (layout.base_radius - TAPE_WIDTH))
	var materials: Array[StandardMaterial3D] = _zone_materials(color)
	_add_fill(center, rim, materials[1])
	# the tape runs along the curved rim only (not along the street edges)
	var on_rim: Array[bool] = []
	for point: Vector2 in rim:
		on_rim.append(absf(point.distance_to(center) - layout.base_radius) < 0.01)
	_add_tape(rim, inner, on_rim, materials[0])
	return materials


## The box where a team's tagged kids wait: a square sitting flush in a corner of the street,
## with no tint and no tape. Cardboard walls close its two sides that face the street (the
## other two are the curb and the end line). They are scenery only: nothing is blocked, so a
## teammate walks straight in to rescue.
func _add_jail(side: int) -> void:
	for wall: Rect2 in jail_wall_rects(layout.jail_rect(side), side):
		var center: Vector2 = wall.get_center()
		var turned: bool = wall.size.y > wall.size.x
		var footprint: Vector2 = Vector2(maxf(wall.size.x, wall.size.y), minf(wall.size.x, wall.size.y))
		_add_prop(PropModel.fit("cardboard_wall", footprint, cover_height(), not diorama), Vector3(center.x, 0.0, center.y), PI / 2.0 if turned else 0.0)


## The rescue area round the box: the same look as the base circle (a faint team fill and team
## tape), on the strips round the box and with tape only on its outer sides that face the street
## (yours: bottom and right; theirs: top and left). Returns [tape, fill] materials.
func _add_rescue_area(side: int, color: Color) -> Array[StandardMaterial3D]:
	var materials: Array[StandardMaterial3D] = _zone_materials(color)
	for strip: Rect2 in layout.rescue_strips(side):
		_add_flat_rect(strip, 0.0, materials[1])
	for tape: Rect2 in rescue_tape_rects(layout.rescue_rect(side), side):
		_add_flat_rect(tape, 0.006, materials[0])
	return materials


## The tape round a rescue area: on the two sides that face the street, TAPE_WIDTH wide.
static func rescue_tape_rects(area: Rect2, side: int) -> Array[Rect2]:
	var tape: float = TAPE_WIDTH
	if side == MapLayout.SIDE_OWN:
		return [
			Rect2(area.position.x, area.end.y - tape, area.size.x, tape),
			Rect2(area.end.x - tape, area.position.y, tape, area.size.y - tape),
		]
	return [
		Rect2(area.position.x, area.position.y, area.size.x, tape),
		Rect2(area.position.x, area.position.y + tape, tape, area.size.y - tape),
	]


## A flat, thin rectangle lying on the road (`lift` above the fill height), casting no shadow.
func _add_flat_rect(rect: Rect2, lift: float, material: StandardMaterial3D) -> void:
	var mesh: BoxMesh = BoxMesh.new()
	mesh.size = Vector3(rect.size.x, 0.01 + lift, rect.size.y)
	mesh.material = material
	var center: Vector2 = rect.get_center()
	_add(mesh, Vector3(center.x, RING_HEIGHT + lift / 2.0, center.y)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## The two cardboard walls of a side's box, as rects inside it: yours (top left corner) has
## them on its bottom and right, theirs (bottom right corner) on its top and left. The
## side wall is one wall-thickness shorter so the two do not overlap at the corner.
static func jail_wall_rects(box: Rect2, side: int) -> Array[Rect2]:
	var thick: float = JAIL_WALL_THICKNESS
	var walls: Array[Rect2] = []
	if side == MapLayout.SIDE_OWN:
		walls.append(Rect2(box.position.x, box.end.y - thick, box.size.x, thick))
		walls.append(Rect2(box.end.x - thick, box.position.y, thick, box.size.y - thick))
	else:
		walls.append(Rect2(box.position.x, box.position.y, box.size.x, thick))
		walls.append(Rect2(box.position.x, box.position.y + thick, thick, box.size.y - thick))
	return walls


## [tape, fill] for a base or tagged box: soft team colour, the fill faint.
func _zone_materials(color: Color) -> Array[StandardMaterial3D]:
	var soft: Color = color.lerp(SOFT_GREY, TeamColors.base_soften())
	var tape: StandardMaterial3D = StandardMaterial3D.new()
	tape.albedo_color = soft
	tape.cull_mode = BaseMaterial3D.CULL_DISABLED
	var fill: StandardMaterial3D = StandardMaterial3D.new()
	fill.albedo_color = Color(soft.r, soft.g, soft.b, ZONE_FILL_ALPHA)
	fill.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	fill.cull_mode = BaseMaterial3D.CULL_DISABLED
	# drawn first, so the kids and everything else stand on top of the tint, never under it
	fill.render_priority = -100
	return [tape, fill]


## A flat fan from `center` out to the closed `outline`, just above the road.
func _add_fill(center: Vector2, outline: Array[Vector2], material: StandardMaterial3D) -> void:
	var tool: SurfaceTool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_normal(Vector3.UP)
	for i: int in outline.size():
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % outline.size()]
		for point: Vector2 in [center, b, a]:
			tool.add_vertex(Vector3(point.x, RING_HEIGHT, point.y))
	var mesh: ArrayMesh = tool.commit()
	mesh.surface_set_material(0, material)
	_add(mesh, Vector3.ZERO).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## Tape between the closed `outer` line and the matching `inner` line, on the
## segments whose both ends are marked in `keep`.
func _add_tape(outer: Array[Vector2], inner: Array[Vector2], keep: Array[bool], material: StandardMaterial3D) -> void:
	var tool: SurfaceTool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_normal(Vector3.UP)
	var height: float = RING_HEIGHT + 0.006
	for i: int in outer.size():
		var j: int = (i + 1) % outer.size()
		if not (keep[i] and keep[j]):
			continue
		for point: Vector2 in [outer[i], outer[j], inner[j], outer[i], inner[j], inner[i]]:
			tool.add_vertex(Vector3(point.x, height, point.y))
	var mesh: ArrayMesh = tool.commit()
	mesh.surface_set_material(0, material)
	_add(mesh, Vector3.ZERO).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## How wide (m, along the row) a barrier thing is at its true size.
static func barrier_width(id: String, scale: float = 1.0) -> float:
	if BARRIER_LYING.has(id):
		return BARRIER_HEIGHTS[id] * scale
	var natural: Vector3 = PropModel.size_of(id)
	var side: float = natural.z if BARRIER_TURNED.has(id) else natural.x
	return side * BARRIER_HEIGHTS[id] * scale / natural.y


## The ends of the play area are walled off by a row of street things (a fishball cart, a bench,
## crates, the SLOW DOWN SCHOOL ZONE sign, a basketball hoop, stacked truck tyres, an ironing board
## and the wooden A-frame), each at its true size (never stretched to fill a gap), facing the play
## area, with uneven gaps and a little turned or set back, as if put down by hand.
func _add_barricades() -> void:
	var ids: Array = BARRIER_HEIGHTS.keys()
	for end_index: int in 2:
		var end: float = -1.0 if end_index == 0 else 1.0
		# the same street every match: a fixed seed per end
		var rng: RandomNumberGenerator = RandomNumberGenerator.new()
		rng.seed = BARRIER_SEED + end_index
		var picked: Array[String] = []
		var widths: Array[float] = []
		var used: float = 0.0
		var i: int = end_index * (ids.size() / 2)
		while true:
			var id: String = ids[i % ids.size()]
			var natural: Vector3 = PropModel.size_of(id)
			var width: float = barrier_width(id, scenery())  # its drawn size, never stretched
			var need: float = width + (BARRIER_GAP if not picked.is_empty() else 0.0)
			if used + need > layout.lane_width:
				break
			used += need
			picked.append(id)
			widths.append(width)
			i += 1
		# the room left over goes into uneven gaps (not into the things themselves)
		barrier_rows.append(picked.duplicate())
		var spare: float = layout.lane_width - used
		var shares: Array[float] = []
		var total: float = 0.0
		for k: int in picked.size() + 1:
			shares.append(rng.randf_range(0.4, 1.6))
			total += shares[k]
		var x: float = -layout.lane_width / 2.0 + spare * shares[0] / total
		for k: int in picked.size():
			var id: String = picked[k]
			var model: Node3D = PropModel.by_height(id, BARRIER_HEIGHTS[id] * scenery())
			var turned: bool = BARRIER_TURNED.has(id)
			var natural: Vector3 = PropModel.size_of(id)
			var depth: float = (natural.x if turned else natural.z) * model.scale.z
			# set down by hand: a little in or out of line and a little turned
			var setback: float = BARRIER_SETBACK + rng.randf_range(0.0, BARRIER_JITTER)
			# turning a long thing swings its corners toward the play area: leave room for that
			var swing: float = widths[k] * sin(deg_to_rad(BARRIER_TURN)) / 2.0
			var at_z: float = end * (layout.lane_length / 2.0 + setback + depth / 2.0 + swing)
			var turn: float = deg_to_rad(rng.randf_range(-BARRIER_TURN, BARRIER_TURN))
			_put_barrier(model, id, Vector3(x + widths[k] / 2.0, 0.0, at_z), end, turn)
			x += widths[k] + BARRIER_GAP + spare * shares[k + 1] / total
		# what did not fit in the row stands about on the road beyond the end instead
		var z_far: float = layout.lane_length / 2.0 + FAR_PROPS_START * scenery()
		var side: float = 1.0 if rng.randf() < 0.5 else -1.0
		for id: String in ids:
			if picked.has(id):
				continue
			var width: float = barrier_width(id, scenery())
			var room: float = layout.lane_width / 2.0 - width / 2.0 - 0.5
			var far_x: float = clampf(side * rng.randf_range(0.2, 1.0) * room, -room, room)
			var far_turn: float = deg_to_rad(rng.randf_range(-BARRIER_TURN, BARRIER_TURN) * 2.0)
			_put_barrier(PropModel.by_height(id, BARRIER_HEIGHTS[id] * scenery()), id, Vector3(far_x, 0.0, end * z_far), end, far_turn)
			far_props.append(id)
			side = -side
			z_far += FAR_PROPS_SPACING * scenery()


## Sets one barrier thing down at `at` (the middle of it), facing the play area at `end`.
func _put_barrier(model: Node3D, id: String, at: Vector3, end: float, turn: float) -> void:
	var yaw: float = (0.0 if end < 0.0 else PI) + (PI / 2.0 if BARRIER_TURNED.has(id) else 0.0)
	var placed: Node3D = _add_prop(model, at, yaw + turn)
	if BARRIER_LYING.has(id):
		# on its side: the pole's length runs along the row, its width becomes its height
		var natural: Vector3 = PropModel.size_of(id)
		placed.rotation = Vector3(0.0, turn, -PI / 2.0)
		var along: Vector3 = Vector3(cos(turn), 0.0, -sin(turn))
		placed.position = Vector3(at.x, natural.x * model.scale.x / 2.0, at.z) - along * (barrier_width(id, scenery()) / 2.0)


## The owner's three shopfronts (Nena store, vulcanizing, barber shop) with his two houses between them, along both sides of the street, running on past
## the ends (none across the ends), one of them (the Nena store) where the sari-sari store stood.
func _add_houses() -> void:
	var half_w: float = layout.lane_width / 2.0
	var half_l: float = layout.lane_length / 2.0 + HOUSE_REACH
	var front: float = half_w + layout.boundary_thickness + SIDEWALK_WIDTH
	var store_z: float = -layout.lane_length * 0.18
	# the top end of the play area, a little in from its end line
	var terminal_z: float = -layout.lane_length / 2.0 + 3.0
	var variant: int = 0
	# the shopfronts rotate through the slots along the play area only (so every shop shows however wide the lots are drawn)
	var shops: int = 0
	for side: float in [-1.0, 1.0]:
		var spacing: float = HOUSE_SPACING * scenery()
		var z: float = -half_l + spacing / 2.0
		var played: int = 0
		while z < half_l:
			var id: String = FILLER_HOUSES[variant % FILLER_HOUSES.size()]
			if absf(z) <= layout.lane_length / 2.0:
				# beside the play area: a shop, a house, a shop, ... (beyond its ends only houses, repeated: no shops, no terminal)
				id = BUILDINGS[shops % BUILDINGS.size()] if played % 2 == 0 else FILLER_HOUSES[(played / 2) % FILLER_HOUSES.size()]
				played += 1
			if side < 0.0 and absf(z - store_z) < spacing / 2.0:
				id = "nena_store"
				z = store_z
			elif side < 0.0 and absf(z - terminal_z) < spacing / 2.0:
				id = TERMINAL
				z = terminal_z
			if BUILDINGS.has(id):
				shops += 1
			var natural: Vector3 = PropModel.size_of(id)
			var scale_to: float = BUILDING_WIDTH * scenery() / natural.x
			var depth: float = natural.z * scale_to
			# the front faces the street: +X on the left side, -X on the right
			var yaw: float = PI / 2.0 if side < 0.0 else -PI / 2.0
			_add_prop(PropModel.make(id, Vector3.ONE * scale_to), Vector3(side * (front + depth / 2.0 + 0.2), 0.0, z), yaw)
			variant += 1
			z += spacing


## Life around the street (no power poles or bunting, owner):
## the store's bench and the nanay, the owner's pets and things. The play area itself
## stays plain cement.
func _add_details() -> void:
	var half_w: float = layout.lane_width / 2.0
	var half_l: float = layout.lane_length / 2.0
	var walk_x: float = half_w + layout.boundary_thickness + SIDEWALK_WIDTH / 2.0
	var store_z: float = -layout.lane_length * 0.18
	_add_prop(PropModel.fit("bench", Vector2(2.0, 0.7) * scenery(), 1.1 * scenery(), true), Vector3(-walk_x - 0.3, CURB_HEIGHT, store_z - 1.0), PI / 2.0)
	# the nanay stands by the bench, a little swaying as she waits
	var nanay: Node3D = _add_prop(PropModel.by_height("nanay", NANAY_HEIGHT * scenery()), Vector3(-walk_x, CURB_HEIGHT, store_z + 0.3), PI / 2.0)
	nanay.name = "Nanay"
	nanay.add_child(DecorativeSway.new(nanay, NANAY_SWAY_DEGREES, NANAY_SWAY_SECONDS))
	_add_parked_vehicles(half_w, half_l, walk_x)
	_add_sidewalk_props(walk_x)


## The owner's cat, dog, chicken cage and drainage covers on the sidewalks, facing the street.
func _add_sidewalk_props(walk_x: float) -> void:
	var left: float = PI / 2.0
	var right: float = -PI / 2.0
	_add_prop(PropModel.by_height("cat", CAT_HEIGHT * scenery()), Vector3(-walk_x - 0.2, CURB_HEIGHT, -3.0), left)
	_add_prop(PropModel.by_height("dog", DOG_HEIGHT * scenery()), Vector3(walk_x + 0.2, CURB_HEIGHT, -9.0), right)
	_add_prop(PropModel.by_height("chicken_cage", CHICKEN_CAGE_HEIGHT * scenery()), Vector3(walk_x + 0.3, CURB_HEIGHT, 14.0), right)
	for spot: Vector2 in [Vector2(-walk_x, -17.0), Vector2(-walk_x, 7.0), Vector2(walk_x, 0.0), Vector2(walk_x, 19.0)]:
		_add_prop(PropModel.make("drainage_cover", Vector3.ONE * (DRAINAGE_COVER_WIDTH * scenery() / PropModel.size_of("drainage_cover").x)), Vector3(spot.x, CURB_HEIGHT, spot.y))


## The jeepney parked across the street behind the top barrier row, the pedicab behind the bottom
## one, and the ice cream cart on the right sidewalk (all outside the play area).
func _add_parked_vehicles(half_w: float, half_l: float, walk_x: float) -> void:
	var jeep: Node3D = PropModel.by_height("jeep", JEEP_HEIGHT * scenery())
	jeep.name = "Jeep"
	var jeep_size: Vector3 = PropModel.size_of("jeep") * jeep.scale
	_add_prop(jeep, Vector3(-half_w * 0.3, 0.0, -(half_l + BARRIER_SETBACK + BARRIER_JITTER + 2.0 + jeep_size.z / 2.0)))
	var pedicab: Node3D = PropModel.by_height("pedicab", PEDICAB_HEIGHT * scenery())
	pedicab.name = "Pedicab"
	var pedicab_size: Vector3 = PropModel.size_of("pedicab") * pedicab.scale
	_add_prop(pedicab, Vector3(half_w * 0.35, 0.0, half_l + BARRIER_SETBACK + BARRIER_JITTER + 2.0 + pedicab_size.x / 2.0), PI / 2.0)
	var cart: Node3D = PropModel.by_height("ice_cream_cart", ICE_CREAM_CART_HEIGHT * scenery())
	cart.name = "IceCreamCart"
	_add_prop(cart, Vector3(walk_x, CURB_HEIGHT, -half_l * 0.6), -PI / 2.0)


func _add(mesh: Mesh, at: Vector3, yaw: float = 0.0) -> MeshInstance3D:
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.mesh = mesh
	instance.position = at
	instance.rotation.y = yaw
	_geometry.add_child(instance)
	return instance


func _add_prop(model: Node3D, at: Vector3, yaw: float = 0.0) -> Node3D:
	model.position = at
	model.rotation.y = yaw
	_geometry.add_child(model)
	return model


func _setup_camera() -> void:
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.keep_aspect = Camera3D.KEEP_HEIGHT
	_camera.size = layout.overview_size
	_camera.rotation_degrees = Vector3(-90.0, 0.0, 0.0)
	_camera.position = Vector3(0.0, layout.overview_height, 0.0)
	_camera.environment = make_environment()


## The map's own Exit button (map preview); a match uses its settings menu instead.
func show_back_button(visible_now: bool) -> void:
	_back_button.visible = visible_now


func _on_back_pressed() -> void:
	get_tree().change_scene_to_file("res://scenes/ui/title_screen.tscn")
