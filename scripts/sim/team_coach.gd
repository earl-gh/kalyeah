class_name TeamCoach
extends RefCounted
## Plays one team of bots in a Practice or bot-filled match (up to 5 bots per side in 5v5 Agawang Base).
## Every bot is a real player of the sim (so it counts for a capture, can be tagged and rescued), driven by a
## TeamBrain, and the coach decides each tick whether the team attacks or defends.
## Both teams wait `bot_assault_delay` seconds into the round, then attack in waves (`bot_assault_window` seconds of
## attack, `bot_rest_time` seconds back home), and only while at least two thirds of them are free: with more tagged
## they fall back to defending. A team with a human to follow (the player's allies) also attacks whenever that human
## is free in the enemy half, backing up the push.
## Tactical 5-bot role distribution each tick:
## - `rescuer_id`: free bot nearest own rescue zone while a teammate (bot or human) is tagged;
## - `guard_ids` / `guard_id`: up to 2 free bots nearest home fall back to contest when own base is being captured;
## - `pusher_id`: on a squad backing up a defending human, 1 bot pushes once at most a third of enemies remain free;
## - `bot_base_holders` (`2`): hold compact 2D courtyard positions inside `base_center(enemy_side)` without stepping
##   out, while remaining vanguard attackers screen the eskinita approaches outside `no_cast_in_enemy_base`.

enum Plan { DEFEND, ASSAULT }

## Compact 2D pentagon courtyard unit offsets around `base_center(enemy_side)` so 5 bots stay well inside circular bases.
const COURTYARD_OFFSETS: Array[Vector2] = [
	Vector2(0.0, -0.42),
	Vector2(-0.72, -0.14),
	Vector2(0.72, -0.14),
	Vector2(-0.46, 0.62),
	Vector2(0.46, 0.62),
]
## Distance at which a vanguard screener cycles to its alternate eskinita patrol post so it never stands idle.
const SCREEN_REACH: float = 2.4
## Maximum home-base defenders peeled off an assault wave on a 4–5 bot squad.
const MAX_SQUAD_GUARDS: int = 2

var team: int
var plan: Plan = Plan.DEFEND
## The human this team backs up (-1: none, the team plans by itself).
var follow_id: int = -1

var _brains: Dictionary[int, TeamBrain] = {}
## The attackers that hold the enemy base, kept while they stay free and attacking (so the job does not pass back and forth).
var _holders: Array[int] = []
var _courtyard_spots: Dictionary[int, Vector2] = {}
var _screen_phase: Dictionary[int, int] = {}


func _init(p_team: int, p_follow_id: int = -1) -> void:
	team = p_team
	follow_id = p_follow_id


## Puts `id` (already added to the sim) under this coach in a compact 2D courtyard formation inside the enemy base.
func add(sim: MatchSim, id: int, skill: float) -> void:
	var state: PlayerState = sim.players[id]
	var enemy_side: int = -sim.side_for_team(team)
	var slot: int = _brains.size()
	var spot: Vector2 = _courtyard_spot(sim, enemy_side, slot)
	_courtyard_spots[id] = spot
	_screen_phase[id] = slot % 2
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
	var guards: Array[int] = guard_ids(sim, rescuer)
	var pusher: int = pusher_id(sim, rescuer)
	_pick_holders(sim, rescuer, guards, pusher)
	var enemies_free: bool = _any_free_enemy(sim)
	var vanguard_slot: int = 0
	var fallback_spot: Vector2 = sim.layout.base_center(-sim.side_for_team(team))
	for id: int in _brains:
		var state: PlayerState = sim.players[id]
		if state.tagged:
			continue
		var brain: TeamBrain = _brains[id]
		var attack: bool = (plan == Plan.ASSAULT and not guards.has(id)) or id == pusher
		brain.role = TeamBrain.Role.RUSH if attack else TeamBrain.Role.DEFEND
		brain.rescue_duty = 1 if id == rescuer else 0
		brain.hold = attack and _holders.has(id)
		if brain.hold or sim.rules.bot_base_holders <= 0 or not enemies_free:
			brain.target = _courtyard_spots[id] if _courtyard_spots.has(id) else fallback_spot
		elif attack:
			brain.target = _vanguard_target(sim, state, vanguard_slot)
			vanguard_slot += 1
		sim.set_input(id, brain.think(state, sim))


## Chooses which attackers hold the enemy base this tick: the ones already holding stay, then the free attackers nearest that base.
func _pick_holders(sim: MatchSim, rescuer: int, guards: Array[int], pusher: int) -> void:
	var wanted: int = sim.rules.bot_base_holders
	var attackers: Array[int] = []
	for id: int in _brains:
		if sim.players[id].tagged or id == rescuer or guards.has(id):
			continue
		if (plan == Plan.ASSAULT) or id == pusher:
			attackers.append(id)
	_holders = _holders.filter(func(id: int) -> bool: return attackers.has(id))
	var spot: Vector2 = sim.layout.base_center(-sim.side_for_team(team))
	attackers.sort_custom(func(a: int, b: int) -> bool:
		return sim.players[a].position.distance_squared_to(spot) < sim.players[b].position.distance_squared_to(spot))
	for id: int in attackers:
		if _holders.size() >= wanted:
			break
		if not _holders.has(id):
			_holders.append(id)
	while _holders.size() > wanted:
		_holders.pop_back()


## The bot that fetches a tagged teammate: the free one nearest the team's rescue area (-1: nobody is tagged, or no bot is free).
func rescuer_id(sim: MatchSim) -> int:
	if not sim.objectives.teammate_waiting(sim, team):
		return -1
	return _nearest_free(sim, sim.layout.rescue_rect(sim.side_for_team(team)).get_center(), -1)


## Primary guard sent home while the other team captures this team's base during an attack (-1: none needed).
func guard_id(sim: MatchSim, rescuer: int) -> int:
	var guards: Array[int] = guard_ids(sim, rescuer)
	return guards[0] if not guards.is_empty() else -1


## Up to 2 guards on 4–5 bot squads (1 on <=3 bot squads) peel back to contest when own base is being captured during an assault.
func guard_ids(sim: MatchSim, rescuer: int) -> Array[int]:
	var result: Array[int] = []
	if plan != Plan.ASSAULT or sim.capture_team != 1 - team or sim.capture_kind != MatchSim.CaptureKind.BASE:
		return result
	var home: Vector2 = sim.layout.base_center(sim.side_for_team(team))
	var first: int = _nearest_free(sim, home, rescuer)
	if first < 0:
		return result
	result.append(first)
	if _brains.size() < 4:
		return result
	var available: int = 0
	for id: int in _brains:
		if not sim.players[id].tagged and id != rescuer:
			available += 1
	if available >= 3:
		var second: int = _nearest_free(sim, home, rescuer, first)
		if second >= 0 and result.size() < MAX_SQUAD_GUARDS:
			result.append(second)
	return result


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


## The free bot of this team nearest `point`, skipping `skip` and `second_skip` (-1: none).
func _nearest_free(sim: MatchSim, point: Vector2, skip: int, second_skip: int = -1) -> int:
	var best: int = -1
	var best_distance: float = INF
	for id: int in _brains:
		var state: PlayerState = sim.players[id]
		if state.tagged or id == skip or id == second_skip:
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


## Compact 2D courtyard formation around `base_center(side)` kept strictly inside `in_base(side, ...)`.
func _courtyard_spot(sim: MatchSim, side: int, slot: int) -> Vector2:
	var center: Vector2 = sim.layout.base_center(side)
	var spread: float = clampf(sim.layout.base_radius * 0.32, 1.0, 3.8)
	var unit: Vector2 = COURTYARD_OFFSETS[slot % COURTYARD_OFFSETS.size()]
	var spot: Vector2 = center + Vector2(unit.x, unit.y * float(-side)) * spread
	var margin: float = minf(maxf(sim.rules.player_radius + 0.6, 1.0), sim.layout.base_radius * 0.35)
	var safe_rect: Rect2 = sim.layout.base_rect(side).grow(-margin)
	if safe_rect.size.x > 0.0 and safe_rect.size.y > 0.0:
		spot = spot.clamp(safe_rect.position, safe_rect.end)
	return spot


## Dynamic screening waypoint across the eskinita approaches outside the enemy base so vanguard bots stay armed and moving.
func _vanguard_target(sim: MatchSim, state: PlayerState, lane_slot: int) -> Vector2:
	var enemy_side: int = -sim.side_for_team(team)
	var phase: int = _screen_phase[state.id] if _screen_phase.has(state.id) else 0
	var goal: Vector2 = _screen_spot(sim, enemy_side, lane_slot, phase)
	if state.position.distance_to(goal) <= SCREEN_REACH or state.stuck_time > 0.35:
		phase = 1 - phase
		_screen_phase[state.id] = phase
		goal = _screen_spot(sim, enemy_side, lane_slot, phase)
	return goal


func _screen_spot(sim: MatchSim, enemy_side: int, lane_slot: int, phase: int) -> Vector2:
	var base: Vector2 = sim.layout.base_center(enemy_side)
	var inward: Vector2 = (-base).normalized() if base.length_squared() > 0.001 else Vector2(0.0, -float(enemy_side))
	var across: Vector2 = Vector2(-inward.y, inward.x)
	var depth: float = sim.layout.base_radius + (5.2 if phase == 0 else 9.6)
	var max_lat: float = maxf(sim.layout.lane_width * 0.32, 2.5)
	var lane: int = lane_slot % 3
	var lateral: float = 0.0
	if lane == 0:
		lateral = -minf(8.5 if phase == 0 else 3.6, max_lat)
	elif lane == 1:
		lateral = minf(8.5 if phase == 0 else 3.6, max_lat)
	else:
		lateral = minf(3.2, max_lat) * (-1.0 if phase == 0 else 1.0)
	var point: Vector2 = base + inward * depth + across * lateral
	if sim.layout.in_base(enemy_side, point):
		point += inward * (sim.layout.base_radius * 0.5 + 3.0)
	var clearance: float = sim.rules.player_radius + 0.8
	point = sim.layout.keep_in_street(point, clearance)
	for rect: Rect2 in sim.layout.walk_obstacle_rects():
		var padded: Rect2 = rect.grow(clearance)
		if not padded.has_point(point):
			continue
		var d_left: float = absf(point.x - padded.position.x)
		var d_right: float = absf(padded.end.x - point.x)
		var d_top: float = absf(point.y - padded.position.y)
		var d_bot: float = absf(padded.end.y - point.y)
		var best: float = minf(minf(d_left, d_right), minf(d_top, d_bot))
		if is_equal_approx(best, d_left):
			point.x = padded.position.x - 0.2
		elif is_equal_approx(best, d_right):
			point.x = padded.end.x + 0.2
		elif is_equal_approx(best, d_top):
			point.y = padded.position.y - 0.2
		else:
			point.y = padded.end.y + 0.2
	return sim.layout.keep_in_street(point, clearance)


func _any_free_enemy(sim: MatchSim) -> bool:
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team != team and not other.tagged:
			return true
	return false
