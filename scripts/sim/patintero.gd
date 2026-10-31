class_name Patintero
extends StreetMode
## Patintero (docs/GDD.md "Patintero"): the court is two horizontal lines across the street divided by one vertical line down the middle.
## The guards guard them, one line each (two take a horizontal, one the vertical) and can only move along their own line. A runner who
## gets past the guards to the other end and back home earns a point for the team. A guard that moves into the tag circle of a runner tags
## it: the guards' team earns a point and the round resets by itself (everyone back in their places). Every `turn_seconds` the teams swap
## roles. Runners and guards can both Dive (a quick burst, with a cooldown).

## `id` reached the other end and now has to come back.
signal runner_crossed(id: int)
## `guard` tagged `runner` (the round resets).
signal runner_tagged(guard: int, runner: int)
## `id` dived along `way` (a unit vector) for `time` s.
signal dived(id: int, way: Vector2, time: float)

## A guard's line: across the street (it moves along X) or down the middle (it moves along Z, between the two horizontal lines).
enum Axis { HORIZONTAL, VERTICAL }

## The runners who reached the other end on this run.
var crossed: Dictionary[int, bool] = {}
## Guard id -> its line's axis, and the line's fixed coordinate (z of a horizontal line, x of the vertical one).
var guard_axis: Dictionary[int, int] = {}
var guard_line: Dictionary[int, float] = {}
## id -> seconds left of the dive it is in (absent: not diving), the way it dives, and seconds until it can dive again.
var dive_left: Dictionary[int, float] = {}
var dive_way: Dictionary[int, Vector2] = {}
var dive_cool: Dictionary[int, float] = {}

var _dive_down: Dictionary[int, bool] = {}


## The z of horizontal line `index` (0: the one nearest the start, 1: the far one).
func horizontal_z(index: int) -> float:
	return rules.line_span if index == 0 else -rules.line_span


## The line the `slot`-th guard keeps: two horizontals first, the vertical for the third.
func line_for(slot: int) -> Vector2:
	if slot <= 1:
		return Vector2(float(Axis.HORIZONTAL), horizontal_z(slot))
	return Vector2(float(Axis.VERTICAL), 0.0)


func move_point(state: PlayerState, point: Vector2) -> Vector2:
	if guard_line.has(state.id) and not is_attacker(state):
		return _on_line(state.id, point)
	return point


func constrain(_sim: MatchSim, state: PlayerState) -> void:
	if guard_line.has(state.id) and not is_attacker(state) and not is_resetting():
		state.position = _on_line(state.id, state.position)


## `point` held to the line of guard `id`.
func _on_line(id: int, point: Vector2) -> Vector2:
	if guard_axis[id] == Axis.HORIZONTAL:
		return Vector2(point.x, guard_line[id])
	return Vector2(guard_line[id], clampf(point.y, -rules.line_span, rules.line_span))


# ---- diving --------------------------------------------------------------------------------

## True when `state` could dive now: it is free, not diving and its cooldown is over.
func can_dive(state: PlayerState) -> bool:
	return not state.tagged and not is_resetting() and dive_left.get(state.id, 0.0) <= 0.0 and dive_cool.get(state.id, 0.0) <= 0.0


## How ready the dive of `id` is, from 0 (just used) to 1 (ready).
func dive_ready_share(id: int) -> float:
	return 1.0 - clampf(dive_cool.get(id, 0.0) / maxf(rules.dive_cooldown, 0.001), 0.0, 1.0)


func is_diving(id: int) -> bool:
	return dive_left.get(id, 0.0) > 0.0


## The way `state` dives with the stick at `stick` (the way it faces without one): a guard only along its own line.
func dive_way_for(state: PlayerState, stick: Vector2) -> Vector2:
	var way: Vector2 = stick if stick.length() > 0.1 else state.facing
	if guard_line.has(state.id) and not is_attacker(state):
		if guard_axis[state.id] == Axis.HORIZONTAL:
			return Vector2(signf(way.x) if way.x != 0.0 else 1.0, 0.0)
		return Vector2(0.0, signf(way.y) if way.y != 0.0 else 1.0)
	return way.normalized() if way.length() > 0.0 else Vector2(0.0, -1.0)


func handle_input(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float) -> void:
	var id: int = state.id
	var pressed: bool = input != null and input.is_pressed(PlayerInput.BTN_CARD_2)
	if pressed and not _dive_down.get(id, false) and can_dive(state):
		_dive(sim, state, dive_way_for(state, input.move))
	_dive_down[id] = pressed
	if dive_left.get(id, 0.0) > 0.0:
		sim._move_by(state, dive_way[id] * rules.dive_speed_share * sim.rules.move_speed * dt)
		dive_left[id] -= dt
		if dive_left[id] <= 0.0:
			dive_left.erase(id)
			dive_way.erase(id)


func _dive(sim: MatchSim, state: PlayerState, way: Vector2) -> void:
	dive_left[state.id] = rules.dive_time
	dive_way[state.id] = way
	dive_cool[state.id] = rules.dive_cooldown
	state.facing = way
	state.cast_lock_left = rules.dive_time  # the stick does not steer a dive
	sim.end_move(state)
	dived.emit(state.id, way, rules.dive_time)


# ---- one tick ------------------------------------------------------------------------------

func _step_game(sim: MatchSim, dt: float) -> void:
	for id: int in dive_cool.keys():
		dive_cool[id] = maxf(0.0, dive_cool[id] - dt)
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


## A guard moved into the tag circle of a runner: the guard plays its touch, the guards' team earns a point and the round resets.
func _tag(sim: MatchSim, guard: PlayerState, runner: PlayerState) -> void:
	sim.weapon_cast.emit(guard.id, TAG_CAST)
	runner_tagged.emit(guard.id, runner.id)
	award(sim, guard, false, MatchSim.CaptureKind.TOUCHED)
	if sim.phase == MatchSim.Phase.PLAYING:
		begin_reset(sim)


## Every `turn_seconds` the teams swap roles.
func _step_turn(sim: MatchSim, dt: float) -> void:
	turn_left -= dt
	if turn_left <= 0.0:
		swap_roles(sim)


## The teams swap roles: everyone to their new places.
func swap_roles(sim: MatchSim) -> void:
	attack_team = 1 - attack_team
	turn_left = rules.turn_seconds
	grace_left = rules.swap_grace
	sim.weapons.reset()
	place_teams(sim)
	roles_swapped.emit(attack_team)


func _reset_game(_sim: MatchSim) -> void:
	crossed.clear()
	guard_axis.clear()
	guard_line.clear()
	dive_left.clear()
	dive_way.clear()
	dive_cool.clear()


## Each guard stands in the middle of its own line (where the vertical line crosses a horizontal one, at x = 0).
func spot_for(state: PlayerState, attacker: bool, slot: int, count: int) -> Vector2:
	if attacker:
		return super.spot_for(state, attacker, slot, count)
	var line: Vector2 = line_for(slot)
	guard_axis[state.id] = int(line.x)
	guard_line[state.id] = line.y if int(line.x) == Axis.HORIZONTAL else 0.0
	return Vector2(0.0, line.y) if int(line.x) == Axis.HORIZONTAL else Vector2(0.0, 0.0)
