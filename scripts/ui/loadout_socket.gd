class_name LoadoutSocket
extends Button
## A touch-sized replacement target; full weapon information stays on the weapon card.

var number: int = 1
var weapon: WeaponDef
var active: bool = false


func show_weapon(def: WeaponDef) -> void:
	weapon = def
	queue_redraw()


func _draw() -> void:
	draw_style_box(UiSkin.plate("picked" if active else "inset", 0.0, 22), Rect2(Vector2.ZERO, size))
	var font: Font = KalyeahTheme.BODY_FONT
	var icon: Texture2D = Icons.art(StringName("btn_" + String(weapon.id))) if weapon != null else null
	if icon != null:
		draw_texture_rect(icon, Rect2(12.0, 12.0, 76.0, 76.0), false)
	var caption: String = weapon.short_name if weapon != null else "Empty"
	draw_string(font, Vector2(102.0, 34.0), "SLOT %d" % number, HORIZONTAL_ALIGNMENT_LEFT, size.x - 110.0, 22, KalyeahTheme.AMBER if active else KalyeahTheme.TEXT_MUTED)
	draw_string(font, Vector2(102.0, 76.0), caption, HORIZONTAL_ALIGNMENT_LEFT, size.x - 110.0, 26, KalyeahTheme.TEXT)
