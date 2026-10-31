extends GutTest
## The can acts of a kid in Tumbang Preso (owner: pick the can up with both hands, carry it, set it down, kick it; use the existing clips as the
## basis, exaggerate, squash and stretch, smear into the action, funny and dramatic): the keyframes, as facts about every pose.

const CAN: Vector3 = Vector3(0.0, 0.15, -0.45)
const REACH: float = 0.5


func test_a_keyframe_table_eases_between_neighbours_and_holds_beyond_its_ends() -> void:
	var keys: Array = [[0.2, 1.0], [0.6, 3.0]]
	assert_eq(KidActs.at(keys, 0.0), 1.0, "held before the first key")
	assert_eq(KidActs.at(keys, 0.9), 3.0, "held after the last")
	assert_almost_eq(KidActs.at(keys, 0.4) as float, 2.0, 0.001, "half way, eased")
	assert_eq(KidActs.at([[0.0, Vector3.ZERO], [1.0, Vector3.ONE]], 1.0), Vector3.ONE, "vectors too")


func test_the_pickup_crouches_deep_then_stretches_up_and_buckles_under_the_weight() -> void:
	var low: float = 9.0
	var high: float = 0.0
	for i: int in 101:
		var squash: float = KidActs.pickup_step(float(i) / 100.0, CAN)["squash"] as float
		low = minf(low, squash)
		high = maxf(high, squash)
	assert_lt(low, 0.75, "a deep, funny crouch (exaggerated)")
	assert_gt(high, 1.15, "and a stretch up on the toes when it lifts")
	assert_lt(KidActs.pickup_step(0.74, CAN)["squash"] as float, 0.9, "the knees buckle under the weight")
	var start: Dictionary = KidActs.pickup_step(0.0, CAN)
	var end: Dictionary = KidActs.pickup_step(1.0, CAN)
	assert_eq(start["squash"], 1.0, "it starts and ends upright and level")
	assert_eq(end["squash"], 1.0)
	assert_almost_eq(end["lean"] as float, KidActs.CARRY_LEAN, 0.001, "and ends in the carry's lean")


func test_the_pickup_takes_the_can_in_both_hands_and_carries_it_in_front_of_the_chest() -> void:
	var grab: Dictionary = KidActs.pickup_step(0.3, CAN)
	assert_lt((grab["hand_l"] as Vector3).x, CAN.x, "one hand on each side of the can")
	assert_gt((grab["hand_r"] as Vector3).x, CAN.x)
	assert_lt((grab["hand_l"] as Vector3).y, 0.5, "low, round the can on the street")
	assert_eq(grab["hold"], 0.0, "it has not left the street yet")
	var held: Dictionary = KidActs.pickup_step(0.7, CAN)
	assert_eq(held["hold"], 1.0, "in the hands")
	assert_eq(held["upright"], 1.0, "and standing up")
	var end: Dictionary = KidActs.pickup_step(1.0, CAN)
	var carry: Dictionary = KidActs.carry_step(0.0, 0.0)
	assert_eq(end["hand_l"], carry["hand_l"], "the pick-up ends in the carry pose")
	assert_eq(end["hand_r"], carry["hand_r"])
	assert_lt((carry["hand_l"] as Vector3).z, -0.3, "in front of the chest")
	assert_almost_eq(((carry["hand_r"] as Vector3).x - (carry["hand_l"] as Vector3).x), KidActs.CARRY_HALF * 2.0, 0.001, "a can's width apart")
	var rise: float = 0.0
	var last: float = 0.0
	for i: int in 101:
		var now: float = KidActs.pickup_step(float(i) / 100.0, CAN)["hold"] as float
		rise = maxf(rise, last - now)
		last = now
	assert_eq(rise, 0.0, "the can only ever goes up into the hands")


func test_a_carried_can_bobs_and_squashes_with_every_step_and_stays_still_when_standing() -> void:
	var standing: Dictionary = KidActs.carry_step(0.1, 0.0)
	assert_eq(standing["squash"], 1.0)
	assert_eq(standing["centre"], KidActs.CARRY_CENTRE)
	var bob: float = 0.0
	for i: int in 50:
		var step: Dictionary = KidActs.carry_step(float(i) / 50.0, 1.0)
		bob = maxf(bob, (step["centre"] as Vector3).y - KidActs.CARRY_CENTRE.y)
		assert_lt(step["squash"] as float, 1.0 + 0.0001)
	assert_gt(bob, 0.02, "it bobs when the kid runs")


func test_the_kick_winds_up_then_snaps_through_the_can_smeared_long_and_thin() -> void:
	var wind: Dictionary = KidActs.kick_step(0.2, REACH)
	assert_gt(wind["lean"] as float, 0.35, "leaning back")
	assert_lt(wind["squash"] as float, 0.85, "crouched")
	assert_gt((wind["foot"] as Vector3).z, 0.3, "the foot swung back")
	var hit: Dictionary = KidActs.kick_step(KidActs.KICK_CONTACT / KidActs.KICK_TOTAL, REACH)
	assert_almost_eq((hit["foot"] as Vector3).z, -REACH, 0.001, "the foot meets the can's near side")
	assert_lt((hit["foot"] as Vector3).y, 0.5, "low, at the can")
	assert_gt(hit["stretch"] as float, 1.4, "the whole kid smeared long along the kick (smear transition)")
	assert_lt(hit["lean"] as float, -0.2, "thrown forward")
	assert_lt(hit["lunge"] as float, -0.1, "lunging")


func test_after_the_hit_the_kid_freezes_in_the_air_on_one_leg_with_both_arms_up_then_wobbles_down() -> void:
	var air: Dictionary = KidActs.kick_step(0.5, REACH)
	assert_gt((air["foot"] as Vector3).y, 0.8, "the foot high")
	assert_gt((air["hand_l"] as Vector3).y, 1.6, "both arms up")
	assert_gt((air["hand_r"] as Vector3).y, 1.6)
	assert_gt(air["lift"] as float, 0.1, "off the street")
	assert_gt(air["squash"] as float, 1.08, "stretched tall")
	assert_lt(KidActs.kick_step(0.68, REACH)["squash"] as float, 0.85, "a landing squash")
	var wobble: float = 0.0
	for i: int in range(70, 95):
		wobble = maxf(wobble, absf(KidActs.kick_step(float(i) / 100.0, REACH)["roll"] as float))
	assert_gt(wobble, 0.08, "a wobble")
	var end: Dictionary = KidActs.kick_step(1.0, REACH)
	assert_eq(end["squash"], 1.0, "and it ends standing")
	assert_eq(end["lean"], 0.0)
	assert_eq(end["foot_w"], 0.0, "the leg is the clip's again")
	assert_eq(end["arm_w"], 0.0, "and the arms")


func test_the_kick_runs_in_slow_motion_round_the_hit_and_the_contact_comes_a_third_of_a_second_in() -> void:
	assert_eq(KidActs.kick_rate(0.1), 1.0)
	assert_lt(KidActs.kick_rate(0.26), 0.5, "slow round the hit")
	assert_eq(KidActs.kick_rate(0.6), 1.0)
	var p: float = 0.0
	var seconds: float = 0.0
	while p < KidActs.KICK_CONTACT / KidActs.KICK_TOTAL:
		seconds += 1.0 / 300.0
		p += 1.0 / 300.0 * KidActs.kick_rate(p) / KidActs.KICK_TOTAL
	assert_between(seconds, 0.25, 0.4, "the can leaves about a third of a second after the press")


func test_the_set_down_lowers_the_can_lets_go_and_springs_up_with_the_arms_thrown_up() -> void:
	var spot: Vector3 = Vector3(0.0, 0.0, -0.4)
	assert_eq(KidActs.place_step(0.0, spot)["hold"], 1.0, "the can is in the hands first")
	assert_eq(KidActs.place_step(0.6, spot)["hold"], 0.0, "then it stands on its own")
	assert_lt((KidActs.place_step(0.4, spot)["hand_l"] as Vector3).y, 0.35, "the hands go down to the street")
	assert_gt((KidActs.place_step(0.66, spot)["hand_l"] as Vector3).y, 1.4, "ta-da: both arms up")
	assert_lt(KidActs.place_step(0.44, spot)["squash"] as float, 0.8, "a crouch")
	assert_gt(KidActs.place_step(0.6, spot)["squash"] as float, 1.15, "a spring up")
	assert_eq(KidActs.place_step(1.0, spot)["arm_w"], 0.0, "and the arms are the clip's again")
