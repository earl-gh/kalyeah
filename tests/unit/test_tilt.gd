extends GutTest
## Tilt to run (opt-in) and Home's tilt parallax, from the phone's gravity sensor. Tilt only ever gives a direction: the kid runs at its
## one speed through the same move target a finger sets, so it can do nothing a finger cannot.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const DT: float = 1.0 / 30.0
const G: float = 9.81

var _was_tilt: bool
var _was_motion: bool


func before_each() -> void:
	_was_tilt = Settings.tilt_to_run
	_was_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	TiltInput.test_gravity = null


func after_each() -> void:
	Settings.set_tilt_to_run(_was_tilt)
	Settings.reduced_motion = _was_motion
	TiltInput.test_gravity = null


## Gravity for a phone leaning back `back` degrees from upright (toward the player) and turned `side` degrees (+ lowers the right edge,
## + `forward` lowers the top edge): the phone's axes are x right, y to the top edge, z out of the screen.
static func _held(back: float, side: float = 0.0, forward: float = 0.0) -> Vector3:
	var down: Vector3 = Vector3(0.0, -1.0, 0.0).rotated(Vector3.RIGHT, deg_to_rad(back))
	down = down.rotated(Vector3.BACK, deg_to_rad(side))
	down = down.rotated(Vector3.RIGHT, deg_to_rad(forward))
	return down * G


# ---- reading the lean ----------------------------------------------------------------

func test_level_is_however_the_phone_is_held_when_it_starts() -> void:
	for back: float in [0.0, 40.0, 80.0]:
		var tilt: TiltInput = TiltInput.new()
		assert_eq(tilt.direction(_held(back)), Vector2.ZERO, "%d°: the first reading is level" % int(back))
		assert_eq(tilt.direction(_held(back)), Vector2.ZERO, "held still: stand still")


func test_a_small_wobble_is_under_the_dead_zone() -> void:
	var tilt: TiltInput = TiltInput.new()
	tilt.direction(_held(45.0))
	assert_eq(tilt.direction(_held(45.0, TiltInput.DEAD_ZONE_DEGREES - 2.0)), Vector2.ZERO)
	assert_ne(tilt.direction(_held(45.0, TiltInput.DEAD_ZONE_DEGREES + 4.0)), Vector2.ZERO)


func test_lowering_an_edge_runs_toward_it_on_the_screen() -> void:
	var tilt: TiltInput = TiltInput.new()
	tilt.direction(_held(45.0))
	var right: Vector2 = tilt.direction(_held(45.0, 20.0))
	assert_gt(right.x, 0.9, "the right edge down: run right")
	var left: Vector2 = tilt.direction(_held(45.0, -20.0))
	assert_lt(left.x, -0.9, "the left edge down: run left")
	var up: Vector2 = tilt.direction(_held(45.0, 0.0, 20.0))
	assert_lt(up.y, -0.9, "the top edge down: run up the screen")


func test_it_is_only_ever_a_direction_never_a_speed() -> void:
	var tilt: TiltInput = TiltInput.new()
	tilt.direction(_held(30.0))
	for side: float in [12.0, 25.0, 60.0]:
		assert_almost_eq(tilt.direction(_held(30.0, side)).length(), 1.0, 0.001, "%d°: the same unit direction however far" % int(side))


func test_no_sensor_no_tilt() -> void:
	TiltInput.test_gravity = Vector3.ZERO
	assert_false(TiltInput.available())
	assert_eq(TiltInput.new().direction(Vector3.ZERO), Vector2.ZERO)
	TiltInput.test_gravity = _held(30.0)
	assert_true(TiltInput.available())


func test_the_project_turns_the_gravity_sensor_on() -> void:
	assert_true(ProjectSettings.get_setting("input_devices/sensors/enable_gravity"), "Input.get_gravity() is zero on Android without it")


# ---- the setting ------------------------------------------------------------------------

func test_tilt_to_run_is_off_until_turned_on_and_its_switch_shows_only_with_a_sensor() -> void:
	assert_false(ConfigFile.new().get_value("input", "tilt_to_run", false), "off by default")
	TiltInput.test_gravity = Vector3.ZERO
	var without: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(without)
	assert_null(without.tilt_toggle, "no sensor, no switch")
	TiltInput.test_gravity = _held(30.0)
	var with: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(with)
	assert_not_null(with.tilt_toggle)
	with.tilt_toggle.toggled.emit(true)
	assert_true(Settings.tilt_to_run)


# ---- in a match -------------------------------------------------------------------------

func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	practice.advance(DT)
	return practice


func _kid(practice: PracticeMatch) -> PlayerState:
	return practice.sim.players[practice.local_id]


func _screen(practice: PracticeMatch, street: Vector2) -> Vector2:
	return practice._camera.screen_position(Vector3(street.x, 0.0, street.y))


func _lean(practice: PracticeMatch, reading: Vector3, ticks: int) -> void:
	TiltInput.test_gravity = reading
	for i: int in ticks:
		practice.advance(DT)


func test_leaning_runs_the_kid_that_way_at_its_one_speed_and_level_stops_it() -> void:
	Settings.set_tilt_to_run(true)
	var practice: PracticeMatch = _practice()
	_lean(practice, _held(40.0), 2)
	var start: Vector2 = _kid(practice).position
	_lean(practice, _held(40.0, 25.0), 1)
	assert_true(practice.tilt_steering)
	var steps: Array[float] = []
	var last: Vector2 = _kid(practice).position
	for i: int in 15:
		practice.advance(DT)
		steps.append(_kid(practice).position.distance_to(last))
		last = _kid(practice).position
	assert_gt(_screen(practice, last).x, _screen(practice, start).x + 10.0, "the right edge down: the kid runs right on the screen")
	assert_lte(steps.max(), RULES.move_speed * DT + 0.001, "never faster than the one run speed")
	assert_almost_eq(steps.max(), RULES.move_speed * DT, 0.01, "and at it, like a finger")
	_lean(practice, _held(40.0), 3)
	assert_false(practice.tilt_steering)
	var stopped: Vector2 = _kid(practice).position
	_lean(practice, _held(40.0), 10)
	assert_almost_eq(_kid(practice).position.distance_to(stopped), 0.0, 0.2, "level again: it stops where it is")


func test_off_by_default_the_phone_tilting_does_nothing() -> void:
	Settings.set_tilt_to_run(false)
	var practice: PracticeMatch = _practice()
	_lean(practice, _held(40.0), 2)
	var start: Vector2 = _kid(practice).position
	_lean(practice, _held(40.0, 30.0), 15)
	assert_false(practice.tilt_steering)
	assert_eq(_kid(practice).position, start)


func test_a_finger_on_the_street_wins_over_the_tilt() -> void:
	Settings.set_tilt_to_run(true)
	var practice: PracticeMatch = _practice()
	_lean(practice, _held(40.0), 2)
	var here: Vector2 = _screen(practice, _kid(practice).position)
	practice.move_pressed(here + Vector2(0.0, -150.0))
	var target: Vector2 = practice.input.move_point
	_lean(practice, _held(40.0, 30.0), 3)
	assert_false(practice.tilt_steering, "a held finger is not overridden")
	assert_eq(practice.input.move_point, target)


func test_a_tagged_kid_is_not_steered_and_level_resets_when_the_controls_come_back() -> void:
	Settings.set_tilt_to_run(true)
	var practice: PracticeMatch = _practice()
	_lean(practice, _held(40.0), 2)
	_kid(practice).tagged = true
	_lean(practice, _held(40.0, 30.0), 2)
	assert_false(practice.tilt_steering)
	_kid(practice).tagged = false
	practice._set_controls_active(true)
	assert_eq(practice.tilt.level, Vector3.ZERO, "however the phone is held then is level")


# ---- Home's parallax --------------------------------------------------------------------

func test_home_layers_shift_apart_when_the_phone_leans_and_rest_otherwise() -> void:
	var host: Control = autofree(Control.new()) as Control
	host.size = Vector2(720.0, 1280.0)
	add_child(host)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	host.add_child(screen)
	await wait_frames(2)
	var parallax: HomeParallax = screen.parallax
	parallax.set_process(false)
	parallax.step(_held(40.0), DT)
	for i: int in 20:
		parallax.step(_held(40.0, 15.0), DT)
	assert_gt(parallax.lean.x, 0.5, "leaning right")
	assert_gt(screen.carousel.tilt.x, 0.0, "the models (near) move with it")
	assert_lt(screen.backdrop.picture.offset_left, -UiBackdrop.TILT_ROOM, "the street picture (far) the other way")
	assert_lte(absf(screen.carousel.tilt.x), HomeParallax.MODEL_SHIFT, "a few pixels at most")
	parallax.active = false
	for i: int in 40:
		parallax.step(_held(40.0, 15.0), DT)
	assert_eq(parallax.lean, Vector2.ZERO, "under a page or a sheet it rests")
	parallax.active = true
	Settings.reduced_motion = true
	for i: int in 40:
		parallax.step(_held(40.0, 30.0), DT)
	assert_eq(parallax.lean, Vector2.ZERO, "and with Reduce motion")


func test_held_still_the_parallax_drifts_back_to_rest() -> void:
	var parallax: HomeParallax = autofree(HomeParallax.new(null, null)) as HomeParallax
	parallax.step(_held(40.0), DT)
	for i: int in 10:
		parallax.step(_held(40.0, 15.0), DT)
	var leaning: float = parallax.lean.x
	for i: int in 240:
		parallax.step(_held(40.0, 15.0), DT)
	assert_lt(parallax.lean.x, leaning * 0.3, "level follows the phone, so a still phone comes back to rest")
