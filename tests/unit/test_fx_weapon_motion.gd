extends GutTest
## Every skill object moves like the toy it is (docs/ART.md "Skill effects"): the rock tumbles and stretches, the scissors
## snip, the slipper spins and comes back, the marble bounces, the top wobbles on its tip, the can tumbles to its circle, the
## trap and jacks land and the walls rise and fall. Reduce motion keeps the travel and drops the motion. Facts, not pixels.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _motion: bool
var _sim: MatchSim
var _fx: WeaponFxView


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


## A sim with you (id 1, team 0) at (0, 8) facing north, an enemy (2) at `enemy` and a friend (3) beside you, and a view.
func _setup(enemy: Vector2 = Vector2(0.0, 4.0)) -> void:
	_sim = MatchSim.new(_rules, LAYOUT, 3)
	_sim.add_player(1, 0)
	_sim.add_player(2, 1)
	_sim.add_player(3, 0)
	_sim.players[1].position = Vector2(0.0, 8.0)
	_sim.players[1].facing = Vector2(0.0, -1.0)
	_sim.players[2].position = enemy
	_sim.players[3].position = Vector2(3.0, 9.0)
	_sim.phase = MatchSim.Phase.PLAYING
	_fx = WeaponFxView.new()
	add_child_autofree(_fx)
	_fx.view_scale = 2.0
	_fx.local_team = 0
	_fx.watch(_sim)


func _cast(weapon: StringName, aim: Vector2, hold: float = 0.0, caster: int = 1) -> void:
	var def: WeaponDef = _sim.weapon_defs[weapon]
	assert_true(_sim.weapons.fire(_sim, _sim.players[caster], def, aim, hold, 2), "%s is cast" % weapon)
	_sim.weapon_cast.emit(caster, weapon)


## One 30 Hz frame: the sim steps, the view draws.
func _frame() -> void:
	_sim.step(DT)
	_fx.sync(_sim, DT)


func _rig(id: int) -> Node3D:
	return _fx.projectiles.node_for(id)


func _part(holder: Node3D, path: String) -> Node3D:
	return holder.get_node(path) as Node3D


# ---- bato light (the rock) --------------------------------------------------------------------------------------------

func test_the_rock_tumbles_and_stretches() -> void:
	_setup()
	_cast(&"bato_light", Vector2.ZERO)
	_fx.sync(_sim, 0.0)
	var id: int = _sim.weapons.projectiles[0].id
	for i: int in 5:
		_frame()
	var rock: Node3D = _rig(id)
	assert_ne(_part(rock, "Squash/Spin").basis, Basis.IDENTITY, "it tumbles")
	var along: Vector3 = _part(rock, "Squash").basis * Vector3(0.0, 0.0, -1.0)
	assert_almost_eq(along.length(), 1.30, 0.01, "stretched 1.30 along its flight at 14 m/s")
	while not _sim.weapons.projectiles.is_empty():
		_frame()
	assert_gt(_fx.projectiles.fading_count(), 0, "the hit plays a short squash")
	assert_gt(_fx.context().particles.busy_emitters(), 0, "and throws chips")
	for i: int in 3:
		_frame()
	assert_eq(_fx.projectiles.fading_count(), 0, "gone two frames later")


func test_rm_rock_is_still() -> void:
	Settings.reduced_motion = true
	_setup()
	_cast(&"bato_light", Vector2.ZERO)
	_fx.sync(_sim, 0.0)
	var id: int = _sim.weapons.projectiles[0].id
	var spin: Basis = _part(_rig(id), "Squash/Spin").basis
	for i: int in 5:
		_frame()
		assert_eq(_part(_rig(id), "Squash").basis, Basis.IDENTITY, "no stretch or pop")
		assert_eq(_part(_rig(id), "Squash/Spin").basis, spin, "no tumble")
	assert_eq(_fx.particle_count(), 0, "no puffs")
	assert_not_null(_fx.context().shadows.node_of(id), "the shadow stays: it is information")


# ---- gunting (the scissors) -------------------------------------------------------------------------------------------

func test_scissors_split_keeps_every_triangle() -> void:
	var halves: Array[ArrayMesh] = ScissorsRig.halves()
	var source: int = ScissorsRig.triangles(WeaponModel.mesh_of(ScissorsRig.MODEL))
	assert_eq(ScissorsRig.triangles(halves[0]) + ScissorsRig.triangles(halves[1]), source, "no triangle lost or doubled")
	assert_gt(ScissorsRig.triangles(halves[0]), 400, "blade A is a real blade")
	assert_gt(ScissorsRig.triangles(halves[1]), 400, "and so is blade B")
	assert_same(ScissorsRig.halves()[0], halves[0], "split once")


func test_snip_period_table() -> void:
	var expected: Dictionary[int, int] = {2: 5, 3: 5, 4: 4, 6: 2}
	for n: int in expected:
		assert_eq(FxMotion.snip_period(n), expected[n], "%d snips" % n)
		assert_lte(n * FxMotion.snip_period(n), 16, "all %d snips fit in about half a second" % n)


func _snips_for(hold: float) -> int:
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_light", Vector2(0.0, -1.0), hold)
	_fx.sync(_sim, DT)
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var closes: int = 0
	var last: float = rig.angle()
	for i: int in 20:
		_frame()
		if not is_instance_valid(rig) or _fx.cones.scissors().is_empty():
			break
		if rig.angle() <= ScissorsRig.CLOSED and last > ScissorsRig.CLOSED:
			closes += 1
		last = rig.angle()
	return closes


func test_light_snips_n_times() -> void:
	var def: WeaponDef = _rules.weapons.filter(func(d: WeaponDef) -> bool: return d.id == &"gunting_light")[0] as WeaponDef
	assert_eq(_snips_for(0.0), def.snip_count(0.0), "a tap snips the least")
	var long: float = 10.0
	assert_eq(_snips_for(long), def.snip_count(long), "a long hold snips the most")
	assert_eq(def.snip_count(long), def.snips_max)


func test_scissors_keep_clear_of_kids() -> void:
	for distance: float in [3.0, 2.5]:
		_setup(Vector2(0.0, 8.0 - distance))
		var def: WeaponDef = _sim.weapon_defs[&"gunting_light"]
		var place: Vector2 = _fx.cones.placement(1, def, Vector2(0.0, 8.0), Vector2(0.0, -1.0))
		var handles: float = place.y - place.x / 2.0
		var tips: float = place.y + place.x / 2.0 + FxCones.SNIP_LUNGE_STEP
		assert_gte(handles, FxCones.SNIP_CASTER_CLEAR - 0.001, "handles clear of the caster (%.1f m)" % distance)
		assert_lte(tips, distance - FxCones.SNIP_TARGET_CLEAR + 0.001, "tips short of the kid at %.1f m, lunge included" % distance)
	_setup(Vector2(0.0, -20.0))
	var heavy: WeaponDef = _sim.weapon_defs[&"gunting_heavy"]
	var free: Vector2 = _fx.cones.placement(1, heavy, Vector2(0.0, 8.0), Vector2(0.0, -1.0))
	assert_lte(free.y + free.x / 2.0 + FxCones.HEAVY_LUNGE, heavy.max_range, "with nobody there they stay inside the range")


func test_heavy_opens_during_the_windup_and_closes_on_the_strike() -> void:
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var start: float = rig.angle()
	var widest: float = start
	var struck: bool = false
	for i: int in 20:
		_frame()
		if _fx.cones.windup_count() == 0 and not struck:
			struck = true
			assert_eq(rig.angle(), ScissorsRig.CLOSED, "shut in one frame on the strike")
			break
		widest = maxf(widest, rig.angle())
	assert_true(struck, "the strike came")
	assert_gt(widest, ScissorsRig.WIDE_HEAVY - TREMBLE_SLACK, "they opened wide during the wind-up")
	assert_lt(start, widest, "opening, not born open")


const TREMBLE_SLACK: float = 3.0


func test_rm_scissors_static() -> void:
	Settings.reduced_motion = true
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_light", Vector2(0.0, -1.0), 10.0)
	_fx.sync(_sim, DT)
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var place: Vector3 = (rig.get_parent().get_parent() as Node3D).position
	for i: int in 6:
		_frame()
		assert_eq(rig.angle(), ScissorsRig.CLOSED, "closed and still")
		assert_eq((rig.get_parent().get_parent() as Node3D).position, place, "no lunge")
	assert_eq(_fx.context().ground.live_smears(), 0, "no smear")
	assert_eq(_fx.particle_count(), 0, "no sparks")


# ---- tsinelas (the slippers) ------------------------------------------------------------------------------------------

func _spin_of(id: int) -> float:
	return (_part(_rig(id), "Squash/Tilt/Spin").basis.get_euler()).y


func test_slipper_spin_reverses_on_the_way_back() -> void:
	_setup(Vector2(20.0, 8.0))
	_cast(&"tsinelas_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	var signs: Array[float] = []
	var last: float = _spin_of(id)
	for i: int in 60:
		_frame()
		if _sim.weapons.projectiles.is_empty():
			break
		var now: float = _spin_of(id)
		var step: float = wrapf(now - last, -PI, PI)
		if absf(step) > 0.001:
			var sign: float = signf(step)
			if signs.is_empty() or signs.back() != sign:
				signs.append(sign)
		last = now
	assert_eq(signs.size(), 2, "it spins one way out and the other way back %s" % str(signs))


func test_slap_squashes_the_nearest_slipper() -> void:
	_setup(Vector2(0.0, 5.0))
	_cast(&"tsinelas_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	var slapped: bool = false
	for i: int in 20:
		_frame()
		if _fx.context().ground.live_smears() > 0:
			var along: Vector3 = _part(_rig(id), "Squash").basis * Vector3(0.0, 0.0, -1.0)
			assert_almost_eq(along.length(), FxProjectiles.SLAP_S, 0.05, "stretched on the slap")
			slapped = true
			break
	assert_true(slapped, "the hit kid got a smack streak from the slipper")


func test_catch_shrinks_into_the_hand() -> void:
	_setup(Vector2(20.0, 8.0))
	_cast(&"tsinelas_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var caught: bool = false
	for i: int in 120:
		_frame()
		if _sim.weapons.projectiles.is_empty():
			caught = _fx.projectiles.fading_count() == 1
			break
	assert_true(caught, "the slipper came back and is shrinking into the hand")
	for f: int in FxProjectiles.CATCH_FRAMES + 1:
		_frame()
	assert_eq(_fx.projectiles.fading_count(), 0, "gone into the hand within four frames")


func test_rm_slipper_no_spin() -> void:
	Settings.reduced_motion = true
	_setup(Vector2(20.0, 8.0))
	_cast(&"tsinelas_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	_frame()
	var spin: Basis = _part(_rig(id), "Squash/Tilt/Spin").basis
	for i: int in 10:
		_frame()
		assert_eq(_part(_rig(id), "Squash").basis, Basis.IDENTITY, "no stretch")
		assert_true(_part(_rig(id), "Squash/Tilt/Spin").basis.is_equal_approx(spin), "no spin: a fixed tilt along its flight")
	assert_eq(_fx.particle_count(), 0)


# ---- bola (the marble) ------------------------------------------------------------------------------------------------

func _bottom(id: int) -> float:
	var marble: Node3D = _rig(id)
	var radius: float = _fx.projectiles.drawn_width(_sim.weapon_defs[&"bola"]) / 2.0
	return marble.position.y - radius * _part(marble, "Squash").basis.get_scale().y


func test_the_marble_touches_the_ground_at_each_bounce() -> void:
	_setup(Vector2(30.0, 0.0))
	_cast(&"bola", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var projectile: WeaponSystem.Projectile = _sim.weapons.projectiles[0]
	var id: int = projectile.id
	var bounces: int = 0
	var highest: float = 0.0
	var touched: int = 0
	while not _sim.weapons.projectiles.is_empty():
		_frame()
		if _sim.weapons.projectiles.is_empty():
			break
		var bottom: float = _bottom(id)
		assert_gte(bottom, -0.001, "never below the street")
		highest = maxf(highest, bottom)
		if projectile.bounces != bounces:
			bounces = projectile.bounces
			assert_lt(bottom, 0.05, "on the street on the bounce frame")
			touched += 1
	assert_eq(touched, 1, "the first bounce touched down while still in the sim (the last ends it)")
	assert_gt(highest, 1.5, "it arcs high between bounces")
	assert_gt(_fx.projectiles.fading_count(), 0, "after the last bounce it runs on")


func test_bounce_rings_mark_the_nominal_points_and_go() -> void:
	_setup(Vector2(30.0, 0.0))
	_cast(&"bola", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	_frame()
	assert_eq(_fx.projectiles.bounce_rings(), 2, "both bounce points marked from the start")
	var def: WeaponDef = _sim.weapon_defs[&"bola"]
	for ring: Node in _fx.projectiles.find_children("BounceRing", "MeshInstance3D", true, false):
		var at: Vector2 = Vector2((ring as Node3D).position.x, (ring as Node3D).position.z)
		var along: float = 8.0 - at.y
		assert_almost_eq(fmod(along, def.max_range / def.count), 0.0, 0.01, "on a nominal point (the aim preview's)")
		assert_almost_eq(at.x, 0.0, 0.01)
	var projectile: WeaponSystem.Projectile = _sim.weapons.projectiles[0]
	while projectile.bounces < 1:
		_frame()
	assert_eq(_fx.projectiles.bounce_rings(), 1, "the first goes on its bounce")
	while not _sim.weapons.projectiles.is_empty():
		_frame()
	assert_eq(_fx.projectiles.bounce_rings(), 0, "and the second on the last")
	assert_gt(_fx.context().ground.live_flashes(), 0, "each flashes as it goes")


func test_rm_marble_flat() -> void:
	Settings.reduced_motion = true
	_setup(Vector2(30.0, 0.0))
	_cast(&"bola", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	var radius: float = _fx.projectiles.drawn_width(_sim.weapon_defs[&"bola"]) / 2.0
	var spin: Basis = _part(_rig(id), "Squash/Spin").basis
	for i: int in 15:
		_frame()
		assert_almost_eq(_rig(id).position.y, radius, 0.001, "rolls flat on the street")
		assert_eq(_part(_rig(id), "Squash/Spin").basis, spin, "without turning")
	assert_eq(_fx.particle_count(), 0)


# ---- trumpo (the spinning top) ----------------------------------------------------------------------------------------

## The lowest point of the top's model on screen right now (its tip, vertex by vertex).
func _lowest(holder: Node3D) -> float:
	var lowest: float = INF
	for node: Node in holder.find_children("*", "MeshInstance3D", true, false):
		var mesh: MeshInstance3D = node as MeshInstance3D
		var xform: Transform3D = mesh.global_transform
		for vertex: Vector3 in mesh.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array:
			lowest = minf(lowest, (xform * vertex).y)
	return lowest


func _lean(holder: Node3D) -> float:
	return rad_to_deg(_part(holder, "Squash").basis.y.normalized().angle_to(Vector3.UP))


func test_the_top_stands_on_its_tip() -> void:
	_setup(Vector2(30.0, 0.0))
	_cast(&"trumpo", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	for frame: int in 21:
		_frame()
		if frame == 5 or frame == 20:
			var tip: float = _lowest(_rig(id))
			assert_between(tip, -0.01, 0.05, "the tip is on the street at frame %d (%.3f)" % [frame, tip])


func test_the_top_wobbles() -> void:
	_setup(Vector2(30.0, 0.0))
	_cast(&"trumpo", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	for i: int in 8:
		_frame()
	var axes: Array[Vector3] = []
	for i: int in 5:
		_frame()
		assert_between(_lean(_rig(id)), 6.0, 10.0, "it leans about 8 degrees")
		axes.append(_part(_rig(id), "Squash").basis.y)
	assert_false(axes[0].is_equal_approx(axes[4]), "and the lean goes round (a wobble, not a fixed tilt)")


func test_the_top_tips_over_at_the_end() -> void:
	_setup(Vector2(30.0, 0.0))
	_cast(&"trumpo", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	while not _sim.weapons.projectiles.is_empty():
		_frame()
	assert_eq(_fx.projectiles.fading_count(), 1, "at the end of its run it tips over")
	for i: int in 9:
		_frame()
	assert_eq(_fx.projectiles.fading_count(), 0, "and is gone within nine frames")
	assert_eq(_fx.context().particles.trail_take() != null, true, "its dust trail was given back")


func test_rm_top_upright() -> void:
	Settings.reduced_motion = true
	_setup(Vector2(30.0, 0.0))
	_cast(&"trumpo", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var id: int = _sim.weapons.projectiles[0].id
	for i: int in 10:
		_frame()
		assert_eq(_part(_rig(id), "Squash").basis, Basis.IDENTITY, "upright, no wobble or squash")
		assert_almost_eq(_rig(id).position.y, 0.0, 0.0001, "on the street")
	assert_eq(_fx.particle_count(), 0, "no trail")


# ---- lata (the can) ---------------------------------------------------------------------------------------------------

func test_the_can_flies_from_the_thrower_to_the_circle() -> void:
	_setup(Vector2(0.0, 2.0))
	_cast(&"lata", Vector2(0.0, -6.0 / 7.0))
	_fx.sync(_sim, DT)
	var zone: WeaponSystem.Zone = _sim.weapons.zones[0]
	var can: Node3D = _fx.zones.node_for(zone.id).get_node("Can") as Node3D
	var first: Vector3 = can.global_position
	assert_gt(first.z, zone.center.y + 4.0, "it starts at the thrower, not on the circle")
	var peak: float = 0.0
	var last: Vector3 = first
	while not _sim.weapons.zones.is_empty():
		last = can.global_position
		peak = maxf(peak, last.y)
		_frame()
	assert_gt(peak, 2.2, "it arcs over")
	assert_almost_eq(Vector2(last.x, last.z).distance_to(zone.center), 0.0, 0.05, "and comes down on the circle's centre")
	assert_almost_eq(last.y, _fx.zones.can_height() / 2.0, 0.05, "upright on its base when the zone lands")
	assert_gt(_fx.zones.fading_count(), 0, "the landing plays")


func test_polymorph_on_and_off_puff() -> void:
	_setup(Vector2(0.0, 2.0))
	_cast(&"lata", Vector2(0.0, -6.0 / 7.0))
	var smoke: int = 0
	var turned: bool = false
	var was: Array[CPUParticles3D] = []
	for i: int in 120:
		_frame()
		var now: Array[CPUParticles3D] = []
		for emitter: CPUParticles3D in _fx.context().particles.busy():
			if emitter.mesh == FxKit.puff_quad() and emitter.amount == FxZones.SMOKE_PUFF and emitter.color.is_equal_approx(Palette.WHITE):
				now.append(emitter)
				smoke += 0 if was.has(emitter) else 1
		was = now
		if _sim.players[2].effects.has(StatusEffects.Type.POLYMORPH):
			turned = true
		elif turned:
			break
	assert_true(turned, "the kid became a can")
	assert_eq(smoke, 2, "one smoke puff when it turned into a can, one when it turned back")


# ---- papel trap and jacks ---------------------------------------------------------------------------------------------

func test_trap_arms_over_arm_time() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"papel_trap", Vector2(0.0, -4.0 / 6.0))
	_fx.sync(_sim, DT)
	var zone: WeaponSystem.Zone = _sim.weapons.zones[0]
	var node: Node3D = _fx.zones.node_for(zone.id)
	var disc: Node3D = node.get_node("Progress") as Node3D
	var frames: int = 0
	while disc.visible and frames < 60:
		_frame()
		frames += 1
	assert_almost_eq(float(frames) * DT, zone.def.arm_time, 2.0 * DT, "the arming fill fills over the arming time")
	var rim: float = 0.0
	for color: Color in ((node.get_node("Mark") as MeshInstance3D).mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray):
		rim = maxf(rim, color.a)
	assert_almost_eq(rim, FxKit.RIM_ALPHA, 0.01, "armed: a solid rim")


func test_trap_springs_into_a_team_slow_zone() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"papel_trap", Vector2(0.0, -4.0 / 6.0))
	for i: int in 30:
		_frame()
	var zone: WeaponSystem.Zone = _sim.weapons.zones[0]
	_sim.players[2].position = zone.center
	_frame()
	assert_eq(zone.kind, WeaponSystem.ZoneKind.SLOW_ZONE, "the trap sprang")
	assert_gt(_fx.zones.fading_count(), 0, "the ball springs up")
	assert_gt(_fx.context().particles.busy_emitters(), 0, "into paper bits")
	var fill: Node3D = _fx.zones.node_for(zone.id).get_node("Fill") as Node3D
	assert_lt(fill.scale.x, 1.0, "the slow zone's fill pops out")
	for i: int in FxZones.FILL_POP_FRAMES + 1:
		_frame()
	assert_almost_eq(fill.scale.x, 1.0, 0.001, "to its whole radius")
	var mark: MeshInstance3D = _fx.zones.node_for(zone.id).get_node("Mark") as MeshInstance3D
	var color: Color = (mark.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray)[0]
	var own: Color = TeamColors.world_own()
	assert_lt(absf(color.r - own.r) + absf(color.g - own.g) + absf(color.b - own.b), 0.02, "in the thrower's colour")
	while zone.time_left > FxZones.SLOW_FADE / 2.0:
		_frame()
	assert_lt((mark.material_override as StandardMaterial3D).albedo_color.a, 1.0, "it fades in its last moments")


func test_jacks_scatter_then_rest_on_the_ground() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"jacks", Vector2(0.0, -4.0 / 6.0))
	_fx.sync(_sim, DT)
	var spikes: Node3D = _fx.zones.node_for(_sim.weapons.zones[0].id).get_node("Spikes") as Node3D
	var flying: int = 0
	for i: int in 3:
		_frame()
		for child: Node in spikes.get_children():
			flying += 1 if (child as Node3D).position.y > 0.2 else 0
	assert_gt(flying, 0, "they are tossed, not placed")
	for i: int in 10:
		_frame()
	for child: Node in spikes.get_children():
		assert_almost_eq((child as Node3D).position.y, 0.0, 0.001, "every cluster rests on the street")
		assert_true((child as Node3D).visible)


func test_rm_trap_and_jacks_static() -> void:
	Settings.reduced_motion = true
	_setup(Vector2(20.0, 0.0))
	_cast(&"papel_trap", Vector2(0.0, -4.0 / 6.0))
	_cast(&"jacks", Vector2(0.6, -4.0 / 6.0))
	_fx.sync(_sim, DT)
	for i: int in 40:
		_frame()
		for zone: WeaponSystem.Zone in _sim.weapons.zones:
			var node: Node3D = _fx.zones.node_for(zone.id)
			var ball: Node3D = node.get_node_or_null("Ball") as Node3D
			if ball != null:
				assert_eq((ball.get_node("Squash") as Node3D).basis, Basis.IDENTITY, "no toss, squash or breathing")
			var spikes: Node3D = node.get_node_or_null("Spikes") as Node3D
			if spikes != null:
				for child: Node in spikes.get_children():
					assert_almost_eq((child as Node3D).position.y, 0.0, 0.001, "placed, no toss or hop")
	assert_eq(_fx.particle_count(), 0)
