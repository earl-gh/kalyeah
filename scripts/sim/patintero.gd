class_name Patintero
extends StreetMode
## Patintero (docs/GDD.md "Patintero"): the court is two horizontal lines across the street divided by one vertical line down the middle.
## The guards guard them, one line each (two take a horizontal, one the vertical) and can only move along their own line. A runner who
## gets past the guards to the other end and back home earns a point for the team. A turn ends when every runner is tagged or after
## `turn_seconds`, and the teams swap roles.

## `id` reached the other end and now has to come back.
signal runner_crossed(id: int)

## A guard's line: across the street (it moves along X) or down the middle (it moves along Z, between the two horizontal lines).
enum Axis { HORIZONTAL, VERTICAL }

## The runners who reached the other end on this run.
var crossed: Dictionary[int, bool] = {}
## Guard id -> its line's axis, and the line's fixed coordinate (z of a horizontal line, x of the vertical one).
var guard_axis: Dictionary[int, int] = {}
var guard_line: Dictionary[int, float] = {}


## The z of horizontal line `index` (0: the one nearest the start, 1: the far one).
func horizontal_z(index: int) -> float:
	return rules.line_span if index == 0 else -rules.line_span


## The line the `slot`-th guard of `count` keeps: two horizontals first, the vertical for the third.
func line_for(slot: int) -> Vector2:
	if slot <= 1:
		return Vector2(float(Axis.HORIZONTAL), horizontal_z(slot))
	return Vector2(float(Axis.VERTICAL), 0.0)


func move_point(state: PlayerState, point: Vector2) -> Vector2:
	if guard_line.has(state.id) and not is_attacker(state):
		return _on_line(state.id, point)
	return point


func constrain(_sim: MatchSim, state: PlayerState) -> void:
	if guard_line.has(state.id) and not is_attacker(state):
		state.position = _on_line(state.id, state.position)


## `point` held to the line of guard `id`.
func _on_line(id: int, point: Vector2) -> Vector2:
	if guard_axis[id] == Axis.HORIZONTAL:
		return Vector2(point.x, guard_line[id])
	return Vector2(guard_line[id], clampf(point.y, -rules.line_span, rules.line_span))


func _step_game(sim: MatchSim, _dt: float) -> void:
	for state: PlayerState in sim.players.values():
		if not is_attacker(state) or state.tagged:
			continue
		if not crossed.has(state.id) and at_goal(state.position):
			crossed[state.id] = true
			runner_crossed.emit(state.id)
		elif crossed.has(state.id) and at_home(state.position):
			crossed.erase(state.id)
			award(sim, state)
			if sim.phase != MatchSim.Phase.PLAYING:
				return
	check_tags(sim)


## The turn ends when every runner is tagged or the time runs out: the teams swap roles.
func _step_turn(sim: MatchSim, dt: float) -> void:
	turn_left -= dt
	if turn_left <= 0.0 or _all_attackers_tagged(sim):
		swap_roles(sim)


## The teams swap roles: everyone back from the sidewalk to their new places.
func swap_roles(sim: MatchSim) -> void:
	attack_team = 1 - attack_team
	turn_left = rules.turn_seconds
	grace_left = rules.swap_grace
	sim.weapons.reset()
	place_teams(sim)
	roles_swapped.emit(attack_team)


func _all_attackers_tagged(sim: MatchSim) -> bool:
	var any: bool = false
	for state: PlayerState in sim.players.values():
		if is_attacker(state):
			any = true
			if not state.tagged:
				return false
	return any


func _on_tagged(runner: PlayerState) -> void:
	crossed.erase(runner.id)


func _reset_game(_sim: MatchSim) -> void:
	crossed.clear()
	guard_axis.clear()
	guard_line.clear()


## Each guard stands in the middle of its own line (where the vertical line crosses a horizontal one, at x = 0).
func spot_for(state: PlayerState, attacker: bool, slot: int, count: int) -> Vector2:
	if attacker:
		return super.spot_for(state, attacker, slot, count)
	var line: Vector2 = line_for(slot)
	guard_axis[state.id] = int(line.x)
	guard_line[state.id] = line.y if int(line.x) == Axis.HORIZONTAL else 0.0
	return Vector2(0.0, line.y) if int(line.x) == Axis.HORIZONTAL else Vector2(0.0, 0.0)
