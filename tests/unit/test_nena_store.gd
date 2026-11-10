extends GutTest
## The Nena store, polished (owner, 2026-11-09): the owner's model with its split corners welded and hard edges where its faces fold (so toon
## light falls in clean bands), the slatted pallet that floated over its ridge taken off, its picture repainted (hollow-block walls, the
## owner's sign cleaned; tools/art/nena_store/), and drawn with the polished look laid on in order: albedo, toon shading, then smoothness
## and a slight gloss. Seam: PropModel.make, ModelLook.make_polished.

const ID: String = "nena_store"


func _surface_material(model: Node3D) -> StandardMaterial3D:
	var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	return mesh.get_surface_override_material(0) as StandardMaterial3D


func test_the_store_keeps_its_shape_and_its_triangles() -> void:
	var box: AABB = PropModel.bounds_of(ID)
	assert_almost_eq(box.position.y, 0.0, 0.001, "its foot on the origin")
	assert_almost_eq(box.get_center().x, 0.0, 0.001)
	assert_almost_eq(box.get_center().z, 0.0, 0.001)
	assert_almost_eq(box.size.x, 1.0, 0.001, "as wide as made")
	assert_almost_eq(box.size.y, 0.855, 0.001, "its ridge is its top: nothing floats over the roof")
	var model: Node3D = autofree(PropModel.make(ID)) as Node3D
	var mesh: Mesh = (model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh
	assert_eq(mesh.get_surface_count(), 1, "one mesh, one material")
	assert_eq((mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3, 5720, "every triangle but the pallet's (fewer tore the roof)")


func test_the_store_is_drawn_with_the_polished_look_over_its_own_picture() -> void:
	var model: Node3D = autofree(PropModel.make(ID)) as Node3D
	var source: BaseMaterial3D = (model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh.surface_get_material(0) as BaseMaterial3D
	var look: StandardMaterial3D = _surface_material(model)
	# 1. albedo: its own cleaned picture at the matte look's gain
	assert_not_null(look.albedo_texture, "its picture")
	assert_same(look.albedo_texture, source.albedo_texture)
	assert_eq(look.albedo_color, ModelLook.make(source).albedo_color)
	# 2. toon shading
	assert_eq(look.diffuse_mode, BaseMaterial3D.DIFFUSE_TOON)
	assert_eq(look.specular_mode, BaseMaterial3D.SPECULAR_TOON)
	# 3. smoother than the matte look, with a slight gloss
	assert_almost_eq(look.roughness, ModelLook.POLISHED_ROUGHNESS, 0.0001)
	assert_lt(look.roughness, 1.0)
	assert_almost_eq(look.metallic_specular, ModelLook.POLISHED_SPECULAR, 0.0001)
	assert_lt(look.metallic_specular, 0.5, "only a slight gloss")
	assert_eq(look.metallic, 0.0, "not metal")
	# the rest of the model look stays
	assert_true(look.rim_enabled)
	assert_almost_eq(look.rim, ModelLook.RIM, 0.0001)
	assert_false(look.normal_enabled)
	assert_eq(look.texture_filter, BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC)


func test_other_props_keep_the_matte_look() -> void:
	var look: StandardMaterial3D = _surface_material(autofree(PropModel.make("bench")) as Node3D)
	assert_eq(look.diffuse_mode, BaseMaterial3D.DIFFUSE_LAMBERT_WRAP)
	assert_eq(look.specular_mode, BaseMaterial3D.SPECULAR_DISABLED)
	assert_eq(look.roughness, 1.0)
