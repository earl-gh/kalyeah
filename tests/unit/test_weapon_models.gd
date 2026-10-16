extends GutTest
## The owner's 3D models for the casting skills: slipper (tsinelas), can (lata), marble (bola),
## crumpled paper (papel trap) and paper sheet (papel shield).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
const MODELS: Array[String] = ["slipper", "can", "marble", "crumpled_paper", "paper", "trumpo", "spikes", "meteor", "scissor", "soil_block", "green_cross"]


func test_every_model_exists_light_and_whole() -> void:
	for id: String in MODELS:
		var path: String = WeaponModel.PATH % id
		assert_true(ResourceLoader.exists(path), "%s is there" % id)
		var counts: Vector2i = MeshAudit.open_edges_and_triangles(path)
		assert_between(counts.y, 1000, 3500, "%s is light for phones" % id)
		assert_lt(float(counts.x) / float(counts.y), 0.35, "%s is a whole surface" % id)


func test_the_models_are_matched_to_their_weapons() -> void:
	assert_eq(WeaponModel.OBJECTS[&"tsinelas_light"][0], "slipper")
	assert_eq(WeaponModel.OBJECTS[&"tsinelas_heavy"][0], "slipper")
	assert_eq(WeaponModel.OBJECTS[&"lata"][0], "can")
	assert_eq(WeaponModel.OBJECTS[&"bola"][0], "marble")
	assert_eq(WeaponModel.OBJECTS[&"papel_trap"][0], "crumpled_paper")
	assert_eq(WeaponModel.OBJECTS[&"trumpo"][0], "trumpo")
	assert_false(WeaponModel.has_model(&"bato_light"), "the light rock keeps its code-drawn object")


func test_a_thrown_object_is_scaled_to_its_length_and_centred() -> void:
	for id: StringName in WeaponModel.OBJECTS:
		var node: Node3D = autofree(WeaponModel.make(id, true))
		var mesh: MeshInstance3D = node.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var box: AABB = mesh.get_aabb()
		var scaled: Vector3 = box.size * mesh.get_parent().scale
		var want: float = WeaponModel.OBJECTS[id][1] as float
		assert_almost_eq(maxf(scaled.x, maxf(scaled.y, scaled.z)), want, 0.01, "%s is as long as it should be" % id)
		var middle: float = (box.position.y + box.size.y / 2.0) * mesh.get_parent().scale.y + mesh.get_parent().position.y
		assert_almost_eq(middle, 0.0, 0.01, "%s is centred" % id)
	var heavy: Node3D = autofree(WeaponModel.make(&"tsinelas_heavy", true))
	var light: Node3D = autofree(WeaponModel.make(&"tsinelas_light", true))
	assert_gt(heavy.get_child(0).scale.x, light.get_child(0).scale.x, "the heavy slipper is bigger")


func test_a_placed_trap_stands_on_the_ground() -> void:
	var node: Node3D = autofree(WeaponModel.make(&"papel_trap", false))
	assert_almost_eq(node.get_child(0).position.y, 0.0, 0.001)


func test_the_paper_wall_has_the_walls_size() -> void:
	var node: Node3D = autofree(WeaponModel.shield(3.0, 1.6, 0.3))
	var mesh: MeshInstance3D = node.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var scaled: Vector3 = mesh.get_aabb().size * mesh.get_parent().scale
	assert_almost_eq(scaled.x, 3.0, 0.01)
	assert_almost_eq(scaled.y, 1.6, 0.01)
	assert_almost_eq(scaled.z, 0.3, 0.01)


func test_the_match_draws_the_models_for_the_casts() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.position = Vector2(0.0, 4.0)
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"tsinelas_heavy", &"papel_shield")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.aim_started(0)
	practice.aim_released(Vector2.ZERO, false, 0)
	practice.aim_started(1)
	practice.aim_released(Vector2.ZERO, false, 1)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(fx.live_count(), 2)
	var meshes: Array[Node] = fx.find_children("*", "MeshInstance3D", true, false)
	assert_eq(meshes.size(), 2, "the slipper and the paper sheet, each one mesh")
	for mesh: Node in meshes:
		assert_true((mesh as MeshInstance3D).mesh is ArrayMesh and not (mesh as MeshInstance3D).mesh is BoxMesh)
		assert_gt((mesh as MeshInstance3D).mesh.get_surface_count(), 0)


## A practice match with the local player equipped and one dummy straight ahead.
func _casting(first: StringName, second: StringName) -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.position = Vector2(0.0, 4.0)
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, -1.0)
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, first, second)
	return practice


func _cast(practice: PracticeMatch, slot: int) -> void:
	practice.aim_started(slot)
	practice.aim_released(Vector2.ZERO, false, slot)
	practice.advance(DT)
	practice.advance(DT)


func test_the_rock_meteor_falls_onto_its_circle() -> void:
	var practice: PracticeMatch = _casting(&"bato_heavy", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice, 0)
	var meteor: Node3D = fx.find_child("Meteor", true, false) as Node3D
	assert_not_null(meteor, "a meteor comes with the landing circle")
	var high: float = meteor.position.y
	for i: int in 12:
		practice.advance(DT)
	assert_lt(meteor.position.y, high, "it falls")
	assert_gt(high, 3.0, "from the sky")


func test_the_jacks_field_is_spikes_on_a_circle() -> void:
	var practice: PracticeMatch = _casting(&"jacks", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice, 0)
	assert_eq(fx.find_children("*", "MeshInstance3D", true, false).size(), WeaponModel.SPIKE_CLUSTERS + 1, "the spike clusters and the tinted circle")


func test_the_spinning_top_is_its_model_and_spins_fast() -> void:
	var practice: PracticeMatch = _casting(&"trumpo", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice, 0)
	var meshes: Array[Node] = fx.find_children("*", "MeshInstance3D", true, false)
	assert_eq(meshes.size(), 1)
	assert_true((meshes[0] as MeshInstance3D).mesh is ArrayMesh)
	assert_gt(WeaponFxView.TOP_SPIN, WeaponFxView.SPIN)


func test_langit_lupa_rises_as_the_soil_block() -> void:
	var practice: PracticeMatch = _casting(&"langit_lupa", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_cast(practice, 0)
	assert_eq(fx.live_count(), 1)
	var mesh: MeshInstance3D = fx.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	assert_false(mesh.mesh is BoxMesh, "the owner's soil block, not a box")
	for i: int in 30 * 5:
		practice.advance(DT)
	assert_eq(fx.live_count(), 0, "it collapses and is gone")


func test_the_scissors_snip_for_gunting() -> void:
	var practice: PracticeMatch = _casting(&"gunting_heavy", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	for i: int in 30:
		practice.aim_started(0)
		practice.aim_released(Vector2.ZERO, false, 0)
		practice.advance(DT)
		if fx.find_child("Scissors", true, false) != null:
			break
	var scissors: Node3D = fx.find_child("Scissors", true, false) as Node3D
	assert_not_null(scissors, "scissors show where it snips")
	var start: Vector3 = scissors.position
	practice.advance(DT)
	practice.advance(DT)
	if is_instance_valid(scissors):
		assert_ne(scissors.position, start, "they lunge forward")


func test_a_heal_floats_a_green_cross_over_the_kid_and_it_goes_away() -> void:
	var practice: PracticeMatch = _casting(&"heal", &"lata")
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.sim.damage(PracticeMatch.LOCAL_ID, 40)
	_cast(practice, 0)
	var crosses: Array[Node] = fx.find_children("HealCross*", "Node3D", true, false)
	assert_gt(crosses.size(), 0, "a cross appears")
	assert_gt((crosses[0] as Node3D).position.y, 2.0, "above the head")
	for i: int in 40:
		practice.advance(DT)
	var left: Array[Node] = fx.find_children("HealCross*", "Node3D", true, false).filter(func(c: Node) -> bool: return not c.is_queued_for_deletion())
	assert_eq(left.size(), 0, "and fades away")
