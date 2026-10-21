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


## Shows or hides the wells under the controls.
func set_wells_shown(shown: bool) -> void:
	if shown == wells_shown:
		return
	wells_shown = shown
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


func _draw() -> void:
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
