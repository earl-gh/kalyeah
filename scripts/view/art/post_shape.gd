class_name PostShape
extends RefCounted
## The electric post drawn long and slim (owner, 2026-11-09; `MapLayout.post_height`, `post_width_share`) without stretching what is on it.
## The post node is scaled `share` times narrower across than tall; that suits the concrete pole, but the parts on it (the crossarm with its
## braces and insulators, the transformer with its brackets, the router box with its straps, the flyers) would look stretched upward. So the
## mesh is remade once per model and share: the pole's surfaces stay as they are, and every other part is squeezed upward by the same `share`
## in the model's own units, so the node's scale gives it back its own proportions. Parts are found as assemblies: pieces of a surface that
## share corners or triangles, joined to any other piece whose box touches theirs (`MERGE_GAP`), so a bracket stays on what it holds. Each
## assembly keeps its middle where it was on the pole; one that reaches the pole's top keeps that top instead, so the crossarm stays flush with
## it. The pole is an even column, so the others stay against it. The owner's model file is not changed.

## The pole's own surfaces (by their materials' names): the painted concrete and the team-coloured cap on its top.
const POLE_SURFACES: Array[String] = ["painted_concrete", "team_paint"]
## Pieces whose boxes are this close (model metres) are one assembly.
const MERGE_GAP: float = 0.03
## Corners this close (model metres) are the same corner (a mesh repeats a corner where its UVs or normals split).
const SAME_CORNER: float = 0.0001

## The reshaped meshes, by the source mesh's id and the share.
static var _made: Dictionary[String, ArrayMesh] = {}


## Gives every mesh under `model` its reshaped copy for `share` (1: left as made).
static func apply(model: Node3D, share: float) -> void:
	if is_equal_approx(share, 1.0):
		return
	for node: Node in model.find_children("*", "MeshInstance3D", true, false):
		var instance: MeshInstance3D = node as MeshInstance3D
		if instance.mesh is ArrayMesh:
			instance.mesh = reshaped(instance.mesh as ArrayMesh, share)


## `mesh` with every part but the pole squeezed upward by `share` (made once, then shared).
static func reshaped(mesh: ArrayMesh, share: float) -> ArrayMesh:
	var key: String = "%d:%.4f" % [mesh.get_instance_id(), share]
	if not _made.has(key):
		_made[key] = _reshape(mesh, share)
	return _made[key]


## True when surface `index` of `mesh` is the pole itself.
static func is_pole(mesh: Mesh, index: int) -> bool:
	var material: Material = mesh.surface_get_material(index)
	var name: String = material.resource_name if material != null else ""
	for ending: String in POLE_SURFACES:
		if name.ends_with(ending):
			return true
	return false


static func _reshape(mesh: ArrayMesh, share: float) -> ArrayMesh:
	var arrays: Array[Array] = []
	var pole_top: float = 0.0
	for surface: int in mesh.get_surface_count():
		arrays.append(mesh.surface_get_arrays(surface))
		if is_pole(mesh, surface):
			for corner: Vector3 in arrays[surface][Mesh.ARRAY_VERTEX] as PackedVector3Array:
				pole_top = maxf(pole_top, corner.y)
	# the pieces: [surface, the indices of its corners, their box]
	var pieces: Array[Array] = []
	for surface: int in arrays.size():
		if not is_pole(mesh, surface):
			pieces.append_array(_pieces(surface, arrays[surface]))
	var assemblies: Array[Array] = _assemblies(pieces)
	for assembly: Array in assemblies:
		var box: AABB = assembly[0] as AABB
		var pivot: float = pole_top if box.end.y >= pole_top else box.get_center().y
		for piece: Array in assembly[1] as Array:
			var surface: int = piece[0] as int
			var corners: PackedVector3Array = arrays[surface][Mesh.ARRAY_VERTEX] as PackedVector3Array
			var has_normals: bool = arrays[surface][Mesh.ARRAY_NORMAL] != null
			var normals: PackedVector3Array = arrays[surface][Mesh.ARRAY_NORMAL] as PackedVector3Array if has_normals else PackedVector3Array()
			for index: int in piece[1] as Array:
				var corner: Vector3 = corners[index]
				corners[index] = Vector3(corner.x, pivot + (corner.y - pivot) * share, corner.z)
				if has_normals:
					# the node is `share` narrower across than tall: a normal of the squeezed part must lean the other way to come out true
					var normal: Vector3 = normals[index]
					normals[index] = Vector3(normal.x * share, normal.y, normal.z * share).normalized()
			arrays[surface][Mesh.ARRAY_VERTEX] = corners
			if has_normals:
				arrays[surface][Mesh.ARRAY_NORMAL] = normals
	var made: ArrayMesh = ArrayMesh.new()
	for surface: int in arrays.size():
		made.add_surface_from_arrays(mesh.surface_get_primitive_type(surface), arrays[surface])
		made.surface_set_material(surface, mesh.surface_get_material(surface))
		made.surface_set_name(surface, mesh.surface_get_name(surface))
	return made


## The pieces of one surface: corners joined where they are the same point or share a triangle. Each is [surface, its corners, their box].
static func _pieces(surface: int, arrays: Array) -> Array[Array]:
	var corners: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var triangles: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] as PackedInt32Array if arrays[Mesh.ARRAY_INDEX] != null else PackedInt32Array()
	var parent: PackedInt32Array = PackedInt32Array()
	parent.resize(corners.size())
	for index: int in corners.size():
		parent[index] = index
	var seen: Dictionary[Vector3i, int] = {}
	for index: int in corners.size():
		var key: Vector3i = Vector3i((corners[index] / SAME_CORNER).round())
		if seen.has(key):
			_join(parent, index, seen[key])
		else:
			seen[key] = index
	for first: int in range(0, triangles.size(), 3):
		_join(parent, triangles[first], triangles[first + 1])
		_join(parent, triangles[first], triangles[first + 2])
	var groups: Dictionary[int, Array] = {}
	for index: int in corners.size():
		var root: int = _root(parent, index)
		if not groups.has(root):
			groups[root] = [surface, [], AABB(corners[index], Vector3.ZERO)]
		var group: Array = groups[root]
		(group[1] as Array).append(index)
		group[2] = (group[2] as AABB).expand(corners[index])
	var found: Array[Array] = []
	found.assign(groups.values())
	return found


## The pieces joined into assemblies where their boxes touch (`MERGE_GAP`). Each is [its box, its pieces].
static func _assemblies(pieces: Array[Array]) -> Array[Array]:
	var parent: PackedInt32Array = PackedInt32Array()
	parent.resize(pieces.size())
	for index: int in pieces.size():
		parent[index] = index
	for first: int in pieces.size():
		for second: int in range(first + 1, pieces.size()):
			if (pieces[first][2] as AABB).grow(MERGE_GAP).intersects(pieces[second][2] as AABB):
				_join(parent, first, second)
	var groups: Dictionary[int, Array] = {}
	for index: int in pieces.size():
		var root: int = _root(parent, index)
		if not groups.has(root):
			groups[root] = [pieces[index][2], []]
		var group: Array = groups[root]
		group[0] = (group[0] as AABB).merge(pieces[index][2] as AABB)
		(group[1] as Array).append(pieces[index])
	var found: Array[Array] = []
	found.assign(groups.values())
	return found


static func _root(parent: PackedInt32Array, index: int) -> int:
	var at: int = index
	while parent[at] != at:
		parent[at] = parent[parent[at]]
		at = parent[at]
	return at


static func _join(parent: PackedInt32Array, first: int, second: int) -> void:
	parent[_root(parent, first)] = _root(parent, second)
