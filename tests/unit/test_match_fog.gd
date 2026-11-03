extends GutTest
## The fog beyond the two ends of the street: a wall of fog standing in the street just past each end's boundary row, behind the pictures at
## the end far from the camera and in front of the props at the near end.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _kind: StringName
var _game: StringName
var _pending: bool


func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game
	_pending = Session.briefing_pending


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game
	Session.briefing_pending = _pending


func _match(game: StringName = &"street") -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = game
	Session.briefing_pending = false
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	practice.advance(DT)
	return practice


func test_every_match_has_a_fog_wall_past_each_end_just_beyond_the_boundary_row() -> void:
	var practice: PracticeMatch = _match()
	var fog: MatchFog = practice.get_node("Fog") as MatchFog
	assert_not_null(fog)
	assert_eq(fog.walls.size(), 2, "one at each end")
	var half: float = practice.layout.lane_length / 2.0
	for wall: MeshInstance3D in fog.walls:
		var z: float = absf(wall.position.z)
		assert_gt(z, half + StreetMap.BARRIER_SETBACK, "past the boundary row's line")
		assert_almost_eq(z, half + practice._map.fog_distance(), 0.001, "just past the boundary pictures, leaned back as they are")
		assert_lte(practice._map.fog_distance(), 5.0, "near the boundary")
		assert_almost_eq(wall.position.y, MatchFog.HEIGHT / 2.0, 0.001, "standing on the ground")
	assert_ne(signf(fog.walls[0].position.z), signf(fog.walls[1].position.z), "one at each end")


func test_the_walls_layer_by_the_camera_behind_the_far_boundary_and_in_front_of_the_near_one() -> void:
	var code: String = MatchFog.SHADER.code
	assert_true(code.contains("VIEW_MATRIX"), "the end is told from the camera")
	assert_true(code.contains("is_far"), "a far-end and a near-end look")
	assert_true(code.contains("depth_draw_never"), "it never hides what is behind it in the depth buffer")
	assert_false(code.contains("outline_color"), "the smooth fog, not outlined puffs")
	assert_true(code.contains("smoothstep"), "a smooth fade")


func test_the_fog_is_white_and_nearly_opaque_where_thickest() -> void:
	var code: String = MatchFog.SHADER.code
	var found: RegExMatch = RegEx.create_from_string("uniform vec4 fog_color[^=]*=\\s*vec4\\(([^)]*)\\)").search(code)
	var parts: PackedStringArray = found.get_string(1).split(",")
	var colour: Color = Color(parts[0].to_float(), parts[1].to_float(), parts[2].to_float())
	assert_gt(colour.get_luminance(), 0.95, "white")
	var strength: RegExMatch = RegEx.create_from_string("uniform float strength[^=]*=\\s*([0-9.]+)").search(code)
	assert_gte(strength.get_string(1).to_float(), 0.95, "much less transparent than before (it was 0.85)")


func test_the_fog_never_takes_a_touch_and_casts_no_shadow() -> void:
	var practice: PracticeMatch = _match()
	for wall: MeshInstance3D in (practice.get_node("Fog") as MatchFog).walls:
		assert_eq(wall.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF)


func test_the_fog_is_built_by_how_far_it_looks_past_the_end_line_on_the_screen() -> void:
	var code: String = MatchFog.SHADER.code
	assert_true(code.contains("beyond"), "one measure for both ends")
	assert_true(code.contains("is_ground"), "a strip of ground at the near end")
	assert_true(code.contains("smoothstep(0.0, near_ramp, beyond)"), "the near end's fog starts at the end line and never rises into the play area")


func test_the_near_ends_fog_is_full_before_the_tray_and_the_far_ends_starts_above_the_pictures() -> void:
	var saved: Vector2i = get_window().size
	get_window().size = Vector2i(720, 1600)
	await wait_process_frames(2)
	var practice: PracticeMatch = _match()
	await wait_process_frames(2)
	var camera: Camera3D = practice._camera
	var half: float = practice.layout.lane_length / 2.0
	var line_y: float = camera.unproject_position(Vector3(0.0, 0.0, half)).y
	var tray_y: float = practice.hud.size.y - practice.hud.console_height()
	# how far past the end line (metres of ground) the tray's top edge is on the screen
	var metres_per_pixel: float = 1.0 / (camera.unproject_position(Vector3(0.0, 0.0, half + 1.0)).y - line_y)
	var to_tray: float = (tray_y - line_y) * metres_per_pixel
	assert_gt(to_tray, 3.0, "there is ground to see between the end line and the tray")
	var ramp: RegExMatch = RegEx.create_from_string("uniform float near_ramp[^=]*=\\s*([0-9.]+)").search(MatchFog.SHADER.code)
	assert_lt(ramp.get_string(1).to_float(), to_tray, "the fog is already full where the tray starts: it fills the space between the boundary and the tray")
	var far_start: float = practice._map.fog_far_start()
	var far_y: float = camera.unproject_position(Vector3(0.0, 0.0, -half - far_start)).y
	var top_picture_y: float = camera.unproject_position(Vector3(0.0, 0.0, -half - 0.15)).y
	assert_lt(far_y, top_picture_y, "the far end's fog starts above the boundary pictures' foot")
	assert_gt(far_y, 0.0, "and on the screen")
	get_window().size = saved


func test_each_end_has_a_wall_and_a_strip_of_ground() -> void:
	var practice: PracticeMatch = _match()
	var fog: MatchFog = practice.get_node("Fog") as MatchFog
	assert_eq(fog.grounds.size(), 2)
	var distance: float = practice._map.fog_distance()
	for strip: MeshInstance3D in fog.grounds:
		assert_gt(absf(strip.position.z), practice.layout.lane_length / 2.0 + distance, "the ground strip is beyond the wall")
		assert_lt(strip.position.y, 0.1, "on the ground")


func test_no_boundary_picture_pokes_through_the_fog_wall() -> void:
	var practice: PracticeMatch = _match()
	var map: StreetMap = practice._map
	var tallest: float = 0.0
	for entry: Dictionary in StreetMap.BOUNDARY_PICTURES:
		tallest = maxf(tallest, entry["height"] as float)
	var top_reach: float = StreetMap.BARRIER_SETBACK + tallest * map.scenery() * sin(deg_to_rad(StreetMap.BOUNDARY_TILT))
	assert_gt(map.fog_distance(), top_reach, "the wall is past where the tallest picture's top leans to")
