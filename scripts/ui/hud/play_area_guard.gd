class_name PlayAreaGuard
extends Control
## What the screen says while a held card is dragged outside the play area (the street): everything outside the street tinted red and the
## street's own edge outlined in red, with the aim shape on the ground red too, because letting go there throws nothing. It only draws; it
## never takes a touch. `show_area` gets the street's rect on the screen (viewport pixels), `hide_area` takes the tint away.

const TINT: Color = Color(1.0, 0.12, 0.08, 0.26)
const OUTLINE: Color = Color(1.0, 0.3, 0.25, 0.95)
const OUTLINE_WIDTH: float = 6.0

## The street on the screen while the warning shows (an empty rect: it does not).
var area: Rect2 = Rect2()


func _init() -> void:
	name = "PlayAreaGuard"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	visible = false


func showing() -> bool:
	return visible and area.size.x > 0.0 and area.size.y > 0.0


func show_area(street: Rect2) -> void:
	if street == area and visible:
		return
	area = street
	visible = street.size.x > 0.0 and street.size.y > 0.0
	queue_redraw()


func hide_area() -> void:
	if not visible:
		return
	area = Rect2()
	visible = false
	queue_redraw()


func _draw() -> void:
	if not showing():
		return
	var screen: Rect2 = Rect2(Vector2.ZERO, size)
	var inside: Rect2 = area.intersection(screen)
	# the four bands round the street (everything outside it), then its outline
	draw_rect(Rect2(screen.position, Vector2(screen.size.x, maxf(0.0, inside.position.y - screen.position.y))), TINT)
	draw_rect(Rect2(Vector2(screen.position.x, inside.end.y), Vector2(screen.size.x, maxf(0.0, screen.end.y - inside.end.y))), TINT)
	draw_rect(Rect2(Vector2(screen.position.x, inside.position.y), Vector2(maxf(0.0, inside.position.x - screen.position.x), inside.size.y)), TINT)
	draw_rect(Rect2(Vector2(inside.end.x, inside.position.y), Vector2(maxf(0.0, screen.end.x - inside.end.x), inside.size.y)), TINT)
	draw_rect(area, OUTLINE, false, OUTLINE_WIDTH)
