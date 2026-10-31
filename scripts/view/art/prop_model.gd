class_name PropModel
extends RefCounted
## The street props that are real 3D models (assets/models/props/<id>.glb, made by
## tools/blender/build_prop_model.py from the owner's Meshy models): the electric post at each
## base (post_blue and post_red, the owner's own models, one mesh each), benches, the cardboard wall, the school-zone sign and the wooden A-frame.
## Every model has its base centre on the origin (x and z centred, y up from 0), the size it was
## modelled at, and the matte look from ModelLook. The map scales them to fit.

const PATH: String = "res://assets/models/props/%s.glb"
## Models drawn with the materials their maker authored (the karinderya's see-through glass, glowing parts and metal), not the matte look of `ModelLook`:
## the owner's own model, not to be changed or lowered in quality.
const AS_MADE: Array[String] = ["karinderya"]

static var _scenes: Dictionary[String, PackedScene] = {}
static var _boxes: Dictionary[String, AABB] = {}


## A new instance of prop `id`, scaled by `scale` (x width, y height, z depth of the model).
static func make(id: String, scale: Vector3 = Vector3.ONE) -> Node3D:
	if not _scenes.has(id):
		_scenes[id] = load(PATH % id) as PackedScene
	var root: Node3D = _scenes[id].instantiate() as Node3D
	if not AS_MADE.has(id):
		ModelLook.apply(root)
	root.scale = scale
	return root


## The size (x, y, z) the model was made at.
static func size_of(id: String) -> Vector3:
	return bounds_of(id).size


## The box round the model as it was made: its one mesh's, or (a model made of many parts, the karinderya) all of its parts'.
static func bounds_of(id: String) -> AABB:
	if not _boxes.has(id):
		if not _scenes.has(id):
			_scenes[id] = load(PATH % id) as PackedScene
		var probe: Node3D = _scenes[id].instantiate() as Node3D
		var meshes: Array[Node] = probe.find_children("*", "MeshInstance3D", true, false)
		_boxes[id] = (meshes[0] as MeshInstance3D).mesh.get_aabb() if meshes.size() == 1 else _bounds(probe, meshes)
		probe.free()
	return _boxes[id]


## The box round all `meshes` of `root`, in the root's own space.
static func _bounds(root: Node3D, meshes: Array[Node]) -> AABB:
	var box: AABB = AABB()
	var first: bool = true
	for node: Node in meshes:
		var mesh: MeshInstance3D = node as MeshInstance3D
		var placed: Transform3D = Transform3D.IDENTITY
		var at: Node = mesh
		while at != null and at != root:
			if at is Node3D:
				placed = (at as Node3D).transform * placed
			at = at.get_parent()
		var part: AABB = placed * mesh.mesh.get_aabb()
		box = part if first else box.merge(part)
		first = false
	return box


## Scale that makes the model `height` metres tall, the same in every direction.
static func by_height(id: String, height: float) -> Node3D:
	return make(id, Vector3.ONE * (height / size_of(id).y))


## Fits a footprint (x, z) in metres, the long side along X: the height keeps the model's look
## (the geometric mean of the two stretches) but never taller than `max_height`; with `exact` it is
## `max_height` (cover that has to read as cover, whatever the footprint's shape).
static func fit(id: String, footprint: Vector2, max_height: float, exact: bool = false) -> Node3D:
	var natural: Vector3 = size_of(id)
	var sx: float = footprint.x / natural.x
	var sz: float = footprint.y / natural.z
	var sy: float = max_height / natural.y if exact else minf(sqrt(sx * sz), max_height / natural.y)
	return make(id, Vector3(sx, sy, sz))
