class_name PointResult
extends Control
## The existing point freeze, presented as centered words over a dimmed arena; no gameplay state is changed.

var subject: Label
var outcome: Label
var score: Label

func _init() -> void:
	name = "PointResult"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	hide()
	var dim: ColorRect = ColorRect.new()
	dim.color = MenuDialog.DIM
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	var column: VBoxContainer = VBoxContainer.new()
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	center.add_child(column)
	subject = _line(column, UiMetrics.BODY)
	outcome = _line(column, UiMetrics.HERO)
	score = _line(column, UiMetrics.DISPLAY)

func _line(parent: Control, font_size: int) -> Label:
	var label: Label = Label.new()
	UiKit.letter(label, font_size)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label

func sync(sim: MatchSim, local_team: int) -> void:
	visible = sim.phase == MatchSim.Phase.POINT_FREEZE
	if not visible:
		return
	var point: Vector2i = sim.point_log[-1] if not sim.point_log.is_empty() else Vector2i(local_team, MatchSim.CaptureKind.BASE)
	var ours: bool = point.x == local_team
	if point.y == MatchSim.CaptureKind.ALL_TAGGED:
		subject.text = "All enemies" if ours else "All allies"
		outcome.text = "TAGGED"
	else:
		subject.text = "Enemy base" if ours else "Ally base"
		outcome.text = "CAPTURED"
	score.text = "%d - %d" % [sim.score.points[local_team], sim.score.points[1 - local_team]]
