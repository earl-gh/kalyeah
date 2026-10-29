extends GutTest
## In its first frames in a match the effect view draws one invisible instance of every effect material (dust, scraps and
## chips particles, a ring, a flash disc, a shadow) and builds the lazy meshes, so a phone compiles its GL programs then and
## not on the first throw of the fight. Afterwards every pool is idle again.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func test_the_view_warms_its_materials_then_every_pool_is_idle() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	practice.sim._brains.clear()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	var ctx: FxContext = fx.context()
	var warming: bool = false
	for i: int in WeaponFxView.WARM_FRAMES:
		practice.advance(DT)
		warming = warming or ctx.particles.busy_emitters() > 0 or ctx.shadows.live() > 0
	assert_true(fx.warmed(), "the view warmed its materials in its first frames")
	practice.advance(DT)
	assert_eq(ctx.particles.live_particles(), 0, "then no particle is left")
	assert_eq(ctx.ground.live_rings(), 0, "no ring")
	assert_eq(ctx.ground.live_flashes(), 0, "no flash")
	assert_eq(ctx.shadows.live(), 0, "no shadow")
	assert_gt(FxKit.cached_count(), 0, "and the lazy meshes are built")
	assert_gt(FxParticles.spec_count(), 0)
