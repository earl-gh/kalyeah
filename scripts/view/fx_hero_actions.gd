class_name FxHeroActions
extends Node3D
## Sunlit 90s Philippine province gameplay VFX (powered by `arcane_fx_master.gdshader`, scaled for `0.82x` kids):
## 1. Tirador (`Shape.SKILLSHOT`): warm-espresso rimmed mango-aqua stone streak + 3 floating sketch arcs on release & impact.
## 2. Luksong-Tinik Vault (`sim.player_dashed`): kinetic sunlit wind-sketch lines + low ground chalk/dust ribbon.
## 3. Bagsak Ultimate (`sim.ultimate_cast`): Valorant-clear ground boundary ring (`<= 0.20` inner alpha, exact `zone.radius`) + rising sunlit shock lines.
## 4. Walkable Shallow Mid Canal (`Z ~ 0`): sunlit turquoise-jade wading ripples + water splash droplet crowns on walk/run/jump/slide.
## 5. Locomotion & Power-Ups: subtle sprint ticks, dry-ground slide skid & jump-landing dust ring, and power-up pickup sunlit halo.
## Strictly read-only on MatchSim; all nodes and ShaderMaterials are pre-allocated once in setup().

const MASTER_SHADER: Shader = preload("res://shaders/arcane_fx_master.gdshader")
const P_TINT: StringName = &"tint"
const P_PHASE: StringName = &"phase"
const P_SOFT_FADE: StringName = &"soft_fade"
const P_FRESNEL: StringName = &"fresnel_softness"
const P_SUNLIT: StringName = &"sunlit_lift"
const P_SCROLL: StringName = &"scroll_speed"

const RING_SLOTS: int = 2
const STREAK_SLOTS: int = 4
const BURST_SLOTS: int = 10

const CANAL_HALF_Z: float = 3.75
const WADE_INTERVAL_WALK: float = 0.24
const WADE_INTERVAL_FAST: float = 0.15
const WADE_RIPPLE_LIFE: float = 0.38
const CANAL_SPLASH_LIFE: float = 0.44
const JUMP_RING_LIFE: float = 0.26
const POWERUP_HALO_LIFE: float = 0.46

const BAGSAK_INNER_ALPHA: float = 0.16
const BAGSAK_RIM_WIDTH: float = 0.20
const BAGSAK_INK_WIDTH: float = 0.07
const BAGSAK_RING_LIFE: float = 0.52
const BAGSAK_SHOCK_LIFE: float = 0.34
const TIRADOR_ARC_LIFE: float = 0.18
const TIRADOR_STREAK_FADE: float = 0.08
const DASH_WIND_LIFE: float = 0.25
const DASH_CHALK_LIFE: float = 0.34
const SPRINT_INTERVAL: float = 0.26
const SPRINT_TICK_LIFE: float = 0.16

const INK_ESPRESSO: Color = Color(0.20, 0.15, 0.14, 0.90)
const AMBER_CORE: Color = Color(0.98, 0.72, 0.22, 0.95)
const CYAN_SPARK: Color = Color(0.32, 0.88, 0.92, 0.96)
const CHALK_WARM: Color = Color(0.92, 0.86, 0.74, 0.54)
const WIND_SUNLIT: Color = Color(0.84, 0.96, 0.95, 0.88)
const BAGSAK_GOLD: Color = Color(0.98, 0.78, 0.24, 0.96)
const BAGSAK_ALLY_FILL: Color = Color(1.0, 0.90, 0.56, 1.0)
const SPRINT_DUST_TINT: Color = Color(0.90, 0.84, 0.72, 0.62)
const CANAL_AQUA: Color = Color(0.30, 0.82, 0.76, 0.86)
const CANAL_FOAM: Color = Color(0.92, 0.98, 0.95, 0.90)
const POWERUP_JADE: Color = Color(0.22, 0.86, 0.68, 0.92)


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


class UltWatch extends RefCounted:
	var center: Vector2 = Vector2.ZERO
	var radius: float = 8.5
	var team: int = 0
	var born: float = 0.0


static var _meshes: Dictionary[String, ArrayMesh] = {}

var _ctx: FxContext
var _zones: FxZones
var _rings: Array[Slot] = []
var _streaks: Array[Slot] = []
var _bursts: Array[Slot] = []
var _seen_shots: Dictionary[int, Vector2] = {}
var _shot_dirs: Dictionary[int, Vector2] = {}
var _ult_zones: Dictionary[int, UltWatch] = {}
var _prev_dash: Dictionary[int, float] = {}
var _prev_pos: Dictionary[int, Vector2] = {}
var _prev_air_y: Dictionary[int, float] = {}
var _prev_slide: Dictionary[int, float] = {}
var _prev_buff: Dictionary[int, float] = {}
var _last_dash_fx: Dictionary[int, float] = {}
var _last_ult_fx: Dictionary[int, float] = {}
var _last_powerup_fx: Dictionary[int, float] = {}
var _sprint_due: Dictionary[int, float] = {}
var _wade_due: Dictionary[int, float] = {}
var _step_serial: int = 0


func setup(ctx: FxContext, zones_view: FxZones = null) -> void:
	_ctx = ctx
	_zones = zones_view
	_rings = _make_pool(RING_SLOTS, FxKit.PRIO_RIM, FxKit.flat_quad(), 0.0)
	_streaks = _make_pool(STREAK_SLOTS, FxKit.PRIO_DECAL, _stone_streak_mesh(), 0.14)
	_bursts = _make_pool(BURST_SLOTS, FxKit.PRIO_DECAL, _tirador_arcs_mesh(false), 0.10)


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
	if _bursts.is_empty():
		return
	var slot: Slot = _bursts[0]
	slot.active = true
	slot.warm = true
	slot.proj_id = -1
	slot.born = _ctx.time if _ctx != null else 0.0
	slot.life = 1.0
	slot.node.mesh = _canal_splash_mesh(false)
	slot.node.position = at
	slot.node.scale = Vector3.ONE * 0.1
	slot.material.set_shader_parameter(P_TINT, Color(1.0, 1.0, 1.0, 0.0))
	slot.material.set_shader_parameter(P_SOFT_FADE, 0.0)
	slot.node.visible = true


func drop_warm() -> void:
	for slot: Slot in _bursts:
		if slot.warm:
			slot.warm = false
			slot.active = false
			slot.node.visible = false
			slot.material.set_shader_parameter(P_TINT, Color.WHITE)
			slot.material.set_shader_parameter(P_SOFT_FADE, 1.0)


func _make_pool(count: int, priority: int, default_mesh: ArrayMesh, fresnel: float) -> Array[Slot]:
	var pool: Array[Slot] = []
	for i: int in count:
		var slot: Slot = Slot.new()
		slot.node = MeshInstance3D.new()
		slot.node.name = "HeroFx_%d_%d" % [priority, i]
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
		_activate_burst(_powerup_halo_mesh(eff_kind), Vector3(state.position.x, 0.08, state.position.y), _next_phase() * TAU, Vector3.ONE * hs, POWERUP_HALO_LIFE, 0.34, 0.22 * hs)


func on_projectile_gone(def: WeaponDef, team: int, at: Vector3) -> void:
	if _ctx == null or def == null or def.shape != WeaponDef.Shape.SKILLSHOT:
		return
	var hs: float = hero_scale()
	var turbo: bool = Settings.performance == &"turbo"
	if not _ctx.reduced:
		var yaw: float = _next_phase() * TAU
		var y_safe: float = maxf(at.y - 0.14 * hs, 0.26 * hs)
		_activate_burst(_tirador_arcs_mesh(true), Vector3(at.x, y_safe, at.z), yaw, Vector3.ONE * hs, TIRADOR_ARC_LIFE, 0.22, 0.06 * hs)
		if not turbo:
			_ctx.particles.puff(FxParticles.Preset.CHIPS, Vector3(at.x, 0.12 * hs, at.z), 2)
	var rim_col: Color = AMBER_CORE if team == _ctx.local_team else _ctx.team_color(team)
	_ctx.ground.ring(Vector2(at.x, at.z), 0.16 * hs, 0.52 * hs, Color(rim_col, 0.85), 0.16, _ctx.reduced)


func sync(sim: MatchSim) -> void:
	if _ctx == null:
		return
	_sync_skillshots(sim)
	_sync_ult_zones(sim)
	_sync_players(sim)
	_step_pool(_rings)
	_step_pool(_streaks)
	_step_pool(_bursts)


func active_ring_count() -> int:
	return _count_active(_rings)


func active_streak_count() -> int:
	return _count_active(_streaks)


func active_sketch_count() -> int:
	return _count_active(_bursts)


func _sync_skillshots(sim: MatchSim) -> void:
	var live_ids: Dictionary[int, bool] = {}
	var hs: float = hero_scale()
	var flight_y: float = maxf(FxProjectiles.PROJECTILE_HEIGHT * _ctx.view_scale, 0.48)
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		if projectile.def == null or projectile.def.shape != WeaponDef.Shape.SKILLSHOT:
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
			if not _ctx.reduced:
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
			_update_streak(pid, Vector3(drawn_pos.x, flight_y, drawn_pos.y), _yaw_from_dir(use_dir), hs)

	for old_id: int in _seen_shots.keys():
		if not live_ids.has(old_id):
			_seen_shots.erase(old_id)
			_shot_dirs.erase(old_id)
	for slot: Slot in _streaks:
		if slot.active and slot.proj_id >= 0 and not live_ids.has(slot.proj_id):
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
			var watch: UltWatch = UltWatch.new()
			watch.center = zone.center
			watch.radius = zone.radius
			watch.team = zone.team
			watch.born = _ctx.time
			_ult_zones[zone.id] = watch
			# Keep FxZones' generic 50% fill hidden on ult_bagsak so our Valorant <= 0.20 ring owns floor clarity.
			if _zones != null:
				var znode: Node3D = _zones.node_for(zone.id)
				if znode != null:
					var mark: MeshInstance3D = znode.get_node_or_null("Mark") as MeshInstance3D
					if mark != null:
						mark.visible = false
			var recent_ult: float = _last_ult_fx[zone.owner_id] if _last_ult_fx.has(zone.owner_id) else -10.0
			if _ctx.time - recent_ult > 0.12:
				_spawn_bagsak_cast(zone.center, zone.radius, zone.team)

	for zid: int in _ult_zones.keys():
		if not seen_zones.has(zid):
			var finished: UltWatch = _ult_zones[zid]
			_ult_zones.erase(zid)
			if _ctx.time - finished.born >= 0.18:
				_spawn_bagsak_slam(finished.center, finished.radius, finished.team)


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

		var first_seen: bool = not _prev_pos.has(id)
		var prev_p: Vector2 = _prev_pos[id] if not first_seen else pos
		var prev_air: float = _prev_air_y[id] if _prev_air_y.has(id) else air_y
		var prev_sl: float = _prev_slide[id] if _prev_slide.has(id) else slide_left
		var prev_bf: float = _prev_buff[id] if _prev_buff.has(id) else buff_left
		_prev_pos[id] = pos
		_prev_air_y[id] = air_y
		_prev_slide[id] = slide_left
		_prev_buff[id] = buff_left

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
		var landed_jump: bool = prev_air > 0.10 and air_y <= 0.015
		var started_slide: bool = slide_left > prev_sl + 0.02

		if not _ctx.reduced and not turbo:
			if in_canal:
				if landed_jump or started_slide:
					_wade_due[id] = _ctx.time + WADE_INTERVAL_FAST
					_activate_burst(_canal_splash_mesh(true), Vector3(pos.x, 0.075, pos.y), move_yaw, Vector3.ONE * (hs * 1.15), CANAL_SPLASH_LIFE, 0.54, 0.09 * hs)
				elif step_dist > 0.022 and air_y <= 0.05:
					var wade_due: float = _wade_due[id] if _wade_due.has(id) else 0.0
					if _ctx.time >= wade_due:
						var fast_wade: bool = state.sprinting or state.dash_left > 0.0 or slide_left > 0.0
						_wade_due[id] = _ctx.time + (WADE_INTERVAL_FAST if fast_wade else WADE_INTERVAL_WALK)
						_activate_burst(_canal_splash_mesh(false), Vector3(pos.x, 0.072, pos.y), move_yaw, Vector3.ONE * hs, WADE_RIPPLE_LIFE, 0.46, 0.03 * hs)
			else:
				_wade_due.erase(id)
				if started_slide:
					_activate_burst(_dash_chalk_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), move_yaw, Vector3.ONE * (hs * 0.78), DASH_CHALK_LIFE * 0.85, 0.12, 0.0)
				elif landed_jump:
					_activate_burst(_jump_dust_ring_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), _next_phase() * TAU, Vector3.ONE * hs, JUMP_RING_LIFE, 0.42, 0.04 * hs)

		if state.sprinting and not in_canal and air_y <= 0.05 and not _ctx.reduced and not turbo:
			var due: float = _sprint_due[id] if _sprint_due.has(id) else 0.0
			if _ctx.time >= due:
				_sprint_due[id] = _ctx.time + SPRINT_INTERVAL
				var heel: Vector2 = pos - move_dir * (0.24 * hs)
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(heel.x, 0.0, heel.y), 2, SPRINT_DUST_TINT)
				_activate_burst(_sprint_ticks_mesh(), Vector3(pos.x, 0.05, pos.y), move_yaw, Vector3.ONE * hs, SPRINT_TICK_LIFE, 0.08, 0.03 * hs)
		else:
			_sprint_due.erase(id)

	for old_id: int in _prev_dash.keys():
		if not present.has(old_id):
			_prev_dash.erase(old_id)
			_prev_pos.erase(old_id)
			_prev_air_y.erase(old_id)
			_prev_slide.erase(old_id)
			_prev_buff.erase(old_id)
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
	return absf(pos.y) <= CANAL_HALF_Z


static func _read_float(obj: Object, prop: StringName) -> float:
	var val: Variant = obj.get(prop)
	if val is float:
		return val
	if val is int:
		return float(val)
	return 0.0


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
		_activate_burst(_canal_splash_mesh(true), Vector3(pos.x, 0.075, pos.y), yaw, Vector3.ONE * (hs * 1.2), CANAL_SPLASH_LIFE, 0.56, 0.10 * hs)
	else:
		_activate_burst(_dash_chalk_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), yaw, Vector3.ONE * hs, DASH_CHALK_LIFE, 0.14, 0.0)
	var wind_scale: Vector3 = Vector3.ONE * (hs * (0.85 if turbo else 1.0))
	_activate_burst(_dash_wind_mesh(), Vector3(pos.x, 0.06 * hs, pos.y), yaw, wind_scale, DASH_WIND_LIFE, 0.16, 0.06 * hs)
	if not turbo and not in_canal:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 2, SPRINT_DUST_TINT)


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
	var yaw: float = _next_phase() * TAU
	_activate_burst(_bagsak_shock_mesh(radius), Vector3(center.x, 0.04, center.y), yaw, Vector3(1.0, hs, 1.0), BAGSAK_SHOCK_LIFE, 0.06, 0.30 * hs)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 8, BAGSAK_GOLD)


func _spawn_bagsak_slam(center: Vector2, radius: float, team: int) -> void:
	var own: bool = team == _ctx.local_team
	var hs: float = hero_scale()
	var rim_col: Color = BAGSAK_GOLD if own else _ctx.team_color(team)
	_ctx.ground.ring(center, radius * 0.35, radius, Color(rim_col, 0.90), 0.24, _ctx.reduced)
	_ctx.ground.crack(center, _next_phase() * TAU, &"spokes", Vector2(minf(radius * 0.45, 3.6), 1.0), 0.42)
	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	_activate_burst(_bagsak_shock_mesh(radius * 0.92), Vector3(center.x, 0.04, center.y), _next_phase() * TAU, Vector3(1.04, 1.15 * hs, 1.04), BAGSAK_SHOCK_LIFE, 0.08, 0.42 * hs)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 12, BAGSAK_GOLD)


func _update_streak(proj_id: int, world_pos: Vector3, yaw: float, hs: float) -> void:
	var target: Slot = null
	for slot: Slot in _streaks:
		if slot.active and slot.proj_id == proj_id:
			target = slot
			break
	if target == null:
		target = _take_slot(_streaks)
		target.node.mesh = _stone_streak_mesh()
	target.active = true
	target.warm = false
	target.proj_id = proj_id
	target.born = _ctx.time
	target.life = 0.25
	target.base_pos = world_pos
	target.base_scale = Vector3.ONE * hs
	target.grow = 0.0
	target.rise_y = 0.0
	target.node.position = world_pos
	target.node.rotation = Vector3(0.0, yaw, 0.0)
	target.node.scale = target.base_scale
	target.material.set_shader_parameter(P_TINT, Color.WHITE)
	target.material.set_shader_parameter(P_PHASE, 0.0)
	target.material.set_shader_parameter(P_SOFT_FADE, 1.0)
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


# ---- Baked Sunlit Stylized & Canal Splash Meshes (built once, cached) ------------------------------------

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


## Walkable shallow canal wading ripple ring + sunlit turquoise-white splash droplet arcs.
static func _canal_splash_mesh(big_splash: bool) -> ArrayMesh:
	var key: String = "canal_splash_big" if big_splash else "canal_wade_ripple"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var outer_r: float = 0.78 if big_splash else 0.56
		var inner_r: float = 0.42 if big_splash else 0.28
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, outer_r), 0.075, CANAL_FOAM, 0.0)
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, inner_r), 0.055, CANAL_AQUA, 0.002)
		var drop_count: int = 5 if big_splash else 3
		for i: int in drop_count:
			var ang: float = TAU * float(i) / float(drop_count) + 0.35
			var rad: float = (0.46 if big_splash else 0.34) + float(i % 2) * 0.08
			var arch: float = 0.28 if big_splash else 0.14
			_sketch_crescent(im, ang, 0.78, rad, 0.03, arch, 0.055, CANAL_AQUA, CANAL_FOAM)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _jump_dust_ring_mesh() -> ArrayMesh:
	var key: String = "jump_dust_ring"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, 0.52), 0.085, CHALK_WARM, 0.0)
		for i: int in 4:
			var ang: float = TAU * float(i) / 4.0 + 0.4
			_sketch_crescent(im, ang, 0.72, 0.44, 0.04, 0.10, 0.048, INK_ESPRESSO, CHALK_WARM)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _powerup_halo_mesh(kind: int = 0) -> ArrayMesh:
	var key: String = "powerup_halo_%d" % kind
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var primary: Color = CANAL_AQUA if kind == 1 else (Color(0.95, 0.45, 0.32, 0.94) if kind == 3 else AMBER_CORE)
		var secondary: Color = POWERUP_JADE if kind != 2 else CYAN_SPARK
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, 0.72), 0.085, primary, 0.0)
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, 0.48), 0.060, secondary, 0.003)
		for i: int in 4:
			var ang: float = TAU * float(i) / 4.0 + 0.25
			var base: Vector3 = Vector3(cos(ang) * 0.58, 0.04, sin(ang) * 0.58)
			var tip: Vector3 = Vector3(cos(ang) * 0.68, 0.85, sin(ang) * 0.68)
			_vertical_shock_stroke(im, base, tip, 0.065, secondary, primary)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _stone_streak_mesh() -> ArrayMesh:
	var key: String = "stone_streak"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		_tapered_streak_plane(im, -1.75, 0.06, 0.135, false, INK_ESPRESSO, -0.002)
		_tapered_streak_plane(im, -1.48, 0.03, 0.084, false, AMBER_CORE, 0.0)
		_tapered_streak_plane(im, -1.00, 0.01, 0.038, false, CYAN_SPARK, 0.002)
		_tapered_streak_plane(im, -1.65, 0.05, 0.108, true, INK_ESPRESSO, 0.0)
		_tapered_streak_plane(im, -1.38, 0.02, 0.066, true, AMBER_CORE, 0.0)
		_tapered_streak_plane(im, -0.90, 0.0, 0.030, true, CYAN_SPARK, 0.0)
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


static func _bagsak_shock_mesh(radius: float) -> ArrayMesh:
	var safe_r: float = maxf(radius, 1.0)
	var key: String = "bagsak_shock|%.2f" % safe_r
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		for i: int in 8:
			var a: float = TAU * float(i) / 8.0 + 0.21
			var ring_r: float = safe_r * (0.78 if i % 2 == 0 else 0.42)
			var top_y: float = 1.95 if i % 2 == 0 else 1.40
			var base: Vector3 = Vector3(cos(a) * ring_r, 0.06, sin(a) * ring_r)
			var tip: Vector3 = Vector3(cos(a) * (ring_r + 0.24), top_y, sin(a) * (ring_r + 0.24))
			_vertical_shock_stroke(im, base, tip, 0.095, INK_ESPRESSO, BAGSAK_GOLD)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _sprint_ticks_mesh() -> ArrayMesh:
	var key: String = "sprint_ticks"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var z_list: Array[float] = [-0.26, 0.03, 0.27]
		var y_list: Array[float] = [0.22, 0.38, 0.28]
		var ink_faint: Color = Color(0.20, 0.15, 0.14, 0.52)
		var chalk_faint: Color = Color(0.95, 0.90, 0.78, 0.48)
		for i: int in 3:
			_wind_stroke_3d(im, -0.86 - float(i) * 0.11, -0.22, z_list[i], y_list[i], 0.038, ink_faint, chalk_faint)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _tapered_streak_plane(im: ImmediateMesh, tail_x: float, head_x: float, max_half: float, vertical: bool, col: Color, y_off: float) -> void:
	var steps: int = 8
	for i: int in steps:
		var t0: float = float(i) / float(steps)
		var t1: float = float(i + 1) / float(steps)
		var x0: float = lerpf(tail_x, head_x, t0)
		var x1: float = lerpf(tail_x, head_x, t1)
		var w0: float = max_half * sin(pow(t0, 0.72) * PI)
		var w1: float = max_half * sin(pow(t1, 0.72) * PI)
		var c0: Color = Color(col.r, col.g, col.b, col.a * clampf(t0 * 1.4, 0.0, 1.0))
		var c1: Color = Color(col.r, col.g, col.b, col.a * clampf(t1 * 1.4, 0.0, 1.0))
		var a: Vector3
		var b: Vector3
		var c: Vector3
		var d: Vector3
		if vertical:
			a = Vector3(x0, -w0, 0.0)
			d = Vector3(x0, w0, 0.0)
			b = Vector3(x1, -w1, 0.0)
			c = Vector3(x1, w1, 0.0)
		else:
			a = Vector3(x0, y_off, -w0)
			d = Vector3(x0, y_off, w0)
			b = Vector3(x1, y_off, -w1)
			c = Vector3(x1, y_off, w1)
		_add_quad_grad(im, a, b, c, d, c0, c1)


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


static func _add_tri_grad(im: ImmediateMesh, a: Vector3, tip: Vector3, d: Vector3, col_base: Color, col_tip: Color) -> void:
	im.surface_set_color(col_base)
	im.surface_add_vertex(a)
	im.surface_set_color(col_tip)
	im.surface_add_vertex(tip)
	im.surface_set_color(col_base)
	im.surface_add_vertex(d)


static func _add_quad_grad(im: ImmediateMesh, a: Vector3, b: Vector3, c: Vector3, d: Vector3, col0: Color, col1: Color) -> void:
	im.surface_set_color(col0)
	im.surface_add_vertex(a)
	im.surface_set_color(col1)
	im.surface_add_vertex(b)
	im.surface_set_color(col1)
	im.surface_add_vertex(c)
	im.surface_set_color(col0)
	im.surface_add_vertex(a)
	im.surface_set_color(col1)
	im.surface_add_vertex(c)
	im.surface_set_color(col0)
	im.surface_add_vertex(d)
