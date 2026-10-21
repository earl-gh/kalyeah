extends GutTest
## The fixed arena camera: the whole street is always on screen.

const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PHONE: float = 720.0 / 1280.0
const ARENA: Vector2 = Vector2(27.0, 48.0)


func _corners(distance: float, aspect: float, shift: float) -> Array[Vector2]:
	var points: Array[Vector2] = []
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			points.append(CAMERA.screen_point(distance, aspect, Vector2(sx * ARENA.x / 2.0, sz * ARENA.y / 2.0), shift))
	return points


func test_a_fixed_steep_camera_with_a_narrow_lens() -> void:
	assert_eq(CAMERA.pitch_degrees, 56.0, "owner reference (Clash Royale): tilted, so fronts of kids and props show, yet the arena still reads like a board")
	assert_eq(CAMERA.vfov_degrees, 20.0, "narrow: the far end is only a little smaller than the near end")
	assert_eq(CAMERA.yaw_offset_degrees, 0.0, "straight along the street")
	assert_eq(CameraRules.DESIGN_ASPECT, 9.0 / 16.0, "portrait 9:16")


func test_the_street_is_9_by_16_like_the_arena_it_copies() -> void:
	assert_almost_eq(LAYOUT.lane_width / LAYOUT.lane_length, 9.0 / 16.0, 0.001, "27 x 48 m")


func test_the_whole_arena_fits_on_a_phone_with_its_margins() -> void:
	var framed: Vector2 = CAMERA.frame(PHONE, ARENA)
	for point: Vector2 in _corners(framed.x, PHONE, framed.y):
		assert_lte(absf(point.x), 1.0 - 2.0 * CAMERA.frame_side + 0.001, "inside the side margin")
		assert_lte(point.y, 1.0 - 2.0 * CAMERA.frame_top + 0.001, "below the score row")
		assert_gte(point.y, -1.0 + 2.0 * CAMERA.frame_bottom - 0.001, "above the controls")


func test_the_whole_arena_fits_any_screen_shape() -> void:
	for aspect: float in [0.46, PHONE, 0.62, 0.75, 1.0]:
		var framed: Vector2 = CAMERA.frame(aspect, ARENA)
		assert_true(CAMERA.fits(framed.x * 1.001, aspect, ARENA), "aspect %.2f" % aspect)
		assert_false(CAMERA.fits(framed.x * 0.97, aspect, ARENA), "and it is the nearest the camera can be (aspect %.2f)" % aspect)


func test_the_arena_is_centred_in_the_space_between_the_margins() -> void:
	var framed: Vector2 = CAMERA.frame(PHONE, ARENA)
	var far: float = CAMERA.screen_point(framed.x, PHONE, Vector2(0.0, -ARENA.y / 2.0), framed.y).y
	var near: float = CAMERA.screen_point(framed.x, PHONE, Vector2(0.0, ARENA.y / 2.0), framed.y).y
	var band_middle: float = ((1.0 - 2.0 * CAMERA.frame_top) + (-1.0 + 2.0 * CAMERA.frame_bottom)) / 2.0
	assert_almost_eq((far + near) / 2.0, band_middle, 0.001)


func test_the_far_end_is_only_a_little_smaller_than_the_near_end() -> void:
	var framed: Vector2 = CAMERA.frame(PHONE, ARENA)
	var far: float = absf(CAMERA.screen_point(framed.x, PHONE, Vector2(ARENA.x / 2.0, -ARENA.y / 2.0), framed.y).x)
	var near: float = absf(CAMERA.screen_point(framed.x, PHONE, Vector2(ARENA.x / 2.0, ARENA.y / 2.0), framed.y).x)
	assert_gt(far / near, 0.75, "a nearly flat picture, like a board seen from above")
	assert_lt(far, near, "but still a perspective")


func test_the_camera_sits_behind_the_own_end_and_the_other_team_is_turned_half_way_round() -> void:
	var own: Vector3 = CAMERA.camera_offset(100.0, false)
	var other: Vector3 = CAMERA.camera_offset(100.0, true)
	assert_gt(own.z, 0.0, "own side: camera on +Z looking toward -Z")
	assert_almost_eq(own.y, 100.0 * sin(deg_to_rad(CAMERA.pitch_degrees)), 0.001)
	assert_almost_eq(other.z, -own.z, 0.0001)
	assert_almost_eq(other.y, own.y, 0.0001)
	assert_eq(CAMERA.yaw_degrees(true), CAMERA.yaw_offset_degrees + 180.0)
	assert_eq(CAMERA.camera_target(3.0, true), -CAMERA.camera_target(3.0, false), "the shift is the other way for the other team")


func test_the_kids_are_drawn_a_little_bigger_than_their_model_and_their_bars_smaller() -> void:
	assert_almost_eq(CAMERA.kid_view_scale, 1.5, 0.0001, "world scale: a kid is about a third of a house, not as tall as one (it was 3.2)")
	assert_lt(CAMERA.bar_scale, 1.0)
	assert_gt(LAYOUT.scenery_scale, 1.0, "the scenery is drawn bigger to match")


func test_the_kid_is_readable_at_this_size() -> void:
	var framed: Vector2 = CAMERA.frame(PHONE, ARENA)
	var feet: Vector2 = CAMERA.screen_point(framed.x, PHONE, Vector2.ZERO, framed.y)
	var head: Vector2 = CAMERA.screen_point(framed.x, PHONE, Vector2.ZERO, framed.y, KidModel.KID_HEIGHT * CAMERA.kid_view_scale)
	var tall: float = (head.y - feet.y) / 2.0 * 1280.0
	assert_gt(tall, 33.0, "at least 33 px tall on a 1280 px screen (about 35 at the shipped scale and tilt: do not shrink the kids further)")
	var wide: float = absf(CAMERA.screen_point(framed.x, PHONE, Vector2(0.35 * CAMERA.kid_view_scale, 0.0), framed.y).x - feet.x) * 360.0 * 2.0
	assert_gt(wide, 20.0, "and at least 20 px wide (about 23)")


func test_the_arena_camera_places_itself_and_keeps_the_whole_street_in_view() -> void:
	var camera: ArenaCamera = autofree(ArenaCamera.new()) as ArenaCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	assert_eq(camera.keep_aspect, Camera3D.KEEP_HEIGHT)
	camera.place(false)
	assert_almost_eq(camera.fov, CAMERA.render_vfov_degrees(), 0.001)
	assert_almost_eq(camera.rotation_degrees.x, -CAMERA.pitch_degrees, 0.001)
	var before: Vector3 = camera.position
	camera.place(false)
	assert_eq(camera.position, before, "it never moves")
	assert_lt(camera.near, camera.distance(), "the depth range is cut tight around the arena")
	assert_gt(camera.far, camera.distance())
	camera.place(true)
	assert_lt(camera.position.z, 0.0, "the other team looks from the other end")


func test_a_cinematic_shot_uses_a_wide_lens_and_the_camera_goes_back_after() -> void:
	var camera: ArenaCamera = autofree(ArenaCamera.new()) as ArenaCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	camera.place(false)
	var framed_fov: float = camera.fov
	camera.frame_shot(Vector3(0.0, 5.0, 10.0), Vector3.ZERO)
	assert_gt(camera.fov, framed_fov, "a wide lens close to the post")
	assert_lt(camera.near, 1.0)
	camera.place(false)
	assert_almost_eq(camera.fov, framed_fov, 0.001, "and the fixed camera again")
