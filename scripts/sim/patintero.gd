class_name Patintero
extends StreetMode
## Patintero (docs/GDD.md "Patintero"): the court is two horizontal lines across the street divided by one vertical line down the middle; the
## first and last horizontal line are the edges of the court (nothing else is drawn). The guards guard them, one line each (two take a
## horizontal, one the vertical); running keeps a guard on its line, a dive is the one thing that takes it off it. The runners run free. A
## runner who gets past the last guard and back to the base earns a point for the team (going back after only the first guard is no
## point, and a runner may go back past a guard it has passed, to escape a tag). A guard that moves into the tag circle of a runner tags
## it: the guards' team earns a point and the round resets by itself (everyone back in their places). Every `turn_seconds` the teams swap
## roles. Runners and guards can both Dive (a quick burst, then lying and getting up; a guard runs back to its line), with a cooldown.

## `id` got past the last guard and now has to come back to the base.
signal runner_crossed(id: int)
## `id` dived along `way` (a unit vector): the burst takes `time` s, then it lies flat and gets up for `recover` s more.
signal dived(id: int, way: Vector2, time: float, recover: float)

## The parts of a dive: the burst, lying and getting up, and (a guard only) running back to the nearest point of its line.
enum DivePhase { NONE, BURST, RECOVER, RETURN }
## The stick is locked this long (s) on every tick of a dive, so it never steers it.
const DIVE_LOCK: float = 0.1

## A guard's line: across the street (it moves along X) or down the middle (it moves along Z, between the two horizontal lines).
enum Axis { HORIZONTAL, VERTICAL }

## The runners who got past the last guard on this run (they score when they are back at the base).
var crossed: Dictionary[int, bool] = {}
## Guard id -> its line's axis, and the line's fixed coordinate (z of a horizontal line, x of the vertical one).
var guard_axis: Dictionary[int, int] = {}
var guard_line: Dictionary[int, float] = {}
## id -> the part of its dive it is in (absent: not in a dive), the seconds left of the burst and of the get-up, the way it dives, and the
## seconds until it can dive again (it runs only once the dive is over).
var dive_phase: Dictionary[int, int] = {}
var dive_left: Dictionary[int, float] = {}
var recover_left: Dictionary[int, float] = {}
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
	if guard_line.has(state.id) and not is_attacker(state) and not is_resetting() and not is_busy(state.id):
		state.position = _on_line(state.id, state.position)  # running keeps it on its line; a dive and the way back do not


## `point` held to the line of guard `id`.
func _on_line(id: int, point: Vector2) -> Vector2:
	if guard_axis[id] == Axis.HORIZONTAL:
		return Vector2(point.x, guard_line[id])
	return Vector2(guard_line[id], clampf(point.y, -rules.line_span, rules.line_span))


# ---- diving --------------------------------------------------------------------------------

## True when `state` could dive now: it is free, not in a dive (the burst, the get-up, a guard's way back) and its cooldown is over.
func can_dive(state: PlayerState) -> bool:
	return not state.tagged and not is_resetting() and not is_busy(state.id) and dive_cool.get(state.id, 0.0) <= 0.0


## How ready the dive of `id` is, from 0 (in a dive, or just over) to 1 (ready): the cooldown only runs once the dive is over.
func dive_ready_share(id: int) -> float:
	if is_busy(id):
		return 0.0
	return 1.0 - clampf(dive_cool.get(id, 0.0) / maxf(rules.dive_cooldown, 0.001), 0.0, 1.0)


## In the burst of a dive.
func is_diving(id: int) -> bool:
	return dive_phase.get(id, DivePhase.NONE) == DivePhase.BURST


## In any part of a dive: the burst, lying and getting up, or (a guard) running back to its line. Nothing steers it and it cannot dive.
func is_busy(id: int) -> bool:
	return dive_phase.has(id)


## A guard running back to the nearest point of its line.
func is_returning(id: int) -> bool:
	return dive_phase.get(id, DivePhase.NONE) == DivePhase.RETURN


## The way `state` dives with the stick at `stick` (the way it faces without one). Any way: a guard's dive takes it off its line.
func dive_way_for(state: PlayerState, stick: Vector2) -> Vector2:
	var way: Vector2 = stick if stick.length() > 0.1 else state.facing
	return way.normalized() if way.length() > 0.0 else Vector2(0.0, -1.0)


## The nearest point of guard `id`'s line to `point`.
func nearest_on_line(id: int, point: Vector2) -> Vector2:
	return _on_line(id, point)


func handle_input(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float) -> void:
	var id: int = state.id
	var pressed: bool = input != null and input.is_pressed(PlayerInput.BTN_CARD_2)
	if pressed and not _dive_down.get(id, false) and can_dive(state):
		_dive(sim, state, dive_way_for(state, input.move))
	_dive_down[id] = pressed
	if not is_busy(id):
		return
	state.cast_lock_left = DIVE_LOCK  # the stick does not steer any part of a dive
	match dive_phase[id]:
		DivePhase.BURST:
			sim._move_by(state, dive_way[id] * rules.dive_speed_share * sim.rules.move_speed * dt)
			dive_left[id] -= dt
			if dive_left[id] <= 0.0:
				dive_left.erase(id)
				dive_phase[id] = DivePhase.RECOVER
				recover_left[id] = rules.dive_recover
		DivePhase.RECOVER:
			recover_left[id] -= dt
			if recover_left[id] <= 0.0:
				recover_left.erase(id)
				if guard_line.has(id) and not is_attacker(state):
					dive_phase[id] = DivePhase.RETURN
				else:
					_finish_dive(state)
		DivePhase.RETURN:
			_run_back(sim, state, dt)


## A guard runs back to the nearest point of its line, and the dive is over when it is there.
func _run_back(sim: MatchSim, state: PlayerState, dt: float) -> void:
	var goal: Vector2 = _on_line(state.id, state.position)
	var reach: float = rules.dive_return_speed_share * sim.rules.move_speed * dt
	var gap: float = state.position.distance_to(goal)
	if gap <= reach:
		state.position = goal
		_finish_dive(state)
		return
	var way: Vector2 = (goal - state.position) / gap
	state.facing = way
	sim._move_by(state, way * reach)


## The dive is over (a runner stands, a guard is back on its line): it can steer again and the cooldown starts now.
func _finish_dive(state: PlayerState) -> void:
	dive_phase.erase(state.id)
	dive_way.erase(state.id)
	dive_cool[state.id] = rules.dive_cooldown
	state.cast_lock_left = 0.0


func _dive(sim: MatchSim, state: PlayerState, way: Vector2) -> void:
	dive_phase[state.id] = DivePhase.BURST
	dive_left[state.id] = rules.dive_time
	dive_way[state.id] = way
	state.facing = way
	state.cast_lock_left = DIVE_LOCK
	sim.end_move(state)
	dived.emit(state.id, way, rules.dive_time, rules.dive_recover)


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
	tagged.emit(guard.id, runner.id)
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
	dive_phase.clear()
	dive_left.clear()
	recover_left.clear()
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
