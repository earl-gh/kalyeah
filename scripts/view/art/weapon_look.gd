class_name WeaponLook
extends RefCounted
## The look of the thrown and placed weapon models, replacing `ModelLook` for them (owner: nearest to Clash Royale's painted texture): a
## painted light that lifts the top faces and turns the shadows deep and cool (`weapon_look.gdshader`), and a contour in a deep tone of the
## picture's own colour (`weapon_outline.gdshader`), so each object reads against the street. Still no gloss at all. The kids and the
## props keep `ModelLook` / the owner's own material.

const LOOK: Shader = preload("res://scripts/view/art/weapon_look.gdshader")
const OUTLINE: Shader = preload("res://scripts/view/art/weapon_outline.gdshader")
## The near-white models keep a little under white (the paper walls, the crumpled paper, the slipper), so their lit top is cream and
## never clips to flat white; their top is not lifted.
const WHITE_GAIN: Color = Color(0.93, 0.93, 0.95)
const WHITE_TOP: float = 1.0
## The bare wood of the trumpo keeps most of its red in the shade and the same blue as its green, so its shadow is deep warm brown and not
## lavender (the default tint is violet, right for paint and tin, wrong for pale wood).
const WOOD_SHADOW: Vector3 = Vector3(0.74, 0.62, 0.62)
## Per-model looks: models whose picture is noisy and photographic (the AI-made rock and soil) are drawn flat: blurred this many mip levels, in this
## many value steps, with this much colour. The picture itself is not touched.
const STYLES: Dictionary[String, Dictionary] = {
	"meteor": {"flatten": 2.0, "steps": 5.0, "colour_gain": 1.15},
	"soil_block": {"flatten": 2.5, "steps": 4.0, "colour_gain": 1.1},
	"slipper": {"albedo_color": WHITE_GAIN, "top_boost": WHITE_TOP},
	"paper": {"albedo_color": WHITE_GAIN, "top_boost": WHITE_TOP},
	"trumpo": {"shadow_tint": WOOD_SHADOW},
	"crumpled_paper": {"albedo_color": WHITE_GAIN, "top_boost": WHITE_TOP},
}


## Gives every surface of every mesh under `root` the weapon look of model `id` (its picture kept) and returns the materials, one per surface.
static func apply(root: Node, id: String = "") -> Array[Material]:
	var made: Array[Material] = []
	for node: Node in root.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		for surface: int in mesh_instance.mesh.get_surface_count():
			var material: ShaderMaterial = make(mesh_instance.get_active_material(surface), STYLES.get(id, {}) as Dictionary)
			mesh_instance.set_surface_override_material(surface, material)
			made.append(material)
	return made


## The look for a surface whose own material is `source`: its picture and colour carried over, with the contour as the second pass.
## `style` sets any of the shader's uniforms (see `STYLES`).
static func make(source: Material, style: Dictionary = {}) -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = LOOK
	var outline: ShaderMaterial = ShaderMaterial.new()
	outline.shader = OUTLINE
	if source is BaseMaterial3D:
		var base: BaseMaterial3D = source as BaseMaterial3D
		material.set_shader_parameter(&"albedo_texture", base.albedo_texture)
		material.set_shader_parameter(&"albedo_color", base.albedo_color)
		outline.set_shader_parameter(&"albedo_texture", base.albedo_texture)
	for key: Variant in style:
		material.set_shader_parameter(key as StringName, style[key])
	material.next_pass = outline
	return material


## The picture a weapon look carries (the can's skin, for one).
static func picture_of(material: Material) -> Texture2D:
	return (material as ShaderMaterial).get_shader_parameter(&"albedo_texture") as Texture2D
