class_name TickBlend
extends RefCounted
## View only: the sim moves kids and flying things in 30 Hz steps, so a 60 or 120 Hz screen would move them every second or fourth
## frame. This draws them a little behind the sim (never more than one tick), gliding between the positions of its last two ticks.
## The sim, the hitboxes and the online snapshots never see it.

## A move bigger than this in one tick is a change of place (tagged, rescued, a new round), not a run: it is drawn as it is.
const SNAP_DISTANCE: float = 3.0


class Track extends RefCounted:
	var before: Vector2
	var after: Vector2


## How far the screen is between the last two ticks (0 = the older, 1 = the newer one).
var weight: float = 1.0
var _kids: Dictionary[int, Track] = {}
var _shots: Dictionary[int, Track] = {}


## Call after every sim step.
func record(sim: MatchSim) -> void:
	for id: int in _kids.keys():
		if not sim.players.has(id):
			_kids.erase(id)
	for id: int in sim.players:
		_push(_kids, id, (sim.players[id] as PlayerState).position)
	var live: Dictionary[int, bool] = {}
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		live[projectile.id] = true
		_push(_shots, projectile.id, projectile.position)
	for id: int in _shots.keys():
		if not live.has(id):
			_shots.erase(id)


## The time left over after the last tick, as a share of one tick.
func set_progress(leftover: float, tick_dt: float) -> void:
	weight = clampf(leftover / maxf(tick_dt, 0.0001), 0.0, 1.0)


func kid(state: PlayerState) -> Vector2:
	return _glide(_kids.get(state.id) as Track, state.position)


func shot(projectile: WeaponSystem.Projectile) -> Vector2:
	return _glide(_shots.get(projectile.id) as Track, projectile.position)


func _push(tracks: Dictionary[int, Track], id: int, position: Vector2) -> void:
	var track: Track = tracks.get(id) as Track
	if track == null:
		track = Track.new()
		track.after = position
		tracks[id] = track
	track.before = track.after
	track.after = position


## Not blended when the thing is new, was moved outside a tick (online snapshots place it every frame) or jumped.
func _glide(track: Track, now: Vector2) -> Vector2:
	if track == null or track.after != now or track.before.distance_squared_to(now) > SNAP_DISTANCE * SNAP_DISTANCE:
		return now
	return track.before.lerp(now, weight)
