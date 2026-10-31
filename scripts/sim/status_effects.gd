class_name StatusEffects
extends RefCounted
## Status effects on one player (docs/GDD.md "Weapons"). Only ONE status is active
## at a time. Each accepted application replaces the old status, timer and magnitude.

enum Type { NONE, STUN, SLOW, AIRBORNE, POLYMORPH, BOUNCE, KNOCKOUT, TRIPPED }

const DISPLAY_NAMES: Dictionary[int, String] = {
	Type.STUN: "Stunned",
	Type.SLOW: "Slowed",
	Type.AIRBORNE: "Airborne",
	Type.POLYMORPH: "Morphed",
	Type.BOUNCE: "Bounced",
	Type.KNOCKOUT: "Knocked out",
	Type.TRIPPED: "Tripped",
}

## Effects that stop movement and casting.
## (Tripped, a fall while running, is one too, but it never gives the immunity below: it is not a weapon's hit.)
const HARD_CC: Array[Type] = [Type.STUN, Type.AIRBORNE, Type.BOUNCE, Type.KNOCKOUT, Type.TRIPPED]

## What `cc_immunity` guards against: everything that stops moving or casting, polymorph included.
const IMMUNITY_TYPES: Array[Type] = [Type.STUN, Type.AIRBORNE, Type.BOUNCE, Type.KNOCKOUT, Type.POLYMORPH]

## Seconds of immunity to a new hard crowd control once one has run out (0 = none); set by the match from the rules.
var immunity_length: float = 0.0

var _time_left: Dictionary[int, float] = {}
var _slow: float = 0.0
var _immune_left: float = 0.0


## Returns false when nothing was applied (no effect, no time, or the player is still immune to a new hard CC).
func apply(type: Type, duration: float, magnitude: float = 0.0) -> bool:
	if type == Type.NONE or duration <= 0.0:
		return false
	if IMMUNITY_TYPES.has(type) and _immune_left > 0.0 and not _time_left.has(type):
		return false
	_time_left.clear()
	_slow = 0.0
	_time_left[type] = duration
	if type == Type.SLOW:
		_slow = magnitude
	return true


func step(dt: float) -> void:
	_immune_left = maxf(0.0, _immune_left - dt)
	for type: int in _time_left.keys():
		_time_left[type] -= dt
		if _time_left[type] <= 0.0:
			_time_left.erase(type)
			if type == Type.SLOW:
				_slow = 0.0
			elif IMMUNITY_TYPES.has(type as Type):
				_immune_left = immunity_length


func clear() -> void:
	_time_left.clear()
	_slow = 0.0
	_immune_left = 0.0


func has(type: Type) -> bool:
	return _time_left.has(type)


func time_left(type: Type) -> float:
	return _time_left[type] if _time_left.has(type) else 0.0


func is_hard_cc(type: Type) -> bool:
	return HARD_CC.has(type)


func can_move() -> bool:
	for type: Type in HARD_CC:
		if _time_left.has(type):
			return false
	return true


## Polymorph still lets you walk, but not cast.
func can_cast() -> bool:
	return can_move() and not _time_left.has(Type.POLYMORPH)


func speed_multiplier(polymorph_speed_scale: float) -> float:
	var multiplier: float = 1.0
	if _time_left.has(Type.SLOW):
		multiplier *= 1.0 - _slow
	if _time_left.has(Type.POLYMORPH):
		multiplier *= polymorph_speed_scale
	return multiplier


## Names of active effects, for HUD tags (e.g. "STUN").
## Player-facing words for the active effects ("Stunned", "Slowed", ...).
func display_names() -> PackedStringArray:
	var words: PackedStringArray = []
	for type: int in _time_left:
		words.append(DISPLAY_NAMES.get(type, "") as String)
	return words


func active_names() -> PackedStringArray:
	var names: PackedStringArray = []
	for type: int in _time_left:
		names.append(Type.keys()[type])
	return names


## Plain data for network snapshots: {type: time_left, "slow": magnitude}.
func snapshot() -> Dictionary:
	var data: Dictionary = {}
	for type: int in _time_left:
		data[type] = _time_left[type]
	data["slow"] = _slow
	return data


func restore(data: Dictionary) -> void:
	_time_left.clear()
	_slow = data.get("slow", 0.0) as float
	for key: Variant in data:
		if key is int:
			_time_left[key as int] = data[key] as float
