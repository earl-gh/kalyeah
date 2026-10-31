class_name StreetMode
extends RefCounted
## A timed street game played on the Agawan Base street (docs/GDD.md "Tumbang Preso and Patintero"): Patintero (two teams, runners against
## guards on their lines) and Tumbang Preso (every kid is a thrower except the one taya). After `match_seconds` the leader wins (a tie plays
## on to the next point). Plain sim class: MatchSim calls it each tick instead of the rescue and capture rules; it never touches nodes.
## There are no skills in these games: casting is off and nothing deals damage. The subclasses fill in the hooks.

## The data of each street game, by RoomService.MAP_IDS id (Agawan Base, `street`, has none: it is the base game).
const RULES_PATHS: Dictionary[StringName, String] = {
	&"tumbang_preso": "res://data/rules/tumbang_preso.tres",
	&"patintero": "res://data/rules/patintero.tres",
}

## The id the tagger's touch is sent to the views as (`MatchSim.weapon_cast`): the kid plays its run-cast clip, as if it touched the other.
const TAG_CAST: StringName = &"tag"

## `tagger` caught `target` (the touch is made: the target stumbles, and the round resets).
signal tagged(tagger: int, target: int)
## `id` (of `team`) earned a point.
signal scored(team: int, id: int)
## The roles changed hands: Patintero's `who` is the team that runs now; Tumbang Preso's is the new taya's id.
signal roles_swapped(who: int)

var rules: StreetModeRules
## Patintero: the team that runs this turn; the other guards.
var attack_team: int = 0
## Seconds left in the match (0 = time is up; a tie plays on).
var time_left: float = 0.0
## Patintero: seconds left in this turn.
var turn_left: float = 0.0
## Seconds left in which nobody can be tagged (after a change of roles).
var grace_left: float = 0.0
## Seconds left of the pause after a tag (everyone stands still, then the round resets); 0 = playing.
var reset_left: float = 0.0


func _init(p_rules: StreetModeRules) -> void:
	rules = p_rules
	time_left = rules.match_seconds
	turn_left = rules.turn_seconds


## The game for `p_rules.id` (Tumbang Preso or Patintero), or null for another id.
static func create(p_rules: StreetModeRules) -> StreetMode:
	match p_rules.id:
		&"tumbang_preso":
			return TumbangPreso.new(p_rules)
		&"patintero":
			return Patintero.new(p_rules)
	return null


## The rules of game `id`, or null for Agawan Base (or an unknown id).
static func rules_for(id: StringName) -> StreetModeRules:
	return load(RULES_PATHS[id]) as StreetModeRules if RULES_PATHS.has(id) else null


## Everyone to their places for the first turn (call once all players are in).
func setup(sim: MatchSim) -> void:
	for state: PlayerState in sim.players.values():
		state.has_stamina = false  # speed is the game's own: the taya is faster, nobody tires
	place_teams(sim)


## A runner (Patintero) or a thrower (Tumbang Preso); the other side guards.
func is_attacker(state: PlayerState) -> bool:
	return state.team == attack_team


## In the safe home (behind the throw line, or the start line).
func at_home(position: Vector2) -> bool:
	return position.y >= rules.home_z


## Past the other end.
func at_goal(position: Vector2) -> bool:
	return position.y <= rules.goal_z


## True for a free-for-all of single kids (Tumbang Preso) and not two teams: the header shows no team score and the results compare kids.
func is_free_for_all() -> bool:
	return false


## How fast `state` runs next to a normal kid (the taya is faster).
func speed_scale(_state: PlayerState) -> float:
	return 1.0


## One tick of play (after the kids moved and the weapons flew).
func step(sim: MatchSim, dt: float) -> void:
	time_left = maxf(0.0, time_left - dt)
	grace_left = maxf(0.0, grace_left - dt)
	if reset_left > 0.0:
		reset_left -= dt
		if reset_left <= 0.0:
			reset_left = 0.0
			_new_round(sim)
		return
	_step_game(sim, dt)
	if sim.phase != MatchSim.Phase.PLAYING:
		return
	_step_turn(sim, dt)
	if sim.phase == MatchSim.Phase.PLAYING and time_left <= 0.0 and _leader(sim) != -1:
		_end(sim)


## True during the pause after a tag: everyone stands still.
func is_resetting() -> bool:
	return reset_left > 0.0


## After a tag the round resets by itself: a short pause (the tagger's touch plays), then `_new_round`.
func begin_reset(sim: MatchSim) -> void:
	reset_left = rules.reset_seconds
	for state: PlayerState in sim.players.values():
		sim.end_move(state)


## Everyone back to their places for the next round (the points stay). Overridden by the game that has more to put back.
func _new_round(sim: MatchSim) -> void:
	place_teams(sim)
	grace_left = rules.swap_grace


## The rules of the game itself (fetching, scoring, tagging). Overridden.
func _step_game(_sim: MatchSim, _dt: float) -> void:
	pass


## The turns of the game (Patintero's swap of roles). Overridden.
func _step_turn(_sim: MatchSim, _dt: float) -> void:
	pass


## The game's own actions from `state`'s input this tick (a Tumbang Preso throw or kick). There are no skills in the street games.
func handle_input(_sim: MatchSim, _state: PlayerState, _input: PlayerInput, _dt: float) -> void:
	pass


## Where a walk to `point` really goes (a Patintero guard stays on its line).
func move_point(_state: PlayerState, point: Vector2) -> Vector2:
	return point


## Called after `state` moved this tick (a guard is held on its line, a thrower behind the throw line).
func constrain(_sim: MatchSim, _state: PlayerState) -> void:
	pass


## True when `guard` may tag `runner` now. Overridden for each game's own limits.
func can_tag(_sim: MatchSim, guard: PlayerState, runner: PlayerState) -> bool:
	return grace_left <= 0.0 and not guard.tagged and not runner.tagged and guard.effects.can_move() \
		and not at_home(runner.position) and not at_goal(runner.position)


## Every guard tags every runner it touches (the first touch of the tick: it may change who guards).
func check_tags(sim: MatchSim) -> void:
	for guard_id: int in sim.players:
		var guard: PlayerState = sim.players[guard_id]
		if is_attacker(guard):
			continue
		for runner_id: int in sim.players:
			var runner: PlayerState = sim.players[runner_id]
			if not is_attacker(runner) or not can_tag(sim, guard, runner):
				continue
			if guard.position.distance_to(runner.position) <= rules.tag_circle_radius:
				_tag(sim, guard, runner)
				return


## `guard` moved into the tag circle of `runner`: it plays its touch and the round resets. Overridden.
func _tag(sim: MatchSim, guard: PlayerState, runner: PlayerState) -> void:
	sim.weapon_cast.emit(guard.id, TAG_CAST)
	tagged.emit(guard.id, runner.id)
	begin_reset(sim)


## A point for `state` (and its team); with `send_home` it goes back to its place to run again. Once the time is up the point wins the
## match. Overridden by the game that keeps the points its own way.
func award(sim: MatchSim, state: PlayerState, send_home: bool = true, how: int = MatchSim.CaptureKind.NONE) -> void:
	sim.score.points[state.team] += 1
	sim.point_log.append(Vector2i(state.team, how))
	scored.emit(state.team, state.id)
	if send_home:
		state.position = _spot(sim, state)
		sim.end_move(state)
	if time_left <= 0.0:
		_end(sim)


## Everyone to their place for this turn at full HP, back from the sidewalk; the game's own things back to the start.
func place_teams(sim: MatchSim) -> void:
	_reset_game(sim)
	for state: PlayerState in sim.players.values():
		var was_tagged: bool = state.tagged
		sim.objectives.clear_actions(state)
		state.tagged = false
		state.tag_time = 0.0
		state.tag_order = 0
		state.hp = sim.rules.player_max_hp
		state.position = _spot(sim, state)
		state.spawn_position = state.position
		state.facing = Vector2(0.0, -1.0) if is_attacker(state) else Vector2(0.0, 1.0)
		sim.refill_stamina(state)
		if was_tagged:
			sim.player_rescued.emit(state.id, MatchSim.RELEASED_ALONE)


## The game's own things (the can, the slippers, who crossed) back to the start of a turn. Overridden.
func _reset_game(_sim: MatchSim) -> void:
	pass


## Where `state` stands at the start of a turn.
func _spot(sim: MatchSim, state: PlayerState) -> Vector2:
	var mates: Array[int] = []
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == state.team and is_attacker(other) == is_attacker(state):
			mates.append(id)
	return spot_for(state, is_attacker(state), mates.find(state.id), mates.size())


## The start spot of the `slot`-th of `count` kids of a role. Attackers stand in a row behind the home line. Overridden for guards.
func spot_for(_state: PlayerState, attacker: bool, slot: int, count: int) -> Vector2:
	var x: float = (float(slot) - float(count - 1) / 2.0) * rules.spawn_spacing
	return Vector2(x, rules.home_z + rules.spawn_spacing if attacker else 0.0)


## Who leads: a team (Patintero), or -1 when it is a tie. Overridden by the free-for-all.
func _leader(sim: MatchSim) -> int:
	var points: Array[int] = sim.score.points
	return -1 if points[0] == points[1] else (0 if points[0] > points[1] else 1)


## Time is up and one side leads (or a golden point was scored): it wins.
func _end(sim: MatchSim) -> void:
	sim.score.winner = maxi(_leader(sim), 0)
	for state: PlayerState in sim.players.values():
		sim.end_move(state)
	sim.phase = MatchSim.Phase.MATCH_OVER
	sim.match_won.emit(sim.score.winner)


## How the match went for the local kid: {won, mine, theirs} (the points of the local side and of the best other side).
func local_result(sim: MatchSim, local_id: int) -> Dictionary:
	var team: int = sim.players[local_id].team if sim.players.has(local_id) else 0
	return {"won": sim.score.winner == team, "mine": sim.score.points[team], "theirs": sim.score.points[1 - team]}
