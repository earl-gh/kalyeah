class_name PencilIcon
extends Control
## A pencil on a round blue badge, drawn in code (the edit icon of the name tag). A stand-in until the owner makes one: it is a flat 2D
## icon, so it should be a 2D image when it is made. Scales with its size.

const BADGE: Color = KalyeahTheme.BLUE
const BADGE_RIM: Color = Color("0a5fc4")
const BADGE_SHINE: Color = Color(1.0, 1.0, 1.0, 0.28)
const BODY: Color = Color("ffc233")
const BODY_SHADE: Color = Color("e09a10")
const WOOD: Color = Color("f6d9ae")
const LEAD: Color = Color("2b3350")
const ERASER: Color = Color("ff7a70")
const FERRULE: Color = Color("c9d2e3")


func _draw() -> void:
	var edge: float = minf(size.x, size.y)
	if edge < 4.0:
		return
	var centre: Vector2 = size / 2.0
	var radius: float = edge / 2.0
	draw_circle(centre + Vector2(0.0, radius * 0.06), radius, BADGE_RIM)
	draw_circle(centre, radius * 0.94, BADGE)
	draw_circle(centre + Vector2(-radius * 0.18, -radius * 0.3), radius * 0.5, BADGE_SHINE)
	# the pencil lies at 45 degrees, tip down-left: drawn upright in its own space, then turned
	draw_set_transform(centre + Vector2(radius * 0.04, radius * 0.0), deg_to_rad(45.0), Vector2.ONE * (radius / 100.0))
	var half: float = 17.0
	draw_rect(Rect2(-half, -62.0, half * 2.0, 18.0), ERASER)
	draw_rect(Rect2(-half, -44.0, half * 2.0, 9.0), FERRULE)
	draw_rect(Rect2(-half, -35.0, half * 2.0, 62.0), BODY)
	draw_rect(Rect2(half - 9.0, -35.0, 9.0, 62.0), BODY_SHADE)
	draw_colored_polygon(PackedVector2Array([Vector2(-half, 27.0), Vector2(half, 27.0), Vector2(0.0, 60.0)]), WOOD)
	draw_colored_polygon(PackedVector2Array([Vector2(-6.0, 48.0), Vector2(6.0, 48.0), Vector2(0.0, 60.0)]), LEAD)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
