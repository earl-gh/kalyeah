class_name PropModel
extends RefCounted
## The street props that are real 3D models (assets/models/props/<id>.glb, made by
## tools/blender/build_prop_model.py from the owner's Meshy models): the electric post at each
## base (post_blue and post_red, the owner's own models, one mesh each), benches, the cardboard wall, the school-zone sign and the wooden A-frame.
## Every model has its base centre on the origin (x and z centred, y up from 0), the size it was
## modelled at, and the matte look from ModelLook. The map scales them to fit.

const PATH: String = "res://assets/models/props/%s.glb"

## The models that are many parts as the owner made them (every other prop is one mesh and is measured by it).
const MULTI_PART: Array[String] = ["karinderya"]
## How far off the origin (x or z) a model's centre may be before it is put on a pivot that centres it.
const CENTRE_TOLERANCE: float = 0.05

static var _scenes: Dictionary[String, PackedScene] = {}
static var _sizes: Dictionary[String, Vector3] = {}
static var _centres: Dictionary[String, Vector3] = {}


## A new instance of prop `id`, scaled by `scale` (x width, y height, z depth of the model).
static func make(id: String, scale: Vector3 = Vector3.ONE) -> Node3D:
	if not _scenes.has(id):
		_scenes[id] = load(PATH % id) as PackedScene
	var root: Node3D = _scenes[id].instantiate() as Node3D
	ModelLook.apply(root)
	size_of(id)
	var centre: Vector3 = _centres[id]
	if absf(centre.x) > CENTRE_TOLERANCE or absf(centre.z) > CENTRE_TOLERANCE:
		# a model of many parts, as the owner built it, is not centred on its origin: stand it on a pivot that is
		var pivot: Node3D = Node3D.new()
		root.scale = scale
		root.position = Vector3(-centre.x * scale.x, 0.0, -centre.z * scale.z)
		pivot.add_child(root)
		return pivot
	root.scale = scale
	return root


## The size (x, y, z) the model was made at.
static func size_of(id: String) -> Vector3:
	if not _sizes.has(id):
		if not _scenes.has(id):
			_scenes[id] = load(PATH % id) as PackedScene
		var probe: Node3D = _scenes[id].instantiate() as Node3D
		var meshes: Array[Node] = probe.find_children("*", "MeshInstance3D", true, false)
		var box: AABB = (meshes[0] as MeshInstance3D).mesh.get_aabb()
		if MULTI_PART.has(id):
			# a model of many parts, kept as the owner built it: the box round all of them, where they stand
			box = _parts_box(probe, meshes)
		_sizes[id] = box.size
		_centres[id] = box.get_center() if MULTI_PART.has(id) else Vector3.ZERO
		probe.free()
	return _sizes[id]


static func _parts_box(root: Node3D, meshes: Array[Node]) -> AABB:
	var box: AABB = AABB()
	var first: bool = true
	for node: Node in meshes:
		var part: MeshInstance3D = node as MeshInstance3D
		var placed: Transform3D = Transform3D.IDENTITY
		var up: Node = part
		while up != null and up != root:
			placed = (up as Node3D).transform * placed
			up = up.get_parent()
		var part_box: AABB = placed * part.mesh.get_aabb()
		box = part_box if first else box.merge(part_box)
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
