class_name ScoreColumn
extends Control
## Tumbang Preso's points (every kid plays for itself): a column on the left sidewalk, a face for each kid with its points under it, the kid
## with the most at the top. When the points change the rows slide to their new places (and the number pops); with Reduce motion they jump.
## The taya's face has a red rim and yours a gold one. Display only; it never takes a touch. (Patintero has two teams and uses the score cards
## at the top centre, like Agawan Base.)

## One row: the face and the number under it.
const FACE: float = 48.0
const ROW_HEIGHT: float = 90.0
const WIDTH: float = 64.0
const SLIDE_TIME: float = 0.35
const POP_TIME: float = 0.25
const POP_SCALE: float = 1.5
const RIM: float = 4.0
const RIM_YOU: Color = Color(1.0, 0.8, 0.2)
const RIM_TAYA: Color = Color(1.0, 0.3, 0.26)
const RIM_OTHER: Color = Color(1.0, 1.0, 1.0)
const INK: Color = RoyaleStyle.NAVY

var _rows: Dictionary[int, Control] = {}
var _numbers: Dictionary[int, Label] = {}
var _rims: Dictionary[int, StyleBoxFlat] = {}
var _shown: Dictionary[int, int] = {}
var _slides: Dictionary[int, Tween] = {}
var _targets: Dictionary[int, float] = {}
var _pictures: Dictionary[int, TextureRect] = {}
var _local_id: int = -1


func _init() -> void:
	name = "ScoreColumn"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	custom_minimum_size = Vector2(WIDTH, 0.0)


## One row for each kid of `sim`.
func setup(sim: MatchSim, local_id: int) -> void:
	_local_id = local_id
	var game: TumbangPreso = sim.mode as TumbangPreso
	for id: int in game.points:
		_make_row(sim.players[id])
	sync(sim, true)


## The rows in the order of the points and what each says; `instant`: no sliding.
func sync(sim: MatchSim, instant: bool = false) -> void:
	var game: TumbangPreso = sim.mode as TumbangPreso
	var order: Array[int] = game.ranking()
	for index: int in order.size():
		var id: int = order[index]
		var row: Control = _rows.get(id) as Control
		if row == null:
			continue
		var target: float = float(index) * ROW_HEIGHT
		if not _targets.has(id):
			_targets[id] = row.position.y
		if not is_equal_approx(_targets[id], target):
			_slide(id, row, target, instant)
		var points: int = game.points[id]
		if _shown.get(id, -1) != points:
			var first: bool = not _shown.has(id)
			_shown[id] = points
			_numbers[id].text = str(points)
			if not first and not instant:
				_pop(_numbers[id])
		_rims[id].border_color = RIM_TAYA if id == game.taya_id else (RIM_YOU if id == _local_id else RIM_OTHER)
		# the kids can swap faces at the start (a team never has the same kid twice): the column follows
		var face: Texture2D = KidModel.portrait(sim.players[id].character_id)
		if _pictures[id].texture != face:
			_pictures[id].texture = face


## The kids in the order the rows stand (top first, where they are going), for tests.
func order() -> Array[int]:
	var ids: Array[int] = []
	ids.assign(_rows.keys())
	ids.sort_custom(func(a: int, b: int) -> bool: return target_y(a) < target_y(b))
	return ids


## Where the row of `id` is going (its place now when it is not sliding).
func target_y(id: int) -> float:
	return _targets.get(id, _rows[id].position.y) as float


func points_text(id: int) -> String:
	return _numbers[id].text


func _slide(id: int, row: Control, target: float, instant: bool) -> void:
	_targets[id] = target
	if _slides.has(id):
		_slides[id].kill()
		_slides.erase(id)
	if instant or Settings.reduced_motion or not is_inside_tree():
		row.position.y = target
		return
	var tween: Tween = create_tween()
	tween.tween_property(row, "position:y", target, SLIDE_TIME).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tween.finished.connect(func() -> void: _slides.erase(id))
	_slides[id] = tween


func _pop(label: Label) -> void:
	if Settings.reduced_motion or not is_inside_tree():
		return
	label.pivot_offset = label.size / 2.0
	label.scale = Vector2.ONE * POP_SCALE
	var tween: Tween = create_tween()
	tween.tween_property(label, "scale", Vector2.ONE, POP_TIME).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


func _make_row(state: PlayerState) -> void:
	var row: Control = _row(state.id, "Row%d" % state.id, Color(0.1, 0.18, 0.35))
	var picture: TextureRect = TextureRect.new()
	_pictures[state.id] = picture
	picture.texture = KidModel.portrait(state.character_id)
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	(row.get_node("Face") as Control).add_child(picture)


## One row: a round face (filled with `fill`) with the points under it, kept under `key`.
func _row(key: int, row_name: String, fill: Color) -> Control:
	var row: Control = Control.new()
	row.name = row_name
	row.custom_minimum_size = Vector2(WIDTH, ROW_HEIGHT)
	row.size = Vector2(WIDTH, ROW_HEIGHT)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var face: Panel = Panel.new()
	face.name = "Face"
	face.position = Vector2((WIDTH - FACE) / 2.0, 0.0)
	face.size = Vector2.ONE * FACE
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	face.clip_children = CanvasItem.CLIP_CHILDREN_AND_DRAW
	var rim: StyleBoxFlat = StyleBoxFlat.new()
	rim.bg_color = fill
	rim.set_corner_radius_all(int(FACE / 2.0))
	rim.set_border_width_all(int(RIM))
	rim.border_color = RIM_OTHER
	face.add_theme_stylebox_override("panel", rim)
	row.add_child(face)
	var number: Label = Label.new()
	number.name = "Points"
	number.text = "0"
	number.position = Vector2(0.0, FACE + 2.0)
	number.size = Vector2(WIDTH, ROW_HEIGHT - FACE - 2.0)
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	number.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(number, UiMetrics.DETAIL, Color.WHITE)
	row.add_child(number)
	add_child(row)
	_rows[key] = row
	_numbers[key] = number
	_rims[key] = rim
	return row
