class_name LoadoutSocket
extends Button
## A touch-sized replacement target; full weapon information stays on the weapon card.

## The weapon's card picture, bare, on the slot's plate (a slot is `HEIGHT` tall; the card leaves `ART_PAD` above and below it).
const HEIGHT: float = 96.0
const ART_PAD: float = 8.0
const ART_LEFT: float = 12.0
const ART_WIDTH: float = 66.0
## Where the slot's words start.
const TEXT_LEFT: float = 102.0

var number: int = 1
var weapon: WeaponDef
var active: bool = false
var art: SkillCardArt


func _init() -> void:
	art = SkillCardArt.new()
	art.name = "Art"
	art.position = Vector2(ART_LEFT, ART_PAD)
	art.size = Vector2(ART_WIDTH, HEIGHT - ART_PAD * 2.0)
	add_child(art)


func show_weapon(def: WeaponDef) -> void:
	weapon = def
	art.show_weapon(def.id if def != null else &"")
	accessibility_name = "Slot %d: %s" % [number, def.display_name if def != null else "empty"]
	accessibility_description = "Tap to choose which slot the next weapon fills"
	queue_redraw()


func _draw() -> void:
	draw_style_box(UiSkin.plate("picked" if active else "inset", 0.0, UiSkin.RADIUS_L), Rect2(Vector2.ZERO, size))
	var font: Font = KalyeahTheme.BODY_FONT
	# the same name as on the tile; the size steps down (never below the text floor) until it fits
	var caption: String = weapon.display_name if weapon != null else "Empty"
	var room: float = size.x - TEXT_LEFT - 8.0
	var caption_size: int = UiMetrics.ACTION
	while caption_size > UiMetrics.MIN_TEXT and font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, caption_size).x > room:
		caption_size -= 1
	draw_string(font, Vector2(TEXT_LEFT, 34.0), "SLOT %d" % number, HORIZONTAL_ALIGNMENT_LEFT, room, UiMetrics.CAPTION, KalyeahTheme.AMBER if active else KalyeahTheme.TEXT_MUTED)
	draw_string(font, Vector2(TEXT_LEFT, 70.0), caption, HORIZONTAL_ALIGNMENT_LEFT, room, caption_size, KalyeahTheme.TEXT)
