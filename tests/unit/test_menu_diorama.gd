extends GutTest
## The 3D map model (a 1:1 slab of the far half of the street on a blue platform). The main screen
## does not show it for now (the map cards replaced it); it stays ready and tested.


func _model() -> MenuDiorama:
	var holder: Control = autofree(Control.new()) as Control
	holder.size = Vector2(690.0, 480.0)
	add_child(holder)
	var model: MenuDiorama = MenuDiorama.new()
	model.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	holder.add_child(model)
	return model


func test_the_model_is_the_far_half_of_the_play_area_and_fits_its_frame() -> void:
	var model: MenuDiorama = _model()
	await wait_frames(2)
	var layout: MapLayout = model.map.layout
	assert_true(model.map.diorama)
	assert_eq(layout.lane_length, (load("res://data/rules/map_layout.tres") as MapLayout).lane_length, "the real play area")
	# the cement and its stone base in the game's exact shape, plus the corner props only
	assert_eq(model.map._posts.size(), 1, "the far base's electric post")
	var found: Array[Node] = model.map.find_children("*", "MeshInstance3D", true, false)
	var street: AABB = (found[0] as MeshInstance3D).get_aabb()
	var platform: AABB = (found[1] as MeshInstance3D).get_aabb()
	assert_almost_eq(street.size.x / street.size.z, layout.lane_width / (layout.lane_length / 2.0), 0.001, "exactly the far half of the street (the slab and the platform both)")
	assert_almost_eq(street.size.x, layout.lane_width, 0.01, "no sidewalks")
	assert_almost_eq(street.size.z, layout.lane_length / 2.0, 0.01, "the top half, no extra street at the ends")
	assert_almost_eq(platform.size.x, layout.lane_width + StreetMap.PLATFORM_MARGIN * 2.0, 0.01, "the blue platform reaches past it evenly")
	assert_almost_eq(platform.size.x / platform.size.z, (layout.lane_width + StreetMap.PLATFORM_MARGIN * 2.0) / (layout.lane_length / 2.0 + StreetMap.PLATFORM_MARGIN * 2.0), 0.001, "and keeps that shape too")
	assert_gt(StreetMap.PLATFORM_HEIGHT, StreetMap.PLINTH_HEIGHT, "a thick stone base on a thicker platform")
	var props: Array[Node] = model.map.find_children("*", "MeshInstance3D", true, false)
	assert_eq(props.size(), 2 + 1, "slab + platform + post and nothing else")
	assert_eq(model.viewport.find_children("*", "KidModel", true, false).size(), 0, "no characters")
	for cx: float in [-layout.lane_width / 2.0, layout.lane_width / 2.0]:
		for cz: float in [-layout.lane_length / 2.0, 0.0]:
			assert_true(model.camera.is_position_in_frustum(Vector3(cx, 0.0, cz)), "corner %s of the top half is in view" % Vector2(cx, cz))


func test_the_map_model_stands_still() -> void:
	var model: MenuDiorama = _model()
	await wait_frames(2)
	var before: Transform3D = model.camera.global_transform
	await wait_frames(30)
	assert_eq(model.camera.global_transform, before)
	assert_false(model.is_processing(), "nothing runs every frame")
	assert_false(model.is_physics_processing())
