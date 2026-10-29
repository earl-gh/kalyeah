extends GutTest
## Ground marks of a hit area use the sim's exact numbers (never bigger or smaller), a delayed area fills in exactly over
## its delay, nothing flat sits on the street itself (it would z-fight), and the flat layers draw in the ladder's order.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func _practice(first: StringName) -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	practice.sim.players[PracticeMatch.LOCAL_ID].position = Vector2(0.0, 4.0)
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, -1.0)
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, first, &"heal" if first != &"heal" else &"bola")
	return practice


func _cast(practice: PracticeMatch) -> void:
	practice.card_pressed(0)
	practice.card_released(0, Vector2.ZERO, true, true)
	practice.advance(DT)
	practice.advance(DT)


## The largest distance from the origin of a vertex of `mesh` drawn at alpha above `min_alpha`, minus half the rim.
func _edge(mesh: Mesh, min_alpha: float) -> float:
	var arrays: Array = mesh.surface_get_arrays(0)
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] as PackedColorArray
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var far: float = 0.0
	for i: int in vertices.size():
		if colors[i].a > min_alpha:
			far = maxf(far, Vector2(vertices[i].x, vertices[i].z).length())
	return far - FxKit.RIM / 2.0


func test_every_hit_area_mark_is_exact() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	for def: WeaponDef in rules.weapons:
		match def.id:
			&"bato_heavy", &"lata", &"jacks":
				assert_almost_eq(_edge(FxGround.circle_mark(def.radius, Color.RED, 0.4), 0.9), def.radius, 0.01, "%s circle is its radius" % def.id)
			&"papel_trap":
				assert_almost_eq(_edge(FxGround.circle_mark(def.trigger_radius, Color.RED, 0.0, 0.5), 0.45), def.trigger_radius, 0.01, "trap: its trigger range")
				assert_almost_eq(_edge(FxGround.circle_mark(def.radius, Color.RED, 0.25), 0.9), def.radius, 0.01, "then its slow zone")
			&"gunting_light", &"gunting_heavy":
				var wedge: PackedVector2Array = FxGround.wedge(def)
				var widest: float = 0.0
				for point: Vector2 in wedge:
					assert_lte(point.length(), def.max_range + 0.001, "%s reaches its range" % def.id)
					if point.length() > 0.01:
						widest = maxf(widest, absf(Vector2(0.0, -1.0).angle_to(point)))
				assert_almost_eq(rad_to_deg(widest) * 2.0, def.angle_degrees, 0.01, "%s is its angle" % def.id)
				assert_almost_eq(_edge(FxGround.wedge_mark(def, Color.RED, 0.4), 0.9), def.max_range, 0.06, "%s rim is at its range" % def.id)


func test_progress_fill_matches_the_delay() -> void:
	var practice: PracticeMatch = _practice(&"bato_heavy")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice)
	var zone: WeaponSystem.Zone = practice.sim.weapons.zones[0]
	var disc: Node3D = fx.zones.node_for(zone.id).get_node("Progress") as Node3D
	var total: float = zone.def.delay
	var seen: float = 0.0
	while not practice.sim.weapons.zones.is_empty():
		var expected: float = 1.0 - zone.time_left / total
		assert_almost_eq(disc.scale.x, maxf(expected, FxZones.PROGRESS_START), 0.07, "the fill follows the real delay (%.2f)" % expected)
		seen = disc.scale.x
		practice.advance(DT)
	assert_gt(seen, 0.9, "it is full when it lands")


func test_no_decal_sits_at_ground_zero() -> void:
	for weapon: StringName in [&"bato_heavy", &"jacks", &"papel_trap", &"gunting_heavy", &"bola"]:
		var practice: PracticeMatch = _practice(weapon)
		var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
		_cast(practice)
		practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9)
		for i: int in 3:
			practice.advance(DT)
		for node: Node in fx.find_children("*", "MeshInstance3D", true, false):
			var mesh: MeshInstance3D = node as MeshInstance3D
			if mesh.is_in_group(WeaponModel.GROUP) or not mesh.is_visible_in_tree() or mesh.mesh == null:
				continue
			var bottom: float = (mesh.global_transform * mesh.mesh.get_aabb()).position.y
			assert_gt(bottom, 0.004, "%s: %s is above the street art" % [weapon, mesh.name])
		practice.free()


func test_render_priorities_follow_the_ladder() -> void:
	assert_lt(FxKit.PRIO_FILL, FxKit.PRIO_RIM)
	assert_lt(FxKit.PRIO_RIM, FxKit.PRIO_SHADOW)
	assert_lt(FxKit.PRIO_SHADOW, FxKit.PRIO_DECAL)
	assert_lt(FxKit.Y_FILL, FxKit.Y_RIM)
	assert_lt(FxKit.Y_RIM, FxKit.Y_SHADOW)
	assert_lt(FxKit.Y_SHADOW, FxKit.Y_DECAL)
	assert_lt(FxKit.Y_DECAL, FxKit.Y_FLASH)
	var practice: PracticeMatch = _practice(&"bato_heavy")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice)
	var mark: MeshInstance3D = fx.zones.node_for(practice.sim.weapons.zones[0].id).get_node("Mark") as MeshInstance3D
	assert_eq((mark.material_override as Material).render_priority, FxKit.PRIO_FILL, "zone marks draw first")
	for shadow: MeshInstance3D in fx.context().shadows.all_nodes():
		assert_eq((shadow.material_override as Material).render_priority, FxKit.PRIO_SHADOW)
	for decal: MeshInstance3D in fx.context().ground.all_nodes():
		assert_eq((decal.material_override as Material).render_priority, FxKit.PRIO_DECAL)
