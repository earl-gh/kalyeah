class_name QrView
extends Control
## Draws a QR code (`QrCode`): dark modules on a white card with the 4-module light margin a camera needs, as big as the control
## allows and on whole pixels so every module is crisp.

const MARGIN_MODULES: int = 4
const DARK: Color = Color("1b1f2a")
const LIGHT: Color = Color.WHITE

var code: QrCode


func _init(text: String = "") -> void:
	name = "QrView"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	show_text(text)


## Shows the code for `text` ("" shows nothing).
func show_text(text: String) -> void:
	code = QrCode.encode(text) if not text.is_empty() else null
	queue_redraw()


## The size of one module on screen, in whole pixels.
func module_pixels() -> int:
	if code == null:
		return 0
	return maxi(1, floori(minf(size.x, size.y) / (code.size + MARGIN_MODULES * 2)))


func _draw() -> void:
	if code == null:
		return
	var unit: int = module_pixels()
	var side: int = unit * (code.size + MARGIN_MODULES * 2)
	var origin: Vector2 = ((size - Vector2(side, side)) / 2.0).floor()
	draw_rect(Rect2(origin, Vector2(side, side)), LIGHT)
	var start: Vector2 = origin + Vector2(unit, unit) * MARGIN_MODULES
	for y: int in code.size:
		for x: int in code.size:
			if code.is_dark(x, y):
				draw_rect(Rect2(start + Vector2(x * unit, y * unit), Vector2(unit, unit)), DARK)
