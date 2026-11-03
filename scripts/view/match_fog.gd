class_name MatchFog
extends Node3D
## The fog beyond the street's two ends (owner, 2026-11-03): a wall of fog standing across each end just past its boundary row, in the 3D
## street (`shaders/match_fog_wall.gdshader`). Because it stands in the street it layers by where it stands: at the end far from the camera
## (the top of the screen) it is BEHIND the boundary pictures and props, which stay clear in front of it, with the fog thickening upward above
## them; at the end near the camera (the bottom of the screen) it is IN FRONT of them and covers them, but never rises into the play area. The shader works
## out which end each wall is at from the camera every frame, so it follows the view when the street swings half way round. It is white and
## nearly opaque where it is thickest (owner). View only.

const SHADER: Shader = preload("res://shaders/match_fog_wall.gdshader")
## How wide and tall a wall is (metres): wider than anything the camera sees, tall enough to reach the top of the screen.
const WIDTH: float = 140.0
const HEIGHT: float = 16.0

var walls: Array[MeshInstance3D] = []


func _init() -> void:
	name = "Fog"


## Stands a wall `distance` metres past the end of a street `length` metres long, at both ends (the end line plus the boundary row's setback
## plus how far the boundary pictures lean back: `StreetMap.fog_distance()`).
func build(length: float, past_the_line: float) -> void:
	for end: float in [-1.0, 1.0]:
		var quad: QuadMesh = QuadMesh.new()
		quad.size = Vector2(WIDTH, HEIGHT)
		var material: ShaderMaterial = ShaderMaterial.new()
		material.shader = SHADER
		# the near wall stands `past_the_line` metres past the end line: as high as that, the fog ends on the screen where the end line is, so it
		# never reaches into the play area
		material.set_shader_parameter("near_reach", past_the_line)
		material.set_shader_parameter("near_peak", past_the_line * 0.5)
		quad.material = material
		var wall: MeshInstance3D = MeshInstance3D.new()
		wall.name = "Wall%s" % ("Far" if end < 0.0 else "Near")
		wall.mesh = quad
		wall.position = Vector3(0.0, HEIGHT / 2.0, end * (length / 2.0 + past_the_line))
		wall.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(wall)
		walls.append(wall)
