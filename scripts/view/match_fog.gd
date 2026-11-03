class_name MatchFog
extends Node3D
## The fog beyond the street's two ends (owner, 2026-11-03): a wall of fog standing across each end past its boundary row, and a strip of ground
## beyond it, in the 3D street (`shaders/match_fog_wall.gdshader`). Because it stands in the street it layers by where it stands: at the end far
## from the camera (the top of the screen) it is BEHIND the boundary pictures and props, which stay clear in front of it, with the fog thickening
## upward above them; at the end near the camera (the bottom of the screen) it is IN FRONT of them and covers them, thickening toward the edge of the
## screen (the card tray), and never rises into the play area. The shader works out which end it is at from the camera every frame, so it follows
## the view when the street swings half way round. It is white and nearly opaque where it is thickest (owner). View only.

const SHADER: Shader = preload("res://shaders/match_fog_wall.gdshader")
## How wide and tall a wall is (metres): wider than anything the camera sees, tall enough to reach the top of the screen.
const WIDTH: float = 140.0
const HEIGHT: float = 16.0
## How deep the strip of ground beyond a wall is (m): all the ground the near end's camera sees beyond it.
const GROUND_DEPTH: float = 14.0
## How far (m, on the screen) the far end's fog takes to become thick.
const FAR_RAMP: float = 4.2

var walls: Array[MeshInstance3D] = []
var grounds: Array[MeshInstance3D] = []


func _init() -> void:
	name = "Fog"


## Stands a wall `distance` metres past the end line of a street `length` metres long, at both ends (past the boundary row's setback and as far as
## the boundary pictures lean back: `StreetMap.fog_distance()`), and a strip of ground beyond each. The far end's fog starts `far_start` metres
## beyond the end line on the screen (just above the boundary pictures: `StreetMap.fog_far_start()`).
func build(length: float, distance: float, far_start: float) -> void:
	for end: float in [-1.0, 1.0]:
		var wall_quad: QuadMesh = QuadMesh.new()
		wall_quad.size = Vector2(WIDTH, HEIGHT)
		wall_quad.material = _material(length, far_start, 0.0)
		var wall: MeshInstance3D = _mesh("Wall%s" % _end_name(end), wall_quad, Vector3(0.0, HEIGHT / 2.0, end * (length / 2.0 + distance)))
		walls.append(wall)
		var strip: PlaneMesh = PlaneMesh.new()
		strip.size = Vector2(WIDTH, GROUND_DEPTH)
		strip.material = _material(length, far_start, 1.0)
		grounds.append(_mesh("Ground%s" % _end_name(end), strip, Vector3(0.0, 0.03, end * (length / 2.0 + distance + GROUND_DEPTH / 2.0))))


func _end_name(end: float) -> String:
	return "Far" if end < 0.0 else "Near"


func _material(length: float, far_start: float, ground: float) -> ShaderMaterial:
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = SHADER
	material.set_shader_parameter("lane_half_length", length / 2.0)
	material.set_shader_parameter("far_start", far_start)
	material.set_shader_parameter("far_full", far_start + FAR_RAMP)
	material.set_shader_parameter("is_ground", ground)
	return material


func _mesh(mesh_name: String, mesh: Mesh, at: Vector3) -> MeshInstance3D:
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.name = mesh_name
	instance.mesh = mesh
	instance.position = at
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(instance)
	return instance
