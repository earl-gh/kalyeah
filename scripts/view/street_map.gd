class_name StreetMap
extends Node3D
## The barangay street the game is played on (view only; reads MapLayout, holds no
## rules). The play area is one flat, dark grey cement slab (no tiles or
## markings) with curbs and sidewalks, house fronts, a sari-sari store with an
## umbrella around it; the few cover
## pieces kids hide behind (cardboard walls); each team's electric post in its corner with a chalk base
## circle; and the taped box where each team's tagged kids wait.

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY
const SKY_COLOR: Color = Color(0.74, 0.8, 0.9)
# cinematic morning (see make_environment)
const SUN_ANGLES: Vector3 = Vector3(-30.0, 52.0, 0.0)
const SUN_COLOR: Color = Color(1.0, 0.88, 0.7)
const SUN_ENERGY: float = 1.15
const SHADOW_OPACITY: float = 0.62
const SHADOW_BLUR: float = 1.6
const FILL_ANGLES: Vector3 = Vector3(-30.0, 258.0, 0.0)
const FILL_COLOR: Color = Color(0.58, 0.7, 1.0)
const FILL_ENERGY: float = 0.38
const AMBIENT_COLOR: Color = Color(0.78, 0.86, 1.0)
const AMBIENT_ENERGY: float = 0.82
const EXPOSURE: float = 0.94
const TONEMAP_WHITE: float = 4.0
const GLOW_INTENSITY: float = 0.18
const GLOW_BLOOM: float = 0.03
const GLOW_THRESHOLD: float = 1.1
const SATURATION: float = 0.9
const CONTRAST: float = 0.97
const BRIGHTNESS: float = 0.97
const HAZE_COLOR: Color = Color(1.0, 0.9, 0.8)
const HAZE_DENSITY: float = 0.002
const RING_HEIGHT: float = 0.03
## The base chalk and the box tape are tinted toward a soft grey so they are calm, not neon.
const SOFT_GREY: Color = Color(0.62, 0.64, 0.7)
const BASE_SOFTEN: float = 0.3
## The base circle and the tagged box share one look: a faint team fill and team tape.
const ZONE_FILL_ALPHA: float = 0.22
const TAPE_WIDTH: float = 0.16
const BASE_STEPS: int = 36
## The tagged box has no tint and no tape: cardboard walls close its sides.
const JAIL_WALL_THICKNESS: float = 0.5
## How high the sidewalks stand above the street (owner: raised, was 0.18 m).
const CURB_HEIGHT: float = 0.4
const SIDEWALK_WIDTH: float = 2.4
## The street goes on past both ends of the play area (behind a barricade).
const END_REACH: float = 16.0
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
const BUILDING_WIDTH: float = 6.0
const SHADOW_DISTANCE: float = 75.0
## Painted ground sits just above the coloured road and sidewalk boxes.
const PAVER_TILE: float = 2.4
## The play area is one flat, dark grey cement slab (owner): no repeating tiles.
const CEMENT: Color = Color(0.3, 0.3, 0.31)
## Optional realistic cement art (owner-made 2D image, see docs/ART.md): when this
## file exists it is laid ONCE over the whole play area (stretched, never tiled).
const CEMENT_ART: String = "res://assets/textures/cement.png"
const CEMENT_LIFT: float = 0.004
## The sidewalks are plain gray cement (owner), lighter than the dark street; their raised
## edge facing the street is a darker gray, not a light line.
const SIDEWALK_CEMENT: Color = Color(0.4, 0.4, 0.42)
const SIDEWALK_EDGE: Color = Color(0.3, 0.3, 0.32)
## Optional owner-made 2D images for the sidewalks (docs/ART.md "Ground art"): when a file exists
## it is laid on the sidewalk top / on the raised face toward the street, repeated every
## *_TILE metres along the street. Without them the sidewalks stay flat gray cement.
const SIDEWALK_TOP_ART: String = "res://assets/textures/sidewalk_top.png"
const SIDEWALK_SIDE_ART: String = "res://assets/textures/sidewalk_side.png"
const SIDEWALK_TOP_TILE: float = 3.0
const SIDEWALK_SIDE_TILE: float = 3.0
const ART_LIFT: float = 0.003
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
	"fishball_cart": 2.0, "tricycle": 2.3, "bench": 0.7, "crates": 0.8, "sacks": 0.9, "school_zone": 1.8,
	"basketball_pole": 4.2, "ebike": 1.6, "truck_wheels": 1.4, "sewing_machine": 1.1, "ironing_board": 1.0,
	"no_id_no_entry": 1.7, "rooster_tent": 1.4,
}
## Things whose long side is their depth: they are turned a quarter so the long side faces the play area.
const BARRIER_TURNED: Array[String] = ["tricycle"]
## The nanay on the sidewalk by the sari-sari store, outside the play area, facing the street.
## Parked beyond the barrier rows and on the sidewalk, outside the play area (heights in metres).
const JEEP_HEIGHT: float = 2.8
const PEDICAB_HEIGHT: float = 2.4
const ICE_CREAM_CART_HEIGHT: float = 2.1
const NANAY_HEIGHT: float = 2.2
const NANAY_SWAY_DEGREES: float = 4.0
const NANAY_SWAY_SECONDS: float = 3.5
## The smallest gap between two things in a row; the rest of the room is shared out unevenly.
const BARRIER_GAP: float = 0.25
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


## Cinematic morning: a low golden sun (long soft shadows), a cool blue fill from
## the other side and a soft lavender ambient so the shadows stay blue against the
## warm light, a gentle bloom on the highlights and a golden morning mist into the
## distance.
static func make_environment() -> Environment:
	var env: Environment = Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = SKY_COLOR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = AMBIENT_COLOR
	env.ambient_light_energy = AMBIENT_ENERGY
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.tonemap_exposure = EXPOSURE
	env.tonemap_white = TONEMAP_WHITE
	env.glow_enabled = true
	env.glow_intensity = GLOW_INTENSITY
	env.glow_bloom = GLOW_BLOOM
	env.glow_hdr_threshold = GLOW_THRESHOLD
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SCREEN
	env.adjustment_enabled = true
	env.adjustment_saturation = SATURATION
	env.adjustment_contrast = CONTRAST
	env.adjustment_brightness = BRIGHTNESS
	env.fog_enabled = true
	env.fog_light_color = HAZE_COLOR
	env.fog_density = HAZE_DENSITY
	env.fog_sky_affect = 0.0
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
		var tint: Color = OWN_COLOR if side == own_side else ENEMY_COLOR
		_side_tint[side] = tint
		for material: StandardMaterial3D in _base_materials[side]:
			var color: Color = tint.lerp(SOFT_GREY, BASE_SOFTEN)
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
		var color: Color = OWN_COLOR if side == MapLayout.SIDE_OWN else ENEMY_COLOR
		var center: Vector2 = layout.base_center(side)
		var post: Node3D = _add_prop(PropModel.by_height("electric_pole", POST_HEIGHT), Vector3(center.x, 0.0, center.y))
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
	_posts[MapLayout.SIDE_ENEMY] = _add_prop(PropModel.by_height("electric_pole", POST_HEIGHT), Vector3(center.x, 0.0, center.y))
	_add_jail(MapLayout.SIDE_OWN)
	_turn_posts(MapLayout.SIDE_OWN)


## The look of the street and the sidewalks: code-drawn cement (shaders/street_cement.gdshader)
## over their vertex colours, no image files (owner rule: no generated assets).
func street_material() -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = STREET_SHADER
	material.set_shader_parameter("lane_half_width", layout.lane_width / 2.0)
	material.set_shader_parameter("curb_height", CURB_HEIGHT)
	material.set_shader_parameter("wear", 0.3 if diorama else 1.0)
	return material


## The cement street (past both ends of the play area) and the sidewalks. There is no white curb
## strip between them (owner): the sidewalk runs right up to the street edge (the sim still stops
## kids at the edge, in the strip `boundary_thickness` wide).
func _street() -> ArrayMesh:
	var kit: LowPoly = LowPoly.new()
	var half_w: float = layout.lane_width / 2.0
	var length: float = layout.lane_length + _reach() * 2.0
	if diorama:
		# the main-screen model is the far half only: the exact lane_width x lane_length / 2 area (1:1 when the street is 1:2)
		var half: float = layout.lane_length / 2.0
		kit.rounded_slab(Vector3(0.0, -0.05, -half / 2.0), Vector3(layout.lane_width, 0.1, half), CORNER_RADIUS, CEMENT, CEMENT)
		# the chunky stone base the map model stands on
		kit.rounded_slab(Vector3(0.0, -0.1 - PLINTH_HEIGHT / 2.0, -half / 2.0), Vector3(layout.lane_width, PLINTH_HEIGHT, half), CORNER_RADIUS, PLINTH, PLINTH)
		return kit.commit()
	kit.plain_box(Vector3(0.0, -0.05, 0.0), Vector3(layout.lane_width, 0.1, length), CEMENT, CEMENT)
	for side: float in [-1.0, 1.0]:
		var reach: float = layout.boundary_thickness + SIDEWALK_WIDTH
		kit.plain_box(Vector3(side * (half_w + reach / 2.0), CURB_HEIGHT / 2.0 - 0.02, 0.0), Vector3(reach, CURB_HEIGHT, length), SIDEWALK_EDGE, SIDEWALK_CEMENT)
	return kit.commit()


## The cement art if there is one and paving all around (the sidewalks are plain gray cement).
func _add_ground_textures() -> void:
	var half_w: float = layout.lane_width / 2.0
	var length: float = layout.lane_length + _reach() * 2.0
	_add_cement_art()
	add_sidewalk_art(_owner_art(SIDEWALK_TOP_ART), _owner_art(SIDEWALK_SIDE_ART))
	var outer: MeshInstance3D = StreetArt.ground("pavers", Vector2(layout.lane_width + OUTER_MARGIN * 2.0, length + OUTER_MARGIN * 2.0), PAVER_TILE, OUTER_TINT)
	outer.position = Vector3(0.0, OUTER_DROP, 0.0)
	_geometry.add_child(outer)


## An owner-made image by path, or null while it does not exist yet.
static func _owner_art(path: String) -> Texture2D:
	return load(path) as Texture2D if ResourceLoader.exists(path) else null


## Lays the owner's sidewalk images, if given: `top` on both sidewalk tops, `face` on the raised
## face of each sidewalk toward the street (its image covers the whole face height).
func add_sidewalk_art(top: Texture2D, face: Texture2D) -> void:
	var half_w: float = layout.lane_width / 2.0
	var length: float = layout.lane_length + _reach() * 2.0
	var reach: float = layout.boundary_thickness + SIDEWALK_WIDTH
	for side: float in [-1.0, 1.0]:
		if top != null:
			var plane: PlaneMesh = PlaneMesh.new()
			plane.size = Vector2(reach, length)
			plane.material = StreetArt.material_for(top, Vector2(reach, length) / SIDEWALK_TOP_TILE)
			var top_art: MeshInstance3D = MeshInstance3D.new()
			top_art.name = "SidewalkTopLeft" if side < 0.0 else "SidewalkTopRight"
			top_art.mesh = plane
			top_art.position = Vector3(side * (half_w + reach / 2.0), CURB_HEIGHT - 0.02 + ART_LIFT, 0.0)
			top_art.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			_geometry.add_child(top_art)
		if face != null:
			var quad: QuadMesh = QuadMesh.new()
			quad.size = Vector2(length, CURB_HEIGHT)
			quad.material = StreetArt.material_for(face, Vector2(length / SIDEWALK_SIDE_TILE, 1.0))
			var face_art: MeshInstance3D = MeshInstance3D.new()
			face_art.name = "SidewalkSideLeft" if side < 0.0 else "SidewalkSideRight"
			face_art.mesh = quad
			# the quad faces +Z: turn it to face the middle of the street
			face_art.rotation.y = -side * PI / 2.0
			face_art.position = Vector3(side * (half_w - ART_LIFT), CURB_HEIGHT / 2.0 - 0.02, 0.0)
			face_art.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			_geometry.add_child(face_art)


## Lays the owner's cement image once across the play area (no tiling), if present.
func _add_cement_art() -> void:
	if not ResourceLoader.exists(CEMENT_ART):
		return
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = Vector2(layout.lane_width, layout.lane_length)
	var material: StandardMaterial3D = StreetArt.material("cement", Vector2.ONE)
	plane.material = material
	var art: MeshInstance3D = MeshInstance3D.new()
	art.name = "CementArt"
	art.mesh = plane
	art.position.y = CEMENT_LIFT
	art.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_geometry.add_child(art)


## The cover piece `index` (MapLayout.obstacle_rects order), drawn as its prop.
func _add_obstacle(index: int) -> void:
	var rect: Rect2 = layout.obstacle_rects()[index]
	var center: Vector2 = rect.get_center()
	var at: Vector3 = Vector3(center.x, 0.0, center.y)
	# the long side lies along X; a rect that is longer in Z turns the model a quarter
	var turned: bool = rect.size.y > rect.size.x
	var footprint: Vector2 = Vector2(maxf(rect.size.x, rect.size.y), minf(rect.size.x, rect.size.y))
	var id: String = String(OBSTACLE_MODELS.get(layout.obstacle_prop(index), "cardboard_wall"))
	_add_prop(PropModel.fit(id, footprint, COVER_HEIGHT), at, PI / 2.0 if turned else 0.0)


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
		_add_prop(PropModel.fit("cardboard_wall", footprint, COVER_HEIGHT), Vector3(center.x, 0.0, center.y), PI / 2.0 if turned else 0.0)


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
	var soft: Color = color.lerp(SOFT_GREY, BASE_SOFTEN)
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
			var side: float = natural.z if BARRIER_TURNED.has(id) else natural.x
			var width: float = side * BARRIER_HEIGHTS[id] / natural.y  # true size, never stretched
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
			var model: Node3D = PropModel.by_height(id, BARRIER_HEIGHTS[id])
			var turned: bool = BARRIER_TURNED.has(id)
			var natural: Vector3 = PropModel.size_of(id)
			var depth: float = (natural.x if turned else natural.z) * model.scale.z
			# set down by hand: a little in or out of line and a little turned
			var setback: float = BARRIER_SETBACK + rng.randf_range(0.0, BARRIER_JITTER)
			# turning a long thing swings its corners toward the play area: leave room for that
			var swing: float = widths[k] * sin(deg_to_rad(BARRIER_TURN)) / 2.0
			var at_z: float = end * (layout.lane_length / 2.0 + setback + depth / 2.0 + swing)
			var turn: float = deg_to_rad(rng.randf_range(-BARRIER_TURN, BARRIER_TURN))
			var yaw: float = (0.0 if end < 0.0 else PI) + (PI / 2.0 if turned else 0.0)
			_add_prop(model, Vector3(x + widths[k] / 2.0, 0.0, at_z), yaw + turn)
			x += widths[k] + BARRIER_GAP + spare * shares[k + 1] / total


## The owner's three shopfronts (Nena store, vulcanizing, barber shop) along both sides of the street, running on past
## the ends (none across the ends), one of them (the Nena store) where the sari-sari store stood.
func _add_houses() -> void:
	var half_w: float = layout.lane_width / 2.0
	var half_l: float = layout.lane_length / 2.0 + END_REACH
	var front: float = half_w + layout.boundary_thickness + SIDEWALK_WIDTH
	var store_z: float = -layout.lane_length * 0.18
	var variant: int = 0
	for side: float in [-1.0, 1.0]:
		var z: float = -half_l + HOUSE_SPACING / 2.0
		while z < half_l:
			var id: String = BUILDINGS[variant % BUILDINGS.size()]
			if side < 0.0 and absf(z - store_z) < HOUSE_SPACING / 2.0:
				id = "nena_store"
				z = store_z
			var natural: Vector3 = PropModel.size_of(id)
			var scale_to: float = BUILDING_WIDTH / natural.x
			var depth: float = natural.z * scale_to
			# the front faces the street: +X on the left side, -X on the right
			var yaw: float = PI / 2.0 if side < 0.0 else -PI / 2.0
			_add_prop(PropModel.make(id, Vector3.ONE * scale_to), Vector3(side * (front + depth / 2.0 + 0.2), 0.0, z), yaw)
			variant += 1
			z += HOUSE_SPACING


## Life around the street (no power poles or bunting, owner):
## laundry, the store's umbrella and bench, drums and trees. The play area itself
## stays plain cement.
func _add_details() -> void:
	var half_w: float = layout.lane_width / 2.0
	var half_l: float = layout.lane_length / 2.0
	var walk_x: float = half_w + layout.boundary_thickness + SIDEWALK_WIDTH / 2.0
	var store_z: float = -layout.lane_length * 0.18
	_add(Props.umbrella(), Vector3(-walk_x, CURB_HEIGHT, store_z + 1.6))
	_add_prop(PropModel.fit("bench", Vector2(2.0, 0.7), 1.0), Vector3(-walk_x - 0.3, CURB_HEIGHT, store_z - 1.0), PI / 2.0)
	# the nanay stands between the umbrella and the bench, a little swaying as she waits
	var nanay: Node3D = _add_prop(PropModel.by_height("nanay", NANAY_HEIGHT), Vector3(-walk_x, CURB_HEIGHT, store_z + 0.3), PI / 2.0)
	nanay.name = "Nanay"
	var sway: Tween = nanay.create_tween().set_loops()
	var turned_in: float = nanay.rotation.y
	sway.tween_property(nanay, "rotation:y", turned_in + deg_to_rad(NANAY_SWAY_DEGREES), NANAY_SWAY_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	sway.tween_property(nanay, "rotation:y", turned_in - deg_to_rad(NANAY_SWAY_DEGREES), NANAY_SWAY_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_add_parked_vehicles(half_w, half_l, walk_x)
	for lz: float in [-half_l * 0.7, half_l * 0.3]:
		_add(Props.laundry_line(4.0), Vector3(-walk_x - 0.6, CURB_HEIGHT, lz))
	for lz: float in [-half_l * 0.25, half_l * 0.75]:
		_add(Props.laundry_line(3.0), Vector3(walk_x + 0.6, CURB_HEIGHT, lz))
	for spot: Vector3 in [Vector3(walk_x, CURB_HEIGHT, 6.0), Vector3(-walk_x, CURB_HEIGHT, 15.0), Vector3(walk_x, CURB_HEIGHT, -20.0)]:
		_add(Props.drum(), spot)
	for pz: float in [-half_l * 0.85, half_l * 0.5]:
		_add(Props.potted_plant(), Vector3(walk_x + 0.5, CURB_HEIGHT, pz))
		_add(Props.potted_plant(), Vector3(-walk_x - 0.5, CURB_HEIGHT, -pz))
	var tree_x: float = half_w + layout.boundary_thickness + SIDEWALK_WIDTH + 7.0
	for tz: float in [-30.0, -14.0, 4.0, 22.0]:
		_add(Props.mango_tree(), Vector3(-tree_x - 1.0, 0.0, tz))
		_add(Props.banana_plant(), Vector3(tree_x, 0.0, tz + 6.0))


## The jeepney parked across the street behind the top barrier row, the pedicab behind the bottom
## one, and the ice cream cart on the right sidewalk (all outside the play area).
func _add_parked_vehicles(half_w: float, half_l: float, walk_x: float) -> void:
	var jeep: Node3D = PropModel.by_height("jeep", JEEP_HEIGHT)
	jeep.name = "Jeep"
	var jeep_size: Vector3 = PropModel.size_of("jeep") * jeep.scale
	_add_prop(jeep, Vector3(-half_w * 0.3, 0.0, -(half_l + BARRIER_SETBACK + BARRIER_JITTER + 2.0 + jeep_size.z / 2.0)))
	var pedicab: Node3D = PropModel.by_height("pedicab", PEDICAB_HEIGHT)
	pedicab.name = "Pedicab"
	var pedicab_size: Vector3 = PropModel.size_of("pedicab") * pedicab.scale
	_add_prop(pedicab, Vector3(half_w * 0.35, 0.0, half_l + BARRIER_SETBACK + BARRIER_JITTER + 2.0 + pedicab_size.x / 2.0), PI / 2.0)
	var cart: Node3D = PropModel.by_height("ice_cream_cart", ICE_CREAM_CART_HEIGHT)
	cart.name = "IceCreamCart"
	_add_prop(cart, Vector3(walk_x, CURB_HEIGHT, -half_l * 0.6), -PI / 2.0)


func _add_sign(texture_name: String, height: float, at: Vector3) -> void:
	var sign: Sprite3D = StreetArt.billboard(texture_name, height)
	sign.position = at
	_geometry.add_child(sign)


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
