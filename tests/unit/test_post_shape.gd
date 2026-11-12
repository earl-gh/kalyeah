extends GutTest
## The electric post is drawn long and slim, but only its pole is stretched (owner, 2026-11-09: "adjust ... the components except for the pole,
## the flyers too"): the crossarm, transformer, router box, brackets and flyers keep their own proportions and stay on the pole.
## Seam: PostShape (the reshaped mesh), StreetMap._add_post, MapLayout.post_width_share.

const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const MODELS: Array[String] = ["post_blue", "post_red"]
## Surfaces that are one piece each (the others, the brackets and the crossarm's steel, have pieces in several assemblies).
const SINGLE_PIECES: Array[String] = ["transformer_tank_paint", "isp_box_plastic", "isp_box_lid", "isp_label", "polymer_grey"]


func _source(id: String) -> ArrayMesh:
	var scene: Node3D = (load("res://assets/models/props/%s.glb" % id) as PackedScene).instantiate() as Node3D
	var mesh: ArrayMesh = (scene.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh as ArrayMesh
	scene.free()
	return mesh


func _box(mesh: Mesh, surface: int) -> AABB:
	var corners: PackedVector3Array = mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var box: AABB = AABB(corners[0], Vector3.ZERO)
	for corner: Vector3 in corners:
		box = box.expand(corner)
	return box


func _surface(mesh: Mesh, ending: String) -> int:
	for surface: int in mesh.get_surface_count():
		if mesh.surface_get_material(surface).resource_name.ends_with(ending):
			return surface
	return -1


func test_the_pole_stays_as_made_and_every_part_on_it_is_squeezed_upward_by_the_share() -> void:
	var share: float = LAYOUT.post_width_share
	for id: String in MODELS:
		var source: ArrayMesh = _source(id)
		var made: ArrayMesh = PostShape.reshaped(source, share)
		assert_eq(made.get_surface_count(), source.get_surface_count(), "%s: the same surfaces" % id)
		for surface: int in source.get_surface_count():
			assert_eq(made.surface_get_material(surface), source.surface_get_material(surface), "%s: surface %d keeps its material" % [id, surface])
			var before: AABB = _box(source, surface)
			var after: AABB = _box(made, surface)
			if PostShape.is_pole(source, surface):
				assert_eq(made.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX], source.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX], "%s: the pole itself is not touched" % id)
				continue
			var name: String = source.surface_get_material(surface).resource_name
			assert_almost_eq(after.size.x, before.size.x, 0.0001, "%s %s: as wide" % [id, name])
			assert_almost_eq(after.size.z, before.size.z, 0.0001)
			if SINGLE_PIECES.any(func(ending: String) -> bool: return name.ends_with(ending)):
				# one piece: squeezed upward by exactly the share, so the post's scale gives it its own shape back (a surface spread over several
				# assemblies, the brackets and the crossarm's steel, shrinks round each assembly's own middle instead)
				assert_almost_eq(after.size.y, before.size.y * share, 0.0001, "%s %s: squeezed by the share" % [id, name])


func test_the_flyers_and_the_transformer_keep_their_own_proportions_on_the_drawn_post() -> void:
	var share: float = LAYOUT.post_width_share
	for id: String in MODELS:
		var source: ArrayMesh = _source(id)
		var made: ArrayMesh = PostShape.reshaped(source, share)
		var scale: Vector3 = Vector3(share, 1.0, share)  # the post node's shape (its uniform size aside)
		for ending: String in ["transformer_tank_paint", "isp_box_plastic"]:
			var surface: int = _surface(source, ending)
			assert_ne(surface, -1, "%s has its %s" % [id, ending])
			var before: Vector3 = _box(source, surface).size
			var drawn: Vector3 = _box(made, surface).size * scale
			assert_almost_eq(drawn.y / drawn.x, before.y / before.x, 0.001, "%s %s: as tall for its width as made" % [id, ending])
		for surface: int in source.get_surface_count():
			if source.surface_get_material(surface).resource_name.find("flyer") >= 0:
				var before: Vector3 = _box(source, surface).size
				var drawn: Vector3 = _box(made, surface).size * scale
				assert_almost_eq(drawn.y / maxf(drawn.x, drawn.z), before.y / maxf(before.x, before.z), 0.001, "%s: a flyer is not stretched" % id)


func test_the_crossarm_stays_flush_with_the_poles_top() -> void:
	for id: String in MODELS:
		var source: ArrayMesh = _source(id)
		var made: ArrayMesh = PostShape.reshaped(source, LAYOUT.post_width_share)
		var top: float = 0.0
		for surface: int in source.get_surface_count():
			if PostShape.is_pole(source, surface):
				top = maxf(top, _box(source, surface).end.y)
		var arm: int = _surface(source, "porcelain_brown")  # the insulators on the crossarm, above the pole's top
		var before: AABB = _box(source, arm)
		var after: AABB = _box(made, arm)
		assert_almost_eq(after.end.y - top, (before.end.y - top) * LAYOUT.post_width_share, 0.0001, "%s: the top assembly keeps the pole's top where it is" % id)
		assert_gt(after.end.y, top, "the insulators still stand above the pole")


func test_normals_stay_whole_and_the_mesh_is_made_once() -> void:
	var source: ArrayMesh = _source("post_red")
	var made: ArrayMesh = PostShape.reshaped(source, 0.55)
	assert_same(PostShape.reshaped(source, 0.55), made, "made once per model and share")
	for surface: int in made.get_surface_count():
		for normal: Vector3 in made.surface_get_arrays(surface)[Mesh.ARRAY_NORMAL] as PackedVector3Array:
			assert_almost_eq(normal.length(), 1.0, 0.001)
			if normal.length() < 0.999:
				break


func test_the_match_draws_the_reshaped_post_and_a_full_width_post_is_left_as_made() -> void:
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		for kit_id: String in [StreetMap.POST_BLUE, StreetMap.POST_RED]:
			var model: Node3D = map._posts[side].get_node(kit_id) as Node3D
			assert_not_null(model, "%s is placed at side %d" % [kit_id, side])
			assert_almost_eq(model.scale.x / model.scale.y, LAYOUT.post_width_share, 0.001, "%s uses post_width_share" % kit_id)
	var reshaped_node: Node3D = (load("res://assets/models/props/post_red.glb") as PackedScene).instantiate() as Node3D
	PostShape.apply(reshaped_node, LAYOUT.post_width_share)
	assert_same((reshaped_node.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh, PostShape.reshaped(_source("post_red"), LAYOUT.post_width_share))
	reshaped_node.free()
	var plain: Node3D = (load("res://assets/models/props/post_red.glb") as PackedScene).instantiate() as Node3D
	var before: Mesh = (plain.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh
	PostShape.apply(plain, 1.0)
	assert_same((plain.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh, before, "share 1: nothing to undo")
	plain.free()
