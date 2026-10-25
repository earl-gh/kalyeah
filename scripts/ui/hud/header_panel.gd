class_name HeaderPanel
extends Control
## The top of the match screen (owner: like Clash Royale): no deck, only the settings button, the score and the clock floating over the street
## on a soft dark shade. The camera reserves its height so the whole arena stays clear of them. It only draws; it never takes a touch.


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
	# no deck at the top (like Clash Royale): the pieces float over the street on a soft dark shade that fades out below them
	var w: float = size.x
	var h: float = size.y
	var dark: Color = Color(UiSkin.LINE_COLOR, 0.6)
	var clear: Color = Color(UiSkin.LINE_COLOR, 0.0)
	draw_polygon(PackedVector2Array([Vector2(0.0, 0.0), Vector2(w, 0.0), Vector2(w, h + 16.0), Vector2(0.0, h + 16.0)]), PackedColorArray([dark, dark, clear, clear]))
