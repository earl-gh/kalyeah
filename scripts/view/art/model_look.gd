class_name ModelLook
extends RefCounted
## The soft, matte look every 3D model (props, kids, weapons) gets, and the only part of the picture that the scene's lights shape (the floor, the zone
## marks are unshaded; docs/ART.md "Light and colour by asset class"): no gloss at all (no specular, no
## normal map), light wrapping round the body so the shaded side stays soft, and a faint rim so the outline lifts off the street.
## The lights are tuned (StreetMap.SUN_ENERGY and the ambient) so a model's colour comes out at its own colour: the albedo gain is 1.

const ALBEDO_GAIN: float = 1.0
const RIM: float = 0.1
const RIM_TINT: float = 0.3


## Gives every surface of every mesh under `root` its own copy of the look and returns them
## (so a tint can be applied per kid).
static func apply(root: Node) -> Array[StandardMaterial3D]:
	var made: Array[StandardMaterial3D] = []
	for node: Node in root.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		for surface: int in mesh_instance.mesh.get_surface_count():
			var material: StandardMaterial3D = make(mesh_instance.get_active_material(surface))
			mesh_instance.set_surface_override_material(surface, material)
			made.append(material)
	return made


## The look for one surface whose own material is `source`: its picture and colour kept, everything else the model look.
static func make(source: Material) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	if source is BaseMaterial3D:
		var base: BaseMaterial3D = source as BaseMaterial3D
		material.albedo_texture = base.albedo_texture
		material.albedo_color = base.albedo_color
	material.metallic = 0.0
	material.roughness = 1.0
	material.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	material.normal_enabled = false
	material.diffuse_mode = BaseMaterial3D.DIFFUSE_LAMBERT_WRAP
	material.rim_enabled = true
	material.rim = RIM
	material.rim_tint = RIM_TINT
	material.albedo_color = Color(ALBEDO_GAIN, ALBEDO_GAIN, ALBEDO_GAIN) * material.albedo_color
	return material


## Turns on the model's own baked shading (the vertex colours its maker painted: darker in the creases and under the parts, 0.6 to 1) for
## every surface under `root` that already has the look. Without it a light model (the posts) is flat and blown out to white; with it the
## scene's lights shape a form that already has its soft shadows. Base colour, no gloss and the wrap light stay as they are.
static func use_baked_shading(root: Node) -> void:
	for node: Node in root.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance: MeshInstance3D = node as MeshInstance3D
		for surface: int in mesh_instance.mesh.get_surface_count():
			var material: StandardMaterial3D = mesh_instance.get_surface_override_material(surface) as StandardMaterial3D
			if material != null:
				material.vertex_color_use_as_albedo = true


## The colour a tint multiplies the lit albedo by.
static func tinted(tint: Color) -> Color:
	return Color(ALBEDO_GAIN * tint.r, ALBEDO_GAIN * tint.g, ALBEDO_GAIN * tint.b)
