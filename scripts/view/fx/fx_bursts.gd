class_name FxBursts
extends RefCounted
## The hit burst on a kid who took damage: one per kid per frame (every `player_damaged` of that kid in the frame is summed,
## so six snips make one burst, not six stacked), a sari-yellow ring on the ground that grows from half a metre to one metre
## (never bigger than the smallest hit area, never times the view scale) and four comic stars beside the kid at waist height.
## A tick of damage (a jacks field) gets two stars and no ring; a kid ringed a moment ago gets only stars. Orange is not used:
## it is the colour-blind enemy colour. With Reduce motion the ring holds still at its end size and nothing flies.

## The ring grows from `RING_FROM` to `RING_TO` metres (absolute) over `RING_FRAMES` frames, fading from `RING_ALPHA`.
const RING_FROM: float = 0.5
const RING_TO: float = 1.0
const RING_FRAMES: int = 8
const RING_TIME: float = float(RING_FRAMES) / 30.0
const RING_ALPHA: float = 0.9
const COLOR: Color = Palette.SARI_YELLOW
## Stars of a burst, and of a tick or a merged hit.
const STARS: int = 4
const TICK_STARS: int = 2
## Summed damage at or under this is a tick (stars only).
const TICK_DAMAGE: int = 3
## A kid ringed within this many seconds gets only stars (the light slipper's hits, a field).
const MERGE_TIME: float = 0.1
## Bursts playing at once at most; the oldest gives way.
const MAX_LIVE: int = 12
## How long a stars-only burst counts as playing (the stars' life, seconds).
const STARS_TIME: float = 0.3


## A burst playing.
class Live extends RefCounted:
	var end: float
	var ring: int = -1


var _ctx: FxContext
var _damage: Dictionary[int, int] = {}
var _hits: Dictionary[int, int] = {}
var _out: Dictionary[int, int] = {}
var _ringed_at: Dictionary[int, float] = {}
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
		var merged: bool = _ringed_at.has(id) and _ctx.time - _ringed_at[id] < MERGE_TIME
		if _damage[id] <= TICK_DAMAGE or merged:
			if _ctx.particles.puff(FxParticles.Preset.STARS, ground, TICK_STARS):
				_add(STARS_TIME, -1)
			continue
		_ringed_at[id] = _ctx.time
		_ctx.particles.puff(FxParticles.Preset.STARS, ground, STARS)
		_add(RING_TIME, _ctx.ground.ring(at, RING_FROM, RING_TO, Color(COLOR, RING_ALPHA), RING_TIME, _ctx.reduced))
	_damage.clear()
	_hits.clear()
	return _out


## Bursts playing right now (rings and stars-only bursts).
func count() -> int:
	var playing: int = 0
	for live: Live in _live:
		playing += 1 if live.end > _ctx.time else 0
	return playing


func _add(life: float, ring: int) -> void:
	while _live.size() >= MAX_LIVE:
		_ctx.ground.drop_ring(_live.pop_front().ring)
	var live: Live = Live.new()
	live.end = _ctx.time + life
	live.ring = ring
	_live.append(live)
