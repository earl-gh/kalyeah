class_name Patintero
extends StreetMode
## Patintero (docs/GDD.md "Patintero"): two horizontal lines across the street are the edges of the court, with one vertical line down the
## middle. The three guards guard the lines, one each (two take a horizontal, one the vertical): running keeps a guard on its line, a dive is
## the one thing that takes it off it. The runners run free over the whole court.
## - A runner has to cross the vertical line once before the far end counts (so the vertical guard has its say).
## - A guard that moves into the tag circle of a runner tags it: the runner stumbles, then waits on the sidewalk, in the middle of the left
##   or the right one at random (it can move there with the knob, never dives and cannot interrupt the game; its team can still chat)
##   until its team earns a point.
## - ONE free runner that gets past the last guard and back to the base earns the runners' team a point and resets the round: everyone is
##   free again; the runners stay runners.
## - The guards earn a point only when all the runners are tagged (one or two are not enough), and the round resets with the roles switched.
## - A round in which no runner passes the first guard in `stall_seconds` is a point for the guards and the roles switch after the reset;
##   the clock stops for good once a runner is past the first guard.
## Points belong to the team, not to its role. Runners and guards can Dive (a quick burst, then lying and getting up; a guard runs back to
## its line), with a cooldown that starts when the dive is over.

## `id` got past the last guard and now has to come back to the base.
signal runner_crossed(id: int)
## `id` dived along `way` (a unit vector): the burst takes `time` s, then it lies flat and gets up for `recover` s more.
signal dived(id: int, way: Vector2, time: float, recover: float)
## A tagged runner reached the sidewalk on `side` (-1 left, 1 right).
signal jailed_runner(id: int, side: int)

## A guard's line: across the street (it moves along X) or down the middle (it moves along Z, between the two horizontal lines).
enum Axis { HORIZONTAL, VERTICAL }
## The parts of a dive: the burst, lying and getting up, and (a guard only) running back to the nearest point of its line.
enum DivePhase { NONE, BURST, RECOVER, RETURN }
## The stick is locked this long (s) on every tick of a dive, so it never steers it.
const DIVE_LOCK: float = 0.1
## Tagged runners on one sidewalk stand this far apart (m).
const WAIT_SPACING: float = 3.0

## The runners who got past the last guard on this run (they score when they are back at the base).
var crossed: Dictionary[int, bool] = {}
## The runners that have crossed the vertical line on this run (between the two horizontal lines), and the side (-1, 1) of it each was last on.
var middle_crossed: Dictionary[int, bool] = {}
var middle_side: Dictionary[int, int] = {}
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
## id -> seconds left of the stumble of a runner that was just tagged; id -> the side (-1 or 1) of the sidewalk a tagged runner waits on.
var caught: Dictionary[int, float] = {}
var jailed: Dictionary[int, int] = {}
## Seconds left for a runner to pass the first guard this round, and whether one has.
var stall_left: float = 0.0
var first_guard_passed: bool = false

var _dive_down: Dictionary[int, bool] = {}
var _swap_after_reset: bool = false


func _init(p_rules: StreetModeRules) -> void:
	super(p_rules)
	stall_left = rules.stall_seconds


# ---- the court -----------------------------------------------------------------------------

## The z of horizontal line `index` (0: the one nearest the base, 1: the far one).
func horizontal_z(index: int) -> float:
	return rules.line_span if index == 0 else -rules.line_span


## The line the `slot`-th guard keeps: two horizontals first, the vertical for the third.
func line_for(slot: int) -> Vector2:
	if slot <= 1:
		return Vector2(float(Axis.HORIZONTAL), horizontal_z(slot))
	return Vector2(float(Axis.VERTICAL), 0.0)


func is_jailed(id: int) -> bool:
	return jailed.has(id)


## A tagged runner: stumbling, or waiting on a sidewalk.
func is_tagged_out(id: int) -> bool:
	return jailed.has(id) or caught.has(id)


func is_taggable(state: PlayerState) -> bool:
	return super.is_taggable(state) and not is_tagged_out(state.id)


func catch_seconds() -> float:
	return rules.catch_seconds


## How many runners are tagged (stumbling or on a sidewalk), and how many runners there are.
func runners_out() -> int:
	return jailed.size() + caught.size()


func runner_count(sim: MatchSim) -> int:
	var count: int = 0
	for state: PlayerState in sim.players.values():
		if is_attacker(state):
			count += 1
	return count


func move_point(state: PlayerState, point: Vector2) -> Vector2:
	if guard_line.has(state.id) and not is_attacker(state):
		return _on_line(state.id, point)
	return point


func constrain(sim: MatchSim, state: PlayerState) -> void:
	if is_resetting():
		return
	if jailed.has(state.id):
		_keep_on_sidewalk(sim, state)
	elif guard_line.has(state.id) and not is_attacker(state) and not is_busy(state.id):
		state.position = _on_line(state.id, state.position)  # running keeps it on its line; a dive and the way back do not


## `point` held to the line of guard `id`.
func _on_line(id: int, point: Vector2) -> Vector2:
	if guard_axis[id] == Axis.HORIZONTAL:
		return Vector2(point.x, guard_line[id])
	return Vector2(guard_line[id], clampf(point.y, -rules.line_span, rules.line_span))


## The x range (m, measured away from the middle) of the sidewalk a kid of `radius` can stand on: outside the street's edge, up to the
## sidewalk's far edge.
func sidewalk_range(sim: MatchSim, radius: float) -> Vector2:
	var edge: float = sim.layout.lane_width / 2.0
	return Vector2(edge + radius, edge + sim.layout.boundary_thickness + rules.sidewalk_width - radius)


## A tagged runner is held on its sidewalk, within the length of the court.
func _keep_on_sidewalk(sim: MatchSim, state: PlayerState) -> void:
	var side: int = jailed[state.id]
	var room: Vector2 = sidewalk_range(sim, state.radius)
	state.position.x = float(side) * clampf(float(side) * state.position.x, room.x, room.y)
	state.position.y = clampf(state.position.y, -rules.line_span, rules.line_span)


# ---- diving --------------------------------------------------------------------------------

## True when `state` could dive now: it is free (not tagged), not in a dive (the burst, the get-up, a guard's way back) and its cooldown
## is over.
func can_dive(state: PlayerState) -> bool:
	return not state.tagged and not is_resetting() and not is_tagged_out(state.id) and not is_busy(state.id) and dive_cool.get(state.id, 0.0) <= 0.0


## How ready the dive of `id` is, from 0 (in a dive, or just over) to 1 (ready): the cooldown only runs once the dive is over.
func dive_ready_share(id: int) -> float:
	if is_busy(id):
		return 0.0
	return 1.0 - clampf(dive_cool.get(id, 0.0) / maxf(rules.dive_cooldown, 0.001), 0.0, 1.0)


## In the burst of a dive.
func is_diving(id: int) -> bool:
	return dive_phase.get(id, DivePhase.NONE) == DivePhase.BURST


## In any part of a dive: the burst, lying and getting up, or (a guard only) running back to its line. Nothing steers it and it cannot dive.
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
	if jailed.has(id):
		_dive_down[id] = false  # a tagged runner never dives: it only walks on its sidewalk, by the stick (the street is shut to it)
		state.cast_lock_left = DIVE_LOCK
		if input != null and input.move != Vector2.ZERO:
			var stick: Vector2 = input.move.limit_length(1.0)
			state.facing = stick.normalized()
			state.position += stick * sim.rules.move_speed * dt
		return
	if caught.has(id):
		state.cast_lock_left = DIVE_LOCK  # it stumbles where it was caught
		caught[id] -= dt
		if caught[id] <= 0.0:
			_send_to_sidewalk(sim, state)
		return
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
	_clear_dive(state.id)
	dive_cool[state.id] = rules.dive_cooldown
	state.cast_lock_left = 0.0


func _clear_dive(id: int) -> void:
	dive_phase.erase(id)
	dive_left.erase(id)
	recover_left.erase(id)
	dive_way.erase(id)


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
		if not is_attacker(state) or is_tagged_out(state.id):
			continue
		if state.position.y < rules.line_span:
			first_guard_passed = true  # a runner is past the first guard: the stall clock is off for this round
		_watch_middle(state)
		if not crossed.has(state.id) and at_goal(state.position) and middle_crossed.has(state.id):
			crossed[state.id] = true
			runner_crossed.emit(state.id)
		elif crossed.has(state.id) and at_home(state.position):
			# ONE free runner past the last guard and back at the base is the runners' point; the round resets, and everyone is free
			crossed.erase(state.id)
			middle_crossed.erase(state.id)
			_add_team_point(sim, state.team, state.id)
			if sim.phase == MatchSim.Phase.PLAYING:
				begin_reset(sim)
			return
	check_tags(sim)


## Between the two horizontal lines a runner that changes sides of the vertical line has crossed it.
func _watch_middle(state: PlayerState) -> void:
	if absf(state.position.y) >= rules.line_span or is_zero_approx(state.position.x):
		return
	var side: int = 1 if state.position.x > 0.0 else -1
	if middle_side.has(state.id) and middle_side[state.id] != side:
		middle_crossed[state.id] = true
	middle_side[state.id] = side


## A guard moved into the tag circle of a runner: the guard plays its touch and the runner stumbles, then goes to a sidewalk. When that was
## the last runner, the guards' team earns a point and the round resets with the roles switched.
func _tag(sim: MatchSim, guard: PlayerState, runner: PlayerState) -> void:
	sim.weapon_cast.emit(guard.id, TAG_CAST)
	tagged.emit(guard.id, runner.id)
	caught[runner.id] = rules.catch_seconds
	crossed.erase(runner.id)
	middle_crossed.erase(runner.id)
	middle_side.erase(runner.id)
	_clear_dive(runner.id)
	runner.cast_lock_left = DIVE_LOCK
	sim.end_move(runner)
	if runners_out() >= runner_count(sim):
		_guards_take_the_round(sim, guard.team, guard.id, MatchSim.CaptureKind.TOUCHED)


## The guards' team earns a point (every runner tagged, or the runners too slow), the round resets and the roles switch.
func _guards_take_the_round(sim: MatchSim, team: int, id: int, how: int) -> void:
	_add_team_point(sim, team, id, how)
	if sim.phase == MatchSim.Phase.PLAYING:
		_swap_after_reset = true
		begin_reset(sim)


## The stall rule: no runner past the first guard `stall_seconds` into the round is a point for the guards.
func _step_turn(sim: MatchSim, dt: float) -> void:
	if first_guard_passed:
		return
	stall_left -= dt
	if stall_left <= 0.0:
		var guard: int = -1
		for state: PlayerState in sim.players.values():
			if not is_attacker(state):
				guard = state.id
				break
		_guards_take_the_round(sim, 1 - attack_team, guard, MatchSim.CaptureKind.TOO_SLOW)


## The tagged runner's stumble is over: it goes to the middle of the left or the right sidewalk (at random) and waits there.
func _send_to_sidewalk(sim: MatchSim, state: PlayerState) -> void:
	var id: int = state.id
	caught.erase(id)
	var side: int = 1 if sim.rng.randf() < 0.5 else -1
	var waiting: int = 0
	for other: int in jailed:
		if jailed[other] == side:
			waiting += 1
	var room: Vector2 = sidewalk_range(sim, state.radius)
	# the first stands at the middle of the sidewalk, the next ones a little to either side of it
	var step: int = (waiting + 1) / 2
	var along: float = float(step) * WAIT_SPACING * (1.0 if waiting % 2 == 1 else -1.0)
	state.position = Vector2(float(side) * (room.x + room.y) / 2.0, clampf(along, -rules.line_span, rules.line_span))
	state.facing = Vector2(-float(side), 0.0)
	state.cast_lock_left = 0.0
	jailed[id] = side
	sim.end_move(state)
	jailed_runner.emit(id, side)


## After a reset the roles may switch (every runner tagged, or the stall rule); everyone goes back to their places and the sidewalks are empty.
func _new_round(sim: MatchSim) -> void:
	var switched: bool = _swap_after_reset
	if switched:
		_swap_after_reset = false
		attack_team = 1 - attack_team
	super(sim)
	if switched:
		roles_swapped.emit(attack_team)


func _reset_game(_sim: MatchSim) -> void:
	crossed.clear()
	middle_crossed.clear()
	middle_side.clear()
	guard_axis.clear()
	guard_line.clear()
	dive_phase.clear()
	dive_left.clear()
	recover_left.clear()
	dive_way.clear()
	dive_cool.clear()
	caught.clear()
	jailed.clear()
	first_guard_passed = false
	stall_left = rules.stall_seconds


## Each guard stands in the middle of its own line (where the vertical line crosses a horizontal one, at x = 0).
func spot_for(state: PlayerState, attacker: bool, slot: int, count: int) -> Vector2:
	if attacker:
		return super.spot_for(state, attacker, slot, count)
	var line: Vector2 = line_for(slot)
	guard_axis[state.id] = int(line.x)
	guard_line[state.id] = line.y if int(line.x) == Axis.HORIZONTAL else 0.0
	return Vector2(0.0, line.y) if int(line.x) == Axis.HORIZONTAL else Vector2(0.0, 0.0)
