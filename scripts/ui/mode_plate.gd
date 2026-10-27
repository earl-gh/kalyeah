class_name ModePlate
extends Control
## The mode's name written on the front face of the diorama's platform (owner). The face is a flat wall of the platform standing upright,
## seen by the fixed orthographic camera tilted 45 degrees: lines stay parallel (no keystone) and heights shrink, so the lettering stays
## level and is plain text squashed a little in height (`FORESHORTEN`). It is placed by the face's rectangle on
## the picture (picture pixels) and so follows the picture everywhere: swiping, bobbing and scaling.

## How much a height on the lettering shows: a true cos 45 degrees (0.7071) stretched the letters sideways, so the owner asked for less
## (the squash is milder than the camera's tilt, which also keeps the name readable).
const FORESHORTEN: float = 0.86
## The lettering's share of the face's height and width it may take at most.
const HEIGHT_SHARE: float = 0.95
const WIDTH_SHARE: float = 0.86

## The face of the platform on the picture, in the picture's pixels.
var face: Rect2 = Rect2()
## The size of the picture the face is measured on.
var picture_size: Vector2 = Vector2.ONE
var text: String = "":
	set(value):
		text = value
		_label.text = value
		_layout()

var _label: Label


func _init() -> void:
	name = "Plate"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label = Label.new()
	_label.name = "Name"
	UiKit.letter(_label, UiMetrics.DISPLAY, Color.WHITE, Color.BLACK)
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the line's size is known only once its font is: the lettering is fitted again then
	_label.minimum_size_changed.connect(_layout)
	add_child(_label)


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_layout()


## Where the face is inside this control (the picture is centred and fitted, as the card shows it).
func face_rect() -> Rect2:
	var fit: float = minf(size.x / picture_size.x, size.y / picture_size.y)
	var origin: Vector2 = (size - picture_size * fit) / 2.0
	return Rect2(origin + face.position * fit, face.size * fit)


## The share of the lettering that fits the face: the squashed line must fit its height and the whole name its width.
func lettering_scale() -> float:
	var rect: Rect2 = face_rect()
	var line: Vector2 = _label.get_combined_minimum_size()
	if line.x <= 0.0 or line.y <= 0.0:
		return 0.0
	return minf(rect.size.x * WIDTH_SHARE / line.x, rect.size.y * HEIGHT_SHARE / (line.y * FORESHORTEN))


func _layout() -> void:
	if _label == null or picture_size.x <= 0.0 or size.x <= 0.0:
		return
	var rect: Rect2 = face_rect()
	var line: Vector2 = _label.get_combined_minimum_size()
	var fit: float = lettering_scale()
	_label.size = line
	_label.pivot_offset = line / 2.0
	_label.scale = Vector2(fit, fit * FORESHORTEN)
	_label.position = rect.get_center() - line / 2.0
