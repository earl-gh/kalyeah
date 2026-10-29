class_name FxWarmup
extends RefCounted
## The effect view's GL warm-up, owned by WeaponFxView (Android: a phone compiles a material's program the first time it
## is drawn, which would stall the first throw of a fight).

## Warm-up: in its first `WARM_FRAMES` frames in the tree the view draws one invisible (alpha 0) instance of every effect
## material (dust, scraps and chips particles, a ring, a flash disc, a shadow) where the camera sees it, and builds the lazy
## meshes, so a phone compiles their GL programs now and not on the first throw of the fight. The shadow uses key
## `WARM_KEY` (no sim object has a negative id).
const WARM_FRAMES: int = 2
const WARM_KEY: int = -1
var _warm_left: int = WARM_FRAMES
var _warm_ring: int = -1
var _ctx: FxContext
var _view: WeaponFxView


func setup(ctx: FxContext, view: WeaponFxView) -> void:
	_ctx = ctx
	_view = view


## True once the warm-up has run.
func warmed() -> bool:
	return _warm_left < WARM_FRAMES


## Runs the warm-up in the view's first frames in the tree; then every pool is idle again.
func step(sim: MatchSim) -> void:
	if _warm_left <= 0 or not _view.is_inside_tree():
		return
	if _warm_left == WARM_FRAMES:
		_warm(sim)
	_warm_left -= 1
	if _warm_left == 0:
		_ctx.particles.release_clear()
		_ctx.ground.drop_ring(_warm_ring)
		_ctx.shadows.detach(WARM_KEY)


func _warm(sim: MatchSim) -> void:
	# building the shared meshes now (each is made on first ask)
	var meshes: Array[ArrayMesh] = [FxKit.ring_mesh(), FxKit.disc_mesh(), FxKit.star_quad(), FxKit.plus_quad(), FxKit.flat_quad(), FxKit.puff_quad(), FxKit.smear_arc(), FxKit.smear_streak(), FxKit.crack_spokes()]
	meshes.clear()
	for priority: int in [FxKit.PRIO_FILL, FxKit.PRIO_RIM, FxKit.PRIO_SHADOW, FxKit.PRIO_DECAL]:
		FxKit.overlay(priority)
	ScissorsRig.halves()
	var spot: Vector2 = Vector2.ZERO
	for id: int in sim.players:
		spot = sim.players[id].position
		break
	var clear: Color = Color(1.0, 1.0, 1.0, 0.0)
	var at: Vector3 = Vector3(spot.x, 0.0, spot.y)
	for preset: FxParticles.Preset in [FxParticles.Preset.DUST, FxParticles.Preset.SCRAPS, FxParticles.Preset.CHIPS]:
		_ctx.particles.puff(preset, at, 2, clear)
	_warm_ring = _ctx.ground.ring(spot, 0.1, 0.1, clear, WARM_FRAMES * FxSquash.FRAME)
	_ctx.ground.flash_disc(spot, 0.1, clear, 0.0, 0.0, FxSquash.FRAME)
	if _ctx.shadows.attach(WARM_KEY, 0.1, _view.local_team):
		_ctx.shadows.place(WARM_KEY, spot, 0.0)
