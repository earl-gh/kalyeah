class_name PropModel
extends RefCounted
## The street props that are real 3D models (assets/models/props/<id>.glb, made by
## tools/blender/build_prop_model.py from the owner's Meshy models): the electric post at each
## base (post_blue and post_red, the owner's own models, one mesh each), benches, the cardboard wall, the school-zone sign and the wooden A-frame.
## Every model has its base centre on the origin (x and z centred, y up from 0), the size it was
## modelled at, and the matte look from ModelLook. The map scales them to fit.

const PATH: String = "res://assets/models/props/%s.glb"

static var _scenes: Dictionary[String, PackedScene] = {}
static var _sizes: Dictionary[String, Vector3] = {}


## A new instance of prop `id`, scaled by `scale` (x width, y height, z depth of the model).
static func make(id: String, scale: Vector3 = Vector3.ONE) -> Node3D:
	if not _scenes.has(id):
		_scenes[id] = load(PATH % id) as PackedScene
	var root: Node3D = _scenes[id].instantiate() as Node3D
	ModelLook.apply(root)
	root.scale = scale
	return root


## The size (x, y, z) the model was made at.
static func size_of(id: String) -> Vector3:
	if not _sizes.has(id):
		if not _scenes.has(id):
			_scenes[id] = load(PATH % id) as PackedScene
		var probe: Node3D = _scenes[id].instantiate() as Node3D
		var mesh: MeshInstance3D = probe.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		_sizes[id] = mesh.mesh.get_aabb().size
		probe.free()
	return _sizes[id]


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
