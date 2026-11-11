class_name FxHeroActions
extends Node3D
## Valorant-clear + Arcane-sketch gameplay VFX for the 4 street hero actions:
## 1. Tirador (Shape.SKILLSHOT): dark-rimmed amber-cyan stone streak + 3 floating Arcane charcoal sketch arcs on release & impact.
## 2. Luksong-Tinik Dash (sim.player_dashed): kinetic floating Arcane wind-sketch lines + low ground chalk/dust ribbon.
## 3. Bagsak Ultimate (sim.ultimate_cast): Valorant-style ground boundary ring (sharp outer edge, <= 0.20 inner alpha) + rising Arcane ink-sketch shock lines & golden dust burst.
## 4. Street Sprint (player.sprinting): subtle footstep dust puffs + faint speed-sketch ticks (respecting reduced_motion and turbo).
## Strictly read-only on MatchSim; all nodes and materials are pre-allocated once in setup().

const RING_SLOTS: int = 2
const STREAK_SLOTS: int = 4
const BURST_SLOTS: int = 8

const BAGSAK_INNER_ALPHA: float = 0.16
const BAGSAK_RIM_WIDTH: float = 0.22
const BAGSAK_INK_WIDTH: float = 0.09
const BAGSAK_RING_LIFE: float = 0.52
const BAGSAK_SHOCK_LIFE: float = 0.34
const TIRADOR_ARC_LIFE: float = 0.18
const TIRADOR_STREAK_FADE: float = 0.08
const DASH_WIND_LIFE: float = 0.25
const DASH_CHALK_LIFE: float = 0.36
const SPRINT_INTERVAL: float = 0.25
const SPRINT_TICK_LIFE: float = 0.16

const INK_CHARCOAL: Color = Color(0.08, 0.06, 0.10, 0.96)
const AMBER_CORE: Color = Color(0.98, 0.70, 0.18, 0.95)
const CYAN_SPARK: Color = Color(0.30, 0.90, 0.98, 0.98)
const CHALK_WARM: Color = Color(0.84, 0.79, 0.68, 0.56)
const WIND_CYAN: Color = Color(0.76, 0.94, 0.98, 0.90)
const BAGSAK_GOLD: Color = Color(0.98, 0.78, 0.24, 0.96)
const BAGSAK_ALLY_FILL: Color = Color(1.0, 0.88, 0.52, 1.0)
const SPRINT_DUST_TINT: Color = Color(0.86, 0.80, 0.70, 0.65)


class Slot extends RefCounted:
	var node: MeshInstance3D
	var material: StandardMaterial3D
	var active: bool = false
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
var _last_dash_fx: Dictionary[int, float] = {}
var _last_ult_fx: Dictionary[int, float] = {}
var _sprint_due: Dictionary[int, float] = {}
var _step_serial: int = 0


func setup(ctx: FxContext, zones_view: FxZones = null) -> void:
	_ctx = ctx
	_zones = zones_view
	_rings = _make_pool(RING_SLOTS, FxKit.PRIO_RIM, FxKit.flat_quad())
	_streaks = _make_pool(STREAK_SLOTS, FxKit.PRIO_DECAL, _stone_streak_mesh())
	_bursts = _make_pool(BURST_SLOTS, FxKit.PRIO_DECAL, _tirador_arcs_mesh(false))


static func clear_cache() -> void:
	_meshes.clear()


static func cached_count() -> int:
	return _meshes.size()


func _make_pool(count: int, priority: int, default_mesh: ArrayMesh) -> Array[Slot]:
	var pool: Array[Slot] = []
	for i: int in count:
		var slot: Slot = Slot.new()
		slot.node = MeshInstance3D.new()
		slot.node.name = "HeroFx_%d_%d" % [priority, i]
		slot.node.mesh = default_mesh
		slot.material = FxKit.slot_material(priority)
		slot.node.material_override = slot.material
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


func on_projectile_gone(def: WeaponDef, team: int, at: Vector3) -> void:
	if _ctx == null or def == null or def.shape != WeaponDef.Shape.SKILLSHOT:
		return
	var turbo: bool = Settings.performance == &"turbo"
	if not _ctx.reduced:
		var yaw: float = _next_phase() * TAU
		var y_safe: float = maxf(at.y - 0.22, 0.36)
		_activate_burst(_tirador_arcs_mesh(true), Vector3(at.x, y_safe, at.z), yaw, Vector3.ONE, TIRADOR_ARC_LIFE, 0.22, 0.08)
		if not turbo:
			_ctx.particles.puff(FxParticles.Preset.CHIPS, Vector3(at.x, 0.16, at.z), 2)
	var rim_col: Color = AMBER_CORE if team == _ctx.local_team else _ctx.team_color(team)
	_ctx.ground.ring(Vector2(at.x, at.z), 0.18, 0.56, Color(rim_col, 0.85), 0.16, _ctx.reduced)


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
	var flight_y: float = maxf(FxProjectiles.PROJECTILE_HEIGHT * _ctx.view_scale, 0.65)
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
				_activate_burst(_tirador_arcs_mesh(false), Vector3(drawn_pos.x, maxf(flight_y - 0.26, 0.36), drawn_pos.y), yaw_rel, Vector3.ONE, TIRADOR_ARC_LIFE, 0.18, 0.05)
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
			_update_streak(pid, Vector3(drawn_pos.x, flight_y, drawn_pos.y), _yaw_from_dir(use_dir))

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
	var present: Dictionary[int, bool] = {}
	for id: int in sim.players:
		present[id] = true
		var state: PlayerState = sim.players[id]
		var prev_d: float = _prev_dash[id] if _prev_dash.has(id) else 0.0
		_prev_dash[id] = state.dash_left
		var last_fx: float = _last_dash_fx[id] if _last_dash_fx.has(id) else -10.0
		if state.dash_left > prev_d + 0.02 and _ctx.time - last_fx > 0.12 and not state.tagged:
			_last_dash_fx[id] = _ctx.time
			_spawn_dash_fx(state)

		if state.sprinting and not state.tagged and not _ctx.reduced and not turbo:
			var due: float = _sprint_due[id] if _sprint_due.has(id) else 0.0
			if _ctx.time >= due:
				_sprint_due[id] = _ctx.time + SPRINT_INTERVAL
				var face: Vector2 = state.facing if state.facing.length_squared() > 0.001 else Vector2(0.0, -1.0)
				var yaw: float = _yaw_from_dir(face)
				var heel: Vector2 = state.position - face.normalized() * 0.32
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(heel.x, 0.0, heel.y), 2, SPRINT_DUST_TINT)
				_activate_burst(_sprint_ticks_mesh(), Vector3(state.position.x, 0.06, state.position.y), yaw, Vector3.ONE, SPRINT_TICK_LIFE, 0.08, 0.04)
		else:
			_sprint_due.erase(id)

	for old_id: int in _prev_dash.keys():
		if not present.has(old_id):
			_prev_dash.erase(old_id)
			_last_dash_fx.erase(old_id)
			_last_ult_fx.erase(old_id)
			_sprint_due.erase(old_id)


func _spawn_dash_fx(state: PlayerState) -> void:
	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	var dir: Vector2 = state.dash_dir if state.dash_dir.length_squared() > 0.001 else state.facing
	if dir.length_squared() < 0.001:
		dir = Vector2(0.0, -1.0)
	dir = dir.normalized()
	var yaw: float = _yaw_from_dir(dir)
	var pos: Vector2 = state.position
	# Low ground chalk/dust ribbon along the vault-dash path (zero camera-blocking smoke).
	_activate_burst(_dash_chalk_mesh(), Vector3(pos.x, FxKit.Y_DECAL, pos.y), yaw, Vector3.ONE, DASH_CHALK_LIFE, 0.14, 0.0)
	# Kinetic floating Arcane wind-sketch lines flanking waist & shoulders.
	var wind_scale: Vector3 = Vector3(0.85, 0.85, 0.85) if turbo else Vector3.ONE
	_activate_burst(_dash_wind_mesh(), Vector3(pos.x, 0.10, pos.y), yaw, wind_scale, DASH_WIND_LIFE, 0.16, 0.08)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(pos.x, 0.0, pos.y), 2, SPRINT_DUST_TINT)


func _spawn_bagsak_cast(center: Vector2, radius: float, team: int) -> void:
	var own: bool = team == _ctx.local_team
	var rim_col: Color = BAGSAK_GOLD if own else _ctx.team_color(team)
	var fill_col: Color = BAGSAK_ALLY_FILL if own else _ctx.team_color(team)
	var ring_slot: Slot = _take_slot(_rings)
	ring_slot.active = true
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
	ring_slot.material.albedo_color = Color.WHITE
	ring_slot.node.visible = true

	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	var yaw: float = _next_phase() * TAU
	_activate_burst(_bagsak_shock_mesh(radius), Vector3(center.x, 0.05, center.y), yaw, Vector3.ONE, BAGSAK_SHOCK_LIFE, 0.06, 0.45)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 8, BAGSAK_GOLD)


func _spawn_bagsak_slam(center: Vector2, radius: float, team: int) -> void:
	var own: bool = team == _ctx.local_team
	var rim_col: Color = BAGSAK_GOLD if own else _ctx.team_color(team)
	_ctx.ground.ring(center, radius * 0.35, radius, Color(rim_col, 0.90), 0.24, _ctx.reduced)
	_ctx.ground.crack(center, _next_phase() * TAU, &"spokes", Vector2(minf(radius * 0.45, 3.6), 1.0), 0.42)
	if _ctx.reduced:
		return
	var turbo: bool = Settings.performance == &"turbo"
	_activate_burst(_bagsak_shock_mesh(radius * 0.92), Vector3(center.x, 0.05, center.y), _next_phase() * TAU, Vector3(1.04, 1.18, 1.04), BAGSAK_SHOCK_LIFE, 0.08, 0.62)
	if not turbo:
		_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), 12, BAGSAK_GOLD)


func _update_streak(proj_id: int, world_pos: Vector3, yaw: float) -> void:
	var target: Slot = null
	for slot: Slot in _streaks:
		if slot.active and slot.proj_id == proj_id:
			target = slot
			break
	if target == null:
		target = _take_slot(_streaks)
		target.node.mesh = _stone_streak_mesh()
	target.active = true
	target.proj_id = proj_id
	target.born = _ctx.time
	target.life = 0.25
	target.base_pos = world_pos
	target.base_scale = Vector3.ONE
	target.grow = 0.0
	target.rise_y = 0.0
	target.node.position = world_pos
	target.node.rotation = Vector3(0.0, yaw, 0.0)
	target.node.scale = Vector3.ONE
	target.material.albedo_color = Color.WHITE
	target.node.visible = true


func _activate_burst(mesh: ArrayMesh, pos: Vector3, yaw: float, scale_vec: Vector3, life: float, grow: float, rise_y: float) -> void:
	var slot: Slot = _take_slot(_bursts)
	slot.active = true
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
	slot.material.albedo_color = Color.WHITE
	slot.node.visible = true


func _step_pool(pool: Array[Slot]) -> void:
	for slot: Slot in pool:
		if not slot.active:
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
		var alpha: float = 1.0 - smoothstep(0.35, 1.0, t)
		slot.material.albedo_color = Color(1.0, 1.0, 1.0, alpha)


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
		if slot.active:
			total += 1
	return total


## Maps a 2D XZ direction `(dx, dz)` so local `+X` of the mesh points along `(dx, 0, dz)`.
static func _yaw_from_dir(dir: Vector2) -> float:
	return atan2(-dir.y, dir.x)


func _next_phase() -> float:
	_step_serial = (_step_serial + 1) % 64
	return fmod(float(_step_serial) * 0.381966, 1.0)


# ---- Baked Valorant-Clear & Arcane-Sketch Meshes (built once, cached) ------------------------------------

## Valorant-style ground boundary ring: <= 0.20 inner fill alpha + sharp outer team/gold rim + dark komiks ink edge.
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
		AimIndicator.ribbon(im, rim_outline, BAGSAK_RIM_WIDTH, Color(rim_color.r, rim_color.g, rim_color.b, 0.96), FxKit.Y_RIM)
		var ink_r: float = safe_r + BAGSAK_RIM_WIDTH * 0.5 + BAGSAK_INK_WIDTH * 0.45
		AimIndicator.ribbon(im, AimIndicator.circle(Vector2.ZERO, ink_r), BAGSAK_INK_WIDTH, INK_CHARCOAL, FxKit.Y_RIM + 0.003)
		for i: int in 8:
			var a: float = TAU * float(i) / 8.0
			var dir: Vector2 = Vector2(cos(a), sin(a))
			var tick_len: float = 0.52 if i % 2 == 0 else 0.30
			var pts: PackedVector2Array = PackedVector2Array([dir * (safe_r - tick_len), dir * safe_r])
			AimIndicator.ribbon(im, pts, 0.09, Color(rim_color.r, rim_color.g, rim_color.b, 0.92), FxKit.Y_RIM + 0.002)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _stone_streak_mesh() -> ArrayMesh:
	var key: String = "stone_streak"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		# Dual-plane crossed ribbons along -X..0 so the streak reads cleanly from both 3rd-person and top-down angles.
		_tapered_streak_plane(im, -1.85, 0.06, 0.145, false, INK_CHARCOAL, -0.002)
		_tapered_streak_plane(im, -1.55, 0.03, 0.088, false, AMBER_CORE, 0.0)
		_tapered_streak_plane(im, -1.05, 0.01, 0.040, false, CYAN_SPARK, 0.002)
		_tapered_streak_plane(im, -1.75, 0.05, 0.115, true, INK_CHARCOAL, 0.0)
		_tapered_streak_plane(im, -1.45, 0.02, 0.070, true, AMBER_CORE, 0.0)
		_tapered_streak_plane(im, -0.95, 0.0, 0.032, true, CYAN_SPARK, 0.0)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


## 3 floating Arcane charcoal sketch arcs (release fan or radial impact burst). All local y >= 0.04.
static func _tirador_arcs_mesh(impact: bool) -> ArrayMesh:
	var key: String = "tirador_impact" if impact else "tirador_release"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		if impact:
			var angles: Array[float] = [0.18, 2.27, 4.36]
			var radii: Array[float] = [0.58, 0.70, 0.63]
			var heights: Array[float] = [0.10, 0.26, 0.18]
			for i: int in 3:
				var accent: Color = CYAN_SPARK if i == 1 else AMBER_CORE
				_sketch_crescent(im, angles[i], 1.15, radii[i], heights[i], 0.12, 0.085, INK_CHARCOAL, accent)
		else:
			var rel_angles: Array[float] = [-0.66, 0.06, 0.72]
			var rel_radii: Array[float] = [0.60, 0.82, 0.66]
			var rel_heights: Array[float] = [0.10, 0.28, 0.18]
			for i: int in 3:
				var rel_accent: Color = CYAN_SPARK if i == 1 else AMBER_CORE
				_sketch_crescent(im, rel_angles[i], 1.05, rel_radii[i], rel_heights[i], 0.10, 0.080, INK_CHARCOAL, rel_accent)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _dash_chalk_mesh() -> ArrayMesh:
	var key: String = "dash_chalk"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var spine: PackedVector2Array = PackedVector2Array([
			Vector2(-2.55, 0.0), Vector2(-1.75, 0.05), Vector2(-0.85, -0.04), Vector2(0.15, 0.0)
		])
		AimIndicator.ribbon(im, spine, 0.68, CHALK_WARM, 0.0)
		for i: int in 6:
			var side: float = -1.0 if i % 2 == 0 else 1.0
			var x0: float = -2.35 + float(i) * 0.42
			var z0: float = side * (0.28 + float(i % 3) * 0.06)
			var tick: PackedVector2Array = PackedVector2Array([
				Vector2(x0 - 0.34, z0 + side * 0.09),
				Vector2(x0 + 0.18, z0),
			])
			AimIndicator.ribbon(im, tick, 0.065, INK_CHARCOAL, 0.003)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _dash_wind_mesh() -> ArrayMesh:
	var key: String = "dash_wind"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var z_offsets: Array[float] = [-0.54, 0.54, -0.78, 0.78]
		var y_bases: Array[float] = [0.48, 0.62, 0.92, 1.12]
		var x_starts: Array[float] = [-2.10, -1.85, -1.65, -1.95]
		var x_ends: Array[float] = [0.55, 0.68, 0.42, 0.48]
		for i: int in 4:
			_wind_stroke_3d(im, x_starts[i], x_ends[i], z_offsets[i], y_bases[i], 0.075, INK_CHARCOAL, WIND_CYAN)
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
			var top_y: float = 2.15 if i % 2 == 0 else 1.55
			var base: Vector3 = Vector3(cos(a) * ring_r, 0.08, sin(a) * ring_r)
			var tip: Vector3 = Vector3(cos(a) * (ring_r + 0.28), top_y, sin(a) * (ring_r + 0.28))
			_vertical_shock_stroke(im, base, tip, 0.11, INK_CHARCOAL, BAGSAK_GOLD)
		_meshes[key] = FxKit.bake(im)
	return _meshes[key]


static func _sprint_ticks_mesh() -> ArrayMesh:
	var key: String = "sprint_ticks"
	if not _meshes.has(key):
		var im: ImmediateMesh = ImmediateMesh.new()
		im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		var z_list: Array[float] = [-0.28, 0.03, 0.29]
		var y_list: Array[float] = [0.24, 0.42, 0.30]
		var ink_faint: Color = Color(0.10, 0.08, 0.12, 0.62)
		var chalk_faint: Color = Color(0.92, 0.86, 0.72, 0.50)
		for i: int in 3:
			_wind_stroke_3d(im, -0.92 - float(i) * 0.12, -0.24, z_list[i], y_list[i], 0.042, ink_faint, chalk_faint)
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
		var width_scale: float = 1.0 if layer == 0 else 0.46
		var r_off: float = 0.0 if layer == 0 else 0.012
		var y_bias: float = 0.0 if layer == 0 else 0.006
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
		var scale_w: float = 1.0 if layer == 0 else 0.45
		var y_nudge: float = 0.0 if layer == 0 else 0.004
		for i: int in steps:
			var s0: float = float(i) / float(steps)
			var s1: float = float(i + 1) / float(steps)
			var prof0: float = sin(s0 * PI)
			var prof1: float = sin(s1 * PI)
			var p0: Vector3 = Vector3(lerpf(x0, x1, s0), y_base + 0.14 * prof0 + y_nudge, z_base * (1.0 - 0.18 * s0))
			var p1: Vector3 = Vector3(lerpf(x0, x1, s1), y_base + 0.14 * prof1 + y_nudge, z_base * (1.0 - 0.18 * s1))
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
		var core_w: float = half_w * 0.45
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
