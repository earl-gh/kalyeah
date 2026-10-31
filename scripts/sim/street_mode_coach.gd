class_name StreetModeCoach
extends RefCounted
## Plays the bots of a street game in Practice (Tumbang Preso, Patintero) by role. Bots walk with the stick and never cast cards (there
## are none). Pure data in, input out, with the match's seeded rng, so it stays deterministic.
## - Tumbang Preso thrower: waits a moment in the throwing area, then holds the Throw button aimed at the can until the line reaches it
##   and lets go; then fetches its slipper when the can is down or the taya is far from it (or its scared timer is nearly out), kicks the
##   can when it stands beside it, and runs for the other end, away from the taya.
## - Tumbang Preso taya: fetches the fallen can and carries it back; otherwise chases a thrower out in the fetch area, else waits by the can.
## - Patintero runner: runs for the other end and back, sidestepping the guard of the next horizontal line (keeping to the side it chose,
##   toward the open street) until it has room to pass, and keeping away from the middle where the vertical guard is.
## - Patintero guard: follows the nearest runner along its own line.

## A runner this close (m, along the street) to the next line sidesteps its guard instead of running on.
const LINE_CAUTION: float = 4.0
## ... while that guard is less than this far to the side (m).
const SIDESTEP_ROOM: float = 3.0
## A stick this close to its target counts as there.
const ARRIVED: float = 0.2
## The taya chases throwers this close to the can (m).
const CHASE_RANGE: float = 12.0
## A runner keeps this far from the middle (m) where the vertical guard stands.
const MIDDLE_ROOM: float = 2.5

var ids: Array[int] = []
## Seconds a Tumbang Preso thrower still waits before it aims (absent: not waiting yet).
var _wait: Dictionary[int, float] = {}
## A Tumbang Preso thrower that is aiming: id -> [the way it aims, the seconds it holds the button for].
var _aim: Dictionary[int, Array] = {}
## The side (-1 or 1) a Patintero runner sidesteps the guard ahead to, kept until it is past (absent: not sidestepping).
var _side: Dictionary[int, float] = {}


func add(id: int) -> void:
	ids.append(id)


## Sets the input of every free bot for this tick. Call before `sim.step`.
func drive(sim: MatchSim) -> void:
	if sim.phase != MatchSim.Phase.PLAYING or sim.mode == null:
		return
	for id: int in ids:
		var state: PlayerState = sim.players[id]
		if not state.tagged:
			sim.set_input(id, think(sim, state))


func think(sim: MatchSim, state: PlayerState) -> PlayerInput:
	if sim.mode is TumbangPreso:
		var game: TumbangPreso = sim.mode as TumbangPreso
		return _it(sim, game, state) if state.id == game.taya_id else _thrower(sim, game, state)
	if sim.mode is Patintero:
		var lines: Patintero = sim.mode as Patintero
		return _runner(sim, lines, state) if lines.is_attacker(state) else _line_guard(sim, lines, state)
	return _stick(Vector2.ZERO)


# ---- Tumbang Preso -------------------------------------------------------------------------

func _thrower(sim: MatchSim, game: TumbangPreso, state: PlayerState) -> PlayerInput:
	var id: int = state.id
	var taya: PlayerState = sim.players[game.taya_id]
	if game.flights.has(id):
		return _stick(Vector2.ZERO)
	if game.fetched.has(id):
		_aim.erase(id)
		return _stick(_away_from(state.position, taya.position, Vector2(state.position.x, game.rules.goal_z - 2.0), game.rules.bot_safe_distance))
	if game.has_slipper(id):
		if not game.at_home(state.position):
			return _stick(_toward(state.position, Vector2(state.position.x, game.rules.home_z + 1.0)))
		return _aim_and_throw(sim, game, state)
	_aim.erase(id)
	if game.slipper_busy.get(id, 0.0) > 0.0 or not game.slippers.has(id):
		return _stick(Vector2.ZERO)
	var slipper: Vector2 = game.slippers[id]
	var safe: bool = not game.can_up or taya.position.distance_to(slipper) > game.rules.bot_safe_distance
	var rush: bool = game.scared_left.get(id, INF) <= game.rules.bot_rush_seconds
	if game.can_kick(state) and game.can_up:
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_2, 0)
	if safe or rush:
		return _stick(_toward(state.position, slipper))
	return _stick(Vector2.ZERO)


## Waits a moment, then holds the Throw button aimed at the can until the line is long enough, then lets go (a tick without the button).
func _aim_and_throw(sim: MatchSim, game: TumbangPreso, state: PlayerState) -> PlayerInput:
	var id: int = state.id
	if not _aim.has(id):
		if not _wait.has(id):
			_wait[id] = sim.rng.randf_range(0.0, game.rules.bot_throw_wait)
		_wait[id] -= sim.rules.tick_dt()
		if _wait[id] > 0.0:
			return _stick(Vector2.ZERO)
		_wait.erase(id)
		var error: float = game.rules.bot_throw_error
		var target: Vector2 = game.can_position + Vector2(sim.rng.randf_range(-error, error), sim.rng.randf_range(-error, error))
		var distance: float = state.position.distance_to(target)
		var span: float = game.rules.throw_max_range - game.rules.throw_min_range
		var power: float = clampf((distance + game.rules.bot_throw_margin - game.rules.throw_min_range) / span, 0.0, 1.0) if span > 0.0 else 1.0
		_aim[id] = [(target - state.position).normalized(), power * game.rules.throw_charge_time]
	var plan: Array = _aim[id]
	if game.hold.get(id, 0.0) < (plan[1] as float):
		return PlayerInput.create(Vector2.ZERO, plan[0] as Vector2, PlayerInput.BTN_CARD_1, 0)
	_aim.erase(id)
	return _stick(Vector2.ZERO)  # let go: the slipper flies


func _it(sim: MatchSim, game: TumbangPreso, state: PlayerState) -> PlayerInput:
	var speed: float = game.rules.bot_guard_speed
	if not game.can_up:
		if game.can_busy_left > 0.0:
			return _stick(Vector2.ZERO)
		if game.can_carrier == state.id:
			return _stick(_toward(state.position, game.rules.can_spot))
		return _stick(_toward(state.position, game.can_position) * speed)
	var best: PlayerState = null
	for other: PlayerState in sim.players.values():
		if other.id != state.id and not game.at_home(other.position) and not game.at_goal(other.position) \
				and other.position.distance_to(game.can_position) <= CHASE_RANGE \
				and (best == null or other.position.distance_to(state.position) < best.position.distance_to(state.position)):
			best = other
	var target: Vector2 = best.position if best != null else game.rules.can_spot + Vector2(0.0, game.rules.guard_offset)
	return _stick(_toward(state.position, target) * speed)


# ---- Patintero -----------------------------------------------------------------------------

func _runner(sim: MatchSim, lines: Patintero, state: PlayerState) -> PlayerInput:
	var going_home: bool = lines.crossed.has(state.id)
	var target_z: float = lines.rules.home_z + 1.5 if going_home else lines.rules.goal_z - 1.5
	var way: float = signf(target_z - state.position.y)
	# the guard of the next horizontal line ahead
	var ahead: PlayerState = null
	var gap: float = INF
	for id: int in lines.guard_line:
		if lines.guard_axis[id] != Patintero.Axis.HORIZONTAL or not sim.players.has(id) or sim.players[id].tagged:
			continue
		var along: float = (lines.guard_line[id] - state.position.y) * way
		if along > 0.0 and along < gap:
			gap = along
			ahead = sim.players[id]
	if ahead == null or gap >= LINE_CAUTION:
		_side.erase(state.id)
		return _stick(_keep_off_the_middle(lines, state, Vector2(0.0, way)))
	var dx: float = state.position.x - ahead.position.x
	if not _side.has(state.id):
		# toward the open street: the side of the guard with more room
		_side[state.id] = -signf(ahead.position.x) if absf(ahead.position.x) > 1.0 else (signf(dx) if dx != 0.0 else 1.0)
	var side: float = _side[state.id]
	if absf(dx) < SIDESTEP_ROOM or signf(dx) != side:
		return _stick(Vector2(side, -way * 0.2).normalized())
	return _stick(Vector2(side, way).normalized())


## A runner between the horizontal lines leans away from the middle, where the vertical guard stands.
func _keep_off_the_middle(lines: Patintero, state: PlayerState, way: Vector2) -> Vector2:
	if absf(state.position.y) > lines.rules.line_span + 1.0 or absf(state.position.x) >= MIDDLE_ROOM:
		return way
	var side: float = signf(state.position.x) if state.position.x != 0.0 else 1.0
	return Vector2(side * 0.7, way.y).normalized()


func _line_guard(sim: MatchSim, lines: Patintero, state: PlayerState) -> PlayerInput:
	var vertical: bool = lines.guard_axis.get(state.id, Patintero.Axis.HORIZONTAL) == Patintero.Axis.VERTICAL
	var line: float = lines.guard_line.get(state.id, 0.0) as float
	var best: PlayerState = null
	var best_distance: float = INF
	for other: PlayerState in sim.players.values():
		if not lines.is_attacker(other) or other.tagged:
			continue
		var distance: float = absf(other.position.x - line) if vertical else absf(other.position.y - line)
		if distance < best_distance:
			best_distance = distance
			best = other
	if best == null:
		return _stick(Vector2.ZERO)
	var offset: float = (best.position.y - state.position.y) if vertical else (best.position.x - state.position.x)
	if absf(offset) < ARRIVED:
		return _stick(Vector2.ZERO)
	var move: float = signf(offset) * minf(1.0, absf(offset)) * lines.rules.bot_guard_speed
	return _stick(Vector2(0.0, move) if vertical else Vector2(move, 0.0))


# ---- helpers -------------------------------------------------------------------------------

## Toward `target`, leaning away from `danger` while it is within `room` m.
func _away_from(from: Vector2, danger: Vector2, target: Vector2, room: float) -> Vector2:
	var way: Vector2 = _toward(from, target)
	var offset: Vector2 = from - danger
	var distance: float = offset.length()
	if distance > 0.0 and distance < room:
		way += offset / distance * (1.0 - distance / room)
	return way.normalized()


static func _toward(from: Vector2, to: Vector2) -> Vector2:
	var offset: Vector2 = to - from
	return Vector2.ZERO if offset.length() <= ARRIVED else offset.normalized()


static func _stick(move: Vector2) -> PlayerInput:
	return PlayerInput.create(move, Vector2.ZERO, 0, 0)
