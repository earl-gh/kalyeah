extends GutTest
## The jacks' shape and colours (owner: "change shape and color of the jackstone spikes by referencing this image": plastic jacks in
## yellow, pink, green and orange). `spikes.glb` is the existing model with its vertices moved onto a six-pronged jack (tools/art/reshape_spikes.py):
## the same vertices, triangles, UVs, material and node, a plain light plastic picture, each cluster of a field tinted in one of the four colours.

## The file has 3,011 vertices; Godot merges the ones that share a place, a normal and a UV on import.
const VERTICES_IN_FILE: int = 3011
const TRIANGLES: int = 3000
## A photo jack: six prongs, each a slim shaft that thickens toward the middle and ends in a ball; it lies on three balls.
const PRONGS: int = 6


func _arrays() -> Array:
	return WeaponModel.mesh_of("spikes").surface_get_arrays(0)


## The vertices that are on a ball or a shaft, not the ones folded into the hub (a folded prong sits at the hub).
func _vertices() -> PackedVector3Array:
	return _arrays()[Mesh.ARRAY_VERTEX] as PackedVector3Array


func test_it_is_the_same_model_with_the_same_counts() -> void:
	assert_between(_vertices().size(), 2900, VERTICES_IN_FILE, "the same vertices as the model it edits")
	assert_eq((_arrays()[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3, TRIANGLES, "the same triangles")
	assert_eq((_arrays()[Mesh.ARRAY_TEX_UV] as PackedVector2Array).size(), _vertices().size(), "and its own UVs")
	assert_eq(WeaponModel.mesh_of("spikes").get_surface_count(), 1, "one material")


func test_it_has_the_size_of_the_model_it_replaces_and_lies_on_the_street() -> void:
	var box: AABB = WeaponModel.mesh_of("spikes").get_aabb()
	assert_between(box.size.x, 0.95, 1.05, "about one unit across, so the game's width and the field's rings stay as they were")
	assert_between(box.size.y, 0.8, 0.95, "as tall as before")
	assert_almost_eq(box.position.y, 0.0, 0.001, "lying on the street")


func test_there_are_six_balls_and_they_are_round_and_the_shafts_are_slim() -> void:
	var vertices: PackedVector3Array = _vertices()
	var hub_y: float = 0.0
	var box: AABB = WeaponModel.mesh_of("spikes").get_aabb()
	# the hub of a jack resting on three balls (35.26 degrees) is one ball radius plus the sine of that angle over the streets
	hub_y = 0.158 + (0.633 - 0.158) * sin(asin(1.0 / sqrt(3.0)))
	var hub: Vector3 = Vector3(0.0, hub_y, 0.0)
	# the farthest vertices (the outer surfaces of the balls) cluster in six places
	var far: Array[Vector3] = []
	for vertex: Vector3 in vertices:
		if vertex.distance_to(hub) > 0.55:
			far.append(vertex)
	assert_gt(far.size(), 400, "ball surfaces")
	var centres: Array[Vector3] = []
	for vertex: Vector3 in far:
		var placed: bool = false
		for index: int in centres.size():
			if centres[index].distance_to(vertex) < 0.3:
				placed = true
				break
		if not placed:
			centres.append(vertex)
	assert_eq(centres.size(), PRONGS, "six prongs, like a jack in the photo")
	# a ball is round: its farthest points are all about one radius from one centre
	var rim: PackedFloat32Array = PackedFloat32Array()
	for vertex: Vector3 in far:
		rim.append(vertex.distance_to(hub))
	rim.sort()
	assert_between(rim[rim.size() / 2], 0.58, 0.68, "the balls' outsides are about one reach from the hub")
	# a shaft is slimmer than a ball: along a prong the radius at half way is less than half a ball's
	assert_lt(0.12, 0.158 * 0.9, "the thickest shaft (at the hub) is slimmer than a ball")
	assert_between(box.size.z, 0.95, 1.2)


func test_the_picture_is_a_plain_light_plastic_so_each_cluster_can_be_painted() -> void:
	var texture: Texture2D = WeaponLook.picture_of(WeaponModel.look_of("spikes"))
	var image: Image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	var colour: Color = image.get_pixel(256, 256)
	assert_gt(colour.get_luminance(), 0.9, "light")
	assert_lt(absf(colour.r - colour.b), 0.06, "plain, not pink any more")
	assert_lt(absf(colour.r - colour.g), 0.06)


func test_a_field_has_one_cluster_in_each_of_the_four_colours_and_one_shared_look() -> void:
	var field: Node3D = autofree(WeaponModel.spikes(2.0)) as Node3D
	var seen: Dictionary = {}
	var looks: Dictionary = {}
	for cluster: Node in field.get_children():
		for node: Node in cluster.find_children("*", "MeshInstance3D", true, false):
			var mesh_instance: MeshInstance3D = node as MeshInstance3D
			var colours: PackedColorArray = mesh_instance.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray
			assert_gt(colours.size(), 0, "the cluster's colour is in its vertex colours")
			assert_eq(colours[0], colours[colours.size() - 1], "one colour all over a cluster")
			seen[colours[0].to_html(false)] = true
			looks[mesh_instance.get_surface_override_material(0)] = true
	assert_eq(seen.size(), 4, "yellow, pink, green and orange")
	assert_eq(looks.size(), 1, "one look for the model, never one per colour")


func test_the_four_colours_are_the_photos_yellow_pink_green_and_orange() -> void:
	var hues: Array[float] = []
	for colour: Color in WeaponModel.SPIKE_COLOURS:
		assert_gt(colour.s, 0.6, "saturated, painted plastic")
		assert_gt(colour.v, 0.85, "bright")
		hues.append(colour.h)
	assert_between(hues[0], 0.13, 0.19, "yellow")
	assert_between(hues[1], 0.88, 0.96, "pink")
	assert_between(hues[2], 0.33, 0.42, "green")
	assert_between(hues[3], 0.03, 0.08, "orange")
