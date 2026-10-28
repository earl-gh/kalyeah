class_name UiTabBar
extends Control
## The main screen's bottom navigation, in the style of Clash Royale's: a bar of flat, dark slate-blue slots drawn in code along the
## bottom, split by thin dark lines under a dark top edge, the open tab's slot lit steel blue, with one picture per destination (Street,
## Play, Settings). The strip of tabs is centred with a margin each side; the open tab is the wide one (room for its arrows).
## The open tab's picture grows and rises above the top of the bar (like the reference's) and its name fades in under it, and two small arrows float over the
## bar, a little way out from the picture, to go to the neighbouring tab (the leftmost tab has only a right arrow, the rightmost
## only a left one). Changing tab is one smooth move: the picture grows and shrinks, the name fades, the arrows slide to the new tab.
## The pictures are the owner's files in `assets/ui/nav/`.

signal tab_pressed(id: StringName)

## How tall the bar is at the design width (the reference's bar is about a fifth as tall as it is wide).
const HEIGHT: float = 164.0
## The pictures' sizes (design pixels) for the open and an idle tab, and the line the idle ones are centred on (the middle of the bar:
## `BAR_TOP` to `HEIGHT`), from the top of this control.
const ICON_ACTIVE: float = 96.0
const ICON_IDLE: float = 66.0
const ICON_Y: float = (BAR_TOP + HEIGHT) / 2.0
## Where the bar itself starts, from the top of this control: the space above is clear, and the open tab's picture is centred on this
## edge (half above it, half in it), like the reference's.
const BAR_TOP: float = 56.0
## The open tab's name sits under its picture, centred between the picture's bottom (about `BAR_TOP` + 44) and the bar's bottom (it rises a few pixels into place as it fades in).
const LABEL_Y: float = 116.0
const LABEL_HEIGHT: float = 32.0
const LABEL_RISE: float = 8.0
## The arrows: how big they are drawn, how big they are to the thumb, and how far from the picture's edge.
const ARROW_SIZE: float = 26.0
const ARROW_TOUCH: float = 88.0
const ARROW_GAP: float = 22.0
const IDLE_SHADE: Color = Color(0.82, 0.82, 0.86)
## The bar's colours (sampled from the reference): a slot is lighter at the top; the open one is steel blue, the others dark slate.
const SLOT_IDLE_TOP: Color = Color("62759a")
const SLOT_IDLE_BOTTOM: Color = Color("3b5070")
const SLOT_OPEN_TOP: Color = Color("96b0d0")
const SLOT_OPEN_BOTTOM: Color = Color("587a9e")
## The tabs are centred with this much margin on each side (design pixels), and the open tab is this wide (room for its two arrows):
## the idle tabs share what is left. The margins are coloured like an idle tab. The sizes above (pictures, arrows, the open tab's width)
## are for a strip `STRIP_DESIGN` wide; with a bigger margin they all scale by what is left over (`_content_fit`).
const SIDE_MARGIN: float = 65.0
const OPEN_WIDTH: float = 220.0
const STRIP_DESIGN: float = 640.0
## The dark top edge (with a thin light line under it) and the lines between the slots, in design pixels.
const EDGE_COLOR: Color = Color("0c1a36")
const EDGE_HEIGHT: float = 5.0
const EDGE_LIGHT: Color = Color(0.78, 0.86, 0.97, 0.5)
const EDGE_LIGHT_HEIGHT: float = 2.0
const DIVIDER_COLOR: Color = Color(0.04, 0.09, 0.2, 0.85)
const DIVIDER_WIDTH: float = 3.0
const DIVIDER_LIGHT: Color = Color(0.7, 0.8, 0.95, 0.16)
const DIVIDER_LIGHT_WIDTH: float = 1.5
## The depth: a shadow cast up from the bar over the screen, shades inside the bar, and the lit slot's sheen and outline.
const SHADOW_HEIGHT: float = 36.0
const SHADOW_COLOR: Color = Color(0.0, 0.02, 0.08, 0.5)
const BOTTOM_SHADE_HEIGHT: float = 30.0
const BOTTOM_SHADE_ALPHA: float = 0.3
const INNER_SHADE_WIDTH: float = 12.0
const INNER_SHADE_ALPHA: float = 0.25
const PICTURE_SHADOW_ALPHA: float = 0.3
const PICTURE_SHADOW_SQUASH: float = 0.22
const GLOSS_ALPHA: float = 0.26
const LIT_OUTLINE: float = 2.0
const LIT_OUTLINE_ALPHA: float = 0.45
## The time a change of tab takes (instant with Reduce motion).
const GROW_TIME: float = 0.28

const ARROW: Texture2D = preload("res://assets/ui/nav/nav_arrow.png")
const PICTURES: Dictionary[StringName, Texture2D] = {
	&"street": preload("res://assets/ui/nav/nav_street.webp"),
	&"cards": preload("res://assets/ui/nav/nav_cards.webp"),
	&"play": preload("res://assets/ui/nav/nav_play.webp"),
	&"history": preload("res://assets/ui/nav/nav_history.webp"),
	&"settings": preload("res://assets/ui/nav/nav_settings.png"),
}
## The visible part of each picture inside its file, as shares of the file (the files have clear margins and their art is not
## centred), so the bar can place the art itself: {x, y, width, height}.
const ARROW_PART: Rect2 = Rect2(0.29538, 0.18151, 0.52537, 0.63923)
const PICTURE_PARTS: Dictionary[StringName, Rect2] = {
	&"street": Rect2(0.08453, 0.06459, 0.84927, 0.86124),
	&"cards": Rect2(0.05821, 0.12121, 0.89075, 0.82137),
	&"play": Rect2(0.14317, 0.12514, 0.85683, 0.80158),
	&"history": Rect2(0.17943, 0.07097, 0.64115, 0.87321),
	&"settings": Rect2(0.11387, 0.11387, 0.78803, 0.79061),
}

var tabs: Dictionary[StringName, Button] = {}
var active: StringName = &""
## The arrows floating beside the open tab: to the tab on its left and on its right (faded out at the ends).
var arrow_left: Button
var arrow_right: Button

var _order: Array[StringName] = []
## Each tab's slot (where it starts and how wide it is), set by `_place_slots`.
var _slot_x: Array[float] = []
var _slot_w: Array[float] = []
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
	for item: Dictionary in items:
		_add_tab(item["id"] as StringName, String(item["label"]), item["icon"] as StringName)
	# added last, so they float over the tabs
	arrow_left = _arrow("ArrowLeft", false, "Previous tab")
	arrow_right = _arrow("ArrowRight", true, "Next tab")
	resized.connect(_layout)


## The bar, drawn in code (the open tab's slot lights smoothly as it opens): a soft shadow cast up over the screen, the slots with
## their depth (each edge shaded in, the lit one glossy and outlined), bevelled dividers, a dark top rim with a light line, and the
## bottom fading darker.
func _draw() -> void:
	if _order.is_empty() or size.x <= 0.0 or _slot_x.size() != _order.size():
		return
	var unit: float = size.x / 720.0
	var top_y: float = BAR_TOP * unit
	var height: float = size.y - top_y
	_shade_down(Rect2(0.0, top_y - SHADOW_HEIGHT * unit, size.x, SHADOW_HEIGHT * unit), SHADOW_COLOR, true)
	# the whole bar, margins included, is an idle tab's colour; every tab's slot is drawn over it, the lit one across its whole width
	_fill(Rect2(0.0, top_y, size.x, height), SLOT_IDLE_TOP, SLOT_IDLE_BOTTOM)
	for i: int in _order.size():
		var open: float = _openness[_order[i]]
		var x: float = _slot_x[i]
		var slot: float = _slot_w[i]
		var rect: Rect2 = Rect2(x, top_y, slot, height)
		_fill(rect, SLOT_IDLE_TOP.lerp(SLOT_OPEN_TOP, open), SLOT_IDLE_BOTTOM.lerp(SLOT_OPEN_BOTTOM, open))
		# the lit slot is glossy: a light sheen over its upper half, and a light outline down each side
		_fill(Rect2(x, top_y, slot, height * 0.5), Color(1.0, 1.0, 1.0, GLOSS_ALPHA * open), Color(1.0, 1.0, 1.0, 0.0))
		draw_rect(Rect2(x, top_y, LIT_OUTLINE * unit, height), Color(1.0, 1.0, 1.0, LIT_OUTLINE_ALPHA * open))
		draw_rect(Rect2(x + slot - LIT_OUTLINE * unit, top_y, LIT_OUTLINE * unit, height), Color(1.0, 1.0, 1.0, LIT_OUTLINE_ALPHA * open))
		# every slot's sides are shaded in a little, so the slots read as set into the bar
		var inner: float = INNER_SHADE_WIDTH * unit
		var strength: float = 1.0 - 0.6 * open
		_shade_across(Rect2(x, top_y, inner, height), Color(0.0, 0.03, 0.1, INNER_SHADE_ALPHA * strength), true)
		_shade_across(Rect2(x + slot - inner, top_y, inner, height), Color(0.0, 0.03, 0.1, INNER_SHADE_ALPHA * strength), false)
	for i: int in _order.size() + 1:
		var line: float = _slot_x[0] if i == 0 else _slot_x[i - 1] + _slot_w[i - 1]
		draw_rect(Rect2(line - DIVIDER_WIDTH * unit / 2.0, top_y, DIVIDER_WIDTH * unit, height), DIVIDER_COLOR)
		draw_rect(Rect2(line + DIVIDER_WIDTH * unit / 2.0, top_y, DIVIDER_LIGHT_WIDTH * unit, height), DIVIDER_LIGHT)
	# a soft shadow under every picture (they are drawn after this, over it)
	for id: StringName in _order:
		var picture: TextureRect = _pictures[id]
		var foot: Vector2 = tabs[id].position + picture.position + Vector2(picture.size.x / 2.0, picture.size.y * 0.9)
		draw_set_transform(foot, 0.0, Vector2(1.0, PICTURE_SHADOW_SQUASH))
		draw_circle(Vector2.ZERO, picture.size.x * 0.36, Color(0.0, 0.02, 0.08, PICTURE_SHADOW_ALPHA))
		draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	# the top: a dark rim and its light line
	draw_rect(Rect2(0.0, top_y, size.x, EDGE_HEIGHT * unit), EDGE_COLOR)
	draw_rect(Rect2(0.0, top_y + EDGE_HEIGHT * unit, size.x, EDGE_LIGHT_HEIGHT * unit), EDGE_LIGHT)
	# and the bottom grows darker
	_shade_down(Rect2(0.0, size.y - BOTTOM_SHADE_HEIGHT * unit, size.x, BOTTOM_SHADE_HEIGHT * unit), Color(0.0, 0.02, 0.08, BOTTOM_SHADE_ALPHA), true)


## A rectangle filled with a gradient from `top` to `bottom`.
func _fill(rect: Rect2, top: Color, bottom: Color) -> void:
	draw_polygon(PackedVector2Array([rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]), PackedColorArray([top, top, bottom, bottom]))


## A shade over a rectangle that is `color` at one end and clear at the other: `clear_at_top` true is clear at the top (it grows toward
## the bottom), false is clear at the bottom.
func _shade_down(rect: Rect2, color: Color, clear_at_top: bool) -> void:
	var clear: Color = Color(color, 0.0)
	_fill(rect, clear if clear_at_top else color, color if clear_at_top else clear)


## The same sideways: `strong_at_left` true is strongest at the left edge and clear at the right.
func _shade_across(rect: Rect2, color: Color, strong_at_left: bool) -> void:
	var clear: Color = Color(color, 0.0)
	var left: Color = color if strong_at_left else clear
	var right: Color = clear if strong_at_left else color
	draw_polygon(PackedVector2Array([rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]), PackedColorArray([left, right, right, left]))


## One of the bar's pictures (&"street", &"cards", &"play", &"history", &"settings") as a texture of its own: the same art elsewhere, like the gear in a match.
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


## How much the sizes of the pictures, arrows and the open tab shrink (or grow) with the side margin.
static func _content_fit() -> float:
	return (720.0 - 2.0 * SIDE_MARGIN) / STRIP_DESIGN


## Where each tab's slot starts and how wide it is, for the current openness: the open tab is `OPEN_WIDTH` wide and the others share the
## rest of the width between the side margins (the strip always fills the same width, so nothing outside it moves).
func _place_slots(scale_to: float) -> void:
	var count: int = maxi(_order.size(), 1)
	var avail: float = size.x - 2.0 * SIDE_MARGIN * scale_to
	var open_width: float = minf(OPEN_WIDTH * _content_fit() * scale_to, avail)
	var idle_width: float = (avail - open_width) / float(maxi(count - 1, 1))
	_slot_x.clear()
	_slot_w.clear()
	var x: float = SIDE_MARGIN * scale_to
	for id: StringName in _order:
		var width: float = lerpf(idle_width, open_width, _openness[id])
		_slot_x.append(x)
		_slot_w.append(width)
		x += width


## Places every picture, name and arrow for the current openness. The bar scales with its width over the design width.
func _layout() -> void:
	if _order.is_empty() or size.x <= 0.0:
		return
	queue_redraw()
	var scale_to: float = size.x / 720.0
	_place_slots(scale_to)
	var unit: float = scale_to * _content_fit()
	var total: float = 0.0
	var centre_sum: float = 0.0
	var right_share: float = 0.0
	var left_share: float = 0.0
	for i: int in _order.size():
		var id: StringName = _order[i]
		var open: float = _openness[id]
		var button: Button = tabs[id]
		var slot: float = _slot_w[i]
		button.position = Vector2(_slot_x[i], 0.0)
		button.size = Vector2(slot, size.y)
		var edge: float = lerpf(ICON_IDLE, ICON_ACTIVE, open) * unit
		var picture: TextureRect = _pictures[id]
		picture.size = Vector2.ONE * edge
		# the open picture grows and rises above the bar, and its slot is the wide one
		picture.position = Vector2(slot / 2.0 - edge / 2.0, lerpf(ICON_Y * scale_to, BAR_TOP * scale_to, open) - edge / 2.0)
		picture.modulate = IDLE_SHADE.lerp(Color.WHITE, open)
		var label: Label = _labels[id]
		label.size = Vector2(slot, LABEL_HEIGHT * scale_to)
		label.position = Vector2(0.0, (LABEL_Y + (1.0 - open) * LABEL_RISE) * scale_to)
		label.modulate.a = open
		label.visible = open > 0.01
		total += open
		centre_sum += open * (_slot_x[i] + slot / 2.0)
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
	var unit: float = scale_to * _content_fit()
	var reach: float = (ICON_ACTIVE / 2.0 + ARROW_GAP + ARROW_SIZE / 2.0) * unit
	# the bottom of each arrow (not its middle) is level with the bottom of the open picture
	var y: float = BAR_TOP * scale_to + (ICON_ACTIVE - ARROW_SIZE) * unit / 2.0 - ARROW_TOUCH / 2.0
	for arrow: Button in [arrow_left, arrow_right]:
		var drawn: Control = arrow.get_node("Arrow") as Control
		drawn.size = Vector2.ONE * ARROW_SIZE * unit
		drawn.position = (Vector2.ONE * ARROW_TOUCH - drawn.size) / 2.0
	arrow_left.position = Vector2(centre - reach - ARROW_TOUCH / 2.0, y)
	arrow_right.position = Vector2(centre + reach - ARROW_TOUCH / 2.0, y)
	var left_alpha: float = left_share / total
	var right_alpha: float = right_share / total
	arrow_left.modulate.a = left_alpha
	arrow_right.modulate.a = right_alpha
	arrow_left.visible = left_alpha > 0.02
	arrow_right.visible = right_alpha > 0.02
