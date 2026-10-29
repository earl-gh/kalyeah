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
