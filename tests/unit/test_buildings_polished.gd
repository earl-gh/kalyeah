extends GutTest
## The street's buildings were given the same steps as the Nena store (owner, 2026-11-10): floating shards taken off, the mesh welded with hard
## edges where it folds, the picture repainted piece by piece (hollow-block walls, wood with grain, roofs with their ridges and rust, everything
## else its own colours cleaned into flat tones, the signs cleaned into their own band of the picture) and drawn with the polished look. The
## karinderya is the owner's own model, drawn as made. Seam: PropModel.make, PropModel.POLISHED; tools/art/buildings/.

const BUILT: Array[String] = ["vulcanizing", "barber_shop", "house_1", "house_2", "house_3", "tricycle_terminal"]


func test_every_building_on_the_street_but_the_karinderya_is_polished() -> void:
	var street: Array[String] = []
	street.append_array(StreetMap.BUILDINGS)
	street.append_array(StreetMap.FILLER_HOUSES)
	street.append(StreetMap.TERMINAL)
	for id: String in street:
		if id == StreetMap.KARINDERYA:
			continue
		assert_true(PropModel.POLISHED.has(id), "%s is polished" % id)
	assert_false(PropModel.POLISHED.has(StreetMap.KARINDERYA), "the karinderya is drawn as its maker made it")
	assert_true(PropModel.AS_MADE.has(StreetMap.KARINDERYA))


func test_each_keeps_its_shape_its_triangles_and_one_material() -> void:
	for id: String in BUILT:
		var box: AABB = PropModel.bounds_of(id)
		assert_almost_eq(box.position.y, 0.0, 0.002, "%s stands on the ground" % id)
		assert_almost_eq(box.get_center().x, 0.0, 0.01, "%s is centred" % id)
		var model: Node3D = autofree(PropModel.make(id)) as Node3D
		var mesh: Mesh = (model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh
		assert_eq(mesh.get_surface_count(), 1, "%s: one mesh, one material" % id)
		var triangles: int = (mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3
		assert_between(triangles, 5000, 6800, "%s: its kept parts and its new roof" % id)


func test_each_is_drawn_with_the_polished_look_over_its_own_picture() -> void:
	for id: String in BUILT:
		var model: Node3D = autofree(PropModel.make(id)) as Node3D
		var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		var look: StandardMaterial3D = mesh.get_surface_override_material(0) as StandardMaterial3D
		assert_same(look.albedo_texture, (mesh.mesh.surface_get_material(0) as BaseMaterial3D).albedo_texture, "%s: its own picture" % id)
		assert_eq(look.diffuse_mode, BaseMaterial3D.DIFFUSE_TOON, id)
		assert_eq(look.specular_mode, BaseMaterial3D.SPECULAR_TOON, id)
		assert_almost_eq(look.roughness, ModelLook.POLISHED_ROUGHNESS, 0.0001, id)
