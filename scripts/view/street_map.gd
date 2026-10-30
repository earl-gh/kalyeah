class_name StreetMap
extends Node3D
## The barangay street the game is played on (view only; reads MapLayout, holds no
## rules). The play area is the owner's tiled floor (cement.png) over a flat cement
## slab (no markings) with curbs and sidewalks, house fronts, a sari-sari store with an
## umbrella around it; the few cover
## pieces kids hide behind (cardboard walls); each team's electric post in its corner with a chalk base
## circle; and the rounded, taped rescue area against the street's side edge, level with the sidewalk where each team's tagged kids wait.

const OWN_COLOR: Color = Palette.TEAM_OWN
const ENEMY_COLOR: Color = Palette.TEAM_ENEMY
# Light for the 3D models only (docs/ART.md "Light and colour by asset class"). The floor and the zone marks are unshaded and
# are drawn exactly as made, so none of them reads these numbers; and the Environment has no tonemap curve, saturation or contrast
# to bend them. Clash Royale's model look: a warm key light and a bright warm ambient so the shaded side stays light and soft, a faint
# cool fill from behind; the three together put an upward face at about its own colour and a camera-facing side a little under it.
const SKY_COLOR: Color = Color(0.74, 0.8, 0.9)
const SKY_TOP: Color = Color(0.27, 0.54, 0.95)
const SKY_HORIZON: Color = Color(0.78, 0.88, 1.0)
const SUN_ANGLES: Vector3 = Vector3(-46.0, 52.0, 0.0)
const SUN_COLOR: Color = Color(1.0, 0.96, 0.88)
const SUN_ENERGY: float = 0.52
## How far the (invisible) shadow pass reaches.
const SHADOW_DISTANCE: float = 400.0
const FILL_ANGLES: Vector3 = Vector3(-30.0, 258.0, 0.0)
const FILL_COLOR: Color = Color(0.75, 0.85, 1.0)
const FILL_ENERGY: float = 0.1
const AMBIENT_COLOR: Color = Color(1.0, 0.97, 0.92)
const AMBIENT_ENERGY: float = 0.63
const RING_HEIGHT: float = 0.03
## Visual pass: the bases and rescue areas were the faintest marks on the street (fill 0.22, tape 0.16 m); a stronger fill
## that still lets the cement show makes the objective read second, after the kids. The chalk edge is half of the 0.3 m it was
## (owner: thinner), the one width of every base and rescue area outline.
const ZONE_FILL_ALPHA: float = 0.34
const TAPE_WIDTH: float = 0.15
## A rescue area's fill is stronger than a base's (owner: its white countdown mark was too transparent): 0.34 before, now over half
## opaque, so the white box and the number on it read at once while the cement still shows through.
const RESCUE_FILL_ALPHA: float = 0.55
## While its countdown runs a rescue area is white and half see-through (owner): the tint, the outline and the number on it all draw at this
## alpha, so a pixel of each is the white blended half and half with the floor. Once ready it is the team's colour as before (full outline,
## `RESCUE_FILL_ALPHA` fill).
const RESCUE_WHITE_ALPHA: float = 0.5
## The countdown number is a mark on the ground like the area it is in (owner: a kid standing on it covers it): a flat Label3D lying on the
## street at the area's middle, unshaded, `RESCUE_COUNT_HEIGHT` metres tall (an em), lifted just above the tint and the outline.
const RESCUE_COUNT_HEIGHT: float = 1.6
const RESCUE_COUNT_FONT: int = 96
const RESCUE_COUNT_LIFT: float = RING_HEIGHT + 0.02
## It draws after the area's tint (priority -100) and outline (-99), and before every other see-through mark of the street (priority 0 and up: the
## kids' rings and shadows, the skill areas), so those draw over it; the kids themselves are solid and cover it by depth.
const RESCUE_COUNT_PRIORITY: int = -98
const RESCUE_OUTLINE_PRIORITY: int = -99
## The camera looks down at this angle (`CameraRules.pitch_degrees`), which squeezes a flat mark's height by its sine: the number is stretched
## by the inverse so its digits look as tall as they are wide.
const COUNT_PITCH_DEGREES: float = 45.0
## Bases and rescue areas are rounded rectangles on the street, each with a faint tint and a full outline in a fixed team colour
## (blue yours, red theirs: owner).
const RESCUE_CORNER_RADIUS: float = 0.9
const ZONE_CORNER_STEPS: int = 6
## The sidewalks are flat, level with the street (owner: no raised curb).
const CURB_HEIGHT: float = 0.0
const SIDEWALK_WIDTH: float = 2.4
## The street goes on past both ends of the play area (behind a barricade).
## The road, the sidewalks and the ground go on this far past each end of the play area (m); the
## buildings only part of the way (HOUSE_REACH), then the empty road carries on into the haze.
const END_REACH: float = 70.0
const HOUSE_REACH: float = 30.0
## The main screen shows the play area alone as a small model, the game's exact 1:2 shape: the
## cement on a chunky stone base with only the far base's electric post. No sidewalks, no team colours and no
## street past the ends.
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
## Painted ground sits just above the road slab.
const PAVER_TILE: float = 2.4
## The slab under the owner's tiled floor (and the street past both ends): dark grey cement.
const CEMENT: Color = Color(0.3, 0.3, 0.31)
## Optional realistic cement art (owner-made 2D image, see docs/ART.md): when this
## file exists it is laid ONCE over the whole play area (stretched, never tiled).
const CEMENT_ART: String = "res://assets/textures/cement.png"
const CEMENT_LIFT: float = 0.004
## The owner's floor picture holds 18 tiles across; the play area is narrower now (16 tiles), so it shows the middle of the picture
## (no stretch): the share of the picture's width the play area uses, and where it starts.
const CEMENT_PICTURE_TILES: float = 18.0
## The floor picture outside the play area is this much of its own colour (owner: slightly darker, so the play area reads).
const OUTSIDE_SHADE: Color = Color(0.78, 0.78, 0.78)
const STREET_SHADER: Shader = preload("res://shaders/street_cement.gdshader")
## Paving around the whole map, so the street never ends in sky.
const OUTER_MARGIN: float = 40.0
const OUTER_DROP: float = -0.12
const OUTER_TINT: Color = Color(0.62, 0.58, 0.54)
const SARI_SIGN_HEIGHT: float = 1.15
## Which model each cover piece (MapLayout.obstacle_props) is drawn as.
const OBSTACLE_MODELS: Dictionary[StringName, String] = {
	&"bench": "bench", &"tent": "rooster_tent",
}
const COVER_HEIGHT: float = 1.15
const POST_HEIGHT: float = 8.5
## The base posts are the owner's 3D models (assets/models/props/post_blue.glb and post_red.glb: a painted concrete pole with flyers, a
## crossarm, a transformer and a router box): blue for the viewer's own base, red for the other. The pole's foot is the model's origin and
## its front (the flyers and the router label) looks toward +Z, where the camera stands, so the post turns half way round when the view flips.
const POST_BLUE: String = "post_blue"
## The blue post stands with its back to the camera (owner): turned half way round from the red one, whose front (flyers, router label) faces the camera.
const BLUE_POST_TURN: float = PI
const POST_RED: String = "post_red"
## The barrier rows across both ends: every street thing at its real height (m), in proportion to
## the kids (who stand 1.85 m, so a real thing is about 1.4 times its true size), side by side
## in this order (the far end starts half way along, so the two rows differ).
const BARRIER_HEIGHTS: Dictionary[String, float] = {
	"tricycle": 2.4, "bench": 1.1, "crates": 1.1, "sacks": 1.0,
	"basketball_pole": 6.0, "ebike": 1.5, "truck_wheels": 1.4,
	"no_id_no_entry": 1.7, "rooster_tent": 1.4, "plank_fence": 1.8,
}
## The top boundary (owner): the end of the street at the top of the screen is closed by the owner's PNG pictures instead of the row of
## props, left to right on the screen. Each is a 2D picture (a transparent PNG, drawn as made: unshaded, no tint) standing across the end
## and leaning back to face the camera (the fixed orthographic camera looks `BOUNDARY_TILT` degrees off straight ahead, so a picture
## tilted that much shows undistorted, like a sprite); its bottom edge is on the ground at the end line. `height` is its true height in
## metres, drawn `scenery()` times as big like every other street thing; the width follows the picture's shape.
const BOUNDARY_PATH: String = "res://assets/textures/boundary/%s.png"
const BOUNDARY_PICTURES: Array[Dictionary] = [
	{"id": "fishball_cart", "height": 2.4},
	{"id": "jeepney", "height": 2.4},
]
const CORNER_WALL: Dictionary = {"id": "tin_fence", "height": 2.0}
## 90 degrees less the camera's pitch (`data/rules/camera_rules.tres`, 45).
const BOUNDARY_TILT: float = 45.0
## Things whose long side is their depth: they are turned a quarter so the long side faces the play area.
const BARRIER_TURNED: Array[String] = ["tricycle"]
## Things laid down on their side across the street (the basketball pole, so its whole length is
## the boundary): their BARRIER_HEIGHTS entry is their length along the row.
const BARRIER_LYING: Array[String] = ["basketball_pole"]
## The nanay on the sidewalk by the sari-sari store, outside the play area, facing the street.
## Parked on the sidewalk, outside the play area (heights in metres).
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
## end (-1 = the -Z end, 1 = the +Z end) -> the props of that end's row, and -> that end's row of the owner's pictures. The end at the top
## of the screen shows the pictures, the other its props (`set_own_side`).
var _row_props: Dictionary[float, Array] = {}
var _row_pictures: Dictionary[float, Node3D] = {}
## side -> [base tape, base fill, rescue tape, rescue fill] materials (recoloured per viewer).
var _base_materials: Dictionary[int, Array] = {}
## side -> that base's team colour as the viewer sees it (blue yours, red theirs).
var _side_tint: Dictionary[int, Color] = {}
## Per side: whether its rescue area was last drawn in the team's colour (ready) or white (counting down), so a material is touched only
## when that changes.
var _rescue_ready: Dictionary[int, bool] = {}
## side -> that base's electric post.
var _posts: Dictionary[int, Node3D] = {}
## side -> the countdown number lying on that side's rescue area.
var _counts: Dictionary[int, Label3D] = {}

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
	# the picture goes to the screen as drawn: a linear tonemap at exposure 1 and no colour adjustment (the owner's floor, the zone marks
	# and the posts must not be graded; the models get their look from their own materials and the lights)
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.tonemap_exposure = 1.0
	# no bloom: it is a blur laid over the bright parts (owner: nothing blurred during gameplay)
	env.glow_enabled = false
	env.adjustment_enabled = false
	# no haze: the camera is far above the whole street, and fog by distance would wash all of it out
	env.fog_enabled = false
	return env


## The key sun and the sky fill for a scene (`sun` is reused; the fill is added).
static func _light_scene(parent: Node, sun: DirectionalLight3D) -> void:
	sun.rotation_degrees = SUN_ANGLES
	sun.light_color = SUN_COLOR
	sun.light_energy = SUN_ENERGY
	# no shadows on the street (owner): the shadow is fully see-through. The light keeps its shadow pass on purpose: switching it
	# off made the whole street much darker (about 30%) in the Compatibility renderer, and the owner only asked for no shadows.
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = SHADOW_DISTANCE
	sun.shadow_opacity = 0.0
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
	for side: int in _counts:
		# upright for the viewer: the camera is turned half way round for the team that defends the -Z base
		_counts[side].rotation = Vector3(-PI / 2.0, PI if own_side == MapLayout.SIDE_ENEMY else 0.0, 0.0)
	_show_top_boundary(own_side)
	_rescue_ready.clear()
	for side: int in _base_materials:
		var tint: Color = OWN_COLOR if side == own_side else ENEMY_COLOR
		_side_tint[side] = tint
		for material: StandardMaterial3D in _base_materials[side]:
			var color: Color = tint
			color.a = material.albedo_color.a
			material.albedo_color = color


## Each team's rescue area is white and half see-through while its countdown runs (`MatchSim.rescue_ready_left`), with the seconds on it, and
## the team's colour (blue yours, red theirs) once a teammate can rescue there.
func sync_rescue(sim: MatchSim) -> void:
	var covered: bool = sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]
	for team: int in [0, 1]:
		var side: int = sim.side_for_team(team)
		if not _base_materials.has(side):
			continue
		var left: float = sim.rescue_ready_left[team]
		_show_count(_counts[side], left if not covered else 0.0)
		var ready: bool = left <= 0.0
		if _rescue_ready.get(side, not ready) == ready:
			continue
		_rescue_ready[side] = ready
		var tint: Color = _side_tint[side] if ready else Color.WHITE
		var materials: Array = _base_materials[side]
		(materials[2] as StandardMaterial3D).albedo_color = Color(tint, 1.0 if ready else RESCUE_WHITE_ALPHA)
		(materials[3] as StandardMaterial3D).albedo_color = Color(tint, RESCUE_FILL_ALPHA if ready else RESCUE_WHITE_ALPHA)


## The seconds on the rescue area of base `side` right now ("" when the area is ready or hidden).
func rescue_count_text(side: int) -> String:
	var label: Label3D = _counts.get(side) as Label3D
	return label.text if label != null and label.visible else ""


## The countdown number of base `side`'s rescue area (a mark on the ground).
func rescue_count(side: int) -> Label3D:
	return _counts[side]


func _show_count(label: Label3D, left: float) -> void:
	label.visible = left > 0.0
	if label.visible:
		var seconds: String = str(ceili(left))
		if label.text != seconds:
			label.text = seconds


## Each base's post shows the viewer's colours: the blue model for your own base, the red one for theirs (they swap when the teams
## switch ends), and it faces the camera, which is turned half way round for the team that defends the -Z base.
func _turn_posts(own_side: int) -> void:
	for side: int in _posts:
		_dress_post(_posts[side], side == own_side, own_side == MapLayout.SIDE_ENEMY)


## The team colour of base `side` as the viewer sees it: blue for yours, red for theirs.
func base_color(side: int) -> Color:
	return _side_tint[side]


## [base tape, base fill, rescue tape, rescue fill] colours for `side`.
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
		var color: Color = OWN_COLOR if side == MapLayout.SIDE_OWN else ENEMY_COLOR
		_posts[side] = _add_post(layout.post_position(side))
		var base: Array[StandardMaterial3D] = _add_marked_zone(layout.base_rect(side), layout.base_corner, color)
		var rescue: Array[StandardMaterial3D] = _add_marked_zone(layout.rescue_rect(side), RESCUE_CORNER_RADIUS, color, RESCUE_FILL_ALPHA, true)
		_base_materials[side] = [base[0], base[1], rescue[0], rescue[1]]
		_counts[side] = _add_count(layout.rescue_rect(side).get_center())
		_side_tint[side] = color
	_turn_posts(MapLayout.SIDE_OWN)
	_add_barricades()
	_add_boundary_pictures()
	_show_top_boundary(MapLayout.SIDE_OWN)
	_add_houses()
	_add_details()


## The main-screen model's props: the far base's electric post.
func _add_corner_props() -> void:
	# only the far half of the street is modelled: the enemy post (top right)
	_posts[MapLayout.SIDE_ENEMY] = _add_post(layout.post_position(MapLayout.SIDE_ENEMY))
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


## Lays the owner's floor image over all the ground the camera sees (owner): one whole picture, untouched, on the play area (its four
## corners rounded like the bases'), and the same picture a little darker past both ends and under the sides, so the play area stands
## out. The copies line up so the play area holds exactly one.
func _add_cement_art() -> void:
	if not ResourceLoader.exists(CEMENT_ART):
		return
	var size: Vector2 = Vector2(layout.lane_width, layout.lane_length + _reach() * 2.0) + Vector2.ONE * OUTER_MARGIN * 2.0
	var outside: MeshInstance3D = _outside_floor(size)
	outside.position.y = CEMENT_LIFT / 2.0
	_geometry.add_child(outside)
	var art: MeshInstance3D = _play_floor()
	art.position.y = CEMENT_LIFT
	_geometry.add_child(art)


## The darker floor round the play area, `size` metres (x, z) centred on it: the picture repeated every play-area size and lined up with
## the play area.
func _outside_floor(size: Vector2) -> MeshInstance3D:
	var period: Vector2 = Vector2(layout.lane_width, layout.lane_length)
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = size
	# unshaded and untinted: the floor is the picture itself (the basis every other colour is judged against)
	var share: float = cement_share()
	var material: StandardMaterial3D = StreetArt.unlit_material(StreetArt.texture("cement"), Vector2(size.x / period.x * share, size.y / period.y))
	# the picture's (cropped) edges fall on the play area's edges
	var start: Vector2 = (period - size) / 2.0 / period
	var from_x: float = (1.0 - share) / 2.0 + start.x * share
	material.uv1_offset = Vector3(from_x - floorf(from_x), start.y - floorf(start.y), 0.0)
	material.albedo_color = OUTSIDE_SHADE
	plane.material = material
	var patch: MeshInstance3D = MeshInstance3D.new()
	patch.name = "OutsideArt"
	patch.mesh = plane
	patch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return patch


## The share of the floor picture's width the play area shows (1 when the play area is as wide as the picture).
func cement_share() -> float:
	return minf(float(layout.tiles.x) / CEMENT_PICTURE_TILES, 1.0)


## The play area's floor: the owner's picture once over the whole area, cut to its rounded corners (a fan out from the middle, each
## point showing the picture where it stands).
func _play_floor() -> MeshInstance3D:
	var lane: Rect2 = layout.lane_rect()
	var outline: Array[Vector2] = rounded_outline(lane, layout.base_corner)
	var tool: SurfaceTool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_normal(Vector3.UP)
	for i: int in outline.size():
		for point: Vector2 in [lane.get_center(), outline[i], outline[(i + 1) % outline.size()]]:
			var across: float = (point.x - lane.position.x) / lane.size.x
			tool.set_uv(Vector2((1.0 - cement_share()) / 2.0 + across * cement_share(), (point.y - lane.position.y) / lane.size.y))
			tool.add_vertex(Vector3(point.x, 0.0, point.y))
	var mesh: ArrayMesh = tool.commit()
	mesh.surface_set_material(0, StreetArt.unlit_material(StreetArt.texture("cement")))
	var patch: MeshInstance3D = MeshInstance3D.new()
	patch.name = "CementArt"
	patch.mesh = mesh
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
	var id: String = String(OBSTACLE_MODELS.get(layout.obstacle_prop(index), "bench"))
	_add_prop(PropModel.fit(id, footprint, cover_height(), not diorama), at, PI / 2.0 if turned else 0.0)


## A base or a rescue area: a faint team-tinted rounded rectangle with a full outline, cut off by the street's edge. Only marks on the
## ground: nothing is blocked. Returns [tape, fill] materials (recoloured per viewer). A `see_through` area (the rescue area) has a tape that
## can be see-through too, and its tint stops where the tape starts, so the two never blend over each other (each shows its own alpha).
func _add_marked_zone(rect: Rect2, corner: float, color: Color, fill_alpha: float = ZONE_FILL_ALPHA, see_through: bool = false) -> Array[StandardMaterial3D]:
	var materials: Array[StandardMaterial3D] = _zone_materials(color, fill_alpha)
	if see_through:
		materials[0].transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		materials[0].render_priority = RESCUE_OUTLINE_PRIORITY
		_add_fill(rect.get_center(), rounded_outline(rect.grow(-TAPE_WIDTH), maxf(corner - TAPE_WIDTH, 0.0)), materials[1])
	else:
		_add_fill(rect.get_center(), rounded_outline(rect, corner), materials[1])
	_add_outline(rect, corner, materials[0])
	return materials


## The seconds number of a rescue area: a Label3D lying flat on the ground at `middle` (x, z), white and half see-through, unshaded, drawn
## in the see-through order described at `RESCUE_COUNT_PRIORITY`. Hidden until the sim says the area is counting.
func _add_count(middle: Vector2) -> Label3D:
	var label: Label3D = Label3D.new()
	label.name = "RescueCount"
	label.font = KalyeahTheme.ACTION_FONT
	label.font_size = RESCUE_COUNT_FONT
	label.pixel_size = RESCUE_COUNT_HEIGHT / float(RESCUE_COUNT_FONT)
	label.modulate = Color(1.0, 1.0, 1.0, RESCUE_WHITE_ALPHA)
	label.outline_size = 0
	label.shaded = false
	label.double_sided = true
	label.alpha_cut = Label3D.ALPHA_CUT_DISABLED
	label.render_priority = RESCUE_COUNT_PRIORITY
	label.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	label.position = Vector3(middle.x, RESCUE_COUNT_LIFT, middle.y)
	label.rotation = Vector3(-PI / 2.0, 0.0, 0.0)
	label.scale = Vector3(1.0, 1.0 / sin(deg_to_rad(COUNT_PITCH_DEGREES)), 1.0)
	label.visible = false
	_geometry.add_child(label)
	return label


## The team-coloured line round `rect` (its corners rounded), `TAPE_WIDTH` wide, drawn inside the rectangle.
func _add_outline(rect: Rect2, corner: float, tape: StandardMaterial3D) -> void:
	var rim: Array[Vector2] = rounded_outline(rect, corner)
	var inner: Array[Vector2] = rounded_outline(rect.grow(-TAPE_WIDTH), maxf(corner - TAPE_WIDTH, 0.0))
	_add_tape(rim, inner, tape)


## The outline of `rect` with its corners rounded to `radius`, as a closed list of points (the same count for any rect).
static func rounded_outline(rect: Rect2, radius: float) -> Array[Vector2]:
	var points: Array[Vector2] = []
	var corners: Array[Vector2] = [
		Vector2(rect.end.x - radius, rect.position.y + radius),
		Vector2(rect.end.x - radius, rect.end.y - radius),
		Vector2(rect.position.x + radius, rect.end.y - radius),
		Vector2(rect.position.x + radius, rect.position.y + radius),
	]
	for corner: int in 4:
		for step: int in ZONE_CORNER_STEPS + 1:
			var angle: float = -PI / 2.0 + (float(corner) + float(step) / float(ZONE_CORNER_STEPS)) * PI / 2.0
			points.append(corners[corner] + Vector2(cos(angle), sin(angle)) * radius)
	return points


## [tape, fill] for a zone: the team's colour in full for the outline, faint for the fill.
func _zone_materials(color: Color, fill_alpha: float) -> Array[StandardMaterial3D]:
	var tape: StandardMaterial3D = StandardMaterial3D.new()
	tape.albedo_color = color
	tape.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	tape.cull_mode = BaseMaterial3D.CULL_DISABLED
	return [tape, _fill_material(color, fill_alpha)]


func _fill_material(color: Color, alpha: float) -> StandardMaterial3D:
	var fill: StandardMaterial3D = StandardMaterial3D.new()
	fill.albedo_color = Color(color.r, color.g, color.b, alpha)
	fill.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	fill.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	fill.cull_mode = BaseMaterial3D.CULL_DISABLED
	# drawn first, so the kids and everything else stand on top of the tint, never under it
	fill.render_priority = -100
	return fill


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


## Tape between the closed `outer` line and the matching `inner` line.
func _add_tape(outer: Array[Vector2], inner: Array[Vector2], material: StandardMaterial3D) -> void:
	var tool: SurfaceTool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_normal(Vector3.UP)
	var height: float = RING_HEIGHT + 0.006
	for i: int in outer.size():
		var j: int = (i + 1) % outer.size()
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


## The ends of the play area are walled off by a row of street things (a bench, crates, a basketball hoop, stacked truck
## tyres and the wooden A-frame), each at its true size (never stretched to fill a gap), facing the play
## area, with uneven gaps and a little turned or set back, as if put down by hand.
func _add_barricades() -> void:
	var ids: Array = BARRIER_HEIGHTS.keys()
	for end_index: int in 2:
		var end: float = -1.0 if end_index == 0 else 1.0
		_row_props[end] = []
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
			_row_props[end].append(_put_barrier(model, id, Vector3(x + widths[k] / 2.0, 0.0, at_z), end, turn))
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
func _put_barrier(model: Node3D, id: String, at: Vector3, end: float, turn: float) -> Node3D:
	var yaw: float = (0.0 if end < 0.0 else PI) + (PI / 2.0 if BARRIER_TURNED.has(id) else 0.0)
	var placed: Node3D = _add_prop(model, at, yaw + turn)
	if BARRIER_LYING.has(id):
		# on its side: the pole's length runs along the row, its width becomes its height
		var natural: Vector3 = PropModel.size_of(id)
		placed.rotation = Vector3(0.0, turn, -PI / 2.0)
		var along: Vector3 = Vector3(cos(turn), 0.0, -sin(turn))
		placed.position = Vector3(at.x, natural.x * model.scale.x / 2.0, at.z) - along * (barrier_width(id, scenery()) / 2.0)
	return placed


## How wide (m) a boundary picture stands: its drawn height times the shape of its picture.
static func boundary_width(entry: Dictionary, scale: float = 1.0) -> float:
	var size: Vector2 = (load(BOUNDARY_PATH % String(entry["id"])) as Texture2D).get_size()
	return float(entry["height"]) * scale * size.x / size.y


## The end of the street (-1 or 1) at the top of the screen for a viewer whose own base is on `own_side`.
static func top_end(own_side: int) -> float:
	return -float(own_side)


## The cart replaces the former middle wall; the jeepney keeps its position, with matching walls at both corners.
## The +Z row turns half way round so the same arrangement faces either viewer.
func _add_boundary_pictures() -> void:
	var widths: Array[float] = []
	var used: float = 0.0
	for entry: Dictionary in BOUNDARY_PICTURES:
		widths.append(boundary_width(entry, scenery()))
		used += widths[widths.size() - 1]
	var wall_width: float = boundary_width(CORNER_WALL, scenery())
	# Equal visible gaps between the two fixed corner walls and the two pictures.
	var gap: float = maxf(layout.lane_width - wall_width - used, 0.0) / 3.0
	var left_edge: float = -layout.lane_width / 2.0 + wall_width / 2.0
	var centers: Array[float] = [left_edge + gap + widths[0] / 2.0, left_edge + gap * 2.0 + widths[0] + widths[1] / 2.0]
	for end: float in [-1.0, 1.0]:
		var row: Node3D = Node3D.new()
		row.name = "BoundaryPictures%s" % ("Far" if end < 0.0 else "Near")
		row.position = Vector3(0.0, 0.0, end * (layout.lane_length / 2.0 + BARRIER_SETBACK))
		row.rotation_degrees = Vector3(0.0, 0.0 if end < 0.0 else 180.0, 0.0)
		# in the row's own frame x runs to the right of the screen at either end
		for k: int in BOUNDARY_PICTURES.size():
			var picture: MeshInstance3D = _boundary_picture(BOUNDARY_PICTURES[k], widths[k])
			picture.position = Vector3(centers[k], 0.0, 0.0)
			row.add_child(picture)
		for side: float in [-1.0, 1.0]:
			var roof: MeshInstance3D = _boundary_picture(CORNER_WALL, boundary_width(CORNER_WALL, scenery()))
			roof.name = "RoofWallLeft" if side < 0.0 else "RoofWallRight"
			# A slight setback keeps the PNG behind the other boundary pictures without coplanar flicker.
			roof.position = Vector3(side * layout.lane_width / 2.0, 0.0, -0.05)
			if side > 0.0:
				var material: StandardMaterial3D = (roof.mesh as QuadMesh).material as StandardMaterial3D
				material.uv1_scale.x = -1.0
				material.uv1_offset.x = 1.0
			row.add_child(roof)
		_geometry.add_child(row)
		_row_pictures[end] = row


## One picture of the top boundary: an upright quad leaning back by `BOUNDARY_TILT`, its bottom edge on the ground, drawn as the owner made it.
func _boundary_picture(entry: Dictionary, width: float) -> MeshInstance3D:
	var height: float = float(entry["height"]) * scenery()
	var quad: QuadMesh = QuadMesh.new()
	quad.size = Vector2(width, height)
	quad.center_offset = Vector3(0.0, height / 2.0, 0.0)
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_DEPTH_PRE_PASS
	material.albedo_texture = load(BOUNDARY_PATH % String(entry["id"])) as Texture2D
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	quad.material = material
	var picture: MeshInstance3D = MeshInstance3D.new()
	picture.name = String(entry["id"])
	picture.mesh = quad
	picture.rotation_degrees = Vector3(-BOUNDARY_TILT, 0.0, 0.0)
	picture.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return picture


## The end at the top of the screen (the -Z end when the viewer's own base is at +Z, the +Z end when the camera is turned around) shows
## the owner's pictures; the other end keeps its row of street things.
func _show_top_boundary(own_side: int) -> void:
	var top: float = top_end(own_side)
	for end: float in _row_pictures:
		_row_pictures[end].visible = end == top
		for prop: Node3D in _row_props[end]:
			prop.visible = end != top


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
	_add_ice_cream_cart(half_l, walk_x)
	_add_sidewalk_props(walk_x)


## The owner's cat, dog, chicken cage and drainage covers on the sidewalks, facing the street.
func _add_sidewalk_props(walk_x: float) -> void:
	var left: float = PI / 2.0
	var right: float = -PI / 2.0
	_add_prop(PropModel.by_height("cat", CAT_HEIGHT * scenery()), Vector3(-walk_x - 0.2, CURB_HEIGHT, -3.0), left)
	_add_prop(PropModel.by_height("dog", DOG_HEIGHT * scenery()), Vector3(walk_x + 0.2, CURB_HEIGHT, -9.0), right)
	_add_prop(PropModel.by_height("chicken_cage", CHICKEN_CAGE_HEIGHT * scenery()), Vector3(walk_x + 0.3, CURB_HEIGHT, 6.0), right)
	for spot: Vector2 in [Vector2(-walk_x, -17.0), Vector2(-walk_x, 7.0), Vector2(walk_x, 0.0), Vector2(walk_x, 19.0)]:
		_add_prop(PropModel.make("drainage_cover", Vector3.ONE * (DRAINAGE_COVER_WIDTH * scenery() / PropModel.size_of("drainage_cover").x)), Vector3(spot.x, CURB_HEIGHT, spot.y))


## The ice cream cart on the right sidewalk (outside the play area).
func _add_ice_cream_cart(half_l: float, walk_x: float) -> void:
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


## The owner's post models, standing on the ground at `center` (x, z), `post_height()` tall. Both colours are built and `_dress_post` shows one.
func _add_post(center: Vector2) -> Node3D:
	var post: Node3D = Node3D.new()
	post.name = "Post"
	for id: String in [POST_BLUE, POST_RED]:
		var model: Node3D = PropModel.by_height(id, post_height())
		model.name = id
		ModelLook.use_baked_shading(model)
		model.rotation.y = BLUE_POST_TURN if id == POST_BLUE else 0.0
		post.add_child(model)
	_dress_post(post, true, false)
	_add_prop(post, Vector3(center.x, 0.0, center.y))
	return post


## Shows the blue (`own`) or red model on a post, and turns it half way round when the camera is (`flipped`) so its front faces the screen.
func _dress_post(post: Node3D, own: bool, flipped: bool) -> void:
	(post.get_node(POST_BLUE) as Node3D).visible = own
	(post.get_node(POST_RED) as Node3D).visible = not own
	post.rotation.y = PI if flipped else 0.0


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
