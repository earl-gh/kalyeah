extends GutTest
## The players' point of view: third-person over the shoulder like the hero shooters (owner, 2026-11-09: like Marvel Rivals or Deadlock;
## docs/GDD.md "Shoulder view"). A perspective camera behind and to the right of your kid looks where you look; a drag on the right of the
## screen turns it; the joystick walks the kid relative to the view; a card is cast at the crosshair in the middle of the screen.
## Seam: CameraRules.shoulder_pose, ArenaCamera (look, crosshair), LookTouch, AbilityArc (shoulder), PracticeMatch.crosshair_point.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _kind: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	Session.practice_kind = &"match"


func after_each() -> void:
	Session.practice_kind = _kind


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 3:
		practice.advance(DT)
	return practice


func test_the_data_puts_the_camera_over_the_shoulder() -> void:
	var rules: CameraRules = CAMERA
	assert_true(rules.third_person, "the shoulder view is on")
	assert_gt(rules.shoulder_distance, 0.0, "behind the kid")
	assert_gt(rules.shoulder_offset, 0.0, "to its right: the kid stands left of the crosshair")
	assert_lt(rules.look_pitch_min, 0.0, "you can look up a little")
	assert_gt(rules.look_pitch_max, rules.look_pitch_degrees, "and further down")


func test_the_pose_stands_exactly_the_distance_behind_the_kid_on_the_ground_and_looks_at_it() -> void:
	var rules: CameraRules = CAMERA
	assert_eq(rules.shoulder_distance, 12.0, "12 m behind the kid (owner)")
	var pose: Array[Vector3] = rules.shoulder_pose(Vector2.ZERO, 0.0, 10.0)
	assert_gt(pose[0].z, 0.0, "looking down -Z it stands on the +Z side: behind")
	assert_gt(pose[0].x, 0.0, "and to the right")
	assert_gt(pose[0].y, 0.0, "above the ground")
	assert_eq(pose[1], Vector3(-10.0, 0.0, 0.0), "tilted down, turned the view's way")
	# anywhere on the street, the ends too, whichever way it looks: exactly the distance behind on the ground, nothing pulls it in
	for feet: Vector2 in [Vector2.ZERO, Vector2(0.0, LAYOUT.lane_length / 2.0 - 1.0), Vector2(LAYOUT.lane_width / 2.0 - 1.0, 0.0)]:
		for yaw: float in [0.0, 90.0, 180.0, 270.0, 37.0]:
			var at: Array[Vector3] = rules.shoulder_pose(feet, yaw, rules.look_pitch_degrees)
			var basis: Basis = Basis.from_euler(at[1] * (PI / 180.0))
			var ground: Vector2 = Vector2(at[0].x, at[0].z) - feet
			var right: Vector2 = Vector2(basis.x.x, basis.x.z).normalized()
			var behind: float = ground.dot(Vector2(basis.z.x, basis.z.z).normalized())
			assert_almost_eq(behind, rules.shoulder_distance, 0.001, "%s yaw %d: %.1f m behind" % [feet, yaw, behind])
			assert_almost_eq(ground.dot(right), rules.shoulder_offset, 0.001, "and the shoulder offset to its right")
			# the view's middle line passes over the kid: the point it looks at, shoulder_height above the feet, is straight ahead
			var pivot: Vector3 = Vector3(feet.x, rules.shoulder_height, feet.y) + basis.x * rules.shoulder_offset
			assert_almost_eq((pivot - at[0]).normalized().dot(-basis.z), 1.0, 0.0001, "it looks at the kid")
	assert_eq(rules.clamp_pitch(200.0), rules.look_pitch_max)
	assert_eq(rules.clamp_pitch(-200.0), rules.look_pitch_min)


func test_a_match_starts_looking_down_the_street_from_behind_your_kid_with_a_crosshair() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice._camera
	assert_eq(camera.projection, Camera3D.PROJECTION_PERSPECTIVE, "a perspective lens, like a shooter")
	assert_eq(camera.fov, CAMERA.shoulder_fov)
	assert_eq(camera.look_pitch, CAMERA.look_pitch_degrees)
	var toward: Vector2 = camera.ground_axes()[1]
	var enemy: Vector2 = LAYOUT.base_center(-practice.own_side())
	assert_gt(toward.dot(enemy - practice.sim.players[practice.local_id].position), 0.0, "it looks toward the other team's end")
	var kid: PlayerState = practice.sim.players[practice.local_id]
	var on_screen: Vector2 = camera.screen_position(Vector3(kid.position.x, 1.0, kid.position.y))
	var size: Vector2 = practice.get_viewport().get_visible_rect().size
	assert_lt(on_screen.x, size.x / 2.0, "your kid stands left of the crosshair (the shoulder)")
	assert_gt(on_screen.y, size.y / 2.0, "and below it")
	assert_true(practice.hud.shoulder)
	assert_not_null(practice.hud.crosshair)
	assert_true(practice.hud.crosshair.visible)
	assert_true(practice.hud.look.active, "a drag on the right turns the view while you play")
	assert_eq(camera.near_side(), 0, "no house is left out: the camera is in the street")


func test_a_drag_on_the_right_turns_the_view_and_the_joystick_follows_it() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice._camera
	var look: LookTouch = practice.hud.look
	var yaw: float = camera.look_yaw
	var press: InputEventScreenTouch = InputEventScreenTouch.new()
	press.index = 2
	press.pressed = true
	press.position = Vector2(look.zone_left + 200.0, 300.0)
	assert_true(look.handle(press), "a finger on the right takes the look")
	var drag: InputEventScreenDrag = InputEventScreenDrag.new()
	drag.index = 2
	drag.position = press.position + Vector2(40.0, 0.0)
	assert_true(look.handle(drag))
	assert_almost_eq(camera.look_yaw, fposmod(yaw - 40.0 * CAMERA.look_sensitivity, 360.0), 0.001, "dragged right: the view turns right")
	practice.knob_moved(Vector2(0.0, -1.0))
	assert_almost_eq(practice.input.stick.distance_to(camera.ground_axes()[1]), 0.0, 0.001, "the joystick pushed up walks the way you look")
	press.pressed = false
	assert_true(look.handle(press))
	var left: InputEventScreenTouch = InputEventScreenTouch.new()
	left.pressed = true
	left.position = Vector2(look.zone_left - 50.0, 300.0)
	assert_false(look.handle(left), "the left of the screen is the joystick's")


func test_looking_down_and_up_stops_at_the_limits() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice._camera
	camera.look(Vector2(0.0, 10000.0))
	assert_eq(camera.look_pitch, CAMERA.look_pitch_max)
	camera.look(Vector2(0.0, -10000.0))
	assert_eq(camera.look_pitch, CAMERA.look_pitch_min)


func test_a_card_goes_where_the_crosshair_is_and_the_x_puts_it_back() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	assert_true(arc.shoulder)
	var slot: int = -1
	for i: int in CardHand.SLOTS:
		if arc.pickable[i]:
			slot = i
			break
	if slot < 0:
		pass_test("no card ready (the deal had nothing affordable)")
		return
	var at: Vector2 = arc.get_global_transform_with_canvas() * arc.centers[slot]
	arc.press_at(4, at)
	arc._move(at)
	var expected: Vector2 = practice.crosshair_point()
	assert_true(practice.input.over_street)
	assert_almost_eq(practice.input.aim_point.distance_to(expected), 0.0, 0.01, "held, the card aims at the crosshair")
	var hit: Variant = practice.street_point(practice._camera.crosshair())
	assert_true(hit is Vector2, "looking down the street, the crosshair is on the ground")
	assert_true(practice.layout.lane_rect().has_point(expected), "inside the street")
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CAST, "even a quick tap casts at the crosshair")
	var yaw: float = practice._camera.look_yaw
	arc._move(at + Vector2(30.0, 0.0))
	assert_ne(practice._camera.look_yaw, yaw, "the finger holding the card turns the view")
	arc._move(arc.get_global_transform_with_canvas() * arc.cancel_center)
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CANCEL, "let go on the X: put back")
	arc.cancel_hold()


func test_looking_at_the_sky_casts_along_the_view_inside_the_street() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice._camera
	camera.look(Vector2(0.0, -10000.0))  # looking up as far as it goes: the crosshair's ray never meets the ground
	assert_null(practice.street_point(camera.crosshair()), "nothing under the crosshair")
	var kid: Vector2 = practice.sim.players[practice.local_id].position
	var point: Vector2 = practice.crosshair_point()
	assert_true(practice.layout.lane_rect().has_point(point), "kept inside the street")
	assert_gt((point - kid).dot(camera.ground_axes()[1]), 0.0, "ahead of the kid, the way it looks")


func test_the_minimap_stays_still_and_shows_a_cone_where_you_look() -> void:
	var practice: PracticeMatch = _practice()
	var minimap: StreetMinimap = practice.hud.minimap
	var before: Array[Vector2] = minimap.axes.duplicate()
	practice._camera.look(Vector2(200.0, 0.0))
	practice.advance(DT)
	assert_eq(minimap.axes, before, "the minimap does not turn with the view")
	assert_ne(minimap.look_way, Vector2.ZERO, "a cone shows where you look")
	assert_almost_eq(minimap.look_way.distance_to(practice._camera.ground_axes()[1]), 0.0, 0.001)


func test_a_held_card_keeps_following_the_crosshair_while_the_kid_walks_and_the_view_turns() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	var slot: int = -1
	for i: int in CardHand.SLOTS:
		if arc.pickable[i]:
			slot = i
			break
	if slot < 0:
		pass_test("no card ready (the deal had nothing affordable)")
		return
	var at: Vector2 = arc.get_global_transform_with_canvas() * arc.centers[slot]
	arc.press_at(4, at)
	arc._move(at)
	var first: Vector2 = practice.input.aim_point
	practice.knob_moved(Vector2(1.0, 0.0))
	practice._camera.look(Vector2(-80.0, 0.0))
	for i: int in 6:
		practice.advance(DT)
	practice.knob_moved(Vector2.ZERO)
	for i: int in 3:
		practice.advance(DT)  # (the aim is taken as a tick's input is made, and the camera follows the kid as drawn: stopped, they agree)
	assert_ne(practice.input.aim_point, first, "the aim moved with the view, though the finger did not")
	assert_almost_eq(practice.input.aim_point.distance_to(practice.crosshair_point()), 0.0, 0.01, "it is where the crosshair is now")
	arc.cancel_hold()


func test_things_closer_than_your_kid_are_cut_away_not_drawn_over_the_screen() -> void:
	var rules: CameraRules = CAMERA
	var practice: PracticeMatch = _practice()
	assert_eq(practice._camera.near, rules.shoulder_near)
	var to_kid: float = Vector2(rules.shoulder_distance, rules.shoulder_offset).length()
	assert_lt(rules.shoulder_near, to_kid - 1.0, "your own kid is always drawn")


func test_a_quick_tap_on_a_heal_card_heals_yourself_not_the_crosshair() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.set_loadout(practice.local_id, &"heal", &"tsinelas_light")
	practice.card_pressed(0)
	practice.card_moved(0, Vector2.ZERO, true, false)
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.TAP, "a tap: the sim heals you (docs/GDD.md)")
	practice.card_moved(0, Vector2.ZERO, false, false)
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CAST, "held and turned: it goes to the crosshair (a teammate there)")
	practice.card_cancelled(0)


func test_at_your_own_end_of_the_street_your_kid_stays_on_the_screen() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = practice.sim.players[practice.local_id]
	var end: float = LAYOUT.lane_length / 2.0 - 1.0
	me.position = Vector2(0.0, end if practice.own_side() == MapLayout.SIDE_OWN else -end)
	for i: int in 3:
		practice.advance(DT)
	var camera: ArenaCamera = practice._camera
	var size: Vector2 = practice.get_viewport().get_visible_rect().size
	for height: float in [0.2, 1.8, 3.4]:
		var point: Vector3 = Vector3(me.position.x, height, me.position.y)
		assert_false(camera.is_position_behind(point))
		var on_screen: Vector2 = camera.unproject_position(point)
		assert_true(Rect2(Vector2.ZERO, size).has_point(on_screen), "the kid at %.1f m is on the screen (%s)" % [height, on_screen])
