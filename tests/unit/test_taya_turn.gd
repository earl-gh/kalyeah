extends GutTest
## Tumbang Preso's taya turn (owner): becoming the taya swings the street half way round (the throwers come at you from the top), eased
## over `CameraRules.turn_seconds`; the controls swap halfway through; no longer the taya, it swings back. Reduce motion cuts.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _game: StringName
var _kind: StringName
var _coach: bool
var _reduced: bool


func before_each() -> void:
	_game = Session.practice_game
	_kind = Session.practice_kind
	_coach = Session.coach_pending
	_reduced = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Session.practice_game = _game
	Session.practice_kind = _kind
	Session.coach_pending = _coach
	Settings.reduced_motion = _reduced


func _practice(game: StringName) -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = game
	Session.coach_pending = false
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice._camera.set_process(false)  # the test moves the turn on by hand
	practice.finish_pick()
	practice.skip_intro()
	practice._presentation.sync(DT)
	return practice


func _seconds(practice: PracticeMatch) -> float:
	return practice._camera.rules.turn_seconds


## Makes the local kid the taya (or not) and lets the view see it.
func _set_taya(practice: PracticeMatch, mine: bool) -> void:
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	game.taya_id = practice.local_id if mine else _someone_else(practice)
	practice._presentation.sync(DT)


func _someone_else(practice: PracticeMatch) -> int:
	for id: int in practice.sim.players:
		if id != practice.local_id:
			return id
	return -1


func test_a_thrower_sees_the_street_from_the_throw_line_and_it_never_turns() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	_set_taya(practice, false)
	for i: int in 10:
		practice._presentation.sync(DT)
		practice._camera.advance_turn(DT)
	assert_false(practice._camera.is_turning())
	assert_eq(practice._camera.turn_degrees(), 0.0)
	assert_eq(practice.shown_side(), MapLayout.SIDE_OWN)


func test_becoming_the_taya_swings_the_street_half_way_round_eased() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	_set_taya(practice, false)
	_set_taya(practice, true)
	var camera: ArenaCamera = practice._camera
	assert_true(camera.is_turning(), "the turn starts")
	assert_eq(camera.turn_degrees(), 0.0, "and does not cut")
	camera.advance_turn(_seconds(practice) * 0.1)
	assert_lt(camera.turn_degrees(), 18.0, "eased in: slower than an even turn at the start")
	camera.advance_turn(_seconds(practice) * 0.4)
	assert_almost_eq(camera.turn_degrees(), 90.0, 0.5, "a quarter turn at half the time")
	camera.advance_turn(_seconds(practice) * 0.5)
	assert_false(camera.is_turning(), "done after turn_seconds")
	assert_almost_eq(camera.position, camera.desired_position(true), Vector3.ONE * 0.001, "ends exactly where the flipped view stands")
	assert_almost_eq(camera.rotation_degrees.y, camera.rules.yaw_degrees(true), 0.001)
	assert_eq(practice.shown_side(), MapLayout.SIDE_ENEMY)


func test_the_controls_and_the_map_swap_halfway_through_the_turn() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	_set_taya(practice, false)
	_set_taya(practice, true)
	var up: Vector2 = Vector2(0.0, -1.0)
	practice._camera.advance_turn(_seconds(practice) * 0.45)
	practice.knob_moved(up)
	assert_eq(practice.input.stick, up, "before halfway, up on the stick is still up the old view")
	practice._presentation.sync(DT)
	assert_eq(practice._presentation._viewed_side, MapLayout.SIDE_OWN, "the map is still dressed for the old view")
	practice._camera.advance_turn(_seconds(practice) * 0.1)
	practice.knob_moved(up)
	assert_eq(practice.input.stick, -up, "past halfway, up on the stick is up the new view")
	practice._presentation.sync(DT)
	assert_eq(practice._presentation._viewed_side, MapLayout.SIDE_ENEMY, "and the map is dressed for it")


func test_no_longer_the_taya_swings_back() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	_set_taya(practice, true)
	practice._camera.advance_turn(_seconds(practice))
	assert_eq(practice._camera.turn_degrees(), 180.0)
	_set_taya(practice, false)
	assert_true(practice._camera.is_turning(), "swings back")
	practice._camera.advance_turn(_seconds(practice))
	assert_eq(practice._camera.turn_degrees(), 0.0)
	assert_eq(practice.shown_side(), MapLayout.SIDE_OWN)


func test_reduce_motion_cuts_instead_of_turning() -> void:
	Settings.reduced_motion = true
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	_set_taya(practice, false)
	_set_taya(practice, true)
	assert_false(practice._camera.is_turning())
	assert_eq(practice._camera.turn_degrees(), 180.0)


func test_a_flip_without_smooth_still_cuts() -> void:
	# Agawan Base and Patintero keep the instant cut
	var practice: PracticeMatch = _practice(&"street")
	var camera: ArenaCamera = practice._camera
	camera.place(true)
	assert_false(camera.is_turning())
	assert_eq(camera.turn_degrees(), 180.0)
	assert_almost_eq(camera.position, camera.desired_position(true), Vector3.ONE * 0.001)
