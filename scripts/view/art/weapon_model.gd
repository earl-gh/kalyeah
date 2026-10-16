class_name WeaponModel
extends RefCounted
## The thrown and placed skill objects that are real 3D models (assets/models/weapons/<id>.glb,
## made by tools/blender/build_prop_model.py from the owner's models): the slipper (tsinelas),
## the can (lata), the marble (bola), the crumpled paper (papel trap), the paper sheet (papel
## shield), the spinning top (trumpo), the spikes (jacks), the meteor (bato heavy), the scissors
## (gunting), the soil block (langit lupa wall) and the green cross (heal). Each has its base
## centre on the origin and the matte look from ModelLook.
## Weapons without a model here keep their code-drawn object (Props.weapon_object).

const PATH: String = "res://assets/models/weapons/%s.glb"
## Weapon id -> [model, longest side in metres]. The slippers keep the old boxes' length.
const OBJECTS: Dictionary[StringName, Array] = {
	&"tsinelas_light": ["slipper", 0.8],
	&"tsinelas_heavy": ["slipper", 1.5],
	&"lata": ["can", 0.7],
	&"bola": ["marble", 0.8],
	&"papel_trap": ["crumpled_paper", 0.7],
	&"trumpo": ["trumpo", 0.9],
}
## The spikes of a jacks field: this many clusters (one in the middle, the rest in a ring), each
## this wide in metres.
const SPIKE_CLUSTERS: int = 5
const SPIKE_WIDTH: float = 0.9
const METEOR_SIZE: float = 1.8
const CROSS_WIDTH: float = 0.9
## The paper wall's model.
const SHIELD: String = "paper"
const GAIN: float = 0.85

static var _scenes: Dictionary[String, PackedScene] = {}
static var _sizes: Dictionary[String, Vector3] = {}


static func has_model(weapon_id: StringName) -> bool:
	return OBJECTS.has(weapon_id)


## The thrown / placed object of `weapon_id`, its middle on the origin when `centered` (a
## projectile in flight) or standing on the ground (a placed trap).
static func make(weapon_id: StringName, centered: bool) -> Node3D:
	var spec: Array = OBJECTS[weapon_id]
	var id: String = spec[0] as String
	var natural: Vector3 = size_of(id)
	var scale: float = (spec[1] as float) / maxf(natural.x, maxf(natural.y, natural.z))
	var holder: Node3D = Node3D.new()
	var model: Node3D = _instance(id)
	model.scale = Vector3.ONE * scale
	if centered:
		model.position.y = -natural.y * scale / 2.0
	holder.add_child(model)
	return holder


## The paper wall: `width` metres wide, `height` tall and `thickness` deep (the model is a thin
## sheet standing up, so it is stretched to the wall's size).
static func shield(width: float, height: float, thickness: float) -> Node3D:
	return stretched(SHIELD, width, height, thickness)


## The block of earth of the langit lupa wall, stretched to the wall's size (base on the ground).
static func soil(width: float, height: float, depth: float) -> Node3D:
	return stretched("soil_block", width, height, depth)


## Model `id` stretched to exactly width x height x depth metres (base on the ground).
static func stretched(id: String, width: float, height: float, depth: float) -> Node3D:
	var natural: Vector3 = size_of(id)
	var holder: Node3D = Node3D.new()
	var model: Node3D = _instance(id)
	model.scale = Vector3(width / natural.x, height / natural.y, depth / natural.z)
	holder.add_child(model)
	return holder


## A jacks field: spike clusters scattered over a circle of `radius` (the middle one and a ring).
static func spikes(radius: float) -> Node3D:
	var holder: Node3D = Node3D.new()
	var natural: Vector3 = size_of("spikes")
	var scale: float = SPIKE_WIDTH / natural.x
	for i: int in SPIKE_CLUSTERS:
		var model: Node3D = _instance("spikes")
		model.scale = Vector3.ONE * scale
		if i > 0:
			var angle: float = TAU * float(i - 1) / float(SPIKE_CLUSTERS - 1) + 0.4
			model.position = Vector3(cos(angle), 0.0, sin(angle)) * radius * 0.6
		model.rotation.y = float(i) * 1.7
		holder.add_child(model)
	return holder


## The meteor that falls on a rock-meteor landing circle (middle on the origin).
static func meteor() -> Node3D:
	return _scaled("meteor", METEOR_SIZE, true)


## The green cross over a healed kid (middle on the origin).
static func cross() -> Node3D:
	return _scaled("green_cross", CROSS_WIDTH, true)


## A pair of scissors `length` metres long, lying flat with its tips along -Z (middle on the origin).
static func scissors(length: float) -> Node3D:
	var holder: Node3D = Node3D.new()
	var natural: Vector3 = size_of("scissor")
	var model: Node3D = _instance("scissor")
	var scale: float = length / natural.y
	model.scale = Vector3.ONE * scale
	# the model stands up (length along Y, flat face toward +Z): lay it down on the street
	model.rotation.x = -PI / 2.0
	model.position = Vector3(0.0, natural.z * scale / 2.0, natural.y * scale / 2.0)
	holder.add_child(model)
	return holder


static func _scaled(id: String, longest: float, centered: bool) -> Node3D:
	var natural: Vector3 = size_of(id)
	var scale: float = longest / maxf(natural.x, maxf(natural.y, natural.z))
	var holder: Node3D = Node3D.new()
	var model: Node3D = _instance(id)
	model.scale = Vector3.ONE * scale
	if centered:
		model.position.y = -natural.y * scale / 2.0
	holder.add_child(model)
	return holder


## The size (x, y, z) the model was made at.
static func size_of(id: String) -> Vector3:
	if not _sizes.has(id):
		var probe: Node3D = _scene(id).instantiate() as Node3D
		var mesh: MeshInstance3D = probe.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		_sizes[id] = mesh.mesh.get_aabb().size
		probe.free()
	return _sizes[id]


static func _scene(id: String) -> PackedScene:
	if not _scenes.has(id):
		_scenes[id] = load(PATH % id) as PackedScene
	return _scenes[id]


static func _instance(id: String) -> Node3D:
	var root: Node3D = _scene(id).instantiate() as Node3D
	ModelLook.apply(root, GAIN)
	return root
