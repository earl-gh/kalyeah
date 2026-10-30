extends GutTest
## Smear frames (docs/ART.md "Smear frames"): at its fastest a skill object is drawn stretched along its path for one to three
## frames (volume kept), then crisp; a contact squashes, overshoots and settles on exactly 1; swings leave a tapered arc on the
## frames they close; the thrower's release leaves a tapered trail on the cast tick. View only: nothing here touches the sim.

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


## You (id 1, team 0) at (0, 8) facing north, an enemy (2) at `enemy`, a friend (3), and a view.
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


func _cast(weapon: StringName, aim: Vector2, hold: float = 0.0) -> void:
	var def: WeaponDef = _sim.weapon_defs[weapon]
	assert_true(_sim.weapons.fire(_sim, _sim.players[1], def, aim, hold, 2), "%s is cast" % weapon)
	_sim.weapon_cast.emit(1, weapon)


func _frame() -> void:
	_sim.step(DT)
	_fx.sync(_sim, DT)


func _length_along(basis: Basis, dir: Vector3) -> float:
	return (basis * dir.normalized()).length()


# ---- the maths ---------------------------------------------------------------------------------------------------------

func test_smear_is_full_on_the_first_frames_fades_and_is_exactly_one_after() -> void:
	var speed: float = 16.0
	var first: float = FxSquash.smear_stretch(speed, 1.2, 0.0)
	assert_gt(first, 1.3, "a light slipper at 16 m/s smears by about a frame's travel")
	assert_almost_eq(FxSquash.smear_stretch(speed, 1.2, 1.0), first, 0.0001, "full for the first two frames")
	assert_lt(FxSquash.smear_stretch(speed, 1.2, 2.0), first, "then fading")
	assert_lt(FxSquash.smear_stretch(speed, 1.2, 3.0), FxSquash.smear_stretch(speed, 1.2, 2.0))
	assert_eq(FxSquash.smear_stretch(speed, 1.2, float(FxSquash.SMEAR_FRAMES) + 1.0), 1.0, "a slow frame is exactly crisp")
	assert_eq(FxSquash.smear_stretch(speed, 1.2, 20.0), 1.0)
	assert_eq(FxSquash.smear_stretch(0.0, 1.2, 0.0), 1.0, "still: nothing to smear")


func test_smear_length_grows_with_speed_and_is_capped() -> void:
	var last: float = 1.0
	for speed: float in [4.0, 9.0, 14.0, 16.0, 24.0]:
		var s: float = FxSquash.smear_stretch(speed, 1.2, 0.0)
		assert_gt(s, last, "faster is longer (%.0f m/s)" % speed)
		last = s
	assert_almost_eq(FxSquash.smear_stretch(14.0, 1.1, 0.0), 1.0 + 14.0 / 30.0 / 1.1, 0.001, "about the distance travelled in a frame")
	assert_almost_eq(FxSquash.smear_stretch(500.0, 0.5, 0.0), FxSquash.SMEAR_MAX, 0.0001, "never longer than SMEAR_MAX")
	assert_lte(FxSquash.smear_stretch(30.0, 0.01, 0.0), FxSquash.SMEAR_MAX)


func test_smear_basis_stretches_along_the_velocity_and_keeps_the_volume() -> void:
	for velocity: Vector3 in [Vector3(0.0, 0.0, -14.0), Vector3(3.0, -5.0, -9.0), Vector3(0.0, -12.0, 0.0), Vector3(-2.0, 4.0, 0.0)]:
		var basis: Basis = FxSquash.smear_basis(velocity, 1.7)
		var dir: Vector3 = velocity.normalized()
		assert_almost_eq(_length_along(basis, dir), 1.7, 0.001, "1.7 long along %s" % velocity)
		assert_true((basis * dir).normalized().is_equal_approx(dir), "and the stretch points along the velocity")
		var across: Vector3 = dir.cross(Vector3.UP) if absf(dir.dot(Vector3.UP)) < 0.99 else Vector3.RIGHT
		assert_almost_eq(_length_along(basis, across), 1.0 / sqrt(1.7), 0.001, "thin across")
		assert_almost_eq(basis.determinant(), 1.0, 0.001, "volume kept")
	assert_eq(FxSquash.smear_basis(Vector3.ZERO, 1.7), Basis.IDENTITY, "no direction, no smear")
	assert_true(FxSquash.smear_basis(Vector3.FORWARD, 1.0).is_equal_approx(Basis.IDENTITY), "a smear of 1 is crisp")
	assert_almost_eq(_length_along(FxSquash.smear_basis(Vector3.RIGHT, 9.0), Vector3.RIGHT), FxSquash.SMEAR_MAX, 0.0001, "held to the cap")


func test_landing_squashes_overshoots_and_settles_on_exactly_one() -> void:
	var squash: float = FxSquash.IMPACT_S
	var values: Array[float] = []
	for frame: int in FxSquash.landing_frames() + 2:
		values.append(FxSquash.landing(squash, float(frame)))
	for frame: int in FxSquash.IMPACT_FRAMES:
		assert_eq(values[frame], squash, "squashed while held (frame %d)" % frame)
	var peak: float = 0.0
	for value: float in values:
		peak = maxf(peak, value)
	assert_gt(peak, 1.05, "then it overshoots past 1 on the other side")
	assert_lt(peak, 1.2, "by a little")
	assert_lt(values[FxSquash.IMPACT_FRAMES + 2], peak, "and comes back")
	assert_eq(values[FxSquash.landing_frames()], 1.0, "settled on exactly one")
	assert_eq(values[values.size() - 1], 1.0, "and stays")
	assert_eq(FxSquash.landing(squash, 99.0), 1.0)
	# a stretch rebounds through a squash
	var rebound: float = 2.0
	for frame: int in FxSquash.landing_frames():
		rebound = minf(rebound, FxSquash.landing(1.3, float(frame)))
	assert_lt(rebound, 1.0, "a stretched hit springs back through a squash")
	assert_eq(FxSquash.landing(1.3, float(FxSquash.landing_frames())), 1.0)


func test_the_arc_and_the_trail_are_tapered_and_lighter_toward_the_tail() -> void:
	for mesh: ArrayMesh in [FxKit.smear_tail(), FxKit.smear_arc()]:
		var arrays: Array = mesh.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
		var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] as PackedColorArray
		assert_eq(vertices.size(), colors.size())
		# thickness at the ends (the first and last ring of the strip) against the middle: width = distance between a quad's side corners
		var segments: int = vertices.size() / 6
		var thick: Array[float] = []
		for i: int in segments:
			thick.append(vertices[i * 6 + 0].distance_to(vertices[i * 6 + 5]))
		assert_almost_eq(thick[0], 0.0, 0.0001, "pointed at the start")
		assert_lt(thick[segments - 1], thick[segments / 2], "thin at the end, thickest in the middle")
		var alpha_start: float = colors[0].a
		var alpha_end: float = colors[vertices.size() - 2].a
		assert_almost_eq(alpha_start, FxKit.SMEAR_TAIL_ALPHA, 0.0001, "see-through at the tail")
		assert_gt(alpha_end, 0.85, "nearly whole at the head")
		for color: Color in colors:
			assert_eq(color.r, 1.0, "white: no team colour is touched")
			assert_eq(color.g, 1.0)
			assert_eq(color.b, 1.0)


func test_trail_runs_from_tail_to_head_facing_the_camera() -> void:
	var ground: FxGround = FxGround.new()
	var ctx: FxContext = FxContext.new()
	add_child_autofree(ground)
	ground.setup(ctx)
	var from: Vector3 = Vector3(1.0, 1.4, 5.0)
	var to: Vector3 = Vector3(1.0, 1.4, 3.0)
	assert_true(ground.trail(from, to, 0.5, Color.WHITE, 0.1))
	var node: MeshInstance3D = ground.active_smears()[0]
	assert_true(node.position.is_equal_approx((from + to) / 2.0), "centred between its ends")
	assert_true((node.basis.x).normalized().is_equal_approx((to - from).normalized()), "its head (+X) leads the way it moves")
	assert_almost_eq(node.basis.x.length(), 2.0, 0.001, "as long as the run")
	assert_almost_eq(node.basis.z.length() * FxKit.STREAK_WIDTH, 0.5, 0.001, "0.5 m across at its middle")
	assert_gt(node.basis.y.dot(FxKit.CAMERA_BACK), 0.5, "its face is turned toward the camera")
	assert_false(ground.trail(from, from, 0.5, Color.WHITE, 0.1), "no run, no trail")


func test_trails_never_allocate_after_the_pool_is_built() -> void:
	var ground: FxGround = FxGround.new()
	var ctx: FxContext = FxContext.new()
	add_child_autofree(ground)
	ground.setup(ctx)
	var materials: int = FxKit.material_count()
	var children: int = ground.get_child_count()
	for i: int in 200:
		ground.trail(Vector3(0.0, 1.0, float(i)), Vector3(1.0, 1.0, float(i)), 0.4, Color.WHITE, 0.1)
		ground.smear(Transform3D.IDENTITY, &"tail", 1.0, Color.WHITE, 0.1, 0.3)
		ctx.time += DT
		ground.step()
	assert_eq(FxKit.material_count(), materials, "no material made")
	assert_eq(ground.get_child_count(), children, "no node made")
	assert_lte(ground.live_smears(), FxGround.SMEARS)


# ---- the thrower's release ----------------------------------------------------------------------------------------------

func test_the_release_trail_appears_on_the_cast_tick_along_the_aim() -> void:
	for weapon: StringName in [&"bato_light", &"tsinelas_light", &"tsinelas_heavy", &"bola"]:
		_setup(Vector2(0.0, 3.0))
		_cast(weapon, Vector2(0.0, -1.0))
		_fx.sync(_sim, DT)
		var smears: Array[MeshInstance3D] = _fx.context().ground.active_smears()
		assert_eq(smears.size(), 1, "%s: one release trail within a frame of the cast" % weapon)
		var trail: MeshInstance3D = smears[0]
		assert_true(trail.basis.x.normalized().is_equal_approx(Vector3(0.0, 0.0, -1.0)), "%s: along the aim, north" % weapon)
		assert_almost_eq(trail.position.x, 0.0, 0.001)
		var start: float = trail.position.z + trail.basis.x.length() / 2.0
		assert_almost_eq(start, 8.0 - FxRelease.HAND_CLEAR, 0.01, "%s: starts at the hand, not on the body" % weapon)
		var speed: float = _sim.weapon_defs[weapon].speed
		assert_almost_eq(trail.basis.x.length(), FxRelease.length_for(speed), 0.01, "%s: as long as it travels in the smear frames" % weapon)
		assert_between(FxRelease.length_for(speed), FxRelease.MIN_LENGTH, FxRelease.MAX_LENGTH)
		# the light slipper throws three, each with its own trail, so it is only quiet once the last has faded
		for i: int in FxSquash.TRAIL_FRAMES + 1 + (40 if weapon == &"tsinelas_light" else 0):
			_frame()
		assert_eq(_fx.context().ground.live_smears(), 0, "%s: gone after its trail's %d frames" % [weapon, FxSquash.TRAIL_FRAMES])
		_fx.queue_free()


func test_zone_throws_leave_a_release_trail_but_the_meteor_does_not() -> void:
	for weapon: StringName in [&"lata", &"papel_trap", &"jacks"]:
		_setup(Vector2(20.0, 0.0))
		_cast(weapon, Vector2(0.0, -4.0 / 6.0))
		_fx.sync(_sim, DT)
		assert_eq(_fx.context().ground.live_smears(), 1, "%s: a release trail on the cast tick" % weapon)
		_fx.queue_free()
	_setup(Vector2(20.0, 0.0))
	_cast(&"bato_heavy", Vector2(0.0, -0.5))
	_fx.sync(_sim, DT)
	assert_eq(_fx.context().ground.live_smears(), 0, "the meteor falls from the sky: nothing leaves a hand")


func test_reduced_motion_draws_no_smear() -> void:
	Settings.reduced_motion = true
	for weapon: StringName in [&"bato_light", &"tsinelas_light", &"lata", &"heal", &"gunting_light"]:
		_setup(Vector2(0.0, 5.0))
		_sim.damage(1, 40)
		_cast(weapon, Vector2(0.0, -1.0) if weapon != &"heal" else Vector2.ZERO)
		for i: int in 12:
			_fx.sync(_sim, DT)
			assert_eq(_fx.context().ground.live_smears(), 0, "%s: no smear (frame %d)" % [weapon, i])
		_fx.queue_free()


# ---- thrown objects -----------------------------------------------------------------------------------------------------

func test_the_rock_is_smeared_along_its_flight_on_the_first_frames_only() -> void:
	_setup(Vector2(0.0, 1.0))
	_cast(&"bato_light", Vector2(0.0, -1.0))
	_fx.sync(_sim, 0.0)
	var id: int = _sim.weapons.projectiles[0].id
	var lengths: Array[float] = []
	for i: int in 8:
		_frame()
		var squash: Node3D = _fx.projectiles.node_for(id).get_node("Squash") as Node3D
		var along: Vector3 = squash.basis * Vector3(0.0, 0.0, -1.0)
		lengths.append(along.length())
		if i < 3:
			assert_true(along.normalized().is_equal_approx(Vector3(0.0, 0.0, -1.0)) or along.length() < 0.2, "the stretch lies along the flight (frame %d)" % i)
	assert_gt(lengths[1], 1.2, "smeared on the second frame, once it has popped out")
	assert_almost_eq(lengths[7], 1.0, 0.0001, "crisp in the slow part of the flight")
	assert_almost_eq(lengths[5], 1.0, 0.0001, "crisp from the fifth frame")


func test_the_slipper_is_crisp_between_the_throw_and_the_turn_back() -> void:
	_setup(Vector2(20.0, 8.0))
	_cast(&"tsinelas_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, 0.0)
	var id: int = _sim.weapons.projectiles[0].id
	var smeared_after_turn: bool = false
	var crisp_between: int = 0
	var turned: bool = false
	for i: int in 80:
		_frame()
		if _sim.weapons.projectiles.is_empty():
			break
		var rig: Node3D = _fx.projectiles.node_for(id)
		var length: float = ((rig.get_node("Squash") as Node3D).basis * Vector3(0.0, 0.0, 1.0)).length()
		var back: bool = _sim.weapons.projectiles[0].returning
		if back and not turned:
			turned = true
		if not turned and i >= 6 and absf(length - 1.0) < 0.0001:
			crisp_between += 1
		if turned and not smeared_after_turn and _fx.context().ground.live_smears() > 0:
			smeared_after_turn = true
	assert_gt(crisp_between, 5, "crisp on the way out")
	assert_true(turned, "it turned back")
	assert_true(smeared_after_turn, "the snap back out of the hang leaves a trail")


func test_the_rock_hit_squashes_overshoots_and_settles_on_one() -> void:
	_setup(Vector2(0.0, 2.0))
	_cast(&"bato_light", Vector2(0.0, -1.0))
	_fx.sync(_sim, 0.0)
	while not _sim.weapons.projectiles.is_empty():
		_frame()
	assert_eq(_fx.projectiles.fading_count(), 1, "the rock's end is playing")
	var heights: Array[float] = []
	var guard: int = 0
	while _fx.projectiles.fading_count() > 0 and guard < 20:
		guard += 1
		var squash: Node3D = null
		for child: Node in _fx.projectiles.get_children():
			if child.get_node_or_null("Squash") != null and not child.is_queued_for_deletion():
				squash = child.get_node("Squash") as Node3D
		if squash == null:
			break
		heights.append(squash.basis.get_scale().y)
		_fx.sync(_sim, DT)
		_sim.step(DT)
	assert_gt(heights.size(), 4, "it plays more than the old two frames")
	assert_lt(heights[0], 0.75, "squashed flat on contact")
	var peak: float = 0.0
	for height: float in heights:
		peak = maxf(peak, height)
	assert_gt(peak, 1.05, "then overshoots")
	assert_almost_eq(heights[heights.size() - 1], 1.0, 0.0001, "and settles on exactly one before it goes")


func test_the_can_is_smeared_at_both_ends_of_its_flight_and_crisp_over_the_top() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"lata", Vector2(0.0, -1.0))
	_fx.sync(_sim, 0.0)
	var zone: WeaponSystem.Zone = _sim.weapons.zones[0]
	var lengths: Array[float] = []
	var guard: int = 0
	while not _sim.weapons.zones.is_empty() and guard < 60:
		guard += 1
		_frame()
		if _sim.weapons.zones.is_empty():
			break
		var can: Node3D = _fx.zones.node_for(zone.id).get_node("Can") as Node3D
		var squash: Node3D = can.get_node("Squash") as Node3D
		lengths.append(_max_stretch(squash.basis))
	assert_gt(lengths.size(), 10, "a flight of more than ten frames")
	assert_gt(lengths[0], 1.1, "smeared as it leaves the hand")
	assert_gt(lengths[lengths.size() - 2], 1.1, "and as it comes down")
	var middle: int = lengths.size() / 2
	assert_almost_eq(lengths[middle], 1.0, 0.0001, "crisp over the top of the arc")
	for i: int in lengths.size():
		assert_lte(lengths[i], FxSquash.SMEAR_MAX + 0.001, "never past the cap")
	# its landing ends on exactly one
	var ghosts: int = _fx.zones.fading_count()
	assert_gt(ghosts, 0, "it lands and plays its squash")
	var last_scale: float = 0.0
	var guard2: int = 0
	while _fx.zones.fading_count() > 0 and guard2 < 20:
		guard2 += 1
		for child: Node in _fx.zones.get_children():
			if child.name == "Can" and not child.is_queued_for_deletion():
				last_scale = ((child.get_node("Squash") as Node3D).basis).get_scale().y
		_sim.step(DT)
		_fx.sync(_sim, DT)
	assert_almost_eq(last_scale, 1.0, 0.0001, "settled on exactly one before it goes")


## The longest axis scale of `basis` (a smear's length).
func _max_stretch(basis: Basis) -> float:
	var s: Vector3 = basis.get_scale()
	return maxf(s.x, maxf(s.y, s.z))


func test_the_meteor_is_smeared_down_its_fall_on_the_last_frames_only() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"bato_heavy", Vector2(0.0, -0.5))
	_fx.sync(_sim, 0.0)
	var zone: WeaponSystem.Zone = _sim.weapons.zones[0]
	var lengths: Array[float] = []
	var trails: Array[int] = []
	var guard: int = 0
	while not _sim.weapons.zones.is_empty() and guard < 80:
		guard += 1
		_frame()
		if _sim.weapons.zones.is_empty():
			break
		var meteor: Node3D = _fx.zones.node_for(zone.id).get_node("Meteor") as Node3D
		lengths.append(_max_stretch((meteor.get_node("Squash") as Node3D).basis))
		trails.append(_fx.context().ground.live_smears())
	assert_gt(lengths.size(), 12)
	for i: int in lengths.size() - 6:
		assert_eq(lengths[i], 1.0, "crisp while it is still falling slowly (frame %d)" % i)
		assert_eq(trails[i], 0, "no trail yet")
	assert_gt(lengths[lengths.size() - 2], 1.2, "stretched down its fall as it comes in")
	assert_gt(trails[trails.size() - 2], 0, "with a trail up its path")
	assert_lte(_max_over(lengths), FxSquash.SMEAR_MAX + 0.001)


func _max_over(values: Array[float]) -> float:
	var best: float = 0.0
	for value: float in values:
		best = maxf(best, value)
	return best


# ---- the scissors -------------------------------------------------------------------------------------------------------

func test_the_small_snip_leaves_an_arc_on_the_closing_frames_only() -> void:
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_light", Vector2(0.0, -1.0), 10.0)
	_fx.sync(_sim, DT)
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var last: float = rig.angle()
	var closed_frames: Array[int] = []
	var smeared_frames: Array[int] = []
	for i: int in 24:
		_frame()
		if not is_instance_valid(rig) or _fx.cones.scissors().is_empty():
			break
		if rig.angle() <= ScissorsRig.CLOSED and last > ScissorsRig.CLOSED:
			closed_frames.append(i)
		last = rig.angle()
		var smears: Array[MeshInstance3D] = _fx.context().ground.active_smears()
		if not smears.is_empty():
			smeared_frames.append(i)
	assert_gt(closed_frames.size(), 1, "several snips")
	for frame: int in closed_frames:
		assert_true(smeared_frames.has(frame), "an arc on the closing frame %d" % frame)
	for frame: int in smeared_frames:
		var since_close: bool = false
		for back: int in FxSquash.TRAIL_FRAMES:
			since_close = since_close or closed_frames.has(frame - back)
		assert_true(since_close, "no arc except in the frames after a close, while its ribbon fades (%d)" % frame)
	assert_eq(FxSnips.SMEAR_FRAMES, 2)


func test_the_snip_arcs_sweep_alternately() -> void:
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_light", Vector2(0.0, -1.0), 10.0)
	_fx.sync(_sim, DT)
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var last: float = rig.angle()
	var dets: Array[float] = []
	for i: int in 30:
		_frame()
		if _fx.cones.scissors().is_empty():
			break
		var closed: bool = rig.angle() <= ScissorsRig.CLOSED and last > ScissorsRig.CLOSED
		last = rig.angle()
		if closed:
			dets.append(signf(_newest_arc_determinant()))
	assert_gt(dets.size(), 2)
	for i: int in range(1, dets.size()):
		assert_ne(dets[i], dets[i - 1], "each snip sweeps the other way from the last")


## The sweep (the sign of the determinant) of the arc just drawn: of the live ones, the most opaque.
func _newest_arc_determinant() -> float:
	var newest: MeshInstance3D = null
	for node: MeshInstance3D in _fx.context().ground.active_smears():
		if newest == null or (node.material_override as StandardMaterial3D).albedo_color.a > (newest.material_override as StandardMaterial3D).albedo_color.a:
			newest = node
	return newest.basis.determinant()


func test_the_big_snip_winds_up_without_a_smear_then_smears_longer_and_springs_back() -> void:
	_setup(Vector2(0.0, 5.0))
	_cast(&"gunting_heavy", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var snip_squash: Node3D = _fx.cones.snips.get_child(0).get_child(0) as Node3D
	var rig: ScissorsRig = _fx.cones.scissors()[0]
	var holder: Node3D = snip_squash.get_parent() as Node3D
	var rest: Vector3 = holder.position
	var pulled: float = 0.0
	var struck: bool = false
	var scales: Array[float] = []
	var guard: int = 0
	while guard < 40 and not _fx.cones.scissors().is_empty():
		guard += 1
		_frame()
		if _fx.cones.scissors().is_empty():
			break
		if not struck:
			pulled = maxf(pulled, rest.distance_to(holder.position))
			if rig.angle() <= ScissorsRig.CLOSED:
				struck = true
			else:
				assert_eq(_fx.context().ground.live_smears(), 0, "no smear while it winds up and pulls back")
		if struck:
			scales.append(snip_squash.scale.y)
	assert_gt(pulled, 0.3, "the wind-up pulled back first")
	assert_true(struck, "and then it struck")
	assert_gt(scales[0], 1.4, "the strike frame is stretched along the blades")
	assert_lt(scales[1], 0.8, "then squashed")
	var peak: float = 0.0
	for scale: float in scales.slice(2, 5):
		peak = maxf(peak, scale)
	assert_gt(peak, 1.0, "then it overshoots")
	var settled: bool = false
	for scale: float in scales:
		settled = settled or scale == 1.0
	assert_true(settled, "and settles on exactly one")


# ---- the heal and the walls ---------------------------------------------------------------------------------------------

func test_the_heal_cross_rises_on_a_green_trail_and_is_crisp_after() -> void:
	_setup(Vector2(20.0, 0.0))
	_sim.damage(1, 40)
	_cast(&"heal", Vector2.ZERO)
	_fx.sync(_sim, DT)
	var smears: Array[MeshInstance3D] = _fx.context().ground.active_smears()
	assert_eq(smears.size(), 1, "one rise trail")
	var cross: Node3D = _fx.heals.crosses()[0]
	assert_true(smears[0].basis.x.normalized().dot(Vector3.UP) > 0.5, "it runs up to the cross")
	assert_lt(smears[0].position.y, cross.position.y, "from below it")
	_frame()
	_frame()
	var stretched: float = cross.scale.y / cross.scale.x
	assert_gt(stretched, 1.2, "the cross is stretched up while it rises")
	for i: int in 8:
		_frame()
	assert_almost_eq(cross.scale.y / cross.scale.x, 1.0, 0.0001, "crisp once it is up")
	assert_eq(_fx.context().ground.live_smears(), 0, "the trail is gone after three frames")


func test_the_soil_wall_is_wide_while_low_then_stretched_thin_then_exact() -> void:
	_setup(Vector2(20.0, 0.0))
	_cast(&"langit_lupa", Vector2(0.0, -1.0))
	_fx.sync(_sim, DT)
	var shield: WeaponSystem.Shield = _sim.weapons.shields[0]
	var body: Node3D = _fx.walls.node_for(shield.id)
	var thinnest: float = 10.0
	var widest: float = 0.0
	for i: int in 12:
		_frame()
		thinnest = minf(thinnest, body.scale.x)
		widest = maxf(widest, body.scale.x)
		assert_almost_eq(body.scale.x * body.scale.y * body.scale.z, body.scale.y * pow(body.scale.x, 2.0), 0.0001)
	assert_gt(widest, 1.05, "wide while it is low")
	assert_lt(thinnest, 1.0, "thinner at the stretch past its height")
	assert_almost_eq(body.scale.x, 1.0, 0.0001, "exactly its width once it is up")
