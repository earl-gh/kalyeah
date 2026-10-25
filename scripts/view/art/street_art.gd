class_name StreetArt
extends RefCounted
## The painted textures of the street (assets/textures/, made by
## tools/art/make_assets.py): concrete, asphalt and paver ground, chalk games, the
## sari-sari store sign. View only.

const TEXTURE_PATH: String = "res://assets/textures/%s.png"

static var _textures: Dictionary[String, Texture2D] = {}


static func texture(name: String) -> Texture2D:
	if not _textures.has(name):
		_textures[name] = load(TEXTURE_PATH % name) as Texture2D
	return _textures[name]


## Matte, lit material showing `name`, repeated `repeat` times across the surface.
static func material(name: String, repeat: Vector2 = Vector2.ONE, transparent: bool = false) -> StandardMaterial3D:
	return material_for(texture(name), repeat, transparent)


## The same matte material for any texture (for example an owner-made image loaded by path).
static func material_for(art: Texture2D, repeat: Vector2 = Vector2.ONE, transparent: bool = false) -> StandardMaterial3D:
	var result: StandardMaterial3D = StandardMaterial3D.new()
	result.albedo_texture = art
	result.uv1_scale = Vector3(repeat.x, repeat.y, 1.0)
	result.roughness = 1.0
	result.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
	result.diffuse_mode = BaseMaterial3D.DIFFUSE_LAMBERT_WRAP
	result.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	if transparent:
		result.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	return result


## An unshaded material showing `art` exactly as drawn (no light, no tint): what the owner's floor is made with, so the floor is the
## picture itself.
static func unlit_material(art: Texture2D, repeat: Vector2 = Vector2.ONE) -> StandardMaterial3D:
	var result: StandardMaterial3D = StandardMaterial3D.new()
	result.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	result.albedo_texture = art
	result.uv1_scale = Vector3(repeat.x, repeat.y, 1.0)
	result.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	return result


## A flat textured patch of ground, `size` metres (x, z), one texture repeat per `tile` m.
static func ground(name: String, size: Vector2, tile: float, tint: Color = Color.WHITE) -> MeshInstance3D:
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = size
	var mat: StandardMaterial3D = material(name, size / tile)
	mat.albedo_color = tint
	plane.material = mat
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.mesh = plane
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return instance


## Paint or chalk on the ground (transparent), `size` metres (x, z); text reads from +Z.
static func decal(name: String, size: Vector2) -> MeshInstance3D:
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = size
	plane.material = material(name, Vector2.ONE, true)
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.mesh = plane
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return instance

