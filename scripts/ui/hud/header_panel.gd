class_name HeaderPanel
extends Control
## The top of the match screen, the same controller deck as the bottom turned over: graphite that is darkest at the screen's edge, rounded
## bottom corners with a thin lit lip along them and a soft shadow on the street. The settings button and the score sit on it. The arena
## stays below it (the camera reserves the same height). It only draws; it never takes a touch.


func _init() -> void:
	name = "Header"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)


## The header's height, from the very top of the screen (so a notch's inset is part of it).
func set_header_height(height: float) -> void:
	offset_top = 0.0
	offset_bottom = height
	queue_redraw()


func _draw() -> void:
	var w: float = size.x
	var h: float = size.y
	var corner: float = ConsolePanel.CORNER
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = ConsolePanel.BODY_TOP
	shell.corner_radius_bottom_left = int(corner)
	shell.corner_radius_bottom_right = int(corner)
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.4)
	shell.shadow_size = 16
	draw_style_box(shell, Rect2(0.0, -30.0, w, h + 30.0))
	# darkest at the screen's edge, lifting toward the lip
	var edge: Color = Color(ConsolePanel.BODY_BOTTOM, 1.0)
	var clear: Color = Color(ConsolePanel.BODY_BOTTOM, 0.0)
	draw_polygon(PackedVector2Array([Vector2(0.0, 0.0), Vector2(w, 0.0), Vector2(w, h - corner), Vector2(0.0, h - corner)]), PackedColorArray([edge, edge, clear, clear]))
	# the lit lip along the rounded bottom
	draw_arc(Vector2(corner, h - corner), corner - 1.5, PI * 0.5, PI, 16, ConsolePanel.LIP, 3.0, true)
	draw_arc(Vector2(w - corner, h - corner), corner - 1.5, 0.0, PI * 0.5, 16, ConsolePanel.LIP, 3.0, true)
	draw_line(Vector2(corner, h - 1.5), Vector2(w - corner, h - 1.5), ConsolePanel.LIP, 3.0, true)
