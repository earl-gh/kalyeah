class_name FxContext
extends RefCounted
## What every effect controller reads: the sim it draws, the view's own clock, Reduce motion (read every frame), how much
## bigger than life the kids are drawn, the viewer's team, the shared pools, and where each recent cast came from (the
## thrower's position on `weapon_cast`, relayed online too) so a tossed can or paper ball can fly from the right hand.

## Casts remembered for this long (seconds) and at most this many.
const CAST_MEMORY: float = 2.0
const CAST_CAP: int = 16

var sim: MatchSim
## Seconds of view time (summed frame deltas; not the sim's clock).
var time: float = 0.0
var delta: float = 0.0
var reduced: bool = false
var view_scale: float = 1.0
var local_team: int = 0
var _casts: Array[Dictionary] = []


## Your team's colour for an effect thrown by `team`, else the other team's (colour-blind safe, read now).
func team_color(team: int) -> Color:
	return TeamColors.world_own() if team == local_team else TeamColors.world_enemy()


## The team of player `id` (the viewer's team when unknown).
func team_of(id: int) -> int:
	var state: PlayerState = sim.players.get(id) as PlayerState if sim != null else null
	return state.team if state != null else local_team


## Remembers that player `id` cast `weapon_id` now, from where it stands.
func note_cast(id: int, weapon_id: StringName) -> void:
	if sim == null or not sim.players.has(id):
		return
	var state: PlayerState = sim.players[id]
	if _casts.size() >= CAST_CAP:
		_casts.pop_front()
	_casts.append({"id": id, "w": weapon_id, "pos": state.position, "team": state.team, "time": time})


## Where the latest cast of `weapon_id` by `team` (within `max_age` seconds) came from, or null.
func cast_origin(weapon_id: StringName, team: int, max_age: float = 1.0) -> Variant:
	for i: int in range(_casts.size() - 1, -1, -1):
		var cast: Dictionary = _casts[i]
		if time - (cast["time"] as float) > max_age:
			break
		if cast["w"] == weapon_id and cast["team"] == team:
			return cast["pos"] as Vector2
	return null


## Who cast `weapon_id` for `team` last (within `max_age` seconds), or -1.
func cast_owner(weapon_id: StringName, team: int, max_age: float = 1.0) -> int:
	for i: int in range(_casts.size() - 1, -1, -1):
		var cast: Dictionary = _casts[i]
		if time - (cast["time"] as float) > max_age:
			break
		if cast["w"] == weapon_id and cast["team"] == team:
			return cast["id"] as int
	return -1


## Drops casts older than `CAST_MEMORY`.
func forget_old() -> void:
	while not _casts.is_empty() and time - (_casts[0]["time"] as float) > CAST_MEMORY:
		_casts.pop_front()
