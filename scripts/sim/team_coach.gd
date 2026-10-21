class_name TeamCoach
extends RefCounted
## Plays one team of bots in a Practice match. Every bot is a real player of the sim (so it counts for a capture, can be
## tagged and rescued), driven by a TeamBrain, and the coach decides each tick whether the team attacks (everyone rushes the
## enemy base) or defends (everyone holds its base, fights what comes near and fetches a tagged teammate).
## A team with a human to follow (the player's allies) attacks while that human is free in the enemy half and defends otherwise,
## so the allies back up what the player does. A team without one (the enemies) waits `bot_assault_delay` seconds into the round,
## then attacks in waves (`bot_assault_window` seconds of attack, `bot_rest_time` seconds back home), and only while at least two
## thirds of it are free: with more tagged it falls back to defending.

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
	for id: int in _brains:
		var state: PlayerState = sim.players[id]
		if state.tagged:
			continue
		var brain: TeamBrain = _brains[id]
		brain.role = TeamBrain.Role.RUSH if plan == Plan.ASSAULT else TeamBrain.Role.DEFEND
		sim.set_input(id, brain.think(state, sim))


func decide(sim: MatchSim) -> Plan:
	if follow_id >= 0 and sim.players.has(follow_id):
		var human: PlayerState = sim.players[follow_id]
		return Plan.ASSAULT if not human.tagged and _in_enemy_half(sim, human) else Plan.DEFEND
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
