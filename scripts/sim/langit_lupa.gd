class_name LangitLupa
extends TayaGame
## Langit Lupa (docs/GDD.md "Langit Lupa"): one taya chases everyone else over the whole street. A kid standing on a langit ("heaven": a
## raised platform, or a mound it raised itself) is safe, but only for `langit_seconds` in all, refilled on the lupa (the street). The
## platforms wear out under the kids' feet and break until the next round; standing on one never refills stamina; and now and then a
## brownout leaves each kid seeing only the kids near it. A tagged kid is the new taya and the round resets. Every kid but the taya earns
## a point for each `survive_seconds` it stays free, the tagger a bonus; the most points win when the time is up.

## `id` raised a mound (where it stands is in `mounds`: an event goes to every phone, and in a brownout not every phone may know).
signal mound_raised(id: int)
## The mound of `id` sank back into the street.
signal mound_fell(id: int)
## Platform `index` (of `rules.langit_spots`) wore out and broke.
signal spot_broke(index: int)
## `new_taya` took the "it" from `old_taya` (tagged).
signal taya_changed(old_taya: int, new_taya: int)

## A mound is raised this far inside the street's edges (m).
const MOUND_MARGIN: float = 0.6

## Platform index -> kid-seconds of standing left before it breaks (0: broken, until the next round).
var wear_left: Dictionary[int, float] = {}
## id -> where its mound stands, and seconds until it sinks.
var mounds: Dictionary[int, Vector2] = {}
var mound_left: Dictionary[int, float] = {}
## id -> seconds until it can raise the next mound.
var mound_wait: Dictionary[int, float] = {}
## id -> seconds of safety left on a langit (drains there, refills on the street).
var safe_left: Dictionary[int, float] = {}
## id -> seconds until its next survival point.
var survive_left: Dictionary[int, float] = {}
## A client's copy only: the kids this phone may not see now (the server left them out of its snapshot during a brownout).
var unseen: Dictionary[int, bool] = {}

var _lupa_down: Dictionary[int, bool] = {}
## True on a client's copy (it has been written from a snapshot): who it may see is the server's call (`unseen`), never its own guess.
var _copy: bool = false


func synced_fields() -> Array[StringName]:
	var fields: Array[StringName] = super()
	fields.append_array([&"wear_left", &"mounds", &"mound_left", &"mound_wait", &"safe_left", &"survive_left"])
	return fields


## A client's copy also learns which kids it may not see (absent: it sees everyone).
func restore(data: Dictionary, sim: MatchSim = null) -> void:
	super(data, sim)
	_copy = true
	unseen.clear()
	var hidden: Variant = data.get("unseen")
	if hidden is Array:
		for id: Variant in hidden as Array:
			if id is int:
				unseen[id as int] = true


func setup(sim: MatchSim) -> void:
	super.setup(sim)
	_sync_stamina(sim)


## Only the kids who are not the taya tire (the taya never does).
func _sync_stamina(sim: MatchSim) -> void:
	for state: PlayerState in sim.players.values():
		state.has_stamina = state.id != taya_id
		sim.refill_stamina(state)


# ---- heaven and earth ----------------------------------------------------------------------

## The platform under `position` that still stands, or -1.
func spot_at(position: Vector2) -> int:
	for index: int in rules.langit_spots.size():
		if wear_left.get(index, 0.0) > 0.0 and rules.langit_spots[index].has_point(position):
			return index
	return -1


## True while platform `index` still stands.
func spot_standing(index: int) -> bool:
	return wear_left.get(index, 0.0) > 0.0


## How worn platform `index` is (0 new to 1 broken).
func wear_share(index: int) -> float:
	if rules.langit_wear_seconds <= 0.0:
		return 0.0
	return 1.0 - clampf(wear_left.get(index, 0.0) / rules.langit_wear_seconds, 0.0, 1.0)


## The kid whose mound stands under `position`, or -1.
func mound_at(position: Vector2) -> int:
	for id: int in mounds:
		if mounds[id].distance_to(position) <= rules.mound_radius:
			return id
	return -1


## True while `state` stands on a langit (a platform or a mound), safe or not.
func on_langit(state: PlayerState) -> bool:
	return spot_at(state.position) >= 0 or mound_at(state.position) >= 0


## True while the langit under `state` saves it from the taya (its safe time is not used up).
func is_safe(state: PlayerState) -> bool:
	return is_attacker(state) and on_langit(state) and safe_left.get(state.id, 0.0) > 0.0


## How much safe time `id` has left (0 to 1).
func safe_share(id: int) -> float:
	return clampf(safe_left.get(id, 0.0) / rules.langit_seconds, 0.0, 1.0) if rules.langit_seconds > 0.0 else 0.0


func is_taggable(state: PlayerState) -> bool:
	return is_attacker(state) and not state.tagged and not is_safe(state)


func can_rest(state: PlayerState) -> bool:
	return not (rules.langit_blocks_rest and on_langit(state))


## True when `state` could raise a mound now: not the taya, free, able to move, its last mound's wait is over, and it has safe time left
## (a mound raised with none would save nothing and waste the wait).
func can_raise(state: PlayerState) -> bool:
	return is_attacker(state) and not state.tagged and state.effects.can_move() and not is_resetting() \
		and mound_wait.get(state.id, 0.0) <= 0.0 and safe_left.get(state.id, 0.0) > 0.0


## How ready the next mound of `id` is (0 just raised to 1 ready).
func mound_ready_share(id: int) -> float:
	if rules.mound_cooldown <= 0.0:
		return 1.0
	return 1.0 - clampf(mound_wait.get(id, 0.0) / rules.mound_cooldown, 0.0, 1.0)


## The Lupa button raises a mound under the kid (pressed, not held).
func handle_input(sim: MatchSim, state: PlayerState, input: PlayerInput, _dt: float) -> void:
	var pressed: bool = input != null and input.is_pressed(PlayerInput.BTN_CARD_2)
	if pressed and not _lupa_down.get(state.id, false) and can_raise(state):
		raise_mound(sim, state)
	_lupa_down[state.id] = pressed


## A mound of earth rises under `state` (its old one, if any, sinks first).
func raise_mound(sim: MatchSim, state: PlayerState) -> void:
	if mounds.has(state.id):
		mound_fell.emit(state.id)
	var at: Vector2 = sim.layout.keep_in_street(state.position, MOUND_MARGIN)
	mounds[state.id] = at
	mound_left[state.id] = rules.mound_seconds
	mound_wait[state.id] = rules.mound_cooldown
	sim.weapon_cast.emit(state.id, &"langit_lupa")
	mound_raised.emit(state.id)


# ---- the brownout --------------------------------------------------------------------------

## Seconds of play so far (the match clock counts down from `match_seconds`).
func elapsed() -> float:
	return rules.match_seconds - time_left


## True while the street is dark.
func is_brownout() -> bool:
	return brownout_left() > 0.0


## Seconds the brownout still lasts (0 when the lights are on).
func brownout_left() -> float:
	if rules.brownout_seconds <= 0.0 or time_left <= 0.0:
		return 0.0
	var since: float = elapsed() - rules.brownout_first
	if since < 0.0:
		return 0.0
	var into: float = fmod(since, rules.brownout_every) if rules.brownout_every > 0.0 else since
	return maxf(0.0, rules.brownout_seconds - into)


## True when the kid `viewer` can see `target` now: always, except in a brownout beyond `brownout_sight` (or, on a client's copy, a kid
## the server left out).
func visible_to(sim: MatchSim, viewer: int, target: int) -> bool:
	if viewer == target or not is_brownout():
		return true
	if _copy:
		return not unseen.has(target)  # the server already measured, on the real positions
	var a: PlayerState = sim.players.get(viewer) as PlayerState
	var b: PlayerState = sim.players.get(target) as PlayerState
	if a == null or b == null:
		return true
	return a.position.distance_to(b.position) <= rules.brownout_sight


func hidden_from(sim: MatchSim, viewer: int) -> Array[int]:
	var hidden: Array[int] = []
	if not is_brownout() or not sim.players.has(viewer):
		return hidden
	for id: int in sim.players:
		if not visible_to(sim, viewer, id):
			hidden.append(id)
	return hidden


## A viewer's copy of the game's data: the mounds of the kids it cannot see are left out (they would say where those kids stand).
func filter_for(data: Dictionary, hidden: Array[int]) -> Dictionary:
	var out: Dictionary = data.duplicate()
	for field: String in ["mounds", "mound_left"]:
		var table: Dictionary = (out.get(field, {}) as Dictionary).duplicate()
		for id: int in hidden:
			table.erase(id)
		out[field] = table
	return out


# ---- one tick ------------------------------------------------------------------------------

func _step_game(sim: MatchSim, dt: float) -> void:
	for id: int in mound_wait.keys():
		mound_wait[id] = maxf(0.0, mound_wait[id] - dt)
	for id: int in mound_left.keys():
		mound_left[id] -= dt
		if mound_left[id] <= 0.0:
			mounds.erase(id)
			mound_left.erase(id)
			mound_fell.emit(id)
	for state: PlayerState in sim.players.values():
		if not is_attacker(state) or state.tagged:
			continue
		_step_kid(sim, state, dt)
		if sim.phase != MatchSim.Phase.PLAYING:
			return
	check_tags(sim)


## The safe time of a kid drains on a langit (wearing the platform) and refills on the street; it earns its survival points.
func _step_kid(sim: MatchSim, state: PlayerState, dt: float) -> void:
	var id: int = state.id
	var spot: int = spot_at(state.position)
	if spot >= 0 or mound_at(state.position) >= 0:
		safe_left[id] = maxf(0.0, safe_left.get(id, 0.0) - dt)
		if spot >= 0 and rules.langit_wear_seconds > 0.0:
			wear_left[spot] = maxf(0.0, wear_left[spot] - dt)
			if wear_left[spot] <= 0.0:
				spot_broke.emit(spot)
	else:
		safe_left[id] = minf(rules.langit_seconds, safe_left.get(id, 0.0) + rules.langit_refill_per_s * dt)
	if rules.survive_seconds <= 0.0:
		return
	survive_left[id] = survive_left.get(id, rules.survive_seconds) - dt
	if survive_left[id] <= 0.0:
		survive_left[id] += rules.survive_seconds
		award(sim, state, false)


## The taya touched a kid off a langit: the tagger gets its bonus and the kid is the new taya; the round resets.
func _tag(sim: MatchSim, guard: PlayerState, runner: PlayerState) -> void:
	sim.weapon_cast.emit(guard.id, TAG_CAST)
	tagged.emit(guard.id, runner.id)
	if rules.tag_points > 0:
		points[guard.id] = points.get(guard.id, 0) + rules.tag_points
		scored.emit(guard.team, guard.id)
	var old: int = taya_id
	taya_id = runner.id
	taya_changed.emit(old, runner.id)
	roles_swapped.emit(runner.id)
	begin_reset(sim)


func _new_round(sim: MatchSim) -> void:
	super._new_round(sim)
	_sync_stamina(sim)


## Every platform stands again, the mounds sink, every kid's safe time and survival clock start full.
func _reset_game(sim: MatchSim) -> void:
	wear_left.clear()
	for index: int in rules.langit_spots.size():
		wear_left[index] = rules.langit_wear_seconds if rules.langit_wear_seconds > 0.0 else 1.0  # (0: it never wears)
	for id: int in mounds:
		mound_fell.emit(id)
	mounds.clear()
	mound_left.clear()
	mound_wait.clear()
	safe_left.clear()
	survive_left.clear()
	for id: int in sim.players:
		safe_left[id] = rules.langit_seconds
		survive_left[id] = rules.survive_seconds


## The taya starts in the middle of the street; everyone else on a ring round it, in the order of their ids.
func _spot(sim: MatchSim, state: PlayerState) -> Vector2:
	if state.id == taya_id:
		return Vector2.ZERO
	var others: Array[int] = []
	for id: int in sim.players:
		if id != taya_id:
			others.append(id)
	var slot: int = others.find(state.id)
	var angle: float = TAU * float(slot) / float(maxi(others.size(), 1)) - PI / 2.0
	return sim.layout.keep_in_street(Vector2.from_angle(angle) * rules.ring_radius, state.radius)
