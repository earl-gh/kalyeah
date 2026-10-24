class_name WeaponCasting
extends RefCounted
## Authoritative card-button arbitration and cast-on-release state machine (docs/GDD.md "Controls: tap to move, cards to cast").
## A player can hold only one slot at a time; simultaneous presses prefer the lowest slot. The release casts: at the aim point sent
## with it (clamped by the sim to the range from where the kid is), else the legacy stick aim, else the auto-aim taken at the press.

## Hand slot -> its PlayerInput bit.
const CARD_BITS: Array[int] = PlayerInput.CARD_BITS
## Superseded name (two weapon buttons), kept for callers that index slot 0 and 1.
const WEAPON_BITS: Array[int] = PlayerInput.CARD_BITS


## Advances one player's weapon input. Releasing an active aim consumes the tick,
## so another slot needs a fresh press on a later input before it can start.
func handle(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float) -> void:
	var active_slot: int = _active_slot(state)
	if active_slot >= 0:
		_step_active(sim, state, input, dt, active_slot)
	else:
		_start_fresh_aim(sim, state, input)
	state.previous_buttons = input.buttons


## A weapon slot can start aiming: equipped, off cooldown, and casting allowed.
func weapon_ready(state: PlayerState, slot: int) -> bool:
	return (
		not state.tagged and state.effects.can_cast()
		and slot >= 0 and slot < state.weapons.size() and state.weapons[slot] != &""
		and slot < state.weapon_cooldowns.size()
		and state.weapon_cooldowns[slot] <= 0.0
	)


## The aim a release would use: the stick if it is past the deadzone, otherwise the
## auto-aim locked at the press (or a live auto-aim if the slot is not held yet).
func resolved_aim(sim: MatchSim, state: PlayerState, slot: int, stick: Vector2) -> Vector2:
	if stick.length() >= sim.rules.aim_deadzone:
		return stick.limit_length(1.0)
	if state.aim_hold[slot] >= 0.0:
		return state.aim_lock[slot]
	return sim.weapons.auto_aim(sim, state, sim.weapon_defs[state.weapons[slot]])


## The target a TARGETED weapon would throw at (locked at the press while held).
func resolved_target(sim: MatchSim, state: PlayerState, slot: int) -> int:
	if state.aim_hold[slot] >= 0.0:
		return state.aim_target[slot]
	var target: PlayerState = sim.nearest_enemy(state, sim.weapon_defs[state.weapons[slot]].max_range)
	return target.id if target != null else -1


## Finds the sole active aim. If malformed state contains more than one, the
## lower slot wins deterministically and every later slot is cancelled.
func _active_slot(state: PlayerState) -> int:
	var active: int = -1
	for slot: int in state.aim_hold.size():
		if state.aim_hold[slot] < 0.0:
			continue
		if active < 0:
			active = slot
		else:
			state.aim_hold[slot] = -1.0
	return active


func _step_active(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float, slot: int) -> void:
	var bit: int = CARD_BITS[slot]
	if input.is_pressed(bit):
		if state.tagged or not state.effects.can_cast() or sim.casting_blocked(state):
			state.aim_hold[slot] = -1.0
		else:
			state.aim_hold[slot] += dt
		return
	if (state.previous_buttons & bit) != 0:
		if not input.is_pressed(PlayerInput.BTN_AIM_CANCEL):
			_try_fire(sim, state, slot, input, state.aim_hold[slot])
	state.aim_hold[slot] = -1.0


func _start_fresh_aim(sim: MatchSim, state: PlayerState, input: PlayerInput) -> void:
	for slot: int in mini(CARD_BITS.size(), state.aim_hold.size()):
		var bit: int = CARD_BITS[slot]
		var fresh_press: bool = input.is_pressed(bit) and (state.previous_buttons & bit) == 0
		if not fresh_press:
			continue
		# an unaffordable card cannot be picked up (no precasting: holding it while the bar fills does nothing)
		if sim.weapon_ready(state, slot):
			state.aim_hold[slot] = 0.0
			_lock_aim(sim, state, slot)
		# The lowest freshly pressed slot owns this input even when it was not ready.
		break


func _lock_aim(sim: MatchSim, state: PlayerState, slot: int) -> void:
	var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
	state.aim_lock[slot] = sim.weapons.auto_aim(sim, state, def)
	var target: PlayerState = sim.nearest_enemy(state, def.max_range)
	state.aim_target[slot] = target.id if target != null else -1


func _try_fire(sim: MatchSim, state: PlayerState, slot: int, input: PlayerInput, hold_seconds: float) -> void:
	if not state.can_act() or not sim.weapon_ready(state, slot):
		return
	var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
	var aim: Vector2
	var target_id: int = state.aim_target[slot]
	if input.is_pressed(PlayerInput.BTN_AIM_POINT) and input.aim_point.is_finite():
		aim = sim.aim_from_point(state, def, input.aim_point)
		if def.shape == WeaponDef.Shape.TARGETED:
			target_id = sim.target_near_point(state, def, input.aim_point)
	else:
		aim = resolved_aim(sim, state, slot, input.aim if input.aim.is_finite() else Vector2.ZERO)
	if not sim.weapons.fire(sim, state, def, aim, hold_seconds, target_id):
		return  # nothing was cast: the card keeps its slot and nothing is spent
	if sim.pays_elixir(state):
		state.elixir = maxf(0.0, state.elixir - float(def.elixir_cost))
	else:
		state.weapon_cooldowns[slot] = def.cooldown
	sim.cycle_used(state, slot)
	state.cast_lock_left = def.cast_lock
	if def.cast_lock > 0.0:
		sim.end_move(state)  # a heavy roots the kid and ends its walk
	sim.weapon_cast.emit(state.id, def.id)
