class_name KidActs
extends RefCounted
## The can acts of a kid in Tumbang Preso (owner: use the existing clips as the basis, exaggerate, squash and stretch, smear into the action,
## funny and dramatic): picking the can up with both hands, carrying it, setting it down, and the kick. Each is a table of keyframes over its
## progress (0 to 1) that `KidModel` plays on top of the kid's own idle and run clips: how the body leans, rises, squashes and stretches,
## and where the hands (and for the kick the right foot) go, in the kid's own space (x right, y up, -z forward, the kid 1.85 m tall).
## Pure functions of the progress, so tests can read every pose. View only.

## Seconds each act takes.
const PICKUP_TOTAL: float = 0.9
const PLACE_TOTAL: float = 0.7
const KICK_TOTAL: float = 1.0
## The kick: the foot meets the can this many seconds in (the can leaves then), and in the dramatic moment round it the clock runs at
## `KICK_SLOW` of its speed (a hit-stop in slow motion).
const KICK_CONTACT: float = 0.26
const KICK_SLOW_FROM: float = 0.235
const KICK_SLOW_TO: float = 0.34
const KICK_SLOW: float = 0.3
## Where the can is held, in the kid's space (its middle), and how far each hand is from the middle sideways (the can is about 0.6 wide).
const CARRY_CENTRE: Vector3 = Vector3(0.0, 0.95, -0.42)
const CARRY_HALF: float = 0.27
## The can's radius in the kid's space; the foot meets the near side of it.
const CAN_RADIUS: float = 0.3
## The carried can bobs this far (m) and squashes this much with every step.
const CARRY_BOB: float = 0.035
const CARRY_SQUASH: float = 0.03
const CARRY_LEAN: float = 0.1


## The value of `keys` (a list of [progress, value] in order, floats or Vector3s) at `p`: eased between neighbours, held beyond the ends.
static func at(keys: Array, p: float) -> Variant:
	if p <= (keys[0] as Array)[0]:
		return (keys[0] as Array)[1]
	for i: int in range(1, keys.size()):
		var after: Array = keys[i]
		if p <= (after[0] as float):
			var before: Array = keys[i - 1]
			var span: float = (after[0] as float) - (before[0] as float)
			var t: float = 0.0 if span <= 0.0 else (p - (before[0] as float)) / span
			t = t * t * (3.0 - 2.0 * t)
			return lerp(before[1], after[1], t)
	return (keys[keys.size() - 1] as Array)[1]


static func _hands(left: Array, right: Array, weight: Array, p: float) -> Dictionary:
	return {"hand_l": at(left, p) as Vector3, "hand_r": at(right, p) as Vector3, "arm_w": at(weight, p) as float}


## The carry at `phase` (steps, one per half turn): the can held in both hands in front of the chest, the kid leaning back a little under its
## weight, the can bobbing and squashing on every step (`moving` 0 to 1).
static func carry_step(phase: float, moving: float) -> Dictionary:
	var step: float = sin(phase * TAU * 2.0) * moving
	var centre: Vector3 = CARRY_CENTRE + Vector3(0.0, absf(step) * CARRY_BOB, 0.0)
	return {"lean": CARRY_LEAN, "lift": 0.0, "squash": 1.0 - absf(step) * CARRY_SQUASH, "stretch": 1.0, "lunge": 0.0, "roll": step * 0.04,
		"hand_l": centre + Vector3(-CARRY_HALF, 0.0, 0.0), "hand_r": centre + Vector3(CARRY_HALF, 0.0, 0.0), "arm_w": 1.0,
		"hold": 1.0, "upright": 1.0, "centre": centre, "foot_w": 0.0}


## Picking the can up (it lies at `can` in the kid's space): a deep crouch and a lean forward with both hands reaching round it, a grunt
## of strain, then a hoist that stretches the kid up on its toes with the can in its hands, buckling under the weight, and a wobble into the carry.
static func pickup_step(p: float, can: Vector3) -> Dictionary:
	var low: float = maxf(can.y, 0.12) + 0.08
	var left: Array = [[0.0, CARRY_CENTRE + Vector3(-CARRY_HALF, 0.0, 0.0)], [0.2, Vector3(can.x - 0.28, low, can.z)], [0.4, Vector3(can.x - 0.28, low, can.z)],
		[0.6, CARRY_CENTRE + Vector3(-CARRY_HALF, 0.05, 0.0)], [0.72, CARRY_CENTRE + Vector3(-CARRY_HALF - 0.04, -0.06, 0.0)],
		[0.86, CARRY_CENTRE + Vector3(-CARRY_HALF, 0.02, 0.0)], [1.0, CARRY_CENTRE + Vector3(-CARRY_HALF, 0.0, 0.0)]]
	var right: Array = [[0.0, CARRY_CENTRE + Vector3(CARRY_HALF, 0.0, 0.0)], [0.2, Vector3(can.x + 0.28, low, can.z)], [0.4, Vector3(can.x + 0.28, low, can.z)],
		[0.6, CARRY_CENTRE + Vector3(CARRY_HALF, 0.05, 0.0)], [0.72, CARRY_CENTRE + Vector3(CARRY_HALF + 0.04, -0.06, 0.0)],
		[0.86, CARRY_CENTRE + Vector3(CARRY_HALF, 0.02, 0.0)], [1.0, CARRY_CENTRE + Vector3(CARRY_HALF, 0.0, 0.0)]]
	var step: Dictionary = _hands(left, right, [[0.0, 0.0], [0.12, 1.0], [1.0, 1.0]], p)
	step["lean"] = at([[0.0, 0.0], [0.2, -0.4], [0.36, -0.46], [0.46, -0.2], [0.6, 0.1], [0.72, 0.2], [0.86, 0.12], [1.0, CARRY_LEAN]], p)
	step["squash"] = at([[0.0, 1.0], [0.2, 0.76], [0.34, 0.7], [0.44, 0.8], [0.58, 1.22], [0.66, 1.14], [0.74, 0.84], [0.86, 1.05], [1.0, 1.0]], p)
	step["stretch"] = 1.0
	step["lift"] = at([[0.0, 0.0], [0.5, 0.0], [0.58, 0.1], [0.68, 0.0], [1.0, 0.0]], p)
	step["lunge"] = at([[0.0, 0.0], [0.2, -0.1], [0.4, -0.1], [0.6, 0.0], [1.0, 0.0]], p)
	step["roll"] = at([[0.0, 0.0], [0.7, 0.0], [0.76, 0.1], [0.84, -0.08], [0.93, 0.03], [1.0, 0.0]], p)
	step["hold"] = smoothstep(0.38, 0.6, p)  # how much the can is in the hands (0: lying where it was)
	step["upright"] = smoothstep(0.42, 0.62, p)
	step["foot_w"] = 0.0
	return step


## Setting the can down at `spot` (in the kid's space): it crouches and lowers the can to the street, lets go, and springs up tall with its
## arms thrown up (ta-da), then settles.
static func place_step(p: float, spot: Vector3) -> Dictionary:
	var low: float = spot.y + 0.1
	var left: Array = [[0.0, CARRY_CENTRE + Vector3(-CARRY_HALF, 0.0, 0.0)], [0.38, Vector3(spot.x - 0.28, low, spot.z)], [0.5, Vector3(spot.x - 0.3, low, spot.z)],
		[0.64, Vector3(-0.42, 1.65, -0.05)], [0.9, Vector3(-0.3, 0.95, 0.0)], [1.0, Vector3(-0.3, 0.9, 0.0)]]
	var right: Array = [[0.0, CARRY_CENTRE + Vector3(CARRY_HALF, 0.0, 0.0)], [0.38, Vector3(spot.x + 0.28, low, spot.z)], [0.5, Vector3(spot.x + 0.3, low, spot.z)],
		[0.64, Vector3(0.42, 1.65, -0.05)], [0.9, Vector3(0.3, 0.95, 0.0)], [1.0, Vector3(0.3, 0.9, 0.0)]]
	var step: Dictionary = _hands(left, right, [[0.0, 1.0], [0.88, 1.0], [1.0, 0.0]], p)
	step["lean"] = at([[0.0, CARRY_LEAN], [0.26, -0.42], [0.44, -0.46], [0.6, -0.08], [0.76, 0.12], [1.0, 0.0]], p)
	step["squash"] = at([[0.0, 1.0], [0.26, 0.78], [0.44, 0.74], [0.6, 1.2], [0.74, 0.9], [0.86, 1.04], [1.0, 1.0]], p)
	step["stretch"] = 1.0
	step["lift"] = at([[0.0, 0.0], [0.56, 0.0], [0.62, 0.12], [0.72, 0.0], [1.0, 0.0]], p)
	step["lunge"] = at([[0.0, 0.0], [0.4, -0.1], [0.6, 0.0], [1.0, 0.0]], p)
	step["roll"] = at([[0.0, 0.0], [0.74, 0.0], [0.8, -0.09], [0.9, 0.05], [1.0, 0.0]], p)
	step["hold"] = 1.0 - smoothstep(0.34, 0.48, p)
	step["upright"] = 1.0
	step["foot_w"] = 0.0
	return step


## The kick at `p`, the near side of the can `reach` m in front of the kid: wind up (lean back, crouch, the right foot swung back and up,
## the arms thrown out), strike (the foot snaps through the can, the whole kid smeared long and thin along the kick), freeze in the air
## on one leg with both arms up (slow motion round the hit), a hop and a landing squash, a wobble, standing again.
static func kick_step(p: float, reach: float) -> Dictionary:
	var foot: Array = [[0.0, Vector3(0.1, 0.1, 0.05)], [0.12, Vector3(0.11, 0.45, 0.3)], [0.22, Vector3(0.12, 0.66, 0.5)],
		[0.245, Vector3(0.07, 0.42, -reach * 0.3)], [0.26, Vector3(0.04, 0.3, -reach)], [0.3, Vector3(0.03, 0.62, -reach - 0.15)],
		[0.4, Vector3(0.03, 1.0, -reach - 0.22)], [0.62, Vector3(0.04, 0.94, -reach - 0.15)], [0.7, Vector3(0.08, 0.42, -0.1)],
		[0.78, Vector3(0.1, 0.12, 0.0)], [1.0, Vector3(0.1, 0.1, 0.0)]]
	var left: Array = [[0.0, Vector3(-0.3, 0.95, 0.0)], [0.08, Vector3(-0.35, 1.05, -0.35)], [0.22, Vector3(-0.46, 1.16, -0.45)],
		[0.26, Vector3(-0.5, 1.0, 0.12)], [0.4, Vector3(-0.42, 1.75, -0.1)], [0.62, Vector3(-0.42, 1.75, -0.1)],
		[0.78, Vector3(-0.4, 1.2, 0.0)], [0.9, Vector3(-0.3, 0.95, 0.0)], [1.0, Vector3(-0.3, 0.95, 0.0)]]
	var right: Array = [[0.0, Vector3(0.3, 0.95, 0.0)], [0.08, Vector3(0.45, 1.0, 0.2)], [0.22, Vector3(0.52, 1.0, 0.3)],
		[0.26, Vector3(0.36, 1.18, -0.38)], [0.4, Vector3(0.42, 1.75, -0.1)], [0.62, Vector3(0.42, 1.75, -0.1)],
		[0.78, Vector3(0.4, 1.2, 0.0)], [0.9, Vector3(0.3, 0.95, 0.0)], [1.0, Vector3(0.3, 0.95, 0.0)]]
	var step: Dictionary = _hands(left, right, [[0.0, 0.0], [0.06, 1.0], [0.9, 1.0], [1.0, 0.0]], p)
	step["foot"] = at(foot, p)
	step["foot_w"] = at([[0.0, 0.0], [0.06, 1.0], [0.78, 1.0], [0.92, 0.4], [1.0, 0.0]], p)
	step["lean"] = at([[0.0, 0.0], [0.12, 0.3], [0.22, 0.5], [0.26, -0.35], [0.34, -0.3], [0.45, -0.12], [0.62, -0.05], [0.75, 0.14], [0.88, -0.04], [1.0, 0.0]], p)
	step["lift"] = at([[0.0, 0.0], [0.22, 0.0], [0.26, 0.04], [0.36, 0.16], [0.55, 0.18], [0.66, 0.0], [1.0, 0.0]], p)
	step["squash"] = at([[0.0, 1.0], [0.12, 0.86], [0.22, 0.78], [0.25, 0.8], [0.27, 1.0], [0.36, 1.16], [0.58, 1.12], [0.68, 0.82], [0.78, 1.06], [0.88, 0.97], [1.0, 1.0]], p)
	step["stretch"] = at([[0.0, 1.0], [0.24, 1.0], [0.26, 1.5], [0.3, 1.25], [0.36, 1.0], [1.0, 1.0]], p)
	step["lunge"] = at([[0.0, 0.0], [0.22, 0.05], [0.26, -0.2], [0.36, -0.18], [0.6, -0.1], [0.7, 0.0], [1.0, 0.0]], p)
	step["roll"] = at([[0.0, 0.0], [0.12, -0.08], [0.22, -0.12], [0.26, 0.0], [0.7, 0.0], [0.76, 0.16], [0.84, -0.12], [0.92, 0.06], [1.0, 0.0]], p)
	step["hold"] = 0.0
	step["upright"] = 1.0
	return step


## The clock's speed at progress `p` of a kick: slow motion round the hit.
static func kick_rate(p: float) -> float:
	return KICK_SLOW if p > KICK_SLOW_FROM and p < KICK_SLOW_TO else 1.0
