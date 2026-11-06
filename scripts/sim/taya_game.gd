class_name TayaGame
extends StreetMode
## A street game with one taya (the "it") and every other kid playing for itself (docs/GDD.md "Tumbang Preso", "Langit Lupa"): the
## first taya is drawn at random from everyone (the match's seeded generator), each kid keeps its own points and the one with the most
## wins when the time is up. Tumbang Preso and Langit Lupa fill in what the taya and the others do.

## The taya (the "it").
var taya_id: int = -1
## id -> points (the match is won by the kid with the most).
var points: Dictionary[int, int] = {}
## The kid that won (-1 until the match is over).
var winner_id: int = -1


func is_free_for_all() -> bool:
	return true


func synced_fields() -> Array[StringName]:
	var fields: Array[StringName] = super()
	fields.append_array([&"taya_id", &"points", &"winner_id"])
	return fields


func is_attacker(state: PlayerState) -> bool:
	return state.id != taya_id


func speed_scale(state: PlayerState) -> float:
	return rules.taya_speed_share if state.id == taya_id else 1.0


## The first taya is drawn at random from everyone in the match (nobody picks it in the lobby or the room), then everyone goes to their place.
func setup(sim: MatchSim) -> void:
	var kids: Array[int] = []
	for id: int in sim.players:
		points[id] = 0
		kids.append(id)
	if not kids.is_empty():
		taya_id = kids[sim.rng.randi_range(0, kids.size() - 1)]
	super.setup(sim)


## The kids by points, most first (ties by id).
func ranking() -> Array[int]:
	var ids: Array[int] = []
	ids.assign(points.keys())
	ids.sort_custom(func(a: int, b: int) -> bool: return points[a] > points[b] or (points[a] == points[b] and a < b))
	return ids


## A point for `state`; with `send_home` it is back in its place. Once the time is up the point wins the match.
func award(sim: MatchSim, state: PlayerState, send_home: bool = true, _how: int = MatchSim.CaptureKind.NONE) -> void:
	points[state.id] = points.get(state.id, 0) + 1
	scored.emit(state.team, state.id)
	if send_home:
		state.position = _spot(sim, state)
		sim.end_move(state)
	if time_left <= 0.0:
		_end(sim)


## The kid with the most points, or -1 when two or more share the most (or nobody has any).
func _leader(_sim: MatchSim) -> int:
	var best: int = -1
	var tie: bool = false
	var top: int = 0
	for id: int in points:
		if points[id] > top:
			top = points[id]
			best = id
			tie = false
		elif points[id] == top and top > 0:
			tie = true
	return -1 if tie else best


func _end(sim: MatchSim) -> void:
	winner_id = _leader(sim)
	sim.score.winner = 0  # the sim's own winner is a team: everyone is on team 0, the kid that won is `winner_id`
	for state: PlayerState in sim.players.values():
		sim.end_move(state)
	sim.phase = MatchSim.Phase.MATCH_OVER
	sim.match_won.emit(0)


func local_result(_sim: MatchSim, local_id: int) -> Dictionary:
	var theirs: int = 0
	for id: int in points:
		if id != local_id:
			theirs = maxi(theirs, points[id])
	return {"won": winner_id == local_id, "mine": points.get(local_id, 0), "theirs": theirs}
