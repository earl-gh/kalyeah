extends GutTest
## `KidModel` playing the can acts on top of its own clips: the body squashes and stretches, the arms and the kicking foot are bent by the pose
## modifier, the kick announces its strike, and the can follows the hands (docs/ART.md "Tumbang Preso").

const DT: float = 1.0 / 30.0
var _kid: KidModel
var _state: PlayerState
var _saved: bool


func before_each() -> void:
	_saved = Settings.reduced_motion
	Settings.reduced_motion = false
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var sim: MatchSim = MatchSim.new(rules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	sim.add_player(1, 0)
	_state = sim.players[1]
	_kid = KidModel.new()
	_kid.setup(rules.characters[0])
	_kid.scale = Vector3.ONE * 2.0
	add_child_autofree(_kid)


func after_each() -> void:
	Settings.reduced_motion = _saved


func _run(seconds: float, speed: float = 0.0) -> void:
	for i: int in ceili(seconds / DT):
		_kid.animate(DT, speed, _state)


func test_picking_the_can_up_crouches_hoists_and_leaves_the_kid_carrying_it() -> void:
	_kid.play_pickup(Vector3(0.0, 0.3, -0.7))
	assert_true(_kid.has_can())
	assert_true(_kid.is_acting())
	var lowest: float = 9.0
	var highest: float = 0.0
	var upright: float = 0.0
	for i: int in 30:
		_kid.animate(DT, 0.0, _state)
		lowest = minf(lowest, _kid._body.scale.y)
		highest = maxf(highest, _kid._body.scale.y)
		upright = maxf(upright, (_kid.can_hold().get("upright", 0.0)) as float)
	assert_lt(lowest, 0.8, "the body squashes down")
	assert_gt(highest, 1.1, "and stretches up")
	assert_almost_eq(upright, 1.0, 0.01, "the can ends upright in the hands")
	_run(0.3)
	assert_false(_kid.is_acting(), "the act is over ...")
	assert_true(_kid.has_can(), "... and the kid carries the can")
	assert_false(_kid.can_hold().is_empty(), "held in the hands")


func test_the_carried_can_sits_in_front_of_the_kids_chest() -> void:
	_kid.play_pickup(Vector3(0.0, 0.3, -0.7))
	_run(1.2)
	var held: Dictionary = _kid.can_hold()
	var local: Vector3 = _kid.to_local(held["position"] as Vector3)
	assert_lt(local.z, -0.3, "in front")
	assert_between(local.y, 0.7, 1.2, "at chest height")
	assert_almost_eq(local.x, 0.0, 0.05, "in the middle")


func test_the_arms_and_the_kicking_foot_are_bent_to_their_targets_only_during_an_act() -> void:
	_kid.play_kick(Vector3(0.0, 0.35, -1.0))
	_run(0.12)
	assert_true(_kid._pose.active, "the pose modifier is on while the kid kicks")
	assert_gt(_kid._pose.foot_weight, 0.5)
	assert_gt(_kid._pose.arm_weight, 0.5)
	_run(1.4)
	assert_false(_kid._pose.active, "and off when it is over")
	assert_eq(_kid._pose.foot_weight, 0.0)
	assert_false(_kid._manual, "the clips are on their own again")


func test_the_kick_announces_the_strike_once_with_the_foot_at_the_can() -> void:
	var strikes: Array = []
	_kid.kick_struck.connect(func(from: Vector3, foot: Vector3, way: Vector3) -> void: strikes.append([from, foot, way]))
	_kid.play_kick(Vector3(0.0, 0.35, -1.0))
	var seconds: float = 0.0
	while strikes.is_empty() and seconds < 1.0:
		_kid.animate(DT, 0.0, _state)
		seconds += DT
	assert_eq(strikes.size(), 1)
	assert_between(seconds, 0.2, 0.5, "about a third of a second after it starts")
	_run(1.5)
	assert_eq(strikes.size(), 1, "never twice")
	assert_false(_kid.is_acting())
	var way: Vector3 = strikes[0][2]
	assert_almost_eq(way.length(), 1.0, 0.001)
	assert_gt((strikes[0][1] as Vector3).distance_to(strikes[0][0] as Vector3), 0.2, "the foot swung")


func test_the_kicker_turns_to_the_can_and_keeps_looking_at_it() -> void:
	_kid.face(Vector2(0.0, -1.0))
	_run(0.3)
	_kid.play_kick(Vector3(1.0, 0.35, 0.0))  # the can is to the right (+x)
	_kid.face(Vector2(0.0, -1.0))  # the sim's facing must not turn it back
	_run(0.3)
	assert_almost_eq(_kid.rotation.y, atan2(-1.0, 0.0), 0.05, "facing +x")


func test_dropping_the_can_ends_the_carry_at_once() -> void:
	_kid.play_pickup(Vector3(0.0, 0.3, -0.7))
	_run(1.3)
	assert_true(_kid.has_can())
	_kid.drop_can()
	_run(DT)
	assert_false(_kid.has_can())
	assert_true(_kid.can_hold().is_empty())
	assert_eq(_kid._body.scale, Vector3.ONE, "and the body is as it was")


func test_setting_the_can_down_lets_go_of_it_and_the_kid_stands_empty_handed() -> void:
	_kid.play_pickup(Vector3(0.0, 0.3, -0.7))
	_run(1.3)
	_kid.play_place(_kid.to_global(Vector3(0.0, 0.35, -0.6)))
	assert_true(_kid.has_can())
	_run(0.4)
	assert_gt((_kid.can_hold().get("hold", 0.0)) as float, -1.0)
	_run(0.6)
	assert_false(_kid.has_can(), "it stands on its own and the kid is done")


func test_reduce_motion_keeps_the_poses_but_not_the_squash_stretch_and_slow_motion() -> void:
	Settings.reduced_motion = true
	_kid.play_kick(Vector3(0.0, 0.35, -1.0))
	var most: float = 0.0
	for i: int in 30:
		_kid.animate(DT, 0.0, _state)
		most = maxf(most, absf(_kid._body.scale.y - 1.0))
	assert_almost_eq(most, 0.0, 0.0001, "no squash or stretch")
	assert_true(_kid._pose.active, "but the arms and the foot still swing")
