extends GutTest
## Reduce motion keeps the facts and drops the motion (docs/ART.md "Readability and reduced motion"): for every weapon the
## ground marks, the travel and the end states stay; no particles, no smears, no squash and no spin.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const ALL: Array[StringName] = [&"bato_light", &"bato_heavy", &"gunting_light", &"gunting_heavy", &"papel_trap", &"papel_shield", &"tsinelas_light", &"tsinelas_heavy", &"lata", &"jacks", &"bola", &"trumpo", &"heal", &"langit_lupa"]

var _rules: GameRules
var _motion: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = true


func after_each() -> void:
	Settings.reduced_motion = _motion


func test_every_weapon_keeps_its_marks_and_drops_its_motion() -> void:
	for weapon: StringName in ALL:
		var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
		sim.add_player(1, 0)
		sim.add_player(2, 1)
		sim.add_player(3, 0)
		sim.players[1].position = Vector2(0.0, 8.0)
		sim.players[1].facing = Vector2(0.0, -1.0)
		sim.players[2].position = Vector2(0.0, 4.5)
		sim.players[3].position = Vector2(1.0, 9.0)
		sim.phase = MatchSim.Phase.PLAYING
		var fx: WeaponFxView = WeaponFxView.new()
		add_child(fx)
		fx.view_scale = 2.0
		fx.watch(sim)
		sim.damage(1, 30)
		var def: WeaponDef = sim.weapon_defs[weapon]
		var aim: Vector2 = Vector2.ZERO if def.shape == WeaponDef.Shape.HEAL else Vector2(0.0, -3.5 / maxf(def.max_range, 3.5))
		assert_true(sim.weapons.fire(sim, sim.players[1], def, aim, 1.0, 2), "%s cast" % weapon)
		sim.weapon_cast.emit(1, weapon)
		var spins: Dictionary[Node3D, Basis] = {}
		var marks: int = 0
		for frame: int in 10:
			sim.step(DT)
			fx.sync(sim, DT)
			assert_eq(fx.particle_count(), 0, "%s: no particles" % weapon)
			assert_eq(fx.context().ground.live_smears(), 0, "%s: no smears" % weapon)
			for node: Node in fx.find_children("Squash", "Node3D", true, false):
				if not _gone(node):
					assert_true((node as Node3D).basis.is_equal_approx(Basis.IDENTITY), "%s: no squash or stretch (frame %d)" % [weapon, frame])
			for node: Node in fx.find_children("Spin", "Node3D", true, false):
				var spin: Node3D = node as Node3D
				if _gone(spin):
					continue
				if spins.has(spin):
					assert_true(spin.basis.is_equal_approx(spins[spin]), "%s: no spin or tumble" % weapon)
				else:
					spins[spin] = spin.basis
			for node: Node in fx.find_children("*", "MeshInstance3D", true, false):
				var mesh: MeshInstance3D = node as MeshInstance3D
				if mesh.is_visible_in_tree() and not mesh.is_in_group(WeaponModel.GROUP) and not _gone(mesh):
					marks += 1
		assert_gt(marks, 0, "%s: its ground mark (circle, wedge, ribbon, ring or shadow) still shows" % weapon)
		fx.free()


## True when `node` or a parent was freed this frame (the loop runs inside one engine frame).
func _gone(node: Node) -> bool:
	var at: Node = node
	while at != null:
		if at.is_queued_for_deletion():
			return true
		at = at.get_parent()
	return false
