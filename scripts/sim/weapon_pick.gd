class_name WeaponPick
extends RefCounted
## The weapon pick (docs/GDD.md "Weapon pick"): two different weapons within
## `GameRules.weapon_pick_time`. Tapping a third weapon replaces the older pick.
## When time runs out the missing picks are filled at random. Plain data, so the
## server and offline practice share it.

const SLOTS: int = 2

var slots: int = SLOTS
var time_left: float = 0.0
## The pick time it started with (for countdown displays).
var time_total: float = 0.0
var picks: Array[StringName] = []
var done: bool = false

var _available: Array[StringName] = []


## `current` pre-selects a loadout (the respawn swap keeps your superpowers by default).
## `p_slots` defaults to `SLOTS` (2) for legacy callers and accepts `3` for 5v5 3-superpower loadouts.
func _init(available: Array[StringName], time: float, current: Array[StringName] = [], p_slots: int = SLOTS) -> void:
	_available = available.duplicate()
	slots = maxi(p_slots, 1)
	time_left = time
	time_total = time
	for id: StringName in current:
		if _available.has(id) and not picks.has(id) and picks.size() < slots:
			picks.append(id)


## A pair for a player who does not know the weapons yet: an attack that aims itself (a homing throw) and the first
## block, or failing that the first crowd control. Empty if the arsenal has no such pair. Only a suggestion: the
## picker still lets anyone choose any two.
static func suggestion(defs: Array[WeaponDef]) -> Array[StringName]:
	var attack: WeaponDef = null
	var support: WeaponDef = null
	var fallback: WeaponDef = null
	for def: WeaponDef in defs:
		if attack == null and def.kind == WeaponDef.Kind.ATTACK and def.shape == WeaponDef.Shape.TARGETED:
			attack = def
		elif support == null and def.kind == WeaponDef.Kind.BLOCK:
			support = def
		elif fallback == null and def.kind == WeaponDef.Kind.CROWD_CONTROL:
			fallback = def
	if support == null:
		support = fallback
	var found: Array[StringName] = []
	if attack != null and support != null:
		found.append(attack.id)
		found.append(support.id)
	return found


## Suggests `count` unique superpowers: delegates to `suggestion` when `count <= SLOTS`, and for 3+ slots
## recommends Attack (homing throw preferred, else first Attack) + Block + Crowd Control, filling any missing
## slot with remaining unique entries from `defs`.
static func suggestion_for(defs: Array[WeaponDef], count: int) -> Array[StringName]:
	if count <= SLOTS:
		return suggestion(defs)
	var attack: WeaponDef = null
	var fallback_attack: WeaponDef = null
	var block_def: WeaponDef = null
	var crowd_def: WeaponDef = null
	for def: WeaponDef in defs:
		if attack == null and def.kind == WeaponDef.Kind.ATTACK and def.shape == WeaponDef.Shape.TARGETED:
			attack = def
		elif fallback_attack == null and def.kind == WeaponDef.Kind.ATTACK:
			fallback_attack = def
		elif block_def == null and def.kind == WeaponDef.Kind.BLOCK:
			block_def = def
		elif crowd_def == null and def.kind == WeaponDef.Kind.CROWD_CONTROL:
			crowd_def = def
	if attack == null:
		attack = fallback_attack
	var found: Array[StringName] = []
	if attack == null:
		return found
	found.append(attack.id)
	if block_def != null and not found.has(block_def.id) and found.size() < count:
		found.append(block_def.id)
	if crowd_def != null and not found.has(crowd_def.id) and found.size() < count:
		found.append(crowd_def.id)
	for def: WeaponDef in defs:
		if found.size() >= count:
			break
		if not found.has(def.id):
			found.append(def.id)
	if found.size() < count:
		found.clear()
	return found


## True when none of `ids` can hurt anyone (no damage on any of them): a pair of traps, shields and heals only
## slows, blocks and heals. Allowed (open decision D10), but the picker says so. `defs` maps id to definition.
static func deals_no_damage(ids: Array[StringName], defs: Dictionary[StringName, WeaponDef]) -> bool:
	if ids.is_empty():
		return false
	for id: StringName in ids:
		var def: WeaponDef = defs.get(id) as WeaponDef
		if def == null or def.damage > 0:
			return false
	return true


func toggle(id: StringName) -> void:
	if done or not _available.has(id):
		return
	if picks.has(id):
		picks.erase(id)
		return
	if picks.size() >= slots:
		picks.pop_front()
	picks.append(id)


func can_confirm() -> bool:
	return not done and picks.size() == slots


func confirm() -> bool:
	if not can_confirm():
		return false
	done = true
	return true


## Counts down; on timeout fills the empty slots and finishes. Returns `done`.
func step(dt: float, rng: RandomNumberGenerator) -> bool:
	if done:
		return true
	time_left = maxf(time_left - dt, 0.0)
	if time_left <= 0.0:
		fill(rng)
		done = true
	return done


func fill(rng: RandomNumberGenerator) -> void:
	var left: Array[StringName] = []
	for id: StringName in _available:
		if not picks.has(id):
			left.append(id)
	while picks.size() < slots and not left.is_empty():
		picks.append(left.pop_at(rng.randi_range(0, left.size() - 1)))


## Deliberately replace a selected socket; the available/unique constraints still apply.
func replace_slot(slot: int, id: StringName) -> bool:
	if done or slot < 0 or slot >= picks.size() or not _available.has(id) or picks.has(id):
		return false
	picks[slot] = id
	return true
