extends GutTest
## The pooled effect pieces (docs/ART.md "Skill effects"): particles stay low and short and stop at the live cap, nothing
## is allocated after the pools are built, rings recycle the oldest, shadows shrink and fade with height, and the marks of a
## hit area use its exact size.

const DT: float = 1.0 / 30.0

var _motion: bool
var _ctx: FxContext
var _host: Node3D


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_ctx = FxContext.new()
	_host = Node3D.new()
	add_child_autofree(_host)
	_ctx.particles = FxParticles.new()
	_ctx.ground = FxGround.new()
	_ctx.shadows = FxShadows.new()
	_host.add_child(_ctx.particles)
	_host.add_child(_ctx.ground)
	_host.add_child(_ctx.shadows)
	_ctx.particles.setup(_ctx)
	_ctx.ground.setup(_ctx)
	_ctx.shadows.setup(_ctx)


func after_each() -> void:
	Settings.reduced_motion = _motion


func _advance(seconds: float) -> void:
	var frames: int = int(ceil(seconds / DT))
	for i: int in frames:
		_ctx.time += DT
		_ctx.particles.step()
		_ctx.ground.step()


func test_particle_presets_respect_height_and_reach_bounds() -> void:
	for preset: int in FxParticles.Preset.values():
		var s: FxParticles.Spec = FxParticles.spec(preset)
		assert_lte(FxParticles.apex(s), s.apex_bound + 0.001, "%s stays under %.2f m" % [FxParticles.Preset.keys()[preset], s.apex_bound])
		assert_lte(s.lifetime, 0.6, "%s is short" % FxParticles.Preset.keys()[preset])
	var stars: FxParticles.Spec = FxParticles.spec(FxParticles.Preset.STARS)
	assert_between(stars.lifetime * stars.speed_min, 0.899, 1.301, "stars fly 0.9-1.3 m out")
	assert_between(stars.lifetime * stars.speed_max, 0.899, 1.301)
	assert_lte(FxParticles.apex(stars), 1.3, "born and kept at waist height, under the face")
	for normal: Vector3 in stars.normals:
		assert_lte(normal.z, 0.3, "never toward the camera")
	var plus: FxParticles.Spec = FxParticles.spec(FxParticles.Preset.PLUS)
	for point: Vector3 in plus.points:
		assert_lte(point.z, 0.0, "heal sparkles rise beside or behind the kid, never in front of the face")
	assert_lte(FxParticles.apex(FxParticles.spec(FxParticles.Preset.SMOKE)), 2.0, "the polymorph puff stays under 2 m")
	for emitter: Node in _ctx.particles.get_children():
		var particles: CPUParticles3D = emitter as CPUParticles3D
		assert_false(particles.local_coords, "world space: a moving thrower does not drag its dust")
	for preset: int in FxParticles.Preset.values():
		var material: BaseMaterial3D = FxParticles.spec(preset).material as BaseMaterial3D
		if material.billboard_mode == BaseMaterial3D.BILLBOARD_PARTICLES:
			assert_true(material.billboard_keep_scale, "%s keeps each particle's size (else every puff is drawn 1 m)" % FxParticles.Preset.keys()[preset])


func test_one_shot_emitters_are_one_shot() -> void:
	var one_shots: int = 0
	for emitter: Node in _ctx.particles.get_children():
		one_shots += 1 if (emitter as CPUParticles3D).one_shot else 0
	assert_eq(one_shots, 36, "36 pooled one-shot emitters (plus the trails)")
	assert_eq(_ctx.particles.emitter_count(), 36 + FxParticles.TRAILS)


func test_particle_cap_skips_decoration() -> void:
	var skipped: int = 0
	for i: int in 30:
		if not _ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(float(i), 0.0, 0.0), 12):
			skipped += 1
		if not _ctx.particles.puff(FxParticles.Preset.DUST, Vector3(float(i), 0.0, 0.0), 4):
			skipped += 1
		assert_lte(_ctx.particles.live_particles(), FxParticles.LIVE_CAP)
	assert_gt(skipped, 0, "past the cap a puff is skipped")
	assert_lte(_ctx.particles.busy_emitters(), 36)
	_advance(0.6)
	assert_eq(_ctx.particles.live_particles(), 0, "and every puff is over in 0.6 s")


func test_reduce_motion_throws_no_particles() -> void:
	_ctx.reduced = true
	assert_false(_ctx.particles.puff(FxParticles.Preset.DUST, Vector3.ZERO, 4))
	assert_null(_ctx.particles.trail_take())
	assert_eq(_ctx.particles.live_particles(), 0)


func test_pools_never_allocate_after_build() -> void:
	var materials: int = FxKit.material_count()
	var children: int = _host.get_child_count() + _ctx.particles.get_child_count() + _ctx.ground.get_child_count() + _ctx.shadows.get_child_count()
	for i: int in 200:
		_ctx.ground.ring(Vector2(i, 0), 0.5, 1.0, Palette.SARI_YELLOW, 0.27)
		_ctx.ground.smear(Transform3D.IDENTITY, &"arc", 2.0, Color.WHITE, 0.1)
		_ctx.ground.crack(Vector2.ZERO, 0.0, &"spokes", Vector2(1.2, 1.2), 0.5)
		_ctx.ground.flash_disc(Vector2.ZERO, 1.2, Color.WHITE, 0.3, 0.07, 0.2)
		_ctx.particles.puff(FxParticles.Preset.STARS, Vector3.ZERO, 4)
		_ctx.shadows.attach(i, 1.0, 0)
		_ctx.shadows.place(i, Vector2.ZERO, 1.0)
		_ctx.shadows.detach(i)
		_advance(DT)
	assert_eq(FxKit.material_count(), materials, "no material made while playing")
	var after: int = _host.get_child_count() + _ctx.particles.get_child_count() + _ctx.ground.get_child_count() + _ctx.shadows.get_child_count()
	assert_eq(after, children, "no node made while playing")


func test_ring_pool_recycles_the_oldest() -> void:
	for i: int in 20:
		_ctx.ground.ring(Vector2(i, 0), 0.5, 1.0, Palette.SARI_YELLOW, 1.0)
	assert_eq(_ctx.ground.live_rings(), FxGround.RINGS)
	var newest: bool = false
	for ring: MeshInstance3D in _ctx.ground.active_rings():
		newest = newest or is_equal_approx(ring.position.x, 19.0)
	assert_true(newest, "the newest ring is drawn (an old one made room)")


func test_shadow_shrinks_and_fades_with_height() -> void:
	assert_true(_ctx.shadows.attach(7, 2.0, 0))
	_ctx.shadows.place(7, Vector2.ZERO, 0.0)
	var node: MeshInstance3D = _ctx.shadows.node_of(7)
	assert_almost_eq(node.scale.x, FxShadows.SHADOW_SCALE * 2.0, 0.001, "full size on the ground")
	assert_almost_eq(FxShadows.alpha_at(0.0), 0.45, 0.001)
	_ctx.shadows.place(7, Vector2.ZERO, 3.0)
	assert_almost_eq(node.scale.x, FxShadows.SHADOW_SCALE * 2.0 * 0.55, 0.001, "smaller high up")
	assert_almost_eq(FxShadows.alpha_at(3.0), 0.2, 0.001, "and lighter")
	assert_almost_eq(node.position.y, FxKit.Y_SHADOW, 0.0001)
	for key: int in range(100, 100 + FxShadows.SLOTS - 1):
		assert_true(_ctx.shadows.attach(key, 1.0, 1))
	assert_false(_ctx.shadows.attach(999, 1.0, 1), "the 25th has no shadow (the object is still drawn)")


func test_marks_are_the_exact_area() -> void:
	var mark: ArrayMesh = FxGround.circle_mark(2.0, Color.RED, 0.4)
	assert_same(FxGround.circle_mark(2.0, Color.RED, 0.4), mark, "built once, reused")
	var arrays: Array = mark.surface_get_arrays(0)
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] as PackedColorArray
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var outer: float = 0.0
	for i: int in vertices.size():
		if colors[i].a > 0.9:
			var r: float = Vector2(vertices[i].x, vertices[i].z).length()
			assert_almost_eq(r, 2.0, FxKit.RIM / 2.0 + 0.001, "the rim straddles the real edge")
			outer = maxf(outer, r)
		else:
			assert_lte(Vector2(vertices[i].x, vertices[i].z).length(), 2.0 + 0.001, "the fill stops at the edge")
	assert_almost_eq(outer, 2.0 + FxKit.RIM / 2.0, 0.01)


# ---- hit bursts -----------------------------------------------------------------------------------------------------

## A sim with `kids` enemies standing apart and a fx view watching it.
func _brawl(kids: int) -> WeaponFxView:
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres") as GameRules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	for id: int in range(1, kids + 1):
		sim.add_player(id, 1)
		sim.players[id].position = Vector2(float(id) * 2.0 - 14.0, 4.0)
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	fx.watch(sim)
	# count from after the view's warm-up frames (it draws one invisible instance of each material first)
	for i: int in FxWarmup.WARM_FRAMES + 1:
		fx.sync(sim, DT)
	return fx


func _stars(fx: WeaponFxView) -> int:
	var emitters: int = 0
	for emitter: CPUParticles3D in fx.context().particles.busy():
		emitters += 1 if emitter.mesh == FxKit.star_quad() else 0
	return emitters


func test_six_snips_on_one_kid_make_one_ring() -> void:
	var fx: WeaponFxView = _brawl(2)
	for i: int in 6:
		fx.context().sim.damage(1, 6)
	fx.sync(fx.context().sim, DT)
	assert_eq(fx.context().ground.live_rings(), 1, "one ring for six snips in one frame")
	assert_eq(_stars(fx), 1, "and one star burst")
	assert_eq(fx.burst_count(), 1)


func test_a_tick_has_no_ring() -> void:
	var fx: WeaponFxView = _brawl(2)
	fx.context().sim.damage(1, 3)
	fx.sync(fx.context().sim, DT)
	assert_eq(fx.context().ground.live_rings(), 0, "a 3-damage tick has no ring")
	assert_eq(_stars(fx), 1, "just two stars")
	assert_eq((fx.context().particles.busy()[0] as CPUParticles3D).amount, FxBursts.TICK_STARS)


func test_burst_cap_is_twelve() -> void:
	var fx: WeaponFxView = _brawl(14)
	for id: int in range(1, 15):
		fx.context().sim.damage(id, 10)
	fx.sync(fx.context().sim, DT)
	assert_lte(fx.burst_count(), FxBursts.MAX_LIVE, "at most twelve bursts at once")
	assert_lte(fx.context().ground.live_rings(), FxBursts.MAX_LIVE)
	for i: int in 10:
		fx.sync(fx.context().sim, DT)
	assert_eq(fx.burst_count(), 0, "and they are over in a third of a second")
