class_name DummyBrain
extends RefCounted
## Training-dummy behaviour for practice mode. Produces a PlayerInput each tick;
## dummies never use skills. Pure data in, input out, so it stays deterministic.
## The practice ally is a RESCUER: it runs to its team's rescue area when a teammate is
## tagged, then walks back to where it stood.

enum Mode { STAND, PATROL, RESCUER }
## A rescuer back within this many metres of its spot stands still.
const HOME_TOLERANCE: float = 0.3

var mode: Mode = Mode.STAND
var origin_x: float = 0.0
var half_range: float = 0.0
## Stick magnitude while patrolling (1.0 = full speed).
var speed_scale: float = 1.0
## Where a rescuer waits.
var home: Vector2 = Vector2.ZERO

var _direction: float = 1.0


static func standing() -> DummyBrain:
	return DummyBrain.new()


static func patrolling(p_origin_x: float, p_half_range: float, p_speed_scale: float) -> DummyBrain:
	var brain: DummyBrain = DummyBrain.new()
	brain.mode = Mode.PATROL
	brain.origin_x = p_origin_x
	brain.half_range = p_half_range
	brain.speed_scale = p_speed_scale
	return brain


## Waits at `home` until a teammate is tagged, then fetches them.
static func rescuer(home: Vector2) -> DummyBrain:
	var brain: DummyBrain = DummyBrain.new()
	brain.mode = Mode.RESCUER
	brain.home = home
	return brain


func think(state: PlayerState, sim: MatchSim) -> PlayerInput:
	if mode == Mode.RESCUER:
		return _rescue(state, sim)
	if mode == Mode.STAND:
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0)
	if state.position.x >= origin_x + half_range:
		_direction = -1.0
	elif state.position.x <= origin_x - half_range:
		_direction = 1.0
	return PlayerInput.create(Vector2(_direction * speed_scale, 0.0), Vector2.ZERO, 0, 0)


func _rescue(state: PlayerState, sim: MatchSim) -> PlayerInput:
	var target: Vector2 = home
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == state.team and other.tagged:
			target = sim.layout.rescue_rect(sim.side_for_team(state.team)).get_center()
			break
	var offset: Vector2 = target - state.position
	if offset.length() <= HOME_TOLERANCE:
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0)
	return PlayerInput.create(offset.normalized(), Vector2.ZERO, 0, 0)
