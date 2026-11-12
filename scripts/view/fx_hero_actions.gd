class_name FxHeroActions
extends Node3D
## Sunlit 90s Philippine province gameplay VFX powered by node-based `VisualShader` resources
## (`shaders/visual/*.tres`) + `arcane_fx_master.gdshader` + `CPUParticles3D` (`gl_compatibility` mobile-safe):
## 1. Impact Shockwaves (`vs_impact_shockwave.tres`): eroding radial energy ring + Fresnel shock skirt for `Bagsak` & heavy hits.
## 2. Projectile Ribbons (`vs_projectile_ribbon.tres`): scrolling tapered UV streak + hot core / team-tinted edge glow for
##    `Tirador`, `Tsinelas`, `Sumpit`, `Holen`/`bato_light`, `Teks`, `Jolens`/`bola`, and `Turumpo`/`trumpo`.
## 3. Canal Water Ripples (`vs_water_ripple.tres`): concentric *Tabing-Ilog* splash rings + foam dissolve + aqua droplet puffs.
## 4. Merienda Stand Auras (`vs_merienda_aura.tres`): soft rising golden/cyan Fresnel shimmer cylinders + floating motes on the 4 stands.
## 5. Painterly Dust Puffs (`vs_dust_puff.tres` via `FxParticles`): footsteps/sprint, slides, jumps, and trips.
## Strictly read-only on `MatchSim`; all nodes and `ShaderMaterial`s are pre-allocated once in `setup()`.

const MASTER_SHADER: Shader = preload("res://shaders/arcane_fx_master.gdshader")
const VS_SHOCKWAVE: Shader = preload("res://shaders/visual/vs_impact_shockwave.tres")
const VS_RIBBON: Shader = preload("res://shaders/visual/vs_projectile_ribbon.tres")
const VS_WATER_RIPPLE: Shader = preload("res://shaders/visual/vs_water_ripple.tres")
const VS_MERIENDA_AURA: Shader = preload("res://shaders/visual/vs_merienda_aura.tres")

const P_TINT: StringName = &"tint"
const P_EDGE_COLOR: StringName = &"edge_color"
const P_RING_COLOR: StringName = &"ring_color"
const P_CORE_COLOR: StringName = &"core_color"
const P_RING_SHARPNESS: StringName = &"ring_sharpness"
const P_AQUA_COLOR: StringName = &"aqua_color"
const P_FOAM_COLOR: StringName = &"foam_color"
const P_RING_COUNT: StringName = &"ring_count"
const P_AURA_COLOR: StringName = &"aura_color"
const P_ACCENT_COLOR: StringName = &"accent_color"
const P_PULSE_PHASE: StringName = &"pulse_phase"
const P_PHASE: StringName = &"phase"
const P_SOFT_FADE: StringName = &"soft_fade"
const P_FRESNEL: StringName = &"fresnel_softness"
const P_SUNLIT: StringName = &"sunlit_lift"
const P_SCROLL: StringName = &"scroll_speed"

## 17 pooled ShaderMaterials total (75 base + 17 = 92 <= 96 pool budget; 96 after warm-up).
const RING_SLOTS: int = 2
const SHOCK_SLOTS: int = 2
const STREAK_SLOTS: int = 4
const RIPPLE_SLOTS: int = 3
const AURA_SLOTS: int = 4
const BURST_SLOTS: int = 2

const CANAL_HALF_Z: float = 3.75
const WADE_INTERVAL_WALK: float = 0.24
const WADE_INTERVAL_FAST: float = 0.15
const WADE_RIPPLE_LIFE: float = 0.38
const CANAL_SPLASH_LIFE: float = 0.44
const JUMP_RING_LIFE: float = 0.26
const POWERUP_HALO_LIFE: float = 0.44
const HEAVY_HIT_SHOCK_LIFE: float = 0.32

const BAGSAK_INNER_ALPHA: float = 0.06
const BAGSAK_RIM_WIDTH: float = 0.14
const BAGSAK_INK_WIDTH: float = 0.05
const BAGSAK_RING_LIFE: float = 0.52
const BAGSAK_SHOCK_LIFE: float = 0.36
const TIRADOR_ARC_LIFE: float = 0.18
const TIRADOR_STREAK_FADE: float = 0.08
const DASH_WIND_LIFE: float = 0.25
const DASH_CHALK_LIFE: float = 0.32
const SPRINT_INTERVAL: float = 0.26
const SPRINT_TICK_LIFE: float = 0.16

const AURA_RADIUS: float = 0.68
const AURA_HEIGHT: float = 1.44
const MOTE_AMOUNT: int = 6
const MOTE_LIFETIME: float = 1.10

const INK_ESPRESSO: Color = Color(0.20, 0.15, 0.14, 0.90)
const AMBER_CORE: Color = Color(0.98, 0.72, 0.22, 0.95)
const CYAN_SPARK: Color = Color(0.32, 0.88, 0.92, 0.96)
const CHALK_WARM: Color = Color(0.92, 0.86, 0.74, 0.54)
const WIND_SUNLIT: Color = Color(0.84, 0.96, 0.95, 0.88)
const BAGSAK_GOLD: Color = Color(0.98, 0.78, 0.24, 0.96)
const BAGSAK_ALLY_FILL: Color = Color(1.0, 0.90, 0.56, 1.0)
const SHOCK_HOT_CORE: Color = Color(1.0, 0.97, 0.86, 1.0)
const SPRINT_DUST_TINT: Color = Color(0.90, 0.84, 0.72, 0.62)
const TRIP_DUST_TINT: Color = Color(0.92, 0.84, 0.70, 0.78)
const CANAL_AQUA: Color = Color(0.26, 0.82, 0.78, 0.92)
const CANAL_FOAM: Color = Color(0.94, 0.99, 0.97, 0.95)
const POWERUP_JADE: Color = Color(0.24, 0.90, 0.84, 0.94)
const POWERUP_GOLD: Color = Color(1.0, 0.82, 0.24, 0.96)
const POWERUP_TURON: Color = Color(1.0, 0.68, 0.20, 0.96)

const RIBBON_WEAPON_IDS: Dictionary[StringName, bool] = {
	&"tirador_bato": true,
	&"tsinelas_light": true,
	&"tsinelas_heavy": true,
	&"sumpit": true,
	&"bato_light": true,
	&"holen": true,
	&"teks": true,
	&"bola": true,
	&"jolens": true,
	&"trumpo": true,
	&"turumpo": true,
}


class Slot extends RefCounted:
	var node: MeshInstance3D
	var material: ShaderMaterial
	var active: bool = false
	var warm: bool = false
	var born: float = 0.0
	var life: float = 0.25
	var proj_id: int = -1
	var base_pos: Vector3 = Vector3.ZERO
	var base_scale: Vector3 = Vector3.ONE
	var grow: float = 0.0
	var rise_y: float = 0.0


class ZoneWatch extends RefCounted:
	var center: Vector2 = Vector2.ZERO
	var radius: float = 2.0
	var team: int = 0
	var born: float = 0.0
	var weapon_id: StringName = &""


static var _meshes: Dictionary[String, ArrayMesh] = {}

var _ctx: FxContext
var _zones: FxZones
var _rings: Array[Slot] = []
var _shockwaves: Array[Slot] = []
var _streaks: Array[Slot] = []
var _ripples: Array[Slot] = []
var _auras: Array[Slot] = []
var _bursts: Array[Slot] = []
var _aura_motes: Array[CPUParticles3D] = []
var _warming: bool = false
var _seen_shots: Dictionary[int, Vector2] = {}
var _shot_dirs: Dictionary[int, Vector2] = {}
var _ult_zones: Dictionary[int, ZoneWatch] = {}
var _heavy_zones: Dictionary[int, ZoneWatch] = {}
var _prev_dash: Dictionary[int, float] = {}
var _prev_pos: Dictionary[int, Vector2] = {}
var _prev_air_y: Dictionary[int, float] = {}
var _prev_slide: Dictionary[int, float] = {}
var _prev_buff: Dictionary[int, float] = {}
var _prev_tripped: Dictionary[int, bool] = {}
var _last_dash_fx: Dictionary[int, float] = {}
var _last_ult_fx: Dictionary[int, float] = {}
var _last_powerup_fx: Dictionary[int, float] = {}
var _sprint_due: Dictionary[int, float] = {}
var _wade_due: Dictionary[int, float] = {}
var _step_serial: int = 0


func setup(ctx: FxContext, zones_view: FxZones = null) -> void:
	_ctx = ctx
	_zones = zones_view
	_rings = _make_master_pool(RING_SLOTS, FxKit.PRIO_RIM, FxKit.flat_quad(), 0.0, "BagsakRing")
	_shockwaves = _make_visual_pool(SHOCK_SLOTS, VS_SHOCKWAVE, FxKit.PRIO_DECAL, _shockwave_mesh(2.4), "Shockwave")
	_streaks = _make_visual_pool(STREAK_SLOTS, VS_RIBBON, FxKit.PRIO_DECAL, _ribbon_streak_mesh(), "ProjectileRibbon")
	_ripples = _make_visual_pool(RIPPLE_SLOTS, VS_WATER_RIPPLE, FxKit.PRIO_DECAL, _water_ripple_mesh(false), "WaterRipple")
	_auras = _make_visual_pool(AURA_SLOTS, VS_MERIENDA_AURA, FxKit.PRIO_DECAL, _merienda_cylinder_mesh(), "MeriendaAura")
	_bursts = _make_master_pool(BURST_SLOTS, FxKit.PRIO_DECAL, _tirador_arcs_mesh(false), 0.10, "HeroBurst")
	_build_aura_motes()


static func clear_cache() -> void:
	_meshes.clear()


static func cached_count() -> int:
	return _meshes.size()


func hero_scale() -> float:
	if _ctx == null:
		return 0.55
	if is_equal_approx(_ctx.view_scale, 2.0):
		return 1.0
	return clampf(_ctx.view_scale / 1.65, 0.50, 1.05)


func warm_slot(at: Vector3) -> void:
	_warming = true
	var now: float = _ctx.time if _ctx != null else 0.0
	_warm_one_pool(_rings, at, now)
	_warm_one_pool(_shockwaves, at, now)
	_warm_one_pool(_streaks, at, now)
	_warm_one_pool(_ripples, at, now)
	_warm_one_pool(_auras, at, now)
	_warm_one_pool(_bursts, at, now)


static func _warm_one_pool(pool: Array[Slot], at: Vector3, now: float) -> void:
	if pool.is_empty():
		return
	var slot: Slot = pool[0]
	slot.active = true
	slot.warm = true
	slot.proj_id = -1
	slot.born = now
	slot.life = 1.0
	slot.node.position = at
	slot.node.scale = Vector3.ONE * 0.08
	slot.material.set_shader_parameter(P_SOFT_FADE, 0.0)
	slot.node.visible = true


func drop_warm() -> void:
	_warming = false
	for slot: Slot in _rings:
		_reset_warm_slot(slot, true)
	for slot: Slot in _shockwaves:
		_reset_warm_slot(slot, false)
	for slot: Slot in _streaks:
		_reset_warm_slot(slot, false)
	for slot: Slot in _ripples:
		_reset_warm_slot(slot, false)
	for slot: Slot in _auras:
		_reset_warm_slot(slot, false)
	for slot: Slot in _bursts:
		_reset_warm_slot(slot, true)


static func _reset_warm_slot(slot: Slot, is_master: bool) -> void:
	if not slot.warm:
		return
	slot.warm = false
	slot.active = false
	slot.node.visible = false
	slot.material.set_shader_parameter(P_SOFT_FADE, 1.0)
	if is_master:
		slot.material.set_shader_parameter(P_TINT, Color.WHITE)


func _make_master_pool(count: int, priority: int, default_mesh: ArrayMesh, fresnel: float, prefix: String) -> Array[Slot]:
	var pool: Array[Slot] = []
	for i: int in count:
		var slot: Slot = Slot.new()
		slot.node = MeshInstance3D.new()
		slot.node.name = "%s_%d" % [prefix, i]
		slot.node.mesh = default_mesh
		var mat: ShaderMaterial = ShaderMaterial.new()
		mat.shader = MASTER_SHADER
		mat.render_priority = priority
		mat.set_shader_parameter(P_TINT, Color.WHITE)
		mat.set_shader_parameter(P_PHASE, 0.0)
		mat.set_shader_parameter(P_SOFT_FADE, 1.0)
		mat.set_shader_parameter(P_FRESNEL, fresnel)
		mat.set_shader_parameter(P_SUNLIT, 0.08)
		mat.set_shader_parameter(P_SCROLL, 0.9)
		FxKit.count_material()
		slot.material = mat
		slot.node.material_override = mat
		slot.node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		slot.node.visible = false
		add_child(slot.node)
		pool.append(slot)
	return pool


func _make_visual_pool(count: int, shader_res: Shader, priority: int, default_mesh: ArrayMesh, prefix: String) -> Array[Slot]:
	var pool: Array[Slot] = []
	for i: int in count:
		var slot: Slot = Slot.new()
		slot.node = MeshInstance3D.new()
		slot.node.name = "%s_%d" % [prefix, i]
		slot.node.mesh = default_mesh
		var mat: ShaderMaterial = ShaderMaterial.new()
		mat.shader = shader_res
		mat.render_priority = priority
		mat.set_shader_parameter(P_SOFT_FADE, 1.0)
		if shader_res != VS_MERIENDA_AURA:
			mat.set_shader_parameter(P_PHASE, 0.0)
		else:
			mat.set_shader_parameter(P_PULSE_PHASE, float(i) * 1.57)
		FxKit.count_material()
		slot.material = mat
		slot.node.material_override = mat
		slot.node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		slot.node.visible = false
		add_child(slot.node)
		pool.append(slot)
	return pool


func _build_aura_motes() -> void:
	var shared_puff_mat: ShaderMaterial = FxKit.dust_puff_material()
	var mote_ramp: Gradient = Gradient.new()
	mote_ramp.offsets = PackedFloat32Array([0.0, 0.25, 0.78, 1.0])
	mote_ramp.colors = PackedColorArray([
		Color(1.0, 0.96, 0.72, 0.0),
		Color(1.0, 0.92, 0.48, 0.95),
		Color(0.38, 0.96, 0.90, 0.78),
		Color(0.38, 0.96, 0.90, 0.0),
	])
	for i: int in AURA_SLOTS:
		var mote: CPUParticles3D = CPUParticles3D.new()
		mote.name = "MeriendaMotes_%d" % i
		mote.emitting = false
		mote.one_shot = false
		mote.amount = MOTE_AMOUNT
		mote.lifetime = MOTE_LIFETIME
		mote.explosiveness = 0.0
		mote.fixed_fps = FxParticles.FIXED_FPS
		mote.local_coords = false
		mote.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mote.mesh = FxKit.puff_quad()
		mote.material_override = shared_puff_mat
		mote.emission_shape = CPUParticles3D.EMISSION_SHAPE_RING
		mote.emission_ring_axis = Vector3.UP
		mote.emission_ring_height = 0.25
		mote.emission_ring_radius = AURA_RADIUS * 0.88
		mote.emission_ring_inner_radius = AURA_RADIUS * 0.42
		mote.direction = Vector3.UP
		mote.spread = 18.0
		mote.initial_velocity_min = 0.42
		mote.initial_velocity_max = 0.76
		mote.gravity = Vector3.ZERO
		mote.scale_amount_min = 0.20
		mote.scale_amount_max = 0.34
		mote.color = Color.WHITE
		mote.color_ramp = mote_ramp
		mote.visible = false
		add_child(mote)
		_aura_motes.append(mote)


func _has_street_world() -> bool:
	var fx_parent: Node = get_parent()
	if fx_parent == null:
		return false
	var match_root: Node = fx_parent.get_parent()
	if match_root == null:
		return false
	return match_root.get_node_or_null("Map") != null


func on_player_dashed(id: int) -> void:
	if _ctx == null or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	var state: PlayerState = _ctx.sim.players[id]
	_last_dash_fx[id] = _ctx.time
	_spawn_dash_fx(state)


func on_ultimate_cast(id: int) -> void:
	if _ctx == null or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	var caster: PlayerState = _ctx.sim.players[id]
	var center: Vector2 = caster.position
	var radius: float = 8.5
	if _ctx.sim.rules != null and _ctx.sim.rules.ultimate_weapon != null:
		radius = _ctx.sim.rules.ultimate_weapon.radius
	for zone: WeaponSystem.Zone in _ctx.sim.weapons.zones:
		if zone.owner_id == id and zone.def != null and zone.def.id == &"ult_bagsak":
			center = zone.center
			radius = zone.radius
	_last_ult_fx[id] = _ctx.time
	_spawn_bagsak_cast(center, radius, caster.team)


func on_powerup_taken(id: int, _powerup_id: int = -1, kind: int = 0) -> void:
	if _ctx == null or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	var last_t: float = _last_powerup_fx[id] if _last_powerup_fx.has(id) else -10.0
	if _ctx.time - last_t < 0.15:
		return
	_last_powerup_fx[id] = _ctx.time
	var state: PlayerState = _ctx.sim.players[id]
	if not _ctx.reduced and not state.tagged:
		var hs: float = hero_scale()
		var eff_kind: int = kind if kind > 0 else int(_read_float(state, &"buff_type"))
		var aura_col: Color = _aura_primary_color(eff_kind)
		_activate_burst(_powerup_halo_mesh(eff_kind), Vector3(state.position.x, 0.08, state.position.y), _next_phase() * TAU, Vector3.ONE * hs, POWERUP_HALO_LIFE, 0.34, 0.22 * hs)
		_activate_shockwave(Vector2(state.position.x, state.position.y), 0.92 * hs, aura_col, SHOCK_HOT_CORE, 0.26, 0.08, 0.05 * hs)


func on_projectile_gone(def: WeaponDef, team: int, at: Vector3) -> void:
	if _ctx == null or def == null:
		return
	var is_skillshot: bool = def.shape == WeaponDef.Shape.SKILLSHOT
	if not is_skillshot and not (_has_street_world() and RIBBON_WEAPON_IDS.has(def.id)):
		return
	var hs: float = hero_scale()
	var turbo: bool = Settings.performance == &"turbo"
	var rim_col: Color = AMBER_CORE if team == _ctx.local_team else _ctx.team_color(team)
	if is_skillshot:
		if not _ctx.reduced:
			var yaw: float = _next_phase() * TAU
			var y_safe: float = maxf(at.y - 0.14 * hs, 0.26 * hs)
			_activate_burst(_tirador_arcs_mesh(true), Vector3(at.x, y_safe, at.z), yaw, Vector3.ONE * hs, TIRADOR_ARC_LIFE, 0.22, 0.06 * hs)
			if not turbo:
				_ctx.particles.puff(FxParticles.Preset.CHIPS, Vector3(at.x, 0.12 * hs, at.z), 2)
		_ctx.ground.ring(Vector2(at.x, at.z), 0.16 * hs, 0.52 * hs, Color(rim_col, 0.85), 0.16, _ctx.reduced)


func sync(sim: MatchSim) -> void:
	if _ctx == null:
		return
	var in_match_world: bool = _has_street_world()
	_sync_projectiles(sim, in_match_world)
	_sync_ult_zones(sim)
	if in_match_world:
		_sync_heavy_zones(sim)
	_sync_merienda_stands(sim, in_match_world)
	_sync_players(sim)
	_step_pool(_rings)
	_step_pool(_shockwaves)
	_step_pool(_streaks)
	_step_pool(_ripples)
	_step_pool(_bursts)


func active_ring_count() -> int:
	return _count_active(_rings)


func active_streak_count() -> int:
	return _count_active(_streaks)


func active_sketch_count() -> int:
	return _count_active(_bursts) + _count_active(_shockwaves) + _count_active(_ripples)


func _sync_projectiles(sim: MatchSim, in_match_world: bool) -> void:
	var live_ids: Dictionary[int, bool] = {}
	var hs: float = hero_scale()
	var flight_y: float = maxf(FxProjectiles.PROJECTILE_HEIGHT * _ctx.view_scale, 0.48)
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		if projectile.def == null:
			continue
		var wid: StringName = projectile.def.id
		var is_skillshot: bool = projectile.def.shape == WeaponDef.Shape.SKILLSHOT
		if not is_skillshot and not (in_match_world and RIBBON_WEAPON_IDS.has(wid)):
			continue
		var pid: int = projectile.id
		live_ids[pid] = true
		var drawn_pos: Vector2 = _ctx.shot_position(projectile)
		var dir: Vector2 = projectile.direction
		var is_new: bool = not _seen_shots.has(pid)
		if is_new:
			if dir.length_squared() < 0.0001 and sim.players.has(projectile.owner_id):
				var shooter: PlayerState = sim.players[projectile.owner_id]
				var delta_from_shooter: Vector2 = drawn_pos - shooter.position
				dir = delta_from_shooter.normalized() if delta_from_shooter.length_squared() > 0.001 else shooter.facing
			if dir.length_squared() < 0.0001:
				dir = Vector2(0.0, -1.0)
			_shot_dirs[pid] = dir.normalized()
			if is_skillshot and not _ctx.reduced:
				var yaw_rel: float = _yaw_from_dir(_shot_dirs[pid])
				_activate_burst(_tirador_arcs_mesh(false), Vector3(drawn_pos.x, maxf(flight_y - 0.18 * hs, 0.26 * hs), drawn_pos.y), yaw_rel, Vector3.ONE * hs, TIRADOR_ARC_LIFE, 0.18, 0.04 * hs)
		else:
			var prev_pos: Vector2 = _seen_shots[pid]
			var step_vec: Vector2 = drawn_pos - prev_pos
			if step_vec.length_squared() > 0.0004:
				_shot_dirs[pid] = step_vec.normalized()
			elif dir.length_squared() > 0.0001:
				_shot_dirs[pid] = dir.normalized()
		_seen_shots[pid] = drawn_pos
		if not _ctx.reduced:
			var use_dir: Vector2 = _shot_dirs[pid] if _shot_dirs.has(pid) else Vector2(0.0, -1.0)
			var ribbon_y: float = 0.26 * hs if wid == &"trumpo" or wid == &"turumpo" else flight_y
			_update_streak(pid, wid, projectile.team, Vector3(drawn_pos.x, ribbon_y, drawn_pos.y), _yaw_from_dir(use_dir), hs)

	for old_id: int in _seen_shots.keys():
		if not live_ids.has(old_id):
			_seen_shots.erase(old_id)
			_shot_dirs.erase(old_id)
	for slot: Slot in _streaks:
		if slot.active and not slot.warm and slot.proj_id >= 0 and not live_ids.has(slot.proj_id):
			slot.proj_id = -1
			if _ctx.reduced:
				slot.active = false
				slot.node.visible = false
			else:
				slot.born = _ctx.time
				slot.life = TIRADOR_STREAK_FADE


func _sync_ult_zones(sim: MatchSim) -> void:
	var seen_zones: Dictionary[int, bool] = {}
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		if zone.def == null or zone.def.id != &"ult_bagsak":
			continue
		seen_zones[zone.id] = true
		if not _ult_zones.has(zone.id):
			var watch: ZoneWatch = ZoneWatch.new()
			watch.center = zone.center
			watch.radius = zone.radius
			watch.team = zone.team
			watch.born = _ctx.time
			watch.weapon_id = zone.def.id
			_ult_zones[zone.id] = watch
			var recent_ult: float = _last_ult_fx[zone.owner_id] if _last_ult_fx.has(zone.owner_id) else -10.0
			if _ctx.time - recent_ult > 0.12:
				_spawn_bagsak_cast(zone.center, zone.radius, zone.team)
		# Keep FxZones' generic 50% Mark fill and 35% Progress disc hidden on ult_bagsak so our <= 0.06 ring owns floor clarity.
		if _zones != null:
			var znode: Node3D = _zones.node_for(zone.id)
			if znode != null:
				var mark: MeshInstance3D = znode.get_node_or_null("Mark") as MeshInstance3D
				if mark != null:
					mark.visible = false
				var prog: MeshInstance3D = znode.get_node_or_null("Progress") as MeshInstance3D
				if prog != null:
					prog.visible = false

	for zid: int in _ult_zones.keys():
		if not seen_zones.has(zid):
			var finished: ZoneWatch = _ult_zones[zid]
			_ult_zones.erase(zid)
			if _ctx.time - finished.born >= 0.18:
				_spawn_bagsak_slam(finished.center, finished.radius, finished.team)


func _sync_heavy_zones(sim: MatchSim) -> void:
	var seen_heavy: Dictionary[int, bool] = {}
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		if zone.def == null:
			continue
		var wid: StringName = zone.def.id
		if wid != &"bato_heavy" and wid != &"lata":
			continue
		seen_heavy[zone.id] = true
		if not _heavy_zones.has(zone.id):
			var watch: ZoneWatch = ZoneWatch.new()
			watch.center = zone.center
			watch.radius = zone.radius
			watch.team = zone.team
			watch.born = _ctx.time
			watch.weapon_id = wid
			_heavy_zones[zone.id] = watch

	for zid: int in _heavy_zones.keys():
		if not seen_heavy.has(zid):
			var landed: ZoneWatch = _heavy_zones[zid]
			_heavy_zones.erase(zid)
			if not _ctx.reduced and _ctx.time - landed.born >= 0.12:
				var own: bool = landed.team == _ctx.local_team
				var ring_col: Color = FxRelease.tint_for(landed.weapon_id) if own else _ctx.team_color(landed.team)
				_activate_shockwave(landed.center, maxf(landed.radius * 0.92, 1.4), ring_col, SHOCK_HOT_CORE, HEAVY_HIT_SHOCK_LIFE, 0.09)


func _sync_merienda_stands(sim: MatchSim, in_match_world: bool) -> void:
	if _warming:
		return
	var turbo: bool = Settings.performance == &"turbo"
	var stands_enabled: bool = in_match_world and not _ctx.reduced and sim != null and sim.mode == null
	var count: int = mini(sim.powerups.size(), AURA_SLOTS) if stands_enabled else 0
	for i: int in AURA_SLOTS:
		var slot: Slot = _auras[i]
		var mote: CPUParticles3D = _aura_motes[i]
		if i >= count:
			slot.active = false
			slot.node.visible = false
			if mote.emitting:
				mote.emitting = false
			mote.visible = false
			continue
		var entry: Dictionary = sim.powerups[i]
		var ready: bool = bool(entry.get("active", false))
		if not ready:
			slot.active = false
			slot.node.visible = false
			if mote.emitting:
				mote.emitting = false
			mote.visible = false
			continue
		var raw_pos: Variant = entry.get("pos", Vector2.ZERO)
		var stand_xz: Vector2 = (raw_pos as Vector2) if raw_pos is Vector2 else Vector2.ZERO
		var kind: int = int(entry.get("kind", 0))
		var primary: Color = _aura_primary_color(kind)
		var accent: Color = _aura_accent_color(kind)
		var breathe: float = 0.96 + 0.05 * sin(_ctx.time * 2.4 + float(i) * 1.57)
		slot.active = true
		slot.warm = false
		slot.node.position = Vector3(stand_xz.x, 0.03, stand_xz.y)
		slot.node.rotation = Vector3(0.0, _ctx.time * 0.35 + float(i) * 0.9, 0.0)
		slot.node.scale = Vector3(breathe, 1.0, breathe)
		slot.material.set_shader_parameter(P_AURA_COLOR, primary)
		slot.material.set_shader_parameter(P_ACCENT_COLOR, accent)
		slot.material.set_shader_parameter(P_PULSE_PHASE, float(i) * 1.57)
		slot.material.set_shader_parameter(P_SOFT_FADE, 0.92)
		slot.node.visible = true
		mote.position = Vector3(stand_xz.x, 0.14, stand_xz.y)
		if turbo:
			if mote.emitting:
				mote.emitting = false
			mote.visible = false
		else:
			mote.visible = true
			if not mote.emitting:
				mote.emitting = true


func _sync_players(sim: MatchSim) -> void:
	var turbo: bool = Settings.performance == &"turbo"
	var hs: float = hero_scale()
	var present: Dictionary[int, bool] = {}
	for id: int in sim.players:
		present[id] = true
		var state: PlayerState = sim.players[id]
		var pos: Vector2 = state.position
		var air_y: float = _read_float(state, &"air_y")
		var slide_left: float = _read_float(state, &"slide_left")
		var buff_left: float = _read_float(state, &"buff_left")
		var tripped_now: bool = state.effects != null and state.effects.has(StatusEffects.Type.TRIPPED)

		var first_seen: bool = not _prev_pos.has(id)
		var prev_p: Vector2 = _prev_pos[id] if not first_seen else pos
		var prev_air: float = _prev_air_y[id] if _prev_air_y.has(id) else air_y
		var prev_sl: float = _prev_slide[id] if _prev_slide.has(id) else slide_left
		var prev_bf: float = _prev_buff[id] if _prev_buff.has(id) else buff_left
		var was_tripped: bool = _prev_tripped[id] if _prev_tripped.has(id) else false
		_prev_pos[id] = pos
		_prev_air_y[id] = air_y
		_prev_slide[id] = slide_left
		_prev_buff[id] = buff_left
		_prev_tripped[id] = tripped_now

		var prev_d: float = _prev_dash[id] if _prev_dash.has(id) else 0.0
		_prev_dash[id] = state.dash_left
		var last_fx: float = _last_dash_fx[id] if _last_dash_fx.has(id) else -10.0
		if state.dash_left > prev_d + 0.02 and _ctx.time - last_fx > 0.12 and not state.tagged:
			_last_dash_fx[id] = _ctx.time
			_spawn_dash_fx(state)

		if first_seen or state.tagged:
			_sprint_due.erase(id)
			_wade_due.erase(id)
			continue

		if buff_left > prev_bf + 0.08:
			on_powerup_taken(id)

		var in_canal: bool = _in_walkable_canal(sim, pos)
		var step_vec: Vector2 = pos - prev_p
		var step_dist: float = step_vec.length()
		var move_dir: Vector2 = step_vec.normalized() if step_dist > 0.004 else (state.facing if state.facing.length_squared() > 0.001 else Vector2(0.0, -1.0))
		var move_yaw: float = _yaw_from_dir(move_dir)
		var started_jump: bool = prev_air <= 0.015 and air_y > 0.04
		var landed_jump: bool = prev_air > 0.10 and air_y <= 0.015
		var started_slide: bool = slide_left > prev_sl + 0.02
		var started_trip: bool = tripped_now and not was_tripped

		if started_trip and not _ctx.reduced and not turbo:
			if in_canal:
				_activate_water_ripple(pos, move_yaw, hs * 1.20, CANAL_SPLASH_LIFE, true)
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.02, pos.y), 4, CANAL_FOAM)
			else:
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 6, TRIP_DUST_TINT)

		if not _ctx.reduced and not turbo:
			if in_canal:
				if landed_jump or started_slide or started_jump:
					_wade_due[id] = _ctx.time + WADE_INTERVAL_FAST
					_activate_water_ripple(pos, move_yaw, hs * 1.15, CANAL_SPLASH_LIFE, true)
					_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.03, pos.y), 4, CANAL_AQUA)
				elif step_dist > 0.022 and air_y <= 0.05:
					var wade_due: float = _wade_due[id] if _wade_due.has(id) else 0.0
					if _ctx.time >= wade_due:
						var fast_wade: bool = state.sprinting or state.dash_left > 0.0 or slide_left > 0.0
						_wade_due[id] = _ctx.time + (WADE_INTERVAL_FAST if fast_wade else WADE_INTERVAL_WALK)
						_activate_water_ripple(pos, move_yaw, hs, WADE_RIPPLE_LIFE, false)
						if fast_wade:
							_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.02, pos.y), 2, CANAL_AQUA)
			else:
				_wade_due.erase(id)
				if started_slide:
					_activate_burst(_dash_chalk_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), move_yaw, Vector3.ONE * (hs * 0.78), DASH_CHALK_LIFE * 0.85, 0.12, 0.0)
					_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 4, SPRINT_DUST_TINT)
				elif landed_jump:
					_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 4, SPRINT_DUST_TINT)
				elif started_jump:
					_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 2, SPRINT_DUST_TINT)

		if state.sprinting and not in_canal and air_y <= 0.05 and not _ctx.reduced and not turbo:
			var due: float = _sprint_due[id] if _sprint_due.has(id) else 0.0
			if _ctx.time >= due:
				_sprint_due[id] = _ctx.time + SPRINT_INTERVAL
				var heel: Vector2 = pos - move_dir * (0.24 * hs)
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(heel.x, 0.0, heel.y), 2, SPRINT_DUST_TINT)
		else:
			_sprint_due.erase(id)

	for old_id: int in _prev_dash.keys():
		if not present.has(old_id):
			_prev_dash.erase(old_id)
			_prev_pos.erase(old_id)
			_prev_air_y.erase(old_id)
			_prev_slide.erase(old_id)
			_prev_buff.erase(old_id)
			_prev_tripped.erase(old_id)
			_last_dash_fx.erase(old_id)
			_last_ult_fx.erase(old_id)
			_last_powerup_fx.erase(old_id)
			_sprint_due.erase(old_id)
			_wade_due.erase(old_id)


static func _in_walkable_canal(sim: MatchSim, pos: Vector2) -> bool:
	if sim == null or sim.layout == null or not sim.layout.is_circular():
		return false
	var r_limit: float = maxf(sim.layout.arena_radius - 1.5, 0.0)
	if pos.length_squared() > r_limit * r_limit:
		return false
	if sim.layout.has_method(&"is_in_canal"):
		return bool(sim.layout.call(&"is_in_canal", pos))
	if sim.layout.has_method(&"in_canal"):
		return bool(sim.layout.call(&"in_canal", pos))
	var center_z: float = 0.0
	if sim.layout.has_method(&"z_creek"):
		center_z = float(sim.layout.call(&"z_creek", pos.x))
	elif sim.layout.has_method(&"canal_z"):
		center_z = float(sim.layout.call(&"canal_z", pos.x))
	elif sim.layout.has_method(&"canal_center_z"):
		center_z = float(sim.layout.call(&"canal_center_z", pos.x))
	return absf(pos.y - center_z) <= CANAL_HALF_Z


static func _read_float(obj: Object, prop: StringName) -> float:
	var val: Variant = obj.get(prop)
	if val is float:
		return val
	if val is int:
		return float(val)
	return 0.0


static func _aura_primary_color(kind: int) -> Color:
	if kind == MatchSim.POWERUP_PALAMIG:
		return POWERUP_JADE
	if kind == MatchSim.POWERUP_TURON:
		return POWERUP_TURON
	return POWERUP_GOLD


static func _aura_accent_color(kind: int) -> Color:
	if kind == MatchSim.POWERUP_PALAMIG:
		return POWERUP_GOLD
	if kind == MatchSim.POWERUP_TURON:
		return SHOCK_HOT_CORE
	return CYAN_SPARK


static func _ribbon_colors(wid: StringName, own_team: bool, team_col: Color) -> Array[Color]:
	var core_tint: Color = FxRelease.tint_for(wid)
	if wid == &"tirador_bato":
		core_tint = AMBER_CORE
	var edge_col: Color = CYAN_SPARK if own_team else Color(team_col.r, team_col.g, team_col.b, 1.0)
	return [Color(core_tint.r, core_tint.g, core_tint.b, 1.0), edge_col]


func _spawn_dash_fx(state: PlayerState) -> void:
	if _ctx.reduced:
		return
	var hs: float = hero_scale()
	var turbo: bool = Settings.performance == &"turbo"
	var dir: Vector2 = state.dash_dir if state.dash_dir.length_squared() > 0.001 else state.facing
	if dir.length_squared() < 0.001:
		dir = Vector2(0.0, -1.0)
	dir = dir.normalized()
	var yaw: float = _yaw_from_dir(dir)
	var pos: Vector2 = state.position
	var in_canal: bool = _in_walkable_canal(_ctx.sim, pos)
	if in_canal:
		_activate_water_ripple(pos, yaw, hs * 1.22, CANAL_SPLASH_LIFE, true)
		if not turbo:
			_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.03, pos.y), 4, CANAL_AQUA)
	else:
		_activate_burst(_dash_chalk_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), yaw, Vector3.ONE * hs, DASH_CHALK_LIFE, 0.14, 0.0)
	var wind_scale: Vector3 = Vector3.ONE * (hs * (0.85 if turbo else 1.0))
	_activate_burst(_dash_wind_mesh(), Vector3(pos.x, 0.06 * hs, pos.y), yaw, wind_scale, DASH_WIND_LIFE, 0.16, 0.06 * hs)
	if not turbo and not in_canal:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 4, SPRINT_DUST_TINT)


func _spawn_bagsak_cast(center: Vector2, radius: float, team: int) -> void:
	var own: bool = team == _ctx.local_team
	var hs: float = hero_scale()
	var rim_col: Color = BAGSAK_GOLD if own else _ctx.team_color(team)
	var fill_col: Color = BAGSAK_ALLY_FILL if own else _ctx.team_color(team)
	var ring_slot: Slot = _take_slot(_rings)
	ring_slot.active = true
	ring_slot.warm = false
	ring_slot.proj_id = -1
	ring_slot.born = _ctx.time
	ring_slot.life = BAGSAK_RING_LIFE
	ring_slot.base_pos = Vector3(center.x, 0.0, center.y)
	ring_slot.base_scale = Vector3.ONE
	ring_slot.grow = 0.0
	ring_slot.rise_y = 0.0
	ring_slot.node.mesh = bagsak_ring_mesh(radius, rim_col, fill_col)
	ring_slot.node.position = ring_slot.base_pos
	ring_slot.node.rotation = Vector3.ZERO
	ring_slot.node.scale = Vector3.ONE
	ring_slot.material.set_shader_parameter(P_TINT, Color.WHITE)
	ring_slot.material.set_shader_parameter(P_PHASE, 0.0)
	ring_slot.material.set_shader_parameter(P_SOFT_FADE, 1.0)
	ring_slot.node.visible = true

	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	_activate_shockwave(center, radius * 0.96, rim_col, SHOCK_HOT_CORE, BAGSAK_SHOCK_LIFE, 0.08, 0.06 * hs)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 8, BAGSAK_GOLD)


func _spawn_bagsak_slam(center: Vector2, radius: float, team: int) -> void:
	var own: bool = team == _ctx.local_team
	var hs: float = hero_scale()
	var rim_col: Color = BAGSAK_GOLD if own else _ctx.team_color(team)
	_ctx.ground.ring(center, radius * 0.62, radius, Color(rim_col, 0.90), 0.24, _ctx.reduced)
	_ctx.ground.crack(center, _next_phase() * TAU, &"spokes", Vector2(minf(radius * 0.45, 3.6), 1.0), 0.42)
	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	_activate_shockwave(center, radius, rim_col, SHOCK_HOT_CORE, BAGSAK_SHOCK_LIFE, 0.09, 0.10 * hs)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 12, BAGSAK_GOLD)


func _activate_shockwave(
	center: Vector2,
	radius: float,
	ring_col: Color,
	core_col: Color,
	life: float,
	sharpness: float = 0.09,
	rise_y: float = 0.05
) -> void:
	var slot: Slot = _take_slot(_shockwaves)
	slot.active = true
	slot.warm = false
	slot.proj_id = -1
	slot.born = _ctx.time
	slot.life = maxf(life, 0.05)
	slot.base_pos = Vector3(center.x, 0.02, center.y)
	slot.base_scale = Vector3.ONE
	slot.grow = 0.08
	slot.rise_y = rise_y
	slot.node.mesh = _shockwave_mesh(radius)
	slot.node.position = slot.base_pos
	slot.node.rotation = Vector3(0.0, _next_phase() * TAU, 0.0)
	slot.node.scale = Vector3.ONE
	slot.material.set_shader_parameter(P_RING_COLOR, Color(ring_col.r, ring_col.g, ring_col.b, 1.0))
	slot.material.set_shader_parameter(P_CORE_COLOR, core_col)
	slot.material.set_shader_parameter(P_RING_SHARPNESS, sharpness)
	slot.material.set_shader_parameter(P_PHASE, 0.0)
	slot.material.set_shader_parameter(P_SOFT_FADE, 1.0)
	slot.node.visible = true


func _activate_water_ripple(center: Vector2, yaw: float, scale_factor: float, life: float, big_splash: bool) -> void:
	var slot: Slot = _take_slot(_ripples)
	slot.active = true
	slot.warm = false
	slot.proj_id = -1
	slot.born = _ctx.time
	slot.life = maxf(life, 0.05)
	slot.base_pos = Vector3(center.x, 0.04, center.y)
	slot.base_scale = Vector3.ONE * scale_factor
	slot.grow = 0.52 if big_splash else 0.42
	slot.rise_y = (0.06 if big_splash else 0.02) * scale_factor
	slot.node.mesh = _water_ripple_mesh(big_splash)
	slot.node.position = slot.base_pos
	slot.node.rotation = Vector3(0.0, yaw, 0.0)
	slot.node.scale = slot.base_scale
	slot.material.set_shader_parameter(P_AQUA_COLOR, CANAL_AQUA)
	slot.material.set_shader_parameter(P_FOAM_COLOR, CANAL_FOAM)
	slot.material.set_shader_parameter(P_RING_COUNT, 3.5 if big_splash else 2.5)
	slot.material.set_shader_parameter(P_PHASE, 0.0)
	slot.material.set_shader_parameter(P_SOFT_FADE, 0.94)
	slot.node.visible = true


func _update_streak(proj_id: int, wid: StringName, team: int, world_pos: Vector3, yaw: float, hs: float) -> void:
	var target: Slot = null
	for slot: Slot in _streaks:
		if slot.active and not slot.warm and slot.proj_id == proj_id:
			target = slot
			break
	if target == null:
		target = _take_slot(_streaks)
		target.node.mesh = _ribbon_streak_mesh()
	var own: bool = team == _ctx.local_team
	var cols: Array[Color] = _ribbon_colors(wid, own, _ctx.team_color(team))
	var len_scale: float = 1.05 if wid == &"tirador_bato" else 0.82
	target.active = true
	target.warm = false
	target.proj_id = proj_id
	target.born = _ctx.time
	target.life = 0.22
	target.base_pos = world_pos
	target.base_scale = Vector3(hs * len_scale, hs, hs)
	target.grow = 0.0
	target.rise_y = 0.0
	target.node.position = world_pos
	target.node.rotation = Vector3(0.0, yaw, 0.0)
	target.node.scale = target.base_scale
	target.material.set_shader_parameter(P_TINT, cols[0])
	target.material.set_shader_parameter(P_EDGE_COLOR, cols[1])
	target.material.set_shader_parameter(P_SCROLL, 3.8 if wid == &"tirador_bato" else 3.1)
	target.material.set_shader_parameter(P_PHASE, 0.0)
	target.material.set_shader_parameter(P_SOFT_FADE, 0.60)
	target.node.visible = true


func _activate_burst(mesh: ArrayMesh, pos: Vector3, yaw: float, scale_vec: Vector3, life: float, grow: float, rise_y: float) -> void:
	var slot: Slot = _take_slot(_bursts)
	slot.active = true
	slot.warm = false
	slot.proj_id = -1
	slot.born = _ctx.time
	slot.life = maxf(life, 0.01)
	slot.base_pos = pos
	slot.base_scale = scale_vec
	slot.grow = grow
	slot.rise_y = rise_y
	slot.node.mesh = mesh
	slot.node.position = pos
	slot.node.rotation = Vector3(0.0, yaw, 0.0)
	slot.node.scale = scale_vec
	slot.material.set_shader_parameter(P_TINT, Color.WHITE)
	slot.material.set_shader_parameter(P_PHASE, 0.0)
	slot.material.set_shader_parameter(P_SOFT_FADE, 1.0)
	slot.node.visible = true


func _step_pool(pool: Array[Slot]) -> void:
	for slot: Slot in pool:
		if not slot.active or slot.warm:
			continue
		if slot.proj_id >= 0:
			continue
		var age: float = _ctx.time - slot.born
		if age >= slot.life - 0.0001:
			slot.active = false
			slot.node.visible = false
			continue
		var t: float = clampf(age / slot.life, 0.0, 1.0)
		var ease_out: float = 1.0 - (1.0 - t) * (1.0 - t)
		if slot.grow != 0.0:
			slot.node.scale = slot.base_scale * (1.0 + slot.grow * ease_out)
		if slot.rise_y != 0.0:
			slot.node.position = slot.base_pos + Vector3(0.0, slot.rise_y * ease_out, 0.0)
		var alpha: float = 1.0 - smoothstep(0.32, 1.0, t)
		slot.material.set_shader_parameter(P_PHASE, t)
		slot.material.set_shader_parameter(P_SOFT_FADE, alpha)


static func _take_slot(pool: Array[Slot]) -> Slot:
	for slot: Slot in pool:
		if not slot.active:
			return slot
	var oldest: Slot = pool[0]
	for slot: Slot in pool:
		if slot.born < oldest.born:
			oldest = slot
	return oldest


static func _count_active(pool: Array[Slot]) -> int:
	var total: int = 0
	for slot: Slot in pool:
		if slot.active and not slot.warm:
			total += 1
	return total


## Maps a 2D XZ direction `(dx, dz)` so local `+X` of the mesh points along `(dx, 0, dz)`.
static func _yaw_from_dir(dir: Vector2) -> float:
	return atan2(-dir.y, dir.x)


func _next_phase() -> float:
	_step_serial = (_step_serial + 1) % 64
	return fmod(float(_step_serial) * 0.381966, 1.0)


# ---- Low-Poly Stylized VisualShader & Master FX Meshes (built once, cached) ------------------------------------

## Valorant-clear ground boundary ring: <= 0.20 inner fill alpha + sharp outer team/gold rim + warm-espresso edge.
static func bagsak_ring_mesh(radius: float, rim_color: Color, fill_color: Color) -> ArrayMesh:
	var safe_r: float = maxf(radius, 0.8)
	var key: String = "bagsak_ring|%.2f|%s|%s" % [safe_r, rim_color.to_html(), fill_color.to_html()]
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var inner_r: float = maxf(safe_r - BAGSAK_RIM_WIDTH * 0.5, 0.2)
		var inner_outline: PackedVector2Array = AimIndicator.circle(Vector2.ZERO, inner_r)
		AimIndicator.fill(im, inner_outline, Color(fill_color.r, fill_color.g, fill_color.b, BAGSAK_INNER_ALPHA), FxKit.Y_FILL)
		var rim_outline: PackedVector2Array = AimIndicator.circle(Vector2.ZERO, safe_r)
		AimIndicator.ribbon(im, rim_outline, BAGSAK_RIM_WIDTH, Color(rim_color.r, rim_color.g, rim_color.b, 0.94), FxKit.Y_RIM)
		var ink_r: float = safe_r + BAGSAK_RIM_WIDTH * 0.5 + BAGSAK_INK_WIDTH * 0.45
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, ink_r), BAGSAK_INK_WIDTH, INK_ESPRESSO, FxKit.Y_RIM + 0.003)
		for i: int in 8:
			var a: float = TAU * float(i) / 8.0
			var dir: Vector2 = Vector2(cos(a), sin(a))
			var tick_len: float = 0.48 if i % 2 == 0 else 0.28
			var pts: PackedVector2Array = PackedVector2Array([dir * (safe_r - tick_len), dir * safe_r])
			AimIndicator.ribbon(im, pts, 0.08, Color(rim_color.r, rim_color.g, rim_color.b, 0.90), FxKit.Y_RIM + 0.002)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## Radial UV `[0..1]` ground disc + flared 3D Fresnel shockwave skirt for `vs_impact_shockwave.tres`.
static func _shockwave_mesh(radius: float) -> ArrayMesh:
	var safe_r: float = maxf(radius, 0.9)
	var key: String = "vs_shockwave|%.2f" % safe_r
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var segs: int = 24
		var center_uv: Vector2 = Vector2(0.5, 0.5)
		var y_disc: float = FxKit.Y_DECAL + 0.008
		for i: int in segs:
			var a0: float = TAU * float(i) / float(segs)
			var a1: float = TAU * float(i + 1) / float(segs)
			var d0: Vector2 = Vector2(cos(a0), sin(a0))
			var d1: Vector2 = Vector2(cos(a1), sin(a1))
			_add_tri_uv(
				im,
				Vector3(0.0, y_disc, 0.0),
				Vector3(d1.x * safe_r, y_disc, d1.y * safe_r),
				Vector3(d0.x * safe_r, y_disc, d0.y * safe_r),
				center_uv,
				center_uv + d1 * 0.49,
				center_uv + d0 * 0.49,
				Vector3.UP,
				Color.WHITE
			)
		var skirt_segs: int = 20
		var r_base: float = safe_r * 0.88
		var r_top: float = safe_r * 0.97
		var h_top: float = clampf(safe_r * 0.07, 0.14, 0.32)
		for i: int in skirt_segs:
			var a0: float = TAU * float(i) / float(skirt_segs)
			var a1: float = TAU * float(i + 1) / float(skirt_segs)
			var d0: Vector2 = Vector2(cos(a0), sin(a0))
			var d1: Vector2 = Vector2(cos(a1), sin(a1))
			var nrm: Vector3 = Vector3((d0.x + d1.x) * 0.5, 0.35, (d0.y + d1.y) * 0.5).normalized()
			_add_quad_uv(
				im,
				Vector3(d0.x * r_base, 0.02, d0.y * r_base),
				Vector3(d1.x * r_base, 0.02, d1.y * r_base),
				Vector3(d1.x * r_top, h_top, d1.y * r_top),
				Vector3(d0.x * r_top, h_top, d0.y * r_top),
				center_uv + d0 * 0.43,
				center_uv + d1 * 0.43,
				center_uv + d1 * 0.48,
				center_uv + d0 * 0.48,
				nrm,
				Color(1.0, 1.0, 1.0, 0.52),
				Color(1.0, 1.0, 1.0, 0.0)
			)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## Crossed horizontal + vertical tapered `[0..1]` UV ribbons along local `-X -> +X` for `vs_projectile_ribbon.tres`.
static func _ribbon_streak_mesh() -> ArrayMesh:
	var key: String = "vs_ribbon_streak"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var steps: int = 12
		var tail_x: float = -2.10
		var head_x: float = 0.12
		var max_half: float = 0.115
		for plane_idx: int in 2:
			var vertical: bool = plane_idx == 1
			var nrm: Vector3 = Vector3(0.0, 0.0, 1.0) if vertical else Vector3.UP
			for i: int in steps:
				var u0: float = float(i) / float(steps)
				var u1: float = float(i + 1) / float(steps)
				var x0: float = lerpf(tail_x, head_x, u0)
				var x1: float = lerpf(tail_x, head_x, u1)
				var head_round0: float = sin(minf((1.0 - u0) / 0.12, 1.0) * (PI * 0.5))
				var head_round1: float = sin(minf((1.0 - u1) / 0.12, 1.0) * (PI * 0.5))
				var w0: float = max_half * pow(u0, 1.25) * head_round0
				var w1: float = max_half * pow(u1, 1.25) * head_round1
				var c0: Color = Color(1.0, 1.0, 1.0, pow(u0, 1.35) * 0.88)
				var c1: Color = Color(1.0, 1.0, 1.0, pow(u1, 1.35) * 0.88)
				if vertical:
					_add_quad_uv(
						im,
						Vector3(x0, w0 * 0.88, 0.0),
						Vector3(x0, -w0 * 0.88, 0.0),
						Vector3(x1, -w1 * 0.88, 0.0),
						Vector3(x1, w1 * 0.88, 0.0),
						Vector2(u0, 1.0),
						Vector2(u0, 0.0),
						Vector2(u1, 0.0),
						Vector2(u1, 1.0),
						nrm,
						c0,
						c1
					)
				else:
					_add_quad_uv(
						im,
						Vector3(x0, 0.0, w0),
						Vector3(x0, 0.0, -w0),
						Vector3(x1, 0.0, -w1),
						Vector3(x1, 0.0, w1),
						Vector2(u0, 1.0),
						Vector2(u0, 0.0),
						Vector2(u1, 0.0),
						Vector2(u1, 1.0),
						nrm,
						c0,
						c1
					)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## Radial UV `[0..1]` water disc + tilted Fresnel foam splash crown petals for `vs_water_ripple.tres`.
static func _water_ripple_mesh(big_splash: bool) -> ArrayMesh:
	var key: String = "vs_water_ripple_big" if big_splash else "vs_water_ripple_wade"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var outer_r: float = 0.86 if big_splash else 0.60
		var segs: int = 20
		var center_uv: Vector2 = Vector2(0.5, 0.5)
		for i: int in segs:
			var a0: float = TAU * float(i) / float(segs)
			var a1: float = TAU * float(i + 1) / float(segs)
			var d0: Vector2 = Vector2(cos(a0), sin(a0))
			var d1: Vector2 = Vector2(cos(a1), sin(a1))
			_add_tri_uv(
				im,
				Vector3(0.0, 0.01, 0.0),
				Vector3(d1.x * outer_r, 0.01, d1.y * outer_r),
				Vector3(d0.x * outer_r, 0.01, d0.y * outer_r),
				center_uv,
				center_uv + d1 * 0.49,
				center_uv + d0 * 0.49,
				Vector3.UP,
				Color.WHITE
			)
		var petals: int = 6 if big_splash else 4
		var crown_r: float = outer_r * 0.58
		var crown_h: float = 0.26 if big_splash else 0.14
		for i: int in petals:
			var ang: float = TAU * float(i) / float(petals) + 0.32
			var span: float = 0.34
			var d0: Vector2 = Vector2(cos(ang - span), sin(ang - span))
			var d1: Vector2 = Vector2(cos(ang + span), sin(ang + span))
			var dm: Vector2 = Vector2(cos(ang), sin(ang))
			var nrm: Vector3 = Vector3(dm.x, 0.42, dm.y).normalized()
			_add_tri_uv(
				im,
				Vector3(d0.x * crown_r, 0.02, d0.y * crown_r),
				Vector3(d1.x * crown_r, 0.02, d1.y * crown_r),
				Vector3(dm.x * (crown_r + 0.14), crown_h, dm.y * (crown_r + 0.14)),
				center_uv + d0 * 0.34,
				center_uv + d1 * 0.34,
				center_uv + dm * 0.48,
				nrm,
				Color.WHITE
			)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## 16-sided open shimmer cylinder + crisp base beacon ring (`UV.x = 0..1` around, `UV.y = 0` base -> `1` top) for `vs_merienda_aura.tres`.
static func _merienda_cylinder_mesh() -> ArrayMesh:
	var key: String = "vs_merienda_cylinder"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var segs: int = 16
		var y0: float = 0.02
		var y1: float = AURA_HEIGHT
		var y_ring: float = 0.03
		var inner_r: float = AURA_RADIUS * 0.90
		var outer_r: float = AURA_RADIUS * 1.12
		for i: int in segs:
			var u0: float = float(i) / float(segs)
			var u1: float = float(i + 1) / float(segs)
			var a0: float = u0 * TAU
			var a1: float = u1 * TAU
			var d0: Vector2 = Vector2(cos(a0), sin(a0))
			var d1: Vector2 = Vector2(cos(a1), sin(a1))
			var nrm: Vector3 = Vector3((d0.x + d1.x) * 0.5, 0.08, (d0.y + d1.y) * 0.5).normalized()
			_add_quad_uv(
				im,
				Vector3(d0.x * AURA_RADIUS, y0, d0.y * AURA_RADIUS),
				Vector3(d1.x * AURA_RADIUS, y0, d1.y * AURA_RADIUS),
				Vector3(d1.x * (AURA_RADIUS * 1.04), y1, d1.y * (AURA_RADIUS * 1.04)),
				Vector3(d0.x * (AURA_RADIUS * 1.04), y1, d0.y * (AURA_RADIUS * 1.04)),
				Vector2(u0, 0.0),
				Vector2(u1, 0.0),
				Vector2(u1, 1.0),
				Vector2(u0, 1.0),
				nrm,
				Color.WHITE,
				Color.WHITE
			)
			_add_quad_uv(
				im,
				Vector3(d0.x * inner_r, y_ring, d0.y * inner_r),
				Vector3(d1.x * inner_r, y_ring, d1.y * inner_r),
				Vector3(d1.x * outer_r, y_ring, d1.y * outer_r),
				Vector3(d0.x * outer_r, y_ring, d0.y * outer_r),
				Vector2(u0, 0.02),
				Vector2(u1, 0.02),
				Vector2(u1, 0.06),
				Vector2(u0, 0.06),
				Vector3.UP,
				Color(1.0, 1.0, 1.0, 0.92),
				Color(1.0, 1.0, 1.0, 0.82)
			)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _powerup_halo_mesh(kind: int = 0) -> ArrayMesh:
	var key: String = "powerup_halo_%d" % kind
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var primary: Color = _aura_primary_color(kind)
		var secondary: Color = _aura_accent_color(kind)
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, 0.72), 0.085, primary, 0.0)
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, 0.48), 0.060, secondary, 0.003)
		for i: int in 4:
			var ang: float = TAU * float(i) / 4.0 + 0.25
			var base: Vector3 = Vector3(cos(ang) * 0.58, 0.04, sin(ang) * 0.58)
			var tip: Vector3 = Vector3(cos(ang) * 0.68, 0.85, sin(ang) * 0.68)
			_vertical_shock_stroke(im, base, tip, 0.065, secondary, primary)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## 3 floating sunlit sketch arcs (release fan or radial impact burst).
static func _tirador_arcs_mesh(impact: bool) -> ArrayMesh:
	var key: String = "tirador_impact" if impact else "tirador_release"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		if impact:
			var angles: Array[float] = [0.18, 2.27, 4.36]
			var radii: Array[float] = [0.54, 0.66, 0.60]
			var heights: Array[float] = [0.10, 0.24, 0.17]
			for i: int in 3:
				var accent: Color = CYAN_SPARK if i == 1 else AMBER_CORE
				_sketch_crescent(im, angles[i], 1.12, radii[i], heights[i], 0.11, 0.078, INK_ESPRESSO, accent)
		else:
			var rel_angles: Array[float] = [-0.66, 0.06, 0.72]
			var rel_radii: Array[float] = [0.56, 0.76, 0.62]
			var rel_heights: Array[float] = [0.10, 0.25, 0.17]
			for i: int in 3:
				var rel_accent: Color = CYAN_SPARK if i == 1 else AMBER_CORE
				_sketch_crescent(im, rel_angles[i], 1.02, rel_radii[i], rel_heights[i], 0.09, 0.074, INK_ESPRESSO, rel_accent)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _dash_chalk_mesh() -> ArrayMesh:
	var key: String = "dash_chalk"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var spine: PackedVector2Array = PackedVector2Array([
			Vector2(-2.35, 0.0), Vector2(-1.60, 0.05), Vector2(-0.78, -0.04), Vector2(0.15, 0.0)
		])
		AimIndicator.ribbon(im, spine, 0.62, CHALK_WARM, 0.0)
		for i: int in 6:
			var side: float = -1.0 if i % 2 == 0 else 1.0
			var x0: float = -2.15 + float(i) * 0.38
			var z0: float = side * (0.26 + float(i % 3) * 0.05)
			var tick: PackedVector2Array = PackedVector2Array([
				Vector2(x0 - 0.30, z0 + side * 0.08),
				Vector2(x0 + 0.16, z0),
			])
			AimIndicator.ribbon(im, tick, 0.055, INK_ESPRESSO, 0.003)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _dash_wind_mesh() -> ArrayMesh:
	var key: String = "dash_wind"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var z_offsets: Array[float] = [-0.50, 0.50, -0.72, 0.72]
		var y_bases: Array[float] = [0.44, 0.58, 0.86, 1.04]
		var x_starts: Array[float] = [-1.95, -1.72, -1.55, -1.82]
		var x_ends: Array[float] = [0.50, 0.62, 0.38, 0.44]
		for i: int in 4:
			_wind_stroke_3d(im, x_starts[i], x_ends[i], z_offsets[i], y_bases[i], 0.068, INK_ESPRESSO, WIND_SUNLIT)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _sketch_crescent(im: ImmediateMesh, center_angle: float, span: float, radius: float, y_base: float, y_arch: float, half_w: float, ink: Color, core: Color) -> void:
	var steps: int = 8
	for layer: int in 2:
		var col: Color = ink if layer == 0 else core
		var width_scale: float = 1.0 if layer == 0 else 0.50
		var r_off: float = 0.0 if layer == 0 else 0.010
		var y_bias: float = 0.0 if layer == 0 else 0.005
		for i: int in steps:
			var s0: float = float(i) / float(steps)
			var s1: float = float(i + 1) / float(steps)
			var a0: float = center_angle + lerpf(-span * 0.5, span * 0.5, s0)
			var a1: float = center_angle + lerpf(-span * 0.5, span * 0.5, s1)
			var prof0: float = sin(s0 * PI)
			var prof1: float = sin(s1 * PI)
			var w0: float = half_w * width_scale * prof0
			var w1: float = half_w * width_scale * prof1
			var r0: float = radius + r_off
			var out0: Vector3 = Vector3(cos(a0), 0.0, sin(a0))
			var out1: Vector3 = Vector3(cos(a1), 0.0, sin(a1))
			var p0: Vector3 = out0 * r0 + Vector3(0.0, y_base + y_arch * prof0 + y_bias, 0.0)
			var p1: Vector3 = out1 * r0 + Vector3(0.0, y_base + y_arch * prof1 + y_bias, 0.0)
			var side0: Vector3 = (out0 * 0.72 + Vector3.UP * 0.69).normalized()
			var side1: Vector3 = (out1 * 0.72 + Vector3.UP * 0.69).normalized()
			_add_quad_grad(im, p0 - side0 * w0, p1 - side1 * w1, p1 + side1 * w1, p0 + side0 * w0, col, col)


static func _wind_stroke_3d(im: ImmediateMesh, x0: float, x1: float, z_base: float, y_base: float, half_w: float, ink: Color, core: Color) -> void:
	var steps: int = 6
	for layer: int in 2:
		var col: Color = ink if layer == 0 else core
		var scale_w: float = 1.0 if layer == 0 else 0.48
		var y_nudge: float = 0.0 if layer == 0 else 0.004
		for i: int in steps:
			var s0: float = float(i) / float(steps)
			var s1: float = float(i + 1) / float(steps)
			var prof0: float = sin(s0 * PI)
			var prof1: float = sin(s1 * PI)
			var p0: Vector3 = Vector3(lerpf(x0, x1, s0), y_base + 0.13 * prof0 + y_nudge, z_base * (1.0 - 0.18 * s0))
			var p1: Vector3 = Vector3(lerpf(x0, x1, s1), y_base + 0.13 * prof1 + y_nudge, z_base * (1.0 - 0.18 * s1))
			var side: Vector3 = Vector3(0.0, 0.78, 0.62 if z_base >= 0.0 else -0.62).normalized()
			var w0: float = half_w * scale_w * prof0
			var w1: float = half_w * scale_w * prof1
			_add_quad_grad(im, p0 - side * w0, p1 - side * w1, p1 + side * w1, p0 + side * w0, col, col)


static func _vertical_shock_stroke(im: ImmediateMesh, base: Vector3, tip: Vector3, half_w: float, ink: Color, core: Color) -> void:
	var radial: Vector3 = Vector3(base.x, 0.0, base.z).normalized()
	var tangent: Vector3 = Vector3(-radial.z, 0.0, radial.x)
	var ink_fade: Color = Color(ink.r, ink.g, ink.b, 0.0)
	var core_fade: Color = Color(core.r, core.g, core.b, 0.0)
	for axis: Vector3 in [tangent, radial]:
		var mid: Vector3 = base.lerp(tip, 0.42)
		_add_quad_grad(im, base - axis * (half_w * 0.35), mid - axis * half_w, mid + axis * half_w, base + axis * (half_w * 0.35), ink, ink)
		_add_tri_grad(im, mid - axis * half_w, tip, mid + axis * half_w, ink, ink_fade)
		var core_w: float = half_w * 0.48
		_add_quad_grad(im, base - axis * (core_w * 0.35), mid - axis * core_w, mid + axis * core_w, base + axis * (core_w * 0.35), core, core)
		_add_tri_grad(im, mid - axis * core_w, tip, mid + axis * core_w, core, core_fade)


static func _add_tri_uv(
	im: ImmediateMesh,
	a: Vector3,
	b: Vector3,
	c: Vector3,
	uv_a: Vector2,
	uv_b: Vector2,
	uv_c: Vector2,
	nrm: Vector3,
	col: Color
) -> void:
	im.surface_set_normal(nrm)
	im.surface_set_color(col)
	im.surface_set_uv(uv_a)
	im.surface_add_vertex(a)
	im.surface_set_normal(nrm)
	im.surface_set_color(col)
	im.surface_set_uv(uv_b)
	im.surface_add_vertex(b)
	im.surface_set_normal(nrm)
	im.surface_set_color(col)
	im.surface_set_uv(uv_c)
	im.surface_add_vertex(c)


static func _add_quad_uv(
	im: ImmediateMesh,
	a: Vector3,
	b: Vector3,
	c: Vector3,
	d: Vector3,
	uv_a: Vector2,
	uv_b: Vector2,
	uv_c: Vector2,
	uv_d: Vector2,
	nrm: Vector3,
	col_ab: Color,
	col_cd: Color
) -> void:
	im.surface_set_normal(nrm)
	im.surface_set_color(col_ab)
	im.surface_set_uv(uv_a)
	im.surface_add_vertex(a)
	im.surface_set_normal(nrm)
	im.surface_set_color(col_ab)
	im.surface_set_uv(uv_b)
	im.surface_add_vertex(b)
	im.surface_set_normal(nrm)
	im.surface_set_color(col_cd)
	im.surface_set_uv(uv_c)
	im.surface_add_vertex(c)
	im.surface_set_normal(nrm)
	im.surface_set_color(col_ab)
	im.surface_set_uv(uv_a)
	im.surface_add_vertex(a)
	im.surface_set_normal(nrm)
	im.surface_set_color(col_cd)
	im.surface_set_uv(uv_c)
	im.surface_add_vertex(c)
	im.surface_set_normal(nrm)
	im.surface_set_color(col_cd)
	im.surface_set_uv(uv_d)
	im.surface_add_vertex(d)


static func _add_tri_grad(im: ImmediateMesh, a: Vector3, tip: Vector3, d: Vector3, col_base: Color, col_tip: Color) -> void:
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(0.0, 0.0))
	im.surface_set_color(col_base)
	im.surface_add_vertex(a)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(0.5, 1.0))
	im.surface_set_color(col_tip)
	im.surface_add_vertex(tip)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(1.0, 0.0))
	im.surface_set_color(col_base)
	im.surface_add_vertex(d)


static func _add_quad_grad(im: ImmediateMesh, a: Vector3, b: Vector3, c: Vector3, d: Vector3, col0: Color, col1: Color) -> void:
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(0.0, 0.0))
	im.surface_set_color(col0)
	im.surface_add_vertex(a)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(1.0, 0.0))
	im.surface_set_color(col1)
	im.surface_add_vertex(b)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(1.0, 1.0))
	im.surface_set_color(col1)
	im.surface_add_vertex(c)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(0.0, 0.0))
	im.surface_set_color(col0)
	im.surface_add_vertex(a)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(1.0, 1.0))
	im.surface_set_color(col1)
	im.surface_add_vertex(c)
	im.surface_set_normal(Vector3.UP)
	im.surface_set_uv(Vector2(0.0, 1.0))
	im.surface_set_color(col0)
	im.surface_add_vertex(d)
