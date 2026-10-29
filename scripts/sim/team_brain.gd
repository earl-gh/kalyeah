class_name TeamBrain
extends FightBrain
## A bot that plays a round of tag, not just a duel, in this order (owner): 1. tag enemies by killing them (anything within
## `bot_aggro_range` is fought), 2. stay at the enemy base (a RUSHER walks in and holds it; a DEFENDER holds its own base and does not
## chase out of it), 3. rescue a tagged teammate (the lowest-numbered free one, and only once it stands at its post).
## Pure sim, deterministic for a seed, used by the round balance tool and (later) by practice teams.

## A defender stays this far inside its base's edge (metres).
const GUARD_MARGIN: float = 0.5

enum Role { RUSH, DEFEND }

var role: Role = Role.DEFEND
## Where a rusher goes: its place in the enemy base.
var target: Vector2 = Vector2.ZERO


static func rusher(spot: Vector2, enemy_base_spot: Vector2) -> TeamBrain:
	var brain: TeamBrain = TeamBrain.new()
	brain.mode = Mode.STAND
	brain.home = spot
	brain.role = Role.RUSH
	brain.target = enemy_base_spot
	return brain


## A member of a squad that switches between attacking and defending (TeamCoach sets `role`): it starts as a defender.
static func member(spot: Vector2, enemy_base_spot: Vector2) -> TeamBrain:
	var brain: TeamBrain = TeamBrain.rusher(spot, enemy_base_spot)
	brain.role = Role.DEFEND
	return brain


static func defender(spot: Vector2) -> TeamBrain:
	var brain: TeamBrain = TeamBrain.new()
	brain.mode = Mode.STAND
	brain.home = spot
	return brain


func think(state: PlayerState, sim: MatchSim) -> PlayerInput:
	var input: PlayerInput = super.think(state, sim)
	if role == Role.DEFEND:
		return _guard(state, sim, input)
	return input


## No enemy in reach: a rusher goes to and stays in the enemy base, a defender stays in its own, and only one already at its post (the
## lowest-numbered free one) sets off to rescue (priorities 2 and 3, see FightBrain). A rusher that meets an enemy on the way now
## stops and fights it (priority 1): killing is what keeps the base open.
func _no_enemy(state: PlayerState, sim: MatchSim) -> PlayerInput:
	if _rescue_due(state, sim) and _is_rescuer(state, sim):
		return _walk_to(state, sim.layout.rescue_rect(sim.side_for_team(state.team)).get_center())
	return _walk_to(state, target if role == Role.RUSH else home)


## A rusher is at its post in the enemy base, a defender in its own.
func _at_post(state: PlayerState, sim: MatchSim) -> bool:
	var side: int = -sim.side_for_team(state.team) if role == Role.RUSH else sim.side_for_team(state.team)
	return sim.layout.in_base(side, state.position)


## A defender fights from inside its own base and does not chase out of it (stepping out would leave the base open); with
## no enemy near it is free to go, for instance to fetch a tagged teammate.
func _guard(state: PlayerState, sim: MatchSim, input: PlayerInput) -> PlayerInput:
	if not input.is_pressed(PlayerInput.BTN_MOVE_TARGET) or sim.nearest_enemy(state, sim.rules.bot_aggro_range) == null:
		return input
	var way: Vector2 = input.move_target - state.position
	if way.length_squared() <= 0.0:
		return input
	var centre: Vector2 = sim.layout.base_center(sim.side_for_team(state.team))
	var edge: float = sim.layout.base_radius - GUARD_MARGIN
	var ahead: Vector2 = state.position + way.normalized() * 0.3
	if ahead.distance_to(centre) > edge and ahead.distance_to(centre) > state.position.distance_to(centre):
		input.move_target = state.position  # stand at the edge instead of stepping out
	return input


## The team's rescuer fetches the tagged ones (everyone else keeps going): the one its TeamCoach picked, or, deciding alone, the free
## teammate with the lowest id.
func _is_rescuer(state: PlayerState, sim: MatchSim) -> bool:
	if rescue_duty >= 0:
		return rescue_duty == 1
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == state.team and not other.tagged and other.id < state.id:
			return false
	return true
