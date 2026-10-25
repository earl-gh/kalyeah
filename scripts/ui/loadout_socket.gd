class_name LoadoutSocket
extends Button
## A touch-sized replacement target; full weapon information stays on the weapon card.

var number: int = 1
var weapon: WeaponDef
var active: bool = false


func show_weapon(def: WeaponDef) -> void:
	weapon = def
	accessibility_name = "Slot %d: %s" % [number, def.display_name if def != null else "empty"]
	accessibility_description = "Tap to choose which slot the next weapon fills"
	queue_redraw()


func _draw() -> void:
	draw_style_box(UiSkin.plate("picked" if active else "inset", 0.0, UiSkin.RADIUS_M), Rect2(Vector2.ZERO, size))
	var font: Font = KalyeahTheme.BODY_FONT
	var icon: Texture2D = Icons.art(StringName("btn_" + String(weapon.id))) if weapon != null else null
	if icon != null:
		draw_texture_rect(icon, Rect2(10.0, 10.0, 76.0, 76.0), false)
	# the same name as on the tile; the size steps down (never below the text floor) until it fits
	var caption: String = weapon.display_name if weapon != null else "Empty"
	var room: float = size.x - 110.0
	var caption_size: int = 26
	while caption_size > UiMetrics.MIN_TEXT and font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, caption_size).x > room:
		caption_size -= 1
	draw_string(font, Vector2(102.0, 34.0), "SLOT %d" % number, HORIZONTAL_ALIGNMENT_LEFT, room, 22, KalyeahTheme.AMBER if active else KalyeahTheme.TEXT_MUTED)
	draw_string(font, Vector2(102.0, 70.0), caption, HORIZONTAL_ALIGNMENT_LEFT, room, caption_size, KalyeahTheme.TEXT)
