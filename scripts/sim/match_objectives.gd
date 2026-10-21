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
	clear_actions(state)
	state.position = sim.layout.jail_slot(sim.side_for_team(state.team), waiting)
	state.facing = Vector2(0.0, sim.side_for_team(state.team))
	sim.player_tagged.emit(state.id)


## A free teammate stepping into the rescue area round the box (or the box) frees whoever
## waits there (everyone, or one per touch when `rescue_frees_all` is off).
func check_rescues(sim: MatchSim) -> void:
	for id: int in sim.players:
		var rescuer: PlayerState = sim.players[id]
		if rescuer.tagged:
			continue
		var box: Rect2 = sim.layout.rescue_rect(sim.side_for_team(rescuer.team)).grow(rescuer.radius)
		if not box.has_point(rescuer.position):
			continue
		for other_id: int in sim.players:
			var waiting: PlayerState = sim.players[other_id]
			if waiting.team == rescuer.team and waiting.tagged:
				rescue(sim, waiting, rescuer.id)
				if not sim.rules.rescue_frees_all:
					break


## Back to the post at full HP.
func rescue(sim: MatchSim, state: PlayerState, by_id: int) -> void:
	state.tagged = false
	state.tag_time = 0.0
	state.hp = sim.rules.player_max_hp
	state.position = state.spawn_position
	state.facing = Vector2(0.0, -sim.side_for_team(state.team))
	clear_actions(state)
	sim.player_rescued.emit(state.id, by_id)


## Status effects, cast locks and weapon aiming end on a tag or rescue.
## Cooldowns keep running.
func clear_actions(state: PlayerState) -> void:
	state.cast_lock_left = 0.0
	state.effects.clear()
	state.aim_hold = [-1.0, -1.0]


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


## `team` is all in the enemy base but a defender still stands there, so the capture cannot run.
func base_contested_by(sim: MatchSim, team: int, brains: Dictionary[int, DummyBrain]) -> bool:
	return sim.rules.capture_contested and team_in_enemy_base(sim, team, brains) and base_defended(sim, team)


## The capture countdown runs while exactly one team keeps a capture going (whole team in the enemy base with
## nobody defending it, or every enemy tagged); it restarts as soon as that breaks. When both teams qualify at
## once (a mirror rush) nobody's countdown runs: the tie is broken by tagging, not by which team has the lower number.
func check_captures(sim: MatchSim, brains: Dictionary[int, DummyBrain], dt: float) -> void:
	var team: int = -1
	var kind: MatchSim.CaptureKind = MatchSim.CaptureKind.NONE
	var running: int = 0
	for candidate: int in [0, 1]:
		var how: MatchSim.CaptureKind = MatchSim.CaptureKind.NONE
		if team_in_enemy_base(sim, candidate, brains) and not base_contested_by(sim, candidate, brains):
			how = MatchSim.CaptureKind.BASE
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
	if team < 0:
		return
	sim.capture_elapsed += dt
	if sim.capture_elapsed >= sim.capture_needed:
		_score_point(sim, team, kind)


func capture_time_left(sim: MatchSim) -> float:
	if sim.capture_team < 0:
		return 0.0
	return maxf(0.0, sim.capture_needed - sim.capture_elapsed)


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
	_clear_capture(sim)
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
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		var was_tagged: bool = state.tagged
		clear_actions(state)
		state.position = state.spawn_position
		state.facing = Vector2(0.0, -sim.side_for_team(state.team))
		state.hp = sim.rules.player_max_hp
		state.tagged = false
		state.tag_time = 0.0
		for slot: int in state.weapon_cooldowns.size():
			state.weapon_cooldowns[slot] = 0.0
		if was_tagged:
			sim.player_rescued.emit(id, MatchSim.RELEASED_ALONE)
