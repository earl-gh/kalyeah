extends GutTest

const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PHONE: float = 720.0 / 1280.0


func test_a_fixed_low_angle_with_a_wide_perspective_lens() -> void:
	assert_eq(CAMERA.pitch_degrees, 46.0, "owner: higher and pitched forward, like a MOBA")
	assert_eq(CAMERA.vfov_degrees, 75.0, "wide vertical field of view")
	assert_eq(CameraRules.DESIGN_ASPECT, 9.0 / 16.0, "portrait 9:16")


func test_other_camera_numbers_come_from_data() -> void:
	assert_eq(CAMERA.visible_width, 12.0, "a little farther from the kid for the higher view")
	assert_eq(CAMERA.player_screen_y, 0.62)


func test_distance_shows_the_visible_width_on_any_phone() -> void:
	for aspect: float in [0.46, PHONE, 0.75]:
		var distance: float = CAMERA.distance(aspect)
		var width: float = 2.0 * distance * tan(deg_to_rad(CAMERA.vfov_degrees / 2.0)) * aspect
		assert_almost_eq(width, CAMERA.visible_width, 0.001, "aspect %.2f" % aspect)
	assert_almost_eq(CAMERA.distance(PHONE), 13.9, 0.05)


func test_the_camera_is_high_above_and_behind_the_hero() -> void:
	var offset: Vector3 = CAMERA.camera_offset(CAMERA.distance(PHONE), false)
	assert_almost_eq(offset.y, 10.0, 0.1, "about 10 m up (was 7)")
	assert_almost_eq(offset.length(), CAMERA.distance(PHONE), 0.001)


func test_aspect_is_kept_in_a_sane_range() -> void:
	assert_eq(CAMERA.distance(0.05), CAMERA.distance(CAMERA.min_aspect))
	assert_eq(CAMERA.distance(5.0), CAMERA.distance(CAMERA.max_aspect))


func test_offset_sits_behind_and_above_the_target() -> void:
	var offset: Vector3 = CAMERA.camera_offset(20.0, false)
	assert_almost_eq(offset.y, 20.0 * sin(deg_to_rad(CAMERA.pitch_degrees)), 0.001)
	var back: float = 20.0 * cos(deg_to_rad(CAMERA.pitch_degrees))
	assert_almost_eq(Vector2(offset.x, offset.z).length(), back, 0.001)
	assert_gt(offset.z, 0.0, "own side: camera behind on +Z, looking toward -Z")
	assert_gte(offset.x, 0.0, "never to the left of the hero")
	assert_almost_eq(rad_to_deg(atan2(offset.x, offset.z)), CAMERA.yaw_offset_degrees, 0.001)


func test_other_team_view_is_mirrored() -> void:
	var own: Vector3 = CAMERA.camera_offset(20.0, false)
	var other: Vector3 = CAMERA.camera_offset(20.0, true)
	assert_almost_eq(other.z, -own.z, 0.0001)
	assert_almost_eq(other.x, -own.x, 0.0001)
	assert_almost_eq(other.y, own.y, 0.0001)
	assert_eq(CAMERA.yaw_degrees(false), CAMERA.yaw_offset_degrees)
	assert_eq(CAMERA.yaw_degrees(true), CAMERA.yaw_offset_degrees + 180.0)


func test_the_aim_point_is_far_enough_ahead_to_hold_the_hero_low_on_screen() -> void:
	var reach: float = CAMERA.distance(PHONE)
	var ahead: float = CAMERA.look_ahead(reach)
	assert_almost_eq(ahead, 3.02, 0.05, "about 3 m ahead")
	assert_eq(CAMERA.camera_target(Vector2(0.0, 10.0), false, reach), Vector2(0.0, 10.0 - ahead), "own side looks toward -Z")
	assert_eq(CAMERA.camera_target(Vector2(0.0, -10.0), true, reach), Vector2(0.0, -10.0 + ahead), "flipped looks toward +Z")


func test_the_camera_follows_the_hero_everywhere_the_map_never_holds_it_back() -> void:
	var reach: float = CAMERA.distance(PHONE)
	var ahead: float = CAMERA.look_ahead(reach)
	var half_w: float = LAYOUT.lane_width / 2.0
	var half_l: float = LAYOUT.lane_length / 2.0
	# every corner, edge and the middle: the aim is always the hero's spot moved ahead
	for x: float in [-half_w, 0.0, half_w]:
		for z: float in [-half_l, 0.0, half_l]:
			var hero: Vector2 = Vector2(x, z)
			assert_eq(CAMERA.camera_target(hero, false, reach), Vector2(x, z - ahead), "own view at %s" % hero)
			assert_eq(CAMERA.camera_target(hero, true, reach), Vector2(x, z + ahead), "flipped view at %s" % hero)
	# walking on toward an edge always moves the camera on too (it is never stuck)
	var near_edge: Vector2 = CAMERA.camera_target(Vector2(half_w - 1.0, -half_l + 1.0), false, reach)
	var at_edge: Vector2 = CAMERA.camera_target(Vector2(half_w - 0.5, -half_l + 0.5), false, reach)
	assert_gt(at_edge.x, near_edge.x)
	assert_lt(at_edge.y, near_edge.y)


func test_ground_offsets_cover_the_whole_screen() -> void:
	var reach: float = CAMERA.distance(PHONE)
	assert_almost_eq(CAMERA.ground_offset(reach, 0.5), 0.0, 0.001, "the screen centre is the aim point")
	assert_gt(CAMERA.ground_offset(reach, 1.0), 0.0, "the bottom edge is behind the aim point")
	assert_lt(CAMERA.ground_offset(reach, 0.0), 0.0, "the top edge is ahead of it (or the sky)")
	assert_lt(CAMERA.ground_offset(reach, 0.0), -30.0, "the very top of the screen is ground far up the street, not sky")
	assert_gt(CAMERA.ground_offset(reach, 0.0), -INF, "a pitched-down camera has no sky on screen")
	assert_lt(CAMERA.ground_offset(reach, 1.0), reach, "the bottom edge is in front of the camera")


func test_follow_weight_is_frame_rate_independent() -> void:
	var one_step: float = CAMERA.follow_weight(0.1)
	var two_steps: float = 1.0 - pow(1.0 - CAMERA.follow_weight(0.05), 2.0)
	assert_almost_eq(one_step, two_steps, 0.0001)
	assert_eq(CAMERA.follow_weight(0.0), 0.0)


func test_the_follow_camera_uses_keep_height_and_the_data_angle() -> void:
	var camera: FollowCamera = autofree(FollowCamera.new()) as FollowCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	assert_eq(camera.keep_aspect, Camera3D.KEEP_HEIGHT)
	assert_almost_eq(camera.fov, CAMERA.render_vfov_degrees(), 0.001, "wider than the design view; the lens pulls it back in")
	camera.follow(Vector2.ZERO, false)
	assert_almost_eq(camera.rotation_degrees.x, -CAMERA.pitch_degrees, 0.001)


func test_the_near_plane_is_close_enough_not_to_slice_models() -> void:
	var camera: FollowCamera = autofree(FollowCamera.new()) as FollowCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	camera.follow(Vector2.ZERO, false)
	# the camera is only about 5 m up: with a plane any further out, the ground and props
	# directly beneath and in front of it are cut off
	var height: float = CAMERA.camera_offset(CAMERA.distance(PHONE), false).y
	assert_lt(camera.near, 1.0, "the near plane is about half a metre out")
	assert_lt(camera.near, height / 4.0, "well inside the camera's height")
	assert_lt(camera.far / camera.near, 1000.0, "the depth range is still sane")

