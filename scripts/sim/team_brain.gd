class_name TeamBrain
extends FightBrain
## A bot that plays a round of tag, not just a duel, in this order (owner): 1. tag enemies by killing them (anything within
## `bot_aggro_range` is fought), 2. stay at the enemy base (a RUSHER walks in and holds it; a DEFENDER holds its own base and does not
## chase out of it), 3. rescue a tagged teammate (the lowest-numbered free one, and only once it stands at its post).
## Pure sim, deterministic for a seed, used by the round balance tool and (later) by practice teams.

## A defender stays this far inside its base's edge (metres).
const GUARD_MARGIN: float = 0.5
## A rescuer waiting in its rescue area walks about this far inside its edge (metres).
const RESCUE_PATROL_MARGIN: float = 0.8
## Where a rusher that stepped out to fight waits for the next enemy to show up: this far outside the enemy base's edge (metres).
const SIEGE_GAP: float = 2.0

enum Role { RUSH, DEFEND }

var role: Role = Role.DEFEND
## Set by the coach on an attacker that stays in the enemy base for good (never steps out to fight, so the capture keeps running).
var hold: bool = false
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


## No enemy in reach: a rusher goes to the enemy base and walks about in it, a defender walks about in its own, and only one already at
## its post (the lowest-numbered free one) sets off to rescue (priorities 2 and 3, see FightBrain). A rusher that meets an enemy on the
## way stops and fights it (priority 1): killing is what keeps the base open. Nobody stands still while there is something to do:
## the rescuer waiting for its area walks about in it too.
func _no_enemy(state: PlayerState, sim: MatchSim) -> PlayerInput:
	var team_side: int = sim.side_for_team(state.team)
	if _rescue_due(state, sim) and _is_rescuer(state, sim):
		var area: Rect2 = sim.layout.rescue_rect(team_side)
		if sim.objectives.in_rescue_area(sim, state):
			return _patrol(state, sim, area, RESCUE_PATROL_MARGIN)
		return _walk_to(state, area.get_center())
	if role == Role.RUSH:
		var enemy_side: int = -team_side
		if _stay_out_left > 0.0 and not hold and not sim.layout.in_base(enemy_side, state.position):
			return _patrol(state, sim, _siege_area(sim, enemy_side), 0.0)
		if sim.layout.in_base(enemy_side, state.position):
			return _patrol(state, sim, sim.layout.base_rect(enemy_side), sim.layout.base_corner)
		return _walk_to(state, target)
	if sim.layout.in_base(team_side, state.position):
		return _patrol(state, sim, sim.layout.base_rect(team_side), sim.layout.base_corner)
	return _walk_to(state, home)


## A patch of street just outside the enemy base, on the side toward the middle of the street.
func _siege_area(sim: MatchSim, enemy_side: int) -> Rect2:
	var rect: Rect2 = sim.layout.base_rect(enemy_side)
	var toward: float = -float(enemy_side)
	var z: float = rect.position.y - SIEGE_GAP if toward < 0.0 else rect.end.y + SIEGE_GAP
	return Rect2(rect.position.x, z - 1.0, rect.size.x, 2.0).intersection(sim.layout.lane_rect())


## A rusher is at its post in the enemy base, a defender in its own.
func _at_post(state: PlayerState, sim: MatchSim) -> bool:
	var side: int = -sim.side_for_team(state.team) if role == Role.RUSH else sim.side_for_team(state.team)
	return sim.layout.in_base(side, state.position)


## A defender fights from inside its own base and does not chase out of it (stepping out would leave the base open): at the edge it
## slides along it instead of standing; with no enemy near it is free to go, for instance to fetch a tagged teammate.
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
		var radial: Vector2 = (state.position - centre).normalized()
		var along: Vector2 = radial.orthogonal()
		if along.dot(way) < 0.0:
			along = -along
		input.move_target = state.position + along * STEER_AHEAD  # along the edge instead of stepping out
	return input


## A base holder stays in the enemy base it is in, even with an enemy near (nothing can be cast there, but the capture runs).
func _holds_zone(state: PlayerState, sim: MatchSim) -> bool:
	return hold and role == Role.RUSH and sim.layout.in_base(-sim.side_for_team(state.team), state.position)


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
