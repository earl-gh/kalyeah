class_name MatchObjectives
extends RefCounted
## Tagging, rescue, capture scoring, and between-point round reset rules.


## Off to the box of the player's team (own: top-left, enemy: bottom-right).
func tag(sim: MatchSim, state: PlayerState) -> void:
	var waiting: int = 0
	for id: int in sim.players:
		if sim.players[id].team == state.team and sim.players[id].tagged:
			waiting += 1
	state.tagged = true
	state.hp = 0
	state.tag_time = 0.0
	sim.tag_counter += 1
	state.tag_order = sim.tag_counter
	clear_actions(state)
	state.position = sim.layout.jail_slot(sim.side_for_team(state.team), waiting)
	# the tagged kid in the box looks toward the enemy's post, where the other team is holding it (owner)
	state.facing = (sim.layout.base_center(-sim.side_for_team(state.team)) - state.position).normalized()
	sim.player_tagged.emit(state.id)


## A tagged teammate waits in the box (the rescue area is only a rescue area while someone needs rescuing).
func teammate_waiting(sim: MatchSim, team: int) -> bool:
	for id: int in sim.players:
		if sim.players[id].team == team and sim.players[id].tagged:
			return true
	return false


## `state` stands in the rescue area round its own team's box (or the box).
func in_rescue_area(sim: MatchSim, state: PlayerState) -> bool:
	return sim.layout.rescue_rect(sim.side_for_team(state.team)).grow(state.radius).has_point(state.position)


## Rescuing (owner): one free teammate at a time stays in the rescue area for `rescue_hold_time` and frees the teammate who was
## tagged first (everyone waiting when `rescue_frees_all` is on). The first to arrive holds the rescue; the others can stand there
## but cannot start one. Leaving the area, being tagged or a new round starts the count over, and the next in line takes over.
func check_rescues(sim: MatchSim, dt: float) -> void:
	for team: int in [0, 1]:
		var holder: PlayerState = sim.players.get(sim.rescue_rescuer[team]) as PlayerState
		if holder != null and (holder.tagged or holder.team != team or not in_rescue_area(sim, holder)):
			holder = null
		if holder == null or not teammate_waiting(sim, team):
			holder = null
			sim.rescue_elapsed[team] = 0.0
			for id: int in sim.players:
				var candidate: PlayerState = sim.players[id]
				if candidate.team == team and not candidate.tagged and in_rescue_area(sim, candidate) and teammate_waiting(sim, team):
					holder = candidate
					break
		sim.rescue_rescuer[team] = holder.id if holder != null else -1
		if holder == null:
			continue
		sim.rescue_elapsed[team] += dt
		if sim.rescue_elapsed[team] + 1e-6 < sim.rules.rescue_hold_time:
			continue
		sim.rescue_elapsed[team] = 0.0
		for waiting: PlayerState in _waiting_in_tag_order(sim, team):
			rescue(sim, waiting, holder.id)
			if not sim.rules.rescue_frees_all:
				break


## The tagged players of `team`, the one tagged first at the front.
func _waiting_in_tag_order(sim: MatchSim, team: int) -> Array[PlayerState]:
	var found: Array[PlayerState] = []
	for id: int in sim.players:
		if sim.players[id].team == team and sim.players[id].tagged:
			found.append(sim.players[id])
	found.sort_custom(func(a: PlayerState, b: PlayerState) -> bool: return a.tag_order < b.tag_order)
	return found


## Back to the post at full HP.
func rescue(sim: MatchSim, state: PlayerState, by_id: int) -> void:
	state.tagged = false
	state.tag_time = 0.0
	state.tag_order = 0
	state.hp = sim.rules.player_max_hp
	state.position = state.spawn_position
	state.facing = Vector2(0.0, -sim.side_for_team(state.team))
	clear_actions(state)
	sim.refill_stamina(state)
	sim.player_rescued.emit(state.id, by_id)


## Status effects, cast locks, weapon aiming and the walk to a move target end on a tag or rescue.
## Cooldowns and elixir keep running.
func clear_actions(state: PlayerState) -> void:
	state.cast_lock_left = 0.0
	state.effects.clear()
	state.aim_hold.fill(-1.0)
	state.has_move_target = false
	state.stuck_time = 0.0


## How many free players of `team` stand in the enemy base (training dummies never count).
func capturers_in_enemy_base(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> int:
	var enemy_side: int = -sim.side_for_team(team)
	var count: int = 0
	for state: PlayerState in _capture_members(sim, team, brains):
		if not state.tagged and sim.layout.in_base(enemy_side, state.position):
			count += 1
	return count


## Every player of `team` free and standing in the enemy base.
func team_in_enemy_base(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> bool:
	var members: Array[PlayerState] = _capture_members(sim, team, brains)
	if members.is_empty():
		return false
	var enemy_side: int = -sim.side_for_team(team)
	for state: PlayerState in members:
		if state.tagged or not sim.layout.in_base(enemy_side, state.position):
			return false
	return true


## Every player of the other team waiting in a box (a team of training dummies
## never scores this way).
func all_enemies_tagged(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> bool:
	var members: Array[PlayerState] = _capture_members(sim, 1 - team, brains)
	if members.is_empty() or _capture_members(sim, team, brains).is_empty():
		return false
	for state: PlayerState in members:
		if not state.tagged:
			return false
	return true


## A free player of the team that owns the base `team` is invading stands in that base.
func base_defended(sim: MatchSim, team: int) -> bool:
	var enemy_side: int = -sim.side_for_team(team)
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team != team and not other.tagged and sim.layout.in_base(enemy_side, other.position):
			return true
	return false


## `team` has players in the enemy base but a defender still stands there, so the capture cannot run.
func base_contested_by(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> bool:
	return sim.rules.capture_contested and capturers_in_enemy_base(sim, team, brains) > 0 and base_defended(sim, team)


## The capture countdown runs while exactly one team keeps a capture going (at least one free player in the enemy base with
## nobody defending it, or every enemy tagged); it restarts as soon as that breaks. A base capture runs faster with every
## teammate in the base (`capture_solo_time`, `capture_ally_bonus`). When both teams qualify at once (a mirror rush) nobody's
## countdown runs: the tie is broken by tagging, not by which team has the lower number.
func check_captures(sim: MatchSim, brains: Dictionary[int, DummyBrain], dt: float) -> void:
	var team: int = -1
	var kind: MatchSim.CaptureKind = MatchSim.CaptureKind.NONE
	var running: int = 0
	var in_base: int = 0
	for candidate: int in [0, 1]:
		var how: MatchSim.CaptureKind = MatchSim.CaptureKind.NONE
		var here: int = capturers_in_enemy_base(sim, candidate, brains)
		if here > 0 and not base_contested_by(sim, candidate, brains):
			how = MatchSim.CaptureKind.BASE
			in_base = here
		elif all_enemies_tagged(sim, candidate, brains):
			how = MatchSim.CaptureKind.ALL_TAGGED
		if how != MatchSim.CaptureKind.NONE:
			running += 1
			team = candidate
			kind = how
	if running != 1:
		team = -1
		kind = MatchSim.CaptureKind.NONE
	if team != sim.capture_team or kind != sim.capture_kind:
		sim.capture_team = team
		sim.capture_kind = kind
		sim.capture_elapsed = 0.0
	sim.capture_rate = 1.0
	if team < 0:
		return
	if kind == MatchSim.CaptureKind.BASE:
		sim.capture_needed = sim.current_base_capture_time()
		sim.capture_rate = 1.0 + float(in_base - 1) * sim.rules.capture_ally_bonus
	sim.capture_elapsed += dt * sim.capture_rate
	if sim.capture_elapsed >= sim.capture_needed:
		_score_point(sim, team, kind)


func capture_time_left(sim: MatchSim) -> float:
	if sim.capture_team < 0:
		return 0.0
	return maxf(0.0, sim.capture_needed - sim.capture_elapsed) / maxf(sim.capture_rate, 0.001)


func end_freeze(sim: MatchSim) -> void:
	_reset_round(sim)
	sim.phase = MatchSim.Phase.PLAYING
	sim.point_reset.emit()


## The players a capture needs: everyone on `team` except training dummies.
func _capture_members(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> Array[PlayerState]:
	var result: Array[PlayerState] = []
	for id: int in sim.players:
		if sim.players[id].team == team and not brains.has(id):
			result.append(sim.players[id])
	return result


func _score_point(sim: MatchSim, team: int, how: MatchSim.CaptureKind) -> void:
	var result: MatchScore.Result = sim.score.award_point(team)
	sim.point_log.append(Vector2i(team, how))
	_clear_capture(sim)
	for id: int in sim.players:
		sim.end_move(sim.players[id])  # the point freeze: everyone stops where they are
	sim.point_scored.emit(team, how)
	if result == MatchScore.Result.MATCH:
		sim.phase = MatchSim.Phase.MATCH_OVER
		sim.match_won.emit(team)
		return
	sim.phase = MatchSim.Phase.POINT_FREEZE
	sim.freeze_left = sim.rules.point_freeze_time


func _clear_capture(sim: MatchSim) -> void:
	sim.capture_team = -1
	sim.capture_kind = MatchSim.CaptureKind.NONE
	sim.capture_elapsed = 0.0


## Everyone back to their post at full HP, out of the boxes, cooldowns and effects
## cleared; weapon objects are removed.
func _reset_round(sim: MatchSim) -> void:
	sim.weapons.reset()
	_clear_capture(sim)
	sim.round_time = 0.0
	sim.capture_needed = sim.rules.capture_time
	sim.rescue_rescuer = [-1, -1]
	sim.rescue_elapsed = [0.0, 0.0]
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		var was_tagged: bool = state.tagged
		clear_actions(state)
		state.position = state.spawn_position
		state.facing = Vector2(0.0, -sim.side_for_team(state.team))
		state.hp = sim.rules.player_max_hp
		state.tagged = false
		state.tag_time = 0.0
		state.tag_order = 0
		sim.refill_stamina(state)
		if sim.rules.elixir_reset_on_round and sim.pays_elixir(state):
			state.elixir = sim.rules.elixir_start
		sim.finish_reloads(state)
		if was_tagged:
			sim.player_rescued.emit(id, MatchSim.RELEASED_ALONE)
