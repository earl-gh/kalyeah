extends GutTest
## A kid turns to a new facing at a fixed rate and never pops round in one frame (owner: models blinked on a sharp turn or an exact
## about-turn).

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const FRAME: float = 1.0 / 60.0


func _kid() -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(RULES.characters[0])
	add_child(kid)
	return kid


func _forward(kid: KidModel) -> Vector2:
	var look: Vector3 = -kid.global_transform.basis.z
	return Vector2(look.x, look.z)


func test_a_new_kid_just_stands_the_way_it_faces() -> void:
	var kid: KidModel = _kid()
	kid.face(Vector2(-1.0, 0.0))
	assert_true(_forward(kid).is_equal_approx(Vector2(-1.0, 0.0)), "no spin on the first frame")


func test_an_about_turn_never_jumps_more_than_the_turn_rate_in_a_frame() -> void:
	var kid: KidModel = _kid()
	kid.face(Vector2(1.0, 0.0))
	kid.face(Vector2(-1.0, 0.0))
	var frames: int = 0
	var previous: float = kid.rotation.y
	while not _forward(kid).is_equal_approx(Vector2(-1.0, 0.0)) and frames < 60:
		kid.animate(FRAME, 0.0, PlayerState.new())
		var step: float = absf(angle_difference(previous, kid.rotation.y))
		assert_lte(step, KidModel.TURN_RATE * FRAME + 0.0001, "frame %d turned %.2f rad" % [frames, step])
		previous = kid.rotation.y
		frames += 1
	assert_lt(frames, 60, "it does arrive")
	assert_gt(frames, 3, "and takes a few frames, not one")
	assert_lt(frames * FRAME, 0.25, "but well under a quarter of a second")


func test_an_exact_opposite_always_turns_the_same_way() -> void:
	var seen: Dictionary[int, bool] = {}
	for start: Vector2 in [Vector2(1.0, 0.0), Vector2(0.0, 1.0), Vector2(-1.0, 0.0), Vector2(0.0, -1.0), Vector2(0.7071, 0.7071)]:
		var kid: KidModel = _kid()
		kid.face(start)
		kid.face(-start)
		var before: float = kid.rotation.y
		kid.animate(FRAME, 0.0, PlayerState.new())
		seen[signi(int(signf(angle_difference(before, kid.rotation.y))))] = true
	assert_eq(seen.size(), 1, "one direction for every about-turn: no hesitating or choosing differently from frame to frame")


func test_a_facing_that_lasts_one_tick_is_only_a_small_wiggle() -> void:
	var kid: KidModel = _kid()
	kid.face(Vector2(1.0, 0.0))
	var home: float = kid.rotation.y
	kid.face(Vector2(-1.0, 0.0))
	kid.animate(FRAME, 0.0, PlayerState.new())
	kid.animate(FRAME, 0.0, PlayerState.new())  # two frames, one 30 Hz tick
	var wiggle: float = absf(angle_difference(home, kid.rotation.y))
	assert_lt(wiggle, PI / 2.0, "two frames of the opposite way turn it less than a quarter turn")
	kid.face(Vector2(1.0, 0.0))
	for i: int in 30:
		kid.animate(FRAME, 0.0, PlayerState.new())
	assert_almost_eq(angle_difference(home, kid.rotation.y), 0.0, 0.001, "and it is back as it was")


func test_a_short_dip_in_the_measured_speed_does_not_flash_the_kid_into_idle() -> void:
	var kid: KidModel = _kid()
	var state: PlayerState = PlayerState.new()
	for i: int in 20:
		kid.animate(FRAME, 5.0, state)
	assert_eq(kid.pose, "run")
	for i: int in 6:  # 0.1 s of a short step: under the grace
		kid.animate(FRAME, 0.1, state)
		assert_eq(kid.pose, "run", "frame %d of the dip" % i)
	kid.animate(FRAME, 5.0, state)
	assert_eq(kid.pose, "run", "and it was never anything else")


func test_a_kid_that_really_stops_goes_idle_soon() -> void:
	var kid: KidModel = _kid()
	var state: PlayerState = PlayerState.new()
	for i: int in 20:
		kid.animate(FRAME, 5.0, state)
	var frames: int = 0
	while kid.pose == "run" and frames < 60:  # a kid that has stood still measures exactly 0
		kid.animate(FRAME, 0.0, state)
		frames += 1
	assert_eq(kid.pose, "idle")
	assert_lt(float(frames) * FRAME, 0.12, "no grace for a kid that has really stopped: only the blend")


func test_a_standing_kid_stays_idle_until_it_moves() -> void:
	var kid: KidModel = _kid()
	for i: int in 30:
		kid.animate(FRAME, 0.0, PlayerState.new())
		assert_eq(kid.pose, "idle")
