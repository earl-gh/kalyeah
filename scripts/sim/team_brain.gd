class_name TeamBrain
extends FightBrain
## A bot that plays a round of tag, not just a duel: a RUSHER walks into the enemy base to capture it (casting at whoever is in
## reach on the way, without stopping to fight) and the lowest-numbered free rusher goes to fetch a tagged teammate instead; a
## DEFENDER is a sparring partner (FightBrain) that holds its post and fights whatever comes within `bot_aggro_range`.
## Pure sim, deterministic for a seed, used by the round balance tool and (later) by practice teams.

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


static func defender(spot: Vector2) -> TeamBrain:
	var brain: TeamBrain = TeamBrain.new()
	brain.mode = Mode.STAND
	brain.home = spot
	return brain


func think(state: PlayerState, sim: MatchSim) -> PlayerInput:
	if role == Role.DEFEND:
		return super.think(state, sim)
	var was_pressing: bool = _press_slot >= 0
	var input: PlayerInput = super.think(state, sim)
	if was_pressing or input.buttons != 0:
		# the two halves of a tap (press, then release aimed): the rusher stands still for them
		return input
	if _teammate_tagged(state, sim) and _is_rescuer(state, sim):
		return _walk_to(state, sim.layout.jail_rect(sim.side_for_team(state.team)).get_center())
	return _walk_to(state, target)


## The free teammate with the lowest id fetches the tagged ones (everyone else keeps going).
func _is_rescuer(state: PlayerState, sim: MatchSim) -> bool:
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == state.team and not other.tagged and other.id < state.id:
			return false
	return true
