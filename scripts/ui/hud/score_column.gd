class_name ScoreColumn
extends Control
## Tumbang Preso's points (every kid plays for itself): a column on the left sidewalk, the kid's character card with its points under it, the
## kid with the most at the top. When the points change the rows slide to their new places (and the number pops); with Reduce motion they jump.
## The taya's card has a red rim and yours a gold one. Display only; it never takes a touch. (Patintero has two teams and uses the score cards
## at the top centre, like Agawan Base.)

## One row: the character card (the 1.75:2.5 picture of the Book's Kids) and the number under it.
const CARD: Vector2 = Vector2(46.0, 66.0)
const ROW_HEIGHT: float = 114.0
const WIDTH: float = 64.0
const SLIDE_TIME: float = 0.35
const POP_TIME: float = 0.25
const POP_SCALE: float = 1.5
const RIM: float = 3.0
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
		var face: Texture2D = _card(sim.players[id].character_id)
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
	var row: Control = _row(state.id, "Row%d" % state.id)
	var picture: TextureRect = TextureRect.new()
	_pictures[state.id] = picture
	picture.name = "Card"
	picture.texture = _card(state.character_id)
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var face: Control = row.get_node("Face") as Control
	face.add_child(picture)
	face.move_child(picture, 0)  # under the rim


## The kid's character card picture (the Book's), or its portrait when a card has no picture.
static func _card(character_id: StringName) -> Texture2D:
	var art: Texture2D = CharactersPage.card_picture(character_id)
	return art if art != null else KidModel.portrait(character_id)


## One row: a character card with a rim round it (the rim shows who is the taya and who is you) and the points under it, kept under `key`.
func _row(key: int, row_name: String) -> Control:
	var row: Control = Control.new()
	row.name = row_name
	row.custom_minimum_size = Vector2(WIDTH, ROW_HEIGHT)
	row.size = Vector2(WIDTH, ROW_HEIGHT)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var face: Control = Control.new()
	face.name = "Face"
	face.position = Vector2((WIDTH - CARD.x) / 2.0, 0.0)
	face.size = CARD
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var rim: StyleBoxFlat = StyleBoxFlat.new()
	rim.bg_color = Color(0.0, 0.0, 0.0, 0.0)
	rim.set_corner_radius_all(UiSkin.RADIUS_S)
	rim.set_border_width_all(int(RIM))
	rim.border_color = RIM_OTHER
	var frame: Panel = Panel.new()
	frame.name = "Rim"
	frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	frame.add_theme_stylebox_override("panel", rim)
	face.add_child(frame)
	row.add_child(face)
	var number: Label = Label.new()
	number.name = "Points"
	number.text = "0"
	number.position = Vector2(0.0, CARD.y + 2.0)
	number.size = Vector2(WIDTH, ROW_HEIGHT - CARD.y - 2.0)
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	number.vertical_alignment = VERTICAL_ALIGNMENT_TOP  # the points hug their card; the room under them is the gap to the next row
	number.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(number, UiMetrics.DETAIL, Color.WHITE)
	row.add_child(number)
	add_child(row)
	_rows[key] = row
	_numbers[key] = number
	_rims[key] = rim
	return row
