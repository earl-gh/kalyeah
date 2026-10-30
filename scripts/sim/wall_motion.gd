class_name WallMotion
extends RefCounted
## How a wall (papel shield, langit lupa) moves and grows with its age, as pure functions of the weapon's data: the paper wall
## glides `push_distance` m forward over `push_time` s (cubic ease-out) then holds; the block of earth keeps its width for
## `expand_delay` s (it is rising), then widens to `expand_factor` times over `expand_time` s (smooth in and out) and keeps that
## width until it is gone. A weapon with the defaults (0 / 1) never moves or widens, as before. The sim's blocking line
## (`WeaponSystem.Shield`) and the drawn wall both take their place and width from here, so they are never apart.


## Metres the wall has moved along its facing `age` seconds after it was cast (exactly `push_distance` once `push_time` is over).
static func push(def: WeaponDef, age: float) -> float:
	if def == null or def.push_distance == 0.0 or def.push_time <= 0.0:
		return 0.0
	var left: float = 1.0 - clampf(age / def.push_time, 0.0, 1.0)
	return def.push_distance * (1.0 - left * left * left)


## The wall's width as a multiple of the weapon's `width` (exactly `expand_factor` once the widening is over).
static func width_scale(def: WeaponDef, age: float) -> float:
	if def == null or def.expand_factor == 1.0 or def.expand_time <= 0.0:
		return 1.0
	var t: float = clampf((age - def.expand_delay) / def.expand_time, 0.0, 1.0)
	return lerpf(1.0, def.expand_factor, t * t * (3.0 - 2.0 * t))
