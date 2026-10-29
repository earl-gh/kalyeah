class_name TeamCoach
extends RefCounted
## Plays one team of bots in a Practice match. Every bot is a real player of the sim (so it counts for a capture, can be
## tagged and rescued), driven by a TeamBrain, and the coach decides each tick whether the team attacks (everyone rushes the
## enemy base) or defends (everyone holds its base, fights what comes near and fetches a tagged teammate).
## Both teams wait `bot_assault_delay` seconds into the round, then attack in waves (`bot_assault_window` seconds of attack,
## `bot_rest_time` seconds back home), and only while at least two thirds of them are free: with more tagged they fall back to
## defending. A team with a human to follow (the player's allies) also attacks whenever that human is free in the enemy half, so it
## backs up what the player does (owner, 2026-10-29: it used to wait for the human, and stood still in its own half until then).
## Whatever the plan, three bots get a job of their own each tick:
## - the rescuer: the free bot nearest the team's rescue area, while a teammate (bot or human) is tagged;
## - a guard: while the other team is taking this team's base and the plan is to attack, the free bot nearest home goes back;
## - a pusher (a team backing up a human, while it defends): the free bot nearest the enemy base attacks it alone once it is safe,
##   when at most a third of the enemies are free (an empty enemy base is not enough: it is empty because they are attacking).

enum Plan { DEFEND, ASSAULT }

var team: int
var plan: Plan = Plan.DEFEND
## The human this team backs up (-1: none, the team plans by itself).
var follow_id: int = -1

var _brains: Dictionary[int, TeamBrain] = {}


func _init(p_team: int, p_follow_id: int = -1) -> void:
	team = p_team
	follow_id = p_follow_id


## Puts `id` (already added to the sim) under this coach. `skill` is how well it aims (0 to 1).
func add(sim: MatchSim, id: int, skill: float) -> void:
	var state: PlayerState = sim.players[id]
	var enemy_side: int = -sim.side_for_team(team)
	var spot: Vector2 = sim.layout.spawn_center(enemy_side) + Vector2(float(_brains.size() - 1) * 1.2, 0.0)
	var brain: TeamBrain = TeamBrain.member(state.spawn_position, spot)
	brain.skill = skill
	_brains[id] = brain


func members() -> Array[int]:
	var ids: Array[int] = []
	ids.assign(_brains.keys())
	return ids


## Decides the plan and sets the input of every free bot for this tick. Call before `sim.step`.
func drive(sim: MatchSim) -> void:
	if sim.phase != MatchSim.Phase.PLAYING:
		return
	plan = decide(sim)
	var rescuer: int = rescuer_id(sim)
	var guard: int = guard_id(sim, rescuer)
	var pusher: int = pusher_id(sim, rescuer)
	for id: int in _brains:
		var state: PlayerState = sim.players[id]
		if state.tagged:
			continue
		var brain: TeamBrain = _brains[id]
		var attack: bool = (plan == Plan.ASSAULT and id != guard) or id == pusher
		brain.role = TeamBrain.Role.RUSH if attack else TeamBrain.Role.DEFEND
		brain.rescue_duty = 1 if id == rescuer else 0
		sim.set_input(id, brain.think(state, sim))


## The bot that fetches a tagged teammate: the free one nearest the team's rescue area (-1: nobody is tagged, or no bot is free).
func rescuer_id(sim: MatchSim) -> int:
	if not sim.objectives.teammate_waiting(sim, team):
		return -1
	return _nearest_free(sim, sim.layout.rescue_rect(sim.side_for_team(team)).get_center(), -1)


## The bot sent home while the other team takes this team's base and the plan is to attack (-1: none needed).
func guard_id(sim: MatchSim, rescuer: int) -> int:
	if plan != Plan.ASSAULT or sim.capture_team != 1 - team or sim.capture_kind != MatchSim.CaptureKind.BASE:
		return -1
	return _nearest_free(sim, sim.layout.base_center(sim.side_for_team(team)), rescuer)


## The bot that attacks alone while a team backing up a human defends, once that is safe (-1: none).
func pusher_id(sim: MatchSim, rescuer: int) -> int:
	if follow_id < 0 or plan != Plan.DEFEND:
		return -1
	var enemies: int = 0
	var free: int = 0
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team != team:
			enemies += 1
			if not other.tagged:
				free += 1
	if enemies == 0 or free * 3 > enemies:
		return -1
	return _nearest_free(sim, sim.layout.base_center(-sim.side_for_team(team)), rescuer)


## The free bot of this team nearest `point`, other than `skip` (-1: none).
func _nearest_free(sim: MatchSim, point: Vector2, skip: int) -> int:
	var best: int = -1
	var best_distance: float = INF
	for id: int in _brains:
		var state: PlayerState = sim.players[id]
		if state.tagged or id == skip:
			continue
		var distance: float = state.position.distance_to(point)
		if distance < best_distance:
			best = id
			best_distance = distance
	return best


func decide(sim: MatchSim) -> Plan:
	if follow_id >= 0 and sim.players.has(follow_id):
		var human: PlayerState = sim.players[follow_id]
		if not human.tagged and _in_enemy_half(sim, human):
			return Plan.ASSAULT
	if sim.round_time < sim.rules.bot_assault_delay:
		return Plan.DEFEND
	var window: float = sim.rules.bot_assault_window
	if window > 0.0 and fmod(sim.round_time - sim.rules.bot_assault_delay, window + sim.rules.bot_rest_time) >= window:
		return Plan.DEFEND
	var free: int = 0
	for id: int in _brains:
		if not sim.players[id].tagged:
			free += 1
	return Plan.ASSAULT if free * 3 >= _brains.size() * 2 else Plan.DEFEND


## The half of the street on the far side of this team's own base.
func _in_enemy_half(sim: MatchSim, state: PlayerState) -> bool:
	var own_z: float = sim.layout.base_center(sim.side_for_team(team)).y
	return state.position.y * own_z < 0.0
