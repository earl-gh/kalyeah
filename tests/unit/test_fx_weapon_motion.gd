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
