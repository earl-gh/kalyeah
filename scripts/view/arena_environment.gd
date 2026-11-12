class_name ArenaEnvironment
extends RefCounted
## Deadlock-inspired golden-afternoon Philippine Barangay daylight & atmosphere helper.
## Pure view configuration: warm directional afternoon sun, cool cerulean sky fill,
## balanced sky-bounce ambient, ACES tonemapping, and soft atmospheric depth haze.

const SKY_SHADER: Shader = preload("res://shaders/tropical_sky.gdshader")

## Warm golden afternoon sun from the South-East sky (`SUN_ENERGY * sin(48 deg) + AMBIENT_ENERGY = 1.02`).
const SUN_ANGLES: Vector3 = Vector3(-48.0, 38.0, 0.0)
const SUN_COLOR: Color = Color(1.0, 0.91, 0.78)
const SUN_ENERGY: float = 0.78
const SHADOW_DISTANCE: float = 135.0
const SHADOW_OPACITY: float = 0.66

## Cool cerulean-lavender sky fill from the opposing North-West hemisphere to sculpt shadow planes.
const FILL_ANGLES: Vector3 = Vector3(-34.0, -142.0, 0.0)
const FILL_COLOR: Color = Color(0.66, 0.79, 0.95)
const FILL_ENERGY: float = 0.25

## Neutral-warm sky bounce ambient keeping recessed porches, alleys, and capiz eaves readable.
const AMBIENT_COLOR: Color = Color(0.86, 0.86, 0.89)
const AMBIENT_ENERGY: float = 0.44

## Soft afternoon provincial horizon haze matching `tropical_sky.gdshader`.
const FOG_LIGHT_COLOR: Color = Color(0.93, 0.85, 0.73)
const FOG_LIGHT_ENERGY: float = 0.26
const FOG_DENSITY: float = 0.00078
const FOG_SKY_AFFECT: float = 0.12


static func make_environment() -> Environment:
	var env: Environment = Environment.new()
	var sky_mat: ShaderMaterial = ShaderMaterial.new()
	sky_mat.shader = SKY_SHADER
	var sky: Sky = Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = AMBIENT_COLOR
	env.ambient_light_energy = AMBIENT_ENERGY
	env.adjustment_enabled = false
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.0
	env.glow_enabled = false
	env.fog_enabled = true
	env.fog_light_color = FOG_LIGHT_COLOR
	env.fog_light_energy = FOG_LIGHT_ENERGY
	env.fog_density = FOG_DENSITY
	env.fog_sky_affect = FOG_SKY_AFFECT
	return env


static func light_scene(parent: Node, sun: DirectionalLight3D) -> void:
	sun.rotation_degrees = SUN_ANGLES
	sun.light_color = SUN_COLOR
	sun.light_energy = SUN_ENERGY
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	sun.directional_shadow_split_1 = 0.22
	sun.directional_shadow_blend_splits = true
	sun.directional_shadow_max_distance = SHADOW_DISTANCE
	sun.shadow_opacity = SHADOW_OPACITY
	sun.shadow_blur = 1.35
	sun.shadow_bias = 0.062
	sun.shadow_normal_bias = 2.15
	var existing_fill: DirectionalLight3D = parent.get_node_or_null("SkyFill") as DirectionalLight3D
	if existing_fill == null:
		var fill: DirectionalLight3D = DirectionalLight3D.new()
		fill.name = "SkyFill"
		fill.rotation_degrees = FILL_ANGLES
		fill.light_color = FILL_COLOR
		fill.light_energy = FILL_ENERGY
		fill.shadow_enabled = false
		parent.add_child(fill)
	else:
		existing_fill.rotation_degrees = FILL_ANGLES
		existing_fill.light_color = FILL_COLOR
		existing_fill.light_energy = FILL_ENERGY
		existing_fill.shadow_enabled = false
