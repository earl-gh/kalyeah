class_name ScissorsRig
extends Node3D
## The owner's scissors (assets/models/weapons/scissor.glb, one mesh) as two blades that open and close (the snip of the
## gunting cards). The single mesh is split once, at first use, into two halves by triangle centre: blade A is the upper-left
## blade with the lower-right handle (and the screw), blade B the rest. Each half turns about the screw on the model's flat
## face normal; `set_open` turns A by +deg and B by -deg (0 = the model as made, slightly open). No new asset: a real two-
## blade model (docs/art-briefs advisory "scissors with separate blades") would snip without the split's seam at the screw.
## Model frame: length along +Y from the handles (y 0) to the tips (y 1), flat face toward +Z. The rig's origin is the
## middle of the scissors.

const MODEL: String = "scissor"
## Blade angles (degrees) of `set_open`: as made, closed (both tips on the axis: as made they stand `TIP_ANGLE` off it),
## open wide (a snip: a V a quarter of a kid wide on screen), wide open (the big snip's wind-up).
const OPEN_AUTHORED: float = 0.0
const CLOSED: float = -13.2
const WIDE: float = 24.0
const WIDE_HEAVY: float = 30.0
## As made, each blade's tip stands this many degrees off the scissors' axis, this far (of the length) from the screw.
const TIP_ANGLE: float = 13.2
const TIP_ARM: float = 0.5
## A thin outline in the thrower's team colour round both blades (shaders/team_outline.gdshader, like the kids'), so dark
## blades read over hair and say who snipped; this many metres wide.
const OUTLINE_SHADER: Shader = preload("res://shaders/team_outline.gdshader")
const OUTLINE_WIDTH: float = 0.03
## The screw in model units (the blades turn about it), triangles this close to it stay on blade A (the screw on top), and
## the two blades sit this far apart along the face normal so closed blades never z-fight.
const PIVOT: Vector2 = Vector2(-0.005, 0.515)
const PIVOT_KEEP: float = 0.03
const BLADE_GAP: float = 0.008

static var _halves: Array[ArrayMesh] = []
## The blades' look marking the stencil (so the outline draws only round them), and one outline material per team colour.
static var _marked_look: BaseMaterial3D
static var _outlines: Dictionary[String, ShaderMaterial] = {}

var _pivot_a: Node3D
var _pivot_b: Node3D
var _angle: float = OPEN_AUTHORED


## A pair of scissors `length` metres long (middle on the origin, tips toward local +Y, flat face toward +Z), outlined in
## `team` (no outline when transparent).
static func make(length: float, team: Color = Color.TRANSPARENT) -> ScissorsRig:
	var rig: ScissorsRig = ScissorsRig.new()
	var natural: Vector3 = WeaponModel.size_of(MODEL)
	var model: Node3D = Node3D.new()
	model.name = "Model"
	model.scale = Vector3.ONE * length / natural.y
	model.position = Vector3(0.0, -length / 2.0, 0.0)
	rig.add_child(model)
	var halves: Array[ArrayMesh] = halves()
	var look: Material = WeaponModel.look_of(MODEL) if team.a <= 0.0 else _marked()
	var outline: ShaderMaterial = _outline(team) if team.a > 0.0 else null
	for i: int in 2:
		var pivot: Node3D = Node3D.new()
		pivot.name = "PivotA" if i == 0 else "PivotB"
		pivot.position = Vector3(PIVOT.x, PIVOT.y, BLADE_GAP if i == 0 else -BLADE_GAP)
		var blade: MeshInstance3D = MeshInstance3D.new()
		blade.name = "Blade"
		blade.mesh = halves[i]
		blade.position = Vector3(-PIVOT.x, -PIVOT.y, 0.0)
		blade.set_surface_override_material(0, look)
		blade.material_overlay = outline
		blade.add_to_group(WeaponModel.GROUP)
		pivot.add_child(blade)
		model.add_child(pivot)
	rig._pivot_a = model.get_node("PivotA") as Node3D
	rig._pivot_b = model.get_node("PivotB") as Node3D
	return rig


## Blade meshes and materials cached right now (tests).
static func cached_count() -> int:
	return _halves.size() + _outlines.size() + (1 if _marked_look != null else 0)


## Empties the caches (the last effect view left the tree).
static func clear_cache() -> void:
	_halves.clear()
	_outlines.clear()
	_marked_look = null


## The blades' look, marking the stencil like a kid's (built once).
static func _marked() -> BaseMaterial3D:
	if _marked_look == null:
		_marked_look = WeaponModel.look_of(MODEL).duplicate() as BaseMaterial3D
		_marked_look.stencil_mode = BaseMaterial3D.STENCIL_MODE_CUSTOM
		_marked_look.stencil_flags = BaseMaterial3D.STENCIL_FLAG_WRITE
		_marked_look.stencil_compare = BaseMaterial3D.STENCIL_COMPARE_ALWAYS
		_marked_look.stencil_reference = KidModel.STENCIL
	return _marked_look


## The outline material for `team` (one per colour, built once).
static func _outline(team: Color) -> ShaderMaterial:
	var key: String = team.to_html()
	if not _outlines.has(key):
		var outline: ShaderMaterial = ShaderMaterial.new()
		outline.shader = OUTLINE_SHADER
		outline.set_shader_parameter("color", Color(team, 1.0))
		outline.set_shader_parameter("width", OUTLINE_WIDTH)
		_outlines[key] = outline
	return _outlines[key]


## Opens the blades to `degrees` (each blade turns this much about the screw, in opposite directions).
func set_open(degrees: float) -> void:
	_angle = degrees
	_pivot_a.rotation = Vector3(0.0, 0.0, deg_to_rad(degrees))
	_pivot_b.rotation = Vector3(0.0, 0.0, -deg_to_rad(degrees))


## The blade angle last set.
func angle() -> float:
	return _angle


## The two blade nodes (tests).
func blades() -> Array[MeshInstance3D]:
	return [_pivot_a.get_node("Blade") as MeshInstance3D, _pivot_b.get_node("Blade") as MeshInstance3D]


## The model's mesh split into [blade A, blade B] (built once, shared by every pair of scissors).
static func halves() -> Array[ArrayMesh]:
	if _halves.is_empty():
		var source: Mesh = WeaponModel.mesh_of(MODEL)
		var arrays: Array = source.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] as PackedInt32Array
		var a: PackedInt32Array = PackedInt32Array()
		var b: PackedInt32Array = PackedInt32Array()
		for t: int in range(0, indices.size(), 3):
			var corners: PackedInt32Array = indices.slice(t, t + 3)
			var centre: Vector3 = (vertices[corners[0]] + vertices[corners[1]] + vertices[corners[2]]) / 3.0
			var offset: Vector2 = Vector2(centre.x, centre.y) - PIVOT
			if offset.x * offset.y < 0.0 or offset.length() < PIVOT_KEEP:
				a.append_array(corners)
			else:
				b.append_array(corners)
		for half: PackedInt32Array in [a, b]:
			_halves.append(_compact(arrays, half))
	return _halves


## A mesh of only the vertices `indices` uses (each blade keeps its own vertices, not the whole model's).
static func _compact(arrays: Array, indices: PackedInt32Array) -> ArrayMesh:
	var remap: Dictionary[int, int] = {}
	var used: PackedInt32Array = PackedInt32Array()
	var local: PackedInt32Array = PackedInt32Array()
	for index: int in indices:
		if not remap.has(index):
			remap[index] = used.size()
			used.append(index)
		local.append(remap[index])
	var copy: Array = []
	copy.resize(Mesh.ARRAY_MAX)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL] as PackedVector3Array
	var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV] as PackedVector2Array
	var tangents: PackedFloat32Array = arrays[Mesh.ARRAY_TANGENT] as PackedFloat32Array
	var new_vertices: PackedVector3Array = PackedVector3Array()
	var new_normals: PackedVector3Array = PackedVector3Array()
	var new_uvs: PackedVector2Array = PackedVector2Array()
	var new_tangents: PackedFloat32Array = PackedFloat32Array()
	for index: int in used:
		new_vertices.append(vertices[index])
		if not normals.is_empty():
			new_normals.append(normals[index])
		if not uvs.is_empty():
			new_uvs.append(uvs[index])
		if not tangents.is_empty():
			new_tangents.append_array(tangents.slice(index * 4, index * 4 + 4))
	copy[Mesh.ARRAY_VERTEX] = new_vertices
	if not new_normals.is_empty():
		copy[Mesh.ARRAY_NORMAL] = new_normals
	if not new_uvs.is_empty():
		copy[Mesh.ARRAY_TEX_UV] = new_uvs
	if not new_tangents.is_empty():
		copy[Mesh.ARRAY_TANGENT] = new_tangents
	copy[Mesh.ARRAY_INDEX] = local
	var mesh: ArrayMesh = ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, copy)
	return mesh


## Triangles in `mesh` (tests: the split keeps every one).
static func triangles(mesh: Mesh) -> int:
	return int((mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3.0)
