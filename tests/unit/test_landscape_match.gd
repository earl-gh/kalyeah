extends GutTest
## The landscape match, Wild Rift style (owner, 2026-11-09; docs/GDD.md "Landscape layout"): the phone turns sideways, the camera follows your
## kid with the street running left to right (your base at the left), a joystick under the left thumb walks the kid, the four cards are a
## round ability arc under the right thumb aimed by dragging from the button (no minimap: owner). The map keeps its 1:2 shape.
## Seam: Settings.set_landscape, CameraRules.follow_aim / ground_way, ArenaCamera (follow), MoveStick, AbilityArc, PracticeMatch.

const TOP_DOWN_MATCH: GDScript = preload("res://tests/fixtures/top_down_match.gd")
const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const NOW: int = 10000
## Phones on their side on the landscape base: a 16:9 one and a long 19.5:9 one (the UI's height is 720 either way).
const SCREENS: Array[Vector2] = [Vector2(1280.0, 720.0), Vector2(1560.0, 720.0)]

var _kind: StringName


## These tests pin the top-down follow camera (`CameraRules.follow`); the shoulder view that is on by default has its own tests
## (test_shoulder_view.gd).
func before_each() -> void:
	_kind = Session.practice_kind
	Session.practice_kind = &"match"
	TOP_DOWN_MATCH.use(true)


func after_each() -> void:
	Session.practice_kind = _kind
	TOP_DOWN_MATCH.use(false)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 3:
		practice.advance(DT)
	return practice


# ---- data and math ----------------------------------------------------------------

func test_the_data_turns_the_camera_to_look_across_the_street_and_follow_the_kid() -> void:
	var rules: CameraRules = CAMERA
	assert_true(rules.follow, "the follow camera is on")
	assert_eq(rules.yaw_offset_degrees, 90.0, "a quarter turn: the street runs left to right")
	assert_eq(rules.pitch_degrees, 45.0, "the same 45 degree tilt (docs/ART.md)")
	assert_gt(rules.follow_view_size, 0.0)
	var layout: MapLayout = LAYOUT
	assert_eq(layout.tiles, Vector2i(58, 58), "58 x 58 grid enclosing the 168 m circular barangay")
	assert_almost_eq(layout.arena_radius, 84.0, 0.001)


func test_with_a_quarter_turn_right_on_the_screen_is_toward_the_enemy_end_and_up_is_minus_x() -> void:
	assert_eq(CameraRules.ground_way(Vector2.RIGHT, 90.0), Vector2(0.0, -1.0), "right: toward the -Z end (the other team's)")
	assert_eq(CameraRules.ground_way(Vector2.LEFT, 90.0), Vector2(0.0, 1.0), "left: toward your own +Z end")
	assert_eq(CameraRules.ground_way(Vector2.UP, 90.0), Vector2(-1.0, 0.0), "up the screen: toward -X")
	assert_eq(CameraRules.ground_way(Vector2.RIGHT, 270.0), Vector2(0.0, 1.0), "the other team's view is turned half way round")
	assert_eq(CameraRules.ground_way(Vector2(0.0, -1.0), 0.0), Vector2(0.0, -1.0), "the fixed portrait camera: up the screen is -Z, as before")


func test_the_followed_view_keeps_the_kid_in_the_middle_and_stops_at_the_ends_of_the_street() -> void:
	var rules: CameraRules = CAMERA
	var arena: Vector2 = Vector2(LAYOUT.lane_width, LAYOUT.lane_length)
	var aspect: float = 16.0 / 9.0
	var middle: Vector2 = rules.follow_aim(Vector2(0.0, 4.0), arena, rules.follow_view_size, aspect, 90.0)
	assert_almost_eq(middle.y, 4.0, 0.001, "along the street the view follows the kid")
	var far: Vector2 = rules.follow_aim(Vector2(0.0, -arena.y / 2.0), arena, rules.follow_view_size, aspect, 90.0)
	var half_seen: float = rules.follow_view_size * aspect / 2.0
	assert_almost_eq(far.y, -(arena.y / 2.0 + rules.follow_edge - half_seen), 0.001, "at the end it stops: no more than follow_edge past the street")
	var side: Vector2 = rules.follow_aim(Vector2(arena.x / 2.0, 0.0), arena, rules.follow_view_size, aspect, 90.0)
	var seen_across: float = rules.follow_view_size / sin(deg_to_rad(rules.pitch_degrees))
	assert_lte(absf(side.x), maxf(0.0, arena.x / 2.0 + rules.follow_edge - seen_across / 2.0) + 0.001, "across, the whole width stays in view as far as it can")


# ---- the match --------------------------------------------------------------------

func test_a_match_is_landscape_with_a_joystick_an_ability_arc_and_no_tray_and_leaving_turns_it_back() -> void:
	var practice: PracticeMatch = _practice()
	assert_true(Settings.landscape, "the match turned the phone sideways")
	assert_eq(get_window().content_scale_size, Settings.LANDSCAPE_BASE)
	var hud: MatchHud = practice.hud
	assert_true(hud.landscape)
	assert_true(hud.hand is AbilityArc, "the cards are the round ability arc")
	assert_not_null(hud.stick, "a joystick")
	assert_false(hud.console.visible, "no tray")
	assert_eq(hud.console_height(), 0.0)
	assert_true(hud.stick.active, "the joystick takes touches while you play")
	assert_false(hud.street.active, "a tap on the street does not walk the kid: the joystick does")
	remove_child(practice)
	practice.free()
	assert_true(Settings.landscape, "widescreen 1280x720 stays active across menus and matches")
	assert_eq(get_window().content_scale_size, Settings.LANDSCAPE_BASE)


func test_the_camera_follows_your_kid_and_your_base_is_at_the_left() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice._camera
	var kid: Vector2 = practice.sim.players[practice.local_id].position
	var expected: Vector2 = CAMERA.follow_aim(kid, Vector2(LAYOUT.lane_width, LAYOUT.lane_length), CAMERA.follow_view_size, camera.screen_aspect(), CAMERA.yaw_degrees(practice._flip()))
	assert_almost_eq(camera.aim_point().distance_to(expected), 0.0, 0.01, "the view aims where it keeps the kid in the middle")
	assert_eq(camera.view_size(), CAMERA.follow_view_size)
	var own: Vector2 = LAYOUT.base_center(practice.own_side())
	var enemy: Vector2 = LAYOUT.base_center(-practice.own_side())
	var own_x: float = camera.screen_position(Vector3(own.x, 0.0, own.y)).x
	var enemy_x: float = camera.screen_position(Vector3(enemy.x, 0.0, enemy.y)).x
	assert_lt(own_x, enemy_x, "your base is to the left of theirs")


func test_the_row_of_houses_between_the_camera_and_the_street_is_left_out() -> void:
	var practice: PracticeMatch = _practice()
	var near: int = practice._camera.near_side()
	assert_ne(near, 0, "the turned camera stands over one side of the street")
	var map: StreetMap = practice._map
	assert_gt(map.house_rows[near].size(), 0)
	for house: Node3D in map.house_rows[near]:
		assert_false(house.visible, "the near row is hidden")
	for house: Node3D in map.house_rows[-near]:
		assert_true(house.visible, "the far row stays")


func test_the_joystick_walks_the_kid_the_way_it_points_on_the_screen() -> void:
	var practice: PracticeMatch = _practice()
	var stick: MoveStick = practice.hud.stick
	var start: Vector2 = practice.sim.players[practice.local_id].position
	var press: InputEventScreenTouch = InputEventScreenTouch.new()
	press.index = 3
	press.pressed = true
	press.position = stick.get_global_transform_with_canvas() * stick.rest_center()
	assert_true(stick.handle(press), "a finger in the left zone takes the stick")
	var drag: InputEventScreenDrag = InputEventScreenDrag.new()
	drag.index = 3
	drag.position = press.position + Vector2(MoveStick.BASE_RADIUS, 0.0)
	assert_true(stick.handle(drag))
	assert_eq(stick.way, Vector2.RIGHT)
	assert_eq(practice.input.stick, practice._camera.ground_way(Vector2.RIGHT), "the joystick's way is a way on the street")
	for i: int in 10:
		practice.advance(DT)
	var moved: Vector2 = practice.sim.players[practice.local_id].position - start
	var on_screen: Vector2 = practice._camera.screen_position(Vector3(moved.x, 0.0, moved.y)) - practice._camera.screen_position(Vector3.ZERO)
	assert_gt(on_screen.x, 10.0, "pushed right: the kid walks right on the screen")
	press.pressed = false
	assert_true(stick.handle(press))
	assert_eq(stick.way, Vector2.ZERO, "let go: it stops")


func test_a_finger_outside_the_joystick_zone_is_left_alone() -> void:
	var stick: MoveStick = autofree(MoveStick.new()) as MoveStick
	add_child(stick)
	stick.set_anchors_preset(Control.PRESET_TOP_LEFT)  # (it fills its parent in a match; here it is sized by hand)
	stick.size = SCREENS[0]
	stick.active = true
	var press: InputEventScreenTouch = InputEventScreenTouch.new()
	press.pressed = true
	press.position = Vector2(SCREENS[0].x * 0.8, 500.0)
	assert_false(stick.handle(press), "the right of the screen is the arc's")
	press.position = Vector2(100.0, 60.0)
	assert_false(stick.handle(press), "the top band is the header's")
	stick.active = false
	press.position = Vector2(100.0, 500.0)
	assert_false(stick.handle(press), "not while the controls are off")


# ---- the ability arc --------------------------------------------------------------

func test_the_four_buttons_and_next_fit_the_bottom_right_corner_apart_and_clear_of_the_joystick() -> void:
	for screen: Vector2 in SCREENS:
		for inset: Vector2 in [Vector2.ZERO, Vector2(60.0, 24.0)]:
			var arc: AbilityArc = autofree(AbilityArc.new()) as AbilityArc
			add_child(arc)
			arc.size = screen
			arc.set_inset(inset)
			var safe: Rect2 = Rect2(Vector2.ZERO, screen - inset)
			var slot_radius: float = AbilityArc.BUTTON_RADIUS if arc.uses_elixir else AbilityArc.HERO_BTN_RADIUS
			for slot: int in CardHand.SLOTS:
				var button: Rect2 = arc.card_rects[slot]
				assert_true(safe.encloses(button), "%s %s: button %d is on the screen, clear of the notch and home bar" % [screen, inset, slot])
				assert_gt(button.position.x, screen.x * MoveStick.ZONE_SHARE, "right of the joystick's zone")
				for other: int in range(slot + 1, CardHand.SLOTS):
					assert_gt(arc.centers[slot].distance_to(arc.centers[other]), slot_radius * 2.0, "buttons %d and %d do not overlap" % [slot, other])
			assert_true(safe.encloses(arc.next_rect), "NEXT / primary anchor is on the screen")
			assert_true(safe.encloses(arc.bar_rect), "the ice water bar is on the screen")
			assert_gt(arc.bar_rect.size.x, 100.0, "and long enough to read")
			var top_slot: int = 3 if arc.uses_elixir else 2
			assert_lt(arc.cancel_center.y - AbilityArc.CANCEL_RADIUS, arc.card_rects[top_slot].position.y, "cancel stands above the arc")
			assert_gt(arc.cancel_center.y - AbilityArc.CANCEL_RADIUS, 100.0, "and below the header")


func test_dragging_from_a_button_aims_that_share_of_the_cards_range_from_the_kid_and_letting_go_casts_there() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	var slot: int = _ready_slot(practice)
	if slot < 0:
		pass_test("no card ready to aim (the deal had nothing affordable)")
		return
	var at: Vector2 = arc.get_global_transform_with_canvas() * arc.centers[slot]
	arc.press_at(7, at)
	assert_eq(arc.held_slot, slot)
	var drag_to: Vector2 = at + Vector2(-AbilityArc.AIM_RADIUS * 0.5, 0.0)
	arc._move(drag_to)
	assert_almost_eq(arc.aim_way.x, -0.5, 0.01, "half the drag radius to the left: half the range to the left")
	assert_false(arc.on_card)
	assert_true(practice.input.over_street, "the drag aims at the street")
	var state: PlayerState = practice.sim.players[practice.local_id]
	var def: WeaponDef = practice.sim.weapon_defs[state.weapons[slot]]
	var expected: Vector2 = state.position + practice._camera.ground_way(Vector2(-0.5, 0.0)) * def.max_range
	var lane: Rect2 = LAYOUT.lane_rect()
	var clamped: Vector2 = LAYOUT.clamp_to_arena(expected, practice.rules.player_radius) if LAYOUT.is_circular() else expected.clamp(lane.position, lane.end)
	assert_almost_eq(practice.input.aim_point.distance_to(clamped), 0.0, 0.01, "that share of the range, that way, inside the arena")
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CAST, "letting go casts there")


func test_a_quick_tap_on_a_button_casts_at_once_and_letting_go_on_cancel_puts_it_back() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	var slot: int = _ready_slot(practice)
	if slot < 0:
		pass_test("no card ready (the deal had nothing affordable)")
		return
	var at: Vector2 = arc.get_global_transform_with_canvas() * arc.centers[slot]
	arc.press_at(7, at)
	arc._move(at + Vector2(4.0, 0.0))
	assert_true(arc.on_card, "a few pixels is still a tap")
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.TAP, "a tap: the sim aims")
	arc._move(arc.get_global_transform_with_canvas() * arc.cancel_center)
	assert_true(arc.over_hand, "over the cancel button")
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CANCEL, "letting go there puts the card back")
	arc.cancel_hold()


func test_a_card_that_is_not_ready_cannot_be_picked_up() -> void:
	var arc: AbilityArc = autofree(AbilityArc.new()) as AbilityArc
	add_child(arc)
	arc.size = SCREENS[0]
	arc.active = true
	arc.defs[0] = WeaponDef.new()
	arc.pickable[0] = false
	arc.press_at(1, arc.centers[0])
	assert_eq(arc.held_slot, -1)
	assert_true(arc.denied(0), "it shakes")


## The first slot whose card can be cast now (-1: none). Practice deals a full bar, so normally slot 0.
func _ready_slot(practice: PracticeMatch) -> int:
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	for slot: int in CardHand.SLOTS:
		if arc.pickable[slot]:
			return slot
	return -1


func test_the_aim_guide_shows_on_the_street_while_a_button_is_dragged() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	var slot: int = _ready_slot(practice)
	if slot < 0:
		pass_test("no card ready (the deal had nothing affordable)")
		return
	var at: Vector2 = arc.get_global_transform_with_canvas() * arc.centers[slot]
	arc.press_at(7, at)
	arc._move(at + Vector2(-AbilityArc.AIM_RADIUS * 0.6, 0.0))
	practice.advance(DT)
	assert_true(practice.hud.playing_controls(), "playing: the joystick is live (there is no tap on the street)")
	assert_gt(practice._aim_indicator.mesh.get_surface_count(), 0, "the guide shows where the card goes")
	if practice.hud.feedback.hint.text != MatchFeedback.NOBODY:
		assert_eq(practice.hud.feedback.hint.text, "Drag to the X to cancel", "the hint names the arc's cancel button, not the old hand")
	arc.cancel_hold()
