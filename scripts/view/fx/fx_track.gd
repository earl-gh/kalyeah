class_name FxTrack
extends RefCounted
## What the effects know about one sim object from seeing it frame after frame (works the same offline and online, where
## a snapshot carries only the position): when and where it was first seen, where it came from, which way it moves and
## how fast, and the frame it turned back (a boomerang slipper on its way home).

## Moves shorter than this (metres) are ignored for the direction (a snapshot that did not move the object).
const MIN_MOVE: float = 0.01
## A new direction this far against the old one (dot product) is a turn-back.
const TURN_DOT: float = -0.5

var first_seen: float = 0.0
var first_pos: Vector2 = Vector2.ZERO
## Where the object came from: its thrower's position at the cast when known, else where it was first seen.
var origin: Vector2 = Vector2.ZERO
var prev_pos: Vector2 = Vector2.ZERO
var last_pos: Vector2 = Vector2.ZERO
## Unit direction of the last real move (zero until it moved).
var dir: Vector2 = Vector2.ZERO
## Metres per second over the last real move.
var speed: float = 0.0
## True only on the update where the direction reversed.
var flipped: bool = false
## How many times it reversed so far.
var flips: int = 0
var _seen: bool = false
var _last_move_time: float = 0.0


## Records the object at `pos` at view time `now`.
func see(pos: Vector2, now: float) -> void:
	flipped = false
	if not _seen:
		_seen = true
		first_seen = now
		first_pos = pos
		origin = pos
		prev_pos = pos
		last_pos = pos
		_last_move_time = now
		return
	var delta: Vector2 = pos - last_pos
	if delta.length() <= MIN_MOVE:
		return
	var new_dir: Vector2 = delta.normalized()
	if dir != Vector2.ZERO and new_dir.dot(dir) < TURN_DOT:
		flipped = true
		flips += 1
	speed = delta.length() / maxf(now - _last_move_time, 0.001)
	dir = new_dir
	prev_pos = last_pos
	last_pos = pos
	_last_move_time = now


## Seconds since it was first seen.
func age(now: float) -> float:
	return now - first_seen
