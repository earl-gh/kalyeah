extends GutTest
## A held card dragged outside the play area (docs/GDD.md "Controls: tap to move, cards to cast"): the aim shape and its drop circle go
## red and letting go there throws nothing (no tint or outline on the screen).
## Seam: MatchInput.card_drag (the `inside` flag), PracticeMatch.card_moved, AimIndicator.

const NOW: int = 10000
const DT: float = 1.0 / 30.0
const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")

var _kind: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	Session.practice_kind = &"match"


func after_each() -> void:
	Session.practice_kind = _kind


func _input_with_card() -> MatchInput:
	var input: MatchInput = MatchInput.new()
	input.tap_time = 0.3
	input.card_press(1, NOW)
	return input


func test_a_finger_over_the_ground_outside_the_street_aims_in_red_and_letting_go_cancels() -> void:
	var input: MatchInput = _input_with_card()
	input.card_drag(false, Vector2(14.0, 0.0), false)
	assert_true(input.out_of_bounds)
	assert_false(input.over_street, "not a place to cast")
	assert_true(input.aimed)
	assert_eq(input.aim_point, Vector2(14.0, 0.0), "the shape still follows the finger")
	assert_eq(input.release_kind(NOW + 900), MatchInput.Release.CANCEL)
	var built: PlayerInput = input.build(1)
	assert_false(built.is_pressed(PlayerInput.BTN_AIM_POINT), "no aim point is sent while it is outside")


func test_back_inside_the_street_it_casts_again() -> void:
	var input: MatchInput = _input_with_card()
	input.card_drag(false, Vector2(14.0, 0.0), false)
	input.card_drag(false, Vector2(3.0, -4.0), true)
	assert_false(input.out_of_bounds)
	assert_true(input.over_street)
	assert_eq(input.release_kind(NOW + 900), MatchInput.Release.CAST)


func test_over_the_hand_is_not_out_of_bounds() -> void:
	var input: MatchInput = _input_with_card()
	input.card_drag(false, null)
	assert_false(input.out_of_bounds, "over the hand the old red shape is enough")
	assert_eq(input.release_kind(NOW + 900), MatchInput.Release.CANCEL)


func test_the_flag_is_cleared_when_the_card_is_let_go_or_a_new_one_picked_up() -> void:
	var input: MatchInput = _input_with_card()
	input.card_drag(false, Vector2(14.0, 0.0), false)
	input.card_release(NOW + 900)
	assert_false(input.out_of_bounds)
	input.card_press(2, NOW + 2000)
	assert_false(input.out_of_bounds)


func test_dragging_a_card_off_the_street_in_a_match_makes_the_aim_red_and_back_on_it_takes_it_away() -> void:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 3:
		practice.advance(DT)
	var camera: ArenaCamera = practice._camera
	var lane: Rect2 = practice.layout.lane_rect()
	var outside: Vector2 = camera.screen_position(Vector3(lane.end.x + 1.5, 0.0, 0.0))
	var inside: Vector2 = camera.screen_position(Vector3(0.0, 0.0, 0.0))
	practice.card_pressed(0)
	practice.card_moved(0, inside, false, false)
	practice.advance(DT)
	assert_false(practice.input.out_of_bounds, "on the street it is a normal aim")
	assert_true(practice.input.over_street)
	practice.card_moved(0, outside, false, false)
	practice.advance(DT)
	assert_true(practice.input.out_of_bounds, "a point outside the street")
	assert_false(practice.input.over_street)
	assert_eq(practice.input.release_kind(Time.get_ticks_msec()), MatchInput.Release.CANCEL, "letting go there throws nothing")
	assert_null(practice.find_child("PlayAreaGuard", true, false), "no tint or outline on the screen")
	practice.card_moved(0, inside, false, false)
	practice.advance(DT)
	assert_false(practice.input.out_of_bounds, "back on the street the red goes")
	practice.card_released(0, inside, false, false)


func test_the_aim_shape_outside_the_street_is_drawn_in_the_cancel_red() -> void:
	assert_eq(AimIndicator.color_for(true, true), AimIndicator.CANCEL_COLOR)
	assert_gt(AimIndicator.CANCEL_COLOR.r, AimIndicator.CANCEL_COLOR.b + 0.4)
