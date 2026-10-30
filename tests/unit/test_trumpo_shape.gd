extends GutTest
## The spinning top's shape (owner: "Filipino pinoy wooden trumpo, not a Mexican-style spinning top"): `trumpo.glb` was reshaped from the owner's own
## model by moving its vertices only (a fat flattened dome over a cone that tapers to a blunt tip), so its triangle layout, UVs and picture are
## as they were. These pin the new silhouette and that nothing else moved.

const PATH: String = "res://assets/models/weapons/trumpo.glb"
## The model as the owner made it: the same counts must stay (the shape changed, the topology did not).
const VERTICES: int = 1640
const TRIANGLES: int = 2199
## The cone ends and the dome begins about here (fraction of the height).
const RIM: float = 0.6
## Height bands (fraction of the height) the profile is read in.
const BANDS: int = 10


func _arrays() -> Array:
	return WeaponModel.mesh_of("trumpo").surface_get_arrays(0)


## The widest radius of the vertices in each height band (0 where the band has none).
func _profile() -> PackedFloat32Array:
	var vertices: PackedVector3Array = _arrays()[Mesh.ARRAY_VERTEX]
	var height: float = WeaponModel.size_of("trumpo").y
	var widest: PackedFloat32Array = PackedFloat32Array()
	widest.resize(BANDS)
	for vertex: Vector3 in vertices:
		var band: int = clampi(int(vertex.y / height * float(BANDS)), 0, BANDS - 1)
		widest[band] = maxf(widest[band], Vector2(vertex.x, vertex.z).length())
	return widest


func test_the_topology_is_the_owners_only_the_positions_moved() -> void:
	var arrays: Array = _arrays()
	assert_eq((arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size(), VERTICES, "same vertices")
	assert_eq((arrays[Mesh.ARRAY_INDEX] as PackedInt32Array).size(), TRIANGLES * 3, "same triangles")
	assert_eq((arrays[Mesh.ARRAY_TEX_UV] as PackedVector2Array).size(), VERTICES, "same UVs")
	for normal: Vector3 in arrays[Mesh.ARRAY_NORMAL] as PackedVector3Array:
		assert_almost_eq(normal.length(), 1.0, 0.01, "every normal is a unit vector")


func test_it_stands_on_its_tip_centred_on_its_axis_and_taller_than_wide() -> void:
	var box: AABB = (WeaponModel.mesh_of("trumpo") as Mesh).get_aabb()
	assert_almost_eq(box.position.y, 0.0, 0.01, "the tip is on the ground")
	assert_gt(box.size.y, box.size.x, "the model is tallest up and down: OBJECTS' longest side is its height")
	assert_gt(box.size.y, box.size.z)
	assert_almost_eq(box.position.x + box.size.x / 2.0, 0.0, 0.01, "centred on x")
	assert_almost_eq(box.position.z + box.size.z / 2.0, 0.0, 0.01, "centred on z")
	var node: Node3D = autofree(WeaponModel.make(&"trumpo", false))
	var drawn: Vector3 = box.size * (node.get_child(0) as Node3D).scale
	assert_almost_eq(drawn.y, WeaponModel.OBJECTS[&"trumpo"][1] as float, 0.01, "still 1.12 m long")


func test_the_dome_is_wider_than_it_is_tall() -> void:
	var box: AABB = (WeaponModel.mesh_of("trumpo") as Mesh).get_aabb()
	var dome_height: float = box.size.y * (1.0 - RIM)
	assert_gt(box.size.x, dome_height * 1.4, "the dome (%.2f wide) is far wider than the %.2f it stands above the rope" % [box.size.x, dome_height])
	var profile: PackedFloat32Array = _profile()
	var widest: int = 0
	for band: int in BANDS:
		if profile[band] > profile[widest]:
			widest = band
	assert_gte(widest, int(RIM * float(BANDS)), "the widest part is in the dome, above the rope")
	var crown: float = 0.0
	for vertex: Vector3 in _arrays()[Mesh.ARRAY_VERTEX] as PackedVector3Array:
		if vertex.y > box.size.y * 0.97:
			crown = maxf(crown, Vector2(vertex.x, vertex.z).length())
	assert_lt(crown, profile[widest] * 0.5, "and the crown narrows again, round, with no knob on it")


func test_the_cone_tapers_without_a_bulge_to_a_blunt_tip() -> void:
	var profile: PackedFloat32Array = _profile()
	var previous: float = 0.0
	for band: int in int(RIM * float(BANDS)):
		if profile[band] == 0.0:
			continue
		assert_gte(profile[band], previous - 0.01, "band %d is no narrower than the one below it" % band)
		previous = profile[band]
	var widest: float = box_width() / 2.0
	assert_lt(profile[0], widest * 0.3, "the tip is narrow")
	assert_gt(profile[0], 0.02, "but blunt, not a needle")
	assert_gt(profile[int(RIM * float(BANDS)) - 1], widest * 0.6, "the mouth of the cone holds the dome")


func box_width() -> float:
	return (WeaponModel.mesh_of("trumpo") as Mesh).get_aabb().size.x
