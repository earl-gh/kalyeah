class_name WeaponModel
extends RefCounted
## The thrown and placed skill objects that are real 3D models (assets/models/weapons/<id>.glb,
## made by tools/blender/build_prop_model.py from the owner's models): the slipper (tsinelas),
## the can (lata), the marble (bola), the crumpled paper (papel trap), the paper sheet (papel
## shield), the spinning top (trumpo), the spikes (jacks), the meteor (bato heavy), the scissors
## (gunting), the soil block (langit lupa wall) and the green cross (heal). Each has its base
## centre on the origin and the painted look from WeaponLook.
## Weapons without a model here keep their code-drawn object (Props.weapon_object).
## Every instance of one model shares one set of look materials (built by the first instance), and every model mesh joins
## `GROUP` so tests and tools can tell the skill objects apart from the flat effects drawn round them.

const PATH: String = "res://assets/models/weapons/%s.glb"
## Weapon id -> [model, longest side in metres]. The slippers are drawn at most twice their hit circle (0.6 m light, 1.0 m
## heavy; 1.2 and 2.0 m on screen) so a flying slipper never hides a whole kid (owner's call: they were 0.8 and 1.5).
const OBJECTS: Dictionary[StringName, Array] = {
	&"tsinelas_light": ["slipper", 0.6],
	&"tsinelas_heavy": ["slipper", 1.0],
	&"lata": ["can", 0.7],
	# the marble: 1.12 m drawn (owner's call: 0.8 / 1.6 m covered kids' faces, 0.9 m was too small, so +25%; it bounces low)
	&"bola": ["marble", 0.56],
	&"papel_trap": ["crumpled_paper", 0.7],
	# the top: 0.9 m was a little small (owner), +25%
	&"trumpo": ["trumpo", 1.12],
	# the light rock is the meteor's stone, small (the owner made no separate rock)
	&"bato_light": ["meteor", 0.55],
}
## The spikes of a jacks field: this many clusters (owner: fewer and smaller than the five 0.9 m ones), each this wide in metres. They end
## wherever they roll to: a different, random spread in every field (owner: no fixed formation), see `spikes`.
const SPIKE_CLUSTERS: int = 6
const SPIKE_WIDTH: float = 0.315 * 1.75
## The plastic colours of the jacks: the owner's photo's yellow, pink, green and orange (a little cleaner and brighter in the painted way), then
## blue and purple (owner); each cluster of a field takes the next one, so a field of six has all of them. The model's picture is plain light plastic and the colour
## rides in the cluster's vertex colours (see `_instance`), so there is still one look for the model.
const SPIKE_COLOURS: Array[Color] = [Color("F4E43A"), Color("FF4F9A"), Color("45E06C"), Color("FF6A1F"), Color("3D8BFF"), Color("A855F7")]
## A jack lies at most this far (of its width) in from the field's rim, so it never overhangs the slow and damage area, and no two lie closer
## than `SPIKE_APART` of their width (when there is room for that).
const SPIKE_RIM: float = 0.6
const SPIKE_APART: float = 1.25
## Tries for a jack's spot before it takes the roomiest one it saw.
const SPIKE_TRIES: int = 40
## The rock meteor of bato heavy (metres across; a 2 m circle reads "a big rock lands here" without hiding both kids in it).
const METEOR_SIZE: float = 1.4
## The heal cross (metres wide before the kids' view scale: 0.84 m drawn, so on the chest it stays under the face).
const CROSS_WIDTH: float = 0.42
## The group every weapon model mesh joins.
const GROUP: StringName = &"weapon_model"
## The paper wall's model.
const SHIELD: String = "paper"

static var _scenes: Dictionary[String, PackedScene] = {}
static var _sizes: Dictionary[String, Vector3] = {}
## Model id -> the look materials of its surfaces, in find order (shared by every instance of the model).
static var _looks: Dictionary[String, Array] = {}
## "model:colour" -> the model's mesh with that colour in its vertex colours (one mesh per colour, shared by every copy).
static var _painted: Dictionary[String, ArrayMesh] = {}


static func has_model(weapon_id: StringName) -> bool:
	return OBJECTS.has(weapon_id)


## True for the weapons that snip with the scissors (the gunting cards).
static func has_scissors(weapon_id: StringName) -> bool:
	return weapon_id == &"gunting_light" or weapon_id == &"gunting_heavy"


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


## Model `id` standing on the ground, `height` metres tall with its own proportions (the kid turned into a can when polymorphed).
static func standing(id: String, height: float) -> Node3D:
	var natural: Vector3 = size_of(id)
	var holder: Node3D = Node3D.new()
	var model: Node3D = _instance(id)
	model.scale = Vector3.ONE * (height / natural.y)
	holder.add_child(model)
	return holder


## A jacks field: spike clusters lying anywhere inside a circle of `radius`, each turned any way. The spread is random for every field
## (no ring, no pattern: it comes from `seed`, the field's id, so every screen shows the same field the same way, and each field differs).
static func spikes(radius: float, seed: int = 0) -> Node3D:
	var holder: Node3D = Node3D.new()
	var natural: Vector3 = size_of("spikes")
	var scale: float = SPIKE_WIDTH / natural.x
	var spots: Array[Vector2] = spike_spots(radius, seed)
	for i: int in SPIKE_CLUSTERS:
		var model: Node3D = _instance("spikes", SPIKE_COLOURS[i % SPIKE_COLOURS.size()])
		model.scale = Vector3.ONE * scale
		model.position = Vector3(spots[i].x, 0.0, spots[i].y)
		model.rotation.y = _unit(seed, i, 1000) * TAU
		holder.add_child(model)
	return holder


## Where the jacks of field `seed` lie, in a circle of `radius` round (0, 0): a random place for each (uniform over the circle's room), none
## overhanging the rim and none on top of another where there is room, tried `SPIKE_TRIES` times each and then the roomiest seen.
static func spike_spots(radius: float, seed: int) -> Array[Vector2]:
	var room: float = maxf(radius - SPIKE_WIDTH * SPIKE_RIM, 0.0)
	var apart: float = SPIKE_WIDTH * SPIKE_APART
	var spots: Array[Vector2] = []
	for i: int in SPIKE_CLUSTERS:
		var best: Vector2 = Vector2.ZERO
		var best_gap: float = -1.0
		for attempt: int in SPIKE_TRIES:
			var spot: Vector2 = Vector2.from_angle(_unit(seed, i, attempt * 2) * TAU) * (sqrt(_unit(seed, i, attempt * 2 + 1)) * room)
			var gap: float = INF
			for other: Vector2 in spots:
				gap = minf(gap, spot.distance_to(other))
			if gap > best_gap:
				best_gap = gap
				best = spot
			if gap >= apart:
				break
		spots.append(best)
	return spots


## A number from 0 up to (not including) 1 that depends only on the field's `seed` and the two salts (a hash, not a dice roll).
static func _unit(seed: int, a: int, b: int) -> float:
	return float(hash(Vector3i(seed, a, b)) & 0xFFFFF) / 1048576.0


## `mesh` of model `id` with every vertex coloured `colour` (made once per colour).
static func _painted_mesh(id: String, mesh: Mesh, colour: Color) -> ArrayMesh:
	var key: String = "%s:%s" % [id, colour.to_html(false)]
	if not _painted.has(key):
		var arrays: Array = mesh.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
		var colours: PackedColorArray = PackedColorArray()
		colours.resize(vertices.size())
		colours.fill(colour)
		arrays[Mesh.ARRAY_COLOR] = colours
		var painted: ArrayMesh = ArrayMesh.new()
		painted.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		painted.surface_set_material(0, mesh.surface_get_material(0))
		_painted[key] = painted
	return _painted[key]


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


## Wraps `model` for an effect that moves it: Holder (placed at the sim position) > "Squash" (squash and stretch) >
## ["Tilt" when `tilted`] > "Spin" (turns and tumbles) > the model. Returns the holder.
static func rig(model: Node3D, tilted: bool = false) -> Node3D:
	var holder: Node3D = Node3D.new()
	var squash: Node3D = Node3D.new()
	squash.name = "Squash"
	holder.add_child(squash)
	var parent: Node3D = squash
	if tilted:
		var tilt: Node3D = Node3D.new()
		tilt.name = "Tilt"
		squash.add_child(tilt)
		parent = tilt
	var spin: Node3D = Node3D.new()
	spin.name = "Spin"
	parent.add_child(spin)
	spin.add_child(model)
	return holder


## Model scenes and sizes cached right now (tests).
static func cached_count() -> int:
	return _scenes.size() + _sizes.size() + _painted.size()


## Empties the scene, size and look caches (the last effect view left the tree; a kid's can keeps its own copy).
static func clear_cache() -> void:
	_scenes.clear()
	_sizes.clear()
	_looks.clear()
	_painted.clear()


## How many model looks are shared right now (one per model id; never one per instance).
static func look_count() -> int:
	return _looks.size()


## The first mesh of model `id` as made (for particles that throw little copies of it, like rock chips).
static func mesh_of(id: String) -> Mesh:
	var probe: Node3D = _instance(id)
	var mesh: Mesh = (probe.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).mesh
	probe.free()
	return mesh


## The shared look material of model `id`'s first surface.
static func look_of(id: String) -> Material:
	if not _looks.has(id):
		_instance(id).free()
	return _looks[id][0] as Material


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


## A copy of model `id`, painted in `colour` when one is given (the model's picture is plain, the colour is in its vertex colours).
static func _instance(id: String, colour: Variant = null) -> Node3D:
	var root: Node3D = _scene(id).instantiate() as Node3D
	var meshes: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
	if colour != null:
		for node: Node in meshes:
			var mesh_instance: MeshInstance3D = node as MeshInstance3D
			mesh_instance.mesh = _painted_mesh(id, mesh_instance.mesh, colour as Color)
	if not _looks.has(id):
		_looks[id] = WeaponLook.apply(root, id)
	else:
		var looks: Array = _looks[id]
		var next: int = 0
		for node: Node in meshes:
			var mesh_instance: MeshInstance3D = node as MeshInstance3D
			mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
			for surface: int in mesh_instance.mesh.get_surface_count():
				if next < looks.size():
					mesh_instance.set_surface_override_material(surface, looks[next] as Material)
				next += 1
	for node: Node in meshes:
		node.add_to_group(GROUP)
	return root
