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
## Blade angles (degrees) of `set_open`: as made, closed (the blades cross on the axis), open wide (a snip), wide open (the big
## snip's wind-up).
const OPEN_AUTHORED: float = 0.0
const CLOSED: float = -16.0
const WIDE: float = 14.0
const WIDE_HEAVY: float = 24.0
## The screw in model units (the blades turn about it), triangles this close to it stay on blade A (the screw on top), and
## the two blades sit this far apart along the face normal so closed blades never z-fight.
const PIVOT: Vector2 = Vector2(-0.005, 0.515)
const PIVOT_KEEP: float = 0.03
const BLADE_GAP: float = 0.008

static var _halves: Array[ArrayMesh] = []

var _pivot_a: Node3D
var _pivot_b: Node3D
var _angle: float = OPEN_AUTHORED


## A pair of scissors `length` metres long (middle on the origin, tips toward local +Y, flat face toward +Z).
static func make(length: float) -> ScissorsRig:
	var rig: ScissorsRig = ScissorsRig.new()
	var natural: Vector3 = WeaponModel.size_of(MODEL)
	var model: Node3D = Node3D.new()
	model.name = "Model"
	model.scale = Vector3.ONE * length / natural.y
	model.position = Vector3(0.0, -length / 2.0, 0.0)
	rig.add_child(model)
	var halves: Array[ArrayMesh] = halves()
	var look: Material = WeaponModel.look_of(MODEL)
	for i: int in 2:
		var pivot: Node3D = Node3D.new()
		pivot.name = "PivotA" if i == 0 else "PivotB"
		pivot.position = Vector3(PIVOT.x, PIVOT.y, BLADE_GAP if i == 0 else -BLADE_GAP)
		var blade: MeshInstance3D = MeshInstance3D.new()
		blade.name = "Blade"
		blade.mesh = halves[i]
		blade.position = Vector3(-PIVOT.x, -PIVOT.y, 0.0)
		blade.set_surface_override_material(0, look)
		blade.add_to_group(WeaponModel.GROUP)
		pivot.add_child(blade)
		model.add_child(pivot)
	rig._pivot_a = model.get_node("PivotA") as Node3D
	rig._pivot_b = model.get_node("PivotB") as Node3D
	return rig


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
			var copy: Array = arrays.duplicate()
			copy[Mesh.ARRAY_INDEX] = half
			var mesh: ArrayMesh = ArrayMesh.new()
			mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, copy)
			_halves.append(mesh)
	return _halves


## Triangles in `mesh` (tests: the split keeps every one).
static func triangles(mesh: Mesh) -> int:
	return int((mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3.0)
