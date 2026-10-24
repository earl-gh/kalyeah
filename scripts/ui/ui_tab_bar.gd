class_name UiTabBar
extends Control
## The main screen's bottom navigation, in the style of Clash Royale's: the owner's cardboard plate along the bottom with one
## picture per destination (Street, Play, Settings). The layout never moves: three equal tabs, every picture centred on the same line.
## The open tab's picture only grows (round its own centre) and its name fades in under it, and two small arrows float over the
## bar, a little way out from the picture, to go to the neighbouring tab (the leftmost tab has only a right arrow, the rightmost
## only a left one). Changing tab is one smooth move: the picture grows and shrinks, the name fades, the arrows slide to the new tab.
## The pictures are the owner's files in `assets/ui/nav/`.

signal tab_pressed(id: StringName)

## The plate's picture is 1804 x 493 of its file; the bar is that tall at the design width.
const HEIGHT: float = 196.0
## The pictures' sizes (design pixels) for the open and an idle tab, and the one line their centres sit on, from the bar's top.
const ICON_ACTIVE: float = 116.0
const ICON_IDLE: float = 84.0
const ICON_Y: float = 86.0
## The open tab's name sits under its picture (it rises a few pixels into place as it fades in).
const LABEL_Y: float = 152.0
const LABEL_HEIGHT: float = 32.0
const LABEL_RISE: float = 8.0
## The arrows: how big they are drawn, how big they are to the thumb, and how far from the picture's edge.
const ARROW_SIZE: float = 26.0
const ARROW_TOUCH: float = 88.0
const ARROW_GAP: float = 30.0
const IDLE_SHADE: Color = Color(0.82, 0.82, 0.86)
## The time a change of tab takes (instant with Reduce motion).
const GROW_TIME: float = 0.28

const PLATE: Texture2D = preload("res://assets/ui/nav/nav_plate.webp")
const ARROW: Texture2D = preload("res://assets/ui/nav/nav_arrow.png")
const PICTURES: Dictionary[StringName, Texture2D] = {
	&"street": preload("res://assets/ui/nav/nav_street.webp"),
	&"play": preload("res://assets/ui/nav/nav_play.webp"),
	&"settings": preload("res://assets/ui/nav/nav_settings.png"),
}
## The visible part of each picture inside its file, as shares of the file (the files have clear margins and their art is not
## centred), so the bar can place the art itself: {x, y, width, height}.
const PLATE_PART: Rect2 = Rect2(0.04539, 0.25221, 0.90974, 0.62169)
const ARROW_PART: Rect2 = Rect2(0.29538, 0.18151, 0.52537, 0.63923)
const PICTURE_PARTS: Dictionary[StringName, Rect2] = {
	&"street": Rect2(0.08453, 0.06459, 0.84927, 0.86124),
	&"play": Rect2(0.14317, 0.12514, 0.85683, 0.80158),
	&"settings": Rect2(0.11387, 0.11387, 0.78803, 0.79061),
}

var tabs: Dictionary[StringName, Button] = {}
var active: StringName = &""
## The arrows floating beside the open tab: to the tab on its left and on its right (faded out at the ends).
var arrow_left: Button
var arrow_right: Button

var _order: Array[StringName] = []
var _pictures: Dictionary[StringName, TextureRect] = {}
var _labels: Dictionary[StringName, Label] = {}
## How open each tab is now (0 idle, 1 open), tweened.
var _openness: Dictionary[StringName, float] = {}
var _tween: Tween


## `items`: {id, label, icon} in order, left to right (`icon`: the key of PICTURES).
func _init(items: Array[Dictionary] = []) -> void:
	name = "TabBar"
	custom_minimum_size = Vector2(0.0, HEIGHT)
	mouse_filter = Control.MOUSE_FILTER_PASS
	var plate: TextureRect = TextureRect.new()
	plate.name = "Plate"
	plate.texture = _part(PLATE, PLATE_PART)
	plate.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	plate.stretch_mode = TextureRect.STRETCH_SCALE
	plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
	plate.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(plate)
	for item: Dictionary in items:
		_add_tab(item["id"] as StringName, String(item["label"]), item["icon"] as StringName)
	# added last, so they float over the tabs
	arrow_left = _arrow("ArrowLeft", false, "Previous tab")
	arrow_right = _arrow("ArrowRight", true, "Next tab")
	resized.connect(_layout)


## One of the bar's pictures (&"street", &"play", &"settings") as a texture of its own: the same art elsewhere, like the gear in a match.
static func picture(kind: StringName) -> Texture2D:
	return _part(PICTURES[kind], PICTURE_PARTS[kind])


## A picture's visible part as a texture of its own (the part is shares of the file, so it holds at any import size).
static func _part(texture: Texture2D, part: Rect2) -> AtlasTexture:
	var atlas: AtlasTexture = AtlasTexture.new()
	atlas.atlas = texture
	var size: Vector2 = texture.get_size()
	atlas.region = Rect2(part.position * size, part.size * size)
	atlas.filter_clip = true
	return atlas


func _add_tab(id: StringName, text: String, icon_kind: StringName) -> void:
	var button: Button = Button.new()
	button.name = "Tab%s" % String(id).capitalize()
	button.accessibility_name = text
	button.focus_mode = Control.FOCUS_ALL
	button.flat = true
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	button.pressed.connect(tab_pressed.emit.bind(id))
	UiKit.feel_press(button)
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = _part(PICTURES[icon_kind], PICTURE_PARTS[icon_kind])
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(picture)
	var label: Label = Label.new()
	label.name = "Name"
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(label, UiMetrics.CAPTION)
	button.add_child(label)
	add_child(button)
	tabs[id] = button
	_pictures[id] = picture
	_labels[id] = label
	_openness[id] = 0.0
	_order.append(id)


func _arrow(node_name: String, flip: bool, say: String) -> Button:
	var button: Button = Button.new()
	button.name = node_name
	button.accessibility_name = say
	button.flat = true
	button.focus_mode = Control.FOCUS_ALL
	button.visible = false
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	var arrow: TextureRect = TextureRect.new()
	arrow.name = "Arrow"
	arrow.texture = _part(ARROW, ARROW_PART)
	arrow.flip_h = flip
	arrow.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	arrow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	arrow.size = Vector2.ONE * ARROW_SIZE
	arrow.position = (Vector2.ONE * ARROW_TOUCH - arrow.size) / 2.0
	button.add_child(arrow)
	button.custom_minimum_size = Vector2.ONE * ARROW_TOUCH
	button.size = Vector2.ONE * ARROW_TOUCH
	UiKit.feel_press(button)
	button.pressed.connect(_step.bind(1 if flip else -1))
	add_child(button)
	return button


## The arrows go to the tab beside the open one.
func _step(way: int) -> void:
	var at: int = _order.find(active) + way
	if at >= 0 and at < _order.size():
		tab_pressed.emit(_order[at])


## Opens `id`: its picture grows and its name fades in while the others shrink, and the arrows slide to it: one smooth move.
func set_active(id: StringName) -> void:
	active = id
	if is_instance_valid(_tween):
		_tween.kill()
	if Settings.reduced_motion or not is_inside_tree() or size.x <= 0.0:
		for other: StringName in _order:
			_openness[other] = 1.0 if other == id else 0.0
		_layout()
		return
	_tween = create_tween().set_parallel(true)
	for other: StringName in _order:
		_tween.tween_method(_set_openness.bind(other), _openness[other], 1.0 if other == id else 0.0, GROW_TIME).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


func _set_openness(value: float, id: StringName) -> void:
	_openness[id] = value
	_layout()


## The width of every tab: equal, whichever tab is open (nothing restructures when the tab changes).
func slot_width() -> float:
	return size.x / float(maxi(_order.size(), 1))


## Places every picture, name and arrow for the current openness. The bar scales with its width over the design width.
func _layout() -> void:
	if _order.is_empty() or size.x <= 0.0:
		return
	var scale_to: float = size.x / 720.0
	var slot: float = slot_width()
	var total: float = 0.0
	var centre_sum: float = 0.0
	var right_share: float = 0.0
	var left_share: float = 0.0
	for i: int in _order.size():
		var id: StringName = _order[i]
		var open: float = _openness[id]
		var button: Button = tabs[id]
		button.position = Vector2(slot * float(i), 0.0)
		button.size = Vector2(slot, size.y)
		var edge: float = lerpf(ICON_IDLE, ICON_ACTIVE, open) * scale_to
		var picture: TextureRect = _pictures[id]
		picture.size = Vector2.ONE * edge
		# every picture is centred on the same line, so a growing one never pushes itself up or its neighbours aside
		picture.position = Vector2(slot / 2.0 - edge / 2.0, ICON_Y * scale_to - edge / 2.0)
		picture.modulate = IDLE_SHADE.lerp(Color.WHITE, open)
		var label: Label = _labels[id]
		label.size = Vector2(slot, LABEL_HEIGHT * scale_to)
		label.position = Vector2(0.0, (LABEL_Y + (1.0 - open) * LABEL_RISE) * scale_to)
		label.modulate.a = open
		label.visible = open > 0.01
		total += open
		centre_sum += open * slot * (float(i) + 0.5)
		if i > 0:
			left_share += open
		if i < _order.size() - 1:
			right_share += open
	_place_arrows(centre_sum, total, left_share, right_share, scale_to)


## The arrows follow the open tab's centre (a weighted one while two tabs are changing places) and fade by how much of the open
## tab has a neighbour on their side. They are floating controls: they never take room from the tabs.
func _place_arrows(centre_sum: float, total: float, left_share: float, right_share: float, scale_to: float) -> void:
	if total <= 0.001:
		arrow_left.visible = false
		arrow_right.visible = false
		return
	var centre: float = centre_sum / total
	var reach: float = (ICON_ACTIVE / 2.0 + ARROW_GAP + ARROW_SIZE / 2.0) * scale_to
	var y: float = ICON_Y * scale_to - ARROW_TOUCH / 2.0
	arrow_left.position = Vector2(centre - reach - ARROW_TOUCH / 2.0, y)
	arrow_right.position = Vector2(centre + reach - ARROW_TOUCH / 2.0, y)
	var left_alpha: float = left_share / total
	var right_alpha: float = right_share / total
	arrow_left.modulate.a = left_alpha
	arrow_right.modulate.a = right_alpha
	arrow_left.visible = left_alpha > 0.02
	arrow_right.visible = right_alpha > 0.02
