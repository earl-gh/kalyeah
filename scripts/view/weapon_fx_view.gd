class_name WeaponFxView
extends Node3D
## Draws the sim's weapon effects: it owns the shared pools and context and hands each kind of sim object to one controller
## (projectiles, zones, walls, cones, heals, hit bursts). Read-only: every frame it reads WeaponSystem state and the relayed
## signals; nothing here changes a rule, a timing or a hit (docs/ART.md "Skill effects").

## Landing circles and the cone flash say who threw them (TeamColors, yours or theirs) from their first frame: an opaque rim
## (`FX_RIM` m wide) round a see-through fill.
const FX_RIM: float = FxKit.RIM
const FX_RIM_ALPHA: float = FxKit.RIM_ALPHA
const FX_FILL_ALPHA: float = FxKit.FILL_ALPHA
const CONE_FLASH_TIME: float = FxCones.CONE_FLASH_TIME
## Turns per second of a thrown object (the spinning top spins fast).
const SPIN: float = FxProjectiles.SPIN
const TOP_SPIN: float = FxProjectiles.TOP_SPIN
## The hit burst on a kid (FxBursts): `BURST_STARS` comic stars, no ring.
const BURST_STARS: int = FxBursts.STARS


## How much bigger than real thrown objects are drawn (CameraRules.kid_view_scale), like the kids who throw them.
var view_scale: float = 1.0
## The viewer's team: effects thrown by it are drawn in your team's colour, the rest in theirs.
var local_team: int = 0
var projectiles: FxProjectiles
var zones: FxZones
var walls: FxWalls
var cones: FxCones
var heals: FxHeals
## The first frames' GL warm-up (FxWarmup).
var warmup: FxWarmup
var _ctx: FxContext


## Effect views in the tree: when the last one leaves (the match ends), every static cache of the effects is emptied, so
## nothing keeps meshes, materials or model scenes alive in the menus or at exit.
static var _live_views: int = 0


func _init() -> void:
	_ctx = FxContext.new()
	_ctx.ground = FxGround.new()
	_ctx.ground.name = "Ground"
	_ctx.shadows = FxShadows.new()
	_ctx.shadows.name = "Shadows"
	_ctx.particles = FxParticles.new()
	_ctx.particles.name = "Particles"
	_ctx.bursts = FxBursts.new()
	projectiles = FxProjectiles.new()
	zones = FxZones.new()
	walls = FxWalls.new()
	cones = FxCones.new()
	heals = FxHeals.new()
	for node: Node3D in [_ctx.ground, _ctx.shadows, _ctx.particles, projectiles, zones, walls, cones, heals]:
		add_child(node)
	_ctx.ground.setup(_ctx)
	_ctx.shadows.setup(_ctx)
	_ctx.particles.setup(_ctx)
	_ctx.bursts.setup(_ctx)
	projectiles.setup(_ctx)
	zones.setup(_ctx)
	walls.setup(_ctx)
	cones.setup(_ctx)
	heals.setup(_ctx)
	projectiles.gone.connect(_on_projectile_gone)
	warmup = FxWarmup.new()
	warmup.setup(_ctx, self)


func _notification(what: int) -> void:
	if what == NOTIFICATION_PREDELETE and _ctx != null:
		# the context and the hit bursts point at each other (both RefCounted): break the loop so neither, nor the sim
		# they read, outlives the view
		_ctx.bursts = null
		_ctx.sim = null


func _enter_tree() -> void:
	_live_views += 1


func _exit_tree() -> void:
	_live_views = maxi(_live_views - 1, 0)
	if _live_views == 0:
		clear_caches()


## Empties every static cache of the effects (the kit, the marks, the particle presets, the shadow quad, the weapon models'
## scenes and looks, the scissors' blades).
static func clear_caches() -> void:
	FxKit.clear_cache()
	FxGround.clear_cache()
	FxParticles.clear_cache()
	FxShadows.clear_cache()
	WeaponModel.clear_cache()
	ScissorsRig.clear_cache()


## Starts listening to `sim`'s relayed weapon signals (the same offline and online).
func watch(sim: MatchSim) -> void:
	_ctx.sim = sim
	_copy_settings()
	sim.weapons.cone_struck.connect(cones.on_struck)
	sim.weapons.cone_winding.connect(cones.on_winding)
	sim.player_damaged.connect(_ctx.bursts.hit)
	sim.player_healed.connect(heals.on_healed)
	sim.weapon_cast.connect(_on_cast)
	sim.effect_applied.connect(projectiles.on_effect)


## What every controller reads (tests and tools).
func context() -> FxContext:
	return _ctx


## Sim-bound objects drawn right now (projectiles, zones, walls).
func live_count() -> int:
	return projectiles.live_count() + zones.live_count() + walls.live_count()


## Delayed-cone warnings on the ground right now.
func windup_count() -> int:
	return cones.windup_count()


## Hit bursts playing right now.
func burst_count() -> int:
	return _ctx.bursts.count()


## Effects still playing after their sim object (ghosts, flashes, crosses).
func fading_count() -> int:
	return projectiles.fading_count() + zones.fading_count() + walls.fading_count() + cones.fading_count() + heals.fading_count()


## Particles alive right now.
func particle_count() -> int:
	return _ctx.particles.live_particles()


## Your team's colour for an effect thrown by `team`, else the other team's.
func team_color(team: int) -> Color:
	return _ctx.team_color(team)


func sync(sim: MatchSim, delta: float) -> void:
	_ctx.sim = sim
	_ctx.delta = delta
	_ctx.time += delta
	_copy_settings()
	warmup.step(sim)
	projectiles.sync(sim)
	zones.sync(sim)
	walls.sync(sim)
	var hits: Dictionary[int, int] = _ctx.bursts.flush()
	cones.resolve(hits)
	for id: int in hits:
		var kid: PlayerState = sim.players.get(id) as PlayerState
		if kid != null:
			projectiles.react_hit(id, kid.position)
			zones.on_tick_hit(id, kid.position)
	heals.flush()
	cones.step()
	heals.step()
	_ctx.particles.step()
	_ctx.ground.step()
	_ctx.forget_old()


func _copy_settings() -> void:
	_ctx.view_scale = view_scale
	_ctx.local_team = local_team
	_ctx.reduced = Settings.reduced_motion


## A projectile left the sim: a wall of the other team it stopped at reacts.
func _on_projectile_gone(_id: int, def: WeaponDef, team: int, at: Vector3) -> void:
	walls.on_projectile_gone(def, team, at)


func _on_cast(id: int, weapon_id: StringName) -> void:
	_ctx.note_cast(id, weapon_id)
	heals.on_cast(id, weapon_id)
