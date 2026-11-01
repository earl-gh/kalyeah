extends GutTest
## Tumbang Preso (owner, 2026-11-01): every kid's slipper has its own colour, lying on the street and in the air, the same on every phone
## (given by the kid's place among the players). Agawan Base's tsinelas skill keeps its own look.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _kind: StringName
var _game: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game


func _practice(game: StringName) -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = game
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	return practice


## The colour painted on `node`'s slipper mesh (its vertex colours), or null when it is not painted.
func _painted(node: Node) -> Variant:
	var meshes: Array[Node] = node.find_children("*", "MeshInstance3D", true, false)
	if node is MeshInstance3D:
		meshes.push_front(node)
	for child: Node in meshes:
		var mesh: Mesh = (child as MeshInstance3D).mesh
		if mesh == null or mesh.get_surface_count() == 0:
			continue
		var arrays: Array = mesh.surface_get_arrays(0)
		if arrays.size() <= Mesh.ARRAY_COLOR:
			continue
		var colours: Variant = arrays[Mesh.ARRAY_COLOR]
		if colours is PackedColorArray and not (colours as PackedColorArray).is_empty():
			return (colours as PackedColorArray)[0]
	return null


func test_six_kids_have_six_different_slipper_colours() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var seen: Array[Color] = []
	for id: int in practice.sim.players:
		var colour: Color = StreetModeView.slipper_colour(practice.sim, id)
		assert_false(seen.has(colour), "kid %d's colour is its own" % id)
		seen.append(colour)
	assert_eq(seen.size(), 6)


func test_a_slipper_lying_on_the_street_wears_its_kids_colour() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	for id: int in practice.sim.players:
		if id != game.taya_id:
			game.slippers[id] = Vector2(float(id) - 3.0, -6.0)
	practice.advance(DT)
	for id: int in game.slippers:
		var node: Node3D = practice.mode_view.slipper_node(id)
		assert_not_null(node, "kid %d's slipper is drawn" % id)
		assert_eq(_painted(node), StreetModeView.slipper_colour(practice.sim, id), "in kid %d's colour" % id)


func test_a_thrown_slipper_keeps_its_kids_colour_in_the_air() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	var thrower: PlayerState = practice.sim.players[practice.local_id]
	game.throw(practice.sim, thrower, Vector2(0.0, -1.0), 14.0)
	var projectile: WeaponSystem.Projectile = game.flights[thrower.id]
	assert_eq(StreetModeView.slipper_owner(practice.sim, projectile), thrower.id, "the game knows whose it is")
	practice.advance(DT)
	var colour: Color = StreetModeView.slipper_colour(practice.sim, thrower.id)
	var found: bool = false
	for node: Node in practice.find_children("*", "MeshInstance3D", true, false):
		if _painted(node) == colour and not practice.mode_view.is_ancestor_of(node):
			found = true
	assert_true(found, "the slipper in the air is painted the thrower's colour")


func test_the_owner_of_a_slipper_is_found_on_a_phone_by_the_projectiles_id() -> void:
	# online the copy's projectiles are rebuilt from the snapshot each time: the same id, a new object
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	game.throw(practice.sim, practice.sim.players[practice.local_id], Vector2(0.0, -1.0), 14.0)
	var copy: WeaponSystem.Projectile = WeaponSystem.Projectile.new()
	copy.id = game.flights[practice.local_id].id
	assert_eq(StreetModeView.slipper_owner(practice.sim, copy), practice.local_id)


func test_agawan_base_tsinelas_stays_unpainted() -> void:
	var practice: PracticeMatch = _practice(&"street")
	var projectile: WeaponSystem.Projectile = WeaponSystem.Projectile.new()
	assert_eq(StreetModeView.slipper_owner(practice.sim, projectile), -1, "no street game, no slipper owner")
	assert_null(_painted(WeaponModel.make(&"tsinelas_light", false)), "the skill's slipper keeps its own look")
