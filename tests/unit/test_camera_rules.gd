extends GutTest

const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PHONE: float = 720.0 / 1280.0


func test_a_fixed_moba_angle_with_a_moderate_perspective_lens() -> void:
	assert_eq(CAMERA.pitch_degrees, 44.0, "high enough to read the ground, low enough to see the kids' faces")
	assert_eq(CAMERA.vfov_degrees, 60.0, "a moderate lens: little stretch at the edges, so little to get used to")
	assert_eq(CAMERA.yaw_offset_degrees, 0.0, "straight behind in the middle of the street")
	assert_eq(CAMERA.yaw_side_degrees, 8.0, "leaning 8 degrees toward the side the hero is on: the sides of props and houses show, the street stops looking like a corridor")
	assert_eq(CameraRules.DESIGN_ASPECT, 9.0 / 16.0, "portrait 9:16")


func test_other_camera_numbers_come_from_data() -> void:
	assert_eq(CAMERA.visible_width, 13.5, "metres of street across the screen at the aim point: wide enough to see a threat at the side")
	assert_eq(CAMERA.player_screen_y, 0.52, "the kid sits near the middle: you see as much behind and beside as ahead")


func test_distance_shows_the_visible_width_on_any_phone() -> void:
	for aspect: float in [0.46, PHONE, 0.75]:
		var distance: float = CAMERA.distance(aspect)
		var width: float = 2.0 * distance * tan(deg_to_rad(CAMERA.vfov_degrees / 2.0)) * aspect
		assert_almost_eq(width, CAMERA.visible_width, 0.001, "aspect %.2f" % aspect)
	assert_almost_eq(CAMERA.distance(PHONE), 20.78, 0.05)


func test_the_camera_is_high_behind_the_hero() -> void:
	var offset: Vector3 = CAMERA.camera_offset(CAMERA.distance(PHONE), false)
	assert_almost_eq(offset.y, 14.43, 0.1, "about 14 m up")
	assert_almost_eq(offset.length(), CAMERA.distance(PHONE), 0.001)


func test_aspect_is_kept_in_a_sane_range() -> void:
	assert_eq(CAMERA.distance(0.05), CAMERA.distance(CAMERA.min_aspect))
	assert_eq(CAMERA.distance(5.0), CAMERA.distance(CAMERA.max_aspect))


func test_offset_sits_behind_and_above_the_target() -> void:
	var offset: Vector3 = CAMERA.camera_offset(20.0, false, 8.0)
	assert_almost_eq(offset.y, 20.0 * sin(deg_to_rad(CAMERA.pitch_degrees)), 0.001)
	var back: float = 20.0 * cos(deg_to_rad(CAMERA.pitch_degrees))
	assert_almost_eq(Vector2(offset.x, offset.z).length(), back, 0.001)
	assert_gt(offset.z, 0.0, "own side: camera behind on +Z, looking toward -Z")
	assert_gt(offset.x, 0.0, "a positive yaw puts the camera on the right")
	assert_lt(CAMERA.camera_offset(20.0, false, -8.0).x, 0.0, "a negative one on the left")
	assert_almost_eq(rad_to_deg(atan2(offset.x, offset.z)), 8.0, 0.001)


func test_other_team_view_is_mirrored() -> void:
	var own: Vector3 = CAMERA.camera_offset(20.0, false, 8.0)
	var other: Vector3 = CAMERA.camera_offset(20.0, true, 8.0)
	assert_almost_eq(other.z, -own.z, 0.0001)
	assert_almost_eq(other.x, -own.x, 0.0001)
	assert_almost_eq(other.y, own.y, 0.0001)
	assert_eq(CAMERA.yaw_degrees(false), CAMERA.yaw_offset_degrees)
	assert_eq(CAMERA.yaw_degrees(true), CAMERA.yaw_offset_degrees + 180.0)


func test_the_aim_point_is_far_enough_ahead_to_hold_the_hero_low_on_screen() -> void:
	var reach: float = CAMERA.distance(PHONE)
	var ahead: float = CAMERA.look_ahead(reach)
	assert_almost_eq(ahead, 0.67, 0.05, "the kid is nearly under the middle of the screen")
	var own: Vector2 = CAMERA.camera_target(Vector2(0.0, 10.0), false, reach)
	assert_almost_eq(own.distance_to(Vector2(0.0, 10.0)), ahead, 0.001, "the aim is `ahead` metres from the hero")
	assert_lt(own.y, 10.0, "own side looks toward -Z")
	var other: Vector2 = CAMERA.camera_target(Vector2(0.0, -10.0), true, reach)
	assert_gt(other.y, -10.0, "flipped looks toward +Z")
	assert_almost_eq(other.x, -own.x, 0.001, "the other team's view is the same view turned half way round")


func test_the_camera_follows_the_hero_everywhere_the_map_never_holds_it_back() -> void:
	var reach: float = CAMERA.distance(PHONE)
	var ahead: float = CAMERA.look_ahead(reach)
	var half_w: float = LAYOUT.lane_width / 2.0
	var half_l: float = LAYOUT.lane_length / 2.0
	# every corner, edge and the middle: the aim is always the hero's spot moved ahead
	for x: float in [-half_w, 0.0, half_w]:
		for z: float in [-half_l, 0.0, half_l]:
			var hero: Vector2 = Vector2(x, z)
			assert_eq(CAMERA.camera_target(hero, false, reach), hero + CAMERA.ground_forward(false) * ahead, "own view at %s" % hero)
			assert_eq(CAMERA.camera_target(hero, true, reach), hero + CAMERA.ground_forward(true) * ahead, "flipped view at %s" % hero)
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
	assert_lt(CAMERA.ground_offset(reach, 0.0), -30.0, "the very top of the screen is street, 30 m and more ahead (no sky)")
	assert_ne(CAMERA.ground_offset(reach, 0.0), -INF, "the steep angle never shows the sky")
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
	# the camera is only about 11 m up: with a plane any further out, the ground and props
	# directly beneath and in front of it are cut off
	var height: float = CAMERA.camera_offset(CAMERA.distance(PHONE), false).y
	assert_lt(camera.near, 1.0, "the near plane is about half a metre out")
	assert_lt(camera.near, height / 4.0, "well inside the camera's height")
	assert_lt(camera.far / camera.near, 1000.0, "the depth range is still sane")



## Fraction of the screen height a `height`-metre-tall thing standing on the hero's spot fills
## (flat view, no lens), for a phone of `aspect`.
func _hero_height_fraction(height: float, aspect: float) -> float:
	var reach: float = CAMERA.distance(aspect)
	var pitch: float = deg_to_rad(CAMERA.pitch_degrees)
	var ground: float = reach * cos(pitch) - CAMERA.look_ahead(reach)  # camera foot to hero
	var half: float = tan(deg_to_rad(CAMERA.vfov_degrees / 2.0))
	var forward: Vector2 = Vector2(cos(pitch), -sin(pitch))
	var up: Vector2 = Vector2(sin(pitch), cos(pitch))
	var feet: Vector2 = Vector2(ground, -reach * sin(pitch))
	var head: Vector2 = feet + Vector2(0.0, height)
	return (head.dot(up) / head.dot(forward) - feet.dot(up) / feet.dot(forward)) / (2.0 * half)


func test_kids_fill_enough_of_the_screen_to_read() -> void:
	# a steeper camera shrinks the kid on screen (foreshortening), so the width is chosen with it
	# the kid is drawn kid_view_scale times its real size, so the street across the screen can be wide
	var fraction: float = _hero_height_fraction(KidModel.KID_HEIGHT * CAMERA.kid_view_scale, PHONE)
	assert_gt(fraction, 0.078, "the kid is at least ~8% of the screen height (100 px of 1280)")
	assert_lt(fraction, 0.10, "and not so big that the street gets narrow")


func test_the_street_is_wide_enough_at_the_heros_row() -> void:
	var reach: float = CAMERA.distance(PHONE)
	var pitch: float = deg_to_rad(CAMERA.pitch_degrees)
	var ground: float = reach * cos(pitch) - CAMERA.look_ahead(reach)
	var depth: float = Vector2(ground, -reach * sin(pitch)).dot(Vector2(cos(pitch), -sin(pitch)))
	var across: float = 2.0 * depth * tan(deg_to_rad(CAMERA.vfov_degrees / 2.0)) * PHONE
	assert_gt(across, 9.0, "at the hero's own row you see at least 9 m across (the old camera showed 8.9)")


func test_the_ground_is_not_squashed_too_flat_for_aiming() -> void:
	# a circle on the ground around the hero looks like an ellipse: height / width on screen
	var pitch: float = deg_to_rad(CAMERA.pitch_degrees)
	var reach: float = CAMERA.distance(PHONE)
	var ground: float = reach * cos(pitch) - CAMERA.look_ahead(reach)
	var depression: float = atan(reach * sin(pitch) / ground)
	assert_gt(sin(depression), 0.7, "ground circles look at least 70% as tall as wide (they were 75% to 80%)")


func test_the_hero_stays_on_the_screens_centre_line_with_the_yaw() -> void:
	# the aim point is `ahead` metres along the way the camera looks: the hero is straight below the middle of the screen
	var reach: float = CAMERA.distance(PHONE)
	for flip: bool in [false, true]:
		for turn: float in [-8.0, 0.0, 5.0, 8.0]:
			var hero: Vector2 = Vector2(3.0, 5.0)
			var target: Vector2 = CAMERA.camera_target(hero, flip, reach, turn)
			var offset: Vector3 = CAMERA.camera_offset(reach, flip, turn)
			var camera: Vector2 = target + Vector2(offset.x, offset.z)
			var view: Vector2 = (target - camera).normalized()
			var to_hero: Vector2 = (hero - camera).normalized()
			assert_almost_eq(view.cross(to_hero), 0.0, 0.0001, "hero on the line the camera looks along (flip %s, yaw %.0f)" % [flip, turn])


# ---- the lean toward the side the hero is on ---------------------------------------------

func test_the_camera_leans_toward_the_side_the_hero_is_on_and_eases_in_the_middle() -> void:
	assert_almost_eq(CAMERA.yaw_for(0.0, false), 0.0, 0.0001, "straight in the middle")
	assert_almost_eq(CAMERA.yaw_for(-CAMERA.yaw_side_reach, false), -8.0, 0.0001, "left curb: -8")
	assert_almost_eq(CAMERA.yaw_for(CAMERA.yaw_side_reach, false), 8.0, 0.0001, "right curb: +8")
	assert_almost_eq(CAMERA.yaw_for(-99.0, false), -8.0, 0.0001, "never more than the full lean")
	var last: float = CAMERA.yaw_for(-12.0, false)
	for x: float in [-9.0, -6.0, -3.0, 0.0, 3.0, 6.0, 9.0, 12.0]:
		var now: float = CAMERA.yaw_for(x, false)
		assert_gt(now, last, "it turns steadily from the left to the right (x %.0f)" % x)
		last = now
	assert_lt(absf(CAMERA.yaw_for(1.0, false)), 0.2, "a step to the side hardly turns it (it eases in)")


func test_the_other_team_leans_the_same_way_on_its_own_screen() -> void:
	# its left of the screen is world +x: the same lean on the same side of the screen
	assert_almost_eq(CAMERA.yaw_for(CAMERA.yaw_side_reach, true), -8.0, 0.0001, "left of its screen")
	assert_almost_eq(CAMERA.yaw_for(-CAMERA.yaw_side_reach, true), 8.0, 0.0001, "right of its screen")


func test_the_follow_camera_eases_its_turn_as_the_hero_crosses_and_snaps_when_told() -> void:
	var camera: FollowCamera = autofree(FollowCamera.new()) as FollowCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	camera.follow(Vector2(-10.0, 0.0), false)
	assert_almost_eq(camera.yaw_offset(), CAMERA.yaw_for(-10.0, false), 0.0001, "placed straight away")
	camera.follow(Vector2(10.0, 0.0), false, 1.0 / 30.0)
	var after_one_frame: float = camera.yaw_offset()
	assert_gt(after_one_frame, CAMERA.yaw_for(-10.0, false), "it has started to turn")
	assert_lt(after_one_frame, CAMERA.yaw_for(10.0, false), "but not jumped")
	for i: int in 120:
		camera.follow(Vector2(10.0, 0.0), false, 1.0 / 30.0)
	assert_almost_eq(camera.yaw_offset(), CAMERA.yaw_for(10.0, false), 0.05, "and it gets there")
	assert_almost_eq(camera.rotation_degrees.y, camera.yaw_offset(), 0.001, "what you see is the yaw the controls use")
	camera.follow(Vector2(-10.0, 0.0), false)
	assert_almost_eq(camera.yaw_offset(), CAMERA.yaw_for(-10.0, false), 0.0001, "delta 0 snaps")


func test_the_kids_are_drawn_bigger_than_they_are_but_the_hit_area_is_not() -> void:
	assert_almost_eq(CAMERA.kid_view_scale, 1.35, 0.0001, "bigger on screen so the street can be wider")
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	for view: Node in practice.get_node("%Actors").get_children():
		assert_almost_eq((view as Node3D).scale.x, CAMERA.kid_view_scale, 0.0001, "every kid the same")
	assert_almost_eq(practice.overhead().view_scale, CAMERA.kid_view_scale, 0.0001, "the health bar rides on the drawn head")


func test_the_camera_peeks_toward_a_dragged_aim_and_comes_back() -> void:
	var camera: FollowCamera = autofree(FollowCamera.new()) as FollowCamera
	camera.rules = CAMERA
	camera.layout = LAYOUT
	add_child(camera)
	var hero: Vector2 = Vector2(0.0, 5.0)
	camera.follow(hero, false)
	var rest: Vector3 = camera.position
	var aim: Vector2 = Vector2(0.0, -1.0)
	var peek: Vector2 = CAMERA.peek_for(aim, 9.0)
	assert_almost_eq(peek.length(), CAMERA.peek_max, 0.0001, "a long reach is capped")
	assert_almost_eq(CAMERA.peek_for(aim, 4.0).length(), 4.0 * CAMERA.peek_fraction, 0.0001, "half the reach of a short one")
	assert_eq(CAMERA.peek_for(Vector2.ZERO, 9.0), Vector2.ZERO, "no aim, no peek")
	camera.follow(hero, false, 1.0 / 30.0, peek)
	assert_gt(camera.peek_offset().length(), 0.0, "it has started")
	assert_lt(camera.peek_offset().length(), peek.length(), "but not jumped")
	for i: int in 90:
		camera.follow(hero, false, 1.0 / 30.0, peek)
	assert_almost_eq(camera.peek_offset().length(), peek.length(), 0.05, "and it gets there")
	assert_lt(camera.position.z, rest.z - 3.0, "the camera has moved toward the aim (up the street)")
	for i: int in 120:
		camera.follow(hero, false, 1.0 / 30.0)
	assert_lt(camera.peek_offset().length(), 0.05, "it comes back when the aim is let go")
	assert_almost_eq(camera.position.z, rest.z, 0.1)
