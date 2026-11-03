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
		assert_almost_eq(z, half + StreetMap.BARRIER_SETBACK + StreetMap.FOG_DEPTH, 0.001, "close to it: just past its pictures and props")
		assert_lte(StreetMap.FOG_DEPTH, 2.5, "near the boundary")
		assert_almost_eq(wall.position.y, MatchFog.HEIGHT / 2.0, 0.001, "standing on the ground")
	assert_ne(signf(fog.walls[0].position.z), signf(fog.walls[1].position.z), "one at each end")


func test_the_walls_layer_by_the_camera_behind_the_far_boundary_and_in_front_of_the_near_one() -> void:
	var code: String = MatchFog.SHADER.code
	assert_true(code.contains("VIEW_MATRIX"), "the end is told from the camera")
	assert_true(code.contains("is_far"), "a far-end and a near-end look")
	assert_true(code.contains("depth_draw_never"), "it never hides what is behind it in the depth buffer")
	assert_false(code.contains("outline_color"), "the smooth fog, not outlined puffs")
	assert_true(code.contains("smoothstep"), "a smooth fade")


func test_the_fog_is_slightly_darker_than_the_first_haze() -> void:
	var first_haze: Color = Color(0.74, 0.77, 0.82)
	var found: RegExMatch = RegEx.create_from_string("uniform vec4 fog_color[^=]*=\\s*vec4\\(([^)]*)\\)").search(MatchFog.SHADER.code)
	var parts: PackedStringArray = found.get_string(1).split(",")
	var now: Color = Color(parts[0].to_float(), parts[1].to_float(), parts[2].to_float())
	assert_lt(now.get_luminance(), first_haze.get_luminance(), "darker")
	assert_gt(now.get_luminance(), 0.45, "but still a pale mist")


func test_the_fog_never_takes_a_touch_and_casts_no_shadow() -> void:
	var practice: PracticeMatch = _match()
	for wall: MeshInstance3D in (practice.get_node("Fog") as MatchFog).walls:
		assert_eq(wall.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF)
