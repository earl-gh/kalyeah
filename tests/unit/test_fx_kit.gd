extends GutTest
## The shared effect kit: squash keeps the volume and stays in bounds, pop-in overshoots once, shared meshes and model looks
## are built once, and the per-object track reads a turn-back from positions alone (docs/ART.md "Skill effects").

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func test_squash_preserves_volume_and_stays_in_bounds() -> void:
	for s: float in [0.65, 0.7, 1.0, 1.35, 2.0]:
		var scale: Vector3 = FxSquash.along(s)
		assert_almost_eq(scale.x * scale.y * scale.z, 1.0, 0.001, "volume kept at %.2f" % s)
	assert_almost_eq(FxSquash.along(2.0).y, FxSquash.MAX, 0.0001, "clamped to the longest")
	assert_almost_eq(FxSquash.along(0.1).y, FxSquash.MIN, 0.0001, "clamped to the flattest")
	assert_lte(FxSquash.along(0.7).x, 1.2, "the contact squash never widens past 1.2")
	var basis: Basis = FxSquash.stretch_basis(Vector3(1.0, 0.0, 1.0), 1.3)
	assert_almost_eq(basis.determinant(), 1.0, 0.001, "a stretch along any direction keeps the volume too")
	assert_almost_eq((basis * Vector3(1.0, 0.0, 1.0).normalized()).length(), 1.3, 0.001, "and is 1.3 along it")


func test_velocity_stretch_matches_the_audit() -> void:
	assert_almost_eq(FxSquash.velocity_stretch(14.0), 1.30, 0.001, "the rock")
	assert_almost_eq(FxSquash.velocity_stretch(9.0), 1.20, 0.001, "the heavy slipper")
	assert_almost_eq(FxSquash.velocity_stretch(12.0), 1.267, 0.001, "the marble")
	assert_almost_eq(FxSquash.velocity_stretch(0.0), 1.0, 0.001, "still")


func test_pop_overshoots_once_and_lands() -> void:
	assert_almost_eq(FxSquash.pop(0.0), 0.0, 0.0001)
	var peak: float = 0.0
	var falls: int = 0
	var rising: bool = true
	var last: float = 0.0
	for i: int in 101:
		var value: float = FxSquash.pop(float(i) / 100.0)
		peak = maxf(peak, value)
		if rising and value < last - 0.00001:
			falls += 1
			rising = false
		elif not rising and value > last + 0.00001:
			rising = true
		last = value
	assert_almost_eq(peak, FxSquash.POP_PEAK, 0.005, "one overshoot to 1.15")
	assert_lte(falls, 1, "it turns back down once")
	assert_almost_eq(FxSquash.pop(1.0), 1.0, 0.0001, "and lands on its size")


func test_shared_resources_are_built_once() -> void:
	assert_same(FxKit.ring_mesh(), FxKit.ring_mesh(), "one ring mesh")
	assert_same(FxKit.star_quad(), FxKit.star_quad())
	assert_same(FxKit.overlay(FxKit.PRIO_FILL), FxKit.overlay(FxKit.PRIO_FILL))
	var before: int = FxKit.material_count()
	for i: int in 50:
		FxKit.ring_mesh()
		FxKit.disc_mesh()
		FxKit.smear_arc()
		FxKit.overlay(FxKit.PRIO_DECAL)
	assert_lte(FxKit.material_count(), before + 1, "asking again makes nothing new (at most the first decal overlay)")


func test_model_instances_share_their_look() -> void:
	var a: Node3D = autofree(WeaponModel.make(&"bola", true)) as Node3D
	var b: Node3D = autofree(WeaponModel.make(&"bola", true)) as Node3D
	var mesh_a: MeshInstance3D = a.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var mesh_b: MeshInstance3D = b.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	assert_same(mesh_a.get_surface_override_material(0), mesh_b.get_surface_override_material(0), "one look for every marble")
	assert_true(mesh_a.is_in_group(WeaponModel.GROUP), "a weapon model mesh says it is one")
	assert_lte(WeaponModel.look_count(), 20, "one look per model, never one per throw")
	var looks: int = WeaponModel.look_count()
	for i: int in 5:
		(WeaponModel.make(&"bola", true) as Node3D).free()
	assert_eq(WeaponModel.look_count(), looks)


func test_track_turn_back_and_origin() -> void:
	var track: FxTrack = FxTrack.new()
	var now: float = 0.0
	var flips: int = 0
	for i: int in 10:
		track.see(Vector2(0.0, -float(i) * 0.5), now)
		flips += 1 if track.flipped else 0
		now += 1.0 / 30.0
	assert_eq(track.first_pos, Vector2.ZERO, "first seen at the throw")
	assert_almost_eq(track.dir.y, -1.0, 0.001, "going out")
	assert_almost_eq(track.speed, 15.0, 0.01, "half a metre a frame")
	for i: int in 10:
		track.see(Vector2(0.0, -4.5 + float(i) * 0.5), now)
		flips += 1 if track.flipped else 0
		now += 1.0 / 30.0
	assert_eq(flips, 1, "it turned back once")
	var dir: Vector2 = track.dir
	track.see(track.last_pos + Vector2(0.005, 0.0), now)
	assert_eq(track.dir, dir, "a tiny snapshot wobble is not a new direction")
	assert_false(track.flipped)
