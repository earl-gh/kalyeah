extends GutTest
## The team outline (owner, 2026-10-29): a flat edge round every kid, blue round your team and red round the other, drawn
## over the kid's authored material without changing it.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")

var _was_colorblind: bool


func before_each() -> void:
	_was_colorblind = Settings.colorblind
	Settings.colorblind = false


func after_each() -> void:
	Settings.colorblind = _was_colorblind


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	return practice


func _outline(view: KidModel) -> ShaderMaterial:
	var mesh: MeshInstance3D = view.get_node("Body").find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	return mesh.material_overlay as ShaderMaterial


func test_your_team_is_outlined_blue_and_the_other_red() -> void:
	var practice: PracticeMatch = _practice()
	var own: int = 0
	var other: int = 0
	for id: int in practice.sim.players:
		var outline: ShaderMaterial = _outline(practice._views[id] as KidModel)
		assert_not_null(outline, "kid %d has an outline" % id)
		assert_eq(outline.shader, KidModel.OUTLINE_SHADER)
		var color: Color = outline.get_shader_parameter("color")
		if practice.sim.players[id].team == practice.local_team:
			assert_eq(color, TeamColors.world_own(), "kid %d is on your team: blue" % id)
			own += 1
		else:
			assert_eq(color, TeamColors.world_enemy(), "kid %d is on the other team: red" % id)
			other += 1
	assert_gt(own, 0)
	assert_gt(other, 0)


func test_the_outline_leaves_the_authored_material_alone() -> void:
	var practice: PracticeMatch = _practice()
	var view: KidModel = practice._views[practice.local_id] as KidModel
	var mesh: MeshInstance3D = view.get_node("Body").find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	assert_null(mesh.material_override, "no override: the kid keeps its own material")
	assert_true(mesh.get_active_material(0) is BaseMaterial3D, "the GLB's material is still the one drawn")
