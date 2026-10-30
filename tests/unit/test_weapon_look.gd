extends GutTest
## The painted look of the weapon models (owner: nearest to Clash Royale's texture): painted light and a contour, no gloss, never black or grey.

const WEAPONS: Array[StringName] = [&"tsinelas_light", &"lata", &"bola", &"papel_trap", &"trumpo", &"bato_light"]


func _surfaces(root: Node) -> Array[Material]:
	var found: Array[Material] = []
	for node: Node in root.find_children("*", "MeshInstance3D", true, false):
		var mesh: MeshInstance3D = node as MeshInstance3D
		for surface: int in mesh.mesh.get_surface_count():
			found.append(mesh.get_surface_override_material(surface))
	return found


func test_every_weapon_surface_has_the_painted_look_and_its_contour() -> void:
	var models: Array[Node3D] = [WeaponModel.meteor(), WeaponModel.cross(), WeaponModel.scissors(1.0), WeaponModel.spikes(1.0), WeaponModel.shield(1.0, 1.0, 0.2), WeaponModel.soil(1.0, 1.0, 1.0)]
	for id: StringName in WEAPONS:
		models.append(WeaponModel.make(id, true))
	for model: Node3D in models:
		autofree(model)
		var surfaces: Array[Material] = _surfaces(model)
		assert_gt(surfaces.size(), 0, "the model has surfaces")
		for material: Material in surfaces:
			var look: ShaderMaterial = material as ShaderMaterial
			assert_not_null(look, "a weapon surface carries the painted look")
			if look == null:
				continue
			assert_eq(look.shader, WeaponLook.LOOK)
			assert_not_null(WeaponLook.picture_of(look), "it keeps its own picture")
			var contour: ShaderMaterial = look.next_pass as ShaderMaterial
			assert_not_null(contour, "a contour is the second pass")
			if contour != null:
				assert_eq(contour.shader, WeaponLook.OUTLINE)
				assert_same(WeaponLook.picture_of(contour), WeaponLook.picture_of(look), "the contour is a deep tone of the same picture")


func test_the_light_is_painted_not_shiny_and_never_clips_or_greys() -> void:
	var code: String = WeaponLook.LOOK.code
	assert_true(code.contains("unshaded"), "no scene light and no gloss: the painted light is the only light")
	assert_false(code.contains("SPECULAR") or code.contains("ROUGHNESS") or code.contains("METALLIC"), "no gloss at all")
	assert_true(code.contains("min(colour * top_boost, vec3(1.0))"), "the lifted top never passes white")


func test_the_shadow_goes_cool_and_deep_the_contour_is_never_black() -> void:
	var look: ShaderMaterial = WeaponLook.make(null)
	assert_null(WeaponLook.picture_of(look), "a surface with no picture gets none")
	var shade: Vector3 = _default(WeaponLook.LOOK, "shadow_tint")
	assert_gt(shade.z, shade.x, "the shadow keeps more blue than red: it goes violet, not grey")
	assert_lt(shade.x, 0.8, "and is clearly darker")
	assert_gt(shade.x, 0.4, "but never black")
	var ink: Vector3 = _default(WeaponLook.OUTLINE, "ink")
	assert_gt(minf(ink.x, minf(ink.y, ink.z)), 0.15, "the contour is a deep tone, not black")
	assert_gt(_default(WeaponLook.OUTLINE, "width"), 0.02, "the contour is visible at phone size")
	assert_lt(_default(WeaponLook.OUTLINE, "width"), 0.12, "and stays a line")


## The default value of a uniform in `shader`, read from its source.
func _default(shader: Shader, uniform: String) -> Variant:
	var found: RegExMatch = RegEx.create_from_string("uniform [a-z0-9]+ %s[^=]*= ([^;]+);" % uniform).search(shader.code)
	assert_not_null(found, "%s is a uniform with a default" % uniform)
	if found == null:
		return 0.0
	var text: String = found.get_string(1).strip_edges()
	if text.begins_with("vec3("):
		var parts: PackedStringArray = text.trim_prefix("vec3(").trim_suffix(")").split(",")
		return Vector3(parts[0].to_float(), parts[1].to_float(), parts[2].to_float())
	return text.to_float()
