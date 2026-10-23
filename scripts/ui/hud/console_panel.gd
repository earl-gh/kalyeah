class_name ConsolePanel
extends Control
## The bottom of the screen, a modern game controller deck: dark graphite that fades a little darker toward the screen's edge, a thin
## lit lip along its rounded top, and soft round wells where the D-pad and the two skill buttons sit. The arena stays above it
## (there are no cards to deploy here, so the space is the controller). It only draws; it never takes a touch.

const BODY_TOP: Color = RoyaleStyle.BLUE_TOP
const BODY_BOTTOM: Color = RoyaleStyle.BLUE_BOTTOM
const LIP: Color = RoyaleStyle.GOLD
const CORNER: float = 36.0
const HEALTH_FONT: int = 40
const HEALTH_TEXT: Color = Color.WHITE
const BAR_FONT: int = 28
## The bar keeps your own colour above this share of your health, turns amber below it and red below the second.
const HEALTH_LOW: float = 0.5
const HEALTH_CRITICAL: float = 0.25
const HEALTH_AMBER: Color = Color(1.0, 0.76, 0.2)
const HEALTH_RED: Color = Color(1.0, 0.3, 0.28)

## Round recesses {centre (from this panel's top-left), radius}: set by the HUD's layout.
var sockets: Array[Dictionary] = []
## The speaker slots sit toward this side (true = the right).
var slots_right: bool = true
## Your health, shown in the empty middle of the shell: 0 to 1, the number, and whether it shows at all (not while tagged).
var health_fraction: float = 1.0
var health_text: String = ""
var health_shown: bool = false
## The wells show only while the controls do (not while you wait in the box, when the deck carries a message instead).
var wells_shown: bool = true
## Cards mode (GameRules.card_cycle): the bottom is a card tray, not a controller. Where the two hand cards, the shuffler's window and the
## health bar go (this panel's own coordinates), set by the HUD's layout.
var cards_mode: bool = false
var hand_rects: Array[Rect2] = []
var shuffler_rect: Rect2 = Rect2()
var health_rect: Rect2 = Rect2()


## Shows or hides the wells under the controls.
func set_wells_shown(shown: bool) -> void:
	if shown == wells_shown:
		return
	wells_shown = shown
	queue_redraw()


## Cards mode: the layout of the tray (see `cards_mode`).
func set_card_layout(hands: Array[Rect2], shuffler: Rect2, health: Rect2) -> void:
	cards_mode = true
	hand_rects = hands
	shuffler_rect = shuffler
	health_rect = health
	queue_redraw()


func set_sockets(list: Array[Dictionary], right: bool) -> void:
	sockets = list
	slots_right = right
	queue_redraw()


## Your health in the middle of the console, between the pad and the buttons, where the thumbs do not cover it.
func set_health(hp: int, max_hp: int, shown: bool) -> void:
	var fraction: float = clampf(float(hp) / maxf(float(max_hp), 1.0), 0.0, 1.0)
	var text: String = str(hp)
	if shown == health_shown and text == health_text and is_equal_approx(fraction, health_fraction):
		return
	health_shown = shown
	health_text = text
	health_fraction = fraction
	queue_redraw()


## The free stretch of the shell between the pad's socket and the nearest weapon socket: (centre x, width). Zero width without sockets.
func gap() -> Vector2:
	if sockets.size() < 2:
		return Vector2.ZERO
	var pad: Dictionary = sockets[0]
	var pad_x: float = (pad["at"] as Vector2).x
	var pad_edge: float = pad_x
	var best: float = INF
	var near_edge: float = pad_x
	for index: int in range(1, sockets.size()):
		var socket: Dictionary = sockets[index]
		var x: float = (socket["at"] as Vector2).x
		if absf(x - pad_x) < best:
			best = absf(x - pad_x)
			near_edge = x - signf(x - pad_x) * (socket["radius"] as float)
	pad_edge = pad_x + signf(near_edge - pad_x) * (pad["radius"] as float)
	return Vector2((pad_edge + near_edge) / 2.0, absf(near_edge - pad_edge))


## The colour of your health bar: your own colour while healthy, amber below half, red below a quarter.
static func health_color(fraction: float) -> Color:
	if fraction >= HEALTH_LOW:
		return TeamColors.bar_self()
	return HEALTH_AMBER if fraction > HEALTH_CRITICAL else HEALTH_RED


## Your health on a dark plate in the middle of the deck, like the elixir bar: a heart and the number over a chunky bar marked in tenths
## that turns amber, then red, as it empties.
func _draw_health() -> void:
	var free: Vector2 = gap()
	if not health_shown or free.y < 90.0:
		return
	var width: float = minf(free.y - 16.0, 150.0)
	var plate: Rect2 = Rect2(Vector2(free.x - width / 2.0, 52.0), Vector2(width, 112.0))
	RoyaleStyle.chunky(self, plate, RoyaleStyle.BLUE_WELL, 24, false)
	var font: Font = KalyeahTheme.ACTION_FONT
	var label_size: Vector2 = font.get_string_size(health_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HEALTH_FONT)
	var heart: float = 15.0
	var row_width: float = heart * 2.0 + 10.0 + label_size.x
	var row_left: float = free.x - row_width / 2.0
	var color: Color = health_color(health_fraction)
	_draw_heart(Vector2(row_left + heart, plate.position.y + 38.0), heart, color)
	RoyaleStyle.lettered(self, font, health_text, Vector2(row_left + heart * 2.0 + 10.0, plate.position.y + 38.0 + label_size.y * 0.32), HEALTH_FONT, HEALTH_TEXT)
	var track: Rect2 = Rect2(plate.position + Vector2(16.0, 76.0), Vector2(plate.size.x - 32.0, 20.0))
	var well: StyleBoxFlat = StyleBoxFlat.new()
	well.bg_color = Color(0.02, 0.06, 0.17)
	well.border_color = RoyaleStyle.NAVY
	well.set_border_width_all(3)
	well.set_corner_radius_all(10)
	well.anti_aliasing = true
	draw_style_box(well, track.grow(3.0))
	if health_fraction > 0.0:
		var fill: StyleBoxFlat = StyleBoxFlat.new()
		fill.bg_color = color
		fill.set_corner_radius_all(8)
		fill.anti_aliasing = true
		draw_style_box(fill, Rect2(track.position, Vector2(maxf(track.size.x * health_fraction, 16.0), track.size.y)))
		var sheen: StyleBoxFlat = StyleBoxFlat.new()
		sheen.bg_color = Color(1.0, 1.0, 1.0, 0.35)
		sheen.set_corner_radius_all(5)
		draw_style_box(sheen, Rect2(track.position + Vector2(4.0, 2.0), Vector2(maxf(track.size.x * health_fraction - 8.0, 8.0), 5.0)))
	for tenth: int in range(1, 10):
		var x: float = track.position.x + track.size.x * float(tenth) / 10.0
		draw_line(Vector2(x, track.position.y + 2.0), Vector2(x, track.end.y - 2.0), Color(0.0, 0.0, 0.0, 0.3), 2.0)


## A heart centred on `at`, `radius` wide from its middle to a side: two lobes and a point.
func _draw_heart(at: Vector2, radius: float, color: Color) -> void:
	var lobe: float = radius * 0.56
	draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	draw_colored_polygon(PackedVector2Array([at + Vector2(-radius - 1.0, -radius * 0.12), at + Vector2(radius + 1.0, -radius * 0.12), at + Vector2(0.0, radius + 3.0)]), Icons.OUTLINE)
	draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	draw_colored_polygon(PackedVector2Array([at + Vector2(-radius, -radius * 0.12), at + Vector2(radius, -radius * 0.12), at + Vector2(0.0, radius)]), color)


## The card tray (cards mode): a dark navy strip under the street with a gold line along its top, a darker tray behind the hand and the
## shuffler like Clash Royale's card row, and your health along the bottom edge like its elixir bar.
func _draw_tray() -> void:
	var w: float = size.x
	var h: float = size.y
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = Color(0.04, 0.09, 0.22)
	shell.corner_radius_top_left = 28
	shell.corner_radius_top_right = 28
	shell.border_color = RoyaleStyle.NAVY
	shell.border_width_top = 5
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.5)
	shell.shadow_size = 16
	draw_style_box(shell, Rect2(0.0, 0.0, w, h + 30.0))
	draw_line(Vector2(28.0, 7.0), Vector2(w - 28.0, 7.0), Color(RoyaleStyle.GOLD, 0.8), 3.0, true)
	var shade_top: Color = Color(0.1, 0.2, 0.45, 0.0)
	var shade_bottom: Color = Color(0.1, 0.2, 0.45, 0.35)
	draw_polygon(PackedVector2Array([Vector2(0.0, 24.0), Vector2(w, 24.0), Vector2(w, h), Vector2(0.0, h)]), PackedColorArray([shade_top, shade_top, shade_bottom, shade_bottom]))
	if wells_shown and not hand_rects.is_empty():
		var tray: Rect2 = shuffler_rect.merge(hand_rects[0]).merge(hand_rects[hand_rects.size() - 1]).grow(14.0)
		var back: StyleBoxFlat = StyleBoxFlat.new()
		back.bg_color = Color(0.0, 0.0, 0.0, 0.3)
		back.border_color = Color(RoyaleStyle.BLUE_LIGHT, 0.18)
		back.set_border_width_all(2)
		back.set_corner_radius_all(22)
		back.anti_aliasing = true
		draw_style_box(back, tray)
	_draw_bar()


## Your health as a long bar along the bottom (like the elixir bar): a heart, then a bar marked in tenths with the number in it; amber
## below half and red below a quarter.
func _draw_bar() -> void:
	if not health_shown or health_rect.size.x <= 0.0:
		return
	var color: Color = health_color(health_fraction)
	var heart: float = health_rect.size.y * 0.42
	_draw_heart(Vector2(health_rect.position.x + heart + 2.0, health_rect.get_center().y - 2.0), heart, color)
	var track: Rect2 = Rect2(health_rect.position + Vector2(heart * 2.0 + 14.0, 0.0), Vector2(health_rect.size.x - heart * 2.0 - 14.0, health_rect.size.y))
	var well: StyleBoxFlat = StyleBoxFlat.new()
	well.bg_color = Color(0.02, 0.05, 0.14)
	well.border_color = RoyaleStyle.NAVY
	well.set_border_width_all(4)
	well.set_corner_radius_all(int(track.size.y / 2.0))
	well.anti_aliasing = true
	draw_style_box(well, track)
	var inner: Rect2 = track.grow(-5.0)
	if health_fraction > 0.0:
		var fill: StyleBoxFlat = StyleBoxFlat.new()
		fill.bg_color = color
		fill.set_corner_radius_all(int(inner.size.y / 2.0))
		fill.anti_aliasing = true
		draw_style_box(fill, Rect2(inner.position, Vector2(maxf(inner.size.x * health_fraction, inner.size.y), inner.size.y)))
		var sheen: StyleBoxFlat = StyleBoxFlat.new()
		sheen.bg_color = Color(1.0, 1.0, 1.0, 0.3)
		sheen.set_corner_radius_all(4)
		draw_style_box(sheen, Rect2(inner.position + Vector2(6.0, 3.0), Vector2(maxf(inner.size.x * health_fraction - 12.0, 6.0), 5.0)))
	for tenth: int in range(1, 10):
		var x: float = inner.position.x + inner.size.x * float(tenth) / 10.0
		draw_line(Vector2(x, inner.position.y + 3.0), Vector2(x, inner.end.y - 3.0), Color(0.0, 0.0, 0.0, 0.25), 2.0)
	var font: Font = KalyeahTheme.ACTION_FONT
	var number_size: Vector2 = font.get_string_size(health_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, BAR_FONT)
	RoyaleStyle.lettered(self, font, health_text, Vector2(track.get_center().x - number_size.x / 2.0, track.get_center().y + BAR_FONT * 0.35), BAR_FONT)


func _draw() -> void:
	if cards_mode:
		_draw_tray()
		return
	var w: float = size.x
	var h: float = size.y
	# the deck: royal blue, a thick navy outline along its rounded top, a lighter top and a darker bottom, a gold trim and a sheen
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = BODY_BOTTOM
	shell.corner_radius_top_left = int(CORNER)
	shell.corner_radius_top_right = int(CORNER)
	shell.border_color = RoyaleStyle.NAVY
	shell.border_width_top = RoyaleStyle.OUTLINE + 2
	shell.border_width_left = RoyaleStyle.OUTLINE + 2
	shell.border_width_right = RoyaleStyle.OUTLINE + 2
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.5)
	shell.shadow_size = 18
	draw_style_box(shell, Rect2(0.0, 0.0, w, h + 30.0))
	var inset: float = float(RoyaleStyle.OUTLINE + 2)
	var top_c: Color = BODY_TOP
	var bottom_c: Color = BODY_BOTTOM
	draw_polygon(PackedVector2Array([Vector2(inset, CORNER), Vector2(w - inset, CORNER), Vector2(w - inset, h * 0.7), Vector2(inset, h * 0.7)]),
		PackedColorArray([top_c, top_c, bottom_c, bottom_c]))
	draw_polygon(PackedVector2Array([Vector2(inset + 4.0, inset), Vector2(w - inset - 4.0, inset), Vector2(w - inset, CORNER), Vector2(inset, CORNER)]),
		PackedColorArray([top_c, top_c, top_c, top_c]))
	# the gold trim along the top, and a light sheen under it
	draw_line(Vector2(CORNER, inset + 5.0), Vector2(w - CORNER, inset + 5.0), LIP, 5.0, true)
	draw_line(Vector2(CORNER, inset + 12.0), Vector2(w - CORNER, inset + 12.0), Color(1.0, 1.0, 1.0, 0.22), 3.0, true)
	for socket: Dictionary in sockets:
		if not wells_shown:
			break
		RoyaleStyle.well(self, socket["at"] as Vector2, socket["radius"] as float)
	_draw_health()
