class_name FxBursts
extends RefCounted
## The hit burst on a kid who took damage: one per kid per frame (every `player_damaged` of that kid in the frame is summed, so six snips
## make one burst, not six stacked): four comic stars beside the kid at waist height. There is no ring on the ground (owner), and nothing
## is drawn over the kid. A tick of damage (a jacks field) or a kid burst a moment ago gets two stars. With Reduce motion nothing flies.

## Stars of a burst, and of a tick or a merged hit.
const STARS: int = 4
const TICK_STARS: int = 2
## Summed damage at or under this is a tick (stars only).
const TICK_DAMAGE: int = 3
## A kid that burst within this many seconds gets only the small burst (the light slipper's hits, a field).
const MERGE_TIME: float = 0.1
## Bursts playing at once at most; the oldest gives way.
const MAX_LIVE: int = 12
## How long a stars-only burst counts as playing (the stars' life, seconds).
const STARS_TIME: float = 0.3


## A burst playing.
class Live extends RefCounted:
	var end: float


var _ctx: FxContext
var _damage: Dictionary[int, int] = {}
var _hits: Dictionary[int, int] = {}
var _out: Dictionary[int, int] = {}
var _burst_at: Dictionary[int, float] = {}
var _live: Array[Live] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## Kid `id` took `amount` damage (queued: drawn once per kid at `flush`).
func hit(id: int, amount: int) -> void:
	_damage[id] = _damage.get(id, 0) + amount
	_hits[id] = _hits.get(id, 0) + 1


## Draws this frame's bursts and returns how many hits each kid took this frame (kid id -> hits; reused, read it now).
func flush() -> Dictionary[int, int]:
	while not _live.is_empty() and _live[0].end <= _ctx.time:
		_live.pop_front()
	_out.clear()
	for id: int in _damage:
		_out[id] = _hits[id]
		if _ctx.sim == null or not _ctx.sim.players.has(id):
			continue
		var at: Vector2 = _ctx.sim.players[id].position
		var ground: Vector3 = Vector3(at.x, 0.0, at.y)
		var merged: bool = _burst_at.has(id) and _ctx.time - _burst_at[id] < MERGE_TIME
		var small: bool = _damage[id] <= TICK_DAMAGE or merged
		if not small:
			_burst_at[id] = _ctx.time
		if _ctx.particles.puff(FxParticles.Preset.STARS, ground, TICK_STARS if small else STARS):
			_add(STARS_TIME)
	_damage.clear()
	_hits.clear()
	return _out


## Bursts playing right now.
func count() -> int:
	var playing: int = 0
	for live: Live in _live:
		playing += 1 if live.end > _ctx.time else 0
	return playing


func _add(life: float) -> void:
	while _live.size() >= MAX_LIVE:
		_live.pop_front()
	var live: Live = Live.new()
	live.end = _ctx.time + life
	_live.append(live)
