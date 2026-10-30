class_name WeaponLook
extends RefCounted
## The look of the thrown and placed weapon models, replacing `ModelLook` for them (owner: nearest to Clash Royale's painted texture): a
## painted light that lifts the top faces and turns the shadows deep and cool (`weapon_look.gdshader`), and a contour in a deep tone of the
## picture's own colour (`weapon_outline.gdshader`), so each object reads against the street. Still no gloss at all. The kids and the
## props keep `ModelLook` / the owner's own material.

const LOOK: Shader = preload("res://scripts/view/art/weapon_look.gdshader")
const OUTLINE: Shader = preload("res://scripts/view/art/weapon_outline.gdshader")


## Gives every surface of every mesh under `root` the weapon look (its picture kept) and returns the materials, one per surface.
static func apply(root: Node) -> Array[Material]:
	var made: Array[Material] = []
	for node: Node in root.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		for surface: int in mesh_instance.mesh.get_surface_count():
			var material: ShaderMaterial = make(mesh_instance.get_active_material(surface))
			mesh_instance.set_surface_override_material(surface, material)
			made.append(material)
	return made


## The look for a surface whose own material is `source`: its picture and colour carried over, with the contour as the second pass.
static func make(source: Material) -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = LOOK
	var outline: ShaderMaterial = ShaderMaterial.new()
	outline.shader = OUTLINE
	if source is BaseMaterial3D:
		var base: BaseMaterial3D = source as BaseMaterial3D
		material.set_shader_parameter(&"albedo_texture", base.albedo_texture)
		material.set_shader_parameter(&"albedo_color", base.albedo_color)
		outline.set_shader_parameter(&"albedo_texture", base.albedo_texture)
	material.next_pass = outline
	return material


## The picture a weapon look carries (the can's skin, for one).
static func picture_of(material: Material) -> Texture2D:
	return (material as ShaderMaterial).get_shader_parameter(&"albedo_texture") as Texture2D
