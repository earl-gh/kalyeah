extends GutTest
## QA of the weapon skill effects: lifecycle (restart, owner gone, zone removed the frame it appears, a flood of casts), the
## online path (snapshots only, late joiner, dropped snapshots), Reduce motion toggled mid-effect, the viewer on either team,
## the ground marks against the sim's true hit areas, and that the view never changes the sim.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const ALL: Array[StringName] = [&"bato_light", &"bato_heavy", &"gunting_light", &"gunting_heavy", &"papel_trap", &"papel_shield", &"tsinelas_light", &"tsinelas_heavy", &"lata", &"jacks", &"bola", &"trumpo", &"heal", &"langit_lupa"]

var _rules: GameRules
var _motion: bool
var _blind: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	_blind = Settings.colorblind
	Settings.reduced_motion = false
	Settings.set_colorblind(false)


func after_each() -> void:
	Settings.reduced_motion = _motion
	Settings.set_colorblind(_blind)


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.add_player(3, 0)
	sim.add_player(4, 1)
	sim.players[1].position = Vector2(0.0, 8.0)
	sim.players[1].facing = Vector2(0.0, -1.0)
	sim.players[2].position = Vector2(0.0, 4.0)
	sim.players[3].position = Vector2(-3.0, 8.0)
	sim.players[4].position = Vector2(3.0, 3.0)
	sim.phase = MatchSim.Phase.PLAYING
	return sim


func _fx(sim: MatchSim, team: int = 0) -> WeaponFxView:
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	fx.view_scale = 2.0
	fx.local_team = team
	fx.watch(sim)
	return fx


func _cast(sim: MatchSim, id: int, weapon: StringName) -> bool:
	var caster: PlayerState = sim.players[id]
	var def: WeaponDef = sim.weapon_defs[weapon]
	var target: PlayerState = sim.nearest_enemy(caster, 30.0)
	var aim: Vector2 = caster.facing
	if target != null:
		aim = ((target.position - caster.position) / maxf(def.max_range, 0.01)).limit_length(1.0)
	if def.shape == WeaponDef.Shape.HEAL:
		aim = Vector2.ZERO
	var fired: bool = sim.weapons.fire(sim, caster, def, aim, 1.0, target.id if target != null else -1)
	if fired:
		sim.weapon_cast.emit(id, weapon)
	return fired


func _run(sim: MatchSim, fx: WeaponFxView, frames: int) -> void:
	for i: int in frames:
		sim.step(DT)
		fx.sync(sim, DT)


func _idle(fx: WeaponFxView) -> void:
	var ctx: FxContext = fx.context()
	assert_eq(fx.live_count(), 0, "no sim object drawn")
	assert_eq(fx.fading_count(), 0, "no ghost left")
	assert_eq(fx.windup_count(), 0, "no warning left")
	assert_eq(fx.burst_count(), 0, "no burst left")
	assert_eq(fx.particle_count(), 0, "no particle live (so every trail is back)")
	assert_eq(ctx.shadows.live(), 0, "no shadow attached")
	assert_eq(ctx.ground.live_rings(), 0, "rings")
	assert_eq(ctx.ground.live_flashes(), 0, "flashes")
	assert_eq(ctx.ground.live_smears(), 0, "smears")
	assert_eq(ctx.ground.live_cracks(), 0, "cracks")
	assert_eq(ctx.ground.live_fades(), 0, "fades")


## The pooled nodes the view always owns, and the nodes it has beyond those (must come back to the same count).
func _node_count(fx: WeaponFxView) -> int:
	return fx.find_children("*", "", true, false).filter(func(n: Node) -> bool: return not n.is_queued_for_deletion()).size()


# ---- 1. lifecycle -----------------------------------------------------------------------------

func test_every_weapon_cast_ends_clean_and_returns_the_node_count() -> void:
	var sim: MatchSim = _sim()
	var fx: WeaponFxView = _fx(sim)
	var base: int = _node_count(fx)
	for weapon: StringName in ALL:
		sim.players[1].hp = 40
		sim.players[1].elixir = 10.0
		assert_true(_cast(sim, 1, weapon), "%s fires" % weapon)
		_run(sim, fx, 200)
		await get_tree().process_frame
		assert_eq(_node_count(fx), base, "%s: back to the pooled nodes only" % weapon)
	_idle(fx)


func test_thirty_casts_in_one_second_stay_bounded_and_end_clean() -> void:
	var sim: MatchSim = _sim()
	var fx: WeaponFxView = _fx(sim)
	var base: int = _node_count(fx)
	var most: int = 0
	var ctx: FxContext = fx.context()
	for frame: int in 30:
		for id: int in [1, 2, 3, 4]:
			sim.players[id].hp = 50
			if frame % 4 == id - 1:
				_cast(sim, id, ALL[(frame + id) % ALL.size()])
		_run(sim, fx, 1)
		most = maxi(most, _node_count(fx))
		assert_lte(ctx.particles.live_particles(), FxParticles.LIVE_CAP)
		assert_lte(ctx.shadows.live(), FxShadows.SLOTS)
	gut.p("30-frame flood: most nodes %d over %d pooled" % [most, base])
	assert_lt(most, base + 400, "bounded")
	sim.weapons.reset()
	_run(sim, fx, 240)
	await get_tree().process_frame
	_idle(fx)
	assert_eq(_node_count(fx), base, "and every node it made is freed")


func test_the_view_freed_mid_effect_leaves_nothing_behind() -> void:
	var sim: MatchSim = _sim()
	var orphans: int = int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT))
	var fx: WeaponFxView = WeaponFxView.new()
	add_child(fx)
	fx.view_scale = 2.0
	fx.watch(sim)
	for weapon: StringName in ALL:
		sim.players[1].elixir = 10.0
		_cast(sim, 1, weapon)
		_run(sim, fx, 3)
	remove_child(fx)
	fx.free()
	# the sim keeps running and emitting signals after the view is gone (a restart): no call into a freed view
	sim.weapon_cast.emit(1, &"heal")
	sim.player_damaged.emit(2, 9)
	sim.player_healed.emit(1, 9)
	sim.effect_applied.emit(2, 1)
	sim.step(DT)
	assert_eq(int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)), orphans, "no orphan left by the view")


func test_a_new_view_on_the_same_sim_after_a_restart_is_clean() -> void:
	var sim: MatchSim = _sim()
	for round: int in 3:
		var fx: WeaponFxView = WeaponFxView.new()
		add_child(fx)
		fx.view_scale = 2.0
		fx.watch(sim)
		_cast(sim, 1, ALL[round])
		_run(sim, fx, 5)
		fx.queue_free()
		await get_tree().process_frame
	sim.weapons.reset()
	var fresh: WeaponFxView = _fx(sim)
	_run(sim, fresh, 5)
	_idle(fresh)


func test_a_caster_who_leaves_mid_flight_does_not_break_or_stick_any_weapon() -> void:
	for weapon: StringName in ALL:
		var sim: MatchSim = _sim()
		var fx: WeaponFxView = _fx(sim)
		assert_true(_cast(sim, 1, weapon), "%s fires" % weapon)
		_run(sim, fx, 4)
		sim.players.erase(1)
		sim.players[2].tagged = true
		_run(sim, fx, 700)
		assert_eq(fx.live_count(), 0, "%s: nothing stuck once its owner is gone" % weapon)
		assert_eq(fx.fading_count(), 0, "%s: no ghost stuck" % weapon)
		assert_eq(fx.particle_count(), 0, "%s: no particle or trail stuck" % weapon)
		assert_eq(fx.context().shadows.live(), 0, "%s: shadow back" % weapon)


func test_a_zone_removed_the_frame_it_appears_leaves_no_node() -> void:
	for weapon: StringName in [&"bato_heavy", &"lata", &"jacks", &"papel_trap", &"papel_shield", &"langit_lupa", &"bola", &"trumpo", &"tsinelas_heavy", &"bato_light"]:
		var sim: MatchSim = _sim()
		var fx: WeaponFxView = _fx(sim)
		assert_true(_cast(sim, 1, weapon), "%s fires" % weapon)
		sim.weapons.reset()
		fx.sync(sim, DT)
		assert_eq(fx.live_count(), 0, "%s: gone with the sim" % weapon)
		_run(sim, fx, 40)
		assert_eq(fx.fading_count(), 0, "%s" % weapon)
		assert_eq(fx.particle_count(), 0, "%s" % weapon)
		assert_eq(fx.context().shadows.live(), 0, "%s" % weapon)
	# and one that appears and goes between two syncs without ever being drawn
	var sim2: MatchSim = _sim()
	var fx2: WeaponFxView = _fx(sim2)
	_cast(sim2, 1, &"lata")
	sim2.weapons.zones.clear()
	fx2.sync(sim2, DT)
	_run(sim2, fx2, 30)
	_idle(fx2)


func test_syncing_twice_in_one_frame_and_with_zero_delta_is_harmless() -> void:
	var sim: MatchSim = _sim()
	var fx: WeaponFxView = _fx(sim)
	_cast(sim, 1, &"bola")
	_cast(sim, 3, &"trumpo")
	fx.sync(sim, 0.0)
	fx.sync(sim, 0.0)
	fx.sync(sim, DT)
	fx.sync(sim, DT)
	assert_eq(fx.projectiles.live_count(), 2, "one drawing per sim object, not per sync")
	_run(sim, fx, 200)
	_idle(fx)


# ---- 2. the online path --------------------------------------------------------------------

func _relay(source: MatchSim, mirror: MatchSim) -> void:
	source.weapon_cast.connect(func(id: int, weapon: StringName) -> void: mirror.weapon_cast.emit(id, weapon))
	source.player_damaged.connect(func(id: int, amount: int) -> void: mirror.player_damaged.emit(id, amount))
	source.player_healed.connect(func(id: int, amount: int) -> void: mirror.player_healed.emit(id, amount))
	source.effect_applied.connect(func(id: int, type: int) -> void: mirror.effect_applied.emit(id, type))
	source.weapons.cone_struck.connect(func(o: int, d: WeaponDef, a: Vector2, b: Vector2) -> void: mirror.weapons.cone_struck.emit(o, d, a, b))
	source.weapons.cone_winding.connect(func(o: int, d: WeaponDef, a: Vector2, b: Vector2) -> void: mirror.weapons.cone_winding.emit(o, d, a, b))


func _snapshot(source: MatchSim, mirror: MatchSim) -> void:
	var acks: Dictionary[int, int] = {}
	Snapshot.apply(mirror, Snapshot.capture(source, acks), -1)


func test_online_every_weapon_with_dropped_snapshots_draws_once_and_ends() -> void:
	for weapon: StringName in ALL:
		var server: MatchSim = _sim()
		var mirror: MatchSim = _sim()
		_relay(server, mirror)
		var fx: WeaponFxView = _fx(mirror)
		server.players[1].hp = 40
		assert_true(_cast(server, 1, weapon), "%s fires" % weapon)
		var most: int = 0
		for frame: int in 220:
			server.step(DT)
			# every third snapshot, then a long gap of 20 frames (dropped packets)
			if frame % 3 == 0 and not (frame >= 20 and frame < 40):
				_snapshot(server, mirror)
			fx.sync(mirror, DT)
			most = maxi(most, fx.live_count())
			assert_lte(fx.live_count(), mirror.weapons.projectiles.size() + mirror.weapons.zones.size() + mirror.weapons.shields.size(), "%s: never more drawn than the mirror holds (frame %d)" % [weapon, frame])
		assert_lte(most, 3, "%s: no duplicates" % weapon)
		assert_eq(fx.live_count(), 0, "%s: nothing stuck" % weapon)
		assert_eq(fx.fading_count(), 0, "%s: no ghost stuck" % weapon)
		assert_eq(fx.particle_count(), 0, "%s: no puff stuck" % weapon)
		assert_eq(fx.context().shadows.live(), 0, "%s: no shadow stuck" % weapon)


func test_online_a_late_joiner_sees_standing_effects_once_and_they_end() -> void:
	var server: MatchSim = _sim()
	for weapon: StringName in [&"jacks", &"papel_shield", &"papel_trap", &"langit_lupa", &"bato_heavy"]:
		server.players[1].elixir = 10.0
		_cast(server, 1, weapon)
	for i: int in 20:
		server.step(DT)
	var standing: int = server.weapons.zones.size() + server.weapons.shields.size()
	assert_gt(standing, 0)
	var mirror: MatchSim = _sim()
	var fx: WeaponFxView = _fx(mirror)
	_snapshot(server, mirror)
	fx.sync(mirror, DT)
	_snapshot(server, mirror)
	fx.sync(mirror, DT)
	assert_eq(fx.live_count(), mirror.weapons.zones.size() + mirror.weapons.shields.size() + mirror.weapons.projectiles.size(), "one drawing per object, none doubled")
	for frame: int in 400:
		server.step(DT)
		if frame % 2 == 0:
			_snapshot(server, mirror)
		fx.sync(mirror, DT)
	assert_eq(fx.live_count(), 0)
	assert_eq(fx.fading_count(), 0)
	assert_eq(fx.particle_count(), 0)


func test_online_snip_count_and_cone_from_relayed_events_only() -> void:
	var mirror: MatchSim = _sim()
	var fx: WeaponFxView = _fx(mirror)
	var snip: WeaponDef = mirror.weapon_defs[&"gunting_light"]
	mirror.weapons.cone_struck.emit(1, snip, Vector2(0.0, 8.0), Vector2(0.0, -1.0))
	fx.sync(mirror, DT)
	assert_eq(fx.cones.scissors().size(), snip.snips_min, "one pair of scissors per snip")
	assert_eq(fx.cones.flashes().size(), 1, "one wedge")
	# a duplicate strike packet in the same frame is two strikes (the sim never does that); a later frame must not add more
	_run(mirror, fx, 90)
	assert_eq(fx.cones.scissors().size(), 0)
	assert_eq(fx.cones.flashes().size(), 0)
	var big: WeaponDef = mirror.weapon_defs[&"gunting_heavy"]
	mirror.weapons.cone_winding.emit(1, big, Vector2(0.0, 8.0), Vector2(0.0, -1.0))
	fx.sync(mirror, DT)
	assert_eq(fx.windup_count(), 1)
	# the strike never arrives (packet lost): the warning must not stay on the street for ever
	_run(mirror, fx, 150)
	assert_eq(fx.windup_count(), 0, "a lost strike packet does not leave the warning up")


# ---- 3. Reduce motion mid-effect, colour-blind, both teams --------------------------------------

func test_reduce_motion_toggled_mid_effect_never_breaks_and_stops_new_motion() -> void:
	for weapon: StringName in ALL:
		var sim: MatchSim = _sim()
		var fx: WeaponFxView = _fx(sim)
		sim.players[1].hp = 40
		assert_true(_cast(sim, 1, weapon), "%s fires" % weapon)
		_run(sim, fx, 6)
		Settings.reduced_motion = true
		# every puff that was already in the air may finish; settle then check nothing new starts
		_run(sim, fx, 40)
		var busy: int = fx.context().particles.busy_emitters()
		assert_eq(busy, 0, "%s: with Reduce motion the puffs end and no new one starts" % weapon)
		Settings.reduced_motion = false
		_run(sim, fx, 200)
		assert_eq(fx.live_count(), 0, "%s: nothing stuck after toggling back" % weapon)
		assert_eq(fx.fading_count(), 0, "%s" % weapon)
		assert_eq(fx.particle_count(), 0, "%s" % weapon)
		assert_eq(fx.context().shadows.live(), 0, "%s" % weapon)


func test_reduce_motion_from_the_start_throws_no_particle_for_a_brawl() -> void:
	Settings.reduced_motion = true
	var sim: MatchSim = _sim()
	var fx: WeaponFxView = _fx(sim)
	for frame: int in 120:
		if frame % 8 == 0:
			for id: int in [1, 2, 3, 4]:
				sim.players[id].hp = 50
				_cast(sim, id, ALL[(frame / 8 + id) % ALL.size()])
		sim.players[1].hp = 40
		sim.step(DT)
		sim.player_damaged.emit(2, 12)
		fx.sync(sim, DT)
		assert_eq(fx.particle_count(), 0, "no particle with Reduce motion (frame %d)" % frame)
		assert_eq(fx.context().particles.busy_emitters(), 0)


func test_the_enemy_viewpoint_draws_enemy_effects_in_the_enemy_colour() -> void:
	for blind: bool in [false, true]:
		Settings.set_colorblind(blind)
		for viewer: int in [0, 1]:
			var sim: MatchSim = _sim()
			var fx: WeaponFxView = _fx(sim, viewer)
			# team 0 casts (player 1): mine when the viewer is team 0, theirs when team 1
			for weapon: StringName in [&"bato_heavy", &"lata", &"jacks", &"papel_trap", &"bola", &"trumpo"]:
				sim.players[1].elixir = 10.0
				_cast(sim, 1, weapon)
			_run(sim, fx, 4)
			# the areas a team casts: white for the viewer's own team, the enemy colour for the other
			var want: Color = Color.WHITE if viewer == 0 else TeamColors.world_enemy()
			var rim_want: Color = TeamColors.world_own() if viewer == 0 else TeamColors.world_enemy()
			var marks: int = 0
			for mark: Node in fx.zones.find_children("Mark", "MeshInstance3D", true, false):
				var mesh: ArrayMesh = (mark as MeshInstance3D).mesh as ArrayMesh
				var colors: PackedColorArray = mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray
				assert_gt(colors.size(), 0)
				for c: Color in colors:
					assert_true(absf(c.r - want.r) < 0.01 and absf(c.g - want.g) < 0.01 and absf(c.b - want.b) < 0.01, "viewer %d blind %s: mark colour %s wanted %s" % [viewer, blind, c, want])
				marks += 1
			assert_gt(marks, 2)
			for shadow: MeshInstance3D in fx.context().shadows.all_nodes():
				if shadow.visible:
					var rim: Color = (shadow.material_override as ShaderMaterial).get_shader_parameter(&"rim_color") as Color
					assert_true(absf(rim.r - rim_want.r) < 0.01 and absf(rim.g - rim_want.g) < 0.01 and absf(rim.b - rim_want.b) < 0.01, "viewer %d blind %s: shadow rim %s wanted %s" % [viewer, blind, rim, rim_want])


# ---- 4. ground marks equal the sim's hit areas ------------------------------------------------------

func _extent(mesh: Mesh) -> Vector3:
	var box: AABB = mesh.get_aabb()
	return Vector3(maxf(absf(box.position.x), absf(box.end.x)), 0.0, maxf(absf(box.position.z), absf(box.end.z)))


func test_zone_marks_are_the_true_hit_areas() -> void:
	var rim: float = FxKit.RIM / 2.0 + 0.01
	var cases: Array = [
		[&"bato_heavy", WeaponSystem.ZoneKind.EXPLOSION],
		[&"lata", WeaponSystem.ZoneKind.EXPLOSION],
		[&"jacks", WeaponSystem.ZoneKind.FIELD],
		[&"papel_trap", WeaponSystem.ZoneKind.TRAP],
		[&"papel_trap", WeaponSystem.ZoneKind.SLOW_ZONE],
	]
	for pair: Array in cases:
		var sim: MatchSim = _sim()
		var fx: WeaponFxView = _fx(sim)
		var def: WeaponDef = sim.weapon_defs[pair[0] as StringName]
		assert_true(_cast(sim, 1, pair[0] as StringName), "%s fires" % pair[0])
		var zone: WeaponSystem.Zone = sim.weapons.zones[0]
		if pair[1] == WeaponSystem.ZoneKind.SLOW_ZONE:
			# spring it: an enemy on the trap
			sim.players[2].position = zone.center
			for i: int in 60:
				sim.step(DT)
				fx.sync(sim, DT)
				if not sim.weapons.zones.is_empty() and sim.weapons.zones[0].kind == WeaponSystem.ZoneKind.SLOW_ZONE:
					break
			zone = sim.weapons.zones[0]
		else:
			fx.sync(sim, DT)
		assert_eq(zone.kind, pair[1] as WeaponSystem.ZoneKind, "%s kind" % pair[0])
		var node: Node3D = fx.zones.node_for(zone.id)
		var mark: MeshInstance3D = node.get_node("Mark") as MeshInstance3D
		var want: float = zone.radius
		var got: float = _extent(mark.mesh).x
		assert_between(got, want - 0.001, want + rim, "%s %s: mark radius %.3f vs sim radius %.3f" % [pair[0], pair[1], got, want])
		assert_almost_eq(zone.radius, def.trigger_radius if pair[1] == WeaponSystem.ZoneKind.TRAP else def.radius, 0.0001, "%s sim radius is the data's" % pair[0])


func test_cone_wedges_are_the_true_range_and_angle() -> void:
	for weapon: StringName in [&"gunting_light", &"gunting_heavy"]:
		var def: WeaponDef = _rules.weapons.filter(func(d: WeaponDef) -> bool: return d.id == weapon)[0] as WeaponDef
		var wedge: PackedVector2Array = FxGround.wedge(def)
		var far: float = 0.0
		var widest: float = 0.0
		for p: Vector2 in wedge:
			far = maxf(far, p.length())
			widest = maxf(widest, absf(p.angle_to(Vector2(0.0, -1.0))) if p != Vector2.ZERO else 0.0)
		assert_almost_eq(far, def.max_range, 0.0001, "%s: the wedge reaches the sim's range" % weapon)
		assert_almost_eq(rad_to_deg(widest), def.angle_degrees / 2.0, 0.01, "%s: and its half angle" % weapon)


func test_bola_rings_and_walls_match_the_sim() -> void:
	var sim: MatchSim = _sim()
	sim.players[2].position = Vector2(30.0, 0.0)
	var fx: WeaponFxView = _fx(sim)
	_cast(sim, 1, &"bola")
	fx.sync(sim, DT)
	var bola: WeaponDef = sim.weapon_defs[&"bola"]
	var rec_rings: Array = fx.projectiles.find_children("*", "MeshInstance3D", true, false).filter(func(n: Node) -> bool: return n.mesh is ArrayMesh and n.get_parent() == fx.projectiles)
	assert_gt(rec_rings.size(), 0, "bounce rings are drawn")
	for ring: MeshInstance3D in rec_rings:
		assert_between(_extent(ring.mesh).x, bola.radius - 0.001, bola.radius + FxKit.RIM, "ring radius is the bounce radius %.2f" % bola.radius)
	for weapon: StringName in [&"papel_shield", &"langit_lupa"]:
		var s2: MatchSim = _sim()
		var f2: WeaponFxView = _fx(s2)
		_cast(s2, 1, weapon)
		f2.sync(s2, DT)
		var shield: WeaponSystem.Shield = s2.weapons.shields[0]
		var ribbon: MeshInstance3D = f2.walls.ribbon_for(shield.id)
		assert_not_null(ribbon, "%s ribbon" % weapon)
		assert_between(_extent(ribbon.mesh).x, shield.half_width - 0.001, shield.half_width + 0.1, "%s: the ribbon spans the sim's segment %.2f" % [weapon, shield.half_width])
		var def: WeaponDef = s2.weapon_defs[weapon]
		assert_almost_eq(shield.half_width * 2.0, def.width, 0.0001)


# ---- 5. determinism: the view never changes the sim ----------------------------------------------------

func _digest(sim: MatchSim) -> String:
	var acks: Dictionary[int, int] = {}
	return str(Snapshot.capture(sim, acks))


func test_the_sim_is_identical_with_and_without_the_view() -> void:
	var with_view: MatchSim = _sim()
	var without: MatchSim = _sim()
	var fx: WeaponFxView = _fx(with_view)
	for frame: int in 240:
		if frame % 10 == 0:
			for id: int in [1, 2, 3, 4]:
				var weapon: StringName = ALL[(frame / 10 + id * 3) % ALL.size()]
				for sim: MatchSim in [with_view, without]:
					sim.players[id].elixir = 10.0
					sim.players[id].hp = 60
					_cast(sim, id, weapon)
		with_view.step(DT)
		without.step(DT)
		fx.sync(with_view, DT)
		assert_eq(_digest(with_view), _digest(without), "same snapshot at frame %d" % frame)
		if _digest(with_view) != _digest(without):
			break
	assert_eq(with_view.rng.state, without.rng.state, "and the same random stream position")


func test_the_view_source_never_touches_the_sim_random_or_writes_sim_state() -> void:
	var offenders: Array[String] = []
	var paths: Array[String] = ["res://scripts/view/weapon_fx_view.gd"]
	for file: String in DirAccess.get_files_at("res://scripts/view/fx"):
		if file.ends_with(".gd"):
			paths.append("res://scripts/view/fx/" + file)
	var banned: PackedStringArray = PackedStringArray(["randf(", "randi(", "randf_range(", "sim.rng", "weapons.rng", ".rng.", "weapons.projectiles.", "weapons.zones.", "weapons.shields.", "players.erase", "sim.damage(", "sim.heal(", "sim.step("])
	for path: String in paths:
		var text: String = FileAccess.get_file_as_string(path)
		for line: String in text.split("\n"):
			var code: String = line.split("#")[0]
			for token: String in banned:
				if token in code and not ("RandomNumberGenerator" in code):
					# only reads are allowed: .size() / .is_empty() / .has( / .get( are fine
					if token.begins_with("weapons.") and (".size()" in code or ".is_empty()" in code):
						continue
					offenders.append("%s: %s" % [path.get_file(), line.strip_edges()])
	assert_eq(offenders, [], "the view reads the sim; it never rolls its dice or changes it")
