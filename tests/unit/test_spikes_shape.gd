extends GutTest
## The jacks' shape and colours (owner: "change shape and color of the jackstone spikes by referencing this image": plastic jacks in
## yellow, pink, green and orange; then "only 4 subpoles have balls at the end"). `spikes.glb` is the same file, node and material as before
## with its mesh rebuilt by tools/art/make_spikes.py: a round hub and six slim shafts that thicken toward the middle, a ball at the end of four
## of them and a plain rounded end on the two opposite ones, lying on three of its balls, in a plain light plastic picture that each cluster
## of a field is painted over in one of the four colours.

const REACH: float = 0.633
const BALL: float = 0.158
const SHAFT_HUB: float = 0.12


func _arrays() -> Array:
	return WeaponModel.mesh_of("spikes").surface_get_arrays(0)


func _vertices() -> PackedVector3Array:
	return _arrays()[Mesh.ARRAY_VERTEX] as PackedVector3Array


## The hub of a jack resting on three balls: one ball radius and the sine of 35.26 degrees of the prong's length above the street.
func _hub() -> Vector3:
	return Vector3(0.0, BALL + (REACH - BALL) * sin(asin(1.0 / sqrt(3.0))), 0.0)


func test_it_is_one_light_clean_mesh_in_one_material() -> void:
	assert_eq(WeaponModel.mesh_of("spikes").get_surface_count(), 1, "one mesh, one material")
	var triangles: int = (_arrays()[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3
	assert_between(triangles, 1500, 3000, "no more than the brief's 3,000 triangles")
	var normals: PackedVector3Array = _arrays()[Mesh.ARRAY_NORMAL] as PackedVector3Array
	assert_eq(normals.size(), _vertices().size(), "smooth normals: the contour is pushed out along them")


func test_it_has_the_size_of_the_model_it_replaces_and_lies_on_the_street() -> void:
	var box: AABB = WeaponModel.mesh_of("spikes").get_aabb()
	assert_between(box.size.x, 0.9, 1.1, "about one unit across, so the game's width and the field's rings work as before")
	assert_between(box.size.y, 0.8, 0.95, "as tall as before")
	assert_almost_eq(box.position.y, 0.0, 0.001, "lying on the street")


## The farthest points of the jack from its hub, grouped by the prong they are on.
func _tips() -> Array[Vector3]:
	var hub: Vector3 = _hub()
	var tips: Array[Vector3] = []
	for vertex: Vector3 in _vertices():
		if vertex.distance_to(hub) > REACH - 0.03:
			var near: bool = false
			for tip: Vector3 in tips:
				if tip.distance_to(vertex) < 0.2:
					near = true
					break
			if not near:
				tips.append(vertex)
	return tips


func test_six_prongs_four_with_a_ball_and_two_bare_opposite_each_other() -> void:
	var hub: Vector3 = _hub()
	var tips: Array[Vector3] = _tips()
	assert_eq(tips.size(), 6, "six prongs, like a jack in the photo")
	# a ball is wide, a bare end is narrow: the width of the vertices round each tip
	var wide: Array[Vector3] = []
	var bare: Array[Vector3] = []
	for tip: Vector3 in tips:
		var axis: Vector3 = (tip - hub).normalized()
		var behind: float = 0.0
		for vertex: Vector3 in _vertices():
			var along: float = (vertex - hub).dot(axis)
			if along > REACH - 0.17 and along < REACH - 0.1:
				behind = maxf(behind, ((vertex - hub) - axis * along).length())
		(wide if behind > 0.12 else bare).append(tip)
	assert_eq(wide.size(), 4, "four prongs end in a ball")
	assert_eq(bare.size(), 2, "one pole has no balls")
	if bare.size() == 2:
		assert_lt(((bare[0] - hub).normalized() + (bare[1] - hub).normalized()).length(), 0.05, "the two bare ends are one pole (opposite each other)")


func test_a_ball_is_round_and_a_shaft_is_slimmer_than_it() -> void:
	var hub: Vector3 = _hub()
	for tip: Vector3 in _tips():
		var axis: Vector3 = (tip - hub).normalized()
		var thickest_shaft: float = 0.0
		for vertex: Vector3 in _vertices():
			var along: float = (vertex - hub).dot(axis)
			var across: float = ((vertex - hub) - axis * along).length()
			if along > 0.1 and along < 0.28 and across < 0.14:  # the other prongs' vertices are far from this axis
				thickest_shaft = maxf(thickest_shaft, across)
		assert_lt(thickest_shaft, BALL * 0.9, "a shaft out of the hub is slimmer than a ball")
		assert_gt(thickest_shaft, 0.04, "and is a real shaft, not a wire")


func test_the_picture_is_a_plain_light_plastic_so_each_cluster_can_be_painted() -> void:
	var texture: Texture2D = WeaponLook.picture_of(WeaponModel.look_of("spikes"))
	var image: Image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	var colour: Color = image.get_pixel(256, 256)
	assert_gt(colour.get_luminance(), 0.9, "light")
	assert_lt(absf(colour.r - colour.b), 0.06, "plain, not pink any more")
	assert_lt(absf(colour.r - colour.g), 0.06)


func test_a_field_has_one_cluster_in_each_of_the_six_colours_and_one_shared_look() -> void:
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
	assert_eq(seen.size(), 6, "yellow, pink, green, orange, blue and purple")
	assert_eq(looks.size(), 1, "one look for the model, never one per colour")


func test_the_six_colours_are_the_photos_yellow_pink_green_orange_then_blue_and_purple() -> void:
	var hues: Array[float] = []
	for colour: Color in WeaponModel.SPIKE_COLOURS:
		assert_gt(colour.s, 0.6, "saturated, painted plastic")
		assert_gt(colour.v, 0.85, "bright")
		hues.append(colour.h)
	assert_between(hues[0], 0.13, 0.19, "yellow")
	assert_between(hues[1], 0.88, 0.96, "pink")
	assert_between(hues[2], 0.33, 0.42, "green")
	assert_between(hues[3], 0.03, 0.08, "orange")
	assert_between(hues[4], 0.56, 0.64, "blue")
	assert_between(hues[5], 0.72, 0.80, "purple")


func test_the_jacks_exist_only_in_agawan_base() -> void:
	# owner: the street games have no weapons but the can and the slipper, so no field of jacks is ever built there
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		var rules: StreetModeRules = StreetMode.rules_for(game)
		assert_true(rules != null)
	var source: String = FileAccess.get_file_as_string("res://scripts/view/fx/fx_zones.gd")
	assert_true(source.contains('zone.def.id == &"jacks"'), "a field of jacks is built from a jacks weapon zone, which only the weapon cards make")
