extends GutTest
## The shoulder camera leaves out any prop standing between it and your kid (owner, 2026-11-10: "hide the props when the camera ends behind
## it"): at the ends of the street the camera stands among the barrier rows and the post, and turning it can put a building in the way. A
## prop comes back as soon as the line is clear. Seam: StreetMap.hide_between, segment_hits_box; PracticeMatch (shoulder view).

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _kind: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	Session.practice_kind = &"match"


func after_each() -> void:
	Session.practice_kind = _kind


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 3:
		practice.advance(DT)
	return practice


func test_a_line_hits_a_box_only_when_it_passes_through_it() -> void:
	var box: AABB = AABB(Vector3(-1, 0, -1), Vector3(2, 2, 2))
	assert_true(StreetMap.segment_hits_box(Vector3(0, 1, 5), Vector3(0, 1, -5), box), "straight through")
	assert_true(StreetMap.segment_hits_box(Vector3(0, 1, 0), Vector3(0, 1, 9), box), "starting inside")
	assert_false(StreetMap.segment_hits_box(Vector3(0, 3, 5), Vector3(0, 3, -5), box), "over it")
	assert_false(StreetMap.segment_hits_box(Vector3(0, 1, 5), Vector3(0, 1, 2), box), "stops short of it")
	assert_false(StreetMap.segment_hits_box(Vector3(3, 1, 5), Vector3(3, 1, -5), box), "beside it")


func test_props_in_the_way_are_left_out_and_come_back_when_the_line_is_clear() -> void:
	var practice: PracticeMatch = _practice()
	var map: StreetMap = practice._map
	var kid: float = KidModel.KID_HEIGHT * practice._camera.rules.kid_view_scale
	# a camera low among the houses on the -X side, looking across at a kid in the street: the house it stands in is in the way
	var house: Node3D = map.house_rows[-1][map.house_rows[-1].size() / 2] as Node3D
	var middle: Vector3 = StreetMap.world_box(house).get_center()
	map.hide_between(Vector3(middle.x, 2.0, middle.z), Vector3(0.0, 0.0, middle.z), kid)
	var hidden: Array[Node3D] = map.hidden_props()
	assert_true(hidden.has(house), "the house the camera stands in is left out")
	for prop: Node3D in hidden:
		assert_false(prop.visible, "%s is left out" % prop.name)
		assert_lt(StreetMap.world_box(prop).position.x, 0.0, "%s stands on the camera's side of the kid" % prop.name)
	# from above the middle of the street nothing is in the way: everything is back
	map.hide_between(Vector3(0.0, 30.0, 10.0), Vector3.ZERO, kid)
	assert_eq(map.hidden_props().size(), 0)
	for prop: Node3D in hidden:
		assert_true(prop.visible, "%s is back" % prop.name)


func test_the_match_hides_what_stands_between_the_shoulder_camera_and_your_kid() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = practice.sim.players[practice.local_id]
	var end: float = LAYOUT.lane_length / 2.0 - 1.0
	me.position = Vector2(0.0, end if practice.own_side() == MapLayout.SIDE_OWN else -end)  # your own end: the camera stands behind the row
	for i: int in 3:
		practice.advance(DT)
	var eye: Vector3 = practice._camera.global_position
	var feet: Vector3 = Vector3(me.position.x, 0.0, me.position.y)
	var kid: float = KidModel.KID_HEIGHT * practice._camera.rules.kid_view_scale
	var map: StreetMap = practice._map
	for prop: Node3D in map.hidden_props():
		assert_false(prop.visible)
	# nothing left drawn stands in the line to your kid, or right next to the camera
	var clear: float = practice._camera.rules.shoulder_clear_reach
	assert_gt(clear, 0.0, "the data clears a few metres round the camera")
	for prop: Node3D in map._occluders:
		if not prop.visible or not prop.is_visible_in_tree():
			continue
		var box: AABB = StreetMap.world_box(prop)
		assert_gt((eye.clamp(box.position, box.end) - eye).length(), clear, "%s is not right at the camera" % prop.name)
		for height: float in [0.15, 0.5, 1.0]:
			assert_false(StreetMap.segment_hits_box(eye, feet + Vector3.UP * kid * height, box), "%s is not in the way" % prop.name)
